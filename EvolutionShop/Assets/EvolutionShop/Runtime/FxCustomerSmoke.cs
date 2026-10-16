#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class CustomerMotionFrame
        {
            public float seconds,distance,gait,speed,blend,reactionAge,turn;
            public int sample,reactions,particles;
            public string phase,reaction;
            public Vector3 position;
            public Quaternion rotation;
            public bool paused,reduced;
        }
        [Serializable] sealed class CustomerMotionReceipt
        {
            public string archetype,visit,specimen,context;
            public float actualDistance,reportedDistance,stride,heightLimit;
            public int meshVertices,geometrySamples;
            public Vector3 minimum,maximum,neutralMinimum,neutralMaximum;
            public CustomerMotionFrame[] frames;
        }
        sealed class CustomerMesh
        {
            public Transform root;public Vector3[] vertices;
        }
        sealed class CustomerMotionTrack
        {
            public CustomerVisit visit;public CustomerActor actor;
            public CustomerMotionReceipt receipt;
            public CustomerMesh[] meshes;
            public readonly List<CustomerMotionFrame> frames=new List<CustomerMotionFrame>();
            public Vector3 previous;
            public float previousDistance,previousGait,start,nextGeometry;
            public int previousSample=-1;
        }
        CustomerMotionTrack customerMotionTrack;
        readonly Dictionary<Mesh,Vector3[]> customerParticleVertices=new Dictionary<Mesh,Vector3[]>();

        CustomerMotionTrack CustomerMotionBegin(CustomerVisit visit)
        {
            var actor=shop.Customers.ActorOf(visit);Check(actor,"Live customer actor / "+visit.ArchetypeId);
            var body=actor.transform.Find("Visitor appearance");Check(body,"Existing customer appearance root");
            var meshes=new List<CustomerMesh>();int vertices=0;
            foreach(var filter in body.GetComponentsInChildren<MeshFilter>()) {
                if(filter.transform.name.IndexOf("Slap",StringComparison.OrdinalIgnoreCase)>=0||filter.transform.parent.name.IndexOf("slap",StringComparison.OrdinalIgnoreCase)>=0)continue;
                Check(filter.sharedMesh&&filter.sharedMesh.isReadable,"Readable original customer mesh / "+filter.name);
                var v=filter.sharedMesh.vertices;meshes.Add(new CustomerMesh{root=filter.transform,vertices=v});vertices+=v.Length;
            }
            Check(vertices>100&&body.GetComponentsInChildren<Collider>().Length==0,"Original customer meshes have no gameplay colliders");
            var s=actor.MotionSnapshot;
            var track=new CustomerMotionTrack{visit=visit,actor=actor,meshes=meshes.ToArray(),start=Time.realtimeSinceStartup,previous=actor.transform.position,previousDistance=s.TravelDistance,previousGait=s.GaitPhase,
                receipt=new CustomerMotionReceipt{archetype=visit.ArchetypeId,visit=visit.Id,specimen=shop.Model.GetTank("tank-a").HasValue?shop.Model.GetTank("tank-a").Value.Id:null,stride=FxCustomerMotion.StrideLength(visit.ArchetypeId),meshVertices=vertices,
                    minimum=Vector3.one*100,maximum=Vector3.one*-100,context="Actual navigation frames and original mesh vertices; accelerated recipe fixture only. Silent scripted ordinary-HUD video is not native-input, creative, audio or GPU acceptance."}};
            actor.PreviewMotionForValidation(CustomerPhase.Arriving,0,0,0,FxCustomerReaction.None,0,0,true);
            Vector3 neutralMin=Vector3.one*100,neutralMax=Vector3.one*-100;
            foreach(var mesh in track.meshes)foreach(var vertex in mesh.vertices) {
                Vector3 point=actor.transform.InverseTransformPoint(mesh.root.TransformPoint(vertex));
                neutralMin=Vector3.Min(neutralMin,point);neutralMax=Vector3.Max(neutralMax,point);
            }
            track.receipt.neutralMinimum=neutralMin;track.receipt.neutralMaximum=neutralMax;
            // Existing premium cylinder top is (1.87+.20)*1.07=2.2149m before motion.
            // Its waist-pivot radial upper bound is ~2.2253m; 2.23 remains below
            // the caption backing bottom (2.36-.245/2=2.2375m). Other original
            // silhouettes retain the original fixture's 2.2m ceiling.
            track.receipt.heightLimit=visit.ArchetypeId==CustomerArchetypes.Premium?2.23f:2.2f;
            if(visit.ArchetypeId==CustomerArchetypes.Premium)Check(Mathf.Abs(neutralMax.y-2.2149f)<.002f,"Original premium neutral hat height preserves authored 1.07 scale");
            Check(neutralMax.y<=track.receipt.heightLimit,"Neutral authored silhouette fits independently justified motion clearance / "+visit.ArchetypeId);
            customerMotionTrack=track;return track;
        }
        void CustomerMotionGeometry(CustomerMotionTrack track)
        {
            var root=track.actor.transform;
            foreach(var mesh in track.meshes)foreach(var vertex in mesh.vertices) {
                Vector3 p=root.InverseTransformPoint(mesh.root.TransformPoint(vertex));
                if(p.y<-.001f||p.y>track.receipt.heightLimit||Mathf.Abs(p.x)>.6001f||Mathf.Abs(p.z)>.7001f)
                    throw new InvalidOperationException("FX14 original customer mesh outside floor/aisle envelope / "+track.visit.ArchetypeId+" / "+mesh.root.name+" / "+p);
                track.receipt.minimum=Vector3.Min(track.receipt.minimum,p);track.receipt.maximum=Vector3.Max(track.receipt.maximum,p);
            }
            track.receipt.geometrySamples++;
        }
        void CustomerMotionObserve()
        {
            var t=customerMotionTrack;if(t==null||!t.actor)return;
            var actor=t.actor;var s=actor.MotionSnapshot;
            if(s.SampleCount==t.previousSample)return;
            var position=actor.transform.position;
            if(actor.PathFailed||!shop.Customers.Navigation.IsInStore(position))throw new InvalidOperationException("Customer navigation regression / "+actor.LastError);
            float traveled=Flat(t.previous,position),reported=s.TravelDistance-t.previousDistance;
            if(reported<-.00001f||reported>.5f)throw new InvalidOperationException("Invalid distance sample "+reported);
            float phase=s.GaitPhase-t.previousGait;
            // Wrapped gait phases are compared modulo one complete cycle.
            float expected=reported*Mathf.PI*2/t.receipt.stride;
            if(Mathf.Abs(Mathf.DeltaAngle(phase*Mathf.Rad2Deg,expected*Mathf.Rad2Deg))>.15f)
                throw new InvalidOperationException("Stride does not match measured displacement / "+phase+" vs "+expected);
            t.receipt.actualDistance+=traveled;t.receipt.reportedDistance+=reported;
            t.previous=position;t.previousDistance=s.TravelDistance;t.previousGait=s.GaitPhase;t.previousSample=s.SampleCount;
            int particles=shop.World.FxPool.ParticleCountForValidation(actor.CustomerFxOwner);
            if(particles>6)throw new InvalidOperationException("Customer local accent exceeds six-particle bounded burst");
            for(int i=0;i<particles;i++) {
                var visual=shop.World.FxPool.ParticleVisualForValidation(actor.CustomerFxOwner,i);
                if(!visual)continue;
                var mesh=visual.GetComponent<MeshFilter>().sharedMesh;
                if(!customerParticleVertices.TryGetValue(mesh,out var vertices)){vertices=mesh.vertices;customerParticleVertices.Add(mesh,vertices);}
                foreach(var vertex in vertices) {
                    Vector3 local=actor.transform.InverseTransformPoint(visual.TransformPoint(vertex));
                    if(Mathf.Abs(local.x)>.65f||Mathf.Abs(local.z)>.7f||local.y<.4f||local.y>1.9f)
                        throw new InvalidOperationException("Customer accent mesh escaped local envelope / "+local);
                }
            }
            t.frames.Add(new CustomerMotionFrame{seconds=Time.realtimeSinceStartup-t.start,position=position,rotation=actor.transform.rotation,phase=t.visit.Phase.ToString(),distance=s.TravelDistance,gait=s.GaitPhase,speed=s.Speed,blend=s.MovingBlend,turn=s.Turn,reaction=s.Reaction.ToString(),reactionAge=s.ReactionAge,reactions=s.ReactionCount,sample=s.SampleCount,paused=s.Paused,reduced=s.Reduced,particles=particles});
            if(Time.realtimeSinceStartup>=t.nextGeometry){CustomerMotionGeometry(t);t.nextGeometry=Time.realtimeSinceStartup+.12f;}
        }
        void CustomerMotionSave(CustomerMotionTrack t,string name)
        {
            t.receipt.frames=t.frames.ToArray();File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(t.receipt,true));
            Check(t.frames.Count>20&&t.receipt.geometrySamples>10,"Actual sampled customer motion and mesh receipt / "+name);
            Check(Mathf.Abs(t.receipt.actualDistance-t.receipt.reportedDistance)<.18f,"Independent root displacement agrees with measured gait travel / "+name);
            customerMotionTrack=null;
        }
        IEnumerator CustomerMotionWait(Func<bool> condition,float seconds,string reason)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition()) {CustomerMotionObserve();if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("FX14 waiting for "+reason+" / "+shop.Customers.Status);yield return null;}
            CustomerMotionObserve();Check(true,"Actual customer transition / "+reason);
        }
        IEnumerator CustomerMotionClip(string name,float duration,Action<float> action=null)
        {
            Action<float> tick=age=>{action?.Invoke(age);CustomerMotionObserve();};
            if(Application.isEditor){float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<duration){tick(Time.realtimeSinceStartup-start);yield return null;}}
            else yield return ReviewClip(name,duration,tick);
        }
        void CustomerMotionSweep(CustomerMotionTrack t)
        {
            var a=t.actor;var root=a.transform;var agent=a.GetComponent<NavMeshAgent>();
            Vector3 position=root.position,destination=agent.destination;Quaternion rotation=root.rotation;var status=agent.pathStatus;bool stopped=agent.isStopped;
            string authority=FxAuthority();int count=a.MotionSnapshot.ReactionCount;
            foreach(var phase in new[]{CustomerPhase.Arriving,CustomerPhase.Browsing,CustomerPhase.ToCounter,CustomerPhase.Queued,CustomerPhase.Paid,CustomerPhase.Leaving})
                for(int i=0;i<16;i++) {
                    a.PreviewMotionForValidation(phase,i*Mathf.PI/8,i%2==0?1:0,i*.4f,FxCustomerReaction.None,0,i%2==0?-1:1,false);CustomerMotionGeometry(t);
                }
            foreach(var reaction in new[]{FxCustomerReaction.Accepted,FxCustomerReaction.Refused,FxCustomerReaction.Checkout})
                for(int i=0;i<=24;i++) {
                    a.PreviewMotionForValidation(CustomerPhase.Queued,0,0,.8f,reaction,FxCustomerMotion.ReactionDuration(reaction)*i/24,.5f,false);CustomerMotionGeometry(t);
                }
            a.PreviewMotionForValidation(CustomerPhase.Queued,1,0,8,FxCustomerReaction.Checkout,.3f,1,true);CustomerMotionGeometry(t);
            a.PreviewMotionForValidation(t.visit.Phase,a.MotionSnapshot.GaitPhase,a.MotionSnapshot.MovingBlend,0,FxCustomerReaction.None,0,0,false);
            Check(root.position==position&&root.rotation==rotation&&agent.destination==destination&&agent.pathStatus==status&&agent.isStopped==stopped&&FxAuthority()==authority&&a.MotionSnapshot.ReactionCount==count,
                "Complete mesh pose sweep preserves NavMesh root/path and exact authority / "+t.visit.ArchetypeId);
            for(int i=0;i<64;i++)a.PreviewMotionForValidation(CustomerPhase.Arriving,i*.1f,1,0,FxCustomerReaction.None,0,0,false);
            long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<256;i++)a.PreviewMotionForValidation(CustomerPhase.Arriving,i*.1f,1,0,FxCustomerReaction.None,0,0,false);
            long allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
            Check(allocated==0,"Warmed direct customer pose scope allocates zero managed bytes / "+t.visit.ArchetypeId+" / "+allocated);
            a.PreviewMotionForValidation(t.visit.Phase,a.MotionSnapshot.GaitPhase,0,0,FxCustomerReaction.None,0,0,false);
        }
        void CustomerMotionAim(CustomerVisit visit,bool queue=false)
        {
            Vector3 target=queue?shop.Customers.Navigation.QueueSpot(0):shop.Customers.Navigation.BrowseSpot("tank-a",0);
            AimAt(queue?new Vector3(-1.1f,.2f,-4.7f):new Vector3(.5f,.2f,-3.8f),target+Vector3.up*1.2f);
        }
        IEnumerator CustomerMotionPause(CustomerMotionTrack t)
        {
            var a=t.actor;shop.SetPanel(ShopPanel.Pause);yield return null;
            Vector3 position=a.transform.position;Quaternion rotation=a.transform.rotation;float gait=a.MotionSnapshot.GaitPhase,clock=shop.Cycle.SecondsRemaining;
            var agent=a.GetComponent<NavMeshAgent>();Vector3 destination=agent.destination;
            yield return CustomerMotionClip("customer-pause-stationary-live",.8f);
            Check(a.transform.position==position&&a.transform.rotation==rotation&&a.MotionSnapshot.GaitPhase==gait&&shop.Cycle.SecondsRemaining==clock&&agent.destination==destination,"Actual pause freezes root, gait and domain clock while retaining destination");
            Check(a.MotionSnapshot.Reaction==FxCustomerReaction.None,"Pause cancels local reaction");shop.SetPanel(ShopPanel.None);yield return null;
        }
        IEnumerator CustomerMotionRoute(string type,bool lifecycle)
        {
            GateFresh();shop.SetFxFocusForValidation(true);shop.AllowValidationInput=false;
            string species="pondlet";
            SeedStock(species,"tank-a");var definition=CustomerArchetypes.Find(shop.Customers.Archetypes,type);
            Check(shop.Customers.Preview(definition,shop.Model.GetPrice(species).Value)!=PriceReaction.TooExpensive,"Actual species/tag is affordable for "+type);
            yield return EnterCounter();Check(shop.OpenForDay(),"Open actual customer route / "+type);shop.SetPanel(ShopPanel.None);
            var visit=Spawn(type);shop.SetPanel(ShopPanel.Pause);yield return null;
            var t=CustomerMotionBegin(visit);CustomerMotionSweep(t);yield return null;
            shop.SetPanel(ShopPanel.None);yield return null;
            CustomerMotionAim(visit);yield return CustomerMotionClip("customer-"+type+"-arrival-walk",2);
            if(lifecycle)yield return CustomerMotionPause(t);
            yield return CustomerMotionWait(()=>visit.Phase==CustomerPhase.Browsing,20,type+" browsing");
            Check(t.receipt.actualDistance>1,"Customer actually navigated from entrance / "+type);
            yield return CustomerMotionClip("customer-"+type+"-browse-price",definition.BrowseSeconds+.65f);
            Check(visit.Phase==CustomerPhase.ToCounter||visit.Phase==CustomerPhase.Queued,"Accepted price leads to counter / "+type);
            Check(t.actor.MotionSnapshot.ReactionCount>=1,"Local accepted-price event observed / "+type);
            CustomerMotionAim(visit,true);yield return CustomerMotionWait(()=>visit.Phase==CustomerPhase.Queued,20,type+" queued");
            string specimen=visit.SpecimenId;int money=shop.Model.Currency;
            Check(shop.Model.GetReservation("tank-a").Value.CustomerId==visit.Id&&shop.Model.GetTank("tank-a").Value.Id==specimen,"Queue retains exact unpaid reservation / "+type);
            yield return CustomerMotionClip("customer-"+type+"-quiet-queue",2);
            int reactions=t.actor.MotionSnapshot.ReactionCount;float gait=t.actor.MotionSnapshot.GaitPhase;Vector3 position=t.actor.transform.position;
            yield return CustomerMotionClip("customer-"+type+"-queue-rest",1.2f);
            Check(t.actor.MotionSnapshot.ReactionCount==reactions&&Mathf.Abs(t.actor.MotionSnapshot.GaitPhase-gait)<.0001f&&Flat(position,t.actor.transform.position)<.001f&&shop.World.FxPool.ParticleCountForValidation(t.actor.CustomerFxOwner)==0,"Queue does not repeat reaction, bright particles or stationary stride / "+type);
            if(lifecycle) {
                shop.World.SetFxPreviewModes(true,true);yield return CustomerMotionClip("customer-reduced-queue-live",1.2f);Check(t.actor.MotionSnapshot.Reduced,"Actual actor consumes reduced policy");shop.World.SetFxPreviewModes(false,false);
            }
            yield return EnterCounter();bool served=false;
            yield return CustomerMotionClip("customer-"+type+"-saved-checkout",2.2f,age=>{if(!served&&age>=.15f){served=true;Check(shop.ServeCustomer()&&shop.SaveError==null,"Actual durable checkout / "+type);}});
            Check(visit.Outcome==CustomerOutcome.Sold&&shop.Model.Currency==money+visit.Price&&!shop.Model.GetTank("tank-a").HasValue,"Checkout pays agreed price once and consumes exact stock / "+type);
            Check(t.actor&&t.actor.MotionSnapshot.ReactionCount==reactions+1,"Exactly one saved checkout accent / "+type);
            int paidCount=t.actor.MotionSnapshot.ReactionCount;
            Check(!shop.Customers.ServeExpected(visit).HasValue&&shop.Model.Currency==money+visit.Price&&t.actor.MotionSnapshot.ReactionCount==paidCount,"Duplicate stale checkout cannot pay or celebrate again / "+type);
            shop.SetPanel(ShopPanel.None);CustomerMotionAim(visit,true);yield return CustomerMotionClip("customer-"+type+"-leave",2.5f);
            yield return CustomerMotionWait(()=>shop.Customers.ActiveCount==0,20,type+" departure");CustomerMotionSave(t,"customer-"+type);
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload exact completed sale / "+type);shop.SetPanel(ShopPanel.None);yield return null;
            Check(shop.Model.Currency==money+visit.Price&&!shop.Model.GetTank("tank-a").HasValue&&shop.Customers.ActiveCount==0,"Reload cannot resurrect sold specimen or customer accent / "+type);
        }
        IEnumerator CustomerMotionActiveInterruptions()
        {
            foreach(string mode in new[]{"pause","journal","arrange","focus","disable","reset","reload"}) {
                GateFresh();shop.SetFxFocusForValidation(true);SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Open active interruption / "+mode);shop.SetPanel(ShopPanel.None);
                var visit=Spawn(CustomerArchetypes.Casual);yield return null;
                var t=CustomerMotionBegin(visit);CustomerMotionAim(visit);
                yield return CustomerMotionWait(()=>t.actor.MotionSnapshot.Reaction==FxCustomerReaction.Accepted,30,"real accepted-price accent before "+mode);
                var actor=t.actor;var owner=actor.CustomerFxOwner;int count=actor.MotionSnapshot.ReactionCount;string specimen=visit.SpecimenId;
                Check(shop.World.FxPool.ParticleCountForValidation(owner)>0&&count==1&&shop.Model.GetReservation("tank-a").Value.CustomerId==visit.Id,"Nonzero actual actor and particle state before interruption / "+mode);
                if(mode=="pause")shop.SetPanel(ShopPanel.Pause);
                else if(mode=="journal")shop.SetPanel(ShopPanel.Journal);
                else if(mode=="arrange")shop.SetPanel(ShopPanel.Arrange);
                else if(mode=="focus")shop.SetFxFocusForValidation(false);
                else if(mode=="disable")actor.enabled=false;
                else if(mode=="reset")shop.Customers.ResetVisitors();
                else {Check(shop.SaveNow(),"Save unpaid active order before reload");Check(shop.ReloadSaved(),"Reload active unpaid order");}
                yield return null;
                Check(shop.World.FxPool.ParticleCountForValidation(owner)==0&&(!actor||actor.MotionSnapshot.Reaction==FxCustomerReaction.None),"Active local accent canceled on "+mode);
                Check(shop.Model.Currency==0&&shop.Model.GetTank("tank-a").HasValue&&shop.Model.GetTank("tank-a").Value.Id==specimen,"Interruption retains exact unpaid specimen / "+mode);
                if(mode=="disable")actor.enabled=true;
                shop.SetFxFocusForValidation(true);shop.SetPanel(ShopPanel.None);
                yield return CustomerMotionClip("customer-active-cancel-"+mode,.45f);
                Check(shop.World.FxPool.ParticleCountForValidation(owner)==0&&(!actor||actor.MotionSnapshot.ReactionCount==count),"Resume cannot replay canceled accent / "+mode);
                if(mode=="disable") {
                    Check(shop.World.FxCues.IsValid(actor.CustomerFxOwner),"Re-enabled customer has a valid rebound owner");
                    yield return CustomerMotionWait(()=>visit.Phase==CustomerPhase.Queued,20,"re-enabled customer reaches counter");
                    yield return EnterCounter();Check(shop.ServeCustomer()&&shop.SaveError==null&&actor.MotionSnapshot.ReactionCount==count+1&&shop.World.FxPool.ParticleCountForValidation(actor.CustomerFxOwner)>0,"Rebound customer receives exactly one later real saved checkout accent");
                }
                if(mode=="reset"||mode=="reload")Check(shop.Customers.ActiveCount==0&&!shop.Model.GetReservation("tank-a").HasValue,"Cleanup releases unpaid reservation once / "+mode);
                CustomerMotionSave(t,"customer-active-cancel-"+mode);
            }
        }
        IEnumerator CustomerMotionRefusalAndSaveFailure()
        {
            GateFresh();SeedStock("pondlet","tank-a");
            var tag=shop.Model.GetPrice("pondlet").Value;var bargain=CustomerArchetypes.Find(shop.Customers.Archetypes,CustomerArchetypes.Bargain);
            Check(tag.Maximum>bargain.Budget,"Legal tag range contains a genuine bargain-hunter budget refusal");
            Check(shop.SetSpeciesPrice("pondlet",tag.Maximum).Success&&shop.SaveError==null,"Actual saved legal over-budget price tag");yield return EnterCounter();Check(shop.OpenForDay(),"Open refusal route");shop.SetPanel(ShopPanel.None);
            var visit=Spawn(CustomerArchetypes.Bargain);yield return null;
            var t=CustomerMotionBegin(visit);CustomerMotionAim(visit);
            yield return CustomerMotionWait(()=>visit.Phase==CustomerPhase.Browsing,25,"bargain refusal browsing");
            yield return CustomerMotionClip("customer-bargain-price-refusal",3.3f);
            Check(visit.Outcome==CustomerOutcome.TooExpensive&&shop.Model.Currency==0&&shop.Model.GetTank("tank-a").HasValue&&!shop.Model.GetReservation("tank-a").HasValue,"Price refusal retains unpaid exact stock and no reservation");
            Check(t.actor.MotionSnapshot.ReactionCount==1,"Single bounded price refusal accent");
            yield return CustomerMotionWait(()=>shop.Customers.ActiveCount==0,20,"refusing visitor departure");CustomerMotionSave(t,"customer-refusal");
            GateFresh();SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Open failed-write route");shop.SetPanel(ShopPanel.None);
            visit=Spawn(CustomerArchetypes.Casual);yield return null;
            t=CustomerMotionBegin(visit);yield return CustomerMotionWait(()=>visit.Phase==CustomerPhase.Queued,35,"failed-write queued visitor");
            yield return EnterCounter();Check(shop.SaveNow(),"Exact pre-checkout durable baseline");byte[] durable=File.ReadAllBytes(shop.SavePath);string specimen=visit.SpecimenId;int money=shop.Model.Currency,reactions=t.actor.MotionSnapshot.ReactionCount;
            TutorialBlockSave();
            try {
                shop.ServeCustomer();
                Check(shop.SaveError!=null&&t.actor.MotionSnapshot.Reaction!=FxCustomerReaction.Checkout&&t.actor.MotionSnapshot.ReactionCount==reactions,"Failed durable checkout never celebrates transient in-memory sale");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed checkout preserves exact durable bytes");
                yield return CustomerMotionClip("customer-failed-save-no-checkout",.7f);
            }finally{TutorialUnblockSave();}
            CustomerMotionSave(t,"customer-failed-save");Check(shop.ReloadSaved(),"Reload durable pre-sale after deliberate failure");shop.SetPanel(ShopPanel.None);yield return null;
            Check(shop.Model.Currency==money&&shop.Model.GetTank("tank-a").HasValue&&shop.Model.GetTank("tank-a").Value.Id==specimen&&!shop.Model.GetReservation("tank-a").HasValue&&shop.Customers.ActiveCount==0,"Failed-sale reload restores exact money/stock and clears transient reservations/actors");
            Check(!shop.Customers.ServeExpected(visit).HasValue&&shop.Model.Currency==money,"Stale pre-reload order cannot pay");
        }
        IEnumerator FxCustomerScenario()
        {
            limitations.Add("FX14 uses actual four-type visits and normal navigation/browse/queue/payment clocks. Stock alone is seeded via accelerated existing recipe commands. Scripted cameras and synthetic input are not native controls. Windows clips contain ordinary HUD and are silent. Mesh sweeps are explicit diagnostic poses preserving root/path/domain state. No human creative/audio/minimum-spec/GPU acceptance.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX14 isolated save only");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionCustomerMotionKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionCustomerMotionMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("customer-input-provenance.json");
            if(!Application.isEditor){int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==width&&Screen.height==height,"Exact customer HUD resolution");}
            Check(shop.Customers.MaxVisitors==2&&shop.Customers.Archetypes.Count==4,"Four existing customer types and unchanged two-place capacity");
            foreach(string type in new[]{"casual","bargain","collector","premium"})yield return CustomerMotionRoute(type,type=="casual");
            yield return CustomerMotionActiveInterruptions();
            yield return CustomerMotionRefusalAndSaveFailure();
            Check(shop.World.FxPool.OwnedCount==96&&shop.World.FxPool.OwnedMeshCount==5&&shop.World.FxPool.OwnedMaterialCount==1&&shop.GetComponents<AudioSource>().Length==3,"Retained bounded pool and audio ownership");
            Check(shop.SaveNow(),"Final isolated customer save");File.Copy(shop.SavePath,Path.Combine(evidence,"customer-final-save.json"),true);shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
