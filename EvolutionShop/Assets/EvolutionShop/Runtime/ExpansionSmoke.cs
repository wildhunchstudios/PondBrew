#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator ExpansionScenario()
        {
            limitations.Add("R04.2 technical fixture uses an isolated save, seeded 320 credits, accelerated production and fixture teleports. Mouse/keyboard placement and physical transfers use Input System events; menus/expansion/save/checkout use application commands. No ordinary economy or human-feel claim.");
            limitations.Add("Two customer actors navigate expanded geometry and buy independent slots on one large display. A temporary entry obstruction exercises atomic navigation rejection. No appeal, automation, refunds or stocked-display movement is included.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Expansion checks use the isolated evidence slot only");
            yield return new WaitForSeconds(1);
            Check(shop.Customers.Ready && shop.Layout.StoreTier == 0 && shop.Model.TankIds.Length == 2, "Starter shop begins with original two slots and ready native routes");
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionExpansionKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionExpansionMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.AllowValidationInput = true;
            shop.Customers.ArrivalsEnabled = false; shop.Customers.MaxVisitors = 2;
            if (ShopSession.Arg("-evolutionExpansionReload") != null)
            {
                yield return ExpansionRestartScenario();
                yield break;
            }
            Check(shop.OpenPlacementMenu() && !shop.BuyStoreExpansion() && shop.Layout.StoreTier == 0 && shop.Model.Currency == 0, "Zero funds rejects expansion without geometry or ownership change");
            Check(shop.SaveNow(), "Save empty isolated slot before seeded-credit fixture");
            var data = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); data.game.currency = 320;
            Check(SaveStore.Write(shop.SavePath, data, out var error) && shop.ReloadSaved(), "Load explicitly seeded 320-credit fixture / " + error);
            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(null, ShopLayout.LargeDisplayId), "Open locked large-display preview");
            yield return PlacementNeutral();
            Check(!shop.PlacementValid, "Starter tier rejects locked large display");
            yield return PlacementClick();
            Check(shop.Model.Currency == 320 && shop.Layout.Items.Count == 0, "Locked display purchase spends nothing");
            shop.CancelPlacement();
            Check(shop.BeginPlacement(null, ShopLayout.SmallDisplayId), "Preview small display in starter store");
            yield return PlacementNeutral();
            Check(shop.PlacementValid, "Small-display footprint and customer front access validate / " + shop.PlacementMessage);
            yield return Capture("100-small-preview");
            yield return PlacementClick();
            Check(shop.Layout.Items.Count == 1 && shop.Model.Currency == 296 && shop.Model.TankIds.Length == 3, "Small display costs24 and adds exactly one empty authoritative slot");
            string small = shop.Layout.Items[0].Id, smallSlot = shop.Layout.GetSlotIds(small)[0];
            Check(shop.BeginPlacement(null, ShopLayout.SmallDisplayId), "Preview another starter display at capacity");
            yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement() && shop.Model.Currency == 296, "Starter purchased-display limit rejects another purchase");
            shop.CancelPlacement();
            Check(shop.SaveNow(), "Save unchanged starter geometry before native-failure fixture");
            var blocker = new GameObject("Expansion validation temporary entry obstruction"); blocker.transform.SetParent(shop.World.transform, false);
            blocker.transform.position = shop.Customers.Navigation.EntryPoint + Vector3.up * .8f;
            var collision = blocker.AddComponent<BoxCollider>(); collision.size = new Vector3(3, 2, 2); Physics.SyncTransforms();
            try
            {
                Check(!shop.BuyStoreExpansion() && shop.Layout.StoreTier == 0 && shop.World.AppliedLayout.StoreTier == 0 && shop.Model.Currency == 296 && shop.Model.TankIds.Length == 3,
                    "Native expansion preflight failure restores geometry and preserves money, furniture and slots");
            }
            finally { collision.enabled = false; blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            Check(shop.ReloadSaved() && shop.Customers.Navigation.Ready && shop.OpenPlacementMenu(), "Recover original navigation through normal reload after removing fixture obstruction");
            Check(shop.BuyStoreExpansion() && shop.Layout.StoreTier == 1 && shop.Model.Currency == 236 && shop.Model.TankIds.Length == 3, "First expansion costs60 and adds four metres of west floor without inventing stock capacity");
            Check(shop.World.AppliedLayout.StoreMinX == -11 && shop.Customers.Navigation.CanReach(shop.Customers.Navigation.EntryPoint, new Vector3(-9, 0, -2.5f)), "First west wing has a complete native route from entrance");
            yield return Capture("101-first-expansion-menu");
            yield return PlacementAimAt(-18, -3, new Vector3(-9, .2f, -4.5f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(null, ShopLayout.LargeDisplayId), "Preview large display in first wing");
            yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.PlacementCost == 40, "Large footprint and two-slot front access are clear / " + shop.PlacementMessage);
            yield return CycleTap(Key.R);
            Check(shop.PlacementRotation == 1, "Large ghost rotates from real R input");
            yield return CycleTap(Key.R); yield return CycleTap(Key.R); yield return CycleTap(Key.R);
            Check(shop.PlacementValid && shop.PlacementRotation == 0, "Four quarter-turns restore legal large-display orientation");
            yield return Capture("102-large-preview"); yield return PlacementClick();
            Check(shop.Model.Currency == 196 && shop.Model.TankIds.Length == 5 && shop.Layout.Items.Count == 2, "Large display costs40 and adds two separately addressed empty stock slots");
            string large = shop.Layout.Items[1].Id; string[] largeSlots = shop.Layout.GetSlotIds(large);
            Check(shop.BuyStoreExpansion() && shop.Layout.StoreTier == 2 && shop.Model.Currency == 76 && shop.Model.TankIds.Length == 5, "Second expansion costs120 and keeps existing stock slots unchanged");
            Check(!shop.BuyStoreExpansion() && shop.Model.Currency == 76, "Maximum store tier cannot be charged again");
            yield return PlacementAimAt(-26, -3, new Vector3(-13, .2f, -4.5f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(null, ShopLayout.LargeDisplayId), "Preview third display in second wing");
            yield return PlacementNeutral(); Check(shop.PlacementValid, "Second-wing footprint valid / " + shop.PlacementMessage);
            yield return PlacementClick();
            Check(shop.Layout.Items.Count == 3 && shop.Model.Currency == 36 && shop.Model.TankIds.Length == 7, "Second large display adds two slots; work tables do not share the purchased-display limit");
            string[] finalSlots = shop.Layout.GetSlotIds(shop.Layout.Items[2].Id);
            Check(shop.World.GetComponents<NavMeshSurface>().Length == 1, "All expansions and placements retain one native navigation surface");
            var nav = shop.Customers.Navigation;
            foreach (string slot in shop.Model.TankIds)
                for (int spot = 0; spot < 2; spot++)
                    Check(nav.CanReach(nav.EntryPoint, nav.BrowseSpot(slot, spot)) && nav.CanReach(nav.BrowseSpot(slot, spot), nav.QueueSpot(0)) && nav.CanReach(nav.BrowseSpot(slot, spot), nav.QueueSpot(1)), "Expanded routes to both queue places from " + slot + " spot" + spot);
            Check(!nav.HasNavigationAt(new Vector3(0, 0, 1), .1f) && !nav.HasNavigationAt(new Vector3(-13, 0, 1), .1f), "Customer bake excludes original lab and space behind expansion wall");
            shop.SetPanel(ShopPanel.None); shop.Player.Teleport(new Vector3(-4, .2f, -4.7f), Quaternion.Euler(0, -90, 0));
            yield return PlacementNeutral(); yield return MoveFor(Key.W, 2.9f);
            Check(shop.Player.transform.position.x < -11, "Real WASD/CharacterController crosses both west-wing boundaries");
            yield return Capture("103-expanded-store");
            yield return ExpansionTransfer("pondlet", largeSlots[0]);
            yield return ExpansionTransfer("bubblebelly", largeSlots[1]);
            for (int frame = 0; frame < 60; frame++) { if (!shop.World.ValidateTankBounds(out var boundsError)) throw new InvalidOperationException(boundsError); yield return null; }
            Check(true, "Both large-display residents remain inside their separate glass compartments over 60 animated frames");
            string fish1 = shop.Model.GetTank(largeSlots[0]).Value.Id, fish2 = shop.Model.GetTank(largeSlots[1]).Value.Id;
            Check(fish1 != fish2 && shop.DisplayedStockCount() == 2, "Large display holds two unique specimens counted in opening stock");
            Check(shop.OpenPlacementMenu() && !shop.BeginPlacement(large) && shop.Model.GetTank(largeSlots[0]).Value.Id == fish1 && shop.Model.GetTank(largeSlots[1]).Value.Id == fish2, "Moving stocked display is rejected without losing either specimen");
            yield return Capture("104-stocked-arrangement");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Layout.StoreTier == 2 && shop.Model.GetTank(largeSlots[0]).Value.Id == fish1 && shop.Model.GetTank(largeSlots[1]).Value.Id == fish2, "Expanded stocked save roundtrip preserves tier, stable slot IDs and both unique specimens");
            shop.AllowValidationInput = false;
            yield return EnterCounter(); Check(shop.OpenForDay() && shop.Reports.Current.stockedAtOpen == 2, "Expanded display stock contributes to actual opening report");
            Check(!shop.OpenPlacementMenu() && !shop.BuyStoreExpansion(), "Daytime rejects both arrangement and expansion");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(.3f); var second = Spawn(CustomerArchetypes.Collector);
            Check(shop.Customers.BrowseSpotOf(first) != shop.Customers.BrowseSpotOf(second) && Flat(nav.BrowseSpot(first.TargetTankId, shop.Customers.BrowseSpotOf(first)), nav.BrowseSpot(second.TargetTankId, shop.Customers.BrowseSpotOf(second))) > .8f, "Two buyers receive separate physical browse destinations on the same large display");
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 45, "two large-display buyers reaching queue");
            Check(first.TargetTankId != second.TargetTankId && shop.Model.GetReservation(largeSlots[0]).HasValue && shop.Model.GetReservation(largeSlots[1]).HasValue, "Two customers browse and reserve different slots on one large display");
            yield return Capture("105-expanded-customer-queue");
            int cash = shop.Model.Currency; yield return ServeNext("First expanded-display buyer");
            var next = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(next) == 0 && Flat(ActorPosition(next), nav.QueueSpot(0)) < .5f, 12, "second expanded buyer advancing physically");
            yield return ServeNext("Second expanded-display buyer");
            Check(shop.Model.Currency == cash + 16 && !shop.Model.GetTank(largeSlots[0]).HasValue && !shop.Model.GetTank(largeSlots[1]).HasValue && !shop.ServeCustomer(), "Large-display sales consume exactly two specimens and pay16 once");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "expanded customers exiting");
            Check(shop.CloseForNight(), "Close expanded shop after both buyers leave");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "expanded shop closing");
            Check(shop.Reports.Last.sales == 2 && shop.Reports.Last.revenue == 16 && shop.Reports.Last.pathProblems == 0, "Expanded shop daily report contains two sales and no path problems");
            yield return Capture("106-expanded-day-report");
            SeedStock("pondlet", smallSlot); SeedStock("ribbonfin", finalSlots[1]);
            shop.SetPanel(ShopPanel.None); AimAt(new Vector3(-10.5f, .2f, -4.7f), new Vector3(-11, 1.1f, -1.5f));
            for (int frame = 0; frame < 150; frame++) { if (!shop.World.ValidateTankBounds(out var endBounds)) throw new InvalidOperationException(endBounds); yield return new WaitForSeconds(.02f); }
            Check(true, "Small Pondlet and far-wing large Ribbonfin remain within glass through three seconds of swim animation");
            yield return Capture("107-stocked-small-and-far-wing");
            Check(shop.SaveNow(), "Save stocked full expansion before reset/reload recovery check");
            string stockedSave = File.ReadAllText(shop.SavePath), preserved = shop.Model.GetTank(finalSlots[1]).Value.Id;
            shop.ConfirmNewGame();
            Check(shop.Layout.StoreTier == 0 && shop.Layout.Items.Count == 0 && shop.Model.TankIds.Length == 2 && shop.Model.Currency == 0, "Confirmed new game restores starter geometry and exact original slots");
            File.WriteAllText(shop.SavePath, stockedSave);
            Check(shop.ReloadSaved() && shop.Layout.StoreTier == 2 && shop.Layout.Items.Count == 3 && shop.Model.TankIds.Length == 7 && shop.Model.GetTank(finalSlots[1]).Value.Id == preserved && shop.Customers.Navigation.Ready,
                "Changed-geometry reload restores both expansions and furnished stock without duplication");
            Check(shop.SaveNow(), "Final expanded fixture is available for separate-process reload validation");
        }

        IEnumerator ExpansionTransfer(string speciesId, string slot)
        {
            shop.AllowValidationInput = false;
            var species = shop.Model.GetSpecies(speciesId); shop.Model.Tick(30);
            for (int i = 0; i < species.Bloom; i++) { Ok(shop.Model.Collect(CultureKind.Bloom), "Collect fixture Bloom"); Ok(shop.Model.Dose(CultureKind.Bloom), "Dose fixture Bloom"); }
            for (int i = 0; i < species.Flow; i++) { Ok(shop.Model.Collect(CultureKind.Flow), "Collect fixture Flow"); Ok(shop.Model.Dose(CultureKind.Flow), "Dose fixture Flow"); }
            Ok(shop.Model.BeginIncubation(), "Begin accelerated fixture incubation"); shop.Model.Tick(30); shop.World.Refresh(shop.Model);
            Station station = null; foreach (var item in shop.World.Stations) if (item.TankId == slot) { station = item; break; }
            Check(station != null, "Physical interaction station exists for " + slot);
            Vector3 point = shop.Customers.Navigation.BrowseSpot(slot, 0); point.y = .2f;
            shop.SetPanel(ShopPanel.None); AimAt(point, station.transform.position + Vector3.up * 1.15f); yield return null; yield return null;
            Check(shop.Focused == station, "Crosshair identifies independent display slot " + slot);
            yield return CycleTap(Key.E);
            Check(shop.Model.GetTank(slot).HasValue && shop.Model.GetTank(slot).Value.SpeciesId == speciesId && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Real E input transfers ready specimen once into " + slot);
        }

        IEnumerator ExpansionRestartScenario()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh player process loads supplied isolated expanded fixture through normal reload");
            Check(shop.Layout.StoreTier == 2 && shop.Layout.Items.Count == 3 && shop.Model.TankIds.Length == 7 && shop.DisplayedStockCount() == 2 && shop.Model.Currency == 52, "Fresh process restores expansion, three displays, seven slots, two specimens and52credits");
            foreach (string slot in shop.Model.TankIds) Check(shop.Customers.Navigation.CanReach(shop.Customers.Navigation.EntryPoint, shop.Customers.Navigation.BrowseSpot(slot, 0)), "Fresh-process route reaches " + slot);
            shop.AllowValidationInput = false; AimAt(new Vector3(-10.5f, .2f, -4.7f), new Vector3(-11, 1.1f, -1.5f));
            yield return Capture("107-expanded-process-reload");
            Check(shop.SaveNow(), "Reloaded expanded state writes a valid current slot in its new isolated directory");
        }
    }
}
#endif



