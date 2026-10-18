using NUnit.Framework;
using UnityEngine;
namespace EvolutionShop.Tests
{
 public sealed class FxComedyMotionTests
 {
  static float Magnitude(FxComedyPose p)=>(p.LeftArmEuler-new Vector3(0,0,-7)).magnitude+(p.RightArmEuler-new Vector3(0,0,7)).magnitude+p.HeadEuler.magnitude+p.UpperEuler.magnitude;
  static void Finite(Vector3 p){Assert.That(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude),Is.False);Assert.That(p.magnitude,Is.LessThanOrEqualTo(120));}
  [TestCase(false)][TestCase(true)] public void EveryIncidentReturnsToRestWithoutUnboundedPose(bool reduced)
  {foreach(ComedyKind kind in System.Enum.GetValues(typeof(ComedyKind)))for(int variant=0;variant<2;variant++){for(int i=0;i<=300;i++){var p=FxComedyMotion.Sample(kind,i*.01f,reduced,variant);Finite(p.LeftArmEuler);Finite(p.RightArmEuler);Finite(p.HeadEuler);Finite(p.UpperEuler);Assert.That(p.Envelope,Is.InRange(0f,1f));Assert.That(p.Contact,Is.InRange(0f,1f));}Assert.That(Magnitude(FxComedyMotion.Sample(kind,5,reduced,variant)),Is.LessThan(.001f));}}
  [TestCase(false)][TestCase(true)] public void ContactFitsOriginalReactionWindow(bool reduced)
  {float contact=FxComedyMotion.SlapContactSeconds(reduced),duration=FxComedyMotion.SlapDuration(reduced);Assert.That(contact,Is.GreaterThan(0));Assert.That(contact,Is.LessThan(duration));Assert.That(duration,Is.EqualTo(reduced?.32f:.75f));Assert.That(FxComedyMotion.Sample(ComedyKind.Slap,duration+.01f,reduced,0).Envelope,Is.EqualTo(0));}
  [Test] public void ReducedIncidentsRemainSmallerAcrossTheirWholeEnvelope()
  {foreach(ComedyKind kind in System.Enum.GetValues(typeof(ComedyKind))){float normal=0,reduced=0;for(int i=0;i<300;i++){normal=Mathf.Max(normal,Magnitude(FxComedyMotion.Sample(kind,i*.01f,false,0)));reduced=Mathf.Max(reduced,Magnitude(FxComedyMotion.Sample(kind,i*.01f,true,0)));}Assert.That(normal,Is.GreaterThan(.01f),kind.ToString());Assert.That(reduced,Is.LessThanOrEqualTo(normal+.001f));}}
  [TestCase(false)][TestCase(true)] public void WhiffHasShorterReachAndBothGesturesReturnToStart(bool reduced)
  {FxComedyMotion.SampleSlapHand(0,reduced,false,out var origin,out var rotation,out var scale);float contact=FxComedyMotion.SlapContactSeconds(reduced);FxComedyMotion.SampleSlapHand(contact,reduced,false,out var hit,out _,out _);FxComedyMotion.SampleSlapHand(contact,reduced,true,out var miss,out _,out _);Assert.That(Vector3.Distance(origin,miss),Is.LessThan(Vector3.Distance(origin,hit)));foreach(bool whiff in new[]{false,true}){FxComedyMotion.SampleSlapHand(2,reduced,whiff,out var end,out var endRotation,out var endScale);Assert.That(Vector3.Distance(end,origin),Is.LessThan(.0001));Assert.That(Quaternion.Angle(rotation,endRotation),Is.LessThan(.001));Assert.That(endScale,Is.EqualTo(scale));}}
  [Test] public void InvalidElapsedCannotGenerateNonfiniteGeometry()
  {foreach(float t in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f,float.MaxValue})foreach(ComedyKind kind in System.Enum.GetValues(typeof(ComedyKind))){var p=FxComedyMotion.Sample(kind,t,false,0);Finite(p.LeftArmEuler);Finite(p.RightArmEuler);Finite(p.HeadEuler);Finite(p.UpperEuler);}}
 }
}
