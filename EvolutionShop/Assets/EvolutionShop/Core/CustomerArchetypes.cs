using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>One customer type. Pure tuning: a visit copies it and never changes it.</summary>
    [Serializable]
    public sealed class CustomerArchetype
    {
        public string Id;
        public string DisplayName;
        /// <summary>Relative arrival weight. Zero keeps the type defined but absent.</summary>
        public int Weight = 1;
        /// <summary>The most this type spends on one creature, whatever its standard price.</summary>
        public int Budget = int.MaxValue;
        public int BargainPercent = 75;
        public int TolerancePercent = 150;
        /// <summary>Species this type seeks out first and pays FavoriteTolerancePercent for.</summary>
        public string[] FavoriteSpecies = new string[0];
        public int FavoriteTolerancePercent = 150;
        public float PatienceSeconds = 45f;
        public float BrowseSeconds = 2f;
        public string PresentationId = "casual";

        public bool Likes(string speciesId) => speciesId != null && FavoriteSpecies != null && Array.IndexOf(FavoriteSpecies, speciesId) >= 0;
        public int ToleranceFor(string speciesId) => Likes(speciesId) ? FavoriteTolerancePercent : TolerancePercent;

        /// <summary>Highest price this type pays for the species: its tolerance limit, capped by its budget.</summary>
        public int AcceptableLimit(string speciesId, int standard)
        {
            return Math.Min(Budget, ShopPricing.AcceptableLimit(standard, ToleranceFor(speciesId)));
        }

        public PriceReaction Judge(string speciesId, int price, int standard)
        {
            if (price > Budget) return PriceReaction.TooExpensive;
            return ShopPricing.Judge(price, standard, BargainPercent, ToleranceFor(speciesId));
        }

        public bool OverBudget(int price) => price > Budget;

        public CustomerArchetype Copy()
        {
            return new CustomerArchetype
            {
                Id = Id, DisplayName = DisplayName, Weight = Weight, Budget = Budget,
                BargainPercent = BargainPercent, TolerancePercent = TolerancePercent,
                FavoriteSpecies = FavoriteSpecies == null ? null : (string[])FavoriteSpecies.Clone(),
                FavoriteTolerancePercent = FavoriteTolerancePercent, PatienceSeconds = PatienceSeconds,
                BrowseSeconds = BrowseSeconds, PresentationId = PresentationId
            };
        }

        /// <summary>The single R02/R03.1 customer, built from shared rules, for callers without a type.</summary>
        public static CustomerArchetype FromRules(CustomerRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return new CustomerArchetype
            {
                Id = CustomerArchetypes.Casual, DisplayName = "Casual visitor", Weight = 1,
                BargainPercent = rules.BargainPercent, TolerancePercent = rules.PriceTolerancePercent,
                FavoriteTolerancePercent = rules.PriceTolerancePercent,
                PatienceSeconds = rules.PatienceSeconds, BrowseSeconds = rules.BrowseSeconds, PresentationId = "casual"
            };
        }

        /// <summary>Null when valid; otherwise why this definition cannot be used.</summary>
        public string Problem(Func<string, bool> knownSpecies = null)
        {
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 32) return "Customer type IDs must be 1-32 characters.";
            if (string.IsNullOrWhiteSpace(DisplayName)) return "Customer type " + Id + " needs a name.";
            if (string.IsNullOrWhiteSpace(PresentationId)) return "Customer type " + Id + " needs a presentation.";
            if (Weight < 0 || Weight > 1000) return "Customer type " + Id + " weight must be 0-1000.";
            if (Budget < 1) return "Customer type " + Id + " budget must be at least 1.";
            if (BargainPercent < 0 || BargainPercent > 100) return "Customer type " + Id + " bargain must be 0-100%.";
            if (TolerancePercent < 100 || TolerancePercent > 1000 || FavoriteTolerancePercent < 100 || FavoriteTolerancePercent > 1000)
                return "Customer type " + Id + " tolerances must be 100-1000%.";
            if (!PositiveFinite(PatienceSeconds) || !PositiveFinite(BrowseSeconds)) return "Customer type " + Id + " timings must be finite and positive.";
            if (FavoriteSpecies == null) return "Customer type " + Id + " favourites cannot be null.";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string species in FavoriteSpecies)
                if (string.IsNullOrWhiteSpace(species) || !seen.Add(species) || (knownSpecies != null && !knownSpecies(species)))
                    return "Customer type " + Id + " has an unknown or repeated favourite.";
            return null;
        }

        private static bool PositiveFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    /// <summary>The four launch customer types and their deterministic arrival rotation.</summary>
    public static class CustomerArchetypes
    {
        public const string Casual = "casual";
        public const string Bargain = "bargain";
        public const string Collector = "collector";
        public const string Premium = "premium";
        public const int MaxTypes = 16;

        /// <summary>Tuning hypotheses, not measured balance. Change them here with evidence.</summary>
        public static CustomerArchetype[] CreateDefault()
        {
            return new[]
            {
                new CustomerArchetype
                {
                    Id = Casual, DisplayName = "Casual visitor", Weight = 40, Budget = 30,
                    BargainPercent = 75, TolerancePercent = 150, FavoriteTolerancePercent = 150,
                    PatienceSeconds = 45f, BrowseSeconds = 2f, PresentationId = "casual"
                },
                new CustomerArchetype
                {
                    Id = Bargain, DisplayName = "Bargain hunter", Weight = 30, Budget = 8,
                    BargainPercent = 85, TolerancePercent = 110, FavoriteTolerancePercent = 110,
                    PatienceSeconds = 60f, BrowseSeconds = 3f, PresentationId = "bargain"
                },
                new CustomerArchetype
                {
                    Id = Collector, DisplayName = "Collector", Weight = 20, Budget = 40,
                    BargainPercent = 75, TolerancePercent = 100,
                    FavoriteSpecies = new[] { "bubblebelly", "ribbonfin" }, FavoriteTolerancePercent = 250,
                    PatienceSeconds = 50f, BrowseSeconds = 4f, PresentationId = "collector"
                },
                new CustomerArchetype
                {
                    Id = Premium, DisplayName = "Big spender", Weight = 10, Budget = 60,
                    BargainPercent = 50, TolerancePercent = 250, FavoriteTolerancePercent = 250,
                    PatienceSeconds = 30f, BrowseSeconds = 1.5f, PresentationId = "premium"
                }
            };
        }

        /// <summary>Copies and checks a catalog: unique IDs, valid fields, at least one arriving type.</summary>
        public static CustomerArchetype[] Validate(IReadOnlyList<CustomerArchetype> catalog, Func<string, bool> knownSpecies = null)
        {
            if (catalog == null || catalog.Count == 0 || catalog.Count > MaxTypes)
                throw new ArgumentException("A customer catalog needs 1-" + MaxTypes + " types.", nameof(catalog));
            var copy = new CustomerArchetype[catalog.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                var type = catalog[i] ?? throw new ArgumentException("Customer types cannot be null.", nameof(catalog));
                string problem = type.Problem(knownSpecies);
                if (problem != null) throw new ArgumentException(problem, nameof(catalog));
                if (!ids.Add(type.Id)) throw new ArgumentException("Customer type IDs must be unique: " + type.Id, nameof(catalog));
                total += type.Weight;
                copy[i] = type.Copy();
            }
            if (total <= 0) throw new ArgumentException("At least one customer type must arrive (weight above zero).", nameof(catalog));
            return copy;
        }

        /// <summary>
        /// Deterministic weighted choice for the nth visitor of a day. Visitor counts are saved with the day
        /// report, so reloading continues the same rotation instead of rerolling it.
        /// </summary>
        public static CustomerArchetype Pick(IReadOnlyList<CustomerArchetype> catalog, int dayNumber, int visitorIndex)
        {
            if (catalog == null || catalog.Count == 0) throw new ArgumentException("Empty customer catalog.", nameof(catalog));
            long total = 0;
            foreach (var type in catalog) total += Math.Max(0, type.Weight);
            if (total <= 0) throw new ArgumentException("No customer type can arrive.", nameof(catalog));
            uint hash = Mix(unchecked((uint)dayNumber * 0x9E3779B9u) ^ Mix(unchecked((uint)visitorIndex + 0x632BE5ABu)));
            long roll = (long)(hash % (ulong)total);
            foreach (var type in catalog)
            {
                roll -= Math.Max(0, type.Weight);
                if (roll < 0) return type.Copy();
            }
            throw new InvalidOperationException("Weighted customer choice fell outside its total.");
        }

        public static CustomerArchetype Find(IReadOnlyList<CustomerArchetype> catalog, string id)
        {
            if (catalog == null || id == null) return null;
            foreach (var type in catalog) if (type != null && type.Id == id) return type.Copy();
            return null;
        }

        private static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7FEB352Du;
                x ^= x >> 15; x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
