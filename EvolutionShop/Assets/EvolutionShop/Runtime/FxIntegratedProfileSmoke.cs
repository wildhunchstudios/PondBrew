#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public bool FxIntegratedProfileForValidation { get; set; }
    }
    public sealed partial class MilestoneSmoke
    {
        string integratedHero;
        Station integratedCollectionStation;
        FxInventory integratedFirstInventory;
        FxDoseMix integratedDoseMix;
        int integratedAcceptedHeroCues;
        const int IntegratedWarmFrames = 240, IntegratedFrames = 960;
        const float IntegratedStep = 1f / 120;
        [Serializable] sealed class IntegratedWindow
        {
            public string schema = "fx06r07-integrated-v1";
            public string method = "Frozen gameplay; production ShopWorld.Animate called once per rendered frame at fixed 1/120 s. 240 warm + 960 measured frames. Synthetic deterministic steady-state replay, not native play or an old-build comparison. Off disables current FX only; inherited presentation remains. Direct CPU/allocation scope is all ShopWorld.Animate, not only newly authored FX. GPU samples may be delayed; repeated timestamps are excluded. No raster/capture/I/O inside measured windows.";
            public string scene, fixture, phase, view, mode, windowStartUtc, windowEndUtc, authoritySha256, heroReplay, incubationPhase;
            public double incubationSecondsRemaining;
            public string unity, gpu, cpu, graphicsApi, pipeline, quality;
            public int width, height, cycle, window, frames, warmFrames, focusedFrames, freshGpuFrames, peakParticles;
            public int spawnedParticles, droppedParticles, ownedParticles, ownedParticleMeshes, ownedParticleMaterials, targetFrameRate, vsync, heroActivityCount;
            public bool continuouslyFocused, frameTimingEnabled, authorityUnchanged, saveUnchanged, resourcesStable, crossWindowResourcesStable;
            public double presentationSeconds, wallSeconds;
            public FxInventory before, after;
            public FxMetric[] metrics;
        }
        struct IntegratedFrame
        {
            public long utcUnixMs;
            public int renderedFrame, particles, spawned, dropped;
            public bool focused, freshGpu;
            public ulong gpuTimestamp;
            public double interval, main, render, gpu, alloc, presentationCpu, presentationAlloc;
            public double batches, drawCalls, setPass, triangles, vertices;
        }
        static string IntegratedHash(byte[] bytes)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        IEnumerator FxIntegratedViews(string fixture, string phase)
        {
            string selected = ShopSession.Arg("-evolutionFxProfileScene");
            string cycleArg = ShopSession.Arg("-evolutionFxProfileCycles");
            int cycles = cycleArg == null ? 2 : int.Parse(cycleArg, CultureInfo.InvariantCulture);
            Check(cycles == 1 || cycles == 2, "Integrated profile bounded to one or two bracket cycles");
            if (selected != null)
                Check(Array.IndexOf(new[] { "ordinary-night-tanks", "ordinary-night-lab", "ordinary-day-tanks", "ordinary-day-lab", "busy-night-tanks", "busy-night-lab", "busy-day-tanks", "busy-day-lab", "hero-night-collection", "hero-night-dose", "hero-night-growth", "hero-night-hatch" }, selected) >= 0,
                    "Integrated profile scene filter is explicit and supported");
            if (selected != null && !selected.StartsWith(fixture + "-" + phase + "-", StringComparison.Ordinal)) yield break;
            limitations.Add("R07 integrated windows freeze model, workers, customers and time. One production presentation tick/frame uses a fixed 1/120 s step; actual wall duration is separately recorded. Steady ordinary/busy composition is measured, not a live hero transaction. Historical baseline route and real hero routes remain separate evidence. Continuous Unity focus is not compositor overlap proof; external timestamped samples determine available overlap/clock evidence. Raster covers only pooled cartoon particles, ignores opaque/HUD occlusion and does not measure whole-scene transparency cost.");
            int width = int.TryParse(ShopSession.Arg("-screen-width"), out int w) ? w : Screen.width;
            int height = int.TryParse(ShopSession.Arg("-screen-height"), out int h) ? h : Screen.height;
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false;
            shop.FreezeFxVocabularyForValidation = true;
            shop.FxIntegratedProfileForValidation = true;
            try
            {
                Screen.SetResolution(width == 1280 ? 1920 : 1280, height == 720 ? 1080 : 720, FullScreenMode.Windowed);
                yield return new WaitForSeconds(.3f);
                Screen.SetResolution(width, height, FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
                Check(Screen.width == width && Screen.height == height && Application.targetFrameRate == 120,
                    "Integrated replay has requested surface and existing 120 FPS target");
                for (int v = 0; v < 2; v++)
                {
                    string view = v == 0 ? "tanks" : "lab", scene = fixture + "-" + phase + "-" + view;
                    if (selected != null && selected != scene) continue;
                    integratedFirstInventory=null;
                    GateAim(v == 0 ? new Vector3(-1.4f, .05f, -4.65f) : new Vector3(1f, .05f, 1.28f),
                        v == 0 ? new Vector3(4.9f, 1.55f, -.7f) : new Vector3(-1.5f, 1.5f, 4.45f));
                    Physics.SyncTransforms(); yield return null; yield return null;
                    for (int cycle = 0; cycle < cycles; cycle++)
                    {
                        string[] modes = cycle == 0 ? new[] { "off-before", "standard", "low", "off-after" } : new[] { "off-before", "low", "standard", "off-after" };
                        for (int window = 0; window < modes.Length; window++)
                            yield return FxIntegratedMeasure(fixture, phase, view, cycle, window, modes[window]);
                    }
                    // Expensive readback/capture is kept out of every timing bracket.
                    foreach (string mode in new[] { "standard", "low" })
                    {
                        shop.World.ResetFxProfileForValidation(true, mode == "low");
                        for (int frame = 0; frame < 360; frame++)
                        {
                            shop.World.Animate((frame + 1) * IntegratedStep, IntegratedStep);
                            if (frame == 119 || frame == 239 || frame == 359)
                                IntegratedRaster(scene + "-" + mode + "-phase-" + (frame + 1));
                        }
                        yield return GateCapture("integrated-" + scene + "-" + mode);
                    }
                }
            }
            finally
            {
                shop.World.ResetFxProfileForValidation(true, false);
                shop.FxIntegratedProfileForValidation = false;
                shop.FreezeFxVocabularyForValidation = false;
            }
        }
        IEnumerator FxIntegratedHeroes()
        {
            string selected = ShopSession.Arg("-evolutionFxProfileScene");
            if (selected != null && !selected.StartsWith("hero-", StringComparison.Ordinal)) yield break;
            limitations.Add("R07 hero cost stages: restore the previously persisted ordinary isolated fixture, collect and dose a real pair, begin normal incubation, sample frozen working state, then let the real countdown reach Ready. Only the presentation is replayed during measured brackets. Collection intake carries no newly committed units; dose replays the saved real mixture; hatch alternates first/repeat choreography on one actual Ready specimen. No audio playback is replayed or measured as listening. Each hero has one bounded off/Standard/Low/off bracket; no repeated-hero causal claim.");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(Path.Combine(evidence,"ordinary-fixture.json")));
            Check(SaveStore.Write(shop.SavePath,fixture,out var error) && shop.ReloadSaved(), "Restore isolated ordinary fixture for hero cost / " + error);
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput=false;
            shop.Customers.ArrivalsEnabled=false; shop.AmbientComedyEnabled=false;
            CollectionAim(CultureKind.Bloom); yield return null; yield return null;
            integratedCollectionStation = StationOf(StationKind.Bloom);
            Check(shop.Focused == integratedCollectionStation && shop.Model.GetGrower(CultureKind.Bloom).Units > 0 && shop.Model.ReservedCultureCount < shop.Model.Capacity,
                "Collection cost has actual stocked source, physical focus and free chamber");
            if(selected == null || selected == "hero-night-collection") yield return IntegratedHeroBracket("collection");
            integratedCollectionStation = null;
            yield return FxLabHarvest(CultureKind.Bloom,false); yield return FxLabHarvest(CultureKind.Flow,false);
            DoseAim(); Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow) && shop.SaveNow(), "Hero cost uses actual guarded staged pair");
            if(selected == null || selected == "hero-night-dose") yield return IntegratedHeroBracket("dose");
            Check(shop.BeginManualIncubation(), "Start normal-duration hero profiling batch");
            shop.SetPanel(ShopPanel.None); yield return new WaitForSeconds(2);
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.SaveNow(), "Growth cost freezes actual working batch");
            if(selected == null || selected == "hero-night-growth") yield return IntegratedHeroBracket("growth");
            yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"natural Ready for hatch cost replay");
            Check(shop.SaveNow(), "Persist actual Ready output before cosmetic hatch replay");
            if(selected == null || selected == "hero-night-hatch") yield return IntegratedHeroBracket("hatch");
        }
        IEnumerator IntegratedHeroBracket(string hero)
        {
            integratedHero=hero; integratedFirstInventory=null; shop.FreezeFxVocabularyForValidation=true; shop.FxIntegratedProfileForValidation=true;
            integratedDoseMix=FxDoseMix.Read(shop.Model);
            try
            {
                if(hero != "collection") { DoseAim(); if(hero != "dose") shop.SetPanel(ShopPanel.None); }
                for(int window=0;window<4;window++)
                    yield return FxIntegratedMeasure("hero","night",hero,0,window,new[]{"off-before","standard","low","off-after"}[window]);
                foreach(string mode in new[]{"standard","low"})
                {
                    shop.World.ResetFxProfileForValidation(true,mode=="low");
                    for(int frame=0;frame<120;frame++)
                    {
                        IntegratedAdvance(shop.World,frame);
                        if(frame==29 || frame==59 || frame==119) IntegratedRaster("hero-night-"+hero+"-"+mode+"-phase-"+(frame+1));
                    }
                    yield return GateCapture("integrated-hero-night-"+hero+"-"+mode);
                }
            }
            finally
            {
                shop.World.ResetFxProfileForValidation(true,false); integratedHero=null;
                shop.FxIntegratedProfileForValidation=false; shop.FreezeFxVocabularyForValidation=false;
            }
        }
        void IntegratedAdvance(ShopWorld world,int frame)
        {
            // No commands, authority mutations or recorder work in this presentation replay.
            if(integratedHero == "collection") world.SetHarvesting(true,integratedCollectionStation);
            else if(integratedHero == "dose" && frame%90==0 && world.TryFxCue(FxCueId.DoseCommitted,(uint)(frame/90+1),FxCueOrigin.Manual,CultureKind.Bloom))
            { world.ConfigureDoseContext(integratedDoseMix,integratedDoseMix,FxDoseMode.Pour); integratedAcceptedHeroCues++; }
            else if(integratedHero == "hatch" && frame%240==0) world.TryFxCue(frame%480==0 ? FxCueId.HatchFirst : FxCueId.HatchRepeat,(uint)(frame/240+1));
            world.Animate((frame+1)*IntegratedStep,IntegratedStep);
        }
        int IntegratedHeroActivity(ShopWorld world) => integratedHero=="collection" ? world.CollectionEmissionCount : integratedHero=="hatch" ? world.LabHatchCount :
            integratedHero=="growth" ? world.GrowthAmbientEmissionCount : integratedHero=="dose" ? integratedAcceptedHeroCues : 0;
        static ProfilerRecorder IntegratedRecorder(ProfilerCategory category, string name)
        {
            try { return ProfilerRecorder.StartNew(category, name, 1); }
            catch (ArgumentException) { return default; }
        }
        static double IntegratedValue(ProfilerRecorder recorder, double scale = 1) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue / scale : -1;
        IEnumerator FxIntegratedMeasure(string fixture, string phase, string view, int cycle, int window, string mode)
        {
            string scene = fixture + "-" + phase + "-" + view;
            string name = scene + "-c" + cycle + "-w" + window + "-" + mode;
            var world = shop.World; var pool = world.FxPool;
            var rows = new IntegratedFrame[IntegratedFrames]; var timings = new FrameTiming[1];
            string authority = FxAuthority(); byte[] saved = File.ReadAllBytes(shop.SavePath);
            world.ResetFxProfileForValidation(!mode.StartsWith("off", StringComparison.Ordinal), mode == "low");
            using (var main = IntegratedRecorder(ProfilerCategory.Internal, "Main Thread"))
            using (var render = IntegratedRecorder(ProfilerCategory.Internal, "Render Thread"))
            using (var alloc = IntegratedRecorder(ProfilerCategory.Memory, "GC Allocated In Frame"))
            using (var batches = IntegratedRecorder(ProfilerCategory.Render, "Batches Count"))
            using (var draws = IntegratedRecorder(ProfilerCategory.Render, "Draw Calls Count"))
            using (var passes = IntegratedRecorder(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var triangles = IntegratedRecorder(ProfilerCategory.Render, "Triangles Count"))
            using (var vertices = IntegratedRecorder(ProfilerCategory.Render, "Vertices Count"))
            {
                for (int i = 0; i < IntegratedWarmFrames; i++)
                {
                    IntegratedAdvance(world,i);
                    FrameTimingManager.CaptureFrameTimings(); yield return null;
                }
                var before = FxCount();
                // Inventory allocates; flush it before sampling whole-game allocation.
                yield return null; yield return null;
                int focused = 0, peak = 0, freshGpu = 0, initialSpawn = pool.SpawnedCount, initialDrop = pool.DroppedCount;
                int initialActivity=IntegratedHeroActivity(world);
                ulong previousGpuTimestamp = 0;
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0) previousGpuTimestamp = timings[0].frameStartTimestamp;
                string startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                double started = Time.realtimeSinceStartupAsDouble, previous = started;
                for (int i = 0; i < rows.Length; i++)
                {
                    long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                    long cpuStart = Stopwatch.GetTimestamp();
                    IntegratedAdvance(world,IntegratedWarmFrames+i);
                    long cpuStop = Stopwatch.GetTimestamp();
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                    int active = pool.ActiveCount, spawned = pool.SpawnedCount - initialSpawn, dropped = pool.DroppedCount - initialDrop;
                    FrameTimingManager.CaptureFrameTimings(); yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    uint received = FrameTimingManager.GetLatestTimings(1, timings);
                    bool fresh = received > 0 && timings[0].frameStartTimestamp != 0 && timings[0].frameStartTimestamp != previousGpuTimestamp;
                    if (received > 0) previousGpuTimestamp = timings[0].frameStartTimestamp;
                    bool hasGpu = fresh && timings[0].gpuFrameTime > 0;
                    bool focus = Application.isFocused; if (focus) focused++; if (hasGpu) freshGpu++;
                    peak = Mathf.Max(peak, active);
                    rows[i] = new IntegratedFrame { utcUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), renderedFrame = Time.renderedFrameCount,
                        focused = focus, freshGpu = hasGpu, gpuTimestamp = previousGpuTimestamp,
                        interval = (now - previous) * 1000, main = IntegratedValue(main, 1000000), render = IntegratedValue(render, 1000000),
                        gpu = hasGpu ? timings[0].gpuFrameTime : -1, alloc = IntegratedValue(alloc),
                        presentationCpu = (cpuStop - cpuStart) * 1000.0 / Stopwatch.Frequency, presentationAlloc = allocated,
                        particles = active, spawned = spawned, dropped = dropped, batches = IntegratedValue(batches), drawCalls = IntegratedValue(draws),
                        setPass = IntegratedValue(passes), triangles = IntegratedValue(triangles), vertices = IntegratedValue(vertices) };
                    previous = now;
                }
                var report = new IntegratedWindow { scene = scene, fixture = fixture, phase = phase, view = view, mode = mode, cycle = cycle, window = window,
                    width = Screen.width, height = Screen.height, unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(), pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "none",
                    quality = QualitySettings.names[QualitySettings.GetQualityLevel()], targetFrameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount,
                    frames = rows.Length, warmFrames = IntegratedWarmFrames, focusedFrames = focused, continuouslyFocused = focused == rows.Length,
                    freshGpuFrames = freshGpu, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), peakParticles = peak,
                    spawnedParticles = pool.SpawnedCount - initialSpawn, droppedParticles = pool.DroppedCount - initialDrop,
                    ownedParticles = pool.OwnedCount, ownedParticleMeshes = pool.OwnedMeshCount, ownedParticleMaterials = pool.OwnedMaterialCount,
                    presentationSeconds = rows.Length * (double)IntegratedStep, wallSeconds = previous - started,
                    windowStartUtc = startedUtc, windowEndUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    authoritySha256 = IntegratedHash(Encoding.UTF8.GetBytes(authority)), authorityUnchanged = authority == FxAuthority(),
                    heroReplay = integratedHero ?? "none", incubationPhase = shop.Model.Incubator.Phase.ToString(), incubationSecondsRemaining = shop.Model.Incubator.SecondsRemaining,
                    heroActivityCount = IntegratedHeroActivity(world)-initialActivity,
                    saveUnchanged = IntegratedHash(saved) == IntegratedHash(File.ReadAllBytes(shop.SavePath)), before = before, after = FxCount(),
                    metrics = new[] {
                        IntegratedStats(rows,"frameInterval","ms",r=>r.interval), IntegratedStats(rows,"mainThreadIncludingWaits","ms",r=>r.main),
                        IntegratedStats(rows,"renderThread","ms",r=>r.render), IntegratedStats(rows,"gpuFrame","ms",r=>r.gpu),
                        IntegratedStats(rows,"gcAllocatedPerFrame","bytes",r=>r.alloc), IntegratedStats(rows,"presentationCpu","ms",r=>r.presentationCpu),
                        IntegratedStats(rows,"presentationAllocated","bytes",r=>r.presentationAlloc), IntegratedStats(rows,"batches","count",r=>r.batches),
                        IntegratedStats(rows,"drawCalls","count",r=>r.drawCalls), IntegratedStats(rows,"setPassCalls","count",r=>r.setPass),
                        IntegratedStats(rows,"triangles","count",r=>r.triangles), IntegratedStats(rows,"vertices","count",r=>r.vertices),
                        IntegratedStats(rows,"activeParticles","count",r=>r.particles) } };
                report.resourcesStable = IntegratedSameInventory(report.before,report.after);
                if(integratedFirstInventory==null) integratedFirstInventory=before;
                report.crossWindowResourcesStable=IntegratedSameInventory(integratedFirstInventory,before) && IntegratedSameInventory(integratedFirstInventory,report.after);
                File.WriteAllText(Path.Combine(evidence, name + "-integrated-metrics.json"), JsonUtility.ToJson(report, true));
                var csv = new StringBuilder("frame,utcUnixMs,renderedFrame,focused,gpuFresh,gpuTimestamp,intervalMs,mainMs,renderMs,gpuMs,gcBytes,presentationCpuMs,presentationAllocatedBytes,particles,spawned,dropped,batches,drawCalls,setPassCalls,triangles,vertices\n");
                for (int i = 0; i < rows.Length; i++)
                {
                    var r = rows[i]; csv.Append(i).Append(',').Append(r.utcUnixMs).Append(',').Append(r.renderedFrame).Append(',').Append(r.focused ? 1 : 0).Append(',')
                        .Append(r.freshGpu ? 1 : 0).Append(',').Append(r.gpuTimestamp);
                    foreach (double value in new[] { r.interval,r.main,r.render,r.gpu,r.alloc,r.presentationCpu,r.presentationAlloc,r.particles,r.spawned,r.dropped,r.batches,r.drawCalls,r.setPass,r.triangles,r.vertices })
                        csv.Append(',').Append(value.ToString("G17", CultureInfo.InvariantCulture));
                    csv.Append('\n');
                }
                File.WriteAllText(Path.Combine(evidence, name + "-integrated-frames.csv"), csv.ToString());
                Check(report.authorityUnchanged && report.saveUnchanged, "Frozen integrated window preserves authority and disk / " + name);
                Check(report.resourcesStable && report.crossWindowResourcesStable && report.ownedParticles == 96 && report.ownedParticleMeshes == 5 && report.ownedParticleMaterials == 1,
                    "Integrated window retains exact warmed resources / " + name);
                Check(peak <= (mode.StartsWith("off", StringComparison.Ordinal) ? 0 : mode == "low" ? 32 : 96) && report.after.renderedFrames - report.before.renderedFrames >= rows.Length,
                    "Bounded actual rendered integrated window / " + name);
                if(integratedHero!=null) Check(mode.StartsWith("off",StringComparison.Ordinal) ? report.heroActivityCount==0 : report.heroActivityCount>0 && peak>0 &&
                    (integratedHero!="dose" || world.LabDoseMixture.Total==integratedDoseMix.Total && integratedDoseMix.Total>0),
                    "Actual owned hero presentation activity (or disabled absence) / "+name+" count="+report.heroActivityCount);
            }
        }
        static bool IntegratedSameInventory(FxInventory a,FxInventory b) => a.worldRenderers==b.worldRenderers && a.worldTransforms==b.worldTransforms &&
            a.loadedMeshes==b.loadedMeshes && a.loadedMaterials==b.loadedMaterials && a.audioSources==b.audioSources && a.animatorComponents==b.animatorComponents &&
            a.tanks==b.tanks && a.occupiedTanks==b.occupiedTanks && a.racks==b.racks && a.occupiedRacks==b.occupiedRacks && a.visitors==b.visitors;
        static FxMetric IntegratedStats(IntegratedFrame[] rows, string name, string unit, Func<IntegratedFrame,double> select)
        {
            var values = new double[rows.Length]; int count = 0;
            foreach (var row in rows) { double value = select(row); if (value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value)) values[count++] = value; }
            Array.Sort(values, 0, count);
            return new FxMetric { name = name, unit = unit, available = count > 0, samples = count,
                median = count > 0 ? values[(count - 1)/2] : 0, p95 = count > 0 ? values[Math.Min(count-1,(int)Math.Ceiling(count*.95)-1)] : 0,
                maximum = count > 0 ? values[count-1] : 0 };
        }
        void IntegratedRaster(string name)
        {
            var camera = shop.Player.View; int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width,height,0,RenderTextureFormat.ARGBFloat); target.Create();
            var buffer = new CommandBuffer { name = "R07 pooled-particle conservative raster sample" };
            var prior = RenderTexture.active; var read = new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
            try
            {
                buffer.SetRenderTarget(target); buffer.ClearRenderTarget(false,true,Color.clear);
                buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                foreach (var renderer in shop.World.FxPool.Root.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMaterial && renderer.sharedMaterial.shader.name == "EvolutionShop/Cartoon Particle")
                        buffer.DrawRenderer(renderer,renderer.sharedMaterial,0,1);
                Graphics.ExecuteCommandBuffer(buffer); RenderTexture.active = target;
                read.ReadPixels(new Rect(0,0,width,height),0,0); read.Apply();
                double fragments = 0; int covered = 0, peak = 0;
                foreach (var pixel in read.GetPixels()) { int layers = Mathf.RoundToInt(pixel.r); fragments += layers; if (layers > 0) covered++; peak = Mathf.Max(peak,layers); }
                var result = new VocabularyRaster { view = name,width = width,height = height,activeParticles = shop.World.FxPool.ActiveCount,
                    coveredPixels = covered,peakOverlap = peak,coveragePercent = 100.0*covered/(width*height),fragmentSum = fragments,
                    averageFullScreenLayers = fragments/(width*height),meanCoveredLayers = covered > 0 ? fragments/covered : 0 };
                File.WriteAllText(Path.Combine(evidence,name+"-raster.json"),JsonUtility.ToJson(result,true));
                // Descriptive evidence only: neither a whole-scene overdraw number nor an acceptance ceiling.
            }
            finally { RenderTexture.active = prior; buffer.Release(); target.Release(); Destroy(target); Destroy(read); }
        }
    }
}
#endif
