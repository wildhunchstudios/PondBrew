using System.IO;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-23: the controls and the settings file. The game's words name the keys as the player bound them (with the
    /// usual keys they are as written, and two swapped keys never swap back); only the Input System's own keys are keys; and
    /// the whole game's settings file: kept and read back, an unreadable one kept aside for the defaults, and an older save's
    /// settings carried over when there is no file yet.</summary>
    public sealed class GrowthControlsTests
    {
        GrowthSettings before;
        string dir;

        [SetUp] public void Keep() { before = GrowthControls.Settings; dir = Path.Combine(Application.temporaryCachePath, "gr23-tests-" + System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); }
        [TearDown] public void Restore() { GrowthControls.Settings = before; try { Directory.Delete(dir, true); } catch (IOException) { } }

        [Test]
        public void TheWordsNameTheKeysAsBound()
        {
            const string words = "Open the incubator (E): Tab: journal · Hold the mouse on the counter · Era 4 · Emberpuff";
            GrowthControls.Settings = new GrowthSettings();
            Assert.That(GrowthControls.Keyed(words), Is.EqualTo(words), "the usual keys: as written");
            var g = new GrowthSettings(); g.Bind(GrowthAction.Interact, "Key/Q"); GrowthControls.Settings = g;
            Assert.That(GrowthControls.Keyed(words), Is.EqualTo("Open the incubator (Q): Tab: journal · Hold the mouse on the counter · Era 4 · Emberpuff"), "E reads Q; words with an E in them stay");
            g = new GrowthSettings(); g.Bind(GrowthAction.Journal, "Key/E"); GrowthControls.Settings = g;
            Assert.That(GrowthControls.Keyed("E opens it · Tab: journal"), Is.EqualTo("Tab opens it · E: journal"), "two keys swapped are named so, never swapped back");
            g = new GrowthSettings(); g.Bind(GrowthAction.Use, "Key/Space"); GrowthControls.Settings = g;
            Assert.That(GrowthControls.Keyed("Hold the mouse on a vial"), Is.EqualTo("Hold Space on a vial"));
            g = new GrowthSettings(); g.Bind(GrowthAction.Use, "Mouse/Right"); GrowthControls.Settings = g;
            Assert.That(GrowthControls.Keyed("hold the mouse on a grower"), Is.EqualTo("hold the right mouse button on a grower"));
            Assert.That(GrowthControls.Keyed(null), Is.Null);
        }

        [Test]
        public void OnlyTheInputSystemsKeysAreKeys()
        {
            Assert.That(GrowthControls.RealKey("Q") && GrowthControls.RealKey("Space") && GrowthControls.RealKey("Digit7") && GrowthControls.RealKey("LeftShift"), Is.True);
            Assert.That(GrowthControls.RealKey("NoSuchKey") || GrowthControls.RealKey("5") || GrowthControls.RealKey("") || GrowthControls.RealKey("None"), Is.False, "not keys: a name it does not know, a number, nothing, None");
        }

        [Test]
        public void TheSettingsFileIsKeptAndReadBack()
        {
            string path = Path.Combine(dir, GrowthSettingsStore.FileName);
            var g = new GrowthSettings { music = 35, fieldOfView = 80, invertY = true, uiScale = 120 };
            g.Bind(GrowthAction.Slap, "Mouse/Middle");
            Assert.That(GrowthSettingsStore.Save(path, g), Is.True);
            var back = GrowthSettingsStore.Load(path, null, out string notice);
            Assert.That(notice, Is.Null);
            Assert.That(back.music == 35 && back.fieldOfView == 80 && back.invertY && back.uiScale == 120 && back.InputOf(GrowthAction.Slap) == "Mouse/Middle", Is.True, "the same settings");
        }

        [Test]
        public void AnUnreadableFileIsKeptAsideForTheDefaults()
        {
            string path = Path.Combine(dir, GrowthSettingsStore.FileName);
            File.WriteAllText(path, "{ not json at all");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var g = GrowthSettingsStore.Load(path, null, out string notice);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.That(notice, Does.Contain("could not be read"));
            Assert.That(g.music == 70 && g.preset == 2, Is.True, "the defaults");
            Assert.That(File.Exists(path), Is.False, "the unreadable file moved aside");
            Assert.That(Directory.GetFiles(dir, "growth-settings.unreadable-*.json").Length, Is.EqualTo(1), "and kept");
        }

        [Test]
        public void AnOlderSavesSettingsAreCarriedOverWhenThereIsNoFile()
        {
            string old = Path.Combine(dir, "growth-gr02.json");
            File.WriteAllText(old, JsonUtility.ToJson(new GrowthSave { soundLevel = 2, humLevel = 1, peekOff = true, slapKey = "G" }));
            var g = GrowthSettingsStore.Load(Path.Combine(dir, GrowthSettingsStore.FileName), old, out string notice);
            Assert.That(notice, Is.Null);
            Assert.That(g.sounds == 0 && g.hum == 50 && g.peekOff && g.InputOf(GrowthAction.Slap) == "Key/G" && g.carriedFrom == "growth-gr02.json", Is.True, "carried over");
            var none = GrowthSettingsStore.Load(Path.Combine(dir, "nothing.json"), Path.Combine(dir, "no-save.json"), out _);
            Assert.That(none.sounds == 100 && none.carriedFrom == "", Is.True, "neither: the defaults");
        }
    }
}
