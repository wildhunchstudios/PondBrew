using UnityEngine;

namespace EvolutionShop
{
    /// <summary>One original hatch composition. Pure local samples; never touches gameplay or Unity's random state.</summary>
    public static class FxHatchRelease
    {
        public static int Count(bool first, bool low, bool reduced) => reduced ? 1 : first ? (low ? 18 : 36) : (low ? 10 : 18);
        static float Noise(int index, uint seed, uint salt)
        {
            uint x = unchecked((uint)(index + 1) * 747796405u + seed * 2891336453u + salt);
            x = unchecked(((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u);
            return ((x >> 22) ^ x) / (float)uint.MaxValue;
        }
        public static void Sample(int index, uint seed, bool first, bool reduced, out float delay, out Vector3 origin, out FxParticleRequest p)
        {
            float a = Noise(index, seed, 17), b = Noise(index, seed, 109), c = Noise(index, seed, 919), d = Noise(index, seed, 3119);
            // A dominant watery family, soft supporting vapor, and only two small warm glints.
            bool vapor = !reduced && index < 4, glint = !reduced && (index == 10 || (first && index == 24));
            Color water = b < .2f ? new Color(1, .62f, .57f, .9f) : b > .87f ? new Color(.73f, .96f, .64f, .9f) : new Color(.3f, .89f, .91f, .95f);
            p = FxParticleRequest.Create(vapor ? FxParticleShape.Puff : glint ? FxParticleShape.Spark : FxParticleShape.Bubble,
                vapor ? new Color(.78f, .98f, .95f, .48f) : glint ? new Color(1, .93f, .66f, .9f) : water);
            float duration = FxHatchChoreography.Duration(first) - FxHatchChoreography.Release(first);
            delay = index < 4 || reduced ? 0 : (first ? .28f : .10f) * a;
            origin = new Vector3((c - .5f) * .18f, 1.43f + (d - .5f) * .12f, -.56f - b * .07f);
            float side = Mathf.Cos(a * Mathf.PI * 2), spread = .28f + c * .34f;
            p.Motion = FxParticleMotion.Curve;
            p.End = new Vector3(side * spread, .18f + b * (first ? .63f : .4f), -.035f - d * .24f);
            p.Bend = new Vector3(side * .13f, .045f + c * .045f, (a - .5f) * .08f);
            p.Lifetime = Mathf.Min(duration - delay, (first ? .7f : .48f) + d * (first ? .42f : .18f));
            p.Size = glint ? .055f + c * .025f : .065f + c * c * .15f;
            p.EndSize = p.Size * (.8f + b * .24f);
            p.Spin = glint ? (a - .5f) * 120 : (a - .5f) * 24;
            if (vapor)
            {
                float sign = index % 2 == 0 ? -1 : 1;
                origin.x = sign * (.13f + c * .1f); origin.y -= .06f;
                p.End = new Vector3(sign * (.24f + b * .15f), .13f + d * .19f, -.05f);
                p.Bend = new Vector3(sign * .1f, .06f, 0);
                p.Size = .16f + c * .05f; p.EndSize = .29f;
                p.Lifetime = first ? .7f + a * .15f : .6f;
            }
            if (reduced)
            {
                delay = 0; origin = new Vector3(-.23f, 1.65f, -.58f); p.Size = p.EndSize = .18f;
                p.Motion = FxParticleMotion.Drift; p.End = p.Bend = Vector3.zero; p.Spin = 0;
            }
        }
    }
}
