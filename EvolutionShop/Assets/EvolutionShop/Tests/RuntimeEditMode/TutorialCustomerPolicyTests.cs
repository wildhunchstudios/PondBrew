using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// Isolated production director selection and real customer transactions. Navigation,
    /// native input and actual frame-time pause are covered by the runtime smoke, not these fixtures.
    /// </summary>
    public sealed class TutorialCustomerPolicyTests
    {
        const string Prefix = "EvolutionShop-TutorialCustomerPolicy-";
        GameObject owner;
        ShopSession session;
        ShopCustomers customers;
        CustomerArchetype[] catalog;
        string directory, path;
        CursorLockMode cursorLock;
        bool cursorVisible;

        [SetUp]
        public void Setup()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "slot.json");
            owner = new GameObject("Isolated tutorial customer policy"); owner.SetActive(false);
            session = owner.AddComponent<ShopSession>(); customers = owner.AddComponent<ShopCustomers>();
            Set("Config", GameConfig.CreateDefault()); Set("Model", new GameModel(session.Config));
            Set("Cycle", new ShopDayCycle()); Set("Reports", new ShopTradeLedger());
            Set("Reputation", new ShopReputation()); Set("Layout", new ShopLayout());
            Set("Staff", new ShopStaff()); Set("Stocking", new ShopStocking()); Set("Laboratory", new ShopLaboratory());
            Set("SavePath", path); Set("Customers", customers);
            catalog = CustomerArchetypes.CreateDefault();
            Field(customers, "shop", session); Field(customers, "catalog", catalog);
            customers.MaxVisitors = 2;
        }

        [TearDown]
        public void Cleanup()
        {
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory), Does.StartWith(Prefix));
            Directory.Delete(directory, true);
        }

        [TestCase(TutorialMode.Offered, false)]
        [TestCase(TutorialMode.Active, true)]
        [TestCase(TutorialMode.Skipped, false)]
        [TestCase(TutorialMode.Completed, false)]
        [TestCase(TutorialMode.LegacyUnguided, false)]
        public void OnlyActiveUnsoldSaveChangesFutureArrivals(TutorialMode mode, bool introductory)
        {
            if (mode == TutorialMode.Active) Assert.That(session.Tutorial.Start(), Is.True);
            if (mode == TutorialMode.Skipped) Assert.That(session.Tutorial.Skip(), Is.True);
            if (mode == TutorialMode.LegacyUnguided) Set("Tutorial", ShopTutorial.LegacyUnguided());
            if (mode == TutorialMode.Completed) CompleteTutorialFixture();
            session.ReducedGags = true; session.HumorVolume = .23f;
            Assert.That(session.Tutorial.Mode, Is.EqualTo(mode));
            Assert.That(customers.IntroductoryArrivalsActive, Is.EqualTo(introductory));
            Assert.That(customers.EffectiveMaxVisitors, Is.EqualTo(introductory ? 1 : 2));
            Assert.That(customers.MaxVisitors, Is.EqualTo(2), "Guidance cannot overwrite the configured normal limit.");
            Assert.That(session.AmbientComedyDeferredForTutorial, Is.EqualTo(introductory));
            Assert.That(session.ReducedGags, Is.True); Assert.That(session.HumorVolume, Is.EqualTo(.23f));
            Assert.That(customers.ArrivingTypes, Is.EqualTo(introductory ? 1 : 4));
        }

        [Test]
        public void ShippingPolicyDoesNotConsumeValidationOverridesOrMutateCustomerCatalog()
        {
            Assert.That(session.ValidationMode, Is.False);
            string before = JsonUtility.ToJson(new CatalogSnapshot { values = catalog });
            Assert.That(session.Tutorial.Start(), Is.True);
            customers.ScriptedArchetypes.Enqueue(CustomerArchetypes.Premium);
            customers.ForcedArchetypeId = CustomerArchetypes.Bargain;
            for (int i = 0; i < 5; i++)
            {
                var type = Choose(i);
                Assert.That(type.Id, Is.EqualTo(CustomerArchetypes.Casual));
                Assert.That(type.PatienceSeconds, Is.EqualTo(90));
                type.Budget = 1; type.PatienceSeconds = 1;
            }
            Assert.That(customers.ScriptedArchetypes.Count, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(new CatalogSnapshot { values = catalog }), Is.EqualTo(before));
            Assert.That(session.Tutorial.Skip(), Is.True);
            Assert.That(Choose(5).Id, Is.EqualTo(CustomerArchetypes.Premium));
            Assert.That(customers.ScriptedArchetypes.Count, Is.Zero);
            Assert.That(Choose(6).Id, Is.EqualTo(CustomerArchetypes.Bargain));
        }

        [Test]
        public void IntroductoryPreviewsUseTheActualCasualBudgetAndCopies()
        {
            var ordinary = customers.Archetypes;
            var normalCasual = CustomerArchetypes.Find(ordinary, CustomerArchetypes.Casual);
            Assert.That(session.Tutorial.Start(), Is.True);
            var preview = customers.Archetypes;
            Assert.That(preview.Count, Is.EqualTo(1));
            var initial = preview[0];
            Assert.That(initial.Id, Is.EqualTo(normalCasual.Id));
            Assert.That(initial.Budget, Is.EqualTo(normalCasual.Budget));
            Assert.That(initial.TolerancePercent, Is.EqualTo(normalCasual.TolerancePercent));
            Assert.That(initial.FavoriteTolerancePercent, Is.EqualTo(normalCasual.FavoriteTolerancePercent));
            Assert.That(initial.FavoriteSpecies, Is.EqualTo(normalCasual.FavoriteSpecies));
            Assert.That(initial.BargainPercent, Is.EqualTo(normalCasual.BargainPercent));
            Assert.That(initial.BrowseSeconds, Is.EqualTo(normalCasual.BrowseSeconds));
            Assert.That(initial.PresentationId, Is.EqualTo(normalCasual.PresentationId));
            Assert.That(initial.PatienceSeconds, Is.EqualTo(90));
            Stock(false);
            var standard = session.Model.GetPrice("pondlet").Value;
            Assert.That(customers.BuyerTypes(standard), Is.EqualTo(1));
            int unacceptable = normalCasual.AcceptableLimit("pondlet", standard.Standard) + 1;
            Ok(session.Model.SetPrice("pondlet", unacceptable));
            var tag = session.Model.GetPrice("pondlet").Value;
            Assert.That(customers.Preview(tag), Is.EqualTo(PriceReaction.TooExpensive));
            Assert.That(customers.BuyerTypes(tag), Is.Zero);
            initial.Budget = 1; initial.PatienceSeconds = 1;
            Assert.That(customers.Archetypes[0].Budget, Is.EqualTo(normalCasual.Budget));
            Assert.That(customers.Archetypes[0].PatienceSeconds, Is.EqualTo(90));
            Assert.That(session.Tutorial.Skip(), Is.True);
            Assert.That(customers.ArrivingTypes, Is.EqualTo(4));
            Assert.That(customers.BuyerTypes(tag), Is.GreaterThan(0), "Ordinary higher-tolerance buyers return after skipping.");
        }

        [Test]
        public void QueuedVisitorHasFiniteNinetySecondActiveBudgetAndSafeRetry()
        {
            StartAndOpen(); Stock(false);
            var visit = NewQueuedVisit();
            Assert.That(visit.QueueSecondsRemaining, Is.EqualTo(90));
            visit.Advance(89.5f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
            Assert.That(visit.QueueSecondsRemaining, Is.EqualTo(.5f).Within(.001f));
            // An omitted Advance contributes no active time; the runtime smoke checks actual pause/help frames.
            Assert.That(visit.Archetype.PatienceSeconds, Is.EqualTo(90));
            Assert.That(visit.QueueSecondsRemaining, Is.EqualTo(.5f).Within(.001f));
            visit.Advance(.5f, false, ShopDayPhase.Open);
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.Impatient));
            Assert.That(session.Model.GetReservation("tank-a").HasValue, Is.False);
            Assert.That(session.Model.GetTank("tank-a").HasValue, Is.True);
            Assert.That(session.Model.Currency, Is.Zero); Assert.That(customers.IntroductoryArrivalsActive, Is.True);
            Resolve(visit);
            var retry = NewQueuedVisit(); Assert.That(retry.QueueSecondsRemaining, Is.EqualTo(90));
            CompleteSale(retry, TutorialActionOrigin.Player);
            Assert.That(session.Model.Currency, Is.EqualTo(6)); Assert.That(customers.IntroductoryArrivalsActive, Is.False);
        }

        [Test]
        public void SkipPreservesAcceptedPriceAndPerVisitPatienceWhileNewVisitorsUseNormalSettings()
        {
            StartAndOpen(); Stock(false);
            var visit = NewQueuedVisit(); visit.Advance(12, false, ShopDayPhase.Open);
            Assert.That(visit.Price, Is.EqualTo(6));
            Ok(session.Model.SetPrice("pondlet", 10));
            Assert.That(session.SkipTutorial(), Is.True, session.SaveError);
            Assert.That(visit.QueueSecondsRemaining, Is.EqualTo(78));
            Assert.That(visit.Archetype.PatienceSeconds, Is.EqualTo(90));
            Assert.That(visit.Price, Is.EqualTo(6));
            Assert.That(session.Model.GetReservation("tank-a").Value.Price, Is.EqualTo(6));
            customers.ForcedArchetypeId = CustomerArchetypes.Casual;
            Assert.That(Choose(1).PatienceSeconds, Is.EqualTo(45));
            var sold = visit.Serve(ShopDayPhase.Open); Ok(sold); Resolve(visit);
            Assert.That(session.Model.Currency, Is.EqualTo(6));
            Assert.That(session.Tutorial.ObserveSale(session.Model, sold, TutorialActionOrigin.Player, 1), Is.False);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Skipped));
            Assert.That(customers.EffectiveMaxVisitors, Is.EqualTo(2));
        }

        [TestCase(TutorialActionOrigin.Player, true)]
        [TestCase(TutorialActionOrigin.Player, false)]
        [TestCase(TutorialActionOrigin.Worker, true)]
        [TestCase(TutorialActionOrigin.Worker, false)]
        public void FirstRealSaleEndsPolicyPermanentlyIncludingWorkerAndUntrackedStock(TutorialActionOrigin origin, bool tracked)
        {
            StartAndOpen(); Stock(tracked);
            var visit = NewQueuedVisit(); var sale = CompleteSale(visit, origin);
            Assert.That(customers.IntroductoryArrivalsActive, Is.False);
            Assert.That(customers.EffectiveMaxVisitors, Is.EqualTo(2));
            Assert.That(session.AmbientComedyDeferredForTutorial, Is.False);
            Assert.That(session.Model.Currency, Is.EqualTo(6));
            Assert.That(visit.Serve(ShopDayPhase.Open).Success, Is.False, "A repeated checkout cannot pay twice.");
            Assert.That(session.Model.Currency, Is.EqualTo(6));
            Assert.That(session.Tutorial.CaptureSave().first.saleDay, Is.EqualTo(tracked && origin == TutorialActionOrigin.Player ? 1 : 0));
            for (int reload = 0; reload < 2; reload++)
            {
                Assert.That(session.SaveNow(), Is.True, session.SaveError);
                ReadIntoFixture();
                Assert.That(customers.IntroductoryArrivalsActive, Is.False);
                Assert.That(session.Tutorial.CaptureSave().firstSaleAccommodationEnded, Is.True);
                Assert.That(session.Model.Currency, Is.EqualTo(6));
                Assert.That(session.Model.GetTank("tank-a").HasValue, Is.False);
            }
            Assert.That(sale.Success, Is.True);
        }

        [Test]
        public void RefusedPriceAndPrematureCheckoutDoNotManufactureSaleOrConsumeAccommodation()
        {
            StartAndOpen(); Stock(false);
            Ok(session.Model.SetPrice("pondlet", 10));
            var visit = NewVisit();
            var premature = visit.Serve(ShopDayPhase.Open);
            Assert.That(premature.Success, Is.False);
            Assert.That(session.Tutorial.ObserveSale(session.Model, premature, TutorialActionOrigin.Player, 1), Is.False);
            visit.Advance(.1f, true, ShopDayPhase.Open); visit.Advance(3, false, ShopDayPhase.Open);
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(visit.PriceRejections, Is.EqualTo(1));
            Assert.That(session.Model.GetReservation("tank-a").HasValue, Is.False);
            Assert.That(session.Model.Currency, Is.Zero); Assert.That(customers.IntroductoryArrivalsActive, Is.True);
            Resolve(visit); Ok(session.Model.SetPrice("pondlet", 6));
            var retry = NewQueuedVisit(); CompleteSale(retry, TutorialActionOrigin.Player);
            Assert.That(session.Model.Currency, Is.EqualTo(6)); Assert.That(customers.IntroductoryArrivalsActive, Is.False);
        }

        [Test]
        public void IntroductoryVisitorStillRefusesAStandardTagAboveItsOrdinaryBudget()
        {
            // Deliberately low-budget catalog fixture isolates the hard budget from price tolerance.
            // Guidance must preserve configured values, including a budget below a standard tag.
            foreach (var type in catalog) if (type.Id == CustomerArchetypes.Casual) type.Budget = 5;
            StartAndOpen(); Stock(false); var visit = NewVisit();
            Assert.That(visit.Budget, Is.EqualTo(5));
            visit.Advance(.1f, true, ShopDayPhase.Open); visit.Advance(3, false, ShopDayPhase.Open);
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(visit.RefusedOverBudget, Is.True);
            Assert.That(session.Model.GetReservation("tank-a").HasValue, Is.False);
            Assert.That(session.Model.Currency, Is.Zero); Assert.That(customers.IntroductoryArrivalsActive, Is.True);
        }

        [TestCase(CustomerOutcome.Closed)]
        [TestCase(CustomerOutcome.PathBlocked)]
        [TestCase(CustomerOutcome.OrderLost)]
        [TestCase(CustomerOutcome.Slapped)]
        public void CancelledAcceptedVisitReleasesOnlyItsStockAndAllowsPatientReplacement(CustomerOutcome outcome)
        {
            StartAndOpen(); Stock(false);
            var visit = NewQueuedVisit(); visit.Cancel(outcome); Resolve(visit);
            Assert.That(session.Model.GetReservation("tank-a").HasValue, Is.False);
            Assert.That(session.Model.GetTank("tank-a").HasValue, Is.True);
            Assert.That(session.Model.Currency, Is.Zero); Assert.That(customers.IntroductoryArrivalsActive, Is.True);
            var replacement = NewQueuedVisit();
            Assert.That(replacement.Id, Is.Not.EqualTo(visit.Id));
            Assert.That(replacement.QueueSecondsRemaining, Is.EqualTo(90));
        }

        [Test]
        public void FailedSaleSaveKeepsOldSnapshotTogetherAndRetryCommitsMoneyWithPolicyFlag()
        {
            StartAndOpen(); Stock(false);
            Assert.That(session.SaveNow(), Is.True, session.SaveError); byte[] original = File.ReadAllBytes(path);
            var visit = NewQueuedVisit(); CompleteSale(visit, TutorialActionOrigin.Worker);
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(session.SaveNow(), Is.False); Assert.That(session.SaveError, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(SaveStore.Read(path, session.Config, out var previousGame, out var previousSlot, out var error), Is.True, error);
            Assert.That(previousGame.Currency, Is.Zero); Assert.That(previousGame.GetTank("tank-a").HasValue, Is.True);
            Assert.That(previousSlot.tutorial.firstSaleAccommodationEnded, Is.False);
            Assert.That(session.Model.Currency, Is.EqualTo(6)); Assert.That(customers.IntroductoryArrivalsActive, Is.False);
            Directory.Delete(path + ".tmp", false);
            Assert.That(session.SaveNow(), Is.True, session.SaveError); ReadIntoFixture();
            Assert.That(session.Model.Currency, Is.EqualTo(6)); Assert.That(session.Model.GetTank("tank-a").HasValue, Is.False);
            Assert.That(customers.IntroductoryArrivalsActive, Is.False);
        }

        [Test]
        public void ReloadOfUnpaidReservationCancelsTransientVisitWithoutInventingASale()
        {
            StartAndOpen(); Stock(false); var accepted = NewQueuedVisit();
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            ReadIntoFixture();
            Assert.That(session.Model.Currency, Is.Zero); Assert.That(session.Model.GetTank("tank-a").HasValue, Is.True);
            Assert.That(session.Model.GetReservation("tank-a").HasValue, Is.False);
            Assert.That(customers.IntroductoryArrivalsActive, Is.True);
            var replacement = NewQueuedVisit();
            Assert.That(replacement.Id, Is.Not.EqualTo(accepted.Id)); Assert.That(replacement.QueueSecondsRemaining, Is.EqualTo(90));
        }

        void StartAndOpen()
        {
            Assert.That(session.Tutorial.Start(), Is.True);
            Assert.That(session.Cycle.OpenShop(), Is.True); Assert.That(session.Reports.BeginDay(1, 0), Is.True);
        }
        void Stock(bool tracked)
        {
            session.Model.Tick(100); // Fixture culture regrowth only; runtime tests retain ordinary production timing.
            Ok(session.Model.Collect(CultureKind.Bloom)); Ok(session.Model.Dose(CultureKind.Bloom));
            Ok(session.Model.Collect(CultureKind.Flow)); Ok(session.Model.Dose(CultureKind.Flow));
            var started = session.Model.BeginIncubation(); Ok(started);
            if (tracked) Assert.That(session.Tutorial.ObserveStarted(session.Model, started, TutorialActionOrigin.Player), Is.True);
            session.Model.Tick(session.Config.IncubationSeconds);
            if (tracked) session.Tutorial.ObserveReady(session.Model);
            var displayed = session.Model.TransferToTank("tank-a"); Ok(displayed);
            if (tracked) Assert.That(session.Tutorial.ObserveDisplayed(session.Model, displayed, TutorialActionOrigin.Player), Is.True);
        }
        CustomerVisit NewVisit()
        {
            var visit = new CustomerVisit(session.Model, session.Model.TankIds, null, Choose(session.Reports.Current.visitors));
            Assert.That(session.Reports.BeginVisit(visit.Id), Is.True); return visit;
        }
        CustomerVisit NewQueuedVisit()
        {
            var visit = NewVisit(); visit.Advance(.1f, true, ShopDayPhase.Open);
            visit.Advance(visit.Archetype.BrowseSeconds, false, ShopDayPhase.Open);
            visit.Advance(.1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued)); return visit;
        }
        ActionResult CompleteSale(CustomerVisit visit, TutorialActionOrigin origin)
        {
            var result = visit.Serve(ShopDayPhase.Open); Ok(result); Resolve(visit);
            Assert.That(session.Tutorial.ObserveSale(session.Model, result, origin, session.Cycle.DayNumber), Is.True);
            return result;
        }
        void Resolve(CustomerVisit visit) => Assert.That(session.Reports.ResolveVisit(visit.Id, visit.Outcome,
            visit.Outcome == CustomerOutcome.Sold ? visit.Price : 0), Is.True);
        void ReadIntoFixture()
        {
            Assert.That(SaveStore.Read(path, session.Config, out var model, out var slot, out var error), Is.True, error);
            Assert.That(ShopTutorial.TryRestore(slot.tutorial, model, slot.cycle.dayNumber, out var tutorial), Is.True);
            Assert.That(ShopDayCycle.TryRestore(slot.cycle, out var cycle), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var reports), Is.True);
            reports.CancelPendingForReload(); Set("Model", model); Set("Tutorial", tutorial); Set("Cycle", cycle); Set("Reports", reports);
        }
        void CompleteTutorialFixture()
        {
            StartAndOpen(); session.Tutorial.ObserveOrientation(true, true, true);
            Stock(true); CompleteSale(NewQueuedVisit(), TutorialActionOrigin.Player);
            Stock(true); CompleteSale(NewQueuedVisit(), TutorialActionOrigin.Player);
            Assert.That(session.Cycle.RequestClose(), Is.True); session.Cycle.Tick(3); session.Reports.FinishDay(0);
            Assert.That(session.Tutorial.ObserveReportAcknowledged(1), Is.True);
            Assert.That(session.Tutorial.Finish(session.Cycle), Is.True);
        }
        CustomerArchetype Choose(int index) => (CustomerArchetype)typeof(ShopCustomers)
            .GetMethod("ChooseArchetype", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(customers, new object[] { index });
        void Set(string property, object value) => typeof(ShopSession).GetProperty(property).SetValue(session, value);
        static void Field(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        [Serializable] sealed class CatalogSnapshot { public CustomerArchetype[] values; }
    }
}
