using UnityEngine;

namespace EvolutionShop
{
    public struct PondletPose
    {
        public Vector3 Position, Angles;
        public float Effort;
    }
    /// <summary>Bounded cosmetic curves. No random/game state, objects or callbacks.</summary>
    public static class PondletMotion
    {
        public const float Period = 7.6f;
        public static float IdentityPhase(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            uint hash = 2166136261;
            for (int i = 0; i < id.Length; i++) hash = unchecked((hash ^ id[i]) * 16777619);
            return (hash % 10007) / 10007f * Period;
        }
        // Integral of a cosine velocity ramp:400ms accelerate/stop, constant middle cruise.
        static float Distance(float age)
        {
            const float duration = 3, ramp = .4f;
            age = Mathf.Clamp(age, 0, duration);
            if (age < ramp) return .5f * (age - ramp / Mathf.PI * Mathf.Sin(Mathf.PI * age / ramp)) / (duration - ramp);
            if (age > duration - ramp) return 1 - Distance(duration - age);
            return (age - ramp * .5f) / (duration - ramp);
        }
        public static PondletPose Resolve(float time, float phase, bool reduced, float placedAge = -1, float glanceAge = -1)
        {
            if (reduced) return new PondletPose { Angles = new Vector3(0, -90, 0), Effort = .12f };
            float cycle = Mathf.Repeat(time + phase, Period), segment = cycle < 3.8f ? cycle : cycle - 3.8f;
            float direction = cycle < 3.8f ? 1 : -1;
            float distance = Distance(segment);
            float effort = segment >= 3 ? 0 : segment < .4f ? .5f - .5f * Mathf.Cos(Mathf.PI * segment / .4f) :
                segment > 2.6f ? .5f - .5f * Mathf.Cos(Mathf.PI * (3 - segment) / .4f) : 1;
            float turn = Mathf.Sin((time + phase) * Mathf.PI * 2 / Period);
            float glance = glanceAge >= 0 && glanceAge < .6f ? Mathf.Sin(glanceAge / .6f * Mathf.PI) : 0;
            float place = placedAge >= 0 && placedAge < .52f ? Mathf.Sin(placedAge / .52f * Mathf.PI) : 0;
            return new PondletPose {
                Position = new Vector3(direction * Mathf.Lerp(-.22f, .22f, distance), Mathf.Sin((time + phase) * 1.3f) * .035f - place * .01f, 0),
                Angles = new Vector3(turn * 1.5f, -90 + Mathf.Clamp(turn * 7 + glance * 3, -10, 10), turn * 1.5f), Effort = effort
            };
        }
    }
    public sealed partial class CreatureVisual
    {
        Quaternion pondletTailRest, pondletLeftRest, pondletRightRest, pondletCrestRest;
        float pondletIdentityPhase;
        float mimicTime;
        int mimicFrame = -10;
        bool mimicReduced;
        void CapturePondletRest()
        {
            pondletTailRest = tail.localRotation; pondletLeftRest = finLeft.localRotation;
            pondletRightRest = finRight.localRotation; pondletCrestRest = crest.localRotation;
        }
        public void SetPondletIdentity(string id) { pondletIdentityPhase = PondletMotion.IdentityPhase(id); ClearMimicPose(); }
        public void RequestMimicPose(float elapsed, bool reduced) { mimicTime = elapsed; mimicFrame = Time.frameCount; mimicReduced = reduced; }
        public void ClearMimicPose() { mimicFrame = -10; }
        public bool MimicPoseActive => Time.frameCount - mimicFrame <= 1 && !mimicReduced;
        public void AnimatePondlet(float time, float effort, bool reduced, float glanceAge = -1, float hatchAge = -1, bool firstHatch = false)
        {
            if (!FxParticleRequest.Finite(time)) time = 0;
            effort = FxParticleRequest.Finite(effort) ? Mathf.Clamp01(effort) : 0;
            AnimateExpression(time, reduced);
            float stroke = (time + pondletIdentityPhase) * 3.8f;
            float wave = Mathf.Sin(stroke), petal = Mathf.Sin(stroke - .55f);
            if (MimicPoseActive && !reduced) wave = petal = Mathf.Sin(Mathf.Floor(mimicTime * 2) * 1.7f);
            float flick = glanceAge >= 0 && glanceAge < .6f && !reduced ? Mathf.Sin(glanceAge / .6f * Mathf.PI) * 3 : 0;
            var wake = FxHatchChoreography.Resolve(hatchAge, firstHatch, reduced);
            float tailAngle = reduced ? 0 : Mathf.Clamp(wave * Mathf.Lerp(5,14,effort)*wake.IdleWeight + wake.Tail,-24,24);
            float finAngle = reduced ? 0 : Mathf.Clamp(petal * Mathf.Lerp(4,9,effort)*wake.IdleWeight + flick + wake.Fin,-28,28);
            if (hatchAge < 0) finAngle = Mathf.Clamp(finAngle,-12,12);
            if (tail) tail.localRotation = pondletTailRest * Quaternion.Euler(0, tailAngle, 0);
            if (finLeft) finLeft.localRotation = pondletLeftRest * Quaternion.Euler(0, 0, -finAngle);
            if (finRight) finRight.localRotation = pondletRightRest * Quaternion.Euler(0, 0, finAngle);
            if (crest) crest.localRotation = pondletCrestRest * Quaternion.Euler(0, 0, reduced ? 0 : wave * 4 * wake.IdleWeight + wake.Crest);
        }
    }
}
