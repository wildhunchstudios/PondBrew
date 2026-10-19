using NUnit.Framework;
using UnityEngine;
namespace EvolutionShop.Tests
{
 public sealed class FxRoomAtmosphereTests
 {
  static void Step(ref float f,ref float v,bool open,float dt){FxRoomAtmosphereMotion.StepDoor(f,v,open,dt,out float n,out float nv);f=n;v=nv;Assert.That(float.IsNaN(f)||float.IsInfinity(f)||float.IsNaN(v)||float.IsInfinity(v),Is.False);Assert.That(f,Is.InRange(0f,1f));}
  [TestCase(.008f)][TestCase(.0166667f)][TestCase(.0333333f)][TestCase(.1f)]
  public void DoorConvergesWithoutOvershootAtOrdinaryFrameRates(float dt)
  {float f=0,v=0;for(int i=0;i<Mathf.CeilToInt(2/dt);i++){float old=f;Step(ref f,ref v,true,dt);Assert.That(f,Is.GreaterThanOrEqualTo(old));}Assert.That(f,Is.EqualTo(1));Assert.That(v,Is.EqualTo(0));for(int i=0;i<Mathf.CeilToInt(2/dt);i++){float old=f;Step(ref f,ref v,false,dt);Assert.That(f,Is.LessThanOrEqualTo(old));}Assert.That(f,Is.EqualTo(0));Assert.That(v,Is.EqualTo(0));}
  [Test] public void OccupiedReversalNeverContinuesClosing()
  {for(int i=1;i<20;i++){float f=i/20f,v=-2.8f,old=f;Step(ref f,ref v,true,1f/60);Assert.That(f,Is.GreaterThanOrEqualTo(old));Assert.That(v,Is.GreaterThanOrEqualTo(0));}}
  [Test] public void RepeatedReversalsRemainBoundedAndContinuous()
  {float f=.5f,v=0;for(int i=0;i<1200;i++){float old=f;Step(ref f,ref v,(i/11)%2==0,1f/120);Assert.That(Mathf.Abs(f-old),Is.LessThan(.05f));}}
  [Test] public void PausedAndInvalidDeltaDoNotMoveValidState()
  {foreach(float dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity}){float f=.4f,v=.8f;Step(ref f,ref v,true,dt);Assert.That(f,Is.EqualTo(.4f));}}
  [Test] public void LongFrameCannotEscapePhysicalTravel()
  {foreach(bool open in new[]{false,true}){float f=.5f,v=0;Step(ref f,ref v,open,float.MaxValue);Assert.That(Mathf.Abs(v),Is.LessThanOrEqualTo(10));}}
  [Test] public void LightBlendIsMonotonicAndSettlesWithoutOvershoot()
  {float old=0;for(int i=0;i<=200;i++){float value=FxRoomAtmosphereMotion.LightBlend(i*.01f);Assert.That(value,Is.InRange(0f,1f));Assert.That(value,Is.GreaterThanOrEqualTo(old));old=value;}Assert.That(old,Is.EqualTo(1));Assert.That(FxRoomAtmosphereMotion.LightBlend(0),Is.EqualTo(0));}
  [Test] public void DoorSensorUsesBothWorkerPositionsAndPreservesRoot()
  {var root=new GameObject("Door test");var l=new GameObject("left");var r=new GameObject("right");l.transform.SetParent(root.transform);r.transform.SetParent(root.transform);var door=root.AddComponent<ShopRoomDoor>();try{door.Initialize(l.transform,r.transform);var rootPosition=root.transform.position;var far=new Vector3(20,0,20);door.Tick(far,.1f,Vector3.zero);Assert.That(door.WantsOpen,Is.True);door.Initialize(l.transform,r.transform);door.Tick(far,.1f,null,Vector3.zero);Assert.That(door.WantsOpen,Is.True);door.Initialize(l.transform,r.transform);door.Tick(new Vector3(0,4,0),.1f);Assert.That(door.WantsOpen,Is.False);Assert.That(root.transform.position,Is.EqualTo(rootPosition));}finally{Object.DestroyImmediate(root);}}
 }
}
