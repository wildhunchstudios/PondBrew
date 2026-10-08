using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public enum ShopPanel { None, Welcome, Incubator, Tank, Counter, Upgrade, Journal, Pause, Reset, CloseShop, DayReport, Arrange, StorageRack, Conditioning, Presentation, Staff, Stocker, StockerTargets }
    public sealed partial class ShopSession : MonoBehaviour
    {
        public GameModel Model { get; private set; }
        public ShopDayCycle Cycle { get; private set; }
        public ShopTradeLedger Reports { get; private set; }
        public ShopReputation Reputation { get; private set; }
        public GameConfig Config { get; private set; }
        public ShopWorld World { get; private set; }
        public ShopPlayer Player { get; private set; }
        public ShopCustomers Customers { get; private set; }
        public ShopPanel Panel { get; private set; }
        public Station Focused { get; private set; }
        public bool Paused => Panel == ShopPanel.Pause || Panel == ShopPanel.Reset || Panel == ShopPanel.Welcome || Panel == ShopPanel.Presentation;
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
                Model = restored; ShopDayCycle.TryRestore(slot.cycle, out var cycle); Cycle = cycle;
                ShopStaff.TryRestore(slot.staff,Cycle.DayNumber,out var staff); Staff=staff;
                ShopStocking.TryRestore(slot.stocker,Model,Cycle.DayNumber,out var stocking);Stocking=stocking;
                ShopLayout.TryRestore(slot.layout, out var layout); Layout = layout;
                ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var reports); Reports = reports;
                ShopReputation.TryRestore(slot.reputation, out var reputation); Reputation = reputation; Reports.CancelPendingForReload();
                Sensitivity = slot.sensitivity; Volume = slot.volume; SlapKey = slot.slapKey; HumorVolume = slot.humorVolume; ReducedGags = slot.reducedGags;
            }
            else
            {
                Model = new GameModel(Config); Layout = new ShopLayout();
                Cycle = new ShopDayCycle();
                Reports = new ShopTradeLedger();
                Reputation = new ShopReputation();
                Staff = new ShopStaff();Stocking=new ShopStocking();
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
            World.Build(camera); World.ApplyLayout(Layout);
            World.SetDayPhase(Cycle.Phase);
            Customers = gameObject.AddComponent<ShopCustomers>();
            Customers.Initialize(this);
            Customers.ArrivalsEnabled = !ValidationMode;
            Vector3 spawn = World.SpawnPosition; spawn.y = 0.2f;
            Player.Teleport(spawn, Quaternion.identity);
            sound = gameObject.AddComponent<ShopAudio>();
            Cashier=gameObject.AddComponent<ShopCashier>();Cashier.Initialize(this);
            Stocker=gameObject.AddComponent<ShopStocker>();Stocker.Initialize(this);
            InitializeSlap();
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
            bool placementAtFrameStart = PlacementActive;
            bool gameplayAtFrameStart = Panel == ShopPanel.None && !placementAtFrameStart;
            if (placementAtFrameStart) HandlePlacementInput(keyboard);
            if (HandleSlapRebind(keyboard)) return;
            if (!placementAtFrameStart && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetPanel(Panel == ShopPanel.Presentation ? ShopPanel.Pause : Panel == ShopPanel.None ? ShopPanel.Pause : ShopPanel.None);
            if (!placementAtFrameStart && keyboard != null && keyboard.tabKey.wasPressedThisFrame && !Paused)
                SetPanel(Panel == ShopPanel.Journal ? ShopPanel.None : ShopPanel.Journal);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && Model.UpgradeOwned)
                SetBaselinePreview(!BaselinePreview);
#endif
            if (Panel == ShopPanel.Presentation) TickPresentationView();
            ComedyInput(keyboard,gameplayAtFrameStart);
            Player.Sensitivity = Sensitivity;
            Player.InputEnabled = Panel == ShopPanel.None && (!ValidationMode || AllowValidationInput);
            if (!Paused)
            {
                Advance(Time.deltaTime);
                visualClock += Time.deltaTime;
                World.Animate(visualClock, Time.deltaTime);
                World.LabDoor.Tick(Player.transform.position, Time.deltaTime, Stocker.ActiveDoorPosition);
                saveClock += Time.deltaTime;
                if (saveClock >= 5) SaveNow();
            }
            if (placementAtFrameStart)
            {
                Focused = null; SlapTarget = null; StopHarvest(); World.SetHarvesting(false, null);
                return;
            }
            if (ValidationMode && ScriptedHarvest.HasValue)
            {
                Harvest(ScriptedHarvest.Value, Time.deltaTime);
                return;
            }
            FindFocus();
            bool held = Panel == ShopPanel.None && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool grower = TryStationCulture(Focused, out var culture);
            if (held && grower)
                Harvest(culture, Time.deltaTime);
            else StopHarvest();
            World.SetHarvesting(held && grower && HarvestProgress > 0, Focused);
            if (Panel == ShopPanel.None && keyboard != null && keyboard.eKey.wasPressedThisFrame && Focused != null)
                Interact(Focused);
            UpdateSlapInput(keyboard, gameplayAtFrameStart);
        }
        public void Advance(float seconds)
        {
            if (Paused) return;
            slapCooldown.Tick(seconds);
            var previousPhase = Cycle.Phase;
            int previousDay = Cycle.DayNumber;
            Cycle.Tick(seconds);
            bool phaseChanged = previousPhase != Cycle.Phase || previousDay != Cycle.DayNumber;
            if (phaseChanged)
            {
                World.SetDayPhase(Cycle.Phase);
                Notify(Cycle.Phase == ShopDayPhase.Night ? "Night preparation. Your unsold stock is safe. Prepare for day " + Cycle.DayNumber + "." : "Closing time. The next preparation night begins shortly.");
            }
            var result = Model.Tick(seconds);
            Customers.Tick(seconds);
            Cashier.Tick(seconds);
            Stocker.Tick(seconds);
            TickComedy(seconds);
            if (phaseChanged && Cycle.Phase == ShopDayPhase.Night)
            {
                Reports.FinishDay(DisplayedStockCount());
                BeginScheduledStaffNight();
                BeginScheduledStockerNight();
                ShowDayReport();
            }
            if (result.Matured)
            {
                belchWait=4f;belchExpiry=8f;
                if (result.FirstDiscovery)
                {
                    revealedSpecies = result.SpeciesId;
                    revealUntil = Time.unscaledTime + 3.5f;
                    sound.Discovery();
                }
                else sound.Feedback(true);
                Notify("A new specimen is ready. Use an empty display or lab rack to store it.");
            }
            if (result.ConditionedCount > 0)
            { sound.Feedback(true); Notify(result.ConditionedCount + " Lanternfin ready in the conditioning vat. Collect at a rack or display."); }
            if (phaseChanged || result.Matured || result.ConditionedCount > 0) SaveNow();
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview);
        }
        public bool OpenForDay()
        {
            if (!layoutRoutesReady) { Notify("Customer routes could not recover. Save and reopen your shop before trading."); return false; }
            if (PlacementActive || Panel == ShopPanel.Arrange) return false;
            if (!Customers.Ready) { Notify(Customers.Status); return false; }
            if (Paused || Cycle.Phase != ShopDayPhase.Night || Cycle.DayNumber == int.MaxValue) return false;
            if (!Reports.BeginDay(Cycle.DayNumber, DisplayedStockCount()))
            { Notify("The day report could not begin. Save and reload before opening."); return false; }
            Cycle.OpenShop();
            World.SetDayPhase(Cycle.Phase);
            Notify("Day " + Cycle.DayNumber + " is open. Serve the counter and keep your tanks stocked.");
            SaveNow();
            return true;
        }
        public bool CloseForNight()
        {
            if (Paused || !Cycle.RequestClose()) return false;
            CancelComedy();belchWait=-1;
            World.SetDayPhase(Cycle.Phase);
            Notify("Closing in 3 seconds. Finish checkout now; unpaid orders release safely for the night.");
            SaveNow();
            return true;
        }
        public int DisplayedStockCount()
        {
            int count = 0;
            foreach (string tankId in Model.TankIds) if (Model.GetTank(tankId).HasValue) count++;
            return count;
        }
        public bool ShowDayReport()
        {
            if (Reports.LastDayNumber == 0) return false;
            SetPanel(ShopPanel.DayReport);
            return true;
        }
        public string SelectedTank => selectedTank;
        /// <summary>Scrolls the open panel; large values reach the end.</summary>
        public void ScrollPanel(float y) => panelScroll.y = Mathf.Max(0, y);
        public ActionResult TransferToDisplay(string tankId)
        {
            var result = Model.TransferToTank(tankId);
            if (result.Success) Reports.RecordRestock();
            return Perform(result);
        }
        public bool IsAtCounter
        {
            get
            {
                if (Player == null || Player.transform.position.z >= 0.05f) return false;
                foreach (var station in World.Stations)
                    if (station.Kind == StationKind.Counter)
                    {
                        Vector3 delta = Player.transform.position - station.transform.position;
                        return Mathf.Abs(delta.y) < 2.5f && new Vector2(delta.x, delta.z).sqrMagnitude <= 3.3f * 3.3f;
                    }
                return false;
            }
        }
        public bool ServeCustomer()
        {
            if (Paused || Panel != ShopPanel.Counter || !IsAtCounter)
            { Notify("Use the front counter in person to serve the waiting customer."); return false; }
            int agreed = Customers.NextToServe == null ? 0 : Customers.NextToServe.Price;
            var result = Customers.Serve();
            if (!result.HasValue) { Notify("No customer is waiting to check out yet."); return false; }
            bool success = Perform(result.Value).Success;
            if (success) Notify("Sold for the agreed " + agreed + " credits! Your customer is taking their new creature home.");
            return success;
        }
        /// <summary>Tags every displayed specimen of a species. Queued orders keep their agreed price.</summary>
        public ActionResult SetSpeciesPrice(string speciesId, int price)
        {
            var result = Perform(Model.SetPrice(speciesId, price));
            if (result.Success)
            {
                var species = Model.GetSpecies(speciesId);
                var tag = Model.GetPrice(speciesId).Value;
                Notify(species.DisplayName + " now costs " + tag.Price + " credits. " + Customers.BuyerTypes(tag) + " of " +
                    Customers.ArrivingTypes + " customer types will buy at this price.");
            }
            return result;
        }
        /// <summary>Called by the customer director when a visitor refuses a tag.</summary>
        public void PriceRefused(CustomerVisit visit)
        {
            sound.Feedback(false);
            var species = Model.GetSpecies(visit.QuotedSpeciesId);
            var tag = Model.GetPrice(visit.QuotedSpeciesId);
            string who = "A " + visit.ArchetypeName.ToLowerInvariant();
            string what = species == null ? "creature" : species.DisplayName;
            Notify(visit.RefusedOverBudget
                ? who + " refused the " + what + " at " + visit.QuotedPrice + " credits: over their budget of " + visit.Budget + ". Nothing was reserved."
                : who + " refused the " + what + " at " + visit.QuotedPrice + " credits: too expensive" +
                    (tag.HasValue ? " (standard " + tag.Value.Standard + ")" : "") + ". Nothing was reserved.", 6);
        }
        public static string ReactionLabel(PriceReaction reaction)
        {
            switch (reaction)
            {
                case PriceReaction.Bargain: return "Bargain";
                case PriceReaction.Fair: return "Fair price";
                case PriceReaction.Pricey: return "Pricey, still buys";
                case PriceReaction.TooExpensive: return "Too expensive, won't buy";
                default: return "Not judged yet";
            }
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
            if (Paused || !Model.IsCultureUnlocked(kind)) return;
            if (harvesting != kind) { harvestClock = 0; harvesting = kind; }
            if (Model.ReservedCultureCount >= (BaselinePreview ? Config.BaseCapacity : Model.Capacity))
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
                case StationKind.Conditioning:
                    selectedConditioner = station.ConditionerItemId; conditioningInputs.Clear(); SetPanel(ShopPanel.Conditioning); break;
                case StationKind.StorageRack:
                    selectedRack = station.StorageItemId; SetPanel(ShopPanel.StorageRack); break;
                case StationKind.TankA: case StationKind.TankB: case StationKind.Display:
                    selectedTank = station.TankId ?? (station.Kind == StationKind.TankA ? "tank-a" : "tank-b");
                    // An occupied tank opens its price tag; ready output waits safely in the incubator.
                    if (Model.Incubator.Phase == IncubatorPhase.Ready && !Model.GetTank(selectedTank).HasValue && StoredSpecimenCount == 0 && ReadyConditionedCount == 0)
                        TransferToDisplay(selectedTank);
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
            if (panel==ShopPanel.Arrange && Stocker) Stocker.ResetTransient();
            if(Panel!=panel && panel!=ShopPanel.None){CancelComedy();belchWait=-1;}
            if (Panel != panel && Panel == ShopPanel.Presentation) EndPresentationView();
            if (Panel != panel && panel == ShopPanel.Presentation) BeginPresentationView();
            if (panel != ShopPanel.Arrange) pendingFurnitureSaleId = null;
            if (panel != ShopPanel.None && PlacementActive) EndPlacementPreview();
            if (initialized && (Panel == ShopPanel.Pause || Panel == ShopPanel.Reset) && panel == ShopPanel.None) SaveNow();
            if (Panel != panel) panelScroll = Vector2.zero;
            Panel = panel;
            if (panel != ShopPanel.Pause) WaitingForSlapKey = false;
            if (SlapGesture != null) SlapGesture.SetPaused(Paused);
            if (Customers != null) Customers.SetPaused(Paused);
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
            Stocking.PruneTargets(Model);
            var slot = CaptureStaffSlot(Model,Staff);
            bool success = SaveStore.Write(SavePath, slot, out var error);
            SaveError = success ? null : error;
            return success;
        }
        public bool ReloadSaved()
        {
            if (!SaveStore.Read(SavePath, Config, out var model, out var slot, out var error))
            { Notify(error ?? "No saved game yet."); return false; }
            ShopLayout.TryRestore(slot.layout, out var restoredLayout);
            // Do not alter live authority, visitors, or a protected-save error until geometry succeeds.
            if (!TryApplyLayoutGeometry(restoredLayout)) return false;
            AutosaveBlocked = false;
            SaveError = null;
            ResetComedyTransient(); Customers.ResetVisitors(); SlapGesture.Cancel(); SlapTarget = null;
            pendingFurnitureSaleId = null; selectedRack = null; selectedConditioner = null; conditioningInputs.Clear();
            AdoptLayout(restoredLayout);
            Stocker.ResetTransient();
            Model = model; ShopDayCycle.TryRestore(slot.cycle, out var cycle); Cycle = cycle;
            ShopStaff.TryRestore(slot.staff,Cycle.DayNumber,out var staff); Staff=staff;
                ShopStocking.TryRestore(slot.stocker,Model,Cycle.DayNumber,out var stocking);Stocking=stocking;
            Cashier.ResetTransient();Cashier.RefreshVisual();Stocker.RefreshVisual();
            ShopTradeLedger.TryRestore(slot.trades, slot.cycle, out var reports); Reports = reports;
            ShopReputation.TryRestore(slot.reputation, out var reputation); Reputation = reputation; Reports.CancelPendingForReload();
            World.SetDayPhase(Cycle.Phase);
            Sensitivity = slot.sensitivity; Volume = slot.volume; SlapKey = slot.slapKey; HumorVolume = slot.humorVolume; ReducedGags = slot.reducedGags;
            AudioListener.volume = Volume;
            BaselinePreview = false;
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); World.SetToolUpgrade(Model.UpgradeOwned);
            // A successful restore supersedes notices from the state that was replaced.
            message = null; messageUntil = 0;
            return true;
        }
        public void ConfirmNewGame()
        {
            var fresh = new GameModel(Config);
            var freshCycle = new ShopDayCycle();
            var freshReports = new ShopTradeLedger();
            var freshReputation = new ShopReputation();
            var freshLayout = new ShopLayout();
            if (!TryApplyLayoutGeometry(freshLayout)) return;
            if (!SaveStore.Write(SavePath,new SlotData {game=fresh.CaptureSave(),cycle=freshCycle.CaptureSave(),trades=freshReports.CaptureSave(),reputation=freshReputation.CaptureSave(),layout=freshLayout.CaptureSave(),sensitivity=Sensitivity,volume=Volume,slapKey=SlapKey,humorVolume=HumorVolume,reducedGags=ReducedGags},out var error))
            { RestoreCurrentLayout(); SaveError = error; SetPanel(ShopPanel.Reset); return; }
            ResetComedyTransient(); Customers.ResetVisitors(); SlapGesture.Cancel(); SlapTarget = null;
            selectedRack = null; selectedConditioner = null; conditioningInputs.Clear();
            Stocker.ResetTransient();
            Model = fresh; AutosaveBlocked = false; BaselinePreview = false; SaveError = null;
            Cycle = freshCycle; Reports = freshReports; Reputation = freshReputation; Staff=new ShopStaff();Stocking=new ShopStocking();
            Cashier.ResetTransient();Cashier.RefreshVisual();Stocker.RefreshVisual();World.SetDayPhase(Cycle.Phase);
            AdoptLayout(freshLayout);
            revealUntil = 0; revealedSpecies = null;
            World.Refresh(Model); World.SetToolUpgrade(Model.UpgradeOwned && !BaselinePreview); World.SetToolUpgrade(false);
            Vector3 spawn = World.SpawnPosition; spawn.y = 0.2f;
            Player.Teleport(spawn, Quaternion.identity);
            SetPanel(ShopPanel.None); Notify("Preparation night 1. Grow your first stock, then open at the front counter.");
        }
        void OnApplicationFocus(bool focus) { if (!focus && initialized && !ValidationMode && !Paused) SetPanel(ShopPanel.Pause); }
        void OnApplicationQuit() { if (initialized && !quitRequested) SaveNow(); }
        void OnDestroy() { DisposeHudTheme(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
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
                case "UnknownConditioner": return "That conditioning vat is no longer placed.";
                case "ConditionerBusy": return "Collect every waiting creature before starting another batch.";
                case "InvalidConditioningInput": return "Choose one or two different stored Pondlets for this known process.";
                case "ConditionerNotProcessing": return "There is no running batch to cancel.";
                case "UnknownStorage": return "That storage compartment is no longer available.";
                case "StockReserved": return "Nori has reserved this creature or display. Pause stocking to use it manually.";
                case "StorageFull": return "That compartment is full. The waiting creature is safe.";
                case "StorageEmpty": return "That storage compartment is empty.";
                case "SpecimenMismatch": return "That exact creature has moved. Choose the current stock again.";
                case "ReservoirFull": return "Reservoir full. Staged doses still reserve their return space.";
                case "GrowerEmpty": case "EmptyGrower": return "Grower is replenishing. Try again shortly.";
                case "InsufficientCulture": return "Collect enough of each required culture first.";
                case "ResearchLocked": return "Unlock the required Spark or Mist culture in the back lab first.";
                case "MistPrerequisiteMissing": return "Discover Lanternfin and place a conditioning vat before Mist research.";
                case "MistAlreadyUnlocked": return "Mist is already unlocked. No payment taken.";
                case "ResearchPrerequisiteMissing": return "Discover Pondlet, Bubblebelly and Ribbonfin before Spark research.";
                case "ResearchAlreadyUnlocked": return "Spark research is already unlocked. No payment taken.";
                case "IncubatorBusy": return "Incubator occupied. Finish and transfer its specimen first.";
                case "InvalidRecipe": case "UnsupportedRecipe": return "Unsupported mixture. Return every dose safely, or adjust the counts.";
                case "TankFull": return "Tank full. Sell its resident at the counter first. Your new specimen is safe.";
                case "TankEmpty": return "There is no specimen in that tank.";
                case "InsufficientFunds": return "Not enough credits yet. Keep growing and selling creatures.";
                case "AlreadyUpgraded": return "Your siphon is already upgraded.";
                case "UndiscoveredRecipe": return "Discover this species by experimenting first.";
                case "TankReserved": return "This creature is reserved for a waiting customer. Serve their order at the counter.";
                case "NoCustomerReady": return "Wait for the customer to reach checkout before serving.";
                case "ReservationMismatch": return "That order is no longer available. No payment was taken.";
                case "InvalidPrice": return "That price is outside this creature's allowed range. The tag is unchanged.";
                case "PriceTooHigh": return "The customer refused that price. Nothing was reserved or paid.";
                default: return "Action blocked: " + code + ". No resources were lost.";
            }
        }
    }
}



