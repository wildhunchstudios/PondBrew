using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// GR-31 (tester round 1): the shop and its helpers. A boop for a giggle, the shop opening by itself once there is enough
    /// to sell, what a visitor picked and bought, Nori working with no targets, and tanks that take only what their rule allows.
    /// </summary>
    public sealed class GrowthShopFloorTests
    {
        static GrowthShop OpenShop(string species)
        {
            var shop = new GrowthShop();
            shop.DevelopmentStock(shop.Containers[0].Id, species);
            Assert.That(shop.OpenShop().Success, Is.True);
            return shop;
        }

        static GrowthVisitor Queued(GrowthShop shop, int id, string type = "bargain")
        {
            var v = new GrowthVisitor { Id = id, Type = type, Budget = 500, State = GrowthVisitorState.Queued, ContainerId = shop.Containers[0].Id, Patience = GrowthShop.QueuePatience };
            shop.Visitors.Add(v);
            return v;
        }

        static GrowthShop WithNori(params GrowthContainer[] extra)
        {
            var shop = new GrowthShop { Era = 3, Reputation = GrowthContent.StarThresholds[2] + 10, Credits = 50000, Rp = 500, SalesTotal = GrowthContent.PipSalesGate };
            foreach (var c in extra) shop.Containers.Add(c);
            if (extra.Length == 0) shop.Containers.Add(Large("a3"));
            Assert.That(shop.Hire("nori").Success, Is.True);
            return shop;
        }

        static GrowthContainer Large(string place) => new GrowthContainer { Id = "large-" + place, Kind = "large", Place = place };

        static int nextId = 8000;
        static GrowthSpecimen Make(string species) => new GrowthSpecimen { Id = nextId++, Species = species };

        static void Run(GrowthShop shop, float seconds) { for (float t = 0; t < seconds; t += 0.1f) shop.Tick(0.1f); }

        [Test]
        public void ABoopIsForAGiggleAndBuysALittlePatienceOnce()
        {
            var night = new GrowthShop();
            night.Visitors.Add(new GrowthVisitor { Id = 9, Type = "bargain" });
            Assert.That(night.Boop(9).Failure, Is.EqualTo(GrowthFailure.NotDuringDay), "visitors come by day");

            var shop = OpenShop("pondlet");
            var v = Queued(shop, 1);
            v.Clock = 20;
            int rep = shop.Reputation, credits = shop.Credits;
            Assert.That(shop.Boop(1).Success, Is.True);
            Assert.That((v.Boops, v.Clock), Is.EqualTo((1, 20 - GrowthShop.BoopPatience)), "the first boop gives back a little of their wait");
            Assert.That(v.State, Is.EqualTo(GrowthVisitorState.Queued), "and they stay in the queue");
            Assert.That((shop.Reputation, shop.Credits), Is.EqualTo((rep, credits)), "no harm done");
            Assert.That(shop.Boop(1).Failure, Is.EqualTo(GrowthFailure.Busy), "one thing at a time");
            shop.Tick(GrowthShop.BoopCooldown + 0.1f);
            float now = v.Clock;
            Assert.That(shop.Boop(1).Success, Is.True, "a moment later, again");
            Assert.That((v.Boops, v.Clock), Is.EqualTo((2, now)), "but patience is bought only once");
            Assert.That(shop.Boop(404).Failure, Is.EqualTo(GrowthFailure.NoSuchVisitor), "nobody there");
            shop.Visitors.Add(new GrowthVisitor { Id = 2, Type = GrowthShop.GuestType, State = GrowthVisitorState.Browsing });
            shop.Tick(GrowthShop.BoopCooldown + 0.1f);
            Assert.That(shop.Boop(2).Success, Is.False, "exhibition guests are here on important business");
        }

        [Test]
        public void TheShopOpensByItselfOnceThereIsEnoughToSell()
        {
            var shop = new GrowthShop();
            shop.DevelopmentStock(shop.Containers[0].Id, "pondlet");
            shop.AutoOpenAt = 2;
            Run(shop, GrowthShop.AutoOpenWait + 1);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night), "one creature is not enough");
            shop.DevelopmentStock(shop.Containers[0].Id, "pondlet");
            shop.Tick(0.1f);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Day), "two is, once the night's first seconds have passed");
            Assert.That(shop.TakeEvents().Any(e => e.Text.StartsWith("The shop opened by itself")), Is.True, "and it says why");

            var early = new GrowthShop();
            early.DevelopmentStock(early.Containers[0].Id, "pondlet");
            early.AutoOpenAt = 1;
            Run(early, GrowthShop.AutoOpenWait - 1);
            Assert.That(early.Phase, Is.EqualTo(GrowthPhase.Night), "a stocked shop gives the player a moment before opening");

            var off = new GrowthShop();
            off.DevelopmentStock(off.Containers[0].Id, "pondlet");
            Run(off, GrowthShop.AutoOpenWait + 1);
            Assert.That(off.Phase, Is.EqualTo(GrowthPhase.Night), "off unless chosen");
            // Chosen late in the night, it waits its seconds again, so it never opens the moment it is chosen.
            off.DevelopmentStock(off.Containers[0].Id, "pondlet"); off.DevelopmentStock(off.Containers[0].Id, "pondlet");
            off.ChooseAutoOpen(3);
            Assert.That(off.AutoOpenAt, Is.EqualTo(3), "one of the choices");
            off.Tick(0.1f);
            Assert.That(off.Phase, Is.EqualTo(GrowthPhase.Night), "not the moment it is chosen");
            Run(off, GrowthShop.AutoOpenWait + 0.5f);
            Assert.That(off.Phase, Is.EqualTo(GrowthPhase.Day), "but once its seconds have passed again");
        }

        [Test]
        public void TheAutoOpenAndATanksRuleKeepInTheSave()
        {
            var shop = new GrowthShop { AutoOpenAt = 6 };
            Assert.That(shop.SetTankRule(shop.Containers[0].Id, "calm").Success, Is.True);
            var back = GrowthShop.Restore(shop.Snapshot(), out var problem);
            Assert.That(problem, Is.Null);
            Assert.That(back.AutoOpenAt, Is.EqualTo(6));
            Assert.That(back.Containers[0].Rule, Is.EqualTo("calm"));
            Assert.That(shop.SetTankRule("nope", "calm").Failure, Is.EqualTo(GrowthFailure.NoSuchContainer));
            Assert.That(shop.SetTankRule(shop.Containers[0].Id, "sparkly").Success, Is.False, "only the rules there are");
        }

        [Test]
        public void WithNoTargetsNoriFillsTheTanksWithWhateverWaits()
        {
            var shop = WithNori();
            var nori = shop.Helper("nori");
            shop.Tick(0.1f);
            Assert.That(nori.Status, Is.EqualTo("Nothing waiting to move"), "nothing there yet, and it says so");
            shop.Hatchlings.Add(Make("pondlet"));
            shop.Tick(0.1f);
            Assert.That(nori.Step, Is.EqualTo("fetch"), "no targets set: a waiting creature still gets carried");
            Run(shop, 30);
            Assert.That(shop.Hatchlings, Is.Empty);
            Assert.That(shop.Stock.Single().Species, Is.EqualTo("pondlet"), "into a tank");
        }

        [Test]
        public void WithATargetNoriStillStopsAtIt()
        {
            var shop = WithNori();
            shop.Discovered.Add("pondlet");
            shop.SetTarget("pondlet", 1);
            shop.Hatchlings.Add(Make("pondlet"));
            shop.Hatchlings.Add(Make("pondlet"));
            Run(shop, 40);
            Assert.That(shop.Shown("pondlet"), Is.EqualTo(1), "one on show, as asked");
            Assert.That(shop.Hatchlings.Count, Is.EqualTo(1), "the other waits: no rack to take it");
            Assert.That(shop.Helper("nori").Status, Does.StartWith("No room for what is waiting"));
        }

        [Test]
        public void ATanksRuleSaysWhatHelpersMayPutInIt()
        {
            var calm = GrowthContent.Species.First(x => x.Temperament == Temperament.Calm && x.Water == Water.Fresh);
            var lively = GrowthContent.Species.First(x => x.Temperament == Temperament.Lively && x.Water == Water.Fresh);
            var brine = GrowthContent.Species.First(x => x.Water == Water.Brine);
            var c = new GrowthContainer { Id = "t", Kind = "large", Place = "a3" };
            Assert.That(GrowthShop.RuleAllows(c, calm.Id), Is.True, "anything, unless a rule says");
            c.Rule = "calm";
            Assert.That((GrowthShop.RuleAllows(c, calm.Id), GrowthShop.RuleAllows(c, lively.Id)), Is.EqualTo((true, false)));
            c.Rule = "lively";
            Assert.That((GrowthShop.RuleAllows(c, calm.Id), GrowthShop.RuleAllows(c, lively.Id)), Is.EqualTo((false, true)));
            c.Rule = "fresh";
            Assert.That((GrowthShop.RuleAllows(c, calm.Id), GrowthShop.RuleAllows(c, brine.Id)), Is.EqualTo((true, false)));
            c.Rule = "brine";
            Assert.That((GrowthShop.RuleAllows(c, calm.Id), GrowthShop.RuleAllows(c, brine.Id)), Is.EqualTo((false, true)));
            c.Rule = "one kind";
            Assert.That(GrowthShop.RuleAllows(c, calm.Id), Is.True, "an empty tank takes any one kind");
            c.Occupants.Add(Make(calm.Id));
            Assert.That((GrowthShop.RuleAllows(c, calm.Id), GrowthShop.RuleAllows(c, lively.Id)), Is.EqualTo((true, false)), "then only that kind");
        }

        [Test]
        public void NoriFillsTheTankWithARuleFirstAndKeepsOthersOutOfIt()
        {
            var calm = GrowthContent.Species.First(x => x.Temperament == Temperament.Calm && x.Water == Water.Fresh && x.Kind == SpeciesKind.Founder);
            var lively = GrowthContent.Species.First(x => x.Temperament == Temperament.Lively && x.Water == Water.Fresh && x.Kind == SpeciesKind.Founder);
            var ruled = Large("a3");
            ruled.Rule = "calm";
            var shop = WithNori(ruled);
            shop.Hatchlings.Add(Make(calm.Id));
            Run(shop, 40);
            Assert.That(ruled.Occupants.Select(o => o.Species), Is.EqualTo(new[] { calm.Id }), "the tank whose rule asks for it, ahead of a plain one");
            shop.Hatchlings.Add(Make(lively.Id));
            Run(shop, 40);
            Assert.That(ruled.Occupants.Any(o => o.Species == lively.Id), Is.False, "a lively one stays out of the calm tank");
            Assert.That(shop.Shown(lively.Id), Is.EqualTo(1), "and goes to a tank with no rule");
        }

        [Test]
        public void ATankUpgradesByDayToo()
        {
            var shop = OpenShop("pondlet");
            shop.Credits = 50000;
            shop.Era = 4;
            shop.Reputation = GrowthContent.StarThresholds[4] + 10;
            var tank = shop.Containers[0];
            int before = tank.Level;
            Assert.That(shop.ContainerUpgradeCheck(tank.Id, out _, out _), Is.EqualTo(GrowthFailure.None), "the shop is open and the upgrade is allowed");
            Assert.That(shop.UpgradeContainer(tank.Id).Success, Is.True);
            Assert.That(tank.Level, Is.EqualTo(before + 1));
            Assert.That(tank.Occupants.Single().Species, Is.EqualTo("pondlet"), "and the creature stays in it");
        }

        [Test]
        public void WhatAVisitorPickedIsTheirsWhileItIsInItsTank()
        {
            var shop = OpenShop("pondlet");
            var creature = shop.Stock.Single();
            var v = Queued(shop, 1);
            Assert.That(shop.PickedBy(v), Is.Null, "nothing picked yet");
            v.SpecimenIds.Add(creature.Id);
            Assert.That(shop.PickedBy(v), Is.SameAs(creature));
            shop.Containers[0].Occupants.Clear();
            Assert.That(shop.PickedBy(v), Is.Null, "gone from the tank");
        }

        [Test]
        public void ADayLengthReadsWithItsUnits()
        {
            Assert.That(GrowthShop.DayLengthWords, Is.EqualTo("5 minutes 30 seconds"));
            Assert.That(GrowthShop.DurationWords(60), Is.EqualTo("1 minute"));
            Assert.That(GrowthShop.DurationWords(61), Is.EqualTo("1 minute 1 second"));
            Assert.That(GrowthShop.DurationWords(45), Is.EqualTo("45 seconds"));
            Assert.That(GrowthShop.DurationWords(0.4f), Is.EqualTo("1 second"), "rounded up, as the day's clock is");
        }

        [Test]
        public void NoWordsLeftOfTheOldRoomsOrAClutch()
        {
            var words = GrowthLessons.All.SelectMany(l => l.Card.Concat(new[] { l.Title })).Concat(GrowthContent.Eras.SelectMany(e => new[] { e.Name, e.Opens, e.Goal }))
                .Concat(GrowthContent.Stations.SelectMany(st => st.Levels.Select(l => l.Change))).ToList();
            Assert.That(words.Where(w => w != null && (w.Contains("ack room") || w.Contains("annex") || w.Contains("lutch") || w.Contains("Show me") || w.Contains("5:30"))), Is.Empty);
            Assert.That(GrowthShop.AnnexDef.Name, Is.EqualTo("Power room"));
            Assert.That(GrowthContent.Eras[0].Name, Is.EqualTo("Laboratory"));
        }

        [Test]
        public void OnlyTheTanksInTheMiddleOfARoomStandFree()
        {
            var free = GrowthShop.Places.Select(p => p.Id).Where(GrowthSpaces.FreeStanding).ToList();
            Assert.That(free, Is.EquivalentTo(new[] { "a3", "b3" }));
        }

        [Test]
        public void ThePlainDecorationsAreTheCheapestOfEachTheme()
        {
            foreach (Water w in System.Enum.GetValues(typeof(Water)))
            {
                var theme = GrowthContent.Decor.Where(d => d.Water == w).OrderBy(d => d.Credits).ToList();
                Assert.That(theme.Take(2).All(d => new[] { "stones", "fern", "rocks", "bamboo", "shells", "driftwood" }.Contains(d.Id)), Is.True, w + "'s two plain pieces cost the least");
            }
        }

        [Test]
        public void TheNewKeysAreAllFree()
        {
            var settings = new GrowthSettings();
            var used = System.Enum.GetValues(typeof(GrowthAction)).Cast<GrowthAction>().Select(a => settings.InputOf(a)).ToList();
            Assert.That(used.Distinct().Count(), Is.EqualTo(used.Count), "no two actions share a key by default");
            Assert.That(settings.InputOf(GrowthAction.Run), Is.EqualTo("Key/LeftShift"));
            Assert.That(settings.InputOf(GrowthAction.Boop), Is.EqualTo("Key/C"));
        }
    }
}
