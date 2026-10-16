using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxContainerMotionTests
    {
        // Conservative admitted water regions include the narrow conditioner and incubator
        // response prism. Runtime fixtures separately measure the actual authored meshes.
        static readonly Bounds[] Waters = {
            new Bounds(new Vector3(0,1.52f,0),new Vector3(1.80f,.84f,1.10f)),
            new Bounds(new Vector3(0,1.16f,0),new Vector3(1.31f,.62f,.81f)),
            new Bounds(new Vector3(0,1.16f,0),new Vector3(1.06f,.62f,.81f)),
            new Bounds(Vector3.zero,new Vector3(1.25f,.62f,.77f)),
            new Bounds(Vector3.zero,new Vector3(.77f,.64f,.77f)),
            new Bounds(new Vector3(0,1.1f,0),new Vector3(.30f,.30f,.30f))
        };

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void ScaledResponsesContainActualParticleVerticesAtEveryLegalRotation(int container)
        {
            var root=new GameObject("FX13 isolated particle owner");
            var frame=new GameObject("FX13 rotated container");frame.transform.SetParent(root.transform);
            var cameraObject=new GameObject("FX13 actual view");var camera=cameraObject.AddComponent<Camera>();
            camera.transform.rotation=Quaternion.Euler(19,37,0);
            var borrowed=new Mesh();borrowed.vertices=new[]{Vector3.zero,Vector3.up,Vector3.right};borrowed.triangles=new[]{0,1,2};
            var cues=new FxCueRuntime();var token=cues.Register("container","specimen");
            var pool=new FxEffectPool(root.transform,borrowed);pool.SetViewCamera(camera);
            try {
                Bounds water=Waters[container];
                foreach(int turn in new[]{0,1,2,3}) {
                    frame.transform.SetPositionAndRotation(new Vector3(3,.4f,-7),Quaternion.Euler(0,turn*90,0));
                    foreach(bool reduced in new[]{false,true})for(int recipe=0;recipe<6;recipe++) {
                        var fish=new Bounds(recipe%2==0?water.min:water.max,water.size*.3f);
                        FxAquariumMotion.ScaledPlacement(recipe,fish,water,reduced,out float delay,out var origin,out var request);
                        Assert.That(request.IsValid,Is.True);Assert.That(delay,Is.GreaterThanOrEqualTo(0));
                        pool.CancelAll();Assert.That(pool.EmitParticle(cues,token,frame.transform,origin,request),Is.True);
                        var visual=pool.ParticleVisualForValidation(token,0);var vertices=visual.GetComponent<MeshFilter>().sharedMesh.vertices;
                        for(int step=0;step<24;step++) {
                            pool.Tick(cues,request.Lifetime*.999f/24);
                            foreach(var vertex in vertices) {
                                var point=frame.transform.InverseTransformPoint(visual.TransformPoint(vertex));
                                Assert.That(water.Contains(point),Is.True,"Actual particle exceeds water: container="+container+" turn="+turn+" recipe="+recipe+" vertex="+point);
                            }
                        }
                        pool.Tick(cues,request.Lifetime);Assert.That(pool.ParticleCountForValidation(token),Is.Zero);
                    }
                }
                Assert.That(pool.OwnedCount,Is.EqualTo(96));Assert.That(pool.OwnedMeshCount,Is.EqualTo(5));Assert.That(pool.OwnedMaterialCount,Is.EqualTo(1));
            }
            finally {pool.Dispose();cues.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(borrowed);}
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void ScaledBubblesRemainContainedDuringWrapAndEmptyOccupiedChanges(int container)
        {
            Bounds water=Waters[container];
            foreach(bool occupied in new[]{false,true})for(int index=0;index<4;index++)for(int sample=0;sample<1200;sample++) {
                float age=sample*.031f;
                FxAquariumMotion.ScaledBubble(index,age,.71f,water,occupied,out var point,out float size);
                FxAquariumMotion.ScaledBubble(index,age+.001f,.71f,water,occupied,out var next,out float nextSize);
                Assert.That(water.Contains(point+Vector3.one*size*.5f)&&water.Contains(point-Vector3.one*size*.5f),Is.True);
                if(Vector3.Distance(point,next)>water.size.y*.2f)Assert.That(Mathf.Max(size,nextSize),Is.LessThan(.003f),"Wrap stays visually hidden");
            }
        }

        [Test] public void ScaledWakeRejectsAnImpossibleOutletAndReducedModeForEveryContainer()
        {
            foreach(var water in Waters) {
                Assert.That(FxAquariumMotion.ScaledWakeAnchor(water,Vector3.right,water,out _),Is.False,"Never clamp an outlet inside a fish filling the water");
                Assert.That(FxAquariumMotion.ScaledWakeAnchor(new Bounds(water.center,water.size*.1f),Vector3.up,water,out _),Is.False);
                Assert.That(FxAquariumMotion.ScaledTrail(1,water.center,Vector3.right,water,false,true,out _,out _,out _),Is.False);
                Assert.That(FxAquariumMotion.ScaledTrail(1,water.center,Vector3.right,water,false,false,out float normal,out _,out var request),Is.True);
                Assert.That(FxAquariumMotion.ScaledTrail(1,water.center,Vector3.right,water,true,false,out float low,out _,out _),Is.True);
                Assert.That(low,Is.GreaterThan(normal));Assert.That(request.IsValid,Is.True);
            }
        }

        [Test] public void WarmedContainerSamplingHasNoManagedAllocationOrGlobalRandomMutation()
        {
            var random=UnityEngine.Random.state;
            foreach(var water in Waters) {
                FxAquariumMotion.ScaledBubble(1,1,1,water,true,out _,out _);
                FxAquariumMotion.ScaledPlacement(0,new Bounds(water.center,water.size*.2f),water,false,out _,out _,out _);
                FxAquariumMotion.ScaledTrail(1,water.center,Vector3.right,water,false,false,out _,out _,out _);
            }
            long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;
            for(int i=0;i<10000;i++) {
                var water=Waters[i%Waters.Length];
                FxAquariumMotion.ScaledBubble(i%4,i*.013f,.7f,water,true,out var point,out float size);
                FxAquariumMotion.ScaledPlacement(i%6,new Bounds(water.center,water.size*.2f),water,false,out _,out var ring,out _);
                FxAquariumMotion.ScaledTrail((uint)i,point,Vector3.right,water,i%2==0,false,out _,out _,out _);sum+=size+ring.x;
            }
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(bytes,Is.Zero);Assert.That(float.IsNaN(sum),Is.False);Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
        }
    }
}
