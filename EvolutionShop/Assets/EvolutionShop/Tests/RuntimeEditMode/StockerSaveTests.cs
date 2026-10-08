using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class StockerSaveTests
    {
        const string Prefix = "EvolutionShop-StockerSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); config = GameConfig.CreateDefault();
        }
        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(directory, true);
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void CurrentRoundTripPreservesIndependentTwoPeriodReceiptsTargetsAndStock(ShopDayPhase phase)
        {
            var slot = FundedRoster(out var game, out var cycle, out var cashier, out var stocking);
            cycle.OpenShop(); cycle.Tick(483);
            Assert.That(cashier.TryBeginDuty(game, cycle), Is.True);
            Assert.That(stocking.Employee.TryBeginDuty(game, cycle), Is.True);
            stocking.Employee.SetPaused(true);
            if (phase != ShopDayPhase.Night) cycle.OpenShop();
            if (phase == ShopDayPhase.Closing) cycle.RequestClose();
            slot.game = game.CaptureSave(); slot.cycle = cycle.CaptureSave(); slot.staff = cashier.CaptureSave();
            slot.stocker = stocking.CaptureSave(game);
            slot.trades = ShopTradeLedger.LegacyForCycle(slot.cycle, 1).CaptureSave();
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restoredGame, out var loaded, out error), Is.True, error);
                Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
                Assert.That(ShopStocking.TryRestore(loaded.stocker, restoredGame, 2, out var restored), Is.True);
                Assert.That(restored.Employee.WagesForDay(1), Is.EqualTo(4));
                Assert.That(restored.Employee.WagesForDay(2), Is.EqualTo(4));
                Assert.That(restored.Employee.Paused, Is.True);
                Assert.That(restored.Employee.IsOnDuty(2), Is.False);
                Assert.That(restored.GetTarget("tank-b"), Is.EqualTo("pondlet"));
                Assert.That(restoredGame.Currency, Is.EqualTo(34));
                Assert.That(JsonUtility.ToJson(loaded.staff), Is.EqualTo(JsonUtility.ToJson(slot.staff)));
                Assert.That(JsonUtility.ToJson(restoredGame.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(slot.game)));
                Assert.That(JsonUtility.ToJson(loaded.stocker), Is.EqualTo(JsonUtility.ToJson(slot.stocker)));
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void VersionSeventeenKeepsExactCashierAndMigratesOnlyEmptyStocker(bool paidCashier)
        {
            var slot = FundedRoster(out var game, out var cycle, out _, out _);
            slot.formatVersion = 17;
            if (!paidCashier) slot.staff = new ShopStaff().CaptureSave();
            string cashierJson = JsonUtility.ToJson(slot.staff);
            string json = WithoutStocker(JsonUtility.ToJson(slot), slot.stocker);
            File.WriteAllText(PathFor(), json); byte[] old = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(JsonUtility.ToJson(loaded.staff), Is.EqualTo(cashierJson));
            Assert.That(loaded.stocker.employee.hired || loaded.stocker.employee.scheduled, Is.False);
            Assert.That(loaded.stocker.employee.paidWage, Is.Zero); Assert.That(loaded.stocker.targets, Is.Empty);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(game.CaptureSave())));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(old));
            Assert.That(File.Exists(PathFor() + ".v17.bak"), Is.False);
            Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v17.bak"), Is.EqualTo(old));
            loaded.volume = .12f; Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v17.bak"), Is.EqualTo(old));
        }

        [Test] public void VersionSeventeenCannotHideMissingOrCorruptCashierBehindStockerMigration()
        {
            var slot = FreshSlot(); slot.formatVersion = 17;
            string valid = WithoutStocker(JsonUtility.ToJson(slot), slot.stocker);
            File.WriteAllText(PathFor(), valid);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out var error), Is.True, error);
            string invalid = valid.Replace("\"staff\":" + JsonUtility.ToJson(slot.staff) + ",", "");
            File.WriteAllText(PathFor(), invalid);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out _), Is.False);
            Assert.That(File.ReadAllText(PathFor()), Is.EqualTo(invalid));
        }

        [Test] public void UnpaidScheduledStockerRestoresWithoutPaymentOrWorkEvenWithNewFunds()
        {
            var slot = FreshSlot(); var game = new GameModel(config); var cycle = new ShopDayCycle();
            var stocking = new ShopStocking();
            stocking.Employee.Hire(cycle); stocking.Employee.SetScheduled(true, cycle);
            Assert.That(stocking.Employee.TryBeginDuty(game, cycle), Is.False);
            stocking.SetTarget(game, "tank-a", ShopStocking.AnySpecies);
            slot.stocker = stocking.CaptureSave(game); slot.game.currency = 20;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out game, out var loaded, out error), Is.True, error);
            Assert.That(game.Currency, Is.EqualTo(20));
            Assert.That(ShopStocking.TryRestore(loaded.stocker, game, 1, out stocking), Is.True);
            Assert.That(stocking.Employee.IsOnDuty(1), Is.False); Assert.That(stocking.Employee.PaidDay, Is.Zero);
            Assert.That(stocking.GetTarget("tank-a"), Is.EqualTo(ShopStocking.AnySpecies));
            Assert.That(stocking.Employee.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(16));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("empty")] [TestCase("nested-only")]
        [TestCase("version-missing")] [TestCase("version-string")] [TestCase("version-zero")] [TestCase("duplicate-root")]
        [TestCase("employee-missing")] [TestCase("employee-null")] [TestCase("employee-empty")] [TestCase("employee-cashier")]
        [TestCase("targets-missing")] [TestCase("targets-null")] [TestCase("targets-object")] [TestCase("targets-string")]
        [TestCase("targets-duplicate-key")] [TestCase("target-null")] [TestCase("target-empty")] [TestCase("target-duplicate")]
        [TestCase("tank-missing")] [TestCase("tank-null")] [TestCase("tank-number")] [TestCase("tank-unknown")]
        [TestCase("species-missing")] [TestCase("species-null")] [TestCase("species-number")]
        [TestCase("species-unknown")] [TestCase("species-undiscovered")] [TestCase("target-duplicate-field")]
        [TestCase("employee-missing-bool")] [TestCase("employee-bool-null")] [TestCase("employee-bool-number")]
        [TestCase("employee-missing-day")] [TestCase("employee-day-string")] [TestCase("employee-day-fraction")]
        [TestCase("employee-duplicate-field")] [TestCase("employee-future-wage")] [TestCase("employee-unhired-scheduled")]
        public void MalformedCurrentStockerNeverFallsBackToEmptySettingsOrChangesOriginal(string problem)
        {
            var slot = FreshSlot(); slot.game.currency = 71;
            slot.stocker.targets = new[] { new StockerTargetSave { tankId = "tank-a", speciesId = "*" } };
            string valid = JsonUtility.ToJson(slot), part = JsonUtility.ToJson(slot.stocker);
            File.WriteAllText(PathFor(), valid);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out var controlError), Is.True, controlError);
            string changed = part, employee = JsonUtility.ToJson(slot.stocker.employee);
            string target = JsonUtility.ToJson(slot.stocker.targets[0]);
            string invalid = valid;
            switch (problem)
            {
                case "missing": invalid = WithoutStocker(valid, slot.stocker); break;
                case "null": changed = "null"; break;
                case "empty": changed = "{}"; break;
                case "nested-only": invalid = WithoutStocker(valid, slot.stocker).TrimEnd('}') + ",\"ignored\":{\"stocker\":" + part + "}}"; break;
                case "version-missing": changed = changed.Remove(1, "\"version\":1,".Length); break;
                case "version-string": changed = changed.Remove(1, "\"version\":1".Length).Insert(1, "\"version\":\"1\""); break;
                case "version-zero": changed = changed.Remove(1, "\"version\":1".Length).Insert(1, "\"version\":0"); break;
                case "duplicate-root": invalid = valid.Replace("\"stocker\":" + part, "\"stocker\":" + part + ",\"stocker\":" + part); break;
                case "employee-missing": changed = changed.Replace("\"employee\":" + employee + ",", ""); break;
                case "employee-null": changed = changed.Replace(employee, "null"); break;
                case "employee-empty": changed = changed.Replace(employee, "{}"); break;
                case "employee-cashier": changed = changed.Replace(ShopStaff.StockerId, ShopStaff.CashierId); break;
                case "targets-missing": changed = changed.Replace(",\"targets\":[" + target + "]", ""); break;
                case "targets-null": changed = changed.Replace("[" + target + "]", "null"); break;
                case "targets-object": changed = changed.Replace("[" + target + "]", "{}"); break;
                case "targets-string": changed = changed.Replace("[" + target + "]", "\"[]\""); break;
                case "targets-duplicate-key": changed = changed.Replace("\"targets\":", "\"targets\":[],\"targets\":"); break;
                case "target-null": changed = changed.Replace(target, "null"); break;
                case "target-empty": changed = changed.Replace(target, "{}"); break;
                case "target-duplicate": changed = changed.Replace(target, target + "," + target); break;
                case "tank-missing": changed = changed.Replace("\"tankId\":\"tank-a\",", ""); break;
                case "tank-null": changed = changed.Replace("\"tank-a\"", "null"); break;
                case "tank-number": changed = changed.Replace("\"tank-a\"", "0"); break;
                case "tank-unknown": changed = changed.Replace("tank-a", "missing"); break;
                case "species-missing": changed = changed.Replace(",\"speciesId\":\"*\"", ""); break;
                case "species-null": changed = changed.Replace("\"*\"", "null"); break;
                case "species-number": changed = changed.Replace("\"*\"", "0"); break;
                case "species-unknown": changed = changed.Replace("\"*\"", "\"missing\""); break;
                case "species-undiscovered": changed = changed.Replace("\"*\"", "\"pondlet\""); break;
                case "target-duplicate-field": changed = changed.Replace("\"tankId\":", "\"tankId\":\"tank-b\",\"tankId\":"); break;
                case "employee-missing-bool": changed = changed.Replace("\"paused\":false,", ""); break;
                case "employee-bool-null": changed = changed.Replace("\"paused\":false", "\"paused\":null"); break;
                case "employee-bool-number": changed = changed.Replace("\"scheduled\":false", "\"scheduled\":0"); break;
                case "employee-missing-day": changed = changed.Replace("\"previousPaidDay\":0,", ""); break;
                case "employee-day-string": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":\"0\""); break;
                case "employee-day-fraction": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":0.0"); break;
                case "employee-duplicate-field": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":0,\"paidDay\":0"); break;
                case "employee-future-wage": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":2").Replace("\"paidWage\":0", "\"paidWage\":4"); break;
                case "employee-unhired-scheduled": changed = changed.Replace("\"scheduled\":false", "\"scheduled\":true"); break;
            }
            if (changed != part) invalid = valid.Replace("\"stocker\":" + part, "\"stocker\":" + changed);
            Assert.That(invalid, Is.Not.EqualTo(valid), "The selected corruption must alter the control.");
            File.WriteAllText(PathFor(), invalid); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var game, out var loaded, out var error), Is.False);
            Assert.That(game, Is.Null); Assert.That(loaded, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase("null")] [TestCase("receipt")] [TestCase("targets")] [TestCase("role")]
        public void InvalidWritePreservesKnownGoodRosterStockAndCash(string problem)
        {
            var slot = FundedRoster(out _, out _, out _, out _);
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            if (problem == "null") slot.stocker = null;
            else if (problem == "receipt") slot.stocker.employee.paidDay = 2;
            else if (problem == "targets") slot.stocker.targets[0].tankId = "retired-display";
            else slot.stocker.employee.employeeId = ShopStaff.CashierId;
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(), config, out var game, out _, out error), Is.True, error);
            Assert.That(game.Currency, Is.EqualTo(42));
            Assert.That(game.GetTank("tank-a").Value.SpeciesId, Is.EqualTo("pondlet"));
        }

        SlotData FundedRoster(out GameModel game, out ShopDayCycle cycle, out ShopStaff cashier, out ShopStocking stocking)
        {
            var slot = FreshSlot(); slot.game.currency = 50;
            Assert.That(GameModel.TryRestore(config, slot.game, out game, out _), Is.True);
            game.Collect(CultureKind.Bloom); game.Collect(CultureKind.Flow); game.Dose(CultureKind.Bloom); game.Dose(CultureKind.Flow);
            Assert.That(game.BeginIncubation().Success, Is.True); game.Tick(10);
            Assert.That(game.TransferToTank("tank-a").Success, Is.True);
            cycle = new ShopDayCycle(); cashier = new ShopStaff(); stocking = new ShopStocking();
            cashier.Hire(cycle); cashier.SetScheduled(true, cycle); Assert.That(cashier.TryBeginDuty(game, cycle), Is.True);
            stocking.Employee.Hire(cycle); stocking.Employee.SetScheduled(true, cycle);
            Assert.That(stocking.Employee.TryBeginDuty(game, cycle), Is.True);
            stocking.SetTarget(game, "tank-b", "pondlet");
            slot.game = game.CaptureSave(); slot.cycle = cycle.CaptureSave(); slot.staff = cashier.CaptureSave();
            slot.stocker = stocking.CaptureSave(game); return slot;
        }
        SlotData FreshSlot() => new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        static string WithoutStocker(string json, StockerSave stocker) => json.Replace("\"stocker\":" + JsonUtility.ToJson(stocker) + ",", "");
    }
}
