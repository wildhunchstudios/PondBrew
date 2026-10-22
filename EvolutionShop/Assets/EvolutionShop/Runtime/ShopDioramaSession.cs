using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// AD-A art slice switch. The Pocket Aquarium Diorama look is a development preview until the user reviews the slice
    /// in game (AD.7 gate): on by default when a development build is played, off in validation routes unless asked for
    /// (-evolutionDiorama, or the slice route), never in release builds. F10 flips it to compare with the classic look.
    /// </summary>
    public sealed partial class ShopSession
    {
        public bool DioramaLookOn => World && World.DioramaActive;

        void InitializeDiorama()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool asked = Arg("-evolutionDiorama") != null || Arg("-evolutionDioramaSliceSmoke") != null || Arg("-evolutionDioramaCreaturesSmoke") != null ||
                Arg("-evolutionDioramaCharactersSmoke") != null;
            bool refused = Arg("-evolutionDioramaOff") != null;
            if (!refused && (asked || !ValidationMode)) SetDioramaLook(true);
#endif
        }

        /// <summary>Switch the diorama look; the world restores the classic presentation exactly when it is switched off.</summary>
        public void SetDioramaLook(bool on)
        {
            if (!World || !Player) return;
            World.SetDioramaLook(on, Player.View);
            // AD-C: the crew stay in the shop, so they rebuild in place as the kit or the classic figure (customers already
            // in the shop keep the look they arrived with until they leave).
            foreach (var actor in new[] { Cashier ? Cashier.Actor : null, Stocker ? Stocker.Actor : null, Technician ? Technician.Actor : null })
                if (actor) actor.RebuildForLook();
            if (Cashier) Cashier.FitSignToActor();
            RefreshPresentationForLook();
            TickDioramaClock();
        }

        void TickDioramaClock()
        {
            if (!World || !World.DioramaActive || !Player || !Player.View) return;
            var phase = Cycle.Phase;
            float progress = phase == ShopDayPhase.Open
                ? 1 - Mathf.Clamp01(Cycle.SecondsRemaining / ShopDayCycle.DefaultTradingSeconds) : 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ValidationMode && DioramaTimeForValidation.HasValue) { phase = DioramaTimeForValidation.Value.phase; progress = DioramaTimeForValidation.Value.progress; }
#endif
            World.SetDioramaTime(phase, progress, Player.View.transform.position);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Validation only: show a lighting key without playing the day to it. Presentation only.</summary>
        public (ShopDayPhase phase, float progress)? DioramaTimeForValidation { get; set; }
#endif

        void TickDioramaInput(Keyboard keyboard)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ValidationMode && keyboard != null && keyboard.f10Key.wasPressedThisFrame) SetDioramaLook(!DioramaLookOn);
#endif
            TickDioramaClock();
        }
    }
}
