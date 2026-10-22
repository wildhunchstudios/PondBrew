using UnityEngine;

namespace EvolutionShop
{
    public enum LabModuleVisualState { Absent, Off, Waiting, Blocked, Paused, StaffPriority }
    public sealed partial class ShopWorld
    {
        readonly Transform[] labNeedles = new Transform[4];
        Transform labFluid, labHarvestSource;
        MeshRenderer labModuleIndicator;
        Vector3 labFluidRest;
        float labDoseRemaining, labHatchRemaining, labModuleRemaining;
        float labHatchDuration;
        uint labHatchSeed;
        ulong labHatchEmitted;
        bool labHatchFirst;
        FxOwnerToken labHatchOwner;
        float labIncubationSeconds = 10;
        CultureKind labDoseCulture;
        FxCueId labDoseId;
        LabModuleVisualState labModuleState;
        string labModuleRequest;
        public int LabHatchCount { get; private set; }
        public int LabModuleCueCount { get; private set; }
        public float LabDoseRemaining => labDoseRemaining;
        public CultureKind LabDoseCulture => labDoseCulture;
        public FxCueId LabDoseId => labDoseId;
        public float LabHatchRemaining => labHatchRemaining;
        public float LabHatchAge => labHatchRemaining > 0 ? labHatchDuration - labHatchRemaining : -1;
        public bool LabHatchFirst => labHatchFirst;
        public LabModuleVisualState LabModuleState => labModuleState;
        public bool LabFxEnabled => fxEnabled;
        public void ConfigureLabTiming(float seconds, float refillSeconds = 2) {
            if (seconds > 0 && !float.IsInfinity(seconds)) labIncubationSeconds = seconds;
            if (refillSeconds > 0 && !float.IsInfinity(refillSeconds)) labGrowerRefillSeconds = refillSeconds;
        }
        void ShowLabRefusal(CultureKind culture) { ResetDoseFx(); labDoseMode=FxDoseMode.Refused; labDoseRemaining = .18f; labDoseCulture = culture; labDoseId = FxCueId.HandRefused; }

        void BuildLabFx()
        {
            // Four tiny moving indicators borrow the current palette and primitive mesh.
            labNeedles[0] = LabNeedle(bloomRoot, "Bloom refill needle", new Vector3(.37f, .475f, -.674f));
            labNeedles[1] = LabNeedle(flowRoot, "Flow refill needle", new Vector3(.37f, .475f, -.674f));
            if (displays.TryGetValue("incubator", out var display))
            {
                var station = display.Root.parent;
                labNeedles[2] = LabNeedle(station, "Hatchery progress needle", new Vector3(.532f, .632f, -.718f));
                labFluid = station.Find("Incubator fluid");
                if (labFluid) labFluidRest = labFluid.localScale;
                BuildDoseFx();
                BuildGrowthFx();
            }
            labNeedles[3] = LabNeedle(upgradeRoot, "Collector module valve", FxResearchAutomationMotion.CollectorValve);
            if (labNeedles[3]) { labModuleIndicator = labNeedles[3].GetComponentInChildren<MeshRenderer>(); labNeedles[3].gameObject.SetActive(false); }
        }
        Transform LabNeedle(Transform station, string name, Vector3 point)
        {
            if (!station) return null;
            var pivot = Group("FX / " + name, station, point);
            Shape(pivot, name, PrimitiveType.Capsule, new Vector3(0, .037f, 0), new Vector3(.014f, .04f, .014f), gold);
            return pivot;
        }
        public void RefreshLabModuleFx(LabModuleVisualState state, string request)
            => RefreshLabAutomationFx(state, LabModuleVisualState.Absent, request);
        static bool LabCue(FxCueId id) => id == FxCueId.DoseCommitted || id == FxCueId.DoseReturned || id == FxCueId.IncubationStarted ||
            id == FxCueId.HatchFirst || id == FxCueId.HatchRepeat || id == FxCueId.ModuleCollected || id == FxCueId.ModuleDosed;
        bool TryLabCue(FxCueId id, uint sequence, FxCueOrigin origin, CultureKind culture)
        {
            bool hatch = id == FxCueId.HatchFirst || id == FxCueId.HatchRepeat;
            if (hatch && fxPolicy != FxViewPolicy.Visible) return false;
            bool module = id == FxCueId.ModuleCollected || id == FxCueId.ModuleDosed;
            if (module)
            {
                bool began = origin == FxCueOrigin.Module && BeginModuleFx(id == FxCueId.ModuleDosed, sequence, culture);
                if (began && id == FxCueId.ModuleDosed) PulseDioramaPipe(culture);
                return began;
            }
            if (origin != FxCueOrigin.Manual) return false;
            var token = fxCues.Find("incubator");
            float duration = id == FxCueId.HatchFirst ? FxHatchChoreography.FirstDuration : id == FxCueId.HatchRepeat ? FxHatchChoreography.RepeatDuration : FxDosePour.Duration;
            if (!fxCues.Play(token, id, sequence, origin, duration, 4)) return false;
            fxPool?.Cancel(token);
            if (id == FxCueId.DoseCommitted || id == FxCueId.DoseReturned)
            {
                BeginDoseFx(token,sequence,id,culture);
                if (id == FxCueId.DoseCommitted) PulseDioramaPipe(culture);
            }
            if (id == FxCueId.HatchFirst || id == FxCueId.HatchRepeat)
            {
                labHatchRemaining = labHatchDuration = duration; LabHatchCount++;
                labHatchFirst = id == FxCueId.HatchFirst; labHatchSeed = sequence;
                labHatchOwner = token; labHatchEmitted = 0;
                TickHatchRelease();
            }
            return true;
        }
        void TickHatchRelease()
        {
            if (labHatchRemaining <= 0 || fxPool == null || !fxCues.IsValid(labHatchOwner) ||
                !displays.TryGetValue("incubator", out var display)) return;
            // AD-A: the diorama look releases its hand-drawn bubble cloud instead (ShopWorldDioramaHatch).
            if (dioramaActive) return;
            float age = LabHatchAge - FxHatchChoreography.Release(labHatchFirst);
            if (age < 0) return;
            int count = FxHatchRelease.Count(labHatchFirst, fxPool.ActionLimit < 64, fxReducedMotion);
            for (int i = 0; i < count; i++)
            {
                ulong bit = 1UL << i;
                if ((labHatchEmitted & bit) != 0) continue;
                FxHatchRelease.Sample(i, labHatchSeed, labHatchFirst, fxReducedMotion, out float delay, out var origin, out var request);
                if (age < delay) continue;
                // Never queue an old burst after a stalled frame, canceled owner, or busy pool.
                labHatchEmitted |= bit;
                request.Lifetime = Mathf.Min(request.Lifetime, labHatchRemaining);
                fxPool.EmitParticle(fxCues, labHatchOwner, display.Root.parent, origin, request);
            }
        }
        bool IncubationQuiet => fxReducedMotion || !isActiveAndEnabled || fxPolicy == FxViewPolicy.Paused || fxPolicy == FxViewPolicy.Unfocused || fxPolicy == FxViewPolicy.Arrange;
        void PoseIncubationProgress()
        {
            if (labNeedles[2]) labNeedles[2].localRotation = Quaternion.Euler(0, 0, -12 * LabGrowthProgress);
        }
        void PoseLabCreature(Display display, float time, float age, bool first)
        {
            if (lastModel.Incubator.Phase == IncubatorPhase.Incubating) { PoseGrowingCreature(display, LabGrowthProgress, time); return; }
            bool quiet = IncubationQuiet;
            if (dioramaActive && PoseDioramaHatchCreature(display, time, age, first, quiet)) return;
            var pose = FxHatchChoreography.Resolve(age, first, quiet);
            float t = time * .78f + display.Seed, calm = pose.IdleWeight;
            var root = display.Creature.transform;
            root.localPosition = quiet ? Vector3.zero : new Vector3(Mathf.Sin(t)*.04f*calm, Mathf.Sin(t*1.8f)*.085f*calm + pose.Lift, 0);
            root.localRotation = quiet ? Quaternion.Euler(0,-90,0) : Quaternion.Euler(0,-90 + Mathf.Cos(t)*20*calm, Mathf.Sin(t*1.6f)*5*calm + pose.Roll);
            root.localScale = pose.Scale * CreatureVisual.IncubationScale(display.SpeciesId) * display.Creature.ContainerScale(true);
            if (display.Creature.IsDiorama)
            {
                // Waiting to be collected: a contented face that watches the player.
                display.Creature.SetDioramaFace(.3f, 0, 0);
                if (dioramaCamera) display.Creature.SetLookTarget(dioramaCamera.transform.position);
            }
            display.Creature.AnimateIncubation(time+display.Seed,.7f,quiet,age,first);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Transform HatchCreatureForValidation => displays.TryGetValue("incubator",out var d) && d.Creature ? d.Creature.transform : null;
        public Transform IncubationFluidForValidation => labFluid;
        public Transform IncubationStationForValidation => displays.TryGetValue("incubator", out var d) ? d.Root.parent : null;
        public string IncubationSpeciesForValidation => displays.TryGetValue("incubator", out var d) ? d.SpeciesId : null;
        public string IncubationSpecimenForValidation => displays.TryGetValue("incubator", out var d) ? d.FxSpecimenId : null;
        public void SampleHatchPoseForValidation(float age, bool first, float time)
        {
            if (displays.TryGetValue("incubator",out var d) && d.Creature) PoseLabCreature(d,time,age,first);
            if (labFluid) labFluid.localScale = labFluidRest * FxHatchChoreography.Resolve(age,first,IncubationQuiet).FluidScale;
        }
        public Transform RebuildIncubationResidentForValidation()
        {
            if (lastModel == null || !displays.TryGetValue("incubator", out var d)) return null;
            if (fxCues != null) CancelFxOwner(fxCues.Find("incubator"));
            SetCreature(d, null); Refresh(lastModel);
            return HatchCreatureForValidation;
        }
#endif
        void EmitLabMote(FxOwnerToken token, Material material, Vector3 point, float duration, float size = .035f, bool action = true)
        {
            if (displays.TryGetValue("incubator", out var display))
                fxPool?.Emit(fxCues, token, display.Root.parent, point, material, duration, size, action);
        }
        void CancelLabFx()
        {
            labHatchEmitted = 0; labHatchOwner = default; labHatchSeed = 0;
            labHarvestSource = null; labDoseRemaining = labHatchRemaining = labModuleRemaining = 0;
            ClearDioramaHatch();
            ResetCollectionFx();
            ResetDoseFx(); ResetGrowthFx();
            foreach (var needle in labNeedles) if (needle) needle.localRotation = Quaternion.identity;
            if (labFluid) labFluid.localScale = labFluidRest;
            PoseIncubationProgress();
            if (displays.TryGetValue("incubator", out var resident) && resident.Creature)
                PoseLabCreature(resident, lastTime, -1, false);
        }
        void TickLabFx(float time, float dt)
        {
            if (!fxEnabled || lastModel == null || fxPolicy == FxViewPolicy.Paused || fxPolicy == FxViewPolicy.Unfocused || fxPolicy == FxViewPolicy.Arrange) return;
            labDoseRemaining = Mathf.Max(0, labDoseRemaining - dt); labHatchRemaining = Mathf.Max(0, labHatchRemaining - dt);
            labModuleRemaining = Mathf.Max(0, labModuleRemaining - dt);
            TickHatchRelease(); TickDoseFx(); RefreshDoseMixture();
            TickResearchAutomationFx(dt);
            TickGrowthFx(time, dt);
            TickCollectionFx(dt);
            var inc = lastModel.Incubator;
            PoseIncubationProgress();
            if (labNeedles[3]) labNeedles[3].localRotation = Quaternion.Euler(0, 0, fxReducedMotion ? 0 : 6 * Mathf.Sin(Mathf.Clamp01(labModuleRemaining / FxResearchAutomationMotion.Duration) * Mathf.PI));
            if (labFluid) labFluid.localScale = labFluidRest * (1f + (fxReducedMotion ? 0 : (inc.Phase == IncubatorPhase.Incubating ? Mathf.Sin(time * 2.2f) * .016f : labDoseRemaining > 0 && labDoseId != FxCueId.HandRefused ? -Mathf.Sin(labDoseRemaining / FxDosePour.Duration * Mathf.PI) * .025f : 0)));
            if (labFluid && labHatchRemaining > 0) labFluid.localScale = labFluidRest * FxHatchChoreography.Resolve(LabHatchAge,labHatchFirst,fxReducedMotion).FluidScale;
        }
    }
}
