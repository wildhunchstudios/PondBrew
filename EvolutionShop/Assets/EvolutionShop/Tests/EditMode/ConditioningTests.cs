using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ConditioningTests
    {
        [TestCase(1)] [TestCase(2)]
        public void OneSparkRunsEitherBatchWithoutChangingIdentityIncomeOrDiscovery(int count)
        {
            var model = Fixture(); var known = model.DiscoveredSpeciesIds;
            Begin(model, count);
            Assert.That(model.Available(CultureKind.Spark), Is.Zero);
            Assert.That(model.ReservedCultureCount, Is.EqualTo(1));
            Assert.That(model.GetStorage("rack-1"), Is.Null);
            Assert.That(model.GetStorage("rack-2").HasValue, Is.EqualTo(count == 1));
            var processing = model.GetConditioner("vat").Value;
            Assert.That(processing.Capacity, Is.EqualTo(2)); Assert.That(processing.Count, Is.EqualTo(count));
            Assert.That(processing.First.Value.Id, Is.EqualTo("p1"));
            Assert.That(processing.First.Value.SpeciesId, Is.EqualTo("pondlet"));
            Assert.That(model.Tick(11.75f).ConditionedCount, Is.Zero);
            var result = model.Tick(.25f);
            Assert.That(result.ConditionedCount, Is.EqualTo(count)); Assert.That(result.Matured, Is.False);
            Assert.That(result.FirstDiscovery, Is.False); Assert.That(model.ReservedCultureCount, Is.Zero);
            Assert.That(model.GetConditioner("vat").Value.First.Value.Id, Is.EqualTo("p1"));
            Assert.That(model.GetConditioner("vat").Value.First.Value.SpeciesId, Is.EqualTo("lanternfin"));
            if (count == 2) Assert.That(model.GetConditioner("vat").Value.Second.Value.Id, Is.EqualTo("p2"));
            Assert.That(model.Currency, Is.Zero); Assert.That(model.DiscoveredSpeciesIds, Is.EqualTo(known));
            Assert.That(model.Tick(1000).ConditionedCount, Is.Zero);
        }

        [Test]
        public void FullSiphonAndFullRacksCannotPreventLosslessCancellationAtZeroCash()
        {
            var model = Fixture(); Begin(model, 2);
            for (int i = 0; i < 3; i++) Ok(model.Collect(CultureKind.Bloom));
            Assert.That(model.Collect(CultureKind.Flow).Failure, Is.EqualTo(FailureCode.ReservoirFull));
            // Other production may fill both freed rack slots while the batch is running.
            var save = model.CaptureSave();
            save.storage[0].specimenId = "other1"; save.storage[0].speciesId = "pondlet";
            save.storage[1].specimenId = "other2"; save.storage[1].speciesId = "pondlet";
            model = Restore(Config(), save); model.Tick(5);
            Ok(model.CancelConditioning("vat"));
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(1));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(model.Capacity));
            var vat = model.GetConditioner("vat").Value;
            Assert.That(vat.Phase, Is.EqualTo(ConditionerPhase.Ready)); Assert.That(vat.SecondsRemaining, Is.Zero);
            Assert.That(vat.First.Value.Id, Is.EqualTo("p1")); Assert.That(vat.Second.Value.SpeciesId, Is.EqualTo("pondlet"));
            string before = State(model);
            Assert.That(model.CancelConditioning("vat").Failure, Is.EqualTo(FailureCode.ConditionerNotProcessing));
            Assert.That(model.TransferConditionerToStorage("vat", 0, "rack-1", "p1").Failure, Is.EqualTo(FailureCode.StorageFull));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferConditionerToTank("vat", 0, "tank-a", "p1")); Ok(model.SellFromTank("tank-a"));
            Assert.That(model.Currency, Is.EqualTo(6));
        }

        [Test]
        public void IncubatorAndConditionerCompleteIndependentlyAndOnTheSameTick()
        {
            var model = Fixture(); Begin(model, 2);
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation());
            var specimen = model.Incubator.SpecimenId;
            var result = model.Tick(12);
            Assert.That(result.Matured, Is.True); Assert.That(result.SpecimenId, Is.EqualTo(specimen));
            Assert.That(result.ConditionedCount, Is.EqualTo(2)); Assert.That(result.FirstDiscovery, Is.False);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(model.GetConditioner("vat").Value.Phase, Is.EqualTo(ConditionerPhase.Ready));
        }

        [TestCase("missing-machine", FailureCode.UnknownConditioner)]
        [TestCase("null-sources", FailureCode.InvalidConditioningInput)]
        [TestCase("null-expected", FailureCode.InvalidConditioningInput)]
        [TestCase("empty", FailureCode.InvalidConditioningInput)]
        [TestCase("too-many", FailureCode.InvalidConditioningInput)]
        [TestCase("mismatch", FailureCode.InvalidConditioningInput)]
        [TestCase("duplicate-source", FailureCode.InvalidConditioningInput)]
        [TestCase("duplicate-specimen", FailureCode.InvalidConditioningInput)]
        [TestCase("stale-second", FailureCode.SpecimenMismatch)]
        [TestCase("unknown-source", FailureCode.UnknownStorage)]
        [TestCase("wrong-species", FailureCode.InvalidConditioningInput)]
        [TestCase("no-spark", FailureCode.InsufficientCulture)]
        public void BadBatchNeverPartiallyMovesOrConsumes(string issue, FailureCode expected)
        {
            var model = Fixture(); var save = model.CaptureSave();
            string id = "vat"; string[] sources = { "rack-1", "rack-2" }, ids = { "p1", "p2" };
            switch (issue)
            {
                case "missing-machine": id = "missing"; break;
                case "null-sources": sources = null; break;
                case "null-expected": ids = null; break;
                case "empty": sources = ids = Array.Empty<string>(); break;
                case "too-many": sources = new[] { "rack-1", "rack-2", "rack-3" }; ids = new[] { "p1", "p2", "p3" }; break;
                case "mismatch": ids = new[] { "p1" }; break;
                case "duplicate-source": sources[1] = sources[0]; break;
                case "duplicate-specimen": ids[1] = ids[0]; break;
                case "stale-second": ids[1] = "stale"; break;
                case "unknown-source": sources[1] = "tank-a"; break;
                case "wrong-species": save.storage[1].speciesId = "bubblebelly"; model = Restore(Config(), save); break;
                case "no-spark": Ok(model.ReleaseCulture(CultureKind.Spark)); break;
            }
            string before = State(model);
            Assert.That(model.BeginConditioning(id, sources, ids).Failure, Is.EqualTo(expected));
            Assert.That(State(model), Is.EqualTo(before));
        }

        [Test]
        public void LockedResearchAndUnknownProcessCannotBeBypassedWithStoredPondlets()
        {
            var save = Fixture().CaptureSave(); save.sparkResearchUnlocked = false; save.availableSpark = 0;
            save.growers[2].units = 0; save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin" };
            var model = Restore(Config(), save); string before = State(model);
            Assert.That(model.BeginConditioning("vat", new[] { "rack-1" }, new[] { "p1" }).Failure, Is.EqualTo(FailureCode.ResearchLocked));
            Assert.That(State(model), Is.EqualTo(before));
            save.sparkResearchUnlocked = true; save.availableSpark = 1;
            model = Restore(Config(), save); before = State(model);
            Assert.That(model.BeginConditioning("vat", new[] { "rack-1" }, new[] { "p1" }).Failure, Is.EqualTo(FailureCode.UndiscoveredRecipe));
            Assert.That(State(model), Is.EqualTo(before));
        }

        [Test]
        public void OutputGuardsPartialDrainAndReservedSalePreserveExactSpecimens()
        {
            var model = Fixture(); Begin(model, 2); string before = State(model);
            Assert.That(model.TransferConditionerToTank("vat", 0, "tank-a", "p1").Failure, Is.EqualTo(FailureCode.OutputNotReady));
            Assert.That(model.BeginConditioning("vat", new[] { "rack-1" }, new[] { "p1" }).Failure, Is.EqualTo(FailureCode.ConditionerBusy));
            Assert.That(State(model), Is.EqualTo(before)); model.Tick(12);
            before = State(model);
            Assert.That(model.TransferConditionerToStorage("vat", 0, "rack-1", "old").Failure, Is.EqualTo(FailureCode.SpecimenMismatch));
            Assert.That(model.TransferConditionerToStorage("vat", 2, "rack-1", "p1").Failure, Is.EqualTo(FailureCode.InvalidConditioningInput));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferConditionerToTank("vat", 0, "tank-a", "p1")); Ok(model.ReserveTank("tank-a", "customer"));
            before = State(model);
            Assert.That(model.TransferConditionerToTank("vat", 1, "tank-a", "p2").Failure, Is.EqualTo(FailureCode.TankReserved));
            Assert.That(model.TransferConditionerToTank("vat", 0, "tank-b", "p1").Failure, Is.EqualTo(FailureCode.AlreadyEmpty));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.SetPrice("lanternfin", 15)); Ok(model.SellReservedTank("tank-a", "customer", "p1"));
            Assert.That(model.Currency, Is.EqualTo(14));
            Assert.That(model.SellReservedTank("tank-a", "customer", "p1").Success, Is.False);
            // Capture a partial output hole and restore it without compacting or changing IDs.
            model = Restore(Config(), model.CaptureSave());
            Assert.That(model.GetConditioner("vat").Value.First, Is.Null);
            Assert.That(model.GetConditioner("vat").Value.Second.Value.Id, Is.EqualTo("p2"));
            Ok(model.TransferConditionerToStorage("vat", 1, "rack-2", "p2"));
            Assert.That(model.GetConditioner("vat").Value.Phase, Is.EqualTo(ConditionerPhase.Idle));
            Assert.That(model.TransferConditionerToStorage("vat", 1, "rack-1", "p2").Success, Is.False);
            Assert.That(model.GetStorage("rack-2").Value.Id, Is.EqualTo("p2")); Assert.That(model.Currency, Is.EqualTo(14));
        }

        [Test]
        public void SaveSnapshotsDoNotAliasAndProcessingReloadPreservesTimerAndRefund()
        {
            var model = Fixture(); Begin(model, 2); model.Tick(3.25f);
            var snapshot = model.CaptureSave(); var view = model.GetConditioner("vat").Value;
            var restored = Restore(Config(), JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(snapshot)));
            Assert.That(restored.GetConditioner("vat").Value.SecondsRemaining, Is.EqualTo(8.75f));
            Assert.That(restored.ReservedCultureCount, Is.EqualTo(1));
            snapshot.conditioners[0].firstSpecimenId = "changed";
            snapshot.conditioners[0].secondsRemaining = 99;
            Assert.That(model.GetConditioner("vat").Value.First.Value.Id, Is.EqualTo("p1"));
            Ok(restored.CancelConditioning("vat")); Assert.That(restored.Available(CultureKind.Spark), Is.EqualTo(1));
            Assert.That(view.Phase, Is.EqualTo(ConditionerPhase.Processing));
            Assert.That(model.GetConditioner("vat").Value.Phase, Is.EqualTo(ConditionerPhase.Processing));
            restored = Restore(Config(), restored.CaptureSave());
            Assert.That(restored.GetConditioner("vat").Value.First.Value.SpeciesId, Is.EqualTo("pondlet"));
        }

        [TestCase("missing")] [TestCase("extra")] [TestCase("unknown-machine")]
        [TestCase("bad-phase")] [TestCase("nan")] [TestCase("over-duration")] [TestCase("zero-time")]
        [TestCase("input-hole")] [TestCase("wrong-input-species")]
        [TestCase("duplicate-in-machine")] [TestCase("duplicate-in-rack")] [TestCase("duplicate-in-tank")]
        [TestCase("duplicate-in-incubator")] [TestCase("reserved-overflow")] [TestCase("missing-species")]
        public void CorruptProcessingSaveCannotInventStockOrRefundCapacity(string issue)
        {
            var model = Fixture(); Begin(model, 2); var save = model.CaptureSave(); var vat = save.conditioners[0];
            switch (issue)
            {
                case "missing": save.conditioners = null; break;
                case "extra": save.conditioners = new[] { vat, vat }; break;
                case "unknown-machine": vat.conditionerId = "missing"; break;
                case "bad-phase": vat.phase = (ConditionerPhase)99; break;
                case "nan": vat.secondsRemaining = float.NaN; break;
                case "over-duration": vat.secondsRemaining = 12.01f; break;
                case "zero-time": vat.secondsRemaining = 0; break;
                case "input-hole": vat.firstSpecimenId = vat.firstSpeciesId = null; break;
                case "wrong-input-species": vat.firstSpeciesId = "lanternfin"; break;
                case "duplicate-in-machine": vat.secondSpecimenId = "p1"; break;
                case "duplicate-in-rack": save.storage[0].specimenId = "p1"; save.storage[0].speciesId = "pondlet"; break;
                case "duplicate-in-tank": save.tanks[0].specimenId = "p1"; save.tanks[0].speciesId = "pondlet"; break;
                case "duplicate-in-incubator": save.incubator.phase = IncubatorPhase.Ready; save.incubator.specimenId = "p1"; save.incubator.speciesId = "pondlet"; break;
                case "reserved-overflow": save.availableBloom = 4; break;
                case "missing-species": vat.firstSpeciesId = null; break;
            }
            Assert.That(GameModel.TryRestore(Config(), save, out var rejected, out var error), Is.False);
            Assert.That(rejected, Is.Null); Assert.That(error, Is.EqualTo(FailureCode.InvalidSave));
        }

        [TestCase("mixed")] [TestCase("empty")] [TestCase("time")] [TestCase("wrong-species")]
        public void CorruptReadySaveCannotSmuggleMixedOrUnfinishedResults(string issue)
        {
            var model = Fixture(); Begin(model, 2); model.Tick(12); var save = model.CaptureSave(); var vat = save.conditioners[0];
            switch (issue)
            {
                case "mixed": vat.secondSpeciesId = "pondlet"; break;
                case "empty": vat.firstSpecimenId = vat.firstSpeciesId = vat.secondSpecimenId = vat.secondSpeciesId = null; break;
                case "time": vat.secondsRemaining = 1; break;
                case "wrong-species": vat.firstSpeciesId = vat.secondSpeciesId = "bubblebelly"; break;
            }
            Assert.That(GameModel.TryRestore(Config(), save, out _, out _), Is.False);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void HistoricalGamesMigrateEmptyAndRejectConditionerState(int version)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.version = version;
            if (version < 4) Array.Resize(ref save.growers, 2);
            if (version < 3) save.storage = null;
            save.conditioners = null;
            Assert.That(Restore(config, save).CaptureSave().conditioners, Is.Empty);
            save.conditioners = new[] { new ConditionerSave { conditionerId = "vat" } };
            Assert.That(GameModel.TryRestore(config, save, out _, out _), Is.False);
        }

        [Test]
        public void AtomicRegistryChangePreservesJobsAndRejectsRemovalWithoutCharging()
        {
            var model = Fixture(); Begin(model, 2); model.Tick(2.5f); string before = State(model);
            Assert.That(Apply(model, Array.Empty<string>(), 24).Failure, Is.EqualTo(FailureCode.ConditionerBusy));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(Apply(model, new[] { "vat" }, 0)); Assert.That(State(model), Is.EqualTo(before));
            Ok(model.CancelConditioning("vat")); before = State(model);
            Assert.That(Apply(model, Array.Empty<string>(), 24).Failure, Is.EqualTo(FailureCode.ConditionerBusy));
            Assert.That(State(model), Is.EqualTo(before));
            Ok(model.TransferConditionerToStorage("vat", 0, "rack-1", "p1"));
            Ok(model.TransferConditionerToStorage("vat", 1, "rack-2", "p2"));
            Ok(Apply(model, Array.Empty<string>(), 24)); Assert.That(model.ConditionerIds, Is.Empty);
            Assert.That(model.Currency, Is.EqualTo(24)); Assert.That(model.GetStorage("rack-1").Value.Id, Is.EqualTo("p1"));
            before = State(model);
            Assert.That(Apply(model, new[] { "rack-1" }, -12).Failure, Is.EqualTo(FailureCode.InvalidSave));
            Assert.That(State(model), Is.EqualTo(before));
        }

        [Test]
        public void ConfigRegistryAndDurationAreBoundedAndSnapshotsAreIndependent()
        {
            var config = Config(); config.ConditioningSeconds = 7;
            var save = Fixture().CaptureSave(); var model = Restore(config, save);
            config.ConditionerIds[0] = "changed"; config.ConditioningSeconds = 999;
            var ids = model.ConditionerIds; ids[0] = "changed";
            Begin(model, 1); Assert.That(model.GetConditioner("vat").Value.SecondsRemaining, Is.EqualTo(7));
            foreach (var duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            { config = Config(); config.ConditioningSeconds = duration; Assert.Throws<ArgumentException>(() => new GameModel(config)); }
            foreach (var registry in new[] { new[] { "rack-1" }, new[] { "tank-a" }, new[] { "one", "two" }, new[] { " " } })
            { config = Config(); config.ConditionerIds = registry; Assert.Throws<ArgumentException>(() => new GameModel(config)); }
        }

        static GameConfig Config()
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "rack-1", "rack-2" }; config.ConditionerIds = new[] { "vat" }; return config;
        }
        static GameModel Fixture()
        {
            var config = Config(); var save = new GameModel(config).CaptureSave();
            save.sparkResearchUnlocked = true; save.availableSpark = 1; save.growers[2].units = 4;
            save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin" };
            save.storage[0].specimenId = "p1"; save.storage[0].speciesId = "pondlet";
            save.storage[1].specimenId = "p2"; save.storage[1].speciesId = "pondlet";
            return Restore(config, save);
        }
        static GameModel Restore(GameConfig config, SaveData save)
        {
            Assert.That(GameModel.TryRestore(config, save, out var model, out var error), Is.True, error.ToString()); return model;
        }
        static void Begin(GameModel model, int count)
            => Ok(model.BeginConditioning("vat", count == 1 ? new[] { "rack-1" } : new[] { "rack-1", "rack-2" }, count == 1 ? new[] { "p1" } : new[] { "p1", "p2" }));
        static ActionResult Apply(GameModel model, string[] machines, int delta)
        {
            var method = typeof(GameModel).GetMethod("TryApplyLayoutSlots", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string[]), typeof(string[]), typeof(string[]), typeof(int) }, null);
            return (ActionResult)method.Invoke(model, new object[] { model.TankIds, model.StorageIds, machines, delta });
        }
        static string State(GameModel model) => JsonUtility.ToJson(model.CaptureSave());
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
