using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>
    /// Single authority for prototype transactions. Time advances only through Tick.
    /// Staged doses reserve their return space until incubation starts, so invalid
    /// mixtures can always be recovered without losing a culture unit.
    /// </summary>
    public sealed partial class GameModel
    {
        /// <summary>Version 6 adds the Mist culture and Airfin family; earlier saves remain exact.</summary>
        public const int SaveVersion = 6;
        private const int CultureCount = 4;

        private readonly GameConfig config;
        private readonly Dictionary<string, SpeciesDefinition> species;
        private Dictionary<string, SpecimenView?> tanks;
        private Dictionary<string, SpecimenView?> storage;
        private readonly Dictionary<string, ReservationView> reservations = new Dictionary<string, ReservationView>(StringComparer.Ordinal);
        private readonly HashSet<string> discoveries = new HashSet<string>(StringComparer.Ordinal);
        // Only tags that differ from the standard price are kept, so a save stays canonical.
        private readonly Dictionary<string, int> prices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly int[] available = new int[CultureCount];
        private readonly int[] staged = new int[CultureCount];
        private readonly int[] growerUnits = new int[CultureCount];
        private readonly float[] growerRemaining = new float[CultureCount];
        private IncubatorPhase phase;
        private string activeSpecies;
        private string activeSpecimen;
        private float incubationRemaining;

        public int Currency { get; private set; }
        public string[] TankIds => (string[])config.TankIds.Clone();
        public string[] StorageIds => (string[])config.StorageIds.Clone();
        public bool UpgradeOwned { get; private set; }
        public bool SparkResearchUnlocked { get; private set; }
        public bool MistResearchUnlocked { get; private set; }
        public bool SparkResearchReady => discoveries.Contains("pondlet") && discoveries.Contains("bubblebelly") && discoveries.Contains("ribbonfin");
        public bool MistResearchReady => SparkResearchUnlocked && discoveries.Contains("lanternfin") && config.ConditionerIds.Length > 0;
        public bool IsCultureUnlocked(CultureKind culture) => ValidCulture((int)culture) &&
            (culture != CultureKind.Spark || SparkResearchUnlocked) && (culture != CultureKind.Mist || MistResearchUnlocked);
        public int Capacity { get { return UpgradeOwned ? config.UpgradedCapacity : config.BaseCapacity; } }
        public float CollectionSecondsPerUnit
        {
            get { return UpgradeOwned ? config.UpgradedCollectionSeconds : config.BaseCollectionSeconds; }
        }
        public int ReservedCultureCount
        {
            get { return available[0] + available[1] + available[2] + available[3] + staged[0] + staged[1] + staged[2] + staged[3] + ReservedConditioningSpark; }
        }
        public IncubatorView Incubator
        {
            get { return new IncubatorView(phase, staged[0], staged[1], activeSpecies, activeSpecimen, incubationRemaining, staged[2], staged[3]); }
        }
        public IReadOnlyList<string> DiscoveredSpeciesIds
        {
            get
            {
                var ids = new List<string>(discoveries);
                ids.Sort(StringComparer.Ordinal);
                return ids.AsReadOnly();
            }
        }

        public GameModel(GameConfig config)
        {
            this.config = CopyAndValidate(config);
            species = new Dictionary<string, SpeciesDefinition>(StringComparer.Ordinal);
            foreach (var definition in this.config.Species) species.Add(definition.Id, definition);
            tanks = new Dictionary<string, SpecimenView?>(StringComparer.Ordinal);
            foreach (var id in this.config.TankIds) tanks.Add(id, null);
            storage = new Dictionary<string, SpecimenView?>(StringComparer.Ordinal);
            foreach (var id in this.config.StorageIds) storage.Add(id, null);
            foreach (var id in this.config.ConditionerIds) conditioners.Add(id, new ConditionerState());
            for (int i = 0; i < CultureCount; i++)
            {
                growerUnits[i] = i >= (int)CultureKind.Spark ? 0 : this.config.InitialGrowerUnits;
                growerRemaining[i] = this.config.GrowerRefillSeconds;
            }
            phase = IncubatorPhase.Dosing;
        }

        public int Available(CultureKind culture) { return available[CultureIndexOrThrow(culture)]; }
        public int Staged(CultureKind culture) { return staged[CultureIndexOrThrow(culture)]; }
        public GrowerView GetGrower(CultureKind culture)
        {
            int i = CultureIndexOrThrow(culture);
            return new GrowerView(culture, growerUnits[i], config.GrowerCapacity, growerRemaining[i]);
        }
        public SpecimenView? GetTank(string tankId)
        {
            return tankId != null && tanks.TryGetValue(tankId, out var value) ? value : null;
        }
        public ReservationView? GetReservation(string tankId)
        {
            return tankId != null && reservations.TryGetValue(tankId, out var value) ? value : (ReservationView?)null;
        }
        public SpecimenView? GetStorage(string storageId)
        {
            return storageId != null && storage.TryGetValue(storageId, out var value) ? value : null;
        }
        public SpeciesDefinition GetSpecies(string speciesId)
        {
            return speciesId != null && species.TryGetValue(speciesId, out var value) ? value.Copy() : null;
        }
        public bool IsDiscovered(string speciesId) { return speciesId != null && discoveries.Contains(speciesId); }

        /// <summary>The species' current price tag, its standard price and the allowed range.</summary>
        public PriceView? GetPrice(string speciesId)
        {
            if (speciesId == null || !species.TryGetValue(speciesId, out var definition)) return null;
            int standard = definition.SaleValue;
            return new PriceView(speciesId, CurrentPrice(speciesId), standard, config.MinimumPrice,
                ShopPricing.MaximumPrice(standard, config.MinimumPrice, config.MaximumPricePercent));
        }

        /// <summary>
        /// Tags every displayed specimen of a discovered species. Reservations keep the price agreed
        /// when they were made; the new tag applies to the next customer.
        /// </summary>
        public ActionResult SetPrice(string speciesId, int price)
        {
            var view = GetPrice(speciesId);
            if (!view.HasValue) return ActionResult.Fail(FailureCode.UnknownSpecies);
            if (!discoveries.Contains(speciesId)) return ActionResult.Fail(FailureCode.UndiscoveredRecipe);
            if (price < view.Value.Minimum || price > view.Value.Maximum) return ActionResult.Fail(FailureCode.InvalidPrice);
            if (price == view.Value.Standard) prices.Remove(speciesId);
            else prices[speciesId] = price;
            return ActionResult.Ok(speciesId);
        }

        public ActionResult Collect(CultureKind culture)
        {
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (!IsCultureUnlocked(culture)) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (ReservedCultureCount >= Capacity) return ActionResult.Fail(FailureCode.ReservoirFull);
            if (growerUnits[i] <= 0) return ActionResult.Fail(FailureCode.GrowerEmpty);
            growerUnits[i]--;
            available[i]++;
            return ActionResult.Ok();
        }

        /// <summary>
        /// Deliberately releases one carried culture to free reservoir space.
        /// This never touches staged ingredients, active production, or grower stock.
        /// </summary>
        public ActionResult ReleaseCulture(CultureKind culture)
        {
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (!IsCultureUnlocked(culture)) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (available[i] <= 0) return ActionResult.Fail(FailureCode.InsufficientCulture);
            if (available[i] <= TechnicianReservedCulture(i)) return ActionResult.Fail(FailureCode.TechnicianReserved);
            available[i]--;
            return ActionResult.Ok();
        }
        public ActionResult Dose(CultureKind culture)
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (!IsCultureUnlocked(culture)) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (available[i] <= 0) return ActionResult.Fail(FailureCode.InsufficientCulture);
            available[i]--;
            staged[i]++;
            return ActionResult.Ok();
        }

        public ActionResult ReturnDose(CultureKind culture)
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (!IsCultureUnlocked(culture)) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (staged[i] <= 0) return ActionResult.Fail(FailureCode.AlreadyEmpty);
            staged[i]--;
            available[i]++;
            return ActionResult.Ok();
        }

        public ActionResult ClearDoses()
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (staged[0] + staged[1] + staged[2] + staged[3] == 0) return ActionResult.Fail(FailureCode.AlreadyEmpty);
            for (int i = 0; i < CultureCount; i++) { available[i] += staged[i]; staged[i] = 0; }
            return ActionResult.Ok();
        }

        public ActionResult UseKnownRecipe(string speciesId)
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            return UseKnownRecipeCore(speciesId);
        }

        private ActionResult UseKnownRecipeCore(string speciesId)
        {
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (speciesId == null || !species.TryGetValue(speciesId, out var recipe))
                return ActionResult.Fail(FailureCode.UnknownSpecies);
            if (recipe.Spark > 0 && !SparkResearchUnlocked || recipe.Mist > 0 && !MistResearchUnlocked) return ActionResult.Fail(FailureCode.ResearchLocked);
            if (!discoveries.Contains(speciesId)) return ActionResult.Fail(FailureCode.UndiscoveredRecipe);
            int bloom = available[0] + staged[0];
            int flow = available[1] + staged[1];
            int spark = available[2] + staged[2];
            int mist = available[3] + staged[3];
            if (bloom < recipe.Bloom || flow < recipe.Flow || spark < recipe.Spark || mist < recipe.Mist)
                return ActionResult.Fail(FailureCode.InsufficientCulture);
            available[0] = bloom - recipe.Bloom;
            available[1] = flow - recipe.Flow;
            available[2] = spark - recipe.Spark;
            available[3] = mist - recipe.Mist;
            staged[0] = recipe.Bloom;
            staged[1] = recipe.Flow;
            staged[2] = recipe.Spark;
            staged[3] = recipe.Mist;
            return ActionResult.Ok(speciesId);
        }

        public ActionResult BeginIncubation()
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            SpeciesDefinition match = null;
            foreach (var definition in config.Species)
                if (definition.Bloom == staged[0] && definition.Flow == staged[1] && definition.Spark == staged[2] && definition.Mist == staged[3]) { match = definition; break; }
            if (match == null) return ActionResult.Fail(FailureCode.InvalidRecipe);
            if (match.Spark > 0 && !SparkResearchUnlocked || match.Mist > 0 && !MistResearchUnlocked) return ActionResult.Fail(FailureCode.ResearchLocked);
            return StartIncubation(match, Guid.NewGuid().ToString("N"));
        }

        // Both callers validate all inputs before reaching this single production commit.
        // The technician supplies a stable ID so a saved staging clone and the live model agree.
        private ActionResult StartIncubation(SpeciesDefinition match, string specimenId)
        {
            activeSpecies = match.Id;
            activeSpecimen = specimenId;
            staged[0] = staged[1] = staged[2] = staged[3] = 0;
            incubationRemaining = config.IncubationSeconds;
            phase = IncubatorPhase.Incubating;
            return ActionResult.Ok(activeSpecies, activeSpecimen);
        }

        public TickResult Tick(float simulationSeconds)
        {
            if (!Finite(simulationSeconds) || simulationSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
            for (int i = 0; i < CultureCount; i++)
            {
                if (!IsCultureUnlocked((CultureKind)i)) continue;
                if (growerUnits[i] >= config.GrowerCapacity)
                {
                    growerRemaining[i] = config.GrowerRefillSeconds;
                    continue;
                }
                double remaining = (double)growerRemaining[i] - simulationSeconds;
                if (remaining <= 0)
                {
                    int missing = config.GrowerCapacity - growerUnits[i];
                    int produced = (int)Math.Min(missing, Math.Floor(-remaining / config.GrowerRefillSeconds) + 1);
                    growerUnits[i] += produced;
                    remaining = growerUnits[i] >= config.GrowerCapacity
                        ? config.GrowerRefillSeconds : remaining + produced * (double)config.GrowerRefillSeconds;
                }
                growerRemaining[i] = (float)remaining;
            }

            int conditioned = TickConditioners(simulationSeconds);
            if (phase != IncubatorPhase.Incubating) return new TickResult(false, false, null, null, conditioned);
            incubationRemaining = Math.Max(0f, incubationRemaining - simulationSeconds);
            if (incubationRemaining > 0f) return new TickResult(false, false, null, null, conditioned);
            phase = IncubatorPhase.Ready;
            bool first = discoveries.Add(activeSpecies);
            return new TickResult(true, first, activeSpecies, activeSpecimen, conditioned);
        }

        public ActionResult TransferToTank(string tankId)
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (tankId == null || !tanks.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (phase != IncubatorPhase.Ready) return ActionResult.Fail(FailureCode.OutputNotReady);
            if (IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (tanks[tankId].HasValue) return ActionResult.Fail(FailureCode.TankFull);
            var specimen = new SpecimenView(activeSpecimen, activeSpecies);
            tanks[tankId] = specimen;
            phase = IncubatorPhase.Dosing;
            activeSpecies = activeSpecimen = null;
            incubationRemaining = 0f;
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
        }

        public ActionResult StoreReadyOutput(string storageId)
        {
            if (IsIncubatorReservedForTechnician) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (storageId == null || !storage.ContainsKey(storageId)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (phase != IncubatorPhase.Ready) return ActionResult.Fail(FailureCode.OutputNotReady);
            if (IsStorageReservedForTechnician(storageId)) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (IsStorageReservedForStocking(storageId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (storage[storageId].HasValue) return ActionResult.Fail(FailureCode.StorageFull);
            var specimen = new SpecimenView(activeSpecimen, activeSpecies);
            storage[storageId] = specimen;
            phase = IncubatorPhase.Dosing;
            activeSpecies = activeSpecimen = null;
            incubationRemaining = 0f;
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
        }

        public ActionResult TransferStorageToTank(string storageId, string tankId, string expectedSpecimenId)
        {
            if (storageId == null || !storage.TryGetValue(storageId, out var source)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (tankId == null || !tanks.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (reservations.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.TankReserved);
            if (IsStorageReservedForTechnician(storageId)) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (IsStorageReservedForStocking(storageId) || IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (!source.HasValue) return ActionResult.Fail(FailureCode.StorageEmpty);
            if (source.Value.Id != expectedSpecimenId) return ActionResult.Fail(FailureCode.SpecimenMismatch);
            if (tanks[tankId].HasValue) return ActionResult.Fail(FailureCode.TankFull);
            tanks[tankId] = source;
            storage[storageId] = null;
            return ActionResult.Ok(source.Value.SpeciesId, source.Value.Id);
        }

        public ActionResult ReturnTankToStorage(string tankId, string storageId, string expectedSpecimenId)
        {
            if (tankId == null || !tanks.TryGetValue(tankId, out var source)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (storageId == null || !storage.ContainsKey(storageId)) return ActionResult.Fail(FailureCode.UnknownStorage);
            if (reservations.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.TankReserved);
            if (IsStorageReservedForTechnician(storageId)) return ActionResult.Fail(FailureCode.TechnicianReserved);
            if (IsStorageReservedForStocking(storageId) || IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (!source.HasValue) return ActionResult.Fail(FailureCode.TankEmpty);
            if (source.Value.Id != expectedSpecimenId) return ActionResult.Fail(FailureCode.SpecimenMismatch);
            if (storage[storageId].HasValue) return ActionResult.Fail(FailureCode.StorageFull);
            storage[storageId] = source;
            tanks[tankId] = null;
            return ActionResult.Ok(source.Value.SpeciesId, source.Value.Id);
        }

        public ActionResult SellFromTank(string tankId)
        {
            if (tankId == null || !tanks.TryGetValue(tankId, out var value)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (reservations.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.TankReserved);
            if (IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (!value.HasValue) return ActionResult.Fail(FailureCode.TankEmpty);
            var specimen = value.Value;
            int price = CurrentPrice(specimen.SpeciesId);
            if ((long)Currency + price > int.MaxValue) return ActionResult.Fail(FailureCode.CurrencyOverflow);
            tanks[tankId] = null;
            Currency += price;
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
        }

        public ActionResult ReserveTank(string tankId, string customerId)
        {
            return ReserveTank(tankId, customerId, int.MaxValue);
        }

        /// <summary>
        /// Claims the exact resident at its current tag, but only if the tag is at most maximumPrice.
        /// The agreed price is copied into the reservation; later repricing never changes it.
        /// </summary>
        public ActionResult ReserveTank(string tankId, string customerId, int maximumPrice)
        {
            if (tankId == null || !tanks.TryGetValue(tankId, out var value)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (!ValidCustomer(customerId)) return ActionResult.Fail(FailureCode.InvalidCustomer);
            if (maximumPrice < 0) return ActionResult.Fail(FailureCode.InvalidPrice);
            if (IsTankReservedForStocking(tankId)) return ActionResult.Fail(FailureCode.StockReserved);
            if (!value.HasValue) return ActionResult.Fail(FailureCode.TankEmpty);
            if (reservations.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.TankReserved);
            foreach (var reservation in reservations.Values)
                if (reservation.CustomerId == customerId) return ActionResult.Fail(FailureCode.ReservationMismatch);

            var specimen = value.Value;
            int price = CurrentPrice(specimen.SpeciesId);
            if (price > maximumPrice) return ActionResult.Fail(FailureCode.PriceTooHigh);
            reservations.Add(tankId, new ReservationView(tankId, customerId, specimen.Id, specimen.SpeciesId, price));
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
        }

        public ActionResult ReleaseReservation(string tankId, string customerId)
        {
            if (tankId == null || !tanks.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (!ValidCustomer(customerId)) return ActionResult.Fail(FailureCode.InvalidCustomer);
            if (!reservations.TryGetValue(tankId, out var reservation) || reservation.CustomerId != customerId)
                return ActionResult.Fail(FailureCode.ReservationMismatch);
            reservations.Remove(tankId);
            return ActionResult.Ok(reservation.SpeciesId, reservation.SpecimenId);
        }

        public ActionResult SellReservedTank(string tankId, string customerId, string expectedSpecimenId)
        {
            if (tankId == null || !tanks.TryGetValue(tankId, out var value)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (!ValidCustomer(customerId)) return ActionResult.Fail(FailureCode.InvalidCustomer);
            if (!reservations.TryGetValue(tankId, out var reservation) || reservation.CustomerId != customerId ||
                reservation.SpecimenId != expectedSpecimenId || !value.HasValue ||
                value.Value.Id != reservation.SpecimenId || value.Value.SpeciesId != reservation.SpeciesId)
                return ActionResult.Fail(FailureCode.ReservationMismatch);
            if ((long)Currency + reservation.Price > int.MaxValue) return ActionResult.Fail(FailureCode.CurrencyOverflow);

            // All checks precede the single stock/payment commit. The visitor owns no separate stock copy.
            tanks[tankId] = null;
            reservations.Remove(tankId);
            Currency += reservation.Price;
            return ActionResult.Ok(reservation.SpeciesId, reservation.SpecimenId);
        }

        /// <summary>Visit teardown releases unpaid claims without moving stock or granting payment.</summary>
        public void ClearReservations() { reservations.Clear(); }

        // Layout validates ownership, phase and geometry before asking this single money authority.
        internal bool TrySpendCredits(int amount)
        {
            if (amount <= 0 || Currency < amount) return false;
            Currency -= amount;
            return true;
        }

        // Layout supplies the complete active registry and one signed currency adjustment.
        // Validate and allocate before publishing either, preserving surviving specimens/claims.
        internal ActionResult TryApplyLayoutSlots(string[] desiredIds, int currencyDelta)
            => TryApplyLayoutSlots(desiredIds, config.StorageIds, currencyDelta);

        internal ActionResult TryApplyLayoutSlots(string[] desiredIds, string[] desiredStorageIds, int currencyDelta)
            => TryApplyLayoutSlots(desiredIds, desiredStorageIds, config.ConditionerIds, currencyDelta);

        internal ActionResult TryApplyLayoutSlots(string[] desiredIds, string[] desiredStorageIds, string[] desiredConditionerIds, int currencyDelta)
        {
            if (desiredIds == null || desiredIds.Length < 2 || desiredIds.Length > 8 ||
                desiredIds[0] != config.TankIds[0] || desiredIds[1] != config.TankIds[1] ||
                desiredStorageIds == null || desiredStorageIds.Length > 6 ||
                desiredConditionerIds == null || desiredConditionerIds.Length > 1)
                return ActionResult.Fail(FailureCode.InvalidSave);
            long nextCurrency = (long)Currency + currencyDelta;
            if (nextCurrency < 0) return ActionResult.Fail(FailureCode.InsufficientFunds);
            if (nextCurrency > int.MaxValue) return ActionResult.Fail(FailureCode.CurrencyOverflow);
            var desired = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in desiredIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !desired.Add(id))
                    return ActionResult.Fail(FailureCode.InvalidSave);
            }
            var desiredStorage = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in desiredStorageIds)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || desired.Contains(id) || !desiredStorage.Add(id))
                    return ActionResult.Fail(FailureCode.InvalidSave);
            var desiredMachines = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in desiredConditionerIds)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || desired.Contains(id) || desiredStorage.Contains(id) || !desiredMachines.Add(id))
                    return ActionResult.Fail(FailureCode.InvalidSave);
            if (technicianClaim.HasValue && !desiredStorage.Contains(technicianClaim.Value.StorageId))
                return ActionResult.Fail(FailureCode.TechnicianReserved);
            foreach (var claim in stockTransfers.Values)
                if (!desired.Contains(claim.TankId) || !desiredStorage.Contains(claim.StorageId))
                    return ActionResult.Fail(FailureCode.StockReserved);
            foreach (var machine in conditioners)
                if (!desiredMachines.Contains(machine.Key) && (machine.Value.Phase != ConditionerPhase.Idle || machine.Value.Count != 0))
                    return ActionResult.Fail(FailureCode.ConditionerBusy);
            foreach (var tank in tanks)
                if (!desired.Contains(tank.Key))
                {
                    if (reservations.ContainsKey(tank.Key)) return ActionResult.Fail(FailureCode.TankReserved);
                    if (tank.Value.HasValue) return ActionResult.Fail(FailureCode.TankFull);
                }
            foreach (var slot in storage)
                if (!desiredStorage.Contains(slot.Key) && slot.Value.HasValue)
                    return ActionResult.Fail(FailureCode.StorageFull);
            var replacement = new Dictionary<string, SpecimenView?>(StringComparer.Ordinal);
            var ids = (string[])desiredIds.Clone();
            foreach (string id in ids) replacement.Add(id, tanks.TryGetValue(id, out var value) ? value : null);
            var storedReplacement = new Dictionary<string, SpecimenView?>(StringComparer.Ordinal);
            var storageIds = (string[])desiredStorageIds.Clone();
            foreach (string id in storageIds) storedReplacement.Add(id, storage.TryGetValue(id, out var value) ? value : null);
            var machineReplacement = new Dictionary<string, ConditionerState>(StringComparer.Ordinal);
            var machineIds = (string[])desiredConditionerIds.Clone();
            foreach (string id in machineIds) machineReplacement.Add(id, conditioners.TryGetValue(id, out var value) ? value : new ConditionerState());
            tanks = replacement;
            storage = storedReplacement;
            conditioners = machineReplacement;
            config.ConditionerIds = machineIds;
            config.TankIds = ids;
            config.StorageIds = storageIds;
            Currency = (int)nextCurrency;
            return ActionResult.Ok();
        }
        public ActionResult BuySparkResearch()
        {
            if (SparkResearchUnlocked) return ActionResult.Fail(FailureCode.ResearchAlreadyUnlocked);
            if (!SparkResearchReady) return ActionResult.Fail(FailureCode.ResearchPrerequisiteMissing);
            if (Currency < config.SparkResearchCost) return ActionResult.Fail(FailureCode.InsufficientFunds);
            Currency -= config.SparkResearchCost;
            SparkResearchUnlocked = true;
            growerUnits[(int)CultureKind.Spark] = config.InitialGrowerUnits;
            growerRemaining[(int)CultureKind.Spark] = config.GrowerRefillSeconds;
            return ActionResult.Ok();
        }

        public ActionResult BuyMistResearch()
        {
            if (MistResearchUnlocked) return ActionResult.Fail(FailureCode.MistAlreadyUnlocked);
            if (!MistResearchReady) return ActionResult.Fail(FailureCode.MistPrerequisiteMissing);
            if (Currency < config.MistResearchCost) return ActionResult.Fail(FailureCode.InsufficientFunds);
            Currency -= config.MistResearchCost;
            MistResearchUnlocked = true;
            growerUnits[(int)CultureKind.Mist] = config.InitialGrowerUnits;
            growerRemaining[(int)CultureKind.Mist] = config.GrowerRefillSeconds;
            return ActionResult.Ok();
        }

        public ActionResult BuyUpgrade()
        {
            if (UpgradeOwned) return ActionResult.Fail(FailureCode.AlreadyUpgraded);
            if (Currency < config.UpgradeCost) return ActionResult.Fail(FailureCode.InsufficientFunds);
            Currency -= config.UpgradeCost;
            UpgradeOwned = true;
            return ActionResult.Ok();
        }

        public SaveData CaptureSave()
        {
            // Visits are transient. Save the unpaid stock, so loading cancels visitors safely.
            var save = new SaveData
            {
                version = SaveVersion, currency = Currency, upgradeOwned = UpgradeOwned,
                availableBloom = available[0], availableFlow = available[1],
                availableSpark = available[2], sparkResearchUnlocked = SparkResearchUnlocked,
                availableMist = available[3], mistResearchUnlocked = MistResearchUnlocked,
                growers = new GrowerSave[CultureCount],
                incubator = new IncubatorSave
                {
                    phase = phase, stagedBloom = staged[0], stagedFlow = staged[1], stagedSpark = staged[2], stagedMist = staged[3],
                    speciesId = activeSpecies, specimenId = activeSpecimen, secondsRemaining = incubationRemaining
                },
                tanks = new TankSave[config.TankIds.Length],
                storage = new StorageSave[config.StorageIds.Length],
                conditioners = CaptureConditioners(),
                discoveries = new string[discoveries.Count]
            };
            for (int i = 0; i < CultureCount; i++)
                save.growers[i] = new GrowerSave
                {
                    cultureId = CultureId(i), units = growerUnits[i],
                    secondsUntilNextUnit = growerRemaining[i]
                };
            for (int i = 0; i < config.TankIds.Length; i++)
            {
                string id = config.TankIds[i];
                var value = tanks[id];
                save.tanks[i] = new TankSave
                {
                    tankId = id, specimenId = value.HasValue ? value.Value.Id : null,
                    speciesId = value.HasValue ? value.Value.SpeciesId : null
                };
            }
            for (int i = 0; i < config.StorageIds.Length; i++)
            {
                string id = config.StorageIds[i];
                var value = storage[id];
                save.storage[i] = new StorageSave
                {
                    storageId = id, specimenId = value.HasValue ? value.Value.Id : null,
                    speciesId = value.HasValue ? value.Value.SpeciesId : null
                };
            }
            discoveries.CopyTo(save.discoveries);
            Array.Sort(save.discoveries, StringComparer.Ordinal);
            var tagged = new List<string>(prices.Keys);
            tagged.Sort(StringComparer.Ordinal);
            save.prices = new PriceSave[tagged.Count];
            for (int i = 0; i < tagged.Count; i++)
                save.prices[i] = new PriceSave { speciesId = tagged[i], price = prices[tagged[i]] };
            return save;
        }

        public static bool TryRestore(GameConfig config, SaveData save, out GameModel model, out FailureCode error)
        {
            model = null;
            error = FailureCode.InvalidSave;
            GameModel candidate;
            try { candidate = new GameModel(config); }
            catch (ArgumentException) { return false; }
            if (!candidate.ValidSave(save)) return false;

            candidate.Currency = save.currency;
            candidate.UpgradeOwned = save.upgradeOwned;
            candidate.SparkResearchUnlocked = save.sparkResearchUnlocked;
            candidate.MistResearchUnlocked = save.mistResearchUnlocked;
            candidate.available[0] = save.availableBloom;
            candidate.available[1] = save.availableFlow;
            candidate.available[2] = save.availableSpark;
            candidate.available[3] = save.availableMist;
            foreach (var grower in save.growers)
            {
                int i = CultureIndex(grower.cultureId);
                candidate.growerUnits[i] = grower.units;
                candidate.growerRemaining[i] = grower.secondsUntilNextUnit;
            }
            candidate.phase = save.incubator.phase;
            candidate.staged[0] = save.incubator.stagedBloom;
            candidate.staged[1] = save.incubator.stagedFlow;
            candidate.staged[2] = save.incubator.stagedSpark;
            candidate.staged[3] = save.incubator.stagedMist;
            candidate.activeSpecies = EmptyToNull(save.incubator.speciesId);
            candidate.activeSpecimen = EmptyToNull(save.incubator.specimenId);
            candidate.incubationRemaining = save.incubator.secondsRemaining;
            foreach (var id in save.discoveries) candidate.discoveries.Add(id);
            foreach (var tank in save.tanks)
                candidate.tanks[tank.tankId] = string.IsNullOrEmpty(tank.specimenId)
                    ? (SpecimenView?)null : new SpecimenView(tank.specimenId, tank.speciesId);
            if (save.version >= 3)
                foreach (var slot in save.storage)
                    candidate.storage[slot.storageId] = string.IsNullOrEmpty(slot.specimenId)
                        ? (SpecimenView?)null : new SpecimenView(slot.specimenId, slot.speciesId);
            if (save.version >= 5) candidate.RestoreConditioners(save.conditioners);
            // Version 1 had no tags: every species restores at its standard price.
            if (save.version >= 2 && save.prices != null)
                foreach (var tag in save.prices)
                    if (tag.price != candidate.species[tag.speciesId].SaleValue) candidate.prices[tag.speciesId] = tag.price;
            model = candidate;
            error = FailureCode.None;
            return true;
        }

        private bool ValidSave(SaveData save)
        {
            if (save == null || save.version < 1 || save.version > SaveVersion || save.currency < 0 || save.availableBloom < 0 ||
                save.availableFlow < 0 || save.availableSpark < 0 || save.availableMist < 0 || save.growers == null || save.growers.Length != (save.version < 4 ? 2 : save.version < 6 ? 3 : CultureCount) ||
                save.incubator == null || save.tanks == null || save.tanks.Length != config.TankIds.Length ||
                save.discoveries == null) return false;
            if (save.version < 3)
            {
                if (config.StorageIds.Length != 0 || save.storage != null && save.storage.Length != 0) return false;
            }
            else if (save.storage == null || save.storage.Length != config.StorageIds.Length) return false;
            var inc = save.incubator;
            if (save.version < 4 && (save.sparkResearchUnlocked || save.availableSpark != 0 || inc.stagedSpark != 0)) return false;
            if (!save.sparkResearchUnlocked && (save.availableSpark != 0 || inc.stagedSpark != 0)) return false;
            if (save.version < 6 && (save.mistResearchUnlocked || save.availableMist != 0 || inc.stagedMist != 0)) return false;
            if (!save.mistResearchUnlocked && (save.availableMist != 0 || inc.stagedMist != 0)) return false;
            int capacity = save.upgradeOwned ? config.UpgradedCapacity : config.BaseCapacity;
            if (inc.stagedBloom < 0 || inc.stagedFlow < 0 || inc.stagedSpark < 0 || inc.stagedMist < 0 ||
                (long)save.availableBloom + save.availableFlow + save.availableSpark + save.availableMist + inc.stagedBloom + inc.stagedFlow + inc.stagedSpark + inc.stagedMist > capacity ||
                !Finite(inc.secondsRemaining) || inc.secondsRemaining < 0f ||
                inc.secondsRemaining > config.IncubationSeconds) return false;
            var seenCultures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grower in save.growers)
            {
                if (grower == null || CultureIndex(grower.cultureId) < 0 || (save.version < 4 && grower.cultureId == "spark" || save.version < 6 && grower.cultureId == "mist") ||
                    !seenCultures.Add(grower.cultureId) || grower.units < 0 || grower.units > config.GrowerCapacity ||
                    !Finite(grower.secondsUntilNextUnit) || grower.secondsUntilNextUnit <= 0f ||
                    grower.secondsUntilNextUnit > config.GrowerRefillSeconds) return false;
                if (grower.cultureId == "spark" && !save.sparkResearchUnlocked &&
                    (grower.units != 0 || grower.secondsUntilNextUnit != config.GrowerRefillSeconds)) return false;
                if (grower.cultureId == "mist" && !save.mistResearchUnlocked &&
                    (grower.units != 0 || grower.secondsUntilNextUnit != config.GrowerRefillSeconds)) return false;
            }
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in save.discoveries)
                if (!HasResearchForSpecies(id, save.sparkResearchUnlocked, save.mistResearchUnlocked) || !known.Add(id)) return false;
            if (save.sparkResearchUnlocked && (!known.Contains("pondlet") || !known.Contains("bubblebelly") || !known.Contains("ribbonfin"))) return false;
            if (save.mistResearchUnlocked && (!save.sparkResearchUnlocked || !known.Contains("lanternfin"))) return false;
            var specimens = new HashSet<string>(StringComparer.Ordinal);
            if (inc.phase == IncubatorPhase.Dosing)
            {
                if (!string.IsNullOrEmpty(inc.speciesId) || !string.IsNullOrEmpty(inc.specimenId) ||
                    inc.secondsRemaining != 0f) return false;
            }
            else if (inc.phase == IncubatorPhase.Incubating || inc.phase == IncubatorPhase.Ready)
            {
                if (inc.stagedBloom != 0 || inc.stagedFlow != 0 || inc.stagedSpark != 0 || inc.stagedMist != 0 ||
                    !HasResearchForSpecies(inc.speciesId, save.sparkResearchUnlocked, save.mistResearchUnlocked) ||
                    string.IsNullOrWhiteSpace(inc.specimenId) ||
                    !specimens.Add(inc.specimenId)) return false;
                if (inc.phase == IncubatorPhase.Incubating && inc.secondsRemaining <= 0f) return false;
                if (inc.phase == IncubatorPhase.Ready &&
                    (inc.secondsRemaining != 0f || !known.Contains(inc.speciesId))) return false;
            }
            else return false;

            var seenTanks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tank in save.tanks)
            {
                if (tank == null || tank.tankId == null || !tanks.ContainsKey(tank.tankId) ||
                    !seenTanks.Add(tank.tankId)) return false;
                if (string.IsNullOrEmpty(tank.specimenId) && string.IsNullOrEmpty(tank.speciesId)) continue;
                if (string.IsNullOrWhiteSpace(tank.specimenId) || !HasResearchForSpecies(tank.speciesId, save.sparkResearchUnlocked, save.mistResearchUnlocked) ||
                    !known.Contains(tank.speciesId) ||
                    !specimens.Add(tank.specimenId)) return false;
            }

            if (save.version >= 3)
            {
                var seenStorage = new HashSet<string>(StringComparer.Ordinal);
                foreach (var slot in save.storage)
                {
                    if (slot == null || slot.storageId == null || !storage.ContainsKey(slot.storageId) ||
                        !seenStorage.Add(slot.storageId)) return false;
                    if (string.IsNullOrEmpty(slot.specimenId) && string.IsNullOrEmpty(slot.speciesId)) continue;
                    if (string.IsNullOrWhiteSpace(slot.specimenId) || !HasResearchForSpecies(slot.speciesId, save.sparkResearchUnlocked, save.mistResearchUnlocked) ||
                        !known.Contains(slot.speciesId) ||
                        !specimens.Add(slot.specimenId)) return false;
                }
            }

            if (!ValidConditionerSave(save, known, specimens, capacity)) return false;

            // Unity's JSON reader may give an absent array as null or empty. Version 1 never had tags.
            if (save.prices == null || save.prices.Length == 0) return true;
            if (save.version == 1) return false;
            var tagged = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in save.prices)
            {
                if (tag == null || tag.speciesId == null || !known.Contains(tag.speciesId) || !tagged.Add(tag.speciesId))
                    return false;
                var view = GetPrice(tag.speciesId).Value;
                if (tag.price < view.Minimum || tag.price > view.Maximum) return false;
            }
            return true;
        }

        internal static GameConfig CopyAndValidate(GameConfig input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.GrowerCapacity <= 0 || input.InitialGrowerUnits < 0 ||
                input.InitialGrowerUnits > input.GrowerCapacity ||
                input.BaseCapacity <= 0 || input.UpgradedCapacity < input.BaseCapacity ||
                input.UpgradeCost < 0 || input.SparkResearchCost < 0 || input.MistResearchCost < 0 || input.MinimumPrice < 1 ||
                input.MaximumPricePercent < 100 || input.MaximumPricePercent > 1000 ||
                !PositiveFinite(input.GrowerRefillSeconds) ||
                !PositiveFinite(input.IncubationSeconds) || !PositiveFinite(input.ConditioningSeconds) || !PositiveFinite(input.BaseCollectionSeconds) ||
                !PositiveFinite(input.UpgradedCollectionSeconds) || input.Species == null ||
                input.Species.Length == 0 || input.TankIds == null || input.TankIds.Length < 2 || input.TankIds.Length > 8 ||
                input.StorageIds == null || input.StorageIds.Length > 6 ||
                input.ConditionerIds == null || input.ConditionerIds.Length > 1)
                throw new ArgumentException("Invalid prototype tuning.", nameof(input));
            var copy = new GameConfig
            {
                GrowerCapacity = input.GrowerCapacity, InitialGrowerUnits = input.InitialGrowerUnits,
                GrowerRefillSeconds = input.GrowerRefillSeconds, IncubationSeconds = input.IncubationSeconds,
                ConditioningSeconds = input.ConditioningSeconds, ConditionerIds = (string[])input.ConditionerIds.Clone(),
                BaseCapacity = input.BaseCapacity, UpgradedCapacity = input.UpgradedCapacity,
                BaseCollectionSeconds = input.BaseCollectionSeconds, UpgradedCollectionSeconds = input.UpgradedCollectionSeconds,
                UpgradeCost = input.UpgradeCost, SparkResearchCost = input.SparkResearchCost, MistResearchCost = input.MistResearchCost, MinimumPrice = input.MinimumPrice,
                MaximumPricePercent = input.MaximumPricePercent, TankIds = (string[])input.TankIds.Clone(),
                StorageIds = (string[])input.StorageIds.Clone(),
                Species = new SpeciesDefinition[input.Species.Length]
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var recipes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < input.Species.Length; i++)
            {
                var value = input.Species[i];
                if (value == null || string.IsNullOrWhiteSpace(value.Id) || !ids.Add(value.Id) ||
                    string.IsNullOrWhiteSpace(value.DisplayName) || string.IsNullOrWhiteSpace(value.PresentationId) || string.IsNullOrWhiteSpace(value.FamilyId) ||
                    value.Bloom < 0 || value.Flow < 0 || value.Spark < 0 || value.Mist < 0 || (long)value.Bloom + value.Flow + value.Spark + value.Mist <= 0 ||
                    (long)value.Bloom + value.Flow + value.Spark + value.Mist > input.UpgradedCapacity || value.SaleValue < input.MinimumPrice ||
                    !recipes.Add(value.Bloom + ":" + value.Flow + ":" + value.Spark + ":" + value.Mist))
                    throw new ArgumentException("Invalid or ambiguous species definition.", nameof(input));
                copy.Species[i] = value.Copy();
            }
            ids.Clear();
            foreach (var id in copy.TankIds)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id))
                    throw new ArgumentException("Two to eight unique stable tank IDs are required.", nameof(input));
            foreach (var id in copy.StorageIds)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id))
                    throw new ArgumentException("Up to six unique storage IDs distinct from tank IDs are required.", nameof(input));
            foreach (var id in copy.ConditionerIds)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id))
                    throw new ArgumentException("Up to one unique conditioner ID distinct from stock slots is required.", nameof(input));
            return copy;
        }

        private int CurrentPrice(string speciesId)
        {
            return prices.TryGetValue(speciesId, out int tag) ? tag : species[speciesId].SaleValue;
        }
        private bool HasResearchForSpecies(string id, bool sparkUnlocked, bool mistUnlocked)
            => id != null && species.TryGetValue(id, out var definition) && (definition.Spark == 0 || sparkUnlocked) && (definition.Mist == 0 || mistUnlocked);
        private static string CultureId(int index) => index == 0 ? "bloom" : index == 1 ? "flow" : index == 2 ? "spark" : "mist";
        private static int CultureIndex(string id) => id == "bloom" ? 0 : id == "flow" ? 1 : id == "spark" ? 2 : id == "mist" ? 3 : -1;
        private static bool ValidCulture(int index) { return index >= 0 && index < CultureCount; }
        private static bool ValidCustomer(string customerId) { return !string.IsNullOrWhiteSpace(customerId) && customerId.Length <= 128; }
        private static int CultureIndexOrThrow(CultureKind culture)
        {
            if (!ValidCulture((int)culture)) throw new ArgumentOutOfRangeException(nameof(culture));
            return (int)culture;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool PositiveFinite(float value) { return Finite(value) && value > 0f; }
        private static string EmptyToNull(string value) { return string.IsNullOrEmpty(value) ? null : value; }
    }
}
