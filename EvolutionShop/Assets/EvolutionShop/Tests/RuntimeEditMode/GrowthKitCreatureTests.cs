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
                    // Its swim room leaves room for its crest at its fullest (a Cinderling's spines, a Reefback's corals), since a
                    // trick turns it round on the spot.
                    var crestPivot = view.Kit.GetComponentsInChildren<Transform>(true).First(x => x.name == "Crest pivot");
                    var crestRest = crestPivot.localScale;
                    crestPivot.localScale = crestRest * (1 + CreatureVisual.CrestFlareGrowth * DioramaTricks.MaxFlare(s.Id));
                    var flared = BoundsIn(view.transform, view.Kit.transform);
                    crestPivot.localScale = crestRest;
                    Assert.That(view.Reach + 1e-4f, Is.GreaterThanOrEqualTo(Mathf.Max(Mathf.Abs(flared.min.x), flared.max.x, Mathf.Abs(flared.min.z), flared.max.z) * .999f), s.Id + " reach with its crest at its fullest");
                    Assert.That(view.HalfHeight + 1e-4f, Is.GreaterThanOrEqualTo(Mathf.Max(Mathf.Abs(flared.min.y), flared.max.y) * .999f), s.Id + " height with its crest at its fullest");
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
                foreach (var id in new[] { "puddlepup", "starglimmer", "zephyray", "pondlet", "kindlekoi", "rainbowregent", "coralossus", "seaspark" })
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
                Assert.That(Make(parent, "coalbelly").Kit.DioramaGlowMaterial, Is.Not.Null, "A Cinderling's embers glow (its spines' tips, its coal belly)");
                Assert.That(Make(parent, "haloling").Kit.DioramaGlowMaterial, Is.Null, "A Dewling does not");
                Assert.That(Make(parent, "tidepip").Kit.DioramaGlowMaterial, Is.Not.Null, "A Saltscale's salt stripe glitters");
                Assert.That(Make(parent, "clamjaw").Kit.DioramaGlowMaterial, Is.Null, "A Reefback does not glow");
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void ATrickPlaysAfterALingerInPeekNeverWithReducedMotion()
        {
            var parent = new GameObject("GR-12 tricks").transform;
            try
            {
                foreach (var id in new[] { "pondlet", "splashkit", "fizzlet", "gustgill", "brookmonarch", "aurorafin", "zephyray", "sootsnap", "fogfin", "phoenixfry", "rainbowregent", "kelpgrazer", "surfskipper", "coralossus", "pearlemperor" })
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

        [Test] public void ACinderlingsSpinesFlareAndItsSparksAndADewlingsDropletsFlyFromTheCrest()
        {
            var parent = new GameObject("GR-12 crests").transform;
            try
            {
                foreach (var id in new[] { "emberpuff", "phoenixfry", "pearlpip", "rainbowregent", "barnaclebrow", "coralossus", "foamtail", "pearlemperor" })
                {
                    var view = Make(parent, id);
                    var crest = view.Kit.GetComponentsInChildren<Transform>(true).First(t => t.name == "Crest pivot");
                    var rest = crest.localScale;
                    Assert.That(view.Kit.DioramaCrestPoint, Is.Not.Null, id + " has the top of its crest");
                    float t = 0, dt = 1 / 60f, widest = 0;
                    view.Tick(t);
                    view.Highlighted = true;
                    int bursts = view.Bursts;
                    string family = GrowthContent.Get(id).Family;
                    // Sparks, droplets and plankton fly from the top of the crest; a Saltscale's salt from its body, along its stripe.
                    Transform Origin() => family == "saltscale" ? view.Kit.transform : view.Kit.DioramaCrestPoint;
                    var crestAtBurst = new List<(Vector3 crest, Vector3 lips)>();
                    while (view.TrickAge < 0 && t < 3) view.Tick(t += dt);
                    for (float end = t + DioramaTricks.Duration(id); t < end; t += dt)
                    {
                        int was = view.Bursts;
                        view.Tick(t);
                        widest = Mathf.Max(widest, crest.localScale.y / rest.y);
                        if (view.Bursts > was) crestAtBurst.Add((Origin().position, view.Kit.DioramaLips.position));
                    }
                    bool flares = family == "cinderling" || family == "reefback", glows = family != "dewling";
                    if (flares) Assert.That(widest, Is.GreaterThan(1.25f), id + " its spines flare, or its corals bloom, during its trick");
                    else Assert.That(widest, Is.EqualTo(1).Within(1e-4f), id + " a Dewling's beads and a Saltscale's sail never stand taller");
                    Assert.That(view.Bursts - bursts, Is.EqualTo(DioramaTricks.Releases(id)), id + " bursts on its beats");
                    var spawned = parent.GetComponentsInChildren<GrowthCreatureBurst>(true);
                    Assert.That(spawned.Length, Is.EqualTo(crestAtBurst.Count), id);
                    for (int i = 0; i < spawned.Length; i++)
                        Assert.That(Vector3.Distance(spawned[i].transform.position, crestAtBurst[i].crest), Is.LessThan(Vector3.Distance(spawned[i].transform.position, crestAtBurst[i].lips)),
                            id + " its burst flies from " + (family == "saltscale" ? "its body" : "the top of its crest"));
                    var piece = spawned[0].GetComponentInChildren<Renderer>();
                    Assert.That(piece.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.EqualTo(glows), id + (glows ? " its sparks, plankton or salt glitter" : " its droplets do not glow"));
                    // Reduced motion: the spines never flare.
                    var calm = Make(parent, id, Colour.Classic, 9);
                    var calmCrest = calm.Kit.GetComponentsInChildren<Transform>(true).First(x => x.name == "Crest pivot");
                    calm.Highlighted = true;
                    for (float s = 0; s < 4; s += dt) { calm.Tick(s, true); Assert.That(calmCrest.localScale, Is.EqualTo(rest), id + " calm with Reduced motion"); }
                    Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(calm.gameObject);
                    foreach (var burst in parent.GetComponentsInChildren<GrowthCreatureBurst>(true)) Object.DestroyImmediate(burst.gameObject);
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void PeekEnlargesACreatureOnlyAsFarAsItsWaterAllowsThroughItsTrick()
        {
            // GR-02's peek enlarges the creature under the reticle 1.35 times. Its reach and height through its trick are sampled
            // (TrickReach, TrickHalf) and cover what it really does; in a Large tank's water a large crown is enlarged less, so its
            // trick at its widest stays inside, while a small creature keeps the full 1.35. Every kit species' trick, at its peek
            // size and swimming as far as its tank lets it (at least 1 cm up and down, 2 cm across), stays inside the water of the
            // water of every container it can live in (a lab rack's too: it holds large creatures).
            var parent = new GameObject("GR-12 peek").transform;
            var large = GrowthWorld.WaterOf("large");
            Assert.That(large, Is.EqualTo(new Vector3(2.44f, 0.63f, 0.94f)), "a Large tank's water");
            try
            {
                foreach (var kind in DioramaSpecies.All)
                {
                    string id = kind.Id;
                    var homes = GrowthContent.Containers.Where(c => (int)GrowthContent.Get(id).Size <= (int)c.Largest).ToArray();
                    Assert.That(homes.Length, Is.GreaterThan(0), id + " has a container");
                    foreach (var home in homes)
                    {
                        var water = GrowthWorld.WaterOf(home.Id);
                        var fit = Make(parent, id);
                        fit.FitTo(water);
                        Assert.That(fit.PeekScale, Is.InRange(1f, GrowthCreatureView.PeekGrowth), id + " in a " + home.Id);
                        Assert.That(fit.TrickHalf * fit.PeekScale, Is.LessThanOrEqualTo(water.y * .5f - .01f + 1e-4f), id + " its trick fits a " + home.Id + "'s water's height at its peek size");
                        Assert.That(fit.TrickReach * fit.PeekScale, Is.LessThanOrEqualTo(Mathf.Min(water.x, water.z) * .5f - .02f + 1e-4f), id + " and its depth");
                        Object.DestroyImmediate(fit.gameObject);
                    }
                    var view = Make(parent, id);
                    view.FitTo(large);
                    view.Highlighted = true;
                    Assert.That(view.transform.localScale.x, Is.EqualTo(view.PeekScale).Within(1e-5f), id + " peek enlarges it that far");
                    // The sampled reach and height cover its real trick, measured on its parts as the route does.
                    float far = 0, high = 0, t = 0, dt = 1 / 60f;
                    view.Tick(t);
                    for (; t < GrowthCreatureView.TrickLinger + DioramaTricks.Duration(id) + .2f; t += dt)
                    {
                        view.Tick(t);
                        var m = view.transform.worldToLocalMatrix;
                        foreach (var r in view.Kit.GetComponentsInChildren<Renderer>(true))
                        {
                            var b = r is SkinnedMeshRenderer s ? s.localBounds : r.GetComponent<MeshFilter>() is MeshFilter f && f.sharedMesh ? f.sharedMesh.bounds : r.localBounds;
                            var to = m * r.transform.localToWorldMatrix;
                            for (int k = 0; k < 8; k++)
                            {
                                var p = to.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                                far = Mathf.Max(far, new Vector2(p.x, p.z).magnitude); high = Mathf.Max(high, Mathf.Abs(p.y));
                            }
                        }
                    }
                    Assert.That(GrowthCreatureView.TricksPlayed > 0 && view.TrickAge < 0, Is.True, id + " played its trick");
                    Assert.That(far, Is.LessThanOrEqualTo(view.TrickReach), id + " its trick's reach");
                    Assert.That(high, Is.LessThanOrEqualTo(view.TrickHalf), id + " its trick's height");
                }
                var crown = Make(parent, "coralossus", Colour.Classic, 21); crown.FitTo(large);
                var small = Make(parent, "puddlepup", Colour.Classic, 22); small.FitTo(large);
                Assert.That(crown.PeekScale, Is.LessThan(GrowthCreatureView.PeekGrowth), "Coralossus is enlarged less in a Large tank");
                Assert.That(small.PeekScale, Is.EqualTo(GrowthCreatureView.PeekGrowth), "a Puddlepup the full 1.35");
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void ItsSwimRoomCoversItsTrickWithItsHabitOnTop()
        {
            // A trick plays on top of the creature's habit (a Reefback's nod under its bloom's bow, a Dewling's bob under its
            // sprinkle): posed through every moment of both together, its kit model and crest set as the game sets them, every part
            // stays within its sampled TrickReach and TrickHalf.
            var parent = new GameObject("GR-12 habit").transform;
            try
            {
                foreach (var id in new[] { "coralossus", "shellback", "kelpgrazer", "dewdrop", "phoenixfry", "emberpuff", "zephyray", "brinefin", "pondlet" })
                {
                    var view = Make(parent, id);
                    var holder = view.Kit.transform;
                    var crest = holder.GetComponentsInChildren<Transform>(true).First(x => x.name == "Crest pivot");
                    var crestRest = crest.localScale;
                    var renderers = view.Kit.GetComponentsInChildren<Renderer>(true);
                    float d = DioramaTricks.Duration(id), far = 0, high = 0;
                    for (int i = 0; i <= 30; i++)
                    {
                        var trick = id == "pondlet" ? DioramaTricks.PondletBubble(d * i / 30f) : DioramaTricks.Trick(id, d * i / 30f);
                        for (float t = 0; t < 12; t += .2f)
                        {
                            var idle = DioramaTricks.Idle(id, t, 0);
                            holder.localRotation = GrowthCreatureView.KitTurn * Quaternion.Euler(idle.Pitch + trick.Pitch, idle.Yaw + trick.Yaw, idle.Roll + trick.Roll);
                            holder.localPosition = Vector3.up * ((idle.Lift + trick.Lift) * view.Length * GrowthCreatureView.LiftShare);
                            crest.localScale = crestRest * (1 + CreatureVisual.CrestFlareGrowth * Mathf.Clamp(idle.Flare + trick.Flare, 0, 1.5f));
                            var m = view.transform.worldToLocalMatrix;
                            foreach (var r in renderers)
                            {
                                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                                if (!mesh) continue;
                                var to = m * r.transform.localToWorldMatrix;
                                for (int k = 0; k < 8; k++)
                                {
                                    var p = to.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(mesh.bounds.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                                    far = Mathf.Max(far, new Vector2(p.x, p.z).magnitude); high = Mathf.Max(high, Mathf.Abs(p.y));
                                }
                            }
                        }
                    }
                    Assert.That(far, Is.LessThanOrEqualTo(view.TrickReach / 1.04f), id + " its reach through its trick and habit together");
                    Assert.That(high, Is.LessThanOrEqualTo(view.TrickHalf / 1.04f), id + " its height through its trick and habit together");
                }
            }
            finally { Object.DestroyImmediate(parent.gameObject); }
        }

        [Test] public void ARestAfterATrickRunsOnTheShopsClockEvenWhileItIsFarAway()
        {
            // A trick's 6 s rest runs on the shop's clock: a creature whose tank was far away meanwhile (its distant look, not
            // animated) is rested when the player comes back, and plays its trick again after the usual linger.
            var parent = new GameObject("GR-12 rest").transform;
            try
            {
                var view = Make(parent, "brinefin");
                float t = 0, dt = 1 / 30f;
                view.Tick(t);
                view.Highlighted = true;
                for (float end = t + GrowthCreatureView.TrickLinger + DioramaTricks.Duration("brinefin") + .5f; t < end; t += dt) view.Tick(t);
                Assert.That(view.TrickAge, Is.EqualTo(-1), "its trick has played");
                int played = GrowthCreatureView.TricksPlayed;
                view.Highlighted = false;
                view.SetDetail(1); t += 10; view.SetDetail(0);
                view.Highlighted = true;
                bool started = false;
                for (float end = t + GrowthCreatureView.TrickLinger + .3f; t < end && !started; t += dt) { view.Tick(t); started = view.TrickAge >= 0; }
                Assert.That(started, Is.True, "Back after 10 s away, lingering on it plays its trick again: its rest is over");
                Assert.That(GrowthCreatureView.TricksPlayed, Is.EqualTo(played + 1));
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
