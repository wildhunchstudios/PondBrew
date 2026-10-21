namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        /// <summary>
        /// UX-07: the tutorial's words name the toolkit's controls (the cards' Start, Open shop, Crew...) from the string
        /// table. The older wording each call carries is only a fallback for a table that failed to load; since UX-09 the
        /// toolkit is the only UI. Which line applies is decided by the tutorial code either way.
        /// </summary>
        string TutText(string key, string classic, params object[] args)
        {
            var strings = ToolkitUiActive ? ToolkitUi.Strings : null;
            return strings != null && strings.Has(key) ? strings.Format(key, args) : classic;
        }
    }
}
