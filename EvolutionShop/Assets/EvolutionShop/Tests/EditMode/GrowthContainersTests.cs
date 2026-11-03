using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-03: size classes, Starter, Large and Show tanks on places that fit them, container levels, moving and
    /// selling, water and harmony, appeal and tolerance, visitors browsing one container at a time, pair reservations (all
    /// or none), Big spenders, the version 2 save and the densest layout (GR.5, GR.10, GR.15.1).</summary>
    public sealed class GrowthContainersTests
    {
        const string Small = "pondlet", Medium = "emberpuff", Large = "shellback";

        /// <summary>A shop far along (era 4, 4 stars, unit B open, plenty of credits) at night, with its first Starter tank.</summary>
        static GrowthShop Grown(int stars = 4, int era = 4)
        {
            return new GrowthShop { Era = era, UnitB = true, Credits = 100000, Reputation = GrowthContent.StarThresholds[stars - 1] };
        }

        static GrowthSpecimen Add(GrowthShop shop, GrowthContainer c, string species)
        {
            Assert.That(shop.DevelopmentStock(c.Id, species).Success, Is.True, species + " into " + c.Kind);
            return c.Occupants.Last();
        }

        static void Run(GrowthShop shop, float seconds, bool serve = false)
        {
            for (float t = 0; t < seconds; t += 0.25f)
            {
                shop.Tick(0.25f);
                if (serve && shop.FrontOfQueue != null) shop.Serve(0.25f);
            }
        }

        [Test]
        public void SizeClassesTakeOneTwoAndFourSpacesAndEachContainerItsLargest()
        {
            Assert.That(GrowthContent.Get(Small).Size, Is.EqualTo(SizeClass.Small));
            Assert.That(GrowthContent.Get(Medium).Size, Is.EqualTo(SizeClass.Medium));
            Assert.That(GrowthContent.Get(Large).Size, Is.EqualTo(SizeClass.Large));
            var shop = Grown();
            var starter = shop.Containers[0];
            Assert.That(shop.DevelopmentGive(Large).Success, Is.True);
            Assert.That(shop.PutIn(starter.Id).Failure, Is.EqualTo(GrowthFailure.DoesNotFit), "a Starter tank holds small and medium creatures");
            shop.Carried.Clear();
            Add(shop, starter, Medium); Add(shop, starter, Medium);
            Assert.That(starter.Used, Is.EqualTo(4));
            Assert.That(shop.DevelopmentStock(starter.Id, Small).Failure, Is.EqualTo(GrowthFailure.DoesNotFit), "two medium creatures fill four spaces");

            Assert.That(shop.BuyContainer("large", "a3").Success, Is.True);
            var large = shop.ContainerAt("a3");
            Assert.That(large.Spaces, Is.EqualTo(8));
            Add(shop, large, Large); Add(shop, large, Medium); Add(shop, large, Small); Add(shop, large, Small);
            Assert.That(large.Used, Is.EqualTo(8));
            Assert.That(shop.DevelopmentStock(large.Id, Small).Failure, Is.EqualTo(GrowthFailure.DoesNotFit));

            Assert.That(shop.BuyContainer("show", "b4").Success, Is.True);
            var show = shop.ContainerAt("b4");
            Assert.That(show.Spaces, Is.EqualTo(16));
            for (int i = 0; i < 4; i++) Add(shop, show, Large);
            Assert.That(show.Free, Is.Zero, "four large creatures fill a Show tank");
        }

        [Test]
        public void PlacesTakeContainersByTheirFootprint()
        {
            var shop = Grown();
            Assert.That(GrowthShop.Places.Select(p => p.Id), Is.EqualTo(new[] { "a1", "a2", "a3", "a4", "b1", "b2", "b3", "b4", "c1", "c2", "c3", "c4", "c5", "c6", "e1", "e2" }), "GR-10: unit C's six and the hall's two");
            Assert.That(GrowthShop.Places.Where(p => p.Size == "feature").Select(p => p.Id), Is.EqualTo(new[] { "b4", "c1", "c2" }), "unit B's feature place and unit C's feature wall");
            Assert.That(shop.BuyContainer("large", "a2").Failure, Is.EqualTo(GrowthFailure.TooBigForPlace), "a standard place takes a Starter tank only");
            Assert.That(shop.BuyContainer("show", "b1").Failure, Is.EqualTo(GrowthFailure.TooBigForPlace), "a long place is 2.5 x 1 m; a Show tank 3 x 1.5 m");
            Assert.That(shop.BuyContainer("starter", "b4").Success, Is.True, "any smaller container fits a bigger place");
            Assert.That(shop.BuyContainer("large", "b4").Failure, Is.EqualTo(GrowthFailure.PlaceTaken));
            Assert.That(shop.BuyContainer("large", "a1").Failure, Is.EqualTo(GrowthFailure.PlaceTaken), "the first Starter tank stands on a1");
            Assert.That(shop.BuyContainer("large", "b1").Success, Is.True);
            Assert.That(shop.BuyContainer("rack", "b2").Failure, Is.EqualTo(GrowthFailure.UnknownContainer), "the lab rack is not a shop container");
            Assert.That(shop.BuyContainer("large", "z9").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace));
            var closed = Grown(); closed.UnitB = false;
            Assert.That(closed.BuyContainer("starter", "b1").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "unit B's places open with it");
            Assert.That(shop.BuyContainer("starter", "c3").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "unit C's places open with it");
        }

        [Test]
        public void LargeAndShowTanksWaitForTheirEraStarsAndCreditsAndAreBoughtAtNight()
        {
            var shop = new GrowthShop { Credits = 199 };
            Assert.That(shop.BuyContainer("large", "a3").Failure, Is.EqualTo(GrowthFailure.Locked), "a Large tank needs era 2");
            shop.Era = 2;
            Assert.That(shop.BuyCheck("large", "a3", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 2 stars"));
            shop.Reputation = GrowthContent.StarThresholds[1] + 60; // two stars, with room for a refusal or two during the day
            Assert.That(shop.BuyContainer("large", "a3").Failure, Is.EqualTo(GrowthFailure.NotEnoughCredits));
            shop.Credits = 200;
            shop.Containers[0].Occupants.Add(new GrowthSpecimen { Id = 900, Species = Small });
            shop.OpenShop();
            Assert.That(shop.BuyCheck("large", "a3", out string dayWhy), Is.Not.EqualTo(GrowthFailure.NotAtNight), "F81: tanks are bought by day too (" + dayWhy + ")");
            Run(shop, GrowthContent.DaySeconds + 1);
            int credits = shop.Credits;
            Assert.That(shop.BuyContainer("large", "a3").Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(credits - 200));
            Assert.That(shop.BuyContainer("show", "b4").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "unit B is still walled");
            var grown = Grown(stars: 3, era: 3);
            Assert.That(grown.BuyCheck("show", "b4", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs Era 4"));
            grown.Era = 4;
            Assert.That(grown.BuyCheck("show", "b4", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 4 stars"));
            grown.Reputation = GrowthContent.StarThresholds[3];
            Assert.That(grown.BuyContainer("show", "b4").Success, Is.True);
            Assert.That(grown.TakeEvents().Any(e => e.Kind == GrowthEventKind.Bought && e.Text == "Show tank placed"), Is.True);
        }

        [Test]
        public void ContainerLevelsAddSpacesKeepTheirCreaturesAndFollowTheirGates()
        {
            var shop = new GrowthShop { Credits = 1000 };
            var tank = shop.Containers[0];
            Add(shop, tank, Small); Add(shop, tank, Small);
            Assert.That(shop.UpgradeContainer(tank.Id).Success, Is.True);
            Assert.That(tank.Level, Is.EqualTo(2));
            Assert.That(tank.Spaces, Is.EqualTo(6));
            Assert.That(shop.Credits, Is.EqualTo(975), "the filter costs 25");
            Assert.That(tank.Occupants, Has.Count.EqualTo(2), "an upgrade keeps every creature");
            Assert.That(shop.ContainerUpgradeCheck(tank.Id, out var next, out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(next.Level, Is.EqualTo(3));
            Assert.That(why, Is.EqualTo("Needs Era 2"));
            shop.Era = 2; shop.Reputation = GrowthContent.StarThresholds[1];
            Assert.That(shop.UpgradeContainer(tank.Id).Success, Is.True);
            Assert.That(tank.Spaces, Is.EqualTo(8));
            Assert.That(shop.UpgradeContainer(tank.Id).Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            Assert.That(shop.UpgradeContainer("tank-404").Failure, Is.EqualTo(GrowthFailure.NoSuchContainer));
            shop.BuyContainer("large", "a3");
            var large = shop.ContainerAt("a3");
            Assert.That(shop.ContainerUpgradeCheck(large.Id, out next, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs Era 3"));
            shop.OpenShop();
            shop.Era = 3; shop.Reputation = GrowthContent.StarThresholds[2];
            Assert.That(shop.UpgradeContainer(large.Id).Success, Is.True, "a tank upgrades while the shop is open too (GR-31, F33)");
        }

        [Test]
        public void ContainersMoveWithTheirCreaturesAndEmptyOnesSellForHalf()
        {
            var shop = Grown();
            var starter = shop.Containers[0];
            Add(shop, starter, Small); Add(shop, starter, Medium);
            Assert.That(shop.MoveContainer(starter.Id, "a2").Success, Is.True);
            Assert.That(starter.Place, Is.EqualTo("a2"));
            Assert.That(starter.Occupants, Has.Count.EqualTo(2), "the creatures move with their tank");
            Assert.That(shop.ContainerAt("a1"), Is.Null);
            shop.BuyContainer("large", "a1");
            var large = shop.ContainerAt("a1");
            Assert.That(shop.MoveContainer(large.Id, "a4").Failure, Is.EqualTo(GrowthFailure.TooBigForPlace));
            Assert.That(shop.MoveContainer(large.Id, "a2").Failure, Is.EqualTo(GrowthFailure.PlaceTaken));
            Assert.That(shop.MoveContainer(large.Id, "b3").Success, Is.True);
            Assert.That(shop.SellContainer(starter.Id).Failure, Is.EqualTo(GrowthFailure.NotEmpty), "creatures are never sold with a tank");
            starter.Occupants.Clear();
            Assert.That(shop.UpgradeContainer(starter.Id).Success, Is.True);
            int credits = shop.Credits;
            Assert.That(GrowthShop.Paid(starter), Is.EqualTo(45));
            Assert.That(shop.SellContainer(starter.Id).Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(credits + 22), "half of 20 + 25");
            Assert.That(shop.Container(starter.Id), Is.Null);
            Assert.That(shop.SellContainer(large.Id).Failure, Is.EqualTo(GrowthFailure.LastContainer), "the shop keeps at least one tank");
            Assert.That(shop.TakeEvents().Count(e => e.Kind == GrowthEventKind.Moved), Is.EqualTo(2));
            var day = Grown();
            day.Containers[0].Occupants.Add(new GrowthSpecimen { Id = 900, Species = Small });
            day.OpenShop();
            Assert.That(day.MoveCheck(day.Containers[0].Id, "a4", out string why), Is.Not.EqualTo(GrowthFailure.NotAtNight), "F81: tanks are moved by day too (" + why + ")");
            day.Containers[0].Occupants[0].ReservedBy = 1;
            Assert.That(day.MoveCheck(day.Containers[0].Id, "a4", out why), Is.EqualTo(GrowthFailure.Reserved), "never while a visitor is buying from it");
        }

        [Test]
        public void HarmonyNeedsEraThreeOneTemperamentAndTheContainersWater()
        {
            var shop = Grown(stars: 3, era: 2);
            var tank = shop.Containers[0];
            Add(shop, tank, "pondlet"); Add(shop, tank, "bubblebelly");
            Assert.That(shop.Harmony(tank), Is.False, "comfort comes with era 3");
            Assert.That(shop.SetWater(tank.Id, Water.Warm).Failure, Is.EqualTo(GrowthFailure.Locked));
            shop.Era = 3;
            Assert.That(shop.Harmony(tank), Is.True, "two calm fresh-water creatures in fresh water");
            Assert.That(shop.Appeal(tank), Is.EqualTo(1));
            Add(shop, tank, "ribbonfin");
            Assert.That(shop.Harmony(tank), Is.False, "a lively Ribbonfin among calm ones: no bonus, nothing worse");
            tank.Occupants.RemoveAt(2);
            Add(shop, tank, "lanternfin");
            Assert.That(shop.Harmony(tank), Is.False, "a warm-water Lanternfin in fresh water");
            int credits = shop.Credits;
            Assert.That(shop.SetWater(tank.Id, Water.Warm).Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(credits - GrowthContent.WaterKitCredits));
            Assert.That(tank.Occupants, Has.Count.EqualTo(3), "changing the water keeps every creature");
            Assert.That(shop.Harmony(tank), Is.False, "now the fresh-water ones disagree");
            Assert.That(shop.SetWater(tank.Id, Water.Warm).Failure, Is.EqualTo(GrowthFailure.SameWater));
            tank.Occupants.Clear();
            Add(shop, tank, "lanternfin"); Add(shop, tank, "kindlekoi");
            Assert.That(shop.Harmony(tank), Is.True, "calm and warm, small and medium alike");
            tank.Occupants.RemoveAt(1);
            Assert.That(shop.Harmony(tank), Is.False, "harmony needs two or more");
        }

        [Test]
        public void AppealAndSchoolsStretchABudgetUpToFortyPercent()
        {
            var shop = Grown(stars: 4, era: 4);
            shop.BuyContainer("show", "b4");
            var show = shop.ContainerAt("b4");
            var starter = shop.Containers[0];
            Add(shop, starter, "pondlet");
            Assert.That(shop.Tolerance(starter, "pondlet"), Is.EqualTo(1f).Within(1e-5));
            starter.Water = Water.Warm;
            Add(shop, starter, "pondlet"); Add(shop, starter, "pondlet");
            Assert.That(shop.Appeal(starter), Is.Zero, "fresh-water Pondlets in warm water: no harmony");
            Assert.That(shop.Tolerance(starter, "pondlet"), Is.EqualTo(1f + GrowthContent.SchoolTolerance).Within(1e-5), "a school");
            Assert.That(shop.Tolerance(starter, "pondlet"), Is.EqualTo(GrowthShop.SchoolBudgetBonus).Within(1e-5));
            starter.Water = Water.Fresh;
            Assert.That(shop.Appeal(starter), Is.EqualTo(1), "three calm fresh-water Pondlets in fresh water are in harmony");
            Assert.That(shop.Tolerance(starter, "pondlet"), Is.EqualTo(1.25f).Within(1e-5));
            for (int i = 0; i < 3; i++) Add(shop, show, "pondlet");
            Assert.That(shop.Appeal(show), Is.EqualTo(2), "a lit Show tank in harmony");
            Assert.That(shop.Tolerance(show, "pondlet"), Is.EqualTo(1.35f).Within(1e-5));
            Assert.That(GrowthContent.ToleranceCap, Is.EqualTo(0.4f));
        }

        [Test]
        public void VisitorsBrowseOneContainerAtATimeTheMostAppealingFirst()
        {
            // One star: budgets of 20 and 10. The lit Show tank is looked at first; it holds only a Brook Monarch (30).
            var shop = Grown(stars: 1, era: 4);
            var show = new GrowthContainer { Id = "tank-show", Kind = "show", Place = "b4" };
            shop.Containers.Add(show);
            var starter = shop.Containers[0];
            Add(shop, show, "brookmonarch");
            Add(shop, starter, "pondlet");
            shop.Credits = 0;
            shop.OpenShop();
            Run(shop, 4.5f);
            var v = shop.Visitors.Single();
            Assert.That(v.Plan, Is.EqualTo(new[] { show.Id, starter.Id }), "the most appealing container first");
            Assert.That(v.ContainerId, Is.EqualTo(show.Id));
            // GR-10: a visitor's look counts from the door, so one bound for unit B's Show tank has the time to walk there.
            Assert.That(GrowthSpaces.FirstLook("b4"), Is.GreaterThan(GrowthShop.BrowseSeconds));
            Assert.That(v.BrowseFor, Is.EqualTo(GrowthSpaces.FirstLook("b4")));
            Run(shop, v.BrowseFor - v.Clock - 0.5f);
            Assert.That(v.Browsed, Is.Zero, "still on the way to it and looking");
            Run(shop, 1f);
            Assert.That(v.State, Is.EqualTo(GrowthVisitorState.Browsing), "nothing affordable there: on to the next");
            Assert.That(v.Browsed, Is.EqualTo(1));
            Assert.That(v.ContainerId, Is.EqualTo(starter.Id));
            // The next look counts the walk back from unit B's Show tank too.
            Assert.That(GrowthSpaces.NextLook("b4", starter.Place), Is.GreaterThan(GrowthShop.NextBrowseSeconds));
            Assert.That(v.BrowseFor, Is.EqualTo(GrowthSpaces.NextLook("b4", starter.Place)));
            Run(shop, GrowthSpaces.NextLook("b4", starter.Place));
            Assert.That(v.State, Is.EqualTo(GrowthVisitorState.Queued));
            Assert.That(v.SpecimenIds.Single(), Is.EqualTo(starter.Occupants[0].Id));
            Assert.That(shop.Today.Refusals, Is.Zero);
            // At most three containers are looked into.
            var many = Grown(stars: 1, era: 4);
            foreach (var p in new[] { "a2", "a3", "a4", "b1" }) { many.BuyContainer("starter", p); Add(many, many.ContainerAt(p), "brookmonarch"); }
            Add(many, many.Containers[0], "brookmonarch");
            many.OpenShop();
            Run(many, 4.5f);
            Assert.That(many.Visitors[0].Plan, Has.Count.EqualTo(GrowthShop.MostBrowsed));
            Run(many, GrowthShop.BrowseSeconds + 2 * GrowthShop.NextBrowseSeconds + 0.5f);
            Assert.That(many.Visitors[0].Outcome, Is.EqualTo(GrowthVisitOutcome.NothingAffordable));
            Assert.That(many.Visitors[0].Browsed, Is.EqualTo(GrowthShop.MostBrowsed));
        }

        [Test]
        public void APairIsReservedAllOrNoneAndSoldTogether()
        {
            var shop = Grown(stars: 3, era: 3);
            var tank = shop.Containers[0];
            var a = Add(shop, tank, "pondlet"); var b = Add(shop, tank, "pondlet"); var c = Add(shop, tank, "bubblebelly");
            var first = new GrowthVisitor { Id = 501, Type = "collector", Budget = 90 };
            var second = new GrowthVisitor { Id = 502, Type = "collector", Budget = 90 };
            Assert.That(shop.ReserveAll(first, new[] { a, b }), Is.True);
            Assert.That(a.ReservedBy == 501 && b.ReservedBy == 501, Is.True);
            Assert.That(shop.ReserveAll(second, new[] { b, c }), Is.False, "one of them is taken: none is reserved");
            Assert.That(c.ReservedBy, Is.Zero);
            Assert.That(second.SpecimenIds, Is.Empty);
            Assert.That(shop.ReserveAll(second, new[] { c, c }), Is.False, "the same specimen twice");
            shop.BuyContainer("starter", "a2");
            var other = Add(shop, shop.ContainerAt("a2"), "pondlet");
            Assert.That(shop.ReserveAll(second, new[] { c, other }), Is.False, "a pair comes from one container");
            Assert.That(shop.TakeOut(tank.Id, a.Id).Failure, Is.EqualTo(GrowthFailure.Reserved));

            // A collector at three stars chooses a pair of one species from what they look into; the counter sells both.
            var day = Grown(stars: 3, era: 3);
            var stock = day.Containers[0];
            for (int i = 0; i < 4; i++) Add(day, stock, "pondlet");
            var collector = new GrowthVisitor { Id = 700, Type = "collector", Budget = 90 };
            var pick = day.PickFrom(collector, stock);
            Assert.That(pick, Has.Count.EqualTo(2));
            Assert.That(pick.All(s => s.Species == "pondlet"), Is.True);
            var casual = new GrowthVisitor { Id = 701, Type = "casual", Budget = 45 };
            Assert.That(day.PickFrom(casual, stock), Has.Count.EqualTo(1), "only Collectors buy pairs");
            var early = Grown(stars: 2, era: 2);
            for (int i = 0; i < 4; i++) Add(early, early.Containers[0], "pondlet");
            Assert.That(early.PickFrom(new GrowthVisitor { Id = 702, Type = "collector", Budget = 60 }, early.Containers[0]), Has.Count.EqualTo(1), "pairs come at three stars");
            day.Reputation += 100; // still three stars after a few unserved visitors give up (-1 each)
            day.OpenShop();
            GrowthVisitor pairBuyer = null;
            for (int i = 0; i < 1200 && pairBuyer == null && day.Phase == GrowthPhase.Day; i++)
            {
                day.Tick(0.25f);
                pairBuyer = day.Queue.FirstOrDefault(v => v.SpecimenIds.Count == 2);
            }
            Assert.That(pairBuyer, Is.Not.Null, "a collector came and reserved a pair");
            while (day.FrontOfQueue != pairBuyer) Assert.That(day.Serve(5).Success, Is.True);
            int credits = day.Credits, sales = day.SalesTotal, rep = day.Reputation;
            Assert.That(day.Serve(5).Detail, Is.EqualTo("sold"));
            Assert.That(day.Credits, Is.EqualTo(credits + 12), "two Pondlets at 6");
            Assert.That(day.SalesTotal, Is.EqualTo(sales + 2));
            Assert.That(day.Reputation, Is.EqualTo(rep + 2 * GrowthContent.RepPerSale));
            Assert.That(day.TakeEvents().Any(e => e.Text == "Sold a pair of Pondlets for 12 credits"), Is.True);
        }

        [Test]
        public void BigSpendersFavourBiggerCreaturesAndOthersTheDearest()
        {
            var shop = Grown(stars: 3, era: 3);
            shop.BuyContainer("large", "a3");
            var tank = shop.ContainerAt("a3");
            var monarch = Add(shop, tank, "brookmonarch"); // small, 30 credits
            var ember = Add(shop, tank, "emberpuff");      // medium, 24 credits
            var premium = new GrowthVisitor { Id = 800, Type = "premium", Budget = 120 };
            Assert.That(shop.PickFrom(premium, tank).Single(), Is.SameAs(ember), "a medium creature weighs 1.5 times its price");
            var casual = new GrowthVisitor { Id = 801, Type = "casual", Budget = 45 };
            Assert.That(shop.PickFrom(casual, tank).Single(), Is.SameAs(monarch), "the dearest within budget");
            var bargain = new GrowthVisitor { Id = 802, Type = "bargain", Budget = 20 };
            Assert.That(shop.PickFrom(bargain, tank), Is.Null, "nothing within 20 credits");
        }

        [Test]
        public void TheVersionTwoSaveKeepsContainersAndAVersionOneSaveLoads()
        {
            var shop = Grown(stars: 4, era: 4);
            shop.BuyContainer("show", "b4");
            shop.BuyContainer("large", "a3");
            var large = shop.ContainerAt("a3");
            Add(shop, large, Large); Add(shop, large, Medium);
            shop.UpgradeContainer(large.Id);
            shop.SetWater(large.Id, Water.Brine);
            shop.MoveContainer(shop.Containers[0].Id, "a2");
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            Assert.That(back.ContainerAt("a3").Water, Is.EqualTo(Water.Brine));
            Assert.That(back.ContainerAt("a3").Level, Is.EqualTo(2));
            Assert.That(back.ContainerAt("b4").Kind, Is.EqualTo("show"));
            Assert.That(GrowthSave.CurrentVersion, Is.GreaterThanOrEqualTo(2), "version 2 or later (GR-04 is 3)");

            var v1 = JsonUtility.FromJson<GrowthSave>(json);
            v1.version = 1;
            foreach (var box in v1.containers) box.water = null;
            var old = GrowthShop.Restore(v1, out problem);
            Assert.That(problem, Is.Null, "a GR-02 save loads");
            Assert.That(old.Containers.All(c => c.Water == Water.Fresh), Is.True);

            var bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.containers[0].water = "lava";
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null);
            Assert.That(problem, Is.EqualTo("Unknown water lava"));
            bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.containers[1].place = bad.containers[0].place;
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null);
            Assert.That(problem, Is.EqualTo("Two containers in one place"));
            bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.containers[0].place = "z9";
            Assert.That(GrowthShop.Restore(bad, out _), Is.Null);
            bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.version = 99; // a version from the future
            Assert.That(GrowthShop.Restore(bad, out _), Is.Null);
        }

        [Test]
        public void TheDensestLayoutFillsEveryPlaceWithTheLargestContainerAtItsHighestLevel()
        {
            var shop = GrowthShop.Densest();
            Assert.That(shop.Containers.Select(c => c.Place), Is.EqualTo(GrowthShop.UnitAPlaces.Concat(GrowthShop.UnitBPlaces)));
            Assert.That(shop.Containers.Select(c => c.Kind), Is.EqualTo(new[] { "large", "starter", "large", "starter", "large", "large", "large", "show" }));
            Assert.That(shop.Containers.All(c => c.Level == c.Def.Levels.Length && c.Free == 0), Is.True);
            Assert.That(shop.Stock.Count(), Is.EqualTo(120), "16 x 5 + 8 x 2 + 24 spaces of small creatures");
            Assert.That(shop.Stock.Select(s => s.Species).Distinct().Count(), Is.EqualTo(21));
            Assert.That(shop.Containers.All(c => c.Occupants.GroupBy(s => s.Species).Any(g => g.Count() >= 3)), Is.True, "every container has a school");
            Assert.That(shop.Stock.Select(s => s.Id).Distinct().Count(), Is.EqualTo(120));
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(shop.Snapshot())), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(back.Stock.Count(), Is.EqualTo(120));
            // GR-10's final layout: unit C and the hall too, every place: 264 small creatures.
            var final = GrowthShop.Densest(5, final: true);
            Assert.That((final.Containers.Count, final.Stock.Count(), final.Containers.Count(c => c.Kind == "show")), Is.EqualTo((16, 264, 3)));
            Assert.That(GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(final.Snapshot())), out problem), Is.Not.Null, problem);
            // A whole day in it, served at the counter: visitors browse, reserve and buy without a rule breaking.
            shop.OpenShop();
            for (int i = 0; i < 2000 && shop.Phase == GrowthPhase.Day; i++)
            {
                shop.Tick(0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(0.5f);
                Assert.That(shop.Stock.Count(s => s.ReservedBy != 0), Is.EqualTo(shop.Queue.Sum(v => v.SpecimenIds.Count)), "every reservation belongs to someone queuing");
            }
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            Assert.That(shop.LastDay.Sales, Is.GreaterThan(30));
            Assert.That(shop.Stock.All(s => s.ReservedBy == 0), Is.True);
        }
    }
}
