using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Pause (UX.6), after the approved mockup: a left card with Resume, Settings, Help and Save &amp; quit, and the save
    /// file (Reload… and New game…, each with a confirm step); a right card with the settings tabs Controls, Sound and
    /// Comfort. New game (ShopPanel.Reset) asks on the right card. Development builds add the presentation sample and
    /// comedy preview.
    /// </summary>
    public sealed class PauseView : ScreenView
    {
        public const string MenuName = "es-pause-menu", CardName = "es-pause-card", ResumeName = "es-pause-resume", SettingsName = "es-pause-settings",
            HelpName = "es-pause-help", QuitName = "es-pause-quit", SkipName = "es-pause-skip", SkipConfirmName = "es-pause-skip-confirm",
            SkipKeepName = "es-pause-skip-keep", SkipYesName = "es-pause-skip-yes", SaveStatusName = "es-pause-save-status",
            ReloadName = "es-pause-reload", ReloadConfirmName = "es-pause-reload-confirm", ReloadKeepName = "es-pause-reload-keep",
            ReloadYesName = "es-pause-reload-yes", NewGameName = "es-pause-new", DevName = "es-pause-dev", PresentationName = "es-pause-presentation",
            ComedyName = "es-pause-comedy", SettingsPageName = "es-pause-settings-page", ResetName = "es-pause-reset", ResetKeepName = "es-pause-reset-keep",
            ResetYesName = "es-pause-reset-yes", ResetErrorName = "es-pause-reset-error", SensitivityName = "es-pause-sensitivity",
            SlapKeyName = "es-pause-slap-key", SlapChangeName = "es-pause-slap-change", SlapWaitName = "es-pause-slap-wait",
            VolumeName = "es-pause-volume", AmbienceName = "es-pause-ambience", HumorName = "es-pause-humor",
            ReducedMotionName = "es-pause-reduced-motion", DetailStandardName = "es-pause-detail-standard", DetailLowName = "es-pause-detail-low",
            ReducedGagsName = "es-pause-reduced-gags", FooterName = "es-pause-footer", NoticeName = "es-pause-notice";
        public static string TabName(SettingsTab tab) => "es-pause-tab-" + tab.ToString().ToLowerInvariant();
        public static string PageName(SettingsTab tab) => "es-pause-" + tab.ToString().ToLowerInvariant();

        readonly PauseState state = new PauseState();
        readonly Dictionary<SettingsTab, Button> tabs = new Dictionary<SettingsTab, Button>();
        readonly Dictionary<SettingsTab, VisualElement> pages = new Dictionary<SettingsTab, VisualElement>();
        readonly Button settings, skip, reload, newGame, slapChange, detailStandard, detailLow;
        readonly VisualElement skipConfirm, reloadConfirm, dev, settingsPage, reset;
        readonly Label saveStatus, slapKey, slapWait, footer, notice, resetError, sensitivityValue, volumeValue, ambienceValue, humorValue;
        readonly Slider sensitivity, volume, ambience, humor;
        readonly PlanSwitch reducedMotion, reducedGags;
        SettingsTab tab;
        bool askingSkip, askingReload;

        public PauseState State => state;
        public SettingsTab Tab => tab;

        public PauseView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, "es-pause")
        {
            Root.AddToClassList("es-screen-pause");
            // Left: what to do.
            var menu = NewBox(Root, "es-screen-card es-screen-pause__menu", MenuName);
            NewLabel(menu, "es-pause-title", "es-screen-card__title", Strings.Get("pause.title"));
            KeyButton(menu, ResumeName, "es-button--primary es-screen-pause__nav", Strings.Get("pause.resume"), Strings.Get("card.keycap.back"), () => Shop.ScreenResume());
            settings = NewButton(menu, SettingsName, "es-button--secondary es-screen-pause__nav", Strings.Get("pause.settings"), () => { askingSkip = askingReload = false; Shop.ScreenKeepShop(); });
            KeyButton(menu, HelpName, "es-button--secondary es-screen-pause__nav", Strings.Get("pause.help"), "F1", () => Shop.OpenTutorialHelp());
            NewButton(menu, QuitName, "es-button--secondary es-screen-pause__nav", Strings.Get("pause.quit"), () => Shop.ScreenQuit());
            skip = NewButton(menu, SkipName, "es-link es-screen-link--danger", Strings.Get("welcome.skip"), () => { askingReload = false; askingSkip = true; });
            var file = NewBox(menu, "es-screen-pause__file", "es-pause-file");
            NewLabel(file, "es-pause-file-head", "es-screen-eyebrow", Strings.Get("pause.file"));
            saveStatus = NewLabel(file, SaveStatusName, "es-screen-pause__status");
            var fileLinks = NewBox(file, "es-plan-links", "es-pause-file-links");
            reload = NewButton(fileLinks, ReloadName, "es-link", Strings.Get("pause.reload"), () => { askingSkip = false; askingReload = true; });
            newGame = NewButton(fileLinks, NewGameName, "es-link es-screen-link--danger", Strings.Get("pause.newGame"), () => { askingSkip = askingReload = false; Shop.ScreenNewGame(); });
            dev = NewBox(menu, "es-screen-pause__dev", DevName);
            NewLabel(dev, "es-pause-dev-head", "es-screen-eyebrow", Strings.Get("pause.dev"));
            var devLinks = NewBox(dev, "es-plan-links", "es-pause-dev-links");
            NewButton(devLinks, PresentationName, "es-link", Strings.Get("pause.presentation"), () => Shop.ScreenPresentation());
            NewButton(devLinks, ComedyName, "es-link", Strings.Get("pause.comedy"), () => Shop.ScreenComedyPreview());

            // Right: settings, or the New game question.
            var card = NewBox(Root, "es-screen-card es-screen-pause__settings", CardName);
            settingsPage = NewBox(card, "es-screen-pause__page", SettingsPageName);
            var segs = NewBox(settingsPage, "es-plan-segs", "es-pause-tabs");
            foreach (SettingsTab t in Enum.GetValues(typeof(SettingsTab)))
            {
                var which = t;
                tabs[t] = NewButton(segs, TabName(t), "es-plan-seg", Strings.Get("pause.tab." + t.ToString().ToLowerInvariant()), () => tab = which);
                pages[t] = NewBox(settingsPage, "es-screen-pause__tab", PageName(t));
            }
            var controls = pages[SettingsTab.Controls];
            sensitivity = SliderRow(controls, SensitivityName, "pause.sensitivity", ScreenRules.MinSensitivity, ScreenRules.MaxSensitivity, out sensitivityValue,
                v => Shop.ScreenSetSensitivity(v));
            var slap = NewBox(controls, "es-screen-setting", "es-pause-slap");
            var slapHead = NewBox(slap, "es-screen-setting__head", "es-pause-slap-head");
            NewLabel(slapHead, "es-pause-slap-label", "es-screen-setting__label", Strings.Get("pause.slap"));
            var slapKeys = NewBox(slapHead, "es-screen-setting__value-row", "es-pause-slap-keys");
            slapKey = NewLabel(slapKeys, SlapKeyName, "es-keycap es-screen-setting__keycap");
            slapChange = NewButton(slapKeys, SlapChangeName, "es-button--secondary es-screen-setting__button", Strings.Get("pause.slap.change"), () => Shop.ScreenSlapRebind());
            slapWait = NewLabel(slap, SlapWaitName, "es-plan-why es-plan-why--info", Strings.Get("pause.slap.wait"));
            NewLabel(slap, "es-pause-slap-rule", "es-screen-setting__hint", Strings.Get("help.slap.rule"));

            var sound = pages[SettingsTab.Sound];
            volume = SliderRow(sound, VolumeName, "pause.volume", 0, 1, out volumeValue, v => Shop.ScreenSetVolumes(v, state.Ambience, state.Humor));
            ambience = SliderRow(sound, AmbienceName, "pause.ambience", 0, 1, out ambienceValue, v => Shop.ScreenSetVolumes(state.Volume, v, state.Humor));
            humor = SliderRow(sound, HumorName, "pause.humor", 0, 1, out humorValue, v => Shop.ScreenSetVolumes(state.Volume, state.Ambience, v));
            NewLabel(sound, "es-pause-humor-hint", "es-screen-setting__hint", Strings.Get("pause.humor.hint"));

            var comfort = pages[SettingsTab.Comfort];
            reducedMotion = SwitchRow(comfort, ReducedMotionName, "pause.reducedMotion", "pause.reducedMotion.hint",
                () => Shop.SetComfortSettings(!state.ReducedMotion, state.LowDetail ? FxEffectDetail.Low : FxEffectDetail.Standard));
            var detail = NewBox(comfort, "es-screen-setting", "es-pause-detail");
            var detailHead = NewBox(detail, "es-screen-setting__head", "es-pause-detail-head");
            NewLabel(detailHead, "es-pause-detail-label", "es-screen-setting__label", Strings.Get("pause.detail"));
            var detailSegs = NewBox(detailHead, "es-plan-segs", "es-pause-detail-segs");
            detailStandard = NewButton(detailSegs, DetailStandardName, "es-plan-seg", Strings.Get("pause.detail.standard"),
                () => Shop.SetComfortSettings(state.ReducedMotion, FxEffectDetail.Standard));
            detailLow = NewButton(detailSegs, DetailLowName, "es-plan-seg", Strings.Get("pause.detail.low"),
                () => Shop.SetComfortSettings(state.ReducedMotion, FxEffectDetail.Low));
            NewLabel(detail, "es-pause-detail-hint", "es-screen-setting__hint", Strings.Get("pause.detail.hint"));
            reducedGags = SwitchRow(comfort, ReducedGagsName, "pause.reducedGags", "pause.reducedGags.hint", () => Shop.ScreenSetReducedGags(!state.ReducedGags));
            footer = NewLabel(settingsPage, FooterName, "es-screen-pause__footer");

            reloadConfirm = Question(card, ReloadConfirmName, "pause.reload.title", "pause.reload.text", ReloadKeepName, "pause.reload.keep", () => askingReload = false,
                ReloadYesName, "pause.reload.yes", () => { askingReload = false; Shop.ScreenReload(Strings.Get("pause.reloaded")); });
            skipConfirm = Question(card, SkipConfirmName, "pause.skip.title", "pause.skip.text", SkipKeepName, "welcome.skip.keep", () => askingSkip = false,
                SkipYesName, "welcome.skip.yes", () => { askingSkip = false; Shop.SkipTutorial(); });

            reset = NewBox(card, "es-screen-pause__page", ResetName);
            NewLabel(reset, "es-pause-reset-title", "es-screen-card__title", Strings.Get("pause.reset.title"));
            NewLabel(reset, "es-pause-reset-text", "es-screen-card__line", Strings.Get("pause.reset.text"));
            resetError = NewLabel(reset, ResetErrorName, "es-plan-why");
            var resetButtons = NewBox(reset, "es-plan-confirm__buttons es-screen-pause__reset-buttons", "es-pause-reset-buttons");
            NewButton(resetButtons, ResetKeepName, "es-button--secondary", Strings.Get("pause.reset.keep"), () => Shop.ScreenKeepShop());
            NewButton(resetButtons, ResetYesName, "es-button--danger", Strings.Get("pause.reset.yes"), () => Shop.ScreenConfirmNewGame());
            notice = NewLabel(card, NoticeName, "es-screen-notice");
        }

        VisualElement Question(VisualElement card, string name, string titleKey, string textKey, string keepName, string keepKey, Action keep,
            string yesName, string yesKey, Action yes)
        {
            var page = NewBox(card, "es-screen-pause__page", name);
            NewLabel(page, name + "-title", "es-screen-card__title", Strings.Get(titleKey));
            NewLabel(page, name + "-text", "es-screen-card__line", Strings.Get(textKey));
            var buttons = NewBox(page, "es-plan-confirm__buttons es-screen-pause__reset-buttons", name + "-buttons");
            NewButton(buttons, keepName, "es-button--secondary", Strings.Get(keepKey), keep);
            NewButton(buttons, yesName, "es-button--danger", Strings.Get(yesKey), yes);
            return page;
        }

        static void KeyButton(VisualElement parent, string name, string className, string label, string cap, Action onClick)
        {
            var button = NewButton(parent, name, className, "", onClick);
            var text = NewLabel(button, name + "-text", "es-screen-pause__nav-text", label);
            text.pickingMode = PickingMode.Ignore;
            var c = NewLabel(button, name + "-keycap", "es-keycap es-screen-pause__nav-key", cap);
            c.pickingMode = PickingMode.Ignore;
        }

        Slider SliderRow(VisualElement parent, string name, string key, float low, float high, out Label value, Action<float> changed)
        {
            var row = NewBox(parent, "es-screen-setting", name + "-row");
            var head = NewBox(row, "es-screen-setting__head", name + "-head");
            NewLabel(head, name + "-label", "es-screen-setting__label", Strings.Get(key));
            value = NewLabel(head, name + "-value", "es-screen-setting__value");
            var slider = new Slider(low, high) { name = name };
            slider.AddToClassList("es-screen-slider");
            slider.RegisterValueChangedCallback(e => changed(e.newValue));
            row.Add(slider);
            return slider;
        }

        PlanSwitch SwitchRow(VisualElement parent, string name, string key, string hint, Action onClick)
        {
            var row = NewBox(parent, "es-screen-setting", name + "-row");
            var head = NewBox(row, "es-screen-setting__head", name + "-head");
            NewLabel(head, name + "-label", "es-screen-setting__label", Strings.Get(key));
            var sw = new PlanSwitch(onClick) { name = name };
            head.Add(sw);
            NewLabel(row, name + "-hint", "es-screen-setting__hint", Strings.Get(hint));
            return sw;
        }

        public override void Opened() { tab = SettingsTab.Controls; askingSkip = false; askingReload = false; }

        public override void Refresh()
        {
            Shop.FillPause(state);
            if (!state.TutorialActive || state.Reset) askingSkip = false;
            if (state.Reset) askingReload = false;
            bool settingsShown = !state.Reset && !askingReload && !askingSkip;
            settings.EnableInClassList("es-screen-pause__nav--on", settingsShown);
            Shown(settingsPage, settingsShown);
            Shown(reset, state.Reset);
            Shown(reloadConfirm, askingReload);
            Shown(skipConfirm, askingSkip);
            // Hidden but keeping its line while a question is up, so the menu (and both cards, centred together) never jump.
            Shown(skip, state.TutorialActive);
            skip.style.visibility = askingSkip || state.Reset ? Visibility.Hidden : Visibility.Visible;
            Shown(dev, state.Dev);
            SetText(saveStatus, state.AutosaveBlocked ? Strings.Get("pause.save.blocked") : !string.IsNullOrEmpty(state.SaveError)
                ? Strings.Get("pause.save.error") : Strings.Get("pause.save.ok"));
            saveStatus.EnableInClassList("es-screen-pause__status--error", state.AutosaveBlocked || !string.IsNullOrEmpty(state.SaveError));
            Shown(reload, !askingReload);
            Enabled(newGame, !state.Reset);
            if (!settingsShown)
            {
                SetText(resetError, state.SaveError);
                Shown(resetError, !string.IsNullOrEmpty(state.SaveError));
                return;
            }
            foreach (var pair in tabs) pair.Value.EnableInClassList("es-plan-seg--on", pair.Key == tab);
            foreach (var pair in pages) Shown(pair.Value, pair.Key == tab);
            SetText(footer, Strings.Get(tab == SettingsTab.Comfort ? "pause.footer.comfort" : "pause.footer"));
            Sync(sensitivity, state.Sensitivity);
            SetText(sensitivityValue, Strings.Format("pause.sensitivity.value", state.Sensitivity.ToString("0.00")));
            SetText(slapKey, state.WaitingForSlapKey ? "…" : state.SlapKey);
            Shown(slapChange, !state.WaitingForSlapKey);
            Shown(slapWait, state.WaitingForSlapKey);
            Sync(volume, state.Volume); SetText(volumeValue, Strings.Format("pause.percent", ScreenRules.Percent(state.Volume)));
            Sync(ambience, state.Ambience); SetText(ambienceValue, Strings.Format("pause.percent", ScreenRules.Percent(state.Ambience)));
            Sync(humor, state.Humor); SetText(humorValue, Strings.Format("pause.percent", ScreenRules.Percent(state.Humor)));
            reducedMotion.SetOn(state.ReducedMotion);
            reducedGags.SetOn(state.ReducedGags);
            detailStandard.EnableInClassList("es-plan-seg--on", !state.LowDetail);
            detailLow.EnableInClassList("es-plan-seg--on", state.LowDetail);
        }

        static void Sync(Slider slider, float value)
        {
            if (Mathf.Abs(slider.value - value) > 0.0005f) slider.SetValueWithoutNotify(value);
        }

        public override void ShowNotice(string text, bool error) => FillNotice(notice, text, error);
    }
}
