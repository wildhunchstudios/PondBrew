using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class StockTransferTests
    {
        [Test]
        public void ClaimLeavesExactResidentAtSourceAndCommitMovesItOnceWithoutIncome()
        {
            var model = Fixture(); string before = State(model);
            Ok(model.ReserveStockTransfer("job", "stocker", "rack-1", "tank-a", "p1"));
            Assert.That(State(model), Is.EqualTo(before));
            var claim = model.GetStockTransfer("job").Value;
            Assert.That(claim.JobId, Is.EqualTo("job")); Assert.That(claim.OwnerId, Is.EqualTo("stocker"));
            Assert.That(claim.StorageId, Is.EqualTo("rack-1")); Assert.That(claim.TankId, Is.EqualTo("tank-a"));
            Assert.That(claim.SpecimenId, Is.EqualTo("p1"));
            Assert.That(model.GetStorage("rack-1").Value.Id, Is.EqualTo("p1"));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.IsStorageReservedForStocking("rack-1"), Is.True);
            Assert.That(model.IsTankReservedForStocking("tank-a"), Is.True);
            Ok(model.CompleteStockTransfer("job", "stocker"));
            Assert.That(model.GetStorage("rack-1"), Is.Null);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo("p1"));
            Assert.That(model.Currency, Is.EqualTo(90));
            Assert.That(model.IsStorageReservedForStocking("rack-1"), Is.False);
            Assert.That(model.IsTankReservedForStocking("tank-a"), Is.False);
            Assert.That(model.CompleteStockTransfer("job", "stocker").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Ok(model.ReserveTank("tank-a", "customer"));
            Ok(model.SellReservedTank("tank-a", "customer", "p1"));
            Assert.That(model.Currency, Is.EqualTo(96));
        }

        [Test]
        public void IdenticalClaimIsIdempotentButOtherOwnersJobsAndResourcesCannotStealIt()
        {
            var model = Fixture();
            Ok(model.ReserveStockTransfer("job", "stocker", "rack-1", "tank-a", "p1"));
            Ok(model.ReserveStockTransfer("job", "stocker", "rack-1", "tank-a", "p1"));
            Assert.That(model.ReserveStockTransfer("job", "other", "rack-1", "tank-a", "p1").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.ReserveStockTransfer("job", "stocker", "rack-2", "tank-b", "p2").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.ReserveStockTransfer("job", "stocker", "rack-1", "tank-a", "stale").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.ReserveStockTransfer("job-2", "other", "rack-1", "tank-b", "p1").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.ReserveStockTransfer("job-2", "other", "rack-2", "tank-a", "p2").Failure, Is.EqualTo(FailureCode.StockReserved));
            Ok(model.ReserveStockTransfer("job-2", "other", "rack-2", "tank-b", "p2"));
            Ok(model.CompleteStockTransfer("job", "stocker"));
            Ok(model.CompleteStockTransfer("job-2", "other"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo("p1"));
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo("p2"));
        }

        [TestCase(null, "owner")]
        [TestCase("", "owner")]
        [TestCase(" ", "owner")]
        [TestCase("job", null)]
        [TestCase("job", "")]
        [TestCase("job", " ")]
        public void InvalidClaimIdentityCannotReserveResources(string job, string owner)
        {
            var model = Fixture(); string before = State(model);
            Assert.That(model.ReserveStockTransfer(job, owner, "rack-1", "tank-a", "p1").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.ReserveStockTransfer(new string('j', 129), "owner", "rack-1", "tank-a", "p1").Success, Is.False);
            Assert.That(model.ReserveStockTransfer("job", new string('o', 129), "rack-1", "tank-a", "p1").Success, Is.False);
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.IsTankReservedForStocking("tank-a"), Is.False);
        }

        [Test]
        public void BadSourceTargetAndSpecimenCannotCreateClaims()
        {
            var model = Fixture(); string before = State(model);
            Assert.That(model.ReserveStockTransfer("job", "owner", "absent", "tank-a", "p1").Failure, Is.EqualTo(FailureCode.UnknownStorage));
            Assert.That(model.ReserveStockTransfer("job", "owner", "rack-1", "absent", "p1").Failure, Is.EqualTo(FailureCode.UnknownTank));
            Assert.That(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "stale").Failure, Is.EqualTo(FailureCode.SpecimenMismatch));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.GetStockTransfer("job"), Is.Null);
            Ok(model.TransferStorageToTank("rack-2", "tank-a", "p2"));
            Assert.That(model.ReserveStockTransfer("job", "owner", "rack-2", "tank-b", "p2").Failure, Is.EqualTo(FailureCode.StorageEmpty));
            Assert.That(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1").Failure, Is.EqualTo(FailureCode.TankFull));
            Ok(model.ReserveTank("tank-a", "customer"));
            Assert.That(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1").Failure, Is.EqualTo(FailureCode.TankReserved));
        }

        [Test]
        public void WrongOwnerCannotCancelOrCompleteAndAuthorizedCancelRestoresManualAccess()
        {
            var model = Fixture(); string before = State(model);
            Ok(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1"));
            Assert.That(model.CancelStockTransfer("job", "intruder"), Is.False);
            Assert.That(model.CompleteStockTransfer("job", "intruder").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.CancelStockTransfer(null, "owner"), Is.False);
            Assert.That(model.GetStockTransfer(null), Is.Null);
            Assert.That(model.IsStorageReservedForStocking(null), Is.False);
            Assert.That(model.IsTankReservedForStocking(null), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.CancelStockTransfer("job", "owner"), Is.True);
            Assert.That(model.CancelStockTransfer("job", "owner"), Is.False);
            Ok(model.TransferStorageToTank("rack-1", "tank-a", "p1"));
        }

        [Test]
        public void ManualTransfersCustomerClaimsAndIncubatorCannotWriteOrConsumeWorkerResources()
        {
            var model = Fixture();
            Ok(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1"));
            string before = State(model);
            Assert.That(model.TransferStorageToTank("rack-1", "tank-b", "p1").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.TransferStorageToTank("rack-2", "tank-a", "p2").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.ReserveTank("tank-a", "customer").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.SellFromTank("tank-a").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferStorageToTank("rack-2", "tank-b", "p2")); before = State(model);
            Assert.That(model.ReturnTankToStorage("tank-b", "rack-1", "p2").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.ReturnTankToStorage("tank-a", "rack-2", "p1").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation()); model.Tick(10);
            before = State(model);
            Assert.That(model.TransferToTank("tank-a").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.StoreReadyOutput("rack-1").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.StoreReadyOutput("rack-2"));
        }

        [TestCase(0)] [TestCase(1)]
        public void ConditioningCannotConsumeAnyClaimedBatchMemberOrItsSpark(int claimedIndex)
        {
            var model = Fixture(); string source = claimedIndex == 0 ? "rack-1" : "rack-2";
            Ok(model.ReserveStockTransfer("job", "owner", source, "tank-a", claimedIndex == 0 ? "p1" : "p2"));
            string before = State(model);
            Assert.That(model.BeginConditioning("vat", new[] { "rack-1", "rack-2" }, new[] { "p1", "p2" }).Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.GetConditioner("vat").Value.Phase, Is.EqualTo(ConditionerPhase.Idle));
            Assert.That(model.CancelStockTransfer("job", "owner"), Is.True);
            Ok(model.BeginConditioning("vat", new[] { "rack-1", "rack-2" }, new[] { "p1", "p2" }));
        }

        [Test]
        public void ReadyConditionerCannotOverwriteAWorkerTargetAndKeepsItsOutput()
        {
            var model = Fixture();
            Ok(model.BeginConditioning("vat", new[] { "rack-2" }, new[] { "p2" })); model.Tick(12);
            Ok(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1")); string before = State(model);
            Assert.That(model.TransferConditionerToTank("vat", 0, "tank-a", "p2").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(model.TransferConditionerToStorage("vat", 0, "rack-1", "p2").Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferConditionerToTank("vat", 0, "tank-b", "p2"));
        }

        [Test]
        public void ClearAndReloadReleaseClaimsWithoutMovingStockOrPaying()
        {
            var model = Fixture(); string before = State(model);
            Ok(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1"));
            var loaded = Restore(Config(), model.CaptureSave());
            Assert.That(State(loaded), Is.EqualTo(before)); Assert.That(loaded.GetStockTransfer("job"), Is.Null);
            Assert.That(loaded.CompleteStockTransfer("job", "owner").Success, Is.False);
            Ok(loaded.TransferStorageToTank("rack-1", "tank-a", "p1"));
            model.ClearStockTransfers(); model.ClearStockTransfers();
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.GetStockTransfer("job"), Is.Null);
            Ok(model.TransferStorageToTank("rack-1", "tank-a", "p1"));
        }

        [TestCase(true)] [TestCase(false)]
        public void CommitRechecksAuthoritativeSourceAndTargetBeforeAnyPublication(bool changeSource)
        {
            var model = Fixture();
            Ok(model.ReserveStockTransfer("job", "owner", "rack-1", "tank-a", "p1"));
            var field = typeof(GameModel).GetField(changeSource ? "storage" : "tanks", BindingFlags.Instance | BindingFlags.NonPublic);
            var registry = (Dictionary<string, SpecimenView?>)field.GetValue(model);
            registry[changeSource ? "rack-1" : "tank-a"] = Fixture().GetStorage("rack-2");
            string before = State(model);
            Assert.That(model.CompleteStockTransfer("job", "owner").Failure, Is.EqualTo(changeSource ? FailureCode.SpecimenMismatch : FailureCode.TankFull));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.GetStockTransfer("job").HasValue, Is.True);
            Assert.That(model.CancelStockTransfer("job", "owner"), Is.True);
        }

        [Test]
        public void RegistryCannotRemoveClaimedSlotsOrChargeAndSurvivingClaimsRemainValid()
        {
            var model = Fixture();
            Ok(Apply(model, new[] { "tank-a", "tank-b", "extra" }, model.StorageIds, 0));
            Ok(model.ReserveStockTransfer("job", "owner", "rack-1", "extra", "p1"));
            string before = State(model);
            Assert.That(Apply(model, new[] { "tank-a", "tank-b" }, model.StorageIds, 12).Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(Apply(model, model.TankIds, new[] { "rack-2" }, -12).Failure, Is.EqualTo(FailureCode.StockReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(Apply(model, model.TankIds, model.StorageIds, 0));
            Assert.That(model.GetStockTransfer("job").HasValue, Is.True);
            Ok(model.CompleteStockTransfer("job", "owner"));
            Assert.That(model.GetTank("extra").Value.Id, Is.EqualTo("p1"));
        }

        [Test]
        public void ClaimedFurnitureCannotMoveBeStoredOrResoldUntilWorkerReleasesIt()
        {
            var layout = new ShopLayout(); var save = new GameModel(GameConfig.CreateDefault()).CaptureSave(); save.currency = 300;
            var model = Restore(GameConfig.CreateDefault(), save);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, 7, 0, out string rack).Success, Is.True);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.SmallDisplayId, null, -4, -1, 0, out string table).Success, Is.True);
            save = model.CaptureSave(); save.discoveries = new[] { "pondlet" };
            save.storage[0].specimenId = "p1"; save.storage[0].speciesId = "pondlet";
            model = Restore(layout.WithTankIds(GameConfig.CreateDefault()), save);
            Ok(model.ReserveStockTransfer("job", "owner", layout.GetStorageSlotIds(rack)[0], layout.GetSlotIds(table)[0], "p1"));
            string before = State(model) + JsonUtility.ToJson(layout.CaptureSave());
            foreach (string id in new[] { rack, table })
            {
                var item = layout.GetItem(id).Value;
                Assert.That(layout.TryCommit(model, ShopDayPhase.Night, id, item.X, item.Z, item.QuarterTurns, out _).Failure, Is.EqualTo(LayoutFailure.StockReserved));
                Assert.That(layout.TryStore(model, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.StockReserved));
                Assert.That(layout.TrySell(model, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.StockReserved));
            }
            Assert.That(State(model) + JsonUtility.ToJson(layout.CaptureSave()), Is.EqualTo(before));
            model.ClearStockTransfers();
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, rack, 11, 7, 0, out _).Success, Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, table).Success, Is.True);
            Assert.That(model.GetStorage(layout.GetStorageSlotIds(rack)[0]).Value.Id, Is.EqualTo("p1"));
        }

        static GameConfig Config()
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "rack-1", "rack-2" }; config.ConditionerIds = new[] { "vat" }; return config;
        }
        static GameModel Fixture()
        {
            var config = Config(); var save = new GameModel(config).CaptureSave(); save.currency = 90;
            save.sparkResearchUnlocked = true; save.availableSpark = 1; save.growers[2].units = 4;
            save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin" };
            save.storage[0].specimenId = "p1"; save.storage[0].speciesId = "pondlet";
            save.storage[1].specimenId = "p2"; save.storage[1].speciesId = "pondlet";
            return Restore(config, save);
        }
        static GameModel Restore(GameConfig config, SaveData save)
        {
            Assert.That(GameModel.TryRestore(config, save, out var model, out var failure), Is.True, failure.ToString()); return model;
        }
        static ActionResult Apply(GameModel model, string[] tanks, string[] storage, int delta)
        {
            var method = typeof(GameModel).GetMethod("TryApplyLayoutSlots", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string[]), typeof(string[]), typeof(string[]), typeof(int) }, null);
            return (ActionResult)method.Invoke(model, new object[] { tanks, storage, model.ConditionerIds, delta });
        }
        static string State(GameModel model) => JsonUtility.ToJson(model.CaptureSave());
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
