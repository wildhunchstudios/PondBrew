using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        string selectedConditioner;
        readonly Dictionary<string, string> conditioningInputs = new Dictionary<string, string>();
        public int ReadyConditionedCount
        {
            get
            {
                int count = 0;
                foreach (string id in Model.ConditionerIds)
                { var vat = Model.GetConditioner(id); if (vat.HasValue && vat.Value.Phase == ConditionerPhase.Ready) count += vat.Value.Count; }
                return count;
            }
        }

        bool AtConditioner()
        {
            if (Panel != ShopPanel.Conditioning || selectedConditioner == null) return false;
            foreach (var station in World.Stations)
                if (station.ConditionerItemId == selectedConditioner && AtStockStation(station)) return true;
            Notify("Use E at this conditioning vat in person to operate it."); return false;
        }

        public bool ToggleConditioningInput(string storageId, string expectedSpecimenId)
        {
            if (!AtConditioner()) return false;
            var vat = Model.GetConditioner(selectedConditioner);
            if (!vat.HasValue || vat.Value.Phase != ConditionerPhase.Idle) return false;
            if (conditioningInputs.Remove(storageId)) return true;
            var fish = Model.GetStorage(storageId);
            if (!fish.HasValue || fish.Value.Id != expectedSpecimenId || fish.Value.SpeciesId != "pondlet")
            { Notify("That Pondlet has moved. Choose its current rack compartment."); return false; }
            if (conditioningInputs.Count >= 2) { Notify("Capacity is two Pondlets. Remove a selection first."); return false; }
            conditioningInputs.Add(storageId, expectedSpecimenId); return true;
        }

        public bool BeginSelectedConditioning()
        {
            if (!AtConditioner()) return false;
            var sources = new List<string>(conditioningInputs.Keys);
            var expected = new List<string>(); foreach (string source in sources) expected.Add(conditioningInputs[source]);
            var result = PerformAdvancedLab(Model.BeginConditioning(selectedConditioner, sources.ToArray(), expected.ToArray()), FxAdvancedLabEvent.ConditioningStarted, selectedConditioner);
            if (result.Success) { conditioningInputs.Clear(); Notify("Glow bath started. One Spark is reserved until completion; cancel to recover it and your Pondlets."); }
            return result.Success;
        }

        public bool CancelSelectedConditioning()
        {
            if (!AtConditioner()) return false;
            var result = PerformAdvancedLab(Model.CancelConditioning(selectedConditioner), FxAdvancedLabEvent.ConditioningCancelled, selectedConditioner);
            if (result.Success) Notify("Batch canceled. Spark refunded; collect the unchanged Pondlets at a rack or display.");
            return result.Success;
        }

        public bool StoreConditionerInRack(string conditionerId, int slot, string storageId, string expectedId)
        {
            if (!AtRackSlot(storageId)) return false;
            var result = PerformWithFx(Model.TransferConditionerToStorage(conditionerId, slot, storageId, expectedId), FxCueId.None,
                destination: storageId, advancedEvent: FxAdvancedLabEvent.ConditioningOutput, advancedOwner: conditionerId, playerInteraction: FxPlayerInteraction.Rack);
            if (result.Success && SaveError == null) Notify("Vat output moved safely into this rack.");
            return result.Success;
        }

        public bool StockDisplayFromConditioner(string conditionerId, int slot, string expectedId)
        {
            if (!AtDisplay()) return false;
            var result = Model.TransferConditionerToTank(conditionerId, slot, selectedTank, expectedId);
            if (result.Success)
            {
                Reports.RecordRestock();
                Tutorial.ObserveDisplayed(Model, result, TutorialActionOrigin.Player);
            }
            PerformWithFx(result, FxCueId.None, destination: selectedTank, advancedEvent: FxAdvancedLabEvent.ConditioningOutput,
                advancedOwner: conditionerId, playerInteraction: FxPlayerInteraction.Place);
            if (result.Success && SaveError == null) Notify("Vat output stocked in this display. It is available to customers.");
            return result.Success;
        }

    }
}
