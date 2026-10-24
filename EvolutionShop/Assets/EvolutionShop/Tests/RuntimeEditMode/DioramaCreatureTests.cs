using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>AD-B creature families: the kit rigs, the species table, the colour variant atlas, tricks and idle habits, family
    /// water and container fit. GR-12: wave 1's species on the same rigs, their family and crown tricks.</summary>
    public sealed class DioramaCreatureTests
    {
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        static string Manifest => File.ReadAllText(Path.Combine(Repo, "ArtSource", "DioramaKit", "manifest.json"));
        static readonly string[] RigParts = { "Body", "Eyes", "Pupils", "Glints", "Lids", "Blush", "Lips", "Tail pivot", "Left fin pivot", "Right fin pivot", "Crest pivot" };

        /// <summary>The classic game's five species (the classic containers show only those).</summary>
        static IEnumerable<DioramaSpecies> Classic()
        {
            var game = GameConfig.CreateDefault().Species.Select(s => s.Id).ToArray();
            return DioramaSpecies.All.Where(s => game.Contains(s.Id));
        }

        [Test] public void EveryGameSpeciesHasAKitSpeciesInManifestOrder()
        {
            var game = GameConfig.CreateDefault().Species.Select(s => s.Id).ToArray();
            Assert.That(DioramaSpecies.All.Select(s => s.Id), Is.SupersetOf(game), "Every species the classic game has is a kit species");
            Assert.That(DioramaSpecies.All.Take(5).Select(s => s.Id), Is.EquivalentTo(game), "AD-B's five first, at their atlas blocks");
            // GR-12 waves 1 to 4: all of GR.7's roster, the nine families' eight each.
            var waves = GrowthContent.Species.ToArray();
            Assert.That(DioramaSpecies.All.Select(s => s.Id), Is.EquivalentTo(waves.Select(s => s.Id)), "Waves 1 to 4: the nine families' eight each");
            Assert.That(DioramaSpecies.All.Count, Is.EqualTo(72));
            Assert.That(DioramaSpecies.All.Count, Is.EqualTo(DioramaVariants.SpeciesCapacity), "The atlas is full");
            foreach (var def in waves)
            {
                Assert.That(DioramaSpecies.TryGet(def.Id, out var kit), Is.True, def.Id);
                Assert.That(kit.Family.ToString().ToLowerInvariant(), Is.EqualTo(def.Family), def.Id + " in its GR family");
                Assert.That(kit.Crown, Is.EqualTo(def.Kind == SpeciesKind.Crown), def.Id + " a crown exactly when GR.7's is");
                Assert.That(kit.Tempo, Is.InRange(.7f, 1.4f), def.Id + " tempo");
            }
            Assert.That(DioramaSpecies.All.Select(s => s.Index), Is.EqualTo(Enumerable.Range(0, DioramaSpecies.All.Count)), "Atlas blocks in order");
            Assert.That(DioramaSpecies.All.Count, Is.LessThanOrEqualTo(DioramaVariants.SpeciesCapacity), "The atlas holds every species");
            string manifest = Manifest;
            int at = 0;
            foreach (var s in DioramaSpecies.All)
            {
                int found = manifest.IndexOf("\"id\": \"" + s.Id + "\"", at, StringComparison.Ordinal);
                Assert.That(found, Is.GreaterThan(at), "Manifest species order / " + s.Id);
                Assert.That(manifest.Substring(found, 200), Does.Contain("\"family\": \"" + s.Family + "\""), s.Id);
                at = found;
                Assert.That(s.TankScale, Is.InRange(1f, 1.7f)); Assert.That(s.IncubatorScale, Is.InRange(1f, 1.6f));
                Assert.That(s.TrickSeconds, Is.InRange(.5f, 2f));
            }
            // Signatures: AD-B's five keep their own; a family's other species play its family's, each crown its own.
            var family = new Dictionary<DioramaFamily, string> { { DioramaFamily.Brookkin, "splash" }, { DioramaFamily.Sparkline, "twinkle" }, { DioramaFamily.Airfin, "glide" },
                { DioramaFamily.Cinderling, "flare" }, { DioramaFamily.Dewling, "sprinkle" }, { DioramaFamily.Reefback, "bloom" }, { DioramaFamily.Saltscale, "dart" },
                { DioramaFamily.Gloamer, "lure" }, { DioramaFamily.Cometail, "streak" } };
            Assert.That(DioramaSpecies.All.Take(5).Select(s => s.Signature), Is.EqualTo(new[] { "bubble", "balloon", "twirl", "flash", "loop" }));
            foreach (var s in DioramaSpecies.All.Skip(5))
                if (s.Crown) Assert.That(family.Values.Contains(s.Signature) || DioramaSpecies.All.Count(o => o.Signature == s.Signature) > 1, Is.False, s.Id + " a trick of its own");
                else Assert.That(s.Signature, Is.EqualTo(family[s.Family]), s.Id + " plays its family's trick");
            foreach (var group in DioramaSpecies.All.Skip(5).Where(s => !s.Crown).GroupBy(s => s.Family))
                Assert.That(group.Select(s => s.TrickSeconds).Distinct().Count(), Is.GreaterThan(group.Count() / 2), group.Key + "'s species play at tempos of their own");
            Assert.That(DioramaSpecies.TryGet("pondlet", out var pondlet) && pondlet.TankScale == CreatureVisual.DioramaTankScale &&
                pondlet.IncubatorScale == CreatureVisual.DioramaIncubatorScale && pondlet.SwimAmplitude == CreatureVisual.DioramaSwimAmplitude,
                Is.True, "The Pondlet keeps AD-A's sizes");
        }

        [Test] public void EveryKitSpeciesSharesTheFamilyRig()
        {
            foreach (var s in DioramaSpecies.All)
            {
                var kit = Resources.Load<GameObject>(DioramaLook.KitPath + s.Kit);
                Assert.That(kit, Is.Not.Null, s.Kit);
                var root = kit.transform;
                foreach (var part in RigParts) Assert.That(root.Find(part), Is.Not.Null, s.Id + " / " + part);
                Assert.That(root.Find("Lantern pivot") != null, Is.EqualTo(s.Family == DioramaFamily.Sparkline || s.Family == DioramaFamily.Gloamer), "The Sparkline's lantern and the Gloamer's lure / " + s.Id);
                foreach (Transform part in root)
                {
                    Assert.That(part.childCount, Is.EqualTo(0), "Flat kit / " + s.Id + "/" + part.name);
                    Assert.That(Quaternion.Angle(part.localRotation, Quaternion.identity), Is.LessThan(.01f), s.Id + "/" + part.name);
                    Assert.That(part.name, Does.Not.Contain("."), "No clashing Blender names / " + s.Id);
                }
                Assert.That(Shapes(root, "Body"), Does.Contain("Puffed"), s.Id);
                Assert.That(Shapes(root, "Lids"), Is.SupersetOf(new[] { "Blink", "Happy" }), s.Id);
                Assert.That(Shapes(root, "Lips"), Is.SupersetOf(new[] { "Happy", "Surprised" }), s.Id);
                // Front -Z: the lips are ahead of the body's middle and the tail behind it (GR-12B: behind its body's middle; a
                // squid's cloak, on its Tail pivot, under it); left is -X.
                Assert.That(Centre(root.Find("Lips")).z, Is.LessThan(Centre(root.Find("Body")).z - (s.Remade ? .05f : .2f)), "Lips at the front / " + s.Id);
                if (s.Gait == DioramaGait.Pulse)
                    Assert.That(root.Find("Tail pivot").localPosition.y, Is.LessThan(Centre(root.Find("Body")).y - .1f), "A cloak under its body / " + s.Id);
                else
                    Assert.That(root.Find("Tail pivot").localPosition.z, Is.GreaterThan(s.Remade ? Centre(root.Find("Body")).z + .1f : .25f), "Tail behind / " + s.Id);
                // GR-12B: what its way of swimming moves: a sunfish's keel, an undulating body's Wave keys.
                Assert.That(root.Find("Keel pivot") != null, Is.EqualTo(s.Gait == DioramaGait.Scull), "A keel to scull with / " + s.Id);
                Assert.That(Shapes(root, "Body").Contains("WaveSin"), Is.EqualTo(s.Gait == DioramaGait.Undulate), "Wave keys to undulate with / " + s.Id);
                if (s.Gait == DioramaGait.Undulate) Assert.That(Shapes(root, "Body"), Is.SupersetOf(new[] { "WaveSin", "WaveSinNeg", "WaveCos", "WaveCosNeg" }), s.Id);
                Assert.That(root.Find("Left fin pivot").localPosition.x, Is.LessThan(0), s.Id);
                Assert.That(root.Find("Right fin pivot").localPosition.x, Is.GreaterThan(0), s.Id);
            }
        }

        [Test] public void RemadeSpeciesSwimByTheirBodyPlans()
        {
            // GR-12B: a ray's wings beat up and down together; a sunfish's tall top and bottom fins sweep to one side together; a
            // wave runs down an oarfish's body from head to tail, its tail fin riding it; a squid's cloak opens and closes. None of
            // it with Reduced motion.
            Assert.That(DioramaSpecies.All.Where(s => s.Family == DioramaFamily.Gloamer).All(s => s.Remade), Is.True, "GR-12B wave 1: the Gloamer");
            Assert.That(DioramaSpecies.All.Where(s => s.Family == DioramaFamily.Cometail || s.Family == DioramaFamily.Reefback).All(s => s.Remade), Is.True,
                "GR-12B wave 2: the Cometail and the Reefback");
            Assert.That(DioramaSpecies.All.Where(s => s.Family == DioramaFamily.Saltscale || s.Family == DioramaFamily.Cinderling).All(s => s.Remade), Is.True,
                "GR-12B wave 3: the Saltscale and the Cinderling");
            Assert.That(DioramaSpecies.All.Where(s => s.Family == DioramaFamily.Dewling || s.Family == DioramaFamily.Sparkline && s.Id != "lanternfin").All(s => s.Remade),
                Is.True, "GR-12B wave 4: the Dewling and the Sparkline (the Lanternfin, AD-B's, waits for wave 5)");
            Assert.That(DioramaSpecies.All.Where(s => s.Remade).Select(s => s.Gait).Distinct(),
                Is.SupersetOf(new[] { DioramaGait.Flap, DioramaGait.Scull, DioramaGait.Undulate, DioramaGait.Pulse, DioramaGait.Fluke, DioramaGait.Paddle, DioramaGait.Upright }));
            var parent = new GameObject("GR-12B gaits").transform;
            try
            {
                CreatureVisual Kit(string id)
                {
                    var go = new GameObject(id); go.transform.SetParent(parent, false);
                    var v = go.AddComponent<CreatureVisual>();
                    Assert.That(v.BuildKit(id), Is.True, id);
                    return v;
                }
                Transform Part(CreatureVisual v, string name) => v.GetComponentsInChildren<Transform>(true).First(x => x.name == name);
                float Roll(Transform t) => Mathf.DeltaAngle(0, t.localEulerAngles.z);

                // A ray: both wings rise and fall together, as mirror images, far.
                var ray = Kit("hushray");
                Transform left = Part(ray, "Left fin pivot"), right = Part(ray, "Right fin pivot");
                float widest = 0;
                for (float t = 0; t < 8; t += .05f)
                {
                    ray.AnimateSpecies(t, 1, false);
                    Assert.That(Roll(left), Is.EqualTo(-Roll(right)).Within(.5f), "the ray's wings beat together at " + t);
                    widest = Mathf.Max(widest, Mathf.Abs(Roll(right)));
                }
                Assert.That(widest, Is.GreaterThan(18), "the ray's wings beat deep");

                // A sunfish: its top and bottom fins sweep to the same side together.
                var sunfish = Kit("gloomfin");
                Transform crest = Part(sunfish, "Crest pivot"), keel = Part(sunfish, "Keel pivot");
                Vector3 topRest = crest.TransformPoint(Vector3.up * .3f), bottomRest = keel.TransformPoint(Vector3.down * .3f);
                widest = 0;
                int together = 0;
                for (float t = 0; t < 8; t += .05f)
                {
                    sunfish.AnimateSpecies(t, 1, false);
                    float top = crest.TransformPoint(Vector3.up * .3f).x - topRest.x, bottom = keel.TransformPoint(Vector3.down * .3f).x - bottomRest.x;
                    widest = Mathf.Max(widest, Mathf.Abs(Roll(crest)));
                    if (Mathf.Abs(Roll(crest)) > 5) { Assert.That(Mathf.Sign(top), Is.EqualTo(Mathf.Sign(bottom)), "its fins sweep to one side at " + t); together++; }
                }
                Assert.That(widest, Is.GreaterThan(18), "the sunfish's fins sweep far");
                Assert.That(together, Is.GreaterThan(40));

                // An oarfish: a wave runs from head to tail (the tail end swings later than the middle), its tail fin riding it.
                var oarfish = Kit("duskdrifter");
                var body = oarfish.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Body");
                var rest = body.sharedMesh.vertices;
                int Nearest(float z) { int best = 0; for (int i = 1; i < rest.Length; i++) if (Mathf.Abs(rest[i].z - z) + Mathf.Abs(rest[i].x) * .1f < Mathf.Abs(rest[best].z - z) + Mathf.Abs(rest[best].x) * .1f) best = i; return best; }
                float zMin = rest.Min(v => v.z), zMax = rest.Max(v => v.z);
                int mid = Nearest(Mathf.Lerp(zMin, zMax, .65f)), end = Nearest(Mathf.Lerp(zMin, zMax, .9f));
                var tail = Part(oarfish, "Tail pivot");
                oarfish.AnimateSpecies(0, 1, true);  // at rest (building it swam it one frame)
                var tailRest = tail.localPosition;
                var baked = new Mesh();
                float period = 2 * Mathf.PI / (2.6f * DioramaSpecies.All.First(s => s.Id == "duskdrifter").Tempo);
                float midPeak = -1, endPeak = -1, midMost = 0, endMost = 0, tailMost = 0;
                for (float t = 0; t < period; t += period / 200)
                {
                    oarfish.AnimateSpecies(t, 1, false);
                    body.BakeMesh(baked, false);
                    var v = baked.vertices;
                    float m = v[mid].x - rest[mid].x, e = v[end].x - rest[end].x;
                    if (m > midMost) { midMost = m; midPeak = t; }
                    if (e > endMost) { endMost = e; endPeak = t; }
                    tailMost = Mathf.Max(tailMost, (tail.localPosition - tailRest).magnitude);
                }
                Assert.That(endMost, Is.GreaterThan(.02f), "the oarfish's tail end swings");
                Assert.That(Mathf.Repeat(endPeak - midPeak, period), Is.InRange(.03f * period, .5f * period), "the wave runs from its middle to its tail");
                Assert.That(tailMost, Is.GreaterThan(.02f), "its tail fin rides the wave");
                UnityEngine.Object.DestroyImmediate(baked);

                // A squid: its cloak opens and closes.
                var squid = Kit("nightlantern");
                var cloak = Part(squid, "Tail pivot");
                squid.AnimateSpecies(0, 1, true);
                var cloakRest = cloak.localScale;
                float open = 0, shut = 0;
                for (float t = 0; t < 8; t += .05f)
                {
                    squid.AnimateSpecies(t, 1, false);
                    open = Mathf.Max(open, cloak.localScale.x / cloakRest.x - 1); shut = Mathf.Max(shut, 1 - cloak.localScale.x / cloakRest.x);
                }
                Assert.That(open, Is.GreaterThan(.08f), "the squid's cloak opens"); Assert.That(shut, Is.GreaterThan(.08f), "and closes");

                // A narwhal: its fluke beats up and down, far, and its tail swings little from side to side.
                var narwhal = Kit("novatusk");
                var fluke = Part(narwhal, "Tail pivot");
                float up = 0, down = 0, aside = 0;
                for (float t = 0; t < 8; t += .05f)
                {
                    narwhal.AnimateSpecies(t, 1, false);
                    float beat = Mathf.DeltaAngle(0, fluke.localEulerAngles.x);
                    up = Mathf.Max(up, beat); down = Mathf.Min(down, beat);
                    aside = Mathf.Max(aside, Mathf.Abs(Mathf.DeltaAngle(0, fluke.localEulerAngles.y)));
                }
                Assert.That(up, Is.GreaterThan(12), "the narwhal's fluke beats up"); Assert.That(down, Is.LessThan(-12), "and down");
                Assert.That(aside, Is.LessThan(up / 2), "more than it swings aside");

                // A snapping turtle: its legs stroke in turn (each mirrors the other's roll, so one rises as the other falls), far.
                var turtle = Kit("sootsnap");
                Transform leftLeg = Part(turtle, "Left fin pivot"), rightLeg = Part(turtle, "Right fin pivot");
                widest = 0;
                for (float t = 0; t < 8; t += .05f)
                {
                    turtle.AnimateSpecies(t, 1, false);
                    Assert.That(Roll(leftLeg), Is.EqualTo(Roll(rightLeg)).Within(.5f), "the turtle's legs stroke in turn at " + t);
                    widest = Mathf.Max(widest, Mathf.Abs(Roll(rightLeg)));
                }
                Assert.That(widest, Is.GreaterThan(14), "the turtle's legs stroke far");

                // A seahorse: its back fin flutters, fast (a swing every half second or so) and far.
                var seahorse = Kit("tidepip");
                var backFin = Part(seahorse, "Crest pivot");
                int flips = 0; float last = 0, flutter = 0;
                for (float t = 0; t < 4; t += .02f)
                {
                    seahorse.AnimateSpecies(t, 1, false);
                    float r = Roll(backFin);
                    if (Mathf.Abs(r) > 4 && Mathf.Sign(r) != Mathf.Sign(last) && last != 0) flips++;
                    if (Mathf.Abs(r) > 4) last = r;
                    flutter = Mathf.Max(flutter, Mathf.Abs(r));
                }
                Assert.That(flutter, Is.GreaterThan(10), "the seahorse's back fin flutters far");
                Assert.That(flips, Is.GreaterThan(6), "and fast");

                // A swimming scallop: its upper shell, on its tail's pivot at its hinge, claps open and shut by the fluke's beat.
                var scallop = Kit("pearlpip");
                var valve = Part(scallop, "Tail pivot");
                float clapWide = 0, clapShut = 0;
                for (float t = 0; t < 8; t += .05f)
                {
                    scallop.AnimateSpecies(t, 1, false);
                    float clap = Mathf.DeltaAngle(0, valve.localEulerAngles.x);
                    clapWide = Mathf.Max(clapWide, clap); clapShut = Mathf.Min(clapShut, clap);
                }
                Assert.That(clapWide, Is.GreaterThan(12), "the scallop's shell claps one way"); Assert.That(clapShut, Is.LessThan(-12), "and the other");

                // None of it with Reduced motion.
                for (float t = 0; t < 4; t += .25f)
                {
                    foreach (var v in new[] { ray, sunfish, oarfish, squid, narwhal, turtle, seahorse, scallop }) v.AnimateSpecies(t, 1, true);
                    Assert.That(Mathf.DeltaAngle(0, valve.localEulerAngles.x), Is.EqualTo(0).Within(.01f));
                    Assert.That(Roll(rightLeg), Is.EqualTo(0).Within(.01f)); Assert.That(Roll(backFin), Is.EqualTo(0).Within(.01f));
                    Assert.That(Mathf.DeltaAngle(0, fluke.localEulerAngles.x), Is.EqualTo(0).Within(.01f));
                    Assert.That(Roll(right), Is.EqualTo(0).Within(.01f)); Assert.That(Roll(crest), Is.EqualTo(0).Within(.01f)); Assert.That(Roll(keel), Is.EqualTo(0).Within(.01f));
                    Assert.That((tail.localPosition - tailRest).magnitude, Is.LessThan(1e-5f)); Assert.That(cloak.localScale.x, Is.EqualTo(cloakRest.x).Within(1e-5f));
                    for (int k = 0; k < body.sharedMesh.blendShapeCount; k++)
                        if (body.sharedMesh.GetBlendShapeName(k).StartsWith("Wave")) Assert.That(body.GetBlendShapeWeight(k), Is.EqualTo(0).Within(1e-4f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(parent.gameObject); }
        }

        static string[] Shapes(Transform root, string part)
        {
            var mesh = root.Find(part).GetComponent<SkinnedMeshRenderer>().sharedMesh;
            return Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
        }
        static Vector3 Centre(Transform part)
        {
            var filter = part.GetComponent<MeshFilter>();
            var skinned = part.GetComponent<SkinnedMeshRenderer>();
            return part.localPosition + (filter ? filter.sharedMesh : skinned.sharedMesh).bounds.center;
        }

        [Test] public void TheVariantAtlasHoldsTheManifestsColours()
        {
            DioramaVariants.Reset();
            Assert.That(DioramaVariants.Ready, Is.True, "The atlas loads, readable, at 6 x 288 (GR.7's 72 species)");
            var atlas = Resources.Load<Texture2D>(DioramaLook.KitPath + DioramaVariants.AtlasAsset);
            Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(atlas.mipmapCount, Is.EqualTo(1));
            string manifest = Manifest;
            foreach (var s in DioramaSpecies.All)
            {
                int start = manifest.IndexOf("\"id\": \"" + s.Id + "\"", StringComparison.Ordinal);
                int end = manifest.IndexOf("\"id\": \"", start + 5, StringComparison.Ordinal);
                string block = manifest.Substring(start, (end < 0 ? manifest.Length : end) - start);
                foreach (DioramaVariant v in Enum.GetValues(typeof(DioramaVariant)))
                {
                    var match = Regex.Match(block, "\"" + v + "\": \\{([^}]*)\\}");
                    Assert.That(match.Success, Is.True, s.Id + " " + v);
                    for (int slot = 0; slot < DioramaVariants.Slots.Length; slot++)
                    {
                        var hex = Regex.Match(match.Groups[1].Value, "\"" + DioramaVariants.Slots[slot] + "\": \"([0-9A-F]{6,8})\"");
                        Assert.That(hex.Success, Is.True, s.Id + " " + v + " " + DioramaVariants.Slots[slot]);
                        var code = hex.Groups[1].Value;
                        Color32 shown = DioramaVariants.Color(s.Index, v, slot);
                        Assert.That(shown.r, Is.EqualTo(Convert.ToByte(code.Substring(0, 2), 16)), s.Id + " " + v + " " + slot + " r");
                        Assert.That(shown.g, Is.EqualTo(Convert.ToByte(code.Substring(2, 2), 16)), s.Id + " " + v + " " + slot + " g");
                        Assert.That(shown.b, Is.EqualTo(Convert.ToByte(code.Substring(4, 2), 16)), s.Id + " " + v + " " + slot + " b");
                        if (slot == DioramaVariants.Sheen)
                            Assert.That(shown.a > 0, Is.EqualTo(v == DioramaVariant.Shimmer), "Only Shimmer has a sheen / " + s.Id + " " + v);
                    }
                }
                // Variants are visibly different colours of the same creature.
                var classic = DioramaVariants.Color(s.Index, DioramaVariant.Classic, 0);
                foreach (var v in new[] { DioramaVariant.Sunny, DioramaVariant.Deep, DioramaVariant.Shimmer })
                    Assert.That(Distance(classic, DioramaVariants.Color(s.Index, v, 0)), Is.GreaterThan(.12f), s.Id + " " + v + " differs from Classic");
            }
            // GR-12 wave 4 fills the atlas: its last row is the Celestial Wyrm's Shimmer.
            Assert.That(DioramaVariants.Color(DioramaVariants.SpeciesCapacity - 1, DioramaVariant.Shimmer, 0).a, Is.EqualTo(1));
        }

        static float Distance(Color a, Color b) => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        [Test] public void EveryKitSlotResolvesAndTheLanternGlows()
        {
            foreach (var s in DioramaSpecies.All)
            {
                var root = new GameObject("kit test").transform;
                var owned = new List<Material>();
                try
                {
                    var model = DioramaLook.InstantiateKit(s.Kit, root, DioramaLook.Style.Creature, owned);
                    Assert.That(model, Is.Not.Null, s.Kit);
                    var body = owned.First(m => m.name == "Diorama / " + DioramaVariants.SlotName(s.Id, 0));
                    var classic = DioramaVariants.Color(s.Index, DioramaVariant.Classic, 0); classic.a = 1;
                    Assert.That(Distance(body.GetColor("_BaseColor"), classic), Is.LessThan(.005f), "Body from the atlas's Classic row / " + s.Id);
                    var lantern = owned.FirstOrDefault(m => DioramaLook.GlowSlot(m.name.Replace("Diorama / ", "")));
                    Assert.That(lantern != null, Is.EqualTo(s.Family == DioramaFamily.Sparkline || s.Family == DioramaFamily.Cinderling || s.Family == DioramaFamily.Saltscale ||
                        s.Family == DioramaFamily.Gloamer || s.Family == DioramaFamily.Cometail), s.Id);
                    if (lantern) Assert.That(lantern.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(.3f), "The lantern (a Cinderling's embers, a Saltscale's stripe, a Gloamer's lure, a Cometail's head and tail) glows");
                }
                finally
                {
                    foreach (var m in owned) UnityEngine.Object.DestroyImmediate(m);
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                }
            }
        }

        [Test] public void AVariantRecoloursTheCreatureFromTheAtlas()
        {
            bool was = DioramaLook.Active;
            DioramaLook.Active = true;
            try
            {
                foreach (var s in DioramaSpecies.All)
                {
                    var go = new GameObject("variant test");
                    try
                    {
                        var creature = go.AddComponent<CreatureVisual>();
                        creature.Build(s.Id);
                        Assert.That(creature.IsDiorama && creature.DioramaSpecies.Id == s.Id, Is.True, s.Id);
                        foreach (DioramaVariant v in Enum.GetValues(typeof(DioramaVariant)))
                        {
                            creature.SetDioramaVariant(v);
                            for (int slot = 0; slot < DioramaVariants.Sheen; slot++)
                            {
                                var material = creature.DioramaSlotForValidation(slot);
                                Assert.That(material, Is.Not.Null, s.Id + " slot " + slot);
                                var expected = DioramaVariants.Color(s.Index, v, slot); expected.a = 1;
                                Assert.That(Distance(material.GetColor("_BaseColor"), expected), Is.LessThan(.005f), s.Id + " " + v + " " + slot);
                                Assert.That(material.GetFloat("_SheenStrength") > 0, Is.EqualTo(v == DioramaVariant.Shimmer), s.Id + " " + v + " sheen");
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            }
            finally { DioramaLook.Active = was; }
        }

        [Test] public void EachTrickPlaysOutAndEndsAtRest()
        {
            foreach (var s in DioramaSpecies.All)
            {
                if (s.Id == "pondlet") continue; // AD-A's bubble rides the existing PondletGlance cue.
                float d = DioramaTricks.Duration(s.Id);
                int releases = 0; float peak = 0;
                for (float age = 0, before = -1; age < d; before = age, age += 1 / 60f)
                {
                    var p = DioramaTricks.Trick(s.Id, age, before);
                    foreach (var f in new[] { p.Pitch, p.Yaw, p.Roll, p.Lift, p.Puffed, p.Happy, p.Surprised, p.Glow, p.Tail, p.Fins, p.Flare })
                        Assert.That(float.IsNaN(f) || float.IsInfinity(f), Is.False, s.Id);
                    Assert.That(p.Lift, Is.InRange(-.01f, .08f), s.Id + " stays low in the water");
                    peak = Mathf.Max(peak, Mathf.Abs(p.Roll) + Mathf.Abs(p.Pitch) + Mathf.Abs(p.Yaw) + p.Puffed * 100 + p.Glow * 100);
                    if (p.Release) releases++;
                }
                Assert.That(peak, Is.GreaterThan(90), s.Id + " does something you can see");
                // The flash and the aurora sparkle twice, the gale gusts on both wing beats, the rebirth sparks going over and coming
                // round, the eclipse's corona blazing twice, the fountain bubbles three times, the rainbow sprays three times and a
                // streak and the starfall leave stardust three times.
                int beats = s.Signature == "flash" || s.Signature == "aurora" || s.Signature == "gale" || s.Signature == "rebirth" || s.Signature == "pearl" || s.Signature == "eclipse" ? 2 :
                    s.Signature == "fountain" || s.Signature == "rainbow" || s.Signature == "reef" || s.Signature == "streak" || s.Signature == "starfall" ? 3 : 1;
                Assert.That(releases, Is.EqualTo(beats), s.Id + " releases its bubbles or sparkles on its beats");
                Assert.That(DioramaTricks.Releases(s.Id), Is.EqualTo(beats), s.Id + " says how many");
                var after = DioramaTricks.Trick(s.Id, d, d - .01f);
                Assert.That(after.Roll == 0 && after.Pitch == 0 && after.Yaw == 0 && after.Puffed == 0 && after.Glow == 0 && after.Flare == 0 && !after.Release, Is.True, s.Id + " ends at rest");
                Assert.That(DioramaTricks.Trick(s.Id, d - 1e-3f).Flare, Is.LessThan(.01f), s.Id + " its spines settle back");
                Assert.That(DioramaTricks.Trick(s.Id, -1).Equals(default(DioramaTrickPose)), Is.True);
                Assert.That(DioramaTricks.Trick(s.Id, float.NaN).Equals(default(DioramaTrickPose)), Is.True);
                // Full turns end where they began, so the swim pose takes over without a jump.
                var end = DioramaTricks.Trick(s.Id, d - 1e-4f);
                Assert.That(Quaternion.Angle(Quaternion.Euler(end.Pitch, end.Yaw, end.Roll), Quaternion.identity), Is.LessThan(2f), s.Id + " ends upright");
            }
            // The Pondlet's own (AD-A's glance in the classic shop) stays out of DioramaTricks.Trick; the greybox plays it as a pose.
            Assert.That(DioramaTricks.Trick("pondlet", .3f, .29f).Equals(default(DioramaTrickPose)), Is.True, "The classic Pondlet's bubble is PondletGlance's");
            int bubbles = 0;
            for (float age = 0, before = -1; age < DioramaTricks.Duration("pondlet"); before = age, age += 1 / 60f)
                if (DioramaTricks.PondletBubble(age, before).Release) bubbles++;
            Assert.That(bubbles, Is.EqualTo(1), "The greybox Pondlet blows one bubble");
            Assert.That(DioramaTricks.PondletBubble(DioramaTricks.Duration("pondlet")).Equals(default(DioramaTrickPose)), Is.True, "and ends at rest");
        }

        [Test] public void FamilyTricksPlayAtEachSpeciesTempoAndHabitsFollowIt()
        {
            // Two of a family play the same moves, stretched to their own lengths: the same pose at the same share of the trick.
            foreach (var pair in new[] { ("puddlepup", "reedwhisker"), ("sparkminnow", "glowbelly"), ("hazewing", "cloudcarp"), ("cindertail", "coalbelly"), ("rainskipper", "haloling"), ("coralhorn", "clamjaw"), ("seaspark", "pebblejaw"), ("gloomfin", "moonmouth"), ("starmaw", "meteorback") })
            {
                float a = DioramaTricks.Duration(pair.Item1), b = DioramaTricks.Duration(pair.Item2);
                Assert.That(a, Is.Not.EqualTo(b), pair + " differ in tempo");
                for (float u = .05f; u < 1; u += .1f)
                {
                    var p = DioramaTricks.Trick(pair.Item1, u * a); var q = DioramaTricks.Trick(pair.Item2, u * b);
                    Assert.That(p.Roll, Is.EqualTo(q.Roll).Within(.01f)); Assert.That(p.Yaw, Is.EqualTo(q.Yaw).Within(.01f));
                    Assert.That(p.Glow, Is.EqualTo(q.Glow).Within(.001f)); Assert.That(p.Flare, Is.EqualTo(q.Flare).Within(.001f));
                }
            }
            // GR-12 wave 4: a lure dims first, then pulses brighter; an eclipse goes dark, then its corona blazes; the starfall
            // spirals round twice.
            float Glows(string id, bool lowest)
            {
                float most = 0, d = DioramaTricks.Duration(id);
                for (float a = 0; a < d; a += 1 / 60f) most = lowest ? Mathf.Min(most, DioramaTricks.Trick(id, a).Glow) : Mathf.Max(most, DioramaTricks.Trick(id, a).Glow);
                return most;
            }
            Assert.That(Glows("moonmouth", true), Is.LessThan(-.5f), "A Gloamer's lights dim first");
            Assert.That(Glows("moonmouth", false), Is.GreaterThan(2f), "then its lure pulses brighter");
            Assert.That(Glows("eclipseleviathan", true), Is.LessThan(-.8f), "An eclipse goes dark");
            Assert.That(Glows("eclipseleviathan", false), Is.GreaterThan(2.5f), "then its corona blazes");
            Assert.That(DioramaTricks.Trick("celestialwyrm", DioramaTricks.Duration("celestialwyrm") * .5f).Yaw, Is.GreaterThan(360), "The starfall spirals round twice");
            // A quicker species runs its family's habit faster: the Splashkit's wag comes round sooner than the Reedwhisker's.
            float First(string species)
            {
                for (float t = 0; t < 20; t += 1 / 60f) if (Mathf.Abs(DioramaTricks.Idle(species, t, 2f).Tail) > 3) return t;
                return float.PositiveInfinity;
            }
            Assert.That(First("splashkit"), Is.LessThan(First("reedwhisker")), "Tempo sets the habit's pace");
            Assert.That(DioramaTricks.Idle("glowbelly", 1, 0).Glow, Is.Not.EqualTo(0), "A Sparkline's lantern breathes");
            Assert.That(DioramaTricks.Idle("mistrunner", 1, 0).Fins, Is.GreaterThan(1), "An Airfin holds its sails out");
            // GR-12 wave 2: a Cinderling smoulders, its spines bristling now and then; a Dewling bobs and gathers a drop.
            float Peak(string species, System.Func<DioramaTrickPose, float> of)
            {
                float most = 0;
                for (float t = 0; t < 14; t += 1 / 30f) most = Mathf.Max(most, of(DioramaTricks.Idle(species, t, .2f)));
                return most;
            }
            Assert.That(Peak("coalbelly", p => p.Glow), Is.GreaterThan(1), "A Cinderling's embers pulse brighter");
            Assert.That(Peak("coalbelly", p => p.Flare), Is.InRange(.3f, .5f), "and its spines bristle, gently");
            Assert.That(Peak("pearlpip", p => p.Lift), Is.GreaterThan(.004f), "A Dewling bobs");
            Assert.That(Peak("pearlpip", p => p.Puffed), Is.GreaterThan(.2f), "and gathers itself round");
            // GR-12 wave 3: a Reefback's corals breathe and it nods as it grazes; a Saltscale's stripe glitters soft, flickering bright.
            Assert.That(Peak("kelpgrazer", p => p.Flare), Is.InRange(.15f, .3f), "A Reefback's corals breathe open and closed");
            Assert.That(Peak("kelpgrazer", p => -p.Pitch), Is.GreaterThan(4), "and it nods as it grazes");
            Assert.That(Peak("brinefin", p => p.Glow), Is.GreaterThan(.3f), "A Saltscale's stripe flickers bright now and then");
            Assert.That(Peak("brinefin", p => -p.Glow), Is.GreaterThan(.3f), "and glitters softly at rest");
            // GR-12 wave 4: a Gloamer's lights breathe deep and dim, its lure winking brighter now and then, as it hangs nose a little
            // down; a Cometail's head and tail glow bright, and its tail streams out with a flick.
            Assert.That(Peak("hushray", p => p.Glow), Is.GreaterThan(.8f), "A Gloamer's lure winks brighter now and then");
            Assert.That(Peak("hushray", p => -p.Glow), Is.GreaterThan(.3f), "and its lights breathe deep and dim");
            Assert.That(Peak("hushray", p => -p.Pitch), Is.GreaterThan(2.5f), "as it hangs, nose a little down");
            Assert.That(Peak("cosmocarp", p => p.Glow), Is.GreaterThan(.6f), "A Cometail's head and tail glow bright");
            Assert.That(Peak("cosmocarp", p => p.Tail), Is.GreaterThan(8), "and its tail streams out with a flick");
            Assert.That(Peak("puddlepup", p => p.Flare) + Peak("pearlpip", p => p.Flare) + Peak("zephyray", p => p.Flare) + Peak("brinefin", p => p.Flare) +
                Peak("hushray", p => p.Flare) + Peak("cosmocarp", p => p.Flare), Is.EqualTo(0),
                "Only the Cinderling's spines and the Reefback's corals stand taller");
            Assert.That(DioramaTricks.MaxFlare("emberpuff"), Is.EqualTo(1).Within(.02f), "A flare stands the spines at their fullest");
            Assert.That(DioramaTricks.MaxFlare("shellback"), Is.EqualTo(1).Within(.02f), "and a bloom the corals");
            Assert.That(DioramaTricks.MaxFlare("brinefin") + DioramaTricks.MaxFlare("puddlepup"), Is.EqualTo(0), "Other crests never stand taller");
            // AD-B's five keep their own habits exactly (their switch, untouched by the tempo).
            Assert.That(DioramaTricks.Idle("ribbonfin", 3.3f, .37f).Equals(DioramaTricks.Idle("ribbonfin", 3.3f, .37f)), Is.True);
            Assert.That(DioramaTricks.Idle("pondlet", 3.3f, .37f).Equals(default(DioramaTrickPose)), Is.True, "The Pondlet's habit is AD-A's");
        }

        [Test] public void ATrickDrawsTheCreatureToMidWaterAndBackSmoothly()
        {
            foreach (var s in DioramaSpecies.All)
            {
                float d = DioramaTricks.Duration(s.Id);
                Assert.That(DioramaTricks.Settle(s.Id, -1), Is.EqualTo(0), s.Id + " not tricking");
                Assert.That(DioramaTricks.Settle(s.Id, 0), Is.EqualTo(0), s.Id + " starts where it swam");
                Assert.That(DioramaTricks.Settle(s.Id, d), Is.EqualTo(0), s.Id + " ends where it swims");
                Assert.That(DioramaTricks.Settle(s.Id, d * .5f), Is.EqualTo(1).Within(.001f), s.Id + " mid-water for the trick");
                float previous = 0;
                for (int i = 1; i <= 200; i++)
                {
                    float now = DioramaTricks.Settle(s.Id, d * i / 200f);
                    Assert.That(Mathf.Abs(now - previous), Is.LessThan(.12f), s.Id + " eases, never jumps, at " + i);
                    previous = now;
                }
            }
        }

        [Test] public void IdleHabitsAreGentleAndTheHiccupBubblesOncePerCycle()
        {
            foreach (var s in DioramaSpecies.All)
            {
                int releases = 0;
                for (float t = 0, before = -1; t < 14; before = t, t += 1 / 60f)
                {
                    var p = DioramaTricks.Idle(s.Id, t, .37f, before);
                    Assert.That(Mathf.Abs(p.Roll) <= 12 && Mathf.Abs(p.Pitch) <= 12 && p.Lift <= .03f && p.Puffed <= .5f && Mathf.Abs(p.Tail) <= 15, Is.True,
                        s.Id + " habit at " + t);
                    if (p.Release) releases++;
                }
                Assert.That(releases, Is.EqualTo(s.Id == "bubblebelly" ? 2 : 0), s.Id + " habit bubbles");
            }
            Assert.That(DioramaTricks.Idle("lanternfin", 0, 0).Glow, Is.InRange(-.8f, 1.01f));
        }

        [Test] public void FamilyWaterTintsAreDistinctAndNeverMatchTheirSpecies()
        {
            var tints = ((DioramaFamily[])Enum.GetValues(typeof(DioramaFamily))).Select(DioramaWater.Tint).ToArray();
            Assert.That(tints.Length, Is.EqualTo(9));
            for (int i = 0; i < tints.Length; i++)
            {
                Assert.That(tints[i].a, Is.InRange(.05f, .2f));
                for (int j = i + 1; j < tints.Length; j++) Assert.That(Distance(tints[i], tints[j]), Is.GreaterThan(.2f));
            }
            foreach (var s in DioramaSpecies.All)
                Assert.That(Distance(DioramaVariants.Color(s.Index, DioramaVariant.Classic, 0), DioramaWater.Tint(s.Family)), Is.GreaterThan(.3f),
                    s.Id + " stands out from its water (AD.4)");
        }

        [Test] public void TheWaterCarriesItsFamilyTintOnTheFarSide()
        {
            // At the tint's own opacity (10-13%) the family water never read on screen; the far side carries it at a visible
            // strength that still leaves the creature and the back wall clear. The AD-B route reads the result off the screen.
            var glass = Resources.Load<Shader>("EvolutionDioramaGlass");
            Assert.That(glass, Is.Not.Null);
            var material = new Material(glass);
            try { Assert.That(material.HasProperty("_FarSide") && material.GetFloat("_FarSide") == 1, Is.True, "The far side is plain unless asked"); }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            foreach (DioramaFamily family in Enum.GetValues(typeof(DioramaFamily)))
            {
                float far = DioramaWater.Tint(family).a * DioramaWater.FarSide;
                Assert.That(far, Is.InRange(.25f, .45f), family + " far-side water opacity");
                var fluid = DioramaWater.Fluid(family);
                Assert.That(fluid.a * DioramaWater.FarSide, Is.InRange(.25f, .65f), family + " far-side fluid opacity");
            }
            Assert.That(DioramaWater.GlowShare, Is.InRange(.4f, .8f));
            Assert.That(DioramaWater.FluidGlowShare, Is.InRange(.15f, .35f));
        }

        [Test] public void EveryTrickStaysInsideTheTankGlass()
        {
            // Drives the real creature through its trick at mid-water (DioramaTricks.Settle), broadside either way, with its
            // tail and sails swishing, and measures its renderers' bounds as the AD-B route does every frame. The resident origin
            // sits 0.52 above the bottom of the water and 0.54 below its surface, 0.62 from the front and the back glass. The
            // classic shop's five only: GR-12's species swim in the greybox's tanks (GrowthKitCreatureTests and its route).
            bool was = DioramaLook.Active;
            DioramaLook.Active = true;
            try
            {
                foreach (var s in Classic())
                {
                    var tank = new GameObject("tank test");
                    try
                    {
                        var resident = new GameObject(s.Id).transform;
                        resident.SetParent(tank.transform, false);
                        resident.localScale = Vector3.one * (s.Id == "driftwing" ? .59f : .69f) * s.TankScale;
                        var creature = resident.gameObject.AddComponent<CreatureVisual>();
                        creature.Build(s.Id);
                        float d = DioramaTricks.Duration(s.Id), worst = 0;
                        string at = "";
                        for (float age = 0; age < d; age += .02f)
                            foreach (float heading in new[] { 90f, -90f })
                                for (int k = 0; k < 4; k++)
                                {
                                    var p = DioramaTricks.Trick(s.Id, age);
                                    creature.SetDioramaFace(Mathf.Max(.2f, p.Happy), p.Surprised, p.Puffed);
                                    creature.SetDioramaAction(p.Glow, p.Tail, p.Fins);
                                    resident.localPosition = Vector3.up * p.Lift;
                                    resident.localRotation = Quaternion.Euler(0, heading, 0) * Quaternion.Euler(p.Pitch, p.Yaw, p.Roll);
                                    creature.AnimateSpecies(age + k * 1.7f, .7f, false);
                                    foreach (var r in resident.GetComponentsInChildren<Renderer>())
                                    {
                                        var b = r.bounds;
                                        float over = Mathf.Max(Mathf.Max(b.max.y / .54f, -b.min.y / .52f), Mathf.Max(b.max.z, -b.min.z) / .62f);
                                        if (over > worst) { worst = over; at = r.name + " at " + age.ToString("0.00") + " s"; }
                                    }
                                }
                        Assert.That(worst, Is.LessThan(.97f), s.Id + " / " + at + " / largest tank scale about " + (s.TankScale * .97f / worst).ToString("0.00"));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tank); }
                }
            }
            finally { DioramaLook.Active = was; }
        }

        [Test] public void EveryKitSpeciesFitsTheDisplayTankAtItsScale()
        {
            // Static fit, with the swim path and the trick's lift and puff; the AD-B route checks every frame of the real swim.
            var interior = new Vector3(1.94f, 1.06f, 1.24f);
            foreach (var s in Classic())
            {
                var kit = Resources.Load<GameObject>(DioramaLook.KitPath + s.Kit).transform;
                var bounds = new Bounds();
                bool first = true;
                foreach (var r in kit.GetComponentsInChildren<Renderer>())
                {
                    var filter = r.GetComponent<MeshFilter>();
                    var mesh = filter ? filter.sharedMesh : ((SkinnedMeshRenderer)r).sharedMesh;
                    var b = new Bounds(r.transform.localPosition + mesh.bounds.center, mesh.bounds.size);
                    if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
                }
                float scale = s.ModelScale * (s.Id == "driftwing" ? .59f : .69f) * s.TankScale;
                float puff = s.Id == "bubblebelly" ? 1.28f : 1.05f;
                // Swimming broadside: its length runs along the tank, its width across it; the Driftwing's loop stands its whole
                // length upright at mid-water, and the Bubblebelly's spin turns its width.
                float length = bounds.size.z * scale, width = bounds.size.x * scale * puff, height = bounds.size.y * scale * puff;
                Assert.That(Mathf.Max(width, s.Id == "driftwing" ? length : 0), Is.LessThan(interior.z), s.Id + " across the tank");
                Assert.That(Mathf.Max(height, s.Id == "driftwing" ? length : s.Id == "bubblebelly" ? Mathf.Min(length, width) : height) + .1f, Is.LessThan(interior.y), s.Id + " top to bottom");
                Assert.That(length, Is.LessThan(interior.x * .75f), s.Id + " along the tank, with room to swim");
            }
        }
    }
}
