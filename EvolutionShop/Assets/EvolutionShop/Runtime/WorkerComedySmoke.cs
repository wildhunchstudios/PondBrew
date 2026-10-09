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
        IEnumerator WorkerComedyScenario()
        {
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionWorkerComedySmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Worker comedy uses only the explicitly isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors == 2 && !shop.Customers.UseBaseReputationEffectsForValidation,
                "Worker scenario retains normal customer concurrency and reputation rules");
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionWorkerComedyKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionWorkerComedyMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            limitations.Add("R07.5 uses one labelled 300-credit construction/wage fixture and one accelerated manual Pondlet discovery. Guarded session staff commands, synthetic station E input, selected arrivals and scripted player/camera positions are diagnostic controls. At the technician maturity boundary a Journal open/close suppresses the independent pending belch so this isolated completion probe has no competing incidental gag. Moss's own recipe, both Nori deliveries and Pip's sale complete on ordinary frames and trigger their own production hooks. Explicit QueueWorkerCelebration requests and presentation-only Comedy.Tick(120) calls below exercise eligibility/interruption guards, not completion or cooldown-pacing evidence. Screens are actual standalone surfaces at 720p/1080p, or labelled Editor diagnostic captures. No native-input, human humor/audio, ordinary economy, exhaustive-layout or performance acceptance is claimed.");
            Check(!shop.TryStartComedy(ComedyKind.WorkerCelebration), "Generic comedy API cannot manufacture worker-completion feedback");
            shop.QueueWorkerCelebration("missing-worker");
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null,
                "Unknown employee cannot queue or start a completion gag");
            Check(shop.SkipTutorial(), "Explicitly skip guidance in this isolated staffing fixture");
            StaffCreditFixture(300, "R07.5 worker presentation and interruption checks");
            yield return StaffEnter();
            Check(shop.HireCashier() && shop.SetCashierScheduled(true), "Hire and pay Pip through normal staff controls");
            yield return StockerEnter();
            Check(shop.HireStocker() && shop.SetStockerScheduled(true) && shop.SetStockerPaused(true),
                "Hire and pay Nori, paused during fixture construction");
            yield return TechnicianEnter();
            Check(shop.HireTechnician() && shop.SetTechnicianScheduled(true) && shop.SetTechnicianPaused(true),
                "Hire and pay Moss, paused during fixture construction");
            Check(shop.Model.Currency == 288, "Three independent role wages cost twelve credits once");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string rack = StockerRack(); string[] slots = shop.Layout.GetStorageSlotIds(rack);
            Check(slots.Length == 2, "One paid laboratory rack provides two exact specimen locations");
            SpecimenProduce("pondlet"); string manualSpecimen = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]), "Store labelled manual discovery in the first rack compartment");
            yield return TechnicianEnter();
            Check(shop.QueueTechnicianRecipe("pondlet"), "Assign one known Pondlet to Moss");
            TechnicianCollect("pondlet");
            Check(shop.SetTechnicianPaused(false), "Resume paid Moss for an actual timed production job");
            WorkerComedyView(ShopStaff.TechnicianId);
            int comedyBefore = shop.ComedyStartedCount;
            yield return TechnicianWait(() => shop.Technician.StartedJobs == 1, 25, "one real recipe start");
            Check(shop.Technician.CompletedJobs == 0 && shop.CelebratingWorkerId == null &&
                shop.PendingWorkerCelebrationId == null && shop.ComedyStartedCount == comedyBefore,
                "Starting incubation is not a completed technician delivery and does not celebrate");
            string producedSpecimen = shop.Laboratory.ActiveSpecimenId;
            yield return TechnicianWait(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 30, "ordinary recipe maturity");
            // Suppress the independent pending belch in this presentation-isolation fixture.
            shop.SetPanel(ShopPanel.Journal); shop.SetPanel(ShopPanel.None); shop.Comedy.Tick(120);
            yield return TechnicianWait(() => shop.Technician.CompletedJobs == 1, 30, "ordinary rack delivery");
            yield return WaitFor(() => shop.CelebratingWorkerId == ShopStaff.TechnicianId, 3, "actual Moss completion celebration");
            Check(shop.Model.GetStorage(slots[1]).Value.Id == producedSpecimen && shop.Laboratory.ActiveSpecimenId == null &&
                shop.Technician.CurrentJobId == null && !shop.Model.TechnicianClaim.HasValue,
                "Moss celebrates only after exact output is durably in its rack and claims are released");
            yield return WorkerComedyVisualProof(ShopStaff.TechnicianId, "500-moss-completion");
            shop.SkipComedy();
            Check(shop.CelebratingWorkerId == null && !shop.Comedy.Active && shop.Technician.CompletedJobs == 1,
                "Skipping Moss's gag does not undo or repeat its completed job");

            yield return StockerEnter();
            Check(shop.SetStockingTarget("tank-a", "pondlet") && shop.SetStockingTarget("tank-b", "pondlet") &&
                shop.SetStockerPaused(false), "Nori receives two normal display targets and resumes");
            WorkerComedyView(ShopStaff.StockerId); shop.Comedy.Tick(120);
            yield return StockerWait(() => shop.Stocker.CompletedJobs == 1, 40, "first real display delivery");
            yield return WaitFor(() => shop.CelebratingWorkerId == ShopStaff.StockerId, 3, "actual Nori completion celebration");
            Check(shop.Model.GetTank("tank-a").Value.Id == manualSpecimen && !shop.Model.GetStorage(slots[0]).HasValue &&
                shop.Model.GetStorage(slots[1]).Value.Id == producedSpecimen && shop.Stocker.CurrentJobId == null,
                "Nori celebrates the first committed exact transfer without owning a second copy");
            yield return WorkerComedyVisualProof(ShopStaff.StockerId, "501-nori-completion");
            yield return StockerWait(() => shop.Stocker.CurrentJobId != null, 5, "next target claimed without waiting for the gag");
            Check(shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null && !shop.Comedy.Active,
                "Nori's next ordinary job cancels presentation rather than waiting for the dance");
            string interruptedJob = shop.Stocker.CurrentJobId;
            int paidMoney = shop.Model.Currency;
            yield return StockerEnter();
            Check(shop.DismissStocker() && !shop.Stocking.Employee.Hired && shop.Stocker.CurrentJobId == null &&
                !shop.Model.GetStockTransfer(interruptedJob).HasValue && shop.Model.GetStorage(slots[1]).Value.Id == producedSpecimen,
                "Dismissing Nori during its second claimed job releases ownership and preserves the exact source");
            Check(shop.HireStocker() && shop.SetStockerScheduled(true) && shop.Model.Currency == paidMoney &&
                shop.Stocking.Employee.WagesForDay(1) == 4,
                "Reactivating Nori in the same night preserves its wage receipt without a second charge");
            WorkerComedyView(ShopStaff.StockerId); shop.Comedy.Tick(120);
            shop.QueueWorkerCelebration(ShopStaff.StockerId);
            Check(shop.PendingWorkerCelebrationId == ShopStaff.StockerId, "Idle reactivated Nori accepts a diagnostic pending request before its next work frame");
            yield return StockerWait(() => shop.Stocker.CurrentJobId != null, 5, "work preempts the pending diagnostic request");
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null,
                "Claiming real work discards pending feedback without replay or presentation delay");
            yield return StockerWait(() => shop.Stocker.CompletedJobs == 2, 40, "safe second delivery after reactivation");
            Check(shop.Model.GetTank("tank-b").Value.Id == producedSpecimen && !shop.Model.GetStorage(slots[1]).HasValue,
                "Reactivated Nori completes one fresh job with the original second specimen");
            shop.CancelComedy();
            yield return WorkerComedyGuardChecks();

            yield return EnterCounter(); Check(shop.OpenForDay(), "Open the normally paid three-worker day");
            WorkerComedyView(ShopStaff.CashierId); shop.Comedy.Tick(120);
            int saleMoney = shop.Model.Currency;
            var visitor = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => visitor.Outcome == CustomerOutcome.Sold, 35, "real walking customer and Pip service");
            yield return WaitFor(() => shop.CelebratingWorkerId == ShopStaff.CashierId, 3, "actual Pip completion celebration");
            Check(shop.Model.Currency == saleMoney + visitor.Price && shop.Reports.Current.sales == 1 &&
                shop.Cashier.Target == null && !shop.ServeCashier(visitor),
                "Pip celebrates one already committed exact payment and cannot pay the old visitor again");
            yield return WorkerComedyVisualProof(ShopStaff.CashierId, "502-pip-completion");
            shop.CancelComedy();
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 15, "paid customer leaving");
            yield return StaffEnter(); Check(shop.SetCashierPaused(true), "Pause Pip while a second real order queues");
            WorkerComedyView(ShopStaff.CashierId);
            var nextVisitor = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => nextVisitor.Phase == CustomerPhase.Queued, 30, "second real order queued");
            yield return StaffEnter(); Check(shop.SetCashierPaused(false), "Resume Pip with queued work waiting");
            WorkerComedyView(ShopStaff.CashierId); shop.CancelComedy(); shop.Comedy.Tick(120);
            int beforeSecondSale = shop.Model.Currency;
            shop.QueueWorkerCelebration(ShopStaff.CashierId);
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null && shop.Model.Currency == beforeSecondSale,
                "Queued cashier demand rejects a diagnostic gag before the next service timer starts");
            yield return WaitFor(() => nextVisitor.Outcome == CustomerOutcome.Sold, 6, "uninterrupted second checkout");
            Check(shop.Model.Currency == beforeSecondSale + nextVisitor.Price && shop.Reports.Current.sales == 2,
                "Refused presentation never delays or duplicates the second real payment");
            shop.CancelComedy();
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 15, "second paid customer leaving");
            shop.ReducedGags = true; shop.HumorVolume = 0;
            shop.CancelComedy(); shop.Comedy.Tick(120); shop.QueueWorkerCelebration(ShopStaff.CashierId);
            yield return WaitFor(() => shop.CelebratingWorkerId == ShopStaff.CashierId, 3, "reduced and muted diagnostic celebration");
            var sound = shop.GetComponent<ShopAudio>();
            Check(shop.Comedy.Reduced && shop.Comedy.Caption.Length > 0 && sound.LastComedyGain == 0,
                "Reduced muted worker feedback retains a caption and requests zero humor gain");
            shop.CancelComedy();
            foreach (int height in Application.isEditor ? new[] { 0 } : new[] { 720, 1080 })
            {
                if (height != 0) { Screen.SetResolution(height == 720 ? 1280 : 1920, height, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f); }
                yield return WorkerComedyBeginDiagnostic(ShopStaff.CashierId);
                yield return GateCapture("503-worker-reduced-muted-" + (height == 0 ? "editor" : height.ToString()));
                Check(shop.Comedy.Active && shop.Comedy.Reduced && shop.CelebratingWorkerId == ShopStaff.CashierId,
                    "Reduced worker caption remains active through its actual capture / " + height);
                shop.CancelComedy();
            }
            if (!Application.isEditor) { Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f); }
            shop.CancelComedy(); shop.ReducedGags = false; shop.HumorVolume = .65f;
            yield return WorkerComedyBeginDiagnostic(ShopStaff.CashierId);
            int beforeClosing = shop.Model.Currency;
            Check(shop.CloseForNight() && shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null,
                "Closing immediately cancels active worker feedback");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "next three-role duty period");
            Check(shop.Cycle.DayNumber == 2 && shop.Model.Currency == beforeClosing - 12 && shop.Staff.IsOnDuty(2) &&
                shop.Stocking.Employee.IsOnDuty(2) && shop.Laboratory.Employee.IsOnDuty(2),
                "All three roles complete one night/day duty and begin the next with exactly twelve credits in wages");
            int nightMoney = shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency == nightMoney &&
                shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null,
                "Next-night reload does not repay wages or replay worker feedback");
            Check(shop.SaveNow(), "Save final isolated R07.5 progress");
        }

        IEnumerator WorkerComedyVisualProof(string workerId, string name)
        {
            var actor = WorkerComedyActor(workerId);
            Vector3 position = actor.transform.position; Quaternion rotation = actor.transform.rotation;
            string stock = StockerInventory(); int count = shop.ComedyStartedCount;
            WorkerComedyView(workerId);
            if (workerId == ShopStaff.StockerId && !Application.isEditor)
            {
                // Keep this real completion capture comfortably inside the normal two-second retry.
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.15f);
                yield return GateCapture(name + "-720");
            }
            else yield return TutorialCapture(name);
            Check(shop.ComedyStartedCount == count && StockerInventory() == stock &&
                Vector3.Distance(position, actor.transform.position) < .0001f && Quaternion.Angle(rotation, actor.transform.rotation) < .001f,
                "Worker caption and joint animation leave exact stock, cash and actor root unchanged / " + workerId);
        }

        IEnumerator WorkerComedyGuardChecks()
        {
            var actor = shop.Stocker.Actor;
            string stock = StockerInventory(); int jobs = shop.Stocker.CompletedJobs;
            if (!Application.isEditor)
            {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f);
                yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
                yield return GateCapture("504-nori-diagnostic-1080");
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.25f);
            }
            yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
            shop.SkipComedy();
            Check(!shop.Comedy.Active && shop.CelebratingWorkerId == null && StockerInventory() == stock,
                "Explicit skip clears worker pose feedback without stock or money changes");
            shop.QueueWorkerCelebration(ShopStaff.StockerId);
            Check(shop.PendingWorkerCelebrationId == null, "Shared cooldown rejects immediate repeated worker feedback");
            foreach (var panel in new[] { ShopPanel.Pause, ShopPanel.Journal, ShopPanel.Arrange })
            {
                yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
                shop.SetPanel(panel);
                Check(!shop.Comedy.Active && shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null,
                    "Panel transition clears active and pending worker presentation / " + panel);
            }
            WorkerComedyView(ShopStaff.StockerId); shop.Comedy.Tick(120);
            shop.QueueWorkerCelebration(ShopStaff.StockerId);
            Check(shop.PendingWorkerCelebrationId == ShopStaff.StockerId, "Idle eligible diagnostic request enters one bounded pending slot");
            shop.SetPanel(ShopPanel.Pause); yield return new WaitForSeconds(.45f); shop.SetPanel(ShopPanel.None);
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null,
                "Pause cancels a pending request without delayed replay on resume");
            WorkerComedyView(ShopStaff.StockerId); shop.Comedy.Tick(120);
            shop.QueueWorkerCelebration(ShopStaff.StockerId);
            int countBeforeExpiry = shop.ComedyStartedCount;
            float busyUntil = Time.realtimeSinceStartup + 1.8f;
            while (Time.realtimeSinceStartup < busyUntil) { shop.GetComponent<ShopAudio>().Feedback(true); yield return null; }
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null && shop.ComedyStartedCount == countBeforeExpiry,
                "Continuously busy diagnostic service cues expire the pending gag instead of building a backlog");
            yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
            shop.GetComponent<ShopAudio>().Feedback(true); yield return null; yield return null;
            Check(!shop.Comedy.Active && shop.CelebratingWorkerId == null,
                "Priority service feedback interrupts worker presentation");
            yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
            shop.Stocker.enabled = false; yield return null;
            Check(shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null && !shop.Comedy.Active,
                "Disabled role cancels its worker gag and pending request");
            shop.Stocker.enabled = true; yield return null;
            yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
            actor.enabled = false; yield return null; yield return null;
            Check(shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null,
                "Disabled employee visual cannot retain its completion gag");
            actor.enabled = true; shop.Stocker.RefreshVisual();
            yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
            shop.enabled = false; yield return null;
            Check(!shop.Comedy.Active && shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null,
                "Disabled session owner clears worker presentation immediately");
            shop.enabled = true; yield return null;
            WorkerComedyView(ShopStaff.StockerId); shop.Comedy.Tick(120);
            shop.Player.Teleport(new Vector3(0, .2f, 4.4f), Quaternion.identity);
            shop.QueueWorkerCelebration(ShopStaff.StockerId); yield return new WaitForSeconds(.5f);
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null,
                "A front-store employee does not celebrate to a player in the separated lab");
            yield return WorkerComedyBeginDiagnostic(ShopStaff.StockerId);
            Check(shop.SaveNow() && shop.ReloadSaved(), "Save and normal reload during an active diagnostic gag succeed");
            shop.SetPanel(ShopPanel.None); yield return new WaitForSeconds(.5f);
            Check(!shop.Comedy.Active && shop.CelebratingWorkerId == null && shop.PendingWorkerCelebrationId == null &&
                StockerInventory() == stock && shop.Stocker.CompletedJobs == jobs,
                "Reload drops transient worker feedback without replay, extra delivery, money change or specimen duplication");
            yield return StockerEnter();
            Check(shop.SetStockerPaused(true), "Role pause remains available after interrupted worker feedback");
            WorkerComedyView(ShopStaff.StockerId); shop.Comedy.Tick(120); shop.QueueWorkerCelebration(ShopStaff.StockerId);
            Check(shop.PendingWorkerCelebrationId == null && shop.CelebratingWorkerId == null,
                "Paused paid employee cannot receive a celebration request");
            yield return StockerEnter(); Check(shop.SetStockerPaused(false), "Resume Nori without changing its duty receipt");
        }

        IEnumerator WorkerComedyBeginDiagnostic(string workerId)
        {
            WorkerComedyView(workerId); shop.CancelComedy(); shop.Comedy.Tick(120);
            yield return new WaitForSeconds(.85f);
            shop.QueueWorkerCelebration(workerId);
            yield return WaitFor(() => shop.CelebratingWorkerId == workerId, 3,
                "explicit presentation-only eligibility probe / " + workerId);
            Check(shop.Comedy.Kind == ComedyKind.WorkerCelebration && shop.Comedy.Active,
                "Explicit diagnostic request starts only eligible idle worker presentation / " + workerId);
        }

        ShopCharacterVisual WorkerComedyActor(string workerId) => workerId == ShopStaff.CashierId ? shop.Cashier.Actor :
            workerId == ShopStaff.StockerId ? shop.Stocker.Actor : shop.Technician.Actor;

        void WorkerComedyView(string workerId)
        {
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false;
            Vector3 position = workerId == ShopStaff.TechnicianId ? new Vector3(2, .2f, 2.5f) :
                workerId == ShopStaff.CashierId ? new Vector3(-3.9f, .2f, -3.8f) : new Vector3(1, .2f, -4.1f);
            GateAim(position, WorkerComedyActor(workerId).transform.position + Vector3.up * 1.15f);
        }
    }
}
#endif
