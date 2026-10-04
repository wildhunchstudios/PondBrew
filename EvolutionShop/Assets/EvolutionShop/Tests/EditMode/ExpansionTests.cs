using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ExpansionTests
    {
        [Test]
        public void TwoWestExpansionsChargeOnceAndLeaveLabAndExistingOwnershipAlone()
        {
            var layout = new ShopLayout(); var model = Cash(192);
            Buy(layout, model, ShopLayout.TableDefinitionId, -4, -1, 0);
            var snapshot = layout.Items;
            Assert.That(layout.TryPreviewExpansion(out var preview).Success, Is.True);
            Assert.That(preview.StoreTier, Is.EqualTo(1));
            Assert.That(layout.StoreTier, Is.Zero);
            Assert.That(model.Currency, Is.EqualTo(180));
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Assert.That(layout.StoreMinX, Is.EqualTo(-11));
            Assert.That(layout.NextExpansionPrice, Is.EqualTo(120));
            Assert.That(model.Currency, Is.EqualTo(120));
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Assert.That(layout.StoreMinX, Is.EqualTo(-15));
            Assert.That(layout.NextExpansionPrice, Is.Zero);
            Assert.That(model.Currency, Is.Zero);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Failure, Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(layout.GetItem("table-1").Value.X, Is.EqualTo(snapshot[0].X));
            Assert.That(layout.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" }));
            Assert.That(layout.CanReachMainAisle(-9, -2.5f), Is.True);
            Assert.That(layout.CanReachMainAisle(-13, -2.5f), Is.True);
            Assert.That(layout.CanReachMainAisle(-9, 2), Is.False, "Expansion never adds lab floor.");
            Assert.That(layout.ValidatePlacement(null, -18, 4, 0).Failure, Is.EqualTo(LayoutFailure.OutOfBounds));
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        [TestCase((ShopDayPhase)17)]
        public void ExpansionAndDisplayPurchasesRequireNightWithoutSideEffects(ShopDayPhase phase)
        {
            var layout = new ShopLayout(); var model = Cash(100);
            string before = State(layout, model);
            Assert.That(layout.TryExpand(model, phase).Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(layout.TryCommit(model, phase, ShopLayout.SmallDisplayId, null, 3, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void InsufficientMoneyUnknownDefinitionsAndPrematureLargeDisplayAreAtomic()
        {
            var layout = new ShopLayout(); var model = Cash(23);
            string before = State(layout, model);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Failure, Is.EqualTo(LayoutFailure.InsufficientFunds));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.SmallDisplayId, null, 3, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.InsufficientFunds));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "missing", null, 3, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.UnknownDefinition));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.LargeDisplayId, null, -18, -3, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LockedDefinition));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void SeparateTableAndDisplayLimitsKeepSixStableIdentitiesAndSevenIndependentSlots()
        {
            var layout = new ShopLayout(); var model = Cash(400);
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5, 0);
            Assert.That(layout.TryPreview(ShopLayout.SmallDisplayId, null, -4, -1, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3, 0);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -26, -3, 0);
            Buy(layout, model, ShopLayout.TableDefinitionId, -4, -1, 0);
            Buy(layout, model, ShopLayout.TableDefinitionId, 4, -1, 0);
            Buy(layout, model, ShopLayout.TableDefinitionId, 9, 4, 1);
            Assert.That(layout.Items.Count, Is.EqualTo(6));
            Assert.That(layout.DisplayLimit, Is.EqualTo(3));
            Assert.That(model.Currency, Is.EqualTo(80));
            Assert.That(layout.TankIds, Is.EqualTo(model.TankIds));
            Assert.That(model.TankIds.Length, Is.EqualTo(7));
            Assert.That(layout.GetSlotIds("table-2"), Is.EqualTo(new[] { "table-2-slot-1", "table-2-slot-2" }));
            Assert.That(layout.GetSlotIds("table-4"), Is.Empty);
            Assert.That(layout.GetSlotIds("unknown"), Is.Empty);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, null, -26, -8, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.SmallDisplayId, null, -26, -8, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void NewSlotsPreserveAnExistingExactReservationAndActiveIncubation()
        {
            var layout = new ShopLayout(); var model = Cash(24);
            Produce(model, "tank-a");
            Assert.That(model.ReserveTank("tank-a", "buyer").Success, Is.True);
            var reservation = model.GetReservation("tank-a").Value;
            Assert.That(model.SetPrice("pondlet", 12).Success, Is.True);
            BeginProduction(model); model.Tick(3);
            var incubator = model.Incubator;
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5, 0);
            Assert.That(model.Currency, Is.Zero);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(reservation.SpecimenId));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(6));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(incubator.SpecimenId));
            Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(incubator.SecondsRemaining));
            Assert.That(model.GetTank("table-1-slot-1"), Is.Null);
            Assert.That(model.SellReservedTank("tank-a", "buyer", reservation.SpecimenId).Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void LargeSlotsPaySeparatelyAndAnyRemainingStockBlocksTheWholeDisplayMove()
        {
            var layout = new ShopLayout(); var model = Cash(100);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3, 0);
            string[] slots = layout.GetSlotIds("table-1");
            Produce(model, slots[0]); Produce(model, slots[1]);
            Assert.That(model.ReserveTank(slots[0], "first").Success, Is.True);
            Assert.That(model.ReserveTank(slots[1], "second").Success, Is.True);
            var first = model.GetReservation(slots[0]).Value;
            var second = model.GetReservation(slots[1]).Value;
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Failure,
                Is.EqualTo(LayoutFailure.StockedDisplay));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(model.GetReservation(slots[1]).Value.CustomerId, Is.EqualTo("second"));
            Assert.That(model.SellReservedTank(slots[0], "first", first.SpecimenId).Success, Is.True);
            Assert.That(model.ReleaseReservation(slots[1], "second").Success, Is.True);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Failure,
                Is.EqualTo(LayoutFailure.StockedDisplay), "Unclaimed stock still belongs to the display.");
            Assert.That(model.ReserveTank(slots[1], "second").Success, Is.True);
            Assert.That(model.SellReservedTank(slots[1], "second", second.SpecimenId).Success, Is.True);
            Assert.That(model.SellReservedTank(slots[1], "second", second.SpecimenId).Success, Is.False);
            string gameBeforeMove = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Success, Is.True);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(gameBeforeMove));
            Assert.That(model.Currency, Is.EqualTo(12));
        }

        [Test]
        public void LargeDisplaySlotsShareRotatedBrowsePointsAndCapturedArraysCannotMutateAuthority()
        {
            var layout = new ShopLayout(); var model = Cash(100);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3, 0);
            Assert.That(layout.TryGetBrowsePoint("table-1-slot-1", 0, out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(-9.45f).Within(.0001f)); Assert.That(z, Is.EqualTo(-2.9f).Within(.0001f));
            Assert.That(layout.TryGetBrowsePoint("table-1-slot-2", 0, out float secondX, out float secondZ), Is.True);
            Assert.That(secondX, Is.EqualTo(x)); Assert.That(secondZ, Is.EqualTo(z));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Success, Is.True);
            Assert.That(layout.TryGetBrowsePoint("table-1-slot-1", 0, out x, out z), Is.True);
            Assert.That(x, Is.EqualTo(-8.55f).Within(.0001f)); Assert.That(z, Is.EqualTo(-2.1f).Within(.0001f));
            Assert.That(layout.TryGetBrowsePoint("invented", 0, out _, out _), Is.False);
            Assert.That(layout.TryGetBrowsePoint("tank-a", 2, out _, out _), Is.False);
            model.TankIds[0] = "changed"; layout.TankIds[0] = "changed";
            Assert.That(model.TankIds[0], Is.EqualTo("tank-a")); Assert.That(layout.TankIds[0], Is.EqualTo("tank-a"));
        }

        [Test]
        public void RestoreUsesLayoutOwnedSlotsKeepsSpecimensAndCancelsOnlyTransientClaims()
        {
            var layout = new ShopLayout(); var model = Cash(24);
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5, 0);
            Produce(model, "table-1-slot-1");
            Assert.That(model.ReserveTank("table-1-slot-1", "unpaid").Success, Is.True);
            var specimen = model.GetTank("table-1-slot-1").Value;
            var layoutSave = layout.CaptureSave(); var gameSave = model.CaptureSave();
            Assert.That(ShopLayout.TryRestore(layoutSave, out var restoredLayout), Is.True);
            var config = restoredLayout.WithTankIds(GameConfig.CreateDefault());
            Assert.That(GameModel.TryRestore(config, gameSave, out var restored, out _), Is.True);
            Assert.That(restored.GetTank("table-1-slot-1").Value.Id, Is.EqualTo(specimen.Id));
            Assert.That(restored.GetReservation("table-1-slot-1"), Is.Null);
            Assert.That(restored.Currency, Is.Zero);
            Assert.That(GameModel.TryRestore(GameConfig.CreateDefault(), gameSave, out _, out _), Is.False);
            gameSave.tanks[2].tankId = "unowned-slot";
            Assert.That(GameModel.TryRestore(config, gameSave, out _, out _), Is.False);
            Assert.That(restoredLayout.TryCommit(Cash(100), ShopDayPhase.Night, "table-1", 3, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.ModelMismatch));
        }

        [Test]
        public void WithTankIdsDeepCopiesCustomTuningAndDuplicateResidentsStillReject()
        {
            var layout = new ShopLayout(); var model = Cash(24);
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5, 0);
            var original = GameConfig.CreateDefault(); original.IncubationSeconds = 25; original.Species[0].SaleValue = 9;
            var derived = layout.WithTankIds(original);
            Assert.That(derived.IncubationSeconds, Is.EqualTo(25)); Assert.That(derived.Species[0].SaleValue, Is.EqualTo(9));
            derived.Species[0].SaleValue = 10; derived.TankIds[0] = "changed";
            Assert.That(original.Species[0].SaleValue, Is.EqualTo(9)); Assert.That(original.TankIds.Length, Is.EqualTo(2));
            Produce(model, "tank-a"); Produce(model, "table-1-slot-1");
            var save = model.CaptureSave(); save.tanks[2].specimenId = save.tanks[0].specimenId;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(GameConfig.CreateDefault()), save, out _, out _), Is.False);
        }

        [Test]
        public void LegacyLayoutOnePreservesTablesButCannotSmuggleExpansionOrDisplays()
        {
            var layout = new ShopLayout(); var model = Cash(12);
            Buy(layout, model, ShopLayout.TableDefinitionId, -4, -1, 0);
            var save = layout.CaptureSave(); save.version = 1;
            Assert.That(ShopLayout.TryRestore(save, out var migrated), Is.True);
            Assert.That(migrated.GetItem("table-1").Value.X, Is.EqualTo(-4));
            Assert.That(migrated.CaptureSave().version, Is.EqualTo(ShopLayout.SaveVersion));
            save.storeTier = 1; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.storeTier = 0; save.items[0].definitionId = ShopLayout.SmallDisplayId;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
        }

        [TestCase("tier below")]
        [TestCase("tier above")]
        [TestCase("large locked")]
        [TestCase("unowned wing")]
        [TestCase("lab display")]
        [TestCase("duplicate id")]
        [TestCase("wrong counter")]
        public void MalformedExpansionOwnershipAndGeometryReject(string fault)
        {
            var layout = new ShopLayout(); var model = Cash(124);
            Buy(layout, model, ShopLayout.SmallDisplayId, 3, -5, 0);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3, 0);
            var save = layout.CaptureSave();
            switch (fault)
            {
                case "tier below": save.storeTier = -1; break;
                case "tier above": save.storeTier = 3; break;
                case "large locked": save.storeTier = 0; break;
                case "unowned wing": save.items[1].x = -26; break;
                case "lab display": save.items[0].x = 9; save.items[0].z = 4; save.items[0].quarterTurns = 1; break;
                case "duplicate id": save.items[1].id = save.items[0].id; break;
                case "wrong counter": save.nextItemNumber++; break;
            }
            Assert.That(ShopLayout.TryRestore(save, out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        private static string State(ShopLayout layout, GameModel model)
            => JsonUtility.ToJson(layout.CaptureSave()) + JsonUtility.ToJson(model.CaptureSave());
        private static void Buy(ShopLayout layout, GameModel model, string definition, int x, int z, int turns)
        {
            var result = layout.TryCommit(model, ShopDayPhase.Night, definition, null, x, z, turns, out _);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
        }
        private static GameModel Cash(int amount)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.currency = amount;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
        private static void BeginProduction(GameModel model)
        {
            Assert.That(model.Collect(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Flow).Success, Is.True);
            Assert.That(model.BeginIncubation().Success, Is.True);
        }
        private static void Produce(GameModel model, string slot)
        {
            BeginProduction(model); model.Tick(10);
            Assert.That(model.TransferToTank(slot).Success, Is.True);
        }
    }
}

