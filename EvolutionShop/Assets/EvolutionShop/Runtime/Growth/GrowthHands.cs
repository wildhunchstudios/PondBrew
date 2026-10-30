using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>What the player is doing, as the hands show it (GR-18 wave 3): the session fills it in each frame from the
    /// rules and the input; the validation tests fill it in themselves.</summary>
    public struct GrowthHandsState
    {
        /// <summary>A card or a menu is open: the hands are lowered.</summary>
        public bool CardOpen;
        public bool Collecting, Serving, Cranking, Pumping, Piping, Reduced;
        /// <summary>How far peek has eased in (0 to 1).</summary>
        public float Peek;
        /// <summary>The siphon's time for one dose, and how far the counter's bagging has come (0 to 1).</summary>
        public float HoldSeconds, ServeFraction;
        /// <summary>The player's speed over the floor (m/s) and how fast the view turns (degrees a second, yaw and pitch).</summary>
        public float Speed, YawRate, PitchRate;
        /// <summary>The camera's field of view now and without peek.</summary>
        public float Fov, NormalFov;
        public int SiphonLevel, SiphonCapacity;
        /// <summary>The shop's sales so far (a sale while serving hands the bag over).</summary>
        public int Sales;
        public IList<GrowthSpecimen> Carried;
        public IList<string> Held;
    }

    /// <summary>
    /// GR-18 wave 3: the player's hands in first person, in the look the user approved (GR.55): the shop's yellow gloves and
    /// turquoise sleeves rolled up in cream, as the look board drew them (hands-carry and hands-siphon). They show what the
    /// player is doing: up to three creatures carried in the shop's water bags in the left hand, each swimming in its bag;
    /// the siphon in the right, its tube banded with each dose it holds in that culture's colour and its bulb squeezed while
    /// it collects (a bigger bulb, a second tube and the vacuum's canister as the siphon goes up its levels); both hands on a
    /// crank while a hand pump or the generator is cranked; a bag held out over the counter while serving, let go when the
    /// sale is made; a length of pipe held while laying one; a reach to a tank when a creature goes in or comes out. They
    /// sway when still, bob when walking and trail a little behind the view as it turns (less, and no trailing, with reduced
    /// motion), and are lowered while a card is open. Presentation only: what the hands hold is the rules' (GrowthShop.Carried
    /// and Held); they decide, pay and move nothing. They are drawn by the player's camera, small and close to it (Near):
    /// the same on screen, but inside the player's capsule, so never through a wall or a tank; and kept the same size on screen
    /// while peek narrows the view.
    /// </summary>
    public sealed class GrowthHands : MonoBehaviour
    {
        /// <summary>How close to the eye the hands are drawn, and as much smaller: the same on screen, inside the player's capsule (0.3 m).</summary>
        public const float Near = 0.36f;
        /// <summary>The player's capsule's radius: no part of the hands in view is ever drawn further from the eye.</summary>
        public const float Capsule = 0.3f;
        const float Ease = 14f, ReachSeconds = 0.6f, HandoverSeconds = 0.45f, BandGrowSeconds = 0.2f, PopSeconds = 0.25f;
        /// <summary>GR-18 wave 4 (§6.7): the slap's swing across the view and back, in seconds.</summary>
        public const float SlapSeconds = 0.45f;
        float slapping = -1;
        /// <summary>Validation: the slaps the right hand has swung.</summary>
        public int Slaps { get; private set; }

        /// <summary>GR-18 wave 4: the right hand swings a cartoon slap across the view (the session calls it on a slap that landed).</summary>
        public void Slap() { slapping = 0; Slaps++; }
        static readonly string[] TieColours = { "#22BFB0", "#FF7A7A", "#FFC93C" };
        const string ServeTie = "#FF9EC4";
        /// <summary>Where a fist closes, in the arm's frame (along the fingers from the wrist).</summary>
        static readonly Vector3 Fist = new Vector3(0, -0.045f, 0.008f);

        sealed class Arm
        {
            public string Side;
            public Transform Root;
            public readonly Dictionary<string, GameObject> Shapes = new Dictionary<string, GameObject>();
            public string Shape, Doing = "rest";
            public Vector3 Pos;
            public Quaternion Rot = Quaternion.identity;
            public bool Placed;
        }

        sealed class Bag
        {
            public Transform Root;
            public GrowthCreatureView View;
            public int Specimen;
            public float Pop = 1, Leaving = -1;
            public Vector2 Swing, SwingV;
        }

        GrowthSession session;
        Camera view;
        Arm left, right;
        Transform siphon, bulb, canister, pipe;
        readonly List<Transform> tubes = new List<Transform>();
        readonly List<(Transform t, Renderer r)> bands = new List<(Transform, Renderer)>();
        readonly List<Bag> bags = new List<Bag>();
        Bag serveBag;
        float clock, walkPhase, reachPut = -1, reachTake = -1, handover = -1, lastServing = -1, growBand = 1;
        int lastHeld, lastSales = -1;
        Vector3 lastPlayer; float lastYaw, lastPitch; bool tracked;
        Vector3 lookLag;

        // ------------------------------------------------------------------------------------------------ for validation
        public string LeftDoing => left?.Doing;
        public string RightDoing => right?.Doing;
        public string ShapeOf(string side) => (side == "L" ? left : right)?.Shape;
        public bool Shown(string side) { var a = side == "L" ? left : right; return a != null && a.Root.gameObject.activeSelf; }
        public int BagsShown => bags.Count(b => b.Leaving < 0 && b.Root.gameObject.activeInHierarchy);
        public int BagCreatures => bags.Count(b => b.Leaving < 0 && b.View != null && b.Root.gameObject.activeInHierarchy);
        public IEnumerable<int> BagSpecimens => bags.Where(b => b.Leaving < 0).Select(b => b.Specimen);
        public bool SiphonShown => siphon != null && siphon.gameObject.activeInHierarchy;
        public int Tubes => tubes.Count(t => t.gameObject.activeSelf);
        public bool CanisterShown => canister != null && canister.gameObject.activeInHierarchy;
        public float BulbSize => bulb != null ? bulb.localScale.x : 0;
        public int BandsShown => bands.Count(b => b.t.gameObject.activeInHierarchy);
        /// <summary>A band's colour as the look's material for it names it ("#RRGGBB").</summary>
        public string BandColour(int i)
        {
            if (i >= bands.Count || !bands[i].r || !bands[i].r.sharedMaterial) return null;
            string n = bands[i].r.sharedMaterial.name;
            int at = n.LastIndexOf('#');
            return at < 0 ? n : n.Substring(at);
        }
        public bool PipeShown => pipe != null && pipe.gameObject.activeInHierarchy;
        public bool ServeBagShown => serveBag != null && serveBag.Root.gameObject.activeInHierarchy;
        /// <summary>How many bags the right hand has handed over at the counter (one a sale made while serving).</summary>
        public int Handovers { get; private set; }
        public int Triangles => GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh).Sum(m => Enumerable.Range(0, m.sharedMesh.subMeshCount).Sum(s => (int)m.sharedMesh.GetIndexCount(s) / 3));
        public int Renderers => GetComponentsInChildren<Renderer>().Length;
        public IEnumerable<Renderer> Drawn => GetComponentsInChildren<Renderer>();
        /// <summary>Where a wrist is on screen (the viewport, 0 to 1 across and up).</summary>
        public Vector3 WristOnScreen(string side) => view.WorldToViewportPoint((side == "L" ? left : right).Root.position);
        /// <summary>How far from the eye any part in view reaches (it stays inside the player's capsule, so it never shows through a wall
        /// or a tank): each part's own box, its corners, edges and faces, where they are in view (the forearms run out of view below).</summary>
        public float FarthestInView => Drawn.SelectMany(Points).Where(InView).Select(p => (p - view.transform.position).magnitude).DefaultIfEmpty(0).Max();
        bool InView(Vector3 p) { var v = view.WorldToViewportPoint(p); return v.z > 0 && v.x > -0.02f && v.x < 1.02f && v.y > -0.02f && v.y < 1.02f; }
        static IEnumerable<Vector3> Points(Renderer r)
        {
            var b = r.localBounds;
            for (int i = 0; i < 27; i++)
                yield return r.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(i % 3 - 1, i / 3 % 3 - 1, i / 9 - 1)));
        }

        // ------------------------------------------------------------------------------------------------ building

        /// <summary>The player's hands on the session's camera (null without the people kit's hands).</summary>
        public static GrowthHands Build(GrowthSession s)
        {
            var hands = Build(s.Player.View);
            if (hands) hands.session = s;
            return hands;
        }

        /// <summary>The hands on a camera, lowered (null without the people kit's hands).</summary>
        public static GrowthHands Build(Camera camera)
        {
            var data = GrowthPeopleKit.Data?.hands;
            if (data == null || data.parts == null || data.parts.Length == 0 || GrowthPeopleKit.Part(Asset, "arm__L") == null) return null;
            var go = new GameObject("Player hands");
            go.transform.SetParent(camera.transform, false);
            var h = go.AddComponent<GrowthHands>();
            h.view = camera;
            h.Assemble(data);
            return h;
        }

        const string Asset = "PK_Hands";

        void Assemble(GrowthPeopleKit.HandsData data)
        {
            left = MakeArm("L"); right = MakeArm("R");
            // The siphon in the right hand: its bulb in the cupped palm (the palm faces the arm's -X), its tube along the fingers.
            siphon = new GameObject("Siphon").transform; siphon.SetParent(right.Root, false);
            siphon.localPosition = new Vector3(-0.022f, -0.072f, 0.004f);
            siphon.localRotation = Quaternion.Euler(90, 0, 0);
            bulb = Place("siphon_bulb__fp", siphon, null).transform;
            float t0 = data.tube[0], t1 = data.tube[1];
            for (int i = 0; i < 2; i++)
            {
                var tube = new GameObject("Tube " + i).transform; tube.SetParent(siphon, false);
                Place("siphon_tube__fp", tube, null);
                tubes.Add(tube);
            }
            for (int i = 0; i < 16; i++)
            {
                var band = Place("siphon_band__fp", siphon, "#FF5E7E").transform;
                bands.Add((band, band.GetComponentInChildren<Renderer>()));
                band.gameObject.SetActive(false);
            }
            canister = Place("siphon_canister__fp", siphon, null).transform;
            pipe = Place("pipe__fp", right.Root, null).transform;
            serveBag = MakeBag(right.Root, ServeTie);
            foreach (var a in new[] { left, right }) a.Root.gameObject.SetActive(false);
        }

        Arm MakeArm(string side)
        {
            var a = new Arm { Side = side };
            a.Root = new GameObject("Arm " + side).transform;
            a.Root.SetParent(transform, false);
            Place("arm__" + side, a.Root, null);
            foreach (var shape in new[] { "relaxed", "grip", "open", "point", "cup" })
            {
                var go = Place("hand-" + shape + "__" + side, a.Root, null);
                if (go) { a.Shapes[shape] = go; go.SetActive(shape == "relaxed"); }
            }
            a.Shape = "relaxed";
            return a;
        }

        Bag MakeBag(Transform parent, string tie)
        {
            var b = new Bag { Root = new GameObject("Bag").transform };
            b.Root.SetParent(parent, false);
            Place("bag__fp", b.Root, tie);
            return b;
        }

        /// <summary>A kit part under a holder, in the look's materials (a role's colour given), drawn without shadows and never in the way.</summary>
        GameObject Place(string part, Transform holder, string roleHex)
        {
            var source = GrowthPeopleKit.Part(Asset, part);
            if (!source) return null;
            var go = Instantiate(source, holder, false);
            go.name = part;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => Resolve(m ? m.name.Replace(" (Instance)", "") : "", roleHex)).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
               
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Gone(c);
            return go;
        }

        /// <summary>A kit slot's material (PK_kind_colour): its own colour, or the one given for its role (the bag's tie, a dose).</summary>
        static Material Resolve(string slot, string roleHex)
        {
            var parts = slot.Split('_');
            if (parts.Length < 3 || parts[0] != "PK") throw new InvalidOperationException("Unmapped hands kit material: " + slot);
            string kind = parts[1], colour = string.Join("_", parts.Skip(2));
            string hex = colour.Length == 6 && colour.All(Uri.IsHexDigit) ? "#" + colour : roleHex ?? "#FF00FF";
            return GrowthPeopleKit.Material(kind, hex);
        }

        // ------------------------------------------------------------------------------------------------ what each hand does

        /// <summary>
        /// What each hand does now: the left carries the bags (and reaches with one to a tank), the right holds the siphon,
        /// a bag at the counter or a pipe (and reaches to take a creature); both crank, and both hold a pipe when the left is
        /// free. A card lowers both.
        /// </summary>
        public static (string left, string right) Decide(in GrowthHandsState s, bool putting, bool taking)
        {
            if (s.CardOpen) return ("rest", "rest");
            bool crank = s.Cranking || s.Pumping;
            int held = s.Held?.Count ?? 0, carried = s.Carried?.Count ?? 0;
            string right = taking ? "take" : s.Serving ? "serve" : crank ? "crank" : s.Piping ? "pipe" : s.Collecting ? "collect" : held > 0 ? "siphon" : "rest";
            string leftDoing = putting ? "put" : carried > 0 ? "carry" : crank ? "crank" : right == "pipe" ? "pipe" : "rest";
            return (leftDoing, right);
        }

        /// <summary>A hand's pose in the camera's space (metres, before it is drawn small and close): the wrist, the way the fingers
        /// point and the thumb, as one rotation, and the hand's shape. Written for the right hand; the left's is its mirror. The
        /// fingers point up and in, so the forearm runs down and out of view from the bottom corner, as the board's hands-carry
        /// drew it; what a hand holds (the siphon's tube, a pipe) is turned on its own.</summary>
        public static (Vector3 wrist, Quaternion rot, string shape) PoseFor(string doing, bool isLeft, float t, in GrowthHandsState s, float reach = 0)
        {
            Vector3 w, f, th = Vector3.up; string shape = "grip";
            switch (doing)
            {
                case "carry": w = Carry; f = CarryFingers; break;
                case "put":
                    {
                        float a = Smooth(Mathf.Sin(Mathf.PI * Mathf.Clamp01(reach)));
                        w = Vector3.Lerp(Carry, new Vector3(0.09f, -0.08f, 0.57f), a);
                        f = Vector3.Lerp(CarryFingers, new Vector3(-0.2f, 0.45f, 0.85f), a);
                        shape = reach > 0.45f && reach < 0.75f ? "open" : "grip";
                        break;
                    }
                case "siphon": w = new Vector3(0.17f, -0.18f, 0.42f); f = new Vector3(-0.35f, 0.62f, 0.42f); shape = "cup"; break;
                case "collect": w = new Vector3(0.12f, -0.16f, 0.43f); f = new Vector3(-0.3f, 0.58f, 0.55f); shape = "cup"; break;
                case "serve":
                    {
                        float a = Smooth(Mathf.Clamp01(s.ServeFraction));
                        w = Vector3.Lerp(new Vector3(0.16f, -0.22f, 0.38f), new Vector3(0.07f, -0.13f, 0.56f), a);
                        f = Vector3.Lerp(new Vector3(-0.32f, 0.62f, 0.45f), new Vector3(-0.16f, 0.52f, 0.75f), a);
                        break;
                    }
                case "handover": w = new Vector3(0.07f, -0.13f, 0.56f); f = new Vector3(-0.16f, 0.52f, 0.75f); shape = "open"; break;
                case "crank":
                    {
                        float deg = (s.Pumping ? 300 : 240) * t * Mathf.Deg2Rad;
                        w = new Vector3(0.06f, -0.15f, 0.46f); f = new Vector3(-0.3f, 0.62f, 0.5f);
                        // Both hands are on the one handle: it goes round, and they with it (not mirrored).
                        var round = new Vector3(Mathf.Cos(-deg), Mathf.Sin(-deg), 0) * 0.055f;
                        if (isLeft) round.x = -round.x;
                        w += round;
                        break;
                    }
                case "pipe": w = new Vector3(0.12f, -0.2f, 0.44f); f = new Vector3(-0.28f, 0.62f, 0.5f); th = new Vector3(-1, 0.3f, 0); break;
                case "slap":
                    {
                        // GR-18 wave 4: the open hand swings in from the right across the view and drops back (reach 0 to 1).
                        float swing = Smooth(Mathf.Clamp01(reach / 0.45f)), back = Smooth(Mathf.Clamp01((reach - 0.45f) / 0.55f));
                        w = Vector3.Lerp(Vector3.Lerp(new Vector3(0.3f, -0.04f, 0.38f), new Vector3(-0.04f, 0.0f, 0.52f), swing), new Vector3(0.2f, -0.3f, 0.4f), back);
                        f = new Vector3(-0.25f, 0.9f, 0.3f); th = new Vector3(0, 0, -1); shape = "open";
                        break;
                    }
                case "take":
                    {
                        float a = Smooth(Mathf.Sin(Mathf.PI * Mathf.Clamp01(reach)));
                        w = Vector3.Lerp(new Vector3(0.18f, -0.3f, 0.4f), new Vector3(0.1f, -0.08f, 0.57f), a);
                        f = Vector3.Lerp(new Vector3(-0.3f, 0.7f, 0.4f), new Vector3(-0.2f, 0.45f, 0.85f), a);
                        shape = reach < 0.45f ? "open" : "grip";
                        break;
                    }
                default: w = new Vector3(0.22f, -0.5f, 0.36f); f = new Vector3(-0.15f, 0.9f, 0.3f); th = new Vector3(-1, 0, 0); shape = "relaxed"; break;
            }
            if (isLeft) { w.x = -w.x; f.x = -f.x; th.x = -th.x; }
            return (w, Orient(f, th), shape);
        }

        /// <summary>The left hand carrying (written as the right's, mirrored): the board's hands-carry.</summary>
        static readonly Vector3 Carry = new Vector3(0.15f, -0.04f, 0.46f), CarryFingers = new Vector3(-0.35f, 0.6f, 0.42f);

        /// <summary>Where the siphon's tube points (the camera's space): at what the reticle is on while it collects, else ahead and
        /// a little up and in.</summary>
        public static Vector3 TubeAim(string doing) => doing == "collect" ? new Vector3(0, 0, 1.6f) : new Vector3(-0.35f, 0.25f, 1.5f);

        /// <summary>A hand's rotation from where its fingers point and its thumb (the arm's own frame: fingers -Y, forearm +Y, thumb +Z).</summary>
        public static Quaternion Orient(Vector3 fingers, Vector3 thumb)
        {
            var y = -fingers.normalized;
            var z = thumb - y * Vector3.Dot(thumb, y);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.Cross(y, Vector3.right);
            return Quaternion.LookRotation(z.normalized, y);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        /// <summary>Removes an object, also outside play (the tests show the hands without running the game).</summary>
        static void Gone(UnityEngine.Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        // ------------------------------------------------------------------------------------------------ each frame

        void LateUpdate()
        {
            if (session == null || session.Player == null || view == null) return;
            var shop = session.Shop;
            var p = session.Player.transform.position;
            float dt = Time.deltaTime;
            float yaw = session.Player.transform.eulerAngles.y, pitch = session.Player.Pitch;
            float speed = 0, yawRate = 0, pitchRate = 0;
            if (tracked && dt > 0)
            {
                var d = p - lastPlayer; d.y = 0;
                speed = d.magnitude / dt;
                yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt; pitchRate = (pitch - lastPitch) / dt;
            }
            lastPlayer = p; lastYaw = yaw; lastPitch = pitch; tracked = true;
            var state = new GrowthHandsState
            {
                CardOpen = session.Card != GrowthCard.None,
                Collecting = session.CollectingNow, Serving = session.ServingNow, Cranking = session.CrankingNow, Pumping = session.PumpingNow,
                Piping = session.PipeFrom != null, Reduced = session.ReducedMotion,
                Peek = Mathf.InverseLerp(session.FieldOfView, GrowthSession.PeekFov, view.fieldOfView),
                HoldSeconds = shop.HoldSeconds, ServeFraction = shop.ServeSeconds > 0 ? shop.ServeProgress / shop.ServeSeconds : 0,
                Speed = speed, YawRate = yawRate, PitchRate = pitchRate, Fov = view.fieldOfView, NormalFov = session.FieldOfView,
                SiphonLevel = shop.Level("siphon"), SiphonCapacity = shop.SiphonCapacity, Sales = shop.SalesTotal,
                Carried = shop.Carried, Held = shop.Held,
            };
            Show(state, dt);
        }

        /// <summary>Shows a moment of what the player is doing (the session calls it each frame; the tests with their own states).</summary>
        public void Show(in GrowthHandsState s, float dt)
        {
            clock += dt;
            float t = clock;
            UpdateBags(s, dt);
            // A sale while serving (or a moment after): the bag is handed over.
            if (s.Serving) lastServing = t;
            if (lastSales >= 0 && s.Sales > lastSales && t - lastServing < 0.25f) { handover = 0; Handovers++; }
            lastSales = s.Sales;
            if (handover >= 0) { handover += dt; if (handover > HandoverSeconds) handover = -1; }
            if (reachPut >= 0) { reachPut += dt / ReachSeconds; if (reachPut >= 1) reachPut = -1; }
            if (reachTake >= 0) { reachTake += dt / ReachSeconds; if (reachTake >= 1) reachTake = -1; }
            if (slapping >= 0) { slapping += dt / SlapSeconds; if (slapping >= 1) slapping = -1; }
            var (l, r) = Decide(s, reachPut >= 0 && !s.CardOpen, reachTake >= 0 && !s.CardOpen);
            // GR-18 wave 4: a slap swings the right hand whatever it held (the siphon and a pipe wait in the other hand's place).
            if (slapping >= 0 && !s.CardOpen) r = "slap";
            if (r == "rest" && handover >= 0 && !s.CardOpen) r = "handover";
            if (r == "serve" && handover >= 0) r = "handover";
            // Walking bobs the hands, standing sways them, and turning the view leaves them a little behind (none of it with
            // reduced motion but a little sway).
            float motion = s.Reduced ? 0.25f : 1;
            walkPhase += s.Speed * dt / 1.25f * Mathf.PI * 2;
            float walking = Mathf.Clamp01(s.Speed / 2.5f);
            var bob = new Vector3(0.006f * Mathf.Sin(walkPhase) * walking, -0.011f * Mathf.Abs(Mathf.Sin(walkPhase)) * walking, 0)
                    + new Vector3(0.002f * Mathf.Sin(2 * Mathf.PI * 0.23f * t), 0.003f * Mathf.Sin(2 * Mathf.PI * 0.35f * t), 0) * (1 - walking);
            var lagTarget = s.Reduced ? Vector3.zero : new Vector3(Mathf.Clamp(-s.YawRate * 0.00022f, -0.03f, 0.03f), Mathf.Clamp(s.PitchRate * 0.00022f, -0.025f, 0.025f), 0);
            lookLag = Vector3.Lerp(lookLag, lagTarget, 1 - Mathf.Exp(-10 * dt));
            var sway = bob * motion + lookLag;
            float k = Mathf.Tan(s.Fov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(s.NormalFov * 0.5f * Mathf.Deg2Rad);
            Drive(left, l, t, s, dt, sway, k, reachPut);
            Drive(right, r, t, s, dt, sway, k, r == "slap" ? slapping : reachTake);
            UpdateSiphon(s, dt, t);
            // The pipe: across both fists, or in the right alone.
            bool piping = r == "pipe";
            pipe.gameObject.SetActive(piping);
            if (piping)
            {
                // In the fist (along the fingers from the wrist), across the view; between both fists when both hold it.
                var fistR = right.Root.TransformPoint(Fist);
                var fistL = left.Root.TransformPoint(Fist);
                bool both = l == "pipe";
                pipe.position = both ? Vector3.Lerp(fistL, fistR, 0.5f) : fistR;
                var across = both ? fistR - fistL : view.transform.right;
                pipe.rotation = Quaternion.FromToRotation(Vector3.right, across.normalized);
                // GR-24: long enough to run on past both fists (its ends and their joints sat inside the gloves).
                float kitLength = GrowthPeopleKit.Data.hands.pipe * Mathf.Max(1e-4f, right.Root.lossyScale.x);
                pipe.localScale = new Vector3(both ? Mathf.Max(1f, across.magnitude * 1.7f / kitLength) : 1f, 1, 1);
            }
            // The bag at the counter: in the right fist while bagging, let go of when the sale is made.
            bool serving = r == "serve";
            serveBag.Root.gameObject.SetActive(serving);
            if (serving) Hang(serveBag, right, new Vector3(-0.012f, -0.032f, 0.02f), s, dt);  // GR-24: from inside the fist
        }

        void Drive(Arm a, string doing, float t, in GrowthHandsState s, float dt, Vector3 sway, float k, float reach)
        {
            a.Doing = doing;
            var pose = PoseFor(doing, a.Side == "L", t, s, reach);
            var w = pose.wrist;
            if (doing != "rest") w += sway;
            if (doing == "carry" || doing == "siphon" || doing == "pipe") w.y -= 0.12f * Mathf.Clamp01(s.Peek);
            if (!a.Placed) { a.Pos = PoseFor("rest", a.Side == "L", t, s).wrist; a.Rot = pose.rot; a.Placed = true; }
            float e = 1 - Mathf.Exp(-Ease * dt);
            a.Pos = Vector3.Lerp(a.Pos, w, e);
            a.Rot = Quaternion.Slerp(a.Rot, pose.rot, e);
            string shape = pose.shape;
            if (doing == "collect") shape = Squeeze(s, t) > 0.5f ? "grip" : "cup";
            if (shape != a.Shape && a.Shapes.ContainsKey(shape))
            {
                foreach (var kv in a.Shapes) kv.Value.SetActive(kv.Key == shape);
                a.Shape = shape;
            }
            // Drawn small and close (Near), and kept the same size on screen while peek narrows the view (k).
            a.Root.localPosition = new Vector3(a.Pos.x * k, a.Pos.y * k, a.Pos.z) * Near;
            a.Root.localRotation = a.Rot;
            a.Root.localScale = Vector3.one * (Near * k);
            bool lowered = doing == "rest" && (a.Pos - PoseFor("rest", a.Side == "L", t, s).wrist).magnitude < 0.02f;
            if (a.Root.gameObject.activeSelf == lowered) a.Root.gameObject.SetActive(!lowered);
        }

        /// <summary>How hard the siphon's bulb is squeezed while it collects (0 to 1), once a dose.</summary>
        static float Squeeze(in GrowthHandsState s, float t) => s.Collecting ? 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * t / Mathf.Max(0.2f, s.HoldSeconds)) : 0;

        void UpdateSiphon(in GrowthHandsState s, float dt, float t)
        {
            bool shown = right.Doing == "siphon" || right.Doing == "collect";
            siphon.gameObject.SetActive(shown);
            var held = s.Held ?? (IList<string>)Array.Empty<string>();
            if (held.Count > lastHeld) growBand = 0;
            lastHeld = held.Count;
            growBand = Mathf.Min(1, growBand + dt / BandGrowSeconds);
            if (!shown) return;
            var data = GrowthPeopleKit.Data.hands;
            int level = Mathf.Max(1, s.SiphonLevel), capacity = Mathf.Clamp(s.SiphonCapacity, 1, bands.Count);
            int count = level >= 3 ? 2 : 1;
            float gap = 0.026f;
            // A second tube (the twin chamber) beside the first, toward the thumb, the bulb between them.
            for (int i = 0; i < tubes.Count; i++)
            {
                tubes[i].gameObject.SetActive(i < count);
                tubes[i].localPosition = new Vector3(0, count == 2 ? (i == 0 ? -gap : gap) : 0, 0);
            }
            canister.gameObject.SetActive(level >= 4);
            var aim = view.transform.TransformPoint(TubeAim(right.Doing) * Near);
            if (right.Doing != "collect") aim = siphon.position + view.transform.rotation * TubeAim(right.Doing);
            siphon.rotation = Quaternion.LookRotation(aim - siphon.position, view.transform.up);
            float q = Squeeze(s, t) * (s.Reduced ? 0.5f : 1);
            float size = level >= 2 ? 1.25f : 1;
            bulb.localScale = new Vector3(size * (1 - 0.28f * q), size * (1 - 0.28f * q), size * (1 + 0.12f * q));
            // A band of each dose's colour, the tubes filling from the bulb's end, the newest growing in.
            int perTube = Mathf.CeilToInt(capacity / (float)count);
            float from = data.tube[0] + 0.006f, length = (data.tube[1] - data.tube[0] - 0.02f) / perTube;
            for (int i = 0; i < bands.Count; i++)
            {
                var (band, r) = bands[i];
                bool on = i < held.Count && i < capacity;
                band.gameObject.SetActive(on);
                if (!on) continue;
                int tube = i / perTube, slot = i % perTube;
                float grow = i == held.Count - 1 ? growBand : 1;
                band.localPosition = new Vector3(0, count == 2 ? (tube == 0 ? -gap : gap) : 0, from + slot * length);
                band.localScale = new Vector3(1, 1, Mathf.Max(0.001f, length * grow));
                string hex = GrowthContent.Culture(held[i])?.Hex ?? "#FF5E7E";
                var m = GrowthPeopleKit.Material("liquid", hex);
                if (r && r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        /// <summary>The bags in the left hand follow what is carried: one for each creature, the creature swimming in it; one that goes
        /// is taken to the tank in a reach and let go, one that comes pops in after the right hand's reach.</summary>
        void UpdateBags(in GrowthHandsState s, float dt)
        {
            var carried = s.Carried ?? (IList<GrowthSpecimen>)Array.Empty<GrowthSpecimen>();
            // Gone from the hands: the bag goes with the left hand's reach (none while a card is open: it is simply gone).
            foreach (var b in bags.Where(b => b.Leaving < 0 && !carried.Any(c => c.Id == b.Specimen)).ToList())
            {
                if (s.CardOpen || reachPut >= 0) { Gone(b.Root.gameObject); bags.Remove(b); continue; }
                b.Leaving = 0; reachPut = 0;
            }
            foreach (var b in bags.Where(b => b.Leaving >= 0).ToList())
            {
                b.Leaving += dt / ReachSeconds;
                if (b.Leaving >= 0.5f) { Gone(b.Root.gameObject); bags.Remove(b); }
            }
            // Come into the hands: a new bag (after the right hand's reach, unless a card is open).
            bool added = false;
            foreach (var c in carried)
            {
                if (bags.Any(b => b.Specimen == c.Id)) continue;
                var b = MakeBag(left.Root, TieColours[bags.Count(x => x.Leaving < 0) % TieColours.Length]);
                b.Specimen = c.Id;
                var data = GrowthPeopleKit.Data.hands;
                if (c.Def != null)
                {
                    b.View = GrowthCreatureView.Create(c, b.Root);
                    b.View.transform.localPosition = GrowthPeopleKit.V(data.bagCreature);
                    // Small enough to swim in the bag (the bag's frame is in metres, as the creature's own length is).
                    b.View.transform.localScale = Vector3.one * Mathf.Min(1f, 0.1f / Mathf.Max(0.01f, b.View.Length));
                    b.View.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    foreach (var col in b.View.GetComponentsInChildren<Collider>(true)) Gone(col);
                    foreach (var r in b.View.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; }
                }
                b.Pop = s.CardOpen ? 1 : 0;
                bags.Add(b);
                added = true;
            }
            if (added && !s.CardOpen && reachTake < 0) reachTake = 0;
            // Each bag's place: fanned out under the left fist, hanging (and swinging) the way gravity pulls.
            var kept = bags.Where(b => b.Leaving < 0).ToList();
            for (int i = 0; i < kept.Count; i++)
            {
                var b = kept[i];
                if (b.Pop < 1 && (reachTake < 0 || reachTake > 0.5f)) b.Pop = Mathf.Min(1, b.Pop + dt / PopSeconds);
                float n = kept.Count;
                // GR-24: their knots together in the fist, the bags fanning out below it (the knots hung beside the fist).
                float c = i - (n - 1) / 2;
                var off = new Vector3(c * 0.016f, -0.03f, 0.012f + 0.008f * (1 - Mathf.Abs(c)));
                Hang(b, left, off, s, dt, c * 16f);
                b.Root.localScale = Vector3.one * Smooth(b.Pop);
                if (b.View) b.View.Tick(clock, s.Reduced);
            }
            foreach (var b in bags.Where(b => b.Leaving >= 0))
            {
                Hang(b, left, new Vector3(0.012f, -0.032f, 0.02f), s, dt);
                b.Root.localScale = Vector3.one * (1 - Smooth(Mathf.InverseLerp(0.3f, 0.5f, b.Leaving)));
            }
        }

        /// <summary>A bag from a fist: its knot just under the fist (an offset from it in the camera's space), hanging the way gravity
        /// pulls (but never more than 35 degrees from straight down on screen), swinging a little as the player moves and turns.</summary>
        void Hang(Bag b, Arm a, Vector3 offset, in GrowthHandsState s, float dt, float fan = 0)
        {
            var cam = view.transform;
            b.Root.localPosition = Fist + Quaternion.Inverse(a.Rot) * offset;
            var down = Quaternion.Inverse(cam.rotation) * Vector3.down;
            down = Vector3.RotateTowards(Vector3.down, down, 35 * Mathf.Deg2Rad, 0);
            if (!s.Reduced)
            {
                var push = new Vector2(Mathf.Clamp(s.YawRate * 0.0012f, -0.5f, 0.5f) + 0.25f * Mathf.Sin(walkPhase) * Mathf.Clamp01(s.Speed / 2.5f), 0);
                b.SwingV += ((push - b.Swing) * 40 - b.SwingV * 6) * dt;
                b.Swing += b.SwingV * dt;
            }
            else { b.Swing = Vector2.zero; b.SwingV = Vector2.zero; }
            var hang = Quaternion.Euler(b.Swing.y * 30, 0, b.Swing.x * 30 + fan) * Quaternion.FromToRotation(Vector3.down, down);
            b.Root.rotation = cam.rotation * hang;
        }
    }
}
