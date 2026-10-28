using UnityEngine;

namespace EvolutionShop
{
    public struct FxHatchPose
    {
        public Vector3 Scale;
        public float Fin, Tail, Crest, Lift, Roll, IdleWeight, FluidScale;
    }
    /// <summary>Post-commit presentation clock shared by wake, release and original audio. No gameplay delay.</summary>
    public static class FxHatchChoreography
    {
        public const float FirstDuration = 1.4f, RepeatDuration = .85f;
        public static float Duration(bool first) => first ? FirstDuration : RepeatDuration;
        public static float Release(bool first) => first ? .12f : .06f;
        public static FxHatchPose Resolve(float age, bool first, bool reduced)
        {
            var pose = new FxHatchPose { Scale = Vector3.one, IdleWeight = 1, FluidScale = 1 };
            if (reduced || !CreatureMotion.Finite(age) || age <= 0 || age >= Duration(first)) return pose;
            float release = Release(first), speed = first ? 1 : .6f, amplitude = first ? 1 : .7f;
            float pop = release + .18f * speed, rebound = release + .43f * speed;
            float beat = age < release ? -Mathf.SmoothStep(0,1,age/release) : age < pop ? Mathf.Lerp(-1,1,Mathf.SmoothStep(0,1,(age-release)/(pop-release))) :
                age < rebound ? Mathf.Lerp(1,-.18f,Mathf.SmoothStep(0,1,(age-pop)/(rebound-pop))) : Mathf.Lerp(-.18f,0,Mathf.SmoothStep(0,1,(age-rebound)/(Duration(first)-rebound)));
            beat *= amplitude;
            pose.Scale = new Vector3(1 - beat*.045f, 1 + beat*.12f, 1 - beat*.04f);
            pose.Fin = beat * 24; pose.Tail = beat * 14; pose.Crest = beat * 7;
            pose.Lift = Mathf.Max(0,beat) * .03f; pose.Roll = beat * 3.5f;
            pose.IdleWeight = 1 - Mathf.Abs(beat) * .8f;
            // Fluid gathers inward; release stays below the accepted shell size.
            pose.FluidScale = 1 - Mathf.Abs(beat) * .025f;
            return pose;
        }
    }
}
