using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace EvolutionShop.Tests
{
    public sealed class FxEffectPoolTests
    {
        GameObject parent, source;
        Mesh mesh;
        Material material;
        FxCueRuntime cues;
        FxEffectPool pool;
        FxOwnerToken token;
        [SetUp] public void Setup()
        {
            parent = new GameObject("Isolated FX pool test"); source = new GameObject("Isolated source"); source.transform.SetParent(parent.transform);
            mesh = new Mesh { name = "Task-owned isolated triangle" }; mesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }; mesh.triangles = new[] { 0, 1, 2 };
            material = ShopWorld.MakeMaterial("Isolated borrowed palette", Color.white);
            cues = new FxCueRuntime(); token = cues.Register("source", "id-a"); pool = new FxEffectPool(parent.transform, mesh);
        }
        [TearDown] public void Cleanup()
        { pool.Dispose(); cues.Dispose(); Object.DestroyImmediate(parent); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        bool Emit(bool action = true) => pool.Emit(cues, token, source.transform, Vector3.zero, material, .28f, .04f, action);
        [Test] public void WarmedObjectsAreReusedAndHaveNoCollider()
        {
            var root = pool.Root; Assert.That(root.childCount, Is.EqualTo(96)); Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            int first = root.GetChild(0).GetInstanceID(); Assert.That(Emit(), Is.True); pool.Tick(cues, .3f);
            Assert.That(pool.ActiveCount, Is.Zero); Assert.That(Emit(), Is.True);
            Assert.That(root.GetChild(0).GetInstanceID(), Is.EqualTo(first)); Assert.That(root.childCount, Is.EqualTo(96));
        }
        [Test] public void AmbientCannotConsumeReservedActionSlots()
        {
            for (int i = 0; i < 32; i++) Assert.That(Emit(false), Is.True);
            Assert.That(Emit(false), Is.False);
            for (int i = 0; i < 64; i++) Assert.That(Emit(), Is.True);
            Assert.That(Emit(), Is.False); Assert.That(pool.ActiveCount, Is.EqualTo(96));
            pool.CancelAll(); Assert.That(pool.ActiveCount, Is.Zero); Assert.That(Emit(), Is.True);
        }
        [Test] public void LowDetailKeepsReservedFeedbackAndBoundsAmbient()
        {
            pool.SetLowDetail(true);
            for (int i = 0; i < 8; i++) Assert.That(Emit(false), Is.True);
            Assert.That(Emit(false), Is.False);
            for (int i = 0; i < 24; i++) Assert.That(Emit(), Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(32));
        }
        [Test] public void OwnerRetirementAndEpochResetReturnStaleMotes()
        {
            Emit(); cues.Register("source", "id-b"); pool.Tick(cues, .01f); Assert.That(pool.ActiveCount, Is.Zero);
            token = cues.Find("source"); Emit(); cues.Reset(); pool.Tick(cues, .01f); Assert.That(pool.ActiveCount, Is.Zero);
        }
        [Test] public void DisabledSourceCannotLeaveAnOrphan()
        { Emit(); source.SetActive(false); pool.Tick(cues, .01f); Assert.That(pool.ActiveCount, Is.Zero); Assert.That(Emit(), Is.False); }
        [Test] public void MissingAttachmentOrMaterialDoesNotThrowOrSpawn()
        {
            Assert.That(pool.Emit(cues, token, null, Vector3.zero, material, .28f, .04f), Is.False);
            Assert.That(pool.Emit(cues, token, source.transform, Vector3.zero, null, .28f, .04f), Is.False);
            Assert.That(pool.ActiveCount, Is.Zero);
        }
        [Test] public void DisposeDoesNotDestroyBorrowedAssetsOrParent()
        {
            Emit(); pool.Dispose(); pool.Dispose();
            Assert.That(parent && source && mesh && material, Is.True); Assert.That(pool.OwnedCount, Is.Zero); Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(Emit(), Is.False);
        }
        [Test] public void TravelUsesNewSlotAndMovingMouthNotAnotherFreshMote()
        {
            var end = new GameObject("Moving mouth"); end.transform.SetParent(parent.transform); end.transform.position=Vector3.right*2;
            Assert.That(Emit(false),Is.True);
            Assert.That(pool.EmitTravel(cues,token,source.transform,Vector3.zero,end.transform,Vector3.zero,material,.04f),Is.True);
            pool.Tick(cues,.26f);
            Assert.That(pool.Root.GetChild(FxEffectPool.ActionReserved).position.x,Is.EqualTo(0).Within(.001));
            Assert.That(pool.Root.GetChild(FxEffectPool.ActionReserved + 1).position.x,Is.EqualTo(.5f).Within(.001));
            Assert.That(pool.Root.GetChild(FxEffectPool.ActionReserved + 1).position.y,Is.EqualTo(.12f).Within(.001));
            end.transform.position=Vector3.right*4;pool.Tick(cues,.01f);
            Assert.That(pool.Root.GetChild(FxEffectPool.ActionReserved + 1).position.x,Is.GreaterThan(1));
            end.SetActive(false);pool.Tick(cues,.02f);Assert.That(pool.ActiveCount,Is.EqualTo(0));
        }
        [Test] public void TravelExhaustionRetainsReservedActionsAndReusesFixedObjects()
        {
            pool.SetLowDetail(true);var end=new GameObject("Mouth");end.transform.SetParent(parent.transform);
            for(int i=0;i<8;i++)Assert.That(pool.EmitTravel(cues,token,source.transform,Vector3.zero,end.transform,Vector3.zero,material,.04f),Is.True);
            Assert.That(pool.EmitTravel(cues,token,source.transform,Vector3.zero,end.transform,Vector3.zero,material,.04f),Is.False);
            Assert.That(Emit(),Is.True);pool.CancelAll();Assert.That(pool.ActiveCount,Is.Zero);
            Assert.That(pool.Root.childCount,Is.EqualTo(96));Assert.That(pool.EmitTravel(cues,token,source.transform,Vector3.zero,end.transform,Vector3.zero,material,.04f),Is.True);
            pool.Tick(cues,.53f);Assert.That(pool.ActiveCount,Is.Zero);
        }
        [Test] public void MissingTravelEndpointCannotAllocateOrEmit()
        {
            Assert.That(pool.EmitTravel(cues,token,source.transform,Vector3.zero,null,Vector3.zero,material,.04f),Is.False);
            Assert.That(pool.ActiveCount,Is.Zero);Assert.That(pool.SpawnedCount,Is.Zero);
        }
        [Test] public void CartoonVocabularyOwnsFiveMeshesOneMaterialAndReleasesOnlyItsAssets()
        {
            Assert.That(pool.VocabularyReady, Is.True); Assert.That(pool.OwnedMeshCount, Is.EqualTo(5)); Assert.That(pool.OwnedMaterialCount, Is.EqualTo(1));
            var assets = new Mesh[5]; Material owned = null;
            for (int i = 0; i < 5; i++)
            {
                var p = FxParticleRequest.Create((FxParticleShape)i, Color.cyan);
                Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p), Is.True);
                var filter = pool.Root.GetChild(i).GetComponent<MeshFilter>(); assets[i] = filter.sharedMesh;
                Assert.That(assets[i].triangles.Length / 3, Is.LessThanOrEqualTo(192));
                foreach (var v in assets[i].vertices) Assert.That(v.magnitude, Is.LessThanOrEqualTo(.501f));
                owned = filter.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(owned.shader.name, Is.EqualTo("EvolutionShop/Cartoon Particle"));
            }
            Assert.That(assets[0] == assets[4], Is.False); pool.Dispose();
            foreach (var asset in assets) Assert.That(asset == null, Is.True);
            Assert.That(owned == null, Is.True); Assert.That(mesh && material && source && parent, Is.True);
        }
        [Test] public void CurveAndSpiralFollowMovingEndpointAndCancelDestroyedEndpoint()
        {
            var end = new GameObject("Moving rich endpoint"); end.transform.SetParent(parent.transform); end.transform.position = Vector3.right;
            var p = FxParticleRequest.Create(FxParticleShape.Droplet, Color.cyan);
            p.Lifetime = 1; p.Motion = FxParticleMotion.Curve; p.Bend = Vector3.up * .2f;
            Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p, true, end.transform), Is.True);
            p.Motion = FxParticleMotion.Spiral; p.Radius = .1f; p.Turns = 1;
            Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p, true, end.transform), Is.True);
            pool.Tick(cues, .5f);
            Assert.That(pool.Root.GetChild(0).position, Is.EqualTo(new Vector3(.5f, .2f, 0)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(Vector3.Distance(pool.Root.GetChild(1).position, pool.Root.GetChild(0).position), Is.EqualTo(.1f).Within(.0001));
            end.transform.position = Vector3.right * 2; pool.Tick(cues, .01f);
            Assert.That(pool.Root.GetChild(0).position.x, Is.GreaterThan(1));
            Object.DestroyImmediate(end); pool.Tick(cues, .01f); Assert.That(pool.ActiveCount, Is.Zero);
        }
        [Test] public void RichFadeVelocityAndReuseClearPropertyBlockMeshAndPath()
        {
            var p = FxParticleRequest.Create(FxParticleShape.Puff, new Color(1, .3f, .1f, .8f));
            p.Lifetime = 1; p.Velocity = Vector3.up * .4f; p.Size = .1f; p.EndSize = .2f;
            Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p), Is.True);
            var first = pool.Root.GetChild(0); var renderer = first.GetComponent<MeshRenderer>(); var block = new MaterialPropertyBlock();
            pool.Tick(cues, .2f); renderer.GetPropertyBlock(block); Assert.That(block.GetColor("_Tint").a, Is.EqualTo(.8f).Within(.001));
            Assert.That(first.position.y, Is.EqualTo(.08f).Within(.001)); Assert.That(first.localScale.x, Is.GreaterThan(.1f));
            pool.Tick(cues, .6f); renderer.GetPropertyBlock(block); Assert.That(block.GetColor("_Tint").a, Is.LessThan(.2f));
            pool.CancelAll(); Assert.That(renderer.HasPropertyBlock(), Is.False); Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(Emit(), Is.True); pool.Tick(cues, .1f);
            Assert.That(renderer.sharedMaterial, Is.SameAs(material)); Assert.That(renderer.HasPropertyBlock(), Is.False);
            Assert.That(first.position.x, Is.Zero); Assert.That(first.position.y, Is.LessThan(.02f));
        }
        [Test] public void AmbientEmitterPressureCannotStealActionEmissionAndLowCancelsOldPaths()
        {
            var p = FxParticleRequest.Create(FxParticleShape.Bubble, Color.cyan);
            for (int i = 0; i < 5; i++)
            {
                var owner = cues.Register("ambient-" + i, "id");
                Assert.That(pool.EmitParticle(cues, owner, source.transform, Vector3.zero, p, false), Is.EqualTo(i < 4));
            }
            for (int i = 0; i < 8; i++) Assert.That(pool.EmitParticle(cues, cues.Register("action-" + i, "id"), source.transform, Vector3.zero, p), Is.True);
            Assert.That(pool.EmitParticle(cues, cues.Register("action-over", "id"), source.transform, Vector3.zero, p), Is.False);
            Assert.That(pool.LogicalEmitterLimit, Is.EqualTo(12)); pool.SetLowDetail(true); Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.LogicalEmitterLimit, Is.EqualTo(6)); Assert.That(pool.ActionLimit, Is.EqualTo(24)); Assert.That(pool.AmbientLimit, Is.EqualTo(8));
            Assert.That(pool.Root.childCount, Is.EqualTo(96));
        }
        [TestCase("local")][TestCase("end")][TestCase("bend")][TestCase("velocity")][TestCase("color")][TestCase("radius")][TestCase("size")][TestCase("end-size")][TestCase("lifetime")][TestCase("spin")][TestCase("turns")][TestCase("shape")][TestCase("motion")]
        public void InvalidParticleDataCannotPoisonReusedTransforms(string fault)
        {
            var p = FxParticleRequest.Create(FxParticleShape.Bubble, Color.cyan); Vector3 local = Vector3.zero;
            switch (fault)
            {
                case "local": local.x = float.PositiveInfinity; break; case "end": p.End.x = float.NaN; break;
                case "bend": p.Bend.y = float.NaN; break; case "velocity": p.Velocity.z = float.PositiveInfinity; break;
                case "color": p.Color.r = float.NaN; break; case "radius": p.Radius = .31f; break; case "size": p.Size = .31f; break;
                case "end-size": p.EndSize = -1; break; case "lifetime": p.Lifetime = 1.6f; break; case "spin": p.Spin = float.PositiveInfinity; break;
                case "turns": p.Turns = 5; break; case "shape": p.Shape = (FxParticleShape)5; break; case "motion": p.Motion = (FxParticleMotion)3; break;
            }
            Assert.That(pool.EmitParticle(cues, token, source.transform, local, p), Is.False); Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(Emit(), Is.True); Assert.That(pool.Root.GetChild(0).position, Is.EqualTo(Vector3.zero));
        }
        [Test] public void WarmedRichEmitTickCancelAllocatesNoManagedBytes()
        {
            var p = FxParticleRequest.Create(FxParticleShape.Bubble, Color.cyan); p.Motion = FxParticleMotion.Spiral; p.End = Vector3.up; p.Radius = .1f; p.Turns = 2;
            for (int i = 0; i < 20; i++) { pool.EmitParticle(cues, token, source.transform, Vector3.zero, p); pool.Tick(cues, .1f); pool.CancelAll(); }
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { pool.EmitParticle(cues, token, source.transform, Vector3.zero, p); pool.Tick(cues, .1f); pool.CancelAll(); }
            long delta = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(delta, Is.Zero); Assert.That(pool.Root.childCount, Is.EqualTo(96));
        }
        [Test] public void ReducedMotionKeepsStationaryFeedbackAndModeChangeClearsSpiral()
        {
            var p = FxParticleRequest.Create(FxParticleShape.Bubble, Color.cyan); p.Motion = FxParticleMotion.Spiral; p.End = Vector3.up; p.Radius = .2f; p.Turns = 2; p.Spin = 180; p.EndSize = .3f;
            pool.EmitParticle(cues, token, source.transform, Vector3.zero, p); pool.SetReducedMotion(true); Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p), Is.True);
            var first = pool.Root.GetChild(0); Vector3 scale = first.localScale; Quaternion rotation = first.rotation;
            pool.Tick(cues, .3f); Assert.That(first.position, Is.EqualTo(Vector3.zero)); Assert.That(first.localScale, Is.EqualTo(scale)); Assert.That(first.rotation, Is.EqualTo(rotation));
            Assert.That(scale.x, Is.EqualTo(p.Size * .65f).Within(.0001)); pool.Tick(cues, .31f); Assert.That(pool.ActiveCount, Is.Zero);
        }
        [Test] public void CachedCameraBillboardsAndRichOwnerEpochCannotReplay()
        {
            var go = new GameObject("Explicit isolated view"); go.transform.SetParent(parent.transform); var camera = go.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(20, 70, 0); pool.SetViewCamera(camera);
            var p = FxParticleRequest.Create(FxParticleShape.Ring, Color.cyan); Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p), Is.True);
            pool.Tick(cues, .1f); Assert.That(Quaternion.Angle(pool.Root.GetChild(0).rotation, camera.transform.rotation), Is.LessThan(.01f));
            cues.Reset(); pool.Tick(cues, .1f); Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p), Is.False);
            token = cues.Register("source", "replacement"); Assert.That(pool.EmitParticle(cues, token, source.transform, Vector3.zero, p), Is.True);
            source.SetActive(false); pool.Tick(cues, .01f); Assert.That(pool.ActiveCount, Is.Zero);
        }
    }
}
