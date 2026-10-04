using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class SpecimenStorageTests
    {
        static readonly string[] Slots = { "table-1-storage-1", "table-1-storage-2" };

        [Test]
        public void ProductionStorageDisplayAndSaleMoveOneIdentityWithoutCreatingIncomeOrCustomerStock()
        {
            var model = StoredModel();
            string id = Produce(model);
            Ok(model.StoreReadyOutput(Slots[0]));
            Assert.That(model.GetStorage(Slots[0]).Value.Id, Is.EqualTo(id));
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing));
            Assert.That(model.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" }));
            Assert.That(model.GetTank(Slots[0]), Is.Null);
            Assert.That(model.ReserveTank(Slots[0], "visitor").Failure, Is.EqualTo(FailureCode.UnknownTank));
            Assert.That(model.SellFromTank(Slots[0]).Failure, Is.EqualTo(FailureCode.UnknownTank));
            Ok(model.TransferStorageToTank(Slots[0], "tank-a", id));
            Assert.That(model.GetStorage(Slots[0]), Is.Null);
            Assert.That(model.TransferStorageToTank(Slots[0], "tank-a", id).Success, Is.False);
            Ok(model.ReturnTankToStorage("tank-a", Slots[1], id));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.Currency, Is.Zero);
            Ok(model.SetPrice("pondlet", 11));
            Ok(model.TransferStorageToTank(Slots[1], "tank-b", id));
            Ok(model.ReserveTank("tank-b", "visitor"));
            Assert.That(model.GetReservation("tank-b").Value.Price, Is.EqualTo(11));
            Ok(model.SellReservedTank("tank-b", "visitor", id));
            Assert.That(model.Currency, Is.EqualTo(11));
            Assert.That(model.ReturnTankToStorage("tank-b", Slots[0], id).Failure, Is.EqualTo(FailureCode.TankEmpty));
        }

        [Test]
        public void FullRackKeepsNewReadyOutputAndStaleCommandsCannotSwapOrDuplicateResidents()
        {
            var model = StoredModel();
            string first = Produce(model); Ok(model.StoreReadyOutput(Slots[0]));
            string second = Produce(model); Ok(model.StoreReadyOutput(Slots[1]));
            string ready = Produce(model);
            string before = State(model);
            Assert.That(model.StoreReadyOutput(Slots[0]).Failure, Is.EqualTo(FailureCode.StorageFull));
            Assert.That(model.StoreReadyOutput("missing").Failure, Is.EqualTo(FailureCode.UnknownStorage));
            Assert.That(model.TransferStorageToTank(Slots[0], "tank-a", second).Failure, Is.EqualTo(FailureCode.SpecimenMismatch));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(ready));
            Ok(model.TransferStorageToTank(Slots[0], "tank-a", first));
            before = State(model);
            Assert.That(model.ReturnTankToStorage("tank-a", Slots[1], first).Failure, Is.EqualTo(FailureCode.StorageFull));
            Assert.That(model.TransferStorageToTank(Slots[1], "tank-a", second).Failure, Is.EqualTo(FailureCode.TankFull));
            Assert.That(model.ReturnTankToStorage("tank-a", Slots[0], null).Failure, Is.EqualTo(FailureCode.SpecimenMismatch));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.StoreReadyOutput(Slots[0]));
            Assert.That(model.GetStorage(Slots[0]).Value.Id, Is.EqualTo(ready));
            Assert.That(model.GetStorage(Slots[1]).Value.Id, Is.EqualTo(second));
        }

        [Test]
        public void ExistingCustomerAgreementBlocksBothTransferDirectionsAndKeepsItsLockedPrice()
        {
            var model = StoredModel();
            string sold = Produce(model); Ok(model.TransferToTank("tank-a"));
            Ok(model.ReserveTank("tank-a", "queued"));
            string stored = Produce(model); Ok(model.StoreReadyOutput(Slots[0]));
            Ok(model.SetPrice("pondlet", 14));
            string before = State(model);
            Assert.That(model.ReturnTankToStorage("tank-a", Slots[1], sold).Failure, Is.EqualTo(FailureCode.TankReserved));
            Assert.That(model.TransferStorageToTank(Slots[0], "tank-a", stored).Failure, Is.EqualTo(FailureCode.TankReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.GetReservation("tank-a").Value.SpecimenId, Is.EqualTo(sold));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(6));
            Ok(model.SellReservedTank("tank-a", "queued", sold));
            Ok(model.TransferStorageToTank(Slots[0], "tank-a", stored));
            Assert.That(model.GetPrice("pondlet").Value.Price, Is.EqualTo(14));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void UnknownAndEmptyLocationsRejectWithoutTouchingProduction()
        {
            var model = StoredModel();
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom));
            string before = State(model);
            Assert.That(model.StoreReadyOutput(Slots[0]).Failure, Is.EqualTo(FailureCode.OutputNotReady));
            Assert.That(model.TransferStorageToTank(Slots[0], "tank-a", "stale").Failure, Is.EqualTo(FailureCode.StorageEmpty));
            Assert.That(model.TransferStorageToTank(null, "tank-a", "stale").Failure, Is.EqualTo(FailureCode.UnknownStorage));
            Assert.That(model.ReturnTankToStorage(null, Slots[0], "stale").Failure, Is.EqualTo(FailureCode.UnknownTank));
            Assert.That(model.ReturnTankToStorage("tank-a", null, "stale").Failure, Is.EqualTo(FailureCode.UnknownStorage));
            Assert.That(model.ReturnTankToStorage("tank-a", Slots[0], "stale").Failure, Is.EqualTo(FailureCode.TankEmpty));
            Assert.That(State(model), Is.EqualTo(before));
        }

        [Test]
        public void StockedRackMovesButCannotBeStoredOrResoldUntilItsExactResidentsLeave()
        {
            var layout = new ShopLayout(); var model = Cash(32);
            Buy(layout, model, 9, 7, 0);
            string id = Produce(model); Ok(model.StoreReadyOutput(Slots[0]));
            var before = model.CaptureSave();
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 11, 7, 0, out _).Success, Is.True);
            Assert.That(State(model), Is.EqualTo(JsonUtility.ToJson(before)), "A free move preserves all simulation state.");
            string all = State(layout, model);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.StockedStorage));
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.StockedStorage));
            Assert.That(State(layout, model), Is.EqualTo(all));
            Ok(model.TransferStorageToTank(Slots[0], "tank-a", id));
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(model.StorageIds, Is.Empty); Assert.That(layout.StorageIds, Is.Empty);
            Assert.That(layout.GetStorageSlotIds("table-1"), Is.EqualTo(Slots));
            Assert.That(layout.TryGetStorageAccessPoint("table-1", out _, out _), Is.False);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 7, 0, out _).Success, Is.True);
            Assert.That(model.StorageIds, Is.EqualTo(Slots)); Assert.That(model.Currency, Is.Zero);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(16)); Assert.That(model.StorageIds, Is.Empty);
            all = State(layout, model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.False);
            Assert.That(State(layout, model), Is.EqualTo(all));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OwnedRackLimitTracksLabTierIncludingStoredFurnitureWithoutAddingCustomerSlots(int tier)
        {
            var layout = new ShopLayout(); var model = Cash(1000);
            for (int i = 0; i < tier; i++) Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Success, Is.True);
            for (int i = 0; i <= tier; i++)
            {
                string rack = Buy(layout, model, 9, 7, 0);
                Assert.That(layout.TryStore(model, ShopDayPhase.Night, rack).Success, Is.True);
            }
            string before = State(layout, model);
            Assert.That(layout.RackCount, Is.EqualTo(tier + 1));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, 7, 0, out _).Failure, Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(model.TankIds.Length, Is.EqualTo(2)); Assert.That(model.StorageIds, Is.Empty);
            Assert.That(layout.AppealPoints, Is.Zero);
        }

        [Test]
        public void RackFootprintAndRotatedOperatorRemainInsideReachableLabAndFailuresAreAtomic()
        {
            var layout = new ShopLayout(); var model = Cash(1000);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Open, ShopLayout.RackDefinitionId, null, 9, 7, 0, out _).Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            foreach (var pose in new[] { new[] { 3, -5, 0 }, new[] { -8, 8, 0 }, new[] { 0, 3, 0 }, new[] { 9, 4, 0 }, new[] { 9, int.MaxValue, 0 } })
                Assert.That(layout.TryPreview(ShopLayout.RackDefinitionId, null, pose[0], pose[1], pose[2], out _).Success, Is.False);
            Assert.That(State(layout, model), Is.EqualTo(before));
            Buy(layout, model, 9, 4, 1);
            Assert.That(layout.TryGetStorageAccessPoint("table-1", out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(3.1f).Within(.0001)); Assert.That(z, Is.EqualTo(2f));
            Assert.That(layout.CanReachMainAisle(x, z), Is.True);
            Assert.That(layout.TryGetBrowsePoint(Slots[0], 0, out _, out _), Is.False);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 4, 3, out _).Success, Is.True);
            layout.TryGetStorageAccessPoint("table-1", out x, out z);
            Assert.That(x, Is.EqualTo(5.9f).Within(.0001)); Assert.That(z, Is.EqualTo(2f));
            Assert.That(layout.CanReachMainAisle(x, z), Is.True);
        }

        [Test]
        public void RackPurchaseAndSaleRejectCurrencyFailuresWithoutPublishingEitherRegistry()
        {
            var layout = new ShopLayout(); var model = Cash(31);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, 7, 0, out _).Failure, Is.EqualTo(LayoutFailure.InsufficientFunds));
            Assert.That(State(layout, model), Is.EqualTo(before));
            model = Cash(32); Buy(layout, model, 9, 7, 0);
            var save = model.CaptureSave(); save.currency = int.MaxValue;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(GameConfig.CreateDefault()), save, out model, out _), Is.True);
            before = State(layout, model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.CurrencyOverflow));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void AllFifteenOwnedItemsRestoreButHistoricalLayoutsCannotSmuggleRacksOrExtraOwnership()
        {
            var layout = new ShopLayout(); var model = Cash(3000);
            for (int i = 0; i < 2; i++) { Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True); Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Success, Is.True); }
            foreach (var definition in new[] { ShopLayout.TableDefinitionId, ShopLayout.SmallDisplayId, ShopLayout.CoralPlanterId, ShopLayout.RackDefinitionId })
            {
                int count = definition == ShopLayout.CoralPlanterId ? 6 : 3;
                for (int i = 0; i < count; i++)
                {
                    bool rack = definition == ShopLayout.RackDefinitionId;
                    int x = rack ? 9 : definition == ShopLayout.SmallDisplayId ? 3 : -4;
                    int z = rack ? 7 : definition == ShopLayout.SmallDisplayId ? -5 : -1;
                    var result = layout.TryCommit(model, ShopDayPhase.Night, definition, null, x, z, 0, out string id);
                    Assert.That(result.Success, Is.True, result.Failure.ToString());
                    Assert.That(layout.TryStore(model, ShopDayPhase.Night, id).Success, Is.True);
                }
            }
            Assert.That(layout.Items.Count, Is.EqualTo(15));
            Assert.That(ShopLayout.TryRestore(layout.CaptureSave(), out _), Is.True);
            for (int version = 1; version <= 5; version++)
            {
                var legacy = layout.CaptureSave(); legacy.version = version;
                Assert.That(ShopLayout.TryRestore(legacy, out _), Is.False);
                var minimal = new ShopLayout(); var cash = Cash(32); Buy(minimal, cash, 9, 7, 0);
                legacy = minimal.CaptureSave(); legacy.version = version;
                Assert.That(ShopLayout.TryRestore(legacy, out _), Is.False, "Historical version " + version);
            }
        }

        [TestCase(1)] [TestCase(2)]
        public void LegacyGameSavesMigrateOnlyWithoutStorageAndDoNotInventCapacity(int version)
        {
            var config = GameConfig.CreateDefault(); var model = new GameModel(config);
            Produce(model); Ok(model.TransferToTank("tank-a"));
            var legacy = model.CaptureSave(); legacy.version = version; legacy.storage = null;
            Array.Resize(ref legacy.growers, 2); // Historical storage migration must start with the actual old culture shape.
            Assert.That(GameModel.TryRestore(config, legacy, out var restored, out _), Is.True);
            Assert.That(restored.StorageIds, Is.Empty); Assert.That(restored.CaptureSave().storage, Is.Empty);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(model.GetTank("tank-a").Value.Id));
            config.StorageIds = (string[])Slots.Clone();
            Assert.That(GameModel.TryRestore(config, legacy, out _, out _), Is.False);
            config.StorageIds = Array.Empty<string>();
            legacy.storage = new[] { new StorageSave { storageId = Slots[0] } };
            Assert.That(GameModel.TryRestore(config, legacy, out _, out _), Is.False);
        }

        [Test]
        public void CurrentRoundTripKeepsReadyOutputStoredSpecimensTagsAndUnpaidDisplayStockWithoutAliasing()
        {
            var config = StorageConfig(); var model = new GameModel(config);
            string display = Produce(model); Ok(model.TransferToTank("tank-a")); Ok(model.ReserveTank("tank-a", "unpaid"));
            string stored = Produce(model); Ok(model.StoreReadyOutput(Slots[0]));
            string ready = Produce(model); Ok(model.SetPrice("pondlet", 12));
            var save = model.CaptureSave();
            var decoded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.That(GameModel.TryRestore(config, decoded, out var restored, out _), Is.True);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(display)); Assert.That(restored.GetReservation("tank-a"), Is.Null);
            Assert.That(restored.GetStorage(Slots[0]).Value.Id, Is.EqualTo(stored)); Assert.That(restored.Incubator.SpecimenId, Is.EqualTo(ready));
            Assert.That(restored.GetPrice("pondlet").Value.Price, Is.EqualTo(12));
            decoded.storage[0].specimenId = "modified"; save.storage[0].storageId = "modified";
            config.StorageIds[0] = "modified"; var snapshot = model.StorageIds; snapshot[0] = "modified";
            Assert.That(model.StorageIds[0], Is.EqualTo(Slots[0])); Assert.That(restored.GetStorage(Slots[0]).Value.Id, Is.EqualTo(stored));
            Assert.That(model.GetStorage(Slots[0]).Value.Id, Is.EqualTo(stored));
        }

        [TestCase("missing")] [TestCase("null-entry")] [TestCase("duplicate-slot")] [TestCase("unknown-slot")]
        [TestCase("duplicate-resident")] [TestCase("display-identity")] [TestCase("output-identity")]
        [TestCase("missing-species")] [TestCase("unknown-species")] [TestCase("undiscovered-species")]
        public void CorruptStorageRejectsTheWholeSave(string fault)
        {
            var config = StorageConfig(); var model = new GameModel(config);
            Produce(model); Ok(model.TransferToTank("tank-a"));
            Produce(model); Ok(model.StoreReadyOutput(Slots[0]));
            Produce(model); Ok(model.StoreReadyOutput(Slots[1])); Produce(model);
            var save = model.CaptureSave();
            switch (fault)
            {
                case "missing": save.storage = null; break;
                case "null-entry": save.storage[0] = null; break;
                case "duplicate-slot": save.storage[1].storageId = save.storage[0].storageId; break;
                case "unknown-slot": save.storage[0].storageId = "unowned"; break;
                case "duplicate-resident": save.storage[1].specimenId = save.storage[0].specimenId; break;
                case "display-identity": save.storage[0].specimenId = save.tanks[0].specimenId; break;
                case "output-identity": save.storage[0].specimenId = save.incubator.specimenId; break;
                case "missing-species": save.storage[0].speciesId = null; break;
                case "unknown-species": save.storage[0].speciesId = "fake"; break;
                case "undiscovered-species": save.storage[0].speciesId = "ribbonfin"; break;
            }
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.False);
            Assert.That(restored, Is.Null); Assert.That(error, Is.EqualTo(FailureCode.InvalidSave));
        }

        [Test]
        public void RegistryValidationRejectsAliasesDuplicatesAndExcessCapacityBeforeModelCreation()
        {
            foreach (var slots in new[] { new[] { "tank-a" }, new[] { "one", "one" }, new[] { " " }, new string[7] })
            {
                var config = GameConfig.CreateDefault(); config.StorageIds = slots;
                Assert.Throws<ArgumentException>(() => new GameModel(config));
            }
            var valid = StorageConfig(); var model = new GameModel(valid); valid.StorageIds[0] = "later mutation";
            Assert.That(model.StorageIds, Is.EqualTo(Slots));
        }

        static GameConfig StorageConfig() { var config = GameConfig.CreateDefault(); config.StorageIds = (string[])Slots.Clone(); return config; }
        static GameModel StoredModel() => new GameModel(StorageConfig());
        static GameModel Cash(int amount)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.currency = amount;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
        static string Buy(ShopLayout layout, GameModel model, int x, int z, int rotation)
        {
            var result = layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, x, z, rotation, out string id);
            Assert.That(result.Success, Is.True, result.Failure.ToString()); return id;
        }
        static string Produce(GameModel model)
        {
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation());
            model.Tick(10); Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready)); return model.Incubator.SpecimenId;
        }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        static string State(GameModel model) => JsonUtility.ToJson(model.CaptureSave());
        static string State(ShopLayout layout, GameModel model) => JsonUtility.ToJson(layout.CaptureSave()) + "|" + State(model);
    }
}
