using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// A customer type's face on the price tag (UX.5): its hat tells the type (cap, visor, bowler and glasses, top
    /// hat) and its fill is the type's HUD colour. Happy at a bargain or fair price, a flat mouth when pricey, a frown
    /// when too expensive, and an empty-wallet badge (coral disc with an X) when the price is past their budget.
    /// </summary>
    public sealed class MoodFace : VisualElement
    {
        public const string ClassName = "es-mood";
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        static readonly Color Coral = new Color(0.84f, 0.38f, 0.27f);
        static readonly Color Paper = new Color(1f, 0.963f, 0.874f);
        static readonly Color HatDark = new Color(0.12f, 0.12f, 0.14f);
        static readonly Color CapNavy = new Color(0.11f, 0.17f, 0.2f);
        static readonly Color Visor = new Color(0.95f, 0.76f, 0.19f);
        BuyerMood mood = BuyerMood.Happy;
        string archetype = "";
        Color fill = Color.white;

        public BuyerMood Mood => mood;
        public string Archetype => archetype;

        public MoodFace()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(BuyerMood value, string archetypeId, Color face)
        {
            archetypeId = archetypeId ?? "";
            if (value == mood && face == fill && archetypeId == archetype) return;
            mood = value; fill = face; archetype = archetypeId;
            MarkDirtyRepaint();
        }

        void Circle(Painter2D p, Vector2 at, float radius)
        {
            p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath();
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            float radius = Mathf.Min(r.width, r.height) * 0.36f;
            // The face sits low in its box so the hat has room above it.
            Vector2 c = new Vector2(r.center.x, r.yMax - radius - 2f);
            var p = context.painter2D;
            p.fillColor = fill;
            Circle(p, c, radius); p.Fill();
            p.strokeColor = Ink; p.lineWidth = 2.5f; p.Stroke();
            DrawHat(p, c, radius);
            p.fillColor = Ink;
            float eye = radius * 0.36f, eyeY = -radius * 0.08f, dot = Mathf.Max(2f, radius * 0.12f);
            Circle(p, c + new Vector2(-eye, eyeY), dot); p.Fill();
            Circle(p, c + new Vector2(eye, eyeY), dot); p.Fill();
            if (archetype == CustomerArchetypes.Collector)
            {
                p.strokeColor = Ink; p.lineWidth = 1.8f;
                Circle(p, c + new Vector2(-eye, eyeY), radius * 0.27f); p.Stroke();
                Circle(p, c + new Vector2(eye, eyeY), radius * 0.27f); p.Stroke();
            }
            p.strokeColor = Ink; p.lineWidth = 2.5f; p.lineCap = LineCap.Round;
            float m = radius * 0.38f;
            p.BeginPath();
            switch (mood)
            {
                case BuyerMood.Happy: p.Arc(c + new Vector2(0, radius * 0.12f), m, Angle.Degrees(30), Angle.Degrees(150)); break;
                case BuyerMood.Neutral: p.MoveTo(c + new Vector2(-m * 0.8f, radius * 0.45f)); p.LineTo(c + new Vector2(m * 0.8f, radius * 0.45f)); break;
                default: p.Arc(c + new Vector2(0, radius * 0.82f), m, Angle.Degrees(215), Angle.Degrees(325)); break;
            }
            p.Stroke();
            if (mood != BuyerMood.Broke) return;
            // Empty wallet: a coral badge with a paper X at the lower right.
            Vector2 b = c + new Vector2(radius * 0.78f, radius * 0.62f);
            float br = radius * 0.42f;
            p.fillColor = Coral;
            Circle(p, b, br); p.Fill();
            p.strokeColor = Paper; p.lineWidth = 2.2f;
            float x = br * 0.45f;
            p.BeginPath(); p.MoveTo(b + new Vector2(-x, -x)); p.LineTo(b + new Vector2(x, x)); p.Stroke();
            p.BeginPath(); p.MoveTo(b + new Vector2(x, -x)); p.LineTo(b + new Vector2(-x, x)); p.Stroke();
        }

        void DrawHat(Painter2D p, Vector2 c, float radius)
        {
            Vector2 top = c + new Vector2(0, -radius * 0.55f);
            switch (archetype)
            {
                case CustomerArchetypes.Casual:
                    // A beanie with a bobble.
                    p.fillColor = CapNavy;
                    p.BeginPath(); p.Arc(top + new Vector2(0, radius * 0.1f), radius * 0.95f, Angle.Degrees(180), Angle.Degrees(360)); p.ClosePath(); p.Fill();
                    p.fillColor = Coral; Circle(p, top + new Vector2(0, -radius * 0.92f), radius * 0.2f); p.Fill();
                    break;
                case CustomerArchetypes.Bargain:
                    // A ball cap with its visor out to the side.
                    p.fillColor = Visor;
                    p.BeginPath(); p.Arc(top + new Vector2(0, radius * 0.1f), radius * 0.92f, Angle.Degrees(180), Angle.Degrees(360)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(top + new Vector2(0, radius * 0.02f)); p.LineTo(top + new Vector2(radius * 1.35f, radius * 0.02f));
                    p.LineTo(top + new Vector2(radius * 1.35f, radius * 0.24f)); p.LineTo(top + new Vector2(0, radius * 0.24f)); p.ClosePath(); p.Fill();
                    break;
                case CustomerArchetypes.Collector:
                    // A bowler with a wide brim.
                    p.fillColor = HatDark;
                    p.BeginPath(); p.Arc(top + new Vector2(0, -radius * 0.05f), radius * 0.62f, Angle.Degrees(180), Angle.Degrees(360)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(top + new Vector2(-radius * 1.05f, -radius * 0.08f)); p.LineTo(top + new Vector2(radius * 1.05f, -radius * 0.08f));
                    p.LineTo(top + new Vector2(radius * 1.05f, radius * 0.12f)); p.LineTo(top + new Vector2(-radius * 1.05f, radius * 0.12f)); p.ClosePath(); p.Fill();
                    break;
                case CustomerArchetypes.Premium:
                    // A top hat with a coral band.
                    p.fillColor = HatDark;
                    float w = radius * 0.62f, h = radius * 1.05f;
                    p.BeginPath(); p.MoveTo(top + new Vector2(-w, -h)); p.LineTo(top + new Vector2(w, -h)); p.LineTo(top + new Vector2(w, 0)); p.LineTo(top + new Vector2(-w, 0)); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(top + new Vector2(-radius, -radius * 0.05f)); p.LineTo(top + new Vector2(radius, -radius * 0.05f));
                    p.LineTo(top + new Vector2(radius, radius * 0.15f)); p.LineTo(top + new Vector2(-radius, radius * 0.15f)); p.ClosePath(); p.Fill();
                    p.fillColor = Coral;
                    p.BeginPath(); p.MoveTo(top + new Vector2(-w, -h * 0.32f)); p.LineTo(top + new Vector2(w, -h * 0.32f));
                    p.LineTo(top + new Vector2(w, -h * 0.14f)); p.LineTo(top + new Vector2(-w, -h * 0.14f)); p.ClosePath(); p.Fill();
                    break;
            }
        }
    }

    /// <summary>
    /// A creature's silhouette by its presentation id (teardrop, round, ribbon, lantern, wing), drawn in grey and then
    /// in its colour as <see cref="Fill01"/> rises, so a growing creature fills in.
    /// </summary>
    public sealed class CreatureGlyph : VisualElement
    {
        public const string ClassName = "es-creature";
        static readonly Color Grey = new Color(0.73f, 0.7f, 0.6f);
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        string look = "";
        float fill01 = 1;

        public string Look => look;
        public float Fill01 => fill01;

        public CreatureGlyph()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(string presentationId, float fill)
        {
            presentationId = presentationId ?? "";
            fill = Mathf.Clamp01(fill);
            if (presentationId == look && Mathf.Approximately(fill, fill01)) return;
            look = presentationId; fill01 = fill;
            MarkDirtyRepaint();
        }

        public static Color ColourOf(string presentationId)
        {
            switch (presentationId)
            {
                case "round": return new Color(0.95f, 0.69f, 0.31f);
                case "ribbon": return new Color(0.89f, 0.43f, 0.56f);
                case "lantern": return new Color(0.91f, 0.71f, 0.23f);
                case "wing": return new Color(0.75f, 0.66f, 0.85f);
                default: return new Color(0.94f, 0.48f, 0.35f);
            }
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = context.painter2D;
            Shape(p, r, Grey);
            if (fill01 <= 0.001f) return;
            Color colour = ColourOf(look); colour.a = fill01;
            Shape(p, r, colour);
            if (fill01 < 0.999f) return;
            p.fillColor = Ink;
            p.BeginPath(); p.Arc(new Vector2(r.xMin + r.width * 0.3f, r.center.y - r.height * 0.08f), Mathf.Max(1.6f, r.height * 0.08f), Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
        }

        void Shape(Painter2D p, Rect r, Color colour)
        {
            p.fillColor = colour;
            float x = r.xMin, y = r.yMin, w = r.width, h = r.height;
            Vector2 P(float u, float v) => new Vector2(x + u * w, y + v * h);
            switch (look)
            {
                case "round":
                    p.BeginPath(); p.Arc(P(0.42f, 0.5f), h * 0.42f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    p.BeginPath(); p.MoveTo(P(0.68f, 0.5f)); p.LineTo(P(0.92f, 0.22f)); p.LineTo(P(0.92f, 0.78f)); p.ClosePath(); p.Fill();
                    break;
                case "ribbon":
                    p.BeginPath(); p.MoveTo(P(0.06f, 0.5f)); p.BezierCurveTo(P(0.2f, 0.18f), P(0.55f, 0.22f), P(0.75f, 0.5f));
                    p.BezierCurveTo(P(0.55f, 0.78f), P(0.2f, 0.82f), P(0.06f, 0.5f)); p.Fill();
                    p.lineWidth = Mathf.Max(2f, h * 0.1f); p.strokeColor = colour; p.lineCap = LineCap.Round;
                    p.BeginPath(); p.MoveTo(P(0.74f, 0.5f)); p.BezierCurveTo(P(0.84f, 0.2f), P(0.9f, 0.3f), P(0.96f, 0.12f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(0.74f, 0.5f)); p.BezierCurveTo(P(0.84f, 0.8f), P(0.9f, 0.7f), P(0.96f, 0.88f)); p.Stroke();
                    break;
                case "lantern":
                    Body(p, P);
                    p.lineWidth = Mathf.Max(1.6f, h * 0.07f); p.strokeColor = colour;
                    p.BeginPath(); p.MoveTo(P(0.3f, 0.22f)); p.BezierCurveTo(P(0.3f, 0.0f), P(0.44f, 0.0f), P(0.46f, 0.08f)); p.Stroke();
                    p.BeginPath(); p.Arc(P(0.47f, 0.1f), h * 0.09f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    break;
                case "wing":
                    Body(p, P);
                    p.BeginPath(); p.MoveTo(P(0.36f, 0.4f)); p.LineTo(P(0.52f, 0.0f)); p.LineTo(P(0.6f, 0.4f)); p.ClosePath(); p.Fill();
                    break;
                default:
                    Body(p, P);
                    break;
            }
        }

        static void Body(Painter2D p, System.Func<float, float, Vector2> P)
        {
            p.BeginPath(); p.MoveTo(P(0.14f, 0.5f)); p.BezierCurveTo(P(0.14f, 0.22f), P(0.36f, 0.12f), P(0.54f, 0.2f));
            p.BezierCurveTo(P(0.72f, 0.26f), P(0.78f, 0.4f), P(0.78f, 0.5f));
            p.BezierCurveTo(P(0.78f, 0.6f), P(0.72f, 0.74f), P(0.54f, 0.8f));
            p.BezierCurveTo(P(0.36f, 0.88f), P(0.14f, 0.78f), P(0.14f, 0.5f)); p.Fill();
            p.BeginPath(); p.MoveTo(P(0.76f, 0.5f)); p.LineTo(P(0.96f, 0.22f)); p.LineTo(P(0.96f, 0.78f)); p.ClosePath(); p.Fill();
        }
    }

    /// <summary>A growing incubation as a ring that fills, with the creature filling in inside it.</summary>
    public sealed class ProgressRing : VisualElement
    {
        public const string ClassName = "es-ring";
        static readonly Color Track = new Color(0.95f, 0.9f, 0.77f);
        static readonly Color Teal = new Color(0.13f, 0.37f, 0.35f);
        readonly CreatureGlyph creature;
        float progress;

        public float Progress => progress;
        public CreatureGlyph Creature => creature;

        public ProgressRing()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            creature = new CreatureGlyph { name = "es-ring-creature" };
            creature.AddToClassList("es-ring__creature");
            Add(creature);
            generateVisualContent += Draw;
        }

        public void Set(float progress01, string look)
        {
            progress01 = Mathf.Clamp01(progress01);
            if (!Mathf.Approximately(progress, progress01)) { progress = progress01; MarkDirtyRepaint(); }
            creature.Set(look, progress01);
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f - 7f;
            var p = context.painter2D;
            p.lineWidth = 12f; p.lineCap = LineCap.Round;
            p.strokeColor = Track;
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (progress <= 0.001f) return;
            p.strokeColor = Teal;
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * progress)); p.Stroke();
        }
    }
}
