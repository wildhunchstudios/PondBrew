using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    // AD-E gameplay prototype (AD.6): wishes, shop stars, tank themes and a shorter day with one rush. These are pure
    // rules; the runtime applies them only while the development prototype switch is on, and the classic rules
    // stay exactly as they were while it is off. Nothing here is saved.

    public enum WishMatch { None, Close, Exact }

    /// <summary>What one visitor wishes for: a species, or a family's trait such as "glows". A preference, not a quest.</summary>
    public sealed class ShopWish
    {
        readonly string[] exact, close;

        public string Key { get; }
        /// <summary>How the wish reads after "Wants", such as "a Ribbonfin" or "something that glows".</summary>
        public string Label { get; }
        /// <summary>A short name for the bubble over the visitor's head.</summary>
        public string Short { get; }
        public bool IsSpecies { get; }
        public IReadOnlyList<string> Exact => Array.AsReadOnly(exact);
        public IReadOnlyList<string> Close => Array.AsReadOnly(close);

        public ShopWish(string key, string label, string shortName, bool isSpecies, string[] exactSpecies, string[] closeSpecies)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(label) || exactSpecies == null || exactSpecies.Length == 0)
                throw new ArgumentException("A wish needs a key, a label and at least one species.");
            Key = key; Label = label; Short = string.IsNullOrEmpty(shortName) ? label : shortName; IsSpecies = isSpecies;
            exact = (string[])exactSpecies.Clone();
            close = closeSpecies == null ? new string[0] : Array.FindAll(closeSpecies, id => Array.IndexOf(exactSpecies, id) < 0);
        }

        public WishMatch Match(string speciesId)
        {
            if (speciesId == null) return WishMatch.None;
            if (Array.IndexOf(exact, speciesId) >= 0) return WishMatch.Exact;
            return Array.IndexOf(close, speciesId) >= 0 ? WishMatch.Close : WishMatch.None;
        }
    }

    public static class ShopWishes
    {
        /// <summary>An exact match tips this share of the agreed price (at least 1 credit) and earns a reputation point.</summary>
        public const int TipPercent = 25;
        /// <summary>A close match haggles this share off the tag (at least 1 credit, never below 1).</summary>
        public const int HagglePercent = 15;
        /// <summary>Display scores: a visitor heads for an exact match first, then a close one, before its usual choice.</summary>
        public const int ExactScore = 8, CloseScore = 4;

        public static int Tip(int price) => price <= 0 ? 0 : Math.Max(1, (int)Math.Round(price * TipPercent / 100.0, MidpointRounding.AwayFromZero));

        public static int Haggled(int tag)
        {
            if (tag <= 1) return Math.Max(0, tag);
            int off = Math.Max(1, (int)Math.Round(tag * HagglePercent / 100.0, MidpointRounding.AwayFromZero));
            return Math.Max(1, tag - off);
        }

        /// <summary>
        /// The family a species belongs to for wishes and themes (AD.3): species that need Spark are Sparkline, whatever
        /// their saved family (the Lanternfin is still recorded as a Brookkin until AD-F's families).
        /// </summary>
        public static string Family(SpeciesDefinition species) => species == null ? null : species.Spark > 0 ? "Sparkline" : species.FamilyId;

        /// <summary>The trait a family's visitors wish for when they do not name a species.</summary>
        public static string FamilyTrait(string familyId)
        {
            switch (familyId)
            {
                case "Sparkline": return "glows";
                case "Airfin": return "glides";
                case "Brookkin": return "splashes";
                default: return "is rare";
            }
        }

        public static ShopWish ForSpecies(SpeciesDefinition species, IReadOnlyList<SpeciesDefinition> all)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            var close = new List<string>();
            if (all != null) foreach (var other in all) if (other != null && other.Id != species.Id && Family(other) == Family(species)) close.Add(other.Id);
            string name = species.DisplayName ?? species.Id;
            return new ShopWish("species:" + species.Id, Article(name) + " " + name, name, true, new[] { species.Id }, close.ToArray());
        }

        public static ShopWish ForFamily(string familyId, IReadOnlyList<SpeciesDefinition> all)
        {
            var exact = new List<string>();
            if (all != null) foreach (var species in all) if (species != null && Family(species) == familyId) exact.Add(species.Id);
            if (exact.Count == 0) throw new ArgumentException("No species in family " + familyId, nameof(familyId));
            string trait = FamilyTrait(familyId);
            return new ShopWish("trait:" + familyId, "something that " + trait, "It " + trait, false, exact.ToArray(), null);
        }

        /// <summary>
        /// A visitor's wish, deterministic for the day and their place in it, from the species the shop has discovered.
        /// Collectors name one of their favourites, Big spenders the most valuable species, Bargain hunters a species or
        /// a trait, Casual visitors a trait. Null when nothing is discovered.
        /// </summary>
        public static ShopWish Pick(int dayNumber, int visitorIndex, string archetypeId, string[] favourites,
            IReadOnlyList<SpeciesDefinition> discovered, IReadOnlyList<SpeciesDefinition> all)
        {
            if (discovered == null || discovered.Count == 0) return null;
            var known = new List<SpeciesDefinition>();
            foreach (var species in discovered) if (species != null) known.Add(species);
            if (known.Count == 0) return null;
            known.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            uint hash = Mix(unchecked((uint)dayNumber * 0x85EBCA6Bu) ^ Mix(unchecked((uint)visitorIndex * 0xC2B2AE35u + 0x27D4EB2Fu)));
            switch (archetypeId)
            {
                case CustomerArchetypes.Collector:
                {
                    var liked = known.FindAll(s => favourites != null && Array.IndexOf(favourites, s.Id) >= 0);
                    var from = liked.Count > 0 ? liked : known;
                    return ForSpecies(from[(int)(hash % (uint)from.Count)], all);
                }
                case CustomerArchetypes.Premium:
                {
                    var best = known[0];
                    foreach (var species in known) if (species.SaleValue > best.SaleValue) best = species;
                    return ForSpecies(best, all);
                }
                case CustomerArchetypes.Bargain:
                    if ((hash & 1u) == 0) return ForSpecies(known[(int)((hash >> 1) % (uint)known.Count)], all);
                    return ForFamily(FamilyOf(known, hash >> 1), all);
                default:
                    return ForFamily(FamilyOf(known, hash), all);
            }
        }

        static string FamilyOf(List<SpeciesDefinition> known, uint hash)
        {
            var families = new List<string>();
            foreach (var species in known) if (!families.Contains(Family(species))) families.Add(Family(species));
            families.Sort(string.CompareOrdinal);
            return families[(int)(hash % (uint)families.Count)];
        }

        static string Article(string name) => name.Length > 0 && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an" : "a";

        internal static uint Mix(uint value)
        {
            value ^= value >> 16; value = unchecked(value * 0x7FEB352Du);
            value ^= value >> 15; value = unchecked(value * 0x846CA68Bu);
            return value ^ (value >> 16);
        }
    }

    /// <summary>A visitor's wish and the shop's tank themes, as the visit sees them.</summary>
    public sealed class VisitPreference
    {
        public ShopWish Wish;
        /// <summary>Extra tolerance points a display gives a species (its tank theme), by display and species.</summary>
        public Func<string, string, int> ThemeTolerance;
    }

    /// <summary>The 1-5 star storefront sign over the reputation rules, which stay as they are.</summary>
    public static class ShopStars
    {
        public const int Max = 5, BudgetStars = 4, ExhibitionStars = 5, BudgetBonusPercent = 25;
        static readonly int[] thresholds = { 0, 10, 30, 55, 85 };

        public static int Threshold(int stars) => thresholds[Math.Max(1, Math.Min(Max, stars)) - 1];

        public static int Of(int points)
        {
            int stars = 1;
            for (int i = 1; i < thresholds.Length; i++) if (points >= thresholds[i]) stars = i + 1;
            return stars;
        }

        /// <summary>Reputation points the next star needs; -1 at five stars.</summary>
        public static int NextThreshold(int points)
        {
            int stars = Of(points);
            return stars >= Max ? -1 : thresholds[stars];
        }

        /// <summary>What reaching this many stars unlocks, for the sign and the receipt.</summary>
        public static string Unlock(int stars)
        {
            switch (stars)
            {
                case 1: return "Casual visitors and Bargain hunters";
                case 2: return "Collectors visit";
                case 3: return "Big spenders visit";
                case 4: return "Bigger budgets (+25%)";
                case 5: return "The Grand Exhibition (coming later)";
                default: return "";
            }
        }

        public static int StarsFor(string archetypeId)
            => archetypeId == CustomerArchetypes.Collector ? 2 : archetypeId == CustomerArchetypes.Premium ? 3 : 1;

        public static bool Unlocked(string archetypeId, int stars) => stars >= StarsFor(archetypeId);

        /// <summary>Copies of an already adjusted catalog: types above the shop's stars stay away, and four stars raise budgets.</summary>
        public static CustomerArchetype[] ApplyTo(IReadOnlyList<CustomerArchetype> catalog, int stars)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new CustomerArchetype[catalog.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var type = catalog[i].Copy();
                if (!Unlocked(type.Id, stars)) type.Weight = 0;
                if (stars >= BudgetStars && type.Budget != int.MaxValue)
                    type.Budget = (int)Math.Min(int.MaxValue - 1L, (long)Math.Round(type.Budget * (100 + BudgetBonusPercent) / 100.0, MidpointRounding.AwayFromZero));
                result[i] = type;
            }
            return result;
        }
    }

    public enum ShopTheme { None, Reef, Glow, Sky }

    /// <summary>Decor near a display sets its theme; matching species sell for more and draw a type of visitor.</summary>
    public static class ShopThemes
    {
        /// <summary>Decor counts for a display within this distance, centre to centre, in metres.</summary>
        public const float Radius = 3f;
        /// <summary>Extra price tolerance, in points of the standard price, for a species in a display that matches its family.</summary>
        public const int TolerancePoints = 25;
        /// <summary>Arrival weight each matching display adds to its theme's visitor type, up to MaxAttract.</summary>
        public const int AttractWeight = 5, MaxAttract = 15;

        public static ShopTheme OfDecor(string definitionId)
            => definitionId == ShopLayout.CoralPlanterId ? ShopTheme.Reef : definitionId == ShopLayout.BubbleLampId ? ShopTheme.Glow :
                definitionId == ShopLayout.SillyStatueId ? ShopTheme.Sky : ShopTheme.None;

        public static string Family(ShopTheme theme)
            => theme == ShopTheme.Reef ? "Brookkin" : theme == ShopTheme.Glow ? "Sparkline" : theme == ShopTheme.Sky ? "Airfin" : null;

        public static string Attracts(ShopTheme theme)
            => theme == ShopTheme.Reef ? CustomerArchetypes.Collector : theme == ShopTheme.Glow ? CustomerArchetypes.Premium :
                theme == ShopTheme.Sky ? CustomerArchetypes.Casual : null;

        public static string Name(ShopTheme theme)
            => theme == ShopTheme.Reef ? "Reef" : theme == ShopTheme.Glow ? "Glow" : theme == ShopTheme.Sky ? "Sky" : "";

        public static bool Matches(ShopTheme theme, string familyId) => theme != ShopTheme.None && familyId != null && Family(theme) == familyId;

        public static int ToleranceFor(ShopTheme theme, string familyId) => Matches(theme, familyId) ? TolerancePoints : 0;

        /// <summary>Copies of the catalog with each matching display drawing its theme's type (only types that already arrive).</summary>
        public static CustomerArchetype[] Attract(IReadOnlyList<CustomerArchetype> catalog, IEnumerable<ShopTheme> matchingDisplays)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var extra = new Dictionary<string, int>(StringComparer.Ordinal);
            if (matchingDisplays != null)
                foreach (var theme in matchingDisplays)
                {
                    string id = Attracts(theme);
                    if (id == null) continue;
                    extra.TryGetValue(id, out int value);
                    extra[id] = Math.Min(MaxAttract, value + AttractWeight);
                }
            var result = new CustomerArchetype[catalog.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var type = catalog[i].Copy();
                if (type.Weight > 0 && extra.TryGetValue(type.Id, out int add)) type.Weight += add;
                result[i] = type;
            }
            return result;
        }
    }

    /// <summary>The prototype's day: shorter, with one rush when visitors arrive as soon as there is room.</summary>
    public static class ShopDayPlan
    {
        public const float PrototypeSeconds = 330f;
        public const float RushStart = .5f, RushEnd = .65f, RushArrivalSeconds = 1.5f;

        /// <summary>How far into the day, 0 at opening and 1 at closing.</summary>
        public static float Progress(float dayLength, float secondsRemaining)
            => dayLength <= 0 ? 0 : Math.Max(0, Math.Min(1, 1 - secondsRemaining / dayLength));

        public static bool InRush(float dayLength, float secondsRemaining)
        {
            float progress = Progress(dayLength, secondsRemaining);
            return progress >= RushStart && progress < RushEnd;
        }

        public static float RushStartsAt(float dayLength) => dayLength * RushStart;
        public static float RushEndsAt(float dayLength) => dayLength * RushEnd;
    }
}
