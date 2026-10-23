using System;
using System.Collections.Generic;

namespace EvolutionShop.Growth
{
    // GR-01 (GR.4-GR.10): the grow-from-nothing redesign's content and tuning tables, as data. Pure and engine-free, read
    // by the rules (GrowthRules), the pacing simulator (PacingSimulator) and, from GR-02, the new ruleset. Every number
    // here is a first-pass default: change it with the pacing report or playtest evidence and record why in GR.

    /// <summary>Spaces a creature of this size takes in a container (GR.5).</summary>
    public enum SizeClass { Small = 1, Medium = 2, Large = 4 }
    public enum SpeciesKind { Founder, Hybrid, Crown }
    public enum Temperament { Calm, Lively }
    public enum Water { Fresh, Brine, Warm }
    public enum CultureSource { StarterGrower, CatalogGrower, Research, Vat }
    /// <summary>AD.3's four colours, in the order an untinted offspring inherits the higher one (GR.7.2).</summary>
    public enum Colour { Classic, Sunny, Deep, Shimmer }

    public sealed class CultureDef
    {
        public string Id, Name, Hex;
        public CultureSource Source;
        public int Era, Stars, Credits, Rp;
    }

    public sealed class FamilyDef
    {
        public string Id, Name;
        public SizeClass Size;
        public string LeadCulture;
        public int Era;
    }

    public sealed class SpeciesDef
    {
        public string Id, Name, Family;
        public SpeciesKind Kind;
        /// <summary>Founders: doses of each culture, in GrowthContent.Cultures order. Null otherwise.</summary>
        public int[] Recipe;
        /// <summary>Crowns: the special pair that makes them. Null otherwise.</summary>
        public string PairA, PairB;
        /// <summary>Hidden breeding value (GR.7.2). Crowns have one as parents but are never an averaged result.</summary>
        public int BreedingValue;
        public int Price;
        public Temperament Temperament;
        public Water Water;
        public SizeClass Size => GrowthContent.Family(Family).Size;
        public int Doses { get { int n = 0; if (Recipe != null) foreach (int d in Recipe) n += d; return n; } }
    }

    /// <summary>One level of a station or container: its cost, its gate and what it gives (GR.9).</summary>
    public sealed class LevelDef
    {
        public int Level, Credits, Rp, Stars, Era;
        public string Change;
        /// <summary>The level's numbers by name (capacity, seconds, clutch...), read by the rules and the simulator.</summary>
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        public float Stat(string name, float fallback = 0) => Stats.TryGetValue(name, out float v) ? v : fallback;
    }

    public sealed class StationDef
    {
        public string Id, Name;
        public LevelDef[] Levels;
    }

    public sealed class ContainerDef
    {
        public string Id, Name;
        public SizeClass Largest;
        public float Width, Depth;
        public bool Lab;
        /// <summary>Level 1 is buying the container; later levels are its upgrades.</summary>
        public LevelDef[] Levels;
    }

    public sealed class ExpansionDef
    {
        public string Id, Name, Room;
        public int Credits, Stars, Era, ContainerPlaces, VisitorsAtOnce;
        public float Width, Depth, ExtraWalkSeconds;
    }

    public sealed class HelperDef
    {
        public string Id, Name, Job, Gate;
        public int Stars, Fee;
        public int[] Wages, TrainCosts;
    }

    public sealed class EraDef
    {
        public int Number;
        public string Name, Gate, Goal;
        public int Stars;
        /// <summary>Target window for reaching this era, in minutes of play (GR.4).</summary>
        public int TargetFrom, TargetTo;
    }

    public sealed class VisitorTypeDef
    {
        public string Id, Name;
        public int Stars;
        /// <summary>Relative arrival weight and budget at each star level (index 0 = 1 star).</summary>
        public int[] Weights, Budgets;
    }

    public static class GrowthContent
    {
        public const int SpeciesCount = 72, ColourCount = 4, JournalEntries = SpeciesCount * ColourCount;
        public const float DaySeconds = 330f;
        /// <summary>Credits a new shop starts with: a first upgrade is a sale or two away (GR.3).</summary>
        public const int StartingCredits = 10;

        // ---------------------------------------------------------------- cultures (GR.6)

        public static readonly CultureDef[] Cultures =
        {
            new CultureDef { Id = "bloom", Name = "Bloom", Hex = "#FF6F61", Source = CultureSource.StarterGrower, Era = 1 },
            new CultureDef { Id = "flow", Name = "Flow", Hex = "#3FC5F0", Source = CultureSource.StarterGrower, Era = 1 },
            new CultureDef { Id = "ember", Name = "Ember", Hex = "#FF9A3C", Source = CultureSource.CatalogGrower, Era = 2, Stars = 2, Credits = 150 },
            new CultureDef { Id = "spark", Name = "Spark", Hex = "#FFD447", Source = CultureSource.Research, Era = 2, Stars = 2, Rp = 10 },
            new CultureDef { Id = "mist", Name = "Mist", Hex = "#C9B6FF", Source = CultureSource.Vat, Era = 3, Stars = 3, Credits = 600, Rp = 40 },
            new CultureDef { Id = "brine", Name = "Brine", Hex = "#4FD1A5", Source = CultureSource.CatalogGrower, Era = 4, Stars = 4, Credits = 1500 },
            new CultureDef { Id = "dusk", Name = "Dusk", Hex = "#5B4BDB", Source = CultureSource.Research, Era = 5, Stars = 5, Rp = 150 },
        };

        public static int CultureIndex(string id)
        {
            for (int i = 0; i < Cultures.Length; i++) if (Cultures[i].Id == id) return i;
            return -1;
        }

        /// <summary>A recipe by culture: R(bloom: 1, flow: 1).</summary>
        static int[] R(int bloom = 0, int flow = 0, int ember = 0, int spark = 0, int mist = 0, int brine = 0, int dusk = 0)
            => new[] { bloom, flow, ember, spark, mist, brine, dusk };

        // ---------------------------------------------------------------- families and species (GR.7)

        public static readonly FamilyDef[] Families =
        {
            new FamilyDef { Id = "brookkin", Name = "Brookkin", Size = SizeClass.Small, LeadCulture = "bloom", Era = 1 },
            new FamilyDef { Id = "sparkline", Name = "Sparkline", Size = SizeClass.Small, LeadCulture = "spark", Era = 2 },
            new FamilyDef { Id = "cinderling", Name = "Cinderling", Size = SizeClass.Medium, LeadCulture = "ember", Era = 2 },
            new FamilyDef { Id = "airfin", Name = "Airfin", Size = SizeClass.Medium, LeadCulture = "mist", Era = 3 },
            new FamilyDef { Id = "dewling", Name = "Dewling", Size = SizeClass.Small, LeadCulture = "mist", Era = 3 },
            new FamilyDef { Id = "reefback", Name = "Reefback", Size = SizeClass.Large, LeadCulture = "brine", Era = 4 },
            new FamilyDef { Id = "saltscale", Name = "Saltscale", Size = SizeClass.Medium, LeadCulture = "brine", Era = 4 },
            new FamilyDef { Id = "gloamer", Name = "Gloamer", Size = SizeClass.Large, LeadCulture = "dusk", Era = 5 },
            new FamilyDef { Id = "cometail", Name = "Cometail", Size = SizeClass.Large, LeadCulture = "spark", Era = 5 },
        };

        public static FamilyDef Family(string id)
        {
            foreach (var f in Families) if (f.Id == id) return f;
            throw new ArgumentException("Unknown family " + id);
        }

        static SpeciesDef Founder(string id, string name, string family, int[] recipe, int bv, int price, Temperament t, Water w)
            => new SpeciesDef { Id = id, Name = name, Family = family, Kind = SpeciesKind.Founder, Recipe = recipe, BreedingValue = bv, Price = price, Temperament = t, Water = w };
        static SpeciesDef Hybrid(string id, string name, string family, int bv, int price, Temperament t, Water w)
            => new SpeciesDef { Id = id, Name = name, Family = family, Kind = SpeciesKind.Hybrid, BreedingValue = bv, Price = price, Temperament = t, Water = w };
        static SpeciesDef Crown(string id, string name, string family, string a, string b, int bv, int price, Temperament t, Water w)
            => new SpeciesDef { Id = id, Name = name, Family = family, Kind = SpeciesKind.Crown, PairA = a, PairB = b, BreedingValue = bv, Price = price, Temperament = t, Water = w };

        // Nine families of eight (the user's choice, 2026-10-23): three founders, four hybrids and a crown each. Breeding
        // values lie on one line, each family in its own band and the families in era order, so breeding never reaches
        // past the later parent's era; the highest value of every era belongs to a founder or hybrid, never a crown. Each
        // hybrid sits at the exact average (rounded up) of its intended parents, named in its comment and checked by
        // GrowthRules.Validate; the first hybrid of a new family is bred from the family before it. Names are
        // provisional (GR.17). The existing five keep their recipes: Pondlet, Bubblebelly, Ribbonfin, Lanternfin, Driftwing.
        public static readonly SpeciesDef[] Species =
        {
            // Brookkin: small, Bloom and Flow, era 1 (band 40-160).
            Founder("pondlet", "Pondlet", "brookkin", R(bloom: 1, flow: 1), 100, 6, Temperament.Calm, Water.Fresh),
            Founder("bubblebelly", "Bubblebelly", "brookkin", R(bloom: 2, flow: 1), 40, 8, Temperament.Calm, Water.Fresh),
            Founder("ribbonfin", "Ribbonfin", "brookkin", R(bloom: 1, flow: 2), 160, 8, Temperament.Lively, Water.Fresh),
            Hybrid("puddlepup", "Puddlepup", "brookkin", 70, 10, Temperament.Lively, Water.Fresh),       // bubblebelly x pondlet
            Hybrid("splashkit", "Splashkit", "brookkin", 115, 11, Temperament.Lively, Water.Fresh),      // puddlepup x ribbonfin
            Hybrid("reedwhisker", "Reedwhisker", "brookkin", 130, 11, Temperament.Calm, Water.Fresh),    // pondlet x ribbonfin
            Hybrid("lilyfin", "Lilyfin", "brookkin", 145, 12, Temperament.Calm, Water.Fresh),            // reedwhisker x ribbonfin
            Crown("brookmonarch", "Brook Monarch", "brookkin", "puddlepup", "reedwhisker", 105, 30, Temperament.Calm, Water.Fresh),

            // Sparkline: small, Spark, era 2 (band 200-400).
            Founder("lanternfin", "Lanternfin", "sparkline", R(bloom: 1, flow: 1, spark: 1), 260, 12, Temperament.Calm, Water.Warm),
            Founder("sparkminnow", "Sparkminnow", "sparkline", R(flow: 1, spark: 2), 340, 12, Temperament.Lively, Water.Warm),
            Founder("fizzlet", "Fizzlet", "sparkline", R(bloom: 1, spark: 2), 380, 13, Temperament.Lively, Water.Warm),
            Hybrid("glowbelly", "Glowbelly", "sparkline", 210, 13, Temperament.Calm, Water.Fresh),       // ribbonfin x lanternfin
            Hybrid("flickerfin", "Flickerfin", "sparkline", 300, 14, Temperament.Lively, Water.Warm),    // lanternfin x sparkminnow
            Hybrid("buzzgill", "Buzzgill", "sparkline", 360, 15, Temperament.Lively, Water.Warm),        // sparkminnow x fizzlet
            Hybrid("starglimmer", "Starglimmer", "sparkline", 400, 16, Temperament.Lively, Water.Warm),  // sparkminnow x emberpuff
            Crown("aurorafin", "Aurorafin", "sparkline", "flickerfin", "starglimmer", 350, 40, Temperament.Calm, Water.Warm),

            // Cinderling: medium, Ember, era 2 (band 460-600).
            Founder("emberpuff", "Emberpuff", "cinderling", R(bloom: 1, ember: 1), 460, 24, Temperament.Lively, Water.Warm),
            Founder("cindertail", "Cindertail", "cinderling", R(flow: 1, ember: 2), 580, 26, Temperament.Lively, Water.Warm),
            Founder("coalbelly", "Coalbelly", "cinderling", R(ember: 2, spark: 1), 600, 28, Temperament.Calm, Water.Warm),
            Hybrid("sizzlesnout", "Sizzlesnout", "cinderling", 490, 30, Temperament.Lively, Water.Warm), // starglimmer x cindertail
            Hybrid("kindlekoi", "Kindlekoi", "cinderling", 520, 32, Temperament.Calm, Water.Warm),       // emberpuff x cindertail
            Hybrid("sootsnap", "Sootsnap", "cinderling", 530, 33, Temperament.Lively, Water.Warm),       // emberpuff x coalbelly
            Hybrid("ashfin", "Ashfin", "cinderling", 535, 34, Temperament.Calm, Water.Warm),             // cindertail x sizzlesnout
            Crown("phoenixfry", "Phoenix Fry", "cinderling", "kindlekoi", "ashfin", 560, 80, Temperament.Lively, Water.Warm),

            // Airfin: medium, Mist, era 3 (band 640-800).
            Founder("driftwing", "Driftwing", "airfin", R(bloom: 1, flow: 1, mist: 1), 700, 28, Temperament.Calm, Water.Fresh),
            Founder("puffglider", "Puffglider", "airfin", R(bloom: 1, ember: 1, mist: 2), 800, 30, Temperament.Calm, Water.Fresh),
            Founder("hazewing", "Hazewing", "airfin", R(flow: 1, mist: 2), 640, 30, Temperament.Lively, Water.Fresh),
            Hybrid("gustgill", "Gustgill", "airfin", 668, 34, Temperament.Lively, Water.Fresh),          // ashfin x puffglider
            Hybrid("mistrunner", "Mistrunner", "airfin", 670, 35, Temperament.Lively, Water.Fresh),      // hazewing x driftwing
            Hybrid("cloudcarp", "Cloudcarp", "airfin", 750, 36, Temperament.Calm, Water.Fresh),         // driftwing x puffglider
            Hybrid("breezeling", "Breezeling", "airfin", 775, 38, Temperament.Lively, Water.Fresh),     // cloudcarp x puffglider
            Crown("zephyray", "Zephyray", "airfin", "cloudcarp", "gustgill", 780, 90, Temperament.Calm, Water.Fresh),

            // Dewling: small, Mist, era 3 (band 830-1000).
            Founder("dewdrop", "Dewdrop", "dewling", R(flow: 2, mist: 1), 860, 16, Temperament.Calm, Water.Fresh),
            Founder("fogfin", "Fogfin", "dewling", R(spark: 1, mist: 1), 940, 17, Temperament.Lively, Water.Fresh),
            Founder("mistlet", "Mistlet", "dewling", R(bloom: 1, mist: 1), 1000, 18, Temperament.Calm, Water.Fresh),
            Hybrid("drizzler", "Drizzler", "dewling", 830, 19, Temperament.Lively, Water.Fresh),         // puffglider x dewdrop
            Hybrid("pearlpip", "Pearlpip", "dewling", 900, 20, Temperament.Calm, Water.Fresh),           // dewdrop x fogfin
            Hybrid("haloling", "Haloling", "dewling", 950, 21, Temperament.Calm, Water.Fresh),           // pearlpip x mistlet
            Hybrid("rainskipper", "Rainskipper", "dewling", 970, 22, Temperament.Lively, Water.Fresh),   // fogfin x mistlet
            Crown("rainbowregent", "Rainbow Regent", "dewling", "pearlpip", "rainskipper", 920, 60, Temperament.Calm, Water.Fresh),

            // Reefback: large, Brine, era 4 (band 1030-1200).
            Founder("shellback", "Shellback", "reefback", R(flow: 2, brine: 2), 1060, 70, Temperament.Calm, Water.Brine),
            Founder("coralhorn", "Coralhorn", "reefback", R(bloom: 1, flow: 1, ember: 1, brine: 2), 1160, 76, Temperament.Lively, Water.Brine),
            Founder("clamjaw", "Clamjaw", "reefback", R(ember: 1, brine: 3), 1200, 80, Temperament.Calm, Water.Brine),
            Hybrid("kelpgrazer", "Kelpgrazer", "reefback", 1030, 84, Temperament.Calm, Water.Brine),     // mistlet x shellback
            Hybrid("barnaclebrow", "Barnaclebrow", "reefback", 1110, 90, Temperament.Calm, Water.Brine), // shellback x coralhorn
            Hybrid("tidetusk", "Tidetusk", "reefback", 1135, 96, Temperament.Lively, Water.Brine),       // barnaclebrow x coralhorn
            Hybrid("wavebreaker", "Wavebreaker", "reefback", 1180, 100, Temperament.Lively, Water.Brine), // coralhorn x clamjaw
            Crown("coralossus", "Coralossus", "reefback", "barnaclebrow", "tidetusk", 1120, 220, Temperament.Calm, Water.Brine),

            // Saltscale: medium, Brine, era 4 (band 1230-1400).
            Founder("brinefin", "Brinefin", "saltscale", R(bloom: 1, brine: 2), 1260, 40, Temperament.Lively, Water.Brine),
            Founder("pebblejaw", "Pebblejaw", "saltscale", R(mist: 1, brine: 2), 1340, 42, Temperament.Calm, Water.Brine),
            Founder("seaspark", "Seaspark", "saltscale", R(spark: 2, brine: 1), 1400, 44, Temperament.Lively, Water.Brine),
            Hybrid("foamtail", "Foamtail", "saltscale", 1230, 46, Temperament.Lively, Water.Brine),      // clamjaw x brinefin
            Hybrid("surfskipper", "Surfskipper", "saltscale", 1300, 48, Temperament.Lively, Water.Brine), // brinefin x pebblejaw
            Hybrid("shoalglider", "Shoalglider", "saltscale", 1350, 50, Temperament.Calm, Water.Brine),  // surfskipper x seaspark
            Hybrid("tidepip", "Tidepip", "saltscale", 1370, 52, Temperament.Calm, Water.Brine),          // pebblejaw x seaspark
            Crown("pearlemperor", "Pearl Emperor", "saltscale", "tidepip", "shoalglider", 1320, 140, Temperament.Calm, Water.Brine),

            // Gloamer: large, Dusk, era 5 (band 1430-1600).
            Founder("moonmouth", "Moonmouth", "gloamer", R(spark: 1, mist: 1, dusk: 2), 1460, 100, Temperament.Calm, Water.Brine),
            Founder("gloomfin", "Gloomfin", "gloamer", R(flow: 1, brine: 2, dusk: 2), 1540, 108, Temperament.Lively, Water.Brine),
            Founder("shadeback", "Shadeback", "gloamer", R(bloom: 1, dusk: 2), 1600, 112, Temperament.Calm, Water.Brine),
            Hybrid("hushray", "Hushray", "gloamer", 1430, 118, Temperament.Calm, Water.Brine),           // seaspark x moonmouth
            Hybrid("duskdrifter", "Duskdrifter", "gloamer", 1500, 126, Temperament.Calm, Water.Brine),   // moonmouth x gloomfin
            Hybrid("murkmaw", "Murkmaw", "gloamer", 1550, 130, Temperament.Lively, Water.Brine),         // duskdrifter x shadeback
            Hybrid("nightlantern", "Nightlantern", "gloamer", 1570, 132, Temperament.Lively, Water.Brine), // gloomfin x shadeback
            Crown("eclipseleviathan", "Eclipse Leviathan", "gloamer", "duskdrifter", "nightlantern", 1520, 300, Temperament.Calm, Water.Brine),

            // Cometail: large, Spark and Dusk, era 5 (band 1630-1800).
            Founder("starmaw", "Starmaw", "cometail", R(spark: 2, dusk: 2), 1660, 120, Temperament.Lively, Water.Warm),
            Founder("meteorback", "Meteorback", "cometail", R(ember: 1, spark: 1, dusk: 2), 1740, 128, Temperament.Calm, Water.Warm),
            Founder("nebulafin", "Nebulafin", "cometail", R(spark: 1, mist: 2, dusk: 1), 1800, 134, Temperament.Calm, Water.Warm),
            Hybrid("orbitail", "Orbitail", "cometail", 1630, 138, Temperament.Lively, Water.Warm),       // shadeback x starmaw
            Hybrid("quasarray", "Quasarray", "cometail", 1700, 144, Temperament.Calm, Water.Warm),       // starmaw x meteorback
            Hybrid("novatusk", "Novatusk", "cometail", 1750, 150, Temperament.Lively, Water.Warm),       // quasarray x nebulafin
            Hybrid("cosmocarp", "Cosmocarp", "cometail", 1770, 156, Temperament.Calm, Water.Warm),       // meteorback x nebulafin
            Crown("celestialwyrm", "Celestial Wyrm", "cometail", "cosmocarp", "novatusk", 1720, 320, Temperament.Calm, Water.Warm),
        };

        /// <summary>The parents each hybrid is designed around (its breeding value is their average); validated.</summary>
        public static readonly (string hybrid, string a, string b)[] IntendedPairs =
        {
            ("puddlepup", "bubblebelly", "pondlet"), ("splashkit", "puddlepup", "ribbonfin"), ("reedwhisker", "pondlet", "ribbonfin"), ("lilyfin", "reedwhisker", "ribbonfin"),
            ("glowbelly", "ribbonfin", "lanternfin"), ("flickerfin", "lanternfin", "sparkminnow"), ("buzzgill", "sparkminnow", "fizzlet"), ("starglimmer", "sparkminnow", "emberpuff"),
            ("sizzlesnout", "starglimmer", "cindertail"), ("kindlekoi", "emberpuff", "cindertail"), ("sootsnap", "emberpuff", "coalbelly"), ("ashfin", "cindertail", "sizzlesnout"),
            ("gustgill", "ashfin", "puffglider"), ("mistrunner", "hazewing", "driftwing"), ("cloudcarp", "driftwing", "puffglider"), ("breezeling", "cloudcarp", "puffglider"),
            ("drizzler", "puffglider", "dewdrop"), ("pearlpip", "dewdrop", "fogfin"), ("haloling", "pearlpip", "mistlet"), ("rainskipper", "fogfin", "mistlet"),
            ("kelpgrazer", "mistlet", "shellback"), ("barnaclebrow", "shellback", "coralhorn"), ("tidetusk", "barnaclebrow", "coralhorn"), ("wavebreaker", "coralhorn", "clamjaw"),
            ("foamtail", "clamjaw", "brinefin"), ("surfskipper", "brinefin", "pebblejaw"), ("shoalglider", "surfskipper", "seaspark"), ("tidepip", "pebblejaw", "seaspark"),
            ("hushray", "seaspark", "moonmouth"), ("duskdrifter", "moonmouth", "gloomfin"), ("murkmaw", "duskdrifter", "shadeback"), ("nightlantern", "gloomfin", "shadeback"),
            ("orbitail", "shadeback", "starmaw"), ("quasarray", "starmaw", "meteorback"), ("novatusk", "quasarray", "nebulafin"), ("cosmocarp", "meteorback", "nebulafin"),
        };

        public static SpeciesDef Get(string id)
        {
            foreach (var s in Species) if (s.Id == id) return s;
            return null;
        }

        // ---------------------------------------------------------------- research points (GR.7.2)

        public const int RpFounder = 5, RpHybrid = 8, RpCrown = 20, RpColour = 2;

        public static int RpFor(SpeciesKind kind) => kind == SpeciesKind.Founder ? RpFounder : kind == SpeciesKind.Hybrid ? RpHybrid : RpCrown;

        // ---------------------------------------------------------------- stations (GR.9)

        static LevelDef L(int level, int credits, string change, int era = 1, int stars = 1, int rp = 0, params (string, float)[] stats)
        {
            var l = new LevelDef { Level = level, Credits = credits, Rp = rp, Era = era, Stars = stars, Change = change };
            foreach (var (k, v) in stats) l.Stats[k] = v;
            return l;
        }

        public static readonly StationDef[] Stations =
        {
            new StationDef { Id = "siphon", Name = "Siphon and net", Levels = new[]
            {
                L(1, 0, "Hand bulb: 3 doses, hold 0.8 s each; picks creatures in peek", 1, 1, 0, ("capacity", 3), ("holdSeconds", .8f), ("perHold", 1)),
                L(2, 15, "Bigger bulb: 6 doses, 0.6 s", 1, 1, 0, ("capacity", 6), ("holdSeconds", .6f), ("perHold", 1)),
                L(3, 150, "Twin chamber: 10 doses, collects 2 at a time", 2, 2, 0, ("capacity", 10), ("holdSeconds", .6f), ("perHold", 2)),
                L(4, 900, "Vacuum: 16 doses, pulls from every grower in range", 3, 3, 0, ("capacity", 16), ("holdSeconds", .5f), ("perHold", 4)),
            }},
            new StationDef { Id = "grower", Name = "Grower (each culture)", Levels = new[]
            {
                L(1, 0, "Capacity 4, refills one every 3 s", 1, 1, 0, ("capacity", 4), ("refillSeconds", 3)),
                L(2, 20, "Capacity 6, every 2 s", 1, 1, 0, ("capacity", 6), ("refillSeconds", 2)),
                L(3, 160, "Capacity 10, every 1.5 s; a tap for a pipe", 2, 2, 0, ("capacity", 10), ("refillSeconds", 1.5f), ("tap", 1)),
                L(4, 1500, "Capacity 16, every 1 s; feeds two pipes", 4, 4, 0, ("capacity", 16), ("refillSeconds", 1), ("tap", 2)),
            }},
            new StationDef { Id = "incubator", Name = "Incubator", Levels = new[]
            {
                L(1, 0, "One creature in 12 s; mixes of 2-3 doses", 1, 1, 0, ("seconds", 12), ("maxDoses", 3), ("clutch", 1)),
                L(2, 25, "8 s; mixes up to 4; the mix log screen", 1, 1, 0, ("seconds", 8), ("maxDoses", 4), ("clutch", 1)),
                L(3, 250, "Clutch of 2; mixes up to 6; a tint port", 2, 2, 0, ("seconds", 8), ("maxDoses", 6), ("clutch", 2), ("tint", 1)),
                L(4, 3000, "Clutch of 3; the auto-doser port (needs power)", 4, 4, 0, ("seconds", 8), ("maxDoses", 6), ("clutch", 3), ("tint", 1), ("autoDoser", 1)),
            }},
            new StationDef { Id = "breeding", Name = "Breeding tank", Levels = new[]
            {
                L(1, 60, "One pair, one offspring in 60 s", 2, 2, 5, ("seconds", 60), ("pairs", 1), ("largeParents", 0)),
                L(2, 200, "40 s; a tint port; Large parents", 2, 2, 0, ("seconds", 40), ("pairs", 1), ("tint", 1), ("largeParents", 1)),
                L(3, 2000, "Two pairs at once", 4, 4, 0, ("seconds", 40), ("pairs", 2), ("tint", 1), ("largeParents", 1)),
            }},
            new StationDef { Id = "counter", Name = "Counter", Levels = new[]
            {
                L(1, 0, "Bell: hold 1.2 s to bag an order", 1, 1, 0, ("serveSeconds", 1.2f), ("queue", 2)),
                L(2, 20, "Bag dispenser: 0.8 s", 1, 1, 0, ("serveSeconds", .8f), ("queue", 2)),
                L(3, 180, "Card reader: 0.5 s; a queue of 3", 2, 2, 0, ("serveSeconds", .5f), ("queue", 3)),
                L(4, 1500, "Second till: a helper serves while you do too", 4, 4, 0, ("serveSeconds", .5f), ("queue", 3), ("tills", 2)),
            }},
            new StationDef { Id = "bench", Name = "Research bench", Levels = new[]
            {
                L(1, 0, "Notes: culture icons on undiscovered silhouettes", 1, 1, 0, ("triesPerFounder", 2.5f), ("triesPerHybrid", 3)),
                L(2, 80, "Microscope: how many doses to the nearest new creature", 2, 2, 5, ("triesPerFounder", 1.5f), ("triesPerHybrid", 3)),
                L(3, 1500, "Analyzer: one undiscovered reachable mix or pair a day", 4, 4, 120, ("triesPerFounder", 1.2f), ("triesPerHybrid", 1.8f)),
                L(4, 4000, "Planner: predicts the offspring of any two known species", 5, 5, 200, ("triesPerFounder", 1.2f), ("triesPerHybrid", 1)),
            }},
            new StationDef { Id = "vat", Name = "Conditioning vat", Levels = new[]
            {
                L(1, 600, "Mist (as today)", 3, 3, 40, ("mist", 1)),
                L(2, 800, "Double batch", 3, 3, 0, ("mist", 2)),
                L(3, 1500, "Faster; Shimmer tint", 4, 4, 0, ("mist", 3), ("shimmer", 1)),
            }},
            new StationDef { Id = "generator", Name = "Generator", Levels = new[]
            {
                L(1, 200, "Hand crank: 1 unit while cranked", 3, 3, 30, ("units", 1), ("manual", 1)),
                L(2, 800, "Bubble turbine: 3 units", 3, 3, 0, ("units", 3)),
                L(3, 3000, "Twin turbine: 6 units", 4, 4, 0, ("units", 6)),
                L(4, 6000, "Tide engine: 12 units", 5, 5, 0, ("units", 12)),
            }},
            new StationDef { Id = "pump", Name = "Pumps and pipes", Levels = new[]
            {
                L(1, 150, "Hand pump and pipes: crank to fill a buffer in bulk", 3, 3, 0, ("collectFactor", .5f), ("units", 0)),
                L(2, 400, "Electric pumps: growers feed buffers by themselves", 3, 3, 0, ("collectFactor", 0), ("units", 1)),
                L(3, 1500, "Fast pumps", 4, 4, 0, ("collectFactor", 0), ("units", 2), ("fast", 1)),
            }},
            new StationDef { Id = "sorter", Name = "Hatchling chute and sorter", Levels = new[]
            {
                L(1, 3000, "Hatchlings go to racks and tanks by species rules", 4, 4, 0, ("carryFactor", 0), ("units", 1)),
            }},
        };

        public static StationDef Station(string id)
        {
            foreach (var s in Stations) if (s.Id == id) return s;
            throw new ArgumentException("Unknown station " + id);
        }

        // ---------------------------------------------------------------- containers (GR.5)

        public static readonly ContainerDef[] Containers =
        {
            new ContainerDef { Id = "starter", Name = "Starter tank", Largest = SizeClass.Medium, Width = 1.5f, Depth = 1f, Levels = new[]
            {
                L(1, 20, "4 spaces", 1, 1, 0, ("spaces", 4)), L(2, 25, "Filter: 6 spaces", 1, 1, 0, ("spaces", 6)), L(3, 100, "Wide lid: 8 spaces", 2, 2, 0, ("spaces", 8)),
            }},
            new ContainerDef { Id = "large", Name = "Large tank", Largest = SizeClass.Large, Width = 2.5f, Depth = 1f, Levels = new[]
            {
                L(1, 200, "8 spaces", 2, 2, 0, ("spaces", 8)), L(2, 400, "12 spaces", 3, 3, 0, ("spaces", 12)), L(3, 1500, "16 spaces", 4, 4, 0, ("spaces", 16)),
            }},
            new ContainerDef { Id = "show", Name = "Show tank", Largest = SizeClass.Large, Width = 3f, Depth = 1.5f, Levels = new[]
            {
                L(1, 2500, "16 spaces, lit (+appeal)", 4, 4, 0, ("spaces", 16)), L(2, 5000, "24 spaces, spotlight", 5, 5, 0, ("spaces", 24)),
            }},
            new ContainerDef { Id = "rack", Name = "Lab rack", Largest = SizeClass.Large, Width = 1.5f, Depth = 1f, Lab = true, Levels = new[]
            {
                L(1, 30, "4 spaces", 1, 1, 0, ("spaces", 4)), L(2, 120, "8 spaces", 2, 2, 0, ("spaces", 8)), L(3, 400, "12 spaces", 3, 3, 0, ("spaces", 12)),
            }},
        };

        public static ContainerDef Container(string id)
        {
            foreach (var c in Containers) if (c.Id == id) return c;
            throw new ArgumentException("Unknown container " + id);
        }

        // ---------------------------------------------------------------- spaces (GR.11)

        public static readonly ExpansionDef[] Expansions =
        {
            new ExpansionDef { Id = "unit-a", Name = "Shop unit A and back room", Room = "shop", Credits = 0, Stars = 1, Era = 1, Width = 8, Depth = 8, ContainerPlaces = 4, VisitorsAtOnce = 2, ExtraWalkSeconds = 0 },
            new ExpansionDef { Id = "unit-b", Name = "Shop unit B (east)", Room = "shop", Credits = 400, Stars = 2, Era = 2, Width = 8, Depth = 8, ContainerPlaces = 4, VisitorsAtOnce = 1, ExtraWalkSeconds = 2 },
            new ExpansionDef { Id = "annex", Name = "Lab annex", Room = "lab", Credits = 1000, Stars = 3, Era = 3, Width = 8, Depth = 8, ContainerPlaces = 0, VisitorsAtOnce = 0, ExtraWalkSeconds = 2 },
            new ExpansionDef { Id = "unit-c", Name = "Shop unit C (north-east)", Room = "shop", Credits = 4000, Stars = 4, Era = 4, Width = 8, Depth = 4, ContainerPlaces = 6, VisitorsAtOnce = 1, ExtraWalkSeconds = 2 },
            new ExpansionDef { Id = "workshop", Name = "Workshop", Room = "lab", Credits = 5000, Stars = 4, Era = 4, Width = 8, Depth = 6, ContainerPlaces = 0, VisitorsAtOnce = 0, ExtraWalkSeconds = 1 },
            new ExpansionDef { Id = "exhibition-hall", Name = "Exhibition hall finish", Room = "shop", Credits = 6000, Stars = 5, Era = 5, Width = 0, Depth = 0, ContainerPlaces = 2, VisitorsAtOnce = 1, ExtraWalkSeconds = 0 },
        };

        // ---------------------------------------------------------------- helpers (GR.10)

        public static readonly HelperDef[] Helpers =
        {
            new HelperDef { Id = "pip", Name = "Pip", Job = "cashier", Gate = "2 stars and 40 sales", Stars = 2, Fee = 100, Wages = new[] { 8, 12, 16 }, TrainCosts = new[] { 300, 1200 } },
            new HelperDef { Id = "nori", Name = "Nori", Job = "stocker", Gate = "3 stars and a Large tank", Stars = 3, Fee = 400, Wages = new[] { 25, 35, 50 }, TrainCosts = new[] { 1000, 3000 } },
            new HelperDef { Id = "moss", Name = "Moss", Job = "technician", Gate = "3 stars and a powered generator", Stars = 3, Fee = 800, Wages = new[] { 60, 80, 100 }, TrainCosts = new[] { 2000, 5000 } },
        };

        public const int PipSalesGate = 150;

        // ---------------------------------------------------------------- stars, eras and visitors (GR.4, GR.10)

        /// <summary>Reputation for 1-5 stars, rescaled for a four-hour campaign (GR.10). Reputation runs 0-1000.</summary>
        public static readonly int[] StarThresholds = { 0, 40, 220, 850, 1700 };
        public const int ReputationMax = 2500, RepPerSale = 2, RepPerWish = 1, RepPerRefusal = -1;

        public static int StarsFor(int reputation)
        {
            int stars = 1;
            for (int i = 1; i < StarThresholds.Length; i++) if (reputation >= StarThresholds[i]) stars = i + 1;
            return stars;
        }

        public static readonly EraDef[] Eras =
        {
            new EraDef { Number = 1, Name = "Back room", Stars = 1, Gate = "Start", Goal = "Discover 3 creatures and buy your second tank", TargetFrom = 0, TargetTo = 0 },
            new EraDef { Number = 2, Name = "Real shop", Stars = 2, Gate = "2 stars, 3 creatures discovered, 2 tanks", Goal = "Breed your first hybrid; reach 3 stars", TargetFrom = 20, TargetTo = 40 },
            new EraDef { Number = 3, Name = "Plumbing", Stars = 3, Gate = "3 stars, a hybrid bred, a Large tank", Goal = "Power the lab; fill a Large tank", TargetFrom = 60, TargetTo = 90 },
            new EraDef { Number = 4, Name = "Machines", Stars = 4, Gate = "4 stars, powered pumps running, 12 species", Goal = "Run a full day on the line; discover a large creature", TargetFrom = 120, TargetTo = 160 },
            new EraDef { Number = 5, Name = "Exhibition", Stars = 5, Gate = "5 stars, a large creature, a full day on the line", Goal = "Host the Grand Exhibition", TargetFrom = 180, TargetTo = 220 },
        };

        /// <summary>The Grand Exhibition becomes available (GR.12), in minutes.</summary>
        public const int ExhibitionFrom = 200, ExhibitionTo = 250, ExhibitionSpecies = 48, ExhibitionCrowns = 5;

        public static readonly VisitorTypeDef[] Visitors =
        {
            new VisitorTypeDef { Id = "casual", Name = "Casual visitor", Stars = 1, Weights = new[] { 40, 35, 30, 30, 25 }, Budgets = new[] { 20, 30, 45, 60, 80 } },
            new VisitorTypeDef { Id = "bargain", Name = "Bargain hunter", Stars = 1, Weights = new[] { 30, 25, 20, 20, 15 }, Budgets = new[] { 10, 14, 20, 28, 36 } },
            new VisitorTypeDef { Id = "collector", Name = "Collector", Stars = 2, Weights = new[] { 0, 25, 25, 25, 25 }, Budgets = new[] { 0, 60, 90, 130, 180 } },
            new VisitorTypeDef { Id = "premium", Name = "Big spender", Stars = 3, Weights = new[] { 0, 0, 25, 30, 35 }, Budgets = new[] { 0, 0, 120, 200, 320 } },
        };

        /// <summary>Seconds between arrivals at each star level, and a visit's average length (browse, queue, pay).</summary>
        public static readonly float[] ArrivalSeconds = { 6f, 5.5f, 5f, 4.5f, 4f };
        public const float VisitSeconds = 22f;
    }
}
