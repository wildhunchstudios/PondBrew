using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    // UX-04: what the incubator, display-tank and counter cards show, filled by the session each frame
    // (ShopToolkitCards.cs). Cards read these and call the session's existing commands; nothing here changes state.

    public enum IncubatorCardMode { Dosing, Growing, Ready }

    public struct CultureChip
    {
        public CultureKind Kind;
        public int Staged;
        public int InSiphon;
    }

    public struct RecipeTile
    {
        public string SpeciesId;
        public string Name;
        /// <summary>The species' presentation id (teardrop, round, ribbon, lantern, wing): its tile silhouette.</summary>
        public string Look;
        public int Bloom, Flow, Spark, Mist;
        /// <summary>The first culture the siphon and staged doses lack for this recipe; null when it can be measured.</summary>
        public CultureKind? Missing;
    }

    public sealed class IncubatorCardState
    {
        public IncubatorCardMode Mode;
        public string SpeciesName;
        public string SpeciesLook;
        public float SecondsLeft;
        public float Duration;
        /// <summary>Why the player can't dose here now (Moss or the lab helper owns the incubator); null when free.</summary>
        public string Busy;
        public bool CanCancelHelper;
        public bool SiphonFull;
        public int StagedTotal;
        public readonly List<CultureChip> Chips = new List<CultureChip>();
        public readonly List<RecipeTile> Recipes = new List<RecipeTile>();

        public void Clear()
        {
            Chips.Clear(); Recipes.Clear();
            SpeciesName = SpeciesLook = Busy = null; CanCancelHelper = SiphonFull = false; StagedTotal = 0; SecondsLeft = 0;
        }
    }

    public enum StockSource { Incubator, Rack, Vat }

    public struct StockTile
    {
        public StockSource Source;
        public string Name;
        public string Look;
        public string Where;
        public string StorageId;
        public string ExpectedId;
        public string MachineId;
        public int MachineSlot;
        public bool Enabled;
    }

    public enum BuyerMood { Happy, Neutral, Frown, Broke }

    public struct BuyerFace
    {
        public string ArchetypeId;
        public string Name;
        public PriceReaction Reaction;
        public bool OverBudget;
        public int Budget;
        public bool Collects;
    }

    public sealed class TankCardState
    {
        public string Title;
        public bool Empty;
        public string SpeciesId;
        public string SpeciesName;
        public int Price, Standard, Minimum, Maximum;
        public bool Reserved;
        public int ReservedPrice;
        public int Buyers, Types;
        public bool StockerDelivering;
        public string IncubatorReady;
        public string ReturnStorageId, ReturnWhere, ResidentId;
        public readonly List<BuyerFace> Faces = new List<BuyerFace>();
        public readonly List<StockTile> Sources = new List<StockTile>();

        public void Clear()
        {
            Faces.Clear(); Sources.Clear();
            Title = SpeciesId = SpeciesName = IncubatorReady = ReturnStorageId = ReturnWhere = ResidentId = null;
            Empty = Reserved = StockerDelivering = false; Price = Standard = Minimum = Maximum = ReservedPrice = Buyers = Types = 0;
        }
    }

    public struct OrderRow
    {
        public string VisitId;
        public string ArchetypeId;
        public string Customer;
        public string Species;
        public int Price;
        public bool AtCounter;
        public float SecondsLeft;
        public float PatienceSeconds;
    }

    public enum CounterPrimary { None, OpenShop, Serve }

    public sealed class CounterCardState
    {
        public HudDayPhase Phase;
        public int Day;
        public float SecondsLeft;
        public bool AtCounter;
        public bool CanServe;
        public int ServePrice;
        public int Displays, EmptyDisplays, Browsing;
        public bool HasReport;
        public int ReportDay;
        public bool ShowNextGoal;
        public string Cashier;
        public float TradingSeconds;
        /// <summary>AD-E prototype: orders are bagged by holding the mouse on the counter, not with Serve.</summary>
        public bool HandsOn;
        public readonly List<OrderRow> Rows = new List<OrderRow>();

        public void Clear()
        {
            Rows.Clear();
            Cashier = null; CanServe = HasReport = ShowNextGoal = AtCounter = false;
            ServePrice = Displays = EmptyDisplays = Browsing = ReportDay = 0;
        }
    }

    // ---------- UX-05: specimen rack, conditioning vat, research bench ----------

    public struct RackCompartment
    {
        public string StorageId;
        /// <summary>"lower" or "upper".</summary>
        public string Where;
        /// <summary>Null when the compartment is empty.</summary>
        public string SpeciesName;
        public string Look;
        /// <summary>Nori has reserved what is here for a delivery.</summary>
        public bool Reserved;
    }

    public sealed class RackCardState
    {
        public bool Placed;
        public readonly List<RackCompartment> Compartments = new List<RackCompartment>();
        /// <summary>Ready output that can be stored here: the incubator, then each ready vat chamber.</summary>
        public readonly List<StockTile> Sources = new List<StockTile>();

        public void Clear()
        {
            Compartments.Clear(); Sources.Clear();
            Placed = false;
        }
    }

    public enum VatCardMode { Missing, Locked, Idle, Processing, Ready }

    public struct VatInput
    {
        public string StorageId;
        public string ExpectedId;
        public string Where;
        public bool Selected;
    }

    public struct VatOutput
    {
        public int Chamber;
        public string Name;
        public string Look;
    }

    public sealed class VatCardState
    {
        public VatCardMode Mode;
        public float SecondsLeft, Duration;
        public int SelectedCount, SparkInSiphon;
        public bool FreeSpace;
        public bool MistUnlocked, MistReady, Night;
        public int MistCost, Currency;
        public readonly List<VatInput> Inputs = new List<VatInput>();
        public readonly List<VatOutput> Outputs = new List<VatOutput>();

        public void Clear()
        {
            Inputs.Clear(); Outputs.Clear();
            Mode = VatCardMode.Missing; SecondsLeft = 0; SelectedCount = SparkInSiphon = 0;
            FreeSpace = MistUnlocked = MistReady = Night = false;
        }
    }

    public struct BenchRequirement
    {
        public string Name;
        public bool Done;
    }

    public struct BenchModule
    {
        public bool Owned, Enabled, CanInstall;
        public int Cost;
        /// <summary>Why it can't be installed now (a string key, formatted with the coins missing); null when it can.</summary>
        public string BlockedKey;
        public int Missing;
    }

    public enum BenchAction { None, ResearchSpark, BuySiphon, InstallCollector, InstallDoser, TurnOnCollector, TurnOnDoser }

    public sealed class BenchCardState
    {
        public bool SparkUnlocked, SparkReady, Night, UpgradeOwned;
        public int SparkCost, UpgradeCost, Currency, BaseCapacity, UpgradedCapacity;
        public BenchModule Collector, Doser;
        public readonly List<BenchRequirement> Requirements = new List<BenchRequirement>();

        public void Clear()
        {
            Requirements.Clear();
            SparkUnlocked = SparkReady = Night = UpgradeOwned = false;
            Collector = Doser = default;
        }
    }

    /// <summary>Pure rules shared by the station cards and their EditMode tests.</summary>
    public static class StationCardRules
    {
        public const int ShiftStep = 5;

        public const int VatCapacity = 2;

        /// <summary>
        /// The research bench's one primary: the next thing to get, in order: Spark research, the bigger siphon, the
        /// collector, the doser; then switching an installed module on. None once everything is owned and on.
        /// </summary>
        public static BenchAction BenchPrimary(bool sparkUnlocked, bool upgradeOwned, BenchModule collector, BenchModule doser)
        {
            if (!sparkUnlocked) return BenchAction.ResearchSpark;
            if (!upgradeOwned) return BenchAction.BuySiphon;
            if (!collector.Owned) return BenchAction.InstallCollector;
            if (!doser.Owned) return BenchAction.InstallDoser;
            if (!collector.Enabled) return BenchAction.TurnOnCollector;
            if (!doser.Enabled) return BenchAction.TurnOnDoser;
            return BenchAction.None;
        }

        /// <summary>Why Spark research can't start now (a string key); null when it can.</summary>
        public static string SparkBlockedKey(bool ready, bool night, int currency, int cost)
        {
            if (!ready) return "card.bench.why.starters";
            if (!night) return "card.bench.why.night";
            return currency < cost ? "card.bench.why.coins" : null;
        }

        /// <summary>Why a helper module can't be installed now (a string key); null when it can.</summary>
        public static string ModuleBlockedKey(bool upgradeOwned, bool pondletKnown, bool night, int customers, int currency, int cost)
        {
            if (!upgradeOwned) return "card.bench.why.siphon";
            if (!pondletKnown) return "card.bench.why.pondlet";
            if (!night || customers > 0) return "card.bench.why.clearNight";
            return currency < cost ? "card.bench.why.coins" : null;
        }

        /// <summary>The rack compartment a store goes into: the chosen one while it is empty, else the first empty one.</summary>
        public static string RackTarget(IList<RackCompartment> compartments, string chosen)
        {
            string first = null;
            foreach (var compartment in compartments)
            {
                if (compartment.SpeciesName != null) continue;
                if (compartment.StorageId == chosen) return chosen;
                if (first == null) first = compartment.StorageId;
            }
            return first;
        }

        /// <summary>Why the vat can't start (a string key); null when one or two Pondlets are chosen and a Spark is in the siphon.</summary>
        public static string VatBlockedKey(int stored, int selected, int spark)
        {
            if (stored <= 0) return "card.vat.why.none";
            if (selected <= 0) return "card.vat.why.pick";
            return spark < 1 ? "card.vat.why.spark" : null;
        }

        /// <summary>A price after one press: ±1, or ±5 with Shift, kept inside the tag's allowed range.</summary>
        public static int PriceStep(int price, int minimum, int maximum, int direction, bool shift) =>
            Mathf.Clamp(price + direction * (shift ? ShiftStep : 1), minimum, maximum);

        /// <summary>The face a buyer type makes at a price (UX.5): happy for bargain or fair, neutral when pricey,
        /// a frown when too expensive, and the empty-wallet face when the price is past their budget.</summary>
        public static BuyerMood MoodFor(PriceReaction reaction, bool overBudget)
        {
            if (overBudget) return BuyerMood.Broke;
            switch (reaction)
            {
                case PriceReaction.Bargain: case PriceReaction.Fair: return BuyerMood.Happy;
                case PriceReaction.Pricey: case PriceReaction.None: return BuyerMood.Neutral;
                default: return BuyerMood.Frown;
            }
        }

        public static bool WillBuy(BuyerMood mood) => mood == BuyerMood.Happy || mood == BuyerMood.Neutral;

        /// <summary>The first culture (Bloom, Flow, Spark, Mist order) that siphon plus staged doses can't cover.</summary>
        public static CultureKind? MissingCulture(int[] available, int[] staged, int[] recipe)
        {
            for (int i = 0; i < 4; i++)
                if (recipe[i] > 0 && available[i] + staged[i] < recipe[i]) return (CultureKind)i;
            return null;
        }

        /// <summary>The counter's one primary action: Open shop at night; Serve while open and while closing, since an
        /// accepted checkout can still finish in the closing seconds.</summary>
        public static CounterPrimary PrimaryFor(HudDayPhase phase) =>
            phase == HudDayPhase.Night ? CounterPrimary.OpenShop : CounterPrimary.Serve;

        /// <summary>The word under a buyer's face (UX-00 mockup): Bargain!, Fair, Pricey, Nope, Can't afford.</summary>
        public static string MoodWordKey(PriceReaction reaction, bool overBudget)
        {
            if (overBudget) return "card.tank.word.broke";
            switch (reaction)
            {
                case PriceReaction.Bargain: return "card.tank.word.bargain";
                case PriceReaction.Fair: return "card.tank.word.fair";
                case PriceReaction.Pricey: case PriceReaction.None: return "card.tank.word.pricey";
                default: return "card.tank.word.nope";
            }
        }

        /// <summary>Faces that will buy at the tag's price; the tag's "3 of 4 will buy" counts these.</summary>
        public static int Buyers(IList<BuyerFace> faces)
        {
            int count = 0;
            foreach (var face in faces) if (WillBuy(MoodFor(face.Reaction, face.OverBudget))) count++;
            return count;
        }

        /// <summary>The counter's night warnings: how many displays are empty, or that none is stocked.</summary>
        public static string NightWarningKey(int displays, int emptyDisplays)
        {
            if (displays > 0 && emptyDisplays >= displays) return "card.counter.warn.noStock";
            if (emptyDisplays == 1) return "card.counter.warn.oneEmpty";
            return emptyDisplays > 1 ? "card.counter.warn.someEmpty" : null;
        }
    }
}
