using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// A waiting customer: a face in the archetype's colour inside a patience ring (UX.4). The ring empties as
    /// patience runs out and turns coral when it is low; the mouth follows it. Alt shows the seconds instead.
    /// </summary>
    public sealed class HudFace : VisualElement
    {
        public const string ClassName = "es-face";
        static readonly Color Track = new Color(0.95f, 0.9f, 0.77f, 0.95f);
        static readonly Color Teal = new Color(0.13f, 0.37f, 0.35f);
        static readonly Color Coral = new Color(0.84f, 0.38f, 0.27f);
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        readonly Label seconds;
        float patience = 1;
        bool low, atCounter, showSeconds;
        Color fill = Color.white;

        public float Patience => patience;
        public bool Low => low;

        public HudFace()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            seconds = new Label { pickingMode = PickingMode.Ignore };
            seconds.style.position = Position.Absolute;
            seconds.style.left = 0; seconds.style.right = 0; seconds.style.top = 0; seconds.style.bottom = 0;
            seconds.style.unityTextAlign = TextAnchor.MiddleCenter;
            seconds.style.marginLeft = 0; seconds.style.marginTop = 0; seconds.style.paddingLeft = 0; seconds.style.paddingTop = 0;
            Add(seconds);
            generateVisualContent += Draw;
        }

        public void Set(float patience01, bool isLow, bool waitingAtCounter, Color faceColor, string secondsText)
        {
            bool show = !string.IsNullOrEmpty(secondsText);
            if (!Mathf.Approximately(patience, patience01) || low != isLow || atCounter != waitingAtCounter || fill != faceColor || showSeconds != show)
            {
                patience = patience01; low = isLow; atCounter = waitingAtCounter; fill = faceColor; showSeconds = show;
                MarkDirtyRepaint();
            }
            string text = secondsText ?? "";
            if (seconds.text != text) seconds.text = text;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            var p = context.painter2D;
            p.lineWidth = 6f;
            p.lineCap = LineCap.Round;
            p.strokeColor = Track;
            p.BeginPath(); p.Arc(c, radius - 3f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (atCounter && patience > 0.001f)
            {
                p.strokeColor = low ? Coral : Teal;
                p.BeginPath(); p.Arc(c, radius - 3f, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * patience)); p.Stroke();
            }
            p.fillColor = fill;
            p.BeginPath(); p.Arc(c, radius - 9f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.strokeColor = Ink; p.lineWidth = 2.5f; p.Stroke();
            if (showSeconds) return;
            p.fillColor = Ink;
            p.BeginPath(); p.Arc(c + new Vector2(-6.5f, -4f), 2.8f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.BeginPath(); p.Arc(c + new Vector2(6.5f, -4f), 2.8f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.lineWidth = 2.5f;
            p.BeginPath();
            if (!atCounter || patience > 0.6f) p.Arc(c + new Vector2(0, 2f), 8f, Angle.Degrees(30), Angle.Degrees(150));
            else if (!low) { p.MoveTo(c + new Vector2(-7f, 8f)); p.LineTo(c + new Vector2(7f, 8f)); }
            else p.Arc(c + new Vector2(0, 15f), 8f, Angle.Degrees(215), Angle.Degrees(325));
            p.Stroke();
        }
    }

    /// <summary>A paper triangle pointing along <see cref="Direction"/> (panel space, y down) towards an off-screen ping.</summary>
    public sealed class HudArrowGlyph : VisualElement
    {
        static readonly Color Paper = new Color(1f, 0.963f, 0.874f);
        Vector2 direction = Vector2.right;

        public Vector2 Direction
        {
            get => direction;
            set
            {
                Vector2 d = value.sqrMagnitude > 1e-6f ? value.normalized : Vector2.right;
                if ((d - direction).sqrMagnitude < 1e-6f) return;
                direction = d;
                MarkDirtyRepaint();
            }
        }

        public HudArrowGlyph()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            Vector2 c = r.center;
            float s = Mathf.Min(r.width, r.height) * 0.26f;
            var p = context.painter2D;
            p.fillColor = Paper;
            p.BeginPath();
            // A notched dart: a near-equilateral triangle reads as pointing at whichever corner is on top.
            Vector2 d = direction, n = new Vector2(-direction.y, direction.x);
            p.MoveTo(c + d * s * 1.25f);
            p.LineTo(c - d * s * 0.85f + n * s * 0.8f);
            p.LineTo(c - d * s * 0.3f);
            p.LineTo(c - d * s * 0.85f - n * s * 0.8f);
            p.ClosePath();
            p.Fill();
        }
    }
}
