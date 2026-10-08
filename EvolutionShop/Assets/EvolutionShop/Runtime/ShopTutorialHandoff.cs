using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        // Directions are requested presentation, never proof of independent practice.
        // Only the existing Assisted receipt is durable; reload starts with the goal again.
        bool tutorialPracticeDirections;
        bool tutorialJournalReturnsToHelp;
        public bool TutorialPracticeGuidanceRequested => tutorialPracticeDirections &&
            Tutorial.Mode == TutorialMode.Active && ActiveTutorialLesson == TutorialLesson.Practice;
        public bool TutorialJournalReturnsToHelp => tutorialJournalReturnsToHelp && Panel == ShopPanel.Journal;
        public bool TutorialUpgradeHelpOpen { get; private set; }
        public TutorialLesson TutorialGuidanceLesson => TutorialPracticeGuidanceRequested
            ? ResolvePracticeGuidanceLesson() : ActiveTutorialLesson;

        TutorialLesson ResolvePracticeGuidanceLesson()
        {
            string id = Tutorial.PracticeSpecimenId;
            if (!string.IsNullOrEmpty(id))
            {
                if (Model.Incubator.SpecimenId == id)
                    return Model.Incubator.Phase == IncubatorPhase.Ready ? TutorialLesson.Display : TutorialLesson.Grow;
                foreach (string tank in Model.TankIds)
                {
                    var stock = Model.GetTank(tank);
                    if (stock.HasValue && stock.Value.Id == id)
                        return Cycle.Phase == ShopDayPhase.Night ? TutorialLesson.PriceAndOpen : TutorialLesson.Serve;
                }
                // A tracked ready creature on a rack or in another output is selected
                // through the existing empty-display source chooser, never a new transfer.
                return TutorialLesson.Display;
            }
            if (Model.IsIncubatorReservedForTechnician || Model.Incubator.Phase != IncubatorPhase.Dosing)
                return TutorialLesson.Grow;
            return TutorialNeededCulture().HasValue ? TutorialLesson.Collect : TutorialLesson.Prepare;
        }

        public bool CanShowTutorialTarget => Panel == ShopPanel.TutorialHelp && TutorialGuidanceAvailable &&
            TutorialHelpLesson == ActiveTutorialLesson && ActiveTutorialLesson >= TutorialLesson.Orient &&
            ActiveTutorialLesson <= TutorialLesson.CloseAndReview && Cycle.Phase != ShopDayPhase.Closing;

        public bool StopTutorialPracticeGuidance()
        {
            if (!TutorialPracticeGuidanceRequested) return false;
            tutorialPracticeDirections = false;
            if (Panel == ShopPanel.TutorialHelp) CloseTutorialHelp();
            SetPanel(ShopPanel.None); return true;
        }

        // The ledger deliberately retains one full report. If it was replaced before
        // acknowledgement, the existing exact tutorial receipts provide an honest,
        // bounded recovery view; they do not reconstruct missing report totals.
        public bool TutorialNeedsReportRecovery => Tutorial.Mode == TutorialMode.Active &&
            ActiveTutorialLesson == TutorialLesson.CloseAndReview && !Tutorial.ReportAcknowledged &&
            Cycle.Phase == ShopDayPhase.Night && Tutorial.PracticeSaleDay > 0 &&
            Cycle.DayNumber > Tutorial.PracticeSaleDay &&
            Reports.LastDayNumber != Tutorial.FirstSaleDay && Reports.LastDayNumber != Tutorial.PracticeSaleDay;
        public int TutorialReportRecoveryDay => TutorialNeedsReportRecovery ? Tutorial.PracticeSaleDay : 0;
        public bool AcknowledgeTutorialSaleReceipts()
        {
            if (AutosaveBlocked || Panel != ShopPanel.TutorialHelp || TutorialHelpLesson != TutorialLesson.CloseAndReview ||
                !TutorialNeedsReportRecovery) return false;
            if (!Tutorial.ObserveReportAcknowledged(TutorialReportRecoveryDay)) return false;
            // As with ordinary report acknowledgement, failed persistence leaves a
            // truthful dirty receipt plus SaveError, never a falsely completed mode.
            if (!SaveNow()) return false;
            SelectTutorialHelpLesson(ActiveTutorialLesson); return true;
        }

        public bool ShowTutorialHandoff()
        {
            if (AutosaveBlocked || Tutorial.Mode != TutorialMode.Active || ActiveTutorialLesson != TutorialLesson.NextPlan)
                return false;
            if (!OpenTutorialHelp()) return false;
            SelectTutorialHelpLesson(TutorialLesson.NextPlan); return true;
        }

        public bool OpenTutorialJournal()
        {
            if (Panel != ShopPanel.TutorialHelp || TutorialHelpLesson != TutorialLesson.NextPlan) return false;
            TutorialUpgradeHelpOpen = false;
            tutorialJournalReturnsToHelp = true;
            SetPanel(ShopPanel.Journal); return true;
        }
        public bool OpenTutorialUpgradeHelp()
        {
            if (Panel != ShopPanel.TutorialHelp || TutorialHelpLesson != TutorialLesson.NextPlan) return false;
            TutorialUpgradeHelpOpen = true; panelScroll = Vector2.zero; return true;
        }
        void ResetTutorialPresentation()
        {
            tutorialPracticeDirections = false;
            ClearTutorialHelpReturn();
        }
    }
}
