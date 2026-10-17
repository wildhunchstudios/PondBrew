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
  [Serializable] sealed class WorkerFrame { public float seconds,distance,gait,speed,progress,eventAge;public int sample,events,proxies,particles;public string worker,job,activity,eventKind,eventJob,proxyIdentity;public Vector3 position,proxyPosition;public bool carrying,paused,reduced; }
  [Serializable] sealed class WorkerReceipt {public string context;public WorkerFrame[] frames;public Vector3 meshMinimum,meshMaximum;public int geometrySamples;public string[] neutralEnvelopes;}
  readonly List<WorkerFrame> workerFrames=new List<WorkerFrame>();
  readonly Dictionary<ShopCharacterVisual,WorkerFrame> workerPrevious=new Dictionary<ShopCharacterVisual,WorkerFrame>();
  readonly Dictionary<ShopCharacterVisual,float> workerHeight=new Dictionary<ShopCharacterVisual,float>();readonly List<string> workerNeutral=new List<string>();
  readonly HashSet<string> workerFacingChecks=new HashSet<string>();
  float workerStarted;Vector3 workerMin=Vector3.one*100,workerMax=Vector3.one*-100;int workerGeometry;
  void WorkerObserve()
  {
   foreach(var a in new[]{shop.Cashier.Actor,shop.Stocker.Actor,shop.Technician.Actor}) {
    if(!a||!a.isActiveAndEnabled)continue;var s=a.WorkerSnapshot;
    if(workerPrevious.TryGetValue(a,out var old)&&s.SampleCount==old.sample)continue;
    if(!CustomerMotionFinite(s.TravelDistance)||!CustomerMotionFinite(s.GaitPhase)||!CustomerMotionFinite(s.Speed)||s.ProxyCount<0||s.ProxyCount>1)throw new InvalidOperationException("Invalid worker motion/proxy sample");
    if(old!=null&&s.SampleCount>old.sample&&s.TravelDistance>=old.distance) {
     float distance=s.TravelDistance-old.distance;
     if(Mathf.Abs(Mathf.DeltaAngle((s.GaitPhase-old.gait)*Mathf.Rad2Deg,distance*360/FxWorkerMotion.StrideLength))>.2f)throw new InvalidOperationException("Worker gait disagrees with accumulated movement / "+s.WorkerId);
     if(Flat(a.transform.position,old.position)>distance+.04f)throw new InvalidOperationException("Worker reported travel shorter than observed displacement / "+s.WorkerId);
    }
    Transform service=a==shop.Stocker.Actor?shop.Stocker.WorkerServiceTarget:a==shop.Technician.Actor?shop.Technician.WorkerServiceTarget:null;
    if(a!=shop.Cashier.Actor&&(s.Activity==FxWorkerActivity.Pickup||s.Activity==FxWorkerActivity.Place||s.Activity==FxWorkerActivity.Measure||s.Activity==FxWorkerActivity.Dose||s.Activity==FxWorkerActivity.Start)&&!service)throw new InvalidOperationException("Worker service lacks actual physical target / "+s.WorkerId);
    if(service&&s.Progress>=.35f&&!shop.Paused){Vector3 direction=service.position-a.transform.position;direction.y=0;if(Vector3.Angle(a.transform.forward,direction)>3)throw new InvalidOperationException("Worker service faces away from actual station / "+s.WorkerId+" / "+s.Activity);if(old!=null&&old.job==s.JobId&&old.activity==s.Activity.ToString()&&Flat(a.transform.position,old.position)>.001f)throw new InvalidOperationException("Station service changed worker root position");string key=s.WorkerId+" / "+s.Activity;if(workerFacingChecks.Add(key))Check(true,"Actual stationary service faces physical station / "+key);}
    if(a==shop.Cashier.Actor&&s.Activity==FxWorkerActivity.Checkout&&s.EventKind==FxWorkerEvent.None&&!shop.Paused){float minimum=100;int hands=0;foreach(var filter in a.GetComponentsInChildren<MeshFilter>())if(filter.name=="Mitten hand"&&filter.sharedMesh){hands++;foreach(var vertex in filter.sharedMesh.vertices)minimum=Mathf.Min(minimum,filter.transform.TransformPoint(vertex).y);}if(hands!=2||minimum<1.145f)throw new InvalidOperationException("Pip service hands hidden below countertop / "+minimum);if(workerFacingChecks.Add("Pip hand clearance"))Check(true,"Actual Pip mitten mesh clears authored countertop during checkout");}
    int particles=shop.World.FxPool.ParticleCountForValidation(a.WorkerFxOwner);if(particles>4)throw new InvalidOperationException("Worker accent exceeds bounded local budget");
    if(s.ProxyVisible&&(!s.Carrying||string.IsNullOrEmpty(s.ProxyIdentity)))throw new InvalidOperationException("Visible carry proxy lacks authoritative carrying identity");
    var f=new WorkerFrame{seconds=Time.realtimeSinceStartup-workerStarted,worker=s.WorkerId,job=s.JobId,activity=s.Activity.ToString(),eventKind=s.EventKind.ToString(),eventJob=s.EventJobId,proxyIdentity=s.ProxyIdentity,position=a.transform.position,proxyPosition=a.WorkerProxy?a.WorkerProxy.position:Vector3.zero,distance=s.TravelDistance,gait=s.GaitPhase,speed=s.Speed,progress=s.Progress,eventAge=s.EventAge,sample=s.SampleCount,events=s.EventCount,proxies=s.ProxyCount,particles=particles,carrying=s.Carrying,paused=s.Paused,reduced=s.Reduced};workerFrames.Add(f);workerPrevious[a]=f;
   }
  }
  IEnumerator WorkerWait(Func<bool> ready,float timeout,string reason)
  {float end=Time.realtimeSinceStartup+timeout;while(!ready()){WorkerObserve();if(Time.realtimeSinceStartup>end)throw new TimeoutException("FX15 "+reason+" / "+shop.Cashier.Status+" / "+shop.Stocker.Status+" / "+shop.Technician.Status);yield return null;}WorkerObserve();Check(true,"Actual worker transition / "+reason);}
  IEnumerator WorkerClip(string stem,float seconds,Action<float> action=null)
  {Action<float> tick=age=>{action?.Invoke(age);WorkerObserve();};if(Application.isEditor){float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<seconds){tick(Time.realtimeSinceStartup-start);yield return null;}}else yield return ReviewClip(stem,seconds,tick);}
  void WorkerAim(ShopCharacterVisual actor,bool lab=false)
  {
   shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;
   // Side/front views stay off the visitor queue and show both service station and hands.
   Vector3 camera;
   if(actor==shop.Cashier.Actor)camera=new Vector3(-4.0f,.2f,-4.5f);
   else if(actor==shop.Technician.Actor&&actor.transform.position.z>2.2f&&actor.transform.position.x<3)camera=new Vector3(-1.7f,.2f,2.5f);
   else if(actor.transform.position.z>1.5f&&actor.transform.position.x>3.5f)camera=new Vector3(2.3f,.2f,1.5f);
   else if(actor.transform.position.z<-.5f)camera=new Vector3(4.0f,.2f,-3.6f);
   else {camera=actor.transform.position+actor.transform.forward*2.8f+actor.transform.right*1.8f;camera.y=.2f;}
   GateAim(camera,actor.transform.position+Vector3.up*1.0f);
  }
  void WorkerAimHeld(ShopCharacterVisual actor)
  {
   shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;
   // Fixed clear aisle: actor-relative front offsets can land inside incubator equipment.
   GateAim(new Vector3(0,.2f,1.2f),actor.transform.position+Vector3.up*1.0f);
   // The real pause panel occupies the middle; keep both mittens in the exposed left strip.
   shop.Player.View.transform.rotation*=Quaternion.Euler(0,38,0);
  }
  void WorkerGeometry(ShopCharacterVisual actor)
  {
   foreach(var filter in actor.GetComponentsInChildren<MeshFilter>()) {
    bool label=false;for(var t=filter.transform;t&&t!=actor.transform;t=t.parent)if(t.name.IndexOf("sign",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("name",StringComparison.OrdinalIgnoreCase)>=0)label=true;
    if(label||!filter.sharedMesh||!filter.gameObject.activeInHierarchy)continue;
    foreach(var v in filter.sharedMesh.vertices){var p=actor.transform.InverseTransformPoint(filter.transform.TransformPoint(v));if(p.y<-.001f||p.y>workerHeight[actor]+.001f||Mathf.Abs(p.x)>.65f||Mathf.Abs(p.z)>.8f)throw new InvalidOperationException("Worker original mesh exceeds floor/local envelope / "+actor.name+" / "+filter.name+" / "+p);workerMin=Vector3.Min(workerMin,p);workerMax=Vector3.Max(workerMax,p);}
   }workerGeometry++;
  }
  void WorkerSweep(ShopCharacterVisual a)
  {
   var position=a.transform.position;var rotation=a.transform.rotation;string authority=FxAuthority();int events=a.WorkerSnapshot.EventCount;
   a.PreviewWorkerMotionForValidation(FxWorkerActivity.Idle,0,0,0,0,FxWorkerEvent.None,0,false,true);
   float neutralTop=0,ceiling=0;var head=a.transform.Find("Head pivot");
   foreach(var filter in a.GetComponentsInChildren<MeshFilter>()){if(!filter.sharedMesh||!filter.gameObject.activeInHierarchy)continue;bool label=false;for(var t=filter.transform;t&&t!=a.transform;t=t.parent)if(t.name.IndexOf("sign",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("name",StringComparison.OrdinalIgnoreCase)>=0)label=true;if(label)continue;
    foreach(var v in filter.sharedMesh.vertices){var world=filter.transform.TransformPoint(v);var point=a.transform.InverseTransformPoint(world);neutralTop=Mathf.Max(neutralTop,point.y);ceiling=Mathf.Max(ceiling,head&&filter.transform.IsChildOf(head)?head.localPosition.y+head.InverseTransformPoint(world).magnitude:point.y);}}
   workerHeight[a]=ceiling;workerNeutral.Add(a.name+" neutralTop="+neutralTop.ToString("R")+" rotationIndependentHeadCeiling="+ceiling.ToString("R"));
   Check(neutralTop>1.7f&&neutralTop<1.8f&&ceiling>=neutralTop,"Authored employee cap and exact head radial envelope measured / "+a.name);

   foreach(FxWorkerActivity activity in Enum.GetValues(typeof(FxWorkerActivity)))for(int i=0;i<=16;i++){a.PreviewWorkerMotionForValidation(activity,i/16f,i*Mathf.PI/4,i%2,1,FxWorkerEvent.None,0,activity==FxWorkerActivity.Carry||activity==FxWorkerActivity.Place,false);WorkerGeometry(a);if(activity==FxWorkerActivity.Carry||activity==FxWorkerActivity.Place)WorkerGrip(a);}
   foreach(FxWorkerEvent e in Enum.GetValues(typeof(FxWorkerEvent)))for(int i=0;i<=12;i++){a.PreviewWorkerMotionForValidation(FxWorkerActivity.Idle,0,0,0,1,e,FxWorkerMotion.EventDuration(e)*i/12,false,false);WorkerGeometry(a);}
   a.PreviewWorkerMotionForValidation(FxWorkerActivity.Idle,0,0,0,0,FxWorkerEvent.None,0,false,true);
   Check(a.transform.position==position&&a.transform.rotation==rotation&&FxAuthority()==authority&&a.WorkerSnapshot.EventCount==events,"Diagnostic worker mesh sweep changes no roots, jobs, stock, payment or event counts / "+a.name);
  }
  void WorkerGrip(ShopCharacterVisual a)
  {
   var box=a.WorkerProxy.Find("Empty delivery box");var filter=box.GetComponent<MeshFilter>();var bounds=filter.sharedMesh.bounds;
   foreach(var arm in new[]{a.LeftArm,a.RightArm}){var hand=arm.Find("Mitten hand");var point=box.InverseTransformPoint(hand.position);var closest=bounds.ClosestPoint(point);float separation=Vector3.Distance(box.TransformPoint(closest),hand.position);Check(separation<=.081f,"Actual mitten center remains within one mitten radius of empty proxy surface");
    var local=a.WorkerProxy.InverseTransformPoint(hand.position);Check(Mathf.Abs(Mathf.Abs(local.x)-.235f)<.085f&&Mathf.Abs(local.y)<.16f&&Mathf.Abs(local.z)<.20f,"Both real hands grip side faces through Carry/Place sweep");}
  }
  IEnumerator FxWorkerScenario()
  {
   limitations.Add("FX15 uses isolated labelled 300-credit funds and accelerated known-recipe/source preparation. Actual paid worker travel, service, incubation and saved commits use ordinary frames. Mesh sweeps are diagnostic only. Scripted camera and synthetic interaction are not native controls. Ordinary HUD videos are silent; human creative/audio/minimum-spec/GPU acceptance remains deferred.");
   Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX15 isolated save only");yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Skip isolated guidance");
   shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;
   previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
   smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
   InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionWorkerKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionWorkerMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("worker-input-provenance.json");
   if(!Application.isEditor){int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w&&Screen.height==h,"Exact worker HUD resolution");}
   StaffCreditFixture(300,"FX15 isolated paid worker construction fixture");
   yield return StaffEnter();Check(shop.HireCashier()&&shop.SetCashierScheduled(true)&&shop.SetCashierPaused(true),"Hire and pay Pip, hold work");
   yield return StockerEnter();Check(shop.HireStocker()&&shop.SetStockerScheduled(true)&&shop.SetStockerPaused(true),"Hire and pay Nori, hold work");
   yield return TechnicianEnter();Check(shop.HireTechnician()&&shop.SetTechnicianScheduled(true)&&shop.SetTechnicianPaused(true),"Hire and pay Moss, hold work");
   Check(shop.Model.Currency==288,"Three independent four-credit wages");
   yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));string rack=StockerRack();var slots=shop.Layout.GetStorageSlotIds(rack);
   SpecimenProduce("pondlet");string specimen=shop.Model.Incubator.SpecimenId;yield return SpecimenRackPanel(rack);Check(shop.StoreReadyInRack(slots[0]),"Saved labelled exact source specimen");yield return null;
   workerStarted=Time.realtimeSinceStartup;shop.SetPanel(ShopPanel.Pause);foreach(var a in new[]{shop.Cashier.Actor,shop.Stocker.Actor,shop.Technician.Actor})WorkerSweep(a);shop.SetPanel(ShopPanel.None);
   yield return WorkerNori(slots[0],specimen);
   yield return WorkerPip(specimen);
   yield return WorkerMoss(rack,slots);
   Check(shop.SaveNow()&&shop.ReloadSaved(),"Final worker save/reload");yield return null;
   foreach(var a in new[]{shop.Cashier.Actor,shop.Stocker.Actor,shop.Technician.Actor})Check(a.WorkerSnapshot.ProxyCount==0&&a.WorkerSnapshot.EventKind==FxWorkerEvent.None,"Reload has no transient parcel/event replay / "+a.name);
   File.WriteAllText(Path.Combine(evidence,"worker-motion.json"),JsonUtility.ToJson(new WorkerReceipt{context="Actual sampled role states; diagnostic complete original mesh sweeps. Accumulated observer gaps are allowed while recorder drains.",frames=workerFrames.ToArray(),meshMinimum=workerMin,meshMaximum=workerMax,geometrySamples=workerGeometry,neutralEnvelopes=workerNeutral.ToArray()},true));
   Check(workerFrames.Count>100&&workerGeometry>100,"Worker motion and geometry receipts populated");
  }
  IEnumerator WorkerNori(string slot,string specimen)
  {
   yield return StockerEnter();Check(shop.SetStockingTarget("tank-a","pondlet")&&shop.SetStockerPaused(false),"Start paid exact Nori route");StockerParkPlayer();
   yield return WorkerWait(()=>shop.Stocker.CurrentJobId!=null,8,"Nori active reservation before reload");
   string beforeReload=shop.Stocker.CurrentJobId;var oldModel=shop.Model;int beforeMoney=shop.Model.Currency;
   Check(shop.SaveNow()&&shop.ReloadSaved()&&!oldModel.GetStockTransfer(beforeReload).HasValue&&shop.Model.GetStorage(slot).Value.Id==specimen&&shop.Model.Currency==beforeMoney,"Active reservation reload releases old claim while retaining exact goods and wages");yield return null;workerPrevious.Clear();
   var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.name="FX15 diagnostic doorway barrier";barrier.transform.SetParent(shop.World.transform,false);barrier.transform.localPosition=new Vector3(0,1.2f,.8f);barrier.transform.localScale=new Vector3(3.3f,2.4f,.45f);barrier.GetComponent<Renderer>().enabled=false;Physics.SyncTransforms();
   try{StockerParkPlayer();yield return WorkerWait(()=>shop.Stocker.Blocked,35,"real worker route blocked by diagnostic barrier");WorkerAim(shop.Stocker.Actor,true);yield return WorkerClip("worker-nori-blocked-route",.7f);Check(shop.Model.GetStorage(slot).Value.Id==specimen&&!shop.Model.GetTank("tank-a").HasValue&&shop.Stocker.Actor.WorkerSnapshot.ProxyCount==0,"Blocked route releases proxy and preserves source");}finally{Destroy(barrier);}yield return null;Physics.SyncTransforms();
   yield return StockerEnter();Check(shop.RetryStocker(),"Retry physical route after diagnostic barrier removal");StockerParkPlayer();
   yield return WorkerWait(()=>shop.Stocker.WorkerActivity==FxWorkerActivity.Pickup,25,"Nori at real source");WorkerAim(shop.Stocker.Actor,true);
   bool activeCanceled=false;float cancelAge=0;int pickupEvents=0;
   yield return WorkerClip("worker-nori-pickup-focus-cancel",1.6f,age=>{var snap=shop.Stocker.Actor.WorkerSnapshot;if(!activeCanceled&&snap.EventKind==FxWorkerEvent.Pickup&&shop.World.FxPool.ParticleCountForValidation(shop.Stocker.Actor.WorkerFxOwner)>0){pickupEvents=snap.EventCount;shop.SetFxFocusForValidation(false);activeCanceled=true;cancelAge=age;Check(shop.Stocker.Actor.WorkerSnapshot.EventKind==FxWorkerEvent.None&&shop.World.FxPool.ParticleCountForValidation(shop.Stocker.Actor.WorkerFxOwner)==0,"Cancel an actually active pickup event and particles");}if(activeCanceled&&age-cancelAge>.3f)shop.SetFxFocusForValidation(true);});
   shop.SetFxFocusForValidation(true);Check(activeCanceled&&shop.Stocker.Actor.WorkerSnapshot.EventCount==pickupEvents,"Held focus interruption and recovery do not replay pickup");
   Check(shop.Stocker.Carrying&&shop.Model.GetStorage(slot).Value.Id==specimen&&!shop.Model.GetTank("tank-a").HasValue,"Carry proxy has no inventory ownership");
   string job=shop.Stocker.CurrentJobId;var actor=shop.Stocker.Actor;var position=actor.transform.position;var rotation=actor.transform.rotation;float distance=shop.Stocker.TravelDistance;
   WorkerAimHeld(actor);shop.SetPanel(ShopPanel.Pause);yield return WorkerClip("worker-nori-pause-held",.7f);
   Check(shop.Stocker.CurrentJobId==job&&shop.Stocker.TravelDistance==distance&&actor.transform.position==position&&actor.transform.rotation==rotation&&shop.Model.GetStorage(slot).Value.Id==specimen,"Held pause freezes actual job/root and keeps one safe source");
   // Keep the existing global pause while checking view policy so recorder drain cannot advance a delivery.

   shop.SetFxFocusForValidation(false);yield return WorkerClip("worker-nori-focus-held",.4f);Check(actor.WorkerSnapshot.EventKind==FxWorkerEvent.None&&shop.World.FxPool.ParticleCountForValidation(actor.WorkerFxOwner)==0,"Focus policy cancels transient accents without inventing goods");shop.SetFxFocusForValidation(true);
   shop.World.SetFxPreviewModes(true,true);yield return WorkerClip("worker-nori-reduced-held-grip",.4f);Check(actor.WorkerSnapshot.Reduced,"Actual carry uses reduced presentation policy");shop.World.SetFxPreviewModes(false,false);
   shop.SetPanel(ShopPanel.None);shop.Stocker.enabled=false;yield return null;yield return WorkerClip("worker-nori-disabled-held",.5f);
   Check(shop.Stocker.CurrentJobId==null&&!shop.Model.GetStockTransfer(job).HasValue&&!actor.gameObject.activeSelf&&actor.WorkerSnapshot.ProxyCount==0,"Disable releases claim and hides proxy without transferring source");shop.Stocker.enabled=true;yield return null;
   yield return WorkerWait(()=>shop.Stocker.Carrying,30,"Nori reacquires exact source after enable");WorkerAim(shop.Stocker.Actor,true);yield return WorkerClip("worker-nori-enabled-recovery",.4f);
   int places=shop.World.WorkerPlaceAccentCount,jobs=shop.Stocker.CompletedJobs;string inventory=StockerInventory();Check(shop.SaveNow(),"Durable baseline before deliberate Nori failure");byte[] durable=File.ReadAllBytes(shop.SavePath);TutorialBlockSave();
   try{yield return WorkerWait(()=>shop.Stocker.Blocked,35,"Nori failed saved placement");WorkerAim(actor);yield return WorkerClip("worker-nori-failed-place",.7f);Check(StockerInventory()==inventory&&shop.Stocker.CompletedJobs==jobs&&shop.World.WorkerPlaceAccentCount==places&&Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath))&&actor.WorkerSnapshot.ProxyCount==0,"Failed place has no goods/event duplication and preserves durable bytes");}finally{TutorialUnblockSave();}
   yield return StockerEnter();Check(shop.RetryStocker(),"Explicit Nori save retry");StockerParkPlayer();yield return WorkerWait(()=>shop.Stocker.WorkerActivity==FxWorkerActivity.Place,35,"Nori real placement service");job=shop.Stocker.CurrentJobId;WorkerAim(actor);
   yield return WorkerClip("worker-nori-saved-place",2.3f);
   Check(shop.Model.GetTank("tank-a").HasValue&&shop.Model.GetTank("tank-a").Value.Id==specimen&&!shop.Model.GetStorage(slot).HasValue&&shop.World.WorkerPlaceAccentCount==places+1&&shop.Stocker.CompletedJobs==jobs+1,"Saved place moves exact specimen once and emits one committed cue");Check(!shop.CompleteStockerJob(job),"Stale Nori completion rejected");
   yield return StockerEnter();Check(shop.SetStockerPaused(true),"Hold Nori before later lab output");
  }
  IEnumerator WorkerPip(string specimen)
  {
   yield return EnterCounter();Check(shop.OpenForDay(),"Open paid worker trading day");shop.SetPanel(ShopPanel.None);var visitor=Spawn(CustomerArchetypes.Casual);yield return WorkerWait(()=>visitor.Phase==CustomerPhase.Queued,30,"actual exact customer queue");
   int money=shop.Model.Currency,events=shop.World.WorkerCheckoutAccentCount;Check(visitor.SpecimenId==specimen,"Pip targets Nori's exact delivered specimen");
   yield return StaffEnter();Check(shop.SetCashierPaused(false),"Resume paid Pip");WorkerAim(shop.Cashier.Actor);yield return WorkerClip("worker-pip-service-saved-checkout",3.7f);
   Check(visitor.Outcome==CustomerOutcome.Sold&&shop.Model.Currency==money+visitor.Price&&shop.World.WorkerCheckoutAccentCount==events+1&&!shop.Model.GetTank("tank-a").HasValue&&!shop.ServeCashier(visitor),"Pip saved checkout pays exact order once, rejects replay and emits one cue");
   yield return WorkerWait(()=>shop.Customers.ActiveCount==0,15,"paid customer departure");yield return StaffEnter();Check(shop.SetCashierPaused(true),"Hold Pip for failed-save route");SeedStock("pondlet","tank-a");var failed=Spawn(CustomerArchetypes.Casual);shop.SetPanel(ShopPanel.None);yield return WorkerWait(()=>failed.Phase==CustomerPhase.Queued,30,"failed-write customer queue");
   yield return StaffEnter();Check(shop.SetCashierPaused(false)&&shop.SaveNow(),"Resume Pip with durable baseline");byte[] bytes=File.ReadAllBytes(shop.SavePath);events=shop.World.WorkerCheckoutAccentCount;int refused=shop.World.WorkerRefusalAccentCount;int gags=shop.ComedyStartedCount;int durableMoney=shop.Model.Currency;string durableSpecimen=failed.SpecimenId;TutorialBlockSave();
   try{WorkerAim(shop.Cashier.Actor);yield return WorkerClip("worker-pip-failed-save-refusal",3.6f);Check(shop.SaveError!=null&&shop.World.WorkerCheckoutAccentCount==events&&Convert.ToBase64String(bytes)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed Pip write never emits committed checkout and preserves durable bytes");Check(shop.ComedyStartedCount==gags&&shop.CelebratingWorkerId!=ShopStaff.CashierId&&shop.PendingWorkerCelebrationId!=ShopStaff.CashierId,"Failed Pip save cannot queue or start inherited success gag");int count=shop.World.WorkerRefusalAccentCount;Check(count<=refused+1,"At most one refusal cue per failed job");yield return WorkerClip("worker-pip-refusal-quiet",.7f);Check(shop.World.WorkerRefusalAccentCount==count,"Failed job cannot repeat refusal cue while quiet");}finally{TutorialUnblockSave();}
   Check(shop.ReloadSaved(),"Reload exact pre-sale durable data");yield return null;Check(shop.Customers.ActiveCount==0&&shop.Model.GetTank("tank-a").HasValue&&shop.Model.GetTank("tank-a").Value.Id==durableSpecimen&&shop.Model.Currency==durableMoney,"Reload clears visits and restores exact durable specimen/currency");yield return StaffEnter();Check(shop.SetCashierPaused(true),"Hold Pip after reload");yield return EnterCounter();Check(shop.CloseForNight(),"Close isolated worker shift");yield return WorkerWait(()=>shop.Cycle.Phase==ShopDayPhase.Night,8,"preparation night for Moss");
  }
  IEnumerator WorkerMoss(string rack,string[] slots)
  {
   yield return TechnicianEnter();TechnicianCollect("pondlet");Check(shop.QueueTechnicianRecipe("pondlet")&&shop.SetTechnicianPaused(false),"Saved Moss order with real collected ingredients");StockerParkPlayer();yield return WorkerWait(()=>shop.Technician.WorkerActivity==FxWorkerActivity.Measure||shop.Technician.WorkerActivity==FxWorkerActivity.Dose||shop.Technician.WorkerActivity==FxWorkerActivity.Start,25,"Moss real start service");string job=shop.Technician.CurrentJobId;int doses=shop.World.WorkerDoseAccentCount;WorkerAim(shop.Technician.Actor,true);
   yield return WorkerClip("worker-moss-measure-dose-start",2.3f);
   Check(shop.Model.Incubator.Phase==IncubatorPhase.Incubating&&shop.World.WorkerDoseAccentCount==doses+1&&!shop.StartTechnicianJob(job),"One saved recipe start emits one dose cue and rejects duplicate commit");string specimen=shop.Model.Incubator.SpecimenId;WorkerAim(shop.Technician.Actor,true);yield return WorkerClip("worker-moss-incubation-check",1.2f);
   yield return WorkerWait(()=>shop.Technician.WorkerActivity==FxWorkerActivity.Pickup,35,"Moss actual ready output pickup");WorkerAim(shop.Technician.Actor,true);yield return WorkerClip("worker-moss-pickup-carry",1.3f);
   Check(shop.Technician.Carrying&&shop.Model.Incubator.SpecimenId==specimen,"Moss carry proxy retains only authoritative incubator specimen");
   yield return WorkerWait(()=>shop.Technician.WorkerActivity==FxWorkerActivity.Place,25,"Moss rack placement service");int places=shop.World.WorkerPlaceAccentCount;job=shop.Technician.CurrentJobId;string target=shop.Technician.TargetStorageId;WorkerAim(shop.Technician.Actor,true);yield return WorkerClip("worker-moss-saved-rack",2.3f);
   Check(shop.Model.GetStorage(target).HasValue&&shop.Model.GetStorage(target).Value.Id==specimen&&shop.World.WorkerPlaceAccentCount==places+1&&!shop.CompleteTechnicianJob(job),"Saved rack places one exact specimen and rejects replay");
   yield return TechnicianEnter();Check(shop.SetTechnicianPaused(true)&&shop.DismissTechnician(),"Night dismissal clears Moss visual and transient state");yield return null;Check(!shop.Technician.Actor.gameObject.activeSelf&&shop.Technician.Actor.WorkerSnapshot.ProxyCount==0,"Dismissed Moss has no active carry proxy");
  }
 }
}
#endif
