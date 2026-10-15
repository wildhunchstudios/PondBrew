using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxPlayerInteractionTests
    {
        [Test] public void InvalidMovementInputsFallBackToStationaryGrip()
        {
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            foreach(var pose in new[]{FxHandPose.Move(invalid,1,false),FxHandPose.Move(1,invalid,false)}){
                Assert.That(pose.ToolOffset,Is.EqualTo(Vector3.zero));Assert.That(pose.ToolAngles,Is.EqualTo(Vector3.zero));
            }
        }
        [Test] public void MovementSwayHasFiniteBoundedLateralAndVerticalMotion()
        {
            float x=0,y=0;
            for(int i=-1000;i<=1000;i++){
                var pose=FxHandPose.Move(i*.031f,1,false);
                Assert.That(float.IsNaN(pose.ToolOffset.x)||float.IsInfinity(pose.ToolOffset.x),Is.False);
                Assert.That(pose.ToolOffset.magnitude,Is.LessThan(.02f),"Grip must remain within a two-centimetre motion envelope");
                Assert.That(pose.ToolAngles.magnitude,Is.LessThan(3),"Tool-only motion must stay restrained");
                Assert.That(pose.ToolOffset.z,Is.EqualTo(0),"Movement must not thrust the nozzle into the station");
                Assert.That(pose.LeftOffset,Is.EqualTo(Vector3.zero),"Movement does not independently detach supporting wrist");
                x=Mathf.Max(x,Mathf.Abs(pose.ToolOffset.x));y=Mathf.Max(y,Mathf.Abs(pose.ToolOffset.y));
            }
            Assert.That(x,Is.GreaterThan(0));Assert.That(y,Is.GreaterThan(0));
        }
        [Test] public void StoppedWeightAndReducedPolicyHaveNoResidualMovement()
        {
            for(int i=0;i<300;i++)foreach(bool reduced in new[]{false,true}){
                var pose=FxHandPose.Move(i*.17f,reduced?1:0,reduced);
                Assert.That(pose.ToolOffset,Is.EqualTo(Vector3.zero));Assert.That(pose.ToolAngles,Is.EqualTo(Vector3.zero));
            }
        }
        [Test] public void MovementBlendCannotOvershootOrInvertFromInvalidWeight()
        {
            for(int i=0;i<100;i++){
                float phase=i*.123f;
                var negative=FxHandPose.Move(phase,-2,false);var over=FxHandPose.Move(phase,9,false);var full=FxHandPose.Move(phase,1,false);
                Assert.That(negative.ToolOffset,Is.EqualTo(Vector3.zero));Assert.That(negative.ToolAngles,Is.EqualTo(Vector3.zero));
                Assert.That(over.ToolOffset,Is.EqualTo(full.ToolOffset));Assert.That(over.ToolAngles,Is.EqualTo(full.ToolAngles));
            }
        }
        [Test] public void MovementBlendScalesContinuouslyTowardRest()
        {
            for(int i=0;i<100;i++){
                float phase=i*.17f;var full=FxHandPose.Move(phase,1,false);
                var half=FxHandPose.Move(phase,.5f,false);var quarter=FxHandPose.Move(phase,.25f,false);
                Assert.That(Vector3.Distance(half.ToolOffset,full.ToolOffset*.5f),Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(quarter.ToolAngles,full.ToolAngles*.25f),Is.LessThan(.00001f));
            }
        }
        [Test] public void SwayRepeatsWithoutPhaseSeamOrAccumulatedDisplacement()
        {
            for(int i=0;i<100;i++){
                float phase=i*.17f;var first=FxHandPose.Move(phase,1,false);var repeat=FxHandPose.Move(phase+4*Mathf.PI,1,false);
                Assert.That(Vector3.Distance(first.ToolOffset,repeat.ToolOffset),Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(first.ToolAngles,repeat.ToolAngles),Is.LessThan(.0001f));
            }
        }
    }
}