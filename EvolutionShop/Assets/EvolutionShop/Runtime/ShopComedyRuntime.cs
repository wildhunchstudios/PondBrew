using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopComedy Comedy { get; } = new ShopComedy();
        public bool ComedyPreview { get; private set; }
        public ShopComedy PreviewComedy { get; private set; } = new ShopComedy();
        public bool AmbientComedyEnabled { get; set; } = true;
        public bool AmbientComedyDeferredForTutorial => Tutorial.FirstSaleAccommodationActive;
        public int ComedyStartedCount { get; private set; }
        CustomerVisit comedyVisitor;
        CustomerActor comedyActor;
        string mimicTank;
        float ambientWait=18, belchWait=-1, belchExpiry;
        int ambientKind, comedyVariant, previewVariant;
        ComedyKind previewKind=ComedyKind.WorkerCelebration;

        void TickComedy(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)return;
            bool wasActive=Comedy.Active;
            Comedy.Tick(seconds);
            if(Cycle.Phase==ShopDayPhase.Closing)
            {if(Comedy.Active && Comedy.Kind!=ComedyKind.Slap)CancelComedy();belchWait=-1;}
            if(wasActive && !Comedy.Active) ClearComedyVisuals();
            if(Comedy.Active && comedyVisitor!=null &&
                (!comedyActor || !comedyActor.isActiveAndEnabled || Customers.ActorOf(comedyVisitor)!=comedyActor || comedyVisitor.Phase==CustomerPhase.Paid || comedyVisitor.Phase==CustomerPhase.Leaving || comedyVisitor.Phase==CustomerPhase.Finished)) CancelComedy();
            if(Comedy.Active && mimicTank!=null && !Model.GetTank(mimicTank).HasValue) CancelComedy();
            if(Comedy.Active && Comedy.Kind==ComedyKind.Belch && Model.Incubator.Phase!=IncubatorPhase.Ready) CancelComedy();
            TickWorkerCelebration(seconds);
            if(Comedy.Active && World)World.TickComedyFx(Comedy.Elapsed);
            if(belchWait>=0)
            {
                belchWait=Mathf.Max(0,belchWait-seconds);belchExpiry-=seconds;
                if(belchExpiry<=0 || Model.Incubator.Phase!=IncubatorPhase.Ready)belchWait=-1;
                else if(belchWait<=0 && Panel==ShopPanel.None)
                { TryStartComedy(ComedyKind.Belch);belchWait=-1; }
            }
            if(!AmbientComedyEnabled || AmbientComedyDeferredForTutorial || Panel!=ShopPanel.None || PlacementActive || Cycle.Phase!=ShopDayPhase.Open)return;
            ambientWait-=seconds;
            if(ambientWait>0 || Comedy.Active)return;
            ambientWait=ReducedGags?45:18;
            var kind=ambientKind%3==0?ComedyKind.Fart:ambientKind%3==1?ComedyKind.Complaint:ComedyKind.Mimic;
            if(TryStartComedy(kind))ambientKind++;
        }

        /// <summary>Shared live trigger. A joke never owns a transaction, route, specimen or task.</summary>
        public bool TryStartComedy(ComedyKind kind)
        {
            if(!isActiveAndEnabled || Paused || Panel!=ShopPanel.None || PlacementActive ||
                kind==ComedyKind.Slap || kind==ComedyKind.WorkerCelebration)return false;
            if(AmbientComedyDeferredForTutorial &&
                (kind==ComedyKind.Fart || kind==ComedyKind.Complaint || kind==ComedyKind.Mimic))return false;
            CustomerVisit visit=null;CustomerActor actor=null;string tank=null;
            if(kind==ComedyKind.Belch)
            {
                if(Model.Incubator.Phase!=IncubatorPhase.Ready || Cycle.Phase==ShopDayPhase.Closing)return false;
            }
            else
            {
                if(Cycle.Phase!=ShopDayPhase.Open || Player.transform.position.z>.4f)return false;
                foreach(var candidate in Customers.Visits)
                {
                    if(candidate.Phase!=CustomerPhase.Browsing && candidate.Phase!=CustomerPhase.Queued)continue;
                    var visual=Customers.ActorOf(candidate);
                    if(visual && visual.isActiveAndEnabled && Vector3.Distance(Player.transform.position,visual.transform.position)<7f)
                    {visit=candidate;actor=visual;break;}
                }
                if(visit==null)return false;
                if(kind==ComedyKind.Mimic)
                {
                    foreach(var id in Model.TankIds)if(Model.GetTank(id).HasValue){tank=id;break;}
                    if(tank==null)return false;
                }
            }
            if(!Comedy.TryStart(kind,comedyVariant++,ReducedGags))return false;
            comedyVisitor=visit;comedyActor=actor;mimicTank=tank;ComedyStartedCount++;
            Transform source=actor?actor.transform:null;
            if(kind==ComedyKind.Belch && World)World.TryGetLabFacility(ShopLayout.IncubatorFacilityId,out source);
            if(kind==ComedyKind.Mimic && World)source=World.ComedyMimicSource(tank);
            BeginComedyPresentation(source);
            sound.PlayComedy(kind,Comedy.Variant,HumorVolume);
            return true;
        }

        void StartSlapComedy(CustomerActor target)
        {
            ClearComedyVisuals();
            Comedy.StartSlap(comedyVariant++,ReducedGags);ComedyStartedCount++;
            BeginComedyPresentation(target?target.transform:null);
            // The existing accepted slap action owns its sound and body reaction.
        }
        void BeginComedyPresentation(Transform source)
        {
            comedySpeaker=source; // UX-03: the toolkit HUD's caption bubble follows the beat's source
            if(World && fxHasFocus && !Paused && Panel==ShopPanel.None && !PlacementActive)
                World.BeginComedyFx(Comedy.Kind,Comedy.Variant,source,NextFxSequence(),ReducedGags);
        }
        public void CancelComedy()
        {
            Comedy.Cancel();ClearComedyVisuals();
        }
        public void SkipComedy()
        {
            if(SlapGesture)SlapGesture.Cancel();
            if(Customers)foreach(var visit in Customers.Visits)
            {var actor=Customers.ActorOf(visit);if(actor)actor.CancelSlapPresentation();}
            CancelComedy();
        }
        void ClearComedyVisuals()
        {
            ClearWorkerCelebration();
            if(comedyActor)comedyActor.ClearComedyPose();
            comedyActor=null;comedyVisitor=null;mimicTank=null;comedySpeaker=null;
            if(World) { World.CancelComedyFx(); World.HideComedyBubble(); World.ClearFishMimic(); }
            if(sound)sound.StopComedy();
        }
        void ResetComedyTransient()
        {
            SkipComedy();StopComedyPreview();belchWait=-1;ambientWait=18;
        }
        void ComedyInput(Keyboard keyboard,bool gameplayAtFrameStart)
        {
            if(keyboard==null || !keyboard.backspaceKey.wasPressedThisFrame)return;
            if(Panel==ShopPanel.Presentation && ComedyPreview)StopComedyPreview();
            else if(gameplayAtFrameStart && Panel==ShopPanel.None &&
                (ValidationMode?AllowValidationInput:Application.isFocused))SkipComedy();
        }
        void LateUpdate()
        {
            if(!initialized || Paused || !Comedy.Active)return;
            if(activeWorkerId!=null)
            {
                if(!WorkerCelebrationStillValid(activeWorkerId,activeWorkerActor) || (sound && sound.ServicePriorityActive))
                {CancelComedy();return;}
                activeWorkerActor.PoseComedy(ComedyKind.WorkerCelebration,Comedy.Elapsed,Comedy.Reduced || World.CustomerReducedMotion,Comedy.Variant);
            }
            if(comedyActor)comedyActor.SetComedyPose(Comedy.Kind,Comedy.Elapsed,Comedy.Reduced || World.CustomerReducedMotion,Comedy.Variant);
            if(mimicTank!=null)World.PoseFishMimic(mimicTank,Comedy.Elapsed,Comedy.Reduced);

        }
        void OnDisable()
        {
            DisableSessionFx();
            ResetAutomationClock();
            if(!initialized)return;
            // Release the look-only stage and borrowed camera before this owner stops ticking.
            if(Panel==ShopPanel.Presentation && Player && World)SetPanel(ShopPanel.Pause);
            ResetComedyTransient();
        }

        public void OpenComedyPreview()
        {
            if(!isActiveAndEnabled || Panel!=ShopPanel.Pause)return;
            SetPanel(ShopPanel.Presentation);ComedyPreview=true;
            SelectPresentationView(4);previewKind=ComedyKind.WorkerCelebration;
            Player.View.fieldOfView=68;
        }
        public bool PlayComedyPreview(ComedyKind kind)
        {
            if(!isActiveAndEnabled || Panel!=ShopPanel.Presentation || !ComedyPreview || !System.Enum.IsDefined(typeof(ComedyKind),kind))return false;
            StopComedyPreview();PreviewComedy=new ShopComedy();previewKind=kind;
            SelectPresentationView(kind==ComedyKind.WorkerCelebration?4:kind==ComedyKind.Mimic || kind==ComedyKind.Belch?2:3);
            Player.View.fieldOfView=68;
            if(kind==ComedyKind.Slap)PreviewComedy.StartSlap(previewVariant++,ReducedGags);
            else PreviewComedy.TryStart(kind,previewVariant++,ReducedGags);
            sound.PlayComedy(kind,PreviewComedy.Variant,HumorVolume);
            return PreviewComedy.Active;
        }
        /// <summary>The beat the comedy study last played (or is playing); the study view names it.</summary>
        public ComedyKind ComedyPreviewKind => previewKind;
        public void StopComedyPreview()
        {
            PreviewComedy.Cancel();if(sound)sound.StopComedy();
            if(World){World.HideComedyBubble();World.AnimatePresentationSample(CharacterSamplePose.Idle,0,0);}
        }
        void TickComedyPreview()
        {
            PreviewComedy.Tick(Time.unscaledDeltaTime);
            if(PreviewComedy.Active)
            {
                World.AnimateComedySample(PreviewComedy.Kind,PreviewComedy.Elapsed,PreviewComedy.Reduced);
                if(PreviewComedy.Kind==ComedyKind.Belch)World.ShowComedyBubble(new Vector3(0,1.5f,-2.35f),PreviewComedy.Elapsed,PreviewComedy.Reduced);
            }
            else{World.HideComedyBubble();World.AnimatePresentationSample(CharacterSamplePose.Idle,0,0);}
        }
    }
}
