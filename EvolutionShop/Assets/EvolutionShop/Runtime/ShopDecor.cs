using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        ShopAppeal cachedAppeal;
        bool showDecorCatalogue;
        public ShopAppeal Appeal
        {
            get
            {
                if (cachedAppeal == null || cachedAppeal.Points != Layout.AppealPoints)
                    cachedAppeal = ShopAppeal.FromLayout(Layout);
                return cachedAppeal;
            }
        }
        public bool OpenDecorMenu()
        {
            if (!OpenPlacementMenu()) return false;
            showLabFacilities = false;
            showOwnedFurniture = false;
            showDecorCatalogue = true;
            return true;
        }

        void DrawDecorCatalogue()
        {
            Small("Each different type in the front store adds 10 appeal. Duplicate types and pieces in the lab add zero. All three types reach the 30-point cap. Decor adds no creature slots.");
            Text("New arrivals: " + Customers.NextArrivalSeconds.ToString("0.##") + "s gap; big spender share " + Customers.PremiumSharePercent.ToString("0.#") + "%.");
            if (Layout.DecorCount < ShopLayout.DecorLimit)
            {
                DecorButton(ShopLayout.CoralPlanterId);
                DecorButton(ShopLayout.BubbleLampId);
                DecorButton(ShopLayout.SillyStatueId);
            }
            else Text("All six decor places are owned. Place stored pieces or move them for free.");
            Small("Each 10 appeal: arrivals 0.25s closer when space is free, +5% of standard to price limits, +1 credit to budgets, and more big spenders. At most two visitors; accepted prices never change.");
            Small("Each piece reserves 1 m x 1 m. Check the preview for its exact appeal change before paying.");
        }
        void DecorButton(string definition)
        {
            bool contributes = false;
            foreach (var item in Layout.Items)
                if (!item.Stored && item.DefinitionId == definition && item.Z * ShopLayout.GridSize + .5f < .39f) { contributes = true; break; }
            string benefit = contributes ? "duplicate +0 appeal" : "+10 if placed in store";
            Button(ShopLayout.DefinitionName(definition) + " / " + ShopLayout.DefinitionPrice(definition) + " credits / " + benefit,
                () => BeginPlacement(null, definition));
        }

        void DrawAppealDetails()
        {
            Space(); Heading("Store appeal / " + Appeal.Points + " of 30");
            Text("Three distinct decor types earn 10 each in the store. Duplicates and lab decor earn zero. Move decor at night to change the score; reputation is separate and comes from service.");
            Small("Current decor benefit: arrival gap -" + Appeal.ArrivalReductionSeconds.ToString("0.##") + "s; price limits +" + Appeal.ToleranceBonusPercent + "% of standard; budgets +" + Appeal.BudgetBonus + " credits.");
            Small("Big spender share among new arrivals: " + Customers.PremiumSharePercent.ToString("0.#") + "%. This is a long-run chance, not a guaranteed visitor count.");
            Small("Combined reputation and appeal stop at a 3.25s arrival gap, +35% of standard to price limits and +7 budget credits. Two visitors maximum. Prices remain yours to set; better limits do not raise tags or agreed prices automatically.");
            if (Cycle.Phase == ShopDayPhase.Night) Button("Shop for decor / arrange rooms", () => OpenDecorMenu());
        }
    }
}
