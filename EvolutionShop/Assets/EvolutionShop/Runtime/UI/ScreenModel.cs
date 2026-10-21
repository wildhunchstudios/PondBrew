using System.Collections.Generic;

namespace EvolutionShop
{
    /// <summary>The journal's tabs (UX.6).</summary>
    public enum JournalTab { Creatures, Customers, Shop }

    /// <summary>The settings card's tabs in Pause (UX.6).</summary>
    public enum SettingsTab { Controls, Sound, Comfort }

    /// <summary>What the help screen shows on its right-hand page.</summary>
    public enum HelpPage { Lesson, Controls, NextPlan, Upgrade, Receipts }

    // ---------- welcome ----------

    public sealed class WelcomeState
    {
        public TutorialMode Mode;
        public bool AutosaveBlocked, SaveError;
        public int LessonNumber;
        public TutorialLesson Lesson;
    }

    // ---------- help ----------

    public sealed class HelpState
    {
        public HelpPage Page;
        public bool TutorialActive, Practice, Assisted, CanShowTarget, Directions, StaffHired, OnActiveLesson, Paused;
        public TutorialLesson Lesson, ActiveLesson;
        public string Recovery;
        // Lesson text arguments.
        public string Recipe = "";
        public int IncubationSeconds, StandardPrice, TagPrice, IntroPatience;
        public bool IntroArrivals;
        // Your shop right now.
        public int Day, Money, Bloom, Flow, Spark, Mist, StagedBloom, StagedFlow, Reserved, Capacity, Stocked;
        public bool Night, Open, SparkKnown, MistKnown;
        public string IncubatorName = "";
        public IncubatorPhase IncubatorPhase;
        public int IncubatorSeconds;
        // Next goal and the upgrade preview.
        public int UpgradeCost, BaseCapacity, UpgradedCapacity;
        public float BaseCollect, UpgradedCollect;
        public bool UpgradeOwned;
        // Receipts recovery.
        public string FirstName = "", PracticeName = "";
        public int FirstDay, PracticeDay;
    }

    // ---------- journal ----------

    public sealed class JournalCreature
    {
        public string Id = "", Name = "", Family = "", Look = "", ClueKey = "";
        public bool Discovered;
        public int Bloom, Flow, Spark, Mist, Standard, Price;
    }

    public sealed class JournalCustomer
    {
        public string Id = "", Name = "";
        public int Budget, TolerancePercent, FavoritePercent, BargainPercent, Patience;
        public readonly List<string> FavoriteLooks = new List<string>();
        public readonly List<string> FavoriteNames = new List<string>();
    }

    public sealed class JournalState
    {
        public readonly List<JournalCreature> Creatures = new List<JournalCreature>();
        public readonly List<JournalCustomer> Customers = new List<JournalCustomer>();
        public int Discovered, Reputation, ReputationMax, Tier, NextThreshold, Appeal, AppealMax, PriceBonus, BudgetBonus;
        public float ArrivalSeconds;
        public string TierName = "", NextTierName = "";
        public bool ReturnsToHelp;
        public void Clear() { Creatures.Clear(); Customers.Clear(); }
    }

    // ---------- day report ----------

    public sealed class ReportReason
    {
        public string Key; public int Count;
        public ReportReason(string key, int count) { Key = key; Count = count; }
    }

    public sealed class ReportState
    {
        public bool Has, Partial, Guided;
        public int Day, Sales, Visitors, Missed, Wages, Reputation, Tier, NextThreshold, StockedOpen, Restocked, StockedClose;
        public long Revenue, Profit, Cash;
        public string TierName = "";
        public readonly List<ReportReason> Reasons = new List<ReportReason>();
        public string TipKey = "";
        public object[] TipArgs = new object[0];
    }

    // ---------- pause ----------

    public sealed class PauseState
    {
        public bool Reset, TutorialActive, Dev, AutosaveBlocked, WaitingForSlapKey, ReducedMotion, LowDetail, ReducedGags;
        public string SaveError = "", SlapKey = "";
        public float Sensitivity, Volume, Ambience, Humor;
        public int Day;
        public bool Night;
    }

    /// <summary>Pure rules for the UX-07 screens; EditMode tests cover them.</summary>
    public static class ScreenRules
    {
        public const float MinSensitivity = 0.25f, MaxSensitivity = 2.5f;

        /// <summary>The lesson's step number on the help screen (Orient is step 1).</summary>
        public static int LessonNumber(TutorialLesson lesson) => (int)lesson - (int)TutorialLesson.Orient + 1;

        /// <summary>The string key of a lesson's short name, such as help.lesson.prepare.</summary>
        public static string LessonKey(TutorialLesson lesson) => "help.lesson." + lesson.ToString().ToLowerInvariant();

        /// <summary>Why visitors left without buying, only the reasons that happened, in a fixed order.</summary>
        public static void MissedReasons(TradeDaySave report, List<ReportReason> into)
        {
            into.Clear();
            if (report == null) return;
            Add(into, "report.why.pricey", report.tooExpensive);
            Add(into, "report.why.noStock", report.noStock);
            Add(into, "report.why.impatient", report.impatient);
            Add(into, "report.why.closed", report.closed);
            Add(into, "report.why.interrupted", report.interrupted);
            Add(into, "report.why.route", report.pathProblems);
            Add(into, "report.why.slapped", report.slapped);
        }

        static void Add(List<ReportReason> into, string key, int count) { if (count > 0) into.Add(new ReportReason(key, count)); }

        public static int Missed(TradeDaySave report) => report == null ? 0 :
            report.noStock + report.impatient + report.closed + report.interrupted + report.pathProblems + report.tooExpensive + report.slapped;

        /// <summary>
        /// Tonight's one tip on the receipt: the same choice, in the same order, as the classic report's
        /// ReportPreparationTip, as a string key and its arguments.
        /// </summary>
        public static string ReportTip(TradeDaySave report, int emptyDisplays, IncubatorPhase incubator, string readyName,
            bool upgradeOwned, int currency, int upgradeCost, out object[] args)
        {
            args = new object[0];
            if (incubator == IncubatorPhase.Ready)
            {
                args = new object[] { readyName ?? "" };
                return emptyDisplays > 0 ? "report.tip.stockReady" : "report.tip.readyWaits";
            }
            if (emptyDisplays > 0)
            {
                args = new object[] { emptyDisplays };
                return emptyDisplays == 1 ? "report.tip.fillOne" : "report.tip.fillMany";
            }
            if (report != null)
            {
                if (report.tooExpensive > 0 && report.tooExpensive >= report.impatient && report.tooExpensive >= report.noStock)
                { args = new object[] { report.tooExpensive }; return "report.tip.pricey"; }
                if (report.slapped > 0) return "report.tip.slapped";
                if (report.impatient > 0 && report.impatient >= report.noStock) return "report.tip.impatient";
                if (report.noStock > 0) return "report.tip.noStock";
            }
            if (!upgradeOwned && currency >= upgradeCost) { args = new object[] { upgradeCost }; return "report.tip.upgrade"; }
            return incubator == IncubatorPhase.Incubating ? "report.tip.growing" : "report.tip.ready";
        }

        /// <summary>Reputation tiers as the game sets them (ShopReputation): what a tier gives new visitors.</summary>
        public static float TierArrivalSeconds(int tier) => tier >= 2 ? 4f : tier == 1 ? 5f : 6f;
        public static int TierPricePercent(int tier) => tier * 10;
        public static int TierBudget(int tier) => tier * 2;

        /// <summary>A 0..1 fill for a bar, safe for an empty or zero maximum.</summary>
        public static float Fill(float value, float max) => max <= 0 ? 0 : value <= 0 ? 0 : value >= max ? 1 : value / max;

        /// <summary>A volume or setting shown as a whole percent.</summary>
        public static int Percent(float value01) => (int)System.Math.Round(System.Math.Max(0, System.Math.Min(1, value01)) * 100);
    }
}
