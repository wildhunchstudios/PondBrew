using UnityEngine;

namespace EvolutionShop
{
    public enum FxGrowerVisualState { Depleted, Refilling, Full }
    public struct FxGrowerPose
    {
        public FxGrowerVisualState State;
        public float Fill, Refill;
    }
    public struct FxGrowthPose
    {
        public Vector3 Position, Angles, Scale;
        public float Effort;
    }
    /// <summary>Contained presentation sampled from actual production state; no production clock or random state.</summary>
    public static class FxGrowthMotion
    {
        public static float Progress(float remaining, float duration)
        {
            if (!FxParticleRequest.Finite(remaining) || !FxParticleRequest.Finite(duration) || duration <= 0) return 0;
            return Mathf.Clamp01(1 - remaining / duration);
        }
        public static FxGrowthPose Resolve(float progress, float time, float seed, bool reduced)
        {
            float p = FxParticleRequest.Finite(progress) ? Mathf.Clamp01(progress) : 0;
            if (!FxParticleRequest.Finite(time)) time = 0;
            if (!FxParticleRequest.Finite(seed)) seed = 0;
            float maturity = Mathf.SmoothStep(0, 1, p), curled = (1 - maturity) * (1 - maturity);
            float t = time * .78f + seed, life = Mathf.Lerp(.15f, 1, maturity);
            float size = .62f * Mathf.Lerp(.32f, 1, maturity);
            return new FxGrowthPose {
                Position = reduced ? Vector3.zero : new Vector3(Mathf.Sin(t) * .04f * life, Mathf.Sin(t * 1.8f) * .085f * life, 0),
                Angles = reduced ? new Vector3(0, -90, 0) : new Vector3(0, -90 + Mathf.Cos(t) * 20 * life, Mathf.Sin(t * 1.6f) * 5 * life + curled * 16),
                // The tiny rounded embryo lengthens as it uncurls. Adult scale matches the existing hatch exactly.
                Scale = new Vector3(1 - curled * .20f, 1 - curled * .08f, 1) * size,
                Effort = Mathf.Lerp(.04f, .7f, maturity)
            };
        }
        public static FxGrowerPose ReadGrower(int units, int capacity, float secondsRemaining, float refillSeconds)
        {
            float fill = capacity > 0 ? Mathf.Clamp01((float)units / capacity) : 0;
            return new FxGrowerPose {
                State = units <= 0 || capacity <= 0 ? FxGrowerVisualState.Depleted : units >= capacity ? FxGrowerVisualState.Full : FxGrowerVisualState.Refilling,
                Fill = fill, Refill = capacity <= 0 || units >= capacity ? 0 : Progress(secondsRemaining, refillSeconds)
            };
        }
        public static Vector3 Cell(int index, int culture, float time, FxGrowerPose state, bool reduced)
        {
            float phase = index * 2.399f + culture * 1.73f;
            bool working = state.State != FxGrowerVisualState.Full;
            float drift = reduced ? 0 : time * (working ? .34f : .08f);
            float radius = .245f + .025f * Mathf.Sin(phase * 1.7f);
            return new Vector3(Mathf.Cos(phase + drift) * radius,
                1.15f + index % 3 * .22f * Mathf.Lerp(.7f, 1, state.Fill) + (reduced ? 0 : Mathf.Sin(time * (working ? 1.45f : .65f) + phase) * (working ? .045f : .012f)),
                Mathf.Sin(phase + drift) * radius);
        }
        public static void Bubble(uint sequence, float progress, bool grower, int culture, bool reduced, out Vector3 origin, out FxParticleRequest request)
        {
            // Irrational phase offsets avoid synchronized rows without global randomness.
            float a = Mathf.Repeat(sequence * .618034f + culture * .271f, 1);
            float b = Mathf.Repeat(sequence * .381966f + .17f, 1);
            float side = sequence % 2 == 0 ? -1 : 1;
            float p = FxParticleRequest.Finite(progress) ? Mathf.Clamp01(progress) : 0;
            Color tint = grower ? culture == 0 ? new Color(1, .69f, .68f, .78f) : new Color(.4f, .92f, 1, .82f) :
                Color.Lerp(new Color(.36f, .83f, .96f, .78f), new Color(.81f, .94f, .58f, .88f), p);
            request = FxParticleRequest.Create(FxParticleShape.Bubble, tint);
            origin = grower ? new Vector3(side * (.13f + a * .09f), 1.05f + b * .09f, -.19f) :
                new Vector3(side * (.23f + a * .07f), 1.22f + b * .10f, -.24f);
            request.Motion = FxParticleMotion.Curve;
            request.End = new Vector3(-side * (.10f + b * .10f), grower ? .58f : .38f, .04f + a * .08f);
            request.Bend = new Vector3(side * (.055f + a * .035f), .035f, -.035f);
            request.Size = .048f + a * a * .04f;
            request.EndSize = request.Size * 1.15f;
            request.Lifetime = 1.12f + b * .22f;
            if (reduced) {
                origin.y = grower ? 1.44f : 1.55f;
                request.Motion = FxParticleMotion.Drift;
                request.End = request.Bend = Vector3.zero;
                request.Size = request.EndSize = .075f;
                request.Lifetime = .6f;
            }
        }
    }
}
