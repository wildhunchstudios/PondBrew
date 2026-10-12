#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        string vocabularyCaption;
        void OnGUI()
        {
            if (string.IsNullOrEmpty(vocabularyCaption)) return;
            GUI.Box(new Rect(Screen.width / 2 - 270, 70, 540, 48), "FX-06R-01 / PARTICLE VOCABULARY FIXTURE\n" + vocabularyCaption);
        }
        [Serializable] sealed class VocabularyRaster
        {
            public string view, method = "Actual additive floating-point particle raster; alpha > .01; excludes opaque and HUD occlusion (conservative).";
            public int width, height, activeParticles, coveredPixels, peakOverlap;
            public double coveragePercent, fragmentSum, averageFullScreenLayers, meanCoveredLayers;
        }
        [Serializable] sealed class VocabularyTiming
        {
            public string mode, gpu, cpu, unity, graphicsApi, pipeline, quality;
            public int width, height, targetFrameRate, vsync, frames, focusedFrames, peakParticles;
            public bool continuouslyFocused;
            public FxInventory before, after;
            public FxMetric[] metrics;
        }
        IEnumerator FxVocabularyScenario()
        {
            limitations.Add("R-01 diagnostic vocabulary only, not a new hatch/dose/collection: frozen isolated game authority, real world/camera/HUD with scripted cosmetic samples. Timing compares 96/32/0-particle stress fixtures, not the completed hero or a minimum PC. Raster excludes opaque/HUD depth and is a conservative transparent particle footprint. No native controls or listening claim.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Vocabulary uses isolated save only");
            yield return new WaitForSeconds(1); Check(shop.SkipTutorial(), "Explicitly skip Welcome in isolated vocabulary fixture");
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false; shop.AllowValidationInput = false;
            shop.SetFxFocusForValidation(true); shop.SetPanel(ShopPanel.None);
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Screen.SetResolution(width == 1280 ? 1920 : 1280, height == 720 ? 1080 : 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.3f);
                Screen.SetResolution(width, height, FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows HUD surface");
            }
            Check(shop.SaveNow(), "Save pre-fixture isolated authority");
            var world = shop.World; var pool = world.FxPool; var cues = world.FxCues;
            Check(pool.VocabularyReady && pool.OwnedMeshCount == 5 && pool.OwnedMaterialCount == 1, "Five original shared meshes and one supported shared URP material");
            Check(pool.Root.childCount == 96 && pool.Root.GetComponentsInChildren<Renderer>(true).Length == 96 && pool.Root.GetComponentsInChildren<Collider>(true).Length == 0, "96 fixed renderers and no collision objects");
            int transforms = world.GetComponentsInChildren<Transform>(true).Length, renderers = world.GetComponentsInChildren<Renderer>(true).Length;
            shop.FreezeFxVocabularyForValidation = true; shop.Player.InputEnabled = false; // Freeze simulation only; keep actual HUD/focus queries alive.
            string authority = FxAuthority(); byte[] saved = File.ReadAllBytes(shop.SavePath);
            var inc = StationOf(StationKind.Incubator).transform;
            var tank = StationOf(StationKind.TankA).transform;
            var incOwner = cues.Find("incubator"); var tankOwner = cues.Find("tank-a");
            try
            {
                GateAim(inc.TransformPoint(new Vector3(0, .05f, -2.4f)), inc.TransformPoint(new Vector3(0, 1.55f, 0)));
                vocabularyCaption = "Lab operator view / before sample"; yield return GateCapture("r01-lab-before");
                VocabularyShapes(pool, cues, incOwner, inc); pool.Tick(cues, .18f);
                vocabularyCaption = "Bubble / droplet / puff / spark / ring (left to right)"; yield return GateCapture("r01-lab-shapes");
                VocabularyRasterAudit("lab-shapes"); pool.CancelAll();
                VocabularyPaths(pool, cues, incOwner, inc); pool.Tick(cues, .36f);
                vocabularyCaption = "Curved coral stream and spiraling turquoise stream"; yield return GateCapture("r01-lab-paths");
                VocabularyRasterAudit("lab-paths"); pool.CancelAll();
                for (int i = 0; i < 5; i++)
                {
                    VocabularyPaths(pool, cues, incOwner, inc); pool.Tick(cues, .08f + i * .23f);
                    vocabularyCaption = "Technical path sample / elapsed seconds " + (.08f + i * .23f).ToString("F2", CultureInfo.InvariantCulture);
                    yield return GateCapture("r01-path-phase-" + i); pool.CancelAll();
                }
                VocabularyPressure(pool, cues, incOwner, inc);
                Check(pool.ActiveCount == 96, "32 ambient plus 48 hero/16 supporting action particles fit fixed Standard pool");
                var p = FxParticleRequest.Create(FxParticleShape.Bubble, Color.cyan);
                Check(!pool.EmitParticle(cues, incOwner, inc, Vector3.zero, p, false) && !pool.EmitParticle(cues, incOwner, inc, Vector3.zero, p), "Full partitions drop excess without allocation");
                pool.Tick(cues, .2f); vocabularyCaption = "Standard pressure / 32 ambient + 64 actions"; yield return GateCapture("r01-lab-pressure"); VocabularyRasterAudit("lab-pressure");
                pool.SetLowDetail(true); Check(pool.ActiveCount == 0, "Detail switch cancels previous paths");
                VocabularyPressure(pool, cues, incOwner, inc); Check(pool.ActiveCount == 32, "Low retains 24 actions with 8 ambient");
                pool.Tick(cues, .2f); vocabularyCaption = "Low pressure / 8 ambient + 24 actions"; yield return GateCapture("r01-lab-low"); VocabularyRasterAudit("lab-low");
                world.SetFxPreviewModes(true, true); VocabularyPaths(pool, cues, incOwner, inc); pool.Tick(cues, .18f);
                vocabularyCaption = "Reduced / stationary small local shapes, no swirl or expansion"; yield return GateCapture("r01-lab-reduced"); pool.CancelAll(); world.SetFxPreviewModes(false, false);
                GateAim(tank.TransformPoint(new Vector3(0, .05f, -2.4f)), tank.TransformPoint(new Vector3(0, 1.55f, 0)));
                vocabularyCaption = "Aquarium operator / before sample"; yield return GateCapture("r01-tank-before");
                VocabularyShapes(pool, cues, tankOwner, tank); pool.Tick(cues, .18f);
                vocabularyCaption = "Aquarium palette readability / shapes only, no new habitat"; yield return GateCapture("r01-tank-shapes"); VocabularyRasterAudit("tank-shapes"); pool.CancelAll();
                VocabularyPressure(pool, cues, tankOwner, tank); pool.Tick(cues, .2f); vocabularyCaption = "Aquarium worst-density vocabulary stress";
                yield return GateCapture("r01-tank-pressure"); VocabularyRasterAudit("tank-pressure"); pool.CancelAll();
                GateAim(new Vector3(-1.4f, .05f, -4.65f), new Vector3(4.9f, 1.55f, -.7f));
                VocabularyPressure(pool, cues, tankOwner, tank); pool.Tick(cues, .2f); vocabularyCaption = "Store aisle / particle stress with real HUD";
                yield return GateCapture("r01-aisle-pressure"); VocabularyRasterAudit("aisle-pressure"); pool.CancelAll();
                GateAim(new Vector3(0, .05f, 1.28f), new Vector3(1f, 1.55f, 3.85f));
                VocabularyPressure(pool, cues, incOwner, inc); pool.Tick(cues, .2f); vocabularyCaption = "Lab overview / particle stress with real HUD";
                yield return GateCapture("r01-overview-pressure"); VocabularyRasterAudit("overview-pressure"); pool.CancelAll();
                // Shared world owners exercise the existing interruption path with rich particles.
                foreach (FxViewPolicy policy in new[] { FxViewPolicy.Modal, FxViewPolicy.Paused, FxViewPolicy.Unfocused, FxViewPolicy.Arrange })
                {
                    VocabularyPaths(pool, cues, incOwner, inc); world.SetFxViewPolicy(policy);
                    Check(pool.ActiveCount == 0, "Rich paths canceled by existing view policy / " + policy); world.SetFxViewPolicy(FxViewPolicy.Visible);
                }
                VocabularyPaths(pool, cues, incOwner, inc); world.enabled = false; Check(pool.ActiveCount == 0, "World disable cancels rich particles"); world.enabled = true;
                for (int i = 0; i < 100; i++) { VocabularyPaths(pool, cues, incOwner, inc); pool.Tick(cues, 1.51f); }
                Check(pool.ActiveCount == 0 && pool.OwnedCount == 96 && world.GetComponentsInChildren<Transform>(true).Length == transforms && world.GetComponentsInChildren<Renderer>(true).Length == renderers, "100 rich reuses keep exact topology and return all particles");
                Check(FxAuthority() == authority && System.Linq.Enumerable.SequenceEqual(saved, File.ReadAllBytes(shop.SavePath)), "All shape/path/pressure/policy fixtures preserve exact authority and durable isolated save");
                if (!Application.isEditor)
                {
                    GateAim(inc.TransformPoint(new Vector3(0, .05f, -2.4f)), inc.TransformPoint(new Vector3(0, 1.55f, 0)));
                    yield return VocabularyMeasure("off-before", incOwner, inc);
                    yield return VocabularyMeasure("standard", incOwner, inc);
                    yield return VocabularyMeasure("low", incOwner, inc);
                    yield return VocabularyMeasure("off-after", incOwner, inc);
                }
                else limitations.Add("Editor route records lifecycle only; standalone provides target HUD, raster and timing results.");
                Check(FxAuthority() == authority, "Timing stress changes no gameplay authority");
            }
            finally { pool.CancelAll(); world.SetFxPreviewModes(false, false); vocabularyCaption = null; shop.FreezeFxVocabularyForValidation = false; }
            world.ResetFxPresentation(); Check(pool.ActiveCount == 0 && shop.SaveNow() && shop.ReloadSaved(), "Reset/save/reload reconstructs quiet state without rich replay");
            shop.SetPanel(ShopPanel.None); Check(shop.World.FxPool.ActiveCount == 0 && shop.World.FxPool.OwnedCount == 96, "Rebuilt pool stays quiet after reload");
        }
        void VocabularyShapes(FxEffectPool pool, FxCueRuntime cues, FxOwnerToken owner, Transform source)
        {
            for (int i = 0; i < 5; i++)
            {
                var p = FxParticleRequest.Create((FxParticleShape)i, VocabularyColor(i)); p.Size = .24f; p.EndSize = .24f; p.Lifetime = 1.5f;
                Check(pool.EmitParticle(cues, owner, source, new Vector3((i - 2) * .32f, 1.85f, -.8f), p), "Shared shape admitted / " + p.Shape);
            }
        }
        static Color VocabularyColor(int i) => i % 3 == 0 ? new Color(.2f, .95f, 1, .95f) : i % 3 == 1 ? new Color(1, .4f, .32f, .95f) : new Color(1, .84f, .34f, .85f);
        void VocabularyPaths(FxEffectPool pool, FxCueRuntime cues, FxOwnerToken owner, Transform source)
        {
            for (int i = 0; i < 18; i++)
            {
                var p = FxParticleRequest.Create(i % 2 == 0 ? FxParticleShape.Bubble : FxParticleShape.Droplet, VocabularyColor(i % 2));
                p.Motion = i < 9 ? FxParticleMotion.Curve : FxParticleMotion.Spiral;
                p.End = new Vector3(.55f, .35f, 0); p.Bend = new Vector3(0, .18f, -.1f); p.Radius = .10f; p.Turns = 1.5f;
                p.Size = .1f; p.EndSize = .035f; p.Lifetime = 1.5f; p.Spin = 90;
                pool.EmitParticle(cues, owner, source, new Vector3(-.55f + i % 9 * .045f, 1.5f + (i < 9 ? 0 : .30f), -.90f), p);
            }
        }
        void VocabularyPressure(FxEffectPool pool, FxCueRuntime cues, FxOwnerToken owner, Transform source)
        {
            for (int i = 0; i < pool.AmbientLimit + pool.ActionLimit; i++)
            {
                var p = FxParticleRequest.Create((FxParticleShape)(i % 5), VocabularyColor(i));
                p.Lifetime = 1.2f; p.Size = .10f + i % 3 * .035f; p.EndSize = p.Size * 1.1f;
                p.Velocity = new Vector3((i % 5 - 2) * .07f, .04f, 0);
                pool.EmitParticle(cues, owner, source, new Vector3((i % 12 - 5.5f) * .055f, 1.55f + i % 8 * .055f, -.80f), p, i >= pool.AmbientLimit);
            }
        }
        void VocabularyRasterAudit(string view)
        {
            if (Application.isEditor) return;
            var camera = shop.Player.View; int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat); target.Create();
            var buffer = new CommandBuffer { name = "R01 conservative particle overlap audit" }; var prior = RenderTexture.active;
            var read = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            try
            {
                buffer.SetRenderTarget(target); buffer.ClearRenderTarget(false, true, Color.clear);
                buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
                var renderers = shop.World.FxPool.Root.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                    if (renderer.sharedMaterial && renderer.sharedMaterial.shader.name == "EvolutionShop/Cartoon Particle") buffer.DrawRenderer(renderer, renderer.sharedMaterial, 0, 1);
                Graphics.ExecuteCommandBuffer(buffer); RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, width, height), 0, 0); read.Apply();
                double sum = 0; int pixels = 0, peak = 0;
                foreach (var pixel in read.GetPixels()) { int layers = Mathf.RoundToInt(pixel.r); sum += layers; if (layers > 0) pixels++; peak = Mathf.Max(peak, layers); }
                var result = new VocabularyRaster { view = view, width = width, height = height, activeParticles = shop.World.FxPool.ActiveCount,
                    coveredPixels = pixels, peakOverlap = peak, coveragePercent = 100.0 * pixels / (width * height), fragmentSum = sum,
                    averageFullScreenLayers = sum / (width * height), meanCoveredLayers = pixels > 0 ? sum / pixels : 0 };
                File.WriteAllText(Path.Combine(evidence, view + "-raster.json"), JsonUtility.ToJson(result, true));
                // Heat map: 1 layer=cyan,4=yellow,8+=coral; diagnostic pixels, not a game render.
                var heat = read.GetPixels(); for (int i = 0; i < heat.Length; i++) heat[i] = heat[i].r <= 0 ? Color.black : Color.Lerp(Color.cyan, Color.red, Mathf.Clamp01((heat[i].r - 1) / 7));
                var upright = new Color[heat.Length]; for (int y = 0; y < height; y++) Array.Copy(heat, y * width, upright, (height - 1 - y) * width, width);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.SetPixels(upright); image.Apply();
                File.WriteAllBytes(Path.Combine(evidence, view + "-particle-overlap.png"), image.EncodeToPNG()); Destroy(image);
                Check(pixels > 0 && result.averageFullScreenLayers < .15 && peak <= 32, "Measured raster within R01 conservative stress envelope / " + view + " coverage=" + result.coveragePercent.ToString("F3") + "% peak=" + peak);
            }
            finally { RenderTexture.active = prior; buffer.Release(); target.Release(); Destroy(target); Destroy(read); }
        }
        IEnumerator VocabularyMeasure(string mode, FxOwnerToken owner, Transform source)
        {
            var pool = shop.World.FxPool; pool.CancelAll(); pool.SetLowDetail(mode == "low");
            vocabularyCaption = "8-second fixed-camera cost window / " + mode;
            yield return new WaitForSeconds(1);
            var rows = new FxFrame[8192]; var focus = new bool[8192]; var timing = new FrameTiming[1]; int count = 0, focused = 0, peak = 0;
            using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            using (var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1))
            using (var alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                var before = FxCount(); double origin = Time.realtimeSinceStartupAsDouble, prior = origin;
                while (Time.realtimeSinceStartupAsDouble - origin < 8 && count < rows.Length)
                {
                    if (!mode.StartsWith("off") && pool.ActiveCount == 0) VocabularyPressure(pool, shop.World.FxCues, owner, source);
                    pool.Tick(shop.World.FxCues, Time.deltaTime); peak = Mathf.Max(peak, pool.ActiveCount);
                    FrameTimingManager.CaptureFrameTimings(); yield return null;
                    double now = Time.realtimeSinceStartupAsDouble; uint n = FrameTimingManager.GetLatestTimings(1, timing);
                    focus[count] = Application.isFocused; if (focus[count]) focused++;
                    rows[count++] = new FxFrame { interval = (now - prior) * 1000, main = main.Valid && main.Count > 0 ? main.LastValue / 1000000.0 : -1,
                        render = render.Valid && render.Count > 0 ? render.LastValue / 1000000.0 : -1, gpu = n > 0 && timing[0].gpuFrameTime > 0 ? timing[0].gpuFrameTime : -1,
                        alloc = alloc.Valid && alloc.Count > 0 ? alloc.LastValue : -1 }; prior = now;
                }
                var report = new VocabularyTiming { mode = mode, width = Screen.width, height = Screen.height, frames = count, focusedFrames = focused, continuouslyFocused = count == focused,
                    targetFrameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount, gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                    unity = Application.unityVersion, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), pipeline = GraphicsSettings.currentRenderPipeline.name,
                    quality = QualitySettings.names[QualitySettings.GetQualityLevel()], peakParticles = peak, before = before, after = FxCount(), metrics = new[] {
                    FxStats(rows, count, "frameInterval", "ms", r => r.interval), FxStats(rows, count, "mainThreadIncludingWaits", "ms", r => r.main),
                    FxStats(rows, count, "renderThread", "ms", r => r.render), FxStats(rows, count, "gpuFrame", "ms", r => r.gpu), FxStats(rows, count, "gcAllocatedPerFrame", "bytes", r => r.alloc) } };
                File.WriteAllText(Path.Combine(evidence, mode + "-vocabulary-metrics.json"), JsonUtility.ToJson(report, true));
                var csv = new StringBuilder("frame,intervalMs,mainMs,renderMs,gpuMs,gcBytes,focused\n");
                for (int i = 0; i < count; i++) csv.Append(i).Append(',').Append(rows[i].interval.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(rows[i].main.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                    .Append(rows[i].render.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(rows[i].gpu.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(rows[i].alloc).Append(',').Append(focus[i] ? 1 : 0).Append('\n');
                File.WriteAllText(Path.Combine(evidence, mode + "-vocabulary-frames.csv"), csv.ToString());
                Check(count >= 60 && peak == (mode.StartsWith("off") ? 0 : mode == "low" ? 32 : 96), "Actual live cost window / " + mode + " frames=" + count);
                Check(report.before.worldRenderers == report.after.worldRenderers && report.before.worldTransforms == report.after.worldTransforms && report.before.loadedMeshes == report.after.loadedMeshes && report.before.loadedMaterials == report.after.loadedMaterials, "Cost window retains warmed resource inventory / " + mode);
            }
            pool.CancelAll();
        }
    }
}
#endif
