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
        IEnumerator PlacementScenario()
        {
            limitations.Add("R04.1 technical fixture: an isolated save seeds 60 credits, stock uses accelerated recipe commands, and teleports position the player for bounded checks. Menu/open/serve/save actions use application commands. Placement aim, rotate, cancel, purchase and move use synthetic Input System mouse/keyboard events; this is not a native GUI click or ordinary earning/pacing playtest.");
            limitations.Add("Two explicitly spawned customer archetypes use real navigation, browse, queue and exit frames after furniture changes. No appeal, capacity, expansion, storage or refund feature is claimed.");
            limitations.Add("A temporary invisible entry collider deliberately invalidates one purchase NavMesh bake; it is disabled and removed before recovering through normal save reload.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Placement scenario uses only the isolated evidence save");
            yield return new WaitForSeconds(1);
            Check(shop.Customers.Ready && shop.Layout.Items.Count == 0 && shop.Model.Currency == 0 && shop.Cycle.Phase == ShopDayPhase.Night,
                "Fresh night starts with zero credits, no work tables and valid customer navigation");

            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionPlacementKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionPlacementMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.AllowValidationInput = true;
            shop.Customers.ArrivalsEnabled = false;
            shop.Customers.MaxVisitors = 2;
            shop.Customers.ForcedArchetypeId = null;
            shop.Customers.UseBaseReputationEffectsForValidation = false;

            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(), "Commands open the first worktable preview without buying");
            yield return PlacementNeutral();
            Check(shop.PlacementActive && shop.PlacementX == 3 && shop.PlacementZ == -5 && !shop.PlacementValid && shop.PlacementCost == 12,
                "Real mouse floor aim previews the requested store grid but rejects zero funds / " + shop.PlacementMessage);
            yield return Capture("90-placement-no-funds");
            yield return PlacementClick();
            Check(shop.Model.Currency == 0 && shop.Layout.Items.Count == 0 && shop.PlacementActive,
                "Left click with zero funds creates no table and charges nothing");
            yield return CycleTap(Key.R);
            Check(shop.PlacementRotation == 1 && shop.Layout.Items.Count == 0, "R input rotates a preview without buying");
            yield return CycleTap(Key.Escape);
            Check(!shop.PlacementActive && shop.Panel == ShopPanel.Arrange && shop.Layout.Items.Count == 0,
                "Escape cancels placement into arrangement without also pausing");

            Check(shop.SaveNow(), "Save the initial isolated state before a labelled credit fixture");
            var slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            slot.game.currency = 60;
            Check(SaveStore.Write(shop.SavePath, slot, out var fixtureError) && shop.ReloadSaved() && shop.Model.Currency == 60,
                "Load labelled isolated 60-credit placement fixture / " + fixtureError);

            // A boundary preview is legal lengthwise but would overlap the counter after a quarter turn.
            yield return PlacementAimAt(-12, -8, new Vector3(-4.2f, .2f, -4.2f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(), "Open boundary rotation preview");
            yield return PlacementNeutral();
            Check(shop.PlacementX == -12 && shop.PlacementZ == -8 && shop.PlacementValid, "Boundary preview is valid before rotation / " + shop.PlacementMessage);
            yield return CycleTap(Key.R);
            Check(shop.PlacementRotation == 1 && !shop.PlacementValid, "R input exposes a blocked rotated footprint / " + shop.PlacementMessage);
            yield return Capture("91-placement-blocked-rotation");
            yield return PlacementClick();
            Check(shop.Layout.Items.Count == 0 && shop.Model.Currency == 60 && shop.PlacementActive,
                "Blocked rotated footprint rejects click with no debit or live object");
            yield return PlacementClick(true);
            Check(!shop.PlacementActive && shop.Panel == ShopPanel.Arrange, "Right click cancels the blocked preview");

            // An artificial collision source isolates the native purchase-preflight failure path.
            Check(shop.BeginPlacement(), "Open native-navigation rejection preview");
            yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.SaveNow(), "Save unchanged ownership before the temporary entry-blocker fixture");
            var entryBlocker = new GameObject("Validation only / purchase preflight entry blocker");
            entryBlocker.transform.SetParent(shop.World.transform, false);
            entryBlocker.transform.position = shop.Customers.Navigation.EntryPoint + Vector3.up * .8f;
            var entryCollider = entryBlocker.AddComponent<BoxCollider>();
            entryCollider.size = new Vector3(3, 2, 2);
            Physics.SyncTransforms();
            int beforeRejectedBake = shop.Customers.Navigation.BuildCount;
            try
            {
                yield return PlacementClick();
                Check(shop.Model.Currency == 60 && shop.Layout.Items.Count == 0 && !shop.PlacementValid &&
                    shop.World.GetComponentsInChildren<PlacedWorkTable>().Length == 0 &&
                    shop.Customers.Navigation.BuildCount > beforeRejectedBake && !string.IsNullOrEmpty(shop.Customers.Navigation.LastError),
                    "Actual failed purchase NavMesh preflight removes its candidate and preserves all credits and ownership / " + shop.Customers.Navigation.LastError);
            }
            finally
            {
                entryCollider.enabled = false; entryBlocker.SetActive(false); Destroy(entryBlocker);
                Physics.SyncTransforms();
            }
            shop.CancelPlacement();
            Check(shop.ReloadSaved() && shop.Customers.Ready && shop.Model.Currency == 60 && shop.Layout.Items.Count == 0,
                "Removing the blocker and reloading restores valid routes without a debit or orphaned furniture");

            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenPlacementMenu(), "Open legal store arrangement menu");
            cycleMouse.MakeCurrent();
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 });
            yield return null; yield return null;
            Check(cycleMouse.leftButton.isPressed && shop.BeginPlacement(), "Open store preview while the opening left click is still held");
            yield return null; yield return null;
            Check(shop.PlacementActive && shop.Layout.Items.Count == 0 && shop.Model.Currency == 60,
                "A held opening mouse button cannot buy a table");
            yield return PlacementNeutral();
            Check(shop.PlacementActive && shop.Layout.Items.Count == 0 && shop.Model.Currency == 60,
                "Releasing the opening mouse button arms placement without purchasing");
            Check(shop.PlacementValid, "Store preview accepts available funds and reachable floor / " + shop.PlacementMessage);
            var ghost = shop.World.transform.Find("Worktable placement preview (no colliders)");
            Check(ghost && ghost.gameObject.activeSelf && ghost.GetComponentsInChildren<Collider>(true).Length == 0,
                "The visible ghost has zero Collider components, including inactive children");
            yield return Capture("92-placement-store-preview");
            int beforeBake = shop.Customers.Navigation.BuildCount;
            yield return PlacementClick();
            Check(shop.Layout.Items.Count == 1 && shop.Model.Currency == 48 && !shop.PlacementActive && !ghost.gameObject.activeSelf,
                "Input purchase commits exactly one store table for 12 credits and hides preview");
            Check(shop.Customers.Navigation.BuildCount > beforeBake && shop.Customers.Ready,
                "Store placement rebuilt and validated real navigation");
            string storeId = shop.Layout.Items[0].Id;
            Check(shop.World.TryGetPlacedTable(storeId, out var storeRoot) && Mathf.Abs(storeRoot.localPosition.x - 1.5f) < .001f &&
                Mathf.Abs(storeRoot.localPosition.z + 2.5f) < .001f, "Committed store grid becomes the correct metre position");
            var tableCollider = storeRoot.GetComponent<BoxCollider>();
            Check(tableCollider && tableCollider.enabled && tableCollider.size == new Vector3(1.5f, .96f, 1f) &&
                tableCollider.GetComponentInParent<PlacedWorkTable>().Id == storeId, "Owned table collider has the exact footprint and selectable stable identity");

            shop.SetPanel(ShopPanel.None);
            shop.Player.Teleport(new Vector3(1.5f, .2f, -4.2f), Quaternion.identity);
            yield return PlacementNeutral();
            yield return MoveFor(Key.W, .9f);
            Check(shop.Player.transform.position.z > -4f && shop.Player.transform.position.z < -3.15f,
                "Real WASD CharacterController stops at the new store table / " + shop.Player.transform.position);
            yield return Capture("93-placement-table-collision");

            yield return PlacementAimAt(9, 4, new Vector3(2.75f, .2f, 1.3f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(), "Open legal lab preview");
            yield return CycleTap(Key.R);
            Check(shop.PlacementX == 9 && shop.PlacementZ == 4 && shop.PlacementRotation == 1 && shop.PlacementValid,
                "Mouse aim plus R previews a quarter-turned table inside the lab / " + shop.PlacementMessage);
            yield return Capture("94-placement-lab-preview");
            yield return PlacementClick();
            Check(shop.Layout.Items.Count == 2 && shop.Model.Currency == 36, "Lab purchase adds one table and debits 12 once");
            string labId = shop.Layout.Items[1].Id;
            Check(shop.World.TryGetPlacedTable(labId, out var labRoot) && Mathf.Abs(labRoot.localPosition.x - 4.5f) < .001f &&
                Mathf.Abs(labRoot.localPosition.z - 2f) < .001f && Quaternion.Angle(labRoot.localRotation, Quaternion.Euler(0, 90, 0)) < .01f,
                "Lab collider and geometry retain the committed quarter turn");
            Check(shop.SaveNow(), "Save the two-room furniture layout");
            string beforeMoveSave = File.ReadAllText(shop.SavePath);

            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(storeId), "Preview the owned table at its existing footprint");
            yield return PlacementNeutral();
            Check(shop.PlacementX == 3 && shop.PlacementZ == -5 && shop.PlacementValid && shop.PlacementCost == 0,
                "The selected table's own collider does not obstruct its floor aim / " + shop.PlacementMessage);
            Vector3 unchangedStorePosition = storeRoot.localPosition;
            Quaternion unchangedStoreRotation = storeRoot.localRotation;
            yield return CycleTap(Key.R);
            Check(shop.PlacementRotation == 1 && shop.PlacementValid, "R previews a legal in-place rotation over the selected owned table");
            yield return CycleTap(Key.Escape);
            Check(!shop.PlacementActive && shop.Model.Currency == 36 && shop.Layout.Items.Count == 2 &&
                storeRoot.localPosition == unchangedStorePosition && storeRoot.localRotation == unchangedStoreRotation &&
                shop.Layout.GetItem(storeId).Value.QuarterTurns == 0,
                "Escape from an in-place rotation keeps the original pose, ownership and credits");
            Check(shop.BeginPlacement(storeId), "Open an owned-table preview before the pause lifecycle probe");
            yield return PlacementNeutral();
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.Paused && !shop.PlacementActive && !ghost.gameObject.activeSelf && shop.Model.Currency == 36 &&
                storeRoot.localPosition == unchangedStorePosition && storeRoot.localRotation == unchangedStoreRotation,
                "Opening Pause cancels the active preview and keeps the old table and credits");
            shop.SetPanel(ShopPanel.None);
            yield return PlacementNeutral();
            Check(!shop.Paused && !shop.PlacementActive && !ghost.gameObject.activeSelf,
                "Resume leaves the cancelled placement inactive with no ghost");

            yield return PlacementAimAt(-4, -2, new Vector3(-2f, .2f, -3f));
            Check(shop.OpenPlacementMenu() && shop.BeginPlacement(storeId), "Open owned-table move preview");
            yield return CycleTap(Key.R);
            Check(shop.PlacementValid && shop.PlacementCost == 0 && shop.PlacementRotation == 1,
                "Owned-table move previews a legal rotated position for free / " + shop.PlacementMessage);
            Vector3 originalStorePosition = storeRoot.localPosition;
            Check(Mathf.Abs(originalStorePosition.x - 1.5f) < .001f, "Owned table stays at its old position while previewing a move");
            yield return PlacementClick();
            Check(shop.Layout.Items.Count == 2 && shop.Model.Currency == 36 && shop.World.TryGetPlacedTable(storeId, out var movedRoot) &&
                movedRoot == storeRoot && Mathf.Abs(movedRoot.localPosition.x + 2f) < .001f && Mathf.Abs(movedRoot.localPosition.z + 1f) < .001f,
                "Input move reuses the same owned object at its new grid without charge or duplication");
            yield return Capture("95-placement-owned-moved");

            // Reload different saved geometry to exercise ApplyLayout and a fresh native bake, not just an identical snapshot.
            File.WriteAllText(shop.SavePath, beforeMoveSave);
            Check(shop.ReloadSaved() && shop.Layout.Items.Count == 2 && shop.Model.Currency == 36 &&
                shop.World.TryGetPlacedTable(storeId, out var restoredRoot) && restoredRoot.localPosition == originalStorePosition &&
                shop.Layout.GetItem(labId).Value.QuarterTurns == 1, "Reload restores the saved two-room positions, rotation, IDs and cash after a different live move");
            Check(shop.SaveNow() && shop.ReloadSaved(), "Saved restored layout round-trips through normal save/reload");

            shop.SetPanel(ShopPanel.None);
            shop.Player.Teleport(new Vector3(0, .2f, -1.2f), Quaternion.identity);
            yield return PlacementNeutral();
            yield return MoveFor(Key.W, 1f);
            Check(shop.Player.transform.position.z > 1.5f, "WASD crosses the protected doorway into the furnished lab");
            yield return MoveFor(Key.S, 1f);
            Check(shop.Player.transform.position.z < -.8f, "WASD returns through the protected doorway into the furnished store");

            var nav = shop.Customers.Navigation;
            Check(shop.World.GetComponents<NavMeshSurface>().Length == 1 && nav.Ready,
                "Purchases, move and geometry reload retain exactly one active navigation surface");
            foreach (string tank in new[] { "tank-a", "tank-b" })
                for (int spot = 0; spot < 2; spot++)
                    Check(nav.CanReach(nav.EntryPoint, nav.BrowseSpot(tank, spot)) && nav.CanReach(nav.BrowseSpot(tank, spot), nav.QueueSpot(0)) &&
                        nav.CanReach(nav.BrowseSpot(tank, spot), nav.QueueSpot(1)), "Both queue routes remain complete from " + tank + " browse spot " + spot);
            Check(!nav.HasNavigationAt(new Vector3(0, 0, 1), .1f) && !nav.CanReach(nav.EntryPoint, new Vector3(0, 0, 2)),
                "Furniture rebakes keep the entire lab unavailable to customers");

            shop.AllowValidationInput = false;
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the shop with the saved furniture layout");
            Check(!shop.OpenPlacementMenu() && !shop.PlacementActive && shop.Layout.Items.Count == 2,
                "Daytime placement is rejected without changing furniture");
            var first = Spawn(CustomerArchetypes.Casual);
            yield return Hold(1f);
            var second = Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 35, "two visitors reaching the furnished-store queue");
            Check(shop.Customers.WaitingCount == 2 && shop.Model.GetReservation("tank-a").HasValue && shop.Model.GetReservation("tank-b").HasValue,
                "Two real visitors browse, reserve separate stock and queue around the worktable");
            yield return WaitFor(() => Flat(ActorPosition(first), nav.QueueSpot(shop.Customers.QueuePlaceOf(first))) < .5f &&
                Flat(ActorPosition(second), nav.QueueSpot(shop.Customers.QueuePlaceOf(second))) < .5f, 8, "both physical queue positions");
            yield return Capture("96-placement-customer-queue");
            int cashBeforeSales = shop.Model.Currency;
            yield return ServeNext("First visitor in the furnished shop");
            var remaining = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(remaining) == 0 && Flat(ActorPosition(remaining), nav.QueueSpot(0)) < .5f,
                12, "the second buyer physically moving up around furniture");
            Check(remaining.Phase == CustomerPhase.Queued, "The second visitor physically advances to the front without losing its claim");
            yield return ServeNext("Second visitor in the furnished shop");
            Check(shop.Model.Currency == cashBeforeSales + 16 && !shop.Model.GetTank("tank-a").HasValue && !shop.Model.GetTank("tank-b").HasValue,
                "The two routed purchases pay 16 total once and consume exactly the two specimens");
            Check(!shop.ServeCustomer() && shop.Model.Currency == cashBeforeSales + 16, "Repeated checkout pays nothing twice");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "both customers exiting around furniture");
            Check(shop.CloseForNight(), "Close after the furnished-store route test");
            Check(shop.Cycle.Phase == ShopDayPhase.Closing && !shop.OpenPlacementMenu() && !shop.PlacementActive && shop.Layout.Items.Count == 2,
                "Closing rejects arrangement without opening a preview or changing ownership");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "the normal closing transition");
            var report = shop.Reports.Last;
            Check(report != null && report.sales == 2 && report.revenue == 16 && report.pathProblems == 0 && shop.Layout.Items.Count == 2,
                "Daily report records two sales, 16 sales income and no path failures; furniture remains owned");
            yield return Capture("97-placement-day-report");
            Check(shop.SaveNow(), "Save completed placement and customer-route evidence");
            yield return PlacementRecoveryScenario();
        }

        IEnumerator PlacementNeutral()
        {
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            InputSystem.QueueStateEvent(cycleMouse, new MouseState());
            yield return null; yield return null;
        }

        IEnumerator PlacementClick(bool right = false)
        {
            yield return PlacementNeutral();
            cycleMouse.MakeCurrent();
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = (ushort)(right ? 2 : 1) });
            yield return null; yield return null;
            yield return PlacementNeutral();
        }

        IEnumerator PlacementAimAt(int gridX, int gridZ, Vector3 fixturePlayerPosition)
        {
            shop.SetPanel(ShopPanel.None);
            yield return PlacementNeutral();
            Vector3 target = shop.World.transform.TransformPoint(new Vector3(gridX * ShopLayout.GridSize, 0, gridZ * ShopLayout.GridSize));
            Vector3 difference = target - fixturePlayerPosition;
            float yaw = Mathf.Atan2(difference.x, difference.z) * Mathf.Rad2Deg;
            shop.Player.Teleport(fixturePlayerPosition, Quaternion.Euler(0, yaw, 0));
            yield return new WaitForSeconds(.2f); // Let the CharacterController settle on the floor before deriving pitch.
            difference = target - shop.Player.View.transform.position;
            float pitch = Mathf.Atan2(-difference.y, new Vector2(difference.x, difference.z).magnitude) * Mathf.Rad2Deg;
            cycleMouse.MakeCurrent();
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(0, -pitch / (.075f * shop.Sensitivity)) });
            yield return null; yield return null;
            yield return PlacementNeutral();
        }
    }
}
#endif
