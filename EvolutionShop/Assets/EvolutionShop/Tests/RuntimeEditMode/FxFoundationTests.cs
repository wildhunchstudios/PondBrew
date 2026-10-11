using System;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class FxFoundationTests
    {
        [Test] public void SameOwnerReconciliationDoesNotRestartOrPublishACue()
        {
            using (var cues = new FxCueRuntime())
            {
                var owner = cues.Register("incubator", "specimen-a");
                cues.SetState(owner, FxSteadyState.Ready);
                for (int i = 0; i < 100; i++) Assert.That(cues.Register("incubator", "specimen-a"), Is.EqualTo(owner));
                Assert.That(cues.AcceptedCount, Is.Zero); Assert.That(cues.ActiveCount, Is.Zero);
                Assert.That(cues.TryState(owner, out var state) && state == FxSteadyState.Ready, Is.True);
            }
        }
        [Test] public void DuplicateAndLateCallbacksCannotReplayAfterHistoryWraps()
        {
            using (var cues = new FxCueRuntime())
            {
                var owner = cues.Register("tool", null); var other = cues.Register("other", null);
                Assert.That(cues.Play(owner, FxCueId.CollectedUnit, 7, FxCueOrigin.Manual, .2f, 4), Is.True);
                cues.Tick(1);
                for (uint i = 1; i <= 100; i++) { cues.Play(other, FxCueId.PondletGlance, i, FxCueOrigin.Manual, .2f, 1); cues.Tick(1); }
                Assert.That(cues.Play(owner, FxCueId.CollectedUnit, 7, FxCueOrigin.Manual, .2f, 4), Is.False);
                Assert.That(cues.Play(owner, FxCueId.CollectedUnit, 6, FxCueOrigin.Manual, .2f, 4), Is.False);
                Assert.That(cues.ActiveCount, Is.Zero);
            }
        }
        [Test] public void SameSpeciesNewSpecimenRetiresOldGeneration()
        {
            using (var cues = new FxCueRuntime())
            {
                var first = cues.Register("tank-a", "pondlet-id-a");
                cues.Play(first, FxCueId.PondletPlaced, 1, FxCueOrigin.Manual, .4f, 3);
                var next = cues.Register("tank-a", "pondlet-id-b");
                Assert.That(next, Is.Not.EqualTo(first)); Assert.That(cues.IsValid(first), Is.False);
                Assert.That(cues.ActiveCount, Is.Zero); Assert.That(cues.OwnerCount, Is.EqualTo(1));
                Assert.That(cues.Play(first, FxCueId.PondletPlaced, 2, FxCueOrigin.Manual, .4f, 3), Is.False);
            }
        }
        [Test] public void RetiredOwnerSlotAndResetEpochRejectOldToken()
        {
            using (var cues = new FxCueRuntime())
            {
                var first = cues.Register("one", null); cues.Unregister(first);
                var second = cues.Register("one", null); Assert.That(cues.IsValid(first), Is.False);
                cues.Reset(); var third = cues.Register("one", null);
                Assert.That(third.Epoch, Is.Not.EqualTo(second.Epoch)); Assert.That(cues.IsValid(second), Is.False);
                Assert.That(cues.Play(second, FxCueId.HandRefused, 1, FxCueOrigin.Manual, .18f, 5), Is.False);
            }
        }
        [Test] public void RepeatedRealUnitsCoalesceWithoutQueueingOldActions()
        {
            using (var cues = new FxCueRuntime())
            {
                var owner = cues.Register("tool", null);
                for (uint i = 1; i <= 200; i++) Assert.That(cues.Play(owner, FxCueId.CollectedUnit, i, FxCueOrigin.Manual, .28f, 4), Is.True);
                Assert.That(cues.ActiveCount, Is.EqualTo(1)); cues.Tick(.29f); Assert.That(cues.ActiveCount, Is.Zero);
            }
        }
        [Test] public void HigherPriorityTransactionSurvivesAmbientSaturation()
        {
            using (var cues = new FxCueRuntime())
            {
                for (int i = 0; i < FxCueRuntime.ActiveLimit; i++)
                    Assert.That(cues.Play(cues.Register("idle-" + i, null), FxCueId.PondletGlance, 1, FxCueOrigin.Manual, .6f, 1), Is.True);
                var service = cues.Register("service", null);
                Assert.That(cues.Play(service, FxCueId.HandRefused, 1, FxCueOrigin.Manual, .18f, 5), Is.True);
                Assert.That(cues.ActiveCount, Is.EqualTo(FxCueRuntime.ActiveLimit));
                Assert.That(cues.Play(cues.Register("late-idle", null), FxCueId.PondletGlance, 1, FxCueOrigin.Manual, .6f, 0), Is.False);
                cues.Cancel(service); Assert.That(cues.ActiveCount, Is.EqualTo(FxCueRuntime.ActiveLimit - 1));
            }
        }
        [Test] public void OwnerExhaustionHasNoUnboundedRegistry()
        {
            using (var cues = new FxCueRuntime())
            {
                for (int i = 0; i < 64; i++) Assert.That(cues.Register("owner-" + i, null).IsDefined, Is.True);
                Assert.That(cues.Register("overflow", null).IsDefined, Is.False);
                Assert.That(cues.OwnerCount, Is.EqualTo(64));
            }
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(1.6f)]
        public void MalformedLifetimeCannotCreateAnIndefiniteCue(float duration)
        { using (var cues = new FxCueRuntime()) Assert.That(cues.Play(cues.Register("tool", null), FxCueId.CollectedUnit, 1, FxCueOrigin.Manual, duration, 4), Is.False); }
        [Test] public void CancelledActionCannotReplayButANewActionCan()
        {
            using (var cues = new FxCueRuntime())
            {
                var owner = cues.Register("tool", null); cues.Play(owner, FxCueId.CollectedUnit, 1, FxCueOrigin.Manual, .28f, 4); cues.CancelAll();
                Assert.That(cues.Play(owner, FxCueId.CollectedUnit, 1, FxCueOrigin.Manual, .28f, 4), Is.False);
                Assert.That(cues.Play(owner, FxCueId.CollectedUnit, 2, FxCueOrigin.Manual, .28f, 4), Is.True);
            }
        }
        [Test] public void TickDoesNotAllocateManagedMemoryAfterWarmup()
        {
            using (var cues = new FxCueRuntime())
            {
                cues.Play(cues.Register("tool", null), FxCueId.CollectedUnit, 1, FxCueOrigin.Manual, .28f, 4); cues.Tick(.0001f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) cues.Tick(.0001f);
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(bytes, Is.Zero, "Only the new plain-C# cue tick is measured, not the whole game.");
            }
        }
        [Test] public void DisposedLifetimeCannotRegisterOrPlay()
        { var cues = new FxCueRuntime(); var owner = cues.Register("tool", null); cues.Dispose(); cues.Dispose(); Assert.That(cues.IsValid(owner), Is.False); Assert.That(cues.Register("tool", null).IsDefined, Is.False); }
    }
}
