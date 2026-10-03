using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public enum ShopPanel { None, Welcome, Incubator, Tank, Counter, Upgrade, Journal, Pause, Reset }
    public sealed partial class ShopSession : MonoBehaviour
    {
        public GameModel Model { get; private set; }
        public GameConfig Config { get; private set; }
        public ShopWorld World { get; private set; }
        public ShopPlayer Player { get; private set; }
        public ShopPanel Panel { get; private set; }
        public Station Focused { get; private set; }
        public bool Paused => Panel == ShopPanel.Pause || Panel == ShopPanel.Reset || Panel == ShopPanel.Welcome;
        public bool ValidationMode { get; private set; }
        public string SavePath { get; private set; }
        public float Sensitivity = 1;
        public float Volume = 0.65f;
        public float HarvestProgress { get; private set; }
        public bool BaselinePreview { get; private set; }
        public CultureKind? ScriptedHarvest;
        public bool AllowValidationInput;
        public bool AutosaveBlocked { get; private set; }
        public string SaveError { get; private set; }
        ShopAudio sound;
        string message;
        float messageUntil, revealUntil, saveClock, visualClock;
        string revealedSpecies, selectedTank = "tank-a";
        bool initialized, quitRequested;
        CultureKind? harvesting;
        float harvestClock;
        public static string Arg(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (args[i] == key) return i + 1 < args.Length ? args[i + 1] : "";
            return null;
        }
        void Awake()
        {
            Application.targetFrameRate = 120;
            Config = GameConfig.CreateDefault();
            ValidationMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-evolutionSmoke") >= 0;
            SavePath = ValidationMode ? Path.Combine(Arg("-evolutionEvidence") ?? Application.temporaryCachePath, "smoke-save.json") : SaveStore.DefaultPath;
            if (!ValidationMode && SaveStore.Read(SavePath, Config, out var restored, out var slot, out var problem))
            {
                Model = restored; Sensitivity = slot.sensitivity; Volume = slot.volume;
            }
            else
            {
                Model = new GameModel(Config);
                if (!ValidationMode && File.Exists(SavePath))
                {
                    AutosaveBlocked = true;
                    SaveStore.Read(SavePath, Config, out _, out _, out var reason);
                    SaveError = reason;
                }
            }
            var camera = new GameObject("Player camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 68;
            camera.nearClipPlane = 0.035f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.11f, 0.15f);
            camera.gameObject.AddComponent<AudioListener>();
            Player = new GameObject("Player").AddComponent<ShopPlayer>();
            Player.Initialize(camera);
            World = new GameObject("Evolution Shop").AddComponent<ShopWorld>();
            World.Build(camera);
            Vector3 spawn = World.SpawnPosition; spawn.y = 0.2f;
            Player.Teleport(spawn, Quaternion.identity);
            sound = gameObject.AddComponent<ShopAudio>();
            AudioListener.volume = Volume;
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview);
            initialized = true;
            SetPanel(ValidationMode ? ShopPanel.None : ShopPanel.Welcome);
            Debug.Log("EVOLUTION_RUNTIME " + Application.dataPath + " save=" + SavePath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ValidationMode) gameObject.AddComponent<MilestoneSmoke>();
#endif
        }
        void Update()
        {
            if (!initialized) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetPanel(Panel == ShopPanel.None ? ShopPanel.Pause : ShopPanel.None);
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && !Paused)
                SetPanel(Panel == ShopPanel.Journal ? ShopPanel.None : ShopPanel.Journal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && Model.UpgradeOwned)
                SetBaselinePreview(!BaselinePreview);
#endif
            Player.Sensitivity = Sensitivity;
            Player.InputEnabled = Panel == ShopPanel.None && (!ValidationMode || AllowValidationInput);
            if (!Paused)
            {
                Advance(Time.deltaTime);
                visualClock += Time.deltaTime;
                World.Animate(visualClock, Time.deltaTime);
                saveClock += Time.deltaTime;
                if (saveClock >= 5) SaveNow();
            }
            if (ValidationMode && ScriptedHarvest.HasValue)
            {
                Harvest(ScriptedHarvest.Value, Time.deltaTime);
                return;
            }
            FindFocus();
            bool held = Panel == ShopPanel.None && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool grower = Focused != null && (Focused.Kind == StationKind.Bloom || Focused.Kind == StationKind.Flow);
            if (held && grower)
                Harvest(Focused.Kind == StationKind.Bloom ? CultureKind.Bloom : CultureKind.Flow, Time.deltaTime);
            else StopHarvest();
            World.SetHarvesting(held && grower && HarvestProgress > 0, Focused);
            if (Panel == ShopPanel.None && keyboard != null && keyboard.eKey.wasPressedThisFrame && Focused != null)
                Interact(Focused);
        }
        public void Advance(float seconds)
        {
            if (Paused) return;
            var result = Model.Tick(seconds);
            if (result.Matured)
            {
                if (result.FirstDiscovery)
                {
                    revealedSpecies = result.SpeciesId;
                    revealUntil = Time.unscaledTime + 3.5f;
                    sound.Discovery();
                }
                else sound.Feedback(true);
                Notify("A new specimen is ready. Take it to an empty display tank.");
                SaveNow();
            }
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview);
        }
        void FindFocus()
        {
            Focused = null;
            if (Panel != ShopPanel.None) return;
            var ray = new Ray(Player.View.transform.position, Player.View.transform.forward);
            if (Physics.SphereCast(ray, 0.14f, out var hit, 3.3f, ~0, QueryTriggerInteraction.Collide))
                Focused = hit.collider.GetComponentInParent<Station>();
        }
        public void Harvest(CultureKind kind, float dt)
        {
            if (Paused) return;
            if (harvesting != kind) { harvestClock = 0; harvesting = kind; }
            if (Model.Available(CultureKind.Bloom) + Model.Available(CultureKind.Flow) + Model.Staged(CultureKind.Bloom) + Model.Staged(CultureKind.Flow) >= (BaselinePreview ? Config.BaseCapacity : Model.Capacity))
            {
                HarvestProgress = 0;
                Notify("Reservoir full. Dose or release spare culture at the incubator.", 1);
                return;
            }
            if (Model.GetGrower(kind).Units == 0) { HarvestProgress = 0; return; }
            float interval = BaselinePreview ? Config.BaseCollectionSeconds : Model.CollectionSecondsPerUnit;
            harvestClock += dt;
            HarvestProgress = Mathf.Clamp01(harvestClock / interval);
            if (harvestClock < interval) return;
            harvestClock -= interval;
            var result = Model.Collect(kind);
            if (result.Success)
            {
                World.PulseHarvest(kind); sound.Collect(); World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); SaveNow();
            }
            else Notify(FailureText(result.Failure));
        }
        public void StopHarvest() { harvesting = null; harvestClock = 0; HarvestProgress = 0; }
        public void Interact(Station station)
        {
            switch (station.Kind)
            {
                case StationKind.Bloom: case StationKind.Flow:
                    Notify("Hold left mouse to siphon living cultures."); break;
                case StationKind.Incubator: SetPanel(ShopPanel.Incubator); break;
                case StationKind.TankA: case StationKind.TankB:
                    selectedTank = station.Kind == StationKind.TankA ? "tank-a" : "tank-b";
                    if (Model.Incubator.Phase == IncubatorPhase.Ready) Perform(Model.TransferToTank(selectedTank));
                    else SetPanel(ShopPanel.Tank);
                    break;
                case StationKind.Counter: SetPanel(ShopPanel.Counter); break;
                case StationKind.Upgrade: SetPanel(ShopPanel.Upgrade); break;
            }
        }
        public ActionResult Perform(ActionResult result)
        {
            sound.Feedback(result.Success);
            if (result.Success)
            {
                Notify("Done.", 1.3f);
                if (BaselinePreview && Model.ReservedCultureCount > Config.BaseCapacity) SetBaselinePreview(false);
                World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); SaveNow();
            }
            else Notify(FailureText(result.Failure));
            return result;
        }
        public void SetPanel(ShopPanel panel)
        {
            if (initialized && (Panel == ShopPanel.Pause || Panel == ShopPanel.Reset) && panel == ShopPanel.None) SaveNow();
            if (Panel != panel) panelScroll = Vector2.zero;
            Panel = panel;
            StopHarvest();
            Cursor.lockState = panel == ShopPanel.None ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = panel != ShopPanel.None;
            if (Player != null) Player.InputEnabled = panel == ShopPanel.None && !ValidationMode;
        }
        public bool SetBaselinePreview(bool enabled)
        {
            if (enabled && (!Model.UpgradeOwned || Model.ReservedCultureCount > Config.BaseCapacity))
            { Notify("Comparison needs an owned upgrade and at most four cultures."); return false; }
            BaselinePreview = enabled;
            World.SetToolUpgrade(Model.UpgradeOwned && !enabled);
            Notify(enabled ? "DEV comparison: original 4-unit chamber / slower collection." : "Upgraded chamber restored.");
            return true;
        }
        public void Notify(string text, float seconds = 4) { message = text; messageUntil = Time.unscaledTime + seconds; }
        public bool SaveNow()
        {
            saveClock = 0;
            if (AutosaveBlocked)
            {
                SaveError = "Saving blocked: original save is invalid. Reload a repaired save or confirm New game.";
                return false;
            }
            var slot = new SlotData {game = Model.CaptureSave(), sensitivity = Sensitivity, volume = Volume};
            bool success = SaveStore.Write(SavePath, slot, out var error);
            SaveError = success ? null : error;
            return success;
        }
        public bool ReloadSaved()
        {
            if (!SaveStore.Read(SavePath, Config, out var model, out var slot, out var error))
            { Notify(error ?? "No saved game yet."); return false; }
            AutosaveBlocked = false;
            SaveError = null;
            Model = model; Sensitivity = slot.sensitivity; Volume = slot.volume;
            AudioListener.volume = Volume;
            BaselinePreview = false;
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); World.SetToolUpgrade(Model.UpgradeOwned);
            return true;
        }
        public void ConfirmNewGame()
        {
            var fresh = new GameModel(Config);
            if (!SaveStore.Write(SavePath,new SlotData {game=fresh.CaptureSave(),sensitivity=Sensitivity,volume=Volume},out var error))
            { SaveError = error; SetPanel(ShopPanel.Reset); return; }
            Model = fresh; AutosaveBlocked = false; BaselinePreview = false; SaveError = null;
            revealUntil = 0; revealedSpecies = null;
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); World.SetToolUpgrade(false);
            Vector3 spawn = World.SpawnPosition; spawn.y = 0.2f;
            Player.Teleport(spawn, Quaternion.identity);
            SetPanel(ShopPanel.None); Notify("Fresh start. Growers are free and always replenish.");
        }
        void OnApplicationFocus(bool focus) { if (!focus && initialized && !ValidationMode && !Paused) SetPanel(ShopPanel.Pause); }
        void OnApplicationQuit() { if (initialized && !quitRequested) SaveNow(); }
        void OnDestroy() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void Quit()
        {
            if (!SaveNow()) { SetPanel(ShopPanel.Pause); return; }
            quitRequested = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public static string FailureText(FailureCode code)
        {
            switch (code.ToString())
            {
                case "ReservoirFull": return "Reservoir full. Staged doses still reserve their return space.";
                case "GrowerEmpty": case "EmptyGrower": return "Grower is replenishing. Try again shortly.";
                case "InsufficientCulture": return "Collect enough Bloom and Flow first.";
                case "IncubatorBusy": return "Incubator occupied. Finish and transfer its specimen first.";
                case "InvalidRecipe": case "UnsupportedRecipe": return "Unsupported mixture. Return every dose safely, or adjust the counts.";
                case "TankFull": return "Tank full. Sell its resident at the counter first. Your new specimen is safe.";
                case "TankEmpty": return "There is no specimen in that tank.";
                case "InsufficientFunds": return "Not enough credits yet. Keep growing and selling creatures.";
                case "AlreadyUpgraded": return "Your siphon is already upgraded.";
                case "UndiscoveredRecipe": return "Discover this species by experimenting first.";
                default: return "Action blocked: " + code + ". No resources were lost.";
            }
        }
    }
}



