#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // UX-02: the UI Toolkit root, theme, fonts, scaling, world-anchored card, cursor policy and close paths.
        IEnumerator ToolkitUiScenario()
        {
            limitations.Add("UX-02 foundation route: stations open through ShopSession.Interact after a scripted aim, and the card's Close button is activated with a UI Toolkit navigation submit event. Physical mouse clicks on the card are a separate hands-on check.");
            limitations.Add("The card is a frame only; station controls move into it in UX-04/UX-05. With the preview on, the classic IMGUI panel for that station is hidden.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready, "UI Toolkit root loads theme, tokens and card template" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready) yield break;
            Check(ui.Settings.scaleMode == PanelScaleMode.ScaleWithScreenSize && ui.Settings.referenceResolution == ShopUiRoot.ReferenceResolution &&
                Mathf.Approximately(ui.Settings.match, ShopUiRoot.ScreenMatch) && ui.Settings.themeStyleSheet != null,
                "Panel scales from a 1920x1080 reference with match 0.5 and the shop theme");
            Check(ui.Strings.Count >= 11 && ui.Strings.Has("card.close") && ui.Strings.Has("card.hint"), "UI string table loaded (" + ui.Strings.Count + " entries)");
            Check(shop.Panel == ShopPanel.None && !ui.Active, "Preview starts off in validation without -evolutionToolkitUi");
            Check(shop.SetToolkitUiActive(true) && shop.ToolkitUiActive, "Preview turns on");
            yield return null; yield return null;

            var root = ui.Root;
            Check(root != null && root.panel != null, "UI Toolkit root is attached to a runtime panel");
            if (root == null || root.panel == null) yield break;
            Vector2 expected = ShopUiLayout.ExpectedPanelSize(new Vector2(Screen.width, Screen.height), ShopUiRoot.ReferenceResolution);
            Vector2 actual = root.layout.size;
            Check(Mathf.Abs(actual.x - expected.x) <= 2 && Mathf.Abs(actual.y - expected.y) <= 2,
                "Panel size " + actual.x.ToString("0") + "x" + actual.y.ToString("0") + " matches scale-with-screen for " + Screen.width + "x" + Screen.height +
                " (expected " + expected.x.ToString("0") + "x" + expected.y.ToString("0") + ")");

            var card = ui.StationCard;
            var incubator = StationOf(StationKind.Incubator);
            yield return ToolkitOpen(incubator, ShopPanel.Incubator);
            Check(shop.Panel == ShopPanel.Incubator && card.IsShown, "Incubator opens the themed station card");
            Check(card.Title == incubator.Label && !card.ShopStyled, "Card shows the incubator's name on the lab (teal) strip");
            Check(!card.Placement.AnchorHidden && !card.Placement.CoversAnchor, "Card sits beside the incubator, not on its anchor (flipped=" + card.Placement.Flipped + ")");
            Check(ShopUiTestDriver.FullyInside(card.Element, root), "Card is fully on screen");
            var titleLabel = ShopUiTestDriver.Find<Label>(root, StationCardPresenter.TitleName);
            var bodyLabel = ShopUiTestDriver.Find<Label>(root, StationCardPresenter.BodyName);
            // Text is drawn with -unity-font-definition, which wins over -unity-font; the default theme sets one,
            // so a run 2 check of resolvedStyle.unityFont passed while the captures still showed the theme's font.
            var buttonLabel = ShopUiTestDriver.Find<Button>(root, StationCardPresenter.CloseName);
            string titleFont = DrawnFontName(titleLabel), bodyFont = DrawnFontName(bodyLabel), buttonFont = DrawnFontName(buttonLabel);
            Check(titleFont.Contains("Baloo"), "Card title draws with Baloo 2 (" + titleFont + ")");
            Check(bodyFont.Contains("Nunito"), "Card body draws with Nunito (" + bodyFont + ")");
            Check(buttonFont.Contains("Baloo"), "Card button draws with Baloo 2 (" + buttonFont + ")");
            string titleProblem = TitleProblem(root);
            Check(titleProblem == null, "Incubator title fits its strip at the width it is drawn" + (titleProblem != null ? " / " + titleProblem : ""));
            float oneLineTitle = titleLabel != null ? titleLabel.layout.height : 0;
            Check(Cursor.visible && Cursor.lockState == CursorLockMode.None && !shop.Player.InputEnabled, "Card frees the cursor and pauses movement");
            yield return UiSettled();
            yield return Capture("ux02-incubator-card");

            var close = ShopUiTestDriver.Find<Button>(root, StationCardPresenter.CloseName);
            Check(close != null && close.text == ui.Strings.Get("card.close"), "Close button is named and uses the string table");
            Check(ShopUiTestDriver.Submit(close), "Close button accepts a submit event");
            yield return null; yield return null;
            Check(shop.Panel == ShopPanel.None && !card.IsShown, "Close returns to play and hides the card");
            Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked || Application.isBatchMode, "Closing restores the captured cursor");

            var counter = StationOf(StationKind.Counter);
            yield return ToolkitOpen(counter, ShopPanel.Counter);
            Check(shop.Panel == ShopPanel.Counter && card.IsShown && card.Title == counter.Label && card.ShopStyled,
                "Counter opens the same card frame on the shop (kraft) strip");
            Check(!card.Placement.CoversAnchor && ShopUiTestDriver.FullyInside(card.Element, root), "Counter card sits beside the counter and on screen");
            titleProblem = TitleProblem(root);
            Check(titleProblem == null, "Counter title fits its strip at the width it is drawn" + (titleProblem != null ? " / " + titleProblem : ""));
            yield return UiSettled();
            yield return Capture("ux02-counter-card");

            // Run 4's 2560x1080 capture showed "Sales counter" wrapping out of its strip. Station names get longer
            // than that once research unlocks, so a real one is forced here at every size.
            string shownTitle = titleLabel.text;
            titleLabel.text = "Spark grower / Siphon upgrade";
            yield return null; yield return null;
            titleProblem = TitleProblem(root);
            Check(titleLabel.layout.height > oneLineTitle * 1.5f && titleProblem == null,
                "A long station name wraps inside its strip (" + (titleLabel.layout.height / Mathf.Max(1, oneLineTitle)).ToString("0.0") + "x one line" +
                (titleProblem != null ? " / " + titleProblem : "") + ")");
            Check(ShopUiTestDriver.FullyInside(card.Element, root), "The taller card stays on screen");
            yield return UiSettled();
            yield return Capture("ux02-long-title");
            titleLabel.text = shownTitle;
            yield return null;
            shop.SetPanel(ShopPanel.None);
            yield return null; yield return null;
            Check(!card.IsShown, "Card hides when the session closes the panel (Escape path)");

            Check(shop.SetToolkitUiActive(false) && !shop.ToolkitUiActive, "Preview turns off");
            yield return ToolkitOpen(incubator, ShopPanel.Incubator);
            Check(shop.Panel == ShopPanel.Incubator && !card.IsShown, "With the preview off the classic panel owns the incubator again");
            yield return UiSettled();
            yield return Capture("ux02-classic-fallback");
            shop.SetPanel(ShopPanel.None);
            yield return null;
        }

        IEnumerator ToolkitOpen(Station station, ShopPanel expected)
        {
            var anchor = station.transform;
            GateAim(anchor.TransformPoint(new Vector3(0, .2f, -1.6f)), ShopUiLayout.AnchorPoint(station));
            Physics.SyncTransforms();
            yield return null;
            shop.Interact(station);
            yield return null; yield return null; yield return null;
            if (shop.Panel != expected) Debug.LogWarning("UX02_OPEN expected " + expected + " got " + shop.Panel);
        }

        /// <summary>
        /// Null when the card title's text, measured at the width it is laid out at, fits the title's box and that box
        /// sits inside the strip; else why not. The box alone can pass while the text spills past it.
        /// </summary>
        static string TitleProblem(VisualElement root)
        {
            var title = ShopUiTestDriver.Find<Label>(root, StationCardPresenter.TitleName);
            var strip = ShopUiTestDriver.Find<VisualElement>(root, StationCardPresenter.StripName);
            if (title == null || strip == null) return "title or strip missing";
            Rect box = title.contentRect;
            Vector2 needed = title.MeasureTextSize(title.text, box.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
            if (needed.y > box.height + 1f) return "text needs " + needed.y.ToString("0") + " px, its box is " + box.height.ToString("0");
            if (!ShopUiTestDriver.FullyInside(title, strip)) return "the title's box leaves the strip";
            return null;
        }

        /// <summary>The font a text element is drawn with: its font definition's asset or font, else its plain font.</summary>
        static string DrawnFontName(VisualElement element)
        {
            if (element == null) return "none";
            var definition = element.resolvedStyle.unityFontDefinition;
            if (definition.fontAsset != null) return definition.fontAsset.name;
            if (definition.font != null) return definition.font.name;
            var font = element.resolvedStyle.unityFont;
            return font ? font.name : "none";
        }
    }
}
#endif
