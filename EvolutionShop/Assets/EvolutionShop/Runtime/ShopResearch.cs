using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public bool TryStationCulture(Station station, out CultureKind culture)
        {
            culture = CultureKind.Bloom;
            if (!station) return false;
            if (station.Kind == StationKind.Bloom) return true;
            if (station.Kind == StationKind.Flow) { culture = CultureKind.Flow; return true; }
            if (station.Kind == StationKind.Upgrade && Model.SparkResearchUnlocked)
            { culture = CultureKind.Spark; return true; }
            return false;
        }

        public bool ResearchSpark()
        {
            if (Cycle.Phase != ShopDayPhase.Night)
            { Notify("Research Spark during preparation night. Unlocked cultures still grow during the day."); return false; }
            bool atBench = false;
            if (Panel == ShopPanel.Upgrade)
                foreach (var station in World.Stations)
                    if (station.Kind == StationKind.Upgrade && AtStockStation(station)) atBench = true;
            if (!atBench) { Notify("Use E at the lab research bench in person."); return false; }
            var result = Perform(Model.BuySparkResearch());
            if (result.Success) Notify("Spark unlocked! Hold left mouse at this bench to collect. The journal has a new clue.", 6);
            return result.Success;
        }

        static string RecipeText(SpeciesDefinition species, bool compact = false)
        {
            return species.Bloom + (compact ? "B" : " Bloom") + " + " + species.Flow + (compact ? "F" : " Flow") +
                (species.Spark > 0 ? " + " + species.Spark + (compact ? "S" : " Spark") : "");
        }

        void DrawSparkResearch()
        {
            Heading("Spark research");
            if (Model.SparkResearchUnlocked)
            {
                Text("Installed / renewable Spark culture");
                Small("Close this panel and hold LEFT MOUSE at this bench to collect Spark. It refills during active days and nights. E opens research and siphon equipment.");
                Text(Model.IsDiscovered("lanternfin") ? "Lanternfin discovered. Its known recipe is in your journal and incubator." : "New clue: A balanced Bloom/Flow pair needs one Spark.");
                Small("Incubator / " + Config.IncubationSeconds + " seconds / fits the original four-unit siphon.");
                return;
            }
            Text("Unlock a third renewable culture at this bench and a clue for a new creature.");
            Small("Required discoveries: " + (Model.IsDiscovered("pondlet") ? "[done] " : "[ ] ") + "Pondlet / " +
                (Model.IsDiscovered("bubblebelly") ? "[done] " : "[ ] ") + "Bubblebelly / " +
                (Model.IsDiscovered("ribbonfin") ? "[done] " : "[ ] ") + "Ribbonfin");
            Small("One-time cost: " + Config.SparkResearchCost + " credits. Preparation night only. Uses this bench; no new floor space or siphon upgrade required.");
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && Model.SparkResearchReady && Model.Currency >= Config.SparkResearchCost && Cycle.Phase == ShopDayPhase.Night;
            Button("Research Spark / " + Config.SparkResearchCost + " credits", () => ResearchSpark());
            GUI.enabled = enabled;
            if (!Model.SparkResearchReady) Small("Grow the missing starter species first; the journal contains their clues.");
            else if (Cycle.Phase != ShopDayPhase.Night) Small("Return during preparation night to research.");
            else if (Model.Currency < Config.SparkResearchCost) Small("You need " + (Config.SparkResearchCost - Model.Currency) + " more credits.");
        }
    }
}
