using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-18 wave 1: the people kit (Tools/Art/create_people_kit.py) against the game. Every part a look can ask for is in
    /// the kit and every material slot maps; every kind of visitor assembles for many seeds with its mark and no two alike; the
    /// board's Blender angles turn the joints the same way in Unity; and through every motion a person stays upright on the
    /// floor and inside a person's room. Wave 2: the helpers are the board's cast (Pip, Nori with the jar, Moss in lilac gloves
    /// with the pipette and clipboard), Moss's pipette takes the dose's colour, and at work and walking they stay on the floor.</summary>
    public sealed class GrowthPeopleTests
    {
        static readonly string[] Kinds = { "casual", "bargain", "collector", "premium", "guest" };
        /// <summary>GR-18 wave 2: the helpers, each one person.</summary>
        static readonly string[] Helpers = { "pip", "nori", "moss" };
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        readonly List<GameObject> made = new List<GameObject>();

        [TearDown] public void CleanUp() { foreach (var g in made) if (g) Object.DestroyImmediate(g); made.Clear(); }

        GrowthPerson Build(string kind, int seed)
        {
            var parent = new GameObject("People test"); made.Add(parent);
            var p = GrowthPerson.Build(GrowthPersonLook.For(kind, seed), parent.transform);
            Assert.IsNotNull(p, kind + " " + seed + " builds");
            return p;
        }

        static Bounds Bounds(GrowthPerson p)
        {
            var rs = p.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void TheKitHasEveryKindAndEveryPartALookCanAskFor()
        {
            var data = GrowthPeopleKit.Data;
            Assert.IsNotNull(data, "people.json loads");
            CollectionAssert.AreEquivalent(Kinds.Concat(Helpers).ToArray(), data.kinds.Select(k => k.id).ToArray());
            foreach (var k in data.kinds)
            {
                foreach (var part in k.parts) Assert.IsNotNull(GrowthPeopleKit.Part("PK_Body_" + k.id, part), k.id + " has " + part);
                foreach (var j in new[] { "root", "hips", "spine", "head", "shoulder.L", "elbow.L", "wrist.L", "shoulder.R", "elbow.R", "wrist.R", "hip.L", "knee.L", "ankle.L", "hip.R", "knee.R", "ankle.R" })
                    Assert.IsTrue(k.joints.Any(x => x.name == j), k.id + " has the joint " + j);
                foreach (var side in new[] { "L", "R" })
                    foreach (var shape in new[] { "relaxed", "grip", "point", "open" })
                        Assert.IsTrue(k.parts.Contains("wrist." + side + "__hand-" + shape), k.id + " has a " + shape + " " + side + " hand");
            }
            foreach (var part in data.head.parts) Assert.IsNotNull(GrowthPeopleKit.Part("PK_Head", part), "the head kit has " + part);
            foreach (var part in data.held.parts) Assert.IsNotNull(GrowthPeopleKit.Part("PK_Held", part), "the held kit has " + part);
            // Every head part any look of any kind asks for, over many seeds.
            foreach (var kind in Kinds.Concat(Helpers))
                for (int seed = 1; seed <= 200; seed++)
                {
                    var look = GrowthPersonLook.For(kind, seed);
                    foreach (var p in look.HeadParts().Concat(new[] { "mouth-" + look.Mouth }).Concat(GrowthPersonLook.Expressions.Select(e => "mouth-" + e)))
                        Assert.IsTrue(data.head.parts.Contains("head__" + p), kind + " " + seed + " asks for head__" + p);
                    if (look.HeldLeft != null) Assert.IsTrue(data.held.parts.Contains(look.HeldLeft + "__L"), "the held kit has " + look.HeldLeft + " for the left hand");
                    if (look.HeldRight != null) Assert.IsTrue(data.held.parts.Contains(look.HeldRight + "__R"), "the held kit has " + look.HeldRight + " for the right hand");
                    foreach (var item in new[] { "creature_bag", "coin" })
                        Assert.IsTrue(data.held.parts.Contains(item + "__L") && data.held.parts.Contains(item + "__R"), "the held kit has " + item);
                }
            Assert.IsTrue(data.held.parts.Contains("jar__R"), "the held kit has the helpers' jar (carried in both arms, on the right wrist)");
        }

        [Test]
        public void EveryKindAssemblesInTheLooksMaterialsWithItsMark()
        {
            foreach (var kind in Kinds)
                for (int seed = 1; seed <= 12; seed++)
                {
                    var p = Build(kind, seed);
                    var mats = p.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
                    Assert.IsFalse(mats.Any(m => m == null), kind + " " + seed + ": no missing material");
                    Assert.IsFalse(mats.Any(m => m.name.StartsWith("PK_")), kind + " " + seed + ": every kit slot mapped onto the look's materials");
                    Assert.IsFalse(mats.Any(m => m.color == Color.magenta), kind + " " + seed + ": every role filled in");
                    Assert.Greater(p.Renderers, 15, kind + " " + seed + " is drawn from its parts");
                    Assert.Less(p.Triangles, 120000, kind + " " + seed + " within a person's budget");
                    var held = p.HeldShown.ToList();
                    switch (kind)
                    {
                        case "bargain": CollectionAssert.IsSubsetOf(new[] { "L.netbag", "R.coupons" }, held, "the bargain hunter's coupons and shopping"); break;
                        case "collector": CollectionAssert.Contains(held, "L.notebook", "the collector's notebook"); break;
                        case "premium": CollectionAssert.IsSubsetOf(new[] { "L.bags", "R.bags" }, held, "the big spender's shopping bags"); break;
                        case "guest": CollectionAssert.Contains(held, "L.program", "the guest's programme"); break;
                        case "casual": Assert.IsEmpty(held, "the casual visitor's hands free"); break;
                    }
                    Assert.AreEqual(p.Look.HeldLeft != null ? "grip" : "relaxed", p.HandShape("L"), kind + " " + seed + "'s left hand");
                    Assert.AreEqual(p.Look.Mouth, p.MouthShown, kind + " " + seed + " wears their own mouth");
                    // Every part of the head the look asks for is on the person (their hair, hat, glasses, nose...), not only in the look.
                    var placed = p.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                    foreach (var part in p.Look.HeadParts()) Assert.Contains("head__" + part, placed, kind + " " + seed + " wears their head's " + part);
                    var b = Bounds(p);
                    Assert.AreEqual(GrowthPeopleKit.Kind(kind).height * p.Look.Height, b.max.y, p.Look.Hat == "tophat" ? 0.36f : 0.16f, kind + " " + seed + " is their height (a hat may add a little, a top hat more)");
                    Assert.Less(Mathf.Abs(b.min.y), 0.03f, kind + " " + seed + " stands on the floor");
                }
        }

        [Test]
        public void NoTwoVisitorsAlikeButEachSeedTheSame()
        {
            foreach (var kind in Kinds)
            {
                var looks = Enumerable.Range(1, 30).Select(s => GrowthPersonLook.For(kind, s)).ToList();
                var keys = looks.Select(l => l.Colours["SKIN"] + l.Colours["HAIR"] + l.Hair + l.Nose + l.Mouth + l.Colours["TOP"] + l.Height.ToString("0.00")).ToList();
                Assert.Greater(keys.Distinct().Count(), 27, kind + ": thirty visitors, (nearly) all different");
                Assert.Greater(looks.Select(l => l.Colours["SKIN"]).Distinct().Count(), 4, kind + ": many skin tones");
                Assert.Greater(looks.Select(l => l.Hair).Distinct().Count(), 2, kind + ": several hair styles");
                Assert.IsTrue(looks.All(l => l.Height >= 0.93f && l.Height <= 1.06f && l.Build >= 0.92f && l.Build <= 1.1f), kind + ": heights and builds in range");
                var again = GrowthPersonLook.For(kind, 7);
                Assert.AreEqual(looks[6].Hair + looks[6].Colours["SKIN"] + looks[6].Height, again.Hair + again.Colours["SKIN"] + again.Height, kind + ": a seed always gives the same visitor");
            }
            Assert.IsTrue(Enumerable.Range(1, 50).Select(s => GrowthPersonLook.For("premium", s)).All(l => l.Sunglasses), "every big spender in sunglasses");
            Assert.IsTrue(Enumerable.Range(1, 50).Select(s => GrowthPersonLook.For("bargain", s)).All(l => l.Hat == "bucket" && l.Glasses == "round"), "every bargain hunter in the bucket hat and round glasses");
            Assert.IsTrue(Enumerable.Range(1, 50).Select(s => GrowthPersonLook.For("guest", s)).All(l => l.Hat == "tophat"), "every guest in a top hat");
            Assert.IsTrue(Enumerable.Range(1, 200).SelectMany(s => Kinds.Select(k => GrowthPersonLook.For(k, s))).Where(l => l.Hat != null).All(l => GrowthPersonLook.HatHair.Contains(l.Hair)), "hats only on hair that sits under one");
            Assert.AreEqual("guest", GrowthPersonLook.KindOf(Growth.GrowthShop.GuestType));
            Assert.AreEqual("casual", GrowthPersonLook.KindOf("casual"));
        }

        [Test]
        public void TheBoardsAnglesTurnTheJointsTheSameWayInUnity()
        {
            var p = Build("casual", 3);
            var pose = new GrowthPersonPose();
            p.Apply(pose);
            Vector3 Wrist(string s) => p.Joint("wrist." + s).position;
            Vector3 Ankle(string s) => p.Joint("ankle." + s).position;
            Vector3 headAt = p.Joint("head").position, wristR = Wrist("R"), ankleL = Ankle("L");
            // The person faces +Z, their left at -X (the kit's mapping).
            Assert.Less(p.Joint("shoulder.L").position.x, 0, "the left shoulder at Unity -X");
            pose.E[GrowthPersonPose.ShoulderR] = new Vector3(40, 0, 0); p.Apply(pose);
            Assert.Less(Wrist("R").z, wristR.z - 0.1f, "Blender X positive on a shoulder swings the arm back (Unity -Z)");
            pose.Clear(); pose.E[GrowthPersonPose.HipL] = new Vector3(-30, 0, 0); p.Apply(pose);
            Assert.Greater(Ankle("L").z, ankleL.z + 0.1f, "Blender X negative on a hip swings the leg forward (Unity +Z)");
            pose.Clear(); pose.E[GrowthPersonPose.Spine] = new Vector3(20, 0, 0); p.Apply(pose);
            Assert.Greater(p.Joint("head").position.z, headAt.z + 0.05f, "Blender X positive on the spine leans forward");
            pose.Clear(); pose.E[GrowthPersonPose.Head] = new Vector3(0, 0, 30); p.Apply(pose);
            Assert.Less(p.Joint("head").forward.x, -0.3f, "Blender Z positive turns the head toward the person's left (Unity -X)");
            pose.Clear(); pose.E[GrowthPersonPose.ShoulderR] = new Vector3(0, 60, 0); p.Apply(pose);
            Assert.Greater(Wrist("R").x, wristR.x + 0.15f, "Blender Y positive raises the right arm out to the side (Unity +X)");
            pose.Clear(); pose.Lift = 0.1f; p.Apply(pose);
            Assert.AreEqual(headAt.y + 0.1f * p.Look.Height, p.Joint("head").position.y, 0.001f, "the hips' lift raises the person");
        }

        [Test]
        public void ThroughEveryMotionAPersonStaysOnTheFloorAndInAPersonsRoom()
        {
            var pose = new GrowthPersonPose();
            var motions = new (string name, System.Action<GrowthPersonPose, float> make)[]
            {
                ("stand", (q, t) => GrowthPeopleMotion.Stand(q, t * 10)), ("browse", GrowthPeopleMotion.Browse), ("wait", (q, t) => GrowthPeopleMotion.Wait(q, t, 1)),
                ("pay", GrowthPeopleMotion.Pay), ("happy", GrowthPeopleMotion.Happy), ("shrug", GrowthPeopleMotion.Shrug), ("huff", GrowthPeopleMotion.Huff),
                ("wave", GrowthPeopleMotion.WaveBye), ("clap", GrowthPeopleMotion.Clap),
            };
            foreach (var kind in Kinds)
            {
                var p = Build(kind, 11);
                float height = GrowthPeopleKit.Kind(kind).height * p.Look.Height;
                foreach (var m in motions.Concat(new (string, System.Action<GrowthPersonPose, float>)[] { ("walk", (q, t) => GrowthPeopleMotion.Walk(q, t, GrowthPeopleMotion.Gaits[kind])) }))
                    for (int i = 0; i < 16; i++)
                    {
                        m.Item2(pose, i / 16f);
                        Assert.IsTrue(pose.E.All(e => !float.IsNaN(e.x) && !float.IsNaN(e.y) && !float.IsNaN(e.z)), kind + " " + m.Item1 + ": finite angles");
                        p.Apply(pose);
                        var b = Bounds(p);
                        Assert.Greater(b.min.y, -0.06f, kind + " " + m.Item1 + " " + i + ": feet no deeper than the shoes' soles allow");
                        Assert.Less(b.max.y, height * 1.25f + 0.3f, kind + " " + m.Item1 + " " + i + ": no taller than a raised hand");
                        Assert.Less(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), 1.25f, kind + " " + m.Item1 + " " + i + ": within a person's reach across");
                        Assert.Less(Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)), 1.25f, kind + " " + m.Item1 + " " + i + ": within a person's reach forward and back");
                    }
            }
        }

        [Test]
        public void TheHelpersAreTheBoardsCast()
        {
            var marks = new Dictionary<string, string[]>
            {
                ["pip"] = new[] { "hair-pigtails", "hat-cap", "freckles", "nose-button" },
                ["nori"] = new[] { "hair-crop", "hat-bandana", "brows-bushy", "nose-bulb" },
                ["moss"] = new[] { "hair-curly", "hat-goggles", "nose-long" },
            };
            foreach (var id in Helpers)
            {
                var p = Build(id, 1);
                Assert.AreEqual(id, p.Look.Kind);
                var again = GrowthPersonLook.For(id, 99);
                Assert.AreEqual(p.Look.Hair + p.Look.Hat + p.Look.Colours["SKIN"] + p.Look.Mouth, again.Hair + again.Hat + again.Colours["SKIN"] + again.Mouth, id + " is the same person whatever the seed");
                var mats = p.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
                Assert.IsFalse(mats.Any(m => m == null || m.name.StartsWith("PK_") || m.color == Color.magenta), id + ": every kit slot mapped and filled");
                var placed = p.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                foreach (var part in marks[id]) Assert.Contains("head__" + part, placed, id + " wears their " + part);
                Assert.AreEqual(id == "moss" ? "o" : id == "pip" ? "grin" : "smile", p.MouthShown, id + "'s own mouth");
                var b = Bounds(p);
                Assert.AreEqual(GrowthPeopleKit.Kind(id).height, b.max.y, 0.2f, id + " is their height (a hat may add a little)");
                Assert.Less(Mathf.Abs(b.min.y), 0.03f, id + " stands on the floor");
                Assert.Less(p.Triangles, 120000, id + " within a person's budget");
            }
            var nori = Build("nori", 1);
            CollectionAssert.Contains(nori.HeldShown.ToList(), "R.jar", "Nori carries the shop's jar");
            Assert.AreEqual("grip", nori.HandShape("R"));
            Assert.IsNotNull(nori.HeldIn("R", "jar"), "the jar Nori's creatures swim in");
            var moss = Build("moss", 1);
            CollectionAssert.IsSubsetOf(new[] { "R.pipette", "L.clipboard" }, moss.HeldShown.ToList(), "Moss's pipette and clipboard");
            var gloves = moss.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("__hand-")).SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).ToList();
            Assert.IsTrue(gloves.Count > 0 && gloves.All(m => m.name.Contains("#A98BFF")), "Moss's hands in lilac gloves (" + string.Join(", ", gloves.Select(m => m.name).Distinct()) + ")");
            var pip = Build("pip", 1);
            Assert.IsEmpty(pip.HeldShown, "Pip's hands free for the till");
        }

        [Test]
        public void MossesPipetteTakesTheDosesColour()
        {
            var moss = Build("moss", 1);
            var pipette = moss.HeldIn("R", "pipette");
            Assert.IsNotNull(pipette);
            string Fill() => string.Join(",", pipette.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Select(m => m.name));
            StringAssert.Contains(GrowthPersonLook.EmptyDose, Fill(), "an empty pipette, rinsed pale");
            moss.SetColour("DOSE", "#FF5E7E");
            StringAssert.Contains("#FF5E7E", Fill(), "a dose of Bloom fills it pink");
            StringAssert.DoesNotContain(GrowthPersonLook.EmptyDose, Fill());
            moss.SetColour("DOSE", "#3FA9FF");
            StringAssert.Contains("#3FA9FF", Fill(), "a dose of Flow fills it blue");
            Assert.AreEqual("#3FA9FF", moss.Colour("DOSE"));
            Assert.AreEqual("#D99467", moss.Colour("SKIN"), "nothing else changes colour with the dose");
            StringAssert.Contains("#D99467", string.Join(",", moss.GetComponentsInChildren<Transform>(true).Where(t => t.name == "head__skull").SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Select(m => m.name)), "the skin still Moss's");
        }

        [Test]
        public void AtWorkAndWalkingTheHelpersStayOnTheFloorAndInAPersonsRoom()
        {
            var pose = new GrowthPersonPose();
            var work = new (string name, System.Action<GrowthPersonPose, float> make)[] { ("Pip at the till", GrowthPeopleMotion.WorkPip), ("Nori's reach", GrowthPeopleMotion.WorkNori), ("Moss's drops", GrowthPeopleMotion.WorkMoss) };
            foreach (var id in Helpers)
            {
                var p = Build(id, 1);
                float height = GrowthPeopleKit.Kind(id).height;
                var all = work.Concat(new (string, System.Action<GrowthPersonPose, float>)[]
                {
                    ("walk", (q, t) => GrowthPeopleMotion.Walk(q, t, GrowthPeopleMotion.Gaits[id])),
                    ("stand", (q, t) => { GrowthPeopleMotion.Stand(q, t * 10); GrowthPeopleMotion.HoldArms(q, id, p.Look.HeldLeft, p.Look.HeldRight); }),
                });
                foreach (var m in all)
                    for (int i = 0; i < 16; i++)
                    {
                        m.Item2(pose, i / 16f);
                        Assert.IsTrue(pose.E.All(e => !float.IsNaN(e.x) && !float.IsNaN(e.y) && !float.IsNaN(e.z)), id + " " + m.Item1 + ": finite angles");
                        p.Apply(pose);
                        var b = Bounds(p);
                        Assert.Greater(b.min.y, -0.06f, id + " " + m.Item1 + " " + i + ": feet no deeper than the soles allow");
                        Assert.Less(b.max.y, height * 1.25f + 0.3f, id + " " + m.Item1 + " " + i + ": no taller than a raised hand");
                        Assert.Less(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), 1.25f, id + " " + m.Item1 + " " + i + ": within reach across");
                        Assert.Less(Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)), 1.25f, id + " " + m.Item1 + " " + i + ": within reach forward and back");
                    }
            }
            // Nori walks with the jar held out in front in both arms (the board's carry), steady through the stride.
            var a = new GrowthPersonPose(); var c = new GrowthPersonPose();
            GrowthPeopleMotion.Walk(a, 0.1f, GrowthPeopleMotion.Gaits["nori"]); GrowthPeopleMotion.Walk(c, 0.6f, GrowthPeopleMotion.Gaits["nori"]);
            Assert.AreEqual(a.E[GrowthPersonPose.ShoulderR], c.E[GrowthPersonPose.ShoulderR], "Nori's arms hold the jar steady through the stride");
            Assert.AreEqual(a.E[GrowthPersonPose.ShoulderL].x, a.E[GrowthPersonPose.ShoulderR].x, "both arms under the jar");
            Assert.Less(a.E[GrowthPersonPose.ShoulderR].x, -30f, "the jar out in front");
            // Pip's work at the till waves goodbye with the right hand at the loop's end, and Moss's drops come three times a loop.
            GrowthPeopleMotion.WorkPip(a, 0.0f); GrowthPeopleMotion.WorkPip(c, 0.5f);
            Assert.Greater(Mathf.Abs(a.E[GrowthPersonPose.ShoulderR].y - c.E[GrowthPersonPose.ShoulderR].y), 30f, "Pip's arm goes up to wave goodbye and down again");
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void HeldItemsSitUprightInTheHandsThatHoldThem()
        {
            // Each item is made in the arm pose the game holds it in: there it stands as the board drew it, not tipped with the wrist.
            var pose = new GrowthPersonPose();
            var nori = Build("nori", 1);
            GrowthPeopleMotion.Stand(pose, 0); GrowthPeopleMotion.HoldArms(pose, "nori", null, "jar");
            // The arms that hold each item come up to it, as the board's poses (the item is made for them).
            Assert.IsTrue(pose.E[GrowthPersonPose.ShoulderL].x < -40 && pose.E[GrowthPersonPose.ShoulderR].x < -40 && pose.E[GrowthPersonPose.ElbowL].x < -40 && pose.E[GrowthPersonPose.ElbowR].x < -40,
                "Nori's both arms come up and forward to hold the jar");
            nori.Apply(pose);
            var jar = BoundsOf(nori.HeldIn("R", "jar"));
            Assert.Greater(jar.size.y, Mathf.Max(jar.size.x, jar.size.z), "Nori's jar stands upright in both arms (" + jar.size.ToString("F3") + ")");
            Vector3 l = nori.Joint("wrist.L").position, r = nori.Joint("wrist.R").position;
            Assert.IsTrue(jar.center.x > Mathf.Min(l.x, r.x) - 0.06f && jar.center.x < Mathf.Max(l.x, r.x) + 0.06f, "the jar between Nori's hands");
            Assert.Greater(jar.center.z, 0.1f, "held out in front");
            var moss = Build("moss", 1);
            GrowthPeopleMotion.Stand(pose, 0); GrowthPeopleMotion.HoldArms(pose, "moss", "clipboard", "pipette");
            Assert.IsTrue(pose.E[GrowthPersonPose.ElbowR].x < -60 && pose.E[GrowthPersonPose.ElbowL].x < -60, "Moss's arms bend up to hold the pipette and the clipboard");
            moss.Apply(pose);
            var pipette = BoundsOf(moss.HeldIn("R", "pipette"));
            Assert.Greater(pipette.size.y, Mathf.Max(pipette.size.x, pipette.size.z), "Moss's pipette held upright (" + pipette.size.ToString("F3") + ")");
            var clipboard = BoundsOf(moss.HeldIn("L", "clipboard"));
            Assert.Greater(clipboard.size.y, clipboard.size.z, "the clipboard held up and tilted back to read, not lying flat (" + clipboard.size.ToString("F3") + ")");
            var collector = Build("collector", 1);
            GrowthPeopleMotion.Stand(pose, 0); GrowthPeopleMotion.HoldArms(pose, "collector", "notebook", null); collector.Apply(pose);
            var notebook = BoundsOf(collector.HeldIn("L", "notebook"));
            Assert.Greater(notebook.size.y, notebook.size.z, "the collector's notebook held up, not lying flat (" + notebook.size.ToString("F3") + ")");
            var casual = Build("casual", 1);
            casual.ShowHeld("R", "creature_bag");
            GrowthPeopleMotion.Stand(pose, 0); pose.E[GrowthPersonPose.ShoulderR] = new Vector3(-50, 10, 0); pose.E[GrowthPersonPose.ElbowR] = new Vector3(-96, 0, 30); casual.Apply(pose);
            var bag = BoundsOf(casual.HeldIn("R", "creature_bag"));
            Assert.Greater(bag.size.y, Mathf.Max(bag.size.x, bag.size.z) * 0.9f, "the bag hugged to the chest hangs upright, its knot at the top (" + bag.size.ToString("F3") + ")");
        }

        [Test]
        public void TheKitsRecordMatchesItsFiles()
        {
            var manifest = File.ReadAllText(Path.Combine(Repo, "ArtSource", "PeopleKit", "manifest.json"));
            StringAssert.Contains("\"external_sources\": []", manifest);
            StringAssert.Contains("\"paid_generation\": false", manifest);
            foreach (var asset in Kinds.Select(k => "PK_Body_" + k).Concat(new[] { "PK_Head", "PK_Held", "PK_Hands" }))
            {
                var path = Path.Combine(Application.dataPath, "EvolutionShop", "Resources", "Art", "PeopleKit", asset + ".fbx");
                Assert.IsTrue(File.Exists(path), asset + ".fbx exists");
                string sha;
                using (var s = SHA256.Create()) sha = string.Concat(s.ComputeHash(File.ReadAllBytes(path)).Select(x => x.ToString("x2")));
                StringAssert.Contains(sha, manifest, asset + " is the one the kit's record lists");
            }
        }
    }
}
