using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxAquariumMotionTests
    {
        static readonly Bounds Water=new Bounds(new Vector3(0,1.5225f,0),new Vector3(1.85f,.895f,1.15f));
        [Test] public void HabitatRemainsBoundedContinuousAndReducedIsStill()
        {
            foreach(bool occupied in new[]{false,true})for(int i=0;i<500;i++)
            {
                float time=i*.073f;var p=FxAquariumMotion.Habitat(time,.37f,occupied,false,-1);var next=FxAquariumMotion.Habitat(time+.001f,.37f,occupied,false,-1);
                Assert.That(p.WaterScale,Is.InRange(.98f,1f));Assert.That(p.WaterTint,Is.InRange(0f,.3f));
                Assert.That(Mathf.Abs(p.PlantLeft),Is.LessThan(7));Assert.That(Mathf.Abs(p.PlantRight),Is.LessThan(7));
                Assert.That(Mathf.Abs(next.PlantLeft-p.PlantLeft),Is.LessThan(.01f));Assert.That(Mathf.Abs(next.PlantRight-p.PlantRight),Is.LessThan(.01f));
                var reduced=FxAquariumMotion.Habitat(time,.37f,occupied,true,.26f);
                Assert.That(reduced.WaterScale,Is.EqualTo(1));Assert.That(reduced.WaterTint+reduced.PlantLeft+reduced.PlantRight,Is.Zero);
            }
        }
        [Test] public void PlacementEnvelopeStartsAndEndsAtOrdinaryHabitat()
        {
            var rest=FxAquariumMotion.Habitat(2,.1f,true,false,-1);
            Assert.That(FxAquariumMotion.Habitat(2,.1f,true,false,0),Is.EqualTo(rest));
            Assert.That(FxAquariumMotion.Habitat(2,.1f,true,false,.52f),Is.EqualTo(rest));
            Assert.That(FxAquariumMotion.Habitat(2,.1f,true,false,10),Is.EqualTo(rest));
            var active=FxAquariumMotion.Habitat(2,.1f,true,false,.26f);Assert.That(active.WaterScale,Is.LessThan(rest.WaterScale));
            Assert.That(FxAquariumMotion.PlacementCount(true,false),Is.LessThan(FxAquariumMotion.PlacementCount(false,false)));
            Assert.That(FxAquariumMotion.PlacementCount(false,true),Is.EqualTo(1));
            for(int i=0;i<6;i++){FxAquariumMotion.Placement(i,Water.center,Water,false,out float delay,out _,out var p);Assert.That(p.IsValid,Is.True);Assert.That(delay,Is.GreaterThanOrEqualTo(0));Assert.That(delay+p.Lifetime,Is.LessThanOrEqualTo(.52f));}
        }
        [Test] public void HabitatBubblesHaveContainedExtentAndHideWrapDiscontinuity()
        {
            foreach(bool occupied in new[]{false,true})for(int index=0;index<4;index++)for(int i=0;i<1500;i++)
            {
                float time=i*.02f;FxAquariumMotion.Bubble(index,time,.47f,Water,occupied,out var point,out float size);
                FxAquariumMotion.Bubble(index,time+.001f,.47f,Water,occupied,out var next,out float nextSize);
                Assert.That(Water.Contains(point+Vector3.one*size*.5f) && Water.Contains(point-Vector3.one*size*.5f),Is.True);
                if(Vector3.Distance(point,next)>.1f)Assert.That(Mathf.Max(size,nextSize),Is.LessThan(.003f),"Wrap occurs only at almost invisible radius");
            }
        }
        [Test] public void WakeRequiresMotionAndLowReducesCadenceWithoutGlobalRandom()
        {
            var random=Random.state;var intervals=new HashSet<float>();
            for(uint index=0;index<32;index++)
            {
                Assert.That(FxAquariumMotion.Trail(index,Water.center,Vector3.zero,Water,false,false,out _,out _,out _),Is.False);
                Assert.That(FxAquariumMotion.Trail(index,Water.center,Vector3.right,Water,false,true,out _,out _,out _),Is.False);
                Assert.That(FxAquariumMotion.Trail(index,Water.center,Vector3.right,Water,false,false,out float standard,out var local,out var request),Is.True);
                Assert.That(FxAquariumMotion.Trail(index,Water.center,Vector3.right,Water,true,false,out float low,out var same,out var duplicate),Is.True);
                Assert.That(low,Is.GreaterThan(standard));Assert.That(local.x,Is.LessThan(Water.center.x));Assert.That(same,Is.EqualTo(local));Assert.That(duplicate,Is.EqualTo(request));Assert.That(request.IsValid,Is.True);intervals.Add(standard);
            }
            Assert.That(intervals.Count,Is.GreaterThan(24));Assert.That(Random.state,Is.EqualTo(random));
        }
        [Test] public void ActualParticleMeshesStayInsideWaterIncludingSurfacePlaneAndCameraDefaults()
        {
            var root=new GameObject("Aquarium mesh boundary test");var source=new GameObject("Rotated aquarium");source.transform.SetParent(root.transform);
            var cameraObject=new GameObject("Aquarium test camera");var camera=cameraObject.AddComponent<Camera>();camera.transform.rotation=Quaternion.Euler(17,33,9);
            var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.up,Vector3.right};mesh.triangles=new[]{0,1,2};
            var cues=new FxCueRuntime();var owner=cues.Register("tank-a","real-specimen");var pool=new FxEffectPool(root.transform,mesh);pool.SetViewCamera(camera);
            try
            {
                source.transform.SetPositionAndRotation(new Vector3(2,3,-4),Quaternion.Euler(7,90,3));source.transform.localScale=Vector3.one*1.3f;
                for(int recipe=0;recipe<22;recipe++)
                {
                    FxParticleRequest request;Vector3 local;
                    if(recipe<6)FxAquariumMotion.Placement(recipe,Water.max,Water,false,out _,out local,out request);
                    else Assert.That(FxAquariumMotion.Trail((uint)recipe,recipe%2==0?Water.min:Water.max,new Vector3(1,.2f,-.3f),Water,false,false,out _,out local,out request),Is.True);
                    pool.CancelAll();Assert.That(pool.EmitParticle(cues,owner,source.transform,local,request),Is.True);
                    var visual=pool.ParticleVisualForValidation(owner,0);var vertices=visual.GetComponent<MeshFilter>().sharedMesh.vertices;
                    for(int frame=0;frame<25;frame++)
                    {
                        pool.Tick(cues,request.Lifetime*.999f/25);
                        foreach(var vertex in vertices)
                        {var actual=source.transform.InverseTransformPoint(visual.TransformPoint(vertex));Assert.That(Water.Contains(actual),Is.True,"Rendered vertex includes curve, orientation and world-size scale: recipe="+recipe+" vertex="+actual);}
                        float normal=Mathf.Abs(Vector3.Dot(visual.forward,request.SurfaceAligned?source.transform.up:camera.transform.forward));
                        Assert.That(normal,Is.GreaterThan(.9999f),request.SurfaceAligned?"Surface ring follows tilted tank water plane":"Default vocabulary remains camera-facing");
                    }
                    pool.Tick(cues,request.Lifetime);Assert.That(pool.ParticleCountForValidation(owner),Is.Zero);
                }
                Assert.That(FxParticleRequest.Create(FxParticleShape.Bubble,Color.white).SurfaceAligned,Is.False);
            }
            finally {pool.Dispose();cues.Dispose();Object.DestroyImmediate(root);Object.DestroyImmediate(source);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(mesh);}
        }
    }
}
