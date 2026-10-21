#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // UX-03: the UI Toolkit HUD through real night, open, closing, report and tutorial states. Every state checks
        // that no visible HUD element overlaps another, covers the crosshair area or leaves the screen.
        IEnumerator ToolkitHudScenario()
        {
            limitations.Add("UX-03 HUD route: states are reached through the session's own commands (New game, guided opening, OpenForDay, SpawnNow, ServeCustomer, CloseForNight, TryStartComedy) and scripted aims; no physical input. Captures are screen-only.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready && ui.Hud != null, "UI Toolkit root and HUD load" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready || ui.Hud == null) yield break;
            GateFresh();
            Check(ui.Strings.Has("hud.day.night") && ui.Strings.Has("hud.status.waiting"), "HUD strings load (" + ui.Strings.Count + " entries)");
            Check(shop.ToolkitHudActive && ui.Active, "The UI Toolkit HUD is on from start-up; since UX-09 it is the only HUD");
            var hud = ui.Hud;
            var root = hud.Element;
            yield return HudFrames(4);
            Check(hud.IsShown && root.panel != null, "HUD is attached and shown");

            // Night, looking at the incubator: day chip, money, next step and the prompt beside the crosshair.
            var incubator = StationOf(StationKind.Incubator);
            GateAim(incubator.transform.TransformPoint(new Vector3(0, .2f, -1.8f)), ShopUiLayout.AnchorPoint(incubator));
            yield return HudFrames(4);
            Check(HudText(root, "es-hud-day-title") == ui.Strings.Format("hud.day.night", shop.Cycle.DayNumber.ToString("N0")) &&
                HudText(root, "es-hud-day-detail") == ui.Strings.Get("hud.day.noRush"), "Day chip reads " + HudText(root, "es-hud-day-title") + " / " + HudText(root, "es-hud-day-detail"));
            Check(HudText(root, "es-hud-money-amount") == shop.Model.Currency.ToString("N0"), "Money chip shows the credits (" + HudText(root, "es-hud-money-amount") + ")");
            Check(hud.NextStepShown && HudText(root, "es-hud-next-text") == hud.State.Objective, "Next step shows the current objective");
            Check(HudShown(root, HudPresenter.PromptName) && HudText(root, "es-hud-prompt-name") == ui.Strings.Get("card.title.Incubator") &&
                HudText(root, "es-hud-prompt-status") == "· " + ui.Strings.Get("hud.status.empty") && HudText(root, "es-hud-prompt-key") == "E",
                "Prompt beside the crosshair: [" + HudText(root, "es-hud-prompt-key") + "] " + HudText(root, "es-hud-prompt-name") + " " + HudText(root, "es-hud-prompt-status"));
            Check(HudShown(root, HudPresenter.CrosshairName), "Crosshair shows in play");
            CheckHudLayout(hud, "Night at the incubator");
            yield return HudShot("ux03-night");

            // Nothing in view and nothing happening: only the three persistent elements.
            GateAim(shop.World.SpawnPosition, shop.World.SpawnPosition + new Vector3(0, 7f, 1f));
            shop.Notify("", 0);
            yield return HudFrames(4);
            string persistent = HudItems(root);
            Check(persistent == "es-hud-day,es-hud-money,es-hud-next-card", "HUD keeps at most three persistent elements (" + persistent + ")");
            CheckHudLayout(hud, "Quiet night");
            yield return HudShot("ux03-quiet");
            yield return new WaitForSecondsRealtime(ShopHudRules.NextStepSeconds + .7f);
            Check(!hud.NextStepShown && HudItems(root) == "es-hud-day,es-hud-money", "Next step fades after ten seconds without a change (" + HudItems(root) + ")");
            yield return HudShot("ux03-faded");

            // A notice is a toast in the top-centre column.
            shop.Notify("Shelves dusted. The fish are unimpressed.", 6);
            yield return HudFrames(3);
            Check(HudShown(root, "es-hud-toast-notice") && HudToastText(root, "es-hud-toast-notice") == "Shelves dusted. The fish are unimpressed.", "Notice shows as a toast");
            CheckHudLayout(hud, "Notice");
            yield return HudShot("ux03-notice");
            shop.Notify("", 0);

            // A real hatch while facing away: discovery stamp, ready toast and an edge arrow to the incubator.
            GateAim(new Vector3(0, .2f, -3.8f), new Vector3(0, 1.4f, -9f));
            GateMature();
            yield return HudFrames(4);
            string pondlet = shop.Model.GetSpecies("pondlet").DisplayName;
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.DiscoveryRevealActiveForValidation, "A real hatch makes the incubator ready and reveals a discovery");
            Check(HudShown(root, HudPresenter.DiscoveryName) && HudText(root, "es-hud-discovery-name") == pondlet, "Discovery stamp names " + HudText(root, "es-hud-discovery-name"));
            string pingText = ui.Strings.Format("hud.ping.ready", pondlet);
            bool pingShown = HudToastShownWith(root, pingText);
            Check(pingShown || hud.ToastsHeldBack > 0, "Ready ping toast " + (pingShown ? "shows" : "waits for room under the stamp (" + hud.ToastsHeldBack + " held back)") + ": " + pingText);
            Check(hud.ArrowsShown >= 1, "Edge arrow points to the off-screen incubator (" + hud.ArrowsShown + " shown)");
            Check(hud.LastArrowDirection.y > 0.3f, "The arrow points down, behind the player, towards the incubator (" + hud.LastArrowDirection + ")");
            // UX-08: the stamp comes down from above (larger) for a quarter second; its layout is checked once it has landed.
            yield return UiSettled();
            CheckHudLayout(hud, "Discovery and ready ping");
            yield return new WaitForSecondsRealtime(.5f);
            CheckHudLayout(hud, "Discovery and ready ping, settled");
            yield return HudShot("ux03-discovery-ping");
            float stampEnds = Time.realtimeSinceStartup + 5f;
            while (shop.DiscoveryRevealActiveForValidation && Time.realtimeSinceStartup < stampEnds) yield return null;
            yield return HudFrames(3);
            Check(!HudShown(root, HudPresenter.DiscoveryName) && HudToastShownWith(root, pingText), "Once the stamp is gone the ready toast is visible");
            CheckHudLayout(hud, "Ready ping after the stamp");

            // Stock two displays, open the shop and let two customers queue.
            Ok(shop.TransferToDisplay("tank-a"), "Hatched creature goes to display A");
            SeedStock("bubblebelly", "tank-b");
            Check(shop.OpenForDay(), "Shop opens through the ordinary command");
            shop.SetPanel(ShopPanel.None);
            var first = Spawn(CustomerArchetypes.Casual);
            var second = Spawn(CustomerArchetypes.Casual);
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!(first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, "Two customers queue at the counter");
            var counter = StationOf(StationKind.Counter);
            HudFaceCounter(counter);
            yield return HudFrames(4);
            Check(HudText(root, "es-hud-day-title") == ui.Strings.Format("hud.day.day", shop.Cycle.DayNumber.ToString("N0")) &&
                HudText(root, "es-hud-day-detail").EndsWith(" left", StringComparison.Ordinal), "Open day chip: " + HudText(root, "es-hud-day-title") + " / " + HudText(root, "es-hud-day-detail"));
            Check(HudShown(root, HudPresenter.QueueName) && HudFaces(root, out _) == 2, "Queue strip shows one face per waiting customer");
            Check(HudText(root, "es-hud-prompt-status") == "· " + ui.Strings.Format("hud.status.waiting", 2), "Counter prompt: " + HudText(root, "es-hud-prompt-status"));
            CheckHudLayout(hud, "Open with a queue");
            yield return HudShot("ux03-open-queue");

            // A comedy beat: its caption is a bubble near the speaker, never on the crosshair.
            yield return GateStart(ComedyKind.Complaint);
            yield return HudFrames(4);
            Check(shop.Comedy.Active && HudShown(root, HudPresenter.BubbleName) && !float.IsNaN(hud.BubbleRect.x) &&
                HudText(root, "es-hud-bubble-text") == shop.Comedy.Caption, "Comedy caption shows as a bubble (" + HudText(root, "es-hud-bubble-text") + ")");
            CheckHudLayout(hud, "Comedy caption");
            yield return HudShot("ux03-comedy");
            shop.CancelComedy();

            // Looking at a customer up close: the slap prompt joins the tag beside the crosshair.
            yield return AimForSlap(first);
            yield return HudFrames(3);
            Check(HudShown(root, HudPresenter.SlapName) && HudText(root, "es-hud-slap-key") == shop.SlapKey &&
                HudText(root, "es-hud-slap-status") == "· " + ui.Strings.Get("hud.status.ready"), "Slap prompt: [" + HudText(root, "es-hud-slap-key") + "] " + HudText(root, "es-hud-slap-name") + " " + HudText(root, "es-hud-slap-status"));
            CheckHudLayout(hud, "Slap prompt");
            yield return HudShot("ux03-slap");

            // Patience running low turns the faces coral.
            float left = Mathf.Min(first.QueueSecondsRemaining, second.QueueSecondsRemaining);
            shop.Advance(Mathf.Max(0f, left - 8f));
            HudFaceCounter(counter);
            yield return HudFrames(3);
            Check(HudFaces(root, out int low) == 2 && low >= 1, "Low patience turns a face coral (" + low + " low)");
            CheckHudLayout(hud, "Low patience");
            yield return HudShot("ux03-queue-low");

            // Serving at the counter: rep (and the saved money change) float under the money chip.
            int repBefore = shop.Reputation.Points;
            shop.Interact(counter);
            yield return HudFrames(2);
            Check(shop.Panel == ShopPanel.Counter && shop.IsAtCounter && shop.ServeCustomer(), "Serve the first customer at the counter");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(3);
            Check(shop.Reputation.Points > repBefore && hud.FloatersShown >= 1, "Sale floaters show (" + hud.FloatersShown + "; rep " + repBefore + " to " + shop.Reputation.Points + ")");
            yield return new WaitForSecondsRealtime(.3f);
            CheckHudLayout(hud, "Sale floaters");
            yield return HudShot("ux03-sale");

            // Closing.
            Check(shop.CloseForNight(), "Close for the night");
            yield return HudFrames(3);
            Check(HudText(root, "es-hud-day-detail").StartsWith(ui.Strings.Format("hud.day.closing", "").TrimEnd(), StringComparison.Ordinal),
                "Closing day chip: " + HudText(root, "es-hud-day-title") + " / " + HudText(root, "es-hud-day-detail"));
            CheckHudLayout(hud, "Closing");
            yield return HudShot("ux03-closing");

            // The day report is open: the HUD keeps its corners and hides what belongs to play.
            deadline = Time.realtimeSinceStartup + 10f;
            while (shop.Cycle.Phase != ShopDayPhase.Night && Time.realtimeSinceStartup < deadline) yield return null;
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.DayReport, "The day report opens at night");
            Check(HudShown(root, HudPresenter.DayName) && HudShown(root, HudPresenter.MoneyName) && !hud.NextStepShown &&
                !HudShown(root, HudPresenter.PromptName) && !HudShown(root, HudPresenter.CrosshairName), "With a planning panel open only the day and money chips stay (" + HudItems(root) + ")");
            CheckHudLayout(hud, "Day report");
            yield return HudShot("ux03-report");
            shop.SetPanel(ShopPanel.None);

            // A reload is a new baseline; placing decor in the store then floats its appeal.
            StaffCreditFixture(200, "UX-03 decor appeal");
            yield return HudFrames(3);
            Check(hud.FloatersShown == 0, "A reload is a new baseline: no floater for its values (" + hud.FloatersShown + ")");
            int appealBefore = shop.Appeal.Points;
            Vector3 cell = shop.World.transform.TransformPoint(new Vector3(-12 * ShopLayout.GridSize, 0, -8 * ShopLayout.GridSize));
            GateAim(new Vector3(-4.2f, .2f, -4.2f), cell);
            yield return new WaitForSeconds(.2f);
            shop.Player.View.transform.LookAt(cell);
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(null, ShopLayout.CoralPlanterId), "Coral planter placement opens in the store");
            yield return HudFrames(3);
            Check(shop.PlacementValid && shop.CommitPlacement() && shop.Appeal.Points > appealBefore, "Coral planter placed (appeal " + appealBefore + " to " + shop.Appeal.Points + ")");
            yield return HudFrames(3);
            Check(HudFloaterWith(root, ui.Strings.Format("hud.float.appeal", ShopHudRules.Signed(shop.Appeal.Points - appealBefore))), "Appeal floater after placing decor");
            yield return new WaitForSecondsRealtime(.3f);
            CheckHudLayout(hud, "Decor appeal floater");
            yield return HudShot("ux03-decor-appeal");

            // Tutorial: the next step stays, with F1, and the guidance marker sits beside its target.
            // Earlier floaters expire first, so the New game check sees only what New game itself raises.
            yield return new WaitForSecondsRealtime(ShopHudRules.FloaterSeconds + .2f);
            Check(hud.FloatersShown == 0, "Earlier floaters have expired before New game");
            shop.ConfirmNewGame();
            Check(shop.StartGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Active, "Guided opening starts");
            yield return HudFrames(2);
            Check(shop.TryGetTutorialGuidance(out var target), "Tutorial has a guidance target");
            shop.Player.View.transform.LookAt(target.Position);
            yield return HudFrames(4);
            Check(hud.FloatersShown == 0, "New game is a new baseline: its reset values raise no floater (" + hud.FloatersShown + ")");
            yield return new WaitForSecondsRealtime(.6f);
            Check(hud.NextStepShown && HudShown(root, "es-hud-next-help"), "Tutorial next step shows with its F1 key");
            Check(HudShown(root, HudPresenter.MarkerName) && !float.IsNaN(hud.MarkerRect.x) && HudText(root, "es-hud-marker-title") == target.Label,
                "Tutorial marker beside " + target.Label + " (" + HudText(root, "es-hud-marker-action") + ")");
            CheckHudLayout(hud, "Tutorial");
            yield return HudShot("ux03-tutorial");
            yield return new WaitForSecondsRealtime(ShopHudRules.NextStepSeconds + .7f);
            Check(hud.NextStepShown, "During the tutorial the next step stays past ten seconds");

            // Only HUD (UX-09): no other HUD draws, and this one stays.
            Check(NoImmediateModeGui(out string imgui), "No IMGUI path remains: nothing in the game assembly draws with OnGUI" + imgui);
            yield return HudFrames(3);
            Check(hud.IsShown, "The toolkit HUD stays on; there is no classic HUD to hand back to");
        }

        void HudFaceCounter(Station counter)
        {
            var anchor = counter.transform;
            shop.Player.Teleport(anchor.TransformPoint(new Vector3(0, .2f, -2.5f)), anchor.rotation);
            shop.Player.View.transform.LookAt(anchor.TransformPoint(new Vector3(0, 1.1f, 0)));
        }

        static IEnumerator HudFrames(int count) { for (int i = 0; i < count; i++) yield return null; }

        /// <summary>UX-08: waits, at most three seconds, until no feel animation, coin flight, money count or receipt count-up runs.</summary>
        IEnumerator UiSettled()
        {
            var ui = shop.ToolkitUi;
            float until = Time.realtimeSinceStartup + 3f;
            while (ui != null && ui.Ready && ui.Active && !ui.Settled && Time.realtimeSinceStartup < until) yield return null;
        }

        IEnumerator HudShot(string name)
        {
            // Screen-only: the HUD is what is reviewed, and a camera render would not show it. UX-08: a capture shows the
            // settled state, not a card mid-open or coins mid-flight (the feel route captures those on purpose).
            yield return UiSettled();
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, name + "-screen.png"));
            yield return new WaitForSecondsRealtime(.25f);
        }

        static string HudText(VisualElement root, string name)
        {
            var label = root.Q<Label>(name);
            return label != null ? label.text : "<" + name + " missing>";
        }

        static bool HudShown(VisualElement root, string name)
        {
            var element = root.Q(name);
            return element != null && HudPresenter.Visible(element);
        }

        static string HudToastText(VisualElement root, string name)
        {
            var toast = root.Q(name);
            var label = toast?.Q<Label>(className: "es-toast__text");
            return label != null ? label.text : null;
        }

        static bool HudToastShownWith(VisualElement root, string text)
        {
            foreach (var toast in root.Query(className: "es-toast").ToList())
                if (HudPresenter.Visible(toast) && toast.Q<Label>(className: "es-toast__text")?.text == text) return true;
            return false;
        }

        static bool HudFloaterWith(VisualElement root, string text)
        {
            foreach (var floater in root.Query(className: "es-floater").ToList())
                if (floater.Q<Label>(className: "es-floater__text")?.text == text) return true;
            return false;
        }

        static int HudFaces(VisualElement root, out int low)
        {
            low = 0;
            int shown = 0;
            foreach (var face in root.Query<HudFace>().ToList())
                if (HudPresenter.Visible(face)) { shown++; if (face.Low) low++; }
            return shown;
        }

        /// <summary>Names of the visible HUD items, sorted and comma-joined.</summary>
        static string HudItems(VisualElement root)
        {
            var names = new List<string>();
            foreach (var item in root.Query(className: HudPresenter.ItemClass).ToList())
                if (HudPresenter.Visible(item)) names.Add(item.name);
            names.Sort(StringComparer.Ordinal);
            return string.Join(",", names);
        }

        /// <summary>Null when every visible HUD item is on screen, clear of the crosshair area and of every other item.</summary>
        static string HudLayoutProblem(HudPresenter hud)
        {
            var root = hud.Element;
            Vector2 panel = root.layout.size;
            Rect cross = ShopHudRules.CrosshairArea(panel);
            var items = new List<VisualElement>();
            foreach (var item in root.Query(className: HudPresenter.ItemClass).ToList()) if (HudPresenter.Visible(item)) items.Add(item);
            for (int i = 0; i < items.Count; i++)
            {
                Rect a = Shrink(items[i].worldBound);
                if (a.xMin < 0 || a.yMin < 0 || a.xMax > panel.x || a.yMax > panel.y) return items[i].name + " leaves the screen " + a;
                if (a.Overlaps(cross)) return items[i].name + " covers the crosshair area " + a;
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (items[j].FindCommonAncestor(items[i]) == items[i] || items[i].FindCommonAncestor(items[j]) == items[j]) continue;
                    Rect b = Shrink(items[j].worldBound);
                    if (a.Overlaps(b)) return items[i].name + " overlaps " + items[j].name + " " + a + " / " + b;
                }
            }
            return null;
        }

        static Rect Shrink(Rect r) => new Rect(r.x + .5f, r.y + .5f, Mathf.Max(0, r.width - 1), Mathf.Max(0, r.height - 1));

        void CheckHudLayout(HudPresenter hud, string state)
        {
            string problem = HudLayoutProblem(hud);
            Check(problem == null, state + ": no HUD element overlaps another, covers the crosshair area or leaves the screen" + (problem != null ? " / " + problem : ""));
            // UX-09: a toast or the next step once kept a one-line height while its text wrapped onto a clipped second line.
            string clipped = HudClippedText(hud.Element);
            Check(clipped == null, state + ": every toast and next-step line fits its card" + (clipped != null ? " / " + clipped : ""));
        }

        /// <summary>The first toast or next-step text whose wrapped lines need more height than its box has; null when all fit.</summary>
        static string HudClippedText(VisualElement root)
        {
            // The wrapping texts only; key caps are fixed-height glyphs.
            foreach (string textClass in new[] { "es-toast__text", "es-next__text" })
                foreach (var label in root.Query<Label>(className: textClass).ToList())
                {
                    if (!HudPresenter.Visible(label) || !HudPresenter.Visible(label.parent) || string.IsNullOrEmpty(label.text)) continue;
                    float width = label.contentRect.width;
                    if (float.IsNaN(width) || width <= 0) continue;
                    var need = label.MeasureTextSize(label.text, width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                    if (need.y > label.contentRect.height + 1) return "'" + label.text + "' needs " + need.y.ToString("0") + " px at " + width.ToString("0") + " wide, has " + label.contentRect.height.ToString("0");
                }
            return null;
        }
    }
}
#endif
