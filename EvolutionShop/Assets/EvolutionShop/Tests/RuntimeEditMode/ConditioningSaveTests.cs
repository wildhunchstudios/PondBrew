using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ConditioningSaveTests
    {
        const string Prefix = "EvolutionShop-ConditioningSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");
        string Fixture() => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V14Research.json");

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

        [Test] public void FrozenVersionFourteenMigratesReadOnlyWithoutInventingMachineOrChangingResearch()
        {
            byte[] bytes = File.ReadAllBytes(Fixture()); File.WriteAllBytes(PathFor(), bytes);
            var before = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture()));
            Assert.That(before.formatVersion, Is.EqualTo(14)); Assert.That(before.game.version, Is.EqualTo(4)); Assert.That(before.layout.version, Is.EqualTo(6));
            var after = Read(out var model); AssertState(before, after, model);
            Assert.That(after.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(after.game.version, Is.EqualTo(6)); Assert.That(after.layout.version, Is.EqualTo(7));
            Assert.That(model.Currency, Is.EqualTo(400)); Assert.That(model.SparkResearchUnlocked, Is.True);
            Assert.That(model.IsDiscovered("lanternfin"), Is.True); Assert.That(model.CaptureSave().conditioners, Is.Empty);
            Assert.That(model.GetTank("tank-b").Value.Id, Is.EqualTo("b5f94c97652d4df8bd706c8ad5d3563b"));
            Assert.That(model.GetStorage("table-1-storage-1").Value.Id, Is.EqualTo("fdcc9f4c814b47459d00c27d8732822d"));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes)); Assert.That(File.ReadAllBytes(Fixture()), Is.EqualTo(bytes));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test] public void FirstWriteArchivesExactVersionFourteenAndLaterWritesOnlyRotateOrdinaryBackup()
        {
            byte[] original = File.ReadAllBytes(Fixture()); File.WriteAllBytes(PathFor(), original);
            var slot = Read(out _); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v14.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] first = File.ReadAllBytes(PathFor()); slot.volume = .23f; Write(slot); Read(out _);
            Assert.That(File.ReadAllBytes(PathFor() + ".v14.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(first));
        }

        [Test] public void ExistingVersionFourteenArchiveIsImmutable()
        {
            File.Copy(Fixture(), PathFor()); byte[] archive = { 14, 4, 6 };
            File.WriteAllBytes(PathFor() + ".v14.bak", archive); Write(Read(out _));
            Assert.That(File.ReadAllBytes(PathFor() + ".v14.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedVersionFourteenArchivePreservesPrimaryAndRotatingBackup()
        {
            File.Copy(Fixture(), PathFor()); byte[] original = File.ReadAllBytes(PathFor()), backup = { 14, 14 };
            File.WriteAllBytes(PathFor() + ".bak", backup); var slot = Read(out _);
            Directory.CreateDirectory(PathFor() + ".v14.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("idle")] [TestCase("processing")] [TestCase("completed")]
        [TestCase("cancelled")] [TestCase("first-collected")] [TestCase("second-collected")]
        public void MachinePhasesAndPartialOutputSurviveRepeatedDiskReadsWithoutTimerOrIdentityChanges(string state)
        {
            var slot = Machine(out var model, out var layout, out string machine);
            if (state != "idle")
            {
                BeginBatch(model, machine, 2);
                if (state == "processing") model.Tick(2.75f);
                else if (state == "cancelled") Ok(model.CancelConditioning(machine));
                else model.Tick(12f);
                if (state == "first-collected" || state == "second-collected")
                {
                    int position = state == "first-collected" ? 0 : 1;
                    var saved = model.CaptureSave().conditioners[0];
                    Ok(model.TransferConditionerToStorage(machine, position, model.StorageIds[0], position == 0 ? saved.firstSpecimenId : saved.secondSpecimenId));
                }
            }
            Capture(slot, model, layout); Write(slot); byte[] bytes = File.ReadAllBytes(PathFor());
            for (int i = 0; i < 3; i++) { var after = Read(out var restored); AssertState(slot, after, restored); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }

        [Test] public void TwoResidentCancellationAfterReloadRefundsOneSparkExactlyOnce()
        {
            var slot = Machine(out var model, out var layout, out string machine); BeginBatch(model, machine, 2);
            model.Tick(4f); var active = model.CaptureSave().conditioners[0]; Capture(slot, model, layout); Write(slot);
            slot = Read(out model); Assert.That(model.Available(CultureKind.Spark), Is.Zero); Ok(model.CancelConditioning(machine));
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(1)); Assert.That(model.CancelConditioning(machine).Success, Is.False);
            var ready = model.CaptureSave().conditioners[0]; Assert.That(ready.firstSpecimenId, Is.EqualTo(active.firstSpecimenId));
            Assert.That(ready.secondSpecimenId, Is.EqualTo(active.secondSpecimenId)); Assert.That(ready.firstSpeciesId, Is.EqualTo("pondlet"));
            Capture(slot, model, layout); Write(slot); slot = Read(out model);
            Assert.That(model.CancelConditioning(machine).Success, Is.False); Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(1));
            Ok(model.TransferConditionerToStorage(machine, 0, model.StorageIds[0], active.firstSpecimenId));
            Ok(model.TransferConditionerToStorage(machine, 1, model.StorageIds[1], active.secondSpecimenId));
            Assert.That(model.CaptureSave().conditioners[0].phase, Is.EqualTo(ConditionerPhase.Idle));
        }

        [Test] public void ReadyOutputReloadTransfersAndSellsExactSpecimenOnceWithoutReplayingProcess()
        {
            var slot = Machine(out var model, out var layout, out string machine); BeginBatch(model, machine, 1);
            string id = model.CaptureSave().conditioners[0].firstSpecimenId; model.Tick(12f); Capture(slot, model, layout); Write(slot);
            slot = Read(out model); Ok(model.SellFromTank("tank-a")); int cash = model.Currency;
            Ok(model.TransferConditionerToTank(machine, 0, "tank-a", id));
            Assert.That(model.TransferConditionerToTank(machine, 0, "tank-a", id).Success, Is.False);
            Ok(model.ReserveTank("tank-a", "conditioning-buyer")); Ok(model.SellReservedTank("tank-a", "conditioning-buyer", id));
            Capture(slot, model, layout); Write(slot); slot = Read(out model);
            Assert.That(model.Currency, Is.EqualTo(cash + 14)); Assert.That(model.GetTank("tank-a"), Is.Null);
            Assert.That(model.SellReservedTank("tank-a", "conditioning-buyer", id).Success, Is.False);
            Assert.That(model.TransferConditionerToTank(machine, 0, "tank-a", id).Success, Is.False);
            model.Tick(120f); Assert.That(model.CaptureSave().conditioners[0].phase, Is.EqualTo(ConditionerPhase.Idle));
            Assert.That(model.Currency, Is.EqualTo(cash + 14));
        }

        [Test] public void IndependentIncubatorAndConditioningProgressPersistTogetherWithoutOfflineAdvance()
        {
            var slot = Machine(out var model, out var layout, out string machine); BeginBatch(model, machine, 2);
            foreach (var culture in new[] { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark }) { Ok(model.Collect(culture)); Ok(model.Dose(culture)); }
            Ok(model.BeginIncubation()); model.Tick(3.25f); Capture(slot, model, layout); Write(slot);
            var restoredSlot = Read(out var restored); AssertState(slot, restoredSlot, restored);
            Assert.That(restored.Incubator.SecondsRemaining, Is.EqualTo(6.75f));
            Assert.That(restored.CaptureSave().conditioners[0].secondsRemaining, Is.EqualTo(8.75f));
            restored.Tick(8.75f); Assert.That(restored.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(restored.CaptureSave().conditioners[0].phase, Is.EqualTo(ConditionerPhase.Ready));
            Assert.That(restored.Incubator.SpecimenId, Is.Not.EqualTo(restored.CaptureSave().conditioners[0].firstSpecimenId));
        }

        [Test] public void EmptyFurnitureStorageRedeployAndSalePersistTheStableMachineIdWithoutReusingIt()
        {
            var slot = Machine(out var model, out var layout, out string machine); int paidCash = model.Currency;
            Assert.That(layout.TryStore(model, ShopDayPhase.Night, machine).Success, Is.True);
            Capture(slot, model, layout); Write(slot); slot = Read(out model); Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(model.CaptureSave().conditioners, Is.Empty); Assert.That(layout.GetItem(machine).Value.Stored, Is.True);
            var item = layout.GetItem(machine).Value;
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, machine, item.X, item.Z, item.QuarterTurns, out string same).Success, Is.True);
            Assert.That(same, Is.EqualTo(machine)); Assert.That(model.Currency, Is.EqualTo(paidCash));
            Capture(slot, model, layout); Write(slot); slot = Read(out model); Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            Assert.That(model.CaptureSave().conditioners[0].conditionerId, Is.EqualTo(machine));
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, machine).Success, Is.True); int refunded = model.Currency;
            Capture(slot, model, layout); Write(slot); Read(out model);
            Assert.That(layout.TrySell(model, ShopDayPhase.Night, machine).Success, Is.False); Assert.That(model.Currency, Is.EqualTo(refunded));
            Assert.That(layout.CaptureSave().nextItemNumber, Is.EqualTo(3)); Assert.That(model.CaptureSave().conditioners, Is.Empty);
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("object")] [TestCase("nested-only")]
        public void EvenAnEmptyCurrentMachineRegistryMustBeExplicit(string problem)
        {
            var slot = FromFixture(out _); Write(slot); string json = JsonUtility.ToJson(slot); const string field = "\"conditioners\":[]";
            Assert.That(json, Does.Contain(field));
            json = problem == "missing" ? RemoveField(json, field) : json.Replace(field,
                problem == "nested-only" ? "\"metadata\":{" + field + "}" : "\"conditioners\":" + (problem == "null" ? "null" : "{}"));
            RejectRead(json);
        }

        [TestCase("conditionerId")] [TestCase("phase")] [TestCase("secondsRemaining")]
        [TestCase("firstSpecimenId")] [TestCase("firstSpeciesId")] [TestCase("secondSpecimenId")] [TestCase("secondSpeciesId")]
        public void EveryMachineFieldMustBeExplicitAtItsOwnObjectLevel(string key)
        {
            var slot = Machine(out var model, out var layout, out _); Capture(slot, model, layout);
            string record = JsonUtility.ToJson(slot.game.conditioners[0]); string field = Field(record, key);
            RejectRead(JsonUtility.ToJson(slot).Replace(record, RemoveField(record, field)));
        }

        [TestCase("conditionerId", "null")] [TestCase("phase", "null")] [TestCase("phase", "0.0")]
        [TestCase("phase", "\"0\"")] [TestCase("secondsRemaining", "null")] [TestCase("secondsRemaining", "\"0\"")]
        [TestCase("secondsRemaining", "1e999")] [TestCase("firstSpecimenId", "0")]
        [TestCase("firstSpeciesId", "false")] [TestCase("secondSpecimenId", "[]")] [TestCase("secondSpeciesId", "{}")]
        public void MalformedMachinePrimitiveTypesCannotNormalizeIntoAnIdleMachine(string key, string replacement)
        {
            var slot = Machine(out var model, out var layout, out _); Capture(slot, model, layout);
            string record = JsonUtility.ToJson(slot.game.conditioners[0]);
            RejectRead(JsonUtility.ToJson(slot).Replace(record, record.Replace(Field(record, key), "\"" + key + "\":" + replacement)));
        }

        [TestCase("conditionerId")] [TestCase("phase")] [TestCase("secondsRemaining")]
        [TestCase("firstSpecimenId")] [TestCase("firstSpeciesId")] [TestCase("secondSpecimenId")] [TestCase("secondSpeciesId")]
        public void DuplicateMachineKeysRejectEvenWhenTheValuesAgree(string key)
        {
            var slot = Machine(out var model, out var layout, out _); Capture(slot, model, layout);
            string record = JsonUtility.ToJson(slot.game.conditioners[0]); string field = Field(record, key);
            RejectRead(JsonUtility.ToJson(slot).Replace(record, record.Replace(field, field + "," + field)));
        }

        [TestCase("orphan")] [TestCase("missing-machine")] [TestCase("duplicate-machine")] [TestCase("duplicate-resident")]
        [TestCase("rack-duplicate")] [TestCase("display-duplicate")] [TestCase("invalid-phase")]
        [TestCase("zero-active-timer")] [TestCase("excess-time")] [TestCase("mixed-ready")]
        [TestCase("missing-process-discovery")] [TestCase("return-capacity")]
        public void ImpossibleStateAndOwnershipRejectReadAndWriteWithoutTouchingGoodFiles(string problem)
        {
            var slot = Machine(out var model, out var layout, out string machine); BeginBatch(model, machine, 2);
            Capture(slot, model, layout); Write(slot); Write(slot); byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            var vat = slot.game.conditioners[0];
            if (problem == "orphan") vat.conditionerId = "table-999";
            else if (problem == "missing-machine") slot.game.conditioners = Array.Empty<ConditionerSave>();
            else if (problem == "duplicate-machine") slot.game.conditioners = new[] { vat, vat };
            else if (problem == "duplicate-resident") vat.secondSpecimenId = vat.firstSpecimenId;
            else if (problem == "rack-duplicate") { slot.game.storage[0].specimenId = vat.firstSpecimenId; slot.game.storage[0].speciesId = "pondlet"; }
            else if (problem == "display-duplicate") { slot.game.tanks[0].specimenId = vat.firstSpecimenId; slot.game.tanks[0].speciesId = "pondlet"; }
            else if (problem == "invalid-phase") vat.phase = (ConditionerPhase)3;
            else if (problem == "zero-active-timer") vat.secondsRemaining = 0;
            else if (problem == "excess-time") vat.secondsRemaining = 12.5f;
            else if (problem == "mixed-ready") { vat.phase = ConditionerPhase.Ready; vat.secondsRemaining = 0; vat.secondSpeciesId = "lanternfin"; }
            else if (problem == "missing-process-discovery")
            {
                slot.game.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin" };
                foreach (var tank in slot.game.tanks) { tank.specimenId = null; tank.speciesId = null; }
            }
            else slot.game.availableBloom = config.BaseCapacity;
            RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
        }

        [Test] public void VersionFourteenCannotSmuggleFutureMachineStateOrFurniture()
        {
            var legacy = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture()));
            legacy.game.conditioners = new[] { new ConditionerSave { conditionerId = "table-2" } };
            RejectRead(JsonUtility.ToJson(legacy));
            var slot = Machine(out var model, out var layout, out _); Capture(slot, model, layout);
            slot.formatVersion = 14; slot.game.version = 4; slot.game.conditioners = Array.Empty<ConditionerSave>(); slot.layout.version = 6;
            RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void PlacedAndStoredOwnershipRequireResearchAndProcessDiscovery(bool stored, bool researchUnlocked)
        {
            var slot = Machine(out var model, out var layout, out string machine);
            if (stored) Assert.That(layout.TryStore(model, ShopDayPhase.Night, machine).Success, Is.True);
            Capture(slot, model, layout); Write(slot); Write(slot);
            byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            slot.game.sparkResearchUnlocked = researchUnlocked;
            slot.game.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin" };
            foreach (var resident in slot.game.tanks) { resident.specimenId = null; resident.speciesId = null; }
            foreach (var resident in slot.game.storage) { resident.specimenId = null; resident.speciesId = null; }
            if (!researchUnlocked)
                foreach (var grower in slot.game.growers)
                    if (grower.cultureId == "spark") { grower.units = 0; grower.secondsUntilNextUnit = config.GrowerRefillSeconds; }
            // The game alone cannot see stored ownership. Its otherwise valid empty state
            // proves the envelope rejects the missing purchase prerequisites specifically.
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), slot.game, out _, out _), Is.True);
            RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("research-flag")] [TestCase("spark-amount")] [TestCase("storage-array")]
        public void VersionFourteenStillUsesItsOriginalResearchAndStorageShapeGuards(string problem)
        {
            string json = File.ReadAllText(Fixture());
            // Serialize only to obtain compact mutation tokens; preserve the fixture itself untouched.
            var slot = JsonUtility.FromJson<SlotData>(json); json = JsonUtility.ToJson(slot);
            if (problem == "research-flag") json = RemoveField(json, "\"sparkResearchUnlocked\":true");
            else if (problem == "spark-amount") json = json.Replace("\"availableSpark\":0", "\"availableSpark\":null");
            else json = RemoveField(json, "\"storage\":" + StorageArray(slot.game.storage));
            RejectRead(json);
        }

        [TestCase(14, 5, 6)] [TestCase(14, 4, 7)] [TestCase(15, 4, 7)] [TestCase(15, 5, 6)]
        public void EnvelopeGameAndLayoutPairingsRemainFrozen(int envelope, int game, int layoutVersion)
        {
            var slot = FromFixture(out _); slot.formatVersion = envelope; slot.game.version = game; slot.layout.version = layoutVersion;
            RejectRead(JsonUtility.ToJson(slot));
        }

        [Test] public void EscapedMachineKeysAndNestedDecoysHaveAValidPositiveControl()
        {
            var slot = Machine(out var model, out var layout, out _); Capture(slot, model, layout);
            string json = JsonUtility.ToJson(slot).Replace("\"conditionerId\":", "\"\\u0063onditionerId\":")
                .Replace("\"conditioners\":", "\"ignored\":{\"conditioners\":null,\"text\":\"escaped \\\" } ]\"},\"conditioners\":");
            File.WriteAllText(PathFor(), json); byte[] bytes = File.ReadAllBytes(PathFor());
            var after = Read(out var restored); AssertState(slot, after, restored); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }

        SlotData Machine(out GameModel model, out ShopLayout layout, out string machine)
        {
            var slot = FromFixture(out model); Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            // Preserve both original Lanternfin residents while freeing the two rack inputs.
            var resident = model.GetStorage(model.StorageIds[0]).Value;
            Ok(model.TransferStorageToTank(model.StorageIds[0], "tank-a", resident.Id));
            var result = layout.TryCommit(model, ShopDayPhase.Night, "conditioning-vat", null, 9, 13, 0, out machine);
            Assert.That(result.Success, Is.True, result.Failure.ToString());
            Capture(slot, model, layout); return slot;
        }
        void BeginBatch(GameModel model, string machine, int count)
        {
            var sources = new string[count]; var ids = new string[count];
            for (int i = 0; i < count; i++)
            {
                model.Tick(20f); Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom));
                Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds);
                sources[i] = model.StorageIds[i]; ids[i] = model.Incubator.SpecimenId; Ok(model.StoreReadyOutput(sources[i]));
            }
            Ok(model.Collect(CultureKind.Spark)); Ok(model.BeginConditioning(machine, sources, ids));
            Assert.That(model.Available(CultureKind.Spark), Is.Zero);
            Assert.That(model.ReservedCultureCount, Is.EqualTo(1));
        }
        SlotData FromFixture(out GameModel model) { File.WriteAllBytes(PathFor(), File.ReadAllBytes(Fixture())); return Read(out model); }
        static void Capture(SlotData slot, GameModel model, ShopLayout layout) { slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); }
        void AssertState(SlotData before, SlotData after, GameModel actual)
        {
            Assert.That(ShopLayout.TryRestore(before.layout, out var layout), Is.True);
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), before.game, out var expected, out _), Is.True);
            Assert.That(JsonUtility.ToJson(actual.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expected.CaptureSave())));
            Assert.That(JsonUtility.ToJson(after.layout), Is.EqualTo(JsonUtility.ToJson(layout.CaptureSave())));
            Assert.That(JsonUtility.ToJson(after.cycle), Is.EqualTo(JsonUtility.ToJson(before.cycle)));
            Assert.That(ShopTradeLedger.TryRestore(before.trades, before.cycle, out var oldReport), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(after.trades, after.cycle, out var newReport), Is.True);
            Assert.That(JsonUtility.ToJson(newReport.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(oldReport.CaptureSave())));
            Assert.That(JsonUtility.ToJson(after.reputation), Is.EqualTo(JsonUtility.ToJson(before.reputation)));
            Assert.That(after.sensitivity, Is.EqualTo(before.sensitivity)); Assert.That(after.volume, Is.EqualTo(before.volume));
            Assert.That(after.slapKey, Is.EqualTo(before.slapKey)); Assert.That(after.humorVolume, Is.EqualTo(before.humorVolume)); Assert.That(after.reducedGags, Is.EqualTo(before.reducedGags));
        }
        static string StorageArray(StorageSave[] entries) { var json = new string[entries.Length]; for (int i = 0; i < entries.Length; i++) json[i] = JsonUtility.ToJson(entries[i]); return "[" + string.Join(",", json) + "]"; }
        static string Field(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\":(?:\"(?:\\\\.|[^\"])*\"|[^,}]+)");
            Assert.That(match.Success, Is.True, key); return match.Value;
        }
        static string RemoveField(string json, string field) => json.Contains(field + ",") ? json.Replace(field + ",", "") : json.Replace("," + field, "");
        void RejectRead(string json)
        {
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.False);
            Assert.That(model, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }
        void RejectWrite(SlotData slot, byte[] primary, byte[] backup)
        {
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
            string missing = Path.Combine(directory, "not-created", "slot.json");
            Assert.That(SaveStore.Write(missing, slot, out _), Is.False); Assert.That(Directory.Exists(Path.GetDirectoryName(missing)), Is.False);
        }
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
        SlotData Read(out GameModel model) { Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error); return slot; }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
