#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // UX-06: the planning notebook through its real flows. From the counter: hire, schedule, pause and dismiss crew,
        // set Nori's displays and Moss's queue, and see Esc back out one level. Arrange: buy a wing, place, store and sell
        // furniture, and move equipment, with the key bar while placing. From the bench: the research path and the lab
        // helper. Then the same crew by day. Controls are pressed with the synthetic Input System
        // mouse and keyboard (CardClick, ToolkitCardsSmoke.cs); stations open with a real E.
        IEnumerator ToolkitPlanningScenario()
        {
            limitations.Add("UX-06 planning route: a labelled 600-credit fixture; starter discoveries are accelerated model fixtures (produce, display and sell), as in the R05 lab routes. Furniture is placed with real placement aim and clicks, stations open with a real E, and notebook controls are pressed with synthetic Input System devices, not physical hardware. Captures are screen-only.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready && ui.Planning != null, "UI Toolkit root and planning notebook load" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready || ui.Planning == null) yield break;
            GateFresh();
            CardInput();
            StaffCreditFixture(600, "UX-06 planning");
            // Labelled fixture: Pondlet and Bubblebelly discovered (Ribbonfin stays undiscovered, for the research path). The
            // model sales are saved before the notebook opens, so the first money floater is the first real spend, not the
            // fixture's earnings folded into it.
            foreach (string species in new[] { "pondlet", "bubblebelly" })
            { SpecimenProduce(species); Ok(shop.Model.TransferToTank("tank-a"), "Discovery fixture display"); Ok(shop.Model.SellFromTank("tank-a"), "Discovery fixture sale"); }
            shop.World.Refresh(shop.Model);
            Check(shop.SaveNow(), "Discovery fixture saved");
            Check(shop.ToolkitUiActive && ui.Active, "The UI Toolkit UI is on from start-up; since UX-09 it is the only UI");
            var plan = ui.Planning;
            var card = ui.StationCard;
            var root = ui.Root;
            var s = ui.Strings;
            yield return HudFrames(3);
            var counter = StationOf(StationKind.Counter);
            int day = shop.Cycle.DayNumber;

            // ---------- crew, from the counter ----------
            yield return CardOpenCounter(counter);
            Check(card.IsShown && shop.Panel == ShopPanel.Counter && shop.IsAtCounter, "A real E opens the counter card");
            yield return CardClick(root.Q<Button>(CounterCardView.CrewName), "Crew");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Staff && plan.IsShown && plan.CurrentView == plan.Crew && !card.IsShown,
                "The counter card's Crew opens the notebook on its crew tab (panel " + shop.Panel + ", shown " + plan.IsShown + ", view " + (plan.CurrentView != null ? plan.CurrentView.Root.name : "none") + ", card " + card.IsShown + ", notice " + shop.PlanningNotice(out _) + ", screen " + Screen.width + "x" + Screen.height + ", panel size " + root.layout.size + ")");
            Check(root.Q<Label>(PlanningViewPresenter.TitleName).text == s.Get("plan.crew.title"), "The page is titled " + root.Q<Label>(PlanningViewPresenter.TitleName).text);
            Check(PlanTabActive(root, PlanningTab.Crew) && PlanTabOpen(root, PlanningTab.Arrange) && !PlanTabOpen(root, PlanningTab.Automation) && !PlanTabOpen(root, PlanningTab.Research) &&
                CardShown(root, PlanningViewPresenter.TabWhereName(PlanningTab.Automation)) && !CardShown(root, PlanningViewPresenter.TabWhereName(PlanningTab.Arrange)),
                "At the counter at night: Crew and Arrange open; Automation and Research say they're at the lab bench");
            var dim = root.Q(PlanningViewPresenter.DimName);
            Check(CardShown(dim) && dim.resolvedStyle.backgroundColor.a > .4f && dim.worldBound.width >= root.worldBound.width - 1 && dim.worldBound.height >= root.worldBound.height - 1,
                "The world behind the notebook is dimmed (" + dim.resolvedStyle.backgroundColor + ", " + dim.worldBound + " over " + root.worldBound + ", display " + dim.resolvedStyle.display + ", opacity " + dim.resolvedStyle.opacity + ")");
            Check(ui.Hud.IsShown && CardShown(root, HudPresenter.MoneyName) && CardShown(root, HudPresenter.DayName) && !CardShown(root, HudPresenter.QueueName),
                "The day and money chips stay over the dimmed world");
            var crew = plan.Crew;
            foreach (CrewRole role in new[] { CrewRole.Cashier, CrewRole.Stocker, CrewRole.Technician })
                Check(root.Q<Label>(CrewView.StatusName(role)).text == s.Get("plan.crew.status.notHired") && CardShown(root, CrewView.HireName(role)) &&
                    !CardShown(root, CrewView.SwitchName(role)) && !CardShown(root, CrewView.MenuName(role)), role + " starts not hired, with Hire and no switch or menu");
            yield return HudShot("ux06-crew-hire");
            CheckPlan(plan, "Crew, nobody hired", 0, false);
            int credits = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(CrewView.HireName(CrewRole.Cashier)), "Hire Pip");
            yield return HudFrames(2);
            Check(shop.Staff.Hired && !shop.Staff.Scheduled && shop.Model.Currency == credits && CardShown(root, CrewView.SwitchName(CrewRole.Cashier)) &&
                root.Q<Label>(CrewView.StatusName(CrewRole.Cashier)).text == s.Get("plan.crew.status.off"), "A real click hires Pip at no cost, off duty, with a switch");
            Check(plan.Footer.StartsWith("Pip hired", System.StringComparison.Ordinal) && ui.Hud.ToastsShown == 0,
                "The notice shows in the notebook's footer, not as a toast: " + plan.Footer);
            yield return CardClick(root.Q<Button>(CrewView.SwitchName(CrewRole.Cashier)), "Pip's On duty switch");
            yield return HudFrames(2);
            Check(shop.Staff.Scheduled && shop.Staff.PaidDay == day && shop.Model.Currency == credits - ShopStaff.CashierWage &&
                root.Q<PlanSwitch>(CrewView.SwitchName(CrewRole.Cashier)).On && root.Q<Label>(CrewView.StatusName(CrewRole.Cashier)).text == s.Get("plan.crew.status.ready"),
                "Switching Pip on pays tonight's wage; Pip is ready when you open");
            yield return PlanFloater(root, ShopHudRules.Signed(-ShopStaff.CashierWage));
            Check(HudFloaterWith(root, ShopHudRules.Signed(-ShopStaff.CashierWage)), "The money floater shows the wage alone, " + ShopHudRules.Signed(-ShopStaff.CashierWage) + PlanMoneyCue(root));
            yield return CardClick(root.Q<Button>(CrewView.MenuName(CrewRole.Cashier)), "Pip's menu");
            Check(CardShown(root, CrewView.PauseName(CrewRole.Cashier)) && CardShown(root, CrewView.DismissName(CrewRole.Cashier)) &&
                root.Q<Button>(CrewView.PauseName(CrewRole.Cashier)).text == s.Get("plan.crew.pause"), "The ••• menu holds Pause and Dismiss…");
            yield return HudShot("ux06-crew-menu");
            CheckPlan(plan, "Crew, Pip's menu", 0, false);
            yield return CardClick(root.Q<Button>(CrewView.PauseName(CrewRole.Cashier)), "Pause Pip");
            yield return HudFrames(2);
            Check(shop.Staff.Paused && !CardShown(root, CrewView.PauseName(CrewRole.Cashier)) && root.Q<Label>(CrewView.StatusName(CrewRole.Cashier)).text == s.Get("plan.crew.status.paused"),
                "Pause pauses Pip and closes the menu");
            yield return CardClick(root.Q<Button>(CrewView.MenuName(CrewRole.Cashier)), "Pip's menu");
            Check(root.Q<Button>(CrewView.PauseName(CrewRole.Cashier)).text == s.Get("plan.crew.resume"), "Paused, the menu offers Resume");
            yield return CardClick(root.Q<Button>(CrewView.PauseName(CrewRole.Cashier)), "Resume Pip");
            Check(!shop.Staff.Paused, "Resume resumes Pip");
            yield return CardClick(root.Q<Button>(CrewView.MenuName(CrewRole.Cashier)), "Pip's menu");
            yield return CardClick(root.Q<Button>(CrewView.DismissName(CrewRole.Cashier)), "Dismiss Pip…");
            Check(CardShown(root, CrewView.CardName(CrewRole.Cashier) + "-confirm") && shop.Staff.Hired &&
                root.Q<Label>(CrewView.CardName(CrewRole.Cashier) + "-confirm-text").text == s.Get("plan.crew.pip.dismissAsk"), "Dismiss asks first: " + s.Get("plan.crew.pip.dismissAsk"));
            yield return HudShot("ux06-crew-dismiss");
            CheckPlan(plan, "Crew, dismiss asks", 0, false);
            yield return CardClick(root.Q<Button>(CrewView.KeepName(CrewRole.Cashier)), "Keep Pip");
            Check(shop.Staff.Hired && !CardShown(root, CrewView.CardName(CrewRole.Cashier) + "-confirm"), "Keep Pip backs out");

            // Nori: hire, switch on, then the display targets page.
            credits = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(CrewView.HireName(CrewRole.Stocker)), "Hire Nori");
            yield return HudFrames(2);
            yield return CardClick(root.Q<Button>(CrewView.SwitchName(CrewRole.Stocker)), "Nori's On duty switch");
            yield return HudFrames(2);
            Check(shop.Stocking.Employee.Hired && shop.Stocking.Employee.Scheduled && shop.Stocking.Employee.PaidDay == day && shop.Model.Currency == credits - ShopStaff.StockerWage &&
                root.Q<Label>(CrewView.StatusName(CrewRole.Stocker)).text == s.Get("plan.crew.status.noTargets"),
                "Nori hired and on duty from the crew card (the notebook's crew tab holds Nori's controls); idle without targets");
            yield return CardClick(root.Q<Button>(CrewView.PageName(CrewRole.Stocker)), "Display targets");
            yield return HudFrames(3);
            var targets = plan.Targets;
            Check(shop.Panel == ShopPanel.StockerTargets && plan.CurrentView == targets && PlanTabActive(root, PlanningTab.Crew) &&
                targets.State.Rows.Count == shop.Model.TankIds.Length && targets.State.Options.Count == 4,
                "Display targets opens Nori's page: " + targets.State.Rows.Count + " displays, Off, Any, Pondlet and Bubblebelly");
            yield return CardClick(root.Q<Button>(StockerTargetsView.PickName("tank-b", "pondlet")), "Display B: Pondlet");
            yield return HudFrames(2);
            Check(shop.Stocking.GetTarget("tank-b") == "pondlet" && root.Q<Button>(StockerTargetsView.PickName("tank-b", "pondlet")).ClassListContains("es-plan-pick--on"),
                "A real click sets Display B to keep a Pondlet");
            yield return CardClick(root.Q<Button>(StockerTargetsView.PickName("tank-a", ShopStocking.AnySpecies)), "Display A: Any");
            Check(shop.Stocking.GetTarget("tank-a") == ShopStocking.AnySpecies, "Display A keeps any rack species");
            yield return HudShot("ux06-targets");
            CheckPlan(plan, "Nori's displays", 0, true);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Staff && plan.CurrentView == crew, "Esc backs out one level, to the crew");

            // Moss: hire, then the recipe queue.
            yield return CardClick(root.Q<Button>(CrewView.HireName(CrewRole.Technician)), "Hire Moss");
            yield return HudFrames(2);
            Check(shop.Laboratory.Employee.Hired, "A real click hires Moss");
            yield return CardClick(root.Q<Button>(CrewView.PageName(CrewRole.Technician)), "Recipe queue");
            yield return HudFrames(3);
            var queue = plan.Queue;
            Check(shop.Panel == ShopPanel.TechnicianOrders && plan.CurrentView == queue && CardShown(root, TechnicianQueueView.EmptyName) && queue.State.Known.Count == 2,
                "Recipe queue opens Moss's page: nothing queued, two known recipes to add");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.AddName("pondlet")), "+ Pondlet");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.AddName("pondlet")), "+ Pondlet");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.AddName("bubblebelly")), "+ Bubblebelly");
            yield return HudFrames(2);
            var orders = shop.Laboratory.Queue;
            Check(orders.Length == 2 && orders[0].speciesId == "pondlet" && orders[0].count == 2 && orders[1].speciesId == "bubblebelly" &&
                root.Q<Label>(TechnicianQueueView.RowName("pondlet") + "-count").text == s.Format("plan.queue.starts", 2), "Real clicks queue two Pondlet starts and one Bubblebelly");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.LaterName("pondlet")), "Pondlet later");
            yield return HudFrames(2);
            Check(shop.Laboratory.Queue[0].speciesId == "bubblebelly" && !root.Q<Button>(TechnicianQueueView.EarlierName("bubblebelly")).enabledSelf,
                "Later moves Pondlet down; the top row can't go earlier");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.EarlierName("pondlet")), "Pondlet earlier");
            Check(shop.Laboratory.Queue[0].speciesId == "pondlet", "Earlier moves it back up");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.RemoveName("bubblebelly")), "Remove Bubblebelly");
            yield return HudFrames(2);
            Check(shop.Laboratory.Queue.Length == 1, "Remove takes Bubblebelly off the queue");
            yield return CardClick(root.Q<Button>(TechnicianQueueView.ClearName), "Clear queue…");
            Check(CardShown(root, "es-plan-queue-confirm") && shop.Laboratory.Queue.Length == 1, "Clear asks first");
            yield return HudShot("ux06-queue");
            CheckPlan(plan, "Moss's queue", 0, true);
            yield return CardClick(root.Q<Button>(TechnicianQueueView.KeepName), "Keep queue");
            Check(shop.Laboratory.Queue.Length == 1 && !CardShown(root, "es-plan-queue-confirm"), "Keep queue backs out");
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Staff && plan.CurrentView == crew, "Esc from Moss's page goes back to the crew");
            yield return HudFrames(2);
            Check(plan.Footer == crew.Note, "The queue's notice stays on its page; the crew shows its own line: " + plan.Footer);
            Check(root.Q<Label>(CrewView.StatusName(CrewRole.Stocker)).text == s.Get("plan.crew.status.onDuty") &&
                root.Q<Label>(CrewView.StatusName(CrewRole.Technician)).text == s.Get("plan.crew.status.off"), "With targets set Nori is on duty; Moss is hired, off duty");
            yield return HudShot("ux06-crew");
            CheckPlan(plan, "Crew, everyone hired", 0, false);

            // Tabs: Arrange from the crew, then Esc back to the counter card, then back to play.
            yield return CardClick(root.Q<Button>(PlanningViewPresenter.TabName(PlanningTab.Arrange)), "Arrange tab");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Arrange && plan.CurrentView == plan.Arrange && plan.Arrange.Tab == ArrangeTab.Furniture && PlanTabActive(root, PlanningTab.Arrange),
                "The Arrange tab switches the notebook to arrange, on Furniture");
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Counter && card.IsShown && !plan.IsShown, "Esc from a tab goes back to the counter card it was opened from");
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.None && !card.IsShown, "Esc again closes the card");

            // ---------- arrange ----------
            yield return CardOpenCounter(counter);
            yield return CardClick(root.Q<Button>(CounterCardView.ArrangeName), "Arrange");
            yield return HudFrames(3);
            var arrange = plan.Arrange;
            Check(shop.Panel == ShopPanel.Arrange && plan.CurrentView == arrange, "The counter card's Arrange opens the notebook on arrange");
            var large = root.Q<Button>(ArrangeView.TileName(ShopLayout.LargeDisplayId));
            Check(arrange.State.Furniture.Count == 4 && !large.enabledSelf && root.Q<Label>(ArrangeView.TileReasonName(ShopLayout.LargeDisplayId)).text == s.Get("plan.arrange.why.wing"),
                "Furniture: small and large display, work table and the west wing; the large display needs the wing");
            yield return HudShot("ux06-arrange-furniture");
            CheckPlan(plan, "Arrange, furniture", 0, false);
            credits = shop.Model.Currency;
            var wing = root.Q<Button>(ArrangeView.TileName("expand-store"));
            var picked = root.panel.Pick(wing.worldBound.center);
            Check(picked != null && (picked == wing || wing.Contains(picked)), "The west wing tile is what the pointer hits (" + (picked == null ? "nothing" : picked.name + " " + string.Join(".", picked.GetClasses())) + " at " + wing.worldBound.center + ")");
            yield return CardClick(root.Q<Button>(ArrangeView.TileName("expand-store")), "West wing");
            yield return HudFrames(3);
            Check(shop.Layout.StoreTier == 1 && shop.Model.Currency == credits - 60 && root.Q<Button>(ArrangeView.TileName(ShopLayout.LargeDisplayId)).enabledSelf,
                "A real click buys the west wing; the large display opens up (tier " + shop.Layout.StoreTier + ", notice " + shop.PlanningNotice(out _) + ")");
            yield return CardClick(root.Q<Button>(ArrangeView.TileName(ShopLayout.SmallDisplayId)), "Small display");
            yield return HudFrames(3);
            Check(shop.PlacementActive && plan.PlacementBarShown && !CardShown(plan.Book) &&
                root.Q<Label>(PlanningViewPresenter.PlaceTitleName).text == s.Format("plan.place.buy", "Small display", 24),
                "Choosing a tile enters placement: the notebook steps aside for the key bar (" + root.Q<Label>(PlanningViewPresenter.PlaceTitleName).text + ")");
            yield return HudShot("ux06-place-bar");
            Check(ShopUiTestDriver.FullyInside(root.Q(PlanningViewPresenter.PlaceBarName), root) && CardTextProblem(root.Q(PlanningViewPresenter.PlaceBarName)) == null,
                "The key bar is on screen and its text fits");
            yield return CardKey(Key.Escape);
            yield return HudFrames(3);
            Check(!shop.PlacementActive && shop.Panel == ShopPanel.Arrange && CardShown(plan.Book) && !plan.PlacementBarShown && shop.Layout.Items.Count == 0 &&
                arrange.Tab == ArrangeTab.Furniture, "Esc cancels placement and the notebook comes back on Furniture, nothing bought");
            // A real placement: aim at a known free floor cell first, then choose the work table.
            shop.AllowValidationInput = true;
            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenPlacementMenu(), "Arrange reopens where the player now stands (fixture aim)");
            CardInput(); shop.AllowValidationInput = true;
            yield return HudFrames(3);
            credits = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(ArrangeView.TileName(ShopLayout.TableDefinitionId)), "Work table");
            yield return PlacementNeutral();
            Check(shop.PlacementActive && shop.PlacementX == 3 && shop.PlacementZ == -5 && shop.PlacementValid, "The work table previews at the aimed cell / " + shop.PlacementMessage);
            yield return PlacementClick();
            yield return HudFrames(3);
            Check(!shop.PlacementActive && shop.Layout.Items.Count == 1 && shop.Model.Currency == credits - ShopLayout.DefinitionPrice(ShopLayout.TableDefinitionId) && shop.Panel == ShopPanel.Arrange && CardShown(plan.Book),
                "A real click places the table and the notebook returns");
            shop.AllowValidationInput = false;
            string table = shop.Layout.Items[0].Id;
            yield return CardClick(root.Q<Button>(ArrangeView.SegName(ArrangeTab.Owned)), "Owned");
            yield return HudFrames(3);
            Check(arrange.Tab == ArrangeTab.Owned && CardShown(root, ArrangeView.OwnedName(table)) &&
                root.Q<Label>(ArrangeView.OwnedName(table) + "-place").text == s.Get("plan.arrange.place.store"), "Owned lists the work table, in the store");
            yield return CardClick(root.Q<Button>(ArrangeView.StoreName(table)), "Store the table");
            yield return HudFrames(3);
            Check(shop.Layout.GetItem(table).Value.Stored && root.Q<Label>(ArrangeView.OwnedName(table) + "-place").text == s.Get("plan.arrange.place.storage") &&
                root.Q<Button>(ArrangeView.MoveName(table)).text == s.Get("plan.arrange.placeOwned"), "Store puts it in storage; it can be placed again");
            credits = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(ArrangeView.SellName(table)), "Sell the table…");
            yield return HudFrames(2);
            Check(shop.PendingFurnitureSaleId == table && CardShown(root, ArrangeView.OwnedName(table) + "-confirm"), "Sell asks first");
            yield return HudShot("ux06-arrange-owned");
            CheckPlan(plan, "Arrange, owned, selling", 0, true);
            yield return CardClick(root.Q<Button>(ArrangeView.SellNowName(table)), "Sell");
            yield return HudFrames(3);
            Check(shop.Layout.Items.Count == 0 && shop.Model.Currency == credits + ShopLayout.DefinitionResalePrice(ShopLayout.TableDefinitionId),
                "Sell sells it for half its price");
            yield return CardClick(root.Q<Button>(ArrangeView.SegName(ArrangeTab.Decor)), "Decor");
            yield return HudFrames(3);
            Check(arrange.Tab == ArrangeTab.Decor && arrange.State.Decor.Count == 3 && arrange.State.Decor.TrueForAll(t => t.Appeal == 10), "Decor: three types, +10 appeal each");
            yield return HudShot("ux06-arrange-decor");
            CheckPlan(plan, "Arrange, decor", 0, false);
            yield return CardClick(root.Q<Button>(ArrangeView.SegName(ArrangeTab.Lab)), "Lab");
            yield return HudFrames(3);
            Check(arrange.Tab == ArrangeTab.Lab && CardShown(root, ArrangeView.TileName("expand-lab")) && CardShown(root, ArrangeView.TileName(ShopLayout.RackDefinitionId)) &&
                !root.Q<Button>(ArrangeView.TileName(ShopLayout.ConditionerDefinitionId)).enabledSelf &&
                root.Q<Label>(ArrangeView.TileReasonName(ShopLayout.ConditionerDefinitionId)).text == s.Get("plan.arrange.why.vat") &&
                CardShown(root, ArrangeView.TileName(ShopLayout.IncubatorFacilityId)), "Lab: the wing, a rack, the vat waiting for Spark and a Lanternfin, and the equipment to move");
            credits = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(ArrangeView.TileName("expand-lab")), "Lab wing");
            yield return HudFrames(3);
            Check(shop.Layout.LabTier == 1 && shop.Model.Currency == credits - 60, "A real click buys the lab wing");
            yield return HudShot("ux06-arrange-lab");
            CheckPlan(plan, "Arrange, lab", 0, false);
            yield return CardClick(root.Q<Button>(ArrangeView.TileName(ShopLayout.IncubatorFacilityId)), "Move the incubator");
            yield return HudFrames(3);
            Check(shop.PlacementActive && root.Q<Label>(PlanningViewPresenter.PlaceTitleName).text == s.Format("plan.place.move", "Incubator"), "Moving equipment uses the key bar too");
            yield return CardKey(Key.Escape);
            yield return HudFrames(3);
            Check(!shop.PlacementActive && shop.Panel == ShopPanel.Arrange && arrange.Tab == ArrangeTab.Lab, "Esc cancels the move and returns to the Lab tab");
            // Arrange was reopened after aiming, not from a card, so one Esc goes straight back to play.
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.None, "Esc backs out to play");

            // ---------- research path and lab helper, from the bench ----------
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            CardInput();
            yield return HudFrames(3);
            Check(card.IsShown && shop.Panel == ShopPanel.Upgrade, "A real E opens the bench card");
            yield return CardClick(root.Q<Button>(BenchCardView.ResearchLinkName), "Research path");
            yield return HudFrames(3);
            var research = plan.Research;
            Check(shop.Panel == ShopPanel.Research && plan.CurrentView == research && PlanTabActive(root, PlanningTab.Research) &&
                PlanTabOpen(root, PlanningTab.Automation) && !PlanTabOpen(root, PlanningTab.Crew), "The bench's Research path opens the notebook; at the bench Crew is the one closed tab");
            var spark = root.Q<Button>(ResearchView.SparkName);
            Check(root.Q<Label>(ResearchView.StepStateName(CultureKind.Bloom)).text == s.Get("plan.research.starter") &&
                root.Q<Label>(ResearchView.StepStateName(CultureKind.Spark)).text == s.Get("plan.research.locked") &&
                CardShown(spark) && !spark.enabledSelf && !CardShown(root, ResearchView.SparkReasonName) &&
                root.Q<Label>(ResearchView.StampName(CultureKind.Spark, 0)).ClassListContains("es-req--done") &&
                !root.Q<Label>(ResearchView.StampName(CultureKind.Spark, 2)).ClassListContains("es-req--done") &&
                root.Q<Label>(ResearchView.StepName(CultureKind.Mist) + "-where").text == s.Format("plan.research.atVat", shop.Config.MistResearchCost),
                "Bloom and Flow come with the shop; Spark waits for Ribbonfin, said once by its stamps; Mist is at the vat");
            credits = shop.Model.Currency;
            yield return CardClick(root.Q<Button>(ResearchView.SiphonName), "Buy Siphon MK II");
            yield return HudFrames(2);
            Check(shop.Model.UpgradeOwned && shop.Model.Currency == credits - shop.Config.UpgradeCost && CardShown(root, ResearchView.SiphonStateName), "A real click buys the siphon, now Installed");
            Check(plan.Footer != "Done.", "The generic Done. stays out of the footer; Installed says it: " + plan.Footer);
            yield return HudShot("ux06-research-locked");
            CheckPlan(plan, "Research, Spark locked", 1, false);
            SpecimenProduce("ribbonfin"); Ok(shop.Model.TransferToTank("tank-a"), "Ribbonfin fixture display"); Ok(shop.Model.SellFromTank("tank-a"), "Ribbonfin fixture sale");
            // Saved, and its floater left to finish, so the Spark floater shows the research cost alone.
            Check(shop.SaveNow(), "Ribbonfin fixture saved");
            yield return new WaitForSecondsRealtime(ShopHudRules.FloaterSeconds + .2f);
            yield return HudFrames(3);
            Check(spark.enabledSelf && !CardShown(root, ResearchView.SparkReasonName) && spark.text == s.Format("plan.research.researchSpark", shop.Config.SparkResearchCost),
                "With Ribbonfin found, " + spark.text + " is ready");
            credits = shop.Model.Currency;
            yield return CardClick(spark, "Research Spark");
            yield return HudFrames(2);
            Check(shop.Model.SparkResearchUnlocked && shop.Model.Currency == credits - shop.Config.SparkResearchCost && !CardShown(spark) &&
                root.Q<Label>(ResearchView.StepStateName(CultureKind.Spark)).text == s.Get("plan.research.done"), "A real click researches Spark from the path; nothing else is researchable here");
            yield return PlanFloater(root, ShopHudRules.Signed(-shop.Config.SparkResearchCost));
            Check(HudFloaterWith(root, ShopHudRules.Signed(-shop.Config.SparkResearchCost)), "The money floater shows the research cost alone, " + ShopHudRules.Signed(-shop.Config.SparkResearchCost) + PlanMoneyCue(root));
            yield return HudShot("ux06-research-done");
            CheckPlan(plan, "Research, Spark done", 0, false);

            yield return CardClick(root.Q<Button>(PlanningViewPresenter.TabName(PlanningTab.Automation)), "Automation tab");
            yield return HudFrames(3);
            var auto = plan.Automation;
            Check(shop.Panel == ShopPanel.Automation && plan.CurrentView == auto && auto.State.Rows.Count == 3 && CardShown(root, AutomationView.PickRecipeName) &&
                !CardShown(root, AutomationView.CollectorName) && root.Q<Label>(AutomationView.CollectorName + "-state").text == s.Get("plan.auto.module.missing") &&
                !root.Q<Button>(AutomationView.PrepareName).enabledSelf && root.Q<Label>(AutomationView.PrepareReasonName).text == s.Get("plan.auto.why.recipe"),
                "The lab helper lists three recipes, no modules yet, and asks for a recipe");
            yield return HudShot("ux06-auto-empty");
            CheckPlan(plan, "Lab helper, nothing chosen", 1, false);
            // Modules: back to the bench card (Esc goes there) and install them with its own buttons.
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Upgrade && card.IsShown, "Esc goes back to the bench card the notebook was opened from");
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Install collector");
            yield return HudFrames(2);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Install doser");
            yield return HudFrames(2);
            Check(shop.Automation.CollectorOwned && shop.Automation.DoserOwned, "The bench card installs both modules");
            yield return CardClick(root.Q<Button>(BenchCardView.CulturePrepName), "Culture prep");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Automation && plan.CurrentView == auto && CardShown(root, AutomationView.CollectorName), "Culture prep now opens the lab helper in the notebook");
            yield return CardClick(root.Q<Button>(AutomationView.ChooseName("pondlet")), "Choose Pondlet");
            yield return HudFrames(2);
            Check(shop.Automation.RecipeId == "pondlet" && CardShown(root, "es-plan-auto-chosen") && root.Q(AutomationView.RowName("pondlet")).ClassListContains("es-plan-row--chosen"),
                "A real click chooses Pondlet; its details open beside the list");
            yield return CardClick(root.Q<Button>(AutomationView.PlusName), "Keep +");
            yield return CardClick(root.Q<Button>(AutomationView.PlusName), "Keep +");
            yield return HudFrames(2);
            Check(shop.Automation.DesiredStockCount == 3 && root.Q<Label>(AutomationView.CountName).text == "3", "Two clicks on + keep 3");
            Check(!root.Q<Button>(AutomationView.MaintainName).enabledSelf && root.Q<Label>(AutomationView.MaintainReasonName).text == s.Get("plan.auto.why.destination"),
                "Keeping stock waits for a display, and says so");
            yield return CardClick(root.Q<Button>(AutomationView.DestinationName("tank-b")), "Fill Display B");
            yield return HudFrames(2);
            Check(shop.Automation.DestinationTankId == "tank-b" && shop.Stocking.GetTarget("tank-b") == "pondlet" && root.Q<Button>(AutomationView.MaintainName).enabledSelf,
                "A real click picks Display B (Nori's target follows); Keep stock can switch on");
            yield return CardClick(root.Q<Button>(AutomationView.CollectorName), "Collector on");
            yield return HudFrames(2);
            Check(shop.Automation.CollectorEnabled && root.Q<Button>(AutomationView.PrepareName).enabledSelf, "The collector's switch turns it on; Prepare one batch is ready");
            yield return HudShot("ux06-auto-chosen");
            CheckPlan(plan, "Lab helper, Pondlet chosen", 1, false);
            yield return CardClick(root.Q<Button>(AutomationView.PrepareName), "Prepare one batch");
            yield return HudFrames(2);
            Check(shop.Automation.IsPending && CardShown(root, AutomationView.CancelName) && !CardShown(root, AutomationView.PrepareName) &&
                !root.Q<Button>(AutomationView.ChooseName("bubblebelly")).enabledSelf, "Prepare one batch requests it; Cancel batch replaces it and the recipe is held");
            yield return CardClick(root.Q<Button>(AutomationView.CancelName), "Cancel batch");
            yield return HudFrames(2);
            Check(!shop.Automation.IsPending && CardShown(root, AutomationView.PrepareName), "Cancel batch cancels it");
            yield return CardClick(root.Q<Button>(AutomationView.MaintainName), "Keep stock on");
            yield return HudFrames(3);
            Check(shop.Automation.MaintainEnabled && root.Q<PlanSwitch>(AutomationView.MaintainName).On && !root.Q<Button>(AutomationView.PrepareName).enabledSelf &&
                !string.IsNullOrEmpty(root.Q<Label>(AutomationView.StatusName).text), "Keep stock switches on; the helper's status is said in words: " + root.Q<Label>(AutomationView.StatusName).text);
            yield return HudShot("ux06-auto-keeping");
            CheckPlan(plan, "Lab helper, keeping stock", 1, false);
            yield return CardClick(root.Q<Button>(AutomationView.MaintainName), "Keep stock off");
            Check(!shop.Automation.MaintainEnabled, "Keep stock switches off");
            yield return CardKey(Key.Escape);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.None, "Esc backs out to play");

            // ---------- the crew by day ----------
            yield return CardOpenCounter(counter);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Open shop");
            yield return HudFrames(3);
            Check(shop.Cycle.Phase == ShopDayPhase.Open, "The counter card opens the shop");
            yield return CardClick(root.Q<Button>(CounterCardView.CrewName), "Crew");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Staff && plan.CurrentView == crew && !root.Q<Button>(CrewView.SwitchName(CrewRole.Cashier)).enabledSelf &&
                !root.Q<Button>(CrewView.SwitchName(CrewRole.Technician)).enabledSelf, "By day the On duty switches wait for night");
            Check(!PlanTabOpen(root, PlanningTab.Arrange) && CardShown(root, PlanningViewPresenter.TabWhereName(PlanningTab.Arrange)) &&
                root.Q<Label>(PlanningViewPresenter.SubtitleName).text == s.Get("plan.crew.subtitle.day"), "By day Arrange says it's for the night; the crew says when to hire");
            yield return HudShot("ux06-crew-day");
            CheckPlan(plan, "Crew by day", 0, false);
            yield return CardClick(root.Q<Button>(CrewView.MenuName(CrewRole.Stocker)), "Nori's menu");
            yield return CardClick(root.Q<Button>(CrewView.PauseName(CrewRole.Stocker)), "Pause Nori");
            yield return HudFrames(2);
            Check(shop.Stocking.Employee.Paused, "Pausing works by day too");

            // ---------- only UI (UX-09) ----------
            Check(NoImmediateModeGui(out string imgui), "No IMGUI path remains: nothing in the game assembly draws with OnGUI" + imgui);
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Staff && plan.IsShown, "The crew stays in the notebook; no other UI draws it");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        // A saved money change floats within a few frames; wait up to half a second for it.
        static IEnumerator PlanFloater(VisualElement root, string text)
        {
            float until = Time.unscaledTime + .5f;
            while (!HudFloaterWith(root, text) && Time.unscaledTime < until) yield return null;
        }

        string PlanMoneyCue(VisualElement root)
        {
            var cue = shop.UiFeedback;
            var shown = new System.Collections.Generic.List<string>();
            foreach (var floater in root.Query(className: "es-floater").ToList()) shown.Add(floater.Q<Label>(className: "es-floater__text")?.text);
            return " (cue " + cue.MoneyDelta + " #" + cue.MoneySequence + (cue.Visible ? "" : " hidden") + ", floaters [" + string.Join(", ", shown) + "])";
        }

        static bool PlanTabOpen(VisualElement root, PlanningTab tab) => root.Q<Button>(PlanningViewPresenter.TabName(tab)).enabledSelf;
        static bool PlanTabActive(VisualElement root, PlanningTab tab) => root.Q<Button>(PlanningViewPresenter.TabName(tab)).ClassListContains("es-plan-tab--active");

        /// <summary>
        /// The notebook is on screen, the page has the expected number of primaries, every text fits, and — on the pages
        /// UX.6 requires to fit at 1280×720 — nothing scrolls or spills. Pages with lists (Nori's displays, Moss's queue,
        /// Owned) may scroll; their visible part is checked.
        /// </summary>
        void CheckPlan(PlanningViewPresenter plan, string state, int primaries, bool scrolls)
        {
            var root = shop.ToolkitUi.Root;
            Check(plan.IsShown && ShopUiTestDriver.FullyInside(plan.Book, root), state + ": the notebook is fully on screen " + plan.Book.worldBound + " in " + root.worldBound);
            int shown = 0;
            foreach (var button in plan.Element.Query<Button>(className: PlanningViewPresenter.PrimaryClass).ToList()) if (CardShown(button)) shown++;
            Check(shown == primaries, state + ": " + shown + " primary button(s) shown, expected " + primaries);
            int scrolling = 0;
            // A list that could scroll but fits doesn't count: only one whose content is taller than its view scrolls.
            foreach (var view in plan.Sheet.Query<ScrollView>().ToList())
                if (CardShown(view) && view.contentContainer.layout.height > view.contentViewport.layout.height + 1f) scrolling++;
            if (!scrolls) Check(scrolling == 0, state + ": nothing scrolls");
            string problem = PlanTextProblem(plan.Book);
            Check(problem == null, state + ": every text fits inside the notebook" + (problem != null ? " / " + problem : ""));
        }

        /// <summary>CardTextProblem for the notebook: inside a scrolling list, only what is in its visible viewport is measured.</summary>
        static string PlanTextProblem(VisualElement book)
        {
            Rect outer = book.worldBound;
            foreach (var element in book.Query<VisualElement>().ToList())
            {
                if (element == book || !CardShown(element)) continue;
                Rect r = element.worldBound;
                if (r.width <= 0 || r.height <= 0) continue;
                var scroll = element.GetFirstAncestorOfType<ScrollView>();
                if (scroll != null)
                {
                    Rect view = scroll.contentViewport.worldBound;
                    if (r.yMin < view.yMin - .5f || r.yMax > view.yMax + .5f) continue;
                }
                if (r.xMin < outer.xMin - .5f || r.yMin < outer.yMin - .5f || r.xMax > outer.xMax + .5f || r.yMax > outer.yMax + .5f)
                    return element.name + " leaves the notebook " + r + " / " + outer;
                // Each piece also stays inside the box that holds it: a squashed tile or panel can keep its content inside the
                // notebook while the content spills past the tile's own border (UX-06 dev runs at 2560x1080).
                var holder = element.parent;
                if (holder != null && holder != book && scroll == null && holder.worldBound.width > 0)
                {
                    Rect h = holder.worldBound;
                    if (r.xMin < h.xMin - 1f || r.yMin < h.yMin - 1f || r.xMax > h.xMax + 1f || r.yMax > h.yMax + 1f)
                        return (string.IsNullOrEmpty(element.name) ? element.GetType().Name + " " + string.Join(".", element.GetClasses()) : element.name) +
                            " leaves " + (string.IsNullOrEmpty(holder.name) ? string.Join(".", holder.GetClasses()) : holder.name) + " " + r + " / " + h;
                }
                if (!(element is TextElement text) || string.IsNullOrEmpty(text.text)) continue;
                var style = text.resolvedStyle;
                float wrapWidth = text.contentRect.width + 1f;
                float innerWidth = text.layout.width - style.borderLeftWidth - style.borderRightWidth;
                float innerHeight = text.layout.height - style.borderTopWidth - style.borderBottomWidth;
                Vector2 needed = text.MeasureTextSize(text.text, wrapWidth, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                if (needed.y > innerHeight + 1.5f) return text.name + " '" + text.text + "' needs " + needed.y.ToString("0") + " px, it has " + innerHeight.ToString("0") + " inside its borders";
                if (style.whiteSpace == WhiteSpace.NoWrap)
                {
                    Vector2 line = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    if (line.x > innerWidth + 1.5f) return text.name + " '" + text.text + "' needs " + line.x.ToString("0") + " px wide, it has " + innerWidth.ToString("0") + " inside its borders";
                }
            }
            return null;
        }
    }
}
#endif
