using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>
    /// Derived presentation value, never a second saved authority. Each distinct decoration wholly
    /// on the store floor counts once; lab pieces and duplicate copies have no economic effect.
    /// Tuning is provisional. Only future customer copies receive the capped additive bonuses.
    /// </summary>
    public sealed class ShopAppeal
    {
        public const int MaxPoints = 30, PointsPerDefinition = 10;
        public int Points { get; }
        public int Tier => Points / PointsPerDefinition;
        public float ArrivalReductionSeconds => .25f * Tier;
        public int ToleranceBonusPercent => 5 * Tier;
        public int BudgetBonus => Tier;
        public int PremiumWeightBonus => 5 * Tier;

        private ShopAppeal(int points) { Points = points; }

        public static ShopAppeal FromLayout(ShopLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            return new ShopAppeal(layout.AppealPoints);
        }

        /// <summary>Always supply the base catalog. Reputation and appeal each apply once to independent copies.</summary>
        public CustomerArchetype[] ApplyTo(IReadOnlyList<CustomerArchetype> baseCatalog, ShopReputation reputation)
        {
            if (reputation == null) throw new ArgumentNullException(nameof(reputation));
            var adjusted = reputation.ApplyTo(baseCatalog);
            foreach (var type in adjusted)
            {
                type.TolerancePercent = Math.Min(1000, type.TolerancePercent + ToleranceBonusPercent);
                type.FavoriteTolerancePercent = Math.Min(1000, type.FavoriteTolerancePercent + ToleranceBonusPercent);
                type.Budget = (int)Math.Min(int.MaxValue, (long)type.Budget + BudgetBonus);
                if (type.Id == CustomerArchetypes.Premium && type.Weight > 0)
                    type.Weight = Math.Min(1000, type.Weight + PremiumWeightBonus);
            }
            return adjusted;
        }

        public float ArrivalSeconds(ShopReputation reputation)
        {
            if (reputation == null) throw new ArgumentNullException(nameof(reputation));
            return Math.Max(3.25f, reputation.ArrivalSeconds - ArrivalReductionSeconds);
        }
    }
}
