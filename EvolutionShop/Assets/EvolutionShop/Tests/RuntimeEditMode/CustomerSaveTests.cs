using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class CustomerSaveTests
    {
        private const string DirectoryPrefix = "EvolutionShop-CustomerSaveTests-";
        private static readonly string[] Tanks = {"tank-a", "tank-b"};
        private string directory;
        private GameConfig config;
        private GameModel model;

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
        }

        [TearDown]
        public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            string target = Path.GetFullPath(directory);
            string parent = Path.GetFullPath(Path.GetDirectoryName(target)).TrimEnd(Path.DirectorySeparatorChar);
            string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            Assert.That(parent, Is.EqualTo(temporary).IgnoreCase, "Refusing cleanup outside the test temp directory.");
            Assert.That(Path.GetFileName(target).StartsWith(DirectoryPrefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(target, true);
        }

        [TestCase(CustomerPhase.ToCounter, ShopDayPhase.Open)]
        [TestCase(CustomerPhase.Queued, ShopDayPhase.Open)]
        [TestCase(CustomerPhase.ToCounter, ShopDayPhase.Closing)]
        [TestCase(CustomerPhase.Queued, ShopDayPhase.Closing)]
        public void FileRestoreCancelsUnpaidVisitOwnershipAndPreservesStockMoneyAndClock(
            CustomerPhase visitPhase, ShopDayPhase dayPhase)
        {
            StockShopWithPriorIncome();
            var cycle = RunningCycle();
            var visit = AcceptedVisit();
            if (visitPhase == CustomerPhase.Queued) visit.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(visitPhase));
            if (dayPhase == ShopDayPhase.Closing)
            {
                Assert.That(cycle.RequestClose(), Is.True);
                cycle.Tick(0.75f);
                visit.Advance(0.75f, false, ShopDayPhase.Closing);
            }

            string orderedSpecimen = visit.SpecimenId;
            string otherSpecimen = model.GetTank("tank-b").Value.Id;
            string expectedGame = JsonUtility.ToJson(model.CaptureSave());
            string expectedClock = JsonUtility.ToJson(cycle.CaptureSave());
            int expectedCurrency = model.Currency;
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(visit.Id));

            string path = Path.Combine(directory, "reserved-visit.json");
            WriteSlot(path, cycle);
            byte[] originalBytes = File.ReadAllBytes(path);
            Assert.That(SaveStore.Read(path, config, out var restored, out var slot, out var error), Is.True, error);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalBytes), "Read must not rewrite the saved slot.");
            Assert.That(slot.formatVersion, Is.EqualTo(2)); Assert.That(slot.game.version, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(expectedGame));
            Assert.That(JsonUtility.ToJson(slot.cycle), Is.EqualTo(expectedClock));
            Assert.That(slot.sensitivity, Is.EqualTo(1.25f)); Assert.That(slot.volume, Is.EqualTo(0.42f));
            Assert.That(restored.GetReservation("tank-a"), Is.Null);
            Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(orderedSpecimen));
            Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(otherSpecimen));
            Assert.That(restored.Currency, Is.EqualTo(expectedCurrency));

            // The old visit belongs to the old in-memory model. Its identity is not a restored order.
            Assert.That(restored.SellReservedTank("tank-a", visit.Id, orderedSpecimen).Success, Is.False);
            Assert.That(restored.Currency, Is.EqualTo(expectedCurrency));
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(orderedSpecimen));
            Assert.That(model.GetReservation("tank-a").Value.CustomerId, Is.EqualTo(visit.Id),
                "Saving alone must not cancel the active visit in the still-running model.");

            Assert.That(ShopDayCycle.TryRestore(slot.cycle, out var restoredCycle), Is.True);
            if (restoredCycle.Phase == ShopDayPhase.Closing)
            {
                restoredCycle.Tick(restoredCycle.SecondsRemaining);
                Assert.That(restoredCycle.Phase, Is.EqualTo(ShopDayPhase.Night));
                Assert.That(restoredCycle.OpenShop(), Is.True);
            }
            var replacement = new CustomerVisit(restored, Tanks);
            Queue(replacement, restoredCycle.Phase);
            Assert.That(restored.SellReservedTank("tank-a", visit.Id, orderedSpecimen).Success, Is.False);
            Ok(replacement.Serve(restoredCycle.Phase));
            Assert.That(restored.Currency, Is.EqualTo(expectedCurrency + 6));
            Assert.That(restored.GetTank("tank-a"), Is.Null);
            Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(otherSpecimen));
            Assert.That(replacement.Serve(restoredCycle.Phase).Success, Is.False);
            Assert.That(restored.Currency, Is.EqualTo(expectedCurrency + 6));
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void FileRestoreKeepsPaidSaleCommittedAndCannotReplayItsOldOrder(ShopDayPhase dayPhase)
        {
            StockShopWithPriorIncome();
            var cycle = RunningCycle();
            var visit = AcceptedVisit();
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            if (dayPhase == ShopDayPhase.Closing)
            {
                Assert.That(cycle.RequestClose(), Is.True);
                cycle.Tick(0.5f);
            }
            string soldSpecimen = visit.SpecimenId;
            string otherSpecimen = model.GetTank("tank-b").Value.Id;
            Ok(visit.Serve(cycle.Phase));
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Paid));
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.Sold));
            Assert.That(model.Currency, Is.EqualTo(16));
            string expectedClock = JsonUtility.ToJson(cycle.CaptureSave());

            string path = Path.Combine(directory, "paid-visit.json");
            WriteSlot(path, cycle);
            Assert.That(SaveStore.Read(path, config, out var restored, out var slot, out var error), Is.True, error);
            Assert.That(restored.Currency, Is.EqualTo(16));
            Assert.That(restored.GetTank("tank-a"), Is.Null);
            Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(otherSpecimen));
            Assert.That(restored.IsDiscovered("pondlet"), Is.True);
            Assert.That(restored.GetReservation("tank-a"), Is.Null);
            Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(JsonUtility.ToJson(slot.cycle), Is.EqualTo(expectedClock));
            Assert.That(restored.SellReservedTank("tank-a", visit.Id, soldSpecimen).Success, Is.False);
            Assert.That(restored.ReserveTank("tank-a", "replayed-visit").Success, Is.False);
            Assert.That(restored.Currency, Is.EqualTo(16));
            Assert.That(restored.GetTank("tank-a"), Is.Null);

            // Re-saving the loaded slot must not turn a completed sale into an unpaid order.
            Assert.That(SaveStore.Write(path, slot, out error), Is.True, error);
            Assert.That(SaveStore.Read(path, config, out var again, out var repeated, out error), Is.True, error);
            Assert.That(again.Currency, Is.EqualTo(16)); Assert.That(again.GetTank("tank-a"), Is.Null);
            Assert.That(again.GetTank("tank-b").Value.Id, Is.EqualTo(otherSpecimen));
            Assert.That(again.GetReservation("tank-a"), Is.Null);
            Assert.That(JsonUtility.ToJson(repeated.cycle), Is.EqualTo(expectedClock));
        }

        private void WriteSlot(string path, ShopDayCycle cycle)
        {
            var slot = new SlotData
            {
                formatVersion = 2, game = model.CaptureSave(), cycle = cycle.CaptureSave(),
                sensitivity = 1.25f, volume = 0.42f
            };
            Assert.That(SaveStore.Write(path, slot, out var error), Is.True, error);
        }

        private static ShopDayCycle RunningCycle()
        {
            var cycle = new ShopDayCycle();
            Assert.That(cycle.OpenShop(), Is.True);
            cycle.Tick(12.25f);
            return cycle;
        }

        private CustomerVisit AcceptedVisit()
        {
            var visit = new CustomerVisit(model, Tanks);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            Assert.That(visit.TargetTankId, Is.EqualTo("tank-a"));
            return visit;
        }

        private static void Queue(CustomerVisit visit, ShopDayPhase phase)
        {
            visit.Advance(0.1f, true, phase);
            visit.Advance(2f, false, phase);
            visit.Advance(0.1f, true, phase);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
        }

        private void StockShopWithPriorIncome()
        {
            Produce("ribbonfin", "tank-a"); Ok(model.SellFromTank("tank-a"));
            Produce("pondlet", "tank-a"); Produce("bubblebelly", "tank-b");
            Assert.That(model.Currency, Is.EqualTo(10));
        }

        private void Produce(string species, string tank)
        {
            model.Tick(100f); var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Ok(model.TransferToTank(tank));
        }

        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
