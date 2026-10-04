using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopLayout Layout { get; private set; }
        public bool PlacementActive { get; private set; }
        public bool PlacementValid { get; private set; }
        public string PlacementMessage { get; private set; } = "Aim at the floor within five metres.";
        public int PlacementX { get; private set; }
        public int PlacementZ { get; private set; }
        public int PlacementRotation { get; private set; }
        string movingTableId;
        string placementDefinition = ShopLayout.TableDefinitionId;
        bool placementArmed, placementHasFloor;
        int previewX = int.MinValue, previewZ, previewRotation, previewPlayerX, previewPlayerZ, previewCash;
        readonly RaycastHit[] placementHits = new RaycastHit[32];
        bool layoutRoutesReady = true;
        public int PlacementCost => movingTableId == null ? ShopLayout.DefinitionPrice(placementDefinition) : 0;

        bool CanArrange => !Paused && !AutosaveBlocked && Cycle.Phase == ShopDayPhase.Night &&
            Customers != null && Customers.Ready && layoutRoutesReady && Customers.ActiveCount == 0;

        public bool OpenPlacementMenu()
        {
            if (AutosaveBlocked) { Notify(SaveError ?? "Restore a valid save before arranging rooms."); return false; }
            if (!layoutRoutesReady) { Notify("Customer routes could not recover. Save and reopen your shop before arranging or trading."); return false; }
            if (!CanArrange) { Notify("Arrange rooms during preparation night, after all customers have left."); return false; }
            SetPanel(ShopPanel.Arrange);
            return true;
        }

        public bool BeginPlacement(string ownedId = null, string definitionId = ShopLayout.TableDefinitionId)
        {
            if (Panel != ShopPanel.Arrange || !CanArrange) return false;
            var owned = ownedId == null ? null : Layout.GetItem(ownedId);
            if (ownedId != null && !owned.HasValue) return false;
            if (owned.HasValue && DisplayHasStock(ownedId))
            { Notify("Sell the stock before moving this display. Its creatures stay safe here."); return false; }
            placementDefinition = owned.HasValue ? owned.Value.DefinitionId : definitionId;
            if (ShopLayout.DefinitionName(placementDefinition) == null) return false;
            movingTableId = ownedId;
            PlacementRotation = owned.HasValue ? owned.Value.QuarterTurns : 0;
            SetPanel(ShopPanel.None);
            PlacementActive = true; placementArmed = false; previewX = int.MinValue;
            message = null; messageUntil = 0; // A new preview replaces stale action/recovery notices; SaveError remains visible.
            SlapTarget = null; Focused = null; StopHarvest();
            World.SetHarvesting(false, null);
            UpdatePlacementAim();
            return true;
        }

        void EndPlacementPreview()
        {
            PlacementActive = false; PlacementValid = false; placementArmed = false;
            movingTableId = null;
            if (World) World.HidePreview();
        }

        public void CancelPlacement()
        {
            if (!PlacementActive) return;
            EndPlacementPreview(); SetPanel(ShopPanel.Arrange);
            Notify("Placement cancelled. Your furniture and credits are unchanged.");
        }

        public void RotatePlacement()
        {
            if (!PlacementActive || !CanArrange) return;
            PlacementRotation = (PlacementRotation + 1) % 4;
            previewX = int.MinValue;
            UpdatePlacementAim();
        }

        void HandlePlacementInput(Keyboard keyboard)
        {
            if (!PlacementActive) return;
            if (!CanArrange) { EndPlacementPreview(); return; }
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            { CancelPlacement(); return; }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) RotatePlacement();
            UpdatePlacementAim();
            var mouse = Mouse.current;
            if (mouse == null || (!AllowValidationInput && ValidationMode)) return;
            if (!mouse.leftButton.isPressed) placementArmed = true;
            // Opening a preview while the menu click is still held cannot buy anything.
            if (placementArmed && mouse.leftButton.wasPressedThisFrame) CommitPlacement();
        }

        void UpdatePlacementAim()
        {
            var ray = new Ray(Player.View.transform.position, Player.View.transform.forward);
            var plane = new Plane(World.transform.up, World.transform.TransformPoint(Vector3.zero));
            placementHasFloor = plane.Raycast(ray, out float distance) && distance > 0 && distance <= 5f;
            if (placementHasFloor)
            {
                // A floor projected through a wall or a machine is not a usable placement target.
                int hits = Physics.RaycastNonAlloc(ray, placementHits, Mathf.Max(0, distance - .06f), ~0, QueryTriggerInteraction.Ignore);
                if (hits == placementHits.Length) placementHasFloor = false;
                for (int i = 0; i < hits && placementHasFloor; i++)
                {
                    var obstruction = placementHits[i].transform;
                    if (obstruction.IsChildOf(Player.transform)) continue;
                    var owned = obstruction.GetComponentInParent<PlacedWorkTable>();
                    if (owned && owned.Id == movingTableId) continue;
                    placementHasFloor = false;
                }
            }
            if (!placementHasFloor)
            {
                PlacementValid = false; PlacementMessage = "Aim at clear floor within five metres.";
                previewX = int.MinValue; World.HidePreview(); return;
            }
            Vector3 local = World.transform.InverseTransformPoint(ray.GetPoint(distance));
            PlacementX = Mathf.RoundToInt(local.x / ShopLayout.GridSize);
            PlacementZ = Mathf.RoundToInt(local.z / ShopLayout.GridSize);
            Vector3 player = World.transform.InverseTransformPoint(Player.transform.position);
            int px = Mathf.RoundToInt(player.x * 4), pz = Mathf.RoundToInt(player.z * 4);
            if (previewX != PlacementX || previewZ != PlacementZ || previewRotation != PlacementRotation ||
                previewPlayerX != px || previewPlayerZ != pz || previewCash != Model.Currency)
            {
                previewX = PlacementX; previewZ = PlacementZ; previewRotation = PlacementRotation;
                previewPlayerX = px; previewPlayerZ = pz; previewCash = Model.Currency;
                PlacementValid = ValidatePlacementCandidate(out _, out var reason);
                PlacementMessage = reason;
            }
            World.ShowPlacementPreview(placementDefinition, PlacementX * ShopLayout.GridSize, PlacementZ * ShopLayout.GridSize, PlacementRotation, PlacementValid);
        }

        bool ValidatePlacementCandidate(out ShopLayout candidate, out string reason)
        {
            candidate = null;
            if (!PlacementActive || !placementHasFloor || !CanArrange)
            { reason = "Arrange rooms at night and aim at a clear floor."; return false; }
            var result = Layout.TryPreview(placementDefinition, movingTableId, PlacementX, PlacementZ, PlacementRotation, out candidate);
            if (!result.Success) { reason = PlacementFailureText(result.Failure); return false; }
            if (Model.Currency < PlacementCost) { reason = "Need " + PlacementCost + " credits. Previewing costs nothing."; return false; }
            var player = World.transform.InverseTransformPoint(Player.transform.position);
            if (movingTableId != null && DisplayHasStock(movingTableId))
            { reason = "Sell the stock before moving this display."; return false; }
            float width = ShopLayout.DefinitionWidth(placementDefinition), depth = ShopLayout.DefinitionDepth(placementDefinition);
            float halfX = (PlacementRotation % 2 == 0 ? width : depth) / 2;
            float halfZ = (PlacementRotation % 2 == 0 ? depth : width) / 2;
            if (Mathf.Abs(player.x - PlacementX * ShopLayout.GridSize) < halfX + .4f &&
                Mathf.Abs(player.z - PlacementZ * ShopLayout.GridSize) < halfZ + .4f)
            { reason = "Step outside the furniture footprint before placing it."; return false; }
            if (!candidate.CanReachMainAisle(player.x, player.z))
            { reason = "Keep a clear route from your position to the main aisle."; return false; }
            reason = PlacementCost == 0 ? "Clear / move owned furniture for free" : "Clear / buy for " + PlacementCost + " credits";
            return true;
        }

        public bool CommitPlacement()
        {
            // Recheck authority, funds, footprint and the player's current position at commit time.
            if (!ValidatePlacementCandidate(out var candidate, out var reason))
            { PlacementValid = false; PlacementMessage = reason; Notify(reason); return false; }
            World.ApplyLayout(candidate);
            bool routesReady;
            try { routesReady = Customers.Navigation.Rebuild(World); }
            catch (Exception error)
            {
                routesReady = false;
                Debug.LogWarning("EVOLUTION_PLACEMENT navigation preflight failed: " + error.Message);
            }
            if (!routesReady)
            {
                RestoreCurrentLayout();
                PlacementValid = false; PlacementMessage = layoutRoutesReady ? "Customer access is blocked. Choose another spot." :
                    "Customer routes could not recover. Save and reopen your shop.";
                Notify(PlacementMessage); return false;
            }
            var result = Layout.TryCommit(Model, Cycle.Phase, placementDefinition, movingTableId, PlacementX, PlacementZ, PlacementRotation, out var id);
            if (!result.Success)
            {
                RestoreCurrentLayout(); PlacementMessage = PlacementFailureText(result.Failure);
                PlacementValid = false; Notify(PlacementMessage); return false;
            }
            bool purchased = movingTableId == null;
            int paid = PlacementCost; string name = ShopLayout.DefinitionName(placementDefinition);
            World.ApplyLayout(Layout);
            EndPlacementPreview(); SetPanel(ShopPanel.Arrange);
            SaveNow();
            Notify(purchased ? name + " placed / " + paid + " credits paid." : name + " moved. No charge.");
            return true;
        }

        void RestoreCurrentLayout()
        {
            World.ApplyLayout(Layout);
            layoutRoutesReady = Customers.Navigation.Rebuild(World);
            if (!layoutRoutesReady)
                Notify("Room navigation could not recover. Save and reopen before trading.", 10);
        }

        bool TryApplyLayoutGeometry(ShopLayout restored)
        {
            EndPlacementPreview();
            // Ordinary reloads reuse the existing surface when geometry is unchanged.
            if (layoutRoutesReady && SameLayout(Layout, restored)) return true;
            World.ApplyLayout(restored);
            if (!Customers.Navigation.Rebuild(World))
            { RestoreCurrentLayout(); Notify("Saved layout could not restore customer routes. Current stock and cash were kept.", 8); return false; }
            layoutRoutesReady = true;
            return true;
        }

        static bool SameLayout(ShopLayout left, ShopLayout right)
        {
            var a = left.Items; var b = right.Items;
            if (a.Count != b.Count || left.StoreTier != right.StoreTier) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Id != b[i].Id || a[i].DefinitionId != b[i].DefinitionId || a[i].X != b[i].X || a[i].Z != b[i].Z || a[i].QuarterTurns != b[i].QuarterTurns) return false;
            return true;
        }

        void AdoptLayout(ShopLayout restored)
        {
            Layout = restored;
            Vector3 local = World.transform.InverseTransformPoint(Player.transform.position);
            if (!Layout.CanReachMainAisle(local.x, local.z))
            {
                Vector3 spawn = World.SpawnPosition; spawn.y = .2f;
                Player.Teleport(spawn, Player.transform.rotation);
            }
        }

        static string PlacementFailureText(LayoutFailure failure)
        {
            switch (failure)
            {
                case LayoutFailure.StockedDisplay: return "Sell all stock before moving this display.";
                case LayoutFailure.LockedDefinition: return "Large displays unlock with the first store expansion.";
                case LayoutFailure.WrongPhase: return "Arrange rooms during preparation night.";
                case LayoutFailure.OutOfBounds: return "Keep the whole item inside its room. Displays belong in the store.";
                case LayoutFailure.OverlapsFurniture: return "That spot overlaps furniture or a machine.";
                case LayoutFailure.ProtectedAccess: return "Keep the doorway, main aisle and service spaces clear.";
                case LayoutFailure.UnreachableAccess: return "This would block access to a station.";
                case LayoutFailure.LimitReached: return "This furniture limit is reached, or this display needs a store expansion.";
                case LayoutFailure.InsufficientFunds: return "Not enough credits. Nothing was purchased.";
                case LayoutFailure.UnknownItem: return "That furniture is no longer available.";
                default: return "That placement is unavailable. Nothing was purchased.";
            }
        }

        bool DisplayHasStock(string itemId)
        {
            foreach (string slot in Layout.GetSlotIds(itemId))
                if (Model.GetTank(slot).HasValue || Model.GetReservation(slot).HasValue) return true;
            return false;
        }

        public bool BuyStoreExpansion()
        {
            if (Panel != ShopPanel.Arrange || !CanArrange) return false;
            if (!Layout.TryPreviewExpansion(out var candidate).Success)
            { Notify(Layout.StoreTier == 2 ? "Your store is fully expanded." : "Expansion access is blocked. Move empty furniture away from the west passage, then retry."); return false; }
            int price = Layout.NextExpansionPrice;
            if (Model.Currency < price) { Notify("Need " + price + " credits to expand the store."); return false; }
            if (!TryApplyLayoutGeometry(candidate))
            { Notify("Expansion access is blocked. Move empty furniture away from the west passage, then retry.", 8); return false; }
            var result = Layout.TryExpand(Model, Cycle.Phase);
            if (!result.Success) { RestoreCurrentLayout(); Notify(PlacementFailureText(result.Failure)); return false; }
            World.ApplyLayout(Layout); SaveNow();
            Notify("Store expanded / " + price + " credits paid. New floor space and display options are ready.");
            return true;
        }

        void DrawArrangement()
        {
            Heading("Arrange your rooms / store tier " + (Layout.StoreTier + 1) + " of 3");
            if (Cycle.Phase != ShopDayPhase.Night) { Text("Come back during preparation night."); return; }
            int displayCount = 0, tableCount = 0;
            foreach (var item in Layout.Items) if (ShopLayout.DefinitionSlotCount(item.DefinitionId) > 0) displayCount++; else tableCount++;
            Text("Display furniture " + displayCount + " / " + Layout.DisplayLimit + "  |  Total creature slots " + Model.TankIds.Length);
            if (Layout.StoreTier < 2)
            {
                Small("Add a 4 m west wing. Keep the front entrance and lab unchanged. Display limit becomes " + (Layout.DisplayLimit + 1) + (Layout.StoreTier == 0 ? "; unlock large displays." : "."));
                Button("Expand store / " + Layout.NextExpansionPrice + " credits", () => BuyStoreExpansion());
                Small("Expansion buys floor space; display furniture is purchased separately. No customer or price bonus yet.");
            }
            else Small("Store fully expanded. Three purchased displays plus your two original tanks.");
            Space(); Heading("Buy furniture");
            if (displayCount < Layout.DisplayLimit)
            {
                Button("Small display / 1 creature / 24 credits", () => BeginPlacement(null, ShopLayout.SmallDisplayId));
                if (Layout.StoreTier > 0) Button("Large display / 2 creatures / 40 credits", () => BeginPlacement(null, ShopLayout.LargeDisplayId));
                else Small("Large displays unlock with your first expansion.");
            }
            if (tableCount < ShopLayout.MaxItems)
                Button("Work table / no creature slots / 12 credits", () => BeginPlacement());
            Small("Aim at the floor. R rotates; left click places; right click or Escape cancels. Green outline marks a valid footprint. Display fronts need customer access.");
            Space(); Heading("Owned furniture / move for free");
            foreach (var item in Layout.Items)
            {
                string id = item.Id;
                bool stocked = DisplayHasStock(id);
                if (stocked) Small(ShopLayout.DefinitionName(item.DefinitionId) + " " + id.Replace("table-", "#") + " / stocked: sell contents before moving");
                else Button("Move " + ShopLayout.DefinitionName(item.DefinitionId) + " " + id.Replace("table-", "#") + " / " + (item.Z < 1 ? "store" : "lab"), () => BeginPlacement(id));
            }
            Small("Original tanks stay fixed. Previewing a move keeps furniture at its old position until confirmed. Nothing is removed or refunded here.");
        }

        void DrawPlacementOverlay()
        {
            Box(new Rect(438, 505, 592, 140), ink);
            Box(new Rect(637, 357, 6, 6), Color.white);
            GUI.Label(new Rect(454, 516, 560, 26), movingTableId == null ? "PLACE " + ShopLayout.DefinitionName(placementDefinition).ToUpperInvariant() + " / " + PlacementCost + " CREDITS" : "MOVE " + ShopLayout.DefinitionName(placementDefinition).ToUpperInvariant() + " / FREE", headingStyle);
            GUI.Label(new Rect(454, 548, 560, 46), PlacementMessage, bodyStyle);
            GUI.Label(new Rect(454, 603, 560, 27), "R rotate  |  LEFT CLICK place  |  RIGHT CLICK / ESC cancel", smallStyle);
        }
    }
}
