using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EvolutionShop.Tests
{
    public sealed class FxResearchAutomationTests
    {
        [TestCase(CultureKind.Bloom)] [TestCase(CultureKind.Flow)] [TestCase(CultureKind.Spark)] [TestCase(CultureKind.Mist)]
        public void AcceptedTransferRecipesAreFiniteDeterministicAndLeaveGameplayRandomUntouched(CultureKind culture)
        {
            var random=UnityEngine.Random.state;
            foreach(bool doser in new[]{false,true})foreach(bool reduced in new[]{false,true})for(int i=0;i<12;i++) {
                FxResearchAutomationMotion.SampleTransfer(culture,i,71,doser,reduced,out var local,out var request);
                FxResearchAutomationMotion.SampleTransfer(culture,i,71,doser,reduced,out var again,out var same);
                Assert.That(request.IsValid,Is.True);Assert.That(local,Is.EqualTo(again));Assert.That(request,Is.EqualTo(same));
                Assert.That(local.magnitude,Is.LessThan(1));Assert.That(request.End.magnitude,Is.LessThan(1));
                Assert.That(request.Lifetime,Is.InRange(.05f,1.5f));
                if(reduced){Assert.That(request.Bend.sqrMagnitude,Is.LessThanOrEqualTo(.01f));Assert.That(request.Radius,Is.LessThanOrEqualTo(.025f));}
            }
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test] public void FourCulturesRetainDistinctVisibleIdentity()
        {
            var colors=new Color[4];for(int i=0;i<4;i++){FxResearchAutomationMotion.SampleTransfer((CultureKind)i,0,3,false,false,out _,out var request);colors[i]=request.Color;}
            for(int a=0;a<4;a++)for(int b=a+1;b<4;b++)Assert.That(colors[a],Is.Not.EqualTo(colors[b]),"Distinct original culture colors must survive automatic transfer");
        }
        [Test] public void ResearchConfirmationIsBoundedDeterministicAndReducedMotionRemainsVisible()
        {
            var random=UnityEngine.Random.state;
            for(int i=0;i<20;i++)foreach(bool reduced in new[]{false,true}){
                FxResearchAutomationMotion.SampleResearch(i,93,reduced,out var local,out var request);
                FxResearchAutomationMotion.SampleResearch(i,93,reduced,out var again,out var same);
                Assert.That(request.IsValid,Is.True);Assert.That(request,Is.EqualTo(same));Assert.That(local,Is.EqualTo(again));
                Assert.That(local.magnitude,Is.LessThan(1));Assert.That(request.End.magnitude,Is.LessThan(1));Assert.That(request.Size,Is.GreaterThan(0));
                if(reduced)Assert.That(request.Spin,Is.Zero);
            }
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test] public void SeparateOwnersFollowCurrentRootsAndCannotSurviveCancellationOrReuse()
        {
            var parent=new GameObject("FX10 isolated effect roots");var source=new GameObject("Current culture outlet");var destination=new GameObject("Current incubator inlet");source.transform.SetParent(parent.transform);destination.transform.SetParent(parent.transform);
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};var cues=new FxCueRuntime();var pool=new FxEffectPool(parent.transform,mesh);
            try {
                for(int turn=0;turn<4;turn++) {
                    source.transform.SetPositionAndRotation(new Vector3(3,0,6),Quaternion.Euler(0,turn*90,0));destination.transform.SetPositionAndRotation(new Vector3(-3,0,8),Quaternion.Euler(0,(turn+1)*90,0));
                    var collector=cues.Register("collector","turn"+turn);var doser=cues.Register("doser","turn"+turn);var research=cues.Register("research","turn"+turn);
                    FxResearchAutomationMotion.SampleTransfer(CultureKind.Mist,0,(uint)turn,false,false,out var start,out var collect);
                    FxResearchAutomationMotion.SampleTransfer(CultureKind.Spark,0,(uint)turn,true,false,out var doseStart,out var dose);
                    FxResearchAutomationMotion.SampleResearch(0,(uint)turn,false,out var researchStart,out var confirm);
                    Assert.That(pool.EmitParticle(cues,collector,source.transform,start,collect,false),Is.True);Assert.That(pool.EmitParticle(cues,doser,destination.transform,doseStart,dose,false),Is.True);Assert.That(pool.EmitParticle(cues,research,source.transform,researchStart,confirm,false),Is.True);
                    Assert.That(pool.TryGetParticleForValidation(collector,0,out var actualStart,out var actualEnd,out _,out _,out _),Is.True);
                    Assert.That(Vector3.Distance(actualStart,source.transform.TransformPoint(start)),Is.LessThan(.001f));Assert.That(Vector3.Distance(actualEnd,source.transform.TransformPoint(start+collect.End)),Is.LessThan(.001f));
                    source.transform.position+=new Vector3(2,0,-1);pool.Tick(cues,.001f);
                    Assert.That(pool.TryGetParticleForValidation(collector,0,out actualStart,out _,out _,out _,out _),Is.True);Assert.That(Vector3.Distance(actualStart,source.transform.TransformPoint(start)),Is.LessThan(.001f),"Existing owner root movement cannot leave a stream behind");
                    pool.Cancel(collector);cues.Unregister(collector);Assert.That(pool.ParticleCountForValidation(collector),Is.Zero);Assert.That(pool.ParticleCountForValidation(doser),Is.EqualTo(1));Assert.That(pool.ParticleCountForValidation(research),Is.EqualTo(1));
                    Assert.That(pool.EmitParticle(cues,collector,source.transform,start,collect,false),Is.False,"Retired owner cannot emit after a late callback");
                    cues.Unregister(doser);pool.Tick(cues,.001f);Assert.That(pool.ParticleCountForValidation(doser),Is.Zero);
                    pool.Cancel(research);cues.Unregister(research);Assert.That(pool.ActiveCount,Is.Zero);
                }
                Assert.That(pool.OwnedCount,Is.EqualTo(96));Assert.That(pool.OwnedMeshCount,Is.EqualTo(5));Assert.That(pool.OwnedMaterialCount,Is.EqualTo(1));Assert.That(pool.Root.GetComponentsInChildren<Collider>(true).Length,Is.Zero);
            }
            finally{pool.Dispose();cues.Dispose();Object.DestroyImmediate(parent);Object.DestroyImmediate(mesh);}
        }
        [Test] public void WarmedPresentationRecipesDoNotAllocateManagedMemory()
        {
            float sum=0;for(uint i=0;i<60;i++)Sample(i,ref sum);long before=GC.GetAllocatedBytesForCurrentThread();for(uint i=0;i<1000;i++)Sample(i,ref sum);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(bytes,Is.Zero);Assert.That(float.IsNaN(sum)||float.IsInfinity(sum),Is.False);
        }
        static void Sample(uint sequence,ref float sum)
        {
            for(int i=0;i<4;i++){FxResearchAutomationMotion.SampleTransfer((CultureKind)i,i,sequence,false,false,out var local,out var collect);FxResearchAutomationMotion.SampleTransfer((CultureKind)i,i,sequence,true,true,out _,out var dose);sum+=local.y+collect.Size+dose.Size;}
            FxResearchAutomationMotion.SampleResearch(0,sequence,false,out _,out var research);sum+=research.Size;
        }
    }
}
