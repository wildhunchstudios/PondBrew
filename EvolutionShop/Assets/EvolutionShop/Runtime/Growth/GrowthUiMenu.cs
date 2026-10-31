using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-27: the main menu. A paper panel down the left over the live shop (the menu's slow view, GrowthMenuCamera): the title
    /// lettered on a painted shop-sign plate with a few of the creatures found around it (their own portraits, GrowthPortraits),
    /// the saved shop in a line, and Continue, New game (asking first when it would replace a shop), Settings and Quit. The
    /// shots dissolve through a fade drawn over everything but the panel.
    /// </summary>
    public sealed partial class GrowthUi
    {
        bool menuAskNew;
        readonly List<VisualElement> menuCreatures = new List<VisualElement>();
        VisualElement menuFade;

        /// <summary>Validation: the portraits around the title now (their species), and the fade's darkness.</summary>
        public readonly List<string> MenuShowcase = new List<string>();
        public float MenuFade => menuFade == null ? 0 : menuFade.resolvedStyle.opacity;

        VisualElement MenuScreen()
        {
            var shop = s.Shop;
            menuCreatures.Clear(); MenuShowcase.Clear();
            var root = Box("gr-menu"); root.name = "gr-menu";
            root.pickingMode = PickingMode.Ignore;
            menuFade = Box("gr-menu__fade"); menuFade.pickingMode = PickingMode.Ignore;
            root.Add(menuFade);

            var panel = Box("gr-menu__panel"); panel.name = "gr-menu-panel";
            panel.pickingMode = PickingMode.Position;
            root.Add(panel);

            // The title on its sign, the creatures around it.
            var signBox = Box("gr-menu__signbox");
            var sign = Box("gr-menu__sign");
            sign.Add(L("Evolution", "gr-menu__title"));
            sign.Add(L("Shop", "gr-menu__title", "gr-menu__title--second"));
            signBox.Add(sign);
            var places = new[] { new Vector2(-6, 0), new Vector2(196, -16), new Vector2(398, 4), new Vector2(-14, 214), new Vector2(404, 206), new Vector2(196, 238) };
            var showcase = GrowthMenu.Showcase(shop);
            for (int i = 0; i < showcase.Count && i < places.Length; i++)
            {
                var (species, colour) = showcase[i];
                var portrait = GrowthPortraits.Get(species, colour);
                if (portrait == null) continue;
                var c = Box("gr-menu__creature");
                c.style.left = places[i].x; c.style.top = places[i].y;
                c.style.rotate = new Rotate(new Angle((i % 2 == 0 ? -1 : 1) * (4 + i * 1.5f), AngleUnit.Degree));
                var picture = Box("gr-menu__portrait");
                picture.style.backgroundImage = new StyleBackground(portrait);
                c.Add(picture);
                c.tooltip = GrowthContent.Get(species).Name + (colour == Colour.Classic ? "" : " · " + colour);
                signBox.Add(c);
                menuCreatures.Add(c);
                MenuShowcase.Add(species);
            }
            panel.Add(signBox);
            panel.Add(L("grow a shop of strange little creatures", "gr-menu__tagline"));

            // The saved shop, and what to do.
            var save = Box("gr-menu__save"); save.name = "gr-menu-save";
            if (s.HasGame)
            {
                save.Add(Eyebrow(s.StartPoint != null ? "Start point: " + s.StartPoint : "Your shop"));
                save.Add(L(GrowthMenu.Summary(shop), "gr-menu__summary"));
            }
            else save.Add(L("A tiny shop and a laboratory are waiting.", "gr-menu__summary"));
            panel.Add(save);

            var buttons = Box("gr-menu__buttons");
            if (menuAskNew)
            {
                var ask = Box("gr-callout"); ask.name = "gr-menu-ask";
                ask.Add(L(GrowthMenu.ReplaceQuestion(shop), "gr-strong"));
                var row = Box("gr-row"); row.style.marginTop = 10;
                var yes = MenuButton("Start a new shop", () => s.NewGame(), "gr-menu-new-yes"); yes.AddToClassList("es-button--danger");
                row.Add(yes);
                row.Add(MenuButton("Keep my shop", () => menuAskNew = false, "gr-menu-new-no"));
                ask.Add(row);
                buttons.Add(ask);
            }
            else
            {
                if (s.HasGame) buttons.Add(MenuButton("Continue", () => s.ContinueGame(), "gr-menu-continue", true));
                buttons.Add(MenuButton("New game", () => { if (s.HasGame) menuAskNew = true; else s.NewGame(); }, "gr-menu-new", !s.HasGame));
                buttons.Add(MenuButton("Settings", () => s.OpenSettings(), "gr-menu-settings"));
                buttons.Add(MenuButton("Quit", () => s.QuitGame(), "gr-menu-quit"));
            }
            panel.Add(buttons);
            if (s.Notice != null) { var notice = Box("gr-callout"); notice.style.marginTop = 12; notice.Add(L(s.Notice, "gr-hint")); panel.Add(notice); }
            panel.Add(L("Esc in the shop pauses it; the pause screen has Main menu.", "gr-menu__foot"));
            return root;
        }

        /// <summary>A menu button: wide, in the display font; primary for the one most wanted.</summary>
        Button MenuButton(string text, System.Action click, string name, bool primary = false)
        {
            var b = Button(text, click, primary);
            b.RemoveFromClassList("es-button--compact");
            b.AddToClassList("gr-menu__button");
            b.name = name;
            return b;
        }

        /// <summary>Each frame: the creatures bob around the sign (still with reduced motion), and the fade follows the shots.</summary>
        void TickMenu()
        {
            if (s.Card != GrowthCard.Menu || card == null) return;
            if (menuFade != null) menuFade.style.opacity = s.MenuCamera.Fade;
            if (s.ReducedMotion) return;
            float t = Time.unscaledTime;
            for (int i = 0; i < menuCreatures.Count; i++)
                menuCreatures[i].style.translate = new Translate(0, Mathf.Sin(t * 1.3f + i * 1.7f) * 5f);
        }
    }
}
