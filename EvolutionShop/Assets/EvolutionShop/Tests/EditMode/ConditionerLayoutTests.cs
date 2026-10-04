using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ConditionerLayoutTests
    {
        [Test]
        public void VatPurchaseAddsOnlyOneConditionerAndPreservesConfigTuning()
        {
            var layout = new ShopLayout(); var model = ReadyModel(64);
            Assert.That(ShopLayout.DefinitionWidth(ShopLayout.ConditionerDefinitionId), Is.EqualTo(2));
            Assert.That(ShopLayout.DefinitionDepth(ShopLayout.ConditionerDefinitionId), Is.EqualTo(1));
            string id = Buy(layout, model, 9, 7, 0);
            Assert.That(model.Currency, Is.Zero);
            Assert.That(model.ConditionerIds, Is.EqualTo(new[] {id}));
            Assert.That(layout.ConditionerIds, Is.EqualTo(new[] {id}));
            Assert.That(model.GetConditioner(id).Value.Capacity, Is.EqualTo(2));
            Assert.That(model.GetConditioner(id).Value.Phase, Is.EqualTo(ConditionerPhase.Idle));
            Assert.That(model.TankIds, Is.EqualTo(new[] {"tank-a", "tank-b"}));
            Assert.That(model.StorageIds, Is.Empty);
            Assert.That(layout.GetSlotIds(id), Is.Empty);
            Assert.That(layout.GetStorageSlotIds(id), Is.Empty);
            Assert.That(layout.TryGetBrowsePoint(id, 0, out _, out _), Is.False);
            Assert.That(layout.AppealPoints, Is.Zero);
            var config = GameConfig.CreateDefault(); config.ConditioningSeconds = 17;
            var copy = layout.WithTankIds(config);
            Assert.That(copy.ConditioningSeconds, Is.EqualTo(17));
            Assert.That(copy.ConditionerIds, Is.EqualTo(new[] {id}));
            copy.ConditionerIds[0] = "changed";
            Assert.That(layout.ConditionerIds, Is.EqualTo(new[] {id}));
        }

        [TestCase(false, false)] [TestCase(true, false)]
        public void MissingResearchOrKnownOutputBlocksPurchaseWithoutChangingState(bool unlocked, bool discovered)
        {
            var layout = new ShopLayout(); var model = ReadyModel(100, unlocked, discovered);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.ConditionerDefinitionId, null, 9, 7, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LockedDefinition));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void PurchaseRemainsNightOnly(ShopDayPhase phase)
        {
            var layout = new ShopLayout(); var model = ReadyModel(100);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, phase, ShopLayout.ConditionerDefinitionId, null, 9, 7, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void InsufficientMoneyCannotPublishMachineOrConsumeOwnedId()
        {
            var layout = new ShopLayout(); var model = ReadyModel(63);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.ConditionerDefinitionId, null, 9, 7, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.InsufficientFunds));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(1));
        }

        [Test]
        public void StoreRedeployAndSaleKeepIdentityAndPayOnlyOnce()
        {
            var layout = new ShopLayout(); var model = ReadyModel(200); string id = Buy(layout, model, 9, 7, 0);
            Ok(layout.TryStore(model, ShopDayPhase.Night, id));
            Assert.That(layout.ConditionerCount, Is.EqualTo(1)); Assert.That(layout.ConditionerIds, Is.Empty);
            Assert.That(model.ConditionerIds, Is.Empty); Assert.That(model.GetConditioner(id), Is.Null);
            Assert.That(layout.TryGetConditionerAccessPoint(id, out _, out _), Is.False);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.ConditionerDefinitionId, null, 11, 7, 0, out _).Failure,
                Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Ok(layout.TryCommit(model, ShopDayPhase.Night, id, 11, 7, 0, out string same));
            Assert.That(same, Is.EqualTo(id)); Assert.That(model.Currency, Is.EqualTo(136));
            Assert.That(model.ConditionerIds, Is.EqualTo(new[] {id}));
            Ok(layout.TrySell(model, ShopDayPhase.Night, id));
            Assert.That(model.Currency, Is.EqualTo(168)); Assert.That(layout.ConditionerCount, Is.Zero);
            before = State(layout, model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void StoredVatNeedsResearchAndKnownRecipeBeforeRedeploy()
        {
            var layout = new ShopLayout(); var model = ReadyModel(64); string id = Buy(layout, model, 9, 7, 0);
            Ok(layout.TryStore(model, ShopDayPhase.Night, id));
            model = ReadyModel(0, true, false);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, id, 9, 7, 0, out _).Failure, Is.EqualTo(LayoutFailure.LockedDefinition));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [TestCase(3, -5, 0)] [TestCase(0, 3, 0)] [TestCase(13, 7, 0)] [TestCase(9, 11, 0)] [TestCase(10, 3, 0)]
        public void StoreDoorwayWallsAndOperatorOutsideLabRejectAtomically(int x, int z, int turns)
        {
            var layout = new ShopLayout(); var model = ReadyModel(100);
            string before = State(layout, model);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.ConditionerDefinitionId, null, x, z, turns, out _).Success, Is.False);
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [TestCase(9, 7, 0, 4.5f, 2.1f)] [TestCase(9, 4, 1, 3.1f, 2f)] [TestCase(9, 4, 3, 5.9f, 2f)]
        public void RotatedOperatorAccessIsReachableAndNotCustomerBrowse(int x, int z, int turns, float expectedX, float expectedZ)
        {
            var layout = new ShopLayout(); var model = ReadyModel(64); string id = Buy(layout, model, x, z, turns);
            Assert.That(layout.TryGetConditionerAccessPoint(id, out float ax, out float az), Is.True);
            Assert.That(ax, Is.EqualTo(expectedX).Within(.0001)); Assert.That(az, Is.EqualTo(expectedZ).Within(.0001));
            Assert.That(layout.CanReachMainAisle(ax, az), Is.True);
            Assert.That(layout.TryGetBrowsePoint(id, 0, out _, out _), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void WorkingOrReadyVatMovesFreeButCannotStoreOrSell(bool ready)
        {
            var layout = new ShopLayout(); var model = ReadyModel(1000);
            Ok(layout.TryExpandLab(model, ShopDayPhase.Night));
            Ok(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, 7, 0, out string rack));
            string vat = Buy(layout, model, 9, 13, 0);
            var slots = layout.GetStorageSlotIds(rack);
            string first = Produce(model); Action(model.StoreReadyOutput(slots[0]));
            string second = Produce(model); Action(model.StoreReadyOutput(slots[1]));
            Action(model.Collect(CultureKind.Spark));
            Action(model.BeginConditioning(vat, slots, new[] {first, second}));
            model.Tick(ready ? 12 : 3.25f);
            string production = JsonUtility.ToJson(model.CaptureSave());
            Ok(layout.TryCommit(model, ShopDayPhase.Night, vat, 11, 13, 0, out _));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(production));
            string before = State(layout, model);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, vat).Failure, Is.EqualTo(LayoutFailure.ConditionerOccupied));
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, vat).Failure, Is.EqualTo(LayoutFailure.ConditionerOccupied));
            Assert.That(State(layout, model), Is.EqualTo(before));
            if (!ready) Action(model.CancelConditioning(vat));
            Action(model.TransferConditionerToStorage(vat, 0, slots[0], first));
            Assert.That(model.GetConditioner(vat).Value.First, Is.Null);
            before = State(layout, model);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, vat).Failure, Is.EqualTo(LayoutFailure.ConditionerOccupied));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Action(model.TransferConditionerToStorage(vat, 1, slots[1], second));
            Ok(layout.TryStore(model, ShopDayPhase.Night, vat));
            Assert.That(model.GetStorage(slots[0]).Value.Id, Is.EqualTo(first));
            Assert.That(model.GetStorage(slots[1]).Value.Id, Is.EqualTo(second));
        }

        [Test]
        public void RegistryMismatchAndOverflowSaleCannotPublishLayoutOrMoney()
        {
            var layout = new ShopLayout(); var model = ReadyModel(64); string id = Buy(layout, model, 9, 7, 0);
            var mismatched = ReadyModel(100);
            string before = State(layout, mismatched);
            Assert.That(layout.TryStore(mismatched, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.ModelMismatch));
            Assert.That(State(layout, mismatched), Is.EqualTo(before));
            var save = model.CaptureSave(); save.currency = int.MaxValue;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(GameConfig.CreateDefault()), save, out model, out _), Is.True);
            before = State(layout, model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, id).Failure, Is.EqualTo(LayoutFailure.CurrencyOverflow));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void HistoricalLayoutVersionsCannotContainVatDefinition(int version)
        {
            var layout = new ShopLayout(); var model = ReadyModel(64); Buy(layout, model, 9, 7, 0);
            var save = layout.CaptureSave(); save.version = version;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
        }

        [Test]
        public void SixteenOwnedItemsRoundTripAndVersionSixRetainsFifteenItemCeiling()
        {
            var items = new List<LayoutItemSave>();
            Add(ShopLayout.TableDefinitionId, 3); Add(ShopLayout.SmallDisplayId, 3);
            Add(ShopLayout.CoralPlanterId, 6); Add(ShopLayout.RackDefinitionId, 3);
            var save = new ShopLayout().CaptureSave(); save.storeTier = save.labTier = 2;
            save.version = 6; save.items = items.ToArray(); save.nextItemNumber = 16;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.True, "Historical fifteen items remain valid.");
            Add(ShopLayout.ConditionerDefinitionId, 1);
            save.items = items.ToArray(); save.nextItemNumber = 17;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.version = 7;
            Assert.That(ShopLayout.TryRestore(save, out var restored), Is.True);
            Assert.That(restored.Items.Count, Is.EqualTo(16)); Assert.That(restored.ConditionerCount, Is.EqualTo(1));
            Assert.That(restored.ConditionerIds, Is.Empty);
            Assert.That(ShopLayout.TryRestore(restored.CaptureSave(), out _), Is.True);
            Add(ShopLayout.ConditionerDefinitionId, 1); save.items = items.ToArray(); save.nextItemNumber = 18;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            void Add(string definition, int count)
            {
                for (int i = 0; i < count; i++) items.Add(new LayoutItemSave {id="table-"+(items.Count+1), definitionId=definition, stored=true});
            }
        }

        static GameModel ReadyModel(int cash, bool unlocked = true, bool lantern = true)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave();
            save.currency = cash; save.sparkResearchUnlocked = unlocked;
            save.discoveries = lantern ? new[] {"pondlet","bubblebelly","ribbonfin","lanternfin"} : new[] {"pondlet","bubblebelly","ribbonfin"};
            foreach (var grower in save.growers) if (grower.cultureId == "spark") grower.units = unlocked ? 4 : 0;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
        static string Buy(ShopLayout layout, GameModel model, int x, int z, int turns)
        {
            Ok(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.ConditionerDefinitionId, null, x, z, turns, out string id)); return id;
        }
        static string Produce(GameModel model)
        {
            Action(model.Collect(CultureKind.Bloom)); Action(model.Collect(CultureKind.Flow));
            Action(model.Dose(CultureKind.Bloom)); Action(model.Dose(CultureKind.Flow)); Action(model.BeginIncubation());
            model.Tick(10); return model.Incubator.SpecimenId;
        }
        static void Action(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        static void Ok(LayoutResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        static string State(ShopLayout layout, GameModel model) => JsonUtility.ToJson(layout.CaptureSave()) + "|" + JsonUtility.ToJson(model.CaptureSave());
    }
}
