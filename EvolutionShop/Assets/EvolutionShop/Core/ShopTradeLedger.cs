using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    [Serializable]
    public sealed class TradeDaySave
    {
        public int dayNumber;
        public bool partial;
        public int visitors;
        public int sales;
        public int noStock;
        public int impatient;
        public int closed;
        public int interrupted;
        public int pathProblems;
        // Report version 2. Version 1 reports had no price refusals.
        public int tooExpensive;
        public int stockedAtOpen;
        public int stockedAtClose;
        public int restocked;
        public long revenue;
    }

    [Serializable]
    public sealed class TradeLedgerSave
    {
        public int version = ShopTradeLedger.SaveVersion;
        public bool hasCurrent;
        public bool hasLast;
        public TradeDaySave current;
        public TradeDaySave last;
        // Versions 1-2: at most one unresolved visitor.
        public string pendingCustomerId;
        // Version 3: every unresolved visitor, in arrival order.
        public string[] pendingCustomerIds;
    }

    /// <summary>
    /// Observes committed shop events without owning stock, payment, rewards or the day clock.
    /// Keeps only the current day, last completed day, and the unresolved visitor identities.
    /// </summary>
    public sealed class ShopTradeLedger
    {
        public const int SaveVersion = 3;
        public const int MaxCounter = 1000000;
        public const long MaxRevenue = 1000000000000L;
        /// <summary>Unresolved visitors at once; above the shop's visitor cap so it never limits play.</summary>
        public const int MaxPending = 4;

        TradeDaySave current;
        TradeDaySave last;
        readonly List<string> pending = new List<string>();

        public TradeDaySave Current => Copy(current);
        public TradeDaySave Last => Copy(last);
        public int LastDayNumber => last == null ? 0 : last.dayNumber;
        public bool HasPending => pending.Count > 0;
        public int PendingCount => pending.Count;
        public bool IsPending(string id) => id != null && pending.Contains(id);

        public bool BeginDay(int dayNumber, int stockedCount)
        {
            if (current != null || dayNumber < 1 || dayNumber == int.MaxValue || !Counter(stockedCount) ||
                (last != null && (long)last.dayNumber + 1 != dayNumber)) return false;
            current = new TradeDaySave { dayNumber = dayNumber, stockedAtOpen = stockedCount };
            return true;
        }

        public bool BeginVisit(string id)
        {
            if (current == null || pending.Count >= MaxPending || !CustomerId(id) || pending.Contains(id) ||
                current.visitors == MaxCounter) return false;
            current.visitors++;
            pending.Add(id);
            return true;
        }

        /// <summary>Record Sold at payment, not departure. Resolve other outcomes when a visit is cancelled.</summary>
        public bool ResolveVisit(string id, CustomerOutcome outcome, int price = 0)
        {
            if (current == null || !IsPending(id) || price < 0 ||
                (outcome != CustomerOutcome.Sold && price != 0)) return false;
            switch (outcome)
            {
                case CustomerOutcome.Sold:
                    if (price > MaxRevenue - current.revenue) return false;
                    current.sales++;
                    current.revenue += price;
                    break;
                case CustomerOutcome.NoStock: current.noStock++; break;
                case CustomerOutcome.Impatient: current.impatient++; break;
                case CustomerOutcome.Closed: current.closed++; break;
                case CustomerOutcome.OrderLost: current.interrupted++; break;
                case CustomerOutcome.PathBlocked: current.pathProblems++; break;
                case CustomerOutcome.TooExpensive: current.tooExpensive++; break;
                default: return false;
            }
            pending.Remove(id);
            return true;
        }

        public bool RecordRestock()
        {
            if (current == null || current.restocked == MaxCounter) return false;
            current.restocked++;
            return true;
        }

        public bool FinishDay(int stockedCount)
        {
            if (current == null || !Counter(stockedCount)) return false;
            foreach (string id in pending.ToArray()) ResolveVisit(id, CustomerOutcome.Closed);
            current.stockedAtClose = stockedCount;
            last = current;
            current = null;
            return true;
        }

        /// <summary>Reloading cancels every unpaid visit: each counts once as interrupted.</summary>
        public bool CancelPendingForReload()
        {
            if (!HasPending) return false;
            foreach (string id in pending.ToArray()) ResolveVisit(id, CustomerOutcome.OrderLost);
            return true;
        }

        public TradeLedgerSave CaptureSave()
        {
            return new TradeLedgerSave
            {
                version = SaveVersion, hasCurrent = current != null, hasLast = last != null,
                current = Copy(current), last = Copy(last), pendingCustomerIds = pending.ToArray()
            };
        }

        public static bool TryRestore(TradeLedgerSave save, ShopDaySave cycle, out ShopTradeLedger ledger)
        {
            ledger = null;
            if (save == null || save.version < 1 || save.version > SaveVersion ||
                !ShopDayCycle.TryRestore(cycle, out _)) return false;
            // Version 1 had no price refusals; a nonzero count there is corrupt rather than migrated.
            if (save.version == 1 && ((save.current != null && save.current.tooExpensive != 0) ||
                (save.last != null && save.last.tooExpensive != 0))) return false;
            var unresolved = new List<string>();
            if (save.version < 3)
            {
                // Versions 1-2 held one visitor in the single field; the list did not exist.
                if (save.pendingCustomerIds != null && save.pendingCustomerIds.Length != 0) return false;
                if (!string.IsNullOrEmpty(save.pendingCustomerId)) unresolved.Add(save.pendingCustomerId);
            }
            else
            {
                if (!string.IsNullOrEmpty(save.pendingCustomerId)) return false;
                if (save.pendingCustomerIds != null)
                {
                    if (save.pendingCustomerIds.Length > MaxPending) return false;
                    unresolved.AddRange(save.pendingCustomerIds);
                }
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in unresolved) if (!CustomerId(id) || !seen.Add(id)) return false;
            bool active = cycle.phase == ShopDayPhase.Open || cycle.phase == ShopDayPhase.Closing;
            if (active != save.hasCurrent || (!active && unresolved.Count > 0)) return false;
            // Unity's inline-class serializer may replace null with a default object. Presence flags
            // distinguish that empty sentinel from a real report; nonempty hidden payloads are corrupt.
            if ((!save.hasCurrent && !EmptyDay(save.current)) || (!save.hasLast && !EmptyDay(save.last))) return false;
            if (save.hasCurrent && (save.current == null || !ValidDay(save.current, unresolved.Count) ||
                save.current.dayNumber != cycle.dayNumber || save.current.stockedAtClose != 0)) return false;
            if (save.hasLast && (save.last == null || !ValidDay(save.last, 0) ||
                (long)save.last.dayNumber + 1 != cycle.dayNumber)) return false;

            ledger = new ShopTradeLedger
            {
                current = save.hasCurrent ? Copy(save.current) : null,
                last = save.hasLast ? Copy(save.last) : null
            };
            ledger.pending.AddRange(unresolved);
            return true;
        }

        /// <summary>Old slots contain no report history. Active-day counts begin explicitly as partial.</summary>
        public static ShopTradeLedger LegacyForCycle(ShopDaySave cycle, int stockedCount)
        {
            if (!ShopDayCycle.TryRestore(cycle, out _)) throw new ArgumentException("Invalid saved day clock.", nameof(cycle));
            if (!Counter(stockedCount)) throw new ArgumentOutOfRangeException(nameof(stockedCount));
            var ledger = new ShopTradeLedger();
            if (cycle.phase != ShopDayPhase.Night)
            {
                ledger.BeginDay(cycle.dayNumber, stockedCount);
                ledger.current.partial = true;
            }
            return ledger;
        }

        static bool ValidDay(TradeDaySave day, int unresolved)
        {
            if (day.dayNumber < 1 || day.dayNumber == int.MaxValue ||
                !Counter(day.visitors) || !Counter(day.sales) || !Counter(day.noStock) ||
                !Counter(day.impatient) || !Counter(day.closed) || !Counter(day.interrupted) ||
                !Counter(day.pathProblems) || !Counter(day.tooExpensive) ||
                !Counter(day.stockedAtOpen) || !Counter(day.stockedAtClose) ||
                !Counter(day.restocked) || day.revenue < 0 || day.revenue > MaxRevenue ||
                day.revenue > (long)day.sales * int.MaxValue) return false;
            long outcomes = (long)day.sales + day.noStock + day.impatient + day.closed + day.interrupted +
                day.pathProblems + day.tooExpensive;
            return outcomes + unresolved == day.visitors;
        }

        static bool Counter(int value) => value >= 0 && value <= MaxCounter;
        static bool CustomerId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        static bool EmptyDay(TradeDaySave day)
        {
            return day == null || (day.dayNumber == 0 && !day.partial && day.visitors == 0 && day.sales == 0 &&
                day.noStock == 0 && day.impatient == 0 && day.closed == 0 && day.interrupted == 0 &&
                day.pathProblems == 0 && day.tooExpensive == 0 && day.stockedAtOpen == 0 && day.stockedAtClose == 0 &&
                day.restocked == 0 && day.revenue == 0);
        }
        static TradeDaySave Copy(TradeDaySave day)
        {
            if (day == null) return null;
            return new TradeDaySave
            {
                dayNumber = day.dayNumber, partial = day.partial, visitors = day.visitors,
                sales = day.sales, noStock = day.noStock, impatient = day.impatient, closed = day.closed,
                interrupted = day.interrupted, pathProblems = day.pathProblems, tooExpensive = day.tooExpensive,
                stockedAtOpen = day.stockedAtOpen, stockedAtClose = day.stockedAtClose,
                restocked = day.restocked, revenue = day.revenue
            };
        }
    }
}
