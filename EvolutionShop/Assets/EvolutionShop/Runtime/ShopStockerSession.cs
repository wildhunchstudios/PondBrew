using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopStocking Stocking { get; private set; }
        public ShopStocker Stocker { get; private set; }

        public bool OpenStockerMenu()
        {
            if (Paused || !IsAtCounter || (Panel!=ShopPanel.Counter && Panel!=ShopPanel.Staff &&
                Panel!=ShopPanel.Stocker && Panel!=ShopPanel.StockerTargets))
            { Notify("Use Staff at the physical front counter."); return false; }
            SetPanel(ShopPanel.Stocker); return true;
        }
        bool StockerControlsAvailable()
        {
            if (Paused || !IsAtCounter || (Panel!=ShopPanel.Stocker && Panel!=ShopPanel.StockerTargets))
            { Notify("Use Nori's staff controls at the physical front counter."); return false; }
            return true;
        }
        public bool HireStocker() => StockerControlsAvailable() && ChangeStocker((setting,game)=>setting.Employee.Hire(Cycle),"Nori hired, initially off schedule. Choose display targets and a schedule.");
        public bool DismissStocker() => StockerControlsAvailable() && ChangeStocker((setting,game)=>setting.Employee.Dismiss(Cycle),"Nori dismissed. Reserved creatures remain safe in their racks.");
        public bool SetStockerScheduled(bool scheduled)
        {
            if (!StockerControlsAvailable()) return false;
            return ChangeStocker((setting,game)=>
            {
                if (!setting.Employee.SetScheduled(scheduled,Cycle)) return false;
                if (scheduled) setting.Employee.TryBeginDuty(game,Cycle);
                return true;
            },scheduled ? "Nori's recurring schedule saved. " : "Nori is off schedule. Started wages are not refunded.");
        }
        public bool SetStockerPaused(bool paused)
        {
            if (!StockerControlsAvailable()) return false;
            return ChangeStocker((setting,game)=>
            {
                if (!setting.Employee.SetPaused(paused)) return false;
                if (!paused && Cycle.Phase==ShopDayPhase.Night) setting.Employee.TryBeginDuty(game,Cycle);
                return true;
            },paused ? "Nori paused. Claimed stock is available for manual work again." : "Nori resumed. ");
        }
        public bool PayStockerDuty() => StockerControlsAvailable() && ChangeStocker((setting,game)=>setting.Employee.TryBeginDuty(game,Cycle),"Nori's duty wages confirmed. ");
        public bool SetStockingTarget(string tankId,string speciesId) => StockerControlsAvailable() &&
            ChangeStocker((setting,game)=>setting.SetTarget(game,tankId,speciesId),"Display target saved. Nori will start a fresh job.");
        public bool RetryStocker()
        {
            if (!StockerControlsAvailable() || !Stocking.Employee.Hired) return false;
            Stocker.Retry();Notify("Nori will check the route again. Reserved stock stays safe.");return true;
        }
        void BeginScheduledStockerNight()
        {
            if (Stocking.Employee.Hired && Stocking.Employee.Scheduled && !Stocking.Employee.Paused)
                ChangeStocker((setting,game)=>setting.Employee.TryBeginDuty(game,Cycle),"New stocker duty period. ");
        }
        bool ChangeStocker(Func<ShopStocking,GameModel,bool> change,string success)
        {
            if (AutosaveBlocked) { Notify("Restore a valid save before changing staff."); return false; }
            if (!GameModel.TryRestore(Layout.WithTankIds(Config),Model.CaptureSave(),out var game,out _) ||
                !ShopStocking.TryRestore(Stocking.CaptureSave(game),game,Cycle.DayNumber,out var setting))
                throw new InvalidOperationException("Cannot stage stocker settings.");
            if (!change(setting,game))
            { Notify("Nori needs a valid target, or a night schedule and "+ShopStaff.StockerWage+" credits for an unpaid period. No change was made.");return false; }
            bool paid=game.Currency!=Model.Currency;
            if (paid && (Cycle.Phase!=ShopDayPhase.Night || Customers.ActiveCount!=0))
            { Notify("Wages start during clear preparation nights. No charge was made.");return false; }
            if (!SaveStore.Write(SavePath,CaptureStaffSlot(game,Staff,setting),out var error))
            { SaveError=error;Notify("Stocker change could not be saved. Money, targets and previous work are unchanged.");return false; }
            Stocker.ResetTransient();
            if (paid) { Cashier.ResetTransient();Technician.ResetTransient();Model=game; }
            Stocking=setting;SaveError=null;saveClock=0;
            Stocker.RefreshVisual();World.Refresh(Model);
            Notify(success.TrimEnd()+(Stocking.Employee.Hired && Stocking.Employee.Scheduled ? " "+StockerPaymentLine() : ""));
            return true;
        }

        // Persistence precedes publication. Keep the live model instance: current customers
        // own reservations on it. The staged copy has the same stock but no transient claims.
        public bool CompleteStockerJob(string jobId)
        {
            if (Paused || AutosaveBlocked || PlacementActive || Panel==ShopPanel.Arrange || !Stocker ||
                !Stocker.isActiveAndEnabled || !Stocker.Actor || !Stocker.Actor.isActiveAndEnabled ||
                !Stocking.Employee.IsOnDuty(Cycle.DayNumber) || !Stocker.ReadyToDeliver ||
                jobId==null || jobId!=Stocker.CurrentJobId) return false;
            var reservation=Model.GetStockTransfer(jobId);
            if (!reservation.HasValue || reservation.Value.OwnerId!=ShopStaff.StockerId) return false;
            var claim=reservation.Value;
            if (!Layout.TryGetBrowsePoint(claim.TankId,0,out float tx,out float tz)) return false;
            Vector3 position=World.transform.InverseTransformPoint(Stocker.Actor.transform.position);
            if (Vector2.Distance(new Vector2(position.x,position.z),new Vector2(tx,tz))>.45f) return false;
            string target=Stocking.GetTarget(claim.TankId);
            var source=Model.GetStorage(claim.StorageId);
            if (target==null || !source.HasValue || (target!=ShopStocking.AnySpecies && target!=source.Value.SpeciesId)) return false;
            if (!GameModel.TryRestore(Layout.WithTankIds(Config),Model.CaptureSave(),out var staged,out _) ||
                !ShopTradeLedger.TryRestore(Reports.CaptureSave(),Cycle.CaptureSave(),out var ledger))
                throw new InvalidOperationException("Cannot stage stocker delivery.");
            if (!staged.ReserveStockTransfer(jobId,ShopStaff.StockerId,claim.StorageId,claim.TankId,claim.SpecimenId).Success) return false;
            var stagedResult=staged.CompleteStockTransfer(jobId,ShopStaff.StockerId);
            if (!stagedResult.Success) return false;
            ledger.RecordRestock();
            var tutorial=CopyTutorial();
            tutorial.ObserveDisplayed(staged,stagedResult,TutorialActionOrigin.Worker);tutorial.Reconcile(staged);
            var slot=CaptureStaffSlot(staged,Staff,Stocking,ledger);slot.tutorial=tutorial.CaptureSave();
            if (!SaveStore.Write(SavePath,slot,out var error))
            { SaveError=error;Notify("Delivery was not saved. The creature is still safe in its rack. Retry Nori after fixing saving.");return false; }
            // Synchronous main-thread write cannot advance another transaction between staging
            // and this revalidated publication; no animation callback owns this operation.
            var result=Model.CompleteStockTransfer(jobId,ShopStaff.StockerId);
            if (!result.Success) throw new InvalidOperationException("Validated stocker delivery changed during persistence.");
            Reports.RecordRestock();Tutorial=tutorial;SaveError=null;saveClock=0;
            World.CaptureHabitatTransfers();World.Refresh(Model);World.PresentHabitatTransfers(true,NextFxSequence(),FxCueOrigin.Worker);Notify("Nori stocked "+Model.GetSpecies(result.SpeciesId).DisplayName+" / ready for customers.");
            return true;
        }
        string StockerPaymentLine() => Stocking.Employee.PaidDay==Cycle.DayNumber ?
            Stocking.Employee.PaidWage+" credits paid for preparation night + trading day "+Cycle.DayNumber+"." :
            "Off duty / "+ShopStaff.StockerWage+" credits needed at night. Manual stocking stays available.";

        void DrawStockerPanel()
        {
            Heading("Nori / your stocker");
            Small("Carries ready creatures from lab racks to your chosen displays. You or Moss can prepare the creatures and fill the racks.");
            Text(ShopStaff.StockerWage+" credits per night + day / no hiring fee");
            Small("Scheduled work repeats each new night when funded. Started periods are paid in full. With limited cash, Pip's scheduled wage is paid first.");
            Space();Text(Stocker.Status);Small(StockerPaymentLine());
            Button("Choose display targets",()=>SetPanel(ShopPanel.StockerTargets));
            bool night=Cycle.Phase==ShopDayPhase.Night;var employee=Stocking.Employee;
            if (!night) Small("Hire, schedule, dismiss and pay at night. Targets and pause can change now.");
            if (!employee.Hired) StaffButton("Hire Nori / initially off schedule",night,()=>HireStocker());
            else
            {
                StaffButton(employee.Scheduled ? "Stop recurring schedule" : employee.PaidDay==Cycle.DayNumber ?
                    "Schedule Nori / this period already paid" : "Schedule Nori / pay "+ShopStaff.StockerWage+" credits",night,()=>SetStockerScheduled(!employee.Scheduled));
                if (employee.Scheduled && employee.PaidDay!=Cycle.DayNumber)
                    StaffButton("Pay "+ShopStaff.StockerWage+" credits / start duty",night && !employee.Paused && Model.Currency>=ShopStaff.StockerWage,()=>PayStockerDuty());
                StaffButton(employee.Paused ? "Resume stocking" : "Pause stocking / release reserved stock",true,()=>SetStockerPaused(!employee.Paused));
                StaffButton("Retry a blocked route",Stocker.Blocked,()=>RetryStocker());
                StaffButton("Dismiss Nori / no wage refund",night,()=>DismissStocker());
            }
            Space();Button("Moss / lab technician",()=>OpenTechnicianMenu());
            Button("Back to Pip / cashier",()=>SetPanel(ShopPanel.Staff));
        }
        void DrawStockerTargets()
        {
            Heading("Nori's display targets");
            Small("Keep one creature in each enabled slot, in the order below. Only lab rack stock is used. Full displays wait; existing stock and customer reservations are never replaced.");
            Small("Changing a target cancels the current delivery safely. Stored or sold display furniture loses its target; choose it again after redeploying.");
            foreach (string tank in Model.TankIds)
            {
                Space();string selected=Stocking.GetTarget(tank);
                Text(DisplaySlotName(tank)+" / "+(selected==null ? "off" : selected==ShopStocking.AnySpecies ? "any rack species" : Model.GetSpecies(selected).DisplayName));
                GUILayout.BeginHorizontal();
                StaffButton("Off",selected!=null,()=>SetStockingTarget(tank,null));
                StaffButton("Any species",selected!=ShopStocking.AnySpecies,()=>SetStockingTarget(tank,ShopStocking.AnySpecies));
                GUILayout.EndHorizontal();
                foreach (string species in Model.DiscoveredSpeciesIds)
                    StaffButton("Keep 1 "+Model.GetSpecies(species).DisplayName,selected!=species,()=>SetStockingTarget(tank,species));
            }
            Space();Button("Back to Nori",()=>SetPanel(ShopPanel.Stocker));
        }
        static string DisplaySlotName(string id) => id=="tank-a" ? "Display A" : id=="tank-b" ? "Display B" : "Display "+id.Replace("table-","#").Replace("-slot-"," / slot ");
    }
}
