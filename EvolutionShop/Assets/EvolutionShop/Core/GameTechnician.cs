using System;

namespace EvolutionShop
{
    /// <summary>One transient authority over the incubator and a rack destination; never an inventory copy.</summary>
    public readonly struct TechnicianClaimView
    {
        public readonly string JobId, OwnerId, SpeciesId, SpecimenId, StorageId;
        public readonly bool StartsRecipe;
        internal TechnicianClaimView(string jobId, string ownerId, string speciesId, string specimenId, string storageId, bool startsRecipe)
        { JobId = jobId; OwnerId = ownerId; SpeciesId = speciesId; SpecimenId = specimenId; StorageId = storageId; StartsRecipe = startsRecipe; }
    }

    public sealed partial class GameModel
    {
        // Loading deliberately drops the claim. Unspent cultures and undelivered output stay in their original authority.
        private TechnicianClaimView? technicianClaim;
        public TechnicianClaimView? TechnicianClaim => technicianClaim;
        public bool IsIncubatorReservedForTechnician => technicianClaim.HasValue;
        public bool IsStorageReservedForTechnician(string storageId)
            => storageId != null && technicianClaim.HasValue && technicianClaim.Value.StorageId == storageId;

        public ActionResult ReserveTechnicianStart(string jobId, string ownerId, string speciesId, string storageId, string plannedSpecimenId)
        {
            if (!ValidStockIdentity(jobId) || !ValidStockIdentity(ownerId)) return ActionResult.Fail(FailureCode.ReservationMismatch);
            var claim = new TechnicianClaimView(jobId, ownerId, speciesId, plannedSpecimenId, storageId, true);
            if (technicianClaim.HasValue && !SameTechnicianClaim(technicianClaim.Value, claim))
                return ActionResult.Fail(technicianClaim.Value.JobId == jobId ? FailureCode.ReservationMismatch : FailureCode.TechnicianReserved);
            var valid = ValidateTechnicianStart(claim, out _);
            if (!valid.Success) return valid;
            technicianClaim = claim;
            return ActionResult.Ok(speciesId, plannedSpecimenId);
        }

        public ActionResult ReserveTechnicianOutput(string jobId, string ownerId, string storageId, string expectedSpecimenId)
        {
            if (!ValidStockIdentity(jobId) || !ValidStockIdentity(ownerId)) return ActionResult.Fail(FailureCode.ReservationMismatch);
            var claim = new TechnicianClaimView(jobId, ownerId, activeSpecies, expectedSpecimenId, storageId, false);
            if (technicianClaim.HasValue && !SameTechnicianClaim(technicianClaim.Value, claim))
                return ActionResult.Fail(technicianClaim.Value.JobId == jobId ? FailureCode.ReservationMismatch : FailureCode.TechnicianReserved);
            var valid = ValidateTechnicianOutput(claim);
            if (!valid.Success) return valid;
            technicianClaim = claim;
            return ActionResult.Ok(activeSpecies, activeSpecimen);
        }

        public ActionResult CompleteTechnicianStart(string jobId, string ownerId)
        {
            if (!OwnsTechnicianClaim(jobId, ownerId, true)) return ActionResult.Fail(FailureCode.ReservationMismatch);
            var claim = technicianClaim.Value;
            var valid = ValidateTechnicianStart(claim, out var recipe);
            if (!valid.Success) return valid;
            // The preceding checks cover every UseKnownRecipe rejection. Both operations remain inside this synchronous commit.
            var stagedResult = UseKnownRecipeCore(claim.SpeciesId);
            if (!stagedResult.Success) return stagedResult;
            var started = StartIncubation(recipe, claim.SpecimenId);
            technicianClaim = null;
            return started;
        }

        public ActionResult CompleteTechnicianOutput(string jobId, string ownerId)
        {
            if (!OwnsTechnicianClaim(jobId, ownerId, false)) return ActionResult.Fail(FailureCode.ReservationMismatch);
            var claim = technicianClaim.Value;
            var valid = ValidateTechnicianOutput(claim);
            if (!valid.Success) return valid;
            var specimen = new SpecimenView(activeSpecimen, activeSpecies);
            storage[claim.StorageId] = specimen;
            phase = IncubatorPhase.Dosing;
            activeSpecies = activeSpecimen = null;
            incubationRemaining = 0;
            technicianClaim = null;
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
        }

        public bool CancelTechnicianClaim(string jobId, string ownerId)
        {
            if (jobId == null || !technicianClaim.HasValue || technicianClaim.Value.JobId != jobId || technicianClaim.Value.OwnerId != ownerId) return false;
            technicianClaim = null;
            return true;
        }

        private ActionResult ValidateTechnicianStart(TechnicianClaimView claim, out SpeciesDefinition recipe)
        {
            recipe = null;
            if (phase != IncubatorPhase.Dosing || staged[0] + staged[1] + staged[2] + staged[3] != 0)
                return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (claim.SpeciesId == null || !species.TryGetValue(claim.SpeciesId, out recipe)) return ActionResult.Fail(FailureCode.UnknownSpecies);
            if (recipe.Spark > 0 && !SparkResearchUnlocked || recipe.Mist > 0 && !MistResearchUnlocked) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (!discoveries.Contains(claim.SpeciesId)) return ActionResult.Fail(FailureCode.UndiscoveredRecipe);
            if (!ValidStockIdentity(claim.SpecimenId) || ContainsSpecimenId(claim.SpecimenId)) return ActionResult.Fail(FailureCode.SpecimenMismatch);
            var destination = ValidateTechnicianStorage(claim.StorageId);
            if (!destination.Success) return destination;
            if (available[0] < recipe.Bloom || available[1] < recipe.Flow || available[2] < recipe.Spark || available[3] < recipe.Mist)
                return ActionResult.Fail(FailureCode.InsufficientCulture);
            return ActionResult.Ok(claim.SpeciesId, claim.SpecimenId);
        }

        private ActionResult ValidateTechnicianOutput(TechnicianClaimView claim)
        {
            if (phase != IncubatorPhase.Ready) return ActionResult.Fail(FailureCode.OutputNotReady);
            if (!ValidStockIdentity(claim.SpecimenId) || claim.SpecimenId != activeSpecimen || claim.SpeciesId != activeSpecies)
                return ActionResult.Fail(FailureCode.SpecimenMismatch);
            return ValidateTechnicianStorage(claim.StorageId);
        }

        private ActionResult ValidateTechnicianStorage(string storageId)
        {
            if (storageId == null || !storage.TryGetValue(storageId, out var target)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (IsStorageReservedForStocking(storageId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (target.HasValue) return ActionResult.Fail(FailureCode.StorageFull);
            return ActionResult.Ok();
        }

        private bool OwnsTechnicianClaim(string jobId, string ownerId, bool starts)
            => jobId != null && technicianClaim.HasValue && technicianClaim.Value.JobId == jobId &&
                technicianClaim.Value.OwnerId == ownerId && technicianClaim.Value.StartsRecipe == starts;

        private static bool SameTechnicianClaim(TechnicianClaimView a, TechnicianClaimView b)
            => a.JobId == b.JobId && a.OwnerId == b.OwnerId && a.SpeciesId == b.SpeciesId && a.SpecimenId == b.SpecimenId &&
                a.StorageId == b.StorageId && a.StartsRecipe == b.StartsRecipe;

        private int TechnicianReservedCulture(int culture)
        {
            if (!technicianClaim.HasValue || !technicianClaim.Value.StartsRecipe) return 0;
            var recipe = species[technicianClaim.Value.SpeciesId];
            return culture == 0 ? recipe.Bloom : culture == 1 ? recipe.Flow : culture == 2 ? recipe.Spark : recipe.Mist;
        }

        private bool ContainsSpecimenId(string id)
        {
            if (activeSpecimen == id) return true;
            foreach (var resident in tanks.Values) if (resident.HasValue && resident.Value.Id == id) return true;
            foreach (var resident in storage.Values) if (resident.HasValue && resident.Value.Id == id) return true;
            foreach (var batch in conditioners.Values)
                if (batch.First.HasValue && batch.First.Value.Id == id || batch.Second.HasValue && batch.Second.Value.Id == id) return true;
            return false;
        }
    }
}
