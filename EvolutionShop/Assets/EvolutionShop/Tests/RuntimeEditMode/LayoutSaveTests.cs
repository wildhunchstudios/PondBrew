using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class LayoutSaveTests
    {
        const string Prefix = "EvolutionShop-LayoutSaveTests-";
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

        [Test] public void EmptyLayoutRoundTripDoesNotCreateFurnitureOrSpendCredits()
        {
            var slot = FreshSlot();
            Assert.That(slot.layout.version, Is.EqualTo(ShopLayout.SaveVersion));
            Assert.That(slot.layout.nextItemNumber, Is.EqualTo(1)); Assert.That(slot.layout.items, Is.Empty);
            Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var model);
            Assert.That(loaded.layout.items, Is.Empty); Assert.That(model.Currency, Is.Zero);
            Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(loaded.game.version, Is.EqualTo(GameModel.SaveVersion));
            Assert.That(loaded.trades.version, Is.EqualTo(4)); Assert.That(loaded.reputation.version, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void StoreAndLabTablesRoundTripAlongsideStockPricesSettingsAndClock(ShopDayPhase phase)
        {
            var slot = ProgressSlot(phase, out var model);
            slot.layout = ThreeTables();
            Assert.That(ShopLayout.TryRestore(slot.layout, out _), Is.True, "The concrete placement fixture must be valid.");
            string expectedGame = JsonUtility.ToJson(model.CaptureSave());
            string expectedCycle = JsonUtility.ToJson(slot.cycle), expectedLayout = JsonUtility.ToJson(slot.layout);
            Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(expectedGame));
            Assert.That(JsonUtility.ToJson(loaded.cycle), Is.EqualTo(expectedCycle));
            Assert.That(JsonUtility.ToJson(loaded.layout), Is.EqualTo(expectedLayout));
            Assert.That(loaded.reputation.points, Is.EqualTo(38)); Assert.That(loaded.slapKey, Is.EqualTo("G"));
            Assert.That(loaded.humorVolume, Is.EqualTo(.4f)); Assert.That(loaded.reducedGags, Is.True);
            Assert.That(loaded.volume, Is.EqualTo(.3f)); Assert.That(loaded.sensitivity, Is.EqualTo(1.4f));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryHistoricalEnvelopeGetsEmptyLayoutOnlyInMemoryIgnoringStrayLayout(int format)
        {
            byte[] original = WriteLegacy(format, FreshSlot(), true);
            for (int i = 0; i < 2; i++)
            {
                var slot = Read(out var model);
                Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(slot.layout.version, Is.EqualTo(ShopLayout.SaveVersion));
                Assert.That(slot.layout.nextItemNumber, Is.EqualTo(1)); Assert.That(slot.layout.items, Is.Empty);
                Assert.That(model.Currency, Is.Zero); Assert.That(slot.reputation.points, Is.Zero);
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
            }
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void FormatSevenMigrationPreservesPendingOrderStockAndSettingsWithoutResolvingIt(ShopDayPhase phase)
        {
            var slot = ProgressSlot(phase, out var model);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var ledger), Is.True);
            Assert.That(ledger.BeginVisit("legacy-order"), Is.True);
            Assert.That(model.ReserveTank("tank-a", "legacy-order").Success, Is.True);
            slot.trades = ledger.CaptureSave(); slot.game = model.CaptureSave();
            string expectedGame = JsonUtility.ToJson(slot.game), specimen = model.GetTank("tank-a").Value.Id;
            byte[] original = WriteLegacy(7, slot, false);
            var loaded = Read(out var restored);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(expectedGame));
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(specimen));
            Assert.That(restored.GetReservation("tank-a"), Is.Null);
            Assert.That(loaded.trades.pendingCustomerIds, Is.EqualTo(new[] { "legacy-order" }));
            Assert.That(loaded.reputation.points, Is.EqualTo(38)); Assert.That(loaded.slapKey, Is.EqualTo("G"));
            Assert.That(loaded.humorVolume, Is.EqualTo(.4f)); Assert.That(loaded.reducedGags, Is.True);
            Assert.That(loaded.layout.items, Is.Empty);
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var reloadedLedger), Is.True);
            Assert.That(reloadedLedger.CancelPendingForReload(), Is.True);
            Assert.That(reloadedLedger.CancelPendingForReload(), Is.False);
            Assert.That(reloadedLedger.Current.interrupted, Is.EqualTo(1));
            Assert.That(restored.Currency, Is.EqualTo(model.Currency)); Assert.That(loaded.reputation.points, Is.EqualTo(38));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [Test] public void ActualVersionSevenTradingNightMigratesWithoutRewardsOrChangingFixtureBytes()
        {
            string fixture = Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V7TradingNight.json");
            byte[] original = File.ReadAllBytes(fixture); File.WriteAllBytes(PathFor(), original);
            var loaded = Read(out var model);
            Assert.That(model.Currency, Is.EqualTo(602)); Assert.That(model.UpgradeOwned, Is.True);
            Assert.That(model.GetPrice("bubblebelly").Value.Price, Is.EqualTo(12));
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(loaded.cycle.dayNumber, Is.EqualTo(4)); Assert.That(loaded.cycle.phase, Is.EqualTo(ShopDayPhase.Night));
            Assert.That(loaded.trades.last.sales, Is.EqualTo(26)); Assert.That(loaded.trades.last.revenue, Is.EqualTo(220));
            Assert.That(loaded.reputation.points, Is.EqualTo(100)); Assert.That(loaded.layout.items, Is.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(fixture), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void PurchasedTableReloadsWithoutRechargingAndItsFreeMovePersists()
        {
            var model = new GameModel(config);
            for (int i = 0; i < 2; i++)
            {
                ProducePondlet(model);
                Assert.That(model.TransferToTank("tank-a").Success, Is.True);
                Assert.That(model.SellFromTank("tank-a").Success, Is.True);
            }
            Assert.That(model.Currency, Is.EqualTo(12));
            var layout = new ShopLayout();
            var bought = layout.TryCommit(model, ShopDayPhase.Night, null, -4, -1, 0, out string id);
            Assert.That(bought.Success, Is.True, bought.Failure.ToString());
            Assert.That(id, Is.EqualTo("table-1")); Assert.That(model.Currency, Is.Zero);
            var slot = FreshSlot(); slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); Write(slot);

            var loaded = Read(out var restored);
            Assert.That(ShopLayout.TryRestore(loaded.layout, out var restoredLayout), Is.True);
            Assert.That(restored.Currency, Is.Zero); Assert.That(restoredLayout.Items.Count, Is.EqualTo(1));
            var moved = restoredLayout.TryCommit(restored, ShopDayPhase.Night, id, -4, -2, 1, out string movedId);
            Assert.That(moved.Success, Is.True, moved.Failure.ToString()); Assert.That(movedId, Is.EqualTo(id));
            Assert.That(restored.Currency, Is.Zero);
            Assert.That(restoredLayout.TryCommit(restored, ShopDayPhase.Night, null, 4, -1, 0, out _).Success, Is.False);
            Assert.That(restored.Currency, Is.Zero); Assert.That(restoredLayout.Items.Count, Is.EqualTo(1));
            loaded.game = restored.CaptureSave(); loaded.layout = restoredLayout.CaptureSave(); Write(loaded);
            var again = Read(out var againModel);
            Assert.That(againModel.Currency, Is.Zero); Assert.That(again.layout.nextItemNumber, Is.EqualTo(2));
            Assert.That(again.layout.items.Length, Is.EqualTo(1)); Assert.That(again.layout.items[0].id, Is.EqualTo(id));
            Assert.That(again.layout.items[0].x, Is.EqualTo(-4)); Assert.That(again.layout.items[0].z, Is.EqualTo(-2));
            Assert.That(again.layout.items[0].quarterTurns, Is.EqualTo(1));
        }

        [Test] public void RejectedPlacementLeavesPersistedMoneyAndLayoutUnchanged()
        {
            var model = new GameModel(config);
            for (int i = 0; i < 2; i++)
            {
                ProducePondlet(model); Assert.That(model.TransferToTank("tank-a").Success, Is.True);
                Assert.That(model.SellFromTank("tank-a").Success, Is.True);
            }
            var layout = new ShopLayout(); var slot = FreshSlot(); slot.game = model.CaptureSave(); Write(slot);
            byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, null, 0, 0, 0, out _).Success, Is.False);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Open, null, -4, -1, 0, out _).Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(12)); Assert.That(layout.Items.Count, Is.Zero);
            slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Read(out var restored).layout.items, Is.Empty); Assert.That(restored.Currency, Is.EqualTo(12));
        }

        [Test] public void FirstVersionSevenWriteKeepsExactArchiveAcrossRepeatedCurrentWrites()
        {
            byte[] original = WriteLegacy(7, FreshSlot(), false);
            var migrated = Read(out _); Write(migrated);
            Assert.That(File.ReadAllBytes(PathFor() + ".v7.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] current = File.ReadAllBytes(PathFor()); migrated.layout = ThreeTables(); Write(migrated);
            Assert.That(File.ReadAllBytes(PathFor() + ".v7.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(current));
            Assert.That(Read(out _).layout.items.Length, Is.EqualTo(3));
        }

        [Test] public void ExistingVersionSevenArchiveIsNeverOverwritten()
        {
            WriteLegacy(7, FreshSlot(), false);
            byte[] archive = { 7, 5, 3, 1 }; File.WriteAllBytes(PathFor() + ".v7.bak", archive);
            Write(Read(out _));
            Assert.That(File.ReadAllBytes(PathFor() + ".v7.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedVersionSevenArchivePreservesPrimaryAndRotatingBackup()
        {
            byte[] original = WriteLegacy(7, FreshSlot(), false);
            byte[] backup = { 5, 4, 3 }; File.WriteAllBytes(PathFor() + ".bak", backup);
            var migrated = Read(out _); Directory.CreateDirectory(PathFor() + ".v7.bak");
            Assert.That(SaveStore.Write(PathFor(), migrated, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("empty")] [TestCase("missing-version")]
        [TestCase("missing-items")] [TestCase("null-items")]
        public void CurrentEnvelopeRequiresExplicitLayoutData(string problem)
        {
            var slot = FreshSlot(); Write(slot); Read(out _); // Positive control for all other fields.
            string layout = problem == "missing" ? null : problem == "null" ? "null" :
                problem == "empty" ? "{}" : problem == "missing-items" ? "{\"version\":" + ShopLayout.SaveVersion + ",\"storeTier\":0,\"nextItemNumber\":1}" :
                problem == "null-items" ? "{\"version\":" + ShopLayout.SaveVersion + ",\"storeTier\":0,\"nextItemNumber\":1,\"items\":null}" :
                "{\"storeTier\":0,\"nextItemNumber\":1,\"items\":[]}";
            AssertInvalidRead(CurrentJson(slot, layout));
        }

        [TestCase("nested-items")] [TestCase("quoted-items")]
        public void NestedOrQuotedItemsDoNotSatisfyTheDirectLayoutArray(string problem)
        {
            var slot = FreshSlot(); Write(slot); Read(out _);
            string payload = problem == "nested-items"
                ? "{\"version\":" + ShopLayout.SaveVersion + ",\"storeTier\":0,\"nextItemNumber\":1,\"metadata\":{\"items\":[]}}"
                : "{\"version\":" + ShopLayout.SaveVersion + ",\"storeTier\":0,\"nextItemNumber\":1,\"note\":\"\\\"items\\\": []\"}";
            AssertInvalidRead(CurrentJson(slot, payload));
        }

        [TestCase("root-layout")] [TestCase("root-version")]
        [TestCase("layout-items")] [TestCase("layout-version")]
        public void DuplicateDirectPropertiesAreRejectedWithoutChangingTheirBytes(string problem)
        {
            var slot = FreshSlot(); Write(slot); Read(out _);
            string layout = JsonUtility.ToJson(slot.layout);
            if (problem == "layout-items") layout = layout.Replace("\"items\":[]", "\"items\":[],\"items\":[]");
            if (problem == "layout-version") layout = layout.Replace("\"version\":" + ShopLayout.SaveVersion,
                "\"version\":" + ShopLayout.SaveVersion + ",\"version\":" + ShopLayout.SaveVersion);
            string json = CurrentJson(slot, layout);
            if (problem == "root-layout") json = json.Replace("\"layout\":", "\"layout\":" + layout + ",\"layout\":");
            if (problem == "root-version") json = json.Replace("\"formatVersion\":" + SaveStore.CurrentFormatVersion,
                "\"formatVersion\":" + SaveStore.CurrentFormatVersion + ",\"formatVersion\":" + SaveStore.CurrentFormatVersion);
            AssertInvalidRead(json, null); // The native JSON reader may also reject duplicate keys.
        }

        [Serializable] sealed class ShapeNoise
        {
            public string text = "quoted \"items\":null [ } and \\ escaped content";
            public int[] items = { 1, 2 };
        }

        [TestCase("escaped-keys")] [TestCase("nested-and-escaped-values")]
        public void ExplicitEmptyArrayStillLoadsWithEscapedKeysOrUnrelatedNestedValues(string problem)
        {
            var slot = FreshSlot(); string layout = JsonUtility.ToJson(slot.layout);
            string json = CurrentJson(slot, layout);
            if (problem == "escaped-keys")
                json = json.Replace("\"layout\":", "\"\\u006cayout\":").Replace("\"items\":", "\"\\u0069tems\":");
            else
                json = json.Insert(1, "\"metadata\":" + JsonUtility.ToJson(new ShapeNoise()) + ",");
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var model);
            Assert.That(loaded.layout.items, Is.Empty); Assert.That(loaded.layout.nextItemNumber, Is.EqualTo(1));
            Assert.That(model.Currency, Is.Zero); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [TestCase("null")] [TestCase("version-zero")] [TestCase("future-version")]
        [TestCase("missing-items")] [TestCase("null-item")] [TestCase("duplicate-id")]
        [TestCase("id-gap")] [TestCase("wrong-counter")] [TestCase("unknown-definition")]
        [TestCase("invalid-rotation")] [TestCase("out-of-bounds")] [TestCase("overlap")]
        [TestCase("blocked-aisle")] [TestCase("too-many")]
        public void InvalidLayoutRejectsReadAndWriteWithoutReplacingKnownGoodFiles(string problem)
        {
            var slot = FreshSlot(); slot.layout = ThreeTables();
            Assert.That(ShopLayout.TryRestore(slot.layout, out _), Is.True, "Malformed fixtures start from a valid layout.");
            Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            switch (problem)
            {
                case "null": slot.layout = null; break;
                case "version-zero": slot.layout.version = 0; break;
                case "future-version": slot.layout.version = ShopLayout.SaveVersion + 1; break;
                case "missing-items": slot.layout.items = null; break;
                case "null-item": slot.layout.items[0] = null; break;
                case "duplicate-id": slot.layout.items[1].id = slot.layout.items[0].id; break;
                case "id-gap": slot.layout.items[2].id = "table-4"; break;
                case "wrong-counter": slot.layout.nextItemNumber = 1; break;
                case "unknown-definition": slot.layout.items[0].definitionId = "display-tank"; break;
                case "invalid-rotation": slot.layout.items[0].quarterTurns = 4; break;
                case "out-of-bounds": slot.layout.items[0].x = int.MaxValue; break;
                case "overlap": slot.layout.items[1].x = slot.layout.items[0].x; slot.layout.items[1].z = slot.layout.items[0].z; break;
                case "blocked-aisle": slot.layout.items[0].x = 0; slot.layout.items[0].z = 0; break;
                case "too-many": slot.layout.items = new[] { Item(1, -4, -1, 0), Item(2, 4, -1, 0), Item(3, 9, 4, 1), Item(4, -5, 3, 1) }; slot.layout.nextItemNumber = 5; break;
            }
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2));
            string missingDirectory = Path.Combine(directory, "not-created", "slot.json");
            Assert.That(SaveStore.Write(missingDirectory, slot, out _), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(missingDirectory)), Is.False);
            // JsonUtility writes a null array as [], now a legitimate all-sold layout with a
            // high-water ID counter. Keep this malformed-file case explicitly null on disk.
            string invalidJson = JsonUtility.ToJson(slot);
            if (problem == "missing-items") invalidJson = invalidJson.Replace("\"items\":[]", "\"items\":null");
            AssertInvalidRead(invalidJson);
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        SlotData FreshSlot() => new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        static LayoutItemSave Item(int number, int x, int z, int turns) => new LayoutItemSave
            { id = "table-" + number, definitionId = "work-table", x = x, z = z, quarterTurns = turns };
        static LayoutSave ThreeTables() => new LayoutSave { version = ShopLayout.SaveVersion, nextItemNumber = 4,
            facilities = new ShopLayout().CaptureSave().facilities,
            items = new[] { Item(1, -4, -1, 0), Item(2, 4, -1, 0), Item(3, 9, 4, 1) } };

        SlotData ProgressSlot(ShopDayPhase phase, out GameModel model)
        {
            model = new GameModel(config);
            ProducePondlet(model);
            Assert.That(model.TransferToTank("tank-a").Success, Is.True);
            Assert.That(model.SetPrice("pondlet", 8).Success, Is.True);
            var day = new ShopDayCycle();
            if (phase != ShopDayPhase.Night)
            {
                Assert.That(day.OpenShop(), Is.True); day.Tick(4f);
                if (phase == ShopDayPhase.Closing) { Assert.That(day.RequestClose(), Is.True); day.Tick(.4f); }
            }
            return new SlotData { game = model.CaptureSave(), cycle = day.CaptureSave(),
                trades = ShopTradeLedger.LegacyForCycle(day.CaptureSave(), 1).CaptureSave(),
                reputation = new ReputationSave { version = 1, points = 38 }, slapKey = "G", humorVolume = .4f,
                reducedGags = true, sensitivity = 1.4f, volume = .3f };
        }

        void ProducePondlet(GameModel model)
        {
            Assert.That(model.Collect(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Flow).Success, Is.True);
            Assert.That(model.BeginIncubation().Success, Is.True); model.Tick(config.IncubationSeconds);
        }

        byte[] WriteLegacy(int format, SlotData slot, bool strayLayout)
        {
            slot.game = LegacyGameSaveFixture.AsVersion(slot.game, format <= 3 ? 1 : 2);
            if (format <= 3) slot.game.prices = null;
            slot.trades.version = format == 3 ? 1 : format == 4 ? 2 : format <= 6 ? 3 : 4;
            if (format < 5) slot.trades.pendingCustomerIds = null;
            string json = "{\n\"formatVersion\":" + format + ",\n\"game\":" + JsonUtility.ToJson(slot.game) +
                (format >= 2 ? ",\n\"cycle\":" + JsonUtility.ToJson(slot.cycle) : "") +
                (format >= 3 ? ",\n\"trades\":" + JsonUtility.ToJson(slot.trades) : "") +
                (format >= 6 ? ",\n\"reputation\":" + JsonUtility.ToJson(slot.reputation) : "") +
                (format >= 7 ? ",\n\"slapKey\":\"" + slot.slapKey + "\",\"humorVolume\":" + slot.humorVolume.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"reducedGags\":" + (slot.reducedGags ? "true" : "false") : "") +
                ",\n\"sensitivity\":" + slot.sensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"volume\":" + slot.volume.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                (strayLayout ? ",\n\"layout\":{\"version\":99,\"nextItemNumber\":99,\"items\":null}" : "") + "\n}";
            File.WriteAllText(PathFor(), json); return File.ReadAllBytes(PathFor());
        }

        static string CurrentJson(SlotData slot, string layout) => "{\"formatVersion\":" + SaveStore.CurrentFormatVersion + ",\"game\":" + JsonUtility.ToJson(slot.game) +
            ",\"cycle\":" + JsonUtility.ToJson(slot.cycle) + ",\"trades\":" + JsonUtility.ToJson(slot.trades) +
            ",\"reputation\":" + JsonUtility.ToJson(slot.reputation) + ",\"staff\":" + JsonUtility.ToJson(slot.staff) + ",\"stocker\":" + JsonUtility.ToJson(slot.stocker) + ",\"technician\":" + JsonUtility.ToJson(slot.technician) + ",\"tutorial\":" + JsonUtility.ToJson(slot.tutorial) + ",\"automation\":" + JsonUtility.ToJson(slot.automation) +
            (layout == null ? "" : ",\"layout\":" + WithCurrentLabFields(layout)) +
            ",\"reducedVisualMotion\":" + (slot.reducedVisualMotion ? "true" : "false") + ",\"effectsDetail\":" + slot.effectsDetail +
            ",\"ambienceVolume\":" + slot.ambienceVolume.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"slapKey\":\"F\",\"humorVolume\":1,\"reducedGags\":false,\"sensitivity\":1,\"volume\":0.65}";

        [Serializable] sealed class LabFields { public int labTier; public FacilitySave[] facilities; }
        static string WithCurrentLabFields(string json)
        {
            if (json == null || !json.StartsWith("{", StringComparison.Ordinal) || json.Contains("\"facilities\"")) return json;
            string fields = JsonUtility.ToJson(new LabFields { labTier = 0, facilities = new ShopLayout().CaptureSave().facilities });
            return json.Insert(1, fields.Substring(1, fields.Length - 2) + (json == "{}" ? "" : ","));
        }
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
        SlotData Read(out GameModel model)
        {
            Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error);
            return slot;
        }
        void AssertInvalidRead(string json, string expectedReason = "layout")
        {
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.False);
            Assert.That(model, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty);
            if (expectedReason != null) Assert.That(error, Does.Contain(expectedReason));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }
    }
}
