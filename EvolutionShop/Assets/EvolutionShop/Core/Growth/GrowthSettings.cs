using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    /// <summary>GR-23: what the player can do with a key or a mouse button, each remappable.</summary>
    public enum GrowthAction { Forward, Back, Left, Right, Interact, Use, Journal, Slap, Skip }

    /// <summary>GR-23: an action and the key or mouse button it is on: "Key/W" (an Input System key's name) or "Mouse/Left"
    /// (Left, Right, Middle, Back or Forward).</summary>
    [Serializable]
    public sealed class GrowthBinding
    {
        public string action;
        public string input;
    }

    /// <summary>
    /// GR-23 (the user's request 2026-10-30): the game's settings, the whole game's rather than each save's, kept in a file of
    /// their own (the runtime's <c>GrowthSettingsStore</c>). Graphics (a preset, shadows, bloom, reflections, anti-aliasing, the
    /// render scale, the field of view, brightness and creature detail), display (window mode, resolution, V-Sync, the frame
    /// limit and the interface's size), sound (master, music, sounds, hum and humour, 0 to 100, and muting in the background),
    /// controls (the mouse, invert Y, hold or toggle for the use button, and every action's key or mouse button) and
    /// accessibility (reduced motion, peek, the silly moments and captions). Every value read from a file is put back in its
    /// range (<see cref="Sanitize"/>), so a hand-edited or older file can never break the game.
    /// </summary>
    [Serializable]
    public sealed class GrowthSettings
    {
        public const int Version = 1;
        public int version = Version;

        // ---- Graphics
        /// <summary>0 Low, 1 Medium, 2 High; 3 Custom (the values match no preset).</summary>
        public int preset = 2;
        /// <summary>The sun's shadows: 0 off, 1 hard, 2 soft.</summary>
        public int shadows = 2;
        public bool bloom = true, reflections = true;
        /// <summary>0 off, 1 FXAA, 2 SMAA.</summary>
        public int antiAliasing = 2;
        /// <summary>The 3D view drawn at this share of the window, in percent (50 to 100; the interface is always sharp).</summary>
        public int renderScale = 100;
        public int fieldOfView = DefaultFov;
        /// <summary>-5 to 5: a tenth and a half of a stop of exposure each.</summary>
        public int brightness;
        /// <summary>0 Low (GR.15.1's Low detail: creatures drawn simply nearer, frozen sooner), 1 Normal.</summary>
        public int creatureDetail = 1;

        // ---- Display
        /// <summary>0 borderless full screen, 1 windowed, 2 exclusive full screen.</summary>
        public int windowMode = 1;
        public int width = 1280, height = 720;
        /// <summary>Whether the player ever chose the display: until then the game leaves the window as it opened.</summary>
        public bool displayChosen;
        public bool vSync;
        /// <summary>Frames a second at most (0: no limit); ignored with V-Sync on.</summary>
        public int frameLimit = 120;
        /// <summary>The interface's size, in percent (80 to 130).</summary>
        public int uiScale = 100;

        // ---- Sound (0 to 100)
        public int master = 100, music = 70, sounds = 100, hum = 100, humour = 100;
        public bool muteInBackground = true;

        // ---- Controls
        public float sensitivity = 1;
        public bool invertY;
        /// <summary>The use button (collect, bag, pump, crank): held down (false) or pressed once to start and again to stop.</summary>
        public bool useToggle;
        public List<GrowthBinding> bindings = new List<GrowthBinding>();

        // ---- Accessibility
        public bool reducedMotion, peekOff;
        /// <summary>The silly moments: 0 every one, 1 fewer and shorter, 2 none but the player's slap (<see cref="GrowthGagLevel"/>).</summary>
        public int gags;
        public bool captionsOff;
        /// <summary>R09.4: the lessons that explain each part of the game as it appears (on unless switched off).</summary>
        public bool lessonsOff;

        /// <summary>Where these settings first came from: "" new, or the save they were carried over from once.</summary>
        public string carriedFrom = "";

        public const int DefaultFov = 68, MinFov = 60, MaxFov = 90;
        public static readonly string[] PresetNames = { "Low", "Medium", "High", "Custom" };
        public static readonly string[] ShadowNames = { "Off", "Hard", "Soft" };
        public static readonly string[] AntiAliasingNames = { "Off", "FXAA", "SMAA" };
        public static readonly string[] DetailNames = { "Low", "Normal" };
        public static readonly string[] WindowModeNames = { "Borderless full screen", "Windowed", "Exclusive full screen" };
        public static readonly int[] FrameLimits = { 30, 60, 90, 120, 144, 165, 240, 0 };
        public static readonly string[] GagNames = { "Every one", "Fewer", "Off" };

        /// <summary>The keys and buttons each action starts on.</summary>
        public static readonly IReadOnlyDictionary<GrowthAction, string> DefaultInputs = new Dictionary<GrowthAction, string>
        {
            [GrowthAction.Forward] = "Key/W", [GrowthAction.Back] = "Key/S", [GrowthAction.Left] = "Key/A", [GrowthAction.Right] = "Key/D",
            [GrowthAction.Interact] = "Key/E", [GrowthAction.Use] = "Mouse/Left", [GrowthAction.Journal] = "Key/Tab",
            [GrowthAction.Slap] = "Key/F", [GrowthAction.Skip] = "Key/Backspace",
        };

        /// <summary>Esc pauses and closes (and cancels a rebinding); F8 is the development crate: neither can be given to an action.</summary>
        public static readonly string[] Reserved = { "Key/Escape", "Key/F8" };

        public static readonly string[] MouseButtons = { "Left", "Right", "Middle", "Back", "Forward" };

        public static string ActionName(GrowthAction a)
        {
            switch (a)
            {
                case GrowthAction.Forward: return "Walk forward";
                case GrowthAction.Back: return "Walk back";
                case GrowthAction.Left: return "Step left";
                case GrowthAction.Right: return "Step right";
                case GrowthAction.Interact: return "Interact";
                case GrowthAction.Use: return "Use (collect, bag, pump, crank)";
                case GrowthAction.Journal: return "Journal";
                case GrowthAction.Slap: return "Slap";
                default: return "Skip a silly moment";
            }
        }

        public GrowthSettings() { ResetBindings(); }

        // ---------------------------------------------------------------- the presets

        /// <summary>Sets the graphics to a preset (0 Low, 1 Medium, 2 High). The field of view and brightness are the player's
        /// own and stay as they are.</summary>
        public void ApplyPreset(int which)
        {
            which = Clamp(which, 0, 2);
            var (sh, bl, rf, aa, rs, cd) = PresetValues(which);
            shadows = sh; bloom = bl; reflections = rf; antiAliasing = aa; renderScale = rs; creatureDetail = cd;
            preset = which;
        }

        static (int shadows, bool bloom, bool reflections, int aa, int scale, int detail) PresetValues(int which) =>
            which == 0 ? (0, false, false, 0, 75, 0) : which == 1 ? (1, true, false, 1, 100, 1) : (2, true, true, 2, 100, 1);

        /// <summary>The preset the graphics match, or 3 (Custom).</summary>
        public int MatchingPreset()
        {
            for (int i = 0; i <= 2; i++)
            {
                var v = PresetValues(i);
                if (shadows == v.shadows && bloom == v.bloom && reflections == v.reflections && antiAliasing == v.aa && renderScale == v.scale && creatureDetail == v.detail) return i;
            }
            return 3;
        }

        // ---------------------------------------------------------------- the bindings

        public string InputOf(GrowthAction a)
        {
            var b = bindings.FirstOrDefault(x => x.action == a.ToString());
            return b != null ? b.input : DefaultInputs[a];
        }

        public void ResetBindings()
        {
            bindings = Enum.GetValues(typeof(GrowthAction)).Cast<GrowthAction>().Select(a => new GrowthBinding { action = a.ToString(), input = DefaultInputs[a] }).ToList();
        }

        /// <summary>Whether an input's name is one this game can read: "Key/" and a key's name (letters, digits and the
        /// like; the runtime checks it is a real key), or "Mouse/" and one of the five buttons.</summary>
        public static bool WellFormed(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            if (input.StartsWith("Mouse/", StringComparison.Ordinal)) return Array.IndexOf(MouseButtons, input.Substring(6)) >= 0;
            if (!input.StartsWith("Key/", StringComparison.Ordinal) || input.Length <= 4 || input == "Key/None") return false;
            return input.Skip(4).All(char.IsLetterOrDigit);
        }

        /// <summary>
        /// Puts an action on a key or mouse button. Esc and F8 are refused. Another action already on it swaps: it takes this
        /// action's old input, so every action keeps one and no two share one. Returns what happened, in words, or the refusal.
        /// </summary>
        public (bool done, string note) Bind(GrowthAction action, string input)
        {
            if (!WellFormed(input)) return (false, "That can't be used");
            if (Array.IndexOf(Reserved, input) >= 0) return (false, InputName(input) + " is kept for " + (input == "Key/Escape" ? "pausing" : "development"));
            string old = InputOf(action);
            if (old == input) return (true, ActionName(action) + " stays on " + InputName(input));
            var other = bindings.FirstOrDefault(x => x.input == input && x.action != action.ToString());
            Set(action, input);
            if (other != null)
            {
                other.input = old;
                var otherAction = (GrowthAction)Enum.Parse(typeof(GrowthAction), other.action);
                return (true, ActionName(action) + " is now on " + InputName(input) + "; " + ActionName(otherAction) + " moved to " + InputName(old));
            }
            return (true, ActionName(action) + " is now on " + InputName(input));
        }

        void Set(GrowthAction action, string input)
        {
            var b = bindings.FirstOrDefault(x => x.action == action.ToString());
            if (b == null) bindings.Add(new GrowthBinding { action = action.ToString(), input = input }); else b.input = input;
        }

        /// <summary>An input's name for people: "W", "Tab", "Left mouse button", "1".</summary>
        public static string InputName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "nothing";
            if (input.StartsWith("Mouse/", StringComparison.Ordinal)) return input.Substring(6) + " mouse button";
            string key = input.StartsWith("Key/", StringComparison.Ordinal) ? input.Substring(4) : input;
            if (key.StartsWith("Digit", StringComparison.Ordinal) && key.Length == 6) return key.Substring(5);
            if (key.StartsWith("Numpad", StringComparison.Ordinal) && key.Length == 7 && char.IsDigit(key[6])) return "Numpad " + key[6];
            switch (key)
            {
                case "LeftShift": return "Left Shift";
                case "RightShift": return "Right Shift";
                case "LeftCtrl": return "Left Ctrl";
                case "RightCtrl": return "Right Ctrl";
                case "LeftAlt": return "Left Alt";
                case "RightAlt": return "Right Alt";
                case "UpArrow": return "Up arrow";
                case "DownArrow": return "Down arrow";
                case "LeftArrow": return "Left arrow";
                case "RightArrow": return "Right arrow";
                case "Backquote": return "`";
                default: return key;
            }
        }

        // ---------------------------------------------------------------- reading

        /// <summary>Puts every value back in its range and every action on a key or button of its own (an unknown, reserved or
        /// shared one goes back to its default, or to a free default). Returns the settings, for chaining.</summary>
        public GrowthSettings Sanitize(Func<string, bool> realKey = null)
        {
            version = Version;
            preset = Clamp(preset, 0, 3);
            shadows = Clamp(shadows, 0, 2);
            antiAliasing = Clamp(antiAliasing, 0, 2);
            renderScale = Clamp((int)Math.Round(renderScale / 5.0) * 5, 50, 100);
            fieldOfView = Clamp(fieldOfView, MinFov, MaxFov);
            brightness = Clamp(brightness, -5, 5);
            creatureDetail = Clamp(creatureDetail, 0, 1);
            windowMode = Clamp(windowMode, 0, 2);
            if (width < 640 || height < 360 || width > 16384 || height > 16384) { width = 1280; height = 720; }
            if (Array.IndexOf(FrameLimits, frameLimit) < 0) frameLimit = 120;
            uiScale = Clamp((int)Math.Round(uiScale / 10.0) * 10, 80, 130);
            master = Clamp(master, 0, 100); music = Clamp(music, 0, 100); sounds = Clamp(sounds, 0, 100);
            hum = Clamp(hum, 0, 100); humour = Clamp(humour, 0, 100);
            if (float.IsNaN(sensitivity) || sensitivity < 0.25f || sensitivity > 3f) sensitivity = 1;
            sensitivity = (float)Math.Round(sensitivity * 20) / 20f;
            gags = Clamp(gags, 0, 2);
            if (carriedFrom == null) carriedFrom = "";
            preset = MatchingPreset();

            // Bindings: each action once, on a real, unreserved input of its own.
            var wanted = new Dictionary<GrowthAction, string>();
            foreach (var b in bindings ?? new List<GrowthBinding>())
            {
                if (b == null || !Enum.TryParse(b.action, out GrowthAction a) || wanted.ContainsKey(a)) continue;
                if (!WellFormed(b.input) || Array.IndexOf(Reserved, b.input) >= 0) continue;
                if (realKey != null && b.input.StartsWith("Key/", StringComparison.Ordinal) && !realKey(b.input.Substring(4))) continue;
                if (wanted.ContainsValue(b.input)) continue;
                wanted[a] = b.input;
            }
            foreach (GrowthAction a in Enum.GetValues(typeof(GrowthAction)))
            {
                if (wanted.ContainsKey(a)) continue;
                string d = DefaultInputs[a];
                if (wanted.ContainsValue(d))
                    d = DefaultInputs.Values.Concat(Spares).FirstOrDefault(x => !wanted.ContainsValue(x) && !Enum.GetValues(typeof(GrowthAction)).Cast<GrowthAction>().Any(o => o != a && !wanted.ContainsKey(o) && DefaultInputs[o] == x));
                wanted[a] = d;
            }
            bindings = Enum.GetValues(typeof(GrowthAction)).Cast<GrowthAction>().Select(a => new GrowthBinding { action = a.ToString(), input = wanted[a] }).ToList();
            return this;
        }

        /// <summary>Keys an action falls back to when its own default is taken (a hand-edited file).</summary>
        static readonly string[] Spares = { "Key/Q", "Key/R", "Key/G", "Key/Z", "Key/X", "Key/C", "Key/V", "Key/B", "Key/T", "Key/Y" };

        /// <summary>
        /// The settings an older save kept with it (peek, reduced motion, the mouse, the sounds, the hum, the silly moments and
        /// the slap's key), carried over once into the whole game's settings; everything else is as new.
        /// </summary>
        public static GrowthSettings FromSave(GrowthSave save, string from)
        {
            var s = new GrowthSettings();
            if (save == null) return s;
            s.peekOff = save.peekOff; s.reducedMotion = save.reducedMotion;
            if (save.sensitivity >= 0.25f && save.sensitivity <= 3f) s.sensitivity = save.sensitivity;
            s.sounds = LevelVolume(save.soundLevel); s.hum = LevelVolume(save.humLevel); s.humour = LevelVolume(save.humourLevel);
            s.gags = Clamp(save.gagLevel, 0, 2);
            if (!string.IsNullOrEmpty(save.slapKey) && WellFormed("Key/" + save.slapKey)) s.Bind(GrowthAction.Slap, "Key/" + save.slapKey);
            s.carriedFrom = from ?? "";
            return s.Sanitize();
        }

        /// <summary>An older save's level (0 full, 1 half, 2 off) as a volume.</summary>
        public static int LevelVolume(int level) => level == 1 ? 50 : level == 2 ? 0 : 100;

        public GrowthSettings Copy()
        {
            var c = (GrowthSettings)MemberwiseClone();
            c.bindings = bindings.Select(b => new GrowthBinding { action = b.action, input = b.input }).ToList();
            return c;
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
