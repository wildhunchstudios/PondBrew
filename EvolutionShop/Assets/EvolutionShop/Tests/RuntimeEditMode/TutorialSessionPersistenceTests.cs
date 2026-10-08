using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    // Exercises the shared full-slot boundary used by manual/autosave and all staff
    // transactions, without invoking Awake or touching the player's normal save.
    public sealed class TutorialSessionPersistenceTests
    {
        GameObject owner;
        ShopSession session;
        string directory;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "EvolutionShop-TutorialSession-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            owner = new GameObject("Isolated tutorial slot test");
            owner.SetActive(false);
            session = owner.AddComponent<ShopSession>();
            Set("Config", GameConfig.CreateDefault());
            Set("Model", new GameModel(session.Config));
            Set("Cycle", new ShopDayCycle());
            Set("Reports", new ShopTradeLedger());
            Set("Reputation", new ShopReputation());
            Set("Layout", new ShopLayout());
            Set("Staff", new ShopStaff());
            Set("Stocking", new ShopStocking());
            Set("Laboratory", new ShopLaboratory());
        }

        [TearDown]
        public void Cleanup()
        {
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
            Assert.That(Path.GetFileName(directory), Does.StartWith("EvolutionShop-TutorialSession-"));
            Directory.Delete(directory, true);
        }

        [TestCase(TutorialMode.Offered)]
        [TestCase(TutorialMode.Active)]
        [TestCase(TutorialMode.Skipped)]
        [TestCase(TutorialMode.LegacyUnguided)]
        public void SharedFullSlotPreservesTutorialDuringDetachedStaffCapture(TutorialMode mode)
        {
            var tutorial = mode == TutorialMode.LegacyUnguided ? ShopTutorial.LegacyUnguided() : new ShopTutorial();
            if (mode == TutorialMode.Active || mode == TutorialMode.Skipped)
            {
                Assert.That(tutorial.Start(), Is.True);
                tutorial.ObserveOrientation(true, true, true);
            }
            if (mode == TutorialMode.Skipped) Assert.That(tutorial.Skip(), Is.True);
            Set("Tutorial", tutorial);
            string expected = JsonUtility.ToJson(tutorial.CaptureSave());
            // Staff transactions capture a detached model; the tutorial must not fall
            // back to SlotData's fresh Offered default at this shared boundary.
            Assert.That(GameModel.TryRestore(session.Config, session.Model.CaptureSave(), out var detached, out _), Is.True);
            var slot = Capture(detached);
            Assert.That(JsonUtility.ToJson(slot.tutorial), Is.EqualTo(expected));
            Assert.That(SaveStore.Write(Path.Combine(directory, "slot.json"), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(Path.Combine(directory, "slot.json"), session.Config, out _, out var loaded, out error), Is.True, error);
            Assert.That(JsonUtility.ToJson(loaded.tutorial), Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(tutorial.CaptureSave()), Is.EqualTo(expected), "Capturing and writing a staff slot cannot mutate tutorial authority.");
        }

        [Test]
        public void FailedStaffSlotWriteCannotChangeLiveTutorialOrOriginalBytes()
        {
            session.Tutorial.Start();
            session.Tutorial.ObserveOrientation(true, false, true);
            var slot = Capture(session.Model);
            string path = Path.Combine(directory, "slot.json");
            Assert.That(SaveStore.Write(path, slot, out var error), Is.True, error);
            byte[] bytes = File.ReadAllBytes(path);
            string live = JsonUtility.ToJson(session.Tutorial.CaptureSave());
            // An unusable .tmp destination gives a deterministic filesystem failure.
            Directory.CreateDirectory(path + ".tmp");
            slot.tutorial.mode = TutorialMode.Skipped;
            Assert.That(SaveStore.Write(path, slot, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(JsonUtility.ToJson(session.Tutorial.CaptureSave()), Is.EqualTo(live));
        }

        [Test]
        public void RestoreBoundaryPreservesExactModeAndFacts()
        {
            var tutorial = new ShopTutorial();
            tutorial.Start();
            tutorial.ObserveOrientation(true, true, true);
            TutorialSave save = tutorial.CaptureSave();
            typeof(ShopSession).GetMethod("RestoreTutorial", PrivateInstance).Invoke(session, new object[] { save });
            Assert.That(JsonUtility.ToJson(session.Tutorial.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(save)));
            save.facts = 0;
            Assert.That(session.Tutorial.Facts, Is.Not.EqualTo((TutorialFacts)0), "Restored state must not alias its input DTO.");
        }

        SlotData Capture(GameModel model) => (SlotData)typeof(ShopSession).GetMethod("CaptureStaffSlot", PrivateInstance)
            .Invoke(session, new object[] { model, session.Staff, session.Stocking, session.Reports, session.Laboratory });

        void Set(string property, object value) => typeof(ShopSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)
            .SetValue(session, value);
    }
}
