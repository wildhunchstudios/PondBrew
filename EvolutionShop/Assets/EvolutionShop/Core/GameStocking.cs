using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>A transient claim, never a second inventory copy. The specimen remains in its rack until commit.</summary>
    public readonly struct StockTransferView
    {
        public readonly string JobId, OwnerId, StorageId, TankId, SpecimenId;
        internal StockTransferView(string jobId, string ownerId, string storageId, string tankId, string specimenId)
        { JobId = jobId; OwnerId = ownerId; StorageId = storageId; TankId = tankId; SpecimenId = specimenId; }
    }

    public sealed partial class GameModel
    {
        // At most one claim per rack slot and destination. Existing registry limits bound this collection to six.
        // CaptureSave intentionally omits claims; loading recovers the exact specimen in its original rack.
        private readonly Dictionary<string, StockTransferView> stockTransfers = new Dictionary<string, StockTransferView>(StringComparer.Ordinal);

        public StockTransferView? GetStockTransfer(string jobId)
            => jobId != null && stockTransfers.TryGetValue(jobId, out var claim) ? claim : (StockTransferView?)null;

        public bool IsStorageReservedForStocking(string storageId)
        {
            foreach (var claim in stockTransfers.Values) if (claim.StorageId == storageId) return true;
            return false;
        }

        public bool IsTankReservedForStocking(string tankId)
        {
            foreach (var claim in stockTransfers.Values) if (claim.TankId == tankId) return true;
            return false;
        }

        public ActionResult ReserveStockTransfer(string jobId, string ownerId, string storageId, string tankId, string expectedSpecimenId)
        {
            if (!ValidStockIdentity(jobId) || !ValidStockIdentity(ownerId)) return ActionResult.Fail(FailureCode.ReservationMismatch);
            if (storageId == null || !storage.TryGetValue(storageId, out var source)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (tankId == null || !tanks.TryGetValue(tankId, out var target)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (stockTransfers.TryGetValue(jobId, out var existing))
            {
                if (existing.OwnerId != ownerId || existing.StorageId != storageId || existing.TankId != tankId || existing.SpecimenId != expectedSpecimenId)
                    return ActionResult.Fail(FailureCode.ReservationMismatch);
                // Repeated commands acknowledge this same claim; they cannot steal or change it.
                if (!source.HasValue || source.Value.Id != existing.SpecimenId) return ActionResult.Fail(FailureCode.SpecimenMismatch);
                return ActionResult.Ok(source.Value.SpeciesId, source.Value.Id);
            }
            if (IsStorageReservedForStocking(storageId) || IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (reservations.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.TankReserved);
            if (!source.HasValue) return ActionResult.Fail(FailureCode.StorageEmpty);
            if (source.Value.Id != expectedSpecimenId) return ActionResult.Fail(FailureCode.SpecimenMismatch);
            if (target.HasValue) return ActionResult.Fail(FailureCode.TankFull);
            stockTransfers.Add(jobId, new StockTransferView(jobId, ownerId, storageId, tankId, expectedSpecimenId));
            return ActionResult.Ok(source.Value.SpeciesId, source.Value.Id);
        }

        public ActionResult CompleteStockTransfer(string jobId, string ownerId)
        {
            if (jobId == null || !stockTransfers.TryGetValue(jobId, out var claim) || claim.OwnerId != ownerId)
                return ActionResult.Fail(FailureCode.ReservationMismatch);
            if (!storage.TryGetValue(claim.StorageId, out var source)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (!tanks.TryGetValue(claim.TankId, out var target)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (!source.HasValue || source.Value.Id != claim.SpecimenId) return ActionResult.Fail(FailureCode.SpecimenMismatch);
            if (reservations.ContainsKey(claim.TankId)) return ActionResult.Fail(FailureCode.TankReserved);
            if (target.HasValue) return ActionResult.Fail(FailureCode.TankFull);
            // All checks precede the sole ownership change. No money, production or sale state changes.
            tanks[claim.TankId] = source;
            storage[claim.StorageId] = null;
            stockTransfers.Remove(jobId);
            return ActionResult.Ok(source.Value.SpeciesId, source.Value.Id);
        }

        public bool CancelStockTransfer(string jobId, string ownerId)
        {
            if (jobId == null || !stockTransfers.TryGetValue(jobId, out var claim) || claim.OwnerId != ownerId) return false;
            return stockTransfers.Remove(jobId);
        }

        public void ClearStockTransfers() => stockTransfers.Clear();

        private static bool ValidStockIdentity(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
    }
}
