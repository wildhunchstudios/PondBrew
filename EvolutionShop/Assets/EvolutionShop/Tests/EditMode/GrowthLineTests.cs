using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-08: GR.8's hatchery line (T3) and GR.9's analyzer and planner (T4) in the greybox: the workshop and its
    /// rack places, the hatchling chute laid in pipe mode, the sorter routing clutches by stock targets on the power board,
    /// the auto-doser by targets, a full day on the line and the era 5 gate, the analyzer's daily hint, the planner, and the
    /// version 7 save (GR.4, GR.8, GR.9, GR.15.1).</summary>
    public sealed class GrowthLineTests
    {
        static GrowthShop At(int era, int stars, int credits = 50000, int rp = 500) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = rp };

        static void UpgradeTo(GrowthShop shop, string station, int level)
        {
            while (shop.Level(station) < level) Assert.That(shop.Upgrade(station).Success, Is.True, station + " to level " + (shop.Level(station) + 1));
        }

        static void Lay(GrowthShop shop, string a, string b) => Assert.That(shop.LayPipe(a, b).Success, Is.True, a + " to " + b);

        /// <summary>An era 4 lab: the annex, growers at level 3, the bubble turbine, electric pumps, buffers 1 and 2, the workshop and the sorter.</summary>
        static GrowthShop Line(int stars = 4, int generator = 2, bool chute = true)
        {
            var shop = At(4, stars);
            Assert.That(shop.BuyAnnex().Success, Is.True, "annex");
            UpgradeTo(shop, "grower", 3);
            UpgradeTo(shop, "generator", generator);
            UpgradeTo(shop, "pump", 2);
            Assert.That(shop.BuyBuffer(0).Success && shop.BuyBuffer(1).Success, Is.True, "buffers");
            Assert.That(shop.BuyWorkshop().Success, Is.True, "workshop");
            UpgradeTo(shop, "sorter", 1);
            if (chute) Lay(shop, GrowthLab.Sorter, GrowthLab.Out);
            return shop;
        }

        static int nextSpecimen = 9000;
        static GrowthSpecimen Hatch(GrowthShop shop, string species)
        {
            var s = new GrowthSpecimen { Id = nextSpecimen++, Species = species };
            shop.Hatchlings.Add(s);
            return s;
        }

        static GrowthSpecimen Stock(GrowthShop shop, GrowthContainer c, string species, bool fromLine = false)
        {
            Assert.That(shop.DevelopmentStock(c.Id, species).Success, Is.True, species + " into " + c.Id);
            var s = c.Occupants.Last();
            s.FromLine = fromLine;
            return s;
        }

        /// <summary>A visitor who has chosen the specimen and is first in the queue; the counter bags it at once.</summary>
        static void Sell(GrowthShop shop, GrowthSpecimen s)
        {
            var v = new GrowthVisitor { Id = nextSpecimen++, Type = "casual", Budget = 500, State = GrowthVisitorState.Queued, Patience = 40 };
            Assert.That(shop.ReserveAll(v, new List<GrowthSpecimen> { s }), Is.True);
            shop.Visitors.Add(v);
            Assert.That(shop.Serve(5f).Detail, Is.EqualTo("sold"));
        }

        [Test]
        public void TheWorkshopOpensThroughTheAnnexAndHoldsTheLinesMachines()
        {
            var shop = At(4, 4);
            Assert.That(shop.WorkshopCheck(out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Open the lab annex first: the workshop is through its east wall"));
            shop.BuyAnnex();
            var early = At(3, 4); early.BuyAnnex();
            early.WorkshopCheck(out why); Assert.That(why, Is.EqualTo("Needs era 4: 4 stars, powered pumps running and 12 species"));
            var low = At(4, 3); low.BuyAnnex();
            low.WorkshopCheck(out why); Assert.That(why, Is.EqualTo("Needs 4 stars"));
            var poor = At(4, 4, credits: 5999); poor.BuyAnnex();
            poor.WorkshopCheck(out why); Assert.That(why, Is.EqualTo("Needs 5000 credits"));
            var day = At(4, 4); day.BuyAnnex(); day.OpenShop();
            Assert.That(day.WorkshopCheck(out _), Is.EqualTo(GrowthFailure.NotAtNight));
            // The sorter and the generator's twin turbine stand in the workshop.
            UpgradeTo(shop, "generator", 2);
            Assert.That(shop.UpgradeCheck("sorter", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Open the workshop first"));
            Assert.That(shop.UpgradeCheck("generator", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("The twin turbine stands in the workshop: open it first"));
            int credits = shop.Credits;
            Assert.That(shop.BuyWorkshop().Success, Is.True);
            Assert.That((shop.Workshop, credits - shop.Credits), Is.EqualTo((true, GrowthContent.Expansions.First(e => e.Id == "workshop").Credits)));
            Assert.That(shop.WorkshopCheck(out _), Is.EqualTo(GrowthFailure.MaxLevel));
            UpgradeTo(shop, "generator", 3);
            Assert.That(shop.GeneratorUnits, Is.EqualTo(6f), "the twin turbine: 6 units");
            Assert.That(shop.UpgradeCheck("generator", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs era 5"), "the tide engine in era 5");
            UpgradeTo(shop, "sorter", 1);
            Assert.That((shop.HasSorter, shop.SorterUnits, shop.PowerOrder.Last()), Is.EqualTo((true, 1f, GrowthShop.SorterPower)), "the sorter joins the power board");
            Assert.That(shop.PowerName(GrowthShop.SorterPower), Is.EqualTo("Sorter"));
            Assert.That(shop.SorterStatus, Is.EqualTo("Lay the chute from the incubator"));
        }

        [Test]
        public void RacksStandOnlyOnTheWorkshopsRackPlacesAndVisitorsNeverSeeThem()
        {
            var shop = At(4, 4);
            shop.BuyAnnex();
            Assert.That(shop.RackCheck("r1", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Open the workshop first"));
            shop.BuyWorkshop();
            Assert.That(shop.RackCheck("a2", out why), Is.EqualTo(GrowthFailure.NoSuchPlace), "a shop place is not a rack place");
            Assert.That(shop.BuyContainer("starter", "r1").Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "a rack place takes no shop tank");
            int credits = shop.Credits;
            Assert.That(shop.BuyRack("r1").Success, Is.True);
            Assert.That(credits - shop.Credits, Is.EqualTo(GrowthContent.Container("rack").Levels[0].Credits));
            Assert.That(shop.RackCheck("r1", out _), Is.EqualTo(GrowthFailure.PlaceTaken));
            var rack = shop.Racks.Single();
            Assert.That((rack.Place, rack.Def.Lab, shop.ShopContainers.Contains(rack)), Is.EqualTo(("r1", true, false)));
            Assert.That(shop.MoveCheck(rack.Id, "a2", out why), Is.EqualTo(GrowthFailure.NoSuchPlace));
            Assert.That(why, Is.EqualTo("Racks stay on their rack places in the workshop"));
            Assert.That(shop.MoveCheck(shop.ShopContainers.First().Id, "r2", out _), Is.EqualTo(GrowthFailure.NoSuchPlace), "a shop tank never moves onto a rack place");
            Stock(shop, rack, "pondlet");
            Assert.That(shop.Stock.Any(), Is.False, "a rack's creatures are not on sale");
            Assert.That(shop.UpgradeContainer(rack.Id).Success, Is.True, "racks go to their next level");
            Assert.That(rack.Spaces, Is.EqualTo(8));
            var day = At(4, 4); day.BuyAnnex(); day.BuyWorkshop(); day.OpenShop();
            Assert.That(day.RackCheck("r2", out _), Is.EqualTo(GrowthFailure.NotAtNight));
        }

        [Test]
        public void TheChuteRunsFromTheIncubatorsOutletOverTheAnnexToTheSorter()
        {
            Assert.That(GrowthLab.InLab((20, 19), annex: true), Is.False, "the workshop's cells wait for it");
            Assert.That((GrowthLab.InLab((20, 19), true, true), GrowthLab.InLab((16, 5), true, true), GrowthLab.InLab((16, 22), true, true)), Is.EqualTo((true, false, false)),
                "the workshop lies east of the annex, as deep as its first 6 m");
            var shop = Line(chute: false);
            var bare = At(4, 4); bare.BuyAnnex(); UpgradeTo(bare, "generator", 2); UpgradeTo(bare, "pump", 2);
            Assert.That(bare.PipeCheck(GrowthLab.Out, GrowthLab.Sorter, out _, out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Open the workshop first: the sorter stands there"));
            bare.BuyWorkshop();
            bare.PipeCheck(GrowthLab.Out, GrowthLab.Sorter, out _, out why);
            Assert.That(why, Is.EqualTo("Buy the sorter in the workshop first"));
            Assert.That(shop.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Sorter, out _, out _), Is.EqualTo(GrowthFailure.NoSuchPlace), "only the incubator's outlet feeds the sorter");
            Assert.That(shop.PipeCheck(GrowthLab.Out, GrowthLab.Buffer(0), out _, out _), Is.EqualTo(GrowthFailure.NoSuchPlace));
            Assert.That(shop.PipeCheck(GrowthLab.Sorter, GrowthLab.Out, out var plan, out _), Is.EqualTo(GrowthFailure.None), "picked either way round");
            Assert.That((plan.From, plan.To, plan.Chute, plan.Line), Is.EqualTo((GrowthLab.Out, GrowthLab.Sorter, true, false)));
            int credits = shop.Credits;
            Lay(shop, GrowthLab.Out, GrowthLab.Sorter);
            var chute = shop.Chute;
            Assert.That(chute, Is.Not.Null);
            Assert.That(credits - shop.Credits, Is.EqualTo(chute.Paid));
            Assert.That(chute.Paid, Is.EqualTo((int)System.Math.Ceiling(chute.Length * GrowthContent.PipeCreditsPerMetre - 1e-4f)), "paid by its length");
            Assert.That((chute.Cells.First(), chute.Cells.Last()), Is.EqualTo((GrowthLab.EndCell(GrowthLab.Out), GrowthLab.EndCell(GrowthLab.Sorter))));
            for (int i = 0; i < chute.Cells.Count; i++)
            {
                Assert.That(GrowthLab.InLab(chute.Cells[i], true, true), Is.True);
                if (i > 0) Assert.That(GrowthLab.Adjacent(chute.Cells[i - 1], chute.Cells[i]), Is.True);
            }
            Assert.That(chute.Cells.Any(c => c.r >= GrowthLab.AnnexFirstRow && c.c < GrowthLab.AnnexColumns) && chute.Cells.Any(c => c.c >= GrowthLab.AnnexColumns), Is.True, "over the annex into the workshop");
            Assert.That(chute.Length, Is.EqualTo((chute.Cells.Count - 1) * 0.5f + (GrowthLab.PipeHeight - 0.95f) + (GrowthLab.PipeHeight - 1.6f)).Within(0.001f));
            Assert.That(shop.PowerOrder.Contains(chute.PowerId), Is.False, "the chute needs no pump: clutches slide");
            Assert.That(shop.PipeCheck(GrowthLab.Out, GrowthLab.Sorter, out _, out why), Is.EqualTo(GrowthFailure.Busy));
            Assert.That(why, Is.EqualTo("The chute is laid already"));
            Assert.That(shop.EndName(GrowthLab.Out) + " to " + shop.EndName(GrowthLab.Sorter), Is.EqualTo("the incubator's outlet to the sorter"));
            // Lines laid after the chute route around it, and it comes down at night for half back.
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Assert.That(GrowthLab.CanJoin(shop.Pipes.Where(p => !p.Chute).Select(p => p.Cells), chute.Cells), Is.True);
            credits = shop.Credits;
            Assert.That(shop.RemovePipe(chute.Id).Success, Is.True);
            Assert.That((shop.Chute, shop.Credits - credits), Is.EqualTo(((GrowthPipe)null, chute.Paid / 2)));
        }

        [Test]
        public void TheSorterStocksTheShopToItsTargetsAndRacksTheRest()
        {
            var shop = Line();
            var tank = shop.ShopContainers.Single();
            Assert.That(shop.SetTarget("pondlet", 2).Failure, Is.EqualTo(GrowthFailure.NotDiscovered), "targets only for creatures made");
            shop.Discovered.Add("pondlet");
            Assert.That(shop.SetTarget("pondlet", GrowthShop.MostTarget + 1).Success, Is.False);
            Assert.That(shop.SetTarget("pondlet", 2).Success, Is.True);
            Assert.That(shop.Target("pondlet"), Is.EqualTo(2));
            Assert.That(shop.SorterStatus, Is.EqualTo("Waiting for a clutch"));
            for (int i = 0; i < 3; i++) Hatch(shop, "pondlet");
            Assert.That(shop.SorterStatus, Is.EqualTo("Sorting a Pondlet"), "the first two have their tank");
            shop.Tick(1.01f);
            Assert.That((tank.Occupants.Count, shop.Hatchlings.Count, tank.Occupants.Single().FromLine), Is.EqualTo((1, 2, true)), "one a second, into the shop's tank, stocked by the line");
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Sorted && e.Text == "The sorter put a Pondlet in the starter tank at a1"), Is.True);
            shop.Tick(1f);
            Assert.That((tank.Occupants.Count, shop.Shown("pondlet")), Is.EqualTo((2, 2)));
            shop.Tick(1f);
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(1), "GR.15.1: the target is met and no rack has room, so it waits on the tray");
            Assert.That(shop.SorterStatus, Is.EqualTo("No room for a Pondlet: its tanks are at their target or full, and no rack has room"));
            Assert.That(shop.PowerUse(GrowthShop.SorterPower).Draw, Is.EqualTo(0f), "a sorter with nowhere to sort draws nothing");
            Assert.That(shop.BuyRack("r1").Success, Is.True);
            shop.Tick(1.01f);
            var rack = shop.Racks.Single();
            Assert.That((rack.Occupants.Count, rack.Occupants.Single().FromLine, shop.Hatchlings.Count), Is.EqualTo((1, false, 0)), "the rest go to the racks");
            // The tank holding most of the species comes first, so schools form; a raised target sends the next one there.
            Assert.That(shop.BuyContainer("starter", "a2").Success, Is.True);
            var second = shop.ContainerAt("a2");
            for (int i = 0; i < 3; i++) Stock(shop, second, "pondlet");
            shop.SetTarget("pondlet", 6);
            Hatch(shop, "pondlet");
            shop.Tick(1.01f);
            Assert.That((second.Occupants.Count, tank.Occupants.Count), Is.EqualTo((4, 2)));
            // A creature too big for a tank goes to a rack, which takes Large ones.
            Assert.That(shop.UpgradeContainer(rack.Id).Success, Is.True, "8 spaces");
            shop.Discovered.Add("shellback"); shop.SetTarget("shellback", 1);
            Hatch(shop, "shellback");
            shop.Tick(1.01f);
            Assert.That(rack.Occupants.Any(o => o.Species == "shellback"), Is.True, "no shop tank fits a Large creature, so a rack takes it");
        }

        [Test]
        public void TheSorterRunsOnThePowerBoardAndWaitsForPowerSayingSo()
        {
            var shop = Line(generator: 1);
            shop.Discovered.Add("pondlet");
            shop.SetTarget("pondlet", 3);
            Hatch(shop, "pondlet");
            shop.Tick(2f);
            Assert.That((shop.Hatchlings.Count, shop.SorterStatus), Is.EqualTo((1, "Waiting for power")), "the hand crank is not turning");
            for (int i = 0; i < 5; i++) { shop.CrankGenerator(); shop.Tick(0.25f); }
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(0), "cranked, it sorts");
            // Behind a pump that takes the whole supply, it is slowed or waits, as the board's order says.
            var busy = Line();
            busy.Discovered.Add("pondlet"); busy.SetTarget("pondlet", 3);
            UpgradeTo(busy, "pump", 3);
            Lay(busy, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Lay(busy, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            Assert.That(busy.PowerOrder.First(), Is.EqualTo(GrowthShop.SorterPower), "bought first, served first");
            Assert.That(busy.MovePower(GrowthShop.SorterPower, 2).Success, Is.True);
            Hatch(busy, "pondlet");
            busy.Tick(0.5f);
            var use = busy.PowerUse(GrowthShop.SorterPower);
            Assert.That((use.Draw, use.Got, busy.SorterStatus, busy.Hatchlings.Count), Is.EqualTo((1f, 0f, "Waiting for power", 1)), "two fast pumps (2 units each) take the bubble turbine's 3 first");
            Assert.That(busy.MovePower(GrowthShop.SorterPower, -2).Success, Is.True);
            busy.Tick(0.5f);
            use = busy.PowerUse(GrowthShop.SorterPower);
            Assert.That((use.Draw, use.Got), Is.EqualTo((1f, 1f)), "first on the board, it is served first");
        }

        [Test]
        public void TheAutoDoserCanFollowTheStockTargets()
        {
            var shop = Line(chute: false);
            UpgradeTo(shop, "incubator", 4);
            shop.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin" });
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0)); Lay(shop, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            Lay(shop, GrowthLab.Buffer(0), GrowthLab.Port); Lay(shop, GrowthLab.Buffer(1), GrowthLab.Port);
            shop.GrowerUnits["bloom"] = shop.GrowerUnits["flow"] = 0;
            shop.BufferAt(0).Units = shop.BufferAt(1).Units = GrowthContent.BufferCapacity;
            Assert.That(shop.SetAutoByTargets().Success, Is.True);
            Assert.That(shop.SwitchAutoDoser(true).Success, Is.True, "by targets needs no recipe");
            Assert.That(shop.AutoStatus, Is.EqualTo("Every stock target the port can make is met"));
            shop.SetTarget("pondlet", 2); shop.SetTarget("ribbonfin", 5);
            shop.SetTarget("lanternfin", 12);
            Assert.That(shop.TargetRecipe(), Is.EqualTo("ribbonfin"), "the furthest below its target that the port can make (no Spark is piped for the Lanternfin)");
            Assert.That(shop.BuyRack("r1").Success, Is.True);
            for (int i = 0; i < 3; i++) Stock(shop, shop.Racks.Single(), "ribbonfin");
            Assert.That(shop.TargetRecipe(), Is.EqualTo("ribbonfin"), "creatures in racks do not count: the targets are the shop's tanks");
            for (int i = 0; i < 10; i++) shop.Tick(0.25f);
            Assert.That((shop.Incubating, shop.Pending, shop.AutoRecipe), Is.EqualTo((true, "ribbonfin", "ribbonfin")));
            shop.Tick(shop.IncubationSeconds + 0.1f);
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(3));
            Assert.That(shop.AutoStatus, Is.EqualTo("Waiting: take the clutch out of the incubator"), "no chute: the clutch waits for hands");
            Assert.That(shop.TargetRecipe(), Is.EqualTo("pondlet"), "the clutch on the tray counts: Ribbonfins 2 short, Pondlets 2 short, the first in the catalog first");
            // With the chute and sorter, the tray clears and the next batch follows.
            Lay(shop, GrowthLab.Out, GrowthLab.Sorter);
            Assert.That(shop.AutoStatus, Is.EqualTo("Waiting for the sorter to clear the tray"));
            for (int i = 0; i < 24; i++) shop.Tick(0.25f);
            Assert.That((shop.Shown("ribbonfin"), shop.Hatchlings.Count), Is.EqualTo((3, 0)));
            Assert.That((shop.Incubating, shop.Pending), Is.EqualTo((true, "pondlet")));
            // Choosing one recipe goes back to one recipe.
            shop.Tick(shop.IncubationSeconds + 5f);
            Assert.That(shop.SetAutoRecipe("bubblebelly").Success, Is.True);
            Assert.That(shop.AutoByTargets, Is.False);
            Assert.That(shop.SetAutoByTargets().Success, Is.True);
            Assert.That(shop.SetAutoByTargets().Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            Assert.That(At(4, 4).SetAutoByTargets().Failure, Is.EqualTo(GrowthFailure.Locked), "the auto-doser comes with the incubator's level 4");
        }

        static GrowthShop LineDayShop(bool large = true)
        {
            var shop = Line(stars: 5);
            UpgradeTo(shop, "incubator", 4);
            shop.Discovered.Add("pondlet");
            if (large) shop.Discovered.Add("shellback");
            Assert.That(shop.SetAutoRecipe("pondlet").Success && shop.SwitchAutoDoser(true).Success, Is.True);
            Assert.That(shop.LineRunning, Is.True);
            return shop;
        }

        static void CloseDay(GrowthShop shop) { shop.Tick(shop.DaySecondsLeft + 1f); Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night)); }

        [Test]
        public void AFullDayOnTheLineWithALargeCreatureAndFiveStarsOpensEraFive()
        {
            var shop = LineDayShop();
            Assert.That(shop.Objectives().Select(l => l.text), Has.Some.StartsWith("Run a full day on the line"));
            Assert.That(shop.OpenShop().Success, Is.True);
            Assert.That(shop.LineToday, Is.True, "the line runs from opening");
            var tank = shop.ShopContainers.Single();
            Sell(shop, Stock(shop, tank, "pondlet"));
            Assert.That(shop.LineSalesToday, Is.EqualTo(0), "a creature the player stocked does not count");
            var line = Enumerable.Range(0, 4).Select(_ => Stock(shop, tank, "pondlet", fromLine: true)).ToList();
            foreach (var s in line.Take(3)) Sell(shop, s);
            Assert.That(shop.Objectives().Single(l => l.text.StartsWith("Run a full day")).text, Does.Contain("today 3/5"));
            Sell(shop, line[3]);
            var more = Stock(shop, tank, "pondlet", fromLine: true);
            Sell(shop, more);
            Assert.That(shop.LineSalesToday, Is.EqualTo(GrowthShop.LineDaySales));
            Assert.That(shop.Era, Is.EqualTo(4), "era 5 waits for the day's end");
            CloseDay(shop);
            Assert.That(shop.LineDays, Is.EqualTo(1));
            var events = shop.TakeEvents();
            Assert.That(events.Any(e => e.Kind == GrowthEventKind.LineDay), Is.True);
            Assert.That(events.Any(e => e.Kind == GrowthEventKind.EraUp && e.Text.StartsWith("Era 5")), Is.True);
            Assert.That((shop.Era, shop.EraFiveGateMet), Is.EqualTo((5, true)));
            Assert.That(shop.Objectives().Last().text, Is.EqualTo("The greybox ends here: the Grand Exhibition comes with GR-11"));
        }

        [Test]
        public void ADayCountsOnlyWhenTheLineRanAllDayAndSoldEnoughOfWhatItStocked()
        {
            // Four sales of what the line stocked are not a full day.
            var few = LineDayShop();
            few.OpenShop();
            var tank = few.ShopContainers.Single();
            for (int i = 0; i < GrowthShop.LineDaySales - 1; i++) Sell(few, Stock(few, tank, "pondlet", fromLine: true));
            CloseDay(few);
            Assert.That((few.LineDays, few.Era), Is.EqualTo((0, 4)));
            // The auto-doser switched off for a moment: today no longer counts, even with five sales.
            var stopped = LineDayShop();
            stopped.OpenShop();
            stopped.SwitchAutoDoser(false); stopped.Tick(0.1f); stopped.SwitchAutoDoser(true);
            Assert.That(stopped.LineToday, Is.False);
            Assert.That(stopped.Objectives().Single(l => l.text.StartsWith("Run a full day")).text, Does.Contain("not today: it stopped"));
            tank = stopped.ShopContainers.Single();
            for (int i = 0; i < GrowthShop.LineDaySales; i++) Sell(stopped, Stock(stopped, tank, "pondlet", fromLine: true));
            CloseDay(stopped);
            Assert.That(stopped.LineDays, Is.EqualTo(0));
            // Not running when the shop opens (no chute): the day does not count.
            var late = LineDayShop();
            late.RemovePipe(late.Chute.Id);
            late.OpenShop();
            Assert.That(late.LineToday, Is.False);
            // A full day without a large creature: era 5 waits for one.
            var small = LineDayShop(large: false);
            small.OpenShop();
            tank = small.ShopContainers.Single();
            for (int i = 0; i < GrowthShop.LineDaySales; i++) Sell(small, Stock(small, tank, "pondlet", fromLine: true));
            CloseDay(small);
            Assert.That((small.LineDays, small.Era, small.EraFiveGateMet), Is.EqualTo((1, 4, false)));
        }

        [Test]
        public void TheAnalyzerNamesOneNewThingWithinReachEachDayWithoutNamingIt()
        {
            var shop = At(4, 4);
            shop.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            Assert.That((shop.Analyzer, shop.Hint, shop.HintText), Is.EqualTo((false, (GrowthHint)null, (string)null)));
            UpgradeTo(shop, "bench", 3);
            Assert.That(shop.Analyzer, Is.True);
            Assert.That(shop.HintText, Is.EqualTo("Nothing new within reach of your cultures and the breeding tank"), "Bloom and Flow make nothing new, and there is no breeding tank");
            UpgradeTo(shop, "breeding", 1);
            Assert.That(shop.Hint, Is.Null, "one hint a day: the next when the shop opens");
            shop.OpenShop();
            Assert.That((shop.Hint.Kind, shop.Hint.A, shop.Hint.B, shop.Hint.Species), Is.EqualTo(("pair", "pondlet", "bubblebelly", "puddlepup")));
            Assert.That(shop.HintText, Is.EqualTo("Try pairing a Pondlet with a Bubblebelly in the breeding tank: something new"));
            Assert.That(shop.HintText, Does.Not.Contain("Puddlepup"), "never the creature's name");
            CloseDay(shop);
            Assert.That(shop.Research("spark").Success, Is.True);
            shop.OpenShop();
            Assert.That(shop.Hint.Kind, Is.EqualTo("mix"), "a mix within reach comes first");
            Assert.That(shop.Hint.Recipe, Is.EqualTo(GrowthContent.Get("lanternfin").Recipe), "the fewest doses first, then the catalog's order");
            Assert.That(shop.HintText, Is.EqualTo("Try a mix of 1 Bloom, 1 Flow and 1 Spark: something new"));
            shop.Discovered.Add("lanternfin");
            Assert.That(shop.HintText, Is.EqualTo("Today's hint is found; a new one when the shop opens"));
            CloseDay(shop);
            shop.OpenShop();
            Assert.That(shop.Hint.Species, Is.EqualTo("sparkminnow"));
            // A mix the incubator cannot take yet is not within reach.
            var small = At(4, 4); small.Levels["bench"] = 3;
            foreach (var c in new[] { "ember", "mist" }) { small.Cultures.Add(c); small.GrowerUnits[c] = 0; }
            small.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder && x.Id != "puffglider").Select(x => x.Id));
            small.OpenShop();
            Assert.That(small.Hint, Is.Null, "the incubator's level 1 takes 3 doses; the Puffglider needs 4");
            CloseDay(small);
            small.Levels["incubator"] = 2;
            small.OpenShop();
            Assert.That(small.Hint.Species, Is.EqualTo("puffglider"));
        }

        [Test]
        public void ThePlannerPredictsWhatAnyTwoKnownSpeciesBreed()
        {
            var shop = At(5, 5);
            shop.Discovered.AddRange(new[] { "pondlet", "bubblebelly" });
            UpgradeTo(shop, "bench", 3);
            Assert.That(shop.PlannerPredicts("pondlet", "bubblebelly"), Is.Null, "the planner is the bench's level 4");
            var four = At(4, 4); UpgradeTo(four, "bench", 3);
            four.UpgradeCheck("bench", out _, out string why);
            Assert.That(why, Is.EqualTo("Needs era 5"));
            UpgradeTo(shop, "bench", 4);
            Assert.That(shop.Planner, Is.True);
            Assert.That(shop.PlannerPredicts("pondlet", "bubblebelly").Id, Is.EqualTo("puddlepup"));
            Assert.That(shop.PlannerPredicts("bubblebelly", "pondlet").Id, Is.EqualTo("puddlepup"), "either way round");
            Assert.That(shop.PlannerPredicts("pondlet", "pondlet").Id, Is.EqualTo("pondlet"));
            Assert.That(shop.PlannerPredicts("pondlet", "lilyfin"), Is.Null, "only species you know");
        }

        [Test]
        public void TheLineSavesAtVersionSevenAndRefusesWhatCannotBe()
        {
            var shop = Line();
            UpgradeTo(shop, "incubator", 4);
            UpgradeTo(shop, "breeding", 1);
            shop.Discovered.AddRange(new[] { "pondlet", "bubblebelly" });
            UpgradeTo(shop, "bench", 3);
            Assert.That(shop.BuyRack("r2").Success, Is.True);
            var rack = shop.Racks.Single();
            Stock(shop, rack, "bubblebelly");
            shop.SetTarget("pondlet", 3);
            shop.SetAutoByTargets(); shop.SwitchAutoDoser(true);
            shop.OpenShop();
            var stocked = Stock(shop, shop.ShopContainers.Single(), "pondlet", fromLine: true);
            shop.LineSalesToday = 2;
            shop.SortProgress = 0.4f;
            var save = shop.Snapshot();
            Assert.That(save.version, Is.GreaterThanOrEqualTo(7));
            string json = JsonUtility.ToJson(save);
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That((back.Workshop, back.HasSorter, back.Racks.Single().Place, back.Racks.Single().Occupants.Single().Species), Is.EqualTo((true, true, "r2", "bubblebelly")));
            Assert.That(back.Chute.Cells, Is.EqualTo(shop.Chute.Cells));
            Assert.That((back.Target("pondlet"), back.AutoByTargets, back.AutoOn, back.LineToday, back.LineSalesToday, back.SortProgress), Is.EqualTo((3, true, true, true, 2, 0.4f)));
            Assert.That(back.HolderOf(stocked.Id).Occupants.Single(o => o.Id == stocked.Id).FromLine, Is.True);
            Assert.That((back.Hint.Kind, back.Hint.A, back.Hint.B, back.Hint.Species), Is.EqualTo((shop.Hint.Kind, shop.Hint.A, shop.Hint.B, shop.Hint.Species)));
            Assert.That(back.PowerOrder, Is.EqualTo(shop.PowerOrder));
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json), "the same save again");
            // A version 6 save (no workshop) loads.
            var oldShop = Line(chute: false);
            var six = oldShop.Snapshot();
            six.version = 6; six.workshop = false;
            int sorterAt = System.Array.IndexOf(six.stations, "sorter");
            six.stations = six.stations.Where((_, i) => i != sorterAt).ToArray(); six.levels = six.levels.Where((_, i) => i != sorterAt).ToArray();
            six.powerOrder = six.powerOrder.Where(id => id != GrowthShop.SorterPower).ToArray();
            Assert.That(GrowthShop.Restore(six, out problem), Is.Not.Null, problem);
            // Refused: a rack on a shop place, the workshop gone under the chute, a target that cannot be, a hint that cannot be.
            GrowthSave Edit(System.Action<GrowthSave> change) { var s = JsonUtility.FromJson<GrowthSave>(json); change(s); return s; }
            string Why(System.Action<GrowthSave> change) { GrowthShop.Restore(Edit(change), out string p); return p; }
            Assert.That(Why(s => s.containers.First(b => b.kind == "rack").place = "a3"), Is.EqualTo("A rack outside the workshop's rack places"));
            Assert.That(Why(s => s.workshop = false), Is.EqualTo("A pipe outside the lab"));
            Assert.That(Why(s => s.targetSpecies = new[] { "nothing" }), Is.EqualTo("A stock target that cannot be"));
            Assert.That(Why(s => s.targetCounts = new[] { GrowthShop.MostTarget + 1 }), Is.EqualTo("A stock target that cannot be"));
            Assert.That(Why(s => { s.hintKind = "mix"; s.hintRecipe = null; }), Is.EqualTo("An analyzer hint that cannot be"));
            Assert.That(Why(s => s.levels[System.Array.IndexOf(s.stations, "bench")] = 2), Is.EqualTo("An analyzer hint that cannot be"));
            Assert.That(Why(s => { s.levels[System.Array.IndexOf(s.stations, "incubator")] = 3; s.autoOn = false; s.autoBatch = false; s.pipes = s.pipes.Where(p => p.to != GrowthLab.Port).ToArray(); }),
                Is.EqualTo("An auto-doser by targets without the auto-doser"));
        }
    }
}
