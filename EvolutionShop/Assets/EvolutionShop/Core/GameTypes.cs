using System;

namespace EvolutionShop
{
    public enum CultureKind { Bloom = 0, Flow = 1, Spark = 2, Mist = 3 }
    public enum IncubatorPhase { Dosing = 0, Incubating = 1, Ready = 2 }
    public enum ConditionerPhase { Idle = 0, Processing = 1, Ready = 2 }
    public enum FailureCode
    {
        None, InvalidCulture, GrowerEmpty, ReservoirFull, InsufficientCulture,
        IncubatorBusy, InvalidRecipe, AlreadyEmpty, UnknownSpecies,
        UndiscoveredRecipe, UnknownTank, TankFull, TankEmpty, OutputNotReady,
        InsufficientFunds, AlreadyUpgraded, CurrencyOverflow, InvalidSave,
        TankReserved, InvalidCustomer, ReservationMismatch, NoCustomerReady,
        InvalidPrice, PriceTooHigh, UnknownStorage, StorageFull, StorageEmpty, SpecimenMismatch,
        ResearchLocked, ResearchPrerequisiteMissing, ResearchAlreadyUnlocked,
        UnknownConditioner, ConditionerBusy, InvalidConditioningInput, ConditionerNotProcessing,
        MistPrerequisiteMissing, MistAlreadyUnlocked, StockReserved, TechnicianReserved
    }

    [Serializable]
    public sealed class SpeciesDefinition
    {
        public string Id;
        public string DisplayName;
        public int Bloom;
        public int Flow;
        public int Spark;
        public int Mist;
        public string FamilyId;
        public int SaleValue;
        public string PresentationId;

        public SpeciesDefinition() { }
        public SpeciesDefinition(string id, string name, int bloom, int flow, int saleValue, string presentationId, int spark = 0, int mist = 0, string familyId = "Brookkin")
        {
            Id = id; DisplayName = name; Bloom = bloom; Flow = flow; Spark = spark; Mist = mist; FamilyId = familyId;
            SaleValue = saleValue; PresentationId = presentationId;
        }

        internal SpeciesDefinition Copy()
        {
            return new SpeciesDefinition(Id, DisplayName, Bloom, Flow, SaleValue, PresentationId, Spark, Mist, FamilyId);
        }
    }

    [Serializable]
    public sealed class GameConfig
    {
        public int GrowerCapacity = 6;
        public int InitialGrowerUnits = 4;
        public float GrowerRefillSeconds = 2f;
        public float IncubationSeconds = 10f;
        public float ConditioningSeconds = 12f;
        public int BaseCapacity = 4;
        public int UpgradedCapacity = 8;
        public float BaseCollectionSeconds = 0.6f;
        public float UpgradedCollectionSeconds = 0.3f;
        public int UpgradeCost = 24;
        public int SparkResearchCost = 48;
        public int MistResearchCost = 40;
        // A species' value is its standard price. Players may tag it from MinimumPrice up to this percentage.
        public int MinimumPrice = 1;
        public int MaximumPricePercent = 300;
        public string[] TankIds = { "tank-a", "tank-b" };
        public string[] StorageIds = Array.Empty<string>();
        public string[] ConditionerIds = Array.Empty<string>();
        public SpeciesDefinition[] Species =
        {
            new SpeciesDefinition("pondlet", "Pondlet", 1, 1, 6, "teardrop"),
            new SpeciesDefinition("bubblebelly", "Bubblebelly", 2, 1, 10, "round"),
            new SpeciesDefinition("ribbonfin", "Ribbonfin", 1, 2, 10, "ribbon"),
            new SpeciesDefinition("lanternfin", "Lanternfin", 1, 1, 14, "lantern", 1),
            new SpeciesDefinition("driftwing", "Driftwing", 1, 1, 18, "wing", 0, 1, "Airfin")
        };

        public static GameConfig CreateDefault() { return new GameConfig(); }
    }

    public readonly struct ActionResult
    {
        public readonly bool Success;
        public readonly FailureCode Failure;
        public readonly string SpeciesId;
        public readonly string SpecimenId;

        private ActionResult(bool success, FailureCode failure, string speciesId, string specimenId)
        {
            Success = success; Failure = failure; SpeciesId = speciesId; SpecimenId = specimenId;
        }
        internal static ActionResult Ok(string speciesId = null, string specimenId = null)
        {
            return new ActionResult(true, FailureCode.None, speciesId, specimenId);
        }
        internal static ActionResult Fail(FailureCode code) { return new ActionResult(false, code, null, null); }
    }

    public readonly struct TickResult
    {
        public readonly bool Matured;
        public readonly bool FirstDiscovery;
        public readonly string SpeciesId;
        public readonly string SpecimenId;
        public readonly int ConditionedCount;
        internal TickResult(bool matured, bool firstDiscovery, string speciesId, string specimenId, int conditionedCount = 0)
        {
            Matured = matured; FirstDiscovery = firstDiscovery; SpeciesId = speciesId; SpecimenId = specimenId;
            ConditionedCount = conditionedCount;
        }
    }

    public readonly struct GrowerView
    {
        public readonly CultureKind Culture;
        public readonly int Units;
        public readonly int Capacity;
        public readonly float SecondsToNextUnit;
        internal GrowerView(CultureKind culture, int units, int capacity, float seconds)
        {
            Culture = culture; Units = units; Capacity = capacity; SecondsToNextUnit = seconds;
        }
    }

    public readonly struct IncubatorView
    {
        public readonly IncubatorPhase Phase;
        public readonly int Bloom;
        public readonly int Flow;
        public readonly int Spark;
        public readonly int Mist;
        public readonly string SpeciesId;
        public readonly string SpecimenId;
        public readonly float SecondsRemaining;
        internal IncubatorView(IncubatorPhase phase, int bloom, int flow, string speciesId, string specimenId, float remaining, int spark = 0, int mist = 0)
        {
            Phase = phase; Bloom = bloom; Flow = flow; Spark = spark; Mist = mist;
            SpeciesId = speciesId; SpecimenId = specimenId; SecondsRemaining = remaining;
        }
    }

    public readonly struct SpecimenView
    {
        public readonly string Id;
        public readonly string SpeciesId;
        internal SpecimenView(string id, string speciesId) { Id = id; SpeciesId = speciesId; }
    }

    public readonly struct ReservationView
    {
        public readonly string TankId;
        public readonly string CustomerId;
        public readonly string SpecimenId;
        public readonly string SpeciesId;
        public readonly int Price;
        internal ReservationView(string tankId, string customerId, string specimenId, string speciesId, int price)
        {
            TankId = tankId; CustomerId = customerId; SpecimenId = specimenId;
            SpeciesId = speciesId; Price = price;
        }
    }

    public readonly struct PriceView
    {
        public readonly string SpeciesId;
        public readonly int Price;
        public readonly int Standard;
        public readonly int Minimum;
        public readonly int Maximum;
        internal PriceView(string speciesId, int price, int standard, int minimum, int maximum)
        {
            SpeciesId = speciesId; Price = price; Standard = standard; Minimum = minimum; Maximum = maximum;
        }
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int currency;
        public bool upgradeOwned;
        public int availableBloom;
        public int availableFlow;
        // Version 4: research unlock and third-culture quantities. Old saves remain locked.
        public bool sparkResearchUnlocked;
        public int availableSpark;
        // Version 6: fourth renewable culture and new family.
        public bool mistResearchUnlocked;
        public int availableMist;
        public GrowerSave[] growers;
        public IncubatorSave incubator;
        public TankSave[] tanks;
        // Version 3: separate lab storage; these locations are never customer displays.
        public StorageSave[] storage;
        // Version 5: bounded conditioning jobs own their inputs and completed outputs.
        public ConditionerSave[] conditioners;
        public string[] discoveries;
        // Version 2: price tags that differ from the standard price. Version 1 has none.
        public PriceSave[] prices;
    }

    [Serializable]
    public sealed class PriceSave
    {
        public string speciesId;
        public int price;
    }

    [Serializable]
    public sealed class GrowerSave
    {
        public string cultureId;
        public int units;
        public float secondsUntilNextUnit;
    }

    [Serializable]
    public sealed class IncubatorSave
    {
        public IncubatorPhase phase;
        public int stagedBloom;
        public int stagedFlow;
        public int stagedSpark;
        public int stagedMist;
        public string speciesId;
        public string specimenId;
        public float secondsRemaining;
    }

    [Serializable]
    public sealed class StorageSave
    {
        public string storageId;
        public string specimenId;
        public string speciesId;
    }

    [Serializable]
    public sealed class TankSave
    {
        public string tankId;
        public string specimenId;
        public string speciesId;
    }
}
