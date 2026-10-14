using UnityEngine;

namespace EvolutionShop
{
    public enum FxAdvancedLabEvent { None, SparkUnlocked, MistUnlocked, ConditioningStarted, ConditioningCancelled, ConditioningOutput }

    /// <summary>Root-local cosmetic recipes. Counts, progress and command outcomes remain model-owned.</summary>
    public static class FxAdvancedLabMotion
    {
        public static bool IsAdvanced(CultureKind culture) => culture == CultureKind.Spark || culture == CultureKind.Mist;
        public static Vector3 SourcePoint(CultureKind culture) => culture == CultureKind.Spark ? new Vector3(0, 1.5f, -.27f) : new Vector3(0, .85f, -.53f);
        public static Color CultureColor(CultureKind culture) => culture == CultureKind.Spark ? new Color(1, .82f, .39f, .88f) : new Color(.65f, .96f, .93f, .72f);
        static float Noise(uint index, uint salt)
        {
            uint n = unchecked(index * 747796405u + salt * 2891336453u + 277803737u);
            n = unchecked((n ^ (n >> 16)) * 2246822519u);
            return (n & 65535u) / 65535f;
        }
        public static Vector3 SourceCell(CultureKind culture, int index, float time, bool reduced)
        {
            if (!FxParticleRequest.Finite(time)) time = 0;
            float phase = index * 2.399f, t = reduced ? 0 : time;
            if (culture == CultureKind.Spark) {
                float angle = phase + t * .32f;
                return new Vector3(Mathf.Cos(angle) * .13f, 1.27f + index % 3 * .20f + Mathf.Sin(t * 1.1f + phase) * .022f, Mathf.Sin(angle) * .13f);
            }
            // These are the six real stock cells on the existing supply strip, not an invented reservoir.
            float x = index == 0 ? -.52f : index == 1 ? -.29f : index == 2 ? -.075f : index == 3 ? .17f : index == 4 ? .39f : .58f;
            float y = index == 0 ? .86f : index == 1 ? .805f : index == 2 ? .89f : index == 3 ? .825f : index == 4 ? .875f : .80f;
            return new Vector3(x + Mathf.Sin(t * .7f + phase) * .012f,
                y + Mathf.Sin(t * 1.15f + phase) * .014f, -.55f + Mathf.Cos(t * .9f + phase) * .014f);
        }
        public static float ConditionerProgress(double remaining, double duration)
        {
            if (double.IsNaN(remaining) || double.IsInfinity(remaining) || double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0) return 0;
            return Mathf.Clamp01((float)(1 - remaining / duration));
        }
        public static float PaddleAngle(float time, float progress, bool working, bool reduced)
        {
            if (!working || reduced || !FxParticleRequest.Finite(time)) return 45;
            if (!FxParticleRequest.Finite(progress)) progress = 0;
            return time * 42 + Mathf.Sin(time * 1.7f) * 9 + Mathf.Clamp01(progress) * 24;
        }
        public static void SampleSource(CultureKind culture, uint index, bool low, bool reduced,
            out float interval, out Vector3 local, out FxParticleRequest request)
        {
            float a = Noise(index, 1), b = Noise(index, 2), c = Noise(index, 3);
            bool spark = culture == CultureKind.Spark;
            interval = (reduced ? 1.1f : low ? .67f : .37f) + a * .26f;
            request = FxParticleRequest.Create(spark ? (!reduced && !low && index % 4 == 0 ? FxParticleShape.Ring : FxParticleShape.Spark) : FxParticleShape.Puff, CultureColor(culture));
            request.Motion = FxParticleMotion.Curve;
            if (spark) {
                float angle = a * Mathf.PI * 2;
                local = new Vector3(Mathf.Cos(angle) * .105f, 1.27f + b * .36f, Mathf.Sin(angle) * .105f);
                request.End = new Vector3(-local.x * .3f, reduced ? .015f : .08f, -local.z * .3f);
                request.Bend = reduced ? Vector3.zero : new Vector3(-local.z * .3f, .012f, local.x * .3f);
            } else {
                local = new Vector3((a - .5f) * .94f, .84f + b * .035f, -.565f);
                request.End = new Vector3((c - .5f) * .07f, reduced ? .012f : .075f, -.025f);
                request.Bend = reduced ? Vector3.zero : new Vector3((b - .5f) * .045f, .015f, 0);
            }
            request.Lifetime = reduced ? .45f : .63f + c * .22f;
            request.Size = spark ? .045f + c * .025f : .058f + c * .027f;
            request.EndSize = spark && request.Shape == FxParticleShape.Ring ? .10f : .018f;
            request.Spin = reduced ? 0 : (b - .5f) * (spark ? 100 : 24);
        }
        public static void SampleIntake(CultureKind culture, uint index, bool low, bool reduced,
            out float interval, out Vector3 local, out FxParticleRequest request)
        {
            float a = Noise(index, 5), b = Noise(index, 6), c = Noise(index, 7);
            bool spark = culture == CultureKind.Spark;
            interval = (reduced ? .31f : low ? .12f : .065f) + a * .045f;
            local = SourcePoint(culture);
            request = FxParticleRequest.Create(spark ? FxParticleShape.Spark : (index % 3 == 0 ? FxParticleShape.Droplet : FxParticleShape.Puff), CultureColor(culture));
            request.Motion = reduced ? FxParticleMotion.Drift : FxParticleMotion.Spiral;
            request.End = FxCollectionMotion.MouthLocal;
            request.Lifetime = reduced ? .23f : .48f + c * .16f;
            request.Size = (spark ? .055f : .072f) + b * .022f;
            request.EndSize = .023f;
            request.Bend = reduced ? Vector3.zero : new Vector3(-.11f - b * .03f, .13f + a * .03f, -.025f);
            request.Radius = reduced ? 0 : .025f + b * .025f;
            request.Turns = reduced ? 0 : (spark ? 1 : -1) * (.55f + a * .5f);
            request.Spin = reduced ? 0 : (b - .5f) * (spark ? 130 : 25);
        }
        public static void SampleConditioner(int slot, float progress, uint index, bool low, bool reduced,
            out float interval, out Vector3 local, out FxParticleRequest request)
        {
            float a = Noise(index, 9), b = Noise(index, 10), c = Noise(index, 11);
            if (!FxParticleRequest.Finite(progress)) progress = 0;
            float center = slot == 0 ? -.49f : .49f;
            // Keep the live flow in the open upper window above the front supply plaque.
            // Independent seeds vary the birth heights, bends and cadence rather than making bead rails.
            interval = reduced ? 1.15f + a * .19f : low ? .40f + a * .14f : .18f + a * .07f;
            local = new Vector3(center + (a - .5f) * .38f, 1.20f + b * .09f, -.24f + c * .16f);
            request = FxParticleRequest.Create(!reduced && index % 5 == 0 ? FxParticleShape.Spark : FxParticleShape.Bubble,
                Color.Lerp(new Color(.65f, .90f, 1, .84f), new Color(1, .86f, .48f, .92f), Mathf.Clamp01(progress)));
            request.Motion = FxParticleMotion.Curve;
            request.End = new Vector3(center - (local.x - center) * .48f, reduced ? local.y + .025f : 1.34f + b * .06f, -.11f + a * .10f) - local;
            request.Bend = reduced ? Vector3.zero : new Vector3((c - .5f) * .12f, .012f, (a - .5f) * .08f);
            request.Lifetime = reduced ? .46f : 1.03f + c * .18f;
            request.Size = reduced ? .048f + b * .02f : .075f + b * .035f;
            request.EndSize = reduced ? .02f : .038f;
        }
    }
}
