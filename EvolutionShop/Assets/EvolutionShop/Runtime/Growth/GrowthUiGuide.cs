using System.IO;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-14's guided first night (GR.43) in the HUD: the next-step line says the guide's step, and a marker in the classic
    /// tutorial's style (UX: a coral disc with a paper arrow and a label) points at the thing to use, above it when it is on
    /// screen, or at the screen's edge, pointing the way, when it isn't. Hidden while a card is open.
    /// </summary>
    public sealed partial class GrowthUi
    {
        // The marker's size (HudStyles' es-arrow) and how far from each edge it stays: clear of the HUD's chips at the top and
        // the next-step line at the bottom.
        const float GuideWidth = 132f, GuideHeight = 104f, GuideDisc = 64f, GuideSide = 96f, GuideTop = 120f, GuideBottom = 170f;
        VisualElement guideBox;
        HudArrowGlyph guideGlyph;
        Label guideLabel;

        /// <summary>Validation: the marker's target now (null when hidden), whether it is at the screen's edge, and where it points in the panel.</summary>
        public string GuideTarget { get; private set; }
        public bool GuideAtEdge { get; private set; }
        public Vector2 GuidePoint { get; private set; }
        public string GuideLabel => guideLabel == null ? "" : guideLabel.text;
        /// <summary>Validation: where a guide or lesson target stands in the world (the marker's point), null when it is not built.</summary>
        public Vector3? GuidePointOf(string target) => GuideWorld(target);

        void BuildGuide()
        {
            guideBox = new VisualElement { name = "gr-guide", pickingMode = PickingMode.Ignore };
            guideBox.AddToClassList("es-arrow"); guideBox.AddToClassList("gr-guide");
            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.AddToClassList("es-arrow__disc");
            guideGlyph = new HudArrowGlyph();
            disc.Add(guideGlyph);
            guideLabel = new Label { pickingMode = PickingMode.Ignore };
            guideLabel.AddToClassList("es-arrow__label");
            guideBox.Add(disc); guideBox.Add(guideLabel);
            guideBox.style.display = DisplayStyle.None;
            // Below every other HUD piece, so notes, the prompt and the next step are never covered by it.
            hud.Insert(0, guideBox);
        }

        /// <summary>Where a guide target stands in the world: a point a little above the thing to use.</summary>
        Vector3? GuideWorld(string target)
        {
            if (target == null) return null;
            if (target == "incubator") return GrowthWorld.IncubatorPosition + new Vector3(0, 1.55f, 0);
            if (target == "counter") return GrowthWorld.CounterPosition + new Vector3(0, 1.2f, 0);
            if (target.StartsWith("grower:"))
                return GrowthWorld.GrowerPositions.TryGetValue(target.Substring(7), out var grower) ? grower + new Vector3(0, 1.75f, 0) : (Vector3?)null;
            if (target.StartsWith("container:"))
            {
                string id = target.Substring(10);
                foreach (var c in s.Shop.Containers)
                    if (c.Id == id && GrowthWorld.Places.TryGetValue(c.Place, out var place)) return place.Position + new Vector3(0, 1.45f, 0);
                return null;
            }
            // R09.4: a lesson's target, a GrowthTargetKind's name and maybe an id ("Place:a2", "Board").
            return LessonTargetWorld(target);
        }

        void TickGuideMarker(GrowthGuideView guide, bool free)
        {
            Vector3? world = free && guide != null ? GuideWorld(guide.Target) : null;
            var cam = s.Player != null ? s.Player.View : null;
            var panel = hud.panel;
            Vector2 size = panel == null ? Vector2.zero : panel.visualTree.layout.size;
            if (world == null || cam == null || panel == null || size.x < 1 || float.IsNaN(size.x))
            {
                guideBox.style.display = DisplayStyle.None;
                GuideTarget = null;
                return;
            }
            Vector3 view = cam.WorldToViewportPoint(world.Value);
            bool behind = view.z <= 0;
            // Behind the camera, the point is mirrored through it, so its direction from the centre still says which way to turn.
            Vector3 aim = behind ? cam.transform.position - (world.Value - cam.transform.position) : world.Value;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, aim, cam);
            bool onScreen = !behind && p.x > GuideSide && p.x < size.x - GuideSide && p.y > 24 && p.y < size.y - GuideBottom;
            guideLabel.text = guide.TargetName ?? "";
            if (onScreen)
            {
                // Above the thing to use, its label on top and the arrow pointing down at it; standing close, with no room above
                // (the point near the top of the screen), below it with the arrow pointing up.
                bool above = p.y - GuideHeight - 8 >= GuideTop * 0.6f;
                guideBox.EnableInClassList("gr-guide--over", above);
                guideGlyph.Direction = new Vector2(0, above ? 1 : -1);
                guideBox.style.left = p.x - GuideWidth / 2;
                guideBox.style.top = above ? p.y - GuideHeight - 8 : p.y + 8;
                GuideAtEdge = false;
            }
            else
            {
                // At the screen's edge, on the line from the centre towards it, pointing that way.
                guideBox.RemoveFromClassList("gr-guide--over");
                Vector2 centre = size / 2, d = p - centre;
                if (d.sqrMagnitude < 1) d = Vector2.down;
                if (behind && d.y < 0) d.y = -d.y * 0.25f; // a thing behind the player: point to the side, not up at the sky
                float scale = Mathf.Min((centre.x - GuideSide) / Mathf.Max(0.001f, Mathf.Abs(d.x)), (d.y < 0 ? centre.y - GuideTop : centre.y - GuideBottom) / Mathf.Max(0.001f, Mathf.Abs(d.y)));
                Vector2 at = centre + d * scale;
                guideGlyph.Direction = d;
                guideBox.style.left = at.x - GuideWidth / 2;
                guideBox.style.top = at.y - GuideDisc / 2;
                GuideAtEdge = true;
            }
            guideBox.style.display = DisplayStyle.Flex;
            GuideTarget = guide.Target;
            GuidePoint = p;
        }
    }
}
