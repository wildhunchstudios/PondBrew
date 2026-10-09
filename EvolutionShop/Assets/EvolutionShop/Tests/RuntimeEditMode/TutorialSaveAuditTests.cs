using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    // Isolated TUT-09 corruption fixtures, not player input or runtime evidence.
    public sealed class TutorialSaveAuditTests
    {
        const TutorialActionOrigin Player = TutorialActionOrigin.Player;
        const string Prefix = "EvolutionShop-TutorialSaveAudit-";
        string directory;

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory), Does.StartWith(Prefix));
            Directory.Delete(directory, true);
        }

        [TestCase("growing")]
        [TestCase("ready-incubator")]
        [TestCase("display")]
        [TestCase("rack")]
        [TestCase("rack-after-display")]
        public void LegitimateLiveCandidateLocationsPreserveReadinessAndManualFacts(string location)
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "audit-rack" };
            var game = new GameModel(config); var tutorial = Active();
            StartBatch(game, tutorial);
            if (location != "growing") Mature(game, tutorial);
            Place(game, tutorial, location);
            string before = JsonUtility.ToJson(tutorial.CaptureSave());
            Assert.That(ShopTutorial.TryRestore(tutorial.CaptureSave(), game, 1, out var restored), Is.True);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(before));
            Assert.That(restored.CaptureSave().first.ready, Is.EqualTo(location != "growing"));
            Assert.That(restored.CaptureSave().first.manuallyDisplayed,
                Is.EqualTo(location == "display" || location == "rack-after-display"));
        }

        [TestCase("display", false)]
        [TestCase("rack", false)]
        [TestCase("display", true)]
        [TestCase("rack", true)]
        public void ResidentOutsideIncubatorCannotRestoreAsAnUnreadyTutorialCandidate(string location, bool practice)
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "audit-rack" };
            var game = new GameModel(config); var tutorial = Active();
            if (practice)
            {
                StartBatch(game, tutorial); Mature(game, tutorial); Place(game, tutorial, "display");
                var sale = game.SellFromTank("tank-a"); Assert.That(sale.Success, Is.True);
                Assert.That(tutorial.ObserveSale(game, sale, Player, 1), Is.True);
            }
            StartBatch(game, tutorial); Mature(game, tutorial); Place(game, tutorial, location);
            var corrupt = tutorial.CaptureSave();
            var chain = practice ? corrupt.practice : corrupt.first;
            chain.ready = false; chain.manuallyDisplayed = false;
            Assert.That(ShopTutorial.TryRestore(corrupt, game, 1, out var restored), Is.False,
                "A tank or rack contains a mature specimen. An unready receipt would leave Grow selected while its candidate is no longer in the incubator.");
            Assert.That(restored, Is.Null);
        }

        [TestCase("write")]
        [TestCase("read")]
        public void MalformedDisplayedReadinessIsRejectedWithoutReplacingOriginalBytes(string operation)
        {
            var game = new GameModel(GameConfig.CreateDefault()); var tutorial = Active();
            StartBatch(game, tutorial); Mature(game, tutorial); Place(game, tutorial, "display");
            var cycle = new ShopDayCycle();
            var slot = new SlotData { game = game.CaptureSave(), cycle = cycle.CaptureSave(), tutorial = tutorial.CaptureSave() };
            string path = Path.Combine(directory, "slot.json");
            Assert.That(SaveStore.Write(path, slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(path);
            slot.tutorial.first.ready = false; slot.tutorial.first.manuallyDisplayed = false;
            if (operation == "write")
            {
                Assert.That(SaveStore.Write(path, slot, out error), Is.False);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            }
            else
            {
                File.WriteAllText(path, JsonUtility.ToJson(slot));
                byte[] corrupt = File.ReadAllBytes(path);
                Assert.That(SaveStore.Read(path, GameConfig.CreateDefault(), out var loadedGame, out var loadedSlot, out error), Is.False);
                Assert.That(loadedGame, Is.Null); Assert.That(loadedSlot, Is.Null);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(corrupt));
            }
        }

        static ShopTutorial Active()
        {
            var tutorial = new ShopTutorial(); Assert.That(tutorial.Start(), Is.True);
            tutorial.ObserveOrientation(true, true, true); return tutorial;
        }

        static void StartBatch(GameModel game, ShopTutorial tutorial)
        {
            foreach (var kind in new[] { CultureKind.Bloom, CultureKind.Flow })
            {
                var collected = game.Collect(kind); Assert.That(collected.Success, Is.True);
                tutorial.ObserveCollection(game, kind, collected, Player);
                Assert.That(game.Dose(kind).Success, Is.True);
            }
            tutorial.ObservePrepared(game, "pondlet", Player);
            var started = game.BeginIncubation(); Assert.That(started.Success, Is.True);
            Assert.That(tutorial.ObserveStarted(game, started, Player), Is.True);
        }

        static void Mature(GameModel game, ShopTutorial tutorial)
        {
            game.Tick(10); Assert.That(tutorial.ObserveReady(game), Is.True);
        }

        static void Place(GameModel game, ShopTutorial tutorial, string location)
        {
            if (location == "display" || location == "rack-after-display")
            {
                var displayed = game.TransferToTank("tank-a"); Assert.That(displayed.Success, Is.True);
                Assert.That(tutorial.ObserveDisplayed(game, displayed, Player), Is.True);
                if (location == "rack-after-display")
                    Assert.That(game.ReturnTankToStorage("tank-a", "audit-rack", displayed.SpecimenId).Success, Is.True);
            }
            if (location == "rack") Assert.That(game.StoreReadyOutput("audit-rack").Success, Is.True);
            tutorial.Reconcile(game);
        }
    }
}
