#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable]
        sealed class UiClickStep
        {
            public string stage;
            public string mode;
            public string instruction;
            public string status;
            public string panel;
            public string phase;
            public string customer;
            public int currency;
            public float elapsedSeconds;
        }

        IEnumerator UiClickScenario()
        {
            limitations.Add("Real window clicks are supplied externally by Computer Use and must be evidenced by that operator. This scenario observes UI effects; it does not inject or override input events.");
            limitations.Add("Controlled click probe: one Bloom and one Flow are seeded through Collect, station approach and panels are placed by commands, and the ready specimen is transferred to tank B by command. Further arrivals stop after one visitor. This is not a manual production, movement, pacing, or full-route playtest.");
            Check(shop.ValidationMode && string.Equals(Path.GetDirectoryName(Path.GetFullPath(shop.SavePath)),
                Path.GetFullPath(evidence).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase),
                "UI click probe uses the isolated evidence save directory");
            shop.AllowValidationInput = false;
            shop.Customers.ArrivalsEnabled = false;
            yield return UiClickWait("00-navigation", "automatic", "Waiting for the runtime customer navigation bake.",
                () => shop.Customers.Ready, 15);
            Check(shop.Model.Currency == 0 && shop.Model.ReservedCultureCount == 0 && shop.DisplayedStockCount() == 0 &&
                shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == 1 && shop.Reports.Last == null,
                "UI click probe starts with fresh preparation night and zero currency/stock");

            shop.SetPanel(ShopPanel.Welcome);
            yield return Capture("ui-01-welcome");
            yield return UiClickWait("01-enter-shop", "click", "Click Enter your shop in the visible Welcome panel.",
                () => shop.Panel == ShopPanel.None);
            Check(!shop.Paused, "Welcome dismissal enters active preparation");

            Ok(shop.Model.Collect(CultureKind.Bloom), "UI fixture seeds exactly one free Bloom through Collect");
            Ok(shop.Model.Collect(CultureKind.Flow), "UI fixture seeds exactly one free Flow through Collect");
            shop.World.Refresh(shop.Model);
            PlaceUiClickAt(StationKind.Incubator);
            shop.SetPanel(ShopPanel.Incubator);
            yield return Capture("ui-02-incubator-empty");
            yield return UiClickWait("02-dose-bloom", "click", "Click + Bloom once.",
                () => shop.Model.Staged(CultureKind.Bloom) == 1);
            Check(shop.Model.Available(CultureKind.Bloom) == 0 && shop.Model.Staged(CultureKind.Flow) == 0,
                "Bloom dose button moves exactly one carried culture into the incubator");
            yield return UiClickWait("03-dose-flow", "click", "Click + Flow once.",
                () => shop.Model.Staged(CultureKind.Flow) == 1);
            Check(shop.Model.Available(CultureKind.Flow) == 0 && shop.Model.ReservedCultureCount == 2,
                "Flow dose button completes the exact one-plus-one recipe");
            yield return Capture("ui-03-exact-recipe");
            yield return UiClickWait("04-begin-incubation", "click", "Click Begin incubation once.",
                () => shop.Model.Incubator.Phase == IncubatorPhase.Incubating);
            Check(shop.Panel == ShopPanel.None && shop.Model.Incubator.SpeciesId == "pondlet",
                "Begin incubation button starts Pondlet and closes the station panel");
            float incubationStarted = Time.realtimeSinceStartup;
            yield return UiClickWait("05-incubation", "automatic", "Let the ordinary ten-second incubation finish. No click is needed.",
                () => shop.Model.Incubator.Phase == IncubatorPhase.Ready, shop.Config.IncubationSeconds + 8);
            Check(Time.realtimeSinceStartup - incubationStarted >= shop.Config.IncubationSeconds - .75f,
                "UI probe lets incubation finish on ordinary runtime frames");
            Check(shop.DisplayedStockCount() == 0 && shop.Model.Currency == 0,
                "Ready output remains in the incubator while both displays are empty");
            yield return Capture("ui-04-pondlet-ready");

            PlaceUiClickAt(StationKind.Counter);
            shop.SetPanel(ShopPanel.Counter);
            shop.Customers.ArrivalsEnabled = true;
            yield return Capture("ui-05-empty-counter");
            yield return UiClickWait("06-open-shop", "click", "Click Open for day 1. Keep the counter panel open.",
                () => shop.Cycle.Phase == ShopDayPhase.Open);
            Check(shop.Reports.Current.stockedAtOpen == 0 && shop.Model.Incubator.Phase == IncubatorPhase.Ready,
                "Open button begins a day with zero displayed stock and retained ready output");
            yield return UiClickWait("07-first-arrival", "automatic", "Waiting for the first real visitor; no click is needed.",
                () => shop.Customers.Visit != null, 15);
            shop.Customers.ArrivalsEnabled = false;
            yield return UiClickWait("08-browse-empty-a", "automatic", "The visitor walks to and browses empty tank A.",
                () => shop.Customers.Visit != null && shop.Customers.Visit.TargetTankId == "tank-a" &&
                    shop.Customers.Visit.Phase == CustomerPhase.Browsing, 20);
            Vector3 positionAtA = shop.Customers.Actor.transform.position;
            Check(!shop.Model.GetReservation("tank-a").HasValue && shop.DisplayedStockCount() == 0,
                "Visitor browses empty A before the late-stock fixture transfer");
            Ok(shop.TransferToDisplay("tank-b"), "UI fixture transfers ready Pondlet to B while the visitor browses empty A");
            string specimen = shop.Model.GetTank("tank-b").Value.Id;
            yield return UiClickWait("09-redirect-to-b", "automatic", "Visitor must discover the late stock and walk to tank B before reserving it.",
                () => shop.Customers.Visit != null && shop.Customers.Visit.TargetTankId == "tank-b" &&
                    shop.Customers.Visit.Phase == CustomerPhase.Arriving, 8);
            Check(!shop.Model.GetReservation("tank-b").HasValue,
                "Redirect to newly stocked B does not reserve a creature from across the shop");
            float browseDeadline = Time.realtimeSinceStartup + 12;
            while (shop.Customers.Visit != null && shop.Customers.Visit.Phase == CustomerPhase.Arriving)
            {
                if (Time.realtimeSinceStartup > browseDeadline) throw new TimeoutException("Redirected customer did not reach tank B.");
                if (shop.Model.GetReservation("tank-b").HasValue) throw new InvalidOperationException("Customer reserved B before physically reaching it.");
                ObserveCustomerNavigation();
                yield return null;
            }
            Check(shop.Customers.Visit != null && shop.Customers.Visit.Phase == CustomerPhase.Browsing &&
                shop.Customers.Visit.TargetTankId == "tank-b" &&
                Vector3.Distance(positionAtA, shop.Customers.Actor.transform.position) > 1f &&
                Vector3.Distance(shop.Customers.Actor.transform.position, shop.Customers.Navigation.BrowsePoint("tank-b")) < .4f,
                "Redirected visitor actually walks from A to the browse point at B");
            WriteUiClickStep("10-browse-stocked-b", "automatic", "Visitor browses B locally; reservation must wait until browsing finishes.", "waiting");
            float localBrowseStarted = Time.realtimeSinceStartup;
            while (shop.Customers.Visit != null && shop.Customers.Visit.Phase == CustomerPhase.Browsing)
            {
                if (Time.realtimeSinceStartup - localBrowseStarted > 8) throw new TimeoutException("Visitor browsing B did not complete.");
                if (shop.Model.GetReservation("tank-b").HasValue) throw new InvalidOperationException("Visitor reserved B before its local browse completed.");
                ObserveCustomerNavigation();
                yield return null;
            }
            Check(shop.Customers.Visit != null && shop.Customers.Visit.Phase == CustomerPhase.ToCounter &&
                shop.Model.GetReservation("tank-b").HasValue && shop.Customers.Visit.SpecimenId == specimen,
                "Only completed local browsing reserves the exact late-stocked Pondlet");
            Check(Time.realtimeSinceStartup - localBrowseStarted >= new CustomerRules().BrowseSeconds - .5f,
                "The redirected visitor spends a fresh local browse interval at stocked B");
            yield return UiClickWait("11-walk-to-checkout", "automatic", "Visitor is walking to the counter; keep the counter panel open.",
                () => shop.Customers.Visit != null && shop.Customers.Visit.Phase == CustomerPhase.Queued, 20);
            yield return Capture("ui-06-real-customer-order");
            yield return UiClickWait("12-serve-customer", "click", "Click Serve customer / +6 credits before the visible patience timer expires.",
                () => shop.Model.Currency == 6);
            Check(shop.DisplayedStockCount() == 0 && shop.Reports.Current.sales == 1 && shop.Reports.Current.revenue == 6 &&
                shop.Customers.Visit != null && shop.Customers.Visit.Outcome == CustomerOutcome.Sold,
                "Actual checkout UI effect removes one specimen and records exactly six credits once");
            yield return Capture("ui-07-sale-complete");

            yield return UiClickWait("13-request-close", "click", "Click Close for the night... in the counter panel.",
                () => shop.Panel == ShopPanel.CloseShop);
            yield return Capture("ui-08-confirm-closing");
            yield return UiClickWait("14-confirm-close", "click", "Click Close today's shop.",
                () => shop.Cycle.Phase == ShopDayPhase.Closing);
            yield return UiClickWait("15-closing", "automatic", "Let ordinary closing finish and the daily report open.",
                () => shop.Cycle.Phase == ShopDayPhase.Night && shop.Panel == ShopPanel.DayReport, 10);
            var report = shop.Reports.Last;
            Check(report != null && report.dayNumber == 1 && report.visitors == 1 && report.sales == 1 && report.revenue == 6 &&
                report.stockedAtOpen == 0 && report.restocked == 1 && report.stockedAtClose == 0 && shop.Model.Currency == 6,
                "Click-driven trading report reconciles one late restock, one visitor, one sale and six income");
            Check(!shop.Paused && shop.Cycle.DayNumber == 2, "Report remains an ordinary panel on preparation night two");
            yield return Capture("ui-09-day-report");
            yield return UiClickWait("16-continue-preparation", "click", "Click Continue preparation in the report footer.",
                () => shop.Panel == ShopPanel.None);

            shop.SetPanel(ShopPanel.Pause);
            yield return Capture("ui-10-save-and-resume");
            yield return UiClickWait("17-save-resume", "click", "Click Save and resume in the pause panel.",
                () => shop.Panel == ShopPanel.None);
            Check(SaveStore.Read(shop.SavePath, shop.Config, out var saved, out var slot, out var error) &&
                saved.Currency == 6 && slot.trades.last != null && slot.trades.last.sales == 1 && slot.trades.last.revenue == 6,
                "Save and resume leaves a readable isolated slot with exactly one paid sale / " + error);
            var beforeReload = shop.Model;
            shop.SetPanel(ShopPanel.Pause);
            yield return UiClickWait("18-reload-save", "click", "Click Reload local save in the pause panel.",
                () => !ReferenceEquals(beforeReload, shop.Model) && shop.Panel == ShopPanel.None);
            Check(shop.Model.Currency == 6 && shop.DisplayedStockCount() == 0 && shop.Reports.Last.sales == 1 &&
                shop.Reports.Last.revenue == 6 && shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == 2,
                "Reload UI effect preserves the completed report without duplicating stock or payment");
            yield return Capture("ui-11-restored-result");
            WriteUiClickStep("19-complete", "complete", "Click probe completed. Read smoke-result.json and the external Computer Use evidence.", "completed");
        }

        void PlaceUiClickAt(StationKind kind)
        {
            var station = StationOf(kind).transform;
            shop.Player.Teleport(station.TransformPoint(new Vector3(0, .2f, -2.5f)), station.rotation);
            shop.Player.View.transform.LookAt(station.TransformPoint(new Vector3(0, 1.1f, 0)));
        }

        IEnumerator UiClickWait(string stage, string mode, string instruction, Func<bool> condition, float timeout = 120)
        {
            WriteUiClickStep(stage, mode, instruction, "waiting");
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    WriteUiClickStep(stage, mode, instruction, "timed out");
                    throw new TimeoutException("UI probe " + stage + ": " + instruction + "; panel=" + shop.Panel +
                        "; customer=" + shop.Customers.Status + "; save=" + shop.SaveError);
                }
                yield return null;
            }
            Check(true, "Observed UI probe step " + stage);
            WriteUiClickStep(stage, mode, instruction, "completed");
        }

        void WriteUiClickStep(string stage, string mode, string instruction, string status)
        {
            var step = new UiClickStep
            {
                stage = stage, mode = mode, instruction = instruction, status = status,
                panel = shop.Panel.ToString(), phase = shop.Cycle.Phase.ToString(), customer = shop.Customers.Status,
                currency = shop.Model.Currency, elapsedSeconds = Time.realtimeSinceStartup - start
            };
            File.WriteAllText(Path.Combine(evidence, "ui-step.json"), JsonUtility.ToJson(step, true));
            Debug.Log("UI_CLICK_STEP " + stage + " / " + mode + " / " + status + " / " + instruction);
        }
    }
}
#endif
