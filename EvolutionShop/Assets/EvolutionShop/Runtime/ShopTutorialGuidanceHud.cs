using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        GUIStyle tutorialMarkerTitleStyle, tutorialMarkerActionStyle, tutorialLocalCalloutStyle;

        void TutorialGuidanceStyles()
        {
            if (tutorialMarkerTitleStyle != null) return;
            tutorialMarkerTitleStyle = new GUIStyle(hudTitleStyle) { fontSize = 14 };
            tutorialMarkerActionStyle = new GUIStyle(hudBodyStyle) { fontSize = 13 };
            tutorialLocalCalloutStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 15, padding = new RectOffset(14, 12, 10, 10)
            };
        }

        // Presentation only: the resolver owns live identity, priority and world occlusion.
        // Keep the single marker off the status cards and leave the aim reticle exposed.
        void DrawTutorialWorldGuidance()
        {
            if (Event.current.type != EventType.Repaint ||
                !TryProjectTutorialGuidance(out var target, out var point)) return;
            TutorialGuidanceStyles();
            const float width = 208;
            float titleHeight = Mathf.Max(18, HudTextHeight(target.Label, tutorialMarkerTitleStyle, width - 24));
            float actionHeight = Mathf.Max(17, HudTextHeight(target.Action, tutorialMarkerActionStyle, width - 24));
            float height = titleHeight + actionHeight + 23;
            if (!TryTutorialMarkerCard(point, width, height, out var card)) return;

            // Dark under-strokes and cream edges remain legible against both room palettes.
            DrawTutorialMarkerCorners(point, 4, ShopUiTheme.Ink);
            DrawTutorialMarkerCorners(point, 2, ShopUiTheme.Paper);
            HudCard(card, mint);
            GUI.Label(new Rect(card.x + 12, card.y + 10, width - 24, titleHeight), target.Label, tutorialMarkerTitleStyle);
            GUI.Label(new Rect(card.x + 12, card.y + 12 + titleHeight, width - 24, actionHeight), target.Action, tutorialMarkerActionStyle);
        }

        bool TryTutorialMarkerCard(Vector2 point, float width, float height, out Rect card)
        {
            // Existing HUD cards own the side columns, notice band and focus prompt below.
            // Off-screen targets get no edge arrow; turning remains the player's choice.
            var safe = new Rect(310, 170, 704, 396);
            card = default;
            if (!safe.Contains(point)) return false;
            for (int slot = 0; slot < 4; slot++)
            {
                var candidate = slot == 0 ? new Rect(point.x + 28, point.y - height * .5f, width, height) :
                    slot == 1 ? new Rect(point.x - 28 - width, point.y - height * .5f, width, height) :
                    slot == 2 ? new Rect(point.x - width * .5f, point.y - 28 - height, width, height) :
                    new Rect(point.x - width * .5f, point.y + 28, width, height);
                if (candidate.xMin < safe.xMin || candidate.xMax > safe.xMax ||
                    candidate.yMin < safe.yMin || candidate.yMax > safe.yMax ||
                    candidate.Overlaps(new Rect(623, 343, 34, 34))) continue;
                // The ordinary notice has priority, including its variable wrapped height.
                if ((!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil) ||
                    !string.IsNullOrEmpty(SaveError))
                {
                    string notice = string.IsNullOrEmpty(SaveError) ? message : "PROGRESS NOT SAVED - " + SaveError;
                    float noticeHeight = Mathf.Max(38, HudTextHeight(notice, centerStyle, 568) + 18);
                    if (candidate.Overlaps(new Rect(330, Comedy.Active ? 208 : 82, 620, noticeHeight + 8))) continue;
                }
                if (Comedy.Active)
                {
                    float captionHeight = Mathf.Max(24, HudTextHeight(Comedy.Caption, centerStyle, 512));
                    if (candidate.Overlaps(new Rect(352, 104, 668, captionHeight + 51))) continue;
                }
                if (SlapTarget != null)
                {
                    float focusHeight = Focused == null ? 0 :
                        Mathf.Max(21, HudTextHeight(Focused.Label, hudTitleStyle, 320)) + 39;
                    float slapHeight = Mathf.Max(20, HudTextHeight(SlapTarget.ArchetypeName, hudTitleStyle, 300)) + 39;
                    if (candidate.Overlaps(new Rect(436, 646 - focusHeight - slapHeight, 408, slapHeight + 16))) continue;
                }
                card = candidate;
                return true;
            }
            return false;
        }

        void DrawTutorialMarkerCorners(Vector2 point, float stroke, Color color)
        {
            const float radius = 15, length = 9;
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = (corner & 1) != 0, bottom = (corner & 2) != 0;
                float x = point.x + (right ? radius : -radius);
                float y = point.y + (bottom ? radius : -radius);
                DrawTutorialMarkerStroke(new Rect(right ? x - length : x, y - stroke * .5f, length, stroke), color);
                DrawTutorialMarkerStroke(new Rect(x - stroke * .5f, bottom ? y - length : y, stroke, length), color);
            }
        }

        static void DrawTutorialMarkerStroke(Rect rect, Color color)
        {
            if (!rect.Overlaps(new Rect(632, 352, 16, 16))) Box(rect, color);
        }

        void DrawTutorialLocalCallout()
        {
            string callout = TutorialLocalCallout;
            if (string.IsNullOrEmpty(callout)) return;
            TutorialGuidanceStyles();
            hudMeasure.text = callout;
            Rect rect = GUILayoutUtility.GetRect(hudMeasure, tutorialLocalCalloutStyle, GUILayout.ExpandWidth(true));
            hudTheme.Draw(rect, hudTheme.Inset);
            Box(new Rect(rect.x + 2, rect.y + 8, 3, Mathf.Max(0, rect.height - 16)), mint);
            GUI.Label(rect, callout, tutorialLocalCalloutStyle);
            GUILayout.Space(6);
        }
    }
}
