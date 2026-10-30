using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-13's shared card parts (GR.13, UX.3), drawn with the UX tokens and <c>GrowthStyles.uss</c>: the station card's
    /// strip with its tabs (each tab one job, UX.3.2), the Upgrade tab every station shares (a track of levels, the next
    /// level spelled out with its price and gates as chips), the hover tip that says why a control can't be used and
    /// carries the rules (UX.3.3, UX.3.5), and small drawn pieces: a creature glyph, a timer ring and a comfort face.
    /// </summary>
    public sealed partial class GrowthUi
    {
        const string UpgradeTab = "Upgrade";
        VisualElement strip, tabRow, cardBody, footer, infoChip;
        Label statusLabel, tip;
        VisualElement tipAnchor;
        readonly List<(Button tab, VisualElement body)> tabList = new List<(Button, VisualElement)>();
        string tabCard, tabChosen, pendingScroll;
        /// <summary>The tab shown on the open card (null when it has none).</summary>
        public string CurrentTab => tabList.Count == 0 ? null : tabList[ShownTab()].tab.text;
        /// <summary>The hover tip's text while it shows (validation).</summary>
        public string TipText => tip != null && tip.style.display == DisplayStyle.Flex ? tip.text : null;

        /// <summary>
        /// A station card's strip: its title, a status chip and a close button, then, when <paramref name="tab"/> is given,
        /// a row of tabs starting with it (the Upgrade tab joins when <see cref="UpgradeSection"/> is called). Returns the
        /// first tab's body, or the card's body when it has no tabs. <paramref name="info"/> is the card's rules, on a "?"
        /// at the end of the tab row, shown on hover rather than as prose in the card (UX.3.5).
        /// </summary>
        VisualElement Head(string title, string status = "", string tab = null, string info = null, bool shopStrip = false)
        {
            tabList.Clear(); tabRow = null; footer = null; infoChip = null; notebook = false;
            strip = new VisualElement(); strip.AddToClassList("es-card__strip"); strip.AddToClassList("gr-strip"); strip.style.flexShrink = 0;
            if (shopStrip) strip.AddToClassList("es-card__strip--shop");
            var top = new VisualElement(); top.AddToClassList("gr-strip__top");
            var t = new Label(title); t.AddToClassList("es-card__title");
            statusLabel = new Label(status); statusLabel.AddToClassList("es-card__status");
            if (string.IsNullOrEmpty(status)) statusLabel.style.display = DisplayStyle.None;
            var close = new Button(() => s.SetCard(GrowthCard.None)) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("gr-close");
            top.Add(t); top.Add(statusLabel); top.Add(close);
            strip.Add(top);
            if (tab != null || info != null)
            {
                tabRow = new VisualElement(); tabRow.AddToClassList("gr-tabs");
                if (info != null)
                {
                    infoChip = new Label("?") { tooltip = info }; infoChip.AddToClassList("gr-info");
                    if (tab == null) infoChip.style.marginTop = 0;
                    tabRow.Add(infoChip);
                }
                strip.Add(tabRow);
            }
            if (tab == null) strip.AddToClassList("gr-strip--plain");
            card.Add(strip);
            // A station card's body scrolls only when it is taller than the card may be (GR-03); GR-13 keeps each tab short.
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("es-card__body");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; scroll.style.flexShrink = 1;
            card.Add(scroll);
            cardBody = scroll.contentContainer;
            return tab != null ? AddTab(tab) : cardBody;
        }

        /// <summary>
        /// GR-13's planning view (the classic notebook, UX-06): a wide sheet over the dimmed shop with its title and a line,
        /// tabs when <paramref name="tab"/> is given, a body that fits, and a footer (<see cref="NotebookFooter"/>).
        /// </summary>
        VisualElement Notebook(string title, string subtitle, string tab = null)
        {
            tabList.Clear(); tabRow = null; footer = null; infoChip = null; notebook = true;
            var head = Box("gr-nb__head");
            head.Add(L(title, "gr-nb__title"));
            statusLabel = L(subtitle ?? "", "gr-nb__subtitle");
            head.Add(statusLabel);
            var close = new Button(() => s.SetCard(GrowthCard.None)) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("gr-close"); close.AddToClassList("gr-nb__close");
            head.Add(close);
            card.Add(head);
            if (tab != null) { tabRow = Box("gr-nb__tabs"); card.Add(tabRow); }
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("gr-nb__body");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            card.Add(scroll);
            cardBody = scroll.contentContainer;
            return tab != null ? AddTab(tab) : cardBody;
        }

        /// <summary>A planning view's footer: Esc and the way back, a line of its own, and its main button.</summary>
        void NotebookFooter(string back, Action main = null, string mainText = null, string line = null)
        {
            var row = Box("gr-nb__foot");
            row.Add(L("Esc", "es-keycap"));
            var t = L(back, "gr-foot__text"); row.Add(t);
            if (line != null) row.Add(L(line, "gr-hint"));
            if (main != null) { var b = Button(mainText ?? back, main, true); b.style.marginLeft = 16; b.style.marginTop = 0; row.Add(b); }
            footer = row;
            card.Add(row);
        }

        bool notebook;
        /// <summary>The page shown of the open view's pager (the journal's families), kept while it stays open.</summary>
        int pageShown;

        /// <summary>
        /// Pages a list instead of scrolling it (the journal's families): each page built, one shown, with ◀ page n of m ▶
        /// under them.
        /// </summary>
        void Pager(VisualElement body, IList<VisualElement> pages, string what)
        {
            int at = pageShown = Mathf.Clamp(pageShown, 0, Mathf.Max(0, pages.Count - 1));
            var holder = Box("gr-pages");
            for (int i = 0; i < pages.Count; i++) { pages[i].AddToClassList("gr-page"); pages[i].style.display = i == at ? DisplayStyle.Flex : DisplayStyle.None; holder.Add(pages[i]); }
            body.Add(holder);
            if (pages.Count < 2) return;
            var nav = Box("gr-row", "gr-pager");
            nav.Add(PlusMinus("◀", () => pageShown = at - 1, at > 0, "The first page"));
            nav.Add(L(what + " · page " + (at + 1) + " of " + pages.Count, "gr-pager__words"));
            nav.Add(PlusMinus("▶", () => pageShown = at + 1, at < pages.Count - 1, "The last page"));
            body.Add(nav);
        }

        /// <summary>Whether something on the open card is on a tab or page not shown.</summary>
        bool Hidden(VisualElement e)
        {
            for (var v = e; v != null && v != card; v = v.parent) if (v.style.display.value == DisplayStyle.None) return true;
            return false;
        }

        /// <summary>Adds a tab and its body (built now, shown when chosen; the footer stays last).</summary>
        VisualElement AddTab(string name)
        {
            var body = new VisualElement { name = "gr-tab-" + name.ToLowerInvariant() };
            body.AddToClassList("gr-body");
            var b = new Button(() => { tabChosen = name; ShowTab(); }) { text = name };
            b.AddToClassList(notebook ? "gr-nb-tab" : "gr-tab");
            tabRow.Insert(tabList.Count, b);
            if (footer != null && footer.parent == cardBody) cardBody.Insert(cardBody.IndexOf(footer), body); else cardBody.Add(body);
            tabList.Add((b, body));
            tabRow.EnableInClassList("gr-tabs--many", !notebook && tabList.Count >= 4);
            ShowTab();
            return body;
        }

        int ShownTab()
        {
            int i = tabList.FindIndex(t => t.tab.text == tabChosen);
            return i < 0 ? 0 : i;
        }

        void ShowTab()
        {
            int on = ShownTab();
            for (int i = 0; i < tabList.Count; i++)
            {
                tabList[i].tab.EnableInClassList("gr-tab--on", i == on);
                tabList[i].body.style.display = i == on ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Validation and screenshots: shows the open card's tab of that name.</summary>
        public bool ShowTabNamed(string name)
        {
            if (tabList.All(t => t.tab.text != name)) return false;
            tabChosen = name; ShowTab();
            return true;
        }

        /// <summary>The card's footer: Esc and what it does, then the card's links (UX.3: a link to the journal, never its pages here).</summary>
        void Footer(VisualElement body, string hint = null, params (string text, Action click)[] links)
        {
            var row = new VisualElement(); row.AddToClassList("gr-foot");
            var key = new Label("Esc"); key.AddToClassList("es-keycap");
            var t = new Label("Back" + (string.IsNullOrEmpty(hint) ? "" : " · " + hint)); t.AddToClassList("gr-foot__text");
            row.Add(key); row.Add(t);
            foreach (var (text, click) in links) row.Add(Link(text, click));
            footer = row;
            (tabRow != null ? cardBody : body).Add(row);
        }

        // ---------------------------------------------------------------- small builders

        static Label L(string text, params string[] classes)
        {
            // GR-23: a key named in the words (E, Tab, the mouse) is the one the player chose.
            var l = new Label(GrowthControls.Keyed(text));
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static VisualElement Box(params string[] classes)
        {
            var v = new VisualElement();
            foreach (var c in classes) v.AddToClassList(c);
            return v;
        }

        /// <summary>A small capitalised heading above a group (UX.3: a label, not a sentence).</summary>
        static Label Eyebrow(string text) => L(text.ToUpperInvariant(), "gr-label");

        Button Link(string text, Action click, bool enabled = true, string tooltip = null)
        {
            var b = new Button(() => { if (enabled) { click(); RefreshCard(); } }) { text = text };
            b.AddToClassList("gr-link");
            b.SetEnabled(enabled);
            if (!enabled && tooltip != null) b.tooltip = tooltip;
            return b;
        }

        /// <summary>A button the full width of the card (the card's one main action).</summary>
        Button Wide(string text, Action click, bool primary, bool enabled, string why = null)
        {
            var b = Button(text, click, primary, enabled, enabled ? null : why);
            b.AddToClassList("gr-wide");
            b.RemoveFromClassList("es-button--compact");
            b.style.marginRight = 0;
            return b;
        }

        Button ChipButton(string text, Action click, bool enabled, string why = null, Color? swatch = null)
        {
            var b = new Button(() => { if (enabled) { click(); RefreshCard(); } }) { text = text };
            b.AddToClassList("gr-chip"); b.AddToClassList("gr-chip--button");
            b.SetEnabled(enabled);
            if (why != null) b.tooltip = why;
            if (swatch.HasValue) { b.style.borderLeftWidth = 8; b.style.borderLeftColor = swatch.Value; }
            return b;
        }

        static Label Chip(string text, bool miss = false, string tooltip = null)
        {
            var c = L(text, "gr-chip");
            if (miss) c.AddToClassList("gr-chip--miss");
            if (tooltip != null) c.tooltip = tooltip;
            return c;
        }

        static VisualElement Pip(Color c)
        {
            var p = Box("gr-pip");
            p.style.backgroundColor = c;
            return p;
        }

        /// <summary>A short meter of three: how close something is (UX.3: a meter, not a number).</summary>
        static VisualElement Meter(int lit, string tooltip = null)
        {
            var m = Box("gr-meter");
            for (int i = 0; i < 3; i++) { var seg = Box("gr-meter__seg"); if (i < lit) seg.AddToClassList("gr-meter__seg--on"); m.Add(seg); }
            if (tooltip != null) m.tooltip = tooltip;
            return m;
        }

        static string Stars(int n) => n + (n == 1 ? " star" : " stars");

        // ---------------------------------------------------------------- the Upgrade tab (GR.13, GR.9)

        /// <summary>
        /// GR-06's upgrade framework, GR-13's Upgrade tab on every station card: where the station is on a track of its
        /// levels (each level's change, cost and gate on hover), the next level spelled out with its price and gates as
        /// chips (a gate not met in coral), and its button, which says why it can't be pressed when pointed at. On a card
        /// without tabs (a station not bought yet) it is drawn in the card's body.
        /// </summary>
        void UpgradeSection(VisualElement body, string station, string title = null)
        {
            var target = tabList.Count == 0 ? body : tabList.FirstOrDefault(t => t.tab.text == UpgradeTab).body ?? AddTab(UpgradeTab);
            var shop = s.Shop;
            var def = GrowthContent.Station(station);
            int now = shop.Level(station);
            var failure = shop.UpgradeCheck(station, out var next, out string why);
            target.Add(UpgradeBlock((title ?? def.Name) + " · level " + now + " of " + def.Levels.Length, def.Levels, now, next, failure, why,
                now == 0 ? "Buy" : "Upgrade", () => s.Act(() => shop.Upgrade(station))));
        }

        static string Cost(LevelDef l) => l.Credits + " cr" + (l.Rp > 0 ? " and " + l.Rp + " RP" : "");

        static string LevelLine(LevelDef l, int now) =>
            l.Level + ". " + l.Change + (l.Level <= now ? " · done" : " · " + Cost(l) + ", era " + l.Era + (l.Stars > 0 ? ", " + Stars(l.Stars) : ""));

        VisualElement UpgradeBlock(string head, LevelDef[] levels, int now, LevelDef next, GrowthFailure failure, string why, string verb, Action buy)
        {
            var shop = s.Shop;
            var block = Box("gr-upgrade");
            block.Add(L(head, "gr-upgrade__head"));
            block.Add(Track(levels.Length, now, next?.Level ?? -1, n => LevelLine(levels.First(l => l.Level == n), now)));
            if (next == null) { block.Add(L("Highest level: every change is in.", "gr-hint")); return block; }
            var chips = Gates(next.Era, next.Stars, next.Credits, next.Rp, false);
            LockChip(chips, failure, why);
            block.Add(Benefit("Level " + next.Level, next.Change, null, chips));
            string reason = failure == GrowthFailure.NotEnoughCredits ? next.Credits - shop.Credits + " more credits" : why;
            block.Add(Wide(verb + " · " + Cost(next), buy, true, failure == GrowthFailure.None, reason));
            var after = levels.FirstOrDefault(l => l.Level == next.Level + 1);
            if (after != null) { var then = L("Then, at level " + after.Level + ": " + after.Change, "gr-hint"); then.style.unityTextAlign = TextAnchor.MiddleCenter; block.Add(then); }
            return block;
        }

        // ---------------------------------------------------------------- wave 2's pieces

        /// <summary>A track of levels: those done filled, the next ringed, each level's words on hover.</summary>
        static VisualElement Track(int count, int now, int next, Func<int, string> tip)
        {
            var track = Box("gr-track");
            for (int level = 1; level <= count; level++)
            {
                if (level > 1) { var line = Box("gr-lv-line"); if (level <= now) line.AddToClassList("gr-lv-line--done"); track.Add(line); }
                var dot = L(level.ToString(), "gr-lv");
                if (level <= now) dot.AddToClassList("gr-lv--done"); else if (level == next) dot.AddToClassList("gr-lv--next");
                dot.tooltip = tip(level);
                track.Add(dot);
            }
            return track;
        }

        /// <summary>What something needs, as chips: the era, stars, credits, research points and the night; one not met in coral.</summary>
        VisualElement Gates(int era, int stars, int credits, int rp, bool night)
        {
            var shop = s.Shop;
            var chips = Box("gr-chips");
            if (era > 0) chips.Add(Chip("Era " + era, shop.Era < era));
            if (stars > 0) chips.Add(Chip(Stars(stars), shop.Stars < stars));
            if (credits > 0) chips.Add(Chip(credits + " credits", shop.Credits < credits));
            if (rp > 0) chips.Add(Chip(rp + " RP", shop.Rp < rp));
            if (night) chips.Add(Chip("At night", shop.Phase != GrowthPhase.Night));
            return chips;
        }

        /// <summary>A lock the gates don't cover (busy, at night, another room first) is a chip of its own.</summary>
        static void LockChip(VisualElement chips, GrowthFailure failure, string why)
        {
            if (failure == GrowthFailure.None || failure == GrowthFailure.NotAtNight && chips.Children().OfType<Label>().Any(c => c.text == "At night")) return;
            if (why != null && !why.StartsWith("Needs ")) chips.Add(Chip(why, true));
        }

        /// <summary>A panel saying what something gives: an eyebrow, a title, a line and its chips.</summary>
        static VisualElement Benefit(string eyebrow, string title, string line, VisualElement chips)
        {
            var benefit = Box("gr-benefit");
            if (eyebrow != null) benefit.Add(Eyebrow(eyebrow));
            benefit.Add(L(title, "gr-strong"));
            if (line != null) { var h = L(line, "gr-hint"); h.style.marginTop = 4; benefit.Add(h); }
            if (chips != null) benefit.Add(chips);
            return benefit;
        }

        /// <summary>
        /// GR-13's purchase on a card (a room, a place's buffer or rack): what it gives in a panel with its gates as chips,
        /// and its button, which says why on hover; a purchase that opens something closes the card.
        /// </summary>
        void BuyPanel(VisualElement body, string eyebrow, string title, string line, VisualElement gates, GrowthFailure failure, string why, string button, Func<GrowthResult> buy, bool close = true)
        {
            LockChip(gates, failure, why);
            body.Add(Benefit(eyebrow, title, line, gates));
            body.Add(Wide(button, () => { if (s.Act(buy).Success && close) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None, why));
        }

        /// <summary>A status as a pill in words: 0 going, 1 slowed, 2 waiting.</summary>
        static Label Pill(string text, int tone)
        {
            var p = L(text, "gr-pill");
            p.AddToClassList(tone == 0 ? "gr-pill--go" : tone == 1 ? "gr-pill--slow" : "gr-pill--wait");
            return p;
        }

        /// <summary>A bar read at a glance: a part filled in a colour, and optionally a second part after it in another.</summary>
        static VisualElement Bar(float part01, Color fill, float second01 = 0, Color? second = null)
        {
            var bar = Box("gr-bar");
            var a = Box("gr-bar__fill"); a.style.width = new Length(Mathf.Clamp01(part01) * 100, LengthUnit.Percent); a.style.backgroundColor = fill; bar.Add(a);
            if (second01 > 0 && second.HasValue) { var b = Box("gr-bar__fill"); b.style.width = new Length(Mathf.Clamp01(second01) * 100, LengthUnit.Percent); b.style.backgroundColor = second.Value; bar.Add(b); }
            return bar;
        }

        /// <summary>A row on a card: a title and a line, and what can be done with it at its end.</summary>
        static VisualElement Item(string title, string line, params VisualElement[] end)
        {
            var row = Box("gr-item");
            var words = Box("gr-result__text");
            words.Add(L(title, "gr-item__title"));
            if (!string.IsNullOrEmpty(line)) words.Add(L(line, "gr-hint"));
            row.Add(words);
            foreach (var e in end) if (e != null) row.Add(e);
            return row;
        }

        /// <summary>A laid pipe or chute: where it runs and how it is doing, and Take it down at night.</summary>
        VisualElement PipeItem(string title, string status, int id)
        {
            var shop = s.Shop;
            bool night = shop.Phase == GrowthPhase.Night;
            var take = Link("Take it down", () => s.Act(() => shop.RemovePipe(id)), night, "Pipes are taken down at night");
            return Item(title, status, take);
        }

        /// <summary>A big number with a word under it (the generator's units, the counter's stock).</summary>
        static VisualElement Stat(string number, string words)
        {
            var stat = Box("gr-stat");
            stat.Add(L(number, "gr-stat__number"));
            stat.Add(L(words, "gr-hint"));
            return stat;
        }

        /// <summary>A picker: ◀ the shown name and its line ▶, stepping through a list (the planner's two, the targets' species).</summary>
        VisualElement Picker(List<string> ids, string now, Action<string> set, Func<string, string> line)
        {
            int at = ids.IndexOf(now);
            var pick = Box("gr-picker");
            pick.Add(PlusMinus("◀", () => set(ids[(at + ids.Count - 1) % ids.Count]), ids.Count > 1, "Only one to choose"));
            var words = Box("gr-picker__words");
            var def = GrowthContent.Get(now);
            var g = new GrowthGlyph(GrowthCreatureView.ColourOf(def)); g.AddToClassList("gr-tile__glyph"); words.Add(g);
            words.Add(L(def.Name, "gr-tile__name"));
            if (line != null) words.Add(L(line(now), "gr-dose__count"));
            pick.Add(words);
            pick.Add(PlusMinus("▶", () => set(ids[(at + 1) % ids.Count]), ids.Count > 1, "Only one to choose"));
            return pick;
        }

        // ---------------------------------------------------------------- the hover tip (UX.3.3)

        void BuildTip()
        {
            tip = new Label { name = "gr-tip", pickingMode = PickingMode.Ignore };
            tip.AddToClassList("es-press-tip");
            tip.style.display = DisplayStyle.None;
            var layer = Layer("gr-tips");
            layer.Add(tip);
        }

        /// <summary>Runtime UI Toolkit shows no tooltips: the element under the pointer that carries one shows it in a tip above it.</summary>
        void UpdateTip()
        {
            if (tip == null) return;
            VisualElement over = null;
            var mouse = Mouse.current;
            Vector2 panel = root.layout.size;
            if (card != null && UnityEngine.Cursor.visible && mouse != null && root.panel != null && Screen.width > 0 && Screen.height > 0 && !float.IsNaN(panel.x) && panel.x > 0)
            {
                Vector2 at = mouse.position.ReadValue();
                var point = new Vector2(at.x * panel.x / Screen.width, (Screen.height - at.y) * panel.y / Screen.height);
                for (var v = root.panel.Pick(point); v != null && v != root; v = v.parent)
                    if (!string.IsNullOrEmpty(v.tooltip)) { over = v; break; }
            }
            if (over == null) { tip.style.display = DisplayStyle.None; tipAnchor = null; return; }
            if (over != tipAnchor) { tip.text = over.tooltip; tipAnchor = over; }
            tip.style.display = DisplayStyle.Flex;
            Rect a = over.worldBound;
            float w = tip.layout.width, h = tip.layout.height;
            if (float.IsNaN(w) || w <= 1) { w = 320f; h = 52f; }
            const float gap = 10f, margin = 12f;
            tip.style.left = Mathf.Clamp(a.center.x - w * .5f, margin, Mathf.Max(margin, panel.x - margin - w));
            float y = a.yMin - gap - h;
            tip.style.top = y < margin ? Mathf.Min(a.yMax + gap, Mathf.Max(margin, panel.y - margin - h)) : y;
        }

        // ---------------------------------------------------------------- the journal links

        /// <summary>Opens the journal at a section (the mix and pair logs live there, GR-13).</summary>
        void OpenJournalAt(string section)
        {
            s.SetCard(GrowthCard.Journal);
            pendingScroll = section;
        }

        /// <summary>A scroll asked for before the card was laid out happens once it is.</summary>
        void ApplyPendingScroll()
        {
            if (pendingScroll == null || card == null) return;
            var label = card.Query<Label>().ToList().FirstOrDefault(l => l.text.StartsWith(pendingScroll));
            if (label == null) { pendingScroll = null; return; }
            // On a tab or page not shown, ScrollTo shows it first and asks again.
            if (Hidden(label)) { ScrollTo(pendingScroll); return; }
            if (float.IsNaN(label.layout.height) || label.layout.height <= 0) return;
            string want = pendingScroll; pendingScroll = null;
            ScrollTo(want);
        }
    }

    /// <summary>GR-13: a creature's glyph, a small fish in its colour (grey with a "?" feel when unknown).</summary>
    public sealed class GrowthGlyph : VisualElement
    {
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        readonly Color fill;

        public GrowthGlyph(Color fill)
        {
            this.fill = fill;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            // The mockups' fish (a 120 × 70 drawing), fitted to the element and centred.
            float k = Mathf.Min(r.width / 120f, r.height / 70f);
            Vector2 o = new Vector2(r.center.x - 60f * k, r.center.y - 35f * k);
            Vector2 P(float x, float y) => o + new Vector2(x * k, y * k);
            var p = context.painter2D;
            p.fillColor = fill;
            p.BeginPath(); p.MoveTo(P(8, 35));
            p.BezierCurveTo(P(26, 11), P(60, 5), P(82, 17));
            p.BezierCurveTo(P(91, 22), P(98, 28), P(102, 35));
            p.BezierCurveTo(P(98, 42), P(91, 48), P(82, 53));
            p.BezierCurveTo(P(60, 65), P(26, 59), P(8, 35));
            p.ClosePath(); p.Fill();
            p.BeginPath(); p.MoveTo(P(96, 35)); p.LineTo(P(118, 15)); p.LineTo(P(118, 55)); p.ClosePath(); p.Fill();
            p.fillColor = Color.white;
            p.BeginPath(); p.Arc(P(34, 30), 6 * k, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.fillColor = Ink;
            p.BeginPath(); p.Arc(P(32, 30), 3 * k, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
        }
    }

    /// <summary>GR-13: a timer as a ring that fills, with the time left in the middle.</summary>
    public sealed class GrowthRing : VisualElement
    {
        static readonly Color Track = new Color(0.894f, 0.847f, 0.729f);
        static readonly Color Teal = new Color(0.13f, 0.37f, 0.35f);
        float progress;
        public readonly Label TimeLabel;

        readonly bool small;

        public GrowthRing(bool small = false)
        {
            this.small = small;
            AddToClassList("gr-ring");
            if (small) AddToClassList("gr-ring--small");
            pickingMode = PickingMode.Ignore;
            TimeLabel = new Label(); TimeLabel.AddToClassList("gr-ring__time");
            Add(TimeLabel);
            generateVisualContent += Draw;
        }

        public void Set(float progress01, string time)
        {
            progress01 = Mathf.Clamp01(progress01);
            if (!Mathf.Approximately(progress, progress01)) { progress = progress01; MarkDirtyRepaint(); }
            if (TimeLabel.text != time) TimeLabel.text = time;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f - (small ? 6f : 8f);
            var p = context.painter2D;
            p.lineWidth = small ? 10f : 14f; p.lineCap = LineCap.Round;
            p.strokeColor = Track;
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (progress <= 0.001f) return;
            p.strokeColor = Teal;
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(-90), Angle.Degrees(-90 + 360f * progress)); p.Stroke();
        }
    }

    /// <summary>GR-13: a container's comfort as a face: 2 happy, 1 settled, 0 uneasy.</summary>
    public sealed class GrowthFace : VisualElement
    {
        static readonly Color Ink = new Color(0.105f, 0.255f, 0.245f);
        readonly int mood;

        public GrowthFace(int mood)
        {
            this.mood = Mathf.Clamp(mood, 0, 2);
            AddToClassList("gr-face");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            Vector2 c = r.center;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - 2f;
            var p = context.painter2D;
            p.fillColor = mood == 2 ? new Color(0.98f, 0.78f, 0.37f) : mood == 1 ? new Color(0.84f, 0.89f, 0.86f) : new Color(0.97f, 0.83f, 0.76f);
            p.strokeColor = Ink; p.lineWidth = 3f;
            p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.Stroke();
            p.fillColor = Ink;
            p.BeginPath(); p.Arc(c + new Vector2(-rad * .36f, -rad * .2f), rad * .11f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.BeginPath(); p.Arc(c + new Vector2(rad * .36f, -rad * .2f), rad * .11f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.lineWidth = 3f; p.lineCap = LineCap.Round;
            float y = c.y + rad * .32f, bend = mood == 2 ? rad * .3f : mood == 0 ? -rad * .26f : 0;
            p.BeginPath(); p.MoveTo(new Vector2(c.x - rad * .42f, y - bend * .4f));
            p.QuadraticCurveTo(new Vector2(c.x, y + bend), new Vector2(c.x + rad * .42f, y - bend * .4f));
            p.Stroke();
        }
    }
}
