using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        sealed class ConditionerVisual
        {
            public Transform Root;
            public TextMesh Status;
            public readonly ConditioningResident[] Residents = new ConditioningResident[2];
            public Transform Paddle;
            public TextMesh MistStatus;
            public readonly List<Transform> MistCells = new List<Transform>();
            public bool Processing;
            public readonly ConditioningFx Fx = new ConditioningFx();
        }

        sealed class ConditioningResident
        {
            public Display Display;
            public MeshRenderer WaterRenderer;
            public MaterialPropertyBlock WaterProperties;
            public Color WaterColor;
            public TextMesh Status;
            public readonly HashSet<Material> Materials = new HashSet<Material>();
        }

        readonly Dictionary<string, ConditionerVisual> conditioners = new Dictionary<string, ConditionerVisual>();

        public bool TryGetConditioner(string itemId, out Transform root)
        {
            root = null;
            if (string.IsNullOrEmpty(itemId) || !conditioners.TryGetValue(itemId, out var vat) || !vat.Root) return false;
            root = vat.Root;
            return true;
        }

        void BuildPlacedConditioner(Transform root, string itemId, bool preview)
        {
            const float width = 2f, depth = 1f;
            TablePart(root, "Conditioner console", new Vector3(0, .35f, 0), new Vector3(width, .7f, depth), dark, preview);
            TablePart(root, "Conditioner worktop", new Vector3(0, .74f, 0), new Vector3(width, .08f, depth), cream, preview);
            TablePart(root, "Conditioner upper rim", new Vector3(0, 1.59f, 0), new Vector3(width, .08f, depth), cream, preview);
            TablePart(root, "Conditioner identity plaque", new Vector3(0, 1.80f, -.47f), new Vector3(1.88f, .24f, .05f), dark, preview);
            TablePart(root, "Conditioner Spark band", new Vector3(0, .10f, -.501f), new Vector3(1.85f, .08f, .014f), sparkMat, preview);
            TablePart(root, "Conditioner console stripe", new Vector3(0, .65f, -.501f), new Vector3(1.85f, .035f, .014f), gold, preview);
            for (int side = -1; side <= 1; side += 2)
            {
                TablePart(root, "Conditioner side rail", new Vector3(side * .96f, 1.15f, 0), new Vector3(.08f, .90f, depth), shell, preview);
                TablePart(root, "Conditioner header support", new Vector3(side * .84f, 1.74f, .40f), new Vector3(.08f, .36f, .08f), trim, preview);
            }
            TablePart(root, "Conditioner central divider", new Vector3(0, 1.16f, 0), new Vector3(.08f, .84f, depth), trim, preview);
            var paddle = Group("Conditioner mixing indicator", root, new Vector3(0, .35f, -.521f));
            TablePart(paddle, "Mixing indicator vertical", Vector3.zero, new Vector3(.036f, .20f, .018f), gold, preview);
            TablePart(paddle, "Mixing indicator horizontal", Vector3.zero, new Vector3(.20f, .036f, .018f), gold, preview);

            ConditionerVisual vat = null;
            if (!preview)
            {
                var station = root.gameObject.AddComponent<Station>();
                station.Kind = StationKind.Conditioning;
                station.ConditionerItemId = itemId;
                station.Label = "Conditioning vat " + itemId.Replace("table-", "#");
                stations.Add(station);
                var collider = root.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .96f, 0);
                collider.size = new Vector3(width, 1.92f, depth);
                Text(root, "SPARK CONDITIONING", new Vector3(0, 1.80f, -.502f), .055f, cream.color, 1.7f);
                vat = new ConditionerVisual { Root = root, Paddle = paddle,
                    Status = Text(root, "0 / 2  /  EMPTY", new Vector3(0, .53f, -.519f), .047f, new Color(1f, .88f, .53f), 1.76f) };
                Text(root, "E / LOAD, CONDITION, COLLECT", new Vector3(0, .20f, -.519f), .033f, cream.color, 1.76f);
                vat.Fx.Key = "conditioning-fx:" + itemId;
                conditioners.Add(itemId, vat);
                TablePart(root, "Mist culture plaque", new Vector3(0, 1.04f, -.51f), new Vector3(1.48f, .18f, .05f), dark, false);
                vat.MistStatus = Text(root, "MIST RESEARCH", new Vector3(0, 1.04f, -.538f), .038f, new Color(.70f, 1f, .96f), 1.39f);
                for (int i = 0; i < 6; i++)
                {
                    var cell = MistOrganism(root, .11f);
                    cell.name = "Mist grower unit " + (i + 1);
                    cell.localPosition = new Vector3((i - 2.5f) * .23f, .85f, -.53f);
                    vat.MistCells.Add(cell);
                    cell.gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < 2; i++)
            {
                var chamber = Group("Conditioning chamber " + (i + 1), root, new Vector3(i == 0 ? -.49f : .49f, 1.16f, 0));
                TablePart(chamber, "Conditioning glass", Vector3.zero, new Vector3(.88f, .78f, .88f), glass, preview);
                TablePart(chamber, "Conditioning water", Vector3.zero, new Vector3(.82f, .72f, .82f), water, preview);
                TablePart(chamber, "Conditioning sand", new Vector3(0, -.36f, 0), new Vector3(.82f, .025f, .82f), cream, preview);
                TablePart(chamber, "Conditioning slot plaque", new Vector3(0, -.43f, -.476f), new Vector3(.87f, .11f, .026f), shell, preview);
                if (preview) continue;
                var residentRoot = Group("Conditioning resident", chamber, Vector3.zero);
                residentRoot.localScale = Vector3.one * .34f;
                var display = new Display { Root = residentRoot, Seed = i + displays.Count * .61f,
                    Interior = new Bounds(Vector3.zero, new Vector3(.82f, .72f, .82f)) };
                // These internal visual keys are not tank/storage IDs and register no customer inventory.
                displays[itemId + "-conditioning-visual-" + i] = display;
                var waterRenderer = chamber.Find("Conditioning water").GetComponent<MeshRenderer>();
                vat.Residents[i] = new ConditioningResident { Display = display, WaterRenderer = waterRenderer,
                    WaterProperties = new MaterialPropertyBlock(), WaterColor = waterRenderer.sharedMaterial.GetColor(DoseFluidColor),
                    Status = Text(chamber, (i + 1) + " / EMPTY", new Vector3(0, -.43f, -.493f), .030f, dark.color, .80f) };
            }
        }

        void ConditioningPreviewAccess()
        {
            PreviewMark("Vat operator left", new Vector3(-.5f, .028f, -1.4f), new Vector3(.025f, .014f, 1));
            PreviewMark("Vat operator right", new Vector3(.5f, .028f, -1.4f), new Vector3(.025f, .014f, 1));
            PreviewMark("Vat operator front", new Vector3(0, .028f, -1.9f), new Vector3(1, .014f, .025f));
            PreviewMark("Vat operator back", new Vector3(0, .028f, -.9f), new Vector3(1, .014f, .025f));
            var label = Text(placementPreview, "OPERATOR ACCESS", new Vector3(0, .038f, -1.4f), .03f, dark.color, .9f);
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void RefreshConditioners(GameModel model)
        {
            foreach (var pair in conditioners)
            {
                var vat = pair.Value;
                // Candidate geometry can precede the model registry during atomic placement preflight.
                var view = model.GetConditioner(pair.Key);
                vat.Processing = view.HasValue && view.Value.Phase == ConditionerPhase.Processing;
                var mist = model.GetGrower(CultureKind.Mist);
                vat.MistStatus.text = !view.HasValue ? "SUPPLY UNAVAILABLE" : model.MistResearchUnlocked ? "MIST " + mist.Units + "/" + mist.Capacity +
                    (mist.Units <= 0 ? " / REFILL" : mist.Units >= mist.Capacity ? " / FULL" : " / REFILL") : "MIST RESEARCH / E";
                for (int i = 0; i < vat.MistCells.Count; i++) vat.MistCells[i].gameObject.SetActive(view.HasValue && model.MistResearchUnlocked && i < mist.Units);
                int count = view.HasValue ? view.Value.Count : 0;
                vat.Status.text = count + " / 2  /  " + (vat.Processing ? Mathf.CeilToInt(view.Value.SecondsRemaining) + "s REMAINING" :
                    view.HasValue && view.Value.Phase == ConditionerPhase.Ready ? "READY / COLLECT" : "EMPTY / ADD PONDLETS");
                for (int i = 0; i < 2; i++)
                {
                    SpecimenView? specimen = view.HasValue ? (i == 0 ? view.Value.First : view.Value.Second) : null;
                    var resident = vat.Residents[i];
                    string species = specimen.HasValue ? specimen.Value.SpeciesId : null;
                    if (resident.Display.SpeciesId != species)
                    {
                        ReleaseConditioningResident(resident);
                        SetCreature(resident.Display, species);
                        if (resident.Display.Creature)
                        {
                            resident.Display.Creature.transform.localRotation = Quaternion.Euler(0, -90, 0);
                            resident.Display.Creature.Animate(lastTime + resident.Display.Seed);
                            foreach (var renderer in resident.Display.Creature.GetComponentsInChildren<Renderer>(true))
                                foreach (var material in renderer.sharedMaterials) if (material) resident.Materials.Add(material);
                        }
                    }
                    resident.Status.text = (i + 1) + " / " + (species == null ? "EMPTY" : SpeciesName(species).ToUpperInvariant());
                }
            }
        }

        void AnimateConditioners(float time)
        {
            foreach (var vat in conditioners.Values)
            {
                for (int i = 0; i < vat.MistCells.Count; i++) {
                    vat.MistCells[i].localPosition = FxAdvancedLabMotion.SourceCell(CultureKind.Mist, i, time, fxReducedMotion || !AdvancedVisible);
                    vat.MistCells[i].localRotation = Quaternion.Euler(0, 0, fxReducedMotion || !AdvancedVisible ? 0 : Mathf.Sin(time * .8f + i * 2.399f) * 8);
                }
            }
        }

        void RetireConditioner(Transform root)
        {
            var owned = root.GetComponent<PlacedWorkTable>();
            if (!owned || !conditioners.TryGetValue(owned.Id, out var vat) || vat.Root != root) return;
            RetireConditionerFx(vat);
            if (advancedSources[1].Root == vat.Root) RetireAdvancedSource(1);
            foreach (var resident in vat.Residents) ReleaseConditioningResident(resident);
            conditioners.Remove(owned.Id);
        }

        void DisposeConditioningPresentation()
        {
            foreach (var vat in conditioners.Values)
                foreach (var resident in vat.Residents) ReleaseConditioningResident(resident);
            conditioners.Clear();
        }

        static void ReleaseConditioningResident(ConditioningResident resident)
        {
            if (resident == null) return;
            var display = resident.Display;
            if (display.Creature) { display.Creature.gameObject.SetActive(false); Destroy(display.Creature.gameObject); }
            foreach (var material in resident.Materials) if (material) Destroy(material);
            resident.Materials.Clear();
            display.Creature = null;
            display.SpeciesId = null;
        }
    }
}
