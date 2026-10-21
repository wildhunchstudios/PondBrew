using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopLaboratory Laboratory { get; private set; }
        public ShopTechnician Technician { get; private set; }

        public bool OpenTechnicianMenu()
        {
            if (Paused || !IsAtCounter || (Panel!=ShopPanel.Counter && Panel!=ShopPanel.Staff &&
                Panel!=ShopPanel.Stocker && Panel!=ShopPanel.Technician && Panel!=ShopPanel.TechnicianOrders))
            { Notify("Use Staff at the physical front counter.");return false; }
            SetPanel(ShopPanel.Technician);return true;
        }
        bool TechnicianControlsAvailable()
        {
            // UX-06: the notebook's crew tab (Staff) holds Moss's card, at the same counter.
            bool crewTab=Panel==ShopPanel.Staff && ToolkitOwnsPanel(ShopPanel.Staff);
            if (Paused || !IsAtCounter || (Panel!=ShopPanel.Technician && Panel!=ShopPanel.TechnicianOrders && !crewTab))
            { Notify("Use Moss's staff controls at the physical front counter.");return false; }
            return true;
        }
        public bool HireTechnician() => TechnicianControlsAvailable() && ChangeLaboratory((lab,game)=>lab.Employee.Hire(Cycle),"Moss hired off schedule. Queue known recipes and choose a schedule.");
        public bool DismissTechnician() => TechnicianControlsAvailable() && ChangeLaboratory((lab,game)=>
        {
            if (!lab.Employee.Dismiss(Cycle)) return false;
            lab.ClearAssignments();return true;
        },"Moss dismissed. Waiting work is cleared; any incubator batch stays safe for manual collection.");
        public bool SetTechnicianScheduled(bool scheduled) => TechnicianControlsAvailable() && ChangeLaboratory((lab,game)=>
        {
            if (!lab.Employee.SetScheduled(scheduled,Cycle)) return false;
            if (scheduled) lab.Employee.TryBeginDuty(game,Cycle);
            return true;
        },scheduled ? "Moss's recurring schedule saved." : "Moss is off schedule. Started wages are not refunded.");
        public bool SetTechnicianPaused(bool paused) => TechnicianControlsAvailable() && ChangeLaboratory((lab,game)=>
        {
            if (!lab.Employee.SetPaused(paused)) return false;
            if (!paused && Cycle.Phase==ShopDayPhase.Night) lab.Employee.TryBeginDuty(game,Cycle);
            return true;
        },paused ? "Moss paused. Manual incubator work is available; the machine keeps processing." : "Moss resumed.");
        public bool PayTechnicianDuty() => TechnicianControlsAvailable() && ChangeLaboratory((lab,game)=>lab.Employee.TryBeginDuty(game,Cycle),"Moss's duty wages confirmed.");
        public bool QueueTechnicianRecipe(string speciesId) => TechnicianControlsAvailable() &&
            ChangeLaboratory((lab,game)=>lab.AddOrder(game,speciesId),"One recipe start added to Moss's queue.");
        public bool MoveTechnicianPriority(string speciesId,int direction) => TechnicianControlsAvailable() &&
            ChangeLaboratory((lab,game)=>lab.MovePriority(speciesId,direction),"Waiting recipe priority changed. A started batch stays intact.");
        public bool RemoveTechnicianRecipe(string speciesId) => TechnicianControlsAvailable() &&
            ChangeLaboratory((lab,game)=>lab.RemoveOrder(speciesId),"Waiting starts removed. A started batch stays intact.");
        public bool CancelTechnicianAssignments() => TechnicianControlsAvailable() &&
            ChangeLaboratory((lab,game)=>lab.ClearAssignments(),"Moss's assignments cleared. Any current batch stays in the incubator for manual collection.");
        public bool RetryTechnician()
        {
            if (!TechnicianControlsAvailable() || !Laboratory.Employee.Hired) return false;
            Technician.Retry();Notify("Moss will check the route again. Cultures and specimens stay safe.");return true;
        }
        void BeginScheduledTechnicianNight()
        {
            if (Laboratory.Employee.Hired && Laboratory.Employee.Scheduled && !Laboratory.Employee.Paused)
                ChangeLaboratory((lab,game)=>lab.Employee.TryBeginDuty(game,Cycle),"New technician duty period.");
        }
        bool ChangeLaboratory(Func<ShopLaboratory,GameModel,bool> change,string success)
        {
            if (AutosaveBlocked) { Notify("Restore a valid save before changing staff.");return false; }
            if (!GameModel.TryRestore(Layout.WithTankIds(Config),Model.CaptureSave(),out var game,out _) ||
                !ShopLaboratory.TryRestore(Laboratory.CaptureSave(game),game,Cycle.DayNumber,out var lab))
                throw new InvalidOperationException("Cannot stage technician settings.");
            if (!change(lab,game))
            { Notify("Moss needs a valid known recipe, room in the queue, or a funded night schedule. No change was made.");return false; }
            bool paid=game.Currency!=Model.Currency;
            if (paid && (Cycle.Phase!=ShopDayPhase.Night || Customers.ActiveCount!=0))
            { Notify("Wages start during clear preparation nights. No charge was made.");return false; }
            if (!WriteUiObservedSlot(CaptureStaffSlot(game,Staff,null,null,lab),out var error))
            { SaveError=error;Notify("Technician change was not saved. Previous money, work and queue are unchanged.");return false; }
            Technician.ResetTransient();
            if (paid) { Cashier.ResetTransient();Stocker.ResetTransient();Model=game; }
            Laboratory=lab;SaveError=null;saveClock=0;Technician.RefreshVisual();World.Refresh(Model);
            Notify(success.TrimEnd()+(Laboratory.Employee.Hired && Laboratory.Employee.Scheduled ? " "+TechnicianPaymentLine() : ""));
            return true;
        }

        public bool StartTechnicianJob(string jobId) => CommitTechnicianJob(jobId,true);
        public bool CompleteTechnicianJob(string jobId) => CommitTechnicianJob(jobId,false);
        bool CommitTechnicianJob(string jobId,bool start)
        {
            if (Paused || AutosaveBlocked || PlacementActive || Panel==ShopPanel.Arrange || !Technician ||
                !Technician.isActiveAndEnabled || !Technician.Actor || !Technician.Actor.isActiveAndEnabled ||
                !Laboratory.Employee.IsOnDuty(Cycle.DayNumber) || jobId==null || jobId!=Technician.CurrentJobId ||
                (start ? !Technician.ReadyToStart : !Technician.ReadyToDeliver)) return false;
            var reservation=Model.TechnicianClaim;
            if (!reservation.HasValue || reservation.Value.JobId!=jobId || reservation.Value.OwnerId!=ShopStaff.TechnicianId ||
                reservation.Value.StartsRecipe!=start) return false;
            var claim=reservation.Value;
            float x,z;
            if (start)
            {
                if (Laboratory.ActiveSpecimenId!=null || Laboratory.Queue.Length==0 || Laboratory.Queue[0].speciesId!=claim.SpeciesId ||
                    !Layout.TryGetFacilityAccessPoint(ShopLayout.IncubatorFacilityId,out x,out z)) return false;
            }
            else
            {
                if (Laboratory.ActiveSpecimenId!=claim.SpecimenId) return false;
                string rack=null;
                foreach (var item in Layout.PlacedItems)
                    if (Array.IndexOf(Layout.GetStorageSlotIds(item.Id),claim.StorageId)>=0) { rack=item.Id;break; }
                if (rack==null || !Layout.TryGetStorageAccessPoint(rack,out x,out z)) return false;
            }
            Vector3 actorPosition=World.transform.InverseTransformPoint(Technician.Actor.transform.position);
            if (Vector2.Distance(new Vector2(actorPosition.x,actorPosition.z),new Vector2(x,z))>.45f) return false;
            if (!GameModel.TryRestore(Layout.WithTankIds(Config),Model.CaptureSave(),out var staged,out _) ||
                !ShopLaboratory.TryRestore(Laboratory.CaptureSave(staged),staged,Cycle.DayNumber,out var lab))
                throw new InvalidOperationException("Cannot stage technician transaction.");
            ActionResult reserved=start ? staged.ReserveTechnicianStart(jobId,ShopStaff.TechnicianId,claim.SpeciesId,claim.StorageId,claim.SpecimenId) :
                staged.ReserveTechnicianOutput(jobId,ShopStaff.TechnicianId,claim.StorageId,claim.SpecimenId);
            if (!reserved.Success) return false;
            ActionResult stagedResult=start ? staged.CompleteTechnicianStart(jobId,ShopStaff.TechnicianId) : staged.CompleteTechnicianOutput(jobId,ShopStaff.TechnicianId);
            if (!stagedResult.Success) return false;
            if (start)
            {
                if (!lab.MarkStarted(claim.SpeciesId,claim.SpecimenId)) return false;
            }
            else lab.ClearActive();
            var tutorial=CopyTutorial();
            if (start) tutorial.ObserveStarted(staged,stagedResult,TutorialActionOrigin.Worker);
            tutorial.Reconcile(staged);
            var slot=CaptureStaffSlot(staged,Staff,null,null,lab);slot.tutorial=tutorial.CaptureSave();
            if (!WriteUiObservedSlot(slot,out var error))
            { SaveError=error;PresentWorkerResult(Technician.Actor,start?FxWorkerEvent.Dose:FxWorkerEvent.Place,jobId,false);Notify("Moss could not save this action. All cultures and creatures remain at their original station. Retry after saving is available.");return false; }
            // Synchronous main-thread persistence cannot advance another transaction. Keep
            // the actual model so customer and stocker reservations retain their owner.
            ActionResult result=start ? Model.CompleteTechnicianStart(jobId,ShopStaff.TechnicianId) : Model.CompleteTechnicianOutput(jobId,ShopStaff.TechnicianId);
            if (!result.Success || result.SpecimenId!=claim.SpecimenId)
                throw new InvalidOperationException("Validated technician transaction changed during persistence.");
            PresentWorkerResult(Technician.Actor,start?FxWorkerEvent.Dose:FxWorkerEvent.Place,jobId,true);
            Laboratory=lab;Tutorial=tutorial;SaveError=null;saveClock=0;World.CaptureHabitatTransfers();World.Refresh(Model);World.PresentHabitatTransfers(true,NextFxSequence(),FxCueOrigin.Worker);
            Notify(start ? "Moss started "+Model.GetSpecies(claim.SpeciesId).DisplayName+". The incubator is processing." :
                "Moss stored "+Model.GetSpecies(claim.SpeciesId).DisplayName+" in the lab rack.");
            return true;
        }

        string TechnicianPaymentLine() => Laboratory.Employee.PaidDay==Cycle.DayNumber ?
            Laboratory.Employee.PaidWage+" credits paid for preparation night + trading day "+Cycle.DayNumber+"." :
            "Off duty / "+ShopStaff.TechnicianWage+" credits needed at night. Manual lab work remains available.";
    }
}
