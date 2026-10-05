using UnityEditor;

namespace EvolutionShop.Editor
{
    // Apply only to the authored kit. No package or global importer changes.
    public sealed class RedesignModelImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/EvolutionShop/Resources/Art/RedesignKit/")) return;
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
        }
    }
}
