using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class CreatureVisual
    {
        // The long ribbon tail needs incubation-only clearance through growth and hatch; other container scales stay unchanged.
        public static float IncubationScale(string speciesId) => speciesId == "ribbonfin" ? .50f : speciesId == "driftwing" ? .52f : .62f;

        /// <summary>One absolute pose writer for growth and wake; ordinary container animation stays unchanged.</summary>
        public void AnimateIncubation(float time, float effort, bool reduced, float hatchAge = -1, bool firstHatch = false)
        {
            if (!FxParticleRequest.Finite(time)) time = 0;
            effort = FxParticleRequest.Finite(effort) ? Mathf.Clamp01(effort) : 0;
            if (species == "pondlet") { AnimatePondlet(time, effort, reduced, -1, hatchAge, firstHatch); return; }

            // Sample each species' existing stroke, then attenuate it as the young fish uncurls.
            // Re-sampling first prevents accumulated rotations on refresh, reset or a reused resident.
            Animate(time);
            var wake = FxHatchChoreography.Resolve(hatchAge, firstHatch, reduced);
            float motion = reduced ? 0 : Mathf.Clamp01(effort / .7f) * wake.IdleWeight;
            bool wing = species == "driftwing", lantern = species == "lanternfin", ribbon = species == "ribbonfin";
            float yaw = wing || lantern ? 12 : 18;
            float roll = wing ? 18 : lantern ? 16 : 20;
            float finWake = wake.Fin * (ribbon ? .55f : wing ? .8f : .7f);
            float tailWake = wake.Tail * (ribbon ? .75f : .6f);
            if (finLeft) finLeft.localRotation = Quaternion.Slerp(Quaternion.Euler(0, -yaw, -roll), finLeft.localRotation, motion) * Quaternion.Euler(0, 0, -finWake);
            if (finRight) finRight.localRotation = Quaternion.Slerp(Quaternion.Euler(0, yaw, roll), finRight.localRotation, motion) * Quaternion.Euler(0, 0, finWake);
            if (tail) tail.localRotation = Quaternion.Slerp(Quaternion.identity, tail.localRotation, motion) * Quaternion.Euler(0, tailWake, 0);
            if (crest) crest.localRotation = Quaternion.Slerp(Quaternion.identity, crest.localRotation, motion) * Quaternion.Euler(0, 0, wake.Crest * .65f);
            if (lanternBulb) lanternBulb.localScale = Vector3.one * (.20f + (reduced ? 0 : Mathf.Sin(time * 2.2f) * .012f * motion));
        }
    }
}
