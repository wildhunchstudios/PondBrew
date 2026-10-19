#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class LabAudioMetric { public string clip; public int samples, channels, frequency; public float peak, rms, dc, first, last; }
        [Serializable] sealed class LabAudioReport { public string provenance = "Original local procedural PCM from ShopAudioLabFx; decoded clip data. No audible listening claim."; public LabAudioMetric[] clips; public float conservativeConcurrentPeak; }
        [Serializable] sealed class LabMotionSample { public string state; public float secondsRemaining, fluidScaleY, needleZ; public int activeMotes, moduleCues, hatches; }
        void LabRecord(string name)
        {
            var station = StationOf(StationKind.Incubator).transform;
            var fluid = station.Find("Incubator fluid"); Transform needle=null;
            foreach(var t in station.GetComponentsInChildren<Transform>(true))if(t.name=="FX / Hatchery progress needle")needle=t;
            Check(fluid && needle && Mathf.DeltaAngle(0,needle.localEulerAngles.z)>=-12.01f && Mathf.DeltaAngle(0,needle.localEulerAngles.z)<=.01f,"Actual incubator fluid and bounded progress needle / "+name);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new LabMotionSample {
                state=shop.Model.Incubator.Phase.ToString(),secondsRemaining=shop.Model.Incubator.SecondsRemaining,
                fluidScaleY=fluid ? fluid.localScale.y : 0,needleZ=needle ? Mathf.DeltaAngle(0,needle.localEulerAngles.z) : 0,
                activeMotes=shop.World.FxPool.ActiveCount,moduleCues=shop.World.LabModuleCueCount,hatches=shop.World.LabHatchCount },true));
        }
        void LabAudioEvidence()
        {
            var audio=shop.GetComponent<ShopAudio>(); var clips=audio.LabValidationClips();
            var report=new LabAudioReport { clips=new LabAudioMetric[clips.Length] };
            for(int c=0;c<clips.Length;c++)
            {
                var clip=clips[c]; var data=new float[clip.samples*clip.channels]; Check(clip.GetData(data,0),"Read exact local lab clip / "+clip.name);
                double square=0,sum=0;float peak=0;foreach(float v in data){square+=v*v;sum+=v;peak=Mathf.Max(peak,Mathf.Abs(v));}
                var m=new LabAudioMetric {clip=clip.name,samples=clip.samples,channels=clip.channels,frequency=clip.frequency,peak=peak,rms=(float)Math.Sqrt(square/data.Length),dc=(float)(sum/data.Length),first=data[0],last=data[data.Length-1]};report.clips[c]=m;
                Check(m.channels==1 && m.frequency==22050 && clip.length<=1 && peak<=.25f && m.rms<=.12f && Math.Abs(m.dc)<.005f && Math.Abs(m.first)<.005f && Math.Abs(m.last)<.005f,"PCM envelope, format and endpoints meet contract / "+clip.name);
                using(var writer=new BinaryWriter(File.Create(Path.Combine(evidence,"lab-audio-"+c+".wav"))))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(22050);writer.Write(44100);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(data.Length*2);foreach(float v in data)writer.Write((short)Mathf.RoundToInt(v*32767));
                }
            }
            float servicePeak=0,comedyPeak=0;var previous=audio.ValidationClips();
            for(int c=0;c<previous.Length;c++){var data=new float[previous[c].samples];previous[c].GetData(data,0);foreach(float v in data){if(c<4)servicePeak=Mathf.Max(servicePeak,Math.Abs(v));else comedyPeak=Mathf.Max(comedyPeak,Math.Abs(v));}}
            report.conservativeConcurrentPeak=Mathf.Max(servicePeak,.20f)+comedyPeak+.30f*.20f;
            Check(report.conservativeConcurrentPeak<1 && shop.GetComponents<AudioSource>().Length==5,"Worst-case additive short-voice peak remains below1, five bounded voices (3 short + 2 loops) / "+report.conservativeConcurrentPeak);
            File.WriteAllText(Path.Combine(evidence,"lab-audio.json"),JsonUtility.ToJson(report,true));
        }
        IEnumerator FxLabHarvest(CultureKind kind, bool capture)
        {
            shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;
            var station=StationOf(kind==CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow);
            GateAim(station.transform.TransformPoint(new Vector3(0,.2f,-1.7f)),station.transform.TransformPoint(new Vector3(0,1.2f,0)));Physics.SyncTransforms();
            yield return null;yield return null;
            Check(shop.Focused==station && shop.ScriptedHarvest==null,"Physics focus resolves real grower / "+kind);
            int before=shop.Model.Available(kind),spawned=shop.World.FxPool.SpawnedCount;
            float deadline=Time.realtimeSinceStartup+3;
            while(shop.Model.Available(kind)==before)
            {
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("FX-03 real held collection / "+kind);
                cycleMouse.MakeCurrent();InputSystem.QueueStateEvent(cycleMouse,new MouseState {buttons=1});yield return null;
                if(capture && shop.HarvestProgress>.2f){capture=false;yield return Capture("fx03-intake-"+kind);}
            }
            InputSystem.QueueStateEvent(cycleMouse,new MouseState());yield return null;yield return null;
            Check(shop.Model.Available(kind)==before+1 && shop.World.FxPool.SpawnedCount>spawned,"Ordinary0.6s held collection commits one unit and pooled intake / "+kind);
            Check(!shop.World.CollectionActive && shop.World.CollectionParticleCountForValidation==0,"Release clears owned intake immediately; committed chamber settle and grower ambience remain / "+kind);
        }
        IEnumerator FxLabPrepare(bool capture)
        {
            yield return FxLabHarvest(CultureKind.Bloom,capture);yield return FxLabHarvest(CultureKind.Flow,capture);
            var station=StationOf(StationKind.Incubator).transform;
            GateAim(station.TransformPoint(new Vector3(0,.2f,-1.5f)),station.TransformPoint(new Vector3(0,1.48f,0)));Physics.SyncTransforms();shop.SetPanel(ShopPanel.Incubator);
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.Model.Staged(CultureKind.Bloom)==1 && shop.World.LabDoseId==FxCueId.DoseCommitted && shop.World.LabDoseRemaining>0,"Guarded Bloom dose visible inside the real modal");
            if(capture)yield return GateCapture("fx03-dose-bloom");
            Check(shop.ReturnCultureDose(CultureKind.Bloom) && shop.Model.Available(CultureKind.Bloom)==1 && shop.World.LabDoseId==FxCueId.DoseReturned,"Returning a dose presents its actual direction");
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow),"Manually measure exact1Bloom/1Flow Pondlet recipe");
            if(capture)yield return GateCapture("fx03-dose-flow");
            string authority=FxAuthority();int played=shop.GetComponent<ShopAudio>().LabPlaybackCount;
            Check(!shop.DoseCulture(CultureKind.Flow) && FxAuthority()==authority && shop.World.LabDoseId==FxCueId.HandRefused && shop.GetComponent<ShopAudio>().LabPlaybackCount==played,"Refused empty dose has contextual refusal and no accepted sound/delta");
            if(capture)yield return GateCapture("fx03-dose-refused");
            Check(shop.BeginManualIncubation() && shop.Model.Incubator.Phase==IncubatorPhase.Incubating && shop.Panel==ShopPanel.None && shop.Model.Incubator.SecondsRemaining>9,"Actual guarded start uses unchanged ten-second countdown");
        }
        IEnumerator FxLabScenario()
        {
            limitations.Add("FX-03: synthetic held mouse through normal Physics focus/input, scripted operator cameras and guarded dose/menu commands; real ten-second batch countdowns. Isolated credit fixture only for buying existing upgrade/module. Direct model fixtures below are explicitly labelled. Screens, PCM exports and automated playback requests are not native input, audible listening or human FX-06 approval. No hands/all-species work.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX-03 exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Isolated explicit tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionFxLabKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionFxLabMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor){int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w && Screen.height==h,"Actual requested Windows render resolution");}
            var world=shop.World;var pool=world.FxPool;var audio=shop.GetComponent<ShopAudio>();
            Check(pool.OwnedCount==96 && pool.Root.GetComponentsInChildren<Collider>(true).Length==0,"Fixed96 collider-free pool");LabAudioEvidence();
            var focus=StationOf(StationKind.Bloom).transform;
            GateAim(focus.TransformPoint(new Vector3(0,.2f,-1.7f)),focus.TransformPoint(new Vector3(0,1.2f,0)));yield return null;yield return null;
            InputSystem.QueueStateEvent(cycleMouse,new MouseState {buttons=1});yield return new WaitForSeconds(.2f);
            Check(pool.ActiveCount>0,"Intake only starts while physically focused and held");
            shop.Player.View.transform.LookAt(shop.Player.View.transform.position+Vector3.up);yield return null;yield return null;
            Check(shop.Focused==null && pool.ActiveCount==0,"Target/line-of-sight loss immediately cancels old intake");InputSystem.QueueStateEvent(cycleMouse,new MouseState());yield return null;
            yield return FxLabPrepare(true);
            int hatch=world.LabHatchCount;string specimen=shop.Model.Incubator.SpecimenId;
            var workingStation=StationOf(StationKind.Incubator).transform;
            GateAim(workingStation.TransformPoint(new Vector3(0,.2f,-2.2f)),workingStation.TransformPoint(new Vector3(0,1.2f,0)));
            yield return null;
            LabRecord("fx03-working-a");yield return GateCapture("fx03-working-a");yield return new WaitForSeconds(.18f);LabRecord("fx03-working-b");yield return GateCapture("fx03-working-b");
            float remaining=shop.Model.Incubator.SecondsRemaining;shop.SetPanel(ShopPanel.Pause);yield return new WaitForSeconds(.3f);
            Check(Mathf.Approximately(shop.Model.Incubator.SecondsRemaining,remaining) && pool.ActiveCount==0 && world.LabHatchRemaining==0,"Actual pause freezes countdown and clears transient presentation");shop.SetPanel(ShopPanel.None);
            shop.SetFxFocusForValidation(false);Check(pool.ActiveCount==0 && !world.TryFxCue(FxCueId.HatchFirst,100),"Focus loss rejects late hatch cue");shop.SetFxFocusForValidation(true);
            yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"real first hatch");
            Check(world.LabHatchCount==hatch+1 && world.LabHatchRemaining>1 && shop.Model.IsDiscovered("pondlet") && shop.Model.Incubator.SpecimenId==specimen,"First genuine Pondlet maturation has one bounded first-hatch accent and stable ID");
            LabRecord("fx03-first-hatch");yield return GateCapture("fx03-first-hatch");yield return new WaitForSeconds(1.25f);
            Check(world.LabHatchRemaining==0 && pool.ActiveCount==0,"Hatch action expires without looping");
            Check(shop.SaveNow(),"Save ready specimen");hatch=world.LabHatchCount;playedReady=audio.LabPlaybackCount;
            Check(shop.ReloadSaved() && shop.Model.Incubator.SpecimenId==specimen && world.LabHatchCount==hatch && audio.LabPlaybackCount==playedReady && world.LabHatchRemaining==0,"Loaded Ready reconstructs silently without hatch/sound replay");shop.SetPanel(ShopPanel.None);
            yield return LabTransferReady("tank-a",specimen);
            yield return FxLabPrepare(false);hatch=world.LabHatchCount;
            yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"real repeated hatch");
            Check(world.LabHatchCount==hatch+1 && world.LabHatchRemaining>0 && world.LabHatchRemaining<=FxHatchChoreography.RepeatDuration,"Second natural Pondlet uses ordinary repeat-hatch accent");yield return GateCapture("fx03-repeat-hatch");specimen=shop.Model.Incubator.SpecimenId;yield return LabTransferReady("tank-b",specimen);
            yield return FxLabPrepare(false);yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"full-display hatch");
            specimen=shop.Model.Incubator.SpecimenId;Check(!shop.TransferToDisplay("tank-a").Success && shop.Model.Incubator.SpecimenId==specimen && shop.DisplayedStockCount()==2,"Full displays retain exact ready output without duplicate fish");yield return GateCapture("fx03-output-held");
            Ok(shop.Model.SellFromTank("tank-b"),"Labelled core-only release fixture, not a customer sale");Ok(shop.Model.TransferToTank("tank-b"),"Move protected output for collector fixture");world.Refresh(shop.Model);Check(shop.SaveNow(),"Save prepared collector fixture");
            StaffCreditFixture(200,"FX-03 module unlock only");yield return AutomationEnter();Ok(shop.Perform(shop.Model.BuyUpgrade()),"Existing siphon upgrade");Check(shop.PurchaseAutomationModule(true),"Buy existing collector once");yield return null;
            Check(world.LabModuleState==LabModuleVisualState.Off,"Purchased collector has still off state");yield return GateCapture("fx03-module-off");
            Check(shop.SetAutomationEnabled(true,true) && shop.SelectAutomationRecipe("pondlet"),"Enable collector for discovered recipe");
            Check(shop.SetAutomationPaused(true) && shop.RequestAutomationBatch(),"Pending helper can remain explicitly paused");yield return null;
            Check(world.LabModuleState==LabModuleVisualState.Paused,"Paused pending helper exposes calm explicit paused state");int module=world.LabModuleCueCount;string request=shop.Automation.RequestId;
            yield return new WaitForSeconds(.7f);Check(world.LabModuleCueCount==module,"Blocked helper never pulses on retries");yield return GateCapture("fx03-module-blocked");
            Check(shop.SetAutomationPaused(false),"Resume same collector request");shop.SetPanel(ShopPanel.None);yield return AutomationWait(()=>world.LabModuleCueCount>module,3,"durable module collection");
            Check(world.LabModuleCueCount==module+1 && shop.Automation.RequestId==request && shop.Model.Available(CultureKind.Bloom)==1,"One saved collection unit gives one module valve cue on same request");Transform valve=null;foreach(var t in StationOf(StationKind.Upgrade).GetComponentsInChildren<Transform>(true))if(t.name=="FX / Collector module valve")valve=t;
            Check(valve && Mathf.Abs(Mathf.DeltaAngle(0,valve.localEulerAngles.z))>.01f && Mathf.Abs(Mathf.DeltaAngle(0,valve.localEulerAngles.z))<=6.01f && valve.gameObject.activeInHierarchy,"Real module valve remains bounded in clear world view");
            File.WriteAllText(Path.Combine(evidence,"module-attachment.txt"),"Source="+valve.localPosition+"\nangle="+Mathf.DeltaAngle(0,valve.localEulerAngles.z)+"\nrequest="+request+"\n");
            yield return GateCapture("fx03-module-collected");
            yield return AutomationWait(()=>!shop.Automation.IsPending,4,"collector-only request completion");
            Check(shop.Model.Available(CultureKind.Bloom)==1 && shop.Model.Available(CultureKind.Flow)==1 && shop.Model.Staged(CultureKind.Bloom)==0,"Collector has no pretend player dose or hand");
            yield return AutomationEnter();
            Check(shop.CancelAutomationBatch()==false,"Completed request cannot be canceled twice");AutomationReleaseFixture();world.Refresh(shop.Model);
            Check(shop.RequestAutomationBatch(),"New collector request has new identity");var oldOwner=world.FxCues.Find("lab-collector");yield return null;Check(!world.FxCues.IsValid(oldOwner),"Request replacement retires prior module owner");
            byte[] durable=File.ReadAllBytes(shop.SavePath);module=world.LabModuleCueCount;string ingredients=TechnicianIngredients();TutorialBlockSave();
            try{shop.Advance(shop.Model.CollectionSecondsPerUnit+.01f);Check(shop.AutomationBlockedBySave && world.LabModuleCueCount==module && TechnicianIngredients()==ingredients && Convert.ToBase64String(File.ReadAllBytes(shop.SavePath))==Convert.ToBase64String(durable),"Failed automation write has no inventory or valve cue and preserves durable bytes");}finally{TutorialUnblockSave();}
            Check(shop.RetryAutomation() && shop.CancelAutomationBatch(),"Explicit recovery clears latch and cancels request safely");
            string authority=FxAuthority();world.SetFxPreviewModes(true,true);world.ResetFxPresentation();world.TryFxCue(FxCueId.HatchRepeat,1);world.Animate(1,.01f);Check(pool.ActiveCount<=1 && FxAuthority()==authority,"Low/reduced single hatch mote preserves authority");yield return GateCapture("fx03-low-reduced");world.SetFxPreviewModes(false,false);world.ResetFxPresentation();
            authority=FxAuthority();
            for(uint i=1;i<=100;i++){world.TryFxCue(FxCueId.DoseCommitted,i);world.Animate(i,.45f);}Check(pool.OwnedCount==96 && pool.ActiveCount==0 && FxAuthority()==authority,"100 reuses keep bounded topology and exact authority");world.ResetFxPresentation();
            world.TryFxCue(FxCueId.HatchRepeat,1);shop.SetPanel(ShopPanel.Arrange);Check(pool.ActiveCount==0 && world.LabHatchRemaining==0,"Arrange interruption clears hatch pose");shop.SetPanel(ShopPanel.None);
            int played=audio.LabPlaybackCount;TutorialBlockSave();try{shop.Harvest(CultureKind.Bloom,shop.Model.CollectionSecondsPerUnit+.01f);Check(shop.SaveError!=null && audio.LabPlaybackCount==played && pool.ActiveCount==0,"Failed manual write suppresses new success audio and effects");}finally{TutorialUnblockSave();}
            Check(shop.ReloadSaved(),"Recover saved state after manual write obstruction");shop.SetPanel(ShopPanel.None);
            Check(shop.SaveNow(),"Save before explicitly accelerated capacity fixtures");byte[] capacitySlot=File.ReadAllBytes(shop.SavePath);
            shop.Model.Tick(60);while(shop.Model.GetGrower(CultureKind.Bloom).Units>0){Ok(shop.Model.Collect(CultureKind.Bloom),"Core-only grower depletion fixture");Ok(shop.Model.ReleaseCulture(CultureKind.Bloom),"Core-only release keeps depletion fixture recoverable");}world.Refresh(shop.Model);world.CancelFxTransient();
            focus=StationOf(StationKind.Bloom).transform;GateAim(focus.TransformPoint(new Vector3(0,.2f,-1.7f)),focus.TransformPoint(new Vector3(0,1.2f,0)));yield return null;yield return null;
            InputSystem.QueueStateEvent(cycleMouse,new MouseState {buttons=1});yield return new WaitForSeconds(.12f);
            Check(shop.HarvestProgress==0 && pool.ActiveCount==0 && world.FxCues.TryState(world.FxCues.Find("bloom"),out var emptyState) && emptyState==FxSteadyState.Empty,"Empty grower has no collectable stream");InputSystem.QueueStateEvent(cycleMouse,new MouseState());yield return null;
            shop.Model.Tick(60);foreach(var kind in new[]{CultureKind.Bloom,CultureKind.Flow})while(shop.Model.ReservedCultureCount<shop.Model.Capacity && shop.Model.GetGrower(kind).Units>0)Ok(shop.Model.Collect(kind),"Core-only full reservoir fixture");world.Refresh(shop.Model);world.CancelFxTransient();
            InputSystem.QueueStateEvent(cycleMouse,new MouseState {buttons=1});yield return new WaitForSeconds(.12f);Check(shop.Model.ReservedCultureCount==shop.Model.Capacity && shop.HarvestProgress==0 && pool.ActiveCount==0,"Full siphon stops intake without overflow");InputSystem.QueueStateEvent(cycleMouse,new MouseState());yield return null;
            File.WriteAllBytes(shop.SavePath,capacitySlot);Check(shop.ReloadSaved(),"Restore exact slot after labelled capacity fixtures");shop.SetPanel(ShopPanel.None);
            yield return new WaitForSeconds(1);Check(audio.LabModule(),"Unprotected short module clip can play");audio.Feedback(true);Check(!audio.LabModule() && audio.ServicePlaying && audio.ActiveVoiceCount<=2,"Service cue protected from module replacement");audio.StopLabAudio();
            world.TryFxCue(FxCueId.HatchRepeat,1);shop.enabled=false;Check(pool.ActiveCount==0 && world.LabHatchRemaining==0 && !audio.LabPlaying,"Disable cleans poses, pooled effects and module audio");shop.enabled=true;yield return null;shop.SetFxFocusForValidation(true);Check(shop.SaveNow(),"Final isolated save valid");
        }
        int playedReady;
    }
}
#endif
