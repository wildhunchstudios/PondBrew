using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    [Serializable]
    public sealed class LabOrderSave
    {
        public string speciesId;
        public int count;
    }

    [Serializable]
    public sealed class TechnicianSave
    {
        public int version;
        public StaffSave employee;
        public LabOrderSave[] queue;
        public string activeSpeciesId;
        public string activeSpecimenId;
    }

    /// <summary>Finite known-recipe priorities and one durable started batch; owns no creatures or claims.</summary>
    public sealed class ShopLaboratory
    {
        public const int SaveVersion = 1;
        public const int MaximumOrderCount = 9;
        readonly List<LabOrderSave> queue = new List<LabOrderSave>();
        public ShopStaff Employee { get; private set; } = new ShopStaff(ShopStaff.TechnicianId);
        public string ActiveSpeciesId { get; private set; }
        public string ActiveSpecimenId { get; private set; }
        public LabOrderSave[] Queue
        {
            get
            {
                var result = new LabOrderSave[queue.Count];
                for (int i = 0; i < queue.Count; i++)
                    result[i] = new LabOrderSave { speciesId = queue[i].speciesId, count = queue[i].count };
                return result;
            }
        }

        public bool AddOrder(GameModel game, string speciesId, int count = 1)
        {
            if (!ValidSpecies(game, speciesId) || count < 1 || count > MaximumOrderCount) return false;
            var existing = queue.Find(order => order.speciesId == speciesId);
            if (existing != null)
            {
                if (existing.count > MaximumOrderCount - count) return false;
                existing.count += count;
            }
            else queue.Add(new LabOrderSave { speciesId = speciesId, count = count });
            return true;
        }

        public bool MovePriority(string speciesId, int direction)
        {
            if (direction != -1 && direction != 1) return false;
            int index = queue.FindIndex(order => order.speciesId == speciesId);
            int target = index + direction;
            if (index < 0 || target < 0 || target >= queue.Count) return false;
            var order = queue[index]; queue[index] = queue[target]; queue[target] = order;
            return true;
        }

        public bool RemoveOrder(string speciesId)
        {
            int index = queue.FindIndex(order => order.speciesId == speciesId);
            if (index < 0) return false;
            queue.RemoveAt(index); return true;
        }

        public bool ClearAssignments()
        {
            bool changed = queue.Count != 0 || ActiveSpecimenId != null;
            queue.Clear(); ClearActive(); return changed;
        }

        /// <summary>Call only after an authoritative staged start succeeds; persistence commits both together.</summary>
        public bool MarkStarted(string speciesId, string specimenId)
        {
            if (!Employee.Hired || ActiveSpecimenId != null || string.IsNullOrWhiteSpace(specimenId) ||
                queue.Count == 0 || queue[0].speciesId != speciesId) return false;
            ActiveSpeciesId = speciesId; ActiveSpecimenId = specimenId;
            if (--queue[0].count == 0) queue.RemoveAt(0);
            return true;
        }

        public void ClearActive() { ActiveSpeciesId = ActiveSpecimenId = null; }

        /// <summary>A manually collected or replaced output retires its ticket without recreating an order.</summary>
        public bool Reconcile(GameModel game)
        {
            if (game == null || ActiveSpecimenId == null || MatchesIncubator(game, ActiveSpeciesId, ActiveSpecimenId)) return false;
            ClearActive(); return true;
        }

        public TechnicianSave CaptureSave(GameModel game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            bool active = MatchesIncubator(game, ActiveSpeciesId, ActiveSpecimenId);
            return new TechnicianSave
            {
                version = SaveVersion, employee = Employee.CaptureSave(), queue = Queue,
                activeSpeciesId = active ? ActiveSpeciesId : null,
                activeSpecimenId = active ? ActiveSpecimenId : null
            };
        }

        public static bool TryRestore(TechnicianSave save, GameModel game, int day, out ShopLaboratory laboratory)
        {
            laboratory = null;
            if (game == null || save == null || save.version != SaveVersion || save.queue == null ||
                !ShopStaff.TryRestore(save.employee, day, ShopStaff.TechnicianId, out var employee) ||
                save.queue.Length > game.DiscoveredSpeciesIds.Count) return false;
            var restored = new ShopLaboratory { Employee = employee };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var order in save.queue)
            {
                if (order == null || !ValidSpecies(game, order.speciesId) || !seen.Add(order.speciesId) ||
                    order.count < 1 || order.count > MaximumOrderCount) return false;
                restored.queue.Add(new LabOrderSave { speciesId = order.speciesId, count = order.count });
            }
            string speciesId = string.IsNullOrEmpty(save.activeSpeciesId) ? null : save.activeSpeciesId;
            string specimenId = string.IsNullOrEmpty(save.activeSpecimenId) ? null : save.activeSpecimenId;
            if ((speciesId == null) != (specimenId == null)) return false;
            if (specimenId != null)
            {
                if (!employee.Hired || !ValidSpecies(game, speciesId) ||
                    !MatchesIncubator(game, speciesId, specimenId)) return false;
                restored.ActiveSpeciesId = speciesId; restored.ActiveSpecimenId = specimenId;
            }
            laboratory = restored; return true;
        }

        static bool MatchesIncubator(GameModel game, string speciesId, string specimenId)
        {
            var incubator = game.Incubator;
            return !string.IsNullOrWhiteSpace(specimenId) &&
                (incubator.Phase == IncubatorPhase.Incubating || incubator.Phase == IncubatorPhase.Ready) &&
                incubator.SpeciesId == speciesId && incubator.SpecimenId == specimenId;
        }

        static bool ValidSpecies(GameModel game, string speciesId)
        {
            var recipe = game?.GetSpecies(speciesId);
            return recipe != null && game.IsDiscovered(speciesId) &&
                (recipe.Spark == 0 || game.SparkResearchUnlocked) &&
                (recipe.Mist == 0 || game.MistResearchUnlocked);
        }
    }
}
