using System;

namespace EvolutionShop
{
    [Serializable]
    public sealed class AutomationSave
    {
        public int version;
        public bool collectorOwned, collectorEnabled, doserOwned, doserEnabled;
        public string recipeId, requestId;
        public bool maintainEnabled, paused;
        public int desiredStockCount;
        public string destinationTankId;
        public bool policyHandoffWaiting;
        public int policyQueueCountAtRequest;
    }
    public enum AutomationStepKind { None, Collect, Dose, Finish }
    public readonly struct AutomationStep
    {
        public readonly AutomationStepKind Kind;
        public readonly CultureKind Culture;
        public readonly string Status;
        public AutomationStep(AutomationStepKind kind, string status, CultureKind culture = CultureKind.Bloom)
        { Kind = kind; Status = status; Culture = culture; }
    }

    /// <summary>One requested batch. All inventory and machine ownership stays in GameModel.</summary>
    public sealed class ShopAutomation
    {
        public const int SaveVersion = 2;
        public const int MaximumDesiredStockCount = 9;
        public const int CollectorCost = 36, DoserCost = 24;
        public bool CollectorOwned { get; private set; }
        public bool CollectorEnabled { get; private set; }
        public bool DoserOwned { get; private set; }
        public bool DoserEnabled { get; private set; }
        public string RecipeId { get; private set; }
        public string RequestId { get; private set; }
        public bool MaintainEnabled { get; private set; }
        public bool Paused { get; private set; }
        public int DesiredStockCount { get; private set; } = 1;
        public string DestinationTankId { get; private set; }
        public bool PolicyHandoffWaiting { get; private set; }
        public int PolicyQueueCountAtRequest { get; private set; }
        public bool IsPending => RequestId != null;

        public bool Purchase(GameModel game, bool collector)
        {
            if (game == null || !game.UpgradeOwned || !game.IsDiscovered("pondlet") ||
                (collector ? CollectorOwned : DoserOwned) || !game.TrySpendCredits(collector ? CollectorCost : DoserCost)) return false;
            if (collector) CollectorOwned = true; else DoserOwned = true;
            return true;
        }
        public bool SetEnabled(bool collector, bool enabled)
        {
            if (!(collector ? CollectorOwned : DoserOwned) || (collector ? CollectorEnabled : DoserEnabled) == enabled) return false;
            if (collector) CollectorEnabled = enabled; else DoserEnabled = enabled;
            return true;
        }
        public bool SelectRecipe(GameModel game, string id)
        {
            if (IsPending || !ValidRecipe(game, id) || RecipeId == id) return false;
            RecipeId = id;
            return true;
        }
        public bool SetDesiredStockCount(int count)
        {
            if (count < 1 || count > MaximumDesiredStockCount || DesiredStockCount == count) return false;
            DesiredStockCount = count;
            return true;
        }
        public bool SetDestination(GameModel game, string tankId)
        {
            if (game == null || string.IsNullOrEmpty(tankId) || Array.IndexOf(game.TankIds, tankId) < 0 ||
                DestinationTankId == tankId) return false;
            DestinationTankId = tankId;
            return true;
        }
        public bool SetMaintainEnabled(bool enabled)
        {
            if (MaintainEnabled == enabled || (enabled && (RecipeId == null || DestinationTankId == null))) return false;
            MaintainEnabled = enabled;
            if (!enabled) ClearPolicyHandoffWait();
            return true;
        }
        public bool SetPaused(bool paused)
        {
            if (Paused == paused) return false;
            Paused = paused;
            return true;
        }
        public bool Request(GameModel game, string requestId) => Request(game, requestId, 0);
        public bool RequestForPolicy(GameModel game, string requestId, int matchingQueueCount) =>
            MaintainEnabled && matchingQueueCount > 0 && Request(game, requestId, matchingQueueCount);
        bool Request(GameModel game, string requestId, int policyQueueCount)
        {
            if (IsPending || (!CollectorEnabled && !DoserEnabled) || !ValidRecipe(game, RecipeId) || !ValidRequest(requestId)) return false;
            RequestId = requestId;
            PolicyHandoffWaiting = policyQueueCount > 0;
            PolicyQueueCountAtRequest = policyQueueCount;
            return true;
        }
        public bool ClearPolicyHandoffWait()
        {
            if (!PolicyHandoffWaiting) return false;
            PolicyHandoffWaiting = false;
            PolicyQueueCountAtRequest = 0;
            return true;
        }
        public bool Cancel()
        {
            if (!IsPending) return false;
            RequestId = null;
            ClearPolicyHandoffWait();
            return true;
        }
        public AutomationStep NextStep(GameModel game, bool technicianPriority) => NextStep(game, technicianPriority, false);

        public AutomationStep NextStep(GameModel game, bool technicianPriority, bool technicianHandoff)
        {
            if (Paused) return Wait("Helper paused; its request and cultures are retained.");
            if (!IsPending) return Wait("No batch requested.");
            if (!CollectorEnabled && !DoserEnabled) return Wait("Modules switched off / request retained.");
            if (game == null || !ValidRecipe(game, RecipeId)) return Wait("Known recipe unavailable.");
            if (technicianPriority || game.IsIncubatorReservedForTechnician) return Wait("Waiting for Moss's lab work.");
            if (game.Incubator.Phase != IncubatorPhase.Dosing) return Wait("Waiting for a free incubator.");
            for (int i = 0; i < 4; i++)
                if (game.Staged((CultureKind)i) != 0) return Wait("Manual mixture present / finish or clear it first.");
            var recipe = game.GetSpecies(RecipeId);
            for (int i = 0; i < 4; i++)
            {
                var culture = (CultureKind)i;
                if (game.Available(culture) >= Required(recipe, culture)) continue;
                if (!CollectorEnabled) return Wait("Waiting for manually collected cultures.");
                if (game.ReservedCultureCount >= game.Capacity) return Wait("Siphon full / release spare cultures.");
                if (game.GetGrower(culture).Units <= 0) return Wait(culture + " grower is refilling.");
                return new AutomationStep(AutomationStepKind.Collect, "Collecting " + culture + " for " + recipe.DisplayName + ".", culture);
            }
            return DoserEnabled && !technicianHandoff ? new AutomationStep(AutomationStepKind.Dose, "Preparing " + recipe.DisplayName + " doses.") :
                new AutomationStep(AutomationStepKind.Finish, technicianHandoff ? "Cultures ready / handing the queued recipe to Moss." : "Required cultures collected / finishing request.");
        }
        public bool Step(GameModel game, bool technicianPriority) => Step(game, technicianPriority, false);

        public bool Step(GameModel game, bool technicianPriority, bool technicianHandoff)
        {
            var next = NextStep(game, technicianPriority, technicianHandoff);
            if (next.Kind == AutomationStepKind.Collect) return game.Collect(next.Culture).Success;
            if (next.Kind == AutomationStepKind.Dose)
            {
                if (!game.UseKnownRecipe(RecipeId).Success) return false;
                RequestId = null;
                return true;
            }
            if (next.Kind != AutomationStepKind.Finish) return false;
            RequestId = null;
            return true;
        }
        public AutomationSave CaptureSave() => new AutomationSave
        {
            version = SaveVersion, collectorOwned = CollectorOwned, collectorEnabled = CollectorEnabled,
            doserOwned = DoserOwned, doserEnabled = DoserEnabled, recipeId = RecipeId, requestId = RequestId,
            maintainEnabled = MaintainEnabled, paused = Paused, desiredStockCount = DesiredStockCount,
            destinationTankId = DestinationTankId, policyHandoffWaiting = PolicyHandoffWaiting,
            policyQueueCountAtRequest = PolicyQueueCountAtRequest
        };
        public static bool TryRestore(AutomationSave save, GameModel game, out ShopAutomation automation)
        {
            automation = null;
            if (save == null || game == null || (save.version != 1 && save.version != SaveVersion) ||
                (save.collectorEnabled && !save.collectorOwned) || (save.doserEnabled && !save.doserOwned) ||
                ((save.collectorOwned || save.doserOwned) && (!game.UpgradeOwned || !game.IsDiscovered("pondlet")))) return false;
            // JsonUtility serializes absent strings as empty. Only empty/null are absence.
            string recipe = string.IsNullOrEmpty(save.recipeId) ? null : save.recipeId;
            string request = string.IsNullOrEmpty(save.requestId) ? null : save.requestId;
            bool maintain = save.version >= 2 && save.maintainEnabled;
            bool paused = save.version >= 2 && save.paused;
            int desired = save.version >= 2 ? save.desiredStockCount : 1;
            string destination = save.version >= 2 && !string.IsNullOrEmpty(save.destinationTankId) ? save.destinationTankId : null;
            bool policyWaiting = save.version >= 2 && save.policyHandoffWaiting;
            int policyQueueCount = save.version >= 2 ? save.policyQueueCountAtRequest : 0;
            if (recipe != null && !ValidRecipe(game, recipe) ||
                request != null && (!ValidRequest(request) || recipe == null || (!save.collectorOwned && !save.doserOwned)) ||
                desired < 1 || desired > MaximumDesiredStockCount ||
                destination != null && destination.Length > 64 ||
                maintain && (recipe == null || destination == null) ||
                (policyWaiting && (recipe == null || policyQueueCount < 1 || !maintain)) ||
                (!policyWaiting && policyQueueCount != 0)) return false;
            automation = new ShopAutomation
            {
                CollectorOwned = save.collectorOwned, CollectorEnabled = save.collectorEnabled,
                DoserOwned = save.doserOwned, DoserEnabled = save.doserEnabled, RecipeId = recipe, RequestId = request,
                MaintainEnabled = maintain, Paused = paused, DesiredStockCount = desired, DestinationTankId = destination,
                PolicyHandoffWaiting = policyWaiting, PolicyQueueCountAtRequest = policyQueueCount
            };
            return true;
        }
        public static int Required(SpeciesDefinition recipe, CultureKind culture) => culture == CultureKind.Bloom ? recipe.Bloom :
            culture == CultureKind.Flow ? recipe.Flow : culture == CultureKind.Spark ? recipe.Spark : recipe.Mist;
        static bool ValidRequest(string id) => id != null && id.Length == 32 && Guid.TryParseExact(id, "N", out var value) && value != Guid.Empty;
        static bool ValidRecipe(GameModel game, string id)
        {
            var recipe = game?.GetSpecies(id);
            return recipe != null && game.IsDiscovered(id) && (recipe.Spark == 0 || game.SparkResearchUnlocked) &&
                (recipe.Mist == 0 || game.MistResearchUnlocked);
        }
        static AutomationStep Wait(string status) => new AutomationStep(AutomationStepKind.None, status);
    }
}
