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
            var result = PerformWithFx(Model.StoreReadyOutput(storageId), FxCueId.None, destination: storageId, playerInteraction: FxPlayerInteraction.Rack);
            if (result.Success && SaveError == null) Notify("Creature stored. The incubator is free for the next recipe.");
            return result.Success;
        }

        public bool StockDisplayFromStorage(string storageId, string expectedSpecimenId)
        {
            if (!AtDisplay()) return false;
            var result = Model.TransferStorageToTank(storageId, selectedTank, expectedSpecimenId);
            if (result.Success)
            { Reports.RecordRestock(); Tutorial.ObserveDisplayed(Model, result, TutorialActionOrigin.Player); }
            PerformWithFx(result, FxCueId.None, destination: selectedTank, playerInteraction: FxPlayerInteraction.Place);
            if (result.Success && SaveError == null) Notify("Stored creature moved into this display. It is available to customers.");
            return result.Success;
        }

        public bool ReturnDisplayToStorage(string storageId, string expectedSpecimenId)
        {
            if (!AtDisplay()) return false;
            var result = PerformWithFx(Model.ReturnTankToStorage(selectedTank, storageId, expectedSpecimenId), FxCueId.None,
                destination: storageId, playerInteraction: FxPlayerInteraction.Transfer);
            if (result.Success && SaveError == null) Notify("Creature returned to the lab rack. It is no longer for sale.");
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

    }
}
