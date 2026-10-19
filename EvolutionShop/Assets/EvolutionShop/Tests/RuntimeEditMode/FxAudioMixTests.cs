using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
namespace EvolutionShop.Tests
{
 public sealed class FxAudioMixTests
 {
  [TestCase(false)][TestCase(true)]
  public void ExactLoopPcmIsFiniteQuietDcFreeAndPeriodic(bool machinery)
  {
   var a=FxAudioMix.MakeLoopSamples(machinery);var b=FxAudioMix.MakeLoopSamples(machinery);
   Assert.That(a.Length,Is.EqualTo(88200));CollectionAssert.AreEqual(a,b);
   double sum=0,square=0;float peak=0,maxStep=0;
   for(int i=0;i<a.Length;i++){Assert.That(float.IsNaN(a[i])||float.IsInfinity(a[i]),Is.False);sum+=a[i];square+=(double)a[i]*a[i];peak=Math.Max(peak,Math.Abs(a[i]));if(i>0)maxStep=Math.Max(maxStep,Math.Abs(a[i]-a[i-1]));}
   Assert.That(peak,Is.InRange(.0001f,.14001f));Assert.That(Math.Sqrt(square/a.Length),Is.LessThanOrEqualTo(.05001));Assert.That(Math.Abs(sum/a.Length),Is.LessThanOrEqualTo(.0001));
   // The periodic join must not introduce a larger step than an ordinary adjacent sample.
   Assert.That(Math.Abs(a[0]-a[a.Length-1]),Is.LessThanOrEqualTo(maxStep+1e-6f));
  }
  [TestCase(false,.07f)][TestCase(true,.28f)]
  public void NewShortCuePcmHasQuietSilentEndpoints(bool door,float seconds)
  {
   var a=FxAudioMix.MakeQuietCueSamples(door);Assert.That(a.Length,Is.EqualTo((int)(22050*seconds)).Within(1));
   float peak=0;foreach(float x in a){Assert.That(float.IsNaN(x)||float.IsInfinity(x),Is.False);peak=Math.Max(peak,Math.Abs(x));}
   Assert.That(peak,Is.InRange(.0001f,.14001f));Assert.That(Math.Abs(a[0]),Is.LessThan(1e-5));Assert.That(Math.Abs(a[a.Length-1]),Is.LessThan(1e-5));
  }
  [Test] public void DistanceAndClosedPartitionReduceGain()
  {
   var source=new Vector3(0,1,2);float near=FxAudioMix.Attenuation(new Vector3(0,1,1),source,0,0),far=FxAudioMix.Attenuation(new Vector3(0,1,12),source,0,0);
   float closed=FxAudioMix.Attenuation(new Vector3(0,1,-1),source,0,0),open=FxAudioMix.Attenuation(new Vector3(0,1,-1),source,0,1);
   Assert.That(near,Is.InRange(0f,1f));Assert.That(far,Is.LessThan(near));Assert.That(closed,Is.LessThan(open));Assert.That(open,Is.LessThanOrEqualTo(near));
  }
  [Test] public void GainRampIsBoundedAndReversibleWithoutOvershoot()
  {
   float gain=0;for(int i=0;i<120;i++){gain=FxAudioMix.StepGain(gain,.26f,1f/120,.2f);Assert.That(gain,Is.InRange(0,.26001));}
   Assert.That(gain,Is.EqualTo(.26f).Within(.001f));for(int i=0;i<120;i++){gain=FxAudioMix.StepGain(gain,0,1f/120,.2f);Assert.That(gain,Is.InRange(0,.26001));}Assert.That(gain,Is.LessThan(.001f));
  }
  [TestCase(float.NaN)][TestCase(float.PositiveInfinity)][TestCase(float.NegativeInfinity)]
  public void MalformedGainInputsCannotPoisonMixer(float invalid)
  {
   foreach(float x in new[]{FxAudioMix.StepGain(invalid,.2f,.1f,.2f),FxAudioMix.StepGain(.1f,invalid,.1f,.2f),FxAudioMix.StepGain(.1f,.2f,invalid,.2f),FxAudioMix.Attenuation(new Vector3(invalid,0,0),Vector3.zero,0,0)})Assert.That(float.IsNaN(x)||float.IsInfinity(x),Is.False);
  }
  string directory;GameConfig config;
  [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"EvolutionShop-FX19-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);config=GameConfig.CreateDefault();}
  [TearDown]public void Cleanup(){if(Directory.Exists(directory)){Assert.That(Path.GetFileName(directory),Does.StartWith("EvolutionShop-FX19-"));Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)),Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);Directory.Delete(directory,true);}}
  SlotData Fresh()=>new SlotData{game=new GameModel(config).CaptureSave(),cycle=new ShopDayCycle().CaptureSave(),volume=.31f,humorVolume=.27f};
  [TestCase(0f)][TestCase(.5f)][TestCase(1f)]public void CurrentAmbienceRoundTripPreservesExplicitZeroAndOtherSettings(float gain)
  {var slot=Fresh();slot.ambienceVolume=gain;string path=Path.Combine(directory,"slot.json");Assert.That(SaveStore.Write(path,slot,out var error),Is.True,error);Assert.That(SaveStore.Read(path,config,out var model,out var read,out error),Is.True,error);Assert.That(read.ambienceVolume,Is.EqualTo(gain));Assert.That(read.volume,Is.EqualTo(.31f));Assert.That(read.humorVolume,Is.EqualTo(.27f));Assert.That(JsonUtility.ToJson(model.CaptureSave()),Is.EqualTo(JsonUtility.ToJson(slot.game)));}
  [Test]public void Legacy21MissingAmbienceMigratesWithoutRewritingBytes()
  {var slot=Fresh();slot.formatVersion=21;string json=Regex.Replace(JsonUtility.ToJson(slot),",?\"ambienceVolume\":-?[0-9.]+","");string path=Path.Combine(directory,"legacy.json");File.WriteAllText(path,json);Assert.That(SaveStore.Read(path,config,out _,out var read,out var error),Is.True,error);Assert.That(read.ambienceVolume,Is.EqualTo(.5f));Assert.That(read.volume,Is.EqualTo(.31f));Assert.That(read.humorVolume,Is.EqualTo(.27f));Assert.That(File.ReadAllText(path),Is.EqualTo(json));}
  [Test]public void Legacy22RetainsAmbienceAndDefaultsNewVisualSettings()
  {var slot=Fresh();slot.formatVersion=22;slot.ambienceVolume=.19f;string json=Regex.Replace(JsonUtility.ToJson(slot),",?\"reducedVisualMotion\":(?:true|false)","");json=Regex.Replace(json,",?\"effectsDetail\":-?[0-9]+","");string path=Path.Combine(directory,"legacy22.json");File.WriteAllText(path,json);Assert.That(SaveStore.Read(path,config,out _,out var read,out var error),Is.True,error);Assert.That(read.ambienceVolume,Is.EqualTo(.19f));Assert.That(read.reducedVisualMotion,Is.False);Assert.That(read.effectsDetail,Is.Zero);Assert.That(File.ReadAllText(path),Is.EqualTo(json));byte[] original=File.ReadAllBytes(path);Assert.That(SaveStore.Write(path,read,out error),Is.True,error);CollectionAssert.AreEqual(original,File.ReadAllBytes(path+".v22.bak"));read.effectsDetail=1;Assert.That(SaveStore.Write(path,read,out error),Is.True,error);CollectionAssert.AreEqual(original,File.ReadAllBytes(path+".v22.bak"));}
  [Test]public void CurrentMissingAmbienceDoesNotSilentlyBecomeDefault()
  {string json=Regex.Replace(JsonUtility.ToJson(Fresh()),",?\"ambienceVolume\":-?[0-9.]+","");string path=Path.Combine(directory,"missing.json");File.WriteAllText(path,json);Assert.That(SaveStore.Read(path,config,out _,out _,out _),Is.False);}
  [TestCase(-.01f)][TestCase(1.01f)][TestCase(float.NaN)][TestCase(float.PositiveInfinity)]public void InvalidAmbienceCannotReplaceDurableSlot(float gain)
  {string path=Path.Combine(directory,"bad.json");var slot=Fresh();Assert.That(SaveStore.Write(path,slot,out _),Is.True);string before=File.ReadAllText(path);slot.ambienceVolume=gain;Assert.That(SaveStore.Write(path,slot,out _),Is.False);Assert.That(File.ReadAllText(path),Is.EqualTo(before));}
 }
}
