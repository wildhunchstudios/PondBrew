using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class SlapSaveTests
    {
        const string Prefix = "EvolutionShop-SlapSaveTests-";
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
            string target = Path.GetFullPath(directory);
            Assert.That(Path.GetDirectoryName(target).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(target).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(target, true);
        }

        [TestCase("F", 0f, false)]
        [TestCase("G", .45f, true)]
        [TestCase("Z", 1f, true)]
        public void IntentionalKeyAudioAndReducedMotionRoundTripWithExistingSettings(string key, float humor, bool reduced)
        {
            var slot = FreshSlot(); slot.slapKey = key; slot.humorVolume = humor; slot.reducedGags = reduced;
            slot.sensitivity = 1.7f; slot.volume = .28f; slot.reputation.points = 38;
            Write(slot);
            byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out _);
            Assert.That(loaded.slapKey, Is.EqualTo(key)); Assert.That(loaded.humorVolume, Is.EqualTo(humor));
            Assert.That(loaded.reducedGags, Is.EqualTo(reduced)); Assert.That(loaded.sensitivity, Is.EqualTo(1.7f));
            Assert.That(loaded.volume, Is.EqualTo(.28f)); Assert.That(loaded.reputation.points, Is.EqualTo(38));
            Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(loaded.trades.version, Is.EqualTo(4));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void EveryLegacyFormatReceivesDefaultGagSettingsOnlyInMemory(int format)
        {
            byte[] original = WriteLegacy(format, FreshSlot());
            for (int i = 0; i < 2; i++)
            {
                var slot = Read(out _);
                Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(slot.trades.version, Is.EqualTo(4));
                Assert.That(slot.slapKey, Is.EqualTo("F")); Assert.That(slot.humorVolume, Is.EqualTo(1f));
                Assert.That(slot.reducedGags, Is.False); Assert.That(slot.sensitivity, Is.EqualTo(1.35f));
                Assert.That(slot.volume, Is.EqualTo(.37f));
                Assert.That(slot.reputation.points, Is.EqualTo(format == 6 ? 42 : 0));
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
            }
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void FormatSixMigrationKeepsReputationTagsStockPendingVisitorsAndClock(ShopDayPhase phase)
        {
            var slot = PendingSlot(out var model, out var day, out _, out _, out _);
            if (phase == ShopDayPhase.Closing) { Assert.That(day.RequestClose(), Is.True); day.Tick(.4f); slot.cycle = day.CaptureSave(); }
            string expectedModel = JsonUtility.ToJson(slot.game), expectedClock = JsonUtility.ToJson(slot.cycle);
            string a = model.GetTank("tank-a").Value.Id, b = model.GetTank("tank-b").Value.Id;
            byte[] original = WriteLegacy(6, slot);
            var loaded = Read(out var restored);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(expectedModel));
            Assert.That(JsonUtility.ToJson(loaded.cycle), Is.EqualTo(expectedClock));
            Assert.That(loaded.trades.pendingCustomerIds, Is.EqualTo(slot.trades.pendingCustomerIds));
            Assert.That(loaded.reputation.points, Is.EqualTo(42)); Assert.That(loaded.slapKey, Is.EqualTo("F"));
            Assert.That(restored.GetReservation("tank-a"), Is.Null); Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(a)); Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(b));
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var ledger), Is.True);
            Assert.That(ledger.CancelPendingForReload(), Is.True); Assert.That(ledger.CancelPendingForReload(), Is.False);
            Assert.That(ledger.Current.interrupted, Is.EqualTo(2)); Assert.That(ledger.Current.slapped, Is.Zero);
            Assert.That(loaded.reputation.points, Is.EqualTo(42));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [Test]
        public void FirstFormatSixWriteArchivesExactLegacyBytesAndNeverRotatesThatArchive()
        {
            byte[] original = WriteLegacy(6, FreshSlot());
            var slot = Read(out _); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v6.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] current = File.ReadAllBytes(PathFor());
            slot.slapKey = "G"; slot.humorVolume = 0f; slot.reducedGags = true; Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v6.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(current));
            Assert.That(Read(out _).reputation.points, Is.EqualTo(42));
        }

        [Test]
        public void ExistingFormatSixArchiveIsNeverOverwritten()
        {
            WriteLegacy(6, FreshSlot());
            byte[] archive = { 6, 2, 9, 4 }; File.WriteAllBytes(PathFor() + ".v6.bak", archive);
            Write(Read(out _));
            Assert.That(File.ReadAllBytes(PathFor() + ".v6.bak"), Is.EqualTo(archive));
        }

        [Test]
        public void FailedFormatSixArchiveLeavesPrimaryAndExistingBackupUntouched()
        {
            byte[] original = WriteLegacy(6, FreshSlot());
            byte[] backup = { 1, 2, 3 }; File.WriteAllBytes(PathFor() + ".bak", backup);
            var slot = Read(out _); Directory.CreateDirectory(PathFor() + ".v6.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("")] [TestCase("f")] [TestCase("F8")]
        [TestCase("F ")] [TestCase("1")] [TestCase("!")] [TestCase("A")] [TestCase("D")]
        [TestCase("E")] [TestCase("S")] [TestCase("W")] [TestCase("É")]
        public void InvalidOrMissingCurrentBindingRejectsReadWithoutChangingFiles(string key)
        {
            string json = JsonUtility.ToJson(FreshSlot());
            string binding = key == "null" ? "null" : "\"" + key + "\"";
            json = key == "missing" ? json.Replace(",\"slapKey\":\"F\"", "") :
                json.Replace("\"slapKey\":\"F\"", "\"slapKey\":" + binding);
            Assert.That(json, Does.Not.Contain("\"slapKey\":\"F\""), "The malformed fixture must actually replace the binding.");
            AssertInvalidRead(json, "gag settings");
        }

        [TestCase(null)] [TestCase("")] [TestCase("f")] [TestCase("F8")] [TestCase("F ")]
        [TestCase("1")] [TestCase("!")] [TestCase("A")] [TestCase("D")]
        [TestCase("E")] [TestCase("S")] [TestCase("W")] [TestCase("É")]
        public void InvalidCurrentBindingRejectsWriteBeforeAnyFileReplacement(string key)
        {
            var slot = FreshSlot(); Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            slot.slapKey = key;
            AssertInvalidWrite(slot, original);
        }

        [TestCase(-.01f)] [TestCase(1.01f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidHumorVolumeRejectsReadAndWriteWithoutTouchingKnownGoodPrimary(float value)
        {
            var slot = FreshSlot(); Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            slot.humorVolume = value; AssertInvalidWrite(slot, original);
            // A non-finite value may be rejected by the JSON parser before settings validation.
            AssertInvalidRead(JsonUtility.ToJson(slot), float.IsNaN(value) || float.IsInfinity(value) ? null : "gag settings");
        }

        [TestCase(false)] [TestCase(true)]
        public void SlappedAndPaidOutcomesRoundTripWithoutReplayingPenaltyOrLosingUnpaidStock(bool finishDay)
        {
            PendingSlot(out var model, out var day, out var reports, out var first, out var second);
            string unpaid = model.GetTank("tank-a").Value.Id;
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = 1, points = 30 }, out var reputation), Is.True);
            Assert.That(first.Slap(day.Phase), Is.True);
            Assert.That(reputation.ResolveVisit(reports, first.Id, first.Outcome), Is.True);
            Ok(second.Serve(day.Phase)); Assert.That(reputation.ResolveVisit(reports, second.Id, second.Outcome, second.Price), Is.True);
            int points = reputation.Points, currency = model.Currency;
            Assert.That(reports.Current.slapped, Is.EqualTo(1)); Assert.That(reports.Current.sales, Is.EqualTo(1));
            if (finishDay)
            {
                Assert.That(day.RequestClose(), Is.True); day.Tick(3f); Assert.That(reports.FinishDay(1), Is.True);
            }
            var slot = new SlotData { game = model.CaptureSave(), cycle = day.CaptureSave(), trades = reports.CaptureSave(),
                reputation = reputation.CaptureSave(), slapKey = "H", humorVolume = .3f, reducedGags = true };
            Write(slot);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var loaded = Read(out var restored);
                Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var ledger), Is.True);
                Assert.That(ShopReputation.TryRestore(loaded.reputation, out var rep), Is.True);
                var report = finishDay ? ledger.Last : ledger.Current;
                Assert.That(report.slapped, Is.EqualTo(1)); Assert.That(report.sales, Is.EqualTo(1)); Assert.That(report.revenue, Is.EqualTo(currency));
                Assert.That(rep.ResolveVisit(ledger, first.Id, CustomerOutcome.Slapped), Is.False);
                Assert.That(rep.ResolveVisit(ledger, second.Id, CustomerOutcome.Sold, second.Price), Is.False);
                Assert.That(rep.Points, Is.EqualTo(points)); Assert.That(restored.Currency, Is.EqualTo(currency));
                Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(unpaid)); Assert.That(restored.GetReservation("tank-a"), Is.Null);
                Assert.That(restored.GetTank("tank-b"), Is.Null); Assert.That(loaded.slapKey, Is.EqualTo("H"));
                Write(loaded);
            }
        }

        [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void SlappedOutcomeCannotBeSmuggledIntoAnyLegacyReport(int format)
        {
            var slot = FreshSlot(); var day = new ShopDayCycle(); day.OpenShop(); slot.cycle = day.CaptureSave();
            slot.trades.hasCurrent = true;
            slot.trades.current = new TradeDaySave { dayNumber = 1, visitors = 1, slapped = 1 };
            WriteLegacy(format, slot);
            string json = File.ReadAllText(PathFor()); AssertInvalidRead(json, "trading report");
        }

        [TestCase(6, 4)] [TestCase(7, 3)]
        public void ReportVersionMustMatchItsEnvelope(int format, int report)
        {
            var slot = FreshSlot(); slot.formatVersion = format; slot.game.version = 2; slot.trades.version = report;
            AssertInvalidRead(JsonUtility.ToJson(slot), "trading report");
        }

        [TestCase(-1)] [TestCase(1000001)] [TestCase(1)]
        public void InvalidCurrentSlapCounterRejectsReadAndWrite(int count)
        {
            var slot = FreshSlot(); var day = new ShopDayCycle(); day.OpenShop(); slot.cycle = day.CaptureSave();
            slot.trades = ShopTradeLedger.LegacyForCycle(slot.cycle, 0).CaptureSave();
            Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            slot.trades.current.slapped = count; // Positive one also violates the zero-visitor outcome sum.
            AssertInvalidWrite(slot, original);
            AssertInvalidRead(JsonUtility.ToJson(slot), "trading report");
        }

        SlotData FreshSlot() => new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
        SlotData Read(out GameModel model)
        {
            Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error);
            return slot;
        }
        void AssertInvalidRead(string json, string expectedReason)
        {
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.False);
            Assert.That(model, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty);
            if (expectedReason != null) Assert.That(error, Does.Contain(expectedReason));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }
        void AssertInvalidWrite(SlotData slot, byte[] original)
        {
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }
        byte[] WriteLegacy(int format, SlotData slot)
        {
            slot.game.version = format < 4 ? 1 : 2; if (format < 4) slot.game.prices = null;
            slot.trades.version = format == 3 ? 1 : format == 4 ? 2 : 3;
            if (format < 5) slot.trades.pendingCustomerIds = null;
            string json = "{\n  \"formatVersion\": " + format + ",\n  \"game\": " + JsonUtility.ToJson(slot.game, true) +
                (format >= 2 ? ",\n  \"cycle\": " + JsonUtility.ToJson(slot.cycle, true) : "") +
                (format >= 3 ? ",\n  \"trades\": " + JsonUtility.ToJson(slot.trades, true) : "") +
                (format >= 6 ? ",\n  \"reputation\": {\"version\":1,\"points\":42}" : "") +
                ",\n  \"sensitivity\": 1.35,\n  \"volume\": 0.37\n}";
            File.WriteAllText(PathFor(), json); return File.ReadAllBytes(PathFor());
        }
        SlotData PendingSlot(out GameModel model, out ShopDayCycle day, out ShopTradeLedger reports,
            out CustomerVisit first, out CustomerVisit second)
        {
            model = new GameModel(config); Produce(model, "pondlet", "tank-a"); Produce(model, "bubblebelly", "tank-b");
            Ok(model.SetPrice("pondlet", 8));
            day = new ShopDayCycle(); day.OpenShop(); day.Tick(2f);
            reports = new ShopTradeLedger(); Assert.That(reports.BeginDay(1, 2), Is.True);
            first = new CustomerVisit(model, new[] { "tank-a", "tank-b" });
            Assert.That(reports.BeginVisit(first.Id), Is.True); Queue(first);
            second = new CustomerVisit(model, new[] { "tank-a", "tank-b" });
            Assert.That(reports.BeginVisit(second.Id), Is.True); Queue(second);
            return new SlotData { game = model.CaptureSave(), cycle = day.CaptureSave(), trades = reports.CaptureSave() };
        }
        void Produce(GameModel model, string id, string tank)
        {
            model.Tick(100f); var recipe = model.GetSpecies(id);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Ok(model.TransferToTank(tank));
        }
        static void Queue(CustomerVisit visit)
        {
            visit.Advance(.1f, true, ShopDayPhase.Open); visit.Advance(4f, false, ShopDayPhase.Open);
            visit.Advance(.1f, true, ShopDayPhase.Open); Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
        }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
