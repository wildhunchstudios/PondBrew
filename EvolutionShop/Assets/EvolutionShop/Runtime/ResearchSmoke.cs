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
        IEnumerator ResearchScenario()
        {
            limitations.Add("R05.2 isolated fixture: 500 construction credits, accelerated starter recipes and direct starter fixture sales, selected casual visitor and camera teleports. All Lanternfin production uses ordinary simulation time, physical held mouse harvesting and real E; menu actions use application commands. Animation sweeps change presentation time only. No natural affordability, human GUI-click, audio or full-shift pacing claim.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Research scenario uses only its isolated save");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionResearchKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionResearchMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionResearchReload") != null) { yield return ResearchRestart(); yield break; }

            Check(shop.Customers.Ready && shop.Model.Capacity == 4 && !shop.Model.SparkResearchUnlocked && shop.World.Stations.Count == 7, "Fresh research starts locked with original siphon and seven stations");
            Check(shop.SaveNow(), "Save fresh isolated research state");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency = 500;
            Check(SaveStore.Write(shop.SavePath, fixture, out var problem) && shop.ReloadSaved(), "Load labelled 500-credit research/construction fixture / " + problem);
            Check(shop.World.TryGetLabFacility(ShopLayout.UpgradeFacilityId, out var originalBench), "Research uses the original unique upgrade bench");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(!shop.ResearchSpark() && shop.Model.Currency == 500 && !shop.Model.SparkResearchUnlocked, "Physical bench refuses research before starter discoveries");
            yield return Capture("160-research-locked-prerequisites");
            shop.SetPanel(ShopPanel.None);
            float until = Time.realtimeSinceStartup + .8f;
            while (Time.realtimeSinceStartup < until)
            { cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 }); yield return null; }
            yield return PlacementNeutral();
            Check(shop.Model.Available(CultureKind.Spark) == 0 && shop.Model.GetGrower(CultureKind.Spark).Units == 0, "Locked bench held-mouse cannot collect or grow Spark");
            foreach (string species in new[] { "pondlet", "bubblebelly", "ribbonfin" })
            {
                SpecimenProduce(species);
                Ok(shop.Model.TransferToTank("tank-a"), "Starter fixture transfer " + species);
                Ok(shop.Model.SellFromTank("tank-a"), "Labelled direct starter fixture sale " + species);
            }
            shop.World.Refresh(shop.Model);
            Check(shop.Model.SparkResearchReady && shop.Model.Currency == 526 && shop.Model.DiscoveredSpeciesIds.Count == 3, "Three accelerated starter discoveries meet research prerequisite without discovering Lanternfin");
            Check(shop.SaveNow(), "Preserve eligible research fixture before insufficient-funds branch");
            string funded = File.ReadAllText(shop.SavePath);
            fixture = JsonUtility.FromJson<SlotData>(funded); fixture.game.currency = 47;
            Check(SaveStore.Write(shop.SavePath, fixture, out problem) && shop.ReloadSaved(), "Load labelled 47-credit insufficient research fixture / " + problem);
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(!shop.ResearchSpark() && shop.Model.Currency == 47 && !shop.Model.SparkResearchUnlocked, "Eligible research cannot charge or unlock below48credits");
            File.WriteAllText(shop.SavePath, funded);
            Check(shop.ReloadSaved() && shop.Model.Currency == 526, "Restore exact eligible funded fixture");
            shop.SetPanel(ShopPanel.Upgrade); shop.AllowValidationInput = false;
            AimAt(new Vector3(0, .2f, -3.8f), originalBench.position + Vector3.up);
            Check(!shop.ResearchSpark() && shop.Model.Currency == 526 && !shop.Model.SparkResearchUnlocked, "Stale remote Upgrade panel cannot purchase through the partition");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(shop.ResearchSpark() && shop.Model.Currency == 478 && shop.Model.SparkResearchUnlocked && !shop.Model.IsDiscovered("lanternfin"), "Physical research pays exactly48 and unlocks culture/clue without a free discovery");
            Check(!shop.ResearchSpark() && shop.Model.Currency == 478 && shop.Model.GetGrower(CultureKind.Spark).Units == 4, "Repeated research cannot pay twice or refill Spark");
            Check(shop.World.Stations.Count == 7 && shop.Layout.Facilities.Count == 4 && shop.World.TryGetLabFacility(ShopLayout.UpgradeFacilityId, out var researchedBench) && researchedBench == originalBench, "Research adds no station, footprint or duplicate original bench");
            yield return Capture("161-spark-research-clue");
            yield return ResearchHarvest(CultureKind.Spark);
            yield return Capture("162-original-bench-spark-harvest");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string rack = shop.Layout.Items[0].Id; string[] slots = shop.Layout.GetStorageSlotIds(rack);
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion(), "Buy first lab extension for movable research bench");
            yield return LabMove(ShopLayout.UpgradeFacilityId, 0, 8, 3, new Vector3(-3.2f, .2f, 5.9f));
            Check(shop.World.TryGetLabFacility(ShopLayout.UpgradeFacilityId, out var movedBench) && movedBench == originalBench && shop.Model.Available(CultureKind.Spark) == 1 && shop.Model.Currency == 386, "Free original-bench relocation retains research and carried Spark after rack32/lab60 purchases");
            yield return ResearchHarvest(CultureKind.Spark);
            yield return Capture("163-moved-research-bench");
            yield return ResearchHarvest(CultureKind.Bloom); yield return ResearchHarvest(CultureKind.Flow);
            Check(shop.Model.ReservedCultureCount == 4 && shop.Model.Capacity == 4 && !shop.Model.UpgradeOwned, "TwoSpark plus Bloom/Flow fill the original four-unit siphon");
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "UI command doses Bloom");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "UI command doses Flow");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Spark)), "UI command doses first Spark");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Spark)), "UI command doses extra Spark");
            string mixed = JsonUtility.ToJson(shop.Model.CaptureSave());
            Check(!shop.Perform(shop.Model.BeginIncubation()).Success && JsonUtility.ToJson(shop.Model.CaptureSave()) == mixed, "Extra Spark invalidates the exact recipe without consuming staged cultures");
            Ok(shop.Perform(shop.Model.ReturnDose(CultureKind.Spark)), "Return extra staged Spark safely");
            Ok(shop.Perform(shop.Model.ReleaseCulture(CultureKind.Spark)), "Deliberately release returned extra Spark");
            Check(shop.Model.Incubator.Bloom == 1 && shop.Model.Incubator.Flow == 1 && shop.Model.Incubator.Spark == 1, "Three-ingredient incubator UI has the exact research clue recipe");
            yield return Capture("164-spark-incubator-recipe");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Begin first ordinary-time Lanternfin incubation");
            string first = shop.Model.Incubator.SpecimenId;
            float began = Time.realtimeSinceStartup;
            yield return Hold(2f);
            shop.SetPanel(ShopPanel.Pause); float remaining = shop.Model.Incubator.SecondsRemaining;
            Check(remaining > 0 && !shop.Model.IsDiscovered("lanternfin") && shop.SaveNow(), "Save active Lanternfin before first discovery");
            Check(shop.ReloadSaved() && shop.Paused && shop.Model.Incubator.SpecimenId == first && shop.Model.Incubator.SecondsRemaining == remaining && shop.Model.SparkResearchUnlocked, "Paused reload retains research, exact active specimen and remaining work");
            yield return new WaitForSeconds(.35f);
            Check(shop.Model.Incubator.SecondsRemaining == remaining && !shop.Model.IsDiscovered("lanternfin"), "Pause after reload freezes active research production");
            shop.SetPanel(ShopPanel.Incubator);
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "first natural Lanternfin maturity");
            Check(Time.realtimeSinceStartup - began >= shop.Config.IncubationSeconds && shop.Model.Incubator.SpecimenId == first && shop.Model.Incubator.SpeciesId == "lanternfin" && shop.Model.IsDiscovered("lanternfin"), "First Lanternfin matures after ordinary time and discovers once");
            shop.SetPanel(ShopPanel.Journal); yield return Capture("165-lanternfin-journal");
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]) && shop.Model.GetStorage(slots[0]).Value.Id == first, "Physical rack stores the exact newly discovered Lanternfin");
            yield return Capture("166-lanternfin-rack"); yield return SpecimenBounds("First Lanternfin in rack");
            yield return SpecimenDisplayPanel("tank-a");
            Check(shop.StockDisplayFromStorage(slots[0], first) && shop.Model.GetTank("tank-a").Value.Id == first && shop.Model.GetPrice("lanternfin").Value.Price == 14, "Physical display receives exact Lanternfin at standard14");
            yield return SpecimenBounds("First Lanternfin in customer display");
            shop.AllowValidationInput = false; yield return EnterCounter(); Check(shop.OpenForDay(), "Open day with one personally produced Lanternfin");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(!shop.ResearchSpark() && shop.Model.Currency == 386 && shop.Model.SparkResearchUnlocked, "Daytime physical bench rejects research before any repeated purchase");
            var buyer = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => buyer.Phase == CustomerPhase.Queued, 45, "casual buyer queues for Lanternfin");
            Check(buyer.Price == 14 && buyer.SpecimenId == first && shop.Model.GetReservation("tank-a").Value.SpecimenId == first, "Native buyer agrees exact researched specimen and14price");
            yield return EnterCounter(); yield return Capture("167-lanternfin-checkout");
            yield return ServeNext("Native researched-specimen buyer");
            Check(shop.Model.Currency == 400 && !shop.Model.GetTank("tank-a").HasValue && !shop.ServeCustomer(), "Lanternfin checkout removes exact stock and pays14 only once");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "Lanternfin buyer leaves");
            Check(shop.CloseForNight(), "Close researched-stock trading day");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "research day closing");
            Check(shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == 14 && shop.Reports.Last.pathProblems == 0, "Daily report records only the real researched-specimen sale");

            yield return ResearchProduceLantern(); string stored = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(slots[0]), "Store second naturally grown Lanternfin for final reload");
            yield return ResearchProduceLantern(); string displayed = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(slots[1]), "Store third naturally grown Lanternfin before choosing display source");
            yield return SpecimenDisplayPanel("tank-b"); Check(shop.StockDisplayFromStorage(slots[1], displayed), "Place third exact Lanternfin on display while second remains hidden");
            Check(stored != displayed && stored != first && displayed != first && shop.Model.DiscoveredSpeciesIds.Count == 4, "Repeated production keeps unique IDs and does not invent more discoveries");
            yield return SpecimenBounds("Final Lanternfin rack and display");
            Check(shop.SaveNow(), "Preserve final research state before reset/restore recovery");
            string final = File.ReadAllText(shop.SavePath);
            shop.ConfirmNewGame();
            Check(!shop.Model.SparkResearchUnlocked && shop.Model.StorageIds.Length == 0 && shop.Layout.LabTier == 0 && shop.Model.DiscoveredSpeciesIds.Count == 0, "Reset removes research, rack inventory and extension cleanly");
            File.WriteAllText(shop.SavePath, final);
            Check(shop.ReloadSaved() && shop.Model.GetStorage(slots[0]).Value.Id == stored && shop.Model.GetTank("tank-b").Value.Id == displayed, "Changed-geometry restore recovers exact research residents");
            ResearchFinalCheck();
            yield return SpecimenRackPanel(rack); yield return Capture("168-research-final-rack");
            yield return SpecimenDisplayPanel("tank-b"); yield return Capture("169-research-final-display");
            Check(shop.SaveNow(), "Save final research fixture for separate-process reload");
            File.Copy(shop.SavePath, Path.Combine(evidence, "research-final-save.json"), true);
        }

        IEnumerator ResearchFacilityPanel(string id, ShopPanel panel)
        {
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); yield return null;
            Check(shop.Layout.TryGetFacilityAccessPoint(id, out float x, out float z) && shop.World.TryGetLabFacility(id, out _), "Research route has physical operator point / " + id);
            shop.World.TryGetLabFacility(id, out var root);
            AimAt(new Vector3(x, .2f, z), root.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return LabInteract(id, panel);
        }

        IEnumerator ResearchHarvest(CultureKind culture)
        {
            string id = culture == CultureKind.Bloom ? ShopLayout.BloomFacilityId : culture == CultureKind.Flow ? ShopLayout.FlowFacilityId : ShopLayout.UpgradeFacilityId;
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); yield return null;
            Check(shop.Layout.TryGetFacilityAccessPoint(id, out float x, out float z) && shop.World.TryGetLabFacility(id, out _), "Find physical research culture source / " + culture);
            shop.World.TryGetLabFacility(id, out var root);
            AimAt(new Vector3(x, .2f, z), root.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return null; yield return null;
            Check(shop.Focused && shop.TryStationCulture(shop.Focused, out var resolved) && resolved == culture && shop.ScriptedHarvest == null, "Normal focus resolves correct culture / " + culture);
            yield return LabHarvest(id, culture);
        }

        IEnumerator ResearchProduceLantern()
        {
            yield return ResearchHarvest(CultureKind.Bloom); yield return ResearchHarvest(CultureKind.Flow); yield return ResearchHarvest(CultureKind.Spark);
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.UseKnownRecipe("lanternfin")), "Known-recipe UI stages exact collected Lanternfin ingredients");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Begin ordinary repeat Lanternfin production");
            float began = Time.realtimeSinceStartup;
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "ordinary repeat Lanternfin maturity");
            Check(Time.realtimeSinceStartup - began >= shop.Config.IncubationSeconds - .1f && shop.Model.Incubator.SpeciesId == "lanternfin", "Repeat Lanternfin grows in ordinary simulation time");
        }

        void ResearchFinalCheck()
        {
            Check(shop.Model.SparkResearchUnlocked && shop.Model.SparkResearchReady && shop.Model.DiscoveredSpeciesIds.Count == 4 && shop.Model.IsDiscovered("lanternfin"), "Final research state retains purchase and four discoveries");
            Check(shop.Model.Currency == 400 && shop.Model.Capacity == 4 && !shop.Model.UpgradeOwned && shop.Cycle.DayNumber == 2 && shop.Cycle.Phase == ShopDayPhase.Night, "Final fixture is night2/cash400 with original four-unit siphon");
            Check(shop.Layout.LabTier == 1 && shop.Layout.StoreTier == 0 && shop.Layout.Facilities.Count == 4 && shop.Layout.RackCount == 1 && shop.Layout.GetFacility(ShopLayout.UpgradeFacilityId).Value.Z == 8, "Final geometry retains first lab extension and moved unique research bench");
            Check(shop.World.Stations.Count == 8 && shop.Model.StorageIds.Length == 2 && shop.Model.TankIds.Length == 2 && shop.StoredSpecimenCount == 1 && shop.DisplayedStockCount() == 1, "Research final registry has original stations plus one rack, no customer storage alias");
            var stored = shop.Model.GetStorage("table-1-storage-1"); var displayed = shop.Model.GetTank("tank-b");
            Check(stored.HasValue && displayed.HasValue && stored.Value.SpeciesId == "lanternfin" && displayed.Value.SpeciesId == "lanternfin" && stored.Value.Id != displayed.Value.Id && !shop.Model.GetStorage("table-1-storage-2").HasValue && !shop.Model.GetTank("tank-a").HasValue, "Final rack/display retain two distinct Lanternfin and two empty destinations");
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Dosing && shop.Model.ReservedCultureCount == 0 && shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == 14, "Final incubator is free and the real trading report retains one14credit sale");
            LabRoutesCheck("Researched lab");
        }

        IEnumerator ResearchRestart()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process explicitly restores supplied research fixture");
            ResearchFinalCheck();
            string stored = shop.Model.GetStorage("table-1-storage-1").Value.Id;
            string displayed = shop.Model.GetTank("tank-b").Value.Id;
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(!shop.ResearchSpark() && shop.Model.Currency == 400 && shop.Model.GetStorage("table-1-storage-1").Value.Id == stored && shop.Model.GetTank("tank-b").Value.Id == displayed, "Fresh-process repeated research preserves payment and both exact residents");
            yield return Capture("170-research-restart-bench");
            yield return SpecimenBounds("Fresh-process Lanternfin rack/display");
            yield return SpecimenRackPanel("table-1"); yield return Capture("171-research-restart-rack");
            Check(shop.SaveNow(), "Fresh process writes valid current research save");
        }
    }
}
#endif
