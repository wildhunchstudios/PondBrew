using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-03 HUD on the UI Toolkit layer. Persistent: the day chip, money and the next step (UX.4). Contextual:
    /// the queue strip, toasts and ready pings with edge arrows, floaters, the discovery stamp, the interaction
    /// prompt, caption bubbles and the tutorial marker. It reads <see cref="ShopHudState"/> only.
    /// World-anchored pieces are placed so they never cover the crosshair area or another HUD element.
    /// </summary>
    public sealed class HudPresenter : ShopUiPresenter
    {
        public const string RootName = "es-hud";
        public const string ItemClass = "es-hud-item";
        public const string DayName = "es-hud-day", MoneyName = "es-hud-money", QueueName = "es-hud-queue";
        public const string NextName = "es-hud-next-card", PromptName = "es-hud-prompt", SlapName = "es-hud-slap";
        public const string DiscoveryName = "es-hud-discovery", BubbleName = "es-hud-bubble", MarkerName = "es-hud-marker";
        public const string CrosshairName = "es-hud-crosshair";
        public const string CoinLayerName = "es-hud-coins", CoinName = "es-hud-coin";
        const float Margin = 20f;
        static readonly float[] Gaps = { 28f, ShopHudRules.CrosshairHalf + 20f };
        static readonly int[] BubbleSlots = { 2, 0, 1, 3 };
        static readonly int[] MarkerSlots = { 0, 1, 2, 3 };

        sealed class Toast { public VisualElement Element; public Label Text; public VisualElement Icon; public string Key; public float Until, Started; public bool Pending; public float Height = 64f; }
        sealed class Floater { public VisualElement Element; public Label Text; public VisualElement Icon; public float Born; }
        sealed class Arrow { public VisualElement Element; public HudArrowGlyph Glyph; public Label Label; }

        readonly ShopHudState state = new ShopHudState();
        readonly HudPingTracker pingTracker = new HudPingTracker();
        readonly List<HudPing> startedPings = new List<HudPing>();
        readonly List<Rect> occupied = new List<Rect>();
        readonly List<HudFace> faces = new List<HudFace>();
        readonly List<Toast> pingToasts = new List<Toast>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Arrow> arrows = new List<Arrow>();

        VisualElement day, dayIcon, money, moneyIcon, queue, floaterLayer, toastLayer, discovery, recipe, crosshair;
        VisualElement prompt, promptProgress, slap, nextCard, nextIcon, bubble, marker, arrowLayer;
        Label dayTitle, dayDetail, moneyAmount, queueMore, discoveryTitle, discoveryName, discoveryNote;
        Label promptKey, promptKey2, promptName, promptStatus, slapKey, slapName, slapStatus;
        Label nextText, nextHelp, bubbleText, bubbleKey, bubbleHint, markerTitle, markerAction;
        Toast saveToast, noticeToast;
        Texture2D lastDayIcon;
        bool iconsReady, baselined;
        string objective;
        float objectiveSince;
        int moneySequence, reputation, appeal;
        object generation;

        // UX-08 money feel: the chip counts to the saved amount; a sale also rings and sends coins from the counter.
        VisualElement coinLayer;
        readonly List<VisualElement> coins = new List<VisualElement>();
        long moneyShown, tickFrom, tickTo;
        float tickStart, coinsStart, bumpAt;
        bool ticking, coinsFlying, bumpPending, stampDropped;
        int coinCount, cueSales, cueDay;
        Vector2 coinFrom, coinTo;

        public ShopHudState State => state;
        /// <summary>Where the last frame put the world-anchored pieces; NaN when hidden (for validation).</summary>
        public Rect BubbleRect { get; private set; }
        public Rect MarkerRect { get; private set; }
        public int ArrowsShown { get; private set; }
        /// <summary>The last placed edge arrow's direction in panel space (y down).</summary>
        public Vector2 LastArrowDirection { get; private set; }
        public int ToastsShown { get; private set; }
        /// <summary>Toasts waiting for room in the top-centre column this frame.</summary>
        public int ToastsHeldBack { get; private set; }
        public int FloatersShown => floaters.Count;
        public bool NextStepShown { get; private set; }
        /// <summary>UX-08: the amount the money chip shows this frame (it counts up to the saved credits).</summary>
        public long MoneyShown => moneyShown;
        public bool MoneyTicking => ticking;
        public int SaleCues { get; private set; }
        public int CoinFlights { get; private set; }
        public int CoinsLaunched { get; private set; }
        public int CoinsShown { get; private set; }
        public Vector2 LastCoinOrigin { get; private set; }
        public Vector2 LastCoinTarget { get; private set; }
        public int StampDrops { get; private set; }
        /// <summary>No coins in the air and the money chip at the saved amount.</summary>
        public bool FeelSettled => !coinsFlying && !ticking && !bumpPending;

        protected override VisualElement Build(VisualTreeAsset template) => Extract(template, RootName);

        protected override void OnBound()
        {
            var e = Element;
            day = e.Q(DayName); dayIcon = e.Q("es-hud-day-icon"); dayTitle = e.Q<Label>("es-hud-day-title"); dayDetail = e.Q<Label>("es-hud-day-detail");
            money = e.Q(MoneyName); moneyIcon = e.Q("es-hud-money-icon"); moneyAmount = e.Q<Label>("es-hud-money-amount");
            queue = e.Q(QueueName); floaterLayer = e.Q("es-hud-floaters"); toastLayer = e.Q("es-hud-toasts");
            discovery = e.Q(DiscoveryName); recipe = e.Q("es-hud-discovery-recipe");
            discoveryTitle = e.Q<Label>("es-hud-discovery-title"); discoveryName = e.Q<Label>("es-hud-discovery-name"); discoveryNote = e.Q<Label>("es-hud-discovery-note");
            crosshair = e.Q(CrosshairName);
            prompt = e.Q(PromptName); promptProgress = e.Q("es-hud-prompt-progress");
            promptKey = e.Q<Label>("es-hud-prompt-key"); promptKey2 = e.Q<Label>("es-hud-prompt-key2");
            promptName = e.Q<Label>("es-hud-prompt-name"); promptStatus = e.Q<Label>("es-hud-prompt-status");
            slap = e.Q(SlapName); slapKey = e.Q<Label>("es-hud-slap-key"); slapName = e.Q<Label>("es-hud-slap-name"); slapStatus = e.Q<Label>("es-hud-slap-status");
            nextCard = e.Q(NextName); nextIcon = e.Q("es-hud-next-icon"); nextText = e.Q<Label>("es-hud-next-text"); nextHelp = e.Q<Label>("es-hud-next-help");
            bubble = e.Q(BubbleName); bubbleText = e.Q<Label>("es-hud-bubble-text"); bubbleKey = e.Q<Label>("es-hud-bubble-key"); bubbleHint = e.Q<Label>("es-hud-bubble-hint");
            marker = e.Q(MarkerName); markerTitle = e.Q<Label>("es-hud-marker-title"); markerAction = e.Q<Label>("es-hud-marker-action");
            arrowLayer = e.Q("es-hud-arrows");
            queueMore = new Label { name = "es-hud-queue-more" };
            queueMore.AddToClassList("es-queue__more");
            queue.Add(queueMore);
            coinLayer = new VisualElement { name = CoinLayerName, pickingMode = PickingMode.Ignore };
            coinLayer.AddToClassList("es-hud");
            e.Add(coinLayer);
            saveToast = MakeToast("es-hud-toast-save", true);
            noticeToast = MakeToast("es-hud-toast-notice", false);
            for (int i = 0; i < ShopHudRules.MaxToasts; i++) pingToasts.Add(MakeToast("es-hud-toast-ping-" + i, false));
            discoveryTitle.text = Strings.Get("hud.discovery.title");
            discoveryNote.text = Strings.Get("hud.discovery.journal");
            bubbleKey.text = Strings.Get("hud.key.skip");
            bubbleHint.text = Strings.Get("hud.bubble.skip");
            nextHelp.text = Strings.Get("hud.key.help");
            IgnorePicking(e);
        }

        public override void Refresh()
        {
            if (Element == null || Shop == null) return;
            var root = Shop.ToolkitUi;
            if (root == null || !root.Active || Shop.Panel == ShopPanel.Presentation)
            {
                if (IsShown) Hide();
                baselined = false;
                StopMoneyFeel();
                return;
            }
            if (!IsShown) { Show(); baselined = false; pingTracker.Reset(); }
            Shop.FillHudState(state, Strings);
            if (!ReferenceEquals(state.Generation, generation)) { generation = state.Generation; baselined = false; }
            float now = Time.unscaledTime;
            var keyboard = Keyboard.current;
            EnsureIcons();
            UpdateDay();
            UpdateMoneyAndFloaters(now);
            UpdateQueue(keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed));
            UpdateTopColumn(now);
            UpdatePrompts();
            UpdateNextStep(now, keyboard != null && keyboard.f1Key.wasPressedThisFrame);
            LayoutWorld();
            baselined = true;
        }

        public override void Unbind()
        {
            faces.Clear(); pingToasts.Clear(); floaters.Clear(); arrows.Clear(); coins.Clear();
            base.Unbind();
        }

        // ---------- persistent corners ----------

        void UpdateDay()
        {
            ShopHudRules.DayChip(Strings, state.Phase, state.Day, state.SecondsLeft, state.Paused, out string title, out string detail);
            SetText(dayTitle, title);
            SetText(dayDetail, detail);
            var icon = Shop.HudIcon(state.Phase == HudDayPhase.Night ? ShopUiTheme.Symbol.Moon : ShopUiTheme.Symbol.Sun);
            if (icon && icon != lastDayIcon) { dayIcon.style.backgroundImage = new StyleBackground(icon); lastDayIcon = icon; }
        }

        void UpdateMoneyAndFloaters(float now)
        {
            // Money comes from the saved-result cue (FX-18 rule: presentation follows the authoritative result).
            bool cue = baselined && state.MoneySequence != moneySequence && state.MoneyDelta != 0;
            UpdateMoneyFeel(cue);
            SetText(moneyAmount, moneyShown.ToString("N0"));
            if (cue)
                AddFloater(ShopHudRules.Signed(state.MoneyDelta), ShopUiTheme.Symbol.Coin, state.MoneyDelta < 0, now);
            // Points that change while paused are a new baseline, not news; a reload or New game swaps the model
            // (a new Generation). Placement counts: committing decor changes appeal while neither in play nor in a panel.
            bool countable = !state.Paused;
            if (baselined && countable && state.Reputation != reputation)
                AddFloater(Strings.Format("hud.float.rep", ShopHudRules.Signed(state.Reputation - reputation)), ShopUiTheme.Symbol.Star, state.Reputation < reputation, now);
            if (baselined && countable && state.Appeal != appeal)
                AddFloater(Strings.Format("hud.float.appeal", ShopHudRules.Signed(state.Appeal - appeal)), ShopUiTheme.Symbol.Leaf, state.Appeal < appeal, now);
            moneySequence = state.MoneySequence; reputation = state.Reputation; appeal = state.Appeal;

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                float age = now - f.Born;
                if (age >= ShopHudRules.FloaterSeconds) { f.Element.RemoveFromHierarchy(); floaters.RemoveAt(i); continue; }
                ShopHudRules.FloaterPose(age, state.Reduced, out float alpha, out float rise);
                f.Element.style.opacity = alpha;
                f.Element.style.translate = new Translate(0, -rise);
            }
        }

        // ---------- UX-08 money feel ----------

        void UpdateMoneyFeel(bool cue)
        {
            var feel = Feel;
            if (!baselined || feel == null)
            {
                // A new baseline (shown, reload, New game) is not news: the chip shows the amount as it is.
                StopMoneyFeel();
                cueSales = state.SalesToday; cueDay = state.SalesDay;
                return;
            }
            float clock = feel.Now;
            if (cue) StartMoney(feel, clock);
            // Credits that changed with no saved cue (a failed save is undone the same way) are shown as they are.
            else if (state.Currency != tickTo) { ticking = false; moneyShown = tickTo = state.Currency; }
            if (ticking)
            {
                moneyShown = UiMotion.CountUp(tickFrom, tickTo, clock - tickStart, UiMotion.MoneyTickSeconds);
                if (clock - tickStart >= UiMotion.MoneyTickSeconds) { ticking = false; moneyShown = tickTo; }
            }
            if (bumpPending && clock >= bumpAt) { bumpPending = false; feel.Motion.Play(money, UiMotionKind.ChipBump); }
            CoinsShown = 0;
            if (!coinsFlying) return;
            float age = clock - coinsStart;
            for (int i = 0; i < coins.Count; i++)
            {
                var coin = coins[i];
                Vector2 at = default; float scale = 1;
                bool on = i < coinCount && UiMotion.CoinAt(coinFrom, coinTo, i, age, out at, out scale);
                SetShown(coin, on);
                if (!on) continue;
                CoinsShown++;
                float half = coin.layout.width > 1 && !float.IsNaN(coin.layout.width) ? coin.layout.width * .5f : 17f;
                coin.style.left = at.x - half;
                coin.style.top = at.y - half;
                coin.style.scale = new Scale(new Vector3(scale, scale, 1));
            }
            if (age >= UiMotion.CoinTotalSeconds) { coinsFlying = false; foreach (var c in coins) SetShown(c, false); CoinsShown = 0; }
        }

        void StartMoney(UiFeel feel, float clock)
        {
            // A sale is a gain that came with a new sale in today's ledger since the last cue.
            bool sale = state.MoneyDelta > 0 && state.SalesToday > (state.SalesDay == cueDay ? cueSales : 0);
            cueSales = state.SalesToday; cueDay = state.SalesDay;
            tickFrom = moneyShown; tickTo = state.Currency; tickStart = clock;
            ticking = tickFrom != tickTo;
            if (!sale) return;
            SaleCues++;
            feel.Sounds.Play(UiSound.KaChing);
            int count = UiMotion.CoinCount(feel.Reduced, feel.Low);
            Vector2 target = moneyIcon.worldBound.center;
            if (count > 0 && !float.IsNaN(target.x) && TryCoinOrigin(out Vector2 origin))
            {
                EnsureCoins();
                coinFrom = origin; coinTo = target; coinCount = count; coinsStart = clock; coinsFlying = true;
                CoinFlights++; CoinsLaunched += count;
                LastCoinOrigin = origin; LastCoinTarget = target;
                // The chip counts up and bumps as the first coin lands.
                tickStart = clock + UiMotion.CoinFlightSeconds;
            }
            if (!feel.Reduced) { bumpPending = true; bumpAt = tickStart; }
        }

        /// <summary>The counter card's Serve button when the card is open, else the counter on screen; false when neither shows.</summary>
        bool TryCoinOrigin(out Vector2 origin)
        {
            origin = default;
            var card = Shop.ToolkitUi != null ? Shop.ToolkitUi.StationCard : null;
            if (card != null && card.ShownPanel == ShopPanel.Counter)
            {
                Rect r = card.PrimaryRect;
                if (!float.IsNaN(r.x)) { origin = r.center; return true; }
            }
            var camera = Shop.Player != null ? Shop.Player.View : Camera.main;
            if (!state.HasCounter || camera == null || Element.panel == null) return false;
            if (camera.WorldToViewportPoint(state.CounterWorld).z <= 0) return false;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(Element.panel, state.CounterWorld, camera);
            Vector2 size = Element.layout.size;
            if (float.IsNaN(p.x) || p.x < 0 || p.y < 0 || p.x > size.x || p.y > size.y) return false;
            origin = p;
            return true;
        }

        void EnsureCoins()
        {
            var tex = Shop.HudIcon(ShopUiTheme.Symbol.Coin);
            while (coins.Count < UiMotion.Coins)
            {
                var coin = new VisualElement { name = CoinName, pickingMode = PickingMode.Ignore };
                coin.AddToClassList("es-coin");
                coin.style.display = DisplayStyle.None;
                coinLayer.Add(coin);
                coins.Add(coin);
            }
            if (tex) foreach (var c in coins) if (c.style.backgroundImage.value.texture != tex) c.style.backgroundImage = new StyleBackground(tex);
        }

        void StopMoneyFeel()
        {
            ticking = coinsFlying = bumpPending = false;
            moneyShown = tickTo = state.Currency;
            foreach (var c in coins) SetShown(c, false);
            CoinsShown = 0;
        }

        void AddFloater(string text, ShopUiTheme.Symbol symbol, bool loss, float now)
        {
            if (floaters.Count >= 4) { floaters[0].Element.RemoveFromHierarchy(); floaters.RemoveAt(0); }
            var element = new VisualElement { name = "es-hud-floater", pickingMode = PickingMode.Ignore };
            element.AddToClassList("es-floater"); element.AddToClassList(ItemClass);
            element.EnableInClassList("es-floater--loss", loss);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("es-floater__icon");
            var tex = Shop.HudIcon(symbol);
            if (tex) icon.style.backgroundImage = new StyleBackground(tex);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("es-floater__text");
            element.Add(icon); element.Add(label);
            element.style.opacity = 0;
            floaterLayer.Add(element);
            floaters.Add(new Floater { Element = element, Text = label, Icon = icon, Born = now });
        }

        // ---------- queue strip ----------

        void UpdateQueue(bool alt)
        {
            int shown = Mathf.Min(state.Queue.Count, ShopHudRules.MaxFaces);
            while (faces.Count < shown)
            {
                var face = new HudFace { name = "es-hud-face" };
                queue.Insert(faces.Count, face);
                faces.Add(face);
            }
            for (int i = 0; i < faces.Count; i++)
            {
                bool on = i < shown;
                SetShown(faces[i], on);
                if (!on) continue;
                var entry = state.Queue[i];
                faces[i].Set(ShopHudRules.Patience01(entry), ShopHudRules.PatienceLow(entry), entry.AtCounter, ArchetypeColor(entry.ArchetypeId),
                    alt && entry.AtCounter ? Mathf.CeilToInt(entry.SecondsLeft).ToString() : null);
            }
            int extra = state.Queue.Count - shown;
            SetShown(queueMore, extra > 0);
            if (extra > 0) SetText(queueMore, Strings.Format("hud.queue.more", extra));
            SetShown(queue, state.Queue.Count > 0 && !state.Paused && !state.Planning);
        }

        internal static Color ArchetypeColor(string id)
        {
            switch (id)
            {
                case CustomerArchetypes.Casual: return new Color(0.31f, 0.66f, 0.75f);
                case CustomerArchetypes.Bargain: return new Color(0.98f, 0.78f, 0.37f);
                case CustomerArchetypes.Collector: return new Color(0.75f, 0.66f, 0.85f);
                case CustomerArchetypes.Premium: return new Color(0.94f, 0.48f, 0.35f);
                default: return new Color(0.85f, 0.86f, 0.8f);
            }
        }

        // ---------- toasts and discovery ----------

        Toast MakeToast(string name, bool error)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList("es-toast"); element.AddToClassList(ItemClass);
            element.EnableInClassList("es-toast--error", error);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("es-toast__icon");
            var text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("es-toast__text");
            element.Add(icon); element.Add(text);
            element.style.display = DisplayStyle.None;
            toastLayer.Add(element);
            return new Toast { Element = element, Text = text, Icon = icon };
        }

        readonly List<Toast> wantedToasts = new List<Toast>();
        readonly List<float> wantedHeights = new List<float>();
        float stampHeight = 190f;

        void UpdateTopColumn(float now)
        {
            pingTracker.Update(state.Pings, startedPings);
            foreach (var ping in startedPings)
            {
                Toast slot = null;
                foreach (var t in pingToasts) if (t.Key == ping.Key) { slot = t; break; }
                if (slot == null) { slot = pingToasts[0]; foreach (var t in pingToasts) if (t.Started < slot.Started) slot = t; }
                // Its time starts when it first shows, so a toast held back for room is not lost.
                slot.Key = ping.Key; slot.Pending = true; slot.Started = now; slot.Until = float.MaxValue;
                SetText(slot.Text, ping.Text);
                var tex = Shop.HudIcon(ShopUiTheme.Symbol.Flask);
                if (tex) slot.Icon.style.backgroundImage = new StyleBackground(tex);
            }
            bool stamp = !string.IsNullOrEmpty(state.DiscoveryName);
            SetShown(discovery, stamp);
            // UX-08: the stamp comes down onto the HUD as it appears, with the first-hatch sound the lab already plays.
            if (stamp && !stampDropped && !state.Planning) { StampDrops++; Feel?.Motion.Play(discovery, UiMotionKind.StampDown); }
            // It drops once per reveal: hidden by an open panel it comes back as it was; only a reveal that ended drops again.
            if (stamp && !state.Planning) stampDropped = true;
            else if (!stamp && !state.PanelOpen) stampDropped = false;
            if (stamp)
            {
                SetText(discoveryName, state.DiscoveryName);
                BuildRecipe();
                if (discovery.layout.height > 1 && !float.IsNaN(discovery.layout.height)) stampHeight = discovery.layout.height;
            }

            if (state.Planning)
            {
                // UX-06: the notebook's footer shows the notice and save error itself; pings wait until it closes.
                foreach (var t in pingToasts) SetShown(t.Element, false);
                SetShown(saveToast.Element, false); SetShown(noticeToast.Element, false); SetShown(discovery, false);
                ToastsShown = 0; ToastsHeldBack = 0;
                return;
            }
            // Priority: a save error, then the notice, then pings, newest first; as many as fit above the
            // crosshair area under the stamp (a short ultrawide panel holds fewer).
            wantedToasts.Clear(); wantedHeights.Clear();
            if (!string.IsNullOrEmpty(state.SaveError)) Want(saveToast, state.SaveError, ShopUiTheme.Symbol.Flask);
            if (!string.IsNullOrEmpty(state.Notice)) Want(noticeToast, state.Notice, ShopUiTheme.Symbol.Leaf);
            for (int pass = 0; pass < pingToasts.Count; pass++)
            {
                Toast newest = null;
                foreach (var t in pingToasts)
                    if (t.Key != null && now < t.Until && pingTracker.IsActive(t.Key) && !wantedToasts.Contains(t) && (newest == null || t.Started > newest.Started)) newest = t;
                if (newest == null) break;
                wantedToasts.Add(newest); wantedHeights.Add(ToastHeight(newest));
            }
            float panelHeight = Element.layout.height > 1 && !float.IsNaN(Element.layout.height) ? Element.layout.height : ShopUiRoot.ReferenceResolution.y;
            float limit = ShopHudRules.ColumnLimit(panelHeight, TopColumnTop, 16f);
            float used = stamp ? stampHeight + StampRotationAllowance + 6f : 0f;
            int fit = Mathf.Min(ShopHudRules.MaxToasts, ShopHudRules.ToastsThatFit(limit, used, wantedHeights, ToastGap));
            ToastsShown = 0;
            foreach (var t in pingToasts) if (!wantedToasts.Contains(t)) SetShown(t.Element, false);
            if (!wantedToasts.Contains(saveToast)) SetShown(saveToast.Element, false);
            if (!wantedToasts.Contains(noticeToast)) SetShown(noticeToast.Element, false);
            for (int i = 0; i < wantedToasts.Count; i++)
            {
                var t = wantedToasts[i];
                bool on = i < fit;
                SetShown(t.Element, on);
                if (!on) continue;
                ToastsShown++;
                if (t.Pending) { t.Pending = false; t.Until = now + ShopHudRules.ToastSeconds; }
            }
            ToastsHeldBack = wantedToasts.Count - Mathf.Min(fit, wantedToasts.Count);
        }

        const float TopColumnTop = 28f, ToastGap = 10f, StampRotationAllowance = 16f;

        float ToastHeight(Toast toast)
        {
            float h = toast.Element.layout.height;
            if (h > 1 && !float.IsNaN(h)) toast.Height = h;
            return toast.Height;
        }

        void Want(Toast toast, string text, ShopUiTheme.Symbol symbol)
        {
            ShowToastText(toast, text, symbol);
            wantedToasts.Add(toast); wantedHeights.Add(ToastHeight(toast));
        }

        void ShowToastText(Toast toast, string text, ShopUiTheme.Symbol symbol)
        {
            SetText(toast.Text, text);
            if (toast.Icon.style.backgroundImage.value.texture == null)
            {
                var tex = Shop.HudIcon(symbol);
                if (tex) toast.Icon.style.backgroundImage = new StyleBackground(tex);
            }
        }

        int recipeKey = -1;
        void BuildRecipe()
        {
            int key = state.RecipeBloom * 1000 + state.RecipeFlow * 100 + state.RecipeSpark * 10 + state.RecipeMist;
            if (key == recipeKey && recipe.childCount > 0) return;
            recipeKey = key;
            recipe.Clear();
            AddPip(ShopUiTheme.Symbol.Bloom, state.RecipeBloom);
            AddPip(ShopUiTheme.Symbol.Flow, state.RecipeFlow);
            AddPip(ShopUiTheme.Symbol.Spark, state.RecipeSpark);
            AddPip(ShopUiTheme.Symbol.Mist, state.RecipeMist);
        }

        void AddPip(ShopUiTheme.Symbol symbol, int count)
        {
            if (count <= 0) return;
            var pip = new VisualElement { pickingMode = PickingMode.Ignore };
            pip.AddToClassList("es-pip");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("es-pip__icon");
            var tex = Shop.HudIcon(symbol);
            if (tex) icon.style.backgroundImage = new StyleBackground(tex);
            var label = new Label("×" + count) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("es-pip__count");
            pip.Add(icon); pip.Add(label);
            recipe.Add(pip);
        }

        // ---------- crosshair, prompts and next step ----------

        void UpdatePrompts()
        {
            SetShown(crosshair, state.Gameplay);
            var p = state.Prompt;
            SetShown(prompt, p.Shown);
            if (p.Shown)
            {
                SetText(promptKey, p.Key);
                SetShown(promptKey2, !string.IsNullOrEmpty(p.SecondKey));
                if (!string.IsNullOrEmpty(p.SecondKey)) SetText(promptKey2, p.SecondKey);
                SetText(promptName, p.Name);
                SetShown(promptStatus, !string.IsNullOrEmpty(p.Status));
                if (!string.IsNullOrEmpty(p.Status)) SetText(promptStatus, "· " + p.Status);
                float width = Mathf.Clamp01(p.Progress) * 100f;
                if (!Mathf.Approximately(promptProgress.style.width.value.value, width)) promptProgress.style.width = Length.Percent(width);
            }
            var s = state.SlapPrompt;
            SetShown(slap, s.Shown);
            if (s.Shown)
            {
                SetText(slapKey, s.Key);
                SetText(slapName, s.Name);
                SetText(slapStatus, "· " + s.Status);
            }
        }

        void UpdateNextStep(float now, bool f1)
        {
            string text = state.Objective ?? "";
            if (text != objective) { objective = text; objectiveSince = now; }
            if (f1) objectiveSince = now;
            bool show = !state.PanelOpen && !state.Placement && !string.IsNullOrEmpty(text) && ShopHudRules.NextStepVisible(now, objectiveSince, state.Tutorial);
            SetText(nextText, text);
            SetShown(nextHelp, state.Tutorial);
            SetShown(nextCard.parent, !state.PanelOpen && !state.Placement);
            float opacity = show ? 1f : 0f;
            if (!Mathf.Approximately(nextCard.style.opacity.value, opacity)) nextCard.style.opacity = opacity;
            NextStepShown = show;
        }

        // ---------- world-anchored pieces ----------

        void LayoutWorld()
        {
            var panelRef = Element.panel;
            var camera = Shop.Player != null ? Shop.Player.View : Camera.main;
            Vector2 panel = Element.layout.size;
            BubbleRect = MarkerRect = new Rect(float.NaN, float.NaN, 0, 0);
            ArrowsShown = 0;
            bool ready = panelRef != null && camera != null && panel.x > 0 && panel.y > 0 && !float.IsNaN(panel.x);
            if (!ready || !state.Gameplay)
            {
                SetShown(bubble, false); SetShown(marker, false); HideArrows(0);
                return;
            }
            var bounds = new Rect(Margin, Margin, panel.x - Margin * 2, panel.y - Margin * 2);
            CollectOccupied(panel);

            // Caption bubble: above the speaker first, then beside or below it; never on the crosshair.
            bool bubbleWanted = !string.IsNullOrEmpty(state.Caption);
            SetShown(bubble, bubbleWanted);
            if (bubbleWanted)
            {
                SetText(bubbleText, state.Caption);
                Vector2 size = MeasuredSize(bubble, new Vector2(440, 110));
                Rect placed;
                bool ok = false;
                if (state.HasSpeaker && Project(panelRef, camera, state.SpeakerWorld, panel, out Vector2 point))
                    ok = TryPlace(point, size, bounds, BubbleSlots, out placed);
                else placed = default;
                if (!ok) ok = TryFallback(size, bounds, panel, out placed);
                Position(bubble, placed, ok);
                if (ok) { occupied.Add(placed); BubbleRect = placed; }
            }

            // Tutorial marker beside its target, as the IMGUI marker did; hidden rather than overlapping.
            bool markerWanted = state.HasMarker;
            SetShown(marker, markerWanted);
            if (markerWanted)
            {
                SetText(markerTitle, state.MarkerTitle);
                SetText(markerAction, state.MarkerAction);
                Vector2 size = MeasuredSize(marker, new Vector2(320, 92));
                Rect placed = default;
                bool ok = Project(panelRef, camera, state.MarkerWorld, panel, out Vector2 point) &&
                    TryPlace(point, size, bounds, MarkerSlots, out placed);
                Position(marker, placed, ok);
                if (ok) { occupied.Add(placed); MarkerRect = placed; }
            }

            // Edge arrows for pings whose source is off-screen.
            int used = 0;
            var safe = new Rect(70, 230, panel.x - 140, panel.y - 230 - 170);
            foreach (var ping in state.Pings)
            {
                bool behind;
                Vector2 point = ProjectRaw(panelRef, camera, ping.World, out behind);
                if (!ShopHudRules.OffScreen(point, panel, behind)) continue;
                Vector2 edge = ShopHudRules.EdgePoint(panel, point, behind, safe, out float angle);
                var arrow = ArrowAt(used);
                Vector2 size = new Vector2(132, 104);
                Rect rect = new Rect(edge.x - size.x * 0.5f, edge.y - 32f, size.x, size.y);
                bool ok = false;
                for (int step = 0; step < 6 && !ok; step++)
                {
                    float shift = (step % 2 == 0 ? 1 : -1) * ((step + 1) / 2) * (size.y + 8);
                    var candidate = new Rect(rect.x, rect.y + shift, size.x, size.y);
                    if (ShopHudRules.Free(candidate, bounds, occupied)) { rect = candidate; ok = true; }
                }
                if (!ok) continue;
                // Drawn along the direction itself, so no rotation convention is involved.
                arrow.Glyph.Direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                LastArrowDirection = arrow.Glyph.Direction;
                SetText(arrow.Label, ping.Short);
                Position(arrow.Element, rect, true);
                occupied.Add(rect);
                used++;
            }
            HideArrows(used);
            ArrowsShown = used;
        }

        void CollectOccupied(Vector2 panel)
        {
            occupied.Clear();
            occupied.Add(ShopHudRules.CrosshairArea(panel));
            AddIfVisible(day); AddIfVisible(money); AddIfVisible(queue); AddIfVisible(prompt); AddIfVisible(slap);
            AddIfVisible(discovery); AddIfVisible(saveToast.Element); AddIfVisible(noticeToast.Element);
            foreach (var t in pingToasts) AddIfVisible(t.Element);
            foreach (var f in floaters) AddIfVisible(f.Element);
            if (NextStepShown || state.Tutorial) AddIfVisible(nextCard);
        }

        void AddIfVisible(VisualElement e)
        {
            if (!Visible(e)) return;
            Rect r = e.worldBound;
            if (r.width > 0 && r.height > 0) occupied.Add(Inflate(r, 8f));
        }

        static Rect Inflate(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + by * 2, r.height + by * 2);

        bool TryPlace(Vector2 point, Vector2 size, Rect bounds, int[] order, out Rect placed)
        {
            // Close to the point first; a target at the crosshair needs the wider gap to clear its square.
            foreach (float gap in Gaps)
                foreach (int slot in order)
                {
                    placed = ShopHudRules.Slot(slot, point, size, gap);
                    if (ShopHudRules.Free(placed, bounds, occupied)) return true;
                }
            placed = default;
            return false;
        }

        bool TryFallback(Vector2 size, Rect bounds, Vector2 panel, out Rect placed)
        {
            // Under the top-centre column, then lower on the left side, away from the crosshair.
            float top = Margin;
            foreach (var r in occupied) if (r.center.x > panel.x * 0.3f && r.center.x < panel.x * 0.7f && r.yMax < panel.y * 0.45f) top = Mathf.Max(top, r.yMax);
            placed = new Rect((panel.x - size.x) * 0.5f, top + 12f, size.x, size.y);
            if (ShopHudRules.Free(placed, bounds, occupied)) return true;
            placed = new Rect(Margin + 12f, panel.y * 0.5f - size.y * 0.5f, size.x, size.y);
            return ShopHudRules.Free(placed, bounds, occupied);
        }

        static bool Project(IPanel panelRef, Camera camera, Vector3 world, Vector2 panel, out Vector2 point)
        {
            point = ProjectRaw(panelRef, camera, world, out bool behind);
            return !ShopHudRules.OffScreen(point, panel, behind);
        }

        static Vector2 ProjectRaw(IPanel panelRef, Camera camera, Vector3 world, out bool behind)
        {
            Vector3 view = camera.WorldToViewportPoint(world);
            behind = view.z <= 0;
            if (behind)
            {
                // Mirror through the camera so the direction from the centre stays meaningful.
                Vector3 mirrored = camera.transform.position - (world - camera.transform.position);
                return RuntimePanelUtils.CameraTransformWorldToPanel(panelRef, mirrored, camera);
            }
            return RuntimePanelUtils.CameraTransformWorldToPanel(panelRef, world, camera);
        }

        static Vector2 MeasuredSize(VisualElement e, Vector2 fallback)
        {
            Vector2 size = e.layout.size;
            return size.x > 1 && size.y > 1 && !float.IsNaN(size.x) ? size : fallback;
        }

        static void Position(VisualElement e, Rect rect, bool ok)
        {
            if (!ok) { e.style.opacity = 0; return; }
            e.style.left = rect.x; e.style.top = rect.y;
            e.style.opacity = 1;
        }

        Arrow ArrowAt(int index)
        {
            while (arrows.Count <= index)
            {
                var element = new VisualElement { name = "es-hud-arrow", pickingMode = PickingMode.Ignore };
                element.AddToClassList("es-arrow"); element.AddToClassList(ItemClass);
                var disc = new VisualElement { pickingMode = PickingMode.Ignore };
                disc.AddToClassList("es-arrow__disc");
                var glyph = new HudArrowGlyph();
                disc.Add(glyph);
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("es-arrow__label");
                element.Add(disc); element.Add(label);
                arrowLayer.Add(element);
                arrows.Add(new Arrow { Element = element, Glyph = glyph, Label = label });
            }
            SetShown(arrows[index].Element, true);
            return arrows[index];
        }

        void HideArrows(int from) { for (int i = from; i < arrows.Count; i++) SetShown(arrows[i].Element, false); }

        // ---------- helpers ----------

        void EnsureIcons()
        {
            if (iconsReady) return;
            var coin = Shop.HudIcon(ShopUiTheme.Symbol.Coin);
            var leaf = Shop.HudIcon(ShopUiTheme.Symbol.Leaf);
            if (!coin || !leaf) return;
            moneyIcon.style.backgroundImage = new StyleBackground(coin);
            nextIcon.style.backgroundImage = new StyleBackground(leaf);
            iconsReady = true;
        }

        /// <summary>True when an element is displayed and not faded out, up to the HUD root.</summary>
        public static bool Visible(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
            {
                if (v.resolvedStyle.display == DisplayStyle.None || v.resolvedStyle.visibility == Visibility.Hidden) return false;
                if (v.resolvedStyle.opacity < 0.05f) return false;
                if (v.ClassListContains("es-hud") && v.name == RootName) break;
            }
            return e.panel != null;
        }

        static void SetText(Label label, string text)
        {
            text = text ?? "";
            if (label.text != text) label.text = text;
        }

        static void SetShown(VisualElement e, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display.keyword != StyleKeyword.Undefined || e.style.display.value != display) e.style.display = display;
        }

        static void IgnorePicking(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            foreach (var child in e.Children()) IgnorePicking(child);
        }
    }
}
