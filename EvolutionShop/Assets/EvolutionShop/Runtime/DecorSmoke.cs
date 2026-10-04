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
        IEnumerator DecorScenario()
        {
            limitations.Add("R04.3 isolated technical fixture: 200 seeded credits, accelerated stock, fixture teleports, synthetic Input System placement/rotation/cancel; application commands drive menus, price changes and checkout. This is not ordinary earning/pacing, physical GUI-click or human usability evidence.");
            limitations.Add("Real native navigation and actors execute two appeal-enabled purchases and a bounded natural-arrival wave. A labelled reputation fixture probes combined caps. Duplicate/lab decor never earns appeal; the normal user slot is untouched.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Decor scenario uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            Check(shop.Customers.Ready && shop.Appeal.Points == 0 && shop.Customers.NextArrivalSeconds == 6 && shop.Customers.MaxVisitors == 2, "Fresh shop retains base demand and two-visitor cap before decor");
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionDecorKeyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionDecorMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.AllowValidationInput = true; shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionDecorReload") != null) { yield return DecorRestartScenario(); yield break; }

            yield return PlacementAimAt(-4, -1, new Vector3(-2, .2f, -2.1f));
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(null, ShopLayout.CoralPlanterId), "Open zero-funds coral preview");
            yield return PlacementNeutral(); Check(!shop.PlacementValid && shop.PlacementCost == 16, "Preview exposes coral cost and rejects zero funds");
            yield return PlacementClick(); Check(shop.Model.Currency == 0 && shop.Layout.Items.Count == 0 && shop.Appeal.Points == 0, "Rejected decor purchase grants neither ownership nor appeal");
            yield return CycleTap(UnityEngine.InputSystem.Key.Escape);
            Check(shop.SaveNow(), "Save zero-funds isolated state before cash fixture");
            var data = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); data.game.currency = 200;
            Check(SaveStore.Write(shop.SavePath, data, out var problem) && shop.ReloadSaved(), "Load explicitly seeded200-credit fixture / " + problem);

            yield return DecorPlace(ShopLayout.CoralPlanterId, 9, 4, new Vector3(3, .2f, 1.6f), 184, 0);
            string coral = shop.Layout.Items[0].Id;
            Check(shop.Customers.NextArrivalSeconds == 6 && shop.Model.TankIds.Length == 2, "Lab coral adds no demand bonus or creature capacity");
            yield return DecorMove(coral, -4, -1, new Vector3(-2, .2f, -2.1f), 10);
            Check(shop.Model.Currency == 184 && shop.Customers.NextArrivalSeconds == 5.75f, "Moving owned coral to store is free and refreshes cached demand on the same layout instance");
            yield return DecorPlace(ShopLayout.CoralPlanterId, -12, -8, new Vector3(-4.2f, .2f, -4.2f), 168, 10);
            yield return DecorPlace(ShopLayout.BubbleLampId, 4, -1, new Vector3(2, .2f, -2.1f), 144, 20);
            string lamp = shop.Layout.Items[2].Id;
            yield return DecorMove(lamp, 9, 4, new Vector3(3, .2f, 1.6f), 10);
            Check(shop.Customers.NextArrivalSeconds == 5.75f && shop.Appeal.BudgetBonus == 1, "Moving the only store lamp into lab removes its bonus immediately");
            yield return PlacementAimAt(4, -1, new Vector3(2, .2f, -2.1f));
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(lamp), "Preview returning owned lamp to store"); yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.Appeal.Points == 10 && shop.PlacementMessage.Contains("20"), "Candidate appeal is shown without granting it during preview");
            yield return CycleTap(UnityEngine.InputSystem.Key.R); yield return PlacementClick(true);
            Check(shop.Appeal.Points == 10 && shop.Layout.GetItem(lamp).Value.Z == 4, "Rotation and right-click cancel retain lab placement and current appeal");
            yield return DecorMove(lamp, 4, -1, new Vector3(2, .2f, -2.1f), 20);

            yield return PlacementAimAt(3, -5, new Vector3(1.5f, .2f, -4.7f));
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(null, ShopLayout.SillyStatueId), "Preview third distinct decoration"); yield return PlacementNeutral();
            Check(shop.PlacementValid && shop.SaveNow(), "Save before deliberate native-route failure fixture");
            var blocker = new GameObject("Decor validation temporary entry obstruction"); blocker.transform.SetParent(shop.World.transform, false);
            blocker.transform.position = shop.Customers.Navigation.EntryPoint + Vector3.up * .8f;
            var collider = blocker.AddComponent<BoxCollider>(); collider.size = new Vector3(3, 2, 2); Physics.SyncTransforms();
            try { yield return PlacementClick(); Check(shop.Model.Currency == 144 && shop.Layout.Items.Count == 3 && shop.Appeal.Points == 20, "Rejected native decor preflight grants no money loss, ownership or appeal"); }
            finally { collider.enabled = false; blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms(); }
            shop.CancelPlacement(); Check(shop.ReloadSaved() && shop.Customers.Navigation.Ready, "Normal reload recovers routes after fixture obstruction removal");
            yield return DecorPlace(ShopLayout.SillyStatueId, 3, -5, new Vector3(1.5f, .2f, -4.7f), 112, 30);
            yield return DecorPlace(ShopLayout.CoralPlanterId, 9, 4, new Vector3(3, .2f, 1.6f), 96, 30);
            yield return DecorPlace(ShopLayout.BubbleLampId, 10, 8, new Vector3(5.6f, .2f, 2.8f), 72, 30);
            Check(shop.Layout.DecorCount == 6 && shop.Model.TankIds.Length == 2 && shop.Reputation.Points == 0 && shop.Customers.NextArrivalSeconds == 5.25f, "Six owned pieces keep appeal capped30, two original slots and separate zero reputation");
            var casualType = CustomerArchetypes.Find(shop.Customers.Archetypes, CustomerArchetypes.Casual);
            Check(casualType.Budget == 33 && casualType.TolerancePercent == 165 && Math.Abs(shop.Customers.PremiumSharePercent - 100f * 25 / 115) < .01f, "Maximum decor gives additive limits and bounded Big spender share");
            bool stable = true;
            for (int i = 0; i < 80; i++)
                stable &= CustomerArchetypes.Find(shop.Customers.Archetypes, CustomerArchetypes.Casual).TolerancePercent == 165;
            Check(stable, "Eighty repeated preview refreshes do not compound appeal");
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(null, ShopLayout.CoralPlanterId), "Open seventh decor preview at ownership cap"); yield return PlacementNeutral();
            Check(!shop.PlacementValid && !shop.CommitPlacement() && shop.Model.Currency == 72 && shop.Layout.DecorCount == 6, "Seventh decor cannot charge or extend the separate six-piece cap"); shop.CancelPlacement();
            yield return Capture("111-decor-catalogue-cap");
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); AimAt(new Vector3(.1f, .2f, -4.8f), new Vector3(0, 1, -.4f));
            yield return Capture("112-three-store-decor");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Appeal.Points == 30 && shop.Model.Currency == 72 && shop.Customers.NextArrivalSeconds == 5.25f, "Decor restore recomputes appeal and customer cache without charging or stacking");
            SeedStock("bubblebelly", "tank-a"); SeedStock("bubblebelly", "tank-b");
            Ok(shop.SetSpeciesPrice("bubblebelly", 16), "Set Bubblebelly16credit tag for an appeal-dependent purchase");
            var price = shop.Model.GetPrice("bubblebelly").Value;
            var baseCasual = CustomerArchetypes.Find(CustomerArchetypes.CreateDefault(), CustomerArchetypes.Casual);
            Check(baseCasual.Judge("bubblebelly", 16, 10) == PriceReaction.TooExpensive && shop.Customers.Preview(casualType, price) != PriceReaction.TooExpensive, "Base casual refuses16 while the decorated-shop price preview accepts it");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open decorated shop for two real customers");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(.4f); var second = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 35, "two appeal-enabled buyers reaching real queue");
            Check(first.Price == 16 && second.Price == 16 && first.Budget == 33 && first.Archetype.TolerancePercent == 165, "Actual customers snapshot the decorated-shop budget and agree16 each");
            Check(!shop.OpenDecorMenu() && !shop.BeginPlacement(coral) && shop.Layout.DecorCount == 6 && shop.Appeal.Points == 30, "Daytime arranging cannot alter owned decor or accepted orders");
            Ok(shop.SetSpeciesPrice("bubblebelly", 17), "Change future tag while two sixteen-credit claims wait");
            Check(first.Price == 16 && second.Price == 16, "Appeal never rewrites accepted prices after repricing"); yield return Capture("113-appeal-customer-orders");
            yield return ServeNext("First appeal-enabled sale");
            var remaining = first.Outcome == CustomerOutcome.Sold ? second : first;
            yield return WaitFor(() => shop.Customers.QueuePlaceOf(remaining) == 0 && Flat(ActorPosition(remaining), shop.Customers.Navigation.QueueSpot(0)) < .5f, 12, "second appeal buyer moving up");
            yield return ServeNext("Second appeal-enabled sale");
            Check(shop.Model.Currency == 104 && shop.Reputation.Points == 4 && shop.Appeal.Points == 30 && !shop.ServeCustomer(), "Two sales pay32 once, earn4reputation, and leave appeal unchanged");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "decorated-shop buyers exiting");
            Check(shop.CloseForNight(), "Close first decorated trading day"); yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "decorated shop night");
            Check(shop.Reports.Last.sales == 2 && shop.Reports.Last.revenue == 32 && shop.Reports.Last.pathProblems == 0, "Daily report records actual appeal-enabled sales without path failures");
            yield return Capture("114-decor-day-report");
            Check(shop.SaveNow(), "Save complete decorated state before labelled reputation fixture");
            var combinedFixture = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            combinedFixture.reputation.points = 30;
            Check(SaveStore.Write(shop.SavePath, combinedFixture, out problem) && shop.ReloadSaved(), "Load labelled reputation fixture while preserving full layout and settings / " + problem);
            var maxCasual = CustomerArchetypes.Find(shop.Customers.Archetypes, CustomerArchetypes.Casual);
            Check(shop.Appeal.Points == 30 && shop.Customers.NextArrivalSeconds == 3.25f && maxCasual.TolerancePercent == 185 && maxCasual.Budget == 37 && Math.Abs(shop.Customers.PremiumSharePercent - 100f * 40 / 115) < .01f, "Labelled reputation30 fixture combines with appeal at exact bounded maximums");
            shop.OpenDecorMenu(); yield return Capture("115-combined-demand-catalogue");
            SeedStock("pondlet", "tank-a"); SeedStock("pondlet", "tank-b");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open max-effects natural-arrival wave"); shop.Customers.ArrivalsEnabled = true;
            yield return WaitFor(() => shop.Customers.ActiveCount == 1, 5, "first natural arrival"); float firstTime = Time.realtimeSinceStartup;
            yield return WaitFor(() => shop.Customers.ActiveCount == 2, 5, "second natural capped arrival"); float spacing = Time.realtimeSinceStartup - firstTime;
            Check(spacing >= 3f && spacing <= 4.5f, "Actual natural-arrival spacing follows3.25s cap / observed" + spacing.ToString("0.00") + "s");
            yield return Hold(4); Check(shop.Customers.ActiveCount == 2, "Demand bonus never exceeds the two-visitor physical queue capacity");
            shop.Customers.ArrivalsEnabled = false; Check(shop.CloseForNight(), "Close bounded unpaid wave safely");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 15, "unpaid wave closing grace");
            Check(shop.Model.Currency == 104 && shop.DisplayedStockCount() == 2 && shop.Appeal.Points == 30 && shop.Reputation.Points == 30, "Unpaid closure preserves both specimens, money and independent progression");
            Check(shop.SaveNow(), "Save final decorated stocked fixture for fresh-process reload");
        }

        IEnumerator DecorPlace(string definition, int x, int z, Vector3 player, int cash, int points)
        {
            shop.AllowValidationInput = true; yield return PlacementAimAt(x, z, player);
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(null, definition), "Open " + definition + " preview"); yield return PlacementNeutral();
            Check(shop.PlacementValid, "Valid decor footprint and access / " + shop.PlacementMessage);
            var preview = shop.World.transform.Find("Decor placement preview (no colliders)");
            Check(preview && preview.gameObject.activeSelf && preview.GetComponentsInChildren<Collider>(true).Length == 0, "Active decor preview has no collider");
            yield return Capture("110-" + definition + "-" + shop.Layout.Items.Count + "-preview");
            yield return PlacementClick();
            Check(!shop.PlacementActive && shop.Model.Currency == cash && shop.Appeal.Points == points, "Committed " + definition + " has exact cash" + cash + " and appeal" + points);
            var item = shop.Layout.Items[shop.Layout.Items.Count - 1];
            Check(shop.World.TryGetPlacedTable(item.Id, out var root) && root.GetComponentsInChildren<Collider>().Length > 0, "Owned decor has physical collision and stable identity");
        }
        IEnumerator DecorMove(string id, int x, int z, Vector3 player, int points)
        {
            int cash = shop.Model.Currency; shop.AllowValidationInput = true; yield return PlacementAimAt(x, z, player);
            Check(shop.OpenDecorMenu() && shop.BeginPlacement(id), "Begin owned decor move"); yield return PlacementNeutral();
            Check(shop.PlacementValid, "Moved decor candidate preserves access / " + shop.PlacementMessage); yield return PlacementClick();
            Check(!shop.PlacementActive && shop.Appeal.Points == points && shop.Model.Currency == cash, "Free move updates derived appeal to" + points + " without charging");
        }
        IEnumerator DecorRestartScenario()
        {
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process reads isolated decorated fixture through normal reload");
            Check(shop.Layout.DecorCount == 6 && shop.Appeal.Points == 30 && shop.Reputation.Points == 30 && shop.Model.Currency == 104 && shop.DisplayedStockCount() == 2 && shop.Model.TankIds.Length == 2, "Fresh process restores six decor, derived appeal, reputation, exact money and stock");
            Check(shop.Customers.NextArrivalSeconds == 3.25f && CustomerArchetypes.Find(shop.Customers.Archetypes, CustomerArchetypes.Casual).Budget == 37, "New session derives combined demand once from saved layout and reputation");
            shop.OpenDecorMenu(); yield return Capture("116-decor-process-reload"); Check(shop.SaveNow(), "Restored decorated state writes current format in its isolated directory");
        }
    }
}
#endif
