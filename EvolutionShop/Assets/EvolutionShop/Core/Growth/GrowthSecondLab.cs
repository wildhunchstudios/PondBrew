using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-34 wave 2 (tester round 2, F69, GR.77): the second lab, west of the laboratory and below the research room, its door in
    // the power room's west wall: bought in era 4 with 4 stars for 6,000. Three incubator places along its north wall and three
    // breeding tank places along its west wall, each bought one at a time (3,000, 6,000, 12,000 and 2,000, 4,000, 8,000); they work
    // at the first incubator's and the first breeding tank's levels. An incubator there has no chamber: it makes found recipes from
    // a queue of its own (F73), dosed from the buffers and growers through the pipe the room comes with, onto its own tray; from the
    // first incubator's level 4 its auto-doser doses a chosen recipe by itself, batch after batch. A breeding tank there keeps its
    // own pairs and nurseries (no tint: its water has no port). Moss can be set to any incubator and breeding tank; Nori collects
    // from every tray and nursery. Its machines draw no power.

    /// <summary>An incubator in the second lab: its queue, the batch in it, and its tray.</summary>
    public sealed class GrowthLabIncubator
    {
        public readonly List<string> Queue = new List<string>();
        /// <summary>From the first incubator's level 4: the recipe it doses by itself whenever it is free (null when none).</summary>
        public string Auto;
        public bool Incubating;
        public float Left;
        public string Pending;
        public int[] PendingDoses;
        public readonly List<GrowthSpecimen> Tray = new List<GrowthSpecimen>();
        public int Made;
    }

    /// <summary>A breeding tank in the second lab: its pairs and their nurseries, as the first breeding tank's level gives.</summary>
    public sealed class GrowthLabBreeder
    {
        public readonly GrowthBreedingPair[] Pairs = { new GrowthBreedingPair(), new GrowthBreedingPair() };
    }

    /// <summary>Where the second lab's machines stand (the plan since GR-34: it was built on it), and where a helper stands at each.</summary>
    public static class GrowthLab2
    {
        /// <summary>Its floor (x0, z0, x1, z1): west of the laboratory, below the research room.</summary>
        public static readonly float[] Area = { -12.5f, 5f, -5f, 13.75f };
        /// <summary>The incubators along the north wall, west to east, their fronts to the south (as the laboratory's).</summary>
        public static readonly (float x, float z)[] Incubators = { (-10.625f, 13.12f), (-8.75f, 13.12f), (-6.875f, 13.12f) };
        /// <summary>The breeding tanks along the west wall, south to north, their fronts to the east.</summary>
        public static readonly (float x, float z)[] Breeders = { (-12.09f, 6.6f), (-12.09f, 8.6f), (-12.09f, 10.6f) };
        /// <summary>In front of an incubator's tray, and of a breeding tank's nursery.</summary>
        public static (float x, float z) Tray(int place) => (Incubators[place].x, Incubators[place].z - 0.85f);
        public static (float x, float z) Nursery(int place) => (Breeders[place].x + 1.1f, Breeders[place].z);
        public static bool Contains((float x, float z) p) => p.x < Area[2] && p.x > Area[0] && p.z >= Area[1] && p.z < Area[3];
    }

    public sealed partial class GrowthShop
    {
        public const int Lab2Places = 3;
        public static readonly int[] LabIncubatorCredits = { 3000, 6000, 12000 }, LabBreederCredits = { 2000, 4000, 8000 };
        public static ExpansionDef SecondLabDef => GrowthContent.Expansions.First(e => e.Id == "second-lab");

        public bool SecondLab;
        public readonly List<GrowthLabIncubator> LabIncubators = new List<GrowthLabIncubator>();
        public readonly List<GrowthLabBreeder> LabBreeders = new List<GrowthLabBreeder>();

        /// <summary>The names the cards and the log give the second lab's machines: the laboratory's are the first.</summary>
        public static string LabIncubatorName(int place) => "incubator " + (place + 2);
        public static string LabBreederName(int place) => "breeding tank " + (place + 2);

        // ---------------------------------------------------------------- the room and its places

        public GrowthFailure SecondLabCheck(out string why)
        {
            var room = SecondLabDef;
            why = null;
            if (SecondLab) { why = "Already open"; return GrowthFailure.MaxLevel; }
            if (!Annex) { why = "Open the power room first: its west wall leads in"; return GrowthFailure.Locked; }
            if (Era < room.Era) { why = "Needs Era " + room.Era + ": 4 stars, powered pumps running and 12 species"; return GrowthFailure.Locked; }
            if (Stars < room.Stars) { why = "Needs " + room.Stars + " stars"; return GrowthFailure.Locked; }
            if (Credits < room.Credits) { why = "Needs " + room.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Opens the power room's west wall into the second lab: its places, and the pipe from the buffers along its wall.</summary>
        public GrowthResult BuySecondLab()
        {
            var failure = SecondLabCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= SecondLabDef.Credits;
            SecondLab = true;
            Raise(GrowthEventKind.Opened, "The second lab opened", amount: SecondLabDef.Credits);
            return GrowthResult.Ok();
        }

        public int LabIncubatorNextCredits => LabIncubators.Count < Lab2Places ? LabIncubatorCredits[LabIncubators.Count] : 0;
        public int LabBreederNextCredits => LabBreeders.Count < Lab2Places ? LabBreederCredits[LabBreeders.Count] : 0;

        public GrowthFailure LabIncubatorCheck(out string why)
        {
            why = null;
            if (!SecondLab) { why = "Open the second lab first"; return GrowthFailure.Locked; }
            if (LabIncubators.Count >= Lab2Places) { why = "All " + Lab2Places + " places have one"; return GrowthFailure.MaxLevel; }
            if (Credits < LabIncubatorNextCredits) { why = "Needs " + LabIncubatorNextCredits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Places the next incubator in the second lab (they stand in turn, west to east).</summary>
        public GrowthResult BuyLabIncubator()
        {
            var failure = LabIncubatorCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            int cost = LabIncubatorNextCredits;
            Credits -= cost;
            LabIncubators.Add(new GrowthLabIncubator());
            Raise(GrowthEventKind.Bought, "Incubator " + (LabIncubators.Count + 1) + " placed in the second lab", amount: cost);
            return GrowthResult.Ok();
        }

        public GrowthFailure LabBreederCheck(out string why)
        {
            why = null;
            if (!SecondLab) { why = "Open the second lab first"; return GrowthFailure.Locked; }
            if (!HasBreedingTank) { why = "Buy the first breeding tank first: these work at its level"; return GrowthFailure.Locked; }
            if (LabBreeders.Count >= Lab2Places) { why = "All " + Lab2Places + " places have one"; return GrowthFailure.MaxLevel; }
            if (Credits < LabBreederNextCredits) { why = "Needs " + LabBreederNextCredits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Places the next breeding tank in the second lab (they stand in turn, south to north).</summary>
        public GrowthResult BuyLabBreeder()
        {
            var failure = LabBreederCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            int cost = LabBreederNextCredits;
            Credits -= cost;
            LabBreeders.Add(new GrowthLabBreeder());
            Raise(GrowthEventKind.Bought, "Breeding tank " + (LabBreeders.Count + 1) + " placed in the second lab", amount: cost);
            return GrowthResult.Ok();
        }

        public GrowthLabIncubator LabIncubator(int place) => place >= 0 && place < LabIncubators.Count ? LabIncubators[place] : null;
        public GrowthLabBreeder LabBreeder(int place) => place >= 0 && place < LabBreeders.Count ? LabBreeders[place] : null;

        // ---------------------------------------------------------------- an incubator there: its queue, its auto-doser, its tray

        /// <summary>Why a recipe cannot go in a second-lab incubator's queue (None when it can).</summary>
        public GrowthFailure LabMakeCheck(int place, string species, out string why)
        {
            why = null;
            var inc = LabIncubator(place);
            if (inc == null) { why = "No incubator there yet"; return GrowthFailure.NoSuchPlace; }
            var def = GrowthContent.Get(species);
            if (def == null || !FoundRecipes().Contains(species)) { why = "Only a recipe you have found"; return GrowthFailure.NotDiscovered; }
            if (def.Doses > MaxDoses) { why = "Up to " + MaxDoses + " doses"; return GrowthFailure.ChamberFull; }
            if (inc.Queue.Count >= MakeQueueLimit) { why = "The queue holds " + MakeQueueLimit + " batches"; return GrowthFailure.Busy; }
            return GrowthFailure.None;
        }

        /// <summary>Adds batches of a found recipe to a second-lab incubator's queue; the first starts at once when it can.</summary>
        public GrowthResult QueueLabMake(int place, string species, int batches = 1)
        {
            var failure = LabMakeCheck(place, species, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var inc = LabIncubators[place];
            int added = 0;
            while (added < Math.Max(1, batches) && inc.Queue.Count < MakeQueueLimit) { inc.Queue.Add(species); added++; }
            TickLabIncubator(place);
            return GrowthResult.Ok(added.ToString());
        }

        /// <summary>Takes the last batch of a recipe off a second-lab incubator's queue, or with all, every batch of it.</summary>
        public GrowthResult UnqueueLabMake(int place, string species, bool all = false)
        {
            var inc = LabIncubator(place);
            int at = inc?.Queue.LastIndexOf(species) ?? -1;
            if (at < 0) return GrowthResult.Fail(GrowthFailure.NoDose, "No " + (GrowthContent.Get(species)?.Name ?? species) + " in the queue");
            if (all) inc.Queue.RemoveAll(x => x == species); else inc.Queue.RemoveAt(at);
            return GrowthResult.Ok();
        }

        /// <summary>The second lab's incubators dose by themselves from the first incubator's level 4.</summary>
        public bool LabAutoDoser => Level("incubator") >= 4;

        /// <summary>The recipe a second-lab incubator's auto-doser makes whenever it is free (null stops it).</summary>
        public GrowthResult SetLabAuto(int place, string species)
        {
            var inc = LabIncubator(place);
            if (inc == null) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "No incubator there yet");
            if (species != null)
            {
                if (!LabAutoDoser) return GrowthResult.Fail(GrowthFailure.Locked, "Its auto-doser comes with the first incubator's level 4");
                var def = GrowthContent.Get(species);
                if (def == null || !FoundRecipes().Contains(species)) return GrowthResult.Fail(GrowthFailure.NotDiscovered, "Only a recipe you have found");
                if (def.Doses > MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "Up to " + MaxDoses + " doses");
            }
            inc.Auto = species;
            TickLabIncubator(place);
            return GrowthResult.Ok();
        }

        /// <summary>What a second-lab incubator makes next by itself: its queue's first, else its auto-doser's.</summary>
        string LabNext(GrowthLabIncubator inc) => inc.Queue.Count > 0 ? inc.Queue[0] : LabAutoDoser ? inc.Auto : null;

        /// <summary>Why a recipe cannot start in a second-lab incubator now (null when it can): busy, its tray full, or its doses not in the buffers and growers.</summary>
        string LabCannotStart(GrowthLabIncubator inc, string species)
        {
            if (inc.Incubating) return "Incubating";
            if (inc.Tray.Count > 0) return "Waiting: take the hatchlings off its tray";
            var def = GrowthContent.Get(species);
            if (def.Doses > MaxDoses) return "The recipe needs more doses than the incubators take";
            for (int i = 0; i < def.Recipe.Length; i++)
            {
                if (def.Recipe[i] == 0) continue;
                string c = GrowthContent.Cultures[i].Id;
                if (MakeUnits(c) < def.Recipe[i]) return Owns(c) || Buffers.Any(b => b.Culture == c) ? "Waiting for " + def.Recipe[i] + " " + Name(c) + " in the buffers and growers" : "Needs " + Name(c) + ": its grower";
            }
            return null;
        }

        /// <summary>Why a second-lab incubator does not start its next batch now, or null when it can (or has nothing to make).</summary>
        public string LabWaiting(int place)
        {
            var inc = LabIncubator(place);
            if (inc == null) return "No incubator there yet";
            string next = LabNext(inc);
            return next == null ? null : LabCannotStart(inc, next);
        }

        /// <summary>A second-lab incubator's line for its card and focus.</summary>
        public string LabIncubatorStatus(int place)
        {
            var inc = LabIncubator(place);
            if (inc == null) return "Not placed yet · " + (place == LabIncubators.Count ? LabIncubatorNextCredits + " cr" : "after the one before it");
            if (inc.Incubating) return "Incubating a " + GrowthContent.Get(inc.Pending).Name + " · " + Math.Max(1, (int)Math.Ceiling(inc.Left)) + " s";
            if (inc.Tray.Count > 0) return (inc.Tray.Count > 1 ? inc.Tray.Count + " " + Plural(inc.Tray[0].Def.Name) : "A " + inc.Tray[0].Def.Name) + " on its tray";
            string next = LabNext(inc);
            if (next == null) return MossIncubator == place + 1 && Working("moss") && MossRecipe != null ? "Moss runs it" : "Free: queue a recipe";
            return LabWaiting(place) ?? "Starting a " + GrowthContent.Get(next).Name;
        }

        /// <summary>Starts a second-lab incubator's next batch by itself when it is free and the buffers and growers hold its doses.</summary>
        void TickLabIncubator(int place)
        {
            var inc = LabIncubators[place];
            string species = LabNext(inc);
            if (species == null || LabCannotStart(inc, species) != null) return;
            bool queued = inc.Queue.Count > 0;
            if (queued) inc.Queue.RemoveAt(0);
            StartLabBatch(place, species, "Incubator " + (place + 2) + (queued ? " started a " : "'s auto-doser started a ") + GrowthContent.Get(species).Name
                + (queued && inc.Queue.Count > 0 ? ": " + inc.Queue.Count + " more queued" : ""));
        }

        /// <summary>Doses a found recipe into a second-lab incubator from the buffers and growers (the caller checked it can) and starts it.</summary>
        void StartLabBatch(int place, string species, string said)
        {
            var inc = LabIncubators[place];
            var recipe = GrowthContent.Get(species).Recipe;
            for (int i = 0; i < recipe.Length; i++)
                for (int n = 0; n < recipe[i]; n++) TakeMakeUnit(GrowthContent.Cultures[i].Id);
            inc.Pending = GrowthRules.Mix(recipe, Cultures, Discovered).Species.Id;
            inc.PendingDoses = (int[])recipe.Clone();
            inc.Incubating = true;
            inc.Left = IncubationSeconds;
            inc.Made++;
            Raise(GrowthEventKind.AutoDosed, said, species);
        }

        /// <summary>A second-lab incubator's clutch, at the first incubator's level, onto its own tray.</summary>
        void HatchLab(int place)
        {
            var inc = LabIncubators[place];
            inc.Incubating = false; inc.Left = 0;
            var entry = MixLog.FirstOrDefault(e => e.Doses.SequenceEqual(inc.PendingDoses));
            if (entry != null) { entry.Count++; entry.WasNew = false; }
            else
            {
                var mix = GrowthRules.Mix(inc.PendingDoses, Cultures, Discovered);
                MixLog.Add(new GrowthMixEntry { Doses = inc.PendingDoses, Species = inc.Pending, Exact = mix.Exact, WasNew = !Discovered.Contains(inc.Pending), DistanceToNew = mix.DistanceToNew });
            }
            bool isNew = false;
            for (int i = 0; i < Clutch; i++)
            {
                var h = new GrowthSpecimen { Id = nextId++, Species = inc.Pending, Colour = Colour.Classic };
                inc.Tray.Add(h);
                isNew |= Record(h);
            }
            if (!isNew)
            {
                string name = GrowthContent.Get(inc.Pending).Name;
                Raise(GrowthEventKind.Hatched, "Incubator " + (place + 2) + ": " + (Clutch > 1 ? Clutch + " " + Plural(name) + " hatched" : name + " hatched"), inc.Pending);
            }
            inc.Pending = null; inc.PendingDoses = null;
        }

        /// <summary>Takes a hatchling off a second-lab incubator's tray into the hands.</summary>
        public GrowthResult TakeLabHatchling(int place)
        {
            var inc = LabIncubator(place);
            if (inc == null || inc.Tray.Count == 0) return GrowthResult.Fail(GrowthFailure.NoHatchling, "Nothing on its tray");
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            var h = inc.Tray[0];
            inc.Tray.RemoveAt(0);
            Carried.Add(h);
            TickLabIncubator(place);
            return GrowthResult.Ok(h.Species);
        }

        // ---------------------------------------------------------------- a breeding tank there

        public IEnumerable<GrowthBreedingPair> LabActivePairs(GrowthLabBreeder b) => b.Pairs.Take(PairPlaces);

        public GrowthFailure LabParentCheck(int place, GrowthSpecimen s, out string why)
        {
            why = null;
            var b = LabBreeder(place);
            if (b == null) { why = "No breeding tank there yet"; return GrowthFailure.NoSuchPlace; }
            if (LabActivePairs(b).All(p => p.Parents.Count >= PairSize)) { why = PairPlaces == 1 ? "It takes one pair" : "Its " + PairPlaces + " pairs are full"; return GrowthFailure.ChamberFull; }
            if (s.Def.Size == SizeClass.Large && !LargeParents) { why = "Large parents need the first breeding tank's level 2"; return GrowthFailure.DoesNotFit; }
            return GrowthFailure.None;
        }

        /// <summary>Puts a carried creature into a second-lab breeding tank (the first that may go, or the one asked for).</summary>
        public GrowthResult PutLabParent(int place, int specimenId = 0)
        {
            if (Carried.Count == 0) return GrowthResult.Fail(GrowthFailure.HandsEmpty);
            var candidates = specimenId == 0 ? Carried.ToList() : Carried.Where(x => x.Id == specimenId).ToList();
            if (candidates.Count == 0) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            GrowthSpecimen parent = null; string why = null; var failure = GrowthFailure.None;
            foreach (var x in candidates) { failure = LabParentCheck(place, x, out why); if (failure == GrowthFailure.None) { parent = x; break; } }
            if (parent == null) return GrowthResult.Fail(failure, why);
            var pair = LabActivePairs(LabBreeders[place]).First(p => p.Parents.Count < PairSize);
            Carried.Remove(parent);
            pair.Parents.Add(parent);
            RestartBreeding(pair);
            return GrowthResult.Ok(parent.Species);
        }

        public GrowthResult TakeLabParent(int place, int specimenId)
        {
            var pair = LabBreeder(place)?.Pairs.FirstOrDefault(x => x.Parents.Any(s => s.Id == specimenId));
            if (pair == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            var p = pair.Parents.First(s => s.Id == specimenId);
            pair.Parents.Remove(p);
            Carried.Add(p);
            RestartBreeding(pair);
            return GrowthResult.Ok(p.Species);
        }

        public GrowthResult TakeLabOffspring(int place, int pairIndex = -1)
        {
            var b = LabBreeder(place);
            if (b == null) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "No breeding tank there yet");
            var pair = pairIndex >= 0 && pairIndex < b.Pairs.Length ? b.Pairs[pairIndex] : b.Pairs.FirstOrDefault(p => p.Offspring != null);
            if (pair == null || pair.Offspring == null) return GrowthResult.Fail(GrowthFailure.NoOffspring, "Nothing in its nursery yet");
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            Carried.Add(pair.Offspring);
            pair.Offspring = null;
            RestartBreeding(pair);
            return GrowthResult.Ok();
        }

        /// <summary>A second-lab breeding tank's line for its card and focus.</summary>
        public string LabBreederStatus(int place)
        {
            var b = LabBreeder(place);
            if (b == null) return "Not placed yet · " + (place == LabBreeders.Count ? LabBreederNextCredits + " cr" : "after the one before it");
            var pairs = LabActivePairs(b).ToList();
            if (pairs.Any(p => p.Offspring != null)) return "An offspring waits in the nursery";
            var breeding = pairs.FirstOrDefault(p => p.Breeding);
            if (breeding != null) return "Breeding a " + GrowthRules.Breed(breeding.Parents[0].Species, breeding.Parents[1].Species).Name + " · " + Math.Max(1, (int)Math.Ceiling(breeding.Left)) + " s";
            int parents = pairs.Sum(p => p.Parents.Count);
            return parents == 0 ? "Empty: put in a pair" : "Waiting for a second parent";
        }

        // ---------------------------------------------------------------- every tick

        void TickSecondLab(float seconds)
        {
            if (!SecondLab) return;
            for (int place = 0; place < LabIncubators.Count; place++)
            {
                var inc = LabIncubators[place];
                if (inc.Incubating)
                {
                    inc.Left -= seconds;
                    if (inc.Left <= 0) HatchLab(place);
                }
                TickLabIncubator(place);
            }
            foreach (var b in LabBreeders)
                foreach (var pair in LabActivePairs(b))
                {
                    if (!pair.Breeding) continue;
                    pair.Left -= seconds;
                    if (pair.Left <= 0) BreedLabOffspring(pair);
                }
        }

        /// <summary>A second-lab pair's offspring, in the higher parent colour (no tint there: its water has no port).</summary>
        void BreedLabOffspring(GrowthBreedingPair pair)
        {
            var tint = Tint; Tint = null;
            BreedOffspring(pair);
            Tint = tint;
        }

        /// <summary>Creatures waiting in the second lab: on its trays and in its nurseries.</summary>
        public bool LabCreaturesWaiting => LabIncubators.Any(i => i.Tray.Count > 0) || LabBreeders.Any(b => b.Pairs.Any(p => p.Offspring != null));

        /// <summary>Every creature the second lab holds.</summary>
        public IEnumerable<GrowthSpecimen> SecondLabCreatures =>
            LabIncubators.SelectMany(i => i.Tray).Concat(LabBreeders.SelectMany(b => b.Pairs.SelectMany(p => p.Offspring != null ? p.Parents.Concat(new[] { p.Offspring }) : p.Parents)));

        // ---------------------------------------------------------------- Moss in the second lab

        /// <summary>Which incubator Moss runs his recipe in (0 the laboratory's, k the second lab's incubator k + 1), and which
        /// breeding tank keeps his pair (0 the laboratory's, k the second lab's breeding tank k + 1).</summary>
        public int MossIncubator, MossBreeder;

        /// <summary>Sets the incubator Moss runs his recipe in (0 the laboratory's; 1 to 3 the second lab's, once placed).</summary>
        public GrowthResult SetMossIncubator(int which)
        {
            if (which < 0 || which > LabIncubators.Count) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "No incubator there yet");
            if (MossBatch && which != MossIncubator) return GrowthResult.Fail(GrowthFailure.Busy, "Wait for the batch Moss is dosing");
            MossIncubator = which;
            return GrowthResult.Ok();
        }

        /// <summary>Sets the breeding tank Moss keeps his pair in (0 the laboratory's; 1 to 3 the second lab's, once placed).</summary>
        public GrowthResult SetMossBreeder(int which)
        {
            if (which < 0 || which > LabBreeders.Count) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "No breeding tank there yet");
            MossBreeder = which;
            return GrowthResult.Ok();
        }

        /// <summary>The name of the incubator or breeding tank Moss works at, for his lines.</summary>
        string MossIncubatorName => MossIncubator == 0 ? "the incubator" : LabIncubatorName(MossIncubator - 1);
        string MossBreederName => MossBreeder == 0 ? "the breeding tank" : LabBreederName(MossBreeder - 1);

        /// <summary>The pairs of the breeding tank Moss keeps his pair in (none when it is not placed).</summary>
        IEnumerable<GrowthBreedingPair> MossPairs => MossBreeder == 0 ? ActivePairs : LabBreeder(MossBreeder - 1) is GrowthLabBreeder b ? LabActivePairs(b) : Enumerable.Empty<GrowthBreedingPair>();

        /// <summary>Where Moss stands to put a parent in the breeding tank he keeps his pair in.</summary>
        (float x, float z) MossNursery => MossBreeder == 0 ? GrowthWalk.Nursery : GrowthLab2.Nursery(MossBreeder - 1);

        /// <summary>Why Moss's recipe does not start in his second-lab incubator now (null when it can).</summary>
        string MossLabWaiting()
        {
            var inc = LabIncubator(MossIncubator - 1);
            if (inc == null) return "Incubator " + (MossIncubator + 1) + " is not placed";
            if (inc.Queue.Count > 0) return "Its queue runs " + MossIncubatorName;
            if (LabAutoDoser && inc.Auto != null) return "Its auto-doser runs " + MossIncubatorName;
            string why = LabCannotStart(inc, MossRecipe);
            return why == null ? null : why == "Incubating" ? "Waiting: " + MossIncubatorName + " is incubating" : why.StartsWith("Waiting: take", StringComparison.Ordinal) ? "Waiting for the hatchlings to be taken off " + MossIncubatorName + "'s tray" : why;
        }

        /// <summary>Moss's recipe in a second-lab incubator: he walks to it and starts the batch there, from the pipe the room comes with.</summary>
        bool MossLabRecipe(GrowthHelper h, List<string> reasons)
        {
            string waiting = MossLabWaiting();
            if (waiting != null) { reasons.Add(waiting); return false; }
            int place = MossIncubator - 1;
            if (!Near(h.Spot, GrowthLab2.Tray(place)))
            {
                StartStep(h, "lab-start", GrowthLab2.Tray(place), "lab-incubator:" + place);
                h.Status = "Walking to " + MossIncubatorName + " to start a " + GrowthContent.Get(MossRecipe).Name;
                h.NextJob = 1;
                return true;
            }
            MossLabStart(h);
            return true;
        }

        /// <summary>At his second-lab incubator: Moss starts his recipe there when it still can.</summary>
        void MossLabStart(GrowthHelper h)
        {
            string waiting = MossRecipe == null ? "Choose a recipe on my card" : MossIncubator == 0 ? "Back to the incubator" : MossLabWaiting();
            if (waiting != null) { h.Status = waiting; return; }
            string species = MossRecipe;
            StartLabBatch(MossIncubator - 1, species, "Moss started a " + GrowthContent.Get(species).Name + " in " + MossIncubatorName);
            MossBatches++;
            h.Status = "Started a " + GrowthContent.Get(species).Name + " in " + MossIncubatorName;
            h.NextJob = 1;
        }

        // ---------------------------------------------------------------- saving (version 18)

        void SaveSecondLab(GrowthSave s)
        {
            s.secondLab = SecondLab;
            s.labIncubators = LabIncubators.Select(i => new GrowthSave.LabIncubator
            {
                queue = i.Queue.ToArray(), auto = i.Auto ?? "", incubating = i.Incubating, left = i.Left, pending = i.Pending ?? "",
                pendingDoses = i.PendingDoses ?? new int[0], tray = i.Tray.Select(GrowthSave.Specimen.Of).ToArray(), made = i.Made,
            }).ToArray();
            s.labBreeders = LabBreeders.Select(b => new GrowthSave.LabBreeder
            {
                pairs = b.Pairs.Select(p => new GrowthSave.LabPair { parents = p.Parents.Select(GrowthSave.Specimen.Of).ToArray(), offspring = p.Offspring == null ? new GrowthSave.Specimen[0] : new[] { GrowthSave.Specimen.Of(p.Offspring) }, left = p.Left }).ToArray(),
            }).ToArray();
            s.mossIncubator = MossIncubator; s.mossBreeder = MossBreeder;
        }

        /// <summary>Every creature a save's second lab holds (the save's ids are checked against the rest).</summary>
        static IEnumerable<GrowthSave.Specimen> SavedLabCreatures(GrowthSave s) =>
            (s.labIncubators ?? new GrowthSave.LabIncubator[0]).SelectMany(i => i.tray ?? new GrowthSave.Specimen[0])
            .Concat((s.labBreeders ?? new GrowthSave.LabBreeder[0]).SelectMany(b => (b.pairs ?? new GrowthSave.LabPair[0]).SelectMany(p => (p.parents ?? new GrowthSave.Specimen[0]).Concat(p.offspring ?? new GrowthSave.Specimen[0]))));

        static string RestoreSecondLab(GrowthShop shop, GrowthSave s)
        {
            shop.SecondLab = s.secondLab;
            var incs = s.labIncubators ?? new GrowthSave.LabIncubator[0];
            var breeders = s.labBreeders ?? new GrowthSave.LabBreeder[0];
            if (!shop.SecondLab && (incs.Length > 0 || breeders.Length > 0)) return "Second-lab machines without the second lab";
            if (shop.SecondLab && !shop.Annex) return "A second lab without the power room";
            if (incs.Length > Lab2Places || breeders.Length > Lab2Places) return "More second-lab machines than places";
            if (breeders.Length > 0 && !shop.HasBreedingTank) return "A second-lab breeding tank before the first";
            foreach (var x in incs)
            {
                var inc = new GrowthLabIncubator { Auto = string.IsNullOrEmpty(x.auto) ? null : x.auto, Incubating = x.incubating, Left = x.left, Pending = string.IsNullOrEmpty(x.pending) ? null : x.pending, PendingDoses = x.incubating ? x.pendingDoses : null, Made = Math.Max(0, x.made) };
                foreach (var q in x.queue ?? new string[0]) { if (GrowthContent.Get(q)?.Recipe == null) return "A second-lab queue that cannot be"; inc.Queue.Add(q); }
                if (inc.Queue.Count > MakeQueueLimit) return "A second-lab queue that cannot be";
                if (inc.Auto != null && GrowthContent.Get(inc.Auto)?.Recipe == null) return "A second-lab auto-doser that cannot be";
                if (inc.Incubating && (GrowthContent.Get(inc.Pending) == null || inc.PendingDoses == null || inc.PendingDoses.Length != GrowthContent.Cultures.Length || inc.Left < 0)) return "A second-lab batch that cannot be";
                inc.Tray.AddRange((x.tray ?? new GrowthSave.Specimen[0]).Select(t => t.ToSpecimen()));
                if (inc.Tray.Count > shop.Clutch) return "More hatchlings on a second-lab tray than a clutch";
                shop.LabIncubators.Add(inc);
            }
            foreach (var x in breeders)
            {
                var b = new GrowthLabBreeder();
                var pairs = x.pairs ?? new GrowthSave.LabPair[0];
                if (pairs.Length > b.Pairs.Length) return "A second-lab breeding tank that cannot be";
                for (int k = 0; k < pairs.Length; k++)
                {
                    var pair = b.Pairs[k];
                    pair.Parents.AddRange((pairs[k].parents ?? new GrowthSave.Specimen[0]).Select(p => p.ToSpecimen()));
                    pair.Offspring = pairs[k].offspring != null && pairs[k].offspring.Length > 0 ? pairs[k].offspring[0].ToSpecimen() : null;
                    if (pair.Parents.Count > PairSize || k >= shop.PairPlaces && (pair.Parents.Count > 0 || pair.Offspring != null)) return "A second-lab breeding tank that cannot be";
                    if (pair.Parents.Any(p => p.Def.Size == SizeClass.Large) && !shop.LargeParents) return "A Large parent before the breeding tank's level 2";
                    pair.Left = !pair.Breeding ? 0 : pairs[k].left > 0 ? Math.Min(pairs[k].left, shop.BreedSeconds) : shop.BreedSeconds;
                }
                shop.LabBreeders.Add(b);
            }
            if (s.mossIncubator < 0 || s.mossIncubator > shop.LabIncubators.Count || s.mossBreeder < 0 || s.mossBreeder > shop.LabBreeders.Count) return "Moss at a machine that is not there";
            shop.MossIncubator = s.mossIncubator; shop.MossBreeder = s.mossBreeder;
            return null;
        }
    }
}
