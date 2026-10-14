using UnityEngine;

namespace EvolutionShop
{
    // Collection can be refused while the real grower continues refilling.
    public enum FxGrowerInteraction { Idle, Collecting, ReservoirFull, SaveBlocked, PathObstructed }

    public static class FxGrowerMotion
    {
        public const float ResponseDuration = .36f;

        public static FxGrowerInteraction ReadInteraction(FxGrowerPose stock, bool attempted, bool collecting,
            bool reservoirFull, bool saveBlocked, bool pathClear)
        {
            if (!attempted) return FxGrowerInteraction.Idle;
            if (saveBlocked) return FxGrowerInteraction.SaveBlocked;
            if (reservoirFull) return FxGrowerInteraction.ReservoirFull;
            if (stock.State == FxGrowerVisualState.Depleted) return FxGrowerInteraction.Idle;
            if (!pathClear) return FxGrowerInteraction.PathObstructed;
            return collecting ? FxGrowerInteraction.Collecting : FxGrowerInteraction.Idle;
        }

        public static float Needle(FxGrowerPose stock, FxGrowerInteraction interaction, float responseRemaining,
            float time, bool reduced)
        {
            if (!FxParticleRequest.Finite(time)) time = 0;
            float p = FxParticleRequest.Finite(stock.Refill) ? Mathf.Clamp01(stock.Refill) : 0;
            bool working = stock.State != FxGrowerVisualState.Full;
            if ((int)interaction >= (int)FxGrowerInteraction.ReservoirFull) return 32;
            float angle = working ? Mathf.Lerp(10, -10, p) : -10;
            if (reduced) return angle;
            float response = FxParticleRequest.Finite(responseRemaining) ? Mathf.Clamp01(responseRemaining / ResponseDuration) : 0;
            float stroke = Mathf.Sin(response * Mathf.PI) * 13;
            return angle + (working ? Mathf.Sin(time * 2.4f) * 3 : 0) + stroke;
        }

        public static bool SampleCirculation(CultureKind culture, uint sequence, FxGrowerPose stock, bool low,
            bool reduced, out float interval, out Vector3 origin, out FxParticleRequest request)
        {
            origin = Vector3.zero; request = default; interval = 1;
            if ((culture != CultureKind.Bloom && culture != CultureKind.Flow) || stock.State == FxGrowerVisualState.Full) return false;
            float fill = FxParticleRequest.Finite(stock.Fill) ? Mathf.Clamp01(stock.Fill) : 0;
            float progress = FxParticleRequest.Finite(stock.Refill) ? Mathf.Clamp01(stock.Refill) : 0;
            bool empty = stock.State == FxGrowerVisualState.Depleted, bloom = culture == CultureKind.Bloom;
            float a = Mathf.Repeat(sequence * .618034f + (bloom ? .13f : .47f), 1);
            float b = Mathf.Repeat(sequence * .381966f + .29f, 1);
            float side = sequence % 2 == 0 ? -1 : 1;
            // The existing medium starts at .93 and has a stock-dependent top. Paths remain inside it.
            float surface = .93f + .88f * Mathf.Lerp(.55f, 1, fill);
            float bottom = 1.055f, top = surface - .12f;
            Color tint = empty ? new Color(.63f, .83f, .87f, .42f) : bloom ?
                new Color(1, .67f, .71f, .87f) : new Color(.35f, .89f, 1, .89f);
            request = FxParticleRequest.Create(!bloom && !empty && sequence % 3 == 0 ? FxParticleShape.Droplet : FxParticleShape.Bubble, tint);
            request.Motion = FxParticleMotion.Curve;
            // Bloom rolls broadly across the vessel; Flow follows a narrow rising/return current.
            bool returning = !bloom && sequence % 3 == 0;
            origin = new Vector3(side * (bloom ? .22f : .14f), returning ? top : bottom, -.20f - b * .035f);
            request.End = new Vector3(-side * (bloom ? .34f : .12f), (returning ? bottom : top) - origin.y, .09f + a * .035f);
            request.Bend = new Vector3(side * (bloom ? .11f : .075f), .025f, -.025f);
            request.Size = (empty ? .034f : bloom ? .068f : .055f) + a * (empty ? .012f : .022f);
            request.EndSize = request.Size * (bloom ? 1.13f : .78f);
            request.Lifetime = empty ? 1.0f : bloom ? 1.25f : .95f;
            interval = empty ? 1.22f : Mathf.Lerp(bloom ? .43f : .37f, bloom ? .31f : .26f, progress);
            if (empty) request.End *= .38f;
            if (low) interval *= 1.65f;
            interval += (sequence % 3) * .037f;
            if (reduced) {
                origin.y = Mathf.Lerp(bottom, top, .5f); request.Motion = FxParticleMotion.Drift;
                request.End = request.Bend = Vector3.zero;
                request.Size = request.EndSize = empty ? .045f : .065f;
                request.Lifetime = .5f; interval = empty ? 1.65f : 1.35f;
            }
            return true;
        }

        public static void SampleAccent(CultureKind culture, uint sequence, FxGrowerPose stock, bool accepted,
            bool reduced, out Vector3 origin, out FxParticleRequest request)
        {
            float fill = FxParticleRequest.Finite(stock.Fill) ? Mathf.Clamp01(stock.Fill) : 0;
            float side = sequence % 2 == 0 ? -1 : 1;
            bool empty = stock.State == FxGrowerVisualState.Depleted;
            Color tint = empty ? new Color(.63f, .83f, .87f, .55f) : culture == CultureKind.Flow ?
                new Color(.40f, .91f, 1, .88f) : new Color(1, .68f, .73f, .88f);
            request = FxParticleRequest.Create(accepted ? FxParticleShape.Ring : FxParticleShape.Bubble, tint);
            origin = new Vector3(side * .19f, 1.09f + fill * .23f, -.22f);
            request.Motion = FxParticleMotion.Curve; request.Lifetime = reduced ? .22f : .32f;
            request.Size = accepted ? .055f : .07f; request.EndSize = reduced ? request.Size : accepted ? .12f : .035f;
            request.End = reduced ? Vector3.zero : new Vector3(-side * .04f, .065f, .01f);
            request.Bend = reduced ? Vector3.zero : new Vector3(side * .025f, .025f, 0);
        }
    }
}
