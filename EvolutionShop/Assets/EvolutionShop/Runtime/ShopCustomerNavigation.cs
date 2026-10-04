using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace EvolutionShop
{
    /// <summary>Owns the runtime store bake. Customer decisions and placement transactions live elsewhere.</summary>
    public sealed class ShopCustomerNavigation : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public int BuildCount { get; private set; }
        public string LastError { get; private set; } = "Navigation has not been built.";
        public int AgentTypeId => surface ? surface.agentTypeID : 0;
        public Vector3 EntryPoint => WorldPoint(new Vector3(0, 0, -4.8f));
        public Vector3 ExitPoint => EntryPoint;
        public Vector3 QueuePoint => QueueSpot(0);
        /// <summary>Places in the counter queue: one per visitor allowed in the shop.</summary>
        public const int QueueCapacity = 2;
        /// <summary>Visitors that can look at one display side by side.</summary>
        public const int BrowseSpotsPerDisplay = 2;

        static readonly Vector3[] queueSpots = { new Vector3(-3.8f, 0, -2.8f), new Vector3(-2.85f, 0, -3.55f) };

        ShopWorld world;
        NavMeshSurface surface;
        NavMeshData ownedData;
        GameObject labExclusion;
        NavMeshPath validationPath;

        Vector3 WorldPoint(Vector3 point) => world ? world.transform.TransformPoint(point) : point;
        NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };

        public Vector3 BrowsePoint(string tankId) => BrowseSpot(tankId, 0);

        /// <summary>Spot 0 faces the display; spot 1 stands beside it, toward the middle of the store.</summary>
        public Vector3 BrowseSpot(string tankId, int spot)
        {
            if (world && world.AppliedLayout.TryGetBrowsePoint(tankId, Mathf.Clamp(spot, 0, BrowseSpotsPerDisplay - 1), out float x, out float z))
                return WorldPoint(new Vector3(x, 0, z));
            return new Vector3(float.NaN, 0, float.NaN);
        }

        /// <summary>Spot 0 is at the counter; later spots wait behind it.</summary>
        public Vector3 QueueSpot(int spot) => WorldPoint(queueSpots[Mathf.Clamp(spot, 0, queueSpots.Length - 1)]);

        /// <summary>Initial bake, after ShopWorld.Build and two rendered frames of collider cleanup.</summary>
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
            blocked.size = new Vector3(40, 8, 7); // Starts at z = .05, including the lab and the full expanded width.
            blocked.area = NavMesh.GetAreaFromName("Not Walkable");
            if (blocked.area < 0)
            {
                LastError = "The Not Walkable navigation area is unavailable.";
                ReleaseOwnedData();
                return false;
            }
            return BakeAndValidate();
        }

        /// <summary>
        /// Re-bakes an applied candidate with no customers present. On rejection the last valid nav data is
        /// restored; the caller must also restore the previous world layout before resuming the shop.
        /// </summary>
        public bool Rebuild(ShopWorld shopWorld)
        {
            if (BuildCount == 0) return Build(shopWorld);
            if (!shopWorld || world != shopWorld || !surface || !labExclusion)
            {
                LastError = "Navigation can only rebuild its existing shop world.";
                return false;
            }
            BuildCount++;
            return BakeAndValidate();
        }

        bool BakeAndValidate()
        {
            NavMeshData previous = ownedData;
            bool previousReady = Ready;
            Ready = false;
            try
            {
                Physics.SyncTransforms();
                surface.RemoveData();
                // BuildNavMesh leaves navMeshData unchanged if no replacement is produced. Clearing it prevents
                // a failed bake from being mistaken for a successful validation of the old furniture layout.
                surface.navMeshData = null;
                surface.BuildNavMesh();
                ownedData = surface.navMeshData;
                if (!ownedData) return RejectCandidate(previous, previousReady, "The store navigation bake produced no data.");
                Ready = true;
                if (!ValidateRequiredRoutes(out string error)) return RejectCandidate(previous, previousReady, error);
                if (previous && previous != ownedData) Destroy(previous);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return RejectCandidate(previous, previousReady, "The store navigation bake failed: " + exception.Message);
            }
        }

        bool ValidateRequiredRoutes(out string error)
        {
            foreach (string tank in world.AppliedLayout.TankIds)
                for (int spot = 0; spot < BrowseSpotsPerDisplay; spot++)
                    for (int place = 0; place < QueueCapacity; place++)
                        if (!CanReach(EntryPoint, BrowseSpot(tank, spot)) || !CanReach(BrowseSpot(tank, spot), QueueSpot(place)))
                        {
                            error = "A required customer route is not completely walkable: " + tank + " spot " + spot + " to queue place " + place + ".";
                            return false;
                        }
            for (int place = 0; place < QueueCapacity; place++)
                if (!CanReach(QueueSpot(place), ExitPoint))
                {
                    error = "Queue place " + place + " has no walkable exit.";
                    return false;
                }
            for (int tier = 1; tier <= world.AppliedLayout.StoreTier; tier++)
                if (!CanReach(EntryPoint, WorldPoint(new Vector3(-5 - tier * 4, 0, -2.5f))))
                {
                    error = "Store gallery " + tier + " has no complete route from the entrance.";
                    return false;
                }
            // Probe the bake itself, independently of destination validation, to catch a missing modifier.
            if (HasNavigationAt(WorldPoint(new Vector3(0, 0, 1)), .1f) ||
                HasNavigationAt(WorldPoint(new Vector3(0, 0, 3)), .1f))
            {
                error = "Customer navigation unexpectedly extends into the lab.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        bool RejectCandidate(NavMeshData previous, bool previousReady, string message)
        {
            NavMeshData rejected = surface ? surface.navMeshData : ownedData;
            if (surface)
            {
                surface.RemoveData();
                surface.navMeshData = previous;
                if (previous) surface.AddData();
            }
            if (rejected && rejected != previous) Destroy(rejected);
            ownedData = previous;
            Ready = previous && previousReady;
            LastError = message;
            return false;
        }

        public bool IsInStore(Vector3 position)
        {
            if (!world || !Finite(position)) return false;
            Vector3 p = world.transform.InverseTransformPoint(position);
            return p.x >= world.AppliedLayout.StoreMinX + .45f && p.x <= 6.55f && p.z >= -5.2f && p.z < .05f && p.y >= -.2f && p.y <= .45f;
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
            if (surface)
            {
                surface.RemoveData();
                surface.navMeshData = null;
                surface.enabled = false;
                Destroy(surface);
                surface = null;
            }
            if (ownedData) { Destroy(ownedData); ownedData = null; }
            if (labExclusion) { labExclusion.SetActive(false); Destroy(labExclusion); labExclusion = null; }
        }

        void OnDestroy() { ReleaseOwnedData(); }
    }
}
