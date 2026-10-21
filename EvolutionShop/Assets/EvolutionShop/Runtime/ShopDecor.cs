using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        ShopAppeal cachedAppeal;
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
            return OpenPlacementMenu();
        }

    }
}
