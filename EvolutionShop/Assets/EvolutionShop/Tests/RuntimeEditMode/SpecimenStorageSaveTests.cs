using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class SpecimenStorageSaveTests
    {
        const string Prefix = "EvolutionShop-SpecimenStorageSaveTests-";
        const string First = "table-1-storage-1", Second = "table-1-storage-2";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");
        string Fixture(string name) => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", name);

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

        [TestCase("V12WorkingLab.json", 12, 5, 812, 1)]
        [TestCase("V11StoredFurniture.json", 11, 4, 116, 2)]
        public void ExactLegacyFixturesMigrateReadOnlyWithoutAddingStorageOrChangingProgress(string name, int format, int layoutVersion, int cash, int occupied)
        {
            byte[] original = File.ReadAllBytes(Fixture(name)); File.WriteAllBytes(PathFor(), original);
            var old = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture(name)));
            Assert.That(old.formatVersion, Is.EqualTo(format)); Assert.That(old.layout.version, Is.EqualTo(layoutVersion)); Assert.That(old.game.version, Is.EqualTo(2));
            var slot = Read(out var model); AssertState(old, slot, model);
            Assert.That(slot.formatVersion, Is.EqualTo(13)); Assert.That(slot.layout.version, Is.EqualTo(6)); Assert.That(slot.game.version, Is.EqualTo(3));
            Assert.That(slot.game.storage, Is.Empty); Assert.That(model.StorageIds, Is.Empty);
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True); Assert.That(layout.RackCount, Is.Zero);
            Assert.That(model.Currency, Is.EqualTo(cash)); int count = 0;
            foreach (string id in model.TankIds) if (model.GetTank(id).HasValue) count++;
            Assert.That(count, Is.EqualTo(occupied)); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(Fixture(name)), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void FirstVersionTwelveReplacementArchivesExactBytesOnceAcrossRepeatedWrites()
        {
            byte[] original = File.ReadAllBytes(Fixture("V12WorkingLab.json")); File.WriteAllBytes(PathFor(), original);
            var slot = Read(out _); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v12.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] first = File.ReadAllBytes(PathFor()); slot.volume = .2f; Write(slot); Read(out _);
            Assert.That(File.ReadAllBytes(PathFor() + ".v12.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(first));
        }

        [Test] public void ExistingVersionTwelveArchiveIsImmutable()
        {
            File.Copy(Fixture("V12WorkingLab.json"), PathFor()); byte[] archive = { 12, 5, 2 }; File.WriteAllBytes(PathFor() + ".v12.bak", archive);
            Write(Read(out _)); Assert.That(File.ReadAllBytes(PathFor() + ".v12.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedVersionTwelveArchiveLeavesPrimaryAndRotatingBackupUntouched()
        {
            byte[] original = File.ReadAllBytes(Fixture("V12WorkingLab.json")); File.WriteAllBytes(PathFor(), original);
            byte[] backup = { 5, 12 }; File.WriteAllBytes(PathFor() + ".bak", backup); var slot = Read(out _);
            Directory.CreateDirectory(PathFor() + ".v12.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void StorageAndDisplayInventoryRemainSeparateThroughClockAndPendingReload(ShopDayPhase phase)
        {
            var slot = RackSlot(out var model, out _); Produce(model, "pondlet"); Ok(model.StoreReadyOutput(First));
            Produce(model, "bubblebelly"); Ok(model.TransferToTank("tank-a"));
            string stored = model.GetStorage(First).Value.Id, display = model.GetTank("tank-a").Value.Id;
            var cycle = new ShopDayCycle(); var reports = new ShopTradeLedger();
            if (phase != ShopDayPhase.Night)
            {
                Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(17); Assert.That(reports.BeginDay(1, 1), Is.True);
                Assert.That(reports.BeginVisit("unpaid-storage-reload"), Is.True); Ok(model.ReserveTank("tank-a", "unpaid-storage-reload"));
                if (phase == ShopDayPhase.Closing) { Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.3f); }
            }
            slot.game = model.CaptureSave(); slot.cycle = cycle.CaptureSave(); slot.trades = reports.CaptureSave(); Write(slot);
            byte[] bytes = File.ReadAllBytes(PathFor()); var loaded = Read(out var after); AssertState(slot, loaded, after);
            Assert.That(after.TankIds, Is.EqualTo(new[] { "tank-a", "tank-b" })); Assert.That(after.StorageIds, Is.EqualTo(new[] { First, Second }));
            Assert.That(after.GetStorage(First).Value.Id, Is.EqualTo(stored)); Assert.That(after.GetTank("tank-a").Value.Id, Is.EqualTo(display));
            Assert.That(after.GetReservation("tank-a"), Is.Null); Assert.That(after.Currency, Is.EqualTo(168));
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var restoredReports), Is.True);
            Assert.That(restoredReports.PendingCount, Is.EqualTo(phase == ShopDayPhase.Night ? 0 : 1));
            if (phase != ShopDayPhase.Night)
            { Assert.That(restoredReports.CancelPendingForReload(), Is.True); Assert.That(restoredReports.CancelPendingForReload(), Is.False); Assert.That(restoredReports.Current.interrupted, Is.EqualTo(1)); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }

        [Test] public void StorageDisplayReturnAndPaidReloadRetainExactOwnershipWithoutReplayingMoney()
        {
            var slot = RackSlot(out var model, out _); Produce(model, "pondlet"); string id = model.Incubator.SpecimenId;
            Ok(model.StoreReadyOutput(First)); slot.game = model.CaptureSave(); Write(slot); slot = Read(out model);
            Ok(model.TransferStorageToTank(First, "tank-a", id)); Assert.That(model.GetStorage(First), Is.Null);
            slot.game = model.CaptureSave(); Write(slot); slot = Read(out model);
            Ok(model.ReturnTankToStorage("tank-a", Second, id)); Assert.That(model.GetTank("tank-a"), Is.Null);
            slot.game = model.CaptureSave(); Write(slot); slot = Read(out model);
            Ok(model.TransferStorageToTank(Second, "tank-b", id)); Ok(model.ReserveTank("tank-b", "paid-storage"));
            Ok(model.SellReservedTank("tank-b", "paid-storage", id)); slot.game = model.CaptureSave(); Write(slot);
            var loaded = Read(out model); AssertState(slot, loaded, model); Assert.That(model.Currency, Is.EqualTo(174));
            Assert.That(model.TransferStorageToTank(First, "tank-a", id).Success, Is.False);
            Assert.That(model.TransferStorageToTank(Second, "tank-a", id).Success, Is.False);
            Assert.That(model.SellReservedTank("tank-b", "paid-storage", id).Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(174)); Assert.That(model.GetStorage(First), Is.Null); Assert.That(model.GetStorage(Second), Is.Null);
        }

        [TestCase(IncubatorPhase.Dosing)] [TestCase(IncubatorPhase.Incubating)] [TestCase(IncubatorPhase.Ready)]
        public void RepeatedReadsPreserveStoredSpecimenAndFrozenProductionProgress(IncubatorPhase phase)
        {
            var slot = RackSlot(out var model, out _); Produce(model, "pondlet"); Ok(model.StoreReadyOutput(First));
            model.Tick(100); Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom));
            if (phase != IncubatorPhase.Dosing)
            {
                Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation());
                model.Tick(phase == IncubatorPhase.Ready ? config.IncubationSeconds : 2.75f);
            }
            slot.game = model.CaptureSave(); Write(slot); byte[] bytes = File.ReadAllBytes(PathFor());
            for (int i = 0; i < 3; i++) { var loaded = Read(out var after); AssertState(slot, loaded, after); Assert.That(after.Incubator.Phase, Is.EqualTo(phase)); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }

        [Test] public void EmptyRackStorageAndRedeploymentPreserveOwnedIdAndRestoreOnlyItsEmptySlots()
        {
            var slot = RackSlot(out var model, out var layout);
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, "table-1").Success, Is.True);
            slot.layout = layout.CaptureSave(); slot.game = model.CaptureSave(); Write(slot); slot = Read(out model);
            Assert.That(model.StorageIds, Is.Empty); Assert.That(slot.game.storage, Is.Empty);
            Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True); Assert.That(layout.RackCount, Is.EqualTo(1));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 4, 1, out var id).Success, Is.True);
            Assert.That(id, Is.EqualTo("table-1")); Assert.That(model.Currency, Is.EqualTo(168));
            slot.layout = layout.CaptureSave(); slot.game = model.CaptureSave(); Write(slot); var loaded = Read(out model); AssertState(slot, loaded, model);
            Assert.That(model.StorageIds, Is.EqualTo(new[] { First, Second })); Assert.That(model.GetStorage(First), Is.Null);
        }

        [TestCase("missing-slot")] [TestCase("duplicate-slot")] [TestCase("orphan-slot")]
        [TestCase("storage-in-display-registry")] [TestCase("stored-rack-with-stock")] [TestCase("sold-rack-with-stock")]
        public void InvalidRackOwnershipCannotHideOrCreateSpecimensDuringReadOrWrite(string problem)
        {
            var slot = RackSlot(out var model, out _); Produce(model, "pondlet"); Ok(model.StoreReadyOutput(First));
            slot.game = model.CaptureSave(); Write(slot); Write(slot); byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            switch (problem)
            {
                case "missing-slot": slot.game.storage = new[] { slot.game.storage[0] }; break;
                case "duplicate-slot": slot.game.storage[1].storageId = First; break;
                case "orphan-slot": slot.game.storage[1].storageId = "table-999-storage-1"; break;
                case "storage-in-display-registry": slot.game.tanks[0].tankId = First; break;
                case "stored-rack-with-stock": slot.layout.items[0].stored = true; break;
                case "sold-rack-with-stock": slot.layout.items = Array.Empty<LayoutItemSave>(); break;
            }
            RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("two-storage-slots")] [TestCase("storage-and-display")] [TestCase("storage-and-output")]
        public void DuplicateSpecimenAcrossAuthoritiesRejectsReadAndWrite(string location)
        {
            var slot = RackSlot(out var model, out _); Produce(model, "pondlet"); Ok(model.StoreReadyOutput(First));
            Produce(model, "pondlet"); Ok(model.StoreReadyOutput(Second)); Produce(model, "pondlet"); Ok(model.TransferToTank("tank-a")); Produce(model, "pondlet");
            slot.game = model.CaptureSave(); Write(slot); Write(slot); byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            string duplicate = slot.game.storage[0].specimenId;
            if (location == "two-storage-slots") slot.game.storage[1].specimenId = duplicate;
            else if (location == "storage-and-display") slot.game.tanks[0].specimenId = duplicate;
            else slot.game.incubator.specimenId = duplicate;
            RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("{}")]
        [TestCase("\"array\"")] [TestCase("duplicate")] [TestCase("nested-only")]
        public void CurrentGameRequiresExplicitDirectStorageArrayEvenWithoutRacks(string replacement)
        {
            var slot = Fresh(); Write(slot); Read(out _); string json = JsonUtility.ToJson(slot);
            const string field = "\"storage\":[]";
            if (replacement == "missing") json = RemoveField(json, field);
            else if (replacement == "nested-only") json = json.Replace(field, "\"metadata\":{" + field + "}");
            else if (replacement == "duplicate") json = json.Replace(field, field + ",\"\\u0073torage\":[]");
            else json = json.Replace(field, "\"storage\":" + replacement);
            RejectRead(json);
        }

        [Test] public void CurrentWriteRequiresStorageArrayEvenWhenNoRackIsOwned()
        {
            var slot = Fresh(); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            slot.game.storage = null; RejectWrite(slot, primary, backup);
        }

        [TestCase("missing-storage-id")] [TestCase("missing-specimen-id")] [TestCase("missing-species-id")]
        [TestCase("null-storage-id")] [TestCase("number-specimen-id")] [TestCase("bool-species-id")]
        [TestCase("duplicate-key")] [TestCase("nested-specimen-id")]
        public void CurrentStorageEntriesRequireExplicitUniqueDirectFields(string problem)
        {
            var slot = RackSlot(out _, out _); Write(slot); Read(out _);
            slot.game.storage[0].specimenId = ""; slot.game.storage[0].speciesId = "";
            string original = JsonUtility.ToJson(slot.game.storage[0]); string entry = original;
            string idField = "\"storageId\":\"" + First + "\"";
            // JsonUtility serializes null strings as empty strings; these valid empty slot fields are explicit.
            string specimenField = "\"specimenId\":\"\"", speciesField = "\"speciesId\":\"\"";
            switch (problem)
            {
                case "missing-storage-id": entry = RemoveField(entry, idField); break;
                case "missing-specimen-id": entry = RemoveField(entry, specimenField); break;
                case "missing-species-id": entry = RemoveField(entry, speciesField); break;
                case "null-storage-id": entry = entry.Replace(idField, "\"storageId\":null"); break;
                case "number-specimen-id": entry = entry.Replace(specimenField, "\"specimenId\":0"); break;
                case "bool-species-id": entry = entry.Replace(speciesField, "\"speciesId\":false"); break;
                case "duplicate-key": entry = entry.Replace(idField, idField + ",\"\\u0073torageId\":\"" + First + "\""); break;
                case "nested-specimen-id": entry = entry.Replace(specimenField, "\"metadata\":{" + specimenField + "}"); break;
            }
            Assert.That(entry, Is.Not.EqualTo(original)); RejectRead(JsonUtility.ToJson(slot).Replace(original, entry));
        }

        [Test] public void ExplicitNullEmptySlotFieldsEscapedKeysAndNestedMetadataHaveAValidControl()
        {
            var slot = RackSlot(out _, out _); string json = JsonUtility.ToJson(slot)
                .Replace("\"specimenId\":\"\"", "\"specimenId\":null").Replace("\"speciesId\":\"\"", "\"speciesId\":null")
                .Replace("\"storage\":", "\"\\u0073torage\":").Replace("\"storageId\":", "\"metadata\":{\"storageId\":false,\"storage\":[\"escaped \\\" } ]\"]},\"storageId\":");
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor()); var loaded = Read(out var after); AssertState(slot, loaded, after);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [TestCase(12, 3, 5)] [TestCase(13, 2, 6)] [TestCase(12, 2, 6)] [TestCase(13, 3, 5)]
        public void NewAndPreviousGameLayoutPairingsRemainFrozen(int envelope, int game, int layout)
        {
            var slot = Fresh(); Write(slot); Read(out _); slot.formatVersion = envelope; slot.game.version = game; slot.layout.version = layout;
            RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase(1)] [TestCase(2)]
        public void OldGameVersionsCannotAcquireStorageByAddingNewFields(int gameVersion)
        {
            var slot = Fresh(); slot.formatVersion = gameVersion == 1 ? 3 : 12; slot.game.version = gameVersion;
            if (gameVersion == 1) { slot.game.prices = null; slot.trades.version = 1; } else slot.layout.version = 5;
            slot.game.storage = new[] { new StorageSave { storageId = First } };
            RejectRead(JsonUtility.ToJson(slot));
        }

        SlotData RackSlot(out GameModel model, out ShopLayout layout)
        {
            layout = new ShopLayout(); var seed = new GameModel(config).CaptureSave(); seed.currency = 200;
            Assert.That(GameModel.TryRestore(config, seed, out model, out _), Is.True);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, 4, 1, out var id).Success, Is.True);
            Assert.That(id, Is.EqualTo("table-1")); Assert.That(model.Currency, Is.EqualTo(168));
            return new SlotData { game = model.CaptureSave(), layout = layout.CaptureSave(), cycle = new ShopDayCycle().CaptureSave(),
                reputation = new ReputationSave { version = 1, points = 38 }, sensitivity = 1.4f, volume = .3f, slapKey = "G", humorVolume = .4f, reducedGags = true };
        }
        SlotData Fresh() => new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        static string RemoveField(string json, string field) => json.Contains(field + ",") ? json.Replace(field + ",", "") : json.Replace("," + field, "");
        void AssertState(SlotData before, SlotData after, GameModel actual)
        {
            Assert.That(ShopLayout.TryRestore(before.layout, out var layout), Is.True);
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), before.game, out var expected, out _), Is.True);
            Assert.That(JsonUtility.ToJson(actual.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expected.CaptureSave())));
            Assert.That(JsonUtility.ToJson(after.layout), Is.EqualTo(JsonUtility.ToJson(layout.CaptureSave())));
            Assert.That(JsonUtility.ToJson(after.cycle), Is.EqualTo(JsonUtility.ToJson(before.cycle)));
            Assert.That(ShopTradeLedger.TryRestore(before.trades, before.cycle, out var oldReports), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(after.trades, after.cycle, out var newReports), Is.True);
            Assert.That(JsonUtility.ToJson(newReports.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(oldReports.CaptureSave())));
            Assert.That(after.reputation.points, Is.EqualTo(before.reputation.points)); Assert.That(after.sensitivity, Is.EqualTo(before.sensitivity));
            Assert.That(after.volume, Is.EqualTo(before.volume)); Assert.That(after.slapKey, Is.EqualTo(before.slapKey));
            Assert.That(after.humorVolume, Is.EqualTo(before.humorVolume)); Assert.That(after.reducedGags, Is.EqualTo(before.reducedGags));
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
            File.WriteAllText(PathFor(), json); byte[] primary = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.False);
            Assert.That(model, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
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
