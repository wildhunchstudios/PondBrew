using UnityEngine;

namespace EvolutionShop
{
    public readonly struct FxRoomLightPose
    {
        public readonly Color KeyColor, ShopColor, LabColor;
        public readonly float KeyIntensity, ShopIntensity, LabIntensity;
        public FxRoomLightPose(Color key, Color shop, Color lab, float keyIntensity, float shopIntensity, float labIntensity)
        { KeyColor = key; ShopColor = shop; LabColor = lab; KeyIntensity = keyIntensity; ShopIntensity = shopIntensity; LabIntensity = labIntensity; }
    }

    public readonly struct FxRoomAtmosphereSnapshot
    {
        public readonly ShopDayPhase CurrentPhase, TargetPhase;
        public readonly float Blend, Clock, Realtime;
        public readonly bool Working;
        public readonly int EmissionCount, FrameCount;
        public readonly FxViewPolicy Policy;
        public FxRoomAtmosphereSnapshot(ShopDayPhase current, ShopDayPhase target, float blend, float clock,
            bool working, int emissions, FxViewPolicy policy, int frame, float realtime)
        { CurrentPhase = current; TargetPhase = target; Blend = blend; Clock = clock; Working = working;
            EmissionCount = emissions; Policy = policy; FrameCount = frame; Realtime = realtime; }
    }

    public static class FxRoomAtmosphereMotion
    {
        public const float LightTransitionSeconds = .9f;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static void StepDoor(float fraction, float velocity, bool opening, float dt, out float nextFraction, out float nextVelocity)
        {
            nextFraction = Finite(fraction) ? Mathf.Clamp01(fraction) : 0;
            nextVelocity = Finite(velocity) ? Mathf.Clamp(velocity, -2.8f, 4f) : 0;
            if (!Finite(dt) || dt <= 0) return;
            // Occupation never carries closing inertia into the opening response.
            if (opening && nextVelocity < 0) nextVelocity = 0;
            float remaining = Mathf.Min(dt, .5f);
            for (int i = 0; i < 31 && remaining > .000001f; i++)
            {
                float step = Mathf.Min(remaining, 1f / 60f);
                float before = nextVelocity;
                nextVelocity = Mathf.MoveTowards(before, opening ? 4f : -2.8f, step * (opening ? 40f : 16f));
                nextFraction = Mathf.Clamp01(nextFraction + (before + nextVelocity) * .5f * step);
                if (nextFraction <= 0 && nextVelocity < 0 || nextFraction >= 1 && nextVelocity > 0)
                    nextVelocity = 0;
                remaining -= step;
            }
        }

        public static float LightBlend(float age) => !Finite(age) ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / LightTransitionSeconds));

        public static FxRoomLightPose Lights(ShopDayPhase phase)
        {
            bool night = phase == ShopDayPhase.Night, closing = phase == ShopDayPhase.Closing;
            return new FxRoomLightPose(
                night ? new Color(.70f, .82f, 1f) : closing ? new Color(1f, .89f, .76f) : new Color(1f, .95f, .84f),
                night ? new Color(1f, .83f, .63f) : closing ? new Color(1f, .87f, .69f) : new Color(1f, .95f, .83f),
                night ? new Color(.74f, .94f, 1f) : closing ? new Color(.74f, .98f, .97f) : new Color(.75f, 1f, .94f),
                night ? 1.12f : closing ? 1.45f : 1.65f, night ? 2.65f : closing ? 3f : 3.4f, 3.2f);
        }

        public static FxRoomLightPose BlendLights(FxRoomLightPose from, FxRoomLightPose to, float blend)
        {
            blend = Finite(blend) ? Mathf.Clamp01(blend) : 0;
            return new FxRoomLightPose(Color.Lerp(from.KeyColor, to.KeyColor, blend), Color.Lerp(from.ShopColor, to.ShopColor, blend),
                Color.Lerp(from.LabColor, to.LabColor, blend), Mathf.Lerp(from.KeyIntensity, to.KeyIntensity, blend),
                Mathf.Lerp(from.ShopIntensity, to.ShopIntensity, blend), Mathf.Lerp(from.LabIntensity, to.LabIntensity, blend));
        }
    }
}
