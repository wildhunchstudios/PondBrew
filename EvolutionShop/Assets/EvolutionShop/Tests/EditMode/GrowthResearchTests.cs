using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// GR-30 (tester round 1, F1 to F8): the research room. Opened after the generator; a jar for one of each kind, kept and not for
    /// sale; three tanks working through a queue of pairs and mixes on the generator's power; something new leaving one real creature
    /// on the tray, a repeat only saying what it makes; what is tried once never tried twice by accident; and hybrids only from pairs.
    /// </summary>
    public sealed class GrowthResearchTests
    {
        static GrowthShop Ready(int generator = 2, bool room = true)
        {
            var shop = new GrowthShop { Era = 3, Reputation = GrowthContent.StarThresholds[2] + 10, Credits = 50000, Rp = 500 };
            Assert.That(shop.BuyAnnex().Success, Is.True);
            for (int i = 0; i < generator; i++) Assert.That(shop.Upgrade("generator").Success, Is.True);
            if (room) Assert.That(shop.BuyResearchRoom().Success, Is.True);
            return shop;
        }

        static void InJars(GrowthShop shop, params string[] species)
        {
            foreach (var x in species)
            {
                Assert.That(shop.DevelopmentGive(x).Success, Is.True);
                Assert.That(shop.PutSpecimen(shop.Carried.Last().Id).Success, Is.True, x);
            }
        }

        static readonly string[] Founders = GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Founder).Select(s => s.Id).ToArray();

        /// <summary>Two founders whose pair makes a hybrid, for tests that want one pair.</summary>
        static (string a, string b, string child) APair()
        {
            string a = Founders[0], b = Founders[1];
            return (a, b, GrowthRules.Breed(a, b).Id);
        }

        static void Run(GrowthShop shop, float seconds)
        {
            for (float t = 0; t < seconds; t += 0.25f) shop.Tick(0.25f);
        }

        static int[] Doses(params (string culture, int n)[] doses)
        {
            var d = new int[GrowthContent.Cultures.Length];
            foreach (var (culture, n) in doses) d[GrowthContent.CultureIndex(culture)] = n;
            return d;
        }

        // ---------------------------------------------------------------- the room

        [Test]
        public void F1_TheRoomOpensOnlyAfterTheGenerator_AtNight_ForItsPrice()
        {
            var shop = new GrowthShop { Era = 3, Reputation = GrowthContent.StarThresholds[2] + 10, Credits = 50000, Rp = 500 };
            Assert.That(shop.BuyResearchRoom().Failure, Is.EqualTo(GrowthFailure.Locked), "no power room");
            Assert.That(shop.BuyAnnex().Success, Is.True);
            var refused = shop.BuyResearchRoom();
            Assert.That(refused.Failure, Is.EqualTo(GrowthFailure.Locked), "no generator");
            Assert.That(refused.Detail, Does.Contain("generator"));
            Assert.That(shop.Upgrade("generator").Success, Is.True);

            shop.Credits = GrowthShop.ResearchDef.Credits - 1;
            Assert.That(shop.BuyResearchRoom().Failure, Is.EqualTo(GrowthFailure.NotEnoughCredits));
            shop.Credits = GrowthShop.ResearchDef.Credits;
            Assert.That(shop.BuyResearchRoom().Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(0));
            Assert.That(shop.ResearchRoom, Is.True);
            Assert.That(shop.Level("research"), Is.EqualTo(1));
            Assert.That(shop.PowerOrder, Does.Contain(GrowthShop.ResearchPower), "its tanks are on the power board");
        }

        [Test]
        public void F1_TheRoomIsRefusedDuringTheDayAndBeforeItsEraAndStars()
        {
            var early = Ready(room: false);
            early.Era = 2;
            Assert.That(early.ResearchCheck(out _), Is.EqualTo(GrowthFailure.Locked));
            early.Era = 3;
            early.Reputation = 0;
            Assert.That(early.ResearchCheck(out _), Is.EqualTo(GrowthFailure.Locked));

            var day = Ready(room: false);
            day.DevelopmentStock(day.Containers[0].Id, "pondlet");
            Assert.That(day.OpenShop().Success, Is.True);
            Assert.That(day.ResearchCheck(out _), Is.Not.EqualTo(GrowthFailure.NotAtNight), "F75: the room opens by day too");
        }

        [Test]
        public void F1_TheRoomsUpgradesNeedTheRoomAndMakeItQuicker()
        {
            var shop = Ready(room: false);
            Assert.That(shop.Upgrade("research").Failure, Is.EqualTo(GrowthFailure.Locked));
            Assert.That(shop.BuyResearchRoom().Success, Is.True);
            Assert.That(shop.ResearchSeconds, Is.EqualTo(20));
            shop.Era = 4; shop.Reputation = GrowthContent.StarThresholds[3] + 10;
            Assert.That(shop.Upgrade("research").Success, Is.True);
            Assert.That(shop.ResearchSeconds, Is.EqualTo(14));
        }

        // ---------------------------------------------------------------- the jars

        [Test]
        public void F1_AJarHoldsOneOfEachKind_AndTheCreatureIsNotForSale()
        {
            var shop = Ready();
            InJars(shop, Founders[0]);
            Assert.That(shop.Specimens.Single().Species, Is.EqualTo(Founders[0]));
            Assert.That(shop.Carried, Is.Empty);
            Assert.That(shop.Stock.Any(s => s.Species == Founders[0]), Is.False, "a specimen is not on sale");

            Assert.That(shop.DevelopmentGive(Founders[0]).Success, Is.True);
            var second = shop.PutSpecimen();
            Assert.That(second.Failure, Is.EqualTo(GrowthFailure.Owned));
            Assert.That(second.Detail, Does.Contain("One of each"));
            Assert.That(shop.Carried, Has.Count.EqualTo(1), "the second stays in hand");
        }

        [Test]
        public void F1_AClickPutsTheFirstCreatureThatFits()
        {
            var shop = Ready();
            InJars(shop, Founders[0]);
            Assert.That(shop.DevelopmentGive(Founders[0]).Success, Is.True);
            Assert.That(shop.DevelopmentGive(Founders[1]).Success, Is.True);
            Assert.That(shop.PutSpecimen().Success, Is.True);
            Assert.That(shop.SpecimenOf(Founders[1]), Is.Not.Null);
            Assert.That(shop.Carried.Single().Species, Is.EqualTo(Founders[0]));
        }

        [Test]
        public void F1_TheJarsHaveALimit()
        {
            var shop = Ready();
            var kinds = GrowthContent.Species.Select(s => s.Id).Take(GrowthShop.SpecimenJars + 1).ToArray();
            InJars(shop, kinds.Take(GrowthShop.SpecimenJars).ToArray());
            Assert.That(shop.DevelopmentGive(kinds.Last()).Success, Is.True);
            var refused = shop.PutSpecimen();
            Assert.That(refused.Failure, Is.EqualTo(GrowthFailure.Busy));
            Assert.That(refused.Detail, Does.Contain("Every jar"));
        }

        [Test]
        public void F1_TakingACreatureBack_DropsOnlyThePairsWaitingForIt()
        {
            var shop = Ready();
            InJars(shop, Founders[0], Founders[1], Founders[2], Founders[3]);
            Assert.That(shop.QueuePair(Founders[0], Founders[1]).Success, Is.True);
            Assert.That(shop.QueuePair(Founders[2], Founders[3]).Success, Is.True);
            Assert.That(shop.QueuePair(Founders[0], Founders[2]).Success, Is.True);
            var mix = Doses((shop.Cultures[0], 1));
            shop.GrowerUnits[shop.Cultures[0]] = 0;
            Assert.That(shop.QueueMix(mix).Success, Is.True);

            var id = shop.SpecimenOf(Founders[0]).Id;
            Assert.That(shop.TakeSpecimen(id).Success, Is.True);
            Assert.That(shop.Carried.Single().Id, Is.EqualTo(id));
            Assert.That(shop.QueuedPair(Founders[0], Founders[1]), Is.Null);
            Assert.That(shop.QueuedPair(Founders[0], Founders[2]), Is.Null);
            Assert.That(shop.QueuedPair(Founders[2], Founders[3]), Is.Not.Null);
            Assert.That(shop.QueuedMix(mix), Is.Not.Null, "a mix needs no jar");
        }

        [Test]
        public void F1_APairInATankCarriesOnWhenItsCreatureIsTakenBack()
        {
            var shop = Ready();
            var (a, b, child) = APair();
            InJars(shop, a, b);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            shop.Tick(0.25f);
            Assert.That(shop.QueuedPair(a, b).Running, Is.True);
            Assert.That(shop.TakeSpecimen(shop.SpecimenOf(a).Id).Success, Is.True);
            Assert.That(shop.QueuedPair(a, b), Is.Not.Null);
            Run(shop, shop.ResearchSeconds + 1);
            Assert.That(shop.PairEntry(a, b).Species, Is.EqualTo(child));
        }

        [Test]
        public void F1_FullHandsCannotTakeFromAJarOrTheTray()
        {
            var shop = Ready();
            InJars(shop, Founders[0]);
            for (int i = 0; i < GrowthShop.CarryLimit; i++) Assert.That(shop.DevelopmentGive(Founders[1]).Success, Is.True);
            Assert.That(shop.TakeSpecimen(shop.Specimens[0].Id).Failure, Is.EqualTo(GrowthFailure.HandsFull));
            Assert.That(shop.Specimens, Has.Count.EqualTo(1));
        }

        // ---------------------------------------------------------------- the queue

        [Test]
        public void F4_ThreeTanksWorkAtOnce_TheRestWait_AndEachDrawsAUnit()
        {
            var shop = Ready();
            InJars(shop, Founders[0], Founders[1], Founders[2], Founders[3]);
            Assert.That(int.Parse(shop.QueueAllPairs().Detail), Is.EqualTo(6));
            shop.Tick(0.25f);
            Assert.That(shop.ResearchRunning.Count(), Is.EqualTo(3));
            Assert.That(shop.ResearchWaiting.Count(), Is.EqualTo(3));
            Assert.That(shop.PowerUse(GrowthShop.ResearchPower).Draw, Is.EqualTo(3 * GrowthShop.ResearchTankUnits));
            Assert.That(shop.ResearchRunning.Select(j => j.Tank).OrderBy(t => t), Is.EqualTo(new[] { 0, 1, 2 }));

            Run(shop, shop.ResearchSeconds * 2 + 2);
            Assert.That(shop.ResearchJobs, Is.Empty, "the queue works through itself");
            Assert.That(shop.ResearchDone, Is.EqualTo(6));
            Assert.That(shop.PowerUse(GrowthShop.ResearchPower).Draw, Is.EqualTo(0), "nothing drawn with nothing to do");
        }

        [Test]
        public void F4_APairTakesAbout20Seconds_OnFullPower()
        {
            var shop = Ready();
            var (a, b, _) = APair();
            InJars(shop, a, b);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Run(shop, 19);
            Assert.That(shop.PairEntry(a, b), Is.Null);
            Run(shop, 2);
            Assert.That(shop.PairEntry(a, b), Is.Not.Null);
        }

        [Test]
        public void F4_ShortOfPowerItSlows_WithoutPowerItStops_AndItSaysSo()
        {
            var shop = Ready(generator: 1);
            var (a, b, _) = APair();
            InJars(shop, a, b);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Run(shop, 30);
            Assert.That(shop.PairEntry(a, b), Is.Null, "the hand crank gives nothing until it is cranked");
            Assert.That(shop.QueuedPair(a, b).Left, Is.EqualTo(shop.ResearchSeconds));
            Assert.That(shop.ResearchStatus.ToLowerInvariant(), Does.Contain("power"));

            // Three tanks on the crank's one unit: each at a third of its speed.
            var slow = Ready(generator: 1);
            InJars(slow, Founders[0], Founders[1], Founders[2], Founders[3]);
            Assert.That(slow.QueueAllPairs().Success, Is.True);
            slow.Tick(0.25f);
            for (int i = 0; i < 12; i++) { slow.CrankGenerator(); slow.Tick(0.25f); }
            Assert.That(slow.PowerUse(GrowthShop.ResearchPower).Got, Is.EqualTo(1).Within(0.01f));
            Assert.That(slow.ResearchInTank(0).Left, Is.EqualTo(slow.ResearchSeconds - 1).Within(0.2f));
            Assert.That(slow.ResearchStatus.ToLowerInvariant(), Does.Contain("power"));
        }

        [Test]
        public void F4_TheTanksWorkByDayAsWellAsAtNight()
        {
            var shop = Ready();
            var (a, b, _) = APair();
            InJars(shop, a, b);
            shop.DevelopmentStock(shop.Containers[0].Id, "pondlet");
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Assert.That(shop.OpenShop().Success, Is.True);
            Run(shop, 21);
            Assert.That(shop.PairEntry(a, b), Is.Not.Null);
        }

        [Test]
        public void F5_HowToMakeACreature_ARecipeOrThePairThatMadeIt()
        {
            var shop = Ready();
            var founder = GrowthContent.Species.First(s => s.Kind == SpeciesKind.Founder);
            Assert.That(shop.HowToMake(founder), Is.EqualTo(GrowthReport.Recipe(founder.Recipe)));
            var (a, b, child) = APair();
            Assert.That(shop.HowToMake(GrowthContent.Get(child)), Is.EqualTo("bred"), "no pair known yet");
            InJars(shop, a, b);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Run(shop, 21);
            Assert.That(shop.HowToMake(GrowthContent.Get(child)), Does.Contain(GrowthContent.Get(a).Name).And.Contain(GrowthContent.Get(b).Name));
        }

        [Test]
        public void F4_APairCostsNoCreditsAndNoDoses()
        {
            var shop = Ready();
            var (a, b, _) = APair();
            InJars(shop, a, b);
            int credits = shop.Credits;
            var units = shop.GrowerUnits.ToDictionary(k => k.Key, k => k.Value);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Run(shop, 21);
            Assert.That(shop.Credits, Is.EqualTo(credits));
            foreach (var kv in units) Assert.That(shop.GrowerUnits[kv.Key], Is.GreaterThanOrEqualTo(kv.Value));
            Assert.That(shop.Specimens, Has.Count.EqualTo(2), "the creatures are not used up");
        }

        [Test]
        public void F7_ATriedPairIsRefused_NamingWhatItMakes_InEitherOrder()
        {
            var shop = Ready();
            var (a, b, child) = APair();
            InJars(shop, a, b);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Assert.That(shop.QueuePair(b, a).Failure, Is.EqualTo(GrowthFailure.Busy), "queued already, either way round");
            Run(shop, 21);
            var again = shop.QueuePair(b, a);
            Assert.That(again.Failure, Is.EqualTo(GrowthFailure.AlreadyTried));
            Assert.That(again.Detail, Does.Contain("Already tried").And.Contain(GrowthContent.Get(child).Name));
        }

        [Test]
        public void F7_APairTriedAtTheBreedingTankCountsAsTried()
        {
            var shop = Ready();
            var (a, b, _) = APair();
            InJars(shop, a, b);
            shop.Levels["breeding"] = 1;
            Assert.That(shop.DevelopmentGive(a).Success, Is.True);
            Assert.That(shop.DevelopmentGive(b).Success, Is.True);
            Assert.That(shop.PutParent().Success, Is.True);
            Assert.That(shop.PutParent().Success, Is.True);
            Run(shop, shop.BreedSeconds + 1);
            Assert.That(shop.QueuePair(a, b).Failure, Is.EqualTo(GrowthFailure.AlreadyTried));
        }

        [Test]
        public void F4_QueuingNeedsBothJars_AndTwoDifferentKinds_AndAWaitingJobCanBeTakenOut()
        {
            var shop = Ready();
            InJars(shop, Founders[0]);
            Assert.That(shop.QueuePair(Founders[0], Founders[1]).Failure, Is.EqualTo(GrowthFailure.NoSuchSpecimen));
            Assert.That(shop.QueuePair(Founders[0], Founders[0]).Failure, Is.EqualTo(GrowthFailure.SameWater));
            Assert.That(shop.QueueAllPairs().Failure, Is.EqualTo(GrowthFailure.AlreadyTried), "one jar has no pairs");

            InJars(shop, Founders[1]);
            Assert.That(shop.QueuePair(Founders[0], Founders[1]).Success, Is.True);
            var job = shop.QueuedPair(Founders[0], Founders[1]);
            Assert.That(shop.Unqueue(job).Success, Is.True);
            Assert.That(shop.ResearchJobs, Is.Empty);
        }

        [Test]
        public void F4_AJobInATankCannotBeTakenOut()
        {
            var shop = Ready();
            InJars(shop, Founders[0], Founders[1]);
            Assert.That(shop.QueuePair(Founders[0], Founders[1]).Success, Is.True);
            shop.Tick(0.25f);
            Assert.That(shop.Unqueue(shop.QueuedPair(Founders[0], Founders[1])).Failure, Is.EqualTo(GrowthFailure.Busy));
        }

        // ---------------------------------------------------------------- results

        [Test]
        public void F1_SomethingNew_GoesInTheJournal_AndLeavesOneRealCreatureOnTheTray()
        {
            var shop = Ready();
            var (a, b, child) = APair();
            InJars(shop, a, b);
            shop.Discovered.Remove(child);
            int rp = shop.Rp;
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            shop.TakeEvents();
            Run(shop, 21);
            Assert.That(shop.Discovered, Does.Contain(child));
            Assert.That(shop.Rp, Is.GreaterThan(rp));
            Assert.That(shop.ResearchTray.Single().Species, Is.EqualTo(child));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Discovery), Is.True);
            Assert.That(shop.PairEntry(a, b).WasNew, Is.True);

            Assert.That(shop.TakeResearchResult().Success, Is.True);
            Assert.That(shop.Carried.Single().Species, Is.EqualTo(child));
            Assert.That(shop.ResearchTray, Is.Empty);
        }

        [Test]
        public void F1_ARepeatOnlySaysWhatItMakes_AndGivesNoCreature()
        {
            var shop = Ready();
            var (a, b, child) = APair();
            InJars(shop, a, b);
            shop.Discovered.Add(child);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            shop.TakeEvents();
            Run(shop, 21);
            Assert.That(shop.ResearchTray, Is.Empty);
            var note = shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Researched);
            Assert.That(note.Text, Does.Contain(GrowthContent.Get(child).Name));
            Assert.That(shop.PairEntry(a, b).WasNew, Is.False);
        }

        [Test]
        public void F1_TwoTanksFindingOneThing_LeaveOneCreature()
        {
            var shop = Ready();
            string[] kinds = Founders;
            string a = null, b = null, c = null, d = null;
            foreach (var x in kinds) foreach (var y in kinds) foreach (var z in kinds) foreach (var w in kinds)
                if (a == null && x != y && z != w && !(x == z && y == w) && !(x == w && y == z) && GrowthRules.Breed(x, y).Id == GrowthRules.Breed(z, w).Id) { a = x; b = y; c = z; d = w; }
            Assert.That(a, Is.Not.Null, "two pairs that make one thing");
            string child = GrowthRules.Breed(a, b).Id;
            InJars(shop, new[] { a, b, c, d }.Distinct().ToArray());
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Assert.That(shop.QueuePair(c, d).Success, Is.True);
            Run(shop, 21);
            Assert.That(shop.ResearchTray.Count(x => x.Species == child), Is.EqualTo(1));
            Assert.That(shop.ResearchDone, Is.EqualTo(2));
        }

        [Test]
        public void F1_WithTheTrayFull_ANewCreatureWaitsInItsTank_DrawingNothing_AndTheQueueGoesOn()
        {
            var shop = Ready();
            var (a, b, child) = APair();
            InJars(shop, a, b);
            shop.Discovered.Remove(child);
            for (int i = 0; i < GrowthShop.ResearchTrayPlaces; i++) shop.ResearchTray.Add(new GrowthSpecimen { Id = 7000 + i, Species = Founders[0] });
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            Run(shop, 21);
            Assert.That(shop.Discovered, Does.Not.Contain(child), "not discovered until it comes out");
            Assert.That(shop.ResearchRunning.Single().Left, Is.EqualTo(0));
            Assert.That(shop.PowerUse(GrowthShop.ResearchPower).Draw, Is.EqualTo(0));
            Assert.That(shop.ResearchStatus, Does.Contain("tray"));
            Assert.That(shop.ResearchTankLine(0), Does.Contain("tray"));

            Assert.That(shop.TakeResearchResult().Success, Is.True);
            shop.Tick(0.25f);
            Assert.That(shop.Discovered, Does.Contain(child));
            Assert.That(shop.ResearchTray.Last().Species, Is.EqualTo(child));
            Assert.That(shop.ResearchJobs, Is.Empty);
        }

        // ---------------------------------------------------------------- mixes

        [Test]
        public void F2_AMixRunsInTheBackground_TakesItsDosesFromTheGrower_NeverTheSiphon_AndAFindGoesOnTheTray()
        {
            var shop = Ready();
            string culture = shop.Cultures[0];
            var founder = GrowthContent.Species.First(s => s.Kind == SpeciesKind.Founder && s.Recipe[GrowthContent.CultureIndex(culture)] > 0 && Enumerable.Range(0, s.Recipe.Length).All(i => s.Recipe[i] == 0 || shop.Owns(GrowthContent.Cultures[i].Id)));
            foreach (var c in shop.Cultures) shop.GrowerUnits[c] = 3;
            shop.Discovered.Remove(founder.Id);
            shop.Held.Clear(); shop.Held.Add(culture);
            Assert.That(shop.QueueMix(founder.Recipe).Success, Is.True);
            shop.Tick(0.1f);
            Assert.That(shop.QueuedMix(founder.Recipe).Running, Is.True);
            Assert.That(shop.GrowerUnits[culture], Is.LessThanOrEqualTo(3 - founder.Recipe[GrowthContent.CultureIndex(culture)] + 0.5f), "doses taken as it starts");
            Assert.That(shop.Held, Has.Count.EqualTo(1), "the siphon is the player's");
            Run(shop, 21);
            Assert.That(shop.Discovered, Does.Contain(founder.Id));
            Assert.That(shop.ResearchTray.Single().Species, Is.EqualTo(founder.Id));
            Assert.That(shop.MixEntry(founder.Recipe).Species, Is.EqualTo(founder.Id));
            Assert.That(shop.MixEntry(founder.Recipe).Exact, Is.True);
        }

        [Test]
        public void F2_AMixTakesDosesFromABufferFirst()
        {
            var shop = Ready();
            string culture = shop.Cultures[0];
            shop.Buffers.Add(new GrowthBuffer { Place = 0, Culture = culture, Units = 2 });
            shop.GrowerUnits[culture] = 3;
            Assert.That(shop.QueueMix(Doses((culture, 3))).Success, Is.True);
            shop.Tick(0.05f);
            Assert.That(shop.Buffers[0].Units, Is.EqualTo(0).Within(0.01f));
            Assert.That(shop.GrowerUnits[culture], Is.LessThan(2.5f).And.GreaterThan(1.9f), "the third from the grower");
        }

        [Test]
        public void F2_AMixWithoutItsDosesWaits_LettingTheNextGoFirst_ThenRunsWhenTheyAreThere()
        {
            var shop = Ready();
            var (a, b, _) = APair();
            InJars(shop, a, b);
            string culture = shop.Cultures[0];
            shop.GrowerUnits[culture] = 0;
            var mix = Doses((culture, 2));
            Assert.That(shop.QueueMix(mix).Success, Is.True);
            Assert.That(shop.QueuePair(a, b).Success, Is.True);
            shop.Tick(0.05f);
            Assert.That(shop.QueuedMix(mix).Running, Is.False);
            Assert.That(shop.QueuedPair(a, b).Running, Is.True);

            shop.GrowerUnits[culture] = 3;
            shop.Tick(0.05f);
            Assert.That(shop.QueuedMix(mix).Running, Is.True);
        }

        [Test]
        public void F7_ATriedMixIsRefused_NamingWhatItMakes_WhereverItWasTried()
        {
            var shop = Ready();
            string culture = shop.Cultures[0];
            var mix = Doses((culture, 1));
            Assert.That(shop.QueueMix(mix).Success, Is.True);
            Assert.That(shop.QueueMix(mix).Failure, Is.EqualTo(GrowthFailure.Busy));
            Run(shop, 21);
            string made = GrowthContent.Get(shop.MixEntry(mix).Species).Name;
            var refused = shop.QueueMix(mix);
            Assert.That(refused.Failure, Is.EqualTo(GrowthFailure.AlreadyTried));
            Assert.That(refused.Detail, Does.Contain("Already tried").And.Contain(made));
        }

        [Test]
        public void F2_AMixMustBeWithinTheIncubatorsDoses_AndOfCulturesYouOwn()
        {
            var shop = Ready();
            string culture = shop.Cultures[0];
            Assert.That(shop.QueueMix(Doses()).Failure, Is.EqualTo(GrowthFailure.ChamberEmpty));
            Assert.That(shop.QueueMix(Doses((culture, shop.MaxDoses + 1))).Failure, Is.EqualTo(GrowthFailure.ChamberFull));
            string notOwned = GrowthContent.Cultures.Select(c => c.Id).First(c => !shop.Owns(c));
            Assert.That(shop.QueueMix(Doses((notOwned, 1))).Failure, Is.EqualTo(GrowthFailure.NotOwned));
        }

        // ---------------------------------------------------------------- the microscope and the planner

        [Test]
        public void F3_TheMicroscopeMarksPairsThatWouldBeNew_AndQueueAllTakesThemFirst()
        {
            var shop = Ready();
            InJars(shop, Founders[0], Founders[1], Founders[2]);
            var pairs = new[] { (Founders[0], Founders[1]), (Founders[0], Founders[2]), (Founders[1], Founders[2]) };
            string newChild = GrowthRules.Breed(pairs[2].Item1, pairs[2].Item2).Id;
            foreach (var p in pairs) shop.Discovered.Add(GrowthRules.Breed(p.Item1, p.Item2).Id);
            shop.Discovered.Remove(newChild);

            shop.Levels["bench"] = 1;
            Assert.That(shop.PairLooksNew(pairs[2].Item1, pairs[2].Item2), Is.False, "no microscope, no hint");

            shop.Levels["bench"] = 2;
            Assert.That(shop.PairLooksNew(pairs[2].Item1, pairs[2].Item2), Is.True);
            foreach (var p in pairs.Where(p => GrowthRules.Breed(p.Item1, p.Item2).Id != newChild))
                Assert.That(shop.PairLooksNew(p.Item1, p.Item2), Is.False);
            var first = shop.PairsLeft().First();
            Assert.That(GrowthRules.Breed(first.a, first.b).Id, Is.EqualTo(newChild));
        }

        // ---------------------------------------------------------------- breeding (F8)

        [Test]
        public void F8_TwoDifferentSpeciesOnlyEverBreedAHybrid_OrTheirCrown()
        {
            foreach (var a in GrowthContent.Species)
                foreach (var b in GrowthContent.Species)
                {
                    if (a.Id == b.Id) continue;
                    var child = GrowthRules.Breed(a.Id, b.Id);
                    Assert.That(child.Kind, Is.Not.EqualTo(SpeciesKind.Founder), a.Id + " x " + b.Id);
                    if (child.Kind == SpeciesKind.Crown) Assert.That(new[] { child.PairA, child.PairB }, Is.EquivalentTo(new[] { a.Id, b.Id }));
                }
        }

        [Test]
        public void F8_TwoOfOneKindStillBreedTrue_AndEveryHybridCanBeBred()
        {
            foreach (var a in GrowthContent.Species.Where(s => s.Kind != SpeciesKind.Crown))
                Assert.That(GrowthRules.Breed(a.Id, a.Id).Id, Is.EqualTo(a.Id));
            var made = (from a in GrowthContent.Species from b in GrowthContent.Species where a.Id != b.Id select GrowthRules.Breed(a.Id, b.Id).Id).Distinct().ToList();
            foreach (var h in GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Hybrid))
                Assert.That(made, Does.Contain(h.Id), h.Id);
        }

        [Test]
        public void F1_TheResearchStartPoint_IsReadyToOpenTheRoom()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Research);
            Assert.That(shop.Annex && shop.HasGenerator && !shop.ResearchRoom, Is.True);
            Assert.That(shop.ResearchCheck(out string why), Is.EqualTo(GrowthFailure.None), why);
            Assert.That(shop.BuyResearchRoom().Success, Is.True);
            Assert.That(GrowthShop.Restore(UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot())), out string problem), Is.Not.Null, problem);
        }

        // ---------------------------------------------------------------- saving

        [Test]
        public void F1_TheRoomSavesMidWay_WithoutADoseOrACreatureTwice()
        {
            var shop = Ready();
            InJars(shop, Founders[0], Founders[1], Founders[2], Founders[3]);
            string culture = shop.Cultures[0];
            shop.GrowerUnits[culture] = 3;
            Assert.That(shop.QueueMix(Doses((culture, 2))).Success, Is.True);
            Assert.That(shop.QueueAllPairs().Success, Is.True);
            Run(shop, 5);
            shop.ResearchTray.Add(new GrowthSpecimen { Id = 9000, Species = Founders[0] });
            float units = shop.GrowerUnits[culture];

            var back = GrowthShop.Restore(UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot())), out string problem);
            Assert.That(problem, Is.Null.Or.Empty);
            Assert.That(back.ResearchRoom, Is.True);
            Assert.That(back.Level("research"), Is.EqualTo(1));
            Assert.That(back.Specimens.Select(x => x.Id), Is.EqualTo(shop.Specimens.Select(x => x.Id)));
            Assert.That(back.ResearchTray.Single().Id, Is.EqualTo(9000));
            Assert.That(back.ResearchJobs, Has.Count.EqualTo(shop.ResearchJobs.Count));
            Assert.That(back.ResearchRunning.Count(), Is.EqualTo(3));
            Assert.That(back.ResearchInTank(0).Left, Is.EqualTo(shop.ResearchInTank(0).Left).Within(0.01f));
            Assert.That(back.GrowerUnits[culture], Is.EqualTo(units).Within(0.01f), "a mix under way does not take its doses again");
            Assert.That(back.QueuedMix(Doses((culture, 2))).IsMix, Is.True);
            Assert.That(back.PowerOrder, Does.Contain(GrowthShop.ResearchPower));
            back.Tick(0.1f);
            Assert.That(back.GrowerUnits[culture], Is.GreaterThanOrEqualTo(units - 0.01f));
        }

        [Test]
        public void F1_ASaveThatCannotBeIsRefused()
        {
            string Problem(System.Action<GrowthSave> change)
            {
                var shop = Ready();
                InJars(shop, Founders[0], Founders[1]);
                var save = UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot()));
                change(save);
                GrowthShop.Restore(save, out string problem);
                return problem;
            }
            Assert.That(Problem(s => { }), Is.Null.Or.Empty);
            Assert.That(Problem(s => s.researchRoom = false), Is.Not.Null.And.Not.Empty);
            Assert.That(Problem(s => s.specimens[1].species = s.specimens[0].species), Does.Contain("jars"));
            Assert.That(Problem(s => s.specimens[0].species = "nothing"), Is.Not.Null.And.Not.Empty);
            Assert.That(Problem(s => s.researchJobs = new[] { new GrowthSave.ResearchJob { a = Founders[0], b = Founders[2], tank = -1, left = 5 } }), Does.Contain("jars"));
            Assert.That(Problem(s => s.researchJobs = new[] { new GrowthSave.ResearchJob { a = Founders[0], b = Founders[1], tank = 3, left = 5 } }), Does.Contain("tank"));
            Assert.That(Problem(s => s.researchJobs = new[] { new GrowthSave.ResearchJob { a = Founders[0], b = Founders[1], tank = 0, left = 5 }, new GrowthSave.ResearchJob { a = Founders[0], b = Founders[2], tank = 0, left = 5 } }), Does.Contain("tank"));
            Assert.That(Problem(s => s.researchJobs = new[] { new GrowthSave.ResearchJob { mix = true, doses = new int[2], tank = -1, left = 5 } }), Does.Contain("mix"));
            Assert.That(Problem(s => s.researchJobs = new[] { new GrowthSave.ResearchJob { a = Founders[0], b = Founders[1], tank = -1, left = -1 } }), Does.Contain("time"));
        }

        [Test]
        public void F8_ASaveFromBefore_ForgetsWhatAPairNoLongerMakes_AndThePairCanBeTriedAgain()
        {
            var shop = Ready();
            var (a, b, child) = APair();
            string c = Founders[2], d = Founders[3];
            InJars(shop, c, d);
            shop.PairLog.Add(new GrowthPairEntry { A = string.CompareOrdinal(a, b) <= 0 ? a : b, B = string.CompareOrdinal(a, b) <= 0 ? b : a, Species = child });
            shop.PairLog.Add(new GrowthPairEntry { A = string.CompareOrdinal(c, d) <= 0 ? c : d, B = string.CompareOrdinal(c, d) <= 0 ? d : c, Species = Founders[0] });
            var save = UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot()));
            save.version = 14;
            var back = GrowthShop.Restore(save, out string problem);
            Assert.That(problem, Is.Null.Or.Empty);
            Assert.That(back.PairEntry(a, b).Species, Is.EqualTo(child), "what is still true stays");
            Assert.That(back.PairEntry(c, d), Is.Null, "a founder from a pair is forgotten");
            Assert.That(back.QueuePair(c, d).Success, Is.True, "and the pair can be tried");

            save = UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot()));
            Assert.That(GrowthShop.Restore(save, out problem).PairEntry(c, d), Is.Not.Null, "this version's save keeps its log as it is");
        }

        [Test]
        public void F1_ASaveFromBeforeTheRoomLoadsWithoutIt()
        {
            var shop = new GrowthShop();
            var json = UnityEngine.JsonUtility.ToJson(shop.Snapshot());
            var save = UnityEngine.JsonUtility.FromJson<GrowthSave>(json);
            save.version = 14;
            var back = GrowthShop.Restore(save, out string problem);
            Assert.That(problem, Is.Null.Or.Empty);
            Assert.That(back.ResearchRoom, Is.False);
            Assert.That(back.Specimens, Is.Empty);
        }
    }
}
