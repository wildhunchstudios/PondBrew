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
        void DoseAim()
        {
            var station=StationOf(StationKind.Incubator).transform;
            GateAim(station.TransformPoint(new Vector3(0,.05f,-2.15f)),station.TransformPoint(new Vector3(0,1.5f,-.1f)));
            Physics.SyncTransforms();shop.SetPanel(ShopPanel.Incubator);
        }
        IEnumerator FxDoseScenario()
        {
            limitations.Add("FX-06R-03: real guarded doses/returns/clears/recipe preparation with synthetic held Bloom/Flow collection. Actual10s discovery unlocks known recipe. World/Low/reduced are labeled cosmetic replays of the same bounded recipe. Late four-culture coverage uses frozen progressed-layout fixture and model collection for setup only. Videos are actual silent HUD recordings; no native controls/listening/GPU pass.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Dose uses exact isolated save");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionDoseKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionDoseMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            if(!Application.isEditor)
            {int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w && Screen.height==h,"Exact actual dose HUD resolution");}
            yield return FxLabHarvest(CultureKind.Bloom,false);yield return FxLabHarvest(CultureKind.Bloom,false);yield return FxLabHarvest(CultureKind.Flow,false);DoseAim();
            var world=shop.World;var pool=world.FxPool;var audio=shop.GetComponent<ShopAudio>();int eventIndex=0;
            yield return OrganicClip("measured-dose-return-refusal",3.8f,age=>
            {
                if(eventIndex==0 && age>=.35f){Check(shop.DoseCulture(CultureKind.Bloom),"Recorded real Bloom dose");eventIndex++;}
                if(eventIndex==1 && age>=1.1f){Check(shop.DoseCulture(CultureKind.Flow),"Recorded real Flow dose");eventIndex++;}
                if(eventIndex==2 && age>=1.85f){string state=FxAuthority();int sounds=audio.LabPlaybackCount;Check(!shop.DoseCulture(CultureKind.Flow) && FxAuthority()==state && audio.LabPlaybackCount==sounds && world.LabDoseId==FxCueId.HandRefused,"Empty culture refuses without stock delta or accepted sound");eventIndex++;}
                if(eventIndex==3 && age>=2.35f){Check(shop.ReturnCultureDose(CultureKind.Bloom) && world.LabDoseMode==FxDoseMode.Return,"Recorded real outward return");eventIndex++;}
                if(eventIndex==4 && age>=3.05f){Check(shop.ClearCultureDoses() && world.LabDoseMode==FxDoseMode.Clear,"Recorded safe clear uses outward cue");eventIndex++;}
            });
            Check(eventIndex==5 && shop.Model.Available(CultureKind.Bloom)==2 && shop.Model.Available(CultureKind.Flow)==1 && FxDoseMix.Read(shop.Model).Total==0,"Recorded sequence conserves all three actual units");
            Check(!world.HandsVisual.gameObject.activeSelf,"No operating hands through the dose panel");
            Check(!shop.PrepareKnownRecipe("pondlet"),"Undiscovered known recipe cannot fake a pour");yield return new WaitForSeconds(.3f);
            yield return FxLabHarvest(CultureKind.Flow,false);DoseAim();Check(shop.Model.ReservedCultureCount==shop.Model.Capacity,"Four-unit tool is actually full");
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow) && shop.Model.ReservedCultureCount==4,"Fast accepted doses coalesce without changing reserved capacity");
            yield return new WaitForSeconds(.10f);Check(pool.ActiveCount<=16 && world.LabDoseMixture.Total==2,"One bounded current dose and true mixture under rapid input");
            Check(shop.BeginManualIncubation(),"Normal known-recipe unlock batch starts");yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"normal ten-second recipe discovery");
            string specimen=shop.Model.Incubator.SpecimenId;yield return LabTransferReady("tank-a",specimen);DoseAim();
            eventIndex=0;
            yield return OrganicClip("known-recipe-and-clear",3.2f,age=>
            {
                if(eventIndex==0 && age>=.35f){Check(shop.PrepareKnownRecipe("pondlet") && world.LabDoseMode==FxDoseMode.Recipe && world.LabDoseMixture.Total==2,"Actual discovered recipe measures exactly collected pair");eventIndex++;}
                if(eventIndex==1 && age>=1.15f){Check(shop.ClearCultureDoses() && world.LabDoseMixture.Total==2,"Clear remembers both returned cultures");eventIndex++;}
                if(eventIndex==2 && age>=1.95f){Check(shop.PrepareKnownRecipe("pondlet"),"Repeated recipe command remains available");eventIndex++;}
            });
            Check(FxDoseMix.Read(shop.Model).Total==2 && shop.Model.ReservedCultureCount==2,"Known preparation conserves exact pair");
            Check(shop.SaveNow() && shop.ReloadSaved(),"Reload actual staged recipe");DoseAim();Check(world.LabDoseRemaining==0 && pool.ActiveCount==0,"Staged reload reconstructs mixture with no replay");
            // Same actual staged mixture; visual replay only, without hidden commands or inventory effects.
            shop.SetPanel(ShopPanel.None);shop.FreezeFxVocabularyForValidation=true;
            try
            {
                string state=FxAuthority();
                for(int comfort=0;comfort<3;comfort++)
                {
                    world.SetFxPreviewModes(comfort==2,comfort==1);world.ResetFxPresentation();eventIndex=0;
                    yield return OrganicClip(comfort==0?"world-dose-cosmetic":comfort==1?"low-dose-cosmetic":"reduced-dose-cosmetic",2.6f,age=>
                    {
                        if(eventIndex==0 && age>=.35f){Check(world.TryFxCue(FxCueId.DoseCommitted,1,FxCueOrigin.Manual,CultureKind.Bloom),"Labeled world Bloom replay");eventIndex++;}
                        if(eventIndex==1 && age>=1.2f){Check(world.TryFxCue(FxCueId.DoseCommitted,2,FxCueOrigin.Manual,CultureKind.Flow),"Labeled world Flow replay");eventIndex++;}
                        world.Animate(Time.time,Time.deltaTime);
                    });
                    Check(FxAuthority()==state,"World replay leaves authoritative recipe/stock unchanged");
                }
                world.SetFxPreviewModes(false,false);world.ResetFxPresentation();world.TryFxCue(FxCueId.DoseCommitted,1);world.Animate(Time.time,.2f);world.Animate(Time.time,.04f);VocabularyRasterAudit("dose-actual-mask");
            }
            finally {shop.FreezeFxVocabularyForValidation=false;world.SetFxPreviewModes(false,false);world.ResetFxPresentation();}
            DoseAim();Check(shop.ClearCultureDoses(),"Clear before persistence failure fixture");yield return new WaitForSeconds(.7f);Check(shop.SaveNow(),"Durable pre-dose state");byte[] durable=File.ReadAllBytes(shop.SavePath);int played=audio.LabPlaybackCount;
            TutorialBlockSave();
            try {Check(shop.DoseCulture(CultureKind.Bloom) && shop.SaveError!=null && world.LabDoseRemaining==0 && pool.ActiveCount==0 && audio.LabPlaybackCount==played,"Accepted model change with failed save has no success pour or sound");Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed dose preserves durable bytes");}
            finally {TutorialUnblockSave();}
            Check(shop.ReloadSaved(),"Recover saved carried stock after failure");DoseAim();
            foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Journal,ShopPanel.Arrange})
            {
                Check(shop.DoseCulture(CultureKind.Bloom),"Accepted dose before interruption / "+panel);yield return new WaitForSeconds(.09f);shop.SetPanel(panel);
                Check(world.LabDoseRemaining==0 && pool.ActiveCount==0,"Panel cancels live and delayed dose / "+panel);yield return new WaitForSeconds(.65f);Check(pool.ActiveCount==0,"Canceled dose never restarts");DoseAim();Check(shop.ReturnCultureDose(CultureKind.Bloom),"Return interruption input");
            }
            Check(shop.DoseCulture(CultureKind.Bloom),"Dose before focus loss");shop.SetFxFocusForValidation(false);Check(world.LabDoseRemaining==0 && pool.ActiveCount==0,"Focus loss cancels dose");shop.SetFxFocusForValidation(true);Check(shop.ClearCultureDoses(),"Recover focus fixture");
            Check(shop.DoseCulture(CultureKind.Bloom),"Dose before moving owner");var incubator=StationOf(StationKind.Incubator).transform;var position=incubator.position;incubator.position+=Vector3.right*.04f;world.Animate(Time.time,.01f);Check(world.LabDoseRemaining==0 && pool.ActiveCount==0,"Moved owner retires dose");incubator.position=position;world.ResetFxPresentation();
            // Explicit late-game setup. The immutable historical layout makes all four cultures reachable.
            string fixture=Path.Combine(Application.dataPath,"EvolutionShop","Tests","RuntimeEditMode","Fixtures","V15Conditioning.json");
            if(!Application.isEditor)fixture=Path.Combine(evidence,"seed-v15.json");
            var historical=JsonUtility.FromJson<SlotData>(File.ReadAllText(fixture));Check(ShopLayout.TryRestore(historical.layout,out var layout),"Read immutable progressed-layout fixture");
            var seed=new GameModel(layout.WithTankIds(shop.Config)).CaptureSave();seed.sparkResearchUnlocked=seed.mistResearchUnlocked=true;seed.discoveries=(string[])GateSpecies.Clone();foreach(var grower in seed.growers)grower.units=6;
            Check(SaveStore.Write(shop.SavePath,new SlotData{game=seed,layout=layout.CaptureSave(),cycle=new ShopDayCycle().CaptureSave()},out var error) && shop.ReloadSaved(),"Load labeled four-culture fixture / "+error);shop.Customers.ArrivalsEnabled=false;shop.SetFxFocusForValidation(true);
            for(int c=0;c<4;c++)Ok(shop.Model.Collect((CultureKind)c),"Fixture-only collection of one culture / "+c);shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save isolated four-culture input");DoseAim();eventIndex=0;
            yield return OrganicClip("four-culture-measurement",4.6f,age=>
            {
                if(eventIndex<4 && age>=.35f+eventIndex*.75f){Check(shop.DoseCulture((CultureKind)eventIndex),"Physical guarded late culture dose / "+eventIndex);eventIndex++;}
                if(eventIndex==4 && age>=3.5f){Check(FxDoseMix.Read(shop.Model).Total==4 && shop.Model.ReservedCultureCount==4 && !shop.BeginManualIncubation() && shop.Model.Incubator.Phase==IncubatorPhase.Dosing,"Unsupported four-culture mixture does not invent a recipe");Check(shop.ClearCultureDoses(),"Return full mixed siphon losslessly");eventIndex++;}
            });
            Check(eventIndex==5 && FxDoseMix.Read(shop.Model).Total==0 && shop.Model.ReservedCultureCount==4,"All four input cultures returned without overflow");
            Check(pool.OwnedCount==96 && pool.OwnedMeshCount==5 && pool.OwnedMaterialCount==1 && shop.GetComponents<AudioSource>().Length==3,"Dose adds no pool mesh/material/voice ownership");
            Check(shop.SaveNow(),"Final isolated dose state saves");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
