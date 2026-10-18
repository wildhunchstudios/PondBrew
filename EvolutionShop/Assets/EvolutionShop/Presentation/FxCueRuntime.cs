using System;

namespace EvolutionShop
{
    // Typed presentation vocabulary from FX-01. Only three cues have render bindings in FX-02.
    public enum FxCueId
    {
        None, GrowerLocked, GrowerFull, GrowerRefilling, GrowerEmpty, GrowerCollecting,
        CollectedUnit, IncubatorDosingEmpty, IncubatorDosingStaged, DoseCommitted, DoseReturned,
        IncubationStarted, IncubatorWorking, HatchFirst, HatchRepeat, IncubatorReady, ReadyOutputBlocked,
        OutputRemoved, ModuleOff, ModuleWaiting, ModuleBlocked, ModuleCollected, ModuleDosed,
        HandIdle, HandMove, HandCollectStart, HandCollectHold, HandCollectEnd, HandDose, HandPlace,
        HandRefused, HandUpgrade, HandSlap, TankEmpty, TankOccupied, TankReserved, TankHabitat,
        PondletCruise, PondletPause, PondletGlance, PondletPlaced, SaleCommitted, CustomerPriceAccepted, CustomerPriceRefused, WorkerPickup, WorkerDose, WorkerPlace, WorkerCheckout, WorkerRefused, ComedyBeat, ComedyContact
    }
    public enum FxCueOrigin { Manual, Worker, Module }
    public enum FxSteadyState { Empty, Full, Refilling, Dosing, Working, Ready, Occupied, Reserved }
    public readonly struct FxOwnerToken : IEquatable<FxOwnerToken>
    {
        public readonly int Slot;
        public readonly uint Epoch, Generation;
        public bool IsDefined => Epoch != 0 && Generation != 0;
        internal FxOwnerToken(int slot, uint epoch, uint generation) { Slot = slot; Epoch = epoch; Generation = generation; }
        public bool Equals(FxOwnerToken other) => Slot == other.Slot && Epoch == other.Epoch && Generation == other.Generation;
        public override bool Equals(object obj) => obj is FxOwnerToken other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397 ^ (int)Epoch) * 397 ^ (int)Generation);
    }

    /// <summary>Bounded, local presentation lifetime/priority only. No callbacks or gameplay dependencies.</summary>
    public sealed class FxCueRuntime : IDisposable
    {
        public const int OwnerLimit = 64, ActiveLimit = 16, HistoryLimit = 64;
        struct Owner
        {
            public string Key, Identity;
            public uint Generation, LastSequence;
            public ulong LastMask;
            public FxSteadyState State;
        }
        struct Active
        {
            public FxOwnerToken Owner;
            public FxCueId Id;
            public FxCueOrigin Origin;
            public int Priority;
            public float Remaining;
        }
        readonly Owner[] owners = new Owner[OwnerLimit];
        readonly Active[] active = new Active[ActiveLimit];
        readonly Active[] history = new Active[HistoryLimit];
        int historyCursor;
        bool disposed;
        public uint Epoch { get; private set; } = 1;
        public int AcceptedCount { get; private set; }
        public int SuppressedCount { get; private set; }
        public int ActiveCount { get { int count = 0; for (int i = 0; i < active.Length; i++) if (active[i].Remaining > 0) count++; return count; } }
        public int OwnerCount { get { int count = 0; for (int i = 0; i < owners.Length; i++) if (owners[i].Key != null) count++; return count; } }
        public FxOwnerToken Find(string key)
        {
            if (!disposed && key != null) for (int i = 0; i < owners.Length; i++)
                if (owners[i].Key == key) return new FxOwnerToken(i, Epoch, owners[i].Generation);
            return default;
        }
        public FxOwnerToken Register(string key, string identity)
        {
            if (disposed || string.IsNullOrEmpty(key)) return default;
            int free = -1;
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i].Key == null) { if (free < 0) free = i; continue; }
                if (owners[i].Key != key) continue;
                if (owners[i].Identity == identity) return new FxOwnerToken(i, Epoch, owners[i].Generation);
                Unregister(new FxOwnerToken(i, Epoch, owners[i].Generation)); free = i; break;
            }
            if (free < 0) return default;
            uint next = owners[free].Generation + 1; if (next == 0) next = 1;
            owners[free] = new Owner { Key = key, Identity = identity, Generation = next };
            return new FxOwnerToken(free, Epoch, next);
        }
        public bool IsValid(FxOwnerToken owner) => !disposed && owner.Epoch == Epoch && owner.IsDefined &&
            owner.Slot >= 0 && owner.Slot < owners.Length && owners[owner.Slot].Key != null && owners[owner.Slot].Generation == owner.Generation;
        public void SetState(FxOwnerToken owner, FxSteadyState state) { if (IsValid(owner)) owners[owner.Slot].State = state; }
        public bool TryState(FxOwnerToken owner, out FxSteadyState state)
        { state = IsValid(owner) ? owners[owner.Slot].State : default; return IsValid(owner); }
        public bool Play(FxOwnerToken owner, FxCueId id, uint sequence, FxCueOrigin origin, float seconds, int priority)
        {
            if (!IsValid(owner) || id <= FxCueId.None || id > FxCueId.ComedyContact || sequence == 0 ||
                seconds <= 0 || seconds > 1.5f || float.IsNaN(seconds) || priority < 0 || priority > 5 ||
                origin < FxCueOrigin.Manual || origin > FxCueOrigin.Module) return false;
            ref Owner record = ref owners[owner.Slot];
            ulong mask = 1UL << (int)id;
            // The per-owner high water/mask also blocks late duplicates after bounded history rotates.
            if (sequence < record.LastSequence || (sequence == record.LastSequence && (record.LastMask & mask) != 0))
            { SuppressedCount++; return false; }
            if (sequence > record.LastSequence) { record.LastSequence = sequence; record.LastMask = 0; }
            record.LastMask |= mask;
            int selected = -1, weakest = 0;
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i].Remaining <= 0) { if (selected < 0) selected = i; }
                else if (active[i].Owner.Equals(owner) && active[i].Id == id) { selected = i; break; }
                if (active[i].Priority < active[weakest].Priority) weakest = i;
            }
            if (selected < 0 && active[weakest].Priority < priority) selected = weakest;
            if (selected < 0) { SuppressedCount++; return false; }
            active[selected] = new Active { Owner = owner, Id = id, Origin = origin, Remaining = seconds, Priority = priority };
            history[historyCursor] = active[selected]; historyCursor = (historyCursor + 1) % history.Length;
            AcceptedCount++; return true;
        }
        public void Tick(float seconds)
        {
            if (disposed || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            for (int i = 0; i < active.Length; i++)
                if (active[i].Remaining > 0) { active[i].Remaining = Math.Max(0, active[i].Remaining - seconds); if (active[i].Remaining == 0) active[i] = default; }
        }
        public void Cancel(FxOwnerToken owner)
        { for (int i = 0; i < active.Length; i++) if (active[i].Owner.Equals(owner)) active[i] = default; }
        public void CancelAll() { Array.Clear(active, 0, active.Length); }
        public void Unregister(FxOwnerToken owner)
        {
            if (!IsValid(owner)) return;
            Cancel(owner);
            uint generation = owners[owner.Slot].Generation;
            owners[owner.Slot] = new Owner { Generation = generation };
        }
        public void Reset()
        {
            if (disposed) return;
            Epoch++; if (Epoch == 0) Epoch = 1;
            Array.Clear(owners, 0, owners.Length); Array.Clear(active, 0, active.Length); Array.Clear(history, 0, history.Length); historyCursor = 0;
        }
        public void Dispose() { Reset(); disposed = true; }
    }
}
