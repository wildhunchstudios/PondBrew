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
        IEnumerator AutomationScenario()
        {
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionAutomationSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Automation route uses only its explicitly isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors == 2 && !shop.Customers.UseBaseReputationEffectsForValidation,
                "Automation retains ordinary customer concurrency and reputation rules");
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionAutomationKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionAutomationMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            limitations.Add("R08 automation uses an isolated labelled 300-credit fixture, accelerated manual Pondlet discovery and a temporary Bubblebelly discovery/sale used only for the two-recipe queue-priority probe. Bench/counter/incubator access uses synthetic E and scripted player positions; menu choices call guarded production session commands. Deliberately filled/released culture and replenished grower fixtures isolate capacity and contention; they are labelled and are not economy evidence. Collection, dosing, target planning, Moss handoff, incubation and technician travel run on ordinary frames. Temporary disabled station/session components and an isolated .tmp-directory inject recovery conditions. Actual standalone captures explicitly switch between 1280x720 and 1920x1080; Editor camera exports are diagnostic only. No native input, human usability/audio, campaign balance, performance, or offline progress is claimed.");
            Check(shop.SkipTutorial(), "Skip guidance only in the isolated automation fixture");
            Check(shop.Automation != null && !shop.Automation.CollectorOwned && !shop.Automation.DoserOwned &&
                !shop.Automation.CollectorEnabled && !shop.Automation.DoserEnabled && !shop.Automation.IsPending,
                "Fresh modules are unowned, disabled and have no automatic request");
            yield return AutomationEnter();
            Check(!shop.PurchaseAutomationModule(true) && !shop.PurchaseAutomationModule(false) &&
                !shop.RequestAutomationBatch() && shop.Model.Currency == 0,
                "Fresh shop cannot buy locked modules or request unowned work");
            yield return TutorialCapture("600-automation-locked");
            StaffCreditFixture(300, "R08.1 modules and contention fixture");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Ok(shop.Perform(shop.Model.BuyUpgrade()), "Buy the existing larger siphon at the physical bench");
            SpecimenProduce("pondlet"); string discovery = shop.Model.Incubator.SpecimenId;
            yield return LabTransferReady("tank-a", discovery);
            Check(shop.Model.GetTank("tank-a").Value.Id == discovery, "Retain labelled manual Pondlet discovery in the first display");
            Check(shop.Model.Currency == 276 && shop.Model.IsDiscovered("pondlet") && shop.Model.Capacity == 8,
                "Unlock fixture spends exactly twenty-four credits on the siphon without selling its creature");
            yield return AutomationEnter();
            Check(shop.SaveNow(), "Save unlocked fixture before transactional purchase obstruction");
            var originalModel = shop.Model; string originalFile = File.ReadAllText(shop.SavePath);
            TutorialBlockSave();
            try
            {
                Check(!shop.PurchaseAutomationModule(true) && ReferenceEquals(shop.Model, originalModel) &&
                    !shop.Automation.CollectorOwned && shop.Model.Currency == 276 && File.ReadAllText(shop.SavePath) == originalFile,
                    "Failed collector purchase leaves original model, ownership, credits and save bytes unchanged");
                Check(shop.AutomationBlockedBySave && !shop.RetryAutomation(),
                    "Failed module write latches automation and explicit retry still rejects an obstructed save");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.RetryAutomation() && !shop.AutomationBlockedBySave, "Explicit successful retry clears the automation write block");
            Check(shop.PurchaseAutomationModule(true) && !shop.PurchaseAutomationModule(true) && shop.Model.Currency == 240 &&
                shop.Automation.CollectorOwned && !shop.Automation.CollectorEnabled,
                "Collector purchase costs exactly thirty-six credits once and starts disabled");
            Check(shop.PurchaseAutomationModule(false) && !shop.PurchaseAutomationModule(false) && shop.Model.Currency == 216 &&
                shop.Automation.DoserOwned && !shop.Automation.DoserEnabled,
                "Doser purchase costs exactly twenty-four credits once and starts disabled");
            Check(!shop.SelectAutomationRecipe("missing") && !shop.SelectAutomationRecipe("bubblebelly") &&
                shop.SelectAutomationRecipe("pondlet") && !shop.RequestAutomationBatch(),
                "Only discovered recipes are selectable and both-disabled modules cannot create a request");
            yield return TutorialCapture("601-automation-owned-disabled");
            shop.Player.Teleport(new Vector3(0, .2f, -3), Quaternion.identity);
            Check(!shop.OpenAutomationMenu() && !shop.SetAutomationEnabled(true, true) && !shop.RequestAutomationBatch(),
                "A stale automation panel cannot control the bench through the shop partition");
            yield return AutomationEnter();
            Check(shop.SetAutomationEnabled(true, true) && shop.SetAutomationEnabled(false, true), "Enable both paid modules explicitly");
            var incubator = StationOf(StationKind.Incubator);
            incubator.enabled = false;
            try
            {
                Check(shop.RequestAutomationBatch() && shop.Automation.IsPending && !string.IsNullOrEmpty(shop.Automation.RequestId),
                    "One explicit request creates a stable pending identity");
                string request = shop.Automation.RequestId, ingredients = TechnicianIngredients();
                Check(!shop.RequestAutomationBatch() && !shop.SelectAutomationRecipe("bubblebelly") &&
                    shop.Automation.RequestId == request && shop.Automation.RecipeId == "pondlet",
                    "Duplicate requests and recipe changes cannot replace the active batch");
                yield return new WaitForSeconds(.9f);
                Check(shop.Automation.RequestId == request && TechnicianIngredients() == ingredients &&
                    !string.IsNullOrEmpty(shop.AutomationStatus), "Disabled incubator waits visibly without collecting or dosing");
                yield return TutorialCapture("602-automation-pending-station");
                shop.SetPanel(ShopPanel.Pause); string fullModel = JsonUtility.ToJson(shop.Model.CaptureSave());
                yield return new WaitForSeconds(.9f);
                Check(JsonUtility.ToJson(shop.Model.CaptureSave()) == fullModel && shop.Automation.RequestId == request,
                    "Global pause freezes exact machine, growers, ingredients and pending automation");
                Check(shop.SaveNow() && shop.ReloadSaved() && shop.Automation.RequestId == request &&
                    JsonUtility.ToJson(shop.Model.CaptureSave()) == fullModel && shop.Model.Currency == 216,
                    "Paused save/reload retains exact one-batch identity and resources without offline progress or charges");
                shop.SetPanel(ShopPanel.Automation);
                Check(shop.SetAutomationEnabled(true, false) && shop.SetAutomationEnabled(false, false),
                    "Player can disable both installed modules with a pending batch");
                yield return new WaitForSeconds(.9f);
                Check(shop.Automation.RequestId == request && TechnicianIngredients() == ingredients,
                    "Disabling modules retains the pending request and all existing resources");
                Check(shop.SetAutomationEnabled(true, true) && shop.SetAutomationEnabled(false, true), "Re-enable the same pending batch");
            }
            finally { if (incubator) incubator.enabled = true; }
            // Disable one dependency before yielding; no unguarded work frame separates the probes.
            foreach (StationKind kind in new[] { StationKind.Bloom, StationKind.Flow, StationKind.Upgrade })
            {
                var station = StationOf(kind); station.enabled = false;
                string ingredients = TechnicianIngredients(), request = shop.Automation.RequestId;
                try
                {
                    yield return new WaitForSeconds(.9f);
                    Check(TechnicianIngredients() == ingredients && shop.Automation.RequestId == request &&
                        !string.IsNullOrEmpty(shop.AutomationStatus), "Disabled required station retains pending work and resources / " + kind);
                }
                finally { if (station) station.enabled = true; }
            }
            string guardedIngredients = TechnicianIngredients(), guardedRequest = shop.Automation.RequestId;
            shop.enabled = false;
            try
            {
                yield return new WaitForSeconds(.9f);
                Check(TechnicianIngredients() == guardedIngredients && shop.Automation.RequestId == guardedRequest,
                    "Disabled session does not run hidden automation or accumulate a completed request");
            }
            finally { shop.enabled = true; }
            Check(shop.OpenPlacementMenu(), "Open ordinary room arrangement while automation is pending");
            yield return new WaitForSeconds(.9f);
            Check(TechnicianIngredients() == guardedIngredients && shop.Automation.RequestId == guardedRequest,
                "Arrangement menu stops collection and dosing while preserving the request");
            Check(shop.BeginPlacement(), "Enter a real uncommitted furniture preview");
            yield return new WaitForSeconds(.9f);
            Check(shop.PlacementActive && TechnicianIngredients() == guardedIngredients && shop.Model.Currency == 216,
                "Active placement preview also stops automation and spends no currency");
            shop.CancelPlacement();
            incubator = StationOf(StationKind.Incubator); incubator.enabled = false;
            try { yield return AutomationEnter(); }
            finally { if (incubator) incubator.enabled = true; }
            string tutorialBefore = TutorialState(); float began = Time.realtimeSinceStartup;
            yield return AutomationWait(() => !shop.Automation.IsPending, 10, "ordinary collection and one exact dose preparation");
            Check(shop.Model.Available(CultureKind.Bloom) == 0 && shop.Model.Available(CultureKind.Flow) == 0 &&
                shop.Model.Staged(CultureKind.Bloom) == 1 && shop.Model.Staged(CultureKind.Flow) == 1 &&
                shop.Model.ReservedCultureCount == 2 && shop.Model.Incubator.Phase == IncubatorPhase.Dosing &&
                shop.Model.Incubator.SpecimenId == null && shop.Model.Currency == 216 &&
                shop.Model.GetTank("tank-a").Value.Id == discovery && TutorialState() == tutorialBefore,
                "Ordinary automation collects only two missing units, stages one Pondlet and leaves cash, discovery and manual tutorial credit unchanged");
            Check(Time.realtimeSinceStartup - began >= 2 * shop.Model.CollectionSecondsPerUnit - .08f,
                "Unblocking cannot instantly catch up both timed collections from previously disabled time");
            guardedIngredients = TechnicianIngredients(); shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && !shop.Automation.IsPending &&
                TechnicianIngredients() == guardedIngredients && shop.Model.Currency == 216,
                "Completed request stays completed across exact saved reload without re-dosing");
            yield return AutomationEnter(); yield return TutorialCapture("603-automation-prepared");
            yield return new WaitForSeconds(.9f);
            Check(TechnicianIngredients() == guardedIngredients && shop.Model.Incubator.Phase == IncubatorPhase.Dosing,
                "Enabled modules remain idle after one batch and never start incubation or repeat collection");
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Player starts the automatically prepared batch at the physical incubator");
            string produced = shop.Model.Incubator.SpecimenId;
            Check(!string.IsNullOrEmpty(produced) && produced != discovery, "Manual start creates one new specimen identity");
            yield return AutomationEnter(); Check(shop.RequestAutomationBatch(), "Request next preparation while the manual batch is busy");
            guardedIngredients = TechnicianIngredients(); yield return new WaitForSeconds(.9f);
            Check(shop.Automation.IsPending && TechnicianIngredients() == guardedIngredients &&
                shop.Model.Incubator.SpecimenId == produced && !string.IsNullOrEmpty(shop.AutomationStatus),
                "Busy incubator visibly defers a new request without extra collection or replacing its specimen");
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Check(shop.CancelAutomationBatch(), "Cancel waiting preparation at the physical incubator while current incubation continues");
            yield return AutomationWait(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 15, "ordinary manual-start incubation");
            Check(shop.Model.Incubator.SpecimenId == produced && !shop.Automation.IsPending && shop.Model.Currency == 216,
                "Finished specimen remains in the incubator with no automated transport or payment");
            yield return LabTransferReady("tank-b", produced);
            Check(shop.Model.GetTank("tank-b").Value.Id == produced, "Manual transfer preserves the exact completed identity");

            yield return AutomationEnter();
            Check(shop.SetAutomationEnabled(true, false) && shop.RequestAutomationBatch(), "Request doser-only work with an empty reservoir");
            guardedIngredients = TechnicianIngredients(); yield return new WaitForSeconds(.9f);
            Check(shop.Automation.IsPending && TechnicianIngredients() == guardedIngredients && !string.IsNullOrEmpty(shop.AutomationStatus),
                "Doser-only waits visibly for manually collected ingredients without creating cultures");
            TechnicianCollect("pondlet");
            yield return AutomationWait(() => !shop.Automation.IsPending, 5, "doser-only manually supplied recipe");
            Check(shop.Model.Staged(CultureKind.Bloom) == 1 && shop.Model.Staged(CultureKind.Flow) == 1 &&
                shop.Model.ReservedCultureCount == 2, "Manual input delivery lets the waiting doser prepare exactly one recipe");
            Ok(shop.Perform(shop.Model.ClearDoses()), "Return doser-only probe ingredients"); AutomationReleaseFixture();
            Check(shop.SetAutomationEnabled(true, true), "Re-enable collector before its independent batch");
            Check(shop.SetAutomationEnabled(false, false) && shop.RequestAutomationBatch(), "Request a collector-only batch");
            yield return AutomationWait(() => !shop.Automation.IsPending, 10, "collector-only recipe reservoir");
            Check(shop.Model.Available(CultureKind.Bloom) == 1 && shop.Model.Available(CultureKind.Flow) == 1 &&
                shop.Model.Staged(CultureKind.Bloom) == 0 && shop.Model.Staged(CultureKind.Flow) == 0,
                "Collector-only completion leaves the exact requested ingredients in the shared reservoir");
            Check(shop.SetAutomationEnabled(true, false) && shop.SetAutomationEnabled(false, true) && shop.RequestAutomationBatch(),
                "Request doser-only work against already available ingredients");
            yield return AutomationWait(() => !shop.Automation.IsPending, 5, "doser-only preparation");
            Check(shop.Model.Available(CultureKind.Bloom) == 0 && shop.Model.Available(CultureKind.Flow) == 0 &&
                shop.Model.Staged(CultureKind.Bloom) == 1 && shop.Model.Staged(CultureKind.Flow) == 1,
                "Doser-only transfers existing ingredients exactly once without hidden culture supply");
            Ok(shop.Perform(shop.Model.ClearDoses()), "Return prepared doses before the manual-mixture safety probe");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Player stages one deliberate manual Bloom");
            Check(shop.SetAutomationEnabled(true, true) && shop.RequestAutomationBatch(), "Request both modules with a manual mixture present");
            guardedIngredients = TechnicianIngredients(); yield return new WaitForSeconds(.9f);
            Check(shop.Automation.IsPending && TechnicianIngredients() == guardedIngredients && !string.IsNullOrEmpty(shop.AutomationStatus),
                "Automation waits visibly without replacing or adding to the player's manual mixture");
            yield return TutorialCapture("604-automation-manual-mixture");
            Check(shop.CancelAutomationBatch() && TechnicianIngredients() == guardedIngredients,
                "Cancellation leaves collected cultures and manual doses exactly where the player put them");
            Ok(shop.Perform(shop.Model.ClearDoses()), "Player explicitly returns their manual mixture");
            AutomationReleaseFixture();
            // Fill with the wrong ingredient through ordinary inventory commands; refill time is fixture-only.
            for (int i = 0; i < shop.Model.Capacity; i++)
            {
                if (shop.Model.GetGrower(CultureKind.Bloom).Units == 0) shop.Model.Tick(shop.Config.GrowerRefillSeconds);
                Ok(shop.Model.Collect(CultureKind.Bloom), "Collect labelled all-Bloom full-reservoir fixture");
            }
            Check(shop.RequestAutomationBatch(), "Queue one recipe against a full wrong-ingredient buffer");
            guardedIngredients = TechnicianIngredients(); yield return new WaitForSeconds(.9f);
            Check(shop.Automation.IsPending && shop.Model.ReservedCultureCount == 8 && TechnicianIngredients() == guardedIngredients &&
                !string.IsNullOrEmpty(shop.AutomationStatus), "Full buffer blocks visibly without deleting surplus cultures or inventing the missing Flow");
            yield return TutorialCapture("605-automation-full-buffer");
            Check(shop.CancelAutomationBatch() && TechnicianIngredients() == guardedIngredients, "Cancel full-buffer request without discarding its eight cultures");
            AutomationReleaseFixture();
            yield return AutomationSaveRecovery();
            yield return AutomationTechnicianPriority();
            yield return AutomationClosingGuard();
            yield return AutomationNightPriorityBoundary();
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.Model.GetTank("tank-a").Value.Id == discovery &&
                shop.Model.GetTank("tank-b").Value.Id == produced && !shop.Automation.IsPending,
                "Final isolated save retains both exact specimens, paid modules and no outstanding request");
            File.Copy(shop.SavePath, Path.Combine(evidence, "automation-progress.json"), true);
            yield return AutomationTargetPolicy();
        }

        IEnumerator AutomationTargetPolicy()
        {
            yield return TechnicianEnter();
            if (shop.Laboratory.Employee.Hired)
                Check(shop.DismissTechnician() && !shop.Laboratory.Employee.Hired && shop.Laboratory.Queue.Length == 0,
                    "Dismiss Moss before configuring the labelled no-staff target probe");
            Check(!shop.Laboratory.Employee.Hired && shop.Laboratory.Queue.Length == 0,
                "Target-policy missing-staff fixture has no worker or queued order");
            yield return AutomationEnter();

            // Queue-priority recovery needs a second known recipe. Keep it in this isolated fixture:
            // free one display, discover Bubblebelly through a normal model recipe, then sell that fixture fish.
            if (!shop.Model.IsDiscovered("bubblebelly"))
            {
                Check(shop.Model.GetTank("tank-b").HasValue, "The labelled Pondlet fixture occupies the second display before the queue probe");
                if (shop.Model.GetTank("tank-b").HasValue)
                    Ok(shop.Model.SellFromTank("tank-b"), "Clear the labelled Pondlet fixture display for a second known recipe");
                SpecimenProduce("bubblebelly"); string bubble = shop.Model.Incubator.SpecimenId;
                Check(shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.Model.Incubator.SpeciesId == "bubblebelly",
                    "Produce a labelled Bubblebelly solely to exercise strict Moss queue ordering");
                Ok(shop.Model.TransferToTank("tank-b"), "Move the queue-priority Bubblebelly fixture to the freed display");
                Ok(shop.Model.SellFromTank("tank-b"), "Clear the queue-priority fixture while retaining its discovery");
                Check(!string.IsNullOrEmpty(bubble) && shop.Model.IsDiscovered("bubblebelly") &&
                    !shop.Model.GetTank("tank-b").HasValue,
                    "Queue-priority fixture leaves Bubblebelly known and the second display empty");
            }

            int before = AutomationStockCount("pondlet");
            Check(before < ShopAutomation.MaximumDesiredStockCount, "Target-policy fixture has room below the bounded stock cap");
            int goal = before + 1;
            int waitingCredits = shop.Model.Currency;
            string waitingIngredients = TechnicianIngredients();
            Check(shop.SetAutomationDestination("tank-a") && shop.Stocking.GetTarget("tank-a") == "pondlet",
                "Destination selection saves the matching Nori display target without replacing the current creature");
            Check(shop.SetAutomationDesiredStock(goal), "Set a stock target one specimen above current owned inventory");
            Check(shop.SetAutomationMaintainEnabled(true) && shop.Automation.MaintainEnabled,
                "Enable the saved recipe, destination and desired-stock policy at the physical bench with Moss absent");
            yield return TutorialCapture("610-automation-stock-target");
            yield return new WaitForSeconds(.8f);
            string missingMossStatus = shop.AutomationStatus;
            Check(!shop.Laboratory.Employee.Hired && shop.Laboratory.Queue.Length == 1 &&
                AutomationQueuedStarts("pondlet") == 1 && !shop.Automation.IsPending &&
                AutomationStockCount("pondlet") == before && shop.Model.Currency == waitingCredits &&
                TechnicianIngredients() == waitingIngredients &&
                missingMossStatus.IndexOf("not hired", StringComparison.OrdinalIgnoreCase) >= 0,
                "Missing Moss retains one saved target order with a clear hire message, no active request and unchanged stock, cash and cultures");

            yield return TechnicianEnter();
            Check(shop.HireTechnician() && !shop.Laboratory.Employee.Scheduled,
                "Hire Moss off schedule while retaining the desired stock target");
            yield return AutomationEnter();
            yield return new WaitForSeconds(.6f);
            string offDutyStatus = shop.AutomationStatus;
            Check(shop.Laboratory.Employee.Hired && !shop.Laboratory.Employee.IsOnDuty(shop.Cycle.DayNumber) &&
                shop.Laboratory.Queue.Length == 1 && AutomationQueuedStarts("pondlet") == 1 && !shop.Automation.IsPending &&
                AutomationStockCount("pondlet") == before && shop.Model.Currency == waitingCredits &&
                TechnicianIngredients() == waitingIngredients &&
                offDutyStatus.IndexOf("off duty", StringComparison.OrdinalIgnoreCase) >= 0,
                "Off-duty Moss is explained; the single waiting target order cannot start, duplicate or consume stock, cash or cultures");

            Check(shop.SetAutomationPaused(true) && shop.Automation.Paused,
                "Hold the target helper while Moss's funded schedule is changed");
            yield return TechnicianEnter();
            Check(shop.SetTechnicianScheduled(true) && shop.Laboratory.Employee.IsOnDuty(shop.Cycle.DayNumber),
                "Fund Moss before allowing the target policy to queue work");

            var technician = shop.Technician;
            bool technicianWasEnabled = technician && technician.enabled;
            if (technician) technician.enabled = false;
            try
            {
                yield return AutomationEnter();
                Check(shop.SetAutomationPaused(false), "Resume the target helper with scheduled Moss and its preserved single target order");
                yield return AutomationWait(() => shop.Automation.IsPending && AutomationQueuedStarts("pondlet") == 1, 8,
                    "one target-queued Pondlet and its stable policy request");
                string request = shop.Automation.RequestId;
                Check(!string.IsNullOrEmpty(request) && shop.Automation.MaintainEnabled &&
                    shop.Automation.PolicyHandoffWaiting && AutomationQueuedStarts("pondlet") == 1,
                    "Target deficit creates one matching Moss start and one durable helper request");
                Check(shop.SetAutomationPaused(true) && shop.Automation.Paused,
                    "Freeze the helper request while testing player queue edits");

                string held = TechnicianIngredients();
                int heldStock = AutomationStockCount("pondlet");
                int heldCredits = shop.Model.Currency;
                yield return TechnicianEnter();
                Check(shop.QueueTechnicianRecipe("bubblebelly") && shop.MoveTechnicianPriority("bubblebelly", -1) &&
                    shop.Laboratory.Queue.Length == 2 && shop.Laboratory.Queue[0].speciesId == "bubblebelly" &&
                    shop.Laboratory.Queue[0].count == 1 && shop.Laboratory.Queue[1].speciesId == "pondlet" &&
                    shop.Laboratory.Queue[1].count == 1,
                    "Reorder an unrelated known recipe ahead of the one target-owned Moss start");
                yield return AutomationEnter();
                Check(shop.SetAutomationPaused(false), "Briefly inspect target status against the reordered queue");
                string priorityStatus = shop.AutomationStatus;
                Check(priorityStatus.IndexOf(shop.Model.GetSpecies("bubblebelly").DisplayName,
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    shop.Automation.RequestId == request && shop.Automation.PolicyHandoffWaiting,
                    "The active target explains higher-priority Moss work without replacing its request");
                Check(shop.SetAutomationPaused(true), "Re-freeze the helper before queue recovery checks");

                yield return TechnicianEnter();
                Check(shop.RemoveTechnicianRecipe("bubblebelly") && shop.Laboratory.Queue.Length == 1 &&
                    shop.Laboratory.Queue[0].speciesId == "pondlet" && AutomationQueuedStarts("pondlet") == 1,
                    "Cancel the unrelated higher-priority recipe and retain exactly one target-owned start");
                Check(shop.RemoveTechnicianRecipe("pondlet") && shop.Laboratory.Queue.Length == 0 &&
                    shop.Automation.IsPending && shop.Automation.RequestId == request,
                    "Cancel the matching target order without silently replacing or losing its in-flight request");
                yield return AutomationEnter();
                yield return new WaitForSeconds(.8f);
                Check(shop.Automation.MaintainEnabled && shop.Automation.Paused && shop.Automation.IsPending &&
                    shop.Automation.RequestId == request && shop.Laboratory.Queue.Length == 0 &&
                    AutomationStockCount("pondlet") == heldStock && shop.Model.Currency == heldCredits &&
                    TechnicianIngredients() == held,
                    "A canceled target stays queued nowhere while paused; request, stock, cash and every culture remain exact");

                yield return TechnicianEnter();
                Check(shop.QueueTechnicianRecipe("pondlet") && shop.Laboratory.Queue.Length == 1 &&
                    shop.Laboratory.Queue[0].speciesId == "pondlet" && AutomationQueuedStarts("pondlet") == 1,
                    "Restore exactly one matching start after the explicit queue cancellation");
                yield return AutomationEnter();
                yield return new WaitForSeconds(.8f);
                Check(shop.Automation.RequestId == request && shop.Automation.PolicyHandoffWaiting &&
                    shop.Laboratory.Queue.Length == 1 && AutomationQueuedStarts("pondlet") == 1 &&
                    AutomationStockCount("pondlet") == heldStock && shop.Model.Currency == heldCredits &&
                    TechnicianIngredients() == held,
                    "Target recovery does not duplicate a restored request/order or mutate its blocked resources");

                shop.SetPanel(ShopPanel.Pause);
                Check(shop.SaveNow() && shop.ReloadSaved() && shop.Automation.MaintainEnabled && shop.Automation.Paused &&
                    shop.Automation.DesiredStockCount == goal && shop.Automation.DestinationTankId == "tank-a" &&
                    shop.Automation.RequestId == request && shop.Automation.PolicyHandoffWaiting &&
                    shop.Laboratory.Queue.Length == 1 && AutomationQueuedStarts("pondlet") == 1,
                    "Save/reload retains the paused target, exact request identity and exactly one matching Moss start");
                yield return AutomationEnter();
                yield return new WaitForSeconds(.8f);
                string pausedStatus = shop.AutomationStatus;
                Check(shop.Laboratory.Queue.Length == 1 && shop.Automation.RequestId == request &&
                    TechnicianIngredients() == held && AutomationStockCount("pondlet") == heldStock &&
                    shop.Model.Currency == heldCredits &&
                    pausedStatus.IndexOf("paus", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Paused reload preserves queue, request, inventory and clear hold status without dosing or duplicate work");

                if (technician) technician.enabled = technicianWasEnabled;
                Check(shop.SetAutomationPaused(false), "Resume the saved target request after queue recovery");
                yield return AutomationWait(() => shop.Model.Incubator.Phase == IncubatorPhase.Incubating &&
                    shop.Model.Incubator.SpeciesId == "pondlet" && !shop.Automation.PolicyHandoffWaiting, 30,
                    "Moss accepts and starts the exact queued Pondlet once");
                Check(AutomationStockCount("pondlet") == goal,
                    "Incubating output counts toward the exact desired level as soon as Moss starts it");
                yield return new WaitForSeconds(1.2f);
                Check(shop.Laboratory.Queue.Length == 0 && !shop.Automation.IsPending &&
                    AutomationStockCount("pondlet") == goal && shop.AutomationStatus.Contains("Target met"),
                    "A satisfied target does not enqueue a duplicate specimen while the incubator is occupied");
                Check(shop.SetAutomationMaintainEnabled(false) && !shop.Automation.MaintainEnabled,
                    "Stopping refills preserves the already started specimen and prevents future target orders");
            }
            finally
            {
                if (technician) technician.enabled = technicianWasEnabled;
            }
        }
        int AutomationStockCount(string speciesId)
        {
            int count = 0;
            foreach (string id in shop.Model.TankIds)
            { var item = shop.Model.GetTank(id); if (item.HasValue && item.Value.SpeciesId == speciesId) count++; }
            foreach (string id in shop.Model.StorageIds)
            { var item = shop.Model.GetStorage(id); if (item.HasValue && item.Value.SpeciesId == speciesId) count++; }
            var incubator = shop.Model.Incubator;
            if ((incubator.Phase == IncubatorPhase.Incubating || incubator.Phase == IncubatorPhase.Ready) && incubator.SpeciesId == speciesId) count++;
            return count;
        }
        int AutomationQueuedStarts(string speciesId)
        {
            int count = 0;
            foreach (var order in shop.Laboratory.Queue)
                if (order.speciesId == speciesId) count += order.count;
            return count;
        }
        bool PolicySpecimenStarted(int goal) => shop.Model.Incubator.Phase == IncubatorPhase.Incubating &&
            shop.Model.Incubator.SpeciesId == "pondlet" && AutomationStockCount("pondlet") == goal;

        IEnumerator AutomationSaveRecovery()
        {
            Check(shop.RequestAutomationBatch(), "Start isolated pending request before its collection write fault");
            string request = shop.Automation.RequestId, ingredients = TechnicianIngredients();
            string file = File.ReadAllText(shop.SavePath); int money = shop.Model.Currency; var model = shop.Model;
            TutorialBlockSave();
            try
            {
                yield return AutomationWait(() => shop.AutomationBlockedBySave, 5, "failed ordinary collection save");
                Check(ReferenceEquals(model, shop.Model) && shop.Automation.RequestId == request && TechnicianIngredients() == ingredients &&
                    shop.Model.Currency == money && File.ReadAllText(shop.SavePath) == file,
                    "Failed work write retains original live authority, request, cultures, money and saved bytes");
                Check(!shop.RetryAutomation() && shop.AutomationBlockedBySave, "Explicit retry remains blocked while storage is still obstructed");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.SaveNow() && shop.AutomationBlockedBySave, "An ordinary successful autosave does not silently unlatch blocked automation");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.ReloadSaved() && shop.AutomationBlockedBySave && shop.Automation.RequestId == request &&
                TechnicianIngredients() == ingredients,
                "Reloading the last good pending slot cannot bypass the existing explicit-retry latch");
            yield return AutomationEnter();
            yield return new WaitForSeconds(.9f);
            Check(shop.Automation.RequestId == request && TechnicianIngredients() == ingredients,
                "Recovered filesystem alone does not run the saved request before explicit retry");
            yield return TutorialCapture("606-automation-save-blocked");
            Check(shop.RetryAutomation() && !shop.AutomationBlockedBySave, "Successful explicit retry resumes the same preserved request");
            yield return AutomationWait(() => !shop.Automation.IsPending, 10, "one preparation after save recovery");
            Check(shop.Model.Staged(CultureKind.Bloom) == 1 && shop.Model.Staged(CultureKind.Flow) == 1 &&
                shop.Model.ReservedCultureCount == 2 && shop.Model.Currency == money,
                "Recovered request stages one exact recipe without duplicate resources or charges");
            Ok(shop.Perform(shop.Model.ClearDoses()), "Return recovery batch before a cancellation/reload probe");
            Check(shop.RequestAutomationBatch(), "Create a fresh distinct request for immediate cancellation");
            Check(shop.Automation.RequestId != request, "A subsequent batch receives a different stable request identity");
            ingredients = TechnicianIngredients(); Check(shop.CancelAutomationBatch(), "Cancel before the next automation frame");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && !shop.Automation.IsPending && TechnicianIngredients() == ingredients,
                "Canceled request stays canceled across reload while the exact collected resources remain available");
            yield return AutomationEnter();
            yield return new WaitForSeconds(.9f);
            Check(!shop.Automation.IsPending && TechnicianIngredients() == ingredients, "Reloaded cancellation cannot replay dosing");
            AutomationReleaseFixture();
        }

        IEnumerator AutomationTechnicianPriority()
        {
            int money = shop.Model.Currency;
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            yield return TechnicianEnter();
            Check(shop.HireTechnician() && shop.SetTechnicianScheduled(true) && shop.QueueTechnicianRecipe("pondlet") &&
                shop.Laboratory.Employee.IsOnDuty(shop.Cycle.DayNumber) && shop.Model.Currency == money - 36,
                "Buy one thirty-two-credit rack and pay Moss four credits for an ordinary queued known recipe");
            yield return AutomationEnter(); Check(shop.RequestAutomationBatch(), "Request automation while paid Moss owns queued work");
            string request = shop.Automation.RequestId;
            Check(!string.IsNullOrEmpty(request) && shop.Laboratory.Queue.Length == 1 && !shop.Model.TechnicianClaim.HasValue,
                "Moss leaves the matching queued start unclaimed while the durable helper request owns the handoff");
            yield return AutomationWait(() => !shop.Automation.IsPending, 10, "collector-fed matching Moss handoff");
            yield return TechnicianWait(() => shop.Technician.CurrentJobId != null, 8, "real technician claim ahead of automation");
            Check(shop.Model.TechnicianClaim.HasValue && !shop.Automation.IsPending &&
                shop.Model.Staged(CultureKind.Bloom) == 0 && shop.Model.Staged(CultureKind.Flow) == 0,
                "Actual technician claim consumes the helper-fed cultures once, while the helper neither stages nor duplicates Moss's mixture");
            var incubator = StationOf(StationKind.Incubator); incubator.enabled = false;
            try
            {
                yield return TechnicianEnter();
                Check(shop.SetTechnicianPaused(true) && shop.CancelTechnicianAssignments(), "Explicitly pause Moss and release the unused assignment");
                Check(!shop.Model.TechnicianClaim.HasValue && shop.Model.Incubator.Phase == IncubatorPhase.Dosing,
                    "Releasing the claim leaves the player-owned incubator unstarted");
                yield return AutomationEnter();
                string ingredients = TechnicianIngredients();
                Check(!shop.Automation.IsPending && TechnicianIngredients() == ingredients,
                    "The completed matching handoff stays complete after worker release without changing its remaining cultures");
            }
            finally { if (incubator) incubator.enabled = true; }
            AutomationReleaseFixture();
        }

        IEnumerator AutomationClosingGuard()
        {
            var incubator = StationOf(StationKind.Incubator); incubator.enabled = false;
            try
            {
                Check(shop.RequestAutomationBatch(), "Prepare one waiting batch before the ordinary open/close transition");
                yield return EnterCounter(); Check(shop.OpenForDay() && shop.CloseForNight(), "Open and close an empty ordinary shift through the counter");
            }
            finally { if (incubator) incubator.enabled = true; }
            string request = shop.Automation.RequestId, ingredients = TechnicianIngredients();
            Check(shop.Cycle.Phase == ShopDayPhase.Closing, "Closing probe enters the actual timed closing phase");
            yield return new WaitForSeconds(.9f);
            Check(shop.Cycle.Phase == ShopDayPhase.Closing && shop.Automation.RequestId == request &&
                TechnicianIngredients() == ingredients, "Closing blocks collection and dosing without discarding pending work");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Automation.RequestId == request && TechnicianIngredients() == ingredients,
                "Paused closing reload retains the same request without processing it");
            // Bench cancellation is permitted during closing; disable the machine while approaching to keep the probe bounded.
            incubator = StationOf(StationKind.Incubator); incubator.enabled = false;
            try
            {
                yield return AutomationEnter();
                Check(shop.CancelAutomationBatch() && !shop.Automation.IsPending && TechnicianIngredients() == ingredients,
                    "Explicit cancellation safely ends the closing request without deleting inventory");
            }
            finally { if (incubator) incubator.enabled = true; }
            yield return AutomationWait(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "ordinary closing reaches next preparation night");
        }

        IEnumerator AutomationNightPriorityBoundary()
        {
            limitations.Add("A separate labelled duty-boundary probe disables Moss's component and advances the production session once by closing duration plus 0.5 seconds. This deterministic long-frame probe verifies wage-before-automation ordering; it is not ordinary clock pacing or active worker travel evidence. The earlier close/reload route runs on ordinary frames.");
            var incubator = StationOf(StationKind.Incubator); shop.Technician.enabled = false;
            try
            {
                yield return TechnicianEnter();
                Check(shop.SetTechnicianPaused(false) && shop.QueueTechnicianRecipe("pondlet") &&
                    shop.Laboratory.Employee.IsOnDuty(shop.Cycle.DayNumber),
                    "Schedule one paid Moss order while its component is explicitly disabled for the boundary probe");
                TechnicianCollect("pondlet");
                yield return AutomationEnter(); Check(shop.RequestAutomationBatch(), "Queue one prepared-input helper request behind the paid Moss order");
                string ingredients = TechnicianIngredients(), request = shop.Automation.RequestId;
                yield return EnterCounter(); Check(shop.OpenForDay() && shop.CloseForNight(), "Begin a second actual closing transition for the long-frame priority probe");
                int day = shop.Cycle.DayNumber, money = shop.Model.Currency;
                shop.Advance(ShopDayCycle.DefaultClosingSeconds + .5f);
                Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == day + 1 &&
                    shop.Laboratory.Employee.IsOnDuty(day + 1) && shop.Laboratory.Employee.WagesForDay(day + 1) == 4 &&
                    shop.Model.Currency == money - 4 && shop.Automation.RequestId == request &&
                    TechnicianIngredients() == ingredients && shop.Model.Staged(CultureKind.Bloom) == 0 &&
                    shop.Model.Staged(CultureKind.Flow) == 0 && shop.Laboratory.Queue.Length == 1,
                    "New-night wages are committed before a long-frame helper tick, so newly paid Moss priority prevents dosing its exact inputs");
                incubator.enabled = false;
                yield return TechnicianEnter();
                Check(shop.SetTechnicianPaused(true) && shop.CancelTechnicianAssignments(), "Pause and release the boundary probe's paid technician order");
                yield return AutomationEnter();
                Check(shop.CancelAutomationBatch() && TechnicianIngredients() == ingredients,
                    "Cancel the boundary helper request without duplicating or removing the manually supplied ingredients");
                AutomationReleaseFixture();
            }
            finally
            {
                if (incubator) incubator.enabled = true;
                if (shop.Technician) shop.Technician.enabled = true;
            }
        }

        IEnumerator AutomationEnter()
        {
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            Check(shop.OpenAutomationMenu() && shop.Panel == ShopPanel.Automation, "Physical research bench opens its automation controls");
        }

        IEnumerator AutomationWait(Func<bool> condition, float timeout, string reason)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Automation waiting for " + reason + " / " + shop.AutomationStatus +
                        " / request=" + shop.Automation.RequestId + " / inputs=" + TechnicianIngredients());
                yield return null;
            }
        }

        void AutomationReleaseFixture()
        {
            Check(!shop.Automation.IsPending && shop.Model.Incubator.Phase == IncubatorPhase.Dosing &&
                shop.Model.Staged(CultureKind.Bloom) == 0 && shop.Model.Staged(CultureKind.Flow) == 0,
                "Deliberate fixture release requires no pending request, active production or staged doses");
            foreach (CultureKind kind in new[] { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark, CultureKind.Mist })
                while (shop.Model.Available(kind) > 0) Ok(shop.Model.ReleaseCulture(kind), "Explicit fixture-only culture release / " + kind);
            Check(shop.Model.ReservedCultureCount == 0, "Labelled release fixture leaves the shared reservoir empty");
        }
    }
}
#endif
