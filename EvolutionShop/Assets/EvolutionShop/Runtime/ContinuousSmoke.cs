#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable]
        sealed class ContinuousMetrics
        {
            public string scope = "Automated fresh-save two-day technical route; not human enjoyment or pacing acceptance.";
            public string input = "Synthetic keyboard movement/E/Escape and held mouse harvesting; scripted camera aim; application recipe/open/serve/close/save/reload commands.";
            public string stage;
            public bool completed;
            public float totalSeconds;
            public float preparationSeconds;
            public float firstTradingSeconds;
            public float secondTradingSeconds;
            public float walkingSeconds;
            public float walkingMetres;
            public float harvestingSeconds;
            public float incubationWaitSeconds;
            public float customerWaitSeconds;
            public float pausedSeconds;
            public int naturalProductions;
            public int inputDisplayTransfers;
            public int doorwayCrossings;
            public int navigationSamples;
            public int dayNumber;
            public int money;
            public int displayedStock;
            public Vector3 playerPosition;
            public TradeDaySave dayOne;
            public TradeDaySave dayTwo;
        }

        Mouse cycleMouse;
        ContinuousMetrics cycleMetrics;
        float cycleStarted;

        // Root's shared smoke runner calls this on both success and failure, with RestoreInput.
        void RestoreCycleMouse()
        {
            if (cycleMouse != null && cycleMouse.added) InputSystem.RemoveDevice(cycleMouse);
            cycleMouse = null;
        }

        IEnumerator ContinuousScenario()
        {
            cycleStarted = Time.realtimeSinceStartup;
            cycleMetrics = new ContinuousMetrics();
            limitations.Add("Continuous route uses synthetic keyboard/held mouse input and scripted camera aim. Recipe, open, serve, close, save and reload use application commands; native GUI clicks are a separate probe.");
            limitations.Add("Both trading days close early after the specified transactions. Natural production/customer timing here does not establish eight-minute-shift balance, human enjoyment, workload or audio approval.");
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionCycleSmoke") != null,
                "Continuous route requires isolated evolutionSmoke and evolutionCycleSmoke flags");
            Check(Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Continuous route uses only its isolated evidence save");
            Check(shop.Model.Currency == 0 && shop.DisplayedStockCount() == 0 && shop.Model.DiscoveredSpeciesIds.Count == 0 &&
                shop.Cycle.DayNumber == 1 && shop.Cycle.Phase == ShopDayPhase.Night && shop.Reports.Last == null,
                "Continuous route starts fresh with no seeded progression or report history");
            Check(Mathf.Approximately(shop.Config.IncubationSeconds, 10f) &&
                Mathf.Approximately(shop.Model.CollectionSecondsPerUnit, 0.6f),
                "Continuous route uses ordinary ten-second incubation and 0.6-second harvesting");
            Check(shop.ScriptedHarvest == null, "Continuous harvesting starts without a scripted harvest override");
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionCycleKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionCycleMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false;
            shop.SetPanel(ShopPanel.None);
            yield return CycleUntil(() => shop.Customers.Ready, 10f, "initial customer navigation", false);
            CycleStage("Preparation night 1: physically grow and display two Pondlets");

            yield return CycleProducePondlet();
            yield return CycleTransfer(StationKind.TankA, "tank-a");
            yield return Capture("40-cycle-first-stock");
            yield return CycleProducePondlet();
            yield return CycleTransfer(StationKind.TankB, "tank-b");
            Check(shop.DisplayedStockCount() == 2 && shop.Model.Currency == 0 && shop.Cycle.Phase == ShopDayPhase.Night,
                "Night preparation naturally produces and displays two unsold Pondlets");
            string firstA = shop.Model.GetTank("tank-a").Value.Id;
            string firstB = shop.Model.GetTank("tank-b").Value.Id;
            yield return CycleCounter();
            cycleMetrics.preparationSeconds = Time.realtimeSinceStartup - cycleStarted;
            shop.Customers.ArrivalsEnabled = true;
            Check(shop.OpenForDay() && !shop.OpenForDay(), "First natural trading day opens once with two stocked displays");
            float dayOneStarted = Time.realtimeSinceStartup;
            CycleStage("Day 1: serve first customer, then physically restock during trading");
            yield return CycleQueued(40f);
            Check(shop.Customers.Visit.SpecimenId == firstA && shop.Reports.Current.stockedAtOpen == 2,
                "First visitor reserves the naturally prepared Pondlet in display A");
            yield return Capture("41-cycle-first-checkout");
            Check(shop.ServeCustomer() && !shop.ServeCustomer() && shop.Model.Currency == 6 && !shop.Model.GetTank("tank-a").HasValue,
                "First personal checkout pays six exactly once and frees display A");
            yield return CycleTap(Key.Escape);
            Check(shop.Panel == ShopPanel.None, "Escape closes the counter before walking back to the lab");

            yield return CycleProducePondlet();
            Check(shop.Cycle.Phase == ShopDayPhase.Open, "Ordinary daytime lab production leaves the trading day active");
            yield return CycleTransfer(StationKind.TankA, "tank-a");
            string carried = shop.Model.GetTank("tank-a").Value.Id;
            Check(carried != firstA && carried != firstB && shop.Reports.Current.restocked == 1,
                "Real E input restocks the emptied display once with a newly grown specimen");
            yield return Capture("42-cycle-daytime-restock");
            yield return CycleCounter();
            yield return CycleQueued(40f);
            Check(shop.Customers.Visit.SpecimenId == firstB,
                "Second visitor keeps their original display B order while display A is replenished");
            Check(shop.ServeCustomer() && !shop.ServeCustomer() && shop.Model.Currency == 12,
                "Second personal checkout pays six once without selling the replenished specimen");
            Check(shop.CloseForNight() && !shop.CloseForNight(), "Close day one immediately after its second paid checkout");
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 7f, "day-one closing", false);
            cycleMetrics.firstTradingSeconds = Time.realtimeSinceStartup - dayOneStarted;
            cycleMetrics.dayOne = shop.Reports.Last;
            var dayOne = cycleMetrics.dayOne;
            Check(shop.Panel == ShopPanel.DayReport && dayOne.dayNumber == 1 && dayOne.visitors == 2 &&
                dayOne.sales == 2 && dayOne.revenue == 12 && dayOne.stockedAtOpen == 2 &&
                dayOne.restocked == 1 && dayOne.stockedAtClose == 1 && dayOne.closed == 0 &&
                dayOne.interrupted == 0 && !dayOne.partial,
                "Continuous day-one report reconciles two sales, twelve income, one restock and one carried specimen");
            yield return Capture("43-cycle-day-one-report");
            Check(shop.SaveNow(), "Save the first completed continuous day");
            File.Copy(shop.SavePath, Path.Combine(evidence, "cycle-night-two.json"), true);
            yield return CycleTap(Key.Escape);
            Check(shop.Panel == ShopPanel.None, "Real Escape dismisses the completed day-one report");
            Check(shop.ReloadSaved() && shop.Cycle.DayNumber == 2 && shop.Cycle.Phase == ShopDayPhase.Night &&
                shop.Model.Currency == 12 && shop.Model.GetTank("tank-a").Value.Id == carried &&
                shop.Reports.Last.sales == 2 && shop.Reports.Last.revenue == 12,
                "Next preparation night reload retains the earned money, report and exact carried specimen");
            CycleStage("Preparation night 2: retain existing stock and open a second day");
            yield return CycleCounter();
            Check(shop.OpenForDay() && !shop.OpenForDay() && shop.Reports.Current.stockedAtOpen == 1,
                "Second day opens once with the unsold specimen carried from day one");
            float dayTwoStarted = Time.realtimeSinceStartup;
            yield return CycleQueued(40f);
            string interruptedVisitor = shop.Customers.Visit.Id;
            Check(shop.Customers.Visit.SpecimenId == carried, "Day-two queued order owns the carried specimen");
            shop.SetPanel(ShopPanel.Pause);
            float pauseStarted = Time.realtimeSinceStartup;
            float remaining = shop.Cycle.SecondsRemaining;
            float patience = shop.Customers.Visit.QueueSecondsRemaining;
            yield return CycleDelay(0.7f);
            cycleMetrics.pausedSeconds += Time.realtimeSinceStartup - pauseStarted;
            Check(Mathf.Abs(shop.Cycle.SecondsRemaining - remaining) < 0.001f &&
                Mathf.Abs(shop.Customers.Visit.QueueSecondsRemaining - patience) < 0.001f,
                "Pause freezes the real queued customer's patience and day clock");
            Check(shop.SaveNow() && shop.ReloadSaved(), "Pause-menu save and reload reconciles the queued continuous visit");
            Check(shop.Customers.Visit == null && shop.Model.GetTank("tank-a").Value.Id == carried &&
                !shop.Model.GetReservation("tank-a").HasValue && shop.Model.Currency == 12 &&
                shop.Reports.Current.interrupted == 1 && shop.Reports.Current.sales == 0,
                "Reload records one interruption and retains exact unpaid stock without income");
            yield return CycleTap(Key.Escape);
            Check(!shop.Paused && shop.Panel == ShopPanel.None, "Real Escape resumes after the queued-order reload");
            yield return CycleCounter();
            CycleStage("Day 2: allow replacement order to expire in ordinary closing grace");
            yield return CycleQueued(40f);
            Check(shop.Customers.Visit.Id != interruptedVisitor && shop.Customers.Visit.SpecimenId == carried,
                "Replacement customer independently reserves the same unsold specimen");
            Check(shop.CloseForNight() && !shop.CloseForNight(), "Close day two with a valid unpaid replacement order");
            float closingStarted = Time.realtimeSinceStartup;
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 7f, "day-two unpaid closing", false);
            Check(Time.realtimeSinceStartup - closingStarted >= 2.8f, "Unpaid order uses the ordinary three-second closing grace");
            cycleMetrics.secondTradingSeconds = Time.realtimeSinceStartup - dayTwoStarted;
            cycleMetrics.dayTwo = shop.Reports.Last;
            var dayTwo = cycleMetrics.dayTwo;
            Check(shop.Panel == ShopPanel.DayReport && dayTwo.dayNumber == 2 && dayTwo.visitors == 2 &&
                dayTwo.sales == 0 && dayTwo.revenue == 0 && dayTwo.interrupted == 1 && dayTwo.closed == 1 &&
                dayTwo.stockedAtOpen == 1 && dayTwo.stockedAtClose == 1 && dayTwo.restocked == 0 && !dayTwo.partial,
                "Continuous day-two report reconciles one reload interruption and one unpaid closing cancellation");
            Check(shop.Cycle.DayNumber == 3 && shop.Model.Currency == 12 &&
                shop.Model.GetTank("tank-a").Value.Id == carried && !shop.Model.GetTank("tank-b").HasValue &&
                !shop.Model.GetReservation("tank-a").HasValue && shop.Customers.ActiveCount == 0,
                "Two complete natural cycles leave twelve earned credits and the exact unsold Pondlet recoverable");
            yield return Capture("44-cycle-day-two-report");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Reports.Last.dayNumber == 2 &&
                shop.Reports.Last.revenue == 0 && shop.Model.Currency == 12,
                "Second completed report reload cannot replay either day's income");
            File.Copy(shop.SavePath, Path.Combine(evidence, "cycle-night-three.json"), true);
            Check(cycleMetrics.naturalProductions == 3 && cycleMetrics.inputDisplayTransfers == 3 &&
                cycleMetrics.doorwayCrossings >= 6 && cycleMetrics.walkingMetres > 30f,
                "Continuous route includes three naturally timed productions, three E transfers and repeated real doorway walks");
            Check(shop.ScriptedHarvest == null, "Continuous route finished without a scripted harvest override");
            cycleMetrics.completed = true;
            CycleStage("Completed two continuous technical cycles; human pacing acceptance remains separate");
            shop.Customers.ArrivalsEnabled = false;
        }

        IEnumerator CycleProducePondlet()
        {
            yield return CycleHarvest(CultureKind.Bloom);
            yield return CycleHarvest(CultureKind.Flow);
            yield return CycleGoStation(StationKind.Incubator);
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Incubator, "Real E opens the incubator after physically collecting both cultures");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Incubator command doses one collected Bloom");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "Incubator command doses one collected Flow");
            float began = Time.realtimeSinceStartup;
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Incubator command starts ordinary Pondlet production");
            yield return CycleTap(Key.Escape);
            yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16f, "natural incubation", false);
            float elapsed = Time.realtimeSinceStartup - began;
            cycleMetrics.incubationWaitSeconds += elapsed;
            Check(elapsed >= 9.5f && shop.Model.Incubator.SpeciesId == "pondlet",
                "Pondlet matures after ordinary simulation time without an accelerated model delta");
            cycleMetrics.naturalProductions++;
            CycleStage("Naturally produced Pondlet " + cycleMetrics.naturalProductions);
        }

        IEnumerator CycleHarvest(CultureKind kind)
        {
            var stationKind = kind == CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow;
            yield return CycleGoStation(stationKind);
            int before = shop.Model.Available(kind);
            float began = Time.realtimeSinceStartup;
            float deadline = began + 12f;
            while (shop.Model.Available(kind) == before)
            {
                CycleObserve();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Held mouse failed to harvest " + kind + "; focus=" + shop.Focused);
                CycleAim(StationOf(stationKind));
                cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 });
                yield return null;
            }
            InputSystem.QueueStateEvent(cycleMouse, new MouseState());
            yield return null; yield return null;
            cycleMetrics.harvestingSeconds += Time.realtimeSinceStartup - began;
            Check(shop.Model.Available(kind) == before + 1 && shop.ScriptedHarvest == null,
                "Held left mouse and real focus query collect one " + kind + " through the normal siphon");
        }

        IEnumerator CycleTransfer(StationKind station, string tank)
        {
            string specimen = shop.Model.Incubator.SpecimenId;
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && !shop.Model.GetTank(tank).HasValue,
                "Naturally grown output and an empty display are available for transfer");
            yield return CycleGoStation(station);
            yield return CycleTap(Key.E);
            Check(shop.Model.GetTank(tank).HasValue && shop.Model.GetTank(tank).Value.Id == specimen &&
                shop.Model.Incubator.Phase != IncubatorPhase.Ready,
                "Real E transfers the exact naturally grown specimen to " + tank);
            cycleMetrics.inputDisplayTransfers++;
            CycleStage("Physically stocked " + tank);
        }

        IEnumerator CycleCounter()
        {
            yield return CycleGoStation(StationKind.Counter);
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Counter && shop.IsAtCounter,
                "Real movement and E input open the service counter in person");
        }

        IEnumerator CycleGoStation(StationKind kind)
        {
            if (shop.Panel != ShopPanel.None) yield return CycleTap(Key.Escape);
            Check(shop.Panel == ShopPanel.None, "Station travel begins with gameplay controls available");
            bool lab = kind == StationKind.Bloom || kind == StationKind.Flow || kind == StationKind.Incubator;
            if (lab)
            {
                if (shop.Player.transform.position.z < 0.7f)
                {
                    yield return CycleWalkTo(new Vector2(0, -1.2f));
                    yield return CycleWalkTo(new Vector2(0, 2.4f));
                }
                var position = StationOf(kind).transform.position;
                yield return CycleWalkTo(new Vector2(position.x, 2.4f));
            }
            else
            {
                if (shop.Player.transform.position.z > 0.7f)
                {
                    yield return CycleWalkTo(new Vector2(0, 2.4f));
                    yield return CycleWalkTo(new Vector2(0, -1.2f));
                }
                var approach = StationOf(kind).transform.TransformPoint(new Vector3(0, 0, -2.5f));
                yield return CycleWalkTo(new Vector2(approach.x, approach.z));
            }
            shop.AllowValidationInput = false;
            for (int frame = 0; frame < 3; frame++) { CycleAim(StationOf(kind)); yield return null; }
            Check(shop.Focused == StationOf(kind), "Physical approach and scripted aim find " + kind);
        }

        IEnumerator CycleWalkTo(Vector2 destination)
        {
            float began = Time.realtimeSinceStartup;
            float deadline = began + 12f;
            shop.AllowValidationInput = true;
            shop.Player.transform.rotation = Quaternion.identity;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState());
            while (true)
            {
                CycleObserve();
                Vector3 before = shop.Player.transform.position;
                Vector2 delta = destination - new Vector2(before.x, before.z);
                if (delta.magnitude <= 0.14f) break;
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Continuous WASD route blocked: " + before + " -> " + destination);
                var keys = new System.Collections.Generic.List<Key>();
                if (Mathf.Abs(delta.x) > 0.08f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                if (Mathf.Abs(delta.y) > 0.08f) keys.Add(delta.y > 0 ? Key.W : Key.S);
                syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(keys.ToArray()));
                InputSystem.QueueStateEvent(cycleMouse, new MouseState());
                yield return null;
                Vector3 after = shop.Player.transform.position;
                cycleMetrics.walkingMetres += Vector2.Distance(new Vector2(before.x, before.z), new Vector2(after.x, after.z));
                if ((before.z <= 0.5f && after.z > 0.5f) || (before.z >= 0.5f && after.z < 0.5f))
                    cycleMetrics.doorwayCrossings++;
            }
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null;
            shop.AllowValidationInput = false;
            cycleMetrics.walkingSeconds += Time.realtimeSinceStartup - began;
        }

        void CycleAim(Station station)
        {
            shop.Player.View.transform.LookAt(station.transform.TransformPoint(new Vector3(0, 1.1f, 0)));
        }

        IEnumerator CycleTap(Key key)
        {
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null;
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null; yield return null;
        }

        IEnumerator CycleQueued(float timeout)
        {
            yield return CycleUntil(() => shop.Customers.Visit != null && shop.Customers.Visit.Phase == CustomerPhase.Queued,
                timeout, "naturally queued customer", true);
        }

        IEnumerator CycleUntil(Func<bool> condition, float timeout, string reason, bool waitingForCustomer)
        {
            float began = Time.realtimeSinceStartup;
            float deadline = began + timeout;
            while (!condition())
            {
                CycleObserve();
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Continuous route waiting for " + reason + "; customer=" + shop.Customers.Status);
                yield return null;
            }
            if (waitingForCustomer) cycleMetrics.customerWaitSeconds += Time.realtimeSinceStartup - began;
            CycleObserve();
        }

        IEnumerator CycleDelay(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { CycleObserve(); yield return null; }
        }

        void CycleObserve()
        {
            if (tutorialRun) { ObserveTutorialNavigation(); return; }
            if (tradingRun != null) { CheckTradingInvariants(); return; }
            if (Time.realtimeSinceStartup - cycleStarted > 360f) throw new TimeoutException("Continuous scenario exceeded its six-minute watchdog");
            ObserveCustomerNavigation();
        }

        void CycleStage(string stage)
        {
            if (tradingRun != null) { WriteTradingEvidence(); return; }
            cycleMetrics.stage = stage;
            cycleMetrics.totalSeconds = Time.realtimeSinceStartup - cycleStarted;
            cycleMetrics.navigationSamples = customerNavigationSamples;
            cycleMetrics.dayNumber = shop.Cycle.DayNumber;
            cycleMetrics.money = shop.Model.Currency;
            cycleMetrics.displayedStock = shop.DisplayedStockCount();
            cycleMetrics.playerPosition = shop.Player.transform.position;
            File.WriteAllText(Path.Combine(evidence, "cycle-metrics.json"), JsonUtility.ToJson(cycleMetrics, true));
            Debug.Log("CYCLE_STAGE " + stage + " elapsed=" + cycleMetrics.totalSeconds);
        }
    }
}
#endif
