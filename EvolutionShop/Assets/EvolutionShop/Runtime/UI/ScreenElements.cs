using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>The receipt slip's torn bottom edge: paper teeth along the width.</summary>
    public sealed class ReceiptEdge : VisualElement
    {
        public const string ClassName = "es-screen-receipt__tear";
        static readonly Color Paper = new Color(1f, 0.992f, 0.961f);

        public ReceiptEdge()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = context.painter2D;
            p.fillColor = Paper;
            float tooth = r.height * 1.4f;
            int count = Mathf.Max(1, Mathf.RoundToInt(r.width / tooth));
            float w = r.width / count;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            for (int i = 0; i < count; i++)
            {
                p.LineTo(new Vector2(r.xMin + w * (i + 0.5f), r.yMax));
                p.LineTo(new Vector2(r.xMin + w * (i + 1), r.yMin));
            }
            p.ClosePath(); p.Fill();
        }
    }

    /// <summary>A small filled icon for the UX-07 screens (UX.8: simple filled shapes, no emoji): a clock, a heart or a star.</summary>
    public sealed class ScreenGlyph : VisualElement
    {
        public enum Kind { Clock, Heart, Star }
        public const string ClassName = "es-screen-glyph";
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        static readonly Color Coral = new Color(0.84f, 0.38f, 0.27f);
        static readonly Color Honey = new Color(0.98f, 0.78f, 0.37f);
        static readonly Color HoneyEdge = new Color(0.79f, 0.55f, 0.12f);
        static readonly Color Paper = new Color(1f, 0.963f, 0.874f);
        Kind kind;

        public Kind Shape => kind;

        public ScreenGlyph(Kind kind)
        {
            this.kind = kind;
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = context.painter2D;
            Vector2 c = r.center;
            float s = Mathf.Min(r.width, r.height);
            switch (kind)
            {
                case Kind.Clock:
                    p.fillColor = Paper; p.strokeColor = Ink; p.lineWidth = Mathf.Max(2f, s * 0.11f);
                    p.BeginPath(); p.Arc(c, s * 0.4f, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill(); p.Stroke();
                    p.lineCap = LineCap.Round;
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(0, -s * 0.24f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(s * 0.17f, s * 0.06f)); p.Stroke();
                    break;
                case Kind.Heart:
                    p.fillColor = Coral;
                    float h = s * 0.22f;
                    p.BeginPath(); p.Arc(c + new Vector2(-h, -h * 0.55f), h * 1.05f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    p.BeginPath(); p.Arc(c + new Vector2(h, -h * 0.55f), h * 1.05f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    p.BeginPath(); p.MoveTo(c + new Vector2(-h * 2.0f, -h * 0.2f)); p.LineTo(c + new Vector2(h * 2.0f, -h * 0.2f)); p.LineTo(c + new Vector2(0, s * 0.4f)); p.ClosePath(); p.Fill();
                    break;
                default:
                    p.fillColor = Honey; p.strokeColor = HoneyEdge; p.lineWidth = Mathf.Max(1.5f, s * 0.07f); p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.Deg2Rad * (-90 + i * 36);
                        float rad = (i % 2 == 0 ? 0.46f : 0.2f) * s;
                        var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                        if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                    }
                    p.ClosePath(); p.Fill(); p.Stroke();
                    break;
            }
        }
    }
}
