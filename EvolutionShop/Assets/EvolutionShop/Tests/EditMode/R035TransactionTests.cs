using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>Cross-system transaction regressions; simulated durations are not pacing evidence.</summary>
    public sealed class R035TransactionTests
    {
        private GameConfig config;
        private GameModel model;
        private ShopDayCycle cycle;
        private ShopTradeLedger ledger;
        private ShopReputation reputation;
        private static readonly string[] Tanks = { "tank-a", "tank-b" };

        [SetUp]
        public void Setup()
        {
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
            cycle = new ShopDayCycle();
            ledger = new ShopTradeLedger();
            reputation = new ShopReputation();
        }

        [Test]
        public void RepeatedCustomerPaymentsCanSpendBackToZeroAndRecoverAfterReloadWithoutSeededStock()
        {
            string before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(model.BuyUpgrade().Failure, Is.EqualTo(FailureCode.InsufficientFunds));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
            OpenDay(0);
            var specimenIds = new HashSet<string>();

            // Four standard Pondlet sales fund the default 24-credit upgrade exactly.
            // Production uses collected cultures and real recipe commands, with explicit simulation ticks.
            for (int i = 0; i < 4; i++)
            {
                string tank = Tanks[i % Tanks.Length];
                ProducePondlet(tank);
                Assert.That(ledger.RecordRestock(), Is.True);
                Assert.That(specimenIds.Add(model.GetTank(tank).Value.Id), Is.True, "Each batch must create a new specimen.");
                var customer = BeginVisitor(); Queue(customer);
                Assert.That(customer.Price, Is.EqualTo(6));
                ServeAndRecordOnce(customer);
                Assert.That(model.Currency, Is.EqualTo((i + 1) * 6));
                Assert.That(model.GetTank(tank), Is.Null);
                Assert.That(model.GetReservation(tank), Is.Null);
                Assert.That(ledger.Current.sales, Is.EqualTo(i + 1));
                Assert.That(reputation.Points, Is.EqualTo((i + 1) * 2));
            }

            Assert.That(model.Currency, Is.EqualTo(config.UpgradeCost));
            Ok(model.BuyUpgrade());
            Assert.That(model.Currency, Is.Zero);
            Assert.That(model.UpgradeOwned, Is.True);
            Assert.That(model.ReservedCultureCount, Is.Zero);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing));

            // A stockout at zero cash costs neither money nor reputation and leaves production usable.
            var emptyVisit = BeginVisitor();
            emptyVisit.Advance(.1f, true, cycle.Phase);
            emptyVisit.Advance(emptyVisit.Archetype.BrowseSeconds, false, cycle.Phase);
            Assert.That(emptyVisit.Outcome, Is.EqualTo(CustomerOutcome.NoStock));
            Assert.That(reputation.ResolveVisit(ledger, emptyVisit.Id, emptyVisit.Outcome), Is.True);
            Assert.That(model.Currency, Is.Zero); Assert.That(reputation.Points, Is.EqualTo(8));
            Assert.That(ledger.HasPending, Is.False);

            RoundTripAllAuthorities();
            Assert.That(model.Currency, Is.Zero); Assert.That(model.UpgradeOwned, Is.True);
            Assert.That(ledger.Current.sales, Is.EqualTo(4)); Assert.That(ledger.Current.revenue, Is.EqualTo(24));
            Assert.That(ledger.Current.noStock, Is.EqualTo(1)); Assert.That(reputation.Points, Is.EqualTo(8));
            Assert.That(reputation.ResolveVisit(ledger, emptyVisit.Id, CustomerOutcome.NoStock), Is.False);

            ProducePondlet("tank-a");
            Assert.That(ledger.RecordRestock(), Is.True);
            Assert.That(specimenIds.Add(model.GetTank("tank-a").Value.Id), Is.True);
            var recovery = BeginVisitor(); Queue(recovery); ServeAndRecordOnce(recovery);
            Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(reputation.Points, Is.EqualTo(10)); Assert.That(reputation.Tier, Is.EqualTo(1));
            FinishDay();
            var report = ledger.Last;
            Assert.That(report.visitors, Is.EqualTo(6)); Assert.That(report.sales, Is.EqualTo(5));
            Assert.That(report.noStock, Is.EqualTo(1)); Assert.That(report.revenue, Is.EqualTo(30));
            Assert.That(report.stockedAtOpen, Is.Zero); Assert.That(report.restocked, Is.EqualTo(5));
            Assert.That(report.stockedAtClose, Is.Zero);
            Assert.That(report.revenue - config.UpgradeCost, Is.EqualTo(model.Currency));
            Assert.That(model.DiscoveredSpeciesIds, Is.EqualTo(new[] { "pondlet" }));
        }

        [Test]
        public void ConcurrentSameSpeciesOrdersKeepDifferentAgreedPricesThroughRepricingAndClosing()
        {
            ProducePondlet("tank-a"); ProducePondlet("tank-b");
            OpenDay(2);
            Ok(model.SetPrice("pondlet", 9));
            var first = BeginVisitor(); Queue(first);
            Assert.That(first.TargetTankId, Is.EqualTo("tank-a"));
            Ok(model.SetPrice("pondlet", 4));
            var second = BeginVisitor(); Queue(second);
            Assert.That(second.TargetTankId, Is.EqualTo("tank-b"));
            Assert.That(first.SpecimenId, Is.Not.EqualTo(second.SpecimenId));

            Ok(model.SetPrice("pondlet", 18));
            Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(18));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(9));
            Assert.That(model.GetReservation("tank-b").Value.Price, Is.EqualTo(4));
            Assert.That(cycle.RequestClose(), Is.True);
            first.Advance(.1f, true, cycle.Phase); second.Advance(.1f, true, cycle.Phase);
            Assert.That(first.Phase, Is.EqualTo(CustomerPhase.Queued));
            Assert.That(second.Phase, Is.EqualTo(CustomerPhase.Queued));

            ServeAndRecordOnce(first);
            Assert.That(model.Currency, Is.EqualTo(9));
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo(second.SpecimenId));
            Assert.That(model.GetReservation("tank-b").Value.CustomerId, Is.EqualTo(second.Id));
            Assert.That(model.GetReservation("tank-b").Value.Price, Is.EqualTo(4));
            Assert.That(ledger.IsPending(second.Id), Is.True);
            ServeAndRecordOnce(second);
            Assert.That(model.Currency, Is.EqualTo(13)); Assert.That(reputation.Points, Is.EqualTo(4));
            Assert.That(ledger.HasPending, Is.False);
            Assert.That(model.GetTank("tank-a"), Is.Null); Assert.That(model.GetTank("tank-b"), Is.Null);

            FinishDay(); RoundTripAllAuthorities();
            Assert.That(model.Currency, Is.EqualTo(13)); Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(18));
            Assert.That(ledger.Last.sales, Is.EqualTo(2)); Assert.That(ledger.Last.visitors, Is.EqualTo(2));
            Assert.That(ledger.Last.revenue, Is.EqualTo(13)); Assert.That(ledger.Last.closed, Is.Zero);
            Assert.That(reputation.Points, Is.EqualTo(4));
            Assert.That(reputation.ResolveVisit(ledger, first.Id, CustomerOutcome.Sold, first.Price), Is.False);
            Assert.That(reputation.ResolveVisit(ledger, second.Id, CustomerOutcome.Sold, second.Price), Is.False);
        }

        private void OpenDay(int stocked)
        {
            Assert.That(cycle.OpenShop(), Is.True);
            Assert.That(ledger.BeginDay(cycle.DayNumber, stocked), Is.True);
        }

        private CustomerVisit BeginVisitor()
        {
            var type = CustomerArchetypes.Find(reputation.ApplyTo(CustomerArchetypes.CreateDefault()), CustomerArchetypes.Casual);
            var visitor = new CustomerVisit(model, Tanks, null, type);
            Assert.That(ledger.BeginVisit(visitor.Id), Is.True);
            return visitor;
        }

        private void Queue(CustomerVisit visitor)
        {
            visitor.Advance(.1f, true, cycle.Phase);
            visitor.Advance(visitor.Archetype.BrowseSeconds, false, cycle.Phase);
            visitor.Advance(.1f, true, cycle.Phase);
            Assert.That(visitor.Phase, Is.EqualTo(CustomerPhase.Queued));
        }

        private void ServeAndRecordOnce(CustomerVisit visitor)
        {
            Ok(visitor.Serve(cycle.Phase));
            Assert.That(reputation.ResolveVisit(ledger, visitor.Id, visitor.Outcome, visitor.Price), Is.True);
            string state = JsonUtility.ToJson(model.CaptureSave());
            int points = reputation.Points;
            string report = JsonUtility.ToJson(ledger.CaptureSave());
            Assert.That(visitor.Serve(cycle.Phase).Success, Is.False);
            Assert.That(reputation.ResolveVisit(ledger, visitor.Id, CustomerOutcome.Sold, visitor.Price), Is.False);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(state));
            Assert.That(JsonUtility.ToJson(ledger.CaptureSave()), Is.EqualTo(report));
            Assert.That(reputation.Points, Is.EqualTo(points));
        }

        private void ProducePondlet(string tank)
        {
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            if (model.IsDiscovered("pondlet")) Ok(model.UseKnownRecipe("pondlet"));
            else { Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation());
            model.Tick(config.IncubationSeconds);
            Ok(model.TransferToTank(tank));
        }

        private void FinishDay()
        {
            if (cycle.Phase == ShopDayPhase.Open) Assert.That(cycle.RequestClose(), Is.True);
            cycle.Tick(ShopDayCycle.DefaultClosingSeconds);
            Assert.That(cycle.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(ledger.FinishDay(0), Is.True);
        }

        private void RoundTripAllAuthorities()
        {
            var gameSave = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(model.CaptureSave()));
            var daySave = JsonUtility.FromJson<ShopDaySave>(JsonUtility.ToJson(cycle.CaptureSave()));
            var reportSave = JsonUtility.FromJson<TradeLedgerSave>(JsonUtility.ToJson(ledger.CaptureSave()));
            var reputationSave = JsonUtility.FromJson<ReputationSave>(JsonUtility.ToJson(reputation.CaptureSave()));
            Assert.That(GameModel.TryRestore(config, gameSave, out model, out var reason), Is.True, reason.ToString());
            Assert.That(ShopDayCycle.TryRestore(daySave, out cycle), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(reportSave, daySave, out ledger), Is.True);
            Assert.That(ShopReputation.TryRestore(reputationSave, out reputation), Is.True);
            Assert.That(ledger.CancelPendingForReload(), Is.False, "Only completed visits were saved in this fixture.");
        }

        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
