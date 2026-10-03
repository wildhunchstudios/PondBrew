using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class CustomerTypeTests
    {
        static readonly string[] Tanks = { "tank-a", "tank-b" };
        GameConfig config;
        GameModel model;
        CustomerArchetype[] catalog;

        [SetUp]
        public void Setup()
        {
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
            catalog = CustomerArchetypes.Validate(CustomerArchetypes.CreateDefault(), Known);
        }

        bool Known(string id) => model.GetSpecies(id) != null;
        CustomerArchetype Type(string id) => CustomerArchetypes.Find(catalog, id);

        [Test]
        public void DefaultCatalogHasFourValidTypesWithIndependentCopies()
        {
            Assert.That(catalog.Length, Is.EqualTo(4));
            Assert.That(Array.ConvertAll(catalog, t => t.Id), Is.EqualTo(new[] { "casual", "bargain", "collector", "premium" }));
            Assert.That(Array.ConvertAll(catalog, t => t.Weight), Is.EqualTo(new[] { 40, 30, 20, 10 }));
            Assert.That(Array.ConvertAll(catalog, t => t.PatienceSeconds), Is.EqualTo(new[] { 45f, 60f, 50f, 30f }));
            Assert.That(Type("collector").FavoriteSpecies, Is.EqualTo(new[] { "bubblebelly", "ribbonfin" }));
            var found = Type("collector");
            found.FavoriteSpecies[0] = "pondlet";
            found.Budget = 1;
            Assert.That(Type("collector").FavoriteSpecies[0], Is.EqualTo("bubblebelly"));
            Assert.That(Type("collector").Budget, Is.EqualTo(40));
            Assert.That(CustomerArchetypes.Find(catalog, "unknown"), Is.Null);
            Assert.That(CustomerArchetypes.Find(catalog, null), Is.Null);
        }

        [TestCase("null-catalog")]
        [TestCase("empty-catalog")]
        [TestCase("null-type")]
        [TestCase("duplicate-id")]
        [TestCase("blank-id")]
        [TestCase("long-id")]
        [TestCase("blank-name")]
        [TestCase("blank-presentation")]
        [TestCase("negative-weight")]
        [TestCase("weight-over-cap")]
        [TestCase("no-arrivals")]
        [TestCase("zero-budget")]
        [TestCase("bargain-over-100")]
        [TestCase("tolerance-below-100")]
        [TestCase("favourite-tolerance-over-cap")]
        [TestCase("nan-patience")]
        [TestCase("zero-browse")]
        [TestCase("null-favourites")]
        [TestCase("unknown-favourite")]
        [TestCase("repeated-favourite")]
        public void InvalidCatalogsAreRejected(string problem)
        {
            var types = new List<CustomerArchetype>(CustomerArchetypes.CreateDefault());
            var first = types[0];
            switch (problem)
            {
                case "null-catalog": types = null; break;
                case "empty-catalog": types.Clear(); break;
                case "null-type": types[1] = null; break;
                case "duplicate-id": types[1].Id = first.Id; break;
                case "blank-id": first.Id = " "; break;
                case "long-id": first.Id = new string('a', 33); break;
                case "blank-name": first.DisplayName = ""; break;
                case "blank-presentation": first.PresentationId = null; break;
                case "negative-weight": first.Weight = -1; break;
                case "weight-over-cap": first.Weight = 1001; break;
                case "no-arrivals": foreach (var type in types) type.Weight = 0; break;
                case "zero-budget": first.Budget = 0; break;
                case "bargain-over-100": first.BargainPercent = 101; break;
                case "tolerance-below-100": first.TolerancePercent = 99; break;
                case "favourite-tolerance-over-cap": first.FavoriteTolerancePercent = 1001; break;
                case "nan-patience": first.PatienceSeconds = float.NaN; break;
                case "zero-browse": first.BrowseSeconds = 0f; break;
                case "null-favourites": first.FavoriteSpecies = null; break;
                case "unknown-favourite": types[2].FavoriteSpecies = new[] { "bubblebelly", "unknown" }; break;
                case "repeated-favourite": types[2].FavoriteSpecies = new[] { "ribbonfin", "ribbonfin" }; break;
            }
            Assert.Throws<ArgumentException>(() => CustomerArchetypes.Validate(types, Known));
        }

        [Test]
        public void WeightedChoiceIsDeterministicFollowsWeightsAndNeverPicksZeroWeights()
        {
            for (int day = 1; day <= 5; day++)
                for (int index = 0; index < 20; index++)
                    Assert.That(CustomerArchetypes.Pick(catalog, day, index).Id, Is.EqualTo(CustomerArchetypes.Pick(catalog, day, index).Id));
            var counts = new Dictionary<string, int>();
            const int samples = 8000;
            for (int i = 0; i < samples; i++)
            {
                string id = CustomerArchetypes.Pick(catalog, 1 + i / 40, i % 40).Id;
                counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            Assert.That(counts["casual"] / (double)samples, Is.EqualTo(.40).Within(.03));
            Assert.That(counts["bargain"] / (double)samples, Is.EqualTo(.30).Within(.03));
            Assert.That(counts["collector"] / (double)samples, Is.EqualTo(.20).Within(.03));
            Assert.That(counts["premium"] / (double)samples, Is.EqualTo(.10).Within(.03));

            var withoutPremium = CustomerArchetypes.CreateDefault();
            withoutPremium[3].Weight = 0;
            for (int i = 0; i < 2000; i++) Assert.That(CustomerArchetypes.Pick(withoutPremium, 3, i).Id, Is.Not.EqualTo("premium"));
            var picked = CustomerArchetypes.Pick(catalog, 1, 0);
            picked.Budget = 1;
            Assert.That(CustomerArchetypes.Find(catalog, picked.Id).Budget, Is.Not.EqualTo(1));
            Assert.Throws<ArgumentException>(() => CustomerArchetypes.Pick(new CustomerArchetype[0], 1, 0));
        }

        [TestCase("bargain", "pondlet", 5, PriceReaction.Bargain, false)]
        [TestCase("bargain", "pondlet", 6, PriceReaction.Fair, false)]
        [TestCase("bargain", "pondlet", 7, PriceReaction.TooExpensive, false)]
        [TestCase("bargain", "pondlet", 9, PriceReaction.TooExpensive, true)]
        [TestCase("bargain", "bubblebelly", 8, PriceReaction.Bargain, false)]
        [TestCase("bargain", "bubblebelly", 9, PriceReaction.TooExpensive, true)]
        [TestCase("bargain", "bubblebelly", 10, PriceReaction.TooExpensive, true)]
        [TestCase("collector", "pondlet", 4, PriceReaction.Bargain, false)]
        [TestCase("collector", "pondlet", 6, PriceReaction.Fair, false)]
        [TestCase("collector", "pondlet", 7, PriceReaction.TooExpensive, false)]
        [TestCase("collector", "bubblebelly", 10, PriceReaction.Fair, false)]
        [TestCase("collector", "ribbonfin", 25, PriceReaction.Pricey, false)]
        [TestCase("collector", "ribbonfin", 26, PriceReaction.TooExpensive, false)]
        [TestCase("premium", "pondlet", 3, PriceReaction.Bargain, false)]
        [TestCase("premium", "pondlet", 4, PriceReaction.Fair, false)]
        [TestCase("premium", "pondlet", 15, PriceReaction.Pricey, false)]
        [TestCase("premium", "pondlet", 16, PriceReaction.TooExpensive, false)]
        [TestCase("premium", "bubblebelly", 25, PriceReaction.Pricey, false)]
        [TestCase("casual", "pondlet", 9, PriceReaction.Pricey, false)]
        [TestCase("casual", "pondlet", 10, PriceReaction.TooExpensive, false)]
        public void EachTypeJudgesTagsByToleranceFavouritesAndBudget(string typeId, string species, int price, PriceReaction expected, bool overBudget)
        {
            var type = Type(typeId);
            int standard = model.GetSpecies(species).SaleValue;
            Assert.That(type.Judge(species, price, standard), Is.EqualTo(expected));
            Assert.That(type.OverBudget(price), Is.EqualTo(overBudget));
        }

        [Test]
        public void AcceptableLimitIsExactlyTheHighestPriceEachTypeDoesNotRefuse()
        {
            foreach (var type in catalog)
                foreach (var species in config.Species)
                    for (int price = 1; price <= 40; price++)
                        Assert.That(type.Judge(species.Id, price, species.SaleValue) != PriceReaction.TooExpensive,
                            Is.EqualTo(price <= type.AcceptableLimit(species.Id, species.SaleValue)), type.Id + " " + species.Id + " " + price);
        }

        [Test]
        public void VisitUsesItsTypesBrowseTimeAndPatience()
        {
            Stock("tank-a", "pondlet");
            var visit = new CustomerVisit(model, Tanks, null, Type("bargain"));
            Assert.That(visit.ArchetypeId, Is.EqualTo("bargain"));
            Assert.That(visit.ArchetypeName, Is.EqualTo("Bargain hunter"));
            visit.Advance(.1f, true, ShopDayPhase.Open);
            visit.Advance(2.9f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Browsing), "A bargain hunter browses for 3 seconds.");
            visit.Advance(.2f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            visit.Advance(.1f, true, ShopDayPhase.Open);
            Assert.That(visit.QueueSecondsRemaining, Is.EqualTo(60f));
            var changed = visit.Archetype;
            changed.PatienceSeconds = 1f;
            visit.Advance(30f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued), "Changing a returned type copy cannot change the visit.");
        }

        [Test]
        public void CollectorGoesToItsFavouriteFirstAndOthersKeepDisplayOrder()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "ribbonfin");
            Assert.That(new CustomerVisit(model, Tanks, null, Type("collector")).TargetTankId, Is.EqualTo("tank-b"));
            Assert.That(new CustomerVisit(model, Tanks, null, Type("casual")).TargetTankId, Is.EqualTo("tank-a"));
            Assert.That(new CustomerVisit(model, Tanks, null, Type("premium")).TargetTankId, Is.EqualTo("tank-a"));
        }

        [Test]
        public void ContendedDisplaysAreAvoidedWhileAnotherIsFreeButStillUsedWhenAlone()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "pondlet");
            Assert.That(new CustomerVisit(model, Tanks, null, Type("casual"), t => t == "tank-a").TargetTankId, Is.EqualTo("tank-b"));
            Assert.That(new CustomerVisit(model, Tanks, null, Type("casual"), t => true).TargetTankId, Is.EqualTo("tank-a"));
            // A favourite outweighs contention: the collector still wants its Ribbonfin.
            var model2 = new GameModel(config);
            model = model2;
            Stock("tank-a", "pondlet");
            Stock("tank-b", "ribbonfin");
            Assert.That(new CustomerVisit(model, Tanks, null, Type("collector"), t => t == "tank-b").TargetTankId, Is.EqualTo("tank-b"));
        }

        [Test]
        public void BudgetRefusalLeavesNoClaimAndRedirectsToAnAffordableDisplay()
        {
            Stock("tank-a", "bubblebelly");
            string pondlet = Stock("tank-b", "pondlet");
            var visit = new CustomerVisit(model, Tanks, null, Type("bargain"));
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-a"));
            visit.Advance(.1f, true, ShopDayPhase.Open);
            visit.Advance(3f, false, ShopDayPhase.Open);
            Assert.That(visit.PriceRejections, Is.EqualTo(1));
            Assert.That(visit.RefusedOverBudget, Is.True);
            Assert.That(visit.QuotedPrice, Is.EqualTo(10));
            Assert.That(visit.Budget, Is.EqualTo(8));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-b"));
            visit.Advance(.1f, true, ShopDayPhase.Open);
            visit.Advance(3f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            Assert.That(visit.SpecimenId, Is.EqualTo(pondlet));
            Assert.That(visit.Price, Is.EqualTo(6));
            Assert.That(visit.Reaction, Is.EqualTo(PriceReaction.Fair));
        }

        [Test]
        public void ToleranceRefusalIsNotMarkedAsOverBudget()
        {
            Stock("tank-a", "pondlet");
            Ok(model.SetPrice("pondlet", 7));
            var visit = new CustomerVisit(model, Tanks, null, Type("collector"));
            visit.Advance(.1f, true, ShopDayPhase.Open);
            visit.Advance(4f, false, ShopDayPhase.Open);
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(visit.RefusedOverBudget, Is.False);
        }

        [Test]
        public void TwoVisitorsRacingForTheLastSpecimenProduceOneClaimAndOneSale()
        {
            string specimen = Stock("tank-a", "pondlet");
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 1), Is.True);
            var first = new CustomerVisit(model, Tanks, null, Type("casual"));
            var second = new CustomerVisit(model, Tanks, null, Type("casual"), t => t == first.TargetTankId);
            Assert.That(first.TargetTankId, Is.EqualTo("tank-a"));
            Assert.That(second.TargetTankId, Is.EqualTo("tank-a"), "With one stocked display both visitors share it.");
            Assert.That(ledger.BeginVisit(first.Id), Is.True);
            Assert.That(ledger.BeginVisit(second.Id), Is.True);
            first.Advance(.1f, true, ShopDayPhase.Open);
            second.Advance(.1f, true, ShopDayPhase.Open);
            first.Advance(2f, false, ShopDayPhase.Open);
            second.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(first.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            Assert.That(second.Phase, Is.EqualTo(CustomerPhase.Leaving));
            Assert.That(second.Outcome, Is.EqualTo(CustomerOutcome.NoStock));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(first.Id));
            Assert.That(ledger.ResolveVisit(second.Id, second.Outcome), Is.True);
            first.Advance(.1f, true, ShopDayPhase.Open);
            Ok(first.Serve(ShopDayPhase.Open));
            Assert.That(ledger.ResolveVisit(first.Id, CustomerOutcome.Sold, first.Price), Is.True);
            Assert.That(second.Serve(ShopDayPhase.Open).Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(first.SpecimenId, Is.EqualTo(specimen));
            Assert.That(ledger.Current.visitors, Is.EqualTo(2));
            Assert.That(ledger.Current.sales, Is.EqualTo(1));
            Assert.That(ledger.Current.noStock, Is.EqualTo(1));
            Assert.That(ledger.HasPending, Is.False);
        }

        [Test]
        public void TwoQueuedOrdersAreServedIndependentlyAndOneLeavingDoesNotTouchTheOther()
        {
            Stock("tank-a", "pondlet");
            Stock("tank-b", "bubblebelly");
            var hurried = new CustomerVisit(model, Tanks, null, Type("premium"));
            var calm = new CustomerVisit(model, Tanks, null, Type("casual"), t => t == hurried.TargetTankId);
            Assert.That(calm.TargetTankId, Is.EqualTo("tank-b"));
            foreach (var visit in new[] { hurried, calm })
            {
                visit.Advance(.1f, true, ShopDayPhase.Open);
                visit.Advance(3f, false, ShopDayPhase.Open);
                visit.Advance(.1f, true, ShopDayPhase.Open);
                Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
            }
            hurried.Advance(30f, true, ShopDayPhase.Open);
            calm.Advance(30f, true, ShopDayPhase.Open);
            Assert.That(hurried.Outcome, Is.EqualTo(CustomerOutcome.Impatient));
            Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(calm.Phase, Is.EqualTo(CustomerPhase.Queued));
            Ok(calm.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(10));
            Assert.That(model.GetTank("tank-a").HasValue, Is.True);
        }

        [Test]
        public void VisitRejectsATypeWithAnUnknownFavourite()
        {
            var type = Type("collector");
            type.FavoriteSpecies = new[] { "unknown" };
            Assert.Throws<ArgumentException>(() => new CustomerVisit(model, Tanks, null, type));
            var defaultVisit = new CustomerVisit(model, Tanks);
            Assert.That(defaultVisit.ArchetypeId, Is.EqualTo("casual"));
            Assert.That(defaultVisit.Budget, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void LedgerHoldsSeveralUnresolvedVisitorsAndResolvesThemInAnyOrder()
        {
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 2), Is.True);
            for (int i = 0; i < ShopTradeLedger.MaxPending; i++) Assert.That(ledger.BeginVisit("visitor-" + i), Is.True);
            Assert.That(ledger.BeginVisit("one-too-many"), Is.False);
            Assert.That(ledger.BeginVisit("visitor-0"), Is.False);
            Assert.That(ledger.PendingCount, Is.EqualTo(4));
            Assert.That(ledger.ResolveVisit("visitor-3", CustomerOutcome.Sold, 10), Is.True);
            Assert.That(ledger.ResolveVisit("visitor-0", CustomerOutcome.TooExpensive), Is.True);
            Assert.That(ledger.ResolveVisit("visitor-0", CustomerOutcome.TooExpensive), Is.False);
            Assert.That(ledger.PendingCount, Is.EqualTo(2));
            Assert.That(ledger.IsPending("visitor-1"), Is.True);
            var save = ledger.CaptureSave();
            Assert.That(save.version, Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(save.pendingCustomerIds, Is.EqualTo(new[] { "visitor-1", "visitor-2" }));
            var cycle = new ShopDayCycle();
            Assert.That(cycle.OpenShop(), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(JsonUtility.FromJson<TradeLedgerSave>(JsonUtility.ToJson(save)), cycle.CaptureSave(), out var restored), Is.True);
            Assert.That(restored.CancelPendingForReload(), Is.True);
            Assert.That(restored.CancelPendingForReload(), Is.False);
            Assert.That(restored.Current.interrupted, Is.EqualTo(2));
            Assert.That(ledger.FinishDay(1), Is.True);
            Assert.That(ledger.Last.closed, Is.EqualTo(2));
            Assert.That(ledger.Last.visitors, Is.EqualTo(4));
            Assert.That(ledger.Last.sales + ledger.Last.tooExpensive + ledger.Last.closed, Is.EqualTo(4));
        }

        [TestCase("version-2-single")]
        [TestCase("version-1-single")]
        public void OlderReportsWithOneUnresolvedVisitorRestoreIntoTheList(string shape)
        {
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 2), Is.True);
            Assert.That(ledger.BeginVisit("waiting"), Is.True);
            var save = ledger.CaptureSave();
            save.version = shape == "version-1-single" ? 1 : 2;
            save.pendingCustomerIds = null;
            save.pendingCustomerId = "waiting";
            var cycle = new ShopDayCycle();
            Assert.That(cycle.OpenShop(), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(save, cycle.CaptureSave(), out var restored), Is.True);
            Assert.That(restored.PendingCount, Is.EqualTo(1));
            Assert.That(restored.IsPending("waiting"), Is.True);
            var upgraded = restored.CaptureSave();
            Assert.That(upgraded.version, Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(upgraded.pendingCustomerIds, Is.EqualTo(new[] { "waiting" }));
            Assert.That(upgraded.pendingCustomerId, Is.Null.Or.Empty);
        }

        [TestCase("legacy-field-in-version-3")]
        [TestCase("list-in-version-2")]
        [TestCase("duplicate-ids")]
        [TestCase("too-many")]
        [TestCase("blank-id")]
        [TestCase("count-mismatch")]
        public void MalformedVisitorListsAreRejected(string problem)
        {
            var ledger = new ShopTradeLedger();
            Assert.That(ledger.BeginDay(1, 2), Is.True);
            Assert.That(ledger.BeginVisit("one"), Is.True);
            Assert.That(ledger.BeginVisit("two"), Is.True);
            var save = ledger.CaptureSave();
            switch (problem)
            {
                case "legacy-field-in-version-3": save.pendingCustomerId = "one"; break;
                case "list-in-version-2": save.version = 2; break;
                case "duplicate-ids": save.pendingCustomerIds = new[] { "one", "one" }; break;
                case "too-many": save.pendingCustomerIds = new[] { "a", "b", "c", "d", "e" }; save.current.visitors = 5; break;
                case "blank-id": save.pendingCustomerIds = new[] { "one", " " }; break;
                case "count-mismatch": save.pendingCustomerIds = new[] { "one" }; break;
            }
            var cycle = new ShopDayCycle();
            Assert.That(cycle.OpenShop(), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(save, cycle.CaptureSave(), out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        string Stock(string tankId, string speciesId)
        {
            model.Tick(100f);
            var recipe = model.GetSpecies(speciesId);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation());
            model.Tick(config.IncubationSeconds);
            string id = model.Incubator.SpecimenId;
            Ok(model.TransferToTank(tankId));
            return id;
        }

        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
