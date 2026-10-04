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
        IEnumerator LabScenario()
        {
            limitations.Add("R05.1a isolated construction fixture: seeded 1000 credits, application menu/purchase/save/checkout commands, fixture camera positions and accelerated first recipe. Floor aim, rotate, cancel, placement, harvest and station E interactions use synthetic Input System events. This is not a human usability or economy test.");
            limitations.Add("One continuous route through the doorway and both rear extensions uses the real CharacterController after one starting teleport. Temporary entry obstruction tests native rollback; two selected customer actors use actual navigation and queue transactions. No specimen storage/new recipe/extra machine is included.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Lab validation uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionLabKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionLabMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.AllowValidationInput = true;
            shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionLabReload") != null) { yield return LabRestart(); yield break; }
            Check(shop.Customers.Ready && shop.Layout.LabTier == 0 && shop.World.Stations.Count == 7, "Fresh lab has four essential facilities, seven total stations and native customer routes");
            LabWorldCheck("Original anchors");
            Check(shop.OpenLabMenu() && !shop.BuyLabExpansion() && shop.Model.Currency == 0 && shop.Layout.LabTier == 0, "Zero credits rejects lab expansion without geometry change");
            Check(shop.SaveNow(), "Save isolated lab before labelled cash fixture");
            var fixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency = 1000;
            Check(SaveStore.Write(shop.SavePath, fixture, out var error) && shop.ReloadSaved(), "Load labelled 1000-credit construction fixture / " + error);
            Check(shop.OpenLabMenu() && !shop.StoreFurniture(ShopLayout.BloomFacilityId) && !shop.RequestFurnitureSale(ShopLayout.IncubatorFacilityId), "Essential facilities cannot be stored or sold");
            Check(shop.SaveNow(), "Save baseline before native expansion obstruction");
            var blocker = StorageEntryBlocker();
            try
            {
                Check(!shop.BuyLabExpansion() && shop.Layout.LabTier == 0 && shop.World.AppliedLayout.LabTier == 0 && shop.Model.Currency == 1000, "Failed native lab expansion rolls back geometry and spends nothing");
            }
            finally { blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            Check(shop.ReloadSaved() && shop.Customers.Navigation.Ready && shop.OpenLabMenu(), "Recover routes after removing the labelled obstruction");
            Check(shop.BuyLabExpansion() && shop.Layout.LabTier == 1 && shop.Model.Currency == 940, "First rear extension costs60 and adds four metres");
            Check(shop.BuyLabExpansion() && shop.Layout.LabTier == 2 && shop.Model.Currency == 820, "Second rear extension costs120 and adds another four metres");
            Check(!shop.BuyLabExpansion() && shop.Model.Currency == 820 && shop.Model.TankIds.Length == 2 && shop.Layout.StoreTier == 0, "Maximum lab expansion cannot charge again or invent display capacity/store tiers");
            yield return Capture("140-lab-expansion-menu");
            LabRoutesCheck("Expanded empty lab");

            // Stage exact doses before moving growers, then keep the same incubator job through relocation.
            shop.Model.Tick(20);
            Ok(shop.Model.Collect(CultureKind.Bloom), "Collect seeded Bloom for relocation proof");
            Ok(shop.Model.Dose(CultureKind.Bloom), "Stage exact Bloom before moving grower");
            Ok(shop.Model.Collect(CultureKind.Flow), "Collect seeded Flow for relocation proof");
            Ok(shop.Model.Dose(CultureKind.Flow), "Stage exact Flow before moving grower");
            yield return LabMove(ShopLayout.BloomFacilityId, 0, 8, 0, new Vector3(-4.2f, .2f, 5.5f));
            yield return LabMove(ShopLayout.FlowFacilityId, 0, 8, 0, new Vector3(-1.4f, .2f, 5.5f));
            Check(shop.Model.Staged(CultureKind.Bloom) == 1 && shop.Model.Staged(CultureKind.Flow) == 1 && shop.Model.ReservedCultureCount == 2, "Grower relocation preserves both staged doses and siphon ownership");
            Ok(shop.Model.BeginIncubation(), "Start same Pondlet job before moving incubator");
            string activeId = shop.Model.Incubator.SpecimenId;
            yield return LabMove(ShopLayout.IncubatorFacilityId, 0, 16, 1, new Vector3(2, .2f, 9.4f));
            Check(shop.Model.Incubator.SpecimenId == activeId && shop.Model.Incubator.SpeciesId == "pondlet" && shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Model.Incubator.SecondsRemaining > 0 && shop.Model.Incubator.SecondsRemaining < shop.Config.IncubationSeconds,
                "Running incubation keeps exact specimen/job and advances naturally across move");
            shop.Model.Tick(20); shop.World.Refresh(shop.Model);
            yield return LabMove(ShopLayout.UpgradeFacilityId, 0, 18, 3, new Vector3(-3.2f, .2f, 10.9f));
            yield return LabMove(ShopLayout.IncubatorFacilityId, 0, 14, 1, new Vector3(2, .2f, 8.5f));
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.Model.Incubator.SpecimenId == activeId && shop.Model.Currency == 820, "Ready specimen follows incubator relocation without loss, duplication or charge");
            LabWorldCheck("Relocated active lab");

            // Rejected target and cancellation preserve an occupied facility and its live root.
            string layoutBefore = JsonUtility.ToJson(shop.Layout.CaptureSave());
            yield return LabAim(ShopLayout.IncubatorFacilityId, -4, -7, new Vector3(0, .2f, -1.2f));
            Check(shop.OpenLabMenu() && shop.BeginFacilityPlacement(ShopLayout.IncubatorFacilityId), "Start occupied incubator preview over protected doorway");
            yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement() && shop.Model.Incubator.SpecimenId == activeId && JsonUtility.ToJson(shop.Layout.CaptureSave()) == layoutBefore, "Doorway rejection preserves occupied machine and layout");
            yield return CycleTap(Key.R); yield return CycleTap(Key.Escape);
            Check(!shop.PlacementActive && shop.Model.Currency == 820 && JsonUtility.ToJson(shop.Layout.CaptureSave()) == layoutBefore, "Rotation then Escape cancels without changing occupied facility");
            yield return LabAim(ShopLayout.IncubatorFacilityId, 0, 16, new Vector3(2, .2f, 9.4f));
            Check(shop.OpenLabMenu() && shop.BeginFacilityPlacement(ShopLayout.IncubatorFacilityId), "Preview occupied move before native rollback probe"); yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.SaveNow(), "Occupied candidate valid before deliberate entry obstruction");
            blocker = StorageEntryBlocker();
            try
            {
                yield return PlacementClick();
                Check(shop.Model.Incubator.SpecimenId == activeId && JsonUtility.ToJson(shop.Layout.CaptureSave()) == layoutBefore, "Failed native occupied move restores original pose and exact specimen");
                Check(!shop.PlacementActive, "Unsafe navigation recovery closes the preview until routes recover");
                LabWorldCheck("Rolled-back occupied lab");
            }
            finally { blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            shop.CancelPlacement(); Check(shop.ReloadSaved() && shop.Customers.Navigation.Ready, "Reload recovers lab routes after obstruction removal");

            // One starting teleport, then real controller movement through both extension seams.
            editedWalkingMetres = 0; editedDoorCrossings = 0;
            shop.Player.Teleport(new Vector3(0, .2f, -3.8f), Quaternion.identity);
            yield return EditedWalkTo(new Vector2(0, 2.4f), "lab doorway");
            yield return EditedWalkTo(new Vector2(0, 6.35f), "first rear seam");
            yield return EditedWalkTo(new Vector2(0, 9.7f), "second rear seam");
            yield return EditedWalkTo(new Vector2(0, 12.5f), "back of expanded lab");
            yield return EditedWalkTo(new Vector2(0, 9.7f), "return past incubator");
            yield return EditedWalkTo(new Vector2(-4, 9.7f), "upgrade approach");
            yield return EditedWalkTo(new Vector2(-4, 10.9f), "relocated upgrade operator point");
            Check(editedWalkingMetres > 22 && editedDoorCrossings == 1, "Actual player crosses doorway and both rear extensions / metres=" + editedWalkingMetres.ToString("0.00"));
            yield return LabInteract(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Ok(shop.Perform(shop.Model.BuyUpgrade()), "Buy siphon upgrade at relocated bench");
            Check(shop.Model.UpgradeOwned, "Relocated upgrade bench retains functional upgrade authority");
            yield return Capture("141-expanded-lab-upgrade");
            shop.SetPanel(ShopPanel.None);
            yield return EditedWalkTo(new Vector2(-4, 9.7f), "leave upgrade");
            yield return EditedWalkTo(new Vector2(0, 9.7f), "incubator aisle");
            yield return EditedWalkTo(new Vector2(.5f, 10.85f), "rotated incubator operator point");
            yield return LabInteract(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Check(shop.Model.Incubator.SpecimenId == activeId, "Physical E reaches rotated incubator with retained ready output");
            yield return Capture("142-relocated-incubator-ready");
            shop.SetPanel(ShopPanel.None);
            yield return EditedWalkTo(new Vector2(0, 9.7f), "return from incubator");
            yield return EditedWalkTo(new Vector2(0, 6.35f), "grower aisle");
            yield return EditedWalkTo(new Vector2(-4.2f, 6.35f), "Bloom operator");
            yield return LabHarvest(ShopLayout.BloomFacilityId, CultureKind.Bloom);
            yield return EditedWalkTo(new Vector2(-1.4f, 6.35f), "Flow operator");
            yield return LabHarvest(ShopLayout.FlowFacilityId, CultureKind.Flow);
            yield return EditedWalkTo(new Vector2(0, 6.35f), "leave lab growers");
            yield return EditedWalkTo(new Vector2(6, 6.35f), "first extension east wall");
            yield return MoveFor(Key.D, .6f);
            Check(shop.Player.transform.position.x > 6.3f && shop.Player.transform.position.x < 6.65f, "Actual first-extension east wall contains the player");
            yield return EditedWalkTo(new Vector2(6, 12.8f), "second extension rear wall");
            yield return MoveFor(Key.W, .6f);
            Check(shop.Player.transform.position.z > 13 && shop.Player.transform.position.z < 13.3f, "Moved rear wall contains the player after both extensions");
            yield return EditedWalkTo(new Vector2(6, 6.35f), "return along new east wall");
            yield return EditedWalkTo(new Vector2(0, 6.35f), "return to lab aisle");
            yield return EditedWalkTo(new Vector2(0, -1.2f), "back through store doorway");
            Check(editedDoorCrossings == 2, "Real player returns through doorway after operating all four relocated facilities");
            yield return LabTransferReady("tank-a", activeId);
            yield return ExpansionTransfer("bubblebelly", "tank-b");
            LabRoutesCheck("Working relocated lab");
            Check(shop.SaveNow(), "Save working relocated lab before changed-geometry restore");
            string preserved = File.ReadAllText(shop.SavePath);
            shop.ConfirmNewGame();
            Check(shop.Layout.LabTier == 0 && shop.Layout.GetFacility(ShopLayout.IncubatorFacilityId).Value.Z == 0, "Confirmed reset restores original rear wall and facility anchors");
            File.WriteAllText(shop.SavePath, preserved);
            Check(shop.ReloadSaved() && shop.Layout.LabTier == 2 && shop.Model.GetTank("tank-a").Value.Id == activeId && shop.Model.UpgradeOwned, "Changed-geometry reload restores lab poses, exact displayed specimen and upgrade");
            LabWorldCheck("Reloaded expanded lab");
            shop.AllowValidationInput = false;
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open ordinary store with expanded working lab");
            Check(!shop.OpenLabMenu() && !shop.BuyLabExpansion() && !shop.BeginFacilityPlacement(ShopLayout.BloomFacilityId), "Daytime rejects lab construction and machine rearrangement");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(.3f); var second = Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 45, "two buyers queue after lab edits");
            int cash = shop.Model.Currency;
            yield return ServeNext("First lab-stock buyer");
            var next = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(next) == 0 && Flat(ActorPosition(next), shop.Customers.Navigation.QueueSpot(0)) < .5f, 12, "second lab-stock buyer advances");
            yield return ServeNext("Second lab-stock buyer");
            Check(shop.Model.Currency == cash + 16 && shop.DisplayedStockCount() == 0 && !shop.ServeCustomer(), "Expanded lab supplies two exact sales with16credits paid once");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "lab-stock buyers exit");
            Check(shop.CloseForNight(), "Close shop after lab-stock buyers leave");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "lab-stock trading close");
            Check(shop.Reports.Last.sales == 2 && shop.Reports.Last.pathProblems == 0, "Lab edits preserve two-sale report without customer path problems");
            SeedStock("ribbonfin", "tank-a");
            shop.SetPanel(ShopPanel.None); AimAt(new Vector3(3.8f, .2f, 6.2f), new Vector3(-2, 1.4f, 9.2f));
            yield return Capture("143-expanded-working-lab");
            shop.OpenLabMenu(); yield return Capture("144-lab-final-menu");
            Check(shop.SaveNow(), "Save final lab fixture for fresh-process reload");
            File.Copy(shop.SavePath, Path.Combine(evidence, "lab-final-save.json"));
        }

        IEnumerator LabAim(string id, int x, int z, Vector3 position)
        {
            var facility = shop.Layout.GetFacility(id).Value;
            Vector3 target = new Vector3(facility.WorldX + (x - facility.X) * .5f, 0, facility.WorldZ + (z - facility.Z) * .5f);
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = true; yield return PlacementNeutral();
            Vector3 difference = target - position;
            shop.Player.Teleport(position, Quaternion.Euler(0, Mathf.Atan2(difference.x, difference.z) * Mathf.Rad2Deg, 0));
            yield return new WaitForSeconds(.2f);
            difference = target - shop.Player.View.transform.position;
            float pitch = Mathf.Atan2(-difference.y, new Vector2(difference.x, difference.z).magnitude) * Mathf.Rad2Deg;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(0, -pitch / (.075f * shop.Sensitivity)) });
            yield return null; yield return null; yield return PlacementNeutral();
        }

        IEnumerator LabMove(string id, int x, int z, int turns, Vector3 position)
        {
            Check(shop.World.TryGetLabFacility(id, out var original), "Find stable original root for " + id);
            int cash = shop.Model.Currency;
            yield return LabAim(id, x, z, position);
            Check(shop.OpenLabMenu() && shop.BeginFacilityPlacement(id), "Begin shared facility preview / " + id);
            yield return PlacementNeutral();
            for (int i = 0; i < 4 && shop.PlacementRotation != turns; i++) yield return CycleTap(Key.R);
            Check(shop.PlacementX == x && shop.PlacementZ == z && shop.PlacementRotation == turns && shop.PlacementValid, "Facility offset and rotation preview valid / " + id + " / " + shop.PlacementMessage);
            if (id == ShopLayout.IncubatorFacilityId && z == 16) yield return Capture("145-incubator-ghost");
            yield return PlacementClick();
            var facility = shop.Layout.GetFacility(id).Value;
            Check(!shop.PlacementActive && facility.X == x && facility.Z == z && facility.QuarterTurns == turns && shop.Model.Currency == cash && shop.World.TryGetLabFacility(id, out var current) && original == current, "Real click relocates original root free / " + id);
        }

        void LabWorldCheck(string context)
        {
            Check(shop.World.Stations.Count == 7, context + " / no duplicated stations");
            foreach (var facility in shop.Layout.Facilities)
            {
                Check(shop.World.TryGetLabFacility(facility.Id, out var root) && Vector3.Distance(root.localPosition, new Vector3(facility.WorldX, 0, facility.WorldZ)) < .001f && Quaternion.Angle(root.localRotation, Quaternion.Euler(0, facility.QuarterTurns * 90, 0)) < .01f,
                    context + " / exact live facility pose " + facility.Id);
            }
        }

        void LabRoutesCheck(string context)
        {
            var nav = shop.Customers.Navigation;
            foreach (float z in new[] { 1f, 3f, 7.5f, 11.5f })
                Check(!nav.HasNavigationAt(new Vector3(0, 0, z), .1f), context + " / customer bake excludes lab at z" + z);
            foreach (string slot in shop.Model.TankIds)
                Check(nav.CanReach(nav.EntryPoint, nav.BrowseSpot(slot, 0)) && nav.CanReach(nav.BrowseSpot(slot, 0), nav.QueueSpot(0)), context + " / store route " + slot);
        }

        IEnumerator LabInteract(string id, ShopPanel expected)
        {
            shop.AllowValidationInput = false; yield return null;
            Check(shop.World.TryGetLabFacility(id, out var root), "Find moved interaction root " + id);
            shop.SetPanel(ShopPanel.None);
            shop.Player.View.transform.LookAt(root.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return null; yield return null;
            Check(shop.Focused != null && shop.Focused.FacilityId == id, "Physical crosshair focuses relocated " + id);
            yield return CycleTap(Key.E);
            Check(shop.Panel == expected, "Real E opens relocated facility " + id);
        }

        IEnumerator LabHarvest(string id, CultureKind kind)
        {
            shop.AllowValidationInput = false; yield return null;
            shop.World.TryGetLabFacility(id, out var root);
            shop.SetPanel(ShopPanel.None); int before = shop.Model.Available(kind);
            float deadline = Time.realtimeSinceStartup + 10;
            while (shop.Model.Available(kind) == before)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Relocated grower harvest " + id);
                shop.Player.View.transform.LookAt(root.TransformPoint(new Vector3(0, 1.1f, 0)));
                cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 });
                yield return null;
            }
            yield return PlacementNeutral();
            Check(shop.Model.Available(kind) == before + 1, "Held mouse harvests exactly one culture at relocated " + id);
        }

        IEnumerator LabTransferReady(string slot, string specimen)
        {
            shop.AllowValidationInput = false; yield return null;
            Station station = null; foreach (var candidate in shop.World.Stations) if (candidate.TankId == slot) station = candidate;
            Vector3 position = shop.Customers.Navigation.BrowseSpot(slot, 0); position.y = .2f;
            shop.SetPanel(ShopPanel.None); AimAt(position, station.transform.position + Vector3.up * 1.15f);
            yield return null; yield return null; yield return CycleTap(Key.E);
            Check(shop.Model.GetTank(slot).HasValue && shop.Model.GetTank(slot).Value.Id == specimen && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Real E transfers exact relocated output once into " + slot);
        }

        IEnumerator LabRestart()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process explicitly reloads isolated lab fixture");
            Check(shop.Layout.LabTier == 2 && shop.Layout.StoreTier == 0 && shop.Layout.Facilities.Count == 4 && shop.Model.UpgradeOwned && shop.DisplayedStockCount() == 1, "Fresh process restores both lab wings, four facilities, upgrade and one resident");
            Check(shop.Layout.GetFacility(ShopLayout.BloomFacilityId).Value.Z == 8 && shop.Layout.GetFacility(ShopLayout.FlowFacilityId).Value.Z == 8 && shop.Layout.GetFacility(ShopLayout.IncubatorFacilityId).Value.Z == 14 && shop.Layout.GetFacility(ShopLayout.IncubatorFacilityId).Value.QuarterTurns == 1 && shop.Layout.GetFacility(ShopLayout.UpgradeFacilityId).Value.Z == 18, "Fresh process preserves exact relocated offsets and rotation");
            LabWorldCheck("Fresh process"); LabRoutesCheck("Fresh process");
            shop.AllowValidationInput = false; AimAt(new Vector3(3.8f, .2f, 6.2f), new Vector3(-2, 1.4f, 9.2f));
            shop.OpenLabMenu(); yield return Capture("146-lab-restart");
            Check(shop.SaveNow(), "Restored lab writes valid current save in new isolated directory");
        }
    }
}
#endif
