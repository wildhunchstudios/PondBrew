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
            limitations.Add("R08.1 uses an isolated labelled 300-credit fixture and one accelerated manual Pondlet discovery retained in its display. Bench/counter/incubator access uses synthetic E and scripted player positions; menu choices call guarded production session commands. Deliberately filled/released culture and replenished grower fixtures below isolate capacity and contention; they are labelled and are not economy evidence. Automatic collection, dosing, waits, incubation and technician claiming run on ordinary frames. Temporary disabled station/session components and an isolated .tmp-directory inject recovery conditions. Actual standalone captures explicitly switch between 1280x720 and 1920x1080; Editor camera exports are diagnostic only. No native input, human usability/audio, campaign balance, performance, continuous automation or offline progress is claimed.");
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
        }

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
            string ingredients = TechnicianIngredients(), request = shop.Automation.RequestId;
            yield return new WaitForSeconds(ShopTechnician.RetrySeconds + .3f);
            Check(shop.Automation.RequestId == request && TechnicianIngredients() == ingredients &&
                shop.Laboratory.Queue.Length == 1 && !string.IsNullOrEmpty(shop.AutomationStatus),
                "Paid queued Moss work has priority even while the technician waits for manual input cultures");
            TechnicianCollect("pondlet");
            yield return TechnicianWait(() => shop.Technician.CurrentJobId != null, 8, "real technician claim ahead of automation");
            Check(shop.Model.TechnicianClaim.HasValue && shop.Automation.RequestId == request &&
                shop.Model.Staged(CultureKind.Bloom) == 0 && shop.Model.Staged(CultureKind.Flow) == 0,
                "Actual technician claim retains the exact culture budget and automation cannot dose its machine");
            var incubator = StationOf(StationKind.Incubator); incubator.enabled = false;
            try
            {
                yield return TechnicianEnter();
                Check(shop.SetTechnicianPaused(true) && shop.CancelTechnicianAssignments(), "Explicitly pause Moss and release the unused assignment");
                Check(!shop.Model.TechnicianClaim.HasValue && shop.Model.Incubator.Phase == IncubatorPhase.Dosing,
                    "Releasing the claim leaves the player-owned incubator unstarted");
                yield return AutomationEnter();
                ingredients = TechnicianIngredients();
                Check(shop.CancelAutomationBatch() && TechnicianIngredients() == ingredients,
                    "Cancel waiting automation after worker release without taking its manually collected cultures");
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
