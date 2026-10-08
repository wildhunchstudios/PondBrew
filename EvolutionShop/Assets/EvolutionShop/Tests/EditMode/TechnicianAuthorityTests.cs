using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class TechnicianAuthorityTests
    {
        [TestCase("pondlet")]
        [TestCase("bubblebelly")]
        [TestCase("ribbonfin")]
        [TestCase("lanternfin")]
        [TestCase("driftwing")]
        public void ExactKnownRecipeCommitsStableIdentityOnceAndExistingClockOwnsMaturity(string speciesId)
        {
            var model = Fixture(); var recipe = model.GetSpecies(speciesId);
            int bloom = model.Available(CultureKind.Bloom), flow = model.Available(CultureKind.Flow);
            int spark = model.Available(CultureKind.Spark), mist = model.Available(CultureKind.Mist);
            string before = State(model); int money = model.Currency;
            Ok(model.ReserveTechnicianStart("job", "moss", speciesId, "rack-1", "planned"));
            Assert.That(State(model), Is.EqualTo(before), "Claims do not move or serialize inventory.");
            var claim = model.TechnicianClaim.Value;
            Assert.That(claim.JobId, Is.EqualTo("job")); Assert.That(claim.OwnerId, Is.EqualTo("moss"));
            Assert.That(claim.SpeciesId, Is.EqualTo(speciesId)); Assert.That(claim.SpecimenId, Is.EqualTo("planned"));
            Assert.That(claim.StorageId, Is.EqualTo("rack-1")); Assert.That(claim.StartsRecipe, Is.True);
            Assert.That(model.IsIncubatorReservedForTechnician, Is.True);
            Assert.That(model.IsStorageReservedForTechnician("rack-1"), Is.True);
            Ok(model.CompleteTechnicianStart("job", "moss"));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo("planned")); Assert.That(model.Incubator.SpeciesId, Is.EqualTo(speciesId));
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Incubating));
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(bloom - recipe.Bloom));
            Assert.That(model.Available(CultureKind.Flow), Is.EqualTo(flow - recipe.Flow));
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(spark - recipe.Spark));
            Assert.That(model.Available(CultureKind.Mist), Is.EqualTo(mist - recipe.Mist));
            Assert.That(model.Currency, Is.EqualTo(money)); Assert.That(model.TechnicianClaim, Is.Null);
            before = State(model);
            Assert.That(model.CompleteTechnicianStart("job", "moss").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.Tick(9.75f).Matured, Is.False);
            Assert.That(model.Tick(.25f).Matured, Is.True);
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo("planned"));
        }

        [Test]
        public void StagedSaveAndOriginalCommitShareIdentityWithoutReplacingLiveCustomerAndStockerAuthorities()
        {
            var model = Fixture();
            Ok(model.TransferStorageToTank("rack-3", "tank-a", "existing"));
            Ok(model.ReserveTank("tank-a", "visitor"));
            var save = model.CaptureSave(); save.storage[2].specimenId = "second"; save.storage[2].speciesId = "pondlet";
            // Restore once to add the independent rack fixture, then establish live reservations.
            model = Restore(Config(), save); Ok(model.ReserveTank("tank-a", "visitor"));
            Ok(model.ReserveStockTransfer("nori-job", "nori", "rack-3", "tank-b", "second"));
            Ok(model.ReserveTechnicianStart("moss-job", "moss", "pondlet", "rack-1", "stable"));
            var candidate = Restore(Config(), model.CaptureSave());
            Ok(candidate.ReserveTechnicianStart("moss-job", "moss", "pondlet", "rack-1", "stable"));
            Ok(candidate.CompleteTechnicianStart("moss-job", "moss"));
            Ok(model.CompleteTechnicianStart("moss-job", "moss"));
            Assert.That(State(model), Is.EqualTo(State(candidate)));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("visitor"));
            Assert.That(model.GetStockTransfer("nori-job").Value.SpecimenId, Is.EqualTo("second"));
            Ok(model.CompleteStockTransfer("nori-job", "nori"));
            Ok(model.SellReservedTank("tank-a", "visitor", "existing"));
            Assert.That(model.Currency, Is.EqualTo(306));
        }

        [Test]
        public void IdenticalReserveIsIdempotentAndOtherOwnersCannotStealCompleteOrCancel()
        {
            var model = Fixture(); string before = State(model);
            Ok(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", "planned"));
            Ok(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", "planned"));
            Assert.That(model.ReserveTechnicianStart("job", "thief", "pondlet", "rack-1", "planned").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-2", "planned").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.ReserveTechnicianStart("other-job", "moss", "pondlet", "rack-2", "other").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.CompleteTechnicianStart("job", "thief").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.CompleteTechnicianOutput("job", "moss").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.CancelTechnicianClaim("job", "thief"), Is.False);
            Assert.That(model.CancelTechnicianClaim(null, "moss"), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(model.CancelTechnicianClaim("job", "moss"), Is.True);
            Assert.That(model.CancelTechnicianClaim("job", "moss"), Is.False);
            Assert.That(model.TechnicianClaim, Is.Null);
            Ok(model.UseKnownRecipe("pondlet")); Ok(model.BeginIncubation());
        }

        [TestCase(null, "moss", "planned")]
        [TestCase(" ", "moss", "planned")]
        [TestCase("job", null, "planned")]
        [TestCase("job", "", "planned")]
        [TestCase("job", "moss", null)]
        [TestCase("job", "moss", " ")]
        public void InvalidIdentitiesCannotReserveOrMutate(string job, string owner, string specimen)
        {
            var model = Fixture(); string before = State(model);
            Assert.That(model.ReserveTechnicianStart(job, owner, "pondlet", "rack-1", specimen).Success, Is.False);
            Assert.That(model.ReserveTechnicianStart(new string('j', 129), "moss", "pondlet", "rack-1", "planned").Success, Is.False);
            Assert.That(model.ReserveTechnicianStart("job", new string('m', 129), "pondlet", "rack-1", "planned").Success, Is.False);
            Assert.That(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", new string('p', 129)).Failure, Is.EqualTo(FailureCode.SpecimenMismatch));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.TechnicianClaim, Is.Null);
            Assert.That(model.IsStorageReservedForTechnician(null), Is.False);
        }

        [TestCase("unknown", FailureCode.UnknownSpecies)]
        [TestCase("undiscovered", FailureCode.UndiscoveredRecipe)]
        [TestCase("shortage", FailureCode.InsufficientCulture)]
        [TestCase("dirty", FailureCode.IncubatorBusy)]
        [TestCase("processing", FailureCode.IncubatorBusy)]
        [TestCase("output", FailureCode.IncubatorBusy)]
        [TestCase("full", FailureCode.StorageFull)]
        [TestCase("missing", FailureCode.UnknownStorage)]
        [TestCase("stocker", FailureCode.StockReserved)]
        public void UnavailableStartResourcesNeverConsumeUnitsOrMakeClaims(string issue, FailureCode expected)
        {
            var model = Fixture(); string speciesId = "pondlet", slot = "rack-1";
            switch (issue)
            {
                case "unknown": speciesId = "absent"; break;
                case "undiscovered":
                    var save = model.CaptureSave(); save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin" };
                    model = Restore(Config(), save); speciesId = "driftwing"; break;
                case "shortage": Ok(model.ReleaseCulture(CultureKind.Flow)); Ok(model.ReleaseCulture(CultureKind.Flow)); break;
                case "dirty": Ok(model.Dose(CultureKind.Bloom)); break;
                case "processing": Ok(model.UseKnownRecipe("pondlet")); Ok(model.BeginIncubation()); break;
                case "output": model = Ready(); break;
                case "full": slot = "rack-3"; break;
                case "missing": slot = "absent"; break;
                case "stocker": Ok(model.ReserveStockTransfer("nori-job", "nori", "rack-3", "tank-a", "existing")); slot = "rack-3"; break;
            }
            string before = State(model);
            Assert.That(model.ReserveTechnicianStart("job", "moss", speciesId, slot, "planned").Failure, Is.EqualTo(expected));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.TechnicianClaim, Is.Null);
        }

        [TestCase("lanternfin")]
        [TestCase("driftwing")]
        public void TechnicianCannotBypassResearchGates(string speciesId)
        {
            var config = Config(); var save = new GameModel(config).CaptureSave();
            save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin" };
            save.availableBloom = save.availableFlow = 1;
            var model = Restore(config, save); string before = State(model);
            Assert.That(model.ReserveTechnicianStart("job", "moss", speciesId, "rack-1", "planned").Failure, Is.EqualTo(FailureCode.ResearchLocked));
            Assert.That(State(model), Is.EqualTo(before));
        }

        [TestCase("rack")]
        [TestCase("tank")]
        [TestCase("conditioner-first")]
        [TestCase("conditioner-second")]
        public void PlannedIdCannotDuplicateAnyExistingInventoryOrBatch(string location)
        {
            var model = Fixture(); var save = model.CaptureSave();
            save.storage[2].specimenId = save.storage[2].speciesId = null;
            if (location == "rack") { save.storage[2].specimenId = "duplicate"; save.storage[2].speciesId = "pondlet"; }
            if (location == "tank") { save.tanks[0].specimenId = "duplicate"; save.tanks[0].speciesId = "pondlet"; }
            if (location.StartsWith("conditioner"))
            {
                save.conditioners[0].phase = ConditionerPhase.Ready;
                if (location.EndsWith("first")) { save.conditioners[0].firstSpecimenId = "duplicate"; save.conditioners[0].firstSpeciesId = "pondlet"; }
                else { save.conditioners[0].secondSpecimenId = "duplicate"; save.conditioners[0].secondSpeciesId = "pondlet"; }
            }
            model = Restore(Config(), save); string before = State(model);
            Assert.That(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", "duplicate").Failure, Is.EqualTo(FailureCode.SpecimenMismatch));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.TechnicianClaim, Is.Null);
        }

        [Test]
        public void ManualStagingAndCompetingDestinationWritesCannotInvalidateStartButSurplusRemainsUsable()
        {
            var model = Fixture(); Ok(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", "planned"));
            string before = State(model);
            foreach (CultureKind culture in Enum.GetValues(typeof(CultureKind)))
            {
                Assert.That(model.Dose(culture).Failure, Is.EqualTo(FailureCode.TechnicianReserved));
                Assert.That(model.ReturnDose(culture).Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            }
            Assert.That(model.ClearDoses().Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.UseKnownRecipe("bubblebelly").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.BeginIncubation().Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.ReserveStockTransfer("nori-job", "nori", "rack-1", "tank-a", "planned").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.TransferStorageToTank("rack-1", "tank-a", "planned").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferStorageToTank("rack-3", "tank-a", "existing")); before = State(model);
            Assert.That(model.ReturnTankToStorage("tank-a", "rack-1", "existing").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.ReleaseCulture(CultureKind.Bloom)); Ok(model.ReleaseCulture(CultureKind.Bloom));
            Assert.That(model.ReleaseCulture(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Ok(model.Collect(CultureKind.Bloom)); // Collection adds to the same reservoir without taking reserved units.
            Ok(model.CompleteTechnicianStart("job", "moss"));
            Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(1));
        }

        [Test]
        public void ConditioningCannotConsumeReservedSparkButCanUseSurplusAndCannotFillClaimedRack()
        {
            var model = Fixture(); Ok(model.ReserveTechnicianStart("job", "moss", "lanternfin", "rack-1", "planned"));
            string before = State(model);
            Assert.That(model.BeginConditioning("vat", new[] { "rack-3" }, new[] { "existing" }).Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.ReleaseCulture(CultureKind.Spark).Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.Collect(CultureKind.Spark));
            Ok(model.BeginConditioning("vat", new[] { "rack-3" }, new[] { "existing" }));
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(1));
            model.Tick(12); before = State(model);
            Assert.That(model.TransferConditionerToStorage("vat", 0, "rack-1", "existing").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferConditionerToStorage("vat", 0, "rack-2", "existing"));
            Ok(model.CompleteTechnicianStart("job", "moss"));
            Assert.That(model.Available(CultureKind.Spark), Is.Zero);
        }

        [Test]
        public void ReadyOutputRemainsInIncubatorUntilOneExactDeliveryAndCanFeedExistingStocker()
        {
            var model = Ready(); string id = model.Incubator.SpecimenId, before = State(model);
            Ok(model.ReserveTechnicianOutput("job", "moss", "rack-1", id));
            Ok(model.ReserveTechnicianOutput("job", "moss", "rack-1", id));
            Assert.That(model.TechnicianClaim.Value.StartsRecipe, Is.False);
            Assert.That(model.TechnicianClaim.Value.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(id)); Assert.That(model.GetStorage("rack-1"), Is.Null);
            Assert.That(model.TransferToTank("tank-a").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.StoreReadyOutput("rack-2").Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(model.CompleteTechnicianStart("job", "moss").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.CompleteTechnicianOutput("job", "other").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(model.CancelTechnicianClaim("job", "other"), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            var candidate = Restore(Config(), model.CaptureSave());
            Ok(candidate.ReserveTechnicianOutput("job", "moss", "rack-1", id)); Ok(candidate.CompleteTechnicianOutput("job", "moss"));
            Ok(model.CompleteTechnicianOutput("job", "moss"));
            Assert.That(State(model), Is.EqualTo(State(candidate)));
            Assert.That(model.GetStorage("rack-1").Value.Id, Is.EqualTo(id));
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing)); Assert.That(model.TechnicianClaim, Is.Null);
            before = State(model);
            Assert.That(model.CompleteTechnicianOutput("job", "moss").Failure, Is.EqualTo(FailureCode.ReservationMismatch));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.ReserveStockTransfer("nori-job", "nori", "rack-1", "tank-a", id)); Ok(model.CompleteStockTransfer("nori-job", "nori"));
            Ok(model.ReserveTank("tank-a", "customer")); Ok(model.SellReservedTank("tank-a", "customer", id));
            Assert.That(model.Currency, Is.EqualTo(306));
        }

        [TestCase("processing", FailureCode.OutputNotReady)]
        [TestCase("wrong-output", FailureCode.SpecimenMismatch)]
        [TestCase("full", FailureCode.StorageFull)]
        [TestCase("unknown", FailureCode.UnknownStorage)]
        [TestCase("stocker", FailureCode.StockReserved)]
        public void OutputReservationRejectsWrongOrUnavailableResourcesAtomically(string issue, FailureCode expected)
        {
            var model = Ready(); string id = model.Incubator.SpecimenId, slot = "rack-1";
            switch (issue)
            {
                case "processing": model = Fixture(); Ok(model.UseKnownRecipe("pondlet")); Ok(model.BeginIncubation()); id = model.Incubator.SpecimenId; break;
                case "wrong-output": id = "stale"; break;
                case "full": slot = "rack-3"; break;
                case "unknown": slot = "absent"; break;
                case "stocker": Ok(model.ReserveStockTransfer("nori-job", "nori", "rack-3", "tank-a", "existing")); slot = "rack-3"; break;
            }
            string before = State(model);
            Assert.That(model.ReserveTechnicianOutput("job", "moss", slot, id).Failure, Is.EqualTo(expected));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.TechnicianClaim, Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void CancellationAndReloadPreserveExactUncommittedSourceAndReleaseManualAccess(bool output)
        {
            var model = output ? Ready() : Fixture(); string id = model.Incubator.SpecimenId;
            string before = State(model);
            if (output) Ok(model.ReserveTechnicianOutput("job", "moss", "rack-1", id));
            else Ok(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", "planned"));
            var restored = Restore(Config(), model.CaptureSave());
            Assert.That(restored.TechnicianClaim, Is.Null); Assert.That(State(restored), Is.EqualTo(before));
            Assert.That(restored.CompleteTechnicianStart("job", "moss").Success, Is.False);
            Assert.That(restored.CompleteTechnicianOutput("job", "moss").Success, Is.False);
            Assert.That(model.CancelTechnicianClaim("job", "moss"), Is.True);
            Assert.That(State(model), Is.EqualTo(before));
            if (output) { Ok(model.StoreReadyOutput("rack-1")); Ok(restored.TransferToTank("tank-a")); }
            else { Ok(model.UseKnownRecipe("pondlet")); Ok(restored.UseKnownRecipe("pondlet")); }
        }

        [TestCase("cultures", FailureCode.InsufficientCulture)]
        [TestCase("slot", FailureCode.StorageFull)]
        [TestCase("duplicate-id", FailureCode.SpecimenMismatch)]
        public void StartCommitRechecksAuthorityBeforeAnyConsumption(string issue, FailureCode expected)
        {
            var model = Fixture(); Ok(model.ReserveTechnicianStart("job", "moss", "pondlet", "rack-1", "planned"));
            if (issue == "cultures") ((int[])Field("available").GetValue(model))[0] = 0;
            else
            {
                var storage = (Dictionary<string, SpecimenView?>)Field("storage").GetValue(model);
                if (issue == "slot") storage["rack-1"] = model.GetStorage("rack-3");
                else
                {
                    var other = Ready(); var save = other.CaptureSave(); save.incubator.specimenId = "planned";
                    other = Restore(Config(), save); Ok(other.StoreReadyOutput("rack-2")); storage["rack-2"] = other.GetStorage("rack-2");
                }
            }
            string before = State(model);
            Assert.That(model.CompleteTechnicianStart("job", "moss").Failure, Is.EqualTo(expected));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.TechnicianClaim.HasValue, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void OutputCommitRechecksExactSpecimenAndEmptyDestination(bool changedSource)
        {
            var model = Ready(); Ok(model.ReserveTechnicianOutput("job", "moss", "rack-1", model.Incubator.SpecimenId));
            if (changedSource) Field("activeSpecimen").SetValue(model, "changed");
            else ((Dictionary<string, SpecimenView?>)Field("storage").GetValue(model))["rack-1"] = model.GetStorage("rack-3");
            string before = State(model);
            Assert.That(model.CompleteTechnicianOutput("job", "moss").Failure,
                Is.EqualTo(changedSource ? FailureCode.SpecimenMismatch : FailureCode.StorageFull));
            Assert.That(State(model), Is.EqualTo(before)); Assert.That(model.TechnicianClaim.HasValue, Is.True);
        }

        [Test]
        public void ClaimsGuardRackRegistryChangesAndIncubatorAndRackFurnitureUntilCancellation()
        {
            var layout = new ShopLayout(); var save = new GameModel(GameConfig.CreateDefault()).CaptureSave();
            save.currency = 300; save.discoveries = new[] { "pondlet" }; save.availableBloom = save.availableFlow = 1;
            var model = Restore(GameConfig.CreateDefault(), save);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, 7, 0, out string rack).Success, Is.True);
            string slot = layout.GetStorageSlotIds(rack)[0];
            Ok(model.ReserveTechnicianStart("job", "moss", "pondlet", slot, "planned"));
            string before = State(model) + JsonUtility.ToJson(layout.CaptureSave());
            var item = layout.GetItem(rack).Value;
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, rack, item.X, item.Z, item.QuarterTurns, out _).Failure, Is.EqualTo(LayoutFailure.TechnicianReserved));
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, rack).Failure, Is.EqualTo(LayoutFailure.TechnicianReserved));
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, rack).Failure, Is.EqualTo(LayoutFailure.TechnicianReserved));
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.IncubatorFacilityId, 0, 0, 0).Failure, Is.EqualTo(LayoutFailure.TechnicianReserved));
            var apply = typeof(GameModel).GetMethod("TryApplyLayoutSlots", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string[]), typeof(string[]), typeof(string[]), typeof(int) }, null);
            var result = (ActionResult)apply.Invoke(model, new object[] { model.TankIds, Array.Empty<string>(), model.ConditionerIds, 5 });
            Assert.That(result.Failure, Is.EqualTo(FailureCode.TechnicianReserved));
            Assert.That(State(model) + JsonUtility.ToJson(layout.CaptureSave()), Is.EqualTo(before));
            Assert.That(model.CancelTechnicianClaim("job", "moss"), Is.True);
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.IncubatorFacilityId, 0, 0, 0).Success, Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, rack).Success, Is.True);
        }

        static GameConfig Config()
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "rack-1", "rack-2", "rack-3" }; config.ConditionerIds = new[] { "vat" }; return config;
        }
        static GameModel Fixture()
        {
            var save = new GameModel(Config()).CaptureSave(); save.currency = 300;
            save.upgradeOwned = true; save.sparkResearchUnlocked = true; save.mistResearchUnlocked = true;
            save.availableBloom = 3; save.availableFlow = 2; save.availableSpark = save.availableMist = 1;
            save.growers[2].units = save.growers[3].units = 4;
            save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };
            save.storage[2].specimenId = "existing"; save.storage[2].speciesId = "pondlet";
            return Restore(Config(), save);
        }
        static GameModel Ready()
        {
            var model = Fixture(); Ok(model.UseKnownRecipe("pondlet")); Ok(model.BeginIncubation()); model.Tick(10); return model;
        }
        static FieldInfo Field(string name) => typeof(GameModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        static GameModel Restore(GameConfig config, SaveData save)
        {
            Assert.That(GameModel.TryRestore(config, save, out var model, out var failure), Is.True, failure.ToString()); return model;
        }
        static string State(GameModel model) => JsonUtility.ToJson(model.CaptureSave());
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
