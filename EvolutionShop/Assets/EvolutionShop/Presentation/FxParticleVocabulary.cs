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
            const int n = 32;
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            if (shape == FxParticleShape.Bubble || shape == FxParticleShape.Ring)
            {
                // Hollow center and feathered outer/inner edge; no texture or paid mask.
                float inner = shape == FxParticleShape.Bubble ? .32f : .40f;
                float[] radii = { inner - .025f, inner, .475f, .5f };
                float[] alpha = { 0, .72f, 1, 0 };
                for (int band = 0; band < 4; band++)
                    for (int j = 0; j < n; j++)
                    {
                        float angle = j * Mathf.PI * 2 / n;
                        vertices.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radii[band]);
                        float shine = shape == FxParticleShape.Bubble ? .58f + .42f * Mathf.Max(0, Mathf.Sin(angle)) : 1;
                        colors.Add(new Color(1, 1, 1, alpha[band] * shine));
                    }
                for (int band = 0; band < 3; band++) for (int j = 0; j < n; j++)
                {
                    int a = band * n + j, b = band * n + (j + 1) % n, c = a + n, d = b + n;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            else
            {
                vertices.Add(Vector3.zero); colors.Add(Color.white);
                for (int band = 0; band < 2; band++) for (int j = 0; j < n; j++)
                {
                    float angle = j * Mathf.PI * 2 / n, radius = .5f;
                    if (shape == FxParticleShape.Spark) radius *= .25f + .75f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2)), 5);
                    if (shape == FxParticleShape.Puff) radius *= .82f + .18f * Mathf.Cos(angle * 5);
                    Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                    if (shape == FxParticleShape.Droplet)
                    { point.x *= .68f * (1 - .6f * Mathf.Max(0, Mathf.Sin(angle))); point.y = .5f * Mathf.Sin(angle); }
                    vertices.Add(point * (band == 0 ? .78f : 1));
                    colors.Add(new Color(1, 1, 1, band == 0 ? .85f : 0));
                }
                for (int j = 0; j < n; j++)
                {
                    int a = 1 + j, b = 1 + (j + 1) % n;
                    triangles.AddRange(new[] { 0, a, b, a, a + n, b, b, a + n, b + n });
                }
            }
            var mesh = new Mesh { name = "FX original " + shape };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            mesh.UploadMeshData(false); return mesh;
        }
        static void Release(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose()
        { for (int i = 0; i < meshes.Length; i++) { Release(meshes[i]); meshes[i] = null; } Release(Material); Material = null; }
    }
}
