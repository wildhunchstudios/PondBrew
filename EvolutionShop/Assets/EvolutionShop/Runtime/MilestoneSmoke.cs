#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    /// <summary>Development-only timed smoke using the same model/session actions as the UI. It never touches the user's slot.</summary>
    public sealed partial class MilestoneSmoke : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string context;
            public string scenario;
            public string unityVersion;
            public string graphics;
            public string processor;
            public string savePath;
            public string[] checks;
            public string[] runtimeErrors;
            public string[] limitations;
            public float elapsedSeconds;
        }
        bool tutorialRun;
        ShopSession shop;
        string evidence;
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<string> limitations = new List<string>();
        float start;
        InputSettings previousInputSettings, smokeInputSettings;
        Keyboard syntheticKeyboard;
        IEnumerator Start()
        {
            shop = GetComponent<ShopSession>();
            evidence = ShopSession.Arg("-evolutionEvidence") ?? Application.temporaryCachePath;
            Directory.CreateDirectory(evidence);
            start = Time.realtimeSinceStartup;
            Application.logMessageReceived += Log;
            var stack = new Stack<IEnumerator>();
            bool continuous = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionCycleSmoke") >= 0;
            bool nativeUi = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionUiSmoke") >= 0;
            bool pricing = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionPricingSmoke") >= 0;
            bool customerTypes = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionCustomerTypesSmoke") >= 0;
            bool reputation = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionReputationSmoke") >= 0;
            bool slap = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionSlapSmoke") >= 0;
            bool trading = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionTradingSmoke") >= 0;
            bool comedyGate = ShopSession.Arg("-evolutionComedyGateSmoke") != null;
            bool comedy = ShopSession.Arg("-evolutionComedySmoke") != null;
            bool rooms = ShopSession.Arg("-evolutionRoomsSmoke") != null;
            bool fxComfort = ShopSession.Arg("-evolutionFxComfortSmoke") != null;
            bool toolkitUi = ShopSession.Arg("-evolutionToolkitUiSmoke") != null;
            bool fxAudioMix = ShopSession.Arg("-evolutionFxAudioMixSmoke") != null;
            bool fxNativeUi = ShopSession.Arg("-evolutionFxNativeUiSmoke") != null;
            bool fxRoomAtmosphere = ShopSession.Arg("-evolutionFxRoomAtmosphereSmoke") != null;
            bool fxComedy = ShopSession.Arg("-evolutionFxComedySmoke") != null;
            bool fxWorker = ShopSession.Arg("-evolutionFxWorkerSmoke") != null;
            bool fxCustomer = ShopSession.Arg("-evolutionFxCustomerSmoke") != null;
            bool fxContainer = ShopSession.Arg("-evolutionFxContainerSmoke") != null;
            bool fxSpecies = ShopSession.Arg("-evolutionFxSpeciesSmoke") != null;
            bool fxPlayerInteraction = ShopSession.Arg("-evolutionFxPlayerInteractionSmoke") != null;
            bool fxResearchAutomation = ShopSession.Arg("-evolutionFxResearchAutomationSmoke") != null;
            bool fxIncubation = ShopSession.Arg("-evolutionFxIncubationSmoke") != null;
            bool fxAdvancedLab = ShopSession.Arg("-evolutionFxAdvancedLabSmoke") != null;
            bool fxGrower = ShopSession.Arg("-evolutionFxGrowerSmoke") != null;
            bool fxAquarium = ShopSession.Arg("-evolutionFxAquariumSmoke") != null;
            bool fxCollection = ShopSession.Arg("-evolutionFxCollectionSmoke") != null;
            bool fxGrowth = ShopSession.Arg("-evolutionFxGrowthSmoke") != null;
            bool fxDose = ShopSession.Arg("-evolutionFxDoseSmoke") != null;
            bool fxHatch = ShopSession.Arg("-evolutionFxHatchSmoke") != null;
            bool fxOrganic = ShopSession.Arg("-evolutionFxOrganicSmoke") != null;
            bool fxVocabulary = ShopSession.Arg("-evolutionFxVocabularySmoke") != null;
            bool fxReview = ShopSession.Arg("-evolutionFxReviewSmoke") != null;
            bool fxPondlet = ShopSession.Arg("-evolutionFxPondletSmoke") != null;
            bool fxHands = ShopSession.Arg("-evolutionFxHandsSmoke") != null;
            bool fxLab = ShopSession.Arg("-evolutionFxLabSmoke") != null;
            bool fxCue = ShopSession.Arg("-evolutionFxCueSmoke") != null;
            bool fxBaseline = ShopSession.Arg("-evolutionFxBaselineSmoke") != null;
            bool automation = ShopSession.Arg("-evolutionAutomationSmoke") != null;
            bool workerComedy = ShopSession.Arg("-evolutionWorkerComedySmoke") != null;
            bool staff = ShopSession.Arg("-evolutionStaffSmoke") != null;
            bool technician = ShopSession.Arg("-evolutionTechnicianSmoke") != null;
            bool tutorial = ShopSession.Arg("-evolutionTutorialSmoke") != null;
            bool guidance = ShopSession.Arg("-evolutionGuidanceSmoke") != null;
            bool tutorialCustomer = ShopSession.Arg("-evolutionTutorialCustomerSmoke") != null;
            bool tutorialHandoff = ShopSession.Arg("-evolutionTutorialHandoffSmoke") != null;
            bool tutorialRecovery = ShopSession.Arg("-evolutionTutorialRecoverySmoke") != null;
            bool tutorialConditioningRecovery = ShopSession.Arg("-evolutionTutorialConditioningRecoverySmoke") != null;
            bool tutorialRestart = ShopSession.Arg("-evolutionTutorialRestartSmoke") != null;
            tutorialRun = tutorialRestart || tutorial || guidance || tutorialCustomer || tutorialHandoff || tutorialRecovery || tutorialConditioningRecovery;
            bool stocker = ShopSession.Arg("-evolutionStockerSmoke") != null;
            bool presentation = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionPresentationSmoke") >= 0;
            bool labGate = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionLabGateSmoke") >= 0;
            bool airfin = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionAirfinSmoke") >= 0;
            bool conditioning = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionConditioningSmoke") >= 0;
            bool research = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionResearchSmoke") >= 0;
            bool specimens = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionSpecimenStorageSmoke") >= 0;
            bool lab = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionLabSmoke") >= 0;
            bool editedLayout = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionEditedLayoutSmoke") >= 0;
            bool storage = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionStorageSmoke") >= 0;
            bool decor = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionDecorSmoke") >= 0;
            bool expansion = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionExpansionSmoke") >= 0;
            bool placement = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionPlacementSmoke") >= 0;
            if (!fxComfort && !fxAudioMix && !fxNativeUi && !fxRoomAtmosphere && !fxComedy && !fxWorker && !fxCustomer && !fxSpecies && !fxPlayerInteraction && !fxResearchAutomation && !fxIncubation && !fxAdvancedLab && !fxGrower && !fxAquarium && !fxCollection && !fxGrowth && !fxDose && !fxHatch && !fxOrganic && !fxVocabulary && !fxPondlet && !fxHands && !fxLab && !fxCue && !fxBaseline && !reputation && !slap && !trading && !placement && !expansion && !decor && !storage && !editedLayout && !lab && !specimens && !research && !conditioning && !airfin && !labGate && !presentation && !comedy && !comedyGate && !rooms && !automation && !workerComedy && !staff && !stocker && !technician && !tutorial && !guidance && !tutorialCustomer && !tutorialHandoff && !tutorialRecovery && !tutorialConditioningRecovery && !tutorialRestart)
            {
                shop.Customers.UseBaseReputationEffectsForValidation = true;
                limitations.Add("Earlier scenarios retain base customer demand/budgets/tolerances through an explicit validation-only hook; reputation still scores, and real tier effects are covered by the reputation scenario.");
            }
            if (!fxComfort && !fxAudioMix && !fxNativeUi && !fxRoomAtmosphere && !fxComedy && !fxWorker && !fxCustomer && !fxSpecies && !fxPlayerInteraction && !fxResearchAutomation && !fxIncubation && !fxAdvancedLab && !fxGrower && !fxAquarium && !fxCollection && !fxGrowth && !fxDose && !fxHatch && !fxOrganic && !fxVocabulary && !fxPondlet && !fxHands && !fxLab && !fxCue && !fxBaseline && !customerTypes && !reputation && !slap && !trading && !placement && !expansion && !decor && !storage && !editedLayout && !lab && !specimens && !research && !conditioning && !airfin && !labGate && !presentation && !comedy && !comedyGate && !rooms && !automation && !workerComedy && !staff && !stocker && !technician && !tutorial && !guidance && !tutorialCustomer && !tutorialHandoff && !tutorialRecovery && !tutorialConditioningRecovery && !tutorialRestart)
            {
                // Earlier scenarios were written for one visitor of the R02/R03.1 customer type at a time.
                shop.Customers.MaxVisitors = 1;
                shop.Customers.ForcedArchetypeId = CustomerArchetypes.Casual;
                limitations.Add("Pinned to one casual visitor at a time (R03.2 validation hook) to keep this earlier scenario's assumptions; customer types and two concurrent visitors are covered by the customer-types scenario.");
            }
            stack.Push(fxSpecies ? FxSpeciesScenario() : fxPlayerInteraction ? FxPlayerInteractionScenario() : fxResearchAutomation ? FxResearchAutomationScenario() : fxIncubation ? FxIncubationScenario() : fxAdvancedLab ? FxAdvancedLabScenario() : fxGrower ? FxGrowerScenario() : fxAquarium ? FxAquariumScenario() : fxCollection ? FxCollectionScenario() : fxGrowth ? FxGrowthScenario() : fxDose ? FxDoseScenario() : fxHatch ? FxHatchScenario() : fxOrganic ? FxOrganicScenario() : fxVocabulary ? FxVocabularyScenario() : fxReview ? FxReviewScenario() : fxPondlet ? FxPondletScenario() : fxHands ? FxHandsScenario() : fxLab ? FxLabScenario() : fxCue ? FxCueScenario() : fxBaseline ? FxBaselineScenario() : automation ? AutomationScenario() : workerComedy ? WorkerComedyScenario() : tutorialRestart ? TutorialRestartScenario() : tutorialConditioningRecovery ? TutorialConditioningRecoveryScenario() : tutorialRecovery ? TutorialRecoveryScenario() : tutorialHandoff ? TutorialHandoffScenario() : tutorialCustomer ? TutorialCustomerScenario() : guidance ? TutorialGuidanceScenario() : tutorial ? TutorialScenario() : technician ? TechnicianScenario() : stocker ? StockerScenario() : staff ? StaffScenario() : rooms ? RoomsScenario() : comedyGate ? ComedyGateScenario() : comedy ? ComedyScenario() : presentation ? PresentationScenario() : labGate ? LabGateScenario() : airfin ? AirfinScenario() : conditioning ? ConditioningScenario() : research ? ResearchScenario() : specimens ? SpecimenStorageScenario() : lab ? LabScenario() : editedLayout ? EditedLayoutScenario() : storage ? FurnitureStorageScenario() : decor ? DecorScenario() : expansion ? ExpansionScenario() : placement ? PlacementScenario() : trading ? TradingShiftScenario() : nativeUi ? UiClickScenario() : continuous ? ContinuousScenario() : pricing ? PricingScenario() :
                customerTypes ? CustomerTypesScenario() : reputation ? ReputationScenario() : slap ? SlapScenario() : Scenario());
            if (fxContainer) { stack.Clear(); stack.Push(FxContainerScenario()); }
            if (fxCustomer) { stack.Clear(); stack.Push(FxCustomerScenario()); }
            if (fxWorker) { stack.Clear(); stack.Push(FxWorkerScenario()); }
            if (fxComedy) { stack.Clear(); stack.Push(FxComedyScenario()); }
            if (fxRoomAtmosphere) { stack.Clear(); stack.Push(FxRoomAtmosphereScenario()); }
            if (fxNativeUi) { stack.Clear(); stack.Push(FxNativeUiScenario()); }
            if (fxAudioMix) { stack.Clear(); stack.Push(FxAudioMixScenario()); }
            if (fxComfort) { stack.Clear(); stack.Push(FxComfortScenario()); }
            if (toolkitUi) { stack.Clear(); stack.Push(ToolkitUiScenario()); }
            bool isolateSelectedInputs = ShopSession.Arg("-evolutionIsolateSyntheticInput") != null && (continuous || fxGrowth || fxCollection || fxPlayerInteraction || fxSpecies || fxContainer || fxCustomer || fxWorker || fxComedy || fxRoomAtmosphere || fxNativeUi || fxAudioMix || fxComfort);
            bool failed = false;
            while (stack.Count > 0)
            {
                object yielded = null;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    if (isolateSelectedInputs && !growerIsolatingInputs && syntheticKeyboard != null && cycleMouse != null) {
                        GrowerIsolateInputs("synthetic-input-provenance.json");
                        limitations.Add("Explicit diagnostic-only synthetic input isolation: other Mouse/Keyboard devices are disabled only in this player InputSystem frontend and restored during cleanup; see synthetic-input-provenance.json.");
                    }
                    yielded = stack.Peek().Current;
                    if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception e) { errors.Add(e.ToString()); failed = true; break; }
                yield return yielded;
            }
            // A nested diagnostic may fail while an outer iterator owns a temporary
            // obstruction. Dispose the suspended stack so its finally cleanup runs.
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception cleanup) { errors.Add("Diagnostic cleanup: " + cleanup); failed = true; }
            }
            RestoreInput();
            Application.logMessageReceived -= Log;
            bool passed = !failed && errors.Count == 0;
            var report = new Report
            {
                passed = passed,
                scenario = toolkitUi ? "UX-02 UI Toolkit foundation: theme, fonts, scaling, anchored station card and close paths" : fxComfort ? "FX-20 saved comfort settings, four modes, essential cues and occupied layout" : fxAudioMix ? "FX-19 actual generated PCM, bounded room mix, lifecycle and saved ordinary batches" : fxNativeUi ? "FX-18 actual IMGUI feedback, fixed controls and saved-result validation" : fxWorker ? "FX-15 actual saved Pip/Nori/Moss work, bounded poses and lifecycle" : fxCustomer ? "FX-14 actual four-archetype customer motion, geometry, saved checkout and lifecycle" : fxSpecies ? "FX-12 actual five-species motion, complete mesh fit, identity and lifecycle" : fxPlayerInteraction ? "FX-11 saved player interactions, ownership, input and interruption" : fxResearchAutomation ? "FX-10 actual saved research, all-culture modules, lifecycle and Moss handoff" : fxIncubation ? "FX-09 five-species actual incubation, saved hatch, geometry and lifecycle" : fxAdvancedLab ? "FX-08 real advanced sources and saved conditioning" : fxGrower ? "FX-07 real grower states, saved rotations and source ownership" : fxAquarium ? "FX-06R-06 living Pondlet aquarium, real placement and paid sale" : fxCollection ? "FX-06R-05 held collection, actual endpoints and interruption safety" : fxGrowth ? "FX-06R-04 true growth, grower states and lifecycle" : fxDose ? "FX-06R-03 measured dose, true mixture, panel and lifecycle" : fxHatch ? "FX-06R-02 hatch choreography, real first/repeat, mesh clearance and interruptions" : fxOrganic ? "R01 organic correction: actual first/repeat hatch and labeled comfort fixtures" : fxVocabulary ? "FX-06R-01 bounded original particle vocabulary / cosmetic frozen fixture" : fxReview ? "FX-06 actual live motion review recording / isolated manual batch" : fxPondlet ? "FX-05 live Pondlet/tank, actual mesh bounds and paid hatch-to-sale" : fxHands ? "FX-04 real held collection, original hands, pose fit and interruptions" : fxLab ? "FX-03 real lab hero, modal doses, hatch and collector" : fxCue ? "FX-02 real-result presentation lifecycle and conservation" : fxBaseline ? "FX-00 reproducible fixture / current candidate performance comparison" : automation ? "R08.3 desired-stock configuration, pause and Moss handoff with R08.1 regressions" : workerComedy ? "R07.5 committed worker celebrations and interruption safety" : tutorialRestart ? "TUT-10 true startup / " + ShopSession.Arg("-evolutionTutorialRestartSmoke") : tutorialConditioningRecovery ? "TUT-09 canceled and transformed conditioning attribution" : tutorialRecovery ? "TUT-09 reserved-output and displaced-candidate recovery" : tutorialHandoff ? "TUT-08 requested practice and night handoff" : tutorialCustomer ? "TUT-07 production first-customer pacing" : guidance ? "TUT-06 live tutorial room and station guidance" : tutorial ? "TUT-04/05 guided opening actions and paused help" : technician ? "R08.2 helper-fed production-to-sale route with R07.4 technician regressions" : stocker ? "R07.3 reserved rack-to-display stocker" : staff ? "R07.1-R07.2 hired cashier and duty wages" : rooms ? "R06.5v4 aquatic rooms and solid lab door" : comedyGate ? "R06.4 comedy interruption and feedback gate" : comedy ? "R06.3 bounded comedy kit and preview" : presentation ? "R06.1 presentation sample and paused viewer" : labGate ? "R05.5 combined laboratory production and restart gate" : airfin ? "R05.4 Mist culture and Driftwing Airfin" : conditioning ? "R05.3 known conditioning batches" : research ? "R05.2 Spark research and Lanternfin progression" : specimens ? "R05.1b functional specimen storage" : lab ? "R05.1a lab expansion and essential facility relocation" : editedLayout ? "R04.5 edited layouts and obstruction recovery" : storage ? "R04.4 furniture storage and stocked relocation" : decor ? "R04.3 decor and capped appeal" : expansion ? "R04.2 expanded store and display stock" : placement ? "R04.1 shared room placement" : trading ? "R03.5 ordinary trading shifts" : nativeUi ? "Native GUI click probe" : continuous ? "Continuous fresh-save two-day route" : pricing ? "R03.1 pricing and reactions" :
                    customerTypes ? "R03.2 customer types and two-visitor queue" : reputation ? "R03.3 reputation and capped demand" : slap ? "R03.4 intentional cartoon slap" : "Integrated regression",
                context = Application.isEditor ? "Unity Editor Play mode (automated timed session)" : "Windows standalone development player (automated timed session)",
                unityVersion = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceName,
                processor = SystemInfo.processorType,
                savePath = shop.SavePath,
                checks = checks.ToArray(), runtimeErrors = errors.ToArray(), limitations = limitations.ToArray(),
                elapsedSeconds = Time.realtimeSinceStartup - start
            };
            File.WriteAllText(Path.Combine(evidence, "smoke-result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("EVOLUTION_SMOKE_" + (passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " errors=" + errors.Count);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            Application.Quit(passed ? 0 : 1);
#endif
        }
        void RestoreInput()
        {
            try { RestoreGrowerInputIsolation(); }
            catch (Exception cleanup) { errors.Add("Grower input cleanup: " + cleanup); }
            RestoreCycleMouse();
            if (syntheticKeyboard != null && syntheticKeyboard.added) InputSystem.RemoveDevice(syntheticKeyboard);
            syntheticKeyboard = null;
            if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
            previousInputSettings = null;
            if (smokeInputSettings) Destroy(smokeInputSettings);
            smokeInputSettings = null;
        }
        void OnDestroy() { RestoreInput(); Application.logMessageReceived -= Log; }
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks.Add(label); Debug.Log("SMOKE_CHECK " + label); }
        void Ok(ActionResult result, string label) => Check(result.Success, label + (result.Success ? "" : " / " + result.Failure));
        IEnumerator Scenario()
        {
            yield return new WaitForSeconds(1);
            Check(shop.Customers != null && shop.Customers.Ready, "Startup customer navigation ready / " + (shop.Customers == null || shop.Customers.Navigation == null ? "missing navigation" : shop.Customers.Navigation.LastError));
            Check(shop.Model.Currency == 0 && shop.Model.Capacity == 4 && shop.Model.DiscoveredSpeciesIds.Count == 0, "Fresh zero-currency model and base siphon");
            Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == 1, "Fresh shop starts in untimed preparation night 1");
            Check(shop.World.Stations.Count == 7, "Seven usable stations created");
            yield return Capture("00-startup");
            shop.SetPanel(ShopPanel.Welcome); yield return Capture("00-welcome"); shop.SetPanel(ShopPanel.None);
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionSmokeKeyboard");
            syntheticKeyboard.MakeCurrent();
            var keyboard = syntheticKeyboard;
            shop.AllowValidationInput = true;
            Vector3 oldPosition = shop.Player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            Check(keyboard.wKey.isPressed, "Synthetic keyboard routed through real Input System");
            float movementTime = 0; int movementFrames = 0;
            while (movementTime < 0.7f)
            {
                keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                yield return null; movementTime += Time.deltaTime; movementFrames++;
            }
            Debug.Log("MOVEMENT_DIAGNOSTIC frames="+movementFrames+" dt="+movementTime+" enabled="+shop.Player.InputEnabled+" current="+Keyboard.current.name+" minMove="+shop.Player.GetComponent<CharacterController>().minMoveDistance);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(Vector3.Distance(oldPosition, shop.Player.transform.position) > 0.8f, "WASD input moves CharacterController in running scene " + oldPosition + " -> " + shop.Player.transform.position);
            shop.Player.Teleport(new Vector3(0,0.2f,-1.2f),Quaternion.identity);
            yield return MoveFor(Key.W,1f);
            Check(shop.Player.transform.position.z > 1.5f,"Walk through doorway from front store into back lab");
            yield return MoveFor(Key.S,1f);
            Check(shop.Player.transform.position.z < -0.8f,"Walk back through doorway from lab into store");
            shop.Player.Teleport(new Vector3(3,0.2f,-1.2f),Quaternion.identity);
            yield return MoveFor(Key.W,1f);
            Check(shop.Player.transform.position.z > -0.8f && shop.Player.transform.position.z < 0.25f,"Store side partition blocks CharacterController");
            shop.Player.Teleport(new Vector3(3,0.2f,2f),Quaternion.identity);
            yield return MoveFor(Key.S,1f);
            Check(shop.Player.transform.position.z < 1.6f && shop.Player.transform.position.z > 0.75f,"Lab side partition blocks CharacterController");
            shop.AllowValidationInput = false;
            shop.Player.Teleport(new Vector3(-4.1f,0.2f,-0.4f),Quaternion.identity);
            yield return null;
            shop.Player.View.transform.LookAt(StationOf(StationKind.Upgrade).transform.TransformPoint(new Vector3(0,1.1f,0)));
            yield return null; yield return null;
            yield return MoveFor(Key.E,0.05f);
            Check(shop.Focused == null && shop.Panel == ShopPanel.None,"Partition prevents store-side E interaction with lab upgrade bench");
            shop.Player.Teleport(new Vector3(4.7f,0.2f,1.4f),Quaternion.identity);
            shop.Player.View.transform.LookAt(StationOf(StationKind.TankA).transform.TransformPoint(new Vector3(0,1.1f,0)));
            yield return null; yield return null;
            yield return MoveFor(Key.E,0.05f);
            Check(shop.Focused == null && shop.Panel == ShopPanel.None,"Partition prevents lab-side E interaction with store display tank");
            shop.Player.Teleport(new Vector3(0,0.2f,-3.8f), Quaternion.identity);
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.Paused && Cursor.lockState == CursorLockMode.None && Cursor.visible, "Pause releases and shows cursor");
            shop.SetPanel(ShopPanel.None);
            yield return null;
            Check(!shop.Paused,"Resume unpauses simulation");
            if (Application.isBatchMode && Application.isEditor) limitations.Add("Hidden batch Editor has no focused Game View; actual mouse capture on resume requires standalone check. Requested hidden cursor=" + !Cursor.visible + ", actual lock=" + Cursor.lockState);
            else Check(Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, "Resume restores captured cursor");
            yield return Capture("01-room-base");
            yield return Capture("01-front-store");
            yield return Capture("01-sales-counter");
            yield return Capture("01-back-lab");
            foreach (var station in shop.World.Stations)
            {
                var approach = station.transform.TransformPoint(new Vector3(0,0.2f,-2.5f));
                shop.Player.Teleport(approach,station.transform.rotation);
                shop.Player.View.transform.LookAt(station.transform.TransformPoint(new Vector3(0,1.1f,0)));
                yield return null; yield return null;
                Check(shop.Focused == station,"Aim finds station " + station.Kind);
                if (station.Kind == StationKind.Incubator)
                {
                    keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));
                    yield return null; yield return null;
                    Check(shop.Panel == ShopPanel.Incubator,"E input opens the aimed incubator panel");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    yield return Capture("01-incubator-panel"); shop.SetPanel(ShopPanel.None);
                }
            }
            shop.Player.Teleport(new Vector3(0,0.2f,-3.8f),Quaternion.identity);
            yield return Ensure(CultureKind.Bloom,4);
            Check(!shop.Model.Collect(CultureKind.Flow).Success,"Reproduce single-culture full reservoir");
            Ok(shop.Perform(shop.Model.ReleaseCulture(CultureKind.Bloom)),"Release carried Bloom deliberately frees a slot");
            yield return Ensure(CultureKind.Flow,1);
            Check(shop.Model.Available(CultureKind.Flow)==1,"Other culture can be collected after recovery");
            while(shop.Model.Available(CultureKind.Bloom)>0) shop.Model.ReleaseCulture(CultureKind.Bloom);
            shop.Model.ReleaseCulture(CultureKind.Flow);
            yield return Ensure(CultureKind.Bloom, 2);
            yield return Ensure(CultureKind.Flow, 2);
            Check(!shop.Model.Collect(CultureKind.Bloom).Success, "Shared four-unit reservoir blocks excess collection");
            for (int i=0;i<2;i++) { Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Stage Bloom for invalid mix"); Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "Stage Flow for invalid mix"); }
            Check(!shop.Perform(shop.Model.BeginIncubation()).Success && shop.Model.ReservedCultureCount == 4, "2+2 unsupported recipe consumes nothing");
            Ok(shop.Perform(shop.Model.ClearDoses()), "Unsupported mixture fully returned");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Pondlet Bloom dose");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "Pondlet Flow dose");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Pondlet incubation begins");
            Check(!shop.Model.BeginIncubation().Success && shop.Model.ReservedCultureCount == 2, "Repeated start cannot consume ingredients twice");
            yield return new WaitForSeconds(1.5f);
            shop.SetPanel(ShopPanel.Pause);
            float remaining = shop.Model.Incubator.SecondsRemaining;
            Check(shop.SaveNow() && shop.ReloadSaved(), "Real JSON save and load during incubation");
            Check(Mathf.Abs(shop.Model.Incubator.SecondsRemaining-remaining)<0.001f, "Mid-incubation remaining time preserved");
            yield return new WaitForSeconds(0.7f);
            Check(Mathf.Abs(shop.Model.Incubator.SecondsRemaining-remaining)<0.001f, "Paused incubation does not advance");
            shop.SetPanel(ShopPanel.None);
            yield return Ready();
            Check(shop.Model.IsDiscovered("pondlet"), "Pondlet discovered");
            yield return Capture("02-pondlet-ready");
            Ok(shop.TransferToDisplay("tank-a"), "Pondlet transferred to visible tank A");
            Check(!shop.Model.TransferToTank("tank-b").Success, "Repeated transfer cannot duplicate a specimen");
            yield return Produce("bubblebelly");
            yield return Capture("03-bubblebelly-ready");
            Ok(shop.TransferToDisplay("tank-b"), "Bubblebelly transferred to tank B");
            yield return Produce("ribbonfin");
            for (float t = 0; t < 30; t += 0.25f)
            {
                shop.World.Animate(t,0);
                if (!shop.World.ValidateTankBounds(out var bounds)) throw new InvalidOperationException(bounds);
            }
            Check(true,"Stocked creatures stay within tank glass across 120 animation samples");
            Check(shop.Model.DiscoveredSpeciesIds.Count == 3, "All three exact recipes discovered");
            yield return new WaitForSeconds(3.6f);
            yield return Capture("04-ribbonfin-ready");
            string waiting = shop.Model.Incubator.SpecimenId;
            Check(!shop.TransferToDisplay("tank-a").Success && !shop.Model.TransferToTank("tank-b").Success && shop.Model.Incubator.SpecimenId == waiting, "Two full tanks retain waiting Ribbonfin without loss");
            yield return Capture("04-full-tanks-ribbonfin");
            Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == 1, "Timed production does not consume the untimed preparation night");
            Check(!shop.ServeCustomer() && shop.Model.Currency == 0 && shop.Model.GetTank("tank-a") != null, "Counter without a visitor cannot create a sale at night");
            var counterStation = StationOf(StationKind.Counter).transform;
            shop.Player.Teleport(counterStation.TransformPoint(new Vector3(0,0.2f,-2.5f)),counterStation.rotation);
            shop.Player.View.transform.LookAt(counterStation.TransformPoint(new Vector3(0,1.1f,0)));
            yield return null; yield return null;
            yield return MoveFor(Key.E,0.05f);
            Check(shop.Panel == ShopPanel.Counter, "E input opens the front counter for preparation and trading");
            yield return Capture("09-night-counter");
            Check(shop.OpenForDay() && !shop.OpenForDay(), "Open command starts exactly one trading day");
            float tradingStart = shop.Cycle.SecondsRemaining;
            yield return new WaitForSeconds(0.7f);
            Check(shop.Cycle.SecondsRemaining < tradingStart - 0.5f, "Trading clock advances while counter is open");
            shop.SetPanel(ShopPanel.Pause);
            float tradingRemaining = shop.Cycle.SecondsRemaining;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Cycle.Phase == ShopDayPhase.Open && Mathf.Abs(shop.Cycle.SecondsRemaining-tradingRemaining)<0.001f, "Midday clock and phase survive real file reload");
            yield return new WaitForSeconds(0.7f);
            Check(Mathf.Abs(shop.Cycle.SecondsRemaining-tradingRemaining)<0.001f, "Pause freezes trading clock");
            shop.SetPanel(ShopPanel.Counter); yield return Capture("10-day-counter");
            Ok(shop.Perform(shop.Model.SellFromTank("tank-a")), "Baseline domain Pondlet sale with arrivals suspended");
            Check(shop.Model.Currency == 6 && !shop.Model.SellFromTank("tank-a").Success, "Repeated domain sale pays once");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency == 6 && shop.Model.GetTank("tank-a") == null && shop.Model.IsDiscovered("pondlet"), "Real save after sale preserves money, empty tank, discovery");
            Ok(shop.TransferToDisplay("tank-a"), "Waiting output transfers after space is freed");
            for (float t=0;t<30;t+=0.25f) {shop.World.Animate(t,0); if(!shop.World.ValidateTankBounds(out var bounds)) throw new InvalidOperationException(bounds);}
            Check(true,"Ribbonfin tail stays inside tank across 120 animation samples");
            Check(!shop.Model.BuyUpgrade().Success, "Insufficient funds block upgrade");
            Ok(shop.Perform(shop.Model.SellFromTank("tank-b")), "Baseline domain Bubblebelly sale with arrivals suspended");
            Ok(shop.Perform(shop.Model.SellFromTank("tank-a")), "Baseline domain Ribbonfin sale with arrivals suspended");
            shop.SetPanel(ShopPanel.None);
            Check(shop.Model.Currency == 26, "Three species sales total 26");
            Vector3 baseChamber = shop.World.ToolChamber.localScale;
            Ok(shop.Perform(shop.Model.BuyUpgrade()), "Purchase siphon upgrade");
            Check(shop.Model.Currency == 2 && shop.Model.Capacity == 8 && !shop.Model.BuyUpgrade().Success, "Upgrade changes capacity and charges once");
            Check(shop.World.ToolChamber.localScale != baseChamber && shop.Model.CollectionSecondsPerUnit < shop.Config.BaseCollectionSeconds, "Upgrade physically enlarges chamber and speeds collection");
            yield return Capture("05-upgraded-siphon");
            Check(shop.SetBaselinePreview(true), "Development baseline comparison enabled");
            Check(shop.World.ToolChamber.localScale == baseChamber, "Comparison restores original chamber geometry");
            shop.SetBaselinePreview(false);
            yield return Ensure(CultureKind.Bloom, 1);
            yield return Ensure(CultureKind.Flow, 1);
            Ok(shop.Perform(shop.Model.UseKnownRecipe("pondlet")), "Known recipe prepares exact doses in one action");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Repeat production begins after upgrade");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.UpgradeOwned && shop.Model.Incubator.Phase == IncubatorPhase.Incubating, "Upgrade and repeat production survive reload");
            shop.SetPanel(ShopPanel.Reset); shop.SetPanel(ShopPanel.Pause);
            Check(shop.Model.Currency == 2 && shop.Model.UpgradeOwned, "Cancel reset preserves progression");
            shop.SetPanel(ShopPanel.Journal);
            yield return Capture("06-journal");
            shop.SetPanel(ShopPanel.Pause); yield return Capture("07-pause-settings");
            shop.SetPanel(ShopPanel.Reset); yield return Capture("08-reset-confirmation");
            Check(File.Exists(shop.SavePath), "Separate smoke save exists; user slot untouched");
            string blockedTemp = shop.SavePath + ".tmp";
            Directory.CreateDirectory(blockedTemp);
            var beforeFailedReset = shop.Model;
            var cycleBeforeFailedReset = shop.Cycle;
            try
            {
                Check(!shop.SaveNow() && !string.IsNullOrEmpty(shop.SaveError),"Save failure is reported persistently");
                shop.Notify("An unrelated notification");
                shop.ConfirmNewGame();
                Check(ReferenceEquals(shop.Model,beforeFailedReset) && ReferenceEquals(shop.Cycle,cycleBeforeFailedReset) && shop.Model.UpgradeOwned && !string.IsNullOrEmpty(shop.SaveError),"Failed reset preserves current progression, day and error");
                shop.SendMessage("Quit");
                Check(shop.Panel == ShopPanel.Pause && !string.IsNullOrEmpty(shop.SaveError), "In-game quit stays open after a failed save");
            }
            finally { Directory.Delete(blockedTemp); }
            Check(shop.SaveNow() && string.IsNullOrEmpty(shop.SaveError),"Saving recovers after file obstruction is removed");
            string corrupt = Path.Combine(evidence,"corrupt-save.json"); File.WriteAllText(corrupt,"{not valid json");
            Check(!SaveStore.Read(corrupt,shop.Config,out _,out _,out _) && File.ReadAllText(corrupt)=="{not valid json", "Malformed save rejected and original preserved");
            shop.SetPanel(ShopPanel.Counter);
            yield return Ready();
            Ok(shop.TransferToDisplay("tank-a"), "Stock a real unsold specimen before closing");
            string closingSpecimen = shop.Model.GetTank("tank-a").Value.Id;
            shop.SetPanel(ShopPanel.CloseShop); yield return Capture("11-close-confirmation");
            shop.SetPanel(ShopPanel.Counter);
            Check(shop.Cycle.Phase == ShopDayPhase.Open, "Cancelling closing confirmation keeps shop open");
            Check(shop.CloseForNight() && !shop.CloseForNight() && !shop.OpenForDay(), "Early closing accepts once and cannot reopen during closing");
            Check(!shop.ServeCustomer() && shop.Model.GetTank("tank-a").Value.Id == closingSpecimen && shop.Model.Currency == 2, "No visitor means no checkout or payout during closing");
            shop.SetPanel(ShopPanel.Pause);
            float closingRemaining = shop.Cycle.SecondsRemaining;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Cycle.Phase == ShopDayPhase.Closing && Mathf.Abs(shop.Cycle.SecondsRemaining-closingRemaining)<0.001f, "Closing survives file reload without restarting countdown");
            yield return new WaitForSeconds(0.3f);
            Check(Mathf.Abs(shop.Cycle.SecondsRemaining-closingRemaining)<0.001f, "Pause freezes closing transition");
            shop.SetPanel(ShopPanel.Counter); yield return Capture("12-closing-counter");
            float closingDeadline = Time.realtimeSinceStartup + 5;
            while(shop.Cycle.Phase == ShopDayPhase.Closing)
            { if(Time.realtimeSinceStartup>closingDeadline) throw new TimeoutException("Closing did not finish"); yield return null; }
            Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == 2 && shop.Model.Currency == 2 && shop.Model.UpgradeOwned, "Timed early closing reaches night 2 with progression retained");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Cycle.DayNumber == 2 && shop.Cycle.Phase == ShopDayPhase.Night && shop.Model.GetTank("tank-a").Value.Id == closingSpecimen && shop.Model.Currency == 2, "Next preparation night persists with the exact unsold specimen and unchanged money");
            yield return Capture("13-next-night");
            Check(shop.OpenForDay(), "Day 2 can open with carried stock");
            // Accelerate only the boundary checks; earlier production and early closing use real frames.
            shop.Advance(ShopDayCycle.DefaultTradingSeconds);
            Check(shop.Cycle.Phase == ShopDayPhase.Closing && shop.Cycle.DayNumber == 2, "Day timeout enters bounded closing (accelerated delta)");
            shop.Advance(ShopDayCycle.DefaultClosingSeconds);
            Check(shop.Cycle.Phase == ShopDayPhase.Night && shop.Cycle.DayNumber == 3, "Second cycle finishes without repeated day increment (accelerated delta)");
            shop.ConfirmNewGame();
            Check(shop.Model.Currency==0 && !shop.Model.UpgradeOwned && shop.Model.DiscoveredSpeciesIds.Count==0 && shop.Model.Incubator.Phase==IncubatorPhase.Dosing && shop.Cycle.DayNumber==1 && shop.Cycle.Phase==ShopDayPhase.Night, "Confirmed reset clears progression, active batch and day cycle");
            Check(shop.OpenForDay() && shop.CloseForNight(), "Empty fresh shop can open and close without a progression dead end");
            shop.ConfirmNewGame();
            yield return CustomerScenario();
            yield return null;
        }
        IEnumerator MoveFor(Key key, float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                syntheticKeyboard.MakeCurrent();
                InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(key));
                yield return null; elapsed += Time.deltaTime;
            }
            InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());
            yield return null;
        }
        Station StationOf(StationKind kind)
        {
            foreach (var station in shop.World.Stations) if (station.Kind == kind) return station;
            throw new InvalidOperationException("Missing station " + kind);
        }
        IEnumerator Ensure(CultureKind kind, int units)
        {
            var grower = StationOf(kind == CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow);
            shop.Player.Teleport(grower.transform.TransformPoint(new Vector3(0,0.2f,-2.5f)),grower.transform.rotation);
            shop.Player.View.transform.LookAt(grower.transform.TransformPoint(new Vector3(0,1.1f,0)));
            yield return null; yield return null;
            float timeout = Time.realtimeSinceStartup + 12;
            shop.ScriptedHarvest = kind;
            while (shop.Model.Available(kind)<units)
            {
                if (Time.realtimeSinceStartup > timeout) throw new TimeoutException("Timed harvesting " + kind);
                yield return null;
            }
            shop.ScriptedHarvest = null; shop.StopHarvest();
            checks.Add("Timed siphon harvest " + kind + " to " + units);
        }
        IEnumerator Produce(string id)
        {
            var recipe = shop.Model.GetSpecies(id);
            yield return Ensure(CultureKind.Bloom,recipe.Bloom);
            yield return Ensure(CultureKind.Flow,recipe.Flow);
            for(int i=0;i<recipe.Bloom;i++) Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)),id+" Bloom dose");
            for(int i=0;i<recipe.Flow;i++) Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)),id+" Flow dose");
            Ok(shop.Perform(shop.Model.BeginIncubation()),id+" incubation begins");
            yield return Ready();
        }
        IEnumerator Ready()
        {
            float timeout = Time.realtimeSinceStartup + 14;
            while(shop.Model.Incubator.Phase != IncubatorPhase.Ready)
            {if(Time.realtimeSinceStartup>timeout) throw new TimeoutException("Incubation did not mature"); yield return null;}
        }
        IEnumerator Capture(string name)
        {
            yield return null;
            var camera = shop.Player.View;
            Vector3 cameraPosition = camera.transform.position; Quaternion cameraRotation = camera.transform.rotation;
            if (name.Contains("ready"))
            {
                var incubator = StationOf(StationKind.Incubator).transform;
                camera.transform.position = incubator.TransformPoint(new Vector3(0,1.6f,-2.4f));
                camera.transform.LookAt(incubator.TransformPoint(new Vector3(0,1.5f,0)));
            }
            if (name.Contains("full-tanks"))
            {
                var a = StationOf(StationKind.TankA).transform.position;
                var b = StationOf(StationKind.TankB).transform.position;
                camera.transform.position = new Vector3(1.2f,2.15f,-4f);
                camera.transform.LookAt((a+b)*0.5f + Vector3.up*1.55f);
            }
            if (name == "01-front-store") {camera.transform.position = new Vector3(-2.4f,1.85f,0); camera.transform.LookAt(new Vector3(3.2f,1.5f,-2.5f));}
            if (name == "01-sales-counter")
            {
                var counter = StationOf(StationKind.Counter).transform;
                camera.transform.position = counter.TransformPoint(new Vector3(0,1.6f,-3.2f));
                camera.transform.LookAt(counter.TransformPoint(new Vector3(0,1.4f,0)));
            }
            if (name == "01-back-lab") {camera.transform.position = new Vector3(2.75f,1.8f,1f); camera.transform.LookAt(new Vector3(-1.8f,1.5f,3.6f));}
            // Let the normal focus query catch up to this diagnostic camera viewpoint.
            yield return null; yield return null;
            var target = new RenderTexture(1600,900,24);
            Debug.Log("CAPTURE_PIPELINE " + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);
            foreach (var renderer in shop.World.GetComponentsInChildren<Renderer>()) if (renderer.sharedMaterial && renderer.sharedMaterial.name == "Bloom coral") {Debug.Log("BLOOM_MATERIAL " + renderer.sharedMaterial.shader.name + " supported=" + renderer.sharedMaterial.shader.isSupported + " color=" + renderer.sharedMaterial.GetColor("_BaseColor")); break;}
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest {destination=target});
            RenderTexture.active = target;
            var image = new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(evidence,name+"-camera.png"),image.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=null; Destroy(image); Destroy(target);
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence,name+"-screen.png"));
            yield return new WaitForSeconds(0.2f);
            camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
        }
    }
}
#endif
