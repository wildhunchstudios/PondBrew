using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        string pendingFurnitureSaleId;
        bool showOwnedFurniture;
        public string PendingFurnitureSaleId => pendingFurnitureSaleId;

        public bool OpenOwnedFurnitureMenu()
        {
            if (!OpenPlacementMenu()) return false;
            showOwnedFurniture = true; showDecorCatalogue = false; panelScroll = Vector2.zero;
            return true;
        }

        bool DisplayHasReservation(string itemId)
        {
            foreach (string slot in Layout.GetSlotIds(itemId))
                if (Model.GetReservation(slot).HasValue) return true;
            return false;
        }

        public bool StoreFurniture(string itemId)
        {
            if (Panel != ShopPanel.Arrange || !CanArrange || pendingFurnitureSaleId != null) return false;
            if (!EmptyFurnitureActionAllowed(itemId)) return false;
            var preview = Layout.TryPreviewStore(itemId, out var candidate);
            if (!preview.Success) { Notify(PlacementFailureText(preview.Failure)); return false; }
            if (!PreflightFurnitureChange(candidate)) return false;
            var result = Layout.TryStore(Model, Cycle.Phase, itemId);
            if (!result.Success) { RestoreCurrentLayout(); Notify(PlacementFailureText(result.Failure)); return false; }
            Focused = null; selectedTank = "tank-a"; showOwnedFurniture = true;
            World.ApplyLayout(Layout); World.Refresh(Model); SaveNow();
            Notify("Furniture stored. Place it again for free from Owned furniture.");
            return true;
        }

        public bool RequestFurnitureSale(string itemId)
        {
            if (Panel != ShopPanel.Arrange || !CanArrange || PlacementActive || !EmptyFurnitureActionAllowed(itemId)) return false;
            pendingFurnitureSaleId = itemId;
            panelScroll = Vector2.zero;
            return true;
        }

        public void CancelFurnitureSale() { pendingFurnitureSaleId = null; panelScroll = Vector2.zero; }

        public bool ConfirmFurnitureSale()
        {
            string id = pendingFurnitureSaleId;
            if (id == null || Panel != ShopPanel.Arrange || !CanArrange || !EmptyFurnitureActionAllowed(id)) return false;
            var item = Layout.GetItem(id);
            var preview = Layout.TryPreviewSell(id, out var candidate);
            if (!preview.Success) { Notify(PlacementFailureText(preview.Failure)); return false; }
            if (!PreflightFurnitureChange(candidate)) return false;
            int refund = ShopLayout.DefinitionResalePrice(item.Value.DefinitionId);
            var result = Layout.TrySell(Model, Cycle.Phase, id);
            if (!result.Success) { RestoreCurrentLayout(); Notify(PlacementFailureText(result.Failure)); return false; }
            CancelFurnitureSale(); Focused = null; selectedTank = "tank-a"; showOwnedFurniture = true;
            World.ApplyLayout(Layout); World.Refresh(Model); SaveNow();
            Notify("Furniture sold / " + refund + " credits returned. Its ownership is removed.");
            return true;
        }

        bool EmptyFurnitureActionAllowed(string itemId)
        {
            if (!Layout.GetItem(itemId).HasValue) { Notify("That owned furniture is no longer available."); return false; }
            if (DisplayHasReservation(itemId)) { Notify("Finish the reserved customer order first."); return false; }
            if (DisplayHasStock(itemId))
            { Notify("Sell the creatures before storing or selling this display. You can move it with stock for free.", 6); return false; }
            return true;
        }

        bool PreflightFurnitureChange(ShopLayout candidate)
        {
            Vector3 player = World.transform.InverseTransformPoint(Player.transform.position);
            if (!candidate.CanReachMainAisle(player.x, player.z))
            { Notify("Keep a clear route from your position to the main aisle."); return false; }
            World.ApplyLayout(candidate);
            bool ready;
            try { ready = Customers.Navigation.Rebuild(World); }
            catch (Exception error) { ready = false; Debug.LogWarning("EVOLUTION_FURNITURE navigation preflight failed: " + error.Message); }
            if (ready) return true;
            RestoreCurrentLayout();
            Notify(layoutRoutesReady ? "Customer access could not be verified. Furniture and credits were kept." : "Customer routes could not recover. Save and reopen your shop.", 6);
            return false;
        }

        void DrawOwnedFurniture()
        {
            Space(); Heading("Owned furniture / " + Layout.StoredCount + " in storage");
            Small("Move stocked displays safely at night. Store empty furniture for free, or sell it for half its catalog price. Storage keeps ownership limits; it adds no active slots or appeal.");
            foreach (var item in Layout.Items)
            {
                string id = item.Id;
                bool stocked = DisplayHasStock(id), reserved = DisplayHasReservation(id);
                string place = item.Stored ? "IN STORAGE" : item.Z < 1 ? "store" : "lab";
                Text(ShopLayout.DefinitionName(item.DefinitionId) + " " + id.Replace("table-", "#") + " / " + place + (stocked ? " / contains stock" : ""));
                if (reserved) { Small("Reserved customer order: finish it before rearranging."); continue; }
                Button((item.Stored ? "Place from storage" : stocked ? "Move with stock safely" : "Move") + " / free", () => BeginPlacement(id));
                if (stocked) { Small("Store / sell blocked until every creature here has sold."); continue; }
                GUILayout.BeginHorizontal();
                if (!item.Stored) Button("Store / free", () => StoreFurniture(id));
                Button("Sell / +" + ShopLayout.DefinitionResalePrice(item.DefinitionId) + " credits...", () => RequestFurnitureSale(id));
                GUILayout.EndHorizontal();
            }
            Small("Original tanks, machines and room extensions stay fixed. Stored pieces can be placed anywhere valid; cancel keeps them in storage.");
        }

        void DrawFurnitureSaleConfirmation()
        {
            var item = Layout.GetItem(pendingFurnitureSaleId);
            if (!item.HasValue) { CancelFurnitureSale(); return; }
            Heading("Sell " + ShopLayout.DefinitionName(item.Value.DefinitionId) + "?");
            Text("Receive " + ShopLayout.DefinitionResalePrice(item.Value.DefinitionId) + " credits. This removes the owned piece; buying it again costs the full catalog price.");
            Small("Only empty furniture can be sold. Store it instead if you want to use it again for free. Stock, production and the original stations remain safe.");
            if (Layout.TryPreviewSell(item.Value.Id, out var candidate).Success)
                Text("Store appeal: " + Appeal.Points + " > " + candidate.AppealPoints + " / Active creature slots: " + Model.TankIds.Length + " > " + candidate.TankIds.Length);
            Button("Confirm sale / +" + ShopLayout.DefinitionResalePrice(item.Value.DefinitionId) + " credits", () => ConfirmFurnitureSale());
            Button("Keep furniture / back", CancelFurnitureSale);
        }
    }
}
