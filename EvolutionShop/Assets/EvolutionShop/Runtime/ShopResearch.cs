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

        static string RecipeText(SpeciesDefinition species, bool compact = false)
        {
            return species.Bloom + (compact ? "B" : " Bloom") + " + " + species.Flow + (compact ? "F" : " Flow") +
                (species.Spark > 0 ? " + " + species.Spark + (compact ? "S" : " Spark") : "") +
                (species.Mist > 0 ? " + " + species.Mist + (compact ? "M" : " Mist") : "");
        }

    }
}
