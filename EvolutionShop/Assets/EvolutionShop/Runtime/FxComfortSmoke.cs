#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
namespace EvolutionShop
{
 public sealed partial class MilestoneSmoke
 {
  [Serializable] sealed class ComfortFrame
  {public string clip,phase;public int renderFrame,active,owned,actionLimit,ambientLimit,emitterLimit,materials,meshes,voices,activeVoices,renderers,currency,occupiedTanks,occupiedStorage;public float realtime,frameSeconds,master,humor,ambience,fishTravel,fishAngle;public long monoBytes;public bool reduced,low,effectiveReduced,effectiveLow,reducedGags,paused;}
  [Serializable] sealed class ComfortReceipt
  {public string scope="FX20 actual saved settings and native HWND controls; labelled construction/stock preparation. Frame time includes capture overhead; no causal GPU/human/listening approval.";public ComfortFrame[] frames;public int ownedItems,tankCapacity,storageCapacity,occupiedTanks,occupiedStorage,baselineOwnedItems,baselineOccupiedTanks,baselineOccupiedStorage;public string[] baselineSpecimenIds;}
  readonly List<ComfortFrame> comfortFrames=new List<ComfortFrame>();
  AudioSource[] comfortVoices;Renderer[] comfortRenderers;string comfortVat;string[] comfortSlots;int comfortItems,comfortBaseStations;string[] comfortOccupiedIds;int comfortTankCapacity,comfortStorageCapacity;Transform comfortFish;Vector3 comfortFishStart;Quaternion comfortFishRotation;float comfortFishTravel,comfortFishAngle;
  void ComfortObserve(string clip)
  {
   if(clip.StartsWith("comfort-occupied-")&&comfortFish){comfortFishTravel=Mathf.Max(comfortFishTravel,Vector3.Distance(comfortFish.localPosition,comfortFishStart));comfortFishAngle=Mathf.Max(comfortFishAngle,Quaternion.Angle(comfortFish.localRotation,comfortFishRotation));}
   var s=shop.ComfortSnapshot;var a=shop.GetComponent<ShopAudio>();int tankCount=0,storageCount=0,visible=0;
   foreach(string id in shop.Model.TankIds)if(shop.Model.GetTank(id).HasValue)tankCount++;
   foreach(string id in shop.Model.StorageIds)if(shop.Model.GetStorage(id).HasValue)storageCount++;
   if(comfortRenderers!=null)foreach(var renderer in comfortRenderers)if(renderer&&renderer.enabled&&renderer.gameObject.activeInHierarchy)visible++;
   if(s.OwnedParticles!=96||s.ActiveParticles<0||s.ActiveParticles>s.ActionLimit+s.AmbientLimit||s.OwnedMaterials!=1||a.MixSnapshot.VoiceCapacity!=5||a.ActiveVoiceCount>5)throw new InvalidOperationException("Comfort fixed resource ceiling escaped");
   comfortFrames.Add(new ComfortFrame{clip=clip,fishTravel=comfortFishTravel,fishAngle=comfortFishAngle,phase=shop.Model.Incubator.Phase.ToString(),renderFrame=Time.frameCount,realtime=Time.realtimeSinceStartup,frameSeconds=Time.unscaledDeltaTime,monoBytes=Profiler.GetMonoUsedSizeLong(),active=s.ActiveParticles,owned=s.OwnedParticles,actionLimit=s.ActionLimit,ambientLimit=s.AmbientLimit,emitterLimit=s.EmitterLimit,materials=s.OwnedMaterials,meshes=s.OwnedMeshes,voices=comfortVoices.Length,activeVoices=a.ActiveVoiceCount,renderers=visible,currency=shop.Model.Currency,occupiedTanks=tankCount,occupiedStorage=storageCount,reduced=shop.ReducedVisualMotion,low=shop.EffectsDetail==FxEffectDetail.Low,effectiveReduced=s.EffectiveReducedMotion,effectiveLow=s.EffectiveLowDetail,reducedGags=shop.ReducedGags,master=shop.Volume,humor=shop.HumorVolume,ambience=shop.AmbienceVolume,paused=shop.Paused});
  }
  void ComfortFlush()
  {
   var ids=new List<string>();int tanks=0,storage=0;foreach(string id in shop.Model.TankIds)if(shop.Model.GetTank(id).HasValue){tanks++;ids.Add(shop.Model.GetTank(id).Value.Id);}foreach(string id in shop.Model.StorageIds)if(shop.Model.GetStorage(id).HasValue){storage++;ids.Add(shop.Model.GetStorage(id).Value.Id);}
   File.WriteAllText(Path.Combine(evidence,"comfort.json"),JsonUtility.ToJson(new ComfortReceipt{frames=comfortFrames.ToArray(),ownedItems=shop.Layout.Items.Count,tankCapacity=shop.Model.TankIds.Length,storageCapacity=shop.Model.StorageIds.Length,occupiedTanks=tanks,occupiedStorage=storage,baselineOwnedItems=comfortItems,baselineOccupiedTanks=comfortTankCapacity,baselineOccupiedStorage=comfortStorageCapacity,baselineSpecimenIds=comfortOccupiedIds??Array.Empty<string>()},true));NativeUiFlush();
  }
  IEnumerator ComfortClip(string name,float seconds,IEnumerator action=null)
  {
   nativeUiActionDone=action==null;nativeUiActionFailure=null;if(action!=null)StartCoroutine(NativeUiRunAction(action));
   try{using(var r=new FxReviewRecorder(ShopSession.Arg("-evolutionFxReviewEncoder"),Path.Combine(evidence,name+".mp4"))){while(r.Age<seconds||!nativeUiActionDone){if(nativeUiActionFailure!=null)throw nativeUiActionFailure;if(r.Age>seconds+30)throw new TimeoutException(name);yield return new WaitForEndOfFrame();ComfortObserve(name);NativeUiObserve(name);r.Sample();if(r.Failure!=null)throw new InvalidOperationException(r.Failure);}float deadline=Time.realtimeSinceStartup+3;while(r.Pending>0){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(name+" drain");yield return null;}var receipt=r.Finish();File.WriteAllText(Path.Combine(evidence,name+"-frames.json"),JsonUtility.ToJson(receipt,true));Check(receipt.achievedFPS>=25&&receipt.missedSamples<=receipt.wallSeconds*5,"Actual comfort HUD video / "+name);}}
   finally{ComfortFlush();}
  }
  void ComfortPolicy(bool reduced,bool low)
  {var s=shop.ComfortSnapshot;Check(shop.ReducedVisualMotion==reduced&&s.EffectiveReducedMotion==reduced&&s.LowDetail==low&&s.EffectiveLowDetail==low,"Saved preference reaches actual world policy");Check(s.ActionLimit==(low?24:64)&&s.AmbientLimit==(low?8:32)&&s.EmitterLimit==(low?6:12),"Existing bounded detail admission limits");Check(shop.Volume==.65f&&shop.HumorVolume==.37f&&shop.AmbienceVolume==.29f&&!shop.ReducedGags,"Visual settings preserve independent audio and gag preferences");}
  IEnumerator ComfortNativeMode(bool reduced,bool low)
  {
   shop.SetPanel(ShopPanel.Pause);yield return null;
   if(shop.ReducedVisualMotion!=reduced){string label="Reduced visual motion: "+(shop.ReducedVisualMotion?"On":"Off");yield return NativeUiFind(label);Rect before=nativeUiRect;yield return NativeUiClick(label);yield return NativeUiFind("Reduced visual motion: "+(reduced?"On":"Off"));Check(nativeUiRect==before,"Comfort native toggle preserves exact hit bounds");}
   if((shop.EffectsDetail==FxEffectDetail.Low)!=low){string label="Effects detail: "+(shop.EffectsDetail==FxEffectDetail.Low?"Low":"Standard");yield return NativeUiFind(label);Rect before=nativeUiRect;yield return NativeUiClick(label);yield return NativeUiFind("Effects detail: "+(low?"Low":"Standard"));Check(nativeUiRect==before,"Comfort detail native control preserves exact hit bounds");}
   yield return NativeUiFind("Effects detail: "+(low?"Low":"Standard"));
   ComfortPolicy(reduced,low);Check(SaveStore.Read(shop.SavePath,shop.Config,out _,out var slot,out var error)&&slot.reducedVisualMotion==reduced&&slot.effectsDetail==(low?1:0),"Native controls persist selected policy / "+error);
  }
  string ComfortStock()
  {string value=shop.Model.Currency+"/"+shop.Model.Incubator.SpecimenId;foreach(string id in shop.Model.TankIds)value+="/"+id+":"+(shop.Model.GetTank(id).HasValue?shop.Model.GetTank(id).Value.Id:"empty");foreach(string id in shop.Model.StorageIds)value+="/"+id+":"+(shop.Model.GetStorage(id).HasValue?shop.Model.GetStorage(id).Value.Id:"empty");return value;}
  IEnumerator ComfortPlace(string definition,int preferredX,int preferredZ)
  {
   bool found=false;int x=0,z=0,turn=0;Vector3 approach=default;
   for(int radius=0;radius<=12&&!found;radius++)for(int dz=-radius;dz<=radius&&!found;dz++)for(int dx=-radius;dx<=radius&&!found;dx++){
    if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=radius)continue;
    for(int t=0;t<4&&!found;t++){
     int cx=preferredX+dx,cz=preferredZ+dz;var result=shop.Layout.TryPreview(definition,null,cx,cz,t,out var candidate);if(!result.Success)continue;
     var point=new Vector3(cx*ShopLayout.GridSize,.2f,cz*ShopLayout.GridSize)+Quaternion.Euler(0,t*90,0)*Vector3.back*1.9f;
     if(!candidate.CanReachMainAisle(point.x,point.z)||!shop.Layout.CanReachMainAisle(point.x,point.z))continue;
     x=cx;z=cz;turn=t;approach=point;found=true;
    }
   }
   Check(found,"Actually legal reachable occupied-layout placement / "+definition);yield return EditedPlace(null,definition,x,z,turn,approach);
  }
  void ComfortCheckWorld()
  {
   int expectedStations=comfortBaseStations,expectedRoots=0;
   foreach(var item in shop.Layout.Items){if(item.Stored)continue;expectedRoots++;expectedStations+=ShopLayout.DefinitionSlotCount(item.DefinitionId);if(item.DefinitionId==ShopLayout.RackDefinitionId||item.DefinitionId==ShopLayout.ConditionerDefinitionId)expectedStations++;
    Check(shop.World.TryGetPlacedTable(item.Id,out var root)&&root.gameObject.activeInHierarchy&&root.GetComponentsInChildren<Collider>().Length>0,"Occupied layout retains actual collision and stable identity / "+item.Id);
   }
   Check(expectedRoots==ShopLayout.MaxOwnedItems&&shop.World.GetComponentsInChildren<PlacedWorkTable>().Length==expectedRoots&&shop.World.GetComponents<Unity.AI.Navigation.NavMeshSurface>().Length==1,"Occupied layout has exact16 active owned roots and one native surface");
   Check(shop.World.Stations.Count==expectedStations,"Occupied layout station count equals original foundation plus actual display compartments, racks and vat");
  }
  IEnumerator ComfortBuildOccupied()
  {
   StaffCreditFixture(3000,"FX20 labelled maximum-layout preparation only");foreach(string species in new[]{"pondlet","bubblebelly","ribbonfin"}){SpecimenProduce(species);Ok(shop.Model.TransferToTank("tank-a"),"Labelled discovery seed transfer");Ok(shop.Model.SellFromTank("tank-a"),"Labelled discovery seed sale");}shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save labelled discovery prerequisites");
   yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);Check(shop.ResearchSpark(),"Actual research prerequisite for occupied vat");Ok(shop.BuySiphonUpgrade(),"Actual capacity prerequisite");yield return AdvancedProduce("lanternfin");Ok(shop.TransferToDisplay("tank-a"),"Preserve actual Lanternfin prerequisite");Ok(shop.Model.SellFromTank("tank-a"),"Labelled prerequisite sale before identical Pondlet comparison workload");shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save actual discovery prerequisite");
   Check(shop.OpenPlacementMenu()&&shop.BuyStoreExpansion()&&shop.BuyStoreExpansion(),"Both real store expansions");GateAim(new Vector3(0,.2f,2),new Vector3(0,1.2f,5));yield return null;Check(shop.OpenLabMenu()&&shop.BuyLabExpansion()&&shop.BuyLabExpansion(),"Both real lab expansions");shop.SetPanel(ShopPanel.None);comfortBaseStations=shop.World.Stations.Count;
   yield return EditedPlace(null,ShopLayout.LargeDisplayId,-4,-1,0,new Vector3(-2,.2f,-2.3f));yield return EditedPlace(null,ShopLayout.LargeDisplayId,-18,-3,0,new Vector3(-9,.2f,-4.5f));yield return EditedPlace(null,ShopLayout.LargeDisplayId,-26,-3,0,new Vector3(-13,.2f,-4.5f));
   yield return EditedPlace(null,ShopLayout.TableDefinitionId,9,4,1,new Vector3(3.5f,.2f,3));yield return EditedPlace(null,ShopLayout.TableDefinitionId,6,3,1,new Vector3(1.5f,.2f,1.5f));yield return EditedPlace(null,ShopLayout.TableDefinitionId,11,3,1,new Vector3(6.4f,.2f,2.8f));
   yield return EditedPlace(null,ShopLayout.CoralPlanterId,10,8,0,new Vector3(3.5f,.2f,3));yield return EditedPlace(null,ShopLayout.CoralPlanterId,0,8,0,new Vector3(0,.2f,2.9f));yield return EditedPlace(null,ShopLayout.CoralPlanterId,-28,-8,0,new Vector3(-14,.2f,-2.8f));yield return EditedPlace(null,ShopLayout.BubbleLampId,-23,-8,0,new Vector3(-11.5f,.2f,-2.8f));yield return EditedPlace(null,ShopLayout.SillyStatueId,-20,-8,0,new Vector3(-10,.2f,-2.8f));yield return EditedPlace(null,ShopLayout.BubbleLampId,-17,-8,0,new Vector3(-8.5f,.2f,-2.8f));
   yield return ComfortPlace(ShopLayout.ConditionerDefinitionId,-6,14);comfortVat=shop.Model.ConditionerIds[0];yield return ComfortPlace(ShopLayout.RackDefinitionId,9,15);yield return ComfortPlace(ShopLayout.RackDefinitionId,-9,22);yield return ComfortPlace(ShopLayout.RackDefinitionId,7,23);comfortSlots=shop.Model.StorageIds;comfortItems=shop.Layout.Items.Count;
   Check(comfortItems==ShopLayout.MaxOwnedItems&&shop.Layout.StoredCount==0&&comfortSlots.Length==6&&shop.Model.TankIds.Length==8,"Actual16 placed ownership cap, eight display slots and six rack slots");ComfortCheckWorld();EditedCheckRoutes("FX20 actual maximum owned");
   foreach(string id in shop.Model.TankIds)if(!shop.Model.GetTank(id).HasValue){SpecimenProduce("pondlet");Ok(shop.TransferToDisplay(id),"Labelled occupied display preparation / "+id);}
   foreach(string id in comfortSlots){SpecimenProduce("pondlet");string rack=null;foreach(var item in shop.Layout.Items)foreach(string slot in shop.Layout.GetStorageSlotIds(item.Id))if(slot==id)rack=item.Id;Check(rack!=null,"Exact owned rack mapping");yield return SpecimenRackPanel(rack);Check(shop.StoreReadyInRack(id),"Labelled exact occupied rack preparation / "+id);}
   advancedVat=comfortVat;yield return AdvancedBeginBatch(comfortSlots[0],comfortSlots[1]);yield return WaitFor(()=>shop.Model.GetConditioner(comfortVat).Value.Phase==ConditionerPhase.Ready,20,"Actual two-resident vat finishes naturally");
   foreach(string id in new[]{comfortSlots[0],comfortSlots[1]}){SpecimenProduce("pondlet");string rack=null;foreach(var item in shop.Layout.Items)foreach(string slot in shop.Layout.GetStorageSlotIds(item.Id))if(slot==id)rack=item.Id;yield return SpecimenRackPanel(rack);Check(shop.StoreReadyInRack(id),"Refill exact vacated rack slot after conditioner transfer");}
   Check(shop.SaveNow()&&shop.SaveError==null,"Save legal fully occupied layout provenance");File.Copy(shop.SavePath,Path.Combine(evidence,"comfort-occupied-save.json"),true);comfortTankCapacity=shop.Model.TankIds.Length;comfortStorageCapacity=comfortSlots.Length;var occupied=new List<string>();foreach(string id in shop.Model.TankIds){Check(shop.Model.GetTank(id).HasValue,"Maximum layout display actually occupied / "+id);occupied.Add(shop.Model.GetTank(id).Value.Id);}foreach(string id in comfortSlots){Check(shop.Model.GetStorage(id).HasValue,"Maximum layout rack actually occupied / "+id);occupied.Add(shop.Model.GetStorage(id).Value.Id);}Check(shop.Model.GetConditioner(comfortVat).Value.Count==2,"Maximum layout vat retains two actual residents");comfortOccupiedIds=occupied.ToArray();shop.SetPanel(ShopPanel.None);
  }
  IEnumerator ComfortStartBatch()
  {yield return new WaitForSeconds(.25f);Check(shop.DoseCulture(CultureKind.Bloom)&&shop.DoseCulture(CultureKind.Flow)&&shop.BeginManualIncubation(),"Actual accepted dose and start under selected comfort mode");shop.SetPanel(ShopPanel.None);}
  IEnumerator ComfortLabMode(string mode)
  {
   // Empty one display through the actual domain sale, then prepare only carried inputs; recorded growth uses ordinary frames.
   Ok(shop.Model.SellFromTank("tank-a"),"Labelled room for next observed output / "+mode);shop.Model.Tick(60);Ok(shop.Model.Collect(CultureKind.Bloom),"Labelled carried Bloom / "+mode);Ok(shop.Model.Collect(CultureKind.Flow),"Labelled carried Flow / "+mode);shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save labelled mode input preparation");DoseAim();yield return null;yield return null;yield return ComfortClip("comfort-lab-"+mode,shop.Config.IncubationSeconds+1.5f,ComfortStartBatch());Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready,"Essential actual ready state survives mode / "+mode);string specimen=shop.Model.Incubator.SpecimenId;Ok(shop.TransferToDisplay("tank-a"),"Actual saved output fills display / "+mode);Check(shop.Model.GetTank("tank-a").Value.Id==specimen,"Exact specimen identity survives comfort mode");
   yield return AdvancedClear();DoseAim();yield return null;int cash=shop.Model.Currency;Check(!shop.DoseCulture(CultureKind.Bloom)&&shop.Model.ReservedCultureCount==0&&shop.Model.Currency==cash,"Actual shortage remains refused without resources / "+mode);Check(!string.IsNullOrEmpty((string)typeof(ShopSession).GetField("message",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(shop)),"Essential shortage has actual status text / "+mode);yield return ComfortClip("comfort-shortage-"+mode,.8f);shop.SetPanel(ShopPanel.None);
  }
  IEnumerator ComfortPreviewPolicies()
  {
   foreach(bool reduced in new[]{false,true}){
    shop.SetPanel(ShopPanel.Pause);Check(shop.SetComfortSettings(reduced,FxEffectDetail.Standard),"Saved visual preference for actual user preview");string stock=ComfortStock(),disk=File.ReadAllText(shop.SavePath);shop.SetPanel(ShopPanel.Presentation);shop.SelectPresentationView(4);shop.SetPresentationPose(CharacterSamplePose.Walk);shop.SetPresentationMotion(true);yield return new WaitForSeconds(.12f);
    var stage=shop.World.transform.Find("Look-only presentation stage");Transform employee=null;foreach(Transform child in stage.Find("Turntable"))if(child.name.StartsWith("Employee study"))employee=child;Check(employee,"Actual user preview employee exists");var leg=employee.GetComponent<ShopCharacterVisual>().LeftLeg;Quaternion start=leg.localRotation;float angle=0;for(int i=0;i<3;i++){yield return new WaitForSeconds(.13f);angle=Mathf.Max(angle,Quaternion.Angle(start,leg.localRotation));}Check(reduced?angle<.001f:angle>.05f,"User presentation walk obeys saved visual reduction");
    shop.SetPanel(ShopPanel.Pause);shop.OpenComedyPreview();Check(shop.PlayComedyPreview(ComedyKind.Belch),"Actual user comedy preview starts independently of visual preference");yield return new WaitForSeconds(.15f);Check(reduced?!GateBubble()||!GateBubble().gameObject.activeSelf:GateBubble()&&GateBubble().gameObject.activeSelf,"Actual preview bubble obeys visual preference while reduced-gag remains off");comfortRenderers=shop.World.GetComponentsInChildren<Renderer>(true);yield return ComfortClip("comfort-preview-"+(reduced?"reduced":"standard"),1);Check(ComfortStock()==stock&&File.ReadAllText(shop.SavePath)==disk&&!shop.ReducedGags,"Look-only preview does not mutate stock, saved bytes or gag preference");shop.SetPanel(ShopPanel.None);
   }
  }
  IEnumerator ComfortLifecycle()
  {
   shop.SetPanel(ShopPanel.Pause);bool reduced=shop.ReducedVisualMotion;var detail=shop.EffectsDetail;string disk=File.ReadAllText(shop.SavePath),stock=ComfortStock();Check(!shop.SetComfortSettings(!reduced,(FxEffectDetail)2)&&File.ReadAllText(shop.SavePath)==disk&&shop.ReducedVisualMotion==reduced,"Invalid detail rejected without policy or durable mutation");Check(shop.SetComfortSettings(reduced,detail)&&File.ReadAllText(shop.SavePath)==disk,"Identical comfort request preserves durable bytes");TutorialBlockSave();
   try{yield return ComfortClip("comfort-failed-setting",1.5f,NativeUiClick("Reduced visual motion: "+(reduced?"On":"Off")));Check(shop.SaveError!=null&&shop.ReducedVisualMotion==reduced&&shop.World.VisualMotionReduced==reduced&&shop.EffectsDetail==detail&&File.ReadAllText(shop.SavePath)==disk&&ComfortStock()==stock,"Failed actual GUI setting preserves live/world/durable mode and exact stock");}finally{TutorialUnblockSave();}
   Check(shop.SaveNow()&&shop.ReloadSaved(),"Comfort normal save/reload after failure");ComfortPolicy(reduced,detail==FxEffectDetail.Low);comfortRenderers=shop.World.GetComponentsInChildren<Renderer>(true);
   shop.ReducedGags=true;Check(shop.SaveNow()&&shop.ReloadSaved()&&shop.ReducedGags&&shop.ReducedVisualMotion==reduced&&shop.EffectsDetail==detail,"Reduced gag remains independently saved");shop.ReducedGags=false;
   foreach(string boundary in new[]{"pause","focus","arrange","disable"}){
    shop.SetPanel(ShopPanel.None);shop.World.ResetFxPresentation();Check(shop.World.TryFxCue(FxCueId.HatchRepeat,1),"Labelled lifecycle cue fixture after presentation reset / "+boundary);yield return WaitFor(()=>shop.World.FxPool.ActiveCount>0,1,"Hatch release reaches the pool before comfort lifecycle / "+boundary);Check(shop.World.FxPool.ActiveCount>0,"Actual active cue before comfort lifecycle / "+boundary);
    if(boundary=="pause")shop.SetPanel(ShopPanel.Pause);else if(boundary=="focus")shop.SetFxFocusForValidation(false);else if(boundary=="arrange")Check(shop.OpenPlacementMenu(),"Actual Arrange cancellation");else shop.enabled=false;
    Check(shop.World.FxPool.ActiveCount==0,"Immediate pool cleanup / "+boundary);yield return new WaitForSeconds(.35f);Check(shop.World.FxPool.ActiveCount==0&&ComfortStock()==stock,"Held boundary does not replay cue or mutate stock / "+boundary);shop.enabled=true;shop.SetFxFocusForValidation(true);shop.SetPanel(ShopPanel.None);yield return null;
   }
   yield return ComfortClip("comfort-lifecycle-rest",1.2f);Check(shop.SaveNow()&&shop.ReloadSaved()&&shop.ReducedVisualMotion==reduced&&shop.EffectsDetail==detail,"Final reload retains selected comfort policy");shop.enabled=true;shop.SetPanel(ShopPanel.None);shop.ConfirmNewGame();Check(shop.ReducedVisualMotion==reduced&&shop.EffectsDetail==detail,"New game retains explicitly selected preferences");Check(SaveStore.Read(shop.SavePath,shop.Config,out _,out var freshSlot,out var freshError)&&freshSlot.reducedVisualMotion==reduced&&freshSlot.effectsDetail==(int)detail&&freshSlot.volume==.65f&&freshSlot.humorVolume==.37f&&freshSlot.ambienceVolume==.29f&&!freshSlot.reducedGags,"New game writes explicit comfort, audio and gag preferences / "+freshError);ComfortPolicy(reduced,detail==FxEffectDetail.Low);yield return new WaitForEndOfFrame();Check(shop.ComfortSnapshot.OwnedParticles==96&&shop.GetComponent<ShopAudio>().GetComponents<AudioSource>().Length==5,"Reset keeps fixed owned resources");
  }
  IEnumerator FxComfortScenario()
  {
   limitations.Add("FX20 own-process HWND messages exercise native controls; not physical-human input. Explicit isolated cash/recipe seed only. Exact legal occupied layout and ordinary-frame actual cues; capture-time frame/managed-memory observations are not causal CPU/GPU or allocation-free claims. No listening/creative acceptance.");
   yield return new WaitForSeconds(1);Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX20 isolated save only");
   previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("FX20 isolated keyboard");cycleMouse=InputSystem.AddDevice<Mouse>("FX20 isolated mouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("comfort-input-provenance.json");GateFresh();shop.Volume=.65f;shop.HumorVolume=.37f;shop.AmbienceVolume=.29f;shop.ReducedGags=false;Check(shop.SaveNow(),"Save independent comfort fixture preferences");
   yield return ComfortBuildOccupied();comfortRenderers=shop.World.GetComponentsInChildren<Renderer>(true);comfortVoices=shop.GetComponent<ShopAudio>().GetComponents<AudioSource>();yield return new WaitForEndOfFrame();
   foreach(bool low in new[]{false,true})foreach(bool reduced in new[]{false,true}){
    string mode=(low?"low":"standard")+"-"+(reduced?"reduced":"motion");yield return ComfortClip("comfort-settings-"+mode,2,ComfortNativeMode(reduced,low));shop.SetPanel(ShopPanel.None);GateAim(new Vector3(0,.2f,-3.5f),new Vector3(-2,1.2f,-1));yield return new WaitForSeconds(1);string before=ComfortStock();comfortFish=shop.World.PondletVisualForValidation("tank-a");Check(comfortFish,"Actual occupied Pondlet transform");comfortFishStart=comfortFish.localPosition;comfortFishRotation=comfortFish.localRotation;comfortFishTravel=comfortFishAngle=0;int trails=shop.World.AquariumTrailEmissionCount;yield return ComfortClip("comfort-occupied-"+mode,4);Check(reduced?comfortFishTravel<.00001f&&comfortFishAngle<.001f&&shop.World.AquariumTrailEmissionCount==trails:comfortFishTravel>.001f||comfortFishAngle>.1f,"Source-backed actual aquarium motion policy / "+mode);Check(ComfortStock()==before,"Quiet mode observation changes no specimen or economic authority / "+mode);yield return ComfortLabMode(mode);
   }
   yield return ComfortPreviewPolicies();yield return ComfortLifecycle();ComfortFlush();
  }
 }
}
#endif
