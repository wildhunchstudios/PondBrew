using UnityEngine;

namespace EvolutionShop
{
    public enum FxPlayerInteraction { None, Interact, Place, Rack, Transfer, Refused }

    public sealed partial class ShopWorld
    {
        FxOwnerToken playerInteractionOwner;
        Transform playerInteractionFrame;
        Vector3 playerInteractionLocal;
        string playerInteractionDestination, playerInteractionIdentity;
        public int PlayerInteractionCount { get; private set; }
        public int PlayerRefusalCount { get; private set; }
        public FxPlayerInteraction LastPlayerInteraction { get; private set; }
        public string PlayerInteractionSource { get; private set; }
        public string PlayerInteractionDestination { get; private set; }
        public string PlayerInteractionSpecimenId { get; private set; }
        public float PlayerInteractionRemaining { get; private set; }

        static string PlayerStationId(Station station) => station.TankId ?? station.StorageItemId ??
            station.ConditionerItemId ?? station.FacilityId ?? station.Kind.ToString();

        /// <summary>One local acknowledgment of an already accepted command, never a carried specimen.</summary>
        public bool PresentPlayerInteraction(FxPlayerInteraction kind, Station station, string destinationId, uint sequence)
        {
            if (!fxEnabled || fxCues == null || !isActiveAndEnabled || !station || !station.gameObject.activeInHierarchy ||
                (fxPolicy != FxViewPolicy.Visible && fxPolicy != FxViewPolicy.Modal) || kind <= FxPlayerInteraction.None || kind > FxPlayerInteraction.Refused) return false;
            bool belongs = false;
            for (int i = 0; i < stations.Count; i++) if (stations[i] == station) { belongs = true; break; }
            if (!belongs) return false;

            bool placement = kind == FxPlayerInteraction.Place || kind == FxPlayerInteraction.Rack || kind == FxPlayerInteraction.Transfer;
            Display destination = null, localDisplay = null;
            if (placement && (string.IsNullOrEmpty(destinationId) || !displays.TryGetValue(destinationId, out destination) ||
                !destination.Root || !destination.Creature || string.IsNullOrEmpty(destination.FxSpecimenId))) return false;
            if (placement) {
                if (kind == FxPlayerInteraction.Transfer) {
                    // Return-to-rack is an existing immediate command. A departure accent stays
                    // at the addressed display; no trajectory or hand reaches the remote rack.
                    if (string.IsNullOrEmpty(station.TankId) || !displays.TryGetValue(station.TankId, out localDisplay) || localDisplay.Creature) return false;
                } else {
                    if (!destination.Root.IsChildOf(station.transform)) return false;
                    localDisplay = destination;
                }
            }
            string identity = placement ? destination.FxSpecimenId : null;
            var owner = BindFxOwner("player-interaction", identity, station.transform);
            var cue = kind == FxPlayerInteraction.Refused ? FxCueId.HandRefused : FxCueId.HandPlace;
            float duration = kind == FxPlayerInteraction.Refused ? .18f : placement ? .52f : .24f;
            if (!fxCues.Play(owner, cue, sequence, FxCueOrigin.Manual, duration, kind == FxPlayerInteraction.Refused ? 5 : 4)) return false;
            fxPool?.Cancel(owner);
            playerInteractionOwner = owner;
            playerInteractionDestination = placement ? destinationId : null;
            playerInteractionIdentity = identity;
            PlayerInteractionRemaining = duration;
            PlayerInteractionSource = PlayerStationId(station);
            PlayerInteractionDestination = destinationId;
            PlayerInteractionSpecimenId = identity;
            LastPlayerInteraction = kind;
            PlayerInteractionCount++;
            if (kind == FxPlayerInteraction.Refused) PlayerRefusalCount++;

            if (localDisplay != null) {
                playerInteractionFrame = localDisplay.Root.parent;
                var bounds = localDisplay.Interior;
                playerInteractionLocal = new Vector3(bounds.center.x, bounds.max.y - .12f, bounds.center.z);
            } else {
                playerInteractionFrame = station.transform;
                var box = station.GetComponent<BoxCollider>();
                playerInteractionLocal = box ? box.center + new Vector3(0, box.size.y * .15f, -box.size.z * .5f - .025f) : new Vector3(0, 1.25f, -.5f);
            }
            EmitPlayerInteraction(kind, sequence, placement);
            // Modal input stays immediate and hands stay hidden. No pose is queued for close.
            if (fxPolicy == FxViewPolicy.Visible && placement) AcceptHandPose(FxCueId.HandPlace, .40f);
            else if (fxPolicy == FxViewPolicy.Visible && kind == FxPlayerInteraction.Refused) AcceptHandPose(FxCueId.HandRefused, .18f);
            return true;
        }

        void EmitPlayerInteraction(FxPlayerInteraction kind, uint sequence, bool placement)
        {
            if (fxPool == null) return;
            bool refused = kind == FxPlayerInteraction.Refused, departure = kind == FxPlayerInteraction.Transfer;
            Color color = refused ? new Color(1, .74f, .35f, .8f) : new Color(.56f, .94f, .91f, .8f);
            var ring = FxParticleRequest.Create(FxParticleShape.Ring, color);
            ring.Lifetime = PlayerInteractionRemaining;
            ring.Size = refused ? .075f : placement ? .10f : .065f;
            ring.EndSize = refused || fxReducedMotion ? ring.Size : placement ? .23f : .12f;
            ring.SurfaceAligned = placement;
            fxPool.EmitParticle(fxCues, playerInteractionOwner, playerInteractionFrame, playerInteractionLocal, ring);
            if (!placement || departure || fxReducedMotion) return;
            int count = fxPool.ActionLimit < 64 ? 2 : 5;
            for (int i = 0; i < count; i++) {
                float phase = i * 2.399963f + (sequence % 19) * .37f;
                var offset = new Vector3(Mathf.Cos(phase) * (.038f + i * .007f), -.085f - (i % 3) * .025f, Mathf.Sin(phase) * .055f);
                var bubble = FxParticleRequest.Create(FxParticleShape.Bubble, color);
                bubble.Motion = FxParticleMotion.Curve;
                bubble.End = new Vector3(-offset.x * .35f, .075f + (i % 2) * .02f, -offset.z * .35f);
                bubble.Bend = new Vector3(offset.z * .4f, .01f, -offset.x * .4f);
                bubble.Size = .034f + (i % 3) * .009f; bubble.EndSize = .016f;
                bubble.Lifetime = .32f + (i % 3) * .06f;
                fxPool.EmitParticle(fxCues, playerInteractionOwner, playerInteractionFrame, playerInteractionLocal + offset, bubble);
            }
        }

        public void CancelPlayerInteraction()
        {
            fxCues?.Cancel(playerInteractionOwner); fxPool?.Cancel(playerInteractionOwner);
            playerInteractionOwner = default; playerInteractionFrame = null;
            playerInteractionDestination = playerInteractionIdentity = null; PlayerInteractionRemaining = 0;
        }
        void ReconcilePlayerInteraction()
        {
            if (PlayerInteractionRemaining <= 0 || playerInteractionDestination == null) return;
            if (!displays.TryGetValue(playerInteractionDestination, out var current) || !current.Creature ||
                current.FxSpecimenId != playerInteractionIdentity) CancelPlayerInteraction();
        }
        void TickPlayerInteraction(float dt)
        {
            if (PlayerInteractionRemaining <= 0) return;
            ReconcilePlayerInteraction();
            if (!fxCues.IsValid(playerInteractionOwner) || !playerInteractionFrame || !playerInteractionFrame.gameObject.activeInHierarchy) { CancelPlayerInteraction(); return; }
            PlayerInteractionRemaining = Mathf.Max(0, PlayerInteractionRemaining - Mathf.Max(0, dt));
            if (PlayerInteractionRemaining <= 0) CancelPlayerInteraction();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FxOwnerToken PlayerInteractionOwnerForValidation => playerInteractionOwner;
        public int PlayerInteractionParticleCountForValidation => fxPool == null ? 0 : fxPool.ParticleCountForValidation(playerInteractionOwner);
        public Vector3 PlayerInteractionPointForValidation => playerInteractionFrame ? playerInteractionFrame.TransformPoint(playerInteractionLocal) : Vector3.zero;
#endif
    }
}
