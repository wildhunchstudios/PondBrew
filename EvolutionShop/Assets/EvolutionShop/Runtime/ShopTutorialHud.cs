using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        // Display-only access for runtime validation. This resolver never records learning.
        public string TutorialObjectiveForDisplay => Objective();

        public string TutorialPracticeOutcomeForDisplay
        {
            get
            {
                bool completed = Tutorial.Mode == TutorialMode.Completed || Tutorial.Mode == TutorialMode.Active &&
                    (ActiveTutorialLesson == TutorialLesson.CloseAndReview || ActiveTutorialLesson == TutorialLesson.NextPlan);
                if (!completed) return Tutorial.PracticeAssisted ? "ASSISTED PRACTICE / still in progress" : "Practice is still in progress.";
                return Tutorial.PracticeAssisted
                    ? "ASSISTED PRACTICE / you completed the second loop with help."
                    : "INDEPENDENT PRACTICE / you completed the second loop without requested help.";
            }
        }

        public string TutorialUpgradePreviewForDisplay
        {
            get
            {
                string preview = "Siphon MK II: " + Config.UpgradeCost + " credits. Your cash: " + Model.Currency + " credits.";
                if (Model.UpgradeOwned) return preview + " Already installed.";
                return preview + (Model.Currency >= Config.UpgradeCost
                    ? " You can afford it; buying is optional."
                    : " Save " + (Config.UpgradeCost - Model.Currency) + " more, or keep growing with your current siphon.");
            }
        }

        string TutorialObjective()
        {
            if (Tutorial.Mode != TutorialMode.Active) return null;
            var lesson = ActiveTutorialLesson;
            // Detailed practice coaching exists only after an explicit Show me request.
            // Opening or drawing a card never enables world directions by itself.
            if (lesson == TutorialLesson.Practice)
            {
                if (!TutorialPracticeGuidanceRequested)
                    return TutText("tut.obj.practice", "Grow and sell another creature yourself. F1 opens help if you need it.");
                lesson = TutorialGuidanceLesson;
            }
            string recovery = TutorialGuidanceRecovery;
            if (recovery != null) return recovery;
            recovery = TutorialPreparationRecovery(lesson);
            if (recovery != null) return recovery;
            switch (lesson)
            {
                case TutorialLesson.Orient:
                    return TutText("tut.obj.orient", "Find your back lab. WASD to move, mouse to look. Approach the lab door to open it.");
                case TutorialLesson.Collect:
                    return TutText("tut.obj.collect", "Collect " + TutorialCollectionTarget() + ". Aim at its grower and hold left mouse.", TutorialCollectionTarget());
                case TutorialLesson.Prepare:
                    return TutText("tut.obj.prepare", "Press E at the incubator. Add " + TutorialStarterRecipe() + ".", TutorialStarterRecipe());
                case TutorialLesson.Grow:
                    if (Model.Incubator.Phase == IncubatorPhase.Incubating)
                        return TutText("tut.obj.growing", TutorialIncubatorName() + " is growing / " + Mathf.CeilToInt(Model.Incubator.SecondsRemaining) + "s. It will wait in the incubator.", TutorialIncubatorName(), Mathf.CeilToInt(Model.Incubator.SecondsRemaining));
                    if (Model.Incubator.Phase == IncubatorPhase.Ready)
                        return TutText("tut.obj.occupied", "The incubator is occupied. Stock its ready creature, then start your own batch. F1 for help.");
                    return TutText("tut.obj.grow", "At the incubator, prepare a recipe and choose Begin incubation.");
                case TutorialLesson.Display:
                    if (!TutorialHasEmptyDisplay())
                        return Cycle.Phase == ShopDayPhase.Night ? TutText("tut.obj.fullNight", "Your displays are full. Open at the front counter to make room through sales.") : TutText("tut.obj.fullDay", "Your displays are full. Serve a sale to free a tank; the ready creature can wait.");
                    return Model.Incubator.Phase == IncubatorPhase.Ready && StoredSpecimenCount == 0 && ReadyConditionedCount == 0
                        ? TutText("tut.obj.displayDirect", "Press E at an empty front display to transfer your ready creature directly from the incubator.")
                        : TutText("tut.obj.display", "Press E at an empty front display. If stock choices appear, select your ready or stored creature.");
                case TutorialLesson.PriceAndOpen:
                    if (!TutorialTrackedCreatureIsDisplayed() && !TutorialHasEmptyDisplay())
                        return TutText("tut.obj.fullPrice", "Your displays are full. Open at the front counter and sell existing stock to make room for your creature.");
                    return (Tutorial.Facts & TutorialFacts.PriceViewed) != 0
                        ? TutText("tut.obj.open", "Press E at the front counter, then choose Open for day.")
                        : TutText("tut.obj.price", "Press E at a stocked display to check its price. Then open at the front counter.");
                case TutorialLesson.Serve:
                    if (!TutorialTrackedCreatureIsDisplayed())
                        return TutorialHasEmptyDisplay()
                            ? TutText("tut.obj.restock", "Restock your creature from its rack or output into an empty display, then return to the counter.")
                            : TutText("tut.obj.offDisplay", "Your creature is waiting off display. Open or serve at the counter to free a tank, then stock it yourself.");
                    if (Cycle.Phase == ShopDayPhase.Night)
                        return TutText("tut.obj.serveNight", "Open at the front counter when your display is stocked. Then serve its customer yourself.");
                    return TutText("tut.obj.serve", "Let a customer choose a creature. Serve their order at the front counter yourself.");
                case TutorialLesson.CloseAndReview:
                    return Cycle.Phase == ShopDayPhase.Night ? TutText("tut.obj.review", "Review your trading results at the front counter, then continue preparation.") : TutText("tut.obj.close", "Close for the night at the front counter when ready. Review the day's results.");
                case TutorialLesson.NextPlan:
                    return TutText("tut.obj.nextPlan", "Choose your next goal at the front counter or with F1, then continue running your shop.");
                default: return null;
            }
        }

        string TutorialPreparationRecovery(TutorialLesson lesson)
        {
            if (lesson != TutorialLesson.Collect && lesson != TutorialLesson.Prepare && lesson != TutorialLesson.Grow) return null;
            if (Model.IsIncubatorReservedForTechnician)
                return TutText("tut.fix.mossIncubator", "Moss reserved the incubator. Pause lab work at Counter > Staff to grow this creature yourself.");
            var starter = Model.GetSpecies("pondlet");
            var inc = Model.Incubator;
            if (starter == null || inc.Phase != IncubatorPhase.Dosing) return null;
            // Preserve any legitimate alternative recipe instead of 'repairing' it into a Pondlet.
            foreach (var recipe in Config.Species)
                if (recipe.Bloom == inc.Bloom && recipe.Flow == inc.Flow && recipe.Spark == inc.Spark && recipe.Mist == inc.Mist &&
                    (recipe.Spark == 0 || Model.SparkResearchUnlocked) && (recipe.Mist == 0 || Model.MistResearchUnlocked))
                    return lesson == TutorialLesson.Collect ? null : TutText("tut.fix.recipeReady", recipe.DisplayName + " recipe ready. Choose Begin incubation at the incubator.", recipe.DisplayName);

            CultureKind? missing = null;
            for (int i = 0; i < 4; i++)
            {
                var kind = (CultureKind)i;
                if (Model.Available(kind) + Model.Staged(kind) < TutorialRecipeDose(starter, kind)) { missing = kind; break; }
            }
            if (missing.HasValue && Model.ReservedCultureCount >= Model.Capacity)
            {
                // Pick a real excess, keeping enough total culture for the starter recipe.
                for (int i = 0; i < 4; i++)
                {
                    var kind = (CultureKind)i;
                    if (Model.Available(kind) + Model.Staged(kind) <= TutorialRecipeDose(starter, kind)) continue;
                    if (Model.Available(kind) > 0)
                        return TutText("tut.fix.release", "Siphon full. At the incubator, choose Release 1 " + kind + " to make room for " + missing.Value + ", then collect it.", kind, missing.Value);
                    return TutText("tut.fix.returnRelease", "Siphon full. At the incubator, press - " + kind + " once, then Release 1 " + kind + " to make room for " + missing.Value + ". Returning alone keeps the slot full.", kind, missing.Value);
                }
            }
            if (inc.Bloom + inc.Flow + inc.Spark + inc.Mist == 0) return null;
            for (int i = 0; i < 4; i++)
            {
                var kind = (CultureKind)i;
                int extra = Model.Staged(kind) - TutorialRecipeDose(starter, kind);
                if (extra > 0)
                    return TutText("tut.fix.excess", "This mixture needs a repair. Press - " + kind + " " + extra + (extra == 1 ? " time" : " times") + " to return the excess. Aim for " + TutorialStarterRecipe() + ".", kind, extra, TutorialStarterRecipe());
            }
            for (int i = 0; i < 4; i++)
            {
                var kind = (CultureKind)i;
                int needed = TutorialRecipeDose(starter, kind) - Model.Staged(kind);
                if (needed <= 0) continue;
                int collect = needed - Model.Available(kind);
                return collect > 0
                    ? TutText("tut.fix.collect", "Your mixture needs " + kind + ". Collect " + collect + " " + kind + ", then use + " + kind + " at the incubator.", kind, collect)
                    : TutText("tut.fix.dose", "Your mixture needs " + needed + " more " + kind + ". Press + " + kind + " " + needed + (needed == 1 ? " time" : " times") + " at the incubator.", needed, kind);
            }
            return null;
        }

        static int TutorialRecipeDose(SpeciesDefinition recipe, CultureKind kind)
        {
            switch (kind)
            {
                case CultureKind.Bloom: return recipe.Bloom;
                case CultureKind.Flow: return recipe.Flow;
                case CultureKind.Spark: return recipe.Spark;
                default: return recipe.Mist;
            }
        }

        string TutorialCollectionTarget()
        {
            var starter = Model.GetSpecies("pondlet");
            int bloom = starter == null ? 1 : starter.Bloom;
            int flow = starter == null ? 1 : starter.Flow;
            if (Model.Available(CultureKind.Bloom) + Model.Staged(CultureKind.Bloom) < bloom) return bloom + " Bloom";
            if (Model.Available(CultureKind.Flow) + Model.Staged(CultureKind.Flow) < flow) return flow + " Flow";
            return "Bloom and Flow";
        }
        string TutorialStarterRecipe()
        {
            var starter = Model.GetSpecies("pondlet");
            return starter == null ? "the recipe shown in your journal" : RecipeText(starter);
        }
        string TutorialIncubatorName() => Model.GetSpecies(Model.Incubator.SpeciesId)?.DisplayName ?? "Your creature";
        bool TutorialHasEmptyDisplay()
        {
            foreach (string id in Model.TankIds) if (!Model.GetTank(id).HasValue && !Model.IsTankReservedForStocking(id)) return true;
            return false;
        }

        static string TutorialLessonTitle(TutorialLesson lesson)
        {
            switch (lesson)
            {
                case TutorialLesson.Orient: return "01 / Find your back lab";
                case TutorialLesson.Collect: return "02 / Collect cultures";
                case TutorialLesson.Prepare: return "03 / Prepare a recipe";
                case TutorialLesson.Grow: return "04 / Grow your creature";
                case TutorialLesson.Display: return "05 / Stock the front shop";
                case TutorialLesson.PriceAndOpen: return "06 / Price and open";
                case TutorialLesson.Serve: return "07 / Make your first sale";
                case TutorialLesson.Practice: return "08 / Try it yourself";
                case TutorialLesson.CloseAndReview: return "09 / Close and review";
                case TutorialLesson.NextPlan: return "10 / Choose your next goal";
                default: return "Opening Night";
            }
        }

    }
}
