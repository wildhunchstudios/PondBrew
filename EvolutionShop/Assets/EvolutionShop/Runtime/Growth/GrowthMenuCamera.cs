using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-27: the main menu's view of the live shop. The player's own camera is lent to the menu: it drifts slowly past the
    /// shop's stocked tanks (the fullest first), then the counter and the back room's growers and incubator, each shot a short
    /// sideways glide while it looks at its thing, which stands right of the menu's panel; the shots dissolve through a dark
    /// fade. Every viewpoint is found in the rooms as they are: room to stand, inside the walls, a clear view of the thing, and
    /// room for the glide. Reduced motion holds each shot still. Leaving the menu gives the camera back as it was.
    /// </summary>
    public sealed class GrowthMenuCamera
    {
        public const float ShotSeconds = 10f, FadeSeconds = 0.8f, Glide = 0.45f;
        /// <summary>Where the thing looked at stands across the screen (the menu's panel covers the left third).</summary>
        public const float TargetAcross = 0.66f;
        public const int MostShots = 6, MostTanks = 4;

        public struct Shot { public Vector3 From, To, Look; public string Name; public GrowthTarget Target; }

        readonly GrowthSession s;
        readonly List<Shot> shots = new List<Shot>();
        Vector3 savedPosition; Quaternion savedRotation;
        float clock;

        public GrowthMenuCamera(GrowthSession session) { s = session; }

        public bool Active { get; private set; }
        /// <summary>How dark the fade between shots is now (0 clear, 1 dark).</summary>
        public float Fade { get; private set; }
        public int ShotIndex { get; private set; }
        public IReadOnlyList<Shot> Shots => shots;
        public Shot Current => shots.Count == 0 ? default : shots[ShotIndex];

        public void Begin()
        {
            if (Active) return;
            Active = true;
            var view = s.Player.View.transform;
            savedPosition = view.localPosition; savedRotation = view.localRotation;
            if (s.Hands != null) s.Hands.gameObject.SetActive(false);
            Build();
            ShotIndex = 0; clock = 0;
            Place();
        }

        public void End()
        {
            if (!Active) return;
            Active = false; Fade = 0;
            var view = s.Player.View.transform;
            view.localPosition = savedPosition; view.localRotation = savedRotation;
            if (s.Hands != null) s.Hands.gameObject.SetActive(true);
        }

        public void Step(float dt)
        {
            if (!Active) return;
            clock += dt;
            if (clock >= ShotSeconds) { clock -= ShotSeconds; if (shots.Count > 0) ShotIndex = (ShotIndex + 1) % shots.Count; }
            Place();
        }

        /// <summary>Validation: moves to a shot's middle at once.</summary>
        public void ShowShot(int index) { if (shots.Count == 0) return; ShotIndex = ((index % shots.Count) + shots.Count) % shots.Count; clock = ShotSeconds * 0.5f; Place(); }

        void Place()
        {
            var cam = s.Player.View;
            if (shots.Count == 0) { Fade = 0; return; }
            var shot = shots[ShotIndex];
            bool many = shots.Count > 1;
            Fade = !many ? 0 : clock < FadeSeconds ? 1 - clock / FadeSeconds : clock > ShotSeconds - FadeSeconds ? (clock - (ShotSeconds - FadeSeconds)) / FadeSeconds : 0;
            float t = s.ReducedMotion ? 0.5f : many ? Mathf.SmoothStep(0, 1, clock / ShotSeconds) : Mathf.SmoothStep(0, 1, Mathf.PingPong(clock / ShotSeconds, 1));
            Vector3 at = Vector3.Lerp(shot.From, shot.To, t);
            var look = Quaternion.LookRotation(shot.Look - at);
            cam.transform.SetPositionAndRotation(at, Quaternion.AngleAxis(-YawShift(cam), Vector3.up) * look);
        }

        /// <summary>How far to turn left so the thing looked at stands at TargetAcross of the screen's width.</summary>
        static float YawShift(Camera cam)
        {
            float halfWide = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect);
            return Mathf.Atan((TargetAcross * 2 - 1) * Mathf.Tan(halfWide)) * Mathf.Rad2Deg;
        }

        /// <summary>The shots: up to four stocked tanks in the shop (the fullest first), then the counter, a grower and the incubator.</summary>
        void Build()
        {
            shots.Clear();
            var targets = Object.FindObjectsByType<GrowthTarget>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled).ToList();
            var wanted = new List<(GrowthTarget target, string name)>();
            foreach (var c in s.Shop.ShopContainers.Where(c => c.Occupants.Any()).OrderByDescending(c => c.Occupants.Count).ThenBy(c => c.Id).Take(MostTanks))
            {
                var t = targets.FirstOrDefault(x => x.Kind == GrowthTargetKind.Tank && x.Id == c.Id);
                if (t != null) wanted.Add((t, c.Def.Name + " " + c.Place));
            }
            foreach (var kind in new[] { GrowthTargetKind.Counter, GrowthTargetKind.Grower, GrowthTargetKind.Incubator })
                foreach (var t in targets.Where(x => x.Kind == kind).OrderBy(x => x.Id).Take(1))
                    wanted.Add((t, kind + (kind == GrowthTargetKind.Grower ? " " + t.Id : "")));
            foreach (var (target, name) in wanted)
            {
                if (shots.Count >= MostShots) break;
                if (Find(target, out var shot)) { shot.Name = name; shots.Add(shot); }
            }
        }

        /// <summary>A viewpoint for a thing: room to stand (and to glide), inside the rooms, a clear view of it; the one with the most
        /// room behind it, so the thing is framed with its surroundings.</summary>
        static bool Find(GrowthTarget target, out Shot best)
        {
            best = default;
            var col = target.GetComponentInChildren<Collider>();
            Vector3 point = col != null ? col.bounds.center : target.transform.position + Vector3.up;
            float eye = Mathf.Clamp(point.y + 0.45f, 1.3f, 1.7f), bestScore = -1;
            // Far enough to see the whole thing with room around it: a tank 2.1 m off, a long counter further.
            float size = col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) : 0.5f, near = Mathf.Max(2.1f, size * 2.6f);
            foreach (float r in new[] { near, near * 0.82f, near * 1.25f })
                for (int a = 0; a < 16; a++)
                {
                    float angle = a * 22.5f * Mathf.Deg2Rad;
                    var at = new Vector3(point.x + Mathf.Sin(angle) * r, eye, point.z + Mathf.Cos(angle) * r);
                    Vector3 to = point - at, flat = new Vector3(to.x, 0, to.z).normalized, side = Vector3.Cross(Vector3.up, flat);
                    Vector3 from = at - side * Glide, end = at + side * Glide;
                    if (!Room(from, eye) || !Room(at, eye) || !Room(end, eye)) continue;
                    if (!Inside(at) || !Sees(at, point, target) || !Sees(from, point, target) || !Sees(end, point, target)) continue;
                    float behind = Physics.Raycast(at, -flat, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.distance : 4f;
                    float score = behind - Mathf.Abs(r - near);
                    if (score > bestScore) { bestScore = score; best = new Shot { From = from, To = end, Look = point, Target = target }; }
                }
            return bestScore >= 0;
        }

        static bool Room(Vector3 at, float eye) =>
            !Physics.CheckCapsule(new Vector3(at.x, 0.35f, at.z), new Vector3(at.x, eye + 0.1f, at.z), 0.22f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        /// <summary>Inside the rooms: walls within reach on at least three sides (the rooms are closed boxes).</summary>
        static bool Inside(Vector3 at)
        {
            int walls = 0;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (Physics.Raycast(at, d, 14f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) walls++;
            return walls >= 3;
        }

        static bool Sees(Vector3 at, Vector3 point, GrowthTarget target)
        {
            Vector3 to = point - at;
            if (!Physics.Raycast(at, to.normalized, out var hit, to.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            var t = hit.collider.GetComponentInParent<GrowthTarget>();
            return t == target;
        }
    }
}
