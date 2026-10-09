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
            sound.PlayComedy(kind,Comedy.Variant,HumorVolume);
            return true;
        }

        void StartSlapComedy()
        {
            ClearComedyVisuals();
            Comedy.StartSlap(comedyVariant++,ReducedGags);ComedyStartedCount++;
            // The existing accepted slap action owns its sound and body reaction.
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
            comedyActor=null;comedyVisitor=null;mimicTank=null;
            if(World)World.HideComedyBubble();
            if(sound)sound.StopComedy();
        }
        void ResetComedyTransient()
        {
            CancelComedy();StopComedyPreview();belchWait=-1;ambientWait=18;
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
                activeWorkerActor.PoseComedy(ComedyKind.WorkerCelebration,Comedy.Elapsed,Comedy.Reduced);
            }
            if(comedyActor)comedyActor.SetComedyPose(Comedy.Kind,Comedy.Elapsed,Comedy.Reduced);
            if(mimicTank!=null)World.PoseFishMimic(mimicTank,Comedy.Elapsed,Comedy.Reduced);
            if(Comedy.Kind==ComedyKind.Belch && World.TryGetLabFacility(ShopLayout.IncubatorFacilityId,out var incubator))
                World.ShowComedyBubble(incubator.TransformPoint(new Vector3(0,2.0f,0)),Comedy.Elapsed,Comedy.Reduced);
        }
        void OnDisable()
        {
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
        static string ComedyTitle(ComedyKind kind)
        {
            switch(kind)
            {
                case ComedyKind.Slap:return "Cartoon slap";
                case ComedyKind.Fart:return "Suspicious fart";
                case ComedyKind.Complaint:return "Ridiculous complaint";
                case ComedyKind.Belch:return "Incubator belch";
                case ComedyKind.WorkerCelebration:return "Worker celebration";
                default:return "Fish copies a customer";
            }
        }
        void DrawComedyCaption()
        {
            if(Panel!=ShopPanel.None || !Comedy.Active)return;
            string caption=WorkerComedyCaption+Comedy.Caption;
            hudMeasure.text=caption;
            float width=Mathf.Clamp(centerStyle.CalcSize(hudMeasure).x+36,548,644);
            float left=686-width*0.5f;
            float captionHeight=Mathf.Max(24,HudTextHeight(caption,centerStyle,width-36));
            HudCard(new Rect(left,112,width,captionHeight+35),coral);
            GUI.Label(new Rect(left+18,118,width-36,captionHeight),caption,centerStyle);
            GUI.Label(new Rect(left+18,121+captionHeight,width-36,21),"BACKSPACE: skip gag  /  service and movement stay available",controlsStyle);
        }
        void DrawComedyPreview()
        {
            Box(new Rect(24,22,1232,83),ink);
            GUI.Label(new Rect(44,29,1100,23),"COMEDY STUDY / YOUR SHOP IS PAUSED / NO SALES OR WORK ARE PERFORMED",smallStyle);
            GUI.Label(new Rect(44,53,1090,42),ComedyTitle(previewKind),titleStyle);
            Box(new Rect(24,470,1232,226),ink);
            GUI.Label(new Rect(44,481,1190,45),PreviewComedy.Active?PreviewComedy.Caption:"Pick a beat to preview. Repeat it for the alternate caption and sound.",centerStyle);
            for(int i=0;i<6;i++)
            {
                var kind=(ComedyKind)i;
                if(GUI.Button(new Rect(44+(i%3)*397,534+(i/3)*43,384,36),ComedyTitle(kind),buttonStyle))PlayComedyPreview(kind);
            }
            GUI.Label(new Rect(44,622,800,25),"Uses your Humor volume and Reduced gags settings. Live workers celebrate completed jobs when idle.",smallStyle);
            if(GUI.Button(new Rect(44,653,280,32),"Skip / BACKSPACE",buttonStyle))StopComedyPreview();
            if(GUI.Button(new Rect(914,653,320,32),"Back to pause / ESC",buttonStyle))SetPanel(ShopPanel.Pause);
        }
    }
}
