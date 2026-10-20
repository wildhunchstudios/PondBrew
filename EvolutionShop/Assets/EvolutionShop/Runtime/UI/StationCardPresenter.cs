using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-02 world-anchored station card frame. It follows ShopSession.Panel: it appears when a station panel
    /// opens, sits beside the station and closes through the session's ordinary SetPanel(None). Station
    /// controls move into it in UX-04/UX-05; until then it is a development preview.
    /// </summary>
    public sealed class StationCardPresenter : ShopUiPresenter
    {
        public const string CardName = "es-station-card";
        public const string StripName = "es-card-strip";
        public const string TitleName = "es-card-title";
        public const string StatusName = "es-card-status";
        public const string BodyName = "es-card-body";
        public const string CloseName = "es-card-close";
        public const string KeycapName = "es-card-keycap";
        public const string HintName = "es-card-hint";
        public const string ShopStripClass = "es-card__strip--shop";

        VisualElement strip;
        Label title, status, body, keycap, hint;
        Button close;
        int shownFrame = -1;

        public Station AnchorStation { get; private set; }
        public ShopPanel ShownPanel { get; private set; }
        public ShopUiCardPlacement Placement { get; private set; }
        public Vector2 AnchorPanelPoint { get; private set; }
        public string Title => title != null ? title.text : "";
        public bool ShopStyled => strip != null && strip.ClassListContains(ShopStripClass);

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
            keycap = Element.Q<Label>(KeycapName);
            hint = Element.Q<Label>(HintName);
            close = Element.Q<Button>(CloseName);
            if (close != null)
            {
                close.text = Strings.Get("card.close");
                close.clicked += CloseCard;
            }
            if (keycap != null) keycap.text = Strings.Get("card.keycap.back");
            if (hint != null) hint.text = Strings.Get("card.hint");
        }

        void CloseCard()
        {
            if (Shop != null && IsShown && Handles(Shop.Panel)) Shop.SetPanel(ShopPanel.None);
        }

        public override void Refresh()
        {
            if (Element == null || Shop == null) return;
            var root = Shop.ToolkitUi;
            ShopPanel panel = Shop.Panel;
            if (root == null || !root.Active || !Handles(panel)) { if (IsShown) Hide(); return; }
            if (!IsShown || panel != ShownPanel)
            {
                AnchorStation = Shop.ToolkitAnchorFor(panel);
                ShownPanel = panel;
                string fallback = Strings.Get("card.title." + panel);
                if (title != null) title.text = AnchorStation && !string.IsNullOrEmpty(AnchorStation.Label) ? AnchorStation.Label : fallback;
                if (status != null) status.text = Strings.Get("card.status.preview");
                if (body != null) body.text = Strings.Get("card.preview");
                strip?.EnableInClassList(ShopStripClass, IsShopPanel(panel));
                shownFrame = Time.frameCount;
                Show();
            }
            Position();
            // E toggles the card closed like the station prompt opened it; the opening press is ignored.
            var keyboard = Keyboard.current;
            if (Time.frameCount > shownFrame && keyboard != null && keyboard.eKey.wasPressedThisFrame) CloseCard();
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

        public override void Unbind()
        {
            if (close != null) close.clicked -= CloseCard;
            base.Unbind();
        }
    }
}
