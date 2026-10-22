using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>AD-D rooms and effects: a world material taking the look in place and coming back exactly, the room style, the
    /// culture pipes' routes, the slimmer siphon, the checkout hop, the slapped visitor's spinning hat and the moments' sounds.</summary>
    public sealed class DioramaRoomsTests
    {
        static Material Lit(string name, Color color, bool transparent = false, Color? emission = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            if (transparent) { m.SetFloat("_Surface", 1); m.renderQueue = 3000; }
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            return m;
        }

        static bool Near(Color a, Color b, float tolerance = .0001f) =>
            Mathf.Abs(a.r - b.r) < tolerance && Mathf.Abs(a.g - b.g) < tolerance && Mathf.Abs(a.b - b.b) < tolerance && Mathf.Abs(a.a - b.a) < tolerance;

        [Test] public void AClassicMaterialTakesTheToonLookAndComesBackExactly()
        {
            var m = Lit("Warm porcelain", new Color(.3f, .6f, .8f, 1), emission: new Color(.2f, .1f, .05f, 1));
            var shader = m.shader; int queue = m.renderQueue; var keywords = m.shaderKeywords.OrderBy(k => k).ToArray();
            Color colour = m.color, baseColour = m.GetColor("_BaseColor"), emission = m.GetColor("_EmissionColor"), legacy = m.GetColor("_Color");
            var record = DioramaRoomRules.Convert(m, false);
            Assert.That(m.shader.name, Is.EqualTo(DioramaLook.ToonShaderName));
            Assert.That(Near(m.color, colour), "Game code reading Material.color reads the classic colour / " + m.color);
            Assert.That(Near(m.GetColor("_BaseColor"), baseColour), "The toon keeps the classic colour");
            Assert.That(Near(m.GetColor("_EmissionColor"), emission * 2.2f), "Real emission is lifted for the bloom");
            Assert.That(m.GetShaderPassEnabled(DioramaLook.OutlinePass), Is.False, "A room or prop is never outlined");
            DioramaRoomRules.Restore(record);
            Assert.That(m.shader, Is.EqualTo(shader));
            Assert.That(m.renderQueue, Is.EqualTo(queue));
            Assert.That(m.shaderKeywords.OrderBy(k => k).ToArray(), Is.EqualTo(keywords));
            Assert.That(Near(m.GetColor("_BaseColor"), baseColour) && Near(m.GetColor("_EmissionColor"), emission) && Near(m.GetColor("_Color"), legacy),
                "Every colour comes back exactly");
            Object.DestroyImmediate(m);
        }

        [Test] public void GlassAndWaterBecomeTheLooksGlassAndWaterWobbles()
        {
            var water = Lit("Aquatic water tint", new Color(.17f, .73f, .84f, .085f), true);
            var glass = Lit("Clear aquarium glass", new Color(.61f, .91f, .95f, .6f), true);
            var w = DioramaRoomRules.Convert(water, false); var g = DioramaRoomRules.Convert(glass, false);
            Assert.That(water.shader, Is.EqualTo(DioramaLook.GlassShader)); Assert.That(glass.shader, Is.EqualTo(DioramaLook.GlassShader));
            Assert.That(water.renderQueue, Is.GreaterThanOrEqualTo(3000));
            Assert.That(water.GetColor("_BaseColor").a, Is.InRange(.05f, .3f)); Assert.That(glass.GetColor("_BaseColor").a, Is.InRange(.05f, .2f));
            Assert.That(water.GetFloat("_WobbleAmount"), Is.GreaterThan(0), "Water's surface wobbles");
            Assert.That(glass.GetFloat("_WobbleAmount"), Is.EqualTo(0), "Glass does not");
            DioramaRoomRules.Restore(w); DioramaRoomRules.Restore(g);
            Assert.That(water.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Object.DestroyImmediate(water); Object.DestroyImmediate(glass);
        }

        [Test] public void PlacementGhostsAndNonLitMaterialsAreLeftAlone()
        {
            var ghost = Lit("Placement valid ghost", Color.green);
            var toon = DioramaLook.Toon("already the look", Color.white);
            Assert.That(DioramaRoomRules.SkipsMaterial(ghost), "The placement ghosts keep their colours");
            Assert.That(DioramaRoomRules.SkipsMaterial(toon), "A look material is not converted again");
            Assert.That(DioramaRoomRules.SkipsMaterial(Lit("Warm porcelain", Color.white)), Is.False);
            Object.DestroyImmediate(ghost); Object.DestroyImmediate(toon);
        }

        [Test] public void OnlySurfacesSpanningARoomTakeTheRoomStyle()
        {
            Assert.That(DioramaRoomRules.RoomStyle(new Bounds(Vector3.zero, new Vector3(4, 4.2f, .3f))), "A wall");
            Assert.That(DioramaRoomRules.RoomStyle(new Bounds(Vector3.zero, new Vector3(14, .2f, 4))), "A floor or ceiling");
            Assert.That(DioramaRoomRules.RoomStyle(new Bounds(Vector3.zero, new Vector3(1.2f, .9f, .6f))), Is.False, "A table");
            Assert.That(DioramaRoomRules.RoomStyle(new Bounds(Vector3.zero, new Vector3(3, .1f, .1f))), Is.False, "A long rail");
        }

        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Sampled closest approach of two straight segments (fine enough for the pipes' 1 cm budget).
            float best = float.MaxValue;
            for (int i = 0; i <= 60; i++) for (int j = 0; j <= 60; j++)
                    best = Mathf.Min(best, Vector3.Distance(Vector3.Lerp(a, b, i / 60f), Vector3.Lerp(c, d, j / 60f)));
            return best;
        }

        [Test] public void EachCultureRisesRunsAcrossOnItsOwnLaneAndDropsIntoTheIncubator()
        {
            var inlet = new Vector3(2f, 2.6f, 3.85f);
            var sources = new Dictionary<CultureKind, Vector3>
            {
                { CultureKind.Bloom, new Vector3(-4.2f, 2.3f, 3.85f) }, { CultureKind.Flow, new Vector3(-1.4f, 2.3f, 3.85f) },
                { CultureKind.Spark, new Vector3(-5.5f, 2.2f, 1.9f) }, { CultureKind.Mist, new Vector3(4f, 1.6f, 7.1f) },
            };
            var routes = sources.ToDictionary(p => p.Key, p => DioramaRoomRules.PipeRoute(p.Value, inlet, p.Key));
            foreach (var (kind, route) in routes.Select(p => (p.Key, p.Value)))
            {
                Assert.That(route[0], Is.EqualTo(sources[kind]), "Starts on top of where it grows / " + kind);
                var end = route[route.Length - 1];
                Assert.That(Mathf.Abs(end.y - inlet.y) < 1e-4f && Mathf.Abs(end.z - inlet.z) < 1e-4f, "Ends at the inlet / " + kind);
                Assert.That(Mathf.Abs(end.x - inlet.x), Is.LessThanOrEqualTo(1.5f * DioramaRoomRules.PipeInletStep + 1e-4f), "Beside the others / " + kind);
                for (int i = 1; i < route.Length - 1; i++)
                    Assert.That(route[i].y, Is.EqualTo(DioramaRoomRules.PipeLane(kind)).Within(1e-4f), "Every bend on its own lane / " + kind);
                for (int i = 0; i + 1 < route.Length; i++)
                {
                    var d = route[i + 1] - route[i];
                    int moving = (Mathf.Abs(d.x) > 1e-4f ? 1 : 0) + (Mathf.Abs(d.y) > 1e-4f ? 1 : 0) + (Mathf.Abs(d.z) > 1e-4f ? 1 : 0);
                    Assert.That(moving, Is.EqualTo(1), "Runs straight along one axis at a time / " + kind + " / " + i);
                }
                Assert.That(route.Max(p => p.y) + DioramaRoomRules.PipeRadius, Is.LessThan(3.9f), "Under the ceiling lights / " + kind);
            }
            var lanes = routes.Keys.Select(DioramaRoomRules.PipeLane).ToList();
            Assert.That(lanes.Distinct().Count(), Is.EqualTo(4), "Four lanes");
            // No two pipes touch away from the incubator's top (the last drop of each is beside the others').
            var kinds = routes.Keys.ToArray();
            for (int a = 0; a < kinds.Length; a++)
                for (int b = a + 1; b < kinds.Length; b++)
                {
                    var ra = routes[kinds[a]]; var rb = routes[kinds[b]];
                    for (int i = 0; i + 1 < ra.Length; i++)
                        for (int j = 0; j + 1 < rb.Length; j++)
                            Assert.That(SegmentDistance(ra[i], ra[i + 1], rb[j], rb[j + 1]), Is.GreaterThan(2 * DioramaRoomRules.PipeRadius + .005f),
                                "Pipes keep apart / " + kinds[a] + " " + i + " / " + kinds[b] + " " + j);
                }
        }

        [Test] public void TheSiphonIsSlimmerByAboutAThird()
        {
            float scale = DioramaRoomRules.DioramaToolScale / DioramaRoomRules.ClassicToolScale;
            Assert.That(scale * scale, Is.InRange(.5f, .7f), "Its size against the classic one");
            Assert.That(DioramaRoomRules.DioramaChamberShape.x, Is.LessThan(1), "Its chamber is slimmer");
        }

        [Test] public void TheCheckoutHopIsOneGentleHop()
        {
            Assert.That(CustomerActor.DioramaHop(0, false), Is.EqualTo(0));
            Assert.That(CustomerActor.DioramaHop(CustomerActor.DioramaHopStart - .01f, false), Is.EqualTo(0));
            Assert.That(CustomerActor.DioramaHop(CustomerActor.DioramaHopEnd + .01f, false), Is.EqualTo(0));
            float middle = (CustomerActor.DioramaHopStart + CustomerActor.DioramaHopEnd) * .5f;
            Assert.That(CustomerActor.DioramaHop(middle, false), Is.EqualTo(CustomerActor.DioramaHopHeight).Within(1e-4f));
            Assert.That(CustomerActor.DioramaHop(middle, true), Is.EqualTo(0), "Reduced motion: no hop");
            Assert.That(CustomerActor.DioramaHopEnd, Is.LessThanOrEqualTo(FxCustomerMotion.ReactionDuration(FxCustomerReaction.Checkout)), "Within the checkout reaction");
        }

        [Test] public void ASlappedVisitorsHatSpinsAndSettlesBack()
        {
            foreach (var role in new[] { "casual", "bargain", "collector", "premium" })
            {
                var go = new GameObject("hat test " + role);
                try
                {
                    var figure = go.AddComponent<ShopCharacterVisual>();
                    Assert.That(figure.BuildDioramaRole(role), role);
                    Assert.That(figure.HasDioramaHat, "Every customer type wears a hat / " + role);
                    var head = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head").GetComponent<Renderer>();
                    var before = head.sharedMaterials;
                    figure.PoseDioramaHat(ShopCharacterVisual.DioramaHatSpinSeconds * .5f);
                    Assert.That(figure.DioramaHatSpinning && figure.DioramaHatTurn > 180, "It spins / " + role);
                    var spinning = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Diorama spinning hat");
                    Assert.That(spinning.gameObject.activeSelf && spinning.localPosition.y > .05f, "It pops up off the head / " + role);
                    Assert.That(head.sharedMaterials.Count(m => m && m.name.Contains("hidden hat slot")), Is.GreaterThan(0), "The head's own hat is hidden meanwhile / " + role);
                    figure.PoseDioramaHat(ShopCharacterVisual.DioramaHatSpinSeconds + .01f);
                    Assert.That(figure.DioramaHatSpinning, Is.False, role);
                    Assert.That(head.sharedMaterials, Is.EqualTo(before), "The head shows its hat again / " + role);
                    Assert.That(spinning.gameObject.activeSelf, Is.False, role);
                }
                finally { Object.DestroyImmediate(go); }
            }
            var crew = new GameObject("hat test crew");
            try
            {
                var figure = crew.AddComponent<ShopCharacterVisual>();
                Assert.That(figure.BuildDioramaRole("cashier"));
                Assert.That(figure.HasDioramaHat, Is.False, "The crew wear no hat");
                figure.PoseDioramaHat(.3f);
                Assert.That(figure.DioramaHatSpinning, Is.False);
            }
            finally { Object.DestroyImmediate(crew); }
        }

        [Test] public void TheMomentsSoundsAreShortSoftAndOriginal()
        {
            foreach (ShopWorld.DioramaSound sound in System.Enum.GetValues(typeof(ShopWorld.DioramaSound)))
            {
                var clip = ShopAudio.MakeDioramaClip(sound);
                try
                {
                    Assert.That(clip.length, Is.InRange(.1f, .5f), sound.ToString());
                    var data = new float[clip.samples]; clip.GetData(data, 0);
                    float peak = data.Max(Mathf.Abs), rms = Mathf.Sqrt(data.Average(v => v * v));
                    Assert.That(peak, Is.LessThan(.5f), "Soft / " + sound);
                    Assert.That(rms, Is.GreaterThan(.002f), "Audible / " + sound);
                    Assert.That(Mathf.Abs(data[data.Length - 1]), Is.LessThan(.02f), "Ends without a click / " + sound);
                }
                finally { Object.DestroyImmediate(clip); }
            }
        }
    }
}
