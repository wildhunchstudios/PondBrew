using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class SlapTests
    {
        private GameConfig config;
        private GameModel model;
        private static readonly string[] Tanks = { "tank-a", "tank-b" };

        [SetUp] public void Setup() { config = GameConfig.CreateDefault(); model = new GameModel(config); }

        [TestCase(CustomerPhase.Arriving, ShopDayPhase.Open)]
        [TestCase(CustomerPhase.Browsing, ShopDayPhase.Open)]
        [TestCase(CustomerPhase.ToCounter, ShopDayPhase.Open)]
        [TestCase(CustomerPhase.Queued, ShopDayPhase.Open)]
        [TestCase(CustomerPhase.Arriving, ShopDayPhase.Closing)]
        [TestCase(CustomerPhase.Browsing, ShopDayPhase.Closing)]
        [TestCase(CustomerPhase.ToCounter, ShopDayPhase.Closing)]
        [TestCase(CustomerPhase.Queued, ShopDayPhase.Closing)]
        public void UnpaidSlapCancelsOnceReleasesItsClaimAndLeavesStockUnpaid(CustomerPhase phase, ShopDayPhase day)
        {
            Produce("pondlet", "tank-a");
            var visitor = AtPhase(phase);
            string before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(visitor.CanBeSlapped, Is.True);
            Assert.That(visitor.Slap(day), Is.True);
            Assert.That(visitor.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(visitor.Outcome, Is.EqualTo(CustomerOutcome.Slapped));
            Assert.That(visitor.CanBeSlapped, Is.False);
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
            Assert.That(visitor.Slap(day), Is.False);
            Assert.That(visitor.Serve(day).Failure, Is.EqualTo(FailureCode.NoCustomerReady));
            visitor.Advance(0.1f, true, ShopDayPhase.Night);
            Assert.That(visitor.Phase, Is.EqualTo(CustomerPhase.Finished));
            Assert.That(visitor.Outcome, Is.EqualTo(CustomerOutcome.Slapped));
            Assert.That(model.Currency, Is.Zero);
        }

        [TestCase(ShopDayPhase.Night)]
        [TestCase((ShopDayPhase)99)]
        public void SlapOutsideTradingPhasesLeavesTheWaitingAgreementUntouched(ShopDayPhase day)
        {
            Produce("pondlet", "tank-a");
            var visitor = AtPhase(CustomerPhase.Queued);
            string before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(visitor.Slap(day), Is.False);
            Assert.That(visitor.Phase, Is.EqualTo(CustomerPhase.Queued));
            Assert.That(visitor.Outcome, Is.EqualTo(CustomerOutcome.None));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(visitor.Id));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
        }

        [TestCase(CustomerPhase.Paid)]
        [TestCase(CustomerPhase.Leaving)]
        [TestCase(CustomerPhase.Finished)]
        public void PaidVisitorCannotBeSlappedOrHaveTheSaleChanged(CustomerPhase phase)
        {
            Produce("pondlet", "tank-a");
            var visitor = AtPhase(phase);
            string before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(visitor.CanBeSlapped, Is.False);
            Assert.That(visitor.Slap(ShopDayPhase.Open), Is.False);
            Assert.That(visitor.Slap(ShopDayPhase.Closing), Is.False);
            Assert.That(visitor.Phase, Is.EqualTo(phase));
            Assert.That(visitor.Outcome, Is.EqualTo(CustomerOutcome.Sold));
            Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
        }

        [Test]
        public void CancelledLeavingVisitorKeepsItsOriginalReason()
        {
            var visitor = new CustomerVisit(model, Tanks);
            visitor.Cancel(CustomerOutcome.PathBlocked);
            Assert.That(visitor.CanBeSlapped, Is.False);
            Assert.That(visitor.Slap(ShopDayPhase.Open), Is.False);
            Assert.That(visitor.Outcome, Is.EqualTo(CustomerOutcome.PathBlocked));
        }

        [Test]
        public void SlapReleasesOnlyItsOwnClaimWhileAnotherVisitorCanStillPay()
        {
            Produce("pondlet", "tank-a"); Produce("bubblebelly", "tank-b");
            var first = AtPhase(CustomerPhase.Queued);
            var second = AtPhase(CustomerPhase.Queued);
            Assert.That(second.TargetTankId, Is.EqualTo("tank-b"));
            Assert.That(first.Slap(ShopDayPhase.Open), Is.True);
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(model.GetReservation("tank-b").Value.CustomerId, Is.EqualTo(second.Id));
            Assert.That(first.Serve(ShopDayPhase.Open).Success, Is.False);
            Ok(second.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(10));
            Assert.That(model.GetTank("tank-a").Value.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(model.GetTank("tank-b"), Is.Null);
        }

        [Test]
        public void SlapCannotReleaseAReplacedReservationOwnedBySomeoneElse()
        {
            Produce("pondlet", "tank-a");
            var visitor = AtPhase(CustomerPhase.Queued);
            Ok(model.ReleaseReservation("tank-a", visitor.Id));
            Ok(model.ReserveTank("tank-a", "replacement"));
            Assert.That(visitor.Slap(ShopDayPhase.Open), Is.True);
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("replacement"));
            Assert.That(model.GetTank("tank-a").Value.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void AcceptedSlapLowersReputationAndRecordsOneUnpaidOutcomeAcrossReload()
        {
            Produce("pondlet", "tank-a");
            var visitor = AtPhase(CustomerPhase.Queued);
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 1), Is.True);
            Assert.That(ledger.BeginVisit(visitor.Id), Is.True);
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = 1, points = 10 }, out var reputation), Is.True);
            Assert.That(visitor.Slap(ShopDayPhase.Closing), Is.True);
            Assert.That(reputation.ResolveVisit(ledger, visitor.Id, visitor.Outcome), Is.True);
            Assert.That(reputation.Points, Is.EqualTo(9));
            Assert.That(reputation.ResolveVisit(ledger, visitor.Id, CustomerOutcome.Slapped), Is.False);
            Assert.That(reputation.ResolveVisit(ledger, visitor.Id, CustomerOutcome.Sold, 6), Is.False);
            Assert.That(ledger.Current.slapped, Is.EqualTo(1));
            Assert.That(ledger.Current.sales, Is.Zero); Assert.That(ledger.Current.revenue, Is.Zero);
            Assert.That(ledger.FinishDay(1), Is.True);
            var reportSave = JsonUtility.FromJson<TradeLedgerSave>(JsonUtility.ToJson(ledger.CaptureSave()));
            Assert.That(ShopTradeLedger.TryRestore(reportSave, Cycle(2, ShopDayPhase.Night), out var restored), Is.True);
            Assert.That(ShopReputation.TryRestore(reputation.CaptureSave(), out var restoredReputation), Is.True);
            Assert.That(restoredReputation.ResolveVisit(restored, visitor.Id, CustomerOutcome.Slapped), Is.False);
            Assert.That(restoredReputation.Points, Is.EqualTo(9));
            Assert.That(restored.Last.slapped, Is.EqualTo(1));
            Assert.That(restored.Last.stockedAtClose, Is.EqualTo(1));
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void RejectedSlapReportCannotChangeScoreAndTheFloorStillRecordsRealOutcomes()
        {
            var ledger = new ShopTradeLedger(); var reputation = new ShopReputation();
            ledger.BeginDay(1, 0); ledger.BeginVisit("waiting");
            Assert.That(reputation.ResolveVisit(ledger, "unknown", CustomerOutcome.Slapped), Is.False);
            Assert.That(reputation.ResolveVisit(ledger, "waiting", CustomerOutcome.Slapped, 1), Is.False);
            Assert.That(ledger.IsPending("waiting"), Is.True); Assert.That(ledger.Current.slapped, Is.Zero);
            Assert.That(reputation.ResolveVisit(ledger, "waiting", CustomerOutcome.Slapped), Is.True);
            Assert.That(reputation.Points, Is.Zero); Assert.That(ledger.Current.slapped, Is.EqualTo(1));
            Assert.That(ledger.Current.revenue, Is.Zero); Assert.That(ledger.HasPending, Is.False);
        }

        [Test]
        public void ReportCopiesPreserveSlapsWithoutAliasingCurrentOrLast()
        {
            var ledger = SlappedDay();
            ledger.Current.slapped = 500;
            var save = ledger.CaptureSave();
            Assert.That(save.version, Is.EqualTo(4));
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Open), out var restored), Is.True);
            save.current.slapped = 999;
            Assert.That(ledger.Current.slapped, Is.EqualTo(1));
            Assert.That(restored.Current.slapped, Is.EqualTo(1));
            ledger.FinishDay(0); ledger.Last.slapped = 99;
            ledger.CaptureSave().last.slapped = 99;
            Assert.That(ledger.Last.slapped, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EarlierReportsMigrateWithZeroSlapsButRejectCurrentLastAndHiddenSlapPayloads(int version)
        {
            var clean = new ShopTradeLedger(); clean.BeginDay(1, 0);
            var save = clean.CaptureSave(); save.version = version;
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Open), out var migrated), Is.True);
            Assert.That(migrated.Current.slapped, Is.Zero);
            Assert.That(migrated.CaptureSave().version, Is.EqualTo(4));

            var withSlap = SlappedDay(); save = withSlap.CaptureSave(); save.version = version;
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Open), out _), Is.False);
            withSlap.FinishDay(0); save = withSlap.CaptureSave(); save.version = version;
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(2, ShopDayPhase.Night), out _), Is.False);
            save = new TradeLedgerSave { version = version, current = new TradeDaySave { slapped = 1 } };
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Night), out _), Is.False);
            save.current = null; save.last = new TradeDaySave { slapped = 1 };
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Night), out _), Is.False);
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(ShopTradeLedger.MaxCounter + 1)]
        public void InvalidSlapCountsCannotHideOutsideVisitorTotals(int count)
        {
            var save = SlappedDay().CaptureSave(); save.current.slapped = count;
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Open), out _), Is.False);
        }

        [Test]
        public void CurrentReportRejectsHiddenSlapsEvenWhenNoDayIsPresent()
        {
            var save = new TradeLedgerSave { current = new TradeDaySave { slapped = 1 } };
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Night), out _), Is.False);
            save.current = null; save.last = new TradeDaySave { slapped = 1 };
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Night), out _), Is.False);
        }

        [Test]
        public void CooldownRequiresThreeActiveSecondsAndRejectedStartsDoNotExtendIt()
        {
            var cooldown = new SlapCooldown();
            Assert.That(cooldown.Remaining, Is.Zero); Assert.That(cooldown.TryStart(), Is.True);
            cooldown.Tick(2.5f); Assert.That(cooldown.Remaining, Is.EqualTo(0.5f));
            Assert.That(cooldown.TryStart(), Is.False); Assert.That(cooldown.Remaining, Is.EqualTo(0.5f));
            cooldown.Tick(0.5f); Assert.That(cooldown.Remaining, Is.Zero);
            Assert.That(cooldown.TryStart(), Is.True); Assert.That(cooldown.Remaining, Is.EqualTo(3f));
            cooldown.Tick(float.MaxValue); Assert.That(cooldown.Remaining, Is.Zero);
        }

        [Test]
        public void InvalidElapsedTimesCannotShortenOrPoisonCooldown()
        {
            var cooldown = new SlapCooldown(); cooldown.TryStart(); cooldown.Tick(1f);
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                cooldown.Tick(delta);
                Assert.That(cooldown.Remaining, Is.EqualTo(2f), delta.ToString());
                Assert.That(cooldown.TryStart(), Is.False);
            }
        }

        [Test]
        public void BindingRequiresOneUppercaseLetterAndReservesMovementAndInteraction()
        {
            foreach (char letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
                Assert.That(SlapRules.IsAllowedKey(letter.ToString()), Is.EqualTo("ADESW".IndexOf(letter) < 0), letter.ToString());
            foreach (string key in new[] { null, "", "f", " F", "F ", "FF", "Escape", "1", "[", "É" })
                Assert.That(SlapRules.IsAllowedKey(key), Is.False, key);
        }

        private CustomerVisit AtPhase(CustomerPhase phase)
        {
            var visitor = new CustomerVisit(model, Tanks);
            if (phase == CustomerPhase.Arriving) return visitor;
            visitor.Advance(0.1f, true, ShopDayPhase.Open);
            if (phase == CustomerPhase.Browsing) return visitor;
            visitor.Advance(2f, false, ShopDayPhase.Open);
            if (phase == CustomerPhase.ToCounter) return visitor;
            visitor.Advance(0.1f, true, ShopDayPhase.Open);
            if (phase == CustomerPhase.Queued) return visitor;
            Ok(visitor.Serve(ShopDayPhase.Open));
            if (phase == CustomerPhase.Paid) return visitor;
            visitor.Advance(1.5f, false, ShopDayPhase.Open);
            if (phase == CustomerPhase.Leaving) return visitor;
            visitor.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(visitor.Phase, Is.EqualTo(phase));
            return visitor;
        }

        private static ShopTradeLedger SlappedDay()
        {
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 0), Is.True);
            Assert.That(ledger.BeginVisit("slapped"), Is.True);
            Assert.That(ledger.ResolveVisit("slapped", CustomerOutcome.Slapped), Is.True);
            return ledger;
        }

        private static ShopDaySave Cycle(int day, ShopDayPhase phase) => new ShopDaySave
        {
            dayNumber = day, phase = phase,
            secondsRemaining = phase == ShopDayPhase.Open ? 480f : phase == ShopDayPhase.Closing ? 3f : 0f
        };

        private void Produce(string species, string tank)
        {
            model.Tick(100f); var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Ok(model.TransferToTank(tank));
        }
        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
