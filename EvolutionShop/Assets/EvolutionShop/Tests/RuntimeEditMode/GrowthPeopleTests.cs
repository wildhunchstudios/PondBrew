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
    /// floor and inside a person's room.</summary>
    public sealed class GrowthPeopleTests
    {
        static readonly string[] Kinds = { "casual", "bargain", "collector", "premium", "guest" };
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
            CollectionAssert.AreEquivalent(Kinds, data.kinds.Select(k => k.id).ToArray());
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
            foreach (var kind in Kinds)
                for (int seed = 1; seed <= 200; seed++)
                {
                    var look = GrowthPersonLook.For(kind, seed);
                    foreach (var p in look.HeadParts().Concat(new[] { "mouth-" + look.Mouth }).Concat(GrowthPersonLook.Expressions.Select(e => "mouth-" + e)))
                        Assert.IsTrue(data.head.parts.Contains("head__" + p), kind + " " + seed + " asks for head__" + p);
                    foreach (var item in new[] { look.HeldLeft, look.HeldRight, "creature_bag", "coin" }.Where(x => x != null))
                        Assert.IsTrue(data.held.parts.Contains(item + "__L") && data.held.parts.Contains(item + "__R"), "the held kit has " + item);
                }
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
        public void TheKitsRecordMatchesItsFiles()
        {
            var manifest = File.ReadAllText(Path.Combine(Repo, "ArtSource", "PeopleKit", "manifest.json"));
            StringAssert.Contains("\"external_sources\": []", manifest);
            StringAssert.Contains("\"paid_generation\": false", manifest);
            foreach (var asset in Kinds.Select(k => "PK_Body_" + k).Concat(new[] { "PK_Head", "PK_Held" }))
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
