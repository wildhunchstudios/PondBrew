using System.Collections.Generic;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Crew tab (UX.6, after the approved mockup): Pip, Nori and Moss as three portrait cards with what they do, their
    /// wage, a status word and an On duty switch. Hire comes first; Pause, Retry and Dismiss live in a "…" menu, and
    /// Dismiss asks first. Nori's display targets and Moss's recipe queue are pages of their own.
    /// </summary>
    public sealed class CrewView : PlanningView
    {
        public const string ViewName = "es-plan-crew";
        public static string CardName(CrewRole role) => "es-plan-crew-" + role.ToString().ToLowerInvariant();
        public static string StatusName(CrewRole role) => CardName(role) + "-status";
        public static string SwitchName(CrewRole role) => CardName(role) + "-switch";
        public static string HireName(CrewRole role) => CardName(role) + "-hire";
        public static string PayName(CrewRole role) => CardName(role) + "-pay";
        public static string MenuName(CrewRole role) => CardName(role) + "-menu";
        public static string PauseName(CrewRole role) => CardName(role) + "-pause";
        public static string RetryName(CrewRole role) => CardName(role) + "-retry";
        public static string DismissName(CrewRole role) => CardName(role) + "-dismiss";
        public static string KeepName(CrewRole role) => CardName(role) + "-keep";
        public static string DismissNowName(CrewRole role) => CardName(role) + "-dismiss-now";
        public static string PageName(CrewRole role) => CardName(role) + "-page";

        readonly CrewViewState state = new CrewViewState();
        readonly Card[] cards = new Card[3];
        CrewRole? menuOpen, confirming;

        public CrewViewState State => state;

        sealed class Card
        {
            public CrewRole Role;
            public VisualElement Root, Menu, Confirm, Footer, SwitchBox;
            public CrewPortrait Portrait;
            public Label Status, SwitchLabel, WageAmount;
            public Button MenuButton, Hire, Pay, Pause, Retry, Dismiss, Page;
            public PlanSwitch Switch;
        }

        public CrewView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("plan.crew.title");
            var grid = NewBox(Root, "es-plan-crew-grid", "es-plan-crew-grid");
            for (int i = 0; i < 3; i++) cards[i] = Build(grid, (CrewRole)i);
        }

        Card Build(VisualElement parent, CrewRole role)
        {
            string key = RoleKey(role);
            var c = new Card { Role = role };
            c.Root = NewBox(parent, "es-plan-crew-card", CardName(role));
            var top = NewBox(c.Root, "es-plan-crew-card__top");
            c.Portrait = new CrewPortrait(role) { name = CardName(role) + "-portrait" };
            top.Add(c.Portrait);
            var names = NewBox(top, "es-plan-crew-card__names");
            NewLabel(names, CardName(role) + "-name", "es-plan-crew-card__name", Strings.Get("plan.crew." + key + ".name"));
            NewLabel(names, CardName(role) + "-role", "es-plan-crew-card__role", Strings.Get("plan.crew." + key + ".role"));
            c.MenuButton = NewButton(c.Root, MenuName(role), "es-plan-crew-card__menu-button", Strings.Get("plan.crew.more"), () => ToggleMenu(role));
            NewLabel(c.Root, CardName(role) + "-what", "es-plan-crew-card__what", Strings.Get("plan.crew." + key + ".what"));
            c.Status = NewLabel(c.Root, StatusName(role), "es-plan-chip");
            c.Pay = NewButton(c.Root, PayName(role), "es-button--secondary es-button--compact es-plan-crew-card__pay", "", () => Shop.PlanPay(role));
            if (role != CrewRole.Cashier)
                c.Page = NewButton(c.Root, PageName(role), "es-link es-plan-crew-card__page", Strings.Get("plan.crew." + key + ".page"),
                    () => { if (role == CrewRole.Stocker) Shop.OpenStockerTargets(); else Shop.OpenTechnicianQueue(); });
            NewBox(c.Root, "es-plan-grow");
            c.Footer = NewBox(c.Root, "es-plan-crew-card__footer");
            var wage = NewBox(c.Footer, "es-plan-wage");
            NewCoin(wage, "es-plan-wage__coin");
            c.WageAmount = NewLabel(wage, CardName(role) + "-wage", "es-plan-wage__amount");
            NewLabel(wage, null, "es-plan-wage__unit", Strings.Get("plan.crew.perNight"));
            c.Hire = NewButton(c.Footer, HireName(role), "es-button--secondary es-button--compact", Strings.Format("plan.crew.hire", Strings.Get("plan.crew." + key + ".name")), () => Shop.PlanHire(role));
            c.SwitchBox = NewBox(c.Footer, "es-plan-switch-row");
            c.SwitchLabel = NewLabel(c.SwitchBox, CardName(role) + "-switch-label", "es-plan-switch-row__label", Strings.Get("plan.crew.onDuty"));
            c.Switch = new PlanSwitch(() => Shop.PlanSchedule(role, !state.Members[(int)role].Scheduled)) { name = SwitchName(role) };
            c.SwitchBox.Add(c.Switch);

            c.Menu = NewBox(c.Root, "es-plan-menu", CardName(role) + "-menu-list");
            c.Pause = NewButton(c.Menu, PauseName(role), "es-plan-menu__item", "", () => { menuOpen = null; Shop.PlanPause(role, !state.Members[(int)role].Paused); });
            if (role != CrewRole.Cashier)
                c.Retry = NewButton(c.Menu, RetryName(role), "es-plan-menu__item", Strings.Get("plan.crew.retry"), () => { menuOpen = null; Shop.PlanRetry(role); });
            c.Dismiss = NewButton(c.Menu, DismissName(role), "es-plan-menu__item es-plan-menu__item--danger", Strings.Format("plan.crew.dismiss", Strings.Get("plan.crew." + key + ".name")),
                () => { menuOpen = null; confirming = role; });

            c.Confirm = NewBox(c.Root, "es-plan-confirm", CardName(role) + "-confirm");
            NewLabel(c.Confirm, CardName(role) + "-confirm-text", "es-plan-confirm__text", Strings.Get("plan.crew." + key + ".dismissAsk"));
            var buttons = NewBox(c.Confirm, "es-plan-confirm__buttons");
            NewButton(buttons, KeepName(role), "es-button--secondary es-button--compact", Strings.Format("plan.crew.keep", Strings.Get("plan.crew." + key + ".name")), () => confirming = null);
            NewButton(buttons, DismissNowName(role), "es-button--danger es-button--compact", Strings.Get("plan.crew.dismissNow"), () => { confirming = null; Shop.PlanDismiss(role); });
            return c;
        }

        static string RoleKey(CrewRole role) => role == CrewRole.Cashier ? "pip" : role == CrewRole.Stocker ? "nori" : "moss";

        void ToggleMenu(CrewRole role) { confirming = null; menuOpen = menuOpen == role ? (CrewRole?)null : role; }

        public override void Opened() { menuOpen = null; confirming = null; }

        public override void Refresh()
        {
            Shop.FillCrewView(state);
            Subtitle = Strings.Get(state.Night ? "plan.crew.subtitle.night" : "plan.crew.subtitle.day");
            Note = Strings.Get(state.Night ? "plan.crew.note.night" : "plan.crew.note.day");
            foreach (var c in cards)
            {
                var m = state.Members[(int)c.Role];
                string status = PlanningRules.CrewStatusKey(m, state.Night);
                SetText(c.Status, Strings.Get(status));
                c.Status.EnableInClassList("es-plan-chip--warn", PlanningRules.CrewStatusWarns(status));
                c.Status.EnableInClassList("es-plan-chip--on", m.OnDuty && !PlanningRules.CrewStatusWarns(status));
                c.Root.EnableInClassList("es-plan-crew-card--on", m.OnDuty);
                c.Portrait.SetDimmed(!m.Hired);
                SetText(c.WageAmount, m.Wage.ToString());
                Shown(c.Hire, !m.Hired);
                Enabled(c.Hire, state.Night);
                Shown(c.SwitchBox, m.Hired);
                c.Switch.SetOn(m.Scheduled);
                Enabled(c.Switch, state.Night);
                SetText(c.SwitchLabel, Strings.Get(m.Scheduled ? "plan.crew.onDuty" : "plan.crew.offDuty"));
                bool pay = PlanningRules.CrewNeedsPay(m, state.Night);
                Shown(c.Pay, pay);
                if (pay) { SetText(c.Pay, Strings.Format("plan.crew.pay", m.Wage)); Enabled(c.Pay, state.Currency >= m.Wage); }
                Shown(c.MenuButton, m.Hired);
                bool menu = menuOpen == c.Role && m.Hired && confirming == null;
                Shown(c.Menu, menu);
                SetText(c.Pause, Strings.Get(m.Paused ? "plan.crew.resume" : "plan.crew.pause"));
                if (c.Retry != null) Shown(c.Retry, m.Blocked);
                Enabled(c.Dismiss, state.Night);
                Shown(c.Confirm, confirming == c.Role && m.Hired);
                if (!m.Hired && (menuOpen == c.Role || confirming == c.Role)) { menuOpen = null; confirming = null; }
            }
        }
    }

    /// <summary>Nori's displays (the classic display targets): each display keeps one creature, off, any species or one species.</summary>
    public sealed class StockerTargetsView : PlanningView
    {
        public const string ViewName = "es-plan-targets";
        public static string RowName(string tankId) => "es-plan-target-" + tankId;
        public static string PickName(string tankId, string target) => RowName(tankId) + "-" + (target == null ? "off" : target == ShopStocking.AnySpecies ? "any" : target);

        readonly StockerTargetsState state = new StockerTargetsState();
        readonly ScrollView list;
        readonly Label empty;
        string shape;

        public StockerTargetsState State => state;

        public StockerTargetsView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("plan.targets.title");
            Subtitle = Strings.Get("plan.targets.subtitle");
            Note = Strings.Get("plan.targets.note");
            list = new ScrollView(ScrollViewMode.Vertical) { name = "es-plan-targets-list" };
            list.AddToClassList("es-plan-scroll");
            Root.Add(list);
            empty = NewLabel(Root, "es-plan-targets-empty", "es-plan-empty", Strings.Get("plan.targets.empty"));
        }

        public override void Refresh()
        {
            Shop.FillStockerTargets(state);
            string now = Shape();
            if (now != shape) { shape = now; Rebuild(); }
            Shown(empty, state.Rows.Count == 0);
            foreach (var row in state.Rows)
                foreach (var option in state.Options)
                {
                    var pick = list.Q<Button>(PickName(row.TankId, option.Id));
                    if (pick != null) pick.EnableInClassList("es-plan-pick--on", row.Target == option.Id);
                }
        }

        string Shape()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var row in state.Rows) sb.Append(row.TankId).Append(',');
            sb.Append('|');
            foreach (var option in state.Options) sb.Append(option.Id).Append(',');
            return sb.ToString();
        }

        void Rebuild()
        {
            list.Clear();
            foreach (var row in state.Rows)
            {
                string tank = row.TankId;
                var line = NewBox(list, "es-plan-row", RowName(tank));
                NewLabel(line, RowName(tank) + "-name", "es-plan-row__name", row.Name);
                var picks = NewBox(line, "es-plan-picks");
                foreach (var option in state.Options)
                {
                    string target = option.Id;
                    string text = target == null ? Strings.Get("plan.targets.off") : target == ShopStocking.AnySpecies ? Strings.Get("plan.targets.any") : option.Name;
                    NewButton(picks, PickName(tank, target), "es-plan-pick", text, () => Shop.SetStockingTarget(tank, target));
                }
            }
        }
    }

    /// <summary>Moss's recipe queue (the classic known recipes page): the order Moss starts recipes in, and how many starts each has.</summary>
    public sealed class TechnicianQueueView : PlanningView
    {
        public const string ViewName = "es-plan-queue";
        public const string ActiveName = "es-plan-queue-active", EmptyName = "es-plan-queue-empty", ClearName = "es-plan-queue-clear",
            KeepName = "es-plan-queue-keep", ClearNowName = "es-plan-queue-clear-now";
        public static string RowName(string speciesId) => "es-plan-queue-" + speciesId;
        public static string EarlierName(string speciesId) => RowName(speciesId) + "-earlier";
        public static string LaterName(string speciesId) => RowName(speciesId) + "-later";
        public static string RemoveName(string speciesId) => RowName(speciesId) + "-remove";
        public static string AddName(string speciesId) => "es-plan-queue-add-" + speciesId;

        readonly TechnicianQueueState state = new TechnicianQueueState();
        readonly Label active, empty;
        readonly VisualElement rows, adds, confirm;
        readonly Button clear;
        bool confirming;
        string shape;

        public TechnicianQueueState State => state;

        public TechnicianQueueView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("plan.queue.title");
            Subtitle = Strings.Get("plan.queue.subtitle");
            Note = Strings.Get("plan.queue.note");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "es-plan-queue-scroll" };
            scroll.AddToClassList("es-plan-scroll");
            Root.Add(scroll);
            active = NewLabel(scroll, ActiveName, "es-plan-chip es-plan-chip--on es-plan-queue__active");
            NewLabel(scroll, null, "es-eyebrow", Strings.Get("plan.queue.waiting"));
            rows = NewBox(scroll, "es-plan-list", "es-plan-queue-rows");
            empty = NewLabel(scroll, EmptyName, "es-plan-empty", Strings.Get("plan.queue.empty"));
            NewLabel(scroll, null, "es-eyebrow", Strings.Get("plan.queue.add"));
            adds = NewBox(scroll, "es-plan-picks", "es-plan-queue-adds");
            clear = NewButton(scroll, ClearName, "es-button--danger es-button--compact es-plan-queue__clear", Strings.Get("plan.queue.clear"), () => confirming = true);
            confirm = NewBox(scroll, "es-plan-confirm es-plan-confirm--inline", "es-plan-queue-confirm");
            NewLabel(confirm, null, "es-plan-confirm__text", Strings.Get("plan.queue.clearAsk"));
            var buttons = NewBox(confirm, "es-plan-confirm__buttons");
            NewButton(buttons, KeepName, "es-button--secondary es-button--compact", Strings.Get("plan.queue.keep"), () => confirming = false);
            NewButton(buttons, ClearNowName, "es-button--danger es-button--compact", Strings.Get("plan.queue.clearNow"),
                () => { confirming = false; Shop.CancelTechnicianAssignments(); });
        }

        public override void Opened() { confirming = false; }

        public override void Refresh()
        {
            Shop.FillTechnicianQueue(state);
            string now = Shape();
            if (now != shape) { shape = now; Rebuild(); }
            Shown(active, state.ActiveName != null);
            if (state.ActiveName != null) SetText(active, Strings.Format("plan.queue.growing", state.ActiveName));
            Shown(empty, state.Rows.Count == 0);
            for (int i = 0; i < state.Rows.Count; i++)
            {
                var row = state.Rows[i];
                var count = rows.Q<Label>(RowName(row.SpeciesId) + "-count");
                if (count != null) SetText(count, Strings.Format(row.Count == 1 ? "plan.queue.start" : "plan.queue.starts", row.Count));
                var index = rows.Q<Label>(RowName(row.SpeciesId) + "-index");
                if (index != null) SetText(index, (i + 1).ToString());
                Enabled(rows.Q<Button>(EarlierName(row.SpeciesId)), i > 0);
                Enabled(rows.Q<Button>(LaterName(row.SpeciesId)), i + 1 < state.Rows.Count);
            }
            foreach (var known in state.Known)
            {
                var add = adds.Q<Button>(AddName(known.SpeciesId));
                if (add != null) Enabled(add, known.Count < state.MaxCount);
            }
            bool any = state.Rows.Count > 0 || state.ActiveName != null;
            Shown(clear, any && !confirming);
            Shown(confirm, any && confirming);
            if (!any) confirming = false;
        }

        string Shape()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var row in state.Rows) sb.Append(row.SpeciesId).Append(',');
            sb.Append('|');
            foreach (var known in state.Known) sb.Append(known.SpeciesId).Append(',');
            return sb.ToString();
        }

        void Rebuild()
        {
            rows.Clear(); adds.Clear();
            foreach (var row in state.Rows)
            {
                string id = row.SpeciesId;
                var line = NewBox(rows, "es-plan-row", RowName(id));
                NewLabel(line, RowName(id) + "-index", "es-plan-row__index");
                var glyph = new CreatureGlyph { name = RowName(id) + "-glyph" };
                glyph.AddToClassList("es-plan-row__glyph");
                glyph.Set(row.Look, 1);
                line.Add(glyph);
                NewLabel(line, RowName(id) + "-name", "es-plan-row__name", row.Name);
                NewLabel(line, RowName(id) + "-count", "es-plan-row__meta");
                var actions = NewBox(line, "es-plan-row__actions");
                NewButton(actions, EarlierName(id), "es-button--secondary es-button--compact", Strings.Get("plan.queue.earlier"), () => Shop.MoveTechnicianPriority(id, -1));
                NewButton(actions, LaterName(id), "es-button--secondary es-button--compact", Strings.Get("plan.queue.later"), () => Shop.MoveTechnicianPriority(id, 1));
                NewButton(actions, RemoveName(id), "es-button--secondary es-button--compact", Strings.Get("plan.queue.remove"), () => Shop.RemoveTechnicianRecipe(id));
            }
            foreach (var known in state.Known)
            {
                string id = known.SpeciesId;
                NewButton(adds, AddName(id), "es-plan-pick", Strings.Format("plan.queue.addOne", known.Name), () => Shop.QueueTechnicianRecipe(id));
            }
        }
    }
}
