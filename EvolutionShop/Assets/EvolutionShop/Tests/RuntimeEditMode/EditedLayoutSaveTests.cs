using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class EditedLayoutSaveTests
    {
        const string Prefix = "EvolutionShop-EditedLayoutSaveTests-";
        string directory;
        GameConfig config;
        SlotData slot;
        GameModel model;
        ShopLayout layout;
        ShopDayCycle cycle;
        ShopTradeLedger reports;
        ShopReputation reputation;
        byte[] legacyBytes;
        string PathFor() => Path.Combine(directory, "slot.json");
        string FixturePath() => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V10DecorStock.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); config = GameConfig.CreateDefault(); legacyBytes = null;
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

        [Test] public void DenseCumulativeEditsAcrossReloadsKeepStockCashIdsAppealAndTwoDayProgressConsistent()
        {
            // Seed funds only once as a technical fixture, then use real domain purchase transactions.
            var game = new GameModel(config).CaptureSave(); game.currency = 1000;
            Assert.That(GameModel.TryRestore(config, game, out model, out _), Is.True);
            layout = new ShopLayout(); cycle = new ShopDayCycle(); reports = new ShopTradeLedger();
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = 1, points = 38 }, out reputation), Is.True);
            slot = new SlotData { sensitivity = 1.4f, volume = .3f, slapKey = "G", humorVolume = .4f, reducedGags = true };

            Buy("small-display", 3, -5, 0, "table-1");
            Assert.That(layout.TryExpand(model, cycle.Phase).Success, Is.True);
            Buy("large-display", -18, -3, 0, "table-2");
            Assert.That(layout.TryExpand(model, cycle.Phase).Success, Is.True);
            Buy("large-display", -26, -3, 0, "table-3");
            Buy("work-table", -4, -1, 0, "table-4"); Buy("work-table", 4, -1, 0, "table-5");
            Buy("work-table", 9, 4, 1, "table-6");
            string[] definitions = { "coral-planter", "coral-planter", "coral-planter", "bubble-lamp", "silly-statue", "bubble-lamp" };
            int[] xs = { 10, -12, -28, -23, -20, -17 }, zs = { 8, 8, -8, -8, -8, -8 };
            for (int i = 0; i < 6; i++) Buy(definitions[i], xs[i], zs[i], 0, "table-" + (i + 7));
            Produce("pondlet"); Ok(model.TransferToTank("table-1-slot-1"));
            Produce("bubblebelly"); Ok(model.TransferToTank("table-3-slot-2"));
            Ok(model.SetPrice("pondlet", 8)); Ok(model.SetPrice("bubblebelly", 12));
            Produce("ribbonfin"); // Ready lab output must remain untouched through all furniture edits.
            string first = model.GetTank("table-1-slot-1").Value.Id, second = model.GetTank("table-3-slot-2").Value.Id;
            string ready = model.Incubator.SpecimenId;
            SaveReload(552, 30, 7, 13, 12);

            string labTable = "table-6";
            for (int round = 0; round < 3; round++)
            {
                int cash = 552 - round * 6;
                for (int i = 0; i < 6; i++) Assert.That(layout.TryStore(model, cycle.Phase, "table-" + (i + 7)).Success, Is.True);
                SaveReload(cash, 0, 7, 13 + round, 12);
                Assert.That(layout.StoredCount, Is.EqualTo(6)); Assert.That(layout.DecorCount, Is.EqualTo(6));
                Assert.That(layout.TryCommit(model, cycle.Phase, "table-3", -26, -7, 2, out _).Success, Is.True);
                SaveReload(cash, 0, 7, 13 + round, 12);
                AssertStock(first, second, ready);
                Assert.That(layout.TryCommit(model, cycle.Phase, "table-3", -26, -3, 0, out _).Success, Is.True);
                for (int i = 0; i < 6; i++)
                    Assert.That(layout.TryCommit(model, cycle.Phase, "table-" + (i + 7), xs[i], zs[i], 0, out _).Success, Is.True);
                SaveReload(cash, 30, 7, 13 + round, 12);

                Assert.That(layout.TryStore(model, cycle.Phase, labTable).Success, Is.True);
                Assert.That(layout.TrySell(model, cycle.Phase, labTable).Success, Is.True);
                SaveReload(cash + 6, 30, 7, 13 + round, 11);
                Assert.That(layout.TrySell(model, cycle.Phase, labTable).Success, Is.False);
                labTable = "table-" + (13 + round);
                Buy("work-table", 9, 4, 1, labTable);
                SaveReload(cash - 6, 30, 7, 14 + round, 12);
                AssertStock(first, second, ready);
            }

            Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(17);
            Assert.That(reports.BeginDay(1, 2), Is.True);
            BeginOrder("reload-a", "table-1-slot-1"); BeginOrder("reload-b", "table-3-slot-2");
            Assert.That(layout.TryCommit(model, cycle.Phase, "table-3", -26, -7, 2, out _).Success, Is.False);
            Assert.That(layout.TryStore(model, cycle.Phase, "table-4").Success, Is.False);
            Assert.That(layout.TrySell(model, cycle.Phase, "table-4").Success, Is.False);
            SaveReload(534, 30, 7, 16, 12);
            Assert.That(cycle.Phase, Is.EqualTo(ShopDayPhase.Open)); Assert.That(cycle.SecondsRemaining, Is.EqualTo(463));
            Assert.That(reports.PendingCount, Is.EqualTo(2)); Assert.That(model.GetReservation("table-1-slot-1"), Is.Null);
            Assert.That(model.GetReservation("table-3-slot-2"), Is.Null);
            Assert.That(reports.CancelPendingForReload(), Is.True); Assert.That(reports.CancelPendingForReload(), Is.False);
            AssertStock(first, second, ready);

            BeginOrder("paid-a", "table-1-slot-1"); BeginOrder("paid-b", "table-3-slot-2");
            Pay("paid-a", "table-1-slot-1", first, 8); Pay("paid-b", "table-3-slot-2", second, 12);
            Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.5f);
            SaveReload(554, 30, 7, 16, 12);
            Assert.That(cycle.Phase, Is.EqualTo(ShopDayPhase.Closing)); Assert.That(cycle.SecondsRemaining, Is.EqualTo(2.5f));
            Assert.That(reputation.Points, Is.EqualTo(42)); Assert.That(reports.Current.interrupted, Is.EqualTo(2));
            Assert.That(reports.Current.sales, Is.EqualTo(2)); Assert.That(reports.Current.revenue, Is.EqualTo(20));
            Assert.That(model.SellReservedTank("table-1-slot-1", "paid-a", first).Success, Is.False);
            Assert.That(reputation.ResolveVisit(reports, "paid-a", CustomerOutcome.Sold, 8), Is.False);

            Assert.That(reports.FinishDay(0), Is.True); cycle.Tick(3);
            Assert.That(cycle.Phase, Is.EqualTo(ShopDayPhase.Night)); Assert.That(cycle.DayNumber, Is.EqualTo(2));
            Assert.That(layout.TryStore(model, cycle.Phase, "table-3").Success, Is.True);
            Assert.That(layout.TrySell(model, cycle.Phase, "table-1").Success, Is.True);
            SaveReload(566, 30, 4, 16, 11);
            Buy("small-display", 3, -5, 0, "table-16");
            SaveReload(542, 30, 5, 17, 12);
            Assert.That(layout.GetItem("table-1"), Is.Null); Assert.That(layout.GetItem("table-3").Value.Stored, Is.True);
            Assert.That(model.TankIds, Does.Contain("table-16-slot-1")); Assert.That(model.TankIds, Does.Not.Contain("table-1-slot-1"));
            Assert.That(model.TankIds, Does.Not.Contain("table-3-slot-2")); Assert.That(model.Incubator.SpecimenId, Is.EqualTo(ready));
            Assert.That(reports.Last.visitors, Is.EqualTo(4)); Assert.That(reports.Last.sales, Is.EqualTo(2));
            Assert.That(reports.Last.interrupted, Is.EqualTo(2)); Assert.That(reports.Last.revenue, Is.EqualTo(20));
            Assert.That(reputation.Points, Is.EqualTo(42)); Assert.That(cycle.DayNumber, Is.EqualTo(2));
        }

        [Test] public void LegacyDerivedEditChainKeepsItsOriginalArchiveWhileSalesCreatePersistentIdGaps()
        {
            legacyBytes = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), legacyBytes);
            Assert.That(SaveStore.Read(PathFor(), config, out model, out slot, out var error), Is.True, error); RestoreAuthorities();
            string originalGame = JsonUtility.ToJson(model.CaptureSave());
            string originalClock = JsonUtility.ToJson(cycle.CaptureSave()), originalReport = JsonUtility.ToJson(reports.CaptureSave());
            SaveReload(104, 30, 2, 7, 6);
            string coral = "table-1";
            for (int round = 0; round < 3; round++)
            {
                int cash = 104 - round * 8;
                Assert.That(layout.TryStore(model, cycle.Phase, coral).Success, Is.True);
                SaveReload(cash, 30, 2, 7 + round, 6); // Another store coral keeps the distinct-type score.
                Assert.That(layout.TrySell(model, cycle.Phase, coral).Success, Is.True);
                SaveReload(cash + 8, 30, 2, 7 + round, 5);
                Assert.That(layout.TrySell(model, cycle.Phase, coral).Success, Is.False);
                coral = "table-" + (7 + round); Buy("coral-planter", -4, -1, 0, coral);
                SaveReload(cash - 8, 30, 2, 8 + round, 6);
            }
            var expected = JsonUtility.FromJson<SaveData>(originalGame); expected.currency = 80;
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expected)));
            Assert.That(JsonUtility.ToJson(cycle.CaptureSave()), Is.EqualTo(originalClock));
            Assert.That(JsonUtility.ToJson(reports.CaptureSave()), Is.EqualTo(originalReport));
            Assert.That(reputation.Points, Is.EqualTo(30)); Assert.That(layout.GetItem("table-1"), Is.Null);
            Assert.That(layout.GetItem("table-9").HasValue, Is.True); Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(10));
            Assert.That(File.ReadAllBytes(FixturePath()), Is.EqualTo(legacyBytes));
            Assert.That(Directory.GetFiles(directory, "*.v*.bak").Length, Is.EqualTo(1));
        }

        void SaveReload(int cash, int appeal, int activeSlots, int nextNumber, int owned)
        {
            Assert.That(model.Currency, Is.EqualTo(cash)); Assert.That(layout.AppealPoints, Is.EqualTo(appeal));
            Assert.That(layout.TankIds.Length, Is.EqualTo(activeSlots)); Assert.That(layout.Items.Count, Is.EqualTo(owned));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(nextNumber));
            slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); slot.cycle = cycle.CaptureSave();
            slot.trades = reports.CaptureSave(); slot.reputation = reputation.CaptureSave();
            string expected = JsonUtility.ToJson(slot);
            byte[] previous = File.Exists(PathFor()) ? File.ReadAllBytes(PathFor()) : null;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            if (previous != null) Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(previous));
            if (legacyBytes != null) Assert.That(File.ReadAllBytes(PathFor() + ".v10.bak"), Is.EqualTo(legacyBytes));
            byte[] written = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out model, out slot, out error), Is.True, error);
            Assert.That(JsonUtility.ToJson(slot), Is.EqualTo(expected));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(written), "Read must not perform another transaction or rotate a backup.");
            RestoreAuthorities();
            Assert.That(model.Currency, Is.EqualTo(cash)); Assert.That(layout.AppealPoints, Is.EqualTo(appeal));
            Assert.That(model.TankIds, Is.EqualTo(layout.TankIds)); Assert.That(model.TankIds.Length, Is.EqualTo(activeSlots));
        }
        void RestoreAuthorities()
        {
            Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(ShopDayCycle.TryRestore(slot.cycle, out cycle), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out reports), Is.True);
            Assert.That(ShopReputation.TryRestore(slot.reputation, out reputation), Is.True);
        }
        void Buy(string definition, int x, int z, int turns, string expectedId)
        {
            var result = layout.TryCommit(model, cycle.Phase, definition, null, x, z, turns, out var id);
            Assert.That(result.Success, Is.True, result.Failure.ToString()); Assert.That(id, Is.EqualTo(expectedId));
        }
        void BeginOrder(string customer, string tank)
        { Assert.That(reports.BeginVisit(customer), Is.True); Ok(model.ReserveTank(tank, customer)); }
        void Pay(string customer, string tank, string specimen, int price)
        {
            Assert.That(model.GetReservation(tank).Value.Price, Is.EqualTo(price));
            Ok(model.SellReservedTank(tank, customer, specimen));
            Assert.That(reputation.ResolveVisit(reports, customer, CustomerOutcome.Sold, price), Is.True);
        }
        void AssertStock(string first, string second, string ready)
        {
            Assert.That(model.GetTank("table-1-slot-1").Value.Id, Is.EqualTo(first));
            Assert.That(model.GetTank("table-3-slot-2").Value.Id, Is.EqualTo(second));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(ready));
        }
        void Produce(string species)
        {
            model.Tick(100); var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds);
        }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
