using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class DecorSaveTests
    {
        const string Prefix = "EvolutionShop-DecorSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");
        string FixturePath() => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V9ExpandedStock.json");

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

        [Test] public void ActualFormatNineExpandedStockMigratesWithoutRewardsOrChangingFixtureBytes()
        {
            byte[] original = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), original);
            var expected = JsonUtility.FromJson<SlotData>(File.ReadAllText(FixturePath()));
            var loaded = Read(out var restored);
            Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(loaded.layout.version, Is.EqualTo(ShopLayout.SaveVersion));
            Assert.That(loaded.layout.storeTier, Is.EqualTo(2)); Assert.That(loaded.layout.items.Length, Is.EqualTo(3));
            AssertSameProgress(expected, loaded, restored);
            Assert.That(restored.Currency, Is.EqualTo(52)); Assert.That(restored.TankIds.Length, Is.EqualTo(7));
            Assert.That(restored.GetTank("table-1-slot-1").Value.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(restored.GetTank("table-3-slot-2").Value.SpeciesId, Is.EqualTo("ribbonfin"));
            Assert.That(loaded.trades.last.sales, Is.EqualTo(2)); Assert.That(loaded.trades.last.revenue, Is.EqualTo(16));
            Assert.That(loaded.reputation.points, Is.EqualTo(4)); Assert.That(Appeal(loaded), Is.Zero);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(FixturePath()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void FormatNinePendingMigrationKeepsCompleteProgressAndLeavesReloadReconciliationToSession(ShopDayPhase phase)
        {
            var old = HistoricalSlot(out var model);
            Ok(model.BuyUpgrade()); Produce(model, "bubblebelly"); // Preserve paid upgrade and ready lab output too.
            Assert.That(ShopDayCycle.TryRestore(old.cycle, out var cycle), Is.True); Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(11);
            Assert.That(ShopTradeLedger.TryRestore(old.trades, old.cycle, out var reports), Is.True);
            Assert.That(reports.BeginDay(cycle.DayNumber, 2), Is.True);
            foreach (string id in new[] { "table-1-slot-1", "table-3-slot-2" })
            { Assert.That(reports.BeginVisit(id), Is.True); Ok(model.ReserveTank(id, id)); }
            if (phase == ShopDayPhase.Closing) { Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.5f); }
            old.game = model.CaptureSave(); old.game.version = 2; old.cycle = cycle.CaptureSave(); old.trades = reports.CaptureSave();
            old.sensitivity = 1.6f; old.volume = .27f; old.slapKey = "G"; old.humorVolume = .4f; old.reducedGags = true;
            File.WriteAllText(PathFor(), JsonUtility.ToJson(old, true)); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored); AssertSameProgress(old, loaded, restored);
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var reloadedReports), Is.True);
            Assert.That(reloadedReports.PendingCount, Is.EqualTo(2)); Assert.That(reloadedReports.CancelPendingForReload(), Is.True);
            Assert.That(reloadedReports.CancelPendingForReload(), Is.False); Assert.That(reloadedReports.Current.interrupted, Is.EqualTo(2));
            Assert.That(reloadedReports.Current.revenue, Is.Zero); Assert.That(restored.Currency, Is.EqualTo(28)); Assert.That(Appeal(loaded), Is.Zero);
            foreach (string id in new[] { "table-1-slot-1", "table-3-slot-2" })
            { Assert.That(restored.GetReservation(id), Is.Null); Assert.That(restored.GetTank(id).Value.Id, Is.EqualTo(model.GetTank(id).Value.Id)); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void FirstFormatNineWriteKeepsExactImmutableArchiveAcrossLaterDecorWrites()
        {
            byte[] original = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), original);
            var slot = Read(out _); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v9.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] current = File.ReadAllBytes(PathFor()); slot.layout = AddLabDecor(slot.layout); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v9.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(current));
            var loaded = Read(out var restored); Assert.That(loaded.layout.items.Length, Is.EqualTo(4)); Assert.That(restored.TankIds.Length, Is.EqualTo(7));
            Assert.That(restored.GetTank("table-3-slot-2").Value.SpeciesId, Is.EqualTo("ribbonfin")); Assert.That(Appeal(loaded), Is.Zero);
        }

        [Test] public void ExistingFormatNineArchiveIsNeverOverwritten()
        {
            File.Copy(FixturePath(), PathFor()); byte[] archive = { 9, 4, 2 }; File.WriteAllBytes(PathFor() + ".v9.bak", archive);
            Write(Read(out _)); Assert.That(File.ReadAllBytes(PathFor() + ".v9.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedFormatNineArchiveLeavesPrimaryAndRotatingBackupUntouched()
        {
            byte[] original = File.ReadAllBytes(FixturePath()); File.WriteAllBytes(PathFor(), original);
            byte[] backup = { 9, 9, 9 }; File.WriteAllBytes(PathFor() + ".bak", backup); var slot = Read(out _);
            Directory.CreateDirectory(PathFor() + ".v9.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void SixDecorRoundTripDerivesCappedAppealWithoutAddingCreatureSlots(ShopDayPhase phase)
        {
            var layout = new LayoutSave { version = 3, storeTier = 0, nextItemNumber = 7, items = new[]
            {
                Item(1, "coral-planter", 9, 4), Item(2, "coral-planter", -4, -1), Item(3, "bubble-lamp", 4, -1),
                Item(4, "silly-statue", 3, -5), Item(5, "coral-planter", -12, -8), Item(6, "bubble-lamp", 10, 8)
            } };
            var slot = FreshSlot(layout, phase, out var model);
            Produce(model, "pondlet"); Ok(model.TransferToTank("tank-a")); Produce(model, "ribbonfin");
            slot.game = model.CaptureSave(); Write(slot); byte[] original = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored); AssertSameProgress(slot, loaded, restored);
            Assert.That(Appeal(loaded), Is.EqualTo(30)); Assert.That(restored.TankIds.Length, Is.EqualTo(2));
            Assert.That(ShopLayout.TryRestore(loaded.layout, out var owned), Is.True); Assert.That(owned.DecorCount, Is.EqualTo(6));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllText(PathFor()), Does.Not.Contain("appeal"));
        }

        [Test] public void AllTwelveMixedItemsRoundTripWithSeparateLimitsAndSevenStockSlots()
        {
            var layout = new LayoutSave { version = 3, storeTier = 2, nextItemNumber = 13, items = new[]
            {
                Item(1, "small-display", 3, -5), Item(2, "large-display", -18, -3), Item(3, "large-display", -26, -3),
                Item(4, "work-table", -4, -1), Item(5, "work-table", 4, -1), Item(6, "work-table", 9, 4),
                Item(7, "coral-planter", 10, 8), Item(8, "coral-planter", -12, 8), Item(9, "coral-planter", -28, -8),
                Item(10, "bubble-lamp", -23, -8), Item(11, "silly-statue", -20, -8), Item(12, "bubble-lamp", -17, -8)
            } };
            layout.items[5].quarterTurns = 1;
            var slot = FreshSlot(layout, ShopDayPhase.Night, out var model); Write(slot);
            var loaded = Read(out var restored); AssertSameProgress(slot, loaded, restored);
            Assert.That(loaded.layout.items.Length, Is.EqualTo(12)); Assert.That(loaded.layout.nextItemNumber, Is.EqualTo(13));
            Assert.That(restored.TankIds.Length, Is.EqualTo(7)); Assert.That(Appeal(loaded), Is.EqualTo(30));
        }

        [Test] public void LabToStoreMovesAndRepeatedReloadsRecomputeAppealWithoutAccumulationOrCharge()
        {
            var slot = FreshSlot(new LayoutSave { version = 3, nextItemNumber = 2, items = new[] { Item(1, "coral-planter", 9, 4) } },
                ShopDayPhase.Night, out var model);
            Write(slot); Assert.That(Appeal(Read(out _)), Is.Zero);
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", -4, -1, 0, out _).Success, Is.True);
            slot.layout = layout.CaptureSave(); Write(slot);
            for (int i = 0; i < 3; i++)
            { slot = Read(out model); Assert.That(Appeal(slot), Is.EqualTo(10)); Assert.That(model.Currency, Is.Zero); Write(slot); }
            Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 9, 4, 0, out _).Success, Is.True);
            slot.layout = layout.CaptureSave(); Write(slot); Assert.That(Appeal(Read(out _)), Is.Zero);
        }

        [TestCase(9)] [TestCase(10)]
        public void ForgedSavedAppealNeverGrantsDerivedAppeal(int format)
        {
            var slot = HistoricalSlot(out _); slot.formatVersion = format;
            if (format == 10) slot.layout.version = 3;
            string json = JsonUtility.ToJson(slot).Replace("\"layout\":{", "\"layout\":{\"appealPoints\":999,")
                .Insert(1, "\"appealPoints\":999,\"appeal\":{\"points\":999},");
            Assert.That(json, Does.Contain("\"appealPoints\":999")); File.WriteAllText(PathFor(), json);
            byte[] original = File.ReadAllBytes(PathFor()); var loaded = Read(out var model);
            Assert.That(Appeal(loaded), Is.Zero); Assert.That(model.Currency, Is.EqualTo(52)); Assert.That(model.TankIds.Length, Is.EqualTo(7));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Write(loaded);
            Assert.That(File.ReadAllText(PathFor()), Does.Not.Contain("appeal"));
        }

        [TestCase(8, 1)] [TestCase(9, 2)]
        public void HistoricalLayoutsCannotAcquireDecorByAddingNewDefinitionIds(int format, int layoutVersion)
        {
            var slot = FreshSlot(new LayoutSave { version = 3, nextItemNumber = 2, items = new[] { Item(1, "coral-planter", -4, -1) } },
                ShopDayPhase.Night, out _);
            slot.formatVersion = format; slot.game.version = 2; slot.layout.version = layoutVersion; RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("decor-slot")] [TestCase("missing-display-slot")] [TestCase("stocked-display-reclassified-as-decor")]
        public void DecorDoesNotHideOrCreateStockOwnershipDuringReadOrWrite(string problem)
        {
            File.Copy(FixturePath(), PathFor()); var slot = Read(out _); slot.layout = AddLabDecor(slot.layout); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            var tanks = new List<TankSave>(slot.game.tanks);
            if (problem == "decor-slot") tanks.Add(new TankSave { tankId = "table-4-slot-1" });
            else if (problem == "missing-display-slot") tanks.RemoveAll(t => t.tankId == "table-3-slot-2");
            else slot.layout.items[2].definitionId = "coral-planter";
            slot.game.tanks = tanks.ToArray(); RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase(9, 3)] [TestCase(10, 2)] [TestCase(10, 1)]
        public void EnvelopeLayoutVersionsStayExplicit(int format, int layoutVersion)
        {
            var slot = FreshSlot(new ShopLayout().CaptureSave(), ShopDayPhase.Night, out _);
            Write(slot); Read(out _); slot.formatVersion = format; slot.game.version = 2; slot.layout.version = layoutVersion;
            RejectRead(JsonUtility.ToJson(slot));
        }

        [Test] public void InvalidDecorDefinitionIsRejectedBeforeAnyFileWork()
        {
            var slot = FreshSlot(new LayoutSave { version = 3, nextItemNumber = 2, items = new[] { Item(1, "coral-planter", -4, -1) } },
                ShopDayPhase.Night, out _); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            slot.layout.items[0].definitionId = "unreleased-decor"; RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
        }

        static LayoutItemSave Item(int number, string definition, int x, int z) => new LayoutItemSave
            { id = "table-" + number, definitionId = definition, x = x, z = z, quarterTurns = 0 };
        static LayoutSave AddLabDecor(LayoutSave save)
        {
            var items = new List<LayoutItemSave>(save.items); items.Add(Item(save.nextItemNumber, "coral-planter", 9, 4));
            return new LayoutSave { version = ShopLayout.SaveVersion, storeTier = save.storeTier, nextItemNumber = save.nextItemNumber + 1,
                items = items.ToArray(), facilities = new ShopLayout().CaptureSave().facilities };
        }
        int Appeal(SlotData slot)
        { Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True); return ShopAppeal.FromLayout(layout).Points; }

        SlotData HistoricalSlot(out GameModel model)
        {
            var slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(FixturePath()));
            Assert.That(slot.formatVersion, Is.EqualTo(9)); Assert.That(slot.layout.version, Is.EqualTo(2));
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), slot.game, out model, out _), Is.True); return slot;
        }
        SlotData FreshSlot(LayoutSave save, ShopDayPhase phase, out GameModel model)
        {
            Assert.That(ShopLayout.TryRestore(save, out var layout), Is.True, "New tests must start from valid geometry.");
            model = new GameModel(layout.WithTankIds(config)); var cycle = new ShopDayCycle();
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
