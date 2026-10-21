#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // UX-07: the screens through a real guided opening. New game offers the welcome card; the guided opening runs with
        // real walking, held collection, E and the station cards, with help (F1) at its steps and the tutorial's callouts
        // in the incubator, display and counter cards. Day one has the first sale and the practice sale, then closes early
        // into the guided receipt, whose Next night reaches the next goal; night two has the journal (TAB) and pause with its
        // settings and New game step (Esc), then help's next goal, its Siphon page and the journal from help. Then the welcome for a finished opening and
        // Reload. Screen controls are pressed with the synthetic Input System mouse and keyboard (CardClick, ToolkitCardsSmoke.cs).
        IEnumerator ToolkitScreensScenario()
        {
            limitations.Add("UX-07 screens route: a real guided opening with synthetic WASD, mouse look, held collection, E, F1, TAB and Esc, and screen controls pressed with synthetic Input System devices, not physical hardware. Station cards open through the session's Interact after a scripted aim, as in the UX-04 route; cultures are dosed with the incubator's own commands. Customer arrivals are switched on only while waiting for each checkout; the practice sale is made the same day, and one labelled scripted visitor then walks in to empty displays so the receipt has a missed visitor. The finished-opening welcome is shown with a labelled SetPanel, as a restart would. Captures are screen-only.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready && ui.Screens != null, "UI Toolkit root and screens load" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready || ui.Screens == null) yield break;
            var screens = ui.Screens;
            var card = ui.StationCard;
            var hud = ui.Hud;
            var root = ui.Root;
            var s = ui.Strings;
            shop.enabled = true; shop.SetPanel(ShopPanel.None);
            CardInput();
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            Check(shop.ToolkitUiActive && ui.Active, "The UI Toolkit UI is on from start-up; since UX-09 it is the only UI");
            yield return HudFrames(3);

            // ---------- welcome ----------
            shop.ConfirmNewGame();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            yield return HudFrames(4);
            Check(shop.Panel == ShopPanel.Welcome && shop.Tutorial.Mode == TutorialMode.Offered && screens.IsShown && screens.CurrentView == screens.Welcome && shop.Paused,
                "New game opens the welcome card, paused, with the guided opening offered (panel " + shop.Panel + ", shown " + screens.IsShown + ")");
            Check(root.Q<Button>(WelcomeView.PrimaryName).text == s.Get("welcome.primary.offered") && root.Q<Button>(WelcomeView.SecondaryName).text == s.Get("welcome.secondary.offered") &&
                CardShown(root, WelcomeView.HelpName) && !CardShown(root, WelcomeView.SkipName) && !CardShown(root, WelcomeView.StepName),
                "Welcome offers " + root.Q<Button>(WelcomeView.PrimaryName).text + " or " + root.Q<Button>(WelcomeView.SecondaryName).text + ", with Help");
            Check(ui.Hud.IsShown && CardShown(root, HudPresenter.MoneyName) && CardShown(root, HudPresenter.DayName) && !hud.NextStepShown && !CardShown(root, HudPresenter.CrosshairName),
                "Over the welcome only the day and money chips stay");
            yield return HudShot("ux07-welcome");
            CheckScreen("Welcome, offered", 1);
            yield return CardClick(root.Q<Button>(WelcomeView.HelpName), "Help");
            Check(shop.Panel == ShopPanel.TutorialHelp && screens.CurrentView == screens.Help && screens.Help.State.Lesson == TutorialLesson.Orient && !screens.Help.State.TutorialActive &&
                root.Q<Label>(HelpView.NoteName).text == s.Get("help.note.reference") && !CardShown(root, HelpView.SkipName),
                "Help from the welcome shows the steps as reference cards");
            yield return HudShot("ux07-help-reference");
            CheckScreen("Help, reference", 0);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Welcome && screens.CurrentView == screens.Welcome, "Esc goes back to the welcome card");
            yield return CardClick(root.Q<Button>(WelcomeView.PrimaryName), "Start guided opening");
            yield return HudFrames(3);
            Check(shop.Tutorial.Mode == TutorialMode.Active && shop.Panel == ShopPanel.None && !screens.IsShown && shop.ActiveTutorialLesson == TutorialLesson.Orient,
                "A real click starts the guided opening and goes back to play");
            Check(hud.NextStepShown && HudText(root, "es-hud-next-text") == s.Get("tut.obj.orient"),
                "The HUD's next step uses the toolkit wording: " + HudText(root, "es-hud-next-text"));

            // ---------- help at the first step ----------
            yield return CardKey(Key.F1);
            yield return HudFrames(2);
            var help = screens.Help;
            Check(shop.Panel == ShopPanel.TutorialHelp && shop.Paused && screens.CurrentView == help && help.State.OnActiveLesson && help.State.Page == HelpPage.Lesson,
                "F1 opens help, paused, on the current step");
            Check(root.Q<Label>(HelpView.EyebrowName).text == s.Format("help.step", 1) && root.Q<Label>(HelpView.LessonTitleName).text == s.Get("help.lesson.orient") &&
                root.Q<Label>(HelpView.DoName).text == s.Get("help.lesson.orient.do") && root.Q(HelpView.StepName(TutorialLesson.Orient)).ClassListContains("es-screen-step--now") &&
                CardShown(root, HelpView.SkipName), "Step 1, " + root.Q<Label>(HelpView.LessonTitleName).text + ": " + root.Q<Label>(HelpView.DoName).text);
            int steps = 0;
            for (var lesson = TutorialLesson.Orient; lesson <= TutorialLesson.NextPlan; lesson++)
                if (CardShown(root, HelpView.StepName(lesson)) && root.Q<Label>(HelpView.StepName(lesson) + "-number").text == ScreenRules.LessonNumber(lesson).ToString()) steps++;
            Check(steps == 10 && CardShown(root, HelpView.ControlsStepName), "The step list numbers all ten steps, with Controls after them");
            yield return HudShot("ux07-help-step");
            CheckScreen("Help, step 1", CardShown(root, HelpView.ShowName) ? 1 : 0);
            yield return CardClick(root.Q<Button>(HelpView.ControlsStepName), "Controls");
            Check(help.ControlsShown && help.State.Page == HelpPage.Controls && root.Q<Label>("es-help-key-slap-cap").text == shop.SlapKey, "The Controls page lists the keys, with the slap key as set");
            yield return HudShot("ux07-help-controls");
            CheckScreen("Help, controls", 0);
            yield return CardClick(root.Q<Button>(HelpView.StepName(TutorialLesson.Display)), "Step 5");
            Check(help.State.Lesson == TutorialLesson.Display && !help.State.OnActiveLesson && root.Q<Label>(HelpView.NoteName).text == s.Get("help.note.reading") &&
                CardShown(root, HelpView.CurrentName), "Reading ahead says so, with a way back to the current step");
            yield return CardClick(root.Q<Button>(HelpView.CurrentName), "Back to the current step");
            Check(help.State.Lesson == TutorialLesson.Orient, "The link goes back to step 1");
            yield return CardClick(root.Q<Button>(HelpView.SkipName), "Skip the guided opening…");
            Check(CardShown(root, HelpView.SkipConfirmName) && !CardShown(root, HelpView.NowName) && shop.Tutorial.Mode == TutorialMode.Active, "Skipping asks first, in place of the shop-right-now line");
            yield return HudShot("ux07-help-skip");
            CheckScreen("Help, skip asks", CardShown(root, HelpView.ShowName) ? 1 : 0);
            yield return CardClick(root.Q<Button>(HelpView.SkipKeepName), "Keep the guidance");
            Check(!CardShown(root, HelpView.SkipConfirmName) && CardShown(root, HelpView.NowName) && shop.Tutorial.Mode == TutorialMode.Active, "Keep backs out");
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.None && !shop.Paused, "Esc closes help and play resumes");

            // ---------- orient and collect (real walking, look and held collection) ----------
            shop.AllowValidationInput = true;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(24, -8) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState()); yield return null;
            yield return CycleGoStation(StationKind.Bloom);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Collect, "Real look and walking finish orientation");
            yield return HudFrames(2);
            Check(HudText(root, "es-hud-next-text").StartsWith("Collect ", System.StringComparison.Ordinal) && !HudText(root, "es-hud-next-text").Contains("left mouse."),
                "Collect's next step uses the toolkit wording: " + HudText(root, "es-hud-next-text"));
            yield return CardKey(Key.F1);
            Check(shop.Panel == ShopPanel.TutorialHelp && help.State.Lesson == TutorialLesson.Collect && CardShown(root, HelpView.ShowName), "Help at step 2 can show where to go");
            string recipe = help.State.Recipe;
            yield return CardClick(root.Q<Button>(HelpView.ShowName), "Show me the way");
            Check(shop.Panel == ShopPanel.None && shop.TryGetTutorialGuidance(out _), "Show me the way closes help and points the way");
            yield return CycleHarvest(CultureKind.Bloom);
            yield return CycleHarvest(CultureKind.Flow);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Prepare, "Both cultures collected: the prepare step");

            // ---------- the incubator card's callout ----------
            yield return CycleGoStation(StationKind.Incubator);
            yield return CycleTap(Key.E);
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Incubator && card.IsShown && CardShown(root, StationCardPresenter.CalloutName) && card.Callout == shop.TutorialLocalCallout &&
                card.Callout == s.Format("tut.call.incDose", recipe), "A real E opens the incubator card with the tutorial's callout: " + card.Callout);
            yield return HudShot("ux07-callout-incubator");
            CheckCard(card, "Incubator, guided", 1);
            Check(shop.DoseCulture(CultureKind.Flow) && shop.DoseCulture(CultureKind.Bloom) && shop.ActiveTutorialLesson == TutorialLesson.Grow, "The recipe is dosed: the grow step");
            yield return HudFrames(2);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Start");
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Panel == ShopPanel.None, "A real click on Start begins the batch");
            yield return CardKey(Key.F1);
            yield return HudFrames(2);
            Check(shop.Panel == ShopPanel.TutorialHelp && help.State.Lesson == TutorialLesson.Grow && CardShown(root, "es-help-now-incubator") &&
                root.Q<Label>("es-help-now-incubator").text == s.Format("help.now.growing", help.State.IncubatorName, help.State.IncubatorSeconds),
                "Help at the grow step shows the shop right now, the batch growing: " + root.Q<Label>("es-help-now-incubator").text);
            yield return HudShot("ux07-help-grow");
            CheckScreen("Help, growing", CardShown(root, HelpView.ShowName) ? 1 : 0);
            yield return CardKey(Key.Escape);
            float until = Time.realtimeSinceStartup + 17f;
            while (shop.Model.Incubator.Phase != IncubatorPhase.Ready && Time.realtimeSinceStartup < until) yield return null;
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready, "The batch hatches");

            // ---------- display, price and open ----------
            yield return CycleGoStation(StationKind.TankA);
            yield return CycleTap(Key.E);
            Check(shop.Model.GetTank("tank-a").HasValue, "A real E at the empty display stocks it from the incubator");
            yield return CardOpenTank("tank-a", StationOf(StationKind.TankA));
            Check(shop.Panel == ShopPanel.Tank && card.IsShown && card.Callout == s.Get("tut.call.tankPrice"), "The display's price card carries the callout: " + card.Callout);
            yield return HudShot("ux07-callout-tank");
            CheckCard(card, "Display, guided price", 0);
            var counter = StationOf(StationKind.Counter);
            yield return CardOpenCounter(counter);
            Check(shop.Panel == ShopPanel.Counter && card.IsShown && card.Callout == s.Get("tut.call.counterNight"), "The counter card at night: " + card.Callout);
            yield return HudShot("ux07-callout-counter");
            CheckCard(card, "Counter, guided night", 1);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Open shop");
            Check(shop.Cycle.Phase == ShopDayPhase.Open && shop.ActiveTutorialLesson == TutorialLesson.Serve, "A real click opens the shop: the serve step");
            yield return ScreensCheckout();
            yield return CardOpenCounter(counter);
            Check(card.Callout == s.Get("tut.call.counterServe"), "With an order waiting the counter says: " + card.Callout);
            yield return HudShot("ux07-callout-serve");
            CheckCard(card, "Counter, guided serve", 1);
            int money = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Serve");
            Check(shop.Model.Currency > money && shop.ActiveTutorialLesson == TutorialLesson.Practice, "A real click serves the first sale: the practice step");

            // ---------- the practice sale, the same day ----------
            shop.SetPanel(ShopPanel.None);
            until = Time.realtimeSinceStartup + 30f;
            while (shop.Customers.ActiveCount > 0 && Time.realtimeSinceStartup < until) yield return null;
            cycleStarted = Time.realtimeSinceStartup;
            yield return CycleHarvest(CultureKind.Bloom);
            yield return CycleHarvest(CultureKind.Flow);
            yield return CycleGoStation(StationKind.Incubator);
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Incubator && shop.DoseCulture(CultureKind.Flow) && shop.DoseCulture(CultureKind.Bloom), "Practice: the recipe dosed at the incubator");
            yield return HudFrames(2);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Start");
            until = Time.realtimeSinceStartup + 17f;
            while (shop.Model.Incubator.Phase != IncubatorPhase.Ready && Time.realtimeSinceStartup < until) yield return null;
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready, "The practice batch hatches");
            yield return CycleGoStation(StationKind.TankA);
            yield return CycleTap(Key.E);
            Check(shop.Model.GetTank("tank-a").HasValue && shop.Cycle.Phase == ShopDayPhase.Open, "The practice creature goes on display; day 1 is still trading");
            yield return ScreensCheckout();
            yield return CardOpenCounter(counter);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Serve");
            Check(shop.ActiveTutorialLesson == TutorialLesson.CloseAndReview, "The practice sale: the close-and-review step");

            // A labelled scripted visitor walks in to empty displays, so the receipt has a missed visitor.
            shop.SetPanel(ShopPanel.None);
            until = Time.realtimeSinceStartup + 30f;
            while (shop.Customers.ActiveCount > 0 && Time.realtimeSinceStartup < until) yield return null;
            var visitor = Spawn(CustomerArchetypes.Casual);
            until = Time.realtimeSinceStartup + 40f;
            while (ScreenRules.Missed(shop.Reports.Current) == 0 && Time.realtimeSinceStartup < until) yield return null;
            Check(ScreenRules.Missed(shop.Reports.Current) > 0, "The visitor leaves without a creature (" + visitor.Phase + ")");
            yield return CardOpenCounter(counter);
            Check(card.Callout == s.Get("tut.call.counterClose"), "The counter card says: " + card.Callout);
            yield return HudShot("ux07-callout-close");
            CheckCard(card, "Counter, guided close", 1);
            yield return CardClick(root.Q<Button>(CounterCardView.CloseEarlyName), "Close early");
            yield return CardClick(root.Q<Button>(CounterCardView.CloseNowName), "Close now");
            yield return ScreensNight();


            // ---------- the receipt ----------
            var report = screens.Report;
            var r = shop.Reports;
            Check(shop.Panel == ShopPanel.DayReport && screens.CurrentView == report && report.State.Has && report.State.Guided && CardShown(root, ReportView.GuidedName) &&
                root.Q<Label>(ReportView.TitleName).text == s.Format("report.title", 1), "Night falls on day 1's receipt, with the guided line");
            Check(root.Q<Label>(ReportView.SoldName).text == "2" && root.Q<Label>(ReportView.IncomeName).text == "+" + report.State.Revenue.ToString("N0") &&
                root.Q<Label>(ReportView.MissedName).text == report.State.Missed.ToString() && report.State.Missed > 0 && CardShown(root, ReportView.ReasonsName),
                "Income " + root.Q<Label>(ReportView.IncomeName).text + ", 2 sold, " + report.State.Missed + " missed: " + root.Q<Label>(ReportView.ReasonsName).text);
            Check(!CardShown(root, ReportView.DetailsName) && !string.IsNullOrEmpty(root.Q<Label>(ReportView.TipName).text), "Tonight's tip: " + root.Q<Label>(ReportView.TipName).text);
            Check(!hud.NextStepShown && CardShown(root, HudPresenter.MoneyName) && CardShown(root, HudPresenter.DayName) && !CardShown(root, HudPresenter.CrosshairName),
                "Over the receipt only the day and money chips stay");
            yield return HudShot("ux07-report");
            CheckScreen("Receipt, guided", 1);
            Rect profitAt = root.Q(ReportView.ProfitName).worldBound;
            yield return CardHover(root.Q(ReportView.ProfitName), "Profit");
            Check(report.DetailsShown && CardShown(root, ReportView.DetailsName), "Hovering Profit shows the details: " + root.Q<Label>(ReportView.DetailsName).text + ScreenPickChain(root.Q(ReportView.ProfitName)));
            Check(root.Q(ReportView.ProfitName).worldBound == profitAt, "The details lie over the receipt without moving it, so the row stays under the pointer");
            yield return HudShot("ux07-report-details");
            CheckScreen("Receipt, details", 1);
            yield return CardClick(root.Q<Button>(ReportView.NextName), "Next night");
            Check(shop.Panel == ShopPanel.None && !screens.IsShown && shop.ActiveTutorialLesson == TutorialLesson.NextPlan, "Next night goes back to play and acknowledges it: the next-goal step");

            // ---------- journal ----------
            yield return CardKey(Key.Tab);
            yield return HudFrames(2);
            var journal = screens.Journal;
            string pondlet = shop.Model.GetSpecies("pondlet").DisplayName;
            Check(shop.Panel == ShopPanel.Journal && screens.CurrentView == journal && journal.Tab == JournalTab.Creatures &&
                ScreenLabelWith(root.Q(JournalView.CreatureName("pondlet")), pondlet) && !ScreenLabelWith(root.Q(JournalView.CreatureName("ribbonfin")), shop.Model.GetSpecies("ribbonfin").DisplayName),
                "TAB opens the journal on its creatures: " + pondlet + " found, Ribbonfin still unknown");
            Check(root.Q<Label>(ScreenPresenter.TitleName).text == journal.Title && root.Q<Label>(ScreenPresenter.BackTextName).text == s.Get("plan.back") &&
                CardShown(root, HudPresenter.MoneyName) && !CardShown(root, HudPresenter.QueueName), "The journal sits on the notebook sheet, the chips over the dim");
            yield return HudShot("ux07-journal-creatures");
            CheckScreen("Journal, creatures", 0);
            yield return CardClick(root.Q<Button>(JournalView.TabName(JournalTab.Customers)), "Customers");
            int shownCustomers = 0;
            foreach (var c in journal.State.Customers) if (CardShown(root, JournalView.CustomerName(c.Id))) shownCustomers++;
            Check(journal.Tab == JournalTab.Customers && shownCustomers == journal.State.Customers.Count && shownCustomers > 0, "Customers: " + shownCustomers + " kinds of visitor");
            yield return HudShot("ux07-journal-customers");
            CheckScreen("Journal, customers", 0);
            yield return CardClick(root.Q<Button>(JournalView.TabName(JournalTab.Shop)), "Your shop");
            Check(journal.Tab == JournalTab.Shop && CardShown(root, JournalView.ReputationName) && journal.State.Reputation == shop.Reputation.Points, "Your shop: reputation and appeal");
            yield return HudShot("ux07-journal-shop");
            CheckScreen("Journal, shop", 0);
            yield return CardKey(Key.Tab);
            Check(shop.Panel == ShopPanel.None, "TAB again closes the journal");

            // ---------- pause and settings ----------
            yield return CardKey(Key.Escape);
            var pause = screens.Pause;
            Check(shop.Panel == ShopPanel.Pause && shop.Paused && screens.CurrentView == pause && pause.Tab == SettingsTab.Controls && CardShown(root, PauseView.SkipName) &&
                root.Q<Label>(PauseView.SaveStatusName).text == s.Get("pause.save.ok"), "Esc pauses: the menu and Settings on Controls");
            yield return HudShot("ux07-pause");
            CheckScreen("Pause, controls", 1);
            float sensitivity = shop.Sensitivity;
            yield return ScreenClickAt(root.Q(PauseView.SensitivityName), .9f, "Sensitivity, near the top");
            Check(shop.Sensitivity > sensitivity && root.Q<Label>(PauseView.SensitivityName + "-value").text == s.Format("pause.sensitivity.value", shop.Sensitivity.ToString("0.00")),
                "A real click raises the sensitivity from " + sensitivity.ToString("0.00") + " to " + shop.Sensitivity.ToString("0.00"));
            string slapKey = shop.SlapKey;
            yield return CardClick(root.Q<Button>(PauseView.SlapChangeName), "Change slap key");
            Check(shop.WaitingForSlapKey && CardShown(root, PauseView.SlapWaitName) && root.Q<Label>(PauseView.SlapKeyName).text == "…", "Change waits for a letter");
            yield return HudShot("ux07-pause-slap");
            CheckScreen("Pause, waiting for a key", 1);
            yield return CardKey(Key.Escape);
            Check(!shop.WaitingForSlapKey && shop.Panel == ShopPanel.Pause && shop.SlapKey == slapKey, "Esc cancels the rebind and stays paused");
            yield return CardClick(root.Q<Button>(PauseView.TabName(SettingsTab.Sound)), "Sound");
            Check(pause.Tab == SettingsTab.Sound && CardShown(root, PauseView.VolumeName), "The Sound tab");
            float volume = shop.Volume;
            yield return ScreenClickAt(root.Q(PauseView.VolumeName), .5f, "Volume, halfway");
            Check(Mathf.Abs(shop.Volume - volume) > .01f && Mathf.Approximately(AudioListener.volume, shop.Volume) &&
                root.Q<Label>(PauseView.VolumeName + "-value").text == s.Format("pause.percent", ScreenRules.Percent(shop.Volume)), "A real click sets the volume to " + root.Q<Label>(PauseView.VolumeName + "-value").text);
            yield return HudShot("ux07-pause-sound");
            CheckScreen("Pause, sound", 1);
            yield return CardClick(root.Q<Button>(PauseView.TabName(SettingsTab.Comfort)), "Comfort");
            bool motion = shop.ReducedVisualMotion, gags = shop.ReducedGags;
            yield return CardClick(root.Q<Button>(PauseView.ReducedMotionName), "Reduced motion");
            yield return CardClick(root.Q<Button>(PauseView.DetailLowName), "Low detail");
            yield return CardClick(root.Q<Button>(PauseView.ReducedGagsName), "Fewer gags");
            Check(shop.ReducedVisualMotion != motion && shop.EffectsDetail == FxEffectDetail.Low && shop.ReducedGags != gags &&
                root.Q<PlanSwitch>(PauseView.ReducedMotionName).On == shop.ReducedVisualMotion && root.Q<Button>(PauseView.DetailLowName).ClassListContains("es-plan-seg--on"),
                "Real clicks switch reduced motion, low detail and fewer gags");
            yield return HudShot("ux07-pause-comfort");
            CheckScreen("Pause, comfort", 1);
            yield return CardClick(root.Q<Button>(PauseView.ReducedMotionName), "Reduced motion");
            yield return CardClick(root.Q<Button>(PauseView.DetailStandardName), "Standard detail");
            yield return CardClick(root.Q<Button>(PauseView.ReducedGagsName), "Fewer gags");
            Check(shop.ReducedVisualMotion == motion && shop.EffectsDetail == FxEffectDetail.Standard && shop.ReducedGags == gags, "And back");
            yield return CardClick(root.Q<Button>(PauseView.ReloadName), "Reload save…");
            Check(CardShown(root, PauseView.ReloadConfirmName) && !CardShown(root, PauseView.ReloadName) && !CardShown(root, PauseView.SettingsPageName) && shop.Panel == ShopPanel.Pause,
                "Reload asks first, on the right-hand card");
            yield return HudShot("ux07-pause-reload");
            CheckScreen("Pause, reload asks", 1);
            yield return CardClick(root.Q<Button>(PauseView.ReloadKeepName), "Keep playing");
            Check(!CardShown(root, PauseView.ReloadConfirmName) && shop.Panel == ShopPanel.Pause, "Keep backs out");
            yield return CardClick(root.Q<Button>(PauseView.SkipName), "Skip guidance…");
            Check(CardShown(root, PauseView.SkipConfirmName) && !CardShown(root, PauseView.SettingsPageName) && !CardShown(root, PauseView.SkipName) && shop.Tutorial.Mode == TutorialMode.Active,
                "Skipping from Pause asks on the right-hand card");
            yield return HudShot("ux07-pause-skip");
            CheckScreen("Pause, skip asks", 1);
            yield return CardClick(root.Q<Button>(PauseView.SkipKeepName), "Keep guidance");
            Check(!CardShown(root, PauseView.SkipConfirmName) && CardShown(root, PauseView.SettingsPageName) && shop.Tutorial.Mode == TutorialMode.Active, "Keep guidance goes back to Settings");
            yield return CardClick(root.Q<Button>(PauseView.NewGameName), "New game…");
            Check(shop.Panel == ShopPanel.Reset && screens.CurrentView == pause && CardShown(root, PauseView.ResetName) && !CardShown(root, PauseView.SettingsPageName) &&
                !root.Q<Button>(PauseView.SettingsName).ClassListContains("es-screen-pause__nav--on"), "New game asks on the right-hand card");
            yield return HudShot("ux07-pause-reset");
            CheckScreen("Pause, New game asks", 1);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Pause && CardShown(root, PauseView.SettingsPageName), "Esc from the question goes back to Pause");
            yield return CardClick(root.Q<Button>(PauseView.NewGameName), "New game…");
            yield return CardClick(root.Q<Button>(PauseView.ResetKeepName), "Keep my shop");
            Check(shop.Panel == ShopPanel.Pause && shop.Tutorial.Mode == TutorialMode.Active, "Keep my shop goes back to Pause");
            yield return CardClick(root.Q<Button>(PauseView.ResumeName), "Resume");
            Check(shop.Panel == ShopPanel.None && !shop.Paused, "A real click on Resume goes back to play");

            // ---------- the next goal ----------
            yield return CardKey(Key.F1);
            Check(shop.Panel == ShopPanel.TutorialHelp && help.State.Page == HelpPage.NextPlan && CardShown(root, HelpView.FinishName) && !CardShown(root, HelpView.NowName),
                "Help at the last step chooses a next goal");
            yield return HudShot("ux07-help-next");
            CheckScreen("Help, next goal", 1);
            yield return CardClick(root.Q<Button>(HelpView.UpgradeName), "Siphon MK II");
            Check(help.State.Page == HelpPage.Upgrade && CardShown(root, HelpView.UpgradeBackName), "The Siphon MK II page");
            yield return HudShot("ux07-help-upgrade");
            CheckScreen("Help, Siphon", 0);
            yield return CardClick(root.Q<Button>(HelpView.UpgradeBackName), "Back to the next goal");
            Check(help.State.Page == HelpPage.NextPlan, "Back to the next goal");
            yield return CardClick(root.Q<Button>(HelpView.JournalName), "Open the journal");
            Check(shop.Panel == ShopPanel.Journal && journal.State.ReturnsToHelp && root.Q<Label>(ScreenPresenter.BackTextName).text == s.Get("journal.back.help"),
                "The journal from help says Back to help");
            yield return HudShot("ux07-journal-from-help");
            CheckScreen("Journal from help", 0);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.TutorialHelp && help.State.Page == HelpPage.NextPlan, "Esc goes back to help");
            yield return CardClick(root.Q<Button>(HelpView.FinishName), "Finish the guided opening");
            Check(shop.Tutorial.Mode == TutorialMode.Completed && shop.Panel == ShopPanel.None, "A real click finishes the guided opening");

            // ---------- the welcome for a finished opening, then Reload ----------
            shop.SetPanel(ShopPanel.Welcome);
            yield return HudFrames(3);
            Check(screens.CurrentView == screens.Welcome && root.Q<Label>(WelcomeView.HeadName).text == s.Get("welcome.head.completed") &&
                root.Q<Button>(WelcomeView.PrimaryName).text == s.Get("welcome.primary.done"), "After the opening the welcome reads " + root.Q<Label>(WelcomeView.HeadName).text);
            yield return HudShot("ux07-welcome-done");
            CheckScreen("Welcome, finished", 1);
            yield return CardClick(root.Q<Button>(WelcomeView.PrimaryName), "Continue my shop");
            Check(shop.Panel == ShopPanel.None, "Continue my shop goes back to play");
            yield return CardKey(Key.Escape);
            Check(!CardShown(root, PauseView.SkipName), "With the opening finished, Pause offers no skip");
            yield return CardClick(root.Q<Button>(PauseView.ReloadName), "Reload save…");
            yield return CardClick(root.Q<Button>(PauseView.ReloadYesName), "Reload");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.None && HudToastShownWith(root, s.Get("pause.reloaded")), "Reload reloads the save and says so in a toast");

            // ---------- the development studies (UX-09: their IMGUI viewer is gone) ----------
            yield return ScreensStudies(root, s);

            // ---------- only UI (UX-09) ----------
            Check(NoImmediateModeGui(out string imgui), "No IMGUI path remains: nothing in the game assembly draws with OnGUI" + imgui);
            shop.SetPanel(ShopPanel.Pause);
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Pause && screens.IsShown, "Pause opens the toolkit screen again; no other UI draws it");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        // Arrivals on only until a visitor queues at the counter, as the tutorial routes wait for a checkout.
        IEnumerator ScreensCheckout()
        {
            shop.SetPanel(ShopPanel.None);
            shop.Customers.ArrivalsEnabled = true;
            float until = Time.realtimeSinceStartup + 60f;
            while (!(shop.Customers.NextToServe != null && shop.Customers.NextToServe.Phase == CustomerPhase.Queued) && Time.realtimeSinceStartup < until) yield return null;
            shop.Customers.ArrivalsEnabled = false;
            Check(shop.Customers.NextToServe != null && shop.Customers.NextToServe.Phase == CustomerPhase.Queued, "A visitor queues at the counter");
        }

        IEnumerator ScreensNight()
        {
            Check(shop.Cycle.Phase == ShopDayPhase.Closing, "Close now closes the shop");
            float until = Time.realtimeSinceStartup + 20f;
            while (shop.Cycle.Phase != ShopDayPhase.Night && Time.realtimeSinceStartup < until) yield return null;
            yield return HudFrames(4);
            // UX-08: the receipt's tallies count up first; its numbers are read once they have.
            yield return UiSettled();
        }

        /// <summary>A real click at a fraction of an element's width, at its middle: a slider's track.</summary>
        IEnumerator ScreenClickAt(VisualElement target, float fraction, string label)
        {
            Check(target != null && CardShown(target), label + " is shown");
            yield return HudFrames(2);
            var tracker = target.Q(className: "unity-base-slider__tracker") ?? target;
            Rect r = tracker.worldBound;
            Vector2 panel = shop.ToolkitUi.Root.layout.size;
            var x = r.xMin + r.width * fraction;
            var y = r.center.y;
            var at = new Vector2(x * Screen.width / panel.x, Screen.height - y * Screen.height / panel.y);
            cardMouse.MakeCurrent();
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at, buttons = 1 });
            yield return HudFrames(2);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
        }

        /// <summary>What the pointer would hit at an element's centre, and that element's ancestors (a diagnosis for a check message).</summary>
        static string ScreenPickChain(VisualElement element)
        {
            var picked = element.panel.Pick(element.worldBound.center);
            var chain = new System.Collections.Generic.List<string>();
            for (var e = picked; e != null && chain.Count < 8; e = e.parent) chain.Add(Named(e) + (e.pickingMode == PickingMode.Ignore ? "(ignore)" : ""));
            return " (pointer hits: " + string.Join(" < ", chain) + ")";
        }

        static bool ScreenLabelWith(VisualElement element, string text)
        {
            if (element == null) return false;
            foreach (var label in element.Query<Label>().ToList()) if (label.text == text && CardShown(label)) return true;
            return false;
        }

        /// <summary>
        /// The screen is on screen over the dim, clear of the HUD's chips, with the expected number of primaries, nothing
        /// scrolling, and every text fitting inside the box that holds it (UX.6: screens fit at 1280×720).
        /// </summary>
        void CheckScreen(string state, int primaries)
        {
            var ui = shop.ToolkitUi;
            var root = ui.Root;
            var screens = ui.Screens;
            var view = screens.CurrentView;
            Check(screens.IsShown && view != null, state + ": the screen is shown");
            if (view == null) return;
            var area = view.UsesSheet ? screens.Element.Q("es-screen-book") : view.Root;
            Check(ShopUiTestDriver.FullyInside(area, root), state + ": fully on screen " + area.worldBound + " in " + root.worldBound);
            var dim = screens.Element.Q(ScreenPresenter.DimName);
            Check(CardShown(dim) && dim.resolvedStyle.backgroundColor.a > .4f && dim.worldBound.width >= root.worldBound.width - 1 && dim.worldBound.height >= root.worldBound.height - 1,
                state + ": the world behind is dimmed");
            foreach (string chip in new[] { HudPresenter.DayName, HudPresenter.MoneyName })
            {
                var c = root.Q(chip);
                if (!CardShown(c)) continue;
                Rect a = area.worldBound, b = c.worldBound;
                bool overlap = a.xMin < b.xMax - 1 && b.xMin < a.xMax - 1 && a.yMin < b.yMax - 1 && b.yMin < a.yMax - 1;
                Check(!overlap, state + ": clear of the HUD's " + chip + " " + b + " / " + a);
            }
            int shown = 0;
            foreach (var button in screens.Element.Query<Button>(className: ScreenPresenter.PrimaryClass).ToList()) if (CardShown(button)) shown++;
            Check(shown == primaries, state + ": " + shown + " primary button(s) shown, expected " + primaries);
            int scrolling = 0;
            foreach (var scroll in area.Query<ScrollView>().ToList())
                if (CardShown(scroll) && scroll.contentContainer.layout.height > scroll.contentViewport.layout.height + 1f) scrolling++;
            Check(scrolling == 0, state + ": nothing scrolls");
            string problem = ScreenTextProblem(area);
            Check(problem == null, state + ": every text fits" + (problem != null ? " / " + problem : ""));
        }

        /// <summary>PlanTextProblem for a screen; a slider's own parts (its dragger overhangs the track) are left to Unity.</summary>
        static string ScreenTextProblem(VisualElement area)
        {
            Rect outer = area.worldBound;
            foreach (var element in area.Query<VisualElement>().ToList())
            {
                if (element == area || !CardShown(element)) continue;
                Rect r = element.worldBound;
                if (r.width <= 0 || r.height <= 0) continue;
                bool unityPart = element.GetFirstAncestorOfType<Slider>() != null;
                if (!unityPart && (r.xMin < outer.xMin - .5f || r.yMin < outer.yMin - .5f || r.xMax > outer.xMax + .5f || r.yMax > outer.yMax + .5f))
                    return Named(element) + " leaves the screen " + r + " / " + outer;
                var holder = element.parent;
                if (!unityPart && holder != null && holder != area && holder.worldBound.width > 0)
                {
                    Rect h = holder.worldBound;
                    if (r.xMin < h.xMin - 1f || r.yMin < h.yMin - 1f || r.xMax > h.xMax + 1f || r.yMax > h.yMax + 1f)
                        return Named(element) + " leaves " + Named(holder) + " " + r + " / " + h;
                }
                if (!(element is TextElement text) || string.IsNullOrEmpty(text.text)) continue;
                var style = text.resolvedStyle;
                float wrapWidth = text.contentRect.width + 1f;
                float innerWidth = text.layout.width - style.borderLeftWidth - style.borderRightWidth;
                float innerHeight = text.layout.height - style.borderTopWidth - style.borderBottomWidth;
                Vector2 needed = text.MeasureTextSize(text.text, wrapWidth, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                if (needed.y > innerHeight + 1.5f) return Named(text) + " '" + text.text + "' needs " + needed.y.ToString("0") + " px, it has " + innerHeight.ToString("0") + " inside its borders";
                if (style.whiteSpace == WhiteSpace.NoWrap)
                {
                    Vector2 line = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    if (line.x > innerWidth + 1.5f) return Named(text) + " '" + text.text + "' needs " + line.x.ToString("0") + " px wide, it has " + innerWidth.ToString("0") + " inside its borders";
                }
            }
            return null;
        }

        static string Named(VisualElement element) =>
            string.IsNullOrEmpty(element.name) ? element.GetType().Name + " " + string.Join(".", element.GetClasses()) : element.name;
    }
}
#endif
