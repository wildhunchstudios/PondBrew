using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class PriceSaveTests
    {
        const string DirectoryPrefix = "EvolutionShop-PriceSaveTests-";
        static readonly string[] Tanks = { "tank-a", "tank-b" };
        string directory;
        GameConfig config;
        GameModel model;
        ShopDayCycle cycle;
        ShopTradeLedger reports;

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

        [Test]
        public void QueuedOrderReloadReleasesTheClaimButKeepsTagsMoneyAndStock()
        {
            StockAndOpen();
            Ok(model.SetPrice("pondlet", 8));
            Ok(model.SetPrice("bubblebelly", 13));
            var visit = new CustomerVisit(model, Tanks);
            Assert.That(reports.BeginVisit(visit.Id), Is.True);
            Queue(visit);
            Assert.That(visit.Price, Is.EqualTo(8));
            string ordered = visit.SpecimenId;
            Ok(model.SetPrice("pondlet", 18));
            Write(Slot());
            byte[] original = File.ReadAllBytes(PathFor());

            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original), "Read must not rewrite the slot.");
            Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(slot.game.version, Is.EqualTo(GameModel.SaveVersion));
            Assert.That(slot.game.prices.Length, Is.EqualTo(2));
            Assert.That(restored.GetPrice("pondlet").Value.Price, Is.EqualTo(18));
            Assert.That(restored.GetPrice("bubblebelly").Value.Price, Is.EqualTo(13));
            Assert.That(restored.GetReservation("tank-a"), Is.Null);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(ordered));
            Assert.That(restored.Currency, Is.Zero);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var loadedReports), Is.True);
            Assert.That(loadedReports.CancelPendingForReload(), Is.True);
            Assert.That(loadedReports.Current.interrupted, Is.EqualTo(1));

            // The released creature now sells at the current tag, not the old order's price.
            Ok(restored.SetPrice("pondlet", 9));
            var replacement = new CustomerVisit(restored, Tanks);
            Queue(replacement);
            Assert.That(replacement.Price, Is.EqualTo(9));
            Assert.That(replacement.Reaction, Is.EqualTo(PriceReaction.Pricey));
            Ok(replacement.Serve(ShopDayPhase.Open));
            Assert.That(restored.Currency, Is.EqualTo(9));
        }

        [Test]
        public void ClosedDayReportKeepsPriceRefusalsThroughFileReload()
        {
            StockAndOpen();
            Ok(model.SetPrice("pondlet", 12));
            Ok(model.SetPrice("bubblebelly", 16));
            var visit = new CustomerVisit(model, Tanks);
            Assert.That(reports.BeginVisit(visit.Id), Is.True);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            Assert.That(visit.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            Assert.That(visit.PriceRejections, Is.EqualTo(2));
            Assert.That(reports.ResolveVisit(visit.Id, visit.Outcome), Is.True);
            Assert.That(cycle.RequestClose(), Is.True);
            cycle.Tick(ShopDayCycle.DefaultClosingSeconds);
            Assert.That(reports.FinishDay(2), Is.True);
            Write(Slot());
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var loaded), Is.True);
            Assert.That(loaded.Last.tooExpensive, Is.EqualTo(1));
            Assert.That(loaded.Last.visitors, Is.EqualTo(1));
            Assert.That(loaded.Last.revenue, Is.Zero);
            Assert.That(loaded.Last.stockedAtClose, Is.EqualTo(2));
            Assert.That(restored.Currency, Is.Zero);
            Assert.That(restored.GetPrice("bubblebelly").Value.Price, Is.EqualTo(16));
        }

        [Test]
        public void FormatThreeSlotMigratesInMemoryAndArchivesItsExactBytesOnFirstReplacement()
        {
            StockAndOpen();
            Sell("tank-a", "day-one-buyer");
            byte[] original = WriteFormatThree();
            string expectedReport = JsonUtility.ToJson(reports.CaptureSave());

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1), "Read must not create temp or backup files.");
                Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
                Assert.That(slot.game.version, Is.EqualTo(GameModel.SaveVersion));
                Assert.That(slot.game.prices, Is.Empty);
                Assert.That(slot.trades.version, Is.EqualTo(ShopTradeLedger.SaveVersion));
                Assert.That(JsonUtility.ToJson(slot.trades), Is.EqualTo(expectedReport));
                Assert.That(restored.Currency, Is.EqualTo(6));
                Assert.That(restored.GetPrice("bubblebelly").Value.Price, Is.EqualTo(10));
                Assert.That(slot.sensitivity, Is.EqualTo(1.4f));
                Assert.That(slot.volume, Is.EqualTo(0.33f));
            }

            Assert.That(SaveStore.Read(PathFor(), config, out _, out var migrated, out var readError), Is.True, readError);
            Write(migrated);
            byte[] firstCurrent = File.ReadAllBytes(PathFor());
            Assert.That(File.ReadAllBytes(PathFor() + ".v3.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            migrated.volume = 0.7f;
            Write(migrated);
            Assert.That(File.ReadAllBytes(PathFor() + ".v3.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(firstCurrent));
            Assert.That(File.Exists(PathFor() + ".v1.bak"), Is.False);
            Assert.That(File.Exists(PathFor() + ".v2.bak"), Is.False);
            Assert.That(SaveStore.Read(PathFor(), config, out var again, out var loaded, out var error2), Is.True, error2);
            Assert.That(again.Currency, Is.EqualTo(6));
            Assert.That(loaded.volume, Is.EqualTo(0.7f));
            Assert.That(loaded.trades.current.sales, Is.EqualTo(1));
        }

        [Test]
        public void ExistingVersionThreeArchiveIsNeverOverwritten()
        {
            StockAndOpen();
            WriteFormatThree();
            byte[] preserved = { 3, 1, 4, 1, 5 };
            File.WriteAllBytes(PathFor() + ".v3.bak", preserved);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Write(slot);
            Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v3.bak"), Is.EqualTo(preserved));
        }

        [Test]
        public void FailedVersionThreeArchiveLeavesTheLegacyPrimaryUntouched()
        {
            StockAndOpen();
            byte[] original = WriteFormatThree();
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Directory.CreateDirectory(PathFor() + ".v3.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        [TestCase("tags-in-format-3")]
        [TestCase("game-1-in-format-5")]
        [TestCase("game-2-in-format-3")]
        [TestCase("report-1-in-format-4")]
        [TestCase("report-2-in-format-3")]
        [TestCase("report-2-in-format-5")]
        [TestCase("report-3-in-format-4")]
        [TestCase("refusals-in-report-1")]
        [TestCase("future-format")]
        public void MismatchedSaveVersionsAreRejectedWithoutChangingTheFile(string problem)
        {
            StockAndOpen();
            Ok(model.SetPrice("pondlet", 8));
            int format = problem.EndsWith("format-3", StringComparison.Ordinal) || problem == "refusals-in-report-1" ? 3
                : problem.EndsWith("format-4", StringComparison.Ordinal) ? 4 : problem == "future-format" ? SaveStore.CurrentFormatVersion + 1 : 5;
            var game = model.CaptureSave(); game.version = 2;
            var trades = reports.CaptureSave();
            // Start from each format's true inner versions, then break exactly one thing.
            if (format == 3) { game.version = 1; game.prices = null; trades.version = 1; }
            if (format == 4) trades.version = 2;
            if (format == 5 || format == 6) trades.version = 3;
            switch (problem)
            {
                case "tags-in-format-3": game.prices = new[] { new PriceSave { speciesId = "pondlet", price = 8 } }; break;
                case "game-1-in-format-5": game.version = 1; game.prices = null; break;
                case "game-2-in-format-3": game = model.CaptureSave(); game.version = 2; break;
                case "report-1-in-format-4": trades.version = 1; break;
                case "report-2-in-format-3": trades.version = 2; break;
                case "report-2-in-format-5": trades.version = 2; break;
                case "report-3-in-format-4": trades.version = 3; break;
                case "refusals-in-report-1":
                    Assert.That(reports.BeginVisit("refuser"), Is.True);
                    Assert.That(reports.ResolveVisit("refuser", CustomerOutcome.TooExpensive), Is.True);
                    trades = reports.CaptureSave();
                    trades.version = 1;
                    break;
            }
            string json = "{\"formatVersion\":" + format + ",\"game\":" + JsonUtility.ToJson(game) +
                ",\"cycle\":" + JsonUtility.ToJson(cycle.CaptureSave()) + ",\"trades\":" + JsonUtility.ToJson(trades) +
                ",\"sensitivity\":1,\"volume\":0.5}";
            File.WriteAllText(PathFor(), json);
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(slot, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()), Is.EqualTo(json));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase("game-1")]
        [TestCase("report-1")]
        [TestCase("missing-report")]
        public void WriteRefusesOlderInnerVersionsAndKeepsThePrimary(string problem)
        {
            StockAndOpen();
            var slot = Slot();
            Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor());
            if (problem == "game-1") { slot.game.version = 1; slot.game.prices = null; }
            if (problem == "report-1") slot.trades.version = 1;
            if (problem == "missing-report") slot.trades = null;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        string PathFor() => Path.Combine(directory, "slot.json");
        SlotData Slot() => new SlotData
        {
            game = model.CaptureSave(), cycle = cycle.CaptureSave(), trades = reports.CaptureSave(),
            sensitivity = 1.4f, volume = 0.33f
        };
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);

        /// <summary>The exact shape the R02 player wrote: envelope 3, game 1 without tags, report 1.</summary>
        byte[] WriteFormatThree()
        {
            var game = model.CaptureSave();
            game.version = 1;
            game.prices = null;
            var trades = reports.CaptureSave();
            trades.version = 1;
            string json = "{\n    \"formatVersion\": 3,\n    \"game\": " + JsonUtility.ToJson(game, true) +
                ",\n    \"cycle\": " + JsonUtility.ToJson(cycle.CaptureSave(), true) +
                ",\n    \"trades\": " + JsonUtility.ToJson(trades, true) +
                ",\n    \"sensitivity\": 1.4,\n    \"volume\": 0.33\n}";
            File.WriteAllText(PathFor(), json);
            return File.ReadAllBytes(PathFor());
        }

        void StockAndOpen()
        {
            Produce("pondlet", "tank-a");
            Produce("bubblebelly", "tank-b");
            Assert.That(cycle.OpenShop(), Is.True);
            cycle.Tick(4.5f);
            Assert.That(reports.BeginDay(cycle.DayNumber, 2), Is.True);
        }

        void Sell(string tank, string buyer)
        {
            Assert.That(reports.BeginVisit(buyer), Is.True);
            Ok(model.ReserveTank(tank, buyer));
            var reservation = model.GetReservation(tank).Value;
            Ok(model.SellReservedTank(tank, buyer, reservation.SpecimenId));
            Assert.That(reports.ResolveVisit(buyer, CustomerOutcome.Sold, reservation.Price), Is.True);
        }

        void Produce(string species, string tank)
        {
            model.Tick(100f);
            var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation());
            model.Tick(config.IncubationSeconds);
            Ok(model.TransferToTank(tank));
        }

        static void Queue(CustomerVisit visit)
        {
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            visit.Advance(2f, false, ShopDayPhase.Open);
            visit.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
        }

        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
