using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public enum FxParticleShape { Bubble, Droplet, Puff, Spark, Ring }
    public enum FxParticleMotion { Drift, Curve, Spiral }

    /// <summary>Cosmetic, source-local path. A destination is optional for moving intakes.</summary>
    public struct FxParticleRequest
    {
        public FxParticleShape Shape;
        public FxParticleMotion Motion;
        public bool SurfaceAligned;
        public Color Color;
        public Vector3 End, Bend, Velocity;
        public float Lifetime, Size, EndSize, Radius, Turns, Spin;
        public static FxParticleRequest Create(FxParticleShape shape, Color color) => new FxParticleRequest
        { Shape = shape, Color = color, Lifetime = .9f, Size = .12f, EndSize = .04f };
        public bool IsValid => (uint)Shape <= 4 && (uint)Motion <= 2 &&
            Finite(Lifetime) && Lifetime > 0 && Lifetime <= 1.5f &&
            Finite(Size) && Size > 0 && Size <= .3f && Finite(EndSize) && EndSize >= 0 && EndSize <= .3f &&
            Finite(Radius) && Radius >= 0 && Radius <= .3f && Finite(Turns) && Mathf.Abs(Turns) <= 4 &&
            Finite(Spin) && Mathf.Abs(Spin) <= 720 && Finite(End) && End.sqrMagnitude <= 9 &&
            Finite(Bend) && Bend.sqrMagnitude <= 1 && Finite(Velocity) && Velocity.sqrMagnitude <= 4 &&
            Finite(Color.r) && Finite(Color.g) && Finite(Color.b) && Finite(Color.a) &&
            Color.r >= 0 && Color.r <= 1 && Color.g >= 0 && Color.g <= 1 && Color.b >= 0 && Color.b <= 1 && Color.a > 0 && Color.a <= 1;
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }

    /// <summary>Five original local meshes and one shared unlit material, owned once by the pool.</summary>
    internal sealed class FxParticleVocabulary : IDisposable
    {
        readonly Mesh[] meshes = new Mesh[5];
        public Material Material { get; private set; }
        public bool Ready => Material;
        public int MeshCount => Ready ? meshes.Length : 0;
        public FxParticleVocabulary()
        {
            var shader = Resources.Load<Shader>("EvolutionFxParticle");
            if (!shader || !shader.isSupported) return; // Missing presentation never blocks commands.
            Material = new Material(shader) { name = "FX shared cartoon particles" };
            for (int i = 0; i < meshes.Length; i++) meshes[i] = Make((FxParticleShape)i);
        }
        public Mesh Mesh(FxParticleShape shape) => meshes[(int)shape];
        static Mesh Make(FxParticleShape shape)
        {
            // Shared circular carrier; original analytic masks carry soft edges, shading and highlights.
            const int n = 40;
            var vertices = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector3> { new Vector3(0, 0, (int)shape) };
            var triangles = new List<int>(n * 3);
            for (int j = 0; j < n; j++)
            {
                float angle = j * Mathf.PI * 2 / n;
                var point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices.Add(point * .5f); uv.Add(new Vector3(point.x, point.y, (int)shape));
                triangles.Add(0); triangles.Add(1 + j); triangles.Add(1 + (j + 1) % n);
            }
            var mesh = new Mesh { name = "FX original " + shape };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            mesh.UploadMeshData(false); return mesh;
        }
        static void Release(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose()
        { for (int i = 0; i < meshes.Length; i++) { Release(meshes[i]); meshes[i] = null; } Release(Material); Material = null; }
    }
}
