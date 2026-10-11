using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>Stable identity on owned furniture colliders; no stock or economy authority.</summary>
    public sealed class PlacedWorkTable : MonoBehaviour
    {
        public string Id { get; internal set; }
        public string DefinitionId { get; internal set; }
    }

    public sealed partial class ShopWorld
    {
        public ShopLayout AppliedLayout { get; private set; } = new ShopLayout();
        readonly Dictionary<string, PlacedWorkTable> placedTables = new Dictionary<string, PlacedWorkTable>();
        readonly HashSet<string> layoutIds = new HashSet<string>();
        readonly List<string> retiredTableIds = new List<string>();
        readonly List<Renderer> previewSurfaces = new List<Renderer>();
        readonly List<Renderer> previewFootprint = new List<Renderer>();
        Transform placementPreview;
        Mesh placementCube;
        Material previewValid, previewInvalid, footprintValid, footprintInvalid;
        bool? previewWasValid;
        string previewDefinition;

        /// <summary>Reflects active furniture in a validated or committed layout. Existing placed roots retain identity.</summary>
        public void ApplyLayout(ShopLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            CancelFxTransient();
            if (!cream) throw new InvalidOperationException("Build the shop world before applying furniture.");
            ApplyStoreExpansion(layout.StoreTier);
            ApplyLabExpansion(layout.LabTier);
            ApplyLabFacilityPoses(layout);
            AppliedLayout = layout;
            layoutIds.Clear();
            foreach (var item in layout.Items)
            {
                // Stored furniture keeps its domain identity but has no live collision or display stations.
                // Excluding its ID also retires a previously placed root before the next navigation bake.
                if (item.Stored) continue;
                layoutIds.Add(item.Id);
                placedTables.TryGetValue(item.Id, out var table);
                if (table && table.DefinitionId != item.DefinitionId)
                { RetireConditioner(table.transform); RetireStorageResidents(table.transform); RetirePlacementItem(table); table = null; }
                if (!table)
                {
                    var root = Group(item.DefinitionId + " / " + item.Id, transform, Vector3.zero);
                    table = root.gameObject.AddComponent<PlacedWorkTable>();
                    table.Id = item.Id;
                    table.DefinitionId = item.DefinitionId;
                    // One solid footprint keeps both the player and the navigation bake out from under the table.
                    if (item.DefinitionId == ShopLayout.TableDefinitionId)
                    {
                        var collider = root.gameObject.AddComponent<BoxCollider>();
                        collider.center = new Vector3(0, .48f, 0);
                        collider.size = new Vector3(ShopLayout.TableWidth, .96f, ShopLayout.TableDepth);
                        BuildWorktable(root, false);
                    }
                    else if (item.DefinitionId == ShopLayout.RackDefinitionId) BuildPlacedStorage(root, item.Id, false);
                    else if (item.DefinitionId == ShopLayout.ConditionerDefinitionId) BuildPlacedConditioner(root, item.Id, false);
                    else if (ShopLayout.IsDecor(item.DefinitionId)) BuildPlacedDecor(root, item.DefinitionId, false);
                    else BuildPlacedDisplay(root, item.Id, item.DefinitionId, false);
                    placedTables[item.Id] = table;
                }
                table.transform.localPosition = new Vector3(item.X * ShopLayout.GridSize, 0, item.Z * ShopLayout.GridSize);
                table.transform.localRotation = Quaternion.Euler(0, QuarterTurn(item.QuarterTurns) * 90, 0);
            }

            retiredTableIds.Clear();
            foreach (var pair in placedTables)
                if (!layoutIds.Contains(pair.Key)) retiredTableIds.Add(pair.Key);
            foreach (string id in retiredTableIds)
            {
                var table = placedTables[id];
                if (table)
                {
                    RetireConditioner(table.transform);
                    RetireStorageResidents(table.transform);
                    RetirePlacementItem(table);
                }
                placedTables.Remove(id);
            }
            Physics.SyncTransforms();
        }

        public bool TryGetPlacedTable(string id, out Transform root)
        {
            root = null;
            if (string.IsNullOrEmpty(id) || !placedTables.TryGetValue(id, out var table) || !table) return false;
            root = table.transform;
            return true;
        }

        /// <summary>Coordinates are world-local metres, and rotation is a quarter-turn index, not degrees.</summary>
        public void ShowPlacementPreview(float x, float z, int rotation, bool valid)
            => ShowPlacementPreview(ShopLayout.TableDefinitionId, x, z, rotation, valid);

        public void ShowPlacementPreview(string definitionId, float x, float z, int rotation, bool valid)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z))
            {
                HidePreview();
                return;
            }
            EnsurePlacementPreview(definitionId);
            placementPreview.localPosition = new Vector3(x, 0, z);
            placementPreview.localRotation = Quaternion.Euler(0, QuarterTurn(rotation) * 90, 0);
            if (previewWasValid != valid)
            {
                foreach (var renderer in previewSurfaces) renderer.sharedMaterial = valid ? previewValid : previewInvalid;
                foreach (var renderer in previewFootprint) renderer.sharedMaterial = valid ? footprintValid : footprintInvalid;
                previewWasValid = valid;
            }
            placementPreview.gameObject.SetActive(true);
        }

        public void HidePreview()
        {
            if (placementPreview) placementPreview.gameObject.SetActive(false);
        }

        static int QuarterTurn(int turns) => ((turns % 4) + 4) % 4;

        void BuildWorktable(Transform root, bool preview)
        {
            TablePart(root, "Porcelain worktop", new Vector3(0, .90f, 0), new Vector3(1.5f, .12f, 1f), cream, preview);
            TablePart(root, "Seafoam apron", new Vector3(0, .77f, 0), new Vector3(1.32f, .16f, .82f), shell, preview);
            TablePart(root, "Lower utility shelf", new Vector3(0, .27f, 0), new Vector3(1.28f, .075f, .78f), trim, preview);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    TablePart(root, "Metal leg", new Vector3(x * .62f, .43f, z * .36f), new Vector3(.10f, .82f, .10f), shell, preview);
                    TablePart(root, "Quiet foot", new Vector3(x * .62f, .035f, z * .36f), new Vector3(.13f, .07f, .13f), dark, preview);
                }
            TablePart(root, "Warm edge detail", new Vector3(0, .90f, -.493f), new Vector3(1.20f, .025f, .012f), gold, preview);
        }

        void TablePart(Transform root, string name, Vector3 position, Vector3 scale, Material material, bool preview)
        {
            // Mesh-only objects ensure that even a newly created preview has no transient primitive colliders.
            var part = Group(name, root, position);
            part.localScale = scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = PlacementCube();
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = preview ? previewValid : material;
            if (preview || material != null && material.renderQueue >= 3000)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (preview) previewSurfaces.Add(renderer);
        }

        void EnsurePlacementPreview(string definitionId)
        {
            if (ShopLayout.IsFacility(definitionId)) { EnsureLabFacilityPreview(definitionId); return; }
            PlacementDimensions(definitionId, out float width, out float depth, out _);
            if (placementPreview && previewDefinition == definitionId) return;
            BeginPlacementPreview(definitionId, definitionId == ShopLayout.TableDefinitionId ? "Worktable placement preview (no colliders)" :
                definitionId == ShopLayout.RackDefinitionId ? "Specimen rack placement preview (no colliders)" :
                definitionId == ShopLayout.ConditionerDefinitionId ? "Conditioning vat placement preview (no colliders)" :
                ShopLayout.IsDecor(definitionId) ? "Decor placement preview (no colliders)" : "Display placement preview (no colliders)");
            if (definitionId == ShopLayout.TableDefinitionId) BuildWorktable(placementPreview, true);
            else if (definitionId == ShopLayout.RackDefinitionId) BuildPlacedStorage(placementPreview, null, true);
            else if (definitionId == ShopLayout.ConditionerDefinitionId) BuildPlacedConditioner(placementPreview, null, true);
            else if (ShopLayout.IsDecor(definitionId)) BuildPlacedDecor(placementPreview, definitionId, true);
            else BuildPlacedDisplay(placementPreview, null, definitionId, true);
            PreviewMark("Footprint left", new Vector3(-width * .5f + .015f, .028f, 0), new Vector3(.03f, .014f, depth));
            PreviewMark("Footprint right", new Vector3(width * .5f - .015f, .028f, 0), new Vector3(.03f, .014f, depth));
            PreviewMark("Footprint front", new Vector3(0, .028f, -depth * .5f + .015f), new Vector3(width, .014f, .03f));
            PreviewMark("Footprint back", new Vector3(0, .028f, depth * .5f - .015f), new Vector3(width, .014f, .03f));
            // An asymmetric stripe exposes quarter turns even when the transparent top is viewed obliquely.
            float markerHeight = ShopLayout.IsDecor(definitionId) ? DecorHeight(definitionId) + .013f :
                definitionId == ShopLayout.TableDefinitionId ? .973f : definitionId == ShopLayout.RackDefinitionId ? 2.513f : definitionId == ShopLayout.ConditionerDefinitionId ? 1.933f : 1.65f;
            PreviewMark("Front orientation stripe", new Vector3(0, markerHeight, -.33f), new Vector3(.55f, .012f, .045f));
            if (definitionId == ShopLayout.RackDefinitionId) StoragePreviewAccess();
            if (definitionId == ShopLayout.ConditionerDefinitionId) ConditioningPreviewAccess();
            if (ShopLayout.DefinitionSlotCount(definitionId) > 0)
            {
                PreviewMark("Front access marker (outside furniture footprint)", new Vector3(0, .028f, -depth * .5f - .9f), new Vector3(1.2f, .014f, .34f));
                var accessLabel = Text(placementPreview, "FRONT ACCESS", new Vector3(0, .038f, -depth * .5f - .9f), .035f, dark.color, 1.08f);
                accessLabel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
        }

        void BeginPlacementPreview(string definitionId, string objectName)
        {
            if (placementPreview)
            {
                labels.RemoveAll(label => !label.Text || label.Text.transform.IsChildOf(placementPreview));
                placementPreview.gameObject.SetActive(false); Destroy(placementPreview.gameObject);
            }
            previewSurfaces.Clear(); previewFootprint.Clear(); previewWasValid = null;
            previewDefinition = definitionId;
            if (!previewValid)
            {
                previewValid = MakeGlass("Valid placement ghost", new Color(.18f, .95f, .62f, .24f));
                previewInvalid = MakeGlass("Blocked placement ghost", new Color(1f, .18f, .18f, .24f));
                footprintValid = MakeGlass("Valid placement footprint", new Color(.17f, 1f, .60f, .88f));
                footprintInvalid = MakeGlass("Blocked placement footprint", new Color(1f, .12f, .12f, .88f));
                foreach (var material in new[] { previewValid, previewInvalid, footprintValid, footprintInvalid })
                    material.SetFloat("_Smoothness", .15f);
            }
            placementPreview = Group(objectName, transform, Vector3.zero);
        }

        void PreviewMark(string name, Vector3 position, Vector3 scale)
        {
            var mark = Group(name, placementPreview, position);
            mark.localScale = scale;
            mark.gameObject.AddComponent<MeshFilter>().sharedMesh = PlacementCube();
            var renderer = mark.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = footprintValid;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            previewFootprint.Add(renderer);
        }

        Mesh PlacementCube()
        {
            if (placementCube) return placementCube;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var triangles = new List<int>(36);
            Vector3[] faces = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (Vector3 normal in faces)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                vertices.Add((normal - u - v) * .5f);
                vertices.Add((normal + u - v) * .5f);
                vertices.Add((normal + u + v) * .5f);
                vertices.Add((normal - u + v) * .5f);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            placementCube = new Mesh { name = "Owned worktable cube" };
            placementCube.SetVertices(vertices);
            placementCube.SetNormals(normals);
            placementCube.SetTriangles(triangles, 0);
            placementCube.RecalculateBounds();
            return placementCube;
        }

        void DisposePlacementPresentation()
        {
            DisposeDecorPresentation();
            if (previewValid) Destroy(previewValid);
            if (previewInvalid) Destroy(previewInvalid);
            if (footprintValid) Destroy(footprintValid);
            if (footprintInvalid) Destroy(footprintInvalid);
            if (placementCube) Destroy(placementCube);
        }
    }
}
