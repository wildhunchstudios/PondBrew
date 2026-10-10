using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class AutomationSaveTests
    {
        const string Prefix="EvolutionShop-AutomationSaveTests-";
        const string RequestId="78dc8d9afebd48d2a29c822e667e2736";
        string directory;
        string PathFor()=>Path.Combine(directory,"slot.json");
        static GameConfig Config=>GameConfig.CreateDefault();
        [SetUp] public void Setup()
        { directory=Path.Combine(Path.GetFullPath(Path.GetTempPath()),Prefix+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory); }
        [TearDown] public void Cleanup()
        {
            if(string.IsNullOrEmpty(directory)||!Directory.Exists(directory))return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix,StringComparison.Ordinal),Is.True);
            Directory.Delete(directory,true);
        }
        static SlotData Fixture(bool pending=true)
        {
            var state=new GameModel(Config).CaptureSave();state.currency=300;state.upgradeOwned=true;state.discoveries=new[]{"pondlet"};
            Assert.That(GameModel.TryRestore(Config,state,out var game,out _),Is.True);
            var a=new ShopAutomation();Assert.That(a.Purchase(game,true)&&a.Purchase(game,false),Is.True);
            a.SetEnabled(true,true);a.SetEnabled(false,true);a.SelectRecipe(game,"pondlet");
            if(pending)Assert.That(a.Request(game,RequestId),Is.True);
            return new SlotData{game=game.CaptureSave(),cycle=new ShopDayCycle().CaptureSave(),automation=a.CaptureSave()};
        }
        [TestCase(false)] [TestCase(true)]
        public void RoundTripRetainsPartialCollectionPendingIdentityAndDisabledSettings(bool disabled)
        {
            var slot=Fixture();Assert.That(GameModel.TryRestore(Config,slot.game,out var game,out _),Is.True);
            Assert.That(ShopAutomation.TryRestore(slot.automation,game,out var a),Is.True);Assert.That(a.Step(game,false),Is.True);
            if(disabled){a.SetEnabled(true,false);a.SetEnabled(false,false);}
            slot.game=game.CaptureSave();slot.automation=a.CaptureSave();
            Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);byte[] original=File.ReadAllBytes(PathFor());
            for(int i=0;i<3;i++)
            {
                Assert.That(SaveStore.Read(PathFor(),Config,out var loadedGame,out var loaded,out error),Is.True,error);
                Assert.That(JsonUtility.ToJson(loadedGame.CaptureSave()),Is.EqualTo(JsonUtility.ToJson(slot.game)));
                Assert.That(JsonUtility.ToJson(loaded.automation),Is.EqualTo(JsonUtility.ToJson(slot.automation)));
                Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));Assert.That(loaded.formatVersion,Is.EqualTo(21));
            }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)]
        [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void EveryLegacyVersionDiscardsInjectedModulesAndArchivesExactOriginal(int format)
        {
            var slot=new SlotData{formatVersion=format,game=new GameModel(Config).CaptureSave(),cycle=new ShopDayCycle().CaptureSave(),automation=Fixture().automation};
            slot.game.version=SaveStore.GameVersionFor(format);
            if(format<=13)slot.game=LegacyGameSaveFixture.AsVersion(slot.game,slot.game.version);
            else if(format<16)slot.game.growers=Array.FindAll(slot.game.growers,g=>g.cultureId!="mist");
            if(format>=3)slot.trades.version=SaveStore.ReportVersionFor(format);
            if(format>=8)slot.layout.version=format==8?1:format==9?2:format==10?3:format==11?4:format==12?5:format<=14?6:7;
            File.WriteAllText(PathFor(),JsonUtility.ToJson(slot));byte[] original=File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(),Config,out var game,out var loaded,out var error),Is.True,error);
            Assert.That(JsonUtility.ToJson(loaded.automation),Is.EqualTo(JsonUtility.ToJson(new ShopAutomation().CaptureSave())));
            Assert.That(game.Currency,Is.Zero);Assert.That(game.UpgradeOwned,Is.False);Assert.That(game.DiscoveredSpeciesIds,Is.Empty);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(SaveStore.Write(PathFor(),loaded,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v"+format+".bak"),Is.EqualTo(original));
            Assert.That(SaveStore.Write(PathFor(),loaded,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v"+format+".bak"),Is.EqualTo(original));
        }
        [TestCase("version")] [TestCase("collectorOwned")] [TestCase("collectorEnabled")] [TestCase("doserOwned")]
        [TestCase("doserEnabled")] [TestCase("recipeId")] [TestCase("requestId")]
        public void CurrentRequiresEachExplicitModuleField(string field)
        {
            var slot=Fixture();string automation=JsonUtility.ToJson(slot.automation);
            string value=field=="version"?"1":field=="recipeId"?"\"pondlet\"":field=="requestId"?"\""+slot.automation.requestId+"\"":"true";
            string pair="\""+field+"\":"+value;
            string changed=automation.Contains(pair+",")?automation.Replace(pair+",",""):automation.Replace(","+pair,"");
            Assert.That(changed,Is.Not.EqualTo(automation));Reject(JsonUtility.ToJson(slot).Replace(automation,changed));
        }
        [TestCase("null")] [TestCase("{}")] [TestCase("[]")] [TestCase("false")]
        [TestCase("\"bad\"")]
        public void CurrentRejectsMalformedModuleContainer(string replacement)
        { var slot=Fixture();Reject(JsonUtility.ToJson(slot).Replace(JsonUtility.ToJson(slot.automation),replacement)); }
        [TestCase("collectorOwned","1")] [TestCase("collectorEnabled","null")] [TestCase("doserOwned","\"true\"")]
        [TestCase("doserEnabled","{}")] [TestCase("version","1.5")] [TestCase("recipeId","7")] [TestCase("requestId","false")]
        public void CurrentRejectsIncorrectPrimitiveTypes(string field,string bad)
        {
            var slot=Fixture();string a=JsonUtility.ToJson(slot.automation);
            string value=field=="version"?"1":field=="recipeId"?"\"pondlet\"":field=="requestId"?"\""+slot.automation.requestId+"\"":"true";
            string changed=a.Replace("\""+field+"\":"+value,"\""+field+"\":"+bad);Assert.That(changed,Is.Not.EqualTo(a));Reject(JsonUtility.ToJson(slot).Replace(a,changed));
        }
        [Test]
        public void CurrentRejectsMissingDuplicateAndSemanticallyInvalidModules()
        {
            var slot=Fixture();string json=JsonUtility.ToJson(slot),a=JsonUtility.ToJson(slot.automation);
            Reject(json.Replace(",\"automation\":"+a,""));
            Reject(json.Replace("\"automation\":"+a,"\"automation\":"+a+",\"automation\":"+a));
            Reject(json.Replace(a,a.Replace("\"version\":1","\"version\":1,\"version\":1")));
            slot.automation.collectorOwned=false;Reject(JsonUtility.ToJson(slot));
        }
        [Test]
        public void InvalidWriteAndTemporaryObstructionPreservePreviousFileAndLiveSnapshot()
        {
            var slot=Fixture();Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);byte[] original=File.ReadAllBytes(PathFor());
            string before=JsonUtility.ToJson(slot);slot.automation.requestId="bad";
            Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.False);Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            slot=JsonUtility.FromJson<SlotData>(before);Directory.CreateDirectory(PathFor()+".tmp");
            try{Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.False);Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));Assert.That(JsonUtility.ToJson(slot),Is.EqualTo(before));}
            finally{Directory.Delete(PathFor()+".tmp");}
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
        }
        void Reject(string json)
        {
            File.WriteAllText(PathFor(),json);byte[] before=File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(),Config,out var game,out var slot,out var error),Is.False);
            Assert.That(game,Is.Null);Assert.That(slot,Is.Null);Assert.That(error,Is.Not.Empty);Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(before));
        }
    }
}
