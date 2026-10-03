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
        SlotData FreshSlot() => new SlotData {game=new GameModel(config).CaptureSave(),cycle=new ShopDayCycle().CaptureSave()};
        byte[] HistoricalFixture() => File.ReadAllBytes(Path.Combine(Application.dataPath,"EvolutionShop/Tests/RuntimeEditMode/Fixtures/V1Progress.json"));

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
            var slot = new SlotData {game=model.CaptureSave(),cycle=new ShopDayCycle().CaptureSave(),sensitivity=1.7f,volume=0.31f};
            Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out var restored,out var loaded,out error),Is.True,error);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()),Is.EqualTo(JsonUtility.ToJson(model.CaptureSave())));
            Assert.That(loaded.formatVersion,Is.EqualTo(3)); Assert.That(loaded.game.version,Is.EqualTo(1));
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

        [TestCase(ShopDayPhase.Night)]
        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void CurrentFormatFileRoundTripPreservesEveryDayPhase(ShopDayPhase phase)
        {
            var day=new ShopDayCycle(); day.OpenShop(); day.Tick(483f);
            if(phase!=ShopDayPhase.Night)
            {
                day.OpenShop();
                if(phase==ShopDayPhase.Closing) day.RequestClose();
                day.Tick(0.75f);
            }
            var slot=FreshSlot(); slot.cycle=day.CaptureSave(); slot.sensitivity=2.1f; slot.volume=0.23f;
            slot.trades=ShopTradeLedger.LegacyForCycle(slot.cycle,0).CaptureSave();
            Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var loaded,out error),Is.True,error);
            Assert.That(loaded.formatVersion,Is.EqualTo(3));
            Assert.That(loaded.game.version,Is.EqualTo(1));
            Assert.That(ShopDayCycle.TryRestore(loaded.cycle,out var restored),Is.True);
            Assert.That(restored.DayNumber,Is.EqualTo(2)); Assert.That(restored.Phase,Is.EqualTo(phase));
            Assert.That(restored.SecondsRemaining,Is.EqualTo(day.SecondsRemaining));
            Assert.That(loaded.sensitivity,Is.EqualTo(2.1f)); Assert.That(loaded.volume,Is.EqualTo(0.23f));
            day.Tick(1000f); restored.Tick(1000f);
            Assert.That(restored.DayNumber,Is.EqualTo(day.DayNumber)); Assert.That(restored.Phase,Is.EqualTo(day.Phase));
        }

        [Test] public void HistoricalVersionOneSlotMigratesInMemoryWithoutChangingItsBytesOrProgress()
        {
            byte[] original=HistoricalFixture(); File.WriteAllBytes(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out var model,out var slot,out var error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(File.Exists(PathFor()+".bak"),Is.False); Assert.That(File.Exists(PathFor()+".v1.bak"),Is.False);
            Assert.That(model.Currency,Is.EqualTo(2)); Assert.That(model.UpgradeOwned,Is.True);
            Assert.That(model.DiscoveredSpeciesIds.Count,Is.EqualTo(3)); Assert.That(model.Incubator.Phase,Is.EqualTo(IncubatorPhase.Incubating));
            Assert.That(model.Incubator.SpecimenId,Is.EqualTo("657d8c9c16744f6ab0436867af9d82d2"));
            Assert.That(model.Incubator.SecondsRemaining,Is.EqualTo(10)); Assert.That(slot.sensitivity,Is.EqualTo(1));
            Assert.That(slot.volume,Is.EqualTo(0.65f)); Assert.That(slot.formatVersion,Is.EqualTo(3));
            Assert.That(slot.game.version,Is.EqualTo(1)); Assert.That(slot.cycle.version,Is.EqualTo(1));
            Assert.That(slot.cycle.phase,Is.EqualTo(ShopDayPhase.Night)); Assert.That(slot.cycle.dayNumber,Is.EqualTo(1));
            Assert.That(slot.cycle.secondsRemaining,Is.Zero);
            Assert.That(slot.trades.version,Is.EqualTo(1));
            Assert.That(slot.trades.current,Is.Null); Assert.That(slot.trades.last,Is.Null);
            Assert.That(SaveStore.Read(PathFor(),config,out var again,out var repeated,out error),Is.True,error);
            Assert.That(again.Currency,Is.EqualTo(2)); Assert.That(repeated.cycle.dayNumber,Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
        }

        [Test] public void FirstMigrationWritePreservesImmutableLegacyBytesAcrossLaterWrites()
        {
            byte[] original=HistoricalFixture(); File.WriteAllBytes(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var slot,out var error),Is.True,error);
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor()+".bak"),Is.EqualTo(original));
            byte[] firstCurrentVersion=File.ReadAllBytes(PathFor());
            slot.volume=0.9f;
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor()+".bak"),Is.EqualTo(firstCurrentVersion));
            slot.volume=0.2f;
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(),config,out var restored,out var loaded,out error),Is.True,error);
            Assert.That(restored.Currency,Is.EqualTo(2)); Assert.That(restored.UpgradeOwned,Is.True);
            Assert.That(loaded.formatVersion,Is.EqualTo(3)); Assert.That(loaded.volume,Is.EqualTo(0.2f));
        }

        [Test] public void MigrationDoesNotReplaceAnExistingLegacyArchive()
        {
            File.WriteAllBytes(PathFor(),HistoricalFixture());
            byte[] existingArchive={1,3,5,7,9}; File.WriteAllBytes(PathFor()+".v1.bak",existingArchive);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var slot,out var error),Is.True,error);
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(existingArchive));
        }

        [Test] public void FailedLegacyBackupPreventsMigrationFromReplacingPrimary()
        {
            byte[] original=HistoricalFixture(); File.WriteAllBytes(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var slot,out var error),Is.True,error);
            Directory.CreateDirectory(PathFor()+".v1.bak");
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(File.Exists(PathFor()+".bak"),Is.False);
        }

        [TestCase("game")]
        [TestCase("settings")]
        public void InvalidLegacyDataFailsMigrationWithoutChangingOriginal(string problem)
        {
            var legacy=FreshSlot(); legacy.formatVersion=1; legacy.cycle=null;
            if(problem=="game") legacy.game.currency=-1;
            else legacy.sensitivity=float.NaN;
            string json=JsonUtility.ToJson(legacy); File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out var game,out var slot,out var error),Is.False);
            Assert.That(game,Is.Null); Assert.That(slot,Is.Null); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }

        [TestCase("missing-cycle")]
        [TestCase("null-cycle")]
        [TestCase("cycle-version")]
        [TestCase("phase")]
        [TestCase("day")]
        [TestCase("remaining")]
        [TestCase("open-zero")]
        [TestCase("future-format")]
        [TestCase("missing-format")]
        public void InvalidVersionTwoSlotIsRejectedWithoutExposingPartialGame(string problem)
        {
            var data=FreshSlot(); data.formatVersion=2; data.trades=null;
            switch(problem)
            {
                case "null-cycle": data.cycle=null; break;
                case "cycle-version": data.cycle.version=99; break;
                case "phase": data.cycle.phase=(ShopDayPhase)99; break;
                case "day": data.cycle.dayNumber=0; break;
                case "remaining": data.cycle.secondsRemaining=2f; break;
                case "open-zero": data.cycle.phase=ShopDayPhase.Open; break;
                case "future-format": data.formatVersion=999; break;
            }
            string json=JsonUtility.ToJson(data);
            if(problem=="missing-cycle") json="{\"formatVersion\":2,\"game\":"+JsonUtility.ToJson(data.game)+",\"sensitivity\":1,\"volume\":0.5}";
            if(problem=="missing-format") json="{\"game\":"+JsonUtility.ToJson(data.game)+",\"cycle\":"+JsonUtility.ToJson(data.cycle)+"}";
            File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out var game,out var slot,out var error),Is.False);
            Assert.That(game,Is.Null); Assert.That(slot,Is.Null); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }

        [TestCase("{not valid json")]
        [TestCase("{\"formatVersion\":999,\"game\":{\"version\":1}}")]
        public void MalformedOrUnsupportedSlotIsRejectedWithoutOverwrite(string json)
        {
            File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out _,out _),Is.False);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }

        [TestCase("null-cycle")]
        [TestCase("invalid-cycle")]
        [TestCase("old-format")]
        [TestCase("future-format")]
        public void InvalidWritePreservesExistingPrimary(string problem)
        {
            var slot=FreshSlot(); Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            byte[] original=File.ReadAllBytes(PathFor());
            if(problem=="null-cycle") slot.cycle=null;
            if(problem=="invalid-cycle") slot.cycle.dayNumber=0;
            if(problem=="old-format") slot.formatVersion=1;
            if(problem=="future-format") slot.formatVersion=99;
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(File.Exists(PathFor()+".bak"),Is.False);
        }

        [Test] public void FailedWritePreservesExistingPrimaryAndReportsError()
        {
            var original=FreshSlot(); original.volume=0.25f;
            Assert.That(SaveStore.Write(PathFor(),original,out _),Is.True);
            string bytes=File.ReadAllText(PathFor()); Directory.CreateDirectory(PathFor()+".tmp");
            original.volume=0.9f;
            Assert.That(SaveStore.Write(PathFor(),original,out var error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(bytes));
        }

        [Test] public void AtomicReplacementKeepsPreviousSlotAsBackup()
        {
            var slot=FreshSlot(); slot.volume=0.25f;
            Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True); string original=File.ReadAllText(PathFor());
            slot.volume=0.9f; Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True);
            Assert.That(File.ReadAllText(PathFor()+".bak"),Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var read,out _),Is.True); Assert.That(read.volume,Is.EqualTo(0.9f));
            Assert.That(File.Exists(PathFor()+".v1.bak"),Is.False);
        }

        [Test] public void InvalidPrimaryDoesNotAutomaticallyLoadEitherBackup()
        {
            var slot=FreshSlot();
            Assert.That(SaveStore.Write(PathFor()+".bak",slot,out _),Is.True);
            File.WriteAllBytes(PathFor()+".v1.bak",HistoricalFixture());
            File.WriteAllText(PathFor(),"{bad primary");
            Assert.That(SaveStore.Read(PathFor(),config,out var model,out var loaded,out _),Is.False);
            Assert.That(model,Is.Null); Assert.That(loaded,Is.Null);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo("{bad primary"));
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
