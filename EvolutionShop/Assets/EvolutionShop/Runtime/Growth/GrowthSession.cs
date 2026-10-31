using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// The game's command-line flags: the validation routes' (-evolutionGrowth...Smoke) and the development start points'
    /// (-evolutionGrowthStart). Since GR-17 the game is the only game: the classic game and its switch are gone.
    /// </summary>
    public static class GrowthGreybox
    {
        public const string Argument = "-evolutionGrowth", SmokeArgument = "-evolutionGrowthSmoke",
            TanksSmokeArgument = "-evolutionGrowthTanksSmoke", DensitySmokeArgument = "-evolutionGrowthDensitySmoke", CulturesSmokeArgument = "-evolutionGrowthCulturesSmoke",
            BreedingSmokeArgument = "-evolutionGrowthBreedingSmoke", StationsSmokeArgument = "-evolutionGrowthStationsSmoke", PowerSmokeArgument = "-evolutionGrowthPowerSmoke",
            LineSmokeArgument = "-evolutionGrowthLineSmoke", HelpersSmokeArgument = "-evolutionGrowthHelpersSmoke", SpacesSmokeArgument = "-evolutionGrowthSpacesSmoke",
            ExhibitionSmokeArgument = "-evolutionGrowthExhibitionSmoke", CreaturesSmokeArgument = "-evolutionGrowthCreaturesSmoke",
            GuideSmokeArgument = "-evolutionGrowthGuideSmoke", LookSmokeArgument = "-evolutionGrowthLookSmoke", PeopleSmokeArgument = "-evolutionGrowthPeopleSmoke",
            DecorSmokeArgument = "-evolutionGrowthDecorSmoke", WishesSmokeArgument = "-evolutionGrowthWishesSmoke", SettingsSmokeArgument = "-evolutionGrowthSettingsSmoke", EffectsSmokeArgument = "-evolutionGrowthEffectsSmoke", LessonsSmokeArgument = "-evolutionGrowthLessonsSmoke", MenuSmokeArgument = "-evolutionGrowthMenuSmoke", DeadEndsSmokeArgument = "-evolutionGrowthDeadEndsSmoke", FloorSmokeArgument = "-evolutionGrowthFloorSmoke", WordsSmokeArgument = "-evolutionGrowthWordsSmoke";

        /// <summary>Which greybox route was asked for: "growth" (GR-02), "tanks" or "density" (GR-03), "cultures" (GR-04), "breeding" (GR-05), "stations" (GR-06), "power" (GR-07), "line" (GR-08), "helpers" (GR-09), "spaces" (GR-10), "exhibition" (GR-11), "creatures" (GR-12), "guide" (GR-14), "look" (GR-16), "people" (GR-18), "decor" (GR-19 and GR-22), "wishes" (GR-20), "settings" (GR-21 and GR-23), "effects" (GR-24 and GR-25), "lessons" (R09.4, R10.3 and GR-26), "menu" (GR-27), "deadends" (GR-29), or null.</summary>
        public static string SmokeRoute =>
            ShopSession.Arg(SmokeArgument) != null ? "growth" : ShopSession.Arg(TanksSmokeArgument) != null ? "tanks" : ShopSession.Arg(DensitySmokeArgument) != null ? "density" :
            ShopSession.Arg(CulturesSmokeArgument) != null ? "cultures" : ShopSession.Arg(BreedingSmokeArgument) != null ? "breeding" : ShopSession.Arg(StationsSmokeArgument) != null ? "stations" :
            ShopSession.Arg(PowerSmokeArgument) != null ? "power" : ShopSession.Arg(LineSmokeArgument) != null ? "line" : ShopSession.Arg(HelpersSmokeArgument) != null ? "helpers" : ShopSession.Arg(SpacesSmokeArgument) != null ? "spaces" : ShopSession.Arg(ExhibitionSmokeArgument) != null ? "exhibition" : ShopSession.Arg(CreaturesSmokeArgument) != null ? "creatures" : ShopSession.Arg(GuideSmokeArgument) != null ? "guide" : ShopSession.Arg(LookSmokeArgument) != null ? "look" : ShopSession.Arg(PeopleSmokeArgument) != null ? "people" : ShopSession.Arg(DecorSmokeArgument) != null ? "decor" : ShopSession.Arg(WishesSmokeArgument) != null ? "wishes" : ShopSession.Arg(SettingsSmokeArgument) != null ? "settings" : ShopSession.Arg(EffectsSmokeArgument) != null ? "effects" : ShopSession.Arg(LessonsSmokeArgument) != null ? "lessons" : ShopSession.Arg(MenuSmokeArgument) != null ? "menu" : ShopSession.Arg(DeadEndsSmokeArgument) != null ? "deadends" : ShopSession.Arg(FloorSmokeArgument) != null ? "floor" : ShopSession.Arg(WordsSmokeArgument) != null ? "words" : null;
    }

    public enum GrowthCard { None, Welcome, Grower, Incubator, Tank, Place, Counter, Workbench, UnitB, Board, Journal, Receipt, Pause, Crate, Catalog, Breeding, Annex, Generator, PowerBoard, Buffer, Workshop, Sorter, Staff, Helper, UnitC, Hall, Results, Settings, Lesson, Menu }

    /// <summary>
    /// GR-02: the greybox of the redesign's first era. Owns the GrowthShop (Core rules), the player, the greybox rooms
    /// (GrowthWorld), the UI (GrowthUi) and its own save file, separate from the classic slot (GR.15: a fresh save line).
    /// Controls (GR-29: click acts, E opens): WASD and mouse, click to put in or take, hold the left mouse on a grower to collect and on the counter to bag a sale, E to open a
    /// station or take the creature under the reticle in peek, Tab for the journal, Esc to close or pause.
    /// </summary>
    public sealed class GrowthSession : MonoBehaviour
    {
        public const string SaveFileName = "growth-gr02.json";
        /// <summary>The removed classic game's save (GR-17): left on the player's computer as it was, never read or written.</summary>
        public const string OldSaveFileName = "evolution-shop.json";
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
        /// <summary>GR-07: a development-only start point for the playtest (-evolutionGrowthStart era3 or era4; GR-10's spaces; GR-11's exhibition; GR-12's creatures; GR-16's look), with its own save; null for the usual greybox.</summary>
        public string StartPoint { get; private set; }
        public string Notice { get; private set; }
        /// <summary>GR-23: the whole game's settings (one file for the game, the user's choice), where they are kept, and why
        /// they were reset if they could not be read. The comfort settings below read and write them.</summary>
        public GrowthSettings Settings => settings;
        GrowthSettings settings = new GrowthSettings();
        public string SettingsPath { get; private set; }
        public string SettingsNotice { get; private set; }
        public bool PeekOn { get => !settings.peekOff; set => settings.peekOff = !value; }
        public bool ReducedMotion { get => settings.reducedMotion; set => settings.reducedMotion = value; }
        public float Sensitivity { get => settings.sensitivity; set => settings.sensitivity = value; }
        /// <summary>GR-16 wave 5: the sounds (the crank, the hand pump, the pop, the swish) and the machines' hum, 0 to 1 (GR-23's
        /// volumes); as levels (full, half or off) for the routes that set them so.</summary>
        public float SoundVolume => settings.sounds / 100f;
        public float HumVolume => settings.hum / 100f;
        public float HumourVolume => settings.humour / 100f;
        /// <summary>GR-21: the music's own volume, 0 to 1.</summary>
        public float MusicVolume => settings.music / 100f;
        public GrowthAudio.Level SoundLevel { get => LevelOf(settings.sounds); set => settings.sounds = GrowthSettings.LevelVolume((int)value); }
        public GrowthAudio.Level HumLevel { get => LevelOf(settings.hum); set => settings.hum = GrowthSettings.LevelVolume((int)value); }
        public GrowthAudio Audio { get; private set; }
        /// <summary>GR-21: the music.</summary>
        public GrowthMusic Music { get; private set; }
        /// <summary>GR-25: the visual effects, and where the last station the player opened stands (an upgrade sparkles there).</summary>
        public GrowthEffects Effects { get; private set; }
        public Vector3? LastStation { get; private set; }
        /// <summary>GR-18 wave 4 (§6.7): the silly moments: their sounds, how many there are (every one, fewer, or none but the
        /// player's slap), the slap's key (F unless changed: GR-23's Slap binding), and what plays them.</summary>
        public GrowthAudio.Level HumourLevel { get => LevelOf(settings.humour); set => settings.humour = GrowthSettings.LevelVolume((int)value); }
        public GrowthGagLevel GagLevel { get => (GrowthGagLevel)settings.gags; set => settings.gags = (int)value; }
        public Key SlapKey { get => GrowthControls.KeyOf(GrowthAction.Slap); set => settings.Bind(GrowthAction.Slap, "Key/" + value); }
        /// <summary>GR-23: the field of view the player chose (68 unless changed); peek eases from it to its own.</summary>
        public float FieldOfView => settings.fieldOfView;
        public GrowthGags Gags { get; private set; }
        /// <summary>GR-18 wave 3: the player's hands in first person (in the look, with the people kit), or null.</summary>
        public GrowthHands Hands { get; private set; }
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
        public bool Paused => Card == GrowthCard.Pause || Card == GrowthCard.Settings || Card == GrowthCard.Welcome || Card == GrowthCard.Receipt || Card == GrowthCard.Results || Card == GrowthCard.Lesson || Card == GrowthCard.Menu;
        /// <summary>GR-27: the main menu's view of the live shop (the player's camera lent to it while the menu shows).</summary>
        public GrowthMenuCamera MenuCamera { get; private set; }
        /// <summary>GR-27: whether there is a shop to continue (a save, or a start point); New game asks before replacing it.</summary>
        public bool HasGame { get; private set; }
        /// <summary>GR-23/GR-27: where Settings goes back to: the pause screen or the main menu.</summary>
        GrowthCard settingsFrom = GrowthCard.Pause;
        public GrowthCard SettingsFrom => settingsFrom;
        /// <summary>R09.4: lessons in a validation run, off unless a route turns them on (as the effects and the music are), so
        /// every other route's cards and captures stay as they were.</summary>
        public bool LessonsOnInValidation;
        /// <summary>R09.4: how long the shop has been free of cards (a lesson waits a moment after another card closes).</summary>
        float freeFor;
        float peekBlend, saveClock, visualClock;
        bool initialized;

        static GrowthAudio.Level LevelOf(int volume) => volume >= 75 ? GrowthAudio.Level.Full : volume >= 25 ? GrowthAudio.Level.Half : GrowthAudio.Level.Off;

        void Awake()
        {
            Application.targetFrameRate = 120;
            ValidationMode = ShopSession.Arg("-evolutionSmoke") != null;
            // GR-23: the whole game's settings, beside the saves (a validation run's in its evidence folder, from the defaults:
            // a route reads its file back itself). A first launch carries over once the settings the usual save kept.
            SettingsPath = ValidationMode ? Path.Combine(ShopSession.Arg("-evolutionEvidence") ?? Application.temporaryCachePath, GrowthSettingsStore.FileName)
                : Path.Combine(Application.persistentDataPath, GrowthSettingsStore.FileName);
            if (ValidationMode) settings = new GrowthSettings().Sanitize();
            else
            {
                bool had = File.Exists(SettingsPath);
                settings = GrowthSettingsStore.Load(SettingsPath, Path.Combine(Application.persistentDataPath, SaveFileName), out string settingsNotice);
                SettingsNotice = settingsNotice;
                if (!had || settingsNotice != null) SaveSettings();
            }
            GrowthControls.Settings = settings;
            SavePath = ValidationMode ? Path.Combine(ShopSession.Arg("-evolutionEvidence") ?? Application.temporaryCachePath, SaveFileName)
                : Path.Combine(Application.persistentDataPath, SaveFileName);
            string start = ValidationMode ? null : ShopSession.Arg("-evolutionGrowthStart");
            if (GrowthStarts.Make(start) != null)
            {
                // GR-07: a start point keeps its own save, so the usual greybox shop is never touched.
                StartPoint = start;
                SavePath = Path.Combine(Application.persistentDataPath, (start == GrowthStarts.Wishes ? "growth-gr20-" : start == GrowthStarts.Look ? "growth-gr16-" : start == GrowthStarts.Creatures ? "growth-gr12-" : start == GrowthStarts.Exhibition ? "growth-gr11-" : start == GrowthStarts.Spaces ? "growth-gr10-" : "growth-gr07-") + start + ".json");
            }
            string notice = null;
            bool fresh = !File.Exists(SavePath);
            Shop = StartPoint != null && fresh ? GrowthStarts.Make(StartPoint) : LoadOrNew(SavePath, out notice);
            // GR-14: a brand new game starts with the guided first night (never a start point, an old save or a route).
            if (fresh && StartPoint == null && !ValidationMode) Shop.Guide = GrowthGuideMode.On;
            Notice = notice;
            var camera = new GameObject("Player camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = FieldOfView;
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
            if (GrowthWorld.LookOn) Hands = GrowthHands.Build(this);
            MenuCamera = new GrowthMenuCamera(this);
            HasGame = !fresh || StartPoint != null;
            Audio = gameObject.AddComponent<GrowthAudio>();
            Audio.Initialize(this);
            Music = gameObject.AddComponent<GrowthMusic>();
            Music.Initialize(this);
            Effects = gameObject.AddComponent<GrowthEffects>();
            Effects.Initialize(this);
            Gags = gameObject.AddComponent<GrowthGags>();
            Gags.Initialize(this);
            Ui = gameObject.AddComponent<GrowthUi>();
            Ui.Initialize(this);
            World.Sync(Shop, 0);
            ApplySettings();
            if (!ValidationMode && settings.displayChosen) ApplyDisplay();
            initialized = true;
            // GR-27: the game opens on the main menu (a validation run on its route, which opens the menu itself when it wants it).
            if (ValidationMode) SetCard(GrowthCard.None); else OpenMenu();
            Debug.Log("EVOLUTION_GROWTH_GREYBOX " + Application.dataPath + " save=" + SavePath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ValidationMode && GrowthGreybox.SmokeRoute != null) gameObject.AddComponent<GrowthSmoke>();
#endif
        }

        // ---------------------------------------------------------------- saving (its own file, never the classic slot)

        /// <summary>The comfort settings in a save file, or null when it cannot be read (the shop's own load says why).</summary>
        public static GrowthSave ReadSettings(string path)
        {
            try { return JsonUtility.FromJson<GrowthSave>(File.ReadAllText(path)); }
            catch (Exception) { return null; }
        }

        // ---------------------------------------------------------------- GR-23: the settings in play

        /// <summary>Keeps the whole game's settings in their file (a validation run's in its evidence folder).</summary>
        public bool SaveSettings() => GrowthSettingsStore.Save(SettingsPath, settings);

        /// <summary>Puts other settings in play (validation: the defaults, as a new game's, or a route's own).</summary>
        public void ResetSettings(GrowthSettings to = null)
        {
            settings = (to ?? new GrowthSettings()).Sanitize(GrowthControls.RealKey);
            GrowthControls.Settings = settings;
            ApplySettings();
        }

        /// <summary>Puts the settings in play, all but the window (<see cref="ApplyDisplay"/>): the graphics, the sound and the
        /// interface's size. Every change on the Settings screen calls it.</summary>
        public void ApplySettings()
        {
            settings.Sanitize(GrowthControls.RealKey);
            ApplyGraphics();
            ApplySound();
            Ui?.SetScale(settings.uiScale / 100f);
        }

        /// <summary>The graphics: the sun's shadows, bloom, reflections, brightness and creature detail (the world's), the
        /// camera's anti-aliasing, and the render scale (a built game's: the editor's asset is left alone).</summary>
        public void ApplyGraphics()
        {
            World?.ApplyGraphics(settings);
            if (Player != null && Player.View != null)
            {
                var data = Player.View.GetUniversalAdditionalCameraData();
                data.antialiasing = settings.antiAliasing == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : settings.antiAliasing == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            RenderScaleNow = settings.renderScale / 100f;
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = RenderScaleNow;
        }

        /// <summary>Validation: the render scale last put in play.</summary>
        public float RenderScaleNow { get; private set; } = 1;

        bool focused = true;
        void OnApplicationFocus(bool focus) { focused = focus; if (initialized) ApplySound(); }

        /// <summary>The master volume, and silence while the game is in the background when the player asks for it (never in a
        /// validation run, whose window is often behind another).</summary>
        public void ApplySound() => AudioListener.volume = ListenerVolume(focused || ValidationMode);
        public float ListenerVolume(bool inFront) => settings.master / 100f * (settings.muteInBackground && !inFront ? 0 : 1);

        /// <summary>The frame rate the display settings ask for: the screen's with V-Sync (-1), else the limit (0: none, -1).</summary>
        public int TargetFrameRate => settings.vSync || settings.frameLimit == 0 ? -1 : settings.frameLimit;

        /// <summary>The window: its mode and size, V-Sync and the frame limit. A validation run records what it would do
        /// (<see cref="DisplayAsked"/>) and leaves its window as it is.</summary>
        public void ApplyDisplay()
        {
            var mode = settings.windowMode == 0 ? FullScreenMode.FullScreenWindow : settings.windowMode == 2 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
            DisplayAsked = settings.width + "x" + settings.height + " " + mode + (settings.vSync ? " vsync" : " limit " + TargetFrameRate);
            if (ValidationMode) return;
            QualitySettings.vSyncCount = settings.vSync ? 1 : 0;
            Application.targetFrameRate = TargetFrameRate;
            if (settings.displayChosen) Screen.SetResolution(settings.width, settings.height, mode);
        }

        /// <summary>Validation: what the last display change asked for ("1920x1080 Windowed limit 120").</summary>
        public string DisplayAsked { get; private set; }

        /// <summary>A change of window (its mode or size) waiting to be kept: it goes back by itself unless kept within
        /// <see cref="RevertSeconds"/>.</summary>
        public const float RevertSeconds = 10f;
        GrowthSettings displayBefore;
        float revertAt;
        public bool DisplayPending => displayBefore != null;
        public float DisplaySecondsLeft => DisplayPending ? Mathf.Max(0, revertAt - Time.unscaledTime) : 0;

        /// <summary>Changes the window (its mode or size) and asks to keep it: it goes back in ten seconds unless kept. V-Sync
        /// and the frame limit change at once, with nothing to keep.</summary>
        public void ChangeDisplay(Action<GrowthSettings> change)
        {
            if (displayBefore == null) displayBefore = settings.Copy();
            change(settings);
            settings.displayChosen = true;
            settings.Sanitize(GrowthControls.RealKey);
            ApplyDisplay();
            revertAt = Time.unscaledTime + RevertSeconds;
        }

        public void KeepDisplay() { displayBefore = null; SaveSettings(); Ui?.RefreshCard(); }

        public void RevertDisplay()
        {
            if (displayBefore == null) return;
            settings.windowMode = displayBefore.windowMode; settings.width = displayBefore.width; settings.height = displayBefore.height;
            settings.displayChosen = displayBefore.displayChosen;
            displayBefore = null;
            ApplyDisplay();
            SaveSettings();
            Ui?.RefreshCard();
        }

        /// <summary>Opens the Settings screen (from the pause screen), on the tab asked for.</summary>
        public void OpenSettings(string tab = null)
        {
            settingsFrom = Card == GrowthCard.Menu ? GrowthCard.Menu : GrowthCard.Pause;
            SetCard(GrowthCard.Settings); if (tab != null) Ui?.ShowTabNamed(tab);
        }

        /// <summary>Back to the pause screen or the main menu, whichever opened it; a display change not yet kept goes back, a
        /// rebinding waiting is called off.</summary>
        public void CloseSettings()
        {
            if (DisplayPending) RevertDisplay();
            Rebinding = null;
            SetCard(settingsFrom);
        }

        // ---------------------------------------------------------------- GR-27: the main menu

        /// <summary>The main menu over the live shop: the shop saved first (when there is one), a pipe being laid let go, the
        /// camera lent to the menu's slow view.</summary>
        public void OpenMenu()
        {
            if (PipeFrom != null) StopPipe();
            if (HasGame) SaveNow();
            MenuCamera.Begin();
            SetCard(GrowthCard.Menu);
        }

        /// <summary>Back into the shop where the player was.</summary>
        public void ContinueGame()
        {
            MenuCamera.End();
            SetCard(GrowthCard.None);
        }

        /// <summary>A new shop (its guided first night and the welcome), replacing the saved one; the menu asks first.</summary>
        public void NewGame()
        {
            MenuCamera.End();
            StartOver();
            HasGame = true;
        }

        /// <summary>Validation: as at a first launch: a fresh shop and nothing saved (the routes say when they use it).</summary>
        public void ValidationFirstLaunch()
        {
            Shop = new GrowthShop();
            HasGame = false;
            Gags?.Clear();
            World.ResetForNewShop();
            World.Sync(Shop, 0);
        }

        /// <summary>Quits the game (the shop saved first, when there is one).</summary>
        public void QuitGame()
        {
            if (HasGame) SaveNow();
            Application.Quit();
        }

        // ---------------------------------------------------------------- GR-23: rebinding

        /// <summary>The action waiting for its new key or mouse button (null when none), and what the last rebinding did.</summary>
        public GrowthAction? Rebinding { get; private set; }
        public string RebindNote { get; private set; }
        bool rebindArmed;

        /// <summary>Waits for the next key or mouse button for an action (Esc keeps it as it was). It listens once every key and
        /// button is up, so the press that started it is never taken.</summary>
        public void StartRebind(GrowthAction action) { Rebinding = action; RebindNote = null; rebindArmed = false; Ui?.RefreshCard(); }

        /// <summary>Puts an action on an input as the Controls tab does (the note says what moved) and keeps it.</summary>
        public (bool done, string note) Rebind(GrowthAction action, string input)
        {
            var r = settings.Bind(action, input);
            RebindNote = r.note;
            if (r.done) SaveSettings();
            return r;
        }

        bool UpdateRebind()
        {
            if (Rebinding == null) return false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                RebindNote = GrowthSettings.ActionName(Rebinding.Value) + " stays on " + GrowthControls.NameOf(Rebinding.Value);
                Rebinding = null; Ui?.RefreshCard(); return true;
            }
            if (!rebindArmed) { rebindArmed = !GrowthControls.AnyHeld(); return true; }
            string input = GrowthControls.Listen();
            if (input == null) return true;
            var (done, note) = Rebind(Rebinding.Value, input);
            if (done) Rebinding = null; else RebindNote = note + ": press another";
            Ui?.RefreshCard();
            return true;
        }

        /// <summary>GR-23: the use button held down, or (Toggle) pressed once to start and again to stop: it stops by itself
        /// when the reticle leaves what it was used on or a card opens.</summary>
        bool UseHeld()
        {
            bool input = Card == GrowthCard.None && (!ValidationMode || AllowValidationInput);
            if (!settings.useToggle) { useToggled = false; return input && GrowthControls.Held(GrowthAction.Use); }
            string on = Focused != null ? Focused.Kind + "/" + Focused.Id : null;
            if (!input) useToggled = false;
            else if (GrowthControls.Pressed(GrowthAction.Use)) { useToggled = !useToggled && on != null; useOn = on; }
            else if (useToggled && on != useOn) useToggled = false;
            return useToggled;
        }
        bool useToggled; string useOn;
        /// <summary>Validation: whether the use button is toggled on.</summary>
        public bool UseToggled => useToggled;

        /// <summary>GR-18 wave 4: the slap's key from a save ("" or anything unknown or already used: F).</summary>
        public static Key KeyFrom(string name) =>
            !string.IsNullOrEmpty(name) && Enum.TryParse(name, out Key key) && key != Key.None && Array.IndexOf(GrowthGags.Reserved, key) < 0 ? key : GrowthGags.DefaultSlapKey;

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
            notice = "The save could not be read (" + problem + "), so a new shop started. The old file is kept: " + Path.GetFileName(kept);
            Debug.LogWarning(notice);
            return new GrowthShop();
        }

        public bool SaveNow()
        {
            if (Shop == null) return false;
            // GR-23: the settings are the whole game's, in their own file; a save keeps only the shop.
            var save = Shop.Snapshot();
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

        /// <summary>Starts a brand new shop (the pause menu's Start over), with the guided first night (GR-14).</summary>
        public void StartOver()
        {
            Shop = StartPoint != null ? GrowthStarts.Make(StartPoint) : new GrowthShop();
            if (StartPoint == null && !ValidationMode) Shop.Guide = GrowthGuideMode.On;
            Gags?.Clear();
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
            Gags?.Clear();
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
            Gags?.Clear();
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
            // GR-23: a rebinding waits only while the Settings screen is open (never for a key pressed in the shop).
            if (card != GrowthCard.Settings) Rebinding = null;
            Card = card;
            CardTarget = target;
            // R09.4: the lessons read what has been opened: the pinboard and the journal.
            if (Shop != null && card == GrowthCard.Board) Shop.Note("board");
            if (Shop != null && card == GrowthCard.Journal) Shop.Note("journal");
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
            LastStation = target.transform.position;
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
                case GrowthFailure.SiphonFull: return "The siphon is full. A bigger one is at the research bench.";
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
            // GR-23: a display change not kept goes back by itself.
            if (DisplayPending && Time.unscaledTime >= revertAt) RevertDisplay();
            // GR-23: while an action waits for its new key, every key and button goes to it.
            bool rebinding = UpdateRebind();
            if (rebinding) { }
            else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Card == GrowthCard.None && PipeFrom != null) { StopPipe(); Ui.Hint("Pipe stopped"); }
                else if (Card == GrowthCard.None) SetCard(GrowthCard.Pause);
                else if (Card == GrowthCard.Settings) CloseSettings();
                else if (Card == GrowthCard.Menu) { } // GR-27: the main menu is where the game starts: Esc does not leave it
                else SetCard(GrowthCard.None);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // GR-03 (development): F8 opens the crate of every species, for looking at sizes and families. GR-28: development
            // builds only, so a tester's (non-development) player never has it.
            else if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && (Card == GrowthCard.None || Card == GrowthCard.Crate))
                SetCard(Card == GrowthCard.Crate ? GrowthCard.None : GrowthCard.Crate);
#endif
            else if (GrowthControls.Pressed(GrowthAction.Journal) && (Card == GrowthCard.None || Card == GrowthCard.Journal))
                SetCard(Card == GrowthCard.Journal ? GrowthCard.None : GrowthCard.Journal);
            Player.Sensitivity = Sensitivity;
            Player.InvertY = settings.invertY;
            Player.InputEnabled = Card == GrowthCard.None && (!ValidationMode || AllowValidationInput);
            float dt = Time.deltaTime;
            if (!Paused)
            {
                Advance(dt);
                saveClock += dt;
                if (saveClock >= 5) SaveNow();
            }
            visualClock += dt * (ReducedMotion ? 0.5f : 1f);
            // GR-27: the menu's slow view (behind its Settings too); any other card gives the camera back.
            if (Card == GrowthCard.Menu || Card == GrowthCard.Settings && settingsFrom == GrowthCard.Menu) MenuCamera.Step(Time.unscaledDeltaTime);
            else if (MenuCamera.Active) MenuCamera.End();
            World.Sync(Shop, dt);
            // GR-18 wave 4: the silly moments, after the world followed the shop and before the people move.
            Gags.Step(dt);
            World.Animate(visualClock, dt);
            FindFocus();
            UpdatePipe();
            UpdatePeek(dt);
            bool held = !rebinding && UseHeld();
            HoldActions(held, dt);
            OpenLessonWhenDue(held, dt);
            // GR-29 (F19, the user's choice): click acts on what is under the reticle, and E only ever opens its menu.
            if (!rebinding && Card == GrowthCard.None && (!ValidationMode || AllowValidationInput))
            {
                UpdateCarriedChoice();
                if (GrowthControls.Pressed(GrowthAction.Interact))
                {
                    if (PipeFrom != null) LayAimedPipe();
                    else if (Focused != null) Interact(Focused);
                }
                else if (GrowthControls.Pressed(GrowthAction.Use)) ClickAct();
                else if (GrowthControls.Pressed(GrowthAction.Take)) TakeAct();
            }
            Ui.Tick(dt);
        }

        /// <summary>GR-29 (F13, F21): the creature in hand a click puts in, by its place in the hands (the item bar shows it).</summary>
        public int CarriedChoice { get; private set; }
        public GrowthSpecimen ChosenCarried => Shop.Carried.Count == 0 ? null : Shop.Carried[Mathf.Clamp(CarriedChoice, 0, Shop.Carried.Count - 1)];
        public void ChooseCarried(int index) => CarriedChoice = Mathf.Clamp(index, 0, Mathf.Max(0, Shop.Carried.Count - 1));

        /// <summary>The mouse wheel, or 1, 2 and 3 where no action has them, choose among the creatures in hand.</summary>
        void UpdateCarriedChoice()
        {
            int count = Shop.Carried.Count;
            if (count == 0) { CarriedChoice = 0; return; }
            if (CarriedChoice >= count) CarriedChoice = count - 1;
            var mouse = Mouse.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0;
            if (wheel != 0) CarriedChoice = (CarriedChoice + (wheel < 0 ? 1 : count - 1)) % count;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var digits = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key };
            for (int i = 0; i < digits.Length && i < count; i++)
                if (digits[i].wasPressedThisFrame && !settings.bindings.Any(b => b.input == "Key/" + digits[i].keyCode)) CarriedChoice = i;
        }

        /// <summary>
        /// GR-29 (F19): a click on what is under the reticle: a tank takes the chosen creature in hand, or with empty hands gives
        /// up the one aimed at; the breeding tank takes a parent or gives its offspring; the incubator gives its hatchling, or
        /// takes the siphon's doses. Everything held down (collecting, bagging, cranking) is HoldActions'.
        /// </summary>
        void ClickAct()
        {
            if (PipeFrom != null) { LayAimedPipe(); return; }
            if (Focused == null) return;
            switch (Focused.Kind)
            {
                case GrowthTargetKind.Tank:
                    if (Shop.Carried.Count == 0) { TakeAct(); break; }
                    string tank = Focused.Id; int chosen = ChosenCarried.Id;
                    if (Act(() => Shop.PutIn(tank, chosen)).Failure == GrowthFailure.DoesNotFit) Ui?.Hint("Open it (E) to swap a creature");
                    break;
                case GrowthTargetKind.Breeding:
                    ClickBreeding();
                    break;
                case GrowthTargetKind.Incubator:
                    if (Shop.Hatchling != null) { Act(Shop.TakeHatchling); break; }
                    if (Shop.Incubating) { Ui?.Hint(Mathf.CeilToInt(Shop.IncubationLeft) + " s left"); break; }
                    if (Shop.Held.Count > 0 && Shop.Chamber.Count < Shop.MaxDoses) { Act(Shop.FillChamber); break; }
                    Ui?.Hint(Shop.Chamber.Count > 0 ? "Open it (E) to mix and press Start" : "Your siphon is empty: hold the mouse on a grower to collect");
                    break;
            }
        }

        /// <summary>The take button: a creature from the tank aimed at (with others in hand too), the offspring, or the hatchling.</summary>
        void TakeAct()
        {
            if (PipeFrom != null || Focused == null) return;
            switch (Focused.Kind)
            {
                case GrowthTargetKind.Tank:
                    if (AimedCreature != null) TakeFromTank(AimedCreature); else Ui?.Hint("Aim at a creature to take it");
                    break;
                case GrowthTargetKind.Breeding:
                    if (Shop.HasBreedingTank && Shop.Offspring != null) Act(() => Shop.TakeOffspring());
                    break;
                case GrowthTargetKind.Incubator:
                    if (Shop.Hatchling != null) Act(Shop.TakeHatchling);
                    break;
            }
        }

        /// <summary>GR-05's order: the chosen creature goes in while the pair has room, then the offspring comes out with a free hand.</summary>
        void ClickBreeding()
        {
            if (!Shop.HasBreedingTank) { Ui?.Hint("Open it (E) to see what it needs"); return; }
            bool room = Shop.ActivePairs.Any(p => p.Parents.Count < GrowthShop.PairSize);
            var chosen = ChosenCarried;
            if (room && chosen != null && Shop.ParentCheck(chosen, out _) == GrowthFailure.None) { int id = chosen.Id; Act(() => Shop.PutParent(id)); return; }
            if (room && Shop.Carried.Any(c => Shop.ParentCheck(c, out _) == GrowthFailure.None)) { Act(() => Shop.PutParent()); return; }
            if (Shop.Offspring != null && Shop.Carried.Count < GrowthShop.CarryLimit) { Act(() => Shop.TakeOffspring()); return; }
            Ui?.Hint(Shop.Offspring != null ? "Your hands are full" : chosen != null && room ? "Those can't breed here: open it (E)" : "Open it (E) for the pair");
        }

        /// <summary>
        /// R09.4: a lesson's card opens when its system has appeared and the player is free: no card open, nothing held, no pipe
        /// being laid, a moment after the last card closed; at night, or for a lesson about the day (a wish) while no one waits
        /// at the counter. Opening it meets the lesson; its steps then show on the next-step line until it is learned.
        /// </summary>
        void OpenLessonWhenDue(bool held, float dt)
        {
            Shop.LessonsOff = settings.lessonsOff || ValidationMode && !LessonsOnInValidation;
            freeFor = Card == GrowthCard.None && !held && PipeFrom == null ? freeFor + Time.unscaledDeltaTime : 0;
            if (freeFor < LessonPause) return;
            var lesson = Shop.LessonDue();
            if (lesson == null || !(Shop.Phase == GrowthPhase.Night || lesson.AnyTime && !Shop.Queue.Any())) return;
            Shop.MeetLesson(lesson.Id);
            SetCard(GrowthCard.Lesson, lesson.Id);
            SaveNow();
        }

        /// <summary>R09.4: seconds free of cards before a lesson's card may open.</summary>
        public const float LessonPause = 1.5f;

        /// <summary>Advances the shop and turns its events into the UI's notes; the day's end opens the receipt.</summary>
        public void Advance(float seconds)
        {
            var phase = Shop.Phase;
            Shop.Tick(seconds);
            foreach (var e in Shop.TakeEvents())
            {
                Ui?.Event(e);
                Gags?.Event(e);
                Audio?.Event(e);
                Music?.Event(e);
                Effects?.Event(e);
                // GR-11: the Grand Exhibition held: confetti around the player.
                if (e.Kind == GrowthEventKind.ExhibitionWon) World.Celebrate(Player != null ? Player.transform.position : new Vector3(-0.5f, 0, -1.6f), Player != null && Player.View != null ? Player.View.transform.forward : Vector3.forward);
                // GR-32 (F50): the journal no longer pauses the shop, so a day can end while it is open.
                if (e.Kind == GrowthEventKind.DayEnded) { SaveNow(); if (Card == GrowthCard.None || Card == GrowthCard.Counter || Card == GrowthCard.Journal) SetCard(GrowthCard.Receipt); }
                if (e.Kind == GrowthEventKind.Discovery || e.Kind == GrowthEventKind.NewColour || e.Kind == GrowthEventKind.EraUp || e.Kind == GrowthEventKind.StarUp || e.Kind == GrowthEventKind.CultureOwned) SaveNow();
            }
            if (phase != Shop.Phase) World.SetPhase(Shop.Phase);
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
            bool onTank = Card == GrowthCard.None && Focused != null && Focused.Kind == GrowthTargetKind.Tank;
            // GR-31 (F23): only once standing still: the view narrowing while walking up to a tank felt like suddenly speeding up.
            stillFor = Player.Walking ? 0 : stillFor + dt;
            bool want = PeekOn && onTank && stillFor >= PeekSettle && World.DistanceToGlass(Focused.Id, Player.View.transform.position) <= PeekRange;
            float step = ReducedMotion ? 1 : dt / PeekEase;
            peekBlend = Mathf.MoveTowards(peekBlend, want ? 1 : 0, step);
            Player.View.fieldOfView = Mathf.Lerp(FieldOfView, PeekFov, Mathf.SmoothStep(0, 1, peekBlend));
            // GR-29 (F25): the creature under the reticle, from anywhere a tank can be used, wears an outline; peek also enlarges it.
            AimedCreature = onTank ? World.CreatureNearCentre(Focused.Id, Player.View) : null;
            World.Aim(AimedCreature);
            // While peek eases out after the reticle left the tank there is nothing to read (GR-03 found this throwing).
            PeekCreature = Peeking && want ? AimedCreature : null;
            World.Highlight(PeekCreature);
        }

        /// <summary>GR-29: the creature under the reticle in the tank in focus (a click or the take button takes it).</summary>
        public GrowthCreatureView AimedCreature { get; private set; }
        float stillFor = PeekSettle;
        /// <summary>GR-31: seconds standing still before peek eases in.</summary>
        public const float PeekSettle = 0.2f;

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
                case GrowthTargetKind.AnnexWall: status = Shop.Annex ? "Open" : "E see what it needs"; return "Power room";
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
                    if (!Shop.HasSorter) { var s1 = GrowthContent.Station("sorter").Levels[0]; status = "Era " + s1.Era + " · " + s1.Credits + " credits · E see what it needs"; return "Sorter (not bought)"; }
                    status = Shop.SorterStatus + " · E stock targets";
                    return "Sorter";
                case GrowthTargetKind.Generator:
                    if (!Shop.HasGenerator) { var g1 = GrowthContent.Station("generator").Levels[0]; status = "Era " + g1.Era + " · " + g1.Credits + " credits and " + g1.Rp + " RP · E see what it needs"; return "Generator (not bought)"; }
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
                    status = Shop.Incubating ? Mathf.CeilToInt(Shop.IncubationLeft) + " s left · E open" : Shop.Hatchlings.Count > 1 ? Shop.Hatchlings.Count + " hatchlings are ready · click take · E open" : Shop.Hatchling != null ? "A hatchling is ready · click take · E open" :
                        Shop.Chamber.Count + "/" + Shop.MaxDoses + " doses" + (Shop.Held.Count > 0 && Shop.Chamber.Count < Shop.MaxDoses ? " · click put yours in" : "") + " · E mix";
                    return "Incubator";
                case GrowthTargetKind.Tank:
                    var c = Shop.Container(Focused.Id);
                    if (c == null) return null;
                    if (AimedCreature != null)
                    {
                        var s = c.Occupants.FirstOrDefault(o => o.Id == AimedCreature.SpecimenId);
                        if (s != null)
                        {
                            status = s.Def.Size.ToString().ToLowerInvariant() + " · " + s.Colour + " · " + Shop.Price(s.Species) + " cr" + (c.InSchool(s.Species) ? " · school" : "") + (GrowthShop.Themed(c, s.Species) ? " · themed" : "") +
                                (Shop.Era >= GrowthContent.ComfortEra ? " · " + s.Def.Temperament.ToString().ToLowerInvariant() + ", " + s.Def.Water.ToString().ToLowerInvariant() : "") +
                                (GrowthShop.IsOnDisplay(s) ? " · on display" : s.ReservedBy != 0 ? " · reserved" : Shop.Carried.Count > 0 ? " · click put in · right-click take" : " · click take") + " · E open";
                            return s.Def.Name;
                        }
                    }
                    status = c.Used + "/" + c.Spaces + " spaces" + (Shop.Harmony(c) ? " · in harmony" : "") + (c.Decor != null ? " · " + GrowthShop.DecorOn(c).Theme : "") + (Shop.Carried.Count > 0 ? " · click put in" : "") + " · E open";
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
                        (v == null ? "No one waiting" : "Hold mouse to bag a sale" + (pip ? " beside Pip" : "") + " (" + Shop.Queue.Count() + " waiting)") + " · E details";
                    return "Counter";
                case GrowthTargetKind.Workbench: status = "level " + Shop.Level("bench") + " · E upgrade your siphon or the bench"; return "Research bench";
                case GrowthTargetKind.UnitBWall: status = Shop.UnitB ? "Open" : "E see what it needs"; return "Shop unit B";
                case GrowthTargetKind.Board: status = "E goals"; return "Pinboard";
                case GrowthTargetKind.Breeding:
                    if (!Shop.HasBreedingTank)
                    {
                        var first = GrowthContent.Station("breeding").Levels[0];
                        status = "Era " + first.Era + " · " + first.Credits + " credits and " + first.Rp + " RP · E see what it needs";
                        return "Breeding tank (not bought)";
                    }
                    bool canPut = Shop.ActivePairs.Any(p => p.Parents.Count < GrowthShop.PairSize) && Shop.Carried.Any(c => Shop.ParentCheck(c, out _) == GrowthFailure.None);
                    // What a click does here first: put a creature in, then take an offspring.
                    var waiting = Shop.Pairs.FirstOrDefault(p => p.Offspring != null);
                    status = canPut ? Shop.AllParents.Count() + "/" + GrowthShop.PairSize * Shop.PairPlaces + " parents · click put in · E open" :
                        waiting != null ? Named(waiting.Offspring) + " in the nursery · " + (Shop.Carried.Count < GrowthShop.CarryLimit ? "click take it" : "hands full") + " · E open" :
                        Shop.Breeding ? Mathf.CeilToInt(Shop.BreedingLeft) + " s to the offspring · E details" :
                        Shop.AllParents.Count() + "/" + GrowthShop.PairSize * Shop.PairPlaces + " parents · E open";
                    return "Breeding tank";
            }
            return null;
        }
    }
}
