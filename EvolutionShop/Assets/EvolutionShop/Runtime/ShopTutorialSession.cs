using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopTutorial Tutorial { get; private set; } = new ShopTutorial();
        public TutorialLesson ActiveTutorialLesson => Tutorial.CurrentLesson(Cycle);
        public TutorialLesson TutorialHelpLesson { get; private set; } = TutorialLesson.Orient;
        ShopPanel tutorialReturnPanel;
        string tutorialReturnTank, tutorialReturnRack, tutorialReturnConditioner;

        void RestoreTutorial(TutorialSave save)
        {
            if (!ShopTutorial.TryRestore(save, Model, Cycle.DayNumber, out var tutorial))
                throw new InvalidOperationException("Cannot restore tutorial data from the validated slot.");
            Tutorial = tutorial;
            ResetTutorialPresentation();
        }

        ShopTutorial CopyTutorial()
        {
            if (!ShopTutorial.TryRestore(Tutorial.CaptureSave(), Model, Cycle.DayNumber, out var copy))
                throw new InvalidOperationException("Cannot stage the current tutorial state.");
            return copy;
        }

        bool CommitTutorialMode(Func<ShopTutorial, bool> change, TutorialMode desired)
        {
            if (AutosaveBlocked)
            { Notify("Restore a valid save before changing guidance. Your original save is protected."); return false; }
            if (Tutorial.Mode == desired) return false;
            var staged = CopyTutorial();
            if (!change(staged)) return false;
            staged.Reconcile(Model);
            var slot = CaptureStaffSlot(Model, Staff);
            slot.tutorial = staged.CaptureSave();
            if (!SaveStore.Write(SavePath, slot, out var error))
            { SaveError = error; Notify("Guidance change could not be saved. Your previous guidance mode is unchanged."); return false; }
            Tutorial = staged; ResetTutorialPresentation(); SaveError = null; saveClock = 0;
            message = null; messageUntil = 0; // A successful retry supersedes its old failure notice.
            return true;
        }

        public bool StartGuidedOpening()
        {
            if (!CommitTutorialMode(t => t.Start(), TutorialMode.Active)) return false;
            ClearTutorialHelpReturn(); SetPanel(ShopPanel.None); return true;
        }
        public bool ContinueGuidedOpening()
        {
            if (Tutorial.Mode == TutorialMode.Offered && !AutosaveBlocked) return false;
            ClearTutorialHelpReturn(); SetPanel(ShopPanel.None); return true;
        }
        public bool SkipTutorial()
        {
            if (!CommitTutorialMode(t => t.Skip(), TutorialMode.Skipped)) return false;
            ClearTutorialHelpReturn(); SetPanel(ShopPanel.None); return true;
        }
        public bool FinishGuidedOpening()
        {
            if (!CommitTutorialMode(t => t.Finish(Cycle), TutorialMode.Completed)) return false;
            ClearTutorialHelpReturn(); SetPanel(ShopPanel.None);
            Notify("Your guided opening is complete. Keep growing your shop; F1 help is always available.");
            return true;
        }

        public bool OpenTutorialHelp()
        {
            if (PlacementActive || Panel == ShopPanel.Reset || Panel == ShopPanel.Presentation) return false;
            if (Panel == ShopPanel.TutorialHelp) return true;
            if (TutorialJournalReturnsToHelp)
            { SetPanel(ShopPanel.TutorialHelp); return true; }
            tutorialReturnPanel = Panel;
            tutorialReturnTank = selectedTank; tutorialReturnRack = selectedRack; tutorialReturnConditioner = selectedConditioner;
            TutorialHelpLesson = ActiveTutorialLesson == TutorialLesson.None ? TutorialLesson.Orient : ActiveTutorialLesson;
            // Help is a real assistance choice; a failed write leaves ordinary dirty
            // progress visible through SaveError rather than claiming durability.
            if (Tutorial.MarkPracticeAssisted()) SaveNow();
            SetPanel(ShopPanel.TutorialHelp); return true;
        }
        public void SelectTutorialHelpLesson(TutorialLesson lesson)
        {
            if (Panel != ShopPanel.TutorialHelp || lesson < TutorialLesson.Orient || lesson > TutorialLesson.NextPlan) return;
            TutorialUpgradeHelpOpen = false; TutorialHelpLesson = lesson; panelScroll = Vector2.zero;
        }
        public void CloseTutorialHelp()
        {
            if (Panel != ShopPanel.TutorialHelp) return;
            var back = tutorialReturnPanel;
            if (!TutorialReturnIsValid(back)) back = ShopPanel.None;
            selectedTank = tutorialReturnTank ?? selectedTank;
            selectedRack = tutorialReturnRack; selectedConditioner = tutorialReturnConditioner;
            ClearTutorialHelpReturn(); SetPanel(back);
        }
        void ClearTutorialHelpReturn()
        {
            TutorialUpgradeHelpOpen = false; tutorialJournalReturnsToHelp = false;
            tutorialReturnPanel = ShopPanel.None;
            tutorialReturnTank = tutorialReturnRack = tutorialReturnConditioner = null;
            TutorialHelpLesson = TutorialLesson.Orient;
        }
        bool TutorialReturnIsValid(ShopPanel panel)
        {
            if (panel == ShopPanel.TutorialHelp || panel == ShopPanel.Reset || panel == ShopPanel.Presentation) return false;
            if (panel == ShopPanel.DayReport) return Reports.LastDayNumber > 0;
            bool stationPanel = panel == ShopPanel.Incubator || panel == ShopPanel.Tank || panel == ShopPanel.StorageRack ||
                panel == ShopPanel.Conditioning || panel == ShopPanel.Upgrade || panel == ShopPanel.Counter || panel == ShopPanel.Staff ||
                panel == ShopPanel.Stocker || panel == ShopPanel.StockerTargets || panel == ShopPanel.Technician || panel == ShopPanel.TechnicianOrders;
            if (!stationPanel) return true;
            if (!World || !Player) return false;
            foreach (var station in World.Stations)
            {
                if (!station || !station.gameObject.activeInHierarchy || Vector3.Distance(Player.transform.position, station.transform.position) > 4.5f) continue;
                if (panel == ShopPanel.Incubator && station.Kind == StationKind.Incubator ||
                    panel == ShopPanel.Tank && station.TankId == tutorialReturnTank ||
                    panel == ShopPanel.StorageRack && station.StorageItemId != null && station.StorageItemId == tutorialReturnRack ||
                    panel == ShopPanel.Conditioning && station.ConditionerItemId != null && station.ConditionerItemId == tutorialReturnConditioner ||
                    panel == ShopPanel.Upgrade && station.Kind == StationKind.Upgrade ||
                    (panel == ShopPanel.Counter || panel == ShopPanel.Staff || panel == ShopPanel.Stocker || panel == ShopPanel.StockerTargets ||
                     panel == ShopPanel.Technician || panel == ShopPanel.TechnicianOrders) && station.Kind == StationKind.Counter) return true;
            }
            return false;
        }
        public void BackFromPanel()
        {
            if (Panel == ShopPanel.TutorialHelp) { CloseTutorialHelp(); return; }
            if (Panel == ShopPanel.Welcome)
            {
                // An unwritable fresh slot must still offer recovery/settings/quit.
                if (Tutorial.Mode == TutorialMode.Offered && !string.IsNullOrEmpty(SaveError))
                { SetPanel(ShopPanel.Pause); return; }
                if (!ContinueGuidedOpening()) Notify("Choose Start guided opening or Play without guidance.");
                return;
            }
            SetPanel(Panel == ShopPanel.Presentation ? ShopPanel.Pause : Panel == ShopPanel.None ? ShopPanel.Pause : ShopPanel.None);
        }

        void ObserveTutorialMotion(bool moved, bool looked, bool inLab)
        {
            if (Panel == ShopPanel.None && !Paused && !PlacementActive && Tutorial.ObserveOrientation(moved, looked, inLab)) SaveNow();
        }
        bool AtTutorialIncubator()
        {
            if (Paused || PlacementActive || Panel != ShopPanel.Incubator || !World || !Player) return false;
            var ray = new Ray(Player.View.transform.position, Player.View.transform.forward);
            if (Physics.SphereCast(ray, .14f, out var hit, 3.3f, ~0, QueryTriggerInteraction.Collide))
            {
                var station = hit.collider.GetComponentInParent<Station>();
                if (station && station.Kind == StationKind.Incubator) return true;
            }
            Notify("Use E at the lab incubator in person to prepare cultures."); return false;
        }
        bool PrepareManually(Func<ActionResult> action)
        {
            if (!AtTutorialIncubator()) return false;
            var result = action();
            if (result.Success)
                foreach (var species in Config.Species) Tutorial.ObservePrepared(Model, species.Id, TutorialActionOrigin.Player);
            return Perform(result).Success;
        }
        public bool DoseCulture(CultureKind culture) => PrepareManually(() => Model.Dose(culture));
        public bool ReturnCultureDose(CultureKind culture) => PrepareManually(() => Model.ReturnDose(culture));
        public bool ClearCultureDoses() => PrepareManually(() => Model.ClearDoses());
        public bool ReleaseCulture(CultureKind culture) => PrepareManually(() => Model.ReleaseCulture(culture));
        public bool PrepareKnownRecipe(string speciesId) => PrepareManually(() => Model.UseKnownRecipe(speciesId));
        public bool BeginManualIncubation()
        {
            if (!AtTutorialIncubator()) return false;
            var result = Model.BeginIncubation();
            Tutorial.ObserveStarted(Model, result, TutorialActionOrigin.Player);
            if (!Perform(result).Success) return false;
            SetPanel(ShopPanel.None); return true;
        }
    }
}
