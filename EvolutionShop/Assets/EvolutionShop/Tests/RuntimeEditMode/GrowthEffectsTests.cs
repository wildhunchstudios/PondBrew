using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-25: the visual effects' parts that need no scene. Each texture is drawn here (nothing bought), shows, and is
    /// clear at its corners so no square shows round a particle; a stream keeps its rate whatever the frame rate and never runs
    /// backward; and Reduced motion and the Low preset each halve how much shows.</summary>
    public sealed class GrowthEffectsTests
    {
        [Test] public void EachTextureIsDrawnHereAndClearAtItsCorners()
        {
            int shapes = 0;
            foreach (var (name, alpha) in GrowthEffects.Shapes)
            {
                shapes++;
                float most = 0;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float a = alpha((x + 0.5f) / 32 * 2 - 1, (y + 0.5f) / 32 * 2 - 1);
                        Assert.That(float.IsNaN(a) || float.IsInfinity(a), Is.False, name + " is finite");
                        most = Mathf.Max(most, Mathf.Clamp01(a));
                    }
                Assert.That(most, Is.GreaterThan(0.5f), name + " shows");
                foreach (var (u, v) in new[] { (-0.98f, -0.98f), (0.98f, -0.98f), (-0.98f, 0.98f), (0.98f, 0.98f) })
                    Assert.That(Mathf.Clamp01(alpha(u, v)), Is.LessThan(0.02f), name + " is clear at its corners");
            }
            Assert.That(shapes, Is.EqualTo(8), "dot, ring, sparkle, heart, coin, puff, star and paper");
        }

        [Test] public void AStreamKeepsItsRateWhateverTheFrameRate()
        {
            foreach (float fps in new[] { 17f, 60f, 144f })
            {
                float clock = 0; int n = 0;
                for (int i = 0; i < Mathf.RoundToInt(fps * 2); i++) n += GrowthEffects.Stream(ref clock, 5f, 1f / fps);
                Assert.That(n, Is.InRange(9, 10), "5 a second for 2 seconds at " + fps + " frames a second");
            }
            float c = 0.4f;
            Assert.That(GrowthEffects.Stream(ref c, 0, 1), Is.EqualTo(0), "a stopped stream emits nothing");
            Assert.That(GrowthEffects.Stream(ref c, -3, 1), Is.EqualTo(0), "and never runs backward");
            Assert.That(c, Is.EqualTo(0.4f).Within(1e-6f), "its remainder kept for when it starts again");
        }

        [Test] public void ReducedMotionAndTheLowPresetEachHalveTheEffects()
        {
            Assert.That(GrowthEffects.IntensityFor(false, false), Is.EqualTo(1f));
            Assert.That(GrowthEffects.IntensityFor(true, false), Is.EqualTo(0.5f), "Reduced motion");
            Assert.That(GrowthEffects.IntensityFor(false, true), Is.EqualTo(0.5f), "the Low preset");
            Assert.That(GrowthEffects.IntensityFor(true, true), Is.EqualTo(0.25f), "both");
        }
    }
}
