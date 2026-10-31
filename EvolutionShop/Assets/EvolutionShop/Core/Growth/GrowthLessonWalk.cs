using System;
using System.Linq;

namespace EvolutionShop.Growth
{
    /// <summary>
    /// Development and validation only: R09.4's walk through every lesson in its order, shared by the lessons route and its
    /// EditMode test. For each lesson, Stage puts the shop where its system appears (a new era's stars and credits, and where a
    /// long process would take a day or more, a labelled stand-in: a delivery, a bred pair, a coloured offspring, a batch, a
    /// day on the line, an exhibition held); Act then uses the system once with the shop's own actions, as its step says.
    /// Every stand-in is listed in Labelled, for the route's limitations.
    /// </summary>
    public static class GrowthLessonWalk
    {
        /// <summary>What the walk does that is not the shop's own play, for the route's limitations.</summary>
        public const string Labelled =
            "R09.4 lessons walk (GrowthLessonWalk, development only): the route's fresh shop past its first night (day 2, the guide done, a1 stocked with Pondlets); each era begun with its stars and 100,000 credits; " +
            "research points given where a lesson's purchase needs them; creatures added to the journal (discovered) for the bench, colours and water; " +
            "a starter vial on the shelf, a decoration in the box and Pip's 150 sales, standing in for an order's delivery and days of sales; " +
            "a bred pair, a coloured offspring, an auto-doser batch, a day on the line and a Grand Exhibition attempt and win, standing in for the days each takes. " +
            "Every purchase, hire, order, pipe, water change and decoration is the shop's own action, as each lesson's step says.";

        /// <summary>A shop past its first night: day 2, at night, the guide done, 3,000 credits, a1 stocked with Pondlets.</summary>
        public static void Prepare(GrowthShop s)
        {
            s.Guide = GrowthGuideMode.Done;
            if (s.Day < 2) s.Day = 2;
            s.Credits = Math.Max(s.Credits, 3000);
            if (!s.Discovered.Contains("pondlet")) s.Discovered.Add("pondlet");
            foreach (var j in new[] { "pondlet/" + Colour.Classic }) if (!s.Journal.Contains(j)) s.Journal.Add(j);
            var a1 = s.ShopContainers.FirstOrDefault();
            while (a1 != null && a1.Free > 0 && a1.Occupants.Count < 3 && s.DevelopmentStock(a1.Id, "pondlet").Success) { }
            s.TakeEvents();
        }

        /// <summary>A step's target id after its kind ("Place:a2" gives "a2").</summary>
        static string TargetId(GrowthShop s, string lesson)
        {
            var t = GrowthLessons.Get(lesson).Step(s)?.Target;
            int at = t == null ? -1 : t.IndexOf(':');
            return at < 0 ? null : t.Substring(at + 1);
        }

        /// <summary>A new era: its stars (a few points past them) and 100,000 credits.</summary>
        static void Era(GrowthShop s, int era)
        {
            if (s.Era >= era) return;
            s.Era = era;
            s.Reputation = Math.Max(s.Reputation, GrowthContent.StarThresholds[era - 1] + 5);
            s.Credits = Math.Max(s.Credits, 100000);
        }

        static void Discover(GrowthShop s, string species)
        {
            if (!s.Discovered.Contains(species)) s.Discovered.Add(species);
            string key = GrowthShop.JournalKey(species, Colour.Classic);
            if (!s.Journal.Contains(key)) s.Journal.Add(key);
        }

        static void AtLeastRp(GrowthShop s, int rp) => s.Rp = Math.Max(s.Rp, rp);

        /// <summary>The day closed and run to its night (the shop's own Close for the day, its queue served).</summary>
        public static string ToNight(GrowthShop s, Action<float> advance)
        {
            if (s.Phase == GrowthPhase.Night) return null;
            var closed = s.CloseEarly();
            if (!closed.Success) return "close the day: " + closed;
            for (int i = 0; i < 4000 && s.Phase != GrowthPhase.Night; i++)
            {
                advance(0.25f);
                if (s.FrontOfQueue != null) s.Serve(s.ServeSeconds);
            }
            return s.Phase == GrowthPhase.Night ? null : "the day did not end";
        }

        /// <summary>Puts the shop where the lesson's system appears; null, or why it could not.</summary>
        public static string Stage(GrowthShop s, string id, Action<float> advance)
        {
            var lesson = GrowthLessons.Get(id);
            if (lesson == null) return "no lesson " + id;
            if (!lesson.AnyTime) { var night = ToNight(s, advance); if (night != null) return night; }
            Era(s, lesson.Era);
            switch (id)
            {
                case "wishes":
                {
                    var opened = s.OpenShop();
                    if (!opened.Success) return "open the shop: " + opened;
                    var tank = s.ShopContainers.First(c => c.Occupants.Any());
                    s.DevelopmentArrive("casual", new GrowthWish { Kind = WishKind.Species, Species = tank.Occupants[0].Species }, tank.Id, 999);
                    break;
                }
                case "vials": s.Vials["ember"] = GrowthContent.VialDoses; break;
                case "research": AtLeastRp(s, GrowthContent.Culture("spark").Rp); break;
                case "bench": Discover(s, "bubblebelly"); AtLeastRp(s, 5); break;
                case "breeding": AtLeastRp(s, 5); break;
                case "decor": if (!s.DecorOwned.Contains("reeds")) s.DecorOwned.Add("reeds"); break;
                case "colours": if (!s.TintPort) { var r = s.Upgrade("breeding"); if (!r.Success) return "the breeding tank's tint port: " + r; } break;
                case "pip": s.SalesTotal = Math.Max(s.SalesTotal, GrowthContent.PipSalesGate); break;
                case "generator": AtLeastRp(s, 30); break;
                case "vat": AtLeastRp(s, GrowthContent.CatalogItem("vat").Rp); break;
                case "water": Discover(s, GrowthContent.Species.First(x => x.Kind == SpeciesKind.Founder && x.Water == Water.Warm).Id); break;
                case "analyzer": { AtLeastRp(s, 120); var r = s.Upgrade("bench"); if (!r.Success) return "the analyzer: " + r; break; }
                case "free-play": s.ExhibitionDone = true; s.ExhibitionResults = new GrowthExhibitionResults { Day = s.Day }; break;
            }
            return null;
        }

        static string Do(GrowthResult r, string what) => r.Success ? null : what + ": " + r;

        /// <summary>Uses the lesson's system once with the shop's own actions; null, or why it could not.</summary>
        public static string Act(GrowthShop s, string id, Action<float> advance)
        {
            string e;
            switch (id)
            {
                case "upgrades": return Do(s.Upgrade("counter"), "upgrade the counter");
                case "tanks": return Do(s.BuyTank(TargetId(s, id)), "buy a starter tank");
                case "goals": s.Note("board"); return null;
                case "journal": s.Note("journal"); return null;
                case "wishes":
                {
                    foreach (var v in s.Visitors.Where(v => v.Wish != null && v.State == GrowthVisitorState.Browsing)) v.BrowseFor = v.Clock + 0.05f;
                    for (int i = 0; i < 2000 && !s.Noted.Contains("wish"); i++)
                    {
                        advance(0.1f);
                        if (s.FrontOfQueue != null) s.Serve(s.ServeSeconds);
                    }
                    return s.Noted.Contains("wish") ? null : "no wish was granted";
                }
                case "unit-b": return Do(s.BuyUnitB(), "open unit B");
                case "catalog": return Do(s.Order("grower-ember"), "order the Ember grower");
                case "vials": return Do(s.CollectVial("ember", s.HoldSeconds + 0.01f), "a dose from the vial");
                case "research": return Do(s.Research("spark"), "research Spark");
                case "bench": return Do(s.Upgrade("bench"), "buy the microscope");
                case "breeding":
                    if ((e = Do(s.Upgrade("breeding"), "buy the breeding tank")) != null) return e;
                    s.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
                    return null;
                case "large": return Do(s.BuyContainer("large", TargetId(s, id)), "buy a Large tank");
                case "tank-levels": return Do(s.UpgradeContainer(TargetId(s, id)), "upgrade a tank");
                case "decor": return Do(s.PutDecor("reeds", TargetId(s, id)), "put the reeds beside a tank");
                case "colours": s.Journal.Add(GrowthShop.JournalKey("pondlet", Colour.Sunny)); return null;
                case "pip": return Do(s.Hire("pip"), "hire Pip");
                case "annex": return Do(s.BuyAnnex(), "open the power room");
                case "generator": return Do(s.Upgrade("generator"), "buy the generator");
                case "vat": return Do(s.Order("vat"), "order the vat");
                case "pipes":
                    if (!s.HasPumps && (e = Do(s.Upgrade("pump"), "buy pumps and pipes")) != null) return e;
                    while (s.Level("grower") < 3) if ((e = Do(s.Upgrade("grower"), "upgrade the growers")) != null) return e;
                    if (!s.Buffers.Any() && (e = Do(s.BuyBuffer(0), "buy a buffer")) != null) return e;
                    return Do(s.LayPipe(GrowthLab.Tap(s.Cultures.First()), GrowthLab.Buffer(0)), "lay a pipe");
                case "power":
                    if (s.Level("generator") < 2 && (e = Do(s.Upgrade("generator"), "the bubble turbine")) != null) return e;
                    while (s.Level("pump") < 2) if ((e = Do(s.Upgrade("pump"), "electric pumps")) != null) return e;
                    return null;
                case "nori": return Do(s.Hire("nori"), "hire Nori");
                case "water": return Do(s.SetWater(TargetId(s, id), Water.Warm), "warm a tank's water");
                case "workshop": return Do(s.BuyWorkshop(), "open the workshop");
                case "unit-c": return Do(s.BuyUnitC(), "open unit C");
                case "show": return Do(s.BuyContainer("show", TargetId(s, id)), "buy a Show tank");
                case "autodoser":
                    while (s.Level("incubator") < 4) if ((e = Do(s.Upgrade("incubator"), "upgrade the incubator")) != null) return e;
                    if (!s.Pipes.Any(p => p.To == GrowthLab.Port) && (e = Do(s.LayPipe(GrowthLab.Buffer(0), GrowthLab.Port), "pipe a buffer to the port")) != null) return e;
                    s.AutoBatches = Math.Max(s.AutoBatches, 1);
                    return null;
                case "line":
                    if (!s.HasSorter && (e = Do(s.Upgrade("sorter"), "buy the sorter")) != null) return e;
                    return Do(s.LayPipe(GrowthLab.Out, GrowthLab.Sorter), "lay the chute");
                case "line-day": s.LineDays = Math.Max(s.LineDays, 1); return null;
                case "moss": return Do(s.Hire("moss"), "hire Moss");
                case "hall": return Do(s.BuyHall(), "finish the hall");
                case "exhibition": s.ExhibitionAttempts = Math.Max(s.ExhibitionAttempts, 1); return null;
                default: return null; // a card only: reading it is all
            }
        }
    }
}
