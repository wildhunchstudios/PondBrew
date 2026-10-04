using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class PricingTests
    {
        static readonly string[] Tanks = { "tank-a", "tank-b" };
        GameConfig config;
        GameModel model;

        [SetUp]
        public void Setup()
        {
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
        }

        [TestCase("pondlet", 6, 18)]
        [TestCase("bubblebelly", 10, 30)]
        [TestCase("ribbonfin", 10, 30)]
        public void EachSpeciesStartsAtItsStandardPriceWithinOneToThreeTimesStandard(string id, int standard, int maximum)
        {
            var tag = model.GetPrice(id).Value;
            Assert.That(tag.SpeciesId, Is.EqualTo(id));
            Assert.That(tag.Price, Is.EqualTo(standard));
            Assert.That(tag.Standard, Is.EqualTo(standard));
            Assert.That(tag.Minimum, Is.EqualTo(1));
            Assert.That(tag.Maximum, Is.EqualTo(maximum));
            Assert.That(model.GetPrice(null), Is.Null);
            Assert.That(model.GetPrice("unknown"), Is.Null);
            Assert.That(model.CaptureSave().prices, Is.Empty);
        }

        [TestCase(null, 6, FailureCode.UnknownSpecies)]
        [TestCase("unknown", 6, FailureCode.UnknownSpecies)]
        [TestCase("bubblebelly", 10, FailureCode.UndiscoveredRecipe)]
        [TestCase("pondlet", 0, FailureCode.InvalidPrice)]
        [TestCase("pondlet", -1, FailureCode.InvalidPrice)]
        [TestCase("pondlet", 19, FailureCode.InvalidPrice)]
        [TestCase("pondlet", int.MaxValue, FailureCode.InvalidPrice)]
        [TestCase("pondlet", int.MinValue, FailureCode.InvalidPrice)]
        public void InvalidPriceChangesFailWithoutChangingAnything(string id, int price, FailureCode expected)
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 8));
            string before = Json();
            var result = model.SetPrice(id, price);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(expected));
            Assert.That(Json(), Is.EqualTo(before));
            Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(8));
            Assert.That(model.GetPrice("bubblebelly").Value.Price, Is.EqualTo(10));
        }

        [Test]
        public void BoundaryTagsAreAcceptedAndTheStandardTagLeavesNothingSaved()
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 1));
            Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(1));
            Ok(model.SetPrice("pondlet", 18));
            Ok(model.SetPrice("pondlet", 18));
            var save = model.CaptureSave();
            Assert.That(save.prices.Length, Is.EqualTo(1));
            Assert.That(save.prices[0].speciesId, Is.EqualTo("pondlet"));
            Assert.That(save.prices[0].price, Is.EqualTo(18));
            Ok(model.SetPrice("pondlet", 6));
            Assert.That(model.CaptureSave().prices, Is.Empty);
            Assert.That(model.Currency, Is.Zero);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
        }

        [Test]
        public void ReservationLocksTheAgreedPriceWhileRepricingAffectsOnlyTheNextCustomer()
        {
            string first = Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 8));
            Ok(model.ReserveTank("tank-a", "visitor-a"));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(8));
            Ok(model.SetPrice("pondlet", 3));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(8));
            Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(3));
            Ok(model.SellReservedTank("tank-a", "visitor-a", first));
            Assert.That(model.Currency, Is.EqualTo(8));

            Stock("tank-a", "pondlet");
            Ok(model.ReserveTank("tank-a", "visitor-b"));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(3));
            Ok(model.ReleaseReservation("tank-a", "visitor-b"));
            Ok(model.SetPrice("pondlet", 12));
            Ok(model.ReserveTank("tank-a", "visitor-c"));
            string second = model.GetReservation("tank-a").Value.SpecimenId;
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(12));
            Ok(model.SellReservedTank("tank-a", "visitor-c", second));
            Assert.That(model.Currency, Is.EqualTo(20));
        }

        [Test]
        public void PriceLimitRefusalLeavesNoClaimAndTheLimitIsInclusive()
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 10));
            string before = Json();
            var refused = model.ReserveTank("tank-a", "visitor-a", 9);
            Assert.That(refused.Success, Is.False);
            Assert.That(refused.Failure, Is.EqualTo(FailureCode.PriceTooHigh));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(Json(), Is.EqualTo(before));
            var negative = model.ReserveTank("tank-a", "visitor-a", -1);
            Assert.That(negative.Failure, Is.EqualTo(FailureCode.InvalidPrice));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Ok(model.ReserveTank("tank-a", "visitor-a", 10));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(10));
            Assert.That(model.GetReservation("tank-a").Value.SpecimenId, Is.EqualTo(id));
            // An existing claim is reported as reserved, whatever the second buyer's limit.
            Assert.That(model.ReserveTank("tank-a", "visitor-b", 1).Failure, Is.EqualTo(FailureCode.TankReserved));
        }

        [Test]
        public void ShortcutSaleUsesTheCurrentTag()
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 9));
            Ok(model.SellFromTank("tank-a"));
            Assert.That(model.Currency, Is.EqualTo(9));
        }

        [TestCase(6, 1, PriceReaction.Bargain)]
        [TestCase(6, 4, PriceReaction.Bargain)]
        [TestCase(6, 5, PriceReaction.Fair)]
        [TestCase(6, 6, PriceReaction.Fair)]
        [TestCase(6, 7, PriceReaction.Pricey)]
        [TestCase(6, 9, PriceReaction.Pricey)]
        [TestCase(6, 10, PriceReaction.TooExpensive)]
        [TestCase(6, 18, PriceReaction.TooExpensive)]
        [TestCase(10, 7, PriceReaction.Bargain)]
        [TestCase(10, 8, PriceReaction.Fair)]
        [TestCase(10, 10, PriceReaction.Fair)]
        [TestCase(10, 11, PriceReaction.Pricey)]
        [TestCase(10, 15, PriceReaction.Pricey)]
        [TestCase(10, 16, PriceReaction.TooExpensive)]
        [TestCase(1, 1, PriceReaction.Fair)]
        [TestCase(1, 2, PriceReaction.TooExpensive)]
        public void FirstCustomerTypeJudgesExactIntegerBands(int standard, int price, PriceReaction expected)
        {
            Assert.That(new CustomerRules().Judge(price, standard), Is.EqualTo(expected));
        }

        [Test]
        public void PriceArithmeticCannotOverflowOrAcceptNegativeInputs()
        {
            Assert.That(ShopPricing.Judge(int.MaxValue, int.MaxValue, 75, 150), Is.EqualTo(PriceReaction.Fair));
            Assert.That(ShopPricing.AcceptableLimit(int.MaxValue, 150), Is.EqualTo(int.MaxValue));
            Assert.That(ShopPricing.MaximumPrice(int.MaxValue, 1, 300), Is.EqualTo(int.MaxValue));
            Assert.That(ShopPricing.MaximumPrice(1, 5, 300), Is.EqualTo(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShopPricing.Judge(-1, 6, 75, 150));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShopPricing.Judge(6, -1, 75, 150));
        }

        [TestCase("bargain-negative")]
        [TestCase("bargain-over-standard")]
        [TestCase("tolerance-below-standard")]
        [TestCase("tolerance-over-cap")]
        public void InvalidCustomerPriceRulesAreRejected(string problem)
        {
            Stock("tank-a", "pondlet");
            var rules = new CustomerRules();
            if (problem == "bargain-negative") rules.BargainPercent = -1;
            if (problem == "bargain-over-standard") rules.BargainPercent = 101;
            if (problem == "tolerance-below-standard") rules.PriceTolerancePercent = 99;
            if (problem == "tolerance-over-cap") rules.PriceTolerancePercent = 1001;
            Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, rules));
        }

        [TestCase("minimum-zero")]
        [TestCase("maximum-below-standard")]
        [TestCase("maximum-over-cap")]
        [TestCase("value-below-minimum")]
        public void InvalidPriceConfigurationIsRejected(string problem)
        {
            if (problem == "minimum-zero") config.MinimumPrice = 0;
            if (problem == "maximum-below-standard") config.MaximumPricePercent = 99;
            if (problem == "maximum-over-cap") config.MaximumPricePercent = 1001;
            if (problem == "value-below-minimum") config.MinimumPrice = 7;
            Assert.Throws<ArgumentException>(() => new GameModel(config));
        }

        [TestCase(1, PriceReaction.Bargain)]
        [TestCase(4, PriceReaction.Bargain)]
        [TestCase(6, PriceReaction.Fair)]
        [TestCase(9, PriceReaction.Pricey)]
        public void CustomerAcceptsATagWithinToleranceAndPaysExactlyThatTag(int price, PriceReaction expected)
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", price));
            var visit = new CustomerVisit(model, Tanks);
            Queue(visit);
            Assert.That(visit.SpecimenId, Is.EqualTo(id));
            Assert.That(visit.Price, Is.EqualTo(price));
            Assert.That(visit.Reaction, Is.EqualTo(expected));
            Assert.That(visit.QuotedPrice, Is.EqualTo(price));
            Assert.That(visit.QuotedSpeciesId, Is.EqualTo("pondlet"));
            Assert.That(visit.PriceRejections, Is.Zero);
            Ok(visit.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(price));
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.Sold));
        }

        [Test]
        public void TooExpensiveOnlyDisplayIsRefusedWithoutAClaimAndTheVisitorLeaves()
        {
            string id = Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 10));
            string before = Json();
            var visit = new CustomerVisit(model, Tanks);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(visit.Reaction, Is.EqualTo(PriceReaction.TooExpensive));
            Assert.That(visit.QuotedSpeciesId, Is.EqualTo("pondlet"));
            Assert.That(visit.QuotedPrice, Is.EqualTo(10));
            Assert.That(visit.PriceRejections, Is.EqualTo(1));
            Assert.That(visit.SpecimenId, Is.Null);
            Assert.That(visit.Price, Is.Zero);
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(Json(), Is.EqualTo(before));
            Assert.That(visit.Serve(ShopDayPhase.Open).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Finished));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void RefusedDisplayRedirectsToAnotherDisplayAndPaysItsAgreedPrice()
        {
            string pondlet = Stock("tank-a", "pondlet");
            string bubblebelly = Stock("tank-b", "bubblebelly");
            Ok(model.SetPrice("pondlet", 10));
            var visit = new CustomerVisit(model, Tanks);
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-a"));
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Arriving));
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-b"));
            Assert.That(visit.PriceRejections, Is.EqualTo(1));
            Assert.That(visit.Reaction, Is.EqualTo(PriceReaction.TooExpensive));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b"), Is.Null);
            Queue(visit);
            Assert.That(visit.SpecimenId, Is.EqualTo(bubblebelly));
            Assert.That(visit.Price, Is.EqualTo(10));
            Assert.That(visit.Reaction, Is.EqualTo(PriceReaction.Fair));
            Ok(visit.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(10));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(pondlet));
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.Sold));
        }

        [Test]
        public void TwoRefusedDisplaysEndTheVisitWithoutReturningToEither()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "pondlet");
            Ok(model.SetPrice("pondlet", 12));
            var visit = new CustomerVisit(model, Tanks);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-b"));
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(visit.PriceRejections, Is.EqualTo(2));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b"), Is.Null);
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void LoweringATagAfterARefusalHelpsTheNextVisitorNotTheOneLeaving()
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 12));
            var refused = new CustomerVisit(model, Tanks);
            refused.Advance(0.1f, true, ShopDayPhase.Open);
            refused.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(refused.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Ok(model.SetPrice("pondlet", 6));
            refused.Advance(1f, false, ShopDayPhase.Open);
            Assert.That(refused.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            var next = new CustomerVisit(model, Tanks);
            Queue(next);
            Assert.That(next.Price, Is.EqualTo(6));
            Assert.That(next.Reaction, Is.EqualTo(PriceReaction.Fair));
        }

        [TestCase(18)]
        [TestCase(1)]
        public void RepricingWhileQueuedKeepsTheOrderAndPaysTheAgreedPrice(int newTag)
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 8));
            var visit = new CustomerVisit(model, Tanks);
            Queue(visit);
            Ok(model.SetPrice("pondlet", newTag));
            visit.Advance(1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.None));
            Assert.That(visit.Price, Is.EqualTo(8));
            Ok(visit.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(8));
            Assert.That(visit.Serve(ShopDayPhase.Open).Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(8));
        }

        [Test]
        public void AlternateDisplayTakenAfterARefusalStillReportsThePriceReason()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "bubblebelly");
            Ok(model.SetPrice("pondlet", 10));
            var visit = new CustomerVisit(model, Tanks);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-b"));
            Ok(model.ReserveTank("tank-b", "another-visitor"));
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b").Value.CustomerId, Is.EqualTo("another-visitor"));
        }

        [Test]
        public void LedgerCountsPriceRefusalsAsLostVisitsThroughSaveAndClose()
        {
            var cycle = new ShopDayCycle();
            Assert.That(cycle.OpenShop(), Is.True);
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 1), Is.True);
            Assert.That(ledger.BeginVisit("refuser"), Is.True);
            Assert.That(ledger.ResolveVisit("refuser", CustomerOutcome.TooExpensive, 10), Is.False, "A refusal earns nothing.");
            Assert.That(ledger.ResolveVisit("refuser", CustomerOutcome.TooExpensive), Is.True);
            Assert.That(ledger.ResolveVisit("refuser", CustomerOutcome.TooExpensive), Is.False);
            Assert.That(ledger.Current.tooExpensive, Is.EqualTo(1));
            Assert.That(ledger.Current.visitors, Is.EqualTo(1));
            Assert.That(ledger.Current.revenue, Is.Zero);
            var save = ledger.CaptureSave();
            Assert.That(save.version, Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(ShopTradeLedger.TryRestore(JsonUtility.FromJson<TradeLedgerSave>(JsonUtility.ToJson(save)),
                cycle.CaptureSave(), out var restored), Is.True);
            Assert.That(restored.Current.tooExpensive, Is.EqualTo(1));
            Assert.That(restored.FinishDay(1), Is.True);
            Assert.That(restored.Last.tooExpensive, Is.EqualTo(1));
        }

        [Test]
        public void VersionOneReportsRestoreWithoutRefusalsAndRejectInventedOnes()
        {
            var cycle = new ShopDayCycle();
            Assert.That(cycle.OpenShop(), Is.True);
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 2), Is.True);
            Assert.That(ledger.BeginVisit("buyer"), Is.True);
            Assert.That(ledger.ResolveVisit("buyer", CustomerOutcome.Sold, 6), Is.True);
            var legacy = ledger.CaptureSave();
            legacy.version = 1;
            Assert.That(ShopTradeLedger.TryRestore(legacy, cycle.CaptureSave(), out var restored), Is.True);
            Assert.That(restored.CaptureSave().version, Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(restored.Current.sales, Is.EqualTo(1));
            Assert.That(restored.Current.tooExpensive, Is.Zero);

            legacy.current.visitors++;
            legacy.current.tooExpensive = 1;
            Assert.That(ShopTradeLedger.TryRestore(legacy, cycle.CaptureSave(), out restored), Is.False);
            Assert.That(restored, Is.Null);
            legacy.version = 2;
            Assert.That(ShopTradeLedger.TryRestore(legacy, cycle.CaptureSave(), out restored), Is.True);
            Assert.That(restored.Current.tooExpensive, Is.EqualTo(1));
        }

        [Test]
        public void PriceTagsRoundTripThroughSaveDataAndCaptureCannotMutateTheModel()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "bubblebelly");
            Ok(model.SetPrice("pondlet", 9));
            Ok(model.SetPrice("bubblebelly", 7));
            var save = model.CaptureSave();
            Assert.That(save.version, Is.EqualTo(GameModel.SaveVersion));
            Assert.That(save.prices.Length, Is.EqualTo(2));
            Assert.That(save.prices[0].speciesId, Is.EqualTo("bubblebelly"));
            Assert.That(save.prices[0].price, Is.EqualTo(7));
            Assert.That(save.prices[1].speciesId, Is.EqualTo("pondlet"));
            Assert.That(save.prices[1].price, Is.EqualTo(9));
            save.prices[0].price = 30;
            Assert.That(model.GetPrice("bubblebelly").Value.Price, Is.EqualTo(7));
            var json = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(model.CaptureSave()));
            Assert.That(GameModel.TryRestore(config, json, out var restored, out var error), Is.True, error.ToString());
            Assert.That(restored.GetPrice("pondlet").Value.Price, Is.EqualTo(9));
            Assert.That(restored.GetPrice("bubblebelly").Value.Price, Is.EqualTo(7));
            Assert.That(restored.GetPrice("ribbonfin").Value.Price, Is.EqualTo(10));
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(Json()));
        }

        [TestCase("null-prices")]
        [TestCase("empty-prices")]
        public void VersionOneGameRestoresEveryTagAtItsStandardPrice(string shape)
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 9));
            var save = model.CaptureSave();
            save.version = 1;
            save.prices = shape == "null-prices" ? null : new PriceSave[0];
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.True, error.ToString());
            Assert.That(restored.GetPrice("pondlet").Value.Price, Is.EqualTo(6));
            Assert.That(restored.CaptureSave().version, Is.EqualTo(GameModel.SaveVersion));
            Assert.That(restored.CaptureSave().prices, Is.Empty);
        }

        [TestCase("tags-in-version-one")]
        [TestCase("null-entry")]
        [TestCase("null-species")]
        [TestCase("unknown-species")]
        [TestCase("undiscovered-species")]
        [TestCase("duplicate-species")]
        [TestCase("below-minimum")]
        [TestCase("above-maximum")]
        [TestCase("future-version")]
        public void InvalidPriceTagsRejectTheWholeSave(string problem)
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 9));
            var save = model.CaptureSave();
            switch (problem)
            {
                case "tags-in-version-one": save.version = 1; break;
                case "null-entry": save.prices = new PriceSave[] { null }; break;
                case "null-species": save.prices[0].speciesId = null; break;
                case "unknown-species": save.prices[0].speciesId = "unknown"; break;
                case "undiscovered-species": save.prices[0].speciesId = "ribbonfin"; break;
                case "duplicate-species": save.prices = new[] { save.prices[0], new PriceSave { speciesId = "pondlet", price = 8 } }; break;
                case "below-minimum": save.prices[0].price = 0; break;
                case "above-maximum": save.prices[0].price = 19; break;
                case "future-version": save.version = GameModel.SaveVersion + 1; break;
            }
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(error, Is.EqualTo(FailureCode.InvalidSave));
            Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(9));
        }

        string Json() => JsonUtility.ToJson(model.CaptureSave());

        string Stock(string tankId, string speciesId)
        {
            model.Tick(100f);
            var recipe = model.GetSpecies(speciesId);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation());
            model.Tick(config.IncubationSeconds);
            string id = model.Incubator.SpecimenId;
            Ok(model.TransferToTank(tankId));
            return id;
        }

        static void Queue(CustomerVisit visit)
        {
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.ToCounter), "Visitor did not accept the display.");
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
        }

        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
