using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ResearchTests
    {
        static readonly string[] StarterIds = { "pondlet", "bubblebelly", "ribbonfin" };

        [Test]
        public void LockedSparkNeverGrowsAndEveryCultureCommandRejectsWithoutMutation()
        {
            var model = new GameModel(GameConfig.CreateDefault());
            Assert.That(model.SparkResearchUnlocked, Is.False); Assert.That(model.SparkResearchReady, Is.False);
            Assert.That(model.IsCultureUnlocked(CultureKind.Bloom), Is.True);
            Assert.That(model.IsCultureUnlocked(CultureKind.Flow), Is.True);
            Assert.That(model.IsCultureUnlocked(CultureKind.Spark), Is.False);
            Assert.That(model.IsCultureUnlocked((CultureKind)99), Is.False);
            model.Tick(10000);
            Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.Zero);
            Assert.That(model.GetGrower(CultureKind.Spark).SecondsToNextUnit, Is.EqualTo(2));
            string before = State(model);
            foreach (var result in new[] { model.Collect(CultureKind.Spark), model.Dose(CultureKind.Spark), model.ReturnDose(CultureKind.Spark), model.ReleaseCulture(CultureKind.Spark), model.UseKnownRecipe("lanternfin") })
                Assert.That(result.Failure, Is.EqualTo(FailureCode.ResearchLocked));
            Assert.That(model.BuySparkResearch().Failure, Is.EqualTo(FailureCode.ResearchPrerequisiteMissing));
            Assert.That(State(model), Is.EqualTo(before));
        }

        [Test]
        public void ResearchRequiresAllThreeDiscoveriesAndChargesOnceWithoutDiscoveringOrChangingAnActiveSpecimen()
        {
            var config = GameConfig.CreateDefault(); var model = Cash(config, 21);
            foreach (string id in StarterIds)
            {
                string before = State(model);
                Assert.That(model.BuySparkResearch().Failure, Is.EqualTo(FailureCode.ResearchPrerequisiteMissing));
                Assert.That(State(model), Is.EqualTo(before));
                Produce(model, id); Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a"));
            }
            Assert.That(model.SparkResearchReady, Is.True); Assert.That(model.Currency, Is.EqualTo(47));
            string unfunded = State(model);
            Assert.That(model.BuySparkResearch().Failure, Is.EqualTo(FailureCode.InsufficientFunds)); Assert.That(State(model), Is.EqualTo(unfunded));
            var save = model.CaptureSave(); save.currency = 48;
            Assert.That(GameModel.TryRestore(config, save, out model, out _), Is.True);
            Stage(model, "pondlet"); Ok(model.BeginIncubation()); model.Tick(2.25f);
            var job = model.Incubator; var bloom = model.GetGrower(CultureKind.Bloom);
            Ok(model.BuySparkResearch());
            Assert.That(model.Currency, Is.Zero); Assert.That(model.IsCultureUnlocked(CultureKind.Spark), Is.True);
            Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.EqualTo(4));
            Assert.That(model.GetGrower(CultureKind.Spark).SecondsToNextUnit, Is.EqualTo(2));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(job.SpecimenId)); Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(job.SecondsRemaining));
            Assert.That(model.GetGrower(CultureKind.Bloom).Units, Is.EqualTo(bloom.Units));
            Assert.That(model.IsDiscovered("lanternfin"), Is.False);
            string bought = State(model);
            Assert.That(model.BuySparkResearch().Failure, Is.EqualTo(FailureCode.ResearchAlreadyUnlocked)); Assert.That(State(model), Is.EqualTo(bought));
        }

        [Test]
        public void ConfigAndLayoutCopiesKeepResearchTuningAndDistinctThreeIngredientRecipes()
        {
            var config = GameConfig.CreateDefault(); config.SparkResearchCost = 7;
            config.InitialGrowerUnits = 2; config.GrowerRefillSeconds = 3.5f;
            var copied = new ShopLayout().WithTankIds(config);
            Assert.That(copied.SparkResearchCost, Is.EqualTo(7)); Assert.That(copied.Species[3].Spark, Is.EqualTo(1));
            var model = Prerequisites(copied, 7);
            config.SparkResearchCost = copied.SparkResearchCost = 999;
            copied.Species[3].Spark = 99;
            Ok(model.BuySparkResearch());
            Assert.That(model.Currency, Is.Zero); Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.EqualTo(2));
            Assert.That(model.GetGrower(CultureKind.Spark).SecondsToNextUnit, Is.EqualTo(3.5f));
            Assert.That(model.GetSpecies("lanternfin").Spark, Is.EqualTo(1));
            Assert.That(new SpeciesDefinition("old", "Old", 1, 1, 6, "old").Spark, Is.Zero);
            var bad = GameConfig.CreateDefault(); bad.Species[3].Spark = 0;
            Assert.Throws<ArgumentException>(() => new GameModel(bad), "An actual three-ingredient duplicate still rejects.");
            bad = GameConfig.CreateDefault(); bad.Species[3].Spark = -1;
            Assert.Throws<ArgumentException>(() => new GameModel(bad));
            bad = GameConfig.CreateDefault(); bad.SparkResearchCost = -1;
            Assert.Throws<ArgumentException>(() => new GameModel(bad));
        }

        [Test]
        public void ThirdIngredientDisambiguatesLanternfinAndExtraIngredientsCannotBecomeAnOldRecipe()
        {
            var model = Unlocked();
            Assert.That(model.UseKnownRecipe("lanternfin").Failure, Is.EqualTo(FailureCode.UndiscoveredRecipe));
            foreach (string id in StarterIds)
            {
                Produce(model, id); Assert.That(model.Incubator.SpeciesId, Is.EqualTo(id));
                Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a"));
            }
            Stage(model, "lanternfin"); Ok(model.BeginIncubation());
            Assert.That(model.Incubator.SpeciesId, Is.EqualTo("lanternfin"));
            Assert.That(model.IsDiscovered("lanternfin"), Is.False);
            var result = model.Tick(10); Assert.That(result.FirstDiscovery, Is.True);
            Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a"));
            Stage(model, "lanternfin"); Ok(model.Collect(CultureKind.Spark)); Ok(model.Dose(CultureKind.Spark));
            string before = State(model);
            Assert.That(model.BeginIncubation().Failure, Is.EqualTo(FailureCode.InvalidRecipe)); Assert.That(State(model), Is.EqualTo(before));
            Ok(model.ReturnDose(CultureKind.Spark)); Ok(model.ReleaseCulture(CultureKind.Spark));
            Ok(model.BeginIncubation()); result = model.Tick(10);
            Assert.That(result.FirstDiscovery, Is.False); Assert.That(model.DiscoveredSpeciesIds.Count, Is.EqualTo(4));
        }

        [Test]
        public void KnownRecipeReturnsEveryStagedCultureAndRejectsMissingIngredientsAtomically()
        {
            var model = Unlocked(); Produce(model, "lanternfin"); Ok(model.TransferToTank("tank-a"));
            Stage(model, "lanternfin"); Ok(model.Collect(CultureKind.Spark));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(4));
            Ok(model.UseKnownRecipe("pondlet"));
            Assert.That(model.Incubator.Bloom, Is.EqualTo(1)); Assert.That(model.Incubator.Flow, Is.EqualTo(1)); Assert.That(model.Incubator.Spark, Is.Zero);
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(2)); Assert.That(model.ReservedCultureCount, Is.EqualTo(4));
            Ok(model.UseKnownRecipe("lanternfin")); Assert.That(model.Incubator.Spark, Is.EqualTo(1));
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(1));
            string before = State(model);
            Assert.That(model.UseKnownRecipe("ribbonfin").Failure, Is.EqualTo(FailureCode.InsufficientCulture)); Assert.That(State(model), Is.EqualTo(before));
            Ok(model.ClearDoses()); Assert.That(model.Incubator.Spark, Is.Zero);
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(2)); Assert.That(model.ReservedCultureCount, Is.EqualTo(4));
        }

        [TestCase(false)] [TestCase(true)]
        public void SparkOnlyFullReservoirCanRecoverAndProduceWithoutLosingReservedReturnSpace(bool upgraded)
        {
            var model = Unlocked(100); if (upgraded) Ok(model.BuyUpgrade());
            Produce(model, "lanternfin"); Ok(model.TransferToTank("tank-a"));
            for (int i = 0; i < model.Capacity; i++) { model.Tick(2); Ok(model.Collect(CultureKind.Spark)); }
            string full = State(model);
            Assert.That(model.Collect(CultureKind.Bloom).Failure, Is.EqualTo(FailureCode.ReservoirFull)); Assert.That(State(model), Is.EqualTo(full));
            Ok(model.ReleaseCulture(CultureKind.Spark)); Ok(model.ReleaseCulture(CultureKind.Spark));
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Collect(CultureKind.Flow));
            Ok(model.UseKnownRecipe("lanternfin"));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(model.Capacity));
            Ok(model.ClearDoses()); Assert.That(model.ReservedCultureCount, Is.EqualTo(model.Capacity));
            Ok(model.UseKnownRecipe("lanternfin")); Ok(model.BeginIncubation()); model.Tick(10);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(model.Capacity - 3));
        }

        [Test]
        public void UnlockedSparkUsesActiveTimeRefillAndItsClockSurvivesRoundTrip()
        {
            var model = Unlocked(); Ok(model.Collect(CultureKind.Spark)); model.Tick(1.25f);
            Assert.That(model.GetGrower(CultureKind.Spark).Units, Is.EqualTo(3));
            Assert.That(model.GetGrower(CultureKind.Spark).SecondsToNextUnit, Is.EqualTo(.75f));
            var save = JsonUtility.FromJson<SaveData>(State(model));
            Assert.That(GameModel.TryRestore(GameConfig.CreateDefault(), save, out var restored, out _), Is.True);
            Assert.That(State(restored), Is.EqualTo(State(model)));
            restored.Tick(.75f); Assert.That(restored.GetGrower(CultureKind.Spark).Units, Is.EqualTo(4));
            restored.Tick(float.MaxValue); Assert.That(restored.GetGrower(CultureKind.Spark).Units, Is.EqualTo(6));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void HistoricalTwoCultureProductionRestoresExactStateAndNoFreeResearch(int version)
        {
            var config = GameConfig.CreateDefault(); var model = Prerequisites(config, 77);
            Stage(model, "ribbonfin"); Ok(model.BeginIncubation()); model.Tick(3.25f);
            Ok(model.Collect(CultureKind.Bloom));
            var old = model.CaptureSave(); old.version = version; Array.Resize(ref old.growers, 2);
            if (version < 3) old.storage = null;
            Assert.That(GameModel.TryRestore(config, old, out var restored, out _), Is.True);
            Assert.That(restored.Incubator.SpecimenId, Is.EqualTo(model.Incubator.SpecimenId));
            Assert.That(restored.Incubator.SecondsRemaining, Is.EqualTo(6.75f)); Assert.That(restored.Currency, Is.EqualTo(77));
            Assert.That(restored.Available(CultureKind.Bloom), Is.EqualTo(1)); Assert.That(restored.SparkResearchReady, Is.True);
            Assert.That(restored.SparkResearchUnlocked, Is.False); Assert.That(restored.GetGrower(CultureKind.Spark).Units, Is.Zero);
            Assert.That(restored.CaptureSave().growers.Length, Is.EqualTo(4));
            foreach (var grower in old.growers)
            {
                var now = restored.GetGrower(grower.cultureId == "bloom" ? CultureKind.Bloom : CultureKind.Flow);
                Assert.That(now.Units, Is.EqualTo(grower.units)); Assert.That(now.SecondsToNextUnit, Is.EqualTo(grower.secondsUntilNextUnit));
            }
            old.sparkResearchUnlocked = true; Assert.That(GameModel.TryRestore(config, old, out _, out _), Is.False);
            old.sparkResearchUnlocked = false; old.availableSpark = 1; Assert.That(GameModel.TryRestore(config, old, out _, out _), Is.False);
            old.availableSpark = 0; old.incubator.stagedSpark = 1; Assert.That(GameModel.TryRestore(config, old, out _, out _), Is.False);
        }

        [TestCase("discovery")] [TestCase("incubating")] [TestCase("ready")] [TestCase("tank")] [TestCase("storage")] [TestCase("price")]
        public void LockedAndHistoricalSavesRejectGatedSpeciesInEveryAuthorityLocation(string location)
        {
            foreach (int version in new[] { 3, 4 })
            {
                var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "storage-1" };
                var save = new GameModel(config).CaptureSave(); save.version = version;
                if (version == 3) Array.Resize(ref save.growers, 2);
                if (location == "discovery") save.discoveries = new[] { "lanternfin" };
                if (location == "incubating" || location == "ready")
                {
                    save.incubator.phase = location == "ready" ? IncubatorPhase.Ready : IncubatorPhase.Incubating;
                    save.incubator.speciesId = "lanternfin"; save.incubator.specimenId = "gated-output";
                    save.incubator.secondsRemaining = location == "ready" ? 0 : 5;
                }
                if (location == "tank") { save.tanks[0].speciesId = "lanternfin"; save.tanks[0].specimenId = "gated-display"; }
                if (location == "storage") { save.storage[0].speciesId = "lanternfin"; save.storage[0].specimenId = "gated-storage"; }
                if (location == "price") save.prices = new[] { new PriceSave { speciesId = "lanternfin", price = 16 } };
                Assert.That(GameModel.TryRestore(config, save, out var restored, out _), Is.False, version + ":" + location);
                Assert.That(restored, Is.Null);
            }
        }

        [TestCase("missing-grower")] [TestCase("duplicate-grower")] [TestCase("locked-units")] [TestCase("locked-clock")]
        [TestCase("locked-available")] [TestCase("locked-staged")] [TestCase("unearned-unlock")] [TestCase("negative-spark")]
        [TestCase("over-capacity")]
        public void CorruptCurrentResearchStateRejectsWithoutRepair(string problem)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave();
            switch (problem)
            {
                case "missing-grower": Array.Resize(ref save.growers, 2); break;
                case "duplicate-grower": save.growers[2].cultureId = "flow"; break;
                case "locked-units": save.growers[2].units = 1; break;
                case "locked-clock": save.growers[2].secondsUntilNextUnit = 1; break;
                case "locked-available": save.availableSpark = 1; break;
                case "locked-staged": save.incubator.stagedSpark = 1; break;
                case "unearned-unlock": save.sparkResearchUnlocked = true; break;
                case "negative-spark": save.availableSpark = -1; break;
                case "over-capacity":
                    save = Unlocked().CaptureSave(); save.availableSpark = int.MaxValue; save.availableBloom = int.MaxValue; break;
            }
            Assert.That(GameModel.TryRestore(config, save, out var restored, out var error), Is.False);
            Assert.That(restored, Is.Null); Assert.That(error, Is.EqualTo(FailureCode.InvalidSave));
        }

        [TestCase(IncubatorPhase.Dosing)] [TestCase(IncubatorPhase.Incubating)] [TestCase(IncubatorPhase.Ready)]
        public void ResearchProductionAndStoredStockRoundTripWithoutRediscoveryOrSecondPayment(IncubatorPhase phase)
        {
            var config = GameConfig.CreateDefault(); config.StorageIds = new[] { "storage-1" };
            var model = Prerequisites(config, 48); Ok(model.BuySparkResearch());
            string resident = Produce(model, "lanternfin"); Ok(model.StoreReadyOutput("storage-1"));
            Stage(model, "lanternfin");
            if (phase != IncubatorPhase.Dosing) { Ok(model.BeginIncubation()); model.Tick(phase == IncubatorPhase.Ready ? 10 : 3); }
            var save = JsonUtility.FromJson<SaveData>(State(model));
            Assert.That(GameModel.TryRestore(config, save, out var restored, out _), Is.True);
            Assert.That(State(restored), Is.EqualTo(State(model)));
            string before = State(restored);
            Assert.That(restored.BuySparkResearch().Failure, Is.EqualTo(FailureCode.ResearchAlreadyUnlocked)); Assert.That(State(restored), Is.EqualTo(before));
            Ok(restored.TransferStorageToTank("storage-1", "tank-a", resident));
            Ok(restored.ReserveTank("tank-a", "buyer")); Ok(restored.SellReservedTank("tank-a", "buyer", resident));
            Assert.That(restored.Currency, Is.EqualTo(14));
            if (phase == IncubatorPhase.Dosing) Ok(restored.BeginIncubation());
            Assert.That(restored.Tick(10).FirstDiscovery, Is.False);
        }

        static GameModel Cash(GameConfig config, int amount)
        {
            var save = new GameModel(config).CaptureSave(); save.currency = amount;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
        static GameModel Prerequisites(GameConfig config, int remaining)
        {
            var model = Cash(config, 0);
            foreach (string id in StarterIds) { Produce(model, id); Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a")); }
            var save = model.CaptureSave(); save.currency = remaining;
            Assert.That(GameModel.TryRestore(config, save, out model, out _), Is.True); return model;
        }
        static GameModel Unlocked(int remaining = 0)
        {
            var model = Prerequisites(GameConfig.CreateDefault(), 48 + remaining); Ok(model.BuySparkResearch()); return model;
        }
        static void Stage(GameModel model, string id)
        {
            var recipe = model.GetSpecies(id);
            foreach (var culture in new[] { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark })
            {
                int count = culture == CultureKind.Bloom ? recipe.Bloom : culture == CultureKind.Flow ? recipe.Flow : recipe.Spark;
                for (int i = 0; i < count; i++) { Ok(model.Collect(culture)); Ok(model.Dose(culture)); }
            }
        }
        static string Produce(GameModel model, string id)
        {
            Stage(model, id); Ok(model.BeginIncubation()); model.Tick(10);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready)); return model.Incubator.SpecimenId;
        }
        static string State(GameModel model) => JsonUtility.ToJson(model.CaptureSave());
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
