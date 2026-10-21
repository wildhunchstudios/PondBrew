using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// World-anchored station card (UX-02 frame, UX-04/UX-05 content). It follows ShopSession.Panel: it appears when
    /// a station panel opens, sits beside the station and closes through the session's ordinary SetPanel(None).
    /// Every station panel has a view: incubator, display tank and counter (StationCardViews.cs), and specimen rack,
    /// conditioning vat and research bench (StationCardLabViews.cs). Each card has at most one primary button, the
    /// frame's own; views decide what it says and does.
    /// </summary>
    public sealed class StationCardPresenter : ShopUiPresenter
    {
        public const string CardName = "es-station-card";
        public const string StripName = "es-card-strip";
        public const string TitleName = "es-card-title";
        public const string StatusName = "es-card-status";
        public const string BodyName = "es-card-body";
        public const string ViewSlotName = "es-card-view";
        public const string AfterSlotName = "es-card-after";
        public const string ReasonName = "es-card-reason";
        public const string PrimaryName = "es-card-primary";
        public const string CloseName = "es-card-close";
        public const string KeycapName = "es-card-keycap";
        public const string HintName = "es-card-hint";
        public const string CalloutName = "es-card-callout";
        public const string ShopStripClass = "es-card__strip--shop";
        public const string TagCardClass = "es-card--tag";
        public const string ClipboardCardClass = "es-card--clipboard";
        public const string PrimaryClass = "es-button--primary";
        public const string ReasonFlashClass = "es-card__reason--flash";

        VisualElement strip, viewSlot, afterSlot;
        Label title, status, body, reason, keycap, hint, callout;
        Button close, primary;
        int shownFrame = -1;
        readonly Dictionary<ShopPanel, StationCardView> views = new Dictionary<ShopPanel, StationCardView>();
        readonly List<KeyValuePair<VisualElement, PickingMode>> closingPicking = new List<KeyValuePair<VisualElement, PickingMode>>();

        public Station AnchorStation { get; private set; }
        public ShopPanel ShownPanel { get; private set; }
        public ShopUiCardPlacement Placement { get; private set; }
        public Vector2 AnchorPanelPoint { get; private set; }
        public string Title => title != null ? title.text : "";
        /// <summary>The tutorial callout showing at the top of the card (UX-07), or empty.</summary>
        public string Callout => callout != null && callout.style.display != DisplayStyle.None ? callout.text : "";
        public bool ShopStyled => strip != null && strip.ClassListContains(ShopStripClass);
        /// <summary>The content view for the shown panel; null for stations still on the placeholder.</summary>
        public StationCardView CurrentView { get; private set; }
        public IncubatorCardView IncubatorView => views.TryGetValue(ShopPanel.Incubator, out var v) ? (IncubatorCardView)v : null;
        public TankCardView TankView => views.TryGetValue(ShopPanel.Tank, out var v) ? (TankCardView)v : null;
        public CounterCardView CounterView => views.TryGetValue(ShopPanel.Counter, out var v) ? (CounterCardView)v : null;
        public RackCardView RackView => views.TryGetValue(ShopPanel.StorageRack, out var v) ? (RackCardView)v : null;
        public VatCardView VatView => views.TryGetValue(ShopPanel.Conditioning, out var v) ? (VatCardView)v : null;
        public BenchCardView BenchView => views.TryGetValue(ShopPanel.Upgrade, out var v) ? (BenchCardView)v : null;
        /// <summary>UX-08: the card is closed (not shown) but still drawn for its 80 ms fade, taking no clicks.</summary>
        public bool Closing { get; private set; }
        public int Opens { get; private set; }
        public int ReasonFlashes { get; private set; }
        /// <summary>The primary button's rectangle while the card shows it; NaN otherwise (where a sale's coins start).</summary>
        public Rect PrimaryRect => IsShown && primary != null && HudPresenter.Visible(primary) ? primary.worldBound : new Rect(float.NaN, float.NaN, 0, 0);

        /// <summary>Panels that belong to a physical station and therefore use a world-anchored card.</summary>
        public static bool Handles(ShopPanel panel)
        {
            switch (panel)
            {
                case ShopPanel.Incubator: case ShopPanel.Tank: case ShopPanel.Counter:
                case ShopPanel.Upgrade: case ShopPanel.StorageRack: case ShopPanel.Conditioning:
                    return true;
                default: return false;
            }
        }

        /// <summary>Front-shop stations use the paper/kraft strip; lab stations use the teal enamel strip.</summary>
        public static bool IsShopPanel(ShopPanel panel) => panel == ShopPanel.Tank || panel == ShopPanel.Counter;

        protected override VisualElement Build(VisualTreeAsset template) => Extract(template, CardName);

        protected override void OnBound()
        {
            strip = Element.Q<VisualElement>(StripName);
            title = Element.Q<Label>(TitleName);
            status = Element.Q<Label>(StatusName);
            body = Element.Q<Label>(BodyName);
            viewSlot = Element.Q<VisualElement>(ViewSlotName);
            afterSlot = Element.Q<VisualElement>(AfterSlotName);
            reason = Element.Q<Label>(ReasonName);
            keycap = Element.Q<Label>(KeycapName);
            hint = Element.Q<Label>(HintName);
            close = Element.Q<Button>(CloseName);
            primary = Element.Q<Button>(PrimaryName);
            if (close != null)
            {
                close.text = Strings.Get("card.close");
                close.clicked += CloseCard;
            }
            if (primary != null) primary.clicked += PressPrimary;
            if (keycap != null) keycap.text = Strings.Get("card.keycap.back");
            if (hint != null) hint.text = Strings.Get("card.hint");
            // UX-07: the guided opening's callout for this station, in the cards' own words, at the top of the content.
            var content = Element.Q<VisualElement>("es-card-content");
            if (content != null)
            {
                callout = new Label { name = CalloutName };
                callout.AddToClassList("es-card__callout");
                content.Insert(0, callout);
            }
            if (viewSlot != null && afterSlot != null)
            {
                AddView(ShopPanel.Incubator, new IncubatorCardView(Shop, Strings));
                AddView(ShopPanel.Tank, new TankCardView(Shop, Strings));
                AddView(ShopPanel.Counter, new CounterCardView(Shop, Strings));
                AddView(ShopPanel.StorageRack, new RackCardView(Shop, Strings));
                AddView(ShopPanel.Conditioning, new VatCardView(Shop, Strings));
                AddView(ShopPanel.Upgrade, new BenchCardView(Shop, Strings));
            }
        }

        void AddView(ShopPanel panel, StationCardView view)
        {
            views[panel] = view;
            viewSlot.Add(view.Root);
            afterSlot.Add(view.After);
            SetShown(view.Root, false);
            SetShown(view.After, false);
        }

        void CloseCard()
        {
            if (Shop != null && IsShown && Handles(Shop.Panel)) Shop.SetPanel(ShopPanel.None);
        }

        void PressPrimary()
        {
            if (Shop == null || !IsShown || CurrentView == null || !Handles(Shop.Panel)) return;
            CurrentView.Primary();
        }

        public override void Refresh()
        {
            if (Element == null || Shop == null) return;
            var root = Shop.ToolkitUi;
            ShopPanel panel = Shop.Panel;
            if (root == null || !root.Active || !Handles(panel))
            {
                if (IsShown) { if (root != null && root.Active) BeginClose(); else Hide(); }
                return;
            }
            if (!IsShown || panel != ShownPanel)
            {
                EndClose();
                AnchorStation = Shop.ToolkitAnchorFor(panel);
                ShownPanel = panel;
                string fallback = Strings.Get("card.title." + panel);
                if (title != null) title.text = AnchorStation && !string.IsNullOrEmpty(AnchorStation.Label) ? AnchorStation.Label : fallback;
                strip?.EnableInClassList(ShopStripClass, IsShopPanel(panel));
                Element.EnableInClassList(TagCardClass, panel == ShopPanel.Tank);
                Element.EnableInClassList(ClipboardCardClass, panel == ShopPanel.Counter);
                views.TryGetValue(panel, out var view);
                foreach (var pair in views)
                {
                    SetShown(pair.Value.Root, pair.Value == view);
                    SetShown(pair.Value.After, pair.Value == view);
                }
                CurrentView = view;
                view?.Opened();
                shownFrame = Time.frameCount;
                Show();
                Opens++;
                // UX-08: the card opens with a slight overshoot and a soft paper (shop) or enamel (lab) click.
                var feel = Feel;
                if (feel != null)
                {
                    feel.Motion.Play(Element, UiMotionKind.CardOpen);
                    feel.Sounds.Play(IsShopPanel(panel) ? UiSound.CardPaper : UiSound.CardEnamel);
                }
            }
            RefreshContent();
            // A view may name the card itself: a tank names its slot (the session's selected tank, not the last station
            // used) and the bench uses the string table, since its world label carries " / ".
            string viewTitle = CurrentView != null ? CurrentView.Title : null;
            if (title != null && !string.IsNullOrEmpty(viewTitle) && title.text != viewTitle) title.text = viewTitle;
            Position();
            // E toggles the card closed like the station prompt opened it; the opening press is ignored.
            var keyboard = Keyboard.current;
            if (Time.frameCount > shownFrame && keyboard != null && keyboard.eKey.wasPressedThisFrame) CloseCard();
        }

        void RefreshContent()
        {
            string tip = Shop.TutorialLocalCallout;
            SetText(callout, tip);
            SetShown(callout, !string.IsNullOrEmpty(tip));
            var view = CurrentView;
            if (view == null)
            {
                // Every station panel has a view since UX-05; a panel without one shows the frame alone.
                SetShown(status, false);
                SetShown(body, false);
                SetShown(primary, false);
                SetShown(reason, false);
                return;
            }
            view.FillIcons();
            view.Refresh();
            SetText(status, view.Status);
            SetShown(status, !string.IsNullOrEmpty(view.Status));
            SetText(body, view.Line);
            SetShown(body, !string.IsNullOrEmpty(view.Line));
            bool hasPrimary = !string.IsNullOrEmpty(view.PrimaryText);
            SetShown(primary, hasPrimary);
            if (hasPrimary)
            {
                SetText(primary, view.PrimaryText);
                if (primary.enabledSelf != view.PrimaryEnabled) primary.SetEnabled(view.PrimaryEnabled);
                // A press on the disabled primary flashes the reason line (UX-08); the tooltip carries it for the record.
                string why = view.PrimaryEnabled ? "" : view.Reason ?? "";
                if (primary.tooltip != why) primary.tooltip = why;
            }
            bool hasReason = !string.IsNullOrEmpty(view.Reason);
            SetShown(reason, hasReason);
            if (hasReason)
            {
                SetText(reason, view.Reason);
                reason.EnableInClassList("es-card__reason--warn", view.ReasonIsWarning);
            }
        }

        static void SetText(TextElement element, string text)
        {
            if (element == null) return;
            text = text ?? "";
            if (element.text != text) element.text = text;
        }

        static void SetShown(VisualElement element, bool shown)
        {
            if (element == null) return;
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.keyword != StyleKeyword.Undefined || element.style.display.value != display) element.style.display = display;
        }

        void Position()
        {
            var panelRef = Element.panel;
            var parent = Element.parent;
            var camera = Shop.Player != null ? Shop.Player.View : Camera.main;
            if (panelRef == null || parent == null || camera == null) return;
            Vector2 panelSize = parent.layout.size;
            if (panelSize.x <= 0 || panelSize.y <= 0 || float.IsNaN(panelSize.x)) return;
            Vector3 world = ShopUiLayout.AnchorPoint(AnchorStation);
            bool hidden = !AnchorStation || camera.WorldToViewportPoint(world).z <= 0;
            Vector2 anchor = hidden ? new Vector2(float.NaN, float.NaN) : RuntimePanelUtils.CameraTransformWorldToPanel(panelRef, world, camera);
            Vector2 size = new Vector2(Element.layout.width, Element.layout.height);
            var placement = ShopUiLayout.PlaceBeside(anchor, size, panelSize, anchorHidden: hidden);
            Element.style.left = placement.Position.x;
            Element.style.top = placement.Position.y;
            Placement = placement;
            AnchorPanelPoint = anchor;
        }

        /// <summary>
        /// A press on the disabled primary: its reason line, already above it, flashes. False when the button isn't this card's
        /// primary or there is no reason showing, so the caller can show the button's own tip.
        /// </summary>
        public bool FlashReason(Button button)
        {
            if (button == null || button != primary || !IsShown || reason == null || !HudPresenter.Visible(reason)) return false;
            ReasonFlashes++;
            reason.AddToClassList(ReasonFlashClass);
            var feel = Feel;
            if (feel != null) feel.Motion.Play(reason, UiMotionKind.ReasonFlash, () => reason?.RemoveFromClassList(ReasonFlashClass));
            else reason.RemoveFromClassList(ReasonFlashClass);
            return true;
        }

        // UX-08: closing fades the card out over 80 ms. It is closed at once (IsShown is false, the session has moved on) and
        // nothing in it takes a click while it fades, so the next command is never held up or caught by the old card.
        void BeginClose()
        {
            MarkHidden();
            var feel = Feel;
            if (feel == null) { Hide(); return; }
            Closing = true;
            closingPicking.Clear();
            foreach (var e in Element.Query<VisualElement>().ToList())
            {
                closingPicking.Add(new KeyValuePair<VisualElement, PickingMode>(e, e.pickingMode));
                e.pickingMode = PickingMode.Ignore;
            }
            feel.Motion.Play(Element, UiMotionKind.CardClose, () => { if (Closing) Hide(); });
        }

        void EndClose()
        {
            if (!Closing) return;
            Closing = false;
            foreach (var pair in closingPicking) if (pair.Key != null) pair.Key.pickingMode = pair.Value;
            closingPicking.Clear();
            Feel?.Motion.Stop(Element);
        }

        protected override void OnHidden() => EndClose();

        public override void Unbind()
        {
            EndClose();
            if (close != null) close.clicked -= CloseCard;
            if (primary != null) primary.clicked -= PressPrimary;
            views.Clear();
            CurrentView = null;
            base.Unbind();
        }
    }
}
