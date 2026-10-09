using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// One visible rack-to-display employee. Claims reserve the exact source and destination;
    /// the session alone commits and saves ownership after this actor reaches the display.
    /// </summary>
    public sealed class ShopStocker : MonoBehaviour
    {
        public const float MoveSpeed = 2.1f, ServiceSeconds = 1f, RetrySeconds = 2f;
        const float BodyRadius = .30f, LowerCenter = .34f, UpperCenter = 1.43f;
        enum WorkPhase { None, ToRack, Pickup, ToDisplay, Deliver }
        public string CurrentJobId { get; private set; }
        public string SourceStorageId { get; private set; }
        public string TargetTankId { get; private set; }
        public string Status => shop && shop.Paused ? "Game paused / stocker time frozen" : status;
        public ShopCharacterVisual Actor { get; private set; }
        public bool Blocked { get; private set; }
        public bool Carrying => phase == WorkPhase.ToDisplay || phase == WorkPhase.Deliver;
        public string Phase => phase == WorkPhase.ToRack ? "Walking to rack" : phase == WorkPhase.Pickup ? "Picking up" :
            phase == WorkPhase.ToDisplay ? "Carrying to display" : phase == WorkPhase.Deliver ? "Placing into display" : "Idle";
        public bool ReadyToDeliver => phase == WorkPhase.Deliver && serviceTime >= ServiceSeconds &&
            shop && !shop.Paused && isActiveAndEnabled && Actor && Actor.isActiveAndEnabled && ValidClaim();
        public float TravelDistance { get; private set; }
        public int CompletedJobs { get; private set; }
        // Even a hired worker paused in the threshold must not have solid leaves close through them.
        public Vector3? ActiveDoorPosition => isActiveAndEnabled && Actor && Actor.isActiveAndEnabled
            ? Actor.transform.position : (Vector3?)null;

        ShopSession shop;
        GameModel claimedModel;
        ShopLayout claimedLayout;
        ShopStocking claimedStocking;
        string expectedSpecimenId, targetSetting, status = "No stocker hired";
        WorkPhase phase;
        WorkerPathPoint[] route, deliveryRoute;
        int routeIndex;
        long nextJobNumber;
        float serviceTime, cooldown, poseTime, doorWait;
        bool shown;
        readonly RaycastHit[] sweepHits = new RaycastHit[32];
        readonly Collider[] overlapHits = new Collider[32];
        Material nameMaterial;
        Font font;
        TextMesh nameCaption;

        public void Initialize(ShopSession session)
        {
            if (shop || !session) return;
            shop = session;
            BuildActor();
            RefreshVisual();
        }

        /// <summary>Explicit employment/reload refresh may restore a missing visual; Tick never respawns one.</summary>
        public void RefreshVisual()
        {
            ResetTransient();
            bool hired = shop && shop.Stocking != null && shop.Stocking.Employee.Hired;
            if (hired && !Actor) BuildActor();
            shown = hired;
            if (Actor)
            {
                Actor.gameObject.SetActive(isActiveAndEnabled && hired);
                Actor.Pose(CharacterSamplePose.Idle, 0);
                if (hired) RecoverInvalidStandingPosition();
            }
            if (Available() && !Blocked) status = "On duty / checking configured displays";
        }

        /// <summary>Cancellation releases both claims while the original specimen remains in the rack.</summary>
        public void ResetTransient()
        {
            ClearJob();
            Blocked = false;
            cooldown = 0;
            poseTime = 0;
            status = "Ready to check configured displays";
        }

        public void Retry()
        {
            ResetTransient();
            if (Available()) status = "Retrying the stock route";
        }

        public void Tick(float seconds)
        {
            if (!shop || shop.Paused || !isActiveAndEnabled || seconds <= 0 ||
                float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            bool hired = shop.Stocking != null && shop.Stocking.Employee.Hired;
            if (shown != hired) RefreshVisual();
            if (!Available() || Blocked) return;
            if (CurrentJobId != null && !ValidClaim())
            {
                ClearJob();
                cooldown = RetrySeconds;
                status = "Assignment changed / stock is safe in its rack";
                return;
            }
            if (CurrentJobId == null)
            {
                Animate(CharacterSamplePose.Idle, seconds);
                cooldown = Mathf.Max(0, cooldown - seconds);
                if (cooldown > 0) return;
                cooldown = RetrySeconds;
                AcquireJob();
                return;
            }

            switch (phase)
            {
                case WorkPhase.ToRack:
                case WorkPhase.ToDisplay:
                    Travel(seconds);
                    break;
                case WorkPhase.Pickup:
                    Animate(CharacterSamplePose.Idle, seconds);
                    serviceTime = Mathf.Min(ServiceSeconds, serviceTime + seconds);
                    if (serviceTime < ServiceSeconds) return;
                    route = deliveryRoute;
                    routeIndex = 0;
                    phase = WorkPhase.ToDisplay;
                    serviceTime = 0;
                    status = "Carrying a reserved creature to " + TargetTankId;
                    break;
                case WorkPhase.Deliver:
                    // The parcel is presentation only. It never owns a second copy of the creature.
                    Actor.Pose(CharacterSamplePose.Carry, 0);
                    serviceTime = Mathf.Min(ServiceSeconds, serviceTime + seconds);
                    if (!ReadyToDeliver) return;
                    bool completed = shop.CompleteStockerJob(CurrentJobId);
                    if (!completed)
                    {
                        Fail("Delivery could not be saved / stock is safe in its rack; retry from Staff");
                        return;
                    }
                    CompletedJobs++;
                    ClearJob();
                    cooldown = RetrySeconds;
                    status = "Display replenished / checking the next target";
                    shop.QueueWorkerCelebration(ShopStaff.StockerId);
                    break;
            }
        }

        bool Available()
        {
            if (!shop || shop.Stocking == null) return Stop("Stocker details unavailable");
            var employee = shop.Stocking.Employee;
            if (!isActiveAndEnabled) return Stop("Stocker disabled");
            if (!employee.Hired) return Stop("No stocker hired");
            if (!Actor || !Actor.isActiveAndEnabled)
            {
                Fail("Stocker unavailable / refresh the roster or reload to restore Nori");
                return false;
            }
            if (!employee.Scheduled) return Stop("Off duty / not scheduled");
            if (employee.Paused) return Stop("Stocker paused / manual restocking is available");
            if (!employee.IsOnDuty(shop.Cycle.DayNumber)) return Stop("Off duty / pay the duty period at night");
            if (shop.PlacementActive || shop.Panel == ShopPanel.Arrange)
                return Stop("Room editing / stocker reservations released");
            return true;
        }

        bool Stop(string explanation)
        {
            ClearJob();
            status = explanation;
            return false;
        }

        bool ValidClaim()
        {
            if (CurrentJobId == null || !shop || !ReferenceEquals(claimedModel, shop.Model) ||
                !ReferenceEquals(claimedLayout, shop.Layout) || !ReferenceEquals(claimedStocking, shop.Stocking) ||
                shop.Stocking.GetTarget(TargetTankId) != targetSetting ||
                !shop.Stocking.Employee.IsOnDuty(shop.Cycle.DayNumber) || shop.PlacementActive ||
                shop.Panel == ShopPanel.Arrange) return false;
            var claim = claimedModel.GetStockTransfer(CurrentJobId);
            if (!claim.HasValue || claim.Value.OwnerId != ShopStaff.StockerId ||
                claim.Value.StorageId != SourceStorageId || claim.Value.TankId != TargetTankId ||
                claim.Value.SpecimenId != expectedSpecimenId) return false;
            var source = claimedModel.GetStorage(SourceStorageId);
            return source.HasValue && source.Value.Id == expectedSpecimenId &&
                !claimedModel.GetTank(TargetTankId).HasValue;
        }

        void AcquireJob()
        {
            string[] targets = shop.Model.TankIds;
            // Registry order is stable and matches the target list shown in the staff panel.
            var racks = new List<LayoutItemView>(shop.Layout.PlacedItems);
            racks.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            bool configured = false, emptyTarget = false, matchingStock = false;
            foreach (string tankId in targets)
            {
                string setting = shop.Stocking.GetTarget(tankId);
                if (setting == null) continue;
                configured = true;
                if (shop.Model.GetTank(tankId).HasValue || shop.Model.IsTankReservedForStocking(tankId)) continue;
                emptyTarget = true;
                foreach (var rack in racks)
                {
                    if (rack.DefinitionId != ShopLayout.RackDefinitionId) continue;
                    string[] slots = shop.Layout.GetStorageSlotIds(rack.Id);
                    Array.Sort(slots, StringComparer.Ordinal);
                    foreach (string storageId in slots)
                    {
                        var source = shop.Model.GetStorage(storageId);
                        if (!source.HasValue || shop.Model.IsStorageReservedForStocking(storageId) ||
                            setting != ShopStocking.AnySpecies && source.Value.SpeciesId != setting) continue;
                        matchingStock = true;
                        string jobId = ShopStaff.StockerId + "/restock/" + (++nextJobNumber) + "/" + source.Value.Id;
                        if (!shop.Model.ReserveStockTransfer(jobId, ShopStaff.StockerId, storageId, tankId, source.Value.Id).Success) continue;
                        claimedModel = shop.Model;
                        claimedLayout = shop.Layout;
                        claimedStocking = shop.Stocking;
                        CurrentJobId = jobId;
                        SourceStorageId = storageId;
                        TargetTankId = tankId;
                        expectedSpecimenId = source.Value.Id;
                        targetSetting = setting;
                        Vector3 origin = Actor.transform.position;
                        if (!shop.Layout.TryGetStorageAccessPoint(rack.Id, out float sx, out float sz) ||
                            !shop.Layout.TryGetBrowsePoint(tankId, 0, out float tx, out float tz) ||
                            !shop.Layout.TryGetWorkerPath(origin.x, origin.z, sx, sz, out route) ||
                            !shop.Layout.TryGetWorkerPath(sx, sz, tx, tz, out deliveryRoute))
                        {
                            Fail("Stock route blocked / clear the route and retry from Staff");
                            return;
                        }
                        routeIndex = 0;
                        phase = WorkPhase.ToRack;
                        serviceTime = doorWait = 0;
                        status = "Walking to reserved rack stock / " + storageId;
                        return;
                    }
                }
            }
            status = !configured ? "No display targets / choose Off, Any, or a species in Staff" :
                !emptyTarget ? "Targets full or reserved / waiting for an empty display" :
                !matchingStock ? "Matching rack stock missing / prepare and store creatures" :
                "Stock is currently reserved / waiting for a free source and display";
        }

        void Travel(float seconds)
        {
            // Cap catch-up work. Physics still sweeps every actual displacement, including a slow frame.
            float distance = MoveSpeed * Mathf.Min(seconds, .5f);
            for (int step = 0; step < 16 && distance > .00001f; step++)
            {
                if (route == null || routeIndex >= route.Length)
                {
                    phase = phase == WorkPhase.ToRack ? WorkPhase.Pickup : WorkPhase.Deliver;
                    serviceTime = 0;
                    status = phase == WorkPhase.Pickup ? "Collecting the reserved delivery / stock remains protected" :
                        "Placing the creature into " + TargetTankId;
                    return;
                }
                Vector3 from = Actor.transform.position;
                Vector3 target = new Vector3(route[routeIndex].X, 0, route[routeIndex].Z);
                Vector3 delta = target - from;
                delta.y = 0;
                if (delta.sqrMagnitude < .000001f) { routeIndex++; continue; }
                Vector3 direction = delta.normalized;
                float travel = Mathf.Min(distance, delta.magnitude);
                Vector3 to = from + direction * travel;
                if (!claimedLayout.IsWorkerSegmentClear(from.x, from.z, to.x, to.z))
                {
                    Fail("Furniture changed / stock route blocked; retry from Staff");
                    return;
                }
                bool carrying = phase == WorkPhase.ToDisplay;
                if (!PhysicalSegmentClear(from, to, direction, carrying, out bool openingDoor))
                {
                    if (openingDoor)
                    {
                        doorWait += seconds;
                        status = "Waiting for the laboratory door to open";
                        Actor.Pose(carrying ? CharacterSamplePose.Carry : CharacterSamplePose.Idle, 0);
                        if (doorWait >= 8f) Fail("Laboratory door unavailable / clear the doorway and retry from Staff");
                    }
                    else Fail("Physical route blocked / clear the obstacle and retry from Staff");
                    return;
                }
                doorWait = 0;
                Actor.transform.SetPositionAndRotation(to, Quaternion.LookRotation(direction, Vector3.up));
                TravelDistance += travel;
                distance -= travel;
                Animate(carrying ? CharacterSamplePose.Carry : CharacterSamplePose.Walk, travel / MoveSpeed);
                status = carrying ? "Carrying a reserved creature to " + TargetTankId : "Walking to reserved rack stock / " + SourceStorageId;
                if ((target - to).sqrMagnitude < .000001f) routeIndex++;
            }
        }

        bool PhysicalSegmentClear(Vector3 from, Vector3 to, Vector3 direction, bool carrying, out bool openingDoor)
        {
            openingDoor = false;
            // Keep the torso clear and check the forward parcel envelope independently while carrying.
            for (int envelope = 0; envelope < (carrying ? 2 : 1); envelope++)
            {
                Vector3 offset = envelope == 0 ? Vector3.zero : direction * .24f;
                Vector3 lower = from + offset + Vector3.up * LowerCenter;
                Vector3 upper = from + offset + Vector3.up * UpperCenter;
                int overlaps = Physics.OverlapCapsuleNonAlloc(lower, upper, BodyRadius, overlapHits, ~0, QueryTriggerInteraction.Ignore);
                if (overlaps == overlapHits.Length) return false;
                for (int i = 0; i < overlaps; i++)
                    if (IsObstacle(overlapHits[i], ref openingDoor)) return false;
                int hits = Physics.CapsuleCastNonAlloc(lower, upper, BodyRadius, direction, sweepHits,
                    Vector3.Distance(from, to) + .01f, ~0, QueryTriggerInteraction.Ignore);
                if (hits == sweepHits.Length) return false;
                for (int i = 0; i < hits; i++)
                    if (IsObstacle(sweepHits[i].collider, ref openingDoor)) return false;
            }
            return true;
        }

        bool IsObstacle(Collider collider, ref bool openingDoor)
        {
            if (!collider || !collider.enabled || collider.isTrigger || !shop.World ||
                !collider.transform.IsChildOf(shop.World.transform) || collider.bounds.max.y <= .035f ||
                collider.GetComponentInParent<ShopCharacterVisual>()) return false;
            var door = shop.World.LabDoor;
            if (door && collider.transform.IsChildOf(door.transform) && door.OpenFraction < .98f)
                openingDoor = true;
            return true;
        }

        void ClearJob()
        {
            if (shop) shop.CancelWorkerCelebration(ShopStaff.StockerId);
            if (claimedModel != null && CurrentJobId != null)
                claimedModel.CancelStockTransfer(CurrentJobId, ShopStaff.StockerId);
            CurrentJobId = SourceStorageId = TargetTankId = expectedSpecimenId = targetSetting = null;
            claimedModel = null;
            claimedLayout = null;
            claimedStocking = null;
            route = deliveryRoute = null;
            phase = WorkPhase.None;
            routeIndex = 0;
            serviceTime = doorWait = 0;
            if (Actor) Actor.Pose(CharacterSamplePose.Idle, 0);
        }

        void Fail(string explanation)
        {
            ClearJob();
            Blocked = true;
            cooldown = RetrySeconds;
            status = explanation;
        }

        void Animate(CharacterSamplePose pose, float seconds)
        {
            poseTime += seconds;
            if (Actor) Actor.Pose(pose, poseTime);
        }

        void BuildActor()
        {
            if (nameMaterial) Destroy(nameMaterial);
            var root = new GameObject("Employed stocker / " + ShopStaff.StockerId).transform;
            root.SetParent(transform, false);
            root.position = new Vector3(0, 0, 3.2f);
            Actor = root.gameObject.AddComponent<ShopCharacterVisual>();
            Actor.Build(true);
            var label = new GameObject("Nori / depth-tested name").transform;
            label.SetParent(root, false);
            label.localPosition = new Vector3(0, 1.98f, .02f);
            label.localRotation = Quaternion.Euler(0, 180, 0);
            nameCaption = label.gameObject.AddComponent<TextMesh>();
            nameCaption.text = "NORI\nSTOCKER";
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameCaption.font = font;
            nameCaption.fontSize = 64;
            nameCaption.characterSize = .008f;
            nameCaption.anchor = TextAnchor.MiddleCenter;
            nameCaption.alignment = TextAlignment.Center;
            nameCaption.color = new Color(1, .96f, .83f);
            var shader = Resources.Load<Shader>("EvolutionWorldText");
            if (!shader) throw new InvalidOperationException("Missing Resources/EvolutionWorldText.shader for stocker name.");
            nameMaterial = new Material(shader) { name = "Stocker / depth-tested name" };
            var renderer = nameCaption.GetComponent<Renderer>();
            renderer.sharedMaterial = nameMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            FontAtlasRebuilt(font);
        }

        void RecoverInvalidStandingPosition()
        {
            if (!shop || shop.Layout == null || !Actor) return;
            Vector3 position = Actor.transform.position;
            if (shop.Layout.IsWorkerSegmentClear(position.x, position.z, position.x, position.z)) return;
            // Only an explicit roster/reload refresh may recover a worker stranded by a changed
            // layout. Ordinary movement never teleports or cuts a failed path.
            // Prefer standing outside the automatic door sensor while waiting for work.
            Vector3[] homes = { new Vector3(0, 0, 3.2f), new Vector3(0, 0, 4.4f),
                new Vector3(0, 0, 2.85f), new Vector3(0, 0, -2), new Vector3(0, 0, -3),
                new Vector3(0, 0, 2), new Vector3(0, 0, 1.5f) };
            foreach (Vector3 home in homes)
                if (shop.Layout.IsWorkerSegmentClear(home.x, home.z, home.x, home.z) &&
                    PhysicalSegmentClear(home, home, Vector3.forward, false, out _))
                {
                    Actor.transform.SetPositionAndRotation(home, Quaternion.identity);
                    return;
                }
            Fail("No safe staff standing space / clear the main aisle and retry from Staff");
        }

        void FontAtlasRebuilt(Font rebuilt)
        {
            if (font && rebuilt == font && font.material && nameMaterial)
                nameMaterial.mainTexture = font.material.mainTexture;
        }

        void OnEnable() { Font.textureRebuilt += FontAtlasRebuilt; if (shop) RefreshVisual(); }
        void OnDisable()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            ResetTransient();
            status = "Stocker disabled / reserved stock released";
            if (Actor) Actor.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            ClearJob();
            if (Actor) Destroy(Actor.gameObject);
            if (nameMaterial) Destroy(nameMaterial);
        }
    }
}
