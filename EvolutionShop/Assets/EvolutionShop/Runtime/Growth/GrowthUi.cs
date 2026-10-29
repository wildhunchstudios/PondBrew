using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-02's interface in the game's UI Toolkit style (the UX tokens, HUD and card sheets, and GR-13's GrowthStyles):
    /// the HUD (the day chip, money with the stars, one next-step line, and what the siphon and hands hold while they
    /// hold something), the prompt under the reticle with its hold progress, one note at a time, station cards with their
    /// tabs (GR.13: the incubator's recipes, doses and "close to something new"; a container's occupants as space icons
    /// and its comfort; the breeding tank's pair, timer and result; Upgrade on every station), and the journal, night
    /// receipt, welcome and pause screens. The words in English only.
    /// </summary>
    public sealed partial class GrowthUi : MonoBehaviour
    {
        public const string GrowthStylesResource = ShopUiRoot.ResourceFolder + "GrowthStyles";
        GrowthSession s;
        UIDocument document;
        PanelSettings settings;
        ShopUiTheme theme;
        VisualElement root, hud, cardLayer, screenLayer, promptBox, promptProgress, card, dayIcon, nextBox, carryBox, toastBox, eventBox, modeBox, modeLay, slapBox, captionBox;
        readonly List<VisualElement> stars = new List<VisualElement>();
        bool nextShown;
        int nextRemeasure;
        Label dayLabel, clockLabel, creditsLabel, rpLabel, nextLabel, promptName, promptStatus, siphonLabel, carryLabel, hintLabel, toastLabel, eventLabel, modeTitle, modeDetail, slapKey, slapName, slapWho, captionWho, captionText;
        TextElement liveLabel;
        Texture2D dayTexture;
        float hintUntil, toastUntil, eventUntil, refreshClock;
        string cardSignature;
        public bool Ready { get; private set; }
        public VisualElement Root => root;
        public VisualElement CardElement => card;
        /// <summary>Notes shown so far (validation reads them).</summary>
        public readonly List<string> Log = new List<string>();
        /// <summary>The HUD's next-step line and the note showing now (validation).</summary>
        public string HudNext => nextLabel == null ? "" : nextLabel.text;
        public string HudNote => toastBox != null && toastBox.style.display == DisplayStyle.Flex ? toastLabel.text : null;

        public void Initialize(GrowthSession session)
        {
            s = session;
            var theme = Resources.Load<ThemeStyleSheet>(ShopUiRoot.ThemeResource);
            var tokens = Resources.Load<StyleSheet>(ShopUiRoot.TokensResource);
            var hudStyles = Resources.Load<StyleSheet>(ShopUiRoot.HudStylesResource);
            var cardStyles = Resources.Load<StyleSheet>(ShopUiRoot.CardStylesResource);
            var screenStyles = Resources.Load<StyleSheet>(ShopUiRoot.ScreenStylesResource);
            var growthStyles = Resources.Load<StyleSheet>(GrowthStylesResource);
            if (!theme || !tokens || !hudStyles || !cardStyles || !screenStyles || !growthStyles) { Debug.LogError("GR-02: a UI Toolkit resource is missing"); return; }
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "GR-02 greybox UI";
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ShopUiRoot.ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = ShopUiRoot.ScreenMatch;
            settings.sortingOrder = ShopUiRoot.SortingOrder;
            var holder = new GameObject("GR-02 greybox UI");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            document = holder.AddComponent<UIDocument>();
            document.panelSettings = settings;
            holder.SetActive(true);
            root = document.rootVisualElement;
            foreach (var sheet in new[] { tokens, hudStyles, cardStyles, screenStyles, growthStyles }) root.styleSheets.Add(sheet);
            root.pickingMode = PickingMode.Ignore;
            hud = Layer("gr-hud"); cardLayer = Layer("gr-cards"); screenLayer = Layer("gr-screens");
            this.theme = new ShopUiTheme();
            BuildHud();
            BuildTip();
            Ready = true;
        }

        void OnDestroy()
        {
            theme?.Dispose();
            theme = null;
        }

        VisualElement Layer(string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.AddToClassList("es-layer");
            root.Add(layer);
            return layer;
        }

        static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            l.AddToClassList("es-text");
            foreach (var c in classes) l.AddToClassList(c);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        Button Button(string text, Action click, bool primary = false, bool enabled = true, string tooltip = null)
        {
            var b = new Button(() => { if (enabled) { click(); RefreshCard(); } }) { text = text };
            b.AddToClassList("es-button");
            b.AddToClassList(primary ? "es-button--primary" : "es-button--secondary");
            b.AddToClassList("es-button--compact");
            b.style.marginTop = 6; b.style.marginRight = 6;
            b.SetEnabled(enabled);
            if (tooltip != null) b.tooltip = tooltip;
            return b;
        }

        Texture2D Icon(ShopUiTheme.Symbol symbol) => theme?.IconTexture(symbol);

        // ---------------------------------------------------------------- the HUD

        /// <summary>
        /// GR-13 (UX.4): three fixed elements, the day chip, money with the stars and one next-step line (the goal's full
        /// list is on the pinboard and in the journal); what the siphon and the hands hold shows only while they hold
        /// something; notes come one at a time at the top, a big moment above them, clear of the station cards.
        /// </summary>
        void BuildHud()
        {
            hud.Add(Box("es-crosshair"));
            var day = Box("es-chip", "es-chip--day");
            dayIcon = Box("es-chip__icon");
            var dayText = Box("es-chip__text");
            dayLabel = L("Night 1", "es-chip__title"); clockLabel = L("", "es-chip__detail");
            dayText.Add(dayLabel); dayText.Add(clockLabel);
            day.Add(dayIcon); day.Add(dayText); hud.Add(day);
            var money = Box("es-chip", "es-chip--money", "gr-chip--money");
            var coin = Box("es-chip__icon"); coin.style.backgroundImage = new StyleBackground(Icon(ShopUiTheme.Symbol.Coin));
            var moneyText = Box("gr-chip__money");
            creditsLabel = L("10", "es-chip__amount");
            var starRow = Box("gr-stars");
            for (int i = 0; i < 5; i++) { var star = Box("gr-star"); star.style.backgroundImage = new StyleBackground(Icon(ShopUiTheme.Symbol.Star)); stars.Add(star); starRow.Add(star); }
            rpLabel = L("", "gr-stars__rp"); starRow.Add(rpLabel);
            moneyText.Add(creditsLabel); moneyText.Add(starRow);
            money.Add(coin); money.Add(moneyText); hud.Add(money);
            nextBox = Box("es-next", "gr-next");
            var nextCard = Box("es-next__card");
            var leaf = Box("es-next__icon"); leaf.style.backgroundImage = new StyleBackground(Icon(ShopUiTheme.Symbol.Leaf));
            nextLabel = L("", "es-next__text");
            nextCard.Add(leaf); nextCard.Add(nextLabel); nextBox.Add(nextCard); hud.Add(nextBox);
            carryBox = Box("gr-carry");
            siphonLabel = L("", "gr-carry__line"); carryLabel = L("", "gr-carry__line");
            carryBox.Add(siphonLabel); carryBox.Add(carryLabel); hud.Add(carryBox);
            var prompts = Box("es-prompts"); hud.Add(prompts);
            promptBox = Box("es-prompt");
            promptName = L("", "es-prompt__name");
            promptStatus = L("", "es-prompt__status");
            promptProgress = Box("es-prompt__progress");
            promptBox.Add(promptName); promptBox.Add(promptStatus); promptBox.Add(promptProgress);
            prompts.Add(promptBox);
            // GR-18 wave 4 (§6.7): the slap's own prompt under the reticle's, with its key, when a visitor is in reach.
            slapBox = Box("es-prompt", "es-prompt--slap");
            slapKey = L("F", "es-keycap"); slapName = L("Slap", "es-prompt__name"); slapWho = L("", "es-prompt__status");
            slapBox.Add(slapKey); slapBox.Add(slapName); slapBox.Add(slapWho);
            slapBox.style.display = DisplayStyle.None;
            prompts.Add(slapBox);
            hintLabel = Text(""); hintLabel.style.color = new Color(1f, 0.92f, 0.75f); hintLabel.style.fontSize = 18; hintLabel.style.marginLeft = 8;
            prompts.Add(hintLabel);
            // GR-13: pipe and chute placement as a mode bar at the bottom: what is being laid, its length and price, and the keys.
            modeBox = Box("gr-mode");
            var modeWords = Box("gr-mode__words");
            modeTitle = L("", "gr-mode__title"); modeDetail = L("", "gr-mode__detail");
            modeWords.Add(modeTitle); modeWords.Add(modeDetail);
            modeBox.Add(modeWords);
            modeLay = ModeKey("E", "Lay it"); modeBox.Add(modeLay);
            modeBox.Add(ModeKey("Esc", "Done"));
            modeBox.style.display = DisplayStyle.None;
            hud.Add(modeBox);
            // GR-18 wave 4: the silly moments' captions, above the next-step line (who says it, and what).
            captionBox = Box("gr-caption");
            captionWho = L("", "gr-caption__who"); captionText = L("", "gr-caption__text");
            captionBox.Add(captionWho); captionBox.Add(captionText);
            captionBox.style.display = DisplayStyle.None;
            hud.Add(captionBox);
            var top = Box("es-hud-top");
            eventBox = Note("gr-toast--event", ShopUiTheme.Symbol.Star, out eventLabel);
            toastBox = Note(null, ShopUiTheme.Symbol.Leaf, out toastLabel);
            top.Add(eventBox); top.Add(toastBox); hud.Add(top);
            BuildGuide();
        }

        static VisualElement ModeKey(string key, string words)
        {
            var k = Box("gr-mode__key");
            k.Add(L(key, "es-keycap"));
            k.Add(L(words, "gr-mode__keytext"));
            return k;
        }

        VisualElement Note(string modifier, ShopUiTheme.Symbol symbol, out Label label)
        {
            var note = Box("es-toast", "gr-note");
            if (modifier != null) note.AddToClassList(modifier);
            var icon = Box("es-toast__icon"); icon.style.backgroundImage = new StyleBackground(Icon(symbol));
            label = L("", "es-toast__text");
            note.Add(icon); note.Add(label);
            note.style.display = DisplayStyle.None;
            return note;
        }

        public static string StarsText(int stars) => new string('★', stars) + new string('☆', 5 - stars);

        public void Tick(float dt)
        {
            if (!Ready) return;
            var shop = s.Shop;
            bool night = shop.Phase == GrowthPhase.Night;
            dayLabel.text = (night ? "Night " : "Day ") + shop.Day + " · Era " + shop.Era;
            var dayIconNow = Icon(night ? ShopUiTheme.Symbol.Moon : ShopUiTheme.Symbol.Sun);
            if (dayIconNow != dayTexture) { dayTexture = dayIconNow; dayIcon.style.backgroundImage = new StyleBackground(dayIconNow); }
            clockLabel.text = !night ? ShopClock(shop.DaySecondsLeft) + " left · " + shop.Queue.Count() + " waiting" + (shop.ExhibitionToday ? " · exhibition guests " + shop.GuestsServed + "/" + GrowthShop.ExhibitionPurchases : "") :
                shop.ExhibitionPrepared ? "the Grand Exhibition opens with the shop" : "no rush";
            creditsLabel.text = shop.Credits.ToString();
            for (int i = 0; i < stars.Count; i++) stars[i].style.unityBackgroundImageTintColor = i < shop.Stars ? Color.white : new Color(0.78f, 0.72f, 0.58f, 0.45f);
            rpLabel.text = shop.Rp > 0 ? shop.Rp + " RP" : "";
            // GR-13: one next step, the first goal still to do (the pinboard and the journal have the whole list); during the
            // guided first night (GR-14), the guide's step.
            var guide = shop.GuideNow();
            string next = guide?.Text ?? shop.Objectives().Where(g => !g.done).Select(g => g.text).FirstOrDefault() ?? shop.EraGoal;
            bool free = s.Card == GrowthCard.None;
            // A label measured while hidden, or before its font had drawn its glyphs, keeps that width: measure it again for the
            // frames after it changes or shows (setting its words anew makes it measure itself again).
            if (next != nextLabel.text || free && !nextShown) nextRemeasure = 30;
            else if (nextRemeasure > 0) { nextRemeasure--; nextLabel.text = ""; }
            nextLabel.text = next; nextShown = free;
            hud.style.display = s.Card == GrowthCard.Pause || s.Card == GrowthCard.Welcome ? DisplayStyle.None : DisplayStyle.Flex;
            nextBox.style.display = free ? DisplayStyle.Flex : DisplayStyle.None;
            siphonLabel.text = shop.Held.Count == 0 ? "" : "Siphon " + shop.Held.Count + "/" + shop.SiphonCapacity + ": " + string.Join(", ", shop.Held.GroupBy(c => c).Select(g => g.Count() + " " + GrowthShop.Name(g.Key)));
            carryLabel.text = shop.Carried.Count == 0 ? "" : "Carrying " + shop.Carried.Count + "/" + GrowthShop.CarryLimit + ": " + string.Join(", ", shop.Carried.Select(GrowthSession.Named));
            siphonLabel.style.display = shop.Held.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            carryLabel.style.display = shop.Carried.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            carryBox.style.display = free && (shop.Held.Count > 0 || shop.Carried.Count > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            bool piping = free && s.PipeFrom != null;
            modeBox.style.display = piping ? DisplayStyle.Flex : DisplayStyle.None;
            if (piping)
            {
                modeTitle.text = s.FocusName(out _);
                modeDetail.text = s.PipeTo == null ? "Aim at its other end" : s.PipePlan == null ? s.PipeWhy :
                    "to " + shop.EndName(s.PipeTo) + " · " + s.PipePlan.Length.ToString("0.#") + " m · " + s.PipePlan.Paid + " credits" + (s.PipeReady ? "" : " · " + s.PipeWhy);
                modeLay.style.display = s.PipeReady ? DisplayStyle.Flex : DisplayStyle.None;
                nextBox.style.display = DisplayStyle.None;
            }
            TickGuideMarker(guide, free && !piping);
            string name = free && !piping ? s.FocusName(out string status) : null;
            promptBox.style.display = name == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (name != null)
            {
                promptName.text = name; promptStatus.text = s.FocusName(out status) != null ? status : "";
                float collect = s.Focused != null && s.Focused.Kind == GrowthTargetKind.Vial ? shop.VialProgress : s.Focused != null && s.Focused.Kind == GrowthTargetKind.Buffer ? shop.BufferProgress : shop.CollectProgress;
                float progress = s.CollectingNow ? collect / Mathf.Max(0.01f, shop.HoldSeconds) : s.ServingNow ? shop.ServeProgress / Mathf.Max(0.01f, shop.ServeSeconds) :
                    s.PumpingNow ? shop.PumpProgress / Mathf.Max(0.01f, shop.PumpDoseSeconds) : 0;
                promptProgress.style.width = new Length(Mathf.Clamp01(progress) * 100, LengthUnit.Percent);
            }
            // GR-18 wave 4: a caption measured before its font had drawn its glyphs keeps that width: measure it again for the
            // frames after it changes (as the next-step line does).
            if (captionRemeasure > 0) { captionRemeasure--; var words = captionText.text; captionText.text = ""; captionText.text = words; }
            // GR-18 wave 4: the slap's prompt while a visitor is in reach under the reticle.
            var slapAt = free && !piping && s.Gags != null ? s.Gags.SlapTarget : null;
            slapBox.style.display = slapAt != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (slapAt != null)
            {
                slapKey.text = GrowthGags.KeyName(s.SlapKey);
                slapWho.text = "the " + GrowthShop.TypeName(shop.Visitors.FirstOrDefault(v => v.Id == slapAt.VisitorId)?.Type).ToLowerInvariant();
            }
            if (Time.unscaledTime > hintUntil) hintLabel.text = "";
            if (Time.unscaledTime > eventUntil) eventBox.style.display = DisplayStyle.None;
            if (Time.unscaledTime > toastUntil) toastBox.style.display = DisplayStyle.None;
            refreshClock += dt;
            if (refreshClock >= 0.1f) { refreshClock = 0; if (card != null && Signature() != cardSignature) RefreshCard(); }
            if (liveLabel != null) liveLabel.text = LiveText();
            TickRings();
            ApplyPendingScroll();
            UpdateTip();
        }

        static string ShopClock(float seconds) { int t = Mathf.Max(0, Mathf.CeilToInt(seconds)); return t / 60 + ":" + (t % 60).ToString("00"); }

        public void Hint(string text) { hintLabel.text = text; hintUntil = Time.unscaledTime + 1.5f; }

        /// <summary>GR-18 wave 4 (§6.7): a silly moment's caption (who says it, and what) until it ends (every caption stays in <see cref="Log"/>).</summary>
        public void Caption(string who, string words)
        {
            Log.Add((who != null ? who + ": " : "") + words);
            if (!Ready) return;
            captionWho.text = who ?? ""; captionText.text = words;
            captionWho.style.display = string.IsNullOrEmpty(who) ? DisplayStyle.None : DisplayStyle.Flex;
            captionBox.style.display = DisplayStyle.Flex;
            captionRemeasure = 30;
        }

        int captionRemeasure;

        public void ClearCaption() { if (Ready) captionBox.style.display = DisplayStyle.None; }

        /// <summary>Validation: the caption showing ("who: words"), or null.</summary>
        public string CaptionShown => Ready && captionBox.style.display == DisplayStyle.Flex ? (captionWho.text.Length > 0 ? captionWho.text + ": " : "") + captionText.text : null;
        /// <summary>Validation: whether the slap's prompt shows, and its key.</summary>
        public bool SlapPromptShown => Ready && slapBox.style.display == DisplayStyle.Flex;
        public string SlapPromptKey => Ready ? slapKey.text : null;

        /// <summary>A note: one at a time at the top, the newest replacing the last (every note stays in <see cref="Log"/>).</summary>
        public void Toast(string text, bool warn = false)
        {
            Log.Add(text);
            if (!Ready) return;
            toastLabel.text = text;
            toastBox.EnableInClassList("es-toast--error", warn);
            toastBox.style.display = DisplayStyle.Flex;
            toastUntil = Time.unscaledTime + (warn ? 3.5f : 3f);
        }

        public void Event(GrowthEvent e)
        {
            switch (e.Kind)
            {
                case GrowthEventKind.Collected:
                case GrowthEventKind.Pumped:
                case GrowthEventKind.Sorted: return;
                case GrowthEventKind.Discovery:
                case GrowthEventKind.EraUp:
                case GrowthEventKind.StarUp:
                case GrowthEventKind.CultureOwned:
                case GrowthEventKind.NewColour:
                case GrowthEventKind.LineDay:
                case GrowthEventKind.Opened:
                case GrowthEventKind.ExhibitionWon:
                    // A big moment sits above the notes at the top, clear of the station cards (GR-13).
                    Log.Add(e.Text);
                    if (!Ready) return;
                    eventLabel.text = e.Text; eventBox.style.display = DisplayStyle.Flex; eventUntil = Time.unscaledTime + 3.5f;
                    return;
                default: Toast(e.Text, e.Kind == GrowthEventKind.Refusal || e.Kind == GrowthEventKind.Impatient || e.Kind == GrowthEventKind.StaffHome || e.Kind == GrowthEventKind.ExhibitionFailed || e.Kind == GrowthEventKind.Slapped && e.Text.Contains("huff")); return;
            }
        }

        // ---------------------------------------------------------------- cards

        public void ShowCard(GrowthCard which, string target)
        {
            if (!Ready) return;
            // A card's chosen tab, its open Move list and a Sell waiting for its yes last while it stays open (refreshes rebuild it).
            string key = which + "|" + target;
            if (key != tabCard) { tabCard = key; tabChosen = null; moveOpen = false; sellAsked = false; pageShown = 0; }
            cardLayer.Clear(); screenLayer.Clear(); card = null; liveLabel = null; statusLabel = null;
            tabList.Clear(); tabRow = null; footer = null; rings.Clear(); notebook = false;
            tip.style.display = DisplayStyle.None; tipAnchor = null;
            if (which == GrowthCard.None) return;
            // GR-13: the planning views (the pinboard, the catalog, the journal, the receipt, the results) and the welcome and
            // pause screens are notebooks over the dimmed shop; which of them pause the game is the session's, unchanged.
            bool screen = which == GrowthCard.Journal || which == GrowthCard.Receipt || which == GrowthCard.Welcome || which == GrowthCard.Pause || which == GrowthCard.Results || which == GrowthCard.Board || which == GrowthCard.Catalog;
            card = screen ? ScreenCard(which) : StationCard(which == GrowthCard.Incubator || which == GrowthCard.Tank || which == GrowthCard.Crate || which == GrowthCard.Catalog || which == GrowthCard.Breeding || which == GrowthCard.Helper ? 580 : which == GrowthCard.Place || which == GrowthCard.Board || which == GrowthCard.Workbench || which == GrowthCard.Counter || which == GrowthCard.Grower || which == GrowthCard.Buffer || which == GrowthCard.Staff ? 540 : which == GrowthCard.PowerBoard || which == GrowthCard.Sorter ? 600 : 470);
            (screen ? screenLayer : cardLayer).Add(screen ? Dim(card) : card);
            cardSignature = Signature();
            Fill(which, target);
        }

        public void RefreshCard() { if (card != null) ShowCard(s.Card, s.CardTarget); }

        /// <summary>
        /// Validation: scrolls the open card to its first label that starts with the text (journal pages below the fold),
        /// first showing the tab it is on (GR-13), in which case the scroll waits for that tab's layout.
        /// </summary>
        public bool ScrollTo(string startsWith)
        {
            if (card == null) return false;
            var label = card.Query<Label>().ToList().FirstOrDefault(l => l.text.StartsWith(startsWith));
            var scroll = card.Q<ScrollView>();
            if (label == null || scroll == null) return false;
            var on = tabList.FirstOrDefault(t => t.body.Contains(label));
            if (on.tab != null && on.tab.text != CurrentTab) { ShowTabNamed(on.tab.text); pendingScroll = startsWith; return true; }
            // On a page not shown (the journal's families), that page first.
            for (var v = (VisualElement)label; v != null && v != card; v = v.parent)
                if (v.ClassListContains("gr-page") && v.style.display.value == DisplayStyle.None) { pageShown = v.parent.IndexOf(v); pendingScroll = startsWith; RefreshCard(); return true; }
            float y = label.worldBound.y - scroll.contentContainer.worldBound.y;
            scroll.scrollOffset = new Vector2(0, Mathf.Max(0, y - 8));
            return true;
        }

        /// <summary>
        /// Every label and button text on the open card or screen, every tab's included, and every hover text (why a
        /// control can't be used, the levels on the Upgrade tab's track, the rules on the "?"), for the validation route.
        /// </summary>
        public string CardText() => card == null ? "" : string.Join(" | ", card.Query<VisualElement>().ToList().SelectMany(Texts));

        static IEnumerable<string> Texts(VisualElement v)
        {
            if (v is TextElement t && !string.IsNullOrEmpty(t.text)) yield return t.text;
            if (!string.IsNullOrEmpty(v.tooltip)) yield return v.tooltip;
        }

        VisualElement Dim(VisualElement content)
        {
            var dim = new VisualElement(); dim.AddToClassList("es-screen");
            dim.style.backgroundColor = new Color(0.07f, 0.16f, 0.15f, 0.55f);
            dim.Add(content);
            return dim;
        }

        VisualElement StationCard(float width)
        {
            var c = new VisualElement(); c.AddToClassList("es-card");
            c.style.right = 64; c.style.top = 140; c.style.width = width; c.style.maxHeight = new Length(82, LengthUnit.Percent);
            c.pickingMode = PickingMode.Position;
            return c;
        }

        /// <summary>A notebook's sheet: the planning views wide and tall enough that nothing scrolls, the rest narrower.</summary>
        VisualElement ScreenCard(GrowthCard which)
        {
            var c = new VisualElement(); c.AddToClassList("gr-notebook");
            bool wide = which == GrowthCard.Journal || which == GrowthCard.Board || which == GrowthCard.Catalog || which == GrowthCard.Results;
            c.style.width = wide ? 1620 : which == GrowthCard.Receipt ? 820 : 1000;
            if (wide) c.style.height = 860;
            c.style.maxHeight = new Length(86, LengthUnit.Percent);
            c.pickingMode = PickingMode.Position;
            return c;
        }

        static VisualElement Row()
        {
            var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.flexWrap = Wrap.Wrap; r.style.alignItems = Align.Center;
            return r;
        }

        static VisualElement Dot(Color c, float size = 22)
        {
            var d = new VisualElement();
            d.style.width = d.style.height = size;
            d.style.backgroundColor = c;
            d.style.borderTopLeftRadius = d.style.borderTopRightRadius = d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = size / 2;
            d.style.marginRight = 4;
            return d;
        }

        static Color CultureColour(string culture)
        {
            ColorUtility.TryParseHtmlString(GrowthContent.Cultures[GrowthContent.CultureIndex(culture)].Hex, out var c);
            return c;
        }

        /// <summary>A culture's colour faded toward the paper, for a dose chip's ground.</summary>
        static Color CultureGround(string culture) => Color.Lerp(CultureColour(culture), Color.white, 0.74f);

        string Signature()
        {
            var shop = s.Shop;
            return s.Card + "|" + s.CardTarget + "|" + shop.Credits + "|" + shop.Phase + "|" + shop.Held.Count + string.Join(",", shop.Held) + "|" + string.Join(",", shop.Chamber) +
                "|" + shop.Incubating + "|" + (shop.Hatchling?.Id ?? 0) + "|" + shop.Carried.Count + "|" + string.Join(",", shop.Containers.Select(c => c.Kind + c.Level + c.Water + c.Place)) + "|" + shop.Rp + "|" + string.Join(",", shop.Cultures) +
                "|" + string.Join(",", shop.Vials.Select(v => v.Key + v.Value)) + "|" + string.Join(",", shop.Orders) + "|" + string.Join(",", shop.Containers.Select(c => c.Occupants.Count + ":" + c.Occupants.Count(o => o.ReservedBy != 0))) +
                "|" + shop.Queue.Count() + "|" + shop.Stars + "|" + shop.Era + "|" + shop.UnitB + "|" + string.Join(",", GrowthShop.UpgradeableStations.Select(shop.Level)) +
                "|" + string.Join(",", shop.Parents.Select(p => p.Id)) + "|" + (shop.Offspring?.Id ?? 0) + "|" + string.Join(",", shop.Pairs[1].Parents.Select(p => p.Id)) + (shop.Pairs[1].Offspring?.Id ?? 0) + "|" + shop.Hatchlings.Count + shop.IncubatorTint + "|" + shop.Tint + "|" + shop.PairLog.Count + "|" + shop.Journal.Count + "|" + string.Join(",", shop.Carried.Select(c => c.Id)) + "|" + Mathf.FloorToInt(shop.GrowerUnits.Values.Sum()) + "|" + PowerSignature() + "|" + LineSignature() + "|" + HelperSignature() + "|" + SpacesSignature() + "|" + ExhibitionSignature();
        }

        string LiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Incubator: return shop.Incubating ? "Incubating · " + Mathf.CeilToInt(shop.IncubationLeft) + " s" : shop.Hatchlings.Count > 1 ? "A clutch of " + shop.Hatchlings.Count + " is ready" : shop.Hatchling != null ? "A hatchling is ready" : shop.Chamber.Count > 0 ? "Ready to mix" : "Idle";
                case GrowthCard.Counter: return shop.Phase == GrowthPhase.Day ? "Open: " + ShopClock(shop.DaySecondsLeft) + " left." : "Closed for the night.";
                case GrowthCard.Grower: return s.CardTarget == null ? "" : Mathf.FloorToInt(shop.GrowerUnits[s.CardTarget]) + "/" + shop.GrowerCapacityOf(s.CardTarget) + " doses ready";
                case GrowthCard.Breeding: return !shop.HasBreedingTank ? "not bought" : shop.ActivePairs.Any(p => p.Offspring != null) ? "An offspring is waiting" : shop.ActivePairs.Any(p => p.Breeding) ? "Breeding · " + Mathf.CeilToInt(shop.ActivePairs.Where(p => p.Breeding).Min(p => p.Left)) + " s" : "Waiting for a pair";
                default: return PowerLiveText() ?? LineLiveText() ?? HelperLiveText() ?? "";
            }
        }

        void Live(VisualElement body)
        {
            liveLabel = Text(LiveText()); liveLabel.style.fontSize = 22; liveLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(liveLabel);
        }

        /// <summary>The strip's status chip says what the station is doing now (GR-13), kept current each frame.</summary>
        void LiveStatus()
        {
            if (statusLabel == null) return;
            statusLabel.text = LiveText();
            statusLabel.style.display = DisplayStyle.Flex;
            liveLabel = statusLabel;
        }

        void Fill(GrowthCard which, string target)
        {
            var shop = s.Shop;
            switch (which)
            {
                case GrowthCard.Grower: FillGrower(target); break;
                case GrowthCard.Incubator: FillIncubator(); break;
                case GrowthCard.Tank: FillTank(target); break;
                case GrowthCard.Place: FillPlace(target); break;
                case GrowthCard.Crate: FillCrate(); break;
                case GrowthCard.Catalog: FillCatalog(); break;
                case GrowthCard.Breeding: FillBreeding(); break;
                case GrowthCard.Counter: FillCounter(); break;
                case GrowthCard.Workbench: FillWorkbench(); break;
                case GrowthCard.UnitB: FillUnitB(); break;
                case GrowthCard.Board: FillBoard(); break;
                case GrowthCard.Journal: FillJournal(); break;
                case GrowthCard.Receipt: FillReceipt(); break;
                case GrowthCard.Welcome: FillWelcome(); break;
                case GrowthCard.Pause: FillPause(); break;
                case GrowthCard.Annex: FillAnnex(); break;
                case GrowthCard.Generator: FillGenerator(); break;
                case GrowthCard.PowerBoard: FillPowerBoard(); break;
                case GrowthCard.Buffer: FillBuffer(target); break;
                case GrowthCard.Workshop: FillWorkshop(); break;
                case GrowthCard.Sorter: FillSorter(); break;
                case GrowthCard.Staff: FillStaff(); break;
                case GrowthCard.UnitC: FillUnitC(); break;
                case GrowthCard.Hall: FillHall(); break;
                case GrowthCard.Helper: FillHelper(target); break;
                case GrowthCard.Results: FillResults(); break;
            }
        }

        void FillGrower(string culture)
        {
            var shop = s.Shop;
            bool vat = GrowthContent.Culture(culture).Source == CultureSource.Vat;
            var body = Head(vat ? "Conditioning vat (" + GrowthShop.Name(culture) + ")" : GrowthShop.Name(culture) + " grower", "", vat ? "Vat" : "Grower",
                "Hold the left mouse on it to fill your siphon. It refills by itself, one dose at a time. One upgrade covers every grower" + (vat ? "; the vat has levels of its own." : ".") +
                (shop.Taps < 1 ? " Growers' level 3 gives each grower a tap for a pipe to a buffer in the lab annex." : ""));
            LiveStatus();
            // How full it is, and its numbers as chips (GR-13: numbers, not sentences).
            float units = shop.GrowerUnits[culture];
            int capacity = shop.GrowerCapacityOf(culture);
            var fill = Box("gr-section");
            fill.Add(Bar(units / Mathf.Max(1, capacity), CultureColour(culture)));
            var chips = Box("gr-chips");
            chips.Add(Chip("It holds " + capacity));
            chips.Add(Chip("Refills a dose every " + shop.RefillSecondsOf(culture).ToString("0.#") + " s"));
            chips.Add(Chip("A hold collects " + (shop.PerHold > 1 ? shop.PerHold + " doses" : "a dose") + " every " + shop.HoldSeconds.ToString("0.0") + " s"));
            fill.Add(chips);
            body.Add(fill);
            body.Add(L("Your siphon: " + shop.Held.Count + " of " + shop.SiphonCapacity + " doses", "gr-hint"));
            if (shop.Taps >= 1) TapSection(AddTab("Pipes"), culture);
            if (vat) UpgradeSection(body, "vat", "The vat");
            UpgradeSection(body, "grower", "Every grower");
            Footer(body, "one upgrade covers every grower");
        }

        // ---------------------------------------------------------------- the incubator (GR.13)

        /// <summary>
        /// GR-13's incubator: Mix (the hatchling to take, the recipes found as tiles, the doses as chips with − and +,
        /// "close to something new" as a meter, Start) and Upgrade; the auto-doser and the chute on a Machines tab once
        /// there are any. The mix log is in the journal, a link away.
        /// </summary>
        void FillIncubator()
        {
            var shop = s.Shop;
            var body = Head("Incubator", "", "Mix", "Every mix makes a creature, and an exact recipe finds a new one. + doses a culture from your siphon into the chamber, − takes it back. A recipe you have found doses itself in one press.");
            LiveStatus();
            bool locked = shop.Incubating || shop.ChamberLock != null;
            if (shop.Hatchling != null)
            {
                string name = GrowthSession.Named(shop.Hatchling);
                int clutch = shop.Hatchlings.Count;
                var call = Box("gr-callout");
                var row = Box("gr-row");
                var glyph = new GrowthGlyph(GrowthCreatureView.ColourOf(shop.Hatchling.Def, shop.Hatchling.Colour)); glyph.AddToClassList("gr-result__glyph"); row.Add(glyph);
                row.Add(L(clutch > 1 ? "A clutch of " + clutch + " " + GrowthShop.Plural(name) : "A " + name, "gr-strong"));
                if (shop.LastMix != null && shop.LastMix.WasNew) row.Add(L("New!", "gr-newtag"));
                call.Add(row);
                call.Add(Wide("Take " + (clutch > 1 ? "a " : "the ") + name + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(shop.TakeHatchling), true,
                    shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full: put a creature in a tank first"));
                body.Add(call);
            }
            // The recipes found: one press doses each from the siphon (the doses already in go back first).
            var recipes = shop.MixLog.AsEnumerable().Reverse().Where(m => m.Exact && shop.Discovered.Contains(m.Species)).Select(m => m.Species).Distinct().Take(5).ToList();
            var close = shop.MixLog.AsEnumerable().Reverse().FirstOrDefault(m => m.DistanceToNew == 1 && !m.Exact);
            var found = Box("gr-section");
            found.Add(Eyebrow("Recipes you've found"));
            var tiles = Box("gr-row");
            foreach (var id in recipes) tiles.Add(RecipeTile(id, locked));
            if (close != null)
            {
                var unknown = Box("gr-tile", "gr-tile--unknown");
                unknown.tooltip = "Your mix of " + DoseWords(close.Doses) + " was one dose from something new";
                var g = new GrowthGlyph(new Color(0.72f, 0.67f, 0.57f)); g.AddToClassList("gr-tile__glyph"); unknown.Add(g);
                unknown.Add(L("?", "gr-tile__name"));
                var meta = Box("gr-tile__meta");
                for (int i = 0; i < close.Doses.Length; i++) for (int n = 0; n < close.Doses[i]; n++) meta.Add(Pip(CultureColour(GrowthContent.Cultures[i].Id)));
                unknown.Add(meta);
                tiles.Add(unknown);
            }
            if (recipes.Count == 0 && close == null) tiles.Add(L("None yet: every mix makes a creature. Try two or three doses.", "gr-hint"));
            found.Add(tiles);
            body.Add(found);
            // The chamber: a chip for each culture in the siphon or the chamber, − and + on either side.
            var chamber = Box("gr-section");
            chamber.Add(Eyebrow("In the chamber · " + shop.Chamber.Count + " of " + shop.MaxDoses));
            var doses = Box("gr-row");
            foreach (var culture in GrowthContent.Cultures.Select(c => c.Id).Where(c => shop.Held.Contains(c) || shop.Chamber.Contains(c)))
            {
                string c = culture;
                int inChamber = shop.Chamber.Count(x => x == c), held = shop.Held.Count(x => x == c);
                var chip = Box("gr-dose"); chip.style.backgroundColor = CultureGround(c);
                chip.Add(PlusMinus("−", () => s.Act(() => shop.ReturnDose(c)), !locked && inChamber > 0 && shop.Held.Count < shop.SiphonCapacity,
                    locked ? "The incubator is busy" : inChamber == 0 ? "None of it in the chamber" : "Your siphon is full"));
                var words = Box("gr-dose__text");
                words.Add(L(GrowthShop.Name(c) + " " + inChamber, "gr-dose__name"));
                words.Add(L(held + " in siphon", "gr-dose__count"));
                chip.Add(words);
                chip.Add(PlusMinus("+", () => s.Act(() => shop.AddDose(c)), !locked && held > 0 && shop.Chamber.Count < shop.MaxDoses,
                    locked ? "The incubator is busy" : held == 0 ? "Collect " + GrowthShop.Name(c) + " at its grower" : "The chamber holds " + shop.MaxDoses));
                doses.Add(chip);
            }
            if (shop.Held.Count == 0 && shop.Chamber.Count == 0) doses.Add(L("Your siphon is empty: hold the mouse on a grower to collect.", "gr-hint"));
            chamber.Add(doses);
            body.Add(chamber);
            if (shop.Chamber.Count > 0 && !shop.Incubating)
            {
                var preview = shop.Preview();
                // GR-06: the research bench's microscope measures every mix; without it, only a mix one dose away says so.
                if (shop.Microscope)
                {
                    int d = preview.DistanceToNew;
                    var row = Box("gr-row", "gr-row--spread", "gr-section");
                    row.Add(L("Microscope: " + (d < 0 ? "nothing new left to find with these cultures" : d == 0 ? "this mix is something new" : d + " dose" + (d == 1 ? "" : "s") + " from something new"), "gr-hint"));
                    row.Add(Meter(d < 0 ? 0 : d == 0 ? 3 : d == 1 ? 2 : d == 2 ? 1 : 0, d <= 1 ? "Hot" : d == 2 ? "Warm" : "Cold"));
                    body.Add(row);
                }
                else if (preview.CloseToNew)
                {
                    var row = Box("gr-row", "gr-row--spread", "gr-section");
                    row.Add(L("Close to something new", "gr-hint"));
                    row.Add(Meter(2, "One dose from something new"));
                    body.Add(row);
                }
            }
            body.Add(Wide(shop.Incubating ? "Incubating · " + Mathf.CeilToInt(shop.IncubationLeft) + " s" : "Start · " + shop.IncubationSeconds.ToString("0") + " s", () => s.Act(shop.StartIncubation), true,
                !locked && shop.Hatchling == null && shop.Chamber.Count > 0,
                shop.Incubating ? "It is incubating" : shop.ChamberLock ?? (shop.Hatchling != null ? "Take the hatchling first" : "Add a dose first")));
            var links = Box("gr-row", "gr-row--spread");
            var back = Link("Return doses", () => { foreach (var c in shop.Chamber.ToList()) shop.ReturnDose(c); s.SaveNow(); }, !locked && shop.Chamber.Count > 0, "Nothing in the chamber");
            back.style.marginLeft = 0;
            links.Add(back);
            links.Add(Link("Mix log · " + shop.MixLog.Count + " tried", () => OpenJournalAt("Mix log")));
            body.Add(links);
            // GR-06: the tint port (level 3) colours the next clutch, as the breeding tank's water does.
            if (shop.IncubatorTintPort) body.Add(TintRow(shop.IncubatorTint, "clutch", c => shop.IncubatorTintCheck(c, out string w) == GrowthFailure.None ? null : w,
                c => s.Act(() => shop.TintIncubator(c)), () => s.Act(shop.ClearIncubatorTint), "Tinted with " + (shop.IncubatorTint == null ? "" : GrowthShop.Name(shop.IncubatorTint)) + ": the next clutch is " + (shop.IncubatorTint == null ? "" : GrowthRules.BreedColour(Colour.Classic, Colour.Classic, shop.IncubatorTint).ToString()) + "."));
            if (shop.AutoDoser || shop.HasSorter)
            {
                var machines = AddTab("Machines");
                AutoDoserSection(machines);
                ChuteSection(machines);
            }
            UpgradeSection(body, "incubator");
            Footer(body);
        }

        Button PlusMinus(string text, Action click, bool enabled, string why)
        {
            var b = new Button(() => { if (enabled) { click(); RefreshCard(); } }) { text = text };
            b.AddToClassList("gr-pm");
            b.SetEnabled(enabled);
            if (!enabled) b.tooltip = why;
            return b;
        }

        static string DoseWords(int[] doses) =>
            string.Join(" and ", Enumerable.Range(0, doses.Length).Where(i => doses[i] > 0).Select(i => doses[i] + " " + GrowthContent.Cultures[i].Name));

        /// <summary>A recipe found, as a tile: its creature, its doses and time; a press doses it from the siphon.</summary>
        VisualElement RecipeTile(string species, bool locked)
        {
            var shop = s.Shop;
            var def = GrowthContent.Get(species);
            // What the siphon and the chamber lack between them, if anything.
            var have = new List<string>(shop.Held); have.AddRange(shop.Chamber);
            string missing = null;
            for (int i = 0; i < def.Recipe.Length && missing == null; i++)
            {
                if (def.Recipe[i] == 0) continue;
                string c = GrowthContent.Cultures[i].Id;
                int short_ = def.Recipe[i] - have.Count(x => x == c);
                if (short_ > 0) missing = "Needs " + short_ + " more " + GrowthShop.Name(c) + " in your siphon";
            }
            if (def.Doses > shop.MaxDoses) missing = "The chamber holds " + shop.MaxDoses;
            bool can = !locked && missing == null && shop.Hatchling == null;
            var tile = new Button(() => { if (can) { s.Act(() => shop.FillRecipe(species)); RefreshCard(); } });
            tile.AddToClassList("gr-tile");
            tile.SetEnabled(can);
            tile.tooltip = can ? "Dose " + def.Name + ": " + GrowthReport.Recipe(def.Recipe) : locked ? "The incubator is busy" : shop.Hatchling != null ? "Take the hatchling first" : missing;
            var g = new GrowthGlyph(GrowthCreatureView.ColourOf(def)); g.AddToClassList("gr-tile__glyph"); tile.Add(g);
            tile.Add(L(def.Name, "gr-tile__name"));
            var meta = Box("gr-tile__meta");
            for (int i = 0; i < def.Recipe.Length; i++) for (int n = 0; n < def.Recipe[i]; n++) meta.Add(Pip(CultureColour(GrowthContent.Cultures[i].Id)));
            meta.Add(L(shop.IncubationSeconds.ToString("0") + " s", "gr-tile__time"));
            tile.Add(meta);
            return tile;
        }

        /// <summary>
        /// A tint port as a slot: tinted, the dose's chip with what it makes (its sentence on hover) and Take it back;
        /// otherwise a chip for each tint culture, each saying why when it can't be used.
        /// </summary>
        VisualElement TintRow(string tint, string what, Func<string, string> why, Action<string> put, Action clear, string tintedSentence)
        {
            var shop = s.Shop;
            var row = Box("gr-row", "gr-section");
            var label = Eyebrow("Tint"); label.style.marginBottom = 0; label.style.marginRight = 10; row.Add(label);
            if (tint != null)
            {
                var chip = Box("gr-dose"); chip.style.backgroundColor = CultureGround(tint); chip.style.marginBottom = 0; chip.tooltip = tintedSentence;
                chip.Add(L(GrowthShop.Name(tint) + " → " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, tint), "gr-dose__name"));
                row.Add(chip);
                row.Add(Link("Take it back", clear, shop.Held.Count < shop.SiphonCapacity, "Your siphon is full"));
                return row;
            }
            foreach (var culture in GrowthShop.TintCultures)
            {
                string c = culture;
                string no = why(c);
                var b = ChipButton(GrowthShop.Name(c) + " → " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, c), () => put(c), no == null,
                    no == null ? "A dose of " + GrowthShop.Name(c) + " makes the next " + what + " " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, c) : !shop.Held.Contains(c) ? "Collect " + GrowthShop.Name(c) + " first" : no,
                    CultureColour(c));
                b.style.marginTop = 0;
                row.Add(b);
            }
            return row;
        }

        // ---------------------------------------------------------------- containers (GR.13)

        bool moveOpen, sellAsked;

        static string Holds(ContainerDef def) => def.Largest == SizeClass.Large ? "creatures of every size" : "small and medium creatures";

        /// <summary>Why a container is or is not in harmony (GR.5), in a few words.</summary>
        string HarmonyText(GrowthContainer c)
        {
            var shop = s.Shop;
            if (shop.Harmony(c)) return "in harmony: +1 appeal";
            if (c.Occupants.Count < 2) return "harmony needs two or more creatures that agree";
            if (c.Occupants.Select(o => o.Def.Temperament).Distinct().Count() > 1) return "calm and lively mixed: no harmony";
            var odd = c.Occupants.First(o => o.Def.Water != c.Water);
            return "a " + odd.Def.Water.ToString().ToLowerInvariant() + "-water " + odd.Def.Name + " in " + c.Water.ToString().ToLowerInvariant() + " water: no harmony";
        }

        /// <summary>GR.13's occupant grid: a cell a space, each creature as wide as its spaces in its colour, a school badged, a sold one paler.</summary>
        VisualElement SpaceGrid(GrowthContainer c)
        {
            var shop = s.Shop;
            var grid = Box("gr-spaces");
            var badged = new HashSet<string>();
            // The biggest first, so the cells pack into fewer rows.
            foreach (var o in c.Occupants.OrderByDescending(o => (int)o.Def.Size).ThenBy(o => o.Species).ThenBy(o => o.Id))
            {
                int spaces = (int)o.Def.Size;
                var cell = Box("gr-space");
                cell.style.width = 64 * spaces + 8 * (spaces - 1);
                if (o.ReservedBy != 0) cell.AddToClassList("gr-space--sold");
                cell.tooltip = GrowthSession.Named(o) + " · " + o.Def.Size.ToString().ToLowerInvariant() + " · " + shop.Price(o.Species) + " cr" + (GrowthShop.IsOnDisplay(o) ? " · on display" : o.ReservedBy != 0 ? " · sold" : "");
                var g = new GrowthGlyph(GrowthCreatureView.ColourOf(o.Def, o.Colour)); g.AddToClassList("gr-space__glyph");
                if (spaces > 1) { g.style.width = 54 + 22 * Mathf.Min(spaces, 3); g.style.height = 32 + 4 * Mathf.Min(spaces, 3); }
                cell.Add(g);
                if (c.InSchool(o.Species) && badged.Add(o.Species)) cell.Add(L("school", "gr-badge"));
                grid.Add(cell);
            }
            for (int i = 0; i < c.Free; i++) { var empty = Box("gr-space", "gr-space--empty"); empty.style.width = 64; grid.Add(empty); }
            return grid;
        }

        /// <summary>
        /// GR-13's container card: Tank (the space grid, each species with its price, a press taking one out; putting in
        /// what you carry; its comfort as a face and a word; water kits; Move and Sell at night, Sell asking first) and
        /// Upgrade. Its rules are on the "?" (UX.3.5).
        /// </summary>
        void FillTank(string containerId)
        {
            var shop = s.Shop;
            var c = shop.Container(containerId);
            if (c == null) { s.SetCard(GrowthCard.None); return; }
            bool comfort = shop.Era >= GrowthContent.ComfortEra && !c.Def.Lab;
            string rules = c.Def.Lab ? "In the workshop, off the shop floor: visitors never see it. Holds " + Holds(c.Def) + "; carry creatures from here to the shop's tanks." :
                "Holds " + Holds(c.Def) + ": a small one takes 1 space, a medium one 2, a large one 4. Three of a kind swim as a school and sell a little better." +
                (comfort ? " Creatures that share a temperament and the tank's water are in harmony: +1 appeal." : "") + " Peek: look closely at the glass to read each creature; E takes the one under the reticle.";
            var body = Head(c.Def.Name, c.Used + " of " + c.Spaces + " spaces", c.Def.Lab ? "Rack" : "Tank", rules, !c.Def.Lab);
            body.Add(SpaceGrid(c));
            // Each species with its count and price; a press takes one out.
            var kinds = Box("gr-row", "gr-section");
            foreach (var g in c.Occupants.GroupBy(o => (o.Species, o.Colour)))
            {
                var free = g.FirstOrDefault(o => o.ReservedBy == 0 && !GrowthShop.IsOnDisplay(o));
                int sold = g.Count(o => o.ReservedBy != 0);
                var def = g.First().Def;
                bool can = free != null && shop.Carried.Count < GrowthShop.CarryLimit;
                var b = ChipButton(GrowthSession.Named(g.First()) + (g.Count() > 1 ? " ×" + g.Count() : "") + (sold > 0 ? " (" + sold + " sold)" : "") + " · " + shop.Price(def.Id) + " cr",
                    () => s.Act(() => shop.TakeOut(c.Id, free.Id)), can, can ? "Take one out" : free == null ? "None free to take" : "Your hands are full", GrowthCreatureView.ColourOf(def, g.Key.Colour));
                kinds.Add(b);
            }
            if (c.Occupants.Count == 0) kinds.Add(L("Empty: carry creatures here and press E.", "gr-hint"));
            body.Add(kinds);
            if (shop.Carried.Count > 0)
            {
                var put = Box("gr-row", "gr-section");
                foreach (var carried in shop.Carried.ToList())
                {
                    var spec = carried;
                    bool fits = GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(x => x.Species), spec.Species);
                    put.Add(Button("Put in " + GrowthSession.Named(spec), () => s.Act(() => shop.PutIn(c.Id, spec.Id)), true, fits, fits ? null : (int)spec.Def.Size > (int)c.Def.Largest ? "Too big for this tank" : "Not enough spaces"));
                }
                body.Add(put);
            }
            if (!c.Def.Lab)
            {
                // GR.5 comfort as a face and a word: harmony makes it happy; a creature in the wrong water, uneasy.
                bool harmony = shop.Harmony(c);
                bool wrongWater = comfort && c.Occupants.Any(o => o.Def.Water != c.Water);
                int mood = !comfort ? (c.Occupants.Count == 0 ? 1 : 2) : harmony ? 2 : wrongWater ? 0 : 1;
                var feel = Box("gr-row", "gr-section"); feel.style.flexWrap = Wrap.NoWrap;
                feel.Add(new GrowthFace(mood));
                var words = Box("gr-result__text");
                words.Add(L(mood == 2 ? "Happy" : mood == 1 ? "Settled" : "Uneasy", "gr-strong"));
                bool school = c.Occupants.GroupBy(o => o.Species).Any(x => x.Count() >= GrowthShop.SchoolSize);
                words.Add(L(comfort ? HarmonyText(c) : c.Occupants.Count == 0 ? "nobody to please yet" : school ? "a school sells a little better" : "settled in their tank", "gr-hint"));
                feel.Add(words);
                int appeal = shop.Appeal(c);
                if (appeal > 0)
                    feel.Add(Chip("Appeal +" + appeal, false, string.Join(" and ", new[] { c.Kind == "show" ? "a lit Show tank" : null, harmony ? "harmony" : null }.Where(x => x != null)) +
                        ": visitors look here first and stretch their budget " + (appeal * 10) + "%"));
                body.Add(feel);
                if (comfort)
                {
                    var water = Box("gr-row", "gr-section");
                    var label = Eyebrow("Water · " + c.Water.ToString().ToLowerInvariant()); label.style.marginBottom = 0; label.style.marginRight = 6; water.Add(label);
                    foreach (Water w in Enum.GetValues(typeof(Water)))
                    {
                        if (w == c.Water) continue;
                        var to = w;
                        var failure = shop.WaterCheck(c.Id, to, out string why);
                        var b = ChipButton(to + " · " + GrowthContent.WaterKitCredits + " cr", () => s.Act(() => shop.SetWater(c.Id, to)), failure == GrowthFailure.None, failure == GrowthFailure.None ? "A water kit: " + to.ToString().ToLowerInvariant() + " water" : why);
                        b.style.marginTop = 0;
                        water.Add(b);
                    }
                    body.Add(water);
                }
            }
            // Arranging: Move lists the free places it fits; Sell asks first. Both say why when they can't, on hover.
            var arrange = Box("gr-row", "gr-section");
            if (!c.Def.Lab)
            {
                var places = shop.OwnedPlaces.Where(p => shop.MoveCheck(c.Id, p, out _) == GrowthFailure.None).ToList();
                string moveWhy = shop.Phase != GrowthPhase.Night ? "Containers are moved at night" : places.Count == 0 ? "No free place it fits" : null;
                var move = Button(moveOpen ? "Move: pick a place" : "Move", () => moveOpen = !moveOpen, false, moveWhy == null, moveWhy);
                arrange.Add(move);
                if (moveOpen && moveWhy == null)
                    foreach (var p in places) { var place = p; arrange.Add(ChipButton("To " + place, () => { if (s.Act(() => shop.MoveContainer(c.Id, place)).Success) s.SetCard(GrowthCard.None); }, true, "Move it with its creatures to " + place)); }
            }
            var sell = shop.SellCheck(c.Id, out string sellWhy);
            if (sell == GrowthFailure.None && sellAsked)
            {
                arrange.Add(L("Sell it for " + GrowthShop.SellValue(c) + " cr?", "gr-hint"));
                var yes = Button("Sell", () => { sellAsked = false; if (s.Act(() => shop.SellContainer(c.Id)).Success) s.SetCard(GrowthCard.None); });
                yes.RemoveFromClassList("es-button--secondary"); yes.AddToClassList("es-button--danger"); yes.style.marginLeft = 10;
                arrange.Add(yes);
                arrange.Add(Button("Keep it", () => sellAsked = false));
            }
            else
            {
                var b = Button("Sell " + (c.Def.Lab ? "rack" : "tank") + (sell == GrowthFailure.None ? " · +" + GrowthShop.SellValue(c) + " cr" : ""), () => sellAsked = true, false, sell == GrowthFailure.None, sellWhy);
                b.RemoveFromClassList("es-button--secondary"); b.AddToClassList("es-button--danger");
                arrange.Add(b);
            }
            body.Add(arrange);
            // Upgrade: the container's levels as every station's are.
            var upgrade = AddTab(UpgradeTab);
            var check = shop.ContainerUpgradeCheck(c.Id, out var next, out string upgradeWhy);
            upgrade.Add(UpgradeBlock(c.Def.Name + " · level " + c.Level + " of " + c.Def.Levels.Length, c.Def.Levels, c.Level, next, check, upgradeWhy, "Upgrade", () => s.Act(() => shop.UpgradeContainer(c.Id))));
            Footer(body, "peek and E takes one");
        }

        /// <summary>GR-13's empty place: each container that could stand here as a row with its spaces, size and price, and moving a tank here.</summary>
        void FillPlace(string place)
        {
            var shop = s.Shop;
            if (GrowthShop.LabPlace(place) != null) { FillRackPlace(place); return; }
            var spot = GrowthShop.Place(place);
            if (spot == null) { s.SetCard(GrowthCard.None); return; }
            var body = Head("Empty place", (spot.Unit == "unit-b" ? "shop unit B" : "shop unit A") + " · " + spot.Size, null,
                "A " + spot.Size + " place: room for up to " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m. Tanks are bought, moved and sold at night.", true);
            body.Add(L("Room for up to " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m", "gr-label"));
            foreach (var kind in GrowthShop.ShopKinds)
            {
                var def = GrowthContent.Container(kind);
                var first = def.Levels[0];
                var failure = shop.BuyCheck(kind, place, out string why);
                var k = kind;
                var buy = Button("Buy a " + def.Name + " for " + first.Credits + " cr", () => { if (s.Act(() => shop.BuyContainer(k, place)).Success) s.SetCard(GrowthCard.None); }, kind == "starter", failure == GrowthFailure.None, why);
                buy.style.marginTop = 0;
                var row = Item(def.Name, first.Change + " · " + Holds(def) + " · " + GrowthShop.Footprint(def), buy);
                if (failure == GrowthFailure.TooBigForPlace || failure == GrowthFailure.Locked) row.AddToClassList("gr-item--off");
                body.Add(row);
            }
            var movers = shop.ShopContainers.Where(c => shop.MoveCheck(c.Id, place, out _) == GrowthFailure.None).ToList();
            if (movers.Count > 0)
            {
                body.Add(L("Or move a tank here, with its creatures", "gr-label"));
                var row = Box("gr-row");
                foreach (var m in movers) { var id = m.Id; row.Add(ChipButton("The " + m.Def.Name + " from " + m.Place, () => { if (s.Act(() => shop.MoveContainer(id, place)).Success) s.SetCard(GrowthCard.None); }, true)); }
                body.Add(row);
            }
            Footer(body);
        }

        /// <summary>Development only: any species in hand, to look at sizes and families before cultures and breeding (GR-04, GR-05).</summary>
        void FillCrate()
        {
            var shop = s.Shop;
            var body = Head("Development crate", "not part of the game");
            body.Add(Text("For looking at sizes and families before cultures and breeding arrive: press a species to take one in hand (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried), then E on a tank to put it in. It is not a discovery.", "es-text--muted"));
            foreach (var family in GrowthContent.Families)
            {
                var title = Text(family.Name + " · " + family.Size.ToString().ToLowerInvariant()); title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginTop = 6;
                body.Add(title);
                var row = Row();
                foreach (var sp in GrowthContent.Species.Where(x => x.Family == family.Id))
                {
                    var id = sp.Id;
                    var b = Button(sp.Name, () => s.Act(() => shop.DevelopmentGive(id)), false, shop.Carried.Count < GrowthShop.CarryLimit);
                    b.style.borderLeftWidth = 8; b.style.borderLeftColor = GrowthCreatureView.ColourOf(sp);
                    row.Add(b);
                }
                body.Add(row);
            }
            Footer(body, "F8 closes it too");
        }

        void FillCounter()
        {
            var shop = s.Shop;
            var body = Head("Counter", "", "Counter", "Open the shop here at night: a day lasts 5:30, and visitors pick one creature each and wait here to pay. Hold the left mouse on the counter to bag each sale; others wait by the tanks. Nobody comes into an empty shop.");
            LiveStatus();
            if (shop.Phase == GrowthPhase.Night)
            {
                int stock = shop.Stock.Count();
                body.Add(Stat(stock.ToString(), stock == 1 ? "creature on show" : "creatures on show"));
                if (stock == 0) body.Add(L("Your tanks are empty: nobody comes into an empty shop.", "gr-hint"));
                var open = Wide("Open the shop", () => { if (s.Act(shop.OpenShop).Success) s.SetCard(GrowthCard.None); }, true, true);
                open.style.marginTop = 14;
                body.Add(open);
            }
            else
            {
                bool pip = shop.Working("pip");
                var chips = Box("gr-chips", "gr-section");
                chips.Add(Chip(pip && !shop.SecondTill ? "Pip bags each order in " + GrowthShop.PipServeSeconds[shop.Helper("pip").Level - 1].ToString("0.0") + " s" : "A sale takes " + shop.ServeSeconds.ToString("0.0") + " s" + (pip ? ", beside Pip" : "")));
                chips.Add(Chip("The queue holds " + shop.QueueLength));
                body.Add(chips);
                if (pip && !shop.SecondTill) body.Add(L("The counter's second till (level 4) lets you serve beside Pip.", "gr-hint"));
                body.Add(Eyebrow("Waiting · " + shop.Queue.Count()));
                foreach (var v in shop.Queue.OrderByDescending(x => x.Clock).ThenBy(x => x.Id))
                {
                    var spec = shop.Stock.FirstOrDefault(x => x.Id == v.SpecimenId);
                    var patience = Bar(v.Patience / (v.Type == GrowthShop.GuestType ? GrowthShop.GuestPatience : GrowthShop.QueuePatience), new Color(0.98f, 0.78f, 0.37f));
                    patience.style.width = 120; patience.tooltip = "Patience: " + Mathf.CeilToInt(v.Patience) + " s";
                    body.Add(Item(Visitor(v.Type), "wants a " + (spec?.Def.Name ?? "?") + " · " + (spec == null ? 0 : shop.Price(spec.Species)) + " cr", patience));
                }
                if (!shop.Queue.Any()) body.Add(L("No one is waiting.", "gr-hint"));
            }
            UpgradeSection(body, "counter");
            Footer(body);
        }

        static string Visitor(string type) => GrowthContent.Visitors.First(v => v.Id == type).Name;

        void FillWorkbench()
        {
            var shop = s.Shop;
            // GR-13: Analyzer, Planner and Upgrade, each one job (the mockup); before the analyzer, the siphon's numbers first.
            var body = Head("Research bench", "bench " + shop.Level("bench") + " · siphon " + shop.Level("siphon"), shop.Analyzer ? "Analyzer" : "Siphon",
                "The bench improves your siphon and your study: the microscope (level 2) measures how far each mix is from something new, the analyzer (level 3) gives a hint a day, and the planner (level 4) predicts what any two known species breed. Undiscovered founders show the cultures they use in the journal.");
            if (shop.Analyzer)
            {
                AnalyzerSection(body);
                if (shop.Planner) PlannerSection(AddTab("Planner"));
                SiphonSection(AddTab("Siphon"));
            }
            else SiphonSection(body);
            UpgradeSection(body, "siphon", "The siphon");
            UpgradeSection(body, "bench", "The research bench");
            Footer(body);
        }

        /// <summary>The siphon's numbers as chips, and what the microscope does.</summary>
        void SiphonSection(VisualElement body)
        {
            var shop = s.Shop;
            body.Add(Stat(shop.SiphonCapacity.ToString(), "doses your siphon holds"));
            var chips = Box("gr-chips", "gr-section");
            chips.Add(Chip((shop.PerHold > 1 ? shop.PerHold + " doses" : "A dose") + " every " + shop.HoldSeconds.ToString("0.0") + " s"));
            if (shop.Vacuum) chips.Add(Chip("Vacuum", false, "It keeps pulling from the next grower in reach when the one you aim at runs dry"));
            body.Add(chips);
            body.Add(L(shop.Microscope ? "The microscope is in: the incubator says how many doses each mix is from something new." : "The microscope (level 2) measures how far a mix is from something new.", "gr-hint"));
        }

        void FillUnitB()
        {
            var shop = s.Shop;
            var unit = GrowthShop.UnitBDef;
            var body = Head("Shop unit B", shop.UnitB ? "open" : unit.Credits + " credits", null, "Era 2 comes with 2 stars, 3 creatures discovered and 2 tanks. Walls open at night.");
            var failure = shop.UnitBCheck(out string why);
            if (shop.UnitB) { body.Add(L("Open: four more places for tanks and one more visitor at a time.", "gr-hint")); Footer(body); return; }
            BuyPanel(body, "The unit next door", "Four more places for tanks", "And one more visitor at a time.", Gates(unit.Era, unit.Stars, unit.Credits, 0, true), failure, why,
                "Open unit B for " + unit.Credits + " cr", shop.BuyUnitB);
            Footer(body);
        }

        /// <summary>GR-13's pinboard, a planning view: Goals (three pinned papers) and Research.</summary>
        void FillBoard()
        {
            var shop = s.Shop;
            var body = Notebook("Pinboard", shop.ExhibitionDone ? "free play" : "Era " + shop.Era + " · " + GrowthContent.Eras[shop.Era - 1].Name, "Goals");
            // GR-11: the objective board (GR.12): now, next and the long goal, the same at the top of the journal.
            BoardSections(body);
            // GR-04: research turns research points (earned by discoveries) into Spark and, much later, Dusk.
            var research = AddTab("Research");
            research.Add(L("Research · " + shop.Rp + " research points (each discovery brings some)", "gr-label"));
            var items = Box("gr-items");
            foreach (var c in GrowthContent.Cultures.Where(x => x.Source == CultureSource.Research))
            {
                var failure = shop.ResearchCheck(c.Id, out string why);
                string id = c.Id;
                bool owned = shop.Owns(id);
                var item = ShopItem(CultureColour(c.Id), c.Name, owned ? "Researched: its grower stands in the back room." : "Its grower stands in the back room at once.", c.Rp + " RP", false);
                var b = Button(owned ? "Researched" : "Research " + c.Name + " · " + c.Rp + " RP", () => s.Act(() => shop.Research(id)), true, failure == GrowthFailure.None, owned ? null : why);
                b.style.marginTop = 10; b.style.marginRight = 0;
                item.Add(b);
                items.Add(item);
            }
            research.Add(items);
            NotebookFooter("Back", null, null, shop.Rp + " research points");
        }

        /// <summary>An item card on a planning view: a swatch, a name, a line, its price, and its button added after.</summary>
        VisualElement ShopItem(Color swatch, string name, string line, string price, bool locked)
        {
            var item = Box("gr-shopitem");
            if (locked) item.AddToClassList("gr-shopitem--locked");
            var picture = Box("gr-shopitem__picture"); picture.style.backgroundColor = Color.Lerp(swatch, Color.white, 0.55f);
            var dot = Box("gr-shopitem__dot"); dot.style.backgroundColor = swatch; picture.Add(dot);
            item.Add(picture);
            item.Add(L(name, "gr-shopitem__name"));
            item.Add(L(line, "gr-hint"));
            var cost = Box("gr-row", "gr-shopitem__price");
            var coin = Box("gr-coin"); coin.style.backgroundImage = new StyleBackground(Icon(ShopUiTheme.Symbol.Coin)); cost.Add(coin);
            cost.Add(L(price, "gr-shopitem__amount"));
            item.Add(cost);
            return item;
        }

        /// <summary>Where a culture comes from, in one line (the journal's "Where to get it", GR.6).</summary>
        static string WhereToGet(CultureDef c)
        {
            switch (c.Source)
            {
                case CultureSource.StarterGrower: return "its grower, free from the start";
                case CultureSource.CatalogGrower: return "supplier catalog, page " + c.Stars + " (era " + c.Era + "): its grower for " + c.Credits + " credits, or a starter vial for " + c.VialCredits;
                case CultureSource.Vat: return "supplier catalog, page " + c.Stars + " (era " + c.Era + "): the conditioning vat, " + c.Credits + " credits and " + c.Rp + " research points; or a starter vial for " + c.VialCredits;
                default: return "research at the pinboard: " + c.Rp + " research points (era " + c.Era + ", " + c.Stars + " stars); or a starter vial for " + c.VialCredits;
            }
        }

        string CultureLine(CultureDef c)
        {
            var shop = s.Shop;
            string vial = shop.Vials.TryGetValue(c.Id, out int doses) ? " · starter vial: " + doses + " doses" : "";
            if (shop.Owns(c.Id))
                return c.Name + " · " + (c.Source == CultureSource.Vat ? "conditioning vat" : c.Source == CultureSource.Research ? "researched, its grower" : c.Source == CultureSource.CatalogGrower ? "grower from the supplier" : "grower") +
                    " · level " + shop.Level("grower") + vial;
            bool ordered = shop.Orders.Any(o => GrowthContent.CatalogItem(o).Culture == c.Id && GrowthContent.CatalogItem(o).Kind != CatalogKind.Vial);
            return c.Name + (ordered ? " · ordered: it arrives when the shop opens" : " · where to get it: " + WhereToGet(c)) + vial;
        }

        /// <summary>
        /// GR-13's supplier catalog, a planning view at the counter: a page per star level as tabs (a locked page says the
        /// stars it needs), its items as cards with a picture, a line, a price and Order; nothing scrolls.
        /// </summary>
        void FillCatalog()
        {
            var shop = s.Shop;
            var pages = Enumerable.Range(2, 4).Where(p => GrowthContent.Catalog.Any(i => i.Stars == p)).ToList();
            VisualElement body = null;
            foreach (int page in pages)
            {
                string name = "Page " + page + " · " + page + " stars";
                var tab = body == null ? body = Notebook("Supplier catalog", shop.Phase == GrowthPhase.Night ? "Orders arrive when the shop opens" : "Orders are placed at night", name) : AddTab(name);
                bool locked = shop.Stars < page;
                if (locked) { var tabButton = tabList[tabList.Count - 1].tab; tabButton.tooltip = "Opens at " + page + " stars"; tabButton.AddToClassList("gr-nb-tab--locked"); tab.Add(L("Opens at " + page + " stars: you have " + shop.Stars + ".", "gr-label")); }
                var items = Box("gr-items");
                foreach (var item in GrowthContent.Catalog.Where(i => i.Stars == page))
                {
                    var failure = shop.OrderCheck(item.Id, out string why);
                    string id = item.Id;
                    var card = ShopItem(CultureColour(item.Culture), item.Name, item.Text, item.Credits + " credits" + (item.Rp > 0 ? " and " + item.Rp + " RP" : ""), locked);
                    Button b;
                    if (failure == GrowthFailure.Ordered && shop.Orders.Contains(id))
                        b = Button("On order · take it back", () => s.Act(() => shop.CancelOrder(id)), false, shop.Phase == GrowthPhase.Night, "Orders are changed at night");
                    else if (failure == GrowthFailure.Owned)
                        b = Button("Yours", () => { }, false, false, why);
                    else
                        b = Button("Order for " + item.Credits + " cr" + (item.Rp > 0 ? " and " + item.Rp + " RP" : ""), () => s.Act(() => shop.Order(id)), item.Kind != CatalogKind.Vial, failure == GrowthFailure.None, why);
                    b.style.marginTop = 10; b.style.marginRight = 0;
                    card.Add(b);
                    items.Add(card);
                }
                tab.Add(items);
            }
            NotebookFooter("Back to the shop", null, null, (shop.Orders.Count > 0 ? "On order: " + string.Join(", ", shop.Orders.Select(o => GrowthContent.CatalogItem(o).Name)) + " · " : "") + shop.Credits + " credits · " + StarsText(shop.Stars));
        }

        // ---------------------------------------------------------------- the breeding tank (GR.13)

        readonly List<(GrowthRing ring, int pair)> rings = new List<(GrowthRing, int)>();

        /// <summary>Each breeding pair's ring fills and counts down as it breeds (the card is rebuilt only when something changes).</summary>
        void TickRings()
        {
            if (rings.Count == 0) return;
            var shop = s.Shop;
            foreach (var (ring, k) in rings)
            {
                var pair = shop.Pairs[k];
                if (pair.Offspring != null) ring.Set(1, "✓");
                else if (pair.Breeding) ring.Set(1 - pair.Left / Mathf.Max(0.01f, shop.BreedSeconds), Mathf.CeilToInt(pair.Left) + "s");
                else ring.Set(0, "–");
            }
        }

        /// <summary>
        /// GR-13's breeding tank: Pair (each pair's parents side by side, a press taking one out; a timer ring while they
        /// breed; what they'll make, "?" for a pair not tried, or what the pair log or the planner knows; the offspring in
        /// the nursery; the tint port as a slot) and Upgrade. The pair log is in the journal, a link away.
        /// </summary>
        void FillBreeding()
        {
            var shop = s.Shop;
            var levels = GrowthContent.Station("breeding").Levels;
            const string rules = "Two creatures in, one offspring out, and the parents stay. Every pair makes something: hybrids, the families' crowns and new colours come from breeding, and a pair breeds the same thing every time, so a known pair makes more stock without cultures. Untinted, an offspring takes the higher parent colour (Classic, Sunny, Deep, Shimmer); the tint port (level 2) colours the next one.";
            if (!shop.HasBreedingTank)
            {
                var intro = Head("Breeding tank", "not bought", null, rules);
                var first = levels[0];
                var buy = shop.UpgradeCheck("breeding", out _, out string buyWhy);
                var benefit = Box("gr-benefit");
                benefit.Add(Eyebrow("Level 1"));
                benefit.Add(L(first.Change, "gr-strong"));
                var chips = Box("gr-chips");
                chips.Add(Chip("Era " + first.Era, shop.Era < first.Era));
                if (first.Stars > 0) chips.Add(Chip(Stars(first.Stars), shop.Stars < first.Stars));
                chips.Add(Chip(first.Credits + " credits", shop.Credits < first.Credits));
                chips.Add(Chip(first.Rp + " RP", shop.Rp < first.Rp));
                benefit.Add(chips);
                intro.Add(benefit);
                intro.Add(L("Two creatures in, one offspring out, and the parents stay.", "gr-hint"));
                var b = Wide("Buy the breeding tank for " + first.Credits + " cr and " + first.Rp + " RP", () => s.Act(() => shop.Upgrade("breeding")), true, buy == GrowthFailure.None, buyWhy);
                b.style.marginTop = 14;
                intro.Add(b);
                Footer(intro);
                return;
            }
            var body = Head("Breeding tank", "", "Pair", rules);
            LiveStatus();
            for (int k = 0; k < shop.PairPlaces; k++)
            {
                var pairState = shop.Pairs[k];
                int pairIndex = k;
                var section = Box("gr-section");
                if (shop.PairPlaces > 1)
                    section.Add(L("Pair " + (k + 1) + " (" + pairState.Parents.Count + "/" + GrowthShop.PairSize + ")" + (pairState.Breeding ? " · breeding" : pairState.Offspring != null ? " · offspring ready" : ""), "gr-label"));
                if (pairState.Offspring != null)
                {
                    var o = pairState.Offspring;
                    var call = Box("gr-callout");
                    var row = Box("gr-row");
                    var g = new GrowthGlyph(GrowthCreatureView.ColourOf(o.Def, o.Colour)); g.AddToClassList("gr-result__glyph"); row.Add(g);
                    var words = Box("gr-result__text");
                    words.Add(L("In the nursery: a " + GrowthSession.Named(o) + (pairState.OffspringWasNew ? " — new to your journal!" : "."), "gr-strong"));
                    row.Add(words);
                    call.Add(row);
                    call.Add(Wide("Take the " + GrowthSession.Named(o) + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(() => shop.TakeOffspring(pairIndex)), true,
                        shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full: put a creature in a tank first"));
                    section.Add(call);
                }
                // The parents side by side; a press takes one out, at any time.
                var pair = Box("gr-pair");
                for (int i = 0; i < GrowthShop.PairSize; i++)
                {
                    if (i == 1) pair.Add(L("×", "gr-times"));
                    if (i < pairState.Parents.Count)
                    {
                        var p = pairState.Parents[i];
                        int id = p.Id;
                        bool can = shop.Carried.Count < GrowthShop.CarryLimit;
                        var parent = new Button(() => { if (can) { s.Act(() => shop.TakeParent(id)); RefreshCard(); } });
                        parent.AddToClassList("gr-parent");
                        parent.SetEnabled(can);
                        parent.tooltip = can ? "Take it out" : "Your hands are full";
                        var g = new GrowthGlyph(GrowthCreatureView.ColourOf(p.Def, p.Colour)); g.AddToClassList("gr-parent__glyph"); parent.Add(g);
                        parent.Add(L(p.Def.Name, "gr-tile__name"));
                        parent.Add(L(p.Colour.ToString(), "gr-dose__count"));
                        pair.Add(parent);
                    }
                    else
                    {
                        var empty = Box("gr-parent", "gr-parent--empty");
                        empty.Add(L("Empty", "gr-tile__name"));
                        empty.Add(L(shop.Carried.Count > 0 ? "put one in below" : "E here puts one in", "gr-dose__count"));
                        pair.Add(empty);
                    }
                }
                section.Add(pair);
                if (pairState.Parents.Count >= GrowthShop.PairSize)
                {
                    var plan = Box("gr-row"); plan.style.flexWrap = Wrap.NoWrap;
                    var ring = new GrowthRing();
                    rings.Add((ring, k));
                    plan.Add(ring);
                    var right = Box("gr-result__text");
                    right.Add(Eyebrow("They'll make"));
                    // What the pair makes: the pair log knows a pair once tried; the planner (research bench level 4) predicts any pair.
                    var known = shop.PairEntry(pairState.Parents[0].Species, pairState.Parents[1].Species);
                    var planned = known == null ? shop.PlannerPredicts(pairState.Parents[0].Species, pairState.Parents[1].Species) : null;
                    var makes = known != null ? GrowthContent.Get(known.Species) : planned;
                    var next = shop.NextColourOf(pairState);
                    var result = Box("gr-result");
                    var rg = new GrowthGlyph(makes != null ? GrowthCreatureView.ColourOf(makes, next) : new Color(0.72f, 0.67f, 0.57f)); rg.AddToClassList("gr-result__glyph"); result.Add(rg);
                    var rw = Box("gr-result__text");
                    var nameRow = Box("gr-row");
                    nameRow.Add(L(makes != null ? makes.Name : "?", "gr-strong"));
                    if (planned != null && !shop.Discovered.Contains(planned.Id)) nameRow.Add(L("New", "gr-newtag"));
                    rw.Add(nameRow);
                    rw.Add(L(known != null ? (makes.Kind == SpeciesKind.Crown ? "a crown · " : "") + "bred ×" + known.Count + " so far" : planned != null ? "the planner's prediction" : "A pair you have not tried", "gr-hint"));
                    if (next != Colour.Classic) rw.Add(L("Its colour: " + next + (shop.Tint != null ? " (the " + GrowthShop.Name(shop.Tint) + " tint)" : " (the higher parent colour)"), "gr-hint"));
                    result.Add(rw);
                    right.Add(result);
                    plan.Add(right);
                    section.Add(plan);
                }
                body.Add(section);
            }
            TickRings();
            if (shop.ActivePairs.Any(p => p.Parents.Count < GrowthShop.PairSize) && shop.Carried.Count > 0)
            {
                var put = Box("gr-row", "gr-section");
                foreach (var carried in shop.Carried.ToList())
                {
                    var spec = carried;
                    var f = shop.ParentCheck(spec, out string why);
                    put.Add(Button("Put in " + GrowthSession.Named(spec), () => s.Act(() => shop.PutParent(spec.Id)), true, f == GrowthFailure.None, why));
                }
                body.Add(put);
            }
            // Tinting the water (level 2): Bloom makes Sunny offspring, Flow Deep, Mist Shimmer.
            if (shop.TintPort) body.Add(TintRow(shop.Tint, "offspring", c => shop.TintCheck(c, out string w) == GrowthFailure.None ? null : w, c => s.Act(() => shop.TintWater(c)), () => s.Act(shop.ClearTint),
                shop.Tint == null ? null : "The water is tinted with " + GrowthShop.Name(shop.Tint) + ": the next offspring is " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, shop.Tint) + "."));
            int parents = shop.ActivePairs.Sum(p => p.Parents.Count);
            if (parents > 0)
                body.Add(Wide(parents > 1 ? "Take them out" : "Take it out", () => { foreach (var p in shop.ActivePairs.SelectMany(x => x.Parents).ToList()) if (shop.Carried.Count < GrowthShop.CarryLimit) shop.TakeParent(p.Id); s.SaveNow(); },
                    false, shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full"));
            UpgradeSection(body, "breeding");
            Footer(body, null, ("Pair log · " + shop.PairLog.Count + " pairs tried", (Action)(() => OpenJournalAt("Pair log"))));
        }

        static string PairLine(GrowthPairEntry p) =>
            GrowthContent.Get(p.A).Name + " × " + GrowthContent.Get(p.B).Name + " → " + GrowthContent.Get(p.Species).Name + (p.Count > 1 ? " ×" + p.Count : "") +
            (GrowthContent.Get(p.Species).Kind == SpeciesKind.Crown ? " (a crown!)" : "");

        /// <summary>GR.13's family shelf: a tile for each of a family's eight, found ones in colour with their colours found, the rest as silhouettes with their hint.</summary>
        VisualElement FamilyShelf(FamilyDef family, System.Func<SpeciesDef, bool> reachable)
        {
            var shop = s.Shop;
            var members = GrowthContent.Species.Where(x => x.Family == family.Id).ToList();
            var shelf = Box("gr-shelf");
            shelf.Add(L(family.Name + " · " + family.Size.ToString().ToLowerInvariant() + " · " + members.Count(m => shop.Discovered.Contains(m.Id)) + "/" + members.Count +
                " · colours " + members.Sum(m => shop.Journal.Count(j => j.StartsWith(m.Id + "/"))) + "/" + members.Count * GrowthContent.ColourCount, "gr-shelf__head"));
            var row = Box("gr-shelf__row");
            foreach (var sp in members)
            {
                bool known = shop.Discovered.Contains(sp.Id);
                var tile = Box("gr-jt");
                if (!known) tile.AddToClassList("gr-jt--unknown");
                if (sp.Kind == SpeciesKind.Crown) tile.AddToClassList("gr-jt--crown");
                string clue = sp.Kind != SpeciesKind.Founder ? (sp.Kind == SpeciesKind.Crown ? "the family's crown: one special pair of its own, never an average" :
                    shop.HasBreedingTank ? "a hybrid: try pairs in the breeding tank" : "a hybrid, bred rather than mixed (the breeding tank, era 2)") :
                    "uses " + string.Join(" and ", Enumerable.Range(0, sp.Recipe.Length).Where(i => sp.Recipe[i] > 0).Select(i => GrowthContent.Cultures[i].Name));
                var bred = shop.PairLog.FirstOrDefault(p => p.Species == sp.Id);
                string how = sp.Recipe != null ? GrowthReport.Recipe(sp.Recipe) : bred != null ? "bred from " + GrowthContent.Get(bred.A).Name + " × " + GrowthContent.Get(bred.B).Name : "bred";
                tile.tooltip = known ? sp.Name + " · " + sp.Kind.ToString().ToLowerInvariant() + " · " + how + " · " + sp.Price + " cr" : "??? · " + clue;
                var g = new GrowthGlyph(known ? GrowthCreatureView.ColourOf(sp) : new Color(0.72f, 0.67f, 0.57f, 0.55f)); g.AddToClassList("gr-jt__glyph"); tile.Add(g);
                tile.Add(L(known ? sp.Name : "?", "gr-jt__name"));
                tile.Add(L(known ? sp.Kind.ToString().ToLowerInvariant() : sp.Kind == SpeciesKind.Founder ? clue : sp.Kind == SpeciesKind.Crown ? "crown" : "hybrid", "gr-jt__hint"));
                var dots = Box("gr-jt__dots");
                foreach (Colour c in System.Enum.GetValues(typeof(Colour)))
                {
                    var d = Box("gr-jt__dot");
                    if (shop.Journal.Contains(GrowthShop.JournalKey(sp.Id, c))) { d.AddToClassList("gr-jt__dot--got"); d.style.backgroundColor = GrowthCreatureView.ColourOf(sp, c); }
                    dots.Add(d);
                }
                tile.Add(dots);
                row.Add(tile);
            }
            shelf.Add(row);
            return shelf;
        }

        /// <summary>
        /// GR-13's journal, a planning view: Goals (the pinboard's papers), Creatures (a shelf per family, paged rather than
        /// scrolled), Cultures, Mix log and Pairs, each its own tab.
        /// </summary>
        void FillJournal()
        {
            var shop = s.Shop;
            var body = Notebook("Journal", "Discovered " + shop.Discovered.Count + " of " + GrowthContent.SpeciesCount + " species · journal " + shop.Journal.Count + "/" + GrowthContent.JournalEntries, "Goals");
            // GR-11: the objective board (GR.12), mirrored from the pinboard.
            BoardSections(body);
            // Creatures: a family shows once you have the cultures for one of its founders or have found one of it.
            var creatures = AddTab("Creatures");
            bool Reachable(SpeciesDef sp) => sp.Recipe != null && Enumerable.Range(0, sp.Recipe.Length).All(i => sp.Recipe[i] == 0 || shop.Owns(GrowthContent.Cultures[i].Id) || shop.Vials.ContainsKey(GrowthContent.Cultures[i].Id));
            var shelves = GrowthContent.Families.Where(family => family.Id == "brookkin" || GrowthContent.Species.Any(m => m.Family == family.Id && (shop.Discovered.Contains(m.Id) || m.Kind == SpeciesKind.Founder && Reachable(m))))
                .Select(f => FamilyShelf(f, Reachable)).ToList();
            var pages = new List<VisualElement>();
            // Two families a page, so a page fits the sheet at 1280×720 without scrolling.
            for (int i = 0; i < shelves.Count; i += 2)
            {
                var page = Box("gr-body");
                foreach (var shelf in shelves.Skip(i).Take(2)) page.Add(shelf);
                pages.Add(page);
            }
            Pager(creatures, pages, "Families " + shelves.Count + " of " + GrowthContent.Families.Length);
            // Cultures: all seven from the start, owned ones with their source and level, the rest greyed with where to get them (GR.6).
            var cultures = AddTab("Cultures");
            var tiles = Box("gr-items");
            foreach (var c in GrowthContent.Cultures)
            {
                bool owned = shop.Owns(c.Id);
                var tile = ShopItem(owned || shop.Vials.ContainsKey(c.Id) ? CultureColour(c.Id) : new Color(0.62f, 0.62f, 0.62f), c.Name,
                    owned ? (c.Source == CultureSource.Vat ? "Conditioning vat" : c.Source == CultureSource.Research ? "Researched, its grower" : c.Source == CultureSource.CatalogGrower ? "Grower from the supplier" : "Grower") + " · level " + shop.Level("grower") :
                    c.Source == CultureSource.Research ? "Research at the pinboard" : "From the supplier catalog, page " + c.Stars,
                    shop.Vials.TryGetValue(c.Id, out int doses) ? "starter vial: " + doses + " doses" : owned ? "yours" : c.Source == CultureSource.Research ? c.Rp + " RP" : c.Credits + " credits", !owned);
                tile.tooltip = CultureLine(c);
                tiles.Add(tile);
            }
            cultures.Add(tiles);
            // GR-13: the mix log lives here (the incubator's card links to it): each mix tried and what it made, newest first.
            var mix = AddTab("Mix log");
            mix.Add(L("Mix log · " + shop.MixLog.Count + " different mixes tried", "gr-label"));
            if (shop.MixLog.Count == 0) mix.Add(L("Every mix makes a creature. Try two or three doses at the incubator.", "gr-hint"));
            var mixes = Box("gr-logrows");
            foreach (var m in shop.MixLog.AsEnumerable().Reverse().Take(30))
            {
                var row = Box("gr-logrow");
                for (int i = 0; i < m.Doses.Length; i++) for (int n = 0; n < m.Doses[i]; n++) row.Add(Pip(CultureColour(GrowthContent.Cultures[i].Id)));
                row.Add(L("→ " + GrowthContent.Get(m.Species).Name + (m.Count > 1 ? " ×" + m.Count : "") + (m.Exact ? " · its recipe" : m.DistanceToNew == 1 ? " · one dose from something new" : ""), "gr-logrow__text"));
                mixes.Add(row);
            }
            mix.Add(mixes);
            if (shop.MixLog.Count > 30) mix.Add(L("And " + (shop.MixLog.Count - 30) + " older.", "gr-hint"));
            // GR-05: the pair log, every pair tried and what it makes.
            var pairs = AddTab("Pairs");
            pairs.Add(L("Pair log · " + shop.PairLog.Count + " pairs tried", "gr-label"));
            if (shop.PairLog.Count == 0) pairs.Add(L(shop.HasBreedingTank ? "Put two creatures in the breeding tank: every pair makes something." : "Pairs come with the breeding tank (era 2).", "gr-hint"));
            var pairRows = Box("gr-logrows");
            foreach (var p in shop.PairLog.AsEnumerable().Reverse().Take(30))
            {
                var row = Box("gr-logrow");
                row.Add(Pip(GrowthCreatureView.ColourOf(GrowthContent.Get(p.A)))); row.Add(Pip(GrowthCreatureView.ColourOf(GrowthContent.Get(p.B))));
                row.Add(L(PairLine(p), "gr-logrow__text"));
                pairRows.Add(row);
            }
            pairs.Add(pairRows);
            if (shop.PairLog.Count > 30) pairs.Add(L("And " + (shop.PairLog.Count - 30) + " older.", "gr-hint"));
            NotebookFooter("Back", null, null, "Tab closes it too");
        }

        /// <summary>GR-13's night receipt: the day's numbers on a paper receipt, why some didn't buy, the reputation, and Good night.</summary>
        void FillReceipt()
        {
            var shop = s.Shop;
            var d = shop.LastDay;
            var body = Notebook(d == null ? "Night" : "Day " + d.Day + " is over", "Night " + shop.Day + ": prepare at your own pace");
            if (d != null)
            {
                var stats = Box("gr-receipt");
                stats.Add(Stat(d.Revenue + " credits", "taken today"));
                var chips = Box("gr-chips");
                chips.Add(Chip(d.Visitors + " visitors came in"));
                chips.Add(Chip(d.Sales + " bought"));
                if (d.WalkedPast > 0) chips.Add(Chip(d.WalkedPast + " looked in at empty tanks", true));
                if (d.Refusals > 0) chips.Add(Chip(d.Refusals + " found nothing to buy", true));
                if (d.Impatient > 0) chips.Add(Chip(d.Impatient + " got tired of waiting", true));
                if (d.Slapped > 0) chips.Add(Chip(d.Slapped + " left after a slap", true));
                if (d.LaughedOff > 0) chips.Add(Chip(d.LaughedOff + " laughed off a slap"));
                stats.Add(chips);
                stats.Add(L(d.Visitors + " visitors came in and " + d.Sales + " bought, for " + d.Revenue + " credits.", "gr-hint"));
                if (d.WalkedPast > 0) stats.Add(L("More creatures, more sales: " + d.WalkedPast + " more looked in while your tanks were empty.", "gr-hint"));
                var rep = L("Reputation " + (d.ReputationGained >= 0 ? "+" : "") + d.ReputationGained + " · " + StarsText(d.StarsAfter) + (d.StarsAfter > d.StarsBefore ? " — a new star!" : ""), "gr-strong");
                rep.style.marginTop = 10;
                stats.Add(rep);
                body.Add(stats);
                ExhibitionReceipt(body, d);
            }
            var goal = L("Goal: " + shop.EraGoal, "gr-hint"); goal.style.marginTop = 12;
            body.Add(goal);
            var row = Box("gr-nb__foot");
            row.Add(L("Esc", "es-keycap"));
            row.Add(L("Good night", "gr-foot__text"));
            if (d != null && d.ExhibitionWon) { var see = Button("See the results", () => s.SetCard(GrowthCard.Results), true); see.style.marginTop = 0; row.Add(see); }
            var night = Button("Good night", () => s.SetCard(GrowthCard.None), d == null || !d.ExhibitionWon); night.style.marginTop = 0; night.style.marginLeft = 10;
            row.Add(night);
            footer = row;
            card.Add(row);
        }

        void FillWelcome()
        {
            bool guided = s.Shop.Guide == GrowthGuideMode.On;
            var body = Notebook("Evolution Shop", guided ? "your first night" : "Day " + s.Shop.Day + " · Era " + s.Shop.Era);
            body.Add(L("A tiny shop and a back room. Make creatures, fill your first tank, sell them, and grow.", "gr-strong"));
            var steps = new[]
            {
                "Back room: hold the left mouse on the Bloom and Flow growers to fill your siphon (3 doses).",
                "The incubator (E): put 2–3 doses in and start. Every mix makes a creature; an exact recipe finds a new one.",
                "Carry hatchlings (E) to the tank in the shop (E puts one in). Look closely at the glass to peek.",
                "The counter (E) opens the shop for a 5:30 day. Hold the mouse on it to bag each sale.",
                "Tab: journal · Esc: pause · the pinboard has your goals; the HUD shows the next step.",
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var step = Box("gr-step");
                step.Add(L((i + 1).ToString(), "gr-step__number"));
                step.Add(L(steps[i], "gr-step__text"));
                body.Add(step);
            }
            // GR-14: the guided first night.
            if (guided)
            {
                var guide = Box("gr-callout"); guide.style.marginTop = 10;
                guide.Add(L("Your first night is guided: a marker shows the next thing to use, and the line at the bottom says what to do. Skip it any time from the pause menu (Esc).", "gr-strong"));
                var skip = Link("Play without guidance", () => { s.Shop.SkipGuide(); s.SaveNow(); s.SetCard(GrowthCard.None); });
                skip.style.alignSelf = Align.FlexStart; skip.style.marginLeft = 0; skip.style.marginTop = 8;
                guide.Add(skip);
                body.Add(guide);
            }
            if (s.Notice != null) { var notice = Box("gr-callout"); notice.style.marginTop = 10; notice.Add(L(s.Notice, "gr-hint")); body.Add(notice); }
            NotebookFooter("Start", () => s.SetCard(GrowthCard.None), "Start");
        }

        public static string GagName(GrowthGagLevel level) => level == GrowthGagLevel.Full ? "Full" : level == GrowthGagLevel.Fewer ? "Fewer" : "Off";

        void FillPause()
        {
            var body = Notebook("Paused", "Day " + s.Shop.Day + " · " + s.Shop.Credits + " credits · " + StarsText(s.Shop.Stars) + " · saved to " + System.IO.Path.GetFileName(s.SavePath));
            body.Add(L("Settings", "gr-label"));
            body.Add(Item("Peek", "look closely at a tank's glass to read each creature", ChipButton(s.PeekOn ? "On" : "Off", () => { s.PeekOn = !s.PeekOn; s.SaveNow(); }, true)));
            body.Add(Item("Reduced motion", "no creature tricks, gentler movement", ChipButton(s.ReducedMotion ? "On" : "Off", () => { s.ReducedMotion = !s.ReducedMotion; s.SaveNow(); }, true)));
            // GR-16 wave 5: the sounds and the machines' hum, on one row (the card fits at 1280x720), each full, half or off.
            body.Add(Item("Sound", "the stations' sounds, and the machines' hum",
                ChipButton("Sounds " + GrowthAudio.Name(s.SoundLevel), () => { s.SoundLevel = GrowthAudio.Next(s.SoundLevel); s.SaveNow(); }, true),
                ChipButton("Hum " + GrowthAudio.Name(s.HumLevel), () => { s.HumLevel = GrowthAudio.Next(s.HumLevel); s.SaveNow(); }, true)));
            // GR-18 wave 4 (§6.7): the silly moments' sounds, how many there are, and the slap's key, on one row.
            var gags = s.Gags;
            body.Add(Item("Comedy", gags != null && gags.Rebinding ? (gags.RebindProblem ?? "press the slap's new key (Esc keeps it)") : "the silly moments and the slap's key",
                ChipButton("Humour " + GrowthAudio.Name(s.HumourLevel), () => { s.HumourLevel = GrowthAudio.Next(s.HumourLevel); s.SaveNow(); }, true),
                ChipButton("Gags " + GagName(s.GagLevel), () => { s.GagLevel = (GrowthGagLevel)(((int)s.GagLevel + 1) % 3); s.SaveNow(); }, true),
                ChipButton(gags != null && gags.Rebinding ? "Slap key: press a key" : "Slap key " + GrowthGags.KeyName(s.SlapKey), () => gags?.StartRebind(), true)));
            body.Add(Item("Mouse", "how far the view turns", ChipButton(s.Sensitivity.ToString("0.0") + "×", () => { s.Sensitivity = s.Sensitivity >= 2f ? 0.5f : s.Sensitivity + 0.25f; s.SaveNow(); }, true)));
            if (s.Shop.Guide == GrowthGuideMode.On)
            {
                // GR-16 wave 5: a line of its own rather than a settings row, so with the sound row the card still fits at 1280x720.
                var guide = Box("gr-row"); guide.style.marginTop = 8; guide.style.alignItems = Align.Center;
                guide.Add(L("Guided first night: a marker and the line at the bottom show what to do next", "gr-hint"));
                var skipGuide = ChipButton("Skip it", () => { s.Shop.SkipGuide(); s.SaveNow(); }, true); skipGuide.style.marginLeft = 8;
                guide.Add(skipGuide);
                body.Add(guide);
            }
            var danger = Box("gr-row"); danger.style.marginTop = 14;
            var over = Button("Start over (a new shop)", () => s.StartOver()); over.RemoveFromClassList("es-button--secondary"); over.AddToClassList("es-button--danger");
            danger.Add(over);
            danger.Add(Button("Save and quit", () => { s.SaveNow(); Application.Quit(); }));
            body.Add(danger);
            NotebookFooter("Resume", () => s.SetCard(GrowthCard.None), "Resume");
        }
    }
}
