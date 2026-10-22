using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>AD-C characters: the four customer types and the three crew on one humanoid rig, the role table against the kit
    /// and its manifest, the crew's teal, each role's reactions and rebuilding a figure in place when the look switches.</summary>
    public sealed class DioramaCharacterTests
    {
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        static string Manifest => File.ReadAllText(Path.Combine(Repo, "ArtSource", "DioramaKit", "manifest.json"));
        static readonly string[] Joints = { "Left hip", "Right hip", "Body", "Left shoulder", "Right shoulder", "Head pivot" };

        [Test] public void EveryCharacterIsInTheManifestInOrderWithItsCrewFlag()
        {
            string manifest = Manifest;
            int at = manifest.IndexOf("\"characters\"", System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "The manifest lists the characters");
            foreach (var role in DioramaRoles.All)
            {
                int found = manifest.IndexOf("\"role\": \"" + role.Id + "\"", at, System.StringComparison.Ordinal);
                Assert.That(found, Is.GreaterThan(at), "Manifest character order / " + role.Id);
                string entry = manifest.Substring(found, 120);
                Assert.That(entry, Does.Contain("\"asset\": \"" + role.Kit + "\""), role.Id);
                Assert.That(entry, Does.Contain("\"crew\": " + (role.Crew ? "true" : "false")), role.Id);
                at = found;
            }
            Assert.That(DioramaRoles.Customers, Is.EqualTo(DioramaRoles.All.Where(r => !r.Crew).Select(r => r.Id)));
            Assert.That(DioramaRoles.CrewRoles, Is.EqualTo(DioramaRoles.All.Where(r => r.Crew).Select(r => r.Id)));
            Assert.That(CustomerArchetypes.CreateDefault().Select(a => a.PresentationId), Is.EquivalentTo(DioramaRoles.Customers),
                "Every customer type the game has, and only those, is a kit character");
            Assert.That(DioramaRoles.CrewRoleOf(ShopStaff.CashierId), Is.EqualTo("cashier"));
            Assert.That(DioramaRoles.CrewRoleOf(ShopStaff.StockerId), Is.EqualTo("stocker"));
            Assert.That(DioramaRoles.CrewRoleOf(ShopStaff.TechnicianId), Is.EqualTo("technician"));
            Assert.That(DioramaRoles.CrewRoleOf("nobody"), Is.Null);
            Assert.That(manifest, Does.Contain("\"step_stool_m\": 0.3"), "Pip's step stool is recorded");
        }

        [Test] public void EveryCharacterSharesTheHumanoidRig()
        {
            foreach (var role in DioramaRoles.All)
            {
                var kit = Resources.Load<GameObject>(DioramaLook.KitPath + role.Kit);
                Assert.That(kit, Is.Not.Null, role.Kit);
                var root = kit.transform;
                foreach (var part in Joints.Concat(ShopCharacterVisual.DioramaHeadParts)) Assert.That(root.Find(part), Is.Not.Null, role.Id + " / " + part);
                Assert.That(Shapes(root, "Mouth"), Is.SupersetOf(new[] { "Happy", "Haggle", "Shocked", "Ouch" }), role.Id);
                Assert.That(Shapes(root, "Lids"), Does.Contain("Blink"), role.Id);
                Assert.That(root.Find("Carry crate") != null, Is.EqualTo(role.Crew), "Only the crew carry a crate / " + role.Id);
                Assert.That(root.Find("Step stool") != null, Is.EqualTo(role.Id == "cashier"), "Only Pip stands on a step stool / " + role.Id);
                Assert.That(Quaternion.Angle(root.localRotation, Quaternion.identity), Is.LessThan(.01f), role.Id);
                foreach (Transform part in root)
                {
                    Assert.That(part.childCount, Is.EqualTo(0), "Flat kit / " + role.Id + "/" + part.name);
                    Assert.That(Quaternion.Angle(part.localRotation, Quaternion.identity), Is.LessThan(.01f), role.Id + "/" + part.name);
                    Assert.That(Vector3.Distance(part.localScale, Vector3.one), Is.LessThan(.001f), role.Id + "/" + part.name);
                    Assert.That(part.name, Does.Not.Contain("."), "No clashing Blender names / " + role.Id);
                }
                // Front +Z, left -X, as the classic figures.
                Assert.That(root.Find("Left hip").localPosition.x, Is.LessThan(0), role.Id);
                Assert.That(root.Find("Right hip").localPosition.x, Is.GreaterThan(0), role.Id);
                Assert.That(root.Find("Left shoulder").localPosition.x, Is.LessThan(0), role.Id);
                Assert.That(root.Find("Right shoulder").localPosition.x, Is.GreaterThan(0), role.Id);
                Assert.That(Centre(root.Find("Mouth")).z, Is.GreaterThan(Centre(root.Find("Head")).z + .15f), "Mouth on the front (+Z) / " + role.Id);
                foreach (var leg in new[] { "Left hip", "Right hip" })
                {
                    var filters = root.Find(leg).GetComponentsInChildren<MeshFilter>();
                    Assert.That(filters.Length > 0 && filters.All(f => f.sharedMesh.isReadable), Is.True, "Leg floor contact reads the vertices / " + role.Id + " / " + leg);
                }
            }
        }

        [Test] public void TheRoleTableMatchesTheKit()
        {
            foreach (var role in DioramaRoles.All)
            {
                var root = Resources.Load<GameObject>(DioramaLook.KitPath + role.Kit).transform;
                float low = float.MaxValue, top = float.MinValue;
                foreach (Transform part in root)
                {
                    if (part.name == "Carry crate") continue;
                    var mesh = Mesh(part);
                    if (!mesh) continue;
                    low = Mathf.Min(low, part.localPosition.y + mesh.bounds.min.y);
                    top = Mathf.Max(top, part.localPosition.y + mesh.bounds.max.y);
                }
                Assert.That(low, Is.EqualTo(0).Within(.012f), "Feet (or Pip's stool) on the floor / " + role.Id);
                Assert.That(top, Is.EqualTo(role.HeadTop).Within(.02f), "Head and hat top / " + role.Id);
                Assert.That(root.Find("Left shoulder").localPosition.y, Is.EqualTo(role.Shoulder).Within(.012f), "Shoulder height / " + role.Id);
                float headY = root.Find("Head pivot").localPosition.y;
                Assert.That(headY, Is.InRange(role.Shoulder - .05f, role.Shoulder + .25f), "The head sits on the shoulders / " + role.Id);
                Assert.That(role.Squint, Is.InRange(0f, .5f)); Assert.That(role.BrowTilt, Is.InRange(0f, 25f));
                Assert.That(role.StarHeight, Is.EqualTo(role.HeadTop + .13f).Within(.0001f));
            }
            // AD.4's body types: Casual round, Bargain tall and thin, Collector pear, Big spender a wide coat; Pip lifted by the stool.
            Assert.That(Width("bargain"), Is.LessThan(Width("casual")), "The bargain hunter is thinner than the casual visitor");
            Assert.That(Top("bargain"), Is.GreaterThan(Top("casual") + .1f), "The bargain hunter is taller");
            Assert.That(Width("premium"), Is.GreaterThan(Width("casual")), "The big spender's coat is the widest");
            Assert.That(HipWidth("collector"), Is.GreaterThan(ShoulderWidth("collector")), "The collector is pear-shaped");
            Assert.That(Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaCrewCashier").transform.Find("Left hip").localPosition.y,
                Is.GreaterThan(Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaCasualVisitor").transform.Find("Left hip").localPosition.y + .25f), "Pip stands on the step stool");
        }

        static Transform Kit(string role) { DioramaRoles.TryGet(role, out var r); return Resources.Load<GameObject>(DioramaLook.KitPath + r.Kit).transform; }
        static float Width(string role) { var b = Mesh(Kit(role).Find("Body")).bounds; return b.size.x; }
        static float Top(string role) { DioramaRoles.TryGet(role, out var r); return r.HeadTop; }
        static float HipWidth(string role) { var b = Mesh(Kit(role).Find("Body")).bounds; var m = Mesh(Kit(role).Find("Body")); return WidthAt(m, b.min.y + b.size.y * .2f); }
        static float ShoulderWidth(string role) { var b = Mesh(Kit(role).Find("Body")).bounds; var m = Mesh(Kit(role).Find("Body")); return WidthAt(m, b.max.y - b.size.y * .15f); }
        static float WidthAt(Mesh mesh, float y)
        {
            float low = float.MaxValue, high = float.MinValue;
            foreach (var v in mesh.vertices) if (Mathf.Abs(v.y - y) < .04f) { low = Mathf.Min(low, v.x); high = Mathf.Max(high, v.x); }
            return high - low;
        }

        [Test] public void CrewWearShopTealAndCustomersNever()
        {
            foreach (var role in DioramaRoles.All)
            {
                var root = new GameObject("kit test").transform;
                var owned = new List<Material>();
                try
                {
                    var model = DioramaLook.InstantiateKit(role.Kit, root, DioramaLook.Style.Character, owned);
                    Assert.That(model, Is.Not.Null, role.Kit);
                    var names = owned.Select(m => m.name.Replace("Diorama / ", "")).ToList();
                    if (role.Crew) Assert.That(names, Does.Contain("DK_CrewTeal"), "Crew wear shop teal / " + role.Id);
                    else Assert.That(names.Any(n => n.StartsWith("DK_Crew") || n.StartsWith("DK_Pip") || n.StartsWith("DK_Nori") || n.StartsWith("DK_Moss")), Is.False,
                        "Customers never wear the crew's slots / " + role.Id);
                    Assert.That(model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).All(m => m && m.shader.name == DioramaLook.ToonShaderName), Is.True, role.Id);
                }
                finally
                {
                    foreach (var m in owned) Object.DestroyImmediate(m);
                    Object.DestroyImmediate(root.gameObject);
                }
            }
            // Teal is the crew's alone: no customer slot is close to it.
            DioramaLook.TryGetSlotColor("DK_CrewTeal", out var teal);
            foreach (var slot in DioramaLook.KitPalette.Keys.Where(k => k.StartsWith("DK_Bargain") || k.StartsWith("DK_Collector") || k.StartsWith("DK_Premium") || k.StartsWith("DK_Casual")))
            {
                DioramaLook.TryGetSlotColor(slot, out var c);
                float d = Mathf.Sqrt((c.r - teal.r) * (c.r - teal.r) + (c.g - teal.g) * (c.g - teal.g) + (c.b - teal.b) * (c.b - teal.b));
                Assert.That(d, Is.GreaterThan(.15f), "A customer colour reads as crew teal / " + slot);
            }
        }

        [Test] public void TheKitPaletteMirrorsTheManifest()
        {
            var found = Regex.Matches(Manifest, "\"(DK_[A-Za-z0-9_]+)\":\\s*\\{\\s*\"srgb_hex\":\\s*\"#([0-9A-Fa-f]{6})\"");
            Assert.That(found.Count, Is.GreaterThan(60), "The manifest records the kit's materials");
            var manifest = new Dictionary<string, string>();
            foreach (Match m in found) manifest[m.Groups[1].Value] = m.Groups[2].Value.ToUpperInvariant();
            foreach (var pair in DioramaLook.KitPalette)
            {
                Assert.That(manifest.ContainsKey(pair.Key), Is.True, "The palette slot is in the manifest / " + pair.Key);
                Assert.That(manifest[pair.Key], Is.EqualTo(pair.Value.ToUpperInvariant()), "Same colour as the kit script / " + pair.Key);
            }
            foreach (var slot in manifest.Keys)
                Assert.That(DioramaLook.KitPalette.ContainsKey(slot) || DioramaVariants.TryParse(slot, out _, out _), Is.True, "Every manifest slot resolves in game / " + slot);
        }

        [Test] public void EveryRoleBuildsAndReactsItsOwnWay()
        {
            bool prior = DioramaLook.Active;
            DioramaLook.Active = true;
            try
            {
                foreach (var role in DioramaRoles.All)
                {
                    var go = new GameObject(role.Id + " test");
                    try
                    {
                        var figure = go.AddComponent<ShopCharacterVisual>();
                        figure.BuildForRole(role.Id, role.Crew);
                        Assert.That(figure.IsDiorama && figure.DioramaRole == role, Is.True, role.Id);
                        Assert.That(figure.LeftLeg && figure.RightLeg && figure.LeftArm && figure.RightArm, Is.True, role.Id);
                        foreach (var part in ShopCharacterVisual.DioramaHeadParts) Assert.That(go.transform.Find("Head pivot/" + part), Is.Not.Null, role.Id + " / " + part);
                        var socket = figure.WorkerProxy;
                        Assert.That(socket, Is.Not.Null, role.Id);
                        Assert.That(Vector3.Distance(socket.localPosition, new Vector3(0, .91f, .38f) * role.CarryScale), Is.LessThan(.001f), "The carry socket scales with the role / " + role.Id);
                        Assert.That(socket.Find("Carry crate") != null, Is.EqualTo(role.Crew), "The crate hangs on the socket / " + role.Id);
                        Assert.That(socket.gameObject.activeSelf, Is.False, "Hidden until carrying / " + role.Id);
                        Assert.That(go.transform.Find("Carry crate"), Is.Null, "The crate moved onto the socket / " + role.Id);
                        var reactions = DioramaRoles.Reactions(role);
                        Assert.That(reactions.Count, Is.EqualTo(4), role.Id);
                        Assert.That(reactions, Is.EqualTo(role.Crew
                            ? new[] { DioramaVisitorMood.Focus, DioramaVisitorMood.Happy, DioramaVisitorMood.Shocked, DioramaVisitorMood.Greet }
                            : new[] { DioramaVisitorMood.Haggle, DioramaVisitorMood.Happy, DioramaVisitorMood.Shocked, DioramaVisitorMood.Ouch }), role.Id);
                        foreach (var mood in reactions)
                        {
                            for (int i = 0; i < 30; i++) figure.TickDioramaFace(mood, .05f, true);
                            float expected = mood == DioramaVisitorMood.Haggle ? .85f : mood == DioramaVisitorMood.Focus ? .55f : 1;
                            Assert.That(figure.DioramaMouthWeight(mood), Is.EqualTo(expected).Within(.001f), role.Id + " / " + mood);
                            float lids = mood == DioramaVisitorMood.Ouch ? .7f : mood == DioramaVisitorMood.Haggle || mood == DioramaVisitorMood.Focus ? role.Squint : 0;
                            Assert.That(figure.DioramaLidWeight, Is.EqualTo(lids).Within(.002f), "Lids / " + role.Id + " / " + mood);
                            if (mood == DioramaVisitorMood.Haggle)
                                Assert.That(figure.DioramaBrowLift, Is.EqualTo(.85f * role.BrowTilt).Within(.5f), "Its own raised brow / " + role.Id);
                            if (mood == DioramaVisitorMood.Greet || mood == DioramaVisitorMood.Shocked)
                                Assert.That(figure.DioramaBrowLift, Is.GreaterThan(5), "Brows up / " + role.Id + " / " + mood);
                        }
                        figure.Pose(CharacterSamplePose.Walk, .3f);
                        Assert.That(figure.LeftLeg.localRotation, Is.Not.EqualTo(Quaternion.identity), "The classic walk drives the hips / " + role.Id);
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            finally { DioramaLook.Active = prior; }
        }

        [Test] public void AFigureRebuildsInPlaceWhenTheLookSwitches()
        {
            bool prior = DioramaLook.Active;
            var go = new GameObject("cashier test");
            try
            {
                DioramaLook.Active = true;
                var figure = go.AddComponent<ShopCharacterVisual>();
                figure.BuildForRole("cashier", true);
                var sign = new GameObject("Cashier duty sign").transform; sign.SetParent(go.transform, false);
                int kitChildren = go.transform.childCount;
                Assert.That(figure.IsDiorama && go.transform.Find("Step stool"), Is.True);
                Assert.That(figure.RebuildForLook(), Is.False, "Nothing to do while the look is unchanged");
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    DioramaLook.Active = false;
                    Assert.That(figure.RebuildForLook(), Is.True, "Rebuilds as the classic figure / " + cycle);
                    Assert.That(figure.IsDiorama, Is.False);
                    Assert.That(figure.DioramaRole, Is.Null);
                    Assert.That(go.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Step stool" || t.name == "Carry crate" || t.name == "Head"), Is.False,
                        "No kit part left behind / " + cycle);
                    Assert.That(go.transform.Find("Cashier duty sign"), Is.SameAs(sign), "The owner's sign stays / " + cycle);
                    Assert.That(figure.LeftLeg && figure.RightLeg && figure.LeftArm && figure.RightArm && figure.WorkerProxy, Is.True, "The classic joints are found / " + cycle);
                    DioramaLook.Active = true;
                    Assert.That(figure.RebuildForLook(), Is.True, "Rebuilds as the kit figure / " + cycle);
                    Assert.That(figure.IsDiorama && figure.DioramaRole.Id == "cashier" && go.transform.Find("Step stool"), Is.True, cycle.ToString());
                    Assert.That(go.transform.Find("Cashier duty sign"), Is.SameAs(sign));
                    Assert.That(go.transform.childCount, Is.EqualTo(kitChildren), "Nothing piles up across switches / " + cycle);
                }
            }
            finally { DioramaLook.Active = prior; Object.DestroyImmediate(go); }
        }

        [Test] public void AWorkerKeepsItsFeetOnTheFloorAcrossLookSwitches()
        {
            // A hired crew member is a worker (its legs' resting places are measured for the floor contact); a switch, even
            // while it is hidden, rebuilds it with its own legs where its kit or the classic figure puts them.
            bool prior = DioramaLook.Active;
            try
            {
                foreach (var role in DioramaRoles.CrewRoles)
                    foreach (bool hidden in new[] { false, true })
                    {
                        var go = new GameObject(role + " worker test");
                        try
                        {
                            DioramaLook.Active = false;
                            var figure = go.AddComponent<ShopCharacterVisual>();
                            figure.BuildForRole(role, true);
                            figure.ConfigureWorker(null, "test-" + role);
                            var classicHip = figure.LeftLeg.localPosition;
                            for (int cycle = 0; cycle < 2; cycle++)
                            {
                                if (hidden) go.SetActive(false);
                                DioramaLook.Active = true; figure.RebuildForLook(); go.SetActive(true);
                                figure.Pose(CharacterSamplePose.Idle, 0);
                                var kitHip = Kit(role).Find("Left hip").localPosition;
                                Assert.That(Vector3.Distance(figure.LeftLeg.localPosition, kitHip), Is.LessThan(.001f), "The kit's own hip / " + role + " / hidden " + hidden);
                                Assert.That(Lowest(go.transform), Is.EqualTo(0).Within(.012f), "Kit feet on the floor / " + role + " / hidden " + hidden);
                                figure.PreviewWorkerMotionForValidation(FxWorkerActivity.Idle, 0, 0, 0, 0, FxWorkerEvent.None, 0, false, true);
                                Assert.That(Lowest(go.transform), Is.EqualTo(0).Within(.012f), "Kit feet on the floor in the worker pose / " + role);
                                if (hidden) go.SetActive(false);
                                DioramaLook.Active = false; figure.RebuildForLook(); go.SetActive(true);
                                figure.Pose(CharacterSamplePose.Idle, 0);
                                Assert.That(Vector3.Distance(figure.LeftLeg.localPosition, classicHip), Is.LessThan(.001f), "The classic hip again / " + role + " / hidden " + hidden);
                            }
                        }
                        finally { Object.DestroyImmediate(go); }
                    }
            }
            finally { DioramaLook.Active = prior; }
        }

        static float Lowest(Transform root)
        {
            float low = float.MaxValue;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                bool carried = false;
                for (var t = r.transform; t && t != root; t = t.parent) if (t.name == "Preview parcel") carried = true;
                if (!carried) low = Mathf.Min(low, r.bounds.min.y);
            }
            return low;
        }

        static string[] Shapes(Transform root, string part)
        {
            var mesh = root.Find(part).GetComponent<SkinnedMeshRenderer>().sharedMesh;
            return Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
        }
        static Mesh Mesh(Transform part)
        {
            if (!part) return null;
            var filter = part.GetComponent<MeshFilter>();
            var skinned = part.GetComponent<SkinnedMeshRenderer>();
            return filter ? filter.sharedMesh : skinned ? skinned.sharedMesh : null;
        }
        static Vector3 Centre(Transform part) => part.localPosition + Mesh(part).bounds.center;
    }
}
