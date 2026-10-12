using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        bool labModuleRuntimeBlocked;
        void ReconcileLabPresentation()
        {
            if (!World || Model == null || Config == null) return;
            World.ConfigureLabTiming(Config.IncubationSeconds, Config.GrowerRefillSeconds);
            bool blocked = labModuleRuntimeBlocked || AutomationBlockedBySave || AutosaveBlocked || Automation.Paused || Paused || PlacementActive ||
                Model.ReservedCultureCount >= Model.Capacity || Model.Incubator.Phase != IncubatorPhase.Dosing || Model.IsIncubatorReservedForTechnician;
            var state = !Automation.CollectorOwned ? LabModuleVisualState.Absent : !Automation.CollectorEnabled ? LabModuleVisualState.Off :
                Automation.IsPending && blocked ? LabModuleVisualState.Blocked : LabModuleVisualState.Waiting;
            World.RefreshLabModuleFx(state, Automation.RequestId);
        }
        void PresentLabHatch(bool first, bool saved)
        {
            if (!saved) { World.CancelFxTransient(); sound.StopLabAudio(); sound.StopHatchAudio(); return; }
            if (World.TryFxCue(first ? FxCueId.HatchFirst : FxCueId.HatchRepeat, NextFxSequence())) sound.LabHatch(first);
        }
        void PresentLabModule(AutomationStep step)
        {
            ReconcileLabPresentation();
            if (step.Kind != AutomationStepKind.Collect && step.Kind != AutomationStepKind.Dose) return;
            // FX-03 collector hero only; the doser retains its existing state/feedback.
            if (step.Kind != AutomationStepKind.Collect) return;
            if (World.TryFxCue(FxCueId.ModuleCollected, NextFxSequence(), FxCueOrigin.Module, step.Culture)) sound.LabModule();
        }
    }
}
