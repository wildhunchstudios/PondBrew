using UnityEditor;
using UnityEngine;

namespace EvolutionShop.Editor
{
    // Apply only to the authored kits: the creatures (DioramaKit), the world (GrowthKit) and the people (PeopleKit, GR-18). No package or global importer changes.
    public sealed class RedesignModelImporter : AssetPostprocessor
    {
        const string DioramaKit = "Assets/EvolutionShop/Resources/Art/DioramaKit/";

        void OnPreprocessModel()
        {
            bool diorama = assetPath.StartsWith(DioramaKit);
            if (!assetPath.StartsWith("Assets/EvolutionShop/Resources/Art/GrowthKit/") && !assetPath.StartsWith("Assets/EvolutionShop/Resources/Art/PeopleKit/") && !diorama) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // AD-A: the creature kit's faces are blend shapes (blink, happy, surprised, puffed); the kit stays readable.
            if (diorama) { importer.importBlendShapes = true; importer.importBlendShapeNormals = ModelImporterNormals.Calculate; importer.isReadable = true; }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DioramaKit)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Keep the exact size, never rescale it to a power of two.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (assetPath.EndsWith("/DioramaVariantAtlas.png"))
            {
                // AD-B: the colour variant atlas is data, one texel per slot: read by the game on the CPU, exactly as authored.
                importer.sRGBTexture = false;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = false;
            }
        }
    }
}
