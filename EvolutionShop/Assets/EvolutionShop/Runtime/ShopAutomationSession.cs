using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopAutomation Automation { get; private set; } = new ShopAutomation();
        public bool AutomationBlockedBySave { get; private set; }
        float automationClock;

        void RestoreAutomation(AutomationSave saved)
        {
            if (!ShopAutomation.TryRestore(saved, Model, out var automation))
                throw new InvalidOperationException("Validated slot has invalid culture preparation settings.");
            Automation = automation;
            // In-session reload cannot bypass a write-failure latch; Retry is explicit.
            ResetAutomationClock();
        }
        void ResetAutomationClock() => automationClock = 0;

        bool ActiveAutomationStation(Station station)
        {
            if (!station || !station.isActiveAndEnabled) return false;
            foreach (var collider in station.GetComponentsInChildren<Collider>())
                if (collider.enabled && collider.gameObject.activeInHierarchy) return true;
            return false;
        }
        bool HasAutomationStation(StationKind kind)
        {
            if (!World || !World.isActiveAndEnabled) return false;
            foreach (var station in World.Stations)
                if (ActiveAutomationStation(station) && station.Kind == kind) return true;
            return false;
        }
        bool AtAutomationStation(StationKind kind)
        {
            if (!isActiveAndEnabled || !World || Paused || PlacementActive || AutosaveBlocked) return false;
            foreach (var station in World.Stations)
                if (ActiveAutomationStation(station) && station.Kind == kind && AtStockStation(station)) return true;
            return false;
        }
        bool AutomationControlsAvailable(bool cancel = false)
        {
            bool bench = Panel == ShopPanel.Automation && AtAutomationStation(StationKind.Upgrade);
            bool incubator = cancel && Panel == ShopPanel.Incubator && AtAutomationStation(StationKind.Incubator);
            if (bench || incubator) return true;
            Notify("Use culture preparation at the physical lab bench.");
            return false;
        }
        public bool OpenAutomationMenu()
        {
            if ((Panel != ShopPanel.Upgrade && Panel != ShopPanel.Automation) || !AtAutomationStation(StationKind.Upgrade))
            { Notify("Use E at the lab bench, then choose culture preparation."); return false; }
            SetPanel(ShopPanel.Automation);
            return true;
        }
        public bool PurchaseAutomationModule(bool collector)
        {
            if (!AutomationControlsAvailable()) return false;
            if (Cycle.Phase != ShopDayPhase.Night || Customers.ActiveCount != 0)
            { Notify("Buy lab modules during a clear preparation night."); return false; }
            return ChangeAutomation((automation, game) => automation.Purchase(game, collector),
                collector ? "Collector installed, switched off. Choose a recipe and enable it." : "Doser installed, switched off. Choose a recipe and enable it.");
        }
        public bool SetAutomationEnabled(bool collector, bool enabled) => AutomationControlsAvailable() &&
            ChangeAutomation((automation, game) => automation.SetEnabled(collector, enabled),
                enabled ? "Module enabled. Request one batch when ready." : "Module switched off. Existing cultures are safe.");
        public bool SelectAutomationRecipe(string id) => AutomationControlsAvailable() &&
            ChangeAutomation((automation, game) => automation.SelectRecipe(game, id), "Recipe selected. Request one batch when ready.");
        public bool RequestAutomationBatch()
        {
            if (!AutomationControlsAvailable()) return false;
            string request = Guid.NewGuid().ToString("N");
            return ChangeAutomation((automation, game) => automation.Request(game, request), "One batch requested. You will start incubation yourself.");
        }
        public bool CancelAutomationBatch() => AutomationControlsAvailable(true) &&
            ChangeAutomation((automation, game) => automation.Cancel(), "Preparation canceled. Collected cultures and staged doses are safe.");
        public bool RetryAutomation()
        {
            if (!AutomationControlsAvailable() || !AutomationBlockedBySave || !SaveNow()) return false;
            AutomationBlockedBySave = false;
            ResetAutomationClock();
            Notify("Saving works again. The lab helper can resume its pending batch.");
            return true;
        }

        bool AutomationTechnicianPriority => Model.IsIncubatorReservedForTechnician ||
            (Technician && Technician.CurrentJobId != null) ||
            (Laboratory.Employee.IsOnDuty(Cycle.DayNumber) &&
             (Laboratory.ActiveSpecimenId != null || Laboratory.Queue.Length > 0));

        string AutomationGate()
        {
            if (!isActiveAndEnabled) return "Lab helper is disabled.";
            if (AutosaveBlocked || AutomationBlockedBySave) return "Saving blocked / retry at the lab bench.";
            if (Paused) return "Paused with the shop.";
            if (PlacementActive || Panel == ShopPanel.Arrange) return "Waiting for room editing to finish.";
            if (Cycle.Phase == ShopDayPhase.Closing) return "Waiting for closing to finish.";
            if (!HasAutomationStation(StationKind.Upgrade)) return "Lab bench is unavailable.";
            if (!HasAutomationStation(StationKind.Incubator)) return "Incubator is unavailable.";
            return null;
        }
        string AutomationCollectionGate(CultureKind culture)
        {
            foreach (var station in World.Stations)
                if (ActiveAutomationStation(station) && TryStationCulture(station, out var kind) && kind == culture) return null;
            return culture + " grower is unavailable.";
        }
        string AutomationGrowersGate()
        {
            var recipe = Model.GetSpecies(Automation.RecipeId);
            for (int i = 0; i < 4; i++)
            {
                var culture = (CultureKind)i;
                if (Model.Available(culture) < ShopAutomation.Required(recipe, culture))
                {
                    string reason = AutomationCollectionGate(culture);
                    if (reason != null) return reason;
                }
            }
            return null;
        }
        public string AutomationStatus
        {
            get
            {
                if (Model == null || Cycle == null) return "Lab helper is starting.";
                string gate = AutomationGate();
                if (gate != null) return gate;
                bool priority = AutomationTechnicianPriority;
                if (Automation.IsPending && priority) return "Waiting for Moss's queued lab work.";
                var next = Automation.NextStep(Model, priority);
                if (next.Kind == AutomationStepKind.Collect)
                    return AutomationGrowersGate() ?? next.Status;
                return next.Status;
            }
        }
        void TickAutomation(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) ||
                !Automation.IsPending || AutomationGate() != null || AutomationTechnicianPriority)
            { ResetAutomationClock(); return; }
            var next = Automation.NextStep(Model, false);
            if (next.Kind == AutomationStepKind.None ||
                (next.Kind == AutomationStepKind.Collect && AutomationGrowersGate() != null))
            { ResetAutomationClock(); return; }
            automationClock += seconds;
            if (automationClock < Model.CollectionSecondsPerUnit) return;
            // One operation at most. Discard excess time, including a large catch-up delta.
            ResetAutomationClock();
            string notice = next.Kind == AutomationStepKind.Dose ? "Doses prepared. Use E at the incubator to start growing." :
                next.Kind == AutomationStepKind.Finish ? "Cultures collected. Prepare them at the incubator when ready." : null;
            ChangeAutomation((automation, game) => automation.Step(game, false), notice);
        }
        bool ChangeAutomation(Func<ShopAutomation, GameModel, bool> change, string success)
        {
            if (AutosaveBlocked) return false;
            if (!GameModel.TryRestore(Layout.WithTankIds(Config), Model.CaptureSave(), out var game, out _) ||
                !ShopAutomation.TryRestore(Automation.CaptureSave(), game, out var automation))
                throw new InvalidOperationException("Cannot stage culture preparation transaction.");
            if (!change(automation, game))
            { Notify("Requires an owned module, a known recipe and no other pending request. Existing cultures are safe."); return false; }
            if (!SaveStore.Write(SavePath, CaptureStaffSlot(game, Staff, automation: automation), out var error))
            {
                SaveError = error;
                AutomationBlockedBySave = true;
                ResetAutomationClock();
                Notify("Lab helper could not save. Nothing changed. Retry at the lab bench after restoring saving.");
                return false;
            }
            // No frame or callback runs between staging and this replay. Keep the live model:
            // customer, stocker and technician claims belong to its existing instance.
            if (!change(Automation, Model))
                throw new InvalidOperationException("Saved culture preparation action did not match the live state.");
            SaveError = null;
            saveClock = 0;
            ResetAutomationClock();
            World.Refresh(Model);
            if (success != null) Notify(success);
            return true;
        }
    }
}
