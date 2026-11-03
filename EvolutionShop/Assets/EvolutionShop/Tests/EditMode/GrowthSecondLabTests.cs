using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// GR-34 wave 2 (tester round 2, F69, the preview approved in GR.77): the second lab. It opens through the power room's west
    /// wall in era 4 with 4 stars for 6,000; its three incubators (3,000, 6,000, 12,000) and three breeding tanks (2,000, 4,000,
    /// 8,000) are each bought one at a time and work at the first ones' levels. An incubator there makes its queue from the
    /// buffers and growers onto its own tray, and from the first incubator's level 4 doses a chosen recipe by itself; Moss can be
    /// set to any incubator and breeding tank; Nori collects from every tray; a save keeps it all (version 18).
    /// </summary>
    public sealed class GrowthSecondLabTests
    {
        const string Species = "pondlet";

        static GrowthShop At(int era, int stars, int credits = 60000) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = 500, SalesTotal = GrowthContent.PipSalesGate };

        /// <summary>An era 4 shop with the power room, the Pondlet found and its growers full.</summary>
        static GrowthShop Ready(bool open = true)
        {
            var shop = At(4, 4);
            Assert.That(shop.BuyAnnex().Success, Is.True, "the power room");
            var def = GrowthContent.Get(Species);
            shop.MixLog.Add(new GrowthMixEntry { Doses = (int[])def.Recipe.Clone(), Species = Species, Exact = true });
            if (!shop.Discovered.Contains(Species)) shop.Discovered.Add(Species);
            foreach (var c in Cultures(def))
            {
                if (!shop.Cultures.Contains(c)) shop.Cultures.Add(c);
                shop.GrowerUnits[c] = shop.GrowerCapacityOf(c);
            }
            if (open) Assert.That(shop.BuySecondLab().Success, Is.True, "the second lab");
            return shop;
        }

        static string[] Cultures(SpeciesDef def) => Enumerable.Range(0, def.Recipe.Length).Where(i => def.Recipe[i] > 0).Select(i => GrowthContent.Cultures[i].Id).ToArray();

        static void Run(GrowthShop shop, float seconds, float step = 0.1f) { for (float t = 0; t < seconds; t += step) shop.Tick(step); }

        static void Fill(GrowthShop shop) { foreach (var c in shop.Cultures) shop.GrowerUnits[c] = shop.GrowerCapacityOf(c); }

        [Test]
        public void F69_TheSecondLabOpensThroughThePowerRoom_AndItsMachinesAreBoughtInTurn()
        {
            var before = At(4, 4);
            Assert.That(before.SecondLabCheck(out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Open the power room first: its west wall leads in"));
            var early = At(3, 4); early.BuyAnnex();
            early.SecondLabCheck(out why); Assert.That(why, Does.StartWith("Needs Era 4"));
            var low = At(4, 3); low.BuyAnnex();
            low.SecondLabCheck(out why); Assert.That(why, Is.EqualTo("Needs 4 stars"));
            var poor = Ready(open: false); poor.Credits = 5999;
            poor.SecondLabCheck(out why); Assert.That(why, Is.EqualTo("Needs 6000 credits"));
            var shop = Ready(open: false);
            Assert.That(shop.LabIncubatorCheck(out why), Is.EqualTo(GrowthFailure.Locked), "nothing to place before the room");
            int credits = shop.Credits;
            Assert.That(shop.BuySecondLab().Success && shop.SecondLab, Is.True);
            Assert.That(credits - shop.Credits, Is.EqualTo(6000));
            Assert.That(shop.BuySecondLab().Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            // Three incubators in turn at 3,000, 6,000 and 12,000; no more.
            credits = shop.Credits;
            foreach (int price in new[] { 3000, 6000, 12000 })
            {
                Assert.That(shop.LabIncubatorNextCredits, Is.EqualTo(price));
                Assert.That(shop.BuyLabIncubator().Success, Is.True);
            }
            Assert.That((shop.LabIncubators.Count, credits - shop.Credits), Is.EqualTo((3, 21000)));
            Assert.That(shop.BuyLabIncubator().Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            // Breeding tanks wait for the first one (they work at its level), then 2,000, 4,000 and 8,000.
            shop.LabBreederCheck(out why); Assert.That(why, Is.EqualTo("Buy the first breeding tank first: these work at its level"));
            shop.Levels["breeding"] = 1;
            credits = shop.Credits;
            foreach (int price in new[] { 2000, 4000, 8000 }) { Assert.That(shop.LabBreederNextCredits, Is.EqualTo(price)); Assert.That(shop.BuyLabBreeder().Success, Is.True); }
            Assert.That((shop.LabBreeders.Count, credits - shop.Credits), Is.EqualTo((3, 14000)));
            Assert.That(shop.BuyLabBreeder().Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            shop.Credits = 0;
            Assert.That(Ready().LabIncubatorStatus(1), Is.EqualTo("Not placed yet · after the one before it"));
            Assert.That(Ready().LabIncubatorStatus(0), Is.EqualTo("Not placed yet · 3000 cr"));
            // Its door in the power room's west wall: a helper walks there from the laboratory through the power room.
            var path = GrowthWalk.Path(GrowthWalk.NoriHome, GrowthLab2.Tray(0));
            Assert.That(path.Select(GrowthWalk.RoomOf).Distinct(), Is.EqualTo(new[] { GrowthWalk.Room.Back, GrowthWalk.Room.Annex, GrowthWalk.Room.Lab2 }));
            Assert.That(GrowthSpaces.RoomAt(GrowthLab2.Tray(2)).Id, Is.EqualTo("lab2"));
            Assert.That(GrowthSpaces.Problems(), Is.Empty);
        }

        [Test]
        public void F69_AnIncubatorThereMakesItsQueueFromTheBuffersAndGrowersOntoItsOwnTray_AndItsAutoDoserFromLevelFour()
        {
            var shop = Ready();
            Assert.That(shop.QueueLabMake(0, Species).Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "none placed yet");
            shop.BuyLabIncubator();
            Assert.That(shop.QueueLabMake(0, "bubblebelly").Failure, Is.EqualTo(GrowthFailure.NotDiscovered), "only a recipe found");
            var def = GrowthContent.Get(Species);
            var before = Cultures(def).ToDictionary(c => c, c => shop.GrowerUnits[c]);
            var r = shop.QueueLabMake(0, Species, 2);
            Assert.That((r.Success, r.Detail), Is.EqualTo((true, "2")));
            var inc = shop.LabIncubators[0];
            Assert.That((inc.Incubating, inc.Pending, inc.Queue.Count), Is.EqualTo((true, Species, 1)), "the first starts at once, one more waits");
            Assert.That(shop.Incubating || shop.Chamber.Count > 0, Is.False, "the laboratory's incubator is left alone");
            foreach (var c in Cultures(def))
                Assert.That(shop.GrowerUnits[c], Is.EqualTo(before[c] - def.Recipe[GrowthContent.CultureIndex(c)]).Within(1e-4), c + " drawn from its grower");
            int logged = shop.MixLog.First(m => m.Species == Species).Count;
            Run(shop, shop.IncubationSeconds + 0.5f);
            Assert.That(inc.Tray.Count, Is.EqualTo(shop.Clutch), "a clutch on its own tray");
            Assert.That(shop.Hatchlings, Is.Empty, "not on the laboratory's");
            Assert.That(shop.MixLog.First(m => m.Species == Species).Count, Is.EqualTo(logged + 1), "the mix log counts it");
            Assert.That(shop.LabWaiting(0), Is.EqualTo("Waiting: take the hatchlings off its tray"));
            Assert.That(shop.LabIncubatorStatus(0), Does.EndWith("on its tray"));
            Fill(shop);
            while (inc.Tray.Count > 0) { Assert.That(shop.TakeLabHatchling(0).Success, Is.True); shop.Carried.Clear(); }
            Assert.That((inc.Incubating, inc.Queue.Count), Is.EqualTo((true, 0)), "the next starts once the tray is clear");
            Assert.That(shop.UnqueueLabMake(0, Species).Failure, Is.EqualTo(GrowthFailure.NoDose));
            // Short of a culture it waits and says so.
            Run(shop, shop.IncubationSeconds + 0.5f);
            inc.Tray.Clear();
            foreach (var c in shop.Cultures) shop.GrowerUnits[c] = 0;
            shop.QueueLabMake(0, Species);
            Assert.That(shop.LabWaiting(0), Does.StartWith("Waiting for "));
            shop.UnqueueLabMake(0, Species, all: true);
            // The auto-doser comes with the first incubator's level 4, and doses its recipe batch after batch.
            Assert.That(shop.SetLabAuto(0, Species).Failure, Is.EqualTo(GrowthFailure.Locked));
            shop.Levels["incubator"] = 4;
            Fill(shop);
            Assert.That(shop.SetLabAuto(0, Species).Success && inc.Incubating, Is.True, "it starts at once");
            for (int n = 0; n < 2; n++)
            {
                Run(shop, shop.IncubationSeconds + 0.5f);
                inc.Tray.Clear(); Fill(shop); shop.Tick(0.1f);
                Assert.That(inc.Incubating, Is.True, "batch after batch");
            }
            Assert.That(inc.Made, Is.EqualTo(5));
            Assert.That(shop.SetLabAuto(0, null).Success, Is.True);
            Run(shop, shop.IncubationSeconds + 0.5f); inc.Tray.Clear(); shop.Tick(0.1f);
            Assert.That(inc.Incubating, Is.False, "stopped");
        }

        [Test]
        public void F69_ABreedingTankThereBreedsAtTheFirstOnesLevel()
        {
            var shop = Ready();
            shop.Levels["breeding"] = 1;
            shop.BuyLabBreeder();
            shop.Carried.Add(new GrowthSpecimen { Id = 801, Species = "pondlet" });
            shop.Carried.Add(new GrowthSpecimen { Id = 802, Species = "pondlet" });
            Assert.That(shop.PutLabParent(0).Success && shop.PutLabParent(0).Success, Is.True);
            var pair = shop.LabBreeders[0].Pairs[0];
            Assert.That((pair.Breeding, shop.Breeding), Is.EqualTo((true, false)), "its own pair; the laboratory's tank is empty");
            Assert.That(shop.LabBreederStatus(0), Does.StartWith("Breeding a Pondlet"));
            Run(shop, shop.BreedSeconds + 0.5f);
            Assert.That(pair.Offspring?.Species, Is.EqualTo("pondlet"));
            Assert.That(shop.LabBreederStatus(0), Is.EqualTo("An offspring waits in the nursery"));
            Assert.That(shop.TakeLabOffspring(0).Success && shop.Carried.Count == 1 && pair.Breeding, Is.True, "taken out, the pair starts again");
            Assert.That(shop.TakeLabParent(0, 801).Success, Is.True);
            // A Large parent needs the first tank's level 2; its second pair comes with the first's.
            var large = GrowthContent.Species.First(x => x.Size == SizeClass.Large).Id;
            shop.Carried.Clear(); shop.Carried.Add(new GrowthSpecimen { Id = 803, Species = large });
            Assert.That(shop.PutLabParent(0).Failure, Is.EqualTo(GrowthFailure.DoesNotFit));
            Assert.That(shop.LabActivePairs(shop.LabBreeders[0]).Count(), Is.EqualTo(shop.PairPlaces));
        }

        [Test]
        public void F69_MossRunsHisRecipeAndPairInAnyOfThem_AndNoriCollectsFromEveryTray()
        {
            var shop = Ready();
            shop.Levels["generator"] = 2; shop.Levels["pump"] = 2; shop.Levels["breeding"] = 1;
            shop.Containers.Add(new GrowthContainer { Id = "large-a3", Kind = "large", Place = "a3" });
            Assert.That(shop.Hire("moss").Success, Is.True);
            Assert.That(shop.SetMossIncubator(1).Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "not before it is placed");
            shop.BuyLabIncubator();
            Assert.That(shop.SetMossRecipe(Species).Success && shop.SetMossIncubator(1).Success, Is.True);
            shop.Tick(0.05f);
            var moss = shop.Helper("moss");
            Assert.That((moss.Step, moss.Target), Is.EqualTo(("lab-start", GrowthLab2.Tray(0))), "he walks to incubator 2");
            Assert.That(moss.Status, Is.EqualTo("Walking to incubator 2 to start a Pondlet"));
            Run(shop, moss.StepSeconds + 0.2f, 0.05f);
            var inc = shop.LabIncubators[0];
            Assert.That((inc.Incubating, shop.MossBatches, shop.Incubating, shop.MossBatch), Is.EqualTo((true, 1, false, false)), "his batch there, from its pipe; the laboratory's left alone");
            Assert.That(shop.TakeEvents().Any(e => e.Text == "Moss started a Pondlet in incubator 2"), Is.True);
            Run(shop, shop.IncubationSeconds + 1f);
            Assert.That(moss.Status, Is.EqualTo("Waiting for the hatchlings to be taken off incubator 2's tray"));
            // Nori takes them from its tray to the shop.
            Assert.That(shop.Hire("nori").Success, Is.True);
            int clutch = inc.Tray.Count;
            Assert.That(clutch, Is.GreaterThan(0));
            Run(shop, 1f);
            Assert.That(shop.Helper("nori").Status, Does.Contain("from incubator 2's tray"));
            Run(shop, 60f);
            Assert.That(shop.ShopContainers.Sum(c => c.Occupants.Count(o => o.Species == Species)), Is.GreaterThanOrEqualTo(clutch), "into the shop's tanks");
            // His pair in a breeding tank there.
            shop.BuyLabBreeder();
            Assert.That(shop.SetMossBreeder(1).Success, Is.True);
            shop.Discovered.Add("bubblebelly");
            shop.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
            Assert.That(shop.SetMossPair("pondlet", "bubblebelly").Success && shop.Train("moss").Success && shop.SetMossTask(GrowthShop.MossPairJob).Success, Is.True);
            var tank = shop.Containers.First(c => c.Id == "large-a3");
            tank.Occupants.Add(new GrowthSpecimen { Id = 811, Species = "bubblebelly" });
            Run(shop, 90f);
            var lab = shop.LabBreeders[0].Pairs[0];
            Assert.That(lab.Parents.Select(p => p.Species), Is.EquivalentTo(new[] { "pondlet", "bubblebelly" }), "both parents in breeding tank 2");
            Assert.That(shop.Pairs[0].Parents, Is.Empty, "the laboratory's tank left alone");
        }

        [Test]
        public void F69_SavedAndLoaded_AndASaveFromBeforeHasNoSecondLab()
        {
            var shop = Ready();
            shop.Levels["breeding"] = 1; shop.Levels["incubator"] = 4;
            shop.BuyLabIncubator(); shop.BuyLabIncubator(); shop.BuyLabBreeder();
            shop.QueueLabMake(0, Species, 3);
            shop.SetLabAuto(1, Species);
            shop.Carried.Add(new GrowthSpecimen { Id = 901, Species = "pondlet" });
            shop.PutLabParent(0);
            Run(shop, 2f);
            var back = GrowthShop.Restore(UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot())), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That((back.SecondLab, back.LabIncubators.Count, back.LabBreeders.Count), Is.EqualTo((true, 2, 1)));
            Assert.That(back.LabIncubators[0].Queue, Is.EqualTo(shop.LabIncubators[0].Queue));
            Assert.That((back.LabIncubators[0].Incubating, back.LabIncubators[0].Pending, back.LabIncubators[1].Auto), Is.EqualTo((true, Species, Species)));
            Assert.That(back.LabIncubators[0].Left, Is.EqualTo(shop.LabIncubators[0].Left).Within(1e-4));
            Assert.That(back.LabBreeders[0].Pairs[0].Parents.Single().Id, Is.EqualTo(901));
            Run(back, back.IncubationSeconds);
            Assert.That(back.LabIncubators[0].Tray.Count, Is.EqualTo(back.Clutch), "it hatches after loading");
            var again = GrowthShop.Restore(back.Snapshot(), out problem);
            Assert.That(problem, Is.Null);
            Assert.That(again.LabIncubators[0].Tray.Select(x => x.Id), Is.EqualTo(back.LabIncubators[0].Tray.Select(x => x.Id)));
            int most = again.LabIncubators.SelectMany(i => i.Tray).Max(x => x.Id);
            again.DevelopmentGive("pondlet");
            Assert.That(again.Carried.Last().Id, Is.GreaterThan(most), "new creatures never take a saved one's id");
            // A save from before (version 17) has no second lab; one that cannot be is refused.
            var old = Ready(open: false).Snapshot(); old.version = 17; old.secondLab = false; old.labIncubators = null; old.labBreeders = null;
            var before = GrowthShop.Restore(old, out problem);
            Assert.That((problem, before.SecondLab, before.LabIncubators.Count), Is.EqualTo(((string)null, false, 0)));
            var broken = shop.Snapshot(); broken.secondLab = false;
            GrowthShop.Restore(broken, out problem);
            Assert.That(problem, Is.EqualTo("Second-lab machines without the second lab"));
            broken = shop.Snapshot(); // the parent in breeding tank 2 carried as well
            broken.carried = new[] { new GrowthSave.Specimen { id = 901, species = "pondlet" } };
            GrowthShop.Restore(broken, out problem);
            Assert.That(problem, Is.EqualTo("A creature is saved twice"));
            broken = shop.Snapshot(); broken.mossIncubator = 3;
            GrowthShop.Restore(broken, out problem);
            Assert.That(problem, Is.EqualTo("Moss at a machine that is not there"));
        }

        [Test]
        public void F69_TheSecondLabStartPoint_CanOpenItAndPlaceMachines()
        {
            var shop = GrowthStarts.Make(GrowthStarts.SecondLab);
            Assert.That(shop.Annex && !shop.SecondLab && shop.HasBreedingTank, Is.True);
            Assert.That(shop.SecondLabCheck(out string why), Is.EqualTo(GrowthFailure.None), why);
            Assert.That(shop.BuySecondLab().Success && shop.BuyLabIncubator().Success && shop.BuyLabBreeder().Success && shop.BuyLabIncubator().Success, Is.True);
            Assert.That(shop.HireCheck("moss", out why), Is.Not.EqualTo(GrowthFailure.Locked), "Moss can be hired there: " + why);
        }
    }
}
