using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-08 (GR.8): the hatchery line T3 and the analyzer T4. The user chose (2026-10-23) a first workshop for the line (a
    // greybox version of GR.11's 8 x 6 m workshop east of the lab annex, at 4 stars, as the pacing model has it; GR-10
    // finishes it) and stock targets for sorting (R08.3's idea, as GR.8 asks: how many of each species the shop's tanks
    // show). The incubator's clutch goes down a chute (laid in pipe mode from the incubator to the sorter) to the sorter,
    // which puts each hatchling into a shop tank while its species is below its target, preferring a tank that already
    // holds it, else into a lab rack, and waits (saying so) when nothing has room (GR.15.1). The auto-doser can follow the
    // targets: it makes the known recipe furthest below its target. A full day on the line (the auto-doser and the sorter
    // running from opening to close, and LineDaySales sales of creatures the line stocked) opens era 5 with 5 stars and a
    // large creature. The research bench's analyzer (level 3) names one undiscovered creature within reach each day, as a
    // mix or a pair, without naming it; its planner (level 4) predicts the offspring of any two known species. Neither
    // machines nor the analyzer ever make a choice for the player (GR.8).

    /// <summary>The analyzer's hint for a day: a mix (its doses) or a pair (two known species) that makes something new.</summary>
    public sealed class GrowthHint
    {
        public string Kind, A, B, Species;
        public int[] Recipe;
        public int Day;
    }

    public sealed partial class GrowthShop
    {
        public const string SorterPower = "sorter";
        /// <summary>A full day on the line counts once its sales of creatures the line stocked reach this many.</summary>
        public const int LineDaySales = 5, MostTarget = 24;
        public const float SortSeconds = 1f;

        /// <summary>GR-11's rack places in the workshop: a lab rack (lab containers are not shown to visitors) on each.</summary>
        public static readonly GrowthPlace[] LabPlaces =
        {
            new GrowthPlace { Id = "r1", Unit = "workshop", Size = "standard", Width = 1.5f, Depth = 1f },
            new GrowthPlace { Id = "r2", Unit = "workshop", Size = "standard", Width = 1.5f, Depth = 1f },
            new GrowthPlace { Id = "r3", Unit = "workshop", Size = "standard", Width = 1.5f, Depth = 1f },
            new GrowthPlace { Id = "r4", Unit = "workshop", Size = "standard", Width = 1.5f, Depth = 1f },
        };

        /// <summary>The workshop (GR.11's tier 4), opened at night with 4 stars in era 4, through the lab annex's east wall.</summary>
        public bool Workshop;
        /// <summary>How many of each species the shop's tanks should show (R08.3's desired stock): the sorter's and the auto-doser's rules.</summary>
        public readonly Dictionary<string, int> StockTargets = new Dictionary<string, int>();
        /// <summary>The auto-doser makes the known recipe furthest below its target, instead of one recipe.</summary>
        public bool AutoByTargets;
        /// <summary>Full days on the line so far, and today's: whether the line has run since opening, and its sales.</summary>
        public int LineDays, LineSalesToday;
        public bool LineToday;
        public float SortProgress;
        public GrowthHint Hint;

        // ---------------------------------------------------------------- reading

        public static ExpansionDef WorkshopDef => GrowthContent.Expansions.First(e => e.Id == "workshop");
        public static GrowthPlace LabPlace(string id) => LabPlaces.FirstOrDefault(p => p.Id == id);
        public bool HasSorter => Level("sorter") > 0;
        public GrowthPipe Chute => Pipes.FirstOrDefault(p => p.From == GrowthLab.Out);
        public IEnumerable<GrowthContainer> Racks => Containers.Where(c => c.Def.Lab);
        public int Target(string species) => StockTargets.TryGetValue(species, out int n) ? n : 0;
        public int Shown(string species) => ShopContainers.Sum(c => c.Occupants.Count(o => o.Species == species));
        public int Racked(string species) => Racks.Sum(c => c.Occupants.Count(o => o.Species == species));
        /// <summary>The line is running: the auto-doser on, the sorter bought and the chute laid (the full day's condition).</summary>
        public bool LineRunning => AutoDoser && AutoOn && HasSorter && Chute != null;
        /// <summary>GR.4's era 5 gate: 5 stars, a large creature, a full day on the line.</summary>
        public bool EraFiveGateMet => Stars >= 5 && LineDays > 0 && Discovered.Any(d => GrowthContent.Get(d).Size == SizeClass.Large);
        public bool Analyzer => Level("bench") >= 3;
        public float SorterUnits => HasSorter ? Stat("sorter", "units") : 0;
        /// <summary>The recipe the auto-doser's next batch makes: its one recipe, or by targets the one furthest below its target.</summary>
        string NextRecipe => AutoByTargets ? TargetRecipe() : AutoRecipe;
        public bool Planner => Level("bench") >= 4;

        static bool FitsIn(GrowthContainer c, string species) => GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(o => o.Species), species);

        // ---------------------------------------------------------------- the workshop and its racks

        public GrowthFailure WorkshopCheck(out string why)
        {
            var w = WorkshopDef;
            why = null;
            if (Workshop) { why = "Already open"; return GrowthFailure.MaxLevel; }
            if (!Annex) { why = "Open the lab annex first: the workshop is through its east wall"; return GrowthFailure.Locked; }
            if (Era < w.Era) { why = "Needs era " + w.Era + ": 4 stars, powered pumps running and 12 species"; return GrowthFailure.Locked; }
            if (Stars < w.Stars) { why = "Needs " + w.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Walls open at night"; return GrowthFailure.NotAtNight; }
            if (Credits < w.Credits) { why = "Needs " + w.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Opens the lab annex's east wall into the workshop (GR.11): the sorter, rack places, and the generator's later levels.</summary>
        public GrowthResult BuyWorkshop()
        {
            var failure = WorkshopCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= WorkshopDef.Credits;
            Workshop = true;
            Raise(GrowthEventKind.Opened, WorkshopDef.Name + " opened", amount: WorkshopDef.Credits);
            return GrowthResult.Ok();
        }

        /// <summary>Why a lab rack cannot be placed on a rack place now (None when it can).</summary>
        public GrowthFailure RackCheck(string place, out string why)
        {
            why = null;
            var level = GrowthContent.Container("rack").Levels[0];
            if (LabPlace(place) == null) { why = "Not a rack place"; return GrowthFailure.NoSuchPlace; }
            if (!Workshop) { why = "Open the workshop first"; return GrowthFailure.Locked; }
            if (ContainerAt(place) != null) { why = "The place is taken"; return GrowthFailure.PlaceTaken; }
            if (Phase != GrowthPhase.Night) { why = "Racks are placed at night"; return GrowthFailure.NotAtNight; }
            if (Credits < level.Credits) { why = "Needs " + level.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        public GrowthResult BuyRack(string place)
        {
            var failure = RackCheck(place, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var def = GrowthContent.Container("rack");
            Credits -= def.Levels[0].Credits;
            Containers.Add(new GrowthContainer { Id = "rack-" + nextId++, Kind = "rack", Place = place });
            Raise(GrowthEventKind.Bought, def.Name + " placed", amount: def.Levels[0].Credits);
            return GrowthResult.Ok();
        }

        // ---------------------------------------------------------------- stock targets and the sorter

        /// <summary>Sets how many of a known species the shop's tanks should show (0 to MostTarget).</summary>
        public GrowthResult SetTarget(string species, int count)
        {
            if (GrowthContent.Get(species) == null || !Discovered.Contains(species)) return GrowthResult.Fail(GrowthFailure.NotDiscovered, "Only a species you have made");
            if (count < 0 || count > MostTarget) return GrowthResult.Fail(GrowthFailure.MaxLevel, "Targets go from 0 to " + MostTarget);
            if (count == 0) StockTargets.Remove(species); else StockTargets[species] = count;
            return GrowthResult.Ok();
        }

        /// <summary>
        /// Where the sorter would put a creature: a shop tank while its species shows fewer than its target (one that already
        /// holds it first, so schools form), else a lab rack, each only where it fits; null when nothing has room.
        /// </summary>
        public GrowthContainer SortDestination(GrowthSpecimen s)
        {
            if (Shown(s.Species) < Target(s.Species))
            {
                var tank = ShopContainers.Where(c => FitsIn(c, s.Species)).OrderByDescending(c => c.Occupants.Count(o => o.Species == s.Species)).ThenBy(c => c.Place, StringComparer.Ordinal).FirstOrDefault();
                if (tank != null) return tank;
            }
            return Racks.Where(c => FitsIn(c, s.Species)).OrderBy(c => c.Place, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>Why the sorter moves nothing now, or null when it has a hatchling to move and somewhere to put it.</summary>
        string SorterIdle()
        {
            if (!HasSorter) return "Not bought";
            if (Chute == null) return "Lay the chute from the incubator";
            if (Hatchlings.Count == 0) return "Waiting for a clutch";
            if (SortDestination(Hatchlings[0]) == null) return "No room for a " + GrowthContent.Get(Hatchlings[0].Species).Name + ": its tanks are at their target or full, and no rack has room";
            return null;
        }

        /// <summary>What the sorter is doing now, in a sentence (the power board and its card).</summary>
        public string SorterStatus
        {
            get
            {
                string idle = SorterIdle();
                if (idle != null) return idle;
                return PowerStatus(PowerUse(SorterPower), "Sorting a " + GrowthContent.Get(Hatchlings[0].Species).Name);
            }
        }

        /// <summary>The sorter moves the first hatchling down the chute into its place: a shop tank (stocked by the line) or a rack.</summary>
        bool SortOne()
        {
            if (Hatchlings.Count == 0) return false;
            var h = Hatchlings[0];
            var dest = SortDestination(h);
            if (dest == null) return false;
            Hatchlings.RemoveAt(0);
            if (!dest.Def.Lab) h.FromLine = true;
            dest.Occupants.Add(h);
            Raise(GrowthEventKind.Sorted, "The sorter put a " + (h.Colour == Colour.Classic ? "" : h.Colour + " ") + h.Def.Name + " in " + (dest.Def.Lab ? RackName(dest.Place) : "the " + dest.Def.Name.ToLowerInvariant() + " at " + dest.Place), h.Species);
            return true;
        }

        public static string RackName(string place) => "rack " + place.Substring(1);

        /// <summary>
        /// The known recipe the auto-doser makes by targets: the founder furthest below its target, counting the shop's tanks, the tray
        /// and a clutch incubating (not the racks: their surplus waits there, and a full rack stops the line on the tray), whose
        /// cultures are piped to the port.
        /// </summary>
        public string TargetRecipe()
        {
            string best = null; int most = 0;
            foreach (var s in GrowthContent.Species)
            {
                if (s.Kind != SpeciesKind.Founder || !Discovered.Contains(s.Id) || s.Doses > MaxDoses) continue;
                bool piped = Enumerable.Range(0, s.Recipe.Length).All(i => s.Recipe[i] == 0 || PortBuffers.Any(b => b.Culture == GrowthContent.Cultures[i].Id));
                if (!piped) continue;
                int short_ = Target(s.Id) - Shown(s.Id) - Hatchlings.Count(h => h.Species == s.Id) - (Incubating && Pending == s.Id ? Clutch : 0);
                if (short_ > most) { best = s.Id; most = short_; }
            }
            return best;
        }

        /// <summary>The auto-doser follows the stock targets: each batch, the known recipe furthest below its target (a recipe chosen goes back to one).</summary>
        public GrowthResult SetAutoByTargets()
        {
            if (!AutoDoser) return GrowthResult.Fail(GrowthFailure.Locked, "The auto-doser comes with the incubator's level 4");
            if (AutoByTargets) return GrowthResult.Fail(GrowthFailure.MaxLevel, "By targets already");
            if (AutoBatch) return GrowthResult.Fail(GrowthFailure.Busy, "Wait for the batch being dosed");
            AutoByTargets = true;
            return GrowthResult.Ok();
        }

        // ---------------------------------------------------------------- the day on the line

        /// <summary>At opening: today counts as a day on the line if the line is running.</summary>
        void StartLineDay() { LineToday = LineRunning; LineSalesToday = 0; }

        void TickLineDay() { if (Phase == GrowthPhase.Day && !LineRunning) LineToday = false; }

        /// <summary>At closing: a full day on the line when it ran all day and stocked LineDaySales of the day's sales.</summary>
        void EndLineDay()
        {
            if (LineToday && LineSalesToday >= LineDaySales)
            {
                LineDays++;
                Raise(GrowthEventKind.LineDay, "A full day on the line: " + LineSalesToday + " sales the line stocked");
            }
            LineToday = false;
        }

        // ---------------------------------------------------------------- the analyzer and the planner

        /// <summary>
        /// The analyzer's hint (GR.9, research bench level 3): an undiscovered founder whose cultures are owned and whose mix
        /// the incubator takes (fewest doses first), else a pair of known species whose offspring is undiscovered and that the
        /// breeding tank takes; never the creature's name. Null when nothing new is within reach.
        /// </summary>
        public GrowthHint FindHint()
        {
            foreach (var s in GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder && !Discovered.Contains(x.Id) && x.Doses <= MaxDoses).OrderBy(x => x.Doses))
                if (Enumerable.Range(0, s.Recipe.Length).All(i => s.Recipe[i] == 0 || Owns(GrowthContent.Cultures[i].Id)))
                    return new GrowthHint { Kind = "mix", Recipe = (int[])s.Recipe.Clone(), Species = s.Id, Day = Day };
            if (!HasBreedingTank) return null;
            var known = GrowthContent.Species.Where(x => Discovered.Contains(x.Id) && (x.Size != SizeClass.Large || LargeParents)).Select(x => x.Id).ToList();
            for (int i = 0; i < known.Count; i++)
                for (int j = i; j < known.Count; j++)
                {
                    var child = GrowthRules.Breed(known[i], known[j]);
                    if (child != null && !Discovered.Contains(child.Id)) return new GrowthHint { Kind = "pair", A = known[i], B = known[j], Species = child.Id, Day = Day };
                }
            return null;
        }

        void RefreshHint() { Hint = Analyzer ? FindHint() : null; }

        /// <summary>The analyzer's hint as the bench card says it: what to try, never what it makes.</summary>
        public string HintText
        {
            get
            {
                if (!Analyzer) return null;
                if (Hint == null) return "Nothing new within reach of your cultures and the breeding tank";
                if (Discovered.Contains(Hint.Species)) return "Today's hint is found; a new one when the shop opens";
                if (Hint.Kind == "mix")
                    return "Try a mix of " + Listed(Enumerable.Range(0, Hint.Recipe.Length).Where(i => Hint.Recipe[i] > 0).Select(i => Hint.Recipe[i] + " " + GrowthContent.Cultures[i].Name).ToList()) + ": something new";
                return "Try pairing a " + GrowthContent.Get(Hint.A).Name + " with a " + (Hint.A == Hint.B ? "second one" : GrowthContent.Get(Hint.B).Name) + " in the breeding tank: something new";
            }
        }

        /// <summary>"a", "a and b", "a, b and c".</summary>
        public static string Listed(IList<string> items) =>
            items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[items.Count - 1];

        /// <summary>The planner (research bench level 4): what two known species breed, or null.</summary>
        public SpeciesDef PlannerPredicts(string a, string b) =>
            Planner && Discovered.Contains(a) && Discovered.Contains(b) ? GrowthRules.Breed(a, b) : null;

        // ---------------------------------------------------------------- saving (version 7)

        void SaveLine(GrowthSave s)
        {
            s.workshop = Workshop;
            s.targetSpecies = StockTargets.Keys.ToArray(); s.targetCounts = StockTargets.Values.ToArray();
            s.autoByTargets = AutoByTargets; s.sorterProgress = SortProgress; s.lineDays = LineDays; s.lineToday = LineToday; s.lineSalesToday = LineSalesToday;
            s.hintKind = Hint?.Kind ?? ""; s.hintRecipe = Hint?.Recipe; s.hintA = Hint?.A ?? ""; s.hintB = Hint?.B ?? ""; s.hintSpecies = Hint?.Species ?? ""; s.hintDay = Hint?.Day ?? 0;
        }

        static string RestoreLine(GrowthShop shop, GrowthSave s)
        {
            if (shop.Workshop && !shop.Annex) return "A workshop without the lab annex";
            if (shop.HasSorter && !shop.Workshop) return "A sorter without the workshop";
            if (shop.Level("generator") >= 3 && !shop.Workshop) return "A turbine without the workshop it stands in";
            foreach (var c in shop.Racks) if (!shop.Workshop || LabPlace(c.Place) == null) return "A rack outside the workshop's rack places";
            var species = s.targetSpecies ?? new string[0];
            for (int i = 0; i < species.Length; i++)
            {
                int count = s.targetCounts != null && i < s.targetCounts.Length ? s.targetCounts[i] : 0;
                if (GrowthContent.Get(species[i]) == null || count < 1 || count > MostTarget || shop.StockTargets.ContainsKey(species[i])) return "A stock target that cannot be";
                shop.StockTargets[species[i]] = count;
            }
            shop.LineDays = Math.Max(0, s.lineDays); shop.LineToday = s.lineToday && shop.Phase == GrowthPhase.Day; shop.LineSalesToday = Math.Max(0, s.lineSalesToday);
            shop.SortProgress = Math.Max(0, Math.Min(SortSeconds, s.sorterProgress));
            if (!string.IsNullOrEmpty(s.hintKind))
            {
                var h = new GrowthHint { Kind = s.hintKind, Recipe = s.hintRecipe, A = string.IsNullOrEmpty(s.hintA) ? null : s.hintA, B = string.IsNullOrEmpty(s.hintB) ? null : s.hintB, Species = s.hintSpecies, Day = s.hintDay };
                bool ok = GrowthContent.Get(h.Species) != null && (h.Kind == "mix" && h.Recipe != null && h.Recipe.Length == GrowthContent.Cultures.Length ||
                    h.Kind == "pair" && GrowthContent.Get(h.A ?? "") != null && GrowthContent.Get(h.B ?? "") != null);
                if (!ok || !shop.Analyzer) return "An analyzer hint that cannot be";
                shop.Hint = h;
            }
            return null;
        }
    }
}
