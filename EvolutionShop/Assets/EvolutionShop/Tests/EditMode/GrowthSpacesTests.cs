using System;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-10: GR.11's spaces in the greybox: the rooms and their proportions, every place's position checked (inside
    /// its room, clear of the others, its browse access, every walk through doorways only and clear of the tanks, the 1.6 m
    /// main aisle, the queue inside the shop), shop unit C opened from unit B at 4 stars, the exhibition hall finish at 5
    /// stars, helpers walking into unit C, and the version 9 save (GR.11, GR.15.1).</summary>
    public sealed class GrowthSpacesTests
    {
        static GrowthShop At(int era, int stars, int credits = 50000) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = 500, UnitB = true };

        static GrowthSpecimen Stock(GrowthShop shop, GrowthContainer c, string species)
        {
            Assert.That(shop.DevelopmentStock(c.Id, species).Success, Is.True);
            return c.Occupants.Last();
        }

        [Test]
        public void TheLayoutKeepsGR11sProportionsAndNavigation()
        {
            Assert.That(GrowthSpaces.Problems(), Is.Empty);
            // The rooms: the shop's units at 3.2 m, the lab at 3.6 m; unit C 12.5 x 6.25 m behind unit B and the back room
            // (10 x 5 m before GR-34 made every room a quarter larger each way).
            var c = GrowthSpaces.RoomById("unit-c").Area;
            Assert.That((c.X1 - c.X0, c.Z1 - c.Z0), Is.EqualTo((12.5f, 6.25f)));
            Assert.That(GrowthSpaces.Rooms.Where(r => r.Shop).All(r => r.Ceiling == 3.2f) && GrowthSpaces.Rooms.Where(r => !r.Shop).All(r => r.Ceiling == 3.6f), Is.True);
            var unitC = GrowthContent.Expansions.First(e => e.Id == "unit-c");
            Assert.That((unitC.Width, unitC.Depth), Is.EqualTo((12.5f, 6.25f)));
            // Places: A 4, B 4, C 6 (two of them feature places for Show tanks), the hall 2: sixteen in all (GR.11's tier 5).
            Assert.That(GrowthShop.Places.Length, Is.EqualTo(16));
            Assert.That(GrowthShop.Places.GroupBy(p => p.Unit).ToDictionary(g => g.Key, g => g.Count()),
                Is.EquivalentTo(new System.Collections.Generic.Dictionary<string, int> { ["unit-a"] = 4, ["unit-b"] = 4, ["unit-c"] = 6, ["hall"] = 2 }));
            var feature = GrowthShop.Places.Where(p => p.Unit == "unit-c" && p.Size == "feature").Select(p => p.Id).ToList();
            Assert.That(feature, Is.EqualTo(new[] { "c1", "c2" }));
            Assert.That(feature.All(id => GrowthSpaces.SpotOf(id).Yaw == 180 && GrowthSpaces.Footprint(id).Z1 > c.Z1 - 0.2f), Is.True, "the feature wall is unit C's north wall, facing into the room");
            Assert.That(GrowthShop.Places.All(p => GrowthSpaces.SpotOf(p.Id) != null && GrowthWalk.Places.ContainsKey(p.Id)), Is.True, "every place has its position and a helper's spot");
            // The main aisle runs from inside the entrance through unit A and unit B to unit C's arch.
            Assert.That(GrowthSpaces.Spine.First(), Is.EqualTo("Hub"));
            Assert.That(GrowthSpaces.Points[GrowthSpaces.Spine.Last()], Is.EqualTo(GrowthPlan.At(10.9f, 4f)));
        }

        [Test]
        public void TheChecksFindABrokenLayout()
        {
            // Each break is put back afterwards: the layout's own data, moved for a moment.
            void Broken(string spot, (float x, float z) centre, (float x, float z) browse, string expected)
            {
                var s = GrowthSpaces.SpotOf(spot);
                var (oldCentre, oldBrowse) = (s.Centre, s.Browse);
                try { s.Centre = centre; s.Browse = browse; Assert.That(GrowthSpaces.Problems(), Has.Some.Contains(expected), spot); }
                finally { s.Centre = oldCentre; s.Browse = oldBrowse; }
            }
            // GR-34: each break as a move from where the place stands now.
            (float x, float z) Off(string spot, bool browse, float dx, float dz) { var s = GrowthSpaces.SpotOf(spot); var p = browse ? s.Browse : s.Centre; return (p.x + dx, p.z + dz); }
            Broken("b4", Off("b4", false, 0, 1.2f), Off("b4", true, 0, 1.2f), "the main aisle passes");
            Broken("c3", Off("c1", false, 0, -0.55f), Off("c3", true, 0, 0), "c3 overlaps c1");
            Broken("c4", Off("c4", false, 0, -0.6f), Off("c4", true, 0, -0.6f), "c4 is not inside its room");
            Broken("a1", Off("a1", false, 0, 0), Off("a1", true, 0, -1f), "a1's browse spot is not in front of it");
            Broken("e1", Off("e1", false, 0, 0), Off("e1", true, 0, -2.9f), "goes through a wall");
            Broken("b3", Off("e1", false, 1.95f, 2.0f), Off("e1", false, 1.95f, 0.85f), "b3's browse access is blocked by e1");
            Assert.That(GrowthSpaces.Problems(), Is.Empty, "and back as it was");
        }

        [Test]
        public void VisitorsHaveTheTimeToWalkToFarTanks()
        {
            // A visitor's look counts from the door: unit A's places keep the usual 6 s; farther ones add the walk at 2 m/s.
            Assert.That(GrowthShop.UnitAPlaces.All(p => GrowthSpaces.FirstLook(p) == GrowthShop.BrowseSeconds), Is.True);
            foreach (var p in GrowthShop.Places.Select(x => x.Id))
                Assert.That(GrowthSpaces.FirstLook(p), Is.GreaterThanOrEqualTo(GrowthSpaces.Length(GrowthSpaces.VisitorWalk(p)) / GrowthSpaces.VisitorSpeed + GrowthSpaces.LookSeconds - 1e-4f), p);
            Assert.That(new[] { "c1", "c2", "c3", "c4", "c5", "c6" }.Min(GrowthSpaces.FirstLook), Is.GreaterThan(new[] { "b1", "b2", "b3", "b4" }.Max(GrowthSpaces.FirstLook)));
            // In a shop whose only stock is in unit C, a visitor browses there for the walk and a look.
            var shop = At(4, 4);
            Assert.That(shop.BuyUnitC().Success && shop.BuyContainer("starter", "c4").Success, Is.True);
            shop.Containers.RemoveAll(c => c.Place == "a1");
            Stock(shop, shop.ContainerAt("c4"), "pondlet");
            shop.OpenShop();
            for (int i = 0; i < 200 && shop.Visitors.Count == 0; i++) shop.Tick(0.1f);
            var v = shop.Visitors.Single();
            Assert.That((v.ContainerId, v.BrowseFor), Is.EqualTo((shop.ContainerAt("c4").Id, GrowthSpaces.FirstLook("c4"))));
        }

        [Test]
        public void UnitCOpensFromUnitBAtFourStarsAtNight()
        {
            var shop = At(4, 4);
            shop.UnitB = false;
            Assert.That(shop.UnitCCheck(out string why), Is.EqualTo(GrowthFailure.Locked)); Assert.That(why, Does.StartWith("Open unit B first"));
            shop.UnitB = true;
            Assert.That(At(3, 4).UnitCCheck(out why), Is.EqualTo(GrowthFailure.Locked)); Assert.That(why, Is.EqualTo("Needs Era 4"));
            Assert.That(At(4, 3).UnitCCheck(out why), Is.EqualTo(GrowthFailure.Locked)); Assert.That(why, Is.EqualTo("Needs 4 stars"));
            Assert.That(At(4, 4, credits: 3999).UnitCCheck(out why), Is.EqualTo(GrowthFailure.NotEnoughCredits));
            var day = At(4, 4); day.OpenShop();
            Assert.That(day.UnitCCheck(out why), Is.Not.EqualTo(GrowthFailure.NotAtNight), "F81: walls open by day too");
            Assert.That(shop.Objectives().Single(o => o.text.StartsWith("Open shop unit C")).done, Is.False, "era 4's goal lists unit C");
            int visitors = shop.VisitorsAtOnce;
            Assert.That(shop.FreePlaces.Any(p => p.StartsWith("c")), Is.False, "its places are closed");
            Assert.That(shop.BuyContainer("large", "c3").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace));
            Assert.That(shop.BuyUnitC().Success, Is.True);
            Assert.That((shop.UnitC, shop.Credits, shop.VisitorsAtOnce), Is.EqualTo((true, 46000, visitors + 1)));
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Opened).Text, Is.EqualTo("Shop unit C opened"));
            Assert.That(shop.FreePlaces.Where(p => p.StartsWith("c")), Is.EqualTo(new[] { "c1", "c2", "c3", "c4", "c5", "c6" }));
            Assert.That(shop.Objectives().Single(o => o.text.StartsWith("Open shop unit C")).done, Is.True);
            // The feature wall takes Show tanks; its long places take Starter and Large tanks, never a Show tank.
            Assert.That(shop.BuyContainer("show", "c1").Success, Is.True);
            Assert.That(shop.BuyContainer("large", "c3").Success, Is.True);
            Assert.That(shop.BuyContainer("show", "c4").Success, Is.False);
            Assert.That(shop.UnitCCheck(out why), Is.EqualTo(GrowthFailure.MaxLevel));
            // The other rooms' openings are named for the banner too.
            var annex = At(3, 3); Assert.That(annex.BuyAnnex().Success, Is.True);
            Assert.That(annex.TakeEvents().Single(e => e.Kind == GrowthEventKind.Opened).Text, Is.EqualTo("Power room opened"));
            var b = At(2, 2); b.UnitB = false; Assert.That(b.BuyUnitB().Success, Is.True);
            Assert.That(b.TakeEvents().Any(e => e.Kind == GrowthEventKind.Opened && e.Text == "Shop unit B opened"), Is.True);
        }

        [Test]
        public void TheExhibitionHallFinishIsBoughtAtFiveStars()
        {
            var shop = At(5, 5);
            Assert.That(shop.HallCheck(out string why), Is.EqualTo(GrowthFailure.Locked)); Assert.That(why, Is.EqualTo("Open unit C first"));
            Assert.That(shop.Objectives().Select(o => o.text), Has.Some.StartsWith("Open shop unit C"));
            Assert.That(shop.BuyUnitC().Success, Is.True);
            var four = At(4, 5); four.UnitC = true;
            Assert.That(four.HallCheck(out why), Is.EqualTo(GrowthFailure.Locked)); Assert.That(why, Is.EqualTo("Needs Era 5"));
            var low = At(5, 4); low.UnitC = true;
            Assert.That(low.HallCheck(out why), Is.EqualTo(GrowthFailure.Locked)); Assert.That(why, Is.EqualTo("Needs 5 stars"));
            Assert.That(shop.Objectives().Single(o => o.text.StartsWith("Finish the exhibition hall")).done, Is.False);
            Assert.That(shop.Objectives().Last().text, Does.StartWith("Host it: six exhibition guests"), "GR-11: the Grand Exhibition follows the hall");
            int visitors = shop.VisitorsAtOnce;
            Assert.That(shop.BuyHall().Success, Is.True);
            Assert.That((shop.Hall, shop.VisitorsAtOnce), Is.EqualTo((true, visitors + 1)));
            Assert.That(shop.TakeEvents().Last(e => e.Kind == GrowthEventKind.Opened).Text, Is.EqualTo("The exhibition hall is finished"));
            Assert.That(shop.FreePlaces.Where(p => p.StartsWith("e")), Is.EqualTo(new[] { "e1", "e2" }));
            Assert.That(shop.OwnedPlaces.Count(), Is.EqualTo(16));
            Assert.That(shop.Objectives().Single(o => o.text.StartsWith("Finish the exhibition hall")).done, Is.True);
            Assert.That(shop.BuyContainer("large", "e1").Success, Is.True, "an exhibition place takes a Large tank");
        }

        [Test]
        public void HelpersWalkIntoUnitCThroughUnitBsArch()
        {
            var browse = GrowthSpaces.SpotOf("c4").Browse;
            Assert.That(GrowthWalk.RoomOf(browse), Is.EqualTo(GrowthWalk.Room.C));
            Assert.That(GrowthWalk.RoomOf(GrowthWalk.NoriHome), Is.EqualTo(GrowthWalk.Room.Back), "the laboratory stays the laboratory");
            var path = GrowthWalk.Path(GrowthWalk.Tray, browse);
            Assert.That(path, Has.Member(GrowthWalk.BackDoorShop).And.Member(GrowthSpaces.Points["ArchC"]).And.Member(GrowthSpaces.Points["UnitCAisle"]), "through the laboratory's door, unit A, unit B and unit C's arch");
            Assert.That(GrowthWalk.Path(browse, GrowthWalk.NoriHome).Last(), Is.EqualTo(GrowthWalk.NoriHome));
            Assert.That(GrowthWalk.Path(GrowthWalk.Counter, GrowthWalk.Counter), Is.EqualTo(new[] { GrowthWalk.Counter, GrowthWalk.Counter }), "standing still is no walk");
            // Nori stocks a tank in unit C: the shop's only tank with room for the target's species.
            var shop = At(4, 4);
            shop.Levels["generator"] = 2; shop.Levels["pump"] = 2; shop.Annex = true;
            shop.Containers.Add(new GrowthContainer { Id = "large-a3", Kind = "large", Place = "a3" });
            Assert.That(shop.Hire("nori").Success, Is.True);
            Assert.That(shop.BuyUnitC().Success && shop.BuyContainer("starter", "c4").Success, Is.True);
            foreach (var t in shop.ShopContainers.Where(t => t.Place != "c4")) while (t.Free > 0) Stock(shop, t, "bubblebelly");
            shop.Discovered.Add("pondlet");
            shop.SetTarget("pondlet", 1);
            shop.Hatchlings.Add(new GrowthSpecimen { Id = 9100, Species = "pondlet" });
            var nori = shop.Helper("nori");
            bool sawArch = false;
            for (int i = 0; i < 2000 && shop.Shown("pondlet") < 1; i++)
            {
                shop.Tick(0.05f);
                if (nori.Step == "deliver" && nori.Progress > 0) sawArch |= GrowthWalk.Path(nori.Spot, nori.Target).Contains(GrowthSpaces.Points["ArchC"]);
            }
            Assert.That(shop.ContainerAt("c4").Occupants.Select(o => o.Species), Is.EqualTo(new[] { "pondlet" }));
            Assert.That(sawArch, Is.True, "Nori walked through unit B's arch into unit C");
        }

        [Test]
        public void TheSpacesSaveAtVersionNine()
        {
            var shop = At(5, 5);
            Assert.That(shop.BuyUnitC().Success && shop.BuyHall().Success && shop.BuyContainer("show", "c2").Success && shop.BuyContainer("starter", "e2").Success, Is.True);
            var save = shop.Snapshot();
            Assert.That(save.version, Is.GreaterThanOrEqualTo(9), "version 9 or later (GR-11 is 10)");
            string json = JsonUtility.ToJson(save);
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That((back.UnitC, back.Hall, back.ContainerAt("c2")?.Kind, back.ContainerAt("e2")?.Kind), Is.EqualTo((true, true, "show", "starter")));
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            // A version 8 save (no unit C or hall) loads.
            var eight = At(4, 4).Snapshot(); eight.version = 8;
            Assert.That(GrowthShop.Restore(eight, out problem), Is.Not.Null, problem);
            // Refused: unit C without unit B, the hall without unit C, a tank in a unit that is not open.
            string Why(Action<GrowthSave> change) { var s = JsonUtility.FromJson<GrowthSave>(json); change(s); GrowthShop.Restore(s, out string p); return p; }
            Assert.That(Why(s => s.unitB = false), Is.EqualTo("Unit C without unit B"));
            Assert.That(Why(s => s.unitC = false), Is.EqualTo("The exhibition hall without unit C"));
            Assert.That(Why(s => { s.unitC = false; s.hall = false; }), Is.EqualTo("A tank in a unit that is not open: c2"));
            Assert.That(Why(s => s.hall = false), Is.EqualTo("A tank in a unit that is not open: e2"));
        }
    }
}
