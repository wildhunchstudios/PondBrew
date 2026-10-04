using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class LabSaveTests
    {
        const string Prefix = "EvolutionShop-LabSaveTests-";
        static readonly string[] FacilityIds = { "lab-bloom", "lab-flow", "lab-incubator", "lab-upgrade" };
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

        [TestCase("V11DenseLab.json", 8, 12)]
        [TestCase("V11StoredFurniture.json", 2, 2)]
        public void FrozenFormatElevenMigratesReadOnlyWithCanonicalFacilitiesAndAllProgress(string fixture, int stock, int owned)
        {
            byte[] original = File.ReadAllBytes(Fixture(fixture)); File.WriteAllBytes(PathFor(), original);
            var expected = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture(fixture)));
            Assert.That(expected.formatVersion, Is.EqualTo(11)); Assert.That(expected.layout.version, Is.EqualTo(4));
            var loaded = Read(out var model); AssertSameState(expected, loaded, model);
            Assert.That(loaded.formatVersion, Is.EqualTo(12)); Assert.That(loaded.layout.version, Is.EqualTo(5));
            Assert.That(loaded.layout.labTier, Is.Zero); AssertCanonicalFacilities(loaded.layout);
            Assert.That(loaded.layout.items.Length, Is.EqualTo(owned));
            int occupied = 0; foreach (string tank in model.TankIds) if (model.GetTank(tank).HasValue) occupied++;
            Assert.That(occupied, Is.EqualTo(stock));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(Fixture(fixture)), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void FirstExplicitWriteArchivesExactVersionElevenOnceWhileRotatingBackupContinues()
        {
            byte[] original = File.ReadAllBytes(Fixture("V11StoredFurniture.json")); File.WriteAllBytes(PathFor(), original);
            var slot = Read(out _); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v11.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] first = File.ReadAllBytes(PathFor()); slot.volume = .21f; Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v11.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(first));
            Assert.That(Read(out _).volume, Is.EqualTo(.21f));
        }

        [Test] public void ExistingVersionElevenArchiveIsNeverOverwritten()
        {
            File.Copy(Fixture("V11StoredFurniture.json"), PathFor()); byte[] priorArchive = { 11, 4, 2 };
            File.WriteAllBytes(PathFor() + ".v11.bak", priorArchive); Write(Read(out _));
            Assert.That(File.ReadAllBytes(PathFor() + ".v11.bak"), Is.EqualTo(priorArchive));
        }

        [Test] public void FailedLegacyArchivePreservesPrimaryAndRotatingBackup()
        {
            byte[] original = File.ReadAllBytes(Fixture("V11StoredFurniture.json")); File.WriteAllBytes(PathFor(), original);
            byte[] backup = { 2, 11, 4 }; File.WriteAllBytes(PathFor() + ".bak", backup);
            var slot = Read(out _); Directory.CreateDirectory(PathFor() + ".v11.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("idle")] [TestCase("dosed")] [TestCase("incubating")] [TestCase("ready")]
        public void MigratingAndMovingExistingFacilitiesPreservesSingleProductionState(string phase)
        {
            var old = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture("V11StoredFurniture.json")));
            Assert.That(ShopLayout.TryRestore(old.layout, out var oldLayout), Is.True);
            Assert.That(GameModel.TryRestore(oldLayout.WithTankIds(config), old.game, out var before, out _), Is.True);
            Ok(before.BuyUpgrade()); before.Tick(100);
            if (phase != "idle") { Ok(before.Collect(CultureKind.Bloom)); Ok(before.Dose(CultureKind.Bloom)); }
            if (phase == "incubating" || phase == "ready")
            {
                Ok(before.Collect(CultureKind.Flow)); Ok(before.Dose(CultureKind.Flow)); Ok(before.BeginIncubation());
                before.Tick(phase == "ready" ? config.IncubationSeconds : 2.25f);
            }
            // This is still an actual v11 envelope: only its existing game state is varied.
            old.game = before.CaptureSave(); File.WriteAllText(PathFor(), JsonUtility.ToJson(old, true));
            byte[] legacy = File.ReadAllBytes(PathFor()); var slot = Read(out var model); AssertSameState(old, slot, model);
            Assert.That(ShopLayout.TryRestore(slot.layout, out var layout), Is.True);
            Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Success, Is.True);
            var expectedGame = model.CaptureSave();
            MoveAllNorth(layout, model, 8); Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expectedGame)));
            slot.layout = layout.CaptureSave(); slot.game = model.CaptureSave(); Write(slot);
            var loaded = Read(out var restored); AssertSameState(slot, loaded, restored);
            Assert.That(loaded.layout.labTier, Is.EqualTo(1)); Assert.That(restored.UpgradeOwned, Is.True);
            Assert.That(restored.Incubator.Phase, Is.EqualTo(before.Incubator.Phase));
            Assert.That(restored.Incubator.SpecimenId, Is.EqualTo(before.Incubator.SpecimenId));
            Assert.That(restored.Incubator.SecondsRemaining, Is.EqualTo(before.Incubator.SecondsRemaining));
            Assert.That(File.ReadAllBytes(PathFor() + ".v11.bak"), Is.EqualTo(legacy));
        }

        [TestCase(ShopDayPhase.Night)] [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void TierTwoMovedFacilitySavePreservesClockAndUnresolvedOrdersUntilSessionReconciles(ShopDayPhase phase)
        {
            var layout = new ShopLayout(); var seed = new GameModel(config).CaptureSave(); seed.currency = 600;
            Assert.That(GameModel.TryRestore(config, seed, out var model, out _), Is.True);
            Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Success, Is.True); MoveAllNorth(layout, model, 8);
            Assert.That(layout.TryExpandLab(model, ShopDayPhase.Night).Success, Is.True); MoveAllNorth(layout, model, 16);
            Produce(model); Ok(model.TransferToTank("tank-a")); string specimen = model.GetTank("tank-a").Value.Id;
            var cycle = new ShopDayCycle(); var reports = new ShopTradeLedger();
            if (phase != ShopDayPhase.Night)
            {
                Assert.That(cycle.OpenShop(), Is.True); cycle.Tick(23); Assert.That(reports.BeginDay(1, 1), Is.True);
                Assert.That(reports.BeginVisit("pending-lab-reload"), Is.True); Ok(model.ReserveTank("tank-a", "pending-lab-reload"));
                if (phase == ShopDayPhase.Closing) { Assert.That(cycle.RequestClose(), Is.True); cycle.Tick(.4f); }
            }
            var slot = new SlotData { game = model.CaptureSave(), layout = layout.CaptureSave(), cycle = cycle.CaptureSave(), trades = reports.CaptureSave() };
            Write(slot); byte[] primary = File.ReadAllBytes(PathFor()); var loaded = Read(out var restored); AssertSameState(slot, loaded, restored);
            Assert.That(restored.Currency, Is.EqualTo(420)); Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(specimen));
            Assert.That(restored.GetReservation("tank-a"), Is.Null); Assert.That(loaded.layout.labTier, Is.EqualTo(2));
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var loadedReports), Is.True);
            Assert.That(loadedReports.PendingCount, Is.EqualTo(phase == ShopDayPhase.Night ? 0 : 1));
            if (phase != ShopDayPhase.Night)
            { Assert.That(loadedReports.CancelPendingForReload(), Is.True); Assert.That(loadedReports.CancelPendingForReload(), Is.False); Assert.That(loadedReports.Current.interrupted, Is.EqualTo(1)); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("\"0\"")] [TestCase("false")]
        [TestCase("0.0")] [TestCase("0e0")] [TestCase("-1")] [TestCase("3")] [TestCase("duplicate")] [TestCase("nested-only")]
        public void CurrentEnvelopeRequiresDirectIntegerLabTier(string replacement)
        {
            var slot = ValidControl(); string layout = JsonUtility.ToJson(slot.layout);
            if (replacement == "missing") layout = layout.Replace("\"labTier\":0,", "");
            else if (replacement == "nested-only") layout = layout.Replace("\"labTier\":0", "\"metadata\":{\"labTier\":0}");
            else if (replacement == "duplicate") layout = layout.Replace("\"labTier\":0", "\"labTier\":0,\"\\u006cabTier\":0");
            else layout = layout.Replace("\"labTier\":0", "\"labTier\":" + replacement);
            RejectRead(ReplaceLayout(slot, layout));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("[]")] [TestCase("{}")]
        [TestCase("\"facilities\"")] [TestCase("duplicate")] [TestCase("nested-only")]
        public void CurrentEnvelopeRequiresDirectFourElementFacilityArray(string replacement)
        {
            var slot = ValidControl(); string layout = JsonUtility.ToJson(slot.layout);
            string field = "\"facilities\":" + FacilityArray(slot.layout.facilities);
            if (replacement == "missing") layout = RemoveField(layout, field);
            else if (replacement == "nested-only") layout = layout.Replace(field, "\"metadata\":{" + field + "}");
            else if (replacement == "duplicate") layout = layout.Replace(field, field + "," + field);
            else layout = layout.Replace(field, "\"facilities\":" + replacement);
            RejectRead(ReplaceLayout(slot, layout));
        }

        [TestCase("missing-id")] [TestCase("missing-x")] [TestCase("missing-z")] [TestCase("missing-turns")]
        [TestCase("numeric-id")] [TestCase("null-z")] [TestCase("string-turns")]
        [TestCase("fractional-x")] [TestCase("overflow-x")] [TestCase("duplicate-x")] [TestCase("nested-x")]
        public void EveryFacilityPoseRequiresUniqueDirectPrimitiveFields(string problem)
        {
            var slot = ValidControl(); string original = JsonUtility.ToJson(slot.layout.facilities[0]); string pose = original;
            switch (problem)
            {
                case "missing-id": pose = RemoveField(pose, "\"id\":\"lab-bloom\""); break;
                case "missing-x": pose = RemoveField(pose, "\"x\":0"); break;
                case "missing-z": pose = RemoveField(pose, "\"z\":0"); break;
                case "missing-turns": pose = RemoveField(pose, "\"quarterTurns\":0"); break;
                case "numeric-id": pose = pose.Replace("\"id\":\"lab-bloom\"", "\"id\":3"); break;
                case "null-z": pose = pose.Replace("\"z\":0", "\"z\":null"); break;
                case "string-turns": pose = pose.Replace("\"quarterTurns\":0", "\"quarterTurns\":\"0\""); break;
                case "fractional-x": pose = pose.Replace("\"x\":0", "\"x\":0.5"); break;
                case "overflow-x": pose = pose.Replace("\"x\":0", "\"x\":2147483648"); break;
                case "duplicate-x": pose = pose.Replace("\"x\":0", "\"x\":0,\"\\u0078\":0"); break;
                case "nested-x": pose = pose.Replace("\"x\":0", "\"metadata\":{\"x\":0}"); break;
            }
            Assert.That(pose, Is.Not.EqualTo(original)); RejectRead(JsonUtility.ToJson(slot).Replace(original, pose));
        }

        [TestCase("duplicate-id")] [TestCase("unknown-id")] [TestCase("invalid-turns")] [TestCase("out-of-bounds")]
        public void InvalidFacilitySemanticsRejectReadAndWriteWithoutReplacingFiles(string problem)
        {
            var slot = ValidControl(); Write(slot); byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            if (problem == "duplicate-id") slot.layout.facilities[1].id = "lab-bloom";
            else if (problem == "unknown-id") slot.layout.facilities[0].id = "lab-extra-incubator";
            else if (problem == "invalid-turns") slot.layout.facilities[0].quarterTurns = 4;
            else slot.layout.facilities[0].x = int.MaxValue;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
            string missing = Path.Combine(directory, "not-created", "slot.json");
            Assert.That(SaveStore.Write(missing, slot, out _), Is.False); Assert.That(Directory.Exists(Path.GetDirectoryName(missing)), Is.False);
            RejectRead(JsonUtility.ToJson(slot)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase(11, 5)] [TestCase(12, 4)]
        public void PreviousAndCurrentEnvelopeLayoutPairingsRemainStrict(int format, int version)
        {
            var slot = ValidControl(); slot.formatVersion = format; slot.layout.version = version; RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("tier")] [TestCase("pose")]
        public void LegacyLayoutCannotSmuggleLabExpansionOrRelocatedEquipment(string problem)
        {
            var slot = ValidControl(); slot.formatVersion = 11; slot.layout.version = 4;
            File.WriteAllText(PathFor(), JsonUtility.ToJson(slot)); AssertCanonicalFacilities(Read(out _).layout);
            if (problem == "tier") slot.layout.labTier = 1; else slot.layout.facilities[0].z = 1;
            RejectRead(JsonUtility.ToJson(slot));
        }

        [Test] public void EscapedDirectKeysAndUnrelatedNestedMetadataHaveAValidPositiveControl()
        {
            var slot = ValidControl(); string original = JsonUtility.ToJson(slot.layout.facilities[0]);
            string pose = original.Replace("\"x\":", "\"\\u0078\":").Insert(1, "\"metadata\":{\"x\":null,\"facilities\":[\"escaped \\\" } ]\"]},");
            string json = JsonUtility.ToJson(slot).Replace(original, pose).Replace("\"labTier\":", "\"\\u006cabTier\":");
            File.WriteAllText(PathFor(), json); byte[] primary = File.ReadAllBytes(PathFor());
            var loaded = Read(out var restored); AssertSameState(slot, loaded, restored);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
        }

        SlotData ValidControl()
        {
            var slot = new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
            Write(slot); Read(out _); return slot;
        }
        static string ReplaceLayout(SlotData slot, string layout) => JsonUtility.ToJson(slot).Replace(JsonUtility.ToJson(slot.layout), layout);
        static string RemoveField(string json, string field) => json.Contains(field + ",") ? json.Replace(field + ",", "") : json.Replace("," + field, "");
        static string FacilityArray(FacilitySave[] facilities)
        { var parts = new string[facilities.Length]; for (int i = 0; i < parts.Length; i++) parts[i] = JsonUtility.ToJson(facilities[i]); return "[" + string.Join(",", parts) + "]"; }
        static void AssertCanonicalFacilities(LayoutSave layout)
        {
            Assert.That(layout.facilities.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(layout.facilities[i].id, Is.EqualTo(FacilityIds[i])); Assert.That(layout.facilities[i].x, Is.Zero); Assert.That(layout.facilities[i].z, Is.Zero);
                Assert.That(layout.facilities[i].quarterTurns, Is.EqualTo(i == 3 ? 3 : 0));
            }
        }
        static void MoveAllNorth(ShopLayout layout, GameModel model, int z)
        { for (int i = 0; i < 4; i++) Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, FacilityIds[i], 0, z, i == 3 ? 3 : 0).Success, Is.True); }
        void AssertSameState(SlotData expected, SlotData actual, GameModel after)
        {
            Assert.That(ShopLayout.TryRestore(expected.layout, out var priorLayout), Is.True);
            Assert.That(GameModel.TryRestore(priorLayout.WithTankIds(config), expected.game, out var before, out _), Is.True);
            Assert.That(JsonUtility.ToJson(after.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(before.CaptureSave())));
            Assert.That(JsonUtility.ToJson(actual.layout), Is.EqualTo(JsonUtility.ToJson(priorLayout.CaptureSave())));
            Assert.That(JsonUtility.ToJson(actual.cycle), Is.EqualTo(JsonUtility.ToJson(expected.cycle)));
            Assert.That(ShopTradeLedger.TryRestore(expected.trades, expected.cycle, out var priorReports), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(actual.trades, actual.cycle, out var currentReports), Is.True);
            Assert.That(JsonUtility.ToJson(currentReports.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(priorReports.CaptureSave())));
            Assert.That(JsonUtility.ToJson(actual.reputation), Is.EqualTo(JsonUtility.ToJson(expected.reputation)));
            Assert.That(actual.sensitivity, Is.EqualTo(expected.sensitivity)); Assert.That(actual.volume, Is.EqualTo(expected.volume));
            Assert.That(actual.slapKey, Is.EqualTo(expected.slapKey)); Assert.That(actual.humorVolume, Is.EqualTo(expected.humorVolume)); Assert.That(actual.reducedGags, Is.EqualTo(expected.reducedGags));
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
        void Produce(GameModel model)
        {
            model.Tick(100); Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom));
            Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds);
        }
    }
}
