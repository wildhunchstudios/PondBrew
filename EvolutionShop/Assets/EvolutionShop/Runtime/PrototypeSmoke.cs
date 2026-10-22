#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    // AD-E gameplay prototype (-evolutionPrototypeSmoke): the shop's stars and what each unlocks; a display themed by a
    // bubble lamp; a 5:30 day with its rush; visitors arriving as tomorrow's forecast said, with their wishes; the hands-on
    // checkout by a real held mouse at the counter, with an exact wish's tip and point and a close match's haggling; the
    // day's measurements in the log and on the night receipt with the next forecast; comfort; and the prototype off.
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class PrototypeTrace
        {
            public List<string> forecast = new List<string>(), arrivals = new List<string>(), sales = new List<string>(), notes = new List<string>();
            public List<string> nextForecast = new List<string>();
            public string nextArrival = "", dayLog = "";
            public float rushGap = -1, maxHoldProgressBeforeSale;
        }
        readonly PrototypeTrace protoTrace = new PrototypeTrace();
        int protoExact, protoClose;

        IEnumerator PrototypeScenario()
        {
            limitations.Add("AD-E gameplay prototype: isolated slot started from the frozen v15 save (night, Spark researched, four species discovered); reputation set by labelled save fixtures for each star; a small display and a bubble lamp bought through the real placement controls; creatures made by labelled accelerated incubation and put on display by the game's own transfer; the day clock jumped to the rush by the session's own Advance; visitors are real arrivals walking real routes, served by a synthetic left mouse held on the counter. Placeholder art (AD.6); screens are not the user's playtest, which is the gate.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "AD-E isolated save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("AD-E keyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("AD-E mouse"); cardMouse = cycleMouse;
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            var customers = shop.Customers;
            customers.ArrivalsEnabled = false;
            customers.UseBaseReputationEffectsForValidation = false;
            customers.ForcedArchetypeId = null; customers.ScriptedArchetypes.Clear();
            customers.MaxVisitors = 2;
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows resolution " + width + "x" + height);
            }
            string fixture = Application.isEditor ?
                Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V15Conditioning.json") :
                Path.Combine(evidence, "seed-v15.json");
            File.WriteAllBytes(shop.SavePath, File.ReadAllBytes(fixture));
            Check(shop.ReloadSaved() && shop.Cycle.Phase == ShopDayPhase.Night && shop.Model.SparkResearchUnlocked && shop.Model.DiscoveredSpeciesIds.Count == 4,
                "The frozen v15 save loads: night, Spark researched, four species discovered");
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            Check(shop.PrototypeOn && shop.PrototypeActive && shop.Tutorial.Mode != TutorialMode.Active, "The prototype route switches the gameplay prototype on (no guided opening running)");
            Check(shop.TradingDaySeconds == ShopDayPlan.PrototypeSeconds, "The next opening is a " + ShopHudRules.Clock(shop.TradingDaySeconds) + " day");
            yield return ProtoStars();
            yield return ProtoThemes();
            yield return ProtoDay();
            yield return ProtoReceipt();
            yield return ProtoOff();
            File.WriteAllText(Path.Combine(evidence, "prototype-trace.json"), JsonUtility.ToJson(protoTrace, true));
            shop.SetPanel(ShopPanel.Pause);
        }

        // ---------------------------------------------------------------- helpers

        void ProtoReputation(int points)
        {
            Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Customers.ActiveCount == 0 && shop.SaveNow(), "Reputation fixture requires an isolated saved night / " + points);
            var slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); slot.reputation.points = points;
            Check(SaveStore.Write(shop.SavePath, slot, out var error) && shop.ReloadSaved() && shop.Reputation.Points == points, "Load labelled " + points + "-reputation fixture / " + error);
        }

        void ProtoProduce(string speciesId)
        {
            var species = shop.Model.GetSpecies(speciesId); shop.Model.Tick(30);
            foreach (var (kind, count) in new[] { (CultureKind.Bloom, species.Bloom), (CultureKind.Flow, species.Flow), (CultureKind.Spark, species.Spark), (CultureKind.Mist, species.Mist) })
                for (int i = 0; i < count; i++) { Ok(shop.Model.Collect(kind), "Collect fixture " + kind); Ok(shop.Model.Dose(kind), "Dose fixture " + kind); }
            Ok(shop.Model.BeginIncubation(), "Begin fixture incubation"); shop.Model.Tick(30); shop.World.Refresh(shop.Model);
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.Model.Incubator.SpeciesId == speciesId, "Produce labelled accelerated " + speciesId);
        }

        void ProtoStock(string tank, string speciesId)
        {
            if (shop.Model.GetTank(tank).HasValue) return;
            ProtoProduce(speciesId);
            Ok(shop.TransferToDisplay(tank), "Stock " + speciesId + " on " + tank + " by the game's own transfer");
        }

        /// <summary>A tag every visitor type that may come today can pay (within budget and tolerance).</summary>
        void ProtoAffordable(string speciesId)
        {
            int standard = shop.Model.GetPrice(speciesId).Value.Standard, price = standard;
            foreach (var type in shop.Customers.Archetypes)
                if (type.Weight > 0) price = Math.Min(price, Math.Min(type.Budget, ShopPricing.AcceptableLimit(standard, type.TolerancePercent)));
            if (shop.Model.GetPrice(speciesId).Value.Price != price) Ok(shop.SetSpeciesPrice(speciesId, Math.Max(1, price)), "Tag " + speciesId + " at " + price);
        }

        IEnumerator ProtoWait(Func<bool> condition, float timeout, string reason)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("AD-E waiting for " + reason + "; status=" + shop.Customers.Status + "; nav=" + shop.Customers.Navigation.LastError);
                yield return null;
            }
        }

        IEnumerator ProtoShot(string name, Vector3 eye, Vector3 target)
        {
            DioramaView(eye, target);
            yield return null;
            shop.World.SnapDioramaLightForValidation();
            yield return DioramaShot(name);
        }

        /// <summary>A view of a visitor from inside the store: from their position toward the store's middle, up to 2.8 m back.</summary>
        IEnumerator ProtoVisitorShot(string name, CustomerActor actor, float height)
        {
            Vector3 at = actor.transform.position, middle = shop.World.transform.TransformPoint(new Vector3(.5f, 0, -1.2f));
            middle.y = at.y;
            Vector3 toward = middle - at;
            float distance = Mathf.Min(2.8f, Mathf.Max(.5f, toward.magnitude - .2f));
            Vector3 eye = toward.sqrMagnitude > .01f ? at + toward.normalized * distance : at + new Vector3(0, 0, 2.5f);
            yield return ProtoShot(name, new Vector3(eye.x, .2f, eye.z), at + Vector3.up * height);
        }

        static int ProtoWeight(IReadOnlyList<CustomerArchetype> types, string id) { var t = CustomerArchetypes.Find(types, id); return t == null ? -1 : t.Weight; }

        static string ProtoTypes(IReadOnlyList<CustomerArchetype> types) => string.Join(",", types.Select(t => t.Id + "=" + t.Weight + "/" + t.Budget));

        CustomerArchetype[] ProtoClassicCatalog()
            => shop.Appeal.ApplyTo(CustomerArchetypes.Validate(CustomerArchetypes.CreateDefault(), id => Array.Exists(shop.Config.Species, s => s.Id == id)), shop.Reputation);

        // ---------------------------------------------------------------- stars

        IEnumerator ProtoStars()
        {
            var w = shop.World;
            foreach (var (points, stars) in new[] { (0, 1), (34, 3), (60, 4), (90, 5) })
            {
                ProtoReputation(points);
                yield return null; yield return null;
                Check(shop.Stars == stars && w.PrototypeSignShown && w.PrototypeStarsShown == stars,
                    "The storefront sign shows " + stars + " star(s) at " + points + " reputation / " + w.PrototypeStarsShown);
                Check(w.PrototypeSignNextText.StartsWith(stars == ShopStars.Max ? "FIVE STARS" : "NEXT STAR AT " + ShopStars.NextThreshold(points)),
                    "The sign says what the next star needs and unlocks / " + w.PrototypeSignNextText);
                var types = shop.Customers.Archetypes;
                Check(ProtoWeight(types, CustomerArchetypes.Casual) > 0 && ProtoWeight(types, CustomerArchetypes.Bargain) > 0 &&
                    (ProtoWeight(types, CustomerArchetypes.Collector) > 0) == (stars >= 2) && (ProtoWeight(types, CustomerArchetypes.Premium) > 0) == (stars >= 3),
                    "Arriving types follow the stars (Collectors at two, Big spenders at three) / " + ProtoTypes(types));
                var classic = ProtoClassicCatalog();
                int classicBudget = CustomerArchetypes.Find(classic, CustomerArchetypes.Casual).Budget, budget = CustomerArchetypes.Find(types, CustomerArchetypes.Casual).Budget;
                Check(budget == (stars >= ShopStars.BudgetStars ? (int)Math.Round(classicBudget * 1.25, MidpointRounding.AwayFromZero) : classicBudget),
                    "Four stars raise budgets by a quarter / " + classicBudget + " -> " + budget);
                var sign = w.StarSign.position;
                yield return ProtoShot("proto-stars-" + stars, new Vector3(sign.x + 1.2f, .2f, sign.z - 3.6f), sign + Vector3.down * .1f);
            }
            ProtoReputation(34);
            yield return null;
            shop.SetPrototype(false); yield return null;
            var off = shop.Customers.Archetypes; var plain = ProtoClassicCatalog();
            Check(!w.PrototypeSignShown && shop.TradingDaySeconds == ShopDayCycle.DefaultTradingSeconds && ProtoTypes(off) == ProtoTypes(plain),
                "Switched off: no star sign, the classic 8:00 day and exactly the classic arrivals / " + ProtoTypes(off));
            shop.SetPrototype(true); yield return null;
            Check(w.PrototypeSignShown && shop.TradingDaySeconds == ShopDayPlan.PrototypeSeconds, "Switched on again");
        }

        // ---------------------------------------------------------------- themes

        IEnumerator ProtoPlace(string definition, int x, int z, Vector3 player)
        {
            shop.AllowValidationInput = true; yield return PlacementAimAt(x, z, player);
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(null, definition), "Open the " + definition + " preview");
            yield return PlacementNeutral();
            Check(shop.PlacementValid, "Valid " + definition + " footprint and access / " + shop.PlacementMessage);
            int items = shop.Layout.Items.Count;
            yield return PlacementClick();
            Check(!shop.PlacementActive && shop.Layout.Items.Count == items + 1, "Bought " + definition + " through the real placement controls");
            shop.AllowValidationInput = false;
        }

        IEnumerator ProtoThemes()
        {
            var w = shop.World;
            var before = shop.Customers.Archetypes;
            yield return ProtoPlace(ShopLayout.SmallDisplayId, 3, -5, new Vector3(1.5f, .2f, -4.7f));
            string display = shop.Layout.Items.Last(i => i.DefinitionId == ShopLayout.SmallDisplayId).Id, slot = shop.Layout.GetSlotIds(display)[0];
            yield return ProtoPlace(ShopLayout.BubbleLampId, 4, -1, new Vector3(2, .2f, -2.1f));
            Check(shop.Layout.ThemeOf(slot) == ShopTheme.Glow && shop.Layout.ThemeOf("tank-a") == ShopTheme.None && shop.Layout.ThemeOf("tank-b") == ShopTheme.None,
                "The bubble lamp beside the new display gives it the Glow theme; the fixed tanks stay unthemed");
            for (int i = 0; i < 20; i++) yield return null;
            Check(w.ThemeTagsShown == 1 && w.ThemeTagTexts.First() == "GLOW THEME (SPARKLINE)", "The empty display's tag names its theme / " + string.Join("|", w.ThemeTagTexts));
            int premiumBefore = ProtoWeight(shop.Customers.Archetypes, CustomerArchetypes.Premium);
            ProtoStock(slot, "lanternfin");
            for (int i = 0; i < 20; i++) yield return null;
            Check(w.ThemeTagsShown == 1 && w.ThemeTagTexts.First() == "GLOW THEME: SPARKLINE +25%", "A Lanternfin in the Glow display matches its theme / " + string.Join("|", w.ThemeTagTexts));
            int premium = ProtoWeight(shop.Customers.Archetypes, CustomerArchetypes.Premium);
            Check(premium == premiumBefore + ShopThemes.AttractWeight, "The matching Glow display draws Big spenders / " + premiumBefore + " -> " + premium);
            protoTrace.notes.Add("Glow display " + slot + "; Big spender weight " + premiumBefore + " -> " + premium + "; before the purchases " + ProtoTypes(before));
            Vector3 centre = shop.World.transform.TransformPoint(new Vector3(1.5f, 0, -2.5f));
            yield return ProtoShot("proto-theme-glow", centre + new Vector3(-1.8f, .2f, -2.6f), centre + Vector3.up * 1.4f);
        }

        // ---------------------------------------------------------------- the day

        IEnumerator ProtoDay()
        {
            var customers = shop.Customers;
            int day = shop.Cycle.DayNumber;
            var forecast = shop.Forecast();
            Check(forecast.Count == ShopSession.ForecastVisitors && forecast.All(f => f.Wish != null), "Tonight's forecast names tomorrow's first five visitors and their wishes / " + forecast.Count);
            foreach (var f in forecast) protoTrace.forecast.Add(f.Archetype + " / " + f.Wish);
            var wishes = new ShopWish[forecast.Count]; var types = new CustomerArchetype[forecast.Count];
            for (int i = 0; i < forecast.Count; i++) { types[i] = customers.ForecastArchetype(day, i); wishes[i] = customers.WishFor(day, i, types[i]); }
            var discovered = shop.Model.DiscoveredSpeciesIds.ToList();
            string exact0 = wishes[0].Exact.FirstOrDefault(id => discovered.Contains(id) && id != "lanternfin");
            if (exact0 != null) { ProtoAffordable(exact0); ProtoStock("tank-a", exact0); }
            string close1 = wishes[1].Close.FirstOrDefault(id => discovered.Contains(id) && id != exact0);
            string second = close1 ?? wishes[1].Exact.FirstOrDefault(id => discovered.Contains(id) && id != "lanternfin");
            if (second != null) { ProtoAffordable(second); ProtoStock("tank-b", second); }
            ProtoAffordable("lanternfin");
            var card = new CounterCardState(); shop.FillCounterCard(card);
            Check(card.TradingSeconds == ShopDayPlan.PrototypeSeconds && card.HandsOn, "The counter opens a 5:30 prototype day");
            int cash = shop.Model.Currency, rep = shop.Reputation.Points;

            Check(shop.OpenForDay(), "Open the prototype day");
            Check(shop.Cycle.DayLength == ShopDayPlan.PrototypeSeconds && Math.Abs(shop.Cycle.SecondsRemaining - ShopDayPlan.PrototypeSeconds) < .5f && shop.PrototypeDay != null,
                "The day lasts 5:30 / " + shop.Cycle.SecondsRemaining);
            Check(!shop.InRush, "No rush at opening");
            // The rush: the session's own clock jumps to it; arrivals then come as soon as there is room.
            shop.Advance(ShopDayPlan.RushStartsAt(shop.Cycle.DayLength) + 1 - (shop.Cycle.DayLength - shop.Cycle.SecondsRemaining));
            yield return null; yield return null;
            Check(shop.InRush && customers.NextArrivalSeconds == ShopDayPlan.RushArrivalSeconds, "Halfway through the day the rush begins / " + customers.NextArrivalSeconds);
            customers.ArrivalsEnabled = true;
            yield return ProtoWait(() => customers.ActiveCount >= 1, 6, "the rush's first arrival");
            float first = Time.realtimeSinceStartup;
            yield return ProtoWait(() => customers.ActiveCount >= 2, 6, "the rush's second arrival");
            protoTrace.rushGap = Time.realtimeSinceStartup - first;
            customers.ArrivalsEnabled = false;
            Check(protoTrace.rushGap <= ShopDayPlan.RushArrivalSeconds + .6f, "The rush brings the next visitor within 1.5 s of room / " + protoTrace.rushGap.ToString("0.00") + " s");
            var visits = customers.Visits.ToList();
            for (int i = 0; i < 2; i++)
            {
                var visit = visits[i];
                protoTrace.arrivals.Add(visit.ArchetypeName + " / " + (visit.Wish != null ? visit.Wish.Label : "-"));
                Check(visit.ArchetypeId == types[i].Id && visit.Wish != null && visit.Wish.Key == wishes[i].Key &&
                    visit.ArchetypeName == forecast[i].Archetype && visit.Wish.Label == forecast[i].Wish,
                    "Arrival " + (i + 1) + " is the forecast's: " + visit.ArchetypeName + " wishing for " + (visit.Wish != null ? visit.Wish.Label : "nothing"));
                var bubble = customers.ActorOf(visit) ? customers.ActorOf(visit).GetComponent<CustomerWishBubble>() : null;
                Check(bubble && bubble.Shown && bubble.Text == wishes[i].Short, "The visitor's wish bubble shows " + wishes[i].Short + " / " + (bubble ? bubble.Text : "none"));
            }
            yield return new WaitForSeconds(1.2f);
            var actor0 = customers.ActorOf(visits[0]);
            if (actor0) yield return ProtoVisitorShot("proto-rush-wishes", actor0, 2.2f);
            int index = 2;
            while (customers.ActiveCount > 0 || protoExact == 0 || protoClose == 0)
            {
                if (customers.ActiveCount == 0)
                {
                    if (index >= wishes.Length) break;
                    // A later visitor: stock a close match for a species wish (or an exact one) before they arrive.
                    var wish = wishes[index];
                    string pick = wish.Close.FirstOrDefault(id => discovered.Contains(id)) ?? wish.Exact.FirstOrDefault(id => discovered.Contains(id) && id != "lanternfin");
                    if (pick != null && !shop.Model.GetTank("tank-a").HasValue) { ProtoAffordable(pick); ProtoStock("tank-a", pick); }
                    customers.MaxVisitors = 1;
                    Check(customers.SpawnNow(), "Visitor " + (index + 1) + " arrives");
                    var visit = customers.Visits.Last();
                    protoTrace.arrivals.Add(visit.ArchetypeName + " / " + (visit.Wish != null ? visit.Wish.Label : "-"));
                    // Sales since the forecast can change who arrives (a sold theme no longer draws); the wish always follows the arrival.
                    var expected = customers.WishFor(day, index, visit.Archetype);
                    Check(visit.Wish != null && expected != null && visit.Wish.Key == expected.Key, "Visitor " + (index + 1) + " wishes as the rules pick for " + visit.ArchetypeName + " / " + visit.Wish?.Label);
                    index++;
                }
                var queued = customers.Visits.FirstOrDefault(v => v.Phase == CustomerPhase.Queued);
                var leaving = customers.Visits.All(v => v.Phase == CustomerPhase.Leaving || v.Phase == CustomerPhase.Paid || v.Phase == CustomerPhase.Finished);
                if (queued != null) { yield return ProtoCheckout(queued); continue; }
                if (leaving) { yield return ProtoWait(() => customers.ActiveCount == 0 || customers.Visits.Any(v => v.Phase == CustomerPhase.Queued), 20, "visitors to leave"); continue; }
                yield return null;
                if (shop.Cycle.SecondsRemaining < 20) break;
            }
            Check(protoExact >= 1 && protoClose >= 1, "The day had an exact wish and a close one / " + protoExact + " exact, " + protoClose + " close");
            Check(shop.Model.Currency > cash && shop.Reputation.Points > rep, "The day's sales and wishes paid / " + cash + " -> " + shop.Model.Currency + " credits, " + rep + " -> " + shop.Reputation.Points + " reputation");
            // Closing: the session's own clock runs out the day.
            customers.ArrivalsEnabled = false;
            yield return ProtoWait(() => customers.ActiveCount == 0, 25, "the last visitor to leave");
            shop.Advance(shop.Cycle.SecondsRemaining + .1f);
            yield return ProtoWait(() => shop.Cycle.Phase == ShopDayPhase.Night, 10, "night");
            var log = shop.LastPrototypeDay;
            Check(log != null && log.day == day && log.length == ShopDayPlan.PrototypeSeconds && log.sales == protoTrace.sales.Count &&
                log.exact == protoExact && log.close == protoClose && log.handsOnSales == log.sales && log.rushArrivals >= 2 && log.visitors >= index && log.wishes == log.visitors,
                "The day's measurements: " + JsonUtility.ToJson(log));
            Check(File.Exists(shop.PrototypeLogPath) && File.ReadAllLines(shop.PrototypeLogPath).Length == 1, "The day is appended to the prototype log / " + shop.PrototypeLogPath);
            protoTrace.dayLog = File.ReadAllText(shop.PrototypeLogPath).Trim();
            customers.MaxVisitors = 2;
        }

        /// <summary>The hands-on checkout: stand at the counter, aim at it and hold the left mouse button until the order is bagged.</summary>
        IEnumerator ProtoCheckout(CustomerVisit visit)
        {
            var counter = StationOf(StationKind.Counter);
            shop.SetPanel(ShopPanel.None);
            shop.Player.Teleport(counter.transform.TransformPoint(new Vector3(0, .2f, -2.2f)), counter.transform.rotation);
            CycleAim(counter); Physics.SyncTransforms();
            yield return null; yield return null;
            Check(shop.Focused == counter && shop.IsAtCounter && shop.CheckoutHoldAvailable, "At the counter, aimed at it, with an order waiting / focus=" + (shop.Focused ? shop.Focused.Kind.ToString() : "none"));
            if (protoTrace.sales.Count == 0)
            {
                shop.Interact(counter); yield return null; yield return null; yield return null;
                var view = shop.ToolkitUi.StationCard.CounterView;
                Check(view != null && view.PrimaryText == shop.ToolkitUi.Strings.Get("proto.card.handsOn") && !view.PrimaryEnabled,
                    "The counter card's Serve is replaced by the hands-on hold / " + (view != null ? view.PrimaryText : "no card"));
                yield return DioramaShot("proto-counter-card");
                shop.SetPanel(ShopPanel.None); yield return null;
                CycleAim(counter); Physics.SyncTransforms(); yield return null;
            }
            int cash = shop.Model.Currency, rep = shop.Reputation.Points, price = visit.Price, tag = visit.TagPrice;
            var match = visit.WishMatch;
            string species = visit.SpeciesId;
            float began = Time.realtimeSinceStartup, peak = 0;
            bool shot = false;
            var hud = new ShopHudState();
            while (visit.Phase == CustomerPhase.Queued)
            {
                if (Time.realtimeSinceStartup - began > 4) throw new TimeoutException("The held counter never bagged the order; progress=" + shop.CheckoutProgress + " focus=" + shop.Focused);
                CycleAim(counter);
                cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 });
                yield return null;
                peak = Mathf.Max(peak, shop.CheckoutProgress);
                if (!shot && shop.CheckoutProgress > .35f)
                {
                    shot = true;
                    shop.FillHudState(hud, shop.ToolkitUi.Strings);
                    Check(hud.Prompt.Shown && hud.Prompt.Key == shop.ToolkitUi.Strings.Get("hud.key.hold") && hud.Prompt.Progress > .3f && shop.World.CheckoutBagFill > .3f,
                        "Holding fills the HUD's hold prompt and the bag on the counter / " + hud.Prompt.Progress.ToString("0.00") + ", bag " + shop.World.CheckoutBagFill.ToString("0.00"));
                    if (protoTrace.sales.Count == 0) ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "proto-checkout-hold-screen.png"));
                }
            }
            float held = Time.realtimeSinceStartup - began;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState());
            yield return null; yield return null;
            protoTrace.maxHoldProgressBeforeSale = Mathf.Max(protoTrace.maxHoldProgressBeforeSale, peak);
            Check(visit.Outcome == CustomerOutcome.Sold && held >= ShopSession.CheckoutSeconds * .85f && held < 2.5f,
                "The held mouse bags the order in about a second / " + held.ToString("0.00") + " s");
            int tip = match == WishMatch.Exact ? ShopWishes.Tip(price) : 0;
            Check(shop.Model.Currency == cash + price + tip && shop.Reputation.Points == Math.Min(ShopReputation.MaxPoints, rep + 2 + (match == WishMatch.Exact ? 1 : 0)),
                "The sale pays " + price + (tip > 0 ? " plus a " + tip + " tip and a wish point" : "") + " / " + cash + " -> " + shop.Model.Currency + ", reputation " + rep + " -> " + shop.Reputation.Points);
            if (match == WishMatch.Close)
                Check(visit.Haggled && price == ShopWishes.Haggled(tag) && tag > price, "A close match haggled " + tag + " down to " + price);
            if (match == WishMatch.Exact) protoExact++;
            if (match == WishMatch.Close) protoClose++;
            protoTrace.sales.Add(visit.ArchetypeName + " / " + (visit.Wish != null ? visit.Wish.Label : "-") + " / " + species + " / " + match + " / " + tag + " -> " + price + " +" + tip);
            if (match == WishMatch.Close && protoClose == 1)
            {
                var actor = shop.Customers.ActorOf(visit);
                if (actor) yield return ProtoVisitorShot("proto-haggle-paid", actor, 2.1f);
            }
            if (match == WishMatch.Exact && protoExact == 1)
            {
                var actor = shop.Customers.ActorOf(visit);
                if (actor) yield return ProtoVisitorShot("proto-wish-paid", actor, 2.1f);
            }
        }

        // ---------------------------------------------------------------- the receipt and the next day

        IEnumerator ProtoReceipt()
        {
            var ui = shop.ToolkitUi;
            Check(shop.ShowDayReport(), "The night receipt opens");
            yield return new WaitForSecondsRealtime(1.2f);
            var state = ui.Screens.Report.State;
            Check(state.Prototype && state.Forecast.Count == ShopSession.ForecastVisitors && state.DayStats == shop.LastPrototypeDay && state.Stars == shop.Stars,
                "The receipt shows the stars, the day's measurements and tomorrow's five visitors / " + state.Forecast.Count);
            var next = shop.Forecast();
            foreach (var f in next) protoTrace.nextForecast.Add(f.Archetype + " / " + f.Wish);
            yield return DioramaShot("proto-receipt");
            Check(shop.ScreenNextNight(), "Next night");
            // Tomorrow's first arrival is the forecast's first.
            if (!shop.Model.GetTank("tank-a").HasValue) { ProtoAffordable("pondlet"); ProtoStock("tank-a", "pondlet"); }
            Check(shop.OpenForDay() && shop.Cycle.DayLength == ShopDayPlan.PrototypeSeconds, "Open the next prototype day");
            shop.Customers.MaxVisitors = 1;
            Check(shop.Customers.SpawnNow(), "The next day's first visitor arrives");
            var visit = shop.Customers.Visits.Last();
            protoTrace.nextArrival = visit.ArchetypeName + " / " + (visit.Wish != null ? visit.Wish.Label : "-");
            Check(visit.ArchetypeName == next[0].Archetype && visit.Wish != null && visit.Wish.Label == next[0].Wish, "The forecast's first visitor arrives as forecast / " + protoTrace.nextArrival);
            // Comfort: reduced motion keeps the bubble still and the bag from wobbling.
            var bubble = shop.Customers.ActorOf(visit).GetComponent<CustomerWishBubble>();
            var actor = shop.Customers.ActorOf(visit);
            Check(shop.SetComfortSettings(true, FxEffectDetail.Standard), "Reduced visual motion");
            float spread = 0;
            for (int i = 0; i < 20; i++) { yield return null; spread = Mathf.Max(spread, Mathf.Abs(bubble.WorldPosition.y - actor.CaptionHeight - CustomerWishBubble.Lift)); }
            shop.World.PresentCheckoutHold(.5f, Vector3.zero, true);
            var bag = shop.World.CheckoutBagFill;
            Check(spread < .001f && bag > 0, "Reduced motion: the wish bubble keeps still / " + spread.ToString("0.0000"));
            Check(shop.SetComfortSettings(false, FxEffectDetail.Standard), "Standard comfort");
            spread = 0;
            for (int i = 0; i < 40; i++) { yield return null; spread = Mathf.Max(spread, Mathf.Abs(bubble.WorldPosition.y - actor.CaptionHeight - CustomerWishBubble.Lift)); }
            Check(spread > .005f, "Standard motion: the wish bubble bobs / " + spread.ToString("0.000"));
            Check(shop.CloseForNight(), "Close the second day");
            yield return ProtoWait(() => shop.Cycle.Phase == ShopDayPhase.Night, 25, "night after the second day");
            Check(File.ReadAllLines(shop.PrototypeLogPath).Length == 2, "Each prototype day is logged");
            if (shop.Panel == ShopPanel.DayReport) shop.ScreenNextNight();
        }

        // ---------------------------------------------------------------- off

        IEnumerator ProtoOff()
        {
            var w = shop.World;
            shop.SetPrototype(false);
            for (int i = 0; i < 20; i++) yield return null;
            Check(!w.PrototypeSignShown && w.ThemeTagsShown == 0 && !shop.PrototypeActive, "Off: no star sign and no theme tags");
            var card = new CounterCardState(); shop.FillCounterCard(card);
            Check(card.TradingSeconds == ShopDayCycle.DefaultTradingSeconds && !card.HandsOn, "Off: the counter opens the classic 8:00 day with Serve");
            if (!shop.Model.GetTank("tank-a").HasValue) ProtoStock("tank-a", "pondlet");
            Check(shop.OpenForDay() && shop.Cycle.DayLength == ShopDayCycle.DefaultTradingSeconds && shop.PrototypeDay == null, "Off: the day lasts 8:00 and is not logged");
            Check(shop.Customers.SpawnNow(), "Off: a visitor arrives");
            var visit = shop.Customers.Visits.Last();
            Check(visit.Wish == null && !shop.Customers.ActorOf(visit).GetComponent<CustomerWishBubble>(), "Off: visitors arrive without wishes or bubbles");
            yield return new WaitForSeconds(1.5f);
            var actor = shop.Customers.ActorOf(visit);
            if (actor) yield return ProtoVisitorShot("proto-off", actor, 1.8f);
            Check(shop.CloseForNight(), "Close the classic day");
            yield return ProtoWait(() => shop.Cycle.Phase == ShopDayPhase.Night, 25, "night after the classic day");
            Check(File.ReadAllLines(shop.PrototypeLogPath).Length == 2, "A classic day is not logged");
            if (shop.Panel == ShopPanel.DayReport) shop.ScreenNextNight();
            shop.SetPrototype(true);
        }
    }
}
#endif
