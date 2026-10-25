#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    /// <summary>
    /// GR-02's Windows route (-evolutionSmoke -evolutionGrowthSmoke -evolutionEvidence DIR): a fresh greybox shop in its
    /// own save; a held mouse on a grower fills the siphon; a mix incubates and hatches; a hatchling is carried to the
    /// shop's tank by E; a school forms and peek reads a creature; the mix log and "close to something new"; a day with
    /// real visitors served by a held mouse at the counter, and its receipt; a second tank; era 2 and unit B's wall
    /// opening; a station upgrade; save and reload; comfort; captures of each, in smoke-result.json like the classic routes.
    /// </summary>
    public sealed partial class GrowthSmoke : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string context, scenario, unityVersion, graphics, processor, savePath;
            public string[] checks, runtimeErrors, limitations;
            public float elapsedSeconds, averageFrameMs, worstFrameMs;
        }

        GrowthSession s;
        string evidence;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), limitations = new List<string>();
        float start, frameSum, worstFrame; int frames;
        InputSettings previousSettings, smokeSettings;
        Keyboard keyboard;
        Mouse mouse;
        readonly List<InputDevice> isolated = new List<InputDevice>();

        IEnumerator Start()
        {
            s = GetComponent<GrowthSession>();
            evidence = ShopSession.Arg("-evolutionEvidence") ?? Application.temporaryCachePath;
            Directory.CreateDirectory(evidence);
            start = Time.realtimeSinceStartup;
            Application.logMessageReceived += Log;
            bool failed = false;
            string route = GrowthGreybox.SmokeRoute;
            var steps = route == "tanks" ? TanksScenario() : route == "density" ? DensityScenario() : route == "cultures" ? CulturesScenario() : route == "breeding" ? BreedingScenario() : route == "stations" ? StationsScenario() : route == "power" ? PowerScenario() : route == "line" ? LineScenario() : route == "helpers" ? HelpersScenario() : route == "spaces" ? SpacesScenario() : route == "exhibition" ? ExhibitionScenario() : route == "creatures" ? CreaturesScenario() : Scenario();
            // GR-12: nested steps (such as GR-03's Measure) are run here, not by Unity, so a check failing inside one stops the
            // route with its reason instead of leaving the route waiting until the runner's timeout.
            var stack = new Stack<IEnumerator>();
            stack.Push(steps);
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    current = top.Current;
                }
                catch (Exception e) { errors.Add("GR route stopped: " + e); failed = true; break; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            RestoreInput();
            Application.logMessageReceived -= Log;
            bool passed = !failed && errors.Count == 0;
            var report = new Report
            {
                passed = passed,
                scenario = route == "tanks" ? TanksScenarioText : route == "density" ? DensityScenarioText : route == "cultures" ? CulturesScenarioText : route == "breeding" ? BreedingScenarioText : route == "stations" ? StationsScenarioText : route == "power" ? PowerScenarioText : route == "line" ? LineScenarioText : route == "helpers" ? HelpersScenarioText : route == "spaces" ? SpacesScenarioText : route == "exhibition" ? ExhibitionScenarioText : route == "creatures" ? CreaturesScenarioText :
                    "GR-02 greybox: fresh shop, grower collection by a held mouse, never-fail mixing and the mix log, hatch, carry, tank and school, peek, a day of visitors served at the counter, the receipt, a second tank, era 2 and unit B, an upgrade, save and reload, comfort",
                context = Application.isEditor ? "Unity Editor Play mode (automated)" : "Windows standalone development player (automated)",
                unityVersion = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType, savePath = s.SavePath,
                checks = checks.ToArray(), runtimeErrors = errors.ToArray(), limitations = limitations.ToArray(),
                elapsedSeconds = Time.realtimeSinceStartup - start,
                averageFrameMs = frames == 0 ? 0 : frameSum / frames * 1000f, worstFrameMs = worstFrame * 1000f,
            };
            File.WriteAllText(Path.Combine(evidence, "smoke-result.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(evidence, "ui-log.txt"), string.Join("\n", s.Ui.Log));
            Debug.Log("EVOLUTION_SMOKE_" + (passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " errors=" + errors.Count);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            Application.Quit(passed ? 0 : 1);
#endif
        }

        void Update()
        {
            if (Time.realtimeSinceStartup - start < 3) return;
            frameSum += Time.unscaledDeltaTime; frames++;
            worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
        }

        void OnDestroy() { RestoreInput(); Application.logMessageReceived -= Log; }

        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }

        void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks.Add(label); Debug.Log("SMOKE_CHECK " + label); }

        void Ok(GrowthResult r, string label) => Check(r.Success, label + (r.Success ? "" : " / " + r));

        /// <summary>Checks the reticle is on a station; a failure says where the view was and what the ray met.</summary>
        void Aimed(GrowthTargetKind kind, string id, string label)
        {
            if (s.Focused != null && s.Focused.Kind == kind && (id == null || s.Focused.Id == id)) { checks.Add(label); Debug.Log("SMOKE_CHECK " + label); return; }
            var v = s.Player.View.transform;
            string met = Physics.Raycast(v.position, v.forward, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                ? hit.collider.name + " (" + (hit.collider.GetComponentInParent<GrowthTarget>()?.Id ?? "no target") + ") at " + hit.distance.ToString("0.00") + " m"
                : "nothing within 10 m";
            throw new InvalidOperationException(label + " (focused " + (s.Focused == null ? "none" : s.Focused.Kind + " " + s.Focused.Id) + "; eye " + v.position.ToString("F2") +
                " looking " + v.forward.ToString("F2") + "; the ray meets " + met + "; card " + s.Card + ")");
        }

        void RestoreInput()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            keyboard = null; mouse = null;
            foreach (var device in isolated) if (device.added && !device.enabled) InputSystem.EnableDevice(device);
            isolated.Clear();
            if (previousSettings != null) InputSystem.settings = previousSettings;
            previousSettings = null;
            if (smokeSettings) Destroy(smokeSettings);
            smokeSettings = null;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Puts the player's feet at a point, turned to a yaw and pitch: the view a player standing there would have.</summary>
        void Stand(Vector3 feet, float yaw, float pitch)
        {
            s.Player.Teleport(new Vector3(feet.x, 0.05f, feet.z), Quaternion.Euler(0, yaw, 0));
            s.Player.Pitch = pitch;
        }

        /// <summary>Stands at a point looking at a target point (eye 1.6 m above the feet).</summary>
        void LookAt(Vector3 feet, Vector3 target)
        {
            Vector3 eye = new Vector3(feet.x, 1.65f, feet.z), d = target - eye;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            Stand(feet, yaw, pitch);
        }

        IEnumerator Shot(string name)
        {
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, name + "-screen.png"));
            yield return new WaitForSeconds(0.25f);
            checks.Add("Capture " + name);
        }

        IEnumerator Wait(Func<bool> condition, float timeout, string reason)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("GR-02 waiting for " + reason);
                yield return null;
            }
        }

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        void Hold(bool down) => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(down ? 1 : 0) });

        /// <summary>GR-13: points the route's mouse at an element of the open card and checks the hover tip says what it should.</summary>
        IEnumerator HoverOn(Func<UnityEngine.UIElements.VisualElement, bool> which, string tip, string what, string shot)
        {
            var card = s.Ui.CardElement;
            var element = card == null ? null : UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.VisualElement>(card).ToList().FirstOrDefault(which);
            if (element == null) { Check(false, what + " (nothing to point at)"); yield break; }
            Rect r = element.worldBound; Vector2 panel = s.Ui.Root.layout.size;
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(r.center.x * Screen.width / panel.x, Screen.height - r.center.y * Screen.height / panel.y) });
            yield return null; yield return null;
            Check(s.Ui.TipText == tip, what + " (\"" + s.Ui.TipText + "\")");
            yield return Shot(shot);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2) });
            yield return null;
        }

        /// <summary>A creature made by the shop's own actions with the incubation advanced by its clock (labelled accelerated).</summary>
        void Produce(int bloom, int flow)
        {
            var shop = s.Shop;
            shop.Tick(30);
            int needBloom = bloom - shop.Held.Count(c => c == "bloom"), needFlow = flow - shop.Held.Count(c => c == "flow");
            for (int i = 0; i < needBloom; i++) Ok(shop.Collect("bloom", shop.HoldSeconds), "Collect fixture Bloom");
            for (int i = 0; i < needFlow; i++) Ok(shop.Collect("flow", shop.HoldSeconds), "Collect fixture Flow");
            for (int i = 0; i < bloom; i++) Ok(shop.AddDose("bloom"), "Dose fixture Bloom");
            for (int i = 0; i < flow; i++) Ok(shop.AddDose("flow"), "Dose fixture Flow");
            Ok(shop.StartIncubation(), "Start fixture incubation");
            s.Advance(shop.IncubationSeconds + 0.05f);
            Ok(shop.TakeHatchling(), "Take fixture hatchling");
        }

        /// <summary>Every route's first checks: the greybox replaced the classic session, saves in the evidence folder only, at the requested size.</summary>
        IEnumerator Opening()
        {
            yield return new WaitForSeconds(1);
            Check(!FindObjectsByType<ShopSession>(FindObjectsSortMode.None).Any(), "The greybox replaced the classic session (no ShopSession in the scene)");
            Check(Path.GetFullPath(s.SavePath) == Path.GetFullPath(Path.Combine(evidence, GrowthSession.SaveFileName)), "The greybox saves to its own isolated file in the evidence folder");
            Check(!string.Equals(Path.GetFullPath(s.SavePath), Path.GetFullPath(SaveStore.DefaultPath), StringComparison.OrdinalIgnoreCase), "The greybox never writes the classic save slot");
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows resolution " + width + "x" + height);
            }
        }

        /// <summary>The route's own mouse and keyboard; the real ones are switched off in this player until the end.</summary>
        void TakeInput()
        {
            previousSettings = InputSystem.settings; smokeSettings = Instantiate(previousSettings);
            smokeSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeSettings;
            keyboard = InputSystem.AddDevice<Keyboard>("GR keyboard"); mouse = InputSystem.AddDevice<Mouse>("GR mouse");
            // The real mouse and keyboard are switched off in this player's input system for the route, so a hand on the
            // desk cannot turn the view or release a held button; they are switched back on at the end.
            foreach (var device in InputSystem.devices.ToArray())
                if (device != keyboard && device != mouse && device.enabled && (device is Pointer || device is Keyboard)) { InputSystem.DisableDevice(device); isolated.Add(device); }
            limitations.Add("The route's synthetic mouse and keyboard are the only pointer and keyboard while it runs: " + isolated.Count + " other device(s) are switched off in this player's input system and switched back on at the end.");
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            s.AllowValidationInput = true;
        }

        // ---------------------------------------------------------------- the route

        IEnumerator Scenario()
        {
            limitations.Add("GR-02 greybox route: the shop's own actions drive most steps (what the card buttons call); a synthetic mouse is held on the grower and the counter and a synthetic E pressed at the tanks, through the session's real input; some creatures are made by labelled accelerated incubation and the day is advanced by the shop's own clock; star and credit fixtures are labelled save edits. UI Toolkit buttons are not clicked (the classic routes cover that path). Placeholder art; the user's playtest is the gate.");
            yield return Opening();
            var shop = s.Shop;
            Check(shop.Phase == GrowthPhase.Night && shop.Day == 1 && shop.Credits == GrowthContent.StartingCredits && shop.Containers.Count == 1 && shop.Containers[0].Spaces == 4,
                "A fresh greybox shop: night 1, " + GrowthContent.StartingCredits + " credits, one 4-space Starter tank");
            Check(s.World.Tanks.Count == 1 && s.World.UnitBWallStanding, "One tank in the world and unit B behind its wall");
            TakeInput();

            s.SetCard(GrowthCard.Welcome); yield return Shot("00-welcome"); s.SetCard(GrowthCard.None);
            Stand(GrowthWorld.Spawn, -90, 8); yield return Shot("01-back-room");
            LookAt(new Vector3(0.8f, 0, 4.6f), new Vector3(-0.5f, 1.2f, -1.5f)); yield return Shot("02-shop-from-the-back-room");
            LookAt(new Vector3(1.6f, 0, -3.4f), new Vector3(-1.2f, 1.2f, 2.2f)); yield return Shot("03-shop-unit-a");

            // A held mouse on the Bloom grower fills the siphon, one dose every 0.8 s, three at level 1.
            LookAt(GrowthWorld.GrowerPositions["bloom"] + new Vector3(1.4f, 0, 0), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "bloom", "The reticle focuses the Bloom grower");
            Hold(true);
            yield return Wait(() => shop.Held.Count >= 2, 6, "two Bloom doses from a held mouse");
            Check(s.CollectingNow || shop.Held.Count >= 2, "Holding the mouse collects");
            yield return Shot("04-collecting");
            Hold(false); yield return null;
            Check(shop.Held.Count == 2 && shop.Held.All(c => c == "bloom"), "Two Bloom doses in the siphon");
            LookAt(GrowthWorld.GrowerPositions["flow"] + new Vector3(1.4f, 0, 0), GrowthWorld.GrowerPositions["flow"] + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Hold(true);
            yield return Wait(() => shop.Held.Count >= 3, 4, "a Flow dose");
            yield return new WaitForSeconds(1.0f);
            Hold(false); yield return null;
            Check(shop.Held.Count == 3, "The level 1 siphon stops at three doses");

            // The incubator: a mix of Bloom and Flow, its real 12 s, a new creature.
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Incubator, "E opens the incubator card");
            // GR-13: the card's tabs, a reason on hover, the HUD's one next step and one note at a time.
            Check(s.Ui.CurrentTab == "Mix" && s.Ui.CardText().Contains("Incubator · level 1 of 4") && s.Ui.CardText().Contains("Upgrade · "),
                "The incubator card opens on its Mix tab; its Upgrade tab has the level track and the next level's button");
            yield return HoverOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Start"), "Add a dose first", "Start, with nothing in the chamber, says why when pointed at", "05a-start-why");
            Check(s.Ui.HudNext == shop.Objectives().First(g => !g.done).text, "The HUD's one next step is the first goal still to do: " + s.Ui.HudNext);
            s.Ui.Toast("Validation note 1"); s.Ui.Toast("Validation note 2");
            Check(s.Ui.HudNote == "Validation note 2", "Notes come one at a time, the newest showing");
            Ok(s.Act(() => shop.AddDose("bloom")), "Add a Bloom dose");
            Ok(s.Act(() => shop.AddDose("flow")), "Add a Flow dose");
            yield return Shot("05-incubator-card");
            Check(s.Ui.ShowTabNamed("Upgrade"), "The Upgrade tab opens");
            yield return Shot("05b-incubator-upgrade");
            Check(s.Ui.ShowTabNamed("Mix"), "Back to the Mix tab");
            Ok(s.Act(shop.StartIncubation), "Mix and incubate");
            float started = Time.realtimeSinceStartup;
            yield return Wait(() => shop.Hatchling != null, 20, "the 12 s incubation");
            float took = Time.realtimeSinceStartup - started;
            Check(took > 11 && took < 14.5f, "The level 1 incubation takes 12 s (took " + took.ToString("0.0") + ")");
            Check(shop.Discovered.Contains("pondlet") && shop.Hatchling.Species == "pondlet", "Bloom 1 + Flow 1 hatched a new Pondlet");
            Check(s.Ui.Log.Any(l => l.Contains("New creature: Pondlet")), "The discovery is announced");
            yield return new WaitForSeconds(0.3f);
            Check(s.Ui.CardText().Contains("Take the Pondlet"), "The open incubator card offers the hatchling");
            yield return Shot("06-hatched");
            Ok(s.Act(shop.TakeHatchling), "Take the hatchling");
            s.SetCard(GrowthCard.None);

            // Carry it to the shop's tank: E on the tank puts it in.
            var tank = shop.Containers[0];
            var a1 = GrowthWorld.Places["a1"];
            LookAt(a1.Browse + new Vector3(0, 0, -0.6f), a1.Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, tank.Id, "The reticle focuses the Starter tank");
            yield return Press(Key.E);
            Check(tank.Occupants.Count == 1 && tank.Occupants[0].Species == "pondlet" && shop.Carried.Count == 0, "E puts the carried Pondlet into the tank");
            Check(s.World.Tanks[tank.Id].Creatures.Count == 1, "The Pondlet swims in the world's tank");

            // A school of three, a Bubblebelly beside it; peek reads a creature and E takes it.
            Produce(1, 1); Produce(1, 1); Produce(2, 1);
            Check(shop.Discovered.Contains("bubblebelly"), "Bloom 2 + Flow 1 found the Bubblebelly");
            for (int i = 0; i < 3; i++) Ok(s.Act(() => shop.PutIn(tank.Id)), "Put a creature in the tank");
            Check(tank.Used == 4 && tank.InSchool("pondlet"), "Four in the 4-space tank, three Pondlets swim as a school");
            Ok(shop.PutIn(tank.Id).Failure == GrowthFailure.HandsEmpty ? GrowthResult.Ok() : GrowthResult.Fail(GrowthFailure.Busy), "Nothing more to put in");
            LookAt(a1.Browse + new Vector3(0, 0, 0.15f), a1.Position + new Vector3(0, 1.18f, -0.1f));
            yield return Wait(() => s.Peeking && s.Player.View.fieldOfView < GrowthSession.PeekFov + 1, 3, "peek to ease in");
            Check(s.Player.View.fieldOfView < 50, "Looking closely at the glass eases into peek (field of view " + s.Player.View.fieldOfView.ToString("0") + ")");
            float hunt = Time.realtimeSinceStartup + 12;
            while (s.PeekCreature == null && Time.realtimeSinceStartup < hunt) yield return null;
            Check(s.PeekCreature != null, "A creature comes under the reticle in peek");
            string peekName = s.FocusName(out string peekStatus);
            Check(peekName == "Pondlet" || peekName == "Bubblebelly", "Peek names the creature under the reticle: " + peekName + " (" + peekStatus + ")");
            yield return Shot("07-peek");
            s.PeekOn = false; yield return null; yield return new WaitForSeconds(0.4f);
            Check(s.Player.View.fieldOfView > 66, "Peek switched off keeps the normal view");
            s.PeekOn = true; s.ReducedMotion = true; yield return null; yield return null;
            Check(s.Player.View.fieldOfView < 50, "Reduced motion: peek is instant");
            s.ReducedMotion = false;
            LookAt(a1.Browse + new Vector3(0.8f, 0, -1.0f), a1.Position + new Vector3(0, 1.1f, 0)); yield return Shot("08-school");
            s.SetCard(GrowthCard.Tank, tank.Id); yield return Shot("09-tank-card"); s.SetCard(GrowthCard.None);

            // The mix log and "close to something new": two Bloom is one Flow from the Bubblebelly... now known, so try
            // two Flow, one Bloom short of the Ribbonfin.
            shop.Tick(30);
            Ok(shop.Collect("flow", shop.HoldSeconds), "Collect Flow"); Ok(shop.Collect("flow", shop.HoldSeconds), "Collect Flow");
            Ok(shop.AddDose("flow"), "Dose Flow"); Ok(shop.AddDose("flow"), "Dose Flow");
            Check(shop.Preview().CloseToNew, "Two Flow is one dose from something new");
            s.SetCard(GrowthCard.Incubator); yield return Shot("10-close-to-new");
            Ok(s.Act(shop.StartIncubation), "Incubate the near miss");
            s.Advance(shop.IncubationSeconds + 0.05f);
            Check(shop.Hatchling != null && !shop.Discovered.Contains("ribbonfin") && shop.LastMix.DistanceToNew == 1, "The near miss makes a known creature and is logged as one dose from new");
            Ok(s.Act(shop.TakeHatchling), "Take it");
            Check(shop.MixLog.Count == 3, "The mix log holds the three different mixes");
            yield return Shot("11-mix-log");
            s.SetCard(GrowthCard.None);

            // A day: real visitors walk in, choose and queue; a held mouse at the counter bags the sale.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            Check(shop.Phase == GrowthPhase.Day && Mathf.Approximately(shop.DaySecondsLeft, GrowthContent.DaySeconds), "A 5:30 day begins");
            yield return Wait(() => shop.FrontOfQueue != null, 30, "a visitor to choose and queue");
            var front = shop.FrontOfQueue;
            var reserved = shop.Stock.First(x => x.Id == front.SpecimenId);
            Check(reserved.ReservedBy == front.Id, "The visitor reserved one creature");
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0));
            yield return Wait(() => s.World.VisitorViews.TryGetValue(front.Id, out var view) && view.Arrived, 20, "the visitor to reach the queue");
            yield return null;
            Aimed(GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            yield return Shot("12-day-queue");
            int credits = shop.Credits, sales = shop.SalesTotal, price = shop.Price(reserved.Species);
            Hold(true);
            yield return Wait(() => shop.SalesTotal > sales, 6, "the held mouse to bag the sale");
            Hold(false);
            Check(shop.Credits == credits + price && shop.Reputation >= GrowthContent.RepPerSale, "The sale pays " + price + " credits and reputation");
            yield return Shot("13-sold");
            // The rest of the day on the shop's own clock (labelled accelerated), every queued visitor served.
            for (int i = 0; i < 2000 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(0.5f);
                if (i % 40 == 0) yield return null;
            }
            yield return null;
            Check(shop.Phase == GrowthPhase.Night && shop.Day == 2 && shop.LastDay != null && shop.LastDay.Sales >= 1, "The day ends after 5:30 with its summary: " + (shop.LastDay?.Sales ?? 0) + " sales");
            Check(s.Card == GrowthCard.Receipt, "The night receipt opens");
            yield return Shot("14-receipt");
            s.SetCard(GrowthCard.None);

            // A second tank on an empty place, bought at night (a labelled credits fixture if the day earned too little).
            if (shop.Credits < 20) { shop.Credits = 20; limitations.Add("Credits topped up to 20 for the second tank (fixture)"); }
            var a2 = GrowthWorld.Places["a2"];
            LookAt(a2.Browse + new Vector3(0, 0, -0.4f), a2.Position + new Vector3(0, 0.4f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Place, "a2", "The reticle focuses the empty place a2");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Place, "E opens the place card");
            yield return Shot("15-place-card");
            Ok(s.Act(() => shop.BuyTank("a2")), "Buy a Starter tank for a2");
            s.SetCard(GrowthCard.None);
            yield return null;
            Check(shop.ShopContainers.Count() == 2 && s.World.Tanks.Count == 2, "Two tanks in the shop and the world");

            // Era 2: a labelled save fixture gives 2 stars and the credits for unit B; the three founders are found.
            Produce(1, 2);
            Check(shop.Discovered.Count == 3, "Bloom 1 + Flow 2 found the Ribbonfin: three creatures discovered");
            Ok(s.Act(() => shop.PutIn(shop.ShopContainers.Last().Id)), "Stock the second tank");
            Check(s.SaveNow(), "Save before the fixture");
            var save = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            save.reputation = GrowthContent.StarThresholds[1]; save.credits = 500;
            File.WriteAllText(s.SavePath, JsonUtility.ToJson(save));
            Check(s.ReloadSaved() && s.Shop.Stars == 2, "Load the labelled two-star, 500-credit fixture");
            shop = s.Shop;
            Ok(s.Act(() => shop.Upgrade("siphon")), "Upgrade the siphon at the tool bench");
            Check(shop.SiphonCapacity == 6 && shop.Era == 2, "The level 2 siphon holds 6, and era 2 is open (2 stars, 3 creatures, 2 tanks)");
            s.SetCard(GrowthCard.Workbench); yield return Shot("16-workbench"); s.SetCard(GrowthCard.None);
            LookAt(new Vector3(2.4f, 0, 0), GrowthWorld.UnitBWallPosition + new Vector3(0, 1.4f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.UnitBWall, null, "The reticle focuses unit B's wall");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.UnitB, "E opens unit B's card");
            yield return Shot("17-unit-b-card");
            Ok(s.Act(shop.BuyUnitB), "Open shop unit B");
            s.SetCard(GrowthCard.None);
            yield return null;
            Check(shop.UnitB && !s.World.UnitBWallStanding && shop.FreePlaces.Count(p => p.StartsWith("b")) == 4 && shop.VisitorsAtOnce == 3, "The wall opens: unit B's four places and a third visitor at a time");
            LookAt(new Vector3(1.0f, 0, -0.6f), new Vector3(8.0f, 1.0f, 0.5f)); yield return Shot("18-unit-b-open");
            LookAt(new Vector3(9.5f, 0, -2.6f), new Vector3(4.0f, 1.2f, 1.0f)); yield return Shot("19-unit-b-inside");
            s.SetCard(GrowthCard.Board); yield return Shot("20-pinboard"); s.SetCard(GrowthCard.None);
            s.SetCard(GrowthCard.Journal); yield return Shot("21-journal"); s.SetCard(GrowthCard.None);

            // Save and reload: the same shop comes back.
            Check(s.SaveNow(), "Save");
            string before = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            Check(JsonUtility.ToJson(s.Shop.Snapshot()) == before, "The reloaded shop is the saved one");
            Check(File.Exists(s.SavePath) && JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath)).version == GrowthSave.CurrentVersion, "The greybox save is its own version-" + GrowthSave.CurrentVersion + " file");
            s.SetCard(GrowthCard.Pause); yield return Shot("22-pause"); s.SetCard(GrowthCard.None);
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
