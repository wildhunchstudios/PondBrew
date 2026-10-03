using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class CustomerQueueSaveTests
    {
        const string DirectoryPrefix = "EvolutionShop-CustomerQueueSaveTests-";
        static readonly string[] Tanks = { "tank-a", "tank-b" };
        string directory;
        GameConfig config;
        GameModel model;
        ShopDayCycle cycle;
        ShopTradeLedger reports;
        CustomerArchetype[] catalog;

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
            model = new GameModel(config);
            cycle = new ShopDayCycle();
            reports = new ShopTradeLedger();
            catalog = CustomerArchetypes.CreateDefault();
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
        public void TwoQueuedOrdersSurviveAsTwoInterruptedVisitsWithStockAndMoneyKept()
        {
            StockAndOpen();
            Sell("tank-a", "earlier-buyer");
            Produce("pondlet", "tank-a");
            Assert.That(reports.RecordRestock(), Is.True);
            var first = new CustomerVisit(model, Tanks, null, CustomerArchetypes.Find(catalog, "premium"));
            var second = new CustomerVisit(model, Tanks, null, CustomerArchetypes.Find(catalog, "casual"), t => t == first.TargetTankId);
            Assert.That(reports.BeginVisit(first.Id), Is.True);
            Assert.That(reports.BeginVisit(second.Id), Is.True);
            Queue(first); Queue(second);
            string a = model.GetTank("tank-a").Value.Id, b = model.GetTank("tank-b").Value.Id;
            Write(Slot());
            byte[] original = File.ReadAllBytes(PathFor());

            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original), "Read must not rewrite the slot.");
            Assert.That(slot.formatVersion, Is.EqualTo(5));
            Assert.That(slot.trades.version, Is.EqualTo(3));
            Assert.That(slot.trades.pendingCustomerIds, Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(restored.GetReservation("tank-a"), Is.Null);
            Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(a));
            Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(b));
            Assert.That(restored.Currency, Is.EqualTo(6));
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var loaded), Is.True);
            Assert.That(loaded.CancelPendingForReload(), Is.True);
            Assert.That(loaded.Current.interrupted, Is.EqualTo(2));
            Assert.That(loaded.Current.visitors, Is.EqualTo(3));
            Assert.That(loaded.Current.sales, Is.EqualTo(1));
            Assert.That(loaded.Current.restocked, Is.EqualTo(1));
            Assert.That(restored.SellReservedTank("tank-a", first.Id, first.SpecimenId).Success, Is.False);
            Assert.That(restored.SellReservedTank("tank-b", second.Id, second.SpecimenId).Success, Is.False);
            Assert.That(restored.Currency, Is.EqualTo(6));
        }

        [Test]
        public void FormatFourSlotMigratesInMemoryAndArchivesItsExactBytesOnFirstReplacement()
        {
            StockAndOpen();
            Ok(model.SetPrice("pondlet", 8));
            Assert.That(reports.BeginVisit("r03-1-visitor"), Is.True);
            byte[] original = WriteFormatFour(pendingId: "r03-1-visitor");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var slot, out var error), Is.True, error);
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1), "Read must not create temp or backup files.");
                Assert.That(slot.formatVersion, Is.EqualTo(5));
                Assert.That(slot.trades.version, Is.EqualTo(3));
                Assert.That(slot.trades.pendingCustomerIds, Is.EqualTo(new[] { "r03-1-visitor" }));
                Assert.That(restored.GetPrice("pondlet").Value.Price, Is.EqualTo(8));
            }
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var migrated, out var readError), Is.True, readError);
            Write(migrated);
            byte[] firstCurrent = File.ReadAllBytes(PathFor());
            Assert.That(File.ReadAllBytes(PathFor() + ".v4.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            migrated.volume = .7f;
            Write(migrated);
            Assert.That(File.ReadAllBytes(PathFor() + ".v4.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(firstCurrent));
            Assert.That(File.Exists(PathFor() + ".v3.bak"), Is.False);
        }

        [Test]
        public void ExistingVersionFourArchiveIsNeverOverwrittenAndAFailedArchiveKeepsThePrimary()
        {
            StockAndOpen();
            byte[] original = WriteFormatFour(pendingId: null);
            byte[] preserved = { 4, 4, 4 };
            File.WriteAllBytes(PathFor() + ".v4.bak", preserved);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v4.bak"), Is.EqualTo(preserved));

            File.WriteAllBytes(PathFor(), original);
            File.Delete(PathFor() + ".v4.bak");
            File.Delete(PathFor() + ".bak");
            Directory.CreateDirectory(PathFor() + ".v4.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        [Test]
        public void FormatFiveVersionsArePaired()
        {
            Assert.That(SaveStore.CurrentFormatVersion, Is.EqualTo(5));
            Assert.That(SaveStore.GameVersionFor(5), Is.EqualTo(GameModel.SaveVersion));
            Assert.That(SaveStore.ReportVersionFor(5), Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(SaveStore.ReportVersionFor(4), Is.EqualTo(2));
            Assert.That(SaveStore.ReportVersionFor(3), Is.EqualTo(1));
            Assert.That(SaveStore.GameVersionFor(3), Is.EqualTo(1));
        }

        string PathFor() => Path.Combine(directory, "slot.json");
        SlotData Slot() => new SlotData
        {
            game = model.CaptureSave(), cycle = cycle.CaptureSave(), trades = reports.CaptureSave(),
            sensitivity = 1.1f, volume = .45f
        };
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);

        /// <summary>The exact shape the R03.1 player wrote: envelope 4, game 2 with tags, report 2 with one pending field.</summary>
        byte[] WriteFormatFour(string pendingId)
        {
            var trades = reports.CaptureSave();
            trades.version = 2;
            trades.pendingCustomerIds = null;
            trades.pendingCustomerId = pendingId;
            string json = "{\n    \"formatVersion\": 4,\n    \"game\": " + JsonUtility.ToJson(model.CaptureSave(), true) +
                ",\n    \"cycle\": " + JsonUtility.ToJson(cycle.CaptureSave(), true) +
                ",\n    \"trades\": " + JsonUtility.ToJson(trades, true) +
                ",\n    \"sensitivity\": 1.1,\n    \"volume\": 0.45\n}";
            File.WriteAllText(PathFor(), json);
            return File.ReadAllBytes(PathFor());
        }

        void StockAndOpen()
        {
            Produce("pondlet", "tank-a");
            Produce("bubblebelly", "tank-b");
            Assert.That(cycle.OpenShop(), Is.True);
            cycle.Tick(3.5f);
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
            visit.Advance(.1f, true, ShopDayPhase.Open);
            visit.Advance(4f, false, ShopDayPhase.Open);
            visit.Advance(.1f, true, ShopDayPhase.Open);
            Assert.That(visit.Phase, Is.EqualTo(CustomerPhase.Queued));
        }

        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
