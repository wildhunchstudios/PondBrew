using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-07 screens (UX.6): welcome, help, journal, day report and pause, over the dimmed world. Like the planning
    /// notebook it follows ShopSession.Panel and shows while one of these panels is open and the preview is on. Help and
    /// the journal sit on a notebook sheet with a title, Back (Esc) and a footer line; the welcome card, the receipt and
    /// the pause cards draw their own shapes on a stage, after the approved mockups.
    /// </summary>
    public sealed class ScreenPresenter : ShopUiPresenter
    {
        public const string RootName = "es-screen";
        public const string DimName = "es-screen-dim";
        public const string SheetName = "es-screen-sheet";
        public const string TabsName = "es-screen-tabs";
        public const string StageName = "es-screen-stage";
        public const string TitleName = "es-screen-title";
        public const string SubtitleName = "es-screen-subtitle";
        public const string BackName = "es-screen-back";
        public const string BackTextName = "es-screen-back-text";
        public const string FooterName = "es-screen-footer";
        public const string PrimaryClass = "es-button--primary";

        VisualElement dim, book, tabs, sheet, body, stage;
        Label title, subtitle, footer, backText;
        Button back;
        ShopPanel lastPanel = ShopPanel.None;
        float noticeFloor = float.NegativeInfinity;
        readonly Dictionary<ShopPanel, ScreenView> views = new Dictionary<ShopPanel, ScreenView>();

        public ScreenView CurrentView { get; private set; }
        public ShopPanel ShownPanel { get; private set; }
        public string Footer => footer != null ? footer.text : "";
        public VisualElement Sheet => sheet;
        public WelcomeView Welcome { get; private set; }
        public HelpView Help { get; private set; }
        public JournalView Journal { get; private set; }
        public ReportView Report { get; private set; }
        public PauseView Pause { get; private set; }

        /// <summary>Panels these screens draw; the classic IMGUI panel must not draw them while the preview is on.</summary>
        public static bool Handles(ShopPanel panel) => ShopSession.IsScreenPanel(panel);

        protected override VisualElement Build(VisualTreeAsset template)
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("es-screen");
            return root;
        }

        protected override void OnBound()
        {
            var root = Element;
            dim = Box(root, "es-screen__dim", DimName);
            book = Box(root, "es-screen__book", "es-screen-book");
            tabs = Box(book, "es-plan__tabs", TabsName);
            sheet = Box(book, "es-plan__sheet", SheetName);
            var header = Box(sheet, "es-plan__header", "es-screen-header");
            var titles = Box(header, "es-plan__titles", "es-screen-titles");
            title = Text(titles, TitleName, "es-plan__title");
            subtitle = Text(titles, SubtitleName, "es-plan__subtitle");
            back = new Button(() => Shop.BackFromPanel()) { name = BackName };
            back.AddToClassList("es-button"); back.AddToClassList("es-button--secondary"); back.AddToClassList("es-plan__back");
            var cap = new Label(Strings.Get("card.keycap.back")) { pickingMode = PickingMode.Ignore, name = BackName + "-keycap" };
            cap.AddToClassList("es-keycap"); cap.AddToClassList("es-plan__back-key");
            backText = new Label(Strings.Get("plan.back")) { pickingMode = PickingMode.Ignore, name = BackTextName };
            backText.AddToClassList("es-plan__back-text");
            back.Add(cap); back.Add(backText);
            header.Add(back);
            body = Box(sheet, "es-plan__body", "es-screen-body");
            footer = Text(sheet, FooterName, "es-plan__footer");
            stage = Box(root, "es-screen__stage", StageName);

            Welcome = Register(new WelcomeView(Shop, Strings), ShopPanel.Welcome);
            Help = Register(new HelpView(Shop, Strings), ShopPanel.TutorialHelp);
            Journal = Register(new JournalView(Shop, Strings), ShopPanel.Journal);
            Report = Register(new ReportView(Shop, Strings), ShopPanel.DayReport);
            Pause = Register(new PauseView(Shop, Strings), ShopPanel.Pause, ShopPanel.Reset);
        }

        T Register<T>(T view, params ShopPanel[] panels) where T : ScreenView
        {
            foreach (var panel in panels) views[panel] = view;
            view.Root.style.display = DisplayStyle.None;
            (view.UsesSheet ? body : stage).Add(view.Root);
            if (view.Tabs != null) { view.Tabs.style.display = DisplayStyle.None; tabs.Add(view.Tabs); }
            return view;
        }

        public override void Refresh()
        {
            if (Element == null || Shop == null) return;
            var root = Shop.ToolkitUi;
            if (root == null || !root.Active || !Handles(Shop.Panel) || !views.TryGetValue(Shop.Panel, out var view)) { HideAll(); return; }
            if (!IsShown) Show();
            // As in the notebook, a notice belongs to the screen it was raised on (or one opened by the same action).
            if (Shop.Panel != lastPanel) { lastPanel = Shop.Panel; noticeFloor = Time.unscaledTime; }
            if (!ReferenceEquals(view, CurrentView))
            {
                if (CurrentView != null)
                {
                    CurrentView.Root.style.display = DisplayStyle.None;
                    if (CurrentView.Tabs != null) CurrentView.Tabs.style.display = DisplayStyle.None;
                }
                CurrentView = view;
                view.Root.style.display = DisplayStyle.Flex;
                if (view.Tabs != null) view.Tabs.style.display = DisplayStyle.Flex;
                view.Opened();
            }
            ShownPanel = Shop.Panel;
            SetShown(book, view.UsesSheet);
            SetShown(stage, !view.UsesSheet);
            Element.EnableInClassList("es-screen--tall", view.FullHeight);
            view.FillIcons();
            view.Refresh();
            string notice = Shop.ScreenNotice(out bool error, noticeFloor);
            if (view.UsesSheet)
            {
                SetShown(tabs, view.Tabs != null);
                Set(title, view.Title);
                Set(subtitle, view.Subtitle);
                SetShown(subtitle, !string.IsNullOrEmpty(view.Subtitle));
                Set(backText, view.BackText);
                string note = notice ?? view.Note;
                Set(footer, note);
                SetShown(footer, !string.IsNullOrEmpty(note));
                footer.EnableInClassList("es-plan__footer--notice", notice != null);
                footer.EnableInClassList("es-plan__footer--error", error);
            }
            else view.ShowNotice(notice, error);
        }

        void HideAll()
        {
            if (CurrentView != null)
            {
                CurrentView.Root.style.display = DisplayStyle.None;
                if (CurrentView.Tabs != null) CurrentView.Tabs.style.display = DisplayStyle.None;
                CurrentView = null;
            }
            ShownPanel = ShopPanel.None;
            lastPanel = ShopPanel.None;
            if (IsShown) Hide();
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
            views.Clear();
            CurrentView = null;
            base.Unbind();
        }
    }

    /// <summary>
    /// One UX-07 screen. It fills <see cref="PlanningView.Root"/> from a snapshot the session gives it and calls the
    /// session's existing commands only. A sheet screen leaves its title, Back and footer to the frame; a stage screen
    /// draws its own and shows the current notice itself.
    /// </summary>
    public abstract class ScreenView : PlanningView
    {
        /// <summary>True for help and the journal: drawn on the notebook sheet.</summary>
        public bool UsesSheet { get; protected set; }
        /// <summary>True for the receipt: it may use the full height of the screen, centred between the HUD chips.</summary>
        public bool FullHeight { get; protected set; }
        /// <summary>The screen's own tab row above the sheet (the journal's), or null.</summary>
        public VisualElement Tabs { get; protected set; }
        public virtual string BackText => Strings.Get("plan.back");

        protected ScreenView(ShopSession shop, ShopUiStrings strings, string name) : base(shop, strings, name)
        {
            // Screens size themselves: a stage card keeps its own height, a sheet screen grows (es-screen-view--sheet).
            Root.RemoveFromClassList("es-plan-view");
            Root.AddToClassList("es-screen-view");
        }

        /// <summary>A stage screen shows the current notice (or the save error) itself.</summary>
        public virtual void ShowNotice(string notice, bool error) { }

        /// <summary>A key cap and what it does, for key rows.</summary>
        protected static VisualElement NewKey(VisualElement parent, string name, string cap, string what)
        {
            var row = NewBox(parent, "es-screen-key", name);
            var c = NewLabel(row, name + "-cap", "es-keycap", cap);
            c.pickingMode = PickingMode.Ignore;
            var t = NewLabel(row, name + "-text", "es-screen-key__text", what);
            t.pickingMode = PickingMode.Ignore;
            return row;
        }

        /// <summary>A notice line a stage screen keeps at its foot.</summary>
        protected static void FillNotice(Label label, string notice, bool error)
        {
            SetText(label, notice);
            Shown(label, !string.IsNullOrEmpty(notice));
            label.EnableInClassList("es-screen-notice--error", error);
        }
    }
}
