using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        Transform storeWestWall;
        readonly Transform[] storeWings = new Transform[2];
        readonly Light[] wingLights = new Light[2];
        readonly Dictionary<string, string> slotNames = new Dictionary<string, string>();
        readonly List<string> retiredSlots = new List<string>();
        int appliedStoreTier = -1;

        public string SlotName(string tankId)
        {
            if (tankId == "tank-a") return "Display tank A";
            if (tankId == "tank-b") return "Display tank B";
            return tankId != null && slotNames.TryGetValue(tankId, out var name) ? name : "Display slot";
        }

        static void PlacementDimensions(string definitionId, out float width, out float depth, out int slots)
        {
            width = ShopLayout.DefinitionWidth(definitionId);
            depth = ShopLayout.DefinitionDepth(definitionId);
            slots = ShopLayout.DefinitionSlotCount(definitionId);
            if (width <= 0 || depth <= 0) throw new ArgumentException("Unknown furniture definition: " + definitionId);
        }

        void ApplyStoreExpansion(int tier)
        {
            if (tier < 0 || tier > 2) throw new ArgumentOutOfRangeException(nameof(tier));
            if (appliedStoreTier == tier) return;
            for (int i = 0; i < storeWings.Length; i++)
            {
                if (i < tier && !storeWings[i]) BuildStoreWing(i);
                if (storeWings[i]) storeWings[i].gameObject.SetActive(i < tier);
            }
            // The original lab wall remains at x=-7. Only the store's external west wall moves.
            storeWestWall.localPosition = new Vector3(-7 - tier * 4, 2.1f, -2.5f);
            appliedStoreTier = tier;
            RefreshWingLights();
        }

        void BuildStoreWing(int index)
        {
            float x = -9 - index * 4;
            var root = Group("Store expansion " + (index + 1), transform, Vector3.zero);
            storeWings[index] = root;
            var wall = transform.Find("Right wall").GetComponent<Renderer>().sharedMaterial;
            var floor = transform.Find("Front store floor").GetComponent<Renderer>().sharedMaterial;
            Shape(root, "Expanded store floor", PrimitiveType.Cube, new Vector3(x, -.14f, -2.5f), new Vector3(4, .28f, 6), floor, true);
            Shape(root, "Expanded store front wall", PrimitiveType.Cube, new Vector3(x, 2.1f, -5.6f), new Vector3(4, 4.2f, .3f), wall, true);
            Shape(root, "Expanded store rear wall", PrimitiveType.Cube, new Vector3(x, 2.1f, .5f), new Vector3(4, 4.2f, .22f), wall, true);
            Shape(root, "Expanded store ceiling", PrimitiveType.Cube, new Vector3(x, 4.25f, -2.5f), new Vector3(4, .18f, 6), wall);
            Shape(root, "Gallery rear base", PrimitiveType.Cube, new Vector3(x, .12f, .365f), new Vector3(4, .24f, .04f), dark);
            Shape(root, "Gallery front base", PrimitiveType.Cube, new Vector3(x, .12f, -5.41f), new Vector3(4, .24f, .04f), dark);
            Shape(root, "Gallery ceiling beam", PrimitiveType.Cube, new Vector3(x, 4.02f, -2.5f), new Vector3(4, .12f, .2f), dark);
            Shape(root, "Gallery ceiling light", PrimitiveType.Cube, new Vector3(x, 3.94f, -2.5f), new Vector3(2.6f, .06f, .16f), glow);
            // A flush matching runner identifies the new shopping space without adding a physical step.
            Shape(root, "Gallery floor accent", PrimitiveType.Cube, new Vector3(x, .008f, -2.5f), new Vector3(3.4f, .01f, 4.8f), shell);
            for (int z = -4; z <= 0; z += 2)
                Shape(root, "Gallery floor joint", PrimitiveType.Cube, new Vector3(x, .015f, z), new Vector3(4, .004f, .012f), trim);
            Shape(root, "Gallery sign backing", PrimitiveType.Cube, new Vector3(x, 3.23f, .335f), new Vector3(3.25f, .7f, .045f), dark);
            Text(root, "GALLERY 0" + (index + 1), new Vector3(x, 3.37f, .30f), .115f, cream.color, 3f);
            Text(root, "ROOM FOR MORE LITTLE LIVES", new Vector3(x, 3.08f, .30f), .037f, shell.color, 3f);
            var light = Group("Gallery fill light", root, new Vector3(x, 2.8f, -2.4f)).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 7; light.shadows = LightShadows.None;
            wingLights[index] = light;
            DecorateAquaticStoreWing(root, index);
        }

        void RefreshWingLights()
        {
            if (!storeFillLight) return;
            foreach (var light in wingLights)
                if (light) { light.color = storeFillLight.color; light.intensity = storeFillLight.intensity; }
        }

        void BuildPlacedDisplay(Transform root, string itemId, string definitionId, bool preview)
        {
            PlacementDimensions(definitionId, out float width, out float depth, out int slotCount);
            if (slotCount < 1 || slotCount > 2) throw new ArgumentException("A display needs one or two slots.");
            TablePart(root, "Display cabinet", new Vector3(0, .35f, 0), new Vector3(width - .04f, .7f, depth - .04f), dark, preview);
            TablePart(root, "Porcelain lower rim", new Vector3(0, .74f, 0), new Vector3(width, .08f, depth), cream, preview);
            TablePart(root, "Porcelain upper rim", new Vector3(0, 1.59f, 0), new Vector3(width, .08f, depth), trim, preview);
            float slotWidth = width / slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                float offset = (i - (slotCount - 1) * .5f) * slotWidth;
                var slot = Group("Display compartment " + (i + 1), root, new Vector3(offset, 0, 0));
                TablePart(slot, "Clear compartment glass", new Vector3(0, 1.17f, 0), new Vector3(slotWidth - .08f, .77f, depth - .08f), glass, preview);
                TablePart(slot, "Aquatic tint", new Vector3(0, 1.16f, 0), new Vector3(slotWidth - .14f, .71f, depth - .14f), water, preview);
                TablePart(slot, "Compartment sand", new Vector3(0, .82f, 0), new Vector3(slotWidth - .14f, .05f, depth - .14f), cream, preview);
                TablePart(slot, "Compartment front plate", new Vector3(0, .42f, -.478f), new Vector3(slotWidth - .12f, .39f, .025f), shell, preview);
                for (int side = -1; side <= 1; side += 2)
                    TablePart(slot, "Compartment frame", new Vector3(side * (slotWidth * .5f - .025f), 1.16f, 0), new Vector3(.05f, .79f, depth), trim, preview);
                if (preview) continue;
                string id = itemId + "-slot-" + (i + 1);
                var station = slot.gameObject.AddComponent<Station>();
                station.Kind = StationKind.Display; station.TankId = id;
                station.Label = ShopLayout.DefinitionName(definitionId) + " " + itemId.Replace("table-", "#") + (slotCount == 1 ? "" : " / slot " + (i + 1));
                stations.Add(station); slotNames[id] = station.Label;
                var collider = slot.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .82f, 0); collider.size = new Vector3(slotWidth, 1.64f, depth);
                Text(slot, slotCount == 1 ? "SMALL DISPLAY" : "LARGE DISPLAY / " + (i + 1), new Vector3(0, .52f, -.495f), .035f, dark.color, slotWidth - .16f);
                var label = Text(slot, "EMPTY / 1 SPACE", new Vector3(0, .35f, -.495f), .027f, dark.color, slotWidth - .16f);
                var resident = Group("Resident", slot, new Vector3(0, 1.19f, 0));
                // Scale the established contained swim envelope as a unit, retaining each species' silhouette.
                resident.localScale = Vector3.one * (slotCount == 1 ? .62f : .51f);
                displays[id] = new Display { Root = resident, Seed = i + slotNames.Count * .61f, Status = label,
                    Interior = new Bounds(new Vector3(0, 1.19f, 0), new Vector3(slotWidth - .12f, .72f, depth - .16f)) };
            }
        }

        void RefreshPlacedDisplays(GameModel model)
        {
            foreach (var pair in displays)
            {
                var display = pair.Value;
                if (!display.Status) continue;
                var resident = model.GetTank(pair.Key);
                SetCreature(display, resident.HasValue ? resident.Value.SpeciesId : null, resident.HasValue ? resident.Value.Id : null);
                display.Status.text = !resident.HasValue ? "EMPTY / 1 SPACE" : SpeciesName(resident.Value.SpeciesId).ToUpperInvariant() +
                    (model.GetReservation(pair.Key).HasValue ? " / RESERVED" : " / " + model.GetPrice(resident.Value.SpeciesId).Value.Price + " CREDITS");
            }
        }

        void RetirePlacementItem(PlacedWorkTable table)
        {
            Transform root = table.transform;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            root.gameObject.SetActive(false);
            labels.RemoveAll(label => !label.Text || label.Text.transform.IsChildOf(root));
            stations.RemoveAll(station => !station || station.transform.IsChildOf(root));
            retiredSlots.Clear();
            foreach (var pair in displays)
                if (pair.Value.Root && pair.Value.Root.IsChildOf(root)) retiredSlots.Add(pair.Key);
            foreach (string id in retiredSlots) { displays.Remove(id); slotNames.Remove(id); }
            Destroy(root.gameObject);
        }
    }
}
