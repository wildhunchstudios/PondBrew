using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// Real session commands against isolated inactive components. Core transactions
    /// construct labelled states; these tests do not claim player input, travel or timing.
    /// </summary>
    public sealed class TutorialLifecycleTests
    {
        GameObject owner;
        ShopSession session;
        string directory, path;
        CursorLockMode cursorLock;
        bool cursorVisible;

        [SetUp]
        public void Setup()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            directory = Path.Combine(Path.GetTempPath(), "EvolutionShop-TutorialLifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "slot.json");
            owner = new GameObject("Isolated tutorial lifecycle test"); owner.SetActive(false);
            session = owner.AddComponent<ShopSession>();
            Set("Config", GameConfig.CreateDefault()); Set("Model", new GameModel(session.Config));
            Set("Cycle", new ShopDayCycle()); Set("Reports", new ShopTradeLedger());
            Set("Reputation", new ShopReputation()); Set("Layout", new ShopLayout());
            Set("Staff", new ShopStaff()); Set("Stocking", new ShopStocking()); Set("Laboratory", new ShopLaboratory());
            Set("SavePath", path);
        }

        [TearDown]
        public void Cleanup()
        {
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
            Assert.That(Path.GetFileName(directory), Does.StartWith("EvolutionShop-TutorialLifecycle-"));
            Directory.Delete(directory, true);
        }

        [TestCase("start", TutorialMode.Active)]
        [TestCase("skip", TutorialMode.Skipped)]
        [TestCase("finish", TutorialMode.Completed)]
        public void ModeCommandPublishesOnlyAfterFullSlotWrite(string command, TutorialMode expected)
        {
            if (command == "skip") { Assert.That(session.StartGuidedOpening(), Is.True); ProduceFixture(false); }
            if (command == "finish") NextPlanFixture();
            if (command == "start") session.SetPanel(ShopPanel.Welcome);
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            string before = TutorialJson(), gameplay = GameJson(); byte[] original = File.ReadAllBytes(path);
            var model = session.Model; var tutorial = session.Tutorial;
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(ChangeMode(command), Is.False);
            Assert.That(session.SaveError, Is.Not.Null.And.Not.Empty);
            Assert.That(session.Model, Is.SameAs(model)); Assert.That(session.Tutorial, Is.SameAs(tutorial));
            Assert.That(TutorialJson(), Is.EqualTo(before)); Assert.That(GameJson(), Is.EqualTo(gameplay));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            if (command == "start")
            {
                session.BackFromPanel();
                Assert.That(session.Panel, Is.EqualTo(ShopPanel.Pause), "Failed first-start write must expose keyboard save recovery.");
                Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Offered));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
                session.SetPanel(ShopPanel.Welcome);
            }
            Directory.Delete(path + ".tmp", false);
            Assert.That(ChangeMode(command), Is.True, session.SaveError);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(expected)); Assert.That(GameJson(), Is.EqualTo(gameplay));
            Assert.That(SaveStore.Read(path, session.Config, out var loadedGame, out var loaded, out var error), Is.True, error);
            Assert.That(JsonUtility.ToJson(loaded.tutorial), Is.EqualTo(TutorialJson()));
            Assert.That(JsonUtility.ToJson(loadedGame.CaptureSave()), Is.EqualTo(gameplay));
            string committed = TutorialJson(); byte[] committedBytes = File.ReadAllBytes(path);
            Assert.That(ChangeMode(command), Is.False, "Repeated command reports no new change.");
            Assert.That(TutorialJson(), Is.EqualTo(committed)); Assert.That(GameJson(), Is.EqualTo(gameplay));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(committedBytes));
        }

        [Test]
        public void ProtectedInvalidSlotRejectsGuidanceChangesWithoutTouchingBytes()
        {
            byte[] original = System.Text.Encoding.UTF8.GetBytes("deliberately invalid protected slot");
            File.WriteAllBytes(path, original); Set("AutosaveBlocked", true);
            Assert.That(session.StartGuidedOpening(), Is.False); Assert.That(session.SkipTutorial(), Is.False);
            Assert.That(session.FinishGuidedOpening(), Is.False);
            Assert.That(session.Tutorial.Mode, Is.EqualTo(TutorialMode.Offered));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [TestCase(ShopPanel.None, ShopPanel.None)]
        [TestCase(ShopPanel.Pause, ShopPanel.Pause)]
        [TestCase(ShopPanel.Journal, ShopPanel.Journal)]
        [TestCase(ShopPanel.Welcome, ShopPanel.Welcome)]
        [TestCase(ShopPanel.Incubator, ShopPanel.None)]
        [TestCase(ShopPanel.Tank, ShopPanel.None)]
        [TestCase(ShopPanel.StorageRack, ShopPanel.None)]
        [TestCase(ShopPanel.TechnicianOrders, ShopPanel.None)]
        public void HelpPausesAndReturnsOnlyToValidOrigin(ShopPanel origin, ShopPanel expected)
        {
            session.SetPanel(origin); string before = TutorialJson(), game = GameJson();
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.TutorialHelp)); Assert.That(session.Paused, Is.True);
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            session.SelectTutorialHelpLesson(TutorialLesson.Serve);
            Assert.That(session.OpenTutorialHelp(), Is.True, "Repeated help cannot replace its origin with itself.");
            session.BackFromPanel();
            Assert.That(session.Panel, Is.EqualTo(expected));
            Assert.That(TutorialJson(), Is.EqualTo(before)); Assert.That(GameJson(), Is.EqualTo(game));
        }

        [TestCase(ShopPanel.Reset)]
        [TestCase(ShopPanel.Presentation)]
        public void HelpCannotInterruptConfirmationOrPresentation(ShopPanel panel)
        {
            // Set the panel property directly: this tests tutorial admission, not the
            // rendering subsystem's distinct presentation dependencies.
            Set("Panel", panel);
            Assert.That(session.OpenTutorialHelp(), Is.False);
            Assert.That(session.Panel, Is.EqualTo(panel));
            Set("Panel", ShopPanel.None);
        }

        [Test]
        public void HelpSelectionCannotMutateLessonsOutsideHelpOrSelectInvalidPages()
        {
            session.SelectTutorialHelpLesson(TutorialLesson.Grow);
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.Orient));
            Assert.That(session.OpenTutorialHelp(), Is.True);
            session.SelectTutorialHelpLesson(TutorialLesson.Grow);
            session.SelectTutorialHelpLesson(TutorialLesson.None);
            session.SelectTutorialHelpLesson((TutorialLesson)100);
            Assert.That(session.TutorialHelpLesson, Is.EqualTo(TutorialLesson.Grow));
            Assert.That(session.Tutorial.Facts, Is.EqualTo(TutorialFacts.None));
        }

        [Test]
        public void RequestedPracticeHelpMarksDirtyAssistanceAndReportsFailedPersistence()
        {
            Assert.That(session.StartGuidedOpening(), Is.True);
            session.Tutorial.ObserveOrientation(true, true, true);
            session.Reports.BeginDay(1, 0); session.Cycle.OpenShop();
            ProduceFixture(true); SellFixture("practice-help-sale");
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Practice));
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            byte[] before = File.ReadAllBytes(path); string gameplay = GameJson();
            Directory.CreateDirectory(path + ".tmp");
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Tutorial.PracticeAssisted, Is.True);
            Assert.That(session.Paused, Is.True); Assert.That(session.SaveError, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before)); Assert.That(GameJson(), Is.EqualTo(gameplay));
            Directory.Delete(path + ".tmp", false);
            Assert.That(session.SaveNow(), Is.True, session.SaveError);
            Assert.That(SaveStore.Read(path, session.Config, out _, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.tutorial.practiceAssisted, Is.True);
        }

        [Test]
        public void ReportHelpAndPauseDoNotAcknowledgeUntilExplicitReportDismissal()
        {
            NextPlanFixture(false);
            Assert.That(session.ShowDayReport(), Is.True);
            Assert.That(session.OpenTutorialHelp(), Is.True);
            Assert.That(session.Tutorial.CaptureSave().reportAcknowledged, Is.False);
            session.CloseTutorialHelp(); Assert.That(session.Panel, Is.EqualTo(ShopPanel.DayReport));
            session.SetPanel(ShopPanel.Pause); session.SetPanel(ShopPanel.None);
            Assert.That(session.Tutorial.CaptureSave().reportAcknowledged, Is.False);
            Assert.That(session.ShowDayReport(), Is.True); session.BackFromPanel();
            Assert.That(session.Tutorial.CaptureSave().reportAcknowledged, Is.True);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.NextPlan));
        }

        [TestCase(ShopPanel.None)]
        [TestCase(ShopPanel.Pause)]
        [TestCase(ShopPanel.TutorialHelp)]
        [TestCase(ShopPanel.Incubator)]
        public void ManualPreparationCommandsRequirePhysicalIncubatorAndCannotGrantLearning(ShopPanel panel)
        {
            Assert.That(session.StartGuidedOpening(), Is.True); session.Model.Collect(CultureKind.Bloom);
            session.SetPanel(panel); string game = GameJson(), tutorial = TutorialJson();
            Assert.That(session.DoseCulture(CultureKind.Bloom), Is.False);
            Assert.That(session.ReturnCultureDose(CultureKind.Bloom), Is.False);
            Assert.That(session.ClearCultureDoses(), Is.False); Assert.That(session.ReleaseCulture(CultureKind.Bloom), Is.False);
            Assert.That(session.PrepareKnownRecipe("pondlet"), Is.False); Assert.That(session.BeginManualIncubation(), Is.False);
            Assert.That(GameJson(), Is.EqualTo(game)); Assert.That(TutorialJson(), Is.EqualTo(tutorial));
        }

        void NextPlanFixture(bool acknowledge = true)
        {
            Assert.That(session.StartGuidedOpening(), Is.True);
            session.Tutorial.ObserveOrientation(true, true, true);
            Assert.That(session.Reports.BeginDay(1, 0), Is.True); Assert.That(session.Cycle.OpenShop(), Is.True);
            ProduceFixture(true); SellFixture("first-fixture-sale");
            ProduceFixture(true); SellFixture("second-fixture-sale");
            Assert.That(session.Cycle.RequestClose(), Is.True); session.Cycle.Tick(3);
            session.Reports.FinishDay(0);
            if (acknowledge) Assert.That(session.Tutorial.ObserveReportAcknowledged(1), Is.True);
        }
        void ProduceFixture(bool ready)
        {
            Assert.That(session.Model.Collect(CultureKind.Bloom).Success, Is.True);
            Assert.That(session.Model.Collect(CultureKind.Flow).Success, Is.True);
            session.Model.Dose(CultureKind.Bloom); session.Model.Dose(CultureKind.Flow);
            var start = session.Model.BeginIncubation(); Assert.That(start.Success, Is.True);
            Assert.That(session.Tutorial.ObserveStarted(session.Model, start, TutorialActionOrigin.Player), Is.True);
            if (!ready) return;
            session.Model.Tick(10); session.Tutorial.ObserveReady(session.Model);
            var display = session.Model.TransferToTank("tank-a"); Assert.That(display.Success, Is.True);
            Assert.That(session.Tutorial.ObserveDisplayed(session.Model, display, TutorialActionOrigin.Player), Is.True);
        }
        void SellFixture(string id)
        {
            var sale = session.Model.SellFromTank("tank-a"); Assert.That(sale.Success, Is.True);
            Assert.That(session.Tutorial.ObserveSale(session.Model, sale, TutorialActionOrigin.Player, session.Cycle.DayNumber), Is.True);
            Assert.That(session.Reports.BeginVisit(id), Is.True);
            Assert.That(session.Reports.ResolveVisit(id, CustomerOutcome.Sold, 6), Is.True);
        }
        bool ChangeMode(string command) => command == "start" ? session.StartGuidedOpening() :
            command == "skip" ? session.SkipTutorial() : session.FinishGuidedOpening();
        string TutorialJson() => JsonUtility.ToJson(session.Tutorial.CaptureSave());
        string GameJson() => JsonUtility.ToJson(session.Model.CaptureSave());
        void Set(string property, object value) => typeof(ShopSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)
            .SetValue(session, value);
    }
}
