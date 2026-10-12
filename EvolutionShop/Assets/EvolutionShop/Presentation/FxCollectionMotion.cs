using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Bounded cosmetic intake recipes; never produces or transfers culture.</summary>
    public static class FxCollectionMotion
    {
        public static readonly Vector3 OutletLocal = new Vector3(.36f, .93f, -.745f);
        public static readonly Vector3 MouthLocal = new Vector3(0, .05f, .489f);
        public const float ResponseDuration = .34f;
        // Max bend length .0925 + spiral radius .092 + particle half-size .060 < .25m.
        public const float PathClearance = .25f;
        static float Noise(uint index, uint salt)
        {
            uint n = unchecked(index * 747796405u + salt * 2891336453u + 277803737u);
            n = unchecked((n ^ (n >> 16)) * 2246822519u);
            return (n & 65535u) / 65535f;
        }
        public static void Sample(uint index, CultureKind culture, bool low, bool reduced,
            out float interval, out Vector3 local, out FxParticleRequest request)
        {
            bool flow = culture == CultureKind.Flow;
            float a = Noise(index, 1), b = Noise(index, 2), c = Noise(index, 3);
            interval = reduced ? .28f : (low ? .085f : .041f) + a * (low ? .05f : .027f);
            local = OutletLocal; // Every bead originates inside the actual outlet, not a displaced grid.
            request = FxParticleRequest.Create(index % 5 == 2 ? FxParticleShape.Droplet : FxParticleShape.Bubble,
                flow ? new Color(.36f + .15f*b, .85f, 1, .9f) : new Color(1, .52f + .19f*b, .65f + .12f*c, .9f));
            request.Motion = reduced ? FxParticleMotion.Drift : FxParticleMotion.Spiral;
            request.End = MouthLocal;
            request.Lifetime = reduced ? .22f : (flow ? .48f : .56f) + c * .18f;
            request.Size = reduced ? .052f : .063f + b * .057f;
            request.EndSize = reduced ? request.Size : .026f + c * .013f;
            request.Bend = reduced ? Vector3.zero : new Vector3((b-.5f)*.05f, -.025f-a*.03f, -.035f-c*.035f);
            request.Radius = reduced ? 0 : (flow ? .044f : .064f) + b * .028f;
            request.Turns = reduced ? 0 : (flow ? -1 : 1) * (.65f + a * .8f);
            request.Spin = reduced ? 0 : (b-.5f)*140;
        }
        public static float Response(float remaining, bool reduced)
        {
            if (reduced || !FxParticleRequest.Finite(remaining) || remaining <= 0) return 0;
            float age = ResponseDuration - Mathf.Clamp(remaining, 0, ResponseDuration);
            return Mathf.Sin(Mathf.Clamp01(age / ResponseDuration) * Mathf.PI);
        }
        public static void ReservoirPose(int index, float time, float response, bool reduced,
            out Vector3 position, out Quaternion rotation)
        {
            float kick = reduced ? 0 : Mathf.Clamp01(response);
            float angle = index * 2.399f + time * .6f + kick * .72f;
            float radius = .052f - kick * .007f;
            position = new Vector3(Mathf.Cos(angle) * radius,
                -.062f + (index % 4) * .037f + Mathf.Sin(time * 2 + index) * .008f + kick * .009f,
                Mathf.Sin(angle) * radius);
            rotation = Quaternion.Euler(Mathf.Sin(time + index) * 15 + kick * 9,
                time * 31 + index * 52 + kick * 24, Mathf.Cos(time * 1.3f + index) * 17);
        }
    }
}
