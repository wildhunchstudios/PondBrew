using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-23 (the user's request 2026-10-30): the Settings screen, from the pause screen: Graphics, Display, Sound, Controls and
    /// Accessibility, each a tab of rows. Every change is put in play at once and kept in the whole game's settings file; a new
    /// window mode or resolution asks to be kept and goes back by itself after ten seconds. Each tab can go back to its defaults.
    /// </summary>
    public sealed partial class GrowthUi
    {
        public const string SettingsTitle = "Settings";
        public static readonly string[] SettingsTabs = { "Graphics", "Display", "Sound", "Controls", "Accessibility" };

        void FillSettings()
        {
            var g = s.Settings;
            var body = Notebook(SettingsTitle, "For the whole game · kept in " + System.IO.Path.GetFileName(s.SettingsPath), SettingsTabs[0]);
            FillGraphics(body, g);
            FillDisplay(AddTab(SettingsTabs[1]), g);
            FillSound(AddTab(SettingsTabs[2]), g);
            FillControls(AddTab(SettingsTabs[3]), g);
            FillAccessibility(AddTab(SettingsTabs[4]), g);
            NotebookFooter(s.SettingsFrom == GrowthCard.Menu ? "Back to the menu" : "Back to the pause screen", () => s.CloseSettings(), "Done");
        }

        // ---------------------------------------------------------------- the tabs

        void FillGraphics(VisualElement body, GrowthSettings g)
        {
            int preset = g.MatchingPreset();
            body.Add(Setting("Quality", preset == 3 ? "Custom: your own choices below; a preset sets them all" : "sets every choice below at once (not the view or brightness)",
                Choices("quality", new[] { "Low", "Medium", "High" }, preset, i => g.ApplyPreset(i))));
            body.Add(Setting("Shadows", "the sun's and the lamps' shadows", Choices("shadows", GrowthSettings.ShadowNames, g.shadows, i => g.shadows = i)));
            body.Add(Setting("Bloom", "the soft glow around bright lights and glass", Switch("bloom", g.bloom, v => g.bloom = v)));
            body.Add(Setting("Reflections", "the rooms reflected in glass and glossy plastic", Switch("reflections", g.reflections, v => g.reflections = v)));
            body.Add(Setting("Anti-aliasing", "smooths jagged edges (SMAA is the sharpest)", Choices("aa", GrowthSettings.AntiAliasingNames, g.antiAliasing, i => g.antiAliasing = i)));
            body.Add(Setting("Render scale", "draws the shop smaller and stretches it, for speed (the words stay sharp)",
                Slider("render-scale", g.renderScale, 50, 100, 5, v => v + "%", v => g.renderScale = v)));
            body.Add(Setting("Field of view", "how wide you see (68° unless changed)", Slider("fov", g.fieldOfView, GrowthSettings.MinFov, GrowthSettings.MaxFov, 2, v => v + "°", v => g.fieldOfView = v)));
            body.Add(Setting("Brightness", "lighter or darker, over the shop's own light", Slider("brightness", g.brightness, -5, 5, 1, v => v > 0 ? "+" + v : v.ToString(), v => g.brightness = v)));
            body.Add(Setting("Creature detail", "Low draws the creatures simply nearer and stills them sooner, for speed", Choices("detail", GrowthSettings.DetailNames, g.creatureDetail, i => g.creatureDetail = i)));
            body.Add(Defaults(() => { var d = new GrowthSettings(); g.ApplyPreset(2); g.fieldOfView = d.fieldOfView; g.brightness = d.brightness; }));
        }

        void FillDisplay(VisualElement body, GrowthSettings g)
        {
            if (s.DisplayPending)
            {
                var keep = Box("gr-callout"); keep.name = "gr-settings-keep"; keep.style.marginBottom = 12;
                keep.Add(L("Keep these display settings?", "gr-strong"));
                liveLabel = Text(LiveText()); liveLabel.AddToClassList("gr-hint"); keep.Add(liveLabel);
                var row = Box("gr-row"); row.style.marginTop = 6;
                row.Add(Button("Keep them", () => s.KeepDisplay(), true));
                row.Add(Button("Go back", () => s.RevertDisplay()));
                keep.Add(row);
                body.Add(keep);
            }
            body.Add(Setting("Window", "full screen without borders, a window, or the screen to itself",
                Choices("window", GrowthSettings.WindowModeNames, g.windowMode, i => s.ChangeDisplay(x => x.windowMode = i), display: true)));
            var sizes = Resolutions(g);
            body.Add(Setting("Resolution", "the window's size (full screen: the picture's)", ResolutionList(g, sizes)));
            body.Add(Setting("V-Sync", "waits for the screen, so frames never tear", Switch("vsync", g.vSync, v => { g.vSync = v; s.ApplyDisplay(); })));
            body.Add(Setting("Frame limit", g.vSync ? "V-Sync sets the frame rate (the screen's)" : "frames a second at most",
                Choices("frames", GrowthSettings.FrameLimits.Select(f => f == 0 ? "None" : f.ToString()).ToArray(), Array.IndexOf(GrowthSettings.FrameLimits, g.frameLimit),
                    i => { g.frameLimit = GrowthSettings.FrameLimits[i]; s.ApplyDisplay(); }, enabled: !g.vSync, why: "V-Sync is on")));
            body.Add(Setting("Interface size", "the cards and the HUD, larger or smaller", Slider("ui-scale", g.uiScale, 80, 130, 10, v => v + "%", v => g.uiScale = v)));
            body.Add(Defaults(() =>
            {
                var d = new GrowthSettings();
                g.vSync = d.vSync; g.frameLimit = d.frameLimit; g.uiScale = d.uiScale;
                s.ApplyDisplay();
            }, "Back to the defaults (not the window)"));
        }

        void FillSound(VisualElement body, GrowthSettings g)
        {
            body.Add(Setting("Master volume", "everything the game plays", Slider("master", g.master, 0, 100, 5, v => v.ToString(), v => g.master = v)));
            body.Add(Setting("Music", "the night's, the day's and the exhibition's tunes", Slider("music", g.music, 0, 100, 5, v => v.ToString(), v => g.music = v)));
            body.Add(Setting("Sounds", "the stations: the crank, the hand pump, the pop, the swish, a wish's chime", Slider("sounds", g.sounds, 0, 100, 5, v => v.ToString(), v => g.sounds = v)));
            body.Add(Setting("Machine hum", "the generator and the electric pumps", Slider("hum", g.hum, 0, 100, 5, v => v.ToString(), v => g.hum = v)));
            body.Add(Setting("Humour", "the silly moments' sounds", Slider("humour", g.humour, 0, 100, 5, v => v.ToString(), v => g.humour = v)));
            body.Add(Setting("Mute in the background", "silent while another window is in front", Switch("mute", g.muteInBackground, v => g.muteInBackground = v)));
            body.Add(Defaults(() => { var d = new GrowthSettings(); g.master = d.master; g.music = d.music; g.sounds = d.sounds; g.hum = d.hum; g.humour = d.humour; g.muteInBackground = d.muteInBackground; }));
        }

        void FillControls(VisualElement body, GrowthSettings g)
        {
            body.Add(Setting("Mouse sensitivity", "how far the view turns", Slider("sensitivity", Mathf.RoundToInt(g.sensitivity * 100), 25, 300, 5, v => (v / 100f).ToString("0.00") + "×", v => g.sensitivity = v / 100f)));
            body.Add(Setting("Invert mouse", "moving the mouse up looks down", Switch("invert", g.invertY, v => g.invertY = v)));
            body.Add(Setting("Use button", "collecting, bagging a sale, pumping and cranking: hold it, or press once to start and again to stop",
                Choices("use", new[] { "Hold", "Toggle" }, g.useToggle ? 1 : 0, i => g.useToggle = i == 1)));
            body.Add(Eyebrow("Keys and mouse buttons"));
            var note = s.Rebinding != null ? "Press a key or a mouse button for " + GrowthSettings.ActionName(s.Rebinding.Value) + " (Esc keeps it as it is)" : s.RebindNote;
            if (note != null) { var n = Raw(note, "gr-hint"); n.name = "gr-rebind-note"; n.style.marginBottom = 8; body.Add(n); }
            foreach (GrowthAction a in Enum.GetValues(typeof(GrowthAction)))
            {
                bool waiting = s.Rebinding == a;
                var key = Raw(waiting ? "…" : GrowthSettings.InputName(g.InputOf(a)), "es-keycap"); key.name = "gr-key-" + a.ToString().ToLowerInvariant();
                key.style.minWidth = 120; key.style.unityTextAlign = TextAnchor.MiddleCenter; key.style.marginRight = 10;
                var change = ChipButton(waiting ? "Waiting" : "Change", () => s.StartRebind(a), !waiting);
                change.name = "gr-rebind-" + a.ToString().ToLowerInvariant();
                var row = Setting(GrowthSettings.ActionName(a), null, key, change);
                if (waiting) row.AddToClassList("gr-set-row--active");
                body.Add(row);
            }
            body.Add(L("Esc pauses and closes; it can't be moved.", "gr-hint"));
            body.Add(Defaults(() => { var d = new GrowthSettings(); g.ResetBindings(); g.sensitivity = d.sensitivity; g.invertY = d.invertY; g.useToggle = d.useToggle; }, "Back to the defaults (keys and buttons too)"));
        }

        void FillAccessibility(VisualElement body, GrowthSettings g)
        {
            body.Add(Setting("Reduced motion", "no creature tricks, gentler movement, peek without its easing zoom", Switch("reduced", g.reducedMotion, v => g.reducedMotion = v)));
            body.Add(Setting("Peek", "look closely at a tank's glass to read each creature", Switch("peek", !g.peekOff, v => g.peekOff = !v)));
            body.Add(Setting("Silly moments", "how many there are: every one, fewer and shorter, or none but your own slap", Choices("gags", GrowthSettings.GagNames, g.gags, i => g.gags = i)));
            body.Add(Setting("Captions", "the silly moments' words on screen, with who says them", Switch("captions", !g.captionsOff, v => g.captionsOff = !v)));
            body.Add(Setting("Lessons", "a card and a guided step when each part of the game first appears (the journal's Guide keeps them)", Switch("lessons", !g.lessonsOff, v => g.lessonsOff = !v)));
            body.Add(Defaults(() => { var d = new GrowthSettings(); g.reducedMotion = d.reducedMotion; g.peekOff = d.peekOff; g.gags = d.gags; g.captionsOff = d.captionsOff; g.lessonsOff = d.lessonsOff; }));
        }

        // ---------------------------------------------------------------- the rows

        /// <summary>Puts a change in play and keeps it (the display's own changes apply themselves).</summary>
        void Change(Action change)
        {
            change();
            s.ApplySettings();
            s.SaveSettings();
        }

        /// <summary>A settings row: its name and a line, and its control at the end (the names are never keys to rename).</summary>
        static VisualElement Setting(string title, string line, params VisualElement[] end)
        {
            var row = Box("gr-item", "gr-set-row");
            var words = Box("gr-result__text");
            words.Add(Raw(title, "gr-item__title"));
            if (!string.IsNullOrEmpty(line)) words.Add(Raw(line, "gr-hint"));
            row.Add(words);
            foreach (var e in end) if (e != null) row.Add(e);
            return row;
        }

        /// <summary>A label shown as written (the Settings screen names keys as they are).</summary>
        static Label Raw(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        /// <summary>A choice of a few, the chosen one filled.</summary>
        VisualElement Choices(string key, string[] names, int chosen, Action<int> pick, bool display = false, bool enabled = true, string why = null)
        {
            var chips = Box("gr-chips"); chips.style.justifyContent = Justify.FlexEnd; chips.style.flexShrink = 1;
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                var b = ChipButton(names[i], () => { if (display) pick(at); else Change(() => pick(at)); }, enabled, enabled ? null : why);
                b.name = "gr-choice-" + key + "-" + names[i].ToLowerInvariant().Replace(' ', '-');
                if (i == chosen) b.AddToClassList("gr-chip--on");
                chips.Add(b);
            }
            return chips;
        }

        VisualElement Switch(string key, bool on, Action<bool> set)
        {
            var b = ChipButton(on ? "On" : "Off", () => Change(() => set(!on)), true);
            b.name = "gr-switch-" + key;
            if (on) b.AddToClassList("gr-chip--on");
            return b;
        }

        /// <summary>A value on a track: − and + step it, and a press on the track (or a drag along it) sets it there.</summary>
        VisualElement Slider(string name, int value, int min, int max, int step, Func<int, string> words, Action<int> set)
        {
            var row = Box("gr-row", "gr-set-slider"); row.name = "gr-slider-" + name;
            row.Add(PlusMinus("−", () => Change(() => set(Mathf.Max(min, value - step))), value > min, "The least"));
            var track = Box("gr-set-track"); track.name = "gr-track-" + name;
            var fill = Box("gr-set-fill"); fill.style.width = new Length((value - min) * 100f / (max - min), LengthUnit.Percent);
            track.Add(fill);
            int At(float x) => Mathf.Clamp(min + Mathf.RoundToInt((max - min) * Mathf.Clamp01(x / Mathf.Max(1, track.layout.width)) / step) * step, min, max);
            int dragged = value;
            track.RegisterCallback<PointerDownEvent>(e => { track.CapturePointer(e.pointerId); dragged = At(e.localPosition.x); fill.style.width = new Length((dragged - min) * 100f / (max - min), LengthUnit.Percent); e.StopPropagation(); });
            track.RegisterCallback<PointerMoveEvent>(e => { if (!track.HasPointerCapture(e.pointerId)) return; dragged = At(e.localPosition.x); fill.style.width = new Length((dragged - min) * 100f / (max - min), LengthUnit.Percent); });
            track.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!track.HasPointerCapture(e.pointerId)) return;
                track.ReleasePointer(e.pointerId);
                int v = At(e.localPosition.x);
                Change(() => set(v));
                RefreshCard();
            });
            row.Add(track);
            row.Add(PlusMinus("+", () => Change(() => set(Mathf.Min(max, value + step))), value < max, "The most"));
            var shown = Raw(words(value), "gr-set-value"); shown.name = "gr-value-" + name;
            row.Add(shown);
            return row;
        }

        VisualElement Defaults(Action reset, string text = "Back to the defaults")
        {
            var b = Link(text, () => { reset(); s.ApplySettings(); s.SaveSettings(); });
            b.style.alignSelf = Align.FlexStart; b.style.marginLeft = 0; b.style.marginTop = 6;
            b.name = "gr-defaults";
            return b;
        }

        /// <summary>The window sizes to choose from: the screen's own modes (at least 1024 wide, each size once) and the one in
        /// use, smallest first.</summary>
        bool resolutionOpen;

        /// <summary>GR-32 (F53): the resolution as a list: what it is now, which opens every size to choose from.</summary>
        VisualElement ResolutionList(GrowthSettings g, List<Vector2Int> sizes)
        {
            var pick = Box("gr-dropdown"); pick.name = "gr-resolution";
            var head = new Button(() => { resolutionOpen = !resolutionOpen; RefreshCard(); }) { text = g.width + " × " + g.height, name = "gr-resolution-open" };
            head.AddToClassList("gr-dropdown__head");
            head.Add(L(resolutionOpen ? "▲" : "▼", "gr-dropdown__arrow"));
            pick.Add(head);
            if (!resolutionOpen) return pick;
            var list = new ScrollView(ScrollViewMode.Vertical) { name = "gr-resolution-list" };
            list.AddToClassList("gr-dropdown__list");
            foreach (var size in sizes)
            {
                var r = size;
                var item = new Button(() => { resolutionOpen = false; s.ChangeDisplay(x => { x.width = r.x; x.height = r.y; }); RefreshCard(); }) { text = r.x + " × " + r.y };
                item.AddToClassList("gr-dropdown__item");
                if (r.x == g.width && r.y == g.height) item.AddToClassList("gr-dropdown__item--on");
                list.Add(item);
            }
            pick.Add(list);
            return pick;
        }

        static List<Vector2Int> Resolutions(GrowthSettings g)
        {
            var list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Where(r => r.x >= 1024 && r.y >= 576).ToList();
            foreach (var common in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                if (common.x <= Display.main.systemWidth && common.y <= Display.main.systemHeight) list.Add(common);
            list.Add(new Vector2Int(g.width, g.height));
            return list.Distinct().OrderBy(r => r.x * r.y).ThenBy(r => r.x).ToList();
        }

        /// <summary>The countdown while a display change waits to be kept.</summary>
        string SettingsLiveText() => s.DisplayPending ? "Going back in " + Mathf.CeilToInt(s.DisplaySecondsLeft) + " s unless kept" : "";

        /// <summary>Validation: a settings slider's value as shown, and its track (for clicking it).</summary>
        public string SettingValue(string name) => card?.Q<Label>("gr-value-" + name)?.text;
        public VisualElement SettingTrack(string name) => card?.Q<VisualElement>("gr-track-" + name);

        /// <summary>GR-23: the interface's size (the panel's scale over its reference resolution).</summary>
        public void SetScale(float scale)
        {
            if (settings != null && Mathf.Abs(settings.scale - scale) > 0.001f) settings.scale = scale;
        }
        public float Scale => settings != null ? settings.scale : 1;
    }
}
