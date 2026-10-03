using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ReservationTests
    {
        GameConfig config;
        GameModel model;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
        }

        [Test]
        public void ReservationCapturesExactResidentAndPriceWithoutMovingStockOrMoney()
        {
            string id = Stock("tank-a", "pondlet");
            string before = Snapshot();
            var result = model.ReserveTank("tank-a", "visitor-a");
            Ok(result);
            Assert.That(result.SpecimenId, Is.EqualTo(id));
            Assert.That(result.SpeciesId, Is.EqualTo("pondlet"));
            var reservation = model.GetReservation("tank-a").Value;
            Assert.That(reservation.TankId, Is.EqualTo("tank-a"));
            Assert.That(reservation.CustomerId, Is.EqualTo("visitor-a"));
            Assert.That(reservation.SpecimenId, Is.EqualTo(id));
            Assert.That(reservation.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(reservation.Price, Is.EqualTo(6));
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(model.GetReservation("tank-b"), Is.Null);
            Assert.That(model.GetReservation(null), Is.Null);
            Assert.That(model.GetReservation("unknown"), Is.Null);

            // Neither caller-owned tuning nor a returned definition can reprice a committed purchase.
            config.Species[0].SaleValue = 100;
            model.GetSpecies("pondlet").SaleValue = 200;
            Ok(model.SellReservedTank("tank-a", "visitor-a", id));
            Assert.That(model.Currency, Is.EqualTo(reservation.Price));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t\r\n")]
        public void InvalidCustomerCannotCreateCancelOrServeAReservation(string customerId)
        {
            string id = Stock("tank-a", "pondlet");
            AtomicFailure(() => model.ReserveTank("tank-a", customerId), FailureCode.InvalidCustomer);
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.ReleaseReservation("tank-a", customerId), FailureCode.InvalidCustomer);
            AtomicFailure(() => model.SellReservedTank("tank-a", customerId, id), FailureCode.InvalidCustomer);
        }

        [Test]
        public void CustomerIdentifierLimitAccepts128CharactersAndRejects129()
        {
            string id = Stock("tank-a", "pondlet");
            string accepted = new string('a', 128);
            string rejected = new string('a', 129);
            AtomicFailure(() => model.ReserveTank("tank-a", rejected), FailureCode.InvalidCustomer);
            Ok(model.ReserveTank("tank-a", accepted));
            AtomicFailure(() => model.ReleaseReservation("tank-a", rejected), FailureCode.InvalidCustomer);
            AtomicFailure(() => model.SellReservedTank("tank-a", rejected, id), FailureCode.InvalidCustomer);
            Ok(model.SellReservedTank("tank-a", accepted, id));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown")]
        public void UnknownTankCannotCreateCancelOrServeAReservation(string tankId)
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.ReserveTank(tankId, "visitor-b"), FailureCode.UnknownTank);
            AtomicFailure(() => model.ReleaseReservation(tankId, "visitor-a"), FailureCode.UnknownTank);
            AtomicFailure(() => model.SellReservedTank(tankId, "visitor-a", id), FailureCode.UnknownTank);
        }

        [Test]
        public void EmptyTankCannotBeReserved()
        {
            AtomicFailure(() => model.ReserveTank("tank-a", "visitor-a"), FailureCode.TankEmpty);
            Assert.That(model.GetReservation("tank-a"), Is.Null);
        }

        [TestCase("visitor-a")]
        [TestCase("visitor-b")]
        public void ReservedTankRejectsRepeatedAndCompetingClaims(string otherCustomer)
        {
            Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.ReserveTank("tank-a", otherCustomer), FailureCode.TankReserved);
        }

        [Test]
        public void EachCustomerCanReserveOnlyOneTankWhileDifferentCustomersCanHoldDifferentTanks()
        {
            string a = Stock("tank-a", "pondlet");
            string b = Stock("tank-b", "bubblebelly");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.ReserveTank("tank-b", "visitor-a"), FailureCode.ReservationMismatch);
            Ok(model.ReserveTank("tank-b", "visitor-b"));
            Ok(model.SellReservedTank("tank-a", "visitor-a", a));
            Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.GetReservation("tank-b").Value.SpecimenId, Is.EqualTo(b));
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo(b));
            Ok(model.SellReservedTank("tank-b", "visitor-b", b));
            Assert.That(model.Currency, Is.EqualTo(16));
        }

        [TestCase("visitor-b")]
        [TestCase("VISITOR-A")]
        public void OnlyExactOwnerCanReleaseOrServeAReservation(string wrongOwner)
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.ReleaseReservation("tank-a", wrongOwner), FailureCode.ReservationMismatch);
            AtomicFailure(() => model.SellReservedTank("tank-a", wrongOwner, id), FailureCode.ReservationMismatch);
            Ok(model.SellReservedTank("tank-a", "visitor-a", id));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("different-specimen")]
        public void CheckoutRequiresTheExactReservedSpecimen(string wrongSpecimen)
        {
            Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", wrongSpecimen), FailureCode.ReservationMismatch);
        }

        [Test]
        public void StockWithoutAReservationCannotBeServedOrReleased()
        {
            string id = Stock("tank-a", "pondlet");
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", id), FailureCode.ReservationMismatch);
            AtomicFailure(() => model.ReleaseReservation("tank-a", "visitor-a"), FailureCode.ReservationMismatch);
        }

        [Test]
        public void CancellationKeepsStockAndAllowsAnotherVisitorToRetry()
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            string before = Snapshot();
            Ok(model.ReleaseReservation("tank-a", "visitor-a"));
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            AtomicFailure(() => model.ReleaseReservation("tank-a", "visitor-a"), FailureCode.ReservationMismatch);
            Ok(model.ReserveTank("tank-a", "visitor-b"));
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", id), FailureCode.ReservationMismatch);
            Ok(model.SellReservedTank("tank-a", "visitor-b", id));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void ReleasedCustomerCanChooseAnotherTank()
        {
            Stock("tank-a", "pondlet");
            string other = Stock("tank-b", "bubblebelly");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Ok(model.ReleaseReservation("tank-a", "visitor-a"));
            Ok(model.ReserveTank("tank-b", "visitor-a"));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b").Value.SpecimenId, Is.EqualTo(other));
        }

        [Test]
        public void ReservationBlocksLegacySaleAndOutputReplacementWithoutLosingEitherSpecimen()
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Produce("ribbonfin");
            string output = model.Incubator.SpecimenId;
            AtomicFailure(() => model.SellFromTank("tank-a"), FailureCode.TankReserved);
            AtomicFailure(() => model.TransferToTank("tank-a"), FailureCode.TankFull);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(output));
            Ok(model.ReleaseReservation("tank-a", "visitor-a"));
            Ok(model.SellFromTank("tank-a"));
            Ok(model.TransferToTank("tank-a"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(output));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void OverflowRejectsCheckoutAtomicallyAndClaimRemainsRecoverable()
        {
            string id = Stock("tank-a", "pondlet");
            SetCurrency(int.MaxValue);
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", id), FailureCode.CurrencyOverflow);
            Ok(model.ReleaseReservation("tank-a", "visitor-a"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
            Assert.That(model.Currency, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void CheckoutCanReachMaximumCurrencyAndPaysOnlyOnce()
        {
            string id = Stock("tank-a", "pondlet");
            SetCurrency(int.MaxValue - 6);
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            var result = model.SellReservedTank("tank-a", "visitor-a", id);
            Ok(result);
            Assert.That(result.SpecimenId, Is.EqualTo(id));
            Assert.That(result.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(model.Currency, Is.EqualTo(int.MaxValue));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.IsDiscovered("pondlet"), Is.True);
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", id), FailureCode.ReservationMismatch);
            AtomicFailure(() => model.SellFromTank("tank-a"), FailureCode.TankEmpty);
        }

        [Test]
        public void LateCheckoutForAnOldSpecimenCannotSellAReplacementToTheSameCustomer()
        {
            string oldId = Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Ok(model.SellReservedTank("tank-a", "visitor-a", oldId));
            string newId = Stock("tank-a", "pondlet");
            Assert.That(newId, Is.Not.EqualTo(oldId));
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", oldId), FailureCode.ReservationMismatch);
            Ok(model.SellReservedTank("tank-a", "visitor-a", newId));
            Assert.That(model.Currency, Is.EqualTo(12));
        }

        [Test]
        public void ClearingVisitsOnlyReleasesClaimsAndCanBeRepeatedSafely()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "bubblebelly");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Ok(model.ReserveTank("tank-b", "visitor-b"));
            Start("ribbonfin");
            model.Tick(1.25f);
            string before = Snapshot();
            model.ClearReservations();
            model.ClearReservations();
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b"), Is.Null);
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Ok(model.ReserveTank("tank-b", "visitor-b"));
        }

        [Test]
        public void SavingUnpaidVisitsKeepsTheirStockAndLoadingCancelsOnlyTheirClaims()
        {
            string a = Stock("tank-a", "pondlet");
            string b = Stock("tank-b", "bubblebelly");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Ok(model.ReserveTank("tank-b", "visitor-b"));
            Start("ribbonfin");
            model.Tick(1.25f);
            string json = Snapshot();
            Assert.That(json, Does.Not.Contain("visitor-a"));
            Assert.That(json, Does.Not.Contain("visitor-b"));
            Assert.That(model.GetReservation("tank-a"), Is.Not.Null, "Capturing a save must not cancel live visitors.");
            RestoreJson(json);
            Assert.That(Snapshot(), Is.EqualTo(json));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b"), Is.Null);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(a));
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo(b));
            Assert.That(model.Currency, Is.Zero);
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", a), FailureCode.ReservationMismatch);
            Ok(model.ReserveTank("tank-a", "new-visitor"));
            Ok(model.SellReservedTank("tank-a", "new-visitor", a));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void PaidSaveCannotPayAgainAfterLoading()
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Ok(model.SellReservedTank("tank-a", "visitor-a", id));
            RestoreJson(Snapshot());
            Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            AtomicFailure(() => model.SellReservedTank("tank-a", "visitor-a", id), FailureCode.ReservationMismatch);
            AtomicFailure(() => model.ReserveTank("tank-a", "visitor-a"), FailureCode.TankEmpty);
            AtomicFailure(() => model.SellFromTank("tank-a"), FailureCode.TankEmpty);
        }

        string Snapshot() { return JsonUtility.ToJson(model.CaptureSave()); }

        void AtomicFailure(Func<ActionResult> action, FailureCode expected)
        {
            string before = Snapshot();
            var reservationA = model.GetReservation("tank-a");
            var reservationB = model.GetReservation("tank-b");
            var result = action();
            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(expected));
            Assert.That(Snapshot(), Is.EqualTo(before), "Rejected command changed saved progression or stock.");
            Assert.That(model.GetReservation("tank-a"), Is.EqualTo(reservationA));
            Assert.That(model.GetReservation("tank-b"), Is.EqualTo(reservationB));
        }

        static void Ok(ActionResult result) { Assert.That(result.Success, Is.True, result.Failure.ToString()); }

        void RestoreJson(string json)
        {
            var save = JsonUtility.FromJson<SaveData>(json);
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.True, error.ToString());
            model = restored;
        }

        void SetCurrency(int currency)
        {
            var save = model.CaptureSave();
            save.currency = currency;
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.True, error.ToString());
            model = restored;
        }

        string Stock(string tankId, string speciesId)
        {
            Produce(speciesId);
            string id = model.Incubator.SpecimenId;
            Ok(model.TransferToTank(tankId));
            return id;
        }

        void Produce(string speciesId)
        {
            Start(speciesId);
            model.Tick(config.IncubationSeconds);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
        }

        void Start(string speciesId)
        {
            model.Tick(100f);
            var recipe = model.GetSpecies(speciesId);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation());
        }
    }
}
