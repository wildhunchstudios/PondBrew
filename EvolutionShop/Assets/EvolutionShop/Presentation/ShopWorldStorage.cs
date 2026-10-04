using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        /// <summary>The retained owned rack root, with one interaction station and two storage compartments.</summary>
        public bool TryGetStorageRack(string itemId, out Transform root)
        {
            root = null;
            if (string.IsNullOrEmpty(itemId) || !placedTables.TryGetValue(itemId, out var item) || !item ||
                item.DefinitionId != ShopLayout.RackDefinitionId) return false;
            root = item.transform;
            return true;
        }

        void BuildPlacedStorage(Transform root, string itemId, bool preview)
        {
            float width = ShopLayout.DefinitionWidth(ShopLayout.RackDefinitionId);
            float depth = ShopLayout.DefinitionDepth(ShopLayout.RackDefinitionId);
            TablePart(root, "Storage plinth", new Vector3(0, .24f, 0), new Vector3(width, .28f, depth), dark, preview);
            TablePart(root, "Storage lower shelf", new Vector3(0, .47f, 0), new Vector3(width, .08f, depth), cream, preview);
            TablePart(root, "Storage middle shelf", new Vector3(0, 1.40f, 0), new Vector3(width, .08f, depth), cream, preview);
            TablePart(root, "Storage upper shelf", new Vector3(0, 2.23f, 0), new Vector3(width, .06f, depth), trim, preview);
            TablePart(root, "Storage cap", new Vector3(0, 2.48f, 0), new Vector3(width, .06f, depth), cream, preview);
            TablePart(root, "Storage identity plaque", new Vector3(0, 2.36f, -.489f), new Vector3(width - .12f, .21f, .022f), dark, preview);
            foreach (int side in new[] { -1, 1 })
            {
                TablePart(root, "Storage upright", new Vector3(side * .71f, 1.34f, 0), new Vector3(.08f, 2.28f, .96f), shell, preview);
                TablePart(root, "Storage upright trim", new Vector3(side * .71f, 1.34f, -.487f), new Vector3(.045f, 2.28f, .022f), trim, preview);
                for (int z = -1; z <= 1; z += 2)
                    TablePart(root, "Storage foot", new Vector3(side * .64f, .09f, z * .40f), new Vector3(.16f, .18f, .18f), dark, preview);
            }
            string[] slots = null;
            if (!preview)
            {
                slots = AppliedLayout.GetStorageSlotIds(itemId);
                if (slots.Length != 2) throw new InvalidOperationException("A specimen rack needs exactly two storage slots.");
                var station = root.gameObject.AddComponent<Station>();
                station.Kind = StationKind.StorageRack;
                station.StorageItemId = itemId;
                station.Label = "Specimen rack " + itemId.Replace("table-", "#");
                stations.Add(station);
                var collider = root.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 1.25f, 0);
                collider.size = new Vector3(width, 2.5f, depth);
                Text(root, "SPECIMEN STORAGE", new Vector3(0, 2.36f, -.506f), .047f, cream.color, width - .22f);
            }
            for (int i = 0; i < 2; i++)
            {
                var compartment = Group("Storage compartment " + (i + 1), root, new Vector3(0, .89f + i * .93f, 0));
                TablePart(compartment, "Clear storage glass", Vector3.zero, new Vector3(1.36f, .76f, .86f), glass, preview);
                TablePart(compartment, "Storage water", Vector3.zero, new Vector3(1.30f, .70f, .82f), water, preview);
                TablePart(compartment, "Storage sand", new Vector3(0, -.35f, 0), new Vector3(1.30f, .035f, .82f), cream, preview);
                TablePart(compartment, "Storage status backing", new Vector3(0, -.455f, -.478f), new Vector3(1.31f, .13f, .035f), shell, preview);
                if (preview) continue;
                var label = Text(compartment, (i + 1) + " / EMPTY", new Vector3(0, -.455f, -.504f), .038f, dark.color, 1.20f);
                var resident = Group("Stored resident", compartment, Vector3.zero);
                // Scale the already-contained display swim as one unit; retain the full species silhouette.
                resident.localScale = Vector3.one * .54f;
                displays[slots[i]] = new Display { Root = resident, Seed = i + displays.Count * .61f,
                    StorageStatus = label, StorageMaterials = new HashSet<Material>(),
                    Interior = new Bounds(Vector3.zero, new Vector3(1.30f, .70f, .82f)) };
            }
        }

        void StoragePreviewAccess()
        {
            // Same one-metre operator square as the core reservation, centered 1.4m in front.
            PreviewMark("Rack operator left", new Vector3(-.5f, .028f, -1.4f), new Vector3(.025f, .014f, 1));
            PreviewMark("Rack operator right", new Vector3(.5f, .028f, -1.4f), new Vector3(.025f, .014f, 1));
            PreviewMark("Rack operator front", new Vector3(0, .028f, -1.9f), new Vector3(1, .014f, .025f));
            PreviewMark("Rack operator back", new Vector3(0, .028f, -.9f), new Vector3(1, .014f, .025f));
            var label = Text(placementPreview, "OPERATOR ACCESS", new Vector3(0, .038f, -1.4f), .03f, dark.color, .9f);
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void RefreshStorageRacks(GameModel model)
        {
            foreach (var pair in displays)
            {
                var display = pair.Value;
                if (!display.StorageStatus) continue;
                var resident = model.GetStorage(pair.Key);
                string species = resident.HasValue ? resident.Value.SpeciesId : null;
                if (display.SpeciesId != species)
                {
                    ReleaseStorageCreature(display);
                    SetCreature(display, species);
                    if (display.Creature)
                    {
                        // New residents can render before the next world animation update (including reload).
                        // Start broadside so a long tail cannot briefly point through the shallow rack glass.
                        display.Creature.transform.localRotation = Quaternion.Euler(0, -90, 0);
                        display.Creature.Animate(lastTime + display.Seed);
                        foreach (var renderer in display.Creature.GetComponentsInChildren<Renderer>(true))
                            foreach (var material in renderer.sharedMaterials) if (material) display.StorageMaterials.Add(material);
                    }
                }
                string number = pair.Key.EndsWith("-1", StringComparison.Ordinal) ? "1" : "2";
                display.StorageStatus.text = number + " / " + (resident.HasValue ? SpeciesName(species).ToUpperInvariant() : "EMPTY");
            }
        }

        void RetireStorageResidents(Transform root)
        {
            foreach (var display in displays.Values)
                if (display.StorageMaterials != null && display.Root && display.Root.IsChildOf(root)) ReleaseStorageCreature(display);
        }

        void DisposeStoragePresentation()
        {
            foreach (var display in displays.Values)
                if (display.StorageMaterials != null) ReleaseStorageCreature(display);
        }

        static void ReleaseStorageCreature(Display display)
        {
            if (display.Creature)
            {
                // CreatureVisual creates these materials per resident; rack frames borrow the world's materials.
                display.Creature.gameObject.SetActive(false);
                Destroy(display.Creature.gameObject);
            }
            // Keep owned references until release, even if child OnDestroy ran before the world callback.
            foreach (var material in display.StorageMaterials) if (material) Destroy(material);
            display.StorageMaterials.Clear();
            display.Creature = null;
            display.SpeciesId = null;
        }
    }
}
