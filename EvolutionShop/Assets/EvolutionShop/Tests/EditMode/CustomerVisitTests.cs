using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class CustomerVisitTests
    {
        private GameConfig config;
        private GameModel model;
        private static readonly string[] Tanks = {"tank-a", "tank-b"};

        [SetUp] public void Setup() { config = GameConfig.CreateDefault(); model = new GameModel(config); }

        [Test]
        public void CompleteVisitReservesExactStockWaitsForServiceAndPaysOnlyOnce()
        {
            Produce("pondlet", "tank-a");
            string specimen = model.GetTank("tank-a").Value.Id;
            var customer = new CustomerVisit(model, Tanks);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Arriving));
            Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.None));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(customer.Serve(ShopDayPhase.Open).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Browsing));
            customer.Advance(2f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            Assert.That(customer.SpecimenId, Is.EqualTo(specimen));
            Assert.That(customer.SpeciesId, Is.EqualTo("pondlet")); Assert.That(customer.Price, Is.EqualTo(6));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(customer.Id));
            Assert.That(customer.Serve(ShopDayPhase.Open).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Queued)); Assert.That(customer.QueueSecondsRemaining, Is.EqualTo(45f));
            customer.Advance(5f, true, ShopDayPhase.Open);
            Assert.That(customer.QueueSecondsRemaining, Is.EqualTo(40f)); Assert.That(model.Currency, Is.Zero);
            Ok(customer.Serve(ShopDayPhase.Open));
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Paid)); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Sold));
            Assert.That(model.GetTank("tank-a"), Is.Null); Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.GetReservation("tank-a"), Is.Null); Assert.That(customer.QueueSecondsRemaining, Is.Zero);
            Assert.That(customer.Serve(ShopDayPhase.Open).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            customer.Advance(1.5f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving));
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Finished));
            customer.Advance(float.MaxValue, true, ShopDayPhase.Open);
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void DestinationPrefersFirstAvailableStockAndSkipsOthersReservations()
        {
            Produce("pondlet", "tank-a"); Produce("bubblebelly", "tank-b");
            Ok(model.ReserveTank("tank-a", "another-visitor"));
            var customer = new CustomerVisit(model, Tanks);
            Assert.That(customer.TargetTankId, Is.EqualTo("tank-b"));
            Queue(customer);
            Assert.That(customer.SpeciesId, Is.EqualTo("bubblebelly"));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("another-visitor"));
            Assert.That(new CustomerVisit(model, Tanks).Id, Is.Not.EqualTo(customer.Id));
        }

        [Test]
        public void EmptyShopStillBrowsesThenLeavesWithoutPayment()
        {
            var customer = new CustomerVisit(model, Tanks);
            Assert.That(customer.TargetTankId, Is.EqualTo("tank-a"));
            customer.Advance(1f, true, ShopDayPhase.Open);
            customer.Advance(2f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.NoStock));
            Assert.That(model.Currency, Is.Zero); Assert.That(customer.SpecimenId, Is.Null);
            Assert.That(customer.Serve(ShopDayPhase.Open).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
        }

        [Test]
        public void LastStockRaceDoesNotStealOrReleaseAnotherVisitReservation()
        {
            Produce("pondlet", "tank-a");
            var first = new CustomerVisit(model, Tanks); var second = new CustomerVisit(model, Tanks);
            first.Advance(1f, true, ShopDayPhase.Open); second.Advance(1f, true, ShopDayPhase.Open);
            first.Advance(2f, false, ShopDayPhase.Open); second.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(first.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            Assert.That(second.Outcome, Is.EqualTo(CustomerOutcome.NoStock));
            second.Cancel(CustomerOutcome.Closed);
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(first.Id));
            Assert.That(model.GetTank("tank-a").HasValue, Is.True); Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void ImpatienceReleasesOnlyOwnStockAndDoesNotRestartDeparture()
        {
            Produce("pondlet", "tank-a"); Produce("bubblebelly", "tank-b");
            var customer = new CustomerVisit(model, Tanks); Queue(customer);
            string specimen = customer.SpecimenId;
            Ok(model.ReserveTank("tank-b", "other"));
            customer.Advance(45f, false, ShopDayPhase.Open);
            Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Impatient));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b").Value.CustomerId, Is.EqualTo("other"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(specimen));
            customer.Advance(17f, false, ShopDayPhase.Open); customer.Cancel(CustomerOutcome.Closed);
            customer.Advance(1f, false, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Finished));
            Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Impatient)); Assert.That(model.Currency, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosingBeforeReservationCancelsAnUncommittedVisitor(bool browsing)
        {
            Produce("pondlet", "tank-a");
            var customer = new CustomerVisit(model, Tanks);
            if (browsing) customer.Advance(1f, true, ShopDayPhase.Open);
            customer.Advance(100f, true, ShopDayPhase.Closing);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Closed));
            Assert.That(model.GetReservation("tank-a"), Is.Null); Assert.That(model.GetTank("tank-a").HasValue, Is.True);
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void AcceptedCustomerCanReachCounterAndPayDuringClosing()
        {
            Produce("pondlet", "tank-a");
            var customer = new CustomerVisit(model, Tanks);
            customer.Advance(1f, true, ShopDayPhase.Open); customer.Advance(2f, false, ShopDayPhase.Open);
            customer.Advance(1f, true, ShopDayPhase.Closing);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Queued));
            Ok(customer.Serve(ShopDayPhase.Closing));
            Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Sold)); Assert.That(model.Currency, Is.EqualTo(6));
            customer.Advance(1f, false, ShopDayPhase.Night);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Finished)); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Sold));
            Assert.That(model.GetTank("tank-a"), Is.Null);
        }

        [Test]
        public void NightFinishesUnpaidCustomerAndKeepsTheExactStock()
        {
            Produce("pondlet", "tank-a");
            var customer = new CustomerVisit(model, Tanks); Queue(customer);
            string specimen = customer.SpecimenId;
            Assert.That(customer.Serve(ShopDayPhase.Night).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            customer.Advance(0.1f, false, ShopDayPhase.Night);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Finished)); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Closed));
            Assert.That(model.GetReservation("tank-a"), Is.Null); Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(specimen));
            Assert.That(model.Currency, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TravelStallReleasesOrderAndDepartureHasItsOwnBound(bool toCounter)
        {
            Produce("pondlet", "tank-a");
            var customer = new CustomerVisit(model, Tanks);
            if (toCounter) { customer.Advance(1f, true, ShopDayPhase.Open); customer.Advance(2f, false, ShopDayPhase.Open); }
            customer.Advance(18f, false, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving)); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.PathBlocked));
            Assert.That(model.GetReservation("tank-a"), Is.Null); Assert.That(model.GetTank("tank-a").HasValue, Is.True);
            customer.Advance(17.5f, false, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving));
            customer.Advance(0.5f, false, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Finished)); Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void InvalidDeltasCannotAdvanceOrCancelAVisit()
        {
            Produce("pondlet", "tank-a"); var customer = new CustomerVisit(model, Tanks); Queue(customer);
            foreach (float delta in new[] {0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity})
            {
                customer.Advance(delta, true, ShopDayPhase.Open);
                customer.Advance(delta, true, ShopDayPhase.Night);
            }
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Queued)); Assert.That(customer.QueueSecondsRemaining, Is.EqualTo(45f));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(customer.Id)); Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void OneLargeUpdatePerformsAtMostOneTransitionAndNeverAutomaticallyPays()
        {
            Produce("pondlet", "tank-a"); var customer = new CustomerVisit(model, Tanks);
            customer.Advance(float.MaxValue, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Browsing)); Assert.That(model.GetReservation("tank-a"), Is.Null);
            customer.Advance(float.MaxValue, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            customer.Advance(float.MaxValue, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Queued)); Assert.That(customer.QueueSecondsRemaining, Is.EqualTo(45f));
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void LostReservationCannotChargeForReplacementStockOrReleaseItsNewOwner()
        {
            Produce("pondlet", "tank-a"); var customer = new CustomerVisit(model, Tanks); Queue(customer);
            Ok(model.ReleaseReservation("tank-a", customer.Id)); Ok(model.SellFromTank("tank-a"));
            Produce("bubblebelly", "tank-a"); Ok(model.ReserveTank("tank-a", "replacement-owner"));
            var failure = customer.Serve(ShopDayPhase.Open);
            Assert.That(failure.Success, Is.False); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.OrderLost));
            Assert.That(model.Currency, Is.EqualTo(6)); Assert.That(model.GetTank("tank-a").Value.SpeciesId, Is.EqualTo("bubblebelly"));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("replacement-owner"));
        }

        [Test]
        public void MissingReservationIsDetectedBeforeReachingCounter()
        {
            Produce("pondlet", "tank-a"); var customer = new CustomerVisit(model, Tanks);
            customer.Advance(1f, true, ShopDayPhase.Open); customer.Advance(2f, false, ShopDayPhase.Open);
            Ok(model.ReleaseReservation("tank-a", customer.Id));
            customer.Advance(1f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving)); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.OrderLost));
            Assert.That(model.Currency, Is.Zero); Assert.That(model.GetTank("tank-a").HasValue, Is.True);
        }

        [Test]
        public void CancellingPaidCustomerPreservesCommittedSale()
        {
            Produce("pondlet", "tank-a"); var customer = new CustomerVisit(model, Tanks); Queue(customer);
            Ok(customer.Serve(ShopDayPhase.Open)); customer.Cancel(CustomerOutcome.PathBlocked);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Leaving)); Assert.That(customer.Outcome, Is.EqualTo(CustomerOutcome.Sold));
            customer.Advance(18f, false, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Finished)); Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.GetTank("tank-a"), Is.Null);
        }

        [Test]
        public void SaveRestoreReleasesTransientVisitOwnershipButPreservesUnpaidStock()
        {
            Produce("pondlet", "tank-a"); var customer = new CustomerVisit(model, Tanks); Queue(customer);
            string specimen = customer.SpecimenId;
            var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(model.CaptureSave()));
            Assert.That(GameModel.TryRestore(config, save, out var restored, out _), Is.True);
            Assert.That(restored.GetReservation("tank-a"), Is.Null); Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(specimen));
            Assert.That(restored.Currency, Is.Zero);
            var replacement = new CustomerVisit(restored, Tanks); Queue(replacement);
            Ok(replacement.Serve(ShopDayPhase.Open)); Assert.That(restored.Currency, Is.EqualTo(6));
            Assert.That(restored.GetTank("tank-a"), Is.Null);
        }

        [Test]
        public void RulesAreCopiedAndAllTimingInputsMustBePositiveAndFinite()
        {
            Produce("pondlet", "tank-a"); var rules = new CustomerRules {BrowseSeconds = 0.5f, PatienceSeconds = 4f};
            var customer = new CustomerVisit(model, Tanks, rules); rules.BrowseSeconds = 100f; rules.PatienceSeconds = 100f;
            customer.Advance(1f, true, ShopDayPhase.Open); customer.Advance(0.5f, false, ShopDayPhase.Open); customer.Advance(1f, true, ShopDayPhase.Open);
            Assert.That(customer.QueueSecondsRemaining, Is.EqualTo(4f));
            foreach (float invalid in new[] {0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity})
            {
                Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, new CustomerRules {ArrivalDelaySeconds = invalid}));
                Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, new CustomerRules {NextArrivalSeconds = invalid}));
                Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, new CustomerRules {BrowseSeconds = invalid}));
                Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, new CustomerRules {PatienceSeconds = invalid}));
                Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, new CustomerRules {TravelTimeoutSeconds = invalid}));
                Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, new CustomerRules {PaidReactionSeconds = invalid}));
            }
        }

        [Test]
        public void InvalidConstructionAndFakeSuccessCancellationAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomerVisit(null, Tanks));
            Assert.Throws<ArgumentException>(() => new CustomerVisit(model, null));
            Assert.Throws<ArgumentException>(() => new CustomerVisit(model, new string[0]));
            Assert.Throws<ArgumentException>(() => new CustomerVisit(model, new[] {"missing"}));
            Assert.Throws<ArgumentException>(() => new CustomerVisit(model, new[] {"tank-a", "tank-a"}));
            var customer = new CustomerVisit(model, Tanks);
            Assert.Throws<ArgumentOutOfRangeException>(() => customer.Cancel(CustomerOutcome.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => customer.Cancel(CustomerOutcome.Sold));
            Assert.Throws<ArgumentOutOfRangeException>(() => customer.Cancel((CustomerOutcome)999));
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Arriving));
            Assert.That(customer.Serve((ShopDayPhase)999).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            customer.Advance(100f, true, (ShopDayPhase)999);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Arriving));
        }

        private static void Queue(CustomerVisit customer)
        {
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            customer.Advance(2f, false, ShopDayPhase.Open);
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Queued));
        }
        private void Produce(string species, string tank)
        {
            model.Tick(100f); var recipe = model.GetSpecies(species);
            for (int i=0; i<recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i=0; i<recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Ok(model.TransferToTank(tank));
        }
        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
