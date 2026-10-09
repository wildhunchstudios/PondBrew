using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        void DrawAutomationPanel()
        {
            Heading("Culture preparation");
            Text(AutomationStatus);
            Small("One batch, using your shared siphon. You start incubation at the incubator.");
            Small("Siphon: " + Model.ReservedCultureCount + " / " + Model.Capacity + " units, including staged doses and reserved vat returns.");
            if (AutomationBlockedBySave) Button("Retry saving / resume helper", () => RetryAutomation());
            var recipe = Automation.RecipeId == null ? null : Model.GetSpecies(Automation.RecipeId);
            Text(recipe == null ? "Choose a discovered recipe below." : "Selected: " + recipe.DisplayName + " / " + AutomationRecipeText(recipe));
            if (Automation.IsPending)
            {
                Button("Cancel this batch / keep cultures", () => CancelAutomationBatch());
                Small("Cancel before changing the recipe. Switching a module off keeps this request.");
            }
            else StaffButton("Prepare one batch", recipe != null && (Automation.CollectorEnabled || Automation.DoserEnabled) && !AutomationBlockedBySave,
                () => RequestAutomationBatch());
            Space();
            DrawAutomationModule(true);
            DrawAutomationModule(false);
            if (!Model.UpgradeOwned || !Model.IsDiscovered("pondlet"))
                Small("Modules require the larger siphon and a discovered Pondlet.");
            Space(); Heading("Known recipes");
            foreach (var definition in Config.Species)
            {
                if (!Model.IsDiscovered(definition.Id)) continue;
                string id = definition.Id;
                StaffButton((Automation.RecipeId == id ? "Selected / " : "Choose / ") + definition.DisplayName + " / " + AutomationRecipeText(definition),
                    !Automation.IsPending && Automation.RecipeId != id, () => SelectAutomationRecipe(id));
            }
            Small("Collector fills missing ingredients only. Doser waits for enough cultures and an empty mixture. Manual doses and Moss's paid work take priority.");
            Button("Back to research bench", () => SetPanel(ShopPanel.Upgrade));
        }
        void DrawAutomationModule(bool collector)
        {
            bool owned = collector ? Automation.CollectorOwned : Automation.DoserOwned;
            bool enabled = collector ? Automation.CollectorEnabled : Automation.DoserEnabled;
            int cost = collector ? ShopAutomation.CollectorCost : ShopAutomation.DoserCost;
            string name = collector ? "Collector" : "Recipe doser";
            if (owned)
                Button(name + " / " + (enabled ? "ON / switch off" : "OFF / switch on"), () => SetAutomationEnabled(collector, !enabled));
            else
            {
                StaffButton("Install " + name + " / " + cost + " credits",
                    Cycle.Phase == ShopDayPhase.Night && Customers.ActiveCount == 0 && Model.UpgradeOwned &&
                    Model.IsDiscovered("pondlet") && Model.Currency >= cost, () => PurchaseAutomationModule(collector));
                if (Cycle.Phase != ShopDayPhase.Night) Small("Install during preparation night.");
            }
        }
        static string AutomationRecipeText(SpeciesDefinition recipe) => recipe.Bloom + " Bloom + " + recipe.Flow + " Flow" +
            (recipe.Spark > 0 ? " + " + recipe.Spark + " Spark" : "") + (recipe.Mist > 0 ? " + " + recipe.Mist + " Mist" : "");
    }
}
