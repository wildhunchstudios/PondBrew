using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
namespace EvolutionShop.Tests
{
 public sealed class FxComfortTests
 {
  string directory; GameConfig config;
  [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"EvolutionShop-FX20-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);config=GameConfig.CreateDefault();}
  [TearDown] public void Cleanup(){if(!Directory.Exists(directory))return;Assert.That(Path.GetFileName(directory),Does.StartWith("EvolutionShop-FX20-"));Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)),Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);Directory.Delete(directory,true);}
  string PathFor()=>Path.Combine(directory,"slot.json");
  SlotData Fresh()=>new SlotData{game=new GameModel(config).CaptureSave(),cycle=new ShopDayCycle().CaptureSave(),volume=.31f,humorVolume=.27f,ambienceVolume=.43f,reducedGags=true};
  [TestCase(false,0)][TestCase(false,1)][TestCase(true,0)][TestCase(true,1)]
  public void FourSavedModesPreserveIndependentAudioGagAndGame(bool reduced,int detail)
  {
   var slot=Fresh();slot.reducedVisualMotion=reduced;slot.effectsDetail=detail;string game=JsonUtility.ToJson(slot.game);
   Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);Assert.That(SaveStore.Read(PathFor(),config,out var model,out var loaded,out error),Is.True,error);
   Assert.That(loaded.reducedVisualMotion,Is.EqualTo(reduced));Assert.That(loaded.effectsDetail,Is.EqualTo(detail));Assert.That(loaded.volume,Is.EqualTo(.31f));Assert.That(loaded.humorVolume,Is.EqualTo(.27f));Assert.That(loaded.ambienceVolume,Is.EqualTo(.43f));Assert.That(loaded.reducedGags,Is.True);Assert.That(JsonUtility.ToJson(model.CaptureSave()),Is.EqualTo(game));
  }
  [TestCase(-1)][TestCase(2)][TestCase(int.MaxValue)]
  public void InvalidDetailCannotReplaceDurableSlot(int detail)
  {var slot=Fresh();Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True);byte[] before=File.ReadAllBytes(PathFor());slot.effectsDetail=detail;Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.False);CollectionAssert.AreEqual(before,File.ReadAllBytes(PathFor()));}
  [TestCase("reducedVisualMotion",null)][TestCase("effectsDetail",null)]
  [TestCase("reducedVisualMotion","0")][TestCase("reducedVisualMotion","\"false\"")][TestCase("reducedVisualMotion","null")]
  [TestCase("effectsDetail","true")][TestCase("effectsDetail","\"1\"")][TestCase("effectsDetail","1.5")][TestCase("effectsDetail","-1")][TestCase("effectsDetail","2")][TestCase("effectsDetail","null")]
  public void CurrentMalformedComfortFieldIsRejectedWithoutWriting(string field,string replacement)
  {
   string json=JsonUtility.ToJson(Fresh());string expression="\\\""+field+"\\\":(?:true|false|-?[0-9.]+)";
   Assert.That(Regex.Matches(json,expression).Count,Is.EqualTo(1));json=replacement==null?Regex.Replace(json,",?"+expression,""):Regex.Replace(json,expression,"\""+field+"\":"+replacement);
   File.WriteAllText(PathFor(),json);byte[] before=File.ReadAllBytes(PathFor());Assert.That(SaveStore.Read(PathFor(),config,out _,out _,out _),Is.False);CollectionAssert.AreEqual(before,File.ReadAllBytes(PathFor()));
  }
  [TestCase("reducedVisualMotion","true")][TestCase("effectsDetail","1")]
  public void DuplicateCurrentComfortFieldsAreRejectedWithoutReplacingBytes(string field,string value)
  {string json=JsonUtility.ToJson(Fresh());json=json.Substring(0,json.Length-1)+",\""+field+"\":"+value+"}";File.WriteAllText(PathFor(),json);byte[] before=File.ReadAllBytes(PathFor());Assert.That(SaveStore.Read(PathFor(),config,out _,out _,out _),Is.False);CollectionAssert.AreEqual(before,File.ReadAllBytes(PathFor()));}
  [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)][TestCase(9)][TestCase(10)][TestCase(11)][TestCase(12)][TestCase(13)][TestCase(14)][TestCase(15)][TestCase(16)][TestCase(17)][TestCase(18)][TestCase(19)][TestCase(20)][TestCase(21)][TestCase(22)]
  public void ActualHistoricalEnvelopeDefaultsVisualComfortWithoutRewriting(int format)
  {
   var slot=Fresh();slot.formatVersion=format;
   int gameVersion=format<4?1:format<13?2:format==13?3:format==14?4:format==15?5:6;
   slot.game=gameVersion<=3?LegacyGameSaveFixture.AsVersion(slot.game,gameVersion):slot.game;slot.game.version=gameVersion;
   if(gameVersion==1)slot.game.prices=null;
   if(gameVersion<6)slot.game.growers=Array.FindAll(slot.game.growers,g=>g==null||g.cultureId!="mist");
   slot.trades.version=format==3?1:format==4?2:format<7?3:4;
   slot.layout.version=format==8?1:format==9?2:format==10?3:format==11?4:format==12?5:format<=14?6:7;
   // Historical envelope/game/report/layout versions match their actual introduction boundaries.
   string json="{\"formatVersion\":"+format+",\"game\":"+JsonUtility.ToJson(slot.game)+",\"sensitivity\":1,\"volume\":0.31";
   if(format>=2)json+=",\"cycle\":"+JsonUtility.ToJson(slot.cycle);
   if(format>=3)json+=",\"trades\":"+JsonUtility.ToJson(slot.trades);
   if(format>=6)json+=",\"reputation\":"+JsonUtility.ToJson(slot.reputation);
   if(format>=7)json+=",\"slapKey\":\"F\",\"humorVolume\":0.27,\"reducedGags\":true";
   if(format>=8)json+=",\"layout\":"+JsonUtility.ToJson(slot.layout);
   if(format>=17)json+=",\"staff\":"+JsonUtility.ToJson(slot.staff);
   if(format>=18)json+=",\"stocker\":"+JsonUtility.ToJson(slot.stocker);
   if(format>=19)json+=",\"technician\":"+JsonUtility.ToJson(slot.technician);
   if(format>=20)json+=",\"tutorial\":"+JsonUtility.ToJson(slot.tutorial);
   if(format>=21)json+=",\"automation\":"+JsonUtility.ToJson(slot.automation);
   if(format>=22)json+=",\"ambienceVolume\":0.43";
   json+="}";File.WriteAllText(PathFor(),json);byte[] before=File.ReadAllBytes(PathFor());
   Assert.That(SaveStore.Read(PathFor(),config,out var model,out var loaded,out var error),Is.True,error);Assert.That(loaded.formatVersion,Is.EqualTo(23));Assert.That(loaded.reducedVisualMotion,Is.False);Assert.That(loaded.effectsDetail,Is.Zero);Assert.That(loaded.volume,Is.EqualTo(.31f));Assert.That(loaded.humorVolume,Is.EqualTo(format<7?1f:.27f));Assert.That(loaded.reducedGags,Is.EqualTo(format>=7));Assert.That(loaded.ambienceVolume,Is.EqualTo(format<22?.5f:.43f));Assert.That(model.Currency,Is.Zero);CollectionAssert.AreEqual(before,File.ReadAllBytes(PathFor()));
  }
 }
}
