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
    public sealed class TutorialRecoveryAuditTests
    {
        const string Prefix = "EvolutionShop-TutorialRecoveryAudit-";
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
            owner = new GameObject("Inactive tutorial recovery audit authority"); owner.SetActive(false);
            scene = new GameObject("Isolated tutorial recovery audit stations");
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


        [TestCase(ShopPanel.Incubator)]
        [TestCase(ShopPanel.Counter)]
        [TestCase(ShopPanel.Staff)]
        [TestCase(ShopPanel.StockerTargets)]
        [TestCase(ShopPanel.TechnicianOrders)]
        [TestCase(ShopPanel.Tank)]
        public void HelpDoesNotReturnToDisabledStation(ShopPanel origin)
        {
            Station station = origin == ShopPanel.Incubator ? incubator : origin == ShopPanel.Tank ? tankA : counter;
            player.Teleport(station.transform.position + Vector3.forward, Quaternion.identity);
            session.SetPanel(origin); Assert.That(session.OpenTutorialHelp(), Is.True);
            string original = State(); station.enabled = false;
            session.BackFromPanel();
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.None), "A disabled station is not a valid return destination.");
            Assert.That(State(), Is.EqualTo(original));
            Assert.That(session.Paused, Is.False); Assert.That(player.InputEnabled, Is.True);
        }

        [TestCase("inactive")]
        [TestCase("moved")]
        [TestCase("destroyed")]
        public void HelpRechecksLiveStationBeforeReturning(string change)
        {
            player.Teleport(incubator.transform.position + Vector3.forward, Quaternion.identity);
            session.SetPanel(ShopPanel.Incubator); Assert.That(session.OpenTutorialHelp(), Is.True);
            if (change == "inactive") incubator.gameObject.SetActive(false);
            if (change == "moved") incubator.transform.position += Vector3.right * 30;
            if (change == "destroyed") UnityEngine.Object.DestroyImmediate(incubator.gameObject);
            session.BackFromPanel(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
        }

        [Test]
        public void NestedHelpFocusLossAndAdvanceKeepProductionPausedThenReturnToLiveStation()
        {
            Assert.That(session.StartGuidedOpening(), Is.True);
            session.Tutorial.ObserveOrientation(true, true, true);
            Collect(CultureKind.Bloom); Collect(CultureKind.Flow);
            Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Flow));
            var start = session.Model.BeginIncubation(); Ok(start);
            session.Tutorial.ObserveStarted(session.Model, start, TutorialActionOrigin.Player);
            player.Teleport(incubator.transform.position + Vector3.forward, Quaternion.identity);
            session.SetPanel(ShopPanel.Incubator); Assert.That(session.OpenTutorialHelp(), Is.True);
            Field(session, "initialized", true);
            string before = State(); float seconds = session.Model.Incubator.SecondsRemaining;
            Invoke("OnApplicationFocus", false); session.Advance(120);
            Assert.That(session.OpenTutorialHelp(), Is.True); session.Advance(120);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp)); Assert.That(session.Paused, Is.True);
            Assert.That(State(), Is.EqualTo(before)); Assert.That(session.Model.Incubator.SecondsRemaining, Is.EqualTo(seconds));
            session.BackFromPanel(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.Incubator)); Assert.That(session.Paused, Is.False);
        }

        [TestCase(ShopPanel.Pause)]
        [TestCase(ShopPanel.Reset)]
        [TestCase(ShopPanel.Incubator)]
        [TestCase(ShopPanel.Arrange)]
        public void LinkedJournalInterruptionDiscardsOnlyTransientReturnLink(ShopPanel interruption)
        {
            NextPlan(); Assert.That(session.ShowTutorialHandoff(), Is.True);
            Assert.That(session.OpenTutorialUpgradeHelp(), Is.True); Assert.That(session.OpenTutorialJournal(), Is.True);
            string before = State(); byte[] disk = File.ReadAllBytes(path);
            Assert.That(session.TutorialUpgradeHelpOpen, Is.False);
            session.SetPanel(interruption); Assert.That(session.TutorialJournalReturnsToHelp, Is.False);
            session.SetPanel(ShopPanel.None); Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
            Assert.That(session.OpenTutorialHelp(), Is.True); Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.NextPlan));
            session.BackFromPanel(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
            Assert.That(State(), Is.EqualTo(before)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(disk));
        }

        [Test]
        public void LinkedJournalFocusLossReturnsToPauseWithoutResurrectingOldCounterLink()
        {
            NextPlan(); player.Teleport(counter.transform.position + Vector3.forward, Quaternion.identity);
            session.SetPanel(ShopPanel.Counter); Assert.That(session.ShowTutorialHandoff(), Is.True);
            Assert.That(session.OpenTutorialJournal(), Is.True); Field(session, "initialized", true);
            string before = State(); Invoke("OnApplicationFocus", false);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.Pause)); Assert.That(session.Paused, Is.True);
            Assert.That(session.TutorialJournalReturnsToHelp, Is.False); Assert.That(player.InputEnabled, Is.False);
            session.Advance(30); Assert.That(State(), Is.EqualTo(before));
            session.BackFromPanel(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
            Assert.That(session.OpenTutorialHelp(), Is.True); session.CloseTutorialHelp();
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.None)); Assert.That(State(), Is.EqualTo(before));
        }

        [Test]
        public void LinkedJournalF1ReturnsToExistingHandoffThenOriginalCounter()
        {
            NextPlan(); player.Teleport(counter.transform.position + Vector3.forward, Quaternion.identity);
            session.SetPanel(ShopPanel.Counter); Assert.That(session.ShowTutorialHandoff(), Is.True);
            Assert.That(session.OpenTutorialJournal(), Is.True); string before = State();
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp)); Assert.That(session.Paused, Is.True);
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(session.TutorialJournalReturnsToHelp, Is.False);
            session.CloseTutorialHelp(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.Counter));
            Assert.That(State(), Is.EqualTo(before));
        }

        [Test]
        public void PlacementRejectsHelpThenCancellationKeepsLessonAndOwnership()
        {
            EnterPractice(); string before = State(); byte[] disk = File.ReadAllBytes(path);
            // Only the presentation interruption is under test; no placement purchase is simulated.
            Set("PlacementActive", true);
            Assert.That(session.OpenTutorialHelp(), Is.False); Assert.That(session.CanShowTutorialTarget, Is.False);
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            session.CancelPlacement(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.Arrange));
            Assert.That(session.PlacementActive, Is.False); Assert.That(State(), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(disk));
            session.SetPanel(ShopPanel.None); Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True); Assert.That(session.TutorialPracticeGuidanceRequested, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyDisplayGuidanceExcludesStockerClaimAndPreservesItsExactOwner(bool practice)
        {
            StorageFixture(practice); Produce(false); Ok(session.Model.StoreReadyOutput("rack-a"));
            string stored = session.Model.GetStorage("rack-a").Value.Id;
            Ok(session.Model.ReserveStockTransfer("job-a", "nori", "rack-a", "tank-a", stored));
            Produce(true); EnableDirectionsIfPractice(practice); player.Teleport(new Vector3(0, 0, -2), Quaternion.identity);
            string before = State(); string candidate = session.Model.Incubator.SpecimenId;
            Assert.That(Target().Station, Is.SameAs(tankB), "The other real empty tank is immediately usable by the player.");
            Assert.That(session.Model.TransferToTank("tank-a").Failure, Is.EqualTo(FailureCode.StockReserved));
            AssertClaim("job-a", "rack-a", "tank-a", stored);
            Assert.That(session.Model.Incubator.SpecimenId, Is.EqualTo(candidate)); Assert.That(State(), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllEmptyDisplaysReservedOfferStockerRecoveryWithoutStealingClaims(bool practice)
        {
            StorageFixture(practice); Produce(false); Ok(session.Model.StoreReadyOutput("rack-a"));
            Produce(false); Ok(session.Model.StoreReadyOutput("rack-b"));
            string a = session.Model.GetStorage("rack-a").Value.Id, b = session.Model.GetStorage("rack-b").Value.Id;
            Ok(session.Model.ReserveStockTransfer("job-a", "nori", "rack-a", "tank-a", a));
            Ok(session.Model.ReserveStockTransfer("job-b", "nori", "rack-b", "tank-b", b));
            Produce(true); EnableDirectionsIfPractice(practice); player.Teleport(new Vector3(0, 0, -2), Quaternion.identity);
            string before = State();
            Assert.That(Target().Station, Is.SameAs(counter), "A player cannot transfer into either reserved destination.");
            string recovery = (session.TutorialGuidanceRecovery ?? "") + " " + session.TutorialObjectiveForDisplay;
            Assert.That(recovery.ToLowerInvariant(), Does.Contain("stock"));
            Assert.That(recovery.ToLowerInvariant(), Does.Contain("pause").Or.Contain("wait"));
            AssertClaim("job-a", "rack-a", "tank-a", a); AssertClaim("job-b", "rack-b", "tank-b", b);
            Assert.That(State(), Is.EqualTo(before));
        }

        [TestCase("stocker")]
        [TestCase("technician")]
        public void ReservedExactTutorialOutputPointsToMatchingWorkerRecovery(string worker)
        {
            StorageFixture(false); Produce(true); string candidate = session.Tutorial.FirstSpecimenId;
            if (worker == "stocker")
            {
                Ok(session.Model.StoreReadyOutput("rack-a"));
                Ok(session.Model.ReserveStockTransfer("job-a", "nori", "rack-a", "tank-a", candidate));
            }
            else Ok(session.Model.ReserveTechnicianOutput("job-moss", "moss", "rack-a", candidate));
            player.Teleport(new Vector3(0, 0, -2), Quaternion.identity); string before = State();
            Assert.That(Target().Station, Is.SameAs(counter));
            string text = (session.TutorialGuidanceRecovery ?? "") + " " + session.TutorialObjectiveForDisplay;
            Assert.That(text.ToLowerInvariant(), Does.Contain("pause").Or.Contain("wait"));
            Assert.That(session.Tutorial.FirstSpecimenId, Is.EqualTo(candidate));
            Assert.That(session.Tutorial.CaptureSave().first.manuallyDisplayed, Is.False);
            if (worker == "stocker") AssertClaim("job-a", "rack-a", "tank-a", candidate);
            else Assert.That(session.Model.TechnicianClaim.Value.SpecimenId, Is.EqualTo(candidate));
            Assert.That(State(), Is.EqualTo(before));
        }

        [Test]
        public void MovedFirstManualDisplayMustBeRestockedEvenWithUnrelatedSaleableStock()
        {
            StorageFixture(false); Produce(true, "tank-a"); string candidate = session.Tutorial.FirstSpecimenId;
            Ok(session.Model.ReturnTankToStorage("tank-a", "rack-a", candidate));
            Produce(false, "tank-b"); OpenDay(); player.Teleport(new Vector3(0, 0, -2), Quaternion.identity);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Serve)); string before = State();
            Assert.That(Target().Station, Is.SameAs(tankA), "Counter waiting cannot sell the required creature still in the rack.");
            Assert.That(session.TutorialObjectiveForDisplay.ToLowerInvariant(), Does.Contain("stock"));
            Assert.That(session.Model.GetStorage("rack-a").Value.Id, Is.EqualTo(candidate));
            Assert.That(session.Tutorial.FirstSaleDay, Is.Zero); Assert.That(State(), Is.EqualTo(before));
        }

        [Test]
        public void FullDisplaysBeforeFirstOpeningOfferSalesInsteadOfAnExtraDisplayPurchase()
        {
            StorageFixture(false); Produce(true, "tank-a"); string candidate = session.Tutorial.FirstSpecimenId;
            Ok(session.Model.ReturnTankToStorage("tank-a", "rack-a", candidate));
            Produce(false, "tank-a"); Produce(false, "tank-b");
            player.Teleport(new Vector3(0, 0, -2), Quaternion.identity);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.PriceAndOpen));
            Assert.That(session.Cycle.Phase, Is.EqualTo(ShopDayPhase.Night));
            string before = State();
            Assert.That(Target().Station, Is.SameAs(counter), "Existing unrelated stock can be sold to free a tank; another purchase is unnecessary.");
            Assert.That(session.TutorialObjectiveForDisplay.ToLowerInvariant(), Does.Contain("open"));
            Assert.That(session.TutorialObjectiveForDisplay.ToLowerInvariant(), Does.Not.Contain("arrange"));
            Assert.That(session.Model.GetStorage("rack-a").Value.Id, Is.EqualTo(candidate));
            Assert.That(State(), Is.EqualTo(before));
            OpenDay(); Sell("tank-a");
            Assert.That(session.Tutorial.FirstSaleDay, Is.Zero, "An unrelated sale frees space without substituting for the exact manual proof.");
            Assert.That(session.Model.Currency, Is.EqualTo(6));
            Assert.That(Target().Station, Is.SameAs(tankA));
            Assert.That(session.Model.GetStorage("rack-a").Value.Id, Is.EqualTo(candidate));
        }

        [TestCase(TutorialLesson.Orient)]
        [TestCase(TutorialLesson.Collect)]
        [TestCase(TutorialLesson.Prepare)]
        [TestCase(TutorialLesson.Grow)]
        [TestCase(TutorialLesson.Display)]
        [TestCase(TutorialLesson.PriceAndOpen)]
        [TestCase(TutorialLesson.Serve)]
        [TestCase(TutorialLesson.Practice)]
        [TestCase(TutorialLesson.CloseAndReview)]
        [TestCase(TutorialLesson.NextPlan)]
        public void SessionRestoreAtEachLessonDropsPresentationStateWithoutRepeatingEconomy(TutorialLesson lesson)
        {
            ReachLesson(lesson); Assert.That(session.ActiveTutorialLesson, Is.EqualTo(lesson));
            Assert.That(session.OpenTutorialHelp(), Is.True);
            if (lesson == TutorialLesson.Practice) Assert.That(session.ShowTutorialTarget(), Is.True);
            if (lesson == TutorialLesson.NextPlan) Assert.That(session.OpenTutorialJournal(), Is.True);
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            string before = State(); byte[] disk = File.ReadAllBytes(path);
            string wages = JsonUtility.ToJson(session.Staff.CaptureSave());
            ReadSlot(out var model, out var slot); Set("Model", model);
            Assert.That(ShopDayCycle.TryRestore(slot.cycle, out var cycle), Is.True); Set("Cycle", cycle);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var reports), Is.True); Set("Reports", reports);
            Invoke("RestoreTutorial", slot.tutorial); session.SetPanel(ShopPanel.Welcome);
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.Orient));
            Assert.That(session.TutorialPracticeGuidanceRequested, Is.False); Assert.That(session.TutorialJournalReturnsToHelp, Is.False);
            Assert.That(session.TutorialUpgradeHelpOpen, Is.False);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(lesson));
            Assert.That(session.ContinueGuidedOpening(), Is.True); Assert.That(session.Panel, Is.EqualTo(ShopPanel.None));
            Assert.That(State(), Is.EqualTo(before)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(disk));
            Assert.That(JsonUtility.ToJson(session.Staff.CaptureSave()), Is.EqualTo(wages));
        }

        void ReachLesson(TutorialLesson lesson)
        {
            Assert.That(session.StartGuidedOpening(), Is.True); if (lesson == TutorialLesson.Orient) return;
            session.Tutorial.ObserveOrientation(true, true, true); if (lesson == TutorialLesson.Collect) return;
            Collect(CultureKind.Bloom); Collect(CultureKind.Flow); if (lesson == TutorialLesson.Prepare) return;
            Ok(session.Model.Dose(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Flow));
            var started = session.Model.BeginIncubation(); Ok(started);
            session.Tutorial.ObserveStarted(session.Model, started, TutorialActionOrigin.Player);
            if (lesson == TutorialLesson.Grow) return;
            session.Model.Tick(session.Config.IncubationSeconds); session.Tutorial.ObserveReady(session.Model);
            if (lesson == TutorialLesson.Display) return;
            var displayed = session.Model.TransferToTank("tank-a"); Ok(displayed);
            session.Tutorial.ObserveDisplayed(session.Model, displayed, TutorialActionOrigin.Player);
            if (lesson == TutorialLesson.PriceAndOpen) return;
            OpenDay(); if (lesson == TutorialLesson.Serve) return;
            Sell("tank-a"); if (lesson == TutorialLesson.Practice) return;
            Produce(true, "tank-a"); Sell("tank-a"); if (lesson == TutorialLesson.CloseAndReview) return;
            EndDay(true);
        }

        void StorageFixture(bool practice)
        {
            // Directly configured storage isolates guidance from construction prices/layout.
            // No SaveStore assertion is made for this geometry-only configuration.
            session.Config.StorageIds = new[] { "rack-a", "rack-b" }; Set("Model", new GameModel(session.Config));
            Assert.That(session.Tutorial.Start(), Is.True); session.Tutorial.ObserveOrientation(true, true, true);
            if (!practice) return;
            OpenDay(); Produce(true, "tank-a"); Sell("tank-a");
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Practice));
            session.Tutorial.MarkPracticeAssisted(); // Assistance receipt preexists; no malformed-layout full-slot write.
        }
        void EnableDirectionsIfPractice(bool practice)
        {
            if (!practice) return;
            Assert.That(session.OpenTutorialHelp(), Is.True); Assert.That(session.ShowTutorialTarget(), Is.True);
        }
        void AssertClaim(string job, string storage, string tank, string specimen)
        {
            var claim = session.Model.GetStockTransfer(job); Assert.That(claim.HasValue, Is.True);
            Assert.That(claim.Value.OwnerId, Is.EqualTo("nori")); Assert.That(claim.Value.StorageId, Is.EqualTo(storage));
            Assert.That(claim.Value.TankId, Is.EqualTo(tank)); Assert.That(claim.Value.SpecimenId, Is.EqualTo(specimen));
            Assert.That(session.Model.GetStorage(storage).Value.Id, Is.EqualTo(specimen));
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
