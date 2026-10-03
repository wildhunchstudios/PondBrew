using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class LayoutTests
    {
        private ShopLayout layout;
        private GameModel model;

        [SetUp]
        public void SetUp()
        {
            layout = new ShopLayout();
            model = ModelWithCash(36);
        }

        [Test]
        public void PreviewIsIndependentAndCancelChangesNeitherMoneyNorOwnership()
        {
            string before = JsonUtility.ToJson(layout.CaptureSave());
            Assert.That(layout.TryPreview(null, -4, -1, 0, out var preview).Success, Is.True);
            Assert.That(preview.GetItem("table-1").Value.X, Is.EqualTo(-4));
            Assert.That(model.Currency, Is.EqualTo(36));
            Assert.That(JsonUtility.ToJson(layout.CaptureSave()), Is.EqualTo(before));
            // Mutating a preview never publishes its changes back into the source layout.
            Assert.That(preview.TryPreview("table-1", -4, -2, 1, out var moved).Success, Is.True);
            Assert.That(preview.GetItem("table-1").Value.Z, Is.EqualTo(-1));
            Assert.That(moved.GetItem("table-1").Value.Z, Is.EqualTo(-2));
            Assert.That(layout.Items, Is.Empty);
        }

        [Test]
        public void ThreeDistinctPurchasesChargeOnceAndLimitRejectsAtomically()
        {
            Buy(-4, -1, 0, "table-1"); Buy(4, -1, 0, "table-2"); Buy(9, 4, 1, "table-3");
            Assert.That(model.Currency, Is.Zero);
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(4));
            AssertAtomicFailure(null, 9, 8, 0, ShopDayPhase.Night, LayoutFailure.LimitReached);
        }

        [Test]
        public void FreeMoveAtZeroCashKeepsIdentityAndAllOtherGameState()
        {
            model = ModelWithCash(12);
            Buy(-4, -1, 0, "table-1");
            Assert.That(model.Currency, Is.Zero);
            string beforeGame = JsonUtility.ToJson(model.CaptureSave());
            var oldView = layout.Items;
            var result = layout.TryCommit(model, ShopDayPhase.Night, "table-1", -4, -2, 1, out var id);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
            Assert.That(id, Is.EqualTo("table-1"));
            Assert.That(layout.Items.Count, Is.EqualTo(1));
            Assert.That(layout.GetItem(id).Value.QuarterTurns, Is.EqualTo(1));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(2));
            Assert.That(oldView[0].Z, Is.EqualTo(-1), "Previously captured views are snapshots.");
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(beforeGame));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, id, -4, -2, 1, out _).Success, Is.True);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(beforeGame), "Repeated move is free and does not duplicate ownership.");
        }

        [Test]
        public void InsufficientFundsAndUnknownMovingIdNeverConsumeAnIdOrMoney()
        {
            model = ModelWithCash(11);
            AssertAtomicFailure(null, -4, -1, 0, ShopDayPhase.Night, LayoutFailure.InsufficientFunds);
            AssertAtomicFailure("table-1", -4, -1, 0, ShopDayPhase.Night, LayoutFailure.UnknownItem);
            AssertAtomicFailure("", -4, -1, 0, ShopDayPhase.Night, LayoutFailure.UnknownItem);
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(1));
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        [TestCase((ShopDayPhase)99)]
        public void PurchaseAndOwnedMovesRequireNight(ShopDayPhase phase)
        {
            AssertAtomicFailure(null, -4, -1, 0, phase, LayoutFailure.WrongPhase);
            Buy(-4, -1, 0, "table-1");
            AssertAtomicFailure("table-1", -4, -2, 1, phase, LayoutFailure.WrongPhase);
        }

        [Test]
        public void NullMoneyAuthorityRejectsEvenAValidPreview()
        {
            string before = JsonUtility.ToJson(layout.CaptureSave());
            Assert.That(layout.TryCommit(null, ShopDayPhase.Night, null, -4, -1, 0, out var id).Failure, Is.EqualTo(LayoutFailure.InvalidModel));
            Assert.That(id, Is.Null);
            Assert.That(JsonUtility.ToJson(layout.CaptureSave()), Is.EqualTo(before));
        }

        [TestCase(0, -5, 0, LayoutFailure.ProtectedAccess)] // 1.2 m central aisle.
        [TestCase(1, 1, 0, LayoutFailure.ProtectedAccess)] // Actual lab doorway guard.
        [TestCase(-8, -5, 0, LayoutFailure.ProtectedAccess)] // Counter/queue access.
        [TestCase(7, -2, 0, LayoutFailure.ProtectedAccess)] // Tank browse position.
        [TestCase(-8, 5, 0, LayoutFailure.ProtectedAccess)] // Grower operator position.
        [TestCase(11, -2, 0, LayoutFailure.OverlapsFurniture)] // Existing stock display.
        [TestCase(9, 1, 0, LayoutFailure.OutOfBounds)] // Solid store/lab partition.
        [TestCase(13, -8, 0, LayoutFailure.OutOfBounds)] // Footprint crosses outer wall.
        [TestCase(int.MaxValue, 0, 0, LayoutFailure.OutOfBounds)]
        [TestCase(0, int.MinValue, 0, LayoutFailure.OutOfBounds)]
        [TestCase(-4, -1, -1, LayoutFailure.InvalidRotation)]
        [TestCase(-4, -1, 4, LayoutFailure.InvalidRotation)]
        public void GeometryAndRotationFailuresAreAtomic(int x, int z, int turns, LayoutFailure failure)
            => AssertAtomicFailure(null, x, z, turns, ShopDayPhase.Night, failure);

        [Test]
        public void RotationChecksActualOrientedFootprintAndOwnedTablesCollide()
        {
            Assert.That(layout.ValidatePlacement(null, -12, -8, 0).Success, Is.True);
            AssertAtomicFailure(null, -12, -8, 1, ShopDayPhase.Night, LayoutFailure.OverlapsFurniture);
            Buy(-4, -1, 0, "table-1");
            AssertAtomicFailure(null, -4, -1, 0, ShopDayPhase.Night, LayoutFailure.OverlapsFurniture);
            Assert.That(layout.ValidatePlacement("table-1", -4, -1, 0).Success, Is.True, "Moving ignores only its own old footprint.");
        }

        [Test]
        public void NonOverlappingLabFurnitureCannotCutOffARequiredAccessRoute()
        {
            Buy(-4, -1, 0, "table-1"); Buy(4, -1, 0, "table-2"); Buy(9, 4, 1, "table-3");
            AssertAtomicFailure("table-1", -5, 3, 1, ShopDayPhase.Night, LayoutFailure.UnreachableAccess);
            Assert.That(layout.CanReachMainAisle(-4.2f, 2.35f), Is.True);
            Assert.That(layout.CanReachMainAisle(2f, 2.35f), Is.True);
        }

        [Test]
        public void PlayerReachabilityDoesNotSnapThroughBodiesWallsOrInvalidCoordinates()
        {
            Assert.That(layout.CanReachMainAisle(-2, -.5f), Is.True);
            Buy(-4, -1, 0, "table-1");
            Assert.That(layout.CanReachMainAisle(-2, -.5f), Is.False, "Inside the placed table is not a safe player location.");
            Assert.That(layout.CanReachMainAisle(4, .5f), Is.False, "A partition point cannot snap to its other side.");
            Assert.That(layout.CanReachMainAisle(0, -3.8f), Is.True);
            Assert.That(layout.CanReachMainAisle(0, 1.5f), Is.True);
            Assert.That(layout.CanReachMainAisle(float.NaN, 0), Is.False);
            Assert.That(layout.CanReachMainAisle(0, float.PositiveInfinity), Is.False);
            Assert.That(layout.CanReachMainAisle(float.MaxValue, 0), Is.False);
        }

        [Test]
        public void RestoreIsStrictAndCaptureCannotMutateLiveLayout()
        {
            Assert.That(ShopLayout.TryRestore(layout.CaptureSave(), out var empty), Is.True);
            Assert.That(empty.Items, Is.Empty);
            Buy(-4, -1, 0, "table-1"); Buy(4, -1, 0, "table-2"); Buy(9, 4, 1, "table-3");
            var save = layout.CaptureSave();
            Array.Reverse(save.items);
            Assert.That(ShopLayout.TryRestore(save, out var restored), Is.True, "Array order is not identity.");
            Assert.That(restored.Items[0].Id, Is.EqualTo("table-1"));
            save.items[0].x = 0;
            save.items[1].id = "changed";
            Assert.That(restored.GetItem("table-3").Value.X, Is.EqualTo(9));
            Assert.That(layout.GetItem("table-3").Value.X, Is.EqualTo(9));
            var json = JsonUtility.ToJson(restored.CaptureSave());
            Assert.That(ShopLayout.TryRestore(JsonUtility.FromJson<LayoutSave>(json), out var roundTrip), Is.True);
            Assert.That(JsonUtility.ToJson(roundTrip.CaptureSave()), Is.EqualTo(json));
        }

        [TestCase("version")]
        [TestCase("counter")]
        [TestCase("id")]
        [TestCase("duplicate")]
        [TestCase("catalog")]
        [TestCase("null item")]
        [TestCase("null array")]
        [TestCase("rotation")]
        [TestCase("coordinates")]
        [TestCase("overlap")]
        [TestCase("access")]
        public void MalformedSavesRejectRatherThanRepairOwnershipOrGeometry(string fault)
        {
            Buy(-4, -1, 0, "table-1"); Buy(4, -1, 0, "table-2");
            var save = layout.CaptureSave();
            switch (fault)
            {
                case "version": save.version = ShopLayout.SaveVersion + 1; break;
                case "counter": save.nextItemNumber++; break;
                case "id": save.items[0].id = "table-01"; break;
                case "duplicate": save.items[1].id = save.items[0].id; break;
                case "catalog": save.items[0].definitionId = "display"; break;
                case "null item": save.items[0] = null; break;
                case "null array": save.items = null; break;
                case "rotation": save.items[0].quarterTurns = 4; break;
                case "coordinates": save.items[0].x = int.MaxValue; break;
                case "overlap": save.items[1].x = -4; break;
                case "access": save.items[0].x = 0; break;
            }
            Assert.That(ShopLayout.TryRestore(save, out var restored), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(layout.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void MissingAndOversizedSavesAreNotTreatedAsFreshOwnership()
        {
            Assert.That(ShopLayout.TryRestore(null, out _), Is.False);
            Assert.That(ShopLayout.TryRestore(new LayoutSave(), out _), Is.False);
            Assert.That(ShopLayout.TryRestore(new LayoutSave { version = 1, nextItemNumber = 5, items = new LayoutItemSave[4] }, out _), Is.False);
        }

        private void Buy(int x, int z, int turns, string expectedId)
        {
            int before = model.Currency;
            var result = layout.TryCommit(model, ShopDayPhase.Night, null, x, z, turns, out var id);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
            Assert.That(id, Is.EqualTo(expectedId));
            Assert.That(model.Currency, Is.EqualTo(before - ShopLayout.TablePrice));
        }

        private void AssertAtomicFailure(string movingId, int x, int z, int turns, ShopDayPhase phase, LayoutFailure expected)
        {
            string gameBefore = JsonUtility.ToJson(model.CaptureSave()), layoutBefore = JsonUtility.ToJson(layout.CaptureSave());
            var result = layout.TryCommit(model, phase, movingId, x, z, turns, out var id);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(expected));
            Assert.That(id, Is.Null);
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(gameBefore));
            Assert.That(JsonUtility.ToJson(layout.CaptureSave()), Is.EqualTo(layoutBefore));
        }

        private static GameModel ModelWithCash(int cash)
        {
            var config = GameConfig.CreateDefault();
            var save = new GameModel(config).CaptureSave(); save.currency = cash;
            Assert.That(GameModel.TryRestore(config, save, out var model, out var failure), Is.True, failure.ToString());
            return model;
        }
    }
}
