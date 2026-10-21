using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public bool OpenLabMenu() => OpenPlacementMenu();

        public bool BeginFacilityPlacement(string id)
        {
            if (Panel != ShopPanel.Arrange || !CanArrange) return false;
            var facility = Layout.GetFacility(id);
            if (!facility.HasValue) return false;
            movingFacilityId = id; movingTableId = null; placementDefinition = id;
            PlacementRotation = facility.Value.QuarterTurns;
            StartPlacementPreview();
            return true;
        }

        void FacilityAnchor(out float x, out float z)
        {
            x = z = 0;
            if (movingFacilityId == null) return;
            var facility = Layout.GetFacility(movingFacilityId).Value;
            x = facility.WorldX - facility.X * ShopLayout.GridSize;
            z = facility.WorldZ - facility.Z * ShopLayout.GridSize;
        }

        public bool BuyLabExpansion()
        {
            if (Panel != ShopPanel.Arrange || !CanArrange) return false;
            if (!Layout.TryPreviewLabExpansion(out var candidate).Success)
            { Notify(Layout.LabTier == ShopLayout.MaxLabTier ? "Your lab is fully expanded." : "Keep a walking route to the rear lab clear, then retry."); return false; }
            int price = Layout.NextLabExpansionPrice;
            if (Model.Currency < price) { Notify("Need " + price + " credits to expand the lab."); return false; }
            var player = World.transform.InverseTransformPoint(Player.transform.position);
            if (!candidate.CanReachMainAisle(player.x, player.z))
            { Notify("Step into the clear lab aisle before expanding."); return false; }
            if (!TryApplyLayoutGeometry(candidate))
            { Notify("Expansion could not keep customer routes clear. Your credits are unchanged."); return false; }
            var result = Layout.TryExpandLab(Model, Cycle.Phase);
            if (!result.Success) { RestoreCurrentLayout(); Notify(PlacementFailureText(result.Failure)); return false; }
            World.ApplyLayout(Layout); World.Refresh(Model); Focused = null; SaveNow();
            Notify("Lab expanded / " + price + " credits paid. Move your equipment into the new space.");
            return true;
        }

    }
}
