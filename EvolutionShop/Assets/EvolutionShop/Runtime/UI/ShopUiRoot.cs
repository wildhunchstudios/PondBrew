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
        public const string LayerName = "es-layer";
        public const string HudLayerName = "es-hud-layer";
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
            if (!theme || !tokens || !cardTemplate || !hudTemplate || !hudStyles)
            {
                LoadProblem = "Missing UI Toolkit resource:" + (theme ? "" : " theme") + (tokens ? "" : " tokens") + (cardTemplate ? "" : " station card") +
                    (hudTemplate ? "" : " HUD") + (hudStyles ? "" : " HUD styles");
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
            root.pickingMode = PickingMode.Ignore;
            // The HUD layer comes first, so station cards draw above it.
            var hudLayer = new VisualElement { name = HudLayerName, pickingMode = PickingMode.Ignore };
            hudLayer.AddToClassList("es-layer");
            root.Add(hudLayer);
            var layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            layer.AddToClassList("es-layer");
            root.Add(layer);

            Hud = new HudPresenter();
            Hud.Bind(shop, hudLayer, hudTemplate, Strings);
            StationCard = new StationCardPresenter();
            StationCard.Bind(shop, layer, cardTemplate, Strings);
            Ready = true;
            SetActive(active);
        }

        public void SetActive(bool active)
        {
            Active = active && Ready;
            if (!Active) { StationCard?.Hide(); Hud?.Hide(); }
        }

        /// <summary>True when this root draws the given panel, so the IMGUI path must not.</summary>
        public bool OwnsPanel(ShopPanel panel) => Active && StationCardPresenter.Handles(panel);

        void LateUpdate()
        {
            if (!Ready || shop == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Validation routes control the preview explicitly; F9 is for hands-on comparison only.
            var keyboard = Keyboard.current;
            if (!shop.ValidationMode && keyboard != null && keyboard.f9Key.wasPressedThisFrame) SetActive(!Active);
#endif
            Hud.Refresh();
            StationCard.Refresh();
        }

        void OnDestroy()
        {
            Hud?.Unbind();
            Hud = null;
            StationCard?.Unbind();
            StationCard = null;
            if (documentObject) Destroy(documentObject);
            if (settings) Destroy(settings);
            Ready = false; Active = false;
        }
    }
}
