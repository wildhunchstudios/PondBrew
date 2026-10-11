#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class FxMetric
        {
            public string name, unit;
            public bool available;
            public int samples;
            public double median, p95, maximum;
        }
        [Serializable] sealed class FxInventory
        {
            public int worldTransforms, worldRenderers, loadedMeshes, loadedMaterials, audioSources, animatorComponents;
            public int tanks, occupiedTanks, racks, occupiedRacks, conditioners, visitors, renderedFrames;
            public string incubation, technician, stocker;
        }
        [Serializable] sealed class FxWindow
        {
            public string fixture, view, phase, unity, gpu, graphicsApi, cpu, os, quality, pipeline;
            public int width, height, memoryMB, targetFrameRate, vsync;
            public bool frameTimingEnabled, focused;
            public double seconds;
            public FxInventory before, after;
            public FxMetric[] metrics;
        }
        struct FxFrame { public double interval, main, render, gpu, alloc; }

        IEnumerator FxBaselineScenario()
        {
            bool setupOnly = ShopSession.Arg("-evolutionFxBaselineSetupOnly") != null;
            if (setupOnly) limitations.Add("FIXTURE SETUP PROBE ONLY: no measured windows or visual baseline acceptance.");
            Check(shop.ValidationMode && !Application.isEditor &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "FX baseline requires an isolated standalone development slot");
            limitations.Add("FX-00 diagnostic-only adapter: seeded credits/research/stock and direct validated layout commits are labelled workloads, not progression/economy evidence. Measurements include development-player, IMGUI, recorder and coroutine overhead; four pre-existing user players remain open. Eight-second static camera windows are not a minimum-PC, campaign, native-input or audio-listening pass. This diagnostic adapter changes no gameplay authority, production rules or admitted assets; candidate presentation changes are identified by its source/build checkpoint.");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionFxBaselineKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionFxBaselineMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            Check(shop.SkipTutorial(), "Baseline skips tutorial only in isolated slot");
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            shop.AllowValidationInput = false;
            UnityEngine.Random.InitState(41011);
            SpecimenProduce("pondlet"); Ok(shop.Model.TransferToTank("tank-a"), "Ordinary fixture has one Pondlet");
            Check(shop.SaveNow(), "Persist ordinary comparison fixture");
            File.Copy(shop.SavePath, Path.Combine(evidence, "ordinary-fixture.json"));
            if (!setupOnly) yield return FxViews("ordinary", "night");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Ordinary fixture opens through counter");
            if (!setupOnly) yield return FxViews("ordinary", "day");
            yield return EnterCounter(); Check(shop.CloseForNight(), "Close ordinary fixture through counter");
            yield return WaitForNight(6); shop.SetPanel(ShopPanel.None);
            StaffCreditFixture(2000, "FX busy-layout setup only");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            fixture.game.upgradeOwned = true; fixture.game.sparkResearchUnlocked = true;
            fixture.game.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin" };
            Check(SaveStore.Write(shop.SavePath, fixture, out var error) && shop.ReloadSaved(), "Labelled research fixture loads / " + error);
            Check(shop.Layout.TryExpand(shop.Model, ShopDayPhase.Night).Success && shop.Layout.TryExpand(shop.Model, ShopDayPhase.Night).Success &&
                shop.Layout.TryExpandLab(shop.Model, ShopDayPhase.Night).Success && shop.Layout.TryExpandLab(shop.Model, ShopDayPhase.Night).Success,
                "Busy workload expands both rooms to tier two");
            Check(shop.SaveNow() && shop.ReloadSaved(), "Rebuild expanded geometry before native placement");
            yield return FxPlace(ShopLayout.LargeDisplayId, -18, -3, new Vector3(-9, .2f, -4.5f));
            yield return FxPlace(ShopLayout.RackDefinitionId, 9, 7, new Vector3(4.5f, .2f, 1.3f));
            yield return FxPlace(ShopLayout.ConditionerDefinitionId, -8, 14, new Vector3(-4, .2f, 5.2f));
            Check(shop.SaveNow() && shop.ReloadSaved(), "Busy layout rebuilds physical fixtures through isolated reload");
            Check(shop.Customers.Ready, "Busy layout retains baked customer routes");
            LabRoutesCheck("FX busy fixture");
            fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            fixture.game.mistResearchUnlocked = true;
            fixture.game.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };
            for (int i = 0; i < fixture.game.tanks.Length; i++)
            { fixture.game.tanks[i].specimenId = "fx-tank-" + i; fixture.game.tanks[i].speciesId = fixture.game.discoveries[i % 5]; }
            for (int i = 0; i < fixture.game.storage.Length; i++)
            { fixture.game.storage[i].specimenId = "fx-rack-" + i; fixture.game.storage[i].speciesId = "pondlet"; }
            fixture.game.incubator = new IncubatorSave { phase = IncubatorPhase.Incubating, speciesId = "pondlet", specimenId = "fx-incubator", secondsRemaining = 8 };
            foreach (var vat in fixture.game.conditioners)
            { vat.phase = ConditionerPhase.Processing; vat.secondsRemaining = 8; vat.firstSpecimenId = "fx-conditioning-0"; vat.firstSpeciesId = "pondlet"; vat.secondSpecimenId = "fx-conditioning-1"; vat.secondSpeciesId = "pondlet"; }
            foreach (var grower in fixture.game.growers) { grower.units = 0; grower.secondsUntilNextUnit = 1; }
            fixture.staff.hired = true; fixture.stocker.employee.hired = true; fixture.technician.employee.hired = true;
            // Labelled wage receipts are workload setup only, not earned progression.
            foreach (var employee in new[] { fixture.staff, fixture.stocker.employee, fixture.technician.employee })
            { employee.scheduled = true; employee.paidDay = shop.Cycle.DayNumber; employee.paidWage = 4; }
            fixture.game.currency -= 12;
            fixture.technician.queue = new[] { new LabOrderSave { speciesId = "pondlet", count = 2 } };
            Check(SaveStore.Write(shop.SavePath, fixture, out error) && shop.ReloadSaved(), "Busy stock/research/employee fixture loads / " + error);
            File.Copy(shop.SavePath, Path.Combine(evidence, "busy-fixture.json"));
            Check(shop.Model.TankIds.Length > 2 && shop.Layout.ConditionerCount == 1 && shop.Layout.RackCount == 1,
                "Busy workload has additional stocked display, rack and conditioner");
            if (setupOnly) yield break;
            yield return FxViews("busy", "night");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Busy fixture opens through counter");
            Spawn(CustomerArchetypes.Casual); Spawn(CustomerArchetypes.Collector);
            yield return FxViews("busy", "day");
        }
        IEnumerator FxPlace(string definition, int x, int z, Vector3 player)
        {
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false;
            shop.Player.Teleport(player, Quaternion.identity);
            shop.Player.View.transform.LookAt(new Vector3(x * ShopLayout.GridSize, 0, z * ShopLayout.GridSize));
            yield return null; yield return null;
            var core = shop.Layout.TryPreview(definition, null, x, z, 0, out _);
            Check(core.Success, "Requested busy footprint / " + definition + " / " + core.Failure);
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(null, definition), "Begin diagnostic-camera placement / " + definition);
            yield return null; yield return null;
            File.AppendAllText(Path.Combine(evidence, "placement-fixture.txt"), definition + " desired=" + x + "," + z +
                " actual=" + shop.PlacementX + "," + shop.PlacementZ + " valid=" + shop.PlacementValid + " message=" + shop.PlacementMessage + "\n");
            Check(shop.PlacementX == x && shop.PlacementZ == z && shop.PlacementValid,
                "Exact diagnostic-camera placement / " + definition + " actual=" + shop.PlacementX + "," + shop.PlacementZ + " / " + shop.PlacementMessage);
            Check(shop.CommitPlacement() && shop.SaveError == null, "Production placement/nav/save commits / " + definition);
            shop.SetPanel(ShopPanel.None);
        }
        IEnumerator FxViews(string fixture, string phase)
        {
            int width = int.TryParse(ShopSession.Arg("-screen-width"), out int w) ? w : Screen.width;
            int height = int.TryParse(ShopSession.Arg("-screen-height"), out int h) ? h : Screen.height;
            // Restoring a different resolution forces a real swapchain even after hidden startup.
            Screen.SetResolution(width == 1280 ? 1920 : 1280, height == 720 ? 1080 : 720, FullScreenMode.Windowed);
            yield return new WaitForSeconds(.3f);
            Screen.SetResolution(width, height, FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
            Check(Screen.width == width && Screen.height == height, "Actual baseline render surface / " + width + "x" + height);
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false;
            for (int i = 0; i < 2; i++)
            {
                string view = i == 0 ? "tanks" : "lab";
                GateAim(i == 0 ? new Vector3(-1.4f, .05f, -4.65f) : new Vector3(1f, .05f, 1.28f),
                    i == 0 ? new Vector3(4.9f, 1.55f, -.7f) : new Vector3(-1.5f, 1.5f, 4.45f));
                yield return GateCapture("fx-" + fixture + "-" + phase + "-" + view);
                yield return FxMeasure(fixture, phase, view);
            }
        }
        FxInventory FxCount()
        {
            var result = new FxInventory { worldTransforms = shop.World.GetComponentsInChildren<Transform>(true).Length,
                worldRenderers = shop.World.GetComponentsInChildren<Renderer>(true).Length,
                loadedMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length, loadedMaterials = Resources.FindObjectsOfTypeAll<Material>().Length,
                audioSources = Resources.FindObjectsOfTypeAll<AudioSource>().Length, animatorComponents = shop.World.GetComponentsInChildren<Animator>(true).Length,
                tanks = shop.Model.TankIds.Length, racks = shop.Model.StorageIds.Length, conditioners = shop.Layout.ConditionerCount,
                visitors = shop.Customers.ActiveCount, renderedFrames = Time.renderedFrameCount,
                incubation = shop.Model.Incubator.Phase.ToString(), technician = shop.Technician.Phase, stocker = shop.Stocker.Phase };
            foreach (var id in shop.Model.TankIds) if (shop.Model.GetTank(id).HasValue) result.occupiedTanks++;
            foreach (var id in shop.Model.StorageIds) if (shop.Model.GetStorage(id).HasValue) result.occupiedRacks++;
            return result;
        }
        IEnumerator FxMeasure(string fixture, string phase, string view)
        {
            yield return new WaitForSeconds(3);
            var rows = new FxFrame[8192]; int count = 0;
            var timing = new FrameTiming[1];
            using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            using (var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1))
            using (var alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                yield return null; yield return null;
                var before = FxCount(); yield return null; yield return null;
                double started = Time.realtimeSinceStartupAsDouble, previous = started;
                while (Time.realtimeSinceStartupAsDouble - started < 8 && count < rows.Length)
                {
                    FrameTimingManager.CaptureFrameTimings(); yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    uint timings = FrameTimingManager.GetLatestTimings(1, timing);
                    rows[count++] = new FxFrame { interval = (now - previous) * 1000,
                        main = main.Valid && main.Count > 0 ? main.LastValue / 1000000.0 : -1,
                        render = render.Valid && render.Count > 0 ? render.LastValue / 1000000.0 : -1,
                        gpu = timings > 0 && timing[0].gpuFrameTime > 0 ? timing[0].gpuFrameTime : -1,
                        alloc = alloc.Valid && alloc.Count > 0 ? alloc.LastValue : -1 };
                    previous = now;
                }
                var report = new FxWindow { fixture = fixture, phase = phase, view = view, width = Screen.width, height = Screen.height,
                    unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    cpu = SystemInfo.processorType, memoryMB = SystemInfo.systemMemorySize, os = SystemInfo.operatingSystem,
                    quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,
                    targetFrameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), focused = Application.isFocused,
                    seconds = previous - started, before = before, after = FxCount(), metrics = new[] {
                        FxStats(rows,count,"frameInterval","ms", r=>r.interval), FxStats(rows,count,"mainThreadIncludingWaits","ms",r=>r.main),
                        FxStats(rows,count,"renderThread","ms",r=>r.render), FxStats(rows,count,"gpuFrame","ms",r=>r.gpu), FxStats(rows,count,"gcAllocatedPerFrame","bytes",r=>r.alloc) } };
                string name = fixture + "-" + phase + "-" + view;
                File.WriteAllText(Path.Combine(evidence, name + "-metrics.json"), JsonUtility.ToJson(report, true));
                var csv = new StringBuilder("frame,intervalMs,mainThreadMs,renderThreadMs,gpuMs,gcBytes\n");
                for (int i = 0; i < count; i++) csv.Append(i).Append(',').Append(rows[i].interval.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                    .Append(rows[i].main.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(rows[i].render.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                    .Append(rows[i].gpu.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(rows[i].alloc.ToString(CultureInfo.InvariantCulture)).Append('\n');
                File.WriteAllText(Path.Combine(evidence, name + "-frames.csv"), csv.ToString());
                Check(count >= 60 && report.after.renderedFrames > before.renderedFrames && Screen.width == report.width && !shop.Paused,
                    "Measured live rendered window / " + name + " frames=" + count);
            }
        }
        static FxMetric FxStats(FxFrame[] rows, int count, string name, string unit, Func<FxFrame, double> select)
        {
            var values = new double[count]; int available = 0;
            for (int i = 0; i < count; i++) { double value = select(rows[i]); if (value >= 0) values[available++] = value; }
            Array.Sort(values, 0, available);
            return new FxMetric { name = name, unit = unit, available = available > 0, samples = available,
                median = available > 0 ? values[(available - 1) / 2] : 0,
                p95 = available > 0 ? values[Math.Min(available - 1, (int)Math.Ceiling(available * .95) - 1)] : 0,
                maximum = available > 0 ? values[available - 1] : 0 };
        }
    }
}
#endif
