using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        // Original, code-authored room study. These meshes have no gameplay or collision authority.
        readonly List<Material> artMaterials = new List<Material>();
        readonly List<Mesh> artMeshes = new List<Mesh>();
        Mesh artSoftCube;
        Material artOak, artHoney, artPaleOak, artTeal, artMint, artCoral, artButter, artLeaf, artSky, artTile;

        Material ArtMaterial(string name, Color color, float smoothness = .28f)
        {
            var value = MakeMaterial("Cozy room study / " + name, color, 0, smoothness);
            artMaterials.Add(value);
            return value;
        }

        void BuildArtPalette()
        {
            artOak = ArtMaterial("oak shadow", new Color(.48f, .25f, .12f));
            artHoney = ArtMaterial("honey oak", new Color(.73f, .46f, .25f));
            artPaleOak = ArtMaterial("butterscotch oak", new Color(.87f, .64f, .37f));
            artTeal = ArtMaterial("storybook teal", new Color(.12f, .40f, .38f));
            artMint = ArtMaterial("mint enamel", new Color(.49f, .76f, .67f));
            artCoral = ArtMaterial("peach coral", new Color(.96f, .43f, .31f));
            artButter = ArtMaterial("butter yellow", new Color(1f, .78f, .35f));
            artLeaf = ArtMaterial("leaf green", new Color(.25f, .53f, .29f));
            artSky = ArtMaterial("window blue", new Color(.52f, .81f, .83f));
            artTile = ArtMaterial("lab tile", new Color(.77f, .91f, .82f));
        }

        Transform ArtBox(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool soft = true)
        {
            var part = Group(name, parent, position);
            part.localScale = size;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = soft ? ArtSoftCube() : PlacementCube();
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        Mesh ArtSoftCube()
        {
            if (artSoftCube) return artSoftCube;
            const int steps = 4;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var normal in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                for (int y = 0; y <= steps; y++)
                    for (int x = 0; x <= steps; x++)
                    {
                        Vector3 p = normal * .5f + u * (x / (float)steps - .5f) + v * (y / (float)steps - .5f);
                        Vector3 inner = new Vector3(Mathf.Clamp(p.x, -.40f, .40f), Mathf.Clamp(p.y, -.40f, .40f), Mathf.Clamp(p.z, -.40f, .40f));
                        Vector3 n = (p - inner).normalized;
                        vertices.Add(inner + n * .1f); normals.Add(n);
                        if (x == steps || y == steps) continue;
                        int a = start + y * (steps + 1) + x;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + steps + 2);
                        triangles.Add(a); triangles.Add(a + steps + 2); triangles.Add(a + steps + 1);
                    }
            }
            artSoftCube = new Mesh { name = "Owned soft cartoon box" };
            artSoftCube.SetVertices(vertices); artSoftCube.SetNormals(normals); artSoftCube.SetTriangles(triangles, 0);
            artSoftCube.RecalculateBounds();
            return artSoftCube;
        }

        // Merge only static opaque decoration owned by the passed art group, never text or live stations.
        // Material groups keep the room detail bounded rather than one renderer per plank, leaf or tile.
        void SealArt(Transform root)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var retired = new List<GameObject>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh || filter.GetComponent<TextMesh>() || renderer.sharedMaterial.renderQueue >= 3000) continue;
                if (!byMaterial.TryGetValue(renderer.sharedMaterial, out var parts))
                    byMaterial.Add(renderer.sharedMaterial, parts = new List<CombineInstance>());
                parts.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                renderer.enabled = false;
                retired.Add(filter.gameObject);
            }
            foreach (var pair in byMaterial)
            {
                var mesh = new Mesh { name = root.name + " / " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds(); artMeshes.Add(mesh);
                var part = Group("Merged " + pair.Key.name, root, Vector3.zero);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
            }
            foreach (var part in retired) Destroy(part);
        }

        void BuildRoomArt()
        {
            BuildAquaticRooms();
        }

        void ShopPanelling(Transform root, float width)
        {
            ArtBox(root, "Teal wainscot", new Vector3(0, .57f, -.005f), new Vector3(width, 1.13f, .06f), artTeal, false);
            ArtBox(root, "Oak chair rail", new Vector3(0, 1.15f, -.045f), new Vector3(width, .09f, .105f), artPaleOak);
            ArtBox(root, "Warm skirting", new Vector3(0, .10f, -.044f), new Vector3(width, .17f, .08f), artHoney);
            int panels = Mathf.FloorToInt(width / .62f);
            for (int i = 0; i < panels; i++)
                ArtBox(root, "Panel beading", new Vector3((i - (panels - 1) * .5f) * .60f, .64f, -.05f), new Vector3(.035f, .89f, .022f), artMint);
        }

        void ArtWindow(Transform root)
        {
            ArtBox(root, "Rounded window frame", new Vector3(0, 2.25f, -.04f), new Vector3(2.54f, 2.12f, .11f), artPaleOak);
            ArtBox(root, "Painted window sky", new Vector3(0, 2.25f, -.11f), new Vector3(2.30f, 1.89f, .05f), artSky);
            Shape(root, "Soft outside tree", PrimitiveType.Sphere, new Vector3(.7f, 1.83f, -.155f), new Vector3(.76f, .90f, .025f), artMint);
            Shape(root, "Soft outside shrub", PrimitiveType.Sphere, new Vector3(-.80f, 1.67f, -.16f), new Vector3(.61f, .57f, .025f), artLeaf);
            Shape(root, "Window cloud", PrimitiveType.Sphere, new Vector3(-.62f, 2.83f, -.16f), new Vector3(.65f, .20f, .03f), cream);
            Shape(root, "Window cloud puff", PrimitiveType.Sphere, new Vector3(-.60f, 2.91f, -.16f), new Vector3(.27f, .25f, .03f), cream);
            ArtBox(root, "Window vertical mullion", new Vector3(0, 2.25f, -.195f), new Vector3(.065f, 1.95f, .08f), cream);
            ArtBox(root, "Window horizontal mullion", new Vector3(0, 2.34f, -.195f), new Vector3(2.37f, .06f, .08f), cream);
            ArtBox(root, "Deep window sill", new Vector3(0, 1.18f, -.14f), new Vector3(2.68f, .12f, .38f), artPaleOak);
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * .38f;
                var material = i % 2 == 0 ? artCoral : cream;
                ArtBox(root, "Candy stripe awning", new Vector3(x, 3.36f, -.21f), new Vector3(.375f, .15f, .48f), material);
                Shape(root, "Scalloped awning hem", PrimitiveType.Sphere, new Vector3(x, 3.24f, -.43f), new Vector3(.38f, .32f, .075f), material);
            }
        }

        void ArtShelfPlant(Transform parent, Vector3 position, float scale)
        {
            var root = Group("Shelf succulent", parent, position); root.localScale = Vector3.one * scale;
            Shape(root, "Terracotta pot", PrimitiveType.Cylinder, new Vector3(0, .17f, 0), new Vector3(.44f, .17f, .44f), artCoral);
            Shape(root, "Pot lip", PrimitiveType.Cylinder, new Vector3(0, .33f, 0), new Vector3(.49f, .035f, .49f), artButter);
            Shape(root, "Plant soil", PrimitiveType.Cylinder, new Vector3(0, .367f, 0), new Vector3(.38f, .01f, .38f), artOak);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72 * Mathf.Deg2Rad;
                var leaf = Shape(root, "Soft leaf", PrimitiveType.Sphere, new Vector3(Mathf.Cos(angle) * .14f, .60f, Mathf.Sin(angle) * .14f), new Vector3(.18f, .54f, .10f), i % 2 == 0 ? artLeaf : artMint);
                leaf.localRotation = Quaternion.Euler(0, -i * 72, 26);
            }
        }

        void ArtBottle(Transform parent, Vector3 position, Material color, float scale)
        {
            var root = Group("Decorative reagent bottle", parent, position); root.localScale = Vector3.one * scale;
            Shape(root, "Rounded bottle", PrimitiveType.Sphere, new Vector3(0, .26f, 0), new Vector3(.43f, .54f, .36f), color);
            Shape(root, "Bottle neck", PrimitiveType.Cylinder, new Vector3(0, .55f, 0), new Vector3(.19f, .10f, .19f), color);
            Shape(root, "Cork", PrimitiveType.Cylinder, new Vector3(0, .67f, 0), new Vector3(.22f, .05f, .22f), artPaleOak);
            ArtBox(root, "Bottle paper label", new Vector3(0, .28f, -.165f), new Vector3(.22f, .16f, .03f), cream);
        }

        void ArtFishSign(Transform parent, Vector3 position)
        {
            var root = Group("Framed fish emblem", parent, position);
            ArtBox(root, "Oak picture frame", Vector3.zero, new Vector3(1.65f, 1.22f, .085f), artHoney);
            ArtBox(root, "Butter paper", new Vector3(0, 0, -.05f), new Vector3(1.44f, 1.01f, .03f), cream);
            Shape(root, "Fish illustration body", PrimitiveType.Sphere, new Vector3(.06f, .03f, -.089f), new Vector3(.67f, .40f, .025f), artTeal);
            Shape(root, "Fish illustration tail", PrimitiveType.Sphere, new Vector3(-.37f, .03f, -.085f), new Vector3(.23f, .39f, .025f), artCoral);
            Shape(root, "Fish illustration eye", PrimitiveType.Sphere, new Vector3(.26f, .09f, -.11f), new Vector3(.075f, .075f, .022f), cream);
            for (int i = 0; i < 3; i++)
                Shape(root, "Illustrated bubble", PrimitiveType.Sphere, new Vector3(.47f + i * .085f, .20f + i * .075f, -.095f), Vector3.one * (.08f - i * .015f), artSky);
        }

        void Pendant(Transform parent, Vector3 position, Material color)
        {
            var root = Group("Warm pendant", parent, position);
            Shape(root, "Pendant cord", PrimitiveType.Cylinder, new Vector3(0, .31f, 0), new Vector3(.025f, .33f, .025f), artOak);
            Shape(root, "Rounded shade", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.07f, .44f, 1.07f), color);
            Shape(root, "Warm shade underside", PrimitiveType.Cylinder, new Vector3(0, -.19f, 0), new Vector3(.90f, .025f, .90f), artButter);
            Shape(root, "Milk glass bulb", PrimitiveType.Sphere, new Vector3(0, -.24f, 0), new Vector3(.26f, .20f, .26f), glow);
            var light = Group("Warm pendant light", parent, position + Vector3.down * .39f).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1, .80f, .56f); light.intensity = 1.8f; light.range = 4.5f; light.shadows = LightShadows.None;
        }

        void ArtPipe(Transform parent, Vector3 start, Vector3 end, Material material)
        {
            Vector3 delta = end - start;
            var body = Shape(parent, "Candy enamel pipe", PrimitiveType.Cylinder, (start + end) * .5f, new Vector3(.10f, delta.magnitude * .5f, .10f), material);
            body.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
            Shape(parent, "Round pipe elbow", PrimitiveType.Sphere, start, Vector3.one * .135f, material);
            Shape(parent, "Round pipe elbow", PrimitiveType.Sphere, end, Vector3.one * .135f, material);
        }

        void ArtGauge(Transform parent, Vector3 position, float scale)
        {
            var root = Group("Oversize pressure meter", parent, position); root.localScale = Vector3.one * scale;
            Shape(root, "Meter housing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.62f, .08f, .62f), artTeal).localRotation = Quaternion.Euler(90, 0, 0);
            Shape(root, "Meter face", PrimitiveType.Cylinder, new Vector3(0, 0, -.09f), new Vector3(.49f, .015f, .49f), cream).localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = -2; i <= 2; i++)
            {
                float angle = i * 30 * Mathf.Deg2Rad;
                ArtBox(root, "Meter tick", new Vector3(Mathf.Sin(angle) * .18f, Mathf.Cos(angle) * .18f, -.111f), new Vector3(.024f, .049f, .01f), artTeal, false).localRotation = Quaternion.Euler(0, 0, -i * 30);
            }
            ArtBox(root, "Meter needle", new Vector3(.035f, .06f, -.126f), new Vector3(.025f, .24f, .018f), artCoral).localRotation = Quaternion.Euler(0, 0, -32);
        }

        void AddCounterArt(Transform root)
        {
            var detail = Group("Friendly counter finish", root, Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                ArtBox(detail, "Counter oak corner", new Vector3(side * 1.03f, .55f, -.53f), new Vector3(.13f, .96f, .10f), artHoney);
                ArtBox(detail, "Inset cabinet edge", new Vector3(side * .91f, .52f, -.545f), new Vector3(.036f, .65f, .03f), artPaleOak);
            }
            ArtBox(detail, "Counter warm edge", new Vector3(0, 1.075f, -.60f), new Vector3(2.35f, .07f, .09f), artPaleOak);
            ArtBox(detail, "Counter plinth", new Vector3(0, .12f, -.55f), new Vector3(2.16f, .12f, .11f), artHoney);
            ArtBox(detail, "Gift parcel", new Vector3(-.87f, 1.25f, .27f), new Vector3(.29f, .26f, .29f), artCoral);
            ArtBox(detail, "Parcel ribbon", new Vector3(-.87f, 1.387f, .27f), new Vector3(.06f, .012f, .30f), cream);
            SealArt(detail);
        }

        void AddIncubatorArt(Transform root)
        {
            var detail = Group("Incubator enamel finish", root, Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                ArtBox(detail, "Rounded coral corner", new Vector3(side * .84f, .44f, -.54f), new Vector3(.17f, .78f, .20f), artCoral);
                ArtBox(detail, "Cream cabinet seam", new Vector3(side * .75f, .42f, -.662f), new Vector3(.025f, .58f, .02f), cream);
            }
            ArtBox(detail, "Incubator warm foot", new Vector3(0, .055f, 0), new Vector3(1.83f, .10f, 1.28f), artMint);
            SealArt(detail);
        }

        void DisposeRoomArt()
        {
            foreach (var mesh in artMeshes) if (mesh) Destroy(mesh);
            if (artSoftCube) Destroy(artSoftCube);
            foreach (var material in artMaterials) if (material) Destroy(material);
        }
    }
}
