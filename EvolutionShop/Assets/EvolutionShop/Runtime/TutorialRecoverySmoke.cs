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
        IEnumerator TutorialRecoveryScenario()
        {
            limitations.Add("TUT-09 isolated recovery fixtures use 200 labelled construction credits, accelerated model production, direct tutorial observations, transient worker claims and fixture camera positions. They test blocked guidance and exact ownership; they are not a novice playthrough or ordinary economy proof. Rack construction uses existing placement and synthetic input.");
            limitations.Add("No native OS mouse/focus acceptance or fresh-process continuation is claimed. Screens are actual standalone 720p/1080p surfaces; Editor camera exports are diagnostic. Existing tutorial and three-role staffed routes run separately on this source.");
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionTutorialRecoverySmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Recovery fixtures are confined to the explicitly isolated evidence save");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTutorialRecoveryKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTutorialRecoveryMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            Check(shop.StartGuidedOpening(), "Start active guidance before the labelled recovery fixtures");
            StaffCreditFixture(200, "TUT-09 isolated recovery rack construction");
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string[] slots = shop.Layout.GetStorageSlotIds(StockerRack());
            Check(slots.Length == 2, "Recovery fixture purchased a real two-compartment rack");
            shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false;
            SpecimenProduce("pondlet"); Ok(shop.Model.StoreReadyOutput(slots[0]), "Store first unrelated recovery fixture creature");
            SpecimenProduce("pondlet"); Ok(shop.Model.StoreReadyOutput(slots[1]), "Store second unrelated recovery fixture creature");
            shop.Tutorial.ObserveOrientation(true, true, true);
            shop.Model.Tick(30);
            Ok(shop.Model.Collect(CultureKind.Bloom), "Collect labelled tracked-batch Bloom");
            Ok(shop.Model.Collect(CultureKind.Flow), "Collect labelled tracked-batch Flow");
            Ok(shop.Model.Dose(CultureKind.Bloom), "Dose tracked-batch Bloom");
            Ok(shop.Model.Dose(CultureKind.Flow), "Dose tracked-batch Flow");
            var started = shop.Model.BeginIncubation(); Ok(started, "Begin tracked recovery batch");
            Check(shop.Tutorial.ObserveStarted(shop.Model, started, TutorialActionOrigin.Player), "Record the exact labelled manually started candidate");
            shop.Model.Tick(30); shop.Tutorial.ObserveReady(shop.Model); shop.World.Refresh(shop.Model);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Display && shop.SaveNow(), "Ready candidate and its maturity receipt save together");
            string candidate = started.SpecimenId;
            Ok(shop.Model.ReserveStockTransfer("recovery-a", "nori", slots[0], "tank-a", shop.Model.GetStorage(slots[0]).Value.Id), "Reserve tank A with an unrelated exact stocker claim");
            shop.Player.Teleport(new Vector3(0, .2f, -2), Quaternion.identity);
            Check(shop.TryGetTutorialGuidance(out var target) && target.Station && target.Station.TankId == "tank-b",
                "Guidance skips Nori's reserved empty display and selects the usable tank B");
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("480-recovery-usable-display");
            Ok(shop.Model.ReserveStockTransfer("recovery-b", "nori", slots[1], "tank-b", shop.Model.GetStorage(slots[1]).Value.Id), "Reserve second destination without moving either rack specimen");
            Check(shop.TryGetTutorialGuidance(out target) && target.Station == StationOf(StationKind.Counter) &&
                shop.TutorialObjectiveForDisplay.Contains("Nori"), "All reserved destinations offer counter and stocker-pause recovery");
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("481-recovery-reserved-displays");
            Check(shop.Model.GetStockTransfer("recovery-a").HasValue && shop.Model.GetStockTransfer("recovery-b").HasValue &&
                shop.Model.Incubator.SpecimenId == candidate, "Rendering preserves both claims and exact ready candidate");
            Check(shop.Model.CancelStockTransfer("recovery-a", "nori") && shop.Model.CancelStockTransfer("recovery-b", "nori"),
                "Fixture releases only its two owned stocker claims");
            Ok(shop.Model.TransferStorageToTank(slots[0], "tank-b", shop.Model.GetStorage(slots[0]).Value.Id), "Place unrelated decoy stock and free a lab rack compartment");
            Ok(shop.Model.ReserveTechnicianOutput("recovery-moss", "moss", slots[0], candidate), "Reserve exact ready output for technician fixture");
            Check(shop.TryGetTutorialGuidance(out target) && target.Station == StationOf(StationKind.Counter) &&
                shop.TutorialObjectiveForDisplay.Contains("Moss"), "Claimed output offers pause lab work rather than an impossible transfer");
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("482-recovery-reserved-output");
            Check(shop.Model.CancelTechnicianClaim("recovery-moss", "moss"), "Release only the owned technician fixture claim");
            var displayed = shop.Model.TransferToTank("tank-a"); Ok(displayed, "Manually display tracked candidate");
            shop.Tutorial.ObserveDisplayed(shop.Model, displayed, TutorialActionOrigin.Player);
            Ok(shop.Model.ReturnTankToStorage("tank-a", slots[0], candidate), "Move manually displayed exact candidate back into its rack");
            shop.Tutorial.ObservePriceViewed(); shop.World.Refresh(shop.Model);
            Check(shop.OpenForDay() && shop.ActiveTutorialLesson == TutorialLesson.Serve, "Ordinary opening retains first-sale lesson and prior manual display receipt");
            Check(shop.TryGetTutorialGuidance(out target) && target.Station && target.Station.TankId == "tank-a" &&
                shop.TutorialObjectiveForDisplay == shop.ToolkitUi.Strings.Get("tut.obj.restock"), "A decoy in tank B cannot hide the need to restock the exact first-sale candidate / " + shop.TutorialObjectiveForDisplay);
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("483-recovery-restock-exact-candidate");
            Ok(shop.Model.ReserveStockTransfer("recovery-exact", "nori", slots[0], "tank-a", candidate), "Reserve the exact candidate for Nori without moving it");
            Check(shop.TryGetTutorialGuidance(out target) && target.Station == StationOf(StationKind.Counter) &&
                shop.TutorialObjectiveForDisplay.Contains("Nori"), "Tracked rack output reserved by Nori offers pause-stocking recovery");
            Check(shop.Model.GetStorage(slots[0]).Value.Id == candidate && shop.Tutorial.FirstSaleDay == 0,
                "Recovery text does not advance manual proof or steal the reserved resident");
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("484-recovery-reserved-candidate");
            Check(shop.Model.CancelStockTransfer("recovery-exact", "nori"), "Release only exact-candidate fixture claim");
            var transfer = shop.Model.TransferStorageToTank(slots[0], "tank-a", candidate); Ok(transfer, "Restock exact candidate through ordinary transfer authority");
            shop.Tutorial.ObserveDisplayed(shop.Model, transfer, TutorialActionOrigin.Player); shop.World.Refresh(shop.Model);
            Check(shop.TryGetTutorialGuidance(out target) && target.Station == StationOf(StationKind.Counter), "After real restocking the counter becomes actionable again");
            yield return CycleCounter();
            var counter = StationOf(StationKind.Counter);
            Check(shop.OpenTutorialHelp(), "Open help from real counter panel before its disable fixture");
            try
            {
                counter.enabled = false;
                shop.CloseTutorialHelp();
                Check(shop.Panel == ShopPanel.None && !shop.Paused, "Help never returns to a disabled station's stale interaction panel");
            }
            finally { counter.enabled = true; }
            Check(shop.CloseForNight(), "Close the ordinary recovery-fixture day");
            yield return CycleUntil(() => shop.Cycle.Phase == ShopDayPhase.Night, 8, "recovery fixture ordinary closing", false);
            Check(shop.Reports.Last.sales == 0 && shop.Tutorial.FirstSaleDay == 0 && shop.Model.GetTank("tank-a").Value.Id == candidate && shop.SaveNow(),
                "Recovery scenario ends with intact stock and no invented sale, reward or completion");
            File.Copy(shop.SavePath, Path.Combine(evidence, "tutorial-recovery-fixture-final.json"), false);
        }
    }
}
#endif
