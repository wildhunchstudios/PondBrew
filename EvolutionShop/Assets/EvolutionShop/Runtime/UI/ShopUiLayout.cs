using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Where a world-anchored card goes on the UI Toolkit panel.</summary>
    public struct ShopUiCardPlacement
    {
        public Vector2 Position;
        /// <summary>The card was moved to the anchor's left because the right side had no room.</summary>
        public bool Flipped;
        /// <summary>The card had to cover the anchor point because neither side had room.</summary>
        public bool CoversAnchor;
        /// <summary>The anchor was behind the camera, so the card uses the fallback slot.</summary>
        public bool AnchorHidden;
    }

    /// <summary>Pure placement math for UX-02 station cards. Panel coordinates: origin top-left, y down.</summary>
    public static class ShopUiLayout
    {
        public const float DefaultGap = 56f;
        public const float DefaultMargin = 24f;
        public static readonly Vector2 FallbackCardSize = new Vector2(380f, 220f);

        /// <summary>Places a card beside the anchor: right side first, then left, clamped to the panel.</summary>
        public static ShopUiCardPlacement PlaceBeside(Vector2 anchor, Vector2 cardSize, Vector2 panelSize,
            float gap = DefaultGap, float margin = DefaultMargin, bool anchorHidden = false)
        {
            if (cardSize.x <= 0 || cardSize.y <= 0 || float.IsNaN(cardSize.x) || float.IsNaN(cardSize.y)) cardSize = FallbackCardSize;
            float maxX = Mathf.Max(margin, panelSize.x - margin - cardSize.x);
            float maxY = Mathf.Max(margin, panelSize.y - margin - cardSize.y);
            if (anchorHidden || float.IsNaN(anchor.x) || float.IsNaN(anchor.y))
                return new ShopUiCardPlacement
                {
                    Position = new Vector2(margin, Mathf.Clamp((panelSize.y - cardSize.y) * 0.5f, margin, maxY)),
                    AnchorHidden = true
                };
            float x = anchor.x + gap;
            bool flipped = false;
            if (x + cardSize.x > panelSize.x - margin)
            {
                float left = anchor.x - gap - cardSize.x;
                if (left >= margin) { x = left; flipped = true; }
                else x = Mathf.Clamp(x, margin, maxX);
            }
            float y = Mathf.Clamp(anchor.y - cardSize.y * 0.5f, margin, maxY);
            var rect = new Rect(x, y, cardSize.x, cardSize.y);
            return new ShopUiCardPlacement { Position = new Vector2(x, y), Flipped = flipped, CoversAnchor = rect.Contains(anchor) };
        }

        /// <summary>The world point a station card points at: its collider centre, else a point above its root.</summary>
        public static Vector3 AnchorPoint(Station station)
        {
            if (!station) return Vector3.zero;
            var collider = station.GetComponentInChildren<Collider>();
            return collider ? collider.bounds.center : station.transform.position + Vector3.up * 1.2f;
        }

        /// <summary>Panel size a ScaleWithScreenSize panel reports for a screen, matching UI Toolkit's own formula.</summary>
        public static Vector2 ExpectedPanelSize(Vector2 screen, Vector2 reference, float match = 0.5f)
        {
            if (screen.x <= 0 || screen.y <= 0 || reference.x <= 0 || reference.y <= 0) return Vector2.zero;
            // UI Toolkit's PanelSettings divides by a linear blend of the two ratios (uGUI's CanvasScaler uses a
            // log average instead); the two agree at 16:9 and differ on ultrawide screens.
            float ratio = Mathf.Lerp(screen.x / reference.x, screen.y / reference.y, Mathf.Clamp01(match));
            return screen / ratio;
        }
    }
}
