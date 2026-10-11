using NUnit.Framework;
using UnityEngine;

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
            for (int i = 0; i < 72; i++) Assert.That(Emit(false), Is.True);
            Assert.That(Emit(false), Is.False);
            for (int i = 0; i < 24; i++) Assert.That(Emit(), Is.True);
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
    }
}
