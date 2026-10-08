using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// Isolated session commands, live station identities and ordinary model transactions.
    /// Fixtures do not claim native input, customer travel or wall-clock timing.
    /// </summary>
    public sealed class TutorialHandoffTests
    {
        const string Prefix = "EvolutionShop-TutorialHandoff-";
        GameObject owner, scene;
        ShopSession session;
        ShopCustomers customers;
        ShopWorld world;
        ShopPlayer player;
        List<Station> stations;
        Station bloom, flow, incubator, tankA, tankB, counter;
        string directory, path;
        CursorLockMode cursorLock;
        bool cursorVisible;

        [SetUp]
        public void Setup()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "slot.json");
            owner = new GameObject("Inactive tutorial handoff authority"); owner.SetActive(false);
            scene = new GameObject("Isolated tutorial handoff stations");
            session = owner.AddComponent<ShopSession>(); customers = owner.AddComponent<ShopCustomers>();
            Set("Config", GameConfig.CreateDefault()); Set("Model", new GameModel(session.Config));
            Set("Cycle", new ShopDayCycle()); Set("Reports", new ShopTradeLedger());
            Set("Reputation", new ShopReputation()); Set("Layout", new ShopLayout());
            Set("Staff", new ShopStaff()); Set("Stocking", new ShopStocking()); Set("Laboratory", new ShopLaboratory());
            Set("SavePath", path); Set("Customers", customers);
            Field(customers, "shop", session); Field(customers, "catalog", CustomerArchetypes.CreateDefault());
            world = Child("World").AddComponent<ShopWorld>(); Set("World", world);
            stations = (List<Station>)typeof(ShopWorld).GetField("stations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
            player = Child("Player").AddComponent<ShopPlayer>(); player.enabled = false;
            var camera = Child("Camera").AddComponent<Camera>(); camera.enabled = false;
            player.Initialize(camera); Set("Player", player);
            bloom = StationFixture(StationKind.Bloom, new Vector3(-4, 0, 4), ShopLayout.BloomFacilityId);
            flow = StationFixture(StationKind.Flow, new Vector3(-1, 0, 4), ShopLayout.FlowFacilityId);
            incubator = StationFixture(StationKind.Incubator, new Vector3(2, 0, 4), ShopLayout.IncubatorFacilityId);
            tankA = StationFixture(StationKind.TankA, new Vector3(-4, 0, -3), tank: "tank-a");
            tankB = StationFixture(StationKind.TankB, new Vector3(-1, 0, -3), tank: "tank-b");
            counter = StationFixture(StationKind.Counter, new Vector3(4, 0, -3));
            StationFixture(StationKind.Upgrade, new Vector3(4, 0, 4), ShopLayout.UpgradeFacilityId);
            var door = Child("Door").AddComponent<ShopRoomDoor>(); door.transform.position = new Vector3(0, 0, .5f);
            typeof(ShopWorld).GetProperty("LabDoor").SetValue(world, door);
            player.Teleport(new Vector3(0, 0, 2), Quaternion.identity);
        }

        [TearDown]
        public void Cleanup()
        {
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
            if (scene) UnityEngine.Object.DestroyImmediate(scene);
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory), Does.StartWith(Prefix));
            Directory.Delete(directory, true);
        }

        [Test]
        public void DefaultPracticeOffersOnlyGoalAndReadOnlyQueriesCannotRequestDirections()
        {
            EnterPractice(); Assert.That(session.SaveNow(), Is.True, session.SaveError);
            var saved = File.ReadAllBytes(path); string before = State();
            for (int i = 0; i < 12; i++)
            {
                Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
                Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
                Assert.That(session.TutorialGuidanceRecovery, Is.Null.Or.Empty);
                Assert.That(session.TutorialObjectiveForDisplay, Does.Contain("another creature"));
                Set("Panel", ShopPanel.Incubator); Assert.That(session.TutorialLocalCallout, Is.Null.Or.Empty);
                Set("Panel", ShopPanel.None);
            }
            Assert.That(State(), Is.EqualTo(before)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved));
            Assert.That(session.Tutorial.PracticeAssisted, Is.False);
        }

        [Test]
        public void UrgentCustomerTextDoesNotTurnIndependentPracticeIntoUnrequestedRouteCoaching()
        {
            EnterPractice(); Produce(true, "tank-a"); AddQueuedVisitor("tank-a");
            Assert.That(session.TutorialObjectiveForDisplay, Does.Contain("customer is waiting"));
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Set("Panel", ShopPanel.Counter); Assert.That(session.TutorialLocalCallout, Is.Null.Or.Empty);
            Assert.That(session.Tutorial.PracticeAssisted, Is.False);
        }

        [Test]
        public void ShowMeRequiresCurrentHelpAndExplicitRequestThenCanReturnToGoalOnly()
        {
            EnterPractice(); Assert.That(session.ShowTutorialTarget(), Is.False);
            Assert.That(session.CanShowTutorialTarget, Is.False);
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True);
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            session.SelectTutorialHelpLesson(TutorialLesson.Collect);
            Assert.That(session.CanShowTutorialTarget, Is.False); Assert.That(session.ShowTutorialTarget(), Is.False);
            session.SelectTutorialHelpLesson(TutorialLesson.Practice);
            Assert.That(session.CanShowTutorialTarget, Is.True); Assert.That(session.ShowTutorialTarget(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.True);
            Assert.That(Target().Station, Is.SameAs(bloom));
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.StopTutorialPracticeGuidance(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True);
            Assert.That(session.StopTutorialPracticeGuidance(), Is.False);
            ReadSlot(out _, out var loaded); Assert.That(loaded.tutorial.practiceAssisted, Is.True);
        }

        [Test]
        public void RestoringSavedPracticeKeepsAssistanceButDropsRequestedDirections()
        {
            EnterPractice(); RequestDirections(); Assert.That(session.SaveNow(), Is.True, session.SaveError);
            ReadSlot(out var model, out var loaded);
            Set("Model", model); Invoke("RestoreTutorial", loaded.tutorial);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True);
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Practice));
            Assert.That(session.Model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void FailedAssistanceSaveBlocksShowMeUntilRetryWithoutChangingGameplay()
        {
            EnterPractice(); Assert.That(session.SaveNow(), Is.True, session.SaveError);
            byte[] before = File.ReadAllBytes(path); string game = GameState();
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True);
            Assert.That(session.SaveError, Is.Not.Null.And.Not.Empty);
            Assert.That(session.CanShowTutorialTarget, Is.False); Assert.That(session.ShowTutorialTarget(), Is.False);
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(GameState(), Is.EqualTo(game)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Directory.Delete(path + ".tmp", false);
            Assert.That(session.SaveNow(), Is.True, session.SaveError); Assert.That(session.ShowTutorialTarget(), Is.True);
            ReadSlot(out _, out var loaded); Assert.That(loaded.tutorial.practiceAssisted, Is.True);
        }

        [TestCase(TutorialMode.Offered)]
        [TestCase(TutorialMode.Skipped)]
        [TestCase(TutorialMode.Completed)]
        [TestCase(TutorialMode.LegacyUnguided)]
        public void ReferenceHelpNeverReactivatesInteractivePractice(TutorialMode mode)
        {
            if (mode == TutorialMode.Skipped) Assert.That(session.SkipTutorial(), Is.True);
            if (mode == TutorialMode.Completed) { NextPlan(); Assert.That(session.FinishGuidedOpening(), Is.True); }
            if (mode == TutorialMode.LegacyUnguided) Set("Tutorial", ShopTutorial.LegacyUnguided());
            string before = State(); Assert.That(session.OpenTutorialHelp(), Is.True);
            session.SelectTutorialHelpLesson(TutorialLesson.Practice);
            Assert.That(session.CanShowTutorialTarget, Is.False); Assert.That(session.ShowTutorialTarget(), Is.False);
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(session.StopTutorialPracticeGuidance(), Is.False);
            Assert.That(State(), Is.EqualTo(before)); Assert.That(session.Tutorial.Mode, Is.EqualTo(mode));
        }

        [Test]
        public void SkipClearsRequestedDirectionsAndKeepsEarnedAssistanceAndShop()
        {
            EnterPractice(); RequestDirections(); string game = GameState();
            Assert.That(session.SkipTutorial(), Is.True, session.SaveError);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Skipped));
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True); Assert.That(GameState(), Is.EqualTo(game));
            ReadSlot(out var model, out var loaded); Assert.That(loaded.tutorial.mode, Is.EqualTo(TutorialMode.Skipped));
            Assert.That(model.Currency, Is.EqualTo(6)); Assert.That(loaded.tutorial.practiceAssisted, Is.True);
        }

        [Test]
        public void RequestedPracticeUsesCurrentResourcesAndPreservesValidAlternativeRecipe()
        {
            EnterPractice(); RequestDirections();
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.Collect));
            Assert.That(Target().Station, Is.SameAs(bloom));
            Collect(CultureKind.Flow); Assert.That(Target().Station, Is.SameAs(bloom));
            Collect(CultureKind.Bloom); Collect(CultureKind.Bloom);
            Assert.That(Target().Station, Is.SameAs(incubator));
            Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Flow));
            Set("Panel", ShopPanel.Incubator);
            Assert.That(session.TutorialLocalCallout, Does.Contain("Bubblebelly").And.Not.Contain("repair"));
            Set("Panel", ShopPanel.None);
            var begun = session.Model.BeginIncubation(); Ok(begun);
            Assert.That(session.Tutorial.ObserveStarted(session.Model, begun, TutorialActionOrigin.Player), Is.True);
            Assert.That(session.Tutorial.PracticeSpecimenId, Is.EqualTo(begun.SpecimenId));
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.Grow));
            Assert.That(Target().Station, Is.SameAs(incubator));
        }

        [Test]
        public void WrongPracticeMixtureOnlyExplainsConcreteRepairAfterRequestedDirections()
        {
            EnterPractice(); Collect(CultureKind.Bloom); Collect(CultureKind.Bloom); Collect(CultureKind.Bloom);
            Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Bloom));
            Set("Panel", ShopPanel.Incubator); Assert.That(session.TutorialLocalCallout, Is.Null.Or.Empty);
            Set("Panel", ShopPanel.None); RequestDirections();
            Set("Panel", ShopPanel.Incubator);
            Assert.That(session.TutorialLocalCallout, Does.Contain("Bloom").And.Contain("2").And.Contain("return"));
            Assert.That(session.Model.Staged(CultureKind.Bloom), Is.EqualTo(3), "A hint cannot repair inventory itself.");
            Ok(session.Model.ReturnDose(CultureKind.Bloom)); Ok(session.Model.ReturnDose(CultureKind.Bloom));
            Set("Panel", ShopPanel.None); Assert.That(Target().Station, Is.SameAs(flow));
        }

        [Test]
        public void ReadyPracticeTargetsAnActuallyEmptyDisplayAndFollowsItsLiveMove()
        {
            EnterPractice(); Produce(false, "tank-a"); // unrelated occupied display
            Produce(true); RequestDirections();
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.Display));
            player.Teleport(new Vector3(0, 0, -1), Quaternion.identity);
            var original = Target(); Assert.That(original.Station, Is.SameAs(tankB));
            string before = State(); Vector3 delta = new Vector3(.5f, 0, -1);
            tankB.transform.position += delta;
            var moved = Target(); Assert.That(moved.Station, Is.SameAs(tankB));
            Assert.That(Vector3.Distance(moved.Position, original.Position + delta), Is.LessThan(.001f));
            Assert.That(State(), Is.EqualTo(before));
        }

        [TestCase("disabled")]
        [TestCase("inactive")]
        [TestCase("destroyed")]
        public void RemovedPracticeDisplayDoesNotLeaveAnOutlineOnTheStaleObject(string removal)
        {
            EnterPractice(); Produce(false, "tank-a"); Produce(true); RequestDirections();
            player.Teleport(new Vector3(0, 0, -1), Quaternion.identity); Assert.That(Target().Station, Is.SameAs(tankB));
            if (removal == "disabled") tankB.enabled = false;
            else if (removal == "inactive") tankB.gameObject.SetActive(false);
            else UnityEngine.Object.DestroyImmediate(tankB.gameObject);
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.TutorialGuidanceRecovery, Is.Not.Null.And.Not.Empty);
            Assert.That(session.Model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
        }

        [Test]
        public void ARealPracticeSaleEndsRequestedDirectionsWithoutCompletingHandoff()
        {
            EnterPractice(); RequestDirections(); Produce(true, "tank-a");
            Assert.That(session.Tutorial.PracticeManuallyDisplayed, Is.True);
            Sell("tank-a");
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.CloseAndReview));
            Assert.That(session.FinishGuidedOpening(), Is.False);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Active)); Assert.That(session.Model.Currency, Is.EqualTo(12));
        }

        [Test]
        public void UnrelatedStockCannotImpersonateThePracticeSpecimenBeforeOrAfterItMoves()
        {
            EnterPractice(); Produce(false, "tank-a"); RequestDirections();
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.Collect), "Existing stock is not the new practice batch.");
            Produce(true, "tank-b");
            string tracked = session.Tutorial.PracticeSpecimenId;
            Assert.That(tracked, Is.EqualTo(session.Model.GetTank("tank-b").Value.Id));
            Assert.That(tracked, Is.Not.EqualTo(session.Model.GetTank("tank-a").Value.Id));
            player.Teleport(new Vector3(0, 0, -1), Quaternion.identity);
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.Serve)); Assert.That(Target().Station, Is.SameAs(counter));
            EndDay(false);
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.PriceAndOpen)); Assert.That(Target().Station, Is.SameAs(counter));
            // A non-tutorial transaction removes this exact candidate. Reconciliation must not
            // promote the unrelated specimen to practice or demand service for a missing ID.
            Ok(session.Model.SellFromTank("tank-b")); session.Tutorial.Reconcile(session.Model);
            Assert.That(session.Tutorial.PracticeSpecimenId, Is.Null.Or.Empty);
            Assert.That(session.Model.GetTank("tank-a").HasValue, Is.True);
            Assert.That(session.TutorialGuidanceLesson, Is.EqualTo(TutorialLesson.Collect));
            player.Teleport(new Vector3(0, 0, 2), Quaternion.identity); Assert.That(Target().Station, Is.SameAs(bloom));
        }

        [Test]
        public void EarlierQualifyingReportAndLaterPracticeStillWaitForNormalSecondNight()
        {
            EnterPractice(); EndDay(false); // First sale day, report not yet acknowledged.
            Assert.That(session.ShowDayReport(), Is.True); Assert.That(session.OpenTutorialHelp(), Is.True);
            session.CloseTutorialHelp(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.DayReport));
            Assert.That(session.Tutorial.CaptureSave().reportAcknowledged, Is.False);
            session.BackFromPanel(); Assert.That(session.Tutorial.CaptureSave().reportDay, Is.EqualTo(1));
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Practice));
            OpenDay(); Produce(true, "tank-a"); Sell("tank-a");
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.CloseAndReview));
            Assert.That(session.FinishGuidedOpening(), Is.False);
            Assert.That(session.Cycle.RequestClose(), Is.True); session.Cycle.Tick(2.99f);
            Assert.That(session.Cycle.Phase, Is.EqualTo(ShopDayPhase.Closing)); Assert.That(session.FinishGuidedOpening(), Is.False);
            session.Cycle.Tick(.02f); session.Reports.FinishDay(0);
            Assert.That(session.Cycle.DayNumber, Is.EqualTo(3)); Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(session.ShowTutorialHandoff(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp));
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(session.Tutorial.CaptureSave().reportDay, Is.EqualTo(1), "A prior qualifying acknowledged report remains sufficient.");
        }

        [Test]
        public void HandoffAllowsUnaffordableUpgradeAndCompletesWithoutPurchaseOrReward()
        {
            NextPlan(); Assert.That(session.Model.Currency, Is.EqualTo(12));
            Assert.That(session.Model.Currency, Is.LessThan(session.Config.UpgradeCost));
            Assert.That(session.ShowTutorialHandoff(), Is.True);
            string game = GameState(); Assert.That(session.FinishGuidedOpening(), Is.True, session.SaveError);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Completed));
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
            Assert.That(session.TutorialJournalReturnsToHelp, Is.False); Assert.That(session.TutorialUpgradeHelpOpen, Is.False);
            Assert.That(GameState(), Is.EqualTo(game)); Assert.That(session.Model.UpgradeOwned, Is.False);
            Assert.That(session.TutorialObjectiveForDisplay, Does.Contain(session.Config.UpgradeCost.ToString()));
            Assert.That(session.TutorialObjectiveForDisplay, Does.Not.Contain("finish the opening"));
            ReadSlot(out var restored, out var loaded); Assert.That(loaded.tutorial.handoffCompleted, Is.True);
            Assert.That(loaded.tutorial.mode, Is.EqualTo(TutorialMode.Completed)); Assert.That(restored.Currency, Is.EqualTo(12));
            Assert.That(restored.UpgradeOwned, Is.False);
        }

        [Test]
        public void FailedHandoffWriteKeepsActiveModeOldBytesAndOriginalEconomyUntilRetry()
        {
            NextPlan(); Assert.That(session.ShowTutorialHandoff(), Is.True); Assert.That(session.SaveNow(), Is.True, session.SaveError);
            string before = State(); byte[] bytes = File.ReadAllBytes(path);
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(session.FinishGuidedOpening(), Is.False);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Active)); Assert.That(State(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes)); Assert.That(session.SaveError, Is.Not.Null.And.Not.Empty);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp));
            Directory.Delete(path + ".tmp", false);
            Assert.That(session.FinishGuidedOpening(), Is.True, session.SaveError);
            Assert.That(session.Model.Currency, Is.EqualTo(12)); Assert.That(session.Model.UpgradeOwned, Is.False);
            Assert.That(session.FinishGuidedOpening(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JournalLinkReturnsToNextPlanWithoutCompletingItOrChangingShop(bool closeThroughSetPanel)
        {
            NextPlan(); Assert.That(session.ShowTutorialHandoff(), Is.True);
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            string before = State(); byte[] bytes = File.ReadAllBytes(path);
            Assert.That(session.OpenTutorialJournal(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.Journal)); Assert.That(session.Paused, Is.False);
            Assert.That(session.TutorialJournalReturnsToHelp, Is.True);
            if (closeThroughSetPanel) session.SetPanel(ShopPanel.None); else session.BackFromPanel();
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp)); Assert.That(session.Paused, Is.True);
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(session.TutorialJournalReturnsToHelp, Is.False);
            Assert.That(State(), Is.EqualTo(before)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Active));
        }

        [Test]
        public void BenchReferenceSubviewDoesNotBuyUpgradeGrantProgressOrLeaveStaleNavigation()
        {
            NextPlan(); Assert.That(session.ShowTutorialHandoff(), Is.True);
            Assert.That(session.SaveNow(), Is.True, session.SaveError); string before = State(); byte[] bytes = File.ReadAllBytes(path);
            Assert.That(session.OpenTutorialUpgradeHelp(), Is.True);
            Assert.That(session.TutorialUpgradeHelpOpen, Is.True); Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp));
            Assert.That(session.Paused, Is.True); Assert.That(session.Model.UpgradeOwned, Is.False);
            session.SelectTutorialHelpLesson(TutorialLesson.NextPlan);
            Assert.That(session.TutorialUpgradeHelpOpen, Is.False);
            Assert.That(State(), Is.EqualTo(before)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(session.OpenTutorialUpgradeHelp(), Is.True);
            session.CloseTutorialHelp(); Assert.That(session.TutorialUpgradeHelpOpen, Is.False);
        }

        [Test]
        public void HandoffAndItsLinksCannotBypassCurrentLessonOrProtectedSave()
        {
            EnterPractice(); Assert.That(session.ShowTutorialHandoff(), Is.False);
            Assert.That(session.OpenTutorialJournal(), Is.False); Assert.That(session.OpenTutorialUpgradeHelp(), Is.False);
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.OpenTutorialJournal(), Is.False); Assert.That(session.OpenTutorialUpgradeHelp(), Is.False);
            session.CloseTutorialHelp(); Produce(true, "tank-a"); Sell("tank-a"); EndDay(true);
            Set("AutosaveBlocked", true); Assert.That(session.ShowTutorialHandoff(), Is.False);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Active));
        }

        [Test]
        public void OverwrittenQualifyingReportRecoversOnlyThroughExplicitSavedSaleReceiptReview()
        {
            LoseQualifyingReport();
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.CloseAndReview));
            Assert.That(session.TutorialNeedsReportRecovery, Is.True);
            Assert.That(session.TutorialReportRecoveryDay, Is.EqualTo(1));
            Assert.That(session.ShowDayReport(), Is.True); session.BackFromPanel();
            Assert.That(session.Tutorial.ReportAcknowledged, Is.False, "The later empty report cannot claim it contained either tutorial sale.");
            Assert.That(session.OpenTutorialHelp(), Is.True);
            string game = GameState(); string before = State();
            for (int i = 0; i < 10; i++)
            {
                _ = session.TutorialNeedsReportRecovery; _ = session.TutorialReportRecoveryDay;
                _ = session.Tutorial.FirstSpeciesId; _ = session.Tutorial.PracticeSpeciesId;
            }
            Assert.That(State(), Is.EqualTo(before), "Displaying historic receipts is not acknowledgement.");
            Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.True, session.SaveError);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(session.Tutorial.ReportAcknowledged, Is.True); Assert.That(session.Tutorial.CaptureSave().reportDay, Is.EqualTo(1));
            Assert.That(session.TutorialNeedsReportRecovery, Is.False); Assert.That(GameState(), Is.EqualTo(game));
            Assert.That(session.Model.Currency, Is.EqualTo(12)); Assert.That(session.Reports.LastDayNumber, Is.EqualTo(2));
            Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False);
            ReadSlot(out var loadedModel, out var loaded);
            Assert.That(loaded.tutorial.reportAcknowledged, Is.True); Assert.That(loaded.tutorial.reportDay, Is.EqualTo(1));
            Assert.That(loadedModel.Currency, Is.EqualTo(12));
        }

        [Test]
        public void SaleReceiptRecoveryCannotBypassAvailableReportWrongHelpPageTradingOrProtectedSave()
        {
            EnterPractice(); Produce(true, "tank-a"); Sell("tank-a"); EndDay(false);
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.TutorialNeedsReportRecovery, Is.False, "The ordinary qualifying report is still available.");
            Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False); session.CloseTutorialHelp();
            OpenDay(); EndDay(false);
            Assert.That(session.TutorialNeedsReportRecovery, Is.True);
            Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False, "Requires requested receipt help.");
            Assert.That(session.OpenTutorialHelp(), Is.True); session.SelectTutorialHelpLesson(TutorialLesson.NextPlan);
            Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False);
            session.SelectTutorialHelpLesson(TutorialLesson.CloseAndReview);
            Set("AutosaveBlocked", true); Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False);
            Set("AutosaveBlocked", false); session.CloseTutorialHelp(); OpenDay();
            Assert.That(session.OpenTutorialHelp(), Is.True); Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False);
            Assert.That(session.Tutorial.ReportAcknowledged, Is.False); Assert.That(session.Model.Currency, Is.EqualTo(12));
        }

        [Test]
        public void FailedReceiptAcknowledgementSavePreservesPreviousSnapshotAndReportsDirtyProgress()
        {
            LoseQualifyingReport(); Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            byte[] original = File.ReadAllBytes(path); string game = GameState();
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(session.AcknowledgeTutorialSaleReceipts(), Is.False);
            Assert.That(session.Tutorial.ReportAcknowledged, Is.True, "Ordinary in-memory learning remains honestly dirty until a save succeeds.");
            Assert.That(session.SaveError, Is.Not.Null.And.Not.Empty);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Active)); Assert.That(GameState(), Is.EqualTo(game));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            ReadSlot(out var previousModel, out var previousSlot);
            Assert.That(previousSlot.tutorial.reportAcknowledged, Is.False); Assert.That(previousModel.Currency, Is.EqualTo(12));
            Directory.Delete(path + ".tmp", false); Assert.That(session.SaveNow(), Is.True, session.SaveError);
            ReadSlot(out var savedModel, out var savedSlot);
            Assert.That(savedSlot.tutorial.reportAcknowledged, Is.True); Assert.That(savedSlot.tutorial.reportDay, Is.EqualTo(1));
            Assert.That(savedModel.Currency, Is.EqualTo(12)); Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Active));
        }

        void EnterPractice()
        {
            Assert.That(session.StartGuidedOpening(), Is.True, session.SaveError);
            session.Tutorial.ObserveOrientation(true, true, true); OpenDay(); Produce(true, "tank-a"); Sell("tank-a");
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Practice));
        }
        void NextPlan()
        {
            EnterPractice(); Produce(true, "tank-a"); Sell("tank-a"); EndDay(true);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.NextPlan));
        }
        void LoseQualifyingReport()
        {
            EnterPractice(); Produce(true, "tank-a"); Sell("tank-a"); EndDay(false);
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            // Simulate the durable authority restored before the user dismissed its report.
            ReadSlot(out var model, out var slot); Set("Model", model); Invoke("RestoreTutorial", slot.tutorial);
            Assert.That(session.Tutorial.ReportAcknowledged, Is.False); Assert.That(session.Reports.LastDayNumber, Is.EqualTo(1));
            OpenDay(); EndDay(false); // Ordinary later zero-sale report replaces the retained full report.
            Assert.That(session.Reports.LastDayNumber, Is.EqualTo(2)); Assert.That(session.Cycle.DayNumber, Is.EqualTo(3));
            Assert.That(session.Tutorial.FirstSaleDay, Is.EqualTo(1)); Assert.That(session.Tutorial.PracticeSaleDay, Is.EqualTo(1));
        }
        void RequestDirections()
        {
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.Practice));
            Assert.That(session.ShowTutorialTarget(), Is.True, session.TutorialGuidanceRecovery);
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.True);
        }
        void OpenDay()
        {
            Assert.That(session.Reports.BeginDay(session.Cycle.DayNumber, session.DisplayedStockCount()), Is.True);
            Assert.That(session.Cycle.OpenShop(), Is.True); session.Tutorial.ObserveOpened(true);
        }
        void EndDay(bool acknowledge)
        {
            Assert.That(session.Cycle.RequestClose(), Is.True); session.Cycle.Tick(ShopDayCycle.DefaultClosingSeconds);
            session.Reports.FinishDay(session.DisplayedStockCount());
            if (acknowledge) { Assert.That(session.ShowDayReport(), Is.True); session.BackFromPanel(); }
        }
        void Collect(CultureKind kind)
        {
            var result = session.Model.Collect(kind); Ok(result);
            session.Tutorial.ObserveCollection(session.Model, kind, result, TutorialActionOrigin.Player);
        }
        void Produce(bool tracked, string tank = null)
        {
            session.Model.Tick(20); // Labelled fixture preparation, not a claim of real-time production.
            Collect(CultureKind.Bloom); Collect(CultureKind.Flow);
            Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Flow));
            var result = session.Model.BeginIncubation(); Ok(result);
            if (tracked) Assert.That(session.Tutorial.ObserveStarted(session.Model, result, TutorialActionOrigin.Player), Is.True);
            session.Model.Tick(session.Config.IncubationSeconds);
            if (tracked) session.Tutorial.ObserveReady(session.Model);
            if (tank == null) return;
            result = session.Model.TransferToTank(tank); Ok(result);
            if (tracked) Assert.That(session.Tutorial.ObserveDisplayed(session.Model, result, TutorialActionOrigin.Player), Is.True);
        }
        void Sell(string tank)
        {
            int before = session.Model.Currency;
            var result = session.Model.SellFromTank(tank); Ok(result);
            session.Tutorial.ObserveSale(session.Model, result, TutorialActionOrigin.Player, session.Cycle.DayNumber);
            string visit = Guid.NewGuid().ToString("N"); Assert.That(session.Reports.BeginVisit(visit), Is.True);
            Assert.That(session.Reports.ResolveVisit(visit, CustomerOutcome.Sold, session.Model.Currency - before), Is.True);
        }
        void AddQueuedVisitor(string tank)
        {
            var visit = new CustomerVisit(session.Model, new[] { tank });
            Assert.That(session.Reports.BeginVisit(visit.Id), Is.True);
            visit.Advance(.1f, true, ShopDayPhase.Open); visit.Advance(3, false, ShopDayPhase.Open);
            visit.Advance(.1f, true, ShopDayPhase.Open); Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
            var type = typeof(ShopCustomers).GetNestedType("Visitor", BindingFlags.NonPublic);
            var entry = Activator.CreateInstance(type); type.GetField("Visit").SetValue(entry, visit);
            foreach (string field in new[] { "visitors", "line" })
                ((IList)typeof(ShopCustomers).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(customers)).Add(entry);
        }
        TutorialGuidanceTarget Target()
        {
            Assert.That(session.TryGetTutorialGuidance(out var target), Is.True, session.TutorialGuidanceRecovery); return target;
        }
        void ReadSlot(out GameModel model, out SlotData slot) => Assert.That(SaveStore.Read(path, session.Config, out model, out slot, out var error), Is.True, error);
        string GameState() => JsonUtility.ToJson(session.Model.CaptureSave());
        string State() => GameState() + JsonUtility.ToJson(session.Cycle.CaptureSave()) + JsonUtility.ToJson(session.Tutorial.CaptureSave());
        GameObject Child(string name) { var item = new GameObject(name); item.transform.SetParent(scene.transform); return item; }
        Station StationFixture(StationKind kind, Vector3 position, string facility = null, string tank = null)
        {
            var station = Child(kind.ToString()).AddComponent<Station>(); station.Kind = kind; station.Label = kind.ToString();
            station.FacilityId = facility; station.TankId = tank; station.transform.position = position; stations.Add(station); return station;
        }
        void Set(string property, object value) => typeof(ShopSession).GetProperty(property).SetValue(session, value);
        void Invoke(string method, object argument) => typeof(ShopSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new[] { argument });
        static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
