using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        bool showLabFacilities;

        public bool OpenLabMenu()
        {
            if (!OpenPlacementMenu()) return false;
            showLabFacilities = true; showDecorCatalogue = false; showOwnedFurniture = false;
            panelScroll = Vector2.zero;
            return true;
        }

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

        void DrawLabArrangement()
        {
            Heading("Culture lab / tier " + (Layout.LabTier + 1) + " of 3");
            Text("Extend the lab behind the shop, then rearrange your working equipment. Each extension adds 4 m of floor space.");
            if (Layout.LabTier < ShopLayout.MaxLabTier)
                Button("Expand lab / " + Layout.NextLabExpansionPrice + " credits", () => BuyLabExpansion());
            else Small("Lab fully expanded. Both rear extensions are ready.");
            Small("Expansion adds space. Grower speed, incubation time and creature capacity stay the same.");
            Space();
            Text("Specimen racks " + Layout.RackCount + " / " + Layout.RackLimit + " owned / " + StoredSpecimenCount + " creatures stored");
            if (Layout.RackCount < Layout.RackLimit)
                Button("Specimen rack / 2 slots / 32 credits", () => BeginPlacement(null, ShopLayout.RackDefinitionId));
            else Small("Rack limit reached. Each lab expansion permits one more rack.");
            Small("Racks free the incubator for the next creature. Hidden stock is never sold to customers. Move, store or sell racks from Owned / storage.");
            Space();
            foreach (var facility in Layout.Facilities)
            {
                string id = facility.Id;
                Button("Move " + ShopLayout.FacilityName(id) + " / free", () => BeginFacilityPlacement(id));
            }
            Small("Equipment keeps its cultures, doses, work in progress and ready specimen when moved. These four essential stations cannot be stored or sold.");
            Small("Night only. Aim at the floor; R rotates; left click places; right click or Escape cancels. Leave the marked front service space open.");
        }
    }
}
