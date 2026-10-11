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
        sealed class PondletMesh { public Transform Root; public Vector3[] Vertices; }
        [Serializable] sealed class PondletBound { public float time; public int variant; public Vector3 min,max; }
        [Serializable] sealed class PondletSweepReceipt { public string specimen; public int meshes,vertices; public PondletBound[] samples; }
        [Serializable] sealed class PondletFrame { public string file; public float seconds,visualAge; }
        [Serializable] sealed class PondletMotionReceipt { public string source="Actual live Unity HUD frames; synthetic camera fixture, ordinary simulation continues"; public PondletFrame[] frames; }
        void PondletSweep(string specimen)
        {
            var w=shop.World;var root=w.PondletVisualForValidation("tank-a");var station=StationOf(StationKind.TankA).transform;
            var filters=root.GetComponentsInChildren<MeshFilter>();var meshes=new PondletMesh[filters.Length];int vertices=0;
            for(int i=0;i<filters.Length;i++){Check(filters[i].sharedMesh && filters[i].sharedMesh.isReadable,"Actual Pondlet mesh readable / "+filters[i].name);meshes[i]=new PondletMesh{Root=filters[i].transform,Vertices=filters[i].sharedMesh.vertices};vertices+=meshes[i].Vertices.Length;}
            var samples=new List<PondletBound>();string authority=FxAuthority();
            for(int variant=0;variant<4;variant++)for(int i=0;i<=48;i++)
            {
                float time=i*.16f;w.PreviewPondletForValidation("tank-a",time,variant==3,variant==1?.3f:-1,variant==2?.26f:-1);
                Vector3 min=Vector3.one*100,max=Vector3.one*-100;
                foreach(var m in meshes)foreach(var v in m.Vertices){var p=station.InverseTransformPoint(m.Root.TransformPoint(v));min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
                Check(min.x>=-.93001f && max.x<=.93001f && min.y>=1.07999f && max.y<=1.97001f && min.z>=-.57001f && max.z<=.57001f,"Actual fish vertices inside water/sand prism / "+variant+" / "+time);
                samples.Add(new PondletBound{time=time,variant=variant,min=min,max=max});
            }
            Check(FxAuthority()==authority,"Synchronous actual mesh sweep changes no game authority");
            File.WriteAllText(Path.Combine(evidence,"pondlet-sweep.json"),JsonUtility.ToJson(new PondletSweepReceipt{specimen=specimen,meshes=meshes.Length,vertices=vertices,samples=samples.ToArray()},true));
            w.Animate(0,0);
        }
        IEnumerator PondletView(string name,float distance=2.6f)
        {
            shop.SetPanel(ShopPanel.None);var t=StationOf(StationKind.TankA).transform;
            GateAim(t.TransformPoint(new Vector3(0,.2f,-distance)),t.TransformPoint(new Vector3(0,1.58f,0)));Physics.SyncTransforms();yield return null;yield return null;
            yield return GateCapture(name);
        }
        IEnumerator FxPondletScenario()
        {
            limitations.Add("FX-05: actual mesh/camera/physics/customer/save routes with synthetic InputSystem and scripted spawn/cameras. Static key samples and live frame sequence are separate. No native mouse/keyboard, listening, creative approval, controlled single-instance profile or new species/gameplay.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX-05 isolated evidence slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionFxPondletKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionFxPondletMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor){int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==width && Screen.height==height,"Actual requested Windows HUD resolution");}
            var w=shop.World;Check(!shop.Model.GetTank("tank-a").HasValue && !w.PondletVisualForValidation("tank-a"),"Empty aquarium has no decorative saleable fish");
            int topology=w.GetComponentsInChildren<Transform>(true).Length,renderers=w.GetComponentsInChildren<Renderer>(true).Length;
            yield return PondletView("fx05-empty-night");
            yield return FxLabPrepare(false);yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"Actual ten-second Pondlet hatch");
            string specimen=shop.Model.Incubator.SpecimenId;Check(!string.IsNullOrEmpty(specimen) && shop.Model.Incubator.SpeciesId=="pondlet" && shop.Model.IsDiscovered("pondlet"),"One real hatched Pondlet specimen/discovery");
            shop.SetPanel(ShopPanel.None);var tank=StationOf(StationKind.TankA).transform;GateAim(tank.TransformPoint(new Vector3(0,.2f,-2)),tank.TransformPoint(new Vector3(0,1.56f,0)));yield return null;yield return null;
            Check(shop.SaveNow(),"Save actual Ready checkpoint for isolated failure fixture");byte[] readySlot=File.ReadAllBytes(shop.SavePath);
            Check(shop.Focused==tank.GetComponent<Station>(),"Physics focus resolves destination aquarium");int placements=w.PondletPlacementCount;int plays=shop.GetComponent<ShopAudio>().LabPlaybackCount;
            Ok(shop.TransferToDisplay("tank-a"),"Actual saved destination transfer");
            Check(w.PondletPlacementCount==placements+1 && w.PondletTransientForValidation("tank-a") && shop.Model.GetTank("tank-a").Value.Id==specimen && shop.Model.Incubator.Phase!=IncubatorPhase.Ready,"One placement pose follows one exact committed specimen");
            string authority=FxAuthority();uint sequence=shop.FxActionSequenceForValidation;
            Check(!w.PresentPondletPlacement("tank-a",sequence) && !shop.TransferToDisplay("tank-a").Success && w.PondletPlacementCount==placements+1 && FxAuthority()==authority,"Repeated cue/transfer adds no second pose, specimen or resources");
            Check(shop.GetComponent<ShopAudio>().LabPlaybackCount==plays,"Destination settle adds no duplicate lab sound");
            yield return PondletView("fx05-occupied-night");PondletSweep(specimen);
            var fish=w.PondletVisualForValidation("tank-a");var left=fish.Find("Left petal");Quaternion mirrored=Quaternion.Euler(0,180,0);
            Check(Quaternion.Angle(left.localRotation,mirrored)<=12.01f,"Live left petal keeps authored mirrored heading");
            int glances=w.PondletGlanceCount;yield return new WaitForSeconds(1.7f);
            Check(w.PondletGlanceCount>=glances+1,"Continuous legal Physics focus causes a bounded curious glance");int once=w.PondletGlanceCount;yield return new WaitForSeconds(.8f);Check(w.PondletGlanceCount==once,"Focus glance obeys eight-second cooldown without sound spam");
            string beforeMimic=FxAuthority();Quaternion beforePose=left.localRotation;w.PoseFishMimic("tank-a",.9f,false);Check(left.localRotation==beforePose,"Comedy queues a detail request without a second transform writer");w.Animate(0,0);Check(Quaternion.Angle(left.localRotation,mirrored)<=12.01f && FxAuthority()==beforeMimic,"Single detail resolver contains comedy and preserves authority");w.ClearFishMimic();
            shop.SetPanel(ShopPanel.Pause);float age=w.PondletAgeForValidation("tank-a");yield return new WaitForSeconds(.3f);Check(w.PondletAgeForValidation("tank-a")==age && !w.PondletTransientForValidation("tank-a"),"Actual pause freezes steady swim and cancels transient accents");shop.SetPanel(ShopPanel.None);
            w.SetFxPreviewModes(false,true);yield return PondletView("fx05-low-night");int bubbles=0;foreach(Transform child in tank)if(child.name=="Air bubble" && child.gameObject.activeSelf)bubbles++;Check(bubbles==1,"Low-detail aquarium reuses only one of four existing bubbles");
            w.SetFxPreviewModes(true,true);yield return PondletView("fx05-reduced-night");Vector3 held=fish.localPosition;Quaternion facing=fish.localRotation;yield return new WaitForSeconds(.3f);Check(fish.localPosition==held && fish.localRotation==facing && Quaternion.Angle(left.localRotation,mirrored)<.01f,"Reduced motion keeps fixed fish/rest petals");w.SetFxPreviewModes(false,false);
            foreach(var policy in new[]{FxViewPolicy.Unfocused,FxViewPolicy.Modal,FxViewPolicy.Arrange})
            {w.SetFxViewPolicy(policy);w.SetAquariumFocus(null);float stopped=w.PondletAgeForValidation("tank-a");w.Animate(0,.25f);Check(w.PondletAgeForValidation("tank-a")==stopped && !w.PondletTransientForValidation("tank-a"),"Aquarium interruption clears focus/place/glance / "+policy);}
            w.SetFxViewPolicy(FxViewPolicy.Visible);
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.GetTank("tank-a").Value.Id==specimen && w.PondletPlacementCount==placements+1 && !w.PondletTransientForValidation("tank-a"),"Reload restores one saved specimen without placement replay");shop.SetPanel(ShopPanel.None);
            // Labelled valid save-ID replacement isolates the same-species identity edge; not progression evidence.
            byte[] occupiedSlot=File.ReadAllBytes(shop.SavePath);int rootId=w.PondletVisualForValidation("tank-a").GetInstanceID();var oldOwner=w.FxCues.Find("tank-a");
            string replacement=Guid.NewGuid().ToString("N"),text=File.ReadAllText(shop.SavePath);Check(text.Contains(specimen),"Identity fixture begins from the actual occupied slot");
            File.WriteAllText(shop.SavePath,text.Replace(specimen,replacement));Check(shop.ReloadSaved() && shop.Model.GetTank("tank-a").Value.Id==replacement && w.PondletVisualForValidation("tank-a").GetInstanceID()==rootId && !w.FxCues.IsValid(oldOwner) && w.PondletAgeForValidation("tank-a")==0 && !w.PondletTransientForValidation("tank-a"),"Same-species valid save-ID replacement retires personality/cue identity without duplicate geometry");
            File.WriteAllBytes(shop.SavePath,occupiedSlot);Check(shop.ReloadSaved() && shop.Model.GetTank("tank-a").Value.Id==specimen,"Restore exact actual occupied slot after labelled identity fixture");shop.SetPanel(ShopPanel.None);
            // Actual live capture cadence is recorded, not reconstructed from frozen key poses.
            yield return PondletView("fx05-live-view");var frames=new List<PondletFrame>();float origin=Time.realtimeSinceStartup;
            for(int i=0;i<20;i++){string name="fx05-live-"+i.ToString("000");yield return GateCapture(name);frames.Add(new PondletFrame{file=name+"-screen.png",seconds=Time.realtimeSinceStartup-origin,visualAge=w.PondletAgeForValidation("tank-a")});yield return new WaitForSeconds(.15f);}
            File.WriteAllText(Path.Combine(evidence,"pondlet-live-motion.json"),JsonUtility.ToJson(new PondletMotionReceipt{frames=frames.ToArray()},true));
            int steadyTransforms=w.GetComponentsInChildren<Transform>(true).Length,steadyRenderers=w.GetComponentsInChildren<Renderer>(true).Length;
            yield return new WaitForSeconds(8);Check(w.GetComponentsInChildren<Transform>(true).Length==steadyTransforms && w.GetComponentsInChildren<Renderer>(true).Length==steadyRenderers && w.FxPool.OwnedCount==96,"Live aquarium reuses fixed topology/pool; no spawned habitat objects");
            // The normal customer system owns navigation/reservation/payment. Only arrival selection is scripted.
            yield return EnterCounter();Check(shop.OpenForDay(),"Open actual store day for hatch-to-sale route");shop.SetPanel(ShopPanel.None);yield return PondletView("fx05-occupied-day");
            shop.Customers.ScriptedArchetypes.Enqueue(CustomerArchetypes.Casual);Check(shop.Customers.SpawnNow(),"Scripted casual arrival uses normal navigation and selection");var visit=shop.Customers.Visits[shop.Customers.Visits.Count-1];
            yield return AutomationWait(()=>visit.Phase==CustomerPhase.Queued,40,"Customer browses and reserves actual Pondlet");
            Check(visit.SpecimenId==specimen && visit.TargetTankId=="tank-a" && shop.Model.GetReservation("tank-a").HasValue,"Customer owns exact specimen reservation");yield return PondletView("fx05-reserved-day");
            yield return EnterCounter();int cash=shop.Model.Currency,price=visit.Price;long revenue=shop.Reports.Current.revenue;
            Check(shop.ServeCustomer() && visit.Outcome==CustomerOutcome.Sold && shop.Model.Currency==cash+price && shop.Reports.Current.revenue==revenue+price && !shop.Model.GetTank("tank-a").HasValue,"Actual checkout consumes one specimen and pays agreed price once");
            Check(!visit.Serve(shop.Cycle.Phase).Success && shop.Model.Currency==cash+price,"Repeated completed visit cannot pay twice");
            yield return PondletView("fx05-paid-empty-day");Check(!w.PondletVisualForValidation("tank-a"),"Paid aquarium is genuinely empty without stale fish/personality");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==cash+price && !shop.Model.GetTank("tank-a").HasValue && !w.PondletVisualForValidation("tank-a"),"Actual paid save reload preserves cash and empty display");shop.SetPanel(ShopPanel.None);
            // Rewind only the isolated evidence Ready slot to inject a real write failure; restore paid slot afterwards.
            byte[] paidSlot=File.ReadAllBytes(shop.SavePath);File.WriteAllBytes(shop.SavePath,readySlot);Check(shop.ReloadSaved(),"Load labelled real Ready checkpoint for failed-write presentation check");shop.SetPanel(ShopPanel.None);
            tank=StationOf(StationKind.TankA).transform;GateAim(tank.TransformPoint(new Vector3(0,.2f,-2)),tank.TransformPoint(new Vector3(0,1.56f,0)));yield return null;yield return null;
            string blocked=shop.SavePath+".tmp";int delivered=w.PondletPlacementCount;Directory.CreateDirectory(blocked);
            try {Check(shop.TransferToDisplay("tank-a").Success && w.PondletPlacementCount==delivered && !w.PondletTransientForValidation("tank-a") && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(shop.SavePath),readySlot),"Real failed save suppresses new Pondlet placement cue and preserves durable Ready bytes");}
            finally {Directory.Delete(blocked);}
            File.WriteAllBytes(shop.SavePath,paidSlot);Check(shop.ReloadSaved() && shop.Model.Currency==cash+price && !shop.Model.GetTank("tank-a").HasValue,"Restore exact paid checkpoint after isolated failed-write fixture");shop.SetPanel(ShopPanel.None);
            File.WriteAllText(Path.Combine(evidence,"hatch-to-sale.json"),JsonUtility.ToJson(new PondletSaleReceipt{specimen=specimen,visit=visit.Id,price=price,cashBefore=cash,cashAfter=shop.Model.Currency,revenueBefore=revenue,revenueAfter=shop.Reports.Current.revenue,placements=w.PondletPlacementCount,glances=w.PondletGlanceCount,baselineTransforms=topology,baselineRenderers=renderers},true));
        }
        [Serializable] sealed class PondletSaleReceipt { public string specimen,visit;public int price,cashBefore,cashAfter,placements,glances,baselineTransforms,baselineRenderers;public long revenueBefore,revenueAfter; }
    }
}
#endif
