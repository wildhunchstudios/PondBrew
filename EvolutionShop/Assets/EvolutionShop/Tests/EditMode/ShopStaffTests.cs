using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ShopStaffTests
    {
        GameModel game;
        ShopDayCycle cycle;
        ShopStaff staff;

        [SetUp] public void Setup()
        {
            game = WithCredits(30);
            cycle = new ShopDayCycle();
            staff = new ShopStaff();
        }

        [Test] public void HiringStartsOffScheduleAndCostsNothing()
        {
            Assert.That(staff.Hire(cycle), Is.True);
            Assert.That(staff.Hire(cycle), Is.False);
            Assert.That(staff.Hired, Is.True);
            Assert.That(staff.Scheduled, Is.False);
            Assert.That(staff.Paused, Is.False);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(staff.IsOnDuty(1), Is.False);
            Assert.That(game.Currency, Is.EqualTo(30));
        }

        [Test] public void OnePaymentCoversPreparationOpenAndClosingWithoutNewCharge()
        {
            Schedule();
            Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            for (int i = 0; i < 20; i++) Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(26));
            Assert.That(staff.WagesForDay(1), Is.EqualTo(4));
            Assert.That(staff.IsOnDuty(1), Is.True);
            cycle.OpenShop();
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(staff.IsOnDuty(1), Is.True);
            cycle.RequestClose();
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(staff.IsOnDuty(1), Is.True);
            Assert.That(game.Currency, Is.EqualTo(26));
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void EmploymentAndScheduleChangesAreNightOnly(ShopDayPhase phase)
        {
            Schedule();
            cycle.OpenShop();
            if (phase == ShopDayPhase.Closing) cycle.RequestClose();
            string before = JsonUtility.ToJson(staff.CaptureSave());
            Assert.That(staff.Dismiss(cycle), Is.False);
            Assert.That(staff.SetScheduled(false, cycle), Is.False);
            Assert.That(new ShopStaff().Hire(cycle), Is.False);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(JsonUtility.ToJson(staff.CaptureSave()), Is.EqualTo(before));
            Assert.That(game.Currency, Is.EqualTo(30));
        }

        [TestCase(0)] [TestCase(3)]
        public void InsufficientFundsKeepEmployeeAndScheduleWithoutDebtOrReceipt(int credits)
        {
            game = WithCredits(credits); Schedule();
            string before = JsonUtility.ToJson(game.CaptureSave());
            for (int i = 0; i < 4; i++) Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(staff.Hired && staff.Scheduled, Is.True);
            Assert.That(staff.IsOnDuty(1), Is.False);
            Assert.That(staff.PaidDay, Is.Zero);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
            Assert.That(cycle.OpenShop(), Is.True, "The player can still run an unstaffed shop.");
        }

        [Test] public void ManualRetryCanUseLaterFundsAtNightButNeverAutoChargesOnRestore()
        {
            game = WithCredits(3); Schedule();
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(ShopStaff.TryRestore(staff.CaptureSave(), 1, out staff), Is.True);
            Assert.That(staff.PaidDay, Is.Zero);
            game = WithCredits(4);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.Zero);
            Assert.That(staff.IsOnDuty(1), Is.True);
        }

        [Test] public void PauseResumeAndReschedulingPreserveTheWholePaidPeriod()
        {
            Schedule(); staff.TryBeginDuty(game, cycle);
            Assert.That(staff.SetPaused(true), Is.True);
            Assert.That(staff.SetPaused(true), Is.False);
            Assert.That(staff.IsOnDuty(1), Is.False);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            Assert.That(staff.SetScheduled(false, cycle), Is.True);
            Assert.That(staff.WagesForDay(1), Is.EqualTo(4));
            Assert.That(staff.SetScheduled(true, cycle), Is.True);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False, "Scheduling does not silently clear pause.");
            Assert.That(staff.SetPaused(false), Is.True);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            cycle.OpenShop();
            Assert.That(staff.SetPaused(true), Is.True);
            Assert.That(staff.SetPaused(false), Is.True);
            Assert.That(staff.IsOnDuty(1), Is.True);
            Assert.That(game.Currency, Is.EqualTo(26));
        }

        [Test] public void DismissAndRehireSameNightNeverRefundOrDuplicateWage()
        {
            Schedule(); staff.TryBeginDuty(game, cycle);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(staff.Dismiss(cycle), Is.True);
                Assert.That(staff.SetPaused(true), Is.False);
                Assert.That(staff.WagesForDay(1), Is.EqualTo(4));
                Assert.That(staff.Hire(cycle), Is.True);
                Assert.That(staff.Scheduled, Is.False);
                staff.SetScheduled(true, cycle);
                Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            }
            Assert.That(staff.PreviousPaidDay, Is.Zero);
            Assert.That(game.Currency, Is.EqualTo(26));
        }

        [Test] public void ForwardPeriodsChargeOnceAndKeepPreviousReportAfterNewNightPayment()
        {
            Schedule();
            for (int day = 1; day <= 3; day++)
            {
                Assert.That(cycle.DayNumber, Is.EqualTo(day));
                Assert.That(staff.IsOnDuty(day), Is.False);
                Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
                Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
                Assert.That(staff.WagesForDay(day), Is.EqualTo(4));
                if (day > 1) Assert.That(staff.WagesForDay(day - 1), Is.EqualTo(4));
                cycle.OpenShop(); cycle.Tick(483);
            }
            Assert.That(game.Currency, Is.EqualTo(18));
            Assert.That(staff.PaidDay, Is.EqualTo(3));
            Assert.That(staff.PreviousPaidDay, Is.EqualTo(2));
            Assert.That(staff.WagesForDay(1), Is.Zero);
            Assert.That(staff.WagesForDay(4), Is.Zero);
            Assert.That(staff.WagesForDay(0), Is.Zero);
        }

        [Test] public void MissedUnpaidPeriodsDoNotAccumulateDebt()
        {
            Schedule(); staff.TryBeginDuty(game, cycle);
            cycle.OpenShop(); cycle.Tick(483);
            staff.SetPaused(true);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            cycle.OpenShop(); cycle.Tick(483);
            staff.SetPaused(false);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            Assert.That(staff.PreviousPaidDay, Is.EqualTo(1));
            Assert.That(staff.WagesForDay(2), Is.Zero);
            Assert.That(game.Currency, Is.EqualTo(22));
        }

        [Test] public void PayingPreservesProductionStockDiscoveryAndPricesExactly()
        {
            game.Tick(20);
            Assert.That(game.Collect(CultureKind.Bloom).Success, Is.True);
            Assert.That(game.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(game.Dose(CultureKind.Bloom).Success, Is.True);
            Assert.That(game.Dose(CultureKind.Flow).Success, Is.True);
            Assert.That(game.BeginIncubation().Success, Is.True);
            game.Tick(10);
            Assert.That(game.TransferToTank("tank-a").Success, Is.True);
            Assert.That(game.ReserveTank("tank-a", "manual-buyer").Success, Is.True);
            var expected = game.CaptureSave(); expected.currency -= 4;
            string specimen = game.GetTank("tank-a").Value.Id;
            Schedule(); Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(game.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("manual-buyer"));
            Assert.That(game.GetTank("tank-a").Value.Id, Is.EqualTo(specimen));
        }

        [Test] public void CapturesAndRestoresAreIndependentAndRetainPaidPause()
        {
            Schedule(); staff.TryBeginDuty(game, cycle); staff.SetPaused(true);
            var saved = staff.CaptureSave();
            Assert.That(ShopStaff.TryRestore(saved, 1, out var restored), Is.True);
            saved.paidDay = 0; saved.hired = false; saved.paused = false;
            Assert.That(restored.Hired && restored.Scheduled && restored.Paused, Is.True);
            Assert.That(restored.PaidDay, Is.EqualTo(1));
            Assert.That(restored.TryBeginDuty(game, cycle), Is.False);
            restored.SetPaused(false);
            Assert.That(restored.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(26));
            Assert.That(staff.Paused, Is.True);
        }

        [TestCase("null")] [TestCase("version")] [TestCase("missing-id")] [TestCase("other-id")]
        [TestCase("negative-day")] [TestCase("negative-wage")] [TestCase("future-day")]
        [TestCase("zero-paid-wage")] [TestCase("unpaid-wage")] [TestCase("wrong-wage")]
        [TestCase("previous-negative")] [TestCase("previous-future")] [TestCase("previous-duplicate")]
        [TestCase("previous-without-latest")] [TestCase("previous-wrong-wage")]
        [TestCase("unhired-scheduled")] [TestCase("unhired-paused")] [TestCase("invalid-cycle-day")]
        public void MalformedReceiptsAndEmploymentFailClosed(string problem)
        {
            var saved = new StaffSave { version = 1, employeeId = ShopStaff.CashierId,
                hired = true, scheduled = true, paidDay = 2, paidWage = 4, previousPaidDay = 1, previousPaidWage = 4 };
            int day = 2;
            switch (problem)
            {
                case "null": saved = null; break;
                case "version": saved.version = 0; break;
                case "missing-id": saved.employeeId = null; break;
                case "other-id": saved.employeeId = "cashier-other"; break;
                case "negative-day": saved.paidDay = -1; break;
                case "negative-wage": saved.paidWage = -4; break;
                case "future-day": saved.paidDay = 3; break;
                case "zero-paid-wage": saved.paidWage = 0; break;
                case "unpaid-wage": saved.paidDay = 0; break;
                case "wrong-wage": saved.paidWage = 8; break;
                case "previous-negative": saved.previousPaidDay = -1; break;
                case "previous-future": saved.previousPaidDay = 3; break;
                case "previous-duplicate": saved.previousPaidDay = 2; break;
                case "previous-without-latest": saved.paidDay = saved.paidWage = 0; break;
                case "previous-wrong-wage": saved.previousPaidWage = 1; break;
                case "unhired-scheduled": saved.hired = false; break;
                case "unhired-paused": saved.hired = saved.scheduled = false; saved.paused = true; break;
                case "invalid-cycle-day": day = 0; break;
            }
            Assert.That(ShopStaff.TryRestore(saved, day, out var restored), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(game.Currency, Is.EqualTo(30));
        }

        [Test] public void MissingDependenciesAndBackwardClockCannotMutateState()
        {
            Assert.That(staff.Hire(null), Is.False);
            Assert.That(staff.SetScheduled(true, cycle), Is.False);
            Schedule();
            Assert.That(staff.TryBeginDuty(null, cycle), Is.False);
            Assert.That(staff.TryBeginDuty(game, null), Is.False);
            Assert.That(staff.Dismiss(null), Is.False);
            cycle.OpenShop(); cycle.Tick(483);
            staff.TryBeginDuty(game, cycle);
            Assert.That(staff.TryBeginDuty(game, new ShopDayCycle()), Is.False);
            Assert.That(game.Currency, Is.EqualTo(26));
        }

        void Schedule() { Assert.That(staff.Hire(cycle), Is.True); Assert.That(staff.SetScheduled(true, cycle), Is.True); }
        static GameModel WithCredits(int credits)
        {
            var config = GameConfig.CreateDefault();
            var save = new GameModel(config).CaptureSave(); save.currency = credits;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True);
            return model;
        }
    }
}
