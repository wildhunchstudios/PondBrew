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
  [Serializable] sealed class ComedyFrame {public string clip,kind,caption;public int renderFrame,beatFrame,particles,emissions;public uint sequence;public float realtime,elapsed,beatRealtime;public bool active,reduced,beat,hand,whiff;public Vector3 source;}
  [Serializable] sealed class ComedyReceipt {public string clock="Trace sampled at EndOfFrame immediately before recorder.Sample; absolute realtime and renderFrame are shared with recorder receipt. Editor observations are diagnostic only.";public ComedyFrame[] frames;public int geometrySamples;}
  readonly List<ComedyFrame> comedyFrames=new List<ComedyFrame>();readonly Dictionary<uint,int> comedyBursts=new Dictionary<uint,int>();int comedyGeometry;
  void FxComedyObserve(string clip)
  {
   var s=shop.World.ComedyFxSnapshot;int particles=shop.World.FxPool.ParticleCountForValidation(shop.World.ComedyFxOwner);
   if(particles>6||particles<0||!CustomerMotionFinite(s.Elapsed)||!CustomerMotionFinite(s.SourcePosition.sqrMagnitude))throw new InvalidOperationException("Invalid comedy pose/event budget");
   if(s.BeatEmitted){if(comedyBursts.TryGetValue(s.Sequence,out int count)&&count!=s.EmissionCount)throw new InvalidOperationException("Repeated comedy emission for same sequence");comedyBursts[s.Sequence]=s.EmissionCount;}
   comedyFrames.Add(new ComedyFrame{clip=clip,renderFrame=Time.frameCount,realtime=Time.realtimeSinceStartup,kind=s.Kind.ToString(),sequence=s.Sequence,elapsed=s.Elapsed,beatFrame=s.BeatFrameCount,beatRealtime=s.BeatRealtime,particles=particles,emissions=s.EmissionCount,active=s.Active,reduced=s.Reduced,beat=s.BeatEmitted,source=s.SourcePosition,caption=shop.Comedy.Caption,hand=shop.SlapGesture&&shop.SlapGesture.IsPlaying,whiff=shop.SlapGesture&&shop.SlapGesture.IsWhiff});
  }
  IEnumerator FxComedyClip(string name,float seconds,Action<float> tick=null)
  {
   if(Application.isEditor){float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<seconds){tick?.Invoke(Time.realtimeSinceStartup-start);yield return null;FxComedyObserve(name);}yield break;}
   string path=Path.Combine(evidence,name+".mp4");using(var recorder=new FxReviewRecorder(ShopSession.Arg("-evolutionFxReviewEncoder"),path)){
    while(recorder.Age<seconds){tick?.Invoke(recorder.Age);yield return new WaitForEndOfFrame();FxComedyObserve(name);recorder.Sample();if(recorder.Failure!=null)throw new InvalidOperationException(recorder.Failure);}
    float deadline=Time.realtimeSinceStartup+3;while(recorder.Pending>0){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Comedy capture drain");yield return null;}
    var receipt=recorder.Finish();File.WriteAllText(Path.Combine(evidence,name+"-frames.json"),JsonUtility.ToJson(receipt,true));Check(receipt.frames.Length>=seconds*25&&receipt.achievedFPS>=25&&receipt.missedSamples<=seconds*5,"Comedy actual HUD video and shared render clock / "+name);
   }
  }
  void FxComedyView(CustomerActor actor){shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;var p=actor.transform.position;GateAim(p+new Vector3(1.7f,.2f,-2.2f),p+Vector3.up*1.1f);}
  void FxComedyQuiet(){shop.SkipComedy();shop.Comedy.Tick(120);}
  IEnumerator FxComedyStart(ComedyKind kind){shop.SetPanel(ShopPanel.None);FxComedyQuiet();yield return new WaitForSeconds(.85f);Check(shop.TryStartComedy(kind),"Actual eligible FX16 incident / "+kind);}
  void FxComedyClean(string label){Check(!shop.World.ComedyFxSnapshot.Active&&shop.World.FxPool.ParticleCountForValidation(shop.World.ComedyFxOwner)==0,"No stale comedy cue / "+label);}
  IEnumerator FxComedyScenario()
  {
   limitations.Add("FX16 isolated save; explicitly prepared stock,300-credit staffing and unrecorded cosmetic cooldown expiry. All recorded action/recovery uses ordinary frames. Synthetic InputSystem remap/slap/skip, scripted clear camera lanes, real eligible visitors and saved worker job. Silent Windows footage and numerical audio checks do not constitute listening/native input/human creative/GPU acceptance. Shared absolute capture clock enables event-peak extraction.");
   yield return new WaitForSeconds(1);Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX16 isolated save only");
   previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
   smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
   InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("FX16 comedy keyboard");cycleMouse=InputSystem.AddDevice<Mouse>("FX16 comedy mouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("comedy-input-provenance.json");
   GateFresh();comedyBursts.Clear();SetReputationFixture(5);SeedStock("pondlet","tank-a");SeedStock("bubblebelly","tank-b");yield return EnterCounter();Check(shop.OpenForDay(),"FX16 real trading day");shop.SetPanel(ShopPanel.None);var visit=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>visit.Phase==CustomerPhase.Queued,35,"FX16 eligible queued visitor");var actor=shop.Customers.ActorOf(visit);yield return null;
   FxComedySweep(actor);FxComedyView(actor);
   foreach(var kind in new[]{ComedyKind.Fart,ComedyKind.Complaint,ComedyKind.Mimic}){
    FxComedyView(actor);string stock=GateStock();yield return FxComedyStart(kind);if(kind==ComedyKind.Mimic)GateAim(new Vector3(2.5f,.2f,-3),StationOf(StationKind.TankA).transform.position+Vector3.up*1.4f);string setup=shop.Comedy.Caption;bool payoff=false;int before=shop.World.ComedyFxSnapshot.EmissionCount;
    yield return FxComedyClip("comedy-"+kind.ToString().ToLowerInvariant()+"-live",3.0f,age=>{if(shop.Comedy.Active&&shop.Comedy.Caption!=setup)payoff=true;});
    Check(payoff&&GateStock()==stock,"Setup/reaction and exact order conservation / "+kind);Check(shop.World.ComedyFxSnapshot.EmissionCount==before+1,"One local beat for actual incident / "+kind);FxComedyClean(kind.ToString());
   }
   yield return FxComedyInterruptions(actor);
   // Transaction priority: an ordinary saved sale interrupts the active customer gag.
   FxComedyView(actor);yield return FxComedyStart(ComedyKind.Complaint);yield return new WaitForSeconds(.65f);int money=shop.Model.Currency;int price=visit.Price;
   yield return ServeNext("FX16 actual service preempts complaint");Check(visit.Outcome==CustomerOutcome.Sold&&shop.Model.Currency==money+price&&!shop.Comedy.Active,"Service pays once and cancels incidental presentation");shop.SetPanel(ShopPanel.None);yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"sold visitor departure");
   yield return FxComedySlap();yield return FxComedyBelch();yield return FxComedyWorker();
   shop.SetPanel(ShopPanel.Pause);shop.ReducedGags=true;shop.HumorVolume=0;Check(shop.SetSlapBinding("R")&&shop.SaveNow()&&shop.ReloadSaved(),"FX16 saved humor/remap preferences reload");yield return null;Check(shop.ReducedGags&&shop.HumorVolume==0&&shop.SlapKey=="R"&&!shop.Comedy.Active,"Reload preserves preferences without replay");FxComedyClean("final reload");
   Check(comedyFrames.Count>100&&comedyGeometry>100,"FX16 actual timed trace and original-mesh sweeps populated");File.WriteAllText(Path.Combine(evidence,"comedy-motion.json"),JsonUtility.ToJson(new ComedyReceipt{frames=comedyFrames.ToArray(),geometrySamples=comedyGeometry},true));
  }
  IEnumerator FxComedyInterruptions(CustomerActor actor)
  {
   foreach(string mode in new[]{"skip","pause","journal","focus","actor-disable","session-disable"}){
    FxComedyView(actor);yield return FxComedyStart(ComedyKind.Fart);yield return WaitFor(()=>shop.World.ComedyFxSnapshot.BeatEmitted&&shop.World.FxPool.ParticleCountForValidation(shop.World.ComedyFxOwner)>0,2,"active FX16 beat before "+mode);
    string stock=GateStock();int emissions=shop.World.ComedyFxSnapshot.EmissionCount;bool done=false;
    yield return FxComedyClip("comedy-cancel-"+mode,.7f,age=>{if(done||age<.04f)return;done=true;
     if(mode=="skip")shop.SkipComedy();else if(mode=="pause")shop.SetPanel(ShopPanel.Pause);else if(mode=="journal")shop.SetPanel(ShopPanel.Journal);else if(mode=="focus")shop.SetFxFocusForValidation(false);else if(mode=="actor-disable")actor.enabled=false;else if(mode=="session-disable")shop.enabled=false;else {Check(shop.SaveNow()&&shop.ReloadSaved(),"Active comedy saved/reloaded");}
    });
    Check(done&&shop.World.ComedyFxSnapshot.EmissionCount==emissions,"Canceled beat cannot replay / "+mode);FxComedyClean(mode);
    if(mode!="reload")Check(GateStock()==stock,"Interruption preserves exact unpaid order / "+mode);
    if(mode=="actor-disable")actor.enabled=true;if(mode=="session-disable")shop.enabled=true;if(mode=="focus")shop.SetFxFocusForValidation(true);shop.SetPanel(ShopPanel.None);yield return null;
    if(mode=="reload")break;
    Check(!shop.TryStartComedy(ComedyKind.Fart),"Interruption retains cosmetic quiet time / "+mode);
   }
  }
  IEnumerator FxComedySlap()
  {
   GateFresh();comedyBursts.Clear();SetReputationFixture(5);SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Slap trading fixture");shop.SetPanel(ShopPanel.None);var visit=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>visit.Phase==CustomerPhase.Queued,35,"slap actual queue");
   shop.SetPanel(ShopPanel.Pause);shop.AllowValidationInput=true;shop.BeginSlapRebind();yield return MoveFor(Key.E,.04f);Check(shop.WaitingForSlapKey,"Reserved service key cannot bind slap");yield return MoveFor(Key.R,.04f);Check(shop.SlapKey=="R"&&!shop.WaitingForSlapKey,"Actual remap edge binds without striking");
   yield return AimForSlap(visit);yield return MoveFor(Key.F,.04f);Check(visit.Outcome==CustomerOutcome.None,"Old slap key no longer strikes");
   int contacts=shop.SlapGesture.ContactCount;int money=shop.Model.Currency,slaps=shop.Reports.Current.slapped;string specimen=visit.SpecimenId,tank=visit.TargetTankId;bool pressed=false,released=false,earlyWhiff=false;var aim=shop.Player.View.transform.rotation;
   yield return FxComedyClip("comedy-slap-contact-recovery",1.2f,age=>{if(!earlyWhiff&&age>.02f){earlyWhiff=true;shop.Player.View.transform.rotation=Quaternion.LookRotation(Vector3.up);Check(!shop.TrySlapFocusedCustomer()&&shop.SlapGesture.IsWhiff,"Real invalid aim starts whiff before hit");shop.Player.View.transform.rotation=aim;}if(!pressed&&age>.08f){pressed=true;shop.AllowValidationInput=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.R));}if(!released&&age>.14f){released=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());}});
   Check(visit.Outcome==CustomerOutcome.Slapped&&shop.Reports.Current.slapped==slaps+1&&shop.Model.Currency==money&&shop.Model.GetTank(tank).Value.Id==specimen,"Slap has exact established penalty and no payment or lost stock");Check(shop.SlapGesture.ContactCount==contacts+1,"One contact-aligned existing slap sound event");Check(!shop.TrySlapFocusedCustomer(),"Accepted slap cooldown rejects repeat");yield return WaitFor(()=>shop.Customers.ActiveCount==0&&shop.SlapCooldownRemaining==0,15,"slap departure and real cooldown");
   GateAim(new Vector3(0,.2f,-3),new Vector3(0,1.4f,-6));int emissions=shop.World.ComedyFxSnapshot.EmissionCount;slaps=shop.Reports.Current.slapped;bool whiff=false;
   yield return FxComedyClip("comedy-slap-whiff-recovery",.8f,age=>{if(!whiff&&age>.08f){whiff=true;Check(!shop.TrySlapFocusedCustomer()&&shop.SlapGesture.IsWhiff,"Invalid target produces presentation-only whiff");}});
   Check(shop.Reports.Current.slapped==slaps&&shop.SlapCooldownRemaining==0&&shop.World.ComedyFxSnapshot.EmissionCount==emissions&&shop.SlapGesture.ContactCount==contacts+1&&!shop.SlapGesture.IsPlaying,"Whiff has no contact burst, penalty, cooldown or stale glove");
   shop.ReducedGags=true;var cancelVisit=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>cancelVisit.Phase==CustomerPhase.Queued,35,"precontact cancellation actual visitor");yield return AimForSlap(cancelVisit);int beforeContact=shop.SlapGesture.ContactCount,beforeSound=shop.GetComponent<ShopAudio>().SlapPlaybackCount;bool canceled=false;
   yield return FxComedyClip("comedy-slap-precontact-cancel",.6f,age=>{if(canceled||age<.08f)return;canceled=true;Check(shop.TrySlapFocusedCustomer()&&cancelVisit.Outcome==CustomerOutcome.Slapped,"Precontact canceled slap retains immediate accepted outcome");shop.SkipComedy();});
   Check(canceled&&shop.SlapGesture.ContactCount==beforeContact&&shop.GetComponent<ShopAudio>().SlapPlaybackCount==beforeSound&&!shop.SlapGesture.IsPlaying,"Cancel before contact suppresses existing sound and glove without rollback");shop.ReducedGags=false;

  }
  IEnumerator FxComedyBelch()
  {
   GateFresh();comedyBursts.Clear();GateMature();shop.SetPanel(ShopPanel.Journal);shop.SetPanel(ShopPanel.None);GateAim(new Vector3(-1.7f,.2f,2.5f),new Vector3(2,1.4f,3.85f));
   foreach(bool reduced in new[]{false,true}){shop.ReducedGags=reduced;yield return FxComedyStart(ComedyKind.Belch);string specimen=shop.Model.Incubator.SpecimenId;yield return FxComedyClip(reduced?"comedy-belch-reduced":"comedy-belch-live",3);Check(shop.Model.Incubator.SpecimenId==specimen&&shop.Model.Incubator.Phase==IncubatorPhase.Ready,"Belch preserves ready specimen / reduced="+reduced);FxComedyClean("belch recovery");}yield return FxComedyStart(ComedyKind.Belch);yield return new WaitForSeconds(.65f);Check(shop.SaveNow()&&shop.ReloadSaved(),"Mid-action belch reload cancels presentation");yield return null;FxComedyClean("active reload");shop.ReducedGags=false;
  }
  IEnumerator FxComedyWorker()
  {
   GateFresh();comedyBursts.Clear();StaffCreditFixture(300,"FX16 isolated cashier comedy wage fixture");yield return StaffEnter();Check(shop.HireCashier()&&shop.SetCashierScheduled(true)&&shop.SetCashierPaused(true),"Real paid Pip fixture");SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Worker comedy trading day");shop.SetPanel(ShopPanel.None);var visit=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>visit.Phase==CustomerPhase.Queued,35,"worker comedy actual order");yield return StaffEnter();Check(shop.SetCashierPaused(false),"Resume real cashier service");shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;GateAim(new Vector3(-4,.2f,-4.5f),shop.Cashier.Actor.transform.position+Vector3.up*1.1f);FxComedyQuiet();int cash=shop.Model.Currency;bool celebrated=false;
   yield return FxComedyClip("comedy-worker-saved-completion",5.5f,age=>{if(shop.Comedy.Active&&shop.Comedy.Kind==ComedyKind.WorkerCelebration)celebrated=true;});
   Check(visit.Outcome==CustomerOutcome.Sold&&shop.Model.Currency==cash+visit.Price&&celebrated,"Saved real cashier job triggers existing worker incident once");Check(!shop.ServeCashier(visit),"Comedy cannot repay completed order");FxComedyClean("worker recovery");
  }
  void FxComedySweep(CustomerActor actor)
  {
   var root=actor.transform.position;var rotation=actor.transform.rotation;string authority=GateStock();
   foreach(bool reduced in new[]{false,true})foreach(ComedyKind kind in Enum.GetValues(typeof(ComedyKind)))for(int i=0;i<=12;i++){
    actor.PreviewComedyForValidation(kind,i*.25f,reduced,i%2);
    foreach(var filter in actor.GetComponentsInChildren<MeshFilter>()){
     if(!filter.sharedMesh||!filter.gameObject.activeInHierarchy)continue;bool label=false;for(var p=filter.transform;p&&p!=actor.transform;p=p.parent)if(p.name.IndexOf("caption",StringComparison.OrdinalIgnoreCase)>=0||p.name.IndexOf("sign",StringComparison.OrdinalIgnoreCase)>=0)label=true;if(label)continue;
     foreach(var v in filter.sharedMesh.vertices){var local=actor.transform.InverseTransformPoint(filter.transform.TransformPoint(v));if(local.y<-.001f||Mathf.Abs(local.x)>.85f||Mathf.Abs(local.z)>.85f)throw new InvalidOperationException("Comedy mesh exceeds floor/local aisle envelope / "+filter.name+" / "+local);}
    }comedyGeometry++;
   }
   actor.PreviewComedyForValidation(ComedyKind.Fart,5,false,0);Check(actor.transform.position==root&&actor.transform.rotation==rotation&&GateStock()==authority,"Comedy original-mesh sweep preserves roots and exact authority");
  }
 }
}
#endif
