using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// Where the UI's shared theme, tokens and style sheets are (UX-02's foundation) and how its panel scales: a 1920x1080
    /// reference, scaled with the screen at match 0.5. The new game's UI (<see cref="GrowthUi"/>) builds its panel from these;
    /// GR-17 removed the classic game's UI that this class used to run.
    /// </summary>
    public static class ShopUiRoot
    {
        public const string ResourceFolder = "EvolutionShopUI/";
        public const string ThemeResource = ResourceFolder + "ShopTheme";
        public const string TokensResource = ResourceFolder + "ShopTokens";
        public const string HudStylesResource = ResourceFolder + "HudStyles";
        public const string CardStylesResource = ResourceFolder + "CardStyles";
        public const string ScreenStylesResource = ResourceFolder + "ScreenStyles";
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
        public const float ScreenMatch = 0.5f;
        public const int SortingOrder = 100;
    }
}
