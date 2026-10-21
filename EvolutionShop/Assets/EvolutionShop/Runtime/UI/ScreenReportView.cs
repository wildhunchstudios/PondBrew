using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// The day report (UX.6) as a receipt slip, after the approved mockup: the day's income large, three stats (sold,
    /// visitors, walked out), why people walked out (only the reasons that happened), profit after wages with the details
    /// on hover, reputation, one tip for tonight, and the one primary, Next night.
    /// </summary>
    public sealed class ReportView : ScreenView
    {
        public const string SlipName = "es-report", TitleName = "es-report-title", IncomeName = "es-report-income", SoldName = "es-report-sold",
            VisitorsName = "es-report-visitors", MissedName = "es-report-missed", ReasonsName = "es-report-reasons", ProfitName = "es-report-profit",
            ProfitValueName = "es-report-profit-value", DetailsName = "es-report-details", ReputationName = "es-report-reputation",
            TierName = "es-report-tier", TipName = "es-report-tip", NextName = "es-report-next", GuidedName = "es-report-guided",
            PartialName = "es-report-partial", EmptyName = "es-report-empty", NoticeName = "es-report-notice";

        readonly ReportState state = new ReportState();
        readonly VisualElement full, empty, profitRow, reasonsRow;
        readonly Label title, income, sold, visitors, missed, reasons, profitValue, details, reputation, tier, tip, guided, partial, notice;
        readonly VisualElement repFill;
        bool hovering;

        public ReportState State => state;
        public bool DetailsShown => hovering;

        public ReportView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, SlipName)
        {
            FullHeight = true;
            Root.AddToClassList("es-screen-receipt");
            var paper = NewBox(Root, "es-screen-receipt__paper", "es-report-paper");
            var head = NewBox(paper, "es-screen-receipt__head", "es-report-head");
            NewLabel(head, "es-report-eyebrow", "es-screen-receipt__eyebrow", Strings.Get("report.eyebrow"));
            title = NewLabel(head, TitleName, "es-screen-receipt__title");
            NewBox(paper, "es-screen-receipt__rule", "es-report-rule");

            full = NewBox(paper, "es-screen-receipt__body", "es-report-body");
            guided = NewLabel(full, GuidedName, "es-screen-callout", Strings.Get("report.guided"));
            var incomeBox = NewBox(full, "es-screen-receipt__income", "es-report-income-box");
            var incomeRow = NewBox(incomeBox, "es-screen-receipt__income-row", "es-report-income-row");
            NewCoin(incomeRow, "es-screen-receipt__coin");
            income = NewLabel(incomeRow, IncomeName, "es-screen-receipt__amount");
            NewLabel(incomeBox, "es-report-earned", "es-screen-receipt__muted", Strings.Get("report.earned"));
            var stats = NewBox(full, "es-screen-receipt__stats", "es-report-stats");
            sold = Stat(stats, SoldName, "report.sold");
            visitors = Stat(stats, VisitorsName, "report.visitors");
            missed = Stat(stats, MissedName, "report.missed");
            reasonsRow = NewBox(full, "es-screen-receipt__reasons", "es-report-reasons-row");
            reasons = NewLabel(reasonsRow, ReasonsName, "es-screen-receipt__reasons-text");
            profitRow = NewBox(full, "es-screen-receipt__line", ProfitName);
            NewLabel(profitRow, "es-report-profit-label", "es-screen-receipt__mono", Strings.Get("report.profit"));
            NewBox(profitRow, "es-screen-receipt__dots", "es-report-profit-dots");
            profitValue = NewLabel(profitRow, ProfitValueName, "es-screen-receipt__mono");
            profitRow.RegisterCallback<PointerEnterEvent>(_ => hovering = true);
            profitRow.RegisterCallback<PointerLeaveEvent>(_ => hovering = false);
            details = NewLabel(full, DetailsName, "es-screen-receipt__details");
            details.pickingMode = PickingMode.Ignore;
            partial = NewLabel(full, PartialName, "es-screen-receipt__muted", Strings.Get("report.partial"));
            var rep = NewBox(full, "es-screen-receipt__rep", "es-report-rep");
            rep.Add(new ScreenGlyph(ScreenGlyph.Kind.Star) { name = "es-report-star" });
            reputation = NewLabel(rep, ReputationName, "es-screen-receipt__rep-text");
            tier = NewLabel(rep, TierName, "es-plan-chip es-plan-chip--on es-screen-receipt__tier");
            var bar = NewBox(full, "es-screen-bar es-screen-bar--thin", "es-report-rep-bar");
            repFill = NewBox(bar, "es-screen-bar__fill", "es-report-rep-fill");
            var tipBox = NewBox(full, "es-screen-receipt__tip", "es-report-tip-box");
            tip = NewLabel(tipBox, TipName, "es-screen-receipt__tip-text");
            // Last in the body, so the popover draws over the lines below the profit row.
            details.BringToFront();

            empty = NewBox(paper, "es-screen-receipt__body", EmptyName);
            NewLabel(empty, "es-report-empty-text", "es-screen-receipt__tip-text", Strings.Get("report.none"));

            NewButton(paper, NextName, "es-button--primary es-screen-receipt__next", Strings.Get("report.next"), () => Shop.ScreenNextNight());
            notice = NewLabel(paper, NoticeName, "es-screen-notice");
            Root.Add(new ReceiptEdge { name = "es-report-tear" });
        }

        Label Stat(VisualElement parent, string name, string key)
        {
            var box = NewBox(parent, "es-screen-receipt__stat", name + "-box");
            var value = NewLabel(box, name, "es-screen-receipt__stat-value");
            NewLabel(box, name + "-label", "es-screen-receipt__muted", Strings.Get(key));
            return value;
        }

        public override void Opened() { hovering = false; }

        public override void Refresh()
        {
            Shop.FillReport(state);
            Shown(full, state.Has);
            Shown(empty, !state.Has);
            SetText(title, state.Has ? Strings.Format("report.title", state.Day) : Strings.Get("report.title.none"));
            if (!state.Has) return;
            Shown(guided, state.Guided);
            SetText(income, (state.Revenue >= 0 ? "+" : "") + state.Revenue.ToString("N0"));
            SetText(sold, state.Sales.ToString());
            SetText(visitors, state.Visitors.ToString());
            SetText(missed, state.Missed.ToString());
            Shown(reasonsRow, state.Reasons.Count > 0);
            if (state.Reasons.Count > 0)
            {
                var parts = new string[state.Reasons.Count];
                for (int i = 0; i < parts.Length; i++) parts[i] = Strings.Format(state.Reasons[i].Key, state.Reasons[i].Count);
                SetText(reasons, Strings.Format("report.walked", string.Join(" · ", parts)));
            }
            SetText(profitValue, state.Profit.ToString("N0"));
            SetText(details, Strings.Format("report.details", state.Revenue.ToString("N0"), state.Wages, state.Cash.ToString("N0"), state.StockedOpen, state.Restocked, state.StockedClose));
            Shown(details, hovering);
            if (hovering) details.style.top = profitRow.layout.yMax + 6f;
            Shown(partial, state.Partial);
            SetText(reputation, Strings.Format("report.reputation", state.Reputation));
            SetText(tier, state.TierName);
            repFill.style.width = Length.Percent(100f * ScreenRules.Fill(state.Reputation, ShopReputation.MaxPoints));
            SetText(tip, Strings.Format("report.tonight", Strings.Format(state.TipKey, state.TipArgs)));
        }

        public override void ShowNotice(string text, bool error) => FillNotice(notice, text, error);
    }
}
