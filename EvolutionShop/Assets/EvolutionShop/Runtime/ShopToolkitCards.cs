using UnityEngine;

namespace EvolutionShop
{
    // UX-04: what the incubator, display-tank and counter cards read, and the commands they call. Every command
    // is the session's existing one (the IMGUI panels call the same), with its own presence checks and saves.
    public sealed partial class ShopSession
    {
        static readonly CultureKind[] CardCultures = { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark, CultureKind.Mist };
        readonly int[] cardAvailable = new int[4], cardStaged = new int[4], cardRecipe = new int[4];

        public void FillIncubatorCard(IncubatorCardState s, ShopUiStrings strings)
        {
            s.Clear();
            var inc = Model.Incubator;
            s.Duration = Config.IncubationSeconds;
            s.Mode = inc.Phase == IncubatorPhase.Incubating ? IncubatorCardMode.Growing : inc.Phase == IncubatorPhase.Ready ? IncubatorCardMode.Ready : IncubatorCardMode.Dosing;
            if (!string.IsNullOrEmpty(inc.SpeciesId)) { var grown = Model.GetSpecies(inc.SpeciesId); s.SpeciesName = grown.DisplayName; s.SpeciesLook = grown.PresentationId; }
            s.SecondsLeft = inc.SecondsRemaining;
            if (Model.IsIncubatorReservedForTechnician) s.Busy = strings.Get("card.inc.busyMoss");
            else if (Automation.IsPending) { s.Busy = strings.Format("card.inc.busyHelper", AutomationStatus); s.CanCancelHelper = true; }
            int capacity = BaselinePreview ? Config.BaseCapacity : Model.Capacity;
            s.SiphonFull = Model.ReservedCultureCount >= capacity;
            for (int i = 0; i < 4; i++)
            {
                var kind = CardCultures[i];
                cardAvailable[i] = Model.Available(kind); cardStaged[i] = Model.Staged(kind);
                s.StagedTotal += cardStaged[i];
                bool unlocked = kind == CultureKind.Bloom || kind == CultureKind.Flow ||
                    kind == CultureKind.Spark && Model.SparkResearchUnlocked || kind == CultureKind.Mist && Model.MistResearchUnlocked;
                if (unlocked) s.Chips.Add(new CultureChip { Kind = kind, Staged = cardStaged[i], InSiphon = cardAvailable[i] });
            }
            foreach (var species in Config.Species)
            {
                if (!Model.IsDiscovered(species.Id)) continue;
                cardRecipe[0] = species.Bloom; cardRecipe[1] = species.Flow; cardRecipe[2] = species.Spark; cardRecipe[3] = species.Mist;
                s.Recipes.Add(new RecipeTile
                {
                    SpeciesId = species.Id, Name = species.DisplayName, Look = species.PresentationId,
                    Bloom = species.Bloom, Flow = species.Flow, Spark = species.Spark, Mist = species.Mist,
                    Missing = StationCardRules.MissingCulture(cardAvailable, cardStaged, cardRecipe)
                });
            }
        }

        public void FillTankCard(TankCardState s, ShopUiStrings strings)
        {
            s.Clear();
            s.Title = World ? World.SlotName(selectedTank) : selectedTank;
            var resident = Model.GetTank(selectedTank);
            if (Model.Incubator.Phase == IncubatorPhase.Ready && !string.IsNullOrEmpty(Model.Incubator.SpeciesId))
                s.IncubatorReady = Model.GetSpecies(Model.Incubator.SpeciesId).DisplayName;
            if (!resident.HasValue)
            {
                s.Empty = true;
                s.StockerDelivering = Model.IsTankReservedForStocking(selectedTank);
                if (s.IncubatorReady != null)
                    s.Sources.Add(new StockTile { Source = StockSource.Incubator, Name = s.IncubatorReady, Look = Model.GetSpecies(Model.Incubator.SpeciesId).PresentationId, Where = strings.Get("card.tank.fromIncubator"), Enabled = true });
                foreach (string id in Model.StorageIds)
                {
                    var stored = Model.GetStorage(id);
                    if (!stored.HasValue) continue;
                    s.Sources.Add(new StockTile
                    {
                        Source = StockSource.Rack, Name = Model.GetSpecies(stored.Value.SpeciesId).DisplayName, Look = Model.GetSpecies(stored.Value.SpeciesId).PresentationId, Where = CardRackName(id),
                        StorageId = id, ExpectedId = stored.Value.Id, Enabled = !Model.IsStorageReservedForStocking(id) && !s.StockerDelivering
                    });
                }
                AddVatSources(s.Sources, strings);
                return;
            }
            var species = Model.GetSpecies(resident.Value.SpeciesId);
            s.SpeciesId = species.Id; s.SpeciesName = species.DisplayName; s.ResidentId = resident.Value.Id;
            var tag = Model.GetPrice(species.Id).Value;
            s.Price = tag.Price; s.Standard = tag.Standard; s.Minimum = tag.Minimum; s.Maximum = tag.Maximum;
            var reservation = Model.GetReservation(selectedTank);
            s.Reserved = reservation.HasValue;
            if (s.Reserved) s.ReservedPrice = reservation.Value.Price;
            s.Buyers = Customers.BuyerTypes(tag); s.Types = Customers.ArrivingTypes;
            foreach (var type in Customers.Archetypes)
            {
                if (type.Weight <= 0) continue;
                var reaction = Customers.Preview(type, tag);
                s.Faces.Add(new BuyerFace
                {
                    ArchetypeId = type.Id, Name = type.DisplayName, Reaction = reaction, Budget = type.Budget,
                    OverBudget = reaction == PriceReaction.TooExpensive && type.OverBudget(tag.Price), Collects = type.Likes(species.Id)
                });
            }
            if (!s.Reserved)
                foreach (string id in Model.StorageIds)
                    if (!Model.GetStorage(id).HasValue) { s.ReturnStorageId = id; s.ReturnWhere = CardRackName(id); break; }
        }

        // "Rack #1 / lower" without the " / " separator the copy rules retire (UX.7).
        string CardRackName(string storageId) => StorageLocation(storageId).Replace(" / ", " · ").Replace("#", "");

        public void FillCounterCard(CounterCardState s)
        {
            s.Clear();
            s.Phase = Cycle.Phase == ShopDayPhase.Night ? HudDayPhase.Night : Cycle.Phase == ShopDayPhase.Open ? HudDayPhase.Open : HudDayPhase.Closing;
            s.Day = Cycle.DayNumber;
            s.SecondsLeft = Cycle.SecondsRemaining;
            s.TradingSeconds = ShopDayCycle.DefaultTradingSeconds;
            s.AtCounter = IsAtCounter;
            var next = Customers ? Customers.NextToServe : null;
            s.CanServe = next != null && next.Phase == CustomerPhase.Queued && IsAtCounter && !Paused;
            if (next != null) s.ServePrice = next.Price;
            if (Customers)
                foreach (var visit in Customers.Visits)
                {
                    if (visit.Phase == CustomerPhase.Queued || visit.Phase == CustomerPhase.ToCounter)
                    {
                        var species = string.IsNullOrEmpty(visit.SpeciesId) ? null : Model.GetSpecies(visit.SpeciesId);
                        s.Rows.Add(new OrderRow
                        {
                            VisitId = visit.Id, ArchetypeId = visit.ArchetypeId, Customer = visit.ArchetypeName, Species = species != null ? species.DisplayName : "",
                            Price = visit.Price, AtCounter = visit.Phase == CustomerPhase.Queued, SecondsLeft = visit.QueueSecondsRemaining,
                            PatienceSeconds = hudPatience.TryGetValue(visit.Id, out float p) ? p : visit.Archetype.PatienceSeconds
                        });
                    }
                    else if (visit.Phase == CustomerPhase.Browsing || visit.Phase == CustomerPhase.Arriving) s.Browsing++;
                }
            foreach (var id in Model.TankIds) { s.Displays++; if (!Model.GetTank(id).HasValue) s.EmptyDisplays++; }
            var report = LastReportForDisplay();
            s.HasReport = report != null; if (report != null) s.ReportDay = report.dayNumber;
            s.ShowNextGoal = Tutorial.Mode == TutorialMode.Active && ActiveTutorialLesson == TutorialLesson.NextPlan;
            if (Staff.Hired) s.Cashier = Cashier.Status;
        }

        /// <summary>Each ready vat chamber as a source to stock a display or store in a rack.</summary>
        void AddVatSources(System.Collections.Generic.List<StockTile> sources, ShopUiStrings strings)
        {
            foreach (string id in Model.ConditionerIds)
            {
                var vat = Model.GetConditioner(id);
                if (!vat.HasValue || vat.Value.Phase != ConditionerPhase.Ready) continue;
                for (int i = 0; i < 2; i++)
                {
                    var fish = i == 0 ? vat.Value.First : vat.Value.Second;
                    if (!fish.HasValue) continue;
                    var species = Model.GetSpecies(fish.Value.SpeciesId);
                    sources.Add(new StockTile
                    {
                        Source = StockSource.Vat, Name = species.DisplayName, Look = species.PresentationId, Where = strings.Format("card.tank.fromVat", i + 1),
                        MachineId = id, MachineSlot = i, ExpectedId = fish.Value.Id, Enabled = true
                    });
                }
            }
        }

        // ---------- UX-05: specimen rack, conditioning vat, research bench ----------

        public void FillRackCard(RackCardState s, ShopUiStrings strings)
        {
            s.Clear();
            var item = selectedRack == null ? null : Layout.GetItem(selectedRack);
            s.Placed = item.HasValue && !item.Value.Stored && item.Value.DefinitionId == ShopLayout.RackDefinitionId;
            if (!s.Placed) return;
            var ids = Layout.GetStorageSlotIds(selectedRack);
            for (int i = 0; i < ids.Length; i++)
            {
                var stored = Model.GetStorage(ids[i]);
                var species = stored.HasValue ? Model.GetSpecies(stored.Value.SpeciesId) : null;
                s.Compartments.Add(new RackCompartment
                {
                    StorageId = ids[i], Where = strings.Get(i == 0 ? "card.rack.lower" : "card.rack.upper"),
                    SpeciesName = species != null ? species.DisplayName : null, Look = species != null ? species.PresentationId : null,
                    Reserved = Model.IsStorageReservedForStocking(ids[i])
                });
            }
            if (Model.Incubator.Phase == IncubatorPhase.Ready && !string.IsNullOrEmpty(Model.Incubator.SpeciesId))
            {
                var ready = Model.GetSpecies(Model.Incubator.SpeciesId);
                s.Sources.Add(new StockTile { Source = StockSource.Incubator, Name = ready.DisplayName, Look = ready.PresentationId, Where = strings.Get("card.tank.fromIncubator"), Enabled = true });
            }
            AddVatSources(s.Sources, strings);
        }

        public void FillVatCard(VatCardState s, ShopUiStrings strings)
        {
            s.Clear();
            s.Duration = Config.ConditioningSeconds;
            s.Night = Cycle.Phase == ShopDayPhase.Night;
            s.Currency = Model.Currency;
            s.MistUnlocked = Model.MistResearchUnlocked; s.MistReady = Model.MistResearchReady; s.MistCost = Config.MistResearchCost;
            s.SparkInSiphon = Model.Available(CultureKind.Spark);
            var state = selectedConditioner == null ? null : Model.GetConditioner(selectedConditioner);
            if (!state.HasValue) { s.Mode = VatCardMode.Missing; return; }
            var vat = state.Value;
            foreach (string id in Model.StorageIds) if (!Model.GetStorage(id).HasValue) s.FreeSpace = true;
            foreach (string id in Model.TankIds) if (!Model.GetTank(id).HasValue) s.FreeSpace = true;
            if (vat.Phase != ConditionerPhase.Idle)
            {
                s.Mode = vat.Phase == ConditionerPhase.Processing ? VatCardMode.Processing : VatCardMode.Ready;
                s.SecondsLeft = vat.SecondsRemaining;
                for (int i = 0; i < 2; i++)
                {
                    var fish = i == 0 ? vat.First : vat.Second;
                    if (!fish.HasValue) continue;
                    var species = Model.GetSpecies(fish.Value.SpeciesId);
                    s.Outputs.Add(new VatOutput { Chamber = i + 1, Name = species.DisplayName, Look = species.PresentationId });
                }
                return;
            }
            s.Mode = Model.SparkResearchUnlocked && Model.IsDiscovered("lanternfin") ? VatCardMode.Idle : VatCardMode.Locked;
            if (s.Mode != VatCardMode.Idle) return;
            foreach (string id in Model.StorageIds)
            {
                var fish = Model.GetStorage(id);
                if (!fish.HasValue || fish.Value.SpeciesId != "pondlet") continue;
                bool selected = conditioningInputs.TryGetValue(id, out string expected) && expected == fish.Value.Id;
                if (selected) s.SelectedCount++;
                s.Inputs.Add(new VatInput { StorageId = id, ExpectedId = fish.Value.Id, Where = CardRackName(id), Selected = selected });
            }
        }

        public void FillBenchCard(BenchCardState s)
        {
            s.Clear();
            s.SparkUnlocked = Model.SparkResearchUnlocked; s.SparkReady = Model.SparkResearchReady; s.SparkCost = Config.SparkResearchCost;
            s.Night = Cycle.Phase == ShopDayPhase.Night;
            s.Currency = Model.Currency;
            s.UpgradeOwned = Model.UpgradeOwned; s.UpgradeCost = Config.UpgradeCost;
            s.BaseCapacity = Config.BaseCapacity; s.UpgradedCapacity = Config.UpgradedCapacity;
            foreach (string id in new[] { "pondlet", "bubblebelly", "ribbonfin" })
            {
                var species = Model.GetSpecies(id);
                if (species != null) s.Requirements.Add(new BenchRequirement { Name = species.DisplayName, Done = Model.IsDiscovered(id) });
            }
            s.Collector = BenchModuleOf(true);
            s.Doser = BenchModuleOf(false);
        }

        BenchModule BenchModuleOf(bool collector)
        {
            int cost = collector ? ShopAutomation.CollectorCost : ShopAutomation.DoserCost;
            string blocked = StationCardRules.ModuleBlockedKey(Model.UpgradeOwned, Model.IsDiscovered("pondlet"), Cycle.Phase == ShopDayPhase.Night,
                Customers ? Customers.ActiveCount : 0, Model.Currency, cost);
            return new BenchModule
            {
                Owned = collector ? Automation.CollectorOwned : Automation.DoserOwned,
                Enabled = collector ? Automation.CollectorEnabled : Automation.DoserEnabled,
                Cost = cost, CanInstall = blocked == null, BlockedKey = blocked, Missing = Mathf.Max(0, cost - Model.Currency)
            };
        }

        // Card commands: each is the existing session command; the card shows its result through the state above.
        public bool CardDose(CultureKind kind) => DoseCulture(kind);
        public bool CardReturnDose(CultureKind kind) => ReturnCultureDose(kind);
        public bool CardReturnAllDoses() => ClearCultureDoses();
        public bool CardRelease(CultureKind kind) => ReleaseCulture(kind);
        public bool CardRecipe(string speciesId) => PrepareKnownRecipe(speciesId);
        public bool CardStart() => BeginManualIncubation();
        public bool CardCancelHelper() => CancelAutomationBatch();
        public bool CardSetPrice(string speciesId, int price) => SetSpeciesPrice(speciesId, price).Success;
        public bool CardStock(StockTile tile)
        {
            switch (tile.Source)
            {
                case StockSource.Incubator: StockDisplayFromIncubator(); return Model.GetTank(selectedTank).HasValue;
                case StockSource.Rack: return StockDisplayFromStorage(tile.StorageId, tile.ExpectedId);
                default: return StockDisplayFromConditioner(tile.MachineId, tile.MachineSlot, tile.ExpectedId);
            }
        }
        public bool CardReturnToRack(string storageId, string expectedId) => ReturnDisplayToStorage(storageId, expectedId);
        public bool CardOpenShop() => OpenForDay();
        public bool CardCloseEarly() => CloseForNight();
        public bool CardServe() => ServeCustomer();
        public bool CardCrew() => OpenStaffMenu();
        public bool CardArrange() => OpenPlacementMenu();
        public bool CardReport() => ShowDayReport();
        public bool CardNextGoal() => ShowTutorialHandoff();
        public bool CardStoreInRack(StockTile source, string storageId) => source.Source == StockSource.Incubator
            ? StoreReadyInRack(storageId) : StoreConditionerInRack(source.MachineId, source.MachineSlot, storageId, source.ExpectedId);
        public bool CardToggleVatInput(VatInput input) => ToggleConditioningInput(input.StorageId, input.ExpectedId);
        public bool CardStartVat() => BeginSelectedConditioning();
        public bool CardCancelVat() => CancelSelectedConditioning();
        public bool CardResearchMist() => ResearchMist();
        public bool CardResearchSpark() => ResearchSpark();
        public bool CardBuySiphon() => BuySiphonUpgrade().Success;
        public bool CardInstallModule(bool collector) => PurchaseAutomationModule(collector);
        public bool CardSwitchModule(bool collector, bool on) => SetAutomationEnabled(collector, on);
        public bool CardCulturePrep() => OpenAutomationMenu();
    }
}
