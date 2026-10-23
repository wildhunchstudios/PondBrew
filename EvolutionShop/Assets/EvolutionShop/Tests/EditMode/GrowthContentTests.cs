using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-01: the grow-from-nothing roster, tables and discovery rules (GR.5-GR.10).</summary>
    public sealed class GrowthContentTests
    {
        static readonly List<string> AllCultures = GrowthContent.Cultures.Select(c => c.Id).ToList();

        [Test]
        public void ContentValidatesWithNoProblems()
        {
            Assert.That(GrowthRules.Validate(), Is.Empty);
        }

        [Test]
        public void RosterIsSixFamiliesOfSixWithFoundersHybridsAndOneCrownEach()
        {
            Assert.That(GrowthContent.Species, Has.Length.EqualTo(36));
            Assert.That(GrowthContent.JournalEntries, Is.EqualTo(144));
            Assert.That(GrowthContent.Cultures, Has.Length.EqualTo(7));
            Assert.That(GrowthContent.Families, Has.Length.EqualTo(6));
            foreach (var family in GrowthContent.Families)
            {
                var members = GrowthContent.Species.Where(s => s.Family == family.Id).ToList();
                Assert.That(members, Has.Count.EqualTo(6), family.Id);
                Assert.That(members.Count(s => s.Kind == SpeciesKind.Founder), Is.InRange(2, 3), family.Id);
                Assert.That(members.Count(s => s.Kind == SpeciesKind.Hybrid), Is.InRange(2, 3), family.Id);
                Assert.That(members.Count(s => s.Kind == SpeciesKind.Crown), Is.EqualTo(1), family.Id);
            }
            // The existing five keep their roles and recipes (GR.7).
            Assert.That(GrowthContent.Get("pondlet").Recipe, Is.EqualTo(new[] { 1, 1, 0, 0, 0, 0, 0 }));
            Assert.That(GrowthContent.Get("lanternfin").Family, Is.EqualTo("sparkline"));
            Assert.That(GrowthContent.Get("driftwing").Size, Is.EqualTo(SizeClass.Medium));
            Assert.That(GrowthContent.Cultures.Where(c => c.Era == 1).Select(c => c.Id), Is.EquivalentTo(new[] { "bloom", "flow" }));
            Assert.That(GrowthContent.Cultures.Where(c => c.Era == 1).All(c => c.Credits == 0 && c.Rp == 0), Is.True);
        }

        [Test]
        public void EveryMixOfTwoToSixDosesMakesACreatureAndExactRecipesMakeTheirFounder()
        {
            var (mixes, landing) = GrowthReport.MixCoverage();
            Assert.That(mixes, Is.EqualTo(1708));
            Assert.That(landing.Values.Sum(), Is.EqualTo(mixes));
            foreach (var founder in GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Founder))
            {
                var result = GrowthRules.Mix(founder.Recipe, AllCultures);
                Assert.That(result.Species, Is.SameAs(founder), founder.Id);
                Assert.That(result.Exact, Is.True, founder.Id);
            }
            Assert.That(GrowthRules.Mix(new int[7], AllCultures).Species, Is.Null);
        }

        [Test]
        public void MixesOnlyMakeFoundersWhoseCulturesAreOwnedAndSayWhenOneDoseFromSomethingNew()
        {
            var start = new[] { "bloom", "flow" };
            foreach (var doses in new[] { new[] { 3, 0, 0, 0, 0, 0, 0 }, new[] { 0, 3, 0, 0, 0, 0, 0 }, new[] { 1, 1, 0, 0, 0, 0, 0 } })
                Assert.That(GrowthContent.Family(GrowthRules.Mix(doses, start).Species.Family).Era, Is.EqualTo(1));
            // The very first mix makes the nearest founder; after that, only an exact recipe finds a new one.
            Assert.That(GrowthRules.Mix(new[] { 2, 0, 0, 0, 0, 0, 0 }, start, new string[0]).Species.Id, Is.EqualTo("bubblebelly"));
            var mix = GrowthRules.Mix(new[] { 2, 0, 0, 0, 0, 0, 0 }, start, new[] { "pondlet" });
            Assert.That(mix.Species.Id, Is.EqualTo("pondlet"), "two Bloom is one Flow short of Bubblebelly: it makes the known Pondlet");
            Assert.That(mix.Exact, Is.False);
            Assert.That(mix.CloseToNew, Is.True, "and says it is one dose from something new");
            var exact = GrowthRules.Mix(new[] { 2, 1, 0, 0, 0, 0, 0 }, start, new[] { "pondlet" });
            Assert.That(exact.Species.Id, Is.EqualTo("bubblebelly"));
            Assert.That(exact.Exact, Is.True);
            Assert.That(GrowthRules.Mix(new[] { 1, 1, 0, 0, 0, 0, 0 }, start, new[] { "pondlet", "bubblebelly", "ribbonfin" }).DistanceToNew, Is.EqualTo(-1));
        }

        [Test]
        public void AnyPairBreedsAPredictableCreatureAndOnlySpecialPairsMakeCrowns()
        {
            var sp = GrowthContent.Species;
            foreach (var a in sp)
                foreach (var b in sp)
                {
                    var child = GrowthRules.Breed(a.Id, b.Id);
                    Assert.That(child, Is.Not.Null);
                    Assert.That(GrowthRules.Breed(b.Id, a.Id), Is.SameAs(child), a.Id + " x " + b.Id);
                    if (a == b) Assert.That(child, Is.SameAs(a), "same species breeds true");
                    bool special = sp.Any(c => c.Kind == SpeciesKind.Crown && (c.PairA == a.Id && c.PairB == b.Id || c.PairA == b.Id && c.PairB == a.Id));
                    bool twoOfACrown = a == b && a.Kind == SpeciesKind.Crown;
                    Assert.That(child.Kind == SpeciesKind.Crown, Is.EqualTo(special || twoOfACrown), a.Id + " x " + b.Id);
                    // Breeding never reaches past the later parent's family era.
                    int era = System.Math.Max(GrowthContent.Family(a.Family).Era, GrowthContent.Family(b.Family).Era);
                    Assert.That(GrowthContent.Family(child.Family).Era, Is.LessThanOrEqualTo(era), a.Id + " x " + b.Id + " -> " + child.Id);
                }
            foreach (var (hybrid, a, b) in GrowthContent.IntendedPairs) Assert.That(GrowthRules.Breed(a, b).Id, Is.EqualTo(hybrid));
            var coverage = GrowthReport.PairCoverage();
            Assert.That(coverage.Values.Sum(), Is.EqualTo(630));
            Assert.That(sp.Where(s => s.Kind != SpeciesKind.Crown).All(s => coverage[s.Id] >= 1), Is.True, "every non-crown is some pair's offspring");
            Assert.That(sp.Where(s => s.Kind == SpeciesKind.Crown).All(s => coverage[s.Id] == 1), Is.True, "a crown has exactly one pair");
        }

        [Test]
        public void ColourIsDeterministic()
        {
            Assert.That(GrowthRules.BreedColour(Colour.Classic, Colour.Deep), Is.EqualTo(Colour.Deep));
            Assert.That(GrowthRules.BreedColour(Colour.Shimmer, Colour.Sunny), Is.EqualTo(Colour.Shimmer));
            Assert.That(GrowthRules.BreedColour(Colour.Deep, Colour.Deep, "bloom"), Is.EqualTo(Colour.Sunny));
            Assert.That(GrowthRules.BreedColour(Colour.Classic, Colour.Classic, "flow"), Is.EqualTo(Colour.Deep));
            Assert.That(GrowthRules.BreedColour(Colour.Classic, Colour.Classic, "mist"), Is.EqualTo(Colour.Shimmer));
        }

        [Test]
        public void ContainersHoldCreaturesBySpacesAndSize()
        {
            var starter = GrowthContent.Container("starter");
            var large = GrowthContent.Container("large");
            Assert.That(GrowthRules.Fits(starter, 1, new[] { "pondlet", "pondlet", "ribbonfin" }, "bubblebelly"), Is.True, "four small in the starting tank");
            Assert.That(GrowthRules.Fits(starter, 1, new[] { "pondlet", "pondlet", "ribbonfin", "bubblebelly" }, "pondlet"), Is.False);
            Assert.That(GrowthRules.Fits(starter, 1, new[] { "emberpuff" }, "cindertail"), Is.True, "two medium");
            Assert.That(GrowthRules.Fits(starter, 3, new string[0], "shellback"), Is.False, "large creatures need a Large tank");
            Assert.That(GrowthRules.Fits(large, 1, new[] { "shellback" }, "coralhorn"), Is.True);
            Assert.That(GrowthRules.Fits(large, 1, new[] { "shellback", "coralhorn" }, "pondlet"), Is.False);
            Assert.That(starter.Levels[0].Stat("spaces"), Is.EqualTo(4), "the starting tank has 4 spaces (GR.3)");
        }

        [Test]
        public void StationsStartSmallAndEveryLevelCostsMoreAndUnlocksNoEarlier()
        {
            foreach (var id in new[] { "siphon", "grower", "incubator", "counter", "bench" })
            {
                var station = GrowthContent.Station(id);
                Assert.That(station.Levels.Length, Is.InRange(3, 5), id);
                Assert.That(station.Levels[0].Credits, Is.Zero, id + " starts at level 1, owned");
            }
            foreach (var station in GrowthContent.Stations)
                for (int i = 1; i < station.Levels.Length; i++)
                {
                    Assert.That(station.Levels[i].Level, Is.EqualTo(i + 1), station.Id);
                    Assert.That(station.Levels[i].Credits, Is.GreaterThanOrEqualTo(station.Levels[i - 1].Credits), station.Id);
                    Assert.That(station.Levels[i].Era, Is.GreaterThanOrEqualTo(station.Levels[i - 1].Era), station.Id);
                    Assert.That(station.Levels[i].Stars, Is.GreaterThanOrEqualTo(station.Levels[i - 1].Stars), station.Id);
                }
            foreach (var era in GrowthContent.Eras) Assert.That(era.Stars, Is.EqualTo(era.Number));
        }

        [Test]
        public void StarsRiseWithReputation()
        {
            Assert.That(GrowthContent.StarsFor(0), Is.EqualTo(1));
            for (int s = 1; s < GrowthContent.StarThresholds.Length; s++)
            {
                Assert.That(GrowthContent.StarThresholds[s], Is.GreaterThan(GrowthContent.StarThresholds[s - 1]));
                Assert.That(GrowthContent.StarsFor(GrowthContent.StarThresholds[s] - 1), Is.EqualTo(s));
                Assert.That(GrowthContent.StarsFor(GrowthContent.StarThresholds[s]), Is.EqualTo(s + 1));
            }
            Assert.That(GrowthContent.StarsFor(GrowthContent.ReputationMax), Is.EqualTo(5));
        }
    }
}
