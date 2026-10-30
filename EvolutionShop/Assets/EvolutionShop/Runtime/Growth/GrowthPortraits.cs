using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// GR-27: a creature's portrait for the interface: its own model (the kit's, in its colour) drawn by a camera of its own in
    /// a studio far below the rooms, turned three quarters to the viewer, on a clear background, into a texture. Each is drawn
    /// once and kept. The studio's camera and light see only the studio's layer, so nothing of the shop is in a portrait.
    /// </summary>
    public static class GrowthPortraits
    {
        /// <summary>The studio's layer (unnamed in the project; nothing else uses it).</summary>
        public const int Layer = 30;
        static readonly Vector3 StudioAt = new Vector3(0, -400, 0);
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static Transform studio;
        static Camera camera;
        static Light keyLight, fillLight;

        /// <summary>Validation: how many portraits have been drawn.</summary>
        public static int Drawn { get; private set; }

        /// <summary>A creature's portrait, square, size pixels (null for a species with no model).</summary>
        public static Texture2D Get(string species, Colour colour, int size = 256)
        {
            string key = species + "/" + colour + "/" + size;
            if (cache.TryGetValue(key, out var kept) && kept != null) return kept;
            if (GrowthContent.Get(species) == null) return null;
            Studio();
            var view = GrowthCreatureView.Create(new GrowthSpecimen { Id = 990000 + Drawn, Species = species, Colour = colour }, studio);
            // Three quarters to the viewer, a little from above: the creature swims along its +Z, the camera looks along +Z.
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.Euler(-6, 215, 0);
            foreach (var t in view.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            Bounds bounds = default; bool any = false;
            foreach (var r in view.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("Reserved tag")) { r.enabled = false; continue; }
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (!any) { Object.Destroy(view.gameObject); return null; }
            // Framed on what the camera sees (it looks along +Z): the larger of the width and height, with a little room.
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.14f;
            float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) + bounds.extents.z;
            camera.transform.position = bounds.center - Vector3.forward * distance;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            camera.nearClipPlane = Mathf.Max(0.001f, distance - bounds.extents.magnitude * 2);
            camera.farClipPlane = distance + bounds.extents.magnitude * 2;

            // Drawn at twice the size, then cropped to the creature's own pixels with a little room (a model's bounds can reach
            // well past what shows), into a square of size pixels.
            int big = size * 2;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(big, big, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
            // The studio's lights are on only while the portrait is drawn (URP lights every layer), so they never light the shop.
            keyLight.enabled = fillLight.enabled = true;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else { camera.targetTexture = rt; camera.Render(); camera.targetTexture = null; }
            keyLight.enabled = fillLight.enabled = false;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var drawn = new Texture2D(big, big, TextureFormat.RGBA32, false);
            drawn.ReadPixels(new Rect(0, 0, big, big), 0, 0);
            drawn.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            view.gameObject.SetActive(false);
            Object.Destroy(view.gameObject);
            var texture = Crop(drawn, size);
            texture.name = "GR-27 portrait " + key;
            Object.Destroy(drawn);
            cache[key] = texture;
            Drawn++;
            return texture;
        }

        /// <summary>The square around a drawing's own pixels (with a twelfth more room on each side), scaled to size pixels.</summary>
        static Texture2D Crop(Texture2D drawn, int size)
        {
            int w = drawn.width, h = drawn.height;
            var pixels = drawn.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (pixels[y * w + x].a > 16) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            if (maxX < 0) { result.SetPixels32(new Color32[size * size]); result.Apply(false, false); return result; }
            float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * (1 + 2f / 12f);
            float cx = (minX + maxX + 1) * 0.5f, cy = (minY + maxY + 1) * 0.5f;
            var output = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Sampled with bilinear filtering from the drawing, clear outside it.
                    float u = (cx - side * 0.5f + (x + 0.5f) * side / size) / w, v = (cy - side * 0.5f + (y + 0.5f) * side / size) / h;
                    output[y * size + x] = u < 0 || u > 1 || v < 0 || v > 1 ? new Color32(0, 0, 0, 0) : (Color32)drawn.GetPixelBilinear(u, v);
                }
            result.SetPixels32(output);
            result.Apply(false, false);
            return result;
        }

        /// <summary>Validation: the share of a portrait's pixels the creature covers (not clear).</summary>
        public static float Coverage(Texture2D t)
        {
            if (t == null) return 0;
            var pixels = t.GetPixels32();
            int covered = 0;
            foreach (var p in pixels) if (p.a > 32) covered++;
            return covered / (float)pixels.Length;
        }

        static void Studio()
        {
            if (studio != null) return;
            studio = new GameObject("GR-27 portrait studio").transform;
            studio.position = StudioAt;
            Object.DontDestroyOnLoad(studio.gameObject);
            camera = new GameObject("Portrait camera").AddComponent<Camera>();
            camera.transform.SetParent(studio, false);
            camera.enabled = false;
            camera.fieldOfView = 22;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.cullingMask = 1 << Layer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            // A warm key from above and in front, a cool fill from the other side: the studio's own, for its layer only.
            keyLight = new GameObject("Key light").AddComponent<Light>();
            keyLight.transform.SetParent(studio, false);
            keyLight.type = LightType.Directional; keyLight.intensity = 1.25f; keyLight.color = new Color(1f, 0.96f, 0.88f);
            keyLight.transform.rotation = Quaternion.Euler(40, 30, 0);
            keyLight.cullingMask = 1 << Layer; keyLight.shadows = LightShadows.None;
            fillLight = new GameObject("Fill light").AddComponent<Light>();
            fillLight.transform.SetParent(studio, false);
            fillLight.type = LightType.Directional; fillLight.intensity = 0.45f; fillLight.color = new Color(0.78f, 0.88f, 1f);
            fillLight.transform.rotation = Quaternion.Euler(20, -140, 0);
            fillLight.cullingMask = 1 << Layer; fillLight.shadows = LightShadows.None;
            keyLight.enabled = fillLight.enabled = false;
        }
    }
}
