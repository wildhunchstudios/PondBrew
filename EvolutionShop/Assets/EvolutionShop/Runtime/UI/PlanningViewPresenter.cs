using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-06 planning notebook (UX.6): crew, automation, arrange and research as full-screen pages over the dimmed world,
    /// with tabs along the top and Esc backing out one level. It follows ShopSession.Panel like the station card: it
    /// shows while a planning panel is open. A tab that can't be used from where the player stands
    /// says where to go instead. While the player places furniture, the notebook steps aside for a key bar.
    /// </summary>
    public sealed class PlanningViewPresenter : ShopUiPresenter
    {
        public const string RootName = "es-plan";
        public const string DimName = "es-plan-dim";
        public const string BookName = "es-plan-book";
        public const string SheetName = "es-plan-sheet";
        public const string TitleName = "es-plan-title";
        public const string SubtitleName = "es-plan-subtitle";
        public const string BackName = "es-plan-back";
        public const string BodyName = "es-plan-body";
        public const string FooterName = "es-plan-footer";
        public const string PlaceBarName = "es-plan-placebar";
        public const string PlaceTitleName = "es-plan-place-title";
        public const string PlaceDetailName = "es-plan-place-detail";
        public const string PrimaryClass = "es-button--primary";
        public static string TabName(PlanningTab tab) => "es-plan-tab-" + tab.ToString().ToLowerInvariant();
        public static string TabWhereName(PlanningTab tab) => TabName(tab) + "-where";

        VisualElement dim, book, sheet, body, placeBar;
        Label title, subtitle, footer, placeTitle, placeDetail;
        ShopPanel lastPage = ShopPanel.None;
        float noticeFloor = float.NegativeInfinity;
        Button back;
        readonly Dictionary<PlanningTab, Button> tabs = new Dictionary<PlanningTab, Button>();
        readonly Dictionary<PlanningTab, Label> tabWhere = new Dictionary<PlanningTab, Label>();
        readonly Dictionary<ShopPanel, PlanningView> views = new Dictionary<ShopPanel, PlanningView>();
        readonly List<PlanningView> allViews = new List<PlanningView>();

        public ShopPanel ShownPanel { get; private set; }
        public PlanningView CurrentView { get; private set; }
        public bool PlacementBarShown { get; private set; }
        public string Footer => footer != null ? footer.text : "";
        public VisualElement Sheet => sheet;
        public VisualElement Book => book;
        public CrewView Crew { get; private set; }
        public StockerTargetsView Targets { get; private set; }
        public TechnicianQueueView Queue { get; private set; }
        public AutomationView Automation { get; private set; }
        public ArrangeView Arrange { get; private set; }
        public ResearchView Research { get; private set; }

        /// <summary>Planning panels this notebook draws.</summary>
        public static bool Handles(ShopPanel panel) => ShopSession.IsPlanningPanel(panel);

        protected override VisualElement Build(VisualTreeAsset template)
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("es-plan");
            return root;
        }

        protected override void OnBound()
        {
            var root = Element;
            dim = Box(root, "es-plan__dim", DimName);
            book = Box(root, "es-plan__book", BookName);
            var tabRow = Box(book, "es-plan__tabs", "es-plan-tabs");
            foreach (PlanningTab tab in Enum.GetValues(typeof(PlanningTab)))
            {
                var t = tab;
                var button = new Button(() => Shop.OpenPlanningTab(t)) { name = TabName(tab) };
                button.AddToClassList("es-button"); button.AddToClassList("es-plan-tab");
                var name = new Label(Strings.Get("plan.tab." + tab.ToString().ToLowerInvariant())) { pickingMode = PickingMode.Ignore, name = TabName(tab) + "-label" };
                name.AddToClassList("es-plan-tab__name");
                var where = new Label(Strings.Get(PlanningRules.TabWhereKey(tab))) { pickingMode = PickingMode.Ignore, name = TabWhereName(tab) };
                where.AddToClassList("es-plan-tab__where");
                button.Add(name); button.Add(where);
                tabRow.Add(button);
                tabs[tab] = button; tabWhere[tab] = where;
            }
            sheet = Box(book, "es-plan__sheet", SheetName);
            var header = Box(sheet, "es-plan__header", "es-plan-header");
            var titles = Box(header, "es-plan__titles", "es-plan-titles");
            title = Text(titles, TitleName, "es-plan__title");
            subtitle = Text(titles, SubtitleName, "es-plan__subtitle");
            back = new Button(() => Shop.BackFromPanel()) { name = BackName };
            back.AddToClassList("es-button"); back.AddToClassList("es-button--secondary"); back.AddToClassList("es-plan__back");
            var cap = new Label(Strings.Get("card.keycap.back")) { pickingMode = PickingMode.Ignore, name = BackName + "-keycap" };
            cap.AddToClassList("es-keycap"); cap.AddToClassList("es-plan__back-key");
            var backText = new Label(Strings.Get("plan.back")) { pickingMode = PickingMode.Ignore, name = BackName + "-text" };
            backText.AddToClassList("es-plan__back-text");
            back.Add(cap); back.Add(backText);
            header.Add(back);
            body = Box(sheet, "es-plan__body", BodyName);
            footer = Text(sheet, FooterName, "es-plan__footer");

            placeBar = Box(root, "es-plan__placebar", PlaceBarName);
            placeBar.pickingMode = PickingMode.Ignore;
            var placeText = Box(placeBar, "es-plan__place-text", "es-plan-place-text");
            placeTitle = Text(placeText, PlaceTitleName, "es-plan__place-title");
            placeDetail = Text(placeText, PlaceDetailName, "es-plan__place-detail");
            var keys = Box(placeBar, "es-plan__place-keys", "es-plan-place-keys");
            Key(keys, "plan.place.key.rotate", "plan.place.rotate");
            Key(keys, "plan.place.key.click", "plan.place.place");
            Key(keys, "card.keycap.back", "plan.place.cancel");
            foreach (var e in placeBar.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;

            Crew = Register(new CrewView(Shop, Strings), ShopPanel.Staff, ShopPanel.Stocker, ShopPanel.Technician);
            Targets = Register(new StockerTargetsView(Shop, Strings), ShopPanel.StockerTargets);
            Queue = Register(new TechnicianQueueView(Shop, Strings), ShopPanel.TechnicianOrders);
            Automation = Register(new AutomationView(Shop, Strings), ShopPanel.Automation);
            Arrange = Register(new ArrangeView(Shop, Strings), ShopPanel.Arrange);
            Research = Register(new ResearchView(Shop, Strings), ShopPanel.Research);
        }

        T Register<T>(T view, params ShopPanel[] panels) where T : PlanningView
        {
            foreach (var panel in panels) views[panel] = view;
            allViews.Add(view);
            view.Root.style.display = DisplayStyle.None;
            body.Add(view.Root);
            return view;
        }

        void Key(VisualElement parent, string capKey, string textKey)
        {
            var row = Box(parent, "es-plan__place-key", null);
            var cap = new Label(Strings.Get(capKey)); cap.AddToClassList("es-keycap"); row.Add(cap);
            var text = new Label(Strings.Get(textKey)); text.AddToClassList("es-plan__place-key-text"); row.Add(text);
        }

        static VisualElement Box(VisualElement parent, string className, string name)
        {
            var box = new VisualElement();
            if (name != null) box.name = name;
            box.AddToClassList(className);
            parent.Add(box);
            return box;
        }

        static Label Text(VisualElement parent, string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        public override void Refresh()
        {
            if (Element == null || Shop == null) return;
            var root = Shop.ToolkitUi;
            if (root == null || !root.Active) { HideAll(); return; }
            if (Shop.PlacementActive)
            {
                // Placing: the world is the tool. Only the key bar shows, at the bottom.
                if (!IsShown) Show();
                SetShown(dim, false); SetShown(book, false);
                ShowPlacementBar();
                return;
            }
            SetPlacementBar(false);
            if (!Handles(Shop.Panel) || !views.TryGetValue(Shop.Panel, out var view)) { HideAll(); return; }
            if (!IsShown) { Show(); FadeIn(); }
            SetShown(dim, true); SetShown(book, true);
            // A notice belongs to the page it was raised on: moving to another page drops an older one, while a notice
            // raised in the same frame as the move (a placement committed back to Arrange) still shows.
            if (Shop.Panel != lastPage) { lastPage = Shop.Panel; noticeFloor = Time.unscaledTime; }
            if (!ReferenceEquals(view, CurrentView) || Shop.Panel != ShownPanel)
            {
                if (CurrentView != null) CurrentView.Root.style.display = DisplayStyle.None;
                bool newView = !ReferenceEquals(view, CurrentView);
                CurrentView = view; ShownPanel = Shop.Panel;
                view.Root.style.display = DisplayStyle.Flex;
                if (newView) view.Opened();
            }
            view.FillIcons();
            view.Refresh();
            Set(title, view.Title);
            Set(subtitle, view.Subtitle);
            SetShown(subtitle, !string.IsNullOrEmpty(view.Subtitle));
            string notice = Shop.PlanningNotice(out bool error, noticeFloor);
            string note = notice ?? view.Note;
            Set(footer, note);
            SetShown(footer, !string.IsNullOrEmpty(note));
            footer.EnableInClassList("es-plan__footer--notice", notice != null);
            footer.EnableInClassList("es-plan__footer--error", error);
            var active = PlanningRules.TabOf(Shop.Panel);
            foreach (var pair in tabs)
            {
                bool on = pair.Key == active;
                bool available = on || Shop.PlanningTabAvailable(pair.Key);
                pair.Value.EnableInClassList("es-plan-tab--active", on);
                if (pair.Value.enabledSelf != available) pair.Value.SetEnabled(available);
                SetShown(tabWhere[pair.Key], !available);
            }
        }

        void ShowPlacementBar()
        {
            bool placing = Shop.PlanPlacement(Strings, out string t, out string detail, out bool valid);
            SetPlacementBar(placing);
            if (!placing) return;
            Set(placeTitle, t);
            Set(placeDetail, detail);
            placeDetail.EnableInClassList("es-plan__place-detail--ok", valid);
        }

        void SetPlacementBar(bool shown)
        {
            SetShown(placeBar, shown);
            PlacementBarShown = shown;
        }

        void HideAll()
        {
            if (CurrentView != null) { CurrentView.Root.style.display = DisplayStyle.None; CurrentView = null; }
            ShownPanel = ShopPanel.None;
            lastPage = ShopPanel.None; // reopened later, the notebook starts without older notices
            SetPlacementBar(false);
            if (IsShown) Hide();
        }

        static void Set(TextElement element, string text)
        {
            text = text ?? "";
            if (element.text != text) element.text = text;
        }

        static void SetShown(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.keyword != StyleKeyword.Undefined || element.style.display.value != display) element.style.display = display;
        }

        public override void Unbind()
        {
            views.Clear(); allViews.Clear(); tabs.Clear(); tabWhere.Clear();
            CurrentView = null;
            base.Unbind();
        }
    }

    /// <summary>
    /// One page of the planning notebook. It fills <see cref="Root"/> from a snapshot the session gives it and calls the
    /// session's existing commands only; the frame owns the tabs, the title, Back and the footer line.
    /// </summary>
    public abstract class PlanningView
    {
        protected readonly ShopSession Shop;
        protected readonly ShopUiStrings Strings;
        public VisualElement Root { get; }
        public string Title { get; protected set; }
        public string Subtitle { get; protected set; }
        /// <summary>The footer line when no notice is showing: one short rule of thumb, never the safety fine print.</summary>
        public string Note { get; protected set; }

        protected PlanningView(ShopSession shop, ShopUiStrings strings, string name)
        {
            Shop = shop; Strings = strings;
            Root = new VisualElement { name = name };
            Root.AddToClassList("es-plan-view");
        }

        public abstract void Refresh();
        /// <summary>Called when the notebook turns to this page, to reset transient state such as an open menu or a confirm step.</summary>
        public virtual void Opened() { }

        protected static Label NewLabel(VisualElement parent, string name, string className, string text = "")
        {
            var label = new Label(text) { name = name };
            if (!string.IsNullOrEmpty(className)) foreach (var c in className.Split(' ')) label.AddToClassList(c);
            parent?.Add(label);
            return label;
        }

        protected static Button NewButton(VisualElement parent, string name, string className, string text, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.AddToClassList("es-button");
            if (!string.IsNullOrEmpty(className)) foreach (var c in className.Split(' ')) button.AddToClassList(c);
            parent?.Add(button);
            return button;
        }

        protected static VisualElement NewBox(VisualElement parent, string className, string name = null)
        {
            var box = new VisualElement();
            if (name != null) box.name = name;
            if (!string.IsNullOrEmpty(className)) foreach (var c in className.Split(' ')) box.AddToClassList(c);
            parent?.Add(box);
            return box;
        }

        protected static void SetText(TextElement element, string text)
        {
            text = text ?? "";
            if (element.text != text) element.text = text;
        }

        protected static void Shown(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.keyword != StyleKeyword.Undefined || element.style.display.value != display) element.style.display = display;
        }

        protected static void Enabled(VisualElement element, bool enabled)
        {
            if (element.enabledSelf != enabled) element.SetEnabled(enabled);
        }

        protected static string CultureClass(CultureKind kind) => "es-culture--" + kind.ToString().ToLowerInvariant();

        readonly List<Image> coins = new List<Image>();

        protected Image NewCoin(VisualElement parent, string className)
        {
            var coin = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            coin.AddToClassList(className);
            parent.Add(coin);
            coins.Add(coin);
            return coin;
        }

        /// <summary>Gives coin images their texture once the HUD theme has made it.</summary>
        public void FillIcons()
        {
            for (int i = 0; i < coins.Count; i++)
                if (coins[i].image == null) coins[i].image = Shop.HudIcon(ShopUiTheme.Symbol.Coin);
        }

        /// <summary>A culture recipe as small coloured dots with counts, as on the incubator's recipe tiles.</summary>
        protected static VisualElement NewRecipe(VisualElement parent, string name)
        {
            var row = NewBox(parent, "es-plan-recipe-pips", name);
            row.pickingMode = PickingMode.Ignore;
            return row;
        }

        protected static void FillRecipe(VisualElement row, int bloom, int flow, int spark, int mist)
        {
            row.Clear();
            Pip(row, CultureKind.Bloom, bloom); Pip(row, CultureKind.Flow, flow); Pip(row, CultureKind.Spark, spark); Pip(row, CultureKind.Mist, mist);
        }

        static void Pip(VisualElement row, CultureKind kind, int count)
        {
            if (count <= 0) return;
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("es-plan-pip"); dot.AddToClassList(CultureClass(kind));
            row.Add(dot);
            var n = new Label("×" + count) { pickingMode = PickingMode.Ignore };
            n.AddToClassList("es-plan-pip__count");
            row.Add(n);
        }
    }
}
