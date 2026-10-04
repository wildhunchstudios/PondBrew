using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FurnitureStorageSaveTests
    {
        const string Prefix = "EvolutionShop-FurnitureStorageSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");
        string FixturePath() => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V10DecorStock.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); config = GameConfig.CreateDefault();
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

        [Test] public void ExactFormatTenDecorFixtureMigratesReadOnlyWithoutChangingProgressOrGrantingStorage()
        {
            byte[] original = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), original);
            var expected = JsonUtility.FromJson<SlotData>(File.ReadAllText(FixturePath()));
            Assert.That(expected.formatVersion, Is.EqualTo(10)); Assert.That(expected.layout.version, Is.EqualTo(3));
            var loaded = Read(out var model); AssertSameProgress(expected, loaded, model);
            Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(loaded.layout.version, Is.EqualTo(ShopLayout.SaveVersion));
            Assert.That(model.Currency, Is.EqualTo(104)); Assert.That(model.TankIds.Length, Is.EqualTo(2));
            Assert.That(loaded.layout.items.Length, Is.EqualTo(6)); Assert.That(loaded.layout.nextItemNumber, Is.EqualTo(7));
            foreach (var item in loaded.layout.items) Assert.That(item.stored, Is.False);
            Assert.That(ShopLayout.TryRestore(loaded.layout, out var layout), Is.True); Assert.That(layout.AppealPoints, Is.EqualTo(30));
            Assert.That(loaded.reputation.points, Is.EqualTo(30)); Assert.That(loaded.trades.last.closed, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(FixturePath()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void FormatTenPendingMigrationPreservesPaidProgressStockAndSettingsUntilSessionReconciles(ShopDayPhase phase)
        {
            var old = JsonUtility.FromJson<SlotData>(File.ReadAllText(FixturePath()));
            Assert.That(ShopLayout.TryRestore(old.layout, out var layout), Is.True);
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), old.game, out var model, out _), Is.True);
            Ok(model.BuyUpgrade()); Produce(model, "bubblebelly");
            Assert.That(ShopDayCycle.TryRestore(old.cycle, out var cycle), Is.True); Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(17);
            Assert.That(ShopTradeLedger.TryRestore(old.trades, old.cycle, out var reports), Is.True);
            Assert.That(reports.BeginDay(cycle.DayNumber, 2), Is.True);
            foreach (string id in new[] { "tank-a", "tank-b" })
            { Assert.That(reports.BeginVisit(id), Is.True); Ok(model.ReserveTank(id, id)); }
            if (phase == ShopDayPhase.Closing) { Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.5f); }
            old.game = model.CaptureSave(); old.game = LegacyGameSaveFixture.AsVersion(old.game, 2); old.cycle = cycle.CaptureSave(); old.trades = reports.CaptureSave();
            old.sensitivity = 1.6f; old.volume = .27f; old.slapKey = "G"; old.humorVolume = .4f; old.reducedGags = true;
            File.WriteAllText(PathFor(), JsonUtility.ToJson(old, true)); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored); AssertSameProgress(old, loaded, restored);
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var restoredReports), Is.True);
            Assert.That(restoredReports.PendingCount, Is.EqualTo(2)); Assert.That(restoredReports.CancelPendingForReload(), Is.True);
            Assert.That(restoredReports.CancelPendingForReload(), Is.False); Assert.That(restoredReports.Current.interrupted, Is.EqualTo(2));
            Assert.That(restoredReports.Current.revenue, Is.Zero); Assert.That(restored.Currency, Is.EqualTo(80));
            foreach (string id in new[] { "tank-a", "tank-b" })
            { Assert.That(restored.GetReservation(id), Is.Null); Assert.That(restored.GetTank(id).Value.Id, Is.EqualTo(model.GetTank(id).Value.Id)); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void FirstFormatTenWritePreservesExactArchiveAcrossStorageAndSubsequentWrites()
        {
            byte[] original = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), original);
            var slot = Read(out var model); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v10.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] first = File.ReadAllBytes(PathFor());
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-3").Success, Is.True);
            slot.layout = layout.CaptureSave(); slot.game = model.CaptureSave(); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v10.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(first));
            Assert.That(ShopLayout.TryRestore(Read(out _).layout, out layout), Is.True); Assert.That(layout.GetItem("table-3").Value.Stored, Is.True);
        }

        [Test] public void ExistingFormatTenArchiveIsImmutable()
        {
            File.Copy(FixturePath(), PathFor()); byte[] archive = { 10, 4, 3 }; File.WriteAllBytes(PathFor() + ".v10.bak", archive);
            Write(Read(out _)); Assert.That(File.ReadAllBytes(PathFor() + ".v10.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedFormatTenArchivePreservesPrimaryAndRotatingBackup()
        {
            byte[] original = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), original);
            byte[] backup = { 10, 10 }; File.WriteAllBytes(PathFor() + ".bak", backup); var slot = Read(out _);
            Directory.CreateDirectory(PathFor() + ".v10.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void MixedStoredAndPlacedOwnershipRoundTripsWithOnlyActiveSlotsAndAppeal(ShopDayPhase phase)
        {
            var slot = FreshSlot(MixedLayout(), phase, out var model);
            Produce(model, "pondlet"); Ok(model.TransferToTank("table-1-slot-1")); Produce(model, "ribbonfin");
            slot.game = model.CaptureSave(); Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored); AssertSameProgress(slot, loaded, restored);
            Assert.That(ShopLayout.TryRestore(loaded.layout, out var layout), Is.True);
            Assert.That(layout.Items.Count, Is.EqualTo(4)); Assert.That(layout.TankIds.Length, Is.EqualTo(3));
            Assert.That(layout.GetSlotIds("table-2"), Is.EqualTo(new[] { "table-2-slot-1", "table-2-slot-2" }));
            Assert.That(layout.DecorCount, Is.EqualTo(2)); Assert.That(layout.AppealPoints, Is.EqualTo(10));
            Assert.That(restored.GetTank("table-1-slot-1").Value.Id, Is.EqualTo(model.GetTank("table-1-slot-1").Value.Id));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [Test] public void StoreRedeployAndSellPersistOwnershipGapsWithoutReusingIdsOrRepeatingRefund()
        {
            var slot = FreshSlot(MixedLayout(), ShopDayPhase.Night, out var model);
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-3").Success, Is.True);
            slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); Write(slot);
            slot = Read(out model); Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(layout.TankIds.Length, Is.EqualTo(2)); Assert.That(layout.AppealPoints, Is.Zero);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 3, -5, 0, out var restoredId).Success, Is.True);
            Assert.That(restoredId, Is.EqualTo("table-1")); Assert.That(model.Currency, Is.EqualTo(100));
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-2").Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(120)); Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(5));
            slot.layout = layout.CaptureSave(); slot.game = model.CaptureSave(); Write(slot);
            slot = Read(out model); Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-2").Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(120)); Assert.That(layout.GetItem("table-2"), Is.Null);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, null, -4, -1, 0, out var newId).Success, Is.True);
            Assert.That(newId, Is.EqualTo("table-5")); Assert.That(model.Currency, Is.EqualTo(108));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(6));
            slot.layout = layout.CaptureSave(); slot.game = model.CaptureSave(); Write(slot);
            var loaded = Read(out model); AssertSameProgress(slot, loaded, model);
        }

        [Test] public void SellingLastItemDoesNotResetItsPersistedIdHighWaterMark()
        {
            var slot = FreshSlot(SingleItem("work-table", -4, -1), ShopDayPhase.Night, out var model);
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); Write(slot);
            slot = Read(out model); Assert.That(slot.layout.items, Is.Empty); Assert.That(slot.layout.nextItemNumber, Is.EqualTo(2));
            Assert.That(model.Currency, Is.EqualTo(106)); Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, null, -4, -1, 0, out var id).Success, Is.True);
            Assert.That(id, Is.EqualTo("table-2")); Assert.That(model.Currency, Is.EqualTo(94));
        }

        [Test] public void StockedMovePreservesSlotAndSpecimenAcrossFileReloadWhileRemovalAndReservedMoveRemainBlocked()
        {
            var save = SingleItem("large-display", -26, -3); save.storeTier = 2;
            var slot = FreshSlot(save, ShopDayPhase.Night, out var model);
            Produce(model, "pondlet"); Ok(model.TransferToTank("table-1-slot-1"));
            string specimen = model.GetTank("table-1-slot-1").Value.Id;
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.False);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, "table-1").Success, Is.False);
            Ok(model.ReserveTank("table-1-slot-1", "live-owner"));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -26, -7, 2, out _).Success, Is.False);
            Assert.That(model.GetReservation("table-1-slot-1").Value.CustomerId, Is.EqualTo("live-owner"));
            Ok(model.ReleaseReservation("table-1-slot-1", "live-owner"));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -26, -7, 2, out _).Success, Is.True);
            Assert.That(model.Currency, Is.EqualTo(100)); slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); Write(slot);
            var loaded = Read(out var restored); AssertSameProgress(slot, loaded, restored);
            Assert.That(restored.GetTank("table-1-slot-1").Value.Id, Is.EqualTo(specimen));
            Assert.That(loaded.layout.items[0].id, Is.EqualTo("table-1")); Assert.That(loaded.layout.items[0].z, Is.EqualTo(-7));
        }

        [TestCase("stored-slot")] [TestCase("sold-slot")] [TestCase("missing-active-slot")] [TestCase("stored-stock")]
        public void OwnershipMismatchCannotHideStockOrCreateCapacityDuringReadOrWrite(string problem)
        {
            var slot = FreshSlot(MixedLayout(), ShopDayPhase.Night, out var model);
            Produce(model, "pondlet"); Ok(model.TransferToTank("table-1-slot-1")); slot.game = model.CaptureSave(); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            var tanks = new List<TankSave>(slot.game.tanks);
            if (problem == "stored-slot") tanks.Add(new TankSave { tankId = "table-2-slot-1" });
            else if (problem == "sold-slot") tanks.Add(new TankSave { tankId = "table-99-slot-1" });
            else if (problem == "missing-active-slot") tanks.RemoveAll(t => t.tankId == "table-1-slot-1");
            else slot.layout.items[0].stored = true;
            slot.game.tanks = tanks.ToArray(); RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("number-zero")] [TestCase("number-one")]
        [TestCase("string-false")] [TestCase("string-true")] [TestCase("array")] [TestCase("object")]
        [TestCase("duplicate")] [TestCase("escaped-duplicate")] [TestCase("nested-decoy")] [TestCase("string-decoy")]
        public void CurrentEnvelopeRequiresOneDirectExplicitBooleanStorageFlagPerItem(string problem)
        {
            var slot = FreshSlot(SingleItem("work-table", -4, -1), ShopDayPhase.Night, out _);
            Write(slot); Read(out _); string item = JsonUtility.ToJson(slot.layout.items[0]);
            Assert.That(item, Does.Contain("\"stored\":false"));
            string replacement = problem == "null" ? "null" : problem == "number-zero" ? "0" : problem == "number-one" ? "1" :
                problem == "string-false" ? "\"false\"" : problem == "string-true" ? "\"true\"" : problem == "array" ? "[]" : "{}";
            if (problem == "duplicate") item = item.Replace("\"stored\":false", "\"stored\":false,\"stored\":false");
            else if (problem == "escaped-duplicate") item = item.Replace("\"stored\":false", "\"stored\":false,\"\\u0073tored\":false");
            else if (problem == "missing" || problem == "nested-decoy" || problem == "string-decoy")
            {
                item = item.Replace(",\"stored\":false", "").Replace("\"stored\":false,", "");
                if (problem == "nested-decoy") item = item.Insert(1, "\"metadata\":{\"stored\":false},");
                if (problem == "string-decoy") item = item.Insert(1, "\"metadata\":\"\\\"stored\\\":false\",");
            }
            else item = item.Replace("\"stored\":false", "\"stored\":" + replacement);
            string json = JsonUtility.ToJson(slot).Replace(JsonUtility.ToJson(slot.layout.items[0]), item); RejectRead(json);
        }

        [TestCase(false)] [TestCase(true)]
        public void EscapedStorageKeyAndUnrelatedNestedMetadataHaveValidPositiveControls(bool stored)
        {
            var save = SingleItem("work-table", -4, -1); save.items[0].stored = stored;
            var slot = FreshSlot(save, ShopDayPhase.Night, out _);
            string json = JsonUtility.ToJson(slot).Replace("\"stored\":", "\"\\u0073tored\":")
                .Replace("\"definitionId\":", "\"metadata\":{\"stored\":null,\"items\":[\"escaped \\\" } ]\"]},\"definitionId\":");
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out _); Assert.That(loaded.layout.items[0].stored, Is.EqualTo(stored));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [TestCase(8, 1)] [TestCase(9, 2)] [TestCase(10, 3)]
        public void LegacyLayoutsAcceptAbsentStorageButCannotAcquireStoredOwnership(int format, int version)
        {
            var slot = FreshSlot(SingleItem("work-table", -4, -1), ShopDayPhase.Night, out _);
            slot.formatVersion = format; slot.game = LegacyGameSaveFixture.AsVersion(slot.game, 2); slot.layout.version = version;
            string json = JsonUtility.ToJson(slot).Replace(",\"stored\":false", "").Replace("\"stored\":false,", "");
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out _); Assert.That(loaded.layout.items[0].stored, Is.False);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            slot.layout.items[0].stored = true; RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase(8, 4)] [TestCase(9, 4)] [TestCase(10, 4)] [TestCase(11, 1)] [TestCase(11, 2)] [TestCase(11, 3)]
        public void EnvelopeLayoutMappingsRemainFrozen(int format, int version)
        {
            var slot = FreshSlot(new ShopLayout().CaptureSave(), ShopDayPhase.Night, out _);
            Write(slot); Read(out _); slot.formatVersion = format; slot.game = LegacyGameSaveFixture.AsVersion(slot.game, 2); slot.layout.version = version;
            RejectRead(JsonUtility.ToJson(slot));
        }

        static LayoutItemSave Item(int number, string definition, int x, int z, bool stored = false) => new LayoutItemSave
            { id = "table-" + number, definitionId = definition, x = x, z = z, quarterTurns = 0, stored = stored };
        static LayoutSave SingleItem(string definition, int x, int z) => new LayoutSave
            { version = ShopLayout.SaveVersion, nextItemNumber = 2, items = new[] { Item(1, definition, x, z) } };
        static LayoutSave MixedLayout() => new LayoutSave { version = ShopLayout.SaveVersion, storeTier = 1, nextItemNumber = 5, items = new[]
            { Item(1, "small-display", 3, -5), Item(2, "large-display", -18, -3, true), Item(3, "coral-planter", -4, -1), Item(4, "bubble-lamp", 4, -1, true) } };
        SlotData FreshSlot(LayoutSave save, ShopDayPhase phase, out GameModel model)
        {
            if (save.version == ShopLayout.SaveVersion && save.facilities == null)
                save.facilities = new ShopLayout().CaptureSave().facilities;
            Assert.That(ShopLayout.TryRestore(save, out var layout), Is.True, "Tests start from valid geometry.");
            var game = new GameModel(layout.WithTankIds(config)).CaptureSave(); game.currency = 100;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), game, out model, out _), Is.True);
            var cycle = new ShopDayCycle();
            if (phase != ShopDayPhase.Night)
            { Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(7); if (phase == ShopDayPhase.Closing) { Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.5f); } }
            return new SlotData { game = model.CaptureSave(), layout = layout.CaptureSave(), cycle = cycle.CaptureSave(),
                trades = ShopTradeLedger.LegacyForCycle(cycle.CaptureSave(), 0).CaptureSave(),
                reputation = new ReputationSave { version = 1, points = 38 }, sensitivity = 1.4f, volume = .3f,
                slapKey = "G", humorVolume = .4f, reducedGags = true };
        }
        void AssertSameProgress(SlotData expected, SlotData actual, GameModel after)
        {
            Assert.That(ShopLayout.TryRestore(expected.layout, out var previousLayout), Is.True);
            Assert.That(GameModel.TryRestore(previousLayout.WithTankIds(config), expected.game, out var before, out _), Is.True);
            Assert.That(JsonUtility.ToJson(after.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(before.CaptureSave())));
            Assert.That(JsonUtility.ToJson(actual.cycle), Is.EqualTo(JsonUtility.ToJson(expected.cycle)));
            Assert.That(ShopTradeLedger.TryRestore(expected.trades, expected.cycle, out var previousReports), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(actual.trades, actual.cycle, out var reports), Is.True);
            Assert.That(JsonUtility.ToJson(reports.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(previousReports.CaptureSave())));
            Assert.That(ShopLayout.TryRestore(actual.layout, out var currentLayout), Is.True);
            Assert.That(JsonUtility.ToJson(currentLayout.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(previousLayout.CaptureSave())));
            Assert.That(actual.reputation.points, Is.EqualTo(expected.reputation.points)); Assert.That(actual.sensitivity, Is.EqualTo(expected.sensitivity));
            Assert.That(actual.volume, Is.EqualTo(expected.volume)); Assert.That(actual.slapKey, Is.EqualTo(expected.slapKey));
            Assert.That(actual.humorVolume, Is.EqualTo(expected.humorVolume)); Assert.That(actual.reducedGags, Is.EqualTo(expected.reducedGags));
        }
        void RejectWrite(SlotData slot, byte[] primary, byte[] backup)
        {
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
            string missing = Path.Combine(directory, "not-created", "slot.json");
            Assert.That(SaveStore.Write(missing, slot, out _), Is.False); Assert.That(Directory.Exists(Path.GetDirectoryName(missing)), Is.False);
        }
        void RejectRead(string json)
        {
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.False);
            Assert.That(model, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
        SlotData Read(out GameModel model)
        { Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error); return slot; }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        void Produce(GameModel model, string species)
        {
            model.Tick(100); var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds);
        }
    }
}
