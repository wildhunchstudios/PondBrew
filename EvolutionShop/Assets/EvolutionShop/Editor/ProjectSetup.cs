using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        static ProjectSetup() { EditorApplication.update += WaitForStartup; }
        static void WaitForStartup()
        {
            if (!SessionState.GetBool("EvolutionShop.SmokePending",false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat("EvolutionShop.SmokeRequested",0) < 5) return;
            SessionState.SetBool("EvolutionShop.SmokePending",false);
            EditorApplication.EnterPlaymode();
        }
        public const string ScenePath = "Assets/EvolutionShop/Scenes/Shop.unity";
        [MenuItem("Evolution Shop/Rebuild playable scene")]
        public static void CreateScene()
        {
            Guard();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Shop session").AddComponent<ShopSession>();
            Directory.CreateDirectory("Assets/EvolutionShop/Scenes");
            Directory.CreateDirectory("Assets/EvolutionShop/Resources");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is missing.");
            SaveMaterial("EvolutionLit", new Material(shader));
            var glow = new Material(shader); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(0.15f,0.55f,0.3f)); SaveMaterial("EvolutionGlow",glow);
            var glass = new Material(shader);
            glass.SetFloat("_Surface", 1); glass.SetFloat("_Blend", 0);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = 3000;
            glass.SetColor("_BaseColor", new Color(0.3f,0.8f,0.85f,0.12f));
            SaveMaterial("EvolutionGlass", glass);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "DumbVideoGameStudio";
            PlayerSettings.productName = "EvolutionShopPrototype";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            Debug.Log("EVOLUTION_SCENE_READY " + Application.dataPath + " " + ScenePath);
        }
        static void SaveMaterial(string name, Material material)
        {
            string path = "Assets/EvolutionShop/Resources/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { EditorUtility.CopySerialized(material, existing); UnityEngine.Object.DestroyImmediate(material); }
            else AssetDatabase.CreateAsset(material, path);
        }
        public static void RunSmoke()
        {
            CreateScene();
            SessionState.SetFloat("EvolutionShop.SmokeRequested",(float)EditorApplication.timeSinceStartup); SessionState.SetBool("EvolutionShop.SmokePending",true);
        }
        public static void BuildWindows()
        {
            CreateScene();
            Directory.CreateDirectory("../Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] {ScenePath},
                locationPathName = "../Builds/Windows/EvolutionShop.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            Directory.CreateDirectory("../Docs/Evidence");
            File.WriteAllText("../Docs/Evidence/windows-build-result.json", "{\"result\":\"" + report.summary.result + "\",\"errors\":" + report.summary.totalErrors + ",\"warnings\":" + report.summary.totalWarnings + ",\"bytes\":" + report.summary.totalSize + "}");
            Debug.Log("EVOLUTION_WINDOWS_BUILD " + report.summary.result);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        static void Guard()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!string.Equals(root, "C:/dev/DumbVideoGameStudio/EvolutionShop", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Refusing wrong project: " + root);
            Debug.Log("EVOLUTION_PROJECT_IDENTITY " + root + " Unity=" + Application.unityVersion);
        }
    }
}


