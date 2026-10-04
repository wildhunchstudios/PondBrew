using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ExpansionSaveTests
    {
        const string Prefix = "EvolutionShop-ExpansionSaveTests-";
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
            string target = Path.GetFullPath(directory);
            Assert.That(Path.GetDirectoryName(target).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(target).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(target, true);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void UnfurnishedTierRoundTripKeepsOnlyTheTwoStarterSlotsAndDoesNotCharge(int tier)
        {
            var layout = new ShopLayout().CaptureSave(); layout.storeTier = tier;
            var slot = Slot(layout, ShopDayPhase.Night, out _);
            Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored);
            Assert.That(loaded.layout.storeTier, Is.EqualTo(tier)); Assert.That(loaded.layout.items, Is.Empty);
            Assert.That(restored.CaptureSave().tanks.Length, Is.EqualTo(2)); Assert.That(restored.Currency, Is.EqualTo(600));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase(0, ShopDayPhase.Night)] [TestCase(1, ShopDayPhase.Open)] [TestCase(2, ShopDayPhase.Closing)]
        public void OwnedDisplaysAndEveryStockIdentityRoundTripWithAllOtherProgress(int tier, ShopDayPhase phase)
        {
            var slot = Slot(DisplayLayout(tier), phase, out var model);
            foreach (var tank in model.CaptureSave().tanks)
            {
                Produce(model, tank.tankId == "tank-b" ? "bubblebelly" : "pondlet");
                Ok(model.TransferToTank(tank.tankId));
            }
            Ok(model.SetPrice("pondlet", 8)); Ok(model.SetPrice("bubblebelly", 12));
            Produce(model, "ribbonfin"); // Ready lab output must remain distinct from every display resident.
            slot.game = model.CaptureSave(); Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored);
            AssertSameProgress(slot, loaded, model, restored);
            Assert.That(loaded.layout.version, Is.EqualTo(ShopLayout.SaveVersion)); Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(restored.CaptureSave().tanks.Length, Is.EqualTo(tier == 0 ? 3 : 4));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void ThreePurchasedDisplaysKeepSevenDistinctSlotsAcrossRepeatedFileSaves()
        {
            var layout = new LayoutSave { version = ShopLayout.SaveVersion, storeTier = 2, nextItemNumber = 4, items = new[]
            {
                new LayoutItemSave { id = "table-1", definitionId = "small-display", x = 3, z = -5 },
                new LayoutItemSave { id = "table-2", definitionId = "large-display", x = -18, z = -3 },
                new LayoutItemSave { id = "table-3", definitionId = "large-display", x = -26, z = -3 }
            } };
            var slot = Slot(layout, ShopDayPhase.Night, out var model);
            foreach (var tank in model.CaptureSave().tanks) { Produce(model, "pondlet"); Ok(model.TransferToTank(tank.tankId)); }
            slot.game = model.CaptureSave(); Write(slot); Write(Read(out _));
            var loaded = Read(out var restored);
            Assert.That(restored.CaptureSave().tanks.Length, Is.EqualTo(7)); AssertSameProgress(slot, loaded, model, restored);
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void TwoUnpaidLargeDisplaySlotsReloadWithoutStockLossIncomeOrReputationReplay(ShopDayPhase phase)
        {
            var slot = Slot(DisplayLayout(1), phase, out var model);
            var reports = new ShopTradeLedger(); Assert.That(reports.BeginDay(1, 2), Is.True);
            foreach (string id in new[] { "table-1-slot-1", "table-1-slot-2" })
            {
                Produce(model, "pondlet"); Ok(model.TransferToTank(id));
                Assert.That(reports.BeginVisit(id), Is.True); Ok(model.ReserveTank(id, id));
            }
            slot.game = model.CaptureSave(); slot.trades = reports.CaptureSave(); Write(slot);
            var loaded = Read(out var restored);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(model.CaptureSave())));
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var restoredReports), Is.True);
            Assert.That(restoredReports.PendingCount, Is.EqualTo(2), "Read leaves reconciliation to the session.");
            Assert.That(restoredReports.CancelPendingForReload(), Is.True); Assert.That(restoredReports.CancelPendingForReload(), Is.False);
            Assert.That(restoredReports.Current.interrupted, Is.EqualTo(2)); Assert.That(restoredReports.Current.revenue, Is.Zero);
            Assert.That(restored.Currency, Is.EqualTo(600)); Assert.That(loaded.reputation.points, Is.EqualTo(38));
            foreach (string id in new[] { "table-1-slot-1", "table-1-slot-2" })
            { Assert.That(restored.GetReservation(id), Is.Null); Assert.That(restored.GetTank(id).Value.Id, Is.EqualTo(model.GetTank(id).Value.Id)); }
        }

        [Test] public void PaidOwnedDisplaySlotCannotReplayPaymentAfterFileReload()
        {
            var slot = Slot(DisplayLayout(1), ShopDayPhase.Open, out var model);
            var reports = new ShopTradeLedger(); Assert.That(reports.BeginDay(1, 1), Is.True); Assert.That(reports.BeginVisit("paid"), Is.True);
            Produce(model, "pondlet"); Ok(model.TransferToTank("table-1-slot-2"));
            Ok(model.ReserveTank("table-1-slot-2", "paid")); string specimen = model.GetTank("table-1-slot-2").Value.Id;
            Ok(model.SellReservedTank("table-1-slot-2", "paid", specimen));
            Assert.That(ShopReputation.TryRestore(slot.reputation, out var reputation), Is.True);
            Assert.That(reputation.ResolveVisit(reports, "paid", CustomerOutcome.Sold, 6), Is.True);
            slot.game = model.CaptureSave(); slot.trades = reports.CaptureSave(); slot.reputation = reputation.CaptureSave(); Write(slot);
            var loaded = Read(out var restored);
            Assert.That(restored.GetTank("table-1-slot-2"), Is.Null); Assert.That(restored.Currency, Is.EqualTo(606));
            Assert.That(restored.SellReservedTank("table-1-slot-2", "paid", specimen).Success, Is.False);
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var restoredReports), Is.True);
            Assert.That(ShopReputation.TryRestore(loaded.reputation, out var restoredReputation), Is.True);
            Assert.That(restoredReputation.ResolveVisit(restoredReports, "paid", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(restored.Currency, Is.EqualTo(606)); Assert.That(restoredReputation.Points, Is.EqualTo(40));
            Assert.That(restoredReports.Current.sales, Is.EqualTo(1)); Assert.That(restoredReports.Current.revenue, Is.EqualTo(6));
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void FormatEightMigrationPreservesFurnitureCompleteProgressAndBytes(ShopDayPhase phase)
        {
            var slot = LegacySlot(phase, out var model);
            Produce(model, "pondlet"); Ok(model.TransferToTank("tank-a")); Ok(model.SetPrice("pondlet", 8));
            Produce(model, "bubblebelly"); Ok(model.TransferToTank("tank-b")); Produce(model, "ribbonfin");
            if (phase != ShopDayPhase.Night)
            {
                var report = new ShopTradeLedger(); Assert.That(report.BeginDay(1, 2), Is.True);
                Assert.That(report.BeginVisit("legacy-order"), Is.True); Ok(model.ReserveTank("tank-b", "legacy-order"));
                slot.trades = report.CaptureSave();
            }
            slot.game = model.CaptureSave(); byte[] original = WriteLegacy(slot);
            var loaded = Read(out var restored);
            Assert.That(loaded.layout.storeTier, Is.Zero); Assert.That(loaded.layout.version, Is.EqualTo(ShopLayout.SaveVersion));
            Assert.That(loaded.layout.items.Length, Is.EqualTo(3)); Assert.That(loaded.layout.nextItemNumber, Is.EqualTo(4));
            AssertSameProgress(slot, loaded, model, restored, false);
            for (int i = 0; i < 3; i++) Assert.That(JsonUtility.ToJson(loaded.layout.items[i]), Is.EqualTo(JsonUtility.ToJson(slot.layout.items[i])));
            Assert.That(restored.CaptureSave().tanks.Length, Is.EqualTo(2)); Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void FirstFormatEightWriteArchivesExactOriginalAndRepeatedSavesDoNotReplaceIt()
        {
            byte[] original = WriteLegacy(LegacySlot(ShopDayPhase.Night, out _));
            var migrated = Read(out _); Write(migrated);
            Assert.That(File.ReadAllBytes(PathFor() + ".v8.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] current = File.ReadAllBytes(PathFor()); migrated.volume = .2f; Write(migrated);
            Assert.That(File.ReadAllBytes(PathFor() + ".v8.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(current));
            Assert.That(Read(out _).layout.items.Length, Is.EqualTo(3));
        }

        [Test] public void ExistingFormatEightArchiveIsNeverOverwritten()
        {
            WriteLegacy(LegacySlot(ShopDayPhase.Night, out _)); byte[] archive = { 8, 4, 1 };
            File.WriteAllBytes(PathFor() + ".v8.bak", archive); Write(Read(out _));
            Assert.That(File.ReadAllBytes(PathFor() + ".v8.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedFormatEightArchivePreservesPrimaryAndRotatingBackup()
        {
            byte[] original = WriteLegacy(LegacySlot(ShopDayPhase.Night, out _)); byte[] backup = { 4, 8 };
            File.WriteAllBytes(PathFor() + ".bak", backup); var migrated = Read(out _);
            Directory.CreateDirectory(PathFor() + ".v8.bak");
            Assert.That(SaveStore.Write(PathFor(), migrated, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("\"0\"")] [TestCase("false")]
        [TestCase("0.0")] [TestCase("0e0")] [TestCase("-1")] [TestCase("3")]
        [TestCase("duplicate")] [TestCase("nested-decoy")]
        public void CurrentStoreTierRequiresOneExplicitBoundedInteger(string problem)
        {
            var slot = Slot(new ShopLayout().CaptureSave(), ShopDayPhase.Night, out _); Write(slot); Read(out _);
            string layout = JsonUtility.ToJson(slot.layout);
            string replacement = problem == "missing" ? "" : problem == "duplicate" ? "\"storeTier\":0,\"storeTier\":0," :
                problem == "nested-decoy" ? "\"metadata\":{\"storeTier\":0}," : "\"storeTier\":" + problem + ",";
            Assert.That(layout, Does.Contain("\"storeTier\":0"));
            // Build an explicit field order so JsonUtility's output order is not part of the guard test.
            layout = "{\"version\":" + ShopLayout.SaveVersion + "," + replacement + "\"nextItemNumber\":1,\"items\":[]}";
            RejectRead(JsonWithLayout(slot, layout));
        }

        [Test] public void EscapedDirectTierAndUnrelatedNestedTierAreAccepted()
        {
            var slot = Slot(new ShopLayout().CaptureSave(), ShopDayPhase.Night, out _);
            string json = JsonWithLayout(slot, "{\"version\":" + ShopLayout.SaveVersion + ",\"\\u0073toreTier\":0,\"nextItemNumber\":1,\"items\":[],\"noise\":{\"storeTier\":99}}");
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(Read(out _).layout.storeTier, Is.Zero); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [TestCase("orphan-empty")] [TestCase("orphan-stock")] [TestCase("missing-empty")]
        [TestCase("duplicate-slot")] [TestCase("wrong-owner")] [TestCase("duplicate-specimen")]
        public void SlotRegistryMismatchRejectsReadAndWriteBeforeTouchingFiles(string problem)
        {
            var slot = Slot(DisplayLayout(1), ShopDayPhase.Night, out var model);
            Produce(model, "pondlet"); Ok(model.TransferToTank("table-1-slot-1")); slot.game = model.CaptureSave();
            Write(slot); Write(slot); byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            var entries = new System.Collections.Generic.List<TankSave>(slot.game.tanks);
            switch (problem)
            {
                case "orphan-empty": entries.Add(new TankSave { tankId = "table-99-slot-1" }); break;
                case "orphan-stock": entries.Add(new TankSave { tankId = "table-99-slot-1", specimenId = "orphan", speciesId = "pondlet" }); break;
                case "missing-empty": entries.RemoveAll(t => t.tankId == "table-1-slot-2"); break;
                case "duplicate-slot": entries[0].tankId = entries[1].tankId; break;
                case "wrong-owner": entries.Find(t => t.tankId == "table-1-slot-2").tankId = "table-2-slot-2"; break;
                case "duplicate-specimen":
                    entries.Find(t => t.tankId == "table-1-slot-2").specimenId = model.GetTank("table-1-slot-1").Value.Id;
                    entries.Find(t => t.tankId == "table-1-slot-2").speciesId = "pondlet"; break;
            }
            slot.game.tanks = entries.ToArray(); RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("format8-layout2")] [TestCase("format9-layout1")] [TestCase("future-format")]
        public void EnvelopeLayoutPairingsRemainExplicit(string problem)
        {
            var slot = Slot(new ShopLayout().CaptureSave(), ShopDayPhase.Night, out _);
            if (problem == "format8-layout2") { slot.formatVersion = 8; slot.game.version = 2; slot.layout.version = 2; }
            else if (problem == "format9-layout1") { slot.formatVersion = 9; slot.game.version = 2; slot.layout.version = 1; }
            else slot.formatVersion = SaveStore.CurrentFormatVersion + 1;
            RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase(-1)] [TestCase(3)]
        public void InvalidTierWritePreservesPrimaryBackupAndDoesNotCreateDirectories(int tier)
        {
            var slot = Slot(new ShopLayout().CaptureSave(), ShopDayPhase.Night, out _); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            slot.layout.storeTier = tier; RejectWrite(slot, primary, backup);
        }

        static LayoutSave DisplayLayout(int tier) => new LayoutSave
        {
            version = ShopLayout.SaveVersion, storeTier = tier, nextItemNumber = 2,
            items = new[] { new LayoutItemSave { id = "table-1", definitionId = tier == 0 ? "small-display" : "large-display",
                x = tier == 0 ? 3 : tier == 1 ? -18 : -26, z = tier == 0 ? -5 : -3, quarterTurns = 0 } }
        };

        SlotData Slot(LayoutSave save, ShopDayPhase phase, out GameModel model)
        {
            if (save.version == ShopLayout.SaveVersion && save.facilities == null)
                save.facilities = new ShopLayout().CaptureSave().facilities;
            Assert.That(ShopLayout.TryRestore(save, out var layout), Is.True, "Test layout must be a valid positive control.");
            var derived = layout.WithTankIds(config); var seed = new GameModel(derived).CaptureSave(); seed.currency = 600;
            Assert.That(GameModel.TryRestore(derived, seed, out model, out _), Is.True);
            Ok(model.BuyUpgrade()); // Preserve a paid upgrade as well as new stock capacity.
            var current = model.CaptureSave(); current.currency = 600;
            Assert.That(GameModel.TryRestore(derived, current, out model, out _), Is.True);
            var cycle = new ShopDayCycle();
            if (phase != ShopDayPhase.Night)
            { Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(7); if (phase == ShopDayPhase.Closing) { Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.5f); } }
            return new SlotData { game = model.CaptureSave(), layout = save, cycle = cycle.CaptureSave(),
                trades = ShopTradeLedger.LegacyForCycle(cycle.CaptureSave(), 0).CaptureSave(),
                reputation = new ReputationSave { version = 1, points = 38 }, sensitivity = 1.4f, volume = .3f,
                slapKey = "G", humorVolume = .4f, reducedGags = true };
        }

        SlotData LegacySlot(ShopDayPhase phase, out GameModel model)
        {
            var layout = new LayoutSave { version = 1, nextItemNumber = 4, items = new[]
            {
                new LayoutItemSave { id = "table-1", definitionId = "work-table", x = -4, z = -1, quarterTurns = 0 },
                new LayoutItemSave { id = "table-2", definitionId = "work-table", x = 4, z = -1, quarterTurns = 0 },
                new LayoutItemSave { id = "table-3", definitionId = "work-table", x = 9, z = 4, quarterTurns = 1 }
            } };
            var slot = Slot(layout, phase, out model); slot.formatVersion = 8; slot.game.version = 2; return slot;
        }

        byte[] WriteLegacy(SlotData slot)
        {
            slot.game.version = 2;
            string layout = JsonUtility.ToJson(slot.layout).Replace("\"storeTier\":0,", "").Replace(",\"storeTier\":0", "");
            File.WriteAllText(PathFor(), JsonWithLayout(slot, layout)); return File.ReadAllBytes(PathFor());
        }

        static string JsonWithLayout(SlotData slot, string layout) => "{\"formatVersion\":" + slot.formatVersion +
            ",\"game\":" + JsonUtility.ToJson(slot.game) + ",\"cycle\":" + JsonUtility.ToJson(slot.cycle) +
            ",\"trades\":" + JsonUtility.ToJson(slot.trades) + ",\"reputation\":" + JsonUtility.ToJson(slot.reputation) +
            ",\"layout\":" + (slot.formatVersion == SaveStore.CurrentFormatVersion ? WithCurrentLabFields(layout) : layout) + ",\"sensitivity\":" + slot.sensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"volume\":" + slot.volume.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"slapKey\":\"" + slot.slapKey + "\",\"humorVolume\":" + slot.humorVolume.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"reducedGags\":" + (slot.reducedGags ? "true" : "false") + "}";

        [Serializable] sealed class LabFields { public int labTier; public FacilitySave[] facilities; }
        static string WithCurrentLabFields(string json)
        {
            if (json == null || !json.StartsWith("{", StringComparison.Ordinal) || json.Contains("\"facilities\"")) return json;
            string fields = JsonUtility.ToJson(new LabFields { labTier = 0, facilities = new ShopLayout().CaptureSave().facilities });
            return json.Insert(1, fields.Substring(1, fields.Length - 2) + (json == "{}" ? "" : ","));
        }

        void AssertSameProgress(SlotData expected, SlotData actual, GameModel before, GameModel after, bool compareLayout = true)
        {
            Assert.That(JsonUtility.ToJson(after.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(before.CaptureSave())));
            Assert.That(JsonUtility.ToJson(actual.cycle), Is.EqualTo(JsonUtility.ToJson(expected.cycle)));
            Assert.That(ShopTradeLedger.TryRestore(expected.trades, expected.cycle, out var expectedReports), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(actual.trades, actual.cycle, out var actualReports), Is.True);
            Assert.That(JsonUtility.ToJson(actualReports.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expectedReports.CaptureSave())));
            if (compareLayout) Assert.That(JsonUtility.ToJson(actual.layout), Is.EqualTo(JsonUtility.ToJson(expected.layout)));
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
        {
            Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error); return slot;
        }
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
