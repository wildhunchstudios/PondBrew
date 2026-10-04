using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        Mesh decorCylinder, decorGlobe;

        static float DecorHeight(string definitionId)
        {
            if (definitionId == ShopLayout.CoralPlanterId) return 1.42f;
            if (definitionId == ShopLayout.BubbleLampId) return 1.72f;
            if (definitionId == ShopLayout.SillyStatueId) return 1.48f;
            throw new ArgumentException("Unknown decor definition: " + definitionId);
        }

        void BuildPlacedDecor(Transform root, string definitionId, bool preview)
        {
            float height = DecorHeight(definitionId);
            if (!preview)
            {
                // The conservative footprint is a real obstacle, including the empty space between branches.
                var collider = root.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, height * .5f, 0);
                collider.size = new Vector3(ShopLayout.DefinitionWidth(definitionId), height, ShopLayout.DefinitionDepth(definitionId));
            }
            if (definitionId == ShopLayout.CoralPlanterId) BuildCoralPlanter(root, preview);
            else if (definitionId == ShopLayout.BubbleLampId) BuildBubbleLamp(root, preview);
            else BuildSillyStatue(root, preview);
        }

        void BuildCoralPlanter(Transform root, bool preview)
        {
            DecorPart(root, "Porcelain coral pot", DecorCylinder(), new Vector3(0, .22f, 0), new Vector3(.54f, .44f, .54f), cream, preview);
            DecorPart(root, "Pot seafoam collar", DecorCylinder(), new Vector3(0, .40f, 0), new Vector3(.60f, .09f, .60f), shell, preview);
            DecorPart(root, "Dark growing bed", DecorCylinder(), new Vector3(0, .445f, 0), new Vector3(.49f, .025f, .49f), dark, preview);
            CoralBranch(root, new Vector3(0, .45f, 0), new Vector3(0, 1.33f, -.04f), .115f, preview);
            CoralBranch(root, new Vector3(0, .74f, 0), new Vector3(-.30f, 1.03f, .12f), .09f, preview);
            CoralBranch(root, new Vector3(-.30f, 1.03f, .12f), new Vector3(-.34f, 1.26f, .13f), .075f, preview);
            CoralBranch(root, new Vector3(-.30f, 1.03f, .12f), new Vector3(-.13f, 1.18f, .28f), .07f, preview);
            CoralBranch(root, new Vector3(0, .83f, 0), new Vector3(.28f, 1.02f, -.18f), .095f, preview);
            CoralBranch(root, new Vector3(.28f, 1.02f, -.18f), new Vector3(.32f, 1.22f, -.18f), .075f, preview);
            CoralBranch(root, new Vector3(.28f, 1.02f, -.18f), new Vector3(.13f, 1.16f, -.30f), .07f, preview);
            DecorPart(root, "Coral crown bud", DecorGlobe(), new Vector3(0, 1.33f, -.04f), Vector3.one * .15f, bloomMat, preview);
            DecorPart(root, "Coral left bud", DecorGlobe(), new Vector3(-.34f, 1.26f, .13f), Vector3.one * .13f, bloomMat, preview);
            DecorPart(root, "Coral near bud", DecorGlobe(), new Vector3(-.13f, 1.18f, .28f), Vector3.one * .12f, gold, preview);
            DecorPart(root, "Coral right bud", DecorGlobe(), new Vector3(.32f, 1.22f, -.18f), Vector3.one * .14f, bloomMat, preview);
            DecorPart(root, "Coral far bud", DecorGlobe(), new Vector3(.13f, 1.16f, -.30f), Vector3.one * .12f, gold, preview);
        }

        void CoralBranch(Transform parent, Vector3 start, Vector3 end, float thickness, bool preview)
        {
            var branch = DecorPart(parent, "Faceted coral branch", DecorCylinder(), (start + end) * .5f,
                new Vector3(thickness, Vector3.Distance(start, end), thickness), bloomMat, preview);
            branch.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }

        void BuildBubbleLamp(Transform root, bool preview)
        {
            DecorPart(root, "Lamp weighted foot", DecorCylinder(), new Vector3(0, .06f, 0), new Vector3(.78f, .12f, .78f), dark, preview);
            DecorPart(root, "Lamp brass base ring", DecorCylinder(), new Vector3(0, .13f, 0), new Vector3(.63f, .055f, .63f), gold, preview);
            DecorPart(root, "Slender brass stem", DecorCylinder(), new Vector3(0, .61f, 0), new Vector3(.115f, .96f, .115f), gold, preview);
            DecorPart(root, "Lamp lower collar", DecorCylinder(), new Vector3(0, .99f, 0), new Vector3(.48f, .08f, .48f), dark, preview);
            DecorPart(root, "Warm bubble bulb", DecorGlobe(), new Vector3(0, 1.32f, 0), new Vector3(.44f, .52f, .44f), gold, preview);
            DecorPart(root, "Clear bubble shade", DecorGlobe(), new Vector3(0, 1.32f, 0), new Vector3(.64f, .70f, .64f), glass, preview);
            DecorPart(root, "Lamp cap", DecorCylinder(), new Vector3(0, 1.68f, 0), new Vector3(.42f, .08f, .42f), cream, preview);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    DecorPart(root, "Lamp shade brace", DecorCylinder(), new Vector3(x * .23f, 1.32f, z * .23f), new Vector3(.026f, .65f, .026f), trim, preview);
            // It is a decorative prop; no extra realtime Light, simulation or appeal authority lives here.
        }

        void BuildSillyStatue(Transform root, bool preview)
        {
            TablePart(root, "Statue broad plinth foot", new Vector3(0, .06f, 0), new Vector3(.82f, .12f, .82f), dark, preview);
            TablePart(root, "Statue porcelain pedestal", new Vector3(0, .36f, 0), new Vector3(.48f, .50f, .48f), cream, preview);
            TablePart(root, "Statue gold plinth cap", new Vector3(0, .66f, 0), new Vector3(.68f, .10f, .68f), gold, preview);
            DecorPart(root, "Statue support", DecorCylinder(), new Vector3(0, .80f, 0), new Vector3(.10f, .22f, .10f), trim, preview);
            DecorPart(root, "Improbably regal fish", DecorGlobe(), new Vector3(0, 1.01f, 0), new Vector3(.55f, .46f, .42f), flowMat, preview);
            DecorPart(root, "Fish tail fan", DecorGlobe(), new Vector3(0, 1.02f, .30f), new Vector3(.34f, .40f, .12f), shell, preview);
            for (int side = -1; side <= 1; side += 2)
            {
                var fin = DecorPart(root, "Tiny waving fin", DecorGlobe(), new Vector3(side * .30f, 1.01f, 0), new Vector3(.20f, .12f, .20f), gold, preview);
                fin.localRotation = Quaternion.Euler(0, 0, side * 25);
                DecorPart(root, "Oversized surprised eye", DecorGlobe(), new Vector3(side * .15f, 1.14f, -.17f), Vector3.one * .15f, cream, preview);
                DecorPart(root, "Statue pupil", DecorGlobe(), new Vector3(side * .15f, 1.14f, -.247f), Vector3.one * .062f, black, preview);
            }
            DecorPart(root, "Ridiculous puckered mouth", DecorGlobe(), new Vector3(0, 1f, -.24f), new Vector3(.22f, .12f, .075f), bloomMat, preview);
            DecorPart(root, "Dorsal fin", DecorGlobe(), new Vector3(0, 1.25f, .03f), new Vector3(.10f, .25f, .25f), shell, preview);
            TablePart(root, "Very small crown band", new Vector3(0, 1.33f, -.025f), new Vector3(.32f, .06f, .16f), gold, preview);
            for (int i = -1; i <= 1; i++)
                TablePart(root, "Crown point", new Vector3(i * .11f, 1.40f, -.025f), new Vector3(.055f, .12f, .10f), gold, preview);
        }

        Transform DecorPart(Transform root, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material, bool preview)
        {
            var part = Group(name, root, position);
            part.localScale = scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = preview ? previewValid : material;
            if (preview || material.renderQueue >= 3000)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (preview) previewSurfaces.Add(renderer);
            return part;
        }

        Mesh DecorCylinder()
        {
            if (decorCylinder) return decorCylinder;
            const int sides = 12;
            var vertices = new List<Vector3>(sides * 12);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                var lowerA = new Vector3(Mathf.Cos(a) * .5f, -.5f, Mathf.Sin(a) * .5f);
                var lowerB = new Vector3(Mathf.Cos(b) * .5f, -.5f, Mathf.Sin(b) * .5f);
                Vector3 upperA = lowerA + Vector3.up, upperB = lowerB + Vector3.up;
                DecorTriangle(vertices, lowerA, upperA, upperB);
                DecorTriangle(vertices, lowerA, upperB, lowerB);
                DecorTriangle(vertices, Vector3.up * .5f, upperB, upperA);
                DecorTriangle(vertices, Vector3.down * .5f, lowerA, lowerB);
            }
            decorCylinder = DecorMesh("Shared twelve-sided decor cylinder", vertices);
            return decorCylinder;
        }

        Mesh DecorGlobe()
        {
            if (decorGlobe) return decorGlobe;
            float phi = (1 + Mathf.Sqrt(5)) * .5f;
            var points = new[]
            {
                new Vector3(-1,phi,0),new Vector3(1,phi,0),new Vector3(-1,-phi,0),new Vector3(1,-phi,0),
                new Vector3(0,-1,phi),new Vector3(0,1,phi),new Vector3(0,-1,-phi),new Vector3(0,1,-phi),
                new Vector3(phi,0,-1),new Vector3(phi,0,1),new Vector3(-phi,0,-1),new Vector3(-phi,0,1)
            };
            int[] faces = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var vertices = new List<Vector3>(60);
            for (int i = 0; i < faces.Length; i++) vertices.Add(points[faces[i]] * (.5f / phi));
            decorGlobe = DecorMesh("Shared faceted decor globe", vertices);
            return decorGlobe;
        }

        static void DecorTriangle(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c)
        { vertices.Add(a); vertices.Add(b); vertices.Add(c); }

        static Mesh DecorMesh(string name, List<Vector3> vertices)
        {
            var triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        void DisposeDecorPresentation()
        {
            if (decorCylinder) Destroy(decorCylinder);
            if (decorGlobe) Destroy(decorGlobe);
        }
    }
}
