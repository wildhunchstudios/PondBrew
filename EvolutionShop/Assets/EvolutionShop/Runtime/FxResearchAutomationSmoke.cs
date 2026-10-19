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
        [Serializable] sealed class ResearchAutomationFrame
        {
            public float seconds;
            public string request, status, collector, doser, research, specimen;
            public int collectCount, doseCount, researchCount, collectParticles, doseParticles, researchParticles, total, ambient, cash, hands;
            public int bloom, flow, spark, mist, stagedBloom, stagedFlow, stagedSpark, stagedMist;
            public bool low, reduced;
            public Vector3 collectPoint, dosePoint, researchPoint;
        }
        [Serializable] sealed class ResearchAutomationTrace { public string context; public ResearchAutomationFrame[] frames; }
        bool researchAutomationLow, researchAutomationReduced;
        static readonly CultureKind[] ResearchAutomationCultures = {CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist};
        ResearchAutomationFrame ResearchAutomationSample(float seconds)
        {
            var world=shop.World;var s=world.ResearchAutomationForValidation;var pool=world.FxPool;int ambient=0;
            for(int i=FxEffectPool.ActionReserved;i<FxEffectPool.Capacity;i++)if(pool.Root.GetChild(i).gameObject.activeSelf)ambient++;
            if(pool.ActiveCount>(researchAutomationLow?32:96)||ambient>(researchAutomationLow?8:32))throw new InvalidOperationException("Research/modules exceed existing pool/detail policy");
            if(world.CollectionActive||world.CollectionParticleCountForValidation!=0)throw new InvalidOperationException("Automatic module operation created a player intake stream");
            for(int i=0;i<s.DoserParticles;i++){
                if(!pool.TryGetParticleForValidation(s.DoserOwner,i,out var start,out var end,out _,out _,out _)||!s.DoserRoot||Vector3.Distance(start,s.DoserPoint)>.12f||Vector3.Distance(end,s.DoserPoint)>.8f)throw new InvalidOperationException("Actual Doser particle is detached from current machine inlet");
            }
            for(int i=0;i<s.ResearchParticles;i++){
                if(!pool.TryGetParticleForValidation(s.ResearchOwner,i,out var start,out var end,out _,out _,out _)||!s.ResearchRoot||Vector3.Distance(start,s.ResearchPoint)>.25f||Vector3.Distance(end,s.ResearchPoint)>.5f)throw new InvalidOperationException("Actual research accent is detached from current saved installation");
            }
            if(s.CollectorParticles>0){shop.World.TryGetLabFacility(ShopLayout.UpgradeFacilityId,out var bench);
                for(int i=0;i<s.CollectorParticles;i++){
                    if(!pool.TryGetParticleForValidation(s.CollectorOwner,i,out var start,out var end,out _,out _,out _)||!s.CollectorRoot)throw new InvalidOperationException("Missing actual automatic source particle");
                    bool source=Vector3.Distance(start,s.CollectorPoint)<.12f;bool valve=bench&&Vector3.Distance(start,bench.TransformPoint(FxResearchAutomationMotion.CollectorValve))<.01f;
                    if(!source&&!valve||Vector3.Distance(start,end)>.5f)throw new InvalidOperationException("Collector particle leaves actual source/valve or invents across-room piping");
                }
            }
            return new ResearchAutomationFrame{seconds=seconds,request=shop.Automation.RequestId,status=shop.AutomationStatus,collector=s.CollectorState.ToString(),doser=s.DoserState.ToString(),research=s.LastResearch.ToString(),
                collectCount=s.CollectorCount,doseCount=s.DoserCount,researchCount=s.ResearchCount,collectParticles=s.CollectorParticles,doseParticles=s.DoserParticles,researchParticles=s.ResearchParticles,total=pool.ActiveCount,ambient=ambient,cash=shop.Model.Currency,hands=world.HandCueCount,
                bloom=shop.Model.Available(CultureKind.Bloom),flow=shop.Model.Available(CultureKind.Flow),spark=shop.Model.Available(CultureKind.Spark),mist=shop.Model.Available(CultureKind.Mist),stagedBloom=shop.Model.Staged(CultureKind.Bloom),stagedFlow=shop.Model.Staged(CultureKind.Flow),stagedSpark=shop.Model.Staged(CultureKind.Spark),stagedMist=shop.Model.Staged(CultureKind.Mist),
                specimen=shop.Model.Incubator.SpecimenId,low=researchAutomationLow,reduced=researchAutomationReduced,collectPoint=s.CollectorPoint,dosePoint=s.DoserPoint,researchPoint=s.ResearchPoint};
        }
        IEnumerator ResearchAutomationClip(string name,float seconds,Action<float> action=null)
        {
            var frames=new List<ResearchAutomationFrame>();Action<float> sample=age=>{action?.Invoke(age);frames.Add(ResearchAutomationSample(age));};
            if(Application.isEditor){float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<seconds){sample(Time.realtimeSinceStartup-start);yield return null;}}
            else yield return ReviewClip(name,seconds,sample);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new ResearchAutomationTrace{context="Actual saved commands and ordinary simulation time; scripted operator, isolated labeled setup; no native-input, audio listening or performance claim",frames=frames.ToArray()},true));
            Check(frames.Count>2,"Actual research/module frames / "+name);
        }
        // One diagnostic camera readback, separate from the normal-HUD recorder. CPU PNG work
        // occurs only after the measured clip ends; the captured image is still the real saved cue.
        sealed class ResearchWorldCapture : IDisposable
        {
            const int Width=1600,Height=900;
            RenderTexture target;
            UnityEngine.Rendering.AsyncGPUReadbackRequest request;
            readonly byte[] bytes=new byte[Width*Height*4];
            bool disposed;
            public bool Requested {get;private set;}
            public bool Complete {get;private set;}
            public string Failure {get;private set;}
            public ResearchWorldCapture()
            {
                if(!SystemInfo.supportsAsyncGPUReadback)throw new InvalidOperationException("Research camera diagnostic requires asynchronous GPU readback.");
                target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);target.Create();
            }
            public void Request(Camera camera)
            {
                if(Requested||disposed)throw new InvalidOperationException("Only one actual research camera sample is allowed.");
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                request=UnityEngine.Rendering.AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32,result=>{
                    try {if(result.hasError)Failure="Actual research camera readback failed.";else if(!disposed)result.GetData<byte>().CopyTo(bytes);}
                    catch(Exception ex){Failure=ex.ToString();}
                    finally {Complete=true;ReleaseTarget();}
                });
                Requested=true;
            }
            public void Save(string path)
            {
                if(!Complete||Failure!=null||disposed)throw new InvalidOperationException(Failure??"Actual research camera readback is incomplete.");
                var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
                try {image.LoadRawTextureData(bytes);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
                finally {UnityEngine.Object.Destroy(image);}
            }
            void ReleaseTarget(){if(target){target.Release();UnityEngine.Object.Destroy(target);target=null;}}
            public void Dispose()
            {
                disposed=true;
                // On a failed/canceled clip, retire this request before releasing its GPU resource.
                try {if(Requested&&!Complete)request.WaitForCompletion();}
                finally {ReleaseTarget();}
            }
        }
        IEnumerator ResearchAutomationPurchase(string name,FxResearchUpgrade kind,Func<bool> purchase,Func<bool> duplicate)
        {
            var before=shop.World.ResearchAutomationForValidation;var audio=shop.GetComponent<ShopAudio>();int cash=shop.Model.Currency,sounds=audio.ResearchPlaybackCount;bool accepted=false;float purchaseAge=0;
            using(var diagnostic=new ResearchWorldCapture()){
                yield return ResearchAutomationClip(name,2.1f,age=>{
                    if(accepted){
                        if(!diagnostic.Requested&&age>=purchaseAge+.05f)diagnostic.Request(shop.Player.View);
                        if(age>=1.1f)shop.SetPanel(ShopPanel.None);return;
                    }
                    if(age<.35f)return;accepted=true;purchaseAge=age;Check(purchase()&&shop.SaveError==null,"Recorded actual durable purchase / "+kind);
                    var after=shop.World.ResearchAutomationForValidation;
                    Check(after.ResearchCount==before.ResearchCount+1&&after.LastResearch==kind,"Exactly one correct research accent / "+kind);
                    Check(after.ResearchParticles>0,"Accepted purchase admits visible pooled research effect / "+kind);
                    Check(audio.ResearchPlaybackCount==sounds+1&&audio.ResearchAudioActive,"Actual saved purchase owns exactly one active research confirmation request / "+kind);
                });
                Check(diagnostic.Requested,"Actual saved research camera diagnostic requested / "+kind);
                float deadline=Time.realtimeSinceStartup+3;
                while(!diagnostic.Complete){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Drain actual research camera readback.");yield return null;}
                diagnostic.Save(Path.Combine(evidence,name+"-actual-saved-world-camera.png"));
            }
            Check(accepted&&shop.Model.Currency<cash,"Actual purchase spends existing credits / "+kind);
            int count=shop.World.ResearchAutomationForValidation.ResearchCount;
            if(kind==FxResearchUpgrade.Mist)yield return ConditioningPanel(advancedVat);
            else if(kind==FxResearchUpgrade.Collector||kind==FxResearchUpgrade.Doser)yield return AutomationEnter();
            else yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);
            cash=shop.Model.Currency;Check(!duplicate()&&shop.Model.Currency==cash&&shop.World.ResearchAutomationForValidation.ResearchCount==count,"Repeated owned purchase cannot pay or celebrate twice / "+kind);
            Check(audio.ResearchPlaybackCount==sounds+1,"Rejected repeat has no second accepted research confirmation / "+kind);
        }
        IEnumerator ResearchAutomationClearReady()
        {
            string id=shop.Model.Incubator.SpecimenId;Ok(shop.TransferToDisplay("tank-a"),"Saved setup specimen to empty display");
            Check(shop.Model.GetTank("tank-a").Value.Id==id,"Setup transfer retains exact specimen ID");
            Ok(shop.Perform(shop.Model.SellFromTank("tank-a")),"Labeled domain setup cleanup sale; not customer checkout evidence");
            yield return AdvancedClear();
        }
        IEnumerator ResearchAutomationBatch(string recipe,string clip,bool low=false,bool reduced=false)
        {
            yield return AdvancedClear();yield return AutomationEnter();
            if(shop.Automation.RecipeId!=recipe)Check(shop.SelectAutomationRecipe(recipe),"Select actual known module recipe / "+recipe);
            researchAutomationLow=low;researchAutomationReduced=reduced;shop.World.SetFxPreviewModes(reduced,low);
            var before=shop.World.ResearchAutomationForValidation;int hands=shop.World.HandCueCount,cash=shop.Model.Currency,sounds=shop.GetComponent<ShopAudio>().LabPlaybackCount;
            var collected=new int[4];var dosed=new int[4];for(int i=0;i<4;i++){collected[i]=shop.World.CollectorCultureCountForValidation(ResearchAutomationCultures[i]);dosed[i]=shop.World.DoserCultureCountForValidation(ResearchAutomationCultures[i]);}
            Check(shop.RequestAutomationBatch(),"Save one real module batch / "+recipe);string request=shop.Automation.RequestId;
            Check(!shop.RequestAutomationBatch()&&shop.Automation.RequestId==request,"Duplicate module request retains exact identity / "+recipe);
            shop.SetPanel(ShopPanel.None);bool sourceViews=!low&&!reduced;int seenCollect=before.CollectorCount,seenDose=before.DoserCount;
            if(sourceViews)AdvancedAim(CultureKind.Bloom);else GrowthAim();
            yield return ResearchAutomationClip(clip,5,age=>{if(!sourceViews)return;var current=shop.World.ResearchAutomationForValidation;if(current.CollectorCount!=seenCollect){seenCollect=current.CollectorCount;AdvancedAim(current.LastCulture);}if(current.DoserCount!=seenDose){seenDose=current.DoserCount;GrowthAim();}});
            yield return AutomationWait(()=>!shop.Automation.IsPending,12,"Actual module batch complete / "+recipe);
            var definition=shop.Model.GetSpecies(recipe);int required=0;
            for(int i=0;i<4;i++){var kind=ResearchAutomationCultures[i];int n=ShopAutomation.Required(definition,kind);required+=n;
                Check(shop.Model.Available(kind)==0&&shop.Model.Staged(kind)==n&&shop.World.CollectorCultureCountForValidation(kind)==collected[i]+n&&shop.World.DoserCultureCountForValidation(kind)==dosed[i]+n,"Exact saved collect/dose count and resource conservation / "+recipe+" / "+kind);}
            var after=shop.World.ResearchAutomationForValidation;
            Check(after.CollectorCount==before.CollectorCount+required&&after.DoserCount==before.DoserCount+1&&shop.Model.Currency==cash&&shop.Model.Incubator.Phase==IncubatorPhase.Dosing&&shop.Model.Incubator.SpecimenId==null,"One atomic prepared recipe with no invented fish, money or second dose / "+recipe);
            Check(shop.World.HandCueCount==hands&&!shop.World.CollectionActive&&shop.World.CollectionParticleCountForValidation==0,"Module operation never adds a phantom player hand/intake / "+recipe);
            Check(shop.GetComponent<ShopAudio>().LabPlaybackCount==sounds+required+1,"One accepted lab sound request per saved collection and atomic dose / "+recipe);
            shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload completed real module batch / "+recipe);yield return null;
            var reload=shop.World.ResearchAutomationForValidation;
            Check(!shop.Automation.IsPending&&reload.CollectorCount==after.CollectorCount&&reload.DoserCount==after.DoserCount&&reload.ResearchCount==after.ResearchCount&&reload.CollectorParticles==0&&reload.DoserParticles==0&&reload.ResearchParticles==0,"Completed reload preserves exact success counters and retires all transient effects / "+recipe);
            shop.SetPanel(ShopPanel.None);researchAutomationLow=researchAutomationReduced=false;shop.World.SetFxPreviewModes(false,false);
        }
        IEnumerator ResearchAutomationStates()
        {
            yield return AdvancedClear();yield return AutomationEnter();if(shop.Automation.RecipeId!="pondlet")Check(shop.SelectAutomationRecipe("pondlet"),"Select simple states recipe");
            Check(shop.SetAutomationEnabled(true,false)&&shop.SetAutomationEnabled(false,false),"Both actual paid modules switched off");yield return null;
            Check(shop.World.ResearchAutomationForValidation.CollectorState==LabModuleVisualState.Off&&shop.World.ResearchAutomationForValidation.DoserState==LabModuleVisualState.Off,"Separate installed modules truthfully show off");
            yield return ResearchAutomationClip("modules-off-live",1.2f);
            Check(shop.SetAutomationEnabled(false,true)&&shop.RequestAutomationBatch(),"Doser-only real shortage request");string request=shop.Automation.RequestId,ingredients=TechnicianIngredients();var before=shop.World.ResearchAutomationForValidation;
            yield return ResearchAutomationClip("modules-shortage-live",1.4f);
            Check(shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients&&shop.AutomationStatus.Contains("manually")&&shop.World.ResearchAutomationForValidation.CollectorCount==before.CollectorCount&&shop.World.ResearchAutomationForValidation.DoserCount==before.DoserCount,"Missing cultures wait without fake transfer or duplicate resources");
            Check(shop.SetAutomationPaused(true),"Pause actual helper request");yield return null;
            Check(shop.World.ResearchAutomationForValidation.DoserState==LabModuleVisualState.Paused,"Owned enabled Doser shows paused distinctly");
            yield return ResearchAutomationClip("modules-paused-live",1.2f);
            Check(shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients,"Paused helper preserves exact request and cultures");
            Check(shop.SetAutomationPaused(false)&&shop.SetAutomationEnabled(true,true),"Resume original request with collector");var station=StationOf(StationKind.Bloom);station.enabled=false;
            try {yield return ResearchAutomationClip("modules-blocked-live",1.4f);Check(shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients&&shop.World.ResearchAutomationForValidation.CollectorCount==before.CollectorCount,"Unavailable actual source produces calm blocked state without transfer");}
            finally{station.enabled=true;}
            Check(shop.CancelAutomationBatch(),"Cancel blocked request through real bench command");
            shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow()&&shop.ReloadSaved()&&!shop.Automation.IsPending&&TechnicianIngredients()==ingredients,"Canceled request cannot reappear after durable reload");
            yield return AutomationEnter();yield return new WaitForSeconds(.8f);var after=shop.World.ResearchAutomationForValidation;
            Check(after.CollectorCount==before.CollectorCount&&after.DoserCount==before.DoserCount&&after.CollectorParticles==0&&after.DoserParticles==0,"Off/shortage/pause/block/cancel never celebrates work");
        }
        IEnumerator ResearchAutomationFailure()
        {
            yield return AdvancedClear();yield return AutomationEnter();Check(shop.RequestAutomationBatch(),"Actual helper request before injected work write failure");
            string request=shop.Automation.RequestId,ingredients=TechnicianIngredients(),durable=File.ReadAllText(shop.SavePath);int cash=shop.Model.Currency;var before=shop.World.ResearchAutomationForValidation;
            TutorialBlockSave();
            try {yield return AutomationWait(()=>shop.AutomationBlockedBySave,5,"Real module write fault");Check(shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients&&shop.Model.Currency==cash&&File.ReadAllText(shop.SavePath)==durable,"Failed atomic module save preserves authority, request and durable bytes");
                Check(shop.World.ResearchAutomationForValidation.CollectorCount==before.CollectorCount&&shop.World.ResearchAutomationForValidation.DoserCount==before.DoserCount,"Failed saved operation emits no accepted module cue");yield return ResearchAutomationClip("modules-save-blocked-live",1.2f);}
            finally{TutorialUnblockSave();}
            shop.SetPanel(ShopPanel.Pause);Check(shop.ReloadSaved()&&shop.AutomationBlockedBySave&&shop.Automation.RequestId==request,"Reload cannot bypass explicit write-failure latch");
            yield return AutomationEnter();Check(shop.RetryAutomation(),"Explicit successful retry resumes same request");yield return AutomationWait(()=>!shop.Automation.IsPending,12,"Recovered actual preparation");
            var after=shop.World.ResearchAutomationForValidation;Check(after.CollectorCount==before.CollectorCount+2&&after.DoserCount==before.DoserCount+1&&shop.Model.Staged(CultureKind.Bloom)==1&&shop.Model.Staged(CultureKind.Flow)==1,"Recovered operation prepares exactly one two-culture recipe");
            yield return AdvancedClear();
        }
        IEnumerator ResearchAutomationResearchFailure()
        {
            var audio=shop.GetComponent<ShopAudio>();yield return WaitFor(()=>!audio.ServicePlaying,2,"Earlier actual service cue settles before audio refusal observation");
            Check(shop.SaveNow(),"Save actual eligible research before isolated write fault");string durable=File.ReadAllText(shop.SavePath);var before=shop.World.ResearchAutomationForValidation;
            TutorialBlockSave();
            try {shop.ResearchSpark();Check(shop.SaveError!=null&&File.ReadAllText(shop.SavePath)==durable&&shop.World.ResearchAutomationForValidation.ResearchCount==before.ResearchCount&&shop.World.ResearchAutomationForValidation.ResearchParticles==0&&!audio.ServicePlaying,"Unsaved research has no accepted visual or new service playback and cannot replace durable slot; no live rollback claim");}
            finally{TutorialUnblockSave();}
            Check(shop.ReloadSaved()&&!shop.Model.SparkResearchUnlocked,"Research failure restores actual pre-purchase slot without celebration");yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);
        }
        IEnumerator ResearchAutomationDoserOnly()
        {
            yield return AdvancedClear();yield return AdvancedHarvest(CultureKind.Bloom,1);yield return AdvancedHarvest(CultureKind.Flow,1);yield return AutomationEnter();
            Check(!shop.Automation.CollectorOwned&&shop.Automation.DoserOwned&&shop.SetAutomationEnabled(false,true)&&shop.SelectAutomationRecipe("pondlet"),"Doser can be installed and enabled without Collector");
            var before=shop.World.ResearchAutomationForValidation;Check(shop.RequestAutomationBatch(),"Request Doser-only preparation using real manual ingredients");shop.SetPanel(ShopPanel.None);GrowthAim();
            yield return ResearchAutomationClip("modules-doser-only-live",2.5f);yield return AutomationWait(()=>!shop.Automation.IsPending,5,"Doser-only actual completion");
            var after=shop.World.ResearchAutomationForValidation;
            Check(after.CollectorState==LabModuleVisualState.Absent&&after.CollectorCount==before.CollectorCount&&after.DoserCount==before.DoserCount+1&&shop.Model.Staged(CultureKind.Bloom)==1&&shop.Model.Staged(CultureKind.Flow)==1,"Doser-only batch completes one saved dose with no nonexistent Collector effect");
            yield return AdvancedClear();yield return AutomationEnter();
        }
        IEnumerator ResearchAutomationInterruptions()
        {
            Transform valve=null;foreach(var t in StationOf(StationKind.Upgrade).GetComponentsInChildren<Transform>(true))if(t.name=="FX / Collector module valve")valve=t;
            Check(valve,"Existing installed Collector valve is the actual visual owner");
            foreach(string boundary in new[]{"pause","focus","arrange","disable","cancel","bench-move","bench-disable","source-move","source-disable","doser-move","model-adoption"}){
                yield return AdvancedClear();yield return AutomationEnter();if(shop.Automation.RecipeId!="pondlet")Check(shop.SelectAutomationRecipe("pondlet"),"Select interruption recipe");
                var before=shop.World.ResearchAutomationForValidation;Check(shop.RequestAutomationBatch(),"Actual request for active cancellation / "+boundary);shop.SetPanel(ShopPanel.None);
                bool doseBoundary=boundary=="doser-move";
                yield return AutomationWait(()=>doseBoundary?shop.World.ResearchAutomationForValidation.DoserCount>before.DoserCount:shop.World.ResearchAutomationForValidation.CollectorCount>before.CollectorCount,4,"Actual saved active operation / "+boundary);
                if(boundary=="pause"){
                    float acceptedAt=Time.realtimeSinceStartup;yield return null;
                    Check(Time.realtimeSinceStartup-acceptedAt<shop.Model.CollectionSecondsPerUnit&&Mathf.Abs(Mathf.DeltaAngle(0,valve.localEulerAngles.z))>.01f&&Mathf.Abs(Mathf.DeltaAngle(0,valve.localEulerAngles.z))<=6.01f,"Existing valve visibly moves on the next ordinary frame after an accepted unit, before another unit can be collected");
                }
                var active=shop.World.ResearchAutomationForValidation;Check(doseBoundary?active.DoserParticles>0:active.CollectorParticles>0,"Interruption starts while actual accepted particles are alive / "+boundary);string ingredients=TechnicianIngredients();
                Transform moved=null;Vector3 originalPosition=Vector3.zero;bool reenable=false;
                if(boundary=="focus")shop.SetFxFocusForValidation(false);
                else if(boundary=="disable")shop.World.enabled=false;
                else if(boundary=="arrange")shop.SetPanel(ShopPanel.Arrange);
                else if(boundary=="cancel"){shop.SetPanel(ShopPanel.Automation);Check(shop.CancelAutomationBatch(),"Cancel real active module request");}
                else if(boundary=="model-adoption"){
                    Check(GameModel.TryRestore(shop.Layout.WithTankIds(shop.Config),shop.Model.CaptureSave(),out var replacement,out _),"Clone exact authority solely for direct view adoption");shop.World.Refresh(replacement);
                }
                else if(boundary.EndsWith("-move")||boundary.EndsWith("-disable")){
                    if(boundary.StartsWith("bench"))shop.World.TryGetLabFacility(ShopLayout.UpgradeFacilityId,out moved);
                    else moved=doseBoundary?active.DoserRoot:active.CollectorRoot;
                    Check(moved,"Current actual dependency exists / "+boundary);originalPosition=moved.position;
                    if(boundary.EndsWith("-disable")){moved.gameObject.SetActive(false);reenable=true;}else moved.position+=new Vector3(.2f,0,.1f);
                    shop.World.Animate(Time.time,0); // Diagnostic transform boundary only; do not advance simulation or save layout.
                }
                else shop.SetPanel(ShopPanel.Pause);
                var cleared=shop.World.ResearchAutomationForValidation;Check(cleared.CollectorParticles==0&&cleared.DoserParticles==0&&cleared.ResearchParticles==0&&TechnicianIngredients()==ingredients,"Boundary retires active effects immediately without undoing committed cultures / "+boundary);
                if(moved){moved.position=originalPosition;if(reenable)moved.gameObject.SetActive(true);Physics.SyncTransforms();}
                if(boundary=="model-adoption")shop.World.Refresh(shop.Model);
                shop.World.Animate(Time.time,0);Check(Quaternion.Angle(valve.localRotation,Quaternion.identity)<.01f,"Canceled Collector valve has no stale action pose / "+boundary);
                if(boundary=="focus")shop.SetFxFocusForValidation(true);if(boundary=="disable")shop.World.enabled=true;
                shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload boundary state / "+boundary);yield return new WaitForSeconds(.1f);
                var loaded=shop.World.ResearchAutomationForValidation;Check(loaded.CollectorCount==active.CollectorCount&&loaded.DoserCount==active.DoserCount&&loaded.CollectorParticles==0&&loaded.DoserParticles==0&&TechnicianIngredients()==ingredients,"Paused reload cannot replay canceled visual or spend a second unit / "+boundary);
                yield return AutomationEnter();if(shop.Automation.IsPending)Check(shop.CancelAutomationBatch(),"Release remaining pending validation request / "+boundary);
            }
            yield return AdvancedClear();
        }
        IEnumerator ResearchAutomationMoss()
        {
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));string rack=shop.Layout.Items[shop.Layout.Items.Count-1].Id;string[] slots=shop.Layout.GetStorageSlotIds(rack);
            yield return AutomationEnter();if(shop.Automation.RecipeId!="pondlet")Check(shop.SelectAutomationRecipe("pondlet"),"Choose real matching Moss recipe");
            Check(shop.SetAutomationDestination("tank-a")&&shop.SetAutomationMaintainEnabled(true),"Saved desired-stock target starts with existing one-specimen default");
            yield return new WaitForSeconds(.5f);Check(AutomationQueuedStarts("pondlet")==1&&!shop.Automation.IsPending&&shop.AutomationStatus.Contains("not hired"),"Missing Moss has exactly one waiting target start and no fake module work");
            Check(shop.SetAutomationPaused(true),"Pause policy while hiring existing technician");yield return TechnicianEnter();Check(shop.HireTechnician()&&shop.SetTechnicianScheduled(true),"Hire and pay existing Moss duty");
            var technician=shop.Technician;technician.enabled=false;
            try {
                yield return AutomationEnter();Check(shop.SetAutomationPaused(false),"Resume target with diagnostic technician temporarily disabled");yield return AutomationWait(()=>shop.Automation.IsPending,6,"One durable target request");
                string request=shop.Automation.RequestId;Check(shop.SetAutomationPaused(true),"Retain target while editing actual Moss queue");
                yield return TechnicianEnter();Check(shop.QueueTechnicianRecipe("bubblebelly")&&shop.MoveTechnicianPriority("bubblebelly",-1),"Real higher-priority Moss order precedes target");
                yield return AutomationEnter();Check(shop.SetAutomationPaused(false),"Expose actual staff-priority state");string ingredients=TechnicianIngredients();var before=shop.World.ResearchAutomationForValidation;
                yield return ResearchAutomationClip("modules-staff-priority-live",1.5f);
                Check(shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients&&shop.World.ResearchAutomationForValidation.CollectorState==LabModuleVisualState.StaffPriority&&shop.World.ResearchAutomationForValidation.DoserState==LabModuleVisualState.StaffPriority&&shop.World.ResearchAutomationForValidation.CollectorCount==before.CollectorCount,"Real priority holds exact request/resources without module success");
                Check(shop.SetAutomationPaused(true),"Pause target before explicit queue cancellation");yield return TechnicianEnter();Check(shop.RemoveTechnicianRecipe("bubblebelly")&&shop.RemoveTechnicianRecipe("pondlet"),"Player removes queued starts while target remains paused");
                yield return AutomationEnter();yield return new WaitForSeconds(.7f);Check(shop.Laboratory.Queue.Length==0&&shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients,"Paused target never requeues removed work or spends cultures");
                yield return TechnicianEnter();Check(shop.QueueTechnicianRecipe("pondlet"),"Player restores one matching Moss order");
                shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow()&&shop.ReloadSaved()&&shop.Automation.Paused&&shop.Automation.RequestId==request&&AutomationQueuedStarts("pondlet")==1,"Paused target reload preserves exact request and one queue entry");
                yield return AutomationEnter();yield return new WaitForSeconds(.7f);Check(AutomationQueuedStarts("pondlet")==1&&shop.Automation.RequestId==request&&TechnicianIngredients()==ingredients,"Paused reload cannot duplicate restored Moss order");
                technician.enabled=true;before=shop.World.ResearchAutomationForValidation;Check(shop.SetAutomationPaused(false),"Resume actual helper-to-Moss handoff");shop.SetPanel(ShopPanel.None);
                var root=StationOf(StationKind.Incubator).transform;GateAim(root.TransformPoint(new Vector3(-1.6f,.2f,-1.5f)),root.TransformPoint(new Vector3(0,1.35f,0)));Physics.SyncTransforms();
                yield return ResearchAutomationClip("modules-moss-handoff-live",10);
                yield return AutomationWait(()=>shop.Model.Incubator.Phase!=IncubatorPhase.Dosing&&shop.Model.Incubator.SpeciesId=="pondlet",30,"Real Moss starts matching recipe");string specimen=shop.Model.Incubator.SpecimenId;
                Check(!shop.Automation.IsPending&&!shop.Automation.PolicyHandoffWaiting&&AutomationQueuedStarts("pondlet")==0&&shop.World.ResearchAutomationForValidation.CollectorCount==before.CollectorCount+2&&shop.World.ResearchAutomationForValidation.DoserCount==before.DoserCount,"Moss consumes exact helper-fed cultures once, without a competing Doser preparation");
                yield return AutomationEnter();Check(shop.SetAutomationMaintainEnabled(false),"Stop future target starts while preserving actual specimen");
                yield return TechnicianWait(()=>shop.Model.GetStorage(slots[0]).HasValue||shop.Model.GetStorage(slots[1]).HasValue,40,"Moss completes natural incubation and real rack delivery");
                string stored=shop.Model.GetStorage(slots[0]).HasValue?shop.Model.GetStorage(slots[0]).Value.Id:shop.Model.GetStorage(slots[1]).Value.Id;
                Check(stored==specimen&&AutomationStockCount("pondlet")==1&&shop.Model.ReservedCultureCount==0&&!shop.Automation.IsPending,"Completed handoff owns exactly one same-ID rack specimen and zero duplicate resources");
            }
            finally{if(technician)technician.enabled=true;}
        }
        IEnumerator ResearchAutomationAudioOwnership()
        {
            limitations.Add("Audio ownership boundary probes explicitly call the existing audio API without a purchase, solely to observe source ownership/cancellation. Actual saved purchases above separately prove one accepted confirmation each. Source playback/priority is technical evidence, not listening acceptance.");
            var audio=shop.GetComponent<ShopAudio>();shop.SetPanel(ShopPanel.None);Check(shop.SaveNow(),"Save exact isolated authority before audio-only diagnostics");string authority=FxAuthority();
            foreach(string boundary in new[]{"pause","focus","arrange","session-disable","reload"}){
                audio.ResearchConfirmation();Check(audio.ResearchAudioOwned&&audio.ResearchAudioActive,"Explicit diagnostic research source starts / "+boundary);
                if(boundary=="focus")shop.SetFxFocusForValidation(false);
                else if(boundary=="arrange")shop.SetPanel(ShopPanel.Arrange);
                else if(boundary=="session-disable")shop.enabled=false;
                else if(boundary=="reload")Check(shop.ReloadSaved(),"Actual reload during owned research confirmation");
                else shop.SetPanel(ShopPanel.Pause);
                Check(!audio.ResearchAudioOwned&&!audio.ResearchAudioActive&&!audio.ServicePlaying,"Boundary retires only owned research source / "+boundary);
                if(boundary=="focus")shop.SetFxFocusForValidation(true);if(boundary=="session-disable")shop.enabled=true;shop.SetPanel(ShopPanel.None);
            }
            audio.ResearchConfirmation();audio.Feedback(true);audio.StopResearchAudio();
            Check(!audio.ResearchAudioOwned&&audio.ServicePlaying,"Stale research cancellation preserves a newer generic confirmation using same clip");
            audio.ResearchConfirmation();audio.Discovery();audio.StopResearchAudio();
            Check(!audio.ResearchAudioOwned&&audio.ServicePlaying&&audio.ServicePriorityActive,"Stale research cancellation preserves newer actual discovery playback and service priority");
            audio.ResearchConfirmation();audio.StopResearchAudio();
            Check(!audio.ServicePlaying&&audio.ServicePriorityActive,"Canceling owned research keeps the preceding discovery protection window");
            Check(FxAuthority()==authority&&shop.GetComponents<AudioSource>().Length==5,"Audio-only cancellation probes do not alter exact game authority or voice topology");
            yield return null;
        }
        IEnumerator FxResearchAutomationScenario()
        {
            limitations.Add("FX10 actual saved research/upgrades/modules, ordinary-time module operations and Moss handoff. Labeled1000-credit construction/research fixture and accelerated three starter prerequisite discoveries only; advanced discoveries run original normal countdowns. Cleanup domain sales are not customer-checkout evidence. Synthetic input and scripted operator positions, temporary disabled source/technician and isolated save obstruction are explicit test fixtures. Existing recorder captures actual standalone HUD. Low/reduced are diagnostic settings. Silent media is not native-input, listening, creative acceptance or GPU/performance evidence.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Research/modules exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit FX10 tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionResearchAutomationKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionResearchAutomationMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("research-automation-input-provenance.json");
            if(!Application.isEditor){int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w&&Screen.height==h,"Exact research/module capture resolution");}
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);int initial=shop.World.ResearchAutomationForValidation.ResearchCount;Check(!shop.ResearchSpark()&&shop.World.ResearchAutomationForValidation.ResearchCount==initial,"Actual unmet prerequisites cannot celebrate research");
            StaffCreditFixture(1000,"FX10 existing construction and purchase coverage only");
            foreach(string species in new[]{"pondlet","bubblebelly","ribbonfin"}){SpecimenProduce(species);Ok(shop.Model.TransferToTank("tank-a"),"Labeled starter prerequisite fixture transfer");Ok(shop.Model.SellFromTank("tank-a"),"Labeled starter prerequisite fixture sale");}shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save labeled starter prerequisites");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);
            yield return ResearchAutomationResearchFailure();
            yield return ResearchAutomationPurchase("research-spark-live",FxResearchUpgrade.Spark,()=>shop.ResearchSpark(),()=>shop.ResearchSpark());
            yield return ResearchAutomationPurchase("research-siphon-live",FxResearchUpgrade.Siphon,()=>shop.BuySiphonUpgrade().Success,()=>shop.BuySiphonUpgrade().Success);
            GateAim(new Vector3(0,.2f,2),new Vector3(0,1.2f,5));yield return null;Check(shop.OpenLabMenu()&&shop.BuyLabExpansion()&&shop.BuyLabExpansion(),"Real lab extensions for supported module/vat validation");shop.SetPanel(ShopPanel.None);
            yield return AdvancedProduce("lanternfin");yield return ResearchAutomationClearReady();
            Check(AdvancedFindPosition(null,false,-6,14,out int vx,out int vz),"Find legal installed Mist source");yield return EditedPlace(null,ShopLayout.ConditionerDefinitionId,vx,vz,0,new Vector3(vx*ShopLayout.GridSize,.2f,vz*ShopLayout.GridSize-1.9f));advancedVat=shop.Model.ConditionerIds[0];
            yield return ConditioningPanel(advancedVat);yield return ResearchAutomationPurchase("research-mist-live",FxResearchUpgrade.Mist,()=>shop.ResearchMist(),()=>shop.ResearchMist());
            yield return AdvancedProduce("driftwing");yield return ResearchAutomationClearReady();yield return AutomationEnter();
            yield return ResearchAutomationPurchase("research-doser-live",FxResearchUpgrade.Doser,()=>shop.PurchaseAutomationModule(false),()=>shop.PurchaseAutomationModule(false));
            yield return ResearchAutomationDoserOnly();
            yield return ResearchAutomationPurchase("research-collector-live",FxResearchUpgrade.Collector,()=>shop.PurchaseAutomationModule(true),()=>shop.PurchaseAutomationModule(true));
            Check(shop.SetAutomationEnabled(true,true)&&shop.Automation.DoserEnabled,"Enable both actual owned modules");
            yield return ResearchAutomationBatch("lanternfin","modules-lanternfin-live");yield return ResearchAutomationBatch("driftwing","modules-driftwing-live");
            yield return ResearchAutomationStates();yield return ResearchAutomationFailure();yield return ResearchAutomationInterruptions();
            yield return ResearchAutomationBatch("lanternfin","modules-low-live",true);yield return ResearchAutomationBatch("driftwing","modules-reduced-live",false,true);yield return AdvancedClear();
            yield return ResearchAutomationMoss();
            yield return ResearchAutomationAudioOwnership();
            var pool=shop.World.FxPool;Check(pool.OwnedCount==96&&pool.OwnedMeshCount==5&&pool.OwnedMaterialCount==1&&pool.Root.GetComponentsInChildren<Collider>(true).Length==0&&shop.GetComponents<AudioSource>().Length==5,"Research/modules retain fixed96 carriers,5 meshes,1 material,no particle collider and5 bounded voices (3 short +2 loops)");
            Check(growerInputReceipt.foreignCurrentSamples==0&&growerInputReceipt.otherEnabledSamples==0&&growerInputReceipt.droppedTransitions==0,"Research/module synthetic input provenance stays isolated");Check(shop.SaveNow(),"Save final isolated FX10 state");File.Copy(shop.SavePath,Path.Combine(evidence,"research-automation-final-save.json"),true);shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
