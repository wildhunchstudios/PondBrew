using UnityEngine;

namespace EvolutionShop
{
    // UX-06: what the planning notebook (crew, automation, arrange, research) reads, and the commands it calls. Every
    // command is the session's existing one, with its presence checks, saves and refusals. The notebook's tabs only
    // switch between panels the player could open from where they stand, so no rule is bypassed.
    public sealed partial class ShopSession
    {
        ShopPanel planningReturn;

        /// <summary>Panels the planning notebook draws: the crew and its two pages, automation, arrange and research.</summary>
        public static bool IsPlanningPanel(ShopPanel panel)
        {
            switch (panel)
            {
                case ShopPanel.Staff: case ShopPanel.Stocker: case ShopPanel.StockerTargets:
                case ShopPanel.Technician: case ShopPanel.TechnicianOrders:
                case ShopPanel.Automation: case ShopPanel.Arrange: case ShopPanel.Research:
                    return true;
                default: return false;
            }
        }

        /// <summary>True while the UI Toolkit notebook draws the current planning panel, so its tabs and pages may switch to each other.</summary>
        bool ToolkitPlanning => ToolkitOwnsPanel(Panel) && IsPlanningPanel(Panel);

        // From SetPanel: remember the station card a planning view was opened from, so Esc can go back to it.
        void NotePlanningReturn(ShopPanel from, ShopPanel to)
        {
            if (IsPlanningPanel(to)) { if (from == ShopPanel.Counter || from == ShopPanel.Upgrade) planningReturn = from; }
            else planningReturn = ShopPanel.None;
        }

        /// <summary>
        /// Esc in the notebook backs out one level (UX.6): Nori's or Moss's page to the crew, a tab to the station card it
        /// was opened from (while the player still stands there), else back to play. False when the notebook isn't up.
        /// </summary>
        bool PlanningBack()
        {
            if (!ToolkitPlanning) return false;
            switch (Panel)
            {
                case ShopPanel.Stocker: case ShopPanel.StockerTargets: case ShopPanel.Technician: case ShopPanel.TechnicianOrders:
                    SetPanel(ShopPanel.Staff); return true;
            }
            var back = planningReturn;
            if (back == ShopPanel.Counter && IsAtCounter) SetPanel(ShopPanel.Counter);
            else if (back == ShopPanel.Upgrade && AtAutomationStation(StationKind.Upgrade)) SetPanel(ShopPanel.Upgrade);
            else SetPanel(ShopPanel.None);
            return true;
        }

        /// <summary>Whether a tab can be opened from where the player stands, by the same checks as its own panel.</summary>
        public bool PlanningTabAvailable(PlanningTab tab)
        {
            if (Paused || AutosaveBlocked) return false;
            switch (tab)
            {
                case PlanningTab.Crew: return IsAtCounter;
                case PlanningTab.Arrange: return CanArrange && layoutRoutesReady;
                default: return AtAutomationStation(StationKind.Upgrade);
            }
        }

        /// <summary>Switches the notebook to a tab through that tab's own Open command.</summary>
        public bool OpenPlanningTab(PlanningTab tab)
        {
            switch (tab)
            {
                case PlanningTab.Crew: return OpenStaffMenu();
                case PlanningTab.Automation: return OpenAutomationMenu();
                case PlanningTab.Arrange: return OpenPlacementMenu();
                default: return OpenResearchView();
            }
        }

        /// <summary>The research path (UX.6), from the research bench. It exists only in the UI Toolkit notebook.</summary>
        public bool OpenResearchView()
        {
            if (!ToolkitUiActive || (Panel != ShopPanel.Upgrade && !ToolkitPlanning) || !AtAutomationStation(StationKind.Upgrade))
            { Notify("Use E at the lab bench, then choose research."); return false; }
            SetPanel(ShopPanel.Research);
            return true;
        }

        /// <summary>Nori's display targets page, from the crew tab at the counter.</summary>
        public bool OpenStockerTargets()
        {
            if (!ToolkitPlanning || !IsAtCounter) { Notify("Use Staff at the physical front counter."); return false; }
            SetPanel(ShopPanel.StockerTargets);
            return true;
        }

        /// <summary>Moss's recipe queue page, from the crew tab at the counter.</summary>
        public bool OpenTechnicianQueue()
        {
            if (!ToolkitPlanning || !IsAtCounter) { Notify("Use Staff at the physical front counter."); return false; }
            SetPanel(ShopPanel.TechnicianOrders);
            return true;
        }

        /// <summary>The save error, else the current notice: the notebook's footer shows command feedback itself.</summary>
        /// <summary>The notebook footer's notice: a save error always; otherwise the current message, unless it was raised
        /// before <paramref name="since"/> (the page it belongs to was left) or is the generic "Done." acknowledgement,
        /// which the page itself already shows.</summary>
        public string PlanningNotice(out bool error, float since = float.NegativeInfinity)
        {
            error = !string.IsNullOrEmpty(SaveError);
            if (error) return SaveError;
            return !string.IsNullOrEmpty(message) && !messageGeneric && messageAt >= since && Time.unscaledTime < messageUntil ? message : null;
        }

        // ---------- crew ----------

        public void FillCrewView(CrewViewState s)
        {
            s.Clear();
            s.Night = Cycle.Phase == ShopDayPhase.Night;
            s.Trading = !s.Night;
            s.Currency = Model.Currency;
            s.Day = Cycle.DayNumber;
            int day = Cycle.DayNumber;
            FillCrewMember(s.Members[0], Staff, day);
            s.Members[0].Working = Cashier && Cashier.CurrentJobId != null;
            s.Members[0].HasWork = Customers != null && Customers.ActiveCount > 0;
            FillCrewMember(s.Members[1], Stocking.Employee, day);
            s.Members[1].Blocked = Stocker && Stocker.Blocked;
            s.Members[1].Working = Stocker && Stocker.CurrentJobId != null;
            bool targets = false;
            foreach (string tank in Model.TankIds) if (Stocking.GetTarget(tank) != null) { targets = true; break; }
            s.Members[1].HasWork = targets;
            FillCrewMember(s.Members[2], Laboratory.Employee, day);
            s.Members[2].Blocked = Technician && Technician.Blocked;
            s.Members[2].Working = Technician && Technician.CurrentJobId != null;
            s.Members[2].HasWork = Laboratory.Queue.Length > 0 || Laboratory.ActiveSpecimenId != null;
        }

        static void FillCrewMember(CrewMemberState m, ShopStaff employee, int day)
        {
            m.Hired = employee.Hired; m.Scheduled = employee.Scheduled; m.Paused = employee.Paused;
            m.PaidToday = employee.PaidDay == day;
            m.OnDuty = employee.IsOnDuty(day);
            m.Wage = employee.Wage;
        }

        /// <summary>Hires a crew member through that role's own command.</summary>
        public bool PlanHire(CrewRole role) => role == CrewRole.Cashier ? HireCashier() : role == CrewRole.Stocker ? HireStocker() : HireTechnician();
        public bool PlanSchedule(CrewRole role, bool on) => role == CrewRole.Cashier ? SetCashierScheduled(on) : role == CrewRole.Stocker ? SetStockerScheduled(on) : SetTechnicianScheduled(on);
        public bool PlanPay(CrewRole role) => role == CrewRole.Cashier ? PayCashierDuty() : role == CrewRole.Stocker ? PayStockerDuty() : PayTechnicianDuty();
        public bool PlanPause(CrewRole role, bool paused) => role == CrewRole.Cashier ? SetCashierPaused(paused) : role == CrewRole.Stocker ? SetStockerPaused(paused) : SetTechnicianPaused(paused);
        public bool PlanDismiss(CrewRole role) => role == CrewRole.Cashier ? DismissCashier() : role == CrewRole.Stocker ? DismissStocker() : DismissTechnician();
        public bool PlanRetry(CrewRole role) => role == CrewRole.Stocker ? RetryStocker() : role == CrewRole.Technician && RetryTechnician();

        public void FillStockerTargets(StockerTargetsState s)
        {
            s.Clear();
            foreach (string tank in Model.TankIds)
                s.Rows.Add(new TargetRow { TankId = tank, Name = World ? World.SlotName(tank) : tank, Target = Stocking.GetTarget(tank) });
            s.Options.Add(new TargetOption { Id = null });
            s.Options.Add(new TargetOption { Id = ShopStocking.AnySpecies });
            foreach (string id in Model.DiscoveredSpeciesIds)
            {
                var species = Model.GetSpecies(id);
                s.Options.Add(new TargetOption { Id = id, Name = species.DisplayName, Look = species.PresentationId });
            }
        }

        public void FillTechnicianQueue(TechnicianQueueState s)
        {
            s.Clear();
            s.MaxCount = ShopLaboratory.MaximumOrderCount;
            if (Laboratory.ActiveSpecimenId != null && Laboratory.ActiveSpeciesId != null)
            {
                var active = Model.GetSpecies(Laboratory.ActiveSpeciesId);
                s.ActiveName = active.DisplayName; s.ActiveLook = active.PresentationId;
            }
            var queue = Laboratory.Queue;
            foreach (var order in queue)
            {
                var species = Model.GetSpecies(order.speciesId);
                s.Rows.Add(new QueueRow { SpeciesId = order.speciesId, Name = species.DisplayName, Look = species.PresentationId, Count = order.count });
            }
            foreach (string id in Model.DiscoveredSpeciesIds)
            {
                int count = 0;
                foreach (var order in queue) if (order.speciesId == id) count = order.count;
                var species = Model.GetSpecies(id);
                s.Known.Add(new QueueRow { SpeciesId = id, Name = species.DisplayName, Look = species.PresentationId, Count = count });
            }
        }

        // ---------- automation ----------

        public void FillAutomationView(AutomationViewState s)
        {
            s.Clear();
            s.CollectorOwned = Automation.CollectorOwned; s.CollectorOn = Automation.CollectorEnabled;
            s.DoserOwned = Automation.DoserOwned; s.DoserOn = Automation.DoserEnabled;
            s.RecipeId = Automation.RecipeId;
            if (s.RecipeId != null) { var recipe = Model.GetSpecies(s.RecipeId); s.RecipeName = recipe.DisplayName; s.RecipeLook = recipe.PresentationId; }
            s.DestinationId = Automation.DestinationTankId;
            s.Desired = Automation.DesiredStockCount; s.MaxDesired = ShopAutomation.MaximumDesiredStockCount;
            s.Maintain = Automation.MaintainEnabled; s.Pending = Automation.IsPending; s.Paused = Automation.Paused;
            s.BlockedBySave = AutomationBlockedBySave;
            s.Status = AutomationStatus;
            foreach (var species in Config.Species)
            {
                if (!Model.IsDiscovered(species.Id)) continue;
                s.Rows.Add(new RecipeRow
                {
                    SpeciesId = species.Id, Name = species.DisplayName, Look = species.PresentationId,
                    Bloom = species.Bloom, Flow = species.Flow, Spark = species.Spark, Mist = species.Mist, Chosen = species.Id == s.RecipeId
                });
            }
            foreach (string tank in Model.TankIds)
                s.Destinations.Add(new DestinationOption { TankId = tank, Name = World ? World.SlotName(tank) : tank, Chosen = tank == s.DestinationId });
        }

        // ---------- arrange ----------

        public void FillArrangeView(ArrangeViewState s)
        {
            s.Clear();
            s.CanArrange = CanArrange && layoutRoutesReady;
            s.Currency = Model.Currency;
            s.StoreTier = Layout.StoreTier; s.LabTier = Layout.LabTier;
            s.Appeal = Appeal.Points;
            s.DecorCount = Layout.DecorCount; s.DecorLimit = ShopLayout.DecorLimit;
            s.RackCount = Layout.RackCount; s.RackLimit = Layout.RackLimit;
            s.DisplayLimit = Layout.DisplayLimit;
            s.PendingSaleId = pendingFurnitureSaleId;
            int tables = 0;
            foreach (var item in Layout.Items)
            {
                if (ShopLayout.DefinitionSlotCount(item.DefinitionId) > 0) s.DisplayCount++;
                else if (item.DefinitionId == ShopLayout.TableDefinitionId) tables++;
            }
            bool displaysFull = s.DisplayCount >= s.DisplayLimit;
            s.Furniture.Add(CatalogueTile(ShopLayout.SmallDisplayId, displaysFull, false));
            s.Furniture.Add(CatalogueTile(ShopLayout.LargeDisplayId, displaysFull, Layout.StoreTier == 0));
            s.Furniture.Add(CatalogueTile(ShopLayout.TableDefinitionId, tables >= ShopLayout.MaxItems, false));
            if (Layout.StoreTier < 2) s.Furniture.Add(ExpansionTile("store", Layout.NextExpansionPrice));

            bool decorFull = Layout.DecorCount >= ShopLayout.DecorLimit;
            foreach (string decor in new[] { ShopLayout.CoralPlanterId, ShopLayout.BubbleLampId, ShopLayout.SillyStatueId })
            {
                bool inStore = false;
                foreach (var item in Layout.Items)
                    if (!item.Stored && item.DefinitionId == decor && item.Z * ShopLayout.GridSize + .5f < .39f) { inStore = true; break; }
                var tile = CatalogueTile(decor, decorFull, false);
                tile.Appeal = PlanningRules.DecorAppeal(inStore);
                s.Decor.Add(tile);
            }

            if (Layout.LabTier < ShopLayout.MaxLabTier) s.Lab.Add(ExpansionTile("lab", Layout.NextLabExpansionPrice));
            s.Lab.Add(CatalogueTile(ShopLayout.RackDefinitionId, Layout.RackCount >= Layout.RackLimit, false));
            if (Layout.ConditionerCount == 0)
                s.Lab.Add(CatalogueTile(ShopLayout.ConditionerDefinitionId, false, !Model.SparkResearchUnlocked || !Model.IsDiscovered("lanternfin")));
            foreach (var facility in Layout.Facilities)
                s.Lab.Add(new ArrangeTile { Id = facility.Id, Name = ShopLayout.FacilityName(facility.Id), Look = facility.Id, Move = true });

            foreach (var item in Layout.Items)
            {
                string number = item.Id.Replace("table-", "");
                s.Owned.Add(new OwnedRow
                {
                    ItemId = item.Id, DefinitionId = item.DefinitionId, Name = ShopLayout.DefinitionName(item.DefinitionId), Label = number,
                    Place = item.Stored ? OwnedPlace.Storage : item.Z < 1 ? OwnedPlace.Store : OwnedPlace.Lab,
                    Stocked = DisplayHasStock(item.Id), Reserved = DisplayHasReservation(item.Id),
                    Resale = ShopLayout.DefinitionResalePrice(item.DefinitionId)
                });
            }
        }

        ArrangeTile CatalogueTile(string definition, bool atLimit, bool locked)
        {
            int price = ShopLayout.DefinitionPrice(definition);
            string blocked = PlanningRules.TileBlockedKey(atLimit, locked, Model.Currency, price, out int amount);
            return new ArrangeTile
            {
                Id = definition, Name = ShopLayout.DefinitionName(definition), Look = definition, Price = price,
                Slots = ShopLayout.DefinitionSlotCount(definition), BlockedKey = blocked, Amount = amount
            };
        }

        ArrangeTile ExpansionTile(string which, int price)
        {
            string blocked = PlanningRules.TileBlockedKey(false, false, Model.Currency, price, out int amount);
            return new ArrangeTile { Id = "expand-" + which, Look = "expand-" + which, Price = price, Expansion = true, BlockedKey = blocked, Amount = amount };
        }

        /// <summary>Buys, places or moves what a tile stands for, through the Arrange panel's own commands.</summary>
        public bool PlanTile(ArrangeTile tile)
        {
            if (tile.Expansion) return tile.Id == "expand-lab" ? BuyLabExpansion() : BuyStoreExpansion();
            if (tile.Move) return BeginFacilityPlacement(tile.Id);
            return BeginPlacement(null, tile.Id);
        }

        public bool PlanMoveOwned(string itemId) => BeginPlacement(itemId);

        /// <summary>What the placement key bar says while the notebook steps aside for placing (UX.6): false when not placing.</summary>
        public bool PlanPlacement(ShopUiStrings strings, out string title, out string detail, out bool valid)
        {
            title = detail = null; valid = false;
            if (!PlacementActive) return false;
            string name = movingFacilityId != null ? ShopLayout.FacilityName(movingFacilityId) : ShopLayout.DefinitionName(placementDefinition);
            bool moving = movingFacilityId != null || movingTableId != null;
            title = moving ? strings.Format("plan.place.move", name) : strings.Format("plan.place.buy", name, PlacementCost);
            detail = PlacementMessage;
            valid = PlacementValid;
            return true;
        }

        // ---------- research ----------

        public void FillResearchView(ResearchViewState s)
        {
            s.Clear();
            bool night = Cycle.Phase == ShopDayPhase.Night;
            bool bench = AtAutomationStation(StationKind.Upgrade);
            s.Currency = Model.Currency;
            s.Steps[0].Unlocked = s.Steps[1].Unlocked = true;
            var spark = s.Steps[2];
            spark.Unlocked = Model.SparkResearchUnlocked; spark.Cost = Config.SparkResearchCost; spark.Here = bench;
            foreach (string id in new[] { "pondlet", "bubblebelly", "ribbonfin" })
            {
                var species = Model.GetSpecies(id);
                if (species != null) spark.Requirements.Add(new BenchRequirement { Name = species.DisplayName, Done = Model.IsDiscovered(id) });
            }
            spark.BlockedKey = StationCardRules.SparkBlockedKey(Model.SparkResearchReady, night, Model.Currency, spark.Cost);
            spark.Amount = Mathf.Max(0, spark.Cost - Model.Currency);
            var mist = s.Steps[3];
            mist.Unlocked = Model.MistResearchUnlocked; mist.Cost = Config.MistResearchCost;
            var lantern = Model.GetSpecies("lanternfin");
            if (lantern != null) mist.Requirements.Add(new BenchRequirement { Name = lantern.DisplayName, Done = Model.IsDiscovered("lanternfin") });
            // Mist is researched at the conditioning vat (ResearchMist): the path names it, the vat's card buys it.
            mist.BlockedKey = "plan.research.atVat";
            s.SiphonOwned = Model.UpgradeOwned; s.SiphonCost = Config.UpgradeCost;
            s.SiphonFrom = Config.BaseCapacity; s.SiphonTo = Config.UpgradedCapacity;
            s.SiphonCanBuy = !s.SiphonOwned && Model.Currency >= s.SiphonCost;
            s.SiphonBlockedKey = s.SiphonOwned || s.SiphonCanBuy ? null : "plan.arrange.why.coins";
        }

        public bool PlanResearchSpark() => ResearchSpark();
        public bool PlanBuySiphon() => BuySiphonUpgrade().Success;
    }
}
