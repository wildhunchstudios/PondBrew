namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        ActionResult PerformAdvancedLab(ActionResult result, FxAdvancedLabEvent kind, string conditionerId = null)
            => PerformWithFx(result, FxCueId.None, advancedEvent: kind, advancedOwner: conditionerId);
    }
}
