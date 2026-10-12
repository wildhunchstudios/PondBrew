using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>96 fixed renderers. Original vocabulary owned; legacy mesh/palette borrowed.</summary>
    public sealed class FxEffectPool : IDisposable
    {
        public const int Capacity = 96, ActionReserved = 64;
        static readonly int TintId = Shader.PropertyToID("_Tint");
        sealed class Mote
        {
            public Transform Root, Source, Destination;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public MaterialPropertyBlock Properties;
            public FxOwnerToken Owner;
            public Vector3 Local, EndLocal, StartPoint;
            public FxParticleRequest Particle;
            public bool Travel, Rich, Action, Active, HasDestination;
            public float Age, Lifetime, StartSize;
        }
        readonly Mote[] motes = new Mote[Capacity];
        readonly Mesh borrowedMesh;
        readonly FxParticleVocabulary vocabulary;
        GameObject root;
        Camera viewCamera;
        bool disposed, lowDetail, reducedMotion;
        int lastEmittedIndex;
        public int SpawnedCount { get; private set; }
        public int DroppedCount { get; private set; }
        public int ActiveCount { get; private set; }
        public int OwnedCount => disposed ? 0 : Capacity;
        public int ActionLimit => lowDetail ? 24 : 64;
        public int AmbientLimit => lowDetail ? 8 : 32;
        public int LogicalEmitterLimit => lowDetail ? 6 : 12;
        public int OwnedMeshCount => disposed ? 0 : vocabulary.MeshCount;
        public int OwnedMaterialCount => disposed || !vocabulary.Ready ? 0 : 1;
        public bool VocabularyReady => !disposed && vocabulary.Ready;
        public Transform Root => root ? root.transform : null;
        public FxEffectPool(Transform parent, Mesh mesh)
        {
            if (!parent || !mesh) throw new ArgumentException("A task-owned parent and shared mesh are required.");
            borrowedMesh = mesh; vocabulary = new FxParticleVocabulary();
            root = new GameObject("FX / bounded pulse pool"); root.transform.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("FX pooled pulse " + i); go.transform.SetParent(root.transform, false);
                var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                go.SetActive(false);
                motes[i] = new Mote { Root = go.transform, Renderer = renderer, Filter = filter, Properties = new MaterialPropertyBlock() };
            }
        }
        public void SetViewCamera(Camera camera) { viewCamera = camera; }
        public void SetLowDetail(bool value) { if (lowDetail != value) { CancelAll(); lowDetail = value; } }
        public void SetReducedMotion(bool value) { if (reducedMotion != value) { CancelAll(); reducedMotion = value; } }
        bool ValidOwner(FxCueRuntime runtime, FxOwnerToken owner, Transform source, Vector3 local) =>
            !disposed && source && source.gameObject.activeInHierarchy && runtime != null && runtime.IsValid(owner) && FxParticleRequest.Finite(local) && local.sqrMagnitude <= 25;
        int FreeSlot(FxOwnerToken owner, Transform source, bool action)
        {
            int start = action ? 0 : ActionReserved, end = start + (action ? ActionLimit : AmbientLimit), emitters = 0;
            bool existing = false;
            for (int i = start; i < end; i++)
            {
                var m = motes[i]; if (!m.Active) continue;
                if (m.Owner.Equals(owner) && m.Source == source) existing = true;
                bool first = true;
                for (int j = start; j < i; j++)
                    if (motes[j].Active && motes[j].Owner.Equals(m.Owner) && motes[j].Source == m.Source) { first = false; break; }
                if (first) emitters++;
            }
            // Separate admission protects accepted actions from ambient emitter pressure too.
            int limit = action ? (lowDetail ? 4 : 8) : (lowDetail ? 2 : 4);
            if (existing || emitters < limit) for (int i = start; i < end; i++) if (!motes[i].Active) return i;
            DroppedCount++; return -1;
        }
        void Activate(int i, FxOwnerToken owner, Transform source, Vector3 local, float lifetime, float size, bool action)
        {
            var m = motes[i]; m.Source = source; m.Local = local; m.Owner = owner; m.Age = 0; m.Lifetime = lifetime; m.StartSize = size; m.Action = action; m.Active = true;
            m.Root.position = source.TransformPoint(local); m.Root.rotation = source.rotation; m.Root.localScale = Vector3.one * size;
            lastEmittedIndex = i; m.Root.gameObject.SetActive(true); ActiveCount++; SpawnedCount++;
        }
        public bool Emit(FxCueRuntime runtime, FxOwnerToken owner, Transform source, Vector3 local, Material material, float lifetime, float size, bool action = true)
        {
            if (!ValidOwner(runtime, owner, source, local) || !material || !FxParticleRequest.Finite(lifetime) || lifetime <= 0 || lifetime > 1.2f || !FxParticleRequest.Finite(size) || size <= 0 || size > .08f) return false;
            int i = FreeSlot(owner, source, action); if (i < 0) return false;
            var m = motes[i]; m.Filter.sharedMesh = borrowedMesh; m.Renderer.sharedMaterial = material; m.Renderer.SetPropertyBlock(null);
            Activate(i, owner, source, local, lifetime, size, action); return true;
        }
        public bool EmitTravel(FxCueRuntime runtime, FxOwnerToken owner, Transform source, Vector3 local, Transform destination, Vector3 end, Material material, float size)
        {
            if (!destination || !destination.gameObject.activeInHierarchy || !FxParticleRequest.Finite(end) || end.sqrMagnitude > 25 || !Emit(runtime, owner, source, local, material, .52f, size, false)) return false;
            var m = motes[lastEmittedIndex]; m.Travel = true; m.Destination = destination; m.EndLocal = end; m.StartPoint = source.TransformPoint(local); return true;
        }
        public bool EmitParticle(FxCueRuntime runtime, FxOwnerToken owner, Transform source, Vector3 local, FxParticleRequest request, bool action = true, Transform destination = null)
        {
            if (!ValidOwner(runtime, owner, source, local) || !VocabularyReady || !request.IsValid ||
                (destination != null && !destination.gameObject.activeInHierarchy)) return false;
            int i = FreeSlot(owner, source, action); if (i < 0) return false;
            if (reducedMotion)
            {
                request.Motion = FxParticleMotion.Drift; request.Velocity = Vector3.zero;
                request.Radius = request.Turns = request.Spin = 0;
                request.Size *= .65f; request.EndSize = request.Size; request.Lifetime = Mathf.Min(request.Lifetime, .6f);
            }
            var m = motes[i]; m.Rich = true; m.Particle = request; m.Destination = destination; m.HasDestination = destination != null;
            m.Filter.sharedMesh = vocabulary.Mesh(request.Shape); m.Renderer.sharedMaterial = vocabulary.Material;
            Activate(i, owner, source, local, request.Lifetime, request.Size, action);
            PoseParticle(m, 0); return true;
        }
        void PoseParticle(Mote m, float f)
        {
            var p = m.Particle; Vector3 start = m.Source.TransformPoint(m.Local), position;
            if (p.Motion == FxParticleMotion.Drift) position = start + m.Source.TransformVector(p.Velocity) * m.Age;
            else
            {
                Vector3 end = m.Destination ? m.Destination.TransformPoint(p.End) : m.Source.TransformPoint(m.Local + p.End);
                Vector3 bend = m.Source.TransformVector(p.Bend);
                position = Vector3.Lerp(start, end, f) + bend * (4 * f * (1 - f));
                if (p.Motion == FxParticleMotion.Spiral)
                {
                    Vector3 axis = end - start; axis = axis.sqrMagnitude > .000001f ? axis.normalized : m.Source.up;
                    Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 v = Vector3.Cross(axis, u);
                    float angle = f * p.Turns * Mathf.PI * 2;
                    position += (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * (p.Radius * Mathf.Sin(f * Mathf.PI));
                }
            }
            m.Root.position = position;
            m.Root.rotation = (viewCamera ? viewCamera.transform.rotation : m.Source.rotation) * Quaternion.AngleAxis(p.Spin * m.Age, Vector3.forward);
            m.Root.localScale = Vector3.one * Mathf.Lerp(p.Size, p.EndSize, f * f * (3 - 2 * f));
            Color tint = p.Color; tint.a *= Mathf.Min(1, f / .06f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, f)));
            if (lowDetail) tint.a *= .8f;
            m.Properties.SetColor(TintId, tint); m.Renderer.SetPropertyBlock(m.Properties);
        }
        public void Tick(FxCueRuntime runtime, float seconds)
        {
            if (disposed || seconds <= 0 || !FxParticleRequest.Finite(seconds)) return;
            for (int i = 0; i < motes.Length; i++)
            {
                var m = motes[i]; if (!m.Active) continue;
                if (!m.Source || !m.Source.gameObject.activeInHierarchy || runtime == null || !runtime.IsValid(m.Owner) ||
                    ((m.Travel || m.HasDestination) && (!m.Destination || !m.Destination.gameObject.activeInHierarchy))) { Release(m); continue; }
                m.Age += seconds; if (m.Age >= m.Lifetime) { Release(m); continue; }
                float f = m.Age / m.Lifetime;
                if (m.Rich) PoseParticle(m, f);
                else
                {
                    m.Root.position = m.Travel ? Vector3.Lerp(m.StartPoint, m.Destination.TransformPoint(m.EndLocal), f * f) + Vector3.up * (.12f * Mathf.Sin(f * Mathf.PI)) : m.Source.TransformPoint(m.Local) + Vector3.up * (.015f * Mathf.Sin(f * Mathf.PI));
                    m.Root.localScale = Vector3.one * Mathf.Lerp(m.StartSize, .008f, f * f);
                }
            }
        }
        void Release(Mote m)
        {
            if (!m.Active) return;
            m.Root.gameObject.SetActive(false); m.Root.localPosition = Vector3.zero; m.Root.localRotation = Quaternion.identity; m.Root.localScale = Vector3.one;
            m.Source = m.Destination = null; m.Travel = m.Rich = m.Action = m.HasDestination = false; m.Particle = default; m.EndLocal = m.StartPoint = m.Local = default; m.Owner = default;
            m.Age = m.Lifetime = m.StartSize = 0; m.Properties.Clear(); m.Renderer.SetPropertyBlock(null); m.Filter.sharedMesh = borrowedMesh; m.Renderer.sharedMaterial = null;
            m.Active = false; ActiveCount--;
        }
        public void Cancel(FxOwnerToken owner) { if (!disposed) for (int i = 0; i < motes.Length; i++) if (motes[i].Owner.Equals(owner)) Release(motes[i]); }
        public void CancelAmbient(FxOwnerToken owner) { if (!disposed) for (int i = 0; i < motes.Length; i++) if (!motes[i].Action && motes[i].Owner.Equals(owner)) Release(motes[i]); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int ParticleCountForValidation(FxOwnerToken owner) {
            int count = 0;
            if (!disposed && owner.IsDefined) foreach (var m in motes) if (m.Active && m.Owner.Equals(owner)) count++;
            return count;
        }
        public bool TryGetParticleForValidation(FxOwnerToken owner, int ordinal, out Vector3 start,
            out Vector3 end, out Vector3 position, out float age, out float lifetime) {
            start = end = position = Vector3.zero; age = lifetime = 0;
            if (disposed || !owner.IsDefined || ordinal < 0) return false;
            foreach (var m in motes) {
                if (!m.Active || !m.Rich || !m.Owner.Equals(owner) || !m.Source) continue;
                if (ordinal-- != 0) continue;
                start = m.Source.TransformPoint(m.Local);
                end = m.Destination ? m.Destination.TransformPoint(m.Particle.End) : m.Source.TransformPoint(m.Local + m.Particle.End);
                position = m.Root.position; age = m.Age; lifetime = m.Lifetime; return true;
            }
            return false;
        }
        public int AmbientCount(FxOwnerToken owner) {
            int count = 0;
            if (!disposed) for (int i = 0; i < motes.Length; i++) if (motes[i].Active && !motes[i].Action && motes[i].Owner.Equals(owner)) count++;
            return count;
        }
#endif
        public void CancelAll() { if (!disposed) for (int i = 0; i < motes.Length; i++) Release(motes[i]); }
        public void Dispose()
        {
            if (disposed) return;
            CancelAll(); disposed = true; vocabulary.Dispose(); viewCamera = null;
            if (root) { root.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); }
            root = null;
        }
    }
}
