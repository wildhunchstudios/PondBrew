#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class GrowthFrame
        {
            public float seconds, remaining, progress, bloomFill, flowFill;
            public int active, hatches, bloomUnits, flowUnits, incubatorAmbient, bloomAmbient, flowAmbient;
            public Color bloomTint, flowTint;
            public Vector3 scale;
            public string phase;
        }
        [Serializable] sealed class GrowthMotionReport { public string name; public int peak; public GrowthFrame[] frames; }
        [Serializable] sealed class GrowthFitReport
        { public string specimen; public int samples, meshes, vertices; public float maxEllipsoid; public Vector3 min, max; }
        IEnumerator GrowthClip(string name, float seconds, Action<float> action = null, int cap = 96)
        {
            var rows = new List<GrowthFrame>(); int peak = 0; var properties = new MaterialPropertyBlock();
            Action<float> sample = age =>
            {
                action?.Invoke(age);
                var world = shop.World; var inc = shop.Model.Incubator; var creature = world.GrowthCreatureForValidation;
                peak = Mathf.Max(peak, world.FxPool.ActiveCount);
                var bloom = world.GrowerMediumForValidation(CultureKind.Bloom);var flow = world.GrowerMediumForValidation(CultureKind.Flow);
                bloom.GetComponent<MeshRenderer>().GetPropertyBlock(properties);Color bloomTint=properties.GetColor("_BaseColor");
                flow.GetComponent<MeshRenderer>().GetPropertyBlock(properties);Color flowTint=properties.GetColor("_BaseColor");
                rows.Add(new GrowthFrame { seconds = age, remaining = inc.SecondsRemaining, progress = world.LabGrowthProgress,
                    active = world.FxPool.ActiveCount, hatches = world.LabHatchCount, phase = inc.Phase.ToString(),
                    bloomUnits = shop.Model.GetGrower(CultureKind.Bloom).Units, flowUnits = shop.Model.GetGrower(CultureKind.Flow).Units,
                    bloomFill=bloom.localScale.y,flowFill=flow.localScale.y,bloomTint=bloomTint,flowTint=flowTint,
                    incubatorAmbient=world.GrowthIncubatorAmbientCount,bloomAmbient=world.GrowthGrowerAmbientCount(CultureKind.Bloom),flowAmbient=world.GrowthGrowerAmbientCount(CultureKind.Flow),
                    scale = creature ? creature.localScale : Vector3.zero });
            };
            if (Application.isEditor)
            {
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < seconds) { sample(Time.realtimeSinceStartup - start); yield return null; }
            }
            else yield return ReviewClip(name, seconds, sample);
            File.WriteAllText(Path.Combine(evidence, name + "-motion.json"), JsonUtility.ToJson(new GrowthMotionReport { name = name, peak = peak, frames = rows.ToArray() }, true));
            Check(rows.Count > 1 && peak <= cap, "Actual live state trace stays within current pool cap / " + name + " peak=" + peak);
        }
        IEnumerator GrowthHarvest(CultureKind kind)
        {
            shop.SetPanel(ShopPanel.None);
            var station = StationOf(kind == CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow);
            GateAim(station.transform.TransformPoint(new Vector3(0,.2f,-1.7f)), station.transform.TransformPoint(new Vector3(0,1.2f,0)));
            Physics.SyncTransforms(); yield return null; yield return null;
            Check(shop.Focused == station && shop.ScriptedHarvest == null, "Growth input resolves the real grower / " + kind);
            int before = shop.Model.Available(kind); float deadline = Time.realtimeSinceStartup + 3;
            while (shop.Model.Available(kind) == before)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Growth real held collection / " + kind);
                cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 }); yield return null;
            }
            InputSystem.QueueStateEvent(cycleMouse, new MouseState()); yield return null; yield return null;
            Check(shop.Model.Available(kind) == before + 1 && shop.HarvestProgress == 0, "Normal held collection commits exactly one unit then releases / " + kind);
        }
        void GrowthAim()
        {
            var station = StationOf(StationKind.Incubator).transform;
            GateAim(station.TransformPoint(new Vector3(0,.05f,-2.15f)), station.TransformPoint(new Vector3(0,1.5f,-.1f)));
            Physics.SyncTransforms();
        }
        void GrowthMeshSweep()
        {
            var world = shop.World; var root = world.GrowthCreatureForValidation; var station = StationOf(StationKind.Incubator).transform;
            Check(root && shop.Model.Incubator.Phase == IncubatorPhase.Incubating, "Sweep uses the actual working Pondlet");
            var filters = root.GetComponentsInChildren<MeshFilter>(); var meshes = new PondletMesh[filters.Length];
            var report = new GrowthFitReport { specimen = shop.Model.Incubator.SpecimenId, meshes = filters.Length, min = Vector3.one * 100, max = Vector3.one * -100 };
            for (int i = 0; i < filters.Length; i++) { meshes[i] = new PondletMesh { Root = filters[i].transform, Vertices = filters[i].sharedMesh.vertices }; report.vertices += meshes[i].Vertices.Length; }
            string authority = FxAuthority(); Vector3 position = station.position; Quaternion rotation = station.rotation;
            for (int reduced = 0; reduced < 2; reduced++)
            {
                world.SetFxPreviewModes(reduced == 1, false);
                for (int growth = 0; growth <= 10; growth++) for (int phase = 0; phase < 8; phase++)
                {
                    world.SampleGrowthPoseForValidation(growth / 10f, phase * .95f); report.samples++;
                    foreach (var mesh in meshes) foreach (var v in mesh.Vertices)
                    {
                        Vector3 p = station.InverseTransformPoint(mesh.Root.TransformPoint(v)); report.min = Vector3.Min(report.min,p); report.max = Vector3.Max(report.max,p);
                        Vector3 q = new Vector3(p.x/.69f,(p.y-1.48f)/.48f,p.z/.49f); report.maxEllipsoid = Mathf.Max(report.maxEllipsoid,q.sqrMagnitude);
                    }
                }
            }
            world.SetFxPreviewModes(false,false); world.Animate(Time.time,0);
            File.WriteAllText(Path.Combine(evidence,"growth-swept-mesh.json"),JsonUtility.ToJson(report,true));
            Check(report.samples == 176 && report.vertices > 0 && report.maxEllipsoid < 1, "Every actual mesh vertex fits inner vessel across176 growth/phase/comfort poses / " + report.maxEllipsoid);
            Check(FxAuthority() == authority && station.position == position && station.rotation == rotation, "Growth sweep preserves production and fixed outer station");
        }
        IEnumerator GrowthGrower(CultureKind culture)
        {
            // Explicit model-only depletion/capacity setup, followed by ordinary real-time refill.
            shop.FreezeFxVocabularyForValidation = true;
            try
            {
                shop.Model.Tick(60);
                while (shop.Model.GetGrower(culture).Units > 0)
                { Ok(shop.Model.Collect(culture),"Labeled depletion fixture collection / " + culture); Ok(shop.Model.ReleaseCulture(culture),"Labeled depletion fixture release / " + culture); }
                shop.World.Refresh(shop.Model); shop.World.ResetFxPresentation(); shop.World.Animate(Time.time,.1f);
                var station = StationOf(culture == CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow).transform;
                GateAim(station.TransformPoint(new Vector3(0,.2f,-1.7f)),station.TransformPoint(new Vector3(0,1.2f,0)));
                Check(shop.World.GrowthGrowerForValidation(culture).State == FxGrowerVisualState.Depleted, "Real zero units has depleted presentation / " + culture);
                var medium = shop.World.GrowerMediumForValidation(culture); Check(medium,"Existing medium attachment / " + culture);
                float emptyScale = medium.localScale.y;
                string emptyAuthority = FxAuthority(); yield return GateCapture("grower-" + culture.ToString().ToLowerInvariant() + "-depleted-fixture");
                Check(FxAuthority() == emptyAuthority,"Depleted screenshot cannot grant culture");
                // Seed two missing units explicitly; the following4.5s recording never accelerates time.
                int capacity = shop.Model.GetGrower(culture).Capacity;
                shop.Model.Tick((capacity-2)*shop.Config.GrowerRefillSeconds);
                shop.World.Refresh(shop.Model); shop.World.Animate(Time.time,.1f);
                Check(shop.Model.GetGrower(culture).Units == capacity-2 && shop.World.GrowthGrowerForValidation(culture).State == FxGrowerVisualState.Refilling,
                    "Labeled setup leaves exactly two ordinary refill units / " + culture);
                float partialScale = medium.localScale.y;
                Check(partialScale > emptyScale,"Visible medium fill distinguishes partial from depleted / " + culture);
                shop.FreezeFxVocabularyForValidation = false;
                yield return GrowthClip("grower-" + culture.ToString().ToLowerInvariant() + "-refill-live",4.5f,null,32);
                Check(shop.Model.GetGrower(culture).Units == capacity && shop.World.GrowthGrowerForValidation(culture).State == FxGrowerVisualState.Full && medium.localScale.y > partialScale && shop.World.GrowthGrowerAmbientCount(culture)==0,
                    "Ordinary refill reaches real full capacity and visibly higher medium / " + culture);
            }
            finally { shop.FreezeFxVocabularyForValidation = false; }
        }
        IEnumerator FxGrowthScenario()
        {
            limitations.Add("FX-06R-04: synthetic normal held collection and guarded doses; recorded natural10s growth/hatch. Grower depletion and two-missing-unit capacity setup are labeled model fixtures, then normal4.5s refill. Mesh/comfort/lifecycle cases freeze authoritative working state and sample presentation only; station removal is an inactive-owner fixture, not furniture sale. Actual standalone silent HUD videos are not native-input/listening/creative/GPU acceptance.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Growth uses exact isolated save");
            yield return new WaitForSeconds(1); Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionGrowthKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionGrowthMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor)
            {int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w && Screen.height==h,"Exact actual growth HUD resolution");}
            yield return GrowthHarvest(CultureKind.Bloom); yield return GrowthHarvest(CultureKind.Flow); GrowthAim(); shop.SetPanel(ShopPanel.Incubator);
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow),"Guarded real Pondlet dose pair");
            var world=shop.World; var pool=world.FxPool; var audio=shop.GetComponent<ShopAudio>(); int hatches=world.LabHatchCount;
            bool started=false;string specimen=null;float startTime=0,maxCountdownError=0,minScale=10,maxScale=0;bool earlyReady=false;
            yield return GrowthClip("incubation-growth-live",12.5f,age=>
            {
                if(!started && age>=.3f)
                {started=true;Check(shop.BeginManualIncubation(),"Recorded normal incubation starts");startTime=Time.time;specimen=shop.Model.Incubator.SpecimenId;Check(shop.Model.Incubator.SecondsRemaining==shop.Config.IncubationSeconds,"Start retains configured countdown");}
                if(!started)return;
                var inc=shop.Model.Incubator;float elapsed=Time.time-startTime;
                if(inc.Phase==IncubatorPhase.Incubating)
                {
                    maxCountdownError=Mathf.Max(maxCountdownError,Mathf.Abs(inc.SecondsRemaining-Mathf.Max(0,shop.Config.IncubationSeconds-elapsed)));
                    var creature=world.GrowthCreatureForValidation;if(creature){minScale=Mathf.Min(minScale,creature.localScale.x);maxScale=Mathf.Max(maxScale,creature.localScale.x);}
                    if(world.LabHatchCount!=hatches)earlyReady=true;
                }
                else if(elapsed<shop.Config.IncubationSeconds-.1f)earlyReady=true;
            });
            Check(started && !earlyReady && maxCountdownError<.15f && maxScale>minScale*2,"Visible maturation follows unchanged ordinary countdown; no early hatch / error="+maxCountdownError);
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready && shop.Model.Incubator.SpecimenId==specimen && world.LabHatchCount==hatches+1 && shop.Model.IsDiscovered("pondlet"),"Exactly one real saved hatch and same specimen");
            hatches=world.LabHatchCount;int sounds=audio.LabPlaybackCount;
            yield return GrowthClip("ready-quiet-live",2,null,32);
            Check(!world.LabGrowthActive && world.GrowthIncubatorAmbientCount==0 && world.LabHatchRemaining==0 && world.LabHatchCount==hatches && audio.LabPlaybackCount==sounds,"Ready remains quiet without growth/hatch/audio retries");
            Check(shop.SaveNow() && shop.ReloadSaved(),"Reload actual ready state");shop.SetPanel(ShopPanel.None);yield return null;
            Check(shop.Model.Incubator.SpecimenId==specimen && !world.LabGrowthActive && world.LabHatchRemaining==0 && world.LabHatchCount==hatches && audio.LabPlaybackCount==sounds,"Ready reload preserves same output without replay");
            yield return GrowthGrower(CultureKind.Bloom);yield return GrowthGrower(CultureKind.Flow);
            yield return LabTransferReady("tank-a",specimen);
            yield return GrowthHarvest(CultureKind.Bloom);yield return GrowthHarvest(CultureKind.Flow);GrowthAim();shop.SetPanel(ShopPanel.Incubator);
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow) && shop.BeginManualIncubation(),"Start normal second batch for working-state lifecycle");
            yield return new WaitForSeconds(2.5f);
            float remaining=shop.Model.Incubator.SecondsRemaining;shop.SetPanel(ShopPanel.Pause);yield return new WaitForSeconds(.3f);
            Check(shop.Model.Incubator.SecondsRemaining==remaining && pool.ActiveCount==0,"Actual pause freezes real countdown and retires motes");shop.SetPanel(ShopPanel.None);
            shop.FreezeFxVocabularyForValidation=true;
            try
            {
                Check(shop.SaveNow() && shop.ReloadSaved(),"Save/reload actual working batch");shop.SetPanel(ShopPanel.None);world.Animate(Time.time,.1f);
                Check(shop.Model.Incubator.Phase==IncubatorPhase.Incubating && Mathf.Abs(world.LabGrowthProgress-(1-remaining/shop.Config.IncubationSeconds))<.01f && world.LabHatchRemaining==0,"Working reload resumes true maturation without fake hatch");
                GrowthMeshSweep();GrowthAim();string authority=FxAuthority();hatches=world.LabHatchCount;sounds=audio.LabPlaybackCount;
                for(int mode=1;mode<=2;mode++)
                {
                    world.SetFxPreviewModes(mode==2,mode==1);world.ResetFxPresentation();
                    yield return GrowthClip(mode==1?"growth-low-cosmetic":"growth-reduced-cosmetic",3,age=>world.Animate(Time.time,Time.deltaTime),mode==1?8:32);
                    Check(FxAuthority()==authority && world.LabHatchCount==hatches,"Frozen comfort sample preserves countdown/output/stock and no hatch");
                }
                world.SetFxPreviewModes(false,false);world.ResetFxPresentation();world.Animate(Time.time,.3f);
                foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Arrange})
                {shop.SetPanel(panel);world.Animate(Time.time,.5f);Check(pool.ActiveCount==0 && !world.LabGrowthActive && world.LabHatchRemaining==0,"Policy retires ambient without readiness / "+panel);shop.SetPanel(ShopPanel.None);world.Animate(Time.time,.3f);}
                shop.SetFxFocusForValidation(false);world.Animate(Time.time,.5f);Check(pool.ActiveCount==0 && !world.LabGrowthActive,"Focus loss cancels all growth motes");shop.SetFxFocusForValidation(true);world.Animate(Time.time,.3f);
                var station=StationOf(StationKind.Incubator).transform;Vector3 position=station.position;var owner=world.FxCues.Find("incubator");station.position+=Vector3.right*.05f;world.Animate(Time.time,.1f);
                Check(!world.FxCues.IsValid(owner) && world.LabHatchRemaining==0,"Moved working owner retires old identity without hatch");station.position=position;world.Refresh(shop.Model);world.ResetFxPresentation();
                owner=world.FxCues.Find("incubator");station.gameObject.SetActive(false);world.Animate(Time.time,.1f);
                Check(!world.FxCues.IsValid(owner) && world.LabHatchRemaining==0,"Inactive removed-owner fixture cannot emit fake readiness");station.gameObject.SetActive(true);world.Refresh(shop.Model);world.ResetFxPresentation();
                world.SetFxEnabledForValidation(false);world.Animate(Time.time,5);Check(pool.ActiveCount==0 && FxAuthority()==authority,"Missing/disabled visual effects leave authority intact");world.SetFxEnabledForValidation(true);world.ResetFxPresentation();
                shop.enabled=false;Check(pool.ActiveCount==0,"Session disable retires all growth effects");shop.enabled=true;shop.SetFxFocusForValidation(true);
                Check(FxAuthority()==authority && world.LabHatchCount==hatches && audio.LabPlaybackCount==sounds,"All frozen lifecycle paths preserve countdown, stock, output and sound count");
                world.RefreshLabModuleFx(LabModuleVisualState.Blocked,"growth-blocked-fixture");int module=world.LabModuleCueCount;world.Animate(Time.time,1);
                Check(world.LabModuleCueCount==module,"Explicit blocked-module presentation fixture has no retry success accent");
            }
            finally {shop.FreezeFxVocabularyForValidation=false;world.SetFxPreviewModes(false,false);world.SetFxEnabledForValidation(true);world.ResetFxPresentation();shop.SetPanel(ShopPanel.None);}
            Check(pool.OwnedCount==96 && pool.OwnedMeshCount==5 && pool.OwnedMaterialCount==1 && pool.Root.GetComponentsInChildren<Collider>(true).Length==0 && shop.GetComponents<AudioSource>().Length==3,"Growth retains fixed pool/mesh/material/collider/audio budgets");
            Check(shop.SaveNow(),"Final isolated working state saves");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
