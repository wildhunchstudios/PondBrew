using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// The saved money change the HUD shows as a floater and coin flight (UX-03, UX-08): each successful save that changes
    /// credits gives a new sequence number and its delta. It follows the durable result only; it never stages, publishes or
    /// changes economics. Until UX-09 the FX-18 IMGUI accents read it too; they are gone with the rest of IMGUI.
    /// </summary>
    public readonly struct UiFeedbackSnapshot
    {
        public readonly long MoneyDelta;
        public readonly uint MoneySequence;
        public readonly bool Visible;
        internal UiFeedbackSnapshot(long delta, uint sequence, bool visible) { MoneyDelta = delta; MoneySequence = sequence; Visible = visible; }
    }

    public sealed partial class ShopSession
    {
        long uiCurrencyBaseline, uiMoneyDelta;
        bool uiBaselineReady;
        uint uiMoneySequence;
        // The toolkit HUD's generation: a new one only on a reload or New game. Paid staff changes also replace Model, and
        // using Model itself made the HUD take every wage as a reload, so wage floaters never showed (found by UX-06).
        object hudGeneration = new object();
        bool UiFeedbackReduced => World ? World.VisualMotionReduced : ReducedVisualMotion;
        // Money cues show while the shop has focus, outside placement, the arrange notebook and the presentation study.
        bool UiFeedbackVisible => fxHasFocus && !PlacementActive && Panel != ShopPanel.Arrange && Panel != ShopPanel.Presentation;
        public UiFeedbackSnapshot UiFeedback => new UiFeedbackSnapshot(uiMoneyDelta, uiMoneySequence, UiFeedbackVisible);

        void ResetUiFeedback()
        {
            hudGeneration = new object();
            CancelUiFeedback();
            uiCurrencyBaseline = Model == null ? 0 : Model.Currency;
            uiBaselineReady = Model != null;
        }
        void CancelUiFeedback() { uiMoneyDelta = 0; }
        void UiFeedbackPanelChanged(ShopPanel target)
        {
            // Pause and the full-screen planning, help and study views cancel a pending money cue.
            if (target == ShopPanel.Pause || target == ShopPanel.Reset || target == ShopPanel.Welcome ||
                target == ShopPanel.TutorialHelp || target == ShopPanel.Presentation || target == ShopPanel.Arrange)
                CancelUiFeedback();
        }
        void TickUiFeedback()
        {
            if (!UiFeedbackVisible) CancelUiFeedback();
        }

        // Observe the durable result without changing staging, publication, or economics.
        bool WriteUiObservedSlot(SlotData slot, out string error, bool preserveFeedbackOnFailure = false)
        {
            bool saved = SaveStore.Write(SavePath, slot, out error);
            if (!saved) { if (!preserveFeedbackOnFailure) DiscardUnsavedUiDelta(); return false; }
            long currency = slot.game.currency;
            long delta = uiBaselineReady ? currency - uiCurrencyBaseline : 0;
            uiCurrencyBaseline = currency; uiBaselineReady = true;
            if (delta != 0 && UiFeedbackVisible) { uiMoneyDelta = delta; uiMoneySequence++; }
            return true;
        }
        void DiscardUnsavedUiDelta()
        {
            // A failed live sale must not reappear as a new success during a later retry.
            uiCurrencyBaseline = Model == null ? 0 : Model.Currency;
            uiBaselineReady = Model != null;
            uiMoneyDelta = 0;
        }
    }
}
