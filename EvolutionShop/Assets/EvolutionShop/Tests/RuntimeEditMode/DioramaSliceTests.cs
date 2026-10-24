using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>AD-A Pocket Aquarium Diorama slice: lighting keys, the hatch timeline, materials, the kit and its provenance.</summary>
    public sealed class DioramaSliceTests
    {
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test] public void EachPhasePicksItsLightingKey()
        {
            Assert.That(DioramaLighting.Resolve(ShopDayPhase.Night, .5f).SkyTop, Is.EqualTo(DioramaLighting.Key(DioramaKey.Night).SkyTop));
            Assert.That(DioramaLighting.Resolve(ShopDayPhase.Closing, 0).Sun, Is.EqualTo(DioramaLighting.Key(DioramaKey.Closing).Sun));
            Assert.That(DioramaLighting.Resolve(ShopDayPhase.Open, 0).SunPitch, Is.EqualTo(DioramaLighting.Key(DioramaKey.Morning).SunPitch));
            Assert.That(DioramaLighting.Resolve(ShopDayPhase.Open, .55f).SunPitch, Is.EqualTo(DioramaLighting.Key(DioramaKey.Midday).SunPitch));
            Assert.That(DioramaLighting.Nearest(ShopDayPhase.Open, 0), Is.EqualTo(DioramaKey.Morning));
            Assert.That(DioramaLighting.Nearest(ShopDayPhase.Open, .6f), Is.EqualTo(DioramaKey.Midday));
        }

        [Test] public void TheOpenDayMovesSmoothlyFromMorningTowardEvening()
        {
            float last = DioramaLighting.Resolve(ShopDayPhase.Open, 0).SunPitch;
            for (int i = 1; i <= 35; i++)
            {
                float pitch = DioramaLighting.Resolve(ShopDayPhase.Open, i / 100f).SunPitch;
                Assert.That(pitch, Is.GreaterThanOrEqualTo(last - .0001f), "The sun climbs through the morning");
                Assert.That(Mathf.Abs(pitch - last), Is.LessThan(2.5f), "No jump between nearby moments");
                last = pitch;
            }
            var late = DioramaLighting.Resolve(ShopDayPhase.Open, 1);
            var midday = DioramaLighting.Key(DioramaKey.Midday); var closing = DioramaLighting.Key(DioramaKey.Closing);
            Assert.That(late.Sun.g, Is.LessThan(midday.Sun.g), "Late afternoon is warmer than midday");
            Assert.That(late.Sun.g, Is.GreaterThan(closing.Sun.g), "but not yet closing time");
        }

        [Test] public void BadTimesAndBlendsStayInRange()
        {
            Assert.That(DioramaLighting.Resolve(ShopDayPhase.Open, float.NaN).SunPitch, Is.EqualTo(DioramaLighting.Key(DioramaKey.Morning).SunPitch));
            var a = DioramaLighting.Key(DioramaKey.Morning); var b = DioramaLighting.Key(DioramaKey.Night);
            Assert.That(DioramaLighting.Blend(a, b, 7).SunIntensity, Is.EqualTo(b.SunIntensity));
            Assert.That(DioramaLighting.Blend(a, b, -3).SunIntensity, Is.EqualTo(a.SunIntensity));
            Assert.That(DioramaLighting.Blend(a, b, float.PositiveInfinity).SunIntensity, Is.EqualTo(a.SunIntensity));
        }

        [Test] public void OnlyEveningAndNightGlowAndLightTheStreetLamp()
        {
            foreach (DioramaKey key in Enum.GetValues(typeof(DioramaKey)))
            {
                var k = DioramaLighting.Key(key);
                bool dark = key == DioramaKey.Night || key == DioramaKey.Closing;
                Assert.That(k.TankGlow > 0, Is.EqualTo(dark), "Tank glow / " + key);
                Assert.That(k.Lamp.maxColorComponent > 1, Is.EqualTo(dark), "Street lamp blooms / " + key);
                Assert.That(k.Stars.maxColorComponent > 0, Is.EqualTo(key == DioramaKey.Night), "Stars / " + key);
            }
        }

        [Test] public void TheDoorwayBlendsTheShopAndLabGrades()
        {
            Assert.That(DioramaLighting.LabWeight(new Vector3(0, 1.6f, -3)), Is.EqualTo(0));
            Assert.That(DioramaLighting.LabWeight(new Vector3(0, 1.6f, 3)), Is.EqualTo(1));
            Assert.That(DioramaLighting.LabWeight(new Vector3(0, 1.6f, .5f)), Is.InRange(.3f, .7f));
            Assert.That(DioramaLighting.LabWeight(new Vector3(0, 0, float.NaN)), Is.EqualTo(0));
        }

        [Test] public void TheHatchKeepsTheExistingCueDurations()
        {
            Assert.That(DioramaHatch.FirstDuration, Is.EqualTo(FxHatchChoreography.FirstDuration));
            Assert.That(DioramaHatch.RepeatDuration, Is.EqualTo(FxHatchChoreography.RepeatDuration));
            Assert.That(DioramaHatch.Resolve(-1, true, false).Squash, Is.EqualTo(0));
            Assert.That(DioramaHatch.Resolve(1.4f, true, false).Happy, Is.EqualTo(0));
            Assert.That(DioramaHatch.Resolve(float.NaN, true, false).Glow, Is.EqualTo(0));
        }

        [Test] public void FirstDiscoveryHoldsAPresentationOnlyFreezeBeat()
        {
            Assert.That(DioramaHatch.PopTime(true, false), Is.EqualTo(.38f).Within(1e-5f));
            for (float age = .3f; age < .379f; age += .01f)
            {
                Assert.That(DioramaHatch.Resolve(age, true, false).Frozen, Is.True, "Frozen at " + age);
                Assert.That(DioramaHatch.AnimationAge(age, true, false), Is.EqualTo(.3f), "The pose clock holds at " + age);
            }
            Assert.That(DioramaHatch.AnimationAge(.5f, true, false), Is.EqualTo(.42f).Within(1e-5f), "Afterwards the pose runs 0.08 s behind");
            Assert.That(DioramaHatch.Resolve(.25f, true, false).Frozen, Is.False);
            Assert.That(DioramaHatch.Resolve(.4f, true, false).Frozen, Is.False);
        }

        [Test] public void ReducedMotionAndRepeatsHaveNoFreezeFlashOrShiver()
        {
            for (float age = 0; age < 1.4f; age += .01f)
            {
                var reduced = DioramaHatch.Resolve(age, true, true);
                Assert.That(reduced.Frozen || reduced.Shiver > 0 || reduced.RingFlash > 0 || reduced.TurnToViewer > 0, Is.False, "Reduced at " + age);
                Assert.That(DioramaHatch.AnimationAge(age, true, true), Is.EqualTo(age));
            }
            for (float age = 0; age < .85f; age += .01f)
            {
                var repeat = DioramaHatch.Resolve(age, false, false);
                Assert.That(repeat.Frozen || repeat.TurnToViewer > 0 || repeat.Blink > 0, Is.False, "Repeat is wind-up, pop and settle only / " + age);
            }
            Assert.That(DioramaHatch.Resolve(.15f, true, true).Squash, Is.LessThan(DioramaHatch.Resolve(.15f, true, false).Squash));
        }

        [Test] public void FirstDiscoveryLooksAtYouAndBlinksOnce()
        {
            float peakTurn = 0; int blinkPeaks = 0; float last = 0, prev = 0;
            for (float age = 0; age < 1.4f; age += .005f)
            {
                var p = DioramaHatch.Resolve(age, true, false);
                peakTurn = Mathf.Max(peakTurn, p.TurnToViewer);
                if (last > prev && last > p.Blink && last > .9f) blinkPeaks++;
                prev = last; last = p.Blink;
            }
            Assert.That(peakTurn, Is.EqualTo(1).Within(.01f));
            Assert.That(blinkPeaks, Is.EqualTo(1));
        }

        [Test] public void BurstSizesFollowTheComfortModes()
        {
            DioramaHatch.BurstCounts(true, false, false, out int b, out int p, out int s); Assert.That((b, p, s), Is.EqualTo((40, 3, 12)));
            DioramaHatch.BurstCounts(false, false, false, out b, out p, out s); Assert.That((b, p, s), Is.EqualTo((18, 1, 4)));
            DioramaHatch.BurstCounts(true, true, false, out b, out p, out s); Assert.That((b, p, s), Is.EqualTo((20, 2, 6)));
            DioramaHatch.BurstCounts(true, false, true, out b, out p, out s); Assert.That((b, p, s), Is.EqualTo((6, 0, 0)));
        }

        [Test] public void OnlyCreaturesAndCharactersAreOutlined()
        {
            foreach (DioramaLook.Style style in Enum.GetValues(typeof(DioramaLook.Style)))
            {
                var m = DioramaLook.Toon("test " + style, Color.gray, style);
                try
                {
                    bool alive = style == DioramaLook.Style.Creature || style == DioramaLook.Style.Character;
                    Assert.That(m.shader.name, Is.EqualTo(DioramaLook.ToonShaderName));
                    Assert.That(m.GetShaderPassEnabled(DioramaLook.OutlinePass), Is.EqualTo(alive), "Outline pass / " + style);
                    Assert.That(m.GetFloat("_OutlineWidth") > 0, Is.EqualTo(alive), "Outline width / " + style);
                }
                finally { UnityEngine.Object.DestroyImmediate(m); }
            }
        }

        [Test] public void TheToonShaderHasEveryPassURPNeeds()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "EvolutionShop", "Resources", DioramaLook.ToonResource + ".shader"));
            foreach (var mode in new[] { "UniversalForward", "SRPDefaultUnlit", "ShadowCaster", "DepthOnly", "DepthNormals" })
                Assert.That(source, Does.Contain("\"LightMode\"=\"" + mode + "\""), mode);
            Assert.That(source, Does.Contain("_CLUSTER_LIGHT_LOOP"), "Forward+ additional lights");
            Assert.That(source, Does.Contain("_SCREEN_SPACE_OCCLUSION"), "Takes part in the renderer's SSAO");
            foreach (var resource in new[] { DioramaLook.ToonResource, DioramaLook.GlassResource, DioramaLook.UnlitResource, DioramaLook.WindowResource })
                Assert.That(Resources.Load<Shader>(resource), Is.Not.Null, resource);
        }

        [Test] public void KitPondletHasItsPivotsAndFaceShapes()
        {
            var pondlet = Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaPondlet");
            Assert.That(pondlet, Is.Not.Null);
            foreach (var part in new[] { "Tail pivot", "Left fin pivot", "Right fin pivot", "Crest pivot", "Body", "Lids", "Lips", "Pupils" })
                Assert.That(pondlet.transform.Find(part), Is.Not.Null, part);
            var shapes = pondlet.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(r => Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(i => r.sharedMesh.GetBlendShapeName(i))).ToArray();
            Assert.That(shapes, Is.SupersetOf(new[] { "Blink", "Happy", "Surprised", "Puffed" }));
        }

        [Test] public void KitVisitorHasTheClassicJointsAndFourMoods()
        {
            var visitor = Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaCasualVisitor");
            Assert.That(visitor, Is.Not.Null);
            foreach (var joint in new[] { "Left hip", "Right hip", "Left shoulder", "Right shoulder", "Head pivot" })
                Assert.That(visitor.transform.Find(joint), Is.Not.Null, joint);
            var mouth = visitor.transform.Find("Mouth").GetComponent<SkinnedMeshRenderer>();
            foreach (var mood in new[] { "Happy", "Haggle", "Shocked", "Ouch" })
                Assert.That(mouth.sharedMesh.GetBlendShapeIndex(mood), Is.GreaterThanOrEqualTo(0), mood);
            Assert.That(visitor.transform.Find("Lids").GetComponent<SkinnedMeshRenderer>().sharedMesh.GetBlendShapeIndex("Blink"), Is.GreaterThanOrEqualTo(0), "The visitor blinks");
            foreach (var leg in new[] { "Left hip", "Right hip" })
            {
                var filters = visitor.transform.Find(leg).GetComponentsInChildren<MeshFilter>();
                Assert.That(filters.Length, Is.GreaterThan(0), "Leg floor contact reads plain meshes / " + leg);
                Assert.That(filters.All(f => f.sharedMesh.isReadable), Is.True, "The customer reads the leg vertices / " + leg);
            }
        }

        [Test] public void KitPartsSitWhereUnityExpectsThemAndRestUnrotated()
        {
            // The FBX conversion once mirrored the kit and moved nested parts: the Pondlet faces -Z, the visitor +Z,
            // left is -X, and every part is a direct child of the root with no rest rotation.
            var pondlet = Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaPondlet").transform;
            var visitor = Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaCasualVisitor").transform;
            var kits = new List<Transform> { visitor };
            foreach (var spec in DioramaSpecies.All) kits.Add(Resources.Load<GameObject>(DioramaLook.KitPath + spec.Kit).transform);
            foreach (var root in kits)
            {
                Assert.That(Quaternion.Angle(root.localRotation, Quaternion.identity), Is.LessThan(.01f), root.name);
                Assert.That(Vector3.Distance(root.localScale, Vector3.one), Is.LessThan(.001f), root.name);
                foreach (Transform part in root)
                {
                    Assert.That(Quaternion.Angle(part.localRotation, Quaternion.identity), Is.LessThan(.01f), root.name + "/" + part.name);
                    Assert.That(Vector3.Distance(part.localScale, Vector3.one), Is.LessThan(.001f), root.name + "/" + part.name);
                    Assert.That(part.childCount, Is.EqualTo(0), "Flat kit / " + root.name + "/" + part.name);
                    Assert.That(part.name, Does.Not.Contain("."), "No clashing Blender names / " + root.name);
                }
            }
            Assert.That(pondlet.Find("Tail pivot").localPosition.z, Is.GreaterThan(.25f), "Tail behind");
            // GR-12B made the Pondlet a dwarf gourami, a slim fish, so its fins sit nearer its middle than the round AD-A Pondlet's.
            Assert.That(pondlet.Find("Left fin pivot").localPosition.x, Is.LessThan(-.08f));
            Assert.That(pondlet.Find("Right fin pivot").localPosition.x, Is.GreaterThan(.08f));
            Assert.That(pondlet.Find("Crest pivot").localPosition.y, Is.GreaterThan(.15f), "Crest on top");
            Assert.That(MeshCentre(pondlet.Find("Lips")).z, Is.LessThan(-.3f), "Pouty lips at the front (-Z)");
            Assert.That(MeshCentre(pondlet.Find("Tail pivot")).z, Is.GreaterThan(.4f), "Tail lobes reach back");
            Assert.That(visitor.Find("Left hip").localPosition.x, Is.LessThan(0));
            Assert.That(visitor.Find("Right hip").localPosition.x, Is.GreaterThan(0));
            Assert.That(visitor.Find("Left shoulder").localPosition.x, Is.LessThan(0));
            Assert.That(visitor.Find("Right shoulder").localPosition.x, Is.GreaterThan(0));
            Assert.That(visitor.Find("Head pivot").localPosition.y, Is.EqualTo(.95f).Within(.001f));
            Assert.That(MeshCentre(visitor.Find("Mouth")).z, Is.GreaterThan(.2f), "Mouth on the front (+Z)");
            Assert.That(MeshCentre(visitor.Find("Right shoulder")).y, Is.LessThan(MeshCentre(visitor.Find("Left shoulder")).y - .05f), "The tote hangs from the right hand");
        }

        static Vector3 MeshCentre(Transform part)
        {
            var filter = part.GetComponent<MeshFilter>();
            var skinned = part.GetComponent<SkinnedMeshRenderer>();
            var mesh = filter ? filter.sharedMesh : skinned.sharedMesh;
            return part.localPosition + mesh.bounds.center;
        }

        [Test] public void EveryKitSlotHasAPaletteColour()
        {
            foreach (var asset in new[] { "DioramaPondlet", "DioramaBubblebelly", "DioramaRibbonfin", "DioramaLanternfin", "DioramaDriftwing", "DioramaCasualVisitor" })
            {
                var root = new GameObject("kit test").transform;
                var owned = new System.Collections.Generic.List<Material>();
                try
                {
                    var model = DioramaLook.InstantiateKit(asset, root, DioramaLook.Style.Creature, owned);
                    Assert.That(model, Is.Not.Null, asset);
                    Assert.That(model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).All(m => m && m.shader.name == DioramaLook.ToonShaderName), Is.True, asset);
                }
                finally
                {
                    foreach (var m in owned) UnityEngine.Object.DestroyImmediate(m);
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                }
            }
        }

        [Test] public void TheFlipbookSheetIsFourFramesOfSixShapes()
        {
            var sheet = Resources.Load<Texture2D>(DioramaLook.KitPath + "DioramaFlipbook");
            Assert.That(sheet, Is.Not.Null);
            Assert.That(sheet.width, Is.EqualTo(ShopWorld.DioramaFlipColumns * 128));
            Assert.That(sheet.height, Is.EqualTo(ShopWorld.DioramaFlipRows * 128));
            Assert.That(sheet.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test] public void TheKitPondletReplacesTheClassicOneOnlyWhileTheLookIsOn()
        {
            bool prior = DioramaLook.Active;
            var go = new GameObject("pondlet test");
            try
            {
                DioramaLook.Active = true;
                var kit = go.AddComponent<CreatureVisual>(); kit.Build("pondlet");
                Assert.That(kit.IsDiorama, Is.True);
                Assert.That(kit.ContainerScale(false), Is.EqualTo(CreatureVisual.DioramaTankScale));
                Assert.That(kit.ContainerScale(true), Is.EqualTo(CreatureVisual.DioramaIncubatorScale));
                kit.SetDioramaFace(1, 0, 0, 1);
                kit.AnimatePondlet(1, .5f, false);
                Assert.That(kit.DioramaWeightForValidation("Blink"), Is.EqualTo(100).Within(.01f));
                Assert.That(kit.DioramaWeightForValidation("Happy"), Is.EqualTo(100).Within(.01f));
                // AD-B redesigned the other four species too; DioramaCreatureTests covers them.
                var other = new GameObject("bubblebelly").AddComponent<CreatureVisual>(); other.Build("bubblebelly");
                Assert.That(other.IsDiorama && other.DioramaSpecies.Id == "bubblebelly", Is.True, "Every species has its kit model since AD-B");
                UnityEngine.Object.DestroyImmediate(other.gameObject);
                DioramaLook.Active = false;
                var classic = new GameObject("classic").AddComponent<CreatureVisual>(); classic.Build("pondlet");
                Assert.That(classic.IsDiorama, Is.False);
                Assert.That(classic.ContainerScale(false), Is.EqualTo(1));
                UnityEngine.Object.DestroyImmediate(classic.gameObject);
            }
            finally { DioramaLook.Active = prior; UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void TheKitVisitorsFaceFollowsItsMood()
        {
            var go = new GameObject("visitor test");
            try
            {
                var visitor = go.AddComponent<ShopCharacterVisual>();
                Assert.That(visitor.BuildDioramaCasual(), Is.True);
                Assert.That(visitor.IsDiorama && visitor.LeftLeg && visitor.RightLeg && visitor.LeftArm && visitor.RightArm, Is.True);
                Assert.That(visitor.BuildDioramaCasual(), Is.False, "Builds once");
                foreach (var part in ShopCharacterVisual.DioramaHeadParts)
                    Assert.That(go.transform.Find("Head pivot/" + part), Is.Not.Null, "The face rides on the head / " + part);
                var mouthPart = go.transform.Find("Head pivot/Mouth");
                Assert.That(mouthPart.localPosition.magnitude, Is.LessThan(.001f), "Face parts keep their place on the head");
                Assert.That(go.transform.Find("Head pivot/Left brow pivot").localPosition.z, Is.GreaterThan(.2f));
                for (int i = 0; i < 30; i++) visitor.TickDioramaFace(DioramaVisitorMood.Shocked, .05f, false);
                Assert.That(visitor.DioramaMouthWeight(DioramaVisitorMood.Shocked), Is.EqualTo(1).Within(.001f));
                Assert.That(visitor.DioramaMouthWeight(DioramaVisitorMood.Happy), Is.EqualTo(0).Within(.001f));
                visitor.Pose(CharacterSamplePose.Walk, .3f);
                Assert.That(visitor.LeftLeg.localRotation, Is.Not.EqualTo(Quaternion.identity), "The classic walk drives the kit's hips");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void KitFilesMatchTheirProvenanceManifest()
        {
            string manifestPath = Path.Combine(Repo, "ArtSource", "DioramaKit", "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True, manifestPath);
            string manifest = File.ReadAllText(manifestPath);
            Assert.That(manifest, Does.Contain("\"paid_generation\": false"));
            Assert.That(manifest, Does.Contain("\"external_sources\": []"));
            using (var sha = SHA256.Create())
                foreach (var file in new[] { "DioramaPondlet.fbx", "DioramaBubblebelly.fbx", "DioramaRibbonfin.fbx", "DioramaLanternfin.fbx",
                    "DioramaDriftwing.fbx", "DioramaCasualVisitor.fbx", "DioramaBargainVisitor.fbx", "DioramaCollectorVisitor.fbx",
                    "DioramaPremiumVisitor.fbx", "DioramaCrewCashier.fbx", "DioramaCrewStocker.fbx", "DioramaCrewTechnician.fbx", "DioramaFlipbook.png", "DioramaVariantAtlas.png" })
                {
                    var bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, "EvolutionShop", "Resources", "Art", "DioramaKit", file));
                    string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    Assert.That(manifest, Does.Contain(hash), "Exported bytes are the ones the manifest records / " + file);
                }
        }
    }
}
