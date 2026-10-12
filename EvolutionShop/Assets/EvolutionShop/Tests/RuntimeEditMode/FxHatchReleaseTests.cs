using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxHatchReleaseTests
    {
        [TestCase(true, false, false)] [TestCase(false, false, false)]
        [TestCase(true, true, false)] [TestCase(false, true, false)] [TestCase(true, true, true)]
        public void EverySeedFitsActionBudgetVesselAndCueLifetime(bool first, bool low, bool reduced)
        {
            for (uint seed = 0; seed < 100; seed++)
            {
                int count = FxHatchRelease.Count(first, low, reduced);
                Assert.That(count, Is.LessThanOrEqualTo(reduced ? 1 : low ? 24 : 64));
                for (int i = 0; i < count; i++)
                {
                    FxHatchRelease.Sample(i, seed, first, reduced, out var delay, out var origin, out var p);
                    Assert.That(p.IsValid, Is.True);
                    Assert.That(delay + p.Lifetime, Is.LessThanOrEqualTo(FxHatchChoreography.Duration(first) - FxHatchChoreography.Release(first) + .0001f));
                    Assert.That(Mathf.Abs(origin.x), Is.LessThan(.3f));
                    Assert.That(origin.y, Is.InRange(1.3f, 1.66f));
                    Assert.That(origin.z, Is.InRange(-.64f, -.55f));
                    Assert.That(Mathf.Abs(origin.x + p.End.x), Is.LessThan(.85f));
                    Assert.That(origin.y + p.End.y, Is.LessThan(2.4f));
                }
            }
        }
        [Test] public void ReleaseHasTemporalDepthAndScaleVariationWithoutChangingGameRandom()
        {
            var saved = Random.state; Random.InitState(93); var before = Random.state;
            float earliest = 1, latest = 0, smallest = 1, largest = 0, front = 10, back = -10;
            int bubble = 0, accent = 0;
            for (int i = 0; i < FxHatchRelease.Count(true, false, false); i++)
            {
                FxHatchRelease.Sample(i, 19, true, false, out var delay, out var origin, out var p);
                earliest = Mathf.Min(earliest, delay); latest = Mathf.Max(latest, delay);
                smallest = Mathf.Min(smallest, p.Size); largest = Mathf.Max(largest, p.Size);
                front = Mathf.Min(front, (origin + p.End).z); back = Mathf.Max(back, (origin + p.End).z);
                if (p.Shape == FxParticleShape.Bubble) bubble++; else accent++;
            }
            var after = Random.state; Random.state = saved;
            Assert.That(after, Is.EqualTo(before));
            Assert.That(latest - earliest, Is.GreaterThan(.2f));
            Assert.That(largest / smallest, Is.GreaterThan(2));
            Assert.That(back - front, Is.GreaterThan(.12f));
            Assert.That(bubble, Is.GreaterThan(accent * 3));
        }
    }
}
