using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        bool labModuleRuntimeBlocked;
        void ReconcileLabPresentation()
        {
            if (!World || Model == null || Config == null) return;
            World.ConfigureLabTiming(Config.IncubationSeconds);
            bool blocked = labModuleRuntimeBlocked || AutomationBlockedBySave || AutosaveBlocked || Automation.Paused || Paused || PlacementActive ||
                Model.ReservedCultureCount >= Model.Capacity || Model.Incubator.Phase != IncubatorPhase.Dosing || Model.IsIncubatorReservedForTechnician;
            var state = !Automation.CollectorOwned ? LabModuleVisualState.Absent : !Automation.CollectorEnabled ? LabModuleVisualState.Off :
                Automation.IsPending && blocked ? LabModuleVisualState.Blocked : LabModuleVisualState.Waiting;
            World.RefreshLabModuleFx(state, Automation.RequestId);
        }
        void PresentLabHatch(bool first, bool saved)
        {
            if (!saved) { World.CancelFxTransient(); sound.StopLabAudio(); return; }
            sound.LabHatch(first);
            World.TryFxCue(first ? FxCueId.HatchFirst : FxCueId.HatchRepeat, NextFxSequence());
        }
        void PresentLabModule(AutomationStep step)
        {
            ReconcileLabPresentation();
            if (step.Kind != AutomationStepKind.Collect && step.Kind != AutomationStepKind.Dose) return;
            // FX-03 collector hero only; the doser retains its existing state/feedback.
            if (step.Kind != AutomationStepKind.Collect) return;
            if (World.TryFxCue(FxCueId.ModuleCollected, NextFxSequence(), FxCueOrigin.Module, step.Culture)) sound.LabModule();
        }
        void DrawLabDoseStrip()
        {
            Rect rect = GUILayoutUtility.GetRect(1, 43, GUILayout.ExpandWidth(true));
            bool active = World && World.LabDoseRemaining > 0;
            FxCueId id = active ? World.LabDoseId : FxCueId.None;
            Color color = id == FxCueId.HandRefused ? coral : active && World.LabDoseCulture == CultureKind.Flow ? new Color(.12f,.70f,.78f) : mint;
            hudTheme.Draw(rect, hudTheme.Inset);
            float pulse = active ? Mathf.Clamp01(World.LabDoseRemaining / (id == FxCueId.HandRefused ? .18f : .4f)) : 0;
            if (pulse > 0) hudTheme.Draw(new Rect(rect.x,rect.y,5 + pulse * 3,rect.height),hudTheme.Accent,color);
            string label = id == FxCueId.HandRefused ? "No dose added — check the reason above." : id == FxCueId.DoseReturned ? World.LabDoseCulture + " returned to the siphon" :
                id == FxCueId.DoseCommitted ? World.LabDoseCulture + " measured into the hatchery" : "MEASURED CULTURES";
            GUI.Label(new Rect(rect.x+14,rect.y+4,rect.width-24,19),label,smallStyle);
            GUI.Label(new Rect(rect.x+14,rect.y+23,rect.width-24,18),Model.Incubator.Bloom+" Bloom  /  "+Model.Incubator.Flow+" Flow staged",smallStyle);
        }
    }
}
