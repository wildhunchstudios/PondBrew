using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-02 (GR.18): the greybox ruleset of the grow-from-nothing redesign, the first era and the step into the second.
    // GR-03 adds the containers of GR.5: places sized for them, Starter, Large and Show tanks with their levels, moving and
    // selling them, size classes (small 1, medium 2, large 4 spaces), water and harmony (comfort, era 3), appeal, visitors
    // who browse one container at a time and choose a specimen from it, Collectors who reserve a pair (all or none) and
    // Big spenders who favour bigger creatures.
    // Pure and engine-free, read by GrowthSession (Runtime) and tested in EditMode. Numbers come from GrowthContent; this
    // class holds the state and the rules: growers and the siphon, the incubator's never-fail mixing with its mix log and
    // "one dose from something new", carrying hatchlings, Starter tanks of 4 spaces with occupant lists and schools,
    // station levels 1-2, a 5:30 day of visitors who reserve one specimen each and pay at the counter, reputation and
    // stars, the era 2 gate and shop unit B. Night is untimed; everything that changes the shop's layout happens at night.

    public enum GrowthPhase { Night, Day }

    public enum GrowthFailure
    {
        None, NotAtNight, NotDuringDay, UnknownCulture, NotOwned, SiphonFull, GrowerEmpty, NoDose, ChamberFull, ChamberEmpty,
        IncubatorBusy, HatchlingWaiting, NoHatchling, HandsFull, HandsEmpty, NoSuchContainer, DoesNotFit, Reserved, NoSuchSpecimen,
        NotEnoughCredits, MaxLevel, Locked, PlaceTaken, NoSuchPlace, NoVisitorWaiting, NotAFounder, NotDiscovered, Busy,
        TooBigForPlace, NotEmpty, LastContainer, SameWater, UnknownContainer,
    }

    public readonly struct GrowthResult
    {
        public readonly GrowthFailure Failure;
        public readonly string Detail;
        public bool Success => Failure == GrowthFailure.None;
        GrowthResult(GrowthFailure failure, string detail) { Failure = failure; Detail = detail ?? ""; }
        public static GrowthResult Ok(string detail = "") => new GrowthResult(GrowthFailure.None, detail);
        public static GrowthResult Fail(GrowthFailure failure, string detail = "") => new GrowthResult(failure, detail);
        public override string ToString() => Success ? "ok" + (Detail.Length > 0 ? " (" + Detail + ")" : "") : Failure + (Detail.Length > 0 ? ": " + Detail : "");
    }

    public sealed class GrowthSpecimen
    {
        public int Id;
        public string Species;
        public Colour Colour;
        /// <summary>The visitor who reserved it (one reservation per specimen, §6.2), or 0.</summary>
        public int ReservedBy;
        public SpeciesDef Def => GrowthContent.Get(Species);
    }

    /// <summary>A place on the shop floor for a container (GR.11): its unit and the largest footprint it takes.</summary>
    public sealed class GrowthPlace
    {
        public string Id, Unit, Size;
        public float Width, Depth;
        public bool Takes(ContainerDef c) => c.Width <= Width + 0.001f && c.Depth <= Depth + 0.001f;
    }

    public sealed class GrowthContainer
    {
        public string Id, Kind, Place;
        public int Level = 1;
        /// <summary>GR.5: the water the container keeps (fresh unless a water kit changed it, era 3).</summary>
        public Water Water = Water.Fresh;
        public readonly List<GrowthSpecimen> Occupants = new List<GrowthSpecimen>();
        public ContainerDef Def => GrowthContent.Container(Kind);
        public int Spaces => (int)Def.Levels[Level - 1].Stat("spaces");
        public int Used => Occupants.Sum(s => (int)s.Def.Size);
        public int Free => Spaces - Used;
        /// <summary>GR.5: three or more of one species in one container swim together as a school.</summary>
        public bool InSchool(string species) => Occupants.Count(s => s.Species == species) >= GrowthShop.SchoolSize;
    }

    public enum GrowthVisitorState { Browsing, Queued, Leaving }
    public enum GrowthVisitOutcome { None, Bought, NothingAffordable, TiredOfWaiting, Closed }

    public sealed class GrowthVisitor
    {
        public int Id;
        public string Type;
        public int Budget;
        public GrowthVisitorState State;
        public GrowthVisitOutcome Outcome;
        /// <summary>What the visitor reserved: one specimen, or a pair all or none (GR.15.1); empty until they decide.</summary>
        public readonly List<int> SpecimenIds = new List<int>();
        public int SpecimenId => SpecimenIds.Count > 0 ? SpecimenIds[0] : 0;
        /// <summary>The container the visitor browses now and, once decided, the one that holds their choice.</summary>
        public string ContainerId;
        /// <summary>GR-03: the containers this visitor means to browse, in order (most appealing first), and how far along.</summary>
        public readonly List<string> Plan = new List<string>();
        public int Browsed;
        public float Clock, Patience, BrowseFor = GrowthShop.BrowseSeconds;
    }

    public sealed class GrowthMixEntry
    {
        public int[] Doses;
        public string Species;
        public bool Exact, WasNew;
        /// <summary>Doses to the nearest undiscovered founder before this mix (-1 when none): 1 is "close to something new".</summary>
        public int DistanceToNew;
        public int Count = 1;
    }

    public enum GrowthEventKind { Collected, Hatched, Discovery, Sale, Refusal, Impatient, StarUp, EraUp, DayStarted, DayEnded, Bought, Upgraded, Moved, Sold }

    public sealed class GrowthEvent
    {
        public GrowthEventKind Kind;
        public string Text, Species;
        public int Amount;
    }

    public sealed class GrowthDaySummary
    {
        public int Day, Visitors, Sales, Revenue, Refusals, Impatient, ReputationGained, StarsBefore, StarsAfter;
        /// <summary>Passers-by who did not come in because every tank was empty (no reputation lost).</summary>
        public int WalkedPast;
    }

    public sealed class GrowthShop
    {
        public const int CarryLimit = 3, SchoolSize = 3, GreyboxMaxLevel = 2, RepPerImpatient = -1;
        /// <summary>A visit: walking in and browsing, waiting in the queue, walking out. A visitor counts toward the shop's
        /// "visitors at once" until they have walked out, so a small shop serves a few people at a time (GR.10.1).</summary>
        public const float BrowseSeconds = 6f, QueuePatience = 40f, LeaveSeconds = 8f, SchoolBudgetBonus = 1f + GrowthContent.SchoolTolerance;
        /// <summary>GR-03: a visitor looks into at most three containers, about 5 s for each after the first.</summary>
        public const int MostBrowsed = 3;
        public const double BigSpenderSizeWeight = 0.5;
        public const float NextBrowseSeconds = 5f;
        /// <summary>
        /// GR-03's floor places (GR.11): "long" places along walls and islands take Starter and Large tanks (2.5 x 1 m),
        /// "standard" ones a Starter tank (1.5 x 1 m), and unit B's east wall one feature place for a Show tank (3 x 1.5 m).
        /// </summary>
        public static readonly GrowthPlace[] Places =
        {
            new GrowthPlace { Id = "a1", Unit = "unit-a", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "a2", Unit = "unit-a", Size = "standard", Width = 1.5f, Depth = 1f },
            new GrowthPlace { Id = "a3", Unit = "unit-a", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "a4", Unit = "unit-a", Size = "standard", Width = 1.5f, Depth = 1f },
            new GrowthPlace { Id = "b1", Unit = "unit-b", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "b2", Unit = "unit-b", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "b3", Unit = "unit-b", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "b4", Unit = "unit-b", Size = "feature", Width = 3f, Depth = 1.5f },
        };
        public static readonly string[] UnitAPlaces = Places.Where(p => p.Unit == "unit-a").Select(p => p.Id).ToArray(), UnitBPlaces = Places.Where(p => p.Unit == "unit-b").Select(p => p.Id).ToArray();
        /// <summary>The shop containers a player buys on the floor (the lab rack comes with the lab, later).</summary>
        public static readonly string[] ShopKinds = { "starter", "large", "show" };
        /// <summary>The stations the greybox has (GR-02: siphon, growers, incubator and counter at levels 1-2).</summary>
        public static readonly string[] GreyboxStations = { "siphon", "grower", "incubator", "counter" };

        public int Credits, Reputation, Rp, Era = 1, Day = 1, SalesTotal, RefusalsTotal;
        public GrowthPhase Phase = GrowthPhase.Night;
        public float DaySecondsLeft;
        public bool UnitB;
        public readonly List<string> Cultures = new List<string> { "bloom", "flow" };
        public readonly Dictionary<string, float> GrowerUnits = new Dictionary<string, float>();
        public readonly Dictionary<string, int> Levels = new Dictionary<string, int> { ["siphon"] = 1, ["grower"] = 1, ["incubator"] = 1, ["counter"] = 1 };
        public readonly List<string> Held = new List<string>();
        public readonly List<string> Chamber = new List<string>();
        public bool Incubating;
        public float IncubationLeft;
        public string Pending;
        public GrowthMixEntry LastMix;
        public GrowthSpecimen Hatchling;
        public readonly List<GrowthSpecimen> Carried = new List<GrowthSpecimen>();
        public readonly List<GrowthContainer> Containers = new List<GrowthContainer>();
        public readonly List<string> Discovered = new List<string>();
        public readonly List<GrowthMixEntry> MixLog = new List<GrowthMixEntry>();
        public readonly List<GrowthVisitor> Visitors = new List<GrowthVisitor>();
        public readonly List<GrowthEvent> Events = new List<GrowthEvent>();
        public GrowthDaySummary Today, LastDay;
        public float ServeProgress { get; private set; }
        public float CollectProgress { get; private set; }
        int nextId = 1, visitorsToday;
        float arrivalClock;

        public GrowthShop()
        {
            Credits = GrowthContent.StartingCredits;
            foreach (var c in Cultures) GrowerUnits[c] = GrowerCapacity;
            Containers.Add(new GrowthContainer { Id = "tank-" + nextId++, Kind = "starter", Place = UnitAPlaces[0] });
        }

        // ---------------------------------------------------------------- reading

        public int Stars => GrowthContent.StarsFor(Reputation);
        public int Level(string station) => Levels.TryGetValue(station, out int lv) ? lv : 0;
        public LevelDef LevelDef(string station) => GrowthContent.Station(station).Levels[Level(station) - 1];
        float Stat(string station, string stat) => LevelDef(station).Stat(stat);
        public int SiphonCapacity => (int)Stat("siphon", "capacity");
        public float HoldSeconds => Stat("siphon", "holdSeconds");
        public int GrowerCapacity => (int)Stat("grower", "capacity");
        public float RefillSeconds => Stat("grower", "refillSeconds");
        public int MaxDoses => (int)Stat("incubator", "maxDoses");
        public float IncubationSeconds => Stat("incubator", "seconds");
        public float ServeSeconds => Stat("counter", "serveSeconds");
        public IEnumerable<GrowthContainer> ShopContainers => Containers.Where(c => !c.Def.Lab);
        public IEnumerable<string> OwnedPlaces => UnitB ? UnitAPlaces.Concat(UnitBPlaces) : UnitAPlaces;
        public IEnumerable<string> FreePlaces => OwnedPlaces.Where(p => Containers.All(c => c.Place != p));
        public GrowthContainer Container(string id) => Containers.FirstOrDefault(c => c.Id == id);
        public GrowthContainer ContainerAt(string place) => Containers.FirstOrDefault(c => c.Place == place);
        public int VisitorsAtOnce => GrowthContent.Expansions.Where(e => e.Id == "unit-a" || e.Id == "unit-b" && UnitB).Sum(e => e.VisitorsAtOnce);
        public IEnumerable<GrowthVisitor> Queue => Visitors.Where(v => v.State == GrowthVisitorState.Queued);
        /// <summary>The visitor who queued first (a queued visitor's clock starts when they join the queue).</summary>
        public GrowthVisitor FrontOfQueue => Queue.OrderByDescending(v => v.Clock).ThenBy(v => v.Id).FirstOrDefault();
        public IEnumerable<GrowthSpecimen> Stock => ShopContainers.SelectMany(c => c.Occupants);
        public int Price(string species) => GrowthContent.Get(species).Price;
        public bool InSchool(GrowthSpecimen s) { var c = Containers.FirstOrDefault(x => x.Occupants.Contains(s)); return c != null && c.InSchool(s.Species); }
        public GrowthContainer HolderOf(int specimenId) => Containers.FirstOrDefault(c => c.Occupants.Any(s => s.Id == specimenId));
        public static GrowthPlace Place(string id) => Places.FirstOrDefault(p => p.Id == id);

        /// <summary>GR.5 harmony (era 3): two or more occupants, one temperament, all at home in the container's water.</summary>
        public bool Harmony(GrowthContainer c) =>
            Era >= GrowthContent.ComfortEra && c.Occupants.Count >= 2 && c.Occupants.All(s => s.Def.Water == c.Water) &&
            c.Occupants.Select(s => s.Def.Temperament).Distinct().Count() == 1;

        /// <summary>A container's appeal: +1 for a lit Show tank, +1 for harmony. Visitors look at appealing containers first.</summary>
        public int Appeal(GrowthContainer c) => (c.Kind == "show" ? 1 : 0) + (Harmony(c) ? 1 : 0);

        /// <summary>How far over budget a visitor goes for a species in this container (GR.10.1: school +15%, appeal +10% a point, at most +40%).</summary>
        public float Tolerance(GrowthContainer c, string species) =>
            1f + Math.Min(GrowthContent.ToleranceCap, (c.InSchool(species) ? GrowthContent.SchoolTolerance : 0f) + Appeal(c) * GrowthContent.AppealTolerance);

        /// <summary>Credits paid for a container: its purchase and every level since; selling an empty one returns half.</summary>
        public static int Paid(GrowthContainer c) => c.Def.Levels.Take(c.Level).Sum(l => l.Credits);
        public static int SellValue(GrowthContainer c) => Paid(c) / 2;

        /// <summary>GR.4's era 2 gate: 2 stars, 3 creatures discovered, 2 tanks.</summary>
        public bool EraTwoGateMet => Stars >= 2 && Discovered.Count >= 3 && ShopContainers.Count() >= 2;

        // ---------------------------------------------------------------- the siphon and growers

        /// <summary>Holding the siphon on a grower: one dose every hold time while the grower has culture and the bulb has room.</summary>
        public GrowthResult Collect(string culture, float seconds)
        {
            if (GrowthContent.CultureIndex(culture) < 0) return GrowthResult.Fail(GrowthFailure.UnknownCulture, culture);
            if (!Cultures.Contains(culture)) return GrowthResult.Fail(GrowthFailure.NotOwned, culture);
            if (Held.Count >= SiphonCapacity) { CollectProgress = 0; return GrowthResult.Fail(GrowthFailure.SiphonFull); }
            if (GrowerUnits[culture] < 1) { CollectProgress = 0; return GrowthResult.Fail(GrowthFailure.GrowerEmpty, culture); }
            CollectProgress += seconds;
            int collected = 0;
            while (CollectProgress >= HoldSeconds && Held.Count < SiphonCapacity && GrowerUnits[culture] >= 1)
            {
                CollectProgress -= HoldSeconds;
                GrowerUnits[culture] -= 1;
                Held.Add(culture);
                collected++;
            }
            if (Held.Count >= SiphonCapacity || GrowerUnits[culture] < 1) CollectProgress = 0;
            if (collected > 0) Raise(GrowthEventKind.Collected, collected + " " + Name(culture), amount: collected);
            return GrowthResult.Ok(collected.ToString());
        }

        public void StopCollecting() => CollectProgress = 0;

        // ---------------------------------------------------------------- the incubator

        public GrowthResult AddDose(string culture)
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (!Held.Contains(culture)) return GrowthResult.Fail(GrowthFailure.NoDose, culture);
            if (Chamber.Count >= MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "up to " + MaxDoses + " doses");
            Held.Remove(culture);
            Chamber.Add(culture);
            return GrowthResult.Ok();
        }

        public GrowthResult ReturnDose(string culture)
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (!Chamber.Contains(culture)) return GrowthResult.Fail(GrowthFailure.NoDose, culture);
            if (Held.Count >= SiphonCapacity) return GrowthResult.Fail(GrowthFailure.SiphonFull);
            Chamber.Remove(culture);
            Held.Add(culture);
            return GrowthResult.Ok();
        }

        /// <summary>The mix log's one-click recipe (§6.5 convenience): puts a known founder's doses in from the siphon.</summary>
        public GrowthResult FillRecipe(string species)
        {
            var s = GrowthContent.Get(species);
            if (s == null || s.Kind != SpeciesKind.Founder) return GrowthResult.Fail(GrowthFailure.NotAFounder, species);
            if (!Discovered.Contains(species)) return GrowthResult.Fail(GrowthFailure.NotDiscovered, species);
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (s.Doses > MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "up to " + MaxDoses + " doses");
            // Doses already in the chamber go back first, so the recipe is exact.
            var held = new List<string>(Held); held.AddRange(Chamber);
            for (int i = 0; i < s.Recipe.Length; i++)
            {
                if (s.Recipe[i] == 0) continue;
                string culture = GrowthContent.Cultures[i].Id;
                int have = held.Count(c => c == culture);
                if (have < s.Recipe[i]) return GrowthResult.Fail(GrowthFailure.NoDose, (s.Recipe[i] - have) + " more " + Name(culture));
            }
            Held.Clear(); Held.AddRange(held); Chamber.Clear();
            for (int i = 0; i < s.Recipe.Length; i++)
                for (int n = 0; n < s.Recipe[i]; n++) { Held.Remove(GrowthContent.Cultures[i].Id); Chamber.Add(GrowthContent.Cultures[i].Id); }
            return GrowthResult.Ok();
        }

        public int[] ChamberRecipe()
        {
            var r = new int[GrowthContent.Cultures.Length];
            foreach (var c in Chamber) r[GrowthContent.CultureIndex(c)]++;
            return r;
        }

        /// <summary>What the chamber's mix would make now (never fails while it has doses), for the card's preview of nothing: the result stays a surprise until it hatches.</summary>
        public MixResult Preview() => Chamber.Count == 0 ? new MixResult(null, false, -1) : GrowthRules.Mix(ChamberRecipe(), Cultures, Discovered);

        /// <summary>GR.15.1: an empty mix is refused; the chamber can never hold more than the level allows; the output must be free.</summary>
        public GrowthResult StartIncubation()
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (Hatchling != null) return GrowthResult.Fail(GrowthFailure.HatchlingWaiting);
            if (Chamber.Count == 0) return GrowthResult.Fail(GrowthFailure.ChamberEmpty);
            if (Chamber.Count > MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "up to " + MaxDoses + " doses");
            var recipe = ChamberRecipe();
            var mix = GrowthRules.Mix(recipe, Cultures, Discovered);
            Pending = mix.Species.Id;
            bool isNew = !Discovered.Contains(Pending);
            var entry = MixLog.FirstOrDefault(e => e.Doses.SequenceEqual(recipe));
            if (entry != null) { entry.Count++; entry.WasNew = false; }
            else MixLog.Add(entry = new GrowthMixEntry { Doses = recipe, Species = Pending, Exact = mix.Exact, WasNew = isNew, DistanceToNew = mix.DistanceToNew });
            LastMix = entry;
            Chamber.Clear();
            Incubating = true;
            IncubationLeft = IncubationSeconds;
            return GrowthResult.Ok(Pending);
        }

        void Hatch()
        {
            Incubating = false;
            IncubationLeft = 0;
            Hatchling = new GrowthSpecimen { Id = nextId++, Species = Pending, Colour = Colour.Classic };
            if (!Discovered.Contains(Pending))
            {
                Discovered.Add(Pending);
                var def = GrowthContent.Get(Pending);
                Rp += GrowthContent.RpFor(def.Kind);
                Raise(GrowthEventKind.Discovery, "New creature: " + def.Name + "!", Pending);
                CheckEra();
            }
            else Raise(GrowthEventKind.Hatched, GrowthContent.Get(Pending).Name + " hatched", Pending);
            Pending = null;
        }

        // ---------------------------------------------------------------- carrying and containers

        public GrowthResult TakeHatchling()
        {
            if (Hatchling == null) return GrowthResult.Fail(GrowthFailure.NoHatchling);
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            Carried.Add(Hatchling);
            Hatchling = null;
            return GrowthResult.Ok();
        }

        /// <summary>Puts the first carried creature that fits into the container; a container never accepts one that does not fit (GR.15.1).</summary>
        public GrowthResult PutIn(string containerId, int specimenId = 0)
        {
            var c = Container(containerId);
            if (c == null) return GrowthResult.Fail(GrowthFailure.NoSuchContainer, containerId);
            if (Carried.Count == 0) return GrowthResult.Fail(GrowthFailure.HandsEmpty);
            var candidates = specimenId == 0 ? Carried : Carried.Where(s => s.Id == specimenId).ToList();
            if (specimenId != 0 && candidates.Count == 0) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            var fits = candidates.FirstOrDefault(s => GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(o => o.Species), s.Species));
            if (fits == null)
                return GrowthResult.Fail(GrowthFailure.DoesNotFit, candidates.All(x => (int)x.Def.Size > (int)c.Def.Largest)
                    ? "Too big for a " + c.Def.Name : c.Free + " of " + c.Spaces + " spaces free");
            Carried.Remove(fits);
            c.Occupants.Add(fits);
            return GrowthResult.Ok(fits.Species);
        }

        public GrowthResult TakeOut(string containerId, int specimenId)
        {
            var c = Container(containerId);
            if (c == null) return GrowthResult.Fail(GrowthFailure.NoSuchContainer, containerId);
            var s = c.Occupants.FirstOrDefault(o => o.Id == specimenId);
            if (s == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            if (s.ReservedBy != 0) return GrowthResult.Fail(GrowthFailure.Reserved, "a visitor is buying it");
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            c.Occupants.Remove(s);
            Carried.Add(s);
            return GrowthResult.Ok(s.Species);
        }

        /// <summary>Buys a Starter tank for an empty place in an open unit (layout changes happen at night, §6.1).</summary>
        public GrowthResult BuyTank(string place) => BuyContainer("starter", place);

        /// <summary>Why a container of this kind cannot be bought for the place now (None when it can), with a sentence.</summary>
        public GrowthFailure BuyCheck(string kind, string place, out string why)
        {
            why = null;
            if (!ShopKinds.Contains(kind)) { why = "Not a shop container"; return GrowthFailure.UnknownContainer; }
            var def = GrowthContent.Container(kind);
            var level = def.Levels[0];
            var spot = Place(place);
            if (spot == null || !OwnedPlaces.Contains(place)) { why = "Not an open place"; return GrowthFailure.NoSuchPlace; }
            if (ContainerAt(place) != null) { why = "The place is taken"; return GrowthFailure.PlaceTaken; }
            if (!spot.Takes(def)) { why = def.Name + " is " + Footprint(def) + "; this place takes " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m"; return GrowthFailure.TooBigForPlace; }
            if (level.Era > Era) { why = "Needs era " + level.Era; return GrowthFailure.Locked; }
            if (level.Stars > Stars) { why = "Needs " + level.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Containers are placed at night"; return GrowthFailure.NotAtNight; }
            if (Credits < level.Credits) { why = "Needs " + level.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        public static string Footprint(ContainerDef def) => def.Width.ToString("0.#") + " × " + def.Depth.ToString("0.#") + " m";

        /// <summary>Buys a Starter, Large or Show tank for an empty place it fits (GR.5, GR.11).</summary>
        public GrowthResult BuyContainer(string kind, string place)
        {
            var failure = BuyCheck(kind, place, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var def = GrowthContent.Container(kind);
            int cost = def.Levels[0].Credits;
            Credits -= cost;
            Containers.Add(new GrowthContainer { Id = "tank-" + nextId++, Kind = kind, Place = place });
            Raise(GrowthEventKind.Bought, def.Name + " placed", amount: cost);
            CheckEra();
            return GrowthResult.Ok();
        }

        /// <summary>Why a container cannot go to its next level now (None when it can), with the level and a sentence.</summary>
        public GrowthFailure ContainerUpgradeCheck(string containerId, out LevelDef next, out string why)
        {
            next = null; why = null;
            var c = Container(containerId);
            if (c == null) { why = "No such container"; return GrowthFailure.NoSuchContainer; }
            if (c.Level >= c.Def.Levels.Length) { why = "Highest level"; return GrowthFailure.MaxLevel; }
            next = c.Def.Levels[c.Level];
            if (next.Era > Era) { why = "Needs era " + next.Era; return GrowthFailure.Locked; }
            if (next.Stars > Stars) { why = "Needs " + next.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Fitted at night"; return GrowthFailure.NotAtNight; }
            if (Credits < next.Credits) { why = "Needs " + next.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>A container's next level: more spaces; every creature stays in it (GR.15.1).</summary>
        public GrowthResult UpgradeContainer(string containerId)
        {
            var failure = ContainerUpgradeCheck(containerId, out var next, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var c = Container(containerId);
            Credits -= next.Credits;
            c.Level = next.Level;
            Raise(GrowthEventKind.Upgraded, c.Def.Name + " level " + next.Level + ": " + next.Change, amount: next.Credits);
            return GrowthResult.Ok();
        }

        /// <summary>Why a container cannot move to the place now (None when it can).</summary>
        public GrowthFailure MoveCheck(string containerId, string place, out string why)
        {
            why = null;
            var c = Container(containerId);
            if (c == null) { why = "No such container"; return GrowthFailure.NoSuchContainer; }
            var spot = Place(place);
            if (spot == null || !OwnedPlaces.Contains(place)) { why = "Not an open place"; return GrowthFailure.NoSuchPlace; }
            if (ContainerAt(place) != null) { why = "The place is taken"; return GrowthFailure.PlaceTaken; }
            if (!spot.Takes(c.Def)) { why = c.Def.Name + " is " + Footprint(c.Def) + "; this place takes " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m"; return GrowthFailure.TooBigForPlace; }
            if (Phase != GrowthPhase.Night) { why = "Containers are moved at night"; return GrowthFailure.NotAtNight; }
            if (c.Occupants.Any(s => s.ReservedBy != 0)) { why = "A visitor is buying from it"; return GrowthFailure.Reserved; }
            return GrowthFailure.None;
        }

        /// <summary>Moves a container with everything in it to another free place in one step (GR.15.1, §6.4).</summary>
        public GrowthResult MoveContainer(string containerId, string place)
        {
            var failure = MoveCheck(containerId, place, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var c = Container(containerId);
            c.Place = place;
            Raise(GrowthEventKind.Moved, c.Def.Name + " moved");
            return GrowthResult.Ok();
        }

        /// <summary>Why a container cannot be sold now (None when it can): only empty ones, never the last tank in the shop.</summary>
        public GrowthFailure SellCheck(string containerId, out string why)
        {
            why = null;
            var c = Container(containerId);
            if (c == null) { why = "No such container"; return GrowthFailure.NoSuchContainer; }
            if (c.Occupants.Count > 0) { why = "Take its creatures out first"; return GrowthFailure.NotEmpty; }
            if (!c.Def.Lab && ShopContainers.Count() <= 1) { why = "The shop keeps at least one tank"; return GrowthFailure.LastContainer; }
            if (Phase != GrowthPhase.Night) { why = "Containers are sold at night"; return GrowthFailure.NotAtNight; }
            return GrowthFailure.None;
        }

        /// <summary>Sells an empty container back for half what it cost (GR.19.4: a full floor must never block a bigger tank).</summary>
        public GrowthResult SellContainer(string containerId)
        {
            var failure = SellCheck(containerId, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var c = Container(containerId);
            int value = SellValue(c);
            Containers.Remove(c);
            Credits += value;
            Raise(GrowthEventKind.Sold, c.Def.Name + " sold for " + value + " credits", amount: value);
            return GrowthResult.Ok(value.ToString());
        }

        /// <summary>Why a container's water cannot be set now (None when it can).</summary>
        public GrowthFailure WaterCheck(string containerId, Water water, out string why)
        {
            why = null;
            var c = Container(containerId);
            if (c == null) { why = "No such container"; return GrowthFailure.NoSuchContainer; }
            if (c.Water == water) { why = "Already " + water.ToString().ToLowerInvariant(); return GrowthFailure.SameWater; }
            if (Era < GrowthContent.ComfortEra) { why = "Needs era " + GrowthContent.ComfortEra; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Water is changed at night"; return GrowthFailure.NotAtNight; }
            if (Credits < GrowthContent.WaterKitCredits) { why = "Needs " + GrowthContent.WaterKitCredits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>A water kit sets a container's water (GR.5 comfort); its creatures stay, whatever their water.</summary>
        public GrowthResult SetWater(string containerId, Water water)
        {
            var failure = WaterCheck(containerId, water, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var c = Container(containerId);
            Credits -= GrowthContent.WaterKitCredits;
            c.Water = water;
            Raise(GrowthEventKind.Upgraded, c.Def.Name + ": " + water.ToString().ToLowerInvariant() + " water", amount: GrowthContent.WaterKitCredits);
            return GrowthResult.Ok();
        }

        /// <summary>
        /// Development and validation only: a creature of any species straight into the hands, as if hatched elsewhere. It
        /// is not a discovery. The greybox's development crate and the routes' labelled fixtures use it.
        /// </summary>
        public GrowthResult DevelopmentGive(string species)
        {
            if (GrowthContent.Get(species) == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen, species);
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            Carried.Add(new GrowthSpecimen { Id = nextId++, Species = species });
            return GrowthResult.Ok(species);
        }

        /// <summary>Development and validation only: a new specimen straight into a container if it fits (no discovery).</summary>
        public GrowthResult DevelopmentStock(string containerId, string species)
        {
            var c = Container(containerId);
            if (c == null) return GrowthResult.Fail(GrowthFailure.NoSuchContainer, containerId);
            if (!GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(o => o.Species), species)) return GrowthResult.Fail(GrowthFailure.DoesNotFit, species);
            c.Occupants.Add(new GrowthSpecimen { Id = nextId++, Species = species });
            return GrowthResult.Ok(species);
        }

        /// <summary>
        /// GR-03's densest layout (validation): unit B open, era 4 and 4 stars, every place holding the largest container it
        /// takes at its highest level, filled to the last space with small creatures of 21 species, three of each in turn (schools).
        /// </summary>
        public static GrowthShop Densest(int stars = 4)
        {
            var shop = new GrowthShop { Era = 4, UnitB = true, Credits = 0, Reputation = GrowthContent.StarThresholds[Math.Max(0, stars - 1)] };
            shop.Containers.Clear();
            var small = GrowthContent.Species.Where(s => s.Size == SizeClass.Small && s.Kind != SpeciesKind.Crown).Select(s => s.Id).ToList();
            int k = 0;
            foreach (var place in Places)
            {
                var def = GrowthContent.Containers.Where(d => !d.Lab && place.Takes(d)).OrderByDescending(d => d.Levels.Last().Stat("spaces")).First();
                var c = new GrowthContainer { Id = "tank-" + shop.nextId++, Kind = def.Id, Place = place.Id, Level = def.Levels.Length };
                shop.Containers.Add(c);
                while (c.Free > 0)
                {
                    string species = small[(k++ / 3) % small.Count];
                    c.Occupants.Add(new GrowthSpecimen { Id = shop.nextId++, Species = species });
                }
            }
            return shop;
        }

        // ---------------------------------------------------------------- upgrades and spaces

        /// <summary>Why the next level of a station cannot be bought now (None when it can), with the level and a sentence.</summary>
        public GrowthFailure UpgradeCheck(string station, out LevelDef next, out string why)
        {
            next = null; why = null;
            if (!GreyboxStations.Contains(station)) { why = "Not in the greybox"; return GrowthFailure.MaxLevel; }
            int lv = Level(station);
            var levels = GrowthContent.Station(station).Levels;
            if (lv >= Math.Min(levels.Length, GreyboxMaxLevel)) { why = "Highest level in the greybox"; return GrowthFailure.MaxLevel; }
            next = levels[lv];
            if (next.Era > Era) { why = "Needs era " + next.Era; return GrowthFailure.Locked; }
            if (next.Stars > Stars) { why = "Needs " + next.Stars + " stars"; return GrowthFailure.Locked; }
            if (next.Rp > Rp) { why = "Needs " + next.Rp + " research points"; return GrowthFailure.Locked; }
            // GR.15.1: an upgrade installs only when its station is idle.
            if (station == "incubator" && (Incubating || Hatchling != null)) { why = "The incubator is busy"; return GrowthFailure.Busy; }
            if (station == "counter" && Queue.Any()) { why = "Visitors are waiting"; return GrowthFailure.Busy; }
            if (Credits < next.Credits) { why = "Needs " + next.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        public GrowthResult Upgrade(string station)
        {
            var failure = UpgradeCheck(station, out var next, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= next.Credits;
            Rp -= next.Rp;
            Levels[station] = next.Level;
            Raise(GrowthEventKind.Upgraded, GrowthContent.Station(station).Name + " level " + next.Level + ": " + next.Change, amount: next.Credits);
            CheckEra();
            return GrowthResult.Ok();
        }

        public static ExpansionDef UnitBDef => GrowthContent.Expansions.First(e => e.Id == "unit-b");

        /// <summary>Why shop unit B cannot open now (None when it can), with a sentence.</summary>
        public GrowthFailure UnitBCheck(out string why)
        {
            var unit = UnitBDef;
            why = null;
            if (UnitB) { why = "Already open"; return GrowthFailure.MaxLevel; }
            if (Era < unit.Era) { why = "Needs era " + unit.Era + ": 2 stars, 3 creatures discovered and 2 tanks"; return GrowthFailure.Locked; }
            if (Stars < unit.Stars) { why = "Needs " + unit.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Walls open at night"; return GrowthFailure.NotAtNight; }
            if (Credits < unit.Credits) { why = "Needs " + unit.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Opens the wall into shop unit B (GR.11): four more container places and one more visitor at once.</summary>
        public GrowthResult BuyUnitB()
        {
            var failure = UnitBCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var unit = UnitBDef;
            Credits -= unit.Credits;
            UnitB = true;
            Raise(GrowthEventKind.Bought, unit.Name + " opened", amount: unit.Credits);
            return GrowthResult.Ok();
        }

        void CheckEra()
        {
            if (Era == 1 && EraTwoGateMet)
            {
                Era = 2;
                Raise(GrowthEventKind.EraUp, "Era 2: " + GrowthContent.Eras[1].Name + ". Shop unit B can open.");
            }
        }

        // ---------------------------------------------------------------- the day

        public GrowthResult OpenShop()
        {
            if (Phase != GrowthPhase.Night) return GrowthResult.Fail(GrowthFailure.NotAtNight);
            Phase = GrowthPhase.Day;
            DaySecondsLeft = GrowthContent.DaySeconds;
            arrivalClock = GrowthContent.ArrivalSeconds[Stars - 1] - 2f;
            visitorsToday = 0;
            Today = new GrowthDaySummary { Day = Day, StarsBefore = Stars };
            Raise(GrowthEventKind.DayStarted, "Day " + Day + ": the shop is open");
            return GrowthResult.Ok();
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0) return;
            foreach (var c in Cultures)
                GrowerUnits[c] = Math.Min(GrowerCapacity, GrowerUnits[c] + seconds / RefillSeconds);
            if (Incubating)
            {
                IncubationLeft -= seconds;
                if (IncubationLeft <= 0) Hatch();
            }
            if (Phase == GrowthPhase.Day) TickDay(seconds);
            // Visitors who left are gone once they have walked out (the presentation walks them in that time).
            foreach (var v in Visitors) if (v.State == GrowthVisitorState.Leaving) v.Clock += Phase == GrowthPhase.Day ? 0 : seconds;
            Visitors.RemoveAll(v => v.State == GrowthVisitorState.Leaving && v.Clock >= LeaveSeconds);
        }

        void TickDay(float seconds)
        {
            DaySecondsLeft -= seconds;
            arrivalClock += seconds;
            int star = Stars - 1;
            if (DaySecondsLeft > BrowseSeconds && arrivalClock >= GrowthContent.ArrivalSeconds[star] && Visitors.Count < VisitorsAtOnce)
            {
                arrivalClock = 0;
                Arrive();
            }
            foreach (var v in Visitors.ToList())
            {
                v.Clock += seconds;
                if (v.State == GrowthVisitorState.Browsing && v.Clock >= v.BrowseFor) Decide(v);
                else if (v.State == GrowthVisitorState.Queued && (v.Patience -= seconds) <= 0) Leave(v, GrowthVisitOutcome.TiredOfWaiting);
            }
            if (DaySecondsLeft <= 0) CloseShop();
        }

        /// <summary>Visitors by GrowthContent's weights at today's stars, in a fixed sequence (no luck in what the player is owed).</summary>
        void Arrive()
        {
            // An empty shop window draws nobody in: passers-by walk on, and no reputation is lost for it.
            if (!Stock.Any()) { if (Today != null) Today.WalkedPast++; return; }
            int star = Stars - 1;
            var types = GrowthContent.Visitors.Where(t => t.Weights[star] > 0).ToList();
            int total = types.Sum(t => t.Weights[star]);
            int roll = (int)(Hash(Day * 7919 + visitorsToday * 104729) % (uint)total);
            var type = types[0];
            foreach (var t in types) { if (roll < t.Weights[star]) { type = t; break; } roll -= t.Weights[star]; }
            var v = new GrowthVisitor { Id = nextId++, Type = type.Id, Budget = type.Budgets[star], State = GrowthVisitorState.Browsing };
            // GR-03: the visitor looks into the stocked containers one at a time, the most appealing first, then in an
            // order of their own; at most three.
            v.Plan.AddRange(ShopContainers.Where(c => c.Occupants.Count > 0)
                .OrderByDescending(Appeal).ThenBy(c => Hash(v.Id * 977 + StableHash(c.Id))).Take(MostBrowsed).Select(c => c.Id));
            v.ContainerId = v.Plan.Count > 0 ? v.Plan[0] : null;
            visitorsToday++;
            if (Today != null) Today.Visitors++;
            Visitors.Add(v);
        }

        /// <summary>
        /// A visitor picks, from the container they are looking into, the creature they like best within budget: the dearer
        /// the better, a school's species and an appealing container stretch the budget (GR.10.1), Big spenders favour bigger
        /// creatures, and a small fixed variety per visitor. Collectors from 3 stars take a pair of one species when they can
        /// (reserved together, all or none). Nothing here: on to the next container in their plan, or they leave.
        /// </summary>
        void Decide(GrowthVisitor v)
        {
            var c = v.ContainerId == null ? null : Container(v.ContainerId);
            var chosen = c == null ? null : PickFrom(v, c);
            if (chosen == null || chosen.Count == 0)
            {
                v.Browsed++;
                if (v.Browsed < v.Plan.Count)
                {
                    v.ContainerId = v.Plan[v.Browsed];
                    v.BrowseFor = NextBrowseSeconds;
                    v.Clock = 0;
                    return;
                }
                Leave(v, GrowthVisitOutcome.NothingAffordable);
                return;
            }
            if (!ReserveAll(v, chosen)) { Leave(v, GrowthVisitOutcome.NothingAffordable); return; }
            v.State = GrowthVisitorState.Queued;
            v.Patience = QueuePatience;
            v.Clock = 0;
        }

        /// <summary>What a visitor would take from one container: a pair (Collectors, 3 stars), one specimen, or nothing.</summary>
        public List<GrowthSpecimen> PickFrom(GrowthVisitor v, GrowthContainer c)
        {
            var free = c.Occupants.Where(s => s.ReservedBy == 0).ToList();
            if (v.Type == "collector" && Stars >= 3)
            {
                var pair = free.GroupBy(s => s.Species).Where(g => g.Count() >= 2 && 2 * Price(g.Key) <= v.Budget * Tolerance(c, g.Key))
                    .OrderByDescending(g => Price(g.Key)).ThenBy(g => g.Key).FirstOrDefault();
                if (pair != null) return pair.Take(2).ToList();
            }
            GrowthSpecimen best = null; double bestScore = double.MinValue;
            foreach (var s in free)
            {
                int price = Price(s.Species);
                if (price > v.Budget * Tolerance(c, s.Species)) continue;
                // Big spenders weigh a medium creature 1.5 times and a large one 2.5 times its price (GR.10).
                double weight = v.Type == "premium" ? 1 + BigSpenderSizeWeight * ((int)s.Def.Size - 1) : 1;
                double score = price * weight + (c.InSchool(s.Species) ? 3 : 0) + Hash(v.Id * 131 + s.Id) % 3;
                if (score > bestScore) { best = s; bestScore = score; }
            }
            return best == null ? null : new List<GrowthSpecimen> { best };
        }

        /// <summary>Reserves every specimen for the visitor or none of them (GR.15.1: a pair or a school is all or none).</summary>
        public bool ReserveAll(GrowthVisitor v, IList<GrowthSpecimen> specimens)
        {
            if (specimens.Count == 0 || specimens.Any(s => s.ReservedBy != 0) || specimens.Select(s => s.Id).Distinct().Count() != specimens.Count) return false;
            var holder = HolderOf(specimens[0].Id);
            if (holder == null || specimens.Any(s => HolderOf(s.Id) != holder)) return false;
            foreach (var s in specimens) s.ReservedBy = v.Id;
            v.SpecimenIds.Clear();
            v.SpecimenIds.AddRange(specimens.Select(s => s.Id));
            v.ContainerId = holder.Id;
            return true;
        }

        void Leave(GrowthVisitor v, GrowthVisitOutcome outcome)
        {
            foreach (int id in v.SpecimenIds)
            {
                var s = Stock.FirstOrDefault(x => x.Id == id);
                if (s != null && s.ReservedBy == v.Id) s.ReservedBy = 0;
            }
            if (outcome != GrowthVisitOutcome.Bought) v.SpecimenIds.Clear();
            v.State = GrowthVisitorState.Leaving;
            v.Outcome = outcome;
            v.Clock = 0;
            int before = Reputation;
            if (outcome == GrowthVisitOutcome.NothingAffordable)
            {
                Reputation = Math.Max(0, Reputation + GrowthContent.RepPerRefusal);
                RefusalsTotal++;
                if (Today != null) Today.Refusals++;
                Raise(GrowthEventKind.Refusal, "A visitor found nothing to buy");
            }
            else if (outcome == GrowthVisitOutcome.TiredOfWaiting)
            {
                Reputation = Math.Max(0, Reputation + RepPerImpatient);
                if (Today != null) Today.Impatient++;
                Raise(GrowthEventKind.Impatient, "A visitor got tired of waiting");
            }
            if (Today != null) Today.ReputationGained += Reputation - before;
        }

        /// <summary>Holding the mouse on the counter bags the front visitor's creature (GR.9 counter levels).</summary>
        public GrowthResult Serve(float seconds)
        {
            if (Phase != GrowthPhase.Day) return GrowthResult.Fail(GrowthFailure.NotDuringDay);
            var v = FrontOfQueue;
            if (v == null) { ServeProgress = 0; return GrowthResult.Fail(GrowthFailure.NoVisitorWaiting); }
            ServeProgress += seconds;
            if (ServeProgress < ServeSeconds) return GrowthResult.Ok("bagging");
            ServeProgress = 0;
            var bought = v.SpecimenIds.Select(id => HolderOf(id)?.Occupants.First(o => o.Id == id)).Where(s => s != null).ToList();
            if (bought.Count == 0) { Leave(v, GrowthVisitOutcome.TiredOfWaiting); return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen); }
            int price = 0, starsBefore = Stars;
            foreach (var s in bought)
            {
                HolderOf(s.Id).Occupants.Remove(s);
                price += Price(s.Species);
                Reputation = Math.Min(GrowthContent.ReputationMax, Reputation + GrowthContent.RepPerSale);
                SalesTotal++;
                if (Today != null) { Today.Sales++; Today.ReputationGained += GrowthContent.RepPerSale; }
            }
            Credits += price;
            if (Today != null) Today.Revenue += price;
            v.State = GrowthVisitorState.Leaving;
            v.Outcome = GrowthVisitOutcome.Bought;
            v.Clock = 0;
            var first = bought[0];
            Raise(GrowthEventKind.Sale, "Sold " + (bought.Count > 1 ? "a pair of " + first.Def.Name + "s" : first.Def.Name) + " for " + price + " credits", first.Species, price);
            if (Stars > starsBefore) Raise(GrowthEventKind.StarUp, "The shop has " + Stars + " stars!", amount: Stars);
            CheckEra();
            return GrowthResult.Ok("sold");
        }

        public void StopServing() => ServeProgress = 0;

        void CloseShop()
        {
            foreach (var v in Visitors.Where(x => x.State != GrowthVisitorState.Leaving).ToList()) Leave(v, GrowthVisitOutcome.Closed);
            Phase = GrowthPhase.Night;
            DaySecondsLeft = 0;
            ServeProgress = 0;
            if (Today != null) { Today.StarsAfter = Stars; LastDay = Today; }
            Today = null;
            Raise(GrowthEventKind.DayEnded, "Day " + Day + " is over");
            Day++;
            CheckEra();
        }

        // ---------------------------------------------------------------- the objective board (GR.12)

        public string EraGoal => GrowthContent.Eras[Era - 1].Goal;

        /// <summary>The goal and its progress lines, as the objective board and the HUD show them.</summary>
        public List<(string text, bool done)> Objectives()
        {
            var list = new List<(string, bool)>();
            if (Era == 1)
            {
                list.Add(("Discover 3 creatures (" + Math.Min(3, Discovered.Count) + "/3)", Discovered.Count >= 3));
                list.Add(("Own 2 tanks (" + Math.Min(2, ShopContainers.Count()) + "/2)", ShopContainers.Count() >= 2));
                list.Add(("Reach 2 stars (reputation " + Reputation + "/" + GrowthContent.StarThresholds[1] + ")", Stars >= 2));
            }
            else if (!UnitB)
            {
                var unit = GrowthContent.Expansions.First(e => e.Id == "unit-b");
                list.Add(("Open shop unit B (" + Math.Min(Credits, unit.Credits) + "/" + unit.Credits + " credits, at night)", false));
            }
            else
            {
                bool large = ShopContainers.Any(c => c.Kind != "starter");
                list.Add(("Buy a Large tank for a long place (" + GrowthContent.Container("large").Levels[0].Credits + " credits, at night)", large));
                list.Add(("The greybox ends here: breeding and the rest of era 2 come with GR-05", true));
            }
            return list;
        }

        // ---------------------------------------------------------------- events and helpers

        void Raise(GrowthEventKind kind, string text, string species = null, int amount = 0) =>
            Events.Add(new GrowthEvent { Kind = kind, Text = text, Species = species, Amount = amount });

        public List<GrowthEvent> TakeEvents() { var e = new List<GrowthEvent>(Events); Events.Clear(); return e; }

        public static string Name(string culture) => GrowthContent.Cultures[GrowthContent.CultureIndex(culture)].Name;

        /// <summary>A string hash that is the same on every runtime (string.GetHashCode is not).</summary>
        static int StableHash(string s) { unchecked { int h = 17; foreach (char ch in s) h = h * 31 + ch; return h; } }

        static uint Hash(int x)
        {
            unchecked
            {
                uint h = (uint)x * 2654435761u;
                h ^= h >> 16; h *= 2246822519u; h ^= h >> 13;
                return h;
            }
        }

        // ---------------------------------------------------------------- saving (a fresh save line, GR.15)

        public GrowthSave Snapshot() => new GrowthSave
        {
            credits = Credits, reputation = Reputation, rp = Rp, era = Era, day = Day, sales = SalesTotal, refusals = RefusalsTotal, nextId = nextId,
            phase = Phase.ToString(), daySecondsLeft = DaySecondsLeft, unitB = UnitB,
            cultures = Cultures.ToArray(), growerUnits = Cultures.Select(c => GrowerUnits[c]).ToArray(),
            stations = Levels.Keys.ToArray(), levels = Levels.Values.ToArray(),
            held = Held.ToArray(), chamber = Chamber.ToArray(), incubating = Incubating, incubationLeft = IncubationLeft, pending = Pending ?? "",
            hatchling = Hatchling == null ? new GrowthSave.Specimen[0] : new[] { GrowthSave.Specimen.Of(Hatchling) },
            carried = Carried.Select(GrowthSave.Specimen.Of).ToArray(),
            containers = Containers.Select(c => new GrowthSave.Box { id = c.Id, kind = c.Kind, place = c.Place, level = c.Level, water = c.Water.ToString(), occupants = c.Occupants.Select(GrowthSave.Specimen.Of).ToArray() }).ToArray(),
            discovered = Discovered.ToArray(),
            mixLog = MixLog.Select(m => new GrowthSave.Mix { doses = m.Doses, species = m.Species, exact = m.Exact, wasNew = m.WasNew, distanceToNew = m.DistanceToNew, count = m.Count }).ToArray(),
        };

        /// <summary>Restores a saved shop; visitors and reservations are not saved (a day resumes empty). Null and a reason when the save is not one.</summary>
        public static GrowthShop Restore(GrowthSave s, out string problem)
        {
            problem = null;
            if (s == null) { problem = "No save"; return null; }
            // Version 1 (GR-02) had no water; its containers keep fresh water.
            if (s.version < 1 || s.version > GrowthSave.CurrentVersion) { problem = "Unknown GR save version " + s.version; return null; }
            bool Known(string species) => GrowthContent.Get(species) != null;
            var all = (s.hatchling ?? new GrowthSave.Specimen[0]).Concat(s.carried ?? new GrowthSave.Specimen[0]).Concat((s.containers ?? new GrowthSave.Box[0]).SelectMany(b => b.occupants ?? new GrowthSave.Specimen[0])).ToList();
            if (all.Any(x => !Known(x.species))) { problem = "Unknown species in the save"; return null; }
            if (all.Select(x => x.id).Distinct().Count() != all.Count) { problem = "A creature is saved twice"; return null; }
            if ((s.discovered ?? new string[0]).Any(d => !Known(d))) { problem = "Unknown discovery in the save"; return null; }
            if (!Enum.TryParse<GrowthPhase>(s.phase, out var phase)) { problem = "Unknown phase " + s.phase; return null; }
            var shop = new GrowthShop();
            shop.Containers.Clear(); shop.Cultures.Clear(); shop.GrowerUnits.Clear();
            shop.Credits = s.credits; shop.Reputation = s.reputation; shop.Rp = s.rp; shop.Era = Math.Max(1, s.era); shop.Day = Math.Max(1, s.day);
            shop.SalesTotal = s.sales; shop.RefusalsTotal = s.refusals; shop.Phase = phase; shop.DaySecondsLeft = s.daySecondsLeft; shop.UnitB = s.unitB;
            for (int i = 0; i < (s.cultures ?? new string[0]).Length; i++)
            {
                if (GrowthContent.CultureIndex(s.cultures[i]) < 0) { problem = "Unknown culture " + s.cultures[i]; return null; }
                shop.Cultures.Add(s.cultures[i]); shop.GrowerUnits[s.cultures[i]] = s.growerUnits != null && i < s.growerUnits.Length ? s.growerUnits[i] : 0;
            }
            for (int i = 0; i < (s.stations ?? new string[0]).Length; i++)
            {
                if (!shop.Levels.ContainsKey(s.stations[i]) || s.levels[i] < 1 || s.levels[i] > GreyboxMaxLevel) { problem = "Bad station level " + s.stations[i]; return null; }
                shop.Levels[s.stations[i]] = s.levels[i];
            }
            shop.Held.AddRange(s.held ?? new string[0]); shop.Chamber.AddRange(s.chamber ?? new string[0]);
            if (shop.Held.Concat(shop.Chamber).Any(c => !shop.Cultures.Contains(c))) { problem = "A dose of a culture not owned"; return null; }
            shop.Incubating = s.incubating; shop.IncubationLeft = s.incubationLeft; shop.Pending = string.IsNullOrEmpty(s.pending) ? null : s.pending;
            if (shop.Incubating && (shop.Pending == null || !Known(shop.Pending))) { problem = "An incubation without its creature"; return null; }
            shop.Hatchling = s.hatchling != null && s.hatchling.Length > 0 ? s.hatchling[0].ToSpecimen() : null;
            shop.Carried.AddRange((s.carried ?? new GrowthSave.Specimen[0]).Select(x => x.ToSpecimen()));
            foreach (var b in s.containers ?? new GrowthSave.Box[0])
            {
                var c = new GrowthContainer { Id = b.id, Kind = b.kind, Place = b.place, Level = Math.Max(1, b.level) };
                try { if (c.Level > c.Def.Levels.Length) throw new ArgumentException(); } catch (ArgumentException) { problem = "Unknown container " + b.kind; return null; }
                if (!string.IsNullOrEmpty(b.water)) { if (!Enum.TryParse<Water>(b.water, out var w)) { problem = "Unknown water " + b.water; return null; } c.Water = w; }
                if (!c.Def.Lab && Place(c.Place) == null) { problem = "Unknown place " + c.Place; return null; }
                if (shop.Containers.Any(x => x.Place == c.Place) || shop.Containers.Any(x => x.Id == c.Id)) { problem = "Two containers in one place"; return null; }
                c.Occupants.AddRange((b.occupants ?? new GrowthSave.Specimen[0]).Select(x => x.ToSpecimen()));
                if (c.Used > c.Spaces) { problem = "A container holds more than it fits"; return null; }
                shop.Containers.Add(c);
            }
            shop.Discovered.AddRange(s.discovered ?? new string[0]);
            shop.MixLog.AddRange((s.mixLog ?? new GrowthSave.Mix[0]).Select(m => new GrowthMixEntry { Doses = m.doses, Species = m.species, Exact = m.exact, WasNew = m.wasNew, DistanceToNew = m.distanceToNew, Count = Math.Max(1, m.count) }));
            shop.nextId = Math.Max(s.nextId, all.Count == 0 ? 1 : all.Max(x => x.id) + 1);
            if (phase == GrowthPhase.Day) { shop.Today = new GrowthDaySummary { Day = shop.Day, StarsBefore = shop.Stars }; shop.arrivalClock = 0; }
            shop.CheckEra();
            shop.Events.Clear();
            return shop;
        }
    }

    /// <summary>The GR save line (GR.15): its own file and version; a classic save is never read or written by it.</summary>
    [Serializable]
    public sealed class GrowthSave
    {
        /// <summary>2: GR-03 (container water). Version 1 saves load with fresh water.</summary>
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public int credits, reputation, rp, era, day, sales, refusals, nextId;
        public string phase;
        public float daySecondsLeft;
        public bool unitB;
        public string[] cultures;
        public float[] growerUnits;
        public string[] stations;
        public int[] levels;
        public string[] held, chamber;
        public bool incubating;
        public float incubationLeft;
        public string pending;
        public Specimen[] hatchling, carried;
        public Box[] containers;
        public string[] discovered;
        public Mix[] mixLog;
        /// <summary>Comfort settings kept with the slot: peek on or off, reduced motion.</summary>
        public bool peekOff, reducedMotion;
        public float sensitivity = 1;

        [Serializable] public sealed class Specimen
        {
            public int id;
            public string species, colour;
            public static Specimen Of(GrowthSpecimen s) => new Specimen { id = s.Id, species = s.Species, colour = s.Colour.ToString() };
            public GrowthSpecimen ToSpecimen() => new GrowthSpecimen { Id = id, Species = species, Colour = Enum.TryParse<Colour>(colour, out var c) ? c : Colour.Classic };
        }

        [Serializable] public sealed class Box
        {
            public string id, kind, place, water;
            public int level;
            public Specimen[] occupants;
        }

        [Serializable] public sealed class Mix
        {
            public int[] doses;
            public string species;
            public bool exact, wasNew;
            public int distanceToNew, count;
        }
    }
}
