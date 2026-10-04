using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ResearchSaveTests
    {
        const string Prefix = "EvolutionShop-ResearchSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");
        string Fixture() => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V13SpecimenStorage.json");
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

        [Test] public void FrozenVersionThirteenMigratesReadOnlyWithResearchLockedAndExistingProgressExact()
        {
            byte[] original = File.ReadAllBytes(Fixture()); File.WriteAllBytes(PathFor(), original);
            var before = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture()));
            Assert.That(before.formatVersion, Is.EqualTo(13)); Assert.That(before.game.version, Is.EqualTo(3)); Assert.That(before.layout.version, Is.EqualTo(6));
            var after = Read(out var model); AssertState(before, after, model);
            Assert.That(after.formatVersion, Is.EqualTo(14)); Assert.That(after.game.version, Is.EqualTo(4)); Assert.That(after.layout.version, Is.EqualTo(6));
            Assert.That(model.SparkResearchReady, Is.True); Assert.That(model.SparkResearchUnlocked, Is.False);
            Assert.That(model.Currency, Is.EqualTo(224)); Assert.That(model.Available(CultureKind.Spark), Is.Zero); Assert.That(model.Staged(CultureKind.Spark), Is.Zero);
            Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.Zero); Assert.That(model.GetGrower(CultureKind.Spark).SecondsToNextUnit, Is.EqualTo(config.GrowerRefillSeconds));
            Assert.That(model.StorageIds.Length, Is.EqualTo(4)); Assert.That(model.Incubator.SpecimenId, Is.EqualTo(before.game.incubator.specimenId));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(Fixture()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test] public void FirstExplicitWriteKeepsExactVersionThirteenArchiveAcrossResearchAndRepeatedWrites()
        {
            byte[] original = File.ReadAllBytes(Fixture()); File.WriteAllBytes(PathFor(), original);
            var slot = Read(out var model); Ok(model.BuySparkResearch()); slot.game = model.CaptureSave(); Write(slot);
            Assert.That(File.ReadAllBytes(PathFor() + ".v13.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(original));
            byte[] first = File.ReadAllBytes(PathFor()); slot.volume = .21f; Write(slot); Read(out _);
            Assert.That(File.ReadAllBytes(PathFor() + ".v13.bak"), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(first));
        }

        [Test] public void ExistingVersionThirteenArchiveIsNeverOverwritten()
        {
            File.Copy(Fixture(), PathFor()); byte[] archive = { 13, 3, 6 }; File.WriteAllBytes(PathFor() + ".v13.bak", archive);
            Write(Read(out _)); Assert.That(File.ReadAllBytes(PathFor() + ".v13.bak"), Is.EqualTo(archive));
        }

        [Test] public void FailedVersionThirteenArchivePreservesPrimaryAndRotatingBackup()
        {
            byte[] original = File.ReadAllBytes(Fixture()); File.WriteAllBytes(PathFor(), original);
            byte[] backup = { 13, 13 }; File.WriteAllBytes(PathFor() + ".bak", backup); var slot = Read(out _);
            Directory.CreateDirectory(PathFor() + ".v13.bak"); Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original)); Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(backup));
        }

        [Test] public void ResearchPaymentAndInitialSparkStockPersistOnceWithoutResetOnReload()
        {
            var slot = FromFixture(out var model); string originalOutput = model.Incubator.SpecimenId;
            Ok(model.BuySparkResearch()); Assert.That(model.Currency, Is.EqualTo(176));
            Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.EqualTo(config.InitialGrowerUnits));
            Ok(model.Collect(CultureKind.Spark)); model.Tick(.75f); slot.game = model.CaptureSave(); Write(slot);
            var loaded = Read(out model); AssertState(slot, loaded, model);
            Assert.That(model.BuySparkResearch().Success, Is.False); Assert.That(model.Currency, Is.EqualTo(176));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(originalOutput)); Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(1));
            Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.EqualTo(config.InitialGrowerUnits - 1));
            Assert.That(model.GetGrower(CultureKind.Spark).SecondsToNextUnit, Is.EqualTo(1.25f));
            Write(loaded); var twice = Read(out model); AssertState(slot, twice, model);
        }

        [TestCase(IncubatorPhase.Dosing)] [TestCase(IncubatorPhase.Incubating)] [TestCase(IncubatorPhase.Ready)]
        public void SparkProductionAndOldInventoryRemainExactAcrossRepeatedReads(IncubatorPhase phase)
        {
            var slot = Unlocked(out var model); Ok(model.TransferToTank("tank-a"));
            Ok(model.Collect(CultureKind.Spark)); Ok(model.Dose(CultureKind.Spark));
            if (phase != IncubatorPhase.Dosing)
            {
                Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow));
                Ok(model.BeginIncubation()); model.Tick(phase == IncubatorPhase.Ready ? config.IncubationSeconds : 2.75f);
            }
            slot.game = model.CaptureSave(); Write(slot); byte[] bytes = File.ReadAllBytes(PathFor());
            for (int i = 0; i < 3; i++)
            { var loaded = Read(out var after); AssertState(slot, loaded, after); Assert.That(after.Incubator.Phase, Is.EqualTo(phase)); }
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }

        [TestCase("output")] [TestCase("display")] [TestCase("rack")]
        public void NewSpeciesOwnershipDiscoveryAndPriceRoundTripInEveryDestination(string destination)
        {
            var slot = Unlocked(out var model); Ok(model.TransferToTank("tank-a")); ProduceLanternfin(model);
            string id = model.Incubator.SpecimenId; Ok(model.SetPrice("lanternfin", 16)); string storage = EmptyStorage(model);
            if (destination == "display") Ok(model.TransferToTank("tank-b"));
            else if (destination == "rack") Ok(model.StoreReadyOutput(storage));
            slot.game = model.CaptureSave(); Write(slot); var loaded = Read(out model); AssertState(slot, loaded, model);
            Assert.That(model.GetPrice("lanternfin").Value.Price, Is.EqualTo(16)); Assert.That(model.IsDiscovered("lanternfin"), Is.True);
            string restoredId = destination == "output" ? model.Incubator.SpecimenId : destination == "display" ? model.GetTank("tank-b").Value.Id : model.GetStorage(storage).Value.Id;
            Assert.That(restoredId, Is.EqualTo(id)); Assert.That(model.Currency, Is.EqualTo(176));
        }

        [TestCase(ShopDayPhase.Open)] [TestCase(ShopDayPhase.Closing)]
        public void PendingReloadRetainsResearchNewStoredSpeciesAndClockWithoutPayingAgain(ShopDayPhase phase)
        {
            var slot = Unlocked(out var model); Ok(model.TransferToTank("tank-a")); ProduceLanternfin(model); Ok(model.StoreReadyOutput(EmptyStorage(model)));
            Assert.That(ShopDayCycle.TryRestore(slot.cycle, out var day), Is.True); Assert.That(day.OpenShop(), Is.True); day.Tick(19);
            Assert.That(ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var reports), Is.True); Assert.That(reports.BeginDay(day.DayNumber, 1), Is.True);
            Assert.That(reports.BeginVisit("research-reload"), Is.True); Ok(model.ReserveTank("tank-a", "research-reload"));
            if (phase == ShopDayPhase.Closing) { Assert.That(day.RequestClose(), Is.True); day.Tick(.4f); }
            slot.game = model.CaptureSave(); slot.cycle = day.CaptureSave(); slot.trades = reports.CaptureSave(); Write(slot);
            var loaded = Read(out model); AssertState(slot, loaded, model); Assert.That(model.GetReservation("tank-a"), Is.Null);
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var restoredReports), Is.True);
            Assert.That(restoredReports.CancelPendingForReload(), Is.True); Assert.That(restoredReports.CancelPendingForReload(), Is.False);
            Assert.That(restoredReports.Current.interrupted, Is.EqualTo(1)); Assert.That(model.Currency, Is.EqualTo(176)); Assert.That(model.BuySparkResearch().Success, Is.False);
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("0")]
        [TestCase("\"false\"")] [TestCase("duplicate")] [TestCase("nested-only")]
        public void CurrentResearchFlagMustBeAnExplicitDirectBoolean(string replacement)
        {
            var slot = ValidControl(); string json = JsonUtility.ToJson(slot); const string field = "\"sparkResearchUnlocked\":false";
            if (replacement == "missing") json = RemoveField(json, field);
            else if (replacement == "duplicate") json = json.Replace(field, field + ",\"\\u0073parkResearchUnlocked\":false");
            else if (replacement == "nested-only") json = json.Replace(field, "\"metadata\":{" + field + "}");
            else json = json.Replace(field, "\"sparkResearchUnlocked\":" + replacement);
            RejectRead(json);
        }

        [TestCase("availableSpark", "missing")] [TestCase("availableSpark", "null")]
        [TestCase("availableSpark", "\"0\"")] [TestCase("availableSpark", "0.0")]
        [TestCase("stagedSpark", "missing")] [TestCase("stagedSpark", "null")]
        [TestCase("stagedSpark", "\"0\"")] [TestCase("stagedSpark", "0.0")]
        public void CurrentSparkAmountsMustBeExplicitIntegers(string key, string replacement)
        {
            var slot = ValidControl(); string json = JsonUtility.ToJson(slot); string field = "\"" + key + "\":0";
            json = replacement == "missing" ? RemoveField(json, field) : json.Replace(field, "\"" + key + "\":" + replacement);
            RejectRead(json);
        }

        [TestCase("missing-array")] [TestCase("null-array")] [TestCase("two-growers")]
        [TestCase("missing-id")] [TestCase("missing-units")] [TestCase("null-units")]
        [TestCase("missing-timer")] [TestCase("quoted-timer")]
        public void CurrentSaveRequiresThreeExplicitCompleteGrowerRecords(string problem)
        {
            var slot = ValidControl(); string json = JsonUtility.ToJson(slot); string original = JsonUtility.ToJson(slot.game.growers[2]); string grower = original;
            if (problem == "missing-array" || problem == "null-array" || problem == "two-growers")
            {
                string field = "\"growers\":" + GrowerArray(slot.game.growers);
                json = problem == "missing-array" ? RemoveField(json, field) : json.Replace(field,
                    "\"growers\":" + (problem == "null-array" ? "null" : GrowerArray(new[] { slot.game.growers[0], slot.game.growers[1] })));
            }
            else
            {
                if (problem == "missing-id") grower = RemoveField(grower, "\"cultureId\":\"spark\"");
                else if (problem == "missing-units") grower = RemoveField(grower, "\"units\":0");
                else if (problem == "null-units") grower = grower.Replace("\"units\":0", "\"units\":null");
                else
                {
                    string timer = "\"secondsUntilNextUnit\":" + JsonUtility.ToJson(new TimerField { secondsUntilNextUnit = config.GrowerRefillSeconds }).Split(':')[1].TrimEnd('}');
                    grower = problem == "missing-timer" ? RemoveField(grower, timer) : grower.Replace(timer, "\"secondsUntilNextUnit\":\"2\"");
                }
                Assert.That(grower, Is.Not.EqualTo(original)); json = json.Replace(original, grower);
            }
            RejectRead(json);
        }

        [TestCase("prerequisite")] [TestCase("locked-carried")] [TestCase("locked-staged")]
        [TestCase("locked-grower")] [TestCase("capacity-includes-spark")] [TestCase("duplicate-grower")]
        public void InvalidResearchStateRejectsReadAndWriteWithoutReplacingFiles(string problem)
        {
            var slot = problem == "prerequisite" || problem == "capacity-includes-spark" ? Unlocked(out _) : FromFixture(out _);
            Write(slot); Write(slot); byte[] primary = File.ReadAllBytes(PathFor()), backup = File.ReadAllBytes(PathFor() + ".bak");
            if (problem == "prerequisite")
            {
                slot.game.discoveries = new[] { "bubblebelly", "pondlet" };
                foreach (var resident in slot.game.storage)
                    if (resident.speciesId == "ribbonfin") { resident.speciesId = null; resident.specimenId = null; }
            }
            else if (problem == "locked-carried") slot.game.availableSpark = 1;
            else if (problem == "locked-staged") { slot.game.incubator.phase = IncubatorPhase.Dosing; slot.game.incubator.speciesId = null; slot.game.incubator.specimenId = null; slot.game.incubator.stagedSpark = 1; }
            else if (problem == "locked-grower") slot.game.growers[2].units = 1;
            else if (problem == "capacity-includes-spark") { slot.game.availableBloom = 3; slot.game.availableSpark = 2; }
            else slot.game.growers[2].cultureId = "flow";
            RejectWrite(slot, primary, backup); RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("flag")] [TestCase("carried")] [TestCase("staged")] [TestCase("new-active-species")]
        public void VersionThirteenCannotSmuggleFutureResearchOrSpecies(string problem)
        {
            var slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture()));
            if (problem == "flag") slot.game.sparkResearchUnlocked = true;
            else if (problem == "carried") slot.game.availableSpark = 1;
            else if (problem == "staged") slot.game.incubator.stagedSpark = 1;
            else { slot.game.incubator.phase = IncubatorPhase.Incubating; slot.game.incubator.speciesId = "lanternfin"; slot.game.incubator.secondsRemaining = 3; }
            RejectRead(JsonUtility.ToJson(slot));
        }

        [TestCase("missing-array")] [TestCase("null-array")] [TestCase("missing-entry-id")]
        public void VersionThirteenStillRequiresItsOriginalStorageShape(string problem)
        {
            var slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture())); string json = JsonUtility.ToJson(slot);
            string storage = "\"storage\":" + StorageArray(slot.game.storage);
            if (problem == "missing-array") json = RemoveField(json, storage);
            else if (problem == "null-array") json = json.Replace(storage, "\"storage\":null");
            else
            { string original = JsonUtility.ToJson(slot.game.storage[0]); json = json.Replace(original, RemoveField(original, "\"storageId\":\"" + slot.game.storage[0].storageId + "\"")); }
            RejectRead(json);
        }

        [TestCase(13, 4)] [TestCase(14, 3)] [TestCase(14, 2)] [TestCase(14, 5)]
        public void NewAndHistoricalGameMappingsRemainExplicit(int envelope, int version)
        {
            var slot = ValidControl(); slot.formatVersion = envelope;
            slot.game = version < 4 ? LegacyGameSaveFixture.AsVersion(slot.game, version) : slot.game;
            slot.game.version = version == 5 ? GameModel.SaveVersion + 1 : version; RejectRead(JsonUtility.ToJson(slot));
        }

        [Test] public void EscapedResearchKeysNestedNoiseAndNumericGrowerTimerHaveAValidControl()
        {
            var slot = ValidControl(); string json = JsonUtility.ToJson(slot)
                .Replace("\"sparkResearchUnlocked\":", "\"\\u0073parkResearchUnlocked\":")
                .Replace("\"availableSpark\":", "\"metadata\":{\"availableSpark\":null,\"growers\":[\"escaped \\\" } ]\"]},\"availableSpark\":")
                .Replace("\"stagedSpark\":", "\"\\u0073tagedSpark\":");
            File.WriteAllText(PathFor(), json); byte[] bytes = File.ReadAllBytes(PathFor()); var loaded = Read(out var model); AssertState(slot, loaded, model);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }

        [Serializable] sealed class TimerField { public float secondsUntilNextUnit; }
        static string GrowerArray(GrowerSave[] values) { var parts = new string[values.Length]; for (int i = 0; i < parts.Length; i++) parts[i] = JsonUtility.ToJson(values[i]); return "[" + string.Join(",", parts) + "]"; }
        static string StorageArray(StorageSave[] values) { var parts = new string[values.Length]; for (int i = 0; i < parts.Length; i++) parts[i] = JsonUtility.ToJson(values[i]); return "[" + string.Join(",", parts) + "]"; }
        static string RemoveField(string json, string field) => json.Contains(field + ",") ? json.Replace(field + ",", "") : json.Replace("," + field, "");
        SlotData FromFixture(out GameModel model) { File.WriteAllBytes(PathFor(), File.ReadAllBytes(Fixture())); return Read(out model); }
        SlotData Unlocked(out GameModel model) { var slot = FromFixture(out model); Ok(model.BuySparkResearch()); slot.game = model.CaptureSave(); return slot; }
        SlotData ValidControl() { var slot = FromFixture(out _); Write(slot); Read(out _); return slot; }
        string EmptyStorage(GameModel model) { foreach (string id in model.StorageIds) if (!model.GetStorage(id).HasValue) return id; Assert.Fail("Fixture requires a free rack slot."); return null; }
        void ProduceLanternfin(GameModel model)
        {
            model.Tick(100);
            foreach (var culture in new[] { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark }) { Ok(model.Collect(culture)); Ok(model.Dose(culture)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Assert.That(model.Incubator.SpeciesId, Is.EqualTo("lanternfin"));
        }
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
            Assert.That(after.reputation.points, Is.EqualTo(before.reputation.points)); Assert.That(after.sensitivity, Is.EqualTo(before.sensitivity)); Assert.That(after.volume, Is.EqualTo(before.volume));
            Assert.That(after.slapKey, Is.EqualTo(before.slapKey)); Assert.That(after.humorVolume, Is.EqualTo(before.humorVolume)); Assert.That(after.reducedGags, Is.EqualTo(before.reducedGags));
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
            Assert.That(model, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(primary));
        }
        void Write(SlotData slot) => Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
        SlotData Read(out GameModel model) { Assert.That(SaveStore.Read(PathFor(), config, out model, out var slot, out var error), Is.True, error); return slot; }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
