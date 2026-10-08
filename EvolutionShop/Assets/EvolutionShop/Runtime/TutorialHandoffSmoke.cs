#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator TutorialHandoffScenario()
        {
            limitations.Add("TUT-08 isolated two-day route uses synthetic WASD, mouse look, E, Escape, F1 and held collection, scripted station aim, and guarded application commands. It does not prove native operating-system mouse clicks, focus behavior or novice learning.");
            limitations.Add("Both creatures use ordinary manually collected cultures, ten-second incubation and real customer checkout. No seeded money, forced archetype, clock acceleration, tutorial reward or upgrade purchase is used. Arrivals are enabled only for each checkout; ambient comedy is disabled solely in this validation session.");
            limitations.Add("This focused route requests assistance and crosses normal closing nights, acknowledges the first-sale report before practice finishes, exercises transient directions and reload, and tests an isolated failed completion write. It complements the separate unassisted whole-tutorial regression. Standalone captures are actual 720p/1080p surfaces; Editor captures are diagnostic, not native UI evidence.");
            limitations.Add("After preserving the completed main route, the lost-report recovery branch trades and closes an actual zero-sale day three, then derives an explicitly labelled isolated snapshot by reopening only the tutorial mode/handoff/report-acknowledgement flags. Real sale days one/two, specimens, cash and day-three report remain unchanged. This fixture tests durable sale-receipt review when the full older daily totals are no longer retained; it does not fabricate historical totals or claim a fresh-process restart.");
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionTutorialHandoffSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Handoff route is confined to its explicitly isolated evidence save");
            Check(shop.Tutorial.Mode == TutorialMode.Offered && shop.Model.Currency == 0 &&
                shop.Cycle.DayNumber == 1 && shop.Cycle.Phase == ShopDayPhase.Night &&
                Mathf.Approximately(shop.Config.IncubationSeconds, 10) && shop.ScriptedHarvest == null &&
                shop.Customers.ForcedArchetypeId == null && !shop.Customers.UseBaseReputationEffectsForValidation,
                "Handoff begins with ordinary zero-credit night one, production collection and unforced customer rules");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTutorialHandoffKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTutorialHandoffMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            Check(shop.StartGuidedOpening(), "Start guided opening through the actual atomic mode command");
            shop.AllowValidationInput = true;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(24, -8) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState()); yield return null;
            yield return CycleGoStation(StationKind.Bloom);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Collect,
                "Actual synthetic look and walking satisfy orientation without seeded learning facts");
            yield return TutorialProduce(false);
            string first = shop.Model.Incubator.SpecimenId;
            yield return CycleTransfer(StationKind.TankA, "tank-a");
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Tank, "Inspect the actual first creature's existing price panel");
            yield return CycleCounter();
            Check(shop.OpenForDay() && shop.ActiveTutorialLesson == TutorialLesson.Serve,
                "Ordinary first opening reaches service with no unearned sale");
            yield return TutorialCheckout(first, 6);
            shop.SetPanel(ShopPanel.None);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Practice && !shop.Tutorial.PracticeAssisted &&
                !shop.TutorialPracticeGuidanceRequested && !shop.TryGetTutorialGuidance(out _) &&
                string.IsNullOrEmpty(shop.TutorialLocalCallout),
                "First actual manual sale presents a goal-only repeat without assistance or an unsolicited marker");
            yield return TutorialCapture("460-handoff-independent-practice");

            Check(shop.CloseForNight(), "Player may close normally before starting the second manual batch");
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8,
                "first-sale day ordinary closing", false);
            Check(shop.Cycle.DayNumber == 2 && shop.Panel == ShopPanel.DayReport &&
                shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == 6 &&
                !shop.Tutorial.CaptureSave().reportAcknowledged && shop.ActiveTutorialLesson == TutorialLesson.Practice,
                "Night two presents the real one-sale report while manual practice remains unmet");
            yield return TutorialCapture("461-handoff-early-report");
            shop.BackFromPanel();
            Check(shop.Tutorial.CaptureSave().reportAcknowledged && shop.Tutorial.CaptureSave().reportDay == 1 &&
                shop.ActiveTutorialLesson == TutorialLesson.Practice && !shop.FinishGuidedOpening(),
                "Viewing the first-sale report records its real day but cannot bypass manual practice or complete the tutorial");
            Check(!shop.Tutorial.PracticeAssisted && !shop.TryGetTutorialGuidance(out _),
                "A normal report visit does not label independent practice assisted or turn on directions");

            yield return CycleTap(Key.F1);
            Check(shop.Panel == ShopPanel.TutorialHelp && shop.Paused && shop.Tutorial.PracticeAssisted &&
                !shop.TutorialPracticeGuidanceRequested && shop.TutorialHelpLesson == TutorialLesson.Practice,
                "Synthetic F1 requests practice help, persists Assisted and pauses without silently enabling directions");
            yield return TutorialCapture("462-handoff-requested-help");
            yield return CycleTap(Key.Escape);
            Check(shop.Panel == ShopPanel.None && !shop.TutorialPracticeGuidanceRequested && !shop.TryGetTutorialGuidance(out _),
                "Closing ordinary practice help returns to goal-only gameplay");
            TutorialGuidanceTarget practiceTarget = default;
            Check(shop.OpenTutorialHelp() && shop.CanShowTutorialTarget && shop.ShowTutorialTarget() &&
                shop.TutorialPracticeGuidanceRequested && shop.Panel == ShopPanel.None && !shop.Paused &&
                shop.TryGetTutorialGuidance(out practiceTarget),
                "Explicit Show me enables live practice directions and resumes normal gameplay");
            shop.Player.View.transform.LookAt(practiceTarget.Position);
            yield return TutorialCapture("463-handoff-requested-directions");
            Check(shop.StopTutorialPracticeGuidance() && !shop.TutorialPracticeGuidanceRequested &&
                !shop.TryGetTutorialGuidance(out _) && shop.Tutorial.PracticeAssisted && shop.Model.Currency == 6,
                "Stop directions removes only the transient aid and keeps Assisted attribution and earned money");
            Check(shop.OpenTutorialHelp() && shop.ShowTutorialTarget() && shop.TutorialPracticeGuidanceRequested,
                "The player may explicitly request practice directions again");
            shop.SetPanel(ShopPanel.Pause);
            string practiceBefore = TutorialState();
            Check(shop.SaveNow() && shop.ReloadSaved() && TutorialState() == practiceBefore &&
                !shop.TutorialPracticeGuidanceRequested && shop.Tutorial.PracticeAssisted && shop.Model.Currency == 6,
                "Saved reload retains true practice attribution and earnings but never restores an unsolicited marker request");
            shop.BackFromPanel();
            Check(shop.Panel == ShopPanel.None && !shop.TryGetTutorialGuidance(out _),
                "Resumed practice remains goal-only until another explicit Show me request");
            Check(shop.OpenTutorialHelp() && shop.ShowTutorialTarget(), "Request live directions for the real second manual batch");

            yield return CycleHarvest(CultureKind.Bloom);
            Check(shop.TutorialPracticeGuidanceRequested && shop.TryGetTutorialGuidance(out practiceTarget) &&
                practiceTarget.Station == StationOf(StationKind.Flow),
                "Requested repeat directions follow the still-missing culture after a real Bloom harvest");
            yield return CycleHarvest(CultureKind.Flow);
            yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Incubator && !string.IsNullOrEmpty(shop.TutorialLocalCallout) &&
                shop.DoseCulture(CultureKind.Flow) && shop.DoseCulture(CultureKind.Bloom),
                "Requested practice uses the real incubator callout and ordinary manual dose commands");
            float began = Time.realtimeSinceStartup;
            Check(shop.BeginManualIncubation(), "Begin the second exact manual batch after the first sale, on night two");
            string practice = shop.Model.Incubator.SpecimenId;
            Check(practice != first && shop.Tutorial.CaptureSave().practice.specimenId == practice &&
                shop.ActiveTutorialLesson == TutorialLesson.Practice && shop.Model.Currency == 6,
                "Practice owns a newly started exact specimen without a duplicate first-sale receipt or extra earnings");
            yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 17,
                "night-two ordinary practice incubation", false);
            Check(Time.realtimeSinceStartup - began >= 9.5f && shop.Model.Incubator.SpecimenId == practice &&
                shop.Tutorial.CaptureSave().practice.ready && shop.TutorialPracticeGuidanceRequested,
                "The actual ten-second practice batch matures and its requested directions remain available");
            Check(shop.TryGetTutorialGuidance(out practiceTarget) && practiceTarget.IsDoor,
                "Requested ready-output directions resolve the real shop door before a front display");
            shop.Player.View.transform.LookAt(practiceTarget.Position);
            yield return TutorialCapture("464-handoff-practice-ready");
            yield return CycleTransfer(StationKind.TankA, "tank-a");
            yield return CycleCounter();
            Check(shop.OpenForDay() && shop.Cycle.DayNumber == 2 && shop.Tutorial.CaptureSave().practice.saleDay == 0,
                "Practice follows the normal second-day opening and is not complete merely because stock is displayed");
            yield return TutorialCheckout(practice, 12);
            Check(shop.Tutorial.CaptureSave().first.saleDay == 1 && shop.Tutorial.CaptureSave().practice.saleDay == 2 &&
                shop.Tutorial.PracticeAssisted && !shop.TutorialPracticeGuidanceRequested &&
                shop.ActiveTutorialLesson == TutorialLesson.CloseAndReview && !shop.FinishGuidedOpening(),
                "Actual second-day manual checkout completes Assisted practice but still requires a later preparation night");
            Check(shop.TutorialPracticeOutcomeForDisplay.IndexOf("assist", StringComparison.OrdinalIgnoreCase) >= 0,
                "The completed practice presentation truthfully identifies requested assistance");
            shop.SetPanel(ShopPanel.None);
            yield return TutorialCapture("465-handoff-close-objective");
            Check(shop.CloseForNight(), "Close second day through the ordinary three-second drain");
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8,
                "practice-sale day ordinary closing", false);
            Check(shop.Cycle.DayNumber == 3 && shop.Reports.Last.dayNumber == 2 &&
                shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == 6 && shop.Model.Currency == 12 &&
                shop.Tutorial.CaptureSave().reportDay == 1 && shop.ActiveTutorialLesson == TutorialLesson.NextPlan,
                "Later night unlocks the handoff using the earlier acknowledged qualifying report, with exactly twelve earned credits");
            Check(shop.Panel == ShopPanel.DayReport, "Second normal closing still presents its actual report");
            yield return TutorialCapture("466-handoff-later-report");
            Check(shop.OpenTutorialHelp() && shop.Panel == ShopPanel.TutorialHelp && shop.Paused,
                "Help safely interrupts the later daily report at the handoff lesson");
            shop.CloseTutorialHelp();
            Check(shop.Panel == ShopPanel.DayReport && shop.Tutorial.CaptureSave().reportDay == 1,
                "Returning from help preserves the report origin and the already recorded report identity");
            shop.BackFromPanel();

            Check(shop.ShowTutorialHandoff() && shop.Panel == ShopPanel.TutorialHelp && shop.Paused &&
                shop.TutorialHelpLesson == TutorialLesson.NextPlan && shop.Config.UpgradeCost == 24 &&
                shop.TutorialUpgradePreviewForDisplay.Contains(shop.Config.UpgradeCost.ToString()) &&
                shop.TutorialUpgradePreviewForDisplay.Contains(shop.Model.Currency.ToString()) && !shop.Model.UpgradeOwned,
                "The handoff displays the live twenty-four-credit upgrade and twelve actual credits without requiring a purchase");
            yield return TutorialCapture("467-handoff-next-plan");
            Check(shop.OpenTutorialUpgradeHelp() && shop.TutorialUpgradeHelpOpen && shop.Paused &&
                shop.Model.Currency == 12 && !shop.Model.UpgradeOwned,
                "Optional upgrade details stay paused and never execute an unaffordable purchase");
            yield return TutorialCapture("468-handoff-upgrade-preview");
            shop.SelectTutorialHelpLesson(TutorialLesson.NextPlan);
            Check(!shop.TutorialUpgradeHelpOpen && shop.OpenTutorialJournal() && shop.Panel == ShopPanel.Journal &&
                shop.TutorialJournalReturnsToHelp && !shop.Paused && shop.Model.IsDiscovered("pondlet"),
                "Optional journal link opens actual discoveries, resumes ordinary shop time and retains a return to handoff help");
            yield return TutorialCapture("469-handoff-journal-link");
            yield return CycleTap(Key.Escape);
            Check(shop.Panel == ShopPanel.TutorialHelp && shop.TutorialHelpLesson == TutorialLesson.NextPlan && shop.Paused,
                "Synthetic Escape returns from the linked journal and pauses again at the handoff");
            Check(shop.Model.Currency == 12 && !shop.Model.UpgradeOwned && shop.ActiveTutorialLesson == TutorialLesson.NextPlan,
                "Optional handoff links leave ordinary money, upgrade ownership and completion authority unchanged");
            shop.CloseTutorialHelp();
            Check(shop.Panel == ShopPanel.None, "Close handoff help returns to ordinary night gameplay");
            yield return CycleWalkTo(new Vector2(0, -1.2f));
            yield return CycleWalkTo(new Vector2(0, 1.9f));
            yield return CycleWalkTo(new Vector2(-3f, 1.9f));
            for (int i = 0; i < 3; i++) { CycleAim(StationOf(StationKind.Upgrade)); yield return null; }
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Upgrade && shop.Model.Currency == 12 && !shop.Model.UpgradeOwned,
                "The existing physical door route reaches the real upgrade bench without a mandatory purchase");
            yield return TutorialCapture("470-handoff-real-bench");
            yield return CycleTap(Key.Escape);
            Check(shop.ShowTutorialHandoff() && shop.SaveNow(), "Return to the explicit handoff and save before the isolated failure probe");
            string beforeFile = File.ReadAllText(shop.SavePath), beforeTutorial = TutorialState();
            string beforeGame = JsonUtility.ToJson(shop.Model.CaptureSave());
            TutorialBlockSave();
            try
            {
                Check(!shop.FinishGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Active &&
                    shop.ActiveTutorialLesson == TutorialLesson.NextPlan && TutorialState() == beforeTutorial &&
                    JsonUtility.ToJson(shop.Model.CaptureSave()) == beforeGame && File.ReadAllText(shop.SavePath) == beforeFile &&
                    !string.IsNullOrEmpty(shop.SaveError),
                    "Failed handoff write keeps active progress, exact economy and original full-slot bytes with a visible save error");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.FinishGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Completed &&
                shop.ActiveTutorialLesson == TutorialLesson.None && string.IsNullOrEmpty(shop.SaveError) &&
                shop.Model.Currency == 12 && !shop.Model.UpgradeOwned && !shop.TutorialPracticeGuidanceRequested,
                "Successful retry atomically completes the handoff, clears the obsolete save error and grants no reward or upgrade");
            string completed = TutorialState();
            Check(!shop.FinishGuidedOpening() && TutorialState() == completed && shop.Model.Currency == 12,
                "Repeated completion cannot change receipts or pay a second reward");
            Check(shop.TutorialObjectiveForDisplay.Contains(shop.Config.UpgradeCost.ToString()) &&
                !shop.TryGetTutorialGuidance(out _),
                "Normal saving-for-upgrade hints return after completion without a lingering tutorial marker");
            yield return TutorialCapture("471-handoff-completed-normal-hints");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && TutorialState() == completed &&
                shop.Cycle.DayNumber == 3 && shop.Cycle.Phase == ShopDayPhase.Night && shop.Model.Currency == 12 &&
                !shop.Model.UpgradeOwned && shop.Tutorial.PracticeAssisted && !shop.TutorialPracticeGuidanceRequested,
                "Exact completed reload preserves both sale days, Assisted attribution, later night and twelve real credits");
            File.Copy(shop.SavePath, Path.Combine(evidence, "tutorial-handoff-completed-route.json"), true);
            yield return TutorialHandoffLostReportRecovery();
        }

        IEnumerator TutorialHandoffLostReportRecovery()
        {
            shop.BackFromPanel();
            yield return CycleCounter();
            Check(shop.OpenForDay() && shop.Cycle.DayNumber == 3 && shop.Model.Currency == 12,
                "Lost-report branch opens an ordinary third day after preserving the completed main route");
            Check(shop.CloseForNight(), "Close the real empty third day without a fabricated sale or phase jump");
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8,
                "third-day zero-sale report replacement", false);
            Check(shop.Cycle.DayNumber == 4 && shop.Reports.Last.dayNumber == 3 && shop.Reports.Last.sales == 0 &&
                shop.Reports.Last.revenue == 0 && shop.Model.Currency == 12 && shop.Tutorial.Mode == TutorialMode.Completed,
                "Normal report retention now holds actual zero-sale day three, beyond both tutorial sale days");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow(), "Save the actual night-four economy before deriving the labelled lost-report fixture");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            fixture.tutorial.mode = TutorialMode.Active;
            fixture.tutorial.handoffCompleted = false;
            fixture.tutorial.reportAcknowledged = false;
            fixture.tutorial.reportDay = 0;
            Check(SaveStore.Write(shop.SavePath, fixture, out var fixtureError) && shop.ReloadSaved(),
                "Load labelled lost-report fixture changing only tutorial completion/acknowledgement flags / " + fixtureError);
            File.Copy(shop.SavePath, Path.Combine(evidence, "tutorial-handoff-lost-report-fixture.json"), true);
            Check(shop.TutorialNeedsReportRecovery && shop.ActiveTutorialLesson == TutorialLesson.CloseAndReview &&
                shop.Tutorial.CaptureSave().first.saleDay == 1 && shop.Tutorial.CaptureSave().practice.saleDay == 2 &&
                shop.Reports.Last.dayNumber == 3 && shop.Model.Currency == 12,
                "Missing older report has a recoverable path based on the two real durable sale receipts");
            Check(shop.OpenTutorialHelp() && shop.Paused && shop.TutorialHelpLesson == TutorialLesson.CloseAndReview,
                "Requested closing help exposes the bounded historical sale-receipt review");
            yield return TutorialCapture("472-handoff-lost-report-recovery");
            string beforeGame = JsonUtility.ToJson(shop.Model.CaptureSave());
            string beforeReport = JsonUtility.ToJson(shop.Reports.Last);
            Check(shop.AcknowledgeTutorialSaleReceipts() && shop.Tutorial.CaptureSave().reportAcknowledged &&
                shop.Tutorial.CaptureSave().reportDay == 2 && !shop.TutorialNeedsReportRecovery &&
                shop.ActiveTutorialLesson == TutorialLesson.NextPlan && shop.TutorialHelpLesson == TutorialLesson.NextPlan &&
                JsonUtility.ToJson(shop.Model.CaptureSave()) == beforeGame && JsonUtility.ToJson(shop.Reports.Last) == beforeReport,
                "Explicit receipt review records the actual practice-sale day without inventing old totals or changing money, stock or the latest report");
            Check(shop.FinishGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Completed && shop.Model.Currency == 12,
                "Recovered report review permits the ordinary completion command without a bonus or forced purchase");
            string completed = TutorialState();
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && TutorialState() == completed &&
                shop.Cycle.DayNumber == 4 && shop.Model.Currency == 12 && !shop.Model.UpgradeOwned &&
                shop.Tutorial.CaptureSave().reportDay == 2 && shop.Reports.Last.dayNumber == 3 && shop.Reports.Last.sales == 0,
                "Recovered completion reload retains truthful older receipt identity and the independent zero-sale latest report");
            File.Copy(shop.SavePath, Path.Combine(evidence, "tutorial-handoff-recovered-route.json"), true);
        }
    }
}
#endif
