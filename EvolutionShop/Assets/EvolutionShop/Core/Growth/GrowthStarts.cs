using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    /// <summary>
    /// GR-07: development-only start points for the power playtest (Docs/Evidence/gr-07/PLAY.md), and GR-10's for a look at
    /// the spaces (Docs/Evidence/gr-10/LOOK.md), each with its own save:
    /// the start of era 3 (the lab annex still closed) and the start of era 4 (the annex with the bubble turbine, electric
    /// pumps and two growers piped to buffers). They are shops a player could have by then, made with the shop's own actions
    /// where it has them; the creatures in the tanks are put there as the routes' fixtures put them.
    /// </summary>
    public static class GrowthStarts
    {
        public const string Era3 = "era3", Era4 = "era4", Spaces = "spaces", Exhibition = "exhibition", Creatures = "creatures", Look = "look", Decor = "decor", Wishes = "wishes", Research = "research";

        /// <summary>A start point: an established shop, so it learns the lessons for what it has (R09.4) and meets only the rest.</summary>
        public static GrowthShop Make(string id) { var shop = MakeShop(id); shop?.LearnWhatIsKnown(); return shop; }

        static GrowthShop MakeShop(string id) => id == Era3 ? Era3Start() : id == Era4 ? Era4Start() : id == Spaces ? SpacesStart() : id == Exhibition ? ExhibitionStart() : id == Creatures ? CreaturesStart() : id == Look ? LookStart() : id == Decor ? DecorStart() : id == Wishes ? WishesStart() : id == Research ? ResearchStart() : null;

        /// <summary>GR-12's families (waves 1 to 4: all nine), shown in the AD look.</summary>
        public static readonly string[] KitFamilies = { "brookkin", "sparkline", "airfin", "cinderling", "dewling", "reefback", "saltscale", "gloamer", "cometail" };
        /// <summary>GR-12's newest wave, which the creatures start point shows in two colours each (the user's look is at it).</summary>
        public static readonly string[] NewestFamilies = { "gloamer", "cometail" };

        /// <summary>The start of era 3: 3 stars, 1,200 credits, 45 RP, unit B, Bloom, Flow, Ember and Spark, the founders of
        /// the first families and four hybrids, the breeding tank, a Large tank, stocked tanks, levels 2-3.</summary>
        public static GrowthShop Era3Start()
        {
            var shop = new GrowthShop { Era = 3, Day = 10, Reputation = GrowthContent.StarThresholds[2] + 100, Credits = 100000, Rp = 45, UnitB = true };
            foreach (var c in new[] { "ember", "spark" }) { shop.Cultures.Add(c); shop.GrowerUnits[c] = 0; }
            shop.Levels["siphon"] = 2; shop.Levels["grower"] = 2; shop.Levels["incubator"] = 3; shop.Levels["counter"] = 2; shop.Levels["bench"] = 2; shop.Levels["breeding"] = 1;
            foreach (var c in shop.Cultures) shop.GrowerUnits[c] = shop.GrowerCapacityOf(c);
            var founders = GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Founder && GrowthContent.Family(s.Family).Era <= 2 && s.Doses <= shop.MaxDoses &&
                Enumerable.Range(0, s.Recipe.Length).All(i => s.Recipe[i] == 0 || shop.Cultures.Contains(GrowthContent.Cultures[i].Id))).ToList();
            shop.Discovered.AddRange(founders.Select(s => s.Id));
            shop.Discovered.AddRange(new[] { "puddlepup", "reedwhisker", "splashkit", "glowbelly" }.Where(id => !shop.Discovered.Contains(id)));
            shop.Journal.AddRange(shop.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            foreach (var s in founders) shop.MixLog.Add(new GrowthMixEntry { Doses = (int[])s.Recipe.Clone(), Species = s.Id, Exact = true, DistanceToNew = -1 });
            shop.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
            shop.PairLog.Add(new GrowthPairEntry { A = "pondlet", B = "ribbonfin", Species = "reedwhisker" });
            shop.PairLog.Add(new GrowthPairEntry { A = "puddlepup", B = "ribbonfin", Species = "splashkit" });
            shop.PairLog.Add(new GrowthPairEntry { A = "lanternfin", B = "ribbonfin", Species = "glowbelly" });
            // Tanks: the Starter tank at level 2, a Starter tank, a Large tank, and two Starter tanks in unit B, with stock.
            shop.Containers[0].Level = 2;
            foreach (var (kind, place) in new[] { ("starter", "a2"), ("large", "a3"), ("starter", "b1"), ("starter", "b2") }) shop.BuyContainer(kind, place);
            void Fill(string place, params string[] species) { var c = shop.ContainerAt(place); foreach (var sp in species) shop.DevelopmentStock(c.Id, sp); }
            Fill("a1", "pondlet", "pondlet", "pondlet", "bubblebelly", "bubblebelly");
            Fill("a2", "ribbonfin", "ribbonfin");
            Fill("a3", "puddlepup", "puddlepup", "reedwhisker");
            var spark = founders.Where(s => GrowthContent.Family(s.Family).Era == 2).Select(s => s.Id).Take(2).ToArray();
            if (spark.Length > 0) Fill("b1", spark.Concat(spark).ToArray());
            shop.Credits = 1200;
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-30's look at the research room (Docs/Evidence/gr-30/LOOK.md): the era 3 start with the power room open, the bubble
        /// turbine running and 3,000 credits, the room itself still to open.</summary>
        public static GrowthShop ResearchStart()
        {
            var shop = Era3Start();
            shop.Annex = true;
            shop.Levels["generator"] = 2;
            shop.Credits = 3000;
            return shop;
        }

        /// <summary>The start of era 4: the era 3 start with 4 stars, 6,000 credits, the lab annex, the bubble turbine and
        /// electric pumps, growers at level 3, and the Bloom and Flow growers piped to buffers 1 and 2.</summary>
        public static GrowthShop Era4Start()
        {
            var shop = Era3Start();
            shop.Era = 4; shop.Day = 15; shop.Reputation = GrowthContent.StarThresholds[3] + 50; shop.Credits = 100000; shop.Rp = 90;
            shop.BuyAnnex();
            shop.Upgrade("grower");
            shop.Upgrade("generator"); shop.Upgrade("generator");
            shop.Upgrade("pump"); shop.Upgrade("pump");
            shop.BuyBuffer(0); shop.BuyBuffer(1);
            shop.LayPipe(GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            shop.LayPipe(GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            foreach (var c in shop.Cultures) shop.GrowerUnits[c] = shop.GrowerCapacityOf(c);
            shop.BufferAt(0).Units = 8; shop.BufferAt(1).Units = 8;
            shop.Credits = 6000; shop.Rp = 60;
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-19's look at the decor (Docs/Evidence/gr-19/LOOK.md): the era 4 start (4 stars, every catalog page open) with
        /// a tank of each water: a1's fresh-water Brookkin, b1's warm-water creatures (its water warm) and two Saltscale founders
        /// in b2 (its water brine), and 8,000 credits. Furnished, all nine pieces are delivered and three are on tanks (the reeds
        /// on a1, the volcano on b1, the lighthouse on b2), the rest in the box; unfurnished (the GR-19 route), none yet.</summary>
        public static GrowthShop DecorStart(bool furnished = true)
        {
            var shop = Era4Start();
            shop.ContainerAt("b1").Water = Water.Warm;
            var b2 = shop.ContainerAt("b2");
            b2.Water = Water.Brine;
            foreach (var species in GrowthContent.Species.Where(s => s.Family == "saltscale" && s.Kind == SpeciesKind.Founder).Select(s => s.Id).Take(2))
                shop.DevelopmentStock(b2.Id, species);
            shop.Credits = 8000;
            if (furnished)
            {
                shop.DecorOwned.AddRange(GrowthContent.Decor.Select(d => d.Id));
                shop.ContainerAt("a1").Decor = "reeds"; shop.ContainerAt("b1").Decor = "volcano"; b2.Decor = "lighthouse";
            }
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-20's look at visitors' wishes (Docs/Evidence/gr-20/LOOK.md): the decor start, furnished (4 stars, so every
        /// kind of visitor comes and pairs and schools are wished for; the Brookkin, Sparkline and Cinderling discovered, small
        /// and medium), with colours made and on show: a Sunny Pondlet in a1's school, a Shimmer Ribbonfin beside a2's, and the
        /// Deep Bubblebelly in the journal; an Emberpuff (medium) in a3 with the Brookkin hybrids; and 6,000 credits.</summary>
        public static GrowthShop WishesStart()
        {
            var shop = DecorStart();
            foreach (var (species, colour) in new[] { ("pondlet", Colour.Sunny), ("bubblebelly", Colour.Deep), ("ribbonfin", Colour.Shimmer) })
                if (!shop.Journal.Contains(GrowthShop.JournalKey(species, colour))) shop.Journal.Add(GrowthShop.JournalKey(species, colour));
            shop.ContainerAt("a1").Occupants.First(o => o.Species == "pondlet").Colour = Colour.Sunny;
            Stock(shop, "a2", "ribbonfin", Colour.Shimmer);
            Stock(shop, "a3", "emberpuff", Colour.Classic);
            shop.Credits = 6000;
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-10's look at the spaces (Docs/Evidence/gr-10/LOOK.md): the era 4 start at 5 stars in era 5 with 30,000
        /// credits, so the workshop, shop unit C and the exhibition hall finish can each be opened at night and seen opening.</summary>
        public static GrowthShop SpacesStart()
        {
            var shop = Era4Start();
            shop.Era = 5; shop.Day = 20; shop.Reputation = GrowthContent.StarThresholds[4] + 50; shop.Credits = 30000;
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-16's look at the new game's look (Docs/Evidence/gr-16/LOOK.md): the spaces start with every station the
        /// look dresses at its top level (the growers, the incubator, the counter, the breeding tank, the research bench, the
        /// siphon, and the conditioning vat with Mist owned), five starter vials on the shelf, the Large tank on a3 at level 3, a
        /// Show tank at level 2 on unit B's feature place, and the workshop open with lab racks at levels 3 and 1; and (wave 3)
        /// the line at its top: three buffers piped from Bloom, Flow and Ember, the tide engine, fast pumps, the sorter and its
        /// chute, and the auto-doser's pipe from buffer 1; and (wave 4) unit C and the exhibition hall open, Show tanks at levels
        /// 2 and 1 on unit C's feature places and a Large tank at level 3 on the hall's first exhibition place, with creatures. The workshop, the Show tank, the racks, the buffers, the pipes, the
        /// generator, the pumps and the sorter are bought with the shop's own actions; the other levels, Mist and the vials are
        /// set as the routes' fixtures set them, and the creatures in the new tanks put there as the routes' fixtures put them.</summary>
        public static GrowthShop LookStart()
        {
            var shop = SpacesStart();
            shop.Credits = 100000;
            shop.BuyWorkshop();
            shop.BuyContainer("show", "b4");
            shop.BuyRack("r1"); shop.BuyRack("r2");
            if (!shop.Owns("mist")) shop.Cultures.Add("mist");
            foreach (var (station, level) in new[] { ("grower", 4), ("incubator", 4), ("counter", 4), ("breeding", 3), ("bench", 4), ("siphon", 4), ("vat", 3) })
                shop.Levels[station] = level;
            foreach (var c in shop.Cultures) shop.GrowerUnits[c] = shop.GrowerCapacityOf(c);
            foreach (var (culture, doses) in new[] { ("ember", 3), ("spark", 2), ("mist", 3), ("brine", 1), ("dusk", 2) }) shop.Vials[culture] = doses;
            foreach (var (place, level) in new[] { ("a3", 3), ("b4", 2), ("r1", 3), ("r2", 1) })
                if (shop.ContainerAt(place) != null) shop.ContainerAt(place).Level = level;
            foreach (var (place, count) in new[] { ("b4", 10), ("r1", 3), ("r2", 2) })
                if (shop.ContainerAt(place) != null)
                    foreach (var species in shop.Discovered.Take(count)) shop.DevelopmentStock(shop.ContainerAt(place).Id, species);
            shop.BuyBuffer(2);
            shop.LayPipe(GrowthLab.Tap("ember"), GrowthLab.Buffer(2));
            shop.Upgrade("generator"); shop.Upgrade("generator");
            shop.Upgrade("pump");
            shop.Upgrade("sorter");
            shop.LayPipe(GrowthLab.Out, GrowthLab.Sorter);
            shop.LayPipe(GrowthLab.Buffer(0), GrowthLab.Port);
            // Wave 4: unit C and the exhibition hall, Show tanks on unit C's feature wall and a Large tank on an exhibition place.
            shop.BuyUnitC(); shop.BuyHall();
            shop.BuyContainer("show", "c1"); shop.BuyContainer("show", "c2"); shop.BuyContainer("large", "e1");
            foreach (var (place, level, count) in new[] { ("c1", 2, 8), ("c2", 1, 6), ("e1", 3, 4) })
                if (shop.ContainerAt(place) != null)
                {
                    shop.ContainerAt(place).Level = level;
                    foreach (var species in shop.Discovered.Skip(2).Take(count)) shop.DevelopmentStock(shop.ContainerAt(place).Id, species);
                }
            shop.Credits = 30000;
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-11's look at the Grand Exhibition: the spaces start with the workshop, unit C and the hall open and a
        /// full day run on the line; every founder and two hybrids of each family known, with five crowns and their parents
        /// (the exhibition's requirements met); Show tanks on unit B's and unit C's feature places and Large tanks on the
        /// exhibition places, empty; the five crowns waiting in Large tanks on c3 and c4 to be put on display; and one of
        /// each other creature known, as far as the other tanks hold them.</summary>
        public static GrowthShop ExhibitionStart()
        {
            var shop = SpacesStart();
            shop.Credits = 100000;
            shop.BuyWorkshop(); shop.BuyUnitC(); shop.BuyHall();
            shop.LineDays = 1;
            var known = new List<string>();
            foreach (var f in GrowthContent.Families)
            {
                var members = GrowthContent.Species.Where(s => s.Family == f.Id).ToList();
                known.AddRange(members.Where(s => s.Kind == SpeciesKind.Founder).Select(s => s.Id));
                known.AddRange(members.Where(s => s.Kind == SpeciesKind.Hybrid).Take(2).Select(s => s.Id));
            }
            var crowns = GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Crown).Take(GrowthContent.ExhibitionCrowns).ToList();
            foreach (var c in crowns) known.AddRange(new[] { c.Id, c.PairA, c.PairB });
            foreach (var id in known.Distinct().Where(id => !shop.Discovered.Contains(id)))
            {
                shop.Discovered.Add(id);
                shop.Journal.Add(GrowthShop.JournalKey(id, Colour.Classic));
            }
            foreach (var (kind, place) in new[] { ("large", "b3"), ("show", "b4"), ("show", "c1"), ("show", "c2"), ("large", "c3"), ("large", "c4"), ("large", "c5"), ("large", "c6"), ("large", "e1"), ("large", "e2") })
                shop.BuyContainer(kind, place);
            foreach (var crown in crowns)
                foreach (var place in new[] { "c3", "c4" })
                    if (shop.DevelopmentStock(shop.ContainerAt(place).Id, crown.Id).Success) break;
            var others = shop.Discovered.Where(d => GrowthContent.Get(d).Kind != SpeciesKind.Crown && !shop.Stock.Any(s => s.Species == d)).ToList();
            var tanks = shop.ShopContainers.Where(c => !GrowthShop.SpotlitPlaces.Contains(c.Place)).OrderBy(c => c.Place, System.StringComparer.Ordinal).ToList();
            foreach (var species in others)
                foreach (var t in tanks)
                    if (shop.DevelopmentStock(t.Id, species).Success) break;
            shop.Credits = 20000;
            shop.TakeEvents();
            return shop;
        }

        /// <summary>GR-12's look at wave 4 (Docs/Evidence/gr-12/LOOK.md): the spaces start with shop unit C and the exhibition
        /// hall open, all nine families' species known in all four colours. Wave 4's Gloamer and Cometail, each species in Classic
        /// and one other colour (Sunny, Deep and Shimmer in turn), in Large tanks fitted to 16 spaces (four large creatures each):
        /// the Gloamer in the hall (e1, e2), its second colours on unit B's back wall (b1, b2), the Cometail along unit C's long
        /// wall (c3, c4), its second colours beside them (c5, c6). Waves 1 to 3, which the user approved, each species in Classic:
        /// the Brookkin with the Saltscale in unit B's Show tank (b4, fitted to 24 spaces), the Sparkline with the Dewling and the
        /// Airfin in unit C's Show tanks (c1, c2), the Cinderling on a1 and the Reefback on a3 and b3 (Large tanks). The spaces
        /// start's own tanks on a1, a3, b1 and b2 are emptied and sold to make room.</summary>
        public static GrowthShop CreaturesStart()
        {
            var shop = SpacesStart();
            shop.Credits = 100000;
            shop.BuyUnitC(); shop.BuyHall();
            var wave = GrowthContent.Species.Where(s => KitFamilies.Contains(s.Family)).ToList();
            foreach (var s in wave)
            {
                if (!shop.Discovered.Contains(s.Id)) shop.Discovered.Add(s.Id);
                foreach (Colour colour in System.Enum.GetValues(typeof(Colour)))
                    if (!shop.Journal.Contains(GrowthShop.JournalKey(s.Id, colour))) shop.Journal.Add(GrowthShop.JournalKey(s.Id, colour));
            }
            foreach (var place in new[] { "a1", "a3", "b1", "b2" })
            {
                var old = shop.ContainerAt(place);
                if (old == null) continue;
                old.Occupants.Clear();
                shop.SellContainer(old.Id);
            }
            foreach (var (kind, place) in new[] { ("show", "b4"), ("show", "c1"), ("show", "c2"), ("large", "a1"), ("large", "a3"), ("large", "b3"),
                ("large", "e1"), ("large", "e2"), ("large", "b1"), ("large", "b2"), ("large", "c3"), ("large", "c4"), ("large", "c5"), ("large", "c6") })
            {
                shop.BuyContainer(kind, place);
                // The Large tanks fitted to their third level (16 spaces) and unit B's Show tank to its second (24), as a player
                // would at night.
                if (kind == "large") shop.ContainerAt(place).Level = 3;
                else if (place == "b4") shop.ContainerAt(place).Level = 2;
            }
            var others = new[] { Colour.Sunny, Colour.Deep, Colour.Shimmer };
            var tankFor = new Dictionary<string, string[]> { { "brookkin", new[] { "b4" } }, { "saltscale", new[] { "b4" } }, { "sparkline", new[] { "c1" } },
                { "dewling", new[] { "c1" } }, { "airfin", new[] { "c2" } }, { "cinderling", new[] { "a1" } }, { "reefback", new[] { "a3", "b3" } },
                { "gloamer", new[] { "e1", "e2" } }, { "cometail", new[] { "c3", "c4" } } };
            var besideFor = new Dictionary<string, string[]> { { "gloamer", new[] { "b1", "b2" } }, { "cometail", new[] { "c5", "c6" } } };
            // Every species in Classic in its family's tanks; the newest wave's in a second colour beside them too.
            foreach (var s in wave) tankFor[s.Family].Any(place => Stock(shop, place, s.Id, Colour.Classic));
            var newest = wave.Where(s => NewestFamilies.Contains(s.Family)).ToList();
            for (int i = 0; i < newest.Count; i++)
            {
                var s = newest[i];
                besideFor[s.Family].Any(place => Stock(shop, place, s.Id, others[i % 3]));
            }
            shop.Credits = 20000;
            shop.TakeEvents();
            return shop;
        }

        static bool Stock(GrowthShop shop, string place, string species, Colour colour)
        {
            var c = shop.ContainerAt(place);
            if (c == null || !shop.DevelopmentStock(c.Id, species).Success) return false;
            c.Occupants[c.Occupants.Count - 1].Colour = colour;
            return true;
        }
    }
}
