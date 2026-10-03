using System;

namespace EvolutionShop
{
    public static class SlapRules
    {
        public const float CooldownSeconds = 3f;
        public const float Range = 2.2f;

        /// <summary>One uppercase letter that cannot collide with movement or normal interaction.</summary>
        public static bool IsAllowedKey(string key)
        {
            return key != null && key.Length == 1 && key[0] >= 'A' && key[0] <= 'Z' &&
                key != "A" && key != "D" && key != "E" && key != "S" && key != "W";
        }
    }

    /// <summary>Shared transient cooldown. Runtime supplies active simulation time only.</summary>
    public sealed class SlapCooldown
    {
        public float Remaining { get; private set; }

        public bool TryStart()
        {
            if (Remaining > 0f) return false;
            Remaining = SlapRules.CooldownSeconds;
            return true;
        }

        public void Tick(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
            Remaining = Math.Max(0f, Remaining - seconds);
        }
    }
}
