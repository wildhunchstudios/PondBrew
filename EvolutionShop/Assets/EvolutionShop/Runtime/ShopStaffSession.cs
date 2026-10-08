using System;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public ShopStaff Staff { get; private set; }
        public ShopCashier Cashier { get; private set; }

        public bool OpenStaffMenu()
        {
            if (Paused || !IsAtCounter || (Panel != ShopPanel.Counter && Panel != ShopPanel.Staff))
            { Notify("Use the physical front counter to manage your staff."); return false; }
            SetPanel(ShopPanel.Staff);
            return true;
        }

        bool StaffControlsAvailable()
        {
            if (Paused || Panel != ShopPanel.Staff || !IsAtCounter)
            { Notify("Use Staff at the physical front counter."); return false; }
            return true;
        }

        public bool HireCashier() => StaffControlsAvailable() && ChangeStaff((staff,game)=>staff.Hire(Cycle),"Pip hired. Choose a schedule when you are ready to pay duty wages.");
        public bool DismissCashier() => StaffControlsAvailable() && ChangeStaff((staff,game)=>staff.Dismiss(Cycle),"Pip dismissed. Started duty wages are not refunded.");
        public bool SetCashierScheduled(bool scheduled)
        {
            if (!StaffControlsAvailable()) return false;
            return ChangeStaff((staff,game)=>
            {
                if (!staff.SetScheduled(scheduled,Cycle)) return false;
                if (scheduled) staff.TryBeginDuty(game,Cycle);
                return true;
            },scheduled ? "Schedule saved. " : "Pip is off schedule. Started duty wages are not refunded.");
        }
        public bool SetCashierPaused(bool paused)
        {
            if (!StaffControlsAvailable()) return false;
            return ChangeStaff((staff,game)=>
            {
                if (!staff.SetPaused(paused)) return false;
                if (!paused && Cycle.Phase==ShopDayPhase.Night) staff.TryBeginDuty(game,Cycle);
                return true;
            },paused ? "Pip's work is paused. You can serve manually." : "Pip's work resumed. ");
        }
        public bool PayCashierDuty() => StaffControlsAvailable() && ChangeStaff((staff,game)=>staff.TryBeginDuty(game,Cycle),"Duty wages confirmed. ");

        void BeginScheduledStaffNight()
        {
            if (!Staff.Hired || !Staff.Scheduled || Staff.Paused) return;
            ChangeStaff((staff,game)=>staff.TryBeginDuty(game,Cycle),"New duty period. ");
        }

        // Stage the complete economic change before publishing it. A failed write cannot
        // spend money or grant paid work. Night-only cash changes have no live visit owners.
        bool ChangeStaff(Func<ShopStaff,GameModel,bool> change,string successMessage)
        {
            if (AutosaveBlocked)
            { Notify("Staff changes are blocked while the original save needs repair."); return false; }
            if (!ShopStaff.TryRestore(Staff.CaptureSave(),Cycle.DayNumber,out var stagedStaff) ||
                !GameModel.TryRestore(Layout.WithTankIds(Config),Model.CaptureSave(),out var stagedGame,out _))
                throw new InvalidOperationException("Cannot stage the current staff transaction.");
            if (!change(stagedStaff,stagedGame))
            {
                Notify(Cycle.Phase!=ShopDayPhase.Night ? "Hire, schedule or pay at night; manual checkout remains available." :
                    "Pip needs an enabled schedule, unpaused work and "+ShopStaff.CashierWage+" credits for an unpaid duty period. No charge was made.");
                return false;
            }
            bool cashChanged=stagedGame.Currency!=Model.Currency;
            if (cashChanged && (Cycle.Phase!=ShopDayPhase.Night || Customers.ActiveCount!=0))
            { Notify("Wages can start only during a clear preparation night. No charge was made."); return false; }
            if (!SaveStore.Write(SavePath,CaptureStaffSlot(stagedGame,stagedStaff),out var error))
            { SaveError=error; Notify("Staff change was not saved. Your money and previous schedule are unchanged."); return false; }
            // Never replace a live daytime model: queued visitors hold its reservation authority.
            if (cashChanged) { Stocker.ResetTransient();Technician.ResetTransient();Model=stagedGame; }
            Staff=stagedStaff;SaveError=null;saveClock=0;
            Cashier.ResetTransient();Cashier.RefreshVisual();
            World.Refresh(Model);
            Notify(successMessage+ (Staff.Hired && Staff.Scheduled && !Staff.Paused ? StaffPaymentLine() : ""));
            return true;
        }

        SlotData CaptureStaffSlot(GameModel game,ShopStaff staff,ShopStocking stocking=null,ShopTradeLedger reports=null,ShopLaboratory laboratory=null) => new SlotData
        {
            game=game.CaptureSave(),cycle=Cycle.CaptureSave(),trades=(reports??Reports).CaptureSave(),
            reputation=Reputation.CaptureSave(),layout=Layout.CaptureSave(),staff=staff.CaptureSave(),stocker=(stocking??Stocking).CaptureSave(game),technician=(laboratory??Laboratory).CaptureSave(game),
            sensitivity=Sensitivity,volume=Volume,slapKey=SlapKey,humorVolume=HumorVolume,reducedGags=ReducedGags
        };

        public bool ServeCashier(CustomerVisit expected)
        {
            if (Paused || PlacementActive || !Cashier || !Cashier.isActiveAndEnabled || !Cashier.Actor ||
                !Cashier.Actor.isActiveAndEnabled || !Staff.IsOnDuty(Cycle.DayNumber) ||
                (Cycle.Phase!=ShopDayPhase.Open && Cycle.Phase!=ShopDayPhase.Closing) ||
                expected==null || Cashier.Progress<1f || !ReferenceEquals(Cashier.Target,expected)) return false;
            int agreed=expected.Price;
            var result=Customers.ServeExpected(expected);
            if (!result.HasValue) return false;
            bool success=Perform(result.Value).Success;
            if (success) Notify("Pip served a customer / "+agreed+" credits received.");
            return success;
        }

        string StaffPaymentLine() => Staff.PaidDay==Cycle.DayNumber ?
            Staff.PaidWage+" credits paid for preparation night + trading day "+Cycle.DayNumber+"." :
            "Off duty for day "+Cycle.DayNumber+" / "+ShopStaff.CashierWage+" credits needed at night. You can work manually.";

        void DrawStaffPanel()
        {
            Heading("Your shop crew");
            GUILayout.BeginHorizontal();
            Button("Nori / stocker",()=>OpenStockerMenu());
            Button("Moss / lab technician",()=>OpenTechnicianMenu());
            GUILayout.EndHorizontal();
            Space();
            Text("Pip / cashier");
            Small("Serves queued customers while you grow and restock. You can still check out any order yourself.");
            Text(ShopStaff.CashierWage+" credits per duty period / no hiring fee");
            Small("Covers preparation night and the following trading day. Scheduled staff are paid at each new night if you have enough cash. Started wages are not refundable.");
            Space();
            Text(Cashier.Status);
            Small(StaffPaymentLine());
            bool night=Cycle.Phase==ShopDayPhase.Night;
            if (!night) Small("Hire, change schedules and pay wages during night preparation.");
            if (!Staff.Hired)
                StaffButton("Hire Pip / initially off schedule",night,()=>HireCashier());
            else
            {
                if (Staff.Scheduled)
                    StaffButton("Stop recurring schedule",night,()=>SetCashierScheduled(false));
                else
                    StaffButton(Staff.PaidDay==Cycle.DayNumber ? "Schedule Pip / this period already paid" : "Schedule Pip / pay "+ShopStaff.CashierWage+" credits",night,()=>SetCashierScheduled(true));
                if (Staff.Scheduled && Staff.PaidDay!=Cycle.DayNumber)
                    StaffButton("Pay "+ShopStaff.CashierWage+" credits / start this duty period",night && !Staff.Paused && Model.Currency>=ShopStaff.CashierWage,()=>PayCashierDuty());
                StaffButton(Staff.Paused ? "Resume work" : "Pause work / no refund",true,()=>SetCashierPaused(!Staff.Paused));
                StaffButton("Dismiss Pip / no wage refund",night,()=>DismissCashier());
            }
            Space();
            Button("Back to the sales counter",()=>SetPanel(ShopPanel.Counter));
        }
        void StaffButton(string label,bool enabled,Action action)
        {
            bool previous=GUI.enabled;GUI.enabled=previous&&enabled;Button(label,action);GUI.enabled=previous;
        }
    }
}
