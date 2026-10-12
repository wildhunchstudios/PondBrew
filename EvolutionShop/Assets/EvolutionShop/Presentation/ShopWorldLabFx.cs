using UnityEngine;

namespace EvolutionShop
{
    public enum LabModuleVisualState { Absent, Off, Waiting, Blocked }
    public sealed partial class ShopWorld
    {
        readonly Transform[] labNeedles = new Transform[4];
        Transform labFluid, labHarvestSource;
        MeshRenderer labModuleIndicator;
        Vector3 labFluidRest;
        float labStreamClock, labBubbleClock, labDoseRemaining, labHatchRemaining, labModuleRemaining;
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
        public LabModuleVisualState LabModuleState => labModuleState;
        public bool LabFxEnabled => fxEnabled;
        public void ConfigureLabTiming(float seconds) { if (seconds > 0 && !float.IsInfinity(seconds)) labIncubationSeconds = seconds; }
        void ShowLabRefusal(CultureKind culture) { labDoseRemaining = .18f; labDoseCulture = culture; labDoseId = FxCueId.HandRefused; }

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
            }
            labNeedles[3] = LabNeedle(upgradeRoot, "Collector module valve", new Vector3(-.48f, .88f, -.615f));
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
        {
            if (fxCues == null || !labNeedles[3]) return;
            bool requestChanged = request != labModuleRequest;
            bool changed = state != labModuleState || requestChanged;
            labModuleState = state; labModuleRequest = request;
            labNeedles[3].gameObject.SetActive(state != LabModuleVisualState.Absent);
            if (changed && labModuleIndicator) labModuleIndicator.sharedMaterial = state == LabModuleVisualState.Off ? trim : state == LabModuleVisualState.Blocked ? bloomMat : gold;
            var owner = BindFxOwner("lab-collector", request, upgradeRoot);
            if (changed && (requestChanged || state != LabModuleVisualState.Waiting)) { CancelFxOwner(owner); labModuleRemaining = 0; }
            fxCues.SetState(owner, state == LabModuleVisualState.Blocked ? FxSteadyState.Reserved : FxSteadyState.Empty);
        }
        static bool LabCue(FxCueId id) => id == FxCueId.DoseCommitted || id == FxCueId.DoseReturned || id == FxCueId.IncubationStarted ||
            id == FxCueId.HatchFirst || id == FxCueId.HatchRepeat || id == FxCueId.ModuleCollected || id == FxCueId.ModuleDosed;
        bool TryLabCue(FxCueId id, uint sequence, FxCueOrigin origin, CultureKind culture)
        {
            bool module = id == FxCueId.ModuleCollected || id == FxCueId.ModuleDosed;
            if (module ? origin != FxCueOrigin.Module || (labModuleState != LabModuleVisualState.Waiting && labModuleState != LabModuleVisualState.Blocked) : origin != FxCueOrigin.Manual) return false;
            var token = fxCues.Find(module ? "lab-collector" : "incubator");
            float duration = id == FxCueId.HatchFirst ? 1.2f : id == FxCueId.HatchRepeat ? .5f : .4f;
            if (!fxCues.Play(token, id, sequence, origin, duration, 4)) return false;
            fxPool?.Cancel(token);
            if (module) { labModuleRemaining = .3f; LabModuleCueCount++; return true; }
            if (id == FxCueId.DoseCommitted || id == FxCueId.DoseReturned)
            {
                labDoseRemaining = .4f; labDoseId = id; labDoseCulture = culture;
                // The normal panel has its own visible measured strip; no pretend hand through the modal.
                EmitLabMote(token, culture == CultureKind.Flow ? flowMat : bloomMat, new Vector3(0, 1.65f, -.35f), .4f);
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
            float age = labHatchDuration - labHatchRemaining;
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
        void EmitLabMote(FxOwnerToken token, Material material, Vector3 point, float duration, float size = .035f, bool action = true)
        {
            if (displays.TryGetValue("incubator", out var display))
                fxPool?.Emit(fxCues, token, display.Root.parent, point, material, duration, size, action);
        }
        void CancelLabFx()
        {
            labHatchEmitted = 0; labHatchOwner = default; labHatchSeed = 0;
            labHarvestSource = null; labStreamClock = labBubbleClock = labDoseRemaining = labHatchRemaining = labModuleRemaining = 0;
            foreach (var needle in labNeedles) if (needle) needle.localRotation = Quaternion.identity;
            if (labFluid) labFluid.localScale = labFluidRest;
            if (displays.TryGetValue("incubator", out var resident) && resident.Creature) resident.Creature.transform.localScale = Vector3.one * .62f;
        }
        void TickLabFx(float time, float dt)
        {
            if (!fxEnabled || lastModel == null || fxPolicy == FxViewPolicy.Paused || fxPolicy == FxViewPolicy.Unfocused || fxPolicy == FxViewPolicy.Arrange) return;
            labDoseRemaining = Mathf.Max(0, labDoseRemaining - dt); labHatchRemaining = Mathf.Max(0, labHatchRemaining - dt);
            labModuleRemaining = Mathf.Max(0, labModuleRemaining - dt);
            TickHatchRelease();
            for (int i = 0; i < 2; i++)
            {
                var grower = lastModel.GetGrower((CultureKind)i);
                float working = grower.Units < grower.Capacity && grower.SecondsToNextUnit > 0 ? Mathf.Sin(time * 2f + i) * 8f : 0;
                if (harvesting && harvestingCulture == (CultureKind)i) working = 8;
                if (labNeedles[i]) labNeedles[i].localRotation = Quaternion.Euler(0, 0, fxReducedMotion ? 0 : working);
                var cells = i == 0 ? bloomCells : flowCells;
                for (int c = 0; c < cells.Count; c++)
                {
                    float phase = c * 2.399f + i * 1.73f;
                    // Own these two cell poses once, replacing the inherited wide always-on orbit.
                    cells[c].localPosition = new Vector3(Mathf.Cos(phase) * .27f, 1.15f + c % 3 * .22f + (fxReducedMotion ? 0 : Mathf.Sin(time + phase) * .015f), Mathf.Sin(phase) * .27f);
                    cells[c].localRotation = Quaternion.Euler(0, phase * Mathf.Rad2Deg + (fxReducedMotion ? 0 : Mathf.Sin(time + c) * 6), 0);
                }
            }
            if (harvesting && labHarvestSource && (harvestingCulture == CultureKind.Bloom || harvestingCulture == CultureKind.Flow) && fxPolicy == FxViewPolicy.Visible)
            {
                labStreamClock -= dt;
                if (labStreamClock <= 0 && toolBody)
                {
                    labStreamClock = 1f / 6;
                    fxPool?.EmitTravel(fxCues, fxCues.Find(harvestingCulture == CultureKind.Bloom ? "bloom" : "flow"), labHarvestSource,
                        new Vector3(.36f, 1f, -.76f), toolBody, new Vector3(0, .05f, .489f), harvestingCulture == CultureKind.Flow ? flowMat : bloomMat, fxReducedMotion ? .025f : .04f);
                }
            }
            var inc = lastModel.Incubator;
            float fraction = inc.Phase == IncubatorPhase.Incubating ? Mathf.Clamp01(1f - inc.SecondsRemaining / labIncubationSeconds) : inc.Phase == IncubatorPhase.Ready ? 1 : 0;
            if (labNeedles[2]) labNeedles[2].localRotation = Quaternion.Euler(0, 0, -12 * fraction);
            if (labNeedles[3]) labNeedles[3].localRotation = Quaternion.Euler(0, 0, fxReducedMotion ? 0 : 6 * Mathf.Sin(Mathf.Clamp01(labModuleRemaining / .3f) * Mathf.PI));
            if (labFluid) labFluid.localScale = labFluidRest * (1f + (fxReducedMotion ? 0 : (inc.Phase == IncubatorPhase.Incubating ? Mathf.Sin(time * 5) * .009f : labDoseRemaining > 0 ? Mathf.Sin(labDoseRemaining / .4f * Mathf.PI) * .018f : 0)));
            if (inc.Phase == IncubatorPhase.Incubating)
            {
                labBubbleClock -= dt;
                if (labBubbleClock <= 0) { labBubbleClock = fxReducedMotion ? 1.2f : .65f; EmitLabMote(fxCues.Find("incubator"), flowMat, new Vector3(Mathf.Sin(time) * .30f, 1.36f, -.18f), .8f, .025f, false); }
            }
            if (displays.TryGetValue("incubator", out var resident) && resident.Creature && resident.SpeciesId == "pondlet")
            {
                float size = inc.Phase == IncubatorPhase.Incubating ? Mathf.Lerp(.32f, 1, fraction) : 1;
                if (labHatchRemaining > 0 && !fxReducedMotion) size *= 1 + Mathf.Sin((1 - labHatchRemaining / labHatchDuration) * Mathf.PI) * .02f;
                resident.Creature.transform.localScale = Vector3.one * (.62f * size);
            }
        }
    }
}
