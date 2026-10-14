using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EvolutionShop.Tests
{
    public sealed class FxAdvancedLabMotionTests
    {
        [Test] public void AdvancedCultureSelectionDoesNotSilentlyRemapOtherSources()
        {
            Assert.That(FxAdvancedLabMotion.IsAdvanced(CultureKind.Spark), Is.True);
            Assert.That(FxAdvancedLabMotion.IsAdvanced(CultureKind.Mist), Is.True);
            Assert.That(FxAdvancedLabMotion.IsAdvanced(CultureKind.Bloom), Is.False);
            Assert.That(FxAdvancedLabMotion.IsAdvanced(CultureKind.Flow), Is.False);
            Assert.That(FxAdvancedLabMotion.IsAdvanced((CultureKind)99), Is.False);
            Assert.That(FxAdvancedLabMotion.SourcePoint(CultureKind.Spark), Is.Not.EqualTo(FxAdvancedLabMotion.SourcePoint(CultureKind.Mist)));
            Assert.That(FxAdvancedLabMotion.CultureColor(CultureKind.Spark), Is.Not.EqualTo(FxAdvancedLabMotion.CultureColor(CultureKind.Mist)));
        }
        [Test] public void ProgressClampsToRealRemainingTimeAndRejectsInvalidClocks()
        {
            Assert.That(FxAdvancedLabMotion.ConditionerProgress(12,12), Is.Zero);
            Assert.That(FxAdvancedLabMotion.ConditionerProgress(6,12), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(FxAdvancedLabMotion.ConditionerProgress(0,12), Is.EqualTo(1));
            foreach (double remaining in new[] { -1d, 99d, double.NaN, double.PositiveInfinity })
            foreach (double duration in new[] { 0d, -1d, 12d, double.NaN, double.PositiveInfinity }) {
                float progress=FxAdvancedLabMotion.ConditionerProgress(remaining,duration);
                Assert.That(float.IsNaN(progress)||float.IsInfinity(progress), Is.False);
                Assert.That(progress, Is.InRange(0,1));
            }
        }
        [TestCase(CultureKind.Spark)] [TestCase(CultureKind.Mist)]
        public void RecipesAreDeterministicFiniteAndDoNotUseGameplayRandom(CultureKind culture)
        {
            var random=UnityEngine.Random.state;
            for(uint i=0;i<48;i++) foreach(bool reduced in new[]{false,true}) {
                FxAdvancedLabMotion.SampleSource(culture,i,false,reduced,out var interval,out var origin,out var request);
                FxAdvancedLabMotion.SampleSource(culture,i,false,reduced,out var again,out var sameOrigin,out var same);
                Assert.That(request.IsValid,Is.True); Assert.That(interval,Is.GreaterThan(0));
                Assert.That(again,Is.EqualTo(interval)); Assert.That(sameOrigin,Is.EqualTo(origin)); Assert.That(same,Is.EqualTo(request));
                FxAdvancedLabMotion.SampleIntake(culture,i,false,reduced,out var intakeInterval,out var intakeOrigin,out var intake);
                Assert.That(intake.IsValid,Is.True); Assert.That(intakeInterval,Is.GreaterThan(0));
                Assert.That(Vector3.Distance(intakeOrigin,FxAdvancedLabMotion.SourcePoint(culture)),Is.LessThan(.15f));
                FxAdvancedLabMotion.SampleSource(culture,i,true,reduced,out var lowInterval,out _,out _);
                Assert.That(lowInterval,Is.GreaterThanOrEqualTo(interval));
                for(int cell=0;cell<6;cell++) {
                    var p=FxAdvancedLabMotion.SourceCell(culture,cell,i*.13f,reduced);
                    Assert.That(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude),Is.False);
                }
            }
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
        [Test] public void ConditionerParticlesRemainInsideTheirActualWaterChamber()
        {
            for(int slot=0;slot<2;slot++) foreach(float progress in new[]{0f,.5f,1f})
            foreach(bool reduced in new[]{false,true}) for(uint sequence=0;sequence<32;sequence++) {
                FxAdvancedLabMotion.SampleConditioner(slot,progress,sequence,false,reduced,out var interval,out var origin,out var request);
                Assert.That(request.IsValid,Is.True); Assert.That(interval,Is.GreaterThan(0));
                var center=new Vector3(slot==0?-.49f:.49f,1.16f,0);
                for(int step=0;step<=32;step++) {
                    float t=step/32f;
                    Vector3 p=origin+request.End*t+request.Bend*(4*t*(1-t))-center;
                    float radius=Mathf.Max(request.Size,request.EndSize)*.8f;
                    Assert.That(Mathf.Abs(p.x)+radius,Is.LessThanOrEqualTo(.41f));
                    Assert.That(Mathf.Abs(p.y)+radius,Is.LessThanOrEqualTo(.36f));
                    Assert.That(Mathf.Abs(p.z)+radius,Is.LessThanOrEqualTo(.41f));
                }
            }
        }
        [Test] public void ReducedCellsAndIdleMechanismDoNotKeepTraveling()
        {
            foreach(var culture in new[]{CultureKind.Spark,CultureKind.Mist}) for(int i=0;i<6;i++)
                Assert.That(FxAdvancedLabMotion.SourceCell(culture,i,0,true),Is.EqualTo(FxAdvancedLabMotion.SourceCell(culture,i,100,true)));
            Assert.That(FxAdvancedLabMotion.PaddleAngle(0,.3f,false,false),Is.EqualTo(FxAdvancedLabMotion.PaddleAngle(100,.3f,false,false)));
            Assert.That(FxAdvancedLabMotion.PaddleAngle(0,.3f,true,true),Is.EqualTo(FxAdvancedLabMotion.PaddleAngle(100,.3f,true,true)));
        }
        [Test] public void RealPoolRetiresOnlySelectedOwnerAcrossMovedQuarterTurns()
        {
            var parent=new GameObject("Advanced lab ownership contract");var source=new GameObject("Relocated vat");source.transform.SetParent(parent.transform);
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};
            var cues=new FxCueRuntime();var pool=new FxEffectPool(parent.transform,mesh);
            try {
                for(int turn=0;turn<4;turn++) {
                    source.transform.SetPositionAndRotation(new Vector3(3,0,7),Quaternion.Euler(0,turn*90,0));
                    var vat=cues.Register("conditioner",turn.ToString());var mist=cues.Register("mist",turn.ToString());
                    FxAdvancedLabMotion.SampleConditioner(1,.5f,5,false,false,out _,out var local,out var request);
                    Assert.That(pool.EmitParticle(cues,vat,source.transform,local,request,false),Is.True);
                    FxAdvancedLabMotion.SampleSource(CultureKind.Mist,9,false,false,out _,out var mistLocal,out var mistRequest);
                    Assert.That(pool.EmitParticle(cues,mist,source.transform,mistLocal,mistRequest,false),Is.True);
                    var visual=pool.ParticleVisualForValidation(vat,0);var vertices=visual.GetComponent<MeshFilter>().sharedMesh.vertices;
                    for(int step=0;step<12;step++) {
                        pool.Tick(cues,request.Lifetime/16);
                        foreach(var vertex in vertices) {
                            Vector3 p=source.transform.InverseTransformPoint(visual.TransformPoint(vertex))-new Vector3(.49f,1.16f,0);
                            Assert.That(Mathf.Abs(p.x),Is.LessThanOrEqualTo(.411f)); Assert.That(Mathf.Abs(p.y),Is.LessThanOrEqualTo(.361f)); Assert.That(Mathf.Abs(p.z),Is.LessThanOrEqualTo(.411f));
                        }
                    }
                    pool.Cancel(mist);cues.Unregister(mist);
                    Assert.That(pool.ParticleCountForValidation(mist),Is.Zero);
                    Assert.That(pool.ParticleCountForValidation(vat),Is.EqualTo(1),"Sibling owner cancellation must not clear working conditioner particles");
                    cues.Unregister(vat);pool.Tick(cues,.001f);Assert.That(pool.ParticleCountForValidation(vat),Is.Zero);
                    Assert.That(pool.EmitParticle(cues,vat,source.transform,local,request,false),Is.False);
                }
                Assert.That(pool.OwnedCount,Is.EqualTo(96));Assert.That(pool.OwnedMeshCount,Is.EqualTo(5));Assert.That(pool.OwnedMaterialCount,Is.EqualTo(1));
            }
            finally {pool.Dispose();cues.Dispose();Object.DestroyImmediate(parent);Object.DestroyImmediate(mesh);}
        }
        [Test] public void WarmedPureRecipesAllocateNoManagedBytes()
        {
            float sum=0;for(uint i=0;i<50;i++) Sample(i,ref sum);
            long before=GC.GetAllocatedBytesForCurrentThread();for(uint i=0;i<1000;i++)Sample(i,ref sum);
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(bytes,Is.Zero);Assert.That(float.IsNaN(sum),Is.False);
        }
        [Test] public void ActualSparkMeshesAndEndpointsStayInsideExistingCylinderAtAllQuarterTurns()
        {
            // Existing Upgrade reservoir sample: Unity cylinder at y1.5, scale(.55,.41,.55).
            var parent=new GameObject("Advanced source geometry");var source=new GameObject("Research bench");source.transform.SetParent(parent.transform);
            var glass=GameObject.CreatePrimitive(PrimitiveType.Cylinder);glass.transform.SetParent(source.transform,false);glass.transform.localPosition=new Vector3(0,1.5f,0);glass.transform.localScale=new Vector3(.55f,.41f,.55f);
            var bounds=glass.GetComponent<MeshFilter>().sharedMesh.bounds;
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};
            var cues=new FxCueRuntime();var pool=new FxEffectPool(parent.transform,mesh);
            try {
                for(int turn=0;turn<4;turn++) foreach(bool reduced in new[]{false,true}) for(uint i=0;i<12;i++) {
                    source.transform.SetPositionAndRotation(new Vector3(4,0,9),Quaternion.Euler(0,90*turn,0));
                    var owner=cues.Register("spark",turn+"/"+reduced+"/"+i);
                    FxAdvancedLabMotion.SampleSource(CultureKind.Spark,i,false,reduced,out _,out var origin,out var request);
                    Assert.That(pool.EmitParticle(cues,owner,source.transform,origin,request,false),Is.True);
                    Assert.That(pool.TryGetParticleForValidation(owner,0,out var start,out var end,out _,out _,out _),Is.True);
                    Assert.That(Vector3.Distance(source.transform.InverseTransformPoint(start),origin),Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(source.transform.InverseTransformPoint(end),origin+request.End),Is.LessThan(.0001f),"Pool endpoint is origin plus displacement");
                    var visual=pool.ParticleVisualForValidation(owner,0);var vertices=visual.GetComponent<MeshFilter>().sharedMesh.vertices;
                    for(int step=0;step<15;step++) {
                        pool.Tick(cues,request.Lifetime/16);
                        foreach(var vertex in vertices) {
                            Vector3 p=glass.transform.InverseTransformPoint(visual.TransformPoint(vertex))-bounds.center;
                            Assert.That(p.x*p.x/(bounds.extents.x*bounds.extents.x)+p.z*p.z/(bounds.extents.z*bounds.extents.z),Is.LessThanOrEqualTo(1.005f));
                            Assert.That(Mathf.Abs(p.y),Is.LessThanOrEqualTo(bounds.extents.y+.003f));
                        }
                    }
                    cues.Unregister(owner);pool.Tick(cues,.001f);
                }
            }
            finally{pool.Dispose();cues.Dispose();Object.DestroyImmediate(parent);Object.DestroyImmediate(mesh);}
        }
        static void Sample(uint i,ref float sum)
        {
            FxAdvancedLabMotion.SampleSource(CultureKind.Spark,i,false,false,out var interval,out var p,out var r);
            FxAdvancedLabMotion.SampleIntake(CultureKind.Mist,i,false,true,out _,out _,out var intake);
            FxAdvancedLabMotion.SampleConditioner(0,.5f,i,false,false,out _,out _,out var vat);
            sum+=interval+p.y+r.Size+intake.Size+vat.Size+FxAdvancedLabMotion.PaddleAngle(i,.5f,true,false);
        }
    }
}
