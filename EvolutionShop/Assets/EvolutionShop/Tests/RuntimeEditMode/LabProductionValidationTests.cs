using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class LabProductionValidationTests
    {
        const string Prefix = "EvolutionShop-LabProductionValidation-";
        static readonly string[] Species = { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };
        string directory;
        string SavePath => Path.Combine(directory, "slot.json");
        GameConfig config;

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); config = GameConfig.CreateDefault();
        }
        [TearDown] public void Cleanup()
        {
            if (!Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(directory).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(directory, true);
        }

        [TestCase(false)] [TestCase(true)]
        public void EveryFourCultureMixtureWithinSharedCapacityHasOnlyTheFiveExactResults(bool activeVat)
        {
            int ceiling = activeVat ? 3 : 4, visited = 0, accepted = 0;
            for (int b = 0; b <= ceiling; b++)
            for (int f = 0; f <= ceiling - b; f++)
            for (int s = 0; s <= ceiling - b - f; s++)
            for (int m = 0; m <= ceiling - b - f - s; m++)
            {
                Fixture(out var model, out _);
                if (activeVat) BeginVat(model);
                Dose(model, CultureKind.Bloom, b); Dose(model, CultureKind.Flow, f);
                Dose(model, CultureKind.Spark, s); Dose(model, CultureKind.Mist, m);
                string expected = b == 1 && f == 1 && s == 0 && m == 0 ? "pondlet" :
                    b == 2 && f == 1 && s == 0 && m == 0 ? "bubblebelly" :
                    b == 1 && f == 2 && s == 0 && m == 0 ? "ribbonfin" :
                    b == 1 && f == 1 && s == 1 && m == 0 ? "lanternfin" :
                    b == 1 && f == 1 && s == 0 && m == 1 ? "driftwing" : null;
                string before = State(model); var result = model.BeginIncubation(); visited++;
                Assert.That(result.Success, Is.EqualTo(expected != null), $"{b}/{f}/{s}/{m} activeVat={activeVat}");
                if (expected == null)
                {
                    Assert.That(result.Failure, Is.EqualTo(FailureCode.InvalidRecipe));
                    Assert.That(State(model), Is.EqualTo(before), "Invalid input must preserve every resource and live job");
                    if (b + f + s + m > 0) Ok(model.ClearDoses());
                    Assert.That(model.Available(CultureKind.Bloom), Is.EqualTo(b));
                    Assert.That(model.Available(CultureKind.Flow), Is.EqualTo(f));
                    Assert.That(model.Available(CultureKind.Spark), Is.EqualTo(s));
                    Assert.That(model.Available(CultureKind.Mist), Is.EqualTo(m));
                }
                else { accepted++; Assert.That(result.SpeciesId, Is.EqualTo(expected)); }
                Assert.That(model.ReservedCultureCount, Is.LessThanOrEqualTo(4));
            }
            Assert.That(visited, Is.EqualTo(activeVat ? 35 : 70));
            Assert.That(accepted, Is.EqualTo(5));
        }

        [TestCase("pondlet")] [TestCase("bubblebelly")] [TestCase("ribbonfin")]
        [TestCase("lanternfin")] [TestCase("driftwing")]
        public void EveryRecipeRoundTripsDosingWorkingAndReadyAlongsideTwoVatResidents(string species)
        {
            var slot = Fixture(out var model, out var layout); BeginVat(model);
            var recipe = model.GetSpecies(species);
            Dose(model, CultureKind.Bloom, recipe.Bloom); Dose(model, CultureKind.Flow, recipe.Flow);
            Dose(model, CultureKind.Spark, recipe.Spark); Dose(model, CultureKind.Mist, recipe.Mist);
            model = RoundTrip(slot, model, layout);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing));
            Ok(model.BeginIncubation()); string specimen = model.Incubator.SpecimenId;
            model.Tick(2.75f); model = RoundTrip(slot, model, layout);
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(specimen));
            Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(7.25f));
            Assert.That(model.GetConditioner("table-2").Value.SecondsRemaining, Is.EqualTo(9.25f));
            model.Tick(9.25f); model = RoundTrip(slot, model, layout);
            Assert.That(model.Incubator.Phase, Is.EqualTo(IncubatorPhase.Ready));
            Assert.That(model.Incubator.SpeciesId, Is.EqualTo(species));
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(specimen));
            Assert.That(model.GetConditioner("table-2").Value.Count, Is.EqualTo(2));
            Assert.That(model.Currency, Is.Zero);
        }

        [TestCase("pondlet")] [TestCase("bubblebelly")] [TestCase("ribbonfin")]
        [TestCase("lanternfin")] [TestCase("driftwing")]
        public void KnownRecipeReturnsUnusedFourthCultureWithoutDroppingOrInventingDoses(string species)
        {
            Fixture(out var model, out _); var recipe = model.GetSpecies(species);
            Dose(model, CultureKind.Bloom, recipe.Bloom); Dose(model, CultureKind.Flow, recipe.Flow);
            Dose(model, CultureKind.Spark, recipe.Spark); Dose(model, CultureKind.Mist, recipe.Mist);
            var extra = recipe.Mist == 0 ? CultureKind.Mist : CultureKind.Spark;
            Dose(model, extra, 1); int total = model.ReservedCultureCount;
            Ok(model.UseKnownRecipe(species));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(total));
            Assert.That(model.Available(extra), Is.EqualTo(1));
            Assert.That(model.Staged(extra), Is.Zero);
            Ok(model.BeginIncubation());
            Assert.That(model.Incubator.SpeciesId, Is.EqualTo(species));
            Assert.That(model.Available(extra), Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void FullDestinationsAndOccupiedRelocationPreserveSevenResidentsAtZeroCash(bool cancel)
        {
            var slot = Fixture(out var model, out var layout);
            Produce(model, "ribbonfin"); Ok(model.TransferToTank("tank-a"));
            Produce(model, "lanternfin"); Ok(model.TransferToTank("tank-b"));
            BeginVat(model);
            if (cancel) Ok(model.CancelConditioning("table-2")); else model.Tick(12);
            if (cancel) Ok(model.ReleaseCulture(CultureKind.Spark));
            Produce(model, "driftwing"); Ok(model.StoreReadyOutput(model.StorageIds[0]));
            Produce(model, "bubblebelly"); Ok(model.StoreReadyOutput(model.StorageIds[1]));
            Produce(model, "lanternfin");
            model = RoundTrip(slot, model, layout);
            string before = State(model); var vat = model.GetConditioner("table-2").Value;
            Assert.That(model.StoreReadyOutput(model.StorageIds[0]).Failure, Is.EqualTo(FailureCode.StorageFull));
            Assert.That(model.TransferToTank("tank-a").Failure, Is.EqualTo(FailureCode.TankFull));
            Assert.That(model.TransferConditionerToStorage(vat.Id, 0, model.StorageIds[1], vat.First.Value.Id).Failure, Is.EqualTo(FailureCode.StorageFull));
            Assert.That(model.TransferConditionerToTank(vat.Id, 1, "tank-b", vat.Second.Value.Id).Failure, Is.EqualTo(FailureCode.TankFull));
            Assert.That(model.CancelConditioning(vat.Id).Failure, Is.EqualTo(FailureCode.ConditionerNotProcessing));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, vat.Id, -8, 14, 0, out _).Success, Is.True);
            Assert.That(layout.TryMoveFacility(model, ShopDayPhase.Night, ShopLayout.IncubatorFacilityId, 0, 8, 1).Success, Is.True);
            Assert.That(State(model), Is.EqualTo(before));
            string layoutBefore = JsonUtility.ToJson(layout.CaptureSave());
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 8, 14, 1, out _).Success, Is.False, "Rotated rack operator would overlap the moved incubator");
            Assert.That(JsonUtility.ToJson(layout.CaptureSave()), Is.EqualTo(layoutBefore));
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(layout.TryCommit(model, ShopDayPhase.Night, "table-1", 8, 14, 0, out _).Success, Is.True);
            Assert.That(State(model), Is.EqualTo(before));
            model = RoundTrip(slot, model, layout);
            Ok(model.ReserveTank("tank-a", "lab-gate-buyer")); string sold = model.GetTank("tank-a").Value.Id;
            Ok(model.SellReservedTank("tank-a", "lab-gate-buyer", sold));
            Assert.That(model.Currency, Is.EqualTo(10));
            string ready = model.Incubator.SpecimenId; Ok(model.TransferToTank("tank-a"));
            Assert.That(model.GetTank("tank-a").Value.Id, Is.EqualTo(ready));
            Assert.That(model.TransferToTank("tank-a").Success, Is.False);
            model = RoundTrip(slot, model, layout);
            Assert.That(model.SellReservedTank("tank-a", "lab-gate-buyer", sold).Success, Is.False);
            Assert.That(model.Currency, Is.EqualTo(10));
        }

        SlotData Fixture(out GameModel model, out ShopLayout layout)
        {
            string path = Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V15Conditioning.json");
            var original = JsonUtility.FromJson<SlotData>(File.ReadAllText(path));
            Assert.That(ShopLayout.TryRestore(original.layout, out layout), Is.True);
            var save = new GameModel(layout.WithTankIds(config)).CaptureSave();
            save.sparkResearchUnlocked = true; save.mistResearchUnlocked = true; save.discoveries = (string[])Species.Clone();
            foreach (var grower in save.growers) grower.units = 6;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), save, out model, out var error), Is.True, error.ToString());
            return new SlotData { game = save, layout = layout.CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        }
        GameModel RoundTrip(SlotData slot, GameModel model, ShopLayout layout)
        {
            slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave(); string expected = State(model);
            Assert.That(SaveStore.Write(SavePath, slot, out var error), Is.True, error);
            byte[] bytes = File.ReadAllBytes(SavePath); GameModel result = null;
            for (int i = 0; i < 2; i++)
            {
                Assert.That(SaveStore.Read(SavePath, config, out result, out var restored, out error), Is.True, error);
                Assert.That(State(result), Is.EqualTo(expected));
                Assert.That(JsonUtility.ToJson(restored.layout), Is.EqualTo(JsonUtility.ToJson(slot.layout)));
            }
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(bytes)); return result;
        }
        void BeginVat(GameModel model)
        {
            var ids = new string[2];
            for (int i = 0; i < 2; i++) { Produce(model, "pondlet"); ids[i] = model.Incubator.SpecimenId; Ok(model.StoreReadyOutput(model.StorageIds[i])); }
            Ok(model.Collect(CultureKind.Spark)); Ok(model.BeginConditioning("table-2", model.StorageIds, ids));
        }
        void Produce(GameModel model, string id)
        {
            model.Tick(20); var recipe = model.GetSpecies(id);
            Dose(model, CultureKind.Bloom, recipe.Bloom); Dose(model, CultureKind.Flow, recipe.Flow);
            Dose(model, CultureKind.Spark, recipe.Spark); Dose(model, CultureKind.Mist, recipe.Mist);
            Ok(model.BeginIncubation()); model.Tick(10);
        }
        static void Dose(GameModel model, CultureKind culture, int count)
        { for (int i = 0; i < count; i++) { Ok(model.Collect(culture)); Ok(model.Dose(culture)); } }
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
        static string State(GameModel model) => JsonUtility.ToJson(model.CaptureSave());
    }
}
