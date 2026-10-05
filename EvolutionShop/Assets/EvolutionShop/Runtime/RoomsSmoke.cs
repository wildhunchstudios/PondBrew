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
        IEnumerator RoomsScenario()
        {
            limitations.Add("R06.5v4 isolated room/door route uses diagnostic camera teleports and synthetic keyboard walking. Expansion is a labelled 1000-credit fixture. Actual normal-HUD captures and collision/nav assertions do not establish human art approval, native GUI click coverage, every layout or measured performance.");
            yield return new WaitForSeconds(1);
            previousInputSettings=InputSystem.settings; smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionRoomsKeyboard");
            cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionRoomsMouse");
            syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled=false;
            var door=shop.World.LabDoor;
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Rooms scenario uses only isolated save");
            Check(door && door.GetComponentsInChildren<Collider>().Length==2 && door.GetComponentsInChildren<Station>().Length==0,"Door has two solid leaves and no transaction/station authority");
            Check(door.GetComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild,"Moving door is excluded from static customer bake");
            int bakes=shop.Customers.Navigation.BuildCount;
            AimAt(new Vector3(0,.2f,-3.8f),new Vector3(0,1.7f,.5f));
            yield return new WaitForSeconds(1.6f);
            Check(door.OpenFraction==0 && DoorRayHits(-1) && DoorRayHits(1),"Closed opaque door blocks both directional interaction rays");
            foreach(var renderer in door.GetComponentsInChildren<MeshRenderer>())
                foreach(var material in renderer.sharedMaterials)
                    Check(renderer.GetComponent<TextMesh>() || (material && material.renderQueue<3000),"Door geometry is opaque; labels retain glyph alpha / "+material.name);
            foreach(var renderer in door.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.GetComponent<TextMesh>()) continue;
                Bounds b=renderer.bounds;
                Check(b.min.z>=.39f-.001f && b.max.z<=.61f+.001f,"Moving door geometry stays within wall pocket / "+renderer.name);
            }
            foreach(string name in new[]{"Aquatic shop room identity","Aquarium display wall","Working laboratory interior"})
            {
                var art=shop.World.transform.Find(name);
                Check(art && art.GetComponentsInChildren<Collider>().Length==0 && art.GetComponentsInChildren<Station>().Length==0,"Room decoration has no collision/stock authority / "+name);
            }
            yield return Capture("260-night-door-closed");

            shop.Player.Teleport(new Vector3(0,.2f,-1.60f),Quaternion.identity);
            yield return WaitFor(()=>door.OpenFraction>0 && door.OpenFraction<1,1,"partial door opening");
            shop.SetPanel(ShopPanel.Pause);
            float paused=door.OpenFraction;
            yield return new WaitForSeconds(.35f);
            Check(Mathf.Abs(door.OpenFraction-paused)<.0001f,"Pause freezes partially opening door");
            shop.SetPanel(ShopPanel.None);
            yield return WaitFor(()=>door.OpenFraction==1,2,"resume door opening");
            Check(!DoorRayHits(-1),"Fully open door leaves central ray unobstructed");
            GateAim(new Vector3(0,.05f,-1.55f),new Vector3(0,1.8f,3.7f));
            yield return GateCapture("267-lab-door-open");
            editedWalkingMetres=0;editedDoorCrossings=0;
            shop.Player.Teleport(new Vector3(0,.2f,-3.8f),Quaternion.identity);
            yield return EditedWalkTo(new Vector2(0,.5f),"automatic lab door threshold");
            yield return new WaitForSeconds(1.4f);
            Check(door.OpenFraction==1,"Standing in doorway holds both leaves fully open");
            yield return EditedWalkTo(new Vector2(0,3f),"back lab beyond sensor");
            yield return new WaitForSeconds(1.5f);
            Check(door.OpenFraction==0 && DoorRayHits(1),"Door closes after player clears into lab");
            yield return EditedWalkTo(new Vector2(0,-3.8f),"front store return");
            yield return new WaitForSeconds(1.5f);
            Check(editedDoorCrossings==2 && editedWalkingMetres>11 && door.OpenFraction==0,"Real keyboard/controller crosses both ways and door closes behind");

            shop.AllowValidationInput=false;
            shop.Player.Teleport(new Vector3(0,.2f,.5f),Quaternion.identity);
            yield return WaitFor(()=>door.OpenFraction==1,2,"hold open before reversal");
            shop.Player.Teleport(new Vector3(0,.2f,-3.8f),Quaternion.identity);
            shop.SetPanel(ShopPanel.Pause);
            yield return new WaitForSeconds(1.3f);
            Check(door.OpenFraction==1,"Pause freezes hold-open timer after player leaves");
            shop.SetPanel(ShopPanel.None);
            yield return new WaitForSeconds(.30f);
            Check(door.OpenFraction==1,"Remaining clear delay resumes instead of elapsing during pause");
            yield return WaitFor(()=>door.OpenFraction<.85f && door.OpenFraction>.1f,2,"door starts closing");
            shop.Player.Teleport(new Vector3(0,.2f,-1.3f),Quaternion.identity);
            yield return WaitFor(()=>door.OpenFraction==1,2,"reapproach reverses closing");
            Check(shop.Customers.Navigation.BuildCount==bakes,"Opening/closing never rebakes customer navigation");
            LabRoutesCheck("Automatic door open");
            yield return RoomsViews("night");
            yield return EnterCounter();Check(shop.OpenForDay(),"Open actual shop for daytime room views");
            yield return RoomsViews("day");
            yield return EnterCounter(); Check(shop.CloseForNight(),"Close shop before preparation-only expansion");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"return to preparation");
            shop.SetPanel(ShopPanel.None);
            Check(shop.SaveNow(),"Save before labelled expansion fixture");
            var fixture=JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));fixture.game.currency=1000;
            Check(SaveStore.Write(shop.SavePath,fixture,out var error)&&shop.ReloadSaved(),"Load labelled room-expansion fixture / "+error);
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion() && shop.BuyLabExpansion(),"Both lab expansions retain room separation");
            Check(shop.OpenPlacementMenu() && shop.BuyStoreExpansion() && shop.BuyStoreExpansion(),"Both store expansions retain room separation");
            shop.SetPanel(ShopPanel.None);
            Check(shop.World.GetComponentsInChildren<ShopRoomDoor>().Length==1 && Vector3.Distance(door.transform.position,new Vector3(0,0,.5f))<.001f,"Expansion preserves exactly one fixed door");
            LabRoutesCheck("Both expanded rooms");
            GateAim(new Vector3(-7.8f,.05f,-4.8f),new Vector3(-11.5f,1.8f,-.3f));yield return GateCapture("270-expanded-aquatic-gallery");
            GateAim(new Vector3(0,.05f,6.1f),new Vector3(-3,2f,12.7f));yield return GateCapture("271-expanded-science-lab");
            int money=shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==money && shop.Layout.StoreTier==2 && shop.Layout.LabTier==2,"Reload preserves expanded rooms and exact money");
            Check(shop.World.GetComponentsInChildren<ShopRoomDoor>().Length==1,"Reload does not duplicate door or room ownership");
            shop.SetPanel(ShopPanel.None);shop.Player.Teleport(new Vector3(0,.2f,-3.8f),Quaternion.identity);
            yield return EditedWalkTo(new Vector2(0,2.4f),"expanded-room doorway");
            Check(door.OpenFraction==1,"Door continues functioning after expansion/reload");
        }

        bool DoorRayHits(int side)
        {
            var point=new Vector3(0,1.6f,.5f+side*1.5f);
            return Physics.Raycast(point,Vector3.forward*-side,out var hit,3f,~0,QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ShopRoomDoor>()==shop.World.LabDoor;
        }

        IEnumerator RoomsViews(string phase)
        {
            shop.SetPanel(ShopPanel.None);
            Check(shop.Panel==ShopPanel.None,"Room screenshots have gameplay HUD without a modal / "+phase);
            GateAim(new Vector3(0,.05f,-4.85f),new Vector3(0,1.9f,.5f));
            yield return new WaitForSeconds(1.6f);yield return GateCapture("261-"+phase+"-aquatic-store");
            GateAim(new Vector3(-2.6f,.05f,-4.5f),new Vector3(4.3f,1.8f,-1f));yield return GateCapture("262-"+phase+"-aquatic-displays");
            GateAim(new Vector3(2.3f,.05f,-.3f),new Vector3(-4.2f,1.8f,-4.4f));yield return GateCapture("263-"+phase+"-aquatic-supplies");
            GateAim(new Vector3(.6f,.05f,1.35f),new Vector3(-1.4f,1.8f,4.7f));yield return GateCapture("264-"+phase+"-working-lab");
            GateAim(new Vector3(4.5f,.05f,2.2f),new Vector3(-3f,1.9f,1f));
            yield return new WaitForSeconds(1.6f);yield return GateCapture("265-"+phase+"-lab-separation");
            GateAim(new Vector3(-1f,.05f,2.4f),new Vector3(5.4f,1.8f,4.6f));yield return GateCapture("266-"+phase+"-lab-equipment");
        }
    }
}
#endif
