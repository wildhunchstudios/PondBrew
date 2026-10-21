using UnityEditor;
using UnityEngine;

namespace EvolutionShop.Editor
{
    // Apply only to the authored kit. No package or global importer changes.
    public sealed class RedesignModelImporter : AssetPostprocessor
    {
        const string DioramaKit = "Assets/EvolutionShop/Resources/Art/DioramaKit/";

        void OnPreprocessModel()
        {
            bool diorama = assetPath.StartsWith(DioramaKit);
            if (!assetPath.StartsWith("Assets/EvolutionShop/Resources/Art/RedesignKit/") &&
                !assetPath.StartsWith("Assets/EvolutionShop/Resources/Art/LabRefreshKit/") &&
                !assetPath.StartsWith("Assets/EvolutionShop/Resources/Art/CollectorKit/") && !diorama) return;
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
            // AD-A: the diorama kit's faces are blend shapes (blink, happy, surprised, puffed; the visitor's moods).
            // The customer reads its legs' vertices to keep the shoes on the floor, so the kit stays readable.
            if (diorama) { importer.importBlendShapes = true; importer.importBlendShapeNormals = ModelImporterNormals.Calculate; importer.isReadable = true; }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DioramaKit)) return;
            // The hand-drawn flipbook sheet: transparent, clamped so neighbouring frames never bleed in.
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            // 4 x 128 by 6 x 128: keep the exact grid, never rescale it to a power of two.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
