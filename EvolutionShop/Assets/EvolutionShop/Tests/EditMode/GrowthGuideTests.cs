using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-14: the guided first night (GR.43): off unless a new game starts it, each step read from the shop as it is
    /// (in any order, and asked for again when undone), its targets, the first day with a sale ending it with a note, skipping
    /// it, and its state in the version 11 save (older saves have none).</summary>
    public sealed class GrowthGuideTests
    {
        static GrowthShop Guided() => new GrowthShop { Guide = GrowthGuideMode.On };

        static void Run(GrowthShop shop, float seconds, bool serve = false)
        {
            for (float t = 0; t < seconds; t += 0.25f)
            {
                shop.Tick(0.25f);
                if (serve && shop.FrontOfQueue != null) shop.Serve(0.25f);
            }
        }

        static GrowthGuideStep StepOf(GrowthShop shop) => shop.GuideNow().Step;

        /// <summary>Collects a dose of each culture, mixes them and takes the hatchling, with the shop's own actions.</summary>
        static void Hatch(GrowthShop shop)
        {
            Assert.That(shop.Collect("bloom", shop.HoldSeconds).Success && shop.Collect("flow", shop.HoldSeconds).Success, Is.True);
            Assert.That(shop.AddDose("bloom").Success && shop.AddDose("flow").Success && shop.StartIncubation().Success, Is.True);
            shop.Tick(shop.IncubationSeconds + 0.1f);
            Assert.That(shop.TakeHatchling().Success, Is.True);
        }

        [Test]
        public void AShopHasNoGuideUntilANewGameStartsIt()
        {
            var shop = new GrowthShop();
            Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.Off), "fixtures, start points and old saves have none");
            Assert.That(shop.GuideNow(), Is.Null);
            Assert.That(GrowthStarts.Make(GrowthStarts.Exhibition).Guide, Is.EqualTo(GrowthGuideMode.Off), "a start point is never guided");
        }

        [Test]
        public void TheGuideWalksTheFirstLoopStepByStepWithTargets()
        {
            var shop = Guided();
            var g = shop.GuideNow();
            Assert.That(g.Step, Is.EqualTo(GrowthGuideStep.Collect));
            Assert.That(g.Target, Is.EqualTo("grower:bloom"));
            Assert.That(g.TargetName, Is.EqualTo("Bloom grower"));
            Assert.That(shop.Collect("bloom", shop.HoldSeconds).Success, Is.True);
            g = shop.GuideNow();
            Assert.That((g.Step, g.Target), Is.EqualTo((GrowthGuideStep.Collect, "grower:flow")), "a Bloom dose: the Flow grower next");
            Assert.That(g.Text, Does.StartWith("Now the Flow grower"));
            Assert.That(shop.Collect("flow", shop.HoldSeconds).Success, Is.True);
            Assert.That((StepOf(shop), shop.GuideNow().Target), Is.EqualTo((GrowthGuideStep.Mix, "incubator")));
            Assert.That(shop.AddDose("bloom").Success, Is.True);
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.Mix), "a dose in the chamber: still mixing");
            Assert.That(shop.AddDose("flow").Success && shop.StartIncubation().Success, Is.True);
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.Take));
            Assert.That(shop.GuideNow().Text, Does.StartWith("Your creature is growing"));
            shop.Tick(shop.IncubationSeconds + 0.1f);
            Assert.That(shop.GuideNow().Text, Does.StartWith("It hatched"));
            Assert.That(shop.TakeHatchling().Success, Is.True);
            var tank = shop.Containers[0];
            g = shop.GuideNow();
            Assert.That((g.Step, g.Target, g.TargetName), Is.EqualTo((GrowthGuideStep.Stock, "container:" + tank.Id, "Starter tank")));
            Assert.That(shop.PutIn(tank.Id).Success, Is.True);
            Assert.That((StepOf(shop), shop.GuideNow().Target), Is.EqualTo((GrowthGuideStep.Open, "counter")));
            Assert.That(shop.OpenShop().Success, Is.True);
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.Sell));
            for (int i = 0; i < 4000 && shop.SalesTotal == 0; i++) { shop.Tick(0.25f); if (shop.FrontOfQueue != null) shop.Serve(0.25f); }
            Assert.That(shop.SalesTotal, Is.EqualTo(1), "the first sale");
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.FirstDay));
            Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.On), "on until the day ends");
        }

        [Test]
        public void TheFirstDayWithASaleEndsTheGuideWithANote()
        {
            var shop = Guided();
            Hatch(shop);
            Assert.That(shop.PutIn(shop.Containers[0].Id).Success && shop.OpenShop().Success, Is.True);
            Run(shop, GrowthContent.DaySeconds + 2, serve: true);
            Assert.That(shop.Day, Is.EqualTo(2));
            Assert.That(shop.SalesTotal, Is.GreaterThanOrEqualTo(1));
            Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.Done));
            Assert.That(shop.GuideNow(), Is.Null);
            Assert.That(shop.TakeEvents().Count(e => e.Kind == GrowthEventKind.Guide && e.Text.StartsWith("That was your first day!")), Is.EqualTo(1), "one note");
            Run(shop, 5);
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Guide), Is.False, "and only once");
        }

        [Test]
        public void ADayWithoutASaleKeepsTheGuideOnAndAsksToOpenAgain()
        {
            var shop = Guided();
            Hatch(shop);
            var tank = shop.Containers[0];
            Assert.That(shop.PutIn(tank.Id).Success && shop.OpenShop().Success, Is.True);
            Run(shop, GrowthContent.DaySeconds + 2); // nobody serves at the counter
            Assert.That(shop.Day, Is.EqualTo(2));
            if (shop.SalesTotal == 0)
            {
                Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.On));
                Assert.That(StepOf(shop), Is.EqualTo(tank.Occupants.Count > 0 ? GrowthGuideStep.Open : GrowthGuideStep.Collect));
            }
        }

        [Test]
        public void StepsAreReadFromTheShopSoAStepDoneEarlyOrUndoneIsRight()
        {
            var shop = Guided();
            Hatch(shop);
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.Stock), "collect, mix and take done: straight to the tank");
            var tank = shop.Containers[0];
            Assert.That(shop.PutIn(tank.Id).Success, Is.True);
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.Open));
            Assert.That(shop.TakeOut(tank.Id, tank.Occupants[0].Id).Success, Is.True);
            Assert.That(StepOf(shop), Is.EqualTo(GrowthGuideStep.Stock), "taken back out: put it in again");
            var two = Guided();
            Assert.That(two.Collect("bloom", two.HoldSeconds).Success && two.Collect("bloom", two.HoldSeconds).Success, Is.True);
            Assert.That(StepOf(two), Is.EqualTo(GrowthGuideStep.Mix), "two doses of any culture are enough to mix");
        }

        [Test]
        public void SkippingStopsTheGuideAndChangesNothingElse()
        {
            var shop = Guided();
            int credits = shop.Credits;
            shop.SkipGuide();
            Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.Skipped));
            Assert.That(shop.GuideNow(), Is.Null);
            Assert.That(shop.Credits, Is.EqualTo(credits));
            var off = new GrowthShop();
            off.SkipGuide();
            Assert.That(off.Guide, Is.EqualTo(GrowthGuideMode.Off), "nothing to skip");
        }

        [Test]
        public void TheGuideIsSavedAndOlderSavesHaveNone()
        {
            Assert.That(GrowthSave.CurrentVersion, Is.GreaterThanOrEqualTo(11), "the guide since version 11 (GR-19 is 12)");
            foreach (var mode in new[] { GrowthGuideMode.Off, GrowthGuideMode.On, GrowthGuideMode.Done, GrowthGuideMode.Skipped })
            {
                var shop = new GrowthShop { Guide = mode };
                var save = shop.Snapshot();
                var restored = GrowthShop.Restore(save, out string problem);
                Assert.That(problem, Is.Null);
                Assert.That(restored.Guide, Is.EqualTo(mode), mode + " round trip");
            }
            var old = Guided().Snapshot();
            old.version = 10; old.guide = null;
            Assert.That(GrowthShop.Restore(old, out _).Guide, Is.EqualTo(GrowthGuideMode.Off), "a version 10 save has no guide");
            var bad = Guided().Snapshot();
            bad.guide = "sideways";
            Assert.That(GrowthShop.Restore(bad, out string why), Is.Null);
            Assert.That(why, Does.Contain("guide"));
        }
    }
}
