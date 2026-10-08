#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator TutorialCustomerScenario()
        {
            limitations.Add("TUT-07 isolated arrival-policy integration: ordinary production Tick drives the initial refusal and retry; controlled SpawnNow calls cover policy precedence and the later two-type queue. Synthetic E input and scripted physical positioning open the counter; guarded session commands perform service. No native click or human pacing acceptance is claimed.");
            limitations.Add("Explicit fixtures seed stock through existing recipe/model commands with accelerated incubation; reserved-only and disabled-navigation guards use labelled direct director ticks. A cloned domain visit proves the complete finite patience deadline without a ninety-second wall-clock wait. A separate ten-credit night fixture pays ordinary cashier wages. These fixtures establish authority/recovery, not production or economy pacing.");
            limitations.Add("Standalone captures are actual 720p/1080p surfaces; launch at 1600x900 to force the first surface resize. The scenario writes only its isolated smoke slot, and leaves a labelled worker-sale fixture at the end. Native input, fresh-process release validation and novice acceptance remain separate gates.");
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionTutorialCustomerSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Customer tutorial scenario is confined to its explicitly isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors == 2 &&
                !shop.Customers.UseBaseReputationEffectsForValidation && shop.Customers.ForcedArchetypeId == null &&
                shop.Model.Currency == 0 && shop.Cycle.Phase == ShopDayPhase.Night,
                "Fresh zero-credit night retains ordinary customer catalog, reputation and configured capacity");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTutorialCustomerKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTutorialCustomerMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.AllowValidationInput = true;
            shop.Customers.ArrivalsEnabled = false;
            Check(shop.StartGuidedOpening() && shop.Customers.IntroductoryArrivalsActive &&
                shop.Customers.EffectiveMaxVisitors == 1 && shop.AmbientComedyDeferredForTutorial && shop.AmbientComedyEnabled,
                "Guided opening activates one introductory place and defers ambient comedy without changing its setting");
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Player may open an empty guided shop through the real counter command");
            shop.SetPanel(ShopPanel.None); shop.Customers.ArrivalsEnabled = true;
            int arrivals = shop.Reports.Current.visitors;
            yield return new WaitForSeconds(8);
            Check(shop.Customers.ActiveCount == 0 && shop.Reports.Current.visitors == arrivals && shop.Model.Currency == 0,
                "Ordinary production arrival timer waits on an empty guided shop without fake visitors or payment");
            shop.Customers.ArrivalsEnabled = false;

            SeedStock("pondlet", "tank-a");
            string firstFish = shop.Model.GetTank("tank-a").Value.Id;
            Check(shop.Model.ReserveTank("tank-a", "tut07-reserved-only-fixture").Success,
                "Labelled reserved-only fixture claims the single real displayed specimen");
            shop.Customers.ArrivalsEnabled = true; shop.Customers.Tick(20);
            Check(shop.Customers.ActiveCount == 0 && shop.Reports.Current.visitors == arrivals && !shop.Customers.SpawnNow(),
                "Production tick and validation arrival both wait when every displayed specimen is already reserved");
            Check(shop.Model.ReleaseReservation("tank-a", "tut07-reserved-only-fixture").Success,
                "Reserved-only fixture releases its own claim without removing the fish");
            var navigation = shop.Customers.Navigation;
            navigation.enabled = false;
            try
            {
                shop.Customers.Tick(20);
                Check(!shop.Customers.SpawnNow() && shop.Customers.ActiveCount == 0 && shop.Model.Currency == 0,
                    "Disabled customer navigation prevents introductory arrival and leaves displayed stock unpaid");
            }
            finally { navigation.enabled = true; }
            shop.Customers.ArrivalsEnabled = false;
            var obstruction = new GameObject("TUT-07 isolated temporary browse-route obstruction");
            obstruction.transform.position = navigation.BrowseSpot("tank-a", 0) + Vector3.up;
            var obstacle = obstruction.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = new Vector3(2, 4, 2);
            obstacle.carving = true; obstacle.carveOnlyStationary = false;
            try
            {
                yield return new WaitForSeconds(1);
                Check(!navigation.CanReach(navigation.EntryPoint, navigation.BrowseSpot("tank-a", 0)),
                    "Labelled temporary carving obstacle makes the sole stocked display approach unreachable");
                shop.Customers.ArrivalsEnabled = true; shop.Customers.Tick(20);
                Check(!shop.Customers.SpawnNow() && shop.Customers.ActiveCount == 0 &&
                    shop.Model.GetTank("tank-a").Value.Id == firstFish && shop.Model.Currency == 0,
                    "Actual incomplete route makes introductory arrival wait without removing or paying for stocked fish");
                shop.Customers.ArrivalsEnabled = false;
            }
            finally { Destroy(obstruction); }
            yield return new WaitForSeconds(1);
            Check(navigation.CanReach(navigation.EntryPoint, navigation.BrowseSpot("tank-a", 0)),
                "Removing the isolated obstruction restores the existing customer route without rebuilding the scene");
            int ordinaryPrice = shop.Model.GetPrice("pondlet").Value.Standard;
            int excessivePrice = shop.Model.GetPrice("pondlet").Value.Maximum;
            Check(shop.SetSpeciesPrice("pondlet", excessivePrice).Success && shop.SaveNow(),
                "Labelled high-price fixture uses the existing species pricing command");
            shop.Customers.ArrivalsEnabled = true;
            yield return WaitFor(() => shop.Customers.ActiveCount == 1, 12, "production introductory overpriced arrival");
            var refused = shop.Customers.Visits[0];
            Check(refused.ArchetypeId == CustomerArchetypes.Casual &&
                refused.Archetype.PatienceSeconds == ShopCustomers.IntroductoryPatienceSeconds &&
                refused.Archetype.AcceptableLimit("pondlet", ordinaryPrice) < excessivePrice,
                "Production arrival is a patient Casual whose ordinary price limit still rejects the excessive tag");
            Check(!shop.Customers.SpawnNow() && shop.Customers.ActiveCount == 1,
                "Introductory visitor occupies the only arrival place while browsing");
            yield return WaitFor(() => refused.Outcome == CustomerOutcome.TooExpensive, 25, "real introductory price refusal");
            shop.Customers.ArrivalsEnabled = false;
            Check(refused.PriceRejections > 0 && !shop.Model.GetReservation("tank-a").HasValue &&
                shop.Model.GetTank("tank-a").Value.Id == firstFish && shop.Model.Currency == 0 &&
                shop.Customers.IntroductoryArrivalsActive && shop.Tutorial.FirstSaleAccommodationActive,
                "Real refusal leaves the exact fish unclaimed and unpaid and keeps introductory retry eligibility");
            AimAtVisit(refused, new Vector3(-1.1f, .2f, -4.3f));
            yield return TutorialCapture("440-tutorial-customer-price-refusal");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 15, "refused visitor leaving");
            Check(shop.SetSpeciesPrice("pondlet", ordinaryPrice).Success, "Player corrects the ordinary tag without replacing stock");
            shop.Customers.ArrivalsEnabled = true;
            yield return WaitFor(() => shop.Customers.ActiveCount == 1, 12, "production introductory retry after refusal");
            var queued = shop.Customers.Visits[0];
            shop.Customers.ArrivalsEnabled = false;
            Check(queued.Id != refused.Id && queued.ArchetypeId == CustomerArchetypes.Casual,
                "Retry is a new normal visit rather than a resurrection or forced purchase");
            yield return WaitFor(() => queued.Phase == CustomerPhase.Queued, 30, "introductory customer physically reaching checkout");
            Check(queued.SpecimenId == firstFish && queued.Price == ordinaryPrice && queued.QueueSecondsRemaining > 88 &&
                queued.Archetype.PatienceSeconds == 90 && shop.Model.Currency == 0,
                "Actual route reserves the exact fish at its normal price and enters a finite ninety-second queue without payment");
            AimAtVisit(queued, new Vector3(-1.1f, .2f, -4.3f));
            yield return TutorialCapture("441-tutorial-customer-patient-queue");
            Check(!shop.TryStartComedy(ComedyKind.Fart) && !shop.TryStartComedy(ComedyKind.Complaint) &&
                !shop.TryStartComedy(ComedyKind.Mimic) && !shop.Comedy.Active && shop.AmbientComedyEnabled,
                "Introductory queue defers ambient fart, complaint and mimic triggers while preserving the humor setting");
            yield return EnterCounter();
            yield return TutorialCapture("443-tutorial-customer-counter");
            Check(shop.OpenTutorialHelp(), "Requested help opens while the introductory order waits");
            shop.SelectTutorialHelpLesson(TutorialLesson.Serve);
            yield return TutorialCapture("444-tutorial-customer-serve-help");
            float pausedPatience = queued.QueueSecondsRemaining;
            Vector3 pausedPosition = ActorPosition(queued);
            yield return new WaitForSeconds(1);
            Check(queued.QueueSecondsRemaining == pausedPatience && ActorPosition(queued) == pausedPosition,
                "Help pauses actual queue patience and customer movement");
            shop.CloseTutorialHelp();
            yield return new WaitForSeconds(1);
            Check(queued.QueueSecondsRemaining < pausedPatience - .5f && queued.QueueSecondsRemaining > 0,
                "Returning from help resumes the real finite patience countdown");
            TutorialCustomerFiniteDeadline(queued);
            Check(shop.SaveNow(), "Save the active unpaid introductory branch before skip and reload probes");
            string activePending = File.ReadAllText(shop.SavePath);
            File.WriteAllText(Path.Combine(evidence, "tutorial-customer-active-pending.json"), activePending);
            float patienceBeforeSkip = queued.QueueSecondsRemaining;
            string agreement = JsonUtility.ToJson(queued.Archetype);
            Check(shop.SkipTutorial() && !shop.Customers.IntroductoryArrivalsActive &&
                shop.Customers.EffectiveMaxVisitors == 2 && !shop.AmbientComedyDeferredForTutorial &&
                ReferenceEquals(shop.Customers.NextToServe, queued) && queued.SpecimenId == firstFish && queued.Price == ordinaryPrice &&
                queued.QueueSecondsRemaining == patienceBeforeSkip && JsonUtility.ToJson(queued.Archetype) == agreement &&
                shop.Model.GetReservation("tank-a").Value.CustomerId == queued.Id && shop.Model.Currency == 0,
                "Skip restores future ordinary arrivals without changing the existing visit's agreed price, claim or ninety-second patience snapshot");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Tutorial.Mode == TutorialMode.Skipped &&
                !shop.Customers.IntroductoryArrivalsActive && shop.Customers.ActiveCount == 0 &&
                !shop.Model.GetReservation("tank-a").HasValue && shop.Model.GetTank("tank-a").Value.Id == firstFish &&
                shop.Model.Currency == 0 && shop.Reports.Current.sales == 0,
                "Skipped reload cancels the transient unpaid visit and releases its exact fish without payment or reactivating accommodation");

            File.WriteAllText(shop.SavePath, activePending);
            Check(shop.ReloadSaved() && shop.Customers.IntroductoryArrivalsActive &&
                shop.Customers.ActiveCount == 0 && !shop.Model.GetReservation("tank-a").HasValue && shop.Model.Currency == 0,
                "Restoring the labelled active pending branch preserves eligibility while existing reload rules cancel unpaid ownership");
            shop.SetPanel(ShopPanel.None);
            shop.Customers.ScriptedArchetypes.Enqueue(CustomerArchetypes.Premium);
            Check(shop.Customers.SpawnNow() && shop.Customers.Visits[0].ArchetypeId == CustomerArchetypes.Casual,
                "Production introductory selection takes precedence over a labelled validation type request");
            shop.Customers.ScriptedArchetypes.Clear();
            var manual = shop.Customers.Visits[0];
            yield return WaitFor(() => manual.Phase == CustomerPhase.Queued, 30, "resumed introductory order queued");
            yield return EnterCounter();
            Check(shop.ServeCustomer() && manual.Outcome == CustomerOutcome.Sold && shop.Model.Currency == ordinaryPrice &&
                shop.Reports.Current.sales == 1 && shop.Reports.Current.revenue == ordinaryPrice &&
                !shop.Tutorial.FirstSaleAccommodationActive && !shop.Customers.IntroductoryArrivalsActive &&
                shop.Customers.EffectiveMaxVisitors == 2 && !shop.AmbientComedyDeferredForTutorial,
                "First real manual sale pays once and immediately restores ordinary customer and ambient policies");
            Check(!shop.ServeCustomer() && shop.Model.Currency == ordinaryPrice,
                "Repeated counter command cannot pay the completed first order twice");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && !shop.Customers.IntroductoryArrivalsActive &&
                shop.Tutorial.CaptureSave().firstSaleAccommodationEnded && shop.Model.Currency == ordinaryPrice &&
                shop.Reports.Current.sales == 1,
                "Combined sold-state reload keeps earnings and the permanently ended introductory policy together");
            shop.SetPanel(ShopPanel.None);
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            var collector = Spawn(CustomerArchetypes.Collector);
            var premium = Spawn(CustomerArchetypes.Premium);
            Check(shop.Customers.ActiveCount == 2 && collector.ArchetypeId != premium.ArchetypeId &&
                collector.Archetype.PatienceSeconds == CustomerArchetypes.Find(shop.Customers.Archetypes, CustomerArchetypes.Collector).PatienceSeconds &&
                premium.Archetype.PatienceSeconds == CustomerArchetypes.Find(shop.Customers.Archetypes, CustomerArchetypes.Premium).PatienceSeconds,
                "Following arrivals use two ordinary customer types and their own current catalog patience values");
            yield return WaitFor(() => collector.Phase == CustomerPhase.Queued && premium.Phase == CustomerPhase.Queued,
                32, "ordinary two-customer queue after first sale");
            Check(collector.SpecimenId != premium.SpecimenId && shop.Customers.WaitingCount == 2 &&
                shop.Model.Currency == ordinaryPrice, "Ordinary two-place queue owns separate unpaid exact specimens");
            AimAtVisit(collector, new Vector3(-1.1f, .2f, -4.3f));
            yield return TutorialCapture("442-tutorial-customer-ordinary-queue");
            Check(shop.TryStartComedy(ComedyKind.Fart), "Ambient comedy trigger becomes available after the first sale");
            shop.CancelComedy(); shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && !shop.Customers.IntroductoryArrivalsActive && shop.Model.Currency == ordinaryPrice,
                "Reload with later unpaid visitors never re-enables the already ended introductory policy");
            File.Copy(shop.SavePath, Path.Combine(evidence, "tutorial-customer-manual-sold.json"), true);

            // A separate explicit staff fixture tests the automatic-sale branch without
            // inventing a player-authored tutorial batch or refunding the ordinary wage.
            shop.ConfirmNewGame();
            Check(shop.Tutorial.Mode == TutorialMode.Offered && shop.Model.Currency == 0 && shop.StartGuidedOpening(),
                "Confirmed isolated reset offers and starts a new guided opening for the worker branch");
            StaffCreditFixture(10, "TUT-07 worker-first-sale validation only");
            yield return StaffEnter();
            Check(shop.HireCashier() && shop.SetCashierScheduled(true) && shop.Staff.IsOnDuty(1) &&
                shop.Model.Currency == 10 - ShopStaff.CashierWage,
                "Labelled cashier fixture pays the normal duty wage before trading");
            SeedStock("pondlet", "tank-a");
            Check(shop.Tutorial.CaptureSave().first.saleDay == 0 && shop.Tutorial.FirstSaleAccommodationActive,
                "Accelerated fixture stock has no invented manual tutorial sale receipt");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open the ordinary staffed fixture day");
            shop.SetPanel(ShopPanel.None); shop.Player.Teleport(new Vector3(0, .2f, 3), Quaternion.identity);
            shop.Customers.ArrivalsEnabled = true;
            yield return WaitFor(() => shop.Customers.ActiveCount == 1, 12, "worker branch production customer arrival");
            var worker = shop.Customers.Visits[0]; shop.Customers.ArrivalsEnabled = false;
            int workerCash = shop.Model.Currency;
            yield return WaitFor(() => worker.Outcome == CustomerOutcome.Sold, 35, "cashier first successful introductory sale");
            Check(!shop.IsAtCounter && worker.ArchetypeId == CustomerArchetypes.Casual &&
                shop.Model.Currency == workerCash + worker.Price && shop.Reports.Current.sales == 1 &&
                !shop.Tutorial.FirstSaleAccommodationActive && !shop.Customers.IntroductoryArrivalsActive &&
                shop.Tutorial.CaptureSave().first.saleDay == 0,
                "Actual cashier sale ends introductory policy and pays once while leaving manual tutorial proof unmet");
            Check(!shop.ServeCashier(worker) && shop.Model.Currency == workerCash + worker.Price,
                "Duplicate worker completion cannot pay the introductory fish twice");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Staff.IsOnDuty(1) &&
                shop.Model.Currency == workerCash + worker.Price && !shop.Customers.IntroductoryArrivalsActive &&
                shop.Tutorial.CaptureSave().first.saleDay == 0 && shop.Reports.Current.sales == 1,
                "Worker-sale reload retains normal wage, exact payment and ended accommodation without awarding manual proof");
            File.Copy(shop.SavePath, Path.Combine(evidence, "tutorial-customer-worker-sold.json"), true);
        }

        void TutorialCustomerFiniteDeadline(CustomerVisit live)
        {
            Check(GameModel.TryRestore(shop.Layout.WithTankIds(shop.Config), shop.Model.CaptureSave(), out var clone, out _),
                "Finite patience probe clones only the isolated gameplay fixture");
            var visit = new CustomerVisit(clone, clone.TankIds, new CustomerRules(), live.Archetype);
            visit.Advance(1, true, ShopDayPhase.Open);
            visit.Advance(live.Archetype.BrowseSeconds + 1, true, ShopDayPhase.Open);
            visit.Advance(1, true, ShopDayPhase.Open);
            Check(visit.Phase == CustomerPhase.Queued && visit.QueueSecondsRemaining == 90,
                "Cloned production visitor enters its exact finite ninety-second queue");
            visit.Advance(89, true, ShopDayPhase.Open);
            Check(visit.Phase == CustomerPhase.Queued && visit.QueueSecondsRemaining == 1,
                "Explicitly accelerated domain deadline remains queued through eighty-nine seconds");
            visit.Advance(2, true, ShopDayPhase.Open);
            Check(visit.Outcome == CustomerOutcome.Impatient && !clone.GetReservation(visit.TargetTankId).HasValue &&
                clone.Currency == shop.Model.Currency && live.Phase == CustomerPhase.Queued,
                "Finite deadline releases its unpaid cloned claim without payment or changing the live visitor");
        }
    }
}
#endif
