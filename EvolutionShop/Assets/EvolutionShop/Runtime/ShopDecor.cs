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

    }
}
