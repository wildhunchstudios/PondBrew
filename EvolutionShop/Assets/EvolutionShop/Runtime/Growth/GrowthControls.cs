using System;
using System.IO;
using System.Text.RegularExpressions;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EvolutionShop
{
    /// <summary>
    /// GR-23: the player's actions read through their bindings (<see cref="GrowthSettings.bindings"/>): each action is on a
    /// key or a mouse button the player chose, read from the current keyboard and mouse. Esc (pause and close) and F8 (the
    /// development crate) stay where they are. <see cref="Listen"/> catches the next key or button for a rebinding.
    /// </summary>
    public static class GrowthControls
    {
        /// <summary>The settings whose bindings are read (the session's); the defaults until a session sets them.</summary>
        public static GrowthSettings Settings = new GrowthSettings();
        /// <summary>The Input System's placeholder for the IME state (an obsolete key that is never a binding).</summary>
        const Key ImeKey = (Key)111;

        public static ButtonControl Control(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            if (input.StartsWith("Mouse/", StringComparison.Ordinal))
            {
                var mouse = Mouse.current;
                if (mouse == null) return null;
                switch (input.Substring(6))
                {
                    case "Left": return mouse.leftButton;
                    case "Right": return mouse.rightButton;
                    case "Middle": return mouse.middleButton;
                    case "Back": return mouse.backButton;
                    case "Forward": return mouse.forwardButton;
                    default: return null;
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard == null || !input.StartsWith("Key/", StringComparison.Ordinal) || !TryKey(input.Substring(4), out var key)) return null;
            return keyboard[key];
        }

        public static bool TryKey(string name, out Key key) { key = Key.None; return !string.IsNullOrEmpty(name) && char.IsLetter(name[0]) && Enum.TryParse(name, out key) && key != Key.None && key != ImeKey && Enum.IsDefined(typeof(Key), key); }

        /// <summary>Whether a name is one of the Input System's keys (for <see cref="GrowthSettings.Sanitize"/>).</summary>
        public static bool RealKey(string name) => TryKey(name, out _);

        public static bool Held(GrowthAction action) { var c = Control(Settings.InputOf(action)); return c != null && c.isPressed; }
        public static bool Pressed(GrowthAction action) { var c = Control(Settings.InputOf(action)); return c != null && c.wasPressedThisFrame; }

        /// <summary>The key an action is on, as an Input System key (Key.None for a mouse button).</summary>
        public static Key KeyOf(GrowthAction action)
        {
            string input = Settings.InputOf(action);
            return input.StartsWith("Key/", StringComparison.Ordinal) && TryKey(input.Substring(4), out var key) ? key : Key.None;
        }

        public static string NameOf(GrowthAction action) => GrowthSettings.InputName(Settings.InputOf(action));

        static readonly Regex KeyWords = new Regex(@"(?<![A-Za-z0-9_'’-])(E|Tab)(?![A-Za-z0-9_'’-])");

        /// <summary>
        /// The game's words with its keys as the player bound them: "E" (Interact), "Tab" (the journal) and "the mouse" in "hold
        /// the mouse" (Use) become the player's keys. With the default keys the words are as written.
        /// </summary>
        public static string Keyed(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string e = Settings.InputOf(GrowthAction.Interact), tab = Settings.InputOf(GrowthAction.Journal), use = Settings.InputOf(GrowthAction.Use);
            if (e != "Key/E" || tab != "Key/Tab")
                text = KeyWords.Replace(text, m => GrowthSettings.InputName(m.Value == "E" ? e : tab));
            if (use != "Mouse/Left" && text.IndexOf("the mouse", StringComparison.Ordinal) >= 0)
                text = text.Replace("the mouse", use.StartsWith("Mouse/", StringComparison.Ordinal) ? "the " + use.Substring(6).ToLowerInvariant() + " mouse button" : GrowthSettings.InputName(use));
            // GR-29: "right-click" is the take button and "click" the use button, as the player bound them.
            string take = Settings.InputOf(GrowthAction.Take);
            if (take != "Mouse/Right") text = text.Replace("right-click", GrowthSettings.InputName(take));
            if (use != "Mouse/Left") text = ClickWords.Replace(text, m => GrowthSettings.InputName(use));
            return text;
        }

        static readonly Regex ClickWords = new Regex(@"(?<![A-Za-z0-9_'’-])[Cc]lick(?![A-Za-z0-9_'’-])");

        /// <summary>The key or mouse button pressed this frame, as a binding's input ("Key/G", "Mouse/Right"), or null.</summary>
        public static string Listen()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                foreach (var k in keyboard.allKeys)
                    if (k != null && k.wasPressedThisFrame && k.keyCode != Key.None && k.keyCode != ImeKey) return "Key/" + k.keyCode;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) return "Mouse/Left";
                if (mouse.rightButton.wasPressedThisFrame) return "Mouse/Right";
                if (mouse.middleButton.wasPressedThisFrame) return "Mouse/Middle";
                if (mouse.backButton.wasPressedThisFrame) return "Mouse/Back";
                if (mouse.forwardButton.wasPressedThisFrame) return "Mouse/Forward";
            }
            return null;
        }

        /// <summary>Whether any key or mouse button is down (a rebinding waits until all are up before it listens).</summary>
        public static bool AnyHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed) return true;
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed || mouse.backButton.isPressed || mouse.forwardButton.isPressed);
        }
    }

    /// <summary>
    /// GR-23: the whole game's settings file (the user's choice: one for the game, not one per save), beside the saves, or in
    /// a validation run's evidence folder. A missing file starts from the defaults, carrying over once the settings an older
    /// save kept; an unreadable one is kept aside and the defaults used; every value read is put back in its range.
    /// </summary>
    public static class GrowthSettingsStore
    {
        public const string FileName = "growth-settings.json";

        public static GrowthSettings Load(string path, string oldSave, out string notice)
        {
            notice = null;
            if (File.Exists(path))
            {
                try
                {
                    var read = JsonUtility.FromJson<GrowthSettings>(File.ReadAllText(path));
                    if (read != null) return read.Sanitize(GrowthControls.RealKey);
                    notice = "The settings file was empty, so the settings are back to their defaults.";
                }
                catch (Exception e) { notice = "The settings could not be read (" + e.Message + "), so they are back to their defaults."; }
                string kept = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                try { File.Move(path, kept); notice += " The old file is kept: " + Path.GetFileName(kept); } catch (Exception) { }
                Debug.LogWarning(notice);
                return new GrowthSettings().Sanitize();
            }
            // Carried over once: the settings an older save kept with it (GR-16's and GR-18's comfort settings).
            if (oldSave != null && File.Exists(oldSave))
            {
                var save = GrowthSession.ReadSettings(oldSave);
                if (save != null) return GrowthSettings.FromSave(save, Path.GetFileName(oldSave));
            }
            return new GrowthSettings().Sanitize();
        }

        public static bool Save(string path, GrowthSettings settings)
        {
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(settings, true));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                return true;
            }
            catch (Exception e) { Debug.LogError("GR-23 settings save failed: " + e.Message); return false; }
        }
    }
}
