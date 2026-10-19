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
        [Serializable] sealed class CollectionFrame
        {
            public float seconds, harvestProgress, response, sourceError, mouthError;
            public int available, growerUnits, particles, accepted, poolActive;
            public bool active;
            public Vector3 outlet, mouth;
        }
        [Serializable] sealed class CollectionMotionReport
        {
            public string name;public int capacity, samplesWithParticles, peak;
            public float cadence, maxSourceError, maxMouthError, mouthTravel;
            public CollectionFrame[] frames;
        }
        void CollectionMouse(bool held)
        { cycleMouse.MakeCurrent();InputSystem.QueueStateEvent(cycleMouse,new MouseState { buttons=(ushort)(held?1:0) }); }
        void CollectionAim(CultureKind culture,float sideways=0,float turn=0,float distance=1.7f)
        {
            var station=StationOf(culture==CultureKind.Bloom?StationKind.Bloom:StationKind.Flow).transform;
            GateAim(station.TransformPoint(new Vector3(sideways,.2f,-distance)),station.TransformPoint(new Vector3(turn,1.2f,0)));
            Physics.SyncTransforms();
        }
        IEnumerator CollectionRelease()
        {CollectionMouse(false);yield return null;yield return null;Check(!shop.World.CollectionActive && shop.World.CollectionParticleCountForValidation==0 && shop.HarvestProgress==0,"Released input retires only collection stream and partial clock");}
        IEnumerator CollectionClearBuffer()
        {
            // Normal guarded release commands, not a capacity mutation.
            DoseAim();
            foreach(var culture in new[]{CultureKind.Bloom,CultureKind.Flow})
                while(shop.Model.Available(culture)>0)Check(shop.ReleaseCulture(culture),"Guarded surplus release / "+culture);
            shop.SetPanel(ShopPanel.None);yield return null;
        }
        IEnumerator CollectionClip(string name,CultureKind culture,float seconds,Action<float> action,bool reduced,int particleCap)
        {
            var rows=new List<CollectionFrame>();var report=new CollectionMotionReport { name=name,capacity=shop.Model.Capacity,cadence=shop.Model.CollectionSecondsPerUnit };
            var station=StationOf(culture==CultureKind.Bloom?StationKind.Bloom:StationKind.Flow).transform;
            var outlet=station.Find("Outlet opening");var mouth=shop.World.ToolRoot.Find("Siphon chassis/Collection nozzle/Nozzle light");
            Check(outlet && mouth,"Independently resolved actual outlet and nozzle light / "+name);
            Vector3 firstMouth=mouth.position;
            Action<float> sample=age=>
            {
                var world=shop.World;float sourceError=0,mouthError=0;
                int count=world.CollectionParticleCountForValidation;
                for(int i=0;i<count;i++)
                {
                    if(!world.FxPool.TryGetParticleForValidation(world.CollectionOwnerForValidation,i,out var startPoint,out var endPoint,out var position,out var particleAge,out var lifetime))
                        throw new InvalidOperationException("Active collection particle disappeared during synchronous inspection");
                    sourceError=Mathf.Max(sourceError,Vector3.Distance(startPoint,outlet.position));
                    if(!reduced)mouthError=Mathf.Max(mouthError,Vector3.Distance(endPoint,mouth.position));
                    if(float.IsNaN(position.x)||float.IsInfinity(position.x)||particleAge<0||particleAge>lifetime)throw new InvalidOperationException("Nonfinite or expired collection particle");
                }
                if(count>0)report.samplesWithParticles++;
                report.peak=Mathf.Max(report.peak,count);report.maxSourceError=Mathf.Max(report.maxSourceError,sourceError);report.maxMouthError=Mathf.Max(report.maxMouthError,mouthError);
                report.mouthTravel=Mathf.Max(report.mouthTravel,Vector3.Distance(firstMouth,mouth.position));
                rows.Add(new CollectionFrame {seconds=age,harvestProgress=shop.HarvestProgress,response=world.CollectionResponse,sourceError=sourceError,mouthError=mouthError,
                    available=shop.Model.Available(culture),growerUnits=shop.Model.GetGrower(culture).Units,particles=count,accepted=world.CollectionAcceptedCount,poolActive=world.FxPool.ActiveCount,
                    active=world.CollectionActive,outlet=outlet.position,mouth=mouth.position});
                // Sample completed-frame state before changing the next operator view/input.
                action?.Invoke(age);
            };
            if(Application.isEditor)
            {float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<seconds){sample(Time.realtimeSinceStartup-began);yield return null;}}
            else yield return ReviewClip(name,seconds,sample);
            report.frames=rows.ToArray();File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(report,true));
            Check(report.samplesWithParticles>3 && report.peak<=particleCap && report.maxSourceError<=.066f && (reduced || report.maxMouthError<.015f),
                "Visible bounded stream fits actual outlet/moving mouth / "+name+" peak="+report.peak+" start="+report.maxSourceError+" end="+report.maxMouthError);
            Check(report.mouthTravel>.04f,"Actual mouth moves along scripted operator arc / "+name);
        }
        IEnumerator CollectionLive(CultureKind culture,string name,bool low=false,bool reduced=false)
        {
            yield return CollectionClearBuffer();shop.World.SetFxPreviewModes(reduced,low);CollectionAim(culture);yield return null;yield return null;
            var world=shop.World;var station=StationOf(culture==CultureKind.Bloom?StationKind.Bloom:StationKind.Flow);
            Check(shop.Focused==station && shop.ScriptedHarvest==null,"Actual Physics focus and ordinary held path / "+name);
            int before=shop.Model.Available(culture),accepted=world.CollectionAcceptedCount,sounds=shop.GetComponent<ShopAudio>().LabPlaybackCount;
            int stage=0;float heldTime=0,firstUnit=-1;bool earlyUnit=false;float cadence=shop.Model.CollectionSecondsPerUnit;
            yield return CollectionClip(name,culture,3.8f,age=>
            {
                if(stage==0 && age>=.35f){CollectionMouse(true);stage=1;}
                if(stage==1 && age>=.46f){CollectionMouse(false);stage=2;}
                if(stage==2 && age>=.9f)
                {earlyUnit=shop.Model.Available(culture)!=before;CollectionMouse(true);heldTime=Time.time;stage=3;}
                if(stage==3)
                {
                    float motion=Mathf.Clamp01((age-.9f)/1.2f);
                    CollectionAim(culture,Mathf.Sin(motion*Mathf.PI*2)*.13f,Mathf.Sin(motion*Mathf.PI)*.045f);
                    if(shop.Model.Available(culture)>before && firstUnit<0)firstUnit=Time.time-heldTime;
                    if(shop.Model.Available(culture)>=before+3){CollectionMouse(false);stage=4;}
                }
            },reduced,low?24:64);
            yield return CollectionRelease();
            Check(stage==4 && !earlyUnit && shop.Model.Available(culture)==before+3 && world.CollectionAcceptedCount==accepted+3 && shop.GetComponent<ShopAudio>().LabPlaybackCount==sounds+3,
                "Early release gives no unit; sustained hold commits exactly3 saved units/pulses/sounds / "+name);
            Check(firstUnit>=cadence-.04f && firstUnit<cadence+.15f,"First committed unit retains normal cadence / "+name+" actual="+firstUnit+" configured="+cadence);
            yield return new WaitForSeconds(.4f);Check(world.CollectionResponse==0,"Accepted chamber response settles / "+name);
            uint sequence=shop.FxActionSequenceForValidation;accepted=world.CollectionAcceptedCount;
            Check(!world.TryFxCue(FxCueId.CollectedUnit,sequence) && world.CollectionAcceptedCount==accepted,"Duplicate saved-result callback cannot replay chamber pulse");
            world.SetFxPreviewModes(false,false);
        }
        void CollectionSupplyFixture()
        {
            // Bounded edge-case setup only, never used to claim natural production.
            shop.StopHarvest();shop.World.SetHarvesting(false,null);
            foreach(var culture in new[]{CultureKind.Bloom,CultureKind.Flow})while(shop.Model.Available(culture)>0)Ok(shop.Model.ReleaseCulture(culture),"Fixture-only buffer clear / "+culture);
            shop.Model.Tick(60);shop.World.Refresh(shop.Model);shop.World.ResetFxPresentation();
            Check(shop.SaveNow(),"Save explicit edge-case supply fixture");
        }
        IEnumerator CollectionBeginEdge()
        {
            shop.SetPanel(ShopPanel.None);shop.SetFxFocusForValidation(true);CollectionMouse(false);CollectionSupplyFixture();CollectionAim(CultureKind.Bloom);yield return null;yield return null;
            CollectionMouse(true);yield return new WaitForSeconds(.12f);
            Check(shop.World.CollectionActive && shop.World.CollectionParticleCountForValidation>0 && shop.Model.Available(CultureKind.Bloom)==0,"Edge fixture starts real held stream before any unit");
        }
        IEnumerator CollectionEdges()
        {
            var world=shop.World;var audio=shop.GetComponent<ShopAudio>();
            foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Incubator,ShopPanel.Journal,ShopPanel.Arrange})
            {
                yield return CollectionBeginEdge();shop.SetPanel(panel);CollectionMouse(false);yield return null;yield return null;
                Check(!world.CollectionActive && world.CollectionParticleCountForValidation==0 && world.CollectionResponse==0 && shop.Model.Available(CultureKind.Bloom)==0,"Panel interruption cancels stream without a unit / "+panel);
                shop.SetPanel(ShopPanel.None);
            }
            yield return CollectionBeginEdge();shop.SetFxFocusForValidation(false);CollectionMouse(false);
            Check(!world.CollectionActive && world.CollectionParticleCountForValidation==0,"Focus loss immediately cancels intake");yield return null;yield return null;shop.SetFxFocusForValidation(true);
            foreach(int boundary in new[]{0,1,2})
            {
                yield return CollectionBeginEdge();GameObject obstruction=null;
                try
                {
                    if(boundary==0)shop.Player.View.transform.LookAt(shop.Player.View.transform.position+Vector3.up);
                    else if(boundary==1)CollectionAim(CultureKind.Bloom,0,0,4.4f);
                    else
                    {
                        obstruction=new GameObject("Collection validation-only sight blocker");var collider=obstruction.AddComponent<BoxCollider>();collider.size=new Vector3(2,2,.12f);
                        obstruction.transform.SetPositionAndRotation(shop.Player.View.transform.position+shop.Player.View.transform.forward*.45f,shop.Player.View.transform.rotation);Physics.SyncTransforms();
                    }
                    yield return new WaitForSeconds(.09f);
                    Check(shop.Focused==null && !world.CollectionActive && world.CollectionParticleCountForValidation==0 && shop.Model.Available(CultureKind.Bloom)==0,
                        "Held target/reach/occlusion loss cancels intake without wall trail or unit / "+boundary);
                }
                finally {if(obstruction)Destroy(obstruction);CollectionMouse(false);}
                yield return null;yield return null;
            }
            // This obstacle misses the central aim ray and blocks only the new intake envelope.
            yield return CollectionBeginEdge();
            var pathStation=StationOf(StationKind.Bloom);GameObject pathBlocker=null;
            try
            {
                pathBlocker=new GameObject("Collection validation-only path blocker");
                var blocker=pathBlocker.AddComponent<BoxCollider>();blocker.size=Vector3.one*.04f;
                pathBlocker.transform.position=(pathStation.transform.Find("Outlet opening").position+
                    world.ToolRoot.Find("Siphon chassis/Collection nozzle/Nozzle light").position)*.5f;
                Physics.SyncTransforms();yield return null;yield return null;
                Check(shop.Focused==pathStation && world.CollectionParticleCountForValidation==0 && shop.Model.Available(CultureKind.Bloom)==0,
                    "Side-path obstacle preserves real grower focus but blocks the intake envelope before a unit");
                Destroy(pathBlocker);pathBlocker=null;yield return null;yield return null;
                Check(shop.Focused==pathStation && world.CollectionActive && world.CollectionParticleCountForValidation>0 && shop.Model.Available(CultureKind.Bloom)==0,
                    "Removing side-path obstacle resumes the same valid hold without a queued burst or early unit");
            }
            finally {if(pathBlocker)Destroy(pathBlocker);CollectionMouse(false);}
            yield return CollectionRelease();
            // Full and depleted remain legitimate rejection states even with held input.
            CollectionSupplyFixture();
            foreach(var culture in new[]{CultureKind.Bloom,CultureKind.Flow})while(shop.Model.ReservedCultureCount<shop.Model.Capacity && shop.Model.GetGrower(culture).Units>0)Ok(shop.Model.Collect(culture),"Explicit full-buffer fixture");
            world.Refresh(shop.Model);CollectionAim(CultureKind.Bloom);yield return null;yield return null;int accepted=world.CollectionAcceptedCount;
            CollectionMouse(true);yield return new WaitForSeconds(.18f);Check(shop.Model.ReservedCultureCount==shop.Model.Capacity && !world.CollectionActive && world.CollectionParticleCountForValidation==0 && world.CollectionAcceptedCount==accepted,"Full actual buffer rejects stream and success pulse");yield return CollectionRelease();
            CollectionSupplyFixture();while(shop.Model.GetGrower(CultureKind.Bloom).Units>0){Ok(shop.Model.Collect(CultureKind.Bloom),"Explicit empty-grower fixture collect");Ok(shop.Model.ReleaseCulture(CultureKind.Bloom),"Explicit empty-grower fixture discard");}
            world.Refresh(shop.Model);CollectionAim(CultureKind.Bloom);yield return null;yield return null;CollectionMouse(true);yield return new WaitForSeconds(.18f);
            Check(shop.Model.GetGrower(CultureKind.Bloom).Units==0 && shop.Model.Available(CultureKind.Bloom)==0 && !world.CollectionActive && world.CollectionParticleCountForValidation==0,"Depleted actual grower emits no collectable stream");yield return CollectionRelease();
            yield return CollectionBeginEdge();CollectionMouse(false);yield return null;yield return null;Check(shop.SaveNow(),"Durable pre-failure collected state");byte[] durable=File.ReadAllBytes(shop.SavePath);accepted=world.CollectionAcceptedCount;int played=audio.LabPlaybackCount;
            TutorialBlockSave();
            try
            {
                CollectionMouse(true);yield return new WaitForSeconds(shop.Model.CollectionSecondsPerUnit+.08f);CollectionMouse(false);yield return null;yield return null;
                Check(shop.SaveError!=null && world.CollectionAcceptedCount==accepted && audio.LabPlaybackCount==played && world.CollectionResponse==0 && world.CollectionParticleCountForValidation==0,"Failed collection save suppresses accepted response/audio and retires stream");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed collection save preserves previous durable bytes");
            }
            finally {TutorialUnblockSave();CollectionMouse(false);}
            Check(shop.ReloadSaved(),"Recover durable isolated collection state");shop.SetPanel(ShopPanel.None);
            yield return CollectionBeginEdge();CollectionMouse(false);shop.enabled=false;Check(!world.CollectionActive && world.CollectionParticleCountForValidation==0,"Disable cancels all owned intake particles");shop.enabled=true;yield return null;yield return null;shop.SetFxFocusForValidation(true);
            yield return CollectionBeginEdge();CollectionMouse(false);accepted=world.CollectionAcceptedCount;played=audio.LabPlaybackCount;
            Check(shop.ReloadSaved(),"Reload during held collection");shop.SetPanel(ShopPanel.None);yield return null;yield return null;
            Check(!world.CollectionActive && world.CollectionParticleCountForValidation==0 && world.CollectionAcceptedCount==accepted && audio.LabPlaybackCount==played,"Reload reconstructs reservoir without intake/pulse replay");
            yield return CollectionBeginEdge();CollectionMouse(false);shop.FreezeFxVocabularyForValidation=true;
            try
            {
                var station=StationOf(StationKind.Bloom).transform;var position=station.position;var owner=world.CollectionOwnerForValidation;string authority=FxAuthority();
                station.position+=Vector3.right*.05f;world.Animate(Time.time,.01f);
                Check(!world.FxCues.IsValid(owner) && world.CollectionParticleCountForValidation==0,"Moved grower retires previous intake owner");station.position=position;world.Refresh(shop.Model);world.ResetFxPresentation();
                Check(FxAuthority()==authority,"Movement fixture preserves all authority");
            }
            finally {shop.FreezeFxVocabularyForValidation=false;world.ResetFxPresentation();}
            yield return CollectionRelease();
        }
        IEnumerator FxCollectionScenario()
        {
            limitations.Add("FX-06R-05: real held InputSystem collection and saved unit cadence; scripted operator camera arc, not native play. Guarded surplus release empties the buffer between recordings. One isolated credit fixture buys the existing upgrade. Full/empty/occlusion/moved-owner edge cases use labeled setup fixtures. Low/reduced are diagnostic presentation policies with real collection. Silent actual standalone HUD videos are not listening, creative acceptance or GPU performance evidence.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Collection uses exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionCollectionKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionCollectionMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor)
            {int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w && Screen.height==h,"Exact actual collection HUD resolution");}
            Check(shop.Model.Capacity==shop.Config.BaseCapacity && shop.Model.CollectionSecondsPerUnit==shop.Config.BaseCollectionSeconds,"Real base capacity/cadence");
            yield return CollectionLive(CultureKind.Bloom,"collection-bloom-base-live");yield return CollectionLive(CultureKind.Flow,"collection-flow-base-live");
            StaffCreditFixture(shop.Config.UpgradeCost,"R05 existing capacity purchase only");shop.SetPanel(ShopPanel.Upgrade);Ok(shop.BuySiphonUpgrade(),"Existing guarded siphon upgrade");shop.SetPanel(ShopPanel.None);
            Check(shop.Model.Capacity==shop.Config.UpgradedCapacity && shop.Model.CollectionSecondsPerUnit==shop.Config.UpgradedCollectionSeconds,"Real upgraded capacity/cadence");
            yield return CollectionLive(CultureKind.Bloom,"collection-bloom-upgraded-live");yield return CollectionLive(CultureKind.Flow,"collection-flow-upgraded-live");
            yield return CollectionLive(CultureKind.Bloom,"collection-low-live",true,false);yield return CollectionLive(CultureKind.Flow,"collection-reduced-live",false,true);
            yield return CollectionEdges();var pool=shop.World.FxPool;
            Check(pool.OwnedCount==96 && pool.OwnedMeshCount==5 && pool.OwnedMaterialCount==1 && pool.Root.GetComponentsInChildren<Collider>(true).Length==0 && shop.GetComponents<AudioSource>().Length==5,"Collection retains fixed96-slot topology and mesh/material and fixed five-voice budgets (3 short +2 loops)");
            Check(shop.SaveNow(),"Final isolated collection state saves");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
