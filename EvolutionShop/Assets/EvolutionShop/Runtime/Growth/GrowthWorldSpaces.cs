using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-10's spaces in the greybox (GR.11): shop unit C behind unit B and the back room (10 x 5 m, a 3.2 m ceiling),
    /// opened through an arch in unit B's north wall, with its feature wall (two Show tank places either side of a built-in
    /// feature tank) and the exhibition hall's plans in its north-east corner; the exhibition hall finish (5 stars: banners
    /// unrolled from the ceilings and spotlights on the feature and exhibition places); and set-piece placeholders for the
    /// art pass (GR-14): the bay window bench in unit B's front wall, a window display in unit A's window, the pipe gallery
    /// on the annex's west wall and the dome light in the workshop. The user chose (2026-10-23) that a room bought at night
    /// opens with its partition coming down: it sinks into the floor over two seconds while the room's lamp fades up (the
    /// way in is clear at once), as unit B, the lab annex, the workshop and unit C do; the hall unrolls its banners and
    /// brings up its spotlights. Loading a shop shows its rooms as they are, without a reveal. Plain shapes only.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        public const float RevealSeconds = 2f;
        public static readonly Vector3 UnitCArch = new Vector3(10.875f, 0, 4f), HallBoardPosition = new Vector3(10.3f, 1.6f, 8.86f), FeatureTankPosition = new Vector3(5.975f, 0, 8.75f);
        public static readonly Vector3 BayWindowBenchPosition = new Vector3(8f, 0, -3.675f), WorkshopDomePosition = new Vector3(8f, 3.6f, 12f);

        /// <summary>A room's opening: its partition (sunk into the floor as it opens), its lamp, and for the hall the banners it
        /// unrolls and the spotlights it brings up; Progress runs from 0 to 1 through a reveal.</summary>
        sealed class Opening
        {
            public GameObject Partition;
            public float Height;
            public readonly List<(Light light, float intensity)> Lights = new List<(Light, float)>();
            public readonly List<Transform> Unroll = new List<Transform>();
            /// <summary>GR-16: things shown only while the room is open (the hall's spotlight fixtures).</summary>
            public readonly List<GameObject> Shown = new List<GameObject>();
            public bool Open, Known;
            public float Progress = 1;
        }

        readonly Dictionary<string, Opening> openings = new Dictionary<string, Opening>();
        bool revealNow = true;
        Light unitCLight;
        Transform hallBoard;
        Material accent, copper, cushion;
        readonly List<Material> bannerMats = new List<Material>();

        /// <summary>Validation: whether a room's partition still closes it (closed rooms only: one being revealed is open),
        /// whether its reveal is under way and how far.</summary>
        public bool Standing(string room) => openings.TryGetValue(room, out var o) && !o.Open;
        public bool Revealing(string room) => openings.TryGetValue(room, out var o) && o.Progress < 1;
        public float RevealProgress(string room) => openings.TryGetValue(room, out var o) ? o.Progress : 1;
        public bool PartitionShowing(string room) => openings.TryGetValue(room, out var o) && o.Partition != null && o.Partition.activeSelf;
        public float PartitionDrop(string room) => openings.TryGetValue(room, out var o) && o.Partition != null ? -o.Partition.transform.localPosition.y : 0;
        public float LampIntensity(string room) => openings.TryGetValue(room, out var o) ? o.Lights.Where(l => l.light.enabled).Sum(l => l.light.intensity) : 0;
        /// <summary>What a room's lamps come to once it is open (GR-16: the look's light keys set it).</summary>
        public float LampFull(string room) => openings.TryGetValue(room, out var o) ? o.Lights.Sum(l => l.intensity) : 0;
        public int BannersShown => openings.TryGetValue("hall", out var o) ? o.Unroll.Count(b => b.gameObject.activeSelf && b.localScale.y > 0.99f) : 0;
        public bool UnitCWallStanding => Standing("unit-c");

        void AddOpening(string room, GameObject partition, float height, params Light[] lights)
        {
            var o = new Opening { Partition = partition, Height = height };
            foreach (var l in lights) o.Lights.Add((l, l.intensity));
            openings[room] = o;
        }

        /// <summary>Shows a room open or closed; a room that opens during play is revealed (its partition comes down).</summary>
        void SyncOpening(string room, bool open)
        {
            var o = openings[room];
            if (o.Known && !revealNow && o.Open == open) return;
            bool reveal = o.Known && !revealNow && open && !o.Open;
            o.Known = true; o.Open = open;
            foreach (var c in Colliders(o)) c.enabled = !open;
            if (reveal) { o.Progress = 0; ApplyReveal(o, 0); return; }
            o.Progress = 1;
            if (o.Partition != null) { o.Partition.transform.localPosition = Vector3.zero; o.Partition.SetActive(!open); }
            foreach (var (light, intensity) in o.Lights) { light.enabled = open; light.intensity = intensity; }
            foreach (var b in o.Unroll) { b.gameObject.SetActive(open); b.localScale = Vector3.one; }
            foreach (var g in o.Shown) g.SetActive(open);
        }

        /// <summary>GR-16: a room's lamp at the look's light key; a room still being revealed fades up to it.</summary>
        void SetOpeningLamp(Light light, float intensity)
        {
            foreach (var o in openings.Values)
                for (int i = 0; i < o.Lights.Count; i++)
                    if (o.Lights[i].light == light)
                    {
                        o.Lights[i] = (light, intensity);
                        if (o.Progress >= 1) light.intensity = intensity;
                        return;
                    }
            light.intensity = intensity;
        }

        static IEnumerable<Collider> Colliders(Opening o) => o.Partition == null ? new Collider[0] : o.Partition.GetComponentsInChildren<Collider>(true);

        void ApplyReveal(Opening o, float f)
        {
            float e = Mathf.SmoothStep(0, 1, f);
            if (o.Partition != null)
            {
                o.Partition.SetActive(f < 1);
                o.Partition.transform.localPosition = f < 1 ? new Vector3(0, -o.Height * e, 0) : Vector3.zero;
                if (f >= 1) foreach (var c in Colliders(o)) c.enabled = true; // ready if the room is ever shown closed again
            }
            foreach (var (light, intensity) in o.Lights) { light.enabled = true; light.intensity = intensity * e; }
            foreach (var b in o.Unroll) { b.gameObject.SetActive(true); b.localScale = new Vector3(1, Mathf.Max(0.02f, e), 1); }
            foreach (var g in o.Shown) g.SetActive(true);
        }

        void AnimateOpenings(float dt)
        {
            foreach (var o in openings.Values)
            {
                if (o.Progress >= 1) continue;
                o.Progress = Mathf.Min(1, o.Progress + dt / RevealSeconds);
                ApplyReveal(o, o.Progress);
            }
        }

        void SyncSpaces(GrowthShop shop)
        {
            SyncOpening("unit-b", shop.UnitB);
            SyncOpening("annex", shop.Annex);
            SyncOpening("workshop", shop.Workshop);
            SyncOpening("research", shop.ResearchRoom);
            SyncOpening("unit-c", shop.UnitC);
            SyncOpening("hall", shop.Hall);
            revealNow = false;
        }

        // ---------------------------------------------------------------- building

        void SpaceMaterials()
        {
            if (LookOn)
            {
                // GR-16: the look's colours for what the kit does not replace (the confetti's banner colours, a pillow's).
                accent = Plastic("feature wall", C("#22BFB0"));
                copper = Plastic("copper", C("#E39A5A"), 0.5f);
                cushion = Plastic("cushion", C("#FF7A7A"), 0.3f);
                foreach (var hex in new[] { "#FF7A7A", "#FFC93C", "#9ADBFF" }) bannerMats.Add(Plastic("banner " + bannerMats.Count, C(hex), 0.3f));
                return;
            }
            accent =ShopWorld.MakeMaterial("GR feature wall", new Color(0.20f, 0.42f, 0.47f), 0, 0.25f);
            copper = ShopWorld.MakeMaterial("GR copper", new Color(0.72f, 0.45f, 0.26f), 0.6f, 0.45f);
            cushion = ShopWorld.MakeMaterial("GR cushion", new Color(0.86f, 0.64f, 0.52f), 0, 0.15f);
            foreach (var c in new[] { new Color(0.84f, 0.38f, 0.27f), new Color(0.98f, 0.78f, 0.37f), new Color(0.31f, 0.55f, 0.66f) })
                bannerMats.Add(ShopWorld.MakeMaterial("GR banner " + bannerMats.Count, c, 0, 0.15f));
        }

        /// <summary>Unit C: its floor, ceiling and east wall (its others are the back room's, unit A's, unit B's, the annex's and
        /// the workshop's), the partition in unit B's arch, its lamp, the feature wall and the exhibition hall's plans.</summary>
        void UnitCRoom()
        {
            var room = GrowthSpaces.RoomById("unit-c").Area;
            var rooms = new GameObject("Shop unit C").transform; rooms.SetParent(transform, false);
            Vector3 centre = new Vector3((room.X0 + room.X1) * 0.5f, 0, (room.Z0 + room.Z1) * 0.5f), size = new Vector3(room.X1 - room.X0, 0.1f, room.Z1 - room.Z0);
            Box(rooms, "Unit C floor", centre + new Vector3(0, -0.05f, 0), size, floorB, true);
            Box(rooms, "Unit C ceiling", centre + new Vector3(0, 3.25f, 0), size, wall);
            Wall("Unit C east wall", new Vector2(room.X1, room.Z0), new Vector2(room.X1, room.Z1), 3.2f, wall);
            unitCLight = Lamp("Shop unit C lamp", new Vector3(centre.x, 2.3f, centre.z), 10, 1.25f);
            // The feature wall: an accent panel along the north wall, with a built-in feature tank between its two Show tank places.
            Box(rooms, "Feature wall", new Vector3(6f, 1.6f, room.Z1 - 0.11f), new Vector3(7.6f, 3.2f, 0.02f), accent);
            var tank = new GameObject("Feature tank (set piece)").transform; tank.SetParent(rooms, false);
            tank.localPosition = FeatureTankPosition;
            Box(tank, "Frame", new Vector3(0, 1.2f, 0), new Vector3(1.25f, 1.6f, 0.3f), cabinet, true);
            Box(tank, "Water", new Vector3(0, 1.25f, -0.1f), new Vector3(1.05f, 1.2f, 0.12f), water);
            Box(tank, "Glass", new Vector3(0, 1.25f, -0.165f), new Vector3(1.1f, 1.25f, 0.01f), glass);
            Box(tank, "Light strip", new Vector3(0, 1.93f, -0.12f), new Vector3(1.0f, 0.04f, 0.06f), lightBar);
            for (int i = 0; i < 3; i++)
                ShopWorld.Shape(tank, "Weed", PrimitiveType.Cylinder, new Vector3(-0.35f + 0.35f * i, 0.85f + 0.08f * i, -0.1f), new Vector3(0.05f, 0.2f + 0.05f * i, 0.05f), plantMaterial());
            // The partition in unit B's arch.
            var arch = GrowthSpaces.Doorways.First(d => d.Id == "arch-bc");
            float w = arch.To.x - arch.From.x;
            var partitionRoot = new GameObject("Unit C partition"); partitionRoot.transform.SetParent(transform, false);
            var board = Box(partitionRoot.transform, "Partition", new Vector3(UnitCArch.x, 1.2f, UnitCArch.z), new Vector3(w, 2.4f, 0.12f), partition, true);
            Box(partitionRoot.transform, "Coming soon sign", new Vector3(UnitCArch.x, 1.5f, UnitCArch.z - 0.07f), new Vector3(w * 0.72f, 0.5f, 0.02f), sign);
            Box(partitionRoot.transform, "Sign stripe", new Vector3(UnitCArch.x, 1.5f, UnitCArch.z - 0.09f), new Vector3(w * 0.5f, 0.1f, 0.02f), paper);
            Target(board.gameObject, GrowthTargetKind.UnitCWall, "unit-c");
            AddOpening("unit-c", partitionRoot, 2.45f, unitCLight);
            // The exhibition hall's plans, on the north wall in unit C's north-east corner.
            hallBoard = new GameObject("Exhibition hall plans").transform; hallBoard.SetParent(transform, false);
            hallBoard.localPosition = HallBoardPosition; hallBoard.localRotation = Quaternion.Euler(0, 180, 0);
            Box(hallBoard, "Board", Vector3.zero, new Vector3(0.9f, 0.7f, 0.05f), counterMat, true);
            Box(hallBoard, "Plan", new Vector3(0, 0, 0.03f), new Vector3(0.7f, 0.5f, 0.01f), paper);
            Box(hallBoard, "Plan outline", new Vector3(0, 0.02f, 0.04f), new Vector3(0.5f, 0.3f, 0.005f), trim);
            Box(hallBoard, "Banner sketch", new Vector3(0.22f, -0.12f, 0.04f), new Vector3(0.1f, 0.18f, 0.005f), bannerMats[0]);
            Target(hallBoard.gameObject, GrowthTargetKind.HallBoard, "hall");
        }

        Material plantMat;
        Material plantMaterial() => plantMat ? plantMat : plantMat = ShopWorld.MakeMaterial("GR feature tank weed", new Color(0.35f, 0.62f, 0.38f), 0, 0.4f);

        /// <summary>The set pieces' placeholders (GR.11; the art pass, GR-14, draws them): the bay window bench in unit B's
        /// front wall, a window display on unit A's sill, the pipe gallery on the annex's west wall and the workshop's dome light.</summary>
        void SetPieces()
        {
            var root = new GameObject("Set pieces").transform; root.SetParent(transform, false);
            // Unit B's bay window (the window is a gap in its front wall) and its bench.
            Box(root, "Bay window sill", new Vector3(BayWindowBenchPosition.x, 0.45f, -4), new Vector3(1.8f, 0.9f, 0.22f), wall, true);
            Box(root, "Bay window glass", new Vector3(BayWindowBenchPosition.x, 1.65f, -4), new Vector3(1.8f, 1.5f, 0.05f), glass, true);
            var bench = new GameObject("Bay window bench").transform; bench.SetParent(root, false);
            bench.localPosition = BayWindowBenchPosition;
            Box(bench, "Seat", new Vector3(0, 0.225f, 0), new Vector3(1.8f, 0.45f, 0.45f), counterMat, true);
            Box(bench, "Cushion", new Vector3(0, 0.49f, 0.02f), new Vector3(1.7f, 0.08f, 0.38f), cushion);
            foreach (int side in new[] { -1, 1 }) Box(bench, "Pillow", new Vector3(side * 0.62f, 0.62f, -0.1f), new Vector3(0.32f, 0.22f, 0.12f), bannerMats[side < 0 ? 1 : 2]);
            // Unit A's window display: a small lit tank on the sill.
            var display = new GameObject("Window display").transform; display.SetParent(root, false);
            display.localPosition = new Vector3(-2.6f, 0.9f, -3.93f);
            Box(display, "Stand", new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.08f, 0.16f), stand);
            Box(display, "Water", new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.32f, 0.12f), water);
            Box(display, "Glass", new Vector3(0, 0.26f, 0), new Vector3(0.62f, 0.36f, 0.14f), glass);
            Box(display, "Price card", new Vector3(0.38f, 0.12f, 0.06f), new Vector3(0.1f, 0.07f, 0.01f), paper);
            // The annex's pipe gallery: a manifold of copper pipes, headers, valves and gauges on its west wall.
            var gallery = new GameObject("Pipe gallery").transform; gallery.SetParent(root, false);
            gallery.localPosition = new Vector3(-3.84f, 0, 12.7f);
            for (int i = 0; i < 4; i++)
            {
                float z = -1.2f + 0.8f * i;
                ShopWorld.Shape(gallery, "Riser", PrimitiveType.Cylinder, new Vector3(0, 1.8f, z), new Vector3(0.1f, 1.6f, 0.1f), copper);
                var wheel = ShopWorld.Shape(gallery, "Valve", PrimitiveType.Cylinder, new Vector3(0.12f, 1.35f, z), new Vector3(0.22f, 0.02f, 0.22f), sign);
                wheel.localRotation = Quaternion.Euler(0, 0, 90);
                var gauge = ShopWorld.Shape(gallery, "Gauge", PrimitiveType.Cylinder, new Vector3(0.1f, 2.1f, z + 0.18f), new Vector3(0.14f, 0.01f, 0.14f), paper);
                gauge.localRotation = Quaternion.Euler(0, 0, 90);
            }
            foreach (float y in new[] { 0.5f, 3.1f })
            {
                var header = ShopWorld.Shape(gallery, "Header", PrimitiveType.Cylinder, new Vector3(0, y, 0), new Vector3(0.14f, 1.45f, 0.14f), copper);
                header.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Box(gallery, "Placard", new Vector3(0.05f, 2.6f, 1.55f), new Vector3(0.02f, 0.25f, 0.5f), paper);
            // The workshop's dome light: a glass dome hanging below its ceiling with a glow inside.
            var dome = new GameObject("Workshop dome").transform; dome.SetParent(root, false);
            dome.localPosition = WorkshopDomePosition;
            ShopWorld.Shape(dome, "Dome", PrimitiveType.Sphere, Vector3.zero, new Vector3(2.4f, 0.9f, 2.4f), glass);
            ShopWorld.Shape(dome, "Ring", PrimitiveType.Cylinder, new Vector3(0, -0.02f, 0), new Vector3(2.5f, 0.02f, 2.5f), trim);
            ShopWorld.Shape(dome, "Glow", PrimitiveType.Sphere, new Vector3(0, -0.05f, 0), new Vector3(1.0f, 0.3f, 1.0f), lightBar);
        }

        /// <summary>The exhibition hall finish (5 stars): banners in each shop unit, unrolled from the ceiling, and spotlights
        /// on the feature places and the exhibition places, brought up as it is revealed.</summary>
        void HallFinish()
        {
            var root = new GameObject("Exhibition hall finish").transform; root.SetParent(transform, false);
            var hall = new Opening();
            int index = 0;
            foreach (var (position, yaw) in new[] { (new Vector3(2.4f, 3.15f, 3.82f), 0f), (new Vector3(7.0f, 3.15f, 3.82f), 0f), (new Vector3(8.0f, 3.15f, -3.82f), 180f), (new Vector3(10.9f, 3.15f, 8.8f), 0f), (new Vector3(2.2f, 3.15f, 6.5f), 90f) })
            {
                var rod = Box(root, "Banner rod", position, new Vector3(0.9f, 0.04f, 0.04f), trim);
                rod.localRotation = Quaternion.Euler(0, yaw, 0);
                var pivot = new GameObject("Banner " + (index + 1)).transform; pivot.SetParent(root, false);
                pivot.localPosition = position; pivot.localRotation = Quaternion.Euler(0, yaw, 0);
                Box(pivot, "Cloth", new Vector3(0, -0.6f, 0), new Vector3(0.8f, 1.2f, 0.03f), bannerMats[index % bannerMats.Count]);
                Box(pivot, "Emblem", new Vector3(0, -0.5f, (yaw == 180 ? 1 : -1) * 0.02f), new Vector3(0.3f, 0.3f, 0.01f), paper);
                hall.Unroll.Add(pivot);
                index++;
            }
            foreach (var place in new[] { "b4", "c1", "c2", "e1", "e2" })
            {
                var spot = GrowthSpaces.SpotOf(place);
                var l = new GameObject("Spotlight on " + place).AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = new Vector3(spot.Centre.x, 3.05f, spot.Centre.z);
                l.transform.localRotation = Quaternion.Euler(90, 0, 0);
                l.type = LightType.Spot; l.spotAngle = 60; l.range = 5; l.intensity = 2.2f; l.color = new Color(1f, 0.92f, 0.78f); l.shadows = LightShadows.None;
                hall.Lights.Add((l, l.intensity));
            }
            openings["hall"] = hall;
        }

        void ResetSpaces() => revealNow = true;
    }
}
