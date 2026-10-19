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
        [Serializable] sealed class PlayerInteractionFrame
        {
            public float seconds, remaining, progress;
            public string action, source, destination, specimen, hand;
            public int accepted, refused, particles, hands, capacity, cultures, stored, displayed;
            public bool gloves, collecting;
            public Vector3 point, tool;
        }
        [Serializable] sealed class PlayerInteractionTrace { public string context; public PlayerInteractionFrame[] frames; }
        IEnumerator PlayerInteractionClip(string name,float seconds,Action<float> action=null)
        {
            var frames=new List<PlayerInteractionFrame>();
            Action<float> sample=age=>{
                action?.Invoke(age);var w=shop.World;
                int particles=w.PlayerInteractionParticleCountForValidation;
                for(int i=0;i<particles;i++){
                    if(!w.FxPool.TryGetParticleForValidation(w.PlayerInteractionOwnerForValidation,i,out var startPoint,out var endPoint,out var position,out var particleAge,out var lifetime))throw new InvalidOperationException("Actual interaction particle disappeared during inspection");
                    if(Vector3.Distance(position,w.PlayerInteractionPointForValidation)>.85f || float.IsNaN(position.x) || particleAge>lifetime)
                        throw new InvalidOperationException("Interaction particle escaped bounded local anchor or lifetime");
                }
                frames.Add(new PlayerInteractionFrame{seconds=age,remaining=w.PlayerInteractionRemaining,action=w.LastPlayerInteraction.ToString(),source=w.PlayerInteractionSource,
                    destination=w.PlayerInteractionDestination,specimen=w.PlayerInteractionSpecimenId,hand=w.HandAction.ToString(),accepted=w.PlayerInteractionCount-w.PlayerRefusalCount,
                    refused=w.PlayerRefusalCount,particles=particles,hands=w.HandCueCount,capacity=shop.Model.Capacity,cultures=shop.Model.ReservedCultureCount,
                    stored=shop.StoredSpecimenCount,displayed=shop.DisplayedStockCount(),gloves=w.HandsVisual.gameObject.activeInHierarchy,collecting=w.CollectionActive,
                    progress=shop.HarvestProgress,point=w.PlayerInteractionPointForValidation,tool=w.ToolRoot.localPosition});
            };
            if(Application.isEditor){float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<seconds){sample(Time.realtimeSinceStartup-began);yield return null;}}
            else yield return ReviewClip(name,seconds,sample);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new PlayerInteractionTrace{
                context="Actual saved commands and synthetic input at scripted operator positions. Accelerated specimen setup is labeled. Silent HUD footage; no human, listening or GPU acceptance.",frames=frames.ToArray()},true));
            Check(frames.Count>2,"Actual player interaction frames / "+name);
        }
        string PlayerInventory()
        {
            string state=shop.Model.Currency+"|"+shop.Model.Incubator.SpecimenId+"|";
            foreach(var culture in new[]{CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist})state+=shop.Model.Available(culture)+":"+shop.Model.Staged(culture)+"|";
            foreach(string id in shop.Model.TankIds)state+=id+":"+(shop.Model.GetTank(id).HasValue?shop.Model.GetTank(id).Value.Id:"empty")+"|";
            foreach(string id in shop.Model.StorageIds)state+=id+":"+(shop.Model.GetStorage(id).HasValue?shop.Model.GetStorage(id).Value.Id:"empty")+"|";
            return state;
        }
        void PlayerExactOwner(string id,string location)
        {
            int count=shop.Model.Incubator.SpecimenId==id?1:0;string found=count==1?"incubator":null;
            foreach(string tank in shop.Model.TankIds)if(shop.Model.GetTank(tank).HasValue&&shop.Model.GetTank(tank).Value.Id==id){count++;found=tank;}
            foreach(string slot in shop.Model.StorageIds)if(shop.Model.GetStorage(slot).HasValue&&shop.Model.GetStorage(slot).Value.Id==id){count++;found=slot;}
            Check(count==1&&found==location,"Exact specimen has one authoritative location / "+id+" -> "+location);
        }
        IEnumerator PlayerCommandClip(string name,Func<bool> command,FxPlayerInteraction expected,string destination,string specimen,string owner)
        {
            var w=shop.World;int count=w.PlayerInteractionCount,soundCount=shop.GetComponent<ShopAudio>().FeedbackSuccessCount;bool done=false;
            yield return PlayerInteractionClip(name,1.6f,age=>{
                if(done||age<.25f)return;done=true;
                Check(command()&&shop.SaveError==null,"Actual saved player command / "+name);
                Check(shop.GetComponent<ShopAudio>().FeedbackSuccessCount==soundCount+1,"Exactly one accepted service confirmation after saved player command / "+name);
                Check(w.PlayerInteractionCount==count+1&&w.LastPlayerInteraction==expected&&w.PlayerInteractionDestination==destination&&w.PlayerInteractionSpecimenId==specimen,
                    "One correct local cue retains exact destination/specimen / "+name+" expected="+expected+"/"+destination+"/"+specimen+" count="+count+" actual="+w.LastPlayerInteraction+"/"+w.PlayerInteractionDestination+"/"+w.PlayerInteractionSpecimenId+" count="+w.PlayerInteractionCount);
                Check(w.PlayerInteractionRemaining>0&&w.PlayerInteractionParticleCountForValidation>0,"Saved result admits bounded local particles / "+name);
                Check(shop.Panel==ShopPanel.None||!w.HandsVisual.gameObject.activeInHierarchy,"Modal command never reaches gloves through panel / "+name);
                PlayerExactOwner(specimen,owner);
            });
            Check(done,"Recorded command executed / "+name);
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload saved exact transfer / "+name);yield return null;
            PlayerExactOwner(specimen,owner);
            Check(w.PlayerInteractionCount==count+1&&w.PlayerInteractionRemaining<=0&&w.PlayerInteractionParticleCountForValidation==0,"Reload reconstructs ownership without replay / "+name);
        }
        IEnumerator PlayerTransfers()
        {
            yield return CollectionClearBuffer();StaffCreditFixture(200,"FX11 bounded rack construction fixture");
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));
            string rack=shop.Layout.Items[shop.Layout.Items.Count-1].Id;string slot=shop.Layout.GetStorageSlotIds(rack)[0];
            SpecimenProduce("pondlet");Check(shop.SaveNow(),"Save labeled accelerated ready specimen");
            string id=shop.Model.Incubator.SpecimenId;var w=shop.World;
            yield return SpecimenRackPanel(rack);
            yield return PlayerCommandClip("player-rack-saved-live",()=>shop.StoreReadyInRack(slot),FxPlayerInteraction.Rack,slot,id,slot);
            yield return SpecimenDisplayPanel("tank-a");
            yield return PlayerCommandClip("player-rack-to-display-live",()=>shop.StockDisplayFromStorage(slot,id),FxPlayerInteraction.Place,"tank-a",id,"tank-a");
            yield return SpecimenDisplayPanel("tank-a");
            yield return PlayerCommandClip("player-display-to-rack-live",()=>shop.ReturnDisplayToStorage(slot,id),FxPlayerInteraction.Transfer,slot,id,slot);
            yield return SpecimenDisplayPanel("tank-a");
            string authority=PlayerInventory();int accepted=w.PlayerInteractionCount-w.PlayerRefusalCount,refused=w.PlayerRefusalCount;
            yield return PlayerInteractionClip("player-refused-repeat-live",1.3f,age=>{
                if(age<.25f||refused<0)return;
                for(int i=0;i<8;i++)Check(!shop.StockDisplayFromStorage(slot,"stale-specimen-id"),"Stale expected identity refuses repeated command");
                Check(PlayerInventory()==authority&&w.PlayerInteractionCount-w.PlayerRefusalCount==accepted,"Rapid refused stock cannot move a specimen or emit success");
                refused=-1;
            });
            PlayerExactOwner(id,slot);
            // Physical command boundaries use the same guarded menu operation, not a cue preview.
            yield return SpecimenDisplayPanel("tank-a");accepted=w.PlayerInteractionCount;authority=PlayerInventory();
            var station=shop.Focused;Vector3 original=shop.Player.transform.position;Quaternion rotation=shop.Player.transform.rotation;
            shop.Player.Teleport(original+Vector3.right*8,rotation);yield return null;
            Check(!shop.StockDisplayFromStorage(slot,id)&&w.PlayerInteractionCount==accepted&&PlayerInventory()==authority,"Out-of-reach menu command preserves exact ownership and success count");
            yield return SpecimenDisplayPanel("tank-a");accepted=w.PlayerInteractionCount;authority=PlayerInventory();
            GameObject blocker=null;
            try{
                blocker=new GameObject("FX11 validation-only stock sight blocker");blocker.AddComponent<BoxCollider>().size=new Vector3(2,2,.12f);
                blocker.transform.SetPositionAndRotation(shop.Player.View.transform.position+shop.Player.View.transform.forward*.45f,shop.Player.View.transform.rotation);
                Physics.SyncTransforms();yield return null;
                Check(!shop.StockDisplayFromStorage(slot,id)&&w.PlayerInteractionCount==accepted&&PlayerInventory()==authority,"Opaque physical obstruction prevents local saved stock command and success");
            }finally{if(blocker)Destroy(blocker);}
            yield return null;yield return SpecimenDisplayPanel("tank-a");
            Check(shop.SaveNow(),"Save pre-failure exact rack state");byte[] durable=File.ReadAllBytes(shop.SavePath);
            accepted=w.PlayerInteractionCount;int sounds=shop.GetComponent<ShopAudio>().FeedbackSuccessCount;
            TutorialBlockSave();
            try{
                shop.StockDisplayFromStorage(slot,id);
                Check(shop.SaveError!=null&&w.PlayerInteractionCount==accepted&&w.PlayerInteractionParticleCountForValidation==0&&w.PlayerInteractionRemaining<=0,
                    "Failed stock save cannot celebrate unsaved model result");
                Check(shop.GetComponent<ShopAudio>().FeedbackSuccessCount==sounds,"Failed stock save emits no generic accepted service sound");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed stock write preserves durable exact rack state");
            }finally{TutorialUnblockSave();}
            Check(shop.ReloadSaved(),"Restore durable state after deliberate failed stock save");yield return null;PlayerExactOwner(id,slot);
            yield return SpecimenDisplayPanel("tank-a");
            yield return PlayerCommandClip("player-transfer-recovered-live",()=>shop.StockDisplayFromStorage(slot,id),FxPlayerInteraction.Place,"tank-a",id,"tank-a");
            // Separate physical immediate E transfer, with no stored stock left to choose.
            SpecimenProduce("pondlet");Check(shop.SaveNow(),"Save second labeled ready specimen");id=shop.Model.Incubator.SpecimenId;
            shop.SetPanel(ShopPanel.None);station=null;foreach(var candidate in w.Stations)if(candidate.TankId=="tank-b")station=candidate;
            Vector3 spot=shop.Customers.Navigation.BrowseSpot("tank-b",0);spot.y=.2f;
            AimAt(spot,station.transform.TransformPoint(new Vector3(0,1.1f,0)));yield return null;yield return null;
            Check(shop.Focused==station,"Immediate E transfer has actual physical display focus");
            bool pressed=false,released=false;accepted=w.PlayerInteractionCount;
            yield return PlayerInteractionClip("player-immediate-place-live",1.5f,age=>{
                if(!pressed&&age>=.25f){pressed=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.E));}
                if(!released&&age>=.35f){released=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());}
            });
            Check(w.PlayerInteractionCount==accepted+1&&w.LastPlayerInteraction==FxPlayerInteraction.Place&&w.PlayerInteractionSpecimenId==id,
                "Actual E direct placement emits one accepted hand/local action");PlayerExactOwner(id,"tank-b");
        }
        IEnumerator PlayerPlacementInterruptions()
        {
            var w=shop.World;string rack=null;foreach(var item in shop.Layout.PlacedItems)if(item.DefinitionId==ShopLayout.RackDefinitionId){rack=item.Id;break;}
            string slot=shop.Layout.GetStorageSlotIds(rack)[0],id=shop.Model.GetTank("tank-a").Value.Id;
            foreach(string mode in new[]{"pause","focus","arrange","panel","reload"}){
                yield return SpecimenDisplayPanel("tank-a");int count=w.PlayerInteractionCount;
                Check(shop.ReturnDisplayToStorage(slot,id)&&shop.SaveError==null&&w.PlayerInteractionRemaining>0,"Actual durable departure active before "+mode);
                PlayerExactOwner(id,slot);
                if(mode=="focus")shop.SetFxFocusForValidation(false);
                else if(mode=="reload")Check(shop.ReloadSaved(),"Reload during active saved departure");
                else shop.SetPanel(mode=="pause"?ShopPanel.Pause:mode=="arrange"?ShopPanel.Arrange:ShopPanel.Journal);
                Check(w.PlayerInteractionRemaining<=0&&w.PlayerInteractionParticleCountForValidation==0,"Active departure clears immediately on "+mode);
                yield return null;shop.SetFxFocusForValidation(true);shop.SetPanel(ShopPanel.None);yield return null;
                Check(w.PlayerInteractionCount==count+1&&w.HandAction==FxCueId.None,"Interruption never queues a deferred player action / "+mode);
                PlayerExactOwner(id,slot);
                yield return SpecimenDisplayPanel("tank-a");
                if(mode=="pause"||mode=="focus"){
                    w.SetFxPreviewModes(mode=="focus",mode=="pause");
                    yield return PlayerCommandClip(mode=="pause"?"player-place-low-live":"player-place-reduced-live",()=>shop.StockDisplayFromStorage(slot,id),FxPlayerInteraction.Place,"tank-a",id,"tank-a");
                    w.SetFxPreviewModes(false,false);
                }else Check(shop.StockDisplayFromStorage(slot,id)&&shop.SaveError==null,"Restore actual saved specimen for next interruption / "+mode);
            }
            shop.SetPanel(ShopPanel.None);yield return null;
            int hands=w.HandCueCount,countBefore=w.PlayerInteractionCount;
            w.TryFxCue(FxCueId.HandPlace,shop.FxActionSequenceForValidation+100,FxCueOrigin.Worker);
            w.TryFxCue(FxCueId.HandUpgrade,shop.FxActionSequenceForValidation+101,FxCueOrigin.Module);
            Check(w.HandCueCount==hands&&w.PlayerInteractionCount==countBefore,"Worker/module-origin callbacks cannot create operating hand or manual placement feedback");
        }
        IEnumerator PlayerMovement()
        {
            var w=shop.World;shop.SetPanel(ShopPanel.None);CollectionMouse(false);CollectionAim(CultureKind.Bloom);
            yield return null;yield return null;yield return new WaitForSeconds(.2f);
            Vector3 rest=w.ToolRoot.localPosition,camera=shop.Player.View.transform.localPosition,start=shop.Player.transform.position;
            float peak=0;bool down=false,up=false;
            shop.AllowValidationInput=true;
            yield return PlayerInteractionClip("player-movement-live",1.6f,age=>{
                if(!down&&age>=.25f){down=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.A));}
                if(!up&&age>=.75f){up=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());}
                peak=Mathf.Max(peak,Vector3.Distance(rest,w.ToolRoot.localPosition));
                if(shop.Player.View.transform.localPosition!=camera)throw new InvalidOperationException("Movement changed camera position");
            });
            shop.AllowValidationInput=false;
            Check(Vector3.Distance(start,shop.Player.transform.position)>.05f&&peak>.0001f&&peak<.02f,"Actual horizontal travel produces bounded tool-only sway");
            Check(Vector3.Distance(rest,w.ToolRoot.localPosition)<.00001f,"Stopped actual movement returns exactly to supported rest");
        }
        IEnumerator PlayerLifecycleAndSlap()
        {
            var w=shop.World;
            yield return CollectionBeginEdge();yield return CycleTap(Key.Escape);CollectionMouse(false);
            Check(shop.Panel==ShopPanel.Pause&&!w.CollectionActive&&!w.HandsVisual.gameObject.activeInHierarchy,"Actual Escape pauses active hold and hides hands");
            yield return CycleTap(Key.Escape);Check(shop.Panel==ShopPanel.None,"Actual Escape resumes without deferred hand action");
            foreach(var panel in new[]{ShopPanel.Journal,ShopPanel.Arrange}){
                shop.SetPanel(panel);yield return null;
                Check(!w.HandsVisual.gameObject.activeInHierarchy&&w.PlayerInteractionRemaining<=0,"Panel/arrangement clears all player transient ownership / "+panel);
                shop.SetPanel(ShopPanel.None);
            }
            yield return CollectionBeginEdge();shop.SetPanel(ShopPanel.Journal);yield return null;shop.SetPanel(ShopPanel.None);yield return new WaitForSeconds(.12f);
            Check(!w.CollectionActive&&shop.HarvestProgress==0,"A held button across panel close does not silently reacquire collection");
            yield return CollectionRelease();CollectionMouse(true);yield return new WaitForSeconds(.12f);
            Check(w.CollectionActive&&shop.HarvestProgress>0,"Explicit fresh press reacquires collection after panel cancellation");yield return CollectionRelease();
            var incubator=StationOf(StationKind.Incubator).transform;
            GateAim(incubator.TransformPoint(new Vector3(0,.05f,-2.15f)),incubator.TransformPoint(new Vector3(0,1.5f,-.1f)));
            yield return null;yield return null;shop.SetPanel(ShopPanel.Pause);
            InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.Escape,Key.E));yield return null;yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());yield return null;
            Check(shop.Panel==ShopPanel.None,"Simultaneous Escape and E closes pause without reopening focused station");
            // Real customer slap retains customer/cooldown/input authority. No preview gesture is used.
            yield return EnterCounter();Check(shop.OpenForDay(),"Open stocked actual day for slap integration");shop.Customers.ArrivalsEnabled=false;
            var visitor=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>visitor.Phase==CustomerPhase.Queued,35,"real queued slap customer");
            yield return AimForSlap(visitor);
            int hands=w.HandCueCount,slaps=shop.Reports.Current.slapped;bool strike=false,overlap=false,verified=false,released=false,heldAfter=false;
            shop.AllowValidationInput=true;
            yield return PlayerInteractionClip("player-slap-collection-overlap-live",1.8f,age=>{
                if(!strike&&age>=.25f){strike=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.F));}
                if(!overlap&&age>=.32f){
                    overlap=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());
                    Check(shop.Reports.Current.slapped==slaps+1&&shop.SlapGesture.IsPlaying&&!w.HandsVisual.gameObject.activeInHierarchy,"Actual remappable slap key owns the sole visible hand gesture");
                    CollectionAim(CultureKind.Bloom);CollectionMouse(true);
                }
                if(!verified&&age>=.40f){
                    verified=true;
                    Check(shop.SlapGesture.IsPlaying&&!w.CollectionActive&&w.CollectionParticleCountForValidation==0&&shop.HarvestProgress==0&&w.HandCueCount==hands,
                        "Held collection during actual slap cannot create operating hands, intake or a partial unit");
                }
                if(!heldAfter&&age>=.95f){heldAfter=true;Check(!shop.SlapGesture.IsPlaying&&!w.CollectionActive&&shop.HarvestProgress==0,"Hold across slap recovery remains canceled until a fresh press");}
                if(!released&&age>=1.15f){released=true;CollectionMouse(false);}
            });
            Check(verified&&!shop.SlapGesture.IsPlaying&&w.HandsVisual.gameObject.activeInHierarchy,"Actual slap recovery restores neutral operating grip");
            shop.AllowValidationInput=false;
        }
        IEnumerator FxPlayerInteractionScenario()
        {
            limitations.Add("FX11 uses actual commands, Physics focus and synthetic keyboard/mouse input at scripted operator positions. Specimen preparation is explicitly accelerated, rack construction uses200fixture credits and the existing capacity purchase uses its listed cost. Save failure uses an isolated .tmp obstruction. Silent720p/1080p HUD clips are technical evidence only; native controls, listening, creative acceptance and GPU causality remain open. No carrying inventory/tool selector exists or is invented.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX11 exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionPlayerInteractionKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionPlayerInteractionMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor){int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));
                Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);
                Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);
                Check(Screen.width==width&&Screen.height==height,"Actual requested FX11 HUD dimensions");}
            var w=shop.World;
            Check(shop.Model.Capacity==shop.Config.BaseCapacity,"Actual initial base tool capacity");
            yield return PlayerMovement();
            yield return CollectionLive(CultureKind.Bloom,"player-collection-base-live");
            StaffCreditFixture(shop.Config.UpgradeCost,"FX11 existing siphon purchase fixture");shop.SetPanel(ShopPanel.Upgrade);
            int research=w.ResearchAutomationForValidation.ResearchCount,hands=w.HandCueCount,sounds=shop.GetComponent<ShopAudio>().ResearchPlaybackCount;
            bool purchased=false;
            yield return PlayerInteractionClip("player-upgrade-live",1.6f,age=>{
                if(purchased||age<.25f)return;purchased=true;Ok(shop.BuySiphonUpgrade(),"Actual saved siphon upgrade");
                Check(shop.SaveError==null&&w.ResearchAutomationForValidation.ResearchCount==research+1&&shop.GetComponent<ShopAudio>().ResearchPlaybackCount==sounds+1,
                    "Saved modal upgrade gives one accepted existing research confirmation");
                Check(w.HandCueCount==hands&&!w.HandsVisual.gameObject.activeInHierarchy,"Upgrade modal does not fake a hand through panel");
            });
            shop.SetPanel(ShopPanel.None);Check(shop.Model.Capacity==shop.Config.UpgradedCapacity,"Actual paid upgraded tool capacity");
            yield return CollectionLive(CultureKind.Flow,"player-collection-upgraded-live");
            yield return CollectionEdges();
            // Reused actual edge route covers release/full/empty/reach/LOS/side-path/pause/panel/focus/reload/failed-save.
            yield return PlayerTransfers();yield return PlayerPlacementInterruptions();yield return PlayerLifecycleAndSlap();
            Check(w.FxPool.OwnedCount==96&&w.FxPool.OwnedMeshCount==5&&w.FxPool.OwnedMaterialCount==1&&shop.GetComponents<AudioSource>().Length==5,
                "FX11 retains fixed96-slot five-mesh one-material five-voice ownership (3 short +2 loops)");
            Check(w.HandsVisual.GetComponentsInChildren<Renderer>(true).Length==12,"Existing imported hands retain twelve renderers");
            Check(shop.SaveNow(),"Final isolated FX11 state saves");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif