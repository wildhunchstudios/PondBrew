#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // Deliberately separate from later TUT-06/07 guidance/customer policy and the
        // TUT-09..14 release gates. All menu commands here are synthetic/API evidence.
        IEnumerator TutorialScenario()
        {
            limitations.Add("TUT-04/05 isolated integration route: synthetic WASD/E/Escape/mouse movement and held collection, scripted station aim, guarded application menu actions, and ordinary ten-second incubation. No native operating-system mouse clicks or human learning/pacing acceptance is claimed.");
            limitations.Add("Customer arrivals run with the ordinary two-visitor/reputation catalog only while waiting for each checkout; arrivals are disabled during controlled preparation. This is an integration test, not proof of the planned first-customer production policy. Ambient comedy is disabled only in this isolated validation session.");
            limitations.Add("Failure cases create an empty .tmp directory only beside the isolated smoke slot. Legacy help and assisted-practice help use explicitly labelled derived fixtures after the ordinary route. Focus loss is invoked through the real callback as a synthetic lifecycle event, not an operating-system focus test. Tutorial world markers, TUT-07 pacing, native input coverage and tester acceptance remain separate gates.");
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionTutorialSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Tutorial route uses only its explicitly isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors == 2 &&
                !shop.Customers.UseBaseReputationEffectsForValidation && shop.Customers.ForcedArchetypeId == null,
                "Tutorial integration starts with ordinary customer catalog, concurrency and reputation rules");
            Check(shop.Model.Currency == 0 && shop.DisplayedStockCount() == 0 && !shop.Model.UpgradeOwned &&
                shop.Model.DiscoveredSpeciesIds.Count == 0 && shop.Cycle.DayNumber == 1 && shop.Cycle.Phase == ShopDayPhase.Night,
                "Fresh tutorial starts at ordinary zero-credit night one without rewards or seeded discoveries");
            Check(Mathf.Approximately(shop.Config.IncubationSeconds, 10) && shop.ScriptedHarvest == null,
                "Tutorial keeps the normal incubation duration and genuine collection path");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTutorialKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTutorialMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;

            Check(shop.Tutorial.Mode == TutorialMode.Offered && shop.ActiveTutorialLesson == TutorialLesson.None,
                "Fresh tutorial is offered without silently starting or inventing lesson progress");
            shop.SetPanel(ShopPanel.Welcome);
            Check(shop.Paused && Cursor.visible && Cursor.lockState == CursorLockMode.None,
                "Fresh welcome pauses simulation and exposes the pointer");
            yield return TutorialCapture("400-tutorial-offer");
            Check(shop.SaveNow(), "Save isolated offered state before start-failure probe");
            string beforeFile = File.ReadAllText(shop.SavePath), beforeTutorial = TutorialState();
            var beforeModel = shop.Model;
            TutorialBlockSave();
            try
            {
                Check(!shop.StartGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Offered &&
                    TutorialState() == beforeTutorial && ReferenceEquals(beforeModel, shop.Model) &&
                    File.ReadAllText(shop.SavePath) == beforeFile && !string.IsNullOrEmpty(shop.SaveError),
                    "Failed guided-start write preserves offered mode, live game and original file with a visible error");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.StartGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Active &&
                shop.Panel == ShopPanel.None && shop.Model.Currency == 0,
                "Successful guided start publishes once without money or duplicate mode transition");
            beforeTutorial = TutorialState(); shop.StartGuidedOpening();
            Check(TutorialState() == beforeTutorial && shop.Model.Currency == 0,
                "Repeated guided-start command leaves exact progress and money unchanged");
            Check(shop.ActiveTutorialLesson == TutorialLesson.Orient && shop.Tutorial.Facts == TutorialFacts.None,
                "Guided start requires genuine orientation rather than the existing spawn position");
            shop.AllowValidationInput = true;
            cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(24, -8) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState()); yield return null;
            Check((shop.Tutorial.Facts & TutorialFacts.Looked) != 0,
                "Synthetic mouse delta reaches the real player look observer");
            yield return CycleGoStation(StationKind.Bloom);
            Check((shop.Tutorial.Facts & (TutorialFacts.Moved | TutorialFacts.LabVisited)) ==
                (TutorialFacts.Moved | TutorialFacts.LabVisited) && shop.ActiveTutorialLesson == TutorialLesson.Collect,
                "Synthetic walking through the physical lab doorway completes movement and lab orientation");
            yield return TutorialCapture("401-tutorial-collection-objective");
            Check(!shop.BeginManualIncubation() && !shop.DoseCulture(CultureKind.Bloom) &&
                shop.ActiveTutorialLesson == TutorialLesson.Collect,
                "Invalid out-of-panel manual commands cannot grant preparation or production facts");

            yield return TutorialProduce(false);
            string first = shop.Model.Incubator.SpecimenId;
            Check(shop.ActiveTutorialLesson == TutorialLesson.Display && shop.Tutorial.CaptureSave().first.specimenId == first,
                "First normal ten-second batch becomes the exact tutorial display target");
            yield return CycleTransfer(StationKind.TankA, "tank-a");
            Check(shop.ActiveTutorialLesson == TutorialLesson.PriceAndOpen && shop.Tutorial.CaptureSave().first.manuallyDisplayed,
                "Synthetic E stocks the exact first creature and advances through the actual transfer hook");
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Tank && (shop.Tutorial.Facts & TutorialFacts.PriceViewed) != 0,
                "Opening the real occupied display records price inspection without forcing a price change");
            Check(shop.OpenTutorialHelp() && shop.Paused && shop.TutorialHelpLesson != TutorialLesson.None,
                "Display Help opens a paused named lesson");
            yield return TutorialCapture("403-tutorial-help-from-display");
            yield return CycleTap(Key.Escape);
            Check(shop.Panel == ShopPanel.Tank && !shop.Paused,
                "Synthetic Escape returns from Help to the still-valid display panel");
            yield return CycleCounter();
            Check(shop.OpenForDay() && shop.ActiveTutorialLesson == TutorialLesson.Serve && shop.Model.Currency == 0,
                "Normal counter opening advances tutorial and preserves unearned money");
            yield return TutorialCheckout(first, 6);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Practice && !shop.Tutorial.FirstSaleAccommodationActive &&
                shop.Tutorial.CaptureSave().first.saleDay == 1 && !shop.Tutorial.PracticeAssisted,
                "Actual manual checkout records first-sale day, ends accommodation eligibility and begins unassisted practice");
            shop.SetPanel(ShopPanel.None);
            yield return TutorialCapture("404-tutorial-practice-objective");

            yield return TutorialProduce(true);
            string practice = shop.Model.Incubator.SpecimenId;
            Check(practice != first && shop.Tutorial.CaptureSave().practice.specimenId == practice &&
                shop.ActiveTutorialLesson == TutorialLesson.Practice && !shop.Tutorial.PracticeAssisted,
                "A new manual batch started after the first sale becomes the independent-practice specimen");
            yield return CycleTransfer(StationKind.TankA, "tank-a");
            yield return CycleCounter();
            yield return TutorialCheckout(practice, 12);
            Check(shop.ActiveTutorialLesson == TutorialLesson.CloseAndReview &&
                shop.Tutorial.CaptureSave().practice.saleDay == 1 && !shop.Tutorial.PracticeAssisted,
                "Second manually stocked and served specimen completes ordinary unassisted practice");
            Check(shop.CloseForNight(), "Request ordinary bounded closing after two manual tutorial sales");
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8, "tutorial closing drain", false);
            Check(shop.Cycle.DayNumber == 2 && shop.Reports.Last != null && shop.Reports.Last.sales == 2 &&
                shop.Reports.Last.revenue == 12 && shop.Model.Currency == 12,
                "Normal night transition and daily report contain exactly two six-credit sales");
            Check(shop.ShowDayReport(), "Open the real completed day report");
            Check(shop.OpenTutorialHelp() && !shop.Tutorial.CaptureSave().reportAcknowledged,
                "Opening Help from report is an interruption rather than false report acknowledgement");
            shop.CloseTutorialHelp();
            Check(shop.Panel == ShopPanel.DayReport && !shop.Tutorial.CaptureSave().reportAcknowledged,
                "Closing Help restores the pending daily report");
            yield return TutorialCapture("405-tutorial-daily-report");
            shop.BackFromPanel();
            Check(shop.Tutorial.CaptureSave().reportAcknowledged && shop.ActiveTutorialLesson == TutorialLesson.NextPlan,
                "Leaving the qualifying report in the next night records acknowledgement and offers a next plan");
            Check(shop.OpenTutorialHelp(), "Open optional next-plan help without requiring an unaffordable purchase");
            yield return TutorialCapture("406-tutorial-next-plan");
            shop.ScrollPanel(10000);
            yield return TutorialCapture("406-tutorial-next-plan-finish");
            Check(shop.SaveNow(), "Save final lesson before handoff fault injection");
            beforeFile = File.ReadAllText(shop.SavePath); beforeTutorial = TutorialState(); beforeModel = shop.Model;
            TutorialBlockSave();
            try
            {
                Check(!shop.FinishGuidedOpening() && TutorialState() == beforeTutorial &&
                    ReferenceEquals(beforeModel, shop.Model) && File.ReadAllText(shop.SavePath) == beforeFile,
                    "Failed completion write preserves active next-plan state and exact game");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.FinishGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Completed &&
                shop.ActiveTutorialLesson == TutorialLesson.None && shop.Model.Currency == 12 &&
                !shop.Model.UpgradeOwned,
                "Finish publishes completed mode once with no tutorial reward or forced upgrade");
            beforeTutorial = TutorialState(); shop.FinishGuidedOpening();
            Check(TutorialState() == beforeTutorial && shop.Model.Currency == 12,
                "Repeated completion leaves exact learning receipts and money unchanged");
            shop.SetPanel(ShopPanel.Pause);
            beforeTutorial = TutorialState();
            Check(shop.SaveNow() && shop.ReloadSaved() && TutorialState() == beforeTutorial && shop.Model.Currency == 12,
                "Completed learning receipts and ordinary earnings survive an exact saved reload");
            string completedFile = File.ReadAllText(shop.SavePath);
            File.WriteAllText(Path.Combine(evidence, "tutorial-completed-route.json"), completedFile);
            yield return TutorialBranchChecks(completedFile);
            Check(shop.SaveNow() && shop.Tutorial.Mode == TutorialMode.Completed && shop.Model.Currency == 12,
                "Scenario finishes on the saved completed ordinary two-sale route");
        }

        IEnumerator TutorialProduce(bool practice)
        {
            yield return CycleHarvest(CultureKind.Bloom);
            yield return CycleHarvest(CultureKind.Flow);
            if (!practice) Check(shop.ActiveTutorialLesson == TutorialLesson.Prepare,
                "Real held collection of both cultures advances the preparation lesson");
            yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Incubator && shop.DoseCulture(CultureKind.Flow) &&
                shop.DoseCulture(CultureKind.Bloom), "Manual incubation wrapper accepts the Pondlet cultures in either order");
            if (!practice)
            {
                Check(shop.ActiveTutorialLesson == TutorialLesson.Grow && shop.Model.Staged(CultureKind.Bloom) == 1 &&
                    shop.Model.Staged(CultureKind.Flow) == 1, "Actual staged valid recipe completes preparation without granting extra cultures");
                Check(shop.OpenTutorialHelp() && shop.Paused && Cursor.visible && Cursor.lockState == CursorLockMode.None,
                    "Incubator Help pauses and releases cursor");
                shop.SelectTutorialHelpLesson(TutorialLesson.Collect);
                Check(shop.TutorialHelpLesson == TutorialLesson.Collect, "Help can replay the collection lesson");
                shop.CloseTutorialHelp();
                Check(shop.Panel == ShopPanel.Incubator, "Help returns to its valid incubator origin");
            }
            float began = Time.realtimeSinceStartup;
            Check(shop.BeginManualIncubation() && shop.Panel == ShopPanel.None &&
                shop.Model.Incubator.Phase == IncubatorPhase.Incubating,
                "Successful manual start closes the recipe panel and begins the ordinary machine");
            string specimen = shop.Model.Incubator.SpecimenId;
            if (!practice)
            {
                Check(shop.OpenTutorialHelp(), "Open Help while the exact first batch is incubating");
                float countdown = shop.Model.Incubator.SecondsRemaining;
                yield return new WaitForSeconds(.4f);
                Check(shop.Model.Incubator.SecondsRemaining == countdown,
                    "Requested Help freezes actual incubation rather than allowing hidden work");
                string tutorial = TutorialState();
                Check(shop.SaveNow() && shop.ReloadSaved() && TutorialState() == tutorial &&
                    shop.Model.Incubator.SpecimenId == specimen && shop.Model.Incubator.SecondsRemaining == countdown,
                    "Mid-batch reload preserves exact specimen, countdown and tutorial learning receipt together");
                shop.CloseTutorialHelp();
                Check(shop.Panel == ShopPanel.None && !shop.Paused, "Closing restored Help resumes ordinary gameplay");
            }
            yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 17, "tutorial natural incubation", false);
            Check(Time.realtimeSinceStartup - began >= 9.5f && shop.Model.Incubator.SpeciesId == "pondlet" &&
                shop.Model.Incubator.SpecimenId == specimen,
                "Ten-second runtime production matures the exact manually started Pondlet without time acceleration");
        }

        IEnumerator TutorialCheckout(string specimen, int expectedCash)
        {
            shop.Customers.ArrivalsEnabled = true;
            yield return CycleUntil(() => shop.Customers.NextToServe != null &&
                shop.Customers.NextToServe.Phase == CustomerPhase.Queued, 60, "ordinary tutorial customer queued at checkout", true);
            shop.Customers.ArrivalsEnabled = false;
            var visit = shop.Customers.NextToServe;
            var tag = shop.Model.GetPrice("pondlet").Value;
            int cashBefore = shop.Model.Currency, agreed = visit.Price;
            Check(visit.Price == tag.Price && tag.Price == tag.Standard && shop.Model.GetTank("tank-a").HasValue &&
                shop.Model.GetTank("tank-a").Value.Id == specimen,
                "A real queued visitor reserves the expected specimen at its unchanged standard price / phase=" + visit.Phase +
                " agreed=" + agreed + " tag=" + tag.Price + " standard=" + tag.Standard + " expectedSpecimen=" + specimen +
                " actualSpecimen=" + (shop.Model.GetTank("tank-a").HasValue ? shop.Model.GetTank("tank-a").Value.Id : "none"));
            Check(shop.IsAtCounter && shop.Panel == ShopPanel.Counter && shop.ServeCustomer() &&
                shop.Model.Currency == cashBefore + agreed && shop.Model.Currency == expectedCash &&
                !shop.Model.GetTank("tank-a").HasValue && !shop.ServeCustomer(),
                "Guarded in-person checkout removes one specimen and pays its agreed quote exactly once / agreed=" + agreed +
                " before=" + cashBefore + " actual=" + shop.Model.Currency + " expected=" + expectedCash);
        }

        IEnumerator TutorialBranchChecks(string completedFile)
        {
            Check(shop.OpenTutorialHelp(), "Completed tutorial keeps optional Help available");
            string before = TutorialState();
            shop.SelectTutorialHelpLesson(TutorialLesson.Grow); shop.SelectTutorialHelpLesson(TutorialLesson.Orient);
            Check(TutorialState() == before && shop.Model.Currency == 12,
                "Completed Help replay cannot reset progress, grant learning or alter money");
            yield return TutorialCapture("407-tutorial-completed-help");
            shop.CloseTutorialHelp();
            shop.SetPanel(ShopPanel.Reset); TutorialBlockSave();
            try
            {
                shop.ConfirmNewGame();
                Check(shop.Tutorial.Mode == TutorialMode.Completed && TutorialState() == before &&
                    shop.Model.Currency == 12 && shop.Panel == ShopPanel.Reset && File.ReadAllText(shop.SavePath) == completedFile,
                    "Failed New Game write preserves completed progress, money and disk bytes");
            }
            finally { TutorialUnblockSave(); }
            shop.BackFromPanel();
            Check(shop.Tutorial.Mode == TutorialMode.Completed && shop.Model.Currency == 12,
                "Canceling reset leaves tutorial and gameplay intact");
            shop.SetPanel(ShopPanel.Reset); shop.ConfirmNewGame();
            Check(shop.Tutorial.Mode == TutorialMode.Offered && shop.Panel == ShopPanel.Welcome && shop.Model.Currency == 0,
                "Successful explicit New Game alone returns to the fresh offered welcome");
            Check(shop.StartGuidedOpening(), "Begin isolated skip/recovery branch after explicit New Game");
            yield return CycleHarvest(CultureKind.Bloom); yield return CycleHarvest(CultureKind.Flow);
            yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow) && shop.BeginManualIncubation(),
                "Skip branch starts a real batch from manually collected resources");
            Check(shop.OpenTutorialHelp(), "Open Help for an active skip branch");
            Check(shop.SaveNow(), "Save skip branch before deterministic write obstruction");
            string file = File.ReadAllText(shop.SavePath); before = TutorialState();
            string skipGameplay = JsonUtility.ToJson(shop.Model.CaptureSave());
            string skippedSpecimen = shop.Model.Incubator.SpecimenId;
            TutorialBlockSave();
            try
            {
                Check(!shop.SkipTutorial() && TutorialState() == before && shop.Tutorial.Mode == TutorialMode.Active &&
                    JsonUtility.ToJson(shop.Model.CaptureSave()) == skipGameplay && File.ReadAllText(shop.SavePath) == file,
                    "Failed skip cannot publish skipped state, change the real batch or replace original bytes");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.SkipTutorial() && shop.Tutorial.Mode == TutorialMode.Skipped && shop.Model.Currency == 0 &&
                JsonUtility.ToJson(shop.Model.CaptureSave()) == skipGameplay && shop.Model.Incubator.SpecimenId == skippedSpecimen &&
                !shop.StartGuidedOpening(),
                "Successful skip preserves the exact active batch and is idempotent without replay or refunded cultures");
            before = TutorialState(); shop.SkipTutorial();
            Check(TutorialState() == before && JsonUtility.ToJson(shop.Model.CaptureSave()) == skipGameplay,
                "Repeated skip preserves the real batch and exact terminal learning receipts");
            shop.SetPanel(ShopPanel.Pause); before = TutorialState();
            Check(shop.SaveNow() && shop.ReloadSaved() && TutorialState() == before,
                "Skipped mode survives exact saved reload");
            Check(shop.OpenTutorialHelp(), "Skipped games retain Help");
            shop.SelectTutorialHelpLesson(TutorialLesson.Serve);
            Check(TutorialState() == before, "Skipped Help replay is read-only");
            shop.CloseTutorialHelp(); shop.SetPanel(ShopPanel.None);
            // Invoke the existing production callback with validation suppression temporarily
            // lifted. The save path remains the explicitly checked isolated slot throughout.
            var validationProperty = typeof(ShopSession).GetProperty("ValidationMode");
            var focus = typeof(ShopSession).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            validationProperty.SetValue(shop, false);
            try { focus.Invoke(shop, new object[] { false }); }
            finally { validationProperty.SetValue(shop, true); }
            Check(shop.Panel == ShopPanel.Pause && shop.Paused && Cursor.visible,
                "Synthetic production focus-loss callback pauses and exposes the pointer");
            Check(shop.OpenTutorialHelp(), "Help can originate in pause");
            shop.BackFromPanel();
            Check(shop.Panel == ShopPanel.Pause && shop.Paused, "Back from pause-origin Help stays paused");

            var legacy = JsonUtility.FromJson<SlotData>(completedFile);
            legacy.formatVersion = 19; legacy.tutorial = null;
            File.WriteAllText(shop.SavePath, JsonUtility.ToJson(legacy, true));
            Check(shop.ReloadSaved() && shop.Tutorial.Mode == TutorialMode.LegacyUnguided && shop.Model.Currency == 12,
                "Labelled format-19 route migrates to legacy unguided without invented tutorial completion or money");
            Check(shop.OpenTutorialHelp(), "Legacy game can request optional tutorial Help");
            before = TutorialState(); string gameplay = JsonUtility.ToJson(shop.Model.CaptureSave());
            shop.SelectTutorialHelpLesson(TutorialLesson.Prepare); shop.SelectTutorialHelpLesson(TutorialLesson.NextPlan);
            yield return TutorialCapture("408-tutorial-legacy-help");
            Check(TutorialState() == before && JsonUtility.ToJson(shop.Model.CaptureSave()) == gameplay &&
                !shop.StartGuidedOpening() && !shop.FinishGuidedOpening(),
                "Legacy Help is paused and read-only, and cannot silently start or complete interactive guidance");
            shop.CloseTutorialHelp(); shop.SetPanel(ShopPanel.Pause);

            var assisted = JsonUtility.FromJson<SlotData>(completedFile);
            assisted.tutorial.mode = TutorialMode.Active; assisted.tutorial.handoffCompleted = false;
            assisted.tutorial.practice = new TutorialChainSave(); assisted.tutorial.reportAcknowledged = false;
            assisted.tutorial.reportDay = 0;
            Check(SaveStore.Write(shop.SavePath, assisted, out var assistedError) && shop.ReloadSaved() &&
                shop.ActiveTutorialLesson == TutorialLesson.Practice && !shop.Tutorial.PracticeAssisted,
                "Load explicitly labelled post-sale practice-help fixture without altering its existing ordinary earnings / " + assistedError);
            Check(shop.OpenTutorialHelp() && shop.Tutorial.PracticeAssisted && shop.Model.Currency == 12,
                "Requesting Help during eligible practice records assistance without changing earnings");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Tutorial.PracticeAssisted,
                "Assisted-practice receipt survives an exact saved reload");
            shop.CloseTutorialHelp(); shop.SetPanel(ShopPanel.Pause);
            File.WriteAllText(shop.SavePath, completedFile);
            Check(shop.ReloadSaved() && shop.Tutorial.Mode == TutorialMode.Completed && shop.Model.Currency == 12,
                "Restore the ordinary completed route after all labelled branch probes");
        }

        string TutorialState() => JsonUtility.ToJson(shop.Tutorial.CaptureSave());
        void ObserveTutorialNavigation()
        {
            if (Time.realtimeSinceStartup - cycleStarted > 360f)
                throw new TimeoutException("Tutorial integration scenario exceeded its six-minute watchdog");
            if (shop.Customers.ActiveCount > shop.Customers.MaxVisitors)
                throw new InvalidOperationException("Tutorial customer concurrency exceeded the live bound / active=" +
                    shop.Customers.ActiveCount + " maximum=" + shop.Customers.MaxVisitors);
            foreach (var visit in shop.Customers.Visits)
            {
                var actor = shop.Customers.ActorOf(visit);
                if (!actor) throw new InvalidOperationException("Tutorial active visitor has no actor / " + visit.Id + " phase=" + visit.Phase);
                customerNavigationSamples++;
                if (!shop.Customers.Navigation.IsInStore(actor.transform.position))
                    throw new InvalidOperationException("Tutorial visitor entered the lab or left front-store bounds / " +
                        visit.Id + " phase=" + visit.Phase + " position=" + actor.transform.position);
            }
        }
        void TutorialBlockSave()
        {
            string path = shop.SavePath + ".tmp";
            Check(!File.Exists(path) && !Directory.Exists(path), "Isolated temporary obstruction path is unused");
            Directory.CreateDirectory(path);
        }
        void TutorialUnblockSave()
        {
            string path = shop.SavePath + ".tmp";
            if (Directory.Exists(path)) Directory.Delete(path, false);
        }
        IEnumerator TutorialCapture(string name)
        {
            // Both sizes are actual player surfaces only in standalone. Editor camera
            // exports are useful diagnostics but never evidence of native UI usability.
            if (!Application.isEditor)
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f);
                yield return GateCapture(name + "-720");
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f);
                yield return GateCapture(name + "-1080");
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f);
            }
            else yield return GateCapture(name + "-editor");
        }
    }
}
#endif
