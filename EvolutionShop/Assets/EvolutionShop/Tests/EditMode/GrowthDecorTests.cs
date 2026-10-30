using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-19: decor (GR.60, GR.61): nine pieces in three themes, each sold once in the supplier catalog on the page of
    /// its stars and delivered into the decor box; one on a shop tank's top at night, moving with its tank and back to the box
    /// when the tank is sold; a decorated tank's appeal and its themed creatures' budget (capped with schools); and the
    /// version 12 save.</summary>
    public sealed class GrowthDecorTests
    {
        static GrowthShop Fresh() => GrowthStarts.DecorStart(furnished: false);

        static void Deliver(GrowthShop shop, params string[] decor)
        {
            foreach (var d in decor) Assert.That(shop.Order("decor-" + d).Success, Is.True, "order " + d);
            Assert.That(shop.OpenShop().Success, Is.True);
            Assert.That(shop.CloseEarly().Success, Is.True);
        }

        [Test]
        public void NinePiecesInThreeThemesOfThreeEachWithItsKitPiece()
        {
            var decor = GrowthContent.Decor;
            Assert.That(decor.Select(d => d.Id).Distinct().Count(), Is.EqualTo(9));
            foreach (Water w in System.Enum.GetValues(typeof(Water))) Assert.That(decor.Count(d => d.Water == w), Is.EqualTo(3), w.ToString());
            Assert.That(decor.Where(d => d.Water != Water.Brine).All(d => d.Stars == 2 && d.Era == 2), Is.True, "Riverbank and Hot springs on the 2-star page");
            Assert.That(decor.Where(d => d.Water == Water.Brine).All(d => d.Stars == 4 && d.Era == 4), Is.True, "Seaside on the 4-star page, with Brine");
            Assert.That(decor.All(d => d.Asset.StartsWith("Decor") && d.Credits > 0 && d.Text.Length > 0), Is.True);
            Assert.That(decor.Select(d => d.Theme).Distinct(), Is.EquivalentTo(new[] { "Riverbank", "Hot springs", "Seaside" }));
            var items = GrowthContent.Catalog.Where(i => i.Kind == CatalogKind.Decor).ToList();
            Assert.That(items.Select(i => i.Id), Is.EquivalentTo(decor.Select(d => "decor-" + d.Id)));
            Assert.That(items.All(i => i.Culture == null && GrowthContent.DecorPiece(i.Decor).Stars == i.Stars), Is.True);
        }

        [Test]
        public void EachPieceIsOrderedOnceAtNightOnItsPageAndArrivesInTheBox()
        {
            var tiny = new GrowthShop();
            Assert.That(tiny.OrderCheck("decor-reeds", out string why), Is.EqualTo(GrowthFailure.Locked));
            Assert.That(why, Is.EqualTo("Catalog page 2: needs 2 stars"));
            var era2 = new GrowthShop { Era = 2, Reputation = GrowthContent.StarThresholds[3] + 1, Credits = 5000 };
            Assert.That(era2.OrderCheck("decor-lighthouse", out why), Is.EqualTo(GrowthFailure.Locked), "Seaside waits for era 4");
            var shop = Fresh();
            int credits = shop.Credits;
            Assert.That(shop.Order("decor-reeds").Success, Is.True);
            Assert.That(shop.Credits, Is.EqualTo(credits - GrowthContent.DecorPiece("reeds").Credits));
            Assert.That(shop.OrderCheck("decor-reeds", out why), Is.EqualTo(GrowthFailure.Ordered));
            Assert.That(shop.CancelOrder("decor-reeds").Success && shop.Credits == credits, Is.True, "taken back at night with its credits");
            Assert.That(shop.Order("decor-reeds").Success && shop.Order("decor-lighthouse").Success, Is.True);
            Assert.That(shop.DecorOwned, Is.Empty, "nothing until the shop opens");
            shop.TakeEvents();
            Assert.That(shop.OpenShop().Success, Is.True);
            Assert.That(shop.DecorOwned, Is.EquivalentTo(new[] { "reeds", "lighthouse" }));
            Assert.That(shop.DecorBox, Is.EquivalentTo(new[] { "reeds", "lighthouse" }));
            Assert.That(shop.TakeEvents().Where(e => e.Kind == GrowthEventKind.Delivered).Select(e => e.Text), Has.Some.EqualTo("Delivered: Lighthouse (in the decor box: put it on a tank at night)"));
            Assert.That(shop.OrderCheck("decor-reeds", out why), Is.EqualTo(GrowthFailure.Owned), "each piece once");
            Assert.That(why, Is.EqualTo("It is yours: in the decor box or on a tank"));
        }

        [Test]
        public void APieceGoesOnAShopTankAtNightOneATankAndMovesWithIt()
        {
            var shop = Fresh();
            Assert.That(shop.BuyWorkshop().Success && shop.BuyRack("r1").Success, Is.True, "a lab rack to refuse");
            Deliver(shop, "reeds", "volcano");
            var a1 = shop.ContainerAt("a1"); var a2 = shop.ContainerAt("a2"); var b1 = shop.ContainerAt("b1");
            Assert.That(shop.PutDecor("lighthouse", a1.Id).Failure, Is.EqualTo(GrowthFailure.NotOwned), "only what the shop owns");
            Assert.That(shop.DecorCheck("reeds", shop.ContainerAt("r1").Id, out string why), Is.EqualTo(GrowthFailure.DoesNotFit));
            Assert.That(why, Is.EqualTo("Decor goes on the shop's tanks"));
            Assert.That(shop.PutDecor("reeds", a1.Id).Success, Is.True);
            Assert.That(a1.Decor, Is.EqualTo("reeds"));
            Assert.That(shop.DecorHolder("reeds"), Is.SameAs(a1));
            Assert.That(shop.DecorBox, Is.EqualTo(new[] { "volcano" }));
            Assert.That(shop.PutDecor("reeds", a1.Id).Failure, Is.EqualTo(GrowthFailure.Owned), "it is on this tank");
            // One a tank: another piece on it sends the first back to the box; a piece on another tank moves.
            Assert.That(shop.PutDecor("volcano", a1.Id).Success, Is.True);
            Assert.That((a1.Decor, shop.DecorBox.Single()), Is.EqualTo(("volcano", "reeds")));
            Assert.That(shop.PutDecor("volcano", a2.Id).Success, Is.True);
            Assert.That((a1.Decor, a2.Decor), Is.EqualTo(((string)null, "volcano")), "moved from a1 to a2");
            // By day it stays as it is.
            Assert.That(shop.OpenShop().Success, Is.True);
            Assert.That(shop.PutDecor("reeds", b1.Id).Failure, Is.EqualTo(GrowthFailure.NotAtNight));
            Assert.That(shop.TakeDecor(a2.Id).Failure, Is.EqualTo(GrowthFailure.NotAtNight));
            Assert.That(shop.CloseEarly().Success, Is.True);
            Assert.That(shop.TakeDecor(a2.Id).Success && a2.Decor == null && shop.DecorBox.Count() == 2, Is.True, "back in the box at night");
            Assert.That(shop.TakeDecor(a2.Id).Failure, Is.EqualTo(GrowthFailure.NotOwned));
            // It moves with its tank, and goes back to the box when the tank is sold.
            Assert.That(shop.PutDecor("reeds", b1.Id).Success, Is.True);
            string free = shop.OwnedPlaces.First(p => shop.MoveCheck(b1.Id, p, out _) == GrowthFailure.None);
            Assert.That(shop.MoveContainer(b1.Id, free).Success, Is.True);
            Assert.That((b1.Place, b1.Decor), Is.EqualTo((free, "reeds")));
            b1.Occupants.Clear();
            shop.TakeEvents();
            Assert.That(shop.SellContainer(b1.Id).Success, Is.True);
            Assert.That(shop.DecorBox, Has.Member("reeds"));
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Sold).Text, Does.EndWith("; its Reeds and a dragonfly is back in the decor box"));
        }

        [Test]
        public void ADecoratedTankIsMoreAppealingAndItsThemedCreaturesStretchTheBudget()
        {
            var shop = Fresh();
            Deliver(shop, "reeds", "volcano");
            var a1 = shop.ContainerAt("a1");
            Assert.That(a1.InSchool("pondlet") && !a1.InSchool("bubblebelly"), Is.True, "the fixture's school");
            int appeal = shop.Appeal(a1);
            float Expected(bool school, int app, bool themed) => 1f + Mathf.Min(GrowthContent.ToleranceCap, (school ? GrowthContent.SchoolTolerance : 0) + app * GrowthContent.AppealTolerance + (themed ? GrowthContent.ThemeTolerance : 0));
            Assert.That(shop.Tolerance(a1, "bubblebelly"), Is.EqualTo(Expected(false, appeal, false)).Within(1e-5f));
            // Riverbank suits the fresh-water Brookkin: appeal +1, and a quarter more for them (capped with a school).
            Assert.That(shop.PutDecor("reeds", a1.Id).Success, Is.True);
            Assert.That(shop.Appeal(a1), Is.EqualTo(appeal + 1));
            Assert.That(GrowthShop.Themed(a1, "bubblebelly") && GrowthShop.Themed(a1, "pondlet"), Is.True);
            Assert.That(shop.Tolerance(a1, "bubblebelly"), Is.EqualTo(Expected(false, appeal + 1, true)).Within(1e-5f));
            Assert.That(shop.Tolerance(a1, "pondlet"), Is.EqualTo(1f + GrowthContent.ToleranceCap).Within(1e-5f), "a school and a theme reach the cap");
            // Hot springs does not suit them: only the appeal.
            Assert.That(shop.PutDecor("volcano", a1.Id).Success, Is.True);
            Assert.That(GrowthShop.Themed(a1, "bubblebelly"), Is.False);
            Assert.That(shop.Tolerance(a1, "bubblebelly"), Is.EqualTo(Expected(false, appeal + 1, false)).Within(1e-5f));
            // It suits the warm-water creatures of b1.
            var b1 = shop.ContainerAt("b1");
            Assert.That(shop.PutDecor("volcano", b1.Id).Success, Is.True);
            int warm = b1.Occupants.Count(o => o.Def.Water == Water.Warm);
            Assert.That(warm, Is.GreaterThan(0), "the fixture's warm-water creatures");
            Assert.That(b1.Occupants.Count(o => GrowthShop.Themed(b1, o.Species)), Is.EqualTo(warm));
            Assert.That(shop.Appeal(a1), Is.EqualTo(appeal), "a1's appeal back as it was");
        }

        [Test]
        public void DecorSavesAtVersionTwelveAndOlderSavesLoadWithoutIt()
        {
            var shop = GrowthStarts.DecorStart();
            Assert.That(GrowthSave.CurrentVersion, Is.GreaterThanOrEqualTo(12), "decor came with version 12 (R09.4's lessons made it 13)");
            string json = JsonUtility.ToJson(shop.Snapshot());
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(back.DecorOwned, Is.EquivalentTo(GrowthContent.Decor.Select(d => d.Id)));
            Assert.That(back.Containers.Where(c => c.Decor != null).Select(c => c.Place + ":" + c.Decor), Is.EquivalentTo(new[] { "a1:reeds", "b1:volcano", "b2:lighthouse" }));
            Assert.That(back.DecorBox.Count(), Is.EqualTo(6));
            // An older save has no decor.
            var old = JsonUtility.FromJson<GrowthSave>(json);
            old.version = 11; old.decorOwned = null; foreach (var b in old.containers) b.decor = null;
            var loaded = GrowthShop.Restore(old, out problem);
            Assert.That(problem, Is.Null);
            Assert.That(loaded.DecorOwned, Is.Empty);
            Assert.That(loaded.Containers.All(c => c.Decor == null), Is.True);
            // What cannot be is refused.
            GrowthSave Bad(System.Action<GrowthSave> change) { var s = JsonUtility.FromJson<GrowthSave>(json); change(s); return s; }
            Assert.That(GrowthShop.Restore(Bad(s => s.decorOwned = new[] { "teapot" }), out problem), Is.Null);
            Assert.That(problem, Is.EqualTo("Unknown decor teapot"));
            Assert.That(GrowthShop.Restore(Bad(s => s.decorOwned = new[] { "reeds", "reeds" }), out problem), Is.Null);
            Assert.That(GrowthShop.Restore(Bad(s => s.decorOwned = new[] { "volcano", "lighthouse" }), out problem), Is.Null, "reeds on a1 but not owned");
            Assert.That(problem, Does.StartWith("Decor on a tank that the shop does not own"));
            Assert.That(GrowthShop.Restore(Bad(s => s.containers.First(b => b.place == "a2").decor = "reeds"), out problem), Is.Null);
            Assert.That(problem, Is.EqualTo("A piece of decor on two tanks"));
        }
    }
}
