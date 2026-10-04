using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class EditedLayoutTests
    {
        [Test]
        public void TwelvePlacedPiecesAndEightOccupiedSlotsRestoreAndTradeTheLastSlot()
        {
            var layout = Dense(out var model);
            var ids = model.TankIds;
            var residents = new Dictionary<string, string>();
            foreach (string slot in ids)
            {
                Produce(model, slot);
                residents.Add(slot, model.GetTank(slot).Value.Id);
            }
            Assert.That(layout.Items.Count, Is.EqualTo(12));
            Assert.That(layout.PlacedItems.Count, Is.EqualTo(12));
            Assert.That(ids.Length, Is.EqualTo(8));
            Assert.That(new HashSet<string>(residents.Values).Count, Is.EqualTo(8));
            Assert.That(layout.AppealPoints, Is.EqualTo(30));
            Assert.That(model.Currency, Is.EqualTo(536));
            AssertAccess(layout);

            var savedLayout = JsonUtility.FromJson<LayoutSave>(JsonUtility.ToJson(layout.CaptureSave()));
            Array.Reverse(savedLayout.items); // File order must not assign the eight physical slots.
            Assert.That(ShopLayout.TryRestore(savedLayout, out var restoredLayout), Is.True);
            var savedGame = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(model.CaptureSave()));
            Assert.That(GameModel.TryRestore(restoredLayout.WithTankIds(GameConfig.CreateDefault()), savedGame,
                out var restoredModel, out var failure), Is.True, failure.ToString());
            Assert.That(restoredModel.TankIds, Is.EqualTo(ids));
            foreach (string slot in ids)
                Assert.That(restoredModel.GetTank(slot).Value.Id, Is.EqualTo(residents[slot]));
            AssertAccess(restoredLayout);

            string finalSlot = ids[ids.Length - 1];
            Ok(restoredModel.ReserveTank(finalSlot, "last-slot-buyer"));
            Ok(restoredModel.SellReservedTank(finalSlot, "last-slot-buyer", residents[finalSlot]));
            Assert.That(restoredModel.Currency, Is.EqualTo(542));
            Assert.That(restoredModel.GetTank(finalSlot), Is.Null);
            Assert.That(restoredModel.SellReservedTank(finalSlot, "last-slot-buyer", residents[finalSlot]).Success, Is.False);
            for (int i = 0; i < ids.Length - 1; i++)
                Assert.That(restoredModel.GetTank(ids[i]).Value.Id, Is.EqualTo(residents[ids[i]]));
        }

        [Test]
        public void StockedQuarterTurnsInDenseShopMoveBothApproachesWithoutChangingGameState()
        {
            var layout = Dense(out var model);
            foreach (string slot in model.TankIds) Produce(model, slot);
            string gameBefore = JsonUtility.ToJson(model.CaptureSave());
            string[] movedSlots = layout.GetSlotIds("table-2");

            Move(layout, model, "table-2", 3, -7, 1);
            foreach (string slot in movedSlots)
            {
                AssertBrowse(layout, slot, 0, .1f, -3.05f);
                AssertBrowse(layout, slot, 1, .1f, -3.95f);
            }
            AssertAccess(layout);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(gameBefore));

            Move(layout, model, "table-2", 3, -7, 3);
            foreach (string slot in movedSlots)
            {
                AssertBrowse(layout, slot, 0, 2.9f, -3.95f);
                AssertBrowse(layout, slot, 1, 2.9f, -3.05f);
            }
            AssertAccess(layout);
            Move(layout, model, "table-2", -18, -3, 0);
            Assert.That(layout.GetSlotIds("table-2"), Is.EqualTo(movedSlots));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(gameBefore));
            Assert.That(layout.AppealPoints, Is.EqualTo(30));
        }

        [Test]
        public void UnchangedDisplayFootprintCannotHideAnUnreachableOrOutOfStoreFront()
        {
            var layout = Dense(out var model);
            Produce(model, "table-2-slot-1"); Produce(model, "table-2-slot-2");
            Move(layout, model, "table-2", -15, -5, 1);
            string before = State(layout, model);

            // Turning 90 to 270 degrees preserves the rectangle, but faces the fixed counter.
            var preview = layout.TryPreview("table-2", -15, -5, 3, out var candidate);
            Assert.That(preview.Failure, Is.EqualTo(LayoutFailure.UnreachableAccess));
            Assert.That(candidate, Is.Null);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-2", -15, -5, 3, out _).Failure,
                Is.EqualTo(LayoutFailure.UnreachableAccess));
            Assert.That(State(layout, model), Is.EqualTo(before));

            // A 180-degree turn also keeps this rectangle, but puts browse points north of
            // the customer surface's z=.05 boundary; the lab must not rescue that route.
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-3", -26, -3, 2, out _).Failure,
                Is.EqualTo(LayoutFailure.UnreachableAccess));
            Assert.That(State(layout, model), Is.EqualTo(before));
            AssertAccess(layout);
        }

        private static ShopLayout Dense(out GameModel model)
        {
            var config = GameConfig.CreateDefault();
            var game = new GameModel(config).CaptureSave(); game.currency = 1000;
            Assert.That(GameModel.TryRestore(config, game, out model, out _), Is.True);
            var layout = new ShopLayout();
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Assert.That(layout.TryExpand(model, ShopDayPhase.Night).Success, Is.True);
            Buy(layout, model, ShopLayout.LargeDisplayId, -4, -1);
            Buy(layout, model, ShopLayout.LargeDisplayId, -18, -3);
            Buy(layout, model, ShopLayout.LargeDisplayId, -26, -3);
            Buy(layout, model, ShopLayout.TableDefinitionId, 9, 4, 1);
            Buy(layout, model, ShopLayout.TableDefinitionId, 6, 3, 1);
            Buy(layout, model, ShopLayout.TableDefinitionId, 11, 3, 1);
            Buy(layout, model, ShopLayout.CoralPlanterId, 10, 8);
            Buy(layout, model, ShopLayout.CoralPlanterId, -12, 8);
            Buy(layout, model, ShopLayout.CoralPlanterId, -28, -8);
            Buy(layout, model, ShopLayout.BubbleLampId, -23, -8);
            Buy(layout, model, ShopLayout.SillyStatueId, -20, -8);
            Buy(layout, model, ShopLayout.BubbleLampId, -17, -8);
            return layout;
        }

        private static void AssertAccess(ShopLayout layout)
        {
            foreach (string slot in layout.TankIds)
                for (int spot = 0; spot < 2; spot++)
                {
                    Assert.That(layout.TryGetBrowsePoint(slot, spot, out float x, out float z), Is.True);
                    Assert.That(z, Is.LessThan(.05f), "Every customer approach stays on the store side.");
                    Assert.That(layout.CanReachMainAisle(x, z), Is.True, slot + " browse " + spot);
                }
            foreach (var point in new[] {
                new[] { 0f, -4.8f }, new[] { -3.8f, -2.8f }, new[] { -2.85f, -3.55f },
                new[] { -9f, -2.5f }, new[] { -13f, -2.5f }, new[] { 0f, 1.5f },
                new[] { -4.2f, 2.35f }, new[] { -1.4f, 2.35f }, new[] { 2f, 2.35f },
                new[] { -4f, 1.9f }, new[] { -3.9f, -2.2f } })
                Assert.That(layout.CanReachMainAisle(point[0], point[1]), Is.True,
                    "Required player route " + point[0] + "," + point[1]);
        }
        private static void AssertBrowse(ShopLayout layout, string slot, int spot, float expectedX, float expectedZ)
        {
            Assert.That(layout.TryGetBrowsePoint(slot, spot, out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(expectedX).Within(.0001f));
            Assert.That(z, Is.EqualTo(expectedZ).Within(.0001f));
        }
        private static void Buy(ShopLayout layout, GameModel model, string definition, int x, int z, int turns = 0)
        {
            var result = layout.TryCommit(model, ShopDayPhase.Night, definition, null, x, z, turns, out _);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
        }
        private static void Move(ShopLayout layout, GameModel model, string id, int x, int z, int turns)
        {
            var result = layout.TryCommit(model, ShopDayPhase.Night, id, x, z, turns, out _);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
        }
        private static void Produce(GameModel model, string slot)
        {
            model.Tick(12);
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow));
            Ok(model.BeginIncubation()); model.Tick(10); Ok(model.TransferToTank(slot));
        }
        private static string State(ShopLayout layout, GameModel model)
            => JsonUtility.ToJson(layout.CaptureSave()) + JsonUtility.ToJson(model.CaptureSave());
        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
