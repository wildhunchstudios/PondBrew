using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class DecorTests
    {
        [Test]
        public void OnlyUniqueFrontStoreDecorCountsAndSixPiecesNeverAddStockSlots()
        {
            var layout = new ShopLayout(); var model = Cash(128);
            Buy(layout, model, ShopLayout.CoralPlanterId, 9, 4);
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.Zero, "Lab furnishing has no customer bonus.");
            Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            var snapshot = ShopAppeal.FromLayout(layout);
            Assert.That(snapshot.Points, Is.EqualTo(10));
            Buy(layout, model, ShopLayout.BubbleLampId, 4, -1);
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.EqualTo(20));
            Buy(layout, model, ShopLayout.SillyStatueId, 3, -5);
            Buy(layout, model, ShopLayout.CoralPlanterId, -12, -8);
            Buy(layout, model, ShopLayout.BubbleLampId, 10, 8);
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.EqualTo(30));
            Assert.That(snapshot.Points, Is.EqualTo(10), "Previously derived appeal is an immutable snapshot.");
            Assert.That(layout.DecorCount, Is.EqualTo(ShopLayout.DecorLimit));
            Assert.That(model.Currency, Is.Zero);
            Assert.That(layout.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" }));
            Assert.That(model.TankIds, Is.EqualTo(layout.TankIds));
            Assert.That(layout.GetSlotIds("table-1"), Is.Empty);
            string before = State(layout, model);
            var rejected = layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.SillyStatueId, null, -6, -8, 0, out _);
            Assert.That(rejected.Failure, Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void MovingTheOnlyStoreCopyIntoLabRemovesItsBonusWithoutChargingOrChangingIdentity()
        {
            var layout = ThreeStoreTypes(out var model);
            string game = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 4, 1, out var id).Success, Is.True);
            Assert.That(id, Is.EqualTo("table-1"));
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.EqualTo(20));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -4, -1, 3, out _).Success, Is.True);
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.EqualTo(30));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(game));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(4));
        }

        [Test]
        public void DecorHasItsOwnSixItemAllowanceWithoutConsumingTableOrDisplayCapacity()
        {
            var layout = new ShopLayout(); var model = Cash(1000);
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -26, -3);
            Buy(layout, model, ShopLayout.TableDefinitionId, -4, -1);
            Buy(layout, model, ShopLayout.TableDefinitionId, 4, -1);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.TableDefinitionId, null, 9, 4, 1, out _).Success, Is.True);
            Buy(layout, model, ShopLayout.CoralPlanterId, 10, 8);
            Buy(layout, model, ShopLayout.CoralPlanterId, -12, 8);
            Buy(layout, model, ShopLayout.CoralPlanterId, -28, -8);
            Buy(layout, model, ShopLayout.BubbleLampId, -23, -8);
            Buy(layout, model, ShopLayout.SillyStatueId, -20, -8);
            Buy(layout, model, ShopLayout.BubbleLampId, -17, -8);
            Assert.That(layout.Items.Count, Is.EqualTo(12)); Assert.That(layout.DecorCount, Is.EqualTo(6));
            Assert.That(layout.TankIds.Length, Is.EqualTo(7)); Assert.That(layout.AppealPoints, Is.EqualTo(30));
            Assert.That(model.Currency, Is.EqualTo(552));
            var save = layout.CaptureSave();
            Assert.That(ShopLayout.TryRestore(save, out var restored), Is.True);
            Assert.That(restored.TankIds, Is.EqualTo(model.TankIds)); Assert.That(restored.AppealPoints, Is.EqualTo(30));
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.CoralPlanterId, null, -27, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
            save.version = 2;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False, "Version2 never owned twelve pieces or any décor.");
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void ConstructionDuringTradingCannotChargeOrChangeAppeal(ShopDayPhase phase)
        {
            var layout = new ShopLayout(); var model = Cash(100);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, phase, ShopLayout.CoralPlanterId, null, -4, -1, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.Zero);
        }

        [Test]
        public void RejectedPurchaseDoesNotInventAppealOrAStableId()
        {
            var layout = new ShopLayout(); var model = Cash(15);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.CoralPlanterId, null, -4, -1, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.InsufficientFunds));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.CoralPlanterId, null, 0, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.ProtectedAccess));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(ShopAppeal.FromLayout(layout).Points, Is.Zero);
        }

        [Test]
        public void DecorPurchaseLeavesAnAcceptedPriceAndTheExactResidentUntouched()
        {
            var layout = new ShopLayout(); var model = Cash(16);
            Assert.That(model.Collect(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Flow).Success, Is.True);
            Assert.That(model.BeginIncubation().Success, Is.True); model.Tick(10);
            Assert.That(model.TransferToTank("tank-a").Success, Is.True);
            Assert.That(model.ReserveTank("tank-a", "already-agreed").Success, Is.True);
            var claim = model.GetReservation("tank-a").Value;
            Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(claim.Price));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(claim.SpecimenId));
            Assert.That(model.SellReservedTank("tank-a", "already-agreed", claim.SpecimenId).Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [TestCase(1, ShopLayout.CoralPlanterId)]
        [TestCase(1, ShopLayout.BubbleLampId)]
        [TestCase(1, ShopLayout.SillyStatueId)]
        [TestCase(2, ShopLayout.CoralPlanterId)]
        [TestCase(2, ShopLayout.BubbleLampId)]
        [TestCase(2, ShopLayout.SillyStatueId)]
        public void HistoricalLayoutVersionsCannotAcquireNewCatalogEntries(int version, string definition)
        {
            var layout = new ShopLayout(); var model = Cash(100);
            Buy(layout, model, definition, -4, -1);
            var save = layout.CaptureSave(); save.version = version;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
        }

        [Test]
        public void PriorLayoutTwoMigratesWithoutLosingDisplaysAndCurrentRestoreDerivesAppeal()
        {
            var layout = new ShopLayout(); var model = Cash(124);
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3);
            var oldSave = layout.CaptureSave(); oldSave.version = 2;
            Assert.That(ShopLayout.TryRestore(oldSave, out var migrated), Is.True);
            Assert.That(migrated.CaptureSave().version, Is.EqualTo(ShopLayout.SaveVersion));
            Assert.That(migrated.TankIds, Is.EqualTo(layout.TankIds));
            Assert.That(ShopAppeal.FromLayout(migrated).Points, Is.Zero);
            var decorated = ThreeStoreTypes(out _);
            var save = decorated.CaptureSave();
            Assert.That(ShopLayout.TryRestore(JsonUtility.FromJson<LayoutSave>(JsonUtility.ToJson(save)), out var restored), Is.True);
            Assert.That(ShopAppeal.FromLayout(restored).Points, Is.EqualTo(30));
            save.items[0].definitionId = "invented";
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            Assert.That(ShopAppeal.FromLayout(restored).Points, Is.EqualTo(30));
        }

        [Test]
        public void NoDecorKeepsAllReputationBehaviorAndExistingPremiumAccessExactly()
        {
            var appeal = ShopAppeal.FromLayout(new ShopLayout());
            foreach (int points in new[] { 0, 10, 30, 100 })
            {
                var rep = Reputation(points); var source = CustomerArchetypes.CreateDefault();
                AssertCatalogEqual(appeal.ApplyTo(source, rep), rep.ApplyTo(source));
                Assert.That(appeal.ArrivalSeconds(rep), Is.EqualTo(rep.ArrivalSeconds));
            }
            Assert.That(CustomerArchetypes.Find(appeal.ApplyTo(CustomerArchetypes.CreateDefault(), new ShopReputation()),
                CustomerArchetypes.Premium).Weight, Is.EqualTo(10), "Big spenders were already available in the starter shop.");
        }

        [TestCase(0, 0, 0, 0, 4f)]
        [TestCase(1, 5, 1, 5, 3.75f)]
        [TestCase(2, 10, 2, 10, 3.5f)]
        [TestCase(3, 15, 3, 15, 3.25f)]
        public void AppealComposesAdditivelyWithTopReputationWithinDeclaredCaps(int tier, int tolerance, int budget, int weight, float seconds)
        {
            var layout = new ShopLayout(); var model = Cash(72);
            if (tier > 0) Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            if (tier > 1) Buy(layout, model, ShopLayout.BubbleLampId, 4, -1);
            if (tier > 2) Buy(layout, model, ShopLayout.SillyStatueId, 3, -5);
            var appeal = ShopAppeal.FromLayout(layout); var rep = Reputation(100);
            Assert.That(appeal.Points, Is.EqualTo(10 * tier)); Assert.That(appeal.Tier, Is.EqualTo(tier));
            Assert.That(appeal.ToleranceBonusPercent, Is.EqualTo(tolerance));
            Assert.That(appeal.BudgetBonus, Is.EqualTo(budget)); Assert.That(appeal.PremiumWeightBonus, Is.EqualTo(weight));
            Assert.That(appeal.ArrivalSeconds(rep), Is.EqualTo(seconds));
            var adjusted = appeal.ApplyTo(CustomerArchetypes.CreateDefault(), rep);
            var casual = CustomerArchetypes.Find(adjusted, CustomerArchetypes.Casual);
            var premium = CustomerArchetypes.Find(adjusted, CustomerArchetypes.Premium);
            Assert.That(casual.TolerancePercent, Is.EqualTo(170 + tolerance));
            Assert.That(casual.Budget, Is.EqualTo(34 + budget));
            Assert.That(casual.Weight, Is.EqualTo(30), "Appeal does not replace reputation's other arrival weights.");
            Assert.That(premium.Weight, Is.EqualTo(25 + weight));
            Assert.That(premium.Budget, Is.EqualTo(64 + budget));
            Assert.That(premium.TolerancePercent, Is.EqualTo(270 + tolerance));
        }

        [Test]
        public void RepeatedApplicationStartsFromBaseCopiesAndNeverMutatesExistingCustomerDefinitions()
        {
            var appeal = ShopAppeal.FromLayout(ThreeStoreTypes(out _)); var rep = Reputation(30);
            var source = CustomerArchetypes.CreateDefault();
            var before = Array.ConvertAll(source, type => type.Copy());
            var first = appeal.ApplyTo(source, rep); var second = appeal.ApplyTo(source, rep);
            AssertCatalogEqual(first, second); AssertCatalogEqual(source, before);
            var existingVisitType = CustomerArchetypes.Find(first, CustomerArchetypes.Collector);
            first[2].FavoriteSpecies[0] = "changed"; first[2].Budget = 1;
            Assert.That(existingVisitType.FavoriteSpecies[0], Is.EqualTo("bubblebelly"));
            Assert.That(existingVisitType.Budget, Is.EqualTo(47));
            Assert.That(second[2].FavoriteSpecies[0], Is.EqualTo("bubblebelly"));
            Assert.That(source[2].Budget, Is.EqualTo(40));
        }

        [Test]
        public void DisabledPremiumStaysDisabledAndUnknownWeightsArePreservedWhileValuesSaturate()
        {
            var appeal = ShopAppeal.FromLayout(ThreeStoreTypes(out _));
            var catalog = new[]
            {
                new CustomerArchetype { Id = CustomerArchetypes.Premium, DisplayName = "Disabled", Weight = 0 },
                new CustomerArchetype { Id = "custom", DisplayName = "Custom", Weight = 1000, Budget = int.MaxValue - 1,
                    TolerancePercent = 999, FavoriteTolerancePercent = 1000 }
            };
            var result = appeal.ApplyTo(catalog, Reputation(100));
            Assert.That(result[0].Weight, Is.Zero);
            Assert.That(result[1].Weight, Is.EqualTo(1000));
            Assert.That(result[1].Budget, Is.EqualTo(int.MaxValue));
            Assert.That(result[1].TolerancePercent, Is.EqualTo(1000));
            Assert.That(result[1].FavoriteTolerancePercent, Is.EqualTo(1000));
            Assert.That(catalog[1].Budget, Is.EqualTo(int.MaxValue - 1));
        }

        [Test]
        public void InvalidInputsCannotManufactureDerivedDemand()
        {
            Assert.Throws<ArgumentNullException>(() => ShopAppeal.FromLayout(null));
            var appeal = ShopAppeal.FromLayout(new ShopLayout());
            Assert.Throws<ArgumentNullException>(() => appeal.ArrivalSeconds(null));
            Assert.Throws<ArgumentNullException>(() => appeal.ApplyTo(CustomerArchetypes.CreateDefault(), null));
            Assert.Throws<ArgumentException>(() => appeal.ApplyTo(null, new ShopReputation()));
            Assert.That(ShopLayout.IsDecor(null), Is.False); Assert.That(ShopLayout.IsDecor(ShopLayout.TableDefinitionId), Is.False);
            Assert.That(ShopLayout.DefinitionSlotCount(ShopLayout.CoralPlanterId), Is.Zero);
            Assert.That(ShopLayout.DefinitionSlotCount(ShopLayout.BubbleLampId), Is.Zero);
            Assert.That(ShopLayout.DefinitionSlotCount(ShopLayout.SillyStatueId), Is.Zero);
        }

        private static ShopLayout ThreeStoreTypes(out GameModel model)
        {
            var layout = new ShopLayout(); model = Cash(72);
            Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            Buy(layout, model, ShopLayout.BubbleLampId, 4, -1);
            Buy(layout, model, ShopLayout.SillyStatueId, 3, -5); return layout;
        }
        private static ShopReputation Reputation(int points)
        {
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = 1, points = points }, out var rep), Is.True); return rep;
        }
        private static void AssertCatalogEqual(CustomerArchetype[] actual, CustomerArchetype[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < actual.Length; i++) Assert.That(JsonUtility.ToJson(actual[i]), Is.EqualTo(JsonUtility.ToJson(expected[i])));
        }
        private static string State(ShopLayout layout, GameModel model)
            => JsonUtility.ToJson(layout.CaptureSave()) + JsonUtility.ToJson(model.CaptureSave());
        private static void Buy(ShopLayout layout, GameModel model, string definition, int x, int z)
        {
            var result = layout.TryCommit(model, ShopDayPhase.Night, definition, null, x, z, 0, out _);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
        }
        private static GameModel Cash(int amount)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.currency = amount;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
    }
}
