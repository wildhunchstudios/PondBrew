using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace EvolutionShop
{
    /// <summary>Owns one runtime bake. Customer decisions and transaction state live elsewhere.</summary>
    public sealed class ShopCustomerNavigation : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public int BuildCount { get; private set; }
        public string LastError { get; private set; } = "Navigation has not been built.";
        public int AgentTypeId => surface ? surface.agentTypeID : 0;
        public Vector3 EntryPoint => WorldPoint(new Vector3(0, 0, -4.8f));
        public Vector3 ExitPoint => EntryPoint;
        public Vector3 QueuePoint => WorldPoint(new Vector3(-3.8f, 0, -2.8f));

        ShopWorld world;
        NavMeshSurface surface;
        NavMeshData ownedData;
        GameObject labExclusion;
        NavMeshPath validationPath;

        Vector3 WorldPoint(Vector3 point) => world ? world.transform.TransformPoint(point) : point;
        NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };

        public Vector3 BrowsePoint(string tankId)
        {
            return WorldPoint(new Vector3(3.7f, 0, tankId == "tank-b" ? -3.85f : -1.2f));
        }

        /// <summary>Call once, after ShopWorld.Build and two rendered frames of collider cleanup.</summary>
        public bool Build(ShopWorld shopWorld)
        {
            if (BuildCount != 0) return Ready && world == shopWorld;
            BuildCount++;
            if (!shopWorld) { LastError = "The customer navigation world is missing."; return false; }
            world = shopWorld;
            // NavMeshPath allocates native state and must not run in a MonoBehaviour field initializer.
            validationPath = new NavMeshPath();
            // Only the world hierarchy contributes geometry; the player, camera tool, and actors do not.
            surface = world.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.overrideVoxelSize = true;
            surface.voxelSize = .08f;

            labExclusion = new GameObject("Customer navigation / lab is not walkable");
            labExclusion.transform.SetParent(world.transform, false);
            var blocked = labExclusion.AddComponent<NavMeshModifierVolume>();
            blocked.center = new Vector3(0, 2, 3.55f);
            blocked.size = new Vector3(18, 8, 7); // Starts at z = .05, including the entire doorway/lab.
            blocked.area = NavMesh.GetAreaFromName("Not Walkable");
            if (blocked.area < 0) return FailBuild("The Not Walkable navigation area is unavailable.");

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            ownedData = surface.navMeshData;
            if (!ownedData) return FailBuild("The store navigation bake produced no data.");
            Ready = true;
            if (!CanReach(EntryPoint, BrowsePoint("tank-a")) || !CanReach(EntryPoint, BrowsePoint("tank-b")) ||
                !CanReach(BrowsePoint("tank-a"), QueuePoint) || !CanReach(QueuePoint, ExitPoint))
                return FailBuild("A required customer route is not completely walkable.");
            // Probe the bake itself, independently of destination validation, to catch a missing modifier.
            if (HasNavigationAt(WorldPoint(new Vector3(0, 0, 1)), .1f) ||
                HasNavigationAt(WorldPoint(new Vector3(0, 0, 3)), .1f))
                return FailBuild("Customer navigation unexpectedly extends into the lab.");
            LastError = string.Empty;
            return true;
        }

        bool FailBuild(string message)
        {
            LastError = message;
            ReleaseOwnedData();
            return false;
        }

        public bool IsInStore(Vector3 position)
        {
            if (!world || !Finite(position)) return false;
            Vector3 p = world.transform.InverseTransformPoint(position);
            return p.x >= -6.55f && p.x <= 6.55f && p.z >= -5.2f && p.z < .05f && p.y >= -.2f && p.y <= .45f;
        }

        public bool TryResolveDestination(Vector3 desired, out Vector3 position)
        {
            position = desired;
            if (!Ready || !IsInStore(desired) || !NavMesh.SamplePosition(desired, out var hit, .32f, Filter) ||
                !IsInStore(hit.position)) return false;
            position = hit.position;
            return true;
        }

        public bool CanReach(Vector3 from, Vector3 to)
        {
            return TryGetPath(from, to, validationPath);
        }

        public bool TryGetPath(Vector3 from, Vector3 to, NavMeshPath path)
        {
            if (path == null || !TryResolveDestination(from, out var start) ||
                !TryResolveDestination(to, out var end) || !NavMesh.CalculatePath(start, end, Filter, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            foreach (var corner in path.corners) if (!IsInStore(corner)) return false;
            return true;
        }

        /// <summary>Raw bake probe for validation. Deliberately does not apply the store target filter.</summary>
        public bool HasNavigationAt(Vector3 position, float distance = .1f)
        {
            return Ready && Finite(position) && NavMesh.SamplePosition(position, out _, Mathf.Clamp(distance, .01f, .5f), Filter);
        }

        static bool Finite(Vector3 p)
        {
            return !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) &&
                !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        }

        void ReleaseOwnedData()
        {
            Ready = false;
            if (surface) { surface.RemoveData(); Destroy(surface); surface = null; }
            if (ownedData) { Destroy(ownedData); ownedData = null; }
            if (labExclusion) { Destroy(labExclusion); labExclusion = null; }
        }

        void OnDestroy() { ReleaseOwnedData(); }
    }
}
