using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class LabLayoutTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void LegacyLayoutsKeepExactOriginalAnchorsAndRejectHiddenRelocation(int version)
        {
            var save = new ShopLayout().CaptureSave(); save.version = version; save.facilities = null;
            Assert.That(ShopLayout.TryRestore(save, out var layout), Is.True);
            Assert.That(layout.LabTier, Is.Zero);
            AssertDefaults(layout);
            save.facilities = Array.Empty<FacilitySave>();
            Assert.That(ShopLayout.TryRestore(save, out _), Is.True);
            save.facilities = layout.CaptureSave().facilities;
            Array.Reverse(save.facilities);
            Assert.That(ShopLayout.TryRestore(save, out var explicitDefaults), Is.True);
            AssertDefaults(explicitDefaults);
            save.facilities[0].x = 1;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
            save.facilities = layout.CaptureSave().facilities;
            save.labTier = 1;
            Assert.That(ShopLayout.TryRestore(save, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LabAndStoreTiersComposeWithoutGrantingFloorInTheNorthwestVoid(int storeTier)
        {
            var layout = new ShopLayout(); var model = Cash(1000);
            for (int i = 0; i < storeTier; i++) Success(layout.TryExpand(model, ShopDayPhase.Night));
            int before = model.Currency;
            var originalFacilities = JsonUtility.ToJson(layout.CaptureSave().facilities[0]);
            Success(layout.TryPreviewLabExpansion(out var preview));
            Assert.That(preview.LabTier, Is.EqualTo(1)); Assert.That(layout.LabTier, Is.Zero);
            Assert.That(model.Currency, Is.EqualTo(before));
            Success(layout.TryExpandLab(model, ShopDayPhase.Night));
            Assert.That(layout.LabMaxZ, Is.EqualTo(9.5f));
            Assert.That(layout.NextLabExpansionPrice, Is.EqualTo(120));
            Assert.That(layout.CanReachMainAisle(.25f, 7.5f), Is.True);
            Assert.That(layout.CanReachMainAisle(.25f, 11.5f), Is.False);
            Success(layout.TryExpandLab(model, ShopDayPhase.Night));
            Assert.That(layout.LabMaxZ, Is.EqualTo(13.5f));
            Assert.That(layout.NextLabExpansionPrice, Is.Zero);
            Assert.That(layout.CanReachMainAisle(.25f, 11.5f), Is.True);
            Assert.That(layout.StoreTier, Is.EqualTo(storeTier));
            Assert.That(model.Currency, Is.EqualTo(before - 180));
            Assert.That(model.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" }));
            Assert.That(JsonUtility.ToJson(layout.CaptureSave().facilities[0]), Is.EqualTo(originalFacilities));
            string final = State(layout, model);
            Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Failure, Is.EqualTo(LayoutFailure.LimitReached));
            Assert.That(State(layout, model), Is.EqualTo(final));
            Assert.That(layout.CanReachMainAisle(-9, 7.5f), Is.False);
            Assert.That(layout.ValidatePlacement(null, -18, 16, 0).Failure, Is.EqualTo(LayoutFailure.OutOfBounds));
            Assert.That(layout.ValidatePlacement(null, 10, 24, 0).Success, Is.True);
            Assert.That(layout.ValidatePlacement(null, 10, 26, 0).Failure, Is.EqualTo(LayoutFailure.OutOfBounds));
            Assert.That(layout.ValidatePlacement(null, 0, 20, 0).Failure, Is.EqualTo(LayoutFailure.ProtectedAccess));
            Assert.That(ShopLayout.TryRestore(layout.CaptureSave(), out var restored), Is.True);
            Assert.That(restored.LabTier, Is.EqualTo(2)); Assert.That(restored.StoreTier, Is.EqualTo(storeTier));
        }

        [TestCase(IncubatorPhase.Dosing)]
        [TestCase(IncubatorPhase.Incubating)]
        [TestCase(IncubatorPhase.Ready)]
        public void RelocatingAllFourFacilitiesPreservesProductionAndAnExistingSaleAgreement(IncubatorPhase phase)
        {
            var layout = new ShopLayout(); var model = Cash(300);
            Success(layout.TryExpandLab(model, ShopDayPhase.Night));
            Success(layout.TryExpandLab(model, ShopDayPhase.Night));
            Ok(model.BuyUpgrade()); Produce(model, "tank-a");
            Ok(model.ReserveTank("tank-a", "unchanged-order"));
            var claim = model.GetReservation("tank-a").Value;
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom));
            if (phase != IncubatorPhase.Dosing)
            {
                Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation());
                model.Tick(phase == IncubatorPhase.Incubating ? 2 : 10);
            }
            Assert.That(model.Incubator.Phase, Is.EqualTo(phase));
            string original = JsonUtility.ToJson(model.CaptureSave());
            foreach (var facility in layout.Facilities)
            {
                Success(layout.TryPreviewFacility(facility.Id, 0, 8, facility.QuarterTurns, out var preview));
                Assert.That(layout.GetFacility(facility.Id).Value.Z, Is.Zero);
                Assert.That(preview.GetFacility(facility.Id).Value.Z, Is.EqualTo(8));
                Success(layout.TryMoveFacility(model, ShopDayPhase.Night, facility.Id, 0, 8, facility.QuarterTurns));
                Success(layout.TryMoveFacility(model, ShopDayPhase.Night, facility.Id, 0, 16, facility.QuarterTurns));
                Assert.That(layout.TryGetFacilityAccessPoint(facility.Id, out float x, out float z), Is.True);
                Assert.That(layout.CanReachMainAisle(x, z), Is.True);
                Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(original));
                Assert.That(model.GetReservation("tank-a").Value.SpecimenId, Is.EqualTo(claim.SpecimenId));
                Assert.That(model.GetReservation("tank-a").Value.Price, Is.EqualTo(claim.Price));
            }
            Ok(model.SellReservedTank("tank-a", "unchanged-order", claim.SpecimenId));
            Assert.That(model.Incubator.Phase, Is.EqualTo(phase));
            Assert.That(model.SellReservedTank("tank-a", "unchanged-order", claim.SpecimenId).Success, Is.False);
        }

        [Test]
        public void VacatedBodyAndOperatorSpaceFollowTheFacilityRatherThanItsOldAnchor()
        {
            var layout = new ShopLayout(); var model = Cash(200);
            Assert.That(layout.ValidatePlacement(null, -8, 8, 0).Failure, Is.EqualTo(LayoutFailure.OverlapsFurniture));
            Success(layout.TryExpandLab(model, ShopDayPhase.Night));
            Success(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 8, 0));
            Assert.That(layout.ValidatePlacement(null, -8, 8, 0).Success, Is.True);
            Assert.That(layout.ValidatePlacement(null, -8, 13, 0).Failure, Is.EqualTo(LayoutFailure.ProtectedAccess));
            Success(layout.TryCommit(model, ShopDayPhase.Night, null, -8, 8, 0, out var table));
            string before = State(layout, model);
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 0, 0).Failure,
                Is.EqualTo(LayoutFailure.OverlapsFurniture));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Success(layout.TryStore(model, ShopDayPhase.Night, table));
            Success(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 0, 2));
            Assert.That(layout.TryGetFacilityAccessPoint(ShopLayout.BloomFacilityId, out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(-4.2f)); Assert.That(z, Is.EqualTo(5.35f).Within(.0001f));
            Assert.That(layout.CanReachMainAisle(x, z), Is.True);
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        [TestCase((ShopDayPhase)99)]
        public void LabTransactionsRejectOutsideNightWithoutPublishingOrCharging(ShopDayPhase phase)
        {
            var layout = new ShopLayout(); var model = Cash(200); string before = State(layout, model);
            Assert.That(layout.TryExpandLab(model, phase).Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(layout.TryMoveFacility(model, phase, ShopLayout.BloomFacilityId, 0, 0, 0).Failure, Is.EqualTo(LayoutFailure.WrongPhase));
            Assert.That(State(layout, model), Is.EqualTo(before));
        }

        [Test]
        public void UnaffordableUnknownExtremeAndBlockedMovesAreAtomicAndFreeMovesNeedNoCash()
        {
            var layout = new ShopLayout(); var model = Cash(0); string before = State(layout, model);
            Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Failure, Is.EqualTo(LayoutFailure.InsufficientFunds));
            Assert.That(layout.TryExpandLab(null, ShopDayPhase.Night).Failure, Is.EqualTo(LayoutFailure.InvalidModel));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, "tank-a", 0, 0, 0).Failure, Is.EqualTo(LayoutFailure.UnknownFacility));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, int.MaxValue, 0, 0).Failure, Is.EqualTo(LayoutFailure.OutOfBounds));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, int.MinValue, 0).Failure, Is.EqualTo(LayoutFailure.OutOfBounds));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 0, 4).Failure, Is.EqualTo(LayoutFailure.InvalidRotation));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 8, 0).Failure, Is.EqualTo(LayoutFailure.OutOfBounds));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 6, 0, 0).Failure, Is.EqualTo(LayoutFailure.OverlapsFurniture));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 8, -4, 0).Failure, Is.EqualTo(LayoutFailure.ProtectedAccess));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 0, 2).Failure, Is.EqualTo(LayoutFailure.UnreachableAccess));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Success(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.BloomFacilityId, 0, 0, 0));
            Assert.That(State(layout, model), Is.EqualTo(before));
            Assert.That(layout.TryPreviewStore(ShopLayout.IncubatorFacilityId, out _).Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            Assert.That(layout.TryPreviewSell(ShopLayout.UpgradeFacilityId, out _).Failure, Is.EqualTo(LayoutFailure.UnknownItem));
            Assert.That(layout.Facilities.Count, Is.EqualTo(4));
        }

        [Test]
        public void LaterFurnitureTransactionsAndShuffledRestoreKeepFacilityPosesAndSnapshotIsolation()
        {
            var layout = new ShopLayout(); var model = Cash(300);
            Success(layout.TryExpandLab(model, ShopDayPhase.Night));
            var oldView = layout.Facilities;
            Success(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.IncubatorFacilityId, 0, 8, 0));
            Success(layout.TryCommit(model, ShopDayPhase.Night, null, -4, -1, 0, out var table));
            Success(layout.TryStore(model, ShopDayPhase.Night, table));
            Success(layout.TrySell(model, ShopDayPhase.Night, table));
            Success(layout.TryExpand(model, ShopDayPhase.Night));
            Assert.That(layout.GetFacility(ShopLayout.IncubatorFacilityId).Value.Z, Is.EqualTo(8));
            Assert.That(oldView[2].Z, Is.Zero);
            var save = layout.CaptureSave(); Array.Reverse(save.facilities);
            var json = JsonUtility.ToJson(save);
            Assert.That(ShopLayout.TryRestore(JsonUtility.FromJson<LayoutSave>(json), out var restored), Is.True);
            Assert.That(restored.LabTier, Is.EqualTo(1)); Assert.That(restored.StoreTier, Is.EqualTo(1));
            Assert.That(restored.Facilities[2].Id, Is.EqualTo(ShopLayout.IncubatorFacilityId));
            Assert.That(restored.Facilities[2].WorldZ, Is.EqualTo(7.85f).Within(.0001f));
            save.facilities[0].z = 99;
            Assert.That(layout.GetFacility(ShopLayout.UpgradeFacilityId).Value.Z, Is.Zero);
            Assert.That(restored.GetFacility(ShopLayout.UpgradeFacilityId).Value.Z, Is.Zero);
        }

        [Test]
        public void ModernRestoreRejectsMissingOrCorruptEssentialFacilitiesWithoutRepair()
        {
            foreach (string fault in new[] { "null", "empty", "missing", "duplicate", "unknown", "null item", "rotation", "offset", "overlap", "operator", "lab below", "lab above" })
            {
                var save = new ShopLayout().CaptureSave();
                switch (fault)
                {
                    case "null": save.facilities = null; break;
                    case "empty": save.facilities = Array.Empty<FacilitySave>(); break;
                    case "missing": Array.Resize(ref save.facilities, 3); break;
                    case "duplicate": save.facilities[1].id = save.facilities[0].id; break;
                    case "unknown": save.facilities[1].id = "table-1"; break;
                    case "null item": save.facilities[1] = null; break;
                    case "rotation": save.facilities[0].quarterTurns = -1; break;
                    case "offset": save.facilities[0].z = int.MaxValue; break;
                    case "overlap": save.facilities[0].x = 6; break;
                    case "operator": save.facilities[0].quarterTurns = 2; break;
                    case "lab below": save.labTier = -1; break;
                    case "lab above": save.labTier = 3; break;
                }
                Assert.That(ShopLayout.TryRestore(save, out var rejected), Is.False, fault);
                Assert.That(rejected, Is.Null, fault);
            }
        }

        private static void AssertDefaults(ShopLayout layout)
        {
            Assert.That(layout.Facilities.Count, Is.EqualTo(4));
            float[] expectedX = { -4.2f, -1.4f, 2f, -5.5f }, expectedZ = { 3.85f, 3.85f, 3.85f, 1.9f };
            for (int i = 0; i < 4; i++)
            {
                var facility = layout.Facilities[i];
                Assert.That(facility.X, Is.Zero); Assert.That(facility.Z, Is.Zero);
                Assert.That(facility.WorldX, Is.EqualTo(expectedX[i])); Assert.That(facility.WorldZ, Is.EqualTo(expectedZ[i]));
                Assert.That(facility.QuarterTurns, Is.EqualTo(i == 3 ? 3 : 0));
            }
            Assert.That(layout.GetFacility("missing"), Is.Null);
            Assert.That(layout.TryGetFacilityAccessPoint(ShopLayout.UpgradeFacilityId, out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(-4)); Assert.That(z, Is.EqualTo(1.9f));
        }
        private static GameModel Cash(int amount)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.currency = amount;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
        private static void Produce(GameModel model, string slot)
        {
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow));
            Ok(model.BeginIncubation()); model.Tick(10); Ok(model.TransferToTank(slot));
        }
        private static string State(ShopLayout layout, GameModel model)
            => JsonUtility.ToJson(layout.CaptureSave()) + JsonUtility.ToJson(model.CaptureSave());
        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        private static void Success(LayoutResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
