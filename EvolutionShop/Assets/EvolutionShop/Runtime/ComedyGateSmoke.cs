#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // R06.4 exercises production entry points. Fixture preparation and cosmetic cooldown
        // expiry are explicit; economic assertions retain exact IDs and accepted prices.
        IEnumerator ComedyGateScenario()
        {
            limitations.Add("R06.4 isolated save and original room layout. Every fresh fixture explicitly skips the paused tutorial Welcome through the normal control. Stock preparation, pending-boundary checks and cosmetic cooldown expiry use explicit model/session commands; visitors and interruptions use real runtime frames. E/Escape/Backspace are synthetic Input System input. Menu selections use application commands. No human audio listening, GUI click pass, performance, all-layout or worker-job approval.");
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Interruption gate isolates the normal save");
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("Comedy gate keyboard");syntheticKeyboard.MakeCurrent();
            cycleMouse=InputSystem.AddDevice<Mouse>("Comedy gate mouse");cycleMouse.MakeCurrent();shop.AllowValidationInput=true;
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;
            var sound=shop.GetComponent<ShopAudio>();

            // A disabled preview owner must relinquish camera/stage/audio ownership.
            shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow(),"Save before interrupted worker preview");
            string disk=File.ReadAllText(shop.SavePath),model=JsonUtility.ToJson(shop.Model.CaptureSave()),cycle=JsonUtility.ToJson(shop.Cycle.CaptureSave());
            Vector3 cameraPosition=shop.Player.View.transform.localPosition;
            Quaternion cameraRotation=shop.Player.View.transform.localRotation;
            float fov=shop.Player.View.fieldOfView;bool tool=shop.World.ToolRoot.gameObject.activeSelf;
            shop.OpenComedyPreview();Check(shop.PlayComedyPreview(ComedyKind.WorkerCelebration),"Worker fixture starts for disable test");
            yield return new WaitForSeconds(.65f);
            var stage=shop.World.transform.Find("Look-only presentation stage");
            shop.enabled=false;yield return null;
            Check(shop.Panel==ShopPanel.Pause && !shop.ComedyPreview && !shop.PreviewComedy.Active && !stage.gameObject.activeSelf && !sound.ComedyPlaying,"Disabling session exits preview and clears stage, caption and voice");
            Check(Vector3.Distance(cameraPosition,shop.Player.View.transform.localPosition)<.0001f && Quaternion.Angle(cameraRotation,shop.Player.View.transform.localRotation)<.01f && shop.Player.View.fieldOfView==fov && shop.World.ToolRoot.gameObject.activeSelf==tool,"Disabled preview restores original camera and tool");
            shop.OpenComedyPreview();
            Check(shop.Panel==ShopPanel.Pause && !shop.PlayComedyPreview(ComedyKind.Fart),"Disabled session rejects stale preview commands");
            Check(model==JsonUtility.ToJson(shop.Model.CaptureSave()) && cycle==JsonUtility.ToJson(shop.Cycle.CaptureSave()) && disk==File.ReadAllText(shop.SavePath),"Preview disable changes no model, clock or saved bytes");
            shop.enabled=true;shop.OpenComedyPreview();Check(shop.PlayComedyPreview(ComedyKind.WorkerCelebration),"Re-enabled session can preview again");
            yield return MoveFor(Key.Escape,.05f);Check(shop.Panel==ShopPanel.Pause && !stage.gameObject.activeSelf,"Recovered preview exits through real Escape");
            shop.SetPanel(ShopPanel.None);

            foreach(var kind in new[]{ComedyKind.Fart,ComedyKind.Complaint,ComedyKind.Mimic})
            {
                GateFresh();SeedStock("pondlet","tank-a");SeedStock("bubblebelly","tank-b");
                yield return EnterCounter();Check(shop.OpenForDay(),"Open interruption day / "+kind);shop.SetPanel(ShopPanel.None);
                var first=Spawn(CustomerArchetypes.Casual);var second=Spawn(CustomerArchetypes.Casual);
                yield return WaitFor(()=>first.Phase==CustomerPhase.Queued && second.Phase==CustomerPhase.Queued,35,"two queued visitors / "+kind);
                var actor=shop.Customers.ActorOf(first);
                string owned=GateStock();
                GateAim(new Vector3(-.6f,.2f,-1.2f),actor.transform.position+Vector3.up*1.35f);
                foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Journal,ShopPanel.Counter})
                {
                    yield return GateStart(kind);
                    yield return new WaitForSeconds(.65f);
                    if(panel==ShopPanel.Pause && kind==ComedyKind.Complaint)yield return GateCapture("230-live-queue-complaint");
                    float clock=shop.Cycle.SecondsRemaining,patience=first.QueueSecondsRemaining;
                    if(panel==ShopPanel.Pause)yield return MoveFor(Key.Escape,.05f);else shop.SetPanel(panel);
                    Check(shop.Panel==panel && !shop.Comedy.Active && !sound.ComedyPlaying && shop.Comedy.Caption==string.Empty,"Panel cancels presentation / "+kind+" / "+panel);
                    float pausedClock=shop.Cycle.SecondsRemaining,pausedPatience=first.QueueSecondsRemaining;
                    Vector3 position=actor.transform.position;
                    yield return new WaitForSeconds(.2f);
                    Check(GateStock()==owned && first.Phase==CustomerPhase.Queued && second.Phase==CustomerPhase.Queued,"Panel retains exact two reservations and money / "+kind+" / "+panel);
                    if(panel==ShopPanel.Pause)Check(shop.Cycle.SecondsRemaining==pausedClock && first.QueueSecondsRemaining==pausedPatience && Vector3.Distance(position,actor.transform.position)<.001f,"Pause freezes queue and clock / "+kind);
                    else Check(shop.Cycle.SecondsRemaining<clock && first.QueueSecondsRemaining<patience,"Ordinary panel keeps trading time active / "+kind+" / "+panel);
                    shop.SetPanel(ShopPanel.None);
                    Check(!shop.TryStartComedy(kind),"Panel interruption retains quiet time / "+kind+" / "+panel);
                }
                if(kind==ComedyKind.Fart)
                {
                    foreach(bool wholeObject in new[]{false,true})
                    {
                        yield return GateStart(kind);yield return new WaitForSeconds(.65f);
                        if(wholeObject)actor.gameObject.SetActive(false);else actor.enabled=false;
                        yield return null;yield return null;
                        var body=actor.transform.Find("Visitor appearance");Quaternion afterDisable=body.localRotation;
                        Check(!shop.Comedy.Active && !sound.ComedyPlaying,"Disabled visitor cancels active gag / object="+wholeObject);
                        actor.SetComedyPose(kind,.8f,false);
                        yield return new WaitForSeconds(.2f);
                        Check(Quaternion.Angle(afterDisable,body.localRotation)<.001f && GateStock()==owned,"Disabled actor rejects late pose writes without touching reservations / object="+wholeObject);
                        if(wholeObject)actor.gameObject.SetActive(true);else actor.enabled=true;
                        yield return null;
                    }
                    yield return GateStart(kind);shop.enabled=false;yield return null;
                    Check(!shop.Comedy.Active && !sound.ComedyPlaying && !shop.TryStartComedy(kind) && GateStock()==owned,"Session disable clears live gag and rejects replay");
                    shop.enabled=true;yield return null;
                }
                if(kind==ComedyKind.Mimic)
                {
                    var tank=StationOf(StationKind.TankA).transform;
                    GateAim(new Vector3(2.5f,.2f,-1.45f),tank.TransformPoint(new Vector3(0,1.55f,0)));
                    yield return GateStart(kind);yield return new WaitForSeconds(.65f);
                    Check(shop.World.ValidateTankBounds(out _),"Live imitation remains inside stocked tank");
                    yield return GateCapture("231-live-fish-imitation");
                }
                yield return GateStart(kind);
                yield return MoveFor(Key.Backspace,.05f);
                Check(!shop.Comedy.Active && !sound.ComedyPlaying && GateStock()==owned,"Real skip clears gag and keeps both orders / "+kind);
                yield return GateStart(kind);
                var served=shop.Customers.NextToServe;var unpaid=served==first?second:first;
                yield return ServeNext("Interrupted "+kind+" service");
                Check(!shop.Comedy.Active && !sound.ComedyPlaying && !shop.Model.GetTank(served.TargetTankId).HasValue && served.Outcome==CustomerOutcome.Sold,"Service owns the sold specimen and cancels gag / "+kind);
                int cash=shop.Model.Currency;string survivor=shop.Model.GetTank(unpaid.TargetTankId).Value.Id;
                shop.SetPanel(ShopPanel.None);
                yield return GateStart(kind);
                Check(shop.CloseForNight() && !shop.Comedy.Active && !shop.TryStartComedy(kind),"Closing interrupts and forbids ambient retrigger / "+kind);
                Check(shop.Customers.ActiveCount<=2,"Closing keeps the two-visitor bound / "+kind);
                yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"interrupted two-visitor closing");
                Check(shop.Model.Currency==cash && shop.Model.GetTank(unpaid.TargetTankId).Value.Id==survivor && !shop.Model.GetReservation(unpaid.TargetTankId).HasValue && shop.Reports.Last.sales==1,"Closing retains unpaid stock and exactly one committed sale / "+kind);
            }

            // Explicit owner departure/removal cannot leave late presentation callbacks behind.
            foreach(var interruption in new[]{"cancel","remove","reset"})
            {
                GateFresh();SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Open target interruption / "+interruption);shop.SetPanel(ShopPanel.None);
                var visitor=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>visitor.Phase==CustomerPhase.Queued,30,"target interruption reservation");
                string preserved=shop.Model.GetTank("tank-a").Value.Id;
                yield return GateStart(ComedyKind.Mimic);yield return new WaitForSeconds(.6f);
                if(interruption=="cancel")visitor.Cancel(CustomerOutcome.Impatient);
                else if(interruption=="remove")shop.Customers.ResetVisitors();
                else shop.ConfirmNewGame();
                yield return null;yield return null;
                Check(!shop.Comedy.Active && !sound.ComedyPlaying,"Target interruption clears presentation / "+interruption);
                Check(shop.Model.Currency==0 && !shop.Model.GetReservation("tank-a").HasValue && (interruption=="reset"?!shop.Model.GetTank("tank-a").HasValue:shop.Model.GetTank("tank-a").Value.Id==preserved),"Target interruption releases without payment or unintended stock loss / "+interruption);
            }

            // Active queue reload must release transient reservations, never replay sale/gag.
            GateFresh();SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Open reload fixture");shop.SetPanel(ShopPanel.None);
            var reloaded=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>reloaded.Phase==CustomerPhase.Queued,30,"reload reservation");
            yield return GateStart(ComedyKind.Complaint);
            string id=shop.Model.GetTank("tank-a").Value.Id;int reloadCash=shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved(),"Save and reload during active queued complaint");yield return null;
            Check(!shop.Comedy.Active && !sound.ComedyPlaying && shop.Customers.ActiveCount==0 && shop.Model.GetTank("tank-a").Value.Id==id && shop.Model.Currency==reloadCash && !shop.Model.GetReservation("tank-a").HasValue,"Reload releases unpaid reservation without stock loss, payment or gag replay");
            string ledger=JsonUtility.ToJson(shop.Reports.CaptureSave()),reputation=JsonUtility.ToJson(shop.Reputation.CaptureSave());
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==reloadCash && shop.Model.GetTank("tank-a").Value.Id==id && ledger==JsonUtility.ToJson(shop.Reports.CaptureSave()) && reputation==JsonUtility.ToJson(shop.Reputation.CaptureSave()),"Repeated reload preserves stock, money, ledger and reputation exactly");
            var recovery=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>recovery.Phase==CustomerPhase.Queued,30,"restored stock can sell");
            yield return ServeNext("Post-reload recovery");
            Check(shop.Model.Currency==reloadCash+recovery.Price && !shop.Model.GetTank("tank-a").HasValue && shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==recovery.Price,"Recovered stock pays and records one sale/revenue exactly once");

            // Completion remains authoritative across active and pending joke cancellation.
            GateFresh();GateMature();
            var incubator=StationOf(StationKind.Incubator).transform;
            GateAim(incubator.TransformPoint(new Vector3(0,.2f,-2.6f)),incubator.TransformPoint(new Vector3(0,1.7f,0)));
            yield return WaitFor(()=>shop.Comedy.Active && shop.Comedy.Kind==ComedyKind.Belch,6,"natural delayed belch after completion");
            yield return new WaitForSeconds(.6f);
            var bubble=GateBubble();
            Check(bubble && bubble.gameObject.activeSelf && bubble.GetComponent<Collider>()==null,"Live incubator bubble is visible and nonphysical");
            yield return GateCapture("232-live-incubator-belch");
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready,"Readiness stays available during belch");
            string readySpecimen=shop.Model.Incubator.SpecimenId;
            Ok(shop.TransferToDisplay("tank-a"),"Collect while belch is active");string collected=shop.Model.GetTank("tank-a").Value.Id;
            yield return null;yield return null;
            Check(!shop.Comedy.Active && !bubble.gameObject.activeSelf && !sound.ComedyPlaying,"Collection clears active belch caption, bubble and voice");
            Check(!shop.TransferToDisplay("tank-b").Success && collected==readySpecimen && shop.Model.GetTank("tank-a").Value.Id==collected,"Repeated collection cannot duplicate the ready specimen");

            foreach(var interruption in new[]{"pause","reload","reset","disable"})
            {
                GateFresh();GateMature();string activeId=shop.Model.Incubator.SpecimenId;yield return GateStart(ComedyKind.Belch);yield return new WaitForSeconds(.6f);
                if(interruption=="pause")yield return MoveFor(Key.Escape,.05f);
                if(interruption=="reload")Check(shop.SaveNow() && shop.ReloadSaved(),"Reload active belch");
                if(interruption=="reset")shop.ConfirmNewGame();
                if(interruption=="disable")shop.enabled=false;
                yield return null;yield return null;
                Check(!shop.Comedy.Active && !bubble.gameObject.activeSelf && !sound.ComedyPlaying,"Active belch cleanup / "+interruption);
                Check(shop.Model.Currency==0 && shop.Model.Incubator.Phase==(interruption=="reset"?IncubatorPhase.Dosing:IncubatorPhase.Ready) && (interruption=="reset" || shop.Model.Incubator.SpecimenId==activeId),"Belch interruption keeps economic authority / "+interruption);
                shop.enabled=true;shop.SetPanel(ShopPanel.None);int activeCount=shop.ComedyStartedCount;shop.Advance(4.2f);yield return null;
                Check(!shop.Comedy.Active && shop.ComedyStartedCount==activeCount,"Active belch interruption leaves no delayed replay / "+interruption);
            }

            foreach(var interruption in new[]{"panel","collect","closing","reload","reset","disable"})
            {
                GateFresh();GateMature();string pendingId=shop.Model.Incubator.SpecimenId;
                Check(!shop.Comedy.Active && shop.Model.Incubator.Phase==IncubatorPhase.Ready,"Readiness precedes pending gag / "+interruption);
                if(interruption=="panel"){shop.SetPanel(ShopPanel.Incubator);shop.SetPanel(ShopPanel.None);}
                if(interruption=="collect")Ok(shop.TransferToDisplay("tank-a"),"Collect before delayed belch");
                if(interruption=="closing"){shop.Advance(3.2f);Check(shop.OpenForDay() && shop.CloseForNight(),"Close during pending belch");}
                if(interruption=="reload")Check(shop.SaveNow() && shop.ReloadSaved(),"Reload pending belch");
                if(interruption=="reset")shop.ConfirmNewGame();
                if(interruption=="disable"){shop.enabled=false;shop.enabled=true;}
                int pendingCount=shop.ComedyStartedCount;shop.Advance(interruption=="closing"?1f:4.2f);shop.SetPanel(ShopPanel.None);yield return null;
                Check(!shop.Comedy.Active && shop.ComedyStartedCount==pendingCount && !sound.ComedyPlaying,"Canceled pending belch never replays / "+interruption);
                Check(shop.Model.Currency==0 && (interruption=="reset"?shop.Model.Incubator.Phase==IncubatorPhase.Dosing:interruption=="collect"?shop.Model.GetTank("tank-a").HasValue && shop.Model.GetTank("tank-a").Value.Id==pendingId:shop.Model.Incubator.Phase==IncubatorPhase.Ready && shop.Model.Incubator.SpecimenId==pendingId),"Pending interruption preserves readiness/collected stock or explicit reset / "+interruption);
            }
            GateFresh();GateMature();shop.ReducedGags=true;shop.HumorVolume=0;
            yield return WaitFor(()=>shop.Comedy.Active,6,"muted reduced belch");yield return new WaitForSeconds(.2f);
            bubble=GateBubble();
            Check(shop.Comedy.Reduced && shop.Comedy.Caption.Length>0 && (!bubble || !bubble.gameObject.activeSelf) && sound.LastComedyGain==0 && sound.ActiveVoiceCount<=1,"Reduced muted readiness retains caption without bubble and with zero comedy gain");
            shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow() && shop.ReloadSaved() && !shop.Comedy.Active,"Final paused save/reload retains no incident");
        }

        Transform GateBubble()
        {
            // Slash is part of the authored name, not a hierarchy path.
            foreach(Transform child in shop.World.transform)if(child.name=="Comic bubble / no stock")return child;
            return null;
        }
        void GateAim(Vector3 position,Vector3 target)
        {
            Vector3 direction=target-position;direction.y=0;
            shop.Player.Teleport(position,Quaternion.LookRotation(direction));
            shop.Player.View.transform.LookAt(target);
        }
        IEnumerator GateCapture(string name)
        {
            // Freeze only diagnostic look input while the ordinary simulation continues.
            bool input=shop.AllowValidationInput;
            shop.AllowValidationInput=false;shop.Player.InputEnabled=false;
            try{yield return Capture(name);}
            finally{shop.AllowValidationInput=input;}
        }
        void GateFresh()
        {
            shop.enabled=true;shop.SetPanel(ShopPanel.None);shop.ConfirmNewGame();
            // New Game now offers the paused tutorial Welcome. This older regression is explicitly unguided.
            Check(shop.SkipTutorial() && !shop.Paused && shop.Panel==ShopPanel.None, "Unguided comedy fixture exits Welcome through the normal skip control");
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;
            shop.ReducedGags=false;shop.HumorVolume=.65f;shop.Comedy.Tick(120);
        }
        IEnumerator GateStart(ComedyKind kind)
        {
            shop.SetPanel(ShopPanel.None);shop.CancelComedy();shop.Comedy.Tick(120);
            yield return new WaitForSeconds(.85f);
            Check(shop.TryStartComedy(kind),"Start eligible incident / "+kind);
        }
        void GateMature()
        {
            shop.Model.Tick(60);Ok(shop.Model.Collect(CultureKind.Bloom),"Completion fixture Bloom");Ok(shop.Model.Collect(CultureKind.Flow),"Completion fixture Flow");
            Ok(shop.Model.Dose(CultureKind.Bloom),"Completion dose Bloom");Ok(shop.Model.Dose(CultureKind.Flow),"Completion dose Flow");
            Ok(shop.Model.BeginIncubation(),"Completion fixture incubation");shop.Advance(shop.Config.IncubationSeconds);
        }
        string GateStock()
        {
            var result=new StringBuilder().Append(shop.Model.Currency);
            foreach(var tank in shop.Model.TankIds)
            {
                var specimen=shop.Model.GetTank(tank);var reservation=shop.Model.GetReservation(tank);
                result.Append('|').Append(tank).Append(':').Append(specimen.HasValue?specimen.Value.Id:"empty");
                if(reservation.HasValue)result.Append(':').Append(reservation.Value.CustomerId).Append(':').Append(reservation.Value.SpecimenId).Append(':').Append(reservation.Value.SpeciesId).Append(':').Append(reservation.Value.Price);
                else result.Append(":free");
            }
            return result.ToString();
        }
    }
}
#endif
