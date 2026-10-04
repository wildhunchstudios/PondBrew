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
        IEnumerator FurnitureStorageScenario()
        {
            limitations.Add("R04.4 isolated technical fixture: 250 seeded credits, accelerated stock, fixture teleports, synthetic Input System placement/rotation/cancellation and physical E transfers. Menus, storage, sale confirmation and checkout use application commands; not ordinary earnings or human GUI-click evidence.");
            limitations.Add("Native navigation and actual customer actors exercise a relocated stocked display. Temporary entry obstructions probe preflight rollback. Fresh-process validation explicitly reloads its isolated supplied slot, never the normal user save.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Storage scenario uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            Check(shop.Customers.Ready && shop.Layout.Items.Count == 0 && shop.Model.TankIds.Length == 2, "Fresh shop starts with original slots and verified native routes");
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionStorageKeyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionStorageMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.AllowValidationInput = true; shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionStorageReload") != null) { yield return FurnitureStorageRestart(); yield break; }
            Check(shop.SaveNow(), "Save empty isolated slot before seeded cash");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency = 250;
            Check(SaveStore.Write(shop.SavePath, fixture, out var problem) && shop.ReloadSaved(), "Load labelled 250-credit fixture / " + problem);
            Check(shop.OpenPlacementMenu() && !shop.StoreFurniture("tank-a") && !shop.RequestFurnitureSale("tank-a"), "Original tanks are not owned furniture and cannot be removed");

            yield return StoragePlace(null, ShopLayout.CoralPlanterId, -4, -1, new Vector3(-2, .2f, -2.1f));
            string coral = shop.Layout.Items[0].Id;
            Check(shop.World.TryGetPlacedTable(coral, out var oldCoral) && shop.Model.Currency == 234 && shop.Appeal.Points == 10, "Coral purchased with store appeal and live geometry");
            Check(shop.StoreFurniture(coral) && shop.Layout.GetItem(coral).Value.Stored && shop.Appeal.Points == 0 && shop.Model.Currency == 234, "Free storage removes appeal but keeps ownership and cash");
            Check(!oldCoral.gameObject.activeSelf && !shop.World.TryGetPlacedTable(coral, out _) && !shop.StoreFurniture(coral), "Stored root is inactive immediately and repeated storage is rejected");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Layout.StoredCount == 1 && shop.Appeal.Points == 0, "Stored decor survives save reload without hidden appeal");
            yield return StoragePlace(coral, null, 4, -1, new Vector3(2, .2f, -2.1f));
            Check(shop.Model.Currency == 234 && shop.Appeal.Points == 10 && !shop.Layout.GetItem(coral).Value.Stored, "Redeployment uses same owned piece for free and restores appeal once");

            yield return StoragePlace(null, ShopLayout.SmallDisplayId, 3, -5, new Vector3(1.5f, .2f, -4.7f));
            string small = shop.Layout.Items[1].Id, smallSlot = shop.Layout.GetSlotIds(small)[0];
            Check(shop.World.TryGetPlacedTable(small, out var oldSmall) && shop.Model.Currency == 210 && shop.Model.TankIds.Length == 3, "Small display purchase adds one active slot");
            Check(shop.StoreFurniture(small) && shop.Model.TankIds.Length == 2 && shop.Layout.GetSlotIds(small)[0] == smallSlot, "Storage retires the empty active slot while retaining stable future slot ID");
            bool staleStation = false; foreach (var station in shop.World.Stations) staleStation |= station.TankId == smallSlot;
            Check(!staleStation && !oldSmall.gameObject.activeSelf && !shop.Layout.TryGetBrowsePoint(smallSlot, 0, out _, out _), "Stored display has no active station, collider root or browse destination");
            Check(shop.BeginPlacement(null, ShopLayout.SmallDisplayId), "Preview purchase while the only permitted display is stored"); yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement() && shop.Model.Currency == 210, "Stored ownership still consumes the starter display limit"); shop.CancelPlacement();
            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(small), "Start free stored display preview"); yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.PlacementCost == 0 && shop.Model.TankIds.Length == 2, "Preview grants no active slot before commit");
            yield return CycleTap(UnityEngine.InputSystem.Key.R); yield return PlacementClick(true);
            Check(shop.Layout.GetItem(small).Value.Stored && shop.Model.Currency == 210 && shop.Model.TankIds.Length == 2, "Rotation and cancel keep display in storage without charging");
            yield return StoragePlace(small, null, 3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.Model.TankIds.Length == 3 && shop.Layout.GetSlotIds(small)[0] == smallSlot && shop.Model.Currency == 210, "Free display redeployment restores precisely its original slot");
            Check(shop.StoreFurniture(small) && shop.BuyStoreExpansion() && shop.Model.Currency == 150, "Store display again and buy first west wing without refunding storage");
            yield return StoragePlace(null, ShopLayout.LargeDisplayId, -18, -3, new Vector3(-9, .2f, -4.5f));
            string large = shop.Layout.Items[2].Id; string[] slots = shop.Layout.GetSlotIds(large);
            Check(shop.Model.Currency == 110 && shop.Model.TankIds.Length == 4, "Large display adds only its two placed slots; stored small display remains inactive");
            yield return ExpansionTransfer("pondlet", slots[0]); yield return ExpansionTransfer("bubblebelly", slots[1]);
            string fish1 = shop.Model.GetTank(slots[0]).Value.Id, fish2 = shop.Model.GetTank(slots[1]).Value.Id;
            Check(shop.World.TryGetPlacedTable(large, out var stockedRoot), "Record live root before stocked relocation");
            yield return StoragePlace(large, null, -18, -1, new Vector3(-9, .2f, -3.8f));
            Check(shop.Model.Currency == 110 && shop.Model.GetTank(slots[0]).Value.Id == fish1 && shop.Model.GetTank(slots[1]).Value.Id == fish2 &&
                shop.World.TryGetPlacedTable(large, out var movedRoot) && movedRoot == stockedRoot, "Stocked move preserves both exact specimens, slot IDs, money and presentation root");
            for (int frame = 0; frame < 60; frame++) { if (!shop.World.ValidateTankBounds(out var reason)) throw new InvalidOperationException(reason); yield return null; }
            Check(true, "Both relocated residents remain within their glass compartments over 60 frames");
            Check(!shop.StoreFurniture(large) && !shop.RequestFurnitureSale(large) && shop.PendingFurnitureSaleId == null && shop.DisplayedStockCount() == 2, "Stocked display cannot be stored or sold; both creatures remain active");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("120-stocked-move-actions");
            yield return PlacementAimAt(-18, -3, new Vector3(-9, .2f, -4.5f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(large), "Start return preview with stocked display"); yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.SaveNow(), "Save prior stocked pose before deliberate route failure");
            var blocker = StorageEntryBlocker();
            try { yield return PlacementClick(); Check(shop.Layout.GetItem(large).Value.Z == -1 && shop.Model.Currency == 110 && shop.Model.GetTank(slots[0]).Value.Id == fish1 && shop.Model.GetTank(slots[1]).Value.Id == fish2, "Failed native stocked relocation restores old pose and exact stock without payment"); }
            finally { blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            shop.CancelPlacement(); Check(shop.ReloadSaved() && shop.Customers.Navigation.Ready, "Reload recovers routes after temporary move obstruction is removed");

            yield return StoragePlace(null, ShopLayout.TableDefinitionId, 9, 4, new Vector3(3, .2f, 1.6f));
            string table = shop.Layout.Items[3].Id;
            Check(shop.Model.Currency == 98 && shop.SaveNow(), "Buy work table and save before deliberate storage preflight failure");
            blocker = StorageEntryBlocker();
            try { Check(!shop.StoreFurniture(table) && !shop.Layout.GetItem(table).Value.Stored && shop.Model.Currency == 98 && shop.World.TryGetPlacedTable(table, out _), "Failed native storage preflight restores owned world piece and keeps cash"); }
            finally { blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            Check(shop.ReloadSaved() && shop.OpenPlacementMenu(), "Recover ordinary arrangement after fixture obstruction removal");
            Check(shop.RequestFurnitureSale(coral) && shop.Model.Currency == 98 && shop.Appeal.Points == 10, "Sale request shows confirmation without paying or removing appeal");
            yield return Capture("121-furniture-sale-confirmation");
            shop.CancelFurnitureSale(); Check(!shop.ConfirmFurnitureSale() && shop.Layout.GetItem(coral).HasValue && shop.Model.Currency == 98, "Cancel cannot sell or grant refund");
            Check(shop.RequestFurnitureSale(coral), "Open sale confirmation for Escape cancellation"); yield return CycleTap(UnityEngine.InputSystem.Key.Escape);
            Check(shop.PendingFurnitureSaleId == null && !shop.ConfirmFurnitureSale() && shop.Model.Currency == 98, "Escape clears stale sale confirmation");
            Check(shop.OpenPlacementMenu() && shop.RequestFurnitureSale(coral) && shop.ConfirmFurnitureSale() && !shop.ConfirmFurnitureSale() && shop.Model.Currency == 106 && !shop.Layout.GetItem(coral).HasValue && shop.Appeal.Points == 0, "Confirmed coral resale refunds exactly eight once and removes its ownership and appeal");
            yield return StoragePlace(null, ShopLayout.BubbleLampId, -4, -1, new Vector3(-2, .2f, -2.1f));
            string lamp = shop.Layout.Items[shop.Layout.Items.Count - 1].Id;
            Check(lamp == "table-5" && shop.Model.Currency == 82 && shop.Appeal.Points == 10 && shop.Layout.CaptureSave().nextItemNumber == 6, "Next purchase never reuses sold table1 identity or grants duplicate appeal");
            Check(shop.StoreFurniture(table) && shop.RequestFurnitureSale(table) && shop.ConfirmFurnitureSale() && shop.Model.Currency == 88 && !shop.Layout.GetItem(table).HasValue, "Selling stored work table refunds six once without reinstating its geometry");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Layout.GetItem(small).Value.Stored && shop.Model.GetTank(slots[0]).Value.Id == fish1 && shop.Model.GetTank(slots[1]).Value.Id == fish2 && shop.Model.Currency == 88, "Save with ownership gaps preserves stored piece, active stock and exact resale cash");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("122-owned-and-stored-furniture");

            shop.AllowValidationInput = false; yield return EnterCounter(); Check(shop.OpenForDay(), "Open shop after stocked relocation");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(.3f); var second = Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 40, "two relocated-display buyers reaching queue");
            Check(first.TargetTankId != second.TargetTankId && shop.Model.GetReservation(slots[0]).HasValue && shop.Model.GetReservation(slots[1]).HasValue, "Real customers find and reserve the two moved stock slots independently");
            Check(!shop.OpenPlacementMenu() && !shop.BeginPlacement(large) && !shop.StoreFurniture(large) && !shop.RequestFurnitureSale(large) && !shop.ConfirmFurnitureSale(), "Daytime and accepted orders block furniture transactions");
            yield return Capture("123-relocated-stock-orders");
            yield return ServeNext("First relocated display sale"); var remaining = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(remaining) == 0 && Flat(ActorPosition(remaining), shop.Customers.Navigation.QueueSpot(0)) < .5f, 12, "second relocated buyer advancing");
            yield return ServeNext("Second relocated display sale");
            Check(shop.Model.Currency == 104 && shop.DisplayedStockCount() == 0 && shop.Reputation.Points == 4 && !shop.ServeCustomer(), "Two moved specimens sell exactly once for sixteen total credits");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "relocated buyers exiting");
            Check(shop.CloseForNight(), "Close completed relocated-stock day"); yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "storage preparation night");
            Check(shop.Reports.Last.sales == 2 && shop.Reports.Last.revenue == 16 && shop.Reports.Last.pathProblems == 0, "Furniture resales are not creature sales or report income");
            Check(shop.OpenPlacementMenu() && shop.StoreFurniture(large) && shop.Model.TankIds.Length == 2 && shop.Layout.StoredCount == 2, "Now-empty large display stores safely and removes precisely two active slots");
            yield return StoragePlace(small, null, 3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.RequestFurnitureSale(small) && shop.ConfirmFurnitureSale() && !shop.RequestFurnitureSale(small) && shop.Model.Currency == 116 && shop.Model.TankIds.Length == 2, "Placed empty small display resells once and retires its slot");
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            Check(shop.SaveNow(), "Save final stored-display and stocked-original-tanks fixture"); string storedSave = File.ReadAllText(shop.SavePath);
            yield return StoragePlace(large, null, -18, -1, new Vector3(-9, .2f, -3.8f));
            Check(shop.Model.TankIds.Length == 4 && shop.World.TryGetPlacedTable(large, out _), "Temporarily redeploy same-pose large display before storage-state-only reload");
            File.WriteAllText(shop.SavePath, storedSave);
            Check(shop.ReloadSaved() && shop.Layout.GetItem(large).Value.Stored && shop.Model.TankIds.Length == 2 && !shop.World.TryGetPlacedTable(large, out _) && shop.DisplayedStockCount() == 2, "Reload compares storage state and removes same-pose geometry without losing original stock");
            Check(shop.Model.Currency == 116 && shop.Layout.Items.Count == 2 && shop.Layout.StoredCount == 1 && shop.Appeal.Points == 10 && shop.Layout.CaptureSave().nextItemNumber == 6, "Final ownership gaps, cash, stored capacity and appeal remain exact");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("124-final-stored-large-display"); Check(shop.SaveNow(), "Write final isolated fixture for separate-process restore");
        }

        GameObject StorageEntryBlocker()
        {
            var blocker = new GameObject("Storage validation temporary entry obstruction"); blocker.transform.SetParent(shop.World.transform, false);
            blocker.transform.position = shop.Customers.Navigation.EntryPoint + Vector3.up * .8f;
            blocker.AddComponent<BoxCollider>().size = new Vector3(3, 2, 2); Physics.SyncTransforms(); return blocker;
        }
        IEnumerator StoragePlace(string ownedId, string definition, int x, int z, Vector3 player)
        {
            shop.AllowValidationInput = true; yield return PlacementAimAt(x, z, player);
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(ownedId, definition ?? ShopLayout.TableDefinitionId), "Begin furniture placement / " + (ownedId ?? definition));
            yield return PlacementNeutral(); Check(shop.PlacementValid, "Furniture candidate validates / " + shop.PlacementMessage);
            yield return PlacementClick(); Check(!shop.PlacementActive && shop.SaveError == null, "Furniture placement commits and saves successfully");
        }
        IEnumerator FurnitureStorageRestart()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process restores isolated stored-furniture fixture through normal reload");
            Check(shop.Layout.Items.Count == 2 && shop.Layout.StoredCount == 1 && shop.Layout.GetItem("table-3").Value.Stored && !shop.Layout.GetItem("table-5").Value.Stored && shop.Layout.CaptureSave().nextItemNumber == 6, "Restore preserves exact ownership gaps and high-water identity counter");
            Check(shop.Model.Currency == 116 && shop.Model.TankIds.Length == 2 && shop.DisplayedStockCount() == 2 && shop.Reputation.Points == 4 && shop.Appeal.Points == 10, "Fresh process retains money, two original specimens, reputation and only active decor appeal");
            Check(!shop.World.TryGetPlacedTable("table-3", out _) && shop.World.TryGetPlacedTable("table-5", out _) && shop.Customers.Navigation.Ready && shop.PendingFurnitureSaleId == null, "Stored geometry stays absent, placed lamp is visible and no stale sale confirmation survives");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("125-storage-fresh-process"); Check(shop.SaveNow(), "Restored stored-furniture fixture writes current format safely");
        }
    }
}
#endif
