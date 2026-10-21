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
        string movingFacilityId;
        string placementDefinition = ShopLayout.TableDefinitionId;
        bool placementArmed, placementHasFloor;
        int previewX = int.MinValue, previewZ, previewRotation, previewPlayerX, previewPlayerZ, previewCash;
        readonly RaycastHit[] placementHits = new RaycastHit[32];
        bool layoutRoutesReady = true;
        public int PlacementCost => movingFacilityId != null ? 0 : movingTableId == null ? ShopLayout.DefinitionPrice(placementDefinition) : 0;

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
            if (ShopLayout.IsFacility(ownedId)) return BeginFacilityPlacement(ownedId);
            movingFacilityId = null;
            var owned = ownedId == null ? null : Layout.GetItem(ownedId);
            if (ownedId != null && !owned.HasValue) return false;
            if (owned.HasValue && DisplayHasReservation(ownedId))
            { Notify("Finish the reserved order before moving this display."); return false; }
            placementDefinition = owned.HasValue ? owned.Value.DefinitionId : definitionId;
            if (ShopLayout.DefinitionName(placementDefinition) == null) return false;
            movingTableId = ownedId;
            PlacementRotation = owned.HasValue ? owned.Value.QuarterTurns : 0;
            StartPlacementPreview();
            return true;
        }

        void StartPlacementPreview()
        {
            SetPanel(ShopPanel.None);
            PlacementActive = true; placementArmed = false; previewX = int.MinValue;
            message = null; messageUntil = 0; // A new preview replaces stale action/recovery notices; SaveError remains visible.
            SlapTarget = null; Focused = null; StopHarvest();
            World.SetHarvesting(false, null);
            UpdatePlacementAim();
        }

        void EndPlacementPreview()
        {
            PlacementActive = false; PlacementValid = false; placementArmed = false;
            movingTableId = null; movingFacilityId = null;
            if (World) World.HidePreview();
        }

        public void CancelPlacement()
        {
            if (!PlacementActive) return;
            EndPlacementPreview(); SetPanel(ShopPanel.Arrange);
            Notify("Placement cancelled. Your room layout and credits are unchanged.");
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
                    var facility = obstruction.GetComponentInParent<Station>();
                    if (movingFacilityId != null && facility && facility.FacilityId == movingFacilityId) continue;
                    placementHasFloor = false;
                }
            }
            if (!placementHasFloor)
            {
                PlacementValid = false; PlacementMessage = "Aim at clear floor within five metres.";
                previewX = int.MinValue; World.HidePreview(); return;
            }
            Vector3 local = World.transform.InverseTransformPoint(ray.GetPoint(distance));
            FacilityAnchor(out float anchorX, out float anchorZ);
            PlacementX = Mathf.RoundToInt((local.x - anchorX) / ShopLayout.GridSize);
            PlacementZ = Mathf.RoundToInt((local.z - anchorZ) / ShopLayout.GridSize);
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
            if (movingFacilityId != null) World.ShowLabFacilityPreview(movingFacilityId, PlacementX * ShopLayout.GridSize + anchorX, PlacementZ * ShopLayout.GridSize + anchorZ, PlacementRotation, PlacementValid);
            else World.ShowPlacementPreview(placementDefinition, PlacementX * ShopLayout.GridSize, PlacementZ * ShopLayout.GridSize, PlacementRotation, PlacementValid);
        }

        bool ValidatePlacementCandidate(out ShopLayout candidate, out string reason)
        {
            candidate = null;
            if (!PlacementActive || !placementHasFloor || !CanArrange)
            { reason = "Arrange rooms at night and aim at a clear floor."; return false; }
            var result = movingFacilityId != null ?
                Layout.TryPreviewFacility(movingFacilityId, PlacementX, PlacementZ, PlacementRotation, out candidate) :
                Layout.TryPreview(placementDefinition, movingTableId, PlacementX, PlacementZ, PlacementRotation, out candidate);
            if (!result.Success) { reason = PlacementFailureText(result.Failure); return false; }
            if (Model.Currency < PlacementCost) { reason = "Need " + PlacementCost + " credits. Previewing costs nothing."; return false; }
            var player = World.transform.InverseTransformPoint(Player.transform.position);
            if (movingTableId != null && DisplayHasReservation(movingTableId))
            { reason = "Finish the reserved order before moving this display."; return false; }
            float width = movingFacilityId != null ? ShopLayout.FacilityWidth(movingFacilityId) : ShopLayout.DefinitionWidth(placementDefinition);
            float depth = movingFacilityId != null ? ShopLayout.FacilityDepth(movingFacilityId) : ShopLayout.DefinitionDepth(placementDefinition);
            FacilityAnchor(out float anchorX, out float anchorZ);
            float halfX = (PlacementRotation % 2 == 0 ? width : depth) / 2;
            float halfZ = (PlacementRotation % 2 == 0 ? depth : width) / 2;
            if (Mathf.Abs(player.x - (PlacementX * ShopLayout.GridSize + anchorX)) < halfX + .4f &&
                Mathf.Abs(player.z - (PlacementZ * ShopLayout.GridSize + anchorZ)) < halfZ + .4f)
            { reason = "Step outside the furniture footprint before placing it."; return false; }
            if (Stocker && Stocker.Actor && Stocker.Actor.isActiveAndEnabled)
            {
                Vector3 worker=World.transform.InverseTransformPoint(Stocker.Actor.transform.position);
                if (!candidate.CanReachMainAisle(worker.x,worker.z))
                { reason="Keep Nori's standing space connected to the aisle.";return false; }
            }
            if (Technician && Technician.Actor && Technician.Actor.isActiveAndEnabled)
            {
                Vector3 worker=World.transform.InverseTransformPoint(Technician.Actor.transform.position);
                if (!candidate.CanReachMainAisle(worker.x,worker.z))
                { reason="Keep Moss's standing space connected to the aisle.";return false; }
            }
            if (!candidate.CanReachMainAisle(player.x, player.z))
            { reason = "Keep a clear route from your position to the main aisle."; return false; }
            reason = movingFacilityId != null ? "Clear / move equipment for free / contents and progress stay safe" : PlacementCost == 0 ? "Clear / place owned furniture for free" : "Clear / buy for " + PlacementCost + " credits";
            if (ShopLayout.IsDecor(placementDefinition))
                reason += " / appeal " + Appeal.Points + " > " + candidate.AppealPoints + " of 30" + (candidate.AppealPoints == Appeal.Points ? " (lab or duplicate adds zero)" : "");
            if (movingTableId != null && DisplayHasStock(movingTableId)) reason += " / stock moves safely with it";
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
            var result = movingFacilityId != null ?
                Layout.TryMoveFacility(Model, Cycle.Phase, movingFacilityId, PlacementX, PlacementZ, PlacementRotation) :
                Layout.TryCommit(Model, Cycle.Phase, placementDefinition, movingTableId, PlacementX, PlacementZ, PlacementRotation, out _);
            if (!result.Success)
            {
                RestoreCurrentLayout(); PlacementMessage = PlacementFailureText(result.Failure);
                PlacementValid = false; Notify(PlacementMessage); return false;
            }
            bool purchased = movingTableId == null && movingFacilityId == null;
            int paid = PlacementCost; string name = movingFacilityId != null ? ShopLayout.FacilityName(movingFacilityId) : ShopLayout.DefinitionName(placementDefinition);
            World.ApplyLayout(Layout); World.Refresh(Model);
            EndPlacementPreview(); SetPanel(ShopPanel.Arrange);
            SaveNow();
            Notify(purchased ? name + " placed / " + paid + " credits paid." : name + " moved. No charge.");
            return true;
        }

        void RestoreCurrentLayout()
        {
            World.ApplyLayout(Layout);
            World.Refresh(Model);
            Focused = null;
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
            if (a.Count != b.Count || left.StoreTier != right.StoreTier || left.LabTier != right.LabTier) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Id != b[i].Id || a[i].DefinitionId != b[i].DefinitionId || a[i].X != b[i].X || a[i].Z != b[i].Z || a[i].QuarterTurns != b[i].QuarterTurns || a[i].Stored != b[i].Stored) return false;
            foreach (var facility in left.Facilities)
            {
                var other = right.GetFacility(facility.Id);
                if (!other.HasValue || facility.X != other.Value.X || facility.Z != other.Value.Z || facility.QuarterTurns != other.Value.QuarterTurns) return false;
            }
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
                case LayoutFailure.TechnicianReserved: return "Moss reserved this station or rack. Pause lab work before moving it.";
                case LayoutFailure.StockReserved: return "Nori has reserved this rack or display. Pause stocking before moving it.";
                case LayoutFailure.ConditionerOccupied: return "Collect every creature before storing or selling this vat. Moving it with stock is free.";
                case LayoutFailure.StockedStorage: return "Empty this specimen rack into displays before storing or selling it. Moving it with stock is free.";
                case LayoutFailure.StockedDisplay: return "Empty this furniture before storing or selling it. Moving it with stock is free.";
                case LayoutFailure.ReservedDisplay: return "Finish the reserved customer order first.";
                case LayoutFailure.AlreadyStored: return "That furniture is already in storage.";
                case LayoutFailure.CurrencyOverflow: return "There is no room for the full refund. Your room layout and credits are unchanged.";
                case LayoutFailure.LockedDefinition: return "Large displays need a store expansion; the conditioning vat needs Spark research and a discovered Lanternfin.";
                case LayoutFailure.WrongPhase: return "Arrange rooms during preparation night.";
                case LayoutFailure.OutOfBounds: return "Keep the whole item inside its room. Displays belong in the store.";
                case LayoutFailure.OverlapsFurniture: return "That spot overlaps furniture or a machine.";
                case LayoutFailure.ProtectedAccess: return "Keep the doorway, main aisle and service spaces clear.";
                case LayoutFailure.UnreachableAccess: return "This would block access to a station.";
                case LayoutFailure.LimitReached: return "This furniture limit is reached. Tables: 3; decor: 6; vat: 1; displays and racks depend on room tiers.";
                case LayoutFailure.InsufficientFunds: return "Not enough credits. Nothing was purchased.";
                case LayoutFailure.UnknownItem: return "That furniture is no longer available.";
                default: return "That placement is unavailable. Nothing was purchased.";
            }
        }

        bool DisplayHasStock(string itemId)
        {
            var machine = Model.GetConditioner(itemId);
            if (machine.HasValue && (machine.Value.Count > 0 || machine.Value.Phase != ConditionerPhase.Idle)) return true;
            foreach (string slot in Layout.GetSlotIds(itemId))
                if (Model.GetTank(slot).HasValue || Model.GetReservation(slot).HasValue) return true;
            foreach (string slot in Layout.GetStorageSlotIds(itemId))
                if (Model.GetStorage(slot).HasValue) return true;
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

    }
}
