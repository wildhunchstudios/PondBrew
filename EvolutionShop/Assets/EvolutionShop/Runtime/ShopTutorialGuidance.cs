using UnityEngine;

namespace EvolutionShop
{
    /// <summary>One transient presentation destination, reconstructed from live station identity.</summary>
    public readonly struct TutorialGuidanceTarget
    {
        public readonly Transform Root;
        public readonly Station Station;
        public readonly Vector3 Position;
        public readonly bool IsDoor;
        public readonly string Label, Action;
        public TutorialGuidanceTarget(Transform root, Station station, Vector3 position, bool isDoor, string label, string action)
        { Root = root; Station = station; Position = position; IsDoor = isDoor; Label = label; Action = action; }
    }

    public sealed partial class ShopSession
    {
        // Bounded scratch storage, never a saved/cached destination. Saturation hides a marker.
        readonly RaycastHit[] tutorialOcclusionHits = new RaycastHit[16];
        bool TutorialGuidanceAvailable => Tutorial != null && Tutorial.Mode == TutorialMode.Active &&
            Model != null && Cycle != null && World && Player && !PlacementActive &&
            !AutosaveBlocked && string.IsNullOrEmpty(SaveError);
        bool TutorialRoomGuidanceActive => TutorialGuidanceAvailable &&
            (ActiveTutorialLesson >= TutorialLesson.Orient && ActiveTutorialLesson <= TutorialLesson.Serve ||
             ActiveTutorialLesson == TutorialLesson.CloseAndReview || TutorialPracticeGuidanceRequested);

        public bool ShowTutorialTarget()
        {
            if (!CanShowTutorialTarget) return false;
            if (ActiveTutorialLesson == TutorialLesson.Practice)
            {
                if (Tutorial.MarkPracticeAssisted() && !SaveNow()) return false;
                tutorialPracticeDirections = true;
            }
            CloseTutorialHelp();
            SetPanel(ShopPanel.None);
            return true;
        }

        public bool TryGetTutorialGuidance(out TutorialGuidanceTarget target)
        {
            target = default;
            if (Panel != ShopPanel.None || Paused || !TutorialRoomGuidanceActive || Cycle.Phase == ShopDayPhase.Closing) return false;
            return ResolveTutorialGuidance(out target, out _);
        }

        public string TutorialGuidanceRecovery
        {
            get
            {
                if (!TutorialRoomGuidanceActive || Cycle.Phase == ShopDayPhase.Closing) return null;
                ResolveTutorialGuidance(out _, out var recovery);
                return recovery;
            }
        }

        bool ResolveTutorialGuidance(out TutorialGuidanceTarget target, out string recovery)
        {
            target = default; recovery = null;
            var lesson = TutorialGuidanceLesson;
            if (TutorialNeedsReportRecovery)
            {
                recovery = "The older day report was replaced. Press F1 to review your saved guided-sale receipts and finish preparation.";
                return false;
            }
            Station station = null;
            string action = null;
            bool doorOnly = false;
            bool playerInLab = TutorialPlayerInLab();
            bool urgent = Customers != null &&
                (Customers.NextToServe != null && Customers.NextToServe.Phase == CustomerPhase.Queued ||
                 Customers.Visit != null && Customers.Visit.Phase == CustomerPhase.ToCounter);
            if (urgent)
            { station = TutorialStation(StationKind.Counter); action = "E / serve at the counter"; }
            else if (TutorialProcessingConditioner() is string processingId)
            {
                foreach (var item in World.Stations)
                    if (TutorialStationLive(item) && item.ConditionerItemId == processingId) { station = item; break; }
                action = "E / wait or cancel conditioning";
                recovery = "Your creature is conditioning in the lab vat. Wait for it, or use E at that vat to cancel and recover the unchanged creature.";
            }
            else if ((recovery = TutorialStockRecovery()) != null)
            { station = TutorialStation(StationKind.Counter); action = "E / Staff / pause the reserved work"; }
            else switch (lesson)
            {
                case TutorialLesson.Orient:
                    if (playerInLab) return false; // Movement/look still have their real input predicates.
                    doorOnly = true; action = "Approach to enter your lab";
                    break;
                case TutorialLesson.Collect:
                case TutorialLesson.Prepare:
                case TutorialLesson.Grow:
                    if (Model.IsIncubatorReservedForTechnician)
                    { station = TutorialStation(StationKind.Counter); action = "E / Staff / pause lab work"; }
                    else
                    {
                        var needed = TutorialNeededCulture();
                        if (needed.HasValue)
                        {
                            station = TutorialStation(needed == CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow);
                            action = "Aim + hold left mouse / " + needed.Value;
                        }
                        else
                        {
                            station = TutorialStation(StationKind.Incubator);
                            action = Model.Incubator.Phase == IncubatorPhase.Incubating ? "Growing / output waits here" :
                                Model.Incubator.Phase == IncubatorPhase.Ready ? "Ready / stock an empty display" : "E / prepare or repair doses";
                        }
                    }
                    break;
                case TutorialLesson.Display:
                    station = TutorialEmptyDisplay(); action = "E / stock your ready creature";
                    if (!station && !TutorialHasEmptyDisplay())
                    { station = TutorialStation(StationKind.Counter); action = "E / make room through sales"; }
                    break;
                case TutorialLesson.PriceAndOpen:
                    // Prices follow the actual manually stocked specimen, including relocated displays.
                    station = TutorialFirstStockDisplay();
                    if (!station)
                    {
                        station = TutorialEmptyDisplay(); action = "E / restock your creature";
                        if (!station && !TutorialHasEmptyDisplay())
                        { station = TutorialStation(StationKind.Counter); action = "E / open or serve to make room"; }
                    }
                    else if (!TutorialPracticeGuidanceRequested && (Tutorial.Facts & TutorialFacts.PriceViewed) == 0)
                        action = "E / inspect its price tag";
                    else
                    { station = TutorialStation(StationKind.Counter); action = "E / open for the day"; }
                    break;
                case TutorialLesson.Serve:
                    // Prior manual display remains learned if the exact creature is stored again.
                    // Other saleable stock must not conceal the required restock.
                    if (!TutorialTrackedCreatureIsDisplayed())
                    {
                        station = TutorialEmptyDisplay(); action = "E / restock your creature";
                        if (!station && !TutorialHasEmptyDisplay())
                        { station = TutorialStation(StationKind.Counter); action = "E / make room through sales"; }
                        break;
                    }
                    station = TutorialStation(StationKind.Counter);
                    action = Cycle.Phase == ShopDayPhase.Night ? "E / open for the day" : "E / serve accepted orders";
                    break;
                case TutorialLesson.CloseAndReview:
                    station = TutorialStation(StationKind.Counter);
                    action = Cycle.Phase == ShopDayPhase.Night ? "E / review day results" : "E / close when ready";
                    break;
            }
            if (!doorOnly && !station)
            {
                recovery = lesson == TutorialLesson.Display || lesson == TutorialLesson.PriceAndOpen
                    ? "No usable display target is present. At the counter, arrange a reachable empty display, then stock your creature. F1 for help."
                    : "The required station is unavailable. Check its placement from the counter and restore access; F1 keeps the lesson available.";
                return false;
            }
            bool targetInLab = station && World.transform.InverseTransformPoint(station.transform.position).z > TutorialDoorZ();
            if (doorOnly || targetInLab != playerInLab)
            {
                if (!World.LabDoor || !World.LabDoor.isActiveAndEnabled)
                { recovery = "The lab doorway is unavailable. Restore access between the rooms before continuing; F1 for help."; return false; }
                var door = World.LabDoor.transform;
                bool goingToLab = doorOnly || targetInLab;
                target = new TutorialGuidanceTarget(door, null, door.TransformPoint(new Vector3(0, 1.8f, goingToLab ? -.13f : .13f)), true,
                    goingToLab ? "LAB DOOR" : "SHOP DOOR", goingToLab ? "Approach / go through to the lab" : "Approach / go through to the shop");
                return true;
            }
            target = new TutorialGuidanceTarget(station.transform, station, station.transform.TransformPoint(new Vector3(0, 1.45f, 0)), false,
                station.Label ?? station.Kind.ToString(), action);
            return true;
        }

        float TutorialDoorZ() => World.LabDoor ? World.transform.InverseTransformPoint(World.LabDoor.transform.position).z : .5f;
        bool TutorialPlayerInLab() => World.transform.InverseTransformPoint(Player.transform.position).z > TutorialDoorZ();
        static bool TutorialStationLive(Station station) => station && station.isActiveAndEnabled;
        Station TutorialStation(StationKind kind)
        {
            string id = kind == StationKind.Bloom ? ShopLayout.BloomFacilityId : kind == StationKind.Flow ? ShopLayout.FlowFacilityId :
                kind == StationKind.Incubator ? ShopLayout.IncubatorFacilityId : null;
            foreach (var station in World.Stations)
                if (TutorialStationLive(station) && station.Kind == kind && (id == null || station.FacilityId == id)) return station;
            return null;
        }
        Station TutorialEmptyDisplay()
        {
            foreach (var station in World.Stations)
                if (TutorialStationLive(station) && Model.HasTank(station.TankId) &&
                    !Model.GetTank(station.TankId).HasValue && !Model.IsTankReservedForStocking(station.TankId)) return station;
            return null;
        }
        Station TutorialFirstStockDisplay()
        {
            string specimen = TutorialTrackedSpecimenId;
            if (string.IsNullOrEmpty(specimen)) return null;
            foreach (var station in World.Stations)
            {
                if (!TutorialStationLive(station) || string.IsNullOrEmpty(station.TankId)) continue;
                var stock = Model.GetTank(station.TankId);
                if (stock.HasValue && stock.Value.Id == specimen) return station;
            }
            return null;
        }

        string TutorialTrackedSpecimenId => TutorialPracticeGuidanceRequested ? Tutorial.PracticeSpecimenId : Tutorial.FirstSpecimenId;
        bool TutorialTrackedCreatureIsDisplayed()
        {
            string id = TutorialTrackedSpecimenId;
            if (string.IsNullOrEmpty(id)) return false;
            foreach (string tank in Model.TankIds)
                if (Model.GetTank(tank) is SpecimenView stock && stock.Id == id) return true;
            return false;
        }
        string TutorialProcessingConditioner()
        {
            string specimen = TutorialTrackedSpecimenId;
            if (string.IsNullOrEmpty(specimen)) return null;
            foreach (string id in Model.ConditionerIds)
            {
                var vat = Model.GetConditioner(id);
                if (vat.HasValue && vat.Value.Phase == ConditionerPhase.Processing &&
                    (vat.Value.First is SpecimenView first && first.Id == specimen ||
                     vat.Value.Second is SpecimenView second && second.Id == specimen)) return id;
            }
            return null;
        }
        string TutorialStockRecovery()
        {
            var lesson = TutorialGuidanceLesson;
            if (lesson != TutorialLesson.Display && lesson != TutorialLesson.PriceAndOpen && lesson != TutorialLesson.Serve ||
                TutorialTrackedCreatureIsDisplayed()) return null;
            string id = TutorialTrackedSpecimenId;
            if (!string.IsNullOrEmpty(id))
            {
                if (Model.Incubator.SpecimenId == id && Model.IsIncubatorReservedForTechnician)
                    return "Moss reserved your ready creature. At Counter > Staff, pause lab work before stocking it yourself.";
                foreach (string rack in Model.StorageIds)
                    if (Model.GetStorage(rack) is SpecimenView stored && stored.Id == id && Model.IsStorageReservedForStocking(rack))
                        return "Nori reserved your stored creature. At Counter > Staff, pause stocking before displaying it yourself.";
            }
            if (!TutorialHasEmptyDisplay())
                foreach (string tank in Model.TankIds)
                    if (!Model.GetTank(tank).HasValue && Model.IsTankReservedForStocking(tank))
                        return "Nori reserved the empty displays. Wait for stocking, or pause stocking at Counter > Staff to free a destination.";
            return null;
        }

        CultureKind? TutorialNeededCulture()
        {
            if (Model.Incubator.Phase != IncubatorPhase.Dosing) return null;
            var inc = Model.Incubator;
            foreach (var recipe in Config.Species)
                if (recipe.Bloom == inc.Bloom && recipe.Flow == inc.Flow && recipe.Spark == inc.Spark && recipe.Mist == inc.Mist &&
                    (recipe.Spark == 0 || Model.SparkResearchUnlocked) && (recipe.Mist == 0 || Model.MistResearchUnlocked)) return null;
            SpeciesDefinition starter = null;
            foreach (var recipe in Config.Species) if (recipe.Id == "pondlet") { starter = recipe; break; }
            if (starter == null) return null;
            for (int i = 0; i < 4; i++)
                if (Model.Staged((CultureKind)i) > TutorialRecipeDose(starter, (CultureKind)i)) return null;
            // Returning staged doses alone cannot free the combined carried/staged capacity.
            if (Model.ReservedCultureCount >= Model.Capacity) return null;
            if (Model.Available(CultureKind.Bloom) + inc.Bloom < starter.Bloom) return CultureKind.Bloom;
            if (Model.Available(CultureKind.Flow) + inc.Flow < starter.Flow) return CultureKind.Flow;
            return null;
        }

        public string TutorialLocalCallout
        {
            get
            {
                if (!TutorialRoomGuidanceActive || Paused) return null;
                if (Panel == ShopPanel.Incubator)
                {
                    string recovery = TutorialPreparationRecovery(TutorialGuidanceLesson);
                    if (!string.IsNullOrEmpty(recovery)) return recovery;
                    if (Model.Incubator.Phase == IncubatorPhase.Ready) return "Your creature waits here. Close this panel and press E at an empty front display to stock it.";
                    if (Model.Incubator.Phase == IncubatorPhase.Incubating) return "Your batch is growing normally. It keeps growing with this panel open; F1 help pauses it.";
                    return "Stage " + TutorialStarterRecipe() + " with the + controls below. Use - to return extras, then Begin incubation.";
                }
                if (Panel == ShopPanel.Tank)
                    return Model.GetTank(selectedTank).HasValue
                        ? "Check the price tag and buyer reactions below. Keeping the standard price is fine; open at the counter when ready."
                        : "Choose your ready or stored creature below. Stocking transfers it directly into this display.";
                if (Panel == ShopPanel.Counter && Customers != null && Customers.NextToServe != null && Customers.NextToServe.Phase == CustomerPhase.Queued)
                    return "Serve the waiting order below before patience runs out. Its exact creature is paid only after successful checkout.";
                if (Panel == ShopPanel.Counter && TutorialStockRecovery() is string stockRecovery) return stockRecovery;
                if (Panel == ShopPanel.Counter && ActiveTutorialLesson == TutorialLesson.CloseAndReview)
                    return Cycle.Phase == ShopDayPhase.Night ? "Review your completed day results below, then choose Continue preparation." :
                        "Use Close for the night below, then confirm. Accepted orders still follow ordinary closing rules.";
                if (Panel == ShopPanel.Counter)
                    return Model.IsIncubatorReservedForTechnician ? "Choose Staff, then pause Moss's lab work to prepare this batch yourself." :
                        Cycle.Phase == ShopDayPhase.Night ? "Stock your displays, then use Open for day below. The trading clock starts when you open." :
                        "Serve a waiting order here. A reservation becomes income only after successful checkout.";
                return null;
            }
        }

        public bool TryProjectTutorialGuidance(out TutorialGuidanceTarget target, out Vector2 hudPosition)
        {
            hudPosition = default;
            if (!TryGetTutorialGuidance(out target) || !Player.View) return false;
            var camera = Player.View;
            Vector3 screen = camera.WorldToScreenPoint(target.Position);
            if (screen.z <= camera.nearClipPlane || screen.x < Screen.width * .04f || screen.x > Screen.width * .96f ||
                screen.y < Screen.height * .04f || screen.y > Screen.height * .96f) return false;
            Vector3 offset = target.Position - camera.transform.position;
            float distance = offset.magnitude;
            if (distance < .05f) return false;
            int hits = Physics.RaycastNonAlloc(camera.transform.position, offset / distance, tutorialOcclusionHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == tutorialOcclusionHits.Length) return false;
            for (int i = 0; i < hits; i++)
            {
                var hit = tutorialOcclusionHits[i];
                if (hit.distance >= distance - .025f || !hit.collider) continue;
                var transform = hit.collider.transform;
                if (transform.IsChildOf(Player.transform) || transform.IsChildOf(target.Root)) continue;
                return false;
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            hudPosition = new Vector2((screen.x - (Screen.width - 1280 * scale) * .5f) / scale,
                (Screen.height - screen.y - (Screen.height - 720 * scale) * .5f) / scale);
            return true;
        }
    }
}
