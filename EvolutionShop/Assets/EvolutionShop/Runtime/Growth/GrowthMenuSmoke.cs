#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-27's Windows route (-evolutionGrowthMenuSmoke): the main menu. As at a first launch (nothing saved): the menu over the
    /// live shop, New game first, the first family's founders around the title; New game straight into the welcome. Over an
    /// established shop (the look start point): Continue with the shop in a line, the creatures found around the title as
    /// their own portraits, the slow view's shots past the stocked tanks, the counter and the back room, each a clear view and
    /// a glide, dissolving through a fade; New game asking first and Keep my shop; Settings from the menu and back to it;
    /// Continue into the shop with the camera given back; the pause screen's Main menu (the shop saved first); and Quit's save.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string MenuScenarioText = "GR-27 the main menu: at a first launch, the menu over the live shop with New game first and the first family's founders around the title, New game into the welcome; over an established shop, Continue with the shop in a line, the creatures found as portraits around the title, the slow view's shots past the tanks, the counter and the laboratory with a fade between, New game asking first, Settings and back, Continue with the camera given back, the pause screen's Main menu saving first, Esc staying on the menu, reduced motion holding the view still";

        IEnumerator MenuScenario()
        {
            limitations.Add("GR-27 menu route: the first launch is staged (GrowthSession.ValidationFirstLaunch, labelled: a fresh shop, nothing saved); the established shop is the look start point (GrowthStarts.Look, labelled). The menu's buttons and the pause screen's Main menu are clicked with the route's mouse and Esc pressed with its keyboard; Settings is closed as its Done does; reduced motion is the setting switched in code (labelled); Quit is not pressed (it would end the run); the slow view is moved to each shot's middle for its capture (GrowthMenuCamera.ShowShot, labelled), and its fade measured as it runs.");
            yield return Opening();
            TakeInput();
            Check(s.Card == GrowthCard.None && !s.MenuCamera.Active, "A validation run opens on its route, not the menu");

            // ---- as at a first launch: nothing saved
            s.ValidationFirstLaunch();
            s.OpenMenu();
            yield return null; yield return null; yield return null;
            string text = s.Ui.CardText();
            Check(s.Card == GrowthCard.Menu && s.Paused && text.Contains("Evolution") && text.Contains("Shop") && text.Contains("New game") && !text.Contains("Continue") && text.Contains("Settings") && text.Contains("Quit"),
                "First launch: the menu with the title, New game, Settings and Quit (no Continue: nothing saved)");
            var founders = GrowthContent.Species.Where(x => x.Family == GrowthContent.Families[0].Id && x.Kind == SpeciesKind.Founder).Select(x => x.Id).ToList();
            Check(s.Ui.MenuShowcase.Count == founders.Count && s.Ui.MenuShowcase.All(founders.Contains), "Around the title, the first family's founders: " + string.Join(", ", s.Ui.MenuShowcase));
            yield return new WaitForSeconds(1.2f);
            yield return Shot("1000-menu-first-launch");
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            Check(s.Card == GrowthCard.Menu, "Esc on the menu stays on it (it is where the game starts)");
            yield return ClickOn(e => e.name == "gr-menu-new", "Click New game");
            yield return null; yield return null;
            Check(s.Card == GrowthCard.Welcome && s.HasGame && !s.MenuCamera.Active, "New game, with nothing saved, goes straight to the welcome; the camera is the player's again");
            s.SetCard(GrowthCard.None);

            // ---- an established shop
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Look));
            var shop = s.Shop;
            yield return null; yield return null;
            shop.Credits += 7; // a change since the last save, which Main menu must keep
            s.SetCard(GrowthCard.Pause);
            yield return ClickOn(e => e.name == "gr-pause-menu", "Click Main menu on the pause screen");
            yield return null; yield return null;
            var saved = JsonUtility.FromJson<GrowthSave>(System.IO.File.ReadAllText(s.SavePath));
            Check(saved.credits == shop.Credits, "Main menu saves the shop first (" + saved.credits + " credits in the save)");
            text = s.Ui.CardText();
            Check(s.Card == GrowthCard.Menu && text.Contains(GrowthMenu.Summary(shop)) && text.Contains("Continue") && text.Contains("New game"), "The pause screen's Main menu: Continue with the shop in a line (" + GrowthMenu.Summary(shop) + ")");
            Check(s.Ui.MenuShowcase.Count == GrowthMenu.ShowcaseCount && s.Ui.MenuShowcase.All(shop.Discovered.Contains), "Around the title, " + GrowthMenu.ShowcaseCount + " creatures found: " + string.Join(", ", s.Ui.MenuShowcase));
            foreach (var species in s.Ui.MenuShowcase)
            {
                var portrait = GrowthPortraits.Get(species, GrowthMenu.RarestColour(shop, species));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(evidence, "1030-portrait-" + species + ".png"), portrait.EncodeToPNG());
                float covered = GrowthPortraits.Coverage(portrait);
                Check(covered > 0.05f && covered < 0.9f, GrowthContent.Get(species).Name + "'s portrait is the creature on a clear background (" + (covered * 100).ToString("0") + "% of it)");
            }
            var shots = s.MenuCamera.Shots;
            Check(shots.Count >= 4 && shots.Count(x => x.Target.Kind == GrowthTargetKind.Tank) == GrowthMenuCamera.MostTanks && shots.Any(x => x.Target.Kind != GrowthTargetKind.Tank),
                "The slow view has " + shots.Count + " shots, " + GrowthMenuCamera.MostTanks + " tanks and the rooms: " + string.Join(", ", shots.Select(x => x.Name)));
            for (int i = 0; i < shots.Count; i++)
            {
                s.MenuCamera.ShowShot(i);
                yield return null; yield return null;
                var cam = s.Player.View;
                var shot = shots[i];
                Vector3 vp = cam.WorldToViewportPoint(shot.Look);
                Check(vp.z > 0 && vp.x > 0.45f && vp.x < 0.9f && vp.y > 0.2f && vp.y < 0.85f, "Shot " + (i + 1) + ", " + shot.Name + ": in view right of the panel (" + vp.x.ToString("0.00") + ", " + vp.y.ToString("0.00") + ")");
                yield return Shot("10" + (10 + i) + "-menu-shot-" + (i + 1));
            }
            // The fade between shots, measured as the view runs.
            float darkest = 0;
            s.MenuCamera.ShowShot(0);
            float until = Time.realtimeSinceStartup + GrowthMenuCamera.ShotSeconds * 0.5f + 1.5f;
            while (Time.realtimeSinceStartup < until) { darkest = Mathf.Max(darkest, s.MenuCamera.Fade); yield return null; }
            Check(darkest > 0.6f && s.MenuCamera.ShotIndex == 1 % shots.Count, "The shots dissolve through a fade (darkest " + darkest.ToString("0.00") + ") into the next");
            // Reduced motion holds each shot still; otherwise it glides.
            s.ReducedMotion = true;
            s.MenuCamera.ShowShot(0);
            yield return null;
            Vector3 still = s.Player.View.transform.position;
            yield return new WaitForSeconds(1f);
            Check(Vector3.Distance(still, s.Player.View.transform.position) < 0.001f, "With reduced motion the view holds each shot still");
            s.ReducedMotion = false;
            yield return null;
            Vector3 glide = s.Player.View.transform.position;
            yield return new WaitForSeconds(1f);
            float moved = Vector3.Distance(glide, s.Player.View.transform.position);
            Check(moved > 0.02f && moved < 0.3f, "Otherwise it glides slowly (" + (moved * 100).ToString("0") + " cm in a second)");

            // New game asks first; Keep my shop.
            yield return ClickOn(e => e.name == "gr-menu-new", "Click New game");
            text = s.Ui.CardText();
            Check(text.Contains(GrowthMenu.ReplaceQuestion(shop)) && text.Contains("Start a new shop") && text.Contains("Keep my shop"), "New game asks first: " + GrowthMenu.ReplaceQuestion(shop));
            yield return Shot("1020-menu-new-game-asks");
            yield return ClickOn(e => e.name == "gr-menu-new-no", "Click Keep my shop");
            Check(s.Shop == shop && s.Ui.CardText().Contains("Continue"), "Kept: the menu again, the shop as it was");

            // Settings from the menu, and back to it.
            yield return ClickOn(e => e.name == "gr-menu-settings", "Click Settings");
            Check(s.Card == GrowthCard.Settings && s.MenuCamera.Active && s.Ui.CardText().Contains("Back to the menu"), "Settings over the menu's view, its way back the menu");
            yield return Shot("1021-menu-settings");
            s.CloseSettings();
            yield return null;
            Check(s.Card == GrowthCard.Menu, "Closing Settings goes back to the menu");

            // Continue: into the shop, the camera given back.
            yield return ClickOn(e => e.name == "gr-menu-continue", "Click Continue");
            yield return null;
            Check(s.Card == GrowthCard.None && !s.MenuCamera.Active && Vector3.Distance(s.Player.View.transform.localPosition, new Vector3(0, 1.6f, 0)) < 0.001f && s.Shop == shop,
                "Continue: into the shop where the player was, the camera the player's again");
            yield return Shot("1022-after-continue");
        }
    }
}
#endif
