using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-02's switch (GR.15): the grow-from-nothing greybox runs instead of the classic game only in development builds
    /// and the Editor, when asked for (-evolutionGrowth, its validation route, or the Editor menu). Release builds never see it.
    /// </summary>
    public static class GrowthGreybox
    {
        public const string Argument = "-evolutionGrowth", SmokeArgument = "-evolutionGrowthSmoke", EditorFlag = "EvolutionShop.GrowthGreybox",
            TanksSmokeArgument = "-evolutionGrowthTanksSmoke", DensitySmokeArgument = "-evolutionGrowthDensitySmoke", CulturesSmokeArgument = "-evolutionGrowthCulturesSmoke",
            BreedingSmokeArgument = "-evolutionGrowthBreedingSmoke", StationsSmokeArgument = "-evolutionGrowthStationsSmoke", PowerSmokeArgument = "-evolutionGrowthPowerSmoke",
            LineSmokeArgument = "-evolutionGrowthLineSmoke", HelpersSmokeArgument = "-evolutionGrowthHelpersSmoke", SpacesSmokeArgument = "-evolutionGrowthSpacesSmoke",
            ExhibitionSmokeArgument = "-evolutionGrowthExhibitionSmoke";

        /// <summary>Which greybox route was asked for: "growth" (GR-02), "tanks" or "density" (GR-03), "cultures" (GR-04), "breeding" (GR-05), "stations" (GR-06), "power" (GR-07), "line" (GR-08), "helpers" (GR-09), "spaces" (GR-10), "exhibition" (GR-11), or null.</summary>
        public static string SmokeRoute =>
            ShopSession.Arg(SmokeArgument) != null ? "growth" : ShopSession.Arg(TanksSmokeArgument) != null ? "tanks" : ShopSession.Arg(DensitySmokeArgument) != null ? "density" :
            ShopSession.Arg(CulturesSmokeArgument) != null ? "cultures" : ShopSession.Arg(BreedingSmokeArgument) != null ? "breeding" : ShopSession.Arg(StationsSmokeArgument) != null ? "stations" :
            ShopSession.Arg(PowerSmokeArgument) != null ? "power" : ShopSession.Arg(LineSmokeArgument) != null ? "line" : ShopSession.Arg(HelpersSmokeArgument) != null ? "helpers" : ShopSession.Arg(SpacesSmokeArgument) != null ? "spaces" : ShopSession.Arg(ExhibitionSmokeArgument) != null ? "exhibition" : null;
        public static bool Requested
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if UNITY_EDITOR
                if (UnityEditor.SessionState.GetBool(EditorFlag, false)) return true;
#endif
                return ShopSession.Arg(Argument) != null || SmokeRoute != null;
#else
                return false;
#endif
            }
        }
    }

    public enum GrowthCard { None, Welcome, Grower, Incubator, Tank, Place, Counter, Workbench, UnitB, Board, Journal, Receipt, Pause, Crate, Catalog, Breeding, Annex, Generator, PowerBoard, Buffer, Workshop, Sorter, Staff, Helper, UnitC, Hall, Results }

    /// <summary>
    /// GR-02: the greybox of the redesign's first era. Owns the GrowthShop (Core rules), the player, the greybox rooms
    /// (GrowthWorld), the UI (GrowthUi) and its own save file, separate from the classic slot (GR.15: a fresh save line).
    /// Controls: WASD and mouse, hold the left mouse on a grower to collect and on the counter to bag a sale, E to open a
    /// station or take the creature under the reticle in peek, Tab for the journal, Esc to close or pause.
    /// </summary>
    public sealed class GrowthSession : MonoBehaviour
    {
        public const string SaveFileName = "growth-gr02.json";
        public const float InteractRange = 3.2f, PeekRange = 1.6f, NormalFov = 68f, PeekFov = 45f, PeekEase = 0.25f, VacuumRange = 2.2f;
        public GrowthShop Shop { get; private set; }
        public GrowthWorld World { get; private set; }
        public ShopPlayer Player { get; private set; }
        public GrowthUi Ui { get; private set; }
        public GrowthCard Card { get; private set; }
        /// <summary>What the open card is about: a culture, a container id or a place.</summary>
        public string CardTarget { get; private set; }
        public GrowthTarget Focused { get; private set; }
        public GrowthCreatureView PeekCreature { get; private set; }
        public bool Peeking => peekBlend > 0.5f;
        public bool ValidationMode { get; private set; }
        public bool AllowValidationInput;
        public string SavePath { get; private set; }
        /// <summary>GR-07: a development-only start point for the playtest (-evolutionGrowthStart era3 or era4; GR-10's spaces; GR-11's exhibition), with its own save; null for the usual greybox.</summary>
        public string StartPoint { get; private set; }
        public string Notice { get; private set; }
        public bool PeekOn = true, ReducedMotion;
        public float Sensitivity = 1;
        /// <summary>The held-mouse actions, for the HUD's progress: collecting at a grower or a buffer, bagging at the counter,
        /// cranking a hand pump or the generator (GR-07).</summary>
        public bool CollectingNow { get; private set; }
        public bool ServingNow { get; private set; }
        public bool PumpingNow { get; private set; }
        public bool CrankingNow { get; private set; }
        /// <summary>GR-07's pipe mode: the end a pipe is being connected from, and the plan to the end under the reticle.</summary>
        public string PipeFrom { get; private set; }
        public string PipeTo { get; private set; }
        public GrowthPipe PipePlan { get; private set; }
        public string PipeWhy { get; private set; }
        public bool PipeReady { get; private set; }
        public bool Paused => Card == GrowthCard.Pause || Card == GrowthCard.Welcome || Card == GrowthCard.Receipt || Card == GrowthCard.Journal || Card == GrowthCard.Results;
        float peekBlend, saveClock, visualClock;
        bool initialized;

        void Awake()
        {
            Application.targetFrameRate = 120;
            ValidationMode = ShopSession.Arg("-evolutionSmoke") != null;
            SavePath = ValidationMode ? Path.Combine(ShopSession.Arg("-evolutionEvidence") ?? Application.temporaryCachePath, SaveFileName)
                : Path.Combine(Application.persistentDataPath, SaveFileName);
            string start = ValidationMode ? null : ShopSession.Arg("-evolutionGrowthStart");
            if (GrowthStarts.Make(start) != null)
            {
                // GR-07: a start point keeps its own save, so the usual greybox shop is never touched.
                StartPoint = start;
                SavePath = Path.Combine(Application.persistentDataPath, (start == GrowthStarts.Exhibition ? "growth-gr11-" : start == GrowthStarts.Spaces ? "growth-gr10-" : "growth-gr07-") + start + ".json");
            }
            string notice = null;
            Shop = StartPoint != null && !File.Exists(SavePath) ? GrowthStarts.Make(StartPoint) : LoadOrNew(SavePath, out notice);
            Notice = notice;
            var camera = new GameObject("Player camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = NormalFov;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.70f, 0.80f, 0.86f); // a pale daylight sky past the door and window
            camera.gameObject.AddComponent<AudioListener>();
            Player = new GameObject("Player").AddComponent<ShopPlayer>();
            Player.gameObject.layer = 2; // Ignore Raycast: the reticle's ray never meets the player's own body.
            Player.Initialize(camera);
            World = new GameObject("GR-02 greybox").AddComponent<GrowthWorld>();
            World.Build(this);
            Player.Teleport(GrowthWorld.Spawn, Quaternion.Euler(0, -90, 0));
            Ui = gameObject.AddComponent<GrowthUi>();
            Ui.Initialize(this);
            World.Sync(Shop, 0);
            initialized = true;
            SetCard(ValidationMode ? GrowthCard.None : GrowthCard.Welcome);
            Debug.Log("EVOLUTION_GROWTH_GREYBOX " + Application.dataPath + " save=" + SavePath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ValidationMode && GrowthGreybox.SmokeRoute != null) gameObject.AddComponent<GrowthSmoke>();
#endif
        }

        // ---------------------------------------------------------------- saving (its own file, never the classic slot)

        public static GrowthShop LoadOrNew(string path, out string notice)
        {
            notice = null;
            if (!File.Exists(path)) return new GrowthShop();
            string problem;
            try
            {
                var save = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(path));
                var shop = GrowthShop.Restore(save, out problem);
                if (shop != null)
                {
                    shop.TakeEvents();
                    return shop;
                }
            }
            catch (Exception e) { problem = e.Message; }
            // Never lose a save silently: the unreadable one is kept beside the new one.
            string kept = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            try { File.Move(path, kept); } catch (Exception) { kept = path; }
            notice = "The greybox save could not be read (" + problem + "), so a new shop started. The old file is kept: " + Path.GetFileName(kept);
            Debug.LogWarning(notice);
            return new GrowthShop();
        }

        public bool SaveNow()
        {
            if (Shop == null) return false;
            var save = Shop.Snapshot();
            save.peekOff = !PeekOn; save.reducedMotion = ReducedMotion; save.sensitivity = Sensitivity;
            string temp = SavePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(temp, JsonUtility.ToJson(save, true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, null); else File.Move(temp, SavePath);
                saveClock = 0;
                return true;
            }
            catch (Exception e) { Debug.LogError("GR-02 save failed: " + e.Message); return false; }
        }

        /// <summary>Starts a brand new greybox shop (the pause menu's Start over); the classic game is untouched.</summary>
        public void StartOver()
        {
            Shop = StartPoint != null ? GrowthStarts.Make(StartPoint) : new GrowthShop();
            World.ResetForNewShop();
            World.Sync(Shop, 0);
            Player.Teleport(GrowthWorld.Spawn, Quaternion.Euler(0, -90, 0));
            SaveNow();
            SetCard(GrowthCard.Welcome);
        }

        /// <summary>Validation: puts a labelled fixture shop in play and saves it (the routes say which fixtures they use).</summary>
        public void LoadFixture(GrowthShop fixture)
        {
            Shop = fixture;
            Shop.TakeEvents();
            World.ResetForNewShop();
            World.Sync(Shop, 0);
            SaveNow();
        }

        /// <summary>Reloads the saved slot (validation: a restart from the file).</summary>
        public bool ReloadSaved()
        {
            var loaded = LoadOrNew(SavePath, out string notice);
            if (notice != null) return false;
            Shop = loaded;
            World.ResetForNewShop();
            World.Sync(Shop, 0);
            return true;
        }

        void OnApplicationQuit() { if (initialized && !ValidationMode) SaveNow(); }
        void OnDestroy() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        // ---------------------------------------------------------------- cards

        public void SetCard(GrowthCard card, string target = null)
        {
            if (card == GrowthCard.None) { Shop.StopCollecting(); }
            Card = card;
            CardTarget = target;
            bool free = card == GrowthCard.None;
            Cursor.lockState = free && !ValidationMode ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !free;
            Ui?.ShowCard(card, target);
        }

        public void CloseCard()
        {
            if (Card == GrowthCard.Receipt || Card == GrowthCard.Welcome || Card == GrowthCard.Journal || Card != GrowthCard.None) SetCard(GrowthCard.None);
        }

        public void Interact(GrowthTarget target)
        {
            if (target == null) return;
            switch (target.Kind)
            {
                case GrowthTargetKind.Grower: SetCard(GrowthCard.Grower, target.Id); break;
                case GrowthTargetKind.Incubator: SetCard(GrowthCard.Incubator); break;
                case GrowthTargetKind.Tank: SetCard(GrowthCard.Tank, target.Id); break;
                case GrowthTargetKind.Place: SetCard(GrowthCard.Place, target.Id); break;
                case GrowthTargetKind.Counter: SetCard(GrowthCard.Counter); break;
                case GrowthTargetKind.Workbench: SetCard(GrowthCard.Workbench); break;
                case GrowthTargetKind.UnitBWall: SetCard(GrowthCard.UnitB); break;
                case GrowthTargetKind.Board: SetCard(GrowthCard.Board); break;
                case GrowthTargetKind.Catalog: SetCard(GrowthCard.Catalog); break;
                case GrowthTargetKind.Breeding: SetCard(GrowthCard.Breeding); break;
                case GrowthTargetKind.Vial: Ui.Hint("Hold the mouse on a vial to collect its doses"); break;
                case GrowthTargetKind.AnnexWall: SetCard(GrowthCard.Annex); break;
                case GrowthTargetKind.WorkshopWall: SetCard(GrowthCard.Workshop); break;
                case GrowthTargetKind.Sorter: SetCard(GrowthCard.Sorter); break;
                case GrowthTargetKind.StaffBoard: SetCard(GrowthCard.Staff); break;
                case GrowthTargetKind.UnitCWall: SetCard(GrowthCard.UnitC); break;
                case GrowthTargetKind.HallBoard: SetCard(GrowthCard.Hall); break;
                case GrowthTargetKind.Award: SetCard(GrowthCard.Results); break;
                case GrowthTargetKind.Helper: SetCard(GrowthCard.Helper, target.Id); break;
                case GrowthTargetKind.Generator: SetCard(GrowthCard.Generator); break;
                case GrowthTargetKind.PowerBoard: SetCard(GrowthCard.PowerBoard); break;
                case GrowthTargetKind.Buffer: SetCard(GrowthCard.Buffer, target.Id); break;
                case GrowthTargetKind.Pump: SetCard(GrowthCard.Grower, target.Id); break;
            }
        }

        // ---------------------------------------------------------------- pipes (GR-07)

        /// <summary>The pipe end a target stands for: a grower (or its pump) is its tap, a buffer its place, the incubator its port
        /// (its outlet for the chute, GR-08), the sorter its hopper.</summary>
        public static string EndOf(GrowthTarget t)
        {
            if (t == null) return null;
            switch (t.Kind)
            {
                case GrowthTargetKind.Grower: case GrowthTargetKind.Pump: return GrowthLab.Tap(t.Id);
                case GrowthTargetKind.Buffer: return GrowthLab.Buffer(int.Parse(t.Id));
                case GrowthTargetKind.Incubator: return GrowthLab.Port;
                case GrowthTargetKind.Sorter: return GrowthLab.Sorter;
                default: return null;
            }
        }

        /// <summary>Starts connecting a pipe from an end (a card's button): the card closes; aim at the other end and press E.</summary>
        public void StartPipe(string end)
        {
            PipeFrom = end; PipeTo = null; PipePlan = null; PipeWhy = null; PipeReady = false;
            SetCard(GrowthCard.None);
            Ui?.Toast("Pipe from " + Shop.EndName(end) + ": aim at its other end and press E. Esc stops.");
        }

        public void StopPipe()
        {
            PipeFrom = PipeTo = null; PipePlan = null; PipeWhy = null; PipeReady = false;
            World.HidePipeGhost();
        }

        /// <summary>The preview of the pipe to the end under the reticle: its route, length and cost, or why it cannot be laid.</summary>
        void UpdatePipe()
        {
            if (PipeFrom == null) return;
            string to = EndOf(Focused);
            if (to == GrowthLab.Port && PipeFrom == GrowthLab.Sorter) to = GrowthLab.Out;
            if (to == PipeFrom) to = null;
            if (to == PipeTo && (PipePlan != null || PipeWhy != null)) { World.ShowPipeGhost(PipePlan, PipeReady, Shop); return; }
            PipeTo = to; PipePlan = null; PipeWhy = null; PipeReady = false;
            if (to != null)
            {
                var failure = Shop.PipeCheck(PipeFrom, to, out var plan, out string why);
                PipePlan = plan; PipeWhy = why; PipeReady = failure == GrowthFailure.None;
            }
            World.ShowPipeGhost(PipePlan, PipeReady, Shop);
        }

        void LayAimedPipe()
        {
            if (PipeTo == null) { Ui.Hint("Aim at the pipe's other end: a buffer, a grower, the incubator or the sorter"); return; }
            string from = PipeFrom, to = PipeTo;
            if (Act(() => Shop.LayPipe(from, to)).Success) StopPipe();
            else { PipeTo = null; }
        }

        /// <summary>Runs a card action, saves when it changed something, and says why when it could not.</summary>
        public GrowthResult Act(Func<GrowthResult> action)
        {
            var result = action();
            if (result.Success) SaveNow();
            else Ui?.Toast(Explain(result), true);
            Ui?.RefreshCard();
            return result;
        }

        public static string Explain(GrowthResult r)
        {
            switch (r.Failure)
            {
                case GrowthFailure.SiphonFull: return "The siphon is full.";
                case GrowthFailure.GrowerEmpty: return "This grower is empty; it refills by itself.";
                case GrowthFailure.ChamberFull: return "The incubator takes " + r.Detail + ".";
                case GrowthFailure.ChamberEmpty: return "Put at least one dose in first.";
                case GrowthFailure.IncubatorBusy: return "The incubator is working.";
                case GrowthFailure.HatchlingWaiting: return "Take the hatchling out first.";
                case GrowthFailure.HandsFull: return "Your hands are full (" + r.Detail + ").";
                case GrowthFailure.HandsEmpty: return "You are not carrying a creature.";
                case GrowthFailure.DoesNotFit: return r.Detail.StartsWith("Too big") ? r.Detail + "." : "It does not fit: " + r.Detail + ".";
                case GrowthFailure.Reserved: return "A visitor is buying that one.";
                case GrowthFailure.NotAtNight: return "That waits for the night.";
                case GrowthFailure.NotEnoughCredits: return r.Detail + ".";
                case GrowthFailure.NoDose: return "You need " + r.Detail + " in the siphon.";
                default: return r.Detail.Length > 0 ? r.Detail + "." : r.Failure.ToString();
            }
        }

        // ---------------------------------------------------------------- the frame

        void Update()
        {
            if (!initialized) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Card == GrowthCard.None && PipeFrom != null) { StopPipe(); Ui.Hint("Pipe stopped"); }
                else if (Card == GrowthCard.None) SetCard(GrowthCard.Pause); else SetCard(GrowthCard.None);
            }
            // GR-03 (development): F8 opens the crate of every species, for looking at sizes and families.
            else if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && (Card == GrowthCard.None || Card == GrowthCard.Crate))
                SetCard(Card == GrowthCard.Crate ? GrowthCard.None : GrowthCard.Crate);
            else if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && (Card == GrowthCard.None || Card == GrowthCard.Journal))
                SetCard(Card == GrowthCard.Journal ? GrowthCard.None : GrowthCard.Journal);
            Player.Sensitivity = Sensitivity;
            Player.InputEnabled = Card == GrowthCard.None && (!ValidationMode || AllowValidationInput);
            float dt = Time.deltaTime;
            if (!Paused)
            {
                Advance(dt);
                saveClock += dt;
                if (saveClock >= 5) SaveNow();
            }
            visualClock += dt * (ReducedMotion ? 0.5f : 1f);
            World.Sync(Shop, dt);
            World.Animate(visualClock, dt);
            FindFocus();
            UpdatePipe();
            UpdatePeek(dt);
            bool held = Card == GrowthCard.None && mouse != null && mouse.leftButton.isPressed && (!ValidationMode || AllowValidationInput);
            HoldActions(held, dt);
            if (Card == GrowthCard.None && keyboard != null && keyboard.eKey.wasPressedThisFrame && (!ValidationMode || AllowValidationInput))
            {
                if (PipeFrom != null) LayAimedPipe();
                else if (Peeking && PeekCreature != null) TakeFromTank(PeekCreature);
                // Carrying creatures, E on a tank puts one in; otherwise E opens what is under the reticle.
                else if (Focused != null && Focused.Kind == GrowthTargetKind.Tank && Shop.Carried.Count > 0) { string id = Focused.Id; Act(() => Shop.PutIn(id)); }
                else if (Focused != null && Focused.Kind == GrowthTargetKind.Breeding && BreedingShortcut()) { }
                else if (Focused != null) Interact(Focused);
            }
            Ui.Tick(dt);
        }

        /// <summary>Advances the shop and turns its events into the UI's notes; the day's end opens the receipt.</summary>
        public void Advance(float seconds)
        {
            var phase = Shop.Phase;
            Shop.Tick(seconds);
            foreach (var e in Shop.TakeEvents())
            {
                Ui?.Event(e);
                // GR-11: the Grand Exhibition held: confetti around the player.
                if (e.Kind == GrowthEventKind.ExhibitionWon) World.Celebrate(Player != null ? Player.transform.position : new Vector3(-0.5f, 0, -1.6f), Player != null && Player.View != null ? Player.View.transform.forward : Vector3.forward);
                if (e.Kind == GrowthEventKind.DayEnded) { SaveNow(); if (Card == GrowthCard.None || Card == GrowthCard.Counter) SetCard(GrowthCard.Receipt); }
                if (e.Kind == GrowthEventKind.Discovery || e.Kind == GrowthEventKind.NewColour || e.Kind == GrowthEventKind.EraUp || e.Kind == GrowthEventKind.StarUp || e.Kind == GrowthEventKind.CultureOwned) SaveNow();
            }
            if (phase != Shop.Phase) World.SetPhase(Shop.Phase);
        }

        /// <summary>
        /// GR-05: E at the breeding tank puts a carried creature in while the pair has room, else takes a waiting offspring
        /// with a free hand; otherwise (nothing to put in or take) it opens the card. False when the card should open.
        /// </summary>
        bool BreedingShortcut()
        {
            if (!Shop.HasBreedingTank) return false;
            if (Shop.ActivePairs.Any(p => p.Parents.Count < GrowthShop.PairSize) && Shop.Carried.Any(c => Shop.ParentCheck(c, out _) == GrowthFailure.None)) { Act(() => Shop.PutParent()); return true; }
            if (Shop.Offspring != null && Shop.Carried.Count < GrowthShop.CarryLimit) { Act(() => Shop.TakeOffspring()); return true; }
            return false;
        }

        /// <summary>A creature's name with its colour when it is not Classic ("Sunny Pondlet").</summary>
        public static string Named(GrowthSpecimen s) => (s.Colour == Colour.Classic ? "" : s.Colour + " ") + s.Def.Name;

        void HoldActions(bool held, float dt)
        {
            CollectingNow = ServingNow = false;
            if (held && Focused != null && Focused.Kind == GrowthTargetKind.Grower)
            {
                // GR-06: the vacuum (siphon level 4) also pulls from the growers within reach.
                var r = Shop.Collect(Focused.Id, dt, Shop.Vacuum ? World.SourcesNear(Player.transform.position, VacuumRange) : null);
                CollectingNow = r.Success;
                if (!r.Success && (r.Failure == GrowthFailure.SiphonFull || r.Failure == GrowthFailure.GrowerEmpty)) Ui.Hint(Explain(r));
            }
            else Shop.StopCollecting();
            // GR-04: a starter vial is collected as a grower is, until it is empty.
            if (held && Focused != null && Focused.Kind == GrowthTargetKind.Vial)
            {
                var r = Shop.CollectVial(Focused.Id, dt);
                CollectingNow = r.Success;
                if (!r.Success && (r.Failure == GrowthFailure.SiphonFull || r.Failure == GrowthFailure.NoDose)) Ui.Hint(Explain(r));
            }
            else Shop.StopVial();
            if (held && Focused != null && Focused.Kind == GrowthTargetKind.Counter && Shop.Phase == GrowthPhase.Day)
            {
                var r = Shop.Serve(dt);
                ServingNow = r.Success;
                // GR-09: with Pip at the till and no second till, the counter is Pip's.
                if (!r.Success && r.Failure == GrowthFailure.Busy) Ui.Hint(Explain(r));
            }
            else Shop.StopServing();
            // GR-07: cranking a grower's hand pump, the generator's hand crank, and siphoning from a buffer.
            PumpingNow = CrankingNow = false;
            if (held && PipeFrom == null && Focused != null && Focused.Kind == GrowthTargetKind.Pump)
            {
                var r = Shop.CrankPump(Focused.Id, dt);
                PumpingNow = r.Success;
                if (!r.Success) Ui.Hint(Explain(r));
            }
            else Shop.StopPumping();
            if (held && PipeFrom == null && Focused != null && Focused.Kind == GrowthTargetKind.Generator)
            {
                var r = Shop.CrankGenerator();
                CrankingNow = r.Success;
                if (!r.Success) Ui.Hint(Explain(r));
            }
            if (held && PipeFrom == null && Focused != null && Focused.Kind == GrowthTargetKind.Buffer)
            {
                var r = Shop.CollectBuffer(int.Parse(Focused.Id), dt);
                CollectingNow = r.Success;
                if (!r.Success && (r.Failure == GrowthFailure.SiphonFull || r.Failure == GrowthFailure.GrowerEmpty)) Ui.Hint(Explain(r));
            }
            else Shop.StopBuffer();
        }

        void FindFocus()
        {
            Focused = null;
            if (Card != GrowthCard.None) return;
            var view = Player.View.transform;
            if (Physics.Raycast(view.position, view.forward, out var hit, InteractRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                Focused = hit.collider.GetComponentInParent<GrowthTarget>();
        }

        /// <summary>GR.5 peek: looking at a container from close by eases the view in, and the creature under the reticle can be read and picked.</summary>
        void UpdatePeek(float dt)
        {
            bool want = PeekOn && Card == GrowthCard.None && Focused != null && Focused.Kind == GrowthTargetKind.Tank &&
                World.DistanceToGlass(Focused.Id, Player.View.transform.position) <= PeekRange;
            float step = ReducedMotion ? 1 : dt / PeekEase;
            peekBlend = Mathf.MoveTowards(peekBlend, want ? 1 : 0, step);
            Player.View.fieldOfView = Mathf.Lerp(NormalFov, PeekFov, Mathf.SmoothStep(0, 1, peekBlend));
            // While peek eases out after the reticle left the tank there is nothing to read (GR-03 found this throwing).
            PeekCreature = Peeking && want ? World.CreatureNearCentre(Focused.Id, Player.View) : null;
            World.Highlight(PeekCreature);
        }

        public void TakeFromTank(GrowthCreatureView creature)
        {
            var holder = Shop.HolderOf(creature.SpecimenId);
            if (holder == null) return;
            Act(() => Shop.TakeOut(holder.Id, creature.SpecimenId));
        }

        // ---------------------------------------------------------------- what the HUD says about the focus

        public string FocusName(out string status)
        {
            status = null;
            if (PipeFrom != null)
            {
                // GR-07's pipe mode: the preview of the pipe to the end under the reticle.
                status = PipeTo == null ? "aim at its other end and press E · Esc stops" :
                    PipePlan == null ? PipeWhy + " · Esc stops" :
                    "to " + Shop.EndName(PipeTo) + ": " + PipePlan.Length.ToString("0.#") + " m, " + PipePlan.Paid + " credits · " + (PipeReady ? "E lay it" : PipeWhy) + " · Esc stops";
                return "Pipe from " + Shop.EndName(PipeFrom);
            }
            if (Focused == null) return null;
            switch (Focused.Kind)
            {
                case GrowthTargetKind.AnnexWall: status = Shop.Annex ? "Open" : "E see what it needs"; return "Lab annex";
                case GrowthTargetKind.WorkshopWall: status = Shop.Workshop ? "Open" : "E see what it needs"; return "Workshop";
                case GrowthTargetKind.UnitCWall: status = Shop.UnitC ? "Open" : "E see what it needs"; return "Shop unit C";
                case GrowthTargetKind.HallBoard:
                    status = !Shop.Hall ? "E see what it needs" : Shop.ExhibitionDone ? "Held · E the results" : Shop.ExhibitionToday ? "Today · E the guests" : Shop.ExhibitionPrepared ? "Tomorrow · E details" : "E the Grand Exhibition";
                    return Shop.Hall ? "Exhibition hall" : "Exhibition hall plans";
                case GrowthTargetKind.Award:
                    status = "Held on day " + (Shop.ExhibitionResults?.Day ?? 0) + " · E the results";
                    return "The Grand Exhibition award";
                case GrowthTargetKind.StaffBoard:
                    status = Shop.Staff.Count == 0 ? "E see who could work here" : Shop.Staff.Count + " hired · " + Shop.WageBill + " credits a night and day · E open";
                    return "Staff board";
                case GrowthTargetKind.Helper:
                    var helper = Shop.Helper(Focused.Id);
                    if (helper == null) return null;
                    status = helper.Status + " · E card";
                    return helper.Def.Name + ", " + helper.Def.Job + " (level " + helper.Level + ")";
                case GrowthTargetKind.Sorter:
                    if (!Shop.HasSorter) { var s1 = GrowthContent.Station("sorter").Levels[0]; status = "era " + s1.Era + " · " + s1.Credits + " credits · E see what it needs"; return "Sorter (not bought)"; }
                    status = Shop.SorterStatus + " · E stock targets";
                    return "Sorter";
                case GrowthTargetKind.Generator:
                    if (!Shop.HasGenerator) { var g1 = GrowthContent.Station("generator").Levels[0]; status = "era " + g1.Era + " · " + g1.Credits + " credits and " + g1.Rp + " RP · E see what it needs"; return "Generator (not bought)"; }
                    status = Shop.HandCrank ? (CrankingNow ? "cranking: " + Shop.GeneratorUnits.ToString("0") + " unit" : "hold mouse to crank · E details") : Shop.GeneratorUnits.ToString("0") + " units · E details";
                    return "Generator";
                case GrowthTargetKind.PowerBoard:
                    status = Shop.Supply.ToString("0.#") + " units supplied, " + Shop.Draw.ToString("0.#") + " asked for · E open";
                    return "Power board";
                case GrowthTargetKind.Buffer:
                    var buffer = Shop.BufferAt(int.Parse(Focused.Id));
                    if (buffer == null) { status = Shop.Phase == GrowthPhase.Night ? "E place a buffer (" + GrowthContent.BufferCredits + " credits)" : "Buffers are placed at night"; return "Buffer place " + (int.Parse(Focused.Id) + 1); }
                    status = Mathf.FloorToInt(buffer.Units) + "/" + GrowthContent.BufferCapacity + (buffer.Culture == null ? " · empty" : " " + GrowthShop.Name(buffer.Culture)) + " · hold mouse to collect · E details";
                    return Shop.BufferName(buffer.Place);
                case GrowthTargetKind.Pump:
                    var line = Shop.Lines.FirstOrDefault(p => p.Culture == Focused.Id);
                    string lineStatus = line == null ? "" : Shop.LineStatus(line);
                    status = (Shop.HandPumps && lineStatus.StartsWith("Hold") ? "hold mouse to crank" : lineStatus) + " · E details";
                    return GrowthShop.Name(Focused.Id) + (Shop.HandPumps ? " hand pump" : " pump");
                case GrowthTargetKind.Grower:
                    status = Mathf.FloorToInt(Shop.GrowerUnits.TryGetValue(Focused.Id, out float units) ? units : 0) + "/" + Shop.GrowerCapacityOf(Focused.Id) + " · hold mouse to collect · E details";
                    return GrowthContent.Culture(Focused.Id).Source == CultureSource.Vat ? "Conditioning vat (" + GrowthShop.Name(Focused.Id) + ")" : GrowthShop.Name(Focused.Id) + " grower";
                case GrowthTargetKind.Vial:
                    status = (Shop.Vials.TryGetValue(Focused.Id, out int left) ? left : 0) + " doses · hold mouse to collect";
                    return GrowthShop.Name(Focused.Id) + " starter vial";
                case GrowthTargetKind.Catalog:
                    status = (Shop.Phase == GrowthPhase.Night ? "E order" : "E browse · orders at night") + (Shop.Orders.Count > 0 ? " · " + Shop.Orders.Count + " on order" : "");
                    return "Supplier catalog";
                case GrowthTargetKind.Incubator:
                    status = Shop.Incubating ? Mathf.CeilToInt(Shop.IncubationLeft) + " s left" : Shop.Hatchlings.Count > 1 ? "A clutch of " + Shop.Hatchlings.Count + " is ready · E" : Shop.Hatchling != null ? "A hatchling is ready · E" : Shop.Chamber.Count + "/" + Shop.MaxDoses + " doses · E to mix";
                    return "Incubator";
                case GrowthTargetKind.Tank:
                    var c = Shop.Container(Focused.Id);
                    if (c == null) return null;
                    if (PeekCreature != null)
                    {
                        var s = c.Occupants.FirstOrDefault(o => o.Id == PeekCreature.SpecimenId);
                        if (s != null)
                        {
                            status = s.Def.Size.ToString().ToLowerInvariant() + " · " + s.Colour + " · " + Shop.Price(s.Species) + " cr" + (c.InSchool(s.Species) ? " · school" : "") +
                                (Shop.Era >= GrowthContent.ComfortEra ? " · " + s.Def.Temperament.ToString().ToLowerInvariant() + ", " + s.Def.Water.ToString().ToLowerInvariant() : "") +
                                (GrowthShop.IsOnDisplay(s) ? " · on display" : s.ReservedBy != 0 ? " · reserved" : " · E take");
                            return s.Def.Name;
                        }
                    }
                    status = c.Used + "/" + c.Spaces + " spaces" + (Shop.Harmony(c) ? " · in harmony" : "") + (Shop.Carried.Count > 0 ? " · E put in" : " · E open");
                    return c.Def.Name;
                case GrowthTargetKind.Place:
                    if (GrowthShop.LabPlace(Focused.Id) != null) { status = Shop.Phase == GrowthPhase.Night ? "E place a lab rack" : "Racks are placed at night"; return "Rack place " + Focused.Id.Substring(1); }
                    var spot = GrowthShop.Place(Focused.Id);
                    status = Shop.Phase == GrowthPhase.Night ? "E buy or move a tank here" : "Tanks are placed at night";
                    return "Empty " + (spot?.Size ?? "") + " place";
                case GrowthTargetKind.Counter:
                    if (Shop.Phase == GrowthPhase.Night) { status = "E open the shop"; return "Counter"; }
                    var v = Shop.FrontOfQueue;
                    bool pip = Shop.Working("pip");
                    status = pip && !Shop.SecondTill ? "Pip is at the till (" + Shop.Queue.Count() + " waiting) · E details" :
                        v == null ? "No one waiting" : "Hold mouse to bag a sale" + (pip ? " beside Pip" : "") + " (" + Shop.Queue.Count() + " waiting)";
                    return "Counter";
                case GrowthTargetKind.Workbench: status = "level " + Shop.Level("bench") + " · E the siphon and the bench"; return "Research bench";
                case GrowthTargetKind.UnitBWall: status = Shop.UnitB ? "Open" : "E see what it needs"; return "Shop unit B";
                case GrowthTargetKind.Board: status = "E goals"; return "Pinboard";
                case GrowthTargetKind.Breeding:
                    if (!Shop.HasBreedingTank)
                    {
                        var first = GrowthContent.Station("breeding").Levels[0];
                        status = "era " + first.Era + " · " + first.Credits + " credits and " + first.Rp + " RP · E see what it needs";
                        return "Breeding tank (not bought)";
                    }
                    bool canPut = Shop.ActivePairs.Any(p => p.Parents.Count < GrowthShop.PairSize) && Shop.Carried.Any(c => Shop.ParentCheck(c, out _) == GrowthFailure.None);
                    // What E does here first: put a creature in, then take an offspring, else open the card.
                    var waiting = Shop.Pairs.FirstOrDefault(p => p.Offspring != null);
                    status = canPut ? Shop.AllParents.Count() + "/" + GrowthShop.PairSize * Shop.PairPlaces + " parents · E put in" :
                        waiting != null ? Named(waiting.Offspring) + " in the nursery · " + (Shop.Carried.Count < GrowthShop.CarryLimit ? "E take it" : "hands full") :
                        Shop.Breeding ? Mathf.CeilToInt(Shop.BreedingLeft) + " s to the offspring · E details" :
                        Shop.AllParents.Count() + "/" + GrowthShop.PairSize * Shop.PairPlaces + " parents · E open";
                    return "Breeding tank";
            }
            return null;
        }
    }
}
