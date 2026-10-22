using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>AD-D: one world material the look converted in place, with everything needed to put it back exactly.</summary>
    public sealed class DioramaConvertedMaterial
    {
        public Material Material;
        public Shader Shader;
        public int Queue;
        public string[] Keywords;
        public Color Base, Emission, ColorProperty;
        public bool Emissive, OutlinePass, Transparent, HasColorProperty;
    }

    /// <summary>
    /// AD-D rules of the look on every room: converting a classic material to the toon or glass shader in place and back,
    /// which surfaces read as room walls, the culture pipes' routes, and the slimmer siphon's size. Pure functions, so the
    /// EditMode tests read the same rules the world uses.
    /// </summary>
    public static class DioramaRoomRules
    {
        public const float ClassicToolScale = .62f, DioramaToolScale = .46f;
        public static readonly Vector3 DioramaChamberShape = new Vector3(.7f, 1f, .7f);
        public const float PipeRadius = .046f, PipeLaneBottom = 3.30f, PipeLaneStep = .1f, PipeInletStep = .11f, PipeRunStep = .12f;

        public static bool ClassicLit(Material m) =>
            m && m.shader && (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "Standard");

        /// <summary>Materials the look leaves alone: anything not classic lit, and the placement ghosts (their bright valid
        /// and blocked colours).</summary>
        public static bool SkipsMaterial(Material m) => !ClassicLit(m) || m.name.ToLowerInvariant().Contains("placement");

        /// <summary>A surface that spans a room (at least two sides over 2.5 m) takes the room's softer toon style.</summary>
        public static bool RoomStyle(Bounds b) => (b.size.x > 2.5f ? 1 : 0) + (b.size.y > 2.5f ? 1 : 0) + (b.size.z > 2.5f ? 1 : 0) >= 2;

        public static bool Watery(Material m)
        {
            string name = m.name.ToLowerInvariant();
            return name.Contains("water") || name.Contains("fluid");
        }

        /// <summary>The classic tint at a glassier opacity: water keeps more of its colour than clear glass.</summary>
        public static Color GlassTint(Color classic, bool watery)
        {
            var tint = classic; tint.a = Mathf.Clamp(classic.a * (watery ? .7f : .45f), .05f, watery ? .3f : .2f);
            return tint;
        }

        /// <summary>Converts a classic lit material to the look in place, recording what it was.</summary>
        public static DioramaConvertedMaterial Convert(Material m, bool room)
        {
            var c = new DioramaConvertedMaterial
            {
                Material = m, Shader = m.shader, Queue = m.renderQueue, Keywords = m.shaderKeywords,
                Base = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color,
                Emissive = m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor"),
                Emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black,
                OutlinePass = m.GetShaderPassEnabled(DioramaLook.OutlinePass),
                Transparent = m.renderQueue >= 3000 || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f),
                HasColorProperty = m.HasProperty("_Color"), ColorProperty = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white,
            };
            // What Material.color returned under the classic shader (URP Lit reads its main colour, _BaseColor); the
            // look's shaders keep it in _Color, so game code reading it (text tints, labels) reads what it always did.
            var classicColour = m.color;
            if (c.Transparent)
            {
                bool watery = Watery(m);
                m.shader = DioramaLook.GlassShader;
                m.renderQueue = Mathf.Max(3000, c.Queue);
                m.SetColor("_BaseColor", GlassTint(c.Base, watery));
                m.SetFloat("_RimStrength", watery ? .22f : .38f);
                m.SetFloat("_GlossStrength", watery ? .45f : .75f);
                if (watery) m.SetFloat("_WobbleAmount", .008f);
                m.SetColor("_EmissionColor", c.Emissive ? c.Emission : Color.black);
                m.SetColor("_Color", classicColour);
                return c;
            }
            m.shader = DioramaLook.ToonShader;
            m.renderQueue = -1;
            var color = c.Base; color.a = 1;
            m.SetColor("_BaseColor", color);
            DioramaLook.ApplyToonStyle(m, room ? DioramaLook.Style.Room : DioramaLook.Style.Prop);
            // Lit surfaces stay under the bloom threshold (AD-A's soft knee); only real emission blooms.
            m.SetColor("_EmissionColor", c.Emissive ? c.Emission * 2.2f : Color.black);
            m.SetColor("_Color", classicColour);
            return c;
        }

        /// <summary>Puts a converted material back exactly as it was.</summary>
        public static void Restore(DioramaConvertedMaterial c)
        {
            var m = c.Material;
            if (!m) return;
            m.shader = c.Shader;
            m.renderQueue = c.Queue;
            m.shaderKeywords = c.Keywords;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c.Base);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c.Emission);
            if (c.HasColorProperty) m.SetColor("_Color", c.ColorProperty);
            m.SetShaderPassEnabled(DioramaLook.OutlinePass, c.OutlinePass);
        }

        public static float PipeLane(CultureKind kind) => PipeLaneBottom + (int)kind * PipeLaneStep;

        /// <summary>A culture's pipe from the top of where it grows to the incubator's inlet (the world's own coordinates):
        /// up to its own lane, across on a line of its own, and down into the inlet beside the other cultures.</summary>
        public static Vector3[] PipeRoute(Vector3 start, Vector3 inlet, CultureKind kind)
        {
            int k = (int)kind;
            float lane = PipeLane(kind);
            float across = inlet.x + (k - 1.5f) * PipeInletStep, run = inlet.z + (k - 1.5f) * PipeRunStep;
            var points = new System.Collections.Generic.List<Vector3>
            {
                start, new Vector3(start.x, lane, start.z), new Vector3(start.x, lane, run), new Vector3(across, lane, run),
                new Vector3(across, lane, inlet.z), new Vector3(across, inlet.y, inlet.z),
            };
            for (int i = points.Count - 1; i > 0; i--) if (Vector3.Distance(points[i], points[i - 1]) < .01f) points.RemoveAt(i);
            return points.ToArray();
        }
    }
}
