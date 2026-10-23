using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-02 (GR.18): the greybox ruleset of the grow-from-nothing redesign, the first era and the step into the second.
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

    public sealed class GrowthContainer
    {
        public string Id, Kind, Place;
        public int Level = 1;
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
        public int SpecimenId;
        /// <summary>The container the visitor browses and, once decided, holds its specimen.</summary>
        public string ContainerId;
        public float Clock, Patience;
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

    public enum GrowthEventKind { Collected, Hatched, Discovery, Sale, Refusal, Impatient, StarUp, EraUp, DayStarted, DayEnded, Bought, Upgraded }

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
        public const float BrowseSeconds = 6f, QueuePatience = 40f, LeaveSeconds = 8f, SchoolBudgetBonus = 1.15f;
        public static readonly string[] UnitAPlaces = { "a1", "a2", "a3", "a4" }, UnitBPlaces = { "b1", "b2", "b3", "b4" };
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
            if (fits == null) return GrowthResult.Fail(GrowthFailure.DoesNotFit, c.Free + " of " + c.Spaces + " spaces free");
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
        public GrowthResult BuyTank(string place)
        {
            if (Phase != GrowthPhase.Night) return GrowthResult.Fail(GrowthFailure.NotAtNight);
            if (!OwnedPlaces.Contains(place)) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, place);
            if (ContainerAt(place) != null) return GrowthResult.Fail(GrowthFailure.PlaceTaken, place);
            int cost = GrowthContent.Container("starter").Levels[0].Credits;
            if (Credits < cost) return GrowthResult.Fail(GrowthFailure.NotEnoughCredits, cost + " credits");
            Credits -= cost;
            Containers.Add(new GrowthContainer { Id = "tank-" + nextId++, Kind = "starter", Place = place });
            Raise(GrowthEventKind.Bought, "Starter tank placed", amount: cost);
            CheckEra();
            return GrowthResult.Ok();
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
                if (v.State == GrowthVisitorState.Browsing && v.Clock >= BrowseSeconds) Decide(v);
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
            var shop = ShopContainers.ToList();
            var v = new GrowthVisitor
            {
                Id = nextId++, Type = type.Id, Budget = type.Budgets[star], State = GrowthVisitorState.Browsing,
                ContainerId = shop.Count == 0 ? null : shop[(int)(Hash(nextId * 31 + Day) % (uint)shop.Count)].Id,
            };
            visitorsToday++;
            if (Today != null) Today.Visitors++;
            Visitors.Add(v);
        }

        /// <summary>
        /// A visitor picks the creature in stock they like best within budget: the dearer the better, a school's species
        /// is worth 15% more budget (GR.5), and a small fixed variety per visitor; then reserves it and queues.
        /// </summary>
        void Decide(GrowthVisitor v)
        {
            GrowthSpecimen best = null; GrowthContainer holder = null; double bestScore = double.MinValue;
            foreach (var c in ShopContainers)
                foreach (var s in c.Occupants)
                {
                    if (s.ReservedBy != 0) continue;
                    bool school = c.InSchool(s.Species);
                    int price = Price(s.Species);
                    if (price > v.Budget * (school ? SchoolBudgetBonus : 1)) continue;
                    double score = price + (school ? 3 : 0) + Hash(v.Id * 131 + s.Id) % 3 + (c.Id == v.ContainerId ? 0.5 : 0);
                    if (score > bestScore) { best = s; holder = c; bestScore = score; }
                }
            if (best == null) { Leave(v, GrowthVisitOutcome.NothingAffordable); return; }
            best.ReservedBy = v.Id;
            v.SpecimenId = best.Id;
            v.ContainerId = holder.Id;
            v.State = GrowthVisitorState.Queued;
            v.Patience = QueuePatience;
            v.Clock = 0;
        }

        void Leave(GrowthVisitor v, GrowthVisitOutcome outcome)
        {
            if (v.SpecimenId != 0)
            {
                var s = Stock.FirstOrDefault(x => x.Id == v.SpecimenId);
                if (s != null && s.ReservedBy == v.Id) s.ReservedBy = 0;
            }
            if (outcome != GrowthVisitOutcome.Bought) v.SpecimenId = 0;
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
            var holder = HolderOf(v.SpecimenId);
            var s = holder?.Occupants.First(o => o.Id == v.SpecimenId);
            if (holder == null) { Leave(v, GrowthVisitOutcome.TiredOfWaiting); return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen); }
            holder.Occupants.Remove(s);
            int price = Price(s.Species), starsBefore = Stars;
            Credits += price;
            Reputation = Math.Min(GrowthContent.ReputationMax, Reputation + GrowthContent.RepPerSale);
            SalesTotal++;
            if (Today != null) { Today.Sales++; Today.Revenue += price; Today.ReputationGained += GrowthContent.RepPerSale; }
            v.State = GrowthVisitorState.Leaving;
            v.Outcome = GrowthVisitOutcome.Bought;
            v.Clock = 0;
            Raise(GrowthEventKind.Sale, "Sold " + s.Def.Name + " for " + price + " credits", s.Species, price);
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
            else list.Add(("The greybox ends here: breeding and the rest of era 2 come with GR-05", true));
            return list;
        }

        // ---------------------------------------------------------------- events and helpers

        void Raise(GrowthEventKind kind, string text, string species = null, int amount = 0) =>
            Events.Add(new GrowthEvent { Kind = kind, Text = text, Species = species, Amount = amount });

        public List<GrowthEvent> TakeEvents() { var e = new List<GrowthEvent>(Events); Events.Clear(); return e; }

        public static string Name(string culture) => GrowthContent.Cultures[GrowthContent.CultureIndex(culture)].Name;

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
            containers = Containers.Select(c => new GrowthSave.Box { id = c.Id, kind = c.Kind, place = c.Place, level = c.Level, occupants = c.Occupants.Select(GrowthSave.Specimen.Of).ToArray() }).ToArray(),
            discovered = Discovered.ToArray(),
            mixLog = MixLog.Select(m => new GrowthSave.Mix { doses = m.Doses, species = m.Species, exact = m.Exact, wasNew = m.WasNew, distanceToNew = m.DistanceToNew, count = m.Count }).ToArray(),
        };

        /// <summary>Restores a saved shop; visitors and reservations are not saved (a day resumes empty). Null and a reason when the save is not one.</summary>
        public static GrowthShop Restore(GrowthSave s, out string problem)
        {
            problem = null;
            if (s == null) { problem = "No save"; return null; }
            if (s.version != GrowthSave.CurrentVersion) { problem = "Unknown GR save version " + s.version; return null; }
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
                c.Occupants.AddRange((b.occupants ?? new GrowthSave.Specimen[0]).Select(x => x.ToSpecimen()));
                if (c.Used > c.Spaces) { problem = "A container holds more than it fits"; return null; }
                shop.Containers.Add(c);
            }
            shop.Discovered.AddRange(s.discovered ?? new string[0]);
            shop.MixLog.AddRange((s.mixLog ?? new GrowthSave.Mix[0]).Select(m => new GrowthMixEntry { Doses = m.doses, Species = m.species, Exact = m.exact, WasNew = m.wasNew, DistanceToNew = m.distanceToNew, Count = Math.Max(1, m.count) }));
            shop.nextId = Math.Max(s.nextId, all.Count == 0 ? 1 : all.Max(x => x.id) + 1);
            if (phase == GrowthPhase.Day) { shop.Today = new GrowthDaySummary { Day = shop.Day, StarsBefore = shop.Stars }; shop.arrivalClock = 0; }
            return shop;
        }
    }

    /// <summary>The GR save line (GR.15): its own file and version; a classic save is never read or written by it.</summary>
    [Serializable]
    public sealed class GrowthSave
    {
        public const int CurrentVersion = 1;
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
            public string id, kind, place;
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
