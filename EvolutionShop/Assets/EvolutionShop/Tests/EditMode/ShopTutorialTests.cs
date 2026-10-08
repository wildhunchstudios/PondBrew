using System;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class ShopTutorialTests
    {
        GameModel game;
        ShopDayCycle cycle;
        ShopTutorial tutorial;
        const TutorialActionOrigin Player = TutorialActionOrigin.Player;
        const TutorialActionOrigin Worker = TutorialActionOrigin.Worker;
        [SetUp] public void Setup()
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "rack-a" };
            game = new GameModel(config); cycle = new ShopDayCycle(); tutorial = new ShopTutorial();
        }

        [Test] public void FreshOfferAndLegacyNeverInferLearningOrGrantGameplay()
        {
            Assert.That(tutorial.Mode, Is.EqualTo(TutorialMode.Offered));
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.None));
            Assert.That(tutorial.FirstSaleAccommodationActive, Is.False);
            Assert.That(tutorial.ObserveOrientation(true, true, true), Is.False);
            Assert.That(ShopTutorial.LegacyUnguided().Start(), Is.False);
            Assert.That(tutorial.Start(), Is.True); Assert.That(tutorial.Start(), Is.False);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Orient));
            Assert.That(tutorial.FirstSaleAccommodationActive, Is.True);
            Assert.That(game.Currency, Is.Zero); Assert.That(game.ReservedCultureCount, Is.Zero);
        }

        [Test] public void TenLessonsRequireActualManualLoopsAndNightReportHandoff()
        {
            tutorial.Start(); Assert.That(tutorial.Finish(cycle), Is.False);
            tutorial.ObserveOrientation(true, false, true);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Orient));
            tutorial.ObserveOrientation(false, true, false);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Collect));
            Collect(CultureKind.Flow); Collect(CultureKind.Bloom);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Prepare));
            game.Dose(CultureKind.Bloom); game.Dose(CultureKind.Flow);
            Assert.That(tutorial.ObservePrepared(game, "pondlet", Player), Is.True);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Grow));
            StartStaged(); game.Tick(10); tutorial.ObserveReady(game);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Display));
            Display("tank-a"); Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.PriceAndOpen));
            tutorial.ObservePriceViewed(); Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.PriceAndOpen));
            tutorial.ObserveOpened(cycle.OpenShop()); Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Serve));
            Sale("tank-a"); Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Practice));
            Produce(); Display("tank-a"); Sale("tank-a");
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.CloseAndReview));
            Assert.That(tutorial.Finish(cycle), Is.False);
            cycle.RequestClose(); cycle.Tick(3);
            Assert.That(tutorial.Finish(cycle), Is.False);
            Assert.That(tutorial.ObserveReportAcknowledged(1), Is.True);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(tutorial.Finish(cycle), Is.True); Assert.That(tutorial.Finish(cycle), Is.False);
            Assert.That(tutorial.Mode, Is.EqualTo(TutorialMode.Completed));
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.None));
            Assert.That(game.Currency, Is.EqualTo(12)); Assert.That(tutorial.PracticeAssisted, Is.False);
            AssertRoundTrip();
        }

        [Test] public void AlternateRecipeAndEarlySaleSatisfyConceptsWithoutPriceCardOrRestocking()
        {
            tutorial.Start(); tutorial.ObserveOrientation(true, true, true);
            Produce("bubblebelly"); Display("tank-a"); cycle.OpenShop(); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().first.speciesId, Is.EqualTo("bubblebelly"));
            Assert.That(tutorial.Facts.HasFlag(TutorialFacts.PriceViewed), Is.False);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Practice));
            Assert.That(game.Currency, Is.EqualTo(10)); AssertRoundTrip();
        }

        [Test] public void ExistingCarriedAndStagedCulturesReconcileInEitherOrder()
        {
            game.Collect(CultureKind.Flow); game.Dose(CultureKind.Flow); game.Collect(CultureKind.Bloom);
            tutorial.Start(); tutorial.ObserveOrientation(true, true, true);
            Assert.That(tutorial.Reconcile(game), Is.True); Assert.That(tutorial.Reconcile(game), Is.False);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Prepare));
        }

        [Test] public void FailedActionsAndUnknownOriginsCannotAdvance()
        {
            tutorial.Start(); var failure = game.BeginIncubation(); Assert.That(failure.Success, Is.False);
            Assert.That(tutorial.ObserveCollection(game, CultureKind.Bloom, failure, Player), Is.False);
            Assert.That(tutorial.ObservePrepared(game, "pondlet", Player), Is.False);
            Assert.That(tutorial.ObserveStarted(game, failure, Player), Is.False);
            Assert.That(tutorial.ObserveDisplayed(game, failure, Player), Is.False);
            Assert.That(tutorial.ObserveSale(game, failure, Player, 1), Is.False);
            Assert.That(tutorial.ObserveOpened(false), Is.False);
            Assert.That(tutorial.ObserveReady(game), Is.False);
            Assert.That(tutorial.ObserveReportAcknowledged(1), Is.False);
            Assert.That(tutorial.MarkPracticeAssisted(), Is.False);
            Produce(); var displayed = game.TransferToTank("tank-a");
            Assert.That(tutorial.ObserveDisplayed(game, displayed, (TutorialActionOrigin)9), Is.False);
            var sold = game.SellFromTank("tank-a");
            Assert.That(tutorial.ObserveSale(game, sold, (TutorialActionOrigin)9, 1), Is.False);
            Assert.That(tutorial.ObserveSale(game, sold, Player, 0), Is.False);
            Assert.That(tutorial.FirstSaleAccommodationActive, Is.True);
        }

        [Test] public void DuplicateEventsAndOldFirstSaleCannotBecomeSecondProof()
        {
            Begin(); var started = Produce();
            Assert.That(tutorial.ObserveStarted(game, started, Player), Is.False);
            var displayed = Display("tank-a"); Assert.That(tutorial.ObserveDisplayed(game, displayed, Player), Is.False);
            cycle.OpenShop(); var sold = Sale("tank-a");
            Assert.That(tutorial.ObserveSale(game, sold, Player, 1), Is.False);
            Produce(); Display("tank-a");
            Assert.That(tutorial.ObserveSale(game, sold, Player, 2), Is.False);
            Assert.That(tutorial.CaptureSave().practice.saleDay, Is.Zero);
            Assert.That(game.Currency, Is.EqualTo(6)); AssertRoundTrip();
        }

        [Test] public void BatchStartedBeforeFirstSaleCannotQualifyByReplayedNotification()
        {
            Begin(); Produce(); Display("tank-a");
            var early = StartBatch(); // already running when the first sale occurs
            cycle.OpenShop(); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().practiceBlockedSpecimenId, Is.EqualTo(early.SpecimenId));
            Assert.That(tutorial.ObserveStarted(game, early, Player), Is.False);
            AssertRoundTrip();
            game.Tick(10); tutorial.ObserveReady(game); Display("tank-a"); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().practice.saleDay, Is.Zero);
            Produce(); Assert.That(tutorial.CaptureSave().practiceBlockedSpecimenId, Is.Null.Or.Empty);
            Display("tank-a"); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().practice.saleDay, Is.EqualTo(1));
            Assert.That(game.Currency, Is.EqualTo(18)); AssertRoundTrip();
        }

        [Test] public void PreexistingDisplayStockNeverBecomesPracticeButNormalSaleIsKept()
        {
            Begin(); Produce(); Display("tank-a"); Produce(); Display("tank-b");
            cycle.OpenShop(); Sale("tank-a"); Sale("tank-b");
            Assert.That(tutorial.CaptureSave().practice.saleDay, Is.Zero);
            Assert.That(game.Currency, Is.EqualTo(12));
            Produce(); Display("tank-a"); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().practice.saleDay, Is.EqualTo(1));
        }

        [TestCase("start")] [TestCase("display")] [TestCase("sale")]
        public void WorkerAssistanceEndsGraceOnSaleButNeverProvesManualLoop(string intervention)
        {
            Begin(); Produce("pondlet", intervention == "start" ? Worker : Player);
            Display("tank-a", intervention == "display" ? Worker : Player);
            cycle.OpenShop(); Sale("tank-a", intervention == "sale" ? Worker : Player);
            Assert.That(tutorial.FirstSaleAccommodationActive, Is.False);
            Assert.That(tutorial.CaptureSave().first.saleDay, Is.Zero);
            Assert.That(tutorial.CaptureSave().first.specimenId, Is.Null.Or.Empty);
            Produce(); Display("tank-a"); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().first.saleDay, Is.EqualTo(1)); AssertRoundTrip();
        }

        [Test] public void ConsumedUnobservedCandidateRetiresWithoutErasingLearnedFacts()
        {
            Begin(); Produce(); Display("tank-a"); game.SellFromTank("tank-a");
            Assert.That(tutorial.Reconcile(game), Is.True);
            Assert.That(tutorial.CaptureSave().first.specimenId, Is.Null.Or.Empty);
            Assert.That(tutorial.Facts.HasFlag(TutorialFacts.CreatureDisplayed), Is.True);
            Assert.That(tutorial.CaptureSave().first.saleDay, Is.Zero);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.Grow));
            Produce(); Display("tank-a"); cycle.OpenShop(); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().first.saleDay, Is.EqualTo(1));
        }

        [Test] public void ReadyCreatureCanTravelThroughRackAndMovedDisplayWithExactIdentity()
        {
            Begin(); var started = Produce(); game.StoreReadyOutput("rack-a"); tutorial.Reconcile(game); AssertRoundTrip();
            var display = game.TransferStorageToTank("rack-a", "tank-b", started.SpecimenId);
            Assert.That(tutorial.ObserveDisplayed(game, display, Player), Is.True);
            game.ReturnTankToStorage("tank-b", "rack-a", started.SpecimenId); tutorial.Reconcile(game); AssertRoundTrip();
            display = game.TransferStorageToTank("rack-a", "tank-a", started.SpecimenId);
            tutorial.ObserveDisplayed(game, display, Player); cycle.OpenShop(); Sale("tank-a");
            Assert.That(tutorial.CaptureSave().first.specimenId, Is.EqualTo(started.SpecimenId));
        }

        [Test] public void AssistancePersistsButDoesNotStopCompletionAndEarlyReportCanCountAcrossDays()
        {
            Begin(); Produce(); Display("tank-a"); cycle.OpenShop(); Sale("tank-a");
            cycle.RequestClose(); cycle.Tick(3); Assert.That(tutorial.ObserveReportAcknowledged(1), Is.True);
            Assert.That(tutorial.MarkPracticeAssisted(), Is.True); Assert.That(tutorial.MarkPracticeAssisted(), Is.False);
            AssertRoundTrip(); Produce(); Display("tank-a"); cycle.OpenShop(); Sale("tank-a");
            Assert.That(tutorial.Finish(cycle), Is.False); cycle.RequestClose();
            Assert.That(tutorial.Finish(cycle), Is.False); cycle.Tick(3);
            Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.NextPlan));
            Assert.That(tutorial.Finish(cycle), Is.True); Assert.That(tutorial.PracticeAssisted, Is.True); AssertRoundTrip();
        }

        [TestCase(false)] [TestCase(true)]
        public void SkipPreservesEconomyAndReceiptsButRemovesLiveChainDependency(bool afterFirstSale)
        {
            Begin(); Produce(); Display("tank-a");
            if (afterFirstSale) { cycle.OpenShop(); Sale("tank-a"); Produce(); Display("tank-a"); }
            int cash = game.Currency; string resident = game.GetTank("tank-a").Value.Id;
            Assert.That(tutorial.Skip(), Is.True); Assert.That(tutorial.Skip(), Is.False); Assert.That(tutorial.Start(), Is.False);
            Assert.That(tutorial.FirstSaleAccommodationActive, Is.False); Assert.That(tutorial.CurrentLesson(cycle), Is.EqualTo(TutorialLesson.None));
            Assert.That(game.Currency, Is.EqualTo(cash)); Assert.That(game.GetTank("tank-a").Value.Id, Is.EqualTo(resident));
            var result = game.SellFromTank("tank-a"); Assert.That(tutorial.ObserveSale(game, result, Player, 1), Is.False);
            Assert.That(tutorial.CaptureSave().practice.specimenId, Is.Null.Or.Empty);
            Assert.That(tutorial.CaptureSave().first.saleDay, Is.EqualTo(afterFirstSale ? 1 : 0)); AssertRoundTrip();
        }

        [Test] public void CaptureAndRestoreDoNotShareMutableDtos()
        {
            Begin(); Produce(); var captured = tutorial.CaptureSave(); captured.first.specimenId = "tampered"; captured.facts = 0;
            Assert.That(tutorial.CaptureSave().first.specimenId, Is.Not.EqualTo("tampered"));
            captured = tutorial.CaptureSave(); Assert.That(ShopTutorial.TryRestore(captured, game, 1, out var restored), Is.True);
            captured.first.speciesId = "wrong"; Assert.That(restored.CaptureSave().first.speciesId, Is.EqualTo("pondlet"));
        }

        [TestCase("version")] [TestCase("mode")] [TestCase("flags")] [TestCase("chain-null")] [TestCase("missing-species")]
        [TestCase("wrong-species")] [TestCase("missing-resident")] [TestCase("future-sale")] [TestCase("display-without-ready")]
        [TestCase("premature-practice")] [TestCase("assistance-before-first")] [TestCase("false-report")] [TestCase("false-handoff")]
        [TestCase("empty-chain-flags")] [TestCase("oversize-id")] [TestCase("offered-with-facts")]
        public void InvalidStateFailsRestoreWithoutPublishingPartialAuthority(string problem)
        {
            Begin(); Produce(); var data = tutorial.CaptureSave();
            switch (problem)
            {
                case "version": data.version = 2; break;
                case "mode": data.mode = (TutorialMode)999; break;
                case "flags": data.facts = (TutorialFacts)1024; break;
                case "chain-null": data.first = null; break;
                case "missing-species": data.first.speciesId = null; break;
                case "wrong-species": data.first.speciesId = "bubblebelly"; break;
                case "missing-resident": data.first.specimenId = "absent"; break;
                case "future-sale": data.first.saleDay = 2; break;
                case "display-without-ready": data.first.ready = false; data.first.manuallyDisplayed = true; break;
                case "premature-practice": data.practice = data.first; data.first = new TutorialChainSave(); break;
                case "assistance-before-first": data.practiceAssisted = true; break;
                case "false-report": data.reportAcknowledged = true; data.reportDay = 1; break;
                case "false-handoff": data.handoffCompleted = true; break;
                case "empty-chain-flags": data.practice.ready = true; break;
                case "oversize-id": data.first.specimenId = new string('x', 129); break;
                case "offered-with-facts": data.mode = TutorialMode.Offered; break;
            }
            Assert.That(ShopTutorial.TryRestore(data, game, 1, out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        [Test] public void EveryLessonRoundTripsAndResumeDoesNotRepeatEconomicActions()
        {
            tutorial.Start(); AssertRoundTrip(); tutorial.ObserveOrientation(true, true, true); AssertRoundTrip();
            Collect(CultureKind.Bloom); Collect(CultureKind.Flow); AssertRoundTrip();
            game.Dose(CultureKind.Bloom); game.Dose(CultureKind.Flow); tutorial.ObservePrepared(game, "pondlet", Player); AssertRoundTrip();
            StartStaged(); AssertRoundTrip(); game.Tick(10); tutorial.ObserveReady(game); AssertRoundTrip();
            Display("tank-a"); AssertRoundTrip(); tutorial.ObserveOpened(cycle.OpenShop()); AssertRoundTrip();
            Sale("tank-a"); AssertRoundTrip(); Produce(); Display("tank-a"); Sale("tank-a"); AssertRoundTrip();
            cycle.RequestClose(); cycle.Tick(3); tutorial.ObserveReportAcknowledged(1); AssertRoundTrip();
            tutorial.Finish(cycle); AssertRoundTrip(); Assert.That(game.Currency, Is.EqualTo(12));
        }

        [Test] public void LiveIncubatingCandidateCannotClaimReadinessOrPriorDisplay()
        {
            Begin(); StartBatch(); var saved = tutorial.CaptureSave();
            saved.first.ready = true; saved.facts |= TutorialFacts.CreatureReady;
            Assert.That(ShopTutorial.TryRestore(saved, game, 1, out _), Is.False);
            saved.first.manuallyDisplayed = true; saved.facts |= TutorialFacts.CreatureDisplayed;
            Assert.That(ShopTutorial.TryRestore(saved, game, 1, out _), Is.False);
        }

        [Test] public void CompletedSnapshotRequiresSameLearnedPrerequisitesAsFinish()
        {
            Begin(); Produce(); Display("tank-a"); cycle.OpenShop(); Sale("tank-a");
            Produce(); Display("tank-a"); Sale("tank-a"); cycle.RequestClose(); cycle.Tick(3);
            tutorial.ObserveReportAcknowledged(1); Assert.That(tutorial.Finish(cycle), Is.True); AssertRoundTrip();
            var saved = tutorial.CaptureSave(); saved.facts &= ~TutorialFacts.BloomCollected;
            Assert.That(ShopTutorial.TryRestore(saved, game, 2, out _), Is.False);
        }

        [Test] public void ReadyIncubatorCandidateCannotClaimPriorManualDisplay()
        {
            Begin(); Produce(); var saved = tutorial.CaptureSave();
            saved.first.manuallyDisplayed = true; saved.facts |= TutorialFacts.CreatureDisplayed;
            Assert.That(ShopTutorial.TryRestore(saved, game, 1, out _), Is.False);
        }

        [TestCase(TutorialFacts.CreatureReady)] [TestCase(TutorialFacts.CreatureDisplayed)]
        public void RetiredConceptsStillRequireTheirCausalLearningFacts(TutorialFacts facts)
        {
            var saved = new TutorialSave { mode = TutorialMode.Active, facts = facts };
            Assert.That(ShopTutorial.TryRestore(saved, game, 1, out _), Is.False);
        }

        void Begin() { tutorial.Start(); tutorial.ObserveOrientation(true, true, true); }
        void Collect(CultureKind kind)
        { var result = game.Collect(kind); Assert.That(result.Success, Is.True, result.Failure.ToString()); tutorial.ObserveCollection(game, kind, result, Player); }
        ActionResult StartBatch(string species = "pondlet", TutorialActionOrigin origin = Player)
        {
            var recipe = game.GetSpecies(species); game.Tick(20);
            for (int i = 0; i < recipe.Bloom; i++) { Collect(CultureKind.Bloom); game.Dose(CultureKind.Bloom); }
            for (int i = 0; i < recipe.Flow; i++) { Collect(CultureKind.Flow); game.Dose(CultureKind.Flow); }
            tutorial.ObservePrepared(game, species, origin); return StartStaged(origin);
        }
        ActionResult StartStaged(TutorialActionOrigin origin = Player)
        { var result = game.BeginIncubation(); Assert.That(result.Success, Is.True); tutorial.ObserveStarted(game, result, origin); return result; }
        ActionResult Produce(string species = "pondlet", TutorialActionOrigin origin = Player)
        { var result = StartBatch(species, origin); game.Tick(10); tutorial.ObserveReady(game); return result; }
        ActionResult Display(string tank, TutorialActionOrigin origin = Player)
        { var result = game.TransferToTank(tank); Assert.That(result.Success, Is.True); tutorial.ObserveDisplayed(game, result, origin); return result; }
        ActionResult Sale(string tank, TutorialActionOrigin origin = Player)
        { var result = game.SellFromTank(tank); Assert.That(result.Success, Is.True); tutorial.ObserveSale(game, result, origin, cycle.DayNumber); return result; }
        void AssertRoundTrip()
        {
            var saved = tutorial.CaptureSave(); var previousLesson = tutorial.CurrentLesson(cycle); int money = game.Currency;
            Assert.That(ShopTutorial.TryRestore(saved, game, cycle.DayNumber, out var restored), Is.True);
            Assert.That(restored.CurrentLesson(cycle), Is.EqualTo(previousLesson));
            Assert.That(restored.CaptureSave().first.specimenId, Is.EqualTo(saved.first.specimenId));
            Assert.That(restored.CaptureSave().practice.specimenId, Is.EqualTo(saved.practice.specimenId));
            Assert.That(restored.PracticeAssisted, Is.EqualTo(tutorial.PracticeAssisted));
            tutorial = restored; Assert.That(game.Currency, Is.EqualTo(money));
        }
    }
}
