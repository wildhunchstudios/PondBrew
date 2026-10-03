using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ReputationTests
    {
        static ShopReputation At(int points)
        {
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = 1, points = points }, out var reputation), Is.True);
            return reputation;
        }

        static ShopTradeLedger OpenLedger()
        {
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 2), Is.True);
            return ledger;
        }

        [Test]
        public void AcceptedPaymentScoresOnceAndUnknownOrRepeatedVisitorsDoNot()
        {
            var reputation = new ShopReputation();
            var ledger = OpenLedger();
            Assert.That(reputation.ResolveVisit(null, "visitor", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(reputation.ResolveVisit(ledger, "unknown", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(ledger.BeginVisit("visitor"), Is.True);
            Assert.That(reputation.ResolveVisit(ledger, "VISITOR", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(reputation.Points, Is.Zero);
            Assert.That(reputation.ResolveVisit(ledger, "visitor", CustomerOutcome.Sold, 6), Is.True);
            Assert.That(reputation.Points, Is.EqualTo(2));
            Assert.That(reputation.ResolveVisit(ledger, "visitor", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(reputation.ResolveVisit(ledger, "visitor", CustomerOutcome.Impatient), Is.False);
            Assert.That(reputation.Points, Is.EqualTo(2));
            Assert.That(ledger.Current.sales, Is.EqualTo(1));
            Assert.That(ledger.Current.revenue, Is.EqualTo(6));
        }

        [TestCase(CustomerOutcome.Sold, 12)]
        [TestCase(CustomerOutcome.TooExpensive, 9)]
        [TestCase(CustomerOutcome.Impatient, 9)]
        [TestCase(CustomerOutcome.NoStock, 10)]
        [TestCase(CustomerOutcome.Closed, 10)]
        [TestCase(CustomerOutcome.PathBlocked, 10)]
        [TestCase(CustomerOutcome.OrderLost, 10)]
        public void OnlyTheAcceptedTerminalOutcomeDeterminesTheScore(CustomerOutcome outcome, int expected)
        {
            var reputation = At(10);
            var ledger = OpenLedger();
            ledger.BeginVisit("visitor");
            Assert.That(reputation.ResolveVisit(ledger, "visitor", outcome, outcome == CustomerOutcome.Sold ? 6 : 0), Is.True);
            Assert.That(reputation.Points, Is.EqualTo(expected));
            Assert.That(ledger.HasPending, Is.False);
        }

        [TestCase(CustomerOutcome.None, 0)]
        [TestCase((CustomerOutcome)99, 0)]
        [TestCase(CustomerOutcome.Sold, -1)]
        [TestCase(CustomerOutcome.Impatient, 6)]
        public void RejectedOutcomeLeavesReputationAndPendingLedgerUnchanged(CustomerOutcome outcome, int price)
        {
            var reputation = At(10);
            var ledger = OpenLedger();
            ledger.BeginVisit("visitor");
            string before = JsonUtility.ToJson(ledger.CaptureSave());
            Assert.That(reputation.ResolveVisit(ledger, "visitor", outcome, price), Is.False);
            Assert.That(reputation.Points, Is.EqualTo(10));
            Assert.That(JsonUtility.ToJson(ledger.CaptureSave()), Is.EqualTo(before));
            Assert.That(reputation.ResolveVisit(ledger, "visitor", CustomerOutcome.Sold, 6), Is.True);
            Assert.That(reputation.Points, Is.EqualTo(12));
        }

        [Test]
        public void LedgerRevenueRejectionCannotGrantReputationOrConsumePendingVisit()
        {
            var cycle = new ShopDayCycle();
            cycle.OpenShop();
            var save = new TradeLedgerSave
            {
                hasCurrent = true,
                current = new TradeDaySave { dayNumber = 1, visitors = 1000, sales = 999, revenue = ShopTradeLedger.MaxRevenue },
                pendingCustomerIds = new[] { "visitor" }
            };
            Assert.That(ShopTradeLedger.TryRestore(save, cycle.CaptureSave(), out var ledger), Is.True);
            var reputation = At(10);
            Assert.That(reputation.ResolveVisit(ledger, "visitor", CustomerOutcome.Sold, 1), Is.False);
            Assert.That(reputation.Points, Is.EqualTo(10));
            Assert.That(ledger.IsPending("visitor"), Is.True);
            Assert.That(ledger.Current.sales, Is.EqualTo(999));
        }

        [Test]
        public void ReputationClampsWithoutPreventingRealOutcomesAtEitherBoundary()
        {
            var ledger = OpenLedger();
            var reputation = new ShopReputation();
            ledger.BeginVisit("low");
            Assert.That(reputation.ResolveVisit(ledger, "low", CustomerOutcome.Impatient), Is.True);
            Assert.That(reputation.Points, Is.Zero);
            reputation = At(99);
            ledger.BeginVisit("high");
            Assert.That(reputation.ResolveVisit(ledger, "high", CustomerOutcome.Sold, 6), Is.True);
            Assert.That(reputation.Points, Is.EqualTo(100));
            ledger.BeginVisit("capped");
            Assert.That(reputation.ResolveVisit(ledger, "capped", CustomerOutcome.Sold, 6), Is.True);
            Assert.That(reputation.Points, Is.EqualTo(100));
            Assert.That(ledger.Current.sales, Is.EqualTo(2));
            Assert.That(ledger.Current.revenue, Is.EqualTo(12));
            ledger.BeginVisit("lost");
            reputation.ResolveVisit(ledger, "lost", CustomerOutcome.TooExpensive);
            Assert.That(reputation.Points, Is.EqualTo(99));
        }

        [TestCase(0, 0, "New shop", 10, 6f, 0, 0)]
        [TestCase(9, 0, "New shop", 10, 6f, 0, 0)]
        [TestCase(10, 1, "Getting known", 30, 5f, 10, 2)]
        [TestCase(29, 1, "Getting known", 30, 5f, 10, 2)]
        [TestCase(30, 2, "Local favorite", 100, 4f, 20, 4)]
        [TestCase(100, 2, "Local favorite", 100, 4f, 20, 4)]
        public void TierBoundariesExposeTheCappedDemandContract(int points, int tier, string name, int next, float arrival, int premium, int budget)
        {
            var reputation = At(points);
            Assert.That(reputation.Tier, Is.EqualTo(tier));
            Assert.That(reputation.TierName, Is.EqualTo(name));
            Assert.That(reputation.NextThreshold, Is.EqualTo(next));
            Assert.That(reputation.ArrivalSeconds, Is.EqualTo(arrival));
            Assert.That(reputation.PremiumPercent, Is.EqualTo(premium));
            Assert.That(reputation.BudgetBonus, Is.EqualTo(budget));
        }

        [TestCase(0, 40, 30, 20, 10)]
        [TestCase(10, 35, 25, 25, 15)]
        [TestCase(30, 30, 20, 25, 25)]
        public void DefaultWeightDistributionRemainsBounded(int points, int casual, int bargain, int collector, int premium)
        {
            var adjusted = At(points).ApplyTo(CustomerArchetypes.CreateDefault());
            Assert.That(Array.ConvertAll(adjusted, type => type.Weight), Is.EqualTo(new[] { casual, bargain, collector, premium }));
        }

        [Test]
        public void ApplyingDemandPreservesDisabledAndUnknownTypesAndCapsArithmetic()
        {
            var catalog = CustomerArchetypes.CreateDefault();
            catalog[0].Weight = 0;
            catalog[1].Id = "custom";
            catalog[1].Weight = 7;
            catalog[1].TolerancePercent = 995;
            catalog[1].FavoriteTolerancePercent = 1000;
            catalog[1].Budget = int.MaxValue - 1;
            var adjusted = At(100).ApplyTo(catalog);
            Assert.That(adjusted[0].Weight, Is.Zero);
            Assert.That(adjusted[1].Weight, Is.EqualTo(7));
            Assert.That(adjusted[1].TolerancePercent, Is.EqualTo(1000));
            Assert.That(adjusted[1].FavoriteTolerancePercent, Is.EqualTo(1000));
            Assert.That(adjusted[1].Budget, Is.EqualTo(int.MaxValue));
            Assert.That(adjusted[1].Problem(), Is.Null);
        }

        [Test]
        public void PreviewUsesAdditiveToleranceWithIntegerFloorsAndAHardBudget()
        {
            var adjusted = At(30).ApplyTo(CustomerArchetypes.CreateDefault());
            var casual = adjusted[0];
            Assert.That(casual.AcceptableLimit("pondlet", 6), Is.EqualTo(10));
            Assert.That(casual.Judge("pondlet", 10, 6), Is.EqualTo(PriceReaction.Pricey));
            Assert.That(casual.Judge("pondlet", 11, 6), Is.EqualTo(PriceReaction.TooExpensive));
            var collector = adjusted[2];
            Assert.That(collector.AcceptableLimit("ribbonfin", 10), Is.EqualTo(27));
            Assert.That(collector.AcceptableLimit("pondlet", 6), Is.EqualTo(7));
            Assert.That(adjusted[1].AcceptableLimit("ribbonfin", 10), Is.EqualTo(12), "Bargain budget caps its 130% tolerance.");
            Assert.That(adjusted[1].Judge("ribbonfin", 13, 10), Is.EqualTo(PriceReaction.TooExpensive));
        }

        [Test]
        public void RepeatedAdjustmentAndTierChangesDoNotMutateBaseOrExistingSnapshots()
        {
            var catalog = CustomerArchetypes.CreateDefault();
            var reputation = At(29);
            var first = reputation.ApplyTo(catalog);
            var second = reputation.ApplyTo(catalog);
            Assert.That(first[0].TolerancePercent, Is.EqualTo(160));
            Assert.That(second[0].TolerancePercent, Is.EqualTo(160), "Apply each refresh to the base definitions.");
            first[2].FavoriteSpecies[0] = "pondlet";
            first[0].Budget = 1;
            Assert.That(catalog[0].Budget, Is.EqualTo(30));
            Assert.That(catalog[0].TolerancePercent, Is.EqualTo(150));
            Assert.That(catalog[2].FavoriteSpecies[0], Is.EqualTo("bubblebelly"));
            Assert.That(second[2].FavoriteSpecies[0], Is.EqualTo("bubblebelly"));
            var ledger = OpenLedger();
            ledger.BeginVisit("tier-up");
            reputation.ResolveVisit(ledger, "tier-up", CustomerOutcome.Sold, 6);
            Assert.That(reputation.Tier, Is.EqualTo(2));
            Assert.That(second[0].TolerancePercent, Is.EqualTo(160), "Existing visitor snapshots keep their prior terms.");
            Assert.That(reputation.ApplyTo(catalog)[0].TolerancePercent, Is.EqualTo(170));
        }

        [Test]
        public void SaveCopiesRoundTripWithoutReawardingAPaidVisit()
        {
            var reputation = At(8);
            var ledger = OpenLedger();
            ledger.BeginVisit("paid"); ledger.BeginVisit("unpaid");
            reputation.ResolveVisit(ledger, "paid", CustomerOutcome.Sold, 6);
            var saved = reputation.CaptureSave();
            Assert.That(ShopReputation.TryRestore(JsonUtility.FromJson<ReputationSave>(JsonUtility.ToJson(saved)), out var restored), Is.True);
            saved.points = 100;
            Assert.That(reputation.Points, Is.EqualTo(10));
            Assert.That(restored.Points, Is.EqualTo(10));
            var cycle = new ShopDayCycle(); cycle.OpenShop();
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), cycle.CaptureSave(), out var restoredLedger), Is.True);
            Assert.That(restored.ResolveVisit(restoredLedger, "paid", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(restored.ResolveVisit(restoredLedger, "unpaid", CustomerOutcome.OrderLost), Is.True);
            Assert.That(restored.Points, Is.EqualTo(10));
            Assert.That(restoredLedger.Current.sales, Is.EqualTo(1));
            Assert.That(restoredLedger.Current.interrupted, Is.EqualTo(1));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(2, 0)]
        [TestCase(1, -1)]
        [TestCase(1, 101)]
        [TestCase(1, int.MaxValue)]
        public void InvalidSaveDoesNotProduceAReputationObject(int version, int points)
        {
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = version, points = points }, out var reputation), Is.False);
            Assert.That(reputation, Is.Null);
        }

        [Test]
        public void MissingOrDefaultSerializedReputationIsRejected()
        {
            Assert.That(ShopReputation.TryRestore(null, out _), Is.False);
            Assert.That(new ReputationSave().version, Is.Zero);
            Assert.That(ShopReputation.TryRestore(JsonUtility.FromJson<ReputationSave>("{}"), out _), Is.False);
            Assert.That(ShopReputation.TryRestore(JsonUtility.FromJson<ReputationSave>("{\"points\":10}"), out _), Is.False);
            Assert.That(new ShopReputation().CaptureSave().version, Is.EqualTo(ShopReputation.SaveVersion));
        }

        [Test]
        public void InvalidCatalogCannotBeSilentlyAdjusted()
        {
            var reputation = new ShopReputation();
            Assert.Throws<ArgumentException>(() => reputation.ApplyTo(null));
            Assert.Throws<ArgumentException>(() => reputation.ApplyTo(new CustomerArchetype[0]));
            var catalog = CustomerArchetypes.CreateDefault();
            catalog[0].TolerancePercent = int.MaxValue;
            Assert.Throws<ArgumentException>(() => reputation.ApplyTo(catalog));
        }
    }
}
