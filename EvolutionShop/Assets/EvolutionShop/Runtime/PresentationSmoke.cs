#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator PresentationScenario()
        {
            limitations.Add("R06.1 uses an isolated fresh save. View and pose selections use the same session commands as the GUI; Escape, movement and harvesting use synthetic Input System devices. Captures and assertions are not human tone approval, audio listening, GUI mouse-click coverage or a low-spec performance benchmark.");
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Presentation run uses isolated evidence slot");
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionPresentationKeyboard");
            cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionPresentationMouse");
            syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled=false;
            Check(shop.World.Stations.Count==7 && shop.Customers.Ready,"Presentation retains seven stations and baked store navigation");
            yield return ResearchHarvest(CultureKind.Bloom);yield return ResearchHarvest(CultureKind.Flow);
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId,ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)),"Stage real harvested Bloom");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)),"Stage real harvested Flow");
            Ok(shop.Perform(shop.Model.BeginIncubation()),"Start sample Pondlet production");
            float started=Time.realtimeSinceStartup;
            yield return WaitFor(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,16,"sample ordinary-time growth");
            Check(Time.realtimeSinceStartup-started>=shop.Config.IncubationSeconds-.1f,"Pondlet matures through ordinary runtime time");
            Ok(shop.Perform(shop.Model.TransferToTank("tank-a")),"Put actual Pondlet in refined display");
            shop.World.Refresh(shop.Model);
            yield return SpecimenBounds("Refined Pondlet");
            LabRoutesCheck("Presentation additions");
            shop.SetPanel(ShopPanel.None);
            AimAt(new Vector3(1,.2f,-2.8f),new Vector3(5.5f,1.55f,-1.2f));
            yield return null;
            shop.SetPanel(ShopPanel.Pause);
            Vector3 originalPosition=shop.Player.View.transform.localPosition;
            Quaternion originalRotation=shop.Player.View.transform.localRotation;
            Vector3 playerPosition=shop.Player.transform.position;
            float originalFov=shop.Player.View.fieldOfView;
            Check(shop.SaveNow(),"Save before presentation preview");
            string originalDisk=File.ReadAllText(shop.SavePath);
            string originalModel=JsonUtility.ToJson(shop.Model.CaptureSave());
            string originalCycle=JsonUtility.ToJson(shop.Cycle.CaptureSave());
            shop.SetPanel(ShopPanel.Presentation);
            Check(shop.Paused && !shop.Player.InputEnabled && !shop.World.ToolRoot.gameObject.activeSelf,"Preview pauses shop, blocks movement and hides siphon");
            for(int i=0;i<5;i++)
            {
                shop.SelectPresentationView(i);yield return new WaitForSeconds(.25f);
                Check(shop.PresentationView==i && shop.Paused,"View selection remains paused / "+i);
                yield return Capture("210-sample-"+i);
            }
            var stage=shop.World.transform.Find("Look-only presentation stage");
            Check(stage && stage.GetComponentsInChildren<Collider>(true).Length==0 && stage.GetComponentsInChildren<Station>(true).Length==0,"Preview creates no physical obstacle or authoritative station");
            int renderers=0;
            foreach(var renderer in shop.World.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var material in renderer.sharedMaterials)
                    if(!material || !material.shader || !material.shader.isSupported)throw new InvalidOperationException("Invalid sample material on "+renderer.name);
                renderers++;
            }
            Check(renderers>0,"Every world/sample renderer has a supported material");
            shop.SelectPresentationView(4);
            for(int i=0;i<4;i++)
            {
                shop.SetPresentationPose((CharacterSamplePose)i);yield return new WaitForSeconds(.4f);
                Check(shop.PresentationPose==(CharacterSamplePose)i,"Employee pose selected / "+(CharacterSamplePose)i);
                yield return Capture("211-employee-"+((CharacterSamplePose)i).ToString().ToLowerInvariant());
            }
            shop.SetPresentationPose(CharacterSamplePose.Walk);yield return new WaitForSeconds(.2f);
            shop.SetPresentationMotion(false);yield return null;
            Transform employee=null;
            // Slash characters belong to the name; enumerate direct children instead of path lookup.
            foreach(Transform child in stage.Find("Turntable")) if(child.name.StartsWith("Employee study"))employee=child;
            Check(employee!=null,"Employee study is inspectable");
            var rig=employee.GetComponent<ShopCharacterVisual>();
            Quaternion frozen=rig.LeftLeg.localRotation;
            yield return new WaitForSeconds(.3f);
            Check(Quaternion.Angle(frozen,rig.LeftLeg.localRotation)<.001f,"Freeze motion stops the sample rig");
            shop.SetPresentationMotion(true);yield return new WaitForSeconds(.3f);
            Check(Quaternion.Angle(frozen,rig.LeftLeg.localRotation)>.05f,"Motion resumes after freeze");
            Check(originalModel==JsonUtility.ToJson(shop.Model.CaptureSave()) && originalCycle==JsonUtility.ToJson(shop.Cycle.CaptureSave()) && originalDisk==File.ReadAllText(shop.SavePath),"Five views and all poses leave model, clock and disk byte-equivalent");
            Check(Vector3.Distance(playerPosition,shop.Player.transform.position)<.001f,"Preview does not teleport physical player");
            yield return CycleTap(Key.Escape);
            Check(shop.Panel==ShopPanel.Pause && shop.Paused && !stage.gameObject.activeSelf,"Real Escape returns to pause and hides study objects");
            Check(Vector3.Distance(originalPosition,shop.Player.View.transform.localPosition)<.0001f && Quaternion.Angle(originalRotation,shop.Player.View.transform.localRotation)<.01f && shop.Player.View.fieldOfView==originalFov && shop.World.ToolRoot.gameObject.activeSelf,"Exit restores exact camera pose, FOV and tool");
            int modelCount=shop.World.GetComponentsInChildren<ShopCharacterVisual>(true).Length;
            for(int i=0;i<3;i++){shop.SetPanel(ShopPanel.Presentation);shop.SelectPresentationView(4);shop.SetPanel(ShopPanel.Pause);}
            Check(shop.World.GetComponentsInChildren<ShopCharacterVisual>(true).Length==modelCount,"Repeated preview entry reuses its models without growth");
            shop.SetPanel(ShopPanel.None);
            yield return EnterCounter();Check(shop.OpenForDay(),"Open actual shop after preview");
            var visit=Spawn(CustomerArchetypes.Casual);
            var actor=shop.Customers.ActorOf(visit);
            Check(actor.GetComponentInChildren<ShopCharacterVisual>()!=null,"Actual casual visitor uses the sample rig");
            yield return WaitFor(()=>visit.Phase==CustomerPhase.Queued,30,"sample customer browses and queues");
            AimAtCustomer(new Vector3(-2.0f,.2f,-4.0f));yield return Capture("212-live-customer");
            shop.SetPanel(ShopPanel.Pause);
            float patience=visit.QueueSecondsRemaining,clock=shop.Cycle.SecondsRemaining;
            Vector3 actorPosition=actor.transform.position;
            shop.SetPanel(ShopPanel.Presentation);shop.SelectPresentationView(4);
            yield return new WaitForSeconds(.6f);
            Check(Mathf.Abs(patience-visit.QueueSecondsRemaining)<.001f && Mathf.Abs(clock-shop.Cycle.SecondsRemaining)<.001f && Vector3.Distance(actorPosition,actor.transform.position)<.03f,"Preview freezes a real queued visitor and trading clock");
            shop.SetPanel(ShopPanel.None);yield return ServeNext("Sample visitor");
            Check(shop.Model.Currency==6 && !shop.Model.GetTank("tank-a").HasValue,"Refined visitor pays exactly six for the original Pondlet");
            shop.SetPanel(ShopPanel.None);yield return WaitForNoCustomer(15);
            Check(shop.CloseForNight(),"Close after sample sale");yield return WaitForNight(6);
            Check(shop.Reports.Last.sales==1 && shop.Reports.Last.revenue==6 && shop.SaveNow() && shop.ReloadSaved(),"Sample sale survives normal closing and reload");
            shop.SetPanel(ShopPanel.None);
            Check(shop.World.Stations.Count==7 && shop.Customers.Ready,"Reload preserves station registry and customer routes");
        }
    }
}
#endif
