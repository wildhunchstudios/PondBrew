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
        public const string Era3 = "era3", Era4 = "era4", Spaces = "spaces";

        public static GrowthShop Make(string id) => id == Era3 ? Era3Start() : id == Era4 ? Era4Start() : id == Spaces ? SpacesStart() : null;

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

        /// <summary>GR-10's look at the spaces (Docs/Evidence/gr-10/LOOK.md): the era 4 start at 5 stars in era 5 with 30,000
        /// credits, so the workshop, shop unit C and the exhibition hall finish can each be opened at night and seen opening.</summary>
        public static GrowthShop SpacesStart()
        {
            var shop = Era4Start();
            shop.Era = 5; shop.Day = 20; shop.Reputation = GrowthContent.StarThresholds[4] + 50; shop.Credits = 30000;
            shop.TakeEvents();
            return shop;
        }
    }
}
