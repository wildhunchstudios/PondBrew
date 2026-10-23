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
            BreedingSmokeArgument = "-evolutionGrowthBreedingSmoke";

        /// <summary>Which greybox route was asked for: "growth" (GR-02), "tanks" or "density" (GR-03), "cultures" (GR-04), "breeding" (GR-05), or null.</summary>
        public static string SmokeRoute =>
            ShopSession.Arg(SmokeArgument) != null ? "growth" : ShopSession.Arg(TanksSmokeArgument) != null ? "tanks" : ShopSession.Arg(DensitySmokeArgument) != null ? "density" :
            ShopSession.Arg(CulturesSmokeArgument) != null ? "cultures" : ShopSession.Arg(BreedingSmokeArgument) != null ? "breeding" : null;
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

    public enum GrowthCard { None, Welcome, Grower, Incubator, Tank, Place, Counter, Workbench, UnitB, Board, Journal, Receipt, Pause, Crate, Catalog, Breeding }

    /// <summary>
    /// GR-02: the greybox of the redesign's first era. Owns the GrowthShop (Core rules), the player, the greybox rooms
    /// (GrowthWorld), the UI (GrowthUi) and its own save file, separate from the classic slot (GR.15: a fresh save line).
    /// Controls: WASD and mouse, hold the left mouse on a grower to collect and on the counter to bag a sale, E to open a
    /// station or take the creature under the reticle in peek, Tab for the journal, Esc to close or pause.
    /// </summary>
    public sealed class GrowthSession : MonoBehaviour
    {
        public const string SaveFileName = "growth-gr02.json";
        public const float InteractRange = 3.2f, PeekRange = 1.6f, NormalFov = 68f, PeekFov = 45f, PeekEase = 0.25f;
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
        public string Notice { get; private set; }
        public bool PeekOn = true, ReducedMotion;
        public float Sensitivity = 1;
        /// <summary>The held-mouse actions, for the HUD's progress: collecting at a grower, bagging at the counter.</summary>
        public bool CollectingNow { get; private set; }
        public bool ServingNow { get; private set; }
        public bool Paused => Card == GrowthCard.Pause || Card == GrowthCard.Welcome || Card == GrowthCard.Receipt || Card == GrowthCard.Journal;
        float peekBlend, saveClock, visualClock;
        bool initialized;

        void Awake()
        {
            Application.targetFrameRate = 120;
            ValidationMode = ShopSession.Arg("-evolutionSmoke") != null;
            SavePath = ValidationMode ? Path.Combine(ShopSession.Arg("-evolutionEvidence") ?? Application.temporaryCachePath, SaveFileName)
                : Path.Combine(Application.persistentDataPath, SaveFileName);
            Shop = LoadOrNew(SavePath, out string notice);
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
            Shop = new GrowthShop();
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
            }
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
                if (Card == GrowthCard.None) SetCard(GrowthCard.Pause); else SetCard(GrowthCard.None);
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
            UpdatePeek(dt);
            bool held = Card == GrowthCard.None && mouse != null && mouse.leftButton.isPressed && (!ValidationMode || AllowValidationInput);
            HoldActions(held, dt);
            if (Card == GrowthCard.None && keyboard != null && keyboard.eKey.wasPressedThisFrame && (!ValidationMode || AllowValidationInput))
            {
                if (Peeking && PeekCreature != null) TakeFromTank(PeekCreature);
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
            if (Shop.Parents.Count < GrowthShop.PairSize && Shop.Carried.Any(c => Shop.ParentCheck(c, out _) == GrowthFailure.None)) { Act(() => Shop.PutParent()); return true; }
            if (Shop.Offspring != null && Shop.Carried.Count < GrowthShop.CarryLimit) { Act(Shop.TakeOffspring); return true; }
            return false;
        }

        /// <summary>A creature's name with its colour when it is not Classic ("Sunny Pondlet").</summary>
        public static string Named(GrowthSpecimen s) => (s.Colour == Colour.Classic ? "" : s.Colour + " ") + s.Def.Name;

        void HoldActions(bool held, float dt)
        {
            CollectingNow = ServingNow = false;
            if (held && Focused != null && Focused.Kind == GrowthTargetKind.Grower)
            {
                var r = Shop.Collect(Focused.Id, dt);
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
            }
            else Shop.StopServing();
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
            if (Focused == null) return null;
            switch (Focused.Kind)
            {
                case GrowthTargetKind.Grower:
                    status = Mathf.FloorToInt(Shop.GrowerUnits.TryGetValue(Focused.Id, out float units) ? units : 0) + "/" + Shop.GrowerCapacity + " · hold mouse to collect · E details";
                    return GrowthContent.Culture(Focused.Id).Source == CultureSource.Vat ? "Conditioning vat (" + GrowthShop.Name(Focused.Id) + ")" : GrowthShop.Name(Focused.Id) + " grower";
                case GrowthTargetKind.Vial:
                    status = (Shop.Vials.TryGetValue(Focused.Id, out int left) ? left : 0) + " doses · hold mouse to collect";
                    return GrowthShop.Name(Focused.Id) + " starter vial";
                case GrowthTargetKind.Catalog:
                    status = (Shop.Phase == GrowthPhase.Night ? "E order" : "E browse · orders at night") + (Shop.Orders.Count > 0 ? " · " + Shop.Orders.Count + " on order" : "");
                    return "Supplier catalog";
                case GrowthTargetKind.Incubator:
                    status = Shop.Incubating ? Mathf.CeilToInt(Shop.IncubationLeft) + " s left" : Shop.Hatchling != null ? "A hatchling is ready · E" : Shop.Chamber.Count + "/" + Shop.MaxDoses + " doses · E to mix";
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
                                (s.ReservedBy != 0 ? " · reserved" : " · E take");
                            return s.Def.Name;
                        }
                    }
                    status = c.Used + "/" + c.Spaces + " spaces" + (Shop.Harmony(c) ? " · in harmony" : "") + (Shop.Carried.Count > 0 ? " · E put in" : " · E open");
                    return c.Def.Name;
                case GrowthTargetKind.Place:
                    var spot = GrowthShop.Place(Focused.Id);
                    status = Shop.Phase == GrowthPhase.Night ? "E buy or move a tank here" : "Tanks are placed at night";
                    return "Empty " + (spot?.Size ?? "") + " place";
                case GrowthTargetKind.Counter:
                    if (Shop.Phase == GrowthPhase.Night) { status = "E open the shop"; return "Counter"; }
                    var v = Shop.FrontOfQueue;
                    status = v == null ? "No one waiting" : "Hold mouse to bag a sale (" + Shop.Queue.Count() + " waiting)";
                    return "Counter";
                case GrowthTargetKind.Workbench: status = "E upgrade the siphon"; return "Tool bench";
                case GrowthTargetKind.UnitBWall: status = Shop.UnitB ? "Open" : "E see what it needs"; return "Shop unit B";
                case GrowthTargetKind.Board: status = "E goals"; return "Pinboard";
                case GrowthTargetKind.Breeding:
                    if (!Shop.HasBreedingTank)
                    {
                        var first = GrowthContent.Station("breeding").Levels[0];
                        status = "era " + first.Era + " · " + first.Credits + " credits and " + first.Rp + " RP · E see what it needs";
                        return "Breeding tank (not bought)";
                    }
                    bool canPut = Shop.Parents.Count < GrowthShop.PairSize && Shop.Carried.Any(c => Shop.ParentCheck(c, out _) == GrowthFailure.None);
                    status = Shop.Offspring != null ? Named(Shop.Offspring) + " in the nursery · " + (canPut ? "E put in" : Shop.Carried.Count < GrowthShop.CarryLimit ? "E take it" : "hands full") :
                        Shop.Breeding ? Mathf.CeilToInt(Shop.BreedingLeft) + " s to the offspring · E details" :
                        Shop.Parents.Count + "/" + GrowthShop.PairSize + " parents · " + (canPut ? "E put in" : "E open");
                    return "Breeding tank";
            }
            return null;
        }
    }
}
