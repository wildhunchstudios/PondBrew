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
    // GR-04 adds GR.6's cultures: each of the seven from its source (the two free growers; Ember and Brine growers and the
    // Mist vat ordered from the supplier catalog at night and delivered at the next opening; Spark and Dusk researched with
    // research points), starter vials to try a culture before owning it, and mixing with whatever culture is in the chamber.
    // GR-05 adds GR.7.2's breeding: the breeding tank in the back room (era 2, bought with credits and research points), any
    // pair making the offspring GrowthRules.Breed gives into its nursery while the parents stay, colour set by a tinted dose
    // or the higher parent colour, the pair log, a journal of species in colours (288 entries) with research points for
    // every first species and colour, and the era 3 gate (3 stars, a hybrid bred, a Large tank).
    // GR-06 adds GR.9's station levels past 2: the siphon's twin chamber and vacuum, growers to level 4, the vat's double
    // batch and faster Mist, the incubator's clutches and tint port, the breeding tank's second pair, the counter's card
    // reader and queue of 3, and the research bench (its microscope); a level whose capability belongs to a later task
    // (power, the line, helpers) says what it waits for.
    // GR-07 adds GR.8's power and pipes (GrowthPower.cs): the lab annex, the generator, pumps, buffers, pipes, the power
    // board and the auto-doser, and the era 4 gate (4 stars, powered pumps running, 12 species).
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
        TooBigForPlace, NotEmpty, LastContainer, SameWater, UnknownContainer, Owned, Ordered, NotInCatalog, NoOffspring, NotTinted,
        NoRoute, NotEnough, NoSuchVisitor,
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
        /// <summary>GR-08: the sorter put it in a shop tank (the hatchery line stocked it): its sale counts toward a full day on the line.</summary>
        public bool FromLine;
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
        /// <summary>GR-19: the piece of decor on its top (null for none; shop tanks only).</summary>
        public string Decor;
        /// <summary>GR-31 (F37): what Nori and the sorter may put here: "" anything, a water ("fresh", "warm", "brine"), a temperament ("calm", "lively") or "one kind".</summary>
        public string Rule = "";
        public readonly List<GrowthSpecimen> Occupants = new List<GrowthSpecimen>();
        public ContainerDef Def => GrowthContent.Container(Kind);
        public int Spaces => (int)Def.Levels[Level - 1].Stat("spaces");
        public int Used => Occupants.Sum(s => (int)s.Def.Size);
        public int Free => Spaces - Used;
        /// <summary>GR.5: three or more of one species in one container swim together as a school.</summary>
        public bool InSchool(string species) => Occupants.Count(s => s.Species == species) >= GrowthShop.SchoolSize;
    }

    public enum GrowthVisitorState { Browsing, Queued, Leaving }
    /// <summary>How a visit ended; GR-18 wave 4: Slapped, a slapped visitor who left in a huff (§6.7).</summary>
    public enum GrowthVisitOutcome { None, Bought, NothingAffordable, TiredOfWaiting, Closed, Slapped }

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
        /// <summary>GR-11: an exhibition guest's species and which of the six purchases they came for (-1 for other visitors).</summary>
        public string Wants;
        public int Order = -1;
        /// <summary>GR-18 wave 4: how many times the player slapped this visitor (§6.7; a casual visitor laughs off the first).</summary>
        public int Slaps;
        /// <summary>GR-31: how many times they were booped, and (F30) what they bought, for the icon over their bag.</summary>
        public int Boops;
        public string BoughtSpecies;
        public Colour BoughtColour;
        /// <summary>GR-20: what an ordinary visitor wishes for (null for none, and for exhibition guests), and whether it was
        /// granted by a sale or missed (the visit ended without it).</summary>
        public GrowthWish Wish;
        public bool WishGranted, WishMissed;
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

    /// <summary>GR-05: a pair tried in the breeding tank and what it makes (the journal's pair log, GR.7.2).</summary>
    public sealed class GrowthPairEntry
    {
        /// <summary>The parents' species in id order (a pair is the same pair either way round) and their offspring's.</summary>
        public string A, B, Species;
        public bool WasNew;
        public int Count = 1;
    }

    /// <summary>GR-05, GR-06: one pair's place in the breeding tank: its parents, its offspring in the nursery and its timer.</summary>
    public sealed class GrowthBreedingPair
    {
        public readonly List<GrowthSpecimen> Parents = new List<GrowthSpecimen>();
        public GrowthSpecimen Offspring;
        public float Left;
        /// <summary>Whether the offspring in the nursery was a new journal entry (a new species or colour): the card says so.</summary>
        public bool OffspringWasNew;
        /// <summary>A pair is in and the nursery is free: the timer runs (GR.15.1: a result that has nowhere to go waits).</summary>
        public bool Breeding => Parents.Count >= GrowthShop.PairSize && Offspring == null;
    }

    public enum GrowthEventKind { Collected, Hatched, Discovery, Sale, Refusal, Impatient, StarUp, EraUp, DayStarted, DayEnded, Bought, Upgraded, Moved, Sold, Ordered, Delivered, CultureOwned, Bred, NewColour, Pumped, Laid, AutoDosed, Sorted, LineDay, Hired, Wages, StaffHome, Opened, Exhibition, ExhibitionWon, ExhibitionFailed, Guide, Slapped, Decorated, WishGranted, Lesson }

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
        /// <summary>GR-18 wave 4 (§6.7): visitors slapped who left in a huff, and casual visitors who laughed one off.</summary>
        public int Slapped, LaughedOff;
        /// <summary>GR-11: the Grand Exhibition's day: its guests, those served, and whether it was held or why it fell short.</summary>
        public int Guests, GuestsServed;
        public bool Exhibition, ExhibitionWon;
        public readonly List<string> ExhibitionNotes = new List<string>();
        /// <summary>GR-22: the player closed the shop before its time, with this much of the day left.</summary>
        public bool ClosedEarly;
        public float SecondsLeft;
        /// <summary>GR-20: visitors who came in with a wish, those granted, the tips (counted in Revenue too), and what was
        /// wished for and not granted, with how many wished for it.</summary>
        public int Wishes, WishesGranted, Tips;
        public readonly Dictionary<string, int> WishesMissed = new Dictionary<string, int>();
    }

    public sealed partial class GrowthShop
    {
        public const int CarryLimit = 3, SchoolSize = 3, RepPerImpatient = -1;
        /// <summary>A visit: walking in and browsing, waiting in the queue, walking out. A visitor counts toward the shop's
        /// "visitors at once" until they have walked out, so a small shop serves a few people at a time (GR.10.1).</summary>
        public const float BrowseSeconds = 6f, QueuePatience = 40f, LeaveSeconds = 8f, SchoolBudgetBonus = 1f + GrowthContent.SchoolTolerance;
        /// <summary>GR-03: a visitor looks into at most three containers, about 5 s for each after the first.</summary>
        public const int MostBrowsed = 3;
        public const double BigSpenderSizeWeight = 0.5;
        public const float NextBrowseSeconds = 5f;
        /// <summary>
        /// GR-03's floor places (GR.11): "long" places along walls and islands take Starter and Large tanks (2.5 x 1 m),
        /// "standard" ones a Starter tank (1.5 x 1 m), and "feature" places a Show tank (3 x 1.5 m): unit B's east wall and,
        /// GR-10, unit C's north wall (its feature wall). Unit C adds six places, the exhibition hall finish two along unit
        /// B's front wall; their positions are GrowthSpaces'.
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
            new GrowthPlace { Id = "c1", Unit = "unit-c", Size = "feature", Width = 3f, Depth = 1.5f },
            new GrowthPlace { Id = "c2", Unit = "unit-c", Size = "feature", Width = 3f, Depth = 1.5f },
            new GrowthPlace { Id = "c3", Unit = "unit-c", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "c4", Unit = "unit-c", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "c5", Unit = "unit-c", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "c6", Unit = "unit-c", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "e1", Unit = "hall", Size = "long", Width = 2.5f, Depth = 1f },
            new GrowthPlace { Id = "e2", Unit = "hall", Size = "long", Width = 2.5f, Depth = 1f },
        };
        public static readonly string[] UnitAPlaces = Places.Where(p => p.Unit == "unit-a").Select(p => p.Id).ToArray(), UnitBPlaces = Places.Where(p => p.Unit == "unit-b").Select(p => p.Id).ToArray();
        /// <summary>The shop containers a player buys on the floor (the lab rack comes with the lab, later).</summary>
        public static readonly string[] ShopKinds = { "starter", "large", "show" };
        /// <summary>The stations a shop starts with at level 1 (GR-02's four and, GR-06, the research bench).</summary>
        public static readonly string[] GreyboxStations = { "siphon", "grower", "incubator", "counter", "bench" };
        /// <summary>Every station the greybox upgrades: the breeding tank starts at level 0 and is bought as its level 1 (GR-05);
        /// the vat arrives at level 1 from the supplier (GR-04) and is upgraded on its card (GR-06); the generator and pumps
        /// are bought in the lab annex as their level 1 (GR-07); the sorter in the workshop (GR-08).</summary>
        public static readonly string[] UpgradeableStations = GreyboxStations.Concat(new[] { "breeding", "vat", "generator", "pump", "sorter" }).ToArray();
        /// <summary>Parents in one of the breeding tank's pairs (GR.7.2); its level 3 has two pairs.</summary>
        public const int PairSize = 2;

        /// <summary>
        /// GR-06: a level whose capability belongs to a later task waits for it (shown on the station's card with its change
        /// and gate, not bought yet). None waits now: the analyzer and planner came with GR-08, the counter's second till with
        /// GR-09's helpers.
        /// </summary>
        public static string WaitsFor(string station, int level) => null;
        /// <summary>GR.7.2's tints: Bloom makes Sunny offspring, Flow Deep, Mist Shimmer.</summary>
        public static readonly string[] TintCultures = { "bloom", "flow", "mist" };

        public int Credits, Reputation, Rp, Era = 1, Day = 1, SalesTotal, RefusalsTotal;
        public GrowthPhase Phase = GrowthPhase.Night;
        public float DaySecondsLeft;
        public bool UnitB;
        public readonly List<string> Cultures = new List<string> { "bloom", "flow" };
        /// <summary>GR-04: starter vials' doses left, by culture (collected with the siphon at the vial shelf).</summary>
        public readonly Dictionary<string, int> Vials = new Dictionary<string, int>();
        /// <summary>GR-04: supplier orders paid at night, delivered when the shop next opens.</summary>
        public readonly List<string> Orders = new List<string>();
        public float VialProgress { get; private set; }
        public readonly Dictionary<string, float> GrowerUnits = new Dictionary<string, float>();
        public readonly Dictionary<string, int> Levels = new Dictionary<string, int> { ["siphon"] = 1, ["grower"] = 1, ["incubator"] = 1, ["counter"] = 1, ["bench"] = 1 };
        public readonly List<string> Held = new List<string>();
        public readonly List<string> Chamber = new List<string>();
        public bool Incubating;
        public float IncubationLeft;
        public string Pending;
        public GrowthMixEntry LastMix;
        /// <summary>GR-06: the incubator's output, a clutch of one to three of the same creature (level 3 and 4), taken one by one.</summary>
        public readonly List<GrowthSpecimen> Hatchlings = new List<GrowthSpecimen>();
        public GrowthSpecimen Hatchling => Hatchlings.FirstOrDefault();
        /// <summary>GR-06: the culture tinting the incubator (its level 3 tint port), used by the next clutch.</summary>
        public string IncubatorTint;
        public readonly List<GrowthSpecimen> Carried = new List<GrowthSpecimen>();
        public readonly List<GrowthContainer> Containers = new List<GrowthContainer>();
        public readonly List<string> Discovered = new List<string>();
        public readonly List<GrowthMixEntry> MixLog = new List<GrowthMixEntry>();
        /// <summary>GR-05: the breeding tank's pairs (the second at its level 3), and the culture tinting its water (level 2),
        /// used by the next offspring of either pair. Parents, Offspring and BreedingLeft are the first pair's.</summary>
        public readonly GrowthBreedingPair[] Pairs = { new GrowthBreedingPair(), new GrowthBreedingPair() };
        public List<GrowthSpecimen> Parents => Pairs[0].Parents;
        public GrowthSpecimen Offspring { get => Pairs[0].Offspring; set => Pairs[0].Offspring = value; }
        public float BreedingLeft => Pairs[0].Left;
        public bool OffspringWasNew => Pairs[0].OffspringWasNew;
        public string Tint;
        public GrowthPairEntry LastPair;
        public readonly List<GrowthPairEntry> PairLog = new List<GrowthPairEntry>();
        /// <summary>GR-05: the journal's entries, a species in a colour ("pondlet/Sunny"): 72 species in 4 colours, 288 in all.</summary>
        public readonly List<string> Journal = new List<string>();
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
            foreach (var c in Cultures) GrowerUnits[c] = GrowerCapacityOf(c);
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
        /// <summary>GR-06: the siphon's twin chamber collects two doses a hold (level 3), the vacuum four (level 4).</summary>
        public int PerHold => Math.Max(1, (int)Stat("siphon", "perHold"));
        /// <summary>GR-06: the vacuum (siphon level 4) keeps pulling from the next grower in range when the one aimed at runs dry.</summary>
        public bool Vacuum => Stat("siphon", "vacuum") > 0;
        /// <summary>GR-06: the vat's double batch (level 2) doubles what the Mist source holds; its level 3 refills twice as fast.</summary>
        public int GrowerCapacityOf(string culture) => GrowerCapacity * (culture == "mist" && Level("vat") >= 2 ? 2 : 1);
        public float RefillSecondsOf(string culture) => RefillSeconds / (culture == "mist" && Level("vat") >= 3 ? 2f : 1f);
        public int MaxDoses => (int)Stat("incubator", "maxDoses");
        /// <summary>GR-06: the incubator's clutch (level 3: two of the same creature, level 4: three) and its tint port (level 3).</summary>
        public int Clutch => Math.Max(1, (int)Stat("incubator", "clutch"));
        public bool IncubatorTintPort => Stat("incubator", "tint") > 0;
        /// <summary>GR-06: the counter's queue (2, then 3 with the card reader); more visitors wait by the tanks until there is room.</summary>
        public int QueueLength => Math.Max(1, (int)Stat("counter", "queue")) + PipCalls;
        /// <summary>GR-06: the research bench's microscope (level 2) says how many doses a mix is from the nearest new creature.</summary>
        public bool Microscope => Level("bench") >= 2;
        /// <summary>GR-06: the breeding tank's pairs at its level (two at level 3).</summary>
        public int PairPlaces => HasBreedingTank ? Math.Max(1, (int)Stat("breeding", "pairs")) : 0;
        public IEnumerable<GrowthBreedingPair> ActivePairs => Pairs.Take(PairPlaces);
        public IEnumerable<GrowthSpecimen> AllParents => Pairs.SelectMany(p => p.Parents);
        public IEnumerable<GrowthSpecimen> AllOffspring => Pairs.Where(p => p.Offspring != null).Select(p => p.Offspring);
        public float IncubationSeconds => Stat("incubator", "seconds");
        public float ServeSeconds => Stat("counter", "serveSeconds");
        public bool HasBreedingTank => Level("breeding") > 0;
        public float BreedSeconds => HasBreedingTank ? Stat("breeding", "seconds") : 0;
        public bool LargeParents => HasBreedingTank && Stat("breeding", "largeParents") > 0;
        public bool TintPort => HasBreedingTank && Stat("breeding", "tint") > 0;
        /// <summary>The first pair is in and its nursery place is free: its timer runs.</summary>
        public bool Breeding => Pairs[0].Breeding;
        public static string JournalKey(string species, Colour colour) => species + "/" + colour;
        public bool HasHybrid => Discovered.Any(d => GrowthContent.Get(d).Kind != SpeciesKind.Founder);
        public IEnumerable<GrowthContainer> ShopContainers => Containers.Where(c => !c.Def.Lab);
        public IEnumerable<string> OwnedPlaces => Places.Where(p => UnitOpen(p.Unit)).Select(p => p.Id);
        public IEnumerable<string> FreePlaces => OwnedPlaces.Where(p => Containers.All(c => c.Place != p));
        public GrowthContainer Container(string id) => Containers.FirstOrDefault(c => c.Id == id);
        public GrowthContainer ContainerAt(string place) => Containers.FirstOrDefault(c => c.Place == place);
        public int VisitorsAtOnce => GrowthContent.Expansions.Where(e => e.Id == "unit-a" || e.Id == "unit-b" && UnitB || e.Id == "unit-c" && UnitC || e.Id == "exhibition-hall" && Hall).Sum(e => e.VisitorsAtOnce);
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
        public int Appeal(GrowthContainer c) => (c.Kind == "show" ? 1 : 0) + (Harmony(c) ? 1 : 0) + (c.Decor != null ? 1 : 0);

        /// <summary>How far over budget a visitor goes for a species in this container (GR.10.1: school +15%, appeal +10% a point, at most +40%).</summary>
        public float Tolerance(GrowthContainer c, string species) =>
            1f + Math.Min(GrowthContent.ToleranceCap, (c.InSchool(species) ? GrowthContent.SchoolTolerance : 0f) + Appeal(c) * GrowthContent.AppealTolerance +
                (Themed(c, species) ? GrowthContent.ThemeTolerance : 0f));

        /// <summary>Credits paid for a container: its purchase and every level since; selling an empty one returns half.</summary>
        public static int Paid(GrowthContainer c) => c.Def.Levels.Take(c.Level).Sum(l => l.Credits);
        public static int SellValue(GrowthContainer c) => Paid(c) / 2;

        /// <summary>GR.4's era 2 gate: 2 stars, 3 creatures discovered, 2 tanks.</summary>
        public bool EraTwoGateMet => Stars >= 2 && Discovered.Count >= 3 && ShopContainers.Count() >= 2;

        /// <summary>GR.4's era 3 gate: 3 stars, a hybrid bred, a Large tank. (Era 4's is in GrowthPower.cs.)</summary>
        public bool EraThreeGateMet => Stars >= 3 && HasHybrid && ShopContainers.Any(c => c.Kind == "large");

        // ---------------------------------------------------------------- the siphon and growers

        /// <summary>
        /// Holding the siphon on a grower: every hold time, as many doses as the siphon takes a hold (1; 2 with the twin
        /// chamber, 4 with the vacuum) while the grower has culture and the bulb has room. GR-06: the vacuum also pulls from
        /// the growers in range (inRange, the runtime's) once the one aimed at runs dry.
        /// </summary>
        public GrowthResult Collect(string culture, float seconds, IEnumerable<string> inRange = null)
        {
            if (GrowthContent.CultureIndex(culture) < 0) return GrowthResult.Fail(GrowthFailure.UnknownCulture, culture);
            if (!Cultures.Contains(culture)) return GrowthResult.Fail(GrowthFailure.NotOwned, culture);
            if (Held.Count >= SiphonCapacity) { CollectProgress = 0; return GrowthResult.Fail(GrowthFailure.SiphonFull); }
            var sources = new List<string> { culture };
            if (Vacuum && inRange != null) sources.AddRange(inRange.Where(c => c != culture && Cultures.Contains(c)).Distinct());
            string NextSource() => sources.FirstOrDefault(c => GrowerUnits[c] >= 1);
            if (NextSource() == null) { CollectProgress = 0; return GrowthResult.Fail(GrowthFailure.GrowerEmpty, culture); }
            CollectProgress += seconds;
            var got = new Dictionary<string, int>();
            while (CollectProgress >= HoldSeconds && Held.Count < SiphonCapacity && NextSource() != null)
            {
                CollectProgress -= HoldSeconds;
                for (int n = 0; n < PerHold && Held.Count < SiphonCapacity; n++)
                {
                    string from = NextSource();
                    if (from == null) break;
                    GrowerUnits[from] -= 1;
                    Held.Add(from);
                    got[from] = got.TryGetValue(from, out int k) ? k + 1 : 1;
                }
            }
            if (Held.Count >= SiphonCapacity || NextSource() == null) CollectProgress = 0;
            int collected = got.Values.Sum();
            if (collected > 0) Raise(GrowthEventKind.Collected, string.Join(", ", got.Select(g => g.Value + " " + Name(g.Key))), amount: collected);
            return GrowthResult.Ok(collected.ToString());
        }

        public void StopCollecting() => CollectProgress = 0;

        // ---------------------------------------------------------------- GR-31: the auto-open (F32), what a visitor picked (F30), tank rules (F37)

        /// <summary>F32: the shop opens by itself with this many creatures on show (0: only by hand).</summary>
        public int AutoOpenAt;
        /// <summary>F32: the night's first seconds, so a shop still stocked from yesterday does not open before the player has looked up.</summary>
        public const float AutoOpenWait = 15f;
        public static readonly int[] AutoOpenChoices = { 0, 3, 6, 10, 15, 20, 30 };
        float nightClock;

        void TickAutoOpen(float seconds)
        {
            if (Phase != GrowthPhase.Night) { nightClock = 0; return; }
            nightClock += seconds;
            if (AutoOpenAt <= 0 || nightClock < AutoOpenWait || Stock.Count() < AutoOpenAt) return;
            if (OpenShop().Success) Raise(GrowthEventKind.Guide, "The shop opened by itself: " + AutoOpenAt + " or more creatures on show");
        }

        /// <summary>F30: what a queuing visitor picked, while it is still in its tank.</summary>
        public GrowthSpecimen PickedBy(GrowthVisitor v)
        {
            foreach (var id in v.SpecimenIds)
            {
                var found = HolderOf(id)?.Occupants.FirstOrDefault(o => o.Id == id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>F37: the rules a tank can have.</summary>
        public static readonly string[] TankRules = { "", "fresh", "warm", "brine", "calm", "lively", "one kind" };

        public static bool RuleAllows(GrowthContainer c, string species)
        {
            var def = GrowthContent.Get(species);
            switch (c.Rule)
            {
                case "fresh": return def.Water == Water.Fresh;
                case "warm": return def.Water == Water.Warm;
                case "brine": return def.Water == Water.Brine;
                case "calm": return def.Temperament == Temperament.Calm;
                case "lively": return def.Temperament == Temperament.Lively;
                case "one kind": return c.Occupants.All(o => o.Species == species);
                default: return true;
            }
        }

        public GrowthResult SetTankRule(string containerId, string rule)
        {
            var c = Container(containerId);
            if (c == null) return GrowthResult.Fail(GrowthFailure.NoSuchContainer, containerId);
            if (Array.IndexOf(TankRules, rule) < 0) return GrowthResult.Fail(GrowthFailure.Locked, "Unknown rule " + rule);
            c.Rule = rule;
            return GrowthResult.Ok(rule);
        }

        // ---------------------------------------------------------------- the incubator

        /// <summary>Who owns the chamber's batch, when a machine or a helper does: the player's doses keep out of it.</summary>
        public string ChamberLock => AutoBatch ? "The auto-doser is dosing" : MossBatch ? "Moss is dosing" : null;

        public GrowthResult AddDose(string culture)
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (ChamberLock != null) return GrowthResult.Fail(GrowthFailure.Busy, ChamberLock);
            if (!Held.Contains(culture)) return GrowthResult.Fail(GrowthFailure.NoDose, culture);
            if (Chamber.Count >= MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "up to " + MaxDoses + " doses");
            Held.Remove(culture);
            Chamber.Add(culture);
            return GrowthResult.Ok();
        }

        public GrowthResult ReturnDose(string culture)
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (ChamberLock != null) return GrowthResult.Fail(GrowthFailure.Busy, ChamberLock);
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
            if (ChamberLock != null) return GrowthResult.Fail(GrowthFailure.Busy, ChamberLock);
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
        /// <summary>The cultures a mix may use: those owned and any in the chamber (a vial's culture before its source is owned).</summary>
        List<string> MixCultures() => Cultures.Concat(Chamber).Distinct().ToList();

        public MixResult Preview() => Chamber.Count == 0 ? new MixResult(null, false, -1) : GrowthRules.Mix(ChamberRecipe(), MixCultures(), Discovered);

        /// <summary>GR.15.1: an empty mix is refused; the chamber can never hold more than the level allows; the output must be free.</summary>
        public GrowthResult StartIncubation()
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (ChamberLock != null) return GrowthResult.Fail(GrowthFailure.Busy, ChamberLock);
            if (Hatchlings.Count > 0) return GrowthResult.Fail(GrowthFailure.HatchlingWaiting);
            if (Chamber.Count == 0) return GrowthResult.Fail(GrowthFailure.ChamberEmpty);
            if (Chamber.Count > MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "up to " + MaxDoses + " doses");
            var recipe = ChamberRecipe();
            var mix = GrowthRules.Mix(recipe, MixCultures(), Discovered);
            Pending = mix.Species.Id;
            PendingMix = new GrowthMixEntry { Doses = recipe, Species = Pending, Exact = mix.Exact, WasNew = !Discovered.Contains(Pending), DistanceToNew = mix.DistanceToNew };
            Chamber.Clear();
            Incubating = true;
            IncubationLeft = IncubationSeconds;
            return GrowthResult.Ok(Pending);
        }

        /// <summary>GR-29 (F9): the mix now in the incubator, logged once it hatches, so the log never tells what it makes before then.</summary>
        public GrowthMixEntry PendingMix;

        void LogPendingMix()
        {
            if (PendingMix == null) return;
            var entry = MixLog.FirstOrDefault(e => e.Doses.SequenceEqual(PendingMix.Doses));
            if (entry != null) { entry.Count++; entry.WasNew = false; }
            else MixLog.Add(entry = PendingMix);
            LastMix = entry;
            PendingMix = null;
        }

        // ---------------------------------------------------------------- GR-31 (F26, F27): quick recipes

        /// <summary>How many recipes the incubator's card keeps at hand.</summary>
        public const int QuickRecipeSlots = 3;
        /// <summary>The recipes the player chose to keep at hand.</summary>
        public readonly List<string> PinnedRecipes = new List<string>();

        /// <summary>Every recipe found, newest first.</summary>
        public List<string> FoundRecipes() => MixLog.AsEnumerable().Reverse().Where(m => m.Exact && Discovered.Contains(m.Species)).Select(m => m.Species).Distinct().ToList();

        /// <summary>Whether a recipe could be dosed now from the siphon and the chamber.</summary>
        public bool CanDose(string species)
        {
            var def = GrowthContent.Get(species);
            if (def == null || def.Doses > MaxDoses) return false;
            var have = new List<string>(Held); have.AddRange(Chamber);
            return Enumerable.Range(0, def.Recipe.Length).All(i => def.Recipe[i] == 0 || have.Count(c => c == GrowthContent.Cultures[i].Id) >= def.Recipe[i]);
        }

        /// <summary>The recipes at hand: those pinned, then those the siphon can dose now, then the newest.</summary>
        public List<string> QuickRecipes()
        {
            var found = FoundRecipes();
            return PinnedRecipes.Where(found.Contains).Concat(found.Where(r => !PinnedRecipes.Contains(r)).OrderByDescending(CanDose)).Take(QuickRecipeSlots).ToList();
        }

        /// <summary>Pins a found recipe to the incubator's card, or takes its pin off; three at most.</summary>
        public GrowthResult PinRecipe(string species)
        {
            if (PinnedRecipes.Remove(species)) return GrowthResult.Ok("off");
            if (!FoundRecipes().Contains(species)) return GrowthResult.Fail(GrowthFailure.NotDiscovered, species);
            if (PinnedRecipes.Count >= QuickRecipeSlots) return GrowthResult.Fail(GrowthFailure.Busy, QuickRecipeSlots + " are pinned: take one off first");
            PinnedRecipes.Add(species);
            return GrowthResult.Ok("on");
        }

        /// <summary>GR-29 (F19): a click on the incubator: as many doses from the siphon as the chamber takes, the rest staying in the siphon.</summary>
        public GrowthResult FillChamber()
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (ChamberLock != null) return GrowthResult.Fail(GrowthFailure.Busy, ChamberLock);
            if (Chamber.Count >= MaxDoses) return GrowthResult.Fail(GrowthFailure.ChamberFull, "up to " + MaxDoses + " doses");
            if (Held.Count == 0) return GrowthResult.Fail(GrowthFailure.NoDose, "a dose");
            int moved = 0;
            while (Held.Count > 0 && Chamber.Count < MaxDoses) { Chamber.Add(Held[0]); Held.RemoveAt(0); moved++; }
            return GrowthResult.Ok(moved.ToString());
        }

        /// <summary>GR-29 (F10): throws the chamber's doses away, so a chamber whose doses cannot go back into a full siphon never stops the player.</summary>
        public GrowthResult PourOutChamber()
        {
            if (Incubating) return GrowthResult.Fail(GrowthFailure.IncubatorBusy);
            if (ChamberLock != null) return GrowthResult.Fail(GrowthFailure.Busy, ChamberLock);
            if (Chamber.Count == 0) return GrowthResult.Fail(GrowthFailure.ChamberEmpty);
            int poured = Chamber.Count;
            Chamber.Clear();
            return GrowthResult.Ok(poured.ToString());
        }

        /// <summary>GR-29 (F17): throws doses in the siphon away: one culture's, or with none named, every dose.</summary>
        public GrowthResult PourOutSiphon(string culture = null)
        {
            int poured = Held.RemoveAll(c => culture == null || c == culture);
            return poured == 0 ? GrowthResult.Fail(GrowthFailure.NoDose, culture == null ? "a dose" : Name(culture)) : GrowthResult.Ok(poured.ToString());
        }

        void Hatch()
        {
            LogPendingMix();
            Incubating = false;
            IncubationLeft = 0;
            // GR-06: a clutch of the same creature (incubator levels 3-4), in the tint's colour when the port has one (GR.7.2).
            var colour = IncubatorTint == null ? Colour.Classic : GrowthRules.BreedColour(Colour.Classic, Colour.Classic, IncubatorTint);
            IncubatorTint = null;
            bool isNew = false;
            for (int i = 0; i < Clutch; i++)
            {
                var h = new GrowthSpecimen { Id = nextId++, Species = Pending, Colour = colour };
                Hatchlings.Add(h);
                isNew |= Record(h);
            }
            if (!isNew)
            {
                string name = (colour == Colour.Classic ? "" : colour + " ") + GrowthContent.Get(Pending).Name;
                Raise(GrowthEventKind.Hatched, Clutch > 1 ? Clutch + " " + Plural(name) + " hatched" : name + " hatched", Pending);
            }
            Pending = null;
        }

        /// <summary>Why a dose cannot tint the incubator now (None when it can): its level 3 tint port, Bloom, Flow or Mist.</summary>
        public GrowthFailure IncubatorTintCheck(string culture, out string why)
        {
            why = null;
            if (!IncubatorTintPort) { why = "Tinting needs the incubator's level 3"; return GrowthFailure.Locked; }
            if (!TintCultures.Contains(culture)) { why = "Only Bloom, Flow and Mist tint"; return GrowthFailure.UnknownCulture; }
            if (IncubatorTint != null) { why = "The incubator is tinted with " + Name(IncubatorTint) + " already"; return GrowthFailure.Busy; }
            if (!Held.Contains(culture)) { why = "1 " + Name(culture); return GrowthFailure.NoDose; }
            return GrowthFailure.None;
        }

        /// <summary>GR.7.2's incubator tint (GR-06): a dose of Bloom, Flow or Mist makes the next clutch Sunny, Deep or Shimmer.</summary>
        public GrowthResult TintIncubator(string culture)
        {
            var failure = IncubatorTintCheck(culture, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Held.Remove(culture);
            IncubatorTint = culture;
            return GrowthResult.Ok(culture);
        }

        public GrowthResult ClearIncubatorTint()
        {
            if (IncubatorTint == null) return GrowthResult.Fail(GrowthFailure.NotTinted, "The incubator is not tinted");
            if (Held.Count >= SiphonCapacity) return GrowthResult.Fail(GrowthFailure.SiphonFull);
            Held.Add(IncubatorTint);
            IncubatorTint = null;
            return GrowthResult.Ok();
        }

        /// <summary>
        /// GR.7.2: a new creature's journal entry, its species in its colour. A first species is a discovery (founder 5 RP,
        /// hybrid 8, crown 20); a known species in a new colour is a new colour (2 RP). True when either is new.
        /// </summary>
        bool Record(GrowthSpecimen s)
        {
            string key = JournalKey(s.Species, s.Colour);
            bool newEntry = !Journal.Contains(key);
            if (newEntry) Journal.Add(key);
            var def = s.Def;
            if (!Discovered.Contains(s.Species))
            {
                Discovered.Add(s.Species);
                Rp += GrowthContent.RpFor(def.Kind);
                Raise(GrowthEventKind.Discovery, "New creature: " + (s.Colour == Colour.Classic ? "" : s.Colour + " ") + def.Name + "!", s.Species);
                CheckEra();
                return true;
            }
            if (!newEntry) return false;
            Rp += GrowthContent.RpColour;
            Raise(GrowthEventKind.NewColour, "New colour: " + s.Colour + " " + def.Name + "!", s.Species);
            return true;
        }

        // ---------------------------------------------------------------- carrying and containers

        /// <summary>Takes the first hatchling of the clutch into the hands.</summary>
        public GrowthResult TakeHatchling()
        {
            if (Hatchlings.Count == 0) return GrowthResult.Fail(GrowthFailure.NoHatchling);
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            Carried.Add(Hatchlings[0]);
            Hatchlings.RemoveAt(0);
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
            if (IsOnDisplay(s)) return GrowthResult.Fail(GrowthFailure.Reserved, "on display for the Grand Exhibition");
            if (s.ReservedBy != 0) return GrowthResult.Fail(GrowthFailure.Reserved, "a visitor is buying it");
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            c.Occupants.Remove(s);
            Carried.Add(s);
            return GrowthResult.Ok(s.Species);
        }

        /// <summary>GR-29 (F12): why a creature in a tank cannot change places with one in hand (None when it can). The tank may be
        /// full and the hands too: the swap needs no free space beyond the one the leaving creature gives up.</summary>
        public GrowthFailure SwapCheck(string containerId, int takeId, int putId, out string why)
        {
            why = null;
            var c = Container(containerId);
            if (c == null) { why = containerId; return GrowthFailure.NoSuchContainer; }
            var take = c.Occupants.FirstOrDefault(o => o.Id == takeId);
            var put = Carried.FirstOrDefault(o => o.Id == putId);
            if (take == null || put == null) { why = ""; return GrowthFailure.NoSuchSpecimen; }
            if (IsOnDisplay(take)) { why = "on display for the Grand Exhibition"; return GrowthFailure.Reserved; }
            if (take.ReservedBy != 0) { why = "a visitor is buying it"; return GrowthFailure.Reserved; }
            if (!GrowthRules.Fits(c.Def, c.Level, c.Occupants.Where(o => o != take).Select(o => o.Species), put.Species))
            {
                why = (int)put.Def.Size > (int)c.Def.Largest ? "Too big for a " + c.Def.Name : "Not enough spaces";
                return GrowthFailure.DoesNotFit;
            }
            return GrowthFailure.None;
        }

        public GrowthResult Swap(string containerId, int takeId, int putId)
        {
            var failure = SwapCheck(containerId, takeId, putId, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var c = Container(containerId);
            var take = c.Occupants.First(o => o.Id == takeId);
            var put = Carried.First(o => o.Id == putId);
            c.Occupants.Remove(take); Carried.Remove(put);
            c.Occupants.Add(put); Carried.Add(take);
            return GrowthResult.Ok(put.Species);
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
            if (level.Era > Era) { why = "Needs Era " + level.Era; return GrowthFailure.Locked; }
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
            if (next.Era > Era) { why = "Needs Era " + next.Era; return GrowthFailure.Locked; }
            if (next.Stars > Stars) { why = "Needs " + next.Stars + " stars"; return GrowthFailure.Locked; }
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
            if (c.Def.Lab) { why = "Racks stay on their rack places in the workshop"; return GrowthFailure.NoSuchPlace; }
            var spot = Place(place);
            if (spot == null || !OwnedPlaces.Contains(place)) { why = "Not an open place"; return GrowthFailure.NoSuchPlace; }
            if (ContainerAt(place) != null) { why = "The place is taken"; return GrowthFailure.PlaceTaken; }
            if (!spot.Takes(c.Def)) { why = c.Def.Name + " is " + Footprint(c.Def) + "; this place takes " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m"; return GrowthFailure.TooBigForPlace; }
            if (Phase != GrowthPhase.Night) { why = "Containers are moved at night"; return GrowthFailure.NotAtNight; }
            if (c.Occupants.Any(IsOnDisplay)) { why = "Its crowns are on display for the Grand Exhibition"; return GrowthFailure.Reserved; }
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
            Raise(GrowthEventKind.Sold, c.Def.Name + " sold for " + value + " credits" + (c.Decor != null ? "; its " + GrowthContent.DecorPiece(c.Decor).Name + " is back in the decor box" : ""), amount: value);
            return GrowthResult.Ok(value.ToString());
        }

        /// <summary>Why a container's water cannot be set now (None when it can).</summary>
        public GrowthFailure WaterCheck(string containerId, Water water, out string why)
        {
            why = null;
            var c = Container(containerId);
            if (c == null) { why = "No such container"; return GrowthFailure.NoSuchContainer; }
            if (c.Water == water) { why = "Already " + water.ToString().ToLowerInvariant(); return GrowthFailure.SameWater; }
            if (Era < GrowthContent.ComfortEra) { why = "Needs Era " + GrowthContent.ComfortEra; return GrowthFailure.Locked; }
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

        // ---------------------------------------------------------------- the breeding tank (GR.7.2, GR-05)

        /// <summary>Why a creature cannot go into the breeding tank now (None when it can), with a sentence.</summary>
        public GrowthFailure ParentCheck(GrowthSpecimen s, out string why)
        {
            why = null;
            if (!HasBreedingTank) { why = "Buy the breeding tank first"; return GrowthFailure.Locked; }
            if (ActivePairs.All(p => p.Parents.Count >= PairSize)) { why = PairPlaces == 1 ? "The breeding tank takes one pair" : "The breeding tank's " + PairPlaces + " pairs are full"; return GrowthFailure.ChamberFull; }
            if (s.Def.Size == SizeClass.Large && !LargeParents) { why = "Large parents need the breeding tank's level 2"; return GrowthFailure.DoesNotFit; }
            return GrowthFailure.None;
        }

        /// <summary>Puts a carried creature into the breeding tank (the first that may go, or the one asked for); a pair starts breeding.</summary>
        public GrowthResult PutParent(int specimenId = 0)
        {
            if (Carried.Count == 0) return GrowthResult.Fail(GrowthFailure.HandsEmpty);
            var candidates = specimenId == 0 ? Carried.ToList() : Carried.Where(x => x.Id == specimenId).ToList();
            if (candidates.Count == 0) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            GrowthSpecimen parent = null; string why = null; var failure = GrowthFailure.None;
            foreach (var x in candidates) { failure = ParentCheck(x, out why); if (failure == GrowthFailure.None) { parent = x; break; } }
            if (parent == null) return GrowthResult.Fail(failure, why);
            var pair = ActivePairs.First(p => p.Parents.Count < PairSize);
            Carried.Remove(parent);
            pair.Parents.Add(parent);
            RestartBreeding(pair);
            return GrowthResult.Ok(parent.Species);
        }

        /// <summary>Takes a parent out into the hands, at any time (GR.15.1: the timer starts again when a pair is back in).</summary>
        public GrowthResult TakeParent(int specimenId)
        {
            var pair = Pairs.FirstOrDefault(x => x.Parents.Any(s => s.Id == specimenId));
            if (pair == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            var p = pair.Parents.First(s => s.Id == specimenId);
            pair.Parents.Remove(p);
            Carried.Add(p);
            RestartBreeding(pair);
            return GrowthResult.Ok(p.Species);
        }

        /// <summary>Takes an offspring out of the nursery (the first pair's, or the pair asked for); that pair starts on the next one (breeding as production, GR.7.2).</summary>
        public GrowthResult TakeOffspring(int pairIndex = -1)
        {
            var pair = pairIndex >= 0 && pairIndex < Pairs.Length ? Pairs[pairIndex] : Pairs.FirstOrDefault(p => p.Offspring != null);
            if (pair == null || pair.Offspring == null) return GrowthResult.Fail(GrowthFailure.NoOffspring, "Nothing in the nursery yet");
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            Carried.Add(pair.Offspring);
            pair.Offspring = null;
            RestartBreeding(pair);
            return GrowthResult.Ok();
        }

        /// <summary>Why a dose cannot tint the breeding water now (None when it can).</summary>
        public GrowthFailure TintCheck(string culture, out string why)
        {
            why = null;
            if (!HasBreedingTank) { why = "Buy the breeding tank first"; return GrowthFailure.Locked; }
            if (!TintPort) { why = "Tinting needs the breeding tank's level 2"; return GrowthFailure.Locked; }
            if (!TintCultures.Contains(culture)) { why = "Only Bloom, Flow and Mist tint the water"; return GrowthFailure.UnknownCulture; }
            if (Tint != null) { why = "The water is tinted with " + Name(Tint) + " already"; return GrowthFailure.Busy; }
            if (!Held.Contains(culture)) { why = "1 " + Name(culture); return GrowthFailure.NoDose; }
            return GrowthFailure.None;
        }

        /// <summary>GR.7.2: a dose of Bloom, Flow or Mist from the siphon tints the water; the next offspring is Sunny, Deep or Shimmer.</summary>
        public GrowthResult TintWater(string culture)
        {
            var failure = TintCheck(culture, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Held.Remove(culture);
            Tint = culture;
            return GrowthResult.Ok(culture);
        }

        /// <summary>Takes an unused tint back into the siphon.</summary>
        public GrowthResult ClearTint()
        {
            if (Tint == null) return GrowthResult.Fail(GrowthFailure.NotTinted, "The water is not tinted");
            if (Held.Count >= SiphonCapacity) return GrowthResult.Fail(GrowthFailure.SiphonFull);
            Held.Add(Tint);
            Tint = null;
            return GrowthResult.Ok();
        }

        /// <summary>The colour the pair's next offspring will have (the tint, else the higher parent colour).</summary>
        public Colour NextColour => NextColourOf(Pairs[0]);
        public Colour NextColourOf(GrowthBreedingPair pair) => pair.Parents.Count < PairSize ? Colour.Classic : GrowthRules.BreedColour(pair.Parents[0].Colour, pair.Parents[1].Colour, Tint);

        static (string a, string b) Ordered(string x, string y) => string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);

        /// <summary>The pair log's entry for two species (either order), or null when the pair was never tried.</summary>
        public GrowthPairEntry PairEntry(string x, string y)
        {
            var (a, b) = Ordered(x, y);
            return PairLog.FirstOrDefault(p => p.A == a && p.B == b);
        }

        void RestartBreeding(GrowthBreedingPair pair) => pair.Left = pair.Breeding ? BreedSeconds : 0;

        /// <summary>
        /// GR.7.2: the pair makes what GrowthRules.Breed gives (the species whose breeding value is nearest the parents'
        /// average, a special pair's crown, a same-species pair true) in the tint's colour or the higher parent colour, into
        /// the nursery. The parents stay; the tint is used up.
        /// </summary>
        void BreedOffspring(GrowthBreedingPair pair)
        {
            var a = pair.Parents[0]; var b = pair.Parents[1];
            var child = GrowthRules.Breed(a.Species, b.Species);
            var colour = GrowthRules.BreedColour(a.Colour, b.Colour, Tint);
            Tint = null;
            pair.Left = 0;
            var offspring = pair.Offspring = new GrowthSpecimen { Id = nextId++, Species = child.Id, Colour = colour };
            var entry = PairEntry(a.Species, b.Species);
            if (entry != null) { entry.Count++; entry.WasNew = false; }
            else
            {
                var (x, y) = Ordered(a.Species, b.Species);
                PairLog.Add(entry = new GrowthPairEntry { A = x, B = y, Species = child.Id, WasNew = !Discovered.Contains(child.Id) });
            }
            LastPair = entry;
            pair.OffspringWasNew = Record(offspring);
            if (!pair.OffspringWasNew) Raise(GrowthEventKind.Bred, (colour == Colour.Classic ? "" : colour + " ") + child.Name + " bred", child.Id);
        }

        // ---------------------------------------------------------------- cultures, the supplier and research (GR.6)

        public bool Owns(string culture) => Cultures.Contains(culture);

        /// <summary>A culture becomes renewable: its grower (or the vat for Mist) stands in the back room, full.</summary>
        void AddCulture(string culture, string how)
        {
            if (Cultures.Contains(culture)) return;
            Cultures.Add(culture);
            if (GrowthContent.Culture(culture).Source == CultureSource.Vat) Levels["vat"] = Math.Max(1, Level("vat"));
            GrowerUnits[culture] = GrowerCapacityOf(culture);
            Raise(GrowthEventKind.CultureOwned, "New culture: " + Name(culture) + " (" + how + ")", amount: GrowthContent.CultureIndex(culture));
        }

        /// <summary>Why a catalog item cannot be ordered now (None when it can), with a sentence.</summary>
        public GrowthFailure OrderCheck(string itemId, out string why)
        {
            why = null;
            var item = GrowthContent.CatalogItem(itemId);
            if (item == null) { why = "Not in the catalog"; return GrowthFailure.NotInCatalog; }
            if (Orders.Contains(itemId)) { why = "Ordered: it arrives when the shop opens"; return GrowthFailure.Ordered; }
            if (item.Kind == CatalogKind.Decor) { if (DecorOwned.Contains(item.Decor)) { why = "It is yours: in the decor box or on a tank"; return GrowthFailure.Owned; } }
            else if (Owns(item.Culture)) { why = item.Kind == CatalogKind.Vial ? "You grow " + Name(item.Culture) + " already" : "You have it"; return GrowthFailure.Owned; }
            if (item.Kind != CatalogKind.Vial && item.Kind != CatalogKind.Decor && Orders.Any(o => GrowthContent.CatalogItem(o).Culture == item.Culture && GrowthContent.CatalogItem(o).Kind != CatalogKind.Vial)) { why = "Already on order"; return GrowthFailure.Ordered; }
            if (item.Stars > Stars) { why = "Catalog page " + item.Stars + ": needs " + item.Stars + " stars"; return GrowthFailure.Locked; }
            if (item.Era > Era) { why = "Needs Era " + item.Era; return GrowthFailure.Locked; }
            if (item.Rp > Rp) { why = "Needs " + item.Rp + " research points"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Orders are placed at night"; return GrowthFailure.NotAtNight; }
            if (Credits < item.Credits) { why = "Needs " + item.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Orders an item from the supplier at night: paid now, delivered when the shop next opens (GR.6: no timers).</summary>
        public GrowthResult Order(string itemId)
        {
            var failure = OrderCheck(itemId, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var item = GrowthContent.CatalogItem(itemId);
            Credits -= item.Credits;
            Rp -= item.Rp;
            Orders.Add(itemId);
            Note("order");
            Raise(GrowthEventKind.Ordered, "Ordered: " + item.Name + " (arrives when the shop opens)", amount: item.Credits);
            return GrowthResult.Ok();
        }

        /// <summary>Takes an order back at night, with its credits and research points.</summary>
        public GrowthResult CancelOrder(string itemId)
        {
            if (!Orders.Contains(itemId)) return GrowthResult.Fail(GrowthFailure.NotInCatalog, "Not on order");
            if (Phase != GrowthPhase.Night) return GrowthResult.Fail(GrowthFailure.NotAtNight, "Orders change at night");
            var item = GrowthContent.CatalogItem(itemId);
            Orders.Remove(itemId);
            Credits += item.Credits;
            Rp += item.Rp;
            return GrowthResult.Ok();
        }

        /// <summary>The night's orders arrive as the shop opens: growers and the vat stand in the back room, vials on its shelf.</summary>
        void Deliver()
        {
            foreach (var id in Orders.ToList())
            {
                var item = GrowthContent.CatalogItem(id);
                Orders.Remove(id);
                Raise(GrowthEventKind.Delivered, "Delivered: " + item.Name + (item.Kind == CatalogKind.Decor ? " (in the decor box: put it on a tank at night)" : ""));
                switch (item.Kind)
                {
                    case CatalogKind.Grower: AddCulture(item.Culture, "its grower is in the laboratory"); break;
                    case CatalogKind.Vat: AddCulture(item.Culture, "the conditioning vat is in the laboratory"); break;
                    case CatalogKind.Vial: Vials[item.Culture] = (Vials.TryGetValue(item.Culture, out int n) ? n : 0) + GrowthContent.VialDoses; break;
                    case CatalogKind.Decor: if (!DecorOwned.Contains(item.Decor)) DecorOwned.Add(item.Decor); break;
                }
            }
        }

        /// <summary>Why a culture cannot be researched now (None when it can): Spark and Dusk come from research points.</summary>
        public GrowthFailure ResearchCheck(string culture, out string why)
        {
            why = null;
            var c = GrowthContent.Culture(culture);
            if (c == null || c.Source != CultureSource.Research) { why = "Not a research culture"; return GrowthFailure.NotInCatalog; }
            if (Owns(culture)) { why = "Researched"; return GrowthFailure.Owned; }
            if (c.Era > Era) { why = "Needs Era " + c.Era; return GrowthFailure.Locked; }
            if (c.Stars > Stars) { why = "Needs " + c.Stars + " stars"; return GrowthFailure.Locked; }
            if (c.Rp > Rp) { why = "Needs " + c.Rp + " research points (you have " + Rp + ")"; return GrowthFailure.Locked; }
            return GrowthFailure.None;
        }

        /// <summary>Research a culture with research points: its grower stands in the back room at once (GR.6).</summary>
        public GrowthResult Research(string culture)
        {
            var failure = ResearchCheck(culture, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Rp -= GrowthContent.Culture(culture).Rp;
            AddCulture(culture, "researched; its grower is in the laboratory");
            return GrowthResult.Ok();
        }

        /// <summary>Holding the siphon on a vial: one dose every hold time, as from a grower, until the vial is empty.</summary>
        public GrowthResult CollectVial(string culture, float seconds)
        {
            if (!Vials.TryGetValue(culture, out int left) || left < 1) { VialProgress = 0; return GrowthResult.Fail(GrowthFailure.NoDose, Name(culture) + " vial is empty"); }
            if (Held.Count >= SiphonCapacity) { VialProgress = 0; return GrowthResult.Fail(GrowthFailure.SiphonFull); }
            VialProgress += seconds;
            int collected = 0;
            while (VialProgress >= HoldSeconds && Held.Count < SiphonCapacity && left > 0)
            {
                VialProgress -= HoldSeconds;
                left--; Held.Add(culture); collected++;
            }
            if (left == 0) { Vials.Remove(culture); VialProgress = 0; } else Vials[culture] = left;
            if (Held.Count >= SiphonCapacity) VialProgress = 0;
            if (collected > 0) { Raise(GrowthEventKind.Collected, collected + " " + Name(culture), amount: collected); Note("vial"); }
            return GrowthResult.Ok(collected.ToString());
        }

        public void StopVial() => VialProgress = 0;

        /// <summary>
        /// GR-03's densest layout (validation): unit B open, era 4 and 4 stars, every place of units A and B holding the largest
        /// container it takes at its highest level, filled to the last space with small creatures of 21 species, three of each
        /// in turn (schools). GR-10's final layout (final): unit C and the exhibition hall finish open too, era 5, every place.
        /// </summary>
        public static GrowthShop Densest(int stars = 4, bool final = false)
        {
            var shop = new GrowthShop { Era = final ? 5 : 4, UnitB = true, UnitC = final, Hall = final, Credits = 0, Reputation = GrowthContent.StarThresholds[Math.Max(0, stars - 1)] };
            shop.Containers.Clear();
            var small = GrowthContent.Species.Where(s => s.Size == SizeClass.Small && s.Kind != SpeciesKind.Crown).Select(s => s.Id).ToList();
            int k = 0;
            foreach (var place in Places.Where(p => shop.UnitOpen(p.Unit)))
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
            if (!UpgradeableStations.Contains(station)) { why = "Not in the greybox"; return GrowthFailure.MaxLevel; }
            int lv = Level(station);
            var levels = GrowthContent.Station(station).Levels;
            if (lv >= levels.Length) { why = "Highest level"; return GrowthFailure.MaxLevel; }
            next = levels[lv];
            if (station == "vat" && lv == 0) { why = "Order the conditioning vat from the supplier catalog"; return GrowthFailure.Locked; }
            string later = WaitsFor(station, next.Level);
            if (later != null) { why = later; return GrowthFailure.Locked; }
            if (next.Era > Era) { why = "Needs Era " + next.Era; return GrowthFailure.Locked; }
            if (next.Stars > Stars) { why = "Needs " + next.Stars + " stars"; return GrowthFailure.Locked; }
            if (next.Rp > Rp) { why = "Needs " + next.Rp + " research points"; return GrowthFailure.Locked; }
            // GR-07: the generator and pumps stand in the lab annex; electric pumps need a generator.
            if ((station == "generator" || station == "pump") && !Annex) { why = "Open the power room first"; return GrowthFailure.Locked; }
            if (station == "pump" && next.Level == 2 && !HasGenerator) { why = "Electric pumps need the generator"; return GrowthFailure.Locked; }
            // GR-08: the sorter and the generator's twin turbine and tide engine stand in the workshop.
            if (station == "sorter" && !Workshop) { why = "Open the workshop first"; return GrowthFailure.Locked; }
            if (station == "generator" && next.Level >= 3 && !Workshop) { why = "The " + next.Change.Split(':')[0].ToLowerInvariant() + " stands in the workshop: open it first"; return GrowthFailure.Locked; }
            // GR.15.1: an upgrade installs only when its station is idle.
            if (station == "incubator" && (Incubating || Hatchlings.Count > 0 || AutoBatch || MossBatch)) { why = "The incubator is busy"; return GrowthFailure.Busy; }
            // GR-09: the counter's second till is a helper's.
            if (station == "counter" && next.Level == 4 && Helper("pip") == null) { why = "Hire Pip first: the second till is for a helper"; return GrowthFailure.Locked; }
            if (station == "breeding" && Pairs.Any(p => p.Breeding || p.Offspring != null)) { why = "Busy: take a parent or the offspring out first"; return GrowthFailure.Busy; }
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
            if (station == "sorter" && !PowerOrder.Contains(SorterPower)) PowerOrder.Add(SorterPower);
            if (station == "bench") RefreshHint();
            // A bigger source fills its new room in time; nothing it holds is lost (GR.15.1).
            Raise(GrowthEventKind.Upgraded, GrowthContent.Station(station).Name + (next.Level == 1 ? " bought: " : " level " + next.Level + ": ") + next.Change, amount: next.Credits);
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
            if (Era < unit.Era) { why = "Needs Era " + unit.Era + ": 2 stars, 3 creatures discovered and 2 tanks"; return GrowthFailure.Locked; }
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
            Raise(GrowthEventKind.Opened, "Shop unit B opened", amount: unit.Credits);
            return GrowthResult.Ok();
        }

        void CheckEra()
        {
            if (Era == 1 && EraTwoGateMet)
            {
                Era = 2;
                Raise(GrowthEventKind.EraUp, "Era 2: " + GrowthContent.Eras[1].Name + ". " + GrowthContent.Eras[1].Opens + ".");
            }
            if (Era == 2 && EraThreeGateMet)
            {
                Era = 3;
                Raise(GrowthEventKind.EraUp, "Era 3: " + GrowthContent.Eras[2].Name + ". " + GrowthContent.Eras[2].Opens + ".");
            }
            if (Era == 3 && EraFourGateMet)
            {
                Era = 4;
                Raise(GrowthEventKind.EraUp, "Era 4: " + GrowthContent.Eras[3].Name + ". " + GrowthContent.Eras[3].Opens + ".");
            }
            if (Era == 4 && EraFiveGateMet)
            {
                Era = 5;
                Raise(GrowthEventKind.EraUp, "Era 5: " + GrowthContent.Eras[4].Name + ". " + GrowthContent.Eras[4].Opens + ".");
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
            StartExhibitionDay();
            Deliver();
            StartLineDay();
            RefreshHint();
            return GrowthResult.Ok();
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0) return;
            TickSlap(seconds);
            TickAutoOpen(seconds);
            foreach (var c in Cultures)
                GrowerUnits[c] = Math.Min(GrowerCapacityOf(c), GrowerUnits[c] + seconds / RefillSecondsOf(c));
            TickPower(seconds);
            if (Incubating)
            {
                IncubationLeft -= seconds;
                if (IncubationLeft <= 0) Hatch();
            }
            foreach (var pair in ActivePairs)
            {
                if (!pair.Breeding) continue;
                pair.Left -= seconds;
                if (pair.Left <= 0) BreedOffspring(pair);
            }
            if (Phase == GrowthPhase.Day) TickDay(seconds);
            TickHelpers(seconds);
            // Visitors who left are gone once they have walked out (the presentation walks them in that time).
            foreach (var v in Visitors) if (v.State == GrowthVisitorState.Leaving) v.Clock += Phase == GrowthPhase.Day ? 0 : seconds;
            Visitors.RemoveAll(v => v.State == GrowthVisitorState.Leaving && v.Clock >= LeaveSeconds);
            TickGuide();
            TickLessons();
        }

        void TickDay(float seconds)
        {
            DaySecondsLeft -= seconds;
            arrivalClock += seconds;
            int star = Stars - 1;
            // Exhibition guests (GR-11) come at their own times and do not take ordinary visitors' places.
            if (DaySecondsLeft > BrowseSeconds && arrivalClock >= GrowthContent.ArrivalSeconds[star] && Visitors.Count(v => v.Type != GuestType) < VisitorsAtOnce)
            {
                arrivalClock = 0;
                Arrive();
            }
            if (DaySecondsLeft > BrowseSeconds) TickGuests();
            foreach (var v in Visitors.ToList())
            {
                v.Clock += seconds;
                if (v.State == GrowthVisitorState.Browsing && v.Clock >= v.BrowseFor) Decide(v);
                else if (v.State == GrowthVisitorState.Queued && (v.Patience -= seconds) <= 0) Leave(v, GrowthVisitOutcome.TiredOfWaiting);
            }
            if (DaySecondsLeft <= 0) CloseShop();
        }

        /// <summary>The type of a day's nth visitor, by GrowthContent's weights at the shop's stars: fixed by the day and n (the
        /// arrivals and R10.3's forecast both use it).</summary>
        VisitorTypeDef VisitorTypeOn(int day, int n)
        {
            int star = Stars - 1;
            var types = GrowthContent.Visitors.Where(t => t.Weights[star] > 0).ToList();
            int total = types.Sum(t => t.Weights[star]);
            int roll = (int)(Hash(day * 7919 + n * 104729) % (uint)total);
            foreach (var t in types) { if (roll < t.Weights[star]) return t; roll -= t.Weights[star]; }
            return types[0];
        }

        /// <summary>Visitors by GrowthContent's weights at today's stars, in a fixed sequence (no luck in what the player is owed).</summary>
        void Arrive()
        {
            // An empty shop window draws nobody in: passers-by walk on, and no reputation is lost for it.
            if (!Stock.Any()) { if (Today != null) Today.WalkedPast++; return; }
            int star = Stars - 1;
            var type = VisitorTypeOn(Day, visitorsToday);
            var v = new GrowthVisitor { Id = nextId++, Type = type.Id, Budget = type.Budgets[star], State = GrowthVisitorState.Browsing };
            // GR-20: what they wish for, fixed by the day and their place in it.
            v.Wish = WishFor(v.Type, v.Budget, visitorsToday);
            if (v.Wish != null && Today != null) Today.Wishes++;
            // GR-03: the visitor looks into the stocked containers one at a time, the most appealing first, then in an
            // order of their own; at most three. GR-11: a tank holding only creatures on exhibition display is passed by.
            // GR-20: the tanks holding their wish first (they spot it through the glass).
            v.Plan.AddRange(ShopContainers.Where(c => c.Occupants.Any(s => !IsOnDisplay(s)))
                .OrderByDescending(c => v.Wish != null && WishIn(v, c) != null ? 1 : 0)
                .ThenByDescending(Appeal).ThenBy(c => Hash(v.Id * 977 + StableHash(c.Id))).Take(MostBrowsed).Select(c => c.Id));
            v.ContainerId = v.Plan.Count > 0 ? v.Plan[0] : null;
            // GR-10: the look counts from the door, so a visitor bound for a far tank has the time to walk there.
            if (v.ContainerId != null) v.BrowseFor = GrowthSpaces.FirstLook(Container(v.ContainerId).Place);
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
                    var next = Container(v.ContainerId);
                    v.BrowseFor = c != null && next != null ? GrowthSpaces.NextLook(c.Place, next.Place) : NextBrowseSeconds;
                    v.Clock = 0;
                    return;
                }
                Leave(v, GrowthVisitOutcome.NothingAffordable);
                return;
            }
            // GR-06: the counter's queue has room for its level's length; a visitor who has chosen waits by the tank until there is room.
            if (Queue.Count() >= QueueLength) { v.Clock = v.BrowseFor - 1f; return; }
            if (!ReserveAll(v, chosen)) { Leave(v, GrowthVisitOutcome.NothingAffordable); return; }
            v.State = GrowthVisitorState.Queued;
            v.Patience = v.Type == GuestType ? GuestPatience : QueuePatience;
            v.Clock = 0;
        }

        /// <summary>What a visitor would take from one container: a pair (Collectors, 3 stars), one specimen, or nothing.</summary>
        public List<GrowthSpecimen> PickFrom(GrowthVisitor v, GrowthContainer c)
        {
            var free = c.Occupants.Where(s => s.ReservedBy == 0).ToList();
            // GR-11: an exhibition guest takes only what they came for.
            if (v.Type == GuestType)
            {
                var wanted = free.Where(s => s.Species == v.Wants && Price(s.Species) <= v.Budget).OrderBy(s => s.Id).FirstOrDefault();
                return wanted == null ? null : new List<GrowthSpecimen> { wanted };
            }
            // GR-20: what grants their wish, when this container holds it within their budget.
            var wish = WishIn(v, c);
            if (wish != null) return wish;
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
            if (outcome != GrowthVisitOutcome.Bought) MissWish(v);
            v.State = GrowthVisitorState.Leaving;
            v.Outcome = outcome;
            v.Clock = 0;
            if (v.Type == GuestType) { GuestLeaves(v, outcome); return; }
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
            else if (outcome == GrowthVisitOutcome.Slapped) SlapLeave(v);
            if (Today != null) Today.ReputationGained += Reputation - before;
        }

        /// <summary>
        /// Holding the mouse on the counter bags the front visitor's creature (GR.9 counter levels). GR-09: with Pip at the
        /// one till the player cannot serve; the second till (counter level 4) lets the player serve the next visitor beside Pip.
        /// </summary>
        public GrowthResult Serve(float seconds)
        {
            if (Phase != GrowthPhase.Day) return GrowthResult.Fail(GrowthFailure.NotDuringDay);
            if (Working("pip") && !SecondTill) { ServeProgress = 0; return GrowthResult.Fail(GrowthFailure.Busy, "Pip is at the till: the counter's second till (level 4) lets you serve beside Pip"); }
            PlayerAtTill = true;
            var v = PlayerVisitor;
            if (v == null) { ServeProgress = 0; return GrowthResult.Fail(GrowthFailure.NoVisitorWaiting); }
            ServeProgress += seconds;
            if (ServeProgress < ServeSeconds) return GrowthResult.Ok("bagging");
            ServeProgress = 0;
            return SellTo(v, "Sold ") ? GrowthResult.Ok("sold") : GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
        }

        /// <summary>Completes a visitor's purchase at the counter (the player's or Pip's): false when what they chose is gone.</summary>
        bool SellTo(GrowthVisitor v, string verb)
        {
            var bought = v.SpecimenIds.Select(id => HolderOf(id)?.Occupants.First(o => o.Id == id)).Where(s => s != null).ToList();
            if (bought.Count == 0) { Leave(v, GrowthVisitOutcome.TiredOfWaiting); return false; }
            int price = 0, starsBefore = Stars;
            foreach (var s in bought)
            {
                HolderOf(s.Id).Occupants.Remove(s);
                price += Price(s.Species);
                Reputation = Math.Min(GrowthContent.ReputationMax, Reputation + GrowthContent.RepPerSale);
                SalesTotal++;
                if (s.FromLine) LineSalesToday++;
                if (Today != null) { Today.Sales++; Today.ReputationGained += GrowthContent.RepPerSale; }
            }
            Credits += price;
            if (Today != null) Today.Revenue += price;
            v.State = GrowthVisitorState.Leaving;
            v.Outcome = GrowthVisitOutcome.Bought;
            v.Clock = 0;
            var first = bought[0];
            v.BoughtSpecies = first.Species; v.BoughtColour = first.Colour;
            // GR-20: a granted wish's tip and reputation point.
            int tip = GrantWish(v, bought, price);
            Raise(GrowthEventKind.Sale, verb + (bought.Count > 2 ? "a school of three " + Plural(first.Def.Name) : bought.Count > 1 ? "a pair of " + Plural(first.Def.Name) : first.Def.Name) + " for " + price + " credits" +
                (tip > 0 ? ", a wish granted: a tip of " + tip : ""), first.Species, price);
            if (tip > 0) Raise(GrowthEventKind.WishGranted, "A wish granted: " + v.Wish.Text + ", with a tip of " + tip + (tip == 1 ? " credit" : " credits"), first.Species, tip);
            if (v.Type == GuestType) GuestBuys(v);
            if (Stars > starsBefore) Raise(GrowthEventKind.StarUp, "The shop has " + Stars + " stars!", amount: Stars);
            CheckEra();
            return true;
        }

        public void StopServing() { ServeProgress = 0; PlayerAtTill = false; }

        /// <summary>GR-22: why the shop cannot close early now (None when it can): only while it is open.</summary>
        public GrowthFailure CloseEarlyCheck(out string why)
        {
            why = null;
            if (Phase != GrowthPhase.Day) { why = "The shop is closed: it opens from the counter at night"; return GrowthFailure.NotDuringDay; }
            return GrowthFailure.None;
        }

        /// <summary>GR-22: what closing now cuts short, for the counter to say before it closes (none when nothing is lost).</summary>
        public List<string> CloseEarlyCosts()
        {
            var costs = new List<string>();
            if (Phase != GrowthPhase.Day) return costs;
            int inside = Visitors.Count(v => v.State != GrowthVisitorState.Leaving && v.Type != GuestType);
            if (inside > 0) costs.Add(inside + (inside == 1 ? " visitor still in the shop leaves" : " visitors still in the shop leave") + " without buying");
            if (ExhibitionToday && ExhibitionOrders.Any(o => !o.Bought))
                costs.Add("The Grand Exhibition ends for today with " + ExhibitionOrders.Count(o => o.Bought) + " of " + ExhibitionPurchases + " guests served (nothing is lost; prepare it again any night)");
            if (LineToday) costs.Add("Today won't count as a full day on the line");
            return costs;
        }

        /// <summary>
        /// GR-22 (the user's request, 2026-10-29): the player closes the shop before its time, from the counter. It closes as
        /// at closing time (visitors still inside leave, their reservations released, no unfinished sale paid; the exhibition's
        /// day ends; the night begins), and a day closed early never counts as a full day on the line.
        /// </summary>
        public GrowthResult CloseEarly()
        {
            var failure = CloseEarlyCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            float left = DaySecondsLeft;
            if (Today != null) { Today.ClosedEarly = true; Today.SecondsLeft = left; }
            LineToday = false;
            CloseShop(left);
            return GrowthResult.Ok();
        }

        /// <summary>GR-32 (F44): how long a day lasts, with its units.</summary>
        public static string DayLengthWords => DurationWords(GrowthContent.DaySeconds);

        /// <summary>GR-32 (F44): a length of time in words: 5 minutes 30 seconds; 1 minute; 45 seconds.</summary>
        public static string DurationWords(float seconds)
        {
            int t = Math.Max(0, (int)Math.Ceiling(seconds)), m = t / 60, sec = t % 60;
            string minutes = m + (m == 1 ? " minute" : " minutes"), rest = sec + (sec == 1 ? " second" : " seconds");
            return m == 0 ? rest : sec == 0 ? minutes : minutes + " " + rest;
        }

        /// <summary>Minutes and seconds, as the day's clock shows them (5:30).</summary>
        public static string ClockText(float seconds) { int s = Math.Max(0, (int)Math.Ceiling(seconds)); return s / 60 + ":" + (s % 60).ToString("00"); }

        void CloseShop(float earlyLeft = 0)
        {
            foreach (var v in Visitors.Where(x => x.State != GrowthVisitorState.Leaving).ToList()) Leave(v, GrowthVisitOutcome.Closed);
            Phase = GrowthPhase.Night;
            DaySecondsLeft = 0;
            ServeProgress = 0;
            if (Today != null) { Today.StarsAfter = Stars; LastDay = Today; }
            Today = null;
            EndExhibitionDay();
            EndLineDay();
            Raise(GrowthEventKind.DayEnded, "Day " + Day + " is over" + (earlyLeft > 0 ? ": you closed the shop with " + ClockText(earlyLeft) + " to go" : ""));
            Day++;
            StartDutyPeriod();
            CheckEra();
        }

        // ---------------------------------------------------------------- the objective board (GR.12)

        public string EraGoal => ExhibitionDone ? "Free play: fill the journal" : GrowthContent.Eras[Era - 1].Goal;

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
            else
            {
                if (!UnitB)
                {
                    var unit = GrowthContent.Expansions.First(e => e.Id == "unit-b");
                    list.Add(("Open shop unit B (" + Math.Min(Credits, unit.Credits) + "/" + unit.Credits + " credits, at night)", false));
                }
                if (Era == 2)
                {
                    // GR-05: era 2's goal, breed your first hybrid and reach 3 stars; with a Large tank they open era 3.
                    var breeding = GrowthContent.Station("breeding").Levels[0];
                    list.Add(("Buy the breeding tank in the laboratory (" + breeding.Credits + " credits and " + breeding.Rp + " RP)", HasBreedingTank));
                    list.Add(("Breed your first hybrid", HasHybrid));
                    list.Add(("Buy a Large tank for a long place (" + GrowthContent.Container("large").Levels[0].Credits + " credits, at night)", ShopContainers.Any(c => c.Kind == "large")));
                    list.Add(("Reach 3 stars (reputation " + Reputation + "/" + GrowthContent.StarThresholds[2] + ")", Stars >= 3));
                    list.Add(("Get Ember: order its grower from the supplier catalog at the counter (" + GrowthContent.Culture("ember").Credits + " credits)", Owns("ember") || Orders.Contains("grower-ember")));
                    list.Add(("Research Spark at the pinboard (" + GrowthContent.Culture("spark").Rp + " research points)", Owns("spark")));
                }
                else if (Era == 3)
                {
                    // GR-07: era 3's goal, power the lab; with 4 stars and 12 species, powered pumps open era 4.
                    var large = ShopContainers.Where(c => c.Kind == "large").OrderBy(c => c.Free).FirstOrDefault();
                    list.Add(("Fill a Large tank (" + (large == null ? "buy one first" : large.Used + "/" + large.Spaces + " spaces") + ")", large != null && large.Free == 0));
                    list.Add(("Open the power room behind the laboratory (" + AnnexDef.Credits + " credits, at night)", Annex));
                    var generator = GrowthContent.Station("generator").Levels[0];
                    list.Add(("Buy the generator in the power room (" + generator.Credits + " credits and " + generator.Rp + " RP)", HasGenerator));
                    list.Add(("Pipe a grower's tap to a buffer (pumps and pipes on the power board)", Lines.Any()));
                    list.Add(("Power the pumps: electric pumps and the bubble turbine", PoweredPumps));
                    list.Add(("Discover 12 creatures (" + Math.Min(12, Discovered.Count) + "/12)", Discovered.Count >= 12));
                    list.Add(("Reach 4 stars (reputation " + Reputation + "/" + GrowthContent.StarThresholds[3] + ")", Stars >= 4));
                }
                else if (Era == 4)
                {
                    // GR-08: era 4's goal, a full day on the line and a large creature; with 5 stars they open era 5.
                    list.Add(("Let the incubator dose a recipe by itself: its level 4 and a buffer piped to its port", AutoBatches > 0));
                    list.Add(("Open the workshop east of the power room (" + WorkshopDef.Credits + " credits, at night)", Workshop));
                    list.Add(("Open shop unit C behind unit B (" + UnitCDef.Credits + " credits, at night)", UnitC));
                    var sorter = GrowthContent.Station("sorter").Levels[0];
                    list.Add(("Buy the sorter in the workshop (" + sorter.Credits + " credits) and lay its chute from the incubator", HasSorter && Chute != null));
                    list.Add(("Run a full day on the line: the auto-doser and the sorter on from opening to close, and " + LineDaySales + " sales of what it stocked (" +
                        (LineDays > 0 ? "done" : Phase == GrowthPhase.Day ? (LineToday ? "today " + Math.Min(LineSalesToday, LineDaySales) + "/" + LineDaySales : "not today: it stopped") : "starts when the shop opens") + ")", LineDays > 0));
                    list.Add(("Discover a large creature: a Reefback, made with Brine", Discovered.Any(d => GrowthContent.Get(d).Size == SizeClass.Large)));
                    list.Add(("Reach 5 stars (reputation " + Reputation + "/" + GrowthContent.StarThresholds[4] + ")", Stars >= 5));
                }
                // GR-11: era 5's goal, the Grand Exhibition; then free play.
                else ExhibitionObjectives(list);
            }
            return list;
        }

        // ---------------------------------------------------------------- events and helpers

        void Raise(GrowthEventKind kind, string text, string species = null, int amount = 0) =>
            Events.Add(new GrowthEvent { Kind = kind, Text = text, Species = species, Amount = amount });

        public List<GrowthEvent> TakeEvents() { var e = new List<GrowthEvent>(Events); Events.Clear(); return e; }

        public static string Name(string culture) => GrowthContent.Cultures[GrowthContent.CultureIndex(culture)].Name;

        /// <summary>A creature's name for several of it: Pondlets, Bubblebellies, Zephyrays.</summary>
        public static string Plural(string name) =>
            name.EndsWith("y") && name.Length > 1 && "aeiou".IndexOf(name[name.Length - 2]) < 0 ? name.Substring(0, name.Length - 1) + "ies" :
            name.EndsWith("s") || name.EndsWith("sh") || name.EndsWith("ch") || name.EndsWith("x") ? name + "es" : name + "s";

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

        public GrowthSave Snapshot()
        {
            var save = SnapshotCore();
            SavePower(save);
            SaveLine(save);
            SaveHelpers(save);
            SaveExhibition(save);
            SaveGuide(save);
            SaveDecor(save);
            SaveLessons(save);
            return save;
        }

        GrowthSave SnapshotCore() => new GrowthSave
        {
            credits = Credits, reputation = Reputation, rp = Rp, era = Era, day = Day, sales = SalesTotal, refusals = RefusalsTotal, nextId = nextId,
            phase = Phase.ToString(), daySecondsLeft = DaySecondsLeft, unitB = UnitB, unitC = UnitC, hall = Hall,
            cultures = Cultures.ToArray(), growerUnits = Cultures.Select(c => GrowerUnits[c]).ToArray(),
            stations = Levels.Keys.ToArray(), levels = Levels.Values.ToArray(),
            held = Held.ToArray(), chamber = Chamber.ToArray(), incubating = Incubating, incubationLeft = IncubationLeft, pending = Pending ?? "",
            hatchling = Hatchlings.Select(GrowthSave.Specimen.Of).ToArray(), incubatorTint = IncubatorTint ?? "",
            carried = Carried.Select(GrowthSave.Specimen.Of).ToArray(),
            containers = Containers.Select(c => new GrowthSave.Box { id = c.Id, kind = c.Kind, place = c.Place, level = c.Level, water = c.Water.ToString(), occupants = c.Occupants.Select(GrowthSave.Specimen.Of).ToArray() }).ToArray(),
            discovered = Discovered.ToArray(),
            vialCultures = Vials.Keys.ToArray(), vialDoses = Vials.Values.ToArray(), orders = Orders.ToArray(),
            mixLog = MixLog.Select(m => new GrowthSave.Mix { doses = m.Doses, species = m.Species, exact = m.Exact, wasNew = m.WasNew, distanceToNew = m.DistanceToNew, count = m.Count }).ToArray(),
            pinnedRecipes = PinnedRecipes.ToArray(), autoOpenAt = AutoOpenAt,
            pendingMix = PendingMix == null ? new GrowthSave.Mix[0] : new[] { new GrowthSave.Mix { doses = PendingMix.Doses, species = PendingMix.Species, exact = PendingMix.Exact, wasNew = PendingMix.WasNew, distanceToNew = PendingMix.DistanceToNew, count = PendingMix.Count } },
            parents = Parents.Select(GrowthSave.Specimen.Of).ToArray(),
            offspring = Offspring == null ? new GrowthSave.Specimen[0] : new[] { GrowthSave.Specimen.Of(Offspring) },
            breedingLeft = BreedingLeft, tint = Tint ?? "", journal = Journal.ToArray(),
            secondParents = Pairs[1].Parents.Select(GrowthSave.Specimen.Of).ToArray(),
            secondOffspring = Pairs[1].Offspring == null ? new GrowthSave.Specimen[0] : new[] { GrowthSave.Specimen.Of(Pairs[1].Offspring) }, secondLeft = Pairs[1].Left,
            pairLog = PairLog.Select(p => new GrowthSave.Pair { a = p.A, b = p.B, species = p.Species, wasNew = p.WasNew, count = p.Count }).ToArray(),
        };

        /// <summary>Restores a saved shop; visitors and reservations are not saved (a day resumes empty). Null and a reason when the save is not one.</summary>
        public static GrowthShop Restore(GrowthSave s, out string problem)
        {
            problem = null;
            if (s == null) { problem = "No save"; return null; }
            // Version 1 (GR-02) had no water; its containers keep fresh water. Version 2 (GR-03) had no vials or orders.
            // Version 3 (GR-04) had no breeding tank, pair log or colours: its journal is its discoveries, all Classic.
            // Version 4 (GR-05) had one hatchling, one pair, no bench or vat level and no incubator tint.
            if (s.version < 1 || s.version > GrowthSave.CurrentVersion) { problem = "Unknown GR save version " + s.version; return null; }
            bool Known(string species) => GrowthContent.Get(species) != null;
            var all = (s.hatchling ?? new GrowthSave.Specimen[0]).Concat(s.carried ?? new GrowthSave.Specimen[0]).Concat((s.containers ?? new GrowthSave.Box[0]).SelectMany(b => b.occupants ?? new GrowthSave.Specimen[0]))
                .Concat(s.parents ?? new GrowthSave.Specimen[0]).Concat(s.offspring ?? new GrowthSave.Specimen[0])
                .Concat(s.secondParents ?? new GrowthSave.Specimen[0]).Concat(s.secondOffspring ?? new GrowthSave.Specimen[0]).ToList();
            if (all.Any(x => !Known(x.species))) { problem = "Unknown species in the save"; return null; }
            if (all.Select(x => x.id).Distinct().Count() != all.Count) { problem = "A creature is saved twice"; return null; }
            if ((s.discovered ?? new string[0]).Any(d => !Known(d))) { problem = "Unknown discovery in the save"; return null; }
            if (!Enum.TryParse<GrowthPhase>(s.phase, out var phase)) { problem = "Unknown phase " + s.phase; return null; }
            var shop = new GrowthShop();
            shop.Containers.Clear(); shop.Cultures.Clear(); shop.GrowerUnits.Clear();
            shop.Credits = s.credits; shop.Reputation = s.reputation; shop.Rp = s.rp; shop.Era = Math.Max(1, s.era); shop.Day = Math.Max(1, s.day);
            shop.SalesTotal = s.sales; shop.RefusalsTotal = s.refusals; shop.Phase = phase; shop.DaySecondsLeft = s.daySecondsLeft; shop.UnitB = s.unitB; shop.UnitC = s.unitC; shop.Hall = s.hall;
            for (int i = 0; i < (s.cultures ?? new string[0]).Length; i++)
            {
                if (GrowthContent.CultureIndex(s.cultures[i]) < 0) { problem = "Unknown culture " + s.cultures[i]; return null; }
                shop.Cultures.Add(s.cultures[i]); shop.GrowerUnits[s.cultures[i]] = s.growerUnits != null && i < s.growerUnits.Length ? s.growerUnits[i] : 0;
            }
            for (int i = 0; i < (s.stations ?? new string[0]).Length; i++)
            {
                if (!UpgradeableStations.Contains(s.stations[i]) || s.levels[i] < 1 || s.levels[i] > GrowthContent.Station(s.stations[i]).Levels.Length || WaitsFor(s.stations[i], s.levels[i]) != null)
                { problem = "Bad station level " + s.stations[i]; return null; }
                shop.Levels[s.stations[i]] = s.levels[i];
            }
            shop.Held.AddRange(s.held ?? new string[0]); shop.Chamber.AddRange(s.chamber ?? new string[0]);
            // A dose may be of a culture not owned (from a starter vial, GR-04), but it must be a culture.
            if (shop.Held.Concat(shop.Chamber).Any(c => GrowthContent.CultureIndex(c) < 0)) { problem = "A dose of an unknown culture"; return null; }
            shop.Incubating = s.incubating; shop.IncubationLeft = s.incubationLeft; shop.Pending = string.IsNullOrEmpty(s.pending) ? null : s.pending;
            if (shop.Incubating && (shop.Pending == null || !Known(shop.Pending))) { problem = "An incubation without its creature"; return null; }
            shop.Hatchlings.AddRange((s.hatchling ?? new GrowthSave.Specimen[0]).Select(x => x.ToSpecimen()));
            if (shop.Hatchlings.Count > shop.Clutch) { problem = "More hatchlings than the incubator's clutch"; return null; }
            shop.IncubatorTint = string.IsNullOrEmpty(s.incubatorTint) ? null : s.incubatorTint;
            if (shop.IncubatorTint != null && (!TintCultures.Contains(shop.IncubatorTint) || !shop.IncubatorTintPort)) { problem = "Unknown incubator tint " + s.incubatorTint; return null; }
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
            // The vat came at level 1 with Mist before GR-06 kept its level.
            if (shop.Owns("mist") && shop.Level("vat") == 0) shop.Levels["vat"] = 1;
            shop.Parents.AddRange((s.parents ?? new GrowthSave.Specimen[0]).Select(x => x.ToSpecimen()));
            shop.Offspring = s.offspring != null && s.offspring.Length > 0 ? s.offspring[0].ToSpecimen() : null;
            shop.Pairs[1].Parents.AddRange((s.secondParents ?? new GrowthSave.Specimen[0]).Select(x => x.ToSpecimen()));
            shop.Pairs[1].Offspring = s.secondOffspring != null && s.secondOffspring.Length > 0 ? s.secondOffspring[0].ToSpecimen() : null;
            if ((shop.AllParents.Any() || shop.AllOffspring.Any()) && !shop.HasBreedingTank) { problem = "Breeding without a breeding tank"; return null; }
            if (shop.Pairs.Any(p => p.Parents.Count > PairSize) || (shop.Pairs[1].Parents.Count > 0 || shop.Pairs[1].Offspring != null) && shop.PairPlaces < 2)
            { problem = "More parents than the breeding tank takes"; return null; }
            if (shop.AllParents.Any(p => p.Def.Size == SizeClass.Large) && !shop.LargeParents) { problem = "A Large parent before the breeding tank's level 2"; return null; }
            shop.Tint = string.IsNullOrEmpty(s.tint) ? null : s.tint;
            if (shop.Tint != null && !TintCultures.Contains(shop.Tint)) { problem = "Unknown tint " + s.tint; return null; }
            float Left(GrowthBreedingPair p, float saved) => !p.Breeding ? 0 : saved > 0 ? Math.Min(saved, shop.BreedSeconds) : shop.BreedSeconds;
            shop.Pairs[0].Left = Left(shop.Pairs[0], s.breedingLeft);
            shop.Pairs[1].Left = Left(shop.Pairs[1], s.secondLeft);
            if (s.version < 4) shop.Journal.AddRange(shop.Discovered.Select(d => JournalKey(d, Colour.Classic)));
            else
                foreach (var key in s.journal ?? new string[0])
                {
                    var parts = key.Split('/');
                    if (parts.Length != 2 || !Known(parts[0]) || !Enum.TryParse<Colour>(parts[1], out var colour) || !Enum.IsDefined(typeof(Colour), colour)) { problem = "Unknown journal entry " + key; return null; }
                    if (!shop.Journal.Contains(key)) shop.Journal.Add(key);
                }
            foreach (var p in s.pairLog ?? new GrowthSave.Pair[0])
            {
                if (!Known(p.a) || !Known(p.b) || !Known(p.species)) { problem = "Unknown species in the pair log"; return null; }
                shop.PairLog.Add(new GrowthPairEntry { A = p.a, B = p.b, Species = p.species, WasNew = p.wasNew, Count = Math.Max(1, p.count) });
            }
            var vialCultures = s.vialCultures ?? new string[0];
            for (int i = 0; i < vialCultures.Length; i++)
            {
                int doses = s.vialDoses != null && i < s.vialDoses.Length ? s.vialDoses[i] : 0;
                if (GrowthContent.CultureIndex(vialCultures[i]) < 0 || doses < 1) { problem = "A vial that is not one"; return null; }
                shop.Vials[vialCultures[i]] = doses;
            }
            foreach (var o in s.orders ?? new string[0])
            {
                if (GrowthContent.CatalogItem(o) == null) { problem = "Unknown order " + o; return null; }
                shop.Orders.Add(o);
            }
            shop.MixLog.AddRange((s.mixLog ?? new GrowthSave.Mix[0]).Select(m => new GrowthMixEntry { Doses = m.doses, Species = m.species, Exact = m.exact, WasNew = m.wasNew, DistanceToNew = m.distanceToNew, Count = Math.Max(1, m.count) }));
            shop.AutoOpenAt = Array.IndexOf(AutoOpenChoices, s.autoOpenAt) >= 0 ? s.autoOpenAt : 0;
            foreach (var b in s.containers ?? new GrowthSave.Box[0]) { var c = shop.Container(b.id); if (c != null && Array.IndexOf(TankRules, b.rule ?? "") >= 0) c.Rule = b.rule ?? ""; }
            if (s.pinnedRecipes != null) shop.PinnedRecipes.AddRange(s.pinnedRecipes.Where(r => GrowthContent.Get(r) != null));
            // GR-29: the mix in the incubator, logged when it hatches (version 13 and older logged it when it started).
            if (s.incubating && s.pendingMix != null && s.pendingMix.Length > 0)
            {
                var pm = s.pendingMix[0];
                shop.PendingMix = new GrowthMixEntry { Doses = pm.doses, Species = pm.species, Exact = pm.exact, WasNew = pm.wasNew, DistanceToNew = pm.distanceToNew, Count = Math.Max(1, pm.count) };
            }
            // Version 5 and older had no annex, power or pipes; version 6 and older no workshop, sorter, targets or analyzer.
            problem = RestorePower(shop, s) ?? RestoreLine(shop, s) ?? RestoreHelpers(shop, s) ?? RestoreSpaces(shop) ?? RestoreExhibition(shop, s) ?? RestoreGuide(shop, s) ?? RestoreDecor(shop, s) ?? RestoreLessons(shop, s);
            if (problem != null) return null;
            shop.nextId = Math.Max(shop.nextId, Math.Max(Math.Max(s.nextId, all.Count == 0 ? 1 : all.Max(x => x.id) + 1), shop.Pipes.Count == 0 ? 1 : shop.Pipes.Max(p => p.Id) + 1));
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
        /// <summary>12: GR-19 (decor: the pieces owned, and the one on each shop tank);
        /// 11: GR-14 (the guided first night: on, done or skipped; older saves have none);
        /// 10: GR-11 (the Grand Exhibition: prepared, under way or held, its six purchases, the crowns on display, its results);
        /// 9: GR-10 (shop unit C and the exhibition hall finish);
        /// 8: GR-09 (the helpers: their levels, duty and wages paid, the step each is on and what they carry; Moss's jobs);
        /// 7: GR-08 (the workshop, racks, the sorter and its chute, stock targets, the line's days, the analyzer's hint);
        /// 6: GR-07 (the lab annex, buffers, pipes, the power board's order and the auto-doser);
        /// 5: GR-06 (clutches, the incubator's tint, the breeding tank's second pair, the bench and vat levels);
        /// 4: GR-05 (the breeding tank, the pair log, the journal's colours); 3: GR-04 (vials and supplier orders);
        /// 2: GR-03 (container water). 13: R09.4 (the lessons met and learned, the notes they read, the lesson now). Older saves load.</summary>
        public const int CurrentVersion = 14;
        public int version = CurrentVersion;
        public int credits, reputation, rp, era, day, sales, refusals, nextId;
        public string phase;
        public float daySecondsLeft;
        public bool unitB, unitC, hall;
        public bool exhibitionPrepared, exhibitionToday, exhibitionDone;
        public int exhibitionAttempts;
        public string exhibitionFailure;
        public string[] exhibitionSpecies, exhibitionMissed;
        public bool[] exhibitionBought;
        public int[] exhibitionDisplay;
        public int resultDay, resultSpecies, resultCrowns, resultJournal, resultRevenue, resultSales, resultAttempts;
        public string[] resultGuests, resultCrowned;
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
        /// <summary>GR-29: the mix in the incubator (none or one), logged when it hatches.</summary>
        public Mix[] pendingMix;
        /// <summary>GR-31: the recipes pinned to the incubator's card.</summary>
        public string[] pinnedRecipes;
        /// <summary>GR-31: the stock the shop opens by itself at (0: off).</summary>
        public int autoOpenAt;
        public string[] vialCultures, orders;
        public int[] vialDoses;
        public Specimen[] parents, offspring, secondParents, secondOffspring;
        public float breedingLeft, secondLeft;
        public string tint, incubatorTint;
        public string[] journal;
        public Pair[] pairLog;
        public bool annex;
        public Buffer[] buffers;
        public Pipe[] pipes;
        public string[] powerOrder;
        public string autoRecipe;
        public bool autoOn, autoBatch;
        public float autoProgress;
        public int autoBatches;
        public bool workshop, autoByTargets, lineToday;
        public string[] targetSpecies;
        public int[] targetCounts;
        public int lineDays, lineSalesToday;
        public float sorterProgress;
        public string hintKind, hintA, hintB, hintSpecies;
        public int[] hintRecipe;
        public int hintDay;
        public Helper[] staff;
        public string mossRecipe, mossPairA, mossPairB, mossTask;
        public bool mossBatch;
        public int mossBatches;
        /// <summary>Comfort settings kept with the slot: peek on or off, reduced motion.</summary>
        public bool peekOff, reducedMotion;
        /// <summary>GR-16 wave 5: the sounds and the machines' hum, 0 full, 1 half, 2 off (an older save, without them, is full).</summary>
        public int soundLevel, humLevel;
        /// <summary>GR-18 wave 4 (§6.7): the silly moments' sounds (0 full, 1 half, 2 off), how many of them there are (0 every one,
        /// 1 fewer and shorter, 2 none but the player's slap), and the slap's key ("" for F; an older save has none of them).</summary>
        public int humourLevel, gagLevel;
        public string slapKey;
        /// <summary>GR-14: the guided first night ("" for none, "on", "done" or "skipped").</summary>
        public string guide;
        /// <summary>GR-19: the decor delivered (each piece once; the one on a tank is its Box's decor).</summary>
        public string[] decorOwned;
        /// <summary>R09.4: whether the lessons were kept (version 13; an older save learns what it has used), the lessons met and
        /// learned, what the shop has noted, and the lesson whose steps run ("" for none).</summary>
        public bool lessonsKept;
        public string[] lessonsMet, lessonsLearned, noted;
        public string lessonNow;
        public float sensitivity = 1;

        [Serializable] public sealed class Specimen
        {
            public int id;
            public string species, colour;
            public bool fromLine;
            public static Specimen Of(GrowthSpecimen s) => new Specimen { id = s.Id, species = s.Species, colour = s.Colour.ToString(), fromLine = s.FromLine };
            public GrowthSpecimen ToSpecimen() => new GrowthSpecimen { Id = id, Species = species, Colour = Enum.TryParse<Colour>(colour, out var c) ? c : Colour.Classic, FromLine = fromLine };
        }

        /// <summary>A helper: level, duty and paid period, the step they are on (a walk from a spot to a target and what it is about), what they carry.</summary>
        [Serializable] public sealed class Helper
        {
            public string id, step, place, dose, status;
            public int level, paidPeriod, specimen, nextJob;
            public bool onDuty;
            public float progress, stepSeconds, spotX, spotZ, targetX, targetZ;
            public Specimen[] hands;
        }

        [Serializable] public sealed class Box
        {
            public string id, kind, place, water, decor, rule;
            public int level;
            public Specimen[] occupants;
        }

        [Serializable] public sealed class Pair
        {
            public string a, b, species;
            public bool wasNew;
            public int count;
        }

        [Serializable] public sealed class Buffer
        {
            public int place;
            public string culture;
            public float units;
        }

        /// <summary>A pipe: its ends and its cells on the lab grid as column, row pairs.</summary>
        [Serializable] public sealed class Pipe
        {
            public int id, paid;
            public string from, to;
            public int[] cells;
            public float length;
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
