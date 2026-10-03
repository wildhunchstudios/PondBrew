using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace EvolutionShop.Editor
{
    [InitializeOnLoad]
    public static class Checkpoint00
    {
        const string Key = "EvolutionShop.Checkpoint00";
        static double began;
        static Checkpoint00() { EditorApplication.update += Update; }
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!string.Equals(root, "C:/dev/DumbVideoGameStudio/EvolutionShop", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Wrong project: " + root);
            Directory.CreateDirectory("../Docs/Evidence");
            Directory.CreateDirectory("Assets/EvolutionShop/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 3, -7);
            camera.transform.LookAt(new Vector3(0, 0.7f, 0));
            camera.backgroundColor = new Color(0.035f, 0.12f, 0.16f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(45, -25, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Base room floor";
            floor.transform.localScale = new Vector3(8, 0.2f, 8);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Checkpoint 00 culture placeholder";
            marker.transform.position = Vector3.up;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.25f, 0.8f, 0.65f);
            AssetDatabase.CreateAsset(mat, "Assets/EvolutionShop/CheckpointMaterial.mat");
            marker.GetComponent<Renderer>().sharedMaterial = mat;
            EditorSceneManager.SaveScene(scene, "Assets/EvolutionShop/Scenes/Shop.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/EvolutionShop/Scenes/Shop.unity", true) };
            Debug.Log("EVOLUTION_PROJECT_IDENTITY " + root + " Unity=" + Application.unityVersion);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        static void Update()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (began == 0) began = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - began < 3) return;
            SessionState.SetBool(Key, false);
            try
            {
                var cam = Camera.main;
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("../Docs/Evidence/checkpoint00-playmode.png", image.EncodeToPNG());
                cam.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(rt);
                File.WriteAllText("../Docs/Evidence/checkpoint00-result.json", "{\"passed\":true,\"playing\":true,\"projectPath\":\"C:/dev/DumbVideoGameStudio/EvolutionShop\",\"editorVersion\":\"" + Application.unityVersion + "\",\"scene\":\"" + SceneManager.GetActiveScene().path + "\"}");
                Debug.Log("CHECKPOINT00_PLAYMODE_PASSED");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
