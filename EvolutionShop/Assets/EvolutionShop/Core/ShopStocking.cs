using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    [Serializable]
    public sealed class StockerTargetSave
    {
        public string tankId;
        public string speciesId;
    }

    [Serializable]
    public sealed class StockerSave
    {
        public int version;
        public StaffSave employee;
        public StockerTargetSave[] targets;
    }

    /// <summary>Saved stocker employment and desired one-specimen displays; never owns stock or jobs.</summary>
    public sealed class ShopStocking
    {
        public const int SaveVersion = 1;
        public const string AnySpecies = "*";
        readonly Dictionary<string, string> targets = new Dictionary<string, string>(StringComparer.Ordinal);
        public ShopStaff Employee { get; private set; } = new ShopStaff(ShopStaff.StockerId);

        public string GetTarget(string tankId) =>
            tankId != null && targets.TryGetValue(tankId, out var speciesId) ? speciesId : null;

        public bool SetTarget(GameModel game, string tankId, string speciesId)
        {
            if (game == null || tankId == null || Array.IndexOf(game.TankIds, tankId) < 0 ||
                (speciesId != null && !ValidSpecies(game, speciesId))) return false;
            if (speciesId == null) return targets.Remove(tankId);
            if (GetTarget(tankId) == speciesId) return false;
            targets[tankId] = speciesId;
            return true;
        }

        /// <summary>Forget removed displays so storing then redeploying furniture starts unassigned.</summary>
        public bool PruneTargets(GameModel game)
        {
            if (game == null || targets.Count == 0) return false;
            var active = new HashSet<string>(game.TankIds, StringComparer.Ordinal);
            var removed = new List<string>();
            foreach (var id in targets.Keys) if (!active.Contains(id)) removed.Add(id);
            foreach (var id in removed) targets.Remove(id);
            return removed.Count > 0;
        }

        public StockerSave CaptureSave(GameModel game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            // Furniture storage/removal changes the active tank registry. Exclude retired
            // settings in the durable snapshot without moving creatures or mutating the game.
            var active = new HashSet<string>(game.TankIds, StringComparer.Ordinal);
            var ids = new List<string>();
            foreach (var id in targets.Keys) if (active.Contains(id)) ids.Add(id);
            ids.Sort(StringComparer.Ordinal);
            var savedTargets = new StockerTargetSave[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                savedTargets[i] = new StockerTargetSave { tankId = ids[i], speciesId = targets[ids[i]] };
            return new StockerSave { version = SaveVersion, employee = Employee.CaptureSave(), targets = savedTargets };
        }

        public static bool TryRestore(StockerSave save, GameModel game, int day, out ShopStocking stocking)
        {
            stocking = null;
            if (game == null || save == null || save.version != SaveVersion || save.targets == null ||
                !ShopStaff.TryRestore(save.employee, day, ShopStaff.StockerId, out var employee)) return false;
            var active = new HashSet<string>(game.TankIds, StringComparer.Ordinal);
            if (save.targets.Length > active.Count) return false;
            var restored = new ShopStocking { Employee = employee };
            foreach (var target in save.targets)
            {
                if (target == null || target.tankId == null || !active.Contains(target.tankId) ||
                    !ValidSpecies(game, target.speciesId) || restored.targets.ContainsKey(target.tankId)) return false;
                restored.targets.Add(target.tankId, target.speciesId);
            }
            stocking = restored;
            return true;
        }

        static bool ValidSpecies(GameModel game, string speciesId) => speciesId == AnySpecies ||
            (speciesId != null && game.GetSpecies(speciesId) != null && game.IsDiscovered(speciesId));
    }
}
