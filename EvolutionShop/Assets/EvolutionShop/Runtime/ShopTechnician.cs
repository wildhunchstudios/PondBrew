using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// Carries out known incubation orders. Claims protect the machine and a free rack slot;
    /// the session alone durably starts production or moves its exact output into storage.
    /// </summary>
    public sealed class ShopTechnician : MonoBehaviour
    {
        public const float MoveSpeed = 2.1f, ServiceSeconds = 1f, RetrySeconds = 2f;
        const float BodyRadius = .30f, LowerCenter = .34f, UpperCenter = 1.43f;
        enum WorkPhase { None, ToIncubatorForStart, StartRecipe, ToIncubatorForOutput, Pickup, ToRack, Deliver }
        public string CurrentJobId { get; private set; }
        public string TargetStorageId { get; private set; }
        public string Status => shop && shop.Paused ? "Game paused / technician time frozen" : status;
        public ShopCharacterVisual Actor { get; private set; }
        public float ServiceProgress=>Mathf.Clamp01(serviceTime/ServiceSeconds);
        public FxWorkerActivity WorkerActivity=>phase==WorkPhase.ToIncubatorForStart||phase==WorkPhase.ToIncubatorForOutput?FxWorkerActivity.Walk:
            phase==WorkPhase.StartRecipe?(ServiceProgress<.333333f?FxWorkerActivity.Measure:ServiceProgress<.666667f?FxWorkerActivity.Dose:FxWorkerActivity.Start):
            phase==WorkPhase.Pickup?FxWorkerActivity.Pickup:phase==WorkPhase.ToRack?FxWorkerActivity.Carry:
            phase==WorkPhase.Deliver?FxWorkerActivity.Place:shop&&shop.Laboratory.ActiveSpecimenId!=null?FxWorkerActivity.Check:FxWorkerActivity.Idle;
        public bool Blocked { get; private set; }
        public bool Carrying => phase == WorkPhase.ToRack || phase == WorkPhase.Deliver;
        public string Phase => phase == WorkPhase.ToIncubatorForStart || phase == WorkPhase.ToIncubatorForOutput ? "Walking to incubator" :
            phase == WorkPhase.StartRecipe ? "Starting known recipe" : phase == WorkPhase.Pickup ? "Collecting output" :
            phase == WorkPhase.ToRack ? "Carrying to rack" : phase == WorkPhase.Deliver ? "Placing into rack" : "Idle";
        public bool ReadyToStart => phase == WorkPhase.StartRecipe && ReadyForCommit();
        public bool ReadyToDeliver => phase == WorkPhase.Deliver && ReadyForCommit();
        public float TravelDistance { get; private set; }
        public int StartedJobs { get; private set; }
        public int CompletedJobs { get; private set; }
        // Keep the leaves open even if a visible worker is paused in their threshold.
        public Vector3? ActiveDoorPosition => isActiveAndEnabled && Actor && Actor.isActiveAndEnabled
            ? Actor.transform.position : (Vector3?)null;

        ShopSession shop;
        GameModel claimedModel;
        ShopLayout claimedLayout;
        ShopLaboratory claimedLaboratory;
        string expectedSpecimenId, expectedSpeciesId, expectedSpeciesName, status = "No technician hired";
        WorkPhase phase;
        WorkerPathPoint[] route, deliveryRoute;
        int routeIndex;
        long nextJobNumber;
        float serviceTime, cooldown, poseTime, doorWait;
        bool shown, startsRecipe;
        readonly RaycastHit[] sweepHits = new RaycastHit[32];
        readonly Collider[] overlapHits = new Collider[32];
        Material nameMaterial;
        Font font;
        TextMesh nameCaption;
        Transform actorRoot;

        public void Initialize(ShopSession session)
        {
            if (shop || !session) return;
            shop = session;
            BuildActor();
            RefreshVisual();
        }

        /// <summary>Only explicit roster/reload refresh may restore a missing visual.</summary>
        public void RefreshVisual()
        {
            ResetTransient();
            bool hired = shop && shop.Laboratory != null && shop.Laboratory.Employee.Hired;
            if (hired && !Actor) BuildActor();
            shown = hired;
            if (Actor)
            {
                Actor.enabled = true;
                Actor.gameObject.SetActive(isActiveAndEnabled && hired);
                Actor.Pose(CharacterSamplePose.Idle, 0);
                if (hired) RecoverInvalidStandingPosition();
            }
            if (Available() && !Blocked) status = "On duty / checking assigned laboratory work";
        }

        /// <summary>Releases the original model's claim without consuming cultures or moving output.</summary>
        public void ResetTransient()
        {
            if(Actor&&shop&&shop.World)shop.World.CancelFxOwner(Actor.WorkerFxOwner);
            ClearJob();
            Blocked = false;
            cooldown = 0;
            poseTime = 0;
            status = "Ready to check assigned laboratory work";
        }

        public void Retry()
        {
            ResetTransient();
            if (Available()) status = "Retrying the laboratory route";
        }

        public void Tick(float seconds)
        {
            TickCore(seconds);
            if(shop&&Actor)Actor.PoseWorker(WorkerActivity,ServiceProgress,seconds,Carrying,CurrentJobId,expectedSpecimenId,shop.Paused);
        }
        void TickCore(float seconds)
        {
            if (!shop || shop.Paused || !isActiveAndEnabled || seconds <= 0 ||
                float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            bool hired = shop.Laboratory != null && shop.Laboratory.Employee.Hired;
            if (shown != hired) RefreshVisual();
            if (!Available() || Blocked) return;
            if (CurrentJobId != null && !ValidClaim())
            {
                ClearJob();
                cooldown = RetrySeconds;
                status = "Assignment changed / cultures and incubator output remain safe";
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
                case WorkPhase.ToIncubatorForStart:
                case WorkPhase.ToIncubatorForOutput:
                case WorkPhase.ToRack:
                    Travel(seconds);
                    break;
                case WorkPhase.StartRecipe:
                    Animate(CharacterSamplePose.Idle, seconds);
                    serviceTime = Mathf.Min(ServiceSeconds, serviceTime + seconds);
                    if (!ReadyToStart) return;
                    if (!shop.StartTechnicianJob(CurrentJobId))
                    {
                        Fail("Production could not be saved / cultures are safe; retry from Staff");
                        return;
                    }
                    StartedJobs++;
                    ClearJob();
                    cooldown = RetrySeconds;
                    status = "Recipe started / waiting for its incubation to finish";
                    break;
                case WorkPhase.Pickup:
                    Animate(CharacterSamplePose.Idle, seconds);
                    serviceTime = Mathf.Min(ServiceSeconds, serviceTime + seconds);
                    if (serviceTime < ServiceSeconds) return;
                    route = deliveryRoute;
                    routeIndex = 0;
                    phase = WorkPhase.ToRack;
                    serviceTime = 0;
                    status = "Carrying " + expectedSpeciesName + " to the reserved lab rack";
                    shop.PresentWorkerHandling(Actor,CurrentJobId);
                    break;
                case WorkPhase.Deliver:
                    // The parcel never owns stock. Until the saved delivery commits, the creature
                    // remains in the incubator and interruption simply releases its reservation.
                    Actor.Pose(CharacterSamplePose.Carry, 0);
                    serviceTime = Mathf.Min(ServiceSeconds, serviceTime + seconds);
                    if (!ReadyToDeliver) return;
                    if (!shop.CompleteTechnicianJob(CurrentJobId))
                    {
                        Fail("Delivery could not be saved / output is safe in the incubator; retry from Staff");
                        return;
                    }
                    CompletedJobs++;
                    ClearJob();
                    cooldown = RetrySeconds;
                    status = "Creature stored / checking the next production order";
                    shop.QueueWorkerCelebration(ShopStaff.TechnicianId);
                    break;
            }
        }

        bool ReadyForCommit() => serviceTime >= ServiceSeconds && shop && !shop.Paused &&
            !shop.AutosaveBlocked && isActiveAndEnabled && Actor && Actor.isActiveAndEnabled && ValidClaim();

        bool Available()
        {
            if (!shop || shop.Laboratory == null) return Stop("Technician details unavailable");
            var employee = shop.Laboratory.Employee;
            if (!isActiveAndEnabled) return Stop("Technician disabled");
            if (!employee.Hired) return Stop("No technician hired");
            if (!Actor || !Actor.isActiveAndEnabled)
            {
                Fail("Technician unavailable / refresh the roster or reload to restore Moss");
                return false;
            }
            if (!employee.Scheduled) return Stop("Off duty / not scheduled");
            if (employee.Paused) return Stop("Technician paused / manual laboratory work is available");
            if (!employee.IsOnDuty(shop.Cycle.DayNumber)) return Stop("Off duty / pay the duty period at night");
            if (shop.AutosaveBlocked) return Stop("Save unavailable / technician reservations released");
            if (shop.PlacementActive || shop.Panel == ShopPanel.Arrange)
                return Stop("Room editing / technician reservations released");
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
                !ReferenceEquals(claimedLayout, shop.Layout) || !ReferenceEquals(claimedLaboratory, shop.Laboratory) ||
                !shop.Laboratory.Employee.IsOnDuty(shop.Cycle.DayNumber) || shop.AutosaveBlocked ||
                shop.PlacementActive || shop.Panel == ShopPanel.Arrange) return false;
            var claim = claimedModel.TechnicianClaim;
            if (!claim.HasValue || claim.Value.JobId != CurrentJobId || claim.Value.OwnerId != ShopStaff.TechnicianId ||
                claim.Value.StorageId != TargetStorageId || claim.Value.SpecimenId != expectedSpecimenId ||
                claim.Value.SpeciesId != expectedSpeciesId || claim.Value.StartsRecipe != startsRecipe ||
                claimedModel.GetStorage(TargetStorageId).HasValue) return false;
            var incubator = claimedModel.Incubator;
            if (startsRecipe)
            {
                // Session edits publish a replacement Laboratory and cancel this claim. Avoid
                // cloning its queue every frame; the durable start also validates the head order.
                return claimedLaboratory.ActiveSpecimenId == null &&
                    incubator.Phase == IncubatorPhase.Dosing && EmptyMixture(incubator);
            }
            return claimedLaboratory.ActiveSpecimenId == expectedSpecimenId &&
                claimedLaboratory.ActiveSpeciesId == expectedSpeciesId && incubator.Phase == IncubatorPhase.Ready &&
                incubator.SpecimenId == expectedSpecimenId && incubator.SpeciesId == expectedSpeciesId;
        }

        static bool EmptyMixture(IncubatorView incubator) =>
            incubator.Bloom == 0 && incubator.Flow == 0 && incubator.Spark == 0 && incubator.Mist == 0;

        void AcquireJob()
        {
            var incubator = shop.Model.Incubator;
            string active = shop.Laboratory.ActiveSpecimenId;
            bool start = active == null;
            string speciesId;
            if (!start)
            {
                speciesId = shop.Laboratory.ActiveSpeciesId;
                if (incubator.SpecimenId != active || incubator.SpeciesId != speciesId)
                {
                    status = "Assigned output was collected manually / updating laboratory work";
                    return;
                }
                if (incubator.Phase != IncubatorPhase.Ready)
                {
                    status = "Assigned recipe incubating / waiting for its output";
                    return;
                }
            }
            else
            {
                var queue = shop.Laboratory.Queue;
                if (queue.Length == 0) { status = "No production orders / assign a known recipe in Staff"; return; }
                speciesId = queue[0].speciesId;
                if (incubator.Phase != IncubatorPhase.Dosing || !EmptyMixture(incubator))
                {
                    status = "Incubator belongs to manual work / finish or clear it first";
                    return;
                }
                var recipe = shop.Model.GetSpecies(speciesId);
                if (recipe == null || !shop.Model.IsDiscovered(speciesId))
                {
                    status = "First order is not a known recipe / review the laboratory queue";
                    return;
                }
                if (shop.Automation != null && shop.Automation.IsPending && shop.Automation.RecipeId == speciesId)
                {
                    status = "Waiting for the matching culture request to finish";
                    return;
                }
                if (shop.Model.Available(CultureKind.Bloom) < recipe.Bloom ||
                    shop.Model.Available(CultureKind.Flow) < recipe.Flow ||
                    shop.Model.Available(CultureKind.Spark) < recipe.Spark ||
                    shop.Model.Available(CultureKind.Mist) < recipe.Mist)
                {
                    status = "First order needs cultures / collect its ingredients from growers";
                    return;
                }
            }

            if (shop.Model.TechnicianClaim.HasValue)
            {
                status = "Incubator reserved / waiting for laboratory access";
                return;
            }
            // Scan only at the bounded idle cadence. Rack order remains stable across reloads.
            var racks = new List<LayoutItemView>(shop.Layout.PlacedItems);
            racks.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            foreach (var rack in racks)
            {
                if (rack.DefinitionId != ShopLayout.RackDefinitionId) continue;
                string[] slots = shop.Layout.GetStorageSlotIds(rack.Id);
                Array.Sort(slots, StringComparer.Ordinal);
                foreach (string storageId in slots)
                {
                    if (shop.Model.GetStorage(storageId).HasValue || shop.Model.IsStorageReservedForStocking(storageId) ||
                        shop.Model.IsStorageReservedForTechnician(storageId)) continue;
                    string specimenId = start ? Guid.NewGuid().ToString("N") : active;
                    string jobId = ShopStaff.TechnicianId + (start ? "/produce/" : "/store/") + (++nextJobNumber) + "/" + specimenId;
                    var result = start ? shop.Model.ReserveTechnicianStart(jobId, ShopStaff.TechnicianId, speciesId, storageId, specimenId) :
                        shop.Model.ReserveTechnicianOutput(jobId, ShopStaff.TechnicianId, storageId, specimenId);
                    if (!result.Success) continue;
                    claimedModel = shop.Model;
                    claimedLayout = shop.Layout;
                    claimedLaboratory = shop.Laboratory;
                    CurrentJobId = jobId;
                    TargetStorageId = storageId;
                    expectedSpecimenId = specimenId;
                    expectedSpeciesId = speciesId;
                    expectedSpeciesName = shop.Model.GetSpecies(speciesId)?.DisplayName ?? "the creature";
                    startsRecipe = start;
                    Vector3 origin = Actor.transform.position;
                    if (!shop.Layout.TryGetFacilityAccessPoint(ShopLayout.IncubatorFacilityId, out float ix, out float iz) ||
                        !shop.Layout.TryGetStorageAccessPoint(rack.Id, out float tx, out float tz) ||
                        !shop.Layout.TryGetWorkerPath(origin.x, origin.z, ix, iz, out route) ||
                        !shop.Layout.TryGetWorkerPath(ix, iz, tx, tz, out deliveryRoute))
                    {
                        Fail("Laboratory route blocked / clear the route and retry from Staff");
                        return;
                    }
                    routeIndex = 0;
                    phase = start ? WorkPhase.ToIncubatorForStart : WorkPhase.ToIncubatorForOutput;
                    serviceTime = doorWait = 0;
                    status = start ? "Walking to incubator / cultures reserved for " + expectedSpeciesName :
                        "Walking to collect assigned incubator output";
                    return;
                }
            }
            status = "No free rack slot / store or sell existing creatures; output and cultures remain safe";
        }

        void Travel(float seconds)
        {
            // Cap catch-up work and sweep every real displacement, including slow frames.
            float distance = MoveSpeed * Mathf.Min(seconds, .5f);
            for (int step = 0; step < 16 && distance > .00001f; step++)
            {
                if (route == null || routeIndex >= route.Length)
                {
                    phase = phase == WorkPhase.ToIncubatorForStart ? WorkPhase.StartRecipe :
                        phase == WorkPhase.ToIncubatorForOutput ? WorkPhase.Pickup : WorkPhase.Deliver;
                    serviceTime = 0;
                    status = phase == WorkPhase.StartRecipe ? "Preparing the assigned known recipe" :
                        phase == WorkPhase.Pickup ? "Collecting reserved incubator output" : "Placing " + expectedSpeciesName + " into the reserved lab rack";
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
                    Fail("Furniture changed / laboratory route blocked; retry from Staff");
                    return;
                }
                bool carrying = phase == WorkPhase.ToRack;
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
                status = carrying ? "Carrying " + expectedSpeciesName + " to the reserved lab rack" :
                    startsRecipe ? "Walking to prepare " + expectedSpeciesName : "Walking to collect assigned incubator output";
                if ((target - to).sqrMagnitude < .000001f) routeIndex++;
            }
        }

        bool PhysicalSegmentClear(Vector3 from, Vector3 to, Vector3 direction, bool carrying, out bool openingDoor)
        {
            openingDoor = false;
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
            if (shop) shop.CancelWorkerCelebration(ShopStaff.TechnicianId);
            if (claimedModel != null && CurrentJobId != null)
                claimedModel.CancelTechnicianClaim(CurrentJobId, ShopStaff.TechnicianId);
            CurrentJobId = TargetStorageId = expectedSpecimenId = expectedSpeciesId = expectedSpeciesName = null;
            claimedModel = null;
            claimedLayout = null;
            claimedLaboratory = null;
            route = deliveryRoute = null;
            phase = WorkPhase.None;
            startsRecipe = false;
            routeIndex = 0;
            serviceTime = doorWait = 0;
            if (Actor) { Actor.Pose(CharacterSamplePose.Idle, 0); Actor.ClearWorkerJobPresentation(); }
        }

        void Fail(string explanation)
        {
            string failedJob=CurrentJobId;
            ClearJob();
            if(shop)shop.PresentWorkerResult(Actor,FxWorkerEvent.Refused,failedJob,true);
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
            if (actorRoot) Destroy(actorRoot.gameObject);
            var root = new GameObject("Employed technician / " + ShopStaff.TechnicianId).transform;
            actorRoot = root;
            root.SetParent(transform, false);
            root.position = new Vector3(0, 0, 4.4f);
            Actor = root.gameObject.AddComponent<ShopCharacterVisual>();
            Actor.Build(true);
            Actor.ConfigureWorker(shop.World,ShopStaff.TechnicianId);
            var label = new GameObject("Moss / depth-tested name").transform;
            label.SetParent(root, false);
            label.localPosition = new Vector3(0, 1.98f, .02f);
            label.localRotation = Quaternion.Euler(0, 180, 0);
            nameCaption = label.gameObject.AddComponent<TextMesh>();
            nameCaption.text = "MOSS\nTECHNICIAN";
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameCaption.font = font;
            nameCaption.fontSize = 64;
            nameCaption.characterSize = .008f;
            nameCaption.anchor = TextAnchor.MiddleCenter;
            nameCaption.alignment = TextAlignment.Center;
            nameCaption.color = new Color(.79f, 1f, .91f);
            var shader = Resources.Load<Shader>("EvolutionWorldText");
            if (!shader) throw new InvalidOperationException("Missing Resources/EvolutionWorldText.shader for technician name.");
            nameMaterial = new Material(shader) { name = "Technician / depth-tested name" };
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
            if (shop.Layout.IsWorkerSegmentClear(position.x, position.z, position.x, position.z) &&
                PhysicalSegmentClear(position, position, Actor.transform.forward, false, out _)) return;
            // Only explicit roster/reload refresh recovers a stranded actor; normal travel
            // always follows its checked path. Prefer the lab beyond the door sensor.
            Vector3[] homes = { new Vector3(0, 0, 4.4f), new Vector3(0, 0, 3.2f),
                new Vector3(0, 0, 2.85f), new Vector3(0, 0, -2), new Vector3(0, 0, -3) };
            foreach (Vector3 home in homes)
                if (shop.Layout.IsWorkerSegmentClear(home.x, home.z, home.x, home.z) &&
                    PhysicalSegmentClear(home, home, Vector3.forward, false, out _))
                {
                    Actor.transform.SetPositionAndRotation(home, Quaternion.identity);
                    return;
                }
            Fail("No safe staff standing space / clear the main aisle and refresh Moss from Staff");
        }

        void LateUpdate()
        {
            if (!nameCaption || !shop || !shop.Player || !shop.Player.View) return;
            // Match customer captions: TextMesh faces local -Z; keep its front toward the player.
            Vector3 away = nameCaption.transform.position - shop.Player.View.transform.position;
            away.y = 0;
            if (away.sqrMagnitude > .001f) nameCaption.transform.rotation = Quaternion.LookRotation(away);
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
            status = "Technician disabled / laboratory reservations released";
            if (Actor) Actor.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            ClearJob();
            if (actorRoot) Destroy(actorRoot.gameObject);
            if (nameMaterial) Destroy(nameMaterial);
        }
    }
}
