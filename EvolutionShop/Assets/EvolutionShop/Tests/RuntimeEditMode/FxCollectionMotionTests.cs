using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxCollectionMotionTests
    {
        [Test] public void RecipesAreFiniteDeterministicIrregularAndDoNotUseGlobalRandom()
        {
            var random=Random.state;var intervals=new HashSet<float>();var sizes=new HashSet<float>();var lives=new HashSet<float>();
            foreach(uint sequence in new uint[]{0,1,2,3,4,5,6,7,8,9,10,100,uint.MaxValue})
            foreach(var culture in new[]{CultureKind.Bloom,CultureKind.Flow})
            {
                FxCollectionMotion.Sample(sequence,culture,false,false,out var interval,out var local,out var request);
                FxCollectionMotion.Sample(sequence,culture,false,false,out var again,out var sameLocal,out var same);
                Assert.That(request.IsValid,Is.True);Assert.That(interval,Is.InRange(.01f,.25f));
                Assert.That(request.Bend.magnitude+request.Radius+Mathf.Max(request.Size,request.EndSize)*.5f,Is.LessThanOrEqualTo(FxCollectionMotion.PathClearance));
                Assert.That(again,Is.EqualTo(interval));Assert.That(sameLocal,Is.EqualTo(local));Assert.That(same,Is.EqualTo(request));
                Assert.That(Vector3.Distance(local,new Vector3(.36f,.93f,-.745f)),Is.LessThan(.065f));
                Assert.That(request.End,Is.EqualTo(new Vector3(0,.05f,.489f)));
                intervals.Add(interval);sizes.Add(request.Size);lives.Add(request.Lifetime);
            }
            Assert.That(intervals.Count,Is.GreaterThan(8));Assert.That(sizes.Count,Is.GreaterThan(8));Assert.That(lives.Count,Is.GreaterThan(8));Assert.That(Random.state,Is.EqualTo(random));
        }
        [Test] public void LowReducesDensityAndCulturesRetainDifferentColorAndMotion()
        {
            for(uint i=0;i<32;i++)
            {
                FxCollectionMotion.Sample(i,CultureKind.Bloom,false,false,out var standard,out _,out var bloom);
                FxCollectionMotion.Sample(i,CultureKind.Bloom,true,false,out var low,out _,out var lowBloom);
                FxCollectionMotion.Sample(i,CultureKind.Flow,false,false,out _,out _,out var flow);
                Assert.That(low,Is.GreaterThan(standard));Assert.That(lowBloom.Color,Is.EqualTo(bloom.Color));
                Assert.That(bloom.Color.r,Is.GreaterThan(bloom.Color.b));Assert.That(flow.Color.b,Is.GreaterThan(flow.Color.r));
                Assert.That(bloom.Turns*flow.Turns,Is.LessThan(0));
            }
        }
        [Test] public void ReducedKeepsLocalFeedbackWithoutTravelExpansionOrSpin()
        {
            for(uint i=0;i<16;i++)
            {
                FxCollectionMotion.Sample(i,CultureKind.Flow,false,true,out var interval,out var local,out var p);
                Assert.That(p.IsValid,Is.True);Assert.That(p.Motion,Is.EqualTo(FxParticleMotion.Drift));Assert.That(p.Velocity,Is.EqualTo(Vector3.zero));
                Assert.That(p.Radius+p.Turns+p.Spin,Is.Zero);Assert.That(p.Size,Is.EqualTo(p.EndSize));Assert.That(interval,Is.GreaterThan(.2f));
                Assert.That(local,Is.EqualTo(FxCollectionMotion.OutletLocal));
            }
        }
        [Test] public void AcceptedResponseHasBoundedSettleAndInvalidClockCannotPulse()
        {
            Assert.That(FxCollectionMotion.Response(FxCollectionMotion.ResponseDuration,false),Is.Zero);
            Assert.That(FxCollectionMotion.Response(FxCollectionMotion.ResponseDuration*.5f,false),Is.GreaterThan(.9f));
            foreach(float invalid in new[]{-1f,0f,float.NaN,float.PositiveInfinity})Assert.That(FxCollectionMotion.Response(invalid,false),Is.Zero);
            for(int i=0;i<=100;i++)
            {float remaining=i*FxCollectionMotion.ResponseDuration/100;Assert.That(FxCollectionMotion.Response(remaining,false),Is.InRange(0f,1f));Assert.That(FxCollectionMotion.Response(remaining,true),Is.Zero);}
        }
        [Test] public void ChamberResponseKeepsAllEightCentersInsideBothReservoirSizes()
        {
            for(int unit=0;unit<8;unit++)for(int frame=0;frame<100;frame++)
            {
                FxCollectionMotion.ReservoirPose(unit,frame*.13f,frame/99f,false,out var point,out var rotation);
                Assert.That(new Vector2(point.x,point.z).magnitude,Is.LessThan(.07f));Assert.That(point.y,Is.InRange(-.08f,.08f));
                Assert.That(float.IsNaN(rotation.x),Is.False);
                FxCollectionMotion.ReservoirPose(unit,frame*.13f,1,true,out var reduced,out var reducedRotation);
                FxCollectionMotion.ReservoirPose(unit,frame*.13f,0,true,out var quiet,out var quietRotation);
                Assert.That(reduced,Is.EqualTo(quiet));Assert.That(reducedRotation,Is.EqualTo(quietRotation));
            }
        }
        [TestCase(CultureKind.Bloom)] [TestCase(CultureKind.Flow)]
        public void ActualPoolCurveFollowsTranslatedRotatedMouthAndClosesAtItsTip(CultureKind culture)
        {
            var root=new GameObject("Collection endpoint test");var source=new GameObject("Outlet");var mouth=new GameObject("Mouth");
            source.transform.SetParent(root.transform);mouth.transform.SetParent(root.transform);
            var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.up,Vector3.right};mesh.triangles=new[]{0,1,2};
            var cues=new FxCueRuntime();var token=cues.Register("collection-test","exact-unit");var pool=new FxEffectPool(root.transform,mesh);
            try
            {
                source.transform.SetPositionAndRotation(new Vector3(2,0,1),Quaternion.Euler(0,35,0));mouth.transform.SetPositionAndRotation(new Vector3(1.2f,1.3f,-.2f),Quaternion.Euler(-8,10,-5));mouth.transform.localScale=Vector3.one*.62f;
                FxCollectionMotion.Sample(7,culture,false,false,out _,out var local,out var recipe);
                Assert.That(pool.EmitParticle(cues,token,source.transform,local,recipe,true,mouth.transform),Is.True);
                float step=recipe.Lifetime*.98f/40;
                for(int frame=0;frame<40;frame++)
                {
                    mouth.transform.position+=new Vector3(.002f,.001f,.001f);mouth.transform.Rotate(0,.1f,0);pool.Tick(cues,step);
                    Assert.That(pool.TryGetParticleForValidation(token,0,out var start,out var end,out var position,out var age,out var life),Is.True);
                    Assert.That(Vector3.Distance(start,source.transform.TransformPoint(FxCollectionMotion.OutletLocal)),Is.LessThan(.00001f));
                    Assert.That(Vector3.Distance(end,mouth.transform.TransformPoint(FxCollectionMotion.MouthLocal)),Is.LessThan(.00001f));
                    var axis=end-start;float f=Vector3.Dot(position-start,axis)/axis.sqrMagnitude;var projected=start+axis*Mathf.Clamp01(f);
                    Assert.That(Vector3.Distance(position,projected),Is.LessThan(.20f),"Entire actual pooled path fits the collision envelope");
                    if(frame==39)Assert.That(Vector3.Distance(position,end),Is.LessThan(.06f),"Late bead converges at the current moving mouth");
                }
                pool.Tick(cues,recipe.Lifetime);Assert.That(pool.ParticleCountForValidation(token),Is.Zero);
            }
            finally {pool.Dispose();cues.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
    }
}
