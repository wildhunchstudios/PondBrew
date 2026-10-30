using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-27: the main menu's words (the saved shop in a line, what New game asks first) and the creatures around its
    /// title (found ones: crowns first, then the newest, one of each family before a second of any, each in its rarest colour
    /// found; none found, the first family's founders).</summary>
    public sealed class GrowthMenuTests
    {
        [Test]
        public void TheSavedShopInALine()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Era3);
            int found = shop.Discovered.Count;
            Assert.That(GrowthMenu.Summary(shop), Is.EqualTo("Day " + shop.Day + " · Era 3 · " + shop.Stars + " stars · " + found + " of 72 creatures found"));
            var fresh = new GrowthShop();
            Assert.That(GrowthMenu.Summary(fresh), Is.EqualTo("Day 1 · Era 1 · 1 star · 0 of 72 creatures found"), "one star, not stars");
            Assert.That(GrowthMenu.ReplaceQuestion(shop), Does.Contain("day " + shop.Day + ", era 3, " + found + " creatures found").And.Contain("cannot be undone"));
            fresh.Discovered.Add("pondlet");
            Assert.That(GrowthMenu.ReplaceQuestion(fresh), Does.Contain("1 creature found"));
        }

        [Test]
        public void WithNothingFoundTheFirstFamilysFoundersStandAroundTheTitle()
        {
            var showcase = GrowthMenu.Showcase(new GrowthShop());
            var founders = GrowthContent.Species.Where(x => x.Family == GrowthContent.Families[0].Id && x.Kind == SpeciesKind.Founder).Select(x => x.Id).ToList();
            Assert.That(showcase.Select(x => x.species), Is.EqualTo(founders.Take(GrowthMenu.ShowcaseCount)));
            Assert.That(showcase.All(x => x.colour == Colour.Classic), Is.True);
        }

        [Test]
        public void CrownsFirstThenTheNewestOneOfEachFamilyFirst()
        {
            var shop = new GrowthShop();
            // Found in this order: three Brookkin, then a Sparkline, then the Brookkin crown.
            var brook = GrowthContent.Species.Where(x => x.Family == "brookkin").ToList();
            var crown = brook.First(x => x.Kind == SpeciesKind.Crown);
            var others = brook.Where(x => x.Kind != SpeciesKind.Crown).Take(3).ToList();
            var spark = GrowthContent.Species.First(x => x.Family != "brookkin");
            foreach (var x in others) shop.Discovered.Add(x.Id);
            shop.Discovered.Add(spark.Id);
            shop.Discovered.Add(crown.Id);
            var showcase = GrowthMenu.Showcase(shop).Select(x => x.species).ToList();
            Assert.That(showcase[0], Is.EqualTo(crown.Id), "the crown first");
            Assert.That(showcase[1], Is.EqualTo(spark.Id), "then the newest of another family, before a second Brookkin");
            Assert.That(showcase.Skip(2), Is.EqualTo(others.Select(x => x.Id).Reverse()), "then the rest, newest first");
            Assert.That(showcase.Count, Is.EqualTo(5), "only what is found");
        }

        [Test]
        public void AtMostSixEachInItsRarestColourFound()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Creatures);
            var showcase = GrowthMenu.Showcase(shop);
            Assert.That(showcase.Count, Is.EqualTo(GrowthMenu.ShowcaseCount));
            Assert.That(showcase.Select(x => x.species).Distinct().Count(), Is.EqualTo(showcase.Count), "each once");
            Assert.That(showcase.Select(x => GrowthContent.Get(x.species).Family).Distinct().Count(), Is.EqualTo(showcase.Count), "six families, one each");
            foreach (var (species, colour) in showcase)
            {
                Assert.That(colour == Colour.Classic || shop.Journal.Contains(GrowthShop.JournalKey(species, colour)), Is.True, species + " in a colour found");
                foreach (var rarer in new[] { Colour.Sunny, Colour.Deep, Colour.Shimmer }.Where(c => c > colour))
                    Assert.That(shop.Journal.Contains(GrowthShop.JournalKey(species, rarer)), Is.False, species + ": no rarer colour found than " + colour);
            }
            var plain = new GrowthShop();
            plain.Discovered.Add("pondlet");
            plain.Journal.Add(GrowthShop.JournalKey("pondlet", Colour.Sunny));
            plain.Journal.Add(GrowthShop.JournalKey("pondlet", Colour.Deep));
            Assert.That(GrowthMenu.RarestColour(plain, "pondlet"), Is.EqualTo(Colour.Deep));
        }
    }
}
