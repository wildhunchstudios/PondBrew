using NUnit.Framework;
using UnityEngine;
namespace EvolutionShop.Tests
{
    public sealed class FxNativeUiTests
    {
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static void Bounded(FxNativeUiPose p)
        {
            Assert.That(Finite(p.Alpha) && Finite(p.Scale) && Finite(p.Offset.x) && Finite(p.Offset.y), Is.True);
            Assert.That(p.Alpha, Is.InRange(0f, 1f));
            Assert.That(p.Scale, Is.InRange(.5f, 1.5f));
            Assert.That(p.Offset.magnitude, Is.LessThanOrEqualTo(32f));
            Assert.That(p.CoinCount, Is.InRange(0, 3));
        }
        [Test] public void AllCuesRemainFiniteThroughOrdinaryAndInvalidAges()
        {
            foreach (FxNativeUiCue kind in System.Enum.GetValues(typeof(FxNativeUiCue)))
                foreach (float age in new[] { -1f, 0f, .001f, .08f, .25f, .7f, 1.1f, 100f, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    foreach (bool reduced in new[] { false, true })
                        foreach (bool low in new[] { false, true }) Bounded(FxNativeUiMotion.Sample(kind, age, reduced, low));
        }
        [Test] public void SettledCuesCannotKeepMovingCoinsOrVisibleSuccess()
        {
            foreach (FxNativeUiCue kind in System.Enum.GetValues(typeof(FxNativeUiCue)))
            {
                var p = FxNativeUiMotion.Sample(kind, 10f, false, false);
                Assert.That(p.Alpha, Is.Zero);
                Assert.That(p.CoinCount, Is.Zero);
            }
        }
        [Test] public void ComfortPoliciesBoundMovingDecorationsWithoutRemovingReadableAccent()
        {
            foreach (var kind in new[] { FxNativeUiCue.CreditGain, FxNativeUiCue.CreditSpend })
            {
                bool visible = false;
                for (int i = 1; i < 70; i++)
                {
                    float t = i * .01f;
                    var normal = FxNativeUiMotion.Sample(kind, t, false, false);
                    var low = FxNativeUiMotion.Sample(kind, t, false, true);
                    var reduced = FxNativeUiMotion.Sample(kind, t, true, false);
                    Assert.That(low.CoinCount, Is.LessThanOrEqualTo(1));
                    Assert.That(reduced.CoinCount, Is.Zero);
                    Assert.That(reduced.Offset, Is.EqualTo(Vector2.zero));
                    Assert.That(reduced.Scale, Is.EqualTo(1f));
                    visible |= reduced.Alpha > 0 && normal.Alpha > 0;
                }
                Assert.That(visible, Is.True);
            }
        }
        [Test] public void InteractionEnvelopesAreBoundedAndPanelSettlesMonotonically()
        {
            float previous = FxNativeUiMotion.Panel(0);
            for (int i = 0; i <= 200; i++)
            {
                float age = i * .005f;
                float panel = FxNativeUiMotion.Panel(age);
                Assert.That(panel, Is.InRange(0f, 1f));
                Assert.That(panel, Is.LessThanOrEqualTo(previous + .00001f));
                previous = panel;
                Assert.That(FxNativeUiMotion.Hover(age), Is.InRange(0f, 1f));
                Assert.That(FxNativeUiMotion.Press(age), Is.InRange(0f, 1f));
            }
            Assert.That(previous, Is.Zero);
            Assert.That(FxNativeUiMotion.Press(1), Is.Zero);
        }
        [Test] public void InvalidInteractionAgesNeverProduceNonfiniteGeometry()
        {
            foreach (float age in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
                foreach (float value in new[] { FxNativeUiMotion.Hover(age), FxNativeUiMotion.Press(age), FxNativeUiMotion.Panel(age) })
                    Assert.That(Finite(value) && value >= 0 && value <= 1, Is.True);
        }
        [Test] public void RepeatedSamplingHasNoHistoryOrAccumulatedOffsets()
        {
            var first = FxNativeUiMotion.Sample(FxNativeUiCue.Discovery, .15f, false, false);
            for (int i = 0; i < 1000; i++)
            {
                FxNativeUiMotion.Sample(FxNativeUiCue.CreditSpend, i * .1f, true, true);
                var again = FxNativeUiMotion.Sample(FxNativeUiCue.Discovery, .15f, false, false);
                Assert.That(again.Offset, Is.EqualTo(first.Offset));
                Assert.That(again.Alpha, Is.EqualTo(first.Alpha));
                Assert.That(again.Scale, Is.EqualTo(first.Scale));
            }
        }
    }
}
