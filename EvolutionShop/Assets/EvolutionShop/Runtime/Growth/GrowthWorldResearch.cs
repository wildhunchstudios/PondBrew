using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-30 (tester round 1, F1): the research room west of the power room (x -10 to -4, z 11 to 17), behind a hoarding in the power
    /// room's west wall until it opens. Three small tanks along its north wall, each with a round timer that fills as its job goes;
    /// fifteen jars on its west wall, each showing the creature put in it; the desk against its south wall, its screen lighting a cell
    /// for the first six jars' pairs; and the tray beside the desk, where new creatures wait to be taken. Kit pieces dress it; the
    /// greybox's shapes keep the colliders and targets.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        /// <summary>The doorway along the power room's west wall, in metres from its south end (z 14.65 to 16.05).</summary>
        public const float ResearchDoorFrom = 5.65f, ResearchDoorTo = 7.05f;
        public static readonly Vector3 ResearchDeskPosition = At(-6.9f, 0, 11.6f), ResearchTrayPosition = At(-8.8f, 0, 11.45f);
        public static Vector3 ResearchTankPosition(int tank) => At(-8.7f + tank * 1.4f, 0, 16.55f);
        /// <summary>A jar's centre: three shelves of five, the lowest first.</summary>
        public static Vector3 JarPosition(int jar) => At(-9.72f, 0.96f + jar / 5 * 0.46f, 12.55f + jar % 5 * 0.525f);
        public static Vector3 ResearchTrayPlace(int place) => new Vector3(-0.45f + place * 0.18f, 0.92f, 0);

        const float ResearchWaterY = 1.04f, JarLength = 0.2f, TrayLength = 0.16f, TankLeast = 0.18f;
        const int ResearchTicks = 16, ScreenCells = 6;

        GameObject researchWall;
        Light researchLight;
        Transform researchRoot, researchDesk, researchTray;
        Material wallResearch, tickOn, tickOff, cellOff, cellSame, cellTried, cellQueued;
        readonly Transform[] researchTanks = new Transform[GrowthShop.ResearchTankCount];
        readonly Renderer[][] researchTicks = new Renderer[GrowthShop.ResearchTankCount][];
        readonly int[] ticksLit = new int[GrowthShop.ResearchTankCount];
        readonly GrowthResearchJob[] tankShows = new GrowthResearchJob[GrowthShop.ResearchTankCount];
        readonly List<GrowthCreatureView>[] tankViews = { new List<GrowthCreatureView>(), new List<GrowthCreatureView>(), new List<GrowthCreatureView>() };
        readonly List<Transform>[] tankDoses = { new List<Transform>(), new List<Transform>(), new List<Transform>() };
        readonly Dictionary<int, GrowthCreatureView> jarViews = new Dictionary<int, GrowthCreatureView>(), trayViews = new Dictionary<int, GrowthCreatureView>();
        readonly Renderer[,] screenCells = new Renderer[ScreenCells, ScreenCells];
        string screenShows;

        /// <summary>Validation: what the room shows now.</summary>
        public bool ResearchWallStanding => Standing("research");
        public int JarCreaturesShown => jarViews.Count;
        public int TrayCreaturesShown => trayViews.Count;
        public int TankCreaturesShown(int tank) => tankViews[tank].Count;
        public int TankDosesShown(int tank) => tankDoses[tank].Count;
        public int TankTicksLit(int tank) => ticksLit[tank];
        public int ScreenCellsTried { get { int n = 0; foreach (var r in screenCells) if (r.sharedMaterial == cellTried) n++; return n; } }
        public int ScreenCellsQueued { get { int n = 0; foreach (var r in screenCells) if (r.sharedMaterial == cellQueued) n++; return n; } }
        public Transform ResearchTankRoot(int tank) => researchTanks[tank];

        void ResearchRoom()
        {
            wallResearch = LookOn ? Plastic("research wall", C("#EFE9FF"), 0.12f) : ShopWorld.MakeMaterial("GR research wall", new Color(0.87f, 0.84f, 0.95f), 0, 0.2f);
            var rooms = new GameObject("Research room").transform; rooms.SetParent(transform, false);
            researchRoot = rooms;
            RoomBox(rooms, "Research floor", new Vector3(-7, -0.05f, 14), new Vector3(6, 0.1f, 6), floorTile, true);
            RoomBox(rooms, "Research ceiling", new Vector3(-7, 3.65f, 14), new Vector3(6, 0.1f, 6), wallResearch);
            Wall("Research north wall", new Vector2(-10, 17), new Vector2(-4, 17), 3.6f, wallResearch);
            Wall("Research west wall", new Vector2(-10, 11), new Vector2(-10, 17), 3.6f, wallResearch);
            Wall("Research south wall", new Vector2(-10, 11), new Vector2(-4, 11), 3.6f, wallResearch);
            // The power room's west wall is this room's east wall: a lilac face on this side of it, round the doorway.
            RoomBox(rooms, "Research east face", new Vector3(-4.105f, 1.8f, 12.825f), new Vector3(0.01f, 3.6f, 3.65f), wallResearch);
            RoomBox(rooms, "Research east face", new Vector3(-4.105f, 1.8f, 16.525f), new Vector3(0.01f, 3.6f, 0.95f), wallResearch);
            RoomBox(rooms, "Research east face", new Vector3(-4.105f, 3.2f, 15.35f), new Vector3(0.01f, 0.8f, 1.4f), wallResearch);
            researchLight = Lamp("Research lamp", At(-7, 3.3f, 14), 9, 1.15f);
            researchWall = new GameObject("Research partition");
            researchWall.transform.SetParent(transform, false);
            float w = ResearchDoorTo - ResearchDoorFrom, z = 9 + (ResearchDoorFrom + ResearchDoorTo) * 0.5f;
            var board = RoomBox(researchWall.transform, "Partition", new Vector3(-4, 1.4f, z), new Vector3(0.12f, 2.8f, w), partition, true);
            RoomBox(researchWall.transform, "Coming soon sign", new Vector3(-3.93f, 1.6f, z), new Vector3(0.02f, 0.5f, w * 0.75f), sign);
            RoomBox(researchWall.transform, "Sign stripe", new Vector3(-3.91f, 1.6f, z), new Vector3(0.02f, 0.1f, w * 0.55f), paper);
            Target(board.gameObject, GrowthTargetKind.ResearchWall, "research");
            ResearchStations();
        }

        void ResearchStations()
        {
            tickOn = Glow("GR research tick on", new Color(0.5f, 0.89f, 0.84f), 0.8f);
            tickOff = ShopWorld.MakeMaterial("GR research tick off", new Color(0.17f, 0.36f, 0.4f), 0, 0.4f);
            cellOff = ShopWorld.MakeMaterial("GR research cell", new Color(0.23f, 0.45f, 0.51f), 0, 0.4f);
            cellSame = ShopWorld.MakeMaterial("GR research cell same", new Color(0.11f, 0.31f, 0.36f), 0, 0.4f);
            cellTried = Glow("GR research cell tried", new Color(0.5f, 0.89f, 0.84f), 0.6f);
            cellQueued = Glow("GR research cell queued", new Color(1f, 0.88f, 0.54f), 0.4f);
            for (int k = 0; k < researchTanks.Length; k++)
            {
                var root = new GameObject("Research tank " + (k + 1)).transform; root.SetParent(researchRoot, false);
                root.localPosition = ResearchTankPosition(k);
                researchTanks[k] = root;
                Box(root, "Cabinet", new Vector3(0, 0.39f, 0), new Vector3(1.1f, 0.78f, 0.5f), cabinet);
                Box(root, "Frame", new Vector3(0, 0.81f, 0), new Vector3(1.14f, 0.06f, 0.52f), trim);
                Box(root, "Glass", new Vector3(0, 1.05f, 0), new Vector3(1.06f, 0.46f, 0.44f), glass);
                Box(root, "Water", new Vector3(0, 1.02f, 0), new Vector3(1.02f, 0.38f, 0.4f), water);
                Target(root.gameObject, GrowthTargetKind.ResearchTank, k.ToString());
                AddCollider(root.gameObject, new Vector3(0, 0.67f, 0), new Vector3(1.16f, 1.34f, 0.54f));
                var ticks = researchTicks[k] = new Renderer[ResearchTicks];
                for (int i = 0; i < ResearchTicks; i++)
                {
                    float a = i * Mathf.PI * 2 / ResearchTicks;
                    var tick = Box(root, "Timer tick", new Vector3(Mathf.Sin(a) * 0.11f, 0.45f + Mathf.Cos(a) * 0.11f, -0.283f), new Vector3(0.03f, 0.03f, 0.006f), tickOff);
                    ticks[i] = tick.GetComponent<Renderer>();
                }
            }
            for (int jar = 0; jar < GrowthShop.SpecimenJars; jar++)
            {
                var g = new GameObject("Jar " + (jar + 1)); g.transform.SetParent(researchRoot, false);
                g.transform.localPosition = JarPosition(jar);
                Target(g, GrowthTargetKind.ResearchJar, jar.ToString());
                AddCollider(g, Vector3.zero, new Vector3(0.3f, 0.42f, 0.5f));
                if (!LookOn) Box(g.transform, "Jar", Vector3.zero, new Vector3(0.28f, 0.34f, 0.28f), glass);
            }
            var desk = new GameObject("Research desk").transform; desk.SetParent(researchRoot, false);
            desk.localPosition = ResearchDeskPosition;
            researchDesk = desk;
            Box(desk, "Desk body", new Vector3(0, 0.42f, 0), new Vector3(1.2f, 0.78f, 0.8f), cabinet);
            Box(desk, "Desk top", new Vector3(0, 0.85f, 0), new Vector3(1.3f, 0.07f, 0.9f), stand);
            Box(desk, "Screen", new Vector3(0, 1.42f, -0.2f), new Vector3(1.16f, 0.86f, 0.06f), trim);
            Target(desk.gameObject, GrowthTargetKind.ResearchDesk, "desk");
            AddCollider(desk.gameObject, new Vector3(0, 0.93f, 0), new Vector3(1.3f, 1.86f, 0.92f));
            for (int r = 0; r < ScreenCells; r++)
                for (int c = 0; c < ScreenCells; c++)
                {
                    var cell = Box(desk, "Screen cell", new Vector3(-0.42f + c * 0.17f, 1.72f - r * 0.115f, -0.155f), new Vector3(0.14f, 0.095f, 0.006f), r == c ? cellSame : cellOff);
                    screenCells[r, c] = cell.GetComponent<Renderer>();
                }
            var tray = new GameObject("Research tray").transform; tray.SetParent(researchRoot, false);
            tray.localPosition = ResearchTrayPosition;
            researchTray = tray;
            Box(tray, "Tray stand", new Vector3(0, 0.36f, 0), new Vector3(1.2f, 0.72f, 0.5f), cabinet);
            Box(tray, "Tray glass", new Vector3(0, 0.9f, 0), new Vector3(1.2f, 0.28f, 0.5f), glass);
            Target(tray.gameObject, GrowthTargetKind.ResearchTray, "tray");
            AddCollider(tray.gameObject, new Vector3(0, 0.53f, 0), new Vector3(1.26f, 1.06f, 0.56f));
        }

        /// <summary>The look's pieces for the room.</summary>
        void DressResearch(Transform dressing)
        {
            Hide(researchRoot, "Research floor");
            foreach (var asset in new[] { "ResearchFloor", "ResearchDressing", "ResearchJars" }) Kit(dressing, asset);
            foreach (var tank in researchTanks) Kit(tank, "ResearchTank", default, null, "Cabinet", "Frame");
            Kit(researchDesk, "ResearchDesk", default, null, "Desk body", "Desk top", "Screen");
            Kit(researchTray, "ResearchTray", default, null, "Tray stand", "Tray glass");
            // The hoarding is built facing -X with the others, so it is turned to face the power room.
            var hoarding = Kit(researchWall.transform, "PartitionResearch", default, null, "Partition", "Coming soon sign", "Sign stripe");
            if (hoarding) hoarding.localRotation = Quaternion.Euler(0, 180, 0);
            if (researchLight) researchLight.transform.localPosition = At(-7, 3.3f, 14);
            labLamps = labLamps.Append(researchLight).ToArray();
        }

        // ---------------------------------------------------------------- what it shows

        void SyncResearch(GrowthShop shop)
        {
            SyncViews(jarViews, shop.Specimens, researchRoot);
            SyncViews(trayViews, shop.ResearchTray, researchTray);
            for (int k = 0; k < researchTanks.Length; k++)
            {
                var job = shop.ResearchRoom ? shop.ResearchInTank(k) : null;
                if (job != tankShows[k]) { tankShows[k] = job; ShowInTank(shop, k, job); }
                int lit = job == null ? 0 : Mathf.Clamp(Mathf.CeilToInt(ResearchTicks * (1 - job.Left / Mathf.Max(0.01f, shop.ResearchSeconds))), 0, ResearchTicks);
                if (lit != ticksLit[k])
                {
                    ticksLit[k] = lit;
                    for (int i = 0; i < ResearchTicks; i++) researchTicks[k][i].sharedMaterial = i < lit ? tickOn : tickOff;
                }
            }
            string shows = string.Join(",", shop.Specimens.Take(ScreenCells).Select(x => x.Species)) + "|" + shop.PairLog.Count + "|" + shop.ResearchJobs.Count;
            if (shows == screenShows) return;
            screenShows = shows;
            for (int r = 0; r < ScreenCells; r++)
                for (int c = 0; c < ScreenCells; c++)
                {
                    if (r == c) continue;
                    bool both = r < shop.Specimens.Count && c < shop.Specimens.Count;
                    string a = both ? shop.Specimens[r].Species : null, b = both ? shop.Specimens[c].Species : null;
                    screenCells[r, c].sharedMaterial = !both ? cellOff : shop.PairEntry(a, b) != null ? cellTried : shop.QueuedPair(a, b) != null ? cellQueued : cellOff;
                }
        }

        void SyncViews(Dictionary<int, GrowthCreatureView> views, List<GrowthSpecimen> creatures, Transform parent)
        {
            foreach (var s in creatures)
                if (!views.ContainsKey(s.Id)) views[s.Id] = GrowthCreatureView.Create(s, parent);
            foreach (var id in views.Keys.Where(k => creatures.All(s => s.Id != k)).ToList()) { Destroy(views[id].gameObject); views.Remove(id); }
        }

        /// <summary>A tank's job as you see it: a pair's two creatures swimming, a mix's doses as glowing drops of each culture.</summary>
        void ShowInTank(GrowthShop shop, int tank, GrowthResearchJob job)
        {
            foreach (var v in tankViews[tank]) Destroy(v.gameObject);
            tankViews[tank].Clear();
            foreach (var d in tankDoses[tank]) Destroy(d.gameObject);
            tankDoses[tank].Clear();
            if (job == null) return;
            if (job.IsMix)
            {
                for (int i = 0; i < job.Doses.Length; i++)
                    if (job.Doses[i] > 0)
                        tankDoses[tank].Add(ShopWorld.Shape(researchTanks[tank], "Mix drop", PrimitiveType.Sphere, new Vector3(0, ResearchWaterY, 0), Vector3.one * (0.05f + 0.015f * job.Doses[i]), culture[GrowthContent.Cultures[i].Id]));
                return;
            }
            int id = -(tank * 2 + 1);
            foreach (var species in new[] { job.A, job.B })
                tankViews[tank].Add(GrowthCreatureView.Create(new GrowthSpecimen { Id = id--, Species = species, Colour = shop.SpecimenOf(species)?.Colour ?? Colour.Classic }, researchTanks[tank]));
        }

        /// <summary>Their small movements, when someone is near enough to see them.</summary>
        void AnimateResearch(float t, GrowthShop shop)
        {
            if (!shop.ResearchRoom || session.Player == null) return;
            var at = session.Player.transform.position;
            if (at.x > S(2) || at.z < S(7)) return;
            int index = 0;
            foreach (var s in shop.Specimens)
            {
                int jar = index++;
                if (!jarViews.TryGetValue(s.Id, out var v) || v == null) continue;
                v.transform.localScale = Vector3.one * (JarLength / Mathf.Max(0.01f, v.Length));
                v.transform.localPosition = JarPosition(jar) + new Vector3(0, 0.012f * Mathf.Sin(t * 1.3f + jar), 0);
                v.transform.localRotation = Quaternion.Euler(0, 90 + 20f * Mathf.Sin(t * 0.5f + jar), 0);
                v.Tick(t, session.ReducedMotion);
            }
            index = 0;
            foreach (var s in shop.ResearchTray)
            {
                int place = index++;
                if (!trayViews.TryGetValue(s.Id, out var v) || v == null) continue;
                v.transform.localScale = Vector3.one * (TrayLength / Mathf.Max(0.01f, v.Length));
                v.transform.localPosition = ResearchTrayPlace(place) + new Vector3(0, 0.012f * Mathf.Sin(t * 1.5f + place), 0);
                v.transform.localRotation = Quaternion.Euler(0, 180 + 18f * Mathf.Sin(t * 0.6f + place), 0);
                v.Tick(t, session.ReducedMotion);
            }
            for (int k = 0; k < researchTanks.Length; k++)
            {
                for (int i = 0; i < tankViews[k].Count; i++)
                {
                    var v = tankViews[k][i];
                    if (v == null) continue;
                    // A small creature is shown a little larger here, so a pair can be told apart from across the room.
                    v.transform.localScale = Vector3.one * Mathf.Max(1, TankLeast / Mathf.Max(0.01f, v.Length));
                    float L = Mathf.Max(v.Length, TankLeast), hx = Mathf.Max(0.02f, 0.5f - L * 0.6f), hz = Mathf.Max(0.02f, 0.19f - L * 0.5f), phase = i * Mathf.PI + k, speed = L > 0.2f ? 0.45f : L > 0.1f ? 0.6f : 0.8f;
                    Vector3 Path(float tt) => new Vector3(hx * Mathf.Sin(0.5f * tt + phase), 0.05f * Mathf.Sin(0.9f * tt + phase), hz * 0.7f * Mathf.Sin(0.8f * tt + phase * 0.5f));
                    Vector3 pos = Path(t * speed), dir = Path(t * speed + 0.2f) - pos;
                    v.transform.localPosition = new Vector3(pos.x, ResearchWaterY + pos.y, pos.z);
                    dir.y *= 0.3f;
                    if (dir.sqrMagnitude > 1e-6f) v.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    v.Tick(t, session.ReducedMotion);
                }
                for (int i = 0; i < tankDoses[k].Count; i++)
                {
                    var d = tankDoses[k][i];
                    float phase = i * 2.1f + k;
                    d.localPosition = new Vector3(0.3f * Mathf.Sin(t * 0.7f + phase), ResearchWaterY + 0.08f * Mathf.Sin(t * 1.1f + phase), 0.1f * Mathf.Cos(t * 0.9f + phase));
                }
            }
        }
    }
}
