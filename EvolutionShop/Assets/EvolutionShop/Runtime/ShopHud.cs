using UnityEngine;

namespace EvolutionShop
{
    // The HUD's words the UI Toolkit reads: the next-step line, and the last day report with its tip. Since UX-09 nothing here
    // draws; the IMGUI HUD and panels these once fed are gone.
    public sealed partial class ShopSession
    {
        ShopTradeLedger reportDisplayLedger;
        TradeDaySave reportDisplaySnapshot;
        int reportDisplayDay = -1;

        string Objective()
        {
            if (AutosaveBlocked) return "Your save is protected; progress cannot be saved. Press ESC for save recovery.";
            if (!string.IsNullOrEmpty(SaveError)) return "Progress is not saved. Press ESC to retry saving or reload your last saved shop.";
            if (Customers.NextToServe != null && Customers.NextToServe.Phase == CustomerPhase.Queued)
                return Tutorial.Mode == TutorialMode.Active ? "A customer is waiting. Serve at the front counter yourself." : Staff.IsOnDuty(Cycle.DayNumber) ? "Pip is handling checkout. Keep stock ready, or help at the counter." : "A customer is waiting. Use the front counter to serve.";
            if (Customers.Visit != null && Customers.Visit.Phase == CustomerPhase.ToCounter && (Tutorial.Mode == TutorialMode.Active || !Staff.IsOnDuty(Cycle.DayNumber))) return "A creature is reserved. Head to the front counter.";
            if (Cycle.Phase == ShopDayPhase.Closing) return "Closing up. Unsold stock stays safe for tomorrow.";
            string tutorialObjective = TutorialObjective();
            if (!string.IsNullOrEmpty(tutorialObjective)) return tutorialObjective;
            if (Model.Incubator.Phase == IncubatorPhase.Ready) return Model.StorageIds.Length > 0 ? "Use an empty display or lab rack to store the ready creature." : "Head to a display tank in the front store.";
            if (ReadyConditionedCount > 0) return "Use E at an empty rack or display to collect vat output.";
            if (Model.Incubator.Phase == IncubatorPhase.Incubating) return "New life is growing. Gather your next cultures.";
            if (DisplayedStockCount() == 0 && StoredSpecimenCount > 0) return "Use E at an empty display to choose stored stock.";
            if (DisplayedStockCount() > 0) return Cycle.Phase == ShopDayPhase.Night ? "Stock is ready. Open for the day at the front counter." : Staff.IsOnDuty(Cycle.DayNumber) ? "Pip handles checkout. Keep your displays stocked." : "Let customers browse, then serve their orders at the counter.";
            if (Model.DiscoveredSpeciesIds.Count == 0) return "Back lab: mix one Bloom + one Flow.";
            if (Model.SparkResearchReady && !Model.SparkResearchUnlocked) return "At night: research Spark at the lab bench / " + Config.SparkResearchCost + " credits.";
            if (Model.SparkResearchUnlocked && !Model.IsDiscovered("lanternfin")) return "Try one Bloom + one Flow + one Spark.";
            if (Model.MistResearchReady && !Model.MistResearchUnlocked) return "At night: research Mist at the placed vat / " + Config.MistResearchCost + " credits.";
            if (Model.MistResearchUnlocked && !Model.IsDiscovered("driftwing")) return "Try one Bloom + one Flow + one Mist for an Airfin.";
            if (!Model.UpgradeOwned && Model.Currency >= Config.UpgradeCost) return "Buy the larger siphon at the back lab bench.";
            if (!Model.UpgradeOwned) return "Grow and sell creatures. Save " + Config.UpgradeCost + " credits for a larger siphon.";
            if (Model.DiscoveredSpeciesIds.Count < 3) return "Discover the remaining creatures by changing one dose.";
            return Cycle.Phase == ShopDayPhase.Night ? "Prepare your favorite creatures, then open at the counter." : "Keep stock flowing, or close for the night at the counter.";
        }
        TradeDaySave LastReportForDisplay()
        {
            int day = Reports == null ? 0 : Reports.LastDayNumber;
            // Last is a defensive snapshot. Copy only when its owner or finalized day changes,
            // instead of allocating a fresh report every time a view reads it.
            if (!ReferenceEquals(reportDisplayLedger, Reports) || reportDisplayDay != day)
            {
                reportDisplayLedger = Reports;
                reportDisplayDay = day;
                reportDisplaySnapshot = day == 0 ? null : Reports.Last;
            }
            return reportDisplaySnapshot;
        }
        string ReportPreparationTip(TradeDaySave report)
        {
            int empty = 0;
            foreach (string tank in Model.TankIds) if (!Model.GetTank(tank).HasValue) empty++;
            var incubator = Model.Incubator;
            if (incubator.Phase == IncubatorPhase.Ready)
            {
                if (empty > 0)
                    return "Your " + Model.GetSpecies(incubator.SpeciesId).DisplayName + " is ready. Press E at an empty front tank to stock it before opening.";
                return "All display slots are stocked. Keep the finished specimen in its incubator until a sale frees a tank.";
            }
            if (empty > 0)
            {
                string tankText = empty == 1 ? "Fill the empty display" : "Fill your empty displays";
                if (incubator.Phase == IncubatorPhase.Incubating)
                    return tankText + " before opening. While your specimen grows, collect free cultures for the next one.";
                return tankText + " before opening. One Bloom + one Flow makes a Pondlet; grow and transfer one specimen at a time.";
            }
            if (report.tooExpensive > 0 && report.tooExpensive >= report.impatient && report.tooExpensive >= report.noStock)
                return (report.tooExpensive == 1 ? "A customer" : report.tooExpensive + " customers") +
                    " refused your prices. Press E at a display to lower its tag: \"Pricey\" still sells, \"Too expensive\" does not.";
            if (report.slapped > 0) return "Slapped customers leave without paying. Serve at the counter with E; the slap key is a separate optional gag.";
            if (report.impatient > 0 && report.impatient >= report.noStock)
                return "Displays are ready. To avoid impatient departures, return to the counter promptly when a creature is reserved.";
            if (report.noStock > 0)
                return "Displays are ready. To avoid stockouts, start another specimen before opening and refill a tank after each sale.";
            if (!Model.UpgradeOwned && Model.Currency >= Config.UpgradeCost)
                return "Buy the " + Config.UpgradeCost + "-credit siphon upgrade in the back lab for a larger chamber and faster collection.";
            if (incubator.Phase == IncubatorPhase.Incubating)
                return "Your displays are ready and the next specimen is growing. Open at the front counter when you are ready to serve.";
            return "Your displays are ready. Start the next specimen in the back lab, then open at the front counter when you are ready.";
        }
    }
}
