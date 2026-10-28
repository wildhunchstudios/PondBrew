using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
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
