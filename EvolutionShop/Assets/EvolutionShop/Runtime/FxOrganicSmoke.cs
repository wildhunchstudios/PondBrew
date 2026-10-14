#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class OrganicFrame { public float seconds, hatchRemaining; public int active, hatchActive, spawned; }
        [Serializable] sealed class OrganicMotion { public string name; public int peak, globalPeak; public OrganicFrame[] frames; }
        [Serializable] sealed class OrganicReload
        {
            public string name, specimen, tankABefore, tankAAfter, tankBBefore, tankBAfter;
            public int hatchesBefore, hatchesAfter, soundsBefore, soundsAfter, samples, peakHatchParticles, peakAllParticles, peakTankParticles;
            public float peakHatchRemaining;
            public bool hatchAudioObserved, ownerValid;
        }
        int OrganicHatchParticles() => shop.World.FxPool.ParticleCountForValidation(shop.World.FxCues.Find("incubator"));
        string OrganicTankId(string tank) => shop.Model.GetTank(tank).HasValue ? shop.Model.GetTank(tank).Value.Id : null;
        IEnumerator OrganicClip(string name, float seconds, Action<float> action = null)
        {
            var rows = new List<OrganicFrame>(); int peak = 0, globalPeak = 0;
            Action<float> sample = age =>
            {
                action?.Invoke(age);
                peak = Mathf.Max(peak, OrganicHatchParticles());
                globalPeak = Mathf.Max(globalPeak, shop.World.FxPool.ActiveCount);
                rows.Add(new OrganicFrame { seconds = age, hatchRemaining = shop.World.LabHatchRemaining,
                    active = shop.World.FxPool.ActiveCount, hatchActive = OrganicHatchParticles(), spawned = shop.World.FxPool.SpawnedCount });
            };
            if (Application.isEditor)
            {
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < seconds) { sample(Time.realtimeSinceStartup - start); yield return null; }
            }
            else yield return ReviewClip(name, seconds, sample);
            File.WriteAllText(Path.Combine(evidence, name + "-motion.json"), JsonUtility.ToJson(new OrganicMotion { name = name, peak = peak, globalPeak = globalPeak, frames = rows.ToArray() }, true));
            Check(peak > 0 && peak <= 36 && OrganicHatchParticles() == 0 && shop.World.LabHatchRemaining == 0,
                "Bounded incubator-owned release and quiet settle; other live owners remain independent / " + name + " peak=" + peak);
        }
        IEnumerator OrganicNaturalHatch(string name, string destination)
        {
            yield return FxLabPrepare(false);
            var station = StationOf(StationKind.Incubator).transform;
            GateAim(station.TransformPoint(new Vector3(0, .05f, -2.15f)), station.TransformPoint(new Vector3(0, 1.5f, -.1f)));
            int before = shop.World.LabHatchCount; string specimen = shop.Model.Incubator.SpecimenId;
            yield return AutomationWait(() => shop.Model.Incubator.SecondsRemaining <= .75f, 12, "natural countdown for " + name);
            yield return OrganicClip(name, 3);
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.World.LabHatchCount == before + 1 &&
                shop.Model.Incubator.SpecimenId == specimen && shop.Model.IsDiscovered("pondlet"), "Saved natural hatch has exactly one same-ID specimen / " + name);
            if (ShopSession.Arg("-evolutionFxHatchSmoke") != null) HatchMeshSweep(name);
            int count = shop.World.LabHatchCount, sounds = shop.GetComponent<ShopAudio>().LabPlaybackCount;
            var reload = new OrganicReload { name = name, specimen = specimen, hatchesBefore = count, soundsBefore = sounds,
                tankABefore = OrganicTankId("tank-a"), tankBBefore = OrganicTankId("tank-b") };
            Check(shop.SaveNow() && shop.ReloadSaved(), "Save/reload exact ready batch / " + name); shop.SetPanel(ShopPanel.None);
            float until = Time.realtimeSinceStartup + .4f;
            do
            {
                reload.samples++;
                reload.peakHatchParticles = Mathf.Max(reload.peakHatchParticles, OrganicHatchParticles());
                reload.peakAllParticles = Mathf.Max(reload.peakAllParticles, shop.World.FxPool.ActiveCount);
                reload.peakTankParticles = Mathf.Max(reload.peakTankParticles,
                    shop.World.FxPool.ParticleCountForValidation(shop.World.FxCues.Find("tank-a")) +
                    shop.World.FxPool.ParticleCountForValidation(shop.World.FxCues.Find("tank-b")));
                reload.peakHatchRemaining = Mathf.Max(reload.peakHatchRemaining, shop.World.LabHatchRemaining);
                reload.hatchAudioObserved |= shop.GetComponent<ShopAudio>().HatchAudioActive;
                yield return null;
            } while (Time.realtimeSinceStartup < until);
            reload.hatchesAfter = shop.World.LabHatchCount; reload.soundsAfter = shop.GetComponent<ShopAudio>().LabPlaybackCount;
            reload.tankAAfter = OrganicTankId("tank-a"); reload.tankBAfter = OrganicTankId("tank-b");
            reload.ownerValid = shop.World.FxCues.IsValid(shop.World.FxCues.Find("incubator"));
            File.WriteAllText(Path.Combine(evidence, name + "-reload.json"), JsonUtility.ToJson(reload, true));
            Check(reload.hatchesAfter == count && reload.peakHatchRemaining == 0 && shop.World.LabHatchRemaining == 0 &&
                reload.peakHatchParticles == 0 && OrganicHatchParticles() == 0 && reload.ownerValid &&
                reload.soundsAfter == sounds && !reload.hatchAudioObserved && !shop.GetComponent<ShopAudio>().HatchAudioActive,
                "Ready reload never replays release or sound / " + name);
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.Model.Incubator.SpecimenId == specimen &&
                reload.tankAAfter == reload.tankABefore && reload.tankBAfter == reload.tankBBefore,
                "Reload preserves exact ready output and prior real tank residents / " + name);
            if (destination != null) yield return LabTransferReady(destination, specimen);
        }
        IEnumerator FxOrganicScenario()
        {
            limitations.Add("User-requested R01 visual correction: two actual manual 10s batches and real saved first/repeat hatch; scripted operator views and synthetic held collection. Low/reduced/day/angle views below are explicitly cosmetic replay fixtures with frozen authority. Videos are silent actual standalone HUD at target30FPS, not native controls/listening/creative acceptance or performance profiling.");
            limitations.Add("R07 integrated correction scopes hatch-particle settle/replay to the incubator owner; live occupied-tank ambience remains enabled. Global and per-owner samples are retained separately. Hatch transition count, sound count/activity, pending release and exact specimen ownership remain strict.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")), "Organic correction uses exact isolated slot");
            yield return new WaitForSeconds(1); Check(shop.SkipTutorial(), "Explicit isolated tutorial skip");
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false; shop.AllowValidationInput = false; shop.SetFxFocusForValidation(true);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings; syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionOrganicKeyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionOrganicMouse"); syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            if (!Application.isEditor)
            {
                int w = int.Parse(ShopSession.Arg("-screen-width")), h = int.Parse(ShopSession.Arg("-screen-height"));
                Screen.SetResolution(w == 1280 ? 1920 : 1280, h == 720 ? 1080 : 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.3f);
                Screen.SetResolution(w, h, FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
                Check(Screen.width == w && Screen.height == h, "Actual requested HUD resolution");
            }
            yield return OrganicNaturalHatch("first-hatch-live", "tank-a");
            yield return OrganicNaturalHatch("repeat-hatch-live", "tank-b");
            var world = shop.World; var pool = world.FxPool;
            Check(pool.OwnedCount == 96 && pool.OwnedMeshCount == 5 && pool.OwnedMaterialCount == 1 && pool.Root.GetComponentsInChildren<Collider>(true).Length == 0,
                "Existing96 renderers, five original masks/one material, no effect colliders");
            // Representative material/visibility and comfort fixtures; no pretend production result.
            Check(shop.OpenForDay(), "Ordinary daylight for presentation fixture");
            shop.FreezeFxVocabularyForValidation = true;
            try
            {
                string authority = FxAuthority(); var station = StationOf(StationKind.Incubator).transform;
                for (int mode = 0; mode < 3; mode++)
                {
                    world.SetFxPreviewModes(mode == 2, mode == 1); world.ResetFxPresentation();
                    GateAim(station.TransformPoint(new Vector3(mode == 0 ? -.85f : 0, .05f, -2.15f)), station.TransformPoint(new Vector3(0,1.5f,-.1f)));
                    string name = mode == 0 ? "day-angle-cosmetic" : mode == 1 ? "low-cosmetic" : "reduced-cosmetic";
                    bool started = false;
                    yield return OrganicClip(name, 2.6f, age =>
                    {
                        if (!started && age >= .5f) { started = true; Check(world.TryFxCue(FxCueId.HatchFirst, 1), "One labeled cosmetic release / " + name); }
                        world.Animate(Time.time, Time.deltaTime);
                    });
                    Check(FxAuthority() == authority, "Cosmetic fixture leaves stock, cycle and payment unchanged / " + name);
                }
                world.SetFxPreviewModes(false, false); world.ResetFxPresentation();
                Check(world.TryFxCue(FxCueId.HatchFirst, 1), "Start bounded interruption fixture");
                world.Animate(Time.time,.18f); world.Animate(Time.time,.01f); Check(OrganicHatchParticles() > 0, "Staggered incubator-owned emission has begun");
                shop.SetPanel(ShopPanel.Pause); world.Animate(Time.time,.2f);
                Check(pool.ActiveCount == 0 && world.LabHatchRemaining == 0, "Pause cancels both live motes and unspawned tail"); shop.SetPanel(ShopPanel.None);
                world.Animate(Time.time,.6f); Check(OrganicHatchParticles() == 0 && world.LabHatchRemaining == 0, "Canceled staggered release never resumes while other live owners can animate");
                world.ResetFxPresentation(); world.TryFxCue(FxCueId.HatchFirst, 1); world.Animate(Time.time,.18f); world.Animate(Time.time,.06f);
                VocabularyRasterAudit("organic-actual-mask");
                world.CancelFxTransient(); Check(FxAuthority() == authority, "All revision fixtures remain presentation-only");
            }
            finally { shop.FreezeFxVocabularyForValidation = false; world.SetFxPreviewModes(false,false); world.ResetFxPresentation(); }
            Check(shop.SaveNow(), "Final isolated organic route saves");
        }
    }
}
#endif
