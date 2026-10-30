using System;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-23: the whole game's settings (the user's request 2026-10-30): the defaults, the graphics presets, every value
    /// put back in its range, every action on a key or mouse button of its own (swapping when taken, Esc and F8 refused), the
    /// keys' names, a copy, and an older save's settings carried over once.</summary>
    public sealed class GrowthSettingsTests
    {
        [Test]
        public void TheDefaultsAreHighQualityAndTheGameAsItWas()
        {
            var g = new GrowthSettings().Sanitize();
            Assert.That(g.preset, Is.EqualTo(2), "High");
            Assert.That(g.shadows == 2 && g.bloom && g.reflections && g.antiAliasing == 2 && g.renderScale == 100 && g.creatureDetail == 1, Is.True, "High's choices");
            Assert.That(g.fieldOfView, Is.EqualTo(68), "the view as before");
            Assert.That(g.windowMode == 1 && g.width == 1280 && g.height == 720 && !g.displayChosen, Is.True, "the window as the game opens, not changed until chosen");
            Assert.That(g.frameLimit == 120 && !g.vSync && g.uiScale == 100, Is.True, "120 frames a second at most, as before");
            Assert.That(g.master == 100 && g.music == 70 && g.sounds == 100 && g.hum == 100 && g.humour == 100 && g.muteInBackground, Is.True, "the sounds full, the music at 70, muted behind another window");
            Assert.That(g.sensitivity == 1 && !g.invertY && !g.useToggle, Is.True, "the mouse as before, the use button held");
            Assert.That(!g.reducedMotion && !g.peekOff && g.gags == 0 && !g.captionsOff, Is.True, "accessibility as before");
            foreach (GrowthAction a in Enum.GetValues(typeof(GrowthAction))) Assert.That(g.InputOf(a), Is.EqualTo(GrowthSettings.DefaultInputs[a]), a + " on its usual key");
            Assert.That(g.InputOf(GrowthAction.Use), Is.EqualTo("Mouse/Left"));
        }

        [Test]
        public void APresetSetsEveryGraphicsChoiceButTheViewAndBrightness()
        {
            var g = new GrowthSettings { fieldOfView = 80, brightness = 2 };
            g.ApplyPreset(0);
            Assert.That(g.shadows == 0 && !g.bloom && !g.reflections && g.antiAliasing == 0 && g.renderScale == 75 && g.creatureDetail == 0, Is.True, "Low");
            Assert.That(g.fieldOfView == 80 && g.brightness == 2, Is.True, "the view and brightness are the player's own");
            Assert.That(g.MatchingPreset(), Is.EqualTo(0));
            g.ApplyPreset(1);
            Assert.That(g.shadows == 1 && g.bloom && !g.reflections && g.antiAliasing == 1 && g.renderScale == 100 && g.creatureDetail == 1 && g.MatchingPreset() == 1, Is.True, "Medium");
            g.shadows = 2;
            Assert.That(g.MatchingPreset(), Is.EqualTo(3), "one choice changed: Custom");
            Assert.That(g.Sanitize().preset, Is.EqualTo(3));
        }

        [Test]
        public void EveryValueIsPutBackInItsRange()
        {
            var g = new GrowthSettings
            {
                preset = 9, shadows = -1, antiAliasing = 7, renderScale = 12, fieldOfView = 200, brightness = -40, creatureDetail = 5, windowMode = 3,
                width = 10, height = 99999, frameLimit = 77, uiScale = 300, master = -5, music = 400, sounds = 101, hum = -1, humour = 1000,
                sensitivity = float.NaN, gags = 9, carriedFrom = null, bindings = null,
            }.Sanitize();
            Assert.That(g.shadows, Is.EqualTo(0)); Assert.That(g.antiAliasing, Is.EqualTo(2)); Assert.That(g.renderScale, Is.EqualTo(50));
            Assert.That(g.fieldOfView, Is.EqualTo(90)); Assert.That(g.brightness, Is.EqualTo(-5)); Assert.That(g.creatureDetail, Is.EqualTo(1));
            Assert.That(g.windowMode, Is.EqualTo(2)); Assert.That(g.width == 1280 && g.height == 720, Is.True, "an impossible size: the usual window");
            Assert.That(g.frameLimit, Is.EqualTo(120), "a limit not offered: 120"); Assert.That(g.uiScale, Is.EqualTo(130));
            Assert.That(new[] { g.master, g.music, g.sounds, g.hum, g.humour }, Is.EqualTo(new[] { 0, 100, 100, 0, 100 }));
            Assert.That(g.sensitivity, Is.EqualTo(1f)); Assert.That(g.gags, Is.EqualTo(2)); Assert.That(g.carriedFrom, Is.EqualTo(""));
            Assert.That(g.bindings.Count, Is.EqualTo(Enum.GetValues(typeof(GrowthAction)).Length), "no bindings: the defaults");
            Assert.That(new GrowthSettings { renderScale = 83, uiScale = 104, sensitivity = 1.33f }.Sanitize().renderScale, Is.EqualTo(85), "to the nearest 5%");
            Assert.That(new GrowthSettings { uiScale = 104 }.Sanitize().uiScale, Is.EqualTo(100), "to the nearest 10%");
            Assert.That(new GrowthSettings { sensitivity = 1.33f }.Sanitize().sensitivity, Is.EqualTo(1.35f).Within(1e-5), "to the nearest 0.05");
        }

        [Test]
        public void ActionsSwapWhenAKeyIsTakenAndEscAndF8AreKept()
        {
            var g = new GrowthSettings();
            var r = g.Bind(GrowthAction.Interact, "Key/Q");
            Assert.That(r.done && g.InputOf(GrowthAction.Interact) == "Key/Q" && r.note == "Interact is now on Q", Is.True, r.note);
            r = g.Bind(GrowthAction.Journal, "Key/Q");
            Assert.That(r.done && g.InputOf(GrowthAction.Journal) == "Key/Q" && g.InputOf(GrowthAction.Interact) == "Key/Tab", Is.True, "the two swap: " + r.note);
            Assert.That(r.note, Does.Contain("moved to Tab"));
            r = g.Bind(GrowthAction.Slap, "Mouse/Right");
            Assert.That(r.done && g.InputOf(GrowthAction.Slap) == "Mouse/Right", Is.True, "any mouse button");
            r = g.Bind(GrowthAction.Skip, "Key/Escape");
            Assert.That(!r.done && g.InputOf(GrowthAction.Skip) == "Key/Backspace" && r.note.Contains("pausing"), Is.True, "Esc is kept: " + r.note);
            r = g.Bind(GrowthAction.Skip, "Key/F8");
            Assert.That(!r.done && r.note.Contains("development"), Is.True, "F8 is kept: " + r.note);
            Assert.That(g.Bind(GrowthAction.Skip, "Mouse/Sideways").done, Is.False, "not a mouse button");
            Assert.That(g.Bind(GrowthAction.Skip, "Key/").done, Is.False);
            var inputs = g.bindings.Select(b => b.input).ToList();
            Assert.That(inputs.Distinct().Count(), Is.EqualTo(inputs.Count), "never two actions on one input");
            g.ResetBindings();
            Assert.That(g.InputOf(GrowthAction.Interact) == "Key/E" && g.InputOf(GrowthAction.Journal) == "Key/Tab" && g.InputOf(GrowthAction.Slap) == "Key/F", Is.True, "back to the defaults");
        }

        [Test]
        public void AFileWithBrokenBindingsStillHasEveryActionOnAKeyOfItsOwn()
        {
            var g = new GrowthSettings();
            g.bindings.Clear();
            g.bindings.Add(new GrowthBinding { action = "Interact", input = "Key/W" });   // takes Forward's key
            g.bindings.Add(new GrowthBinding { action = "Journal", input = "Key/W" });    // the same again
            g.bindings.Add(new GrowthBinding { action = "Slap", input = "Key/Escape" });  // reserved
            g.bindings.Add(new GrowthBinding { action = "Skip", input = "Key/NoSuchKey" }); // not a key
            g.bindings.Add(new GrowthBinding { action = "Dance", input = "Key/K" });      // not an action
            g.bindings.Add(null);
            g.Sanitize(name => name != "NoSuchKey");
            var inputs = Enum.GetValues(typeof(GrowthAction)).Cast<GrowthAction>().Select(a => g.InputOf(a)).ToList();
            Assert.That(inputs.Distinct().Count(), Is.EqualTo(inputs.Count), "each action on an input of its own: " + string.Join(", ", inputs));
            Assert.That(g.InputOf(GrowthAction.Interact), Is.EqualTo("Key/W"), "the first claim kept");
            Assert.That(g.InputOf(GrowthAction.Journal), Is.EqualTo("Key/Tab"), "a second claim: its own default");
            Assert.That(g.InputOf(GrowthAction.Slap), Is.EqualTo("Key/F"), "a reserved key: its default");
            Assert.That(g.InputOf(GrowthAction.Skip), Is.EqualTo("Key/Backspace"), "an unknown key: its default");
            Assert.That(g.InputOf(GrowthAction.Forward), Is.Not.EqualTo("Key/W"), "Forward, its key taken, gets a free one");
            Assert.That(inputs.All(GrowthSettings.WellFormed) && inputs.All(i => !GrowthSettings.Reserved.Contains(i)), Is.True);
            Assert.That(g.bindings.Count, Is.EqualTo(inputs.Count), "unknown actions dropped");
        }

        [Test]
        public void KeysHaveNamesForPeople()
        {
            Assert.That(GrowthSettings.InputName("Key/W"), Is.EqualTo("W"));
            Assert.That(GrowthSettings.InputName("Key/Digit7"), Is.EqualTo("7"));
            Assert.That(GrowthSettings.InputName("Key/Numpad3"), Is.EqualTo("Numpad 3"));
            Assert.That(GrowthSettings.InputName("Key/LeftShift"), Is.EqualTo("Left Shift"));
            Assert.That(GrowthSettings.InputName("Mouse/Left"), Is.EqualTo("Left mouse button"));
            Assert.That(GrowthSettings.InputName("Mouse/Back"), Is.EqualTo("Back mouse button"));
            Assert.That(GrowthSettings.WellFormed("Key/Space") && GrowthSettings.WellFormed("Mouse/Middle") && !GrowthSettings.WellFormed("Key/a b") && !GrowthSettings.WellFormed("Pad/A"), Is.True);
        }

        [Test]
        public void AnOlderSavesSettingsAreCarriedOverOnce()
        {
            var save = new GrowthSave { soundLevel = 1, humLevel = 2, humourLevel = 0, gagLevel = 1, peekOff = true, reducedMotion = true, sensitivity = 1.75f, slapKey = "G" };
            var g = GrowthSettings.FromSave(save, "growth-gr02.json");
            Assert.That(g.sounds == 50 && g.hum == 0 && g.humour == 100, Is.True, "full, half or off as 100, 50 or 0");
            Assert.That(g.gags == 1 && g.peekOff && g.reducedMotion && Math.Abs(g.sensitivity - 1.75f) < 1e-5, Is.True);
            Assert.That(g.InputOf(GrowthAction.Slap), Is.EqualTo("Key/G"), "the slap's key");
            Assert.That(g.carriedFrom, Is.EqualTo("growth-gr02.json"));
            Assert.That(g.music == 70 && g.preset == 2 && g.fieldOfView == 68, Is.True, "everything else as new");
            var plain = GrowthSettings.FromSave(new GrowthSave(), "x.json");
            Assert.That(plain.sounds == 100 && plain.InputOf(GrowthAction.Slap) == "Key/F" && plain.sensitivity == 1, Is.True, "an older save with none: the defaults");
            Assert.That(GrowthSettings.FromSave(null, "x").carriedFrom, Is.EqualTo(""), "no save: the defaults");
        }

        [Test]
        public void ACopyIsItsOwn()
        {
            var g = new GrowthSettings { music = 40 };
            var c = g.Copy();
            c.music = 90; c.Bind(GrowthAction.Interact, "Key/Q");
            Assert.That(g.music == 40 && g.InputOf(GrowthAction.Interact) == "Key/E", Is.True, "changing the copy leaves the original");
        }
    }
}
