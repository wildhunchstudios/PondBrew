#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class ShiftVisitEvidence
        {
            public string id, type, outcome, species, specimen;
            public int day, reputationAtFirstObservation, budget, agreedPrice, rejectedTags;
            public float arrivedAt, queuedAt = -1, resolvedAt = -1;
        }
        [Serializable] sealed class TradingShiftEvidence
        {
            public int day, openingCash, closingCash, openingReputation, closingReputation, peakVisitors;
            public int naturalBatchesStarted, transfers, duplicatePaymentRejections;
            public float openedAt, completedAt, tradingSeconds, closingSeconds;
            public float walkingSeconds, harvestingSeconds, incubationBusySeconds, outputBlockedSeconds;
            public float emptyDisplaySeconds, bothEmptySeconds, queueCustomerSeconds, waitingSeconds;
            public bool upgradeOwned, completed, ordinaryFullShift;
            public string pricePolicy;
            public TradeDaySave report;
        }
        [Serializable] sealed class TradingRunEvidence
        {
            public string scope = "R03.5 automated operator; natural default traffic and three consecutive full clocks. Not human pacing or an isolated economic A/B.";
            public string stage, activity;
            public bool setupProbe, completed;
            public float elapsedSeconds, ordinaryTradingSeconds = ShopDayCycle.DefaultTradingSeconds;
            public int currentDay, cash, reputation, activeVisitors;
            public float remaining;
            public List<TradingShiftEvidence> shifts = new List<TradingShiftEvidence>();
            public List<ShiftVisitEvidence> visits = new List<ShiftVisitEvidence>();
        }
        TradingRunEvidence tradingRun;
        TradingShiftEvidence tradingShift;
        readonly Dictionary<string, CustomerVisit> observedTradingVisits = new Dictionary<string, CustomerVisit>();
        readonly Dictionary<string, ShiftVisitEvidence> tradingVisitEvidence = new Dictionary<string, ShiftVisitEvidence>();
        readonly HashSet<string> tradingPaidSpecimens = new HashSet<string>();
        float tradingStarted, nextTradingWrite, nextTradingVisitSample;
        bool tradingProbe, tradingRepriced;

        // Only the explicitly selected development scenario enables this observer.
        void Update()
        {
            if (tradingRun == null || tradingRun.completed || shop == null) return;
            float dt = Time.deltaTime;
            if (tradingShift != null && !tradingShift.completed && !shop.Paused)
            {
                if (shop.Cycle.Phase == ShopDayPhase.Open) tradingShift.tradingSeconds += dt;
                else if (shop.Cycle.Phase == ShopDayPhase.Closing) tradingShift.closingSeconds += dt;
                int stock = shop.DisplayedStockCount();
                tradingShift.emptyDisplaySeconds += (2 - stock) * dt;
                if (stock == 0) tradingShift.bothEmptySeconds += dt;
                tradingShift.queueCustomerSeconds += shop.Customers.WaitingCount * dt;
                tradingShift.peakVisitors = Mathf.Max(tradingShift.peakVisitors, shop.Customers.ActiveCount);
                if (shop.Model.Incubator.Phase == IncubatorPhase.Incubating) tradingShift.incubationBusySeconds += dt;
                if (shop.Model.Incubator.Phase == IncubatorPhase.Ready && stock == 2) tradingShift.outputBlockedSeconds += dt;
                if (tradingRun.activity == "waiting") tradingShift.waitingSeconds += dt;
            }
            if (Time.realtimeSinceStartup >= nextTradingVisitSample)
            {
                nextTradingVisitSample = Time.realtimeSinceStartup + .2f;
                ObserveTradingVisits();
            }
            if (Time.realtimeSinceStartup >= nextTradingWrite)
            {
                nextTradingWrite = Time.realtimeSinceStartup + 5f;
                WriteTradingEvidence();
            }
        }

        void ObserveTradingVisits()
        {
            foreach (var visit in shop.Customers.Visits)
            {
                if (observedTradingVisits.ContainsKey(visit.Id)) continue;
                observedTradingVisits.Add(visit.Id, visit);
                var observation = new ShiftVisitEvidence { id = visit.Id, day = shop.Cycle.DayNumber,
                    type = visit.ArchetypeId, budget = visit.Budget, reputationAtFirstObservation = shop.Reputation.Points,
                    arrivedAt = Time.realtimeSinceStartup - tradingStarted };
                tradingVisitEvidence.Add(visit.Id, observation); tradingRun.visits.Add(observation);
            }
            foreach (var pair in observedTradingVisits)
            {
                var visit = pair.Value; var observation = tradingVisitEvidence[pair.Key];
                observation.rejectedTags = visit.PriceRejections;
                if (visit.Phase == CustomerPhase.Queued && observation.queuedAt < 0)
                    observation.queuedAt = Time.realtimeSinceStartup - tradingStarted;
                if (visit.SpecimenId != null)
                { observation.specimen = visit.SpecimenId; observation.species = visit.SpeciesId; observation.agreedPrice = visit.Price; }
                if (visit.Outcome != CustomerOutcome.None && observation.resolvedAt < 0)
                { observation.outcome = visit.Outcome.ToString(); observation.resolvedAt = Time.realtimeSinceStartup - tradingStarted; }
            }
        }

        void WriteTradingEvidence()
        {
            tradingRun.elapsedSeconds = Time.realtimeSinceStartup - tradingStarted;
            tradingRun.currentDay = shop.Cycle.DayNumber; tradingRun.cash = shop.Model.Currency;
            tradingRun.reputation = shop.Reputation.Points; tradingRun.activeVisitors = shop.Customers.ActiveCount;
            tradingRun.remaining = shop.Cycle.SecondsRemaining;
            File.WriteAllText(Path.Combine(evidence, "trading-shifts.json"), JsonUtility.ToJson(tradingRun, true));
        }

        IEnumerator TradingShiftScenario()
        {
            tradingStarted = cycleStarted = Time.realtimeSinceStartup;
            tradingProbe = ShopSession.Arg("-evolutionTradingProbe") != null;
            tradingRun = new TradingRunEvidence { setupProbe = tradingProbe, stage = "Initialize" };
            cycleMetrics = new ContinuousMetrics();
            limitations.Add("Automated operator uses actual WASD/held-mouse/E, scripted aim and UI application commands. No physical keyboard/mouse or enjoyment claim. Ordinary runs retain natural unforced customers, current reputation effects, full default clocks and real production. Per-visit observations sample at0.2s; ledger reports are authoritative.");
            limitations.Add(tradingProbe ? "SETUP PROBE ONLY: closes after two sales; counts as zero ordinary shifts." :
                "Three consecutive shifts use an improving shop, purchased upgrade and a third-day price change; these confound causal economy comparisons. Do not infer a controlled A/B or tune solely from total income.");
            Check(shop.ValidationMode && shop.SavePath == Path.Combine(evidence, "smoke-save.json"), "Trading route uses isolated save");
            Check(shop.Model.Currency == 0 && shop.Reputation.Points == 0 && shop.DisplayedStockCount() == 0 &&
                shop.Model.DiscoveredSpeciesIds.Count == 0 && shop.Cycle.DayNumber == 1, "Fresh trading operator starts with zero funds and no injected progression");
            Check(Time.timeScale == 1 && shop.Customers.MaxVisitors == 2 && !shop.Customers.UseBaseReputationEffectsForValidation &&
                shop.Customers.ForcedArchetypeId == null && shop.Customers.ScriptedArchetypes.Count == 0,
                "Unaccelerated clock, normal two-visitor cap and unforced reputation-aware demand");
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTradingKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTradingMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.SetPanel(ShopPanel.None);
            shop.Customers.ArrivalsEnabled = true;
            yield return CycleUntil(() => shop.Customers.Ready, 10, "trading navigation startup", false);
            Check(!shop.Model.BuyUpgrade().Success && shop.Model.Currency == 0 && !shop.Model.UpgradeOwned,
                "Zero funds cannot buy the upgrade or prevent free production");
            int days = tradingProbe ? 1 : 3;
            for (int day = 1; day <= days; day++)
            {
                tradingRun.stage = "Preparation night " + day;
                if (day == 2) yield return TradingBuyUpgrade();
                yield return TradingPrepare(day);
                yield return CycleCounter();
                float walkBefore = cycleMetrics.walkingSeconds, harvestBefore = cycleMetrics.harvestingSeconds;
                tradingShift = new TradingShiftEvidence { day = day, openingCash = shop.Model.Currency,
                    openingReputation = shop.Reputation.Points, openedAt = Time.realtimeSinceStartup - tradingStarted,
                    upgradeOwned = shop.Model.UpgradeOwned, pricePolicy = day < 3 ? "Pondlet6; advanced10" : "Pondlet6; advanced10; first safely queued advanced species repriced to12" };
                tradingRun.shifts.Add(tradingShift);
                Check(shop.OpenForDay() && !shop.OpenForDay() && shop.Cycle.SecondsRemaining > 479f,
                    "Day " + day + " opens exactly once at the ordinary480-second duration");
                tradingRun.stage = "Natural trading day " + day;
                int batches = 0, advancedBatches = 0;
                while (shop.Cycle.Phase != ShopDayPhase.Night)
                {
                    if (errors.Count != 0) throw new InvalidOperationException("Runtime error during ordinary trading; inspect player log");
                    CheckTradingInvariants();
                    if (shop.Customers.WaitingCount > 0)
                    {
                        if (day == 3 && !tradingRepriced && shop.Cycle.Phase == ShopDayPhase.Open)
                            yield return TradingRepriceQueuedAdvanced();
                        yield return TradingServe();
                    }
                    else if (tradingProbe && shop.Reports.Current.sales >= 2)
                    {
                        Check(shop.CloseForNight(), "Short setup probe closes explicitly; excluded from full-shift evidence");
                        yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8, "probe closing", false);
                    }
                    else if (shop.Cycle.Phase == ShopDayPhase.Closing || shop.Cycle.SecondsRemaining < 30)
                    {
                        // Deliberate operator policy: finish service instead of starting a final lab trip.
                        if (!shop.IsAtCounter) yield return CycleCounter();
                        tradingRun.activity = "waiting"; yield return null;
                    }
                    else if (shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.DisplayedStockCount() < 2)
                    {
                        yield return TradingTransfer();
                    }
                    else if (shop.Model.Incubator.Phase == IncubatorPhase.Dosing)
                    {
                        // Alternate an affordable species with the two advanced recipes, across all shifts.
                        string species = batches++ % 2 == 0 ? "pondlet" :
                            (advancedBatches++ % 2 == 0 ? "bubblebelly" : "ribbonfin");
                        yield return TradingStartBatch(species);
                    }
                    else
                    {
                        if (!shop.IsAtCounter) yield return CycleCounter();
                        tradingRun.activity = "waiting"; yield return null;
                    }
                }
                tradingShift.walkingSeconds = cycleMetrics.walkingSeconds - walkBefore;
                tradingShift.harvestingSeconds = cycleMetrics.harvestingSeconds - harvestBefore;
                tradingShift.completedAt = Time.realtimeSinceStartup - tradingStarted;
                tradingShift.closingCash = shop.Model.Currency; tradingShift.closingReputation = shop.Reputation.Points;
                tradingShift.report = shop.Reports.Last; tradingShift.completed = true;
                tradingShift.ordinaryFullShift = !tradingProbe;
                var report = tradingShift.report;
                int departures = report.tooExpensive + report.noStock + report.impatient + report.closed + report.interrupted + report.pathProblems + report.slapped;
                Check(report.dayNumber == day && report.visitors == report.sales + departures &&
                    report.stockedAtOpen + report.restocked - report.sales == report.stockedAtClose &&
                    tradingShift.closingCash - tradingShift.openingCash == report.revenue,
                    "Day " + day + " reconciles every visitor, specimen and credit");
                Check(report.sales >= 2 && report.pathProblems == 0 && report.interrupted == 0 &&
                    shop.Customers.ActiveCount == 0 && !shop.Model.GetReservation("tank-a").HasValue && !shop.Model.GetReservation("tank-b").HasValue,
                    "Day " + day + " recovers stock and visitors at natural close without broken routes or lost orders");
                if (!tradingProbe) Check(tradingShift.completedAt - tradingShift.openedAt >= 482f &&
                    tradingShift.tradingSeconds >= 479f && tradingShift.peakVisitors == 2,
                    "Day " + day + " actually ran a full unaccelerated shift with two concurrent visitors");
                shop.ShowDayReport();
                yield return Capture("83-trading-day-" + day + "-report");
                ObserveTradingVisits(); WriteTradingEvidence();
                Check(shop.SaveNow(), "Save completed trading day " + day);
                File.Copy(shop.SavePath, Path.Combine(evidence, "trading-night-" + (day + 1) + ".json"), false);
                int cash = shop.Model.Currency, rep = shop.Reputation.Points;
                Check(shop.ReloadSaved() && shop.Cycle.DayNumber == day + 1 && shop.Model.Currency == cash &&
                    shop.Reputation.Points == rep && shop.Reports.Last.revenue == report.revenue,
                    "Day " + day + " reload retains progression and cannot replay payments");
                yield return CycleTap(UnityEngine.InputSystem.Key.Escape);
            }
            if (!tradingProbe) Check(shop.Model.UpgradeOwned && tradingRepriced && shop.Cycle.DayNumber == 4,
                "Three full shifts include earned upgrade and a queued reprice without resets or forced arrivals");
            tradingRun.completed = true; tradingRun.stage = tradingProbe ? "Setup probe complete; no ordinary shifts" : "Three ordinary shifts complete";
            WriteTradingEvidence();
        }

        void CheckTradingInvariants()
        {
            if (Time.timeScale != 1 || shop.Paused || shop.Customers.MaxVisitors != 2 || shop.Customers.UseBaseReputationEffectsForValidation ||
                shop.Customers.ForcedArchetypeId != null || shop.Customers.ScriptedArchetypes.Count != 0 || !shop.Customers.ArrivalsEnabled ||
                shop.Customers.ActiveCount > 2 || shop.ScriptedHarvest != null)
                throw new InvalidOperationException("Ordinary trading settings changed or visitor cap exceeded");
            if (Time.realtimeSinceStartup - tradingStarted > 2100) throw new TimeoutException("Trading route exceeded35-minute watchdog");
            foreach (var visit in shop.Customers.Visits)
            {
                var actor = shop.Customers.ActorOf(visit);
                if (actor && !shop.Customers.Navigation.IsInStore(actor.transform.position))
                    throw new InvalidOperationException("Natural visitor left the store navigation bounds");
                customerNavigationSamples++;
            }
        }

        IEnumerator TradingPrepare(int day)
        {
            while (shop.DisplayedStockCount() < 2)
            {
                if (shop.Model.Incubator.Phase == IncubatorPhase.Dosing)
                    yield return TradingStartBatch(!shop.Model.GetTank("tank-a").HasValue ? "pondlet" : day % 2 == 0 ? "ribbonfin" : "bubblebelly");
                if (shop.Model.Incubator.Phase == IncubatorPhase.Incubating)
                { tradingRun.activity = "waiting"; yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "ordinary preparation incubation", false); }
                yield return TradingTransfer();
            }
            Check(shop.DisplayedStockCount() == 2 && shop.Cycle.Phase == ShopDayPhase.Night, "Night " + day + " prepares two displays using ordinary production");
        }

        IEnumerator TradingStartBatch(string speciesId)
        {
            var species = shop.Model.GetSpecies(speciesId);
            tradingRun.activity = "production";
            for (int i = shop.Model.Available(CultureKind.Bloom); i < species.Bloom; i++) yield return CycleHarvest(CultureKind.Bloom);
            for (int i = shop.Model.Available(CultureKind.Flow); i < species.Flow; i++) yield return CycleHarvest(CultureKind.Flow);
            yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Incubator, "Physical E opens incubator for " + speciesId);
            for (int i = 0; i < species.Bloom; i++) Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Dose harvested Bloom");
            for (int i = 0; i < species.Flow; i++) Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "Dose harvested Flow");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Start ordinary ten-second " + speciesId + " batch");
            if (tradingShift != null && !tradingShift.completed) tradingShift.naturalBatchesStarted++;
            yield return CycleTap(Key.Escape);
        }

        IEnumerator TradingTransfer()
        {
            string species = shop.Model.Incubator.SpeciesId;
            bool useA = species == "pondlet" ? !shop.Model.GetTank("tank-a").HasValue : shop.Model.GetTank("tank-b").HasValue;
            if (shop.Model.GetTank(useA ? "tank-a" : "tank-b").HasValue) useA = !useA;
            tradingRun.activity = "restocking";
            yield return CycleTransfer(useA ? StationKind.TankA : StationKind.TankB, useA ? "tank-a" : "tank-b");
            if (tradingShift != null && !tradingShift.completed) tradingShift.transfers++;
        }

        IEnumerator TradingServe()
        {
            tradingRun.activity = "serving";
            if (shop.Panel != ShopPanel.Counter || !shop.IsAtCounter) yield return CycleCounter();
            while (shop.Customers.WaitingCount > 0 && shop.Cycle.Phase != ShopDayPhase.Night)
            {
                var visit = shop.Customers.NextToServe;
                int price = visit.Price, cash = shop.Model.Currency; long revenue = shop.Reports.Current.revenue;
                string specimen = visit.SpecimenId;
                Check(shop.ServeCustomer() && shop.Model.Currency == cash + price && shop.Reports.Current.revenue == revenue + price &&
                    tradingPaidSpecimens.Add(specimen), "Natural customer pays exact snapshotted price once for a unique specimen");
                Check(!visit.Serve(shop.Cycle.Phase).Success && shop.Model.Currency == cash + price,
                    "Repeated payment on the completed visit cannot duplicate cash");
                tradingShift.duplicatePaymentRejections++;
                yield return null;
            }
        }

        IEnumerator TradingRepriceQueuedAdvanced()
        {
            CustomerVisit selected = null;
            foreach (var visit in shop.Customers.Visits)
                if (visit.Phase == CustomerPhase.Queued && visit.SpeciesId != "pondlet" && visit.QueueSecondsRemaining > 20 && shop.Cycle.SecondsRemaining > 45) { selected = visit; break; }
            if (selected == null) yield break;
            string tank = selected.TargetTankId; int agreed = selected.Price;
            tradingRun.activity = "repricing";
            yield return CycleGoStation(tank == "tank-a" ? StationKind.TankA : StationKind.TankB);
            var reservation = shop.Model.GetReservation(tank);
            if (selected.Phase != CustomerPhase.Queued || !reservation.HasValue || reservation.Value.CustomerId != selected.Id)
                yield break; // Ordinary impatience is an outcome, not a reason to force the visitor to wait.
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Tank, "Physical display tag opens while an advanced order is queued");
            Ok(shop.SetSpeciesPrice(selected.SpeciesId, 12), "Player raises this displayed advanced species tag to12");
            Check(selected.Outcome == CustomerOutcome.None && selected.Price == agreed &&
                shop.Model.GetReservation(tank).Value.Price == agreed, "Existing advanced order retains its original price during live repricing");
            tradingRepriced = true;
            yield return Capture("82-trading-repriced-tag");
            yield return CycleTap(Key.Escape);
        }

        IEnumerator TradingBuyUpgrade()
        {
            Check(shop.Model.Currency >= shop.Config.UpgradeCost, "Day-one ordinary sales earned the upgrade price");
            tradingRun.activity = "upgrade";
            if (shop.Panel != ShopPanel.None) yield return CycleTap(Key.Escape);
            yield return CycleWalkTo(new Vector2(0, -1.2f)); yield return CycleWalkTo(new Vector2(0, 1.9f));
            yield return CycleWalkTo(new Vector2(-3f, 1.9f));
            for (int i = 0; i < 3; i++) { CycleAim(StationOf(StationKind.Upgrade)); yield return null; }
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Upgrade, "Physical doorway route reaches lab upgrade bench");
            int before = shop.Model.Currency;
            Ok(shop.Perform(shop.Model.BuyUpgrade()), "Buy upgrade only with day-one earned credits");
            Check(shop.Model.Currency == before - shop.Config.UpgradeCost && shop.Model.UpgradeOwned, "Upgrade deducts exactly its displayed cost");
            yield return CycleTap(Key.Escape);
        }
    }
}
#endif
