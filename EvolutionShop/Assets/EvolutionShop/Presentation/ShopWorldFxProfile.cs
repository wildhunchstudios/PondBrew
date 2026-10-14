#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        // Diagnostic replay boundary only. The caller freezes the session and never changes authority.
        public void ResetFxProfileForValidation(bool enabled, bool low)
        {
            SetFxEnabledForValidation(enabled);
            SetFxPreviewModes(false, low);
            ResetFxPresentation();
            lastTime = 0;
            collectionSequence = 0;
            System.Array.Clear(growthBubbleSequences, 0, growthBubbleSequences.Length);
            handTravel = handMovePhase = handMoveWeight = 0;
            handPositionKnown = false;
            foreach (var display in displays.Values)
            {
                if (display.Habitat == null) continue;
                ResetPondletIdentity(display);
                display.Habitat.Clock = 0;
                display.Habitat.TrailSequence = 0;
            }
            Animate(0, 0);
        }
    }
}
#endif
