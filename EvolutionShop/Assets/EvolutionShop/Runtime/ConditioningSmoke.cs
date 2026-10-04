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
        IEnumerator ConditioningScenario()
        {
            limitations.Add("R05.3 isolated fixture: 500 construction credits, accelerated starter/Pondlet preparation and direct starter sales; input-driven placement, physical E and Spark harvesting, normal-time conditioning, selected casual visitor, application menu commands and explicit camera teleports. No human GUI-click, audio, natural affordability or full-shift pacing claim. Animation sweeps advance presentation only.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Conditioning uses isolated evidence save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionConditioningKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionConditioningMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionConditioningReload") != null) { yield return ConditioningRestart(); yield break; }
            Check(shop.Customers.Ready && shop.Model.ConditionerIds.Length == 0 && shop.SaveNow(), "Fresh world has no conditioning authority before purchase");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency = 500;
            Check(SaveStore.Write(shop.SavePath, fixture, out var problem) && shop.ReloadSaved(), "Load labelled construction fixture / " + problem);
            foreach (string species in new[] { "pondlet", "bubblebelly", "ribbonfin" })
            { SpecimenProduce(species); Ok(shop.Model.TransferToTank("tank-a"), "Starter fixture display"); Ok(shop.Model.SellFromTank("tank-a"), "Starter fixture sale"); }
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(shop.ResearchSpark(), "Unlock Spark through physical research bench");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string rack = shop.Layout.Items[0].Id; string[] slots = shop.Layout.GetStorageSlotIds(rack);
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion(), "Expand lab for conditioning work area");
            Check(shop.BeginPlacement(null, ShopLayout.ConditionerDefinitionId), "Preview locked vat before manual Lanternfin discovery");
            yield return PlacementNeutral();
            Check(!shop.CommitPlacement() && shop.Layout.ConditionerCount == 0, "Unknown process cannot be bought before discovery"); shop.CancelPlacement();
            yield return ResearchHarvest(CultureKind.Bloom); yield return ResearchHarvest(CultureKind.Flow); yield return ResearchHarvest(CultureKind.Spark);
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Dose first Lanternfin Bloom");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "Dose first Lanternfin Flow");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Spark)), "Dose first Lanternfin Spark");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Begin manual first Lanternfin discovery");
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "first Lanternfin maturation");
            Check(shop.Model.IsDiscovered("lanternfin"), "Manual incubation unlocks the known conditioning process");
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(slots[0]), "Store manually discovered Lanternfin");
            yield return SpecimenDisplayPanel("tank-b");
            Check(shop.StockDisplayFromStorage(slots[0], shop.Model.GetStorage(slots[0]).Value.Id), "Keep original Lanternfin outside the new machine");
            // Remove the known-recipe exemplar only in this labelled setup, to make the later buyer unambiguous.
            Ok(shop.Model.SellFromTank("tank-b"), "Labelled setup sale of manual exemplar");
            yield return EditedPlace(null, ShopLayout.ConditionerDefinitionId, -8, 14, 0, new Vector3(-4, .2f, 5.2f));
            string vat = shop.Model.ConditionerIds[0];
            Check(shop.Layout.ConditionerCount == 1 && shop.Model.Currency == 336 && shop.Model.StorageIds.Length == 2 && shop.Model.TankIds.Length == 2, "Vat pays64 once and has separate noncustomer registry");
            Check(shop.World.TryGetConditioner(vat, out var originalRoot) && shop.World.Stations.Count == 9, "Purchased vat has one physical station/root");
            editedWalkingMetres = 0; editedDoorCrossings = 0;
            shop.Player.Teleport(new Vector3(0, .2f, -3.8f), Quaternion.identity);
            yield return EditedWalkTo(new Vector2(0, 2.4f), "conditioning doorway");
            yield return EditedWalkTo(new Vector2(0, 5.6f), "conditioning extension aisle");
            yield return EditedWalkTo(new Vector2(-4, 5.6f), "conditioning operator");
            Check(editedWalkingMetres > 11 && editedDoorCrossings == 1, "Actual movement reaches the conditioning operator through the lab doorway");
            yield return ConditioningPanel(vat); yield return Capture("180-conditioning-empty");
            Check(!shop.BeginSelectedConditioning(), "Empty selection cannot consume Spark");

            SpecimenProduce("pondlet"); string first = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(slots[0]), "Prepare first stored Pondlet");
            SpecimenProduce("pondlet"); string second = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(slots[1]), "Prepare second stored Pondlet");
            yield return ResearchHarvest(CultureKind.Spark);
            yield return ConditioningPanel(vat);
            Check(shop.ToggleConditioningInput(slots[0], first) && shop.ToggleConditioningInput(slots[1], second), "Physical vat selects two exact stored Pondlets");
            yield return Capture("181-conditioning-selection");
            AimAt(new Vector3(0, .2f, -3.8f), originalRoot.position + Vector3.up);
            Check(!shop.BeginSelectedConditioning() && shop.StoredSpecimenCount == 2, "Remote stale vat panel cannot move fish through the partition");
            yield return ConditioningPanel(vat);
            Check(shop.ToggleConditioningInput(slots[0], first) && shop.ToggleConditioningInput(slots[1], second), "Reselect exact inputs after returning in person");
            Check(shop.BeginSelectedConditioning() && shop.StoredSpecimenCount == 0 && shop.Model.Available(CultureKind.Spark) == 0 && shop.Model.ReservedCultureCount == 1, "Batch owns both fish and reserves only one Spark");
            Ok(shop.Model.Collect(CultureKind.Bloom), "Fill cancellation fixture siphon1");
            Ok(shop.Model.Collect(CultureKind.Flow), "Fill cancellation fixture siphon2");
            Ok(shop.Model.Collect(CultureKind.Bloom), "Fill cancellation fixture siphon3");
            Check(shop.Model.ReservedCultureCount == 4, "Reserved Spark prevents overfilling original siphon");
            Check(shop.CancelSelectedConditioning() && shop.Model.Available(CultureKind.Spark) == 1 && shop.Model.ReservedCultureCount == 4, "Cancel refunds Spark even with full siphon");
            Check(!shop.CancelSelectedConditioning() && shop.Model.GetConditioner(vat).Value.First.Value.Id == first && shop.Model.GetConditioner(vat).Value.Second.Value.Id == second, "Repeated cancel cannot refund twice or duplicate inputs");
            yield return Capture("182-conditioning-canceled");
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreConditionerInRack(vat, 0, slots[0], first) && shop.StoreConditionerInRack(vat, 1, slots[1], second), "Collect canceled Pondlets into original rack slots");
            Check(shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Idle && shop.Model.GetStorage(slots[0]).Value.SpeciesId == "pondlet", "Drained cancellation restores empty machine and unchanged fish");
            Ok(shop.Model.ReleaseCulture(CultureKind.Bloom), "Release cancellation test Bloom1");
            Ok(shop.Model.ReleaseCulture(CultureKind.Bloom), "Release cancellation test Bloom2");
            Ok(shop.Model.ReleaseCulture(CultureKind.Flow), "Release cancellation test Flow");

            yield return ConditioningPanel(vat);
            Check(shop.ToggleConditioningInput(slots[0], first) && shop.ToggleConditioningInput(slots[1], second) && shop.BeginSelectedConditioning(), "Restart recovered batch through physical vat");
            float began = Time.realtimeSinceStartup;
            Check(shop.OpenOwnedFurnitureMenu() && !shop.StoreFurniture(vat) && !shop.RequestFurnitureSale(vat), "Active vat cannot be stored or sold");
            yield return EditedPlace(vat, ShopLayout.ConditionerDefinitionId, 8, 14, 1, new Vector3(2.2f, .2f, 7));
            Check(shop.World.TryGetConditioner(vat, out var movedRoot) && movedRoot == originalRoot && shop.Model.GetConditioner(vat).Value.Count == 2, "Rotated busy move retains world root and exact machine inventory");
            yield return ConditioningPanel(vat); yield return Capture("183-conditioning-processing");
            shop.SetPanel(ShopPanel.Pause);
            float remaining = shop.Model.GetConditioner(vat).Value.SecondsRemaining;
            Check(remaining > 0 && shop.SaveNow() && shop.ReloadSaved(), "Save and reload an active batch");
            Check(shop.Model.GetConditioner(vat).Value.SecondsRemaining == remaining && shop.Model.GetConditioner(vat).Value.First.Value.Id == first, "Reload preserves exact remaining time and first identity");
            yield return new WaitForSeconds(.4f);
            Check(shop.Model.GetConditioner(vat).Value.SecondsRemaining == remaining, "Pause freezes conditioning after reload");
            shop.SetPanel(ShopPanel.None);
            yield return WaitFor(() => shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Ready, 16, "ordinary batch completion");
            var ready = shop.Model.GetConditioner(vat).Value;
            Check(Time.realtimeSinceStartup - began >= 11.9f && ready.Count == 2 && ready.First.Value.Id == first && ready.Second.Value.Id == second && ready.First.Value.SpeciesId == "lanternfin" && ready.Second.Value.SpeciesId == "lanternfin", "Normal-time batch transforms two species while preserving exact specimen IDs");
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Dosing && shop.Model.ReservedCultureCount == 0 && shop.Model.DiscoveredSpeciesIds.Count == 4, "Independent completion consumes reserved Spark and does not invent discoveries");
            Check(SaveStore.Read(shop.SavePath, shop.Config, out var autoSaved, out _, out problem) && autoSaved.GetConditioner(vat).Value.Phase == ConditionerPhase.Ready, "Completion autosave includes the ready machine / " + problem);
            Check(shop.OpenOwnedFurnitureMenu() && !shop.StoreFurniture(vat) && !shop.RequestFurnitureSale(vat), "Ready output blocks furniture storage and sale until collected");
            yield return ConditioningPanel(vat); yield return Capture("184-conditioning-output");
            yield return SpecimenBounds("Two vat Lanternfin");
            yield return SpecimenDisplayPanel("tank-a");
            Check(shop.StockDisplayFromConditioner(vat, 0, first) && !shop.StockDisplayFromConditioner(vat, 0, first), "Physical display collects exact first output only once");
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreConditionerInRack(vat, 1, slots[0], second) && shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Idle, "Second output moves into rack and machine becomes idle");
            Check(shop.OpenOwnedFurnitureMenu() && shop.StoreFurniture(vat) && !shop.World.TryGetConditioner(vat, out _) && shop.Model.ConditionerIds.Length == 0, "Empty vat storage retires physical station and registry safely");
            yield return EditedPlace(vat, ShopLayout.ConditionerDefinitionId, 8, 14, 1, new Vector3(2.2f, .2f, 7));
            Check(shop.Model.ConditionerIds.Length == 1 && shop.Model.GetConditioner(vat).Value.Count == 0 && shop.Layout.ConditionerCount == 1, "Redeploy retains owned machine ID and empty state without charging again");
            LabRoutesCheck("Conditioning lab");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open with conditioned display stock");
            var buyer = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => buyer.Phase == CustomerPhase.Queued, 35, "conditioned-stock buyer queues");
            Check(buyer.SpecimenId == first && buyer.Price == 14, "Buyer reserves exact conditioned specimen at existing Lanternfin price");
            yield return ServeNext("Conditioned-stock buyer");
            Check(shop.Model.Currency == 350 && !shop.Model.GetTank("tank-a").HasValue && !shop.ServeCustomer(), "Conditioned stock pays14 exactly once");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "conditioned-stock buyer exits");
            Check(shop.CloseForNight(), "Close conditioning sale day");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "conditioning closing");
            Check(shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == 14 && shop.Reports.Last.pathProblems == 0, "Real trading report records one conditioned sale without path problems");

            SpecimenProduce("pondlet"); string next = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(slots[1]), "Prepare single-fish restart fixture");
            yield return ResearchHarvest(CultureKind.Spark);
            yield return ConditioningPanel(vat);
            Check(shop.ToggleConditioningInput(slots[1], next) && shop.BeginSelectedConditioning(), "Single-fish batch remains usable");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow(), "Save active conditioning fixture for separate process");
            File.Copy(shop.SavePath, Path.Combine(evidence, "conditioning-active-save.json"), true);
            Check(shop.Model.GetStorage(slots[0]).Value.Id == second && shop.Model.Currency == 350, "Final isolated fixture retains unsold output, active next input and exact earned cash");
        }

        IEnumerator ConditioningPanel(string id)
        {
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); yield return null;
            Check(shop.Layout.TryGetConditionerAccessPoint(id, out float x, out float z) && shop.World.TryGetConditioner(id, out _), "Vat has physical operator access");
            shop.World.TryGetConditioner(id, out var root);
            AimAt(new Vector3(x, .2f, z), root.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return null; yield return null;
            Check(shop.Focused && shop.Focused.ConditionerItemId == id, "Crosshair resolves conditioning station");
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Conditioning, "Real E opens conditioning controls");
        }

        IEnumerator ConditioningRestart()
        {
            shop.SetPanel(ShopPanel.Pause);
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process loads supplied active conditioning save");
            string id = shop.Model.ConditionerIds[0]; var before = shop.Model.GetConditioner(id).Value;
            Check(before.Phase == ConditionerPhase.Processing && before.Count == 1 && before.First.Value.SpeciesId == "pondlet" && shop.Model.ReservedCultureCount == 1, "Restart keeps active Pondlet, reserved Spark and batch capacity");
            Check(shop.Model.Currency == 350 && shop.Model.GetStorage("table-1-storage-1").Value.SpeciesId == "lanternfin" && shop.Reports.Last.revenue == 14, "Restart retains unsold Lanternfin and committed sale");
            yield return new WaitForSeconds(.4f);
            Check(shop.Model.GetConditioner(id).Value.SecondsRemaining == before.SecondsRemaining, "Closed-app time and paused restart do not advance production");
            yield return ConditioningPanel(id);
            yield return WaitFor(() => shop.Model.GetConditioner(id).Value.Phase == ConditionerPhase.Ready, 16, "restarted batch completion");
            Check(shop.Model.GetConditioner(id).Value.First.Value.Id == before.First.Value.Id && shop.Model.GetConditioner(id).Value.First.Value.SpeciesId == "lanternfin" && shop.Model.ReservedCultureCount == 0, "Restarted work consumes one Spark and preserves specimen ID");
            yield return SpecimenBounds("Restarted vat output");
            yield return ConditioningPanel(id); yield return Capture("185-conditioning-restarted-output");
            yield return SpecimenRackPanel("table-1");
            Check(shop.StoreConditionerInRack(id, 0, "table-1-storage-2", before.First.Value.Id), "Fresh process collects exact resumed output");
            Check(shop.SaveNow() && shop.Model.GetConditioner(id).Value.Phase == ConditionerPhase.Idle && shop.StoredSpecimenCount == 2, "Fresh process saves empty vat and two distinct stored Lanternfin");
            LabRoutesCheck("Restarted conditioning lab");
        }
    }
}
#endif
