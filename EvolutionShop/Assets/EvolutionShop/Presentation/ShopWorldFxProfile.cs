#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        public bool FxProfileResidentsMatchForValidation(GameModel model)
        {
            if (model == null) return false;
            foreach (string id in model.TankIds)
            {
                var specimen=model.GetTank(id);
                if (!displays.TryGetValue(id,out var display) ||
                    (specimen.HasValue ? !display.Creature || display.FxSpecimenId!=specimen.Value.Id || display.SpeciesId!=specimen.Value.SpeciesId : display.Creature!=null)) return false;
            }
            foreach (string id in model.StorageIds)
            {
                var specimen=model.GetStorage(id);
                if (!displays.TryGetValue(id,out var display) ||
                    (specimen.HasValue ? !display.Creature || display.FxSpecimenId!=specimen.Value.Id || display.SpeciesId!=specimen.Value.SpeciesId : display.Creature!=null)) return false;
            }
            if (!displays.TryGetValue("incubator",out var incubator)) return false;
            return model.Incubator.Phase==IncubatorPhase.Dosing ? !incubator.Creature :
                incubator.Creature && incubator.FxSpecimenId==model.Incubator.SpecimenId && incubator.SpeciesId==model.Incubator.SpeciesId;
        }
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
