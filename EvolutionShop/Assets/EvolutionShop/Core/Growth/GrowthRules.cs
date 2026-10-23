using System;
using System.Collections.Generic;

namespace EvolutionShop.Growth
{
    /// <summary>The result of a mix: the creature it makes, and whether it lands one dose from something undiscovered.</summary>
    public readonly struct MixResult
    {
        public readonly SpeciesDef Species;
        public readonly bool Exact;
        /// <summary>Doses between the mix and the nearest undiscovered founder the player could make (-1 when none).</summary>
        public readonly int DistanceToNew;
        public bool CloseToNew => DistanceToNew == 1;
        public MixResult(SpeciesDef species, bool exact, int distanceToNew) { Species = species; Exact = exact; DistanceToNew = distanceToNew; }
    }

    /// <summary>GR.7's discovery rules: never-fail mixing for founders, any-pair breeding for hybrids and crowns.</summary>
    public static class GrowthRules
    {
        /// <summary>
        /// GR.7.1: an exact recipe makes its founder (new or known). Any other mix makes the known founder whose recipe is
        /// nearest (sum of each culture's difference), so a new founder is found only by its exact recipe and "one dose
        /// from something new" is a real hint; before anything is known, the nearest of all founders. Only founders whose
        /// cultures are all owned count; ties go to the earlier family, then the roster order. Null only for an empty mix or
        /// no owned founder at all. discovered null means every founder counts as known.
        /// </summary>
        public static MixResult Mix(int[] doses, ICollection<string> ownedCultures, ICollection<string> discovered = null)
        {
            if (doses == null || doses.Length != GrowthContent.Cultures.Length) throw new ArgumentException("A mix has one count per culture.", nameof(doses));
            int total = 0;
            foreach (int d in doses) { if (d < 0) throw new ArgumentOutOfRangeException(nameof(doses)); total += d; }
            if (total == 0) return new MixResult(null, false, -1);
            bool anyKnown = false;
            if (discovered != null)
                foreach (var s in GrowthContent.Species)
                    if (s.Kind == SpeciesKind.Founder && discovered.Contains(s.Id) && Owns(s.Recipe, ownedCultures)) { anyKnown = true; break; }
            SpeciesDef best = null; int bestDistance = int.MaxValue, bestEra = int.MaxValue, toNew = -1;
            foreach (var s in GrowthContent.Species)
            {
                if (s.Kind != SpeciesKind.Founder || !Owns(s.Recipe, ownedCultures)) continue;
                int distance = Distance(doses, s.Recipe);
                bool known = discovered == null || discovered.Contains(s.Id);
                if (!known && (toNew < 0 || distance < toNew)) toNew = distance;
                if (distance != 0 && anyKnown && !known) continue;
                int era = GrowthContent.Family(s.Family).Era;
                if (distance < bestDistance || distance == bestDistance && era < bestEra) { best = s; bestDistance = distance; bestEra = era; }
            }
            return new MixResult(best, bestDistance == 0, toNew);
        }

        public static int Distance(int[] a, int[] b)
        {
            int d = 0;
            for (int i = 0; i < a.Length; i++) d += Math.Abs(a[i] - b[i]);
            return d;
        }

        static bool Owns(int[] recipe, ICollection<string> owned)
        {
            for (int i = 0; i < recipe.Length; i++)
                if (recipe[i] > 0 && (owned == null || !owned.Contains(GrowthContent.Cultures[i].Id))) return false;
            return true;
        }

        /// <summary>
        /// GR.7.2: two creatures breed the species whose breeding value is nearest their average (rounded up), crowns
        /// excluded; a special pair makes its crown; a same-species pair breeds true. Ties go to the lower value.
        /// </summary>
        public static SpeciesDef Breed(string a, string b)
        {
            var pa = GrowthContent.Get(a) ?? throw new ArgumentException("Unknown species " + a, nameof(a));
            var pb = GrowthContent.Get(b) ?? throw new ArgumentException("Unknown species " + b, nameof(b));
            foreach (var s in GrowthContent.Species)
                if (s.Kind == SpeciesKind.Crown && (s.PairA == a && s.PairB == b || s.PairA == b && s.PairB == a)) return s;
            if (a == b) return pa;
            int target = (pa.BreedingValue + pb.BreedingValue + 1) / 2;
            SpeciesDef best = null; int bestGap = int.MaxValue;
            foreach (var s in GrowthContent.Species)
            {
                if (s.Kind == SpeciesKind.Crown) continue;
                int gap = Math.Abs(s.BreedingValue - target);
                if (gap < bestGap || gap == bestGap && s.BreedingValue < best.BreedingValue) { best = s; bestGap = gap; }
            }
            return best;
        }

        /// <summary>GR.7.2: a tint sets the colour; untinted offspring take the higher-order parent colour.</summary>
        public static Colour BreedColour(Colour a, Colour b, string tintCulture = null)
        {
            switch (tintCulture)
            {
                case "bloom": return Colour.Sunny;
                case "flow": return Colour.Deep;
                case "mist": return Colour.Shimmer;
            }
            return (Colour)Math.Max((int)a, (int)b);
        }

        /// <summary>Spaces a list of creatures takes; whether one more fits a container's level (GR.5).</summary>
        public static int SpacesOf(IEnumerable<string> species)
        {
            int n = 0;
            foreach (var id in species) n += (int)GrowthContent.Get(id).Size;
            return n;
        }

        public static bool Fits(ContainerDef container, int level, IEnumerable<string> occupants, string speciesId)
        {
            var s = GrowthContent.Get(speciesId);
            if (s == null || (int)s.Size > (int)container.Largest) return false;
            return SpacesOf(occupants) + (int)s.Size <= (int)container.Levels[level - 1].Stat("spaces");
        }

        // ---------------------------------------------------------------- validation (GR-01)

        /// <summary>
        /// Every problem with the content: duplicate ids or recipes, recipes too large for their era's incubator, hybrids
        /// whose intended pair does not resolve to them, unreachable species, and an unfinishable exhibition. Empty when good.
        /// </summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            var recipes = new HashSet<string>();
            var values = new HashSet<int>();
            foreach (var s in GrowthContent.Species)
            {
                if (!ids.Add(s.Id)) problems.Add("Duplicate species id " + s.Id);
                if (s.Kind == SpeciesKind.Founder)
                {
                    if (s.Recipe == null || s.Doses < 2) problems.Add(s.Id + ": a founder needs a recipe of at least 2 doses");
                    else if (!recipes.Add(string.Join(",", s.Recipe))) problems.Add(s.Id + ": duplicate recipe");
                    int maxDoses = MaxDosesByEra(GrowthContent.Family(s.Family).Era);
                    if (s.Doses > maxDoses) problems.Add(s.Id + ": " + s.Doses + " doses is more than its era's incubator allows (" + maxDoses + ")");
                    var exact = Mix(s.Recipe, CulturesByEra(GrowthContent.Family(s.Family).Era));
                    if (exact.Species != s) problems.Add(s.Id + ": its own recipe makes " + exact.Species?.Id);
                }
                if (s.Kind != SpeciesKind.Crown && !values.Add(s.BreedingValue)) problems.Add(s.Id + ": duplicate breeding value " + s.BreedingValue);
                if (s.Kind == SpeciesKind.Crown && (GrowthContent.Get(s.PairA) == null || GrowthContent.Get(s.PairB) == null || s.PairA == s.PairB))
                    problems.Add(s.Id + ": a crown needs two different known parents");
                if (s.Price <= 0) problems.Add(s.Id + ": price");
            }
            if (GrowthContent.Species.Length != GrowthContent.SpeciesCount) problems.Add("The roster has " + GrowthContent.Species.Length + " species, not " + GrowthContent.SpeciesCount);
            foreach (var (hybrid, a, b) in GrowthContent.IntendedPairs)
            {
                var result = Breed(a, b);
                if (result?.Id != hybrid) problems.Add(hybrid + ": its intended pair " + a + " x " + b + " makes " + result?.Id);
            }
            foreach (var s in GrowthContent.Species)
                if (s.Kind == SpeciesKind.Hybrid && Array.FindIndex(GrowthContent.IntendedPairs, p => p.hybrid == s.Id) < 0)
                    problems.Add(s.Id + ": a hybrid with no intended pair");
            var reach = ReachableEra();
            foreach (var s in GrowthContent.Species)
            {
                int expected = s.Kind == SpeciesKind.Founder ? GrowthContent.Family(s.Family).Era : Math.Max(GrowthContent.Family(s.Family).Era, BreedingEra);
                if (!reach.TryGetValue(s.Id, out int era)) problems.Add(s.Id + ": unreachable");
                else if (era > expected) problems.Add(s.Id + ": first reachable in era " + era + ", expected by era " + expected);
            }
            int crowns = 0, reachable = 0;
            foreach (var s in GrowthContent.Species)
                if (reach.ContainsKey(s.Id)) { reachable++; if (s.Kind == SpeciesKind.Crown) crowns++; }
            if (reachable < GrowthContent.ExhibitionSpecies || crowns < GrowthContent.ExhibitionCrowns) problems.Add("The exhibition cannot be reached");
            return problems;
        }

        /// <summary>The breeding tank arrives in era 2 (GR.4), so hybrids and crowns are never made earlier.</summary>
        public const int BreedingEra = 2;

        public static int MaxDosesByEra(int era) => era <= 1 ? 3 : era == 2 ? 4 : 6;

        public static List<string> CulturesByEra(int era)
        {
            var owned = new List<string>();
            foreach (var c in GrowthContent.Cultures) if (c.Era <= era) owned.Add(c.Id);
            return owned;
        }

        /// <summary>
        /// The earliest era each species can be made in: founders when their cultures and incubator size arrive (and the
        /// family's era), hybrids and crowns once both parents are reachable and the breeding tank exists (era 2).
        /// </summary>
        public static Dictionary<string, int> ReachableEra()
        {
            var era = new Dictionary<string, int>();
            for (int e = 1; e <= 5; e++)
            {
                var cultures = CulturesByEra(e);
                foreach (var s in GrowthContent.Species)
                    if (s.Kind == SpeciesKind.Founder && !era.ContainsKey(s.Id) && Owns(s.Recipe, cultures) && s.Doses <= MaxDosesByEra(e) &&
                        GrowthContent.Family(s.Family).Era <= e)
                        era[s.Id] = e;
                if (e < 2) continue;
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    var known = new List<string>(era.Keys);
                    foreach (var x in known)
                        foreach (var y in known)
                        {
                            var child = Breed(x, y);
                            if (child == null || era.ContainsKey(child.Id)) continue;
                            if (GrowthContent.Family(child.Family).Era > e) continue;
                            if (!CanBreed(x, e) || !CanBreed(y, e)) continue;
                            era[child.Id] = e; grew = true;
                        }
                }
            }
            return era;
        }

        /// <summary>Large parents need the breeding tank's second level (era 2 onward in this model).</summary>
        static bool CanBreed(string species, int era) => GrowthContent.Get(species).Size != SizeClass.Large || era >= 2;
    }
}
