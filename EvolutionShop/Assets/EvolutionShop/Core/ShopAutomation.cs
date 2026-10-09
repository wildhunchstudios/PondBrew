using System;

namespace EvolutionShop
{
    [Serializable]
    public sealed class AutomationSave
    {
        public int version;
        public bool collectorOwned, collectorEnabled, doserOwned, doserEnabled;
        public string recipeId, requestId;
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
        public const int SaveVersion = 1;
        public const int CollectorCost = 36, DoserCost = 24;
        public bool CollectorOwned { get; private set; }
        public bool CollectorEnabled { get; private set; }
        public bool DoserOwned { get; private set; }
        public bool DoserEnabled { get; private set; }
        public string RecipeId { get; private set; }
        public string RequestId { get; private set; }
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
        public bool Request(GameModel game, string requestId)
        {
            if (IsPending || (!CollectorEnabled && !DoserEnabled) || !ValidRecipe(game, RecipeId) || !ValidRequest(requestId)) return false;
            RequestId = requestId;
            return true;
        }
        public bool Cancel()
        {
            if (!IsPending) return false;
            RequestId = null;
            return true;
        }
        public AutomationStep NextStep(GameModel game, bool technicianPriority)
        {
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
            return DoserEnabled ? new AutomationStep(AutomationStepKind.Dose, "Preparing " + recipe.DisplayName + " doses.") :
                new AutomationStep(AutomationStepKind.Finish, "Required cultures collected / finishing request.");
        }
        public bool Step(GameModel game, bool technicianPriority)
        {
            var next = NextStep(game, technicianPriority);
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
            doserOwned = DoserOwned, doserEnabled = DoserEnabled, recipeId = RecipeId, requestId = RequestId
        };
        public static bool TryRestore(AutomationSave save, GameModel game, out ShopAutomation automation)
        {
            automation = null;
            if (save == null || game == null || save.version != SaveVersion ||
                (save.collectorEnabled && !save.collectorOwned) || (save.doserEnabled && !save.doserOwned) ||
                ((save.collectorOwned || save.doserOwned) && (!game.UpgradeOwned || !game.IsDiscovered("pondlet")))) return false;
            // JsonUtility serializes absent strings as empty. Only empty/null are absence.
            string recipe = string.IsNullOrEmpty(save.recipeId) ? null : save.recipeId;
            string request = string.IsNullOrEmpty(save.requestId) ? null : save.requestId;
            if (recipe != null && !ValidRecipe(game, recipe) ||
                request != null && (!ValidRequest(request) || recipe == null || (!save.collectorOwned && !save.doserOwned))) return false;
            automation = new ShopAutomation
            {
                CollectorOwned = save.collectorOwned, CollectorEnabled = save.collectorEnabled,
                DoserOwned = save.doserOwned, DoserEnabled = save.doserEnabled, RecipeId = recipe, RequestId = request
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
