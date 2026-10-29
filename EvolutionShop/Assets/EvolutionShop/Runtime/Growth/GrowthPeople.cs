using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// GR-18 wave 1: the new game's people in the look the user approved (GR.55, "Looks good"): glossy cartoon figures of
    /// rounded parts, three heads tall. The parts are the people kit's (Tools/Art/create_people_kit.py, built with the look
    /// board's own builder, Tools/Art/gr18_characters.py; Resources/Art/PeopleKit): a mesh per joint and variant for each kind
    /// of visitor, a head kit of swappable parts one unit tall, and what visitors hold. A person is assembled from them by a
    /// look drawn from their kind and a seed (no two alike), and moved by turning their joints with the board's motions,
    /// ported here (GrowthPeopleMotion): rotations written as the board's Blender Euler angles and turned into Unity's.
    /// Presentation only: the rules decide what a visitor does; nothing here pays, sells or moves stock.
    /// </summary>
    public static class GrowthPeopleKit
    {
        public const string Path = "Art/PeopleKit/";

        [Serializable] public sealed class JointData { public string name; public string parent; public float[] rest; }
        [Serializable] public sealed class KindData { public string id; public float height, hh, build, gripDrop; public float[] headCentre; public JointData[] joints; public string[] parts; }
        [Serializable] public sealed class HeadData { public float jointToCentre; public string[] parts; }
        [Serializable] public sealed class HeldData { public float hh; public float[] wristL, wristR; public string[] parts; }
        [Serializable] public sealed class SlotData { public string name, kind, colour; }
        [Serializable] public sealed class KitData { public KindData[] kinds; public HeadData head; public HeldData held; public SlotData[] materials; }

        static KitData data;
        static readonly Dictionary<string, GameObject> assets = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        /// <summary>The kit's record (null when the kit is missing, so people fall back to the greybox's figures).</summary>
        public static KitData Data
        {
            get
            {
                if (data != null) return data;
                var text = Resources.Load<TextAsset>(Path + "people");
                if (text == null) return null;
                data = JsonUtility.FromJson<KitData>(text.text);
                return data;
            }
        }

        public static bool Available => Data != null && Data.kinds != null && Data.kinds.Length > 0;

        public static KindData Kind(string id) => Data?.kinds.FirstOrDefault(k => k.id == id);

        public static Vector3 V(float[] a) => a == null || a.Length < 3 ? Vector3.zero : new Vector3(a[0], a[1], a[2]);

        /// <summary>A kit part's GameObject (a child of its asset, by name), or null.</summary>
        public static GameObject Part(string asset, string part)
        {
            if (!assets.TryGetValue(asset, out var go) || go == null) assets[asset] = go = Resources.Load<GameObject>(Path + asset);
            if (go == null) return null;
            var t = go.transform.Find(part);
            return t ? t.gameObject : null;
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        /// <summary>A kit slot's material for a colour (a slot's own hex, or the person's colour for a role): the look's
        /// materials, shared by everyone who wears the same colour.</summary>
        public static Material Material(string kind, string hex)
        {
            string key = kind + hex;
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            var c = Hex(hex);
            switch (kind)
            {
                case "glass": c.a = 0.12f; m = DioramaLook.Glass("GR people glass " + hex, c, 0.55f, 0.75f); break;
                case "water": c.a = 0.28f; m = DioramaLook.Glass("GR people water " + hex, c, 0.4f, 0.5f); break;
                case "metal": m = ShopWorld.MakeMaterial("GR people metal " + hex, c, 0.8f, 0.72f); break;
                case "liquid":
                case "emit":
                    m = ShopWorld.MakeMaterial("GR people glow " + hex, c, 0, 0.6f);
                    m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.6f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    break;
                default: m = ShopWorld.MakeMaterial("GR people " + kind + " " + hex, c, 0, Smoothness(kind)); break;
            }
            materials[key] = m;
            return m;
        }

        /// <summary>How glossy each material kind is (the look's glossy plastic; skin and cloth softer).</summary>
        public static float Smoothness(string kind)
        {
            switch (kind)
            {
                case "cloth": return 0.32f;
                case "skin": return 0.48f;
                case "hair": return 0.72f;
                case "gloss": return 0.78f;
                case "eye": return 0.88f;
                case "plastic": return 0.62f;
                default: return 0.5f;
            }
        }
    }

    /// <summary>How a person looks: their kind's outfit with this person's own skin, hair, face, colours and size.</summary>
    public sealed class GrowthPersonLook
    {
        public string Kind;
        public int Seed;
        public float Height = 1, Build = 1, Wide = 1;
        public string Hair = "crop", Nose = "button", Mouth = "smile", Hat, Glasses, Bottom = "trousers";
        public bool Fringe, Lids, Lashes, Sunglasses, Mustache, Freckles, Mole, BushyBrows;
        /// <summary>The colour of each role the kit's slots name (SKIN, HAIR, IRIS, TOP, ...), as sRGB hex.</summary>
        public readonly Dictionary<string, string> Colours = new Dictionary<string, string>();
        /// <summary>What the person carries in each hand all the time (the kind's mark), by the held kit's item name, or null.</summary>
        public string HeldLeft, HeldRight;

        public static readonly string[] Skins = { "#FFDCC2", "#F7C6A0", "#EFB088", "#D99467", "#B87448", "#93583A", "#6E412B" };
        public static readonly string[] HairColours = { "#2E2430", "#6B4329", "#8E4F2A", "#B5502F", "#F2C45A", "#E5793B", "#2E2430", "#FF8FB8", "#2FB9AE", "#A98BFF" };
        public static readonly string[] Irises = { "#5A3A24", "#7A5230", "#3E6FB0", "#3F7F4F", "#6E7682", "#2E2430" };
        public static readonly string[] HairStyles = { "crop", "bob", "bun", "curly", "ponytail", "puffs", "spiky", "long", "crop" };
        /// <summary>Hair that sits under a hat without poking through it.</summary>
        public static readonly string[] HatHair = { "crop", "bob", "long", "ponytail", "bald" };
        static readonly Dictionary<string, string[]> TopColours = new Dictionary<string, string[]>
        {
            ["casual"] = new[] { "#22BFB0", "#2BB4C9", "#3FCB9A", "#4FB8A8" },
            ["bargain"] = new[] { "#FFC93C", "#FFB238", "#F7D154" },
            ["collector"] = new[] { "#FF7A7A", "#F2685E", "#FF8F70" },
            ["premium"] = new[] { "#A98BFF", "#B79CFF", "#9575F0" },
            ["guest"] = new[] { "#F2C14E" },
        };

        /// <summary>The kind of figure for a visitor's type (the rules' visitor types; guests are the exhibition's).</summary>
        public static string KindOf(string visitorType) => visitorType == GrowthShop.GuestType ? "guest" : visitorType == "bargain" || visitorType == "collector" || visitorType == "premium" ? visitorType : "casual";

        /// <summary>A kind's part of a look's seed, the same in every run (string.GetHashCode is not promised to be).</summary>
        static int KindSeed(string kind) { int h = 0; foreach (char ch in kind) h = (h * 31 + ch) % 1000; return h; }

        public static GrowthPersonLook For(string kind, int seed)
        {
            var r = new System.Random(seed * 7919 + KindSeed(kind) * 31 + 17);
            T Pick<T>(IList<T> list) => list[r.Next(list.Count)];
            float Range(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var look = new GrowthPersonLook { Kind = kind, Seed = seed };
            var c = look.Colours;
            c["SKIN"] = Pick(Skins);
            c["HAIR"] = Pick(HairColours);
            c["IRIS"] = Pick(Irises);
            c["BAND"] = Pick(new[] { "#FF7A7A", "#FFC93C", "#9BDB4D" });
            c["TOP"] = Pick(TopColours[kind]);
            look.Height = Range(0.93f, 1.06f);
            look.Build = Range(0.92f, 1.1f);
            look.Wide = Range(0.95f, 1.06f);
            look.Nose = Pick(new[] { "button", "button", "bulb", "long" });
            look.Mouth = Pick(new[] { "smile", "smile", "small", "grin", "smirk" });
            look.Freckles = r.NextDouble() < 0.25;
            look.Fringe = r.NextDouble() < 0.5;
            look.BushyBrows = r.NextDouble() < 0.2;
            look.Hair = Pick(HairStyles);
            switch (kind)
            {
                case "casual":
                    look.Bottom = Pick(new[] { "trousers", "trousers", "shorts" });
                    c["BOTTOM"] = Pick(new[] { "#5C7FD6", "#FFF3DF", "#2B3A55", "#C9A46A", "#7D6BC4" });
                    c["SOCKS"] = Pick(new[] { "#FFFFFF", "#FFC93C", "#FF9EC4" });
                    c["SHOE"] = Pick(new[] { "#FFFFFF", "#FFC93C", "#FF7A7A", "#2B3A55" });
                    c["SOLE"] = Pick(new[] { "#FF7A7A", "#FFF3DF", "#22BFB0" });
                    c["SHOEACC"] = Pick(new[] { "#22BFB0", "#FF9EC4", "#9BDB4D" });
                    c["BAG"] = Pick(new[] { "#FFF3DF", "#FFC93C", "#FF9EC4", "#9BDB4D" });
                    c["PRINT"] = Pick(new[] { "#FF7A7A", "#22BFB0", "#A98BFF" });
                    if (Array.IndexOf(new[] { "crop", "bob", "long", "ponytail" }, look.Hair) >= 0)
                    {
                        int h = r.Next(4);
                        if (h == 2) { look.Hat = "beanie"; c["HAT"] = Pick(new[] { "#FF7A7A", "#FFC93C", "#A98BFF" }); c["HATBAND"] = "#FFF3DF"; }
                        if (h == 3) { look.Hat = "beret"; c["HAT"] = Pick(new[] { "#FF7A7A", "#2B3A55" }); c["HATBAND"] = "#FFF3DF"; }
                    }
                    if (r.NextDouble() < 0.3) { look.Glasses = Pick(new[] { "round", "square" }); c["GLASSES"] = Pick(new[] { "#2B3A55", "#FF7A7A", "#15867F" }); }
                    break;
                case "bargain":
                    look.Hair = Pick(new[] { "bald", "bald", "crop", "bob" });
                    look.Hat = "bucket"; c["HAT"] = "#FFC93C"; c["HATBAND"] = "#FF7A7A";
                    look.Glasses = "round"; c["GLASSES"] = "#2B3A55";
                    look.Mustache = r.NextDouble() < 0.6;
                    c["HAIR"] = Pick(new[] { "#C9CBD3", "#F1F1F4", "#6B4329", "#2E2430" });
                    look.HeldLeft = "netbag"; look.HeldRight = "coupons";
                    break;
                case "collector":
                    look.Lids = true;
                    look.Glasses = "square"; c["GLASSES"] = Pick(new[] { "#FF7A7A", "#2B3A55", "#15867F" });
                    look.HeldLeft = "notebook";
                    break;
                case "premium":
                    look.Sunglasses = true;
                    look.Hair = Pick(new[] { "pompadour", "pompadour", "crop", "bun", "curly" });
                    look.HeldLeft = "bags"; look.HeldRight = "bags";
                    break;
                case "guest":
                    look.Lids = true; look.Lashes = r.NextDouble() < 0.6; look.Mole = r.NextDouble() < 0.3;
                    look.Hat = "tophat"; c["HAT"] = "#2B3A55"; c["HATBAND"] = "#F4C04E";
                    look.HeldLeft = "program";
                    break;
            }
            if (look.Hat != null && Array.IndexOf(HatHair, look.Hair) < 0) look.Hair = Pick(new[] { "crop", "bob", "long" });
            if (look.Hair != "crop" && look.Hair != "bob" && look.Hair != "long") look.Fringe = false;
            return look;
        }

        /// <summary>The head kit's parts this look wears (each "head__<part>").</summary>
        public IEnumerable<string> HeadParts()
        {
            yield return "skull";
            if (!Sunglasses) yield return "eyes";
            if (Lids && !Sunglasses) yield return "lids";
            if (Lashes && !Sunglasses) yield return "lashes";
            yield return BushyBrows ? "brows-bushy" : "brows";
            yield return "nose-" + Nose;
            if (Mustache) yield return "mustache";
            if (Freckles) yield return "freckles";
            if (Mole) yield return "mole";
            yield return "hair-" + Hair;
            if (Fringe) yield return "fringe-" + Hair;
            if (Hat != null) yield return "hat-" + Hat;
            if (Glasses != null && !Sunglasses) yield return "glasses-" + Glasses;
            if (Sunglasses) yield return "sunglasses";
        }

        /// <summary>The mouths a face can make: the person's own, a grin when happy, an "o" when not.</summary>
        public static readonly string[] Expressions = { "grin", "o" };
    }

    /// <summary>A pose as the board writes it: each joint's Blender Euler angles (degrees), and the hips' lift, sway and surge.</summary>
    public sealed class GrowthPersonPose
    {
        public static readonly string[] Joints = { "hips", "spine", "head", "shoulder.L", "elbow.L", "wrist.L", "shoulder.R", "elbow.R", "wrist.R", "hip.L", "knee.L", "ankle.L", "hip.R", "knee.R", "ankle.R" };
        public const int Hips = 0, Spine = 1, Head = 2, ShoulderL = 3, ElbowL = 4, WristL = 5, ShoulderR = 6, ElbowR = 7, WristR = 8, HipL = 9, KneeL = 10, AnkleL = 11, HipR = 12, KneeR = 13, AnkleR = 14;
        public readonly Vector3[] E = new Vector3[Joints.Length];
        public float Lift, Sway, Surge;

        public void Clear() { for (int i = 0; i < E.Length; i++) E[i] = Vector3.zero; Lift = Sway = Surge = 0; }

        public void CopyFrom(GrowthPersonPose p) { Array.Copy(p.E, E, E.Length); Lift = p.Lift; Sway = p.Sway; Surge = p.Surge; }

        /// <summary>Blends toward another pose by w (0..1).</summary>
        public void Blend(GrowthPersonPose to, float w)
        {
            for (int i = 0; i < E.Length; i++) E[i] = Vector3.LerpUnclamped(E[i], to.E[i], w);
            Lift = Mathf.LerpUnclamped(Lift, to.Lift, w); Sway = Mathf.LerpUnclamped(Sway, to.Sway, w); Surge = Mathf.LerpUnclamped(Surge, to.Surge, w);
        }

        /// <summary>A Blender Euler rotation (XYZ, degrees, the board's) as Unity's: the kit's mapping (Blender (x, y, z) at Unity
        /// (-x, z, -y)) is a reflection, so a turn about Blender's X stays a turn about Unity's X, Blender's Y becomes Unity's Z,
        /// and Blender's Z becomes Unity's Y the other way round.</summary>
        public static Quaternion ToUnity(Vector3 e) =>
            Quaternion.AngleAxis(-e.z, Vector3.up) * Quaternion.AngleAxis(e.y, Vector3.forward) * Quaternion.AngleAxis(e.x, Vector3.right);

        /// <summary>The hips' offset in Unity space from the board's lift (up), sway (Blender +X, the person's left) and surge.</summary>
        public Vector3 HipsOffset => new Vector3(-Sway, Lift, -Surge);
    }

    /// <summary>The board's motions (Tools/Art/gr18_characters.py: idle, walk, and the visitors' moments), ported: each writes a
    /// pose for a time or phase. t runs 0..1 over a loop; a walk's phase 0..1 is two steps.</summary>
    public static class GrowthPeopleMotion
    {
        public sealed class Gait { public float Stride, Knee, Arm, Bob, Sway, Twist, Lean, Head, Out, Elbow, Cycle; public bool Behind, Carry; }

        /// <summary>Each kind's walk (the board's WALKS), and how far its two steps carry it (metres).</summary>
        public static readonly Dictionary<string, Gait> Gaits = new Dictionary<string, Gait>
        {
            ["casual"] = new Gait { Stride = 24, Knee = 42, Arm = 26, Bob = 0.02f, Sway = 0.012f, Twist = 6, Lean = 3, Head = -2, Out = 0, Elbow = -14, Cycle = 1.15f },
            ["bargain"] = new Gait { Stride = 20, Knee = 46, Arm = 34, Bob = 0.016f, Sway = 0.018f, Twist = 5, Lean = 8, Head = 4, Out = 6, Elbow = -28, Cycle = 0.9f },
            ["collector"] = new Gait { Stride = 22, Knee = 36, Arm = 4, Bob = 0.012f, Sway = 0.008f, Twist = 3, Lean = -2, Head = -6, Out = 0, Elbow = -10, Behind = true, Cycle = 1.3f },
            ["premium"] = new Gait { Stride = 22, Knee = 38, Arm = 14, Bob = 0.018f, Sway = 0.03f, Twist = 11, Lean = -5, Head = -10, Out = 14, Elbow = -16, Cycle = 1.2f },
            ["guest"] = new Gait { Stride = 18, Knee = 34, Arm = 10, Bob = 0.012f, Sway = 0.01f, Twist = 4, Lean = -4, Head = -8, Out = 4, Elbow = -30, Cycle = 1.1f },
        };

        static float Pulse(float t, float n, float phase = 0, float sharp = 3) => Mathf.Pow(Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * (n * t + phase))), sharp);
        static float Wave(float t, float n = 1, float phase = 0) => Mathf.Sin(2 * Mathf.PI * (n * t + phase));
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        /// <summary>A relaxed stand: the weight on one leg, the arms a little out and bent, the head tipped.</summary>
        public static void Idle(GrowthPersonPose p, float style = 0)
        {
            p.Clear();
            p.E[GrowthPersonPose.Hips] = new Vector3(0, 0, 4); p.E[GrowthPersonPose.Spine] = new Vector3(2, 0, -3); p.E[GrowthPersonPose.Head] = new Vector3(-3, 0, 4 + style * 6);
            p.E[GrowthPersonPose.HipL] = new Vector3(-2, 0, 0); p.E[GrowthPersonPose.HipR] = new Vector3(3, 0, 0); p.E[GrowthPersonPose.KneeR] = new Vector3(6, 0, 0); p.E[GrowthPersonPose.AnkleR] = new Vector3(-3, 0, 0);
            p.E[GrowthPersonPose.ShoulderL] = new Vector3(-4, -8, 0); p.E[GrowthPersonPose.ShoulderR] = new Vector3(-2, 8, 0);
            p.E[GrowthPersonPose.ElbowL] = new Vector3(-22, 0, 0); p.E[GrowthPersonPose.ElbowR] = new Vector3(-24, 0, 0);
        }

        /// <summary>A breath and a glance around while standing (t in seconds).</summary>
        public static void Stand(GrowthPersonPose p, float t, float style = 0)
        {
            Idle(p, style);
            p.E[GrowthPersonPose.Spine] += new Vector3(1.5f * Wave(t / 3.2f), 0, 0);
            p.E[GrowthPersonPose.Head] += new Vector3(0, 0, 14 * Wave(t / 7.3f) * Wave(t / 2.9f, 1, 0.2f));
            p.Lift = 0.004f * Wave(t / 3.2f);
        }

        /// <summary>A walking pose at phase t (0..1 is two steps), the board's walk.</summary>
        public static void Walk(GrowthPersonPose p, float t, Gait w)
        {
            p.Clear();
            float a = 2 * Mathf.PI * t, s = Mathf.Sin(a), c = Mathf.Cos(a);
            float kl = w.Knee * Mathf.Pow(Mathf.Max(0, c), 1.5f) + 6, kr = w.Knee * Mathf.Pow(Mathf.Max(0, -c), 1.5f) + 6;
            p.Lift = w.Bob * c * c - w.Bob * 0.5f; p.Sway = -w.Sway * c;
            p.E[GrowthPersonPose.Hips] = new Vector3(0, 0, w.Twist * s);
            p.E[GrowthPersonPose.Spine] = new Vector3(w.Lean, 0, -w.Twist * 0.7f * s);
            p.E[GrowthPersonPose.Head] = new Vector3(w.Head - w.Lean * 0.6f, 0, w.Twist * 0.3f * s);
            p.E[GrowthPersonPose.HipL] = new Vector3(-w.Stride * s - kl * 0.3f, 0, 0); p.E[GrowthPersonPose.HipR] = new Vector3(w.Stride * s - kr * 0.3f, 0, 0);
            p.E[GrowthPersonPose.KneeL] = new Vector3(kl, 0, 0); p.E[GrowthPersonPose.KneeR] = new Vector3(kr, 0, 0);
            p.E[GrowthPersonPose.AnkleL] = new Vector3(-kl * 0.35f + 6 * s, 0, 0); p.E[GrowthPersonPose.AnkleR] = new Vector3(-kr * 0.35f - 6 * s, 0, 0);
            if (w.Behind)
            {
                p.E[GrowthPersonPose.ShoulderL] = new Vector3(28, 0, -10); p.E[GrowthPersonPose.ShoulderR] = new Vector3(28, 0, 10);
                p.E[GrowthPersonPose.ElbowL] = new Vector3(-70, 0, 20); p.E[GrowthPersonPose.ElbowR] = new Vector3(-70, 0, -20);
            }
            else
            {
                p.E[GrowthPersonPose.ShoulderL] = new Vector3(w.Arm * s, -w.Out, 0); p.E[GrowthPersonPose.ShoulderR] = new Vector3(-w.Arm * s, w.Out, 0);
                p.E[GrowthPersonPose.ElbowL] = new Vector3(w.Elbow - Mathf.Max(0, -s) * 12, 0, 0); p.E[GrowthPersonPose.ElbowR] = new Vector3(w.Elbow - Mathf.Max(0, s) * 12, 0, 0);
            }
        }

        /// <summary>At a tank: leaning in, pointing at something swimming past, a nod (the board's browse; t over a 4 s loop).</summary>
        public static void Browse(GrowthPersonPose p, float t)
        {
            Idle(p, -0.4f);
            float lean = 0.5f + 0.5f * Wave(t), point = Smooth(Wave(t, 1, 0.1f) * 2);
            p.E[GrowthPersonPose.Spine] += new Vector3(10 + 12 * lean, 0, 0);
            p.E[GrowthPersonPose.Head] += new Vector3(2 + 10 * Pulse(t, 2, 0.3f), 0, -6 + 10 * Wave(t, 1, 0.3f));
            p.E[GrowthPersonPose.ShoulderR] += new Vector3(-20 - 56 * point, 10, 0);
            p.E[GrowthPersonPose.ElbowR] += new Vector3(-30 + 22 * point, 0, 0);
            p.E[GrowthPersonPose.HipL] += new Vector3(-6 * lean, 0, 0); p.E[GrowthPersonPose.HipR] += new Vector3(-6 * lean, 0, 0);
            p.E[GrowthPersonPose.KneeL] += new Vector3(6 * lean, 0, 0); p.E[GrowthPersonPose.KneeR] += new Vector3(6 + 6 * lean, 0, 0);
        }

        /// <summary>Waiting in the queue: as patience runs low (impatience 0..1) a foot taps and a look goes to the wrist.</summary>
        public static void Wait(GrowthPersonPose p, float t, float impatience)
        {
            Stand(p, t * 4);
            float tap = Pulse(t, 6, 0, 1) * Smooth((impatience - 0.35f) * 3);
            float look = Smooth(Mathf.Clamp01((Wave(t, 1) + 0.2f) * 1.5f)) * Smooth((impatience - 0.55f) * 3);
            p.E[GrowthPersonPose.AnkleR] += new Vector3(-30 * tap, 0, 0); p.E[GrowthPersonPose.KneeR] += new Vector3(12 * tap, 0, 0); p.E[GrowthPersonPose.HipR] += new Vector3(-6 * tap, 0, 0);
            p.E[GrowthPersonPose.ShoulderL] += new Vector3(-62 * look, -6 * look, 0); p.E[GrowthPersonPose.ElbowL] += new Vector3(-88 * look, 0, -60 * look);
            p.E[GrowthPersonPose.Head] += new Vector3(22 * look, 0, -24 * look);
        }

        /// <summary>At the counter: holding out the coins.</summary>
        public static void Pay(GrowthPersonPose p, float t)
        {
            Stand(p, t * 4, 0.2f);
            float reach = 0.85f + 0.15f * Wave(t, 1);
            p.E[GrowthPersonPose.ShoulderR] = new Vector3(-74 * reach, 6, 0); p.E[GrowthPersonPose.ElbowR] = new Vector3(-24, 0, 0);
            p.E[GrowthPersonPose.Spine] += new Vector3(4, 0, 0); p.E[GrowthPersonPose.Head] += new Vector3(-6, 0, 0);
        }

        /// <summary>Leaving with what they bought: hugging the bag, two little hops (the board's happy; t over 1 s per hop pair).</summary>
        public static void Happy(GrowthPersonPose p, float t)
        {
            Idle(p, 0.3f);
            float h = Pulse(t, 2, 0, 2), crouch = Pulse(t, 2, 0.6f, 2);
            p.Lift = 0.13f * h - 0.04f * crouch;
            p.E[GrowthPersonPose.KneeL] = new Vector3(28 * crouch, 0, 0); p.E[GrowthPersonPose.KneeR] = new Vector3(28 * crouch - 6, 0, 0);
            p.E[GrowthPersonPose.HipL] = new Vector3(-14 * crouch, 0, 0); p.E[GrowthPersonPose.HipR] = new Vector3(-14 * crouch, 0, 0);
            p.E[GrowthPersonPose.AnkleL] = new Vector3(-12 * crouch + 18 * h, 0, 0); p.E[GrowthPersonPose.AnkleR] = new Vector3(-12 * crouch + 18 * h, 0, 0);
            p.E[GrowthPersonPose.ShoulderL] = new Vector3(-58, -12, 0); p.E[GrowthPersonPose.ShoulderR] = new Vector3(-58, 12, 0);
            p.E[GrowthPersonPose.ElbowL] = new Vector3(-92, 0, -40); p.E[GrowthPersonPose.ElbowR] = new Vector3(-92, 0, 40);
            p.E[GrowthPersonPose.Head] = new Vector3(-14 * h, 0, 10 * Wave(t, 2)); p.E[GrowthPersonPose.Spine] = new Vector3(-6 * h, 0, 0);
        }

        /// <summary>Nothing they could afford: a shrug with the palms up, a shake of the head.</summary>
        public static void Shrug(GrowthPersonPose p, float t)
        {
            Idle(p, 0);
            float shrug = Pulse(t, 1, 0.1f, 1), shake = Wave(t, 4) * (1 - shrug) * 0.6f;
            p.E[GrowthPersonPose.ShoulderL] = new Vector3(-30 * shrug, -50 * shrug - 8, 0); p.E[GrowthPersonPose.ShoulderR] = new Vector3(-30 * shrug, 50 * shrug + 8, 0);
            p.E[GrowthPersonPose.ElbowL] = new Vector3(-80 * shrug - 20, 0, -50 * shrug); p.E[GrowthPersonPose.ElbowR] = new Vector3(-80 * shrug - 20, 0, 50 * shrug);
            p.E[GrowthPersonPose.Head] = new Vector3(6 + 8 * (1 - shrug), 0, 22 * shake); p.E[GrowthPersonPose.Spine] = new Vector3(8 * (1 - shrug), 0, 0);
            p.Lift = 0.02f * shrug;
        }

        /// <summary>Tired of waiting: arms crossed, a huff with the chin up.</summary>
        public static void Huff(GrowthPersonPose p, float t)
        {
            Idle(p, 0);
            float huff = Pulse(t, 2, 0.1f, 2);
            p.E[GrowthPersonPose.ShoulderL] = new Vector3(-40, -6, 0); p.E[GrowthPersonPose.ShoulderR] = new Vector3(-40, 6, 0);
            p.E[GrowthPersonPose.ElbowL] = new Vector3(-100, 0, -70); p.E[GrowthPersonPose.ElbowR] = new Vector3(-100, 0, 70);
            p.E[GrowthPersonPose.Head] = new Vector3(-12 - 6 * huff, 0, 18 * Wave(t, 1, 0.25f)); p.E[GrowthPersonPose.Spine] = new Vector3(-4 - 3 * huff, 0, 0);
            p.Lift = 0.01f * huff;
        }

        /// <summary>A wave goodbye with the right hand (closing time).</summary>
        public static void WaveBye(GrowthPersonPose p, float t)
        {
            Stand(p, t * 4, 0.4f);
            p.E[GrowthPersonPose.ShoulderR] = new Vector3(-10, 112, 0); p.E[GrowthPersonPose.ElbowR] = new Vector3(0, 46, 0);
            p.E[GrowthPersonPose.WristR] = new Vector3(0, 0, -80 + 18 * Wave(t, 6));
        }

        /// <summary>An exhibition guest applauding.</summary>
        public static void Clap(GrowthPersonPose p, float t)
        {
            Idle(p, -0.2f);
            float c = Pulse(t, 8, 0, 1);
            p.E[GrowthPersonPose.ShoulderL] = new Vector3(-58, -6, 0); p.E[GrowthPersonPose.ShoulderR] = new Vector3(-58, 6, 0);
            p.E[GrowthPersonPose.ElbowL] = new Vector3(-70, 0, -50 + 14 * c); p.E[GrowthPersonPose.ElbowR] = new Vector3(-70, 0, 50 - 14 * c);
            p.E[GrowthPersonPose.Head] = new Vector3(-10, 0, 6 * Wave(t, 2)); p.E[GrowthPersonPose.Spine] = new Vector3(-4, 0, 0);
            p.Lift = 0.01f * Pulse(t, 4);
        }

        /// <summary>Carrying what a kind always carries while walking or standing: the hand that holds it keeps its arm in.</summary>
        public static void HoldArms(GrowthPersonPose p, string kind, string left, string right)
        {
            if (kind == "collector" && left == "notebook") { p.E[GrowthPersonPose.ShoulderL] = new Vector3(-30, -6, 0); p.E[GrowthPersonPose.ElbowL] = new Vector3(-92, 0, -20); }
            if (kind == "guest" && left == "program") { p.E[GrowthPersonPose.ShoulderL] = new Vector3(-28, -6, 0); p.E[GrowthPersonPose.ElbowL] = new Vector3(-80, 0, -10); }
        }
    }

    /// <summary>A person assembled from the people kit: their joints, the parts on them, their face's mouths and what they hold;
    /// <see cref="Apply"/> turns the joints to a pose.</summary>
    public sealed class GrowthPerson : MonoBehaviour
    {
        public GrowthPersonLook Look { get; private set; }
        public float HeadHeight { get; private set; }
        readonly Transform[] joints = new Transform[GrowthPersonPose.Joints.Length];
        readonly Vector3[] rest = new Vector3[GrowthPersonPose.Joints.Length];
        readonly Dictionary<string, Transform> jointsByName = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> hands = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> mouths = new Dictionary<string, GameObject>();
        readonly Dictionary<string, GameObject> heldItems = new Dictionary<string, GameObject>();
        public readonly GrowthPersonPose Pose = new GrowthPersonPose();
        /// <summary>Validation: how many renderers and triangles the person draws.</summary>
        public int Renderers { get; private set; }
        public int Triangles { get; private set; }
        public Transform Joint(string name) => jointsByName.TryGetValue(name, out var t) ? t : null;
        public string HandShape(string side) => hands.Where(kv => kv.Key.StartsWith(side) && kv.Value.activeSelf).Select(kv => kv.Key.Substring(2)).FirstOrDefault();
        public string MouthShown => mouths.Where(kv => kv.Value.activeSelf).Select(kv => kv.Key).FirstOrDefault();
        public IEnumerable<string> HeldShown => heldItems.Where(kv => kv.Value.activeSelf).Select(kv => kv.Key);

        /// <summary>Builds a person of a look under a parent (feet at its origin, facing its +Z); null when the kit is missing.</summary>
        public static GrowthPerson Build(GrowthPersonLook look, Transform parent)
        {
            var kind = GrowthPeopleKit.Kind(look.Kind);
            if (kind == null) return null;
            var root = new GameObject("Person (" + look.Kind + " " + look.Seed + ")");
            root.transform.SetParent(parent, false);
            var person = root.AddComponent<GrowthPerson>();
            person.Look = look;
            person.Assemble(kind);
            return person;
        }

        void Assemble(GrowthPeopleKit.KindData kind)
        {
            var body = new GameObject("Body").transform; body.SetParent(transform, false);
            body.localScale = Vector3.one * Look.Height;
            HeadHeight = kind.hh * Look.Height;
            var restOf = kind.joints.ToDictionary(j => j.name, j => GrowthPeopleKit.V(j.rest));
            // The joints, chained from the root at the feet; a build wider or narrower moves the shoulders and hips out or in.
            foreach (var j in kind.joints)
            {
                var t = new GameObject(j.name).transform;
                var parentT = string.IsNullOrEmpty(j.parent) ? body : jointsByName[j.parent];
                t.SetParent(parentT, false);
                var local = GrowthPeopleKit.V(j.rest) - (string.IsNullOrEmpty(j.parent) ? Vector3.zero : restOf[j.parent]);
                if (j.name.StartsWith("shoulder.") || j.name.StartsWith("hip.")) local.x *= Look.Build;
                t.localPosition = local;
                jointsByName[j.name] = t;
                int i = Array.IndexOf(GrowthPersonPose.Joints, j.name);
                if (i >= 0) { joints[i] = t; rest[i] = local; }
            }
            string asset = "PK_Body_" + kind.id;
            foreach (var part in kind.parts)
            {
                int cut = part.IndexOf("__", StringComparison.Ordinal);
                string jointName = part.Substring(0, cut), variant = part.Substring(cut + 2);
                bool hand = variant.StartsWith("hand-");
                if (!hand && variant != "base" && variant != Look.Bottom) continue;
                var j = jointsByName[jointName];
                var holder = new GameObject(part).transform; holder.SetParent(j, false);
                holder.localPosition = -restOf[jointName];
                if (jointName == "spine" || jointName == "hips") holder.localScale = new Vector3(Look.Build, 1, Look.Build);
                if (jointName == "spine" || jointName == "hips") holder.localPosition = Vector3.Scale(-restOf[jointName], holder.localScale);
                var go = Place(asset, part, holder);
                if (go && hand) { hands[jointName.Substring(6) + "." + variant.Substring(5)] = go; go.SetActive(variant == "hand-relaxed"); }
            }
            // The head: its kit is one unit tall on its joint; the person's head height scales it, their face's width its X.
            var head = jointsByName["head"];
            var face = new GameObject("Face").transform; face.SetParent(head, false);
            face.localScale = new Vector3(kind.hh * Look.Wide, kind.hh, kind.hh);
            foreach (var p in Look.HeadParts()) Place("PK_Head", "head__" + p, face);
            mouths[Look.Mouth] = Place("PK_Head", "head__mouth-" + Look.Mouth, face);
            foreach (var m in GrowthPersonLook.Expressions)
                if (!mouths.ContainsKey(m)) { var go = Place("PK_Head", "head__mouth-" + m, face); if (go) { go.SetActive(false); mouths[m] = go; } }
            // What the person holds: the kind's mark always, the shop's bag and coins when the moment comes.
            var held = GrowthPeopleKit.Data.held;
            foreach (var side in new[] { "L", "R" })
            {
                var holder = new GameObject("Held " + side).transform; holder.SetParent(jointsByName["wrist." + side], false);
                float s = kind.hh / held.hh;
                holder.localScale = Vector3.one * s;
                holder.localPosition = -GrowthPeopleKit.V(side == "L" ? held.wristL : held.wristR) * s;
                foreach (var item in new[] { "creature_bag", "coin", side == "L" ? Look.HeldLeft : Look.HeldRight }.Where(x => x != null).Distinct())
                {
                    var go = Place("PK_Held", item + "__" + side, holder);
                    if (go) { heldItems[side + "." + item] = go; go.SetActive(item == (side == "L" ? Look.HeldLeft : Look.HeldRight)); }
                }
            }
            if (Look.HeldLeft != null) ShowHand("L", "grip");
            if (Look.HeldRight != null) ShowHand("R", "grip");
            Renderers = GetComponentsInChildren<Renderer>(true).Length;
        }

        /// <summary>Puts a kit part under a holder (as it sits under its asset's root), in the look's materials with the person's
        /// colours; no colliders, so it is never in the way.</summary>
        GameObject Place(string asset, string part, Transform holder)
        {
            var source = GrowthPeopleKit.Part(asset, part);
            if (!source) return null;
            var go = Instantiate(source, holder, false);
            go.name = part;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Resolve(mats[i] ? mats[i].name.Replace(" (Instance)", "") : "");
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                var mf = r.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) Triangles += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            return go;
        }

        /// <summary>A kit slot's material (PK_kind_colour) with this person's colour for a role.</summary>
        public Material Resolve(string slot)
        {
            var parts = slot.Split('_');
            if (parts.Length < 3 || parts[0] != "PK") throw new InvalidOperationException("Unmapped people kit material: " + slot);
            string kind = parts[1], colour = string.Join("_", parts.Skip(2));
            string hex = colour.Length == 6 && colour.All(Uri.IsHexDigit) ? "#" + colour : Look.Colours.TryGetValue(colour, out var c) ? c : Fallback(colour);
            return GrowthPeopleKit.Material(kind, hex);
        }

        /// <summary>A role a look doesn't fill (an outfit fixed for its kind wears these).</summary>
        static string Fallback(string role) => role switch
        {
            "HAT" => "#FF7A7A", "HATBAND" => "#FFF3DF", "GLASSES" => "#2B3A55", "BOTTOM" => "#5C7FD6", "SOCKS" => "#FFFFFF",
            "SHOE" => "#FFFFFF", "SOLE" => "#FF7A7A", "SHOEACC" => "#22BFB0", "BAG" => "#FFF3DF", "PRINT" => "#FF7A7A", _ => "#FF00FF",
        };

        public void ShowHand(string side, string shape)
        {
            if (!hands.ContainsKey(side + "." + shape)) shape = "relaxed";
            foreach (var kv in hands) if (kv.Key.StartsWith(side + ".")) kv.Value.SetActive(kv.Key == side + "." + shape);
        }

        public void ShowMouth(string mouth)
        {
            if (!mouths.ContainsKey(mouth)) mouth = Look.Mouth;
            foreach (var kv in mouths) if (kv.Value) kv.Value.SetActive(kv.Key == mouth);
        }

        public void ShowHeld(string side, string item)
        {
            foreach (var kv in heldItems) if (kv.Key.StartsWith(side + ".")) kv.Value.SetActive(kv.Key == side + "." + item);
            ShowHand(side, item != null ? "grip" : "relaxed");
        }

        /// <summary>The shop's bag in a hand (Transform to put the bought creature in), or null.</summary>
        public Transform BagIn(string side) => heldItems.TryGetValue(side + ".creature_bag", out var go) ? go.transform : null;

        /// <summary>Turns the joints to a pose (the board's Blender angles turned into Unity's).</summary>
        public void Apply(GrowthPersonPose p)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (!j) continue;
                j.localRotation = GrowthPersonPose.ToUnity(p.E[i]);
                if (i == GrowthPersonPose.Hips) j.localPosition = rest[i] + p.HipsOffset;
            }
            Pose.CopyFrom(p);
        }
    }
}
