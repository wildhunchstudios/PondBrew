using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class StaffSaveTests
    {
        const string Prefix = "EvolutionShop-StaffSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
        }

        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(directory, true);
        }

        [TestCase(ShopDayPhase.Night, false)] [TestCase(ShopDayPhase.Open, false)]
        [TestCase(ShopDayPhase.Closing, true)] [TestCase(ShopDayPhase.Night, true)]
        public void PaidRosterRoundTripPreservesReceiptsPauseAndAllOtherProgress(ShopDayPhase phase, bool paused)
        {
            var slot = FreshSlot();
            slot.game.currency = 50;
            Assert.That(GameModel.TryRestore(config, slot.game, out var game, out _), Is.True);
            var cycle = new ShopDayCycle(); var staff = new ShopStaff();
            staff.Hire(cycle); staff.SetScheduled(true, cycle); staff.TryBeginDuty(game, cycle);
            cycle.OpenShop(); cycle.Tick(483);
            staff.TryBeginDuty(game, cycle);
            staff.SetPaused(paused);
            if (phase != ShopDayPhase.Night) cycle.OpenShop();
            if (phase == ShopDayPhase.Closing) cycle.RequestClose();
            slot.game = game.CaptureSave(); slot.cycle = cycle.CaptureSave();
            slot.staff = staff.CaptureSave();
            slot.trades = ShopTradeLedger.LegacyForCycle(slot.cycle, 0).CaptureSave();
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restoredGame, out var loaded, out error), Is.True, error);
                Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
                Assert.That(loaded.game.version, Is.EqualTo(6));
                Assert.That(loaded.trades.version, Is.EqualTo(4));
                Assert.That(loaded.layout.version, Is.EqualTo(7));
                Assert.That(ShopStaff.TryRestore(loaded.staff, loaded.cycle.dayNumber, out var restoredStaff), Is.True);
                Assert.That(restoredStaff.WagesForDay(1), Is.EqualTo(4));
                Assert.That(restoredStaff.WagesForDay(2), Is.EqualTo(4));
                Assert.That(restoredStaff.IsOnDuty(2), Is.EqualTo(!paused));
                Assert.That(JsonUtility.ToJson(restoredGame.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(slot.game)));
                Assert.That(JsonUtility.ToJson(loaded.staff), Is.EqualTo(JsonUtility.ToJson(slot.staff)));
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            }
        }

        [Test] public void UnfundedScheduledLoadDoesNotRetryChargeWhenMoneyHasBecomeAvailable()
        {
            var slot = FreshSlot(); var staff = new ShopStaff(); var cycle = new ShopDayCycle();
            var game = new GameModel(config);
            staff.Hire(cycle); staff.SetScheduled(true, cycle);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.False);
            slot.staff = staff.CaptureSave(); slot.game.currency = 20;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out game, out var loaded, out error), Is.True, error);
            Assert.That(game.Currency, Is.EqualTo(20));
            Assert.That(ShopStaff.TryRestore(loaded.staff, 1, out staff), Is.True);
            Assert.That(staff.Hired && staff.Scheduled, Is.True);
            Assert.That(staff.IsOnDuty(1), Is.False);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(16));
        }

        [Test] public void DismissedPaidRosterRestoresReceiptAndRehireCostsNothingUntilNextPeriod()
        {
            var slot = FreshSlot(); slot.game.currency = 10;
            Assert.That(GameModel.TryRestore(config, slot.game, out var game, out _), Is.True);
            var staff = new ShopStaff(); var cycle = new ShopDayCycle();
            staff.Hire(cycle); staff.SetScheduled(true, cycle); staff.TryBeginDuty(game, cycle); staff.Dismiss(cycle);
            slot.game = game.CaptureSave(); slot.staff = staff.CaptureSave();
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out game, out var loaded, out error), Is.True, error);
            Assert.That(ShopStaff.TryRestore(loaded.staff, 1, out staff), Is.True);
            Assert.That(staff.Hired, Is.False); Assert.That(staff.WagesForDay(1), Is.EqualTo(4));
            staff.Hire(cycle); staff.SetScheduled(true, cycle);
            Assert.That(staff.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(6));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)]
        public void EveryLegacyEnvelopeMigratesAnEmptyRosterAndArchivesExactBytes(int version)
        {
            var old = FreshSlot(); old.formatVersion = version; old.game.currency = 37;
            old.game.version = SaveStore.GameVersionFor(version);
            if (version < 16) old.game.growers = Array.FindAll(old.game.growers, grower => grower.cultureId != "mist");
            if (version < 14) old.game.growers = Array.FindAll(old.game.growers, grower => grower.cultureId != "spark");
            old.trades.version = version < 3 ? 4 : SaveStore.ReportVersionFor(version);
            old.layout.version = version <= 8 ? 1 : version == 9 ? 2 : version == 10 ? 3 :
                version == 11 ? 4 : version == 12 ? 5 : version <= 14 ? 6 : 7;
            string json = WithoutStaff(JsonUtility.ToJson(old), old.staff).Replace("\"stocker\":" + JsonUtility.ToJson(old.stocker) + ",", "");
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.True, error);
            Assert.That(model.Currency, Is.EqualTo(37));
            Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(ShopStaff.TryRestore(slot.staff, slot.cycle.dayNumber, out var staff), Is.True);
            Assert.That(staff.Hired || staff.Scheduled || staff.Paused, Is.False);
            Assert.That(staff.PaidDay, Is.Zero); Assert.That(staff.PreviousPaidDay, Is.Zero);
            Assert.That(slot.stocker.employee.hired || slot.stocker.employee.scheduled, Is.False);
            Assert.That(slot.stocker.employee.paidWage, Is.Zero); Assert.That(slot.stocker.targets, Is.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".v" + version + ".bak"), Is.False);
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v" + version + ".bak"), Is.EqualTo(original));
            slot.volume = .23f;
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v" + version + ".bak"), Is.EqualTo(original));
        }

        [TestCase("V1Progress.json")] [TestCase("V7TradingNight.json")] [TestCase("V9ExpandedStock.json")]
        [TestCase("V10DecorStock.json")] [TestCase("V11StoredFurniture.json")] [TestCase("V12WorkingLab.json")]
        [TestCase("V13SpecimenStorage.json")] [TestCase("V14Research.json")] [TestCase("V15Conditioning.json")]
        public void FrozenProgressFixturesKeepStockResearchAndMachineStateWithNoEmployees(string fixture)
        {
            string source = Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", fixture);
            byte[] original = File.ReadAllBytes(source); File.WriteAllBytes(PathFor(), original);
            Assert.That(SaveStore.Read(PathFor(), config, out var game, out var slot, out var error), Is.True, error);
            string progress = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(slot.staff.hired || slot.staff.scheduled || slot.staff.paused, Is.False);
            Assert.That(slot.staff.paidWage, Is.Zero);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out game, out _, out error), Is.True, error);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(progress));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("empty")] [TestCase("nested-only")]
        [TestCase("missing-version")] [TestCase("missing-id")] [TestCase("missing-hired")]
        [TestCase("missing-scheduled")] [TestCase("missing-paused")] [TestCase("missing-paid-day")]
        [TestCase("missing-paid-wage")] [TestCase("missing-previous-day")] [TestCase("missing-previous-wage")]
        [TestCase("bool-null")] [TestCase("bool-number")] [TestCase("bool-string")] [TestCase("id-null")]
        [TestCase("int-null")] [TestCase("int-fraction")] [TestCase("int-string")] [TestCase("int-bool")]
        [TestCase("int-overflow")] [TestCase("duplicate-key")] [TestCase("duplicate-root")]
        [TestCase("future-receipt")] [TestCase("duplicate-receipt")] [TestCase("wrong-id")]
        [TestCase("negative-wage")] [TestCase("unhired-scheduled")] [TestCase("unhired-paused")]
        public void CurrentMalformedStaffCannotEraseProgressOrFallBackToAnEmptyRoster(string problem)
        {
            var slot = FreshSlot(); slot.game.currency = 99;
            string json = JsonUtility.ToJson(slot), staffJson = JsonUtility.ToJson(slot.staff);
            string changed = staffJson;
            switch (problem)
            {
                case "missing": json = WithoutStaff(json, slot.staff); break;
                case "null": changed = "null"; break;
                case "empty": changed = "{}"; break;
                case "nested-only": json = WithoutStaff(json, slot.staff).TrimEnd('}') + ",\"ignored\":{\"staff\":" + staffJson + "}}"; break;
                case "missing-version": changed = changed.Replace("\"version\":1,", ""); break;
                case "missing-id": changed = changed.Replace("\"employeeId\":\"cashier-pip\",", ""); break;
                case "missing-hired": changed = changed.Replace("\"hired\":false,", ""); break;
                case "missing-scheduled": changed = changed.Replace("\"scheduled\":false,", ""); break;
                case "missing-paused": changed = changed.Replace("\"paused\":false,", ""); break;
                case "missing-paid-day": changed = changed.Replace("\"paidDay\":0,", ""); break;
                case "missing-paid-wage": changed = changed.Replace("\"paidWage\":0,", ""); break;
                case "missing-previous-day": changed = changed.Replace("\"previousPaidDay\":0,", ""); break;
                case "missing-previous-wage": changed = changed.Replace(",\"previousPaidWage\":0", ""); break;
                case "bool-null": changed = changed.Replace("\"hired\":false", "\"hired\":null"); break;
                case "bool-number": changed = changed.Replace("\"scheduled\":false", "\"scheduled\":0"); break;
                case "bool-string": changed = changed.Replace("\"paused\":false", "\"paused\":\"false\""); break;
                case "id-null": changed = changed.Replace("\"cashier-pip\"", "null"); break;
                case "int-null": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":null"); break;
                case "int-fraction": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":0.0"); break;
                case "int-string": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":\"0\""); break;
                case "int-bool": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":false"); break;
                case "int-overflow": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":2147483648"); break;
                case "duplicate-key": changed = changed.Replace("\"hired\":false", "\"hired\":true,\"hired\":false"); break;
                case "duplicate-root": json = json.Replace("\"staff\":" + staffJson, "\"staff\":" + staffJson + ",\"staff\":" + staffJson); break;
                case "future-receipt": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":2").Replace("\"paidWage\":0", "\"paidWage\":4"); break;
                case "duplicate-receipt": changed = changed.Replace("\"paidDay\":0", "\"paidDay\":1").Replace("\"paidWage\":0", "\"paidWage\":4").Replace("\"previousPaidDay\":0", "\"previousPaidDay\":1").Replace("\"previousPaidWage\":0", "\"previousPaidWage\":4"); break;
                case "wrong-id": changed = changed.Replace("cashier-pip", "future-role"); break;
                case "negative-wage": changed = changed.Replace("\"paidWage\":0", "\"paidWage\":-4"); break;
                case "unhired-scheduled": changed = changed.Replace("\"scheduled\":false", "\"scheduled\":true"); break;
                case "unhired-paused": changed = changed.Replace("\"paused\":false", "\"paused\":true"); break;
            }
            if (changed != staffJson) json = json.Replace("\"staff\":" + staffJson, "\"staff\":" + changed);
            Assert.That(json, Is.Not.EqualTo(JsonUtility.ToJson(slot)));
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var game, out var loaded, out var error), Is.False);
            Assert.That(game, Is.Null); Assert.That(loaded, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        [TestCase("null")] [TestCase("future-day")] [TestCase("unsafe-state")]
        public void InvalidStaffWritePreservesThePreviousPlayableSave(string problem)
        {
            var slot = FreshSlot(); slot.game.currency = 23;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            if (problem == "null") slot.staff = null;
            else if (problem == "future-day") { slot.staff.paidDay = 2; slot.staff.paidWage = 4; }
            else slot.staff.paused = true;
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(), config, out var game, out _, out error), Is.True, error);
            Assert.That(game.Currency, Is.EqualTo(23));
        }

        SlotData FreshSlot() => new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        static string WithoutStaff(string json, StaffSave staff) => json.Replace("\"staff\":" + JsonUtility.ToJson(staff) + ",", "");
    }
}
