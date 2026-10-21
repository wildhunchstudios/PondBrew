using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// The journal (TAB, UX.6): Creatures is a grid of silhouettes, discovered ones with their recipe pips, family and
    /// standard price and the others with their clue; Customers shows the four types with budget, patience and
    /// favourites as icons; Shop shows reputation and appeal with what the next tier gives. Time keeps running.
    /// </summary>
    public sealed class JournalView : ScreenView
    {
        public const string CreaturesName = "es-journal-creatures", CustomersName = "es-journal-customers", ShopName = "es-journal-shop",
            ReputationName = "es-journal-reputation", ReputationBarName = "es-journal-reputation-bar", NextTierName = "es-journal-next-tier",
            AppealName = "es-journal-appeal", NowName = "es-journal-now";
        public static string TabName(JournalTab tab) => "es-journal-tab-" + tab.ToString().ToLowerInvariant();
        public static string CreatureName(string id) => "es-journal-creature-" + id;
        public static string CustomerName(string id) => "es-journal-customer-" + id;

        sealed class CreatureCard { public VisualElement Box; public CreatureGlyph Glyph; public Label Name, Family, Clue, Price, Tag; public VisualElement Pips, PriceRow; }
        sealed class CustomerCard { public VisualElement Box, Favorites; public MoodFace Face; public Label Name, Budget, Patience, Pays, FavoriteText; }

        readonly JournalState state = new JournalState();
        readonly Dictionary<JournalTab, Button> tabs = new Dictionary<JournalTab, Button>();
        readonly VisualElement creatures, customers, shopPage, creatureGrid, customerRow;
        readonly List<CreatureCard> creatureCards = new List<CreatureCard>();
        readonly List<CustomerCard> customerCards = new List<CustomerCard>();
        readonly Label repValue, repTier, nextTier, appealValue, now;
        readonly VisualElement repFill, repMark, appealFill;
        JournalTab tab;

        public JournalState State => state;
        public JournalTab Tab => tab;
        public override string BackText => Strings.Get(state.ReturnsToHelp ? "journal.back.help" : "plan.back");

        public JournalView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, "es-journal")
        {
            UsesSheet = true;
            Root.AddToClassList("es-screen-view--sheet");
            Title = Strings.Get("journal.title");
            Tabs = new VisualElement { name = "es-journal-tabs" };
            Tabs.AddToClassList("es-screen-tabs");
            foreach (JournalTab t in System.Enum.GetValues(typeof(JournalTab)))
            {
                var which = t;
                var button = new Button(() => tab = which) { name = TabName(t) };
                button.AddToClassList("es-button"); button.AddToClassList("es-plan-tab");
                var label = new Label(Strings.Get("journal.tab." + t.ToString().ToLowerInvariant())) { pickingMode = PickingMode.Ignore, name = TabName(t) + "-label" };
                label.AddToClassList("es-plan-tab__name");
                button.Add(label);
                Tabs.Add(button);
                tabs[t] = button;
            }

            creatures = NewBox(Root, "es-screen-journal__page", CreaturesName);
            creatureGrid = NewBox(creatures, "es-screen-journal__grid", "es-journal-creature-grid");

            customers = NewBox(Root, "es-screen-journal__page", CustomersName);
            customerRow = NewBox(customers, "es-screen-journal__grid", "es-journal-customer-grid");

            shopPage = NewBox(Root, "es-screen-journal__page es-screen-journal__shop", ShopName);
            var rep = NewBox(shopPage, "es-screen-panel", ReputationName);
            var repHead = NewBox(rep, "es-screen-panel__head", "es-journal-reputation-head");
            var star = new ScreenGlyph(ScreenGlyph.Kind.Star) { name = "es-journal-reputation-star" };
            star.style.marginRight = 8;
            repHead.Add(star);
            NewLabel(repHead, "es-journal-reputation-title", "es-screen-panel__title", Strings.Get("journal.shop.reputation"));
            repTier = NewLabel(repHead, "es-journal-reputation-tier", "es-plan-chip es-plan-chip--on");
            repValue = NewLabel(rep, "es-journal-reputation-value", "es-screen-panel__value");
            var bar = NewBox(rep, "es-screen-bar", ReputationBarName);
            repFill = NewBox(bar, "es-screen-bar__fill", "es-journal-reputation-fill");
            repMark = NewBox(bar, "es-screen-bar__mark", "es-journal-reputation-mark");
            nextTier = NewLabel(rep, NextTierName, "es-screen-panel__line");
            NewLabel(rep, "es-journal-reputation-rule", "es-screen-panel__small", Strings.Get("journal.shop.rule"));
            var appeal = NewBox(shopPage, "es-screen-panel", AppealName);
            var appealHead = NewBox(appeal, "es-screen-panel__head", "es-journal-appeal-head");
            NewLabel(appealHead, "es-journal-appeal-title", "es-screen-panel__title", Strings.Get("journal.shop.appeal"));
            appealValue = NewLabel(appeal, "es-journal-appeal-value", "es-screen-panel__value");
            var appealBar = NewBox(appeal, "es-screen-bar es-screen-bar--leaf", "es-journal-appeal-bar");
            appealFill = NewBox(appealBar, "es-screen-bar__fill", "es-journal-appeal-fill");
            NewLabel(appeal, "es-journal-appeal-line", "es-screen-panel__line", Strings.Get("journal.shop.appealLine"));
            now = NewLabel(shopPage, NowName, "es-screen-journal__now");
        }

        public override void Opened() { tab = JournalTab.Creatures; }

        public override void Refresh()
        {
            Shop.FillJournal(state);
            Note = Strings.Get(state.ReturnsToHelp ? "journal.note.help" : "journal.note");
            foreach (var pair in tabs) pair.Value.EnableInClassList("es-plan-tab--active", pair.Key == tab);
            Shown(creatures, tab == JournalTab.Creatures);
            Shown(customers, tab == JournalTab.Customers);
            Shown(shopPage, tab == JournalTab.Shop);
            switch (tab)
            {
                case JournalTab.Creatures:
                    Subtitle = Strings.Format("journal.sub.creatures", state.Discovered, state.Creatures.Count);
                    RefreshCreatures();
                    break;
                case JournalTab.Customers:
                    Subtitle = Strings.Get("journal.sub.customers");
                    RefreshCustomers();
                    break;
                default:
                    Subtitle = Strings.Get("journal.sub.shop");
                    RefreshShop();
                    break;
            }
        }

        void RefreshCreatures()
        {
            while (creatureCards.Count < state.Creatures.Count)
            {
                var card = new CreatureCard { Box = NewBox(creatureGrid, "es-screen-creature") };
                card.Glyph = new CreatureGlyph(); card.Glyph.AddToClassList("es-screen-creature__glyph"); card.Box.Add(card.Glyph);
                card.Name = NewLabel(card.Box, null, "es-screen-creature__name");
                card.Family = NewLabel(card.Box, null, "es-plan-chip");
                card.Pips = NewRecipe(card.Box, null);
                card.PriceRow = NewBox(card.Box, "es-screen-creature__price");
                NewCoin(card.PriceRow, "es-screen-coin");
                card.Price = NewLabel(card.PriceRow, null, "es-screen-creature__amount");
                card.Tag = NewLabel(card.PriceRow, null, "es-screen-creature__tag");
                card.Clue = NewLabel(card.Box, null, "es-screen-creature__clue");
                creatureCards.Add(card);
            }
            for (int i = 0; i < creatureCards.Count; i++)
            {
                var card = creatureCards[i];
                bool used = i < state.Creatures.Count;
                Shown(card.Box, used);
                if (!used) continue;
                var c = state.Creatures[i];
                card.Box.name = CreatureName(c.Id);
                card.Box.EnableInClassList("es-screen-creature--unknown", !c.Discovered);
                card.Glyph.Set(c.Look, c.Discovered ? 1f : 0f);
                SetText(card.Name, c.Discovered ? c.Name : Strings.Get("journal.unknown"));
                card.Name.name = card.Box.name + "-name";
                SetText(card.Family, c.Family);
                Shown(card.Family, c.Discovered);
                Shown(card.Pips, c.Discovered);
                Shown(card.PriceRow, c.Discovered);
                if (c.Discovered)
                {
                    FillRecipe(card.Pips, c.Bloom, c.Flow, c.Spark, c.Mist);
                    SetText(card.Price, c.Standard.ToString());
                    card.Price.name = card.Box.name + "-standard";
                    SetText(card.Tag, c.Price == c.Standard ? Strings.Get("journal.standard") : Strings.Format("journal.tag", c.Price));
                }
                SetText(card.Clue, c.Discovered ? "" : Strings.Get(c.ClueKey));
                card.Clue.name = card.Box.name + "-clue";
                Shown(card.Clue, !c.Discovered);
            }
        }

        void RefreshCustomers()
        {
            while (customerCards.Count < state.Customers.Count)
            {
                var card = new CustomerCard { Box = NewBox(customerRow, "es-screen-customer") };
                card.Face = new MoodFace(); card.Face.AddToClassList("es-screen-customer__face"); card.Box.Add(card.Face);
                card.Name = NewLabel(card.Box, null, "es-screen-customer__name");
                var budget = NewBox(card.Box, "es-screen-stat");
                NewCoin(budget, "es-screen-coin");
                card.Budget = NewLabel(budget, null, "es-screen-stat__text");
                var patience = NewBox(card.Box, "es-screen-stat");
                patience.Add(new ScreenGlyph(ScreenGlyph.Kind.Clock));
                card.Patience = NewLabel(patience, null, "es-screen-stat__text");
                var pays = NewBox(card.Box, "es-screen-stat");
                pays.Add(new ScreenGlyph(ScreenGlyph.Kind.Star));
                card.Pays = NewLabel(pays, null, "es-screen-stat__text");
                var fav = NewBox(card.Box, "es-screen-stat");
                fav.Add(new ScreenGlyph(ScreenGlyph.Kind.Heart));
                card.Favorites = NewBox(fav, "es-screen-customer__favorites");
                card.FavoriteText = NewLabel(fav, null, "es-screen-stat__text");
                customerCards.Add(card);
            }
            for (int i = 0; i < customerCards.Count; i++)
            {
                var card = customerCards[i];
                bool used = i < state.Customers.Count;
                Shown(card.Box, used);
                if (!used) continue;
                var c = state.Customers[i];
                card.Box.name = CustomerName(c.Id);
                card.Face.Set(BuyerMood.Happy, c.Id, HudPresenter.ArchetypeColor(c.Id));
                SetText(card.Name, c.Name);
                SetText(card.Budget, c.Budget >= 100000 ? Strings.Get("journal.budget.none") : Strings.Format("journal.budget", c.Budget));
                card.Budget.name = card.Box.name + "-budget";
                SetText(card.Patience, Strings.Format("journal.patience", c.Patience));
                card.Patience.name = card.Box.name + "-patience";
                SetText(card.Pays, Strings.Format("journal.pays", c.TolerancePercent));
                card.Pays.name = card.Box.name + "-pays";
                bool favorites = c.FavoriteLooks.Count > 0;
                Shown(card.Favorites.parent, true);
                card.Favorites.Clear();
                foreach (string look in c.FavoriteLooks)
                {
                    var glyph = new CreatureGlyph(); glyph.AddToClassList("es-screen-customer__favorite");
                    glyph.Set(look, 1f);
                    card.Favorites.Add(glyph);
                }
                SetText(card.FavoriteText, favorites ? Strings.Format("journal.favorites", string.Join(", ", c.FavoriteNames), c.FavoritePercent)
                    : Strings.Get("journal.favorites.none"));
                card.FavoriteText.name = card.Box.name + "-favorites";
            }
        }

        void RefreshShop()
        {
            SetText(repTier, state.TierName);
            SetText(repValue, Strings.Format("journal.shop.repValue", state.Reputation, state.ReputationMax));
            repFill.style.width = Length.Percent(100f * ScreenRules.Fill(state.Reputation, state.ReputationMax));
            bool topTier = state.Tier >= 2;
            Shown(repMark, !topTier);
            if (!topTier) repMark.style.left = Length.Percent(100f * ScreenRules.Fill(state.NextThreshold, state.ReputationMax));
            int next = state.Tier + 1;
            SetText(nextTier, topTier ? Strings.Get("journal.shop.top") : Strings.Format("journal.shop.next", Strings.Get("journal.tier." + next), state.NextThreshold,
                ScreenRules.TierArrivalSeconds(next).ToString("0"), ScreenRules.TierPricePercent(next), ScreenRules.TierBudget(next)));
            SetText(appealValue, Strings.Format("journal.shop.appealValue", state.Appeal, state.AppealMax));
            appealFill.style.width = Length.Percent(100f * ScreenRules.Fill(state.Appeal, state.AppealMax));
            SetText(now, Strings.Format("journal.shop.now", state.ArrivalSeconds.ToString("0.##"), state.PriceBonus, state.BudgetBonus));
        }
    }
}
