using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public enum GrowthTargetKind { Grower, Incubator, Tank, Place, Counter, Workbench, UnitBWall, Board, Catalog, Vial, Breeding, AnnexWall, Generator, PowerBoard, Buffer, Pump, WorkshopWall, Sorter, StaffBoard, Helper, UnitCWall, HallBoard, Award }

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
    public sealed partial class GrowthWorld : MonoBehaviour
    {
        public static readonly Vector3 Spawn = new Vector3(0.6f, 0.05f, 5.4f);
        public const float FullDistance = 6f, FreezeDistance = 12f;
        /// <summary>GR-12: kit creatures show their full rig (face, blinks, tricks) within this distance and in view; further, their
        /// shared baked look (with its outline to FullDistance, without beyond). Peek is within 1.6 m of the glass.</summary>
        public const float KitDistance = 3.5f;

        /// <summary>A container place: where the tank stands, which way its front faces, and where a visitor stands to browse it.</summary>
        public struct Place
        {
            public Vector3 Position, Browse;
            public float Yaw, Width, Depth;
            public Vector3[] Route;
        }

        static Vector3 V((float x, float z) p) => new Vector3(p.x, 0, p.z);
        static Vector3 Point(string name) => V(GrowthSpaces.Points[name]);
        static readonly Vector3 Outside = Point("Outside"), Door = Point("Door"), Hub = Point("Hub"), East = Point("East");

        /// <summary>
        /// The container places (GR.11), from GrowthSpaces (GR-10): long places along walls and on islands take Starter and
        /// Large tanks, standard ones a Starter tank, feature places a Show tank (unit B's east wall, unit C's feature wall);
        /// unit C adds six and the exhibition hall finish two. Sizes come from GrowthShop.Places; each tank stands centred on
        /// its place with its front (local +z) to the aisle, and visitors walk to its browse spot along its route.
        /// </summary>
        public static readonly Dictionary<string, Place> Places = GrowthSpaces.Spots.ToDictionary(s => s.Id, s =>
        {
            var spot = GrowthShop.Place(s.Id);
            return new Place { Position = V(s.Centre), Yaw = s.Yaw, Browse = V(s.Browse), Route = s.Route.Select(Point).ToArray(), Width = spot.Width, Depth = spot.Depth };
        });

        /// <summary>Queue spots in front of the counter, first in line nearest it.</summary>
        public static readonly Vector3[] QueueSpots = GrowthSpaces.QueueSpots.Select(V).ToArray();
        public static readonly Vector3 CounterPosition = new Vector3(2.8f, 0, -2.6f);
        public static readonly Vector3 IncubatorPosition = new Vector3(GrowthLab.Incubator.x, 0, GrowthLab.Incubator.z);
        /// <summary>
        /// GR-04: a slot in the back room for each culture's source: Bloom, Flow, Ember and Brine growers along the west wall
        /// (facing east), the Spark grower, the Mist vat and the Dusk grower along the north wall (facing south). A slot shows
        /// as a dashed square with the culture's colour until its source arrives.
        /// </summary>
        public static readonly Dictionary<string, Vector3> GrowerPositions = GrowthLab.Growers.ToDictionary(kv => kv.Key, kv => new Vector3(kv.Value.x, 0, kv.Value.z));
        public static float GrowerYaw(string culture) => GrowerPositions[culture].z > 8.2f ? 180 : 90;
        /// <summary>Where a player stands to use a source: 1.3 m in front of it.</summary>
        public static Vector3 GrowerFront(string culture) => GrowerPositions[culture] + Quaternion.Euler(0, GrowerYaw(culture), 0) * new Vector3(0, 0, 1.3f);
        /// <summary>GR-07 moved the research bench 0.8 m south, clear of the doorway into the lab annex.</summary>
        public static readonly Vector3 WorkbenchPosition = new Vector3(1.55f, 0, 6.9f), BoardPosition = new Vector3(1.86f, 1.55f, 5.6f), UnitBWallPosition = new Vector3(4f, 0, 0);
        /// <summary>GR-04: the vial shelf on the back room's south wall, and the supplier catalog on its stand beside the counter.
        /// GR-05 moved the shelf 0.2 m west to make room for the breeding tank.</summary>
        public static readonly Vector3 VialShelfPosition = new Vector3(-1.9f, 0, 4.25f), CatalogPosition = new Vector3(1.2f, 0, -3.3f);
        /// <summary>GR-05: the breeding tank against the back room's south wall, west of the door, facing the incubator (GR.7.2).
        /// Its glass is 1.06 m wide: the pair's compartment, and a nursery behind a divider at its east end.</summary>
        public static readonly Vector3 BreedingPosition = new Vector3(-0.5f, 0, 4.42f);
        public const float BreedingWaterY = 1.0f, BreedingDivider = 0.22f, SecondPairDivider = -0.145f;

        GrowthSession session;
        Material floorWood, floorTile, floorB, wall, wallLab, trim, stand, glass, water, waterBrine, waterWarm, gravel, counterMat, cabinet, paper, marker, partition, sign, dome, harmony, lightBar;
        /// <summary>GR.5's update levels in the last frame: full, cheaper (beyond 6 m or out of view) and frozen (beyond 12 m).</summary>
        public readonly int[] DetailCounts = new int[3];
        readonly Dictionary<string, Material> culture = new Dictionary<string, Material>();
        readonly Dictionary<string, TankView> tanks = new Dictionary<string, TankView>();
        readonly Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();
        /// <summary>Each owned culture's source: its fill (how full it is) and the slot marker it replaced.</summary>
        sealed class Source { public Transform Root, Fill; public float BaseY, Height, Width; }
        readonly Dictionary<string, Source> sources = new Dictionary<string, Source>();
        readonly Dictionary<string, GameObject> slots = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Transform> vials = new Dictionary<string, Transform>();
        Transform stationsRoot;
        /// <summary>GR-05: the breeding tank (built once bought, again when its level changes), its dashed slot until then,
        /// its water (tinted by the culture in it) and the creatures in it by specimen id.</summary>
        GameObject breedingSlot, breedingTank;
        int breedingLevel;
        Renderer breedingWater;
        readonly Dictionary<int, GrowthCreatureView> breedingViews = new Dictionary<int, GrowthCreatureView>();
        public IReadOnlyDictionary<int, GrowthCreatureView> BreedingViews => breedingViews;
        public bool BreedingTankStanding => breedingTank != null;
        readonly Dictionary<int, GrowthVisitorView> visitors = new Dictionary<int, GrowthVisitorView>();
        readonly List<Transform> doseBlobs = new List<Transform>();
        GameObject unitBWall, unitBSign;
        Light unitBLight, incubatorGlow;
        Transform incubatorRoot, tanksRoot, visitorsRoot;
        GrowthCreatureView highlighted;
        /// <summary>GR-06: the incubator's clutch on its tray (one to three).</summary>
        readonly List<GrowthCreatureView> hatchlingViews = new List<GrowthCreatureView>();
        /// <summary>GR-06: each station's level parts (its placeholder look by level), rebuilt when the level changes.</summary>
        readonly Dictionary<string, int> builtLevels = new Dictionary<string, int>();
        readonly Dictionary<string, Transform> levelParts = new Dictionary<string, Transform>();
        Transform benchRoot, counterRoot;
        Renderer incubatorDome;
        readonly Dictionary<string, Material> tintedDome = new Dictionary<string, Material>();
        GrowthPhase phase;
        Light sun;

        public IReadOnlyDictionary<string, TankView> Tanks => tanks;
        public IReadOnlyDictionary<int, GrowthVisitorView> VisitorViews => visitors;
        public GrowthCreatureView HatchlingView => hatchlingViews.Count > 0 ? hatchlingViews[0] : null;
        public IReadOnlyList<GrowthCreatureView> HatchlingViews => hatchlingViews;
        /// <summary>GR-06 (validation): the level each station's look was last built at.</summary>
        public int LookLevel(string key) => builtLevels.TryGetValue(key, out int lv) ? lv : 0;
        public bool UnitBWallStanding => Standing("unit-b");

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
            waterBrine = Transparent("GR brine water", new Color(0.22f, 0.66f, 0.56f, 0.26f));
            waterWarm = Transparent("GR warm water", new Color(0.80f, 0.60f, 0.32f, 0.22f));
            harmony = Glow("GR harmony glow", new Color(0.55f, 0.92f, 0.70f), 0.7f);
            lightBar = Glow("GR show light bar", new Color(0.95f, 0.88f, 0.72f), 0.3f);
            dome = Transparent("GR dome", new Color(0.85f, 0.95f, 0.92f, 0.22f));
            gravel = ShopWorld.MakeMaterial("GR gravel", new Color(0.86f, 0.80f, 0.64f), 0, 0.1f);
            counterMat = ShopWorld.MakeMaterial("GR counter", new Color(0.55f, 0.36f, 0.22f), 0, 0.35f);
            cabinet = ShopWorld.MakeMaterial("GR cabinet", new Color(0.38f, 0.52f, 0.50f), 0, 0.35f);
            paper = ShopWorld.MakeMaterial("GR paper", new Color(0.98f, 0.95f, 0.85f), 0, 0.1f);
            marker = ShopWorld.MakeMaterial("GR place marker", new Color(0.98f, 0.78f, 0.37f), 0, 0.2f);
            partition = ShopWorld.MakeMaterial("GR partition", new Color(0.78f, 0.74f, 0.66f), 0, 0.2f);
            sign = ShopWorld.MakeMaterial("GR sign", new Color(0.84f, 0.38f, 0.27f), 0, 0.2f);
            foreach (var c in GrowthContent.Cultures) culture[c.Id] = ShopWorld.MakeMaterial("GR culture " + c.Id, Hex(c.Hex), 0, 0.6f);
            // GR-16: the look's palette for everything built from here on (GrowthWorldLook).
            if (LookOn) RestyleForLook();

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
            AnnexRoom();
            LabStations();
            WorkshopRoom();
            LineStations();
            StaffBoard();
            // GR-10: the rooms' openings (their partitions come down when bought), unit C, the set pieces and the hall finish.
            SpaceMaterials();
            AddOpening("unit-b", unitBWall, 3.25f, unitBLight);
            AddOpening("annex", annexWall, 2.85f, annexLight);
            AddOpening("workshop", workshopWall, 2.85f, workshopLight);
            UnitCRoom();
            SetPieces();
            HallFinish();
            ExhibitionPieces();
            tanksRoot = new GameObject("Tanks").transform; tanksRoot.SetParent(transform, false);
            visitorsRoot = new GameObject("Visitors").transform; visitorsRoot.SetParent(transform, false);
            foreach (var place in Places) markers[place.Key] = PlaceMarker(place.Key, place.Value);
            // GR-16: the dressed rooms and stations, and the look's light (GrowthWorldLook).
            if (LookOn) { DressRooms(); DressStations(); DressSpaces(); BuildLookLight(); }
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

        /// <summary>A softly glowing material, kept under the bloom threshold (AD's rule for lit surfaces).</summary>
        static Material Glow(string name, Color color, float strength)
        {
            var m = ShopWorld.MakeMaterial(name, color, 0, 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * strength);
            return m;
        }

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
            // GR-07: a doorway into the lab annex, closed by a partition until the annex opens (GrowthWorldLab).
            Wall("Back room north wall", new Vector2(-4, 9), new Vector2(2, 9), 3.6f, wallLab, new Vector2(4 + AnnexDoorFrom, 4 + AnnexDoorTo));
            Wall("Back room east wall", new Vector2(2, 4), new Vector2(2, 9), 3.6f, wallLab);
            // Shop unit B, 8 x 8 m to the east.
            Box(rooms, "Unit B floor", new Vector3(8, -0.05f, 0), new Vector3(8, 0.1f, 8), floorB, true);
            Box(rooms, "Unit B ceiling", new Vector3(8, 3.25f, 0), new Vector3(8, 0.1f, 8), wall);
            // GR-10: an arch at the north wall's east end into unit C, and the bay window in the front wall (GrowthWorldSpaces).
            Wall("Unit B north wall (arch to unit C)", new Vector2(4, 4), new Vector2(12, 4), 3.2f, wall, new Vector2(5.9f, 7.85f));
            Wall("Unit B east wall", new Vector2(12, -4), new Vector2(12, 4), 3.2f, wall);
            Wall("Unit B front wall (bay window)", new Vector2(4, -4), new Vector2(12, -4), 3.2f, wall, new Vector2(3.1f, 4.9f));
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
            var root = stationsRoot = new GameObject("Stations").transform; root.SetParent(transform, false);
            // A slot for each culture's source; the source itself is built when the culture is owned (Sync).
            foreach (var kv in GrowerPositions)
            {
                var slot = new GameObject("Slot for " + GrowthShop.Name(kv.Key)).transform; slot.SetParent(root, false);
                slot.localPosition = kv.Value; slot.localRotation = Quaternion.Euler(0, GrowerYaw(kv.Key), 0);
                for (int i = 0; i < 4; i++)
                {
                    float a = -0.3f + 0.2f * i;
                    Box(slot, "Dash", new Vector3(a, 0.01f, 0.36f), new Vector3(0.1f, 0.01f, 0.03f), marker);
                    Box(slot, "Dash", new Vector3(a, 0.01f, -0.36f), new Vector3(0.1f, 0.01f, 0.03f), marker);
                    Box(slot, "Dash", new Vector3(0.41f, 0.01f, a * 0.8f), new Vector3(0.03f, 0.01f, 0.1f), marker);
                    Box(slot, "Dash", new Vector3(-0.41f, 0.01f, a * 0.8f), new Vector3(0.03f, 0.01f, 0.1f), marker);
                }
                Box(slot, "Culture swatch", new Vector3(0, 0.02f, 0), new Vector3(0.22f, 0.02f, 0.22f), culture[kv.Key]);
                slots[kv.Key] = slot.gameObject;
            }
            // The vial shelf: a bottle for each culture a starter vial can bring, shown while it holds doses.
            var shelf = new GameObject("Vial shelf").transform; shelf.SetParent(root, false);
            shelf.localPosition = VialShelfPosition;
            Box(shelf, "Shelf", new Vector3(0, 0.95f, 0.05f), new Vector3(1.4f, 0.05f, 0.3f), counterMat, true);
            Box(shelf, "Bracket", new Vector3(-0.6f, 0.85f, 0.0f), new Vector3(0.05f, 0.18f, 0.2f), stand);
            Box(shelf, "Bracket", new Vector3(0.6f, 0.85f, 0.0f), new Vector3(0.05f, 0.18f, 0.2f), stand);
            int vialIndex = 0;
            foreach (var c in GrowthContent.Cultures)
            {
                if (c.VialCredits <= 0) continue;
                var bottle = new GameObject(c.Name + " vial").transform; bottle.SetParent(shelf, false);
                bottle.localPosition = new Vector3(-0.5f + 0.25f * vialIndex++, 0.98f, 0.06f);
                ShopWorld.Shape(bottle, "Glass", PrimitiveType.Cylinder, new Vector3(0, 0.11f, 0), new Vector3(0.1f, 0.11f, 0.1f), glass);
                ShopWorld.Shape(bottle, "Cork", PrimitiveType.Cylinder, new Vector3(0, 0.24f, 0), new Vector3(0.06f, 0.025f, 0.06f), counterMat);
                ShopWorld.Shape(bottle, "Culture", PrimitiveType.Cylinder, new Vector3(0, 0.08f, 0), new Vector3(0.085f, 0.08f, 0.085f), culture[c.Id]);
                Target(bottle.gameObject, GrowthTargetKind.Vial, c.Id);
                AddCollider(bottle.gameObject, new Vector3(0, 0.13f, 0), new Vector3(0.2f, 0.3f, 0.2f));
                bottle.gameObject.SetActive(false);
                vials[c.Id] = bottle;
            }
            // GR-05: the breeding tank's dashed slot; focusing it opens its card, where it is bought (era 2).
            var bslot = new GameObject("Slot for the breeding tank").transform; bslot.SetParent(root, false);
            bslot.localPosition = BreedingPosition;
            for (int i = 0; i < 5; i++)
            {
                float a = -0.48f + 0.24f * i;
                Box(bslot, "Dash", new Vector3(a, 0.01f, 0.25f), new Vector3(0.14f, 0.01f, 0.03f), marker);
                Box(bslot, "Dash", new Vector3(a, 0.01f, -0.25f), new Vector3(0.14f, 0.01f, 0.03f), marker);
            }
            foreach (int side in new[] { -1, 1 }) Box(bslot, "Dash", new Vector3(side * 0.55f, 0.01f, 0), new Vector3(0.03f, 0.01f, 0.3f), marker);
            Box(bslot, "Sign", new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.02f, 0.2f), paper);
            Target(bslot.gameObject, GrowthTargetKind.Breeding, "breeding");
            var slotCollider = bslot.gameObject.AddComponent<BoxCollider>();
            slotCollider.center = new Vector3(0, 0.45f, 0); slotCollider.size = new Vector3(1.1f, 0.9f, 0.5f); slotCollider.isTrigger = true; // focus, not a wall
            breedingSlot = bslot.gameObject;
            // The supplier catalog on its stand by the counter (GR.6).
            var catalog = new GameObject("Supplier catalog").transform; catalog.SetParent(root, false);
            catalog.localPosition = CatalogPosition; // facing into the shop (+z), its book tilted toward the reader
            Box(catalog, "Stand", new Vector3(0, 0.5f, 0), new Vector3(0.45f, 1.0f, 0.35f), counterMat, true);
            var book = Box(catalog, "Book", new Vector3(0, 1.06f, 0), new Vector3(0.4f, 0.06f, 0.3f), sign);
            book.localRotation = Quaternion.Euler(12, 0, 0);
            Box(catalog, "Pages", new Vector3(0, 1.1f, 0.01f), new Vector3(0.36f, 0.02f, 0.26f), paper).localRotation = Quaternion.Euler(12, 0, 0);
            Target(catalog.gameObject, GrowthTargetKind.Catalog, "catalog");
            AddCollider(catalog.gameObject, new Vector3(0, 0.6f, 0), new Vector3(0.5f, 1.2f, 0.4f));
            incubatorRoot = new GameObject("Incubator").transform; incubatorRoot.SetParent(root, false);
            incubatorRoot.localPosition = IncubatorPosition;
            Box(incubatorRoot, "Base", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.7f), cabinet, true);
            incubatorDome = ShopWorld.Shape(incubatorRoot, "Dome", PrimitiveType.Sphere, new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.45f, 0.55f), dome).GetComponent<Renderer>();
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

            var counter = counterRoot = new GameObject("Counter").transform; counter.SetParent(root, false);
            counter.localPosition = CounterPosition;
            Box(counter, "Counter body", new Vector3(0, 0.5f, 0), new Vector3(2.0f, 1.0f, 0.6f), counterMat, true);
            Box(counter, "Counter top", new Vector3(0, 1.02f, 0), new Vector3(2.1f, 0.05f, 0.7f), trim);
            ShopWorld.Shape(counter, "Bell", PrimitiveType.Cylinder, new Vector3(0.6f, 1.08f, 0.1f), new Vector3(0.1f, 0.03f, 0.1f), marker);
            Target(counter.gameObject, GrowthTargetKind.Counter, "counter");
            AddCollider(counter.gameObject, new Vector3(0, 0.6f, 0), new Vector3(2.1f, 1.2f, 0.75f));

            var bench = benchRoot = new GameObject("Research bench").transform; bench.SetParent(root, false);
            bench.localPosition = WorkbenchPosition; bench.localRotation = Quaternion.Euler(0, -90, 0);
            Box(bench, "Bench top", new Vector3(0, 0.88f, 0), new Vector3(1.3f, 0.06f, 0.6f), counterMat, true);
            Box(bench, "Bench legs", new Vector3(0, 0.43f, 0), new Vector3(1.2f, 0.84f, 0.5f), stand, true);
            ShopWorld.Shape(bench, "Siphon bulb", PrimitiveType.Capsule, new Vector3(0.2f, 1.0f, 0), new Vector3(0.12f, 0.1f, 0.12f), culture["flow"]);
            Target(bench.gameObject, GrowthTargetKind.Workbench, "workbench");
            AddCollider(bench.gameObject, new Vector3(0, 0.6f, 0), new Vector3(1.35f, 1.2f, 0.65f));

            var board = new GameObject("Pinboard").transform; board.SetParent(root, false);
            board.localPosition = BoardPosition; board.localRotation = Quaternion.Euler(0, 90, 0); // on the east wall, facing the room
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
            // A dashed outline of the place's footprint (long, standard or feature), and a plus sign to buy a container.
            float hw = place.Width * 0.5f, hd = place.Depth * 0.5f;
            int along = Mathf.Max(2, Mathf.RoundToInt(place.Width / 0.24f)), across = Mathf.Max(2, Mathf.RoundToInt(place.Depth / 0.24f));
            for (int i = 0; i < along; i++)
            {
                float x = -hw + 0.12f + (place.Width - 0.24f) * i / (along - 1);
                Box(root.transform, "Dash", new Vector3(x, 0.01f, hd - 0.02f), new Vector3(0.12f, 0.01f, 0.04f), marker);
                Box(root.transform, "Dash", new Vector3(x, 0.01f, -hd + 0.02f), new Vector3(0.12f, 0.01f, 0.04f), marker);
            }
            for (int i = 1; i < across - 1; i++)
            {
                float z = -hd + place.Depth * i / (across - 1);
                Box(root.transform, "Dash", new Vector3(hw - 0.02f, 0.01f, z), new Vector3(0.04f, 0.01f, 0.12f), marker);
                Box(root.transform, "Dash", new Vector3(-hw + 0.02f, 0.01f, z), new Vector3(0.04f, 0.01f, 0.12f), marker);
            }
            Box(root.transform, "Plus", new Vector3(0, 0.45f, 0), new Vector3(0.3f, 0.07f, 0.07f), marker);
            Box(root.transform, "Plus", new Vector3(0, 0.45f, 0), new Vector3(0.07f, 0.3f, 0.07f), marker);
            Target(root, GrowthTargetKind.Place, id);
            AddCollider(root, new Vector3(0, 0.45f, 0), new Vector3(place.Width, 0.9f, place.Depth));
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
            foreach (var h in hatchlingViews) if (h) Destroy(h.gameObject);
            hatchlingViews.Clear();
            foreach (var key in builtLevels.Keys.Where(k => k.StartsWith("grower:")).ToList()) { builtLevels.Remove(key); levelParts.Remove(key); }
            foreach (var kv in sources) { Destroy(kv.Value.Root.gameObject); slots[kv.Key].SetActive(true); }
            sources.Clear();
            if (breedingTank) Destroy(breedingTank);
            breedingTank = null; breedingLevel = 0; breedingViews.Clear();
            breedingSlot.SetActive(true);
            ResetLab();
            ResetLine();
            ResetHelpers();
            ResetSpaces();
            ResetExhibition();
        }

        public void SetPhase(GrowthPhase p) => phase = p;

        public void Sync(GrowthShop shop, float dt)
        {
            phase = shop.Phase;
            // Tanks follow the shop's containers, and the workshop's racks (GR-08); each keeps its creatures in step with its occupants.
            foreach (var c in shop.Containers)
            {
                if (tanks.TryGetValue(c.Id, out var view) && view.Signature != TankView.SignatureOf(c)) { Destroy(view.Root.gameObject); tanks.Remove(c.Id); view = null; }
                if (view == null) tanks[c.Id] = view = BuildTank(c);
                view.Sync(c, shop);
            }
            foreach (var id in tanks.Keys.Where(k => shop.Container(k) == null).ToList()) { Destroy(tanks[id].Root.gameObject); tanks.Remove(id); }
            var free = new HashSet<string>(shop.FreePlaces);
            foreach (var m in markers) { bool show = MarkerShows(shop, m.Key, free); if (m.Value.activeSelf != show) m.Value.SetActive(show); }
            foreach (var c in shop.Cultures)
                if (!sources.ContainsKey(c) && GrowerPositions.ContainsKey(c)) { sources[c] = BuildSource(c); slots[c].SetActive(false); }
            foreach (var kv in sources)
            {
                float f = shop.GrowerCapacity == 0 || !shop.GrowerUnits.ContainsKey(kv.Key) ? 0 : Mathf.Clamp01(shop.GrowerUnits[kv.Key] / shop.GrowerCapacity);
                var s = kv.Value;
                s.Fill.localScale = new Vector3(s.Width, Mathf.Max(0.01f, s.Height * f), s.Width);
                s.Fill.localPosition = new Vector3(0, s.BaseY + s.Height * f, 0);
            }
            foreach (var kv in vials)
            {
                int doses = shop.Vials.TryGetValue(kv.Key, out int n) ? n : 0;
                if (kv.Value.gameObject.activeSelf != doses > 0) kv.Value.gameObject.SetActive(doses > 0);
                if (doses > 0) kv.Value.Find("Culture").localScale = new Vector3(0.085f, 0.08f * doses / GrowthContent.VialDoses, 0.085f);
            }
            for (int i = 0; i < doseBlobs.Count; i++)
            {
                bool on = i < shop.Chamber.Count;
                if (doseBlobs[i].gameObject.activeSelf != on) doseBlobs[i].gameObject.SetActive(on);
                if (on) doseBlobs[i].GetComponent<Renderer>().sharedMaterial = culture[shop.Chamber[i]];
            }
            SyncBreeding(shop);
            // A soft glow while it works; dimmer once the clutch is out, so the hatchlings on the tray read (GR-06).
            incubatorGlow.intensity = shop.Incubating ? 0.45f : shop.Hatchling != null ? 0.12f : 0;
            // GR-06: the clutch sits on the tray, side by side.
            if (hatchlingViews.Count != shop.Hatchlings.Count || hatchlingViews.Where((v, i) => v == null || v.SpecimenId != shop.Hatchlings[i].Id).Any())
            {
                foreach (var h in hatchlingViews) if (h) Destroy(h.gameObject);
                hatchlingViews.Clear();
                for (int i = 0; i < shop.Hatchlings.Count; i++)
                {
                    var view = GrowthCreatureView.Create(shop.Hatchlings[i], incubatorRoot);
                    view.transform.localPosition = new Vector3((i - (shop.Hatchlings.Count - 1) * 0.5f) * 0.11f, 0.98f, -0.22f);
                    hatchlingViews.Add(view);
                }
            }
            // The tint port colours the dome while a dose is in it.
            Material domeWant = dome;
            if (shop.IncubatorTint != null && !tintedDome.TryGetValue(shop.IncubatorTint, out domeWant))
            {
                var tint = Hex(GrowthContent.Culture(shop.IncubatorTint).Hex); tint.a = 0.3f;
                tintedDome[shop.IncubatorTint] = domeWant = TintedDome("GR incubator dome " + shop.IncubatorTint, tint);
            }
            if (incubatorDome.sharedMaterial != domeWant) incubatorDome.sharedMaterial = domeWant;
            SyncLevelLooks(shop);
            SyncLab(shop);
            SyncLine(shop);
            SyncVisitors(shop);
            SyncHelpers(shop);
            SyncSpaces(shop);
            SyncExhibition(shop);
            if (LookOn) TickLook(shop, dt);
        }

        // ---------------------------------------------------------------- the breeding tank (GR-05)

        /// <summary>The breeding tank at its level: a cabinet, a glass tank with the pair's water and a nursery behind a
        /// divider, a label, and at level 2 a rim with the tint port (a funnel), open so the nursery can be seen from above.</summary>
        GameObject BuildBreeding(int level)
        {
            var root = new GameObject("Breeding tank (level " + level + ")").transform; root.SetParent(stationsRoot, false);
            root.localPosition = BreedingPosition;
            Box(root, "Cabinet", new Vector3(0, 0.39f, 0), new Vector3(1.1f, 0.78f, 0.5f), cabinet, true);
            Box(root, "Glass", new Vector3(0, BreedingWaterY, 0), new Vector3(1.06f, 0.44f, 0.46f), glass);
            breedingWater = Box(root, "Water", new Vector3(0, BreedingWaterY - 0.02f, 0), new Vector3(1.0f, 0.36f, 0.42f), water).GetComponent<Renderer>();
            Box(root, "Divider", new Vector3(BreedingDivider, BreedingWaterY, 0), new Vector3(0.015f, 0.4f, 0.44f), trim);
            // GR-06: level 3's second pair has a compartment of its own beside the first.
            if (level >= 3) Box(root, "Pair divider", new Vector3(SecondPairDivider, BreedingWaterY, 0), new Vector3(0.012f, 0.4f, 0.44f), glass);
            Box(root, "Gravel", new Vector3(0, 0.8f, 0), new Vector3(1.0f, 0.03f, 0.42f), gravel);
            Box(root, "Label", new Vector3(0, 0.55f, 0.255f), new Vector3(0.56f, 0.14f, 0.02f), paper);
            Box(root, "Heart", new Vector3(-0.2f, 0.55f, 0.267f), new Vector3(0.08f, 0.08f, 0.01f), sign);
            Box(root, "Nursery label", new Vector3(0.36f, 0.72f, 0.255f), new Vector3(0.22f, 0.06f, 0.02f), marker);
            if (level >= 2)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Box(root, "Rim", new Vector3(0, BreedingWaterY + 0.23f, side * 0.235f), new Vector3(1.08f, 0.025f, 0.03f), trim);
                    Box(root, "Rim", new Vector3(side * 0.535f, BreedingWaterY + 0.23f, 0), new Vector3(0.03f, 0.025f, 0.47f), trim);
                }
                ShopWorld.Shape(root, "Tint port", PrimitiveType.Cylinder, new Vector3(-0.48f, BreedingWaterY + 0.29f, 0.2f), new Vector3(0.09f, 0.05f, 0.09f), counterMat);
            }
            Target(root.gameObject, GrowthTargetKind.Breeding, "breeding");
            AddCollider(root.gameObject, new Vector3(0, 0.62f, 0), new Vector3(1.12f, 1.24f, 0.52f));
            if (LookOn) DressBreeding(root, level);
            return root.gameObject;
        }

        readonly Dictionary<string, Material> tintedWater = new Dictionary<string, Material>();

        void SyncBreeding(GrowthShop shop)
        {
            bool owned = shop.HasBreedingTank;
            if (breedingSlot.activeSelf == owned) breedingSlot.SetActive(!owned);
            int level = shop.Level("breeding");
            if (owned && (breedingTank == null || breedingLevel != level))
            {
                if (breedingTank) Destroy(breedingTank);
                breedingViews.Clear();
                breedingTank = BuildBreeding(level);
                breedingLevel = level;
            }
            if (!owned) return;
            // The water takes the tint's colour while a dose is in it (GR.7.2).
            Material want = water;
            if (shop.Tint != null && !tintedWater.TryGetValue(shop.Tint, out want))
            {
                var tint = Hex(GrowthContent.Culture(shop.Tint).Hex); tint.a = 0.32f;
                tintedWater[shop.Tint] = want = LookOn ? LookGlass("GR breeding water " + shop.Tint, tint, 0.4f, 0.5f) : Transparent("GR breeding water " + shop.Tint, tint);
            }
            if (breedingWater.sharedMaterial != want) breedingWater.sharedMaterial = want;
            var inside = shop.AllParents.Concat(shop.AllOffspring).ToList();
            foreach (var s in inside)
                if (!breedingViews.ContainsKey(s.Id)) breedingViews[s.Id] = GrowthCreatureView.Create(s, breedingTank.transform);
            foreach (var id in breedingViews.Keys.Where(k => inside.All(s => s.Id != k)).ToList()) { Destroy(breedingViews[id].gameObject); breedingViews.Remove(id); }
        }

        /// <summary>The pair swims in its compartment, side by side and facing each other now and then; the offspring bobs in the nursery.</summary>
        void AnimateBreeding(float t, GrowthShop shop)
        {
            if (breedingTank == null) return;
            for (int k = 0; k < shop.Pairs.Length; k++)
            {
                var pair = shop.Pairs[k];
                // One compartment for the pair below level 3; at level 3 each pair has half of it.
                float left = breedingLevel >= 3 && k == 1 ? SecondPairDivider + 0.01f : -0.5f;
                float right = breedingLevel >= 3 && k == 0 ? SecondPairDivider - 0.01f : BreedingDivider - 0.01f;
                for (int i = 0; i < pair.Parents.Count; i++)
                    SwimParent(pair.Parents[i], i, left, right, t);
                if (pair.Offspring != null && breedingViews.TryGetValue(pair.Offspring.Id, out var o) && o != null)
                {
                    float nursery = (BreedingDivider + 0.5f) * 0.5f, z = breedingLevel >= 3 ? (k == 0 ? -0.09f : 0.09f) : 0;
                    o.transform.localPosition = new Vector3(nursery, BreedingWaterY + 0.03f * Mathf.Sin(t * 1.7f + k), z + 0.04f * Mathf.Sin(t * 0.9f + k));
                    o.transform.localRotation = Quaternion.Euler(0, 90 + 25f * Mathf.Sin(t * 0.6f + k), 0);
                    o.Tick(t, session.ReducedMotion);
                }
            }
        }

        void SwimParent(GrowthSpecimen parent, int i, float left, float right, float t)
        {
            {
                if (!breedingViews.TryGetValue(parent.Id, out var v) || v == null) return;
                float L = v.Length;
                float hx = Mathf.Max(0.02f, (right - left) * 0.5f - L * 0.6f), cx = (left + right) * 0.5f, hz = Mathf.Max(0.02f, 0.21f - L * 0.5f);
                float speed = L > 0.2f ? 0.45f : L > 0.1f ? 0.6f : 0.8f, phase = i * Mathf.PI;
                Vector3 Path(float tt) => new Vector3(cx + hx * Mathf.Sin(0.5f * tt + phase), 0.05f * Mathf.Sin(0.9f * tt + phase), hz * 0.7f * Mathf.Sin(0.8f * tt + phase * 0.5f));
                Vector3 pos = Path(t * speed), ahead = Path(t * speed + 0.2f);
                v.transform.localPosition = new Vector3(pos.x, BreedingWaterY + pos.y, pos.z);
                Vector3 dir = ahead - pos; dir.y *= 0.3f;
                if (dir.sqrMagnitude > 1e-6f) v.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                v.Tick(t, session.ReducedMotion);
            }
        }

        // ---------------------------------------------------------------- station levels (GR-06)

        /// <summary>GR.9: each level changes a station's look; these placeholder parts show the level at a glance.</summary>
        void SyncLevelLooks(GrowthShop shop)
        {
            Look("siphon", benchRoot, shop.Level("siphon"), SiphonParts);
            Look("bench", benchRoot, shop.Level("bench"), BenchParts);
            Look("incubator", incubatorRoot, shop.Level("incubator"), IncubatorParts);
            Look("counter", counterRoot, shop.Level("counter"), CounterParts);
            foreach (var kv in sources)
            {
                bool vat = GrowthContent.Culture(kv.Key).Source == CultureSource.Vat;
                Look("grower:" + kv.Key, kv.Value.Root, vat ? shop.Level("vat") : shop.Level("grower"), vat ? (System.Action<Transform, int>)VatParts : GrowerParts);
            }
        }

        void Look(string key, Transform root, int level, System.Action<Transform, int> build)
        {
            if (root == null) return;
            if (builtLevels.TryGetValue(key, out int built) && built == level && levelParts.TryGetValue(key, out var have) && have != null) return;
            if (levelParts.TryGetValue(key, out var old) && old) Destroy(old.gameObject);
            var parts = new GameObject("Level " + level + " parts (" + key + ")").transform; parts.SetParent(root, false);
            build(parts, level);
            if (LookOn) DressLevel(key, parts, level);
            levelParts[key] = parts; builtLevels[key] = level;
        }

        /// <summary>The siphon on the research bench: a bigger bulb (2), a twin chamber (3), the vacuum's canister and hose (4).</summary>
        void SiphonParts(Transform p, int level)
        {
            if (level >= 2) ShopWorld.Shape(p, "Bigger bulb", PrimitiveType.Sphere, new Vector3(0.38f, 0.98f, 0.12f), Vector3.one * 0.12f, culture["flow"]);
            if (level >= 3) ShopWorld.Shape(p, "Twin chamber", PrimitiveType.Sphere, new Vector3(0.38f, 0.98f, -0.12f), Vector3.one * 0.12f, culture["bloom"]);
            if (level >= 4)
            {
                Box(p, "Vacuum canister", new Vector3(0.52f, 1.02f, 0), new Vector3(0.16f, 0.22f, 0.2f), trim);
                ShopWorld.Shape(p, "Hose", PrimitiveType.Cylinder, new Vector3(0.3f, 0.93f, 0), new Vector3(0.04f, 0.2f, 0.04f), stand).localRotation = Quaternion.Euler(0, 0, 90);
            }
        }

        /// <summary>The research bench: notes (1), a microscope (2), the analyzer's screen (3), the planner's board (4, GR-08).</summary>
        void BenchParts(Transform p, int level)
        {
            Box(p, "Notes", new Vector3(-0.15f, 0.915f, 0.1f), new Vector3(0.22f, 0.01f, 0.16f), paper);
            if (level >= 2)
            {
                Box(p, "Microscope base", new Vector3(-0.42f, 0.93f, 0), new Vector3(0.16f, 0.04f, 0.14f), stand);
                ShopWorld.Shape(p, "Microscope arm", PrimitiveType.Cylinder, new Vector3(-0.42f, 1.06f, -0.03f), new Vector3(0.04f, 0.11f, 0.04f), stand);
                ShopWorld.Shape(p, "Microscope tube", PrimitiveType.Cylinder, new Vector3(-0.42f, 1.18f, 0.02f), new Vector3(0.05f, 0.07f, 0.05f), marker).localRotation = Quaternion.Euler(20, 0, 0);
            }
            if (level >= 3)
            {
                Box(p, "Analyzer", new Vector3(0.12f, 1.0f, -0.12f), new Vector3(0.26f, 0.18f, 0.2f), trim);
                Box(p, "Analyzer screen", new Vector3(0.12f, 1.02f, -0.015f), new Vector3(0.2f, 0.12f, 0.01f), Glow("GR analyzer screen", new Color(0.55f, 0.85f, 0.78f), 0.3f));
            }
            if (level >= 4)
            {
                Box(p, "Planner board", new Vector3(0.12f, 1.38f, -0.2f), new Vector3(0.5f, 0.34f, 0.03f), paper);
                Box(p, "Planner line", new Vector3(0.12f, 1.38f, -0.18f), new Vector3(0.36f, 0.03f, 0.01f), sign);
            }
        }

        /// <summary>The incubator: the mix log screen (2), the tint port and a wider tray for clutches (3), the auto-doser port (4, power with GR-07).</summary>
        void IncubatorParts(Transform p, int level)
        {
            if (level >= 2) Box(p, "Mix log screen", new Vector3(0.3f, 0.66f, -0.36f), new Vector3(0.22f, 0.15f, 0.02f), Glow("GR screen", new Color(0.55f, 0.85f, 0.78f), 0.25f));
            if (level >= 3)
            {
                ShopWorld.Shape(p, "Tint port", PrimitiveType.Cylinder, new Vector3(0.22f, 1.32f, 0.1f), new Vector3(0.09f, 0.05f, 0.09f), counterMat);
                ShopWorld.Shape(p, "Clutch tray", PrimitiveType.Cylinder, new Vector3(0, 0.905f, -0.22f), new Vector3(0.42f, 0.01f, 0.24f), paper);
            }
            if (level >= 4)
            {
                // GR-07: the auto-doser's port behind the incubator, where the buffers' pipes drop to it.
                Box(p, "Auto-doser port", new Vector3(0, 0.95f, 0.38f), new Vector3(0.3f, 0.3f, 0.08f), trim);
                ShopWorld.Shape(p, "Port inlet", PrimitiveType.Cylinder, new Vector3(0, 1.17f, 0.42f), new Vector3(0.1f, 0.08f, 0.1f), stand);
            }
        }

        /// <summary>The counter: a bag dispenser (2), a card reader (3), a second till where you serve beside Pip (4, GR-09).</summary>
        void CounterParts(Transform p, int level)
        {
            if (level >= 2) Box(p, "Bag dispenser", new Vector3(-0.7f, 1.16f, 0.1f), new Vector3(0.26f, 0.24f, 0.2f), paper);
            if (level >= 3)
            {
                var reader = Box(p, "Card reader", new Vector3(0.15f, 1.1f, 0.12f), new Vector3(0.12f, 0.05f, 0.18f), trim);
                reader.localRotation = Quaternion.Euler(-20, 0, 0);
                Box(p, "Card reader light", new Vector3(0.15f, 1.13f, 0.17f), new Vector3(0.03f, 0.01f, 0.03f), Glow("GR reader light", new Color(0.5f, 0.95f, 0.6f), 0.4f));
            }
            if (level >= 4)
            {
                Box(p, "Second till", new Vector3(-0.25f, 1.14f, -0.05f), new Vector3(0.34f, 0.2f, 0.28f), cabinet);
                var screen = Box(p, "Till screen", new Vector3(-0.25f, 1.32f, -0.12f), new Vector3(0.24f, 0.14f, 0.02f), stand);
                screen.localRotation = Quaternion.Euler(-15, 0, 0);
            }
        }

        /// <summary>A grower: a second shelf ring (2), a tap for a pipe (3), two taps and a lid (4); the pipes come with GR-07.</summary>
        void GrowerParts(Transform p, int level)
        {
            if (level >= 2) ShopWorld.Shape(p, "Shelf ring", PrimitiveType.Cylinder, new Vector3(0, 1.12f, 0), new Vector3(0.47f, 0.015f, 0.47f), trim);
            if (level >= 3) Tap(p, 1);
            if (level >= 4) { Tap(p, -1); ShopWorld.Shape(p, "Jar lid", PrimitiveType.Cylinder, new Vector3(0, 1.62f, 0), new Vector3(0.44f, 0.02f, 0.44f), trim); }
        }

        /// <summary>A tap on the grower's back, by the wall, where its pipe rises (GR-07): the first in the middle, the second beside it.</summary>
        void Tap(Transform p, int side)
        {
            float x = side > 0 ? 0 : 0.16f;
            ShopWorld.Shape(p, "Tap", PrimitiveType.Cylinder, new Vector3(x, 1.18f, -0.38f), new Vector3(0.07f, 0.1f, 0.07f), stand);
            Box(p, "Tap valve", new Vector3(x + 0.06f, 1.12f, -0.38f), new Vector3(0.06f, 0.03f, 0.03f), sign);
        }

        /// <summary>The conditioning vat: a second batch tank (2), a stirrer for faster batches (3).</summary>
        void VatParts(Transform p, int level)
        {
            if (level >= 2)
            {
                ShopWorld.Shape(p, "Second batch", PrimitiveType.Cylinder, new Vector3(0.48f, 0.45f, -0.18f), new Vector3(0.26f, 0.3f, 0.26f), glass);
                ShopWorld.Shape(p, "Second batch culture", PrimitiveType.Cylinder, new Vector3(0.48f, 0.38f, -0.18f), new Vector3(0.22f, 0.22f, 0.22f), culture["mist"]);
            }
            if (level >= 3)
            {
                Box(p, "Stirrer motor", new Vector3(0, 1.36f, 0), new Vector3(0.2f, 0.12f, 0.2f), trim);
                ShopWorld.Shape(p, "Stirrer", PrimitiveType.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(0.03f, 0.25f, 0.03f), stand);
            }
        }

        /// <summary>The creature in the breeding tank nearest the screen's centre (the route's checks and the peek tag).</summary>
        public GrowthCreatureView BreedingCreatureNearCentre(Camera camera)
        {
            GrowthCreatureView best = null; float bestDistance = 0.12f;
            foreach (var creature in breedingViews.Values)
            {
                if (creature == null) continue;
                var p = camera.WorldToViewportPoint(creature.transform.position);
                if (p.z <= 0) continue;
                float d = new Vector2(p.x - 0.5f, (p.y - 0.5f) * camera.pixelHeight / Mathf.Max(1, camera.pixelWidth)).magnitude;
                if (d < bestDistance) { best = creature; bestDistance = d; }
            }
            return best;
        }

        /// <summary>Stand and glass heights by container: the Starter tank at 0.9 m, the Large tank lower and taller, the Show tank lowest and tallest.</summary>
        // GR-12: a lab rack holds large creatures, so its water is as deep as a Large tank's (its stand lower, its top where it was).
        static (float stand, float glass) Proportions(string kind) => kind == "show" ? (0.55f, 1.0f) : kind == "large" ? (0.8f, 0.75f) : kind == "rack" ? (0.45f, 0.75f) : (0.9f, 0.62f);

        /// <summary>The water inside a container of a kind (width, height, depth), which its creatures swim in.</summary>
        public static Vector3 WaterOf(string kind)
        {
            var def = GrowthContent.Container(kind);
            return new Vector3(def.Width - 0.06f, Proportions(kind).glass - 0.12f, def.Depth - 0.06f);
        }

        Material plant;

        /// <summary>A culture's source: a grower cabinet with its jar, or for Mist the conditioning vat (GR.6).</summary>
        Source BuildSource(string id)
        {
            bool vat = GrowthContent.Culture(id).Source == CultureSource.Vat;
            var g = new GameObject(GrowthShop.Name(id) + (vat ? " vat" : " grower")).transform; g.SetParent(stationsRoot, false);
            g.localPosition = GrowerPositions[id]; g.localRotation = Quaternion.Euler(0, GrowerYaw(id), 0);
            Source s;
            if (vat)
            {
                Box(g, "Vat base", new Vector3(0, 0.15f, 0), new Vector3(0.8f, 0.3f, 0.7f), cabinet, true);
                ShopWorld.Shape(g, "Vat", PrimitiveType.Cylinder, new Vector3(0, 0.8f, 0), new Vector3(0.62f, 0.5f, 0.62f), glass);
                var fill = ShopWorld.Shape(g, "Culture", PrimitiveType.Cylinder, new Vector3(0, 0.6f, 0), new Vector3(0.56f, 0.4f, 0.56f), culture[id]);
                ShopWorld.Shape(g, "Pipe", PrimitiveType.Cylinder, new Vector3(0.3f, 1.4f, 0), new Vector3(0.06f, 0.25f, 0.06f), trim);
                Box(g, "Label", new Vector3(0, 0.15f, 0.36f), new Vector3(0.5f, 0.14f, 0.02f), culture[id]);
                s = new Source { Root = g, Fill = fill, BaseY = 0.32f, Height = 0.42f, Width = 0.56f };
            }
            else
            {
                Box(g, "Cabinet", new Vector3(0, 0.5f, 0), new Vector3(0.8f, 1.0f, 0.7f), cabinet, true);
                ShopWorld.Shape(g, "Jar", PrimitiveType.Cylinder, new Vector3(0, 1.3f, 0), new Vector3(0.42f, 0.3f, 0.42f), glass);
                var fill = ShopWorld.Shape(g, "Culture", PrimitiveType.Cylinder, new Vector3(0, 1.05f, 0), new Vector3(0.36f, 0.25f, 0.36f), culture[id]);
                Box(g, "Label", new Vector3(0, 0.75f, 0.36f), new Vector3(0.5f, 0.18f, 0.02f), culture[id]);
                s = new Source { Root = g, Fill = fill, BaseY = 1.0f, Height = 0.25f, Width = 0.36f };
            }
            if (LookOn) DressSource(g, id, vat);
            Target(g.gameObject, GrowthTargetKind.Grower, id);
            AddCollider(g.gameObject, new Vector3(0, 0.8f, 0), new Vector3(0.85f, 1.6f, 0.75f));
            return s;
        }

        public bool HasSource(string culture) => sources.ContainsKey(culture);

        /// <summary>GR-06: the owned sources within reach of a point on the floor (the vacuum's range), nearest first.</summary>
        public List<string> SourcesNear(Vector3 point, float range)
        {
            float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;
            return sources.Where(kv => Flat(kv.Value.Root.position - point) <= range).OrderBy(kv => Flat(kv.Value.Root.position - point)).Select(kv => kv.Key).ToList();
        }

        TankView BuildTank(GrowthContainer c)
        {
            var place = PlaceOf(c.Place);
            var def = c.Def;
            var (standH, glassH) = Proportions(c.Kind);
            float W = def.Width, D = def.Depth, top = standH + glassH, waterH = glassH - 0.12f, waterY = standH + 0.05f + waterH * 0.5f;
            var root = new GameObject("Tank " + c.Id + " (" + c.Kind + " L" + c.Level + ") at " + c.Place).transform;
            root.SetParent(tanksRoot, false);
            root.localPosition = place.Position;
            root.localRotation = Quaternion.Euler(0, place.Yaw, 0);
            if (plant == null) plant = ShopWorld.MakeMaterial("GR plant", new Color(0.35f, 0.62f, 0.38f), 0, 0.3f);
            Box(root, "Stand", new Vector3(0, standH * 0.5f, 0), new Vector3(W, standH, D), stand, true);
            Box(root, "Gravel", new Vector3(0, standH + 0.03f, 0), new Vector3(W - 0.06f, 0.05f, D - 0.06f), gravel);
            Box(root, "Water", new Vector3(0, waterY, 0), new Vector3(W - 0.06f, waterH, D - 0.06f), c.Water == Water.Brine ? waterBrine : c.Water == Water.Warm ? waterWarm : water);
            Box(root, "Glass", new Vector3(0, standH + glassH * 0.5f, 0), new Vector3(W, glassH, D), glass);
            Box(root, "Rim", new Vector3(0, top + 0.015f, 0), new Vector3(W + 0.02f, 0.03f, D + 0.02f), trim);
            if (def.Lab)
            {
                // GR-08: a lab rack is a shelving frame around a plain tank, with a label: no plants, never on show.
                foreach (int sx in new[] { -1, 1 })
                    foreach (int sz in new[] { -1, 1 })
                        Box(root, "Rack post", new Vector3(sx * (W * 0.5f - 0.03f), (top + 0.4f) * 0.5f, sz * (D * 0.5f - 0.03f)), new Vector3(0.05f, top + 0.4f, 0.05f), stand);
                Box(root, "Rack shelf", new Vector3(0, top + 0.4f, 0), new Vector3(W, 0.04f, D), stand);
                Box(root, "Rack label", new Vector3(0, standH * 0.5f, D * 0.5f + 0.01f), new Vector3(0.4f, 0.12f, 0.02f), paper);
            }
            else
                for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(W)); i++)
                    Box(root, "Plant", new Vector3(W * 0.5f - 0.2f - i * 0.9f, standH + 0.15f, -D * 0.5f + 0.15f), new Vector3(0.05f, 0.22f + 0.06f * (i % 2), 0.05f), plant);
            // Its level shows: a filter on the back at level 2, a lid at level 3 (GR.14: one look a level).
            if (c.Level >= 2) Box(root, "Filter", new Vector3(W * 0.5f - 0.3f, top - 0.12f, -D * 0.5f + 0.07f), new Vector3(0.24f, 0.22f, 0.1f), trim);
            if (c.Level >= 3) Box(root, "Lid", new Vector3(0, top + 0.05f, 0), new Vector3(W + 0.02f, 0.03f, D + 0.02f), stand);
            // A Show tank is lit: a light bar along its top front and a lamp inside; level 2 adds a spotlight above.
            Light showLight = null;
            if (c.Kind == "show")
            {
                Box(root, "Light bar", new Vector3(0, top + 0.06f, D * 0.5f - 0.08f), new Vector3(W - 0.2f, 0.05f, 0.08f), lightBar);
                showLight = new GameObject("Show tank lamp").AddComponent<Light>();
                showLight.transform.SetParent(root, false);
                showLight.transform.localPosition = new Vector3(0, top - 0.1f, 0.1f);
                showLight.type = LightType.Point; showLight.range = 2.2f; showLight.intensity = c.Level >= 2 ? 0.6f : 0.4f;
                showLight.color = new Color(1f, 0.95f, 0.85f); showLight.shadows = LightShadows.None;
                if (c.Level >= 2) Box(root, "Spotlight", new Vector3(0, 2.7f, D * 0.5f + 0.2f), new Vector3(0.18f, 0.14f, 0.18f), lightBar);
            }
            // GR.5 harmony: a soft green frame around the rim while the occupants agree (era 3).
            var frame = Box(root, "Harmony frame", new Vector3(0, top + 0.02f, 0), new Vector3(W + 0.06f, 0.02f, D + 0.06f), harmony).gameObject;
            frame.SetActive(false);
            Target(root.gameObject, GrowthTargetKind.Tank, c.Id);
            AddCollider(root.gameObject, new Vector3(0, top * 0.5f, 0), new Vector3(W, top, D));
            if (LookOn) DressTank(root, c, W, D, top, frame);
            return new TankView
            {
                Root = root, ContainerId = c.Id, Place = c.Place, Signature = TankView.SignatureOf(c), HarmonyFrame = frame, ShowLight = showLight,
                Inner = WaterOf(c.Kind), WaterY = waterY, Width = W, Depth = D, Top = top,
            };
        }

        // ---------------------------------------------------------------- peek and creatures

        /// <summary>How far the eye is from the container's glass (0 inside), on the floor plane.</summary>
        public float DistanceToGlass(string containerId, Vector3 eye)
        {
            if (!tanks.TryGetValue(containerId, out var view)) return float.MaxValue;
            Vector3 local = view.Root.InverseTransformPoint(eye);
            float dx = Mathf.Max(0, Mathf.Abs(local.x) - view.Width * 0.5f), dz = Mathf.Max(0, Mathf.Abs(local.z) - view.Depth * 0.5f);
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

        readonly Plane[] frustum = new Plane[6];

        public void Animate(float t, float dt)
        {
            // GR.5's budget: full swimming near and in view, a cheaper swim beyond 6 m or out of view (every third frame,
            // spread across tanks), and frozen beyond 12 m.
            var cam = session.Player != null ? session.Player.View : null;
            if (cam != null) GeometryUtility.CalculateFrustumPlanes(cam, frustum);
            // GR-12: kit creatures look at the player.
            GrowthCreatureView.ViewerSet = cam != null;
            if (cam != null) GrowthCreatureView.Viewer = cam.transform.position;
            DetailCounts[0] = DetailCounts[1] = DetailCounts[2] = 0;
            int index = 0;
            foreach (var tank in tanks.Values)
            {
                int detail = 0, look = 0;
                if (cam != null)
                {
                    Vector3 centre = tank.Root.TransformPoint(new Vector3(0, tank.WaterY, 0));
                    float distance = Vector3.Distance(cam.transform.position, centre);
                    bool visible = GeometryUtility.TestPlanesAABB(frustum, new Bounds(centre, new Vector3(tank.Width, tank.Inner.y, tank.Width)));
                    detail = distance >= FreezeDistance ? 2 : visible && distance < FullDistance ? 0 : 1;
                    look = visible && distance < KitDistance ? 0 : detail == 0 ? 1 : 2;
                }
                tank.Detail = detail;
                tank.ShowDetail(look);
                DetailCounts[detail] += tank.Creatures.Count;
                if (detail == 0 || detail == 1 && (Time.frameCount + index) % 3 == 0 || !tank.Placed) tank.Animate(t, session.ReducedMotion, detail == 0);
                index++;
            }
            for (int i = 0; i < hatchlingViews.Count; i++)
                if (hatchlingViews[i]) { hatchlingViews[i].transform.localRotation = Quaternion.Euler(0, t * 40f + i * 120f, 0); hatchlingViews[i].Tick(t, session.ReducedMotion); }
            if (session.Shop != null) { AnimateBreeding(t, session.Shop); AnimateLab(t, dt); AnimateLine(session.Shop); AnimateHelpers(t); }
            AnimateOpenings(dt);
            AnimateConfetti(dt);
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

        /// <summary>The walk from one container's browse spot to another's, through the waypoints their routes share (GR-03).</summary>
        public static List<Vector3> RouteBetween(string fromPlace, string toPlace)
        {
            if (fromPlace == null || !Places.TryGetValue(fromPlace, out var from)) return RouteTo(toPlace);
            var r = new List<Vector3>();
            if (toPlace == null || !Places.TryGetValue(toPlace, out var to)) return r;
            int shared = 0;
            while (shared < from.Route.Length && shared < to.Route.Length && from.Route[shared] == to.Route[shared]) shared++;
            for (int i = from.Route.Length - 1; i >= shared; i--) r.Add(from.Route[i]);
            if (shared == 0) r.Add(Hub); else r.Add(from.Route[shared - 1]);
            for (int i = shared; i < to.Route.Length; i++) r.Add(to.Route[i]);
            r.Add(to.Browse);
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
            public string ContainerId, Place, Signature;
            /// <summary>The water's inside size (width, height, depth), its centre height, the tank's footprint and top.</summary>
            public Vector3 Inner;
            public float WaterY, Width, Depth, Top;
            public GameObject HarmonyFrame;
            public Light ShowLight;
            /// <summary>The update level last frame: 0 full, 1 cheaper, 2 frozen.</summary>
            public int Detail;
            int shownDetail = -1;
            /// <summary>GR-12: every creature has been placed in the water at least once (a tank frozen far away still shows its
            /// creatures swimming where they were, never at its floor).</summary>
            public bool Placed { get; private set; }
            public readonly Dictionary<int, GrowthCreatureView> Creatures = new Dictionary<int, GrowthCreatureView>();
            readonly Dictionary<string, int> speciesCount = new Dictionary<string, int>();
            readonly List<GrowthCreatureView> order = new List<GrowthCreatureView>();
            readonly Dictionary<string, int> rank = new Dictionary<string, int>();

            public static string SignatureOf(GrowthContainer c) => c.Kind + "|" + c.Level + "|" + c.Water + "|" + c.Place;

            public void Sync(GrowthContainer c, GrowthShop shop)
            {
                bool changed = false;
                foreach (var s in c.Occupants)
                    if (!Creatures.ContainsKey(s.Id)) { Creatures[s.Id] = GrowthCreatureView.Create(s, Root); Creatures[s.Id].SetDetail(Mathf.Max(0, shownDetail)); changed = true; }
                // Creatures with negative ids are validation's view-only extras (the density stress); they stay.
                foreach (var id in Creatures.Keys.Where(k => k > 0 && c.Occupants.All(o => o.Id != k)).ToList()) { Object.Destroy(Creatures[id].gameObject); Creatures.Remove(id); changed = true; }
                speciesCount.Clear();
                foreach (var s in c.Occupants)
                {
                    speciesCount[s.Species] = speciesCount.TryGetValue(s.Species, out int n) ? n + 1 : 1;
                    Creatures[s.Id].Reserved = s.ReservedBy != 0;
                }
                bool harmony = shop.Harmony(c);
                if (HarmonyFrame.activeSelf != harmony) HarmonyFrame.SetActive(harmony);
                if (changed || order.Count != Creatures.Count) { order.Clear(); order.AddRange(Creatures.Values.OrderBy(v => v.SpecimenId)); }
                if (changed) Placed = false;
            }

            public bool InSchool(string species) => speciesCount.TryGetValue(species, out int n) && n >= GrowthShop.SchoolSize;

            /// <summary>GR-12: shows its kit creatures at an update level (the full kit near and in view, else their distant look).</summary>
            public void ShowDetail(int level)
            {
                if (shownDetail == level) return;
                shownDetail = level;
                foreach (var creature in Creatures.Values) if (creature != null) creature.SetDetail(level);
            }

            /// <summary>Validation only: view-only creatures to stress the frame budget past what the places hold.</summary>
            public void AddStressCreature(GrowthSpecimen fake)
            {
                Creatures[fake.Id] = GrowthCreatureView.Create(fake, Root);
                Creatures[fake.Id].SetDetail(Mathf.Max(0, shownDetail));
                order.Clear(); order.AddRange(Creatures.Values.OrderBy(v => v.SpecimenId));
                Placed = false;
            }

            public void Animate(float t, bool reduced, bool full)
            {
                Placed = true;
                rank.Clear();
                foreach (var creature in order)
                {
                    if (creature == null) continue;
                    bool school = InSchool(creature.Species);
                    int k = rank.TryGetValue(creature.Species, out int r) ? r : 0;
                    rank[creature.Species] = k + 1;
                    float L = creature.Length;
                    // Bounds inside the water for this creature's reach and height (GR-12: a kit creature's sails, tails and
                    // lanterns too); the path fills them.
                    // (GR-12: through its trick and habit too, at its peek size, which its water sets.)
                    creature.FitTo(Inner);
                    float grow = creature.PeekScale;
                    float hx = Mathf.Max(0.02f, Inner.x * 0.5f - creature.TrickReach * grow - 0.03f), hz = Mathf.Max(0.02f, Inner.z * 0.5f - creature.TrickReach * grow - 0.03f), hy = Mathf.Max(0.01f, Inner.y * 0.5f - creature.TrickHalf * grow - 0.03f);
                    float speed = (reduced ? 0.5f : 1f) * (L > 0.2f ? 0.6f : L > 0.1f ? 0.8f : 1f) * Mathf.Clamp(1.4f / Mathf.Max(0.5f, Inner.x), 0.45f, 1f);
                    // A school shares its leader's path and keeps a loose formation behind it; others wander alone.
                    int hash = Stable(creature.Species);
                    float phase = school ? (hash & 1023) * 0.01f : creature.SpecimenId * 1.37f;
                    float layer = ((hash >> 3) % 5 - 2) * 0.22f * hy;
                    float time = t * speed;
                    Vector3 Path(float tt) => new Vector3(hx * Mathf.Sin(0.31f * tt + phase), layer + 0.35f * hy * Mathf.Sin(0.57f * tt + phase * 2f), 0.8f * hz * Mathf.Sin(0.63f * tt + phase * 0.7f));
                    Vector3 pos = Path(time), ahead = Path(time + 0.2f);
                    if (school)
                    {
                        Vector3 back = (pos - ahead).normalized;
                        Vector3 side = Vector3.Cross(Vector3.up, back).normalized;
                        float row = Mathf.Floor((k + 1) / 2f), lane = k == 0 ? 0 : (k % 2 == 0 ? 1 : -1);
                        Vector3 offset = back * (1.2f * L * row) + side * (0.9f * L * lane * Mathf.Max(1, row * 0.6f));
                        pos += offset + Vector3.up * (0.25f * L * Mathf.Sin(t * 2.1f + k));
                        ahead += offset;
                    }
                    pos.x = Mathf.Clamp(pos.x, -hx, hx); pos.z = Mathf.Clamp(pos.z, -hz, hz); pos.y = Mathf.Clamp(pos.y, -hy, hy);
                    // A trick (GR-12) eases the creature to mid-water and back, as AD-B's do in the classic tanks.
                    pos.y *= 1 - creature.Settle;
                    creature.transform.localPosition = new Vector3(pos.x, WaterY + pos.y, pos.z);
                    Vector3 dir = ahead - pos; dir.y *= 0.3f;
                    if (dir.sqrMagnitude > 1e-6f) creature.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    if (full) creature.Tick(t, reduced);
                }
            }

            static int Stable(string s) { unchecked { int h = 17; foreach (char ch in s) h = h * 31 + ch; return h & 0x7fffffff; } }
        }
    }
}
