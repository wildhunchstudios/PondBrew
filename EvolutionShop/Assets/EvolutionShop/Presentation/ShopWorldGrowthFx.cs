using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        readonly Transform[] growthMedia = new Transform[2];
        readonly MeshRenderer[] growthMediumRenderers = new MeshRenderer[2];
        readonly MaterialPropertyBlock[] growthMediumProperties = new MaterialPropertyBlock[2];
        readonly Color[] growthMediumColors = new Color[2];
        readonly float[] growthBubbleClocks = new float[3];
        readonly uint[] growthBubbleSequences = new uint[3];
        readonly FxGrowerVisualState[] growthPreviousStates = new FxGrowerVisualState[2];
        IncubatorPhase growthPreviousPhase;
        float labGrowerRefillSeconds = 2;
        bool growthStateKnown;
        public int GrowthAmbientEmissionCount { get; private set; }
        public float LabGrowthProgress => lastModel == null ? 0 : lastModel.Incubator.Phase == IncubatorPhase.Ready ? 1 :
            lastModel.Incubator.Phase == IncubatorPhase.Incubating ? FxGrowthMotion.Progress((float)lastModel.Incubator.SecondsRemaining, labIncubationSeconds) : 0;
        public bool LabGrowthActive => fxEnabled && isActiveAndEnabled && lastModel != null && lastModel.Incubator.Phase == IncubatorPhase.Incubating &&
            fxPolicy == FxViewPolicy.Visible && fxCues != null && fxCues.IsValid(fxCues.Find("incubator")) &&
            displays.TryGetValue("incubator", out var growthDisplay) && growthDisplay.Root && growthDisplay.Root.gameObject.activeInHierarchy;

        void BuildGrowthFx()
        {
            for (int i = 0; i < 2; i++) {
                var root = i == 0 ? bloomRoot : flowRoot;
                growthMedia[i] = root ? root.Find("Growing medium") : null;
                growthMediumRenderers[i] = growthMedia[i] ? growthMedia[i].GetComponent<MeshRenderer>() : null;
                if (!growthMediumRenderers[i]) continue;
                growthMediumColors[i] = growthMediumRenderers[i].sharedMaterial.GetColor(DoseFluidColor);
                growthMediumProperties[i] = new MaterialPropertyBlock();
            }
            ResetGrowthFx();
        }
        FxGrowerPose ReadGrowthGrower(int index)
        {
            if (lastModel == null || index < 0 || index > 1) return default;
            var grower = lastModel.GetGrower((CultureKind)index);
            return FxGrowthMotion.ReadGrower(grower.Units, grower.Capacity, grower.SecondsToNextUnit, labGrowerRefillSeconds);
        }
        void ReconcileGrowthFx()
        {
            if (lastModel == null) return;
            // Clear only ambient slots: a freshly committed hatch/dose retains its existing cue owner.
            var phase = lastModel.Incubator.Phase;
            if (growthStateKnown && phase != growthPreviousPhase) {
                fxPool?.CancelAmbient(fxCues.Find("incubator")); growthBubbleClocks[2] = .14f;
            }
            growthPreviousPhase = phase;
            for (int i = 0; i < 2; i++) {
                var state = ReadGrowthGrower(i);
                if (growthStateKnown && state.State != growthPreviousStates[i]) {
                    fxPool?.CancelAmbient(fxCues.Find(i == 0 ? "bloom" : "flow")); growthBubbleClocks[i] = .20f + i * .11f;
                }
                growthPreviousStates[i] = state.State;
                PoseGrowthMedium(i, state);
            }
            growthStateKnown = true;
        }
        void PoseGrowthMedium(int i, FxGrowerPose state)
        {
            if (!growthMedia[i]) return;
            float height = .44f * Mathf.Lerp(.55f, 1, state.Fill);
            growthMedia[i].localScale = new Vector3(.92f, height, .92f);
            growthMedia[i].localPosition = new Vector3(0, .93f + height, 0);
            Color baseColor = growthMediumColors[i];
            Color culture = i == 0 ? new Color(1, .64f, .66f) : new Color(.34f, .83f, .98f);
            Color tint = Color.Lerp(baseColor, culture, state.Fill * .30f); tint.a = baseColor.a;
            growthMediumProperties[i].SetColor(DoseFluidColor, tint);
            growthMediumRenderers[i].SetPropertyBlock(growthMediumProperties[i]);
        }
        Color GrowthFluidTint(Color original)
        {
            if (!fxEnabled || lastModel == null || lastModel.Incubator.Phase != IncubatorPhase.Incubating) return original;
            float p = LabGrowthProgress;
            Color life = Color.Lerp(new Color(.35f, .80f, .91f), new Color(.83f, .89f, .52f), p);
            float breath = fxReducedMotion || fxPolicy != FxViewPolicy.Visible ? 0 : Mathf.Sin(lastTime * 2.2f) * .035f;
            Color tint = Color.Lerp(original, life, .22f + p * .16f + breath); tint.a = original.a;
            return tint;
        }
        void PoseGrowingPondlet(Display display, float progress, float time)
        {
            bool quiet = fxReducedMotion || fxPolicy == FxViewPolicy.Paused || fxPolicy == FxViewPolicy.Unfocused || fxPolicy == FxViewPolicy.Arrange;
            var pose = FxGrowthMotion.Resolve(progress, time, display.Seed, quiet);
            var root = display.Creature.transform;
            root.localPosition = pose.Position; root.localRotation = Quaternion.Euler(pose.Angles); root.localScale = pose.Scale;
            display.Creature.AnimatePondlet(time + display.Seed, pose.Effort, quiet);
        }
        void ResetGrowthFx()
        {
            // Restart with separated delays, never an accumulated catch-up burst.
            growthBubbleClocks[0] = .28f; growthBubbleClocks[1] = .51f; growthBubbleClocks[2] = .14f;
            for (int i = 0; i < 2; i++) {
                var state = ReadGrowthGrower(i); PoseGrowthMedium(i, state);
                var cells = i == 0 ? bloomCells : flowCells;
                for (int c = 0; c < cells.Count; c++) {
                    cells[c].localPosition = FxGrowthMotion.Cell(c, i, 0, state, true);
                    cells[c].localRotation = Quaternion.Euler(0, (c * 2.399f + i * 1.73f) * Mathf.Rad2Deg, 0);
                }
            }
        }
        void TickGrowthFx(float time, float dt)
        {
            bool reduced = fxReducedMotion, visible = fxPolicy == FxViewPolicy.Visible;
            // The incubator gets first admission to the existing ambient partition.
            if (LabGrowthActive && labHatchRemaining <= 0 && labDoseRemaining <= 0)
                TickGrowthBubbles(2, time, dt, LabGrowthProgress, false, 0, reduced);
            for (int i = 0; i < 2; i++) {
                var state = ReadGrowthGrower(i);
                bool working = state.State != FxGrowerVisualState.Full;
                float needle = working ? Mathf.Lerp(10, -10, state.Refill) : -10;
                if (working && !reduced) needle += Mathf.Sin(time * 2.4f + i) * 3;
                if (labNeedles[i]) labNeedles[i].localRotation = Quaternion.Euler(0, 0, needle);
                var cells = i == 0 ? bloomCells : flowCells;
                for (int c = 0; c < cells.Count; c++) {
                    cells[c].localPosition = FxGrowthMotion.Cell(c, i, time, state, reduced);
                    float phase = c * 2.399f + i * 1.73f;
                    cells[c].localRotation = Quaternion.Euler(reduced ? 0 : Mathf.Sin(time * .8f + phase) * 9,
                        phase * Mathf.Rad2Deg + (reduced ? 0 : Mathf.Sin(time + c) * (working ? 18 : 4)), 0);
                }
                // Empty chambers cool quietly. Only actual units drift; no prospective culture is drawn.
                if (!visible || !working) continue;
                // Low supports two ambient emitters; share its second emitter over time while incubating.
                if (LabDoseLow && LabGrowthActive && (int)(time / 2f) % 2 != i) continue;
                TickGrowthBubbles(i, time, dt, state.Refill, true, i, reduced);
            }
        }
        void TickGrowthBubbles(int lane, float time, float dt, float progress, bool grower, int culture, bool reduced)
        {
            if (dt <= 0 || fxPool == null) return;
            growthBubbleClocks[lane] -= dt;
            if (growthBubbleClocks[lane] > 0) return;
            bool depleted = grower && ReadGrowthGrower(culture).State == FxGrowerVisualState.Depleted;
            float interval = reduced ? 1.4f : depleted ? 1.2f : grower ? Mathf.Lerp(.48f, .28f, progress) : Mathf.Lerp(.42f, .23f, progress);
            growthBubbleClocks[lane] = interval * (LabDoseLow ? 1.55f : 1) + (growthBubbleSequences[lane] % 3) * .031f;
            var owner = fxCues.Find(grower ? culture == 0 ? "bloom" : "flow" : "incubator");
            Transform source = grower ? culture == 0 ? bloomRoot : flowRoot : displays.TryGetValue("incubator", out var d) ? d.Root.parent : null;
            FxGrowthMotion.Bubble(++growthBubbleSequences[lane], progress, grower, culture, reduced, out var origin, out var request);
            if (grower) {
                float fill = ReadGrowthGrower(culture).Fill;
                // Every lane terminates beneath the actual medium surface, including depleted cooling.
                request.End.y *= Mathf.Lerp(.30f, 1, fill);
                if (depleted) { request.Color = new Color(.63f, .83f, .87f, .40f); request.Size *= .7f; request.EndSize *= .7f; }
                if (reduced) origin.y = Mathf.Min(origin.y, .93f + .88f * Mathf.Lerp(.55f, 1, fill) - .10f);
            }
            if (fxPool.EmitParticle(fxCues, owner, source, origin, request, false)) GrowthAmbientEmissionCount++;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Transform GrowthCreatureForValidation => HatchCreatureForValidation;
        public Transform GrowerMediumForValidation(CultureKind culture) => (int)culture >= 0 && (int)culture < 2 ? growthMedia[(int)culture] : null;
        public FxGrowerPose GrowthGrowerForValidation(CultureKind culture) => ReadGrowthGrower((int)culture);
        public int GrowthIncubatorAmbientCount => fxPool != null && fxCues != null ? fxPool.AmbientCount(fxCues.Find("incubator")) : 0;
        public int GrowthGrowerAmbientCount(CultureKind culture) => fxPool != null && fxCues != null && (culture == CultureKind.Bloom || culture == CultureKind.Flow) ? fxPool.AmbientCount(fxCues.Find(culture == CultureKind.Bloom ? "bloom" : "flow")) : 0;
        public void SampleGrowthPoseForValidation(float progress, float time)
        { if (displays.TryGetValue("incubator", out var d) && d.Creature && d.SpeciesId == "pondlet") PoseGrowingPondlet(d, progress, time); }
#endif
    }
}
