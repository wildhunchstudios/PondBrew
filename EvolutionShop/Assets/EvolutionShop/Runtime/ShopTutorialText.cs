namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        /// <summary>
        /// UX-07: the tutorial's words name the controls of the UI that is showing. With the UI Toolkit preview on, a line
        /// comes from the string table (the cards' Start, Open shop, Crew...); otherwise it is the classic text, unchanged.
        /// Only the words differ: which line applies is decided by the same tutorial code either way.
        /// </summary>
        string TutText(string key, string classic, params object[] args)
        {
            var strings = ToolkitUiActive ? ToolkitUi.Strings : null;
            return strings != null && strings.Has(key) ? strings.Format(key, args) : classic;
        }
    }
}
