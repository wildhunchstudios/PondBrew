#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-21's and GR-23's Windows route (-evolutionGrowthSettingsSmoke). The music: off in a validation run until the route
    /// turns it on; the night's loop rendered off the main thread and fading in; the three loops' lengths, levels and seams;
    /// the shop opening and the day's loop crossfading in; a cue ducking it; playing on through the pause screen; the
    /// exhibition's loop on its day; its own volume; the night's again at closing. The Settings screen, opened from the pause
    /// screen with the route's mouse: the graphics presets and each choice in play in the world and the camera, a slider's
    /// buttons and its track; a new window asked to be kept, going back by itself or when asked, and kept; V-Sync, the frame
    /// limit and the interface's size; every volume and muting in the background; the mouse, invert Y and toggling the use
    /// button; actions rebound to keys and a mouse button, swapping, refused and cancelled, and the words following the keys;
    /// captions and the other accessibility choices; the file read back, an unreadable one kept aside, an older save's
    /// settings carried over; Esc back out; the frame budget at Low and High with the music on.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string SettingsScenarioText = "GR-21 music and GR-23 settings: the three loops made by the game, crossfading night, day and exhibition, ducking under a cue, playing on through the pause screen, on their own volume; the Settings screen from the pause screen: graphics presets and choices in play, sliders, a window change kept or going back by itself, V-Sync, frame limit, interface size, every volume, muting in the background, mouse, invert, hold or toggle, every action rebound to a key or mouse button (swaps, refusals, Esc), the words following the keys, captions and accessibility, the settings file read back, an unreadable file kept aside, an older save's settings carried over";

        static bool Visible(VisualElement e) => e.worldBound.width > 0 && e.worldBound.height > 0;
        IEnumerator ClickNamed(string name, string what) => ClickOn(e => e.name == name && Visible(e), what);
        IEnumerator ClickStep(string slider, string text, string what) => ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == text && b.parent != null && b.parent.name == "gr-slider-" + slider && Visible(e), what);
        IEnumerator ClickTab(string tab) => ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == tab && b.ClassListContains("gr-nb-tab"), "Click the " + tab + " tab");
        AntialiasingMode CameraAa => s.Player.View.GetUniversalAdditionalCameraData().antialiasing;
        float MusicLevel => GrowthMusic.BaseGain * s.MusicVolume * s.Music.Duck;

        IEnumerator SettingsScenario()
        {
            limitations.Add("GR-21 and GR-23 settings route: the fixture is the development start point for the look (GrowthStarts.LookStart, labelled), at night. The music is off in validation runs; the route turns it on (labelled). The shop is opened and closed with its own actions (as the counter card's buttons do; the day's end is its clock set, labelled), the exhibition's day is its flag set (labelled), and a sale's cue is passed to the music as the session passes the shop's events (labelled). The pause screen's Settings button, the tabs, the presets, choices, switches, slider buttons, a slider's track, the resolution's arrows, Keep them, Go back, Change and Back to the defaults are clicked with the route's mouse; new keys are pressed with the route's keyboard and a new mouse button with its mouse. A validation run never changes its own window: a display change records what it would ask of Windows (labelled) and the keep-or-revert countdown runs as in the game. Frame times are measured uncapped (vSync off) for 4 s at Low and at High with the music playing. How the music sounds is for the user to judge.");
            yield return Opening();
            TakeInput();
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Look));
            var shop = s.Shop; var w = s.World; var music = s.Music; var g = s.Settings;
            yield return null; yield return null;
            Check(shop.Phase == GrowthPhase.Night && shop.Workshop && shop.Hall, "Fixture: the look start point, at night (labelled)");

            // ---------------------------------------------------------------- GR-21: the music
            Check(music != null && music.Playing == null && !music.CurrentPlaying && !music.Ready(MusicTrack.Night), "In a validation run the music is off and nothing is rendered until a route turns it on (every other route plays none)");
            float asked = Time.realtimeSinceStartup;
            music.OnInValidation = true;
            yield return Wait(() => music.Playing == MusicTrack.Night && music.CurrentPlaying, 30, "the night's loop");
            float nightReady = Time.realtimeSinceStartup - asked;
            yield return Wait(() => Mathf.Abs(music.CurrentVolume - MusicLevel) < 0.005f, 5, "the night's loop to fade in");
            Check(nightReady < 10, "The night's loop, made by the game, ready " + nightReady.ToString("0.0") + " s after it was asked for (rendered off the main thread, the game running meanwhile) and faded in to " + music.CurrentVolume.ToString("0.00") + " (Music 70)");
            yield return Wait(() => music.Ready(MusicTrack.Day) && music.Ready(MusicTrack.Exhibition), 60, "the day's and the exhibition's loops");
            foreach (MusicTrack track in System.Enum.GetValues(typeof(MusicTrack)))
            {
                var clip = music.ClipOf(track); var piece = GrowthMusicScore.Piece(track);
                var data = new float[clip.samples * clip.channels]; clip.GetData(data, 0);
                var (peak, rms) = GrowthMusicScore.Level(data);
                float step = 0; for (int i = 2; i < data.Length; i += 2) step = Mathf.Max(step, Mathf.Abs(data[i] - data[i - 2]));
                float seam = Mathf.Abs(data[0] - data[data.Length - 2]);
                Check(clip.channels == 2 && clip.frequency == GrowthMusicScore.SampleRate && Mathf.Abs(clip.length - (float)piece.Seconds) < 0.01f && peak <= GrowthMusicScore.PeakLimit + 0.001f && rms > 0.05f && seam <= step,
                    "\"" + piece.Title + "\": " + piece.Seconds.ToString("0.0") + " s at " + piece.Bpm + " bpm, " + piece.Notes.Count + " notes, stereo at 32 kHz, peak " + peak.ToString("0.00") + " (never clipping), " +
                    (20 * Mathf.Log10(rms)).ToString("0.0") + " dBFS, its end running into its start (a step of " + seam.ToString("0.000") + ", the loop's largest " + step.ToString("0.000") + ")");
            }
            Ok(s.Act(shop.OpenShop), "Open the shop (the counter card's action)");
            yield return Wait(() => music.Playing == MusicTrack.Day, 2, "the day's loop");
            yield return new WaitForSeconds(1.2f);
            float rising = music.CurrentVolume, falling = music.PreviousVolume;
            Check(rising > 0.03f && rising < MusicLevel - 0.03f && falling > 0.03f, "The shop opens: the day's loop crossfades in (" + rising.ToString("0.00") + ") as the night's fades out (" + falling.ToString("0.00") + ")");
            yield return new WaitForSeconds(2.3f);
            Check(music.PreviousVolume == 0 && Mathf.Abs(music.CurrentVolume - MusicLevel) < 0.01f, "Three seconds on, only the day's loop plays (" + music.CurrentVolume.ToString("0.00") + ")");
            int ducks = music.Ducks;
            music.Event(new GrowthEvent { Kind = GrowthEventKind.Sale });
            yield return new WaitForSeconds(0.3f);
            Check(music.Ducks > ducks && music.Duck < 0.6f && music.CurrentVolume < GrowthMusic.BaseGain * s.MusicVolume * 0.6f, "A sale's cue: the music dips under it (to " + music.CurrentVolume.ToString("0.00") + ")");
            yield return Wait(() => music.Duck > 0.99f, GrowthMusic.DuckHold + GrowthMusic.DuckRelease + 6, "the music back up after the cue");
            Check(true, "and comes back up after it");
            s.SetCard(GrowthCard.Pause);
            yield return new WaitForSeconds(0.5f);
            Check(music.CurrentPlaying && music.Playing == MusicTrack.Day && music.CurrentVolume > 0.1f, "Paused: the music plays on");
            s.SetCard(GrowthCard.None);
            shop.ExhibitionToday = true;
            yield return Wait(() => music.Playing == MusicTrack.Exhibition, 2, "the exhibition's loop");
            yield return new WaitForSeconds(3.3f);
            Check(music.PreviousVolume == 0 && music.CurrentVolume > 0.1f, "The Grand Exhibition's day (its flag set, labelled): its own loop crossfades in");
            g.music = 0; yield return null; yield return null;
            Check(music.CurrentVolume == 0, "Music 0: silent");
            g.music = 100; yield return null; yield return null;
            Check(Mathf.Abs(music.CurrentVolume - MusicLevel) < 0.01f && MusicLevel > GrowthMusic.BaseGain * 0.5f, "Music 100: at its loudest (" + music.CurrentVolume.ToString("0.00") + ")");
            g.music = 70;
            shop.ExhibitionToday = false;
            yield return Wait(() => music.Playing == MusicTrack.Day, 2, "the day's loop after the exhibition");
            shop.DaySecondsLeft = 0.05f;
            yield return Wait(() => shop.Phase == GrowthPhase.Night, 3, "closing time");
            if (s.Card == GrowthCard.Receipt) s.SetCard(GrowthCard.None);
            yield return Wait(() => music.Playing == MusicTrack.Night, 3, "the night's loop again");
            Check(true, "Closing time: the night's loop again");

            // ---------------------------------------------------------------- GR-23: the Settings screen
            s.SetCard(GrowthCard.Pause);
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("graphics, display, sound, controls and accessibility"), "The pause screen has Settings (its old rows moved there)");
            yield return Shot("700-pause");
            yield return ClickNamed("gr-open-settings", "Click Settings on the pause screen");
            Check(s.Card == GrowthCard.Settings && s.Ui.CurrentTab == "Graphics" && GrowthUi.SettingsTabs.All(t => s.Ui.CardText().Contains(t)) && s.Paused,
                "The Settings screen opens on Graphics, with its five tabs, the game paused");
            Check(g.preset == 2 && w.SunShadows == LightShadows.Soft && w.BloomOn && w.ReflectionsOn && CameraAa == AntialiasingMode.SubpixelMorphologicalAntiAliasing && s.RenderScaleNow == 1f && !w.LowDetail,
                "High unless changed: soft shadows, bloom, reflections, SMAA, the shop at full size, creatures in full detail");
            yield return Shot("701-settings-graphics");

            // ---- Graphics
            int previousRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            yield return ClickNamed("gr-choice-quality-low", "Click Low");
            Check(g.preset == 0 && w.SunShadows == LightShadows.None && !w.BloomOn && !w.ReflectionsOn && CameraAa == AntialiasingMode.None && Mathf.Abs(s.RenderScaleNow - 0.75f) < 0.001f && w.LowDetail,
                "Low: no shadows, no bloom, no reflections, no anti-aliasing, the shop drawn at 75%, creatures in Low detail");
            s.SetCard(GrowthCard.None);
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            yield return Measure("710-low-preset", new Vector3(1.25f, 0, -1.9f), new Vector3(0.6f, 1.6f, 1.9f), 4);
            s.OpenSettings("Graphics");
            yield return ClickNamed("gr-choice-quality-high", "Click High");
            Check(g.preset == 2 && w.SunShadows == LightShadows.Soft && w.BloomOn && w.ReflectionsOn && CameraAa == AntialiasingMode.SubpixelMorphologicalAntiAliasing && s.RenderScaleNow == 1f && !w.LowDetail, "High again: every choice back");
            s.SetCard(GrowthCard.None);
            yield return Measure("711-high-preset", new Vector3(1.25f, 0, -1.9f), new Vector3(0.6f, 1.6f, 1.9f), 4);
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVSync;
            s.OpenSettings("Graphics");
            yield return ClickNamed("gr-choice-shadows-hard", "Click Hard shadows");
            Check(w.SunShadows == LightShadows.Hard && g.MatchingPreset() == 3 && s.Ui.CardText().Contains("Custom"), "One choice changed: hard shadows, and the quality reads Custom");
            yield return ClickNamed("gr-choice-quality-high", "Click High");
            yield return ClickStep("fov", "+", "Click + on Field of view");
            yield return null; yield return null;
            Check(g.fieldOfView == 70 && Mathf.Abs(s.Player.View.fieldOfView - 70) < 0.01f && s.Ui.SettingValue("fov") == "70°", "Field of view + : 70°, the camera wider");
            yield return ClickStep("fov", "−", "Click − on Field of view");
            Check(g.fieldOfView == GrowthSettings.DefaultFov, "and back to 68°");
            yield return ClickStep("brightness", "+", "Click + on Brightness");
            Check(Mathf.Abs(w.ExposureOffset - 0.15f) < 0.001f && s.Ui.SettingValue("brightness") == "+1", "Brightness +1: the look's exposure up 0.15 of a stop");
            yield return ClickStep("brightness", "−", "Click − on Brightness");
            yield return ClickOn(e => e.name == "gr-track-render-scale" && Visible(e), "Press the render scale's track at its middle");
            Check(g.renderScale == 75 && Mathf.Abs(s.RenderScaleNow - 0.75f) < 0.001f && s.Ui.SettingValue("render-scale") == "75%", "Pressed at its middle, the track sets the render scale there: 75%");
            yield return ClickNamed("gr-choice-detail-low", "Click Low creature detail");
            Check(w.LowDetail && g.creatureDetail == 0, "Creature detail Low (GR.15.1's Low detail): the world draws creatures simply nearer");
            yield return ClickNamed("gr-choice-quality-high", "Click High");
            Check(g.renderScale == 100 && !w.LowDetail && File.ReadAllText(s.SettingsPath).Contains("\"renderScale\": 100"), "High again, and every change kept in the whole game's settings file (" + Path.GetFileName(s.SettingsPath) + ")");

            // ---- Display
            yield return ClickTab("Display");
            int width = g.width, height = g.height;
            yield return ClickNamed("gr-choice-window-borderless-full-screen", "Click Borderless full screen");
            Check(s.DisplayPending && g.windowMode == 0 && s.DisplayAsked.Contains("FullScreenWindow") && s.Ui.CardText().Contains("Keep these display settings?") && s.Ui.CardText().Contains("Going back in"),
                "A new window mode asks to be kept, going back in 10 s (the route's own window left as it is; asked: " + s.DisplayAsked + ")");
            yield return Shot("702-display-keep");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "Go back", "Click Go back");
            Check(!s.DisplayPending && g.windowMode == 1 && !g.displayChosen, "Go back: windowed again, as the game opened");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.parent != null && b.parent.name == "gr-resolution" && b.enabledSelf && (b.text == "▶" || b.text == "◀"), "Click an arrow of the resolution");
            Check(s.DisplayPending && (g.width != width || g.height != height), "Another resolution: " + g.width + " × " + g.height + ", asking to be kept");
            float left = Time.realtimeSinceStartup;
            yield return Wait(() => !s.DisplayPending, 13, "the display change to go back by itself");
            float took = Time.realtimeSinceStartup - left;
            Check(took > 8.5f && took < 11.5f && g.width == width && g.height == height, "Left alone, it goes back by itself after 10 s (" + took.ToString("0.0") + " s): " + g.width + " × " + g.height);
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.parent != null && b.parent.name == "gr-resolution" && b.enabledSelf && (b.text == "▶" || b.text == "◀"), "Click an arrow of the resolution again");
            int keptWidth = g.width;
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "Keep them", "Click Keep them");
            Check(!s.DisplayPending && g.displayChosen && g.width == keptWidth && File.ReadAllText(s.SettingsPath).Contains("\"width\": " + keptWidth), "Keep them: the new resolution kept (" + g.width + " × " + g.height + "), from now on the game opens so");
            yield return ClickNamed("gr-switch-vsync", "Switch V-Sync on");
            Check(g.vSync && s.TargetFrameRate == -1 && s.Ui.CardText().Contains("V-Sync sets the frame rate"), "V-Sync on: the screen sets the frame rate, the limit is set aside");
            yield return ClickNamed("gr-switch-vsync", "Switch V-Sync off");
            yield return ClickNamed("gr-choice-frames-60", "Click a frame limit of 60");
            Check(!g.vSync && s.TargetFrameRate == 60, "A frame limit of 60");
            yield return ClickNamed("gr-choice-frames-120", "Click a frame limit of 120");
            Check(s.TargetFrameRate == 120, "and 120 again");
            yield return ClickStep("ui-scale", "+", "Click + on Interface size");
            Check(Mathf.Abs(s.Ui.Scale - 1.1f) < 0.001f && g.uiScale == 110, "Interface size 110%: the cards and the HUD larger");
            yield return Shot("703-interface-110");
            yield return ClickStep("ui-scale", "−", "Click − on Interface size");
            Check(Mathf.Abs(s.Ui.Scale - 1f) < 0.001f, "and 100% again");

            // ---- Sound
            yield return ClickTab("Sound");
            yield return ClickStep("master", "−", "Click − on Master volume");
            Check(g.master == 95 && Mathf.Abs(AudioListener.volume - 0.95f) < 0.001f, "Master 95: everything a little quieter");
            yield return ClickStep("master", "+", "Click + on Master volume");
            yield return ClickStep("music", "−", "Click − on Music");
            yield return null; yield return null;
            Check(g.music == 65 && Mathf.Abs(music.CurrentVolume - MusicLevel) < 0.02f, "Music 65: the music alone quieter");
            yield return ClickStep("music", "+", "Click + on Music");
            yield return ClickOn(e => e.name == "gr-track-sounds" && Visible(e), "Press the Sounds track at its middle");
            Check(g.sounds == 50 && Mathf.Abs(s.SoundVolume - 0.5f) < 0.001f && s.SoundLevel == GrowthAudio.Level.Half, "Sounds 50: the stations' sounds at half");
            Check(g.muteInBackground && s.ListenerVolume(false) == 0 && Mathf.Abs(s.ListenerVolume(true) - 1) < 0.001f, "Muted in the background (on unless changed): silent while another window is in front");
            yield return ClickNamed("gr-switch-mute", "Switch Mute in the background off");
            Check(!g.muteInBackground && Mathf.Abs(s.ListenerVolume(false) - 1) < 0.001f, "Off: it plays on behind another window");
            yield return Shot("704-settings-sound");
            yield return ClickOn(e => e.name == "gr-defaults" && Visible(e), "Click Back to the defaults on Sound");
            Check(g.sounds == 100 && g.music == 70 && g.muteInBackground, "Back to the defaults: every volume as it was");

            // ---- Controls
            yield return ClickTab("Controls");
            yield return ClickStep("sensitivity", "+", "Click + on Mouse sensitivity");
            yield return null; yield return null;
            Check(Mathf.Abs(g.sensitivity - 1.05f) < 0.001f && Mathf.Abs(s.Player.Sensitivity - 1.05f) < 0.001f, "Sensitivity 1.05×, on the player");
            yield return ClickNamed("gr-rebind-interact", "Click Change for Interact");
            Check(s.Rebinding == GrowthAction.Interact && s.Ui.CardText().Contains("Press a key or a mouse button for Open a menu"), "Interact waits for its new key");
            yield return Shot("705-controls-waiting");
            yield return Press(Key.Q);
            Check(s.Rebinding == null && g.InputOf(GrowthAction.Interact) == "Key/Q" && s.RebindNote == "Open a menu is now on Q" && File.ReadAllText(s.SettingsPath).Contains("\"input\": \"Key/Q\""), "Q pressed: Interact is on Q, kept (\"" + s.RebindNote + "\")");
            yield return ClickNamed("gr-rebind-journal", "Click Change for Journal");
            yield return Press(Key.Q);
            Check(g.InputOf(GrowthAction.Journal) == "Key/Q" && g.InputOf(GrowthAction.Interact) == "Key/Tab", "Q for the journal: the two swap, Interact moving to Tab (\"" + s.RebindNote + "\")");
            yield return ClickNamed("gr-rebind-slap", "Click Change for Slap");
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2), buttons = 2 });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2) });
            yield return null; yield return null;
            Check(s.Rebinding == null && g.InputOf(GrowthAction.Slap) == "Mouse/Right" && s.Ui.CardText().Contains("Right mouse button"), "The right mouse button for the slap: any mouse button can be an action's");
            yield return ClickNamed("gr-rebind-skip", "Click Change for Skip a silly moment");
            yield return Press(Key.F8);
            Check(s.Rebinding == GrowthAction.Skip && s.RebindNote != null && s.RebindNote.Contains("F8 is kept for development"), "F8 refused (\"" + s.RebindNote + "\"), still waiting");
            yield return Press(Key.Escape);
            Check(s.Rebinding == null && s.Card == GrowthCard.Settings && g.InputOf(GrowthAction.Skip) == "Key/Backspace", "Esc while waiting: Skip stays on Backspace, and the screen stays open");
            Check(GrowthControls.Keyed("Open the incubator (E): put two or three doses in") == "Open the incubator (Tab): put two or three doses in" &&
                  GrowthControls.Keyed("Tab: journal · Esc: pause") == "Q: journal · Esc: pause", "The game's words name the keys as bound: (E) reads (Tab), Tab reads Q");
            yield return Shot("706-settings-controls");
            // The keys in play: E does nothing now, Tab opens what is under the reticle, Q the journal.
            s.SetCard(GrowthCard.None);
            s.AllowValidationInput = true;
            yield return AimAt(GrowthWorld.CounterPosition + new Vector3(0, 0, 1.4f), GrowthWorld.CounterPosition + new Vector3(0, 1.05f, 0), GrowthTargetKind.Counter, "counter", "The reticle focuses the counter");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.None, "E pressed at the counter: nothing (Interact is on Tab now)");
            yield return Press(Key.Tab);
            Check(s.Card == GrowthCard.Counter, "Tab pressed at the counter: its card opens");
            s.SetCard(GrowthCard.None); yield return null;
            yield return Press(Key.Q);
            Check(s.Card == GrowthCard.Journal, "Q: the journal");
            s.SetCard(GrowthCard.None); yield return null;
            // Invert Y: the mouse moved up looks up, or down when inverted.
            float pitch = s.Player.Pitch;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2), delta = new Vector2(0, 20) });
            yield return null; yield return null;
            float up = s.Player.Pitch - pitch;
            g.invertY = true; yield return null;
            pitch = s.Player.Pitch;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2), delta = new Vector2(0, 20) });
            yield return null; yield return null;
            float inverted = s.Player.Pitch - pitch;
            g.invertY = false;
            Check(up < -0.5f && inverted > 0.5f, "The mouse moved up looks up (" + up.ToString("0.00") + "°), and down with Invert mouse on (" + inverted.ToString("+0.00") + "°)");
            // Toggle: one press starts cranking, it runs on, another press stops it.
            g.useToggle = true;
            shop.Levels["generator"] = 1;
            yield return Wait(() => w.GeneratorLookLevel == 1, 3, "the hand crank rebuilt");
            yield return new WaitForFixedUpdate();
            var crankAt = w.GeneratorPlace;
            yield return AimAt(crankAt + new Vector3(0, 0, -1.6f), crankAt + new Vector3(0, 0.9f, 0), GrowthTargetKind.Generator, null, "The reticle focuses the hand crank");
            Hold(true); yield return null; yield return null; Hold(false);
            yield return new WaitForSeconds(0.6f);
            Check(s.UseToggled && s.CrankingNow, "Use as a toggle: one click and the crank turns on by itself");
            Hold(true); yield return null; yield return null; Hold(false);
            yield return null; yield return null;
            Check(!s.UseToggled && !s.CrankingNow, "another click stops it");
            g.useToggle = false;
            shop.Levels["generator"] = 4;
            s.AllowValidationInput = false;
            s.OpenSettings("Controls");
            yield return ClickOn(e => e.name == "gr-defaults" && Visible(e), "Click Back to the defaults on Controls");
            Check(g.InputOf(GrowthAction.Interact) == "Key/E" && g.InputOf(GrowthAction.Journal) == "Key/Tab" && g.InputOf(GrowthAction.Slap) == "Key/F" && g.sensitivity == 1 && !g.useToggle,
                "Back to the defaults: every key and button as it was, the mouse at 1×, the use button held");

            // ---- Accessibility
            yield return ClickTab("Accessibility");
            yield return ClickNamed("gr-switch-captions", "Switch Captions off");
            s.Ui.Caption("Pip", "A test caption");
            Check(g.captionsOff && s.Ui.CaptionShown == null && s.Ui.Log.Contains("Pip: A test caption"), "Captions off: nothing on screen (the words still in the log)");
            yield return ClickNamed("gr-switch-captions", "Switch Captions on");
            s.Ui.Caption("Pip", "A test caption");
            Check(s.Ui.CaptionShown == "Pip: A test caption", "Captions on: shown");
            s.Ui.ClearCaption();
            yield return ClickNamed("gr-choice-gags-fewer", "Click Fewer silly moments");
            yield return ClickNamed("gr-switch-reduced", "Switch Reduced motion on");
            yield return ClickNamed("gr-switch-peek", "Switch Peek off");
            Check(s.GagLevel == GrowthGagLevel.Fewer && s.ReducedMotion && !s.PeekOn, "Fewer silly moments, reduced motion, peek off: each in play");
            yield return Shot("707-settings-accessibility");
            yield return ClickOn(e => e.name == "gr-defaults" && Visible(e), "Click Back to the defaults on Accessibility");
            Check(s.GagLevel == GrowthGagLevel.Full && !s.ReducedMotion && s.PeekOn && !g.captionsOff, "and back to the defaults");

            // ---- The file
            var back = GrowthSettingsStore.Load(s.SettingsPath, null, out string backNotice);
            Check(backNotice == null && back.width == g.width && back.displayChosen && back.music == g.music && back.fieldOfView == g.fieldOfView && back.InputOf(GrowthAction.Slap) == "Key/F",
                "Read back from its file, the same settings");
            string dir = Path.GetDirectoryName(s.SettingsPath);
            string bad = Path.Combine(dir, "settings-unreadable-test.json");
            File.WriteAllText(bad, "{ this is not json");
            var reset = GrowthSettingsStore.Load(bad, null, out string badNotice);
            Check(badNotice != null && reset.music == 70 && !File.Exists(bad) && Directory.GetFiles(dir, "settings-unreadable-test.unreadable-*.json").Length == 1,
                "An unreadable settings file: the defaults, and the file kept aside (\"" + badNotice + "\")");
            string older = Path.Combine(dir, "older-save.json");
            File.WriteAllText(older, JsonUtility.ToJson(new GrowthSave { soundLevel = 1, humLevel = 2, humourLevel = 1, gagLevel = 1, peekOff = true, reducedMotion = true, sensitivity = 1.5f, slapKey = "G" }));
            var carried = GrowthSettingsStore.Load(Path.Combine(dir, "no-settings-yet.json"), older, out _);
            Check(carried.sounds == 50 && carried.hum == 0 && carried.humour == 50 && carried.gags == 1 && carried.peekOff && carried.reducedMotion && Mathf.Abs(carried.sensitivity - 1.5f) < 0.001f &&
                  carried.InputOf(GrowthAction.Slap) == "Key/G" && carried.carriedFrom == "older-save.json",
                "No settings file yet: an older save's settings carried over once (sounds half, hum off, humour half, fewer moments, peek off, reduced motion, the mouse at 1.5×, the slap on G)");

            // ---- Esc back out
            yield return Press(Key.Escape);
            Check(s.Card == GrowthCard.Pause, "Esc on the Settings screen: back to the pause screen");
            yield return Press(Key.Escape);
            Check(s.Card == GrowthCard.None, "Esc again: back to the game");
        }
    }
}
#endif
