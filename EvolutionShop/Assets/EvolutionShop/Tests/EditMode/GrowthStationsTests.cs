using System;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-06: GR.9's station levels past 2 in the greybox: every station's levels with their costs and gates, the
    /// levels that wait for a later task, the siphon's twin chamber and vacuum, growers and the vat, the incubator's clutches
    /// and tint port, the breeding tank's second pair, the counter's queue, the research bench's microscope and the version
    /// 5 save (GR.9, GR.15.1).</summary>
    public sealed class GrowthStationsTests
    {
        static GrowthShop At(int era, int stars, int credits = 50000, int rp = 500) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = rp };

        /// <summary>GR-09: the counter's second till is a helper's, so its level 4 is checked with Pip hired.</summary>
        static GrowthShop Ready(GrowthShop shop, string station, int level)
        {
            if (station == "counter" && level == 4) shop.Staff["pip"] = new GrowthHelper { Id = "pip" };
            return shop;
        }

        static void UpgradeTo(GrowthShop shop, string station, int level)
        {
            while (shop.Level(station) < level) Assert.That(shop.Upgrade(station).Success, Is.True, station + " to level " + (shop.Level(station) + 1));
        }

        static void Mix(GrowthShop shop, params string[] doses)
        {
            foreach (var d in doses) { shop.Held.Add(d); Assert.That(shop.AddDose(d).Success, Is.True, "dose " + d); }
            Assert.That(shop.StartIncubation().Success, Is.True, "incubate");
            shop.Tick(shop.IncubationSeconds + 0.1f);
        }

        [Test]
        public void EveryStationHasItsLevelsAndTheLaterOnesSayWhatTheyWaitFor()
        {
            Assert.That(GrowthShop.UpgradeableStations, Is.EquivalentTo(new[] { "siphon", "grower", "incubator", "counter", "bench", "breeding", "vat", "generator", "pump", "sorter", "research" }));
            Assert.That(new GrowthShop().Level("bench"), Is.EqualTo(1), "the research bench starts at level 1 (notes)");
            // Each level's era, stars, research points and credits gate it, in that order, as GrowthContent says.
            foreach (var station in new[] { "siphon", "grower", "incubator", "counter", "bench" })
            {
                var levels = GrowthContent.Station(station).Levels;
                for (int lv = 2; lv <= levels.Length; lv++)
                {
                    var def = levels[lv - 1];
                    var shop = Ready(At(era: 5, stars: 5), station, lv);
                    shop.Levels[station] = lv - 1;
                    string later = GrowthShop.WaitsFor(station, lv);
                    if (later != null)
                    {
                        Assert.That(shop.UpgradeCheck(station, out _, out string waits), Is.EqualTo(GrowthFailure.Locked), station + " " + lv);
                        Assert.That(waits, Is.EqualTo(later));
                        continue;
                    }
                    if (def.Era > 1) { var early = Ready(At(era: def.Era - 1, stars: 5), station, lv); early.Levels[station] = lv - 1; early.UpgradeCheck(station, out _, out string why); Assert.That(why, Is.EqualTo("Needs Era " + def.Era), station + " " + lv); }
                    if (def.Stars > 1) { var low = Ready(At(era: 5, stars: def.Stars - 1), station, lv); low.Levels[station] = lv - 1; low.UpgradeCheck(station, out _, out string why); Assert.That(why, Is.EqualTo("Needs " + def.Stars + " stars"), station + " " + lv); }
                    if (def.Rp > 0) { var poor = Ready(At(era: 5, stars: 5, rp: def.Rp - 1), station, lv); poor.Levels[station] = lv - 1; poor.UpgradeCheck(station, out _, out string why); Assert.That(why, Is.EqualTo("Needs " + def.Rp + " research points"), station + " " + lv); }
                    var broke = Ready(At(era: 5, stars: 5, credits: def.Credits - 1), station, lv); broke.Levels[station] = lv - 1;
                    Assert.That(broke.UpgradeCheck(station, out _, out _), Is.EqualTo(GrowthFailure.NotEnoughCredits), station + " " + lv);
                    int credits = shop.Credits, rp = shop.Rp;
                    Assert.That(shop.Upgrade(station).Success, Is.True, station + " " + lv);
                    Assert.That((shop.Level(station), credits - shop.Credits, rp - shop.Rp), Is.EqualTo((lv, def.Credits, def.Rp)), station + " " + lv);
                }
            }
            var top = At(era: 5, stars: 5);
            foreach (var station in new[] { "siphon", "grower", "incubator", "counter", "bench", "breeding" })
                while (top.UpgradeCheck(station, out _, out _) == GrowthFailure.None) top.Upgrade(station);
            Assert.That(GrowthShop.UpgradeableStations.Where(s => s != "vat" && s != "generator" && s != "pump" && s != "sorter" && s != "research").Select(top.Level), Is.EqualTo(new[] { 4, 4, 4, 3, 4, 3 }),
                "siphon, growers, incubator and the bench to 4 (GR-08: the analyzer and planner), the counter to 3 (its next level waits), the breeding tank to 3");
            Assert.That(GrowthShop.WaitsFor("counter", 4), Is.Null, "GR-09: the second till came with the helpers");
            Assert.That(GrowthShop.WaitsFor("bench", 3), Is.Null);
            Assert.That(GrowthShop.WaitsFor("bench", 4), Is.Null);
            Assert.That(top.UpgradeCheck("siphon", out _, out string max), Is.EqualTo(GrowthFailure.MaxLevel));
            Assert.That(max, Is.EqualTo("Highest level"));
            Assert.That(top.UpgradeCheck("vat", out _, out string vat), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(vat, Is.EqualTo("Order the conditioning vat from the supplier catalog"), "the vat's level 1 is the catalog's");
        }

        [Test]
        public void TheTwinChamberAndTheVacuumCollectMoreAHold()
        {
            var shop = At(era: 3, stars: 3);
            UpgradeTo(shop, "grower", 2);
            UpgradeTo(shop, "siphon", 3);
            Assert.That((shop.SiphonCapacity, shop.PerHold, shop.Vacuum), Is.EqualTo((10, 2, false)));
            shop.GrowerUnits["bloom"] = 6; shop.GrowerUnits["flow"] = 6;
            Assert.That(shop.Collect("bloom", shop.HoldSeconds).Success, Is.True);
            Assert.That(shop.Held.Count(h => h == "bloom"), Is.EqualTo(2), "the twin chamber: two doses a hold");
            shop.GrowerUnits["bloom"] = 0;
            Assert.That(shop.Collect("bloom", shop.HoldSeconds, new[] { "flow" }).Failure, Is.EqualTo(GrowthFailure.GrowerEmpty), "without the vacuum, an empty grower is empty");
            UpgradeTo(shop, "siphon", 4);
            Assert.That((shop.SiphonCapacity, shop.PerHold, shop.Vacuum, shop.HoldSeconds), Is.EqualTo((16, 4, true, 0.5f)));
            shop.Held.Clear(); shop.TakeEvents();
            shop.GrowerUnits["bloom"] = 3;
            Assert.That(shop.Collect("bloom", shop.HoldSeconds, new[] { "flow", "bloom", "spark" }).Success, Is.True);
            Assert.That(shop.Held, Is.EqualTo(new[] { "bloom", "bloom", "bloom", "flow" }), "the grower aimed at first, then the next in range; a culture not owned is passed over");
            Assert.That(shop.TakeEvents().Single().Text, Is.EqualTo("3 Bloom, 1 Flow"));
            Assert.That(shop.Collect("bloom", shop.HoldSeconds * 2, new[] { "flow" }).Success, Is.True, "Bloom is empty, the vacuum pulls Flow");
            Assert.That(shop.Held.Count(h => h == "flow"), Is.EqualTo(6), "four Flow a hold, then the last one the grower had");
            Assert.That(shop.Held.Count, Is.EqualTo(9));
            shop.Held.AddRange(Enumerable.Repeat("flow", 7));
            Assert.That(shop.Collect("flow", 1).Failure, Is.EqualTo(GrowthFailure.SiphonFull), "16 doses fill the vacuum");
        }

        [Test]
        public void GrowersAndTheVatHoldMoreAndRefillFaster()
        {
            var shop = At(era: 4, stars: 4);
            UpgradeTo(shop, "grower", 4);
            Assert.That((shop.GrowerCapacity, shop.RefillSeconds), Is.EqualTo((16, 1f)));
            shop.GrowerUnits["bloom"] = 0;
            shop.Tick(5);
            Assert.That(shop.GrowerUnits["bloom"], Is.EqualTo(5).Within(0.01f), "one dose a second at level 4");
            shop.Tick(100);
            Assert.That(shop.GrowerUnits["bloom"], Is.EqualTo(16));
            // The vat: level 1 with Mist from the supplier; its double batch and faster batches upgrade Mist alone.
            Assert.That(shop.Order("vat").Success && shop.OpenShop().Success, Is.True);
            Assert.That((shop.Owns("mist"), shop.Level("vat"), shop.GrowerCapacityOf("mist")), Is.EqualTo((true, 1, 16)));
            Assert.That(shop.Upgrade("vat").Success, Is.True, "level 2 (double batch) is bought on the vat's card");
            Assert.That((shop.GrowerCapacityOf("mist"), shop.GrowerCapacityOf("bloom"), shop.RefillSecondsOf("mist")), Is.EqualTo((32, 16, 1f)));
            Assert.That(shop.Upgrade("vat").Success, Is.True);
            Assert.That(shop.RefillSecondsOf("mist"), Is.EqualTo(0.5f), "level 3: Mist twice as fast");
            shop.GrowerUnits["mist"] = 0;
            shop.Tick(5);
            Assert.That(shop.GrowerUnits["mist"], Is.EqualTo(10).Within(0.01f));
            Assert.That(shop.UpgradeCheck("vat", out _, out _), Is.EqualTo(GrowthFailure.MaxLevel));
        }

        [Test]
        public void TheIncubatorHatchesClutchesAndTakesATint()
        {
            var shop = At(era: 4, stars: 4, rp: 0);
            UpgradeTo(shop, "incubator", 3);
            Assert.That((shop.Clutch, shop.MaxDoses, shop.IncubatorTintPort), Is.EqualTo((2, 6, true)));
            Mix(shop, "bloom", "flow");
            Assert.That(shop.Hatchlings.Select(h => h.Species), Is.EqualTo(new[] { "pondlet", "pondlet" }), "a clutch of two of the same");
            Assert.That(shop.Rp, Is.EqualTo(GrowthContent.RpFounder), "one discovery, once");
            Assert.That(shop.Journal, Is.EqualTo(new[] { "pondlet/Classic" }));
            shop.Held.Add("bloom"); shop.AddDose("bloom");
            Assert.That(shop.StartIncubation().Failure, Is.EqualTo(GrowthFailure.HatchlingWaiting), "the output empties first (GR.15.1)");
            Assert.That(shop.UpgradeCheck("incubator", out _, out _), Is.EqualTo(GrowthFailure.Busy));
            Assert.That(shop.TakeHatchling().Success && shop.Hatchlings.Count == 1 && shop.TakeHatchling().Success && shop.Hatchling == null, Is.True, "taken one by one");
            Assert.That(shop.TakeHatchling().Failure, Is.EqualTo(GrowthFailure.NoHatchling));
            shop.Carried.Clear(); shop.ReturnDose("bloom"); shop.Held.Clear();
            // The tint port: Bloom makes the next clutch Sunny; the tint is used up.
            shop.Held.Add("ember");
            Assert.That(shop.IncubatorTintCheck("ember", out string why), Is.EqualTo(GrowthFailure.UnknownCulture));
            Assert.That(shop.IncubatorTintCheck("bloom", out why), Is.EqualTo(GrowthFailure.NoDose));
            shop.Held.Clear(); shop.Held.Add("bloom");
            Assert.That(shop.TintIncubator("bloom").Success && shop.IncubatorTint == "bloom" && shop.Held.Count == 0, Is.True);
            shop.Held.Add("flow");
            Assert.That(shop.TintIncubator("flow").Failure, Is.EqualTo(GrowthFailure.Busy), "one tint at a time");
            Assert.That(shop.ClearIncubatorTint().Success && shop.IncubatorTint == null && shop.Held.Contains("bloom"), Is.True, "an unused tint comes back");
            Assert.That(shop.TintIncubator("bloom").Success, Is.True);
            shop.TakeEvents();
            Mix(shop, "bloom", "flow");
            Assert.That(shop.Hatchlings.All(h => h.Species == "pondlet" && h.Colour == Colour.Sunny) && shop.Hatchlings.Count == 2 && shop.IncubatorTint == null, Is.True, "a Sunny clutch");
            Assert.That(shop.Rp, Is.EqualTo(GrowthContent.RpFounder + GrowthContent.RpColour), "a new colour, once for the clutch");
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.NewColour).Text, Is.EqualTo("New colour: Sunny Pondlet!"));
            shop.Hatchlings.Clear();
            Mix(shop, "bloom", "flow");
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Hatched).Text, Is.EqualTo("2 Pondlets hatched"));
            shop.Hatchlings.Clear();
            UpgradeTo(shop, "incubator", 4);
            Mix(shop, "bloom", "flow");
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(3), "level 4: a clutch of three");
            Assert.That(new[] { "Pondlet", "Bubblebelly", "Zephyray", "Phoenix Fry" }.Select(GrowthShop.Plural), Is.EqualTo(new[] { "Pondlets", "Bubblebellies", "Zephyrays", "Phoenix Fries" }));
            Assert.That(new GrowthShop().IncubatorTintCheck("bloom", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Tinting needs the incubator's level 3"));
        }

        [Test]
        public void TheBreedingTanksLevelThreeBreedsTwoPairsAtOnce()
        {
            var shop = At(era: 4, stars: 4);
            UpgradeTo(shop, "breeding", 3);
            Assert.That((shop.PairPlaces, shop.BreedSeconds), Is.EqualTo((2, 40f)));
            foreach (var s in new[] { "pondlet", "bubblebelly", "pondlet" }) shop.DevelopmentGive(s);
            for (int i = 0; i < 3; i++) Assert.That(shop.PutParent().Success, Is.True);
            Assert.That((shop.Pairs[0].Parents.Count, shop.Pairs[1].Parents.Count, shop.Pairs[1].Breeding), Is.EqualTo((2, 1, false)), "the first pair fills first");
            shop.DevelopmentGive("ribbonfin");
            Assert.That(shop.PutParent().Success, Is.True);
            Assert.That(shop.Pairs.All(p => p.Breeding && p.Left == 40f), Is.True, "both pairs breed");
            shop.DevelopmentGive("pondlet");
            var full = shop.PutParent();
            Assert.That((full.Failure, full.Detail), Is.EqualTo((GrowthFailure.ChamberFull, "The breeding tank's 2 pairs are full")));
            shop.Carried.Clear();
            Assert.That(shop.UpgradeCheck("breeding", out _, out _), Is.EqualTo(GrowthFailure.MaxLevel));
            shop.Tick(40.1f);
            Assert.That(shop.Pairs[0].Offspring.Species, Is.EqualTo("puddlepup"), "bubblebelly x pondlet");
            Assert.That(shop.Pairs[1].Offspring.Species, Is.EqualTo("reedwhisker"), "pondlet x ribbonfin");
            Assert.That(shop.AllOffspring.Count(), Is.EqualTo(2));
            Assert.That(shop.TakeOffspring().Success && shop.Carried.Single().Species == "puddlepup", Is.True, "the first pair's first");
            Assert.That(shop.Pairs[0].Left, Is.EqualTo(40f), "and only that pair starts again");
            Assert.That(shop.TakeOffspring(1).Success && shop.Carried.Last().Species == "reedwhisker", Is.True);
            var parent = shop.Pairs[1].Parents[0];
            Assert.That(shop.TakeParent(parent.Id).Success && shop.Pairs[1].Parents.Count == 1 && !shop.Pairs[1].Breeding, Is.True, "a parent from the second pair");
            Assert.That(shop.AllParents.Count(), Is.EqualTo(3));
        }

        [Test]
        public void TheCounterQueuesItsLevelsLengthAndTheCardReaderServesFaster()
        {
            foreach (int level in new[] { 1, 3 })
            {
                var shop = At(era: 2, stars: 2);
                shop.UnitB = true;
                UpgradeTo(shop, "counter", level);
                Assert.That(shop.QueueLength, Is.EqualTo(level == 3 ? 3 : 2));
                Assert.That(shop.BuyContainer("starter", "b1").Success, Is.True);
                foreach (var c in shop.ShopContainers.ToList()) while (c.Free > 0) shop.DevelopmentStock(c.Id, "pondlet");
                Assert.That(shop.OpenShop().Success, Is.True);
                int most = 0; bool waited = false;
                for (int i = 0; i < 1200 && shop.Phase == GrowthPhase.Day; i++)
                {
                    shop.Tick(0.25f);
                    int queued = shop.Queue.Count();
                    most = Math.Max(most, queued);
                    Assert.That(queued, Is.LessThanOrEqualTo(shop.QueueLength), "never more in the queue than the counter takes");
                    if (queued == shop.QueueLength && shop.Visitors.Any(v => v.State == GrowthVisitorState.Browsing && v.Clock >= v.BrowseFor - 1.01f)) waited = true;
                }
                Assert.That(most, Is.EqualTo(level == 3 ? 3 : 2), "the queue fills to its length with three visitors at once");
                if (level == 1) Assert.That(waited, Is.True, "a third visitor waits by the tanks while the queue is full");
            }
            var fast = At(era: 2, stars: 2);
            UpgradeTo(fast, "counter", 3);
            Assert.That(fast.ServeSeconds, Is.EqualTo(0.5f), "the card reader bags a sale in 0.5 s");
        }

        [Test]
        public void TheMicroscopeSaysHowFarAMixIsFromSomethingNew()
        {
            var shop = At(era: 2, stars: 2, rp: 5);
            Assert.That(shop.Microscope, Is.False, "level 1: the notes on silhouettes");
            Assert.That(shop.Upgrade("bench").Success && shop.Microscope && shop.Rp == 0, Is.True, "level 2: 80 credits and 5 research points");
            shop.Discovered.Add("pondlet");
            foreach (var d in new[] { "bloom", "bloom", "bloom" }) { shop.Held.Add(d); shop.AddDose(d); }
            Assert.That(shop.Preview().DistanceToNew, Is.EqualTo(2), "Bloom 3 is two doses from the Bubblebelly (Bloom 2 + Flow 1)");
            shop.ReturnDose("bloom");
            Assert.That(shop.Preview().DistanceToNew, Is.EqualTo(1), "Bloom 2 is one");
            shop.Discovered.AddRange(new[] { "bubblebelly", "ribbonfin" });
            Assert.That(shop.Preview().DistanceToNew, Is.EqualTo(-1), "with Bloom and Flow alone, nothing new is left to find");
        }

        [Test]
        public void TheVersionFiveSaveKeepsClutchesTintsPairsAndLevels()
        {
            var shop = At(era: 4, stars: 4);
            UpgradeTo(shop, "incubator", 3); UpgradeTo(shop, "breeding", 3); UpgradeTo(shop, "bench", 2);
            Assert.That(shop.Order("vat").Success && shop.OpenShop().Success, Is.True);
            shop.Tick(GrowthContent.DaySeconds + 1);
            Assert.That(shop.Upgrade("vat").Success, Is.True);
            Mix(shop, "bloom", "flow");
            shop.Held.Add("mist");
            Assert.That(shop.TintIncubator("mist").Success, Is.True);
            foreach (var s in new[] { "pondlet", "bubblebelly", "pondlet" }) shop.DevelopmentGive(s);
            for (int i = 0; i < 3; i++) shop.PutParent();
            shop.DevelopmentGive("ribbonfin"); shop.PutParent();
            shop.Tick(40.1f);
            Assert.That(shop.TakeOffspring(1).Success, Is.True);
            shop.Tick(5);
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            Assert.That((back.Hatchlings.Count, back.IncubatorTint, back.Pairs[1].Parents.Count, back.Pairs[1].Left, back.Pairs[0].Offspring != null), Is.EqualTo((2, "mist", 2, 35f, true)));
            Assert.That((back.Level("bench"), back.Level("vat"), back.GrowerCapacityOf("mist")), Is.EqualTo((2, 2, 8)));
            Assert.That(GrowthSave.CurrentVersion, Is.GreaterThanOrEqualTo(5));
            // A GR-05 save loads: one hatchling, one pair, the vat at level 1 with Mist, the bench at 1.
            var old = JsonUtility.FromJson<GrowthSave>(json);
            old.version = 4; old.secondParents = null; old.secondOffspring = null; old.secondLeft = 0; old.incubatorTint = null; old.hatchling = old.hatchling.Take(1).ToArray();
            old.levels = old.stations.Select((x, i) => (x, i)).Where(t => t.x != "vat" && t.x != "bench").Select(t => old.levels[t.i]).ToArray();
            old.stations = old.stations.Where(x => x != "vat" && x != "bench").ToArray();
            var gr05 = GrowthShop.Restore(old, out problem);
            Assert.That(problem, Is.Null);
            Assert.That((gr05.Level("vat"), gr05.Level("bench"), gr05.Hatchlings.Count), Is.EqualTo((1, 1, 1)));
            void Refused(Action<GrowthSave> edit, string expected)
            {
                var bad = JsonUtility.FromJson<GrowthSave>(json);
                edit(bad);
                Assert.That(GrowthShop.Restore(bad, out string why), Is.Null, expected);
                Assert.That(why, Is.EqualTo(expected));
            }
            Refused(b => b.hatchling = b.hatchling.Concat(new[] { new GrowthSave.Specimen { id = 990, species = "pondlet", colour = "Classic" } }).ToArray(), "More hatchlings than the incubator's clutch");
            Refused(b => b.levels[Array.IndexOf(b.stations, "incubator")] = 2, "More hatchlings than the incubator's clutch");
            Refused(b => { b.hatchling = new GrowthSave.Specimen[0]; b.levels[Array.IndexOf(b.stations, "incubator")] = 2; }, "Unknown incubator tint mist");
            Refused(b => b.levels[Array.IndexOf(b.stations, "breeding")] = 2, "More parents than the breeding tank takes");
            Refused(b => b.levels[Array.IndexOf(b.stations, "counter")] = 4, "A second till without Pip");
            Refused(b => { b.stations = b.stations.Concat(new[] { "sorter" }).ToArray(); b.levels = b.levels.Concat(new[] { 2 }).ToArray(); }, "Bad station level sorter");
        }
    }
}
