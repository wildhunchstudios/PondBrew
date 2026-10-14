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
        [Serializable] sealed class AquariumFrame
        {
            public float seconds,fishAge,placementAge;
            public bool occupied,reserved,trail;
            public int particles,habitatBubbles,placementEmissions,trailEmissions;
        }
        [Serializable] sealed class AquariumClipReceipt
        {
            public string source="Actual live Unity HUD; synthetic operator view and real simulation. Comfort policies are diagnostic only.";
            public string name;public int peak,meshSamples;public AquariumFrame[] frames;
        }
        [Serializable] sealed class AquariumPlantRest
        {public Vector3 position,scale;public Quaternion rotation;public float minY,rotationDisplacement;}
        readonly Dictionary<Transform,AquariumPlantRest> aquariumPlants=new Dictionary<Transform,AquariumPlantRest>();
        void AquariumCachePlantedGeometry()
        {
            // The inherited capsule roots are deliberately buried. Derive their allowance
            // from actual rest vertices and the maximum seven-degree habitat lean.
            shop.World.ResetFxPresentation();aquariumPlants.Clear();var tank=StationOf(StationKind.TankA).transform;
            foreach(Transform child in tank)if(child.name=="Aquatic frond")
            {
                var rest=new AquariumPlantRest{position=child.localPosition,scale=child.localScale,rotation=child.localRotation,minY=float.PositiveInfinity};float radius=0;
                foreach(var vertex in child.GetComponent<MeshFilter>().sharedMesh.vertices)
                {rest.minY=Mathf.Min(rest.minY,tank.InverseTransformPoint(child.TransformPoint(vertex)).y);radius=Mathf.Max(radius,Vector3.Scale(vertex,child.localScale).magnitude);}
                rest.rotationDisplacement=2*radius*Mathf.Sin(7*Mathf.Deg2Rad*.5f);aquariumPlants.Add(child,rest);
            }
            Check(aquariumPlants.Count==2,"Two inherited planted capsules cached from actual rest meshes");
            File.WriteAllText(Path.Combine(evidence,"aquarium-planted-geometry.json"),JsonUtility.ToJson(new AquariumPlantReceipt{plants=new List<AquariumPlantRest>(aquariumPlants.Values).ToArray()},true));
        }
        [Serializable] sealed class AquariumPlantReceipt
        {public string source="Inherited capsule bases extend below sand. Lower allowance is actual rest-mesh minimum minus 2*r*sin(7deg/2); current anchors/scales and seven-degree lean are checked. All upper/side vertices remain in water.";public AquariumPlantRest[] plants;}
        int AquariumParticles() => shop.World.FxPool.ParticleCountForValidation(shop.World.AquariumOwnerForValidation("tank-a"));
        int AquariumBubbles()
        {int count=0;foreach(Transform child in StationOf(StationKind.TankA).transform)if(child.name=="Air bubble" && child.gameObject.activeInHierarchy)count++;return count;}
        IEnumerator AquariumAim(float distance)
        {
            shop.SetPanel(ShopPanel.None);var tank=StationOf(StationKind.TankA).transform;
            GateAim(tank.TransformPoint(new Vector3(0,.2f,-distance)),tank.TransformPoint(new Vector3(0,1.58f,0)));
            Physics.SyncTransforms();yield return null;yield return null;
        }
        int AquariumRenderedBounds()
        {
            var world=shop.World;var tank=StationOf(StationKind.TankA).transform;var water=tank.Find("Water volume");var sand=tank.Find("Tank sand");
            float floor=sand.localPosition.y+sand.localScale.y*.5f;int samples=0;
            Action<Transform> inspect=visual=>
            {
                var filter=visual.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable)throw new InvalidOperationException("Aquarium actual mesh unreadable");
                bool planted=aquariumPlants.TryGetValue(visual,out var rest);
                if(planted && (visual.localPosition!=rest.position || visual.localScale!=rest.scale || Quaternion.Angle(visual.localRotation,rest.rotation)>7.001f))
                    throw new InvalidOperationException("Inherited plant anchor/scale or bounded lean changed");
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point=visual.TransformPoint(vertex);var p=water.InverseTransformPoint(point);
                    bool lower=planted ? tank.InverseTransformPoint(point).y<rest.minY-rest.rotationDisplacement-.0001f : p.y<-.5001f || tank.InverseTransformPoint(point).y<floor-.0001f;
                    if(Mathf.Abs(p.x)>.5001f || p.y>.5001f || Mathf.Abs(p.z)>.5001f || lower)
                        throw new InvalidOperationException("Actual aquarium mesh leaves animated water/sand bounds: "+visual.name+" / "+p);
                }
                samples++;
            };
            foreach(Transform child in tank)if((child.name=="Air bubble" || child.name=="Aquatic frond") && child.gameObject.activeInHierarchy)inspect(child);
            var owner=world.AquariumOwnerForValidation("tank-a");int count=AquariumParticles();
            for(int i=0;i<count;i++)
            {var visual=world.FxPool.ParticleVisualForValidation(owner,i);if(!visual)throw new InvalidOperationException("Aquarium particle ordinal missing");inspect(visual);}
            return samples;
        }
        IEnumerator AquariumClip(string name,float seconds,bool occupied,bool reduced=false,int bubbleCount=4,Action<float> action=null)
        {
            var world=shop.World;var report=new AquariumClipReceipt{name=name};var frames=new List<AquariumFrame>();int index=0;
            Action<float> sample=age=>
            {
                action?.Invoke(age);int count=AquariumParticles();report.peak=Mathf.Max(report.peak,count);
                if(index++%5==0)report.meshSamples+=AquariumRenderedBounds();
                frames.Add(new AquariumFrame{seconds=age,fishAge=world.PondletAgeForValidation("tank-a"),placementAge=world.AquariumPlacementAgeForValidation("tank-a"),
                    occupied=shop.Model.GetTank("tank-a").HasValue,reserved=shop.Model.GetReservation("tank-a").HasValue,trail=world.AquariumTrailActiveForValidation("tank-a"),particles=count,
                    habitatBubbles=AquariumBubbles(),placementEmissions=world.AquariumPlacementEmissionCount,trailEmissions=world.AquariumTrailEmissionCount});
            };
            if(Application.isEditor){float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<seconds){sample(Time.realtimeSinceStartup-began);yield return null;}}
            else yield return ReviewClip(name,seconds,sample);
            report.frames=frames.ToArray();File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(report,true));
            Check(report.meshSamples>0 && report.peak<=(bubbleCount==1?8:16),"Actual bubbles/particles stay in animated water; planted fronds retain derived buried-base envelope / "+name);
            Check(shop.Model.GetTank("tank-a").HasValue==occupied && AquariumBubbles()==(reduced?0:bubbleCount),"Exact occupant and comfort habitat count / "+name);
            if(!occupied || reduced)Check(AquariumParticles()==0 && !world.AquariumTrailActiveForValidation("tank-a"),"No specimen wake in empty/reduced habitat / "+name);
        }
        IEnumerator AquariumLifecycle(string specimen)
        {
            var world=shop.World;shop.FreezeFxVocabularyForValidation=true;
            try
            {
                string authority=FxAuthority();int placements=world.PondletPlacementCount,emissions=world.AquariumPlacementEmissionCount;
                foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Arrange,ShopPanel.Upgrade})
                {
                    shop.SetPanel(panel);float age=world.PondletAgeForValidation("tank-a");world.Animate(Time.time,.3f);
                    Check(world.PondletAgeForValidation("tank-a")==age && AquariumParticles()==0 && !world.PondletTransientForValidation("tank-a") && AquariumBubbles()==0,"Modal/pause/arrange freezes fish and retires habitat/wake / "+panel);
                    shop.SetPanel(ShopPanel.None);world.Animate(Time.time,.1f);
                }
                shop.SetFxFocusForValidation(false);float stopped=world.PondletAgeForValidation("tank-a");world.Animate(Time.time,.2f);
                Check(AquariumParticles()==0 && world.PondletAgeForValidation("tank-a")==stopped && AquariumBubbles()==0,"Focus loss cancels owned aquarium motion");shop.SetFxFocusForValidation(true);
                world.ResetFxPresentation();Check(AquariumParticles()==0 && !world.PondletTransientForValidation("tank-a"),"Reset clears aquarium accents");world.Animate(Time.time,.1f);
                var tank=StationOf(StationKind.TankA).transform;var position=tank.position;var owner=world.AquariumOwnerForValidation("tank-a");
                try {tank.position+=Vector3.right*.05f;world.Animate(Time.time,.1f);Check(!world.FxCues.IsValid(owner) && world.FxPool.ParticleCountForValidation(owner)==0 && !world.PondletTransientForValidation("tank-a"),"Labeled moved-tank fixture retires old wake/surface owner");}
                finally {tank.position=position;world.Refresh(shop.Model);world.ResetFxPresentation();}
                owner=world.AquariumOwnerForValidation("tank-a");
                try {tank.gameObject.SetActive(false);world.Animate(Time.time,.1f);Check(!world.FxCues.IsValid(owner) && world.FxPool.ParticleCountForValidation(owner)==0,"Inactive tank retires old particles");}
                finally {tank.gameObject.SetActive(true);world.Refresh(shop.Model);world.ResetFxPresentation();}
                world.SetFxEnabledForValidation(false);world.Animate(Time.time,.2f);Check(AquariumParticles()==0 && AquariumBubbles()==0,"Disabled FX leaves no wake/habitat motion");world.SetFxEnabledForValidation(true);
                shop.enabled=false;Check(AquariumParticles()==0,"Session disable retires aquarium particles");shop.enabled=true;shop.SetFxFocusForValidation(true);
                Check(FxAuthority()==authority && world.PondletPlacementCount==placements && world.AquariumPlacementEmissionCount==emissions,"Frozen lifecycle fixtures preserve exact authority and cannot replay placement");
                Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.GetTank("tank-a").Value.Id==specimen,"Reload actual occupied saved specimen");shop.SetPanel(ShopPanel.None);world.Animate(Time.time,.1f);
                Check(world.PondletPlacementCount==placements && world.AquariumPlacementEmissionCount==emissions && world.AquariumPlacementAgeForValidation("tank-a")<0,"Reload resumes habitat without replaying placement ripple");
            }
            finally {shop.FreezeFxVocabularyForValidation=false;shop.enabled=true;world.SetFxEnabledForValidation(true);world.SetFxPreviewModes(false,false);shop.SetFxFocusForValidation(true);shop.SetPanel(ShopPanel.None);world.ResetFxPresentation();}
            yield return null;
        }
        IEnumerator FxAquariumScenario()
        {
            limitations.Add("FX-06R-06: actual saved held collection/dosing/ten-second hatch/tank-a placement/customer reservation/paid sale. Synthetic InputSystem and normal-HUD operator views; scripted casual arrival only. Labeled frozen transform/disable fixtures and isolated real Ready checkpoint rewinds for active-ripple interruption and failed persistence; those rewinds are not additional progression. Low/reduced policies are diagnostic, no stored preference edits. Silent videos do not prove native input, audio listening, creative acceptance or performance.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Aquarium exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionAquariumKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionAquariumMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor){int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==width && Screen.height==height,"Exact actual aquarium HUD resolution");}
            var world=shop.World;Check(!shop.Model.GetTank("tank-a").HasValue && !world.PondletVisualForValidation("tank-a"),"Empty tank contains no decorative specimen");
            AquariumCachePlantedGeometry();
            yield return AquariumAim(3.1f);yield return AquariumClip("aquarium-empty-night-aisle",2,false);
            yield return FxLabPrepare(false);yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"Real ten-second Pondlet incubation");
            string specimen=shop.Model.Incubator.SpecimenId;Check(shop.Model.Incubator.SpeciesId=="pondlet" && !string.IsNullOrEmpty(specimen),"Actual hatched Pondlet identity");
            yield return AquariumAim(2);Check(shop.Focused==StationOf(StationKind.TankA) && shop.SaveNow(),"Physical destination focus and real Ready save");byte[] readySlot=File.ReadAllBytes(shop.SavePath);
            int placements=world.PondletPlacementCount,placementMotes=world.AquariumPlacementEmissionCount,plays=shop.GetComponent<ShopAudio>().LabPlaybackCount;bool transferred=false;
            yield return AquariumClip("aquarium-placement-night-close",2.2f,true,false,4,age=>
            {if(!transferred && age>=.35f){Ok(shop.TransferToDisplay("tank-a"),"Actual saved transfer inside live recording");transferred=true;}});
            Check(transferred && world.PondletPlacementCount==placements+1 && world.AquariumPlacementEmissionCount==placementMotes+6 && shop.Model.GetTank("tank-a").Value.Id==specimen && shop.Model.Incubator.Phase!=IncubatorPhase.Ready,"Single actual placement emits six bounded surface/bubble accents once");
            string authority=FxAuthority();uint sequence=shop.FxActionSequenceForValidation;int emissions=world.AquariumPlacementEmissionCount;
            Check(!world.PresentPondletPlacement("tank-a",sequence) && !shop.TransferToDisplay("tank-a").Success && FxAuthority()==authority && world.PondletPlacementCount==placements+1 && world.AquariumPlacementEmissionCount==emissions,"Repeated transfer/cue cannot duplicate specimen or ripple");
            Check(shop.GetComponent<ShopAudio>().LabPlaybackCount==plays,"Placement adds no duplicate laboratory sound");PondletSweep(specimen);
            int topology=world.GetComponentsInChildren<Transform>(true).Length,renderers=world.GetComponentsInChildren<Renderer>(true).Length,trail=world.AquariumTrailEmissionCount;
            yield return AquariumAim(3.1f);yield return AquariumClip("aquarium-swim-night-aisle",4,true);
            Check(world.AquariumTrailEmissionCount>trail,"Real swimming emits a moving specimen wake");yield return AquariumLifecycle(specimen);
            yield return EnterCounter();Check(shop.OpenForDay(),"Open actual day for real sale");shop.SetPanel(ShopPanel.None);
            yield return AquariumAim(2);yield return AquariumClip("aquarium-swim-day-close",4,true);
            world.SetFxPreviewModes(false,true);yield return AquariumClip("aquarium-low-day-close",3,true,false,1);
            world.SetFxPreviewModes(true,true);var fish=world.PondletVisualForValidation("tank-a");yield return null;var position=fish.localPosition;var rotation=fish.localRotation;int noTrail=world.AquariumTrailEmissionCount;
            yield return AquariumClip("aquarium-reduced-day-close",3,true,true,0);Check(fish.localPosition==position && fish.localRotation==rotation && world.AquariumTrailEmissionCount==noTrail,"Reduced motion retains stationary specimen with no wake");world.SetFxPreviewModes(false,false);
            Check(world.GetComponentsInChildren<Transform>(true).Length==topology && world.GetComponentsInChildren<Renderer>(true).Length==renderers,"Steady aquarium and comfort modes reuse existing topology");
            shop.Customers.ScriptedArchetypes.Enqueue(CustomerArchetypes.Casual);Check(shop.Customers.SpawnNow(),"Scripted casual arrival uses normal customer logic");var visit=shop.Customers.Visits[shop.Customers.Visits.Count-1];
            yield return AutomationWait(()=>visit.Phase==CustomerPhase.Queued,40,"Normal customer reserves actual saved Pondlet");
            Check(visit.SpecimenId==specimen && visit.TargetTankId=="tank-a" && shop.Model.GetReservation("tank-a").HasValue,"Exact specimen reservation");yield return AquariumAim(3.1f);yield return AquariumClip("aquarium-reserved-day-aisle",3,true);
            yield return EnterCounter();int cash=shop.Model.Currency,price=visit.Price;long revenue=shop.Reports.Current.revenue;
            Check(shop.ServeCustomer() && visit.Outcome==CustomerOutcome.Sold && shop.Model.Currency==cash+price && shop.Reports.Current.revenue==revenue+price && !shop.Model.GetTank("tank-a").HasValue,"Actual checkout consumes exact specimen and pays once");
            Check(!visit.Serve(shop.Cycle.Phase).Success && shop.Model.Currency==cash+price,"Completed visit cannot pay again");
            yield return AquariumAim(2);yield return AquariumClip("aquarium-paid-empty-day",2,false);
            Check(!world.PondletVisualForValidation("tank-a") && AquariumParticles()==0 && AquariumBubbles()==4,"Paid empty aquarium retains habitat and no ghost specimen/wake");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==cash+price && !shop.Model.GetTank("tank-a").HasValue,"Paid save reload preserves actual cash and empty tank");shop.SetPanel(ShopPanel.None);byte[] paidSlot=File.ReadAllBytes(shop.SavePath);
            int realPlacements=world.PondletPlacementCount;
            // A labeled replay of the actual Ready checkpoint isolates interruption inside
            // the short event; it is not another hatched/sold specimen in progression evidence.
            File.WriteAllBytes(shop.SavePath,readySlot);Check(shop.ReloadSaved(),"Labeled Ready rewind for active placement interruption");yield return AquariumAim(2);
            Ok(shop.TransferToDisplay("tank-a"),"Real guarded fixture transfer starts a fresh placement ripple");
            Check(world.AquariumPlacementAgeForValidation("tank-a")>=0 && world.AquariumPlacementAgeForValidation("tank-a")<.52f && AquariumParticles()>0,"Fixture reaches actual active placement particle state");
            int interruptedEmissions=world.AquariumPlacementEmissionCount;shop.SetPanel(ShopPanel.Pause);float pausedAge=world.PondletAgeForValidation("tank-a");yield return new WaitForSeconds(.15f);
            Check(AquariumParticles()==0 && world.AquariumPlacementAgeForValidation("tank-a")<0 && world.PondletAgeForValidation("tank-a")==pausedAge,"Pause during active ripple immediately retires surface/particle schedule and freezes fish");
            shop.SetPanel(ShopPanel.None);yield return new WaitForSeconds(.6f);
            Check(world.AquariumPlacementEmissionCount==interruptedEmissions && world.AquariumPlacementAgeForValidation("tank-a")<0,"Resume cannot replay or finish canceled placement ripple");
            File.WriteAllBytes(shop.SavePath,paidSlot);Check(shop.ReloadSaved() && shop.Model.Currency==cash+price && !shop.Model.GetTank("tank-a").HasValue,"Restore actual paid checkpoint after active-ripple interruption fixture");shop.SetPanel(ShopPanel.None);
            File.WriteAllBytes(shop.SavePath,readySlot);Check(shop.ReloadSaved(),"Labeled rewind to real Ready checkpoint for failed-write fixture");yield return AquariumAim(2);placements=world.PondletPlacementCount;emissions=world.AquariumPlacementEmissionCount;
            TutorialBlockSave();
            try {Check(shop.TransferToDisplay("tank-a").Success && world.PondletPlacementCount==placements && world.AquariumPlacementEmissionCount==emissions && world.AquariumPlacementAgeForValidation("tank-a")<0 && Convert.ToBase64String(File.ReadAllBytes(shop.SavePath))==Convert.ToBase64String(readySlot),"Real failed persistence suppresses placement ripple and preserves durable Ready bytes");}
            finally {TutorialUnblockSave();File.WriteAllBytes(shop.SavePath,paidSlot);}
            Check(shop.ReloadSaved() && shop.Model.Currency==cash+price && !shop.Model.GetTank("tank-a").HasValue,"Restore exact actual paid checkpoint after failure fixture");shop.SetPanel(ShopPanel.None);
            File.WriteAllText(Path.Combine(evidence,"aquarium-hatch-to-sale.json"),JsonUtility.ToJson(new PondletSaleReceipt{specimen=specimen,visit=visit.Id,price=price,cashBefore=cash,cashAfter=shop.Model.Currency,revenueBefore=revenue,revenueAfter=shop.Reports.Current.revenue,placements=realPlacements,glances=world.PondletGlanceCount,baselineTransforms=topology,baselineRenderers=renderers},true));
            var pool=world.FxPool;Check(pool.OwnedCount==96 && pool.OwnedMeshCount==5 && pool.OwnedMaterialCount==1 && pool.Root.GetComponentsInChildren<Collider>(true).Length==0,"Aquarium retains fixed pool/mesh/material/collider budgets");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
