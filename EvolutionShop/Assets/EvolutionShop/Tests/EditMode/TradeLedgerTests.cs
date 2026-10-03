using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class TradeLedgerTests
    {
        ShopTradeLedger ledger;

        [SetUp] public void Setup() { ledger = new ShopTradeLedger(); }

        [Test]
        public void FreshNightHasNoInventedHistoryOrActivity()
        {
            Assert.That(ledger.Current, Is.Null);
            Assert.That(ledger.Last, Is.Null);
            Assert.That(ledger.LastDayNumber, Is.Zero);
            Assert.That(ledger.HasPending, Is.False);
            AtomicFailure(() => ledger.BeginVisit("visitor"));
            AtomicFailure(() => ledger.ResolveVisit("visitor", CustomerOutcome.Sold, 6));
            AtomicFailure(() => ledger.RecordRestock());
            AtomicFailure(() => ledger.FinishDay(0));
            AtomicFailure(() => ledger.CancelPendingForReload());
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(1, ShopDayPhase.Night), out _), Is.True);
        }

        [Test]
        public void DayReportConnectsSalesStockoutsRestockingAndUnpaidClosing()
        {
            Assert.That(ledger.BeginDay(1, 2), Is.True);
            Resolve("first", CustomerOutcome.Sold, 6);
            Resolve("second", CustomerOutcome.NoStock);
            Assert.That(ledger.RecordRestock(), Is.True);
            Resolve("third", CustomerOutcome.Sold, 10);
            Assert.That(ledger.BeginVisit("fourth"), Is.True);
            Assert.That(ledger.FinishDay(1), Is.True);
            var report = ledger.Last;
            Assert.That(ledger.Current, Is.Null);
            Assert.That(ledger.HasPending, Is.False);
            Assert.That(report.dayNumber, Is.EqualTo(1));
            Assert.That(report.partial, Is.False);
            Assert.That(report.visitors, Is.EqualTo(4));
            Assert.That(report.sales, Is.EqualTo(2));
            Assert.That(report.revenue, Is.EqualTo(16L));
            Assert.That(report.noStock, Is.EqualTo(1));
            Assert.That(report.closed, Is.EqualTo(1));
            Assert.That(report.interrupted, Is.Zero);
            Assert.That(report.stockedAtOpen, Is.EqualTo(2));
            Assert.That(report.stockedAtClose, Is.EqualTo(1));
            Assert.That(report.restocked, Is.EqualTo(1));
            AtomicFailure(() => ledger.ResolveVisit("fourth", CustomerOutcome.Closed));
            AtomicFailure(() => ledger.FinishDay(0));
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(2, ShopDayPhase.Night), out _), Is.True);
        }

        [Test]
        public void VisitOutcomeRequiresTheExactPendingIdAndCannotBeCountedTwice()
        {
            ledger.BeginDay(1, 2);
            Assert.That(ledger.BeginVisit("visitor-a"), Is.True);
            AtomicFailure(() => ledger.BeginVisit("visitor-a"));
            AtomicFailure(() => ledger.BeginVisit("visitor-b"));
            AtomicFailure(() => ledger.ResolveVisit("VISITOR-A", CustomerOutcome.Sold, 6));
            AtomicFailure(() => ledger.ResolveVisit(null, CustomerOutcome.Sold, 6));
            Assert.That(ledger.ResolveVisit("visitor-a", CustomerOutcome.Sold, 6), Is.True);
            AtomicFailure(() => ledger.ResolveVisit("visitor-a", CustomerOutcome.Sold, 6));
            AtomicFailure(() => ledger.ResolveVisit("visitor-a", CustomerOutcome.Closed));
            Assert.That(ledger.Current.visitors, Is.EqualTo(1));
            Assert.That(ledger.Current.sales, Is.EqualTo(1));
            Assert.That(ledger.Current.revenue, Is.EqualTo(6L));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t\r\n")]
        public void InvalidVisitIdentityDoesNotChangeCounters(string id)
        {
            ledger.BeginDay(1, 0);
            AtomicFailure(() => ledger.BeginVisit(id));
        }

        [Test]
        public void CustomerIdentityHasABoundedLength()
        {
            ledger.BeginDay(1, 0);
            AtomicFailure(() => ledger.BeginVisit(new string('a', 129)));
            string id = new string('a', 128);
            Assert.That(ledger.BeginVisit(id), Is.True);
            Assert.That(ledger.ResolveVisit(id, CustomerOutcome.NoStock), Is.True);
        }

        [Test]
        public void OutcomeReasonsAreSeparateAndNeverImplyRevenue()
        {
            ledger.BeginDay(1, 0);
            Resolve("no-stock", CustomerOutcome.NoStock);
            Resolve("impatient", CustomerOutcome.Impatient);
            Resolve("closed", CustomerOutcome.Closed);
            Resolve("interrupted", CustomerOutcome.OrderLost);
            Resolve("path", CustomerOutcome.PathBlocked);
            var report = ledger.Current;
            Assert.That(report.visitors, Is.EqualTo(5));
            Assert.That(report.noStock, Is.EqualTo(1));
            Assert.That(report.impatient, Is.EqualTo(1));
            Assert.That(report.closed, Is.EqualTo(1));
            Assert.That(report.interrupted, Is.EqualTo(1));
            Assert.That(report.pathProblems, Is.EqualTo(1));
            Assert.That(report.sales, Is.Zero);
            Assert.That(report.revenue, Is.Zero);
        }

        [Test]
        public void InvalidOutcomeOrPriceDoesNotConsumeThePendingVisit()
        {
            ledger.BeginDay(1, 1); ledger.BeginVisit("visitor");
            AtomicFailure(() => ledger.ResolveVisit("visitor", CustomerOutcome.None));
            AtomicFailure(() => ledger.ResolveVisit("visitor", (CustomerOutcome)999));
            AtomicFailure(() => ledger.ResolveVisit("visitor", CustomerOutcome.Sold, -1));
            AtomicFailure(() => ledger.ResolveVisit("visitor", CustomerOutcome.Closed, 6));
            Assert.That(ledger.ResolveVisit("visitor", CustomerOutcome.Sold, 0), Is.True);
            Assert.That(ledger.Current.sales, Is.EqualTo(1));
            Assert.That(ledger.Current.revenue, Is.Zero);
        }

        [Test]
        public void RestoredPendingVisitIsInterruptedOnceWithoutLosingPaidClosingIncome()
        {
            ledger.BeginDay(1, 2);
            Resolve("paid", CustomerOutcome.Sold, 10);
            ledger.BeginVisit("unpaid");
            string json = Snapshot();
            var saved = JsonUtility.FromJson<TradeLedgerSave>(json);
            Assert.That(ShopTradeLedger.TryRestore(saved, Cycle(1, ShopDayPhase.Closing), out var restored), Is.True);
            Assert.That(restored.HasPending, Is.True, "Reading alone must not resolve a visitor.");
            Assert.That(ledger.HasPending, Is.True, "Restoring must not alter the still-live ledger.");
            ledger = restored;
            Assert.That(ledger.CancelPendingForReload(), Is.True);
            AtomicFailure(() => ledger.CancelPendingForReload());
            AtomicFailure(() => ledger.ResolveVisit("unpaid", CustomerOutcome.Closed));
            Assert.That(ledger.Current.visitors, Is.EqualTo(2));
            Assert.That(ledger.Current.sales, Is.EqualTo(1));
            Assert.That(ledger.Current.revenue, Is.EqualTo(10L));
            Assert.That(ledger.Current.interrupted, Is.EqualTo(1));
            Assert.That(ledger.FinishDay(1), Is.True);
            Assert.That(ledger.Last.closed, Is.Zero);
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(2, ShopDayPhase.Night), out restored), Is.True);
            Assert.That(restored.CancelPendingForReload(), Is.False);
            Assert.That(restored.Last.revenue, Is.EqualTo(10L));
            Assert.That(restored.Last.interrupted, Is.EqualTo(1));
        }

        [Test]
        public void TwoDaysKeepTheLastCompleteReportWhileCurrentDayRemainsSeparate()
        {
            ledger.BeginDay(1, 1); Resolve("day-one", CustomerOutcome.Sold, 6); ledger.FinishDay(0);
            var firstReport = ledger.Last;
            AtomicFailure(() => ledger.BeginDay(1, 1));
            AtomicFailure(() => ledger.BeginDay(3, 1));
            Assert.That(ledger.BeginDay(2, 1), Is.True);
            Assert.That(ledger.LastDayNumber, Is.EqualTo(1));
            Assert.That(ledger.Last.revenue, Is.EqualTo(6L));
            Assert.That(ledger.Current.visitors, Is.Zero);
            Assert.That(ledger.Current.revenue, Is.Zero);
            AtomicFailure(() => ledger.BeginDay(3, 0));
            Resolve("day-two", CustomerOutcome.Impatient);
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(2, ShopDayPhase.Open), out _), Is.True);
            ledger.FinishDay(1);
            Assert.That(ledger.LastDayNumber, Is.EqualTo(2));
            Assert.That(ledger.Last.revenue, Is.Zero);
            Assert.That(ledger.Last.impatient, Is.EqualTo(1));
            Assert.That(firstReport.dayNumber, Is.EqualTo(1));
            Assert.That(firstReport.revenue, Is.EqualTo(6L));
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(3, ShopDayPhase.Night), out _), Is.True);
        }

        [TestCase(ShopDayPhase.Night)]
        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void LegacyCycleHasNoInventedHistoryAndLabelsAnActiveReportPartial(ShopDayPhase phase)
        {
            var cycle = Cycle(7, phase);
            ledger = ShopTradeLedger.LegacyForCycle(cycle, 2);
            Assert.That(ledger.Last, Is.Null);
            Assert.That(ledger.HasPending, Is.False);
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), cycle, out _), Is.True);
            if (phase == ShopDayPhase.Night)
            {
                Assert.That(ledger.Current, Is.Null);
                Assert.That(ledger.BeginDay(7, 2), Is.True);
                Assert.That(ledger.Current.partial, Is.False);
            }
            else
            {
                Assert.That(ledger.Current.dayNumber, Is.EqualTo(7));
                Assert.That(ledger.Current.partial, Is.True);
                Assert.That(ledger.Current.visitors, Is.Zero);
                Assert.That(ledger.Current.revenue, Is.Zero);
                Assert.That(ledger.Current.stockedAtOpen, Is.EqualTo(2));
                Resolve("after-upgrade", CustomerOutcome.Sold, 6);
                ledger.FinishDay(1);
                Assert.That(ledger.Last.partial, Is.True);
                Assert.That(ledger.Last.revenue, Is.EqualTo(6L));
            }
        }

        [Test]
        public void SnapshotsAndRestoreInputsCannotMutateTheLiveLedger()
        {
            ledger.BeginDay(1, 1); Resolve("first", CustomerOutcome.Sold, 6); ledger.FinishDay(0);
            ledger.BeginDay(2, 2); ledger.BeginVisit("pending");
            string before = Snapshot();
            var save = ledger.CaptureSave();
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(2, ShopDayPhase.Open), out var restored), Is.True);
            ledger.Current.visitors = 999;
            ledger.Last.revenue = 999;
            save.current.dayNumber = 999;
            save.last.revenue = 999;
            save.pendingCustomerId = "changed";
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(before));
            Assert.That(restored.ResolveVisit("pending", CustomerOutcome.Closed), Is.True);
            Assert.That(ledger.HasPending, Is.True);
        }

        [Test]
        public void CounterLimitRejectsNewEventsWithoutBreakingTheLastPendingResolution()
        {
            var save = new TradeLedgerSave
            {
                hasCurrent = true,
                current = new TradeDaySave { dayNumber = 1, visitors = ShopTradeLedger.MaxCounter - 1,
                    noStock = ShopTradeLedger.MaxCounter - 1, restocked = ShopTradeLedger.MaxCounter }
            };
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Open), out ledger), Is.True);
            AtomicFailure(() => ledger.RecordRestock());
            Assert.That(ledger.BeginVisit("last-allowed"), Is.True);
            Assert.That(ledger.ResolveVisit("last-allowed", CustomerOutcome.NoStock), Is.True);
            AtomicFailure(() => ledger.BeginVisit("too-many"));
            Assert.That(ledger.Current.visitors, Is.EqualTo(ShopTradeLedger.MaxCounter));
            Assert.That(ledger.Current.noStock, Is.EqualTo(ShopTradeLedger.MaxCounter));
            Assert.That(ledger.FinishDay(0), Is.True);
        }

        [Test]
        public void RevenueUsesLongAndCannotOverflowItsBoundOrLoseAPendingOutcome()
        {
            var save = new TradeLedgerSave
            {
                hasCurrent = true,
                current = new TradeDaySave { dayNumber = 1, visitors = 601, sales = 600, revenue = ShopTradeLedger.MaxRevenue - 5 },
                pendingCustomerId = "last-sale"
            };
            // Above int.MaxValue: actual JSON round trip must preserve the long value.
            save = JsonUtility.FromJson<TradeLedgerSave>(JsonUtility.ToJson(save));
            Assert.That(save.current.revenue, Is.EqualTo(ShopTradeLedger.MaxRevenue - 5));
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(1, ShopDayPhase.Open), out ledger), Is.True);
            AtomicFailure(() => ledger.ResolveVisit("last-sale", CustomerOutcome.Sold, 6));
            Assert.That(ledger.ResolveVisit("last-sale", CustomerOutcome.Sold, 5), Is.True);
            Assert.That(ledger.Current.revenue, Is.EqualTo(ShopTradeLedger.MaxRevenue));
            Assert.That(ledger.BeginVisit("blocked-sale"), Is.True);
            AtomicFailure(() => ledger.ResolveVisit("blocked-sale", CustomerOutcome.Sold, int.MaxValue));
            Assert.That(ledger.CancelPendingForReload(), Is.True);
            Assert.That(ledger.Current.interrupted, Is.EqualTo(1));
            Assert.That(ledger.Current.sales, Is.EqualTo(601));
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(1, ShopDayPhase.Open), out _), Is.True);
        }

        [Test]
        public void InvalidDayOrStockCommandsFailBeforeChangingPendingOrLastReport()
        {
            AtomicFailure(() => ledger.BeginDay(0, 0));
            AtomicFailure(() => ledger.BeginDay(int.MaxValue, 0));
            AtomicFailure(() => ledger.BeginDay(1, -1));
            AtomicFailure(() => ledger.BeginDay(1, ShopTradeLedger.MaxCounter + 1));
            ledger.BeginDay(1, 1); ledger.BeginVisit("pending");
            AtomicFailure(() => ledger.FinishDay(-1));
            AtomicFailure(() => ledger.FinishDay(ShopTradeLedger.MaxCounter + 1));
            Assert.That(ledger.HasPending, Is.True);
            Assert.That(ledger.FinishDay(1), Is.True);
            Assert.That(ledger.Last.closed, Is.EqualTo(1));
        }

        [TestCase("null")]
        [TestCase("version")]
        [TestCase("missing-current")]
        [TestCase("wrong-current-day")]
        [TestCase("wrong-last-day")]
        [TestCase("future-last-day")]
        [TestCase("last-unresolved")]
        [TestCase("outcomes-exceed-visitors")]
        [TestCase("unresolved-without-id")]
        [TestCase("id-without-unresolved")]
        [TestCase("two-unresolved")]
        [TestCase("whitespace-id")]
        [TestCase("long-id")]
        [TestCase("current-close-stock")]
        [TestCase("negative-visitors")]
        [TestCase("negative-sales")]
        [TestCase("negative-stockout")]
        [TestCase("negative-impatient")]
        [TestCase("negative-closed")]
        [TestCase("negative-interrupted")]
        [TestCase("negative-path")]
        [TestCase("negative-open-stock")]
        [TestCase("negative-close-stock")]
        [TestCase("negative-restocked")]
        [TestCase("negative-revenue")]
        [TestCase("revenue-over-cap")]
        [TestCase("revenue-impossible")]
        [TestCase("revenue-without-sale")]
        [TestCase("counter-over-cap")]
        [TestCase("last-day-zero")]
        [TestCase("hidden-current")]
        [TestCase("hidden-last")]
        [TestCase("missing-last")]
        [TestCase("default-current-marked-present")]
        [TestCase("default-last-marked-present")]
        public void MalformedReportsAreRejectedWithoutReturningAPartialLedger(string problem)
        {
            var save = PopulatedSave();
            switch (problem)
            {
                case "null": save = null; break;
                case "version": save.version = 2; break;
                case "missing-current": save.current = null; break;
                case "wrong-current-day": save.current.dayNumber = 3; break;
                case "wrong-last-day": save.last.dayNumber = 2; break;
                case "future-last-day": save.last.dayNumber = 3; break;
                case "last-unresolved": save.last.visitors++; break;
                case "outcomes-exceed-visitors": save.current.noStock++; break;
                case "unresolved-without-id": save.pendingCustomerId = null; break;
                case "id-without-unresolved": save.current.visitors--; break;
                case "two-unresolved": save.current.visitors++; break;
                case "whitespace-id": save.pendingCustomerId = " "; break;
                case "long-id": save.pendingCustomerId = new string('a', 129); break;
                case "current-close-stock": save.current.stockedAtClose = 1; break;
                case "negative-visitors": save.current.visitors = -1; break;
                case "negative-sales": save.current.sales = -1; break;
                case "negative-stockout": save.current.noStock = -1; break;
                case "negative-impatient": save.current.impatient = -1; break;
                case "negative-closed": save.current.closed = -1; break;
                case "negative-interrupted": save.current.interrupted = -1; break;
                case "negative-path": save.current.pathProblems = -1; break;
                case "negative-open-stock": save.current.stockedAtOpen = -1; break;
                case "negative-close-stock": save.last.stockedAtClose = -1; break;
                case "negative-restocked": save.current.restocked = -1; break;
                case "negative-revenue": save.current.revenue = -1; break;
                case "revenue-over-cap": save.current.revenue = ShopTradeLedger.MaxRevenue + 1; break;
                case "revenue-impossible": save.current.revenue = (long)int.MaxValue + 1; break;
                case "revenue-without-sale": save.current.sales = 0; save.current.noStock = 1; break;
                case "counter-over-cap": save.current.visitors = ShopTradeLedger.MaxCounter + 1; break;
                case "last-day-zero": save.last.dayNumber = 0; break;
                case "hidden-current": save.hasCurrent = false; break;
                case "hidden-last": save.hasLast = false; break;
                case "missing-last": save.last = null; break;
                case "default-current-marked-present": save.current = new TradeDaySave(); break;
                case "default-last-marked-present": save.last = new TradeDaySave(); break;
            }
            string original = save == null ? null : JsonUtility.ToJson(save);
            Assert.That(ShopTradeLedger.TryRestore(save, Cycle(2, ShopDayPhase.Open), out var restored), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(save == null ? null : JsonUtility.ToJson(save), Is.EqualTo(original));
        }

        [Test]
        public void NightCannotContainCurrentReportOrPendingVisitorAndActivePhasesRequireCurrent()
        {
            ledger.BeginDay(1, 1); ledger.BeginVisit("pending");
            Assert.That(ShopTradeLedger.TryRestore(ledger.CaptureSave(), Cycle(1, ShopDayPhase.Night), out _), Is.False);
            Assert.That(ShopTradeLedger.TryRestore(new TradeLedgerSave { pendingCustomerId = "pending" }, Cycle(1, ShopDayPhase.Night), out _), Is.False);
            Assert.That(ShopTradeLedger.TryRestore(new TradeLedgerSave(), Cycle(1, ShopDayPhase.Open), out _), Is.False);
            Assert.That(ShopTradeLedger.TryRestore(new TradeLedgerSave(), Cycle(1, ShopDayPhase.Closing), out _), Is.False);
            Assert.That(ShopTradeLedger.TryRestore(new TradeLedgerSave { pendingCustomerId = "" }, Cycle(1, ShopDayPhase.Night), out var empty), Is.True);
            Assert.That(empty.HasPending, Is.False);
        }

        [Test]
        public void InvalidClockOrLegacyStockIsRejected()
        {
            Assert.That(ShopTradeLedger.TryRestore(new TradeLedgerSave(), null, out _), Is.False);
            var bad = Cycle(1, ShopDayPhase.Night); bad.phase = (ShopDayPhase)999;
            Assert.That(ShopTradeLedger.TryRestore(new TradeLedgerSave(), bad, out _), Is.False);
            Assert.Throws<ArgumentException>(() => ShopTradeLedger.LegacyForCycle(bad, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShopTradeLedger.LegacyForCycle(Cycle(1, ShopDayPhase.Night), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShopTradeLedger.LegacyForCycle(Cycle(1, ShopDayPhase.Night), ShopTradeLedger.MaxCounter + 1));
        }

        [TestCase("fresh-night")]
        [TestCase("first-active-day")]
        [TestCase("completed-night")]
        [TestCase("active-with-history")]
        public void JsonRoundTripPreservesOptionalReportPresenceWithoutInventingDefaultDays(string state)
        {
            var cycle = Cycle(1, ShopDayPhase.Night);
            if (state != "fresh-night")
            {
                ledger.BeginDay(1, 1);
                Resolve("paid", CustomerOutcome.Sold, 6);
                cycle = Cycle(1, ShopDayPhase.Open);
            }
            if (state == "completed-night" || state == "active-with-history")
            {
                ledger.FinishDay(0);
                cycle = Cycle(2, ShopDayPhase.Night);
            }
            if (state == "active-with-history")
            {
                ledger.BeginDay(2, 0);
                ledger.BeginVisit("pending");
                cycle = Cycle(2, ShopDayPhase.Open);
            }
            var before = ledger.CaptureSave();
            var decoded = JsonUtility.FromJson<TradeLedgerSave>(JsonUtility.ToJson(before));
            Assert.That(ShopTradeLedger.TryRestore(decoded, cycle, out var restored), Is.True);
            var after = restored.CaptureSave();
            Assert.That(after.hasCurrent, Is.EqualTo(before.hasCurrent));
            Assert.That(after.hasLast, Is.EqualTo(before.hasLast));
            Assert.That(after.current == null, Is.EqualTo(!before.hasCurrent));
            Assert.That(after.last == null, Is.EqualTo(!before.hasLast));
            Assert.That(after.pendingCustomerId, Is.EqualTo(before.pendingCustomerId));
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)));
        }

        [Test]
        public void AbsentReportsAcceptOnlyEmptySerializationSentinelsNeverHiddenCounters()
        {
            var empty = new TradeLedgerSave { current = new TradeDaySave(), last = new TradeDaySave() };
            Assert.That(ShopTradeLedger.TryRestore(empty, Cycle(1, ShopDayPhase.Night), out var restored), Is.True);
            Assert.That(restored.Current, Is.Null);
            Assert.That(restored.Last, Is.Null);
            empty.current.partial = true;
            Assert.That(ShopTradeLedger.TryRestore(empty, Cycle(1, ShopDayPhase.Night), out _), Is.False);
            empty.current.partial = false;
            empty.last.revenue = 1;
            Assert.That(ShopTradeLedger.TryRestore(empty, Cycle(1, ShopDayPhase.Night), out _), Is.False);
        }

        void Resolve(string id, CustomerOutcome outcome, int price = 0)
        {
            Assert.That(ledger.BeginVisit(id), Is.True);
            Assert.That(ledger.ResolveVisit(id, outcome, price), Is.True);
        }

        string Snapshot() => JsonUtility.ToJson(ledger.CaptureSave());
        void AtomicFailure(Func<bool> action)
        {
            string before = Snapshot();
            Assert.That(action(), Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }

        TradeLedgerSave PopulatedSave()
        {
            ledger.BeginDay(1, 2); Resolve("prior", CustomerOutcome.Sold, 6); ledger.FinishDay(1);
            ledger.BeginDay(2, 2); Resolve("paid", CustomerOutcome.Sold, 10); ledger.BeginVisit("pending");
            return ledger.CaptureSave();
        }

        static ShopDaySave Cycle(int day, ShopDayPhase phase)
        {
            return new ShopDaySave { dayNumber = day, phase = phase,
                secondsRemaining = phase == ShopDayPhase.Open ? 480f : phase == ShopDayPhase.Closing ? 3f : 0f };
        }
    }
}
