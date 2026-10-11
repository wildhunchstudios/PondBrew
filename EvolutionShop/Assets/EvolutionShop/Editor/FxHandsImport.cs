using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EvolutionShop.Editor
{
    public static class FxHandsImport
    {
        public static void Import()
        {
            ProjectSetup.EnsurePlayableScene();
            const string path = "Assets/EvolutionShop/Resources/Art/LivingShopFX/HandsPair.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("FX-04 original FBX is missing");
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = true; importer.addCollider = false; importer.globalScale = 1; importer.bakeAxisConversion = false;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model || model.GetComponentsInChildren<Renderer>(true).Length != 12 || model.GetComponentsInChildren<Animator>(true).Length != 0)
                throw new InvalidOperationException("Unexpected imported glove hierarchy");
            Debug.Log("FX04_IMPORT_READY " + AssetDatabase.AssetPathToGUID(path) + " renderers=12 Unity=" + Application.unityVersion);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) Debug.Log("FX04_PIVOT " + t.name + " position=" + t.position + " local=" + t.localPosition + " rotation=" + t.localEulerAngles + " scale=" + t.localScale);
        }
    }
}
