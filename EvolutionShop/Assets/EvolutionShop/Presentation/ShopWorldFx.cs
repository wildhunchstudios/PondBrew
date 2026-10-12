using UnityEngine;

namespace EvolutionShop
{
    public enum FxViewPolicy { Visible, Modal, Paused, Unfocused, Arrange }
    public sealed partial class ShopWorld
    {
        struct FxBinding
        {
            public FxOwnerToken Token;
            public Transform Root;
            public string Identity;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public bool FollowsTool;
        }
        FxCueRuntime fxCues;
        FxEffectPool fxPool;
        readonly FxBinding[] fxBindings = new FxBinding[FxCueRuntime.OwnerLimit];
        FxViewPolicy fxPolicy;
        bool fxEnabled = true, fxReducedMotion;
        public FxCueRuntime FxCues => fxCues;
        public FxEffectPool FxPool => fxPool;
        public FxViewPolicy FxPolicy => fxPolicy;

        void BuildFxPresentation()
        {
            DisposeFxPresentation();
            fxCues = new FxCueRuntime();
            // Borrow the existing primitive mesh. New pulses add no material or mesh instances/colliders.
            var template = bloomCells.Count > 0 && bloomCells[0] ? bloomCells[0].GetComponentInChildren<MeshFilter>() : null;
            if (template && template.sharedMesh) fxPool = new FxEffectPool(transform, template.sharedMesh);
            BindFxOwner("tool", null, toolBody, true);
            fxPolicy = FxViewPolicy.Visible;
            BuildLabFx();
            BuildHandsVisual();
        }
        public FxOwnerToken BindFxOwner(string key, string identity, Transform root, bool followsTool = false)
        {
            if (fxCues == null || !root) return default;
            var old = fxCues.Find(key);
            if (fxCues.IsValid(old))
            {
                var previous = fxBindings[old.Slot];
                if (previous.Root != root || previous.Identity != identity || (!followsTool &&
                    ((root.position - previous.Position).sqrMagnitude > .000001f || Quaternion.Angle(root.rotation, previous.Rotation) > .01f || (root.lossyScale - previous.Scale).sqrMagnitude > .000001f)))
                { CancelFxOwner(old); fxCues.Unregister(old); }
            }
            var token = fxCues.Register(key, identity);
            if (!fxCues.IsValid(token)) return default;
            fxBindings[token.Slot] = new FxBinding { Token = token, Root = root, Identity = identity,
                Position = root.position, Rotation = root.rotation, Scale = root.lossyScale, FollowsTool = followsTool };
            return token;
        }
        void ReconcileFxState(GameModel model)
        {
            if (fxCues == null || model == null) return;
            RefreshDoseMixture();
            BindFxOwner("tool", null, toolBody, true);
            BindHandsOwner();
            ReconcileFxGrower("bloom", bloomRoot, model.GetGrower(CultureKind.Bloom).Units, model.GetGrower(CultureKind.Bloom).Capacity);
            ReconcileFxGrower("flow", flowRoot, model.GetGrower(CultureKind.Flow).Units, model.GetGrower(CultureKind.Flow).Capacity);
            foreach (var pair in displays)
            {
                var display = pair.Value;
                var owner = BindFxOwner(pair.Key, display.FxSpecimenId, display.Root);
                var state = display.Incubator ? model.Incubator.Phase == IncubatorPhase.Ready ? FxSteadyState.Ready :
                    model.Incubator.Phase == IncubatorPhase.Incubating ? FxSteadyState.Working : FxSteadyState.Dosing :
                    display.Creature ? model.GetReservation(pair.Key).HasValue ? FxSteadyState.Reserved : FxSteadyState.Occupied : FxSteadyState.Empty;
                fxCues.SetState(owner, state);
            }
            ReconcileGrowthFx();
        }
        void ReconcileFxGrower(string key, Transform root, int units, int capacity)
        { var owner = BindFxOwner(key, null, root); fxCues.SetState(owner, units == 0 ? FxSteadyState.Empty : units >= capacity ? FxSteadyState.Full : FxSteadyState.Refilling); }
        public bool TryFxCue(FxCueId id, uint sequence, FxCueOrigin origin = FxCueOrigin.Manual, CultureKind culture = CultureKind.Bloom)
        {
            if (!fxEnabled || fxCues == null || !isActiveAndEnabled || fxPolicy == FxViewPolicy.Paused || fxPolicy == FxViewPolicy.Unfocused || fxPolicy == FxViewPolicy.Arrange) return false;
            if (LabCue(id)) return TryLabCue(id, sequence, origin, culture);
            if (id == FxCueId.HandPlace || id == FxCueId.HandUpgrade || id == FxCueId.HandDose) return TryHandCommand(id, sequence, origin);
            // Other typed names remain later production work.
            if (id != FxCueId.CollectedUnit && id != FxCueId.DoseCommitted && id != FxCueId.HandRefused) return false;
            if (id == FxCueId.CollectedUnit && fxPolicy != FxViewPolicy.Visible) return false;
            // Automatic callers have no hand/tool binding. Real module/staff art is a later assignment.
            if (origin != FxCueOrigin.Manual) return false;
            bool atVessel = id == FxCueId.DoseCommitted || (id == FxCueId.HandRefused && fxPolicy == FxViewPolicy.Modal);
            var token = fxCues.Find(atVessel ? "incubator" : "tool");
            int priority = id == FxCueId.HandRefused ? 5 : 4;
            float duration = id == FxCueId.HandRefused ? .18f : .28f;
            if (!fxCues.Play(token, id, sequence, origin, duration, priority)) return false;
            if (id == FxCueId.HandRefused) { fxPool?.Cancel(token); if (fxPolicy == FxViewPolicy.Modal) ShowLabRefusal(culture); }
            Transform source = atVessel && displays.TryGetValue("incubator", out var vessel) ? vessel.Root.parent : id == FxCueId.CollectedUnit ? ToolChamber : toolBody;
            var point = atVessel ? new Vector3(0, 1.48f, -.46f) : id == FxCueId.CollectedUnit ? new Vector3(0, -.02f, -.13f) : new Vector3(.085f, -.129f, -.195f);
            Material accent = id == FxCueId.HandRefused ? gold : culture == CultureKind.Flow ? flowMat : culture == CultureKind.Spark ? sparkMat : culture == CultureKind.Mist ? mistMat : bloomMat;
            // Missing attachment/material/pool falls back to the existing committed text/audio.
            fxPool?.Emit(fxCues, token, source, point, accent, duration, fxReducedMotion ? .025f : .055f);
            if (!atVessel) AcceptHandPose(id, duration);
            return true;
        }
        public void CancelFxOwner(FxOwnerToken owner)
        {
            if (fxCues != null && owner.Equals(fxCues.Find("incubator"))) CancelLabFx();
            if (fxCues != null && owner.Equals(fxCues.Find("lab-collector"))) labModuleRemaining = 0;
            fxCues?.Cancel(owner); fxPool?.Cancel(owner);
            if (fxCues != null && owner.Equals(fxCues.Find("hands"))) CancelHandsPose();
        }
        public void CancelFxTransient()
        {
            fxCues?.CancelAll(); fxPool?.CancelAll(); CancelLabFx(); CancelHandsPose(); CancelAquariumTransient();
            // The inherited intake remains in FX-02, but cannot leave stale particles at a boundary.
            for (int i = particles.Count - 1; i >= 0; i--) if (particles[i].Root)
            { particles[i].Root.gameObject.SetActive(false); Destroy(particles[i].Root.gameObject); }
            particles.Clear(); harvesting = false; emissionClock = 0; harvestKick = 0;
            if (toolNozzle) toolNozzle.localRotation = Quaternion.identity;
        }
        public void SetFxViewPolicy(FxViewPolicy value)
        { if (fxPolicy == value) return; fxPolicy = value; CancelFxTransient(); }
        public void ResetFxPresentation()
        {
            CancelFxTransient(); fxCues?.Reset(); System.Array.Clear(fxBindings, 0, fxBindings.Length);
            if (lastModel != null) ReconcileFxState(lastModel);
        }
        void TickFxPresentation(float dt)
        {
            if (fxCues == null) return;
            for (int i = 0; i < fxBindings.Length; i++)
            {
                var binding = fxBindings[i]; if (!fxCues.IsValid(binding.Token)) continue;
                if (!binding.Root || !binding.Root.gameObject.activeInHierarchy)
                { CancelFxOwner(binding.Token); fxCues.Unregister(binding.Token); fxBindings[i] = default; continue; }
                if (!binding.FollowsTool && ((binding.Root.position - binding.Position).sqrMagnitude > .000001f ||
                    Quaternion.Angle(binding.Root.rotation, binding.Rotation) > .01f || (binding.Root.lossyScale - binding.Scale).sqrMagnitude > .000001f))
                { CancelFxOwner(binding.Token); fxCues.Unregister(binding.Token); fxBindings[i] = default; }
            }
            fxCues.Tick(dt); fxPool?.Tick(fxCues, dt);
        }
        void DisposeFxPresentation()
        { fxPool?.Dispose(); fxCues?.Dispose(); fxPool = null; fxCues = null; System.Array.Clear(fxBindings, 0, fxBindings.Length); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Isolated review/test switches only. No user settings or ordinary-save schema in FX-02.
        public void SetFxPreviewModes(bool reducedMotion, bool lowDetail) { CancelFxTransient(); fxReducedMotion = reducedMotion; aquariumLow = lowDetail; fxPool?.SetLowDetail(lowDetail); fxPool?.SetReducedMotion(reducedMotion); }
        public void SetFxEnabledForValidation(bool value) { CancelFxTransient(); fxEnabled = value; }
#endif
    }
}
