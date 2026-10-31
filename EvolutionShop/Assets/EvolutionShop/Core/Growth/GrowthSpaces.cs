using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-10 (GR.11): the greybox's spaces as data the rules, the world and the routes share. The shop is unit A (8 x 8 m,
    // the street door), unit B east of it through an arch (2 stars), and unit C (4 stars) behind unit B and the back
    // room's east wall (10 x 5 m, an L-shaped shop 16 x 13 m), opened from unit B through a second arch; the lab is the
    // back room behind unit A, the lab annex behind it (3 stars) and the workshop east of the annex (4 stars). The user
    // chose (2026-10-23) unit C with a Show tank feature wall (two feature places on its north wall) and four long places,
    // the exhibition hall finish now (5 stars: lighting, banners and two exhibition places along unit B's front wall,
    // either side of the bay window bench), and set-piece placeholders. GR.11's proportions are checked here, not only
    // drawn: every place inside its room and clear of every other, 1.2 m of browse access in front of each, the main aisle
    // 1.6 m wide from the entrance to unit C's arch, the counter's queue inside the shop, and every visitor's and helper's
    // walk clear of the tanks and through doorways only (Problems).

    /// <summary>
    /// The rooms, doorways, the places' positions and the walking network (GR.11). Coordinates are metres on the floor
    /// plane (x east, z north), as the world builds them. A place's yaw turns its front (the side visitors look in from):
    /// 0 faces north, 180 south, 90 east, -90 west.
    /// </summary>
    public static class GrowthSpaces
    {
        public struct Rect
        {
            public float X0, Z0, X1, Z1;
            public Rect(float x0, float z0, float x1, float z1) { X0 = Math.Min(x0, x1); Z0 = Math.Min(z0, z1); X1 = Math.Max(x0, x1); Z1 = Math.Max(z0, z1); }
            public bool Contains((float x, float z) p, float margin = 0) => p.x >= X0 + margin - 1e-4f && p.x <= X1 - margin + 1e-4f && p.z >= Z0 + margin - 1e-4f && p.z <= Z1 - margin + 1e-4f;
            public bool Inside(Rect outer) => X0 >= outer.X0 - 1e-4f && X1 <= outer.X1 + 1e-4f && Z0 >= outer.Z0 - 1e-4f && Z1 <= outer.Z1 + 1e-4f;
            public bool Overlaps(Rect o) => X0 < o.X1 - 1e-4f && o.X0 < X1 - 1e-4f && Z0 < o.Z1 - 1e-4f && o.Z0 < Z1 - 1e-4f;
            public Rect Shrunk(float m) => new Rect(X0 + m, Z0 + m, X1 - m, Z1 - m);
            public (float x, float z) Centre => ((X0 + X1) * 0.5f, (Z0 + Z1) * 0.5f);
            public float Distance((float x, float z) p)
            {
                float dx = Math.Max(0, Math.Max(X0 - p.x, p.x - X1)), dz = Math.Max(0, Math.Max(Z0 - p.z, p.z - Z1));
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }

        /// <summary>A room: the expansion that opens it (unit-a, unit-b, unit-c, annex, workshop; the back room comes with
        /// unit A), whether visitors walk there, its walls' outline and its ceiling.</summary>
        public sealed class Room
        {
            public string Id, Name, Expansion;
            public bool Shop;
            public Rect Area;
            public float Ceiling;
        }

        /// <summary>An opening in a wall between two rooms (or a room and the street): its span along the wall.</summary>
        public sealed class Doorway
        {
            public string Id, A, B;
            public (float x, float z) From, To;
            public bool Vertical => Math.Abs(From.x - To.x) < 1e-4f;
        }

        /// <summary>A container place's position: its centre, which way its front faces, where a visitor stands to look in,
        /// and the visitors' route to it from the hub inside the entrance (waypoint names).</summary>
        public sealed class Spot
        {
            public string Id;
            public (float x, float z) Centre, Browse;
            public int Yaw;
            public string[] Route;
        }

        public const float WallThickness = 0.2f, BrowseAccess = 1.2f, MainAisle = 1.6f, Body = 0.25f, ShopCeiling = 3.2f, LabCeiling = 3.6f;
        /// <summary>Visitors walk at 2 m/s and look into a tank for at least a second once they are at it (GR-10: a visitor's
        /// look counts from the door, so one bound for a far tank has the time to walk there).</summary>
        public const float VisitorSpeed = 2f, LookSeconds = 1f;

        /// <summary>GR-32 (F56): the places with no wall behind them, in the middle of a room: seen from every side, their tanks have no backdrop.</summary>
        public static bool FreeStanding(string place) => place == "a3" || place == "b3";

        public static readonly Room[] Rooms =
        {
            new Room { Id = "unit-a", Name = "shop unit A", Expansion = "unit-a", Shop = true, Area = new Rect(-4, -4, 4, 4), Ceiling = ShopCeiling },
            new Room { Id = "unit-b", Name = "shop unit B", Expansion = "unit-b", Shop = true, Area = new Rect(4, -4, 12, 4), Ceiling = ShopCeiling },
            new Room { Id = "unit-c", Name = "shop unit C", Expansion = "unit-c", Shop = true, Area = new Rect(2, 4, 12, 9), Ceiling = ShopCeiling },
            new Room { Id = "back", Name = "the laboratory", Expansion = "unit-a", Shop = false, Area = new Rect(-4, 4, 2, 9), Ceiling = LabCeiling },
            new Room { Id = "annex", Name = "the power room", Expansion = "annex", Shop = false, Area = new Rect(-4, 9, 4, 17), Ceiling = LabCeiling },
            new Room { Id = "workshop", Name = "the workshop", Expansion = "workshop", Shop = false, Area = new Rect(4, 9, 12, 15), Ceiling = LabCeiling },
        };

        /// <summary>The street outside the entrance (visitors arrive and leave there).</summary>
        public static readonly Rect Street = new Rect(-5, -8, 13, -4);

        public static readonly Doorway[] Doorways =
        {
            new Doorway { Id = "entrance", A = "street", B = "unit-a", From = (-1.2f, -4), To = (0.2f, -4) },
            new Doorway { Id = "arch-ab", A = "unit-a", B = "unit-b", From = (4, -1.6f), To = (4, 1.6f) },
            new Doorway { Id = "arch-bc", A = "unit-b", B = "unit-c", From = (9.9f, 4), To = (11.85f, 4) },
            new Doorway { Id = "back-door", A = "unit-a", B = "back", From = (0.2f, 4), To = (1.4f, 4) },
            new Doorway { Id = "annex-door", A = "back", B = "annex", From = (0.45f, 9), To = (1.85f, 9) },
            new Doorway { Id = "workshop-door", A = "annex", B = "workshop", From = (4, 9.6f), To = (4, 11) },
        };

        /// <summary>The walking network's waypoints: the visitors' (the street, the door, the hub inside it and the aisles),
        /// and the helpers' way in from the back room's door (BackA).</summary>
        public static readonly Dictionary<string, (float x, float z)> Points = new Dictionary<string, (float x, float z)>
        {
            ["Outside"] = (-0.5f, -5.8f), ["Door"] = (-0.5f, -3.3f), ["Hub"] = (-0.5f, -1.6f),
            ["West"] = (-2.2f, -1.2f), ["NorthWest"] = (-2.2f, 2.0f), ["East"] = (1.5f, -1.0f), ["BackA"] = (1.1f, 2.3f),
            ["ArchWest"] = (3.3f, 0), ["ArchEast"] = (4.7f, 0), ["UnitBWest"] = (5.6f, 1.55f), ["UnitBNorth"] = (8.0f, 1.6f), ["UnitBSouth"] = (5.6f, -1.4f),
            ["UnitBSouthEast"] = (8.9f, -1.55f), ["UnitBCorner"] = (10.05f, 2.05f), ["ArchCSouth"] = (10.9f, 2.9f), ["ArchC"] = (10.9f, 4.0f),
            ["ArchCNorth"] = (10.45f, 5.6f), ["UnitCAisle"] = (6.0f, 6.25f),
        };

        /// <summary>The walking network's paths between waypoints (visitors keep to them; helpers too, in the shop).</summary>
        public static readonly (string a, string b)[] Edges =
        {
            ("Outside", "Door"), ("Door", "Hub"), ("Hub", "West"), ("West", "NorthWest"), ("Hub", "East"), ("West", "East"),
            ("East", "ArchWest"), ("ArchWest", "ArchEast"), ("ArchEast", "UnitBSouth"), ("ArchEast", "UnitBNorth"), ("ArchEast", "UnitBWest"), ("UnitBWest", "UnitBNorth"),
            ("UnitBSouth", "UnitBSouthEast"), ("UnitBNorth", "UnitBCorner"), ("UnitBCorner", "ArchCSouth"), ("ArchCSouth", "ArchC"), ("ArchC", "ArchCNorth"),
            ("ArchCNorth", "UnitCAisle"), ("BackA", "NorthWest"), ("BackA", "East"),
        };

        /// <summary>The main aisle (GR.11: 1.6 m, two visitors abreast), from inside the entrance to unit C's arch.</summary>
        public static readonly string[] Spine = { "Hub", "East", "ArchWest", "ArchEast", "UnitBWest", "UnitBNorth", "UnitBCorner", "ArchCSouth", "ArchC" };

        static readonly string[] ToB = { "East", "ArchWest", "ArchEast" };
        static readonly string[] ToC = { "East", "ArchWest", "ArchEast", "UnitBNorth", "UnitBCorner", "ArchCSouth", "ArchC", "ArchCNorth" };
        static string[] With(string[] route, params string[] more) => route.Concat(more).ToArray();

        public static readonly Spot[] Spots =
        {
            new Spot { Id = "a1", Centre = (-2.2f, 3.35f), Yaw = 180, Browse = (-2.2f, 2.2f), Route = new[] { "West", "NorthWest" } },
            new Spot { Id = "a2", Centre = (-3.35f, 0.9f), Yaw = 90, Browse = (-2.2f, 0.9f), Route = new[] { "West" } },
            new Spot { Id = "a3", Centre = (-0.4f, 0.2f), Yaw = 180, Browse = (-0.4f, -0.95f), Route = new string[0] },
            new Spot { Id = "a4", Centre = (-3.35f, -1.6f), Yaw = 90, Browse = (-2.2f, -1.6f), Route = new[] { "West" } },
            new Spot { Id = "b1", Centre = (5.6f, 3.35f), Yaw = 180, Browse = (5.6f, 2.2f), Route = ToB },
            new Spot { Id = "b2", Centre = (8.4f, 3.35f), Yaw = 180, Browse = (8.4f, 2.2f), Route = ToB },
            new Spot { Id = "b3", Centre = (7.6f, -0.2f), Yaw = 180, Browse = (7.6f, -1.35f), Route = With(ToB, "UnitBSouth") },
            new Spot { Id = "b4", Centre = (11.15f, -0.2f), Yaw = -90, Browse = (9.8f, -0.2f), Route = With(ToB, "UnitBNorth") },
            new Spot { Id = "c1", Centre = (3.75f, 8.15f), Yaw = 180, Browse = (3.75f, 6.2f), Route = With(ToC, "UnitCAisle") },
            new Spot { Id = "c2", Centre = (8.2f, 8.15f), Yaw = 180, Browse = (8.2f, 6.2f), Route = ToC },
            new Spot { Id = "c3", Centre = (3.45f, 4.6f), Yaw = 0, Browse = (3.45f, 6.3f), Route = With(ToC, "UnitCAisle") },
            new Spot { Id = "c4", Centre = (6.1f, 4.6f), Yaw = 0, Browse = (6.1f, 6.3f), Route = With(ToC, "UnitCAisle") },
            new Spot { Id = "c5", Centre = (8.75f, 4.6f), Yaw = 0, Browse = (8.75f, 6.3f), Route = ToC },
            new Spot { Id = "c6", Centre = (11.4f, 7.25f), Yaw = -90, Browse = (10.1f, 7.0f), Route = ToC },
            new Spot { Id = "e1", Centre = (5.65f, -3.4f), Yaw = 0, Browse = (5.65f, -1.7f), Route = With(ToB, "UnitBSouth") },
            new Spot { Id = "e2", Centre = (10.2f, -3.4f), Yaw = 0, Browse = (9.9f, -1.75f), Route = With(ToB, "UnitBSouth", "UnitBSouthEast") },
        };

        /// <summary>The shop's fixed pieces on the floor, which places and walks keep clear of: the counter, the supplier
        /// catalog's stand, unit B's bay window bench and unit C's feature tank (set pieces, GR.11).</summary>
        public static readonly (string id, Rect area)[] Fixtures =
        {
            ("counter", new Rect(1.75f, -2.975f, 3.85f, -2.225f)),
            ("catalog", new Rect(0.475f, -3.475f, 0.925f, -3.125f)), // GR-31 (F24): moved 0.5 m west: a way behind the counter
            ("bay window bench", new Rect(7.1f, -3.9f, 8.9f, -3.45f)),
            ("feature tank", new Rect(5.35f, 8.6f, 6.6f, 8.9f)),
        };

        /// <summary>The counter's queue, first in line nearest it (inside the shop, GR.11).</summary>
        public static readonly (float x, float z)[] QueueSpots = { (2.8f, -1.7f), (2.8f, -0.95f), (2.8f, -0.2f), (2.05f, -0.2f), (1.3f, -0.2f) };

        public static Spot SpotOf(string place) => Spots.FirstOrDefault(s => s.Id == place);
        public static Room RoomById(string id) => Rooms.First(r => r.Id == id);
        public static Room RoomAt((float x, float z) p) => Rooms.FirstOrDefault(r => r.Area.Contains(p));
        public static Room RoomOfPlace(string place) => RoomById(GrowthShop.Place(place).Unit == "hall" ? "unit-b" : GrowthShop.Place(place).Unit);
        public static (float x, float z) Front(int yaw) => yaw == 0 ? (0f, 1f) : yaw == 180 ? (0f, -1f) : yaw == 90 ? (1f, 0f) : (-1f, 0f);

        /// <summary>A place's footprint on the floor (its width along its front, its depth from back to front).</summary>
        public static Rect Footprint(string place)
        {
            var s = SpotOf(place); var p = GrowthShop.Place(place);
            bool across = s.Yaw == 90 || s.Yaw == -90;
            float hx = (across ? p.Depth : p.Width) * 0.5f, hz = (across ? p.Width : p.Depth) * 0.5f;
            return new Rect(s.Centre.x - hx, s.Centre.z - hz, s.Centre.x + hx, s.Centre.z + hz);
        }

        /// <summary>The browse access in front of a place: its width, 1.2 m deep (GR.11).</summary>
        public static Rect Access(string place)
        {
            var s = SpotOf(place); var f = Front(s.Yaw); var r = Footprint(place);
            if (f.z > 0) return new Rect(r.X0, r.Z1, r.X1, r.Z1 + BrowseAccess);
            if (f.z < 0) return new Rect(r.X0, r.Z0 - BrowseAccess, r.X1, r.Z0);
            if (f.x > 0) return new Rect(r.X1, r.Z0, r.X1 + BrowseAccess, r.Z1);
            return new Rect(r.X0 - BrowseAccess, r.Z0, r.X0, r.Z1);
        }

        /// <summary>The visitors' walk to a place, from the street: the street, the door, the hub, its route, its browse spot.</summary>
        public static List<(float x, float z)> VisitorWalk(string place)
        {
            var s = SpotOf(place);
            var walk = new List<(float x, float z)> { Points["Outside"], Points["Door"], Points["Hub"] };
            walk.AddRange(s.Route.Select(n => Points[n]));
            walk.Add(s.Browse);
            return walk;
        }

        /// <summary>A visitor's walk from one place's browse spot to the next in their plan (as the world walks them: back
        /// along the first place's route to where the two routes part, or the hub, then along the second's).</summary>
        public static List<(float x, float z)> VisitorWalkBetween(string from, string to)
        {
            var f = SpotOf(from); var t = SpotOf(to);
            var walk = new List<(float x, float z)> { f.Browse };
            int shared = 0;
            while (shared < f.Route.Length && shared < t.Route.Length && f.Route[shared] == t.Route[shared]) shared++;
            for (int i = f.Route.Length - 1; i >= shared; i--) walk.Add(Points[f.Route[i]]);
            walk.Add(shared == 0 ? Points["Hub"] : Points[f.Route[shared - 1]]);
            for (int i = shared; i < t.Route.Length; i++) walk.Add(Points[t.Route[i]]);
            walk.Add(t.Browse);
            return walk;
        }

        public static float Length(IList<(float x, float z)> walk) { float n = 0; for (int i = 1; i < walk.Count; i++) n += Distance(walk[i - 1], walk[i]); return n; }

        /// <summary>How long a visitor bound for a place browses from the door: the shop's usual look, or the walk there and a
        /// second's look when that is longer (unit A's places keep the usual 6 s; units B and C's add their walk).</summary>
        public static float FirstLook(string place) => Math.Max(GrowthShop.BrowseSeconds, Length(VisitorWalk(place)) / VisitorSpeed + LookSeconds);

        /// <summary>The same for the next place in a visitor's plan, from the one they looked into.</summary>
        public static float NextLook(string from, string to) => Math.Max(GrowthShop.NextBrowseSeconds, Length(VisitorWalkBetween(from, to)) / VisitorSpeed + LookSeconds);

        // ---------------------------------------------------------------- the shop's walking network (helpers)

        /// <summary>The shortest way along the network between two waypoints (the same every time: ties keep the first).</summary>
        public static List<string> NetworkPath(string from, string to)
        {
            var dist = Points.Keys.ToDictionary(k => k, k => float.MaxValue);
            var prev = new Dictionary<string, string>();
            var open = new List<string>(Points.Keys);
            dist[from] = 0;
            while (open.Count > 0)
            {
                var u = open.OrderBy(k => dist[k]).ThenBy(k => k, StringComparer.Ordinal).First();
                open.Remove(u);
                if (u == to || dist[u] == float.MaxValue) break;
                foreach (var (a, b) in Edges)
                {
                    string v = a == u ? b : b == u ? a : null;
                    if (v == null || !open.Contains(v)) continue;
                    float d = dist[u] + Distance(Points[u], Points[v]);
                    if (d < dist[v] - 1e-5f) { dist[v] = d; prev[v] = u; }
                }
            }
            var path = new List<string> { to };
            while (path[0] != from) { if (!prev.TryGetValue(path[0], out var p)) return null; path.Insert(0, p); }
            return path;
        }

        /// <summary>The waypoint a shop point joins the network at: a place's browse spot its route's last waypoint (the hub
        /// for a3), else the nearest waypoint.</summary>
        public static string JoinOf((float x, float z) p)
        {
            var s = Spots.FirstOrDefault(x => Distance(x.Browse, p) < 1e-3f);
            if (s != null) return s.Route.Length > 0 ? s.Route[s.Route.Length - 1] : "Hub";
            if (Distance(p, (0.8f, 3.5f)) < 1e-3f) return "BackA";
            return Points.Where(kv => kv.Key != "Outside" && kv.Key != "Door").OrderBy(kv => Distance(kv.Value, p)).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
        }

        public static float Distance((float x, float z) a, (float x, float z) b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

        // ---------------------------------------------------------------- walls and clearance

        /// <summary>Every wall of the rooms (and the street front) as a segment on the floor, doorways left out.</summary>
        public static List<((float x, float z) a, (float x, float z) b)> Walls()
        {
            var walls = new List<((float x, float z), (float x, float z))>();
            foreach (var r in Rooms)
            {
                var a = r.Area;
                foreach (var edge in new[] { ((a.X0, a.Z0), (a.X1, a.Z0)), ((a.X1, a.Z0), (a.X1, a.Z1)), ((a.X0, a.Z1), (a.X1, a.Z1)), ((a.X0, a.Z0), (a.X0, a.Z1)) })
                    foreach (var piece in Cut(edge.Item1, edge.Item2)) walls.Add(piece);
            }
            return walls;
        }

        static IEnumerable<((float x, float z), (float x, float z))> Cut((float x, float z) a, (float x, float z) b)
        {
            bool vertical = Math.Abs(a.x - b.x) < 1e-4f;
            float lo = vertical ? a.z : a.x, hi = vertical ? b.z : b.x, line = vertical ? a.x : a.z;
            var spans = new List<(float lo, float hi)> { (lo, hi) };
            foreach (var d in Doorways.Where(d => d.Vertical == vertical && Math.Abs((vertical ? d.From.x : d.From.z) - line) < 1e-4f))
            {
                float g0 = vertical ? Math.Min(d.From.z, d.To.z) : Math.Min(d.From.x, d.To.x), g1 = vertical ? Math.Max(d.From.z, d.To.z) : Math.Max(d.From.x, d.To.x);
                spans = spans.SelectMany(s => g1 <= s.lo || g0 >= s.hi ? new[] { s } : new[] { (s.lo, g0), (g1, s.hi) }.Where(x => x.Item2 - x.Item1 > 1e-4f)).ToList();
            }
            foreach (var s in spans) yield return vertical ? ((line, s.lo), (line, s.hi)) : ((s.lo, line), (s.hi, line));
        }

        /// <summary>The distance from a point to a segment.</summary>
        public static float SegmentDistance((float x, float z) p, (float x, float z) a, (float x, float z) b)
        {
            float dx = b.x - a.x, dz = b.z - a.z, len = dx * dx + dz * dz;
            float t = len < 1e-8f ? 0 : Math.Max(0, Math.Min(1, ((p.x - a.x) * dx + (p.z - a.z) * dz) / len));
            return Distance(p, (a.x + dx * t, a.z + dz * t));
        }

        /// <summary>The closest a walk segment comes to a rectangle (sampled every 5 cm).</summary>
        static float Clearance((float x, float z) a, (float x, float z) b, Rect r)
        {
            int n = Math.Max(1, (int)Math.Ceiling(Distance(a, b) / 0.05f));
            float best = float.MaxValue;
            for (int i = 0; i <= n; i++) best = Math.Min(best, r.Distance((a.x + (b.x - a.x) * i / n, a.z + (b.z - a.z) * i / n)));
            return best;
        }

        /// <summary>The closest a walk segment comes to a wall face (walls are 0.2 m thick).</summary>
        static float WallClearance((float x, float z) a, (float x, float z) b, List<((float x, float z) a, (float x, float z) b)> walls)
        {
            int n = Math.Max(1, (int)Math.Ceiling(Distance(a, b) / 0.05f));
            float best = float.MaxValue;
            for (int i = 0; i <= n; i++)
            {
                var p = (a.x + (b.x - a.x) * i / n, a.z + (b.z - a.z) * i / n);
                foreach (var w in walls) best = Math.Min(best, SegmentDistance(p, w.a, w.b) - WallThickness * 0.5f);
            }
            return best;
        }

        /// <summary>Whether a walk segment crosses a wall (rather than passing through a doorway).</summary>
        static bool CrossesWall((float x, float z) a, (float x, float z) b, List<((float x, float z) a, (float x, float z) b)> walls)
        {
            foreach (var w in walls)
            {
                float d1 = Side(w.a, w.b, a), d2 = Side(w.a, w.b, b), d3 = Side(a, b, w.a), d4 = Side(a, b, w.b);
                if ((d1 > 1e-5f && d2 < -1e-5f || d1 < -1e-5f && d2 > 1e-5f) && (d3 > 1e-5f && d4 < -1e-5f || d3 < -1e-5f && d4 > 1e-5f)) return true;
            }
            return false;
        }

        static float Side((float x, float z) a, (float x, float z) b, (float x, float z) p) => (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);

        // ---------------------------------------------------------------- GR.11's rules, checked

        /// <summary>
        /// What breaks GR.11's proportions and navigation in the layout, as sentences (none when it holds): every place inside
        /// its room and clear of the others and the fixtures, its browse access clear, its browse spot in its access; every
        /// visitor's walk (to each place, between them, to the queue and out) and every helper's walk to a shop place through
        /// doorways only and at least a body's width (0.25 m) from tanks, fixtures and walls; the queue inside unit A and clear;
        /// the main aisle 1.6 m wide; the shop's ceilings 3.2 m and the lab's 3.6 m; and every room's place counts as the
        /// content tables have them.
        /// </summary>
        public static List<string> Problems()
        {
            var problems = new List<string>();
            var walls = Walls();
            var obstacles = Spots.Select(s => (s.Id, Footprint(s.Id))).Concat(Fixtures).ToList();
            foreach (var r in Rooms)
                if (Math.Abs(r.Ceiling - (r.Shop ? ShopCeiling : LabCeiling)) > 1e-4f) problems.Add(r.Name + "'s ceiling is not " + (r.Shop ? ShopCeiling : LabCeiling) + " m");
            foreach (var p in GrowthShop.Places)
            {
                var s = SpotOf(p.Id);
                if (s == null) { problems.Add(p.Id + " has no position"); continue; }
                var room = RoomOfPlace(p.Id).Area.Shrunk(WallThickness * 0.5f);
                var foot = Footprint(p.Id); var access = Access(p.Id);
                if (!foot.Inside(room)) problems.Add(p.Id + " is not inside its room");
                if (!access.Inside(room)) problems.Add(p.Id + "'s browse access leaves its room");
                foreach (var (id, area) in obstacles.Where(o => o.Item1 != p.Id))
                {
                    if (foot.Overlaps(area)) problems.Add(p.Id + " overlaps " + id);
                    if (access.Overlaps(area)) problems.Add(p.Id + "'s browse access is blocked by " + id);
                }
                if (!access.Contains(s.Browse)) problems.Add(p.Id + "'s browse spot is not in front of it");
            }
            foreach (var (a, b) in Edges)
                problems.AddRange(WalkProblems("the path " + a + "-" + b, new List<(float x, float z)> { Points[a], Points[b] }, obstacles, walls, Body));
            foreach (var s in Spots) problems.AddRange(WalkProblems("the visitors' walk to " + s.Id, VisitorWalk(s.Id), obstacles, walls, Body));
            var unitA = RoomById("unit-a").Area.Shrunk(WallThickness * 0.5f);
            for (int i = 0; i < QueueSpots.Length; i++)
            {
                var q = QueueSpots[i];
                if (!unitA.Contains(q, Body)) problems.Add("queue spot " + (i + 1) + " is not inside the shop");
                foreach (var (id, area) in obstacles) if (area.Distance(q) < Body) problems.Add("queue spot " + (i + 1) + " is against " + id);
                var from = i == 0 ? Points["East"] : QueueSpots[i - 1];
                problems.AddRange(WalkProblems("the queue to spot " + (i + 1), new List<(float x, float z)> { Points["East"], q }, obstacles, walls, Body));
            }
            var spine = Spine.Select(n => Points[n]).ToList();
            problems.AddRange(WalkProblems("the main aisle", spine, obstacles, walls, MainAisle * 0.5f));
            foreach (var helper in new[] { GrowthWalk.NoriHome, GrowthWalk.MossHome, GrowthWalk.Tray, GrowthWalk.Nursery })
                foreach (var s in Spots)
                    problems.AddRange(WalkProblems("a helper's walk to " + s.Id, GrowthWalk.Path(helper, s.Browse), obstacles, walls, Body, shopOnly: true));
            foreach (var e in GrowthContent.Expansions.Where(x => x.ContainerPlaces > 0))
            {
                int places = GrowthShop.Places.Count(p => p.Unit == (e.Id == "exhibition-hall" ? "hall" : e.Id));
                if (places != e.ContainerPlaces) problems.Add(e.Name + " has " + places + " places, not " + e.ContainerPlaces);
            }
            return problems.Distinct().ToList();
        }

        static IEnumerable<string> WalkProblems(string what, List<(float x, float z)> walk, List<(string id, Rect area)> obstacles, List<((float x, float z) a, (float x, float z) b)> walls, float clearance, bool shopOnly = false)
        {
            for (int i = 1; i < walk.Count; i++)
            {
                var a = walk[i - 1]; var b = walk[i];
                if (CrossesWall(a, b, walls)) yield return what + " goes through a wall";
                bool inShop = Shop(a) || Shop(b);
                if (shopOnly && !inShop) continue;
                foreach (var (id, area) in obstacles)
                    if (Clearance(a, b, area) < clearance - 1e-3f) yield return what + " passes " + Clearance(a, b, area).ToString("0.00") + " m from " + id;
                if ((!shopOnly || Shop(a) && Shop(b)) && WallClearance(a, b, walls) < clearance - 1e-3f) yield return what + " passes " + WallClearance(a, b, walls).ToString("0.00") + " m from a wall";
            }
        }

        static bool Shop((float x, float z) p) { var r = RoomAt(p); return r != null && r.Shop; }
    }

    // ---------------------------------------------------------------- unit C and the exhibition hall finish (GR-10)

    public sealed partial class GrowthShop
    {
        /// <summary>GR-10: shop unit C (4 stars) and the exhibition hall finish (5 stars) are open.</summary>
        public bool UnitC, Hall;

        public static ExpansionDef UnitCDef => GrowthContent.Expansions.First(e => e.Id == "unit-c");
        public static ExpansionDef HallDef => GrowthContent.Expansions.First(e => e.Id == "exhibition-hall");

        /// <summary>Whether the unit a place belongs to is open.</summary>
        public bool UnitOpen(string unit) => unit == "unit-a" || unit == "unit-b" && UnitB || unit == "unit-c" && UnitC || unit == "hall" && Hall;

        public GrowthFailure UnitCCheck(out string why)
        {
            var unit = UnitCDef;
            why = null;
            if (UnitC) { why = "Already open"; return GrowthFailure.MaxLevel; }
            if (!UnitB) { why = "Open unit B first: unit C is through its north wall"; return GrowthFailure.Locked; }
            if (Era < unit.Era) { why = "Needs Era " + unit.Era; return GrowthFailure.Locked; }
            if (Stars < unit.Stars) { why = "Needs " + unit.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Walls open at night"; return GrowthFailure.NotAtNight; }
            if (Credits < unit.Credits) { why = "Needs " + unit.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Opens the arch in unit B's north wall into shop unit C: six more places (two for Show tanks on its feature
        /// wall) and one more visitor at a time.</summary>
        public GrowthResult BuyUnitC()
        {
            var failure = UnitCCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= UnitCDef.Credits;
            UnitC = true;
            Raise(GrowthEventKind.Opened, "Shop unit C opened", amount: UnitCDef.Credits);
            return GrowthResult.Ok();
        }

        public GrowthFailure HallCheck(out string why)
        {
            var hall = HallDef;
            why = null;
            if (Hall) { why = "Already finished"; return GrowthFailure.MaxLevel; }
            if (!UnitC) { why = "Open unit C first"; return GrowthFailure.Locked; }
            if (Era < hall.Era) { why = "Needs Era " + hall.Era; return GrowthFailure.Locked; }
            if (Stars < hall.Stars) { why = "Needs " + hall.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "The hall is finished at night"; return GrowthFailure.NotAtNight; }
            if (Credits < hall.Credits) { why = "Needs " + hall.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Finishes the shop as an exhibition hall (5 stars): lighting and banners, two exhibition places along unit
        /// B's front wall and one more visitor at a time; the Grand Exhibition's rules come with GR-11.</summary>
        public GrowthResult BuyHall()
        {
            var failure = HallCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= HallDef.Credits;
            Hall = true;
            Raise(GrowthEventKind.Opened, "The exhibition hall is finished", amount: HallDef.Credits);
            return GrowthResult.Ok();
        }

        /// <summary>A save's spaces: unit C needs unit B, the hall unit C, and every container stands in an open unit.</summary>
        static string RestoreSpaces(GrowthShop shop)
        {
            if (shop.UnitC && !shop.UnitB) return "Unit C without unit B";
            if (shop.Hall && !shop.UnitC) return "The exhibition hall without unit C";
            foreach (var c in shop.Containers)
            {
                var p = Place(c.Place);
                if (p != null && !shop.UnitOpen(p.Unit)) return "A tank in a unit that is not open: " + c.Place;
            }
            return null;
        }
    }
}
