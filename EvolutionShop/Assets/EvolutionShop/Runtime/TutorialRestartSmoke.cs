#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable]
        sealed class TutorialRestartReceipt
        {
            public string stage, nextStage, inputPath, outputPath, inputSha256, outputSha256;
            public int processId, inputFormatVersion, day, currency, pending, interrupted, sales;
            public string mode, lesson, phase, incubatorPhase, incubatorSpecimen, firstSpecimen, practiceSpecimen;
            public float incubationSeconds, walkingMetres, harvestingSeconds;
            public int doorwayCrossings, inputDisplayTransfers;
            public bool practiceAssisted;
            public string inputEvidence = "Synthetic Input System movement, look, held collection and E; scripted aim; guarded menu commands. Not native OS input.";
        }

        // Each invocation consumes only the preceding process's immutable output.
        // Awake has already loaded this isolated slot and presented the real Welcome.
        // There is deliberately no ReloadSaved call and no seeded learning/economy.
        IEnumerator TutorialRestartScenario()
        {
            string stage = ShopSession.Arg("-evolutionTutorialRestartSmoke");
            string[] stages = { "new", "orient", "collect", "prepare", "grow", "working", "display", "price-open", "serve", "pending", "practice", "close-review", "report", "next-plan", "completed", "skip", "skipped", "legacy", "migrated" };
            Check(Array.IndexOf(stages, stage) >= 0 && shop.ValidationMode &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Restart stage names an explicit route and uses only its isolated evidence slot / " + stage);
            limitations.Add("TUT-10 fresh-process continuation: Awake restores the isolated full slot before this harness runs. Each invocation advances a bounded part of the real tutorial, saves and exits; the external runner records distinct process identities and copies the exact prior output. No same-process reload substitutes for application startup.");
            limitations.Add("All walking/look, hold collection and E events are synthetic Input System events with scripted station aim; station menu choices, Continue, skip, help and completion use the same guarded application methods as UI. No native operating-system clicks, usability, audio listening or novice acceptance is claimed.");
            limitations.Add("Arrivals are enabled only while waiting for each real checkout; incubation and customer travel run on normal frames. Ambient comedy is suppressed only in this isolated scenario. The legacy branches use labelled format-19 compatibility input: a derived completed slot or an archived pre-tutorial paid-technician snapshot. Their provenance is recorded by the runner. No normal save, injected currency, taught fact, specimen, sale or time acceleration is used in the main chain.");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            Check(shop.Panel == ShopPanel.Welcome && shop.Paused && Cursor.visible && Cursor.lockState == CursorLockMode.None,
                "Actual startup presents paused Welcome with an available cursor before any continuation action");
            Check(!shop.AutosaveBlocked && string.IsNullOrEmpty(shop.SaveError), "Restart input passes normal save validation without recovery bypass");
            byte[] original = null; SlotData input = null; int inputVersion = 0;
            if (stage == "new")
            {
                Check(!File.Exists(shop.SavePath) && shop.Tutorial.Mode == TutorialMode.Offered && shop.Tutorial.Facts == TutorialFacts.None &&
                    shop.Model.Currency == 0 && shop.Model.DiscoveredSpeciesIds.Count == 0 && shop.DisplayedStockCount() == 0 &&
                    shop.Cycle.DayNumber == 1 && shop.Cycle.Phase == ShopDayPhase.Night,
                    "New-process entry has an absent isolated slot and ordinary zero-credit Offered night one");
            }
            else
            {
                Check(File.Exists(shop.SavePath), "Restart has a physical saved input from a previous process");
                original = File.ReadAllBytes(shop.SavePath);
                File.WriteAllBytes(Path.Combine(evidence, "restart-input.json"), original);
                inputVersion = JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)).formatVersion;
                Check(SaveStore.Read(shop.SavePath, shop.Config, out var expectedModel, out input, out var error),
                    "Independent read validates the exact startup bytes / " + error);
                Check(JsonUtility.ToJson(shop.Model.CaptureSave()) == JsonUtility.ToJson(expectedModel.CaptureSave()),
                    "Awake restores exact cultures, discoveries, specimens, prices, currency and machine countdowns");
                Check(JsonUtility.ToJson(shop.Cycle.CaptureSave()) == JsonUtility.ToJson(input.cycle) &&
                    JsonUtility.ToJson(shop.Layout.CaptureSave()) == JsonUtility.ToJson(input.layout) &&
                    JsonUtility.ToJson(shop.Reputation.CaptureSave()) == JsonUtility.ToJson(input.reputation),
                    "Awake restores exact day clock, layout and reputation without offline progress");
                Check(JsonUtility.ToJson(shop.Staff.CaptureSave()) == JsonUtility.ToJson(input.staff) &&
                    JsonUtility.ToJson(shop.Stocking.CaptureSave(shop.Model)) == JsonUtility.ToJson(input.stocker) &&
                    JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model)) == JsonUtility.ToJson(input.technician) &&
                    TutorialState() == JsonUtility.ToJson(input.tutorial),
                    "Awake restores exact worker receipts/orders and tutorial facts together");
                Check(shop.Sensitivity == input.sensitivity && shop.Volume == input.volume && shop.SlapKey == input.slapKey &&
                    shop.HumorVolume == input.humorVolume && shop.ReducedGags == input.reducedGags,
                    "Awake preserves all saved input/audio/comedy settings");
                Check(ShopTradeLedger.TryRestore(input.trades, input.cycle, out var expectedReports),
                    "Restart report baseline is valid before intentional unpaid-visit cancellation");
                expectedReports.CancelPendingForReload();
                Check(JsonUtility.ToJson(shop.Reports.CaptureSave()) == JsonUtility.ToJson(expectedReports.CaptureSave()),
                    "Startup report delta is exactly pending-to-interrupted; no other totals or sale changes");
            }
            Check(shop.Customers.ActiveCount == 0 && shop.Customers.NextToServe == null && !shop.Reports.HasPending &&
                !shop.Model.TechnicianClaim.HasValue && shop.Stocker.CurrentJobId == null && shop.Technician.CurrentJobId == null,
                "Welcome creates no transient visitor, worker job, unpaid checkout or machine claim");
            foreach (string tank in shop.Model.TankIds)
                Check(!shop.Model.GetReservation(tank).HasValue && !shop.Model.IsTankReservedForStocking(tank),
                    "Restart releases transient customer and stocking reservations without moving display stock / " + tank);
            string pausedGame = JsonUtility.ToJson(shop.Model.CaptureSave()), pausedCycle = JsonUtility.ToJson(shop.Cycle.CaptureSave()), pausedTutorial = TutorialState();
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.Paused && JsonUtility.ToJson(shop.Model.CaptureSave()) == pausedGame &&
                JsonUtility.ToJson(shop.Cycle.CaptureSave()) == pausedCycle && TutorialState() == pausedTutorial &&
                (original == null ? !File.Exists(shop.SavePath) : RestartHash(File.ReadAllBytes(shop.SavePath)) == RestartHash(original)),
                "Paused startup frames preserve exact game, clock, tutorial and original input bytes");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors == 2 && !shop.Customers.UseBaseReputationEffectsForValidation &&
                shop.Customers.ForcedArchetypeId == null && Mathf.Approximately(shop.Config.IncubationSeconds, 10) && shop.ScriptedHarvest == null,
                "Restart retains production customer rules, ordinary ten-second incubation and normal collection");
            RestartEntry(stage, input, inputVersion);
            if (stage == "working" || stage == "pending" || stage == "completed" || stage == "skipped" || stage == "legacy")
                yield return TutorialCapture("500-restart-" + stage + "-welcome");

            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTutorialRestartKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTutorialRestartMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            if (stage == "new") Check(shop.StartGuidedOpening(), "Start guided opening commits the fresh Offered choice through its production command");
            else Check(shop.ContinueGuidedOpening(), "Actual Welcome Continue command resumes the restored game without restarting guidance");
            Check(shop.Panel == ShopPanel.None && !shop.Paused, "Continue releases the paused Welcome into normal gameplay");
            string next;
            switch (stage)
            {
                case "new": next = "orient"; break;
                case "orient":
                    shop.AllowValidationInput = true;
                    InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(24, -8) });
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(cycleMouse, new MouseState()); yield return null;
                    yield return CycleGoStation(StationKind.Bloom);
                    Check(shop.ActiveTutorialLesson == TutorialLesson.Collect &&
                        (shop.Tutorial.Facts & (TutorialFacts.Moved | TutorialFacts.Looked | TutorialFacts.LabVisited)) ==
                        (TutorialFacts.Moved | TutorialFacts.Looked | TutorialFacts.LabVisited),
                        "Restored orientation advances through synthetic look and physical WASD travel through the door");
                    next = "collect"; break;
                case "collect":
                    yield return CycleHarvest(CultureKind.Bloom); yield return CycleHarvest(CultureKind.Flow);
                    Check(shop.ActiveTutorialLesson == TutorialLesson.Prepare && shop.Model.Available(CultureKind.Bloom) == 1 &&
                        shop.Model.Available(CultureKind.Flow) == 1, "Restored collection advances after two actual held-input harvests");
                    next = "prepare"; break;
                case "prepare":
                    yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
                    Check(shop.Panel == ShopPanel.Incubator && shop.DoseCulture(CultureKind.Flow) && shop.DoseCulture(CultureKind.Bloom) &&
                        shop.ActiveTutorialLesson == TutorialLesson.Grow, "Restored preparation stages the real Pondlet recipe through guarded station controls");
                    next = "grow"; break;
                case "grow":
                    yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
                    Check(shop.BeginManualIncubation() && shop.Model.Incubator.Phase == IncubatorPhase.Incubating &&
                        shop.Model.Incubator.SpecimenId == shop.Tutorial.FirstSpecimenId,
                        "Restored staged recipe starts one exact manual specimen before process exit");
                    next = "working"; break;
                case "working":
                    string first = shop.Model.Incubator.SpecimenId; float remaining = shop.Model.Incubator.SecondsRemaining;
                    float began = Time.realtimeSinceStartup;
                    yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 17, "restarted natural incubation", false);
                    Check(shop.Model.Incubator.SpecimenId == first && shop.Tutorial.FirstSpecimenId == first &&
                        shop.ActiveTutorialLesson == TutorialLesson.Display && Time.realtimeSinceStartup - began >= remaining - .15f,
                        "Fresh-process active incubation finishes its remaining active time once with the same specimen and lesson receipt");
                    next = "display"; break;
                case "display":
                    yield return CycleTransfer(StationKind.TankA, "tank-a");
                    Check(shop.ActiveTutorialLesson == TutorialLesson.PriceAndOpen && shop.Tutorial.CaptureSave().first.manuallyDisplayed,
                        "Restored ready output stocks through physical movement/E and records the exact manual display");
                    next = "price-open"; break;
                case "price-open":
                    yield return CycleGoStation(StationKind.TankA); yield return CycleTap(Key.E);
                    Check(shop.Panel == ShopPanel.Tank && (shop.Tutorial.Facts & TutorialFacts.PriceViewed) != 0,
                        "Restored stocked display opens its actual price panel without requiring a price change");
                    yield return CycleCounter();
                    Check(shop.OpenForDay() && shop.ActiveTutorialLesson == TutorialLesson.Serve && shop.Model.Currency == 0,
                        "Restored price/open lesson begins normal trading without a payment or clock reset");
                    next = "serve"; break;
                case "serve":
                    yield return CycleCounter(); shop.Customers.ArrivalsEnabled = true;
                    yield return CycleUntil(() => shop.Customers.NextToServe != null && shop.Customers.NextToServe.Phase == CustomerPhase.Queued,
                        60, "pre-exit unpaid tutorial visitor", true);
                    shop.Customers.ArrivalsEnabled = false;
                    Check(shop.Customers.NextToServe.SpecimenId == shop.Tutorial.FirstSpecimenId && shop.Reports.PendingCount == 1 &&
                        shop.Model.GetReservation("tank-a").HasValue && shop.Model.Currency == 0 && shop.Reports.Current.sales == 0,
                        "Real first visitor has queued and reserved the tracked specimen without payment before exit");
                    next = "pending"; break;
                case "pending":
                    yield return CycleCounter(); yield return TutorialCheckout(shop.Tutorial.FirstSpecimenId, 6);
                    Check(shop.ActiveTutorialLesson == TutorialLesson.Practice && shop.Reports.Current.interrupted == 1 &&
                        shop.Reports.Current.sales == 1 && !shop.Tutorial.FirstSaleAccommodationActive,
                        "Canceled unpaid startup visit is replaced by a real buyer; exact checkout pays once and ends introductory arrivals");
                    next = "practice"; break;
                case "practice":
                    yield return TutorialProduce(true);
                    string practice = shop.Model.Incubator.SpecimenId;
                    Check(practice != shop.Tutorial.FirstSpecimenId && practice == shop.Tutorial.PracticeSpecimenId,
                        "After restart a new post-first-sale batch becomes the practice candidate");
                    yield return CycleTransfer(StationKind.TankA, "tank-a"); yield return CycleCounter(); yield return TutorialCheckout(practice, 12);
                    Check(shop.ActiveTutorialLesson == TutorialLesson.CloseAndReview && !shop.Tutorial.PracticeAssisted &&
                        shop.Reports.Current.sales == 2 && shop.Reports.Current.interrupted == 1,
                        "Whole second manual input loop completes in this process with two ordinary payments and no extra interrupted visit");
                    next = "close-review"; break;
                case "close-review":
                    yield return CycleCounter(); Check(shop.CloseForNight(), "Restored final trading lesson uses the normal guarded close command");
                    yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8, "restarted day closing drain", false);
                    Check(shop.Cycle.DayNumber == 2 && shop.Reports.Last.sales == 2 && shop.Reports.Last.revenue == 12 &&
                        !shop.Tutorial.ReportAcknowledged && shop.Model.Currency == 12,
                        "Normal closing produces the real two-sale report and retains its pending tutorial acknowledgement");
                    next = "report"; break;
                case "report":
                    Check(shop.ShowDayReport() && !shop.Tutorial.ReportAcknowledged, "Restart reopens the qualifying saved report without a false acknowledgement");
                    yield return CycleTap(Key.Escape);
                    Check(shop.Tutorial.ReportAcknowledged && shop.ActiveTutorialLesson == TutorialLesson.NextPlan && shop.Model.Currency == 12,
                        "Synthetic Escape acknowledges the real report and advances to the final night plan");
                    next = "next-plan"; break;
                case "next-plan":
                    Check(shop.OpenTutorialHelp() && shop.TutorialHelpLesson == TutorialLesson.NextPlan,
                        "Restart exposes optional next-plan help using the restored live earned cash");
                    Check(shop.FinishGuidedOpening() && shop.Tutorial.Mode == TutorialMode.Completed && shop.Model.Currency == 12 && !shop.Model.UpgradeOwned,
                        "Guarded completion persists the finished tutorial without reward or compulsory purchase");
                    next = "completed"; break;
                case "completed":
                    RestartReadOnlyHelp();
                    Check(!shop.StartGuidedOpening() && !shop.FinishGuidedOpening() && !shop.SkipTutorial() && shop.Model.Currency == 12,
                        "Completed restart cannot replay mode commands, duplicate receipts or pay again");
                    next = "verified"; break;
                case "skip":
                    string beforeSkip = JsonUtility.ToJson(shop.Model.CaptureSave());
                    Check(shop.SkipTutorial() && shop.Tutorial.Mode == TutorialMode.Skipped && shop.ActiveTutorialLesson == TutorialLesson.None &&
                        !shop.Tutorial.FirstSaleAccommodationActive && beforeSkip == JsonUtility.ToJson(shop.Model.CaptureSave()),
                        "Actual skip command preserves the live active machine and economy while removing tutorial policy");
                    Check(!shop.SkipTutorial(), "Repeated skip cannot commit a second mode transition");
                    next = "skipped"; break;
                case "skipped":
                    RestartReadOnlyHelp();
                    string skippedSpecimen = shop.Model.Incubator.SpecimenId;
                    yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 17, "unguided restarted incubation", false);
                    Check(shop.Model.Incubator.SpecimenId == skippedSpecimen && shop.Tutorial.Mode == TutorialMode.Skipped &&
                        shop.ActiveTutorialLesson == TutorialLesson.None && !shop.Tutorial.FirstSaleAccommodationActive && shop.Model.Currency == 0,
                        "Skipped mode survives a real restart and remaining production completes normally without forced lesson or reward");
                    next = "verified"; break;
                case "legacy":
                    RestartReadOnlyHelp();
                    Check(shop.SaveNow() && File.Exists(shop.SavePath + ".v19.bak") &&
                        RestartHash(File.ReadAllBytes(shop.SavePath + ".v19.bak")) == RestartHash(original),
                        "First ordinary legacy write preserves original format-19 bytes in the migration archive");
                    next = "migrated"; break;
                default: // migrated
                    RestartReadOnlyHelp();
                    Check(!shop.StartGuidedOpening() && !shop.FinishGuidedOpening() && !shop.SkipTutorial(),
                        "Already-migrated restart cannot invent interactive lessons or completion");
                    next = "verified"; break;
            }
            RestartCheckpoint(stage, next, original, inputVersion);
        }

        void RestartEntry(string stage, SlotData input, int inputVersion)
        {
            TutorialLesson expected = TutorialLesson.None;
            switch (stage)
            {
                case "orient": expected = TutorialLesson.Orient; break;
                case "collect": expected = TutorialLesson.Collect; break;
                case "prepare": expected = TutorialLesson.Prepare; break;
                case "grow": case "working": case "skip": expected = TutorialLesson.Grow; break;
                case "display": expected = TutorialLesson.Display; break;
                case "price-open": expected = TutorialLesson.PriceAndOpen; break;
                case "serve": case "pending": expected = TutorialLesson.Serve; break;
                case "practice": expected = TutorialLesson.Practice; break;
                case "close-review": case "report": expected = TutorialLesson.CloseAndReview; break;
                case "next-plan": expected = TutorialLesson.NextPlan; break;
            }
            TutorialMode mode = stage == "new" ? TutorialMode.Offered : stage == "completed" ? TutorialMode.Completed :
                stage == "skipped" ? TutorialMode.Skipped : stage == "legacy" || stage == "migrated" ? TutorialMode.LegacyUnguided : TutorialMode.Active;
            Check(shop.Tutorial.Mode == mode && shop.ActiveTutorialLesson == expected,
                "Fresh-process mode and exact actionable lesson match this checkpoint / " + stage + " / " + mode + " / " + expected);
            if (stage == "working" || stage == "skip" || stage == "skipped")
                Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Model.Incubator.SecondsRemaining > 0 && shop.Model.Currency == 0,
                    "Active-machine checkpoint retains actual unsold growing output and positive remaining time");
            if (stage == "pending")
                Check(input.trades.pendingCustomerIds.Length == 1 && input.trades.current.sales == 0 &&
                    shop.Reports.Current.interrupted == input.trades.current.interrupted + 1 && shop.Reports.Current.sales == 0 &&
                    shop.Model.Currency == 0 && shop.Model.GetTank("tank-a").Value.Id == shop.Tutorial.FirstSpecimenId &&
                    shop.Tutorial.FirstSaleAccommodationActive,
                    "Unpaid visit restart cancels exactly one reservation, keeps stock and eligibility, and pays nothing");
            if (stage == "practice")
                Check(shop.Reports.Current.interrupted == 1 && shop.Reports.Current.sales == 1 && shop.Model.Currency == 6 &&
                    !shop.Tutorial.FirstSaleAccommodationActive && !shop.Tutorial.PracticeAssisted,
                    "Next distinct process retains one interruption and one first-sale payment without double cancellation");
            if (stage == "legacy") Check(inputVersion == 19 && shop.Tutorial.Facts == TutorialFacts.None,
                "Explicitly labelled format-19 established-shop fixture migrates without guessing tutorial mastery");
            else if (stage != "new") Check(inputVersion == SaveStore.CurrentFormatVersion, "Checkpoint is the current-format ordinary saved slot");
        }

        void RestartReadOnlyHelp()
        {
            string tutorial = TutorialState(), game = JsonUtility.ToJson(shop.Model.CaptureSave());
            Check(shop.OpenTutorialHelp(), "Restored unguided/completed game can request optional paused help");
            shop.SelectTutorialHelpLesson(TutorialLesson.Collect); shop.SelectTutorialHelpLesson(TutorialLesson.NextPlan);
            Check(shop.Paused && TutorialState() == tutorial && JsonUtility.ToJson(shop.Model.CaptureSave()) == game,
                "Replayed help cards are read-only for all game and tutorial state");
            shop.CloseTutorialHelp();
            Check(shop.Panel == ShopPanel.None && !shop.Paused, "Optional help returns to resumed gameplay without forcing guidance");
        }

        void RestartCheckpoint(string stage, string next, byte[] original, int inputVersion)
        {
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow(), "Write the combined tutorial/game checkpoint before this process exits / next=" + next);
            string output = Path.Combine(evidence, "restart-output.json");
            byte[] bytes = File.ReadAllBytes(shop.SavePath); File.WriteAllBytes(output, bytes);
            Check(SaveStore.Read(output, shop.Config, out var saved, out var slot, out var error) &&
                JsonUtility.ToJson(saved.CaptureSave()) == JsonUtility.ToJson(shop.Model.CaptureSave()) &&
                JsonUtility.ToJson(slot.tutorial) == TutorialState(),
                "Checkpoint output validates exact authoritative model and tutorial together / " + error);
            var trade = shop.Reports.Current ?? shop.Reports.Last;
            var receipt = new TutorialRestartReceipt
            {
                stage = stage, nextStage = next, processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                inputPath = original == null ? null : Path.Combine(evidence, "restart-input.json"), outputPath = output,
                inputSha256 = original == null ? null : RestartHash(original), outputSha256 = RestartHash(bytes), inputFormatVersion = inputVersion,
                mode = shop.Tutorial.Mode.ToString(), lesson = shop.ActiveTutorialLesson.ToString(), day = shop.Cycle.DayNumber,
                phase = shop.Cycle.Phase.ToString(), currency = shop.Model.Currency, pending = shop.Reports.PendingCount,
                interrupted = trade == null ? 0 : trade.interrupted, sales = trade == null ? 0 : trade.sales,
                incubatorPhase = shop.Model.Incubator.Phase.ToString(), incubatorSpecimen = shop.Model.Incubator.SpecimenId,
                incubationSeconds = shop.Model.Incubator.SecondsRemaining, firstSpecimen = shop.Tutorial.FirstSpecimenId,
                practiceSpecimen = shop.Tutorial.PracticeSpecimenId, practiceAssisted = shop.Tutorial.PracticeAssisted,
                walkingMetres = cycleMetrics.walkingMetres, doorwayCrossings = cycleMetrics.doorwayCrossings,
                harvestingSeconds = cycleMetrics.harvestingSeconds, inputDisplayTransfers = cycleMetrics.inputDisplayTransfers
            };
            File.WriteAllText(Path.Combine(evidence, "restart-state.json"), JsonUtility.ToJson(receipt, true));
        }

        static string RestartHash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
#endif
