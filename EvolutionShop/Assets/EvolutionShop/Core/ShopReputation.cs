using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    [Serializable]
    public sealed class ReputationSave
    {
        // Zero is deliberately invalid, including a missing object manufactured by JsonUtility.
        public int version;
        public int points;
    }

    /// <summary>Business reputation observes accepted terminal outcomes; the ledger owns visit identity.</summary>
    public sealed class ShopReputation
    {
        public const int SaveVersion = 1;
        public const int MaxPoints = 100;
        private int points;

        public int Points => points;
        public int Tier => points >= 30 ? 2 : points >= 10 ? 1 : 0;
        public string TierName => Tier == 2 ? "Local favorite" : Tier == 1 ? "Getting known" : "New shop";
        public int NextThreshold => Tier == 2 ? MaxPoints : Tier == 1 ? 30 : 10;
        public float ArrivalSeconds => Tier == 2 ? 4f : Tier == 1 ? 5f : 6f;
        public int PremiumPercent => Tier * 10;
        public int BudgetBonus => Tier * 2;

        /// <summary>Only a successfully resolved pending visit may change reputation, once.</summary>
        public bool ResolveVisit(ShopTradeLedger ledger, string id, CustomerOutcome outcome, int price = 0)
        {
            if (ledger == null || !ledger.ResolveVisit(id, outcome, price)) return false;
            int change = outcome == CustomerOutcome.Sold ? 2 :
                outcome == CustomerOutcome.TooExpensive || outcome == CustomerOutcome.Impatient || outcome == CustomerOutcome.Slapped ? -1 : 0;
            points = Math.Max(0, Math.Min(MaxPoints, points + change));
            return true;
        }

        /// <summary>
        /// Adjust fresh copies of a validated base catalog. Always pass the base definitions, not
        /// a previously adjusted result, so refreshing previews never compounds the premium.
        /// Existing visits already hold their own copies and agreed-price reservations.
        /// </summary>
        public CustomerArchetype[] ApplyTo(IReadOnlyList<CustomerArchetype> baseCatalog)
        {
            var adjusted = CustomerArchetypes.Validate(baseCatalog);
            int tier = Tier;
            foreach (var type in adjusted)
            {
                if (type.Weight > 0)
                {
                    switch (type.Id)
                    {
                        case CustomerArchetypes.Casual: type.Weight = tier == 2 ? 30 : tier == 1 ? 35 : 40; break;
                        case CustomerArchetypes.Bargain: type.Weight = tier == 2 ? 20 : tier == 1 ? 25 : 30; break;
                        case CustomerArchetypes.Collector: type.Weight = tier == 0 ? 20 : 25; break;
                        case CustomerArchetypes.Premium: type.Weight = tier == 2 ? 25 : tier == 1 ? 15 : 10; break;
                    }
                }
                type.TolerancePercent = Math.Min(1000, type.TolerancePercent + PremiumPercent);
                type.FavoriteTolerancePercent = Math.Min(1000, type.FavoriteTolerancePercent + PremiumPercent);
                type.Budget = (int)Math.Min(int.MaxValue, (long)type.Budget + BudgetBonus);
            }
            return adjusted;
        }

        public ReputationSave CaptureSave() => new ReputationSave { version = SaveVersion, points = points };

        public static bool TryRestore(ReputationSave save, out ShopReputation reputation)
        {
            reputation = null;
            if (save == null || save.version != SaveVersion || save.points < 0 || save.points > MaxPoints)
                return false;
            reputation = new ShopReputation { points = save.points };
            return true;
        }
    }
}
