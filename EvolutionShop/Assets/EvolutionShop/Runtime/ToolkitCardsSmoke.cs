#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        Mouse cardMouse;

        // UX-04: the whole manual loop through the incubator, display tank and counter cards: collect, dose, start,
        // place, price, open, serve, close. Key steps are pressed with a synthetic Input System mouse at the control's
        // on-screen position (real UI Toolkit input), the rest through the panel's submit event. Every state checks
        // that the card is on screen, needs no scroll, fits its text and shows at most one primary button.
        IEnumerator ToolkitCardsScenario()
        {
            limitations.Add("UX-04 cards route: collection is the session's timed scripted harvest; clicks, the wheel, Shift, hover, E and Esc come from synthetic Input System devices added by the route, not physical hardware. Customers are scripted spawns with arrivals off. Captures are screen-only.");
            limitations.Add("The specimen rack, conditioning vat and research bench keep the UX-02 placeholder card until UX-05; with the preview off every station uses the classic panel.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready && ui.StationCard != null, "UI Toolkit root and station card load" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready || ui.StationCard == null) yield break;
            GateFresh();
            CardInput();
            Check(shop.SetToolkitUiActive(true) && shop.ToolkitUiActive, "Preview turns the station cards on");
            var card = ui.StationCard;
            var root = ui.Root;
            var s = ui.Strings;
            yield return HudFrames(3);
            var primary = root.Q<Button>(StationCardPresenter.PrimaryName);
            var reason = root.Q<Label>(StationCardPresenter.ReasonName);
            var status = root.Q<Label>(StationCardPresenter.StatusName);
            var body = root.Q<Label>(StationCardPresenter.BodyName);
            Check(primary != null && reason != null && status != null && body != null, "Card frame has its primary, reason, status and status line");

            // ---------- collect ----------
            yield return Ensure(CultureKind.Bloom, 1);
            yield return Ensure(CultureKind.Flow, 1);
            Check(shop.Model.Available(CultureKind.Bloom) >= 1 && shop.Model.Available(CultureKind.Flow) >= 1, "Collected Bloom and Flow at the growers");

            // ---------- dose ----------
            var incubator = StationOf(StationKind.Incubator);
            yield return CardOpenIncubator(incubator);
            var inc = card.IncubatorView;
            Check(card.IsShown && card.CurrentView == inc && card.Title == incubator.Label && !card.ShopStyled, "Incubator opens its card on the lab strip");
            Check(CardShown(root, IncubatorCardView.ChipName(CultureKind.Bloom)) && CardShown(root, IncubatorCardView.ChipName(CultureKind.Flow)) &&
                !CardShown(root, IncubatorCardView.ChipName(CultureKind.Spark)) && !CardShown(root, IncubatorCardView.ChipName(CultureKind.Mist)),
                "Culture chips for Bloom and Flow; Spark and Mist wait for research");
            Check(CardShown(root, "es-inc-no-recipes"), "Before any discovery the recipe area says discoveries show up there");
            Check(CardShown(primary) && !primary.enabledSelf && primary.text == s.Format("card.inc.start", Mathf.RoundToInt(shop.Config.IncubationSeconds)) &&
                CardShown(reason) && reason.text == s.Get("card.inc.why.empty"), "Primary reads " + primary.text + ", disabled, and says why: " + reason.text);
            yield return HudShot("ux04-incubator-empty");
            CheckCard(card, "Incubator, nothing dosed", 1);

            yield return CardClick(root.Q<Button>(IncubatorCardView.PlusName(CultureKind.Bloom)), "+ Bloom");
            Check(shop.Model.Staged(CultureKind.Bloom) == 1, "A real mouse click on + Bloom doses one Bloom (staged " + shop.Model.Staged(CultureKind.Bloom) + ")");
            var flowChip = root.Q(IncubatorCardView.ChipName(CultureKind.Flow));
            yield return CardWheel(flowChip, true, "Flow chip");
            Check(shop.Model.Staged(CultureKind.Flow) == 1, "Wheel up over the Flow chip doses one Flow (staged " + shop.Model.Staged(CultureKind.Flow) + ")");
            yield return CardWheel(flowChip, false, "Flow chip");
            Check(shop.Model.Staged(CultureKind.Flow) == 0 && shop.Model.Available(CultureKind.Flow) >= 1, "Wheel down returns the Flow dose to the siphon");
            Check(ShopUiTestDriver.Submit(root.Q<Button>(IncubatorCardView.PlusName(CultureKind.Flow))), "+ Flow accepts a submit");
            yield return HudFrames(3);
            Check(shop.Model.Staged(CultureKind.Flow) == 1 && primary.enabledSelf && !CardShown(reason) && status.text == s.Format("card.inc.status.dosed", 2),
                "Two doses staged: Start is ready and the status reads " + status.text);
            Check(CardShown(root, IncubatorCardView.ReturnName) && root.Q<Button>(IncubatorCardView.ReturnName).enabledSelf, "Return doses is offered as a secondary link");
            yield return HudShot("ux04-incubator-dosed");
            CheckCard(card, "Incubator, dosed", 1);

            // ---------- start ----------
            yield return CardClick(primary, "Start");
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Panel == ShopPanel.None && !card.IsShown,
                "A real click on Start begins incubation and closes the card, as the classic panel did");
            shop.Advance(shop.Config.IncubationSeconds * .5f);
            yield return CardOpenIncubator(incubator);
            var ring = root.Q<ProgressRing>(IncubatorCardView.RingName);
            Check(CardShown(root, IncubatorCardView.GrowingName) && ring != null && ring.Progress > .3f && ring.Progress < .8f && ring.Creature.Fill01 > .3f,
                "While growing the card shows the ring filling (" + (ring != null ? ring.Progress.ToString("0.00") : "missing") + ") and the creature filling in");
            Check(root.Q<Label>(IncubatorCardView.SecondsName).text == s.Format("card.inc.seconds", Mathf.CeilToInt(shop.Model.Incubator.SecondsRemaining)) &&
                status.text == s.Get("card.inc.status.growing"), "Seconds left: " + root.Q<Label>(IncubatorCardView.SecondsName).text);
            yield return HudShot("ux04-incubator-growing");
            CheckCard(card, "Incubator, growing", 0);
            yield return CardKey(Key.E);
            Check(shop.Panel == ShopPanel.None && !card.IsShown, "E closes the card (synthetic keyboard)");

            // ---------- place ----------
            yield return CardOpenTank("tank-a", StationOf(StationKind.TankA));
            var tank = card.TankView;
            Check(card.IsShown && card.CurrentView == tank && tank.State.Empty && tank.State.Sources.Count == 0 && body.text == s.Get("card.tank.line.none") &&
                card.Title == shop.World.SlotName("tank-a") && card.ShopStyled, "An empty display card (" + card.Title + "): nothing ready yet");
            yield return HudShot("ux04-tank-empty");
            CheckCard(card, "Empty display", 0);
            shop.Advance(shop.Config.IncubationSeconds);
            yield return HudFrames(4);
            var stockTile = root.Q<Button>(TankCardView.StockTileName(0));
            string pondlet = shop.Model.GetSpecies("pondlet").DisplayName;
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && CardShown(stockTile) && tank.State.Sources.Count == 1 &&
                tank.State.Sources[0].Source == StockSource.Incubator && tank.State.Sources[0].Name == pondlet, "The hatch adds the incubator's " + pondlet + " as a stock tile");
            yield return HudShot("ux04-tank-stock");
            CheckCard(card, "Display with a stock tile", 0);
            yield return CardClick(stockTile, "stock tile");
            var placed = shop.Model.GetTank("tank-a");
            Check(placed.HasValue && placed.Value.SpeciesId == "pondlet" && shop.Model.Incubator.Phase == IncubatorPhase.Dosing && card.IsShown && !tank.State.Empty,
                "A real click on the tile places the " + pondlet + "; the card turns into its price tag");

            // ---------- price ----------
            yield return HudFrames(2);
            var tag = shop.Model.GetPrice("pondlet").Value;
            int standard = tag.Standard;
            var priceLabel = root.Q<Label>(TankCardView.PriceName);
            Check(priceLabel.text == standard.ToString() && CardShown(root, TankCardView.StandardChipName) && !CardShown(root, TankCardView.StandardName) &&
                status.text == s.Get("card.tank.status.sale"), "The tag shows the standard price " + priceLabel.text);
            Check(root.Q<Image>(className: "es-tag__coin")?.image != null, "The tag's price carries its coin icon");
            int types = 0;
            foreach (var type in shop.Customers.Archetypes) if (type.Weight > 0) types++;
            Check(tank.State.Faces.Count == types && CardFaces(root) == types, "One face per arriving customer type (" + CardFaces(root) + ")");
            Check(StationCardRules.Buyers(tank.State.Faces) == shop.Customers.BuyerTypes(tag) &&
                root.Q<Label>(TankCardView.BuyersName).text == s.Format("card.tank.buyers", shop.Customers.BuyerTypes(tag), shop.Customers.ArrivingTypes),
                "The faces agree with the shop: " + root.Q<Label>(TankCardView.BuyersName).text);
            yield return HudShot("ux04-tank-price");
            CheckCard(card, "Price tag", 0);
            var raise = root.Q<Button>(TankCardView.RaiseName);
            yield return CardClick(raise, "+");
            Check(shop.Model.GetPrice("pondlet").Value.Price == standard + 1 && priceLabel.text == (standard + 1).ToString(), "A real click on + raises the price by one (" + priceLabel.text + ")");
            yield return CardShiftClick(raise, "Shift +");
            int expected = Mathf.Min(standard + 1 + StationCardRules.ShiftStep, tag.Maximum);
            Check(shop.Model.GetPrice("pondlet").Value.Price == expected, "Shift-click on + raises by five (" + shop.Model.GetPrice("pondlet").Value.Price + ")");
            yield return CardWheel(root.Q("es-tank-tag").Q(className: "es-tag__price-row"), false, "price row");
            Check(shop.Model.GetPrice("pondlet").Value.Price == expected - 1, "Wheel down over the price lowers it by one (" + shop.Model.GetPrice("pondlet").Value.Price + ")");
            for (int i = 0; i < 12 && shop.Customers.BuyerTypes(shop.Model.GetPrice("pondlet").Value) == shop.Customers.ArrivingTypes; i++)
            {
                if (!raise.enabledSelf) break;
                yield return CardShiftClick(raise, "Shift +");
            }
            tag = shop.Model.GetPrice("pondlet").Value;
            int refusing = -1;
            for (int i = 0; i < tank.State.Faces.Count; i++)
                if (!StationCardRules.WillBuy(StationCardRules.MoodFor(tank.State.Faces[i].Reaction, tank.State.Faces[i].OverBudget))) { refusing = i; break; }
            Check(refusing >= 0 && shop.Customers.BuyerTypes(tag) < shop.Customers.ArrivingTypes &&
                root.Q<Label>(TankCardView.BuyersName).text == s.Format("card.tank.buyers", shop.Customers.BuyerTypes(tag), shop.Customers.ArrivingTypes),
                "At " + tag.Price + " some faces refuse: " + root.Q<Label>(TankCardView.BuyersName).text);
            var refusingFace = tank.State.Faces[refusing];
            yield return CardHover(root.Q(TankCardView.FaceName(refusingFace.ArchetypeId)), "refusing face");
            string detail = root.Q<Label>(TankCardView.DetailName).text;
            Check(detail.StartsWith(refusingFace.Name, StringComparison.Ordinal) && detail.Contains(refusingFace.Budget.ToString()),
                "Hovering a face shows its exact reaction and budget: " + detail);
            Check(CardShown(root, TankCardView.StandardName) && root.Q<Button>(TankCardView.StandardName).text == s.Format("card.tank.useStandard", standard),
                "Off the standard price the tag offers " + root.Q<Button>(TankCardView.StandardName).text);
            yield return HudShot("ux04-tank-pricey");
            CheckCard(card, "Price tag, pricey", 0);
            yield return CardClick(root.Q<Button>(TankCardView.StandardName), "Use standard");
            Check(shop.Model.GetPrice("pondlet").Value.Price == standard && CardShown(root, TankCardView.StandardChipName), "Use standard puts the price back to " + standard);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.None && !card.IsShown, "Esc closes the card (synthetic keyboard)");

            // ---------- a known recipe: one tile, one shortage ----------
            yield return CardOpenIncubator(incubator);
            var tile = root.Q<Button>(IncubatorCardView.RecipeName("pondlet"));
            bool shortBloom = shop.Model.Available(CultureKind.Bloom) == 0;
            Check(CardShown(tile) && !tile.enabledSelf && CardShown(tile.Q<Label>("es-inc-recipe-need")) &&
                tile.Q<Label>("es-inc-recipe-need").text == s.Format("card.inc.need", s.Get("hud.culture." + (shortBloom ? CultureKind.Bloom : CultureKind.Flow))),
                "The " + pondlet + " recipe tile names what is missing: " + tile.Q<Label>("es-inc-recipe-need").text);
            yield return HudShot("ux04-incubator-recipe-short");
            CheckCard(card, "Incubator with a short recipe", 1);
            yield return CardClick(root.Q<Button>(StationCardPresenter.CloseName), "Close");
            Check(shop.Panel == ShopPanel.None && !card.IsShown, "A real click on Close returns to play");
            yield return Ensure(CultureKind.Bloom, 1);
            yield return Ensure(CultureKind.Flow, 1);
            yield return CardOpenIncubator(incubator);
            Check(tile.enabledSelf && !CardShown(tile.Q<Label>("es-inc-recipe-need")), "With both cultures in the siphon the tile is ready");
            yield return HudShot("ux04-incubator-recipe");
            yield return CardClick(tile, "recipe tile");
            Check(shop.Model.Staged(CultureKind.Bloom) == 1 && shop.Model.Staged(CultureKind.Flow) == 1, "A real click on the recipe tile doses the whole recipe in one step");
            yield return CardClick(root.Q<Button>(IncubatorCardView.ReturnName), "Return doses");
            Check(shop.Model.Staged(CultureKind.Bloom) == 0 && shop.Model.Staged(CultureKind.Flow) == 0, "Return doses puts them back in the siphon");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);

            // ---------- open ----------
            var counter = StationOf(StationKind.Counter);
            yield return CardOpenCounter(counter);
            var desk = card.CounterView;
            Check(card.IsShown && card.CurrentView == desk && card.Title == counter.Label && card.ShopStyled, "Counter opens its clipboard card");
            int displays = 0, emptyDisplays = 0;
            foreach (string id in shop.Model.TankIds) { displays++; if (!shop.Model.GetTank(id).HasValue) emptyDisplays++; }
            string warningKey = StationCardRules.NightWarningKey(displays, emptyDisplays);
            Check(emptyDisplays > 0 && warningKey != null && primary.text == s.Format("card.counter.open", ShopHudRules.Clock(ShopDayCycle.DefaultTradingSeconds)) &&
                primary.enabledSelf && CardShown(reason) && reason.ClassListContains("es-card__reason--warn") && reason.text == s.Format(warningKey, emptyDisplays),
                "At night: " + primary.text + " with the warning above it: " + reason.text + " (" + emptyDisplays + " of " + displays + " displays empty)");
            Check(CardShown(root, CounterCardView.CrewName) && CardShown(root, CounterCardView.ArrangeName) && !CardShown(root, CounterCardView.ReportName),
                "Night links: Crew and Arrange (no report before the first day)");
            yield return HudShot("ux04-counter-night");
            CheckCard(card, "Counter at night", 1);
            yield return CardClick(primary, "Open shop");
            Check(shop.Cycle.Phase == ShopDayPhase.Open, "A real click on Open shop opens the day");
            yield return HudFrames(3);
            Check(primary.text == s.Get("card.counter.serveIdle") && !primary.enabledSelf && reason.text == s.Get("card.counter.why.nobody") &&
                !CardShown(root, CounterCardView.ArrangeName) && CardShown(root, CounterCardView.CloseEarlyName), "Open with nobody waiting: Serve is disabled and says why");

            // ---------- serve ----------
            var visit = Spawn(CustomerArchetypes.Casual);
            float deadline = Time.realtimeSinceStartup + 45f;
            bool sawComing = false;
            while (visit.Phase != CustomerPhase.Queued && Time.realtimeSinceStartup < deadline)
            {
                if (visit.Phase == CustomerPhase.ToCounter && desk.State.Rows.Count > 0 && !sawComing)
                {
                    sawComing = true;
                    yield return HudShot("ux04-counter-coming");
                    CheckCard(card, "Counter, a customer on the way", 1);
                }
                yield return null;
            }
            Check(visit.Phase == CustomerPhase.Queued, "The customer reserves the " + pondlet + " and queues");
            yield return HudFrames(3);
            int price = visit.Price;
            Check(CardShown(root, CounterCardView.RowName(0)) && root.Q(CounterCardView.RowName(0)).Q<Label>("es-counter-species").text == pondlet &&
                root.Q(CounterCardView.RowName(0)).Q<Label>("es-counter-price").text == price.ToString(), "The order row shows " + pondlet + " at " + price);
            Check(root.Q(CounterCardView.RowName(0)).Q<Image>(className: "es-order__coin")?.image != null, "The order row's price carries its coin icon");
            Check(primary.enabledSelf && primary.text == s.Format("card.counter.serve", price) && status.text == s.Format("card.counter.status.waiting", 1),
                "Serve is ready: " + primary.text + " (" + status.text + ")");
            yield return HudShot("ux04-counter-serve");
            CheckCard(card, "Counter, serving", 1);
            int money = shop.Model.Currency;
            yield return CardClick(primary, "Serve");
            Check(shop.Model.Currency == money + price && !shop.Model.GetTank("tank-a").HasValue, "A real click on Serve sells the " + pondlet + " (+" + price + ")");

            // ---------- close ----------
            yield return HudFrames(3);
            yield return CardClick(root.Q<Button>(CounterCardView.CloseEarlyName), "Close early");
            Check(desk.Confirming && CardShown(root, CounterCardView.ConfirmName) && !CardShown(root, CounterCardView.DayRowName) && shop.Cycle.Phase == ShopDayPhase.Open,
                "Close early asks first");
            yield return HudShot("ux04-counter-confirm");
            CheckCard(card, "Counter, close-early confirm", 1);
            yield return CardClick(root.Q<Button>(CounterCardView.KeepName), "Keep trading");
            Check(!desk.Confirming && shop.Cycle.Phase == ShopDayPhase.Open && CardShown(root, CounterCardView.DayRowName), "Keep trading backs out and the day goes on");
            yield return CardClick(root.Q<Button>(CounterCardView.CloseEarlyName), "Close early");
            yield return CardClick(root.Q<Button>(CounterCardView.CloseNowName), "Close now");
            Check(shop.Cycle.Phase == ShopDayPhase.Closing, "Close now closes the shop");
            yield return HudFrames(2);
            Check(status.text == s.Get("card.counter.status.closing") && root.Q<Label>("es-counter-day-heading").text == s.Get("card.counter.shopClosing"),
                "The card reads closing (" + status.text + ")");
            deadline = Time.realtimeSinceStartup + 10f;
            while (shop.Cycle.Phase != ShopDayPhase.Night && Time.realtimeSinceStartup < deadline) yield return null;
            yield return HudFrames(3);
            Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Panel == ShopPanel.DayReport && !card.IsShown, "Night falls and the day report (still a classic panel) replaces the card");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
            yield return CardOpenCounter(counter);
            var report = root.Q<Button>(CounterCardView.ReportName);
            Check(CardShown(report) && report.text == s.Format("card.counter.report", shop.Reports.LastDayNumber) &&
                primary.text == s.Format("card.counter.open", ShopHudRules.Clock(ShopDayCycle.DefaultTradingSeconds)), "Next night the counter links " + report.text);
            yield return HudShot("ux04-counter-next-night");
            CheckCard(card, "Counter, next night", 1);
            yield return CardClick(report, "report");
            Check(shop.Panel == ShopPanel.DayReport && !card.IsShown, "The report link opens the day report");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);

            // ---------- preview off ----------
            Check(shop.SetToolkitUiActive(false) && !shop.ToolkitUiActive, "Preview turns off");
            yield return CardOpenIncubator(incubator);
            Check(shop.Panel == ShopPanel.Incubator && !card.IsShown, "With the preview off the classic panel owns the incubator again");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        void CardInput()
        {
            if (smokeInputSettings == null)
            {
                previousInputSettings = InputSystem.settings;
                smokeInputSettings = Instantiate(previousInputSettings);
                smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                InputSystem.settings = smokeInputSettings;
            }
            if (syntheticKeyboard == null) syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionCardsKeyboard");
            if (cycleMouse == null) cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionCardsMouse");
            cardMouse = cycleMouse;
            syntheticKeyboard.MakeCurrent(); cardMouse.MakeCurrent();
            shop.AllowValidationInput = false;
        }

        IEnumerator CardOpenIncubator(Station station)
        {
            shop.SetPanel(ShopPanel.None);
            var t = station.transform;
            GateAim(t.TransformPoint(new Vector3(0, .05f, -2.15f)), t.TransformPoint(new Vector3(0, 1.5f, -.1f)));
            Physics.SyncTransforms();
            yield return null;
            shop.Interact(station);
            yield return HudFrames(4);
        }

        IEnumerator CardOpenTank(string tankId, Station station)
        {
            shop.SetPanel(ShopPanel.None);
            Check(shop.World.TryGetHabitatForValidation(tankId, out var space, out _, out _, out _), "Display " + tankId + " has a habitat to face");
            Vector3 position = shop.Customers.Navigation.BrowseSpot(tankId, 0);
            position.y = .2f;
            GateAim(position, space.TransformPoint(new Vector3(0, 1.15f, 0)));
            Physics.SyncTransforms();
            yield return null;
            shop.Interact(station);
            yield return HudFrames(4);
        }

        IEnumerator CardOpenCounter(Station counter)
        {
            shop.SetPanel(ShopPanel.None);
            HudFaceCounter(counter);
            Physics.SyncTransforms();
            yield return null;
            shop.Interact(counter);
            yield return HudFrames(4);
        }

        /// <summary>The control's centre in screen pixels (Input System: origin bottom-left) from its panel rectangle.</summary>
        Vector2 CardScreenPoint(VisualElement element)
        {
            Vector2 panel = shop.ToolkitUi.Root.layout.size;
            Vector2 centre = element.worldBound.center;
            return new Vector2(centre.x * Screen.width / panel.x, Screen.height - centre.y * Screen.height / panel.y);
        }

        IEnumerator CardPointer(VisualElement target, string label)
        {
            Check(target != null && CardShown(target), label + " is shown");
            yield return HudFrames(2);
            Vector2 at = CardScreenPoint(target);
            cardMouse.MakeCurrent();
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
        }

        IEnumerator CardClick(Button button, string label)
        {
            Check(button != null && CardShown(button) && button.enabledInHierarchy, label + " is shown and enabled");
            yield return CardPointer(button, label);
            Vector2 at = CardScreenPoint(button);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at, buttons = 1 });
            yield return HudFrames(2);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
        }

        IEnumerator CardShiftClick(Button button, string label)
        {
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.LeftShift));
            yield return null;
            yield return CardClick(button, label);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return HudFrames(2);
        }

        IEnumerator CardWheel(VisualElement target, bool up, string label)
        {
            yield return CardPointer(target, label);
            Vector2 at = CardScreenPoint(target);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at, scroll = new Vector2(0, up ? 120 : -120) });
            yield return null;
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
        }

        IEnumerator CardHover(VisualElement target, string label)
        {
            yield return CardPointer(target, label);
            yield return HudFrames(2);
        }

        IEnumerator CardKey(Key key)
        {
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(key));
            yield return HudFrames(2);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return HudFrames(3);
        }

        static bool CardShown(VisualElement element) => element != null && HudPresenter.Visible(element);
        static bool CardShown(VisualElement root, string name) => CardShown(root.Q(name));

        static int CardFaces(VisualElement root)
        {
            int shown = 0;
            foreach (var face in root.Query<MoodFace>().ToList()) if (CardShown(face)) shown++;
            return shown;
        }

        static int CardPrimaries(VisualElement card)
        {
            int shown = 0;
            foreach (var button in card.Query<Button>(className: StationCardPresenter.PrimaryClass).ToList()) if (CardShown(button)) shown++;
            return shown;
        }

        /// <summary>
        /// Null when every visible text in the card fits the box it is drawn in (measured at that width) and every visible
        /// element sits inside the card; else the first problem. A box can fit while its text spills (UX-02 lesson).
        /// </summary>
        static string CardTextProblem(VisualElement card)
        {
            Rect outer = card.worldBound;
            foreach (var element in card.Query<VisualElement>().ToList())
            {
                if (element == card || !CardShown(element)) continue;
                Rect r = element.worldBound;
                if (r.width <= 0 || r.height <= 0) continue;
                if (r.xMin < outer.xMin - .5f || r.yMin < outer.yMin - .5f || r.xMax > outer.xMax + .5f || r.yMax > outer.yMax + .5f)
                    return element.name + " leaves the card " + r + " / " + outer;
                if (!(element is TextElement text) || string.IsNullOrEmpty(text.text)) continue;
                // Wrap at the content width; the text must then fit inside the borders. Text running into the padding
                // is drawn unclipped and reads fine (the Esc keycap); text past the border is what spills or clips.
                var style = text.resolvedStyle;
                // One pixel of slack: on the scaled ultrawide panel a shrink-to-fit label is laid out a fraction of a pixel
                // narrower than its measured text, which would wrap a one-line word only in the measurement.
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

        void CheckCard(StationCardPresenter card, string state, int primaries)
        {
            var element = card.Element;
            var root = shop.ToolkitUi.Root;
            Check(card.IsShown && ShopUiTestDriver.FullyInside(element, root), state + ": the card is fully on screen " + element.worldBound + " in " + root.worldBound);
            Check(element.Query<ScrollView>().ToList().Count == 0 && element.worldBound.height <= root.worldBound.height,
                state + ": nothing scrolls; the card is " + element.worldBound.height.ToString("0") + " of " + root.worldBound.height.ToString("0") + " high");
            int shown = CardPrimaries(element);
            Check(shown == primaries, state + ": " + shown + " primary button(s) shown, expected " + primaries);
            string problem = CardTextProblem(element);
            Check(problem == null, state + ": every text fits inside the card" + (problem != null ? " / " + problem : ""));
            Check(!card.Placement.CoversAnchor, state + ": the card sits beside its station, not on it");
        }
    }
}
#endif
