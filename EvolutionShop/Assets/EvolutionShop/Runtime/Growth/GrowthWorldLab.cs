using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-07's lab in the greybox (GR.8, GR.11): the lab annex north of the back room (8 x 8 m, a 3.6 m ceiling), closed by
    /// a partition in the back room's north doorway until it is opened at 3 stars; the generator in its north-east corner
    /// (the hand crank's wheel, then the bubble turbine); the power board on its east wall (supply against draw); the buffer
    /// places along its south wall; a pump on the front of each grower whose tap is piped; and the pipes themselves,
    /// overhead along the lab grid, dropping to their ends and hopping where one crosses another. Plain shapes only.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        public static readonly Vector3 GeneratorPosition = new Vector3(2.9f, 0, 15.9f), PowerBoardPosition = new Vector3(3.88f, 1.5f, 12.5f), AnnexDoorway = new Vector3(1.15f, 0, 9f);
        public const float AnnexDoorFrom = 0.45f, AnnexDoorTo = 1.85f, PipeRadius = 0.045f, HopHeight = 0.18f;
        public static Vector3 BufferPosition(int place) { var p = GrowthLab.BufferPoint(place); return new Vector3(p.x, 0, p.z); }
        public static Vector3 PortPosition => new Vector3(GrowthLab.PortPoint.x, 0, GrowthLab.PortPoint.z);
        /// <summary>A pipe end's point in the world at its height (a tap's riser, a buffer's lid, the incubator's port).</summary>
        public static Vector3 EndPoint(string end) { var p = GrowthLab.Point(end); return new Vector3(p.x, GrowthLab.Height(end), p.z); }

        sealed class BufferView { public Transform Root, Fill; }
        GameObject annexWall;
        Light annexLight;
        Transform labRoot, generatorRoot, generatorLook, crankWheel, boardSupply, boardDraw, pipesRoot, ghostRoot;
        Renderer boardLamp;
        readonly List<Transform> turbineBubbles = new List<Transform>();
        readonly Dictionary<int, GameObject> bufferSlots = new Dictionary<int, GameObject>();
        readonly Dictionary<int, BufferView> bufferViews = new Dictionary<int, BufferView>();
        /// <summary>The pump on each grower whose tap is piped, by culture, with the level it was built at.</summary>
        readonly Dictionary<string, (Transform root, int level, Transform wheel)> pumps = new Dictionary<string, (Transform, int, Transform)>();
        int generatorLevel = -1;
        string pipeSignature = "", ghostSignature = "";
        Material ghostOk, ghostBad, lampOk, lampSlow;
        public bool AnnexWallStanding => Standing("annex");
        public int BuiltPipes => pipesRoot == null ? 0 : pipesRoot.childCount;
        public int BuiltBuffers => bufferViews.Count;
        public bool PumpStanding(string culture) => pumps.ContainsKey(culture);
        public int GeneratorLookLevel => generatorLevel;
        public bool GhostShowing => ghostRoot != null && ghostRoot.childCount > 0;

        /// <summary>The annex: floor, ceiling and walls, its lamp, and the partition in the back room's doorway.</summary>
        void AnnexRoom()
        {
            var rooms = new GameObject("Lab annex").transform; rooms.SetParent(transform, false);
            labRoot = rooms;
            Box(rooms, "Annex floor", new Vector3(0, -0.05f, 13), new Vector3(8, 0.1f, 8), floorTile, true);
            Box(rooms, "Annex ceiling", new Vector3(0, 3.65f, 13), new Vector3(8, 0.1f, 8), wallLab);
            Wall("Annex west wall", new Vector2(-4, 9), new Vector2(-4, 17), 3.6f, wallLab);
            Wall("Annex north wall", new Vector2(-4, 17), new Vector2(4, 17), 3.6f, wallLab);
            // GR-08: a doorway into the workshop, closed by a partition until it opens (GrowthWorldLine).
            Wall("Annex east wall", new Vector2(4, 9), new Vector2(4, 17), 3.6f, wallLab, new Vector2(WorkshopDoorFrom, WorkshopDoorTo));
            Wall("Annex south wall (east of the back room)", new Vector2(2, 9), new Vector2(4, 9), 3.6f, wallLab);
            annexLight = Lamp("Lab annex lamp", new Vector3(0, 2.8f, 13), 9, 1.15f);
            annexWall = new GameObject("Lab annex partition");
            annexWall.transform.SetParent(transform, false);
            float w = AnnexDoorTo - AnnexDoorFrom, x = (AnnexDoorFrom + AnnexDoorTo) * 0.5f;
            var board = Box(annexWall.transform, "Partition", new Vector3(x, 1.4f, 9), new Vector3(w, 2.8f, 0.12f), partition, true);
            Box(annexWall.transform, "Coming soon sign", new Vector3(x, 1.6f, 8.93f), new Vector3(w * 0.75f, 0.5f, 0.02f), sign);
            Box(annexWall.transform, "Sign stripe", new Vector3(x, 1.6f, 8.91f), new Vector3(w * 0.55f, 0.1f, 0.02f), paper);
            Target(board.gameObject, GrowthTargetKind.AnnexWall, "annex");
        }

        /// <summary>The generator's place, the power board and the buffer places (their dashed squares until bought).</summary>
        void LabStations()
        {
            // GR-13: the ghost is honey where the pipe fits, coral where it is refused (the mockup).
            ghostOk = Transparent("GR pipe preview", new Color(0.98f, 0.78f, 0.37f, 0.5f));
            ghostBad = Transparent("GR pipe preview (refused)", new Color(0.95f, 0.45f, 0.35f, 0.45f));
            lampOk = Glow("GR power lamp ok", new Color(0.45f, 0.95f, 0.55f), 0.5f);
            lampSlow = Glow("GR power lamp slow", new Color(1f, 0.72f, 0.25f), 0.5f);
            generatorRoot = new GameObject("Generator").transform; generatorRoot.SetParent(labRoot, false);
            generatorRoot.localPosition = GeneratorPosition; generatorRoot.localRotation = Quaternion.Euler(0, 180, 0); // facing south, into the annex
            Target(generatorRoot.gameObject, GrowthTargetKind.Generator, "generator");
            AddCollider(generatorRoot.gameObject, new Vector3(0, 0.9f, 0), new Vector3(1.1f, 1.8f, 0.9f));
            var boardRoot = new GameObject("Power board").transform; boardRoot.SetParent(labRoot, false);
            boardRoot.localPosition = PowerBoardPosition; boardRoot.localRotation = Quaternion.Euler(0, 90, 0); // on the east wall, its face (local -z) to the west
            Box(boardRoot, "Board", Vector3.zero, new Vector3(1.1f, 0.8f, 0.06f), stand, true);
            Box(boardRoot, "Supply track", new Vector3(0, 0.16f, -0.035f), new Vector3(0.9f, 0.08f, 0.01f), paper);
            Box(boardRoot, "Draw track", new Vector3(0, -0.04f, -0.035f), new Vector3(0.9f, 0.08f, 0.01f), paper);
            boardSupply = Box(boardRoot, "Supply", new Vector3(-0.45f, 0.16f, -0.045f), new Vector3(0.001f, 0.08f, 0.01f), Glow("GR supply", new Color(0.45f, 0.95f, 0.55f), 0.4f));
            boardDraw = Box(boardRoot, "Draw", new Vector3(-0.45f, -0.04f, -0.045f), new Vector3(0.001f, 0.08f, 0.01f), Glow("GR draw", new Color(1f, 0.72f, 0.25f), 0.4f));
            boardLamp = ShopWorld.Shape(boardRoot, "Lamp", PrimitiveType.Sphere, new Vector3(0.42f, -0.26f, -0.04f), Vector3.one * 0.08f, stand).GetComponent<Renderer>();
            Target(boardRoot.gameObject, GrowthTargetKind.PowerBoard, "board");
            for (int p = 0; p < GrowthLab.BufferPlaces; p++)
            {
                var slot = new GameObject("Buffer place " + (p + 1)).transform; slot.SetParent(labRoot, false);
                slot.localPosition = BufferPosition(p);
                foreach (int side in new[] { -1, 1 })
                {
                    Box(slot, "Dash", new Vector3(side * 0.2f, 0.01f, 0), new Vector3(0.03f, 0.01f, 0.3f), marker);
                    Box(slot, "Dash", new Vector3(0, 0.01f, side * 0.17f), new Vector3(0.3f, 0.01f, 0.03f), marker);
                }
                Box(slot, "Number", new Vector3(0, 0.02f, 0), new Vector3(0.06f + 0.03f * p, 0.02f, 0.06f), paper);
                Target(slot.gameObject, GrowthTargetKind.Buffer, p.ToString());
                var col = slot.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.6f, 0); col.size = new Vector3(0.45f, 1.2f, 0.4f); col.isTrigger = true; // focus, not a wall
                bufferSlots[p] = slot.gameObject;
            }
            pipesRoot = new GameObject("Pipes").transform; pipesRoot.SetParent(transform, false);
            ghostRoot = new GameObject("Pipe preview").transform; ghostRoot.SetParent(transform, false);
        }

        void ResetLab()
        {
            foreach (var v in bufferViews.Values) Destroy(v.Root.gameObject);
            bufferViews.Clear();
            foreach (var p in pumps.Values) if (p.root) Destroy(p.root.gameObject);
            pumps.Clear();
            for (int i = pipesRoot.childCount - 1; i >= 0; i--) Destroy(pipesRoot.GetChild(i).gameObject);
            ClearLookPipes();
            pipeSignature = "";
            HidePipeGhost();
            generatorLevel = -1;
        }

        // ---------------------------------------------------------------- following the shop

        void SyncLab(GrowthShop shop)
        {
            // The generator's look follows its level: a dashed place, the hand crank's wheel, the bubble turbine.
            int gen = shop.Level("generator");
            if (gen != generatorLevel) BuildGenerator(gen);
            // Buffers: a tank where one is bought, filling with its culture's colour.
            foreach (var b in shop.Buffers)
            {
                if (!bufferViews.TryGetValue(b.Place, out var view)) bufferViews[b.Place] = view = BuildBuffer(b.Place);
                float f = Mathf.Clamp01(b.Units / GrowthContent.BufferCapacity);
                view.Fill.localScale = new Vector3(0.3f, Mathf.Max(0.005f, 0.5f * f), 0.3f);
                view.Fill.localPosition = new Vector3(0, 0.22f + 1.0f * f * 0.5f, 0);
                var mat = b.Culture == null ? glass : culture[b.Culture];
                var r = view.Fill.GetComponent<Renderer>();
                if (r.sharedMaterial != mat) r.sharedMaterial = mat;
                if (view.Fill.gameObject.activeSelf != (b.Units > 0.01f)) view.Fill.gameObject.SetActive(b.Units > 0.01f);
            }
            foreach (var place in bufferViews.Keys.Where(p => shop.BufferAt(p) == null).ToList()) { Destroy(bufferViews[place].Root.gameObject); bufferViews.Remove(place); }
            foreach (var kv in bufferSlots) { bool show = shop.BufferAt(kv.Key) == null && shop.Annex; if (kv.Value.activeSelf != show) kv.Value.SetActive(show); }
            // A pump on the front of each grower whose tap is piped: a crank wheel (level 1) or an electric motor (2 and 3).
            var piped = new HashSet<string>(shop.Lines.Select(p => p.Culture));
            foreach (var c in pumps.Keys.Where(c => !piped.Contains(c) || pumps[c].level != shop.Level("pump") || !sources.ContainsKey(c)).ToList())
            { if (pumps[c].root) Destroy(pumps[c].root.gameObject); pumps.Remove(c); }
            foreach (var c in piped) if (!pumps.ContainsKey(c) && sources.TryGetValue(c, out var src)) pumps[c] = BuildPump(src.Root, c, shop.Level("pump"));
            // Pipes, rebuilt when they change.
            string signature = string.Join(";", shop.Pipes.Select(p => p.Id + ":" + p.From + ">" + p.To + ":" + (PipeCulture(shop, p) ?? "")));
            if (signature != pipeSignature) { pipeSignature = signature; BuildPipes(shop); }
            // The power board: supply and draw as bars of the generator's largest output, a lamp that is amber when short.
            float scale = 12f;
            boardSupply.localScale = new Vector3(Mathf.Max(0.001f, 0.9f * Mathf.Clamp01(shop.Supply / scale)), 0.08f, 0.01f);
            boardSupply.localPosition = new Vector3(-0.45f + boardSupply.localScale.x * 0.5f, 0.16f, -0.045f);
            boardDraw.localScale = new Vector3(Mathf.Max(0.001f, 0.9f * Mathf.Clamp01(shop.Draw / scale)), 0.08f, 0.01f);
            boardDraw.localPosition = new Vector3(-0.45f + boardDraw.localScale.x * 0.5f, -0.04f, -0.045f);
            var lamp = shop.Draw <= 0 ? stand : shop.Supply + 0.001f >= shop.Draw ? lampOk : lampSlow;
            if (boardLamp.sharedMaterial != lamp) boardLamp.sharedMaterial = lamp;
        }

        void BuildGenerator(int level)
        {
            generatorLevel = level;
            if (generatorLook) Destroy(generatorLook.gameObject);
            crankWheel = null; turbineBubbles.Clear();
            var p = generatorLook = new GameObject("Generator (level " + level + ")").transform; p.SetParent(generatorRoot, false);
            if (level == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = -0.45f + 0.3f * i;
                    Box(p, "Dash", new Vector3(a, 0.01f, 0.4f), new Vector3(0.16f, 0.01f, 0.03f), marker);
                    Box(p, "Dash", new Vector3(a, 0.01f, -0.4f), new Vector3(0.16f, 0.01f, 0.03f), marker);
                }
                Box(p, "Sign", new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.02f, 0.24f), paper);
                return;
            }
            Box(p, "Base", new Vector3(0, 0.2f, 0), new Vector3(1.0f, 0.4f, 0.8f), cabinet, true);
            if (level == 1)
            {
                // The hand crank: a housing, a big wheel facing into the annex and its handle.
                Box(p, "Housing", new Vector3(0, 0.75f, 0), new Vector3(0.6f, 0.7f, 0.5f), trim, true);
                crankWheel = new GameObject("Crank wheel").transform; crankWheel.SetParent(p, false);
                crankWheel.localPosition = new Vector3(0, 0.85f, 0.33f);
                ShopWorld.Shape(crankWheel, "Wheel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.6f, 0.03f, 0.6f), stand).localRotation = Quaternion.Euler(90, 0, 0);
                Box(crankWheel, "Spoke", Vector3.zero, new Vector3(0.56f, 0.04f, 0.04f), marker);
                ShopWorld.Shape(crankWheel, "Handle", PrimitiveType.Cylinder, new Vector3(0.24f, 0, 0.08f), new Vector3(0.05f, 0.08f, 0.05f), counterMat).localRotation = Quaternion.Euler(90, 0, 0);
            }
            else
            {
                // The bubble turbine: a tall glass column of rising bubbles over a turbine housing. GR-08: the twin turbine
                // (level 3) has two columns, the tide engine (level 4) three over a wider drum; both stand in the workshop.
                int columns = level >= 4 ? 3 : level == 3 ? 2 : 1;
                Box(p, "Turbine", new Vector3(0, 0.6f, 0), new Vector3(0.7f + 0.45f * (columns - 1), 0.4f, 0.6f), trim, true);
                if (level >= 4) ShopWorld.Shape(p, "Tide drum", PrimitiveType.Cylinder, new Vector3(0, 0.6f, -0.32f), new Vector3(0.5f, 0.5f, 0.5f), stand).localRotation = Quaternion.Euler(0, 0, 90);
                for (int k = 0; k < columns; k++)
                {
                    float x = (k - (columns - 1) * 0.5f) * 0.5f;
                    ShopWorld.Shape(p, "Column", PrimitiveType.Cylinder, new Vector3(x, 1.3f, 0), new Vector3(0.44f, 0.5f, 0.44f), glass);
                    ShopWorld.Shape(p, "Column water", PrimitiveType.Cylinder, new Vector3(x, 1.25f, 0), new Vector3(0.38f, 0.44f, 0.38f), water);
                    for (int i = 0; i < 8; i++)
                        turbineBubbles.Add(ShopWorld.Shape(p, "Bubble " + i, PrimitiveType.Sphere, new Vector3(x + 0.1f * Mathf.Sin(i * 2.1f + k), 0.85f, 0.1f * Mathf.Cos(i * 2.1f + k)), Vector3.one * 0.07f, dome));
                }
                Box(p, "Lamp", new Vector3(0.28f + 0.22f * (columns - 1), 0.75f, 0.31f), new Vector3(0.08f, 0.08f, 0.02f), lampOk);
            }
            if (LookOn) DressGenerator(p, level);
        }

        BufferView BuildBuffer(int place)
        {
            var root = new GameObject("Buffer " + (place + 1)).transform; root.SetParent(labRoot, false);
            root.localPosition = BufferPosition(place);
            ShopWorld.Shape(root, "Stand", PrimitiveType.Cylinder, new Vector3(0, 0.1f, 0), new Vector3(0.38f, 0.1f, 0.38f), stand);
            ShopWorld.Shape(root, "Tank", PrimitiveType.Cylinder, new Vector3(0, 0.75f, 0), new Vector3(0.36f, 0.55f, 0.36f), glass);
            var fill = ShopWorld.Shape(root, "Culture", PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0), new Vector3(0.3f, 0.01f, 0.3f), glass);
            ShopWorld.Shape(root, "Lid", PrimitiveType.Cylinder, new Vector3(0, GrowthLab.Height(GrowthLab.Buffer(place)) - 0.02f, 0), new Vector3(0.38f, 0.02f, 0.38f), trim);
            Box(root, "Number", new Vector3(0, 0.45f, 0.19f), new Vector3(0.06f + 0.03f * place, 0.05f, 0.02f), paper);
            Target(root.gameObject, GrowthTargetKind.Buffer, place.ToString());
            AddCollider(root.gameObject, new Vector3(0, 0.75f, 0), new Vector3(0.42f, 1.5f, 0.42f));
            if (LookOn) { Kit(root, "Buffer", default, null, "Stand", "Tank", "Lid", "Number"); Kit(root, "BufferTag" + (place + 1)); }
            return new BufferView { Root = root, Fill = fill };
        }

        /// <summary>A grower's pump on its cabinet's front: the hand pump's crank wheel, or an electric motor with its light.</summary>
        (Transform, int, Transform) BuildPump(Transform grower, string id, int level)
        {
            var p = new GameObject("Pump (" + GrowthShop.Name(id) + ", level " + level + ")").transform; p.SetParent(grower, false);
            p.localPosition = new Vector3(0.26f, 0.42f, 0.44f);
            Box(p, "Housing", Vector3.zero, new Vector3(0.22f, 0.26f, 0.14f), trim);
            Transform wheel = null;
            if (level <= 1)
            {
                wheel = new GameObject("Crank").transform; wheel.SetParent(p, false); wheel.localPosition = new Vector3(0, 0, 0.09f);
                ShopWorld.Shape(wheel, "Wheel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.2f, 0.015f, 0.2f), stand).localRotation = Quaternion.Euler(90, 0, 0);
                ShopWorld.Shape(wheel, "Handle", PrimitiveType.Cylinder, new Vector3(0.08f, 0, 0.03f), new Vector3(0.03f, 0.04f, 0.03f), marker).localRotation = Quaternion.Euler(90, 0, 0);
            }
            else
            {
                Box(p, "Motor", new Vector3(0, 0.02f, 0.09f), new Vector3(0.16f, 0.16f, 0.06f), stand);
                Box(p, "Light", new Vector3(0.06f, 0.1f, 0.125f), new Vector3(0.03f, 0.03f, 0.01f), level >= 3 ? lampSlow : lampOk);
            }
            Target(p.gameObject, GrowthTargetKind.Pump, id);
            AddCollider(p.gameObject, new Vector3(0, 0, 0.06f), new Vector3(0.3f, 0.34f, 0.24f));
            if (LookOn)
            {
                if (wheel != null) { Kit(p, "PumpHand", default, null, "Housing"); Kit(wheel, "PumpHandWheel", default, null, "Wheel", "Handle"); }
                else Kit(p, "PumpMotor", default, null, "Housing", "Motor");
            }
            return (p, level, wheel);
        }

        // ---------------------------------------------------------------- pipes

        /// <summary>
        /// A pipe's path in the world: up from its first end, along its cells' centres overhead, down to its last end; over a
        /// cell where it crosses a pipe laid before it, it rises by HopHeight (a hop over the other).
        /// </summary>
        public static List<Vector3> PipePath(GrowthPipe pipe, ICollection<(int c, int r)> crossings)
        {
            float h = GrowthLab.PipeHeight;
            var points = new List<Vector3>();
            var a = EndPoint(pipe.From);
            points.Add(a); points.Add(new Vector3(a.x, h, a.z));
            for (int i = 0; i < pipe.Cells.Count; i++)
            {
                var c = GrowthLab.Centre(pipe.Cells[i]);
                points.Add(new Vector3(c.x, crossings != null && crossings.Contains(pipe.Cells[i]) ? h + HopHeight : h, c.z));
            }
            var b = EndPoint(pipe.To);
            points.Add(new Vector3(b.x, h, b.z)); points.Add(b);
            return points;
        }

        /// <summary>The culture a pipe carries: its line's, or for a port pipe its buffer's; none for the chute.</summary>
        static string PipeCulture(GrowthShop shop, GrowthPipe p) =>
            p.Line ? p.Culture : GrowthLab.IsBuffer(p.From) ? shop.BufferAt(GrowthLab.BufferPlace(p.From))?.Culture : null;

        void BuildPipes(GrowthShop shop)
        {
            for (int i = pipesRoot.childCount - 1; i >= 0; i--) Destroy(pipesRoot.GetChild(i).gameObject);
            ClearLookPipes();
            var laid = new List<GrowthPipe>();
            KeepChutePath(new List<Vector3>());
            foreach (var pipe in shop.Pipes)
            {
                var before = new HashSet<(int c, int r)>(laid.SelectMany(p => p.Cells));
                var crossings = pipe.Cells.Skip(1).Take(Mathf.Max(0, pipe.Cells.Count - 2)).Where(before.Contains).ToList();
                string c = PipeCulture(shop, pipe);
                var root = new GameObject("Pipe " + pipe.Id + ": " + pipe.From + " to " + pipe.To).transform; root.SetParent(pipesRoot, false);
                var path = PipePath(pipe, crossings);
                // GR-08: the hatchling chute is a wider glass tube; its bead follows its path.
                if (pipe.Chute) { if (LookOn) DrawLookChute(root, path); else DrawPath(root, path, chuteMat, ChuteRadius); KeepChutePath(path); }
                else if (LookOn) DrawLookPipe(root, path, pipe, c);
                else DrawPath(root, path, c == null ? stand : culture[c]);
                laid.Add(pipe);
            }
        }

        void DrawPath(Transform root, List<Vector3> points, Material material, float radius = PipeRadius)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1]; var b = points[i];
                float length = Vector3.Distance(a, b);
                if (length < 0.005f) continue;
                var seg = ShopWorld.Shape(root, "Segment", PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(radius * 2, length * 0.5f + radius, radius * 2), material);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            }
        }

        /// <summary>The pipe being connected, before it is laid: green where it can go, red when it is refused (pipe mode).</summary>
        public void ShowPipeGhost(GrowthPipe plan, bool ok, GrowthShop shop)
        {
            string signature = plan == null ? "" : plan.From + ">" + plan.To + ":" + ok + ":" + plan.Cells.Count;
            if (signature == ghostSignature) return;
            HidePipeGhost();
            ghostSignature = signature;
            if (plan == null) return;
            var before = new HashSet<(int c, int r)>(shop.Pipes.SelectMany(p => p.Cells));
            DrawPath(ghostRoot, PipePath(plan, plan.Cells.Skip(1).Take(Mathf.Max(0, plan.Cells.Count - 2)).Where(before.Contains).ToList()), ok ? ghostOk : ghostBad, plan.Chute ? ChuteRadius : PipeRadius);
        }

        public void HidePipeGhost()
        {
            ghostSignature = "";
            if (ghostRoot != null) for (int i = ghostRoot.childCount - 1; i >= 0; i--) Destroy(ghostRoot.GetChild(i).gameObject);
        }

        /// <summary>The crank turns while it is cranked, a hand pump's wheel while it pumps, and the turbine's bubbles always rise.</summary>
        void AnimateLab(float t, float dt)
        {
            var shop = session.Shop;
            if (crankWheel != null && shop.Cranking) crankWheel.localRotation *= Quaternion.Euler(0, 0, -240 * dt);
            for (int i = 0; i < turbineBubbles.Count; i++)
            {
                float y = 0.85f + Mathf.Repeat(t * 0.35f + i / 8f, 1f) * 0.9f;
                var p = turbineBubbles[i].localPosition;
                turbineBubbles[i].localPosition = new Vector3(p.x, y, p.z);
            }
            foreach (var kv in pumps)
                if (kv.Value.wheel != null && session.PumpingNow && session.Focused != null && session.Focused.Id == kv.Key)
                    kv.Value.wheel.localRotation *= Quaternion.Euler(0, 0, -300 * dt);
            if (LookOn) AnimateLookPipes(shop, dt);
        }
    }
}
