using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-02: the greybox ruleset (GrowthShop): growers and the siphon, never-fail mixing, containers by spaces and
    /// schools, visitors, the counter, stars, the era 2 gate, unit B, station levels 1-2 and the GR save line (GR.15.1).</summary>
    public sealed class GrowthShopTests
    {
        static void Collect(GrowthShop shop, string culture, int doses)
        {
            for (int i = 0; i < doses; i++)
            {
                if (shop.GrowerUnits[culture] < 1) shop.Tick(shop.RefillSeconds * 2);
                Assert.That(shop.Collect(culture, shop.HoldSeconds).Success, Is.True, "collect " + culture);
            }
        }

        /// <summary>Collects, doses, incubates and hatches a mix, and takes the hatchling in hand.</summary>
        static GrowthSpecimen Make(GrowthShop shop, int bloom, int flow)
        {
            Collect(shop, "bloom", bloom); Collect(shop, "flow", flow);
            for (int i = 0; i < bloom; i++) Assert.That(shop.AddDose("bloom").Success, Is.True);
            for (int i = 0; i < flow; i++) Assert.That(shop.AddDose("flow").Success, Is.True);
            Assert.That(shop.StartIncubation().Success, Is.True);
            shop.Tick(shop.IncubationSeconds + 0.01f);
            var hatchling = shop.Hatchling;
            Assert.That(shop.TakeHatchling().Success, Is.True);
            return hatchling;
        }

        static GrowthShop Stocked(params string[] species)
        {
            var shop = new GrowthShop();
            foreach (var s in species) shop.Containers[0].Occupants.Add(new GrowthSpecimen { Id = 1000 + shop.Containers[0].Occupants.Count, Species = s });
            return shop;
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
        public void ANewShopStartsTinyAtNightWithOneFourSpaceTank()
        {
            var shop = new GrowthShop();
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            Assert.That(shop.Day, Is.EqualTo(1));
            Assert.That(shop.Era, Is.EqualTo(1));
            Assert.That(shop.Stars, Is.EqualTo(1));
            Assert.That(shop.Credits, Is.EqualTo(GrowthContent.StartingCredits));
            Assert.That(shop.Cultures, Is.EqualTo(new[] { "bloom", "flow" }));
            Assert.That(shop.Containers, Has.Count.EqualTo(1));
            Assert.That(shop.Containers[0].Kind, Is.EqualTo("starter"));
            Assert.That(shop.Containers[0].Spaces, Is.EqualTo(4));
            Assert.That(shop.Containers[0].Place, Is.EqualTo("a1"));
            Assert.That(GrowthShop.GreyboxStations.All(s => shop.Level(s) == 1), Is.True);
            Assert.That(shop.SiphonCapacity, Is.EqualTo(3));
            Assert.That(shop.MaxDoses, Is.EqualTo(3));
            Assert.That(shop.IncubationSeconds, Is.EqualTo(12));
            Assert.That(shop.Discovered, Is.Empty);
            Assert.That(shop.FreePlaces, Is.EqualTo(new[] { "a2", "a3", "a4" }));
        }

        [Test]
        public void TheSiphonCollectsOneDosePerHoldUntilFullAndGrowersRefill()
        {
            var shop = new GrowthShop();
            Assert.That(shop.Collect("bloom", 0.79f).Detail, Is.EqualTo("0"), "a dose takes the hold time");
            Assert.That(shop.Collect("bloom", 0.02f).Detail, Is.EqualTo("1"));
            Collect(shop, "flow", 2);
            Assert.That(shop.Held.Count, Is.EqualTo(3));
            Assert.That(shop.Collect("bloom", 5).Failure, Is.EqualTo(GrowthFailure.SiphonFull));
            Assert.That(shop.Collect("ember", 1).Failure, Is.EqualTo(GrowthFailure.NotOwned), "Ember is not a starting culture");
            Assert.That(shop.Collect("plasma", 1).Failure, Is.EqualTo(GrowthFailure.UnknownCulture));
            // Empty a grower, then let it refill one unit every 3 s (level 1).
            Assert.That(shop.AddDose("bloom").Success && shop.AddDose("flow").Success && shop.AddDose("flow").Success, Is.True);
            Assert.That(shop.Collect("flow", 0.8f * 2).Detail, Is.EqualTo("2"));
            Assert.That(shop.GrowerUnits["flow"], Is.LessThan(1));
            Assert.That(shop.Collect("flow", 1).Failure, Is.EqualTo(GrowthFailure.GrowerEmpty));
            shop.Tick(3);
            Assert.That(shop.GrowerUnits["flow"], Is.GreaterThanOrEqualTo(1));
            shop.Tick(100);
            Assert.That(shop.GrowerUnits["flow"], Is.EqualTo(4), "a level 1 grower holds 4");
        }

        [Test]
        public void TheIncubatorRefusesEmptyAndOversizedMixesBeforeAnyDoseIsUsed()
        {
            var shop = new GrowthShop();
            Assert.That(shop.StartIncubation().Failure, Is.EqualTo(GrowthFailure.ChamberEmpty));
            Collect(shop, "bloom", 3);
            Assert.That(shop.AddDose("flow").Failure, Is.EqualTo(GrowthFailure.NoDose));
            for (int i = 0; i < 3; i++) Assert.That(shop.AddDose("bloom").Success, Is.True);
            Collect(shop, "bloom", 1);
            Assert.That(shop.AddDose("bloom").Failure, Is.EqualTo(GrowthFailure.ChamberFull), "level 1 mixes 2-3 doses");
            Assert.That(shop.Held, Is.EqualTo(new[] { "bloom" }), "the refused dose stays in the siphon");
            Assert.That(shop.ReturnDose("bloom").Success, Is.True);
            Assert.That(shop.Chamber.Count, Is.EqualTo(2));
            Assert.That(shop.StartIncubation().Success, Is.True);
            Assert.That(shop.AddDose("bloom").Failure, Is.EqualTo(GrowthFailure.IncubatorBusy));
            Assert.That(shop.StartIncubation().Failure, Is.EqualTo(GrowthFailure.IncubatorBusy));
            shop.Tick(11.9f);
            Assert.That(shop.Hatchling, Is.Null);
            shop.Tick(0.2f);
            Assert.That(shop.Hatchling, Is.Not.Null);
            // The hatchling waits in the output; the next mix cannot start until it is taken.
            Assert.That(shop.AddDose("bloom").Success && shop.AddDose("bloom").Success, Is.True);
            Assert.That(shop.StartIncubation().Failure, Is.EqualTo(GrowthFailure.HatchlingWaiting));
            Assert.That(shop.TakeHatchling().Success, Is.True);
            Assert.That(shop.StartIncubation().Success, Is.True);
        }

        [Test]
        public void EveryMixMakesACreatureButOnlyAnExactRecipeFindsANewOne()
        {
            var shop = new GrowthShop();
            // The very first mix makes the nearest founder of all: two Bloom is one Flow from Bubblebelly.
            Collect(shop, "bloom", 2); shop.AddDose("bloom"); shop.AddDose("bloom");
            Assert.That(shop.StartIncubation().Detail, Is.EqualTo("bubblebelly"));
            shop.Tick(12.1f); shop.TakeHatchling();
            Assert.That(shop.Discovered, Is.EqualTo(new[] { "bubblebelly" }));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Discovery && e.Species == "bubblebelly"), Is.True);
            // Now two Flow makes the known Bubblebelly, and says it was one dose from something new (Ribbonfin is Bloom 1 + Flow 2).
            Collect(shop, "flow", 2); shop.AddDose("flow"); shop.AddDose("flow");
            Assert.That(shop.StartIncubation().Detail, Is.EqualTo("bubblebelly"));
            Assert.That(shop.LastMix.Exact, Is.False);
            Assert.That(shop.LastMix.WasNew, Is.False);
            Assert.That(shop.LastMix.DistanceToNew, Is.EqualTo(1), "close to something new");
            shop.Tick(12.1f); shop.TakeHatchling();
            // The exact recipe finds it.
            var ribbonfin = Make(shop, 1, 2);
            Assert.That(ribbonfin.Species, Is.EqualTo("ribbonfin"));
            Assert.That(shop.LastMix.Exact && shop.LastMix.WasNew, Is.True);
            Assert.That(shop.Rp, Is.EqualTo(2 * GrowthContent.RpFounder));
        }

        [Test]
        public void TheMixLogKeepsEachMixOnceAndTheKnownRecipeFillsTheChamber()
        {
            var shop = new GrowthShop();
            Make(shop, 1, 1); Make(shop, 1, 1);
            Assert.That(shop.MixLog, Has.Count.EqualTo(1));
            Assert.That(shop.MixLog[0].Count, Is.EqualTo(2));
            Assert.That(shop.MixLog[0].Species, Is.EqualTo("pondlet"));
            Assert.That(shop.FillRecipe("bubblebelly").Failure, Is.EqualTo(GrowthFailure.NotDiscovered));
            Assert.That(shop.FillRecipe("puddlepup").Failure, Is.EqualTo(GrowthFailure.NotAFounder));
            Collect(shop, "bloom", 1);
            Assert.That(shop.FillRecipe("pondlet").Failure, Is.EqualTo(GrowthFailure.NoDose), "a Flow dose is missing");
            Assert.That(shop.FillRecipe("pondlet").Detail, Does.Contain("Flow"));
            Collect(shop, "flow", 1);
            Assert.That(shop.FillRecipe("pondlet").Success, Is.True);
            Assert.That(shop.Chamber.OrderBy(c => c), Is.EqualTo(new[] { "bloom", "flow" }));
            Assert.That(shop.Held, Is.Empty);
        }

        [Test]
        public void HandsCarryThreeAndTanksTakeOnlyWhatFits()
        {
            var shop = new GrowthShop();
            for (int i = 0; i < 3; i++) Make(shop, 1, 1);
            Collect(shop, "bloom", 1); Collect(shop, "flow", 1); shop.AddDose("bloom"); shop.AddDose("flow"); shop.StartIncubation(); shop.Tick(12.1f);
            Assert.That(shop.TakeHatchling().Failure, Is.EqualTo(GrowthFailure.HandsFull));
            var tank = shop.Containers[0];
            for (int i = 0; i < 3; i++) Assert.That(shop.PutIn(tank.Id).Success, Is.True);
            Assert.That(shop.PutIn(tank.Id).Failure, Is.EqualTo(GrowthFailure.HandsEmpty));
            Assert.That(shop.TakeHatchling().Success && shop.PutIn(tank.Id).Success, Is.True);
            Assert.That(tank.Used, Is.EqualTo(4));
            Assert.That(tank.InSchool("pondlet"), Is.True, "four Pondlets swim as a school");
            // A medium creature takes two spaces: it does not fit a tank with one free space.
            tank.Occupants.RemoveAt(0);
            shop.Carried.Add(new GrowthSpecimen { Id = 900, Species = "emberpuff" });
            Assert.That(shop.PutIn(tank.Id).Failure, Is.EqualTo(GrowthFailure.DoesNotFit));
            shop.Carried.Clear();
            Assert.That(shop.TakeOut(tank.Id, tank.Occupants[0].Id).Success, Is.True);
            Assert.That(shop.Carried, Has.Count.EqualTo(1));
            Assert.That(shop.PutIn("tank-404").Failure, Is.EqualTo(GrowthFailure.NoSuchContainer));
        }

        [Test]
        public void VisitorsReserveOneSpecimenAndPayWhenTheCounterIsHeldLongEnough()
        {
            var shop = Stocked("pondlet", "bubblebelly");
            Assert.That(shop.OpenShop().Success, Is.True);
            Run(shop, 15);
            var visitor = shop.FrontOfQueue;
            Assert.That(visitor, Is.Not.Null, "a visitor browsed, chose and queued");
            var tank = shop.Containers[0];
            var reserved = tank.Occupants.First(o => o.Id == visitor.SpecimenId);
            Assert.That(reserved.ReservedBy, Is.EqualTo(visitor.Id));
            Assert.That(shop.TakeOut(tank.Id, reserved.Id).Failure, Is.EqualTo(GrowthFailure.Reserved), "a reserved creature cannot be moved");
            int credits = shop.Credits, price = shop.Price(reserved.Species);
            Assert.That(shop.Serve(1.19f).Detail, Is.EqualTo("bagging"), "level 1 counter: hold 1.2 s");
            Assert.That(shop.Serve(0.02f).Detail, Is.EqualTo("sold"));
            Assert.That(shop.Credits, Is.EqualTo(credits + price));
            Assert.That(shop.Reputation, Is.EqualTo(GrowthContent.RepPerSale));
            Assert.That(tank.Occupants.Any(o => o.Id == reserved.Id), Is.False);
            Assert.That(shop.SalesTotal, Is.EqualTo(1));
            Assert.That(shop.Serve(1).Failure == GrowthFailure.NoVisitorWaiting || shop.FrontOfQueue != null, Is.True);
        }

        [Test]
        public void AnEmptyShopDrawsNobodyInAndCostsNoReputation()
        {
            var shop = new GrowthShop { Reputation = 10 };
            shop.OpenShop();
            Run(shop, GrowthContent.DaySeconds + 1);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            Assert.That(shop.LastDay.Visitors, Is.Zero);
            Assert.That(shop.LastDay.WalkedPast, Is.GreaterThan(0));
            Assert.That(shop.Reputation, Is.EqualTo(10));
        }

        [Test]
        public void NothingAffordableCostsReputationAndWaitingTooLongToo()
        {
            // A Brook Monarch (30 credits) is beyond every one-star budget (20 and 10).
            var shop = Stocked("brookmonarch");
            shop.Reputation = 20;
            shop.OpenShop();
            Run(shop, 20);
            Assert.That(shop.LastDay ?? shop.Today, Is.Not.Null);
            Assert.That(shop.Today.Refusals, Is.GreaterThan(0));
            Assert.That(shop.Reputation, Is.LessThan(20));
            // A queued visitor who is never served leaves after their patience and releases the creature.
            var waiting = Stocked("pondlet");
            waiting.OpenShop();
            Run(waiting, 12);
            var v = waiting.FrontOfQueue;
            Assert.That(v, Is.Not.Null);
            Run(waiting, GrowthShop.QueuePatience + 1);
            Assert.That(v.Outcome, Is.EqualTo(GrowthVisitOutcome.TiredOfWaiting));
            Assert.That(waiting.Containers[0].Occupants.All(o => o.ReservedBy == 0 || o.ReservedBy != v.Id), Is.True);
        }

        [Test]
        public void ASchoolsSpeciesIsPreferredAndStretchesTheBudget()
        {
            var shop = Stocked("ribbonfin", "ribbonfin", "ribbonfin", "bubblebelly");
            Assert.That(shop.Containers[0].InSchool("ribbonfin"), Is.True);
            Assert.That(shop.Containers[0].InSchool("bubblebelly"), Is.False);
            shop.OpenShop();
            Run(shop, 10);
            var v = shop.Visitors.First(x => x.State == GrowthVisitorState.Queued);
            Assert.That(shop.Containers[0].Occupants.First(o => o.Id == v.SpecimenId).Species, Is.EqualTo("ribbonfin"), "same price, the school wins");
            Assert.That(GrowthShop.SchoolBudgetBonus, Is.EqualTo(1.15f));
        }

        [Test]
        public void ADayLastsFiveAndAHalfMinutesAndClosingReleasesEveryone()
        {
            var shop = Stocked("pondlet", "pondlet", "pondlet", "pondlet");
            Assert.That(shop.BuyTank("a2").Failure, Is.EqualTo(GrowthFailure.NotEnoughCredits), "a tank costs 20; a new shop has 10");
            shop.OpenShop();
            Assert.That(shop.DaySecondsLeft, Is.EqualTo(330));
            Assert.That(shop.OpenShop().Failure, Is.EqualTo(GrowthFailure.NotAtNight));
            Assert.That(shop.BuyTank("a3").Failure, Is.EqualTo(GrowthFailure.NotAtNight), "layout changes happen at night");
            Run(shop, 329);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Day));
            Run(shop, 1.5f);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            Assert.That(shop.Day, Is.EqualTo(2));
            Assert.That(shop.Stock.All(s => s.ReservedBy == 0), Is.True);
            Assert.That(shop.Visitors.All(v => v.State == GrowthVisitorState.Leaving), Is.True);
            Run(shop, GrowthShop.LeaveSeconds + 1);
            Assert.That(shop.Visitors, Is.Empty);
            Assert.That(shop.LastDay.Day, Is.EqualTo(1));
        }

        [Test]
        public void TwoStarsThreeCreaturesAndTwoTanksOpenEraTwoAndUnitB()
        {
            var shop = new GrowthShop { Credits = 1000 };
            Make(shop, 1, 1); Make(shop, 2, 1); Make(shop, 1, 2);
            Assert.That(shop.BuyTank("a2").Success, Is.True);
            Assert.That(shop.Era, Is.EqualTo(1), "still one star");
            Assert.That(shop.BuyUnitB().Failure, Is.EqualTo(GrowthFailure.Locked));
            shop.Reputation = GrowthContent.StarThresholds[1];
            Assert.That(shop.Stars, Is.EqualTo(2));
            Assert.That(shop.BuyTank("a3").Success, Is.True, "any change checks the gate");
            Assert.That(shop.Era, Is.EqualTo(2));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.EraUp), Is.True);
            Assert.That(shop.BuyTank("b1").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace));
            shop.OpenShop();
            Assert.That(shop.BuyUnitB().Failure, Is.EqualTo(GrowthFailure.NotAtNight));
            Run(shop, 331);
            int before = shop.Credits;
            shop.Credits = 399;
            Assert.That(shop.BuyUnitB().Failure, Is.EqualTo(GrowthFailure.NotEnoughCredits));
            shop.Credits = before;
            Assert.That(shop.BuyUnitB().Success, Is.True);
            Assert.That(shop.UnitB, Is.True);
            Assert.That(shop.OwnedPlaces.Count(), Is.EqualTo(8));
            Assert.That(shop.VisitorsAtOnce, Is.EqualTo(3));
            Assert.That(shop.BuyTank("b1").Success, Is.True);
            Assert.That(shop.BuyUnitB().Failure, Is.EqualTo(GrowthFailure.MaxLevel));
        }

        [Test]
        public void StationsUpgradeOnceInTheGreyboxWithTheirCostsAndWhenIdle()
        {
            var shop = new GrowthShop();
            Assert.That(shop.Upgrade("siphon").Failure, Is.EqualTo(GrowthFailure.NotEnoughCredits));
            shop.Credits = 200;
            Assert.That(shop.Upgrade("siphon").Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(185));
            Assert.That(shop.SiphonCapacity, Is.EqualTo(6));
            Assert.That(shop.HoldSeconds, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(shop.Upgrade("siphon").Failure, Is.EqualTo(GrowthFailure.MaxLevel), "level 3 waits for a later task");
            Assert.That(shop.Upgrade("grower").Success, Is.True);
            Assert.That(shop.GrowerCapacity, Is.EqualTo(6));
            Assert.That(shop.Upgrade("counter").Success, Is.True);
            Assert.That(shop.ServeSeconds, Is.EqualTo(0.8f).Within(0.001f));
            Collect(shop, "bloom", 1); Collect(shop, "flow", 1); shop.AddDose("bloom"); shop.AddDose("flow"); shop.StartIncubation();
            Assert.That(shop.Upgrade("incubator").Failure, Is.EqualTo(GrowthFailure.Busy), "an upgrade installs when the station is idle");
            shop.Tick(12.1f); shop.TakeHatchling();
            Assert.That(shop.Upgrade("incubator").Success, Is.True);
            Assert.That(shop.MaxDoses, Is.EqualTo(4));
            Assert.That(shop.IncubationSeconds, Is.EqualTo(8));
            Assert.That(shop.Upgrade("breeding").Failure, Is.EqualTo(GrowthFailure.MaxLevel), "not in the greybox");
        }

        [Test]
        public void TheSaveLineKeepsTheShopAndRefusesSavesThatAreNotOne()
        {
            var shop = new GrowthShop { Credits = 300 };
            Make(shop, 1, 1); Make(shop, 2, 1);
            shop.PutIn(shop.Containers[0].Id);
            shop.Upgrade("siphon"); shop.BuyTank("a3");
            Collect(shop, "flow", 2); shop.AddDose("flow"); shop.StartIncubation(); shop.Tick(5);
            Collect(shop, "bloom", 1);
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json), "a saved shop restores exactly");
            Assert.That(back.Incubating && back.IncubationLeft == shop.IncubationLeft, Is.True);
            back.Tick(back.IncubationLeft + 0.1f);
            Assert.That(back.Hatchling, Is.Not.Null, "the incubation resumes");
            // Broken saves are refused, never half-read.
            var bad = JsonUtility.FromJson<GrowthSave>(json); bad.version = 99;
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null); Assert.That(problem, Does.Contain("version"));
            bad = JsonUtility.FromJson<GrowthSave>(json); bad.containers[0].occupants[0].species = "unicorn";
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null); Assert.That(problem, Does.Contain("species"));
            bad = JsonUtility.FromJson<GrowthSave>(json); bad.carried = new[] { bad.containers[0].occupants[0] };
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null); Assert.That(problem, Does.Contain("twice"));
            bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.containers[1].occupants = Enumerable.Range(0, 5).Select(i => new GrowthSave.Specimen { id = 500 + i, species = "pondlet", colour = "Classic" }).ToArray();
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null); Assert.That(problem, Does.Contain("fits"));
            bad = JsonUtility.FromJson<GrowthSave>(json); bad.levels[0] = 3;
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null); Assert.That(problem, Does.Contain("level"));
        }

        [Test]
        public void TheSameNightAndDayPlayOutTheSameWay()
        {
            string Play()
            {
                var shop = Stocked("pondlet", "bubblebelly", "ribbonfin", "pondlet");
                shop.OpenShop();
                Run(shop, 400, serve: true);
                return JsonUtility.ToJson(shop.Snapshot()) + "/" + shop.LastDay.Visitors + "/" + shop.LastDay.Sales + "/" + shop.LastDay.Revenue;
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }

        [Test]
        public void TheObjectiveBoardFollowsTheEraGoal()
        {
            var shop = new GrowthShop();
            Assert.That(shop.EraGoal, Is.EqualTo(GrowthContent.Eras[0].Goal));
            var lines = shop.Objectives();
            Assert.That(lines.Select(l => l.text), Has.Some.StartsWith("Discover 3 creatures (0/3)"));
            Assert.That(lines.Select(l => l.text), Has.Some.StartsWith("Own 2 tanks (1/2)"));
            shop.Era = 2;
            Assert.That(shop.Objectives().Single().text, Does.StartWith("Open shop unit B"));
            shop.UnitB = true;
            Assert.That(shop.Objectives().Single().done, Is.True);
        }
    }
}
