using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop.Tests
{
    /// <summary>GR-12: the kit creatures in the greybox. Every kit species shows as its kit model at its GR size class, facing the
    /// way it swims, in its specimen's colour from the atlas, without shadows; it plays its trick after a linger in peek, not
    /// with Reduced motion; crowns are grander; species outside the kit keep their placeholders.</summary>
    public sealed class GrowthKitCreatureTests
    {
        static GrowthCreatureView Make(Transform parent, string species, Colour colour = Colour.Classic, int id = 7) =>
            GrowthCreatureView.Create(new GrowthSpecimen { Id = id, Species = species, Colour = colour }, parent);

        static Bounds BoundsIn(Transform space, Transform of)
        {
            var b = new Bounds(); bool first = true;
            var m = space.worldToLocalMatrix;
            foreach (var r in of.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh || r.name == "Reserved tag") continue;
                var to = m * r.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var p = to.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(mesh.bounds.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        [Test] public void EveryKitSpeciesShowsAsItsKitModelAtItsSizeClass()
        {
            var parent = new GameObject("GR-12 tank").transform;
            try
            {
                foreach (var s in DioramaSpecies.All)
                {
                    var def = GrowthContent.Get(s.Id);
                    var view = Make(parent, s.Id);
                    Assert.That(view.Kit != null && view.Kit.IsDiorama && view.Kit.DioramaSpecies.Id == s.Id, Is.True, s.Id + " is its kit model");
                    float L = GrowthCreatureView.LengthOf(def.Size) * (def.Kind == SpeciesKind.Crown ? GrowthCreatureView.CrownLength : 1);
                    Assert.That(view.Length, Is.EqualTo(L).Within(1e-5f), s.Id + " at its size class");
                    // The kit body (nose to tail root) is the class's length; fins, sails and tails reach beyond it.
                    var body = view.Kit.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Body");
                    var bodyBounds = BoundsIn(view.transform, body.transform);
                    Assert.That(bodyBounds.size.z, Is.EqualTo(L).Within(L * .02f), s.Id + " body length");
                    // It faces the way it swims (+Z in the greybox; the kit faces -Z): its lips ahead of its body's middle.
                    float lips = view.transform.InverseTransformPoint(view.Kit.DioramaLips.position).z;
                    Assert.That(lips, Is.GreaterThan(bodyBounds.center.z + L * .3f), s.Id + " faces +Z");
                    var parts = view.Kit.GetComponentsInChildren<Transform>(true).Select(p => p.name).ToArray();
                    Assert.That(parts, Is.SupersetOf(new[] { "Body", "Lids", "Lips", "Tail pivot", "Left fin pivot", "Right fin pivot", "Crest pivot" }), s.Id + " on its family rig");
                    Assert.That(view.GetComponentsInChildren<Renderer>(true).All(r => r.shadowCastingMode == ShadowCastingMode.Off), Is.True, s.Id + " casts no shadow (GR.5's budget)");
                    // Its reach and height cover every part at rest, so the swim keeps it inside the glass.
                    var all = BoundsIn(view.transform, view.Kit.transform);
                    Assert.That(view.Reach + 1e-4f, Is.GreaterThanOrEqualTo(Mathf.Max(Mathf.Abs(all.min.x), all.max.x, Mathf.Abs(all.min.z), all.max.z) * .999f), s.Id + " reach");
                    Assert.That(view.HalfHeight + 1e-4f, Is.GreaterThanOrEqualTo(Mathf.Max(Mathf.Abs(all.min.y), all.max.y) * .999f), s.Id + " height");
                    Assert.That(view.Reach, Is.InRange(.45f * L, 1.6f * L), s.Id + " reach for its length");
                    Object.DestroyImmediate(view.gameObject);
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void ASpecimensColourComesFromTheAtlasAndCardsMatchIt()
        {
            var parent = new GameObject("GR-12 colours").transform;
            try
            {
                foreach (var id in new[] { "puddlepup", "starglimmer", "zephyray", "pondlet" })
                {
                    DioramaSpecies.TryGet(id, out var s);
                    var def = GrowthContent.Get(id);
                    var variants = new[] { (Colour.Classic, DioramaVariant.Classic), (Colour.Sunny, DioramaVariant.Sunny), (Colour.Deep, DioramaVariant.Deep), (Colour.Shimmer, DioramaVariant.Shimmer) };
                    foreach (var (colour, variant) in variants)
                    {
                        var view = Make(parent, id, colour);
                        var expected = DioramaVariants.Color(s.Index, variant, 0); expected.a = 1;
                        var body = view.Kit.DioramaSlotForValidation(0).GetColor("_BaseColor");
                        Assert.That(Vector4.Distance(body, expected), Is.LessThan(.005f), id + " " + colour + " body");
                        Assert.That(view.Kit.DioramaSlotForValidation(0).GetFloat("_SheenStrength") > 0, Is.EqualTo(colour == Colour.Shimmer), id + " " + colour + " sheen");
                        Assert.That(Vector4.Distance(GrowthCreatureView.ColourOf(def, colour), expected), Is.LessThan(.005f), id + " " + colour + " on cards and in the journal");
                        Object.DestroyImmediate(view.gameObject);
                    }
                    Assert.That(Vector4.Distance(GrowthCreatureView.ColourOf(def), GrowthCreatureView.ColourOf(def, Colour.Classic)), Is.LessThan(.005f), id);
                }
                // The Sparkline's accent glows: the lantern (and the Starglimmer's stars).
                var star = Make(parent, "starglimmer");
                Assert.That(star.Kit.DioramaGlowMaterial, Is.Not.Null, "A Sparkline glows");
                Assert.That(star.Kit.DioramaLanternPoint, Is.Not.Null, "with its lantern's bulb");
                Assert.That(Make(parent, "lilyfin").Kit.DioramaGlowMaterial, Is.Null, "A Brookkin does not");
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void ATrickPlaysAfterALingerInPeekNeverWithReducedMotion()
        {
            var parent = new GameObject("GR-12 tricks").transform;
            try
            {
                foreach (var id in new[] { "pondlet", "splashkit", "fizzlet", "gustgill", "brookmonarch", "aurorafin", "zephyray" })
                {
                    var view = Make(parent, id);
                    int played = GrowthCreatureView.TricksPlayed, bursts = GrowthCreatureView.BurstsReleased;
                    float t = 0, dt = 1 / 30f;
                    view.Tick(t);
                    for (; t < 2; t += dt) view.Tick(t);
                    Assert.That(GrowthCreatureView.TricksPlayed, Is.EqualTo(played), id + " no trick while just swimming");
                    view.Highlighted = true;
                    float started = -1;
                    for (float end = t + GrowthCreatureView.TrickLinger + .2f; t < end; t += dt)
                    {
                        view.Tick(t);
                        if (started < 0 && view.TrickAge >= 0) started = t;
                    }
                    Assert.That(GrowthCreatureView.TricksPlayed, Is.EqualTo(played + 1), id + " plays its trick after a linger in peek");
                    for (float end = t + DioramaTricks.Duration(id) + .2f; t < end; t += dt) view.Tick(t);
                    Assert.That(view.TrickAge, Is.EqualTo(-1), id + " and the trick ends");
                    Assert.That(GrowthCreatureView.BurstsReleased - bursts, Is.EqualTo(DioramaTricks.Releases(id)), id + " bursts on its beats");
                    Assert.That(parent.GetComponentsInChildren<GrowthCreatureBurst>(true).Length, Is.GreaterThan(0), id + " the bursts are in the tank");
                    for (float end = t + 2; t < end; t += dt) view.Tick(t);
                    Assert.That(GrowthCreatureView.TricksPlayed, Is.EqualTo(played + 1), id + " then rests before another");
                    // Reduced motion: no trick however long the linger, no habit, the pose at rest.
                    var calm = Make(parent, id, Colour.Classic, 9);
                    calm.Highlighted = true;
                    int before = GrowthCreatureView.TricksPlayed;
                    for (float s = 0; s < 4; s += dt) calm.Tick(s, true);
                    Assert.That(GrowthCreatureView.TricksPlayed, Is.EqualTo(before), id + " no trick with Reduced motion");
                    Assert.That(Quaternion.Angle(calm.Kit.transform.localRotation, Quaternion.Euler(0, 180, 0)), Is.LessThan(.01f), id + " at rest with Reduced motion");
                    Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(calm.gameObject);
                    foreach (var burst in parent.GetComponentsInChildren<GrowthCreatureBurst>(true)) Object.DestroyImmediate(burst.gameObject);
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void AtADistanceACreatureIsOneSharedMeshOfItsSpeciesAndColour()
        {
            // GR.5's update levels: further than GrowthWorld.KitDistance (with its ink outline), and beyond 6 m or out of view
            // (without), a kit creature is its species in its colour baked into one mesh, shared by every specimen of it, in one
            // renderer; near and in view, the full kit again.
            var parent = new GameObject("GR-12 far").transform;
            try
            {
                foreach (var id in new[] { "puddlepup", "aurorafin", "zephyray" })
                {
                    DioramaSpecies.TryGet(id, out var s);
                    var a = Make(parent, id, Colour.Deep, 11); var b = Make(parent, id, Colour.Deep, 12); var c = Make(parent, id, Colour.Sunny, 13);
                    var near = BoundsIn(a.transform, a.Kit.transform);
                    foreach (var v in new[] { a, b, c }) v.SetDetail(1);
                    Assert.That(a.Detail == 1 && !a.Kit.gameObject.activeInHierarchy && a.FarRenderer != null, Is.True, id + " shows its distant look");
                    var mesh = a.FarRenderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh, Is.SameAs(b.FarRenderer.GetComponent<MeshFilter>().sharedMesh), id + " one mesh for every specimen of it in a colour");
                    Assert.That(a.FarRenderer.sharedMaterials, Is.EqualTo(b.FarRenderer.sharedMaterials), id + " and the same materials");
                    Assert.That(c.FarRenderer.sharedMaterials[0], Is.Not.SameAs(a.FarRenderer.sharedMaterials[0]), id + " another colour, other materials");
                    Assert.That(a.GetComponentsInChildren<Renderer>().Count(r => r.name != "Reserved tag"), Is.EqualTo(1), id + " in one renderer");
                    Assert.That(a.FarRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), id + " without a shadow");
                    Assert.That(a.FarRenderer.sharedMaterials.Length, Is.EqualTo(mesh.subMeshCount).And.LessThanOrEqualTo(10), id + " one draw a colour slot");
                    var farBounds = BoundsIn(a.transform, a.FarRenderer.transform);
                    Assert.That(Vector3.Distance(farBounds.size, near.size), Is.LessThan(near.size.magnitude * .05f), id + " the same size and shape as near");
                    Assert.That(Vector3.Distance(farBounds.center, near.center), Is.LessThan(near.size.magnitude * .05f), id + " in the same place");
                    var body = a.FarRenderer.sharedMaterials.First(m => m.name.Contains(DioramaVariants.SlotName(id, 0)));
                    var deep = DioramaVariants.Color(s.Index, DioramaVariant.Deep, 0); deep.a = 1;
                    Assert.That(Vector4.Distance(body.GetColor("_BaseColor"), deep), Is.LessThan(.005f), id + " in its colour");
                    Assert.That(a.FarRenderer.sharedMaterials.First(m => m.name.Contains(DioramaVariants.SlotName(id, 0))).GetShaderPassEnabled(DioramaLook.OutlinePass), Is.True, id + " with its ink outline at the middle distance");
                    a.SetDetail(2);
                    Assert.That(a.FarRenderer != null && !a.Kit.gameObject.activeInHierarchy && a.FarRenderer.GetComponent<MeshFilter>().sharedMesh == mesh, Is.True, id + " far away, still its distant look");
                    Assert.That(a.FarRenderer.sharedMaterials.Any(m => m.GetShaderPassEnabled(DioramaLook.OutlinePass)), Is.False, id + " without its outline beyond 6 m");
                    Assert.That(GrowthWorld.KitDistance, Is.InRange(2.5f, GrowthWorld.FullDistance), "The full rig wherever peek can reach");
                    a.SetDetail(0);
                    Assert.That(a.Kit.gameObject.activeInHierarchy && a.FarRenderer == null, Is.True, id + " near again: the full kit creature");
                    foreach (var v in new[] { a, b, c }) Object.DestroyImmediate(v.gameObject);
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void CrownsAreGranderAndOtherFamiliesKeepTheirPlaceholders()
        {
            var parent = new GameObject("GR-12 crowns").transform;
            try
            {
                float Room(GrowthCreatureView v) { var b = BoundsIn(v.transform, v.Kit.transform); return b.size.x * b.size.y * b.size.z; }
                foreach (var family in DioramaSpecies.All.GroupBy(s => s.Family))
                {
                    var crown = family.Single(s => s.Crown);
                    var crownView = Make(parent, crown.Id);
                    Assert.That(crownView.Length, Is.EqualTo(GrowthCreatureView.LengthOf(GrowthContent.Get(crown.Id).Size) * GrowthCreatureView.CrownLength).Within(1e-5f),
                        crown.Id + " at the top of its size class");
                    float room = Room(crownView);
                    foreach (var other in family.Where(s => !s.Crown))
                    {
                        var view = Make(parent, other.Id);
                        Assert.That(room, Is.GreaterThan(Room(view)), crown.Id + " takes more room than the " + other.Id);
                        Object.DestroyImmediate(view.gameObject);
                    }
                }
                // Families GR-12 has not reached yet keep GR-03's placeholders and their swim room.
                foreach (var def in GrowthContent.Species.Where(s => !DioramaSpecies.TryGet(s.Id, out _)).GroupBy(s => s.Family).Select(g => g.First()))
                {
                    var view = Make(parent, def.Id);
                    Assert.That(view.Kit, Is.Null, def.Id + " a placeholder");
                    Assert.That(view.Reach, Is.EqualTo(.6f * view.Length).Within(1e-5f)); Assert.That(view.HalfHeight, Is.EqualTo(.4f * view.Length).Within(1e-5f));
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }
    }
}
