using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    /// <summary>
    /// GR-27: the main menu's words and its creatures. The menu offers Continue (the saved shop in a line), New game (asking
    /// first when it would replace a shop), Settings and Quit, over the live shop; around the title stand a few of the
    /// creatures found, crowns first, then the newest, one of each family before a second of any, each in the rarest colour
    /// found. With none found yet, the first family's founders: the creatures a new game starts with.
    /// </summary>
    public static class GrowthMenu
    {
        /// <summary>How many creatures stand around the title.</summary>
        public const int ShowcaseCount = 6;

        static int Found(GrowthShop s) => s.Discovered.Count(d => GrowthContent.Get(d) != null);

        /// <summary>The saved shop in a line: "Day 12 · Era 3 · 3 stars · 14 of 72 creatures found".</summary>
        public static string Summary(GrowthShop s) =>
            "Day " + s.Day + " · Era " + s.Era + " · " + s.Stars + (s.Stars == 1 ? " star" : " stars") + " · " + Found(s) + " of " + GrowthContent.SpeciesCount + " creatures found";

        /// <summary>What New game asks first, when it would replace a shop.</summary>
        public static string ReplaceQuestion(GrowthShop s) =>
            "Start a new shop? It replaces yours: day " + s.Day + ", Era " + s.Era + ", " + Found(s) + (Found(s) == 1 ? " creature" : " creatures") + " found. This cannot be undone.";

        /// <summary>The rarest colour of a species in the journal (Classic when none is).</summary>
        public static Colour RarestColour(GrowthShop s, string species)
        {
            var best = Colour.Classic;
            foreach (Colour c in Enum.GetValues(typeof(Colour)))
                if (c > best && s.Journal.Contains(GrowthShop.JournalKey(species, c))) best = c;
            return best;
        }

        /// <summary>The creatures around the title, in their places' order (see the class).</summary>
        public static List<(string species, Colour colour)> Showcase(GrowthShop s, int count = ShowcaseCount)
        {
            var found = s.Discovered.Select((id, i) => (def: GrowthContent.Get(id), i)).Where(x => x.def != null)
                .OrderByDescending(x => x.def.Kind == SpeciesKind.Crown).ThenByDescending(x => x.i).Select(x => x.def).ToList();
            if (found.Count == 0)
            {
                string first = GrowthContent.Families[0].Id;
                return GrowthContent.Species.Where(x => x.Family == first && x.Kind == SpeciesKind.Founder).Take(count).Select(x => (x.Id, Colour.Classic)).ToList();
            }
            var picked = new List<SpeciesDef>();
            var families = new HashSet<string>();
            foreach (var def in found) if (picked.Count < count && families.Add(def.Family)) picked.Add(def);
            foreach (var def in found) if (picked.Count < count && !picked.Contains(def)) picked.Add(def);
            return picked.Select(d => (d.Id, RarestColour(s, d.Id))).ToList();
        }
    }
}
