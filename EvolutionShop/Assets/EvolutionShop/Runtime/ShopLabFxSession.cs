using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        bool labModuleRuntimeBlocked, labModuleStaffPriority;
        void ReconcileLabPresentation()
        {
            if (!World || Model == null || Config == null) return;
            World.ConfigureLabTiming(Config.IncubationSeconds, Config.GrowerRefillSeconds);
            World.ConfigureAdvancedLabTiming(Config.ConditioningSeconds);
            bool paused = Automation.Paused || Paused || !fxHasFocus || PlacementActive || Panel == ShopPanel.Arrange;
            bool priority = labModuleStaffPriority || Model.IsIncubatorReservedForTechnician;
            bool blocked = labModuleRuntimeBlocked || AutomationBlockedBySave || AutosaveBlocked;
            if (Automation.IsPending && !priority && !paused) {
                // Read actual ingredients without constructing a queue snapshot or a NextStep status string each frame.
                var recipe = Model.GetSpecies(Automation.RecipeId);
                if (recipe == null || Model.Incubator.Phase != IncubatorPhase.Dosing) blocked = true;
                for (int i = 0; i < 4; i++) {
                    var culture = (CultureKind)i;
                    if (Model.Staged(culture) != 0) blocked = true;
                    if (recipe != null && Model.Available(culture) < ShopAutomation.Required(recipe, culture) &&
                        (Model.GetGrower(culture).Units == 0 || Model.ReservedCultureCount >= Model.Capacity)) blocked = true;
                }
            }
            var collector = ModuleState(Automation.CollectorOwned, Automation.CollectorEnabled, paused, priority, blocked);
            var doser = ModuleState(Automation.DoserOwned, Automation.DoserEnabled, paused, priority, blocked);
            World.RefreshLabAutomationFx(collector, doser, Automation.RequestId);
        }
        LabModuleVisualState ModuleState(bool owned, bool enabled, bool paused, bool priority, bool blocked)
            => !owned ? LabModuleVisualState.Absent : !enabled ? LabModuleVisualState.Off : paused ? LabModuleVisualState.Paused :
                Automation.IsPending && priority ? LabModuleVisualState.StaffPriority :
                (AutomationBlockedBySave || AutosaveBlocked || Automation.IsPending && blocked) ? LabModuleVisualState.Blocked : LabModuleVisualState.Waiting;
        void PresentLabHatch(bool first, bool saved)
        {
            if (!saved) { World.CancelFxTransient(); sound.StopLabAudio(); sound.StopHatchAudio(); return; }
            if (World.TryFxCue(first ? FxCueId.HatchFirst : FxCueId.HatchRepeat, NextFxSequence())) sound.LabHatch(first);
        }
        void PresentLabModule(AutomationStep step)
        {
            ReconcileLabPresentation();
            if (step.Kind != AutomationStepKind.Collect && step.Kind != AutomationStepKind.Dose) return;
            bool collect = step.Kind == AutomationStepKind.Collect;
            if (World.TryFxCue(collect ? FxCueId.ModuleCollected : FxCueId.ModuleDosed, NextFxSequence(), FxCueOrigin.Module, step.Culture)) {
                if (collect) World.PresentAdvancedModuleCollection(step.Culture);
                sound.LabModule();
            }
        }
    }
}
