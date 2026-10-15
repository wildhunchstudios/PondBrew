using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Species-specific cosmetic travel. Containers retain scale, placement and ownership.</summary>
    public static class CreatureMotion
    {
        public static float Period(string species) => species == "bubblebelly" ? 10f : species == "ribbonfin" ? 11.2f :
            species == "lanternfin" ? 8.8f : species == "driftwing" ? 12.4f : PondletMotion.Period;

        // Seconds, deliberately shared with the established Pondlet identity contract.
        public static float IdentityPhase(string id) => PondletMotion.IdentityPhase(id);

        public static PondletPose Resolve(string species, float time, float phase, bool reduced)
        {
            if (!FxParticleRequest.Finite(time)) time = 0;
            if (!FxParticleRequest.Finite(phase)) phase = 0;
            if (species == "pondlet") return PondletMotion.Resolve(time, phase, reduced);
            if (reduced) return new PondletPose { Angles = new Vector3(0, -90, 0), Effort = .12f };
            bool round = species == "bubblebelly", ribbon = species == "ribbonfin", wing = species == "driftwing";
            float period = Period(species), cycle = Mathf.Repeat(time + phase, period) / period;
            float half = Mathf.Repeat(cycle * 2, 1), direction = cycle < .5f ? 1 : -1;
            // Smooth cruise followed by a real hover; both velocity and acceleration settle at the join.
            float travel = wing ? .72f : round ? .80f : ribbon ? .90f : .78f;
            float u = Mathf.Clamp01(half / travel);
            float distance = u * u * u * (u * (u * 6 - 15) + 10);
            float effort = half < travel ? Mathf.Sin(u * Mathf.PI) : 0;
            effort *= effort;
            float angle = cycle * Mathf.PI * 2;
            float bank = Mathf.Sin(angle), heave = Mathf.Sin(angle * 2 - .4f);
            float range = ribbon ? .08f : round ? .30f : wing ? .25f : .28f;
            return new PondletPose {
                Position = new Vector3(direction * Mathf.Lerp(-range, range, distance),
                    heave * (round ? .047f : wing ? .035f : .028f), 0),
                Angles = new Vector3(bank * (round ? 2 : 1.5f), -90 + bank * (ribbon ? 9 : 12),
                    heave * (round ? 3.8f : wing ? 3 : 1.8f)), Effort = effort
            };
        }

        public static float Blink(float time, float phase, bool reduced)
        {
            if (reduced) return 1;
            float age = Mathf.Repeat(time + phase, 6.4f);
            if (age >= .18f) return 1;
            float close = Mathf.Sin(age / .18f * Mathf.PI);
            return 1 - .90f * close * close;
        }

        public static float WingStroke(float time)
        {
            float cycle = Mathf.Repeat(time, 2.8f) / 2.8f;
            if (cycle >= .42f) return 0;
            float stroke = Mathf.Sin(cycle / .42f * Mathf.PI);
            return stroke * stroke;
        }
    }
}
