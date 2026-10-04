using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class ComedyTests
    {
        [Test]
        public void NewSchedulerIsSilentAndHasNoPendingIncident()
        {
            var comedy = new ShopComedy();
            Assert.That(comedy.Active, Is.False);
            Assert.That(comedy.Caption, Is.Empty);
            Assert.That(comedy.Elapsed, Is.Zero);
            Assert.That(comedy.Remaining, Is.Zero);
            Assert.That(comedy.CooldownRemaining, Is.Zero);
            comedy.Tick(100f);
            Assert.That(comedy.Active, Is.False);
        }

        [TestCase(ComedyKind.Fart)]
        [TestCase(ComedyKind.Complaint)]
        [TestCase(ComedyKind.Belch)]
        [TestCase(ComedyKind.WorkerCelebration)]
        [TestCase(ComedyKind.Mimic)]
        public void IncidentsOccupyOneSlotThenLeaveASharedQuietPeriod(ComedyKind kind)
        {
            var comedy = new ShopComedy();
            Assert.That(comedy.TryStart(kind, 0, false), Is.True);
            Assert.That(comedy.Active, Is.True);
            Assert.That(comedy.Kind, Is.EqualTo(kind));
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(8f));
            Assert.That(comedy.Remaining, Is.InRange(0.6f, 3f));
            Assert.That(comedy.TryStart(ComedyKind.Mimic, 1, true), Is.False);
            Assert.That(comedy.Kind, Is.EqualTo(kind));
            Assert.That(comedy.Variant, Is.Zero);
            Assert.That(comedy.Reduced, Is.False);
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(8f));
            comedy.Tick(3f);
            Assert.That(comedy.Active, Is.False);
            Assert.That(comedy.Remaining, Is.Zero);
            Assert.That(comedy.Caption, Is.Empty);
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(5f));
            Assert.That(comedy.TryStart(ComedyKind.WorkerCelebration, 0, false), Is.False);
        }

        [TestCase(ComedyKind.Fart, 35f)]
        [TestCase(ComedyKind.Complaint, 40f)]
        [TestCase(ComedyKind.Belch, 25f)]
        [TestCase(ComedyKind.WorkerCelebration, 15f)]
        [TestCase(ComedyKind.Mimic, 30f)]
        public void KindCooldownSurvivesCancellationAndRejectedAttempts(ComedyKind kind, float cooldown)
        {
            var comedy = new ShopComedy();
            Assert.That(comedy.TryStart(kind, 0, false), Is.True);
            comedy.Tick(1f);
            comedy.Cancel();
            Assert.That(comedy.Caption, Is.Empty);
            Assert.That(comedy.Remaining, Is.Zero);
            Assert.That(comedy.Elapsed, Is.Zero);
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(7f));
            comedy.Tick(cooldown - 1.25f);
            Assert.That(comedy.CooldownRemaining, Is.Zero);
            Assert.That(comedy.TryStart(kind, 1, true), Is.False);
            Assert.That(comedy.CooldownRemaining, Is.Zero);
            comedy.Tick(0.25f);
            Assert.That(comedy.TryStart(kind, 1, true), Is.True);
            Assert.That(comedy.Variant, Is.EqualTo(1));
        }

        [Test]
        public void DifferentKindCanStartAfterSharedQuietWithoutClearingOriginalCooldown()
        {
            var comedy = new ShopComedy();
            comedy.TryStart(ComedyKind.Fart, 0, false);
            comedy.Tick(8f);
            Assert.That(comedy.TryStart(ComedyKind.Complaint, 0, false), Is.True);
            comedy.Cancel();
            comedy.Tick(8f);
            Assert.That(comedy.TryStart(ComedyKind.Fart, 0, false), Is.False);
            comedy.Tick(19f);
            Assert.That(comedy.TryStart(ComedyKind.Fart, 0, false), Is.True);
        }

        [TestCase(ComedyKind.Fart)]
        [TestCase(ComedyKind.Complaint)]
        [TestCase(ComedyKind.Belch)]
        [TestCase(ComedyKind.WorkerCelebration)]
        [TestCase(ComedyKind.Mimic)]
        public void ReducedIncidentFinishesQuicklyAndLeavesLongerSharedQuiet(ComedyKind kind)
        {
            var comedy = new ShopComedy();
            comedy.TryStart(kind, 1, true);
            Assert.That(comedy.Reduced, Is.True);
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(18f));
            Assert.That(comedy.Remaining, Is.InRange(0.6f, 1.5f));
            comedy.Tick(1.5f);
            Assert.That(comedy.Active, Is.False);
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(16.5f));
            comedy.Tick(16.25f);
            Assert.That(comedy.TryStart(ComedyKind.WorkerCelebration, 0, false), Is.False);
            comedy.Tick(0.25f);
            Assert.That(comedy.CooldownRemaining, Is.Zero);
        }

        [TestCase(ComedyKind.Slap)]
        [TestCase(ComedyKind.Fart)]
        [TestCase(ComedyKind.Complaint)]
        [TestCase(ComedyKind.Belch)]
        [TestCase(ComedyKind.WorkerCelebration)]
        [TestCase(ComedyKind.Mimic)]
        public void EachVariantHasASetupAndDelayedReactionIncludingReducedMode(ComedyKind kind)
        {
            string firstSetup = null, firstReaction = null;
            for (int variant = 0; variant < 2; variant++)
            {
                foreach (bool reduced in new[] { false, true })
                {
                    var comedy = new ShopComedy();
                    if (kind == ComedyKind.Slap) comedy.StartSlap(variant, reduced);
                    else Assert.That(comedy.TryStart(kind, variant, reduced), Is.True);
                    string setup = comedy.Caption;
                    Assert.That(string.IsNullOrWhiteSpace(setup), Is.False);
                    comedy.Tick(0.5f);
                    Assert.That(comedy.Caption, Is.EqualTo(setup));
                    comedy.Tick(0.1f);
                    string reaction = comedy.Caption;
                    Assert.That(string.IsNullOrWhiteSpace(reaction), Is.False);
                    Assert.That(reaction, Is.Not.EqualTo(setup));
                    if (variant == 0) { firstSetup = setup; firstReaction = reaction; }
                    else
                    {
                        Assert.That(setup, Is.Not.EqualTo(firstSetup));
                        Assert.That(reaction, Is.Not.EqualTo(firstReaction));
                    }
                    comedy.Tick(3f);
                    Assert.That(comedy.Active, Is.False);
                    Assert.That(comedy.Caption, Is.Empty);
                }
            }
        }

        [Test]
        public void SlapFeedbackNeedsExplicitGameplayEntryPointAndPreemptsAnIncident()
        {
            var comedy = new ShopComedy();
            Assert.That(comedy.TryStart(ComedyKind.Slap, 0, false), Is.False);
            comedy.TryStart(ComedyKind.Complaint, 0, false);
            comedy.Tick(0.7f);
            Assert.That(comedy.TryStart(ComedyKind.Slap, 1, true), Is.False);
            Assert.That(comedy.Kind, Is.EqualTo(ComedyKind.Complaint));
            comedy.StartSlap(1, true);
            Assert.That(comedy.Kind, Is.EqualTo(ComedyKind.Slap));
            Assert.That(comedy.Variant, Is.EqualTo(1));
            Assert.That(comedy.Active, Is.True);
            Assert.That(comedy.Elapsed, Is.Zero);
            Assert.That(comedy.Reduced, Is.True);
            Assert.That(comedy.Remaining, Is.LessThanOrEqualTo(1.5f));
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(18f));
            comedy.Cancel();
            comedy.Tick(18f);
            Assert.That(comedy.TryStart(ComedyKind.Complaint, 0, false), Is.False);
            comedy.Tick(22f);
            Assert.That(comedy.TryStart(ComedyKind.Complaint, 0, false), Is.True);
        }

        [Test]
        public void PrioritySlapDoesNotShortenEarlierReducedQuietPeriod()
        {
            var comedy = new ShopComedy();
            comedy.TryStart(ComedyKind.Fart, 0, true);
            comedy.Tick(1f);
            comedy.StartSlap(0, false);
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(17f));
            Assert.That(comedy.Remaining, Is.LessThanOrEqualTo(3f));
            comedy.Cancel();
            comedy.Cancel();
            Assert.That(comedy.CooldownRemaining, Is.EqualTo(17f));
        }

        [TestCase(-1)]
        [TestCase(6)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidKindsCannotStartOrDisturbAnIncident(int kind)
        {
            var comedy = new ShopComedy();
            Assert.That(comedy.TryStart((ComedyKind)kind, 0, false), Is.False);
            Assert.That(comedy.Active, Is.False);
            Assert.That(comedy.CooldownRemaining, Is.Zero);
            comedy.TryStart(ComedyKind.Belch, 1, true);
            comedy.Tick(0.7f);
            string caption = comedy.Caption;
            Assert.That(comedy.TryStart((ComedyKind)kind, int.MaxValue, false), Is.False);
            Assert.That(comedy.Caption, Is.EqualTo(caption));
            Assert.That(comedy.Kind, Is.EqualTo(ComedyKind.Belch));
            Assert.That(comedy.Elapsed, Is.EqualTo(0.7f));
        }

        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void ArbitraryVariantNumbersNormalizeWithoutOverflow(int variant)
        {
            var comedy = new ShopComedy();
            Assert.That(comedy.TryStart(ComedyKind.Mimic, variant, false), Is.True);
            Assert.That(comedy.Variant, Is.InRange(0, 1));
            Assert.That(comedy.Caption, Is.Not.Empty);
            comedy.Tick(0.6f);
            Assert.That(comedy.Caption, Is.Not.Empty);
            comedy.StartSlap(variant, false);
            Assert.That(comedy.Variant, Is.InRange(0, 1));
            Assert.That(comedy.Caption, Is.Not.Empty);
        }

        [Test]
        public void NonPositiveAndNonFiniteDeltasDoNotAdvanceOrCorruptState()
        {
            var comedy = new ShopComedy();
            comedy.TryStart(ComedyKind.Fart, 0, false);
            comedy.Tick(0.5f);
            string caption = comedy.Caption;
            float remaining = comedy.Remaining;
            foreach (float delta in new[] { 0f, -1f, -float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                comedy.Tick(delta);
                Assert.That(comedy.Elapsed, Is.EqualTo(0.5f));
                Assert.That(comedy.Remaining, Is.EqualTo(remaining));
                Assert.That(comedy.CooldownRemaining, Is.EqualTo(7.5f));
                Assert.That(comedy.Caption, Is.EqualTo(caption));
            }
            comedy.Tick(34.5f);
            Assert.That(comedy.TryStart(ComedyKind.Fart, 0, false), Is.True);
        }

        [Test]
        public void HugeFiniteDeltaFinishesAndClampsAllTimersWithoutReplayingAnything()
        {
            var comedy = new ShopComedy();
            comedy.TryStart(ComedyKind.Mimic, 1, false);
            comedy.Tick(float.MaxValue);
            comedy.Tick(float.MaxValue);
            Assert.That(comedy.Active, Is.False);
            Assert.That(comedy.Caption, Is.Empty);
            Assert.That(comedy.Remaining, Is.Zero);
            Assert.That(comedy.CooldownRemaining, Is.Zero);
            Assert.That(comedy.Elapsed, Is.InRange(0f, 3f));
            Assert.That(comedy.TryStart(ComedyKind.Mimic, 0, false), Is.True);
            Assert.That(comedy.Elapsed, Is.Zero);
        }
    }
}
