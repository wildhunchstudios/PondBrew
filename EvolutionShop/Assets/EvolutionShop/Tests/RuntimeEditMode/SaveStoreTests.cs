using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class SaveStoreTests
    {
        string directory;
        GameConfig config;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(),"EvolutionShop-SaveTests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
        }
        [TearDown] public void Cleanup() { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
        string PathFor(string name="slot.json") => Path.Combine(directory,name);
        static void Ok(ActionResult result) => Assert.That(result.Success,Is.True,result.Failure.ToString());

        [TestCase("fresh")]
        [TestCase("staged")]
        [TestCase("incubating")]
        [TestCase("full-tanks")]
        [TestCase("after-sale")]
        [TestCase("upgraded")]
        public void FullSlotRoundTripPreservesSettingsAndProgress(string state)
        {
            var model = new GameModel(config);
            if(state=="staged") { Collect(model,2,2); Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); model.Tick(0.5f); }
            if(state=="incubating") { StartPondlet(model); model.Tick(3.5f); }
            if(state=="full-tanks")
            {
                Produce(model,"pondlet"); Ok(model.TransferToTank("tank-a"));
                Produce(model,"bubblebelly"); Ok(model.TransferToTank("tank-b")); Produce(model,"ribbonfin");
            }
            if(state=="after-sale") { Produce(model,"pondlet"); Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a")); }
            if(state=="upgraded")
            {
                foreach(var id in new[]{"pondlet","bubblebelly","ribbonfin"}) { Produce(model,id); Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a")); }
                Ok(model.BuyUpgrade()); model.Tick(100); Collect(model,4,4);
            }
            var slot = new SlotData {game=model.CaptureSave(),sensitivity=1.7f,volume=0.31f};
            Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out var restored,out var loaded,out error),Is.True,error);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()),Is.EqualTo(JsonUtility.ToJson(model.CaptureSave())));
            Assert.That(loaded.sensitivity,Is.EqualTo(1.7f)); Assert.That(loaded.volume,Is.EqualTo(0.31f));
            Assert.That(SaveStore.Write(PathFor(),loaded,out error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out restored,out _,out error),Is.True,error);
            if(state=="incubating")
            {
                string id=restored.Incubator.SpecimenId;
                Assert.That(restored.Incubator.SecondsRemaining,Is.EqualTo(6.5f));
                Assert.That(restored.Tick(6.5f).Matured,Is.True); Assert.That(restored.Tick(100).Matured,Is.False);
                Assert.That(restored.Incubator.SpecimenId,Is.EqualTo(id));
            }
            if(state=="full-tanks") { string id=restored.Incubator.SpecimenId; Assert.That(restored.TransferToTank("tank-a").Success,Is.False); Assert.That(restored.Incubator.SpecimenId,Is.EqualTo(id)); }
            if(state=="after-sale") { Assert.That(restored.SellFromTank("tank-a").Success,Is.False); Assert.That(restored.Currency,Is.EqualTo(6)); }
            if(state=="upgraded") { Assert.That(restored.Currency,Is.EqualTo(2)); Assert.That(restored.ReservedCultureCount,Is.EqualTo(8)); }
        }

        [Test] public void HistoricalVersionOneSlotLoadsWithoutChangingItsBytes()
        {
            string fixture=Path.Combine(Application.dataPath,"EvolutionShop/Tests/RuntimeEditMode/Fixtures/V1Progress.json");
            string original=File.ReadAllText(fixture); File.WriteAllText(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out var model,out var slot,out var error),Is.True,error);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(original));
            Assert.That(model.Currency,Is.EqualTo(2)); Assert.That(model.UpgradeOwned,Is.True);
            Assert.That(model.DiscoveredSpeciesIds.Count,Is.EqualTo(3)); Assert.That(model.Incubator.Phase,Is.EqualTo(IncubatorPhase.Incubating));
            Assert.That(model.Incubator.SpecimenId,Is.EqualTo("657d8c9c16744f6ab0436867af9d82d2"));
            Assert.That(model.Incubator.SecondsRemaining,Is.EqualTo(10)); Assert.That(slot.sensitivity,Is.EqualTo(1));
            Assert.That(slot.volume,Is.EqualTo(0.65f));
        }

        [TestCase("{not valid json")]
        [TestCase("{\"formatVersion\":999,\"game\":{\"version\":1}}")]
        public void MalformedOrUnsupportedSlotIsRejectedWithoutOverwrite(string json)
        {
            File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out _,out _),Is.False);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }
        [Test] public void FailedWritePreservesExistingPrimaryAndReportsError()
        {
            var original=new SlotData {game=new GameModel(config).CaptureSave(),volume=0.25f};
            Assert.That(SaveStore.Write(PathFor(),original,out _),Is.True);
            string bytes=File.ReadAllText(PathFor()); Directory.CreateDirectory(PathFor()+".tmp");
            original.volume=0.9f;
            Assert.That(SaveStore.Write(PathFor(),original,out var error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(bytes));
        }
        [Test] public void AtomicReplacementKeepsPreviousSlotAsBackup()
        {
            var slot=new SlotData {game=new GameModel(config).CaptureSave(),volume=0.25f};
            Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True); string original=File.ReadAllText(PathFor());
            slot.volume=0.9f; Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True);
            Assert.That(File.ReadAllText(PathFor()+".bak"),Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var read,out _),Is.True); Assert.That(read.volume,Is.EqualTo(0.9f));
        }
        void Collect(GameModel model,int bloom,int flow)
        {
            for(int i=0;i<bloom;i++) Ok(model.Collect(CultureKind.Bloom));
            for(int i=0;i<flow;i++) Ok(model.Collect(CultureKind.Flow));
        }
        void StartPondlet(GameModel model) { Collect(model,1,1); Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation()); }
        void Produce(GameModel model,string id)
        {
            model.Tick(100); var recipe=model.GetSpecies(id); Collect(model,recipe.Bloom,recipe.Flow);
            for(int i=0;i<recipe.Bloom;i++) Ok(model.Dose(CultureKind.Bloom));
            for(int i=0;i<recipe.Flow;i++) Ok(model.Dose(CultureKind.Flow));
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds);
        }
    }
}
