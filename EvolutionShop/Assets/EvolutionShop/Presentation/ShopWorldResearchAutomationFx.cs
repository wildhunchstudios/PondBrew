using UnityEngine;

namespace EvolutionShop
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct FxResearchAutomationSnapshot
    {
        public LabModuleVisualState CollectorState, DoserState;
        public FxOwnerToken CollectorOwner, DoserOwner, ResearchOwner;
        public Transform CollectorRoot, DoserRoot, ResearchRoot;
        public Vector3 CollectorPoint, DoserPoint, ResearchPoint, CollectorLocal, DoserLocal, ResearchLocal, CollectorEnd, DoserEnd;
        public int CollectorCount, DoserCount, ResearchCount, CollectorParticles, DoserParticles, ResearchParticles;
        public string RequestId;
        public FxResearchUpgrade LastResearch;
        public CultureKind LastCulture;
    }
#endif
    public sealed partial class ShopWorld
    {
        LabModuleVisualState labDoserState;
        FxOwnerToken collectorFxOwner, doserFxOwner, researchFxOwner;
        Transform collectorFxRoot, doserFxRoot, researchFxRoot;
        Vector3 collectorFxLocal, researchFxLocal;
        float collectorFxRemaining, doserFxRemaining, researchFxRemaining;
        uint collectorFxSeed, doserFxSeed, researchFxSeed;
        ushort collectorFxEmitted, doserFxEmitted, researchFxEmitted;
        CultureKind collectorFxCulture;
        FxDoseMix doserFxMix;
        Vector3 moduleBenchPosition, moduleBenchScale;
        Quaternion moduleBenchRotation;
        readonly int[] collectorCultureCounts = new int[4], doserCultureCounts = new int[4];
        int doserFxCount, researchFxCount;
        FxResearchUpgrade lastResearchUpgrade;
        public LabModuleVisualState LabDoserState => labDoserState;
        public float ResearchFxRemaining => researchFxRemaining;
        public float ResearchFxAge => researchFxRemaining > 0 ? FxResearchAutomationMotion.Duration - researchFxRemaining : -1;
        public uint ResearchFxSeed => researchFxSeed;
        public FxResearchUpgrade LastResearchUpgrade => lastResearchUpgrade;

        public void RefreshLabAutomationFx(LabModuleVisualState collector, LabModuleVisualState doser, string request)
        {
            if (fxCues == null) return;
            bool requestChanged = request != labModuleRequest;
            if (requestChanged || collector != labModuleState && collector != LabModuleVisualState.Waiting)
                CancelResearchAutomationOwner(collectorFxOwner);
            if (requestChanged || doser != labDoserState && doser != LabModuleVisualState.Waiting)
                CancelResearchAutomationOwner(doserFxOwner);
            if (requestChanged) { fxCues.Unregister(collectorFxOwner); fxCues.Unregister(doserFxOwner); }
            labModuleState = collector; labDoserState = doser; labModuleRequest = request;
            if (labNeedles[3]) labNeedles[3].gameObject.SetActive(collector != LabModuleVisualState.Absent);
            if (labModuleIndicator) labModuleIndicator.sharedMaterial = collector == LabModuleVisualState.Off || collector == LabModuleVisualState.Paused ? trim :
                collector == LabModuleVisualState.Blocked || collector == LabModuleVisualState.StaffPriority ? bloomMat : gold;
            // Only immutable labels change on state transitions; no per-frame string construction.
            if (upgradeRoot && status.TryGetValue(StationKind.Upgrade, out var label) &&
                (collector != LabModuleVisualState.Absent || doser != LabModuleVisualState.Absent)) {
                label.text = automationStatusLabels[(int)collector * 6 + (int)doser];
            }
        }
        static readonly string[] automationStatusLabels = BuildAutomationStatusLabels();
        static string[] BuildAutomationStatusLabels()
        {
            var labels = new string[36];
            for (int c = 0; c < 6; c++) for (int d = 0; d < 6; d++)
                labels[c * 6 + d] = "COLLECTOR " + FxResearchAutomationMotion.StateLabel((LabModuleVisualState)c) + " / DOSER " + FxResearchAutomationMotion.StateLabel((LabModuleVisualState)d);
            return labels;
        }
        bool BeginModuleFx(bool doser, uint sequence, CultureKind culture)
        {
            var state = doser ? labDoserState : labModuleState;
            if (state == LabModuleVisualState.Absent || state == LabModuleVisualState.Off || state == LabModuleVisualState.Paused || state == LabModuleVisualState.StaffPriority ||
                !upgradeRoot || !upgradeRoot.gameObject.activeInHierarchy || lastModel == null) return false;
            CancelMovedModuleBench();
            Transform root;
            if (doser) root = displays.TryGetValue("incubator", out var display) ? display.Root.parent : null;
            else root = culture == CultureKind.Bloom ? bloomRoot : culture == CultureKind.Flow ? flowRoot : AdvancedSourceRoot(culture);
            if (!root || !root.gameObject.activeInHierarchy) return false;
            string key = doser ? "lab-doser" : "lab-collector";
            var owner = BindFxOwner(key, null, root);
            if (!fxCues.Play(owner, doser ? FxCueId.ModuleDosed : FxCueId.ModuleCollected, sequence, FxCueOrigin.Module, FxResearchAutomationMotion.Duration, 4)) return false;
            fxPool?.Cancel(owner);
            moduleBenchPosition = upgradeRoot.position; moduleBenchRotation = upgradeRoot.rotation; moduleBenchScale = upgradeRoot.lossyScale;
            if (doser) {
                doserFxRoot = root; doserFxOwner = owner; doserFxSeed = sequence; doserFxEmitted = 0;
                doserFxMix = FxDoseMix.Read(lastModel); doserFxRemaining = FxResearchAutomationMotion.Duration; doserFxCount++;
                for (int i = 0; i < doserFxMix.Total; i++) doserCultureCounts[(int)doserFxMix.At(i)]++;
            } else {
                collectorFxRoot = root; collectorFxOwner = owner; collectorFxSeed = sequence; collectorFxEmitted = 0; collectorFxCulture = culture;
                collectorFxLocal = FxAdvancedLabMotion.IsAdvanced(culture) ? FxAdvancedLabMotion.SourcePoint(culture) : FxCollectionMotion.OutletLocal;
                collectorFxRemaining = labModuleRemaining = FxResearchAutomationMotion.Duration; LabModuleCueCount++; collectorCultureCounts[(int)culture]++;
                // The bench valve settles locally. It never pretends to pipe material across the room.
                if (fxPool != null) {
                    var p = FxParticleRequest.Create(FxParticleShape.Ring, FxDosePour.ColorOf(culture));
                    p.Size = .09f; p.EndSize = .16f; p.Lifetime = .32f;
                    fxPool.EmitParticle(fxCues, owner, upgradeRoot, FxResearchAutomationMotion.CollectorValve, p);
                }
            }
            EmitResearchAutomationParticles();
            return true;
        }
        public bool PresentResearchUpgrade(FxResearchUpgrade kind, uint sequence, string conditionerId = null)
        {
            if (kind == FxResearchUpgrade.None || !AdvancedCommandVisible || fxCues == null || lastModel == null) return false;
            Transform root = upgradeRoot;
            Vector3 point = FxResearchAutomationMotion.ResearchSample;
            if (kind == FxResearchUpgrade.Mist) {
                if (conditionerId == null || !conditioners.TryGetValue(conditionerId, out var vat) || !lastModel.GetConditioner(conditionerId).HasValue) return false;
                root = vat.Root; point = new Vector3(0, 1.04f, -.57f);
            }
            if (!root || !root.gameObject.activeInHierarchy) return false;
            var owner = BindFxOwner("research-upgrade", null, root);
            if (!fxCues.Play(owner, FxCueId.HandUpgrade, sequence, FxCueOrigin.Manual, FxResearchAutomationMotion.Duration, 4)) return false;
            fxPool?.Cancel(owner); researchFxOwner = owner; researchFxRoot = root; researchFxLocal = point;
            researchFxSeed = sequence; researchFxEmitted = 0; researchFxRemaining = FxResearchAutomationMotion.Duration;
            lastResearchUpgrade = kind; researchFxCount++;
            EmitResearchAutomationParticles(); return true;
        }
        public void CancelResearchUpgrade() { CancelResearchAutomationOwner(researchFxOwner); }
        void CancelResearchAutomationOwner(FxOwnerToken owner)
        {
            if (!owner.IsDefined) return;
            if (owner.Equals(collectorFxOwner)) { collectorFxRemaining = labModuleRemaining = 0; collectorFxEmitted = 0; }
            if (owner.Equals(doserFxOwner)) { doserFxRemaining = 0; doserFxEmitted = 0; }
            if (owner.Equals(researchFxOwner)) { researchFxRemaining = 0; researchFxEmitted = 0; }
            fxPool?.Cancel(owner); fxCues?.Cancel(owner);
        }
        void ResetResearchAutomationFx()
        {
            CancelResearchAutomationOwner(collectorFxOwner); CancelResearchAutomationOwner(doserFxOwner); CancelResearchAutomationOwner(researchFxOwner);
        }
        void TickResearchAutomationFx(float dt)
        {
            if (!AdvancedCommandVisible) { ResetResearchAutomationFx(); return; }
            CancelMovedModuleBench();
            collectorFxRemaining = Mathf.Max(0, collectorFxRemaining - dt);
            doserFxRemaining = Mathf.Max(0, doserFxRemaining - dt);
            researchFxRemaining = Mathf.Max(0, researchFxRemaining - dt);
            EmitResearchAutomationParticles();
        }
        void CancelMovedModuleBench()
        {
            if (collectorFxRemaining <= 0 && doserFxRemaining <= 0) return;
            if (!upgradeRoot || !upgradeRoot.gameObject.activeInHierarchy ||
                (upgradeRoot.position - moduleBenchPosition).sqrMagnitude > .000001f ||
                Quaternion.Angle(upgradeRoot.rotation, moduleBenchRotation) > .01f ||
                (upgradeRoot.lossyScale - moduleBenchScale).sqrMagnitude > .000001f) {
                CancelResearchAutomationOwner(collectorFxOwner); CancelResearchAutomationOwner(doserFxOwner);
            }
        }
        void EmitResearchAutomationParticles()
        {
            EmitModuleParticles(false); EmitModuleParticles(true);
            if (researchFxRemaining <= 0 || fxPool == null || !fxCues.IsValid(researchFxOwner)) return;
            int count = FxResearchAutomationMotion.Count(LabDoseLow, fxReducedMotion);
            for (int i = 0; i < count; i++) {
                ushort bit = (ushort)(1 << i);
                if ((researchFxEmitted & bit) != 0 || ResearchFxAge < FxResearchAutomationMotion.Delay(i, researchFxSeed)) continue;
                researchFxEmitted |= bit;
                FxResearchAutomationMotion.SampleResearch(i, researchFxSeed, fxReducedMotion, out var local, out var request);
                request.Lifetime = Mathf.Min(request.Lifetime, researchFxRemaining);
                fxPool.EmitParticle(fxCues, researchFxOwner, researchFxRoot, researchFxLocal + local, request);
            }
        }
        void EmitModuleParticles(bool doser)
        {
            float remaining = doser ? doserFxRemaining : collectorFxRemaining;
            var owner = doser ? doserFxOwner : collectorFxOwner;
            if (remaining <= 0 || fxPool == null || !fxCues.IsValid(owner)) return;
            uint seed = doser ? doserFxSeed : collectorFxSeed;
            int count = FxResearchAutomationMotion.Count(LabDoseLow, fxReducedMotion);
            float age = FxResearchAutomationMotion.Duration - remaining;
            for (int i = 0; i < count; i++) {
                ushort bit = (ushort)(1 << i);
                if (((doser ? doserFxEmitted : collectorFxEmitted) & bit) != 0 || age < FxResearchAutomationMotion.Delay(i, seed)) continue;
                if (doser) doserFxEmitted |= bit; else collectorFxEmitted |= bit;
                var culture = doser ? doserFxMix.At(i) : collectorFxCulture;
                FxResearchAutomationMotion.SampleTransfer(culture, i, seed, doser, fxReducedMotion, out var local, out var request);
                request.Lifetime = Mathf.Min(request.Lifetime, remaining);
                fxPool.EmitParticle(fxCues, owner, doser ? doserFxRoot : collectorFxRoot,
                    (doser ? FxResearchAutomationMotion.DoserInlet : collectorFxLocal) + local, request);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int CollectorCultureCountForValidation(CultureKind culture) => collectorCultureCounts[(int)culture];
        public int DoserCultureCountForValidation(CultureKind culture) => doserCultureCounts[(int)culture];
        public FxResearchAutomationSnapshot ResearchAutomationForValidation => new FxResearchAutomationSnapshot {
            CollectorState = labModuleState, DoserState = labDoserState, RequestId = labModuleRequest,
            CollectorOwner = collectorFxOwner, DoserOwner = doserFxOwner, ResearchOwner = researchFxOwner,
            CollectorRoot = collectorFxRoot, DoserRoot = doserFxRoot, ResearchRoot = researchFxRoot,
            CollectorLocal = collectorFxLocal, DoserLocal = FxResearchAutomationMotion.DoserInlet, ResearchLocal = researchFxLocal,
            CollectorPoint = collectorFxRoot ? collectorFxRoot.TransformPoint(collectorFxLocal) : Vector3.zero,
            DoserPoint = doserFxRoot ? doserFxRoot.TransformPoint(FxResearchAutomationMotion.DoserInlet) : Vector3.zero,
            ResearchPoint = researchFxRoot ? researchFxRoot.TransformPoint(researchFxLocal) : Vector3.zero,
            CollectorEnd = collectorFxLocal + new Vector3(0, .17f, -.07f), DoserEnd = FxResearchAutomationMotion.DoserInlet + new Vector3(-.38f, -.365f, -.015f),
            CollectorCount = LabModuleCueCount, DoserCount = doserFxCount, ResearchCount = researchFxCount,
            CollectorParticles = fxPool == null ? 0 : fxPool.ParticleCountForValidation(collectorFxOwner),
            DoserParticles = fxPool == null ? 0 : fxPool.ParticleCountForValidation(doserFxOwner),
            ResearchParticles = fxPool == null ? 0 : fxPool.ParticleCountForValidation(researchFxOwner),
            LastResearch = lastResearchUpgrade, LastCulture = collectorFxCulture
        };
#endif
    }
}
