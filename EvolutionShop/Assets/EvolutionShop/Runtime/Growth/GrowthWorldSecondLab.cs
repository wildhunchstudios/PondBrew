using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-34 wave 2 (tester round 2, F69, the preview approved in GR.77): the second lab, west of the laboratory and below the
    /// research room (x -12.5 to -5, z 5 to 13.75, on the new plan: it was built on it), behind a hoarding in the power room's west
    /// wall until it opens. Its three incubators along its north wall (fronts south, as the laboratory's) and three breeding tanks
    /// along its west wall (fronts east), each place a dashed slot until its machine is bought; the incubators at the first one's
    /// level (its screen, tint port and auto-doser port), their clutches on their own trays; the pipe the room comes with, from
    /// the buffers along the power room's south wall, overhead along the north wall, dropping to each incubator's auto-doser port
    /// once it has one. Kit pieces dress it; the greybox's shapes keep the colliders and targets.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        /// <summary>The doorway along the power room's west wall, in metres of the plan before from its south end (z 11.8 to 13.2 now).</summary>
        public const float Lab2DoorFrom = 0.44f, Lab2DoorTo = 1.56f;
        public static Vector3 LabIncubatorPosition(int place) => V(GrowthLab2.Incubators[place]);
        public static Vector3 LabBreederPosition(int place) => V(GrowthLab2.Breeders[place]);
        const float LabPipeY = 3.3f, LabPipeZ = 13.54f, LabPipeIn = 11.6f;

        GameObject lab2Wall;
        Light lab2Light;
        Transform lab2Root, lab2Pipe;
        readonly Transform[] labIncubators = new Transform[GrowthShop.Lab2Places], labIncubatorSlots = new Transform[GrowthShop.Lab2Places];
        readonly Transform[] labBreeders = new Transform[GrowthShop.Lab2Places], labBreederSlots = new Transform[GrowthShop.Lab2Places];
        readonly Light[] labIncubatorGlows = new Light[GrowthShop.Lab2Places];
        readonly Renderer[] labIncubatorDomes = new Renderer[GrowthShop.Lab2Places];
        readonly List<GrowthCreatureView>[] labTrayViews = Enumerable.Range(0, GrowthShop.Lab2Places).Select(_ => new List<GrowthCreatureView>()).ToArray();
        readonly Dictionary<int, GrowthCreatureView>[] labBreederViews = Enumerable.Range(0, GrowthShop.Lab2Places).Select(_ => new Dictionary<int, GrowthCreatureView>()).ToArray();
        int shownLabIncubators = -1, shownLabBreeders = -1, shownLabBreederLevel = -1;
        string shownLabPipe;
        Material wallLab2;

        /// <summary>Validation: what the second lab shows now.</summary>
        public bool SecondLabWallStanding => Standing("lab2");
        public int LabIncubatorsShown => labIncubators.Count(t => t != null && t.gameObject.activeSelf);
        public int LabIncubatorSlotsShown => labIncubatorSlots.Count(t => t != null && t.gameObject.activeSelf);
        public int LabBreedersShown => labBreeders.Count(t => t != null && t.gameObject.activeSelf);
        public int LabBreederSlotsShown => labBreederSlots.Count(t => t != null && t.gameObject.activeSelf);
        public int LabTrayCreaturesShown(int place) => labTrayViews[place].Count(v => v != null);
        public int LabBreederCreaturesShown(int place) => labBreederViews[place].Count;
        /// <summary>The overhead pipe's drops shown (one to each incubator with an auto-doser port).</summary>
        public int LabPipeDropsShown => lab2Pipe == null ? 0 : lab2Pipe.Cast<Transform>().Count(c => c.name == "Drop");
        public Transform LabIncubatorRoot(int place) => labIncubators[place];
        public Transform LabBreederRoot(int place) => labBreeders[place];

        void SecondLabRoom()
        {
            wallLab2 = LookOn ? Plastic("second lab wall", C("#E4F7EC"), 0.12f) : ShopWorld.MakeMaterial("GR second lab wall", new Color(0.82f, 0.93f, 0.86f), 0, 0.2f);
            var root = new GameObject("Second lab").transform; root.SetParent(transform, false);
            lab2Root = root;
            var a = GrowthLab2.Area;
            Vector3 centre = new Vector3((a[0] + a[2]) * 0.5f, 0, (a[1] + a[3]) * 0.5f), size = new Vector3(a[2] - a[0], 0.1f, a[3] - a[1]);
            Box(root, "Second lab floor", centre + new Vector3(0, -0.05f, 0), size, floorTile, true);
            Box(root, "Second lab ceiling", centre + new Vector3(0, 3.65f, 0), size, wallLab2);
            // Its west and south walls; the north is the research room's south wall, the east the laboratory's and the power room's west walls.
            Wall("Second lab west wall", new Vector2(-10, 4), new Vector2(-10, 11), 3.6f, wallLab2);
            Wall("Second lab south wall", new Vector2(-10, 4), new Vector2(-4, 4), 3.6f, wallLab2);
            // A mint face on this side of its east wall, round the doorway (the laboratory's and the power room's sides keep theirs).
            float face = a[2] - 0.105f, door0 = GrowthPlan.S(9 + Lab2DoorFrom), door1 = GrowthPlan.S(9 + Lab2DoorTo);
            Box(root, "Second lab east face", new Vector3(face, 1.8f, (a[1] + door0) * 0.5f), new Vector3(0.01f, 3.6f, door0 - a[1]), wallLab2);
            Box(root, "Second lab east face", new Vector3(face, 1.8f, (door1 + a[3]) * 0.5f), new Vector3(0.01f, 3.6f, a[3] - door1), wallLab2);
            Box(root, "Second lab east face", new Vector3(face, 3.2f, (door0 + door1) * 0.5f), new Vector3(0.01f, 0.8f, door1 - door0), wallLab2);
            Box(root, "Second lab north face", new Vector3(centre.x, 1.8f, a[3] - 0.105f), new Vector3(size.x, 3.6f, 0.01f), wallLab2);
            lab2Light = Lamp("Second lab lamp", new Vector3(centre.x, 3.3f, centre.z), 10, 1.15f);
            // The hoarding in the power room's west wall, its sign to the power room.
            lab2Wall = new GameObject("Second lab partition");
            lab2Wall.transform.SetParent(transform, false);
            float w = door1 - door0, z = (door0 + door1) * 0.5f;
            var board = Box(lab2Wall.transform, "Partition", new Vector3(a[2], 1.4f, z), new Vector3(0.12f, 2.8f, w), partition, true);
            Box(lab2Wall.transform, "Coming soon sign", new Vector3(a[2] + 0.07f, 1.6f, z), new Vector3(0.02f, 0.5f, w * 0.75f), sign);
            Box(lab2Wall.transform, "Sign stripe", new Vector3(a[2] + 0.09f, 1.6f, z), new Vector3(0.02f, 0.1f, w * 0.55f), paper);
            Target(board.gameObject, GrowthTargetKind.SecondLabWall, "lab2");
            SecondLabStations();
        }

        void SecondLabStations()
        {
            for (int k = 0; k < GrowthShop.Lab2Places; k++)
            {
                // An incubator (as the laboratory's: its front -Z, its tray before its dome).
                var inc = new GameObject("Second lab incubator " + (k + 2)).transform; inc.SetParent(lab2Root, false);
                inc.localPosition = LabIncubatorPosition(k);
                Box(inc, "Base", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.7f), cabinet, true);
                labIncubatorDomes[k] = ShopWorld.Shape(inc, "Dome", PrimitiveType.Sphere, new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.45f, 0.55f), dome).GetComponent<Renderer>();
                ShopWorld.Shape(inc, "Tray", PrimitiveType.Cylinder, new Vector3(0, 0.91f, -0.22f), new Vector3(0.3f, 0.02f, 0.2f), paper);
                var glow = new GameObject("Glow").AddComponent<Light>();
                glow.transform.SetParent(inc, false); glow.transform.localPosition = new Vector3(0, 1.1f, 0);
                glow.type = LightType.Point; glow.range = 1.6f; glow.intensity = 0; glow.color = new Color(1f, 0.85f, 0.55f); glow.shadows = LightShadows.None;
                labIncubatorGlows[k] = glow;
                Target(inc.gameObject, GrowthTargetKind.LabIncubator, k.ToString());
                AddCollider(inc.gameObject, new Vector3(0, 0.7f, 0), new Vector3(0.95f, 1.4f, 0.8f));
                labIncubators[k] = inc;
                labIncubatorSlots[k] = MachineSlot("Second lab incubator place " + (k + 2), LabIncubatorPosition(k), 0, 0.9f, 0.7f, GrowthTargetKind.LabIncubator, k);
                inc.gameObject.SetActive(false);
                // A breeding tank (as the laboratory's: its front +Z, turned to face east).
                var b = new GameObject("Second lab breeding tank " + (k + 2)).transform; b.SetParent(lab2Root, false);
                b.localPosition = LabBreederPosition(k); b.localRotation = Quaternion.Euler(0, 90, 0);
                labBreeders[k] = b;
                labBreederSlots[k] = MachineSlot("Second lab breeding tank place " + (k + 2), LabBreederPosition(k), 90, 1.1f, 0.5f, GrowthTargetKind.LabBreeder, k);
                b.gameObject.SetActive(false);
            }
        }

        /// <summary>A machine's place before it is bought: a dashed outline and a card on the floor, focused to place it.</summary>
        Transform MachineSlot(string name, Vector3 at, float yaw, float width, float depth, GrowthTargetKind kind, int place)
        {
            var slot = new GameObject(name).transform; slot.SetParent(lab2Root, false);
            slot.localPosition = at; slot.localRotation = Quaternion.Euler(0, yaw, 0);
            int n = Mathf.Max(3, Mathf.RoundToInt(width / 0.24f));
            for (int i = 0; i < n; i++)
            {
                float x = -width * 0.5f + (i + 0.5f) * width / n;
                Box(slot, "Dash", new Vector3(x, 0.01f, depth * 0.5f), new Vector3(0.14f, 0.01f, 0.03f), marker);
                Box(slot, "Dash", new Vector3(x, 0.01f, -depth * 0.5f), new Vector3(0.14f, 0.01f, 0.03f), marker);
            }
            foreach (int side in new[] { -1, 1 }) Box(slot, "Dash", new Vector3(side * width * 0.5f, 0.01f, 0), new Vector3(0.03f, 0.01f, depth * 0.6f), marker);
            Box(slot, "Sign", new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.02f, 0.2f), paper);
            Target(slot.gameObject, kind, place.ToString());
            var c = slot.gameObject.AddComponent<BoxCollider>();
            c.center = new Vector3(0, 0.45f, 0); c.size = new Vector3(width, 0.9f, depth); c.isTrigger = true; // focus, not a wall
            slot.gameObject.SetActive(false);
            return slot;
        }

        /// <summary>The look's pieces for the room.</summary>
        void DressSecondLab(Transform dressing)
        {
            Hide(lab2Root, "Second lab floor");
            foreach (var asset in new[] { "SecondLabFloor", "SecondLabDressing" }) Kit(dressing, asset);
            foreach (var inc in labIncubators) Kit(inc, "Incubator", default, null, "Base", "Tray");
            var hoarding = Kit(lab2Wall.transform, "PartitionLab2", default, null, "Partition", "Coming soon sign", "Sign stripe");
            if (hoarding) hoarding.localRotation = Quaternion.Euler(0, 180, 0);
            labLamps = labLamps.Append(lab2Light).ToArray();
        }

        // ---------------------------------------------------------------- what it shows

        void SyncSecondLab(GrowthShop shop)
        {
            int incs = shop.SecondLab ? shop.LabIncubators.Count : 0, breeders = shop.SecondLab ? shop.LabBreeders.Count : 0;
            int key = shop.SecondLab ? 1 : 0;
            if (incs != shownLabIncubators || key * 10 + breeders != shownLabBreeders || shop.Level("breeding") != shownLabBreederLevel)
            {
                bool rebuildBreeders = shop.Level("breeding") != shownLabBreederLevel;
                shownLabIncubators = incs; shownLabBreeders = key * 10 + breeders; shownLabBreederLevel = shop.Level("breeding");
                for (int k = 0; k < GrowthShop.Lab2Places; k++)
                {
                    labIncubators[k].gameObject.SetActive(k < incs);
                    labIncubatorSlots[k].gameObject.SetActive(shop.SecondLab && k >= incs);
                    labBreederSlots[k].gameObject.SetActive(shop.SecondLab && k >= breeders);
                    if (rebuildBreeders) BuildLabBreeder(k, shownLabBreederLevel);
                    labBreeders[k].gameObject.SetActive(k < breeders);
                }
            }
            for (int k = 0; k < GrowthShop.Lab2Places; k++)
            {
                // Each incubator at the first one's level, its glow while it works, its clutch on its tray.
                Look("lab-incubator:" + k, labIncubators[k], shop.Level("incubator"), IncubatorParts);
                var inc = shop.LabIncubator(k);
                labIncubatorGlows[k].intensity = inc == null ? 0 : inc.Incubating ? 0.45f : inc.Tray.Count > 0 ? 0.12f : 0;
                var tray = inc?.Tray ?? new List<GrowthSpecimen>();
                var views = labTrayViews[k];
                if (views.Count != tray.Count || views.Where((v, i) => v == null || v.SpecimenId != tray[i].Id).Any())
                {
                    foreach (var v in views) if (v) Destroy(v.gameObject);
                    views.Clear();
                    for (int i = 0; i < tray.Count; i++)
                    {
                        var view = GrowthCreatureView.Create(tray[i], labIncubators[k]);
                        view.transform.localPosition = new Vector3((i - (tray.Count - 1) * 0.5f) * 0.11f, 0.98f, -0.22f);
                        views.Add(view);
                    }
                }
                var b = shop.LabBreeder(k);
                var inside = b == null ? new List<GrowthSpecimen>() : b.Pairs.SelectMany(p => p.Offspring != null ? p.Parents.Concat(new[] { p.Offspring }) : p.Parents).ToList();
                var bv = labBreederViews[k];
                foreach (var s in inside) if (!bv.ContainsKey(s.Id)) bv[s.Id] = GrowthCreatureView.Create(s, labBreeders[k]);
                foreach (var id in bv.Keys.Where(x => inside.All(s => s.Id != x)).ToList()) { if (bv[id]) Destroy(bv[id].gameObject); bv.Remove(id); }
            }
            string pipe = shop.SecondLab ? incs + "|" + shop.LabAutoDoser : "";
            if (pipe != shownLabPipe) { shownLabPipe = pipe; BuildLabPipe(shop); }
        }

        /// <summary>A breeding tank of the second lab at the first one's level (rebuilt when that level changes), its creatures with it.</summary>
        void BuildLabBreeder(int k, int level)
        {
            var root = labBreeders[k];
            foreach (Transform child in root.Cast<Transform>().ToList()) Destroy(child.gameObject);
            labBreederViews[k].Clear();
            level = Mathf.Max(1, level);
            Box(root, "Cabinet", new Vector3(0, 0.39f, 0), new Vector3(1.1f, 0.78f, 0.5f), cabinet, true);
            Box(root, "Glass", new Vector3(0, BreedingWaterY, 0), new Vector3(1.06f, 0.44f, 0.46f), glass);
            Box(root, "Water", new Vector3(0, BreedingWaterY - 0.02f, 0), new Vector3(1.0f, 0.36f, 0.42f), water);
            Box(root, "Divider", new Vector3(BreedingDivider, BreedingWaterY, 0), new Vector3(0.015f, 0.4f, 0.44f), trim);
            if (level >= 3) Box(root, "Pair divider", new Vector3(SecondPairDivider, BreedingWaterY, 0), new Vector3(0.012f, 0.4f, 0.44f), glass);
            Box(root, "Gravel", new Vector3(0, 0.8f, 0), new Vector3(1.0f, 0.03f, 0.42f), gravel);
            Box(root, "Label", new Vector3(0, 0.55f, 0.255f), new Vector3(0.56f, 0.14f, 0.02f), paper);
            Box(root, "Heart", new Vector3(-0.2f, 0.55f, 0.267f), new Vector3(0.08f, 0.08f, 0.01f), sign);
            Box(root, "Nursery label", new Vector3(0.36f, 0.72f, 0.255f), new Vector3(0.22f, 0.06f, 0.02f), marker);
            if (root.GetComponent<GrowthTarget>() == null) Target(root.gameObject, GrowthTargetKind.LabBreeder, k.ToString());
            if (root.GetComponent<BoxCollider>() == null) AddCollider(root.gameObject, new Vector3(0, 0.62f, 0), new Vector3(1.12f, 1.24f, 0.52f));
            if (LookOn)
            {
                // The laboratory's look without its tint port: the second lab's water has none.
                Kit(root, "BreedingTank", default, null, "Cabinet", "Divider", "Gravel", "Label", "Heart", "Nursery label");
                if (level >= 3) Kit(root, "BreedingPair");
            }
        }

        /// <summary>The pipe the room comes with: from the buffers along the power room's south wall, through the wall over the
        /// doorway, along the second lab's north wall over its incubators, and a drop to each incubator's auto-doser port.</summary>
        void BuildLabPipe(GrowthShop shop)
        {
            if (lab2Pipe) Destroy(lab2Pipe.gameObject);
            if (!shop.SecondLab) return;
            lab2Pipe = new GameObject("Second lab pipe").transform; lab2Pipe.SetParent(lab2Root, false);
            var mat = LookOn ? LookMaterial("GK_Teal") : trim;
            var from = new Vector3(GrowthLab.BufferPoint(0).x, LabPipeY, LabPipeIn);
            float east = GrowthLab2.Area[2] - 0.6f, west = GrowthLab2.Incubators[0].x - 0.3f;
            var points = new List<Vector3> { from, new Vector3(east, LabPipeY, LabPipeIn), new Vector3(east, LabPipeY, LabPipeZ), new Vector3(west, LabPipeY, LabPipeZ) };
            for (int i = 1; i < points.Count; i++) Segment(lab2Pipe, "Run", points[i - 1], points[i], 0.035f, mat);
            foreach (var p in points.Skip(1).Take(2)) ShopWorld.Shape(lab2Pipe, "Joint", PrimitiveType.Sphere, p, Vector3.one * 0.1f, mat);
            if (!shop.LabAutoDoser) return;
            for (int k = 0; k < shop.LabIncubators.Count; k++)
            {
                var top = new Vector3(GrowthLab2.Incubators[k].x, LabPipeY, LabPipeZ);
                Segment(lab2Pipe, "Drop", top, new Vector3(top.x, 1.24f, LabPipeZ), 0.03f, mat);
                ShopWorld.Shape(lab2Pipe, "Joint", PrimitiveType.Sphere, top, Vector3.one * 0.09f, mat);
            }
        }

        /// <summary>A new shop (a reload): its machines are built again as it has them.</summary>
        void ResetSecondLab()
        {
            for (int k = 0; k < GrowthShop.Lab2Places; k++)
            {
                foreach (var v in labTrayViews[k]) if (v) Destroy(v.gameObject);
                labTrayViews[k].Clear();
                foreach (var v in labBreederViews[k].Values) if (v) Destroy(v.gameObject);
                labBreederViews[k].Clear();
            }
            shownLabIncubators = shownLabBreeders = shownLabBreederLevel = -1;
            shownLabPipe = null;
        }

        /// <summary>The pairs swim in their compartments, the offspring bob in the nurseries, when someone is near enough to see them.</summary>
        void AnimateSecondLab(float t, GrowthShop shop)
        {
            if (!shop.SecondLab || session.Player == null) return;
            var at = session.Player.transform.position;
            if (at.x > S(2) || at.z > S(12)) return;
            for (int k = 0; k < shop.LabIncubators.Count; k++)
                for (int i = 0; i < labTrayViews[k].Count; i++)
                    if (labTrayViews[k][i]) { labTrayViews[k][i].transform.localRotation = Quaternion.Euler(0, t * 40f + i * 120f, 0); labTrayViews[k][i].Tick(t, session.ReducedMotion); }
            int level = shownLabBreederLevel;
            for (int k = 0; k < shop.LabBreeders.Count; k++)
            {
                var b = shop.LabBreeders[k];
                for (int p = 0; p < b.Pairs.Length; p++)
                {
                    var pair = b.Pairs[p];
                    float left = level >= 3 && p == 1 ? SecondPairDivider + 0.01f : -0.5f, right = level >= 3 && p == 0 ? SecondPairDivider - 0.01f : BreedingDivider - 0.01f;
                    for (int i = 0; i < pair.Parents.Count; i++) SwimIn(labBreederViews[k], pair.Parents[i], i, left, right, t);
                    if (pair.Offspring != null && labBreederViews[k].TryGetValue(pair.Offspring.Id, out var o) && o != null)
                    {
                        float nursery = (BreedingDivider + 0.5f) * 0.5f, z = level >= 3 ? (p == 0 ? -0.09f : 0.09f) : 0;
                        o.transform.localPosition = new Vector3(nursery, BreedingWaterY + 0.03f * Mathf.Sin(t * 1.7f + p + k), z + 0.04f * Mathf.Sin(t * 0.9f + p));
                        o.transform.localRotation = Quaternion.Euler(0, 90 + 25f * Mathf.Sin(t * 0.6f + p), 0);
                        o.Tick(t, session.ReducedMotion);
                    }
                }
            }
        }

        void SwimIn(Dictionary<int, GrowthCreatureView> views, GrowthSpecimen parent, int i, float left, float right, float t)
        {
            if (!views.TryGetValue(parent.Id, out var v) || v == null) return;
            float L = v.Length;
            float hx = Mathf.Max(0.02f, (right - left) * 0.5f - L * 0.6f), cx = (left + right) * 0.5f, hz = Mathf.Max(0.02f, 0.21f - L * 0.5f);
            float speed = L > 0.2f ? 0.45f : L > 0.1f ? 0.6f : 0.8f, phase = i * Mathf.PI + parent.Id % 7;
            Vector3 Path(float tt) => new Vector3(cx + hx * Mathf.Sin(0.5f * tt + phase), 0.05f * Mathf.Sin(0.9f * tt + phase), hz * 0.7f * Mathf.Sin(0.8f * tt + phase * 0.5f));
            Vector3 pos = Path(t * speed), ahead = Path(t * speed + 0.2f);
            v.transform.localPosition = new Vector3(pos.x, BreedingWaterY + pos.y, pos.z);
            Vector3 dir = ahead - pos; dir.y *= 0.3f;
            if (dir.sqrMagnitude > 1e-6f) v.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            v.Tick(t, session.ReducedMotion);
        }
    }
}
