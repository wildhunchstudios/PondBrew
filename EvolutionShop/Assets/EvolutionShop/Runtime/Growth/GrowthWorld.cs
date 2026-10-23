using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public enum GrowthTargetKind { Grower, Incubator, Tank, Place, Counter, Workbench, UnitBWall, Board }

    /// <summary>Something the reticle can focus on in the greybox; Id is a culture, a container id or a place.</summary>
    public sealed class GrowthTarget : MonoBehaviour
    {
        public GrowthTargetKind Kind;
        public string Id;
    }

    /// <summary>
    /// GR-02's greybox rooms at real proportions (GR.11): shop unit A (8 x 8 m) with four container places, a counter by
    /// the entrance and a window; the back room behind it (6 x 5 m, a 3.6 m ceiling) with the Bloom and Flow growers, the
    /// incubator, the tool bench and the pinboard; and shop unit B (8 x 8 m) to the east behind a wall that opens when it
    /// is bought. Plain shapes and colours only: the look comes later (GR.14).
    /// </summary>
    public sealed class GrowthWorld : MonoBehaviour
    {
        public static readonly Vector3 Spawn = new Vector3(0.6f, 0.05f, 5.4f);

        /// <summary>A container place: where the tank stands, which way its front faces, and where a visitor stands to browse it.</summary>
        public struct Place
        {
            public Vector3 Position, Browse;
            public float Yaw;
            public Vector3[] Route;
        }

        static readonly Vector3 Outside = new Vector3(-0.5f, 0, -5.8f), Door = new Vector3(-0.5f, 0, -3.3f), Hub = new Vector3(-0.5f, 0, -1.6f),
            West = new Vector3(-2.6f, 0, -1.3f), NorthWest = new Vector3(-2.6f, 0, 1.8f), East = new Vector3(1.5f, 0, -1.0f), NorthEast = new Vector3(1.5f, 0, 1.8f),
            ArchWest = new Vector3(3.3f, 0, 0), ArchEast = new Vector3(4.7f, 0, 0), UnitBNorth = new Vector3(8.0f, 0, 1.7f);

        public static readonly Dictionary<string, Place> Places = new Dictionary<string, Place>
        {
            ["a1"] = new Place { Position = new Vector3(-2.4f, 0, 3.35f), Yaw = 180, Browse = new Vector3(-2.4f, 0, 2.2f), Route = new[] { West, NorthWest } },
            ["a2"] = new Place { Position = new Vector3(-0.6f, 0, 3.35f), Yaw = 180, Browse = new Vector3(-0.6f, 0, 2.2f), Route = new[] { East, NorthEast } },
            ["a3"] = new Place { Position = new Vector3(-0.4f, 0, 0.2f), Yaw = 180, Browse = new Vector3(-0.4f, 0, -0.95f), Route = new Vector3[0] },
            ["a4"] = new Place { Position = new Vector3(-3.35f, 0, -0.6f), Yaw = 90, Browse = new Vector3(-2.2f, 0, -0.6f), Route = new[] { West } },
            ["b1"] = new Place { Position = new Vector3(5.6f, 0, 3.35f), Yaw = 180, Browse = new Vector3(5.6f, 0, 2.2f), Route = new[] { East, ArchWest, ArchEast } },
            ["b2"] = new Place { Position = new Vector3(8.6f, 0, 3.35f), Yaw = 180, Browse = new Vector3(8.6f, 0, 2.2f), Route = new[] { East, ArchWest, ArchEast } },
            ["b3"] = new Place { Position = new Vector3(8.0f, 0, -0.2f), Yaw = 180, Browse = new Vector3(8.0f, 0, -1.35f), Route = new[] { East, ArchWest, ArchEast } },
            ["b4"] = new Place { Position = new Vector3(11.35f, 0, 0.2f), Yaw = -90, Browse = new Vector3(10.2f, 0, 0.2f), Route = new[] { East, ArchWest, ArchEast, UnitBNorth } },
        };

        /// <summary>Queue spots in front of the counter, first in line nearest it.</summary>
        public static readonly Vector3[] QueueSpots = { new Vector3(2.8f, 0, -1.7f), new Vector3(2.8f, 0, -0.95f), new Vector3(2.8f, 0, -0.2f), new Vector3(2.05f, 0, -0.2f), new Vector3(1.3f, 0, -0.2f) };
        public static readonly Vector3 CounterPosition = new Vector3(2.8f, 0, -2.6f);
        public static readonly Vector3 IncubatorPosition = new Vector3(-0.8f, 0, 6.9f);
        public static readonly Dictionary<string, Vector3> GrowerPositions = new Dictionary<string, Vector3> { ["bloom"] = new Vector3(-3.45f, 0, 5.3f), ["flow"] = new Vector3(-3.45f, 0, 7.0f) };
        public static readonly Vector3 WorkbenchPosition = new Vector3(1.55f, 0, 7.7f), BoardPosition = new Vector3(-1.6f, 1.55f, 8.86f), UnitBWallPosition = new Vector3(4f, 0, 0);

        GrowthSession session;
        Material floorWood, floorTile, floorB, wall, wallLab, trim, stand, glass, water, gravel, counterMat, cabinet, paper, marker, partition, sign, dome;
        readonly Dictionary<string, Material> culture = new Dictionary<string, Material>();
        readonly Dictionary<string, TankView> tanks = new Dictionary<string, TankView>();
        readonly Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Transform> growerFill = new Dictionary<string, Transform>();
        readonly Dictionary<int, GrowthVisitorView> visitors = new Dictionary<int, GrowthVisitorView>();
        readonly List<Transform> doseBlobs = new List<Transform>();
        GameObject unitBWall, unitBSign;
        Light unitBLight, incubatorGlow;
        Transform incubatorRoot, tanksRoot, visitorsRoot;
        GrowthCreatureView hatchlingView, highlighted;
        GrowthPhase phase;
        Light sun;

        public IReadOnlyDictionary<string, TankView> Tanks => tanks;
        public IReadOnlyDictionary<int, GrowthVisitorView> VisitorViews => visitors;
        public GrowthCreatureView HatchlingView => hatchlingView;
        public bool UnitBWallStanding => unitBWall != null && unitBWall.activeSelf;

        // ---------------------------------------------------------------- building

        public void Build(GrowthSession owner)
        {
            session = owner;
            floorWood = ShopWorld.MakeMaterial("GR wood floor", new Color(0.72f, 0.58f, 0.42f), 0, 0.3f);
            floorB = ShopWorld.MakeMaterial("GR unit B floor", new Color(0.66f, 0.55f, 0.43f), 0, 0.3f);
            floorTile = ShopWorld.MakeMaterial("GR tile floor", new Color(0.55f, 0.62f, 0.64f), 0, 0.5f);
            wall = ShopWorld.MakeMaterial("GR shop wall", new Color(0.93f, 0.89f, 0.80f), 0, 0.2f);
            wallLab = ShopWorld.MakeMaterial("GR lab wall", new Color(0.80f, 0.87f, 0.86f), 0, 0.2f);
            trim = ShopWorld.MakeMaterial("GR trim", new Color(0.21f, 0.37f, 0.35f), 0, 0.3f);
            stand = ShopWorld.MakeMaterial("GR tank stand", new Color(0.24f, 0.27f, 0.30f), 0, 0.4f);
            glass = Transparent("GR glass", new Color(0.75f, 0.92f, 0.96f, 0.16f));
            water = Transparent("GR water", new Color(0.30f, 0.62f, 0.75f, 0.22f));
            dome = Transparent("GR dome", new Color(0.85f, 0.95f, 0.92f, 0.22f));
            gravel = ShopWorld.MakeMaterial("GR gravel", new Color(0.86f, 0.80f, 0.64f), 0, 0.1f);
            counterMat = ShopWorld.MakeMaterial("GR counter", new Color(0.55f, 0.36f, 0.22f), 0, 0.35f);
            cabinet = ShopWorld.MakeMaterial("GR cabinet", new Color(0.38f, 0.52f, 0.50f), 0, 0.35f);
            paper = ShopWorld.MakeMaterial("GR paper", new Color(0.98f, 0.95f, 0.85f), 0, 0.1f);
            marker = ShopWorld.MakeMaterial("GR place marker", new Color(0.98f, 0.78f, 0.37f), 0, 0.2f);
            partition = ShopWorld.MakeMaterial("GR partition", new Color(0.78f, 0.74f, 0.66f), 0, 0.2f);
            sign = ShopWorld.MakeMaterial("GR sign", new Color(0.84f, 0.38f, 0.27f), 0, 0.2f);
            foreach (var c in GrowthContent.Cultures) culture[c.Id] = ShopWorld.MakeMaterial("GR culture " + c.Id, Hex(c.Hex), 0, 0.6f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.72f, .78f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.58f, .56f, .50f);
            RenderSettings.ambientGroundColor = new Color(.30f, .28f, .25f);
            RenderSettings.fog = false;
            sun = new GameObject("GR soft light").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(58, -30, 0);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            Lamp("Shop unit A lamp", new Vector3(0, 2.3f, 0), 10, 1.25f);
            Lamp("Back room lamp", new Vector3(-1, 2.6f, 6.5f), 8, 1.15f);
            unitBLight = Lamp("Shop unit B lamp", new Vector3(8, 2.3f, 0), 10, 1.25f);

            Rooms();
            Stations();
            tanksRoot = new GameObject("Tanks").transform; tanksRoot.SetParent(transform, false);
            visitorsRoot = new GameObject("Visitors").transform; visitorsRoot.SetParent(transform, false);
            foreach (var place in Places) markers[place.Key] = PlaceMarker(place.Key, place.Value);
        }

        Light Lamp(string name, Vector3 position, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = position;
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = new Color(1f, 0.95f, 0.86f);
            l.shadows = LightShadows.None;
            return l;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Material Transparent(string name, Color color)
        {
            var m = ShopWorld.MakeMaterial(name, color, 0, 0.85f);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
            return m;
        }

        Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool solid = false) =>
            ShopWorld.Shape(parent, name, PrimitiveType.Cube, center, size, material, solid);

        /// <summary>A wall from a to b (on the floor plane) with its height, leaving out the gaps given along it (from, to).</summary>
        void Wall(string name, Vector2 a, Vector2 b, float height, Material material, params Vector2[] gaps)
        {
            var root = new GameObject(name).transform; root.SetParent(transform, false);
            Vector2 dir = (b - a).normalized; float length = Vector2.Distance(a, b);
            var spans = new List<Vector2> { new Vector2(0, length) };
            foreach (var g in gaps)
            {
                var next = new List<Vector2>();
                foreach (var s in spans)
                {
                    if (g.y <= s.x || g.x >= s.y) { next.Add(s); continue; }
                    if (g.x > s.x) next.Add(new Vector2(s.x, g.x));
                    if (g.y < s.y) next.Add(new Vector2(g.y, s.y));
                }
                spans = next;
            }
            foreach (var s in spans)
            {
                Vector2 mid = a + dir * ((s.x + s.y) * 0.5f);
                float len = s.y - s.x;
                var piece = Box(root, "Wall", new Vector3(mid.x, height * 0.5f, mid.y), Mathf.Abs(dir.x) > 0.5f ? new Vector3(len, height, 0.2f) : new Vector3(0.2f, height, len), material, true);
            }
            // Lintels over door gaps keep the room's outline.
            foreach (var g in gaps)
            {
                Vector2 mid = a + dir * ((g.x + g.y) * 0.5f);
                float len = g.y - g.x;
                Box(root, "Lintel", new Vector3(mid.x, height - 0.4f, mid.y), Mathf.Abs(dir.x) > 0.5f ? new Vector3(len, 0.8f, 0.2f) : new Vector3(0.2f, 0.8f, len), material);
            }
        }

        void Rooms()
        {
            var rooms = new GameObject("Rooms").transform; rooms.SetParent(transform, false);
            // Shop unit A, 8 x 8 m, 3.2 m ceiling; the street is to the south (-z).
            Box(rooms, "Unit A floor", new Vector3(0, -0.05f, 0), new Vector3(8, 0.1f, 8), floorWood, true);
            Box(rooms, "Unit A ceiling", new Vector3(0, 3.25f, 0), new Vector3(8, 0.1f, 8), wall);
            Wall("Unit A front wall (entrance and window)", new Vector2(-4, -4), new Vector2(4, -4), 3.2f, wall, new Vector2(0.4f, 2.4f), new Vector2(2.8f, 4.2f));
            Box(rooms, "Window sill", new Vector3(-2.6f, 0.45f, -4), new Vector3(2.0f, 0.9f, 0.22f), wall, true);
            Box(rooms, "Window glass", new Vector3(-2.6f, 1.65f, -4), new Vector3(2.0f, 1.5f, 0.05f), glass, true);
            Wall("Unit A west wall", new Vector2(-4, -4), new Vector2(-4, 4), 3.2f, wall);
            // The north wall rises to the back room's taller ceiling, so no light or void shows over it from the back room.
            Wall("Unit A north wall (back room door)", new Vector2(-4, 4), new Vector2(4, 4), 3.7f, wall, new Vector2(4.2f, 5.4f));
            Wall("Unit A east wall (arch to unit B)", new Vector2(4, -4), new Vector2(4, 4), 3.2f, wall, new Vector2(2.4f, 5.6f));
            Box(rooms, "Skirting", new Vector3(0, 0.05f, -3.88f), new Vector3(8, 0.1f, 0.04f), trim);
            // Back room, 6 x 5 m behind unit A, 3.6 m ceiling for overhead pipes later.
            Box(rooms, "Back room floor", new Vector3(-1, -0.05f, 6.5f), new Vector3(6, 0.1f, 5), floorTile, true);
            Box(rooms, "Back room ceiling", new Vector3(-1, 3.65f, 6.5f), new Vector3(6, 0.1f, 5), wallLab);
            Wall("Back room west wall", new Vector2(-4, 4), new Vector2(-4, 9), 3.6f, wallLab);
            Wall("Back room north wall", new Vector2(-4, 9), new Vector2(2, 9), 3.6f, wallLab);
            Wall("Back room east wall", new Vector2(2, 4), new Vector2(2, 9), 3.6f, wallLab);
            // Shop unit B, 8 x 8 m to the east.
            Box(rooms, "Unit B floor", new Vector3(8, -0.05f, 0), new Vector3(8, 0.1f, 8), floorB, true);
            Box(rooms, "Unit B ceiling", new Vector3(8, 3.25f, 0), new Vector3(8, 0.1f, 8), wall);
            Wall("Unit B north wall", new Vector2(4, 4), new Vector2(12, 4), 3.2f, wall);
            Wall("Unit B east wall", new Vector2(12, -4), new Vector2(12, 4), 3.2f, wall);
            Wall("Unit B front wall", new Vector2(4, -4), new Vector2(12, -4), 3.2f, wall);
            // The street outside the entrance.
            Box(rooms, "Pavement", new Vector3(0, -0.06f, -6.5f), new Vector3(10, 0.1f, 5), ShopWorld.MakeMaterial("GR pavement", new Color(0.55f, 0.56f, 0.55f), 0, 0.2f), true);
            // The temporary partition that closes the arch until unit B is bought.
            unitBWall = new GameObject("Unit B partition").gameObject; unitBWall.transform.SetParent(transform, false);
            var wallBox = Box(unitBWall.transform, "Partition", new Vector3(4, 1.6f, 0), new Vector3(0.12f, 3.2f, 3.2f), partition, true);
            unitBSign = Box(unitBWall.transform, "Coming soon sign", new Vector3(3.92f, 1.6f, 0), new Vector3(0.04f, 0.7f, 1.6f), sign).gameObject;
            Box(unitBWall.transform, "Sign stripe", new Vector3(3.9f, 1.6f, 0), new Vector3(0.04f, 0.12f, 1.2f), paper);
            Target(wallBox.gameObject, GrowthTargetKind.UnitBWall, "unit-b");
        }

        void Stations()
        {
            var root = new GameObject("Stations").transform; root.SetParent(transform, false);
            foreach (var kv in GrowerPositions)
            {
                var g = new GameObject(GrowthShop.Name(kv.Key) + " grower").transform; g.SetParent(root, false);
                g.localPosition = kv.Value; g.localRotation = Quaternion.Euler(0, 90, 0);
                var body = Box(g, "Cabinet", new Vector3(0, 0.5f, 0), new Vector3(0.8f, 1.0f, 0.7f), cabinet, true);
                ShopWorld.Shape(g, "Jar", PrimitiveType.Cylinder, new Vector3(0, 1.3f, 0), new Vector3(0.42f, 0.3f, 0.42f), glass);
                var fill = ShopWorld.Shape(g, "Culture", PrimitiveType.Cylinder, new Vector3(0, 1.05f, 0), new Vector3(0.36f, 0.25f, 0.36f), culture[kv.Key]);
                Box(g, "Label", new Vector3(0, 0.75f, 0.36f), new Vector3(0.5f, 0.18f, 0.02f), culture[kv.Key]);
                growerFill[kv.Key] = fill;
                Target(g.gameObject, GrowthTargetKind.Grower, kv.Key);
                AddCollider(g.gameObject, new Vector3(0, 0.8f, 0), new Vector3(0.85f, 1.6f, 0.75f));
            }
            incubatorRoot = new GameObject("Incubator").transform; incubatorRoot.SetParent(root, false);
            incubatorRoot.localPosition = IncubatorPosition;
            Box(incubatorRoot, "Base", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.7f), cabinet, true);
            ShopWorld.Shape(incubatorRoot, "Dome", PrimitiveType.Sphere, new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.45f, 0.55f), dome);
            ShopWorld.Shape(incubatorRoot, "Tray", PrimitiveType.Cylinder, new Vector3(0, 0.91f, -0.22f), new Vector3(0.3f, 0.02f, 0.2f), paper);
            for (int i = 0; i < 6; i++)
            {
                var blob = ShopWorld.Shape(incubatorRoot, "Dose " + i, PrimitiveType.Sphere, new Vector3(-0.15f + 0.06f * i, 0.98f, 0.05f), Vector3.one * 0.05f, culture["bloom"]);
                blob.gameObject.SetActive(false);
                doseBlobs.Add(blob);
            }
            incubatorGlow = new GameObject("Incubator glow").AddComponent<Light>();
            incubatorGlow.transform.SetParent(incubatorRoot, false);
            incubatorGlow.transform.localPosition = new Vector3(0, 1.1f, -0.3f);
            incubatorGlow.type = LightType.Point; incubatorGlow.range = 1.6f; incubatorGlow.intensity = 0; incubatorGlow.color = new Color(1f, 0.85f, 0.5f);
            Target(incubatorRoot.gameObject, GrowthTargetKind.Incubator, "incubator");
            AddCollider(incubatorRoot.gameObject, new Vector3(0, 0.7f, 0), new Vector3(0.95f, 1.4f, 0.8f));

            var counter = new GameObject("Counter").transform; counter.SetParent(root, false);
            counter.localPosition = CounterPosition;
            Box(counter, "Counter body", new Vector3(0, 0.5f, 0), new Vector3(2.0f, 1.0f, 0.6f), counterMat, true);
            Box(counter, "Counter top", new Vector3(0, 1.02f, 0), new Vector3(2.1f, 0.05f, 0.7f), trim);
            ShopWorld.Shape(counter, "Bell", PrimitiveType.Cylinder, new Vector3(0.6f, 1.08f, 0.1f), new Vector3(0.1f, 0.03f, 0.1f), marker);
            Target(counter.gameObject, GrowthTargetKind.Counter, "counter");
            AddCollider(counter.gameObject, new Vector3(0, 0.6f, 0), new Vector3(2.1f, 1.2f, 0.75f));

            var bench = new GameObject("Tool bench").transform; bench.SetParent(root, false);
            bench.localPosition = WorkbenchPosition; bench.localRotation = Quaternion.Euler(0, -90, 0);
            Box(bench, "Bench top", new Vector3(0, 0.88f, 0), new Vector3(1.3f, 0.06f, 0.6f), counterMat, true);
            Box(bench, "Bench legs", new Vector3(0, 0.43f, 0), new Vector3(1.2f, 0.84f, 0.5f), stand, true);
            ShopWorld.Shape(bench, "Siphon bulb", PrimitiveType.Capsule, new Vector3(0.2f, 1.0f, 0), new Vector3(0.12f, 0.1f, 0.12f), culture["flow"]);
            Target(bench.gameObject, GrowthTargetKind.Workbench, "workbench");
            AddCollider(bench.gameObject, new Vector3(0, 0.6f, 0), new Vector3(1.35f, 1.2f, 0.65f));

            var board = new GameObject("Pinboard").transform; board.SetParent(root, false);
            board.localPosition = BoardPosition;
            Box(board, "Board", Vector3.zero, new Vector3(1.4f, 0.9f, 0.05f), counterMat, true);
            for (int i = 0; i < 3; i++) Box(board, "Note " + i, new Vector3(-0.4f + 0.4f * i, 0.05f * (i % 2), -0.03f), new Vector3(0.3f, 0.36f, 0.01f), paper);
            Target(board.gameObject, GrowthTargetKind.Board, "board");
        }

        static void AddCollider(GameObject g, Vector3 center, Vector3 size)
        {
            var c = g.AddComponent<BoxCollider>();
            c.center = center; c.size = size;
        }

        static GrowthTarget Target(GameObject g, GrowthTargetKind kind, string id)
        {
            var t = g.AddComponent<GrowthTarget>();
            t.Kind = kind; t.Id = id;
            return t;
        }

        GameObject PlaceMarker(string id, Place place)
        {
            var root = new GameObject("Place " + id);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = place.Position;
            root.transform.localRotation = Quaternion.Euler(0, place.Yaw, 0);
            // A dashed outline of the tank's footprint, and a plus sign to buy one.
            for (int i = 0; i < 6; i++)
            {
                float x = -0.6f + 0.24f * i;
                Box(root.transform, "Dash", new Vector3(x, 0.01f, 0.48f), new Vector3(0.12f, 0.01f, 0.04f), marker);
                Box(root.transform, "Dash", new Vector3(x, 0.01f, -0.48f), new Vector3(0.12f, 0.01f, 0.04f), marker);
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -0.36f + 0.24f * i;
                Box(root.transform, "Dash", new Vector3(0.73f, 0.01f, z), new Vector3(0.04f, 0.01f, 0.12f), marker);
                Box(root.transform, "Dash", new Vector3(-0.73f, 0.01f, z), new Vector3(0.04f, 0.01f, 0.12f), marker);
            }
            Box(root.transform, "Plus", new Vector3(0, 0.45f, 0), new Vector3(0.3f, 0.07f, 0.07f), marker);
            Box(root.transform, "Plus", new Vector3(0, 0.45f, 0), new Vector3(0.07f, 0.3f, 0.07f), marker);
            Target(root, GrowthTargetKind.Place, id);
            AddCollider(root, new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 1.0f));
            root.SetActive(false);
            return root;
        }

        // ---------------------------------------------------------------- following the shop

        public void ResetForNewShop()
        {
            foreach (var t in tanks.Values) Destroy(t.Root.gameObject);
            tanks.Clear();
            foreach (var v in visitors.Values) Destroy(v.gameObject);
            visitors.Clear();
            if (hatchlingView) Destroy(hatchlingView.gameObject);
            hatchlingView = null;
        }

        public void SetPhase(GrowthPhase p) => phase = p;

        public void Sync(GrowthShop shop, float dt)
        {
            phase = shop.Phase;
            // Tanks follow the shop's containers; each keeps its creatures in step with its occupants.
            foreach (var c in shop.ShopContainers)
            {
                if (!tanks.TryGetValue(c.Id, out var view)) tanks[c.Id] = view = BuildTank(c);
                view.Sync(c, this);
            }
            foreach (var id in tanks.Keys.Where(k => shop.Container(k) == null).ToList()) { Destroy(tanks[id].Root.gameObject); tanks.Remove(id); }
            var free = new HashSet<string>(shop.FreePlaces);
            foreach (var m in markers) if (m.Value.activeSelf != free.Contains(m.Key)) m.Value.SetActive(free.Contains(m.Key));
            if (unitBWall.activeSelf == shop.UnitB) unitBWall.SetActive(!shop.UnitB);
            unitBLight.enabled = shop.UnitB;
            foreach (var kv in growerFill)
            {
                float f = shop.GrowerCapacity == 0 ? 0 : Mathf.Clamp01(shop.GrowerUnits[kv.Key] / shop.GrowerCapacity);
                kv.Value.localScale = new Vector3(0.36f, Mathf.Max(0.01f, 0.25f * f), 0.36f);
                kv.Value.localPosition = new Vector3(0, 1.0f + 0.25f * f, 0);
            }
            for (int i = 0; i < doseBlobs.Count; i++)
            {
                bool on = i < shop.Chamber.Count;
                if (doseBlobs[i].gameObject.activeSelf != on) doseBlobs[i].gameObject.SetActive(on);
                if (on) doseBlobs[i].GetComponent<Renderer>().sharedMaterial = culture[shop.Chamber[i]];
            }
            incubatorGlow.intensity = shop.Incubating ? 0.45f : shop.Hatchling != null ? 0.3f : 0;
            if (shop.Hatchling != null && (hatchlingView == null || hatchlingView.SpecimenId != shop.Hatchling.Id))
            {
                if (hatchlingView) Destroy(hatchlingView.gameObject);
                hatchlingView = GrowthCreatureView.Create(shop.Hatchling, incubatorRoot);
                hatchlingView.transform.localPosition = new Vector3(0, 0.98f, -0.22f);
            }
            else if (shop.Hatchling == null && hatchlingView) { Destroy(hatchlingView.gameObject); hatchlingView = null; }
            SyncVisitors(shop);
        }

        TankView BuildTank(GrowthContainer c)
        {
            var place = Places[c.Place];
            var root = new GameObject("Tank " + c.Id + " at " + c.Place).transform;
            root.SetParent(tanksRoot, false);
            root.localPosition = place.Position;
            root.localRotation = Quaternion.Euler(0, place.Yaw, 0);
            Box(root, "Stand", new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 1.0f), stand, true);
            Box(root, "Gravel", new Vector3(0, 0.93f, 0), new Vector3(1.44f, 0.05f, 0.94f), gravel);
            Box(root, "Water", new Vector3(0, 1.18f, 0), new Vector3(1.44f, 0.5f, 0.94f), water);
            Box(root, "Glass", new Vector3(0, 1.21f, 0), new Vector3(1.5f, 0.62f, 1.0f), glass);
            Box(root, "Rim", new Vector3(0, 1.53f, 0), new Vector3(1.52f, 0.03f, 1.02f), trim);
            Box(root, "Plant", new Vector3(0.55f, 1.05f, 0.3f), new Vector3(0.05f, 0.2f, 0.05f), ShopWorld.MakeMaterial("GR plant", new Color(0.35f, 0.62f, 0.38f), 0, 0.3f));
            var target = Target(root.gameObject, GrowthTargetKind.Tank, c.Id);
            AddCollider(root.gameObject, new Vector3(0, 0.77f, 0), new Vector3(1.5f, 1.54f, 1.0f));
            return new TankView { Root = root, ContainerId = c.Id, Place = c.Place };
        }

        // ---------------------------------------------------------------- peek and creatures

        /// <summary>How far the eye is from the container's glass (0 inside), on the floor plane.</summary>
        public float DistanceToGlass(string containerId, Vector3 eye)
        {
            if (!tanks.TryGetValue(containerId, out var view)) return float.MaxValue;
            Vector3 local = view.Root.InverseTransformPoint(eye);
            float dx = Mathf.Max(0, Mathf.Abs(local.x) - 0.75f), dz = Mathf.Max(0, Mathf.Abs(local.z) - 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The creature in a container nearest the screen's centre, within a small radius.</summary>
        public GrowthCreatureView CreatureNearCentre(string containerId, Camera camera)
        {
            if (!tanks.TryGetValue(containerId, out var view)) return null;
            GrowthCreatureView best = null; float bestDistance = 0.07f;
            foreach (var creature in view.Creatures.Values)
            {
                var p = camera.WorldToViewportPoint(creature.transform.position);
                if (p.z <= 0) continue;
                float d = new Vector2(p.x - 0.5f, (p.y - 0.5f) * camera.pixelHeight / Mathf.Max(1, camera.pixelWidth)).magnitude;
                if (d < bestDistance) { best = creature; bestDistance = d; }
            }
            return best;
        }

        public void Highlight(GrowthCreatureView creature)
        {
            if (highlighted == creature) return;
            if (highlighted) highlighted.Highlighted = false;
            highlighted = creature;
            if (highlighted) highlighted.Highlighted = true;
        }

        public void Animate(float t, float dt)
        {
            foreach (var tank in tanks.Values) tank.Animate(t, session.ReducedMotion);
            if (hatchlingView) { hatchlingView.transform.localRotation = Quaternion.Euler(0, t * 40f, 0); hatchlingView.Tick(t); }
            foreach (var v in visitors.Values) v.Tick(dt);
        }

        // ---------------------------------------------------------------- visitors

        void SyncVisitors(GrowthShop shop)
        {
            var queue = shop.Queue.OrderByDescending(v => v.Clock).ThenBy(v => v.Id).Select(v => v.Id).ToList();
            foreach (var v in shop.Visitors)
            {
                if (!visitors.TryGetValue(v.Id, out var view))
                {
                    view = GrowthVisitorView.Create(v, visitorsRoot);
                    view.transform.position = Outside;
                    visitors[v.Id] = view;
                }
                view.Follow(v, shop, queue.IndexOf(v.Id));
            }
            foreach (var id in visitors.Keys.Where(k => shop.Visitors.All(v => v.Id != k)).ToList()) { Destroy(visitors[id].gameObject); visitors.Remove(id); }
        }

        /// <summary>The walk from the door to a container's browse spot.</summary>
        public static List<Vector3> RouteTo(string place)
        {
            var r = new List<Vector3> { Door, Hub };
            if (place != null && Places.TryGetValue(place, out var p)) { r.AddRange(p.Route); r.Add(p.Browse); }
            return r;
        }

        public static List<Vector3> RouteToQueue(string fromPlace, int index)
        {
            var r = new List<Vector3>();
            if (fromPlace != null && Places.TryGetValue(fromPlace, out var p)) r.AddRange(p.Route.Reverse());
            r.Add(East);
            r.Add(QueueSpots[Mathf.Clamp(index, 0, QueueSpots.Length - 1)]);
            return r;
        }

        public static List<Vector3> RouteOut(string fromPlace, bool fromQueue)
        {
            var r = new List<Vector3>();
            if (fromQueue) r.Add(East);
            else if (fromPlace != null && Places.TryGetValue(fromPlace, out var p)) r.AddRange(p.Route.Reverse());
            r.Add(Hub); r.Add(Door); r.Add(Outside);
            return r;
        }

        /// <summary>A tank in the world and the creatures swimming in it (GR.5's bounded swim; schools move together).</summary>
        public sealed class TankView
        {
            public Transform Root;
            public string ContainerId, Place;
            public readonly Dictionary<int, GrowthCreatureView> Creatures = new Dictionary<int, GrowthCreatureView>();
            readonly Dictionary<string, int> speciesCount = new Dictionary<string, int>();

            public void Sync(GrowthContainer c, GrowthWorld world)
            {
                foreach (var s in c.Occupants)
                    if (!Creatures.ContainsKey(s.Id)) Creatures[s.Id] = GrowthCreatureView.Create(s, Root);
                foreach (var id in Creatures.Keys.Where(k => c.Occupants.All(o => o.Id != k)).ToList()) { Object.Destroy(Creatures[id].gameObject); Creatures.Remove(id); }
                speciesCount.Clear();
                foreach (var s in c.Occupants)
                {
                    speciesCount[s.Species] = speciesCount.TryGetValue(s.Species, out int n) ? n + 1 : 1;
                    Creatures[s.Id].Reserved = s.ReservedBy != 0;
                }
            }

            public bool InSchool(string species) => speciesCount.TryGetValue(species, out int n) && n >= GrowthShop.SchoolSize;

            public void Animate(float t, bool reduced)
            {
                var rank = new Dictionary<string, int>();
                foreach (var creature in Creatures.Values.OrderBy(c => c.SpecimenId))
                {
                    bool school = InSchool(creature.Species);
                    int k = rank.TryGetValue(creature.Species, out int r) ? r : 0;
                    rank[creature.Species] = k + 1;
                    float speed = reduced ? 0.5f : 1f;
                    // A school shares its leader's path and keeps a loose formation behind it; others wander alone.
                    float phase = school ? (creature.Species.GetHashCode() & 1023) * 0.01f : creature.SpecimenId * 1.37f;
                    float time = t * speed;
                    Vector3 Path(float tt) => new Vector3(0.48f * Mathf.Sin(0.31f * tt + phase), 0.07f * Mathf.Sin(0.57f * tt + phase * 2f), 0.22f * Mathf.Sin(0.63f * tt + phase * 0.7f));
                    Vector3 pos = Path(time), ahead = Path(time + 0.2f);
                    if (school)
                    {
                        Vector3 back = (pos - ahead).normalized;
                        Vector3 side = Vector3.Cross(Vector3.up, back).normalized;
                        float row = Mathf.Floor((k + 1) / 2f), lane = k == 0 ? 0 : (k % 2 == 0 ? 1 : -1);
                        pos += back * (0.09f * row) + side * (0.065f * lane * Mathf.Max(1, row * 0.6f)) + Vector3.up * (0.02f * Mathf.Sin(t * 2.1f + k));
                        ahead += back * (0.09f * row) + side * (0.065f * lane * Mathf.Max(1, row * 0.6f));
                    }
                    pos.x = Mathf.Clamp(pos.x, -0.62f, 0.62f); pos.z = Mathf.Clamp(pos.z, -0.36f, 0.36f); pos.y = Mathf.Clamp(pos.y, -0.18f, 0.18f);
                    creature.transform.localPosition = new Vector3(pos.x, 1.18f + pos.y, pos.z);
                    Vector3 dir = ahead - pos; dir.y *= 0.3f;
                    if (dir.sqrMagnitude > 1e-6f) creature.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    creature.Tick(t);
                }
            }
        }
    }
}
