using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        readonly bool[] growerKnown = new bool[2], growerPathClear = { true, true };
        readonly int[] growerPreviousUnits = new int[2], growerCirculationCounts = new int[2], growerRefillCounts = new int[2],
            growerAcceptedCounts = new int[2], growerBlockedCounts = new int[2];
        readonly int[] growerCirculationRequests = new int[2], growerAccentRequests = new int[2],
            growerRefillParticles = new int[2], growerAcceptedParticles = new int[2];
        readonly FxOwnerToken[] growerObservedOwners = new FxOwnerToken[2];
        readonly FxGrowerInteraction[] growerInteractions = new FxGrowerInteraction[2];
        readonly float[] growerResponseRemaining = new float[2];
        readonly int[] growerStatusUnits = { -1, -1 }, growerStatusCapacity = { -1, -1 };
        readonly FxGrowerInteraction[] growerStatusInteractions = new FxGrowerInteraction[2];
        int growerAttempt = -1;
        bool growerAttemptSaveBlocked, growerInputHeld;

        Transform GrowerRoot(int i) => i == 0 ? bloomRoot : flowRoot;
        FxOwnerToken GrowerOwner(int i) => fxCues == null ? default : fxCues.Find(i == 0 ? "bloom" : "flow");
        static bool GrowerCulture(CultureKind culture) => culture == CultureKind.Bloom || culture == CultureKind.Flow;

        // Observe the existing input/result without changing collection cadence, stock or save decisions.
        public void SetGrowerCollectionIntent(bool held, Station station, bool saveFailed)
        {
            growerInputHeld = held;
            int next = held && fxPolicy == FxViewPolicy.Visible && TryHarvestCulture(station, out var culture) && GrowerCulture(culture) ? (int)culture : -1;
            if (next != growerAttempt) {
                if (growerAttempt >= 0) growerPathClear[growerAttempt] = true;
                if (next >= 0) growerPathClear[next] = true;
            }
            // An unrelated autosave error is not a refused collection. The existing collection failure owner decides.
            growerAttempt = next; growerAttemptSaveBlocked = held && ((saveFailed && collectionSaveBlocked) || growerAttemptSaveBlocked);
            UpdateGrowerInteractions();
        }

        void UpdateGrowerInteractions()
        {
            if (lastModel == null) return;
            for (int i = 0; i < 2; i++) {
                bool attempted = growerAttempt == i && fxEnabled && fxPolicy == FxViewPolicy.Visible;
                var next = FxGrowerMotion.ReadInteraction(ReadGrowthGrower(i), attempted,
                    CollectionActive && harvestingCulture == (CultureKind)i,
                    lastModel.ReservedCultureCount >= lastModel.Capacity,
                    growerAttemptSaveBlocked || collectionSaveBlocked, growerPathClear[i]);
                if (next != growerInteractions[i] && (int)next >= (int)FxGrowerInteraction.ReservoirFull) growerBlockedCounts[i]++;
                growerInteractions[i] = next;
                UpdateGrowerStatus(i);
            }
        }

        void ObserveGrowerPath(bool clear)
        {
            if (!GrowerCulture(harvestingCulture)) return;
            growerPathClear[(int)harvestingCulture] = clear;
            UpdateGrowerInteractions();
        }

        void UpdateGrowerStatus(int i)
        {
            if (lastModel == null) return;
            var stock = lastModel.GetGrower((CultureKind)i);
            var interaction = growerInteractions[i];
            if (growerStatusUnits[i] == stock.Units && growerStatusCapacity[i] == stock.Capacity && growerStatusInteractions[i] == interaction) return;
            growerStatusUnits[i] = stock.Units; growerStatusCapacity[i] = stock.Capacity; growerStatusInteractions[i] = interaction;
            string suffix = interaction == FxGrowerInteraction.ReservoirFull ? "RESERVOIR FULL" :
                interaction == FxGrowerInteraction.SaveBlocked ? "RELEASE / RETRY" : interaction == FxGrowerInteraction.PathObstructed ? "INTAKE BLOCKED" :
                interaction == FxGrowerInteraction.Collecting ? "COLLECTING" : stock.Units <= 0 ? "EMPTY / REFILLING" :
                stock.Units >= stock.Capacity ? "FULL" : "REFILLING";
            var kind = i == 0 ? StationKind.Bloom : StationKind.Flow;
            if (status.TryGetValue(kind, out var label) && label) label.text = stock.Units + " / " + stock.Capacity + " | " + suffix;
        }

        void ReconcileGrowerPresentation(int i, FxGrowerPose pose)
        {
            var stock = lastModel.GetGrower((CultureKind)i); var owner = GrowerOwner(i);
            bool sameOwner = growerKnown[i] && growerObservedOwners[i].Equals(owner) && fxCues != null && fxCues.IsValid(owner);
            bool refill = sameOwner && stock.Units > growerPreviousUnits[i];
            // A falling real fluid surface invalidates trajectories sampled at the former stock level.
            // The new accepted-unit response is emitted later, only by the saved-success callback.
            if (sameOwner && stock.Units < growerPreviousUnits[i]) fxPool?.Cancel(owner);
            growerPreviousUnits[i] = stock.Units; growerObservedOwners[i] = owner; growerKnown[i] = true;
            if (refill && fxEnabled && fxPolicy == FxViewPolicy.Visible && GrowerRoot(i) && GrowerRoot(i).gameObject.activeInHierarchy) {
                growerRefillCounts[i]++; growerResponseRemaining[i] = FxGrowerMotion.ResponseDuration;
                EmitGrowerAccent(i, pose, false);
            }
            UpdateGrowerStatus(i);
        }

        void AcceptGrowerCollection(CultureKind culture)
        {
            if (!GrowerCulture(culture)) return;
            int i = (int)culture;
            growerAcceptedCounts[i]++; growerResponseRemaining[i] = FxGrowerMotion.ResponseDuration;
            EmitGrowerAccent(i, ReadGrowthGrower(i), true);
        }

        void EmitGrowerAccent(int i, FxGrowerPose pose, bool accepted)
        {
            if (fxPool == null || fxCues == null || !fxEnabled || fxPolicy != FxViewPolicy.Visible) return;
            int count = LabDoseLow || fxReducedMotion ? 1 : 2;
            for (int j = 0; j < count; j++) {
                FxGrowerMotion.SampleAccent((CultureKind)i, ++growthBubbleSequences[i], pose, accepted, fxReducedMotion, out var point, out var request);
                // Natural refills remain ambient; committed collection uses the existing action reservation.
                growerAccentRequests[i]++;
                if (fxPool.EmitParticle(fxCues, GrowerOwner(i), GrowerRoot(i), point, request, accepted)) {
                    if (accepted) growerAcceptedParticles[i]++;
                    else { growerRefillParticles[i]++; GrowthAmbientEmissionCount++; }
                }
            }
        }

        void TickGrowerPresentation(int i, FxGrowerPose pose, float time, float dt)
        {
            growerResponseRemaining[i] = Mathf.Max(0, growerResponseRemaining[i] - Mathf.Max(0, dt));
            if (labNeedles[i]) labNeedles[i].localRotation = Quaternion.Euler(0, 0,
                FxGrowerMotion.Needle(pose, growerInteractions[i], growerResponseRemaining[i], time + i * .47f, fxReducedMotion));
            if (dt <= 0 || fxPolicy != FxViewPolicy.Visible || pose.State == FxGrowerVisualState.Full || fxPool == null) return;
            var owner = GrowerOwner(i); var root = GrowerRoot(i);
            if (fxCues == null || !fxCues.IsValid(owner) || !root || !root.gameObject.activeInHierarchy) return;
            // Keep the existing Low two-emitter sharing policy; the incubator retains first admission.
            if (LabDoseLow && LabGrowthActive && (int)(time / 2f) % 2 != i) return;
            growthBubbleClocks[i] -= dt;
            if (growthBubbleClocks[i] > 0) return;
            if (!FxGrowerMotion.SampleCirculation((CultureKind)i, ++growthBubbleSequences[i], pose, LabDoseLow, fxReducedMotion,
                out float interval, out var point, out var request)) return;
            growthBubbleClocks[i] = interval; growerCirculationRequests[i]++;
            if (fxPool.EmitParticle(fxCues, owner, root, point, request, false)) { growerCirculationCounts[i]++; GrowthAmbientEmissionCount++; }
        }

        void AdoptGrowerModel()
        {
            // ResetSessionFx can reconcile the old model before ReloadSaved supplies its replacement.
            // Clear observation again at the actual adoption boundary, without touching either model.
            for (int i = 0; i < 2; i++) {
                fxPool?.Cancel(GrowerOwner(i)); growerKnown[i] = false;
                growerResponseRemaining[i] = 0; growerStatusUnits[i] = -1;
            }
        }

        void ResetGrowerPresentation()
        {
            growerAttempt = -1; growerAttemptSaveBlocked = false;
            for (int i = 0; i < 2; i++) {
                growerKnown[i] = false; growerPathClear[i] = true; growerResponseRemaining[i] = 0;
                growerInteractions[i] = FxGrowerInteraction.Idle;
                growerStatusUnits[i] = -1; UpdateGrowerStatus(i);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FxGrowerInteraction GrowerInteractionForValidation(CultureKind culture) => GrowerCulture(culture) ? growerInteractions[(int)culture] : FxGrowerInteraction.Idle;
        public int GrowerCirculationCountForValidation(CultureKind culture) => GrowerCulture(culture) ? growerCirculationCounts[(int)culture] : 0;
        public int GrowerCirculationRequestsForValidation(CultureKind culture) => GrowerCulture(culture) ? growerCirculationRequests[(int)culture] : 0;
        public int GrowerAccentRequestsForValidation(CultureKind culture) => GrowerCulture(culture) ? growerAccentRequests[(int)culture] : 0;
        public int GrowerRefillParticlesForValidation(CultureKind culture) => GrowerCulture(culture) ? growerRefillParticles[(int)culture] : 0;
        public int GrowerAcceptedParticlesForValidation(CultureKind culture) => GrowerCulture(culture) ? growerAcceptedParticles[(int)culture] : 0;
        public int GrowerRefillCountForValidation(CultureKind culture) => GrowerCulture(culture) ? growerRefillCounts[(int)culture] : 0;
        public int GrowerAcceptedCountForValidation(CultureKind culture) => GrowerCulture(culture) ? growerAcceptedCounts[(int)culture] : 0;
        public int GrowerBlockedTransitionCountForValidation(CultureKind culture) => GrowerCulture(culture) ? growerBlockedCounts[(int)culture] : 0;
        public int GrowerActiveCellsForValidation(CultureKind culture) {
            if (!GrowerCulture(culture)) return 0;
            int count = 0; var cells = culture == CultureKind.Bloom ? bloomCells : flowCells;
            for (int i = 0; i < cells.Count; i++) if (cells[i] && cells[i].gameObject.activeInHierarchy) count++;
            return count;
        }
        public Transform GrowerRootForValidation(CultureKind culture) => GrowerCulture(culture) ? GrowerRoot((int)culture) : null;
        public Transform GrowerNeedleForValidation(CultureKind culture) => GrowerCulture(culture) ? labNeedles[(int)culture] : null;
        public FxOwnerToken GrowerOwnerForValidation(CultureKind culture) => GrowerCulture(culture) ? GrowerOwner((int)culture) : default;
#endif
    }
}
