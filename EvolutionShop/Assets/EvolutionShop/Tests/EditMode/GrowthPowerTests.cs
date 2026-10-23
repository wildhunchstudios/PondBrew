using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-07: GR.8's power and pipes in the greybox: the lab annex, the generator and pumps, buffers, pipes laid
    /// between two ends along the lab grid, the hand pump, electric pumps on the power budget in priority order, the
    /// auto-doser, the era 4 gate and the version 6 save (GR.8, GR.9, GR.15.1).</summary>
    public sealed class GrowthPowerTests
    {
        static GrowthShop At(int era, int stars, int credits = 50000, int rp = 500) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = rp };

        static void UpgradeTo(GrowthShop shop, string station, int level)
        {
            while (shop.Level(station) < level) Assert.That(shop.Upgrade(station).Success, Is.True, station + " to level " + (shop.Level(station) + 1));
        }

        /// <summary>An era 3 lab: the annex open, growers at level 3 (one tap each), the hand pump, buffers 1 and 2.</summary>
        static GrowthShop Lab(int era = 3, int stars = 3, int pumps = 1)
        {
            var shop = At(era, stars);
            Assert.That(shop.BuyAnnex().Success, Is.True, "annex");
            UpgradeTo(shop, "grower", 3);
            UpgradeTo(shop, "generator", 1);
            UpgradeTo(shop, "pump", pumps);
            Assert.That(shop.BuyBuffer(0).Success && shop.BuyBuffer(1).Success, Is.True, "buffers");
            return shop;
        }

        static void Lay(GrowthShop shop, string a, string b) => Assert.That(shop.LayPipe(a, b).Success, Is.True, a + " to " + b);

        static void AssertRoute(GrowthPipe p, bool annex)
        {
            Assert.That(p.Cells.First(), Is.EqualTo(GrowthLab.EndCell(p.From)));
            Assert.That(p.Cells.Last(), Is.EqualTo(GrowthLab.EndCell(p.To)));
            for (int i = 0; i < p.Cells.Count; i++)
            {
                Assert.That(GrowthLab.InLab(p.Cells[i], annex), Is.True, "every cell under the lab's ceiling");
                if (i > 0) Assert.That(GrowthLab.Adjacent(p.Cells[i - 1], p.Cells[i]), Is.True, "a pipe runs cell to cell");
            }
            Assert.That(p.Length, Is.EqualTo((p.Cells.Count - 1) * 0.5f + (GrowthLab.PipeHeight - GrowthLab.Height(p.From)) + (GrowthLab.PipeHeight - GrowthLab.Height(p.To))).Within(0.001f));
            Assert.That(p.Paid, Is.EqualTo((int)Math.Ceiling(p.Length * GrowthContent.PipeCreditsPerMetre - 1e-4f)));
        }

        [Test]
        public void TheLabAnnexOpensAtNightWithThreeStarsInEraThree()
        {
            Assert.That(At(2, 3).AnnexCheck(out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Does.StartWith("Needs era 3"));
            At(3, 2).AnnexCheck(out why); Assert.That(why, Is.EqualTo("Needs 3 stars"));
            var day = At(3, 3); day.OpenShop();
            Assert.That(day.AnnexCheck(out why), Is.EqualTo(GrowthFailure.NotAtNight));
            Assert.That(At(3, 3, credits: GrowthShop.AnnexDef.Credits - 1).AnnexCheck(out why), Is.EqualTo(GrowthFailure.NotEnoughCredits));
            var shop = At(3, 3, credits: 1500);
            Assert.That(shop.Objectives().Any(o => o.text.StartsWith("Open the lab annex") && !o.done), Is.True, "era 3's goal names the annex");
            Assert.That(shop.BuyAnnex().Success, Is.True);
            Assert.That((shop.Annex, shop.Credits), Is.EqualTo((true, 500)));
            Assert.That(shop.BuyAnnex().Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            Assert.That(shop.Objectives().Any(o => o.text.StartsWith("Open the lab annex") && o.done), Is.True);
            // Before the annex, the generator, pumps, buffers and pipes say why not.
            var closed = At(3, 3);
            closed.UpgradeCheck("generator", out _, out why); Assert.That(why, Is.EqualTo("Open the lab annex first"));
            closed.BufferCheck(0, out why); Assert.That(why, Is.EqualTo("Open the lab annex first"));
            closed.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(0), out _, out why); Assert.That(why, Is.EqualTo("Open the lab annex first"));
        }

        [Test]
        public void TheGeneratorAndPumpsAreBoughtInTheAnnexAndTheirLevelsWaitWhereTheyShould()
        {
            var shop = At(3, 3, rp: 29);
            shop.BuyAnnex();
            Assert.That(shop.UpgradeCheck("generator", out var next, out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That((next.Level, why), Is.EqualTo((1, "Needs 30 research points")));
            shop.Rp = 30;
            shop.UpgradeCheck("pump", out _, out why);
            Assert.That(shop.Upgrade("pump").Success, Is.True, "the hand pump needs no generator");
            Assert.That(shop.UpgradeCheck("pump", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Electric pumps need the generator"));
            Assert.That(shop.Upgrade("generator").Success, Is.True);
            Assert.That((shop.HasGenerator, shop.HandCrank, shop.GeneratorUnits, shop.Rp), Is.EqualTo((true, true, 1f, 0)));
            Assert.That(shop.Upgrade("pump").Success, Is.True, "electric pumps with the hand crank");
            Assert.That((shop.ElectricPumps, shop.PumpUnits, shop.PumpDoseSeconds), Is.EqualTo((true, 1, 1.5f)));
            Assert.That(shop.Upgrade("generator").Success, Is.True);
            Assert.That((shop.HandCrank, shop.GeneratorUnits, shop.SupplyNow), Is.EqualTo((false, 3f, 3f)), "the bubble turbine runs by itself");
            Assert.That(shop.CrankGenerator().Failure, Is.EqualTo(GrowthFailure.Busy));
            Assert.That(shop.UpgradeCheck("generator", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Waits for the workshop (GR-10)"), "turbines past the bubble turbine stand in the workshop");
            Assert.That(shop.UpgradeCheck("pump", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs era 4"), "fast pumps in era 4");
            // Buffers come with pumps and are placed at night.
            var bare = At(3, 3); bare.BuyAnnex();
            bare.BufferCheck(0, out why); Assert.That(why, Is.EqualTo("Buffers come with pumps and pipes (on the power board)"));
            Assert.That(shop.BuyBuffer(6).Failure, Is.EqualTo(GrowthFailure.NoSuchPlace));
            int credits = shop.Credits;
            Assert.That(shop.BuyBuffer(2).Success, Is.True);
            Assert.That(credits - shop.Credits, Is.EqualTo(GrowthContent.BufferCredits));
            Assert.That(shop.BuyBuffer(2).Failure, Is.EqualTo(GrowthFailure.PlaceTaken));
            shop.OpenShop();
            shop.BufferCheck(3, out why); Assert.That(why, Is.EqualTo("Buffers are placed at night"));
        }

        [Test]
        public void PipesRunFromATapToABufferAndFromABufferToThePortAlongTheGrid()
        {
            var early = At(3, 3); early.BuyAnnex(); UpgradeTo(early, "grower", 2); UpgradeTo(early, "pump", 1); early.BuyBuffer(0);
            early.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(0), out _, out string why);
            Assert.That(why, Is.EqualTo("A grower's tap comes with growers' level 3"));
            var shop = Lab();
            shop.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Tap("flow"), out _, out why);
            Assert.That(why, Does.StartWith("A pipe runs from a grower's tap to a buffer"));
            shop.PipeCheck(GrowthLab.Tap("ember"), GrowthLab.Buffer(0), out _, out why); Assert.That(why, Is.EqualTo("You don't grow Ember yet"));
            shop.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(3), out _, out why); Assert.That(why, Is.EqualTo("Place a buffer there first"));
            // Picked either way round, a pipe runs from the tap to the buffer, cell to cell overhead, paid by its length.
            int credits = shop.Credits;
            Assert.That(shop.PipeCheck(GrowthLab.Buffer(0), GrowthLab.Tap("bloom"), out var plan, out _), Is.EqualTo(GrowthFailure.None));
            Lay(shop, GrowthLab.Buffer(0), GrowthLab.Tap("bloom"));
            var bloom = shop.Pipes.Single();
            Assert.That((bloom.From, bloom.To, bloom.Line, bloom.Culture), Is.EqualTo((GrowthLab.Tap("bloom"), GrowthLab.Buffer(0), true, "bloom")));
            Assert.That(bloom.Cells, Is.EqualTo(plan.Cells), "the preview is the pipe laid");
            AssertRoute(bloom, true);
            Assert.That(credits - shop.Credits, Is.EqualTo(bloom.Paid));
            Assert.That(shop.BufferAt(0).Culture, Is.EqualTo("bloom"), "the buffer takes its line's culture");
            Assert.That(shop.PowerOrder, Is.EqualTo(new[] { bloom.PowerId }));
            shop.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(1), out _, out why); Assert.That(why, Is.EqualTo("The Bloom tap is in use"), "a level 3 grower has one tap");
            shop.PipeCheck(GrowthLab.Tap("flow"), GrowthLab.Buffer(0), out _, out why); Assert.That(why, Is.EqualTo("Buffer 1 has its pipe from Bloom"));
            Lay(shop, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            var flow = shop.Pipes.Last();
            AssertRoute(flow, true);
            Assert.That(GrowthLab.CanJoin(new IList<(int c, int r)>[] { bloom.Cells }, flow.Cells), Is.True, "pipes run around each other or cross at right angles");
            // The port comes with the incubator's level 4; a buffer reaches it once its line brings culture.
            shop.PipeCheck(GrowthLab.Buffer(0), GrowthLab.Port, out _, out why); Assert.That(why, Is.EqualTo("The incubator's port comes with its level 4"));
            var four = Lab(4, 4); UpgradeTo(four, "incubator", 4);
            four.PipeCheck(GrowthLab.Port, GrowthLab.Buffer(0), out _, out why); Assert.That(why, Is.EqualTo("Pipe a grower into buffer 1 first"));
            Lay(four, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Lay(four, GrowthLab.Port, GrowthLab.Buffer(0));
            var port = four.Pipes.Last();
            Assert.That((port.From, port.To, port.Line), Is.EqualTo((GrowthLab.Buffer(0), GrowthLab.Port, false)));
            AssertRoute(port, true);
            four.PipeCheck(GrowthLab.Buffer(0), GrowthLab.Port, out _, out why); Assert.That(why, Is.EqualTo("Buffer 1 is piped to the port already"));
            // Growers' level 4 feeds two pipes: a second Bloom buffer.
            UpgradeTo(four, "grower", 4);
            Lay(four, GrowthLab.Tap("bloom"), GrowthLab.Buffer(1));
            four.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(1), out _, out why); Assert.That(why, Does.StartWith("Both of the Bloom grower's taps are in use").Or.EqualTo("Buffer 2 has its pipe from Bloom"));
            var day = Lab(); day.OpenShop();
            day.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(0), out _, out why); Assert.That(why, Is.EqualTo("Pipes are laid at night"));
            // Laid and taken down at night; half the cost comes back, the culture stays where it is.
            shop.OpenShop();
            shop.PipeCheck(GrowthLab.Tap("bloom"), GrowthLab.Buffer(1), out _, out why); Assert.That(why, Is.Not.EqualTo(null));
            Assert.That(shop.RemovePipe(flow.Id).Failure, Is.EqualTo(GrowthFailure.NotAtNight));
            shop.Tick(GrowthContent.DaySeconds + 1);
            credits = shop.Credits;
            Assert.That(shop.RemovePipe(flow.Id).Success, Is.True);
            Assert.That(shop.Credits - credits, Is.EqualTo(flow.Paid / 2));
            Assert.That((shop.Pipes.Count, shop.PowerOrder.Count, shop.BufferAt(1).Culture), Is.EqualTo((1, 1, (string)null)), "an empty buffer is free for another culture");
            Assert.That(shop.SellBufferCheck(0, out why), Is.EqualTo(GrowthFailure.NotEmpty));
            Assert.That(why, Is.EqualTo("Take its pipes down first"));
            Assert.That(shop.SellBuffer(1).Success, Is.True);
        }

        [Test]
        public void TheGridRoutesStraightRunsAroundWhatIsInTheWay()
        {
            var straight = GrowthLab.Route((0, 0), (3, 3), _ => true);
            Assert.That(straight.Count, Is.EqualTo(7), "the shortest run");
            int bends = 0;
            for (int i = 2; i < straight.Count; i++) if ((straight[i].c - straight[i - 1].c, straight[i].r - straight[i - 1].r) != (straight[i - 1].c - straight[i - 2].c, straight[i - 1].r - straight[i - 2].r)) bends++;
            Assert.That(bends, Is.EqualTo(1), "one bend where one does");
            var around = GrowthLab.Route((0, 0), (4, 0), c => !(c.c == 2 && c.r < 5));
            Assert.That(around.Any(c => c.c == 2 && c.r < 5), Is.False);
            Assert.That(around.Last(), Is.EqualTo((4, 0)));
            Assert.That(GrowthLab.Route((0, 0), (4, 0), c => c.c != 2), Is.Null, "a wall with no way through");
            // A pipe crosses another at right angles, hopping over it, but never runs along it or turns where they cross.
            var vertical = new List<(int c, int r)> { (2, 0), (2, 1), (2, 2), (2, 3), (2, 4), (2, 5), (2, 6) };
            var taken = GrowthLab.Occupancy(new IList<(int c, int r)>[] { vertical });
            Assert.That(GrowthLab.Route((0, 3), (4, 3), _ => true, taken), Is.EqualTo(new[] { (0, 3), (1, 3), (2, 3), (3, 3), (4, 3) }), "straight across");
            var turning = GrowthLab.Route((0, 3), (2, 8), _ => true, taken);
            Assert.That(turning.Contains((2, 3)), Is.False, "no turn inside a crossing");
            Assert.That(GrowthLab.CanJoin(new IList<(int c, int r)>[] { vertical }, turning), Is.True);
            Assert.That(GrowthLab.CanJoin(new IList<(int c, int r)>[] { vertical }, new List<(int c, int r)> { (3, 2), (2, 2), (2, 3), (1, 3) }), Is.False, "a bend on another pipe");
            Assert.That(GrowthLab.CanJoin(new IList<(int c, int r)>[] { vertical }, new List<(int c, int r)> { (3, 1), (3, 2), (2, 2), (1, 2) }), Is.True, "a right-angle crossing");
            Assert.That(GrowthLab.Route((2, 7), (2, 0), _ => true, taken).Skip(1).Take(5).Any(c => c.c == 2 && c.r < 6), Is.False, "never along another pipe");
            Assert.That(GrowthLab.Route((0, 5), (2, 6), c => c.r != 6, taken), Is.Null, "not even turning out of a crossing into an end it shares, when that is the only way in");
            Assert.That(GrowthLab.InLab((13, 5), annex: true), Is.False, "the back room is narrower than the annex");
            Assert.That((GrowthLab.InLab((13, 12), annex: true), GrowthLab.InLab((13, 12), annex: false)), Is.EqualTo((true, false)));
            // Every end sits in its own cell inside the lab.
            var cells = GrowthLab.AllEnds.Select(GrowthLab.EndCell).ToList();
            Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Count));
            Assert.That(cells.All(c => GrowthLab.InLab(c, true)), Is.True);
            Assert.That(GrowthLab.AllEnds.Where(GrowthLab.IsBuffer).All(e => GrowthLab.EndCell(e).r >= GrowthLab.AnnexFirstRow), Is.True, "buffers stand in the annex");
        }

        [Test]
        public void TheHandPumpFillsABufferInBulkWhileItIsCranked()
        {
            var shop = Lab();
            Assert.That(shop.CrankPump("bloom", 1).Failure, Is.EqualTo(GrowthFailure.NoSuchPlace), "no line yet");
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Assert.That(shop.LineStatus(shop.Pipes[0]), Is.EqualTo("Hold the pump's crank to pump"));
            shop.GrowerUnits["bloom"] = 10;
            var r = shop.CrankPump("bloom", 1.0f);
            Assert.That(r.Detail, Is.EqualTo("3"), "a dose every 0.3 s while cranked");
            Assert.That((shop.BufferAt(0).Units, shop.GrowerUnits["bloom"]), Is.EqualTo((3f, 7f)));
            shop.Tick(10);
            Assert.That(shop.BufferAt(0).Units, Is.EqualTo(3f), "a hand pump moves nothing by itself");
            shop.GrowerUnits["bloom"] = 2;
            shop.CrankPump("bloom", 5);
            Assert.That((shop.BufferAt(0).Units, shop.GrowerUnits["bloom"] < 1), Is.EqualTo((5f, true)), "until the grower is empty");
            Assert.That(shop.CrankPump("bloom", 1).Failure, Is.EqualTo(GrowthFailure.GrowerEmpty));
            shop.BufferAt(0).Units = GrowthContent.BufferCapacity;
            shop.GrowerUnits["bloom"] = 10;
            Assert.That(shop.CrankPump("bloom", 1).Failure, Is.EqualTo(GrowthFailure.ChamberFull));
            Assert.That(shop.LineStatus(shop.Pipes[0]), Is.EqualTo("Buffer 1 is full"));
            // The siphon takes from a buffer as from a grower.
            shop.Held.Clear();
            shop.CollectBuffer(0, shop.HoldSeconds * 2 + 0.01f);
            Assert.That(shop.Held, Is.EqualTo(new[] { "bloom", "bloom" }));
            Assert.That(shop.BufferAt(0).Units, Is.EqualTo(GrowthContent.BufferCapacity - 2f));
            UpgradeTo(shop, "pump", 2);
            Assert.That(shop.CrankPump("bloom", 1).Failure, Is.EqualTo(GrowthFailure.Busy), "electric pumps run by themselves");
        }

        [Test]
        public void ElectricPumpsRunOnTheGeneratorsPowerInPriorityOrderAndSayWhenTheyWait()
        {
            var shop = Lab(pumps: 2);
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Lay(shop, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            var bloom = shop.Pipes[0]; var flow = shop.Pipes[1];
            shop.GrowerUnits["bloom"] = shop.GrowerUnits["flow"] = 10;
            shop.Tick(1);
            Assert.That((shop.BufferAt(0).Units, shop.BufferAt(1).Units), Is.EqualTo((0f, 0f)), "the hand crank gives nothing unless cranked");
            Assert.That(shop.LineStatus(bloom), Is.EqualTo("Waiting for power"));
            Assert.That((shop.Supply, shop.Draw), Is.EqualTo((0f, 2f)));
            for (int i = 0; i < 10; i++) { Assert.That(shop.CrankGenerator().Success, Is.True); shop.Tick(0.3f); }
            Assert.That(shop.BufferAt(0).Units, Is.EqualTo(2f).Within(0.01f), "one unit runs the first pump: a dose every 1.5 s");
            Assert.That(shop.BufferAt(1).Units, Is.EqualTo(0f), "the second waits");
            Assert.That((shop.LineStatus(bloom), shop.LineStatus(flow)), Is.EqualTo(("Pumping", "Waiting for power")));
            Assert.That(shop.MovePower(flow.PowerId, -1).Success, Is.True);
            Assert.That(shop.PowerOrder, Is.EqualTo(new[] { flow.PowerId, bloom.PowerId }));
            Assert.That(shop.MovePower(flow.PowerId, -1).Failure, Is.EqualTo(GrowthFailure.MaxLevel));
            for (int i = 0; i < 5; i++) { shop.CrankGenerator(); shop.Tick(0.3f); }
            Assert.That(shop.BufferAt(1).Units, Is.EqualTo(1f).Within(0.01f), "now the Flow pump is served first");
            shop.Tick(1);
            Assert.That(shop.LineStatus(flow), Is.EqualTo("Waiting for power"), "the crank's unit stops when it stops");
            // The bubble turbine: 3 units for both pumps, by themselves.
            UpgradeTo(shop, "generator", 2);
            float b0 = shop.BufferAt(0).Units, b1 = shop.BufferAt(1).Units;
            shop.Tick(1.5f);
            Assert.That(shop.BufferAt(0).Units - b0, Is.EqualTo(1f).Within(0.01f));
            Assert.That(shop.BufferAt(1).Units - b1, Is.EqualTo(1f).Within(0.01f));
            Assert.That((shop.Supply, shop.Draw), Is.EqualTo((3f, 2f)));
            // A full buffer asks for no power and says why; so does a grower that runs dry.
            shop.BufferAt(0).Units = GrowthContent.BufferCapacity;
            shop.Tick(0.1f);
            Assert.That((shop.LineStatus(bloom), shop.PowerUse(bloom.PowerId).Draw), Is.EqualTo(("Buffer 1 is full", 0f)));
            shop.BufferAt(0).Units = 0; shop.GrowerUnits["bloom"] = 0;
            Assert.That(shop.LineStatus(bloom), Is.EqualTo("The Bloom grower is empty"));
            // Fast pumps (era 4) draw 2 units each: over budget, the second in line runs slower and says so.
            var fast = Lab(4, 4, pumps: 2);
            UpgradeTo(fast, "generator", 2); UpgradeTo(fast, "pump", 3);
            Lay(fast, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Lay(fast, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            fast.GrowerUnits["bloom"] = fast.GrowerUnits["flow"] = 10;
            fast.Tick(0.75f);
            Assert.That(fast.BufferAt(0).Units, Is.EqualTo(1f).Within(0.01f));
            Assert.That(fast.BufferAt(1).Units, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(fast.LineStatus(fast.Pipes[1]), Is.EqualTo("Slowed: power for 1 of 2 units"));
            Assert.That(fast.PowerUses.Select(u => u.Name), Is.EqualTo(new[] { "Bloom pump to buffer 1", "Flow pump to buffer 2" }));
        }

        [Test]
        public void TheAutoDoserDosesAKnownRecipeFromThePortsBuffersAndNeverSomethingNew()
        {
            // Hand pumps, so the lines move nothing while the auto-doser works (the bubble turbine powers it).
            var shop = Lab(4, 4, pumps: 1);
            UpgradeTo(shop, "generator", 2);
            Assert.That(shop.SetAutoRecipe("pondlet").Failure, Is.EqualTo(GrowthFailure.Locked), "the auto-doser comes with the incubator's level 4");
            UpgradeTo(shop, "incubator", 4);
            Assert.That(shop.AutoRecipeCheck("pondlet", out string why), Is.EqualTo(GrowthFailure.NotDiscovered));
            Assert.That(why, Does.Contain("never tries something new"));
            Assert.That(shop.SetAutoRecipe("puddlepup").Failure, Is.EqualTo(GrowthFailure.NotAFounder));
            shop.Discovered.Add("pondlet");
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Lay(shop, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            Assert.That(shop.SwitchAutoDoser(true).Failure, Is.EqualTo(GrowthFailure.NoDose), "a recipe first");
            Assert.That(shop.SetAutoRecipe("pondlet").Success && shop.SwitchAutoDoser(true).Success, Is.True);
            Assert.That(shop.AutoStatus, Is.EqualTo("Pipe a Bloom buffer to the port"));
            Lay(shop, GrowthLab.Buffer(0), GrowthLab.Port);
            Lay(shop, GrowthLab.Buffer(1), GrowthLab.Port);
            shop.GrowerUnits["bloom"] = shop.GrowerUnits["flow"] = 0;
            Assert.That(shop.AutoStatus, Is.EqualTo("Waiting for 1 Bloom in the port's buffers"));
            shop.BufferAt(0).Units = 3; shop.BufferAt(1).Units = 3;
            Assert.That(shop.PowerOrder.Last(), Is.EqualTo(GrowthShop.DoserPower), "the auto-doser joins the power board");
            shop.Tick(0.1f);
            Assert.That(shop.AutoBatch, Is.True);
            Assert.That(shop.AutoStatus, Does.StartWith("Dosing a Pondlet: 0 of 2 doses"));
            Assert.That(shop.AddDose("bloom").Failure, Is.EqualTo(GrowthFailure.Busy), "the player keeps out of a batch");
            shop.Tick(0.5f);
            Assert.That((shop.Chamber.Count, shop.BufferAt(0).Units), Is.EqualTo((1, 2f)), "a dose every 0.5 s, from the port's buffer");
            shop.Tick(0.5f);
            Assert.That((shop.Incubating, shop.AutoBatches, shop.Chamber.Count, shop.BufferAt(1).Units), Is.EqualTo((true, 1, 0, 2f)));
            Assert.That(shop.AutoStatus, Is.EqualTo("Incubating"));
            shop.Tick(shop.IncubationSeconds + 0.1f);
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(3), "a clutch of three Pondlets");
            Assert.That(shop.AutoStatus, Is.EqualTo("Waiting: take the clutch out of the incubator"), "GR.15.1: a full output waits");
            shop.Tick(5);
            Assert.That(shop.AutoBatches, Is.EqualTo(1));
            while (shop.Hatchlings.Count > 0) { shop.TakeHatchling(); shop.Carried.Clear(); }
            shop.Tick(0.6f);
            Assert.That((shop.AutoBatch, shop.Chamber.Count), Is.EqualTo((true, 1)));
            // Off mid-batch: the dose stays in the chamber for the player.
            Assert.That(shop.SwitchAutoDoser(false).Success, Is.True);
            Assert.That((shop.AutoBatch, shop.Chamber.Count, shop.AutoStatus), Is.EqualTo((false, 1, "Off")));
            Assert.That(shop.AddDose("bloom").Failure, Is.Not.EqualTo(GrowthFailure.Busy));
            // No power, no dosing: it waits and says so.
            var cold = Lab(4, 4, pumps: 1); UpgradeTo(cold, "incubator", 4); cold.Discovered.Add("pondlet");
            Lay(cold, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0)); Lay(cold, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            Lay(cold, GrowthLab.Buffer(0), GrowthLab.Port); Lay(cold, GrowthLab.Buffer(1), GrowthLab.Port);
            cold.SetAutoRecipe("pondlet"); cold.SwitchAutoDoser(true);
            cold.BufferAt(0).Units = 3; cold.BufferAt(1).Units = 3;
            cold.Tick(2);
            Assert.That((cold.AutoBatch, cold.Chamber.Count, cold.AutoStatus), Is.EqualTo((true, 0, "Waiting for power")));
            for (int i = 0; i < 10; i++) { cold.CrankGenerator(); cold.Tick(0.3f); }
            Assert.That(cold.Incubating, Is.True, "the hand crank powers it too");
        }

        [Test]
        public void PoweredPumpsRunningOpenEraFour()
        {
            var shop = Lab(3, 4, pumps: 2);
            shop.Discovered.AddRange(GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Founder).Take(12).Select(s => s.Id));
            Assert.That((shop.PoweredPumps, shop.Era), Is.EqualTo((false, 3)));
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Assert.That(shop.Era, Is.EqualTo(3), "the hand crank is no powered pump");
            Assert.That(shop.Objectives().Any(o => o.text.StartsWith("Power the pumps") && !o.done), Is.True);
            UpgradeTo(shop, "generator", 2);
            Assert.That((shop.PoweredPumps, shop.Era), Is.EqualTo((true, 4)));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.EraUp && e.Text.StartsWith("Era 4")), Is.True);
            Assert.That(shop.Objectives().Select(o => o.text), Has.Some.StartsWith("Let the incubator dose a recipe by itself"));
            var few = Lab(3, 4, pumps: 2);
            UpgradeTo(few, "generator", 2);
            Lay(few, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0));
            Assert.That(few.Era, Is.EqualTo(3), "12 species too");
        }

        [Test]
        public void TheStartPointsAreShopsAPlayerCouldHave()
        {
            var three = GrowthStarts.Era3Start();
            Assert.That((three.Era, three.Stars, three.Annex, three.Credits, three.Rp, three.UnitB), Is.EqualTo((3, 3, false, 1200, 45, true)));
            Assert.That(three.Discovered.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(three.Stock.Count(), Is.GreaterThan(8), "tanks with stock to sell");
            Assert.That(three.AnnexCheck(out _), Is.EqualTo(GrowthFailure.None), "the lab annex is the next thing to buy");
            var four = GrowthStarts.Era4Start();
            Assert.That((four.Era, four.Stars, four.Annex, four.PoweredPumps, four.Lines.Count(), four.Credits), Is.EqualTo((4, 4, true, true, 2, 6000)));
            Assert.That(four.UpgradeCheck("incubator", out _, out _), Is.EqualTo(GrowthFailure.None), "the incubator's level 4 is next");
            foreach (var shop in new[] { three, four })
                Assert.That(GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(shop.Snapshot())), out string problem), Is.Not.Null, problem);
            Assert.That(GrowthStarts.Make("era5"), Is.Null);
        }

        [Test]
        public void TheVersionSixSaveKeepsTheLabAndAJobMidwayWithoutDuplicatingCulture()
        {
            var shop = Lab(4, 4, pumps: 1);
            UpgradeTo(shop, "generator", 2); UpgradeTo(shop, "incubator", 4);
            shop.Discovered.Add("pondlet");
            Lay(shop, GrowthLab.Tap("bloom"), GrowthLab.Buffer(0)); Lay(shop, GrowthLab.Tap("flow"), GrowthLab.Buffer(1));
            Lay(shop, GrowthLab.Buffer(0), GrowthLab.Port); Lay(shop, GrowthLab.Buffer(1), GrowthLab.Port);
            shop.MovePower(shop.Pipes[1].PowerId, -1);
            shop.SetAutoRecipe("pondlet"); shop.SwitchAutoDoser(true);
            shop.GrowerUnits["bloom"] = shop.GrowerUnits["flow"] = 0;
            shop.BufferAt(0).Units = 1; shop.BufferAt(1).Units = 1;
            shop.Tick(0.6f);
            // The Bloom dose is in; the siphon takes the Flow away: the batch waits for it.
            shop.Held.Clear();
            Assert.That(shop.CollectBuffer(1, shop.HoldSeconds + 0.01f).Detail, Is.EqualTo("1"));
            Assert.That((shop.AutoBatch, shop.Chamber.Count, shop.AutoStatus), Is.EqualTo((true, 1, "Waiting for 1 Flow in the port's buffers")));
            shop.Held.Clear();
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            Assert.That((back.Annex, back.Buffers.Count, back.Pipes.Count, back.AutoBatch, back.Chamber.Count, back.AutoRecipe), Is.EqualTo((true, 2, 4, true, 1, "pondlet")));
            Assert.That(back.PowerOrder, Is.EqualTo(shop.PowerOrder));
            Assert.That(back.Pipes.Select(p => p.Cells.Count), Is.EqualTo(shop.Pipes.Select(p => p.Cells.Count)));
            // The job carries on: one more Flow and the batch incubates, with no dose twice.
            back.BufferAt(1).Units = 1;
            back.Tick(0.6f);
            Assert.That((back.Incubating, back.BufferAt(0).Units, back.BufferAt(1).Units), Is.EqualTo((true, 0f, 0f)));
            Assert.That(GrowthSave.CurrentVersion, Is.EqualTo(6));
            // A GR-06 save loads with no annex.
            var gr06 = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(At(4, 4).Snapshot()).Replace("\"version\":6", "\"version\":5")), out problem);
            Assert.That(problem, Is.Null);
            Assert.That((gr06.Annex, gr06.Pipes.Count, gr06.PowerOrder.Count), Is.EqualTo((false, 0, 0)));
            void Refused(Action<GrowthSave> edit, string expected)
            {
                var bad = JsonUtility.FromJson<GrowthSave>(json);
                edit(bad);
                Assert.That(GrowthShop.Restore(bad, out string why), Is.Null, expected);
                Assert.That(why, Is.EqualTo(expected));
            }
            Refused(b => b.annex = false, "Power without the lab annex");
            Refused(b => b.buffers[1].culture = "bloom", "A buffer holding another culture than its line");
            Refused(b => b.buffers[0].units = GrowthContent.BufferCapacity + 1, "A buffer holds more than it can");
            Refused(b => b.pipes[0].cells = b.pipes[0].cells.Take(2).Concat(b.pipes[0].cells.Skip(4)).ToArray(), "A broken pipe");
            Refused(b => b.pipes[0].cells = b.pipes[0].cells.Skip(2).ToArray(), "A pipe that does not reach its ends");
            Refused(b => b.pipes[1].cells = b.pipes[0].cells, "A pipe that does not reach its ends");
            Refused(b => b.pipes[1].id = b.pipes[0].id, "A pipe is saved twice");
            Refused(b => { b.pipes[3].from = GrowthLab.Tap("flow"); b.pipes[3].to = GrowthLab.Buffer(1); }, "A pipe that does not reach its ends");
            Refused(b => b.chamber = new[] { "bloom", "bloom" }, "An auto-doser batch that cannot be");
            Refused(b => b.autoRecipe = "ribbonfin", "An auto-doser recipe that cannot be");
            Refused(b => b.powerOrder = b.powerOrder.Concat(new[] { "line:999" }).ToArray(), "Unknown power use line:999");
            Refused(b => b.levels[Array.IndexOf(b.stations, "generator")] = 3, "Bad station level generator");
        }
    }
}
