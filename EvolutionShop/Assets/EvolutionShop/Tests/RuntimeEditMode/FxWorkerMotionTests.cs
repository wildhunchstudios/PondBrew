using NUnit.Framework;
using UnityEngine;
namespace EvolutionShop.Tests
{
 public sealed class FxWorkerMotionTests
 {
  static FxWorkerPose Pose(FxWorkerActivity a,float p=0,float gait=0,float moving=0,FxWorkerEvent e=FxWorkerEvent.None,float age=0,bool carry=false,bool reduced=false)=>FxWorkerMotion.Sample(a,p,gait,moving,2,e,age,carry,reduced);
  static void Bound(Vector3 p,float limit){Assert.That(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude),Is.False);Assert.That(p.magnitude,Is.LessThanOrEqualTo(limit));}
  [Test] public void StationaryFeetDoNotCycleDuringWorkOrEvents()
  {foreach(FxWorkerActivity a in System.Enum.GetValues(typeof(FxWorkerActivity)))foreach(FxWorkerEvent e in System.Enum.GetValues(typeof(FxWorkerEvent)))for(int i=0;i<20;i++){var p=Pose(a,i/19f,i*.7f,0,e,i*.1f);Assert.That(p.LeftLegEuler,Is.EqualTo(Vector3.zero));Assert.That(p.RightLegEuler,Is.EqualTo(Vector3.zero));}}
  [TestCase(false)][TestCase(true)] public void MovingLegsArePeriodicAndOpposed(bool carry)
  {float motion=0;var previous=Pose(FxWorkerActivity.Carry,0,0,1,carry:carry);for(int i=1;i<=360;i++){var p=Pose(FxWorkerActivity.Carry,0,i*Mathf.PI/180,1,carry:carry);Assert.That((p.LeftLegEuler+p.RightLegEuler).magnitude,Is.LessThan(.001));Assert.That((p.LeftLegEuler-previous.LeftLegEuler).magnitude,Is.LessThan(3));motion+=(p.LeftLegEuler-previous.LeftLegEuler).magnitude;previous=p;}Assert.That(motion,Is.GreaterThan(40));Assert.That(FxWorkerMotion.StrideLength,Is.InRange(.5f,2f));}
  [TestCase(false)][TestCase(true)] public void AllActivitiesAndEventsStayFiniteAndCompact(bool reduced)
  {foreach(FxWorkerActivity a in System.Enum.GetValues(typeof(FxWorkerActivity)))foreach(FxWorkerEvent e in System.Enum.GetValues(typeof(FxWorkerEvent)))for(int i=0;i<31;i++){var p=Pose(a,i/30f,i*.3f,1,e,i*.05f,a==FxWorkerActivity.Carry,reduced);Bound(p.LeftLegEuler,35);Bound(p.RightLegEuler,35);Bound(p.LeftArmEuler,100);Bound(p.RightArmEuler,100);Bound(p.HeadEuler,30);Bound(p.ParcelPosition,1.5f);}}
  [Test] public void CarryGripDoesNotSwingWithWalkingFeet()
  {var first=Pose(FxWorkerActivity.Carry,.5f,0,1,carry:true);Assert.That(first.ProxyVisible,Is.True);for(int i=1;i<40;i++){var p=Pose(FxWorkerActivity.Carry,.5f,i*.23f,1,carry:true);Assert.That(p.LeftArmEuler,Is.EqualTo(first.LeftArmEuler));Assert.That(p.RightArmEuler,Is.EqualTo(first.RightArmEuler));Assert.That(p.ParcelPosition,Is.EqualTo(first.ParcelPosition));}}
  [Test] public void IdleNeverInventsACarryProxy()
  {for(int i=0;i<100;i++)Assert.That(Pose(FxWorkerActivity.Idle,gait:i*.17f).ProxyVisible,Is.False);}
  [TestCase(FxWorkerEvent.Pickup)][TestCase(FxWorkerEvent.Dose)][TestCase(FxWorkerEvent.Place)][TestCase(FxWorkerEvent.Checkout)][TestCase(FxWorkerEvent.Refused)]
  public void ExpiredEventReturnsToUnderlyingWork(FxWorkerEvent e)
  {float duration=FxWorkerMotion.EventDuration(e);Assert.That(duration,Is.InRange(.1f,2f));var p=Pose(FxWorkerActivity.Idle,e:e,age:duration+.1f);var q=Pose(FxWorkerActivity.Idle);Assert.That((p.LeftArmEuler-q.LeftArmEuler).magnitude+(p.RightArmEuler-q.RightArmEuler).magnitude+(p.HeadEuler-q.HeadEuler).magnitude,Is.LessThan(.001));}
 }
}
