using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
            RequireEditMode();
            bool exists = File.Exists(ScenePath);
            if (Application.isBatchMode)
            {
                RejectDirtyOpenScenes();
                if (exists) InspectSceneAsset(existingScene =>
                {
                    ValidatePlayableScene(existingScene);
                    if (!IsBootstrapOnlyScene(existingScene))
                        throw new InvalidOperationException("Refusing to regenerate authored Shop.unity in batch mode. Build/RunSmoke preserve the existing scene; use the explicit rebuild menu to replace it.");
                });
            }
            else
            {
                if (exists && !EditorUtility.DisplayDialog("Rebuild the generated shop?",
                    "This explicitly replaces Shop.unity with the procedural bootstrap and resets generated build-support materials/settings. Authored objects in that scene will be removed. Build and smoke validation do not need a rebuild.",
                    "Rebuild", "Cancel")) return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }
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
        /// <summary>Read-only validation for an existing scene; generation occurs only when absent.</summary>
        public static void EnsurePlayableScene()
        {
            Guard();
            RequireEditMode();
            RejectDirtyOpenScenes();
            if (!File.Exists(ScenePath)) CreateScene();
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Playable scene creation was cancelled or did not finish.");
            InspectSceneAsset(ValidatePlayableScene);
        }

        // Public editor-only validators are also exercised against temporary in-memory test scenes.
        public static void ValidatePlayableScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("The playable scene must be loaded for validation.");
            int sessions = 0;
            ShopSession found = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0)
                        throw new InvalidOperationException("Missing script in playable scene on: " + node.name);
                foreach (var session in root.GetComponentsInChildren<ShopSession>(true))
                { sessions++; found = session; }
            }
            if (sessions != 1)
                throw new InvalidOperationException("Playable scene needs exactly one ShopSession; found " + sessions + ". Authored content was not changed.");
            if (!found.enabled || !found.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The scene's ShopSession must be enabled and active.");
        }

        public static bool IsBootstrapOnlyScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;
            var roots = scene.GetRootGameObjects();
            if (roots.Length != 1) return false;
            var root = roots[0];
            var session = root.GetComponent<ShopSession>();
            return root.name == "Shop session" && root.activeSelf && session && session.enabled &&
                root.transform.childCount == 0 && root.GetComponents<Component>().Length == 2 &&
                root.transform.position == Vector3.zero && root.transform.rotation == Quaternion.identity &&
                root.transform.localScale == Vector3.one;
        }

        static void InspectSceneAsset(Action<Scene> inspect)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                inspect(scene);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Scene generation, validation, and builds require stopped Play mode.");
        }

        static void RejectDirtyOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                    throw new InvalidOperationException("Unsaved scene changes are preserved. Save or close this scene before validation/build/regeneration: " + scene.name);
            }
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
            EnsurePlayableScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetFloat("EvolutionShop.SmokeRequested",(float)EditorApplication.timeSinceStartup); SessionState.SetBool("EvolutionShop.SmokePending",true);
        }
        public static void BuildWindows()
        {
            EnsurePlayableScene();
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


