#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator ReputationScenario()
        {
            limitations.Add("Reputation boundary fixtures seed points through the isolated save; stock uses accelerated recipe operations, selected impatience uses an injected delta. Real navigation, tier-aware natural arrivals, checkout and E input execute; commands drive GUI actions. No human balance, audio or native mouse usability claim.");
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionReputationSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Reputation scenario uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            var customers = shop.Customers;
            Check(customers.Ready && customers.MaxVisitors == 2 && !customers.UseBaseReputationEffectsForValidation,
                "Real reputation effects and the normal two-visitor capacity are enabled");
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionReputationKeyboard");
            syntheticKeyboard.MakeCurrent();
            Check(shop.Reputation.Points == 0 && shop.Reputation.Tier == 0 && customers.NextArrivalSeconds == 6,
                "Fresh shop starts at zero reputation and six-second spacing");

            // Cross a tier through real checkout while another visitor holds an old agreement.
            SetReputationFixture(8);
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            Ok(shop.SetSpeciesPrice("bubblebelly", 8), "Fixture Bubblebelly fits the base bargain budget");
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open reputation boundary day");
            var casual = Spawn(CustomerArchetypes.Casual);
            yield return Hold(1);
            var bargain = Spawn(CustomerArchetypes.Bargain);
            yield return WaitFor(() => casual.Phase == CustomerPhase.Queued && bargain.Phase == CustomerPhase.Queued, 35, "two base-tier agreements");
            Check(bargain.Budget == 8 && bargain.Price == 8 && bargain.TargetTankId == "tank-b", "Waiting bargain customer snapshots its eight-credit budget and price");
            yield return ServeNext("Tier-crossing casual sale");
            Check(shop.Reputation.Points == 10 && shop.Reputation.Tier == 1 && customers.NextArrivalSeconds == 5,
                "One paid sale grants two points immediately and unlocks Getting known");
            Check(bargain.Budget == 8 && bargain.Price == 8 && bargain.Archetype.TolerancePercent == 110,
                "Tier increase leaves the existing waiting customer's budget, tolerance and agreement unchanged");
            Check(!shop.Reputation.ResolveVisit(shop.Reports, casual.Id, CustomerOutcome.Sold, casual.Price) && shop.Reputation.Points == 10,
                "Repeated terminal callback cannot reward the same paid visitor twice");
            yield return Capture("60-reputation-tier-counter");
            yield return ServeNext("Original bargain agreement");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "paid visitors leaving");
            Check(shop.Reputation.Points == 12, "Departure callbacks do not repeat either sale reward");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.ReloadSaved() && shop.Reputation.Points == 12 && shop.Model.Currency == 14,
                "Paid reputation and exact income survive repeated reload without replay");

            // A new arrival receives the improved budget, and the price preview matches it.
            SeedStock("bubblebelly", "tank-b");
            Ok(shop.SetSpeciesPrice("bubblebelly", 10), "Return Bubblebelly tag to its standard ten credits");
            var effectiveBargain = CustomerArchetypes.Find(customers.Archetypes, CustomerArchetypes.Bargain);
            Check(effectiveBargain.Budget == 10 && effectiveBargain.TolerancePercent == 120 &&
                customers.Preview(effectiveBargain, shop.Model.GetPrice("bubblebelly").Value) != PriceReaction.TooExpensive,
                "Tier-one price preview includes the two-credit budget and ten-point tolerance bonus");
            yield return PressEAt(StationKind.TankB);
            yield return Capture("61-reputation-price-tag");
            shop.SetPanel(ShopPanel.None);
            var richer = Spawn(CustomerArchetypes.Bargain);
            yield return WaitFor(() => richer.Phase == CustomerPhase.Queued, 30, "improved-budget customer reaching checkout");
            Check(richer.Budget == 10 && richer.Price == 10, "New bargain visitor really agrees the price the reputation-aware preview accepts");
            yield return ServeNext("Improved-budget sale");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "improved-budget customer leaving");
            Check(shop.Reputation.Points == 14, "New tier does not multiply sale reputation or money");

            SeedStock("pondlet", "tank-a");
            Ok(shop.SetSpeciesPrice("pondlet", 18), "Set an excessive Pondlet tag");
            var refused = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => refused.Outcome == CustomerOutcome.TooExpensive, 25, "terminal price refusal");
            Check(shop.Reputation.Points == 13 && shop.Model.GetTank("tank-a").HasValue, "Terminal price refusal subtracts one and preserves stock");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "refusing visitor leaving");
            Check(shop.Reputation.Points == 13, "Refusal departure does not charge a second point");
            Ok(shop.SetSpeciesPrice("pondlet", 6), "Restore affordable Pondlet tag");
            var impatient = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => impatient.Phase == CustomerPhase.Queued, 30, "patience test queued");
            impatient.Advance(60, true, ShopDayPhase.Open);
            customers.Tick(.01f);
            Check(impatient.Outcome == CustomerOutcome.Impatient && shop.Reputation.Points == 12 && !shop.Model.GetReservation("tank-a").HasValue,
                "One accelerated patience expiry loses exactly one point and releases the specimen");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "impatient visitor leaving");

            var interrupted = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => interrupted.Phase == CustomerPhase.Queued, 30, "reload test queued");
            string retained = shop.Model.GetTank("tank-a").Value.Id;
            Check(shop.SaveNow() && shop.ReloadSaved() && customers.ActiveCount == 0 && shop.Reputation.Points == 12 &&
                shop.Model.GetTank("tank-a").Value.Id == retained && !shop.Model.GetReservation("tank-a").HasValue,
                "Unpaid reload cancellation is neutral, releases claims and keeps the exact specimen");
            var broken = Spawn(CustomerArchetypes.Casual);
            broken.Cancel(CustomerOutcome.PathBlocked); customers.Tick(.01f);
            Check(shop.Reputation.Points == 12 && shop.Reports.Current.pathProblems == 1, "Technical path failure is reported without a reputation penalty");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "technical-failure visitor leaving");

            // Top tier: actual timed weighted arrivals, observed room capacity and neutral closing.
            SetReputationFixture(30);
            SeedStock("bubblebelly", "tank-b");
            var types = customers.Archetypes;
            Check(shop.Reputation.TierName == "Local favorite" && customers.NextArrivalSeconds == 4 &&
                types[0].Weight == 30 && types[1].Weight == 20 && types[2].Weight == 25 && types[3].Weight == 25 &&
                types[0].TolerancePercent == 170 && types[1].Budget == 12,
                "Top tier has capped four-second spacing, 30/20/25/25 weights, twenty-point tolerance and four-credit budget bonus");
            int visitorIndex = shop.Reports.Current.visitors;
            var expectedFirst = CustomerArchetypes.Pick(types, shop.Cycle.DayNumber, visitorIndex).Id;
            var expectedSecond = CustomerArchetypes.Pick(types, shop.Cycle.DayNumber, visitorIndex + 1).Id;
            customers.ArrivalsEnabled = true;
            yield return WaitFor(() => customers.ActiveCount == 1, 4, "first natural top-tier arrival");
            float firstTime = Time.realtimeSinceStartup;
            Check(customers.Visits[0].ArchetypeId == expectedFirst, "First natural arrival uses reputation-adjusted deterministic weights");
            yield return WaitFor(() => customers.ActiveCount == 2, 6, "second natural top-tier arrival");
            float spacing = Time.realtimeSinceStartup - firstTime;
            Check(spacing >= 3.7f && spacing <= 4.5f && customers.Visits[1].ArchetypeId == expectedSecond,
                "Second natural weighted arrival follows the four-second tier interval; observed " + spacing.ToString("0.000") + " seconds");
            yield return Hold(5);
            Check(customers.ActiveCount == 2 && shop.Reports.Current.visitors == visitorIndex + 2 && !customers.SpawnNow(),
                "Natural timer and explicit arrival both respect the two-visitor cap under top-tier demand");
            customers.ArrivalsEnabled = false;
            yield return WaitFor(() => customers.WaitingCount == 2, 25, "top-tier customers queued");
            yield return EnterCounter();
            yield return Capture("62-reputation-top-tier-queue");
            Check(shop.CloseForNight(), "Close with two unpaid top-tier orders");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night && customers.ActiveCount == 0, 20, "neutral close and departure");
            Check(shop.Reputation.Points == 30 && shop.Reports.Last.closed == 2, "Closing unpaid orders is neutral and counted once each");
            yield return Capture("63-reputation-day-report");
            shop.ShowDayReport();
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Reputation.Points == 30, "Report reopen and night reload do not score the completed day again");
            shop.SetPanel(ShopPanel.Journal); shop.ScrollPanel(10000);
            yield return Capture("64-reputation-journal");
            shop.SetPanel(ShopPanel.None);

            // Existing saves receive no retroactive reward, even when a report records sales.
            string legacy = "{\"formatVersion\":5,\"game\":" + JsonUtility.ToJson(shop.Model.CaptureSave()) +
                ",\"cycle\":" + JsonUtility.ToJson(shop.Cycle.CaptureSave()) + ",\"trades\":" + JsonUtility.ToJson(shop.Reports.CaptureSave()) +
                ",\"sensitivity\":1,\"volume\":0.65}";
            File.WriteAllText(shop.SavePath, legacy);
            int cash = shop.Model.Currency;
            Check(shop.ReloadSaved() && shop.Reputation.Points == 0 && shop.Model.Currency == cash && shop.Reports.Last.sales > 0 && File.ReadAllText(shop.SavePath) == legacy,
                "Format-five migration keeps past sales and money but starts reputation at zero without a read writeback");
            Check(shop.SaveNow() && File.ReadAllText(shop.SavePath + ".v5.bak") == legacy &&
                JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)).reputation.version == 1,
                "First format-six write archives exact legacy bytes and stores versioned reputation");
            SetReputationFixture(100);
            string parked = shop.SavePath + ".reset-test";
            File.Move(shop.SavePath, parked);
            Directory.CreateDirectory(shop.SavePath);
            shop.ConfirmNewGame();
            Check(shop.Reputation.Points == 100 && shop.Model.Currency == cash && shop.Panel == ShopPanel.Reset,
                "Failed reset write retains reputation and progression in memory");
            Directory.Delete(shop.SavePath); File.Move(parked, shop.SavePath);
            shop.ConfirmNewGame();
            Check(shop.Reputation.Points == 0 && shop.Reputation.Tier == 0 && shop.Model.Currency == 0 && customers.Archetypes[1].Budget == 8 &&
                shop.SaveNow() && shop.ReloadSaved() && shop.Reputation.Points == 0,
                "Successful New game resets persisted reputation and refreshes customer effects to baseline");
        }

        void SetReputationFixture(int points)
        {
            Check(shop.Customers.ActiveCount == 0, "Reputation fixture replaces no live visitor");
            var slot = new SlotData { game = shop.Model.CaptureSave(), cycle = shop.Cycle.CaptureSave(), trades = shop.Reports.CaptureSave(),
                reputation = new ReputationSave { version = ShopReputation.SaveVersion, points = points }, sensitivity = shop.Sensitivity, volume = shop.Volume };
            Check(SaveStore.Write(shop.SavePath, slot, out var error) && shop.ReloadSaved() && shop.Reputation.Points == points,
                "Load isolated reputation boundary fixture " + points + " / " + error);
        }
    }
}
#endif
