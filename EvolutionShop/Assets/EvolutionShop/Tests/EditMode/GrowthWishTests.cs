using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-20: visitors' wishes (GR.61, GR.62): every ordinary visitor comes in wishing for something the shop could
    /// offer them (a species it has discovered, never a crown; a colour it has made; a size once it knows two; from 3 stars a
    /// matching pair or a school of three), fixed by the day and their place in it; they look first into the tanks that hold it
    /// and take it over a dearer creature, within their budget as the tank stretches it; a sale that grants it tips a quarter of
    /// the price (at least a credit) and earns a reputation point, by the player or Pip; and the day counts the wishes, those
    /// granted, the tips and what was wished for and not granted. Exhibition guests keep their own orders.</summary>
    public sealed class GrowthWishTests
    {
        static int nextId = 9000;

        /// <summary>A shop at night with stars, the Brookkin's three founders discovered, and its first tank.</summary>
        static GrowthShop Shop(int stars, params string[] discovered)
        {
            var shop = new GrowthShop { Era = 4, UnitB = true, Credits = 100000, Reputation = GrowthContent.StarThresholds[stars - 1] + 5 };
            shop.Discovered.AddRange(discovered.Length > 0 ? discovered : new[] { "pondlet", "bubblebelly", "ribbonfin" });
            foreach (var d in shop.Discovered) shop.Journal.Add(GrowthShop.JournalKey(d, Colour.Classic));
            return shop;
        }

        static GrowthSpecimen Stock(GrowthShop shop, GrowthContainer c, string species, Colour colour = Colour.Classic)
        {
            Assert.That(shop.DevelopmentStock(c.Id, species).Success, Is.True, species + " into " + c.Kind);
            var s = c.Occupants.Last();
            s.Colour = colour;
            return s;
        }

        static GrowthVisitor Visitor(string type, int budget, GrowthWish wish) => new GrowthVisitor { Id = nextId++, Type = type, Budget = budget, Wish = wish };

        static IEnumerable<GrowthWish> Wishes(GrowthShop shop, string type, int budget, int count = 300) => Enumerable.Range(0, count).Select(n => shop.WishFor(type, budget, n));

        [Test]
        public void EveryOrdinaryVisitorComesInWishingForWhatTheShopHasDiscovered()
        {
            var shop = Shop(2, "pondlet", "bubblebelly");
            var tank = shop.Containers[0];
            for (int i = 0; i < 3; i++) Stock(shop, tank, "pondlet");
            Assert.That(shop.OpenShop().Success, Is.True);
            var seen = new HashSet<int>();
            for (float t = 0; t < 120; t += 0.25f)
            {
                shop.Tick(0.25f);
                foreach (var v in shop.Visitors) seen.Add(v.Id);
                foreach (var v in shop.Visitors.Where(x => x.Type != GrowthShop.GuestType))
                {
                    Assert.That(v.Wish, Is.Not.Null, v.Type + " " + v.Id + " wishes");
                    // One size known and no colour made, below 3 stars: a named species, one they could pay for.
                    Assert.That(v.Wish.Kind, Is.EqualTo(WishKind.Species));
                    Assert.That(new[] { "pondlet", "bubblebelly" }, Does.Contain(v.Wish.Species));
                    Assert.That(GrowthContent.Get(v.Wish.Species).Price, Is.LessThanOrEqualTo(v.Budget));
                }
                if (shop.FrontOfQueue != null) shop.Serve(0.25f);
            }
            Assert.That(seen.Count, Is.GreaterThan(2), "a morning's visitors came in");
            Assert.That(shop.Today.Wishes, Is.EqualTo(seen.Count), "every one counted as a wish");
            // The same day, the same places in it: the same wishes (no luck in what the player is owed).
            var again = Shop(2, "pondlet", "bubblebelly");
            Assert.That(Enumerable.Range(0, 40).Select(n => again.WishFor("casual", 30, n)?.Text), Is.EqualTo(Enumerable.Range(0, 40).Select(n => shop.WishFor("casual", 30, n)?.Text)));
            Assert.That(new GrowthShop().WishFor("casual", 20, 0), Is.Null, "nothing discovered: nothing to wish for");
            Assert.That(shop.WishFor(GrowthShop.GuestType, 1000, 0), Is.Null, "exhibition guests keep their own orders");
        }

        [Test]
        public void AWishAsksOnlyForWhatTheShopCouldOfferThatVisitor()
        {
            // Crowns never; only what they could pay for as it is.
            var crowned = Shop(4, "pondlet", "bubblebelly", "ribbonfin", "puddlepup", "reedwhisker", "brookmonarch");
            foreach (var w in Wishes(crowned, "casual", 400).Where(w => w.Kind == WishKind.Species))
                Assert.That(GrowthContent.Get(w.Species).Kind, Is.Not.EqualTo(SpeciesKind.Crown), w.Text);
            foreach (var w in Wishes(crowned, "casual", 9).Where(w => w.Kind == WishKind.Species))
                Assert.That(GrowthContent.Get(w.Species).Price, Is.LessThanOrEqualTo(9), w.Text);
            // A colour only once the shop has made one, in a species the visitor could pay for.
            var shop = Shop(2);
            Assert.That(Wishes(shop, "casual", 30).Any(w => w.Kind == WishKind.Colour), Is.False, "no colour made yet");
            shop.Journal.Add(GrowthShop.JournalKey("bubblebelly", Colour.Sunny));
            var colours = Wishes(shop, "casual", 30).Where(w => w.Kind == WishKind.Colour).ToList();
            Assert.That(colours, Is.Not.Empty);
            Assert.That(colours.All(w => w.Colour == Colour.Sunny), Is.True, "only the colour made");
            Assert.That(Wishes(shop, "casual", 7).Any(w => w.Kind == WishKind.Colour), Is.False, "a Sunny Bubblebelly costs more than they have");
            // A size once the shop knows two; bargain hunters any small one, big spenders a medium or a large one.
            Assert.That(Wishes(shop, "casual", 200).Any(w => w.Kind == WishKind.Size), Is.False, "every creature small: no choice of size");
            var sizes = Shop(4, "pondlet", "bubblebelly", "emberpuff", "lanternfin");
            Assert.That(GrowthContent.Get("emberpuff").Size, Is.EqualTo(SizeClass.Medium), "the fixture's medium creature");
            Assert.That(Wishes(sizes, "casual", 200).Where(w => w.Kind == WishKind.Size).Select(w => w.Size).Distinct(), Is.EquivalentTo(new[] { SizeClass.Small, SizeClass.Medium }));
            Assert.That(Wishes(sizes, "bargain", 200).Where(w => w.Kind == WishKind.Size).All(w => w.Size == SizeClass.Small), Is.True);
            Assert.That(Wishes(sizes, "premium", 320).Where(w => w.Kind == WishKind.Size).All(w => w.Size != SizeClass.Small), Is.True);
            // Pairs and schools from 3 stars, at a price the visitor could pay for two or three.
            Assert.That(Wishes(Shop(2), "collector", 60).Any(w => w.Kind == WishKind.Pair || w.Kind == WishKind.School), Is.False, "not before 3 stars");
            var three = Shop(3);
            Assert.That(Wishes(three, "collector", 90).Count(w => w.Kind == WishKind.Pair), Is.GreaterThan(0));
            Assert.That(Wishes(three, "premium", 120).Count(w => w.Kind == WishKind.School), Is.GreaterThan(0));
            Assert.That(Wishes(three, "premium", 17).Any(w => w.Kind == WishKind.School), Is.False, "three of the cheapest cost 18");
            Assert.That(Wishes(three, "casual", 45).Any(w => w.Kind == WishKind.Pair || w.Kind == WishKind.School), Is.False, "casual visitors wish for one creature");
            // Each type's kinds, all of them showing over a day's worth of visitors.
            var all = Shop(4, "pondlet", "bubblebelly", "emberpuff", "lanternfin");
            all.Journal.Add(GrowthShop.JournalKey("pondlet", Colour.Deep));
            foreach (var (type, budget) in new[] { ("casual", 60), ("bargain", 28), ("collector", 130), ("premium", 200) })
                Assert.That(Wishes(all, type, budget).Select(w => w.Kind).Distinct(), Is.EquivalentTo(GrowthWishes.Kinds[type].Select(k => k.kind)), type);
            // Bargain hunters wish for the cheaper half of the species, big spenders the dearer half.
            var bargains = Wishes(sizes, "bargain", 30).Where(w => w.Kind == WishKind.Species).Select(w => w.Species).Distinct().ToList();
            var dear = Wishes(sizes, "premium", 320).Where(w => w.Kind == WishKind.Species).Select(w => w.Species).Distinct().ToList();
            Assert.That(bargains.Max(id => GrowthContent.Get(id).Price), Is.LessThanOrEqualTo(dear.Min(id => GrowthContent.Get(id).Price)));
        }

        [Test]
        public void AWishingVisitorLooksFirstIntoTheTanksThatHoldTheirWish()
        {
            // a1 holds Pondlets; b1 is more appealing (decor on its top) and holds nothing they could wish for (not discovered).
            var shop = Shop(2, "pondlet");
            var a1 = shop.Containers[0];
            Assert.That(shop.BuyContainer("starter", "b1").Success, Is.True);
            var b1 = shop.ContainerAt("b1");
            b1.Decor = "reeds";
            Stock(shop, a1, "pondlet"); Stock(shop, b1, "ribbonfin");
            Assert.That(shop.Appeal(b1), Is.GreaterThan(shop.Appeal(a1)));
            shop.OpenShop();
            for (int i = 0; i < 40 && !shop.Visitors.Any(); i++) shop.Tick(0.25f);
            var v = shop.Visitors.First();
            Assert.That(v.Wish.Species, Is.EqualTo("pondlet"));
            Assert.That(v.Plan.First(), Is.EqualTo(a1.Id), "the tank with their wish before the more appealing one");
            Assert.That(v.Plan, Does.Contain(b1.Id), "then the rest as before");
        }

        [Test]
        public void AWishIsTakenOverADearerCreatureWithinBudgetAndAPairOrSchoolAllOrNone()
        {
            var shop = Shop(4, "pondlet", "bubblebelly", "ribbonfin", "lilyfin");
            var tank = shop.Containers[0];
            tank.Level = 3;
            var pondlet = Stock(shop, tank, "pondlet");
            var sunny = Stock(shop, tank, "bubblebelly", Colour.Sunny);
            Stock(shop, tank, "lilyfin");
            // Without a wish, the dearest: the Lilyfin (12). Wishing for a Pondlet (6), the Pondlet.
            Assert.That(shop.PickFrom(Visitor("casual", 60, null), tank).Single().Species, Is.EqualTo("lilyfin"));
            Assert.That(shop.PickFrom(Visitor("casual", 60, new GrowthWish { Kind = WishKind.Species, Species = "pondlet" }), tank).Single().Id, Is.EqualTo(pondlet.Id));
            Assert.That(shop.PickFrom(Visitor("casual", 60, new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Sunny }), tank).Single().Id, Is.EqualTo(sunny.Id));
            // Not here: they buy as they would have. Over budget, even as the tank stretches it: not taken.
            Assert.That(shop.PickFrom(Visitor("casual", 60, new GrowthWish { Kind = WishKind.Species, Species = "ribbonfin" }), tank).Single().Species, Is.EqualTo("lilyfin"));
            Assert.That(shop.PickFrom(Visitor("bargain", 5, new GrowthWish { Kind = WishKind.Species, Species = "pondlet" }), tank), Is.Null, "a Pondlet costs 6");
            // A pair takes two of one species; a school three; never two kinds mixed, and not while one is reserved.
            Stock(shop, tank, "pondlet");
            var pair = shop.PickFrom(Visitor("collector", 130, new GrowthWish { Kind = WishKind.Pair }), tank);
            Assert.That(pair.Count == 2 && pair.All(s => s.Species == "pondlet"), Is.True, "two Pondlets");
            Assert.That(shop.PickFrom(Visitor("premium", 200, new GrowthWish { Kind = WishKind.School }), tank).Count, Is.EqualTo(1), "no three of a kind: not a school, so their own pick");
            var third = Stock(shop, tank, "pondlet");
            var school = shop.PickFrom(Visitor("premium", 200, new GrowthWish { Kind = WishKind.School }), tank);
            Assert.That(school.Count == 3 && school.All(s => s.Species == "pondlet"), Is.True, "three Pondlets");
            var other = Visitor("casual", 500, null);
            Assert.That(shop.ReserveAll(other, new List<GrowthSpecimen> { third }), Is.True);
            Assert.That(shop.PickFrom(Visitor("premium", 200, new GrowthWish { Kind = WishKind.School }), tank).Count, Is.EqualTo(1), "one of them reserved: no school left");
            // What grants a wish.
            Assert.That(new GrowthWish { Kind = WishKind.Pair }.Matches(new[] { pondlet, sunny }), Is.False, "a pair is of one species");
            Assert.That(new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Small }.Matches(new[] { pondlet }), Is.True);
            Assert.That(new GrowthWish { Kind = WishKind.School }.Matches(new[] { pondlet }), Is.False);
        }

        [Test]
        public void AGrantedWishTipsAQuarterAndEarnsAReputationPointByWhoeverServes()
        {
            Assert.That(new[] { 1, 2, 6, 8, 10, 100 }.Select(GrowthWishes.Tip), Is.EqualTo(new[] { 1, 1, 2, 2, 3, 25 }), "a quarter, rounded, at least a credit");
            var shop = Shop(2);
            var tank = shop.Containers[0];
            shop.OpenShop();
            shop.TakeEvents();
            var wanted = Stock(shop, tank, "bubblebelly");
            var other = Stock(shop, tank, "ribbonfin");
            var wishing = Visitor("casual", 30, new GrowthWish { Kind = WishKind.Species, Species = "bubblebelly" });
            wishing.State = GrowthVisitorState.Queued; wishing.Patience = 40;
            Assert.That(shop.ReserveAll(wishing, new List<GrowthSpecimen> { wanted }), Is.True);
            shop.Visitors.Add(wishing);
            Assert.That(shop.WouldGrant(wishing), Is.True);
            Assert.That(shop.ReservedText(wishing), Is.EqualTo("a Bubblebelly"));
            int credits = shop.Credits, rep = shop.Reputation;
            Assert.That(shop.Serve(5f).Detail, Is.EqualTo("sold"));
            int price = GrowthContent.Get("bubblebelly").Price;
            Assert.That(shop.Credits, Is.EqualTo(credits + price + GrowthWishes.Tip(price)), "the price and the tip");
            Assert.That(shop.Reputation, Is.EqualTo(rep + GrowthContent.RepPerSale + GrowthContent.RepPerWish), "a sale's points and a wish's");
            Assert.That(wishing.WishGranted && !wishing.WishMissed, Is.True);
            var events = shop.TakeEvents();
            Assert.That(events.Single(e => e.Kind == GrowthEventKind.Sale).Text, Is.EqualTo("Sold Bubblebelly for " + price + " credits, a wish granted: a tip of " + GrowthWishes.Tip(price)));
            Assert.That(events.Single(e => e.Kind == GrowthEventKind.WishGranted).Text, Is.EqualTo("A wish granted: a Bubblebelly, with a tip of " + GrowthWishes.Tip(price) + " credits"));
            // Bought something else: no tip, no point, the wish missed.
            var settled = Visitor("casual", 30, new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Deep });
            settled.State = GrowthVisitorState.Queued; settled.Patience = 40;
            Assert.That(shop.ReserveAll(settled, new List<GrowthSpecimen> { other }), Is.True);
            shop.Visitors.Add(settled);
            Assert.That(shop.WouldGrant(settled), Is.False);
            credits = shop.Credits; rep = shop.Reputation;
            Assert.That(shop.Serve(5f).Detail, Is.EqualTo("sold"));
            Assert.That((shop.Credits, shop.Reputation), Is.EqualTo((credits + GrowthContent.Get("ribbonfin").Price, rep + GrowthContent.RepPerSale)));
            Assert.That(settled.WishMissed && !settled.WishGranted, Is.True);
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.WishGranted), Is.False);
            // A visit that ends unsold (tired of waiting): the wish missed, and the day says what was wished for.
            var tired = Visitor("casual", 30, new GrowthWish { Kind = WishKind.Species, Species = "pondlet" });
            tired.State = GrowthVisitorState.Queued; tired.Patience = 0.05f;
            Assert.That(shop.ReserveAll(tired, new List<GrowthSpecimen> { Stock(shop, tank, "pondlet") }), Is.True);
            shop.Visitors.Add(tired);
            shop.Tick(0.1f);
            Assert.That(tired.Outcome == GrowthVisitOutcome.TiredOfWaiting && tired.WishMissed && !tired.WishGranted, Is.True, "tired of waiting: the wish missed");
            Assert.That(shop.Today.WishesMissed.TryGetValue("a Pondlet", out int missedPondlets) && missedPondlets >= 1, Is.True, "the day lists it");
            // Pip's sale grants it too.
            var pipShop = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 50000, Rp = 500, SalesTotal = GrowthContent.PipSalesGate };
            pipShop.Discovered.Add("pondlet");
            Assert.That(pipShop.Hire("pip").Success, Is.True);
            pipShop.OpenShop();
            var pond = Stock(pipShop, pipShop.Containers[0], "pondlet");
            var pv = Visitor("casual", 500, new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Small });
            pv.State = GrowthVisitorState.Queued; pv.Patience = 40;
            Assert.That(pipShop.ReserveAll(pv, new List<GrowthSpecimen> { pond }), Is.True);
            pipShop.Visitors.Add(pv);
            for (int i = 0; i < 40 && !pv.WishGranted; i++) pipShop.Tick(0.1f);
            Assert.That(pv.WishGranted, Is.True, "Pip's sale granted it");
            Assert.That(pipShop.Today.Tips, Is.EqualTo(GrowthWishes.Tip(GrowthContent.Get("pondlet").Price)));
        }

        [Test]
        public void TheDayCountsTheWishesGrantedTheTipsAndWhatWasWishedForAndNotGranted()
        {
            var shop = Shop(2, "pondlet", "bubblebelly", "ribbonfin");
            var tank = shop.Containers[0];
            for (int i = 0; i < 4; i++) Stock(shop, tank, "pondlet");
            int credits = shop.Credits;
            shop.OpenShop();
            for (float t = 0; t < GrowthContent.DaySeconds + 1 && shop.Phase == GrowthPhase.Day; t += 0.25f)
            {
                shop.Tick(0.25f);
                if (shop.FrontOfQueue != null) shop.Serve(0.25f);
                if (!tank.Occupants.Any(s => s.ReservedBy == 0) && tank.Occupants.Count < 4) Stock(shop, tank, "pondlet");
            }
            var d = shop.LastDay;
            Assert.That(d, Is.Not.Null);
            Assert.That(d.Wishes, Is.EqualTo(d.Visitors), "everyone wished");
            Assert.That(d.WishesGranted, Is.GreaterThan(0), "some wished for a Pondlet");
            Assert.That(d.WishesGranted, Is.LessThan(d.Wishes), "some wished for what was not in stock");
            Assert.That(d.Tips, Is.EqualTo(d.WishesGranted * GrowthWishes.Tip(GrowthContent.Get("pondlet").Price)), "a Pondlet's tip each");
            Assert.That(shop.Credits, Is.EqualTo(credits + d.Revenue), "the day's takings, tips in them");
            Assert.That(d.WishesMissed.Keys, Is.SubsetOf(new[] { "a Bubblebelly", "a Ribbonfin", "a Pondlet" }));
            Assert.That(d.WishesMissed.Values.Sum() + d.WishesGranted, Is.EqualTo(d.Wishes), "every wish granted or missed");
            Assert.That(d.WishesMissed.ContainsKey("a Bubblebelly") || d.WishesMissed.ContainsKey("a Ribbonfin"), Is.True, "what was not in stock is listed");
        }

        [Test]
        public void WishesAreSaidInWords()
        {
            Assert.That(new GrowthWish { Kind = WishKind.Species, Species = "pondlet" }.Text, Is.EqualTo("a Pondlet"));
            Assert.That(GrowthWishes.Article("Inkling"), Is.EqualTo("an Inkling"));
            Assert.That(new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Shimmer }.Text, Is.EqualTo("a Shimmer creature"));
            Assert.That(new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Large }.Text, Is.EqualTo("a large creature"));
            Assert.That(new GrowthWish { Kind = WishKind.Pair }.Text, Is.EqualTo("a matching pair"));
            Assert.That(new GrowthWish { Kind = WishKind.School }.Text, Is.EqualTo("a school of three"));
        }
    }
}
