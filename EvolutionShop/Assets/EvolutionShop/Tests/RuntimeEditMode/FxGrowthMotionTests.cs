using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxGrowthMotionTests
    {
        [Test] public void ProgressUsesProvidedClockAndRejectsInvalidTiming()
        {
            Assert.That(FxGrowthMotion.Progress(7.5f,10),Is.EqualTo(.25f));
            Assert.That(FxGrowthMotion.Progress(7.5f,30),Is.EqualTo(.75f));
            Assert.That(FxGrowthMotion.Progress(20,10),Is.Zero);
            Assert.That(FxGrowthMotion.Progress(-1,10),Is.EqualTo(1));
            foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity}) Assert.That(FxGrowthMotion.Progress(5,invalid),Is.Zero);
            Assert.That(FxGrowthMotion.Progress(float.NaN,10),Is.Zero);
        }
        [Test] public void MaturationIsContinuousMonotoneAndJoinsTheExistingAdultPose()
        {
            var previous=FxGrowthMotion.Resolve(0,2,1,false);
            for(int i=1;i<=1000;i++)
            {
                var pose=FxGrowthMotion.Resolve(i/1000f,2,1,false);
                Assert.That(pose.Scale.x,Is.GreaterThanOrEqualTo(previous.Scale.x));
                Assert.That(pose.Scale.y,Is.GreaterThanOrEqualTo(previous.Scale.y));
                Assert.That(pose.Scale.z,Is.GreaterThanOrEqualTo(previous.Scale.z));
                Assert.That(Vector3.Distance(pose.Scale,previous.Scale),Is.LessThan(.002f)); previous=pose;
            }
            Assert.That(previous.Scale,Is.EqualTo(Vector3.one*.62f));
            Assert.That(FxGrowthMotion.Resolve(0,2,1,false).Scale.x,Is.LessThan(previous.Scale.x*.4f));
            float t=2*.78f+1;
            Assert.That(Vector3.Distance(previous.Position,new Vector3(Mathf.Sin(t)*.04f,Mathf.Sin(t*1.8f)*.085f,0)),Is.LessThan(.00001f));
            Assert.That(previous.Angles.y,Is.EqualTo(-90+Mathf.Cos(t)*20).Within(.00001f));
        }
        [Test] public void ReducedKeepsTruthfulGrowthButRemovesTravelAndTimeDrivenPose()
        {
            for(int i=0;i<=10;i++)
            {
                var first=FxGrowthMotion.Resolve(i/10f,0,0,true);var later=FxGrowthMotion.Resolve(i/10f,37,9,true);
                Assert.That(first.Position,Is.EqualTo(Vector3.zero));Assert.That(later.Position,Is.EqualTo(first.Position));
                Assert.That(later.Angles,Is.EqualTo(first.Angles));Assert.That(later.Scale,Is.EqualTo(first.Scale));
            }
            Assert.That(FxGrowthMotion.Resolve(1,0,0,true).Scale.x,Is.GreaterThan(FxGrowthMotion.Resolve(0,0,0,true).Scale.x*2));
        }
        [Test] public void GrowerStateAndFillUseActualCapacityAndNeverPreviewAUnit()
        {
            var empty=FxGrowthMotion.ReadGrower(0,6,.01f,2);
            Assert.That(empty.State,Is.EqualTo(FxGrowerVisualState.Depleted));Assert.That(empty.Fill,Is.Zero);
            var partial=FxGrowthMotion.ReadGrower(2,8,1,2);
            Assert.That(partial.State,Is.EqualTo(FxGrowerVisualState.Refilling));Assert.That(partial.Fill,Is.EqualTo(.25f));Assert.That(partial.Refill,Is.EqualTo(.5f));
            var full=FxGrowthMotion.ReadGrower(8,8,.01f,2);
            Assert.That(full.State,Is.EqualTo(FxGrowerVisualState.Full));Assert.That(full.Fill,Is.EqualTo(1));Assert.That(full.Refill,Is.Zero);
            Assert.That(FxGrowthMotion.ReadGrower(0,0,1,2).State,Is.EqualTo(FxGrowerVisualState.Depleted));
        }
        [Test] public void CellCentersStayInsideExistingGrowerAndReducedCellsAreStationary()
        {
            foreach(var state in new[]{FxGrowthMotion.ReadGrower(3,6,1,2),FxGrowthMotion.ReadGrower(6,6,2,2)})
            for(int culture=0;culture<2;culture++)for(int cell=0;cell<6;cell++)for(int step=0;step<80;step++)
            {
                var p=FxGrowthMotion.Cell(cell,culture,step*.21f,state,false);
                Assert.That(new Vector2(p.x,p.z).magnitude,Is.LessThan(.28f));Assert.That(p.y,Is.InRange(1.10f,1.64f));
                Assert.That(FxGrowthMotion.Cell(cell,culture,step*.21f,state,true),Is.EqualTo(FxGrowthMotion.Cell(cell,culture,0,state,true)));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void BubblePathsFitVesselAndStayWithinPoolContract(bool grower)
        {
            var random=Random.state;
            for(uint sequence=1;sequence<=24;sequence++)for(int culture=0;culture<2;culture++)
            {
                FxGrowthMotion.Bubble(sequence,.7f,grower,culture,false,out var origin,out var request);
                Assert.That(request.IsValid,Is.True);
                for(int sample=0;sample<=40;sample++)
                {
                    float f=sample/40f;Vector3 p=origin+request.End*f+request.Bend*(4*f*(1-f));float radius=Mathf.Max(request.Size,request.EndSize)*.5f;
                    if(grower)
                    {Assert.That(new Vector2(p.x,p.z).magnitude+radius,Is.LessThan(.46f));Assert.That(p.y-radius,Is.GreaterThan(.93f));Assert.That(p.y+radius,Is.LessThan(1.81f));}
                    else
                    {var q=new Vector3((Mathf.Abs(p.x)+radius)/.69f,(Mathf.Abs(p.y-1.48f)+radius)/.48f,(Mathf.Abs(p.z)+radius)/.49f);Assert.That(q.sqrMagnitude,Is.LessThan(1));}
                }
                FxGrowthMotion.Bubble(sequence,.7f,grower,culture,true,out _,out var reduced);
                Assert.That(reduced.IsValid,Is.True);Assert.That(reduced.Motion,Is.EqualTo(FxParticleMotion.Drift));Assert.That(reduced.Velocity,Is.EqualTo(Vector3.zero));Assert.That(reduced.End,Is.EqualTo(Vector3.zero));
            }
            Assert.That(Random.state,Is.EqualTo(random));
        }
    }
}
