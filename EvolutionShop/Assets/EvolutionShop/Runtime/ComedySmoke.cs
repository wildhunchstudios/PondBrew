#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator ComedyScenario()
        {
            limitations.Add("R06.3 isolated save; real navigation, ordinary ambient wait, delayed reveal, synthetic E/F/Backspace/Escape. Preview/trigger buttons use session commands. Fixture stock/remaining cooldowns/one maturation use explicit accelerated domain time. Captures and PCM/playback checks are not listening, manual GUI clicks, balance or human tone approval. R06.4 broader interruption coverage remains separate.");
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Comedy run isolates the normal save");
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("Comedy keyboard");syntheticKeyboard.MakeCurrent();shop.AllowValidationInput=true;
            cycleMouse=InputSystem.AddDevice<Mouse>("Comedy mouse");cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled=false;
            SeedStock("pondlet","tank-a");SeedStock("bubblebelly","tank-b");
            var sound=shop.GetComponent<ShopAudio>();
            shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow(),"Save before comedy preview");
            string disk=File.ReadAllText(shop.SavePath), model=JsonUtility.ToJson(shop.Model.CaptureSave()),cycle=JsonUtility.ToJson(shop.Cycle.CaptureSave());
            var cameraPosition=shop.Player.View.transform.localPosition;var cameraRotation=shop.Player.View.transform.localRotation;
            float fov=shop.Player.View.fieldOfView;
            shop.OpenComedyPreview();
            Check(shop.ComedyPreview && shop.Paused && !shop.Player.InputEnabled,"Preview pauses shop and blocks movement");
            yield return new WaitForSeconds(1);
            for(int i=0;i<6;i++)
            {
                var kind=(ComedyKind)i;
                Check(shop.PlayComedyPreview(kind),"Preview starts / "+kind);
                int variant=shop.PreviewComedy.Variant;
                string setup=shop.PreviewComedy.Caption;
                yield return new WaitForSeconds(.65f);
                Check(shop.PreviewComedy.Active && shop.PreviewComedy.Caption!=setup,"Delayed caption payoff / "+kind);
                yield return Capture("220-comedy-"+kind);
                Check(shop.PlayComedyPreview(kind) && shop.PreviewComedy.Variant!=variant,"Alternate authored variation / "+kind);
                yield return MoveFor(Key.Backspace,.05f);
                Check(!shop.PreviewComedy.Active && !sound.ComedyPlaying,"Backspace stops preview sound/caption / "+kind);
            }
            Check(model==JsonUtility.ToJson(shop.Model.CaptureSave()) && cycle==JsonUtility.ToJson(shop.Cycle.CaptureSave()) && disk==File.ReadAllText(shop.SavePath),"All six previews leave model, clock and saved bytes untouched");
            var stage=shop.World.transform.Find("Look-only presentation stage");
            AuditPresentationContracts(stage);
            shop.StopComedyPreview();
            yield return MoveFor(Key.Escape,.05f);
            Check(shop.Panel==ShopPanel.Pause && !shop.ComedyPreview && Vector3.Distance(cameraPosition,shop.Player.View.transform.localPosition)<.0001f && Quaternion.Angle(cameraRotation,shop.Player.View.transform.localRotation)<.01f && fov==shop.Player.View.fieldOfView,"Exit restores exact viewpoint and ends preview");

            // Inspect real AudioSource behavior without pretending this is a listening pass.
            yield return new WaitForSeconds(1);
            Check(sound.PlayComedy(ComedyKind.Fart,0,.4f),"Comedy voice accepts a valid gain");
            Check(sound.ComedyPlaying && Mathf.Abs(sound.LastComedyGain-.4f)<.001f,"Humor gain applies to dedicated voice");
            Check(!sound.PlayComedy(ComedyKind.Complaint,1,1),"A second comedy voice cannot stack");
            sound.Discovery();
            Check(!sound.ComedyPlaying && sound.ServicePlaying && sound.ServicePriorityActive,"Discovery interrupts comedy and reserves service priority");
            Check(!sound.PlayComedy(ComedyKind.Belch,0,1),"Comedy cannot mask the completion cue");
            for(int i=0;i<20;i++){sound.Collect();sound.Feedback(i%2==0);}
            Check(sound.ActiveVoiceCount<=1 && sound.ServicePriorityActive,"Repeated service requests replace one voice and retain discovery priority");
            yield return new WaitForSeconds(.9f);
            foreach(float gain in new[]{0f,float.NaN,float.PositiveInfinity,-1f,2f})
            {
                sound.StopComedy();Check(sound.PlayComedy(ComedyKind.Mimic,1,gain),"Accept safe comedy request for edge gain");
                float expected=float.IsNaN(gain)||float.IsInfinity(gain)?0:Mathf.Clamp01(gain);
                Check(sound.LastComedyGain==expected && sound.ActiveVoiceCount<=1,"Clamp/mute without extra voices");
            }
            sound.enabled=false;Check(!sound.ComedyPlaying && !sound.ServicePlaying,"Disable stops both audio voices");sound.enabled=true;
            shop.SetPanel(ShopPanel.None);
            yield return EnterCounter();Check(shop.OpenForDay(),"Open ordinary trading day");shop.SetPanel(ShopPanel.None);
            var first=Spawn(CustomerArchetypes.Casual);var second=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>first.Phase==CustomerPhase.Queued && second.Phase==CustomerPhase.Queued,35,"two real queue reservations");
            var firstTank=first.TargetTankId;var firstId=first.SpecimenId;var secondId=second.SpecimenId;int cash=shop.Model.Currency;
            yield return WaitFor(()=>shop.Comedy.Active && shop.Comedy.Kind==ComedyKind.Fart,25,"natural ambient fart trigger");
            yield return new WaitForSeconds(.65f);yield return Capture("221-live-fart");
            Check(first.SpecimenId==firstId && second.SpecimenId==secondId && shop.Model.Currency==cash && first.Phase==CustomerPhase.Queued && second.Phase==CustomerPhase.Queued,"Ambient reaction retains both orders and currency");
            shop.AmbientComedyEnabled=false;
            yield return MoveFor(Key.Backspace,.05f);
            Check(!shop.Comedy.Active && !shop.TryStartComedy(ComedyKind.Complaint),"Skip clears reaction but does not refund shared quiet time");
            shop.Comedy.Tick(60);
            Check(shop.TryStartComedy(ComedyKind.Complaint),"Live ridiculous complaint starts on safe visitor");
            yield return new WaitForSeconds(.65f);yield return Capture("222-live-complaint");
            shop.CancelComedy();shop.Comedy.Tick(60);
            Check(shop.TryStartComedy(ComedyKind.Mimic),"Stocked fish copies a safe customer beat");
            yield return new WaitForSeconds(.65f);
            Check(shop.World.ValidateTankBounds(out _),"Mimic fin poses keep all stock inside its tanks");yield return Capture("223-live-mimic");
            Check(!shop.TryStartComedy(ComedyKind.WorkerCelebration),"Unowned worker beat cannot start without a committed worker job");
            yield return AimForSlap(first);
            yield return MoveFor(Key.F,.05f);
            Check(first.Outcome==CustomerOutcome.Slapped && shop.Comedy.Kind==ComedyKind.Slap && shop.Comedy.Active,"Accepted deliberate slap replaces incidental comedy");
            Check(shop.Model.Currency==cash && shop.Model.GetTank(firstTank).HasValue,"Slap retains unpaid stock and gives no payment");
            yield return MoveFor(Key.Backspace,.05f);
            Check(!shop.SlapGesture.IsPlaying && !shop.Comedy.Active,"Skipping slap visuals never undoes its committed action");
            yield return ServeNext("Service remains available after comedy");
            Check(second.Outcome==CustomerOutcome.Sold && shop.Model.Currency>cash,"Other queued order pays once after gags");
            shop.SetPanel(ShopPanel.None);yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"both visitors leave after safe service");
            Check(shop.CloseForNight(),"Closing remains available");yield return WaitForNight(6);shop.SetPanel(ShopPanel.None);
            shop.Comedy.Tick(60);shop.Model.Tick(60);
            Ok(shop.Model.Collect(CultureKind.Bloom),"Belch fixture collects Bloom");Ok(shop.Model.Collect(CultureKind.Flow),"Belch fixture collects Flow");
            Ok(shop.Model.Dose(CultureKind.Bloom),"Belch fixture doses Bloom");Ok(shop.Model.Dose(CultureKind.Flow),"Belch fixture doses Flow");Ok(shop.Model.BeginIncubation(),"Begin actual incubation");
            shop.Advance(shop.Config.IncubationSeconds);
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready && !shop.Comedy.Active,"Readiness succeeds before the delayed belch");
            yield return WaitFor(()=>shop.Comedy.Active && shop.Comedy.Kind==ComedyKind.Belch,6,"post-completion belch");
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready,"Belch cannot consume or invalidate its ready specimen");
            yield return Capture("224-live-belch");
            shop.SetPanel(ShopPanel.Pause);
            Check(!shop.Comedy.Active && !sound.ComedyPlaying,"Opening pause cancels incidental sound and caption");
            shop.ReducedGags=true;shop.HumorVolume=0;Check(shop.SaveNow() && shop.ReloadSaved(),"Existing humor/reduced settings survive save/reload");
            Check(shop.ReducedGags && shop.HumorVolume==0 && !shop.Comedy.Active,"Reload does not replay a gag");
            shop.OpenComedyPreview();Check(shop.PlayComedyPreview(ComedyKind.WorkerCelebration),"Reduced muted worker preview remains available");
            Check(shop.PreviewComedy.Reduced && sound.LastComedyGain==0,"Reduced preview respects both existing settings");
            yield return new WaitForSeconds(.6f);yield return Capture("225-reduced-worker");
            shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
