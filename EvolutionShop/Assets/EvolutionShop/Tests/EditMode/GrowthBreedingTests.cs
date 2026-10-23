using System;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-05: the breeding tank and GR.7.2's breeding: every pair's offspring through the tank, the nursery that
    /// waits, colour by tint or the higher parent, the pair log, research points for every first species and colour, the
    /// era 3 gate and the version 4 save (GR.7.2, GR.15.1).</summary>
    public sealed class GrowthBreedingTests
    {
        static GrowthShop At(int era, int stars, int credits = 5000, int rp = 0, int breeding = 0)
        {
            var shop = new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 10, Credits = credits, Rp = rp };
            if (breeding > 0) shop.Levels["breeding"] = breeding;
            return shop;
        }

        static void Pair(GrowthShop shop, string a, string b)
        {
            Assert.That(shop.DevelopmentGive(a).Success && shop.DevelopmentGive(b).Success, Is.True, "give " + a + " and " + b);
            Assert.That(shop.PutParent().Success, Is.True, "put " + a + " in");
            Assert.That(shop.PutParent().Success, Is.True, "put " + b + " in");
        }

        static GrowthSpecimen BreedOnce(GrowthShop shop)
        {
            Assert.That(shop.Breeding, Is.True, "a pair is breeding");
            shop.Tick(shop.BreedingLeft + 0.01f);
            Assert.That(shop.Offspring, Is.Not.Null, "an offspring in the nursery");
            return shop.Offspring;
        }

        /// <summary>GR.7.2's rule, written out on its own: a special pair's crown, a same-species pair true, else the
        /// non-crown species whose breeding value is nearest the parents' average rounded up (the lower value on a tie).</summary>
        static SpeciesDef Expected(SpeciesDef a, SpeciesDef b)
        {
            var crown = GrowthContent.Species.FirstOrDefault(s => s.Kind == SpeciesKind.Crown && new[] { s.PairA, s.PairB }.OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x, StringComparer.Ordinal)));
            if (crown != null) return crown;
            if (a == b) return a;
            int target = (int)Math.Ceiling((a.BreedingValue + b.BreedingValue) / 2.0);
            return GrowthContent.Species.Where(s => s.Kind != SpeciesKind.Crown).OrderBy(s => Math.Abs(s.BreedingValue - target)).ThenBy(s => s.BreedingValue).First();
        }

        [Test]
        public void TheBreedingTankIsBoughtInEraTwoWithCreditsAndResearchPoints()
        {
            var shop = At(era: 1, stars: 1, credits: 500, rp: 20);
            Assert.That(shop.HasBreedingTank, Is.False);
            Assert.That(shop.Level("breeding"), Is.Zero, "not bought at the start");
            Assert.That(shop.UpgradeCheck("breeding", out var first, out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs era 2"));
            Assert.That((first.Credits, first.Rp, first.Era, first.Stars), Is.EqualTo((60, 5, 2, 2)), "60 credits and 5 RP, era 2, two stars");
            shop = At(era: 2, stars: 2, credits: 500, rp: 4);
            Assert.That(shop.UpgradeCheck("breeding", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 5 research points"));
            shop.Rp = 20;
            Assert.That(shop.Upgrade("breeding").Success, Is.True);
            Assert.That((shop.Level("breeding"), shop.Credits, shop.Rp), Is.EqualTo((1, 440, 15)));
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Upgraded).Text, Does.StartWith("Breeding tank bought"));
            Assert.That(shop.BreedSeconds, Is.EqualTo(60f));
            Assert.That(shop.LargeParents || shop.TintPort, Is.False, "level 1: one pair, no tint port, no Large parents");
            Assert.That(shop.Upgrade("breeding").Success, Is.True, "level 2 for 200 credits");
            Assert.That((shop.Level("breeding"), shop.Credits, shop.BreedSeconds, shop.LargeParents, shop.TintPort), Is.EqualTo((2, 240, 40f, true, true)));
            Assert.That(shop.UpgradeCheck("breeding", out _, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs era 4"), "GR-06: its second pair (level 3) comes in era 4");
            Assert.That(GrowthShop.GreyboxStations, Does.Not.Contain("breeding"), "the stations a shop starts with are unchanged");
        }

        [Test]
        public void APairBreedsIntoTheNurseryWhileTheParentsStay()
        {
            var shop = At(era: 2, stars: 2, breeding: 1);
            Assert.That(shop.DevelopmentGive("pondlet").Success, Is.True);
            Assert.That(shop.PutParent().Success, Is.True);
            Assert.That(shop.Breeding, Is.False, "one parent does not breed");
            shop.Tick(500);
            Assert.That(shop.Offspring, Is.Null);
            Assert.That(shop.DevelopmentGive("bubblebelly").Success && shop.PutParent().Success, Is.True);
            Assert.That((shop.Breeding, shop.BreedingLeft), Is.EqualTo((true, 60f)), "a pair starts the 60 s timer");
            Assert.That(shop.DevelopmentGive("ribbonfin").Success, Is.True);
            var third = shop.PutParent();
            Assert.That((third.Failure, third.Detail), Is.EqualTo((GrowthFailure.ChamberFull, "The breeding tank takes one pair")));
            shop.Carried.Clear();
            shop.Tick(59.9f);
            Assert.That(shop.Offspring, Is.Null);
            shop.Tick(0.2f);
            Assert.That(shop.Offspring.Species, Is.EqualTo("puddlepup"), "bubblebelly x pondlet");
            Assert.That(shop.Offspring.Colour, Is.EqualTo(Colour.Classic));
            Assert.That(shop.Parents.Select(p => p.Species), Is.EquivalentTo(new[] { "pondlet", "bubblebelly" }), "the parents stay");
            // Parents and offspring are not stock: visitors never buy them (GR.15.1).
            Assert.That(shop.Stock.Any(s => shop.Parents.Contains(s) || s == shop.Offspring), Is.False);
            // GR.15.1: a result with nowhere to go waits; the timer does not run while the nursery is full.
            shop.Tick(300);
            Assert.That((shop.Breeding, shop.BreedingLeft, shop.PairLog.Single().Count), Is.EqualTo((false, 0f, 1)));
            int id = shop.Offspring.Id;
            Assert.That(shop.TakeOffspring().Success, Is.True);
            Assert.That(shop.Carried.Single().Id, Is.EqualTo(id));
            Assert.That(shop.BreedingLeft, Is.EqualTo(60f), "the pair starts on the next one");
            // Taking a parent out at any time stops the timer; a pair back in starts it from the beginning.
            shop.Tick(30);
            var pondlet = shop.Parents.First(p => p.Species == "pondlet");
            Assert.That(shop.TakeParent(pondlet.Id).Success, Is.True);
            Assert.That((shop.Breeding, shop.BreedingLeft), Is.EqualTo((false, 0f)));
            Assert.That(shop.PutParent(pondlet.Id).Success, Is.True);
            Assert.That(shop.BreedingLeft, Is.EqualTo(60f));
            // A full pair of hands keeps the offspring in the nursery.
            BreedOnce(shop);
            while (shop.Carried.Count < GrowthShop.CarryLimit) shop.DevelopmentGive("pondlet");
            Assert.That(shop.TakeOffspring().Failure, Is.EqualTo(GrowthFailure.HandsFull));
            Assert.That(shop.TakeParent(shop.Parents[0].Id).Failure, Is.EqualTo(GrowthFailure.HandsFull));
            Assert.That(new GrowthShop().TakeOffspring().Failure, Is.EqualTo(GrowthFailure.NoOffspring));
        }

        [Test]
        public void EveryPairResolvesThroughTheBreedingTank()
        {
            var species = GrowthContent.Species;
            int pairs = 0, crowns = 0, same = 0;
            for (int i = 0; i < species.Length; i++)
                for (int j = i; j < species.Length; j++)
                {
                    var a = species[i]; var b = species[j];
                    var shop = At(era: 5, stars: 5, breeding: 2);
                    Pair(shop, a.Id, b.Id);
                    var child = BreedOnce(shop);
                    var expected = Expected(a, b);
                    Assert.That(child.Species, Is.EqualTo(expected.Id), a.Id + " x " + b.Id);
                    Assert.That(child.Species, Is.EqualTo(GrowthRules.Breed(b.Id, a.Id).Id), "either way round");
                    Assert.That(child.Colour, Is.EqualTo(Colour.Classic));
                    Assert.That(shop.PairEntry(b.Id, a.Id).Species, Is.EqualTo(expected.Id), "the pair log knows it");
                    Assert.That(shop.Discovered, Does.Contain(expected.Id), "a first offspring is a discovery");
                    pairs++;
                    if (expected.Kind == SpeciesKind.Crown) crowns++;
                    if (a == b) same++;
                }
            Assert.That(pairs, Is.EqualTo(2628), "72 x 73 / 2 pairs, 72 of them two of one species (GR.7.2)");
            Assert.That(same, Is.EqualTo(72));
            Assert.That(crowns, Is.EqualTo(9 + 9), "nine special pairs, and each crown bred with itself");
        }

        [Test]
        public void ColourComesFromTheTintOrTheHigherParentColour()
        {
            var shop = At(era: 2, stars: 2, breeding: 1);
            shop.Held.Add("bloom");
            Assert.That(shop.TintCheck("bloom", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Tinting needs the breeding tank's level 2"));
            shop.Levels["breeding"] = 2;
            Assert.That(shop.TintCheck("ember", out why), Is.EqualTo(GrowthFailure.UnknownCulture));
            Assert.That(shop.TintCheck("flow", out why), Is.EqualTo(GrowthFailure.NoDose), "a dose from the siphon");
            Assert.That(shop.TintWater("bloom").Success, Is.True);
            Assert.That((shop.Tint, shop.Held.Count), Is.EqualTo(("bloom", 0)));
            shop.Held.Add("flow");
            Assert.That(shop.TintCheck("flow", out why), Is.EqualTo(GrowthFailure.Busy), "one tint at a time");
            Assert.That(shop.ClearTint().Success && shop.Held.Count(h => h == "bloom") == 1 && shop.Tint == null, Is.True, "an unused tint comes back");
            Assert.That(shop.ClearTint().Failure, Is.EqualTo(GrowthFailure.NotTinted));
            Assert.That(shop.TintWater("bloom").Success, Is.True);
            Pair(shop, "pondlet", "pondlet");
            Assert.That(shop.NextColour, Is.EqualTo(Colour.Sunny), "the card can say the next colour");
            var sunny = BreedOnce(shop);
            Assert.That((sunny.Species, sunny.Colour, shop.Tint), Is.EqualTo(("pondlet", Colour.Sunny, (string)null)), "Bloom: Sunny; the tint is used up");
            Assert.That(shop.TakeOffspring().Success, Is.True);
            // Untinted, the offspring takes the higher-order parent colour (Classic < Sunny < Deep < Shimmer).
            var classic = shop.Parents[0];
            Assert.That(shop.TakeParent(classic.Id).Success && shop.PutParent(sunny.Id).Success, Is.True);
            Assert.That(BreedOnce(shop).Colour, Is.EqualTo(Colour.Sunny), "Sunny x Classic: Sunny");
            Assert.That(shop.TakeOffspring().Success, Is.True);
            Assert.That(shop.TintWater("flow").Success, Is.True);
            Assert.That(BreedOnce(shop).Colour, Is.EqualTo(Colour.Deep), "Flow: Deep");
            var deep = shop.Offspring; Assert.That(shop.TakeOffspring().Success, Is.True);
            shop.Carried.RemoveAll(c => c != deep);
            shop.Held.Add("mist");
            Assert.That(shop.TintWater("mist").Success, Is.True);
            Assert.That(BreedOnce(shop).Colour, Is.EqualTo(Colour.Shimmer), "Mist: Shimmer");
            Assert.That(shop.TakeOffspring().Success, Is.True);
            shop.Carried.RemoveAll(c => c != deep);
            Assert.That(shop.TakeParent(shop.Parents[0].Id).Success && shop.PutParent(deep.Id).Success, Is.True);
            Assert.That(shop.NextColour, Is.EqualTo(Colour.Deep), "Sunny x Deep, untinted: Deep");
            // Founders hatch Classic (the incubator's tint port comes with its level 3).
            Assert.That(shop.Journal, Is.SupersetOf(new[] { "pondlet/Sunny", "pondlet/Deep", "pondlet/Shimmer" }));
        }

        [Test]
        public void ResearchPointsComeFromEveryFirstSpeciesAndColour()
        {
            var shop = At(era: 2, stars: 2, breeding: 2);
            shop.Discovered.AddRange(new[] { "bubblebelly", "ribbonfin" });
            shop.Journal.AddRange(shop.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            Pair(shop, "bubblebelly", "ribbonfin");
            BreedOnce(shop);
            Assert.That((shop.Offspring.Species, shop.Rp), Is.EqualTo(("pondlet", GrowthContent.RpFounder)), "a pair can make a founder first: 5 RP");
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Discovery).Text, Is.EqualTo("New creature: Pondlet!"));
            Assert.That(shop.LastPair.WasNew, Is.True);
            Assert.That(shop.TakeOffspring().Success, Is.True);
            BreedOnce(shop);
            Assert.That(shop.Rp, Is.EqualTo(GrowthContent.RpFounder), "a repeat brings nothing");
            Assert.That(shop.TakeEvents().Single().Kind, Is.EqualTo(GrowthEventKind.Bred));
            Assert.That((shop.LastPair.Count, shop.LastPair.WasNew, shop.PairLog.Count), Is.EqualTo((2, false, 1)));
            shop.Parents.Clear(); shop.Offspring = null; shop.Carried.Clear();
            Pair(shop, "pondlet", "bubblebelly");
            Assert.That(BreedOnce(shop).Species, Is.EqualTo("puddlepup"));
            Assert.That(shop.Rp, Is.EqualTo(GrowthContent.RpFounder + GrowthContent.RpHybrid), "a hybrid: 8 RP");
            shop.Parents.Clear(); shop.Offspring = null; shop.Carried.Clear();
            Pair(shop, "reedwhisker", "puddlepup");
            Assert.That(BreedOnce(shop).Species, Is.EqualTo("brookmonarch"), "the Brookkin special pair");
            Assert.That(shop.Rp, Is.EqualTo(GrowthContent.RpFounder + GrowthContent.RpHybrid + GrowthContent.RpCrown), "a crown: 20 RP");
            shop.Parents.Clear(); shop.Offspring = null; shop.Carried.Clear();
            shop.TakeEvents();
            shop.Held.Add("mist");
            Assert.That(shop.TintWater("mist").Success, Is.True);
            Pair(shop, "pondlet", "pondlet");
            BreedOnce(shop);
            Assert.That(shop.Rp, Is.EqualTo(GrowthContent.RpFounder + GrowthContent.RpHybrid + GrowthContent.RpCrown + GrowthContent.RpColour), "a new colour: 2 RP");
            Assert.That(shop.TakeEvents().Single().Text, Is.EqualTo("New colour: Shimmer Pondlet!"));
            Assert.That(shop.Journal, Is.EquivalentTo(new[] { "bubblebelly/Classic", "ribbonfin/Classic", "pondlet/Classic", "puddlepup/Classic", "brookmonarch/Classic", "pondlet/Shimmer" }));
            Assert.That(shop.PairLog.Select(p => p.A + " x " + p.B + " = " + p.Species),
                Is.EqualTo(new[] { "bubblebelly x ribbonfin = pondlet", "bubblebelly x pondlet = puddlepup", "puddlepup x reedwhisker = brookmonarch", "pondlet x pondlet = pondlet" }));
        }

        [Test]
        public void LargeParentsNeedLevelTwoAndUpgradesWaitForAnIdleTank()
        {
            var shop = At(era: 2, stars: 2, credits: 1000, rp: 10, breeding: 1);
            Assert.That(shop.DevelopmentGive("shellback").Success, Is.True);
            var refused = shop.PutParent();
            Assert.That((refused.Failure, refused.Detail), Is.EqualTo((GrowthFailure.DoesNotFit, "Large parents need the breeding tank's level 2")));
            shop.Carried.Clear();
            Pair(shop, "pondlet", "ribbonfin");
            Assert.That(shop.UpgradeCheck("breeding", out _, out string why), Is.EqualTo(GrowthFailure.Busy), "not while breeding");
            Assert.That(why, Is.EqualTo("Busy: take a parent or the offspring out first"));
            BreedOnce(shop);
            Assert.That(shop.UpgradeCheck("breeding", out _, out _), Is.EqualTo(GrowthFailure.Busy), "not while an offspring waits");
            Assert.That(shop.TakeOffspring().Success && shop.TakeParent(shop.Parents[0].Id).Success, Is.True);
            Assert.That(shop.Upgrade("breeding").Success, Is.True, "idle: one parent, an empty nursery");
            Assert.That(shop.Parents.Count, Is.EqualTo(1), "an upgrade keeps the creature inside it");
            shop.Carried.Clear();
            Assert.That(shop.DevelopmentGive("shellback").Success && shop.PutParent().Success, Is.True, "level 2 takes Large parents");
            Assert.That(shop.BreedingLeft, Is.EqualTo(40f), "level 2 breeds in 40 s");
            Assert.That(shop.PutParent().Failure, Is.EqualTo(GrowthFailure.HandsEmpty));
            Assert.That(new GrowthShop { Era = 2 }.ParentCheck(new GrowthSpecimen { Species = "pondlet" }, out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Buy the breeding tank first"));
        }

        [Test]
        public void EraThreeOpensWithThreeStarsAHybridAndALargeTank()
        {
            var shop = At(era: 2, stars: 3, credits: 1000, breeding: 1);
            shop.UnitB = true;
            var lines = shop.Objectives().Select(l => l.text).ToList();
            Assert.That(lines.First(), Does.StartWith("Buy the breeding tank"), "era 2's goal: breed your first hybrid");
            Assert.That(shop.Objectives().First().done, Is.True);
            Assert.That(lines, Has.Some.EqualTo("Breed your first hybrid"));
            Assert.That(shop.Objectives().Single(l => l.text == "Breed your first hybrid").done, Is.False);
            Assert.That(shop.BuyContainer("large", "a3").Success, Is.True);
            Assert.That(shop.Era, Is.EqualTo(2), "3 stars and a Large tank but no hybrid yet");
            Pair(shop, "pondlet", "bubblebelly");
            BreedOnce(shop);
            Assert.That(shop.Era, Is.EqualTo(3), "the first hybrid opens era 3");
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.EraUp).Text, Is.EqualTo("Era 3: Plumbing. Water kits and catalog page 3 open."));
            Assert.That(shop.EraGoal, Is.EqualTo(GrowthContent.Eras[2].Goal));
            Assert.That(shop.Objectives().First().text, Is.EqualTo("Fill a Large tank (0/8 spaces)"));
            Assert.That(shop.Objectives().Select(o => o.text), Has.Some.StartsWith("Open the lab annex"), "GR-07: era 3's goal goes on to the lab annex and power");
            // Two stars are not enough, whatever else is done.
            var early = At(era: 2, stars: 2, credits: 1000, breeding: 1);
            Assert.That(early.BuyContainer("large", "a3").Success, Is.True);
            Pair(early, "pondlet", "bubblebelly");
            BreedOnce(early);
            Assert.That(early.Era, Is.EqualTo(2));
        }

        [Test]
        public void TheVersionFourSaveKeepsTheBreedingTankThePairLogAndColours()
        {
            var shop = At(era: 2, stars: 2, breeding: 2);
            shop.Discovered.Add("pondlet"); shop.Journal.Add("pondlet/Classic");
            shop.Held.Add("bloom");
            Assert.That(shop.TintWater("bloom").Success, Is.True);
            Pair(shop, "pondlet", "pondlet");
            BreedOnce(shop);
            Assert.That(shop.TakeOffspring().Success, Is.True);
            shop.Held.Add("flow");
            Assert.That(shop.TintWater("flow").Success, Is.True);
            shop.Tick(10);
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            Assert.That((back.Level("breeding"), back.Parents.Count, back.Tint, back.BreedingLeft), Is.EqualTo((2, 2, "flow", 30f)));
            Assert.That(back.Journal, Is.EquivalentTo(new[] { "pondlet/Classic", "pondlet/Sunny" }));
            Assert.That(back.Carried.Single().Colour, Is.EqualTo(Colour.Sunny));
            Assert.That(back.PairLog.Single().Count, Is.EqualTo(1));
            Assert.That(GrowthSave.CurrentVersion, Is.GreaterThanOrEqualTo(4));
            // A GR-04 save loads: its journal is its discoveries, all Classic.
            var old = JsonUtility.FromJson<GrowthSave>(json);
            old.version = 3; old.parents = null; old.offspring = null; old.journal = null; old.pairLog = null; old.tint = null; old.breedingLeft = 0;
            old.stations = old.stations.Where(s => s != "breeding").ToArray(); old.levels = old.levels.Take(old.stations.Length).ToArray();
            var gr04 = GrowthShop.Restore(old, out problem);
            Assert.That(problem, Is.Null);
            Assert.That((gr04.HasBreedingTank, gr04.Journal.Single()), Is.EqualTo((false, "pondlet/Classic")));
            void Refused(Action<GrowthSave> edit, string expected)
            {
                var bad = JsonUtility.FromJson<GrowthSave>(json);
                edit(bad);
                Assert.That(GrowthShop.Restore(bad, out string why), Is.Null, expected);
                Assert.That(why, Is.EqualTo(expected));
            }
            Refused(b => b.tint = "ember", "Unknown tint ember");
            Refused(b => b.journal = new[] { "pondlet/Golden" }, "Unknown journal entry pondlet/Golden");
            Refused(b => b.pairLog[0].species = "dragon", "Unknown species in the pair log");
            Refused(b => { b.stations = b.stations.Where(s => s != "breeding").ToArray(); b.levels = b.levels.Take(b.stations.Length).ToArray(); }, "Breeding without a breeding tank");
            Refused(b => { b.levels[Array.IndexOf(b.stations, "breeding")] = 1; b.parents[0].species = "shellback"; }, "A Large parent before the breeding tank's level 2");
            Refused(b => b.parents = b.parents.Concat(new[] { new GrowthSave.Specimen { id = 900, species = "pondlet", colour = "Classic" } }).ToArray(), "More parents than the breeding tank takes");
            Refused(b => b.offspring = new[] { new GrowthSave.Specimen { id = b.parents[0].id, species = "pondlet", colour = "Classic" } }, "A creature is saved twice");
        }
    }
}
