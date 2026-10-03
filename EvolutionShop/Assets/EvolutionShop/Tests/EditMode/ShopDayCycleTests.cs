using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ShopDayCycleTests
    {
        [Test]
        public void FreshShopStartsOnAnUntimedPreparationNight()
        {
            var day = new ShopDayCycle();
            Assert.That(day.DayNumber, Is.EqualTo(1));
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(day.SecondsRemaining, Is.Zero);
            Assert.That(day.RequestClose(), Is.False);
            day.Tick(float.MaxValue);
            Assert.That(day.DayNumber, Is.EqualTo(1));
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(day.SecondsRemaining, Is.Zero);
        }

        [Test]
        public void OpeningStartsEightMinutesAndRepeatedOpeningCannotResetClock()
        {
            var day = new ShopDayCycle();
            Assert.That(day.OpenShop(), Is.True);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Open));
            Assert.That(day.SecondsRemaining, Is.EqualTo(480f));
            day.Tick(37f);
            Assert.That(day.OpenShop(), Is.False);
            Assert.That(day.SecondsRemaining, Is.EqualTo(443f));
            Assert.That(day.DayNumber, Is.EqualTo(1));
        }

        [Test]
        public void TimedClosingCompletesOnceThenTheNextOpeningGetsAFullDay()
        {
            var day = new ShopDayCycle(10f, 3f);
            day.OpenShop();
            day.Tick(9.5f);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Open));
            Assert.That(day.SecondsRemaining, Is.EqualTo(0.5f));
            day.Tick(0.5f);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Closing));
            Assert.That(day.SecondsRemaining, Is.EqualTo(3f));
            day.Tick(2.5f);
            Assert.That(day.DayNumber, Is.EqualTo(1));
            day.Tick(0.5f);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(day.DayNumber, Is.EqualTo(2));
            Assert.That(day.SecondsRemaining, Is.Zero);
            day.Tick(999f);
            Assert.That(day.DayNumber, Is.EqualTo(2));
            Assert.That(day.OpenShop(), Is.True);
            Assert.That(day.SecondsRemaining, Is.EqualTo(10f));
            day.Tick(13f);
            Assert.That(day.DayNumber, Is.EqualTo(3));
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
        }

        [Test]
        public void ManualClosingCannotBeRestartedOrReopenedDuringItsGracePeriod()
        {
            var day = new ShopDayCycle(10f, 3f);
            day.OpenShop();
            day.Tick(1f);
            Assert.That(day.RequestClose(), Is.True);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Closing));
            Assert.That(day.SecondsRemaining, Is.EqualTo(3f));
            day.Tick(1f);
            Assert.That(day.RequestClose(), Is.False);
            Assert.That(day.OpenShop(), Is.False);
            Assert.That(day.SecondsRemaining, Is.EqualTo(2f));
            day.Tick(2f);
            Assert.That(day.DayNumber, Is.EqualTo(2));
            Assert.That(day.RequestClose(), Is.False);
        }

        [Test]
        public void OvershootAdvancesClosingButNeverSkipsAnUntimedNight()
        {
            var day = new ShopDayCycle(10f, 3f);
            day.OpenShop();
            day.Tick(11f);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Closing));
            Assert.That(day.SecondsRemaining, Is.EqualTo(2f));
            day.Tick(float.MaxValue);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(day.DayNumber, Is.EqualTo(2));
            Assert.That(day.SecondsRemaining, Is.Zero);
            day.OpenShop();
            day.Tick(float.MaxValue);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(day.DayNumber, Is.EqualTo(3));
        }

        [Test]
        public void InvalidTimeDeltasCannotChangeAnOpenOrClosingClock()
        {
            var day = new ShopDayCycle();
            day.OpenShop();
            foreach (float delta in new[] {0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity})
                day.Tick(delta);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Open));
            Assert.That(day.SecondsRemaining, Is.EqualTo(480f));
            day.RequestClose();
            foreach (float delta in new[] {0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity})
                day.Tick(delta);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Closing));
            Assert.That(day.SecondsRemaining, Is.EqualTo(3f));
            Assert.That(day.DayNumber, Is.EqualTo(1));
        }

        [TestCase(ShopDayPhase.Night)]
        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void JsonRoundTripPreservesEachPhaseAndItsContinuation(ShopDayPhase phase)
        {
            var original = new ShopDayCycle(10f, 3f);
            original.OpenShop();
            original.Tick(13f);
            if (phase != ShopDayPhase.Night)
            {
                original.OpenShop();
                if (phase == ShopDayPhase.Closing) original.RequestClose();
                original.Tick(0.75f);
            }
            var data = JsonUtility.FromJson<ShopDaySave>(JsonUtility.ToJson(original.CaptureSave()));
            Assert.That(ShopDayCycle.TryRestore(data, out var restored, 10f, 3f), Is.True);
            Assert.That(restored.DayNumber, Is.EqualTo(2));
            Assert.That(restored.Phase, Is.EqualTo(phase));
            Assert.That(restored.SecondsRemaining, Is.EqualTo(original.SecondsRemaining));
            original.Tick(20f);
            restored.Tick(20f);
            Assert.That(restored.DayNumber, Is.EqualTo(original.DayNumber));
            Assert.That(restored.Phase, Is.EqualTo(original.Phase));
            Assert.That(restored.SecondsRemaining, Is.EqualTo(original.SecondsRemaining));
        }

        [Test]
        public void CapturedAndRestoredSaveObjectsCannotMutateTheClock()
        {
            var original = new ShopDayCycle();
            original.OpenShop();
            original.Tick(2f);
            var data = original.CaptureSave();
            data.secondsRemaining = 1f;
            Assert.That(original.SecondsRemaining, Is.EqualTo(478f));
            Assert.That(ShopDayCycle.TryRestore(data, out var restored), Is.True);
            data.dayNumber = 100;
            data.secondsRemaining = 200f;
            Assert.That(restored.DayNumber, Is.EqualTo(1));
            Assert.That(restored.SecondsRemaining, Is.EqualTo(1f));
        }

        [TestCase("null")]
        [TestCase("version")]
        [TestCase("zero-day")]
        [TestCase("negative-day")]
        [TestCase("unknown-phase")]
        [TestCase("negative-remaining")]
        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("night-has-time")]
        [TestCase("open-empty")]
        [TestCase("open-too-long")]
        [TestCase("closing-empty")]
        [TestCase("closing-too-long")]
        [TestCase("max-day-open")]
        [TestCase("max-day-closing")]
        public void MalformedSaveIsRejectedWithoutReturningPartialState(string problem)
        {
            var data = new ShopDayCycle().CaptureSave();
            switch (problem)
            {
                case "null": data = null; break;
                case "version": data.version = 2; break;
                case "zero-day": data.dayNumber = 0; break;
                case "negative-day": data.dayNumber = -1; break;
                case "unknown-phase": data.phase = (ShopDayPhase)99; break;
                case "negative-remaining": data.secondsRemaining = -1f; break;
                case "nan": data.secondsRemaining = float.NaN; break;
                case "infinity": data.secondsRemaining = float.PositiveInfinity; break;
                case "night-has-time": data.secondsRemaining = 1f; break;
                case "open-empty": data.phase = ShopDayPhase.Open; break;
                case "open-too-long": data.phase = ShopDayPhase.Open; data.secondsRemaining = 481f; break;
                case "closing-empty": data.phase = ShopDayPhase.Closing; break;
                case "closing-too-long": data.phase = ShopDayPhase.Closing; data.secondsRemaining = 4f; break;
                case "max-day-open": data.phase = ShopDayPhase.Open; data.dayNumber = int.MaxValue; data.secondsRemaining = 1f; break;
                case "max-day-closing": data.phase = ShopDayPhase.Closing; data.dayNumber = int.MaxValue; data.secondsRemaining = 1f; break;
            }
            Assert.That(ShopDayCycle.TryRestore(data, out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        [Test]
        public void LastRepresentableDayCanCompleteButCannotOpenAgain()
        {
            var data = new ShopDaySave
            {
                dayNumber = int.MaxValue - 1,
                phase = ShopDayPhase.Closing,
                secondsRemaining = 0.5f
            };
            Assert.That(ShopDayCycle.TryRestore(data, out var day), Is.True);
            day.Tick(1f);
            Assert.That(day.DayNumber, Is.EqualTo(int.MaxValue));
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(day.OpenShop(), Is.False);
            Assert.That(day.RequestClose(), Is.False);
            day.Tick(float.MaxValue);
            Assert.That(day.DayNumber, Is.EqualTo(int.MaxValue));
            Assert.That(ShopDayCycle.TryRestore(day.CaptureSave(), out var restored), Is.True);
            Assert.That(restored.OpenShop(), Is.False);
        }

        [Test]
        public void InvalidDurationsAreRejectedByConstructionAndRestore()
        {
            var data = new ShopDayCycle().CaptureSave();
            foreach (float duration in new[] {0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity})
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ShopDayCycle(duration, 3f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ShopDayCycle(480f, duration));
                Assert.That(ShopDayCycle.TryRestore(data, out var first, duration, 3f), Is.False);
                Assert.That(first, Is.Null);
                Assert.That(ShopDayCycle.TryRestore(data, out var second, 480f, duration), Is.False);
                Assert.That(second, Is.Null);
            }
        }
    }
}
