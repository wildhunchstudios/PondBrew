using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        FxOwnerToken collectionOwner;
        float collectionClock, collectionResponseRemaining;
        uint collectionSequence;
        bool collectionSaveBlocked;
        readonly RaycastHit[] collectionObstructions = new RaycastHit[16];
        readonly Collider[] collectionStartOverlaps = new Collider[16];
        public int CollectionEmissionCount { get; private set; }
        public int CollectionAcceptedCount { get; private set; }
        public float CollectionResponse => FxCollectionMotion.Response(collectionResponseRemaining, fxReducedMotion);
        public bool CollectionActive => harvesting && labHarvestSource && labHarvestSource.gameObject.activeInHierarchy &&
            toolBody && toolBody.gameObject.activeInHierarchy && fxEnabled && isActiveAndEnabled &&
            fxPolicy == FxViewPolicy.Visible && !collectionSaveBlocked && lastModel != null &&
            (harvestingCulture == CultureKind.Bloom || harvestingCulture == CultureKind.Flow) &&
            lastModel.ReservedCultureCount < lastModel.Capacity && lastModel.GetGrower(harvestingCulture).Units > 0 &&
            fxCues != null && fxCues.IsValid(collectionOwner);

        void BeginCollectionIntake(Transform source)
        {
            collectionOwner = BindFxOwner("collection-intake", null, source);
            collectionClock = 0;
        }
        void CancelCollectionIntake()
        {
            if (collectionOwner.IsDefined) { fxPool?.Cancel(collectionOwner); fxCues?.Cancel(collectionOwner); }
            collectionOwner = default; collectionClock = 0;
        }
        void ResetCollectionFx()
        {
            CancelCollectionIntake(); collectionResponseRemaining = 0;
        }
        // The model/save command still owns failure. A held pointer cannot restart a canceled
        // successful-looking stream immediately after its write failed.
        public void BlockCollectionUntilRelease() { collectionSaveBlocked = true; ResetCollectionFx(); }

        void AcceptCollectionUnit(FxOwnerToken owner, uint sequence, CultureKind culture)
        {
            collectionResponseRemaining = FxCollectionMotion.ResponseDuration;
            CollectionAcceptedCount++;
            if (!ToolChamber || fxPool == null) return;
            int count = fxReducedMotion ? 1 : fxPool.ActionLimit < 64 ? 2 : 4;
            for (int i = 0; i < count; i++)
            {
                float phase = i * 2.399f + (sequence % 17) * .37f;
                var local = new Vector3(Mathf.Cos(phase)*.046f, -.064f + i*.018f, Mathf.Sin(phase)*.046f);
                var request = FxParticleRequest.Create(FxParticleShape.Bubble,
                    culture == CultureKind.Flow ? new Color(.44f,.9f,1,.9f) : new Color(1,.64f,.74f,.9f));
                request.Motion = FxParticleMotion.Curve; request.Lifetime = .26f+i*.025f;
                request.Size = .044f + (i%2)*.014f; request.EndSize = .018f;
                request.End = new Vector3(-local.x*.5f,.038f,-local.z*.5f);
                request.Bend = new Vector3(-local.z*.5f,.009f,local.x*.5f);
                fxPool.EmitParticle(fxCues, owner, ToolChamber, local, request);
            }
        }
        void TickCollectionFx(float dt)
        {
            collectionResponseRemaining = Mathf.Max(0, collectionResponseRemaining - dt);
            if (!CollectionActive) { CancelCollectionIntake(); return; }
            if (!CollectionPathClear()) { CancelCollectionIntake(); return; }
            collectionClock -= dt;
            if (collectionClock > 0 || fxPool == null) return;
            FxCollectionMotion.Sample(++collectionSequence, harvestingCulture, fxPool.ActionLimit < 64,
                fxReducedMotion, out collectionClock, out var local, out var request);
            // At most one emission after a slow frame; never catch up into a straight bead row.
            if (fxPool.EmitParticle(fxCues, collectionOwner, labHarvestSource, local, request, true, toolBody))
                CollectionEmissionCount++;
        }
        bool CollectionPathClear()
        {
            var start = labHarvestSource.TransformPoint(FxCollectionMotion.OutletLocal);
            var end = toolBody.TransformPoint(FxCollectionMotion.MouthLocal);
            var direction = end - start;
            // The focus ray owns gameplay reach. This narrower local presentation check prevents
            // the curved intake passing through neighbouring furniture while aim remains valid.
            int overlaps = Physics.OverlapSphereNonAlloc(start, FxCollectionMotion.PathClearance,
                collectionStartOverlaps, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps == collectionStartOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++)
                if (CollectionObstruction(collectionStartOverlaps[i].transform)) return false;
            if (direction.sqrMagnitude < .0001f) return true;
            int count = Physics.SphereCastNonAlloc(start, FxCollectionMotion.PathClearance, direction.normalized, collectionObstructions,
                direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) {
                if (CollectionObstruction(collectionObstructions[i].transform)) return false;
            }
            return count < collectionObstructions.Length;
        }
        bool CollectionObstruction(Transform hit) => hit && !hit.IsChildOf(labHarvestSource) &&
            !(ToolRoot && ToolRoot.IsChildOf(hit));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FxOwnerToken CollectionOwnerForValidation => collectionOwner;
        public Vector3 CollectionSourceForValidation => labHarvestSource ? labHarvestSource.TransformPoint(FxCollectionMotion.OutletLocal) : Vector3.zero;
        public Vector3 CollectionMouthForValidation => toolBody ? toolBody.TransformPoint(FxCollectionMotion.MouthLocal) : Vector3.zero;
        public int CollectionParticleCountForValidation => fxPool == null ? 0 : fxPool.ParticleCountForValidation(collectionOwner);
#endif
    }
}
