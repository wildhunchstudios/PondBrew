using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FurnitureStorageTests
    {
        [Test]
        public void EmptyLargeDisplayStoresAndRedeploysTheSameSlotsFreeAtZeroCash()
        {
            var layout = Large(out var model);
            var original = layout.Items;
            string[] slots = layout.GetSlotIds("table-1");
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(layout.StoredCount, Is.EqualTo(1)); Assert.That(layout.PlacedItems, Is.Empty);
            Assert.That(layout.Items.Count, Is.EqualTo(1)); Assert.That(layout.GetItem("table-1").Value.Stored, Is.True);
            Assert.That(original[0].Stored, Is.False, "Old views remain independent snapshots.");
            Assert.That(layout.GetSlotIds("table-1"), Is.EqualTo(slots), "Storage retains potential slot identity.");
            Assert.That(model.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" }));
            Assert.That(layout.TankIds, Is.EqualTo(model.TankIds));
            Assert.That(layout.TryGetBrowsePoint(slots[0], 0, out _, out _), Is.False);
            Assert.That(model.TransferToTank(slots[0]).Failure, Is.EqualTo(FailureCode.UnknownTank));
            string stored = State(layout, model);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.AlreadyStored));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 4, 0, out _).Success, Is.False);
            Assert.That(State(layout, model), Is.EqualTo(stored));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out var id).Success, Is.True);
            Assert.That(id, Is.EqualTo("table-1")); Assert.That(layout.GetItem(id).Value.Stored, Is.False);
            Assert.That(layout.StoredCount, Is.Zero); Assert.That(model.Currency, Is.Zero);
            Assert.That(layout.GetSlotIds(id), Is.EqualTo(slots)); Assert.That(model.TankIds.Length, Is.EqualTo(4));
            Assert.That(model.TransferToTank(slots[0]).Failure, Is.EqualTo(FailureCode.OutputNotReady));
            Assert.That(layout.TryGetBrowsePoint(slots[1], 1, out _, out _), Is.True);
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(2));
        }

        [TestCase(ShopLayout.TableDefinitionId, 3)]
        [TestCase(ShopLayout.SmallDisplayId, 1)]
        [TestCase(ShopLayout.CoralPlanterId, 6)]
        public void StoredOwnershipStillConsumesItsTypeLimit(string definition, int limit)
        {
            var layout = new ShopLayout(); var model = Cash(1000);
            for (int i = 0; i < limit; i++)
            {
                int x = definition == ShopLayout.SmallDisplayId ? 3 : -4;
                int z = definition == ShopLayout.SmallDisplayId ? -5 : -1;
                string id = Buy(layout, model, definition, x, z);
                Assert.That(layout.TryStore(model, ShopDayPhase.Night, id).Success, Is.True);
            }
            string before = State(layout, model);
            Assert.That(layout.Items.Count, Is.EqualTo(limit)); Assert.That(layout.StoredCount, Is.EqualTo(limit));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, definition, null, 3, -5, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void StoringUniqueAndDuplicateDecorUpdatesOnlyPlacedAppeal()
        {
            var layout = new ShopLayout(); var model = Cash(56);
            Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            Buy(layout, model, ShopLayout.CoralPlanterId, 4, -1);
            Buy(layout, model, ShopLayout.BubbleLampId, 3, -5);
            Assert.That(layout.AppealPoints, Is.EqualTo(20));
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(layout.AppealPoints, Is.EqualTo(20));
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-2").Success, Is.True);
            Assert.That(layout.AppealPoints, Is.EqualTo(10));
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-3").Success, Is.True);
            Assert.That(layout.AppealPoints, Is.Zero); Assert.That(layout.DecorCount, Is.EqualTo(3));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 4, 0, out _).Success, Is.True);
            Assert.That(layout.AppealPoints, Is.Zero, "Redeploying into the lab grants no appeal.");
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-2", 4, -1, 0, out _).Success, Is.True);
            Assert.That(layout.AppealPoints, Is.EqualTo(10)); Assert.That(model.Currency, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyDisplayResaleRefundsHalfOnceWhetherPlacedOrStored(bool stored)
        {
            var layout = Large(out var model);
            if (stored) Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(20)); Assert.That(layout.Items, Is.Empty);
            Assert.That(model.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" }));
            string sold = State(layout, model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -3, 0, out _).Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            Assert.That(State(layout, model), Is.EqualTo(sold));
        }

        [Test]
        public void ResaleUsesThePublishedHalfPriceForAllCurrentDefinitions()
        {
            string[] definitions = { ShopLayout.TableDefinitionId, ShopLayout.SmallDisplayId, ShopLayout.LargeDisplayId,
                ShopLayout.CoralPlanterId, ShopLayout.BubbleLampId, ShopLayout.SillyStatueId };
            int[] refunds = { 6, 12, 20, 8, 12, 16 };
            for (int i = 0; i < definitions.Length; i++)
                Assert.That(ShopLayout.DefinitionResalePrice(definitions[i]), Is.EqualTo(refunds[i]));
            Assert.That(ShopLayout.DefinitionResalePrice("unknown"), Is.Zero);
            var layout = new ShopLayout(); var model = Cash(16);
            Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(8)); Assert.That(layout.AppealPoints, Is.Zero);
        }

        [Test]
        public void SoldGapsSurviveRestoreAndNewPurchasesNeverReuseRetiredIds()
        {
            var layout = new ShopLayout(); var model = Cash(100);
            Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1);
            Buy(layout, model, ShopLayout.BubbleLampId, 4, -1);
            Buy(layout, model, ShopLayout.SillyStatueId, 3, -5);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-2").Success, Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            var save = layout.CaptureSave(); Array.Reverse(save.items);
            Assert.That(ShopLayout.TryRestore(save, out layout), Is.True);
            Assert.That(layout.Items[0].Id, Is.EqualTo("table-1")); Assert.That(layout.Items[1].Id, Is.EqualTo("table-3"));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(4));
            Assert.That(Buy(layout, model, ShopLayout.CoralPlanterId, -4, -1), Is.EqualTo("table-4"));
            foreach (string id in new[] { "table-1", "table-3", "table-4" })
                Assert.That(layout.TrySell(model, ShopDayPhase.Night, id).Success, Is.True);
            Assert.That(layout.Items, Is.Empty); Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(5));
            Assert.That(ShopLayout.TryRestore(layout.CaptureSave(), out layout), Is.True);
            Assert.That(Buy(layout, model, ShopLayout.TableDefinitionId, -4, -1), Is.EqualTo("table-5"));
        }

        [Test]
        public void CanonicalNumericIdsSortNumericallyAndExhaustedSerialDoesNotWrap()
        {
            var save = new LayoutSave { version = ShopLayout.SaveVersion, nextItemNumber = int.MaxValue,
                items = new[]
                {
                    Item("table-10", true), Item("table-2", true), Item("table-2147483646", true)
                } };
            Assert.That(ShopLayout.TryRestore(save, out var layout), Is.True);
            Assert.That(layout.Items[0].Id, Is.EqualTo("table-2")); Assert.That(layout.Items[1].Id, Is.EqualTo("table-10"));
            var model = Cash(100);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-10").Success, Is.True);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.CoralPlanterId, null, -4, -1, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void StoreAndRedeployPreserveOtherReservationsIncubationAndEveryUnrelatedValue()
        {
            var layout = Large(out var model);
            Produce(model, "tank-a"); Assert.That(model.ReserveTank("tank-a", "unaffected").Success, Is.True);
            var claim = model.GetReservation("tank-a").Value;
            Assert.That(model.SetPrice("pondlet", 12).Success, Is.True);
            BeginProduction(model); model.Tick(3);
            var before = model.CaptureSave();
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            var after = model.CaptureSave(); before.tanks = after.tanks;
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)));
            Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(claim.Price));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Success, Is.True);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(claim.SpecimenId));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("unaffected"));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(before.incubator.specimenId));
            Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(7));
        }

        [Test]
        public void StockedMovesKeepBothResidentsWhileStorageResaleAndReservedMovesRejectAtomically()
        {
            var layout = Large(out var model); string[] slots = layout.GetSlotIds("table-1");
            Produce(model, slots[0]); Produce(model, slots[1]);
            string stock = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Success, Is.True);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(stock));
            string before = State(layout, model);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.StockedDisplay));
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.StockedDisplay));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(model.ReserveTank(slots[1], "pending").Success, Is.True);
            var claim = model.GetReservation(slots[1]).Value;
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -3, 0, out _).Failure, Is.EqualTo(LayoutFailure.ReservedDisplay));
            // The first slot is stocked but unreserved: every operation must still preserve the later claim.
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.False);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.False);
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(model.GetReservation(slots[1]).Value.CustomerId, Is.EqualTo(claim.CustomerId));
            Assert.That(model.GetReservation(slots[1]).Value.SpecimenId, Is.EqualTo(claim.SpecimenId));
            Assert.That(model.GetReservation(slots[1]).Value.Price, Is.EqualTo(claim.Price));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RefundOverflowRejectsTheWholeSaleAndRetainsOwnership(bool stored)
        {
            var layout = Large(out var model);
            if (stored) Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            var game = model.CaptureSave(); game.currency = int.MaxValue;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(GameConfig.CreateDefault()), game, out model, out _), Is.True);
            string before = State(layout, model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Failure, Is.EqualTo(LayoutFailure.CurrencyOverflow));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(layout.GetItem("table-1").Value.Stored, Is.EqualTo(stored));
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        [TestCase((ShopDayPhase)33)]
        public void EveryOwnershipMutationRequiresNight(ShopDayPhase phase)
        {
            var layout = Large(out var model); string before = State(layout, model);
            Assert.That(layout.TryStore(model, phase, "table-1").Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(layout.TrySell(model, phase, "table-1").Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(layout.TryCommit(model, phase, "table-1", -18, -7, 2, out _).Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void PreviewAndFailedRestoreCannotPublishStorageOrOrphanSavedStock()
        {
            var layout = Large(out var model); string before = State(layout, model);
            Assert.That(layout.TryPreviewStore("table-1", out var stored).Success, Is.True);
            Assert.That(stored.GetItem("table-1").Value.Stored, Is.True); Assert.That(stored.TankIds.Length, Is.EqualTo(2));
            Assert.That(layout.TryPreviewSell("table-1", out var sold).Success, Is.True); Assert.That(sold.Items, Is.Empty);
            Assert.That(State(layout, model), Is.EqualTo(before));
            var orphan = model.CaptureSave();
            Assert.That(GameModel.TryRestore(stored.WithTankIds(GameConfig.CreateDefault()), orphan, out _, out _), Is.False);
            Assert.That(stored.TryCommit(model, ShopDayPhase.Night, "table-1", -18, -7, 2, out _).Failure, Is.EqualTo(LayoutFailure.ModelMismatch));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void HistoricalLayoutsKeepContiguousIdsAndCannotSmuggleStorage(int version)
        {
            var save = new LayoutSave { version = version, nextItemNumber = 2, items = new[] { Item("table-1", false) } };
            Assert.That(ShopLayout.TryRestore(save, out var restored), Is.True);
            Assert.That(restored.GetItem("table-1").Value.Stored, Is.False);
            save.items[0].stored = true; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.items[0].stored = false; save.items[0].id = "table-2"; save.nextItemNumber = 3;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.items[0].id = "table-1";
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False, "Older counters still require count+1.");
        }

        [TestCase("table-01")]
        [TestCase("table-0")]
        [TestCase("table--1")]
        [TestCase("table-2147483648")]
        [TestCase("table-1 ")]
        [TestCase("tank-a")]
        public void NoncanonicalOwnedIdsRejectEvenWhenStored(string id)
        {
            var save = new LayoutSave { version = ShopLayout.SaveVersion, nextItemNumber = 3, items = new[] { Item(id, true) } };
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
        }

        [Test]
        public void CurrentSerialDuplicatesAndStoredPoseCorruptionRemainStrict()
        {
            var save = new LayoutSave { version = ShopLayout.SaveVersion, nextItemNumber = 3,
                items = new[] { Item("table-1", true), Item("table-2", true) } };
            Assert.That(ShopLayout.TryRestore(save, out _), Is.True);
            save.nextItemNumber = 2; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.nextItemNumber = 0; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.nextItemNumber = 3; save.items[1].id = "table-1"; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.items[1].id = "table-2"; save.items[1].x = int.MaxValue; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.items[1].x = -4; save.items[1].quarterTurns = 4; Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
        }

        [Test]
        public void OriginalStationsAndUnknownIdsCannotBeStoredOrResold()
        {
            var layout = new ShopLayout(); var model = Cash(100); string before = State(layout, model);
            foreach (string id in new[] { "tank-a", "tank-b", "incubator", "table-1", null })
            {
                Assert.That(layout.TryStore(model, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.UnknownItem));
                Assert.That(layout.TrySell(model, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            }
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        private static LayoutItemSave Item(string id, bool stored) => new LayoutItemSave
            { id = id, definitionId = ShopLayout.TableDefinitionId, x = -4, z = -1, quarterTurns = 0, stored = stored };
        private static ShopLayout Large(out GameModel model)
        {
            var layout = new ShopLayout(); model = Cash(100);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3); return layout;
        }
        private static string State(ShopLayout layout, GameModel model)
            => JsonUtility.ToJson(layout.CaptureSave()) + JsonUtility.ToJson(model.CaptureSave());
        private static string Buy(ShopLayout layout, GameModel model, string definition, int x, int z)
        {
            var result = layout.TryCommit(model, ShopDayPhase.Night, definition, null, x, z, 0, out var id);
            Assert.That(result.Success, Is.True, result.Failure.ToString()); return id;
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
            BeginProduction(model); model.Tick(10); Assert.That(model.TransferToTank(slot).Success, Is.True);
        }
    }
}
