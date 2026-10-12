namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        // Development-only frozen particle fixture. Ordinary play and every other diagnostic keep normal simulation.
        bool FxVocabularyFrozen
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return ValidationMode && FreezeFxVocabularyForValidation;
#else
                return false;
#endif
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool FreezeFxVocabularyForValidation { get; set; }
#endif
        uint fxSequence;
        bool fxHasFocus = true;
        uint NextFxSequence()
        {
            fxSequence++;
            if (fxSequence == 0) { World?.ResetFxPresentation(); fxSequence = 1; }
            return fxSequence;
        }
        void PresentFxResult(ActionResult result, bool saved, FxCueId successCue, FxCueOrigin origin = FxCueOrigin.Manual, CultureKind culture = CultureKind.Bloom, string destination = null)
        {
            if (!World) return;
            if (result.Success && !saved) { World.CancelFxTransient(); return; }
            // Only opted-in manual actions bind the hero proof cues; ordinary worker feedback is preserved.
            if (successCue == FxCueId.None) return;
            if (result.Success && saved)
            {
                if (successCue == FxCueId.CollectedUnit && (culture == CultureKind.Bloom || culture == CultureKind.Flow)) sound.LabCollect();
                else if (successCue == FxCueId.DoseCommitted || successCue == FxCueId.DoseReturned || successCue == FxCueId.IncubationStarted) sound.LabFeedback(true, successCue);
            }
            uint sequence = NextFxSequence();
            World.TryFxCue(result.Success ? successCue : FxCueId.HandRefused, sequence, origin, culture);
            if (result.Success && saved && successCue == FxCueId.HandPlace) World.PresentPondletPlacement(destination, sequence);
        }
        void ApplyFxViewPolicy()
        {
            if (!World) return;
            ReconcileLabPresentation();
            World.SetFxViewPolicy(!fxHasFocus ? FxViewPolicy.Unfocused : PlacementActive || Panel == ShopPanel.Arrange ? FxViewPolicy.Arrange :
                Paused ? FxViewPolicy.Paused : Panel != ShopPanel.None ? FxViewPolicy.Modal : FxViewPolicy.Visible);
            if (World.LabDoseRemaining <= 0 || !fxHasFocus || Paused || PlacementActive || (Panel != ShopPanel.None && Panel != ShopPanel.Incubator)) sound?.StopDoseAudio();
            if (World.LabHatchRemaining <= 0 || !fxHasFocus || Paused || PlacementActive || Panel != ShopPanel.None) sound?.StopHatchAudio();
        }
        void ResetSessionFx() { fxSequence = 0; World?.ResetFxPresentation(); sound?.StopHatchAudio(); sound?.StopDoseAudio(); }
        void DisableSessionFx() { if (World) { World.SetFxViewPolicy(FxViewPolicy.Unfocused); World.CancelFxTransient(); sound?.StopLabAudio(); sound?.StopHatchAudio(); sound?.StopDoseAudio(); } }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public uint FxActionSequenceForValidation => fxSequence;
        public void SetFxFocusForValidation(bool value) { fxHasFocus = value; ApplyFxViewPolicy(); }
#endif
    }
}
