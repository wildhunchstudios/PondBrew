using UnityEngine;

namespace EvolutionShop
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct FxAdvancedSourceSnapshot
    {
        public bool Present, Unlocked;
        public FxOwnerToken Owner;
        public Transform Root;
        public Vector3 Source;
        public int Units, ActiveCells, Particles, AmbientRequests, AmbientAdmitted, RefillCount, ManualCount, ModuleCount, AccentRequests, AccentAdmitted;
    }
    public struct FxConditionerSnapshot
    {
        public bool Present;
        public FxOwnerToken Owner;
        public Transform Root;
        public ConditionerPhase Phase;
        public int Count, Particles, WorkingRequests, WorkingAdmitted, StartedCount, CancelledCount, CompletedCount, OutputCount;
        public double SecondsRemaining;
        public string FirstId, SecondId, FirstSpecies, SecondSpecies, CompletedFirstId, CompletedSecondId;
    }
#endif
    public sealed partial class ShopWorld
    {
        sealed class AdvancedSourceFx
        {
            public FxOwnerToken Owner;
            public Transform Root;
            public bool Known;
            public int Units, AmbientRequests, AmbientAdmitted, RefillCount, ManualCount, ModuleCount, AccentRequests, AccentAdmitted;
            public float Clock;
            public uint Sequence;
        }
        sealed class ConditioningFx
        {
            public string Key;
            public FxOwnerToken Owner;
            public float Clock, Response;
            public uint Sequence;
            public bool StepCaptured;
            public GameModel StepModel;
            public string StepFirstId, StepSecondId, StepFirstSpecies, StepSecondSpecies;
            public string CompletedFirstId, CompletedSecondId;
            public int WorkingRequests, WorkingAdmitted, StartedCount, CancelledCount, CompletedCount, OutputCount;
            public ConditionerPhase Phase;
            public int Count;
        }
        readonly AdvancedSourceFx[] advancedSources = { new AdvancedSourceFx(), new AdvancedSourceFx() };
        double advancedConditioningSeconds = 12;
        bool AdvancedVisible => fxEnabled && isActiveAndEnabled && fxPolicy == FxViewPolicy.Visible;
        bool AdvancedCommandVisible => fxEnabled && isActiveAndEnabled && (fxPolicy == FxViewPolicy.Visible || fxPolicy == FxViewPolicy.Modal);
        static int AdvancedIndex(CultureKind culture) => culture == CultureKind.Spark ? 0 : 1;
        static string AdvancedKey(int index) => index == 0 ? "advanced-spark" : "advanced-mist";
        public void ConfigureAdvancedLabTiming(double seconds) { if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) advancedConditioningSeconds = seconds; }

        Transform AdvancedSourceRoot(CultureKind culture)
        {
            if (lastModel == null) return null;
            if (culture == CultureKind.Spark) return lastModel.SparkResearchUnlocked && sparkModule && sparkModule.gameObject.activeInHierarchy ? upgradeRoot : null;
            if (culture != CultureKind.Mist || !lastModel.MistResearchUnlocked) return null;
            foreach (var pair in conditioners)
                if (pair.Value.Root && pair.Value.Root.gameObject.activeInHierarchy && lastModel.GetConditioner(pair.Key).HasValue) return pair.Value.Root;
            return null;
        }
        void RetireAdvancedSource(int index)
        {
            var source = advancedSources[index];
            if (source.Owner.IsDefined) { fxPool?.Cancel(source.Owner); fxCues?.Unregister(source.Owner); }
            source.Owner = default; source.Root = null; source.Known = false; source.Clock = .3f + index * .17f;
        }
        void ReconcileAdvancedLabFx()
        {
            if (lastModel == null || fxCues == null) return;
            for (int i = 0; i < 2; i++) {
                var culture = i == 0 ? CultureKind.Spark : CultureKind.Mist;
                var root = AdvancedSourceRoot(culture); var source = advancedSources[i];
                if (!root) { RetireAdvancedSource(i); continue; }
                var owner = BindFxOwner(AdvancedKey(i), null, root);
                var stock = lastModel.GetGrower(culture);
                bool same = source.Known && source.Owner.Equals(owner) && source.Root == root && fxCues.IsValid(owner);
                bool refill = same && stock.Units > source.Units;
                if (same && stock.Units != source.Units) fxPool?.CancelAmbient(owner);
                source.Root = root; source.Owner = owner; source.Units = stock.Units; source.Known = true;
                fxCues.SetState(owner, stock.Units <= 0 ? FxSteadyState.Empty : stock.Units >= stock.Capacity ? FxSteadyState.Full : FxSteadyState.Refilling);
                if (refill && AdvancedVisible) { source.RefillCount++; EmitAdvancedAccent(culture, false); }
            }
            foreach (var pair in conditioners) {
                var vat = pair.Value; var state = lastModel.GetConditioner(pair.Key); var fx = vat.Fx;
                if (!state.HasValue || !vat.Root || !vat.Root.gameObject.activeInHierarchy) { RetireConditionerFx(vat); continue; }
                var owner = BindFxOwner(fx.Key, null, vat.Root);
                if (!owner.Equals(fx.Owner) || state.Value.Phase != fx.Phase || state.Value.Count != fx.Count) {
                    fxPool?.Cancel(fx.Owner); fx.Clock = .16f; fx.Response = 0;
                }
                fx.Owner = owner; fx.Phase = state.Value.Phase; fx.Count = state.Value.Count;
                fxCues.SetState(owner, fx.Phase == ConditionerPhase.Processing ? FxSteadyState.Working : fx.Phase == ConditionerPhase.Ready ? FxSteadyState.Ready : FxSteadyState.Empty);
            }
        }
        void EmitAdvancedAccent(CultureKind culture, bool action)
        {
            if (!FxAdvancedLabMotion.IsAdvanced(culture) || fxPool == null || fxCues == null) return;
            var source = advancedSources[AdvancedIndex(culture)];
            if (!source.Root || !source.Root.gameObject.activeInHierarchy || !fxCues.IsValid(source.Owner)) return;
            int count = fxReducedMotion || LabDoseLow ? 1 : 2;
            for (int i = 0; i < count; i++) {
                FxAdvancedLabMotion.SampleSource(culture, ++source.Sequence, LabDoseLow, fxReducedMotion, out _, out var local, out var request);
                source.AccentRequests++;
                if (fxPool.EmitParticle(fxCues, source.Owner, source.Root, local, request, action)) source.AccentAdmitted++;
            }
        }
        void AcceptAdvancedCollection(CultureKind culture, bool module)
        {
            if (!FxAdvancedLabMotion.IsAdvanced(culture) || !AdvancedCommandVisible) return;
            var source = advancedSources[AdvancedIndex(culture)];
            if (!source.Root || fxCues == null || !fxCues.IsValid(source.Owner)) return;
            if (module) source.ModuleCount++; else source.ManualCount++;
            EmitAdvancedAccent(culture, true);
        }
        public void PresentAdvancedModuleCollection(CultureKind culture) => AcceptAdvancedCollection(culture, true);

        public void PresentAdvancedLabResult(FxAdvancedLabEvent kind, string conditionerId, bool success, bool saved)
        {
            if (!success || !saved) return;
            if (kind == FxAdvancedLabEvent.SparkUnlocked || kind == FxAdvancedLabEvent.MistUnlocked) {
                if (AdvancedCommandVisible) EmitAdvancedAccent(kind == FxAdvancedLabEvent.SparkUnlocked ? CultureKind.Spark : CultureKind.Mist, true);
                return;
            }
            if (conditionerId == null || !conditioners.TryGetValue(conditionerId, out var vat) || lastModel == null || !lastModel.GetConditioner(conditionerId).HasValue) return;
            var fx = vat.Fx;
            if (kind == FxAdvancedLabEvent.ConditioningStarted) fx.StartedCount++;
            else if (kind == FxAdvancedLabEvent.ConditioningCancelled) fx.CancelledCount++;
            else if (kind == FxAdvancedLabEvent.ConditioningOutput) fx.OutputCount++;
            fx.StepCaptured = false;
            if (!AdvancedCommandVisible) return;
            fx.Response = fxReducedMotion ? 0 : .38f;
            // Cancellation only settles the existing mechanism. It never emits a transformation cue.
            if (kind == FxAdvancedLabEvent.ConditioningStarted) EmitConditionerAccent(vat, false);
        }

        // Explicitly bracket the authoritative timed step, not a generic Ready reconciliation edge.
        public void CaptureConditionerStep(GameModel model)
        {
            foreach (var pair in conditioners) {
                var fx = pair.Value.Fx; var state = model.GetConditioner(pair.Key);
                fx.StepCaptured = state.HasValue && state.Value.Phase == ConditionerPhase.Processing;
                fx.StepModel = model;
                fx.StepFirstId = state.HasValue && state.Value.First.HasValue ? state.Value.First.Value.Id : null;
                fx.StepSecondId = state.HasValue && state.Value.Second.HasValue ? state.Value.Second.Value.Id : null;
                fx.StepFirstSpecies = state.HasValue && state.Value.First.HasValue ? state.Value.First.Value.SpeciesId : null;
                fx.StepSecondSpecies = state.HasValue && state.Value.Second.HasValue ? state.Value.Second.Value.SpeciesId : null;
            }
        }
        static bool ConditionedSlot(string beforeId, string beforeSpecies, SpecimenView? after)
            => beforeId == null ? !after.HasValue : beforeSpecies == "pondlet" && after.HasValue && after.Value.Id == beforeId && after.Value.SpeciesId == "lanternfin";
        public void PresentConditionerStep(bool saved, int completedCount)
        {
            foreach (var pair in conditioners) {
                var vat = pair.Value; var fx = vat.Fx; var state = lastModel.GetConditioner(pair.Key);
                bool completed = saved && completedCount > 0 && fx.StepCaptured && ReferenceEquals(fx.StepModel, lastModel) &&
                    state.HasValue && state.Value.Phase == ConditionerPhase.Ready && state.Value.Count > 0 &&
                    ConditionedSlot(fx.StepFirstId, fx.StepFirstSpecies, state.Value.First) && ConditionedSlot(fx.StepSecondId, fx.StepSecondSpecies, state.Value.Second);
                fx.StepCaptured = false; fx.StepModel = null;
                if (!completed) continue;
                fx.CompletedCount++; fx.CompletedFirstId = fx.StepFirstId; fx.CompletedSecondId = fx.StepSecondId;
                if (!AdvancedCommandVisible) continue;
                fx.Response = fxReducedMotion ? 0 : .48f;
                EmitConditionerAccent(vat, true);
            }
            if (!saved) ResetAdvancedLabFx();
        }
        void EmitConditionerAccent(ConditionerVisual vat, bool completed)
        {
            if (fxPool == null || fxCues == null || !fxCues.IsValid(vat.Fx.Owner)) return;
            for (int i = 0; i < 2; i++) {
                if (!vat.Residents[i].Display.Creature) continue;
                int count = completed && !LabDoseLow && !fxReducedMotion ? 2 : 1;
                for (int accent = 0; accent < count; accent++) {
                    FxAdvancedLabMotion.SampleConditioner(i, completed ? 1 : 0, ++vat.Fx.Sequence, LabDoseLow, fxReducedMotion, out _, out var local, out var request);
                    if (completed && !fxReducedMotion) {
                        request.Shape = accent == 0 ? FxParticleShape.Ring : FxParticleShape.Spark;
                        request.Size = accent == 0 ? .10f : .075f;
                        request.EndSize = accent == 0 ? .18f : .025f;
                    }
                    fxPool.EmitParticle(fxCues, vat.Fx.Owner, vat.Root, local, request, true);
                }
            }
        }
        void TickAdvancedLabFx(float time, float dt)
        {
            if (lastModel == null) return;
            bool quiet = fxReducedMotion || !AdvancedVisible;
            AnimateSparkCells(quiet ? 0 : time);
            AnimateConditioners(quiet ? 0 : time);
            foreach (var pair in conditioners) {
                var vat = pair.Value; var fx = vat.Fx; var state = lastModel.GetConditioner(pair.Key);
                fx.Response = Mathf.Max(0, fx.Response - Mathf.Max(0, dt));
                bool working = state.HasValue && state.Value.Phase == ConditionerPhase.Processing;
                float progress = working ? FxAdvancedLabMotion.ConditionerProgress(state.Value.SecondsRemaining, advancedConditioningSeconds) : state.HasValue && state.Value.Phase == ConditionerPhase.Ready ? 1 : 0;
                if (vat.Paddle) vat.Paddle.localRotation = Quaternion.Euler(0, 0, FxAdvancedLabMotion.PaddleAngle(time, progress, working, quiet) + (quiet ? 0 : Mathf.Sin(fx.Response / .48f * Mathf.PI) * 7));
                for (int i = 0; i < 2; i++) {
                    var resident = vat.Residents[i];
                    if (!resident.WaterRenderer) continue;
                    var color = resident.WaterColor;
                    bool occupied = state.HasValue && (i == 0 ? state.Value.First.HasValue : state.Value.Second.HasValue);
                    if (occupied && fxEnabled) {
                        var tint = working ? Color.Lerp(new Color(.58f,.82f,.95f), new Color(.97f,.86f,.51f), progress) : new Color(.80f,.91f,.72f);
                        color = Color.Lerp(color, tint, working ? .18f : .10f); color.a = resident.WaterColor.a;
                    }
                    resident.WaterProperties.SetColor(DoseFluidColor, color); resident.WaterRenderer.SetPropertyBlock(resident.WaterProperties);
                }
                if (!AdvancedVisible || !working || dt <= 0 || fxPool == null || fxCues == null || !fxCues.IsValid(fx.Owner)) continue;
                fx.Clock -= dt; if (fx.Clock > 0) continue;
                int slot = state.Value.First.HasValue && state.Value.Second.HasValue ? (int)(fx.Sequence % 2) : state.Value.First.HasValue ? 0 : 1;
                FxAdvancedLabMotion.SampleConditioner(slot, progress, ++fx.Sequence, LabDoseLow, fxReducedMotion, out fx.Clock, out var local, out var request);
                if (state.Value.Count == 1) fx.Clock *= 2;
                fx.WorkingRequests++;
                if (fxPool.EmitParticle(fxCues, fx.Owner, vat.Root, local, request, false)) fx.WorkingAdmitted++;
            }
            for (int i = 0; i < 2; i++) {
                var source = advancedSources[i]; var culture = i == 0 ? CultureKind.Spark : CultureKind.Mist;
                if (!AdvancedVisible || dt <= 0 || !source.Root || fxCues == null || !fxCues.IsValid(source.Owner) || fxPool == null) continue;
                var stock = lastModel.GetGrower(culture); if (stock.Units >= stock.Capacity) continue;
                source.Clock -= dt; if (source.Clock > 0) continue;
                FxAdvancedLabMotion.SampleSource(culture, ++source.Sequence, LabDoseLow, fxReducedMotion, out source.Clock, out var local, out var request);
                source.AmbientRequests++;
                if (fxPool.EmitParticle(fxCues, source.Owner, source.Root, local, request, false)) source.AmbientAdmitted++;
            }
        }
        void RetireConditionerFx(ConditionerVisual vat)
        {
            fxPool?.Cancel(vat.Fx.Owner); fxCues?.Unregister(vat.Fx.Owner);
            vat.Fx.Owner = default; vat.Fx.StepCaptured = false; vat.Fx.StepModel = null; vat.Fx.Response = 0;
        }
        void ResetAdvancedLabFx()
        {
            for (int i = 0; i < 2; i++) { var source = advancedSources[i]; fxPool?.Cancel(source.Owner); source.Known = false; source.Clock = .31f + i * .19f; }
            foreach (var vat in conditioners.Values) {
                fxPool?.Cancel(vat.Fx.Owner); vat.Fx.StepCaptured = false; vat.Fx.StepModel = null; vat.Fx.Clock = .22f; vat.Fx.Response = 0;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FxAdvancedSourceSnapshot AdvancedSourceForValidation(CultureKind culture)
        {
            if (!FxAdvancedLabMotion.IsAdvanced(culture) || lastModel == null) return default;
            var source = advancedSources[AdvancedIndex(culture)];
            var value = new FxAdvancedSourceSnapshot { Present = source.Root && source.Root.gameObject.activeInHierarchy && fxCues != null && fxCues.IsValid(source.Owner),
                Unlocked = culture == CultureKind.Spark ? lastModel.SparkResearchUnlocked : lastModel.MistResearchUnlocked,
                Owner = source.Owner, Root = source.Root, Source = source.Root ? source.Root.TransformPoint(FxAdvancedLabMotion.SourcePoint(culture)) : Vector3.zero,
                Units = lastModel.GetGrower(culture).Units, Particles = fxPool == null ? 0 : fxPool.ParticleCountForValidation(source.Owner),
                AmbientRequests = source.AmbientRequests, AmbientAdmitted = source.AmbientAdmitted, RefillCount = source.RefillCount,
                ManualCount = source.ManualCount, ModuleCount = source.ModuleCount, AccentRequests = source.AccentRequests, AccentAdmitted = source.AccentAdmitted };
            if (culture == CultureKind.Spark) { foreach (var cell in sparkCells) if (cell && cell.gameObject.activeInHierarchy) value.ActiveCells++; }
            else foreach (var pair in conditioners) if (lastModel.GetConditioner(pair.Key).HasValue) foreach (var cell in pair.Value.MistCells) if (cell && cell.gameObject.activeInHierarchy) value.ActiveCells++;
            return value;
        }
        public FxConditionerSnapshot ConditionerForValidation(string id)
        {
            if (id == null || !conditioners.TryGetValue(id, out var vat) || lastModel == null) return default;
            var state = lastModel.GetConditioner(id); var fx = vat.Fx;
            return new FxConditionerSnapshot { Present = state.HasValue && vat.Root && vat.Root.gameObject.activeInHierarchy && fxCues != null && fxCues.IsValid(fx.Owner), Owner = fx.Owner, Root = vat.Root,
                Phase = state.HasValue ? state.Value.Phase : ConditionerPhase.Idle, Count = state.HasValue ? state.Value.Count : 0, SecondsRemaining = state.HasValue ? state.Value.SecondsRemaining : 0,
                FirstId = state.HasValue && state.Value.First.HasValue ? state.Value.First.Value.Id : null, SecondId = state.HasValue && state.Value.Second.HasValue ? state.Value.Second.Value.Id : null,
                FirstSpecies = state.HasValue && state.Value.First.HasValue ? state.Value.First.Value.SpeciesId : null, SecondSpecies = state.HasValue && state.Value.Second.HasValue ? state.Value.Second.Value.SpeciesId : null,
                CompletedFirstId = fx.CompletedFirstId, CompletedSecondId = fx.CompletedSecondId, Particles = fxPool == null ? 0 : fxPool.ParticleCountForValidation(fx.Owner),
                WorkingRequests = fx.WorkingRequests, WorkingAdmitted = fx.WorkingAdmitted, StartedCount = fx.StartedCount, CancelledCount = fx.CancelledCount, CompletedCount = fx.CompletedCount, OutputCount = fx.OutputCount };
        }
#endif
    }
}
