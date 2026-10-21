#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// AD-A route (-evolutionDioramaSliceSmoke): the Pocket Aquarium Diorama slice in a real player. It checks the rendering
    /// foundation, the four lighting keys, the hatch hero moment (live and stepped), the kit Pondlet in its tank, the kit Casual
    /// visitor, switching the look off and on, the comfort modes, and records frame timings with the look on and off.
    /// </summary>
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class DioramaTimingSample { public string view; public bool look; public int frames, gpuFrames; public double cpuP50, cpuP95, gpuP50, gpuP95, intervalP50, intervalP95; }
        [Serializable] sealed class DioramaTimingReceipt
        {
            public string device, gpu, os, quality; public int width, height, targetFrameRate, vsync; public bool frameTimingEnabled;
            public List<DioramaTimingSample> samples = new List<DioramaTimingSample>();
        }
        [Serializable] sealed class DioramaHatchFrame { public string clip; public float age, scaleY, scaleZ, light; public int particles; public bool frozen; }
        [Serializable] sealed class DioramaHatchTrace { public List<DioramaHatchFrame> frames = new List<DioramaHatchFrame>(); }

        readonly DioramaTimingReceipt dioramaTiming = new DioramaTimingReceipt();
        readonly DioramaHatchTrace dioramaTrace = new DioramaHatchTrace();

        static bool Near(Color a, Color b, float tolerance = .002f) =>
            Mathf.Abs(a.r - b.r) < tolerance && Mathf.Abs(a.g - b.g) < tolerance && Mathf.Abs(a.b - b.b) < tolerance;

        IEnumerator DioramaShot(string name)
        {
            bool input = shop.AllowValidationInput;
            shop.AllowValidationInput = false; shop.Player.InputEnabled = false;
            yield return null; yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, name + "-screen.png"));
            yield return new WaitForSecondsRealtime(.25f);
            shop.AllowValidationInput = input;
        }

        IEnumerator DioramaClip(string name, float seconds, Action<float> tick = null)
        {
            string encoder = ShopSession.Arg("-evolutionFxReviewEncoder");
            if (Application.isEditor || string.IsNullOrEmpty(encoder))
            {
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < seconds) { tick?.Invoke(Time.realtimeSinceStartup - start); yield return null; }
                yield break;
            }
            using (var r = new FxReviewRecorder(encoder, Path.Combine(evidence, name + ".mp4")))
            {
                while (r.Age < seconds)
                {
                    tick?.Invoke(r.Age);
                    yield return new WaitForEndOfFrame();
                    r.Sample();
                    if (r.Failure != null) throw new InvalidOperationException(r.Failure);
                }
                float deadline = Time.realtimeSinceStartup + 3;
                while (r.Pending > 0) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(name + " drain"); yield return null; }
                var receipt = r.Finish();
                File.WriteAllText(Path.Combine(evidence, name + "-frames.json"), JsonUtility.ToJson(receipt, true));
                Check(receipt.frames.Length >= seconds * 20 && receipt.achievedFPS >= 25, "Actual silent Windows video / " + name);
            }
        }

        Vector3 CornerEye => new Vector3(3.05f, .2f, -1.35f);
        Vector3 CornerTarget => StationOf(StationKind.TankA).transform.TransformPoint(new Vector3(0, 1.45f, 0));
        Vector3 IncubatorEye => StationOf(StationKind.Incubator).transform.TransformPoint(new Vector3(.15f, .2f, -2.05f));
        Vector3 IncubatorTarget => StationOf(StationKind.Incubator).transform.TransformPoint(new Vector3(0, 1.38f, 0));
        void DioramaView(Vector3 eye, Vector3 target) { shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false; GateAim(eye, target); Physics.SyncTransforms(); }

        IEnumerator DioramaSliceScenario()
        {
            limitations.Add("AD-A art slice: isolated slot; scripted camera views and synthetic held collection through the real Input System; lighting keys forced by a presentation-only validation hook (the day itself is not played to each key); the stepped hatch freezes the session's FX clock and simulates the flipbook particles by the same steps so each beat can be captured; live hatch and swim clips are real time. Screens and silent videos are not hands-on review, listening, creative approval or a minimum-spec GPU result. Frame timings are this PC's own, with the look on and off in the same session.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "AD-A isolated save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("AD-A keyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("AD-A mouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows resolution " + width + "x" + height);
            }
            GateFresh();
            var w = shop.World;
            yield return DioramaFoundation(w);
            yield return DioramaKeys(w);
            yield return DioramaLiveHatch(w);
            yield return DioramaSteppedHatch(w, true, false, false, "hatch-first");
            yield return DioramaSteppedHatch(w, false, false, false, "hatch-repeat");
            yield return DioramaSteppedHatch(w, true, true, false, "hatch-reduced");
            yield return DioramaSteppedHatch(w, true, false, true, "hatch-low");
            yield return DioramaHatchInterruptions(w);
            yield return DioramaTankPondlet(w);
            yield return DioramaVisitor(w);
            yield return DioramaToggle(w);
            yield return DioramaTimings(w);
            File.WriteAllText(Path.Combine(evidence, "diorama-timing.json"), JsonUtility.ToJson(dioramaTiming, true));
            File.WriteAllText(Path.Combine(evidence, "diorama-hatch-trace.json"), JsonUtility.ToJson(dioramaTrace, true));
            shop.DioramaTimeForValidation = null;
            shop.SetPanel(ShopPanel.Pause);
        }

        IEnumerator DioramaFoundation(ShopWorld w)
        {
            Check(w.DioramaActive && DioramaLook.Active && shop.DioramaLookOn, "The slice route switches the diorama look on");
            foreach (var shader in new[] { DioramaLook.ToonShader, DioramaLook.GlassShader, DioramaLook.UnlitShader, DioramaLook.WindowShader })
                Check(shader && shader.isSupported, "Diorama shader is in the build and supported / " + (shader ? shader.name : "missing"));
            var cameraData = shop.Player.View.GetUniversalAdditionalCameraData();
            Check(cameraData.renderPostProcessing, "Camera post-processing is on for bloom and the room grades");
            Check(w.DioramaShopVolume && w.DioramaShopVolume.enabled && w.DioramaShopVolume.isGlobal && w.DioramaLabVolume && w.DioramaLabVolume.enabled,
                "Shop and lab grade volumes are active");
            var fill = w.DioramaPortholeFillForValidation;
            Check(fill && fill.isActiveAndEnabled && fill.intensity > 0, "A soft fill lights the incubator creature from the porthole");
            Check(w.DioramaShopVolume.sharedProfile.TryGet<Bloom>(out var bloom) && bloom.active && bloom.intensity.overrideState &&
                  w.DioramaShopVolume.sharedProfile.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.Neutral,
                "Grade profile has URP bloom and the project's Neutral tonemapping");
            Check(w.DioramaSwapCount >= 30, "The display corner and incubator have diorama materials / " + w.DioramaSwapCount + " renderers");
            Check(w.DioramaMaterialsConsistent(out var detail), "Swapped renderers show the diorama materials / " + detail);
            int props = 0, alive = 0;
            foreach (var renderer in StationOf(StationKind.TankA).GetComponentsInChildren<Renderer>(true).Concat(StationOf(StationKind.Incubator).GetComponentsInChildren<Renderer>(true)))
                foreach (var m in renderer.sharedMaterials)
                {
                    if (!m || !m.shader || m.shader.name != DioramaLook.ToonShaderName) continue;
                    bool creature = renderer.GetComponentInParent<CreatureVisual>();
                    bool outlined = m.GetShaderPassEnabled(DioramaLook.OutlinePass) && m.GetFloat("_OutlineWidth") > 0;
                    if (creature) { alive++; continue; }
                    props++;
                    Check(!outlined, "Props and rooms never carry the outline / " + renderer.name);
                }
            Check(props >= 20, "Toon props in the slice / " + props);
            Check(w.DioramaShadowsLiftedForValidation > 0, "The room shell no longer blocks the key light / " + w.DioramaShadowsLiftedForValidation + " renderers");
            var sheet = Resources.Load<Texture2D>(DioramaLook.KitPath + "DioramaFlipbook");
            Check(sheet && sheet.width == 512 && sheet.height == 768, "Hand-drawn flipbook sheet loads at 512x768 (4 frames x 6 shapes)");
            var pondlet = Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaPondlet");
            Check(pondlet, "Kit Pondlet loads");
            var shapes = pondlet.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(i => r.sharedMesh.GetBlendShapeName(i))).ToList();
            foreach (var name in new[] { "Blink", "Happy", "Surprised", "Puffed" })
                Check(shapes.Contains(name), "Kit Pondlet face shape key / " + name);
            foreach (var part in new[] { "Tail pivot", "Left fin pivot", "Right fin pivot", "Crest pivot" })
                Check(pondlet.transform.Find(part), "Kit Pondlet pivot the swim code drives / " + part);
            var visitor = Resources.Load<GameObject>(DioramaLook.KitPath + "DioramaCasualVisitor");
            Check(visitor, "Kit Casual visitor loads");
            foreach (var part in new[] { "Left hip", "Right hip", "Left shoulder", "Right shoulder", "Head pivot" })
                Check(visitor.transform.Find(part), "Kit visitor joint the walk code drives / " + part);
            var mouth = visitor.transform.Find("Mouth");
            var moods = mouth ? mouth.GetComponent<SkinnedMeshRenderer>() : null;
            foreach (var name in new[] { "Happy", "Haggle", "Shocked", "Ouch" })
                Check(moods && moods.sharedMesh.GetBlendShapeIndex(name) >= 0, "Kit visitor mood shape key / " + name);
            yield return null;
        }

        IEnumerator DioramaKeys(ShopWorld w)
        {
            var keys = new[] { (DioramaKey.Night, ShopDayPhase.Night, 0f), (DioramaKey.Morning, ShopDayPhase.Open, 0f),
                (DioramaKey.Midday, ShopDayPhase.Open, .55f), (DioramaKey.Closing, ShopDayPhase.Closing, 0f) };
            foreach (var (key, phase, progress) in keys)
            {
                shop.DioramaTimeForValidation = (phase, progress);
                yield return null;
                w.SnapDioramaLightForValidation();
                var expected = DioramaLighting.Key(key);
                var shown = w.DioramaShownKey;
                Check(Near(w.KeyLightForValidation.color, expected.Sun) && Mathf.Abs(w.KeyLightForValidation.intensity - expected.SunIntensity) < .002f,
                    "Key light follows the lighting key / " + key);
                Check(Quaternion.Angle(w.KeyLightForValidation.transform.rotation, Quaternion.Euler(expected.SunPitch, expected.SunYaw, 0)) < .1f,
                    "Sun angle tells the time / " + key);
                var window = w.DioramaWindowMaterialForValidation;
                Check(Near(window.GetColor("_SkyTop"), expected.SkyTop) && Near(window.GetColor("_SkyBottom"), expected.SkyBottom),
                    "Street window sky follows the key / " + key);
                Check((w.DioramaTankGlowForValidation.intensity > 0) == (expected.TankGlow > 0), "Tank glow only after dark / " + key);
                Check(Near(RenderSettings.ambientSkyColor, expected.AmbientSky), "Ambient follows the key / " + key);
                string label = key.ToString().ToLowerInvariant();
                DioramaView(new Vector3(-.6f, .2f, -.4f), new Vector3(4.2f, 1.75f, -4.2f));
                yield return null; yield return null;
                Check(w.DioramaLabWeight < .05f, "Shop grade in the shop / " + key);
                yield return DioramaShot("key-" + label + "-shop");
                DioramaView(CornerEye, CornerTarget);
                yield return DioramaShot("key-" + label + "-corner");
                yield return LabelReadable(w, "tank-a", key.ToString());
                DioramaView(IncubatorEye, IncubatorTarget);
                yield return null; yield return null;
                Check(w.DioramaLabWeight > .95f, "Lab grade in the lab / " + key);
                yield return DioramaShot("key-" + label + "-incubator");
            }
            shop.DioramaTimeForValidation = null;
            yield return null;
            w.SnapDioramaLightForValidation();
        }

        /// <summary>Reads the display's title label off the screen: the lit sign must not bloom over its dark lettering.</summary>
        IEnumerator LabelReadable(ShopWorld w, string tank, string context)
        {
            var station = w.DisplayStationForValidation(tank);
            var text = station ? station.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t => t.text.StartsWith("DISPLAY")) : null;
            var renderer = text ? text.GetComponent<Renderer>() : null;
            Check(renderer, "Display title label found / " + context);
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var camera = shop.Player.View;
                var b = renderer.bounds;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = camera.WorldToScreenPoint(corner);
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                }
                // Screen pixels to capture pixels (they match unless the capture is scaled).
                float sx = shot.width / (float)Screen.width, sy = shot.height / (float)Screen.height;
                int ax = Mathf.Clamp(Mathf.FloorToInt(x0 * sx), 0, shot.width - 1), bx = Mathf.Clamp(Mathf.CeilToInt(x1 * sx), 0, shot.width - 1);
                int ay = Mathf.Clamp(Mathf.FloorToInt(y0 * sy), 0, shot.height - 1), by = Mathf.Clamp(Mathf.CeilToInt(y1 * sy), 0, shot.height - 1);
                float low = 1, high = 0;
                for (int y = ay; y <= by; y++)
                    for (int x = ax; x <= bx; x++)
                    {
                        var c = shot.GetPixel(x, y);
                        float luma = c.r * .2126f + c.g * .7152f + c.b * .0722f;
                        low = Mathf.Min(low, luma); high = Mathf.Max(high, luma);
                    }
                Check(bx - ax >= 20 && by - ay >= 6, "Display title label is on screen / " + context + " / " + (bx - ax) + "x" + (by - ay) + " px");
                Check(low < .4f && high - low > .4f, "Display title stays readable under the lamps (no bloom wash) / " + context +
                    " / darkest " + low.ToString("F2") + ", brightest " + high.ToString("F2"));
            }
            finally { Destroy(shot); }
        }

        IEnumerator DioramaLiveHatch(ShopWorld w)
        {
            shop.SetFxFocusForValidation(true);
            yield return FxLabPrepare(false);
            DioramaView(IncubatorEye, IncubatorTarget);
            yield return AutomationWait(() => shop.Model.Incubator.SecondsRemaining < 5.5f, 12, "half-grown diorama Pondlet");
            yield return DioramaShot("incubator-growing");
            var growing = w.HatchCreatureForValidation;
            Check(growing && growing.GetComponent<CreatureVisual>().IsDiorama, "The kit Pondlet grows in the incubator");
            yield return AutomationWait(() => shop.Model.Incubator.SecondsRemaining < .5f, 12, "near the first hatch");
            int bursts = w.DioramaHatchBursts;
            string specimen = null;
            yield return DioramaClip("hatch-first-live", 3f, age =>
            {
                if (specimen == null && shop.Model.Incubator.Phase == IncubatorPhase.Ready) specimen = shop.Model.Incubator.SpecimenId;
            });
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && specimen != null && shop.Model.Incubator.SpecimenId == specimen,
                "The saved Ready result never changes while the hatch plays");
            Check(w.DioramaHatchBursts == bursts + 1, "The live first discovery released exactly one flipbook burst");
            Check(w.HatchCreatureForValidation.GetComponent<CreatureVisual>().IsDiorama, "The hatched fish is the kit Pondlet");
            yield return DioramaShot("hatch-first-after");
        }

        IEnumerator DioramaSteppedHatch(ShopWorld w, bool first, bool reduced, bool low, string clip)
        {
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready, "Ready Pondlet for the stepped hatch / " + clip);
            if (reduced || low) Check(shop.SetComfortSettings(reduced, low ? FxEffectDetail.Low : FxEffectDetail.Standard), "Comfort mode for " + clip);
            DioramaView(IncubatorEye, IncubatorTarget);
            yield return null;
            shop.FreezeFxVocabularyForValidation = true;
            float[] ages = first ? new[] { .15f, .27f, .33f, .37f, .5f, .9f, 1.25f } : new[] { .12f, .3f, .6f };
            var shots = first ? new[] { .15f, .33f, .5f, .9f, 1.25f } : new[] { .12f, .3f, .6f };
            try
            {
                w.ResetFxPresentation();
                w.PauseDioramaParticlesForValidation(true);
                int bursts = w.DioramaHatchBursts;
                Check(w.TryFxCue(first ? FxCueId.HatchFirst : FxCueId.HatchRepeat, 900 + (uint)dioramaTrace.frames.Count), "Stepped hatch cue / " + clip);
                var creature = w.HatchCreatureForValidation;
                float age = 0, time = 10;
                Vector3 scale31 = Vector3.zero, scale37 = Vector3.zero;
                int expectedBubbles, expectedPuffs, expectedSparkles;
                DioramaHatch.BurstCounts(first, low, reduced, out expectedBubbles, out expectedPuffs, out expectedSparkles);
                foreach (float target in ages)
                {
                    while (age < target - .0001f)
                    {
                        float step = Mathf.Min(.02f, target - age);
                        w.Animate(time += step, step);
                        w.SimulateDioramaParticlesForValidation(step);
                        age += step;
                    }
                    yield return null;
                    var s = creature.localScale;
                    dioramaTrace.frames.Add(new DioramaHatchFrame { clip = clip, age = target, scaleY = s.y, scaleZ = s.z, light = w.DioramaHatchLightForValidation,
                        particles = w.DioramaLiveParticlesForValidation, frozen = first && !reduced && target > .3f && target < .38f });
                    if (Mathf.Approximately(target, .33f) || Mathf.Approximately(target, .27f)) scale31 = s;
                    if (Mathf.Approximately(target, .37f)) scale37 = s;
                    if (target < DioramaHatch.PopTime(first, reduced))
                        Check(w.DioramaLiveParticlesForValidation == (first && !reduced && target >= .3f ? 1 : 0), "No burst before the pop (only the ring flash in the freeze) / " + clip + " " + target);
                    if (Array.IndexOf(shots, target) >= 0) yield return DioramaShot(clip + "-" + Mathf.RoundToInt(target * 100).ToString("000"));
                    Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready, "Hatch presentation never changes the saved Ready state / " + clip + " " + target);
                }
                Check(w.DioramaHatchBursts == bursts + 1, "Exactly one burst per hatch / " + clip);
                if (first)
                {
                    bool frozen = (scale37 - scale31).sqrMagnitude < 1e-8f;
                    Check(reduced ? !frozen || scale31 == Vector3.zero : frozen, reduced ? "Reduced motion has no freeze beat / " + clip : "The 0.08 s freeze beat holds the pose still / " + clip);
                }
                float peakLight = dioramaTrace.frames.Where(f => f.clip == clip).Max(f => f.light);
                Check(reduced ? peakLight <= 1.25f : peakLight > 2f, (reduced ? "Reduced motion keeps the glow soft / " : "The incubator glow builds and peaks / ") + clip + " " + peakLight);
                int afterPop = dioramaTrace.frames.First(f => f.clip == clip && f.age >= DioramaHatch.PopTime(first, reduced)).particles;
                int expected = expectedBubbles + expectedPuffs + expectedSparkles + (first && !reduced ? 1 : 0);
                Check(afterPop == expected, "Burst size follows the comfort mode / " + clip + " " + afterPop + " of " + expected);
            }
            finally
            {
                shop.FreezeFxVocabularyForValidation = false;
                w.ResetFxPresentation();
                w.PauseDioramaParticlesForValidation(false);
                if (reduced || low) shop.SetComfortSettings(false, FxEffectDetail.Standard);
            }
            Check(w.DioramaLiveParticlesForValidation == 0 && w.DioramaHatchLightForValidation == 0 && w.DioramaDomeScaleForValidation == w.DioramaDomeRestForValidation,
                "Reset clears particles, glow and squash / " + clip);
        }

        IEnumerator DioramaHatchInterruptions(ShopWorld w)
        {
            DioramaView(IncubatorEye, IncubatorTarget);
            yield return null;
            foreach (var panel in new[] { ShopPanel.Pause, ShopPanel.Incubator, ShopPanel.Arrange })
            {
                shop.SetPanel(ShopPanel.None); w.ResetFxPresentation();
                Check(w.TryFxCue(FxCueId.HatchFirst, 950 + (uint)panel), "Interruption fixture hatch / " + panel);
                float t = 0; while (t < .55f) { w.Animate(20 + t, .05f); t += .05f; yield return null; }
                Check(w.DioramaLiveParticlesForValidation > 0, "Burst in flight before the interruption / " + panel);
                shop.SetPanel(panel); yield return null;
                Check(w.DioramaLiveParticlesForValidation == 0 && w.DioramaHatchLightForValidation == 0 && w.DioramaDomeScaleForValidation == w.DioramaDomeRestForValidation,
                    "Panel clears the flipbook burst, glow and squash / " + panel);
            }
            shop.SetPanel(ShopPanel.None); w.ResetFxPresentation();
            Check(w.TryFxCue(FxCueId.HatchFirst, 960), "Focus fixture hatch");
            float a = 0; while (a < .55f) { w.Animate(30 + a, .05f); a += .05f; yield return null; }
            shop.SetFxFocusForValidation(false); yield return null;
            Check(w.DioramaLiveParticlesForValidation == 0, "Focus loss clears the burst");
            shop.SetFxFocusForValidation(true); w.ResetFxPresentation();
            // Collection never waits for the moment: transfer during the wind-up retires it at once.
            shop.FreezeFxVocabularyForValidation = true;
            try
            {
                Check(w.TryFxCue(FxCueId.HatchFirst, 970), "Transfer fixture hatch");
                w.Animate(40, .15f); w.Animate(40.15f, .01f);
                string specimen = shop.Model.Incubator.SpecimenId;
                Ok(shop.TransferToDisplay("tank-a"), "Immediate transfer during the diorama wind-up");
                w.Animate(40.2f, .01f); yield return null;
                Check(shop.Model.GetTank("tank-a").HasValue && shop.Model.GetTank("tank-a").Value.Id == specimen && w.LabHatchRemaining == 0 &&
                      w.DioramaLiveParticlesForValidation == 0 && w.DioramaHatchLightForValidation == 0,
                    "Exact specimen moves at once; the hatch retires with no burst and no glow");
            }
            finally { shop.FreezeFxVocabularyForValidation = false; w.ResetFxPresentation(); }
        }

        IEnumerator DioramaTankPondlet(ShopWorld w)
        {
            var creature = w.DisplayCreatureForValidation("tank-a");
            Check(creature && creature.IsDiorama, "The kit Pondlet lives in the display tank");
            var tank = w.DisplayStationForValidation("tank-a");
            float shown = creature.transform.localScale.x;
            Check(Mathf.Abs(shown - .69f * CreatureVisual.DioramaTankScale) < .002f, "Display tank shows it larger (AD.4) / scale " + shown);
            DioramaView(CornerEye, CornerTarget);
            int glance = w.PondletGlanceCount, trick = w.DioramaTrickBubbles;
            float start = Time.realtimeSinceStartup; int samples = 0;
            float happyPeak = 0;
            while (Time.realtimeSinceStartup - start < 6)
            {
                yield return null;
                Check(w.ValidateTankBounds(out var bounds), "Every kit Pondlet mesh stays inside the glass / " + bounds);
                happyPeak = Mathf.Max(happyPeak, creature.DioramaWeightForValidation("Happy"));
                samples++;
                if (w.DioramaTrickBubbles > trick && samples % 4 == 0 && happyPeak > 0) break;
            }
            Check(samples > 60, "Swim sampled for bounds / " + samples + " frames");
            Check(w.PondletGlanceCount > glance && w.DioramaTrickBubbles > trick, "Lingering at the tank: the Pondlet blows its bubble at you");
            Check(happyPeak > 50, "Its face lights up for the trick / " + happyPeak);
            yield return DioramaShot("pondlet-trick");
            yield return DioramaClip("pondlet-swim", 4f, age => Check(w.ValidateTankBounds(out string swim), "Kit Pondlet stays inside the glass while filmed / " + swim));
            DioramaView(new Vector3(3.6f, .2f, -.2f), CornerTarget);
            yield return DioramaShot("pondlet-tank");
        }

        IEnumerator DioramaVisitor(ShopWorld w)
        {
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the shop for the kit visitor");
            shop.SetPanel(ShopPanel.None);
            Check(shop.Customers.SpawnNow(), "A Casual visitor arrives");
            var actor = shop.Customers.Actor;
            Check(actor && actor.DioramaVisitor && actor.DioramaVisitor.IsDiorama, "The visitor is the kit Casual");
            var appearance = actor.transform.Find("Visitor appearance");
            // The customer moves everything but the legs into its upper body, so find the joints at any depth.
            var parts = appearance ? appearance.GetComponentsInChildren<Transform>(true) : new Transform[0];
            foreach (var joint in new[] { "Left hip", "Right hip", "Left shoulder", "Right shoulder", "Head pivot" })
                Check(parts.Any(t => t.name == joint), "Classic joint names keep walking and reactions / " + joint);
            var headPivot = parts.FirstOrDefault(t => t.name == "Head pivot");
            foreach (var part in ShopCharacterVisual.DioramaHeadParts)
                Check(headPivot && headPivot.Find(part), "The face rides on the head pivot / " + part);
            float start = Time.realtimeSinceStartup;
            while (actor && actor.Phase != CustomerPhase.Browsing && Time.realtimeSinceStartup - start < 25) yield return null;
            Check(actor && actor.Phase == CustomerPhase.Browsing, "The visitor browses the displays");
            yield return new WaitForSeconds(.6f);
            Check(actor.DioramaMood == DioramaVisitorMood.Haggle && actor.DioramaVisitor.DioramaMouthWeight(DioramaVisitorMood.Haggle) > .5f,
                "Its face studies the price while browsing");
            Vector3 head = actor.transform.position + Vector3.up * 1.3f;
            // From behind and beside the visitor (it faces the display it studies), so the shot shows both.
            Vector3 eye = actor.transform.position - actor.transform.forward * 2.3f + actor.transform.right * 1.3f; eye.y = .2f;
            DioramaView(eye, head - Vector3.up * .35f + actor.transform.forward * .6f);
            yield return DioramaShot("casual-browse");
            Vector3 close = actor.transform.position + actor.transform.forward * 1.25f; close.y = .2f;
            DioramaView(close, head);
            yield return DioramaShot("casual-face");
            // Send the visitor off, so the classic and timing views of the corner are not blocked by it.
            shop.Customers.ResetVisitors();
            yield return null;
        }

        IEnumerator DioramaToggle(ShopWorld w)
        {
            var camera = shop.Player.View;
            int materialsOn = -1;
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                shop.SetDioramaLook(false); yield return null;
                Check(!w.DioramaActive && !DioramaLook.Active, "Look off / " + cycle);
                Check(w.DioramaMaterialsConsistent(out var off), "Every swapped renderer is back to its classic material / " + cycle + " " + off);
                Check(!camera.GetUniversalAdditionalCameraData().renderPostProcessing && !w.DioramaShopVolume.enabled, "Post-processing and grades off again / " + cycle);
                Check(!w.DioramaPortholeFillForValidation.isActiveAndEnabled, "The porthole fill leaves with the look / " + cycle);
                Check(w.DioramaShadowsLiftedForValidation == 0, "Room shell shadows restored / " + cycle);
                Check(Near(RenderSettings.ambientSkyColor, new Color(.70f, .77f, .84f)) && Quaternion.Angle(w.KeyLightForValidation.transform.rotation, Quaternion.Euler(55, -32, 0)) < .01f,
                    "Classic ambient and key light restored / " + cycle);
                var classic = w.DisplayCreatureForValidation("tank-a");
                Check(classic && !classic.IsDiorama && Mathf.Abs(classic.transform.localScale.x - .69f) < .002f, "Tank Pondlet is the classic one again at its classic size / " + cycle);
                if (cycle == 1) { DioramaView(CornerEye, CornerTarget); yield return DioramaShot("classic-corner"); DioramaView(IncubatorEye, IncubatorTarget); yield return DioramaShot("classic-incubator"); }
                shop.SetDioramaLook(true); yield return null;
                bool consistent = w.DioramaMaterialsConsistent(out var on);
                Check(w.DioramaActive && consistent, "Look on again / " + cycle + " " + on);
                Check(w.DisplayCreatureForValidation("tank-a").IsDiorama, "Kit Pondlet back / " + cycle);
                yield return null;
                int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                if (cycle == 1) materialsOn = materials;
                else Check(materials <= materialsOn, "Switching the look leaks no materials / cycle " + cycle + " " + materials + " vs " + materialsOn);
            }
        }

        IEnumerator DioramaTimings(ShopWorld w)
        {
            dioramaTiming.device = SystemInfo.deviceModel; dioramaTiming.gpu = SystemInfo.graphicsDeviceName; dioramaTiming.os = SystemInfo.operatingSystem;
            dioramaTiming.quality = QualitySettings.names[QualitySettings.GetQualityLevel()]; dioramaTiming.width = Screen.width; dioramaTiming.height = Screen.height;
            dioramaTiming.targetFrameRate = Application.targetFrameRate; dioramaTiming.vsync = QualitySettings.vSyncCount; dioramaTiming.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            foreach (bool look in new[] { true, false, true, false })
            {
                if (shop.DioramaLookOn != look) shop.SetDioramaLook(look);
                foreach (var (view, eye, target) in new[] { ("corner", CornerEye, CornerTarget), ("incubator", IncubatorEye, IncubatorTarget) })
                {
                    DioramaView(eye, target);
                    for (int i = 0; i < 40; i++) yield return null;
                    var cpu = new List<double>(); var gpu = new List<double>(); var interval = new List<double>();
                    var timing = new FrameTiming[1];
                    for (int i = 0; i < 240; i++)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        yield return null;
                        if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                        {
                            if (timing[0].cpuMainThreadFrameTime > 0) cpu.Add(timing[0].cpuMainThreadFrameTime);
                            if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                        }
                        interval.Add(Time.unscaledDeltaTime * 1000);
                    }
                    double P(List<double> v, double q) { if (v.Count == 0) return 0; v.Sort(); return v[Mathf.Clamp((int)Math.Ceiling(q * v.Count) - 1, 0, v.Count - 1)]; }
                    dioramaTiming.samples.Add(new DioramaTimingSample { view = view, look = look, frames = interval.Count, gpuFrames = gpu.Count,
                        cpuP50 = P(cpu, .5), cpuP95 = P(cpu, .95), gpuP50 = P(gpu, .5), gpuP95 = P(gpu, .95), intervalP50 = P(interval, .5), intervalP95 = P(interval, .95) });
                }
            }
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            Check(dioramaTiming.samples.Count == 8 && dioramaTiming.samples.All(s => s.frames == 240), "Frame timings recorded with the look on and off at both views");
        }
    }
}
#endif
