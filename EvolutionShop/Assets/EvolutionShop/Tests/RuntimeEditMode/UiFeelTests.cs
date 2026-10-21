using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop.Tests
{
    // UX-08 feel pass (UX.9): the motion rules' timings and shapes, reduced motion as fades only, Low as no coins, the count-up,
    // the coin path, the generated UI sounds, and the styles and strings the feel uses.
    public sealed class UiFeelTests
    {
        static string Sheet(string name) => File.ReadAllText(Path.Combine(Application.dataPath, "EvolutionShop/Resources/EvolutionShopUI", name));

        static UiMotionKind[] Kinds => (UiMotionKind[])Enum.GetValues(typeof(UiMotionKind));

        [Test] public void EachAnimationTakesItsSpecifiedTimeAndEndsAtRest()
        {
            Assert.That(UiMotion.Duration(UiMotionKind.CardOpen), Is.EqualTo(.12f).Within(1e-6), "card open 120 ms");
            Assert.That(UiMotion.Duration(UiMotionKind.CardClose), Is.EqualTo(.08f).Within(1e-6), "card close 80 ms");
            Assert.That(UiMotion.Duration(UiMotionKind.Press), Is.EqualTo(.06f).Within(1e-6), "button squash 60 ms");
            Assert.That(UiMotion.CountUpSeconds, Is.EqualTo(.6f).Within(1e-6), "receipt tallies 600 ms");
            Assert.That(UiMotion.CoinTotalSeconds, Is.EqualTo(.45f).Within(.001f), "coins about 450 ms");
            foreach (var kind in Kinds)
            {
                float d = UiMotion.Duration(kind);
                Assert.That(d, Is.GreaterThan(0).And.LessThanOrEqualTo(.6f), kind.ToString());
                foreach (bool reduced in new[] { false, true })
                {
                    Assert.That(UiMotion.Sample(kind, d, reduced).Channels, Is.EqualTo(UiPoseChannels.None), kind + " ends at rest");
                    Assert.That(UiMotion.Sample(kind, d + 5, reduced).Channels, Is.EqualTo(UiPoseChannels.None), kind + " stays at rest");
                    Assert.That(UiMotion.Sample(kind, float.NaN, reduced).Channels, Is.EqualTo(UiPoseChannels.None), kind + " ignores a bad clock");
                }
            }
        }

        [Test] public void TheCardOpensFromNinetyFourPercentWithASlightOvershoot()
        {
            var first = UiMotion.Sample(UiMotionKind.CardOpen, 0, false);
            Assert.That(first.ScaleX, Is.EqualTo(UiMotion.CardOpenFrom).Within(1e-5));
            Assert.That(first.Opacity, Is.EqualTo(0).Within(1e-5));
            float peak = 0, lastOpacity = -1;
            for (int i = 0; i < 1200; i++)
            {
                var p = UiMotion.Sample(UiMotionKind.CardOpen, i * .0001f, false);
                Assert.That(p.ScaleX, Is.EqualTo(p.ScaleY), "uniform");
                Assert.That(p.Opacity, Is.GreaterThanOrEqualTo(lastOpacity - 1e-6f), "fades in, never out");
                lastOpacity = p.Opacity;
                peak = Mathf.Max(peak, p.ScaleX);
            }
            Assert.That(peak, Is.GreaterThan(1.002f).And.LessThan(1.02f), "slight overshoot past full size");
        }

        [Test] public void TheCardClosesByFadingAndShrinkingABit()
        {
            var mid = UiMotion.Sample(UiMotionKind.CardClose, .04f, false);
            Assert.That(mid.Opacity, Is.EqualTo(.5f).Within(.01f));
            Assert.That(mid.ScaleX, Is.EqualTo(.98f).Within(.001f));
            Assert.That(UiMotion.Sample(UiMotionKind.CardClose, .0799f, false).ScaleX, Is.GreaterThanOrEqualTo(UiMotion.CardCloseTo - 1e-4f));
        }

        [Test] public void APressSquashesWideAndShortThenSpringsBack()
        {
            var mid = UiMotion.Sample(UiMotionKind.Press, .03f, false);
            Assert.That(mid.Channels, Is.EqualTo(UiPoseChannels.Scale));
            Assert.That(mid.ScaleX, Is.EqualTo(1.04f).Within(.001f));
            Assert.That(mid.ScaleY, Is.EqualTo(.92f).Within(.001f));
            Assert.That(UiMotion.Sample(UiMotionKind.Press, 0, false).ScaleY, Is.EqualTo(1).Within(1e-5));
        }

        [Test] public void ADisabledPressShakesTwoPixels()
        {
            float max = 0, end = 0;
            for (int i = 0; i < 2000; i++)
            {
                var p = UiMotion.Sample(UiMotionKind.DisabledShake, i * .0001f, false);
                Assert.That(p.Channels, Is.EqualTo(UiPoseChannels.Translate), "moves only, no scale");
                Assert.That(p.OffsetY, Is.EqualTo(0), "side to side only");
                max = Mathf.Max(max, Mathf.Abs(p.OffsetX));
                end = p.OffsetX;
            }
            Assert.That(max, Is.GreaterThan(1.97f).And.LessThanOrEqualTo(UiMotion.ShakePixels + 1e-4f));
            Assert.That(Mathf.Abs(end), Is.LessThan(.05f), "it dies down");
        }

        [Test] public void ReducedMotionOnlyFades()
        {
            foreach (var kind in Kinds)
            {
                float d = UiMotion.Duration(kind);
                bool fades = false;
                for (int i = 0; i < 200; i++)
                {
                    var p = UiMotion.Sample(kind, d * i / 200f, true);
                    Assert.That(p.Channels & ~UiPoseChannels.Opacity, Is.EqualTo(UiPoseChannels.None), kind + ": no scale, shake, slide or tilt with reduced motion");
                    if (p.Has(UiPoseChannels.Opacity) && p.Opacity < .95f) fades = true;
                }
                bool shouldFade = kind != UiMotionKind.DisabledShake && kind != UiMotionKind.ReasonFlash && kind != UiMotionKind.ChipBump;
                Assert.That(fades, Is.EqualTo(shouldFade), kind + (shouldFade ? " fades instead" : " has no reduced pose (sound and the reason's highlight remain)"));
            }
        }

        [Test] public void TheNotebookAndScreensOnlyFadeInFromThirtyPercent()
        {
            foreach (bool reduced in new[] { false, true })
            {
                var first = UiMotion.Sample(UiMotionKind.ViewOpen, 0, reduced);
                Assert.That(first.Channels, Is.EqualTo(UiPoseChannels.Opacity), "a fade only, so nothing a layout check measures moves");
                Assert.That(first.Opacity, Is.EqualTo(UiMotion.ViewOpenFrom).Within(1e-5), "never hidden (HudPresenter.Visible needs 0.05)");
                float last = 0;
                for (int i = 0; i <= 120; i++)
                {
                    var p = UiMotion.Sample(UiMotionKind.ViewOpen, i * .001f, reduced);
                    if (!p.Has(UiPoseChannels.Opacity)) break;
                    Assert.That(p.Channels & ~UiPoseChannels.Opacity, Is.EqualTo(UiPoseChannels.None));
                    Assert.That(p.Opacity, Is.GreaterThanOrEqualTo(last - 1e-6f));
                    last = p.Opacity;
                }
            }
            Assert.That(UiMotion.Duration(UiMotionKind.ViewOpen), Is.EqualTo(.12f).Within(1e-6));
        }

        [Test] public void LowDetailAndReducedMotionSendNoCoins()
        {
            Assert.That(UiMotion.CoinCount(false, false), Is.EqualTo(5));
            Assert.That(UiMotion.CoinCount(true, false), Is.EqualTo(0), "reduced: no flight");
            Assert.That(UiMotion.CoinCount(false, true), Is.EqualTo(0), "Low: no particles on UI");
            Assert.That(UiMotion.CoinCount(true, true), Is.EqualTo(0));
        }

        [Test] public void CoinsFlyFromTheCounterIntoTheChip()
        {
            var from = new Vector2(400, 700);
            var to = new Vector2(1700, 70);
            for (int i = 0; i < UiMotion.Coins; i++)
            {
                float start = i * UiMotion.CoinStagger;
                Assert.That(UiMotion.CoinAt(from, to, i, start - .001f, out _, out _), Is.False, "coin " + i + " waits for its turn");
                Assert.That(UiMotion.CoinAt(from, to, i, start, out var at, out var scale), Is.True);
                Assert.That(Vector2.Distance(at, from), Is.LessThan(.5f), "coin " + i + " leaves from the counter");
                Assert.That(UiMotion.CoinAt(from, to, i, start + UiMotion.CoinFlightSeconds - .0005f, out at, out _), Is.True);
                Assert.That(Vector2.Distance(at, to), Is.LessThan(2f), "coin " + i + " lands on the chip");
                Assert.That(UiMotion.CoinAt(from, to, i, start + UiMotion.CoinFlightSeconds + 1e-4f, out _, out _), Is.False, "then it is gone");
                Assert.That(UiMotion.CoinAt(from, to, i, start + UiMotion.CoinFlightSeconds * .5f, out at, out _), Is.True);
                Assert.That(at.y, Is.LessThan(Mathf.Min(from.y, to.y) + (Mathf.Max(from.y, to.y) - Mathf.Min(from.y, to.y)) * .5f), "it arcs up on the way");
            }
            Assert.That(UiMotion.CoinTotalSeconds, Is.EqualTo((UiMotion.Coins - 1) * UiMotion.CoinStagger + UiMotion.CoinFlightSeconds).Within(1e-6));
        }

        [Test] public void TalliesCountUpToTheirTotalExactly()
        {
            foreach (long total in new long[] { 0, 1, 7, 1234, 98765, -500 })
            {
                long last = 0;
                for (int i = 0; i <= 700; i++)
                {
                    long v = UiMotion.CountUp(0, total, i * .001f, UiMotion.CountUpSeconds);
                    if (total >= 0) { Assert.That(v, Is.GreaterThanOrEqualTo(last).And.LessThanOrEqualTo(total), total + " counts up without passing it"); }
                    else { Assert.That(v, Is.LessThanOrEqualTo(last).And.GreaterThanOrEqualTo(total), total + " counts down without passing it"); }
                    last = v;
                }
                Assert.That(UiMotion.CountUp(0, total, UiMotion.CountUpSeconds, UiMotion.CountUpSeconds), Is.EqualTo(total), "exactly the total once done");
            }
            Assert.That(UiMotion.CountUp(10, 20, 0, .3f), Is.EqualTo(10), "starts where it was");
            Assert.That(UiMotion.CountUp(10, 20, -1, .3f), Is.EqualTo(10), "waits before it starts (the coins' flight)");
            Assert.That(UiMotion.CountUp(10, 20, .15f, .3f), Is.GreaterThan(10).And.LessThan(20), "between on the way");
            Assert.That(UiMotion.CountUp(30, 20, .3f, .3f), Is.EqualTo(20), "spending counts down to the new amount");
        }

        [Test] public void TheStampFallsAndLandsAtItsStylesheetTilt()
        {
            var first = UiMotion.Sample(UiMotionKind.StampDown, 0, false);
            Assert.That(first.ScaleX, Is.EqualTo(UiMotion.StampFrom).Within(1e-5), "it starts large, above the paper");
            Assert.That(first.Rotate, Is.EqualTo(UiMotion.StampTiltFrom).Within(1e-4));
            Assert.That(first.Opacity, Is.EqualTo(0).Within(1e-5));
            var landing = UiMotion.Sample(UiMotionKind.StampDown, UiMotion.StampSeconds * .7f, false);
            Assert.That(landing.ScaleX, Is.EqualTo(1).Within(.002f), "it reaches full size as it lands");
            Assert.That(landing.Rotate, Is.EqualTo(UiMotion.StampRestDegrees).Within(.01f));
            var squash = UiMotion.Sample(UiMotionKind.StampDown, UiMotion.StampSeconds * .85f, false);
            Assert.That(squash.ScaleX, Is.LessThan(1f).And.GreaterThan(.94f), "a small squash on landing");
            string hud = Sheet("HudStyles.uss");
            int block = hud.IndexOf(".es-stamp {", StringComparison.Ordinal);
            Assert.That(block, Is.GreaterThanOrEqualTo(0));
            string stamp = hud.Substring(block, hud.IndexOf('}', block) - block);
            Assert.That(stamp, Does.Contain("rotate: " + UiMotion.StampRestDegrees.ToString("0") + "deg"), "the animation ends where the stylesheet rests");
        }

        [Test] public void ThePriceTagWigglesAndAFaceHops()
        {
            float tilt = 0, swell = 0, hop = 0, grow = 0;
            for (int i = 0; i < 300; i++)
            {
                var w = UiMotion.Sample(UiMotionKind.PriceWiggle, i * .001f, false);
                tilt = Mathf.Max(tilt, Mathf.Abs(w.Rotate)); swell = Mathf.Max(swell, w.ScaleX);
                var b = UiMotion.Sample(UiMotionKind.FaceBounce, i * .001f, false);
                hop = Mathf.Min(hop, b.OffsetY); grow = Mathf.Max(grow, b.ScaleX);
            }
            Assert.That(tilt, Is.GreaterThan(4f).And.LessThanOrEqualTo(UiMotion.WiggleDegrees));
            Assert.That(swell, Is.GreaterThan(1.07f).And.LessThan(1.09f));
            Assert.That(hop, Is.LessThan(-5f), "the face hops up");
            Assert.That(grow, Is.GreaterThan(1.13f).And.LessThan(1.15f));
        }

        [Test] public void TheAnimatorRunsItsCallbackAndClearsItsInlineStyle()
        {
            var animator = new UiAnimator();
            var e = new VisualElement();
            bool done = false;
            animator.Play(e, UiMotionKind.CardClose, () => done = true);
            Assert.That(animator.IsPlaying(e, UiMotionKind.CardClose) && animator.Played(UiMotionKind.CardClose) == 1);
            // A detached element (no panel) ends at once and back at its stylesheet values.
            animator.Advance(.01f);
            animator.Tick(false);
            Assert.That(done && animator.Idle);
            Assert.That(e.style.opacity.keyword, Is.Not.EqualTo(StyleKeyword.Undefined));
            Assert.That(e.style.scale.keyword, Is.Not.EqualTo(StyleKeyword.Undefined));
            float before = animator.Now;
            animator.Advance(5);
            Assert.That(animator.Now - before, Is.EqualTo(.25f).Within(1e-6), "a long hitch ends animations rather than replaying them");
            animator.TimeScale = .5f;
            before = animator.Now;
            animator.Advance(.1f);
            Assert.That(animator.Now - before, Is.EqualTo(.05f).Within(1e-6));
            animator.Play(e, UiMotionKind.Press);
            animator.Play(e, UiMotionKind.ChipBump);
            Assert.That(animator.Active, Is.EqualTo(1), "one animation per element: the newest replaces it");
            animator.Stop(e);
            Assert.That(animator.Idle);
        }

        [Test] public void TheUiSoundsAreGeneratedSoftAndAlwaysTheSame()
        {
            float[] previous = null;
            foreach (UiSound sound in Enum.GetValues(typeof(UiSound)))
            {
                var a = UiSoundSynth.Samples(sound);
                var b = UiSoundSynth.Samples(sound);
                Assert.That(a.Length, Is.EqualTo(Mathf.RoundToInt(UiSoundSynth.Seconds(sound) * UiSoundSynth.Rate)), sound.ToString());
                Assert.That(a, Is.EqualTo(b), sound + " is deterministic");
                float peak = 0;
                foreach (float v in a) { Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False); peak = Mathf.Max(peak, Mathf.Abs(v)); }
                Assert.That(peak, Is.GreaterThan(.05f).And.LessThan(.35f), sound + " peak " + peak);
                Assert.That(Mathf.Abs(a[0]), Is.LessThan(.01f), sound + " starts softly");
                Assert.That(Mathf.Abs(a[a.Length - 1]), Is.LessThan(.01f), sound + " ends softly");
                if (previous != null) Assert.That(a, Is.Not.EqualTo(previous), sound + " differs from the one before");
                previous = a;
            }
            Assert.That(UiSoundSynth.Seconds(UiSound.KaChing), Is.GreaterThan(UiSoundSynth.Seconds(UiSound.Tick)), "the sale's ka-ching is the longest");
        }

        [Test] public void TheFeelStylesAndStringsAreWhereTheyAreUsed()
        {
            Assert.That(Sheet("HudStyles.uss"), Does.Contain(".es-coin {"));
            Assert.That(Sheet("CardStyles.uss"), Does.Contain("." + StationCardPresenter.ReasonFlashClass + " {"));
            string tokens = Sheet("ShopTokens.uss");
            Assert.That(tokens, Does.Contain(".es-press-tip {"));
            int button = tokens.IndexOf(".es-button {", StringComparison.Ordinal);
            string block = tokens.Substring(button, tokens.IndexOf('}', button) - button);
            Assert.That(block, Does.Contain("transition-property: background-color, border-color, color;"), "buttons ease between their colours; nothing moves under the pointer");
            Assert.That(block, Does.Not.Contain("translate"));
            var strings = ShopUiStrings.LoadDefault();
            Assert.That(strings.Has("card.tank.why.min") && strings.Has("card.tank.why.max"), "the price steps say why at their limits");
        }
    }
}
