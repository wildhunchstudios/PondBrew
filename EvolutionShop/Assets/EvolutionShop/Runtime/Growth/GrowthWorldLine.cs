using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-08's workshop in the greybox (GR.8, GR.11): an 8 x 6 m room east of the lab annex (a 3.6 m ceiling), closed by a
    /// partition in the annex's east doorway until it is opened at 4 stars; the sorter against its north wall, fed by the
    /// hatchling chute, a glass tube from the incubator's outlet that runs overhead with the pipes (a hatchling slides down
    /// it while the sorter works); four rack places for lab racks, two on its south wall and two on its east wall; and,
    /// from the generator's level 3, the twin turbine and the tide engine standing in its north-east corner. Plain shapes only.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        public static readonly Vector3 SorterPosition = new Vector3(GrowthLab.SorterStand.x, 0, GrowthLab.SorterStand.z), WorkshopGeneratorPosition = new Vector3(9.6f, 0, 14.35f);
        public static readonly Vector3 OutletPosition = new Vector3(GrowthLab.OutPoint.x, 0, GrowthLab.OutPoint.z);
        /// <summary>The annex's east doorway into the workshop, along the wall from its south end (z 9.6 to 11).</summary>
        public const float WorkshopDoorFrom = 0.6f, WorkshopDoorTo = 2.0f, ChuteRadius = 0.075f;

        /// <summary>The workshop's rack places: two along its south wall facing north, two along its east wall facing west.</summary>
        public static readonly Dictionary<string, Place> RackPlaces = new Dictionary<string, Place>
        {
            ["r1"] = Rack(new Vector3(7.0f, 0, 9.6f), 0),
            ["r2"] = Rack(new Vector3(8.75f, 0, 9.6f), 0),
            ["r3"] = Rack(new Vector3(11.4f, 0, 11.6f), -90),
            ["r4"] = Rack(new Vector3(11.4f, 0, 13.4f), -90),
        };

        static Place Rack(Vector3 position, float yaw) =>
            new Place { Position = position, Yaw = yaw, Browse = position + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 1.2f), Route = new Vector3[0], Width = 1.5f, Depth = 1f };

        /// <summary>A container's place in the world: a shop place, or a rack place in the workshop.</summary>
        public static Place PlaceOf(string id) => Places.TryGetValue(id, out var p) ? p : RackPlaces[id];

        GameObject workshopWall, outlet;
        Light workshopLight;
        Transform sorterRoot, sorterLook, chuteBead;
        Material chuteMat;
        int sorterLevel = -1;
        readonly List<Vector3> chutePath = new List<Vector3>();
        public bool WorkshopWallStanding => workshopWall != null && workshopWall.activeSelf;
        public int SorterLookLevel => sorterLevel;
        public int BuiltRacks => tanks.Values.Count(t => t.Place != null && RackPlaces.ContainsKey(t.Place));
        public bool ChuteBuilt => chutePath.Count > 0;
        public bool BeadShowing => chuteBead != null && chuteBead.gameObject.activeSelf;
        public bool OutletShowing => outlet != null && outlet.activeSelf;
        public bool GeneratorInWorkshop => generatorRoot != null && (generatorRoot.localPosition - WorkshopGeneratorPosition).sqrMagnitude < 0.01f;

        /// <summary>The workshop: floor, ceiling and walls, its lamp, and the partition in the annex's east doorway.</summary>
        void WorkshopRoom()
        {
            var rooms = new GameObject("Workshop").transform; rooms.SetParent(transform, false);
            Box(rooms, "Workshop floor", new Vector3(8, -0.05f, 12), new Vector3(8, 0.1f, 6), floorTile, true);
            Box(rooms, "Workshop ceiling", new Vector3(8, 3.65f, 12), new Vector3(8, 0.1f, 6), wallLab);
            Wall("Workshop north wall", new Vector2(4, 15), new Vector2(12, 15), 3.6f, wallLab);
            Wall("Workshop east wall", new Vector2(12, 9), new Vector2(12, 15), 3.6f, wallLab);
            Wall("Workshop south wall", new Vector2(4, 9), new Vector2(12, 9), 3.6f, wallLab);
            workshopLight = Lamp("Workshop lamp", new Vector3(8, 2.8f, 12), 9, 1.15f);
            workshopWall = new GameObject("Workshop partition");
            workshopWall.transform.SetParent(transform, false);
            float w = WorkshopDoorTo - WorkshopDoorFrom, z = 9 + (WorkshopDoorFrom + WorkshopDoorTo) * 0.5f;
            var board = Box(workshopWall.transform, "Partition", new Vector3(4, 1.4f, z), new Vector3(0.12f, 2.8f, w), partition, true);
            Box(workshopWall.transform, "Coming soon sign", new Vector3(3.93f, 1.6f, z), new Vector3(0.02f, 0.5f, w * 0.75f), sign);
            Box(workshopWall.transform, "Sign stripe", new Vector3(3.91f, 1.6f, z), new Vector3(0.02f, 0.1f, w * 0.55f), paper);
            Target(board.gameObject, GrowthTargetKind.WorkshopWall, "workshop");
        }

        /// <summary>The sorter's place, the incubator's outlet (hidden until the sorter is bought), the chute's bead and the rack places.</summary>
        void LineStations()
        {
            chuteMat = Transparent("GR chute", new Color(0.80f, 0.94f, 0.98f, 0.35f));
            sorterRoot = new GameObject("Sorter").transform; sorterRoot.SetParent(labRoot, false);
            sorterRoot.localPosition = SorterPosition; sorterRoot.localRotation = Quaternion.Euler(0, 180, 0); // its front faces south, into the workshop
            Target(sorterRoot.gameObject, GrowthTargetKind.Sorter, "sorter");
            AddCollider(sorterRoot.gameObject, new Vector3(0, 0.8f, 0.1f), new Vector3(1.7f, 1.6f, 1.1f));
            outlet = new GameObject("Incubator outlet");
            outlet.transform.SetParent(labRoot, false);
            outlet.transform.localPosition = OutletPosition;
            Box(outlet.transform, "Outlet", new Vector3(0, 0.82f, 0), new Vector3(0.14f, 0.26f, 0.22f), trim);
            ShopWorld.Shape(outlet.transform, "Chute mouth", PrimitiveType.Cylinder, new Vector3(0, GrowthLab.Height(GrowthLab.Out) + 0.02f, 0), new Vector3(0.2f, 0.03f, 0.2f), stand);
            outlet.SetActive(false);
            chuteBead = ShopWorld.Shape(transform, "Chute bead", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.1f, paper);
            chuteBead.gameObject.SetActive(false);
            foreach (var kv in RackPlaces) markers[kv.Key] = PlaceMarker(kv.Key, kv.Value);
        }

        void ResetLine()
        {
            sorterLevel = -1;
            chutePath.Clear();
            if (chuteBead) chuteBead.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- following the shop

        void SyncLine(GrowthShop shop)
        {
            if (workshopWall.activeSelf == shop.Workshop) workshopWall.SetActive(!shop.Workshop);
            workshopLight.enabled = shop.Workshop;
            int level = shop.Level("sorter");
            if (level != sorterLevel) BuildSorter(level);
            if (outlet.activeSelf != shop.HasSorter) outlet.SetActive(shop.HasSorter);
            // From the generator's level 3 its turbines stand in the workshop.
            var want = shop.Level("generator") >= 3 ? WorkshopGeneratorPosition : GeneratorPosition;
            if ((generatorRoot.localPosition - want).sqrMagnitude > 0.01f) generatorRoot.localPosition = want;
        }

        /// <summary>Whether a place's marker shows: a free place of an open unit, or a free rack place once the workshop is open.</summary>
        static bool MarkerShows(GrowthShop shop, string place, HashSet<string> free) =>
            RackPlaces.ContainsKey(place) ? shop.Workshop && shop.ContainerAt(place) == null : free.Contains(place);

        void BuildSorter(int level)
        {
            sorterLevel = level;
            if (sorterLook) Destroy(sorterLook.gameObject);
            var p = sorterLook = new GameObject("Sorter (level " + level + ")").transform; p.SetParent(sorterRoot, false);
            if (level == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = -0.64f + 0.32f * i;
                    Box(p, "Dash", new Vector3(a, 0.01f, 0.4f), new Vector3(0.16f, 0.01f, 0.03f), marker);
                    Box(p, "Dash", new Vector3(a, 0.01f, -0.4f), new Vector3(0.16f, 0.01f, 0.03f), marker);
                }
                Box(p, "Sign", new Vector3(0, 0.02f, 0), new Vector3(0.5f, 0.02f, 0.3f), paper);
                return;
            }
            // A cabinet with a belt across its top, the hopper the chute drops into at its front, gates over two outlets
            // (the shop and the racks) and a lamp.
            Box(p, "Cabinet", new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.9f, 0.8f), cabinet, true);
            Box(p, "Belt", new Vector3(0, 0.93f, 0), new Vector3(1.4f, 0.06f, 0.4f), stand);
            for (int i = 0; i < 6; i++) Box(p, "Belt slat", new Vector3(-0.6f + 0.24f * i, 0.965f, 0), new Vector3(0.04f, 0.01f, 0.38f), trim);
            float hopperZ = SorterPosition.z - GrowthLab.SorterPoint.z;
            ShopWorld.Shape(p, "Hopper", PrimitiveType.Cylinder, new Vector3(0, GrowthLab.Height(GrowthLab.Sorter) - 0.12f, hopperZ), new Vector3(0.36f, 0.12f, 0.36f), glass);
            Box(p, "Hopper arm", new Vector3(0, 1.2f, hopperZ * 0.5f), new Vector3(0.08f, 0.5f, hopperZ), trim);
            foreach (int side in new[] { -1, 1 })
            {
                Box(p, "Gate", new Vector3(side * 0.55f, 1.12f, 0), new Vector3(0.06f, 0.3f, 0.42f), side < 0 ? marker : sign);
                Box(p, "Outlet tray", new Vector3(side * 0.7f, 0.62f, 0.46f), new Vector3(0.3f, 0.04f, 0.16f), paper);
            }
            Box(p, "Lamp", new Vector3(0.5f, 0.75f, 0.41f), new Vector3(0.08f, 0.08f, 0.02f), lampOk);
            Box(p, "Label", new Vector3(-0.3f, 0.5f, 0.41f), new Vector3(0.6f, 0.16f, 0.02f), paper);
        }

        /// <summary>The chute's path in the world (its bead follows it), kept as the pipes are built.</summary>
        void KeepChutePath(List<Vector3> path) { chutePath.Clear(); chutePath.AddRange(path); }

        /// <summary>A hatchling slides down the chute while the sorter has power and something to sort, as far as its sorting has got.</summary>
        void AnimateLine(GrowthShop shop)
        {
            var use = shop.PowerUse(GrowthShop.SorterPower);
            bool sliding = chutePath.Count > 1 && shop.Chute != null && shop.Hatchlings.Count > 0 && use != null && use.Got > 0;
            if (chuteBead.gameObject.activeSelf != sliding) chuteBead.gameObject.SetActive(sliding);
            if (!sliding) return;
            var mat = culture[GrowthContent.Family(shop.Hatchlings[0].Def.Family).LeadCulture];
            var r = chuteBead.GetComponent<Renderer>();
            if (r.sharedMaterial != mat) r.sharedMaterial = mat;
            chuteBead.localPosition = Along(chutePath, Mathf.Clamp01(shop.SortProgress / GrowthShop.SortSeconds));
        }

        /// <summary>The point a fraction of the way along a path.</summary>
        static Vector3 Along(List<Vector3> path, float f)
        {
            float total = 0;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float want = total * f;
            for (int i = 1; i < path.Count; i++)
            {
                float d = Vector3.Distance(path[i - 1], path[i]);
                if (want <= d || i == path.Count - 1) return Vector3.Lerp(path[i - 1], path[i], d < 1e-4f ? 1 : Mathf.Clamp01(want / d));
                want -= d;
            }
            return path[path.Count - 1];
        }
    }
}
