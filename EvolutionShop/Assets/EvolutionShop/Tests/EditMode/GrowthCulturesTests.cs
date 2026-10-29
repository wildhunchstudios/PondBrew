using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-04: the seven cultures and their sources, the supplier catalog's night orders delivered at the next
    /// opening, starter vials, research, the conditioning vat, mixing with any culture in the chamber, and the version 3
    /// save (GR.6, GR.15.1).</summary>
    public sealed class GrowthCulturesTests
    {
        static GrowthShop At(int era, int stars, int credits = 5000, int rp = 0) =>
            new GrowthShop { Era = era, Reputation = GrowthContent.StarThresholds[stars - 1] + 50, Credits = credits, Rp = rp };

        static void Collect(GrowthShop shop, string culture, int doses)
        {
            for (int i = 0; i < doses; i++)
            {
                if (shop.GrowerUnits[culture] < 1) shop.Tick(shop.RefillSeconds * 2);
                Assert.That(shop.Collect(culture, shop.HoldSeconds).Success, Is.True, "collect " + culture);
            }
        }

        static void Open(GrowthShop shop) => Assert.That(shop.OpenShop().Success, Is.True, "open the shop");

        [Test]
        public void SevenCulturesEachComeFromTheirSource()
        {
            Assert.That(GrowthContent.Cultures.Select(c => c.Id), Is.EqualTo(new[] { "bloom", "flow", "ember", "spark", "mist", "brine", "dusk" }));
            Assert.That(GrowthContent.Cultures.Where(c => c.Source == CultureSource.StarterGrower).Select(c => c.Id), Is.EqualTo(new[] { "bloom", "flow" }));
            Assert.That(GrowthContent.Cultures.Where(c => c.Source == CultureSource.CatalogGrower).Select(c => c.Id), Is.EqualTo(new[] { "ember", "brine" }));
            Assert.That(GrowthContent.Cultures.Where(c => c.Source == CultureSource.Research).Select(c => c.Id), Is.EqualTo(new[] { "spark", "dusk" }));
            Assert.That(GrowthContent.Culture("mist").Source, Is.EqualTo(CultureSource.Vat));
            Assert.That(new GrowthShop().Cultures, Is.EqualTo(new[] { "bloom", "flow" }), "Bloom and Flow are free from the start");
            var catalog = GrowthContent.Catalog;
            Assert.That(catalog.Where(i => i.Kind != CatalogKind.Decor).Select(i => i.Id), Is.EquivalentTo(new[] { "grower-ember", "vial-ember", "vial-spark", "vat", "vial-mist", "grower-brine", "vial-brine", "vial-dusk" }), "the cultures' items (GR-19 adds the decor)");
            Assert.That(catalog.Select(i => i.Stars), Is.Ordered, "pages by stars");
            var ember = GrowthContent.CatalogItem("grower-ember");
            Assert.That((ember.Stars, ember.Era, ember.Credits, ember.Kind), Is.EqualTo((2, 2, 150, CatalogKind.Grower)));
            var vat = GrowthContent.CatalogItem("vat");
            Assert.That((vat.Stars, vat.Era, vat.Credits, vat.Rp, vat.Culture), Is.EqualTo((3, 3, 600, 40, "mist")));
            Assert.That(catalog.Where(i => i.Kind == CatalogKind.Vial).Select(i => i.Credits), Is.EqualTo(new[] { 25, 40, 120, 250, 600 }));
            Assert.That(GrowthContent.CatalogItem("vial-bloom"), Is.Null, "no vials of the free cultures");
        }

        [Test]
        public void OrdersArePaidAtNightAndArriveWhenTheShopNextOpens()
        {
            var shop = At(era: 2, stars: 2, credits: 500);
            Assert.That(shop.Order("grower-ember").Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(350));
            Assert.That(shop.Orders, Is.EqualTo(new[] { "grower-ember" }));
            Assert.That(shop.Owns("ember"), Is.False, "nothing arrives before the shop opens");
            Assert.That(shop.Order("grower-ember").Failure, Is.EqualTo(GrowthFailure.Ordered));
            Assert.That(shop.Order("vial-ember").Success, Is.True, "a vial may come with it");
            Assert.That(shop.CancelOrder("vial-ember").Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(350), "taking an order back returns its credits");
            Assert.That(shop.OrderCheck("grower-brine", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Catalog page 4: needs 4 stars"));
            Assert.That(shop.OrderCheck("vat", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(shop.Order("nothing").Failure, Is.EqualTo(GrowthFailure.NotInCatalog));
            shop.Containers[0].Occupants.Add(new GrowthSpecimen { Id = 900, Species = "pondlet" });
            Open(shop);
            Assert.That(shop.Owns("ember"), Is.True);
            Assert.That(shop.GrowerUnits["ember"], Is.EqualTo(shop.GrowerCapacity), "its grower arrives full");
            Assert.That(shop.Orders, Is.Empty);
            var events = shop.TakeEvents();
            Assert.That(events.Any(e => e.Kind == GrowthEventKind.Delivered && e.Text == "Delivered: Ember grower"), Is.True);
            Assert.That(events.Any(e => e.Kind == GrowthEventKind.CultureOwned && e.Text.StartsWith("New culture: Ember")), Is.True);
            Assert.That(shop.OrderCheck("vial-spark", out why), Is.EqualTo(GrowthFailure.NotAtNight));
            Assert.That(why, Is.EqualTo("Orders are placed at night"));
            Assert.That(shop.CancelOrder("vial-spark").Failure, Is.EqualTo(GrowthFailure.NotInCatalog));
            Assert.That(shop.OrderCheck("grower-ember", out _), Is.EqualTo(GrowthFailure.Owned));
            Assert.That(shop.OrderCheck("vial-ember", out why), Is.EqualTo(GrowthFailure.Owned));
            Assert.That(why, Is.EqualTo("You grow Ember already"));
            shop.Tick(GrowthContent.DaySeconds + 1);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            shop.Credits = 10;
            Assert.That(shop.OrderCheck("vial-spark", out why), Is.EqualTo(GrowthFailure.NotEnoughCredits));
        }

        [Test]
        public void AVialLetsYouTryACultureBeforeOwningItsSource()
        {
            var shop = At(era: 2, stars: 2, credits: 100);
            Assert.That(shop.Order("vial-ember").Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(75));
            Open(shop);
            Assert.That(shop.Vials["ember"], Is.EqualTo(GrowthContent.VialDoses));
            Assert.That(shop.Owns("ember"), Is.False, "a vial is not a source");
            Assert.That(shop.Collect("ember", shop.HoldSeconds).Failure, Is.EqualTo(GrowthFailure.NotOwned), "no Ember grower");
            Assert.That(shop.CollectVial("ember", shop.HoldSeconds * 0.5f).Detail, Is.EqualTo("0"), "a held siphon fills one dose per hold time");
            Assert.That(shop.CollectVial("ember", shop.HoldSeconds * 0.5f).Detail, Is.EqualTo("1"));
            Assert.That(shop.Vials["ember"], Is.EqualTo(2));
            Collect(shop, "bloom", 1);
            Assert.That(shop.AddDose("bloom").Success && shop.AddDose("ember").Success, Is.True);
            Assert.That(shop.Preview().Exact && shop.Preview().Species.Id == "emberpuff", Is.True, "Bloom 1 + Ember 1 is the Emberpuff's recipe");
            Assert.That(shop.StartIncubation().Success, Is.True);
            shop.Tick(shop.IncubationSeconds + 0.1f);
            Assert.That(shop.Discovered, Does.Contain("emberpuff"), "a vial's culture can make a new founder");
            Assert.That(shop.Hatchling.Def.Size, Is.EqualTo(SizeClass.Medium));
            Assert.That(shop.CollectVial("ember", shop.HoldSeconds * 2).Detail, Is.EqualTo("2"));
            Assert.That(shop.Vials.ContainsKey("ember"), Is.False, "an empty vial is gone");
            Assert.That(shop.CollectVial("ember", shop.HoldSeconds).Failure, Is.EqualTo(GrowthFailure.NoDose));
            Assert.That(shop.CollectVial("spark", shop.HoldSeconds).Failure, Is.EqualTo(GrowthFailure.NoDose));
        }

        [Test]
        public void ResearchPointsBringSparkAndDusk()
        {
            var shop = At(era: 2, stars: 2, rp: 9);
            Assert.That(shop.ResearchCheck("spark", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 10 research points (you have 9)"));
            shop.Rp = 12;
            Assert.That(shop.Research("spark").Success, Is.True);
            Assert.That(shop.Rp, Is.EqualTo(2));
            Assert.That(shop.Owns("spark") && shop.GrowerUnits["spark"] == shop.GrowerCapacity, Is.True, "its grower stands in the back room, full");
            Assert.That(shop.Research("spark").Failure, Is.EqualTo(GrowthFailure.Owned));
            Assert.That(shop.ResearchCheck("dusk", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs era 5"));
            Assert.That(shop.Research("ember").Failure, Is.EqualTo(GrowthFailure.NotInCatalog), "Ember comes from the supplier");
            var early = At(era: 1, stars: 1, rp: 50);
            Assert.That(early.ResearchCheck("spark", out why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs era 2"));
            var late = At(era: 5, stars: 5, rp: 150);
            Assert.That(late.Research("dusk").Success && late.Rp == 0 && late.Owns("dusk"), Is.True);
        }

        [Test]
        public void TheVatNeedsItsResearchPointsAndMakesMist()
        {
            var shop = At(era: 3, stars: 3, credits: 1000, rp: 39);
            Assert.That(shop.OrderCheck("vat", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Needs 40 research points"));
            shop.Rp = 50;
            Assert.That(shop.Order("vat").Success, Is.True);
            Assert.That((shop.Credits, shop.Rp), Is.EqualTo((400, 10)));
            Assert.That(shop.CancelOrder("vat").Success && shop.Credits == 1000 && shop.Rp == 50, Is.True, "both come back");
            shop.Order("vat");
            shop.Containers[0].Occupants.Add(new GrowthSpecimen { Id = 900, Species = "pondlet" });
            Open(shop);
            Assert.That(shop.Owns("mist"), Is.True);
            shop.GrowerUnits["mist"] = 0;
            shop.Tick(shop.RefillSeconds);
            Assert.That(shop.GrowerUnits["mist"], Is.EqualTo(1).Within(0.001), "the vat refills like a grower");
        }

        [Test]
        public void MixesUseOwnedCulturesAndWhatIsInTheChamber()
        {
            var shop = At(era: 2, stars: 2, rp: 10);
            shop.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            Collect(shop, "bloom", 1); Collect(shop, "flow", 2);
            shop.AddDose("bloom"); shop.AddDose("flow"); shop.AddDose("flow");
            Assert.That(shop.Preview().DistanceToNew, Is.EqualTo(-1), "with Bloom and Flow alone nothing new is near: every Brookkin founder is known");
            shop.Research("spark");
            Assert.That(shop.Preview().DistanceToNew, Is.EqualTo(2), "owning Spark brings the Lanternfin (Bloom, Flow, Spark) two doses away");
            shop.Upgrade("siphon");
            Collect(shop, "spark", 1);
            shop.ReturnDose("flow");
            Assert.That(shop.AddDose("spark").Success, Is.True);
            Assert.That(shop.Preview().Exact && shop.Preview().Species.Id == "lanternfin", Is.True);
            Assert.That(shop.Hatchling, Is.Null);
            shop.StartIncubation();
            shop.Tick(shop.IncubationSeconds + 0.1f);
            Assert.That(shop.Discovered, Does.Contain("lanternfin"));
            Assert.That(shop.Hatchling.Def.Family, Is.EqualTo("sparkline"));
        }

        [Test]
        public void TheVersionThreeSaveKeepsCulturesVialsAndOrders()
        {
            var shop = At(era: 2, stars: 2, credits: 500, rp: 12);
            shop.Research("spark");
            shop.Order("vial-ember");
            Assert.That(shop.DevelopmentStock(shop.Containers[0].Id, "pondlet").Success, Is.True);
            Open(shop);
            shop.Tick(GrowthContent.DaySeconds + 1);
            shop.Order("grower-ember");
            shop.CollectVial("ember", shop.HoldSeconds);
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            Assert.That(back.Owns("spark"), Is.True);
            Assert.That(back.Vials["ember"], Is.EqualTo(2));
            Assert.That(back.Orders, Is.EqualTo(new[] { "grower-ember" }));
            Assert.That(GrowthSave.CurrentVersion, Is.GreaterThanOrEqualTo(3), "version 3 or later (GR-05 is 4)");
            var old = JsonUtility.FromJson<GrowthSave>(json);
            old.version = 2; old.vialCultures = null; old.vialDoses = null; old.orders = null;
            Assert.That(GrowthShop.Restore(old, out problem), Is.Not.Null, "a GR-03 save loads");
            Assert.That(problem, Is.Null);
            var bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.orders = new[] { "grower-lava" };
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null);
            Assert.That(problem, Is.EqualTo("Unknown order grower-lava"));
            bad = JsonUtility.FromJson<GrowthSave>(json);
            bad.vialDoses = new[] { 0 };
            Assert.That(GrowthShop.Restore(bad, out problem), Is.Null);
            Assert.That(problem, Is.EqualTo("A vial that is not one"));
        }
    }
}
