using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>Fixed world-owned mesh pulse pool. Shared mesh/material assets are borrowed, never destroyed.</summary>
    public sealed class FxEffectPool : IDisposable
    {
        public const int Capacity = 96, ActionReserved = 24;
        sealed class Mote
        {
            public Transform Root, Source, Destination;
            public MeshRenderer Renderer;
            public FxOwnerToken Owner;
            public Vector3 Local, EndLocal, StartPoint;
            public bool Travel;
            public float Age, Lifetime, StartSize;
            public bool Active;
        }
        readonly Mote[] motes = new Mote[Capacity];
        GameObject root;
        bool disposed, lowDetail;
        int lastEmittedIndex;
        public int SpawnedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int ActiveCount { get; private set; }
        public int OwnedCount => disposed ? 0 : Capacity;
        public Transform Root => root ? root.transform : null;
        public FxEffectPool(Transform parent, Mesh mesh)
        {
            if (!parent || !mesh) throw new ArgumentException("A task-owned parent and shared mesh are required.");
            root = new GameObject("FX / bounded pulse pool"); root.transform.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("FX pooled pulse " + i); go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                go.SetActive(false);
                motes[i] = new Mote { Root = go.transform, Renderer = renderer };
            }
        }
        public void SetLowDetail(bool value) { if (lowDetail != value) { CancelAll(); lowDetail = value; } }
        public bool Emit(FxCueRuntime runtime, FxOwnerToken owner, Transform source, Vector3 local, Material material, float lifetime, float size, bool action = true)
        {
            if (disposed || !source || !source.gameObject.activeInHierarchy || !material || runtime == null || !runtime.IsValid(owner) ||
                lifetime <= 0 || lifetime > 1.2f || float.IsNaN(lifetime) || size <= 0 || size > .08f || float.IsNaN(size)) return false;
            int start = action ? 0 : ActionReserved, end = action ? ActionReserved : lowDetail ? 32 : Capacity;
            for (int i = start; i < end; i++)
            {
                var mote = motes[i]; if (mote.Active) continue;
                mote.Travel = false; mote.Destination = null;
                mote.Source = source; mote.Local = local; mote.Owner = owner; mote.Age = 0; mote.Lifetime = lifetime; mote.StartSize = size; mote.Active = true;
                mote.Renderer.sharedMaterial = material; mote.Root.position = source.TransformPoint(local); mote.Root.rotation = source.rotation; mote.Root.localScale = Vector3.one * size;
                lastEmittedIndex = i; mote.Root.gameObject.SetActive(true); ActiveCount++; SpawnedCount++; return true;
            }
            DroppedCount++; return false;
        }
        public bool EmitTravel(FxCueRuntime runtime, FxOwnerToken owner, Transform source, Vector3 local, Transform destination, Vector3 end, Material material, float size)
        {
            if (!destination || !destination.gameObject.activeInHierarchy || !Emit(runtime, owner, source, local, material, .52f, size, false)) return false;
            var m = motes[lastEmittedIndex];
            m.Travel = true; m.Destination = destination; m.EndLocal = end; m.StartPoint = source.TransformPoint(local); return true;
        }
        public void Tick(FxCueRuntime runtime, float seconds)
        {
            if (disposed || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            for (int i = 0; i < motes.Length; i++)
            {
                var mote = motes[i]; if (!mote.Active) continue;
                if (!mote.Source || !mote.Source.gameObject.activeInHierarchy || runtime == null || !runtime.IsValid(mote.Owner)) { Release(mote); continue; }
                if (mote.Travel && (!mote.Destination || !mote.Destination.gameObject.activeInHierarchy)) { Release(mote); continue; }
                mote.Age += seconds;
                if (mote.Age >= mote.Lifetime) { Release(mote); continue; }
                float f = mote.Age / mote.Lifetime;
                mote.Root.position = mote.Travel ? Vector3.Lerp(mote.StartPoint, mote.Destination.TransformPoint(mote.EndLocal), f * f) + Vector3.up * (.12f * Mathf.Sin(f * Mathf.PI)) : mote.Source.TransformPoint(mote.Local) + Vector3.up * (.015f * Mathf.Sin(f * Mathf.PI));
                mote.Root.localScale = Vector3.one * Mathf.Lerp(mote.StartSize, .008f, f * f);
            }
        }
        void Release(Mote mote)
        {
            if (!mote.Active) return;
            mote.Root.gameObject.SetActive(false); mote.Root.localPosition = Vector3.zero; mote.Root.localRotation = Quaternion.identity; mote.Root.localScale = Vector3.one;
            mote.Source = mote.Destination = null; mote.Travel = false; mote.EndLocal = mote.StartPoint = default; mote.Owner = default; mote.Local = default; mote.Age = mote.Lifetime = mote.StartSize = 0; mote.Active = false; ActiveCount--;
        }
        public void Cancel(FxOwnerToken owner) { if (!disposed) for (int i = 0; i < motes.Length; i++) if (motes[i].Owner.Equals(owner)) Release(motes[i]); }
        public void CancelAll() { if (!disposed) for (int i = 0; i < motes.Length; i++) Release(motes[i]); }
        public void Dispose()
        {
            if (disposed) return;
            CancelAll(); disposed = true;
            if (root) { root.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); }
            root = null;
        }
    }
}
