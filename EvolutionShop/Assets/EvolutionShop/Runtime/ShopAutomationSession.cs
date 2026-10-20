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
            // UX-05: the research bench card (Upgrade) installs and switches modules at the same physical bench.
            bool bench = (Panel == ShopPanel.Automation || Panel == ShopPanel.Upgrade) && AtAutomationStation(StationKind.Upgrade);
            bool incubator = cancel && Panel == ShopPanel.Incubator && AtAutomationStation(StationKind.Incubator);
            if (bench || incubator) return true;
            Notify("Use culture preparation at the physical lab bench.");
            return false;
        }
        public bool OpenAutomationMenu()
        {
            // UX-06: the notebook's tabs switch here from another planning panel, still only at the bench.
            if ((Panel != ShopPanel.Upgrade && Panel != ShopPanel.Automation && !ToolkitPlanning) || !AtAutomationStation(StationKind.Upgrade))
            { Notify("Use E at the lab bench, then choose culture preparation."); return false; }
            SetPanel(ShopPanel.Automation);
            return true;
        }
        public bool PurchaseAutomationModule(bool collector)
        {
            if (!AutomationControlsAvailable()) return false;
            if (Cycle.Phase != ShopDayPhase.Night || Customers.ActiveCount != 0)
            { Notify("Buy lab modules during a clear preparation night."); return false; }
            bool purchased = ChangeAutomation((automation, game) => automation.Purchase(game, collector),
                collector ? "Collector installed, switched off. Choose a recipe and enable it." : "Doser installed, switched off. Choose a recipe and enable it.");
            if (purchased) {
                ReconcileLabPresentation();
                World.PresentResearchUpgrade(collector ? FxResearchUpgrade.Collector : FxResearchUpgrade.Doser, NextFxSequence());
                sound.ResearchConfirmation();
            }
            return purchased;
        }
        public bool SetAutomationEnabled(bool collector, bool enabled) => AutomationControlsAvailable() &&
            ChangeAutomation((automation, game) => automation.SetEnabled(collector, enabled),
                enabled ? "Module enabled. Request one batch when ready." : "Module switched off. Existing cultures are safe.");
        public bool SelectAutomationRecipe(string id)
        {
            if (!AutomationControlsAvailable()) return false;
            return ChangeAutomationConfiguration((automation, stocking, game) =>
            {
                if (!automation.SelectRecipe(game, id)) return false;
                return automation.DestinationTankId == null ||
                    stocking.SetTarget(game, automation.DestinationTankId, id) || stocking.GetTarget(automation.DestinationTankId) == id;
            }, "Recipe selected. Its configured display target follows this recipe; occupied stock is never replaced.");
        }
        public bool SetAutomationDestination(string tankId)
        {
            if (!AutomationControlsAvailable()) return false;
            return ChangeAutomationConfiguration((automation, stocking, game) =>
            {
                bool changed = automation.SetDestination(game, tankId);
                if (!changed && automation.DestinationTankId != tankId) return false;
                if (automation.RecipeId != null && stocking.GetTarget(tankId) != automation.RecipeId)
                    changed |= stocking.SetTarget(game, tankId, automation.RecipeId);
                return changed;
            }, "Destination saved. Nori's target now matches the selected recipe; current occupants stay safe.");
        }
        public bool SetAutomationDesiredStock(int count) => AutomationControlsAvailable() &&
            ChangeAutomationConfiguration((automation, stocking, game) => automation.SetDesiredStockCount(count),
                "Desired stock level saved. Existing and queued specimens count toward it.");
        public bool SetAutomationMaintainEnabled(bool enabled) => AutomationControlsAvailable() &&
            ChangeAutomationConfiguration((automation, stocking, game) => automation.SetMaintainEnabled(enabled),
                enabled ? "Stock target enabled. One Moss start at a time will cover the shortfall." :
                    "New automatic refill starts stopped. Already queued work remains safe.");
        public bool SetAutomationPaused(bool paused) => AutomationControlsAvailable() &&
            ChangeAutomation((automation, game) => automation.SetPaused(paused),
                paused ? "Culture helper paused. Its request and cultures are retained; staff duties continue." :
                    "Culture helper resumed. The saved target remains active.");
        public bool RequestAutomationBatch()
        {
            if (!AutomationControlsAvailable()) return false;
            string request = Guid.NewGuid().ToString("N");
            string success = HasMatchingTechnicianOrder(Automation.RecipeId)
                ? "One batch requested. Moss will take its matching recipe through incubation and rack delivery."
                : "One batch requested. You will start incubation yourself.";
            return ChangeAutomation((automation, game) => automation.Request(game, request), success);
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

        bool HasMatchingTechnicianOrder(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId) || Laboratory == null || !Technician || !Technician.isActiveAndEnabled ||
                !Technician.Actor || !Technician.Actor.isActiveAndEnabled) return false;
            var queue = Laboratory.Queue;
            return queue.Length > 0 && queue[0].speciesId == speciesId;
        }

        bool AutomationTechnicianHandoff => Automation.IsPending && HasMatchingTechnicianOrder(Automation.RecipeId);

        bool AutomationTechnicianPriority => Model.IsIncubatorReservedForTechnician ||
            (Technician && Technician.CurrentJobId != null) ||
            (Laboratory.Employee.IsOnDuty(Cycle.DayNumber) &&
             (Laboratory.ActiveSpecimenId != null || (Laboratory.Queue.Length > 0 && !AutomationTechnicianHandoff)));

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
        string AutomationTargetConfigurationIssue()
        {
            if (Automation.RecipeId == null) return "Choose a discovered recipe before enabling a stock target.";
            if (Automation.DestinationTankId == null) return "Choose a front display destination before enabling a stock target.";
            if (Array.IndexOf(Model.TankIds, Automation.DestinationTankId) < 0)
                return "Configured display was removed. Choose an active display destination.";
            if (Stocking.GetTarget(Automation.DestinationTankId) != Automation.RecipeId)
                return "Nori's display target changed. Re-select the destination to sync it with " + Model.GetSpecies(Automation.RecipeId).DisplayName + ".";
            var occupant = Model.GetTank(Automation.DestinationTankId);
            if (occupant.HasValue && occupant.Value.SpeciesId != Automation.RecipeId)
                return "Destination is occupied by " + Model.GetSpecies(occupant.Value.SpeciesId).DisplayName + ". Move or sell it before this target can use the display.";
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
                if (Automation.MaintainEnabled) return AutomationPolicyStatus();
                bool priority = AutomationTechnicianPriority;

                bool technicianHandoff = AutomationTechnicianHandoff;
                var next = Automation.NextStep(Model, priority, technicianHandoff);
                if (next.Kind == AutomationStepKind.Collect)
                    return AutomationGrowersGate() ?? next.Status;
                return next.Status;
            }
        }
        string AutomationPolicyStatus()
        {
            if (Automation.Paused) return "Culture helper paused. The target and in-flight work are retained; Moss and Nori duties continue.";
            string issue = AutomationTargetConfigurationIssue();
            if (issue != null) return issue;
            int physical = CountAutomationStock(Automation.RecipeId);
            int queued = CountTechnicianStarts(Automation.RecipeId);
            int planned = physical + queued;
            if (Automation.IsPending)
            {
                var step = Automation.NextStep(Model, AutomationTechnicianPriority, AutomationTechnicianHandoff);
                var pendingQueue = Laboratory.Queue;
                if (pendingQueue.Length > 0 && pendingQueue[0].speciesId != Automation.RecipeId)
                    return "Waiting for higher-priority Moss work: " + Model.GetSpecies(pendingQueue[0].speciesId).DisplayName + ". " + step.Status;
                if (!Laboratory.Employee.Hired)
                    return "Moss is not hired; the helper can prepare this batch, but you must incubate it manually. " + step.Status;
                if (!Laboratory.Employee.IsOnDuty(Cycle.DayNumber))
                    return "Moss is off duty; the helper can prepare this batch, but it will wait for Moss or manual incubation. " + step.Status;
                return step.Kind == AutomationStepKind.None ? step.Status : "Target " + Math.Min(planned, Automation.DesiredStockCount) + "/" +
                    Automation.DesiredStockCount + " / " + step.Status;
            }
            if (Automation.PolicyHandoffWaiting && queued >= Automation.PolicyQueueCountAtRequest)
            {
                if (!Laboratory.Employee.Hired) return "Cultures are ready, but Moss is not hired to accept the queued start.";
                if (!Laboratory.Employee.IsOnDuty(Cycle.DayNumber)) return "Cultures are ready; Moss is off duty. Schedule or fund his shift at the front counter.";
                return "Cultures are ready. Waiting for Moss to accept the matching queued start.";
            }
            var queue = Laboratory.Queue;
            if (queue.Length > 0 && queue[0].speciesId != Automation.RecipeId)
                return "Waiting for higher-priority Moss work: " + Model.GetSpecies(queue[0].speciesId).DisplayName +
                    ". Target " + physical + "/" + Automation.DesiredStockCount + "; queued work is retained.";
            if (queued > 0 && !Laboratory.Employee.Hired)
                return "Target plan is queued, but Moss is not hired. Hire him before these " + queued + " lab start(s) can run.";
            if (queued > 0 && !Laboratory.Employee.IsOnDuty(Cycle.DayNumber))
                return "Target plan is queued, but Moss is off duty. Schedule or fund his lab shift at the front counter.";
            if (physical >= Automation.DesiredStockCount)
                return queued > 0 ? "Goal met: " + physical + "/" + Automation.DesiredStockCount +
                    " tracked, but " + queued + " Moss start(s) remain queued and may exceed it. Remove extra starts at the counter." :
                    "Target met: " + physical + " tracked specimens in displays, racks or incubator / goal " + Automation.DesiredStockCount + ".";
            if (queued > 0)
            {
                var recipe = Model.GetSpecies(Automation.RecipeId);
                bool needsCollection = false;
                for (int i = 0; i < 4; i++)
                    if (Model.Available((CultureKind)i) < ShopAutomation.Required(recipe, (CultureKind)i)) needsCollection = true;
                if (needsCollection && !Automation.CollectorEnabled)
                    return "Short " + (Automation.DesiredStockCount - planned) + ". Collector is off; enable it or collect the missing cultures manually.";
                if (needsCollection)
                {
                    string grower = AutomationGrowersGate();
                    if (grower != null) return "Short " + (Automation.DesiredStockCount - planned) + ". " + grower;
                    if (Model.ReservedCultureCount >= Model.Capacity) return "Siphon full. Release spare cultures to refill the target.";
                }
                return planned >= Automation.DesiredStockCount ? "Target covered: " + physical + " tracked / " + queued +
                    " Moss start(s) queued / goal " + Automation.DesiredStockCount + "." : needsCollection ?
                    "Moss's next " + Model.GetSpecies(Automation.RecipeId).DisplayName + " start is queued; culture helper is preparing it." :
                    "Moss can start the queued " + Model.GetSpecies(Automation.RecipeId).DisplayName + " recipe from the available cultures.";
            }
            if (!Laboratory.Employee.Hired) return "Short " + (Automation.DesiredStockCount - planned) + ". Hire Moss and schedule a paid lab duty to grow target stock; manual production remains available.";
            if (!Laboratory.Employee.IsOnDuty(Cycle.DayNumber))
                return "Short " + (Automation.DesiredStockCount - planned) + ". Moss is off duty; schedule or fund his lab shift at the front counter.";
            if (queue.Length == 0) return "Short " + (Automation.DesiredStockCount - planned) + ". Refill order is being saved.";
            return "Stock target is short by " + (Automation.DesiredStockCount - planned) + ".";
        }
        int CountAutomationStock(string speciesId)
        {
            int count = 0;
            foreach (string id in Model.TankIds)
            { var item = Model.GetTank(id); if (item.HasValue && item.Value.SpeciesId == speciesId) count++; }
            foreach (string id in Model.StorageIds)
            { var item = Model.GetStorage(id); if (item.HasValue && item.Value.SpeciesId == speciesId) count++; }
            var incubator = Model.Incubator;
            if ((incubator.Phase == IncubatorPhase.Incubating || incubator.Phase == IncubatorPhase.Ready) && incubator.SpeciesId == speciesId) count++;
            return count;
        }
        int CountTechnicianStarts(string speciesId)
        {
            int count = 0;
            foreach (var order in Laboratory.Queue) if (order.speciesId == speciesId) count += order.count;
            return count;
        }
        void MaintainAutomationTarget()
        {
            if (!Automation.MaintainEnabled || Automation.Paused || AutosaveBlocked || AutomationBlockedBySave ||
                AutomationGate() != null || AutomationTargetConfigurationIssue() != null) return;
            int queued = CountTechnicianStarts(Automation.RecipeId);
            if (Automation.PolicyHandoffWaiting)
            {
                if (queued >= Automation.PolicyQueueCountAtRequest) return;
                ChangeAutomation((automation, game) => automation.ClearPolicyHandoffWait(), null);
            }
            if (Automation.IsPending) return;
            int deficit = Automation.DesiredStockCount - CountAutomationStock(Automation.RecipeId) - queued;
            if (deficit > 0)
            {
                int add = Math.Min(deficit, ShopLaboratory.MaximumOrderCount - queued);
                if (add > 0 && AddAutomationOrders(add)) return;
            }
            var queue = Laboratory.Queue;
            if (queued == 0 || queue.Length == 0 || queue[0].speciesId != Automation.RecipeId ||
                !Laboratory.Employee.IsOnDuty(Cycle.DayNumber) || (!Automation.CollectorEnabled && !Automation.DoserEnabled)) return;
            string request = Guid.NewGuid().ToString("N");
            int matching = queued;
            ChangeAutomation((automation, game) => automation.RequestForPolicy(game, request, matching), null);
        }
        bool AddAutomationOrders(int count)
        {
            if (AutosaveBlocked || AutomationBlockedBySave || !GameModel.TryRestore(Layout.WithTankIds(Config), Model.CaptureSave(), out var game, out _) ||
                !ShopLaboratory.TryRestore(Laboratory.CaptureSave(game), game, Cycle.DayNumber, out var lab)) return false;
            if (!lab.AddOrder(game, Automation.RecipeId, count)) return false;
            if (!WriteUiObservedSlot( CaptureStaffSlotWithAutomation(game, Staff, null, null, lab, Automation), out var error))
            {
                SaveError = error; AutomationBlockedBySave = true; ResetAutomationClock();
                World.CancelFxTransient(); sound.StopLabAudio(); sound.StopResearchAudio(); ReconcileLabPresentation();
                Notify("Target refill could not save. No Moss orders changed; retry saving at the lab bench.");
                return false;
            }
            Laboratory = lab; SaveError = null; saveClock = 0;
            Notify("Moss queue updated for the " + Model.GetSpecies(Automation.RecipeId).DisplayName + " stock target.");
            return true;
        }
        void TickAutomation(float seconds)
        {
            MaintainAutomationTarget();
            labModuleRuntimeBlocked = false; labModuleStaffPriority = false;
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || !Automation.IsPending)
            { ResetAutomationClock(); return; }
            if (AutomationGate() != null) { labModuleRuntimeBlocked = true; ResetAutomationClock(); return; }
            bool priority = AutomationTechnicianPriority;
            labModuleStaffPriority = priority;
            if (priority) { labModuleRuntimeBlocked = true; ResetAutomationClock(); return; }
            bool technicianHandoff = AutomationTechnicianHandoff;
            var next = Automation.NextStep(Model, priority, technicianHandoff);
            if (next.Kind == AutomationStepKind.None ||
                (next.Kind == AutomationStepKind.Collect && AutomationGrowersGate() != null))
            { labModuleRuntimeBlocked = true; ResetAutomationClock(); return; }
            automationClock += seconds;
            if (automationClock < Model.CollectionSecondsPerUnit) return;
            // One operation at most. Discard excess time, including a large catch-up delta.
            ResetAutomationClock();
            string notice = next.Kind == AutomationStepKind.Dose ? "Doses prepared. Use E at the incubator to start growing." :
                next.Kind == AutomationStepKind.Finish ? (technicianHandoff ? "Cultures collected. Moss will start the matching queued recipe." : "Cultures collected. Prepare them at the incubator when ready.") : null;
            if (ChangeAutomation((automation, game) => automation.Step(game, priority, technicianHandoff), notice)) PresentLabModule(next);
        }
        bool ChangeAutomation(Func<ShopAutomation, GameModel, bool> change, string success)
        {
            if (AutosaveBlocked || AutomationBlockedBySave) return false;
            if (!GameModel.TryRestore(Layout.WithTankIds(Config), Model.CaptureSave(), out var game, out _) ||
                !ShopAutomation.TryRestore(Automation.CaptureSave(), game, out var automation))
                throw new InvalidOperationException("Cannot stage culture preparation transaction.");
            if (!change(automation, game))
            { Notify("Requires an owned module, a known recipe and no other pending request. Existing cultures are safe."); return false; }
            if (!WriteUiObservedSlot( CaptureStaffSlotWithAutomation(game, Staff, null, null, null, automation), out var error))
            {
                SaveError = error;
                AutomationBlockedBySave = true;
                World.CancelFxTransient(); sound.StopLabAudio(); sound.StopResearchAudio(); ReconcileLabPresentation();
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
            ReconcileLabPresentation();
            if (success != null) Notify(success);
            return true;
        }
        bool ChangeAutomationConfiguration(Func<ShopAutomation, ShopStocking, GameModel, bool> change, string success)
        {
            if (AutosaveBlocked || AutomationBlockedBySave) return false;
            if (!GameModel.TryRestore(Layout.WithTankIds(Config), Model.CaptureSave(), out var game, out _) ||
                !ShopAutomation.TryRestore(Automation.CaptureSave(), game, out var automation) ||
                !ShopStocking.TryRestore(Stocking.CaptureSave(game), game, Cycle.DayNumber, out var stocking))
                throw new InvalidOperationException("Cannot stage culture preparation settings.");
            if (!change(automation, stocking, game))
            { Notify("Choose a valid discovered recipe, active display and stock target. No existing work changed."); return false; }
            if (!WriteUiObservedSlot( CaptureStaffSlotWithAutomation(game, Staff, stocking, null, null, automation), out var error))
            {
                SaveError = error; AutomationBlockedBySave = true; ResetAutomationClock();
                World.CancelFxTransient(); sound.StopLabAudio(); sound.StopResearchAudio(); ReconcileLabPresentation();
                Notify("Culture preparation settings could not save. Previous settings and specimens remain unchanged."); return false;
            }
            bool stockerTargetsChanged = StockerTargetsDiffer(Stocking, stocking, game);
            Automation = automation; Stocking = stocking; SaveError = null; saveClock = 0; ResetAutomationClock();
            if (stockerTargetsChanged) Stocker.ResetTransient();
            World.Refresh(Model);
            ReconcileLabPresentation();
            if (success != null) Notify(success);
            return true;
        }
        static bool StockerTargetsDiffer(ShopStocking before, ShopStocking after, GameModel game)
        {
            foreach (string tank in game.TankIds)
                if (before.GetTarget(tank) != after.GetTarget(tank)) return true;
            return false;
        }
    }
}
