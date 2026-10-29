using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-18 wave 4 (§6.7): what the silly moments show and sound like. The people kit has their faces and props; a
    /// visitor's every reaction moves them and their face and ends clean (eyes back, nothing left over); through every reaction
    /// and a helper's dance a person stays on the floor and in a person's room; a slap's stagger never outlasts the walk out;
    /// the right hand swings a slap and stays inside the player's capsule; a fish copying a face stays where it swims; the
    /// sounds are made here, never clip and start and end at silence; and the settings keep with the save.</summary>
    public sealed class GrowthGagsTests
    {
        static readonly string[] Kinds = { "casual", "bargain", "collector", "premium" };
        static readonly string[] Reactions = { "slapped", "laugh", "culprit", "sniff", "blame", "complain", "fishface" };
        readonly List<GameObject> made = new List<GameObject>();

        [TearDown] public void CleanUp() { foreach (var g in made) if (g) Object.DestroyImmediate(g); made.Clear(); }

        GrowthVisitorView Visitor(string type, int id)
        {
            var parent = new GameObject("Gags test"); made.Add(parent);
            var view = GrowthVisitorView.Create(new GrowthVisitor { Id = id, Type = type, State = GrowthVisitorState.Browsing }, parent.transform);
            Assert.IsNotNull(view.Person, type + " is a person");
            return view;
        }

        static Bounds Bounds(Component c)
        {
            var rs = c.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToList();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void TheKitHasTheSillyMomentsFacesAndPropsInTheLooksMaterials()
        {
            var data = GrowthPeopleKit.Data;
            Assert.IsNotNull(data.gags, "people.json lists the gags");
            CollectionAssert.AreEquivalent(new[] { "head__mouth-wobble", "head__eyes-squeeze", "head__mark-slap", "gag__stars", "gag__cloud", "gag__bubble", "gag__note" }, data.gags.parts);
            Assert.AreEqual(0.15f, data.gags.starsRing, 1e-4f);
            var holder = new GameObject("Props"); made.Add(holder);
            foreach (var part in data.gags.parts)
            {
                Assert.IsNotNull(GrowthPeopleKit.Part(GrowthPerson.GagAsset, part), "the kit has " + part);
                var go = GrowthPeopleKit.Prop(GrowthPerson.GagAsset, part, holder.transform);
                var mats = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
                Assert.IsFalse(mats.Any(m => m == null || m.name.StartsWith("PK_") || m.color == Color.magenta), part + ": every slot in the look's materials");
                Assert.IsFalse(go.GetComponentsInChildren<Collider>(true).Any(), part + " is never in the way");
                var b = Bounds(go.transform);
                Assert.Less(b.size.magnitude, 0.8f, part + " is small");
            }
        }

        [Test]
        public void EachReactionMovesTheBodyAndTheFaceAndEndsClean()
        {
            foreach (var kind in Kinds)
            {
                var view = Visitor(kind, 31);
                var p = view.Person;
                view.Tick(0.1f);
                var rest = (GrowthPersonPose)null;
                rest = new GrowthPersonPose(); rest.CopyFrom(p.Pose);
                var eyes = p.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head__eyes")?.gameObject;
                Assert.IsTrue(eyes != null || p.Look.Sunglasses, kind + " has eyes (or sunglasses)");
                foreach (var r in Reactions)
                {
                    float moved = 0; var mouths = new HashSet<string>(); var parts = new HashSet<string>();
                    for (int i = 0; i < 40; i++)
                    {
                        view.React(r, i * 0.06f, view.transform.position + new Vector3(1.2f, 1, 1.5f));
                        view.Tick(1 / 30f);
                        moved = Mathf.Max(moved, Enumerable.Range(0, rest.E.Length).Max(j => (p.Pose.E[j] - rest.E[j]).magnitude));
                        mouths.Add(p.MouthShown);
                        foreach (var g in new[] { "head__mark-slap", "head__eyes-squeeze", "gag__stars", "gag__cloud" }) if (p.GagShown(g)) parts.Add(g);
                    }
                    Assert.Greater(moved, 20f, kind + " " + r + " moves");
                    Assert.AreEqual(r, view.Reaction);
                    if (r == "slapped" || r == "laugh")
                    {
                        CollectionAssert.IsSubsetOf(new[] { "head__mark-slap", "gag__stars" }, parts.ToList(), kind + " " + r + ": the hand mark and dizzy stars");
                        if (!p.Look.Sunglasses) Assert.Contains("head__eyes-squeeze", parts.ToList(), kind + " " + r + ": eyes squeezed shut");
                    }
                    if (r == "slapped") Assert.Contains("wobble", mouths.ToList(), kind + ": the wobbly grimace");
                    if (r == "laugh") Assert.Contains("grin", mouths.ToList());
                    if (r == "culprit") Assert.Contains("gag__cloud", parts.ToList(), kind + ": the culprit's cloud");
                    if (r == "sniff" || r == "fishface") Assert.Contains("o", mouths.ToList());
                    view.EndReaction();
                    view.Tick(1 / 30f);
                    Assert.IsNull(view.Reaction);
                    foreach (var g in new[] { "head__mark-slap", "head__eyes-squeeze", "gag__stars", "gag__cloud" }) Assert.IsFalse(p.GagShown(g), kind + " " + r + " leaves no " + g);
                    Assert.IsTrue(p.Look.Sunglasses || eyes.activeSelf, kind + " " + r + ": the eyes are back");
                    Assert.AreEqual(p.Look.Mouth, p.MouthShown, kind + " " + r + ": their own mouth again");
                    for (int i = 0; i < 60; i++) view.Tick(1 / 30f);
                }
            }
        }

        [Test]
        public void ThroughEveryReactionAndADanceAPersonStaysOnTheFloorAndInAPersonsRoom()
        {
            var pose = new GrowthPersonPose();
            var motions = new (string name, System.Action<GrowthPersonPose, float> make)[]
            {
                ("slapped", (q, t) => { GrowthPeopleMotion.Stand(q, 0); GrowthPeopleMotion.Slapped(q, t * 2); }),
                ("slapped walking", (q, t) => { GrowthPeopleMotion.Walk(q, t, GrowthPeopleMotion.Gaits["bargain"]); GrowthPeopleMotion.Slapped(q, t * 2); }),
                ("laugh", (q, t) => { GrowthPeopleMotion.Stand(q, 0); GrowthPeopleMotion.Laugh(q, t * 2); }),
                ("innocent", (q, t) => { GrowthPeopleMotion.Stand(q, 0); GrowthPeopleMotion.Innocent(q, t * 2); }),
                ("sniff", (q, t) => { GrowthPeopleMotion.Stand(q, 0); GrowthPeopleMotion.Sniff(q, t * 2, 60); }),
                ("accuse", (q, t) => { GrowthPeopleMotion.Browse(q, t); GrowthPeopleMotion.Accuse(q, t * 2, -60); }),
                ("complain", (q, t) => { GrowthPeopleMotion.Browse(q, t); GrowthPeopleMotion.Complain(q, t * 2.6f, 30); }),
                ("fish face", (q, t) => { GrowthPeopleMotion.Browse(q, t); GrowthPeopleMotion.FishFace(q, t * 2, 0); }),
                ("dance", (q, t) => GrowthPeopleMotion.Dance(q, t * 2)),
            };
            foreach (var kind in Kinds.Concat(new[] { "pip", "nori", "moss" }))
            {
                var parent = new GameObject("Motion test"); made.Add(parent);
                var p = GrowthPerson.Build(GrowthPersonLook.For(kind, 5), parent.transform);
                float height = GrowthPeopleKit.Kind(kind).height * p.Look.Height;
                foreach (var m in motions)
                    for (int i = 0; i < 20; i++)
                    {
                        m.make(pose, i / 20f);
                        Assert.IsTrue(pose.E.All(e => !float.IsNaN(e.x) && !float.IsNaN(e.y) && !float.IsNaN(e.z)), kind + " " + m.name + ": finite");
                        p.Apply(pose);
                        var b = Bounds(p);
                        Assert.Greater(b.min.y, -0.06f, kind + " " + m.name + " " + i + ": feet no deeper than the soles allow");
                        Assert.Less(b.max.y, height * 1.25f + 0.3f, kind + " " + m.name + " " + i + ": no taller than a raised hand");
                        Assert.Less(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), 1.25f, kind + " " + m.name + " " + i + ": within reach across");
                        Assert.Less(Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)), 1.25f, kind + " " + m.name + " " + i + ": within reach forward and back");
                    }
            }
            // The dance hops twice a second; the slap knocks the head to the person's right and it wobbles back.
            var a = new GrowthPersonPose(); var c = new GrowthPersonPose();
            GrowthPeopleMotion.Dance(a, 0.125f); GrowthPeopleMotion.Dance(c, 0.375f);
            Assert.Greater(a.Lift - c.Lift, 0.03f, "a hop");
            GrowthPeopleMotion.Idle(a); GrowthPeopleMotion.Slapped(a, 0.06f);
            Assert.Less(a.E[GrowthPersonPose.Head].z, -25f, "knocked to the right");
            GrowthPeopleMotion.Idle(c); GrowthPeopleMotion.Slapped(c, 2.5f);
            Assert.Less(Mathf.Abs(c.E[GrowthPersonPose.Head].z), 8f, "and back");
        }

        [Test]
        public void ASlapsStaggerNeverOutlastsTheWalkOut()
        {
            var view = Visitor("bargain", 41);
            var shop = new GrowthShop();
            var v = new GrowthVisitor { Id = 41, Type = "bargain", State = GrowthVisitorState.Browsing };
            view.Follow(v, shop, -1);
            v.State = GrowthVisitorState.Leaving; v.Outcome = GrowthVisitOutcome.Slapped;
            view.transform.position = new Vector3(6, 0, 3);
            view.Follow(v, shop, -1);
            view.Stagger(0.55f);
            Assert.That(view.Staggered, Is.InRange(0f, 0.55f));
            float walk = 0; var at = view.transform.position;
            for (int i = 0; i < 600 && !view.Arrived; i++) { view.Tick(1 / 60f); walk += 1 / 60f; }
            Assert.IsTrue(view.Arrived, "out of the door");
            Assert.Less(walk, GrowthShop.LeaveSeconds - 0.5f, "before the rules let them go: nobody vanishes on the way");
            Assert.AreEqual("leave-huff", view.Activity, "leaving in a huff");
            // From far away there is no time to spare: no stagger at all.
            var far = Visitor("collector", 42);
            var w = new GrowthVisitor { Id = 42, Type = "collector", State = GrowthVisitorState.Leaving, Outcome = GrowthVisitOutcome.Slapped };
            far.transform.position = new Vector3(40, 0, 40);
            far.Follow(w, shop, -1);
            far.Stagger(0.55f);
            Assert.AreEqual(0f, far.Staggered, 1e-4f);
        }

        [Test]
        public void TheRightHandSwingsASlapInsideTheCapsule()
        {
            var go = new GameObject("Camera"); made.Add(go);
            var cam = go.AddComponent<Camera>(); cam.fieldOfView = GrowthSession.NormalFov;
            var h = GrowthHands.Build(cam);
            Assert.IsNotNull(h);
            var s = new GrowthHandsState { Fov = GrowthSession.NormalFov, NormalFov = GrowthSession.NormalFov, HoldSeconds = 1, Held = new List<string> { "bloom" }, Carried = new List<GrowthSpecimen>() };
            for (int i = 0; i < 60; i++) h.Show(s, 1 / 60f);
            Assert.AreEqual("siphon", h.RightDoing);
            h.Slap();
            bool slapped = false, open = false, crossed = false; float far = 0;
            for (int i = 0; i < 30; i++)
            {
                h.Show(s, 1 / 60f);
                slapped |= h.RightDoing == "slap";
                open |= h.ShapeOf("R") == "open";
                far = Mathf.Max(far, h.FarthestInView);
                var wrist = h.WristOnScreen("R");
                crossed |= wrist.x < 0.55f;
            }
            Assert.IsTrue(slapped && open, "the right hand swings open-handed");
            Assert.IsTrue(crossed, "across the view");
            Assert.Less(far, GrowthHands.Capsule, "inside the player's capsule");
            for (int i = 0; i < 60; i++) h.Show(s, 1 / 60f);
            Assert.AreEqual("siphon", h.RightDoing, "and back to the siphon");
            Assert.AreEqual(1, h.Slaps);
            s.CardOpen = true; h.Slap();
            h.Show(s, 1 / 60f);
            Assert.AreEqual("rest", h.RightDoing, "never with a card open");
        }

        [Test]
        public void AFishCopyingAFaceStaysWhereItSwims()
        {
            foreach (var species in new[] { "pondlet", "bubblebelly", "ribbonfin" })
            {
                var parent = new GameObject("Fish"); made.Add(parent);
                var fish = GrowthCreatureView.Create(new GrowthSpecimen { Id = 7, Species = species }, parent.transform);
                fish.Tick(0); fish.Tick(0.1f);
                var rest = Bounds(fish);
                float worst = 0;
                fish.MimicFor = 2f;
                for (int i = 0; i < 40; i++)
                {
                    fish.Mimic = i * 0.016f;   // up to 0.64 s, before its burst
                    fish.Tick(0.1f + i * 0.016f);
                    var b = Bounds(fish);
                    worst = Mathf.Max(worst, Vector3.Distance(b.center, rest.center), (b.extents - rest.extents).magnitude);
                }
                Assert.Less(worst, fish.Length * 0.5f, species + " waggles where it swims, never further than its tricks reach");
                Assert.AreEqual(fish.transform.localPosition, Vector3.zero, species + " is not moved");
                fish.Mimic = -1; fish.Tick(1f);
            }
        }

        [Test]
        public void TheSoundsAreMadeHereNeverClipAndStartAndEndAtSilence()
        {
            var sounds = new Dictionary<string, float[]>
            {
                ["slap"] = GrowthGags.MakeSlap(), ["boing"] = GrowthGags.MakeBoing(), ["grumble"] = GrowthGags.MakeGibberish(7, 150f, 0.85f, 21),
                ["laugh"] = GrowthGags.MakeLaugh(), ["squeak"] = GrowthGags.MakeSqueak(0), ["squeak2"] = GrowthGags.MakeSqueak(1),
                ["mumble"] = GrowthGags.MakeGibberish(9, 210f, 1.15f, 5), ["burp"] = GrowthGags.MakeBurp(), ["blub"] = GrowthGags.MakeBlub(), ["shoe"] = GrowthGags.MakeShoe(),
            };
            foreach (var kv in sounds)
            {
                var d = kv.Value;
                Assert.Greater(d.Length, GrowthGags.SampleRate * 0.05f, kv.Key + " is a sound");
                Assert.Less(d.Length, GrowthGags.SampleRate * 1.6f, kv.Key + " is brief");
                Assert.IsTrue(d.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), kv.Key + " is finite");
                float peak = d.Max(Mathf.Abs);
                Assert.LessOrEqual(peak, 0.6001f, kv.Key + " never clips");
                Assert.Greater(peak, 0.05f, kv.Key + " can be heard");
                Assert.Less(Mathf.Abs(d[0]) + Mathf.Abs(d[d.Length - 1]), 1e-3f, kv.Key + " starts and ends at silence");
            }
            Assert.AreNotEqual(sounds["squeak"].Length, sounds["squeak2"].Length, "two squeaks, so it is not the same each time");
        }

        [Test]
        public void TheComedySettingsKeepWithTheSaveAndAnOlderSaveHasEveryGag()
        {
            var older = JsonUtility.FromJson<GrowthSave>("{\"version\":11,\"credits\":5}");
            Assert.AreEqual(0, older.humourLevel); Assert.AreEqual(0, older.gagLevel); Assert.IsTrue(string.IsNullOrEmpty(older.slapKey));
            Assert.AreEqual("F", GrowthGags.KeyFromSave(older.slapKey), "an older save slaps with F");
            Assert.AreEqual("G", GrowthGags.KeyFromSave("G"));
            Assert.AreEqual("F", GrowthGags.KeyFromSave("E"), "never the interact key");
            Assert.AreEqual("F", GrowthGags.KeyFromSave("W"), "never a walking key");
            Assert.AreEqual("F", GrowthGags.KeyFromSave("no such key"));
            var save = new GrowthSave { humourLevel = 1, gagLevel = 2, slapKey = "Q" };
            var back = JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(save));
            Assert.AreEqual(1, back.humourLevel); Assert.AreEqual(2, back.gagLevel); Assert.AreEqual("Q", back.slapKey);
            Assert.IsFalse(GrowthGags.IsReserved("F"), "F does nothing else");
            Assert.IsTrue(GrowthGags.IsReserved("E") && GrowthGags.IsReserved("Backspace"), "E and Skip are taken");
            Assert.AreEqual("7", GrowthGags.KeyFromSave("Digit7"));
        }
    }
}
