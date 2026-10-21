using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// The welcome card (UX.6): what the shop is, then the one choice that matters now (start or continue the guided
    /// opening, continue the shop, or recover a protected save), and the five keys a new player needs.
    /// </summary>
    public sealed class WelcomeView : ScreenView
    {
        public const string CardName = "es-welcome", TitleName = "es-welcome-title", HeadName = "es-welcome-head", LineName = "es-welcome-line",
            StepName = "es-welcome-step", PrimaryName = "es-welcome-primary", SecondaryName = "es-welcome-secondary", HelpName = "es-welcome-help",
            SkipName = "es-welcome-skip", SkipConfirmName = "es-welcome-skip-confirm", SkipKeepName = "es-welcome-skip-keep", SkipYesName = "es-welcome-skip-yes",
            RecoveryName = "es-welcome-recovery", NoticeName = "es-welcome-notice", KeysName = "es-welcome-keys";

        enum Mode { Blocked, Offered, Active, Done }

        readonly WelcomeState state = new WelcomeState();
        readonly Label head, line, step, notice;
        readonly Button primary, secondary, help, skip, recovery;
        readonly VisualElement skipConfirm;
        Mode mode;
        bool askingSkip;

        public WelcomeState State => state;

        public WelcomeView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, CardName)
        {
            Root.AddToClassList("es-screen-card"); Root.AddToClassList("es-screen-welcome");
            NewLabel(Root, "es-welcome-eyebrow", "es-screen-eyebrow", Strings.Get("welcome.eyebrow"));
            NewLabel(Root, TitleName, "es-screen-card__title", Strings.Get("welcome.title"));
            NewLabel(Root, "es-welcome-sub", "es-screen-card__line", Strings.Get("welcome.sub"));
            var section = NewBox(Root, "es-screen-welcome__section", "es-welcome-section");
            var heads = NewBox(section, "es-screen-welcome__heads", "es-welcome-heads");
            head = NewLabel(heads, HeadName, "es-screen-welcome__head");
            step = NewLabel(heads, StepName, "es-plan-chip es-plan-chip--on");
            line = NewLabel(section, LineName, "es-screen-card__line");
            var buttons = NewBox(section, "es-screen-welcome__buttons", "es-welcome-buttons");
            primary = NewButton(buttons, PrimaryName, "es-button--primary es-screen-welcome__primary", "", PressPrimary);
            secondary = NewButton(buttons, SecondaryName, "es-button--secondary es-screen-welcome__secondary", "", PressSecondary);
            var links = NewBox(section, "es-plan-links", "es-welcome-links");
            help = NewButton(links, HelpName, "es-link", Strings.Get("welcome.help"), () => Shop.OpenTutorialHelp());
            recovery = NewButton(links, RecoveryName, "es-link", Strings.Get("welcome.recovery"), () => Shop.ScreenSaveRecovery());
            skip = NewButton(links, SkipName, "es-link es-screen-link--danger", Strings.Get("welcome.skip"), () => askingSkip = true);
            skipConfirm = NewBox(section, "es-plan-confirm es-plan-confirm--inline", SkipConfirmName);
            NewLabel(skipConfirm, "es-welcome-skip-text", "es-plan-confirm__text", Strings.Get("welcome.skip.ask"));
            var confirmButtons = NewBox(skipConfirm, "es-plan-confirm__buttons");
            NewButton(confirmButtons, SkipKeepName, "es-button--secondary", Strings.Get("welcome.skip.keep"), () => askingSkip = false);
            NewButton(confirmButtons, SkipYesName, "es-button--danger", Strings.Get("welcome.skip.yes"), () => { askingSkip = false; Shop.SkipTutorial(); });
            notice = NewLabel(Root, NoticeName, "es-screen-notice");
            var keys = NewBox(Root, "es-screen-keys", KeysName);
            NewKey(keys, "es-welcome-key-move", Strings.Get("help.cap.move"), Strings.Get("welcome.key.move"));
            NewKey(keys, "es-welcome-key-look", Strings.Get("help.cap.look"), Strings.Get("welcome.key.look"));
            NewKey(keys, "es-welcome-key-use", "E", Strings.Get("welcome.key.use"));
            NewKey(keys, "es-welcome-key-help", "F1", Strings.Get("welcome.key.help"));
            NewKey(keys, "es-welcome-key-pause", Strings.Get("card.keycap.back"), Strings.Get("welcome.key.pause"));
        }

        public override void Opened() { askingSkip = false; }

        void PressPrimary()
        {
            switch (mode)
            {
                case Mode.Blocked: Shop.ScreenSaveRecovery(); break;
                case Mode.Offered: Shop.StartGuidedOpening(); break;
                case Mode.Active: Shop.ContinueGuidedOpening(); break;
                default: Shop.ScreenContinueShop(); break;
            }
        }

        void PressSecondary()
        {
            switch (mode)
            {
                case Mode.Blocked: Shop.ScreenExplore(); break;
                case Mode.Offered: Shop.SkipTutorial(); break;
                default: Shop.OpenTutorialHelp(); break;
            }
        }

        public override void Refresh()
        {
            Shop.FillWelcome(state);
            mode = state.AutosaveBlocked ? Mode.Blocked : state.Mode == TutorialMode.Offered ? Mode.Offered :
                state.Mode == TutorialMode.Active ? Mode.Active : Mode.Done;
            string m = mode == Mode.Blocked ? "blocked" : mode == Mode.Offered ? "offered" : mode == Mode.Active ? "active" :
                state.Mode == TutorialMode.Completed ? "completed" : "ready";
            SetText(head, Strings.Get("welcome.head." + m));
            SetText(line, Strings.Get("welcome.line." + m));
            SetText(primary, Strings.Get("welcome.primary." + (mode == Mode.Done ? "done" : m)));
            SetText(secondary, Strings.Get("welcome.secondary." + (mode == Mode.Done || mode == Mode.Active ? "help" : m)));
            Shown(step, mode == Mode.Active);
            if (mode == Mode.Active)
                SetText(step, Strings.Format("help.step.named", state.LessonNumber, Strings.Get(ScreenRules.LessonKey(state.Lesson))));
            Shown(help, mode != Mode.Active && mode != Mode.Done);
            Shown(recovery, mode != Mode.Blocked && state.SaveError);
            Shown(skip, mode == Mode.Active && !askingSkip);
            Shown(skipConfirm, mode == Mode.Active && askingSkip);
        }

        public override void ShowNotice(string text, bool error) => FillNotice(notice, text, error);
    }

    /// <summary>
    /// Help (F1, UX.6): the Opening Night steps as a list on the left and one page on the right: the step's one thing to
    /// do, a tip or two, and "Show me where to go" when it can point the way. Outside the guided opening the steps are
    /// reference cards. The last step is choosing a next goal; a Controls page lists the keys.
    /// </summary>
    public sealed class HelpView : ScreenView
    {
        public const string StepsName = "es-help-steps", PageName = "es-help-page", ControlsStepName = "es-help-step-controls",
            EyebrowName = "es-help-eyebrow", LessonTitleName = "es-help-lesson-title", DoName = "es-help-do", NoteName = "es-help-note",
            RecoveryName = "es-help-recovery", ShowName = "es-help-show", DirectionsOffName = "es-help-directions-off",
            CurrentName = "es-help-current", NowName = "es-help-now", StaffName = "es-help-staff", SkipName = "es-help-skip",
            SkipConfirmName = "es-help-skip-confirm", SkipKeepName = "es-help-skip-keep", SkipYesName = "es-help-skip-yes",
            FinishName = "es-help-finish", JournalName = "es-help-journal", UpgradeName = "es-help-upgrade", UpgradeBackName = "es-help-upgrade-back",
            ReceiptsOkName = "es-help-receipts-ok", ReceiptsBrowseName = "es-help-receipts-browse", ControlsName = "es-help-controls";
        public static string StepName(TutorialLesson lesson) => "es-help-step-" + lesson.ToString().ToLowerInvariant();
        public static string TipName(int i) => "es-help-tip-" + i;

        readonly HelpState state = new HelpState();
        readonly Dictionary<TutorialLesson, Button> steps = new Dictionary<TutorialLesson, Button>();
        readonly Button controlsStep, show, directionsOff, current, skip, finish, upgradeBack;
        readonly VisualElement lessonPage, controlsPage, nextPage, upgradePage, receiptsPage, skipConfirm, now, tips;
        readonly Label eyebrow, lessonTitle, doLine, tip1, tip2, intro, note, recovery, directionsNote, staff;
        readonly Label nextOutcome, nextPreview, upPreview, upCapacity, upSpeed, upWhere, rFirst, rPractice;
        readonly Label nowDay, nowMoney, nowBloom, nowFlow, nowSpark, nowMist, nowSiphon, nowStocked, nowIncubator;
        readonly Label controlsSlap;
        bool showControls, askingSkip;

        public HelpState State => state;
        public bool ControlsShown => showControls;

        public HelpView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, "es-help")
        {
            UsesSheet = true;
            Root.AddToClassList("es-screen-view--sheet");
            Title = Strings.Get("help.title");
            Note = Strings.Get("help.footer");
            var columns = NewBox(Root, "es-screen-help", "es-help-columns");
            var list = NewBox(columns, "es-screen-help__steps", StepsName);
            for (var lesson = TutorialLesson.Orient; lesson <= TutorialLesson.NextPlan; lesson++)
            {
                var l = lesson;
                var b = NewButton(list, StepName(lesson), "es-screen-step", "", () => { showControls = false; askingSkip = false; Shop.SelectTutorialHelpLesson(l); });
                var n = NewLabel(b, StepName(lesson) + "-number", "es-screen-step__number", ScreenRules.LessonNumber(lesson).ToString());
                n.pickingMode = PickingMode.Ignore;
                var t = NewLabel(b, StepName(lesson) + "-name", "es-screen-step__name", Strings.Get(ScreenRules.LessonKey(lesson)));
                t.pickingMode = PickingMode.Ignore;
                steps[lesson] = b;
            }
            controlsStep = NewButton(list, ControlsStepName, "es-screen-step es-screen-step--controls", "", () => { showControls = true; askingSkip = false; });
            var ck = NewLabel(controlsStep, ControlsStepName + "-number", "es-screen-step__number", "?"); ck.pickingMode = PickingMode.Ignore;
            var cn = NewLabel(controlsStep, ControlsStepName + "-name", "es-screen-step__name", Strings.Get("help.controls")); cn.pickingMode = PickingMode.Ignore;

            var page = NewBox(columns, "es-screen-help__page", PageName);

            // One step.
            lessonPage = NewBox(page, "es-screen-help__section", "es-help-lesson");
            eyebrow = NewLabel(lessonPage, EyebrowName, "es-screen-eyebrow");
            lessonTitle = NewLabel(lessonPage, LessonTitleName, "es-screen-help__title");
            doLine = NewLabel(lessonPage, DoName, "es-screen-help__do");
            tips = NewBox(lessonPage, "es-screen-help__tips", "es-help-tips");
            tip1 = NewLabel(tips, TipName(1), "es-screen-help__tip");
            tip2 = NewLabel(tips, TipName(2), "es-screen-help__tip");
            intro = NewLabel(tips, "es-help-intro", "es-screen-help__tip");
            recovery = NewLabel(lessonPage, RecoveryName, "es-plan-why");
            note = NewLabel(lessonPage, NoteName, "es-plan-why es-plan-why--info");
            var actions = NewBox(lessonPage, "es-screen-help__actions", "es-help-actions");
            show = NewButton(actions, ShowName, "es-button--primary", Strings.Get("help.show"), () => Shop.ShowTutorialTarget());
            directionsOff = NewButton(actions, DirectionsOffName, "es-button--secondary", Strings.Get("help.directions.off"), () => Shop.StopTutorialPracticeGuidance());
            current = NewButton(actions, CurrentName, "es-link", Strings.Get("help.current"), () => { showControls = false; Shop.SelectTutorialHelpLesson(state.ActiveLesson); });
            directionsNote = NewLabel(lessonPage, "es-help-directions-note", "es-plan-why es-plan-why--info", Strings.Get("help.directions.note"));

            // Choose your next goal.
            nextPage = NewBox(page, "es-screen-help__section", "es-help-next");
            NewLabel(nextPage, "es-help-next-eyebrow", "es-screen-eyebrow", Strings.Format("help.step", ScreenRules.LessonNumber(TutorialLesson.NextPlan)));
            NewLabel(nextPage, "es-help-next-title", "es-screen-help__title", Strings.Get("help.next.title"));
            NewLabel(nextPage, "es-help-next-line", "es-screen-help__do", Strings.Get("help.next.line"));
            nextOutcome = NewLabel(nextPage, "es-help-next-outcome", "es-plan-chip es-plan-chip--on");
            var nextActions = NewBox(nextPage, "es-screen-help__actions", "es-help-next-actions");
            finish = NewButton(nextActions, FinishName, "es-button--primary", Strings.Get("help.next.finish"), () => Shop.FinishGuidedOpening());
            NewLabel(nextPage, "es-help-next-keep", "es-screen-help__tip", Strings.Get("help.next.keep"));
            nextPreview = NewLabel(nextPage, "es-help-next-preview", "es-screen-help__tip");
            var nextLinks = NewBox(nextPage, "es-plan-links", "es-help-next-links");
            NewButton(nextLinks, JournalName, "es-link", Strings.Get("help.next.journal"), () => Shop.OpenTutorialJournal());
            NewButton(nextLinks, UpgradeName, "es-link", Strings.Get("help.next.upgrade"), () => Shop.OpenTutorialUpgradeHelp());

            // Siphon MK II preview.
            upgradePage = NewBox(page, "es-screen-help__section", "es-help-upgrade-page");
            NewLabel(upgradePage, "es-help-up-title", "es-screen-help__title", Strings.Get("help.up.title"));
            upPreview = NewLabel(upgradePage, "es-help-up-preview", "es-screen-help__do");
            upCapacity = NewLabel(upgradePage, "es-help-up-capacity", "es-screen-help__tip");
            upSpeed = NewLabel(upgradePage, "es-help-up-speed", "es-screen-help__tip");
            upWhere = NewLabel(upgradePage, "es-help-up-where", "es-screen-help__tip");
            NewLabel(upgradePage, "es-help-up-note", "es-plan-why es-plan-why--info", Strings.Get("help.up.note"));
            var upLinks = NewBox(upgradePage, "es-plan-links", "es-help-up-links");
            upgradeBack = NewButton(upLinks, UpgradeBackName, "es-link", Strings.Get("help.up.back"), () => Shop.SelectTutorialHelpLesson(TutorialLesson.NextPlan));
            NewButton(upLinks, "es-help-up-journal", "es-link", Strings.Get("help.next.journal"), () => Shop.OpenTutorialJournal());

            // Earlier guided sales, when a newer day replaced their report.
            receiptsPage = NewBox(page, "es-screen-help__section", "es-help-receipts");
            NewLabel(receiptsPage, "es-help-receipts-title", "es-screen-help__title", Strings.Get("help.receipts.title"));
            NewLabel(receiptsPage, "es-help-receipts-line", "es-screen-help__do", Strings.Get("help.receipts.line"));
            rFirst = NewLabel(receiptsPage, "es-help-receipts-first", "es-screen-help__tip");
            rPractice = NewLabel(receiptsPage, "es-help-receipts-practice", "es-screen-help__tip");
            NewLabel(receiptsPage, "es-help-receipts-note", "es-plan-why es-plan-why--info", Strings.Get("help.receipts.note"));
            var rActions = NewBox(receiptsPage, "es-screen-help__actions", "es-help-receipts-actions");
            NewButton(rActions, ReceiptsOkName, "es-button--primary", Strings.Get("help.receipts.ok"), () => Shop.AcknowledgeTutorialSaleReceipts());
            NewButton(rActions, ReceiptsBrowseName, "es-link", Strings.Get("help.receipts.browse"), () => Shop.SelectTutorialHelpLesson(TutorialLesson.Orient));

            // Controls.
            controlsPage = NewBox(page, "es-screen-help__section", ControlsName);
            NewLabel(controlsPage, "es-help-controls-title", "es-screen-help__title", Strings.Get("help.controls"));
            var grid = NewBox(controlsPage, "es-screen-help__keys", "es-help-keys");
            NewKey(grid, "es-help-key-move", Strings.Get("help.cap.move"), Strings.Get("help.key.move"));
            NewKey(grid, "es-help-key-look", Strings.Get("help.cap.look"), Strings.Get("help.key.look"));
            NewKey(grid, "es-help-key-use", "E", Strings.Get("help.key.use"));
            NewKey(grid, "es-help-key-collect", Strings.Get("help.cap.collect"), Strings.Get("help.key.collect"));
            NewKey(grid, "es-help-key-journal", Strings.Get("help.cap.journal"), Strings.Get("help.key.journal"));
            NewKey(grid, "es-help-key-help", "F1", Strings.Get("help.key.help"));
            NewKey(grid, "es-help-key-pause", Strings.Get("card.keycap.back"), Strings.Get("help.key.pause"));
            NewKey(grid, "es-help-key-rotate", "R", Strings.Get("help.key.rotate"));
            var slapRow = NewKey(grid, "es-help-key-slap", "F", Strings.Get("help.key.slap"));
            controlsSlap = slapRow.Q<Label>("es-help-key-slap-cap");
            NewLabel(controlsPage, "es-help-slap-rule", "es-screen-help__tip", Strings.Get("help.slap.rule"));

            // Your shop right now, and leaving guidance.
            now = NewBox(page, "es-screen-help__now", NowName);
            NewLabel(now, "es-help-now-head", "es-screen-eyebrow", Strings.Get("help.now"));
            var chips = NewBox(now, "es-screen-help__chips", "es-help-now-chips");
            nowDay = NewLabel(chips, "es-help-now-day", "es-plan-chip");
            nowMoney = NewLabel(chips, "es-help-now-money", "es-plan-chip");
            nowBloom = NewLabel(chips, "es-help-now-bloom", "es-plan-chip es-screen-chip--bloom");
            nowFlow = NewLabel(chips, "es-help-now-flow", "es-plan-chip es-screen-chip--flow");
            nowSpark = NewLabel(chips, "es-help-now-spark", "es-plan-chip es-screen-chip--spark");
            nowMist = NewLabel(chips, "es-help-now-mist", "es-plan-chip es-screen-chip--mist");
            nowSiphon = NewLabel(chips, "es-help-now-siphon", "es-plan-chip");
            nowStocked = NewLabel(chips, "es-help-now-stocked", "es-plan-chip");
            nowIncubator = NewLabel(chips, "es-help-now-incubator", "es-plan-chip");
            staff = NewLabel(page, StaffName, "es-plan-why es-plan-why--info", Strings.Get("help.staff"));
            skip = NewButton(page, SkipName, "es-link es-screen-link--danger es-screen-help__skip", Strings.Get("welcome.skip"), () => askingSkip = true);
            skipConfirm = NewBox(page, "es-plan-confirm es-plan-confirm--inline", SkipConfirmName);
            NewLabel(skipConfirm, "es-help-skip-text", "es-plan-confirm__text", Strings.Get("welcome.skip.ask"));
            var confirmButtons = NewBox(skipConfirm, "es-plan-confirm__buttons");
            NewButton(confirmButtons, SkipKeepName, "es-button--secondary", Strings.Get("welcome.skip.keep"), () => askingSkip = false);
            NewButton(confirmButtons, SkipYesName, "es-button--danger", Strings.Get("welcome.skip.yes"), () => { askingSkip = false; Shop.SkipTutorial(); });
        }

        public override void Opened() { showControls = false; askingSkip = false; }

        public override void Refresh()
        {
            Shop.FillHelp(state, showControls);
            Subtitle = Strings.Get(state.TutorialActive ? "help.subtitle.tutorial" : "help.subtitle");
            foreach (var pair in steps)
            {
                bool on = !showControls && state.Page != HelpPage.Upgrade && pair.Key == state.Lesson;
                pair.Value.EnableInClassList("es-screen-step--on", on);
                pair.Value.EnableInClassList("es-screen-step--now", state.TutorialActive && pair.Key == state.ActiveLesson);
            }
            controlsStep.EnableInClassList("es-screen-step--on", showControls);
            Shown(lessonPage, state.Page == HelpPage.Lesson);
            Shown(controlsPage, state.Page == HelpPage.Controls);
            Shown(nextPage, state.Page == HelpPage.NextPlan);
            Shown(upgradePage, state.Page == HelpPage.Upgrade);
            Shown(receiptsPage, state.Page == HelpPage.Receipts);
            object[] args = { state.Recipe, state.IncubationSeconds, state.StandardPrice, state.TagPrice, state.IntroPatience };
            switch (state.Page)
            {
                case HelpPage.Lesson: RefreshLesson(args); break;
                case HelpPage.NextPlan:
                    SetText(nextOutcome, Strings.Get(state.Assisted ? "help.next.assisted" : "help.next.independent"));
                    SetText(nextPreview, UpgradePreview());
                    break;
                case HelpPage.Upgrade:
                    SetText(upPreview, UpgradePreview());
                    SetText(upCapacity, Strings.Format("help.up.capacity", state.BaseCapacity, state.UpgradedCapacity));
                    SetText(upSpeed, Strings.Format("help.up.speed", state.BaseCollect.ToString("0.0"), state.UpgradedCollect.ToString("0.0")));
                    SetText(upWhere, Strings.Get(state.UpgradeOwned ? "help.up.owned" : "help.up.where"));
                    break;
                case HelpPage.Receipts:
                    SetText(rFirst, Strings.Format("help.receipts.first", state.FirstName, state.FirstDay));
                    SetText(rPractice, Strings.Format("help.receipts.practice", state.PracticeName, state.PracticeDay));
                    break;
                case HelpPage.Controls:
                    SetText(controlsSlap, Shop.SlapKey);
                    break;
            }
            bool lessonish = state.Page == HelpPage.Lesson || state.Page == HelpPage.Receipts;
            // While asking to skip, the question takes the shop-right-now line's place, so it never covers it on a short screen.
            Shown(now, lessonish && !askingSkip);
            if (lessonish) RefreshNow();
            Shown(staff, lessonish && state.StaffHired && state.TutorialActive);
            Shown(skip, state.TutorialActive && !askingSkip);
            Shown(skipConfirm, state.TutorialActive && askingSkip);
        }

        void RefreshLesson(object[] args)
        {
            var lesson = state.Lesson;
            string key = ScreenRules.LessonKey(lesson);
            SetText(eyebrow, Strings.Format("help.step", ScreenRules.LessonNumber(lesson)));
            SetText(lessonTitle, Strings.Get(key));
            bool directions = lesson == TutorialLesson.Practice && state.Directions;
            SetText(doLine, directions && !string.IsNullOrEmpty(Shop.TutorialObjectiveForDisplay) ? Shop.TutorialObjectiveForDisplay : Strings.Format(key + ".do", args));
            string t1 = key + ".tip1", t2 = key + ".tip2";
            SetText(tip1, Strings.Has(t1) ? Strings.Format(t1, args) : "");
            SetText(tip2, Strings.Has(t2) ? Strings.Format(t2, args) : "");
            Shown(tip1, Strings.Has(t1));
            Shown(tip2, Strings.Has(t2));
            Shown(intro, lesson == TutorialLesson.Serve && state.IntroArrivals);
            if (lesson == TutorialLesson.Serve && state.IntroArrivals) SetText(intro, Strings.Format("help.lesson.serve.intro", args));
            string n = !state.TutorialActive ? "help.note.reference" : state.Practice && state.OnActiveLesson ? "help.note.practice" :
                !state.OnActiveLesson ? "help.note.reading" : null;
            SetText(note, n == null ? "" : Strings.Get(n));
            Shown(note, n != null);
            SetText(recovery, state.Recovery);
            Shown(recovery, !string.IsNullOrEmpty(state.Recovery));
            Shown(show, state.CanShowTarget);
            SetText(show, Strings.Get(state.Directions ? "help.show.continue" : "help.show"));
            Shown(directionsOff, directions);
            Shown(directionsNote, directions);
            Shown(current, state.TutorialActive && !state.OnActiveLesson && state.ActiveLesson >= TutorialLesson.Orient && state.ActiveLesson <= TutorialLesson.NextPlan);
        }

        void RefreshNow()
        {
            SetText(nowDay, Strings.Format(state.Night ? "help.now.night" : state.Open ? "help.now.open" : "help.now.closing", state.Day));
            SetText(nowMoney, Strings.Format("help.now.money", state.Money));
            SetText(nowBloom, Strings.Format("help.now.culture", Strings.Get("hud.culture.Bloom"), state.Bloom, state.StagedBloom));
            SetText(nowFlow, Strings.Format("help.now.culture", Strings.Get("hud.culture.Flow"), state.Flow, state.StagedFlow));
            Shown(nowSpark, state.SparkKnown);
            Shown(nowMist, state.MistKnown);
            if (state.SparkKnown) SetText(nowSpark, Strings.Format("help.now.carried", Strings.Get("hud.culture.Spark"), state.Spark));
            if (state.MistKnown) SetText(nowMist, Strings.Format("help.now.carried", Strings.Get("hud.culture.Mist"), state.Mist));
            SetText(nowSiphon, Strings.Format("help.now.siphon", state.Reserved, state.Capacity));
            SetText(nowStocked, Strings.Format("help.now.stocked", state.Stocked));
            bool incubator = state.IncubatorPhase != IncubatorPhase.Dosing;
            Shown(nowIncubator, incubator);
            if (incubator) SetText(nowIncubator, state.IncubatorPhase == IncubatorPhase.Ready
                ? Strings.Format("help.now.ready", state.IncubatorName) : Strings.Format("help.now.growing", state.IncubatorName, state.IncubatorSeconds));
        }

        string UpgradePreview()
        {
            if (state.UpgradeOwned) return Strings.Format("help.up.preview.owned", state.UpgradeCost);
            return state.Money >= state.UpgradeCost ? Strings.Format("help.up.preview.afford", state.UpgradeCost, state.Money)
                : Strings.Format("help.up.preview.save", state.UpgradeCost, state.Money, state.UpgradeCost - state.Money);
        }
    }
}
