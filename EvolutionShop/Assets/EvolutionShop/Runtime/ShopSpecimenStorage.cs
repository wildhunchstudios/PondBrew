using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        string selectedRack;
        public int StoredSpecimenCount
        {
            get { int count = 0; foreach (string id in Model.StorageIds) if (Model.GetStorage(id).HasValue) count++; return count; }
        }

        bool AtStockStation(Station station)
        {
            if (!station || Paused || AutosaveBlocked || PlacementActive || !Player) return false;
            var ray = new Ray(Player.View.transform.position, Player.View.transform.forward);
            return Physics.SphereCast(ray, .14f, out var hit, 3.3f, ~0, QueryTriggerInteraction.Collide) &&
                hit.collider.GetComponentInParent<Station>() == station;
        }

        bool AtDisplay()
        {
            if (Panel != ShopPanel.Tank) return false;
            foreach (var station in World.Stations)
                if (station.TankId == selectedTank && AtStockStation(station)) return true;
            Notify("Use E at this display in person to move its stock."); return false;
        }

        bool AtRackSlot(string storageId)
        {
            if (Panel != ShopPanel.StorageRack || selectedRack == null) return false;
            bool belongs = false;
            foreach (string id in Layout.GetStorageSlotIds(selectedRack)) if (id == storageId) belongs = true;
            if (belongs)
                foreach (var station in World.Stations)
                    if (station.StorageItemId == selectedRack && AtStockStation(station)) return true;
            Notify("Use E at this lab rack in person to store ready output."); return false;
        }

        public bool StoreReadyInRack(string storageId)
        {
            if (!AtRackSlot(storageId)) return false;
            var result = Perform(Model.StoreReadyOutput(storageId));
            if (result.Success) Notify("Creature stored. The incubator is free for the next recipe.");
            return result.Success;
        }

        public bool StockDisplayFromStorage(string storageId, string expectedSpecimenId)
        {
            if (!AtDisplay()) return false;
            var result = Model.TransferStorageToTank(storageId, selectedTank, expectedSpecimenId);
            if (result.Success) Reports.RecordRestock();
            Perform(result);
            if (result.Success) Notify("Stored creature moved into this display. It is available to customers.");
            return result.Success;
        }

        public bool ReturnDisplayToStorage(string storageId, string expectedSpecimenId)
        {
            if (!AtDisplay()) return false;
            var result = Perform(Model.ReturnTankToStorage(selectedTank, storageId, expectedSpecimenId));
            if (result.Success) Notify("Creature returned to the lab rack. It is no longer for sale.");
            return result.Success;
        }

        void StockDisplayFromIncubator()
        { if (AtDisplay()) TransferToDisplay(selectedTank); }

        string StorageLocation(string slot)
        {
            foreach (var item in Layout.Items)
            {
                var ids = Layout.GetStorageSlotIds(item.Id);
                for (int i = 0; i < ids.Length; i++)
                    if (ids[i] == slot) return "Rack " + item.Id.Replace("table-", "#") + " / " + (i == 0 ? "lower" : "upper");
            }
            return "Lab storage";
        }

        void DrawSpecimenRack()
        {
            Heading("Specimen rack " + (selectedRack ?? "").Replace("table-", "#"));
            var item = Layout.GetItem(selectedRack);
            if (!item.HasValue || item.Value.Stored || item.Value.DefinitionId != ShopLayout.RackDefinitionId)
            { Text("This rack is no longer placed. Close this panel and use an active rack."); return; }
            Text("Store ready output to free the incubator. Customers cannot buy creatures kept here.");
            foreach (string id in Layout.GetStorageSlotIds(selectedRack))
            {
                var resident = Model.GetStorage(id); string slot = id;
                Space();
                if (resident.HasValue)
                    Text(StorageLocation(id) + " / " + Model.GetSpecies(resident.Value.SpeciesId).DisplayName);
                else
                {
                    Text(StorageLocation(id) + " / empty");
                    if (Model.Incubator.Phase == IncubatorPhase.Ready)
                        Button("Store ready " + Model.GetSpecies(Model.Incubator.SpeciesId).DisplayName + " here", () => StoreReadyInRack(slot));
                    DrawConditioningSources(slot);
                }
            }
            Small("To stock a creature, walk to an empty store display and press E, then choose its rack compartment. At an unreserved stocked display you can return it to any empty rack slot.");
            Small("Racks can move with their creatures at night. Empty them before storing or selling the furniture.");
        }

        void DrawDisplayStockSources()
        {
            bool any = false;
            if (Model.Incubator.Phase == IncubatorPhase.Ready)
            {
                any = true;
                Button("Stock ready " + Model.GetSpecies(Model.Incubator.SpeciesId).DisplayName + " / incubator", StockDisplayFromIncubator);
            }
            foreach (string id in Model.StorageIds)
            {
                var resident = Model.GetStorage(id); if (!resident.HasValue) continue;
                any = true; string slot = id, expected = resident.Value.Id;
                Button("Stock " + Model.GetSpecies(resident.Value.SpeciesId).DisplayName + " / " + StorageLocation(id), () => StockDisplayFromStorage(slot, expected));
            }
            any |= DrawConditioningSources(null);
            if (!any) Text("No ready or stored creatures yet. Grow a recipe in the lab first.");
            Small("One specimen per display. Only displayed creatures attract customers; lab stock stays safe until you choose it.");
        }

        void DrawDisplayStorageReturn(SpecimenView resident, bool reserved)
        {
            if (Model.StorageIds.Length == 0) return;
            Space();
            if (reserved) { Small("Finish or cancel the accepted order before returning this creature to lab storage."); return; }
            bool space = false;
            foreach (string id in Model.StorageIds)
            {
                if (Model.GetStorage(id).HasValue) continue;
                space = true; string slot = id, expected = resident.Id;
                Button("Return to " + StorageLocation(id), () => ReturnDisplayToStorage(slot, expected));
            }
            if (!space) Small("Lab racks are full. This creature stays safely in its display.");
        }
    }
}
