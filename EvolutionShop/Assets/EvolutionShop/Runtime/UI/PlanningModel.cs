using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>The notebook's tabs (UX.6). Journal, day report and pause join with UX-07.</summary>
    public enum PlanningTab { Crew, Automation, Arrange, Research }

    public enum CrewRole { Cashier, Stocker, Technician }

    /// <summary>One crew member's card: what the session knows about their job right now.</summary>
    public sealed class CrewMemberState
    {
        public CrewRole Role;
        public bool Hired, Scheduled, Paused, PaidToday, OnDuty, Blocked, Working, HasWork;
        public int Wage;
        public void Clear() { Hired = Scheduled = Paused = PaidToday = OnDuty = Blocked = Working = HasWork = false; Wage = 0; }
    }

    public sealed class CrewViewState
    {
        public bool Night, Trading;
        public int Currency, Day;
        public readonly CrewMemberState[] Members = { new CrewMemberState { Role = CrewRole.Cashier }, new CrewMemberState { Role = CrewRole.Stocker }, new CrewMemberState { Role = CrewRole.Technician } };
        public void Clear()
        {
            Night = Trading = false; Currency = Day = 0;
            for (int i = 0; i < Members.Length; i++) { Members[i].Clear(); Members[i].Role = (CrewRole)i; }
        }
    }

    /// <summary>A display slot Nori keeps stocked: null target is off, ShopStocking.AnySpecies is any rack species.</summary>
    public struct TargetRow { public string TankId, Name, Target; }
    public struct TargetOption { public string Id, Name, Look; }

    public sealed class StockerTargetsState
    {
        public readonly List<TargetRow> Rows = new List<TargetRow>();
        public readonly List<TargetOption> Options = new List<TargetOption>();
        public void Clear() { Rows.Clear(); Options.Clear(); }
    }

    public struct QueueRow { public string SpeciesId, Name, Look; public int Count; }

    public sealed class TechnicianQueueState
    {
        public string ActiveName, ActiveLook;
        public int MaxCount;
        public readonly List<QueueRow> Rows = new List<QueueRow>();
        /// <summary>Every known recipe with how many starts it already has waiting.</summary>
        public readonly List<QueueRow> Known = new List<QueueRow>();
        public void Clear() { ActiveName = ActiveLook = null; MaxCount = 0; Rows.Clear(); Known.Clear(); }
    }

    public struct RecipeRow { public string SpeciesId, Name, Look; public int Bloom, Flow, Spark, Mist; public bool Chosen; }
    public struct DestinationOption { public string TankId, Name; public bool Chosen; }

    public sealed class AutomationViewState
    {
        public bool CollectorOwned, CollectorOn, DoserOwned, DoserOn;
        public string RecipeId, RecipeName, RecipeLook, DestinationId, Status;
        public int Desired, MaxDesired;
        public bool Maintain, Pending, Paused, BlockedBySave;
        public readonly List<RecipeRow> Rows = new List<RecipeRow>();
        public readonly List<DestinationOption> Destinations = new List<DestinationOption>();
        public void Clear()
        {
            CollectorOwned = CollectorOn = DoserOwned = DoserOn = false;
            RecipeId = RecipeName = RecipeLook = DestinationId = Status = null;
            Desired = MaxDesired = 0;
            Maintain = Pending = Paused = BlockedBySave = false;
            Rows.Clear(); Destinations.Clear();
        }
    }

    public enum ArrangeTab { Furniture, Decor, Owned, Lab }

    /// <summary>A catalogue tile: something to buy and place, an expansion, or an essential station to move.</summary>
    public struct ArrangeTile
    {
        public string Id, Name, Look;
        public int Price, Slots, Appeal;
        public bool Expansion, Move;
        /// <summary>Why it can't be used now (a string key, maybe with Amount); null when it can.</summary>
        public string BlockedKey;
        public int Amount;
    }

    public enum OwnedPlace { Store, Lab, Storage }

    public struct OwnedRow
    {
        public string ItemId, DefinitionId, Name, Label;
        public OwnedPlace Place;
        public bool Stocked, Reserved;
        public int Resale;
    }

    public sealed class ArrangeViewState
    {
        public bool CanArrange;
        public int Currency, StoreTier, LabTier, Appeal, DecorCount, DecorLimit, DisplayCount, DisplayLimit, RackCount, RackLimit;
        public string PendingSaleId;
        public readonly List<ArrangeTile> Furniture = new List<ArrangeTile>();
        public readonly List<ArrangeTile> Decor = new List<ArrangeTile>();
        public readonly List<ArrangeTile> Lab = new List<ArrangeTile>();
        public readonly List<OwnedRow> Owned = new List<OwnedRow>();
        public void Clear()
        {
            CanArrange = false;
            Currency = StoreTier = LabTier = Appeal = DecorCount = DecorLimit = DisplayCount = DisplayLimit = RackCount = RackLimit = 0;
            PendingSaleId = null;
            Furniture.Clear(); Decor.Clear(); Lab.Clear(); Owned.Clear();
        }
    }

    /// <summary>One culture on the research path: Bloom and Flow come with the shop, Spark and Mist are researched.</summary>
    public sealed class ResearchStep
    {
        public CultureKind Culture;
        public bool Unlocked, Here;
        public int Cost;
        public readonly List<BenchRequirement> Requirements = new List<BenchRequirement>();
        public string BlockedKey;
        public int Amount;
        public void Clear() { Unlocked = Here = false; Cost = 0; Requirements.Clear(); BlockedKey = null; Amount = 0; }
    }

    public sealed class ResearchViewState
    {
        public readonly ResearchStep[] Steps = { new ResearchStep { Culture = CultureKind.Bloom }, new ResearchStep { Culture = CultureKind.Flow },
            new ResearchStep { Culture = CultureKind.Spark }, new ResearchStep { Culture = CultureKind.Mist } };
        public bool SiphonOwned, SiphonCanBuy;
        public int SiphonCost, SiphonFrom, SiphonTo, Currency;
        public string SiphonBlockedKey;
        public void Clear()
        {
            foreach (var step in Steps) { var kind = step.Culture; step.Clear(); step.Culture = kind; }
            SiphonOwned = SiphonCanBuy = false; SiphonCost = SiphonFrom = SiphonTo = Currency = 0; SiphonBlockedKey = null;
        }
    }

    /// <summary>
    /// UX-06 planning rules: pure decisions the notebook's views take from their state, kept apart so EditMode tests
    /// cover them. Every command still goes through the session, whose own checks stay authoritative.
    /// </summary>
    public static class PlanningRules
    {
        public const int AppealCap = 30;

        /// <summary>Where a tab can be used, said under it when it can't be used from here.</summary>
        public static string TabWhereKey(PlanningTab tab)
        {
            switch (tab)
            {
                case PlanningTab.Crew: return "plan.tab.where.counter";
                case PlanningTab.Arrange: return "plan.tab.where.night";
                default: return "plan.tab.where.bench";
            }
        }

        /// <summary>The panel a tab opens: the crew tab is the staff panel, research has its own.</summary>
        public static ShopPanel TabPanel(PlanningTab tab)
        {
            switch (tab)
            {
                case PlanningTab.Crew: return ShopPanel.Staff;
                case PlanningTab.Automation: return ShopPanel.Automation;
                case PlanningTab.Arrange: return ShopPanel.Arrange;
                default: return ShopPanel.Research;
            }
        }

        /// <summary>The tab a planning panel belongs to; Nori's and Moss's pages are part of the crew.</summary>
        public static PlanningTab TabOf(ShopPanel panel)
        {
            switch (panel)
            {
                case ShopPanel.Automation: return PlanningTab.Automation;
                case ShopPanel.Arrange: return PlanningTab.Arrange;
                case ShopPanel.Research: return PlanningTab.Research;
                default: return PlanningTab.Crew;
            }
        }

        /// <summary>One short status word for a crew card (a string key).</summary>
        public static string CrewStatusKey(CrewMemberState m, bool night)
        {
            if (!m.Hired) return "plan.crew.status.notHired";
            if (m.Paused) return "plan.crew.status.paused";
            if (!m.Scheduled) return "plan.crew.status.off";
            if (!m.PaidToday) return night ? "plan.crew.status.unpaid" : "plan.crew.status.offToday";
            if (m.Blocked) return "plan.crew.status.stuck";
            if (m.Working)
                return m.Role == CrewRole.Cashier ? "plan.crew.status.serving" : m.Role == CrewRole.Stocker ? "plan.crew.status.stocking" : "plan.crew.status.labWork";
            if (night && m.Role == CrewRole.Cashier) return "plan.crew.status.ready";
            if (!m.HasWork)
                return m.Role == CrewRole.Cashier ? "plan.crew.status.waiting" : m.Role == CrewRole.Stocker ? "plan.crew.status.noTargets" : "plan.crew.status.noOrders";
            return "plan.crew.status.onDuty";
        }

        /// <summary>A status that needs the player (stuck, unpaid) is shown as a warning.</summary>
        public static bool CrewStatusWarns(string key) => key == "plan.crew.status.stuck" || key == "plan.crew.status.unpaid";

        /// <summary>Hiring, scheduling and dismissing happen at night only (ShopStaff); pausing works any time.</summary>
        public static bool CrewNightOnly(bool night) => night;

        /// <summary>A scheduled crew member still unpaid tonight shows Pay; the wage is taken when it can be.</summary>
        public static bool CrewNeedsPay(CrewMemberState m, bool night) => night && m.Hired && m.Scheduled && !m.Paused && !m.PaidToday;

        /// <summary>Why a catalogue item can't be placed: the display or rack limit, a locked item, or coins.</summary>
        public static string TileBlockedKey(bool atLimit, bool locked, int currency, int price, out int amount)
        {
            amount = 0;
            if (locked) return "plan.arrange.why.locked";
            if (atLimit) return "plan.arrange.why.limit";
            if (currency < price) { amount = price - currency; return "plan.arrange.why.coins"; }
            return null;
        }

        /// <summary>Decor adds appeal only for a type not yet in the front store.</summary>
        public static int DecorAppeal(bool typeAlreadyInStore) => typeAlreadyInStore ? 0 : 10;

        /// <summary>Why the one-off batch can't start (a string key); null when it can.</summary>
        public static string PrepareBlockedKey(bool recipeChosen, bool moduleOn, bool pending, bool maintain, bool blockedBySave)
        {
            if (blockedBySave) return "plan.auto.why.save";
            if (maintain) return "plan.auto.why.maintain";
            if (pending) return "plan.auto.why.pending";
            if (!recipeChosen) return "plan.auto.why.recipe";
            if (!moduleOn) return "plan.auto.why.module";
            return null;
        }

        /// <summary>Why keeping stock can't be switched on: it needs a recipe and a display to fill.</summary>
        public static string MaintainBlockedKey(bool recipeChosen, bool destinationChosen)
        {
            if (!recipeChosen) return "plan.auto.why.recipe";
            if (!destinationChosen) return "plan.auto.why.destination";
            return null;
        }

        /// <summary>The research path's one primary: the first locked culture that can be researched where you stand.</summary>
        public static CultureKind? ResearchPrimary(ResearchViewState s)
        {
            foreach (var step in s.Steps)
                if (!step.Unlocked) return step.Here ? step.Culture : (CultureKind?)null;
            return null;
        }
    }
}
