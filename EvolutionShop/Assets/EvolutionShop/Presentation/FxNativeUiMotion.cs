using UnityEngine;

namespace EvolutionShop
{
    public enum FxNativeUiCue { None, CreditGain, CreditSpend, Ready, Discovery }

    public readonly struct FxNativeUiPose
    {
        public readonly float Alpha, Scale;
        public readonly Vector2 Offset;
        public readonly int CoinCount;
        public FxNativeUiPose(float alpha, float scale, Vector2 offset, int coins)
        { Alpha = alpha; Scale = scale; Offset = offset; CoinCount = coins; }
    }

    // Presentation envelopes only: no delayed values, controls or transaction authority.
    public static class FxNativeUiMotion
    {
        static bool Valid(float age) => !float.IsNaN(age) && !float.IsInfinity(age) && age >= 0;
        public static float Hover(float age) => Valid(age) ? Mathf.Clamp01(age / .10f) : 0;
        public static float Press(float age) => Decay(age, .18f);
        public static float Panel(float age) => Decay(age, .20f);
        static float Decay(float age, float duration)
        {
            if (!Valid(age) || age >= duration) return 0;
            float remaining = 1 - age / duration;
            return remaining * remaining;
        }
        public static FxNativeUiPose Sample(FxNativeUiCue kind, float age, bool reduced, bool low)
        {
            float duration = kind == FxNativeUiCue.Discovery ? 1.1f : kind == FxNativeUiCue.Ready ? .8f : .85f;
            if (kind == FxNativeUiCue.None || !Valid(age) || age >= duration)
                return new FxNativeUiPose(0, 1, Vector2.zero, 0);
            float progress = age / duration;
            float alpha = Mathf.Min(1, (1 - progress) * 3);
            bool money = kind == FxNativeUiCue.CreditGain || kind == FxNativeUiCue.CreditSpend;
            float lift = reduced ? 0 : Mathf.Sin(progress * Mathf.PI) * 8;
            return new FxNativeUiPose(alpha, reduced ? 1 : 1 + .06f * Mathf.Sin(progress * Mathf.PI),
                new Vector2(0, -lift), money && !reduced ? low ? 1 : 3 : 0);
        }
    }
}
