using System;

namespace EvolutionShop
{
    public enum ComedyKind { Slap = 0, Fart = 1, Complaint = 2, Belch = 3, WorkerCelebration = 4, Mimic = 5 }

    /// <summary>
    /// Transient presentation scheduling only. Runtime supplies active simulation time and chooses
    /// eligible subjects; this class never changes a visitor, transaction, job, or saved preference.
    /// </summary>
    public sealed class ShopComedy
    {
        public const float QuietSeconds = 8f;
        public const float ReducedQuietSeconds = 18f;
        public const float ReactionDelaySeconds = 0.55f;

        // Slap has a separate gameplay cooldown. Its presentation is admitted only by StartSlap.
        private static readonly float[] KindCooldownSeconds = { 0f, 35f, 40f, 25f, 15f, 30f };
        private static readonly string[,] Setups =
        {
            { "PAP!", "BONK!" },
            { "Prrrt...", "Brrrp..." },
            { "This fish is judging me.", "I asked for a quiet fish." },
            { "BUURP!", "Hrrrp!" },
            { "Task complete!", "Nailed it!" },
            { "Fish: Blub-blub?", "Fish: Blip!" }
        };
        private static readonly string[,] Reactions =
        {
            { "HEY! MY DIGNITY!", "My hat felt that." },
            { "Customer: The fish did it.", "A very tiny foghorn." },
            { "It says my tie is too loud.", "This one has opinions." },
            { "Compliments to the bubbles.", "That was my inside voice." },
            { "A tiny dance is in order.", "Professional wiggle time." },
            { "Customer: Blub-blub!", "Customer: Was that my voice?" }
        };

        private readonly float[] kindCooldowns = new float[6];
        private float duration;

        public bool Active { get; private set; }
        public ComedyKind Kind { get; private set; }
        public int Variant { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining { get { return Active ? Math.Max(0f, duration - Elapsed) : 0f; } }
        /// <summary>Shared quiet time; a specific kind may still have a longer private cooldown.</summary>
        public float CooldownRemaining { get; private set; }
        public bool Reduced { get; private set; }
        public string Caption
        {
            get
            {
                if (!Active) return string.Empty;
                return Elapsed < ReactionDelaySeconds
                    ? Setups[(int)Kind, Variant] : Reactions[(int)Kind, Variant];
            }
        }

        public bool TryStart(ComedyKind kind, int variant, bool reduced)
        {
            if (!ValidKind(kind) || kind == ComedyKind.Slap || Active || CooldownRemaining > 0f ||
                kindCooldowns[(int)kind] > 0f) return false;
            Begin(kind, variant, reduced);
            kindCooldowns[(int)kind] = KindCooldownSeconds[(int)kind];
            return true;
        }

        /// <summary>
        /// Priority feedback after an accepted slap. Caller must first enforce SlapCooldown and
        /// commit the existing gameplay action. A slap may replace a cosmetic incident immediately.
        /// </summary>
        public void StartSlap(int variant, bool reduced)
        {
            Begin(ComedyKind.Slap, variant, reduced);
        }

        public void Tick(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
            CooldownRemaining = Math.Max(0f, CooldownRemaining - seconds);
            for (int i = 0; i < kindCooldowns.Length; i++)
                kindCooldowns[i] = Math.Max(0f, kindCooldowns[i] - seconds);
            if (!Active) return;
            // Bound the addition as well as its result, including a finite float.MaxValue delta.
            Elapsed = Math.Min(duration, Elapsed + Math.Min(seconds, duration));
            if (Elapsed >= duration) Active = false;
        }

        /// <summary>Discard unfinished feedback without refunding any shared or per-kind quiet time.</summary>
        public void Cancel()
        {
            Active = false;
            Elapsed = 0f;
            duration = 0f;
        }

        private void Begin(ComedyKind kind, int variant, bool reduced)
        {
            Kind = kind;
            Variant = variant & 1;
            Reduced = reduced;
            Elapsed = 0f;
            duration = reduced ? 1.3f : 2.6f;
            Active = true;
            CooldownRemaining = Math.Max(CooldownRemaining, reduced ? ReducedQuietSeconds : QuietSeconds);
        }

        private static bool ValidKind(ComedyKind kind)
        {
            return kind >= ComedyKind.Slap && kind <= ComedyKind.Mimic;
        }
    }
}
