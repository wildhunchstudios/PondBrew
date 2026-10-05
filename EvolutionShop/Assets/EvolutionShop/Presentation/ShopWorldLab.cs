using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        readonly Dictionary<string, Transform> labFacilities = new Dictionary<string, Transform>();
        readonly Transform[] labWings = new Transform[2];
        readonly Light[] labWingLights = new Light[2];
        Transform labRearAssembly;
        int appliedLabTier = -1;

        void RegisterLabFacility(Station station)
        {
            string id;
            switch (station.Kind)
            {
                case StationKind.Bloom: id = ShopLayout.BloomFacilityId; break;
                case StationKind.Flow: id = ShopLayout.FlowFacilityId; break;
                case StationKind.Incubator: id = ShopLayout.IncubatorFacilityId; break;
                case StationKind.Upgrade: id = ShopLayout.UpgradeFacilityId; break;
                default: return;
            }
            station.FacilityId = id;
            labFacilities.Add(id, station.transform);
        }

        public bool TryGetLabFacility(string id, out Transform root)
        {
            root = null;
            return id != null && labFacilities.TryGetValue(id, out root) && root;
        }

        void ApplyLabFacilityPoses(ShopLayout layout)
        {
            foreach (var facility in layout.Facilities)
            {
                if (!TryGetLabFacility(facility.Id, out var root))
                    throw new InvalidOperationException("Missing original lab facility: " + facility.Id);
                // Move the one live station, including its status, culture cells and incubator output.
                // Production state and the original off-grid anchor belong to the model/layout.
                root.localPosition = new Vector3(facility.WorldX, 0, facility.WorldZ);
                root.localRotation = Quaternion.Euler(0, facility.QuarterTurns * 90, 0);
            }
        }

        void ApplyLabExpansion(int tier)
        {
            if (tier < 0 || tier > labWings.Length) throw new ArgumentOutOfRangeException(nameof(tier));
            if (appliedLabTier == tier) return;
            for (int i = 0; i < labWings.Length; i++)
            {
                if (i < tier && !labWings[i]) BuildLabWing(i);
                if (labWings[i]) labWings[i].gameObject.SetActive(i < tier);
            }
            // At tier zero every child retains its exact original position and material.
            // The wall, low trim and all identity text move together; no old barrier is left behind.
            labRearAssembly.localPosition = new Vector3(0, 0, tier * 4);
            appliedLabTier = tier;
            RefreshLabWingLights();
        }

        void BuildLabWing(int index)
        {
            float z = 7.5f + index * 4;
            var root = Group("Lab expansion " + (index + 1), transform, Vector3.zero);
            labWings[index] = root;
            var wall = transform.Find("Right wall").GetComponent<Renderer>().sharedMaterial;
            var floor = transform.Find("Floor").GetComponent<Renderer>().sharedMaterial;
            Shape(root, "Expanded lab floor", PrimitiveType.Cube, new Vector3(0, -.14f, z), new Vector3(14, .28f, 4), floor, true);
            Shape(root, "Expanded lab west wall", PrimitiveType.Cube, new Vector3(-7, 2.1f, z), new Vector3(.3f, 4.2f, 4), wall, true);
            Shape(root, "Expanded lab east wall", PrimitiveType.Cube, new Vector3(7, 2.1f, z), new Vector3(.3f, 4.2f, 4), wall, true);
            Shape(root, "Expanded lab ceiling", PrimitiveType.Cube, new Vector3(0, 4.25f, z), new Vector3(14, .18f, 4), wall);
            Shape(root, "Expanded lab work area", PrimitiveType.Cube, new Vector3(0, .008f, z), new Vector3(10.4f, .01f, 3.4f), shell);
            Shape(root, "Lab extension threshold", PrimitiveType.Cube, new Vector3(0, .014f, z - 2), new Vector3(13.7f, .006f, .055f), trim);
            Shape(root, "Lab extension ceiling beam", PrimitiveType.Cube, new Vector3(0, 4.02f, z), new Vector3(14, .12f, .20f), dark);
            Shape(root, "Lab extension ceiling light", PrimitiveType.Cube, new Vector3(0, 3.94f, z), new Vector3(5.8f, .06f, .16f), glow);
            for (int x = -6; x <= 6; x += 2)
                Shape(root, "Expanded lab floor joint", PrimitiveType.Cube, new Vector3(x, .016f, z), new Vector3(.012f, .004f, 4), trim);
            foreach (int side in new[] { -1, 1 })
                Shape(root, "Expanded lab wall base", PrimitiveType.Cube, new Vector3(side * 6.82f, .12f, z), new Vector3(.04f, .24f, 4), dark);
            var light = Group("Lab extension fill light", root, new Vector3(0, 2.8f, z)).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 9; light.shadows = LightShadows.None;
            labWingLights[index] = light;
            DecorateWorkingLabWing(root, index);
        }

        void RefreshLabWingLights()
        {
            if (!labFillLight) return;
            foreach (var light in labWingLights)
                if (light) { light.color = labFillLight.color; light.intensity = labFillLight.intensity; }
        }

        /// <summary>World-local metres and absolute quarter turns. Reuses HidePreview and the ordinary validity colours.</summary>
        public void ShowLabFacilityPreview(string id, float worldX, float worldZ, int quarterTurns, bool valid)
        {
            if (!ShopLayout.IsFacility(id)) throw new ArgumentException("Unknown lab facility: " + id, nameof(id));
            ShowPlacementPreview(id, worldX, worldZ, quarterTurns, valid);
        }

        void EnsureLabFacilityPreview(string id)
        {
            if (placementPreview && previewDefinition == id) return;
            if (!TryGetLabFacility(id, out var source)) throw new InvalidOperationException("Build the lab facility before previewing it: " + id);
            BeginPlacementPreview(id, "Lab facility placement preview (no colliders)");
            CopyLabPreviewMeshes(source, placementPreview);
            float width = ShopLayout.FacilityWidth(id), depth = ShopLayout.FacilityDepth(id);
            PreviewMark("Facility footprint left", new Vector3(-width * .5f + .015f, .028f, 0), new Vector3(.03f, .014f, depth));
            PreviewMark("Facility footprint right", new Vector3(width * .5f - .015f, .028f, 0), new Vector3(.03f, .014f, depth));
            PreviewMark("Facility footprint front", new Vector3(0, .028f, -depth * .5f + .015f), new Vector3(width, .014f, .03f));
            PreviewMark("Facility footprint back", new Vector3(0, .028f, depth * .5f - .015f), new Vector3(width, .014f, .03f));
            // This one-metre square matches the shared operator-access reservation.
            PreviewMark("Operator access left", new Vector3(-.5f, .028f, -1.5f), new Vector3(.025f, .014f, 1));
            PreviewMark("Operator access right", new Vector3(.5f, .028f, -1.5f), new Vector3(.025f, .014f, 1));
            PreviewMark("Operator access front", new Vector3(0, .028f, -2), new Vector3(1, .014f, .025f));
            PreviewMark("Operator access back", new Vector3(0, .028f, -1), new Vector3(1, .014f, .025f));
            var label = Text(placementPreview, "OPERATOR ACCESS", new Vector3(0, .038f, -1.5f), .03f, dark.color, .9f);
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void CopyLabPreviewMeshes(Transform source, Transform destination)
        {
            foreach (Transform child in source)
            {
                if (!child.gameObject.activeSelf || child.GetComponent<TextMesh>() || child.GetComponent<CreatureVisual>()) continue;
                var copy = Group(child.name + " ghost", destination, child.localPosition);
                copy.localRotation = child.localRotation;
                copy.localScale = child.localScale;
                var filter = child.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh && child.GetComponent<MeshRenderer>())
                {
                    // Borrow only meshes from the persistent original root. No renderer material, collider,
                    // Station, animation or production component is copied, and no mesh asset is allocated.
                    copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var renderer = copy.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = previewValid;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    previewSurfaces.Add(renderer);
                }
                CopyLabPreviewMeshes(child, copy);
            }
        }
    }
}
