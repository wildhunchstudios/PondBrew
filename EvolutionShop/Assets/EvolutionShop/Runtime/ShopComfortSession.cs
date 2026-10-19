using UnityEngine;

namespace EvolutionShop
{
    public enum FxEffectDetail { Standard = 0, Low = 1 }

    public readonly struct FxComfortSnapshot
    {
        public readonly bool ReducedVisualMotion, LowDetail, EffectiveReducedMotion, EffectiveLowDetail;
        public readonly int OwnedParticles, ActiveParticles, ActionLimit, AmbientLimit, EmitterLimit, OwnedMeshes, OwnedMaterials;
        public readonly int FrameCount;
        public readonly float Realtime;
        public FxComfortSnapshot(bool reduced, bool low, ShopWorld world)
        {
            ReducedVisualMotion = reduced; LowDetail = low;
            EffectiveReducedMotion = world && world.VisualMotionReduced;
            EffectiveLowDetail = world && world.EffectsLow;
            var pool = world ? world.FxPool : null;
            OwnedParticles = pool == null ? 0 : pool.OwnedCount;
            ActiveParticles = pool == null ? 0 : pool.ActiveCount;
            ActionLimit = pool == null ? 0 : pool.ActionLimit;
            AmbientLimit = pool == null ? 0 : pool.AmbientLimit;
            EmitterLimit = pool == null ? 0 : pool.LogicalEmitterLimit;
            OwnedMeshes = pool == null ? 0 : pool.OwnedMeshCount;
            OwnedMaterials = pool == null ? 0 : pool.OwnedMaterialCount;
            FrameCount = Time.frameCount; Realtime = Time.realtimeSinceStartup;
        }
    }

    public sealed partial class ShopSession
    {
        public bool ReducedVisualMotion { get; private set; }
        public FxEffectDetail EffectsDetail { get; private set; }
        public FxComfortSnapshot ComfortSnapshot => new FxComfortSnapshot(ReducedVisualMotion, EffectsDetail == FxEffectDetail.Low, World);

        public bool SetComfortSettings(bool reducedVisualMotion, FxEffectDetail detail)
        {
            if (detail != FxEffectDetail.Standard && detail != FxEffectDetail.Low) return false;
            if (ReducedVisualMotion == reducedVisualMotion && EffectsDetail == detail) return true;
            if (!initialized || Model == null) return false;
            if (AutosaveBlocked)
            {
                SaveError = "Saving blocked: original save is invalid. Reload a repaired save or confirm New game.";
                Notify(SaveError); return false;
            }
            // Stage preferences in the existing complete slot. Neither live policy nor presentation
            // is changed until persistence succeeds; a failed setting is not a failed sale.
            var slot = CaptureStaffSlot(Model, Staff);
            slot.reducedVisualMotion = reducedVisualMotion; slot.effectsDetail = (int)detail;
            if (!WriteUiObservedSlot(slot, out var error, true))
            {
                SaveError = error; Notify(error); return false;
            }
            SaveError = null; saveClock = 0;
            RestoreComfortSettings(slot);
            // An in-flight gag may have sampled its old visual/contact mode. Cancel presentation
            // after the commit, preserving its already-applied outcome and gameplay cooldown.
            SkipComedy();
            return true;
        }
        void RestoreComfortSettings(SlotData slot)
        {
            ReducedVisualMotion = slot.reducedVisualMotion;
            EffectsDetail = (FxEffectDetail)slot.effectsDetail;
            ApplyComfortSettings();
        }
        void ApplyComfortSettings()
        {
            if (World) World.SetFxComfortModes(ReducedVisualMotion, EffectsDetail == FxEffectDetail.Low);
        }
    }
}
