using System;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>The UX-08 feel animations (UX.9). Each is a short, self-ending pose change on one element.</summary>
    public enum UiMotionKind { CardOpen, CardClose, Press, DisabledShake, PriceWiggle, FaceBounce, StampDown, TipSlide, ReasonFlash, ChipBump, ViewOpen }

    /// <summary>Which inline style properties a pose sets; the rest keep their stylesheet values.</summary>
    [Flags]
    public enum UiPoseChannels { None = 0, Opacity = 1, Scale = 2, Translate = 4, Rotate = 8 }

    /// <summary>One frame of a feel animation: opacity, scale, offset (panel pixels, y down) and rotation (degrees).</summary>
    public readonly struct UiPose
    {
        public readonly UiPoseChannels Channels;
        public readonly float Opacity, ScaleX, ScaleY, OffsetX, OffsetY, Rotate;

        public UiPose(UiPoseChannels channels, float opacity = 1, float scaleX = 1, float scaleY = 1, float offsetX = 0, float offsetY = 0, float rotate = 0)
        {
            Channels = channels; Opacity = opacity; ScaleX = scaleX; ScaleY = scaleY; OffsetX = offsetX; OffsetY = offsetY; Rotate = rotate;
        }

        public static UiPose Rest => new UiPose(UiPoseChannels.None);
        public bool Has(UiPoseChannels channel) => (Channels & channel) != 0;
    }

    /// <summary>
    /// Pure timing and shape rules for the UX-08 feel pass, kept apart so EditMode tests can reach them. Reduced motion
    /// keeps every animation's timing but drops scale, shake, slide and flight: it fades instead (UX.9). Low effects detail
    /// only drops the flying coins, the UI's one particle effect. Counting numbers and sounds stay in every mode.
    /// </summary>
    public static class UiMotion
    {
        public const float CardOpenSeconds = .12f, CardCloseSeconds = .08f, PressSeconds = .06f, ShakeSeconds = .2f, ShakePixels = 2f;
        public const float WiggleSeconds = .28f, BounceSeconds = .22f, StampSeconds = .26f, TipSlideSeconds = .24f, ReasonFlashSeconds = .6f, ChipBumpSeconds = .18f;
        /// <summary>The notebook and screens come up with a fade from this opacity (never 0, so they never read as hidden).</summary>
        public const float ViewOpenSeconds = .12f, ViewOpenFrom = .3f;
        public const float CardOpenFrom = .94f, CardCloseTo = .96f, StampFrom = 1.6f, StampTiltFrom = -9f;
        /// <summary>The discovery stamp's resting tilt; HudStyles.uss sets the same rotate on .es-stamp.</summary>
        public const float StampRestDegrees = -2f;
        public const float TipSlidePixels = 18f, WiggleDegrees = 6f;
        /// <summary>The day report's tallies count up over this long (UX.9).</summary>
        public const float CountUpSeconds = .6f;
        /// <summary>The money chip counts to its new value over this long, starting when the first coin lands (or at once).</summary>
        public const float MoneyTickSeconds = .3f;
        public const int Coins = 5;
        public const float CoinFlightSeconds = .36f, CoinStagger = .0225f;
        /// <summary>From the first coin's launch to the last coin's landing: about 450 ms (UX.9).</summary>
        public static float CoinTotalSeconds => CoinFlightSeconds + (Coins - 1) * CoinStagger;

        public static float Duration(UiMotionKind kind)
        {
            switch (kind)
            {
                case UiMotionKind.CardOpen: return CardOpenSeconds;
                case UiMotionKind.CardClose: return CardCloseSeconds;
                case UiMotionKind.Press: return PressSeconds;
                case UiMotionKind.DisabledShake: return ShakeSeconds;
                case UiMotionKind.PriceWiggle: return WiggleSeconds;
                case UiMotionKind.FaceBounce: return BounceSeconds;
                case UiMotionKind.StampDown: return StampSeconds;
                case UiMotionKind.TipSlide: return TipSlideSeconds;
                case UiMotionKind.ReasonFlash: return ReasonFlashSeconds;
                case UiMotionKind.ChipBump: return ChipBumpSeconds;
                case UiMotionKind.ViewOpen: return ViewOpenSeconds;
                default: return 0;
            }
        }

        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1 - t; return 1 - u * u * u; }
        public static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float EaseInOutQuad(float t) { t = Mathf.Clamp01(t); return t < .5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t); }

        /// <summary>Ease-out with a slight overshoot past 1 before settling (the "back" curve).</summary>
        public static float EaseOutBack(float t, float overshoot = 2.2f)
        {
            t = Mathf.Clamp01(t);
            float u = t - 1;
            return 1 + (overshoot + 1) * u * u * u + overshoot * u * u;
        }

        static float Pulse(float t) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));

        /// <summary>The pose at <paramref name="age"/> seconds; past the duration every kind is back at rest.</summary>
        public static UiPose Sample(UiMotionKind kind, float age, bool reduced)
        {
            float seconds = Duration(kind);
            if (seconds <= 0 || float.IsNaN(age) || age >= seconds) return UiPose.Rest;
            float t = Mathf.Clamp01(age / seconds);
            switch (kind)
            {
                case UiMotionKind.CardOpen:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: .2f + .8f * EaseOutCubic(t));
                    {
                        float s = Mathf.LerpUnclamped(CardOpenFrom, 1, EaseOutBack(t));
                        return new UiPose(UiPoseChannels.Opacity | UiPoseChannels.Scale, EaseOutCubic(t / .6f), s, s);
                    }
                case UiMotionKind.CardClose:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: 1 - t);
                    {
                        float s = Mathf.Lerp(1, CardCloseTo, t);
                        return new UiPose(UiPoseChannels.Opacity | UiPoseChannels.Scale, 1 - t, s, s);
                    }
                case UiMotionKind.Press:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: 1 - .2f * Pulse(t));
                    return new UiPose(UiPoseChannels.Scale, scaleX: 1 + .04f * Pulse(t), scaleY: 1 - .08f * Pulse(t));
                case UiMotionKind.DisabledShake:
                    // Reduced: no shake; the dull sound and the reason still tell the player why.
                    if (reduced) return UiPose.Rest;
                    // Three decaying swings; .9182 is the first swing's peak, so it reaches the full two pixels.
                    return new UiPose(UiPoseChannels.Translate, offsetX: ShakePixels * Mathf.Sin(2 * Mathf.PI * 3 * t) * (1 - t) / .9182f);
                case UiMotionKind.PriceWiggle:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: 1 - .35f * Pulse(t));
                    {
                        float s = 1 + .08f * Pulse(t);
                        return new UiPose(UiPoseChannels.Rotate | UiPoseChannels.Scale, scaleX: s, scaleY: s, rotate: WiggleDegrees * Mathf.Sin(2 * Mathf.PI * 2 * t) * (1 - t));
                    }
                case UiMotionKind.FaceBounce:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: .4f + .6f * EaseOutCubic(t));
                    {
                        float s = 1 + .14f * Pulse(t);
                        return new UiPose(UiPoseChannels.Scale | UiPoseChannels.Translate, scaleX: s, scaleY: s, offsetY: -6f * Pulse(t));
                    }
                case UiMotionKind.StampDown:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: EaseOutCubic(t));
                    {
                        // Falls from above (large) onto the paper by 70%, then a small squash as it lands.
                        float s = t < .7f ? Mathf.Lerp(StampFrom, 1, EaseInCubic(t / .7f)) : 1 - .05f * Pulse((t - .7f) / .3f);
                        float tilt = Mathf.Lerp(StampTiltFrom, StampRestDegrees, EaseOutCubic(t / .7f));
                        return new UiPose(UiPoseChannels.Opacity | UiPoseChannels.Scale | UiPoseChannels.Rotate, Mathf.Clamp01(t / .35f), s, s, rotate: tilt);
                    }
                case UiMotionKind.TipSlide:
                    if (reduced) return new UiPose(UiPoseChannels.Opacity, opacity: EaseOutCubic(t));
                    return new UiPose(UiPoseChannels.Opacity | UiPoseChannels.Translate, EaseOutCubic(t), offsetY: TipSlidePixels * (1 - EaseOutCubic(t)));
                case UiMotionKind.ReasonFlash:
                    // The highlight itself is a class (es-card__reason--flash) held for the whole flash in every mode.
                    if (reduced) return UiPose.Rest;
                    {
                        float s = 1 + .05f * Pulse(t / .35f);
                        return new UiPose(UiPoseChannels.Scale, scaleX: s, scaleY: s);
                    }
                case UiMotionKind.ViewOpen:
                    // A fade only, in every mode: full-screen views move nothing a player or a layout check could lose.
                    return new UiPose(UiPoseChannels.Opacity, opacity: Mathf.Lerp(ViewOpenFrom, 1, EaseOutCubic(t)));
                case UiMotionKind.ChipBump:
                    if (reduced) return UiPose.Rest;
                    {
                        float s = 1 + .12f * Pulse(t);
                        return new UiPose(UiPoseChannels.Scale, scaleX: s, scaleY: s);
                    }
                default: return UiPose.Rest;
            }
        }

        /// <summary>A tally counting from <paramref name="from"/> to <paramref name="to"/>; exactly <paramref name="to"/> once done.</summary>
        public static long CountUp(long from, long to, float age, float seconds)
        {
            if (seconds <= 0 || float.IsNaN(age) || age >= seconds || from == to) return to;
            if (age <= 0) return from;
            double k = EaseOutCubic(age / seconds);
            long value = from + (long)Math.Round((to - from) * k, MidpointRounding.AwayFromZero);
            return to >= from ? Math.Min(value, to) : Math.Max(value, to);
        }

        /// <summary>How many coins fly on a sale: none with reduced motion (no flight) or Low effects detail (no UI particles).</summary>
        public static int CoinCount(bool reduced, bool low) => reduced || low ? 0 : Coins;

        /// <summary>
        /// Coin <paramref name="index"/>'s place on its arc from the counter to the money chip, <paramref name="age"/> seconds after
        /// the sale. False before it launches and after it lands.
        /// </summary>
        public static bool CoinAt(Vector2 from, Vector2 to, int index, float age, out Vector2 at, out float scale)
        {
            at = from; scale = 1;
            float start = index * CoinStagger;
            if (float.IsNaN(age) || age < start || age >= start + CoinFlightSeconds) return false;
            float t = EaseInOutQuad((age - start) / CoinFlightSeconds);
            Vector2 mid = (from + to) * .5f;
            float lift = Mathf.Max(60f, Vector2.Distance(from, to) * .25f);
            // Each coin takes a slightly different arc so the five read as a spray, not one line.
            Vector2 control = mid + new Vector2((index - (Coins - 1) * .5f) * 14f, -lift - index * 6f);
            float u = 1 - t;
            at = u * u * from + 2 * u * t * control + t * t * to;
            scale = Mathf.Lerp(1.1f, .7f, t);
            return true;
        }
    }
}
