using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class AirfinSaveTests
    {
        const string Prefix = "EvolutionShop-AirfinSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");
        string Fixture() => Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V15Conditioning.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
        }

        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(directory).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(directory, true);
        }

        [Test] public void FrozenFifteenMigratesReadOnlyAndArchivesExactOriginalOnFirstWrite()
        {
            byte[] original = File.ReadAllBytes(Fixture()); File.WriteAllBytes(PathFor(), original);
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.True, error);
            Assert.That(slot.formatVersion, Is.EqualTo(16));
            Assert.That(slot.game.version, Is.EqualTo(6)); Assert.That(slot.layout.version, Is.EqualTo(7));
            Assert.That(model.MistResearchUnlocked, Is.False);
            Assert.That(model.Available(CultureKind.Mist), Is.Zero);
            Assert.That(model.GetGrower(CultureKind.Mist).Units, Is.Zero);
            Assert.That(model.GetConditioner("table-2").Value.Phase, Is.EqualTo(ConditionerPhase.Processing));
            Assert.That(model.GetConditioner("table-2").Value.First.Value.Id, Is.EqualTo("e3e36adc30e04ee8a0965c484763a069"));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v15.bak"), Is.EqualTo(original));
            byte[] first = File.ReadAllBytes(PathFor());
            slot.volume = .23f; Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v15.bak"), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(first));
        }

        [Test] public void MistResearchChargesOnceAndDriftwingUsesFourthCultureBesideActiveVat()
        {
            var model = FromFixture(out var layout);
            Assert.That(model.MistResearchReady, Is.True);
            var poor = model.CaptureSave(); poor.currency = 39;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), poor, out var unfunded, out _), Is.True);
            string before = JsonUtility.ToJson(unfunded.CaptureSave());
            Assert.That(unfunded.BuyMistResearch().Failure, Is.EqualTo(FailureCode.InsufficientFunds));
            Assert.That(JsonUtility.ToJson(unfunded.CaptureSave()), Is.EqualTo(before));
            Ok(model.BuyMistResearch()); Assert.That(model.Currency, Is.EqualTo(310));
            Assert.That(model.GetGrower(CultureKind.Mist).Units, Is.EqualTo(4));
            before = JsonUtility.ToJson(model.CaptureSave());
            Assert.That(model.BuyMistResearch().Failure, Is.EqualTo(FailureCode.MistAlreadyUnlocked));
            Assert.That(JsonUtility.ToJson(model.CaptureSave()), Is.EqualTo(before));
            Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom));
            Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow));
            Ok(model.Collect(CultureKind.Mist)); Ok(model.Dose(CultureKind.Mist));
            Assert.That(model.ReservedCultureCount, Is.EqualTo(4)); // 3 doses plus the live vat's reserved Spark.
            Assert.That(model.BeginIncubation().SpeciesId, Is.EqualTo("driftwing"));
            Assert.That(model.Incubator.Mist, Is.Zero);
            model.Tick(2.75f);
            var pending = model.CaptureSave(); string specimen = pending.incubator.specimenId;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), pending, out model, out _), Is.True);
            Assert.That(model.Incubator.SpecimenId, Is.EqualTo(specimen));
            Assert.That(model.Incubator.SecondsRemaining, Is.EqualTo(7.25f));
            var matured = model.Tick(7.25f);
            Assert.That(matured.Matured, Is.True); Assert.That(matured.FirstDiscovery, Is.True);
            Assert.That(model.GetSpecies("driftwing").FamilyId, Is.EqualTo("Airfin"));
            Assert.That(model.GetPrice("driftwing").Value.Standard, Is.EqualTo(18));
            Ok(model.TransferToTank("tank-a"));
            Ok(model.ReserveTank("tank-a", "buyer"));
            Ok(model.SellReservedTank("tank-a", "buyer", specimen));
            Assert.That(model.Currency, Is.EqualTo(328));
            model.Tick(2f);
            Assert.That(model.GetConditioner("table-2").Value.Phase, Is.EqualTo(ConditionerPhase.Ready));
            Assert.That(model.GetConditioner("table-2").Value.First.Value.Id, Is.EqualTo("e3e36adc30e04ee8a0965c484763a069"));
        }

        [TestCase("missing-unlock")][TestCase("bad-amount")][TestCase("bad-grower")][TestCase("bad-staged")]
        public void CurrentMistShapeRejectsMalformedFieldsWithoutChangingFile(string problem)
        {
            var model = FromFixture(out var layout);
            var slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(Fixture()));
            slot.formatVersion = 16; slot.game = model.CaptureSave(); slot.layout = layout.CaptureSave();
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            string json = JsonUtility.ToJson(slot);
            if (problem == "missing-unlock") json = json.Replace("\"mistResearchUnlocked\":false,", "");
            else if (problem == "bad-amount") json = json.Replace("\"availableMist\":0", "\"availableMist\":null");
            else if (problem == "bad-grower") json = json.Replace("\"cultureId\":\"mist\"", "\"cultureId\":null");
            else json = json.Replace("\"stagedMist\":0", "\"stagedMist\":null");
            Assert.That(json, Is.Not.EqualTo(JsonUtility.ToJson(slot)));
            File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        [Test] public void LockedAndLegacyMistForgeryRejectsWithoutInventingFamilyOrCulture()
        {
            var empty = new GameModel(config).CaptureSave();
            empty.mistResearchUnlocked = true;
            Assert.That(GameModel.TryRestore(config, empty, out _, out _), Is.False);
            var model = FromFixture(out var layout);
            var old = model.CaptureSave(); old.version = 5; Array.Resize(ref old.growers, 3);
            old.availableMist = 1;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), old, out _, out _), Is.False);
            old.availableMist = 0; old.mistResearchUnlocked = true;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), old, out _, out _), Is.False);
            old.mistResearchUnlocked = false; old.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), old, out _, out _), Is.False);
            var current = model.CaptureSave(); current.availableMist = int.MaxValue;
            Assert.That(GameModel.TryRestore(layout.WithTankIds(config), current, out _, out _), Is.False);
        }

        GameModel FromFixture(out ShopLayout layout)
        {
            File.WriteAllBytes(PathFor(), File.ReadAllBytes(Fixture()));
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.True, error);
            Assert.That(ShopLayout.TryRestore(slot.layout, out layout), Is.True);
            return model;
        }

        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
