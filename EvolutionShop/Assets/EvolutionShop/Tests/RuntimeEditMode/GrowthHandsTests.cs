using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-18 wave 3: the player's hands (GrowthHands) against the people kit's hands: every part is in the kit; each hand
    /// does what the player is doing (carrying, siphoning, serving, cranking, laying a pipe, reaching to a tank); they stay on
    /// screen where they work and below it at rest, never further from the eye than the player's capsule; the bags follow what is
    /// carried, a creature in each; the siphon's tube shows each dose in its culture's colour and grows with its levels; and they
    /// keep their size on screen while peek narrows the view.</summary>
    public sealed class GrowthHandsTests
    {
        readonly List<GameObject> made = new List<GameObject>();
        [TearDown] public void CleanUp() { foreach (var g in made) if (g) Object.DestroyImmediate(g); made.Clear(); }

        (GrowthHands hands, Camera camera) Build(float fov = GrowthSession.NormalFov)
        {
            var cam = new GameObject("Hands test camera").AddComponent<Camera>();
            made.Add(cam.gameObject);
            cam.fieldOfView = fov; cam.aspect = 16f / 9f; cam.nearClipPlane = 0.02f;
            cam.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            var hands = GrowthHands.Build(cam);
            Assert.IsNotNull(hands, "The hands build from the people kit");
            return (hands, cam);
        }

        static GrowthHandsState State(float fov = GrowthSession.NormalFov) => new GrowthHandsState
        {
            Fov = fov, NormalFov = GrowthSession.NormalFov, HoldSeconds = 0.8f, SiphonLevel = 1, SiphonCapacity = 3,
            Carried = new List<GrowthSpecimen>(), Held = new List<string>(),
        };

        /// <summary>Shows a state for a while (the hands ease into place).</summary>
        static void Run(GrowthHands h, GrowthHandsState s, float seconds = 1.5f, float dt = 1 / 60f)
        {
            for (float t = 0; t < seconds; t += dt) h.Show(s, dt);
        }

        static List<GrowthSpecimen> Creatures(params string[] species) => species.Select((sp, i) => new GrowthSpecimen { Id = 500 + i, Species = sp }).ToList();

        [Test]
        public void TheKitHasThePlayersHands()
        {
            var data = GrowthPeopleKit.Data.hands;
            Assert.IsNotNull(data, "people.json has the hands");
            var want = new List<string> { "bag__fp", "pipe__fp", "siphon_band__fp", "siphon_bulb__fp", "siphon_canister__fp", "siphon_tube__fp" };
            foreach (var side in new[] { "L", "R" })
            {
                want.Add("arm__" + side);
                foreach (var shape in new[] { "relaxed", "grip", "open", "point", "cup" }) want.Add("hand-" + shape + "__" + side);
            }
            CollectionAssert.AreEquivalent(want, data.parts, "The hands' parts, as the kit made them");
            foreach (var p in want) Assert.IsNotNull(GrowthPeopleKit.Part("PK_Hands", p), p + " is in PK_Hands");
            Assert.That(data.tube[1], Is.GreaterThan(data.tube[0]));
            Assert.That(data.forearm, Is.EqualTo(0.42f).Within(0.001f));
        }

        [Test]
        public void TheHandsWearTheLooksMaterialsAndStartLowered()
        {
            var (h, _) = Build();
            var mats = h.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
            Assert.That(mats.Count, Is.GreaterThan(10));
            var odd = mats.Where(m => m == null || !m.name.Contains("GR people ") || m.name.Contains("#FF00FF")).Select(m => m ? m.name : "(none)").Distinct().ToList();
            Assert.IsEmpty(odd, "Every slot maps to the look's materials");
            Assert.IsTrue(h.GetComponentsInChildren<Renderer>(true).All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), "The hands cast no shadows");
            Assert.IsEmpty(h.GetComponentsInChildren<Collider>(true), "Nothing of the hands is in the way of the reticle");
            Assert.IsFalse(h.Shown("L") || h.Shown("R"), "Lowered until the player does something");
            Run(h, State());
            Assert.IsFalse(h.Shown("L") || h.Shown("R"), "Empty-handed, the hands stay lowered");
            Assert.That(h.LeftDoing, Is.EqualTo("rest")); Assert.That(h.RightDoing, Is.EqualTo("rest"));
        }

        [Test]
        public void EachHandDoesWhatThePlayerDoes()
        {
            var s = State();
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("rest", "rest")), "Nothing in hand");
            s.Carried = Creatures("pondlet"); s.Held = new List<string> { "bloom" };
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("carry", "siphon")), "Bags in the left, the siphon in the right");
            s.Collecting = true;
            Assert.That(GrowthHands.Decide(s, false, false).right, Is.EqualTo("collect"));
            s.Collecting = false; s.Serving = true;
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("carry", "serve")), "A bag held out at the counter");
            s.Serving = false; s.Cranking = true;
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("carry", "crank")), "Cranking one-handed while carrying");
            s.Carried = new List<GrowthSpecimen>();
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("crank", "crank")), "Both hands on the crank");
            s.Cranking = false; s.Pumping = true;
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("crank", "crank")), "And on a hand pump");
            s.Pumping = false; s.Piping = true;
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("pipe", "pipe")), "Both hands on a pipe");
            s.Carried = Creatures("pondlet");
            Assert.That(GrowthHands.Decide(s, false, false), Is.EqualTo(("carry", "pipe")), "The pipe in one hand while carrying");
            Assert.That(GrowthHands.Decide(s, true, true), Is.EqualTo(("put", "take")), "Reaching to put one in, and to take one");
            s.CardOpen = true;
            Assert.That(GrowthHands.Decide(s, true, true), Is.EqualTo(("rest", "rest")), "A card lowers both");
        }

        [Test]
        public void OnScreenWhereTheyWorkAndInsideThePlayersCapsule()
        {

            foreach (var (what, mod) in new (string, System.Func<GrowthHandsState, GrowthHandsState>)[]
            {
                ("carrying three", s => { s.Carried = Creatures("pondlet", "bubblebelly", "lanternfin"); return s; }),
                ("holding doses", s => { s.Held = new List<string> { "bloom", "flow", "bloom" }; return s; }),
                ("siphoning", s => { s.Collecting = true; return s; }),
                ("serving", s => { s.Serving = true; s.ServeFraction = 0.9f; return s; }),
                ("cranking", s => { s.Cranking = true; return s; }),
                ("laying a pipe", s => { s.Piping = true; return s; }),
            })
            {
                var (h, cam) = Build();
                var s = mod(State());
                for (int f = 0; f < 150; f++)
                {
                    h.Show(s, 1 / 60f);
                    Assert.That(h.FarthestInView, Is.LessThan(GrowthHands.Capsule), what + ": every part in view stays inside the player's capsule (frame " + f + ")");
                }
                foreach (var side in new[] { "L", "R" })
                {
                    if (!h.Shown(side)) continue;
                    var v = h.WristOnScreen(side);
                    Assert.That(v.z, Is.GreaterThan(0), what + ": the " + side + " wrist is in front of the eye");
                    Assert.That(v.x, Is.InRange(0.0f, 1.0f), what + ": the " + side + " wrist is on screen across (" + v + ")");
                    Assert.That(v.y, Is.InRange(-0.05f, 0.62f), what + ": the " + side + " wrist is in the lower part of the screen (" + v + ")");
                }
                Assert.IsTrue(h.Shown("L") || h.Shown("R"), what + ": a hand is up");
            }
            // Rest: below the screen.
            var rest = GrowthHands.PoseFor("rest", false, 0, State());
            var cam2 = Build().camera;
            var vp = cam2.WorldToViewportPoint(cam2.transform.TransformPoint(rest.wrist * GrowthHands.Near));
            Assert.That(vp.y, Is.LessThan(0), "At rest the hands are below the screen");
        }

        [Test]
        public void TheReachesStayInsideThePlayersCapsule()
        {
            var (h, _) = Build();
            var s = State();
            s.Carried = Creatures("pondlet", "bubblebelly");
            Run(h, s, 1);
            Assert.That(h.BagsShown, Is.EqualTo(2));
            // One goes into a tank: the left hand reaches with it, lets go, and comes back with the other.
            s.Carried = Creatures("pondlet").ToList();
            bool put = false; float far = 0;
            for (int f = 0; f < 60; f++) { h.Show(s, 1 / 60f); put |= h.LeftDoing == "put"; far = Mathf.Max(far, h.FarthestInView); }
            Assert.IsTrue(put, "Putting one in, the left hand reaches");
            Assert.That(far, Is.LessThan(GrowthHands.Capsule), "Reaching, it stays inside the player's capsule");
            Run(h, s, 0.5f);
            Assert.That(h.BagsShown, Is.EqualTo(1), "The bag is let go of in the tank");
            Assert.That(h.LeftDoing, Is.EqualTo("carry"));
            // One comes out: the right hand reaches for it, and its bag joins the others.
            s.Carried = Creatures("pondlet", "bubblebelly", "ribbonfin");
            bool take = false; far = 0;
            for (int f = 0; f < 60; f++) { h.Show(s, 1 / 60f); take |= h.RightDoing == "take"; far = Mathf.Max(far, h.FarthestInView); }
            Assert.IsTrue(take, "Taking one, the right hand reaches");
            Assert.That(far, Is.LessThan(GrowthHands.Capsule));
            Run(h, s, 0.5f);
            Assert.That(h.BagsShown, Is.EqualTo(3));
        }

        [Test]
        public void TheBagsFollowWhatIsCarriedEachWithItsCreature()
        {
            var (h, _) = Build();
            var s = State();
            for (int n = 1; n <= 3; n++)
            {
                s.Carried = Creatures("pondlet", "bubblebelly", "lanternfin").Take(n).ToList();
                Run(h, s);
                Assert.That(h.BagsShown, Is.EqualTo(n), n + " carried, " + n + " bags");
                Assert.That(h.BagCreatures, Is.EqualTo(n), "a creature swimming in each");
                CollectionAssert.AreEquivalent(s.Carried.Select(c => c.Id), h.BagSpecimens);
            }
            // A card open lowers the hands with the bags; closing it, they come back.
            s.CardOpen = true; Run(h, s);
            Assert.IsFalse(h.Shown("L"));
            s.CardOpen = false; Run(h, s);
            Assert.That(h.BagsShown, Is.EqualTo(3));
            // Put away with a card open, a bag is just gone.
            s.CardOpen = true; s.Carried = new List<GrowthSpecimen>(); Run(h, s, 0.2f);
            Assert.That(h.BagsShown, Is.EqualTo(0));
        }

        [Test]
        public void TheSiphonShowsEachDoseAndItsLevel()
        {
            var (h, _) = Build();
            var s = State();
            s.Held = new List<string> { "bloom", "flow" };
            Run(h, s);
            Assert.IsTrue(h.SiphonShown, "Doses in the siphon: it is in the right hand");
            Assert.That(h.BandsShown, Is.EqualTo(2), "A band for each dose");
            Assert.That(h.BandColour(0), Is.EqualTo(GrowthContent.Culture("bloom").Hex), "Bloom's colour");
            Assert.That(h.BandColour(1), Is.EqualTo(GrowthContent.Culture("flow").Hex), "Flow's colour");
            Assert.That(h.Tubes, Is.EqualTo(1)); Assert.IsFalse(h.CanisterShown); Assert.That(h.BulbSize, Is.EqualTo(1).Within(0.01f));
            s.SiphonLevel = 2; s.SiphonCapacity = 6; Run(h, s, 0.2f);
            Assert.That(h.BulbSize, Is.EqualTo(1.25f).Within(0.01f), "Level 2: the bigger bulb");
            s.SiphonLevel = 3; s.SiphonCapacity = 10; Run(h, s, 0.2f);
            Assert.That(h.Tubes, Is.EqualTo(2), "Level 3: the twin chamber");
            s.SiphonLevel = 4; s.SiphonCapacity = 16; s.Held = Enumerable.Repeat("spark", 16).ToList(); Run(h, s, 0.5f);
            Assert.IsTrue(h.CanisterShown, "Level 4: the vacuum's canister");
            Assert.That(h.BandsShown, Is.EqualTo(16), "Sixteen doses, sixteen bands");
            Assert.That(h.BandColour(15), Is.EqualTo(GrowthContent.Culture("spark").Hex));
            // Collecting, the bulb is squeezed and let go of, once a dose.
            s.SiphonLevel = 1; s.SiphonCapacity = 3; s.Held = new List<string>(); s.Collecting = true;
            var sizes = new List<float>(); var shapes = new HashSet<string>();
            for (int f = 0; f < 90; f++) { h.Show(s, 1 / 60f); sizes.Add(h.BulbSize); shapes.Add(h.ShapeOf("R")); }
            Assert.That(sizes.Max() - sizes.Min(), Is.GreaterThan(0.2f), "The bulb is squeezed while it collects");
            CollectionAssert.IsSubsetOf(new[] { "cup", "grip" }, shapes, "The hand cups the bulb and squeezes it");
            // Empty and not collecting: it is put away.
            s.Collecting = false; Run(h, s);
            Assert.IsFalse(h.SiphonShown);
        }

        [Test]
        public void ServingHoldsABagOutAndLetsItGoOnTheSale()
        {
            var (h, _) = Build();
            var s = State();
            s.Serving = true; s.ServeFraction = 0;
            Run(h, s, 1);
            var start = h.WristOnScreen("R");
            Assert.IsTrue(h.ServeBagShown, "A bag in the right hand at the counter");
            s.ServeFraction = 0.95f; Run(h, s, 1);
            var end = h.WristOnScreen("R");
            Assert.That(end.z, Is.GreaterThan(start.z + 0.05f), "Held out over the counter as the bagging comes along");
            s.Sales = 1; s.ServeFraction = 0; h.Show(s, 1 / 60f);
            Assert.That(h.RightDoing, Is.EqualTo("handover")); Assert.IsFalse(h.ServeBagShown, "The sale is made: the bag is handed over");
            Assert.That(h.Handovers, Is.EqualTo(1), "One bag handed over for the one sale");
            Run(h, s, 0.1f);
            Assert.That(h.ShapeOf("R"), Is.EqualTo("open"), "The hand opens");
        }

        [Test]
        public void CrankingBothHandsGoRoundTogether()
        {
            var (h, _) = Build();
            var s = State(); s.Cranking = true;
            Run(h, s, 1);
            var l0 = h.WristOnScreen("L"); var r0 = h.WristOnScreen("R");
            Run(h, s, 0.35f);
            var l1 = h.WristOnScreen("L"); var r1 = h.WristOnScreen("R");
            Assert.That((r1 - r0).magnitude, Is.GreaterThan(0.02f), "The hands go round with the crank");
            Assert.That(Mathf.Abs((r1 - r0).y - (l1 - l0).y), Is.LessThan(0.02f), "Both on the one handle");
            Assert.That(h.ShapeOf("L"), Is.EqualTo("grip")); Assert.That(h.ShapeOf("R"), Is.EqualTo("grip"));
            Assert.IsFalse(h.PipeShown);
            s.Cranking = false; s.Piping = true; Run(h, s, 1);
            Assert.IsTrue(h.PipeShown, "Laying a pipe, a length of it in the hands");
        }

        [Test]
        public void TheSameSizeOnScreenWhilePeekNarrowsTheView()
        {
            var a = Build(GrowthSession.NormalFov); var b = Build(GrowthSession.PeekFov);
            var s = State(); s.Carried = Creatures("pondlet");
            var sa = s; var sb = s; sb.Fov = GrowthSession.PeekFov;
            Run(a.hands, sa); Run(b.hands, sb);
            var va = a.hands.WristOnScreen("L"); var vb = b.hands.WristOnScreen("L");
            Assert.That(Vector2.Distance(va, vb), Is.LessThan(0.01f), "The wrist keeps its place on screen (" + va + " and " + vb + ")");
        }

        [Test]
        public void WalkingBobsThemAndReducedMotionKeepsThemStiller()
        {
            float Bob(bool reduced)
            {
                var (h, _) = Build();
                var s = State(); s.Carried = Creatures("pondlet"); s.Reduced = reduced;
                Run(h, s, 1.5f);
                s.Speed = 3.2f;
                float lo = 1, hi = 0;
                for (int f = 0; f < 120; f++) { h.Show(s, 1 / 60f); var v = h.WristOnScreen("L"); lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
                return hi - lo;
            }
            float full = Bob(false), still = Bob(true);
            Assert.That(full, Is.GreaterThan(0.01f), "Walking, the hands bob (" + full + ")");
            Assert.That(still, Is.LessThan(full * 0.5f), "With reduced motion, much less (" + still + ")");
        }
    }
}
