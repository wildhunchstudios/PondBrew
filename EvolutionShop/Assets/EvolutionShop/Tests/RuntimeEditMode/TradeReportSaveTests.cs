using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class TradeReportSaveTests
    {
        const string DirectoryPrefix = "EvolutionShop-TradeReportSaveTests-";
        string directory;
        GameConfig config;
        GameModel model;
        ShopDayCycle cycle;
        ShopTradeLedger reports;

        [Serializable]
        sealed class VersionTwoSlot
        {
            public int formatVersion = 2;
            public SaveData game;
            public ShopDaySave cycle;
            public float sensitivity = 1.3f;
            public float volume = 0.27f;
        }

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
            cycle = new ShopDayCycle();
            reports = new ShopTradeLedger();
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

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void FileReadPreservesPaidTotalsAndPendingIdentityUntilSessionCancelsIt(ShopDayPhase phase)
        {
            StockAndOpen();
            Sell("tank-a", "paid-first");
            Assert.That(reports.BeginVisit("waiting-second"), Is.True);
            Ok(model.ReserveTank("tank-b", "waiting-second"));
            string unsold = model.GetTank("tank-b").Value.Id;
            if (phase == ShopDayPhase.Closing) Assert.That(cycle.RequestClose(), Is.True);
            cycle.Tick(0.75f);
            var source = Slot();
            Write(source);
            byte[] original = File.ReadAllBytes(PathFor());

            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
            Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(slot.trades.version, Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(JsonUtility.ToJson(slot.trades), Is.EqualTo(JsonUtility.ToJson(source.trades)));
            Assert.That(JsonUtility.ToJson(slot.cycle), Is.EqualTo(JsonUtility.ToJson(source.cycle)));
            Assert.That(slot.sensitivity, Is.EqualTo(1.6f)); Assert.That(slot.volume, Is.EqualTo(0.38f));
            Assert.That(restored.Currency, Is.EqualTo(6)); Assert.That(restored.GetTank("tank-a"), Is.Null);
            Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(unsold));
            Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(slot.trades.pendingCustomerIds, Is.EqualTo(new[] { "waiting-second" }));
            Assert.That(reports.HasPending, Is.True, "Read must not mutate the running ledger.");

            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var loadedReports), Is.True);
            Assert.That(loadedReports.CancelPendingForReload(), Is.True);
            Assert.That(loadedReports.CancelPendingForReload(), Is.False);
            Assert.That(loadedReports.Current.visitors, Is.EqualTo(2));
            Assert.That(loadedReports.Current.sales, Is.EqualTo(1));
            Assert.That(loadedReports.Current.revenue, Is.EqualTo(6L));
            Assert.That(loadedReports.Current.interrupted, Is.EqualTo(1));
            Assert.That(restored.SellReservedTank("tank-b", "waiting-second", unsold).Success, Is.False);
            slot.trades = loadedReports.CaptureSave();
            Write(slot);
            Assert.That(SaveStore.Read(PathFor(), config, out var again, out var repeated, out error), Is.True, error);
            Assert.That(repeated.trades.pendingCustomerIds, Is.Empty);
            Assert.That(repeated.trades.current.interrupted, Is.EqualTo(1));
            Assert.That(repeated.trades.current.sales, Is.EqualTo(1));
            Assert.That(repeated.trades.current.revenue, Is.EqualTo(6L));
            Assert.That(again.Currency, Is.EqualTo(6)); Assert.That(again.GetTank("tank-b").Value.Id, Is.EqualTo(unsold));
        }

        [Test]
        public void TwoDayFileRoundTripsKeepLastReportSeparateFromCurrentAndDoNotReplayPayment()
        {
            StockAndOpen();
            Sell("tank-a", "day-one-sale");
            FinishDay();
            Write(Slot());
            Reload();
            Assert.That(reports.Current, Is.Null); Assert.That(reports.Last.dayNumber, Is.EqualTo(1));
            Assert.That(reports.Last.revenue, Is.EqualTo(6L)); Assert.That(reports.Last.stockedAtClose, Is.EqualTo(1));

            Assert.That(cycle.OpenShop(), Is.True); Assert.That(reports.BeginDay(cycle.DayNumber, StockedCount()), Is.True);
            Produce("ribbonfin", "tank-a"); Assert.That(reports.RecordRestock(), Is.True);
            string ribbonfin = model.GetTank("tank-a").Value.Id;
            Sell("tank-b", "day-two-sale");
            Assert.That(model.Currency, Is.EqualTo(16));
            Write(Slot());
            Reload();
            Assert.That(reports.Current.dayNumber, Is.EqualTo(2)); Assert.That(reports.Current.sales, Is.EqualTo(1));
            Assert.That(reports.Current.revenue, Is.EqualTo(10L)); Assert.That(reports.Current.restocked, Is.EqualTo(1));
            Assert.That(reports.Last.dayNumber, Is.EqualTo(1)); Assert.That(reports.Last.revenue, Is.EqualTo(6L));
            Assert.That(reports.ResolveVisit("day-two-sale", CustomerOutcome.Sold, 10), Is.False);
            Assert.That(model.SellReservedTank("tank-b", "day-two-sale", "already-sold").Success, Is.False);

            FinishDay();
            string expected = JsonUtility.ToJson(reports.CaptureSave());
            Write(Slot()); Reload(); Write(Slot()); Reload();
            Assert.That(cycle.DayNumber, Is.EqualTo(3)); Assert.That(cycle.Phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(reports.Current, Is.Null); Assert.That(reports.Last.dayNumber, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(reports.CaptureSave()), Is.EqualTo(expected));
            Assert.That(reports.Last.sales, Is.EqualTo(1)); Assert.That(reports.Last.revenue, Is.EqualTo(10L));
            Assert.That(reports.Last.stockedAtOpen, Is.EqualTo(1)); Assert.That(reports.Last.stockedAtClose, Is.EqualTo(1));
            Assert.That(reports.Last.restocked, Is.EqualTo(1)); Assert.That(reports.Last.partial, Is.False);
            Assert.That(model.Currency, Is.EqualTo(16)); Assert.That(model.GetTank("tank-b"), Is.Null);
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(ribbonfin));
        }

        [TestCase(ShopDayPhase.Night)]
        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void VersionTwoReadMigratesOnlyInMemoryWithHonestPartialHistory(ShopDayPhase phase)
        {
            byte[] original = WriteVersionTwo(phase);
            string expectedGame = JsonUtility.ToJson(model.CaptureSave());
            string expectedCycle = JsonUtility.ToJson(cycle.CaptureSave());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
                Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(slot.game.version, Is.EqualTo(GameModel.SaveVersion));
                Assert.That(slot.cycle.version, Is.EqualTo(1)); Assert.That(slot.trades.version, Is.EqualTo(ShopTradeLedger.SaveVersion));
                Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(expectedGame));
                Assert.That(JsonUtility.ToJson(slot.cycle), Is.EqualTo(expectedCycle));
                Assert.That(slot.sensitivity, Is.EqualTo(1.3f)); Assert.That(slot.volume, Is.EqualTo(0.27f));
                Assert.That(restored.Currency, Is.EqualTo(6)); Assert.That(slot.trades.last, Is.Null);
                Assert.That(slot.trades.pendingCustomerIds, Is.Null.Or.Empty);
                if (phase == ShopDayPhase.Night) Assert.That(slot.trades.current, Is.Null);
                else
                {
                    Assert.That(slot.trades.current.dayNumber, Is.EqualTo(cycle.DayNumber));
                    Assert.That(slot.trades.current.partial, Is.True);
                    Assert.That(slot.trades.current.stockedAtOpen, Is.EqualTo(2));
                    Assert.That(slot.trades.current.visitors, Is.Zero); Assert.That(slot.trades.current.sales, Is.Zero);
                    Assert.That(slot.trades.current.revenue, Is.Zero);
                }
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1), "Read must not create temp or backup files.");
            }
        }

        [Test]
        public void FirstVersionTwoReplacementArchivesOriginalBytesWhileBackupKeepsRotating()
        {
            byte[] original = WriteVersionTwo(ShopDayPhase.Open);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Write(slot);
            byte[] firstCurrent = File.ReadAllBytes(PathFor());
            Assert.That(File.ReadAllBytes(PathFor() + ".v2.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            slot.volume = 0.8f; Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(firstCurrent));
            slot.volume = 0.4f; Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v2.bak"), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".v1.bak"), Is.False);
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out error), Is.True, error);
            Assert.That(restored.Currency, Is.EqualTo(6)); Assert.That(loaded.trades.current.partial, Is.True);
            Assert.That(loaded.volume, Is.EqualTo(0.4f));
        }

        [Test]
        public void ExistingVersionTwoArchiveIsNeverOverwritten()
        {
            WriteVersionTwo(ShopDayPhase.Closing);
            byte[] preserved = { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(PathFor() + ".v2.bak", preserved);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Write(slot); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v2.bak"), Is.EqualTo(preserved));
        }

        [Test]
        public void FailedVersionTwoArchiveLeavesLegacyPrimaryUntouched()
        {
            byte[] original = WriteVersionTwo(ShopDayPhase.Open);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Directory.CreateDirectory(PathFor() + ".v2.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        [TestCase("missing", 3)]
        [TestCase("null", 3)]
        [TestCase("empty", 3)]
        [TestCase("missing-version", 3)]
        [TestCase("missing", 4)]
        [TestCase("null", 4)]
        [TestCase("empty", 4)]
        [TestCase("missing-version", 4)]
        public void ReportFormatsCannotOmitReportEvenOnPreparationNight(string problem, int format)
        {
            string payload = problem == "null" ? ",\"trades\":null"
                : problem == "empty" ? ",\"trades\":{}"
                : problem == "missing-version" ? ",\"trades\":{\"hasCurrent\":false,\"hasLast\":false}" : "";
            var game = format == 3 ? LegacyGame() : model.CaptureSave();
            string json = "{\"formatVersion\":" + format + ",\"game\":" + JsonUtility.ToJson(game) +
                ",\"cycle\":" + JsonUtility.ToJson(cycle.CaptureSave()) + payload + "}";
            File.WriteAllText(PathFor(), json);
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.False);
            Assert.That(restored, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()), Is.EqualTo(json));
        }

        [TestCase("missing")]
        [TestCase("null")]
        [TestCase("version")]
        [TestCase("negative")]
        [TestCase("outcomes")]
        [TestCase("wrong-day")]
        [TestCase("missing-current")]
        [TestCase("night-current")]
        [TestCase("wrong-last")]
        [TestCase("pending-owner")]
        [TestCase("unearned-revenue")]
        public void InvalidCurrentReportIsRejectedWithoutExposingGameOrChangingFiles(string problem)
        {
            StockAndOpen();
            var slot = Slot();
            switch (problem)
            {
                case "null": slot.trades = null; break;
                case "version": slot.trades.version = 99; break;
                case "negative": slot.trades.current.restocked = -1; break;
                case "outcomes": slot.trades.current.sales = 1; break;
                case "wrong-day": slot.trades.current.dayNumber++; break;
                case "missing-current": slot.trades.current = null; break;
                case "night-current": slot.cycle.phase = ShopDayPhase.Night; slot.cycle.secondsRemaining = 0; break;
                case "wrong-last": slot.trades.last = new TradeDaySave { dayNumber = 1 }; break;
                case "pending-owner": slot.trades.pendingCustomerIds = new[] { " " }; slot.trades.current.visitors = 1; break;
                case "unearned-revenue": slot.trades.current.revenue = 1; break;
            }
            string json = problem == "missing"
                ? "{\"formatVersion\":" + SaveStore.CurrentFormatVersion + ",\"game\":" + JsonUtility.ToJson(slot.game) +
                    ",\"cycle\":" + JsonUtility.ToJson(slot.cycle) + ",\"reputation\":" + JsonUtility.ToJson(slot.reputation) +
                    ",\"layout\":" + JsonUtility.ToJson(slot.layout) +
                    ",\"slapKey\":\"F\",\"humorVolume\":1,\"reducedGags\":false}"
                : JsonUtility.ToJson(slot);
            File.WriteAllText(PathFor(), json);
            byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out var error), Is.False);
            Assert.That(restored, Is.Null); Assert.That(loaded, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase("null")]
        [TestCase("version")]
        [TestCase("wrong-day")]
        public void InvalidReportWriteDoesNotTouchPrimaryBackupOrCreateOutputDirectory(string problem)
        {
            StockAndOpen(); var slot = Slot(); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()); byte[] backup = File.ReadAllBytes(PathFor() + ".bak");
            if (problem == "null") slot.trades = null;
            if (problem == "version") slot.trades.version = 99;
            if (problem == "wrong-day") slot.trades.current.dayNumber++;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
            Assert.That(File.Exists(PathFor() + ".tmp"), Is.False);
            string absent = Path.Combine(directory, "not-created", "slot.json");
            Assert.That(SaveStore.Write(absent, slot, out _), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(absent)), Is.False);
        }

        [Test]
        public void CorruptPrimaryDoesNotSilentlyLoadVersionTwoArchive()
        {
            byte[] legacy = WriteVersionTwo(ShopDayPhase.Night);
            File.WriteAllBytes(PathFor() + ".v2.bak", legacy);
            File.WriteAllText(PathFor(), "{broken primary");
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out _), Is.False);
            Assert.That(restored, Is.Null); Assert.That(slot, Is.Null);
            Assert.That(File.ReadAllText(PathFor()), Is.EqualTo("{broken primary"));
            Assert.That(File.ReadAllBytes(PathFor() + ".v2.bak"), Is.EqualTo(legacy));
        }

        string PathFor() => Path.Combine(directory, "slot.json");
        // Formats 1-3 carried game version 1, which had no price tags.
        SaveData LegacyGame() { var game = model.CaptureSave(); game.version = 1; game.prices = null; return game; }
        int StockedCount() => (model.GetTank("tank-a").HasValue ? 1 : 0) + (model.GetTank("tank-b").HasValue ? 1 : 0);
        SlotData Slot() => new SlotData
        {
            game = model.CaptureSave(), cycle = cycle.CaptureSave(), trades = reports.CaptureSave(),
            sensitivity = 1.6f, volume = 0.38f
        };
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
        void Reload()
        {
            Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error);
            Assert.That(ShopDayCycle.TryRestore(slot.cycle, out cycle), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out reports), Is.True);
        }
        void StockAndOpen()
        {
            Produce("pondlet", "tank-a"); Produce("bubblebelly", "tank-b");
            Assert.That(cycle.OpenShop(), Is.True); Assert.That(reports.BeginDay(cycle.DayNumber, StockedCount()), Is.True);
        }
        void Sell(string tank, string buyer)
        {
            Assert.That(reports.BeginVisit(buyer), Is.True); Ok(model.ReserveTank(tank, buyer));
            var reservation = model.GetReservation(tank).Value;
            Ok(model.SellReservedTank(tank, buyer, reservation.SpecimenId));
            Assert.That(reports.ResolveVisit(buyer, CustomerOutcome.Sold, reservation.Price), Is.True);
        }
        void FinishDay()
        {
            Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(ShopDayCycle.DefaultClosingSeconds);
            Assert.That(reports.FinishDay(StockedCount()), Is.True);
        }
        byte[] WriteVersionTwo(ShopDayPhase phase)
        {
            Produce("pondlet", "tank-a"); Ok(model.SellFromTank("tank-a"));
            Produce("bubblebelly", "tank-a"); Produce("ribbonfin", "tank-b");
            Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(12.5f);
            if (phase != ShopDayPhase.Open) Assert.That(cycle.RequestClose(), Is.True);
            if (phase == ShopDayPhase.Night) cycle.Tick(ShopDayCycle.DefaultClosingSeconds);
            else if (phase == ShopDayPhase.Closing) cycle.Tick(0.5f);
            string json = JsonUtility.ToJson(new VersionTwoSlot { game = LegacyGame(), cycle = cycle.CaptureSave() }, true) + "\n";
            File.WriteAllText(PathFor(), json);
            return File.ReadAllBytes(PathFor());
        }
        void Produce(string species, string tank)
        {
            model.Tick(100f); var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Ok(model.TransferToTank(tank));
        }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
