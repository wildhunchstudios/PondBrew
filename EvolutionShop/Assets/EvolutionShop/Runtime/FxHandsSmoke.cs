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
        [Serializable] sealed class HandSample { public string pose; public bool upgrade, reduced; public float age; public Vector3 min, max, viewportMin, viewportMax; }
        [Serializable] sealed class HandImportEvidence { public int renderers, triangles, pivots, transforms; public string[] materials, hierarchy; public Vector3[] positions, scales; public Quaternion[] rotations; public HandSample[] samples; }
        sealed class HandMesh { public Transform root; public Vector3[] vertices; public int[] triangles; }
        readonly List<HandSample> handSamples = new List<HandSample>();
        void HandsSweep()
        {
            var world=shop.World;var root=world.HandsVisual;var body=root.parent;var camera=shop.Player.View;
            var filters=root.GetComponentsInChildren<MeshFilter>(true);var meshes=new HandMesh[filters.Length];
            int triangles=0;var materials=new HashSet<string>();
            for(int i=0;i<filters.Length;i++){var m=filters[i].sharedMesh;Check(m && m.isReadable,"Actual imported hand mesh readable / "+filters[i].name);meshes[i]=new HandMesh {root=filters[i].transform,vertices=m.vertices,triangles=m.triangles};triangles+=m.triangles.Length/3;foreach(var mat in filters[i].GetComponent<Renderer>().sharedMaterials)materials.Add(mat.name);}
            var transforms=root.GetComponentsInChildren<Transform>(true);int pivots=0;
            foreach(var t in transforms){Check(t.localScale.x>0 && t.localScale.y>0 && t.localScale.z>0,"Positive imported rigid scale / "+t.name);if(t.name.StartsWith("Right",StringComparison.Ordinal)||t.name.StartsWith("Left",StringComparison.Ordinal))pivots++;}
            Check(filters.Length==12 && triangles<=6000 && materials.Count==3 && pivots==14 && transforms.Length<=16,"Admitted12-renderer14-pivot3-material original rig / triangles="+triangles+" transforms="+transforms.Length);
            Check(root.GetComponentsInChildren<Collider>(true).Length==0 && root.GetComponentsInChildren<Animator>(true).Length==0,"Hands add no collider, Animator or camera");
            Quaternion cameraRotation=camera.transform.localRotation;
            foreach(bool upgrade in new[]{false,true})foreach(bool reduced in new[]{false,true})
            {
                world.SetToolUpgrade(upgrade);
                Vector3 combinedMin=Vector3.one*100,combinedMax=Vector3.one*-100;
                foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))
                {
                    if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                    var bounds=renderer.localBounds;
                    for(int corner=0;corner<8;corner++){var ext=bounds.extents;var point=body.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center+new Vector3((corner&1)==0?-ext.x:ext.x,(corner&2)==0?-ext.y:ext.y,(corner&4)==0?-ext.z:ext.z)));combinedMin=Vector3.Min(combinedMin,point);combinedMax=Vector3.Max(combinedMax,point);}
                }
                Check(combinedMin.x>=-.2301f && combinedMax.x<=.2301f && combinedMin.y>=-.3801f && combinedMax.y<=.3801f && combinedMin.z>=-.2401f && combinedMax.z<=.5201f,"Combined active hand/tool renderer bounds / upgraded="+upgrade+" min="+combinedMin+" max="+combinedMax);
                foreach(var id in new[]{FxCueId.None,FxCueId.HandCollectStart,FxCueId.CollectedUnit,FxCueId.HandCollectEnd,FxCueId.HandDose,FxCueId.HandPlace,FxCueId.HandRefused,FxCueId.HandUpgrade})
                foreach(float age in new[]{0f,.04f,.08f,.12f,.20f,.22f,.26f,.30f,.40f,.48f})
                {
                    world.PreviewHandPoseForValidation(id,age,id==FxCueId.HandCollectStart||id==FxCueId.CollectedUnit,reduced);
                    Vector3 min=Vector3.one*100,max=Vector3.one*-100,vmin=min,vmax=max;
                    foreach(var m in meshes)foreach(var v in m.vertices)
                    {
                        Vector3 point=m.root.TransformPoint(v),local=body.InverseTransformPoint(point),view=camera.WorldToViewportPoint(point);
                        min=Vector3.Min(min,local);max=Vector3.Max(max,local);vmin=Vector3.Min(vmin,view);vmax=Vector3.Max(vmax,view);
                    }
                    bool fits=min.x>=-.2301f && max.x<=.2301f && min.y>=-.3801f && max.y<=.3801f && min.z>=-.2401f && max.z<=.5201f;
                    bool clear=vmin.z>camera.nearClipPlane && vmin.x>=0 && vmax.x<=1 && vmin.y>=0 && vmax.y<=1 && (vmin.x>.54f || vmax.x<.46f || vmin.y>.54f || vmax.y<.46f);
                    Check(fits && clear,"Actual vertex sweep, near plane, screen and crosshair / "+upgrade+" "+reduced+" "+id+" "+age+" bounds="+min+".."+max+" viewport="+vmin+".."+vmax);
                    handSamples.Add(new HandSample {pose=id.ToString(),upgrade=upgrade,reduced=reduced,age=age,min=min,max=max,viewportMin=vmin,viewportMax=vmax});
                }
                foreach(float pitch in new[]{-75f,0f,75f})
                {camera.transform.localRotation=Quaternion.Euler(pitch,0,0);world.PreviewHandPoseForValidation(FxCueId.CollectedUnit,.12f,true,reduced);bool clear=true;foreach(var m in meshes)foreach(var v in m.vertices){var p=camera.WorldToViewportPoint(m.root.TransformPoint(v));if(p.z<=camera.nearClipPlane || p.x<0 || p.x>1 || p.y<0 || p.y>1)clear=false;}Check(clear,"Actual imported hand fit at pitch extreme / "+pitch+" upgraded="+upgrade+" reduced="+reduced);}
                camera.transform.localRotation=cameraRotation;
                // Readout is closer to the camera than the imported glove vertices across the complete sweep above.
                // Check actual line-of-sight with ray/triangle intersection at a15-point readout grid.
                world.PreviewHandPoseForValidation(FxCueId.HandPlace,.22f,false,reduced);int obscured=0;
                for(int x=-2;x<=2;x++)for(int y=-1;y<=1;y++)
                {
                    var target=body.TransformPoint(new Vector3(x*.135f/4,-.129f+y*.015f,-.182f));var ray=target-camera.transform.position;
                    foreach(var m in meshes)for(int t=0;t<m.triangles.Length;t+=3)
                    {
                        var a=m.root.TransformPoint(m.vertices[m.triangles[t]]);var b=m.root.TransformPoint(m.vertices[m.triangles[t+1]]);var c=m.root.TransformPoint(m.vertices[m.triangles[t+2]]);
                        var e1=b-a;var e2=c-a;var h=Vector3.Cross(ray,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<.00000001f)continue;
                        var s=camera.transform.position-a;float u=Vector3.Dot(s,h)/det;if(u<0 || u>1)continue;var q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray,q)/det;if(v<0 || u+v>1)continue;float hit=Vector3.Dot(e2,q)/det;if(hit>0 && hit<.999f)obscured++;
                    }
                }
                Check(obscured==0,"Actual glove triangles leave15-point readout clear / upgrade="+upgrade+" reduced="+reduced);
            }
            var report=new HandImportEvidence {renderers=filters.Length,triangles=triangles,pivots=pivots,transforms=transforms.Length,materials=new string[materials.Count],hierarchy=new string[transforms.Length],positions=new Vector3[transforms.Length],scales=new Vector3[transforms.Length],rotations=new Quaternion[transforms.Length],samples=handSamples.ToArray()};materials.CopyTo(report.materials);
            world.SetToolUpgrade(shop.Model.UpgradeOwned);world.SetFxPreviewModes(false,false);world.CancelFxTransient();
            for(int i=0;i<transforms.Length;i++){report.hierarchy[i]=transforms[i].name;report.positions[i]=transforms[i].localPosition;report.scales[i]=transforms[i].localScale;report.rotations[i]=transforms[i].localRotation;}
            File.WriteAllText(Path.Combine(evidence,"hands-import-and-sweep.json"),JsonUtility.ToJson(report,true));
        }
        IEnumerator FxHandsScenario()
        {
            limitations.Add("FX-04: real Physics focus, held synthetic InputSystem mouse and real collection timing; explicitly labelled diagnostic camera/credit/pose/slap-preview/stock fixtures. This is not native control, listening, or human FX-06 acceptance. Modal dosing remains machine-only; no new interaction delay or carried specimen.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX-04 isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionFxHandsKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionFxHandsMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor){int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w && Screen.height==h,"Actual requested Windows render resolution");}
            var world=shop.World;Check(world.HandsVisual && world.HandsRigValid && world.HandsVisual.gameObject.activeInHierarchy,"Original glove resource and all14 named pivots loaded");
            var grower=StationOf(StationKind.Bloom).transform;GateAim(grower.TransformPoint(new Vector3(0,.2f,-1.7f)),grower.TransformPoint(new Vector3(0,1.2f,0)));yield return null;yield return null;
            HandsSweep();yield return GateCapture("fx04-rest-base");
            Vector3 fixedPose=world.ToolRoot.localPosition;yield return new WaitForSeconds(.3f);Check(world.ToolRoot.localPosition==fixedPose,"Idle keeps a fixed supported tool without ambient sway");
            var playerStart=shop.Player.transform.position;var cameraLocal=shop.Player.View.transform.localPosition;shop.AllowValidationInput=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.12f);
            Check(Vector3.Distance(playerStart,shop.Player.transform.position)>.05f && Vector3.Distance(world.ToolRoot.localPosition,fixedPose)>.00001f && Vector3.Distance(world.ToolRoot.localPosition,fixedPose)<=.0061f && shop.Player.View.transform.localPosition==cameraLocal,"Actual WASD travel drives bounded tool bob without camera shake");InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());shop.AllowValidationInput=false;yield return new WaitForSeconds(.18f);Check(world.ToolRoot.localPosition==fixedPose,"Stopped travel returns to fixed grip within160ms");GateAim(grower.TransformPoint(new Vector3(0,.2f,-1.7f)),grower.TransformPoint(new Vector3(0,1.2f,0)));yield return null;yield return null;
            int before=shop.Model.Available(CultureKind.Bloom),count=world.HandCueCount,played=shop.GetComponent<ShopAudio>().LabPlaybackCount;
            InputSystem.QueueStateEvent(cycleMouse,new MouseState {buttons=1});yield return new WaitForSeconds(.12f);
            Check(shop.HarvestProgress>0 && shop.Model.Available(CultureKind.Bloom)==before && world.HandAction==FxCueId.HandCollectStart && world.HandCueCount==count+1,"Valid held edge squeezes before unit, no premature inventory or sound");
            InputSystem.QueueStateEvent(cycleMouse,new MouseState());yield return null;yield return null;
            Check(world.HandAction==FxCueId.HandCollectEnd && shop.Model.Available(CultureKind.Bloom)==before,"Early release uncurls without a phantom unit");yield return new WaitForSeconds(.24f);
            Check(world.HandAction==FxCueId.None && world.ToolRoot.localPosition==fixedPose,"Release returns to exact rest within220ms");
            yield return FxLabHarvest(CultureKind.Bloom,true);
            Check(world.HandCueCount>=count+4 && shop.GetComponent<ShopAudio>().LabPlaybackCount==played+1,"One accepted unit reuses exactly one existing lab sound");
            yield return new WaitForSeconds(.24f);uint seq=shop.FxActionSequenceForValidation;count=world.HandCueCount;
            Check(!world.TryFxCue(FxCueId.CollectedUnit,seq) && world.HandCueCount==count,"Duplicate committed callback cannot replay hand recoil");
            world.TryFxCue(FxCueId.HandRefused,seq+1);Check(world.HandAction==FxCueId.HandRefused,"Explicit visible refusal gets one bounded stop gesture");yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(cycleMouse,new MouseState {buttons=1});yield return new WaitForSeconds(.1f);shop.SetPanel(ShopPanel.Pause);
            Check(!world.HandsVisual.gameObject.activeSelf && world.HandAction==FxCueId.None && world.ToolRoot.localPosition==fixedPose,"Pause clears action and tucks gloves immediately");InputSystem.QueueStateEvent(cycleMouse,new MouseState());shop.SetPanel(ShopPanel.None);yield return null;
            foreach(var policy in new[]{FxViewPolicy.Modal,FxViewPolicy.Unfocused,FxViewPolicy.Arrange}){world.TryFxCue(FxCueId.HandPlace,200);world.SetFxViewPolicy(policy);Check(!world.HandsVisual.gameObject.activeSelf && world.HandAction==FxCueId.None,"Policy interruption resets hands / "+policy);world.SetFxViewPolicy(FxViewPolicy.Visible);}
            world.ResetFxPresentation();world.SetHandContext(shop.Player.transform.position,false,true);Check(!world.HandsVisual.gameObject.activeSelf && world.HandAction==FxCueId.None,"Existing slap owner excludes operating gloves");world.SetHandContext(shop.Player.transform.position,false,false);
            shop.SlapGesture.Play(false);yield return null;Check(shop.SlapGesture.IsPlaying && !world.HandsVisual.gameObject.activeSelf,"Actual legacy SlapHand preview owns the visible gesture");yield return new WaitForSeconds(.4f);yield return null;Check(!shop.SlapGesture.IsPlaying && world.HandsVisual.gameObject.activeSelf,"Slap recovery restores fixed operating grip");
            yield return FxLabHarvest(CultureKind.Flow,false);
            var station=StationOf(StationKind.Incubator).transform;GateAim(station.TransformPoint(new Vector3(0,.2f,-1.5f)),station.TransformPoint(new Vector3(0,1.48f,0)));Physics.SyncTransforms();shop.SetPanel(ShopPanel.Incubator);count=world.HandCueCount;
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow) && !world.HandsVisual.gameObject.activeSelf && world.HandCueCount==count,"Real modal doses retain machine response without reaching through panel");yield return GateCapture("fx04-menu-dose");Check(shop.BeginManualIncubation(),"Normal guarded ten-second incubation starts");
            yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"FX04 real hatch");
            var tank=StationOf(StationKind.TankA);GateAim(tank.transform.TransformPoint(new Vector3(0,.2f,-2)),tank.transform.TransformPoint(new Vector3(0,1.4f,0)));Physics.SyncTransforms();yield return null;yield return null;
            Check(shop.Focused==tank,"Direct placement fixture resolves physical display");string specimen=shop.Model.Incubator.SpecimenId;Ok(shop.TransferToDisplay("tank-a"),"Accepted direct display transfer");
            Check(world.HandAction==FxCueId.HandPlace && shop.Model.GetTank("tank-a").Value.Id==specimen,"Destination settle follows saved exact specimen, no carry inventory");yield return GateCapture("fx04-place-store");
            StaffCreditFixture(100,"FX04 existing upgrade purchase only");shop.SetPanel(ShopPanel.Upgrade);count=world.HandCueCount;Ok(shop.BuySiphonUpgrade(),"Existing capacity upgrade buy");Check(shop.Model.UpgradeOwned && world.ToolChamber.localScale==new Vector3(1.35f,1.48f,1.35f) && world.HandCueCount==count && !world.HandsVisual.gameObject.activeSelf,"Modal purchase expands actual chamber without a replayed hand action");shop.SetPanel(ShopPanel.None);yield return GateCapture("fx04-rest-upgraded");
            string authority=FxAuthority();int resources=world.HandsVisual.GetComponentsInChildren<Transform>(true).Length;
            foreach(var id in new[]{FxCueId.HandCollectStart,FxCueId.CollectedUnit,FxCueId.HandPlace,FxCueId.HandUpgrade,FxCueId.HandRefused}){world.PreviewHandPoseForValidation(id,.12f,id==FxCueId.HandCollectStart,false);yield return Capture("fx04-pose-"+id);world.CancelFxTransient();}
            Check(FxAuthority()==authority,"Explicit pose-preview captures do not alter authority");world.SetFxPreviewModes(true,true);world.PreviewHandPoseForValidation(FxCueId.CollectedUnit,.12f,true,true);Check(world.ToolRoot.localPosition==fixedPose,"Reduced motion removes tool recoil");yield return GateCapture("fx04-reduced");world.SetFxPreviewModes(false,false);
            // Thirty seconds of in-place reuse, not a performance benchmark.
            float end=Time.realtimeSinceStartup+30;uint command=500;
            while(Time.realtimeSinceStartup<end){world.TryFxCue(FxCueId.HandPlace,command++);yield return new WaitForSeconds(.5f);}
            world.CancelFxTransient();Check(world.HandsVisual.GetComponentsInChildren<Transform>(true).Length==resources && world.HandsVisual.GetComponentsInChildren<Renderer>(true).Length==12 && world.FxPool.OwnedCount==96 && world.FxPool.ActiveCount==0 && FxAuthority()==authority,"30s reuse retains topology, pool and all game authority");
            TutorialBlockSave();played=shop.GetComponent<ShopAudio>().LabPlaybackCount;try{shop.Harvest(CultureKind.Bloom,shop.Model.CollectionSecondsPerUnit+.01f);Check(shop.SaveError!=null && world.HandAction==FxCueId.None && shop.GetComponent<ShopAudio>().LabPlaybackCount==played,"Failed saved operation suppresses new hand success and sound");}finally{TutorialUnblockSave();}
            Check(shop.ReloadSaved(),"Restore durable isolated state after failed write");shop.SetPanel(ShopPanel.None);world=shop.World;Check(world.HandsRigValid && world.HandAction==FxCueId.None,"Reload reconstructs grip without replay");
            world.enabled=false;Check(world.HandAction==FxCueId.None && !world.HandsVisual.gameObject.activeSelf,"World disable leaves no stale glove pose");world.enabled=true;yield return null;Check(world.HandsRigValid && world.HandsVisual.gameObject.activeSelf,"World enable restores neutral grip");
            Transform missing=null;foreach(var t in world.HandsVisual.GetComponentsInChildren<Transform>(true))if(t.name=="RightIndexTip")missing=t;Destroy(missing.gameObject);yield return null;yield return null;
            Check(!world.HandsRigValid,"Missing pivot falls back to imported rest");yield return FxLabHarvest(CultureKind.Bloom,false);Check(shop.SaveNow(),"Collection still works with missing presentation pivot");
            Check(shop.SaveNow(),"Final isolated FX04 save valid");
        }
    }
}
#endif
