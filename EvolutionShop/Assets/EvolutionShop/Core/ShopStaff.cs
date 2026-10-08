using System;

namespace EvolutionShop
{
    [Serializable]
    public sealed class StaffSave
    {
        // Missing data is invalid; only CaptureSave supplies the current version and identity.
        public int version;
        public string employeeId;
        public bool hired;
        public bool scheduled;
        public bool paused;
        public int paidDay;
        public int paidWage;
        public int previousPaidDay;
        public int previousPaidWage;
    }

    /// <summary>One employee's employment and paid night/day periods; no scene or job ownership.</summary>
    public sealed class ShopStaff
    {
        public const int SaveVersion = 1;
        public const string CashierId = "cashier-pip";
        public const int CashierWage = 4;
        public const string StockerId = "stocker-nori";
        public const int StockerWage = 4;
        public const string TechnicianId = "technician-moss";
        public const int TechnicianWage = 4;

        public string EmployeeId { get; }
        public int Wage { get; }

        public ShopStaff(string employeeId = CashierId)
        {
            if (!KnownEmployee(employeeId)) throw new ArgumentException("Unknown employee.", nameof(employeeId));
            EmployeeId = employeeId;
            Wage = WageFor(employeeId);
        }

        public bool Hired { get; private set; }
        public bool Scheduled { get; private set; }
        public bool Paused { get; private set; }
        public int PaidDay { get; private set; }
        public int PaidWage { get; private set; }
        public int PreviousPaidDay { get; private set; }
        public int PreviousPaidWage { get; private set; }

        public bool Hire(ShopDayCycle cycle)
        {
            if (cycle == null || cycle.Phase != ShopDayPhase.Night || Hired) return false;
            Hired = true;
            Scheduled = false;
            Paused = false;
            return true;
        }

        public bool Dismiss(ShopDayCycle cycle)
        {
            if (cycle == null || cycle.Phase != ShopDayPhase.Night || !Hired) return false;
            Hired = Scheduled = Paused = false;
            // Receipts survive dismissal so re-hiring cannot charge twice or refund a period.
            return true;
        }

        public bool SetScheduled(bool scheduled, ShopDayCycle cycle)
        {
            if (cycle == null || cycle.Phase != ShopDayPhase.Night || !Hired || Scheduled == scheduled) return false;
            Scheduled = scheduled;
            return true;
        }

        public bool SetPaused(bool paused)
        {
            if (!Hired || Paused == paused) return false;
            Paused = paused;
            return true;
        }

        public bool TryBeginDuty(GameModel game, ShopDayCycle cycle)
        {
            if (game == null || cycle == null || cycle.Phase != ShopDayPhase.Night ||
                !Hired || !Scheduled || Paused || cycle.DayNumber < PaidDay) return false;
            if (PaidDay == cycle.DayNumber) return true;
            // No callbacks/allocations occur after spending: the charge and its receipt commit
            // together on the simulation thread. Failed spending changes neither authority.
            if (!game.TrySpendCredits(Wage)) return false;
            PreviousPaidDay = PaidDay;
            PreviousPaidWage = PaidWage;
            PaidDay = cycle.DayNumber;
            PaidWage = Wage;
            return true;
        }

        public bool IsOnDuty(int day) => day > 0 && Hired && Scheduled && !Paused && PaidDay == day;

        public int WagesForDay(int day) => day <= 0 ? 0 : day == PaidDay ? PaidWage :
            day == PreviousPaidDay ? PreviousPaidWage : 0;

        public StaffSave CaptureSave() => new StaffSave
        {
            version = SaveVersion, employeeId = EmployeeId, hired = Hired, scheduled = Scheduled,
            paused = Paused, paidDay = PaidDay, paidWage = PaidWage,
            previousPaidDay = PreviousPaidDay, previousPaidWage = PreviousPaidWage
        };

        public static bool TryRestore(StaffSave save, int day, out ShopStaff staff) =>
            TryRestore(save, day, CashierId, out staff);

        public static bool TryRestore(StaffSave save, int day, string employeeId, out ShopStaff staff)
        {
            staff = null;
            if (!KnownEmployee(employeeId) || save == null || save.version != SaveVersion || save.employeeId != employeeId || day < 1 ||
                (!save.hired && (save.scheduled || save.paused)) ||
                !ValidReceipt(save.paidDay, save.paidWage, day, WageFor(employeeId)) ||
                !ValidReceipt(save.previousPaidDay, save.previousPaidWage, day, WageFor(employeeId)) ||
                (save.previousPaidDay > 0 && save.previousPaidDay >= save.paidDay)) return false;
            staff = new ShopStaff(employeeId)
            {
                Hired = save.hired, Scheduled = save.scheduled, Paused = save.paused,
                PaidDay = save.paidDay, PaidWage = save.paidWage,
                PreviousPaidDay = save.previousPaidDay, PreviousPaidWage = save.previousPaidWage
            };
            return true;
        }

        private static bool KnownEmployee(string employeeId) => employeeId == CashierId || employeeId == StockerId || employeeId == TechnicianId;
        private static int WageFor(string employeeId) => employeeId == TechnicianId ? TechnicianWage : employeeId == StockerId ? StockerWage : CashierWage;

        private static bool ValidReceipt(int paidDay, int wage, int day, int expectedWage) =>
            paidDay == 0 ? wage == 0 : paidDay > 0 && paidDay <= day && wage == expectedWage;
    }
}
