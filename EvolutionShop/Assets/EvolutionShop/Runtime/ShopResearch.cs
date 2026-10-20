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
            if (station.Kind == StationKind.Conditioning && Model.MistResearchUnlocked &&
                Model.GetConditioner(station.ConditionerItemId).HasValue)
            { culture = CultureKind.Mist; return true; }
            if (station.Kind == StationKind.Upgrade && Model.SparkResearchUnlocked)
            { culture = CultureKind.Spark; return true; }
            return false;
        }

        public bool ResearchSpark()
        {
            if (Cycle.Phase != ShopDayPhase.Night)
            { Notify("Research Spark during preparation night. Unlocked cultures still grow during the day."); return false; }
            bool atBench = false;
            // UX-06: the notebook's research path offers it too, at the same bench.
            if (Panel == ShopPanel.Upgrade || Panel == ShopPanel.Research && ToolkitOwnsPanel(ShopPanel.Research))
                foreach (var station in World.Stations)
                    if (station.Kind == StationKind.Upgrade && AtStockStation(station)) atBench = true;
            if (!atBench) { Notify("Use E at the lab research bench in person."); return false; }
            var result = PerformAdvancedLab(Model.BuySparkResearch(), FxAdvancedLabEvent.SparkUnlocked);
            if (result.Success) Notify("Spark unlocked! Hold left mouse at this bench to collect. The journal has a new clue.", 6);
            return result.Success;
        }

        public bool ResearchMist()
        {
            if (Cycle.Phase != ShopDayPhase.Night)
            { Notify("Research Mist during preparation night. It keeps growing during active days."); return false; }
            if (!AtConditioner()) return false;
            var result = PerformAdvancedLab(Model.BuyMistResearch(), FxAdvancedLabEvent.MistUnlocked, selectedConditioner);
            if (result.Success) Notify("Mist unlocked! Close this panel and hold left mouse at the vat. The journal has an Airfin clue.", 6);
            return result.Success;
        }

        void DrawMistResearch()
        {
            DrawResearchUpgradeStrip();
            Space(); Heading("Mist culture / Airfin research");
            if (Model.MistResearchUnlocked)
            {
                Text("Installed / renewable Mist at this vat");
                Small("Close this panel and hold LEFT MOUSE at the vat to collect. Mist refills during active days and nights.");
                Text(Model.IsDiscovered("driftwing") ? "Driftwing discovered. Its recipe is in your journal." :
                    "Airfin clue: one Bloom + one Flow + one Mist. Use the incubator for 10 seconds.");
                return;
            }
            Text("One-time research: " + Config.MistResearchCost + " credits. Preparation night only.");
            Small("Requires Spark research, a discovered Lanternfin, and this placed vat. No extra floor machine or siphon upgrade.");
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && Model.MistResearchReady && Model.Currency >= Config.MistResearchCost && Cycle.Phase == ShopDayPhase.Night;
            Button("Research Mist / " + Config.MistResearchCost + " credits", () => ResearchMist());
            GUI.enabled = enabled;
            if (!Model.MistResearchReady) Small("Discover Lanternfin and place the conditioning vat first.");
            else if (Cycle.Phase != ShopDayPhase.Night) Small("Return during preparation night to research.");
            else if (Model.Currency < Config.MistResearchCost) Small("You need " + (Config.MistResearchCost - Model.Currency) + " more credits.");
        }

        static string RecipeText(SpeciesDefinition species, bool compact = false)
        {
            return species.Bloom + (compact ? "B" : " Bloom") + " + " + species.Flow + (compact ? "F" : " Flow") +
                (species.Spark > 0 ? " + " + species.Spark + (compact ? "S" : " Spark") : "") +
                (species.Mist > 0 ? " + " + species.Mist + (compact ? "M" : " Mist") : "");
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
