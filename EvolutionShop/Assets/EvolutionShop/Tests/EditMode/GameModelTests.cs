using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class GameModelTests
    {
        private GameConfig config;
        private GameModel model;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
        }

        [Test]
        public void FreshGameStartsRecoverableWithPinnedPrototypeTuning()
        {
            Assert.That(model.Currency, Is.Zero);
            Assert.That(model.Capacity, Is.EqualTo(4));
            Assert.That(model.CollectionSecondsPerUnit, Is.EqualTo(0.6f));
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(4));
            Assert.That(model.GetGrower(CultureKind.Flow).Capacity, Is.EqualTo(6));
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing));
            Assert.That(model.DiscoveredSpeciesIds, Is.Empty);
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.GetTank("tank-b"), Is.Null);
        }

        [Test]
        public void GrowersRefillFreelyAndDiscardTimeSpentAtCapacity()
        {
            config.InitialGrowerUnits = 0;
            model = new GameModel(config);
            Assert.That(model.Collect(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.GrowerEmpty));
            model.Tick(2f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(1));
            model.Tick(1000f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(6));
            AssertOk(model.Collect(CultureKind.Bloom));
            model.Tick(1f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(5));
            model.Tick(1f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(6));
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void GrowersHandleMultipleRefillsAndPreserveFractionalProgress()
        {
            config.InitialGrowerUnits = 0;
            model = new GameModel(config);
            model.Tick(5f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(2));
            Assert.That(model.GetGrower(CultureKind.Flow).SecondsToNextUnit, Is.EqualTo(1f));
        }

        [Test]
        public void ReservoirCapacityIsSharedAcrossCultureTypes()
        {
            AssertOk(model.Collect(CultureKind.Bloom));
            AssertOk(model.Collect(CultureKind.Flow));
            AssertOk(model.Collect(CultureKind.Bloom));
            AssertOk(model.Collect(CultureKind.Flow));
            int units = model.GetGrower(CultureKind.Flow).Units;
            Assert.That(model.Collect(CultureKind.Flow).Failure, Is.EqualTo(FailureCode.ReservoirFull));
            Assert.That(model.GetGrower(CultureKind.Flow).Units, Is.EqualTo(units));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(4));
        }

        [Test]
        public void UnsupportedMixAlwaysRetainsEnoughReturnSpace()
        {
            Stage(2, 2);
            Assert.That(model.BeginIncubation().Failure, Is.EqualTo(FailureCode.InvalidRecipe));
            Assert.That(model.Incubator.Bloom, Is.EqualTo(2));
            Assert.That(model.Incubator.Flow, Is.EqualTo(2));
            Assert.That(model.Collect(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.ReservoirFull));
            AssertOk(model.ClearDoses());
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(2));
            Assert.That(model.Available(CultureKind.Flow), Is.EqualTo(2));
            Assert.That(model.Incubator.Bloom + model.Incubator.Flow, Is.Zero);
            Assert.That(model.ReservedCultureCount, Is.EqualTo(4));
        }

        [Test]
        public void DosingAndReturningOneUnitDoNotChangeTotalOwnedCultures()
        {
            AssertOk(model.Collect(CultureKind.Bloom));
            AssertOk(model.Dose(CultureKind.Bloom));
            Assert.That(model.Available(CultureKind.Bloom), Is.Zero);
            Assert.That(model.Staged(CultureKind.Bloom), Is.EqualTo(1));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(1));
            AssertOk(model.ReturnDose(CultureKind.Bloom));
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(1));
            Assert.That(model.ReturnDose(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.AlreadyEmpty));
            Assert.That(model.Dose(CultureKind.Flow).Failure, Is.EqualTo(FailureCode.InsufficientCulture));
        }

        [TestCase(1, 1, "pondlet")]
        [TestCase(2, 1, "bubblebelly")]
        [TestCase(1, 2, "ribbonfin")]
        public void ExactRecipesConsumeOnceAndProduceExactlyOneSpecimen(int bloom, int flow, string expected)
        {
            Stage(bloom, flow);
            var start = model.BeginIncubation();
            AssertOk(start);
            Assert.That(start.SpeciesId, Is.EqualTo(expected));
            string id = start.SpecimenId;
            Assert.That(id, Is.Not.Empty);
            Assert.That(model.ReservedCultureCount, Is.Zero);
            Assert.That(model.BeginIncubation().Failure, Is.EqualTo(FailureCode.IncubatorBusy));
            Assert.That(model.ClearDoses().Failure, Is.EqualTo(FailureCode.IncubatorBusy));
            Assert.That(model.Dose(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.IncubatorBusy));
            Assert.That(model.TransferToTank("tank-a").Failure, Is.EqualTo(FailureCode.OutputNotReady));
            Assert.That(model.Tick(9f).Matured, Is.False);
            var finish = model.Tick(1f);
            Assert.That(finish.Matured, Is.True);
            Assert.That(finish.FirstDiscovery, Is.True);
            Assert.That(finish.SpecimenId, Is.EqualTo(id));
            Assert.That(model.IsDiscovered(expected), Is.True);
            Assert.That(model.Tick(1000f).Matured, Is.False);
            AssertOk(model.TransferToTank("tank-a"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
            Assert.That(model.TransferToTank("tank-b").Failure, Is.EqualTo(FailureCode.OutputNotReady));
            Assert.That(model.GetTank("tank-b"), Is.Null);
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(3, 1)]
        [TestCase(2, 2)]
        public void UnsupportedIntegerRecipesAreRejectedWithoutLoss(int bloom, int flow)
        {
            Stage(bloom, flow);
            Assert.That(model.BeginIncubation().Failure, Is.EqualTo(FailureCode.InvalidRecipe));
            Assert.That(model.Staged(CultureKind.Bloom), Is.EqualTo(bloom));
            Assert.That(model.Staged(CultureKind.Flow), Is.EqualTo(flow));
            Assert.That(model.DiscoveredSpeciesIds, Is.Empty);
        }

        [Test]
        public void IncubationAndGrowersContinueDuringOtherWork()
        {
            Stage(1, 1);
            AssertOk(model.BeginIncubation());
            AssertOk(model.Collect(CultureKind.Bloom));
            AssertOk(model.Collect(CultureKind.Flow));
            model.Tick(5f);
            Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(5f));
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(4));
            Assert.That(model.GetGrower(CultureKind.Flow).Units, Is.EqualTo(4));
        }

        [Test]
        public void FullTankDoesNotConsumeReadyOutputAndSelectedTransferUsesStableId()
        {
            Produce("pondlet", "tank-a");
            Produce("bubblebelly", "tank-b");
            Stage(1, 2);
            AssertOk(model.BeginIncubation());
            model.Tick(10f);
            string waitingId = model.Incubator.SpecimenId;
            Assert.That(model.TransferToTank("tank-a").Failure, Is.EqualTo(FailureCode.TankFull));
            Assert.That(model.TransferToTank("tank-b").Failure, Is.EqualTo(FailureCode.TankFull));
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(waitingId));
            AssertOk(model.SellFromTank("tank-b"));
            AssertOk(model.TransferToTank("tank-b"));
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo(waitingId));
            Assert.That(model.GetTank("tank-a").Value.SpeciesId, Is.EqualTo("pondlet"));
        }

        [Test]
        public void SellingPaysOnlyOnceAndRetainsDiscovery()
        {
            Produce("pondlet", "tank-a");
            Produce("ribbonfin", "tank-b");
            string untouchedId = model.GetTank("tank-a").Value.Id;
            AssertOk(model.SellFromTank("tank-b"));
            Assert.That(model.Currency, Is.EqualTo(10));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(untouchedId));
            Assert.That(model.GetTank("tank-b"), Is.Null);
            Assert.That(model.SellFromTank("tank-b").Failure, Is.EqualTo(FailureCode.TankEmpty));
            Assert.That(model.Currency, Is.EqualTo(10));
            Assert.That(model.IsDiscovered("ribbonfin"), Is.True);
        }

        [Test]
        public void KnownRecipesCanBePreparedFromAvailableAndStagedPool()
        {
            Assert.That(model.UseKnownRecipe("pondlet").Failure, Is.EqualTo(FailureCode.UndiscoveredRecipe));
            Produce("pondlet", "tank-a");
            Stage(2, 1);
            AssertOk(model.UseKnownRecipe("pondlet"));
            Assert.That(model.Staged(CultureKind.Bloom), Is.EqualTo(1));
            Assert.That(model.Staged(CultureKind.Flow), Is.EqualTo(1));
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(1));
            AssertOk(model.BeginIncubation());
            Assert.That(model.Tick(10f).FirstDiscovery, Is.False);
            Assert.That(model.DiscoveredSpeciesIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void KnownRecipeWithInsufficientIngredientsLeavesPoolUntouched()
        {
            Produce("pondlet", "tank-a");
            Stage(2, 0);
            Assert.That(model.UseKnownRecipe("pondlet").Failure, Is.EqualTo(FailureCode.InsufficientCulture));
            Assert.That(model.Staged(CultureKind.Bloom), Is.EqualTo(2));
            Assert.That(model.Available(CultureKind.Bloom), Is.Zero);
        }

        [Test]
        public void ThreeDifferentSalesFundOneNoticeableUpgrade()
        {
            Assert.That(model.BuyUpgrade().Failure, Is.EqualTo(FailureCode.InsufficientFunds));
            foreach (var recipe in config.Species)
            {
                if (recipe.Spark > 0) continue; // This baseline economy fixture sells the three starter species.
                Produce(recipe.Id, "tank-a");
                AssertOk(model.SellFromTank("tank-a"));
            }
            Assert.That(model.Currency, Is.EqualTo(26));
            AssertOk(model.BuyUpgrade());
            Assert.That(model.Currency, Is.EqualTo(2));
            Assert.That(model.UpgradeOwned, Is.True);
            Assert.That(model.Capacity, Is.EqualTo(8));
            Assert.That(model.CollectionSecondsPerUnit, Is.EqualTo(0.3f));
            Assert.That(model.BuyUpgrade().Failure, Is.EqualTo(FailureCode.AlreadyUpgraded));
            Assert.That(model.Currency, Is.EqualTo(2));
            for (int i = 0; i < 4; i++)
            {
                AssertOk(model.Collect(CultureKind.Bloom));
                AssertOk(model.Collect(CultureKind.Flow));
            }
            Assert.That(model.ReservedCultureCount, Is.EqualTo(8));
            Assert.That(model.Collect(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.ReservoirFull));
        }

        [Test]
        public void InvalidIdsRejectTransactionsWithoutMutation()
        {
            Assert.That(model.Collect((CultureKind)(-1)).Failure, Is.EqualTo(FailureCode.InvalidCulture));
            Assert.That(model.Dose((CultureKind)99).Failure, Is.EqualTo(FailureCode.InvalidCulture));
            Assert.That(model.ReturnDose((CultureKind)99).Failure, Is.EqualTo(FailureCode.InvalidCulture));
            Assert.That(model.TransferToTank("missing").Failure, Is.EqualTo(FailureCode.UnknownTank));
            Assert.That(model.SellFromTank(null).Failure, Is.EqualTo(FailureCode.UnknownTank));
            Assert.That(model.UseKnownRecipe("missing").Failure, Is.EqualTo(FailureCode.UnknownSpecies));
            Assert.That(model.ReservedCultureCount, Is.Zero);
            Assert.That(model.Currency, Is.Zero);
        }

        [Test]
        public void JsonRoundTripDuringIncubationPreservesRemainingTimeAndSingleOutput()
        {
            Stage(1, 2);
            AssertOk(model.BeginIncubation());
            model.Tick(3.5f);
            string id = model.Incubator.SpecimenId;
            model = RoundTrip(model);
            Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(6.5f));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(id));
            Assert.That(model.ReservedCultureCount, Is.Zero);
            Assert.That(model.Tick(6f).Matured, Is.False);
            Assert.That(model.Tick(0.5f).Matured, Is.True);
            Assert.That(model.Tick(0.5f).Matured, Is.False);
            AssertOk(model.TransferToTank("tank-a"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
            Assert.That(model.TransferToTank("tank-b").Success, Is.False);
        }

        [Test]
        public void JsonRoundTripAfterSalePreservesPaymentAndDiscoveryWithoutResurrectingSpecimen()
        {
            Produce("bubblebelly", "tank-a");
            AssertOk(model.SellFromTank("tank-a"));
            model = RoundTrip(model);
            Assert.That(model.Currency, Is.EqualTo(10));
            Assert.That(model.IsDiscovered("bubblebelly"), Is.True);
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.SellFromTank("tank-a").Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(10));
        }

        [Test]
        public void JsonRoundTripPreservesReadyOutputAndTankPlacement()
        {
            Produce("pondlet", "tank-b");
            Stage(2, 1);
            AssertOk(model.BeginIncubation());
            model.Tick(10f);
            string tankId = model.GetTank("tank-b").Value.Id;
            string readyId = model.Incubator.SpecimenId;
            model = RoundTrip(model);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(readyId));
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo(tankId));
            Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.DiscoveredSpeciesIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void JsonRoundTripPreservesPartialGrowerTimeAndStagedDoses()
        {
            Stage(2, 1);
            model.Tick(0.75f);
            model = RoundTrip(model);
            Assert.That(model.Staged(CultureKind.Bloom), Is.EqualTo(2));
            Assert.That(model.Staged(CultureKind.Flow), Is.EqualTo(1));
            Assert.That(model.GetGrower(CultureKind.Bloom).SecondsToNextUnit, Is.EqualTo(1.25f));
            model.Tick(1f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(2));
            model.Tick(0.25f);
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(3));
        }

        [Test]
        public void JsonRoundTripPreservesUpgradeAndEightUnitCapacity()
        {
            foreach (var recipe in config.Species)
            {
                if (recipe.Spark > 0) continue; // Research is a separate purchase, outside this upgrade round trip.
                Produce(recipe.Id, "tank-a");
                AssertOk(model.SellFromTank("tank-a"));
            }
            AssertOk(model.BuyUpgrade());
            for (int i = 0; i < 4; i++)
            {
                AssertOk(model.Collect(CultureKind.Bloom));
                AssertOk(model.Collect(CultureKind.Flow));
            }
            model = RoundTrip(model);
            Assert.That(model.UpgradeOwned, Is.True);
            Assert.That(model.Capacity, Is.EqualTo(8));
            Assert.That(model.Currency, Is.EqualTo(2));
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(4));
            Assert.That(model.Available(CultureKind.Flow), Is.EqualTo(4));
            Assert.That(model.DiscoveredSpeciesIds.Count, Is.EqualTo(3));
        }

        [Test]
        public void CapturedAndRestoredDataCannotMutateModelAuthority()
        {
            Stage(1, 1);
            var save = model.CaptureSave();
            save.incubator.stagedBloom = 999;
            save.growers[0].units = 999;
            Assert.That(model.Staged(CultureKind.Bloom), Is.EqualTo(1));
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(3));
            save = model.CaptureSave();
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.True, error.ToString());
            save.availableBloom = 999;
            save.incubator.stagedBloom = 999;
            Assert.That(restored.Available(CultureKind.Bloom), Is.Zero);
            Assert.That(restored.Staged(CultureKind.Bloom), Is.EqualTo(1));
        }

        [Test]
        public void ConfigurationAndDefinitionCopiesPreventExternalMutation()
        {
            config.BaseCapacity = 99;
            config.Species[0].SaleValue = 999;
            var definition = model.GetSpecies("pondlet");
            definition.SaleValue = 1000;
            Produce("pondlet", "tank-a");
            AssertOk(model.SellFromTank("tank-a"));
            Assert.That(model.Capacity, Is.EqualTo(4));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [TestCase("version")]
        [TestCase("negative-money")]
        [TestCase("negative-culture")]
        [TestCase("over-capacity")]
        [TestCase("null-growers")]
        [TestCase("duplicate-grower")]
        [TestCase("grower-over-capacity")]
        [TestCase("negative-timer")]
        [TestCase("nan-timer")]
        [TestCase("unknown-discovery")]
        [TestCase("duplicate-discovery")]
        [TestCase("duplicate-tank")]
        [TestCase("unknown-tank")]
        [TestCase("partial-specimen")]
        [TestCase("unknown-phase")]
        [TestCase("active-without-species")]
        [TestCase("dosing-with-active-specimen")]
        public void InvalidSavesAreRejectedAtomically(string problem)
        {
            var save = model.CaptureSave();
            switch (problem)
            {
                case "version": save.version = 99; break;
                case "negative-money": save.currency = -1; break;
                case "negative-culture": save.availableFlow = -1; break;
                case "over-capacity": save.availableBloom = int.MaxValue; save.availableFlow = int.MaxValue; break;
                case "null-growers": save.growers = null; break;
                case "duplicate-grower": save.growers[1].cultureId = "bloom"; break;
                case "grower-over-capacity": save.growers[0].units = 7; break;
                case "negative-timer": save.growers[0].secondsUntilNextUnit = -1f; break;
                case "nan-timer": save.incubator.secondsRemaining = float.NaN; break;
                case "unknown-discovery": save.discoveries = new[] { "unknown" }; break;
                case "duplicate-discovery": save.discoveries = new[] { "pondlet", "pondlet" }; break;
                case "duplicate-tank": save.tanks[1].tankId = "tank-a"; break;
                case "unknown-tank": save.tanks[0].tankId = "unknown"; break;
                case "partial-specimen": save.tanks[0].speciesId = "pondlet"; break;
                case "unknown-phase": save.incubator.phase = (IncubatorPhase)900; break;
                case "active-without-species": save.incubator.phase = IncubatorPhase.Incubating; save.incubator.secondsRemaining = 2f; break;
                case "dosing-with-active-specimen": save.incubator.specimenId = "leftover"; break;
            }
            Assert.That(GameModel.TryRestore(config, save, out var loaded, out var error), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.EqualTo(FailureCode.InvalidSave));
            Assert.That(model.Currency, Is.Zero);
            Assert.That(model.ReservedCultureCount, Is.Zero);
        }

        [Test]
        public void DuplicateSpecimenAcrossTankAndIncubatorIsRejected()
        {
            Produce("pondlet", "tank-a");
            Stage(1, 1);
            AssertOk(model.BeginIncubation());
            var save = model.CaptureSave();
            save.incubator.specimenId = save.tanks[0].specimenId;
            Assert.That(GameModel.TryRestore(config, save, out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void ReadyOutputWithoutRecordedDiscoveryIsRejected()
        {
            Stage(1, 1);
            AssertOk(model.BeginIncubation());
            model.Tick(10f);
            var save = model.CaptureSave();
            save.discoveries = Array.Empty<string>();
            Assert.That(GameModel.TryRestore(config, save, out _, out _), Is.False);
        }

        [Test]
        public void SaleOverflowDoesNotRemoveSpecimen()
        {
            Produce("pondlet", "tank-a");
            var save = model.CaptureSave();
            save.currency = int.MaxValue;
            Assert.That(GameModel.TryRestore(config, save, out model, out _), Is.True);
            string id = model.GetTank("tank-a").Value.Id;
            Assert.That(model.SellFromTank("tank-a").Failure, Is.EqualTo(FailureCode.CurrencyOverflow));
            Assert.That(model.Currency, Is.EqualTo(int.MaxValue));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(id));
        }

        [Test]
        public void InvalidTickDeltaCannotAdvanceSimulation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Tick(float.PositiveInfinity));
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(4));
        }

        [Test]
        public void AmbiguousRecipesAndInvalidTuningAreRejected()
        {
            config.Species[1].Bloom = 1;
            config.Species[1].Flow = 1;
            Assert.Throws<ArgumentException>(() => new GameModel(config));
            config = GameConfig.CreateDefault();
            config.GrowerRefillSeconds = 0f;
            Assert.Throws<ArgumentException>(() => new GameModel(config));
        }

        [TestCase(CultureKind.Bloom, false)]
        [TestCase(CultureKind.Flow, false)]
        [TestCase(CultureKind.Bloom, true)]
        [TestCase(CultureKind.Flow, true)]
        public void ReleaseRecoversSingleCultureSaturationIntoProductionAndSale(CultureKind saturated, bool upgraded)
        {
            if (upgraded)
            {
                var save = model.CaptureSave();
                save.upgradeOwned = true;
                Assert.That(GameModel.TryRestore(config, save, out model, out var error), Is.True, error.ToString());
            }
            CultureKind missing = saturated == CultureKind.Bloom ? CultureKind.Flow : CultureKind.Bloom;
            int capacity = model.Capacity;
            for (int i = 0; i < capacity; i++)
            {
                if (model.GetGrower(saturated).Units == 0) model.Tick(config.GrowerRefillSeconds);
                AssertOk(model.Collect(saturated));
            }
            Assert.That(model.ReservedCultureCount, Is.EqualTo(capacity));
            Assert.That(model.Collect(missing).Failure, Is.EqualTo(FailureCode.ReservoirFull));

            AssertOk(model.ReleaseCulture(saturated));
            Assert.That(model.Available(saturated), Is.EqualTo(capacity - 1));
            AssertOk(model.Collect(missing));
            AssertOk(model.Dose(CultureKind.Bloom));
            AssertOk(model.Dose(CultureKind.Flow));
            AssertOk(model.BeginIncubation());
            Assert.That(model.ReservedCultureCount, Is.EqualTo(capacity - 2));
            Assert.That(model.Tick(config.IncubationSeconds).Matured, Is.True);
            AssertOk(model.TransferToTank("tank-a"));
            AssertOk(model.SellFromTank("tank-a"));
            Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(model.IsDiscovered("pondlet"), Is.True);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        public void ReleaseInvalidCultureIsAtomic(int invalidKind)
        {
            Stage(1, 1);
            AssertOk(model.Collect(CultureKind.Bloom));
            string before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(model.ReleaseCulture((CultureKind)invalidKind).Failure, Is.EqualTo(FailureCode.InvalidCulture));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
        }

        [Test]
        public void ReleaseWithoutAvailableCultureCannotConsumeStagedIngredients()
        {
            Stage(2, 2);
            string before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(model.ReleaseCulture(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.InsufficientCulture));
            Assert.That(model.ReleaseCulture(CultureKind.Flow).Failure, Is.EqualTo(FailureCode.InsufficientCulture));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
            AssertOk(model.ClearDoses());
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(2));
            Assert.That(model.Available(CultureKind.Flow), Is.EqualTo(2));
        }

        [Test]
        public void ReleaseDuringIncubationChangesOnlyOneAvailableUnit()
        {
            Stage(1, 1);
            AssertOk(model.BeginIncubation());
            model.Tick(2f);
            AssertOk(model.Collect(CultureKind.Bloom));
            var expected = model.CaptureSave();
            expected.availableBloom--;
            AssertOk(model.ReleaseCulture(CultureKind.Bloom));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expected)));
            string beforeEmptyRelease = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(model.ReleaseCulture(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.InsufficientCulture));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(beforeEmptyRelease));
            Assert.That(model.Tick(8f).Matured, Is.True);
            AssertOk(model.TransferToTank("tank-a"));
            Assert.That(model.GetTank("tank-a").Value.SpeciesId, Is.EqualTo("pondlet"));
        }
        private void Stage(int bloom, int flow)
        {
            for (int i = 0; i < bloom; i++) { AssertOk(model.Collect(CultureKind.Bloom)); AssertOk(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < flow; i++) { AssertOk(model.Collect(CultureKind.Flow)); AssertOk(model.Dose(CultureKind.Flow)); }
        }

        private void Produce(string speciesId, string tankId)
        {
            var recipe = model.GetSpecies(speciesId);
            Stage(recipe.Bloom, recipe.Flow);
            AssertOk(model.BeginIncubation());
            model.Tick(config.IncubationSeconds);
            AssertOk(model.TransferToTank(tankId));
        }

        private GameModel RoundTrip(GameModel source)
        {
            string json = JsonUtility.ToJson(source.CaptureSave());
            var save = JsonUtility.FromJson<SaveData>(json);
            Assert.That(GameModel.TryRestore(config, save, out var result, out var error), Is.True, error.ToString());
            return result;
        }

        private static void AssertOk(ActionResult result)
        {
            Assert.That(result.Success, Is.True, result.Failure.ToString());
        }
    }
}
