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
        IEnumerator SpecimenStorageScenario()
        {
            limitations.Add("R05.1b isolated fixture: 500 seeded construction credits, accelerated recipes, selected customer types, fixture camera positions and application buttons. Placement/rotation/cancel and physical station E use synthetic Input System events. One walking segment uses the real CharacterController. No human GUI-click, economy, audio or every-layout claim.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Specimen storage scenario uses only its isolated save");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionSpecimenKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionSpecimenMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionSpecimenStorageReload") != null) { yield return SpecimenStorageRestart(); yield break; }
            Check(shop.Customers.Ready && shop.Model.StorageIds.Length == 0 && shop.Model.TankIds.Length == 2, "Fresh shop has no hidden slots and two customer displays");
            Check(shop.SaveNow(), "Save isolated fresh shop before construction cash fixture");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency = 500;
            Check(SaveStore.Write(shop.SavePath, fixture, out var problem) && shop.ReloadSaved(), "Load labelled 500-credit fixture / " + problem);
            shop.OpenLabMenu(); yield return Capture("159-storage-catalog");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string rack = shop.Layout.Items[0].Id; string[] slots = shop.Layout.GetStorageSlotIds(rack);
            Check(shop.Model.Currency == 468 && shop.Model.StorageIds.Length == 2 && shop.Model.TankIds.Length == 2 && shop.Appeal.Points == 0, "Rack costs32 and adds two hidden slots without display capacity or appeal");
            Check(shop.World.TryGetStorageRack(rack, out var root), "New rack has one stable world root");
            yield return PlacementAimAt(11, 7, new Vector3(5.5f, .2f, 1.3f));
            Check(shop.OpenLabMenu() && shop.BeginPlacement(null, ShopLayout.RackDefinitionId), "Preview second starter rack at ownership cap"); yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement() && shop.Model.Currency == 468, "Starter rack limit rejects purchase without payment"); shop.CancelPlacement();
            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenLabMenu() && shop.BeginPlacement(rack), "Preview lab rack in front store"); yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement(), "Rack cannot become hidden inventory on the customer floor"); shop.CancelPlacement();

            SpecimenProduce("pondlet"); string pond = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]) && shop.Model.GetStorage(slots[0]).Value.Id == pond && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Stash exact Pondlet and free the incubator");
            SpecimenProduce("bubblebelly"); string bubble = shop.Model.Incubator.SpecimenId;
            Check(!shop.StoreReadyInRack(slots[0]) && shop.Model.Incubator.SpecimenId == bubble && shop.Model.GetStorage(slots[0]).Value.Id == pond, "Full compartment rejects a second creature and preserves both identities");
            Check(shop.StoreReadyInRack(slots[1]) && shop.StoredSpecimenCount == 2 && shop.DisplayedStockCount() == 0, "Store second output; hidden stock never counts as displayed");
            SpecimenProduce("ribbonfin"); string ribbon = shop.Model.Incubator.SpecimenId;
            Check(!shop.StoreReadyInRack(slots[0]) && !shop.StoreReadyInRack(slots[1]) && shop.Model.Incubator.SpecimenId == ribbon && shop.StoredSpecimenCount == 2, "Full rack retains third exact creature safely in incubator");
            yield return Capture("150-full-rack-panel");
            shop.SetPanel(ShopPanel.None); AimAt(new Vector3(4.5f, .2f, 1.3f), root.TransformPoint(new Vector3(0, 1.35f, 0)));
            yield return Capture("158-full-rack-overview");
            yield return SpecimenBounds("Pondlet and Bubblebelly in original rack");
            Check(shop.OpenOwnedFurnitureMenu() && !shop.StoreFurniture(rack) && !shop.RequestFurnitureSale(rack), "Stocked rack cannot be stored or sold");
            yield return EditedPlace(rack, ShopLayout.RackDefinitionId, 11, 7, 1, new Vector3(3.8f, .2f, 3.5f));
            Check(shop.World.TryGetStorageRack(rack, out var movedRoot) && movedRoot == root && shop.Model.GetStorage(slots[0]).Value.Id == pond && shop.Model.GetStorage(slots[1]).Value.Id == bubble && shop.Model.Incubator.SpecimenId == ribbon, "Rotated rack move retains original root, both residents and blocked ready output");
            yield return SpecimenRackPanel(rack); yield return Capture("151-stocked-rack-rotated");
            yield return SpecimenBounds("Rotated stocked rack");
            Check(shop.SaveNow(), "Save before occupied-rack native preflight failure");
            string layoutBefore = JsonUtility.ToJson(shop.Layout.CaptureSave());
            shop.AllowValidationInput = true; yield return PlacementAimAt(9, 7, new Vector3(3.2f, .2f, 2.7f));
            Check(shop.OpenOwnedFurnitureMenu() && shop.BeginPlacement(rack), "Begin occupied rack move for obstruction test"); yield return PlacementNeutral();
            while (shop.PlacementRotation != 0) yield return CycleTap(Key.R);
            Check(shop.PlacementX == 9 && shop.PlacementZ == 7 && shop.PlacementValid, "Rack rollback candidate is otherwise legal / " + shop.PlacementMessage);
            var blocker = StorageEntryBlocker();
            try
            {
                yield return PlacementClick();
                Check(JsonUtility.ToJson(shop.Layout.CaptureSave()) == layoutBefore && shop.Model.GetStorage(slots[0]).Value.Id == pond && shop.Model.GetStorage(slots[1]).Value.Id == bubble && shop.Model.Currency == 468, "Native failure restores exact stocked rack pose and money");
            }
            finally { blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            Check(shop.ReloadSaved() && shop.Customers.Navigation.Ready && shop.Model.GetStorage(slots[1]).Value.Id == bubble && shop.Model.Incubator.SpecimenId == ribbon, "Reload recovers native routes, both rack residents and ready output");

            yield return SpecimenDisplayPanel("tank-a");
            Check(!shop.StockDisplayFromStorage(slots[0], "stale-specimen") && !shop.Model.GetTank("tank-a").HasValue && shop.Model.GetStorage(slots[0]).Value.Id == pond, "Stale expected specimen rejects without consuming stock");
            yield return Capture("152-display-stock-choices");
            Check(shop.StockDisplayFromStorage(slots[0], pond) && shop.Model.GetTank("tank-a").Value.Id == pond && !shop.Model.GetStorage(slots[0]).HasValue && shop.Model.Incubator.SpecimenId == ribbon, "Physical display stocks chosen stored Pondlet while incubator output waits");
            Check(!shop.StockDisplayFromStorage(slots[0], pond) && shop.Model.GetTank("tank-a").Value.Id == pond, "Repeated transfer cannot duplicate the specimen");
            Check(shop.ReturnDisplayToStorage(slots[0], pond) && !shop.Model.GetTank("tank-a").HasValue && shop.Model.GetStorage(slots[0]).Value.Id == pond, "Unreserved display returns exact creature to rack");
            Check(shop.StockDisplayFromStorage(slots[0], pond), "Restock returned Pondlet once");
            Check(!shop.StoreReadyInRack(slots[0]), "Remote display panel cannot stash ready output into a rack");
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]) && shop.Model.GetStorage(slots[0]).Value.Id == ribbon && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Third ready creature enters newly freed storage slot");
            yield return SpecimenDisplayPanel("tank-b");
            Check(shop.StockDisplayFromStorage(slots[1], bubble) && shop.DisplayedStockCount() == 2 && shop.StoredSpecimenCount == 1, "Second stored creature enters separate display; one hidden resident remains");
            shop.AllowValidationInput = false; yield return EnterCounter(); Check(shop.OpenForDay(), "Open trading with two displayed and one hidden creature");
            Check(shop.Reports.Current.stockedAtOpen == 2 && shop.Reports.Current.restocked == 0, "Opening report excludes rack residents");
            Check(!shop.OpenLabMenu(), "Rack construction remains night-only");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(.3f); var second = Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 45, "two buyers queue for displayed stock");
            Check(first.TargetTankId != slots[0] && second.TargetTankId != slots[0] && shop.Model.GetStorage(slots[0]).Value.Id == ribbon, "Customers never browse or reserve hidden rack stock");
            yield return SpecimenDisplayPanel("tank-a");
            Check(!shop.ReturnDisplayToStorage(slots[1], pond) && shop.Model.GetTank("tank-a").Value.Id == pond && shop.Model.GetReservation("tank-a").HasValue, "Reserved exact creature cannot be returned into empty rack slot");
            yield return Capture("153-reserved-display-storage-guard");
            int cash = shop.Model.Currency;
            yield return ServeNext("First stored-stock buyer");
            var next = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(next) == 0 && Flat(ActorPosition(next), shop.Customers.Navigation.QueueSpot(0)) < .5f, 12, "second stored-stock buyer advances");
            yield return ServeNext("Second stored-stock buyer");
            Check(shop.Model.Currency == cash + 16 && shop.DisplayedStockCount() == 0 && shop.Model.GetStorage(slots[0]).Value.Id == ribbon, "Two stored-stock sales pay16 once while hidden Ribbonfin remains owned");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "stored-stock buyers exit");
            yield return SpecimenDisplayPanel("tank-a");
            Check(shop.StockDisplayFromStorage(slots[0], ribbon) && shop.Reports.Current.restocked == 1, "Daytime storage-to-display transfer records one restock");
            SpecimenProduce("pondlet"); string nextPond = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[1]) && shop.Reports.Current.restocked == 1, "Daytime incubator-to-storage transfer frees output without counting as a display restock");
            yield return SpecimenDisplayPanel("tank-a");
            Check(shop.ReturnDisplayToStorage(slots[0], ribbon) && shop.Reports.Current.restocked == 1 && shop.DisplayedStockCount() == 0, "Daytime return to lab is safe and is not a restock");
            Check(shop.CloseForNight(), "Close trading with hidden stock retained");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "stored-stock day closing");
            Check(shop.Reports.Last.sales == 2 && shop.Reports.Last.restocked == 1 && shop.Reports.Last.stockedAtClose == 0 && shop.Reports.Last.pathProblems == 0, "Day report keeps sales, restocks and displayed closing stock distinct from storage");

            Check(shop.OpenLabMenu() && shop.BuyLabExpansion(), "Expand lab to unlock a second owned rack");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, -8, 15, 0, new Vector3(-4, .2f, 5.3f));
            string secondRack = shop.Layout.Items[1].Id; string[] secondSlots = shop.Layout.GetStorageSlotIds(secondRack);
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion(), "Expand lab again to unlock final rack allowance");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 8, 23, 0, new Vector3(4, .2f, 9.3f));
            string thirdRack = shop.Layout.Items[2].Id; string[] thirdSlots = shop.Layout.GetStorageSlotIds(thirdRack);
            Check(shop.Model.Currency == 240 && shop.Model.StorageIds.Length == 6 && shop.Model.TankIds.Length == 2 && shop.Layout.RackCount == 3, "Both extensions plus three racks give exactly six hidden slots and two displays");
            yield return SpecimenDisplayPanel("tank-a"); Check(shop.StockDisplayFromStorage(slots[0], ribbon) && shop.ReturnDisplayToStorage(secondSlots[0], ribbon), "Move Ribbonfin through physical display into second rack");
            yield return SpecimenDisplayPanel("tank-b"); Check(shop.StockDisplayFromStorage(slots[1], nextPond) && shop.ReturnDisplayToStorage(thirdSlots[1], nextPond), "Move Pondlet through physical display into third rack");
            yield return SpecimenBounds("Ribbonfin and Pondlet across expanded racks");
            Check(shop.OpenOwnedFurnitureMenu() && shop.StoreFurniture(rack) && shop.Model.StorageIds.Length == 4 && !shop.World.TryGetStorageRack(rack, out _), "Empty furniture storage retires only its two empty slots and live station");
            Check(shop.Model.GetStorage(secondSlots[0]).Value.Id == ribbon && shop.Model.GetStorage(thirdSlots[1]).Value.Id == nextPond, "Storing empty first rack keeps exact residents in both remaining racks");
            yield return EditedPlace(rack, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            Check(shop.Model.StorageIds.Length == 6 && shop.Model.Currency == 240, "Free empty rack redeployment restores original slot identities once");
            Check(shop.RequestFurnitureSale(rack) && shop.ConfirmFurnitureSale() && shop.Model.Currency == 256 && shop.Model.StorageIds.Length == 4, "Confirmed empty rack resale pays16 once and removes only its slots");
            Check(!shop.ConfirmFurnitureSale() && shop.Model.Currency == 256, "Repeated empty rack confirmation cannot duplicate refund");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string replacement = shop.Layout.Items[2].Id;
            Check(replacement != rack && shop.StoreFurniture(replacement) && shop.Model.Currency == 224 && shop.Layout.RackCount == 3 && shop.Model.StorageIds.Length == 4, "Replacement uses a fresh ID; stored ownership still consumes final rack allowance");
            Check(shop.BeginPlacement(null, ShopLayout.RackDefinitionId), "Preview fourth owned rack while replacement is stored"); yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement() && shop.Model.Currency == 224, "Stored rack still counts toward tier-three cap"); shop.CancelPlacement();

            // One initial pose, then actual input movement through the door and expanded lab to both racks.
            editedWalkingMetres = 0; editedDoorCrossings = 0;
            shop.Player.Teleport(new Vector3(0, .2f, -3.8f), Quaternion.identity);
            yield return EditedWalkTo(new Vector2(0, 2.4f), "storage lab doorway");
            yield return EditedWalkTo(new Vector2(0, 6.1f), "first storage extension");
            yield return EditedWalkTo(new Vector2(-4, 6.1f), "second rack operator");
            yield return SpecimenRackPanel(secondRack, false);
            yield return Capture("154-expanded-rack-operator");
            yield return EditedWalkTo(new Vector2(0, 6.1f), "return to storage aisle");
            yield return EditedWalkTo(new Vector2(0, 10.1f), "second storage extension");
            yield return EditedWalkTo(new Vector2(4, 10.1f), "third rack operator");
            yield return SpecimenRackPanel(thirdRack, false);
            Check(editedWalkingMetres > 24 && editedDoorCrossings == 1, "Actual player reaches both expanded rack service points / metres=" + editedWalkingMetres.ToString("0.00"));
            SpecimenProduce("bubblebelly"); string ready = shop.Model.Incubator.SpecimenId;
            Check(shop.SaveNow(), "Save expanded storage with two exact residents and ready incubator output");
            string saved = File.ReadAllText(shop.SavePath);
            shop.ConfirmNewGame(); Check(shop.Model.StorageIds.Length == 0 && shop.StoredSpecimenCount == 0 && shop.Layout.LabTier == 0, "Confirmed reset returns original lab with no orphan rack inventory");
            File.WriteAllText(shop.SavePath, saved);
            Check(shop.ReloadSaved() && shop.Model.GetStorage(secondSlots[0]).Value.Id == ribbon && shop.Model.GetStorage(thirdSlots[1]).Value.Id == nextPond && shop.Model.Incubator.SpecimenId == ready && shop.Model.Currency == 224, "Changed-geometry reload restores exact hidden/ready stock, rack poses and money");
            SpecimenRegistryCheck();
            yield return SpecimenRackPanel(secondRack); yield return Capture("155-final-rack-and-ready-output");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("156-rack-ownership-storage");
            Check(shop.SaveNow(), "Save final storage state for separate-process reload");
            File.Copy(shop.SavePath, Path.Combine(evidence, "specimen-storage-final-save.json"));
        }

        void SpecimenProduce(string speciesId)
        {
            var species = shop.Model.GetSpecies(speciesId); shop.Model.Tick(30);
            for (int i = 0; i < species.Bloom; i++) { Ok(shop.Model.Collect(CultureKind.Bloom), "Collect fixture Bloom"); Ok(shop.Model.Dose(CultureKind.Bloom), "Dose fixture Bloom"); }
            for (int i = 0; i < species.Flow; i++) { Ok(shop.Model.Collect(CultureKind.Flow), "Collect fixture Flow"); Ok(shop.Model.Dose(CultureKind.Flow), "Dose fixture Flow"); }
            Ok(shop.Model.BeginIncubation(), "Begin fixture incubation"); shop.Model.Tick(30); shop.World.Refresh(shop.Model);
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.Model.Incubator.SpeciesId == speciesId, "Produce labelled accelerated " + speciesId);
        }

        IEnumerator SpecimenRackPanel(string itemId, bool fixturePosition = true)
        {
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); yield return null;
            Station station = null; foreach (var candidate in shop.World.Stations) if (candidate.StorageItemId == itemId) station = candidate;
            Check(station != null && shop.Layout.TryGetStorageAccessPoint(itemId, out _, out _), "Placed rack exposes reachable physical station " + itemId);
            if (fixturePosition)
            {
                shop.Layout.TryGetStorageAccessPoint(itemId, out float x, out float z);
                AimAt(new Vector3(x, .2f, z), station.transform.TransformPoint(new Vector3(0, 1.1f, 0)));
            }
            else shop.Player.View.transform.LookAt(station.transform.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return null; yield return null;
            Check(shop.Focused == station, "Crosshair identifies rack " + itemId);
            yield return CycleTap(UnityEngine.InputSystem.Key.E);
            Check(shop.Panel == ShopPanel.StorageRack, "Real E opens rack compartment panel " + itemId);
        }

        IEnumerator SpecimenDisplayPanel(string id)
        {
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); yield return null;
            Station station = null; foreach (var candidate in shop.World.Stations) if (candidate.TankId == id) station = candidate;
            Vector3 position = shop.Customers.Navigation.BrowseSpot(id, 0); position.y = .2f;
            AimAt(position, station.transform.TransformPoint(new Vector3(0, 1.1f, 0))); yield return null; yield return null;
            Check(shop.Focused == station, "Crosshair identifies display " + id);
            yield return CycleTap(UnityEngine.InputSystem.Key.E);
            Check(shop.Panel == ShopPanel.Tank && shop.SelectedTank == id, "Real E opens source/return choices at physical display " + id);
        }

        IEnumerator SpecimenBounds(string label)
        {
            Check(shop.World.ValidateTankBounds(out var immediate), label + " / immediate resident pose / " + immediate);
            // Deterministic presentation-only sweep samples many body/tail phase combinations.
            // It does not advance model time, sales, growers or the shop day.
            for (int sample = 0; sample <= 2400; sample++)
            {
                shop.World.Animate(sample * .05f, 0);
                if (!shop.World.ValidateTankBounds(out var error)) throw new InvalidOperationException(label + " / sweep " + sample + " / " + error);
            }
            Check(true, label + " / 120-second animation sweep at0.05-second samples stays contained");
            for (int frame = 0; frame < 90; frame++)
            { if (!shop.World.ValidateTankBounds(out var error)) throw new InvalidOperationException(label + " / " + error); yield return null; }
            Check(true, label + " / animated residents remain inside their compartments over90frames");
        }

        void SpecimenRegistryCheck()
        {
            Check(shop.Layout.LabTier == 2 && shop.Layout.RackCount == 3 && shop.Layout.StoredCount == 1 && shop.Model.StorageIds.Length == 4 && shop.StoredSpecimenCount == 2 && shop.Model.TankIds.Length == 2 && shop.DisplayedStockCount() == 0 && shop.Model.Currency == 224, "Final registry has two placed racks, one stored replacement, four hidden slots, two residents and224credits");
            int stations = 0; foreach (var station in shop.World.Stations) if (station.Kind == StationKind.StorageRack) stations++;
            Check(stations == 2 && shop.World.Stations.Count == 9 && shop.Model.Incubator.Phase == IncubatorPhase.Ready, "Only placed racks register stations; ready output remains in the unique incubator");
            foreach (string id in shop.Model.StorageIds)
                Check(!shop.Layout.TryGetBrowsePoint(id, 0, out _, out _), "Hidden compartment is not a customer browse target " + id);
            LabRoutesCheck("Specimen storage lab");
        }

        IEnumerator SpecimenStorageRestart()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process reloads supplied isolated specimen storage fixture");
            SpecimenRegistryCheck();
            var save = shop.Model.CaptureSave();
            var ids = new System.Collections.Generic.HashSet<string>();
            Check(ids.Add(shop.Model.Incubator.SpecimenId), "Fresh process retains ready specimen identity");
            foreach (string id in shop.Model.StorageIds)
                if (shop.Model.GetStorage(id).HasValue) Check(ids.Add(shop.Model.GetStorage(id).Value.Id), "Fresh process retains unique stored specimen " + id);
            yield return SpecimenBounds("Fresh-process storage");
            string rack = null; foreach (var item in shop.Layout.PlacedItems) if (item.DefinitionId == ShopLayout.RackDefinitionId) { rack = item.Id; break; }
            yield return SpecimenRackPanel(rack); yield return Capture("157-specimen-storage-restart");
            Check(shop.SaveNow(), "Fresh process writes valid game3/layout6/envelope13 save");
        }
    }
}
#endif
