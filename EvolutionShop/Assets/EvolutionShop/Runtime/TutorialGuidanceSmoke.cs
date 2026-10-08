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
        IEnumerator TutorialGuidanceScenario()
        {
            limitations.Add("TUT-06 guidance scenario uses an isolated save, synthetic WASD/E/held mouse, scripted aim and guarded station commands. Native operating-system clicks and human usability are not covered.");
            limitations.Add("Full-buffer and wrong-dose recovery deliberately collect extra cultures through labelled model fixture commands. A live grower is moved and temporarily disabled for target-lifetime probes, then restored. No production room, layout, save schema, price or customer policy is changed.");
            limitations.Add("Standalone captures are actual 720p and 1080p player surfaces. Launch at 1600x900 so the first capture includes a real surface resize; editor exports are diagnostic only. TUT-07 customer pacing and independent-practice help remain separate assignments.");
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionGuidanceSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Guidance route is confined to its explicitly isolated evidence save");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionGuidanceKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionGuidanceMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;
            Check(shop.StartGuidedOpening() && shop.Model.Currency == 0, "Start ordinary zero-credit guided opening");
            Check(shop.TryGetTutorialGuidance(out var target) && target.IsDoor && !target.Station,
                "Initial orientation resolves the real opaque lab doorway");
            shop.Player.View.transform.LookAt(target.Position);
            Check(shop.TryProjectTutorialGuidance(out _, out _), "Door marker is visible from the real store spawn");
            Check(shop.World.LabDoor.OpenFraction < .1f, "Distant spawn leaves the opaque lab door closed");
            yield return TutorialCapture("420-guidance-closed-door");
            string modelBefore = JsonUtility.ToJson(shop.Model.CaptureSave()), tutorialBefore = TutorialState();
            for (int i = 0; i < 30; i++)
            {
                shop.TryGetTutorialGuidance(out _); shop.TryProjectTutorialGuidance(out _, out _);
                _ = shop.TutorialLocalCallout; _ = shop.TutorialGuidanceRecovery;
            }
            Check(JsonUtility.ToJson(shop.Model.CaptureSave()) == modelBefore && TutorialState() == tutorialBefore,
                "Repeated rendering queries preserve exact economic and learning state");
            shop.Player.View.transform.rotation = Quaternion.LookRotation(shop.Player.View.transform.position - target.Position);
            Check(!shop.TryProjectTutorialGuidance(out _, out _), "A target behind the camera produces no marker");
            shop.Player.View.transform.LookAt(target.Position);
            Check(shop.OpenTutorialHelp() && shop.Paused && !shop.TryGetTutorialGuidance(out _),
                "Requested paused Help suppresses world guidance");
            Check(shop.ShowTutorialTarget() && shop.Panel == ShopPanel.None && TutorialState() == tutorialBefore,
                "Show me returns to controlled gameplay without inventing lesson completion");
            shop.AllowValidationInput = true;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState { delta = new Vector2(24, -8) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(cycleMouse, new MouseState()); yield return null;
            yield return CycleWalkTo(new Vector2(0, -1.2f));
            yield return CycleDelay(.5f);
            Check(shop.World.LabDoor.OpenFraction > .9f && shop.TryGetTutorialGuidance(out target) && target.IsDoor,
                "Approach opens the ordinary door while guidance still points to the threshold");
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("421-guidance-open-door");

            yield return CycleHarvest(CultureKind.Flow);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Collect &&
                shop.TryGetTutorialGuidance(out target) && target.Station == StationOf(StationKind.Bloom),
                "Reversed collection order points only to the missing Bloom grower");
            yield return CycleGoStation(StationKind.Bloom);
            yield return TutorialCapture("422-guidance-grower");
            var grower = StationOf(StationKind.Bloom);
            Vector3 originalPosition = grower.transform.position, originalTarget = target.Position;
            try
            {
                grower.transform.position += Vector3.right * .5f; Physics.SyncTransforms();
                Check(shop.TryGetTutorialGuidance(out target) && target.Station == grower &&
                    Vector3.Distance(target.Position, originalTarget + Vector3.right * .5f) < .001f,
                    "Labelled moved-facility fixture follows the current live transform");
                shop.Player.View.transform.LookAt(target.Position);
                yield return TutorialCapture("423-guidance-moved-grower");
                grower.gameObject.SetActive(false);
                Check(!shop.TryGetTutorialGuidance(out _) && !string.IsNullOrEmpty(shop.TutorialGuidanceRecovery),
                    "Removed live grower clears its marker and exposes a recoverable missing-target instruction");
                yield return TutorialCapture("424-guidance-missing-grower");
            }
            finally { grower.gameObject.SetActive(true); grower.transform.position = originalPosition; Physics.SyncTransforms(); }
            Check(shop.TryGetTutorialGuidance(out target) && target.Station == grower,
                "Restoring the live facility resolves the target again without a cached stale reference");

            // Labelled branch setup: ordinary model collection fills all four slots with Flow.
            while (shop.Model.ReservedCultureCount < shop.Model.Capacity)
                Check(shop.Model.Collect(CultureKind.Flow).Success, "Full-buffer fixture collects an ordinary extra Flow");
            Check(shop.TryGetTutorialGuidance(out target) && target.Station.Kind == StationKind.Incubator,
                "Full siphon directs the player to the incubator release control instead of an unusable grower");
            yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
            Check(shop.TutorialLocalCallout != null && shop.TutorialLocalCallout.Contains("Release") &&
                shop.TutorialLocalCallout.Contains("Flow") && shop.TutorialLocalCallout.Contains("Bloom"),
                "Full-buffer local callout names the real excess release and missing culture");
            yield return TutorialCapture("425-guidance-full-buffer");
            while (shop.Model.Available(CultureKind.Flow) > 1)
                Check(shop.ReleaseCulture(CultureKind.Flow), "Guarded in-person release frees one ordinary culture slot");
            shop.BackFromPanel();
            Check(shop.TryGetTutorialGuidance(out target) && target.Station.Kind == StationKind.Bloom,
                "Successful real repair returns guidance to the missing grower");
            yield return CycleHarvest(CultureKind.Bloom);
            Check(shop.ActiveTutorialLesson == TutorialLesson.Prepare, "Both manually collected cultures advance to preparation");
            yield return CycleGoStation(StationKind.Incubator); yield return CycleTap(Key.E);
            Check(shop.Model.Collect(CultureKind.Bloom).Success && shop.Model.Collect(CultureKind.Bloom).Success,
                "Wrong-dose fixture collects two ordinary excess Bloom cultures");
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Bloom),
                "Guarded station controls stage the labelled invalid three-Bloom mixture");
            Check(shop.TutorialLocalCallout != null && shop.TutorialLocalCallout.Contains("Bloom") &&
                shop.TutorialLocalCallout.Contains("2"), "Wrong-dose callout names the exact two doses to return");
            yield return TutorialCapture("426-guidance-wrong-dose");
            Check(shop.ReturnCultureDose(CultureKind.Bloom) && shop.ReturnCultureDose(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow),
                "Real controls repair the mixture to the normal Pondlet recipe");
            Check(shop.ActiveTutorialLesson == TutorialLesson.Grow && shop.TutorialLocalCallout != null &&
                shop.TutorialLocalCallout.Contains("Begin"), "Valid preparation points to the actual incubation action");
            yield return TutorialCapture("427-guidance-begin-incubation");
            float began = Time.realtimeSinceStartup;
            Check(shop.BeginManualIncubation(), "Manual command starts the real tutorial specimen without accelerated production");
            string specimen = shop.Model.Incubator.SpecimenId;
            yield return CycleUntil(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 17, "guidance ordinary incubation", false);
            Check(Time.realtimeSinceStartup - began >= 9.5f && shop.Tutorial.FirstSpecimenId == specimen,
                "Ordinary ten-second incubation preserves the exact tutorial identity");
            Check(shop.ActiveTutorialLesson == TutorialLesson.Display && shop.TryGetTutorialGuidance(out target) && target.IsDoor,
                "Ready specimen changes the route to the shop doorway before its display");
            shop.Player.View.transform.LookAt(target.Position);
            yield return TutorialCapture("428-guidance-return-to-shop");
            yield return CycleTransfer(StationKind.TankB, "tank-b");
            Check(shop.ActiveTutorialLesson == TutorialLesson.PriceAndOpen && shop.TryGetTutorialGuidance(out target) &&
                target.Station.TankId == "tank-b" && shop.Model.GetTank("tank-b").Value.Id == specimen,
                "Price guidance follows the actual manually chosen second display and exact specimen");
            yield return TutorialCapture("429-guidance-price-target");
            yield return CycleTap(Key.E);
            Check(shop.Panel == ShopPanel.Tank && !string.IsNullOrEmpty(shop.TutorialLocalCallout) &&
                !shop.TryGetTutorialGuidance(out _), "Occupied display has one local price instruction and no competing world marker");
            yield return TutorialCapture("430-guidance-price-controls");
            shop.BackFromPanel();
            Check(shop.TryGetTutorialGuidance(out target) && target.Station.Kind == StationKind.Counter,
                "Viewing a real price switches guidance to the ordinary front counter");
            yield return CycleCounter();
            Check(!string.IsNullOrEmpty(shop.TutorialLocalCallout), "Counter presents the local Open instruction");
            yield return TutorialCapture("431-guidance-counter-open");
            Check(shop.OpenForDay() && shop.ActiveTutorialLesson == TutorialLesson.Serve && shop.Model.Currency == 0,
                "Opening uses the ordinary trading command without price changes or tutorial money");
            shop.BackFromPanel();
            Check(!shop.TryGetTutorialGuidance(out _), "ON01-06 route ends at the separately assigned service lesson");
            Check(shop.SaveNow(), "Final guidance route saves its legitimate unsold specimen and learning receipts");
        }
    }
}
#endif
