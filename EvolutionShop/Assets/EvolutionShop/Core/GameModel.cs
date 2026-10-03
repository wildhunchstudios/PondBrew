using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>
    /// Single authority for prototype transactions. Time advances only through Tick.
    /// Staged doses reserve their return space until incubation starts, so invalid
    /// mixtures can always be recovered without losing a culture unit.
    /// </summary>
    public sealed class GameModel
    {
        private readonly GameConfig config;
        private readonly Dictionary<string, SpeciesDefinition> species;
        private readonly Dictionary<string, SpecimenView?> tanks;
        private readonly HashSet<string> discoveries = new HashSet<string>(StringComparer.Ordinal);
        private readonly int[] available = new int[2];
        private readonly int[] staged = new int[2];
        private readonly int[] growerUnits = new int[2];
        private readonly float[] growerRemaining = new float[2];
        private IncubatorPhase phase;
        private string activeSpecies;
        private string activeSpecimen;
        private float incubationRemaining;

        public int Currency { get; private set; }
        public bool UpgradeOwned { get; private set; }
        public int Capacity { get { return UpgradeOwned ? config.UpgradedCapacity : config.BaseCapacity; } }
        public float CollectionSecondsPerUnit
        {
            get { return UpgradeOwned ? config.UpgradedCollectionSeconds : config.BaseCollectionSeconds; }
        }
        public int ReservedCultureCount
        {
            get { return available[0] + available[1] + staged[0] + staged[1]; }
        }
        public IncubatorView Incubator
        {
            get { return new IncubatorView(phase, staged[0], staged[1], activeSpecies, activeSpecimen, incubationRemaining); }
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
            for (int i = 0; i < 2; i++)
            {
                growerUnits[i] = this.config.InitialGrowerUnits;
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
        public SpeciesDefinition GetSpecies(string speciesId)
        {
            return speciesId != null && species.TryGetValue(speciesId, out var value) ? value.Copy() : null;
        }
        public bool IsDiscovered(string speciesId) { return speciesId != null && discoveries.Contains(speciesId); }

        public ActionResult Collect(CultureKind culture)
        {
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (ReservedCultureCount >= Capacity) return ActionResult.Fail(FailureCode.ReservoirFull);
            if (growerUnits[i] <= 0) return ActionResult.Fail(FailureCode.GrowerEmpty);
            growerUnits[i]--;
            available[i]++;
            return ActionResult.Ok();
        }

        public ActionResult Dose(CultureKind culture)
        {
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (available[i] <= 0) return ActionResult.Fail(FailureCode.InsufficientCulture);
            available[i]--;
            staged[i]++;
            return ActionResult.Ok();
        }

        public ActionResult ReturnDose(CultureKind culture)
        {
            int i = (int)culture;
            if (!ValidCulture(i)) return ActionResult.Fail(FailureCode.InvalidCulture);
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (staged[i] <= 0) return ActionResult.Fail(FailureCode.AlreadyEmpty);
            staged[i]--;
            available[i]++;
            return ActionResult.Ok();
        }

        public ActionResult ClearDoses()
        {
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (staged[0] + staged[1] == 0) return ActionResult.Fail(FailureCode.AlreadyEmpty);
            for (int i = 0; i < 2; i++) { available[i] += staged[i]; staged[i] = 0; }
            return ActionResult.Ok();
        }

        public ActionResult UseKnownRecipe(string speciesId)
        {
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            if (speciesId == null || !species.TryGetValue(speciesId, out var recipe))
                return ActionResult.Fail(FailureCode.UnknownSpecies);
            if (!discoveries.Contains(speciesId)) return ActionResult.Fail(FailureCode.UndiscoveredRecipe);
            int bloom = available[0] + staged[0];
            int flow = available[1] + staged[1];
            if (bloom < recipe.Bloom || flow < recipe.Flow)
                return ActionResult.Fail(FailureCode.InsufficientCulture);
            available[0] = bloom - recipe.Bloom;
            available[1] = flow - recipe.Flow;
            staged[0] = recipe.Bloom;
            staged[1] = recipe.Flow;
            return ActionResult.Ok(speciesId);
        }

        public ActionResult BeginIncubation()
        {
            if (phase != IncubatorPhase.Dosing) return ActionResult.Fail(FailureCode.IncubatorBusy);
            SpeciesDefinition match = null;
            foreach (var definition in config.Species)
                if (definition.Bloom == staged[0] && definition.Flow == staged[1]) { match = definition; break; }
            if (match == null) return ActionResult.Fail(FailureCode.InvalidRecipe);
            activeSpecies = match.Id;
            activeSpecimen = Guid.NewGuid().ToString("N");
            staged[0] = staged[1] = 0;
            incubationRemaining = config.IncubationSeconds;
            phase = IncubatorPhase.Incubating;
            return ActionResult.Ok(activeSpecies, activeSpecimen);
        }

        public TickResult Tick(float simulationSeconds)
        {
            if (!Finite(simulationSeconds) || simulationSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
            for (int i = 0; i < 2; i++)
            {
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

            if (phase != IncubatorPhase.Incubating) return default;
            incubationRemaining = Math.Max(0f, incubationRemaining - simulationSeconds);
            if (incubationRemaining > 0f) return default;
            phase = IncubatorPhase.Ready;
            bool first = discoveries.Add(activeSpecies);
            return new TickResult(true, first, activeSpecies, activeSpecimen);
        }

        public ActionResult TransferToTank(string tankId)
        {
            if (tankId == null || !tanks.ContainsKey(tankId)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (phase != IncubatorPhase.Ready) return ActionResult.Fail(FailureCode.OutputNotReady);
            if (tanks[tankId].HasValue) return ActionResult.Fail(FailureCode.TankFull);
            var specimen = new SpecimenView(activeSpecimen, activeSpecies);
            tanks[tankId] = specimen;
            phase = IncubatorPhase.Dosing;
            activeSpecies = activeSpecimen = null;
            incubationRemaining = 0f;
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
        }

        public ActionResult SellFromTank(string tankId)
        {
            if (tankId == null || !tanks.TryGetValue(tankId, out var value)) return ActionResult.Fail(FailureCode.UnknownTank);
            if (!value.HasValue) return ActionResult.Fail(FailureCode.TankEmpty);
            var specimen = value.Value;
            int price = species[specimen.SpeciesId].SaleValue;
            if ((long)Currency + price > int.MaxValue) return ActionResult.Fail(FailureCode.CurrencyOverflow);
            tanks[tankId] = null;
            Currency += price;
            return ActionResult.Ok(specimen.SpeciesId, specimen.Id);
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
            var save = new SaveData
            {
                version = 1, currency = Currency, upgradeOwned = UpgradeOwned,
                availableBloom = available[0], availableFlow = available[1],
                growers = new GrowerSave[2],
                incubator = new IncubatorSave
                {
                    phase = phase, stagedBloom = staged[0], stagedFlow = staged[1],
                    speciesId = activeSpecies, specimenId = activeSpecimen, secondsRemaining = incubationRemaining
                },
                tanks = new TankSave[config.TankIds.Length],
                discoveries = new string[discoveries.Count]
            };
            for (int i = 0; i < 2; i++)
                save.growers[i] = new GrowerSave
                {
                    cultureId = i == 0 ? "bloom" : "flow", units = growerUnits[i],
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
            discoveries.CopyTo(save.discoveries);
            Array.Sort(save.discoveries, StringComparer.Ordinal);
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
            candidate.available[0] = save.availableBloom;
            candidate.available[1] = save.availableFlow;
            foreach (var grower in save.growers)
            {
                int i = grower.cultureId == "bloom" ? 0 : 1;
                candidate.growerUnits[i] = grower.units;
                candidate.growerRemaining[i] = grower.secondsUntilNextUnit;
            }
            candidate.phase = save.incubator.phase;
            candidate.staged[0] = save.incubator.stagedBloom;
            candidate.staged[1] = save.incubator.stagedFlow;
            candidate.activeSpecies = EmptyToNull(save.incubator.speciesId);
            candidate.activeSpecimen = EmptyToNull(save.incubator.specimenId);
            candidate.incubationRemaining = save.incubator.secondsRemaining;
            foreach (var id in save.discoveries) candidate.discoveries.Add(id);
            foreach (var tank in save.tanks)
                candidate.tanks[tank.tankId] = string.IsNullOrEmpty(tank.specimenId)
                    ? (SpecimenView?)null : new SpecimenView(tank.specimenId, tank.speciesId);
            model = candidate;
            error = FailureCode.None;
            return true;
        }

        private bool ValidSave(SaveData save)
        {
            if (save == null || save.version != 1 || save.currency < 0 || save.availableBloom < 0 ||
                save.availableFlow < 0 || save.growers == null || save.growers.Length != 2 ||
                save.incubator == null || save.tanks == null || save.tanks.Length != config.TankIds.Length ||
                save.discoveries == null) return false;
            var inc = save.incubator;
            int capacity = save.upgradeOwned ? config.UpgradedCapacity : config.BaseCapacity;
            if (inc.stagedBloom < 0 || inc.stagedFlow < 0 ||
                (long)save.availableBloom + save.availableFlow + inc.stagedBloom + inc.stagedFlow > capacity ||
                !Finite(inc.secondsRemaining) || inc.secondsRemaining < 0f ||
                inc.secondsRemaining > config.IncubationSeconds) return false;
            var seenCultures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grower in save.growers)
                if (grower == null || (grower.cultureId != "bloom" && grower.cultureId != "flow") ||
                    !seenCultures.Add(grower.cultureId) || grower.units < 0 || grower.units > config.GrowerCapacity ||
                    !Finite(grower.secondsUntilNextUnit) || grower.secondsUntilNextUnit <= 0f ||
                    grower.secondsUntilNextUnit > config.GrowerRefillSeconds) return false;
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in save.discoveries)
                if (id == null || !species.ContainsKey(id) || !known.Add(id)) return false;
            var specimens = new HashSet<string>(StringComparer.Ordinal);
            if (inc.phase == IncubatorPhase.Dosing)
            {
                if (!string.IsNullOrEmpty(inc.speciesId) || !string.IsNullOrEmpty(inc.specimenId) ||
                    inc.secondsRemaining != 0f) return false;
            }
            else if (inc.phase == IncubatorPhase.Incubating || inc.phase == IncubatorPhase.Ready)
            {
                if (inc.stagedBloom != 0 || inc.stagedFlow != 0 ||
                    inc.speciesId == null || !species.ContainsKey(inc.speciesId) ||
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
                if (string.IsNullOrWhiteSpace(tank.specimenId) || tank.speciesId == null ||
                    !species.ContainsKey(tank.speciesId) || !known.Contains(tank.speciesId) ||
                    !specimens.Add(tank.specimenId)) return false;
            }
            return true;
        }

        private static GameConfig CopyAndValidate(GameConfig input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.GrowerCapacity <= 0 || input.InitialGrowerUnits < 0 ||
                input.InitialGrowerUnits > input.GrowerCapacity ||
                input.BaseCapacity <= 0 || input.UpgradedCapacity < input.BaseCapacity ||
                input.UpgradeCost < 0 || !PositiveFinite(input.GrowerRefillSeconds) ||
                !PositiveFinite(input.IncubationSeconds) || !PositiveFinite(input.BaseCollectionSeconds) ||
                !PositiveFinite(input.UpgradedCollectionSeconds) || input.Species == null ||
                input.Species.Length == 0 || input.TankIds == null || input.TankIds.Length != 2)
                throw new ArgumentException("Invalid prototype tuning.", nameof(input));
            var copy = new GameConfig
            {
                GrowerCapacity = input.GrowerCapacity, InitialGrowerUnits = input.InitialGrowerUnits,
                GrowerRefillSeconds = input.GrowerRefillSeconds, IncubationSeconds = input.IncubationSeconds,
                BaseCapacity = input.BaseCapacity, UpgradedCapacity = input.UpgradedCapacity,
                BaseCollectionSeconds = input.BaseCollectionSeconds, UpgradedCollectionSeconds = input.UpgradedCollectionSeconds,
                UpgradeCost = input.UpgradeCost, TankIds = (string[])input.TankIds.Clone(),
                Species = new SpeciesDefinition[input.Species.Length]
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var recipes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < input.Species.Length; i++)
            {
                var value = input.Species[i];
                if (value == null || string.IsNullOrWhiteSpace(value.Id) || !ids.Add(value.Id) ||
                    string.IsNullOrWhiteSpace(value.DisplayName) || string.IsNullOrWhiteSpace(value.PresentationId) ||
                    value.Bloom < 0 || value.Flow < 0 || (long)value.Bloom + value.Flow <= 0 ||
                    (long)value.Bloom + value.Flow > input.UpgradedCapacity || value.SaleValue < 0 ||
                    !recipes.Add(value.Bloom + ":" + value.Flow))
                    throw new ArgumentException("Invalid or ambiguous species definition.", nameof(input));
                copy.Species[i] = value.Copy();
            }
            ids.Clear();
            foreach (var id in copy.TankIds)
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    throw new ArgumentException("Exactly two unique stable tank IDs are required.", nameof(input));
            return copy;
        }

        private static bool ValidCulture(int index) { return index >= 0 && index < 2; }
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