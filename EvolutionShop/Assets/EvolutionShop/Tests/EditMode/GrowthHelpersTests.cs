using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-09: GR.10's helpers in the greybox: Pip, Nori and Moss earned by stars and an achievement, hired at night,
    /// paid once each duty period (§6.3: never as debt; short of credits a helper stays home), trained through three levels;
    /// Pip at the till and the counter's second till; Nori keeping the shop's tanks at the stock targets; Moss running a known
    /// recipe by hand and keeping a known pair breeding; their walks between the rooms; the version 8 save (GR.10, GR.15.1).</summary>
    public sealed class GrowthHelpersTests
    {
        static GrowthShop At(int era, int stars, int credits = 50000, int rp = 500) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = rp, SalesTotal = GrowthContent.PipSalesGate };

        static void Hire(GrowthShop shop, string id) => Assert.That(shop.Hire(id).Success, Is.True, "hire " + id);

        static GrowthContainer Large(GrowthShop shop, string place = "a3")
        {
            var c = new GrowthContainer { Id = "large-" + place, Kind = "large", Place = place };
            shop.Containers.Add(c);
            return c;
        }

        static int nextId = 7000;
        static GrowthSpecimen Make(string species) => new GrowthSpecimen { Id = nextId++, Species = species };

        static void Run(GrowthShop shop, float seconds, float step = 0.1f) { for (float t = 0; t < seconds; t += step) shop.Tick(step); }

        static void Queued(GrowthShop shop, GrowthSpecimen s)
        {
            var v = new GrowthVisitor { Id = nextId++, Type = "casual", Budget = 500, State = GrowthVisitorState.Queued, Patience = 40 };
            Assert.That(shop.ReserveAll(v, new List<GrowthSpecimen> { s }), Is.True);
            shop.Visitors.Add(v);
        }

        static GrowthSpecimen Stock(GrowthShop shop, GrowthContainer c, string species)
        {
            Assert.That(shop.DevelopmentStock(c.Id, species).Success, Is.True);
            return c.Occupants.Last();
        }

        [Test]
        public void HelpersAreEarnedByStarsAndAnAchievementAndHiredAtNight()
        {
            var one = At(2, 1);
            Assert.That(one.HireCheck("pip", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 2 stars"));
            var few = At(2, 2); few.SalesTotal = 149;
            few.HireCheck("pip", out why);
            Assert.That(why, Is.EqualTo("Needs 150 sales (149 so far)"));
            var day = At(2, 2); day.OpenShop();
            Assert.That(day.HireCheck("pip", out _), Is.EqualTo(GrowthFailure.NotAtNight));
            var poor = At(2, 2, credits: 107);
            poor.HireCheck("pip", out why);
            Assert.That(why, Is.EqualTo("Needs 108 credits: the fee and tonight's wage"));
            var shop = At(3, 3);
            int credits = shop.Credits;
            Hire(shop, "pip");
            var pip = shop.Helper("pip");
            Assert.That((credits - shop.Credits, pip.Level, pip.OnDuty, pip.PaidPeriod), Is.EqualTo((108, 1, true, shop.Day)), "the fee and this duty period's wage");
            Assert.That(shop.HireCheck("pip", out _), Is.EqualTo(GrowthFailure.Owned));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Hired && e.Text.StartsWith("Pip joins the shop as its cashier")), Is.True);
            shop.HireCheck("nori", out why);
            Assert.That(why, Is.EqualTo("Needs a Large tank in the shop"));
            Large(shop);
            Hire(shop, "nori");
            shop.HireCheck("moss", out why);
            Assert.That(why, Is.EqualTo("Needs a powered generator: the bubble turbine and electric pumps"));
            shop.Levels["generator"] = 2; shop.Levels["pump"] = 2;
            Hire(shop, "moss");
            Assert.That(shop.Staff.Keys, Is.EquivalentTo(new[] { "pip", "nori", "moss" }));
            Assert.That(shop.WageBill, Is.EqualTo(8 + 25 + 60));
            Assert.That(At(3, 2).HireCheck("nori", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 3 stars"));
        }

        [Test]
        public void WagesArePaidOnceADutyPeriodAndNeverAsDebt()
        {
            var shop = At(3, 3);
            Large(shop);
            Hire(shop, "pip"); Hire(shop, "nori");
            shop.TakeEvents();
            Assert.That(shop.OpenShop().Success, Is.True);
            int before = shop.Credits;
            shop.Tick(shop.DaySecondsLeft + 1);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            Assert.That(before - shop.Credits, Is.EqualTo(8 + 25), "a new duty period (the night and tomorrow): each wage once");
            Assert.That(shop.Staff.Values.All(h => h.PaidPeriod == shop.Day), Is.True);
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Wages && e.Text == "Wages for tonight and tomorrow: 33 credits (Pip 8, Nori 25)"), Is.True);
            // Home and back within a paid period: no second charge.
            int paid = shop.Credits;
            Assert.That(shop.SetDuty("nori", false).Success && shop.SetDuty("nori", true).Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(paid));
            // Short of credits at the next period: Pip (paid first) works, Nori stays home and says so; never below zero.
            shop.Credits = 10;
            shop.OpenShop(); shop.Tick(shop.DaySecondsLeft + 1);
            Assert.That((shop.Credits >= 0, shop.Helper("pip").OnDuty, shop.Helper("nori").OnDuty), Is.EqualTo((true, true, false)));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.StaffHome && e.Text.StartsWith("Nori stays home: the wage is 25 credits")), Is.True);
            Assert.That(shop.SetDuty("nori", true).Failure, Is.EqualTo(GrowthFailure.NotEnoughCredits));
            shop.Credits = 100;
            Assert.That(shop.SetDuty("nori", true).Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(75), "back on duty: the period's wage, once");
            shop.SetDuty("nori", false); shop.SetDuty("nori", true);
            Assert.That(shop.Credits, Is.EqualTo(75));
            // A helper at home costs nothing at the next period.
            shop.SetDuty("nori", false);
            shop.OpenShop(); shop.Tick(shop.DaySecondsLeft + 1);
            Assert.That(shop.Credits, Is.EqualTo(75 - 8));
        }

        [Test]
        public void PipServesAtTheTillAndTheSecondTillLetsThePlayerServeBeside()
        {
            var shop = At(4, 4);
            Hire(shop, "pip");
            shop.Tick(0.1f);
            Assert.That((shop.Helper("pip").Status, shop.Helper("pip").Working), Is.EqualTo(("Waiting for the shop to open", false)), "Pip serves by day");
            var tank = shop.Containers[0];
            shop.OpenShop();
            var a = Stock(shop, tank, "pondlet"); var b = Stock(shop, tank, "pondlet");
            Queued(shop, a); Queued(shop, b);
            Assert.That(shop.Serve(5f).Failure, Is.EqualTo(GrowthFailure.Busy), "one till: Pip's");
            shop.Tick(2.9f);
            Assert.That(tank.Occupants.Count, Is.EqualTo(2), "3 s to bag an order at level 1");
            shop.Tick(0.2f);
            Assert.That((tank.Occupants.Count, shop.SalesTotal), Is.EqualTo((1, GrowthContent.PipSalesGate + 1)));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Sale && e.Text.StartsWith("Pip sold Pondlet")), Is.True);
            Assert.That(shop.Helper("pip").Status, Does.StartWith("Bagging an order"), "on to the next visitor");
            // The second till (counter level 4): the player serves the next visitor while Pip serves the front.
            var night = At(4, 4);
            night.Levels["counter"] = 3;
            night.UpgradeCheck("counter", out _, out string why);
            Assert.That(why, Is.EqualTo("Hire Pip first: the second till is for a helper"));
            Hire(night, "pip");
            Assert.That(night.Upgrade("counter").Success && night.SecondTill, Is.True);
            night.OpenShop();
            var t = night.Containers[0];
            var x = Stock(night, t, "pondlet"); var y = Stock(night, t, "ribbonfin");
            Queued(night, x); Queued(night, y);
            night.Tick(0.1f);
            Assert.That(night.Serve(5f).Detail, Is.EqualTo("sold"), "the player serves beside Pip");
            Assert.That(t.Occupants.Single().Id, Is.EqualTo(x.Id), "the player's sale was the visitor Pip was not serving");
            night.Tick(3f);
            Assert.That(t.Occupants.Count, Is.EqualTo(0));
            night.StopServing();
            // At the second till the player holding the counter serves the front of the queue, and Pip the one behind.
            var p1 = Stock(night, t, "pondlet"); var p2 = Stock(night, t, "ribbonfin");
            Assert.That(night.Serve(0.1f).Failure, Is.EqualTo(GrowthFailure.NoVisitorWaiting), "at the till, no one waiting yet");
            Queued(night, p1);
            night.Tick(0.1f);
            Assert.That(night.Helper("pip").Status, Is.EqualTo("Waiting for a visitor at the till"), "the front is the player's while the player is at the till");
            Assert.That(night.Serve(1f).Detail, Is.EqualTo("sold"));
            Assert.That(t.Occupants.Single().Id, Is.EqualTo(p2.Id));
            night.StopServing();
            Queued(night, p2);
            night.Tick(0.1f);
            Assert.That(night.Helper("pip").Status, Does.StartWith("Bagging an order"), "away from the till, Pip takes the front");
            // Level 3 calls the next visitor over: the queue holds one more; at home, the player serves alone.
            int queue = night.QueueLength;
            night.Helper("pip").Level = 3;
            Assert.That(night.QueueLength, Is.EqualTo(queue + 1));
            night.SetDuty("pip", false);
            Assert.That(night.QueueLength, Is.EqualTo(queue));
        }

        [Test]
        public void NoriKeepsTheShopsTanksAtTheirTargetsWalkingBetweenTheRooms()
        {
            var shop = At(3, 3);
            var large = Large(shop);
            Hire(shop, "nori");
            shop.Discovered.Add("pondlet");
            for (int i = 0; i < 3; i++) shop.Hatchlings.Add(Make("pondlet"));
            shop.Tick(0.1f);
            Assert.That(shop.Helper("nori").Status, Is.EqualTo("Nothing to move: set stock targets on my card"));
            shop.SetTarget("pondlet", 2);
            shop.Tick(0.1f);
            var nori = shop.Helper("nori");
            Assert.That((nori.Step, nori.Status), Is.EqualTo(("fetch", "Fetching a Pondlet from the incubator's tray")));
            float fetch = GrowthWalk.Seconds(GrowthWalk.NoriHome, GrowthWalk.Tray);
            Assert.That(nori.StepSeconds, Is.EqualTo(fetch).Within(0.001f), "a walk at walking speed and a moment to pick up");
            Run(shop, fetch + 0.05f, 0.05f);
            Assert.That((nori.Hands.Count, shop.Hatchlings.Count, nori.Step), Is.EqualTo((1, 2, "deliver")), "one at a time at level 1");
            Assert.That(nori.Status, Is.EqualTo("Carrying a Pondlet to the starter tank at a1"), "both tanks are empty: the first by place");
            while (nori.Hands.Count > 0) shop.Tick(0.01f);
            Assert.That(nori.Status, Is.EqualTo("Put a Pondlet in the starter tank at a1"), "and says so once it is in");
            Run(shop, nori.StepSeconds + 0.1f, 0.05f);
            Run(shop, 30f);
            Assert.That(shop.Shown("pondlet"), Is.EqualTo(2), "to the target");
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(1), "GR.15.1: the third waits on the tray: no rack to take it");
            Assert.That(nori.Status, Is.EqualTo("No room for what is waiting: the tanks are at their targets or full, and no rack has room"));
            // The tank holding most of the species first, so schools form.
            var holder = shop.ShopContainers.Single(c => c.Occupants.Any());
            Assert.That(holder.Occupants.Count, Is.EqualTo(2));
            // The nursery: an offspring below its target goes to the tank.
            shop.Discovered.Add("puddlepup"); shop.SetTarget("puddlepup", 1);
            shop.Levels["breeding"] = 1;
            shop.Pairs[0].Offspring = Make("puddlepup");
            Run(shop, 30f);
            Assert.That((shop.Pairs[0].Offspring, shop.Shown("puddlepup")), Is.EqualTo(((GrowthSpecimen)null, 1)));
            // Level 2 carries two at a time.
            shop.Hatchlings.Clear();
            foreach (var c in shop.ShopContainers) c.Occupants.Clear();
            shop.SetTarget("pondlet", 2);
            shop.Helper("nori").Level = 2;
            shop.Hatchlings.Add(Make("pondlet")); shop.Hatchlings.Add(Make("pondlet"));
            Run(shop, 0.2f);
            Run(shop, nori.StepSeconds + 0.1f, 0.05f);
            Assert.That(nori.Hands.Count, Is.EqualTo(2));
            Run(shop, 30f);
            Assert.That((shop.Shown("pondlet"), shop.Hatchlings.Count), Is.EqualTo((2, 0)));
        }

        [Test]
        public void NoriRestocksFromTheRacksAndLeavesTheTrayToTheSorter()
        {
            var shop = At(4, 4);
            shop.BuyAnnex();
            foreach (var (st, lv) in new[] { ("grower", 3), ("generator", 2), ("pump", 2) }) while (shop.Level(st) < lv) Assert.That(shop.Upgrade(st).Success, Is.True);
            shop.BuyWorkshop(); shop.Upgrade("sorter");
            Assert.That(shop.LayPipe(GrowthLab.Out, GrowthLab.Sorter).Success, Is.True);
            Assert.That(shop.BuyRack("r1").Success, Is.True);
            var rack = shop.Racks.Single();
            Large(shop);
            Hire(shop, "nori");
            shop.Discovered.AddRange(new[] { "pondlet", "ribbonfin" });
            Stock(shop, rack, "ribbonfin"); Stock(shop, rack, "ribbonfin"); Stock(shop, rack, "ribbonfin");
            shop.SetTarget("ribbonfin", 2);
            shop.Helper("nori").Level = 3;
            Run(shop, 60f);
            Assert.That((shop.Shown("ribbonfin"), rack.Occupants.Count), Is.EqualTo((2, 1)), "from the rack to the shop's tank, to the target and no more");
            // With the chute laid the tray is the sorter's: Nori leaves it.
            shop.SetTarget("pondlet", 0);
            shop.Hatchlings.Add(Make("pondlet"));
            shop.Levels["generator"] = 0;
            Run(shop, 20f);
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(1), "the sorter (without power) has it; Nori does not take it");
            // A walk from the workshop to the shop goes through the annex, the back room and unit A's doorways.
            var path = GrowthWalk.Path(GrowthWalk.Places["r1"], GrowthWalk.Places["a3"]);
            Assert.That(path.Select(GrowthWalk.RoomOf).Distinct(), Is.EqualTo(new[] { GrowthWalk.Room.Workshop, GrowthWalk.Room.Annex, GrowthWalk.Room.Back, GrowthWalk.Room.A }));
            Assert.That(GrowthWalk.Seconds(GrowthWalk.Places["r1"], GrowthWalk.Places["a3"]), Is.GreaterThan(GrowthWalk.Seconds(GrowthWalk.Tray, GrowthWalk.Places["a3"])));
        }

        [Test]
        public void MossRunsAKnownRecipeByHandAndKeepsTheChamberHis()
        {
            var shop = At(3, 3);
            shop.Levels["generator"] = 2; shop.Levels["pump"] = 2; shop.Levels["incubator"] = 2;
            Hire(shop, "moss");
            Assert.That(shop.SetMossRecipe("pondlet").Failure, Is.EqualTo(GrowthFailure.NotDiscovered), "never something new");
            shop.Discovered.Add("pondlet");
            Assert.That(shop.SetMossRecipe("puddlepup").Failure, Is.EqualTo(GrowthFailure.NotAFounder));
            Assert.That(shop.SetMossRecipe("pondlet").Success, Is.True);
            shop.Tick(0.05f);
            var moss = shop.Helper("moss");
            Assert.That((moss.Step, moss.Place, shop.MossBatch), Is.EqualTo(("dose-out", "grower:bloom", true)), "to the Bloom grower first");
            float grower = shop.GrowerUnits["bloom"];
            Run(shop, moss.StepSeconds + 0.05f, 0.05f);
            Assert.That((moss.Dose, shop.GrowerUnits["bloom"] <= grower - 1 + 0.2f), Is.EqualTo(("bloom", true)), "a dose taken from the grower");
            shop.Held.Add("flow");
            Assert.That(shop.AddDose("flow").Detail, Is.EqualTo("Moss is dosing"), "the player's doses keep out of Moss's batch");
            for (int i = 0; i < 2000 && !shop.Incubating; i++) shop.Tick(0.05f);
            Assert.That((shop.Incubating, shop.Pending, shop.MossBatches, shop.MossBatch), Is.EqualTo((true, "pondlet", 1, false)), "two doses fetched one by one, then incubated");
            Assert.That(shop.TakeEvents().Any(e => e.Text == "Moss started a Pondlet"), Is.True);
            Run(shop, shop.IncubationSeconds + 1f);
            Assert.That(moss.Status, Is.EqualTo("Waiting for the clutch to be taken off the tray"));
            // Home mid-batch: the doses already in stay in the chamber for the player.
            shop.Hatchlings.Clear();
            Run(shop, 0.2f);
            while (moss.Step != null || moss.Dose != null || shop.Chamber.Count == 0) shop.Tick(0.05f);
            Assert.That(shop.SetDuty("moss", false).Success, Is.True);
            Run(shop, 10f);
            Assert.That((shop.MossBatch, shop.Chamber.Count, shop.Incubating), Is.EqualTo((false, 1, false)));
            Assert.That(shop.AddDose("flow").Success, Is.True, "the chamber is the player's again");
        }

        [Test]
        public void MossKeepsAKnownPairBreedingFromLevelTwoAndDoesBothAtLevelThree()
        {
            var shop = At(3, 3);
            shop.Levels["generator"] = 2; shop.Levels["pump"] = 2; shop.Levels["breeding"] = 1;
            Hire(shop, "moss");
            shop.Discovered.AddRange(new[] { "pondlet", "bubblebelly" });
            Assert.That(shop.SetMossTask(GrowthShop.MossPairJob).Failure, Is.EqualTo(GrowthFailure.Locked), "a pair comes with level 2");
            Assert.That(shop.SetMossPair("pondlet", "bubblebelly").Failure, Is.EqualTo(GrowthFailure.NotDiscovered), "only a pair bred before");
            shop.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
            Assert.That(shop.SetMossPair("pondlet", "bubblebelly").Success, Is.True);
            Assert.That(shop.TrainCheck("moss", out _), Is.EqualTo(GrowthFailure.None));
            Assert.That(shop.Train("moss").Success && shop.SetMossTask(GrowthShop.MossPairJob).Success, Is.True);
            var tank = shop.Containers[0];
            var a = Stock(shop, tank, "pondlet"); var b = Stock(shop, tank, "bubblebelly");
            Run(shop, 60f);
            Assert.That(shop.Pairs[0].Parents.Select(p => p.Id), Is.EquivalentTo(new[] { a.Id, b.Id }), "both parents carried to the breeding tank");
            Assert.That(shop.Breeding, Is.True);
            Run(shop, shop.BreedSeconds + 1f);
            Assert.That(shop.Pairs[0].Offspring?.Species, Is.EqualTo("puddlepup"));
            Assert.That(shop.Helper("moss").Status, Is.EqualTo("Breeding: an offspring waits in the nursery"));
            // A reserved parent is never taken; another pair in the tank leaves Moss waiting and saying so.
            var other = At(3, 3); other.Levels["generator"] = 2; other.Levels["pump"] = 2; other.Levels["breeding"] = 1;
            Hire(other, "moss"); other.Train("moss");
            other.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            other.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
            other.SetMossPair("pondlet", "bubblebelly"); other.SetMossTask(GrowthShop.MossPairJob);
            other.Pairs[0].Parents.Add(Make("ribbonfin"));
            other.Tick(0.1f);
            Assert.That(other.Helper("moss").Status, Is.EqualTo("The breeding tank holds another pair"));
            // Level 3: both jobs, in turn.
            var both = At(3, 3); both.Levels["generator"] = 2; both.Levels["pump"] = 2; both.Levels["breeding"] = 1;
            Hire(both, "moss"); both.Train("moss"); both.Train("moss");
            both.Discovered.AddRange(new[] { "pondlet", "bubblebelly" });
            both.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
            both.SetMossPair("pondlet", "bubblebelly"); both.SetMossRecipe("pondlet");
            var t = both.Containers[0];
            Stock(both, t, "pondlet"); Stock(both, t, "bubblebelly");
            Run(both, 90f);
            Assert.That((both.Pairs[0].Parents.Count, both.MossBatches >= 1), Is.EqualTo((2, true)), "the pair in and a batch dosed");
            // A batch Moss is dosing is finished before he turns to the pair (its doses would otherwise hold the chamber).
            var turn = At(3, 3); turn.Levels["generator"] = 2; turn.Levels["pump"] = 2; turn.Levels["breeding"] = 1;
            Hire(turn, "moss"); turn.Train("moss");
            turn.Discovered.AddRange(new[] { "pondlet", "bubblebelly" });
            turn.PairLog.Add(new GrowthPairEntry { A = "bubblebelly", B = "pondlet", Species = "puddlepup" });
            turn.SetMossPair("pondlet", "bubblebelly"); turn.SetMossRecipe("pondlet");
            var u = turn.Containers[0];
            Stock(turn, u, "pondlet"); Stock(turn, u, "bubblebelly");
            for (int i = 0; i < 2000 && turn.Chamber.Count == 0; i++) turn.Tick(0.05f);
            Assert.That((turn.MossBatch, turn.Chamber.Count), Is.EqualTo((true, 1)), "one dose of the batch in");
            Assert.That(turn.SetMossTask(GrowthShop.MossPairJob).Success, Is.True);
            for (int i = 0; i < 2000 && !turn.Incubating; i++) turn.Tick(0.05f);
            Assert.That((turn.MossBatches, turn.MossBatch, turn.Pairs[0].Parents.Count), Is.EqualTo((1, false, 0)), "the batch finished first, no parent fetched meanwhile");
            Run(turn, 60f);
            Assert.That(turn.Pairs[0].Parents.Count, Is.EqualTo(2), "then the pair");
        }

        [Test]
        public void HelpersTrainAtNightAndTheirWageRisesFromTheNextPeriod()
        {
            var shop = At(3, 3);
            Hire(shop, "pip");
            shop.OpenShop();
            Assert.That(shop.TrainCheck("pip", out _), Is.EqualTo(GrowthFailure.NotAtNight));
            shop.Tick(shop.DaySecondsLeft + 1);
            int credits = shop.Credits;
            Assert.That(shop.Train("pip").Success, Is.True);
            Assert.That((credits - shop.Credits, shop.Helper("pip").Level), Is.EqualTo((300, 2)));
            Assert.That(shop.TakeEvents().Any(e => e.Text == "Pip trained to level 2: Bags an order in 2 s"), Is.True);
            credits = shop.Credits;
            shop.OpenShop(); shop.Tick(shop.DaySecondsLeft + 1);
            Assert.That(credits - shop.Credits, Is.EqualTo(12), "level 2's wage");
            Assert.That(shop.Train("pip").Success, Is.True);
            Assert.That(shop.TrainCheck("pip", out string why), Is.EqualTo(GrowthFailure.MaxLevel));
            Assert.That(why, Is.EqualTo("Highest level"));
            Assert.That(shop.TrainCheck("nori", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(GrowthShop.LevelText("nori", 3), Does.Contain("three of one species"));
        }

        [Test]
        public void TheHelpersSaveAtVersionEightMidStepAndCarryOn()
        {
            var shop = At(3, 3);
            var large = Large(shop);
            shop.Annex = true; shop.Levels["generator"] = 2; shop.Levels["pump"] = 2;
            Hire(shop, "pip"); Hire(shop, "nori"); Hire(shop, "moss");
            shop.Discovered.Add("pondlet");
            shop.SetTarget("pondlet", 1);
            shop.SetMossRecipe("pondlet");
            shop.Hatchlings.Add(Make("pondlet"));
            var nori = shop.Helper("nori");
            shop.Tick(0.05f);
            Run(shop, nori.StepSeconds + 0.1f, 0.05f);
            Assert.That((nori.Step, nori.Hands.Count), Is.EqualTo(("deliver", 1)), "Nori is carrying it");
            var moss = shop.Helper("moss");
            while (moss.Dose == null) shop.Tick(0.05f);
            shop.SetDuty("pip", false);
            var save = shop.Snapshot();
            Assert.That(save.version, Is.EqualTo(8));
            string json = JsonUtility.ToJson(save);
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            string once = JsonUtility.ToJson(back.Snapshot());
            Assert.That(JsonUtility.ToJson(GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(once), out _).Snapshot()), Is.EqualTo(once), "the same save again (the test's creatures carry ids past the shop's counter, which a load moves on)");
            Assert.That(back.Snapshot().nextId, Is.GreaterThan(back.Helper("nori").Hands.Single().Id));
            Assert.That((back.Helper("nori").Hands.Single().Species, back.Helper("nori").Step, back.Helper("moss").Dose, back.MossBatch, back.Helper("pip").OnDuty),
                Is.EqualTo(("pondlet", "deliver", "bloom", true, false)));
            Assert.That((back.Helper("nori").Status, back.Helper("moss").Status), Is.EqualTo((nori.Status, moss.Status)), "each says what they are doing, mid-step too");
            Run(back, 30f);
            Assert.That((back.Shown("pondlet"), back.Helper("nori").Hands.Count), Is.EqualTo((1, 0)), "the trip carries on and ends in the tank");
            // A version 7 save (no helpers) loads.
            var seven = At(3, 3).Snapshot(); seven.version = 7; seven.staff = null;
            Assert.That(GrowthShop.Restore(seven, out problem), Is.Not.Null, problem);
            // Refused: a creature in a helper's hands that is also in a tank, a helper who is not one, Moss's batch without Moss.
            string Why(System.Action<GrowthSave> change) { var s = JsonUtility.FromJson<GrowthSave>(json); change(s); GrowthShop.Restore(s, out string p); return p; }
            Assert.That(Why(s => { var h = s.staff.First(x => x.id == "nori"); s.containers = s.containers.Concat(new[] { new GrowthSave.Box { id = "dup", kind = "starter", place = "a2", level = 1, occupants = new[] { h.hands[0] } } }).ToArray(); }), Is.EqualTo("A creature is saved twice"));
            Assert.That(Why(s => s.staff[0].id = "rex"), Is.EqualTo("A helper that cannot be"));
            Assert.That(Why(s => s.staff[0].level = 4), Is.EqualTo("A helper that cannot be"));
            Assert.That(Why(s => s.staff = s.staff.Where(h => h.id != "moss").ToArray()), Is.EqualTo("Moss's batch that cannot be"));
        }
    }
}
