using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        // Display-only access for runtime validation. This resolver never records learning.
        public string TutorialObjectiveForDisplay => Objective();

        void DrawTutorialWelcome()
        {
            Heading("Welcome to your little shop");
            Text("A cozy aquarium shop up front. A curious little lab through the door.");
            Space();
            if (AutosaveBlocked)
            {
                Heading("Your existing save needs attention");
                Text("The original save is protected. Guidance cannot start until a valid save is restored.");
                Small("Exploring now will not save progress. Pause contains reload and explicit New Game options.");
                Space();
                Button("Open pause / save recovery", () => SetPanel(ShopPanel.Pause));
                Button("Explore without saving", () => SetPanel(ShopPanel.None));
                Button("Read Opening Night help  /  F1", () => OpenTutorialHelp());
                return;
            }
            if (!string.IsNullOrEmpty(SaveError))
            {
                Small("Progress is not saved. Retry your choice, or open Pause for save recovery.");
                Button("Open pause / save recovery", () => SetPanel(ShopPanel.Pause));
                Space();
            }
            switch (Tutorial.Mode)
            {
                case TutorialMode.Offered:
                    Heading("Opening Night");
                    Text("Grow a creature, stock your shop, and make your first sale. Then try the loop yourself.");
                    Small("One step at a time. Keep control of your camera, take your time at night, and reopen help whenever you need it.");
                    Space();
                    Button("Start guided opening", () => StartGuidedOpening());
                    Button("Play without guidance", () => SkipTutorial());
                    break;
                case TutorialMode.Active:
                    Heading("Your guided opening continues");
                    Text(TutorialLessonTitle(ActiveTutorialLesson));
                    Small("Your shop and lesson progress continue from where you left off.");
                    Space();
                    Button("Continue guided opening", () => ContinueGuidedOpening());
                    Button("Read Opening Night help  /  F1", () => OpenTutorialHelp());
                    Button("Skip guidance / keep my shop", () => SkipTutorial());
                    break;
                default:
                    Heading(Tutorial.Mode == TutorialMode.Completed ? "Welcome back, shopkeeper" : "Your shop is ready for you");
                    Text("Continue your shop, or browse the Opening Night help cards at any time.");
                    Small("Help does not reset your shop or start a new guided game.");
                    Space();
                    Button("Continue my shop", () => SetPanel(ShopPanel.None));
                    Button("Read Opening Night help  /  F1", () => OpenTutorialHelp());
                    break;
            }
            Space();
            Small("WASD move / mouse look / E use a station / F1 help / ESC pause");
            Small("Nights are untimed. Help pauses everything. Ordinary station panels keep the shop running.");

        }

        string TutorialObjective()
        {
            if (Tutorial.Mode != TutorialMode.Active) return null;
            var lesson = ActiveTutorialLesson;
            // Practice deliberately remains a goal, without unsolicited recipe/route coaching.
            if (lesson == TutorialLesson.Practice)
                return "Grow and sell another creature yourself. F1 opens help if you need it.";
            string recovery = TutorialGuidanceRecovery;
            if (recovery != null) return recovery;
            recovery = TutorialPreparationRecovery(lesson);
            if (recovery != null) return recovery;
            switch (lesson)
            {
                case TutorialLesson.Orient:
                    return "Find your back lab. WASD to move, mouse to look. Approach the lab door to open it.";
                case TutorialLesson.Collect:
                    return "Collect " + TutorialCollectionTarget() + ". Aim at its grower and hold left mouse.";
                case TutorialLesson.Prepare:
                    return "Press E at the incubator. Add " + TutorialStarterRecipe() + ".";
                case TutorialLesson.Grow:
                    if (Model.Incubator.Phase == IncubatorPhase.Incubating)
                        return TutorialIncubatorName() + " is growing / " + Mathf.CeilToInt(Model.Incubator.SecondsRemaining) + "s. It will wait in the incubator.";
                    if (Model.Incubator.Phase == IncubatorPhase.Ready)
                        return "The incubator is occupied. Stock its ready creature, then start your own batch. F1 for help.";
                    return "At the incubator, prepare a recipe and choose Begin incubation.";
                case TutorialLesson.Display:
                    if (!TutorialHasEmptyDisplay())
                        return Cycle.Phase == ShopDayPhase.Night ? "Your displays are full. Open at the front counter to make room through sales." : "Your displays are full. Serve a sale to free a tank; the ready creature can wait.";
                    return Model.Incubator.Phase == IncubatorPhase.Ready && StoredSpecimenCount == 0 && ReadyConditionedCount == 0
                        ? "Press E at an empty front display to transfer your ready creature directly from the incubator."
                        : "Press E at an empty front display. If stock choices appear, select your ready or stored creature.";
                case TutorialLesson.PriceAndOpen:
                    return (Tutorial.Facts & TutorialFacts.PriceViewed) != 0
                        ? "Press E at the front counter, then choose Open for day."
                        : "Press E at a stocked display to check its price. Then open at the front counter.";
                case TutorialLesson.Serve:
                    if (Cycle.Phase == ShopDayPhase.Night)
                        return "Open at the front counter when your display is stocked. Then serve its customer yourself.";
                    if (DisplayedStockCount() == 0)
                        return "Your displays are empty. Stock your creature, then return to the counter.";
                    return "Let a customer choose a creature. Serve their order at the front counter yourself.";
                case TutorialLesson.CloseAndReview:
                    return Cycle.Phase == ShopDayPhase.Night ? "Review your trading results at the front counter, then continue preparation." : "Close for the night at the front counter when ready. Review the day's results.";
                case TutorialLesson.NextPlan:
                    return "Ready for your next goal. Press F1 to finish the opening and continue your shop.";
                default: return null;
            }
        }

        string TutorialPreparationRecovery(TutorialLesson lesson)
        {
            if (lesson != TutorialLesson.Collect && lesson != TutorialLesson.Prepare && lesson != TutorialLesson.Grow) return null;
            if (Model.IsIncubatorReservedForTechnician)
                return "Moss reserved the incubator. Pause lab work at Counter > Staff to grow this creature yourself.";
            var starter = Model.GetSpecies("pondlet");
            var inc = Model.Incubator;
            if (starter == null || inc.Phase != IncubatorPhase.Dosing) return null;
            // Preserve any legitimate alternative recipe instead of 'repairing' it into a Pondlet.
            foreach (var recipe in Config.Species)
                if (recipe.Bloom == inc.Bloom && recipe.Flow == inc.Flow && recipe.Spark == inc.Spark && recipe.Mist == inc.Mist &&
                    (recipe.Spark == 0 || Model.SparkResearchUnlocked) && (recipe.Mist == 0 || Model.MistResearchUnlocked))
                    return lesson == TutorialLesson.Collect ? null : recipe.DisplayName + " recipe ready. Choose Begin incubation at the incubator.";

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
                        return "Siphon full. At the incubator, choose Release 1 " + kind + " to make room for " + missing.Value + ", then collect it.";
                    return "Siphon full. At the incubator, press - " + kind + " once, then Release 1 " + kind + " to make room for " + missing.Value + ". Returning alone keeps the slot full.";
                }
            }
            if (inc.Bloom + inc.Flow + inc.Spark + inc.Mist == 0) return null;
            for (int i = 0; i < 4; i++)
            {
                var kind = (CultureKind)i;
                int extra = Model.Staged(kind) - TutorialRecipeDose(starter, kind);
                if (extra > 0)
                    return "This mixture needs a repair. Press - " + kind + " " + extra + (extra == 1 ? " time" : " times") + " to return the excess. Aim for " + TutorialStarterRecipe() + ".";
            }
            for (int i = 0; i < 4; i++)
            {
                var kind = (CultureKind)i;
                int needed = TutorialRecipeDose(starter, kind) - Model.Staged(kind);
                if (needed <= 0) continue;
                int collect = needed - Model.Available(kind);
                return collect > 0
                    ? "Your mixture needs " + kind + ". Collect " + collect + " " + kind + ", then use + " + kind + " at the incubator."
                    : "Your mixture needs " + needed + " more " + kind + ". Press + " + kind + " " + needed + (needed == 1 ? " time" : " times") + " at the incubator.";
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
            foreach (string id in Model.TankIds) if (!Model.GetTank(id).HasValue) return true;
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

        void DrawTutorialHelp()
        {
            Heading("Opening Night help");
            Small("PAUSED / customers, growers, machines and staff are waiting for you.");
            if (Tutorial.Mode != TutorialMode.Active)
                Small("Reference cards only. Reading does not change your shop or complete a lesson.");
            else if (ActiveTutorialLesson == TutorialLesson.Practice)
                Small("Practice help requested. You can still finish; this attempt is recorded as assisted.");
            else Small("Reading a card does not complete its action. Return to your shop to try it.");
            Space();
            int page = Mathf.Clamp((int)TutorialHelpLesson, (int)TutorialLesson.Orient, (int)TutorialLesson.NextPlan);
            var lesson = (TutorialLesson)page;
            GUILayout.BeginHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && page > (int)TutorialLesson.Orient;
            Button("Previous", () => SelectTutorialHelpLesson((TutorialLesson)(page - 1)));
            GUI.enabled = enabled && page < (int)TutorialLesson.NextPlan;
            Button("Next", () => SelectTutorialHelpLesson((TutorialLesson)(page + 1)));
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            if (Tutorial.Mode == TutorialMode.Active && lesson != ActiveTutorialLesson)
                Button("Return to my current lesson", () => SelectTutorialHelpLesson(ActiveTutorialLesson));
            Space();
            Heading(TutorialLessonTitle(lesson));
            if (TutorialRoomGuidanceActive && lesson == ActiveTutorialLesson)
            {
                string recovery = TutorialGuidanceRecovery;
                if (string.IsNullOrEmpty(recovery)) Button("Show me where to go", () => ShowTutorialTarget());
                else Small(recovery);
            }
            DrawTutorialLessonCard(lesson);
            if (Tutorial.Mode == TutorialMode.Active && ActiveTutorialLesson == TutorialLesson.NextPlan)
                Button("Continue running my shop", () => FinishGuidedOpening());
            Space();
            Heading("Your shop right now");
            Small("Day " + Cycle.DayNumber + " / " + (Cycle.Phase == ShopDayPhase.Night ? "untimed preparation" : Cycle.Phase == ShopDayPhase.Open ? "open for trade" : "closing") + " / " + Model.Currency + " credits");
            Small("Available: " + Model.Available(CultureKind.Bloom) + " Bloom + " + Model.Available(CultureKind.Flow) + " Flow. Staged: " + Model.Staged(CultureKind.Bloom) + " Bloom + " + Model.Staged(CultureKind.Flow) + " Flow.");
            if (Model.SparkResearchUnlocked || Model.MistResearchUnlocked)
                Small("Available / staged: Spark " + Model.Available(CultureKind.Spark) + "/" + Model.Staged(CultureKind.Spark) + "; Mist " + Model.Available(CultureKind.Mist) + "/" + Model.Staged(CultureKind.Mist) + ".");
            Small("Siphon " + Model.ReservedCultureCount + "/" + Model.Capacity + " including staged doses. Displays stocked: " + DisplayedStockCount() + ".");
            if (lesson == TutorialLesson.Collect || lesson == TutorialLesson.Prepare || lesson == TutorialLesson.Grow)
            {
                string recovery = TutorialPreparationRecovery(lesson);
                if (!string.IsNullOrEmpty(recovery)) Small(recovery);
            }
            if (Model.Incubator.Phase != IncubatorPhase.Dosing)
                Small("Incubator: " + TutorialIncubatorName() + (Model.Incubator.Phase == IncubatorPhase.Ready ? " / ready" : " / " + Mathf.CeilToInt(Model.Incubator.SecondsRemaining) + "s remaining"));
            if (Staff.Hired || Stocking.Employee.Hired || Laboratory.Employee.Hired)
            {
                Space();
                Small("These lessons ask you to grow, stock and serve manually. At Counter > Staff, pause Pip's work, Nori's stocking or Moss's lab work if they reach a job first. Hires and wages stay intact.");
            }
            if (Tutorial.Mode == TutorialMode.Active)
            {
                Space();
                Button("Skip guidance / keep my shop", () => SkipTutorial());
                Small("Skipping keeps your creatures, money and progress. These cards remain available with F1.");
            }
        }

        void DrawTutorialLessonCard(TutorialLesson lesson)
        {
            switch (lesson)
            {
                case TutorialLesson.Orient:
                    Text("Find the lab doorway behind the shop. Approach it to open the door; it closes behind you.");
                    Small("WASD moves. Mouse looks. Aim at a station and press E to use it. ESC closes a panel or opens Pause. TAB opens the journal. F1 opens this paused help.");
                    Small("Front: displays and the sales counter. Back: culture growers, incubator and research bench. Night preparation is untimed. The lab works during the day too.");
                    break;
                case TutorialLesson.Collect:
                    Text("Collect " + TutorialStarterRecipe() + " from the lab growers. Aim and hold left mouse until a unit enters your siphon. Either order works.");
                    Small("Available cultures are carried in the siphon. Staged cultures have been added to the incubator; both still use capacity until you start a batch. Growers refill for free while time runs.");
                    Small("Siphon full of the wrong culture? At the incubator, return unwanted staged doses, then release unwanted carried units. Returning a dose alone does not free capacity.");
                    break;
                case TutorialLesson.Prepare:
                    Text("Press E at the incubator. Use + Bloom and + Flow to stage " + TutorialStarterRecipe() + ".");
                    Small("The numbers above the buttons are staged doses. Use - to return extras safely. A supported mixture makes one creature; another valid recipe is fine too. Unsupported mixtures consume nothing.");
                    Small("A discovered recipe also has a Prepare button. It uses your collected cultures; it does not provide them for free. If Moss reserved the machine, pause lab work at the staff counter first.");
                    break;
                case TutorialLesson.Grow:
                    Text("Choose Begin incubation. A normal batch takes " + Config.IncubationSeconds.ToString("0.#") + " seconds of active game time.");
                    Small("Close this help to let it grow. Ordinary station panels and the journal keep production running. Pause, requested help and a closed game stop it. The ready creature waits safely in the incubator.");
                    Small("If an older batch is occupying the machine, move it to a free display or rack first. Then start a new batch yourself. There is no need to buy an upgrade.");
                    break;
                case TutorialLesson.Display:
                    Text("Go through the door to the front shop. Press E at an empty display to transfer the ready incubator creature directly. If several stock sources are available, choose your creature from the options shown.");
                    Small("For a creature waiting on a lab rack, press E at the empty display and choose its rack slot. Creatures are not carried in your siphon. Any reachable empty display works, including one you moved.");
                    Small("All displays full? Sell existing stock to free a tank, or use a free rack temporarily. Reserved stock belongs to its customer's order until checkout or cancellation.");
                    break;
                case TutorialLesson.PriceAndOpen:
                    Text("Press E at a stocked display to inspect its price and customer reactions. Keeping the standard price is fine; changing it is optional.");
                    var starter = Model.GetSpecies("pondlet");
                    var tag = starter == null ? (PriceView?)null : Model.GetPrice(starter.Id);
                    if (tag.HasValue) Small(starter.DisplayName + " standard price: " + tag.Value.Standard + " credits. Your current tag: " + tag.Value.Price + ".");
                    Small("Customers have different price limits and budgets. Too expensive means they will not buy. An agreed reservation keeps its price even if you change the tag later.");
                    Small("At the front counter, choose Open for day. The trading clock starts then. If you opened with no stock, refill a display normally or close early to prepare.");
                    break;
                case TutorialLesson.Serve:
                    Text("Let a customer choose a stocked creature and walk to the counter. Press E at the counter, then Serve customer before patience runs out.");
                    Small("A reservation is not a payment. Credits arrive only when checkout succeeds. Serve this lesson's creature yourself; Pip's sales still earn money but do not count as manual practice.");
                    Small("No buyer? Check that your shop is open and a reachable display has unreserved stock. Review its price. If a customer leaves, the unpaid creature stays available for another visit.");
                    break;
                case TutorialLesson.Practice:
                    Text("After your first manual sale, start a new batch yourself, stock its creature and serve its buyer. Any valid recipe works.");
                    Small("Try the grow > display > sell loop from memory. A creature started before your first sale can sell normally, but it does not replace this new practice batch. You may use more than one trading day.");
                    Small("Need a reminder? Browse cards 02-07. Help is always allowed; assisted practice can still complete the opening.");
                    break;
                case TutorialLesson.CloseAndReview:
                    Text("At the counter, choose Close for the night when ready, then confirm. Finish accepted checkout promptly; unpaid creatures stay safe when trading ends.");
                    Small("Read the day results and choose Continue preparation. You can also review a completed report at the counter. The report must include one of your guided sales.");
                    Small("There is no need to wait for the full trading day. If practice spans days, finish your current trading day normally before the final night handoff.");
                    break;
                case TutorialLesson.NextPlan:
                    Text("Keep growing, discover a recipe, or save for your larger siphon. Your shop is yours to develop.");
                    Small("Siphon MK II costs " + Config.UpgradeCost + " credits at the back lab bench. You have " + Model.Currency + "." + (Model.UpgradeOwned ? " Your upgrade is already installed." : " Buying it is optional; keep selling if you want to save up."));
                    Small("After returning, TAB opens recipe notes. Press E at the lab bench for upgrade details. No purchase is required to finish your guided opening.");
                    if (Tutorial.Mode == TutorialMode.Active && ActiveTutorialLesson != TutorialLesson.NextPlan)
                        Small("Complete both manual sales and the night/report handoff before finishing guidance. Browsing ahead does not skip those actions.");
                    break;
            }
        }
    }
}
