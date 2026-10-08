using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    public readonly struct ConditionerView
    {
        public readonly string Id;
        public readonly ConditionerPhase Phase;
        public readonly float SecondsRemaining;
        public readonly SpecimenView? First;
        public readonly SpecimenView? Second;
        public int Capacity => 2;
        public int Count => (First.HasValue ? 1 : 0) + (Second.HasValue ? 1 : 0);
        internal ConditionerView(string id, ConditionerPhase phase, float remaining, SpecimenView? first, SpecimenView? second)
        { Id = id; Phase = phase; SecondsRemaining = remaining; First = first; Second = second; }
    }

    [Serializable]
    public sealed class ConditionerSave
    {
        public string conditionerId;
        public ConditionerPhase phase;
        public float secondsRemaining;
        public string firstSpecimenId;
        public string firstSpeciesId;
        public string secondSpecimenId;
        public string secondSpeciesId;
    }

    public sealed partial class GameModel
    {
        private sealed class ConditionerState
        {
            public ConditionerPhase Phase;
            public float Remaining;
            public SpecimenView? First, Second;
            public int Count => (First.HasValue ? 1 : 0) + (Second.HasValue ? 1 : 0);
        }
        private Dictionary<string, ConditionerState> conditioners = new Dictionary<string, ConditionerState>(StringComparer.Ordinal);
        public string[] ConditionerIds => (string[])config.ConditionerIds.Clone();
        private int ReservedConditioningSpark
        {
            get
            {
                int count = 0;
                foreach (var machine in conditioners.Values)
                    if (machine.Phase == ConditionerPhase.Processing) count++;
                return count;
            }
        }

        public ConditionerView? GetConditioner(string id)
            => id != null && conditioners.TryGetValue(id, out var machine)
                ? new ConditionerView(id, machine.Phase, machine.Remaining, machine.First, machine.Second)
                : (ConditionerView?)null;

        /// <summary>Move one or two exact rack residents into a batch. One Spark is reserved until completion or cancellation.</summary>
        public ActionResult BeginConditioning(string id, string[] sourceStorageIds, string[] expectedSpecimenIds)
        {
            if (id == null || !conditioners.TryGetValue(id, out var machine)) return ActionResult.Fail(FailureCode.UnknownConditioner);
            if (machine.Phase != ConditionerPhase.Idle || machine.Count != 0) return ActionResult.Fail(FailureCode.ConditionerBusy);
            if (!SparkResearchUnlocked) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (!species.ContainsKey("pondlet") || !species.ContainsKey("lanternfin")) return ActionResult.Fail(FailureCode.UnknownSpecies);
            if (!discoveries.Contains("lanternfin")) return ActionResult.Fail(FailureCode.UndiscoveredRecipe);
            if (sourceStorageIds == null || expectedSpecimenIds == null || sourceStorageIds.Length < 1 || sourceStorageIds.Length > 2 ||
                sourceStorageIds.Length != expectedSpecimenIds.Length ||
                sourceStorageIds.Length == 2 && (sourceStorageIds[0] == sourceStorageIds[1] || expectedSpecimenIds[0] == expectedSpecimenIds[1]))
                return ActionResult.Fail(FailureCode.InvalidConditioningInput);
            SpecimenView? first = null, second = null;
            for (int i = 0; i < sourceStorageIds.Length; i++)
            {
                string source = sourceStorageIds[i];
                if (source == null || !storage.TryGetValue(source, out var resident)) return ActionResult.Fail(FailureCode.UnknownStorage);
                if (IsStorageReservedForStocking(source)) return ActionResult.Fail(FailureCode.StockReserved);
                if (!resident.HasValue) return ActionResult.Fail(FailureCode.StorageEmpty);
                if (string.IsNullOrWhiteSpace(expectedSpecimenIds[i]) || resident.Value.Id != expectedSpecimenIds[i])
                    return ActionResult.Fail(FailureCode.SpecimenMismatch);
                if (resident.Value.SpeciesId != "pondlet") return ActionResult.Fail(FailureCode.InvalidConditioningInput);
                if (i == 0) first = resident; else second = resident;
            }
            if (available[(int)CultureKind.Spark] < 1) return ActionResult.Fail(FailureCode.InsufficientCulture);
            // Every possible rejection precedes this publication of the ownership change.
            foreach (string source in sourceStorageIds) storage[source] = null;
            available[(int)CultureKind.Spark]--;
            machine.First = first; machine.Second = second;
            machine.Phase = ConditionerPhase.Processing; machine.Remaining = config.ConditioningSeconds;
            return ActionResult.Ok();
        }

        /// <summary>Retain the original Pondlets as ready outputs and return the reserved Spark into its guaranteed space.</summary>
        public ActionResult CancelConditioning(string id)
        {
            if (id == null || !conditioners.TryGetValue(id, out var machine)) return ActionResult.Fail(FailureCode.UnknownConditioner);
            if (machine.Phase != ConditionerPhase.Processing) return ActionResult.Fail(FailureCode.ConditionerNotProcessing);
            machine.Phase = ConditionerPhase.Ready; machine.Remaining = 0;
            available[(int)CultureKind.Spark]++;
            return ActionResult.Ok();
        }

        public ActionResult TransferConditionerToStorage(string id, int slot, string storageId, string expectedSpecimenId)
        {
            var result = ReadyConditionerResident(id, slot, expectedSpecimenId, out var machine, out var resident);
            if (!result.Success) return result;
            if (storageId == null || !storage.TryGetValue(storageId, out var target)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (IsStorageReservedForStocking(storageId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (target.HasValue) return ActionResult.Fail(FailureCode.StorageFull);
            storage[storageId] = resident;
            DrainConditionerSlot(machine, slot);
            return ActionResult.Ok(resident.SpeciesId, resident.Id);
        }

        public ActionResult TransferConditionerToTank(string id, int slot, string tankId, string expectedSpecimenId)
        {
            var result = ReadyConditionerResident(id, slot, expectedSpecimenId, out var machine, out var resident);
            if (!result.Success) return result;
            if (tankId == null || !tanks.TryGetValue(tankId, out var target)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (reservations.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.TankReserved);
            if (IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (target.HasValue) return ActionResult.Fail(FailureCode.TankFull);
            tanks[tankId] = resident;
            DrainConditionerSlot(machine, slot);
            return ActionResult.Ok(resident.SpeciesId, resident.Id);
        }

        private ActionResult ReadyConditionerResident(string id, int slot, string expectedId, out ConditionerState machine, out SpecimenView resident)
        {
            resident = default; machine = null;
            if (id == null || !conditioners.TryGetValue(id, out machine)) return ActionResult.Fail(FailureCode.UnknownConditioner);
            if (slot < 0 || slot > 1) return ActionResult.Fail(FailureCode.InvalidConditioningInput);
            if (machine.Phase != ConditionerPhase.Ready) return ActionResult.Fail(FailureCode.OutputNotReady);
            var value = slot == 0 ? machine.First : machine.Second;
            if (!value.HasValue) return ActionResult.Fail(FailureCode.AlreadyEmpty);
            if (string.IsNullOrWhiteSpace(expectedId) || value.Value.Id != expectedId) return ActionResult.Fail(FailureCode.SpecimenMismatch);
            resident = value.Value;
            return ActionResult.Ok();
        }

        private static void DrainConditionerSlot(ConditionerState machine, int slot)
        {
            if (slot == 0) machine.First = null; else machine.Second = null;
            if (machine.Count == 0) { machine.Phase = ConditionerPhase.Idle; machine.Remaining = 0; }
        }

        private int TickConditioners(float seconds)
        {
            int completed = 0;
            foreach (var machine in conditioners.Values)
            {
                if (machine.Phase != ConditionerPhase.Processing) continue;
                machine.Remaining = Math.Max(0f, machine.Remaining - seconds);
                if (machine.Remaining > 0f) continue;
                if (machine.First.HasValue) machine.First = new SpecimenView(machine.First.Value.Id, "lanternfin");
                if (machine.Second.HasValue) machine.Second = new SpecimenView(machine.Second.Value.Id, "lanternfin");
                machine.Phase = ConditionerPhase.Ready;
                completed += machine.Count;
            }
            return completed;
        }

        private ConditionerSave[] CaptureConditioners()
        {
            var result = new ConditionerSave[config.ConditionerIds.Length];
            for (int i = 0; i < result.Length; i++)
            {
                string id = config.ConditionerIds[i]; var machine = conditioners[id];
                result[i] = new ConditionerSave
                {
                    conditionerId = id, phase = machine.Phase, secondsRemaining = machine.Remaining,
                    firstSpecimenId = machine.First?.Id, firstSpeciesId = machine.First?.SpeciesId,
                    secondSpecimenId = machine.Second?.Id, secondSpeciesId = machine.Second?.SpeciesId
                };
            }
            return result;
        }

        private void RestoreConditioners(ConditionerSave[] saves)
        {
            foreach (var save in saves)
            {
                var machine = conditioners[save.conditionerId];
                machine.Phase = save.phase; machine.Remaining = save.secondsRemaining;
                machine.First = string.IsNullOrEmpty(save.firstSpecimenId) ? (SpecimenView?)null : new SpecimenView(save.firstSpecimenId, save.firstSpeciesId);
                machine.Second = string.IsNullOrEmpty(save.secondSpecimenId) ? (SpecimenView?)null : new SpecimenView(save.secondSpecimenId, save.secondSpeciesId);
            }
        }

        private bool ValidConditionerSave(SaveData save, HashSet<string> known, HashSet<string> specimens, int capacity)
        {
            if (save.version < 5)
                return config.ConditionerIds.Length == 0 && (save.conditioners == null || save.conditioners.Length == 0);
            if (save.conditioners == null || save.conditioners.Length != config.ConditionerIds.Length) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int reservedSpark = 0;
            foreach (var machine in save.conditioners)
            {
                if (machine == null || machine.conditionerId == null || !conditioners.ContainsKey(machine.conditionerId) || !seen.Add(machine.conditionerId) ||
                    !Finite(machine.secondsRemaining) || machine.secondsRemaining < 0 || machine.secondsRemaining > config.ConditioningSeconds) return false;
                if (!ValidConditionerResident(machine.firstSpecimenId, machine.firstSpeciesId, known, specimens) ||
                    !ValidConditionerResident(machine.secondSpecimenId, machine.secondSpeciesId, known, specimens)) return false;
                bool first = !string.IsNullOrEmpty(machine.firstSpecimenId), second = !string.IsNullOrEmpty(machine.secondSpecimenId);
                int count = (first ? 1 : 0) + (second ? 1 : 0);
                if (machine.phase == ConditionerPhase.Idle)
                {
                    if (count != 0 || machine.secondsRemaining != 0) return false;
                    continue;
                }
                if (count == 0 || !save.sparkResearchUnlocked || !known.Contains("lanternfin") || !known.Contains("pondlet")) return false;
                if (machine.phase == ConditionerPhase.Processing)
                {
                    // Inputs always fill slot zero first. Ready outputs alone can contain holes.
                    if (!first || machine.secondsRemaining <= 0 || machine.firstSpeciesId != "pondlet" || second && machine.secondSpeciesId != "pondlet") return false;
                    reservedSpark++;
                }
                else if (machine.phase == ConditionerPhase.Ready)
                {
                    if (machine.secondsRemaining != 0 || first && second && machine.firstSpeciesId != machine.secondSpeciesId) return false;
                }
                else return false;
            }
            var inc = save.incubator;
            return (long)save.availableBloom + save.availableFlow + save.availableSpark + save.availableMist + inc.stagedBloom + inc.stagedFlow + inc.stagedSpark + inc.stagedMist + reservedSpark <= capacity;
        }

        private static bool ValidConditionerResident(string id, string speciesId, HashSet<string> known, HashSet<string> specimens)
        {
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(speciesId)) return true;
            return !string.IsNullOrWhiteSpace(id) && (speciesId == "pondlet" || speciesId == "lanternfin") &&
                known.Contains(speciesId) && specimens.Add(id);
        }
    }
}
