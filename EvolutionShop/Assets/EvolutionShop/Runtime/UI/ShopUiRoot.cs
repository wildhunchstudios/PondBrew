using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-02 UI Toolkit root. Owns a runtime PanelSettings (1920x1080 reference, scale with screen, match 0.5),
    /// the shared theme and tokens, and the presenters. It is presentation only. During migration it exists
    /// in Editor/development builds as an opt-in preview: -evolutionToolkitUi or F9 turns it on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopUiRoot : MonoBehaviour
    {
        public const string ResourceFolder = "EvolutionShopUI/";
        public const string ThemeResource = ResourceFolder + "ShopTheme";
        public const string TokensResource = ResourceFolder + "ShopTokens";
        public const string StationCardResource = ResourceFolder + "StationCard";
        public const string HudResource = ResourceFolder + "Hud";
        public const string HudStylesResource = ResourceFolder + "HudStyles";
        public const string CardStylesResource = ResourceFolder + "CardStyles";
        public const string PlanningStylesResource = ResourceFolder + "PlanningStyles";
        public const string ScreenStylesResource = ResourceFolder + "ScreenStyles";
        public const string LayerName = "es-layer";
        public const string HudLayerName = "es-hud-layer";
        public const string PlanningLayerName = "es-plan-layer";
        public const string ScreenLayerName = "es-screen-layer";
        public const string FeelLayerName = "es-feel-layer";
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
        public const float ScreenMatch = 0.5f;
        public const int SortingOrder = 100;

        ShopSession shop;
        PanelSettings settings;
        UIDocument document;
        GameObject documentObject;

        public bool Ready { get; private set; }
        public bool Active { get; private set; }
        public string LoadProblem { get; private set; }
        public ShopUiStrings Strings { get; private set; } = ShopUiStrings.Empty;
        public StationCardPresenter StationCard { get; private set; }
        public HudPresenter Hud { get; private set; }
        public PlanningViewPresenter Planning { get; private set; }
        public ScreenPresenter Screens { get; private set; }
        /// <summary>UX-08 feel: animations, UI sounds, button presses and the disabled-press tip.</summary>
        public UiFeel Feel { get; private set; }
        /// <summary>No feel animation, coin flight, money count or receipt count-up is running (validation waits on this).</summary>
        public bool Settled => Feel == null || (Feel.Motion.Idle && (Hud == null || Hud.FeelSettled) && (Screens == null || Screens.Report == null || !Screens.Report.Counting));
        public VisualElement Root => document != null ? document.rootVisualElement : null;
        public PanelSettings Settings => settings;

        public void Initialize(ShopSession owner, bool active)
        {
            shop = owner;
            Strings = ShopUiStrings.LoadDefault();
            var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
            var tokens = Resources.Load<StyleSheet>(TokensResource);
            var cardTemplate = Resources.Load<VisualTreeAsset>(StationCardResource);
            var hudTemplate = Resources.Load<VisualTreeAsset>(HudResource);
            var hudStyles = Resources.Load<StyleSheet>(HudStylesResource);
            var cardStyles = Resources.Load<StyleSheet>(CardStylesResource);
            var planningStyles = Resources.Load<StyleSheet>(PlanningStylesResource);
            var screenStyles = Resources.Load<StyleSheet>(ScreenStylesResource);
            if (!theme || !tokens || !cardTemplate || !hudTemplate || !hudStyles || !cardStyles || !planningStyles || !screenStyles)
            {
                LoadProblem = "Missing UI Toolkit resource:" + (theme ? "" : " theme") + (tokens ? "" : " tokens") + (cardTemplate ? "" : " station card") +
                    (hudTemplate ? "" : " HUD") + (hudStyles ? "" : " HUD styles") + (cardStyles ? "" : " card styles") + (planningStyles ? "" : " planning styles") + (screenStyles ? "" : " screen styles");
                Debug.LogError(LoadProblem);
                return;
            }
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Evolution Shop UI Toolkit";
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = ScreenMatch;
            settings.sortingOrder = SortingOrder;

            // Assign the panel before the document enables so its root joins the panel exactly once.
            documentObject = new GameObject("Evolution Shop UI Toolkit");
            documentObject.SetActive(false);
            documentObject.transform.SetParent(transform, false);
            document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            documentObject.SetActive(true);

            var root = document.rootVisualElement;
            root.styleSheets.Add(tokens);
            root.styleSheets.Add(hudStyles);
            root.styleSheets.Add(cardStyles);
            root.styleSheets.Add(planningStyles);
            root.styleSheets.Add(screenStyles);
            root.pickingMode = PickingMode.Ignore;
            // The planning notebook comes first: it dims the world, while the HUD's day and money chips stay on top of it.
            var planningLayer = new VisualElement { name = PlanningLayerName, pickingMode = PickingMode.Ignore };
            planningLayer.AddToClassList("es-layer");
            root.Add(planningLayer);
            // UX-07 screens (welcome, help, journal, report, pause) dim the world the same way, under the HUD chips.
            var screenLayer = new VisualElement { name = ScreenLayerName, pickingMode = PickingMode.Ignore };
            screenLayer.AddToClassList("es-layer");
            root.Add(screenLayer);
            // The HUD layer comes first, so station cards draw above it.
            var hudLayer = new VisualElement { name = HudLayerName, pickingMode = PickingMode.Ignore };
            hudLayer.AddToClassList("es-layer");
            root.Add(hudLayer);
            var layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            layer.AddToClassList("es-layer");
            root.Add(layer);
            // UX-08: the disabled-press tip goes over everything, including the station card it explains.
            var feelLayer = new VisualElement { name = FeelLayerName, pickingMode = PickingMode.Ignore };
            feelLayer.AddToClassList("es-layer");
            root.Add(feelLayer);
            Feel = new UiFeel(shop, this, root, feelLayer);

            Hud = new HudPresenter();
            Hud.Bind(shop, hudLayer, hudTemplate, Strings);
            StationCard = new StationCardPresenter();
            StationCard.Bind(shop, layer, cardTemplate, Strings);
            Planning = new PlanningViewPresenter();
            Planning.Bind(shop, planningLayer, null, Strings);
            Screens = new ScreenPresenter();
            Screens.Bind(shop, screenLayer, null, Strings);
            Ready = true;
            SetActive(active);
        }

        public void SetActive(bool active)
        {
            Active = active && Ready;
            if (!Active)
            {
                StationCard?.Hide(); Hud?.Hide(); Planning?.Hide(); Screens?.Hide();
                Feel?.Reset();
                // The research path exists only in the notebook; without it the bench's classic panel holds research.
                if (shop != null && shop.Panel == ShopPanel.Research) shop.SetPanel(ShopPanel.Upgrade);
            }
        }

        /// <summary>True when this root draws the given panel, so the IMGUI path must not.</summary>
        public bool OwnsPanel(ShopPanel panel) => Active && (StationCardPresenter.Handles(panel) || PlanningViewPresenter.Handles(panel) || ScreenPresenter.Handles(panel));

        void LateUpdate()
        {
            if (!Ready || shop == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Validation routes control the preview explicitly; F9 is for hands-on comparison only.
            var keyboard = Keyboard.current;
            if (!shop.ValidationMode && keyboard != null && keyboard.f9Key.wasPressedThisFrame) SetActive(!Active);
#endif
            // The feel clock moves first, so whatever a presenter starts this frame starts at this frame's time.
            Feel.Advance(Time.unscaledDeltaTime);
            Hud.Refresh();
            Planning.Refresh();
            Screens.Refresh();
            StationCard.Refresh();
            Feel.Tick();
        }

        void OnDestroy()
        {
            Hud?.Unbind();
            Hud = null;
            StationCard?.Unbind();
            StationCard = null;
            Planning?.Unbind();
            Planning = null;
            Screens?.Unbind();
            Screens = null;
            Feel?.Unbind();
            Feel = null;
            if (documentObject) Destroy(documentObject);
            if (settings) Destroy(settings);
            Ready = false; Active = false;
        }
    }
}
