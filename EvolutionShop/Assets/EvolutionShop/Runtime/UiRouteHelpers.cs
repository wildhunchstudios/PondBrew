#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    // UX-09: what the older routes use now that the UI Toolkit is the only UI. Their gameplay checks stay as they were; where
    // they scrolled or captured an IMGUI panel they now use the toolkit's named elements (UX.10: named-element queries).
    public sealed partial class MilestoneSmoke
    {
        /// <summary>
        /// True when no MonoBehaviour in the game assembly declares OnGUI, so nothing draws with IMGUI (UX-09). The detail
        /// names any that do.
        /// </summary>
        static bool NoImmediateModeGui(out string detail)
        {
            detail = "";
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in typeof(ShopSession).Assembly.GetTypes())
                if (typeof(MonoBehaviour).IsAssignableFrom(type) && type.GetMethod("OnGUI", flags) != null) detail += " " + type.Name;
            if (detail.Length > 0) detail = ": found in" + detail;
            return detail.Length == 0;
        }

        /// <summary>Waits for the open screen to show, then presses one of its buttons by name and lets it redraw.</summary>
        IEnumerator UiPress(string buttonName, string label)
        {
            yield return null; yield return null;
            var root = shop.ToolkitUi != null ? shop.ToolkitUi.Root : null;
            var button = root?.Q<Button>(buttonName);
            Check(ShopUiTestDriver.Submit(button), label + " (" + buttonName + ") is pressed in the toolkit UI");
            yield return null; yield return null;
        }

        /// <summary>
        /// A fixture caption over the screen for routes that record labelled clips (FX-06R-01's vocabulary fixture); a plain
        /// UI Toolkit label on the UI root, so it shows in captures without IMGUI. Null or empty removes it.
        /// </summary>
        void FixtureCaption(string text)
        {
            var root = shop.ToolkitUi != null ? shop.ToolkitUi.Root : null;
            if (root == null) return;
            var label = root.Q<Label>(FixtureCaptionName);
            if (string.IsNullOrEmpty(text)) { label?.RemoveFromHierarchy(); return; }
            if (label == null)
            {
                label = new Label { name = FixtureCaptionName, pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.top = 70; label.style.left = new Length(50, LengthUnit.Percent); label.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
                label.style.width = 720; label.style.paddingTop = 8; label.style.paddingBottom = 8; label.style.paddingLeft = 14; label.style.paddingRight = 14;
                label.style.backgroundColor = new Color(.05f, .12f, .11f, .82f); label.style.color = Color.white;
                label.style.fontSize = 20; label.style.whiteSpace = WhiteSpace.Normal; label.style.unityTextAlign = TextAnchor.MiddleCenter;
                root.Add(label);
            }
            label.text = text;
        }
        const string FixtureCaptionName = "es-fixture-caption";

        // The prompt beside the crosshair (UX-03): key, second key, name and status labels, read as the player sees them.
        string HudPromptText(string part) => shop.ToolkitUi?.Root?.Q<Label>("es-hud-prompt-" + part)?.text ?? "";
        bool HudPromptShown(string part) => HudPresenter.Visible(shop.ToolkitUi?.Root?.Q("es-hud-prompt-" + part));
        string HudPrompt() => "[" + HudPromptText("key") + (HudPromptShown("key2") ? "][" + HudPromptText("key2") : "") + "] " + HudPromptText("name") + " " + HudPromptText("status");
        string HudString(string key, params object[] values) => values.Length == 0 ? shop.ToolkitUi.Strings.Get(key) : shop.ToolkitUi.Strings.Format(key, values);
        // A countdown status reads "· 12s".
        bool PromptCountdown() { string s = HudPromptText("status"); return HudPromptShown("status") && s.StartsWith("· ") && s.EndsWith("s") && int.TryParse(s.Substring(2, s.Length - 3), out _); }
        // The HUD redraws in LateUpdate, after a coroutine resumes, so a prompt check waits two frames after the panel closes.
        static IEnumerator PromptFrames() { yield return null; yield return null; }
    }
}
#endif
