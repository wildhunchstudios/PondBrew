using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Transient player/employee pocket door. The session owns its clock and pause policy.</summary>
    public sealed class ShopRoomDoor : MonoBehaviour
    {
        public float OpenFraction { get; private set; }
        public bool WantsOpen { get; private set; }
        Transform left, right;
        float clearDelay;

        public void Initialize(Transform leftLeaf, Transform rightLeaf)
        {
            left = leftLeaf; right = rightLeaf;
            OpenFraction = 0; WantsOpen = false; clearDelay = 0;
            ApplyPose();
        }

        public void Tick(Vector3 playerPosition, float dt) => Tick(playerPosition, dt, null);

        public void Tick(Vector3 playerPosition, float dt, Vector3? workerPosition, Vector3? secondWorkerPosition = null)
        {
            if (!left || !right || dt <= 0) return;
            Vector3 local = transform.InverseTransformPoint(playerPosition);
            // Includes capsule radius, side approaches and a full movement frame of clearance.
            bool occupied = NearSensor(local) || workerPosition.HasValue && NearSensor(transform.InverseTransformPoint(workerPosition.Value)) ||
                secondWorkerPosition.HasValue && NearSensor(transform.InverseTransformPoint(secondWorkerPosition.Value));
            clearDelay = occupied ? 1f : Mathf.Max(0, clearDelay - dt);
            WantsOpen = occupied || clearDelay > 0;
            float previous = OpenFraction;
            OpenFraction = Mathf.MoveTowards(previous, WantsOpen ? 1 : 0, dt * 2.8f);
            if (previous == OpenFraction) return;
            ApplyPose();
            // Moving solid colliders must be current before this frame's focus/movement queries.
            Physics.SyncTransforms();
        }

        static bool NearSensor(Vector3 local)
            => Mathf.Abs(local.x) < 2.05f && Mathf.Abs(local.z) < 2.25f && local.y > -1 && local.y < 3;

        void ApplyPose()
        {
            float travel = Mathf.SmoothStep(0, 1, OpenFraction) * 1.66f;
            left.localPosition = new Vector3(-.80f - travel, 0, 0);
            right.localPosition = new Vector3(.80f + travel, 0, 0);
        }
    }
}
