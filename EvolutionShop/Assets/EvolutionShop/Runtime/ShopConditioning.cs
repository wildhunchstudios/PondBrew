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
            var result = Perform(Model.BeginConditioning(selectedConditioner, sources.ToArray(), expected.ToArray()));
            if (result.Success) { conditioningInputs.Clear(); Notify("Glow bath started. One Spark is reserved until completion; cancel to recover it and your Pondlets."); }
            return result.Success;
        }

        public bool CancelSelectedConditioning()
        {
            if (!AtConditioner()) return false;
            var result = Perform(Model.CancelConditioning(selectedConditioner));
            if (result.Success) Notify("Batch canceled. Spark refunded; collect the unchanged Pondlets at a rack or display.");
            return result.Success;
        }

        public bool StoreConditionerInRack(string conditionerId, int slot, string storageId, string expectedId)
        {
            if (!AtRackSlot(storageId)) return false;
            var result = Perform(Model.TransferConditionerToStorage(conditionerId, slot, storageId, expectedId));
            if (result.Success) Notify("Vat output moved safely into this rack.");
            return result.Success;
        }

        public bool StockDisplayFromConditioner(string conditionerId, int slot, string expectedId)
        {
            if (!AtDisplay()) return false;
            var result = Model.TransferConditionerToTank(conditionerId, slot, selectedTank, expectedId);
            if (result.Success) Reports.RecordRestock();
            Perform(result);
            if (result.Success) Notify("Vat output stocked in this display. It is available to customers.");
            return result.Success;
        }

        bool DrawConditioningSources(string storageId)
        {
            bool any = false;
            foreach (string id in Model.ConditionerIds)
            {
                var state = Model.GetConditioner(id);
                if (!state.HasValue || state.Value.Phase != ConditionerPhase.Ready) continue;
                for (int i = 0; i < 2; i++)
                {
                    var fish = i == 0 ? state.Value.First : state.Value.Second;
                    if (!fish.HasValue) continue;
                    any = true; string machine = id, expected = fish.Value.Id; int slot = i;
                    string label = "Collect " + Model.GetSpecies(fish.Value.SpeciesId).DisplayName + " / vat chamber " + (i + 1);
                    if (storageId == null) Button(label, () => StockDisplayFromConditioner(machine, slot, expected));
                    else { string destination = storageId; Button(label, () => StoreConditionerInRack(machine, slot, destination, expected)); }
                }
            }
            return any;
        }

        void DrawConditioning()
        {
            Heading("Conditioning vat / Glow bath");
            var state = Model.GetConditioner(selectedConditioner);
            if (!state.HasValue) { Text("This vat is no longer placed. Use E at a placed vat."); return; }
            var vat = state.Value;
            Text("Known process: 1–2 Pondlets + 1 Spark per batch → Lanternfin");
            Small("12 active seconds. Two at once saves one Spark; incubation can run alongside it.");
            Space();
            if (vat.Phase == ConditionerPhase.Idle)
            {
                Text("Capacity: " + conditioningInputs.Count + " / 2 selected");
                if (!Model.SparkResearchUnlocked || !Model.IsDiscovered("lanternfin"))
                { Text("Research Spark and discover Lanternfin by incubation first."); return; }
                bool pondlet = false;
                foreach (string source in Model.StorageIds)
                {
                    var fish = Model.GetStorage(source);
                    if (!fish.HasValue || fish.Value.SpeciesId != "pondlet") continue;
                    pondlet = true; string id = source, expected = fish.Value.Id;
                    Button((conditioningInputs.ContainsKey(id) ? "Remove: " : "Select: ") + "Pondlet / " + StorageLocation(id), () => ToggleConditioningInput(id, expected));
                }
                if (!pondlet) Text("No stored Pondlets. Grow one Bloom + one Flow, then collect at a specimen rack.");
                if (conditioningInputs.Count == 0) Small("Choose one or two stored Pondlets before starting.");
                if (Model.Available(CultureKind.Spark) < 1) Small("Need 1 collected Spark. Hold left mouse at the research bench.");
                if (conditioningInputs.Count > 0 && Model.Available(CultureKind.Spark) >= 1)
                    Button("Start " + conditioningInputs.Count + "-creature batch / 1 Spark", () => BeginSelectedConditioning());
                Small("Selecting does not move stock. Start moves these exact fish into the vat and reserves one Spark.");
            }
            else if (vat.Phase == ConditionerPhase.Processing)
            {
                Text("Processing " + vat.Count + " / 2 creatures / " + Mathf.CeilToInt(vat.SecondsRemaining) + " seconds left");
                Text("1 Spark reserved in the siphon. Production continues while you work in the store.");
                Button("Cancel batch / recover Pondlets + 1 Spark", () => CancelSelectedConditioning());
                Small("Canceling keeps every fish in the vat until you collect it.");
            }
            else
            {
                Text("Output waiting: " + vat.Count + " / 2 creatures");
                if (vat.First.HasValue) Text("Chamber 1 / " + Model.GetSpecies(vat.First.Value.SpeciesId).DisplayName);
                if (vat.Second.HasValue) Text("Chamber 2 / " + Model.GetSpecies(vat.Second.Value.SpeciesId).DisplayName);
                bool free = false;
                foreach (string id in Model.StorageIds) if (!Model.GetStorage(id).HasValue) free = true;
                foreach (string id in Model.TankIds) if (!Model.GetTank(id).HasValue) free = true;
                Text(free ? "Walk to an empty rack or display, press E and choose a vat chamber." : "All racks and displays are full. Output waits safely; make room before collecting.");
                Small("Collect every creature before the next batch. Canceled batches return unchanged Pondlets.");
            }
            Small("The vat can move with stock at night. Empty it before storing or selling the furniture.");
        }
    }
}
