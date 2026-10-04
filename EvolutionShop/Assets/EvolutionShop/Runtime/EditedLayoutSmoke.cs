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
        float editedWalkingMetres;
        int editedDoorCrossings;

        /// <summary>A representative maximum-owned layout, not an exhaustive layout or human usability test.</summary>
        IEnumerator EditedLayoutScenario()
        {
            limitations.Add("R04.5 isolated fixture: 1000 seeded credits, accelerated recipe stock and application menu/storage/checkout commands. Purchases and moves use synthetic Input System floor aim/click/rotation. Setup/placement/transfer camera positions are explicit fixtures; no ordinary earnings or human GUI-click claim.");
            limitations.Add("One representative maximum-owned layout contains three displays, six decor and three lab work tables, with eight active slots. This is one legal arrangement at current ownership/capacity caps, not every possible layout. A continuous input-driven walking section has one initial teleport; subsequent translation and collision use the real CharacterController.");
            limitations.Add("Customer types and arrivals are explicitly selected through existing validation hooks. Real frames perform their navigation, browsing, reservations, two-place queue, payment and exit. Temporary invisible entry obstruction is a labelled native-preflight failure fixture.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Edited-layout scenario uses only its isolated evidence slot");
            yield return new WaitForSeconds(1);
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors == 2 && !shop.Customers.UseBaseReputationEffectsForValidation,
                "Edited-layout startup uses ready native navigation, ordinary demand effects and the two-visitor cap");
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionEditedLayoutKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionEditedLayoutMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.AllowValidationInput = true; shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionEditedLayoutReload") != null) { yield return EditedLayoutRestartScenario(); yield break; }

            Check(shop.Layout.Items.Count == 0 && shop.SaveNow(), "Save fresh isolated shop before cash fixture");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency = 1000;
            Check(SaveStore.Write(shop.SavePath, fixture, out var problem) && shop.ReloadSaved(), "Load labelled 1000-credit construction fixture / " + problem);
            Check(shop.OpenPlacementMenu() && shop.BuyStoreExpansion() && shop.BuyStoreExpansion(), "Buy both west wings through normal application transactions");
            yield return EditedPlace(null, ShopLayout.LargeDisplayId, -4, -1, 0, new Vector3(-2, .2f, -2.3f));
            yield return EditedPlace(null, ShopLayout.LargeDisplayId, -18, -3, 0, new Vector3(-9, .2f, -4.5f));
            yield return EditedPlace(null, ShopLayout.LargeDisplayId, -26, -3, 0, new Vector3(-13, .2f, -4.5f));
            yield return EditedPlace(null, ShopLayout.TableDefinitionId, 9, 4, 1, new Vector3(3.5f, .2f, 3));
            yield return EditedPlace(null, ShopLayout.TableDefinitionId, 6, 3, 1, new Vector3(1.5f, .2f, 1.5f));
            yield return EditedPlace(null, ShopLayout.TableDefinitionId, 11, 3, 1, new Vector3(6.4f, .2f, 2.8f));
            yield return EditedPlace(null, ShopLayout.CoralPlanterId, 10, 8, 0, new Vector3(3.5f, .2f, 3));
            yield return EditedPlace(null, ShopLayout.CoralPlanterId, 0, 8, 0, new Vector3(0, .2f, 2.9f));
            yield return EditedPlace(null, ShopLayout.CoralPlanterId, -28, -8, 0, new Vector3(-14, .2f, -2.8f));
            yield return EditedPlace(null, ShopLayout.BubbleLampId, -23, -8, 0, new Vector3(-11.5f, .2f, -2.8f));
            yield return EditedPlace(null, ShopLayout.SillyStatueId, -20, -8, 0, new Vector3(-10, .2f, -2.8f));
            yield return EditedPlace(null, ShopLayout.BubbleLampId, -17, -8, 0, new Vector3(-8.5f, .2f, -2.8f));
            Check(shop.Layout.StoreTier == 2 && shop.Layout.Items.Count == 12 && shop.Layout.DecorCount == 6 &&
                shop.Layout.StoredCount == 0 && shop.Model.TankIds.Length == 8 && shop.Model.Currency == 536 && shop.Appeal.Points == 30,
                "Representative maximum-owned fixture has 3 displays, 3 lab tables, 6 decor, 8 slots, 30 appeal and exact 536 remaining credits");
            EditedCheckWorld("Maximum-owned layout");
            EditedCheckRoutes("Maximum-owned layout");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("130-max-owned-layout-actions");

            // A single starting teleport; the following route contains no position writes.
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = true;
            shop.Player.Teleport(new Vector3(0, .2f, -3.8f), Quaternion.identity); yield return PlacementNeutral();
            yield return EditedWalkTo(new Vector2(0, 2.35f), "store through doorway into furnished lab");
            yield return EditedWalkTo(new Vector2(-4.2f, 2.35f), "Bloom grower approach");
            yield return EditedWalkTo(new Vector2(-1.4f, 2.35f), "Flow grower approach");
            yield return EditedWalkTo(new Vector2(0, 2.35f), "lab central access");
            yield return EditedWalkTo(new Vector2(2, 2.35f), "incubator approach beside dense lab tables");
            yield return EditedWalkTo(new Vector2(1.5f, 2.35f), "clear side of lab worktable");
            yield return EditedWalkTo(new Vector2(1.5f, 1.5f), "worktable collision approach");
            shop.AllowValidationInput = true; shop.Player.transform.rotation = Quaternion.identity;
            float beforeCollision = shop.Player.transform.position.x;
            yield return MoveFor(Key.D, .9f);
            Check(shop.Player.transform.position.x > beforeCollision + .25f && shop.Player.transform.position.x < 2.3f &&
                Mathf.Abs(shop.Player.transform.position.z - 1.5f) < .3f,
                "Actual CharacterController advances to the lab table but cannot walk through its solid footprint");
            yield return EditedWalkTo(new Vector2(1.5f, 1.5f), "back away from worktable");
            yield return EditedWalkTo(new Vector2(0, 1.5f), "return to lab aisle");
            yield return EditedWalkTo(new Vector2(0, -1.2f), "return through doorway to original store");
            yield return EditedWalkTo(new Vector2(0, -4.9f), "original store front aisle");
            yield return EditedWalkTo(new Vector2(-7, -4.9f), "pass behind fixed counter to west expansion");
            yield return EditedWalkTo(new Vector2(-7, -2.8f), "first gallery approach around its decor row");
            yield return EditedWalkTo(new Vector2(-9, -2.8f), "first gallery display front");
            yield return EditedWalkTo(new Vector2(-13.4f, -2.8f), "second gallery display front");
            shop.AllowValidationInput = false; shop.Player.View.transform.LookAt(new Vector3(-10.8f, 1.15f, -1.5f));
            yield return Capture("131-edited-layout-gallery-route");
            yield return EditedWalkTo(new Vector2(-7, -2.8f), "return across both furnished galleries");
            yield return EditedWalkTo(new Vector2(-7, -4.9f), "return behind counter");
            yield return EditedWalkTo(new Vector2(0, -4.9f), "return to original front entrance");
            yield return EditedWalkTo(new Vector2(0, -1.2f), "finish in original central aisle");
            Check(editedWalkingMetres > 50 && editedDoorCrossings >= 2, "Continuous input route covers both rooms and wings / " +
                editedWalkingMetres.ToString("0.0") + " metres, " + editedDoorCrossings + " doorway crossings");

            string initialLayout = JsonUtility.ToJson(shop.Layout.CaptureSave()); int cash = shop.Model.Currency;
            yield return PlacementAimAt(0, 1, new Vector3(0, .2f, -1.2f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement("table-5"), "Start existing-table doorway obstruction preview");
            yield return PlacementNeutral();
            Check(!shop.PlacementValid, "Doorway obstruction is visibly invalid before commit");
            yield return Capture("132-doorway-placement-rejected"); yield return PlacementClick();
            Check(shop.PlacementActive && JsonUtility.ToJson(shop.Layout.CaptureSave()) == initialLayout && shop.Model.Currency == cash,
                "Rejected real click cannot move the table, charge money or block the doorway");
            yield return PlacementClick(true); Check(!shop.PlacementActive && shop.Customers.Navigation.Ready, "Cancel rejected doorway preview without damaging routes");

            yield return PlacementAimAt(-26, -3, new Vector3(-13, .2f, -2.8f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement("table-3"), "Start far-gallery display orientation preview");
            yield return PlacementNeutral(); Check(shop.PlacementValid, "Existing far-gallery display orientation keeps its browse side reachable");
            yield return CycleTap(Key.R); yield return CycleTap(Key.R);
            Check(shop.PlacementRotation == 2 && !shop.PlacementValid, "Half-turn with the same rectangular footprint rejects an inaccessible browse side");
            yield return PlacementClick();
            Check(JsonUtility.ToJson(shop.Layout.CaptureSave()) == initialLayout && shop.Model.Currency == cash,
                "Rejected display half-turn preserves original orientation, money and access");
            yield return PlacementClick(true);

            // Only the statue is unique in the store; duplicate corals and lamps must not hide this change.
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Check(shop.World.TryGetPlacedTable("table-11", out var oldStatue) && shop.OpenOwnedFurnitureMenu() && shop.StoreFurniture("table-11"), "Store sole statue / repetition " + cycle);
                bool anyCollider = false; foreach (var collider in oldStatue.GetComponentsInChildren<Collider>(true)) anyCollider |= collider.enabled;
                Check(!oldStatue.gameObject.activeSelf && !anyCollider && shop.Layout.StoredCount == 1 && shop.Appeal.Points == 20 && shop.Model.Currency == cash,
                    "Storage immediately removes collision and exactly one appeal type without money or ownership change / " + cycle);
                if (cycle == 1) Check(shop.SaveNow() && shop.ReloadSaved() && shop.Layout.GetItem("table-11").Value.Stored && shop.Appeal.Points == 20,
                    "Intermediate stored-state save reload does not restore hidden appeal");
                yield return EditedPlace("table-11", null, -20, -8, 0, new Vector3(-10, .2f, -2.8f));
                Check(shop.Layout.Items.Count == 12 && shop.Layout.StoredCount == 0 && shop.Appeal.Points == 30 && shop.Model.Currency == cash,
                    "Redeployment restores the same identity and capped appeal exactly once / " + cycle);
            }
            Check(shop.Appeal.Points == 30 && shop.Appeal.BudgetBonus == 3 && shop.Customers.NextArrivalSeconds == 5.25f,
                "Three real storage/redeployment cycles leave the exact capped appeal and arrival benefit");

            string[] largeSlots = shop.Layout.GetSlotIds("table-2");
            yield return ExpansionTransfer("pondlet", largeSlots[0]); yield return ExpansionTransfer("bubblebelly", largeSlots[1]);
            string fishA = shop.Model.GetTank(largeSlots[0]).Value.Id, fishB = shop.Model.GetTank(largeSlots[1]).Value.Id;
            Check(shop.World.TryGetPlacedTable("table-2", out var stockRoot), "Record original dense-display presentation root");
            yield return EditedPlace("table-2", null, -18, -1, 0, new Vector3(-9, .2f, -2.9f));
            Check(shop.World.TryGetPlacedTable("table-2", out var movedRoot) && stockRoot == movedRoot &&
                shop.Model.GetTank(largeSlots[0]).Value.Id == fishA && shop.Model.GetTank(largeSlots[1]).Value.Id == fishB && shop.Model.Currency == cash,
                "Stocked relocation retains both exact specimens and live presentation root without payment");
            Check(shop.SaveNow(), "Save dense layout with relocated stocked display"); string movedSave = File.ReadAllText(shop.SavePath);
            yield return EditedPlace("table-2", null, -18, -3, 0, new Vector3(-9, .2f, -2.9f));
            File.WriteAllText(shop.SavePath, movedSave);
            Check(shop.ReloadSaved() && shop.Layout.GetItem("table-2").Value.Z == -1 &&
                shop.Model.GetTank(largeSlots[0]).Value.Id == fishA && shop.Model.GetTank(largeSlots[1]).Value.Id == fishB && shop.DisplayedStockCount() == 2,
                "Changed-pose reload restores exact stocked display and both unique specimens");

            Check(shop.OpenOwnedFurnitureMenu() && shop.SaveNow(), "Save authoritative dense state before native obstruction fixture");
            var blocker = StorageEntryBlocker();
            try
            {
                Check(!shop.StoreFurniture("table-11") && !shop.Layout.GetItem("table-11").Value.Stored && shop.Layout.Items.Count == 12 &&
                    shop.Model.Currency == cash && shop.Appeal.Points == 30 && shop.Model.GetTank(largeSlots[0]).Value.Id == fishA,
                    "Native preflight rejection rolls back removed decor, appeal and exact stocked authority");
                Check(shop.World.TryGetPlacedTable("table-11", out var restoredStatue) && restoredStatue.gameObject.activeSelf,
                    "Rejected removal restores visible owned geometry immediately");
            }
            finally { foreach (var collider in blocker.GetComponentsInChildren<Collider>()) collider.enabled = false; blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            Check(shop.ReloadSaved() && shop.Customers.Ready && shop.OpenOwnedFurnitureMenu(), "Ordinary reload recovers editing and native routes after temporary obstruction removal");
            EditedCheckWorld("Recovered dense layout"); EditedCheckRoutes("Recovered dense layout");
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false;
            AimAt(new Vector3(-7, .2f, -2.8f), new Vector3(-9, 1.15f, -.5f)); yield return Capture("133-dense-stocked-relocation");

            yield return EnterCounter(); Check(shop.OpenForDay(), "Open dense edited shop for real actor service");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(.3f); var second = Spawn(CustomerArchetypes.Collector);
            Check(!shop.Customers.SpawnNow() && shop.Customers.ActiveCount == 2 && shop.Reports.Current.visitors == 2,
                "Third arrival is refused without exceeding two visitors or inflating report counts");
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 45, "two dense-layout buyers reaching physical queue");
            Check(first.TargetTankId != second.TargetTankId && shop.Model.GetReservation(largeSlots[0]).HasValue && shop.Model.GetReservation(largeSlots[1]).HasValue,
                "Two visitors navigate edited galleries and reserve separate relocated compartments");
            Check(shop.Customers.QueuePlaceOf(first) != shop.Customers.QueuePlaceOf(second) &&
                Flat(ActorPosition(first), shop.Customers.Navigation.QueueSpot(shop.Customers.QueuePlaceOf(first))) < .5f &&
                Flat(ActorPosition(second), shop.Customers.Navigation.QueueSpot(shop.Customers.QueuePlaceOf(second))) < .5f,
                "Both queue reservations correspond to actors physically at distinct queue positions");
            Check(!shop.OpenPlacementMenu() && !shop.StoreFurniture("table-11"), "Open trading and accepted orders keep editing disabled");
            yield return Capture("134-dense-two-customer-queue");
            yield return ServeNext("First dense-layout buyer"); var remaining = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(remaining) == 0 && Flat(ActorPosition(remaining), shop.Customers.Navigation.QueueSpot(0)) < .5f, 12, "second dense-layout buyer physically advancing");
            yield return ServeNext("Second dense-layout buyer");
            Check(shop.Model.Currency == cash + 16 && shop.DisplayedStockCount() == 0 && !shop.ServeCustomer(), "Two exact dense-layout specimens pay sixteen once with no extra checkout");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "dense-layout buyers exiting through edited store");
            Check(shop.CloseForNight(), "Close dense-layout day after real exits");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "dense-layout report");
            var report = shop.Reports.Last;
            Check(report.visitors == 2 && report.sales == 2 && report.revenue == 16 && report.pathProblems == 0 && report.stockedAtOpen == 2 && report.stockedAtClose == 0,
                "Dense edited-layout day report reconciles two visitors, two sales, sixteen income and zero path problems");
            yield return Capture("135-dense-layout-day-report");

            string[] species = { "pondlet", "bubblebelly", "ribbonfin" }; string[] tanks = shop.Model.TankIds;
            for (int i = 0; i < tanks.Length; i++) SeedStock(species[i % species.Length], tanks[i]);
            var finalSpecimens = new string[tanks.Length];
            var uniqueSpecimens = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < tanks.Length; i++) { finalSpecimens[i] = shop.Model.GetTank(tanks[i]).Value.Id; uniqueSpecimens.Add(finalSpecimens[i]); }
            for (int frame = 0; frame < 120; frame++)
            {
                if (!shop.World.ValidateTankBounds(out var bounds)) throw new InvalidOperationException(bounds);
                yield return new WaitForSeconds(.02f);
            }
            Check(shop.DisplayedStockCount() == 8 && uniqueSpecimens.Count == 8, "All eight original/large slots hold unique seeded stock; all three silhouettes remain inside glass over animated frames");
            EditedCheckRoutes("Fully stocked final layout");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency == 552 && shop.Reputation.Points == 4 && shop.Appeal.Points == 30 && shop.DisplayedStockCount() == 8,
                "Final dense layout roundtrip retains exact cash, reputation, capped appeal and all eight specimens");
            for (int i = 0; i < tanks.Length; i++) Check(shop.Model.GetTank(tanks[i]).Value.Id == finalSpecimens[i], "Final reload preserves exact specimen identity / " + tanks[i]);
            shop.OpenOwnedFurnitureMenu(); yield return Capture("136-final-max-owned-stocked-layout");
            Check(shop.SaveNow(), "Write final isolated edited-layout slot for separate-process restart");
            File.Copy(shop.SavePath, Path.Combine(evidence, "edited-layout-final-save.json"), true);
            File.WriteAllText(Path.Combine(evidence, "edited-layout-route.txt"), "Input walking metres=" + editedWalkingMetres.ToString("0.00") +
                "\nDoorway crossings=" + editedDoorCrossings + "\nOne starting teleport; no position writes during the continuous walking section.\n");
        }

        IEnumerator EditedPlace(string ownedId, string definition, int x, int z, int turns, Vector3 player)
        {
            shop.AllowValidationInput = true; yield return PlacementAimAt(x, z, player);
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(ownedId, definition ?? ShopLayout.TableDefinitionId), "Begin edited-layout placement / " + (ownedId ?? definition));
            yield return PlacementNeutral();
            for (int i = 0; i < 4 && shop.PlacementRotation != turns; i++) yield return CycleTap(Key.R);
            Check(shop.PlacementX == x && shop.PlacementZ == z && shop.PlacementRotation == turns && shop.PlacementValid,
                "Exact edited-layout candidate " + x + "," + z + " / rotation " + turns + " / " + shop.PlacementMessage);
            yield return PlacementClick();
            Check(!shop.PlacementActive && shop.SaveError == null, "Real placement click commits edited layout and saves");
        }

        IEnumerator EditedWalkTo(Vector2 target, string label)
        {
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = true;
            shop.Player.transform.rotation = Quaternion.identity; // Automated facing only; all translation is keyboard input.
            float deadline = Time.realtimeSinceStartup + 15;
            while (true)
            {
                Vector3 before = shop.Player.transform.position;
                Vector2 delta = target - new Vector2(before.x, before.z);
                if (delta.magnitude <= .14f) break;
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Edited-layout walk blocked / " + label + " / " + before + " -> " + target);
                Key horizontal = delta.x > 0 ? Key.D : Key.A, vertical = delta.y > 0 ? Key.W : Key.S;
                KeyboardState state = Mathf.Abs(delta.x) <= .08f ? new KeyboardState(vertical) :
                    Mathf.Abs(delta.y) <= .08f ? new KeyboardState(horizontal) : new KeyboardState(horizontal, vertical);
                syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
                InputSystem.QueueStateEvent(syntheticKeyboard, state); InputSystem.QueueStateEvent(cycleMouse, new MouseState());
                yield return null;
                Vector3 after = shop.Player.transform.position;
                editedWalkingMetres += Flat(before, after);
                if (before.z <= .5f && after.z > .5f || before.z >= .5f && after.z < .5f) editedDoorCrossings++;
                if (after.y < -.2f || after.y > .5f) throw new InvalidOperationException("Player left the floor on edited route: " + after);
            }
            yield return PlacementNeutral();
            Check(Flat(shop.Player.transform.position, new Vector3(target.x, 0, target.y)) < .25f, "Actual input walk reaches " + label);
        }

        void EditedCheckRoutes(string label)
        {
            var nav = shop.Customers.Navigation;
            foreach (string slot in shop.Model.TankIds)
                for (int spot = 0; spot < 2; spot++)
                    Check(nav.CanReach(nav.EntryPoint, nav.BrowseSpot(slot, spot)) && nav.CanReach(nav.BrowseSpot(slot, spot), nav.QueueSpot(0)) &&
                        nav.CanReach(nav.BrowseSpot(slot, spot), nav.QueueSpot(1)), label + " native entry/browse/both-queue routes / " + slot + " / " + spot);
            Check(!nav.HasNavigationAt(new Vector3(0, 0, 1), .1f) && !nav.HasNavigationAt(new Vector3(-13, 0, 1), .1f) &&
                nav.CanReach(nav.EntryPoint, new Vector3(-9, 0, -2.5f)) && nav.CanReach(nav.EntryPoint, new Vector3(-13, 0, -2.5f)),
                label + " keeps both galleries reachable and all back rooms outside the customer bake");
        }

        void EditedCheckWorld(string label)
        {
            Check(shop.World.GetComponents<Unity.AI.Navigation.NavMeshSurface>().Length == 1 &&
                shop.World.GetComponentsInChildren<PlacedWorkTable>().Length == 12 && shop.World.Stations.Count == 13,
                label + " has one native surface, twelve active owned roots and exactly thirteen interaction stations");
            foreach (var item in shop.Layout.Items)
                Check(!item.Stored && shop.World.TryGetPlacedTable(item.Id, out var root) && root.GetComponentsInChildren<Collider>().Length > 0,
                    label + " retains active collision and stable identity / " + item.Id);
        }

        IEnumerator EditedLayoutRestartScenario()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process restores supplied isolated maximum-owned layout through normal reload");
            Check(shop.Layout.StoreTier == 2 && shop.Layout.Items.Count == 12 && shop.Layout.StoredCount == 0 && shop.Layout.DecorCount == 6 &&
                shop.Model.TankIds.Length == 8 && shop.DisplayedStockCount() == 8 && shop.Model.Currency == 552 && shop.Appeal.Points == 30 && shop.Reputation.Points == 4,
                "Fresh process restores twelve owned pieces, eight stocks, exact 552 cash and separate progression");
            Check(shop.Layout.GetItem("table-2").Value.Z == -1 && shop.Layout.CaptureSave().nextItemNumber == 13 && shop.Reports.Last.sales == 2 && shop.Reports.Last.revenue == 16,
                "Fresh process preserves relocated pose, high-water identity counter and completed report without replay");
            EditedCheckWorld("Fresh-process edited layout"); EditedCheckRoutes("Fresh-process edited layout");
            for (int frame = 0; frame < 60; frame++)
            { if (!shop.World.ValidateTankBounds(out var error)) throw new InvalidOperationException(error); yield return null; }
            Check(true, "Fresh-process stock visuals remain inside all eight compartments");
            shop.OpenOwnedFurnitureMenu(); yield return Capture("137-edited-layout-fresh-process");
            Check(shop.SaveNow(), "Fresh-process restored edited layout writes valid current isolated save");
        }
    }
}
#endif
