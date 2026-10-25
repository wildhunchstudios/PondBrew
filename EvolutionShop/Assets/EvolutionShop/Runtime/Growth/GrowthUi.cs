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
        VisualElement root, hud, cardLayer, screenLayer, promptBox, promptProgress, card, dayIcon, nextBox, carryBox, toastBox, eventBox;
        readonly List<VisualElement> stars = new List<VisualElement>();
        Label dayLabel, clockLabel, creditsLabel, rpLabel, nextLabel, promptName, promptStatus, siphonLabel, carryLabel, hintLabel, toastLabel, eventLabel;
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
            nextBox = Box("es-next");
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
            hintLabel = Text(""); hintLabel.style.color = new Color(1f, 0.92f, 0.75f); hintLabel.style.fontSize = 18; hintLabel.style.marginLeft = 8;
            prompts.Add(hintLabel);
            var top = Box("es-hud-top");
            eventBox = Note("gr-toast--event", ShopUiTheme.Symbol.Star, out eventLabel);
            toastBox = Note(null, ShopUiTheme.Symbol.Leaf, out toastLabel);
            top.Add(eventBox); top.Add(toastBox); hud.Add(top);
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
            // GR-13: one next step, the first goal still to do (the pinboard and the journal have the whole list).
            nextLabel.text = shop.Objectives().Where(g => !g.done).Select(g => g.text).FirstOrDefault() ?? shop.EraGoal;
            bool free = s.Card == GrowthCard.None;
            hud.style.display = s.Card == GrowthCard.Pause || s.Card == GrowthCard.Welcome ? DisplayStyle.None : DisplayStyle.Flex;
            nextBox.style.display = free ? DisplayStyle.Flex : DisplayStyle.None;
            siphonLabel.text = shop.Held.Count == 0 ? "" : "Siphon " + shop.Held.Count + "/" + shop.SiphonCapacity + ": " + string.Join(", ", shop.Held.GroupBy(c => c).Select(g => g.Count() + " " + GrowthShop.Name(g.Key)));
            carryLabel.text = shop.Carried.Count == 0 ? "" : "Carrying " + shop.Carried.Count + "/" + GrowthShop.CarryLimit + ": " + string.Join(", ", shop.Carried.Select(GrowthSession.Named));
            siphonLabel.style.display = shop.Held.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            carryLabel.style.display = shop.Carried.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            carryBox.style.display = free && (shop.Held.Count > 0 || shop.Carried.Count > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            string name = free ? s.FocusName(out string status) : null;
            promptBox.style.display = name == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (name != null)
            {
                promptName.text = name; promptStatus.text = s.FocusName(out status) != null ? status : "";
                float collect = s.Focused != null && s.Focused.Kind == GrowthTargetKind.Vial ? shop.VialProgress : s.Focused != null && s.Focused.Kind == GrowthTargetKind.Buffer ? shop.BufferProgress : shop.CollectProgress;
                float progress = s.CollectingNow ? collect / Mathf.Max(0.01f, shop.HoldSeconds) : s.ServingNow ? shop.ServeProgress / Mathf.Max(0.01f, shop.ServeSeconds) :
                    s.PumpingNow ? shop.PumpProgress / Mathf.Max(0.01f, shop.PumpDoseSeconds) : 0;
                promptProgress.style.width = new Length(Mathf.Clamp01(progress) * 100, LengthUnit.Percent);
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
                default: Toast(e.Text, e.Kind == GrowthEventKind.Refusal || e.Kind == GrowthEventKind.Impatient || e.Kind == GrowthEventKind.StaffHome || e.Kind == GrowthEventKind.ExhibitionFailed); return;
            }
        }

        // ---------------------------------------------------------------- cards

        public void ShowCard(GrowthCard which, string target)
        {
            if (!Ready) return;
            // A card's chosen tab, its open Move list and a Sell waiting for its yes last while it stays open (refreshes rebuild it).
            string key = which + "|" + target;
            if (key != tabCard) { tabCard = key; tabChosen = null; moveOpen = false; sellAsked = false; }
            cardLayer.Clear(); screenLayer.Clear(); card = null; liveLabel = null; statusLabel = null;
            tabList.Clear(); tabRow = null; footer = null; rings.Clear();
            tip.style.display = DisplayStyle.None; tipAnchor = null;
            if (which == GrowthCard.None) return;
            bool screen = which == GrowthCard.Journal || which == GrowthCard.Receipt || which == GrowthCard.Welcome || which == GrowthCard.Pause || which == GrowthCard.Results;
            card = screen ? ScreenCard() : StationCard(which == GrowthCard.Incubator || which == GrowthCard.Tank || which == GrowthCard.Crate || which == GrowthCard.Catalog || which == GrowthCard.Breeding || which == GrowthCard.Helper ? 580 : which == GrowthCard.Place || which == GrowthCard.Board || which == GrowthCard.Workbench || which == GrowthCard.Counter || which == GrowthCard.Grower || which == GrowthCard.Buffer || which == GrowthCard.Staff ? 540 : which == GrowthCard.PowerBoard || which == GrowthCard.Sorter ? 600 : 470);
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

        VisualElement ScreenCard()
        {
            var c = new VisualElement(); c.AddToClassList("es-screen-card");
            c.style.width = 980; c.style.maxHeight = new Length(86, LengthUnit.Percent);
            return c;
        }

        VisualElement ScreenHead(string title)
        {
            var t = new Label(title); t.AddToClassList("es-screen-card__title"); t.style.fontSize = 44;
            card.Add(t);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.marginTop = 12;
            card.Add(scroll);
            return scroll.contentContainer;
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
            var body = Head(vat ? "Conditioning vat (" + GrowthShop.Name(culture) + ")" : GrowthShop.Name(culture) + " grower", vat ? "vat level " + shop.Level("vat") : "level " + shop.Level("grower"), vat ? "Vat" : "Grower");
            Live(body);
            body.Add(Text("Hold the left mouse on a grower to fill the siphon: " + (shop.PerHold > 1 ? shop.PerHold + " doses" : "one dose") + " every " + shop.HoldSeconds.ToString("0.0") + " s. It holds " + shop.GrowerCapacityOf(culture) +
                " and refills one dose every " + shop.RefillSecondsOf(culture).ToString("0.#") + " s by itself."));
            body.Add(Text("Siphon: " + shop.Held.Count + "/" + shop.SiphonCapacity + " doses.", "es-text--muted"));
            TapSection(body, culture);
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

        void FillPlace(string place)
        {
            var shop = s.Shop;
            if (GrowthShop.LabPlace(place) != null) { FillRackPlace(place); return; }
            var spot = GrowthShop.Place(place);
            if (spot == null) { s.SetCard(GrowthCard.None); return; }
            var body = Head("Empty place", (spot.Unit == "unit-b" ? "shop unit B" : "shop unit A") + " · " + spot.Size);
            body.Add(Text("A " + spot.Size + " place: room for up to " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m.", "es-text--muted"));
            foreach (var kind in GrowthShop.ShopKinds)
            {
                var def = GrowthContent.Container(kind);
                var first = def.Levels[0];
                var failure = shop.BuyCheck(kind, place, out string why);
                var option = new VisualElement(); option.style.marginTop = 8;
                option.Add(Text(def.Name + ": " + first.Change + ", " + Holds(def) + ", " + GrowthShop.Footprint(def) + " · " + first.Credits + " credits",
                    failure == GrowthFailure.TooBigForPlace || failure == GrowthFailure.Locked ? "es-text--muted" : "es-text"));
                var k = kind;
                option.Add(Button(failure == GrowthFailure.None ? "Buy a " + def.Name + " for " + first.Credits + " cr" : why,
                    () => { if (s.Act(() => shop.BuyContainer(k, place)).Success) s.SetCard(GrowthCard.None); }, kind == "starter", failure == GrowthFailure.None));
                body.Add(option);
            }
            var movers = shop.ShopContainers.Where(c => shop.MoveCheck(c.Id, place, out _) == GrowthFailure.None).ToList();
            if (movers.Count > 0)
            {
                body.Add(Text("Or move a tank here, with its creatures:", "es-text--muted"));
                var row = Row();
                foreach (var m in movers) { var id = m.Id; row.Add(Button("The " + m.Def.Name + " from " + m.Place, () => { if (s.Act(() => shop.MoveContainer(id, place)).Success) s.SetCard(GrowthCard.None); })); }
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
            var body = Head("Counter", "level " + shop.Level("counter"), "Counter");
            Live(body);
            if (shop.Phase == GrowthPhase.Night)
            {
                int stock = shop.Stock.Count();
                body.Add(Text(stock == 0 ? "Your tanks are empty: nobody comes into an empty shop." : stock + " creature" + (stock == 1 ? "" : "s") + " on show. A day lasts 5:30; visitors pick one each and wait here to pay."));
                body.Add(Button("Open the shop", () => { if (s.Act(shop.OpenShop).Success) s.SetCard(GrowthCard.None); }, true));
            }
            else
            {
                bool pip = shop.Working("pip");
                body.Add(Text(pip && !shop.SecondTill ? "Pip is at the till, bagging each order in " + GrowthShop.PipServeSeconds[shop.Helper("pip").Level - 1].ToString("0.0") + " s. The counter's second till (level 4) lets you serve beside Pip. The queue holds " + shop.QueueLength + " (" + shop.Queue.Count() + " now)." :
                    "Hold the left mouse on the counter to bag a sale: " + shop.ServeSeconds.ToString("0.0") + " s each" + (pip ? ", beside Pip at the second till" : "") + ". The queue holds " + shop.QueueLength + " (" + shop.Queue.Count() + " now); others wait by the tanks."));
                foreach (var v in shop.Queue.OrderByDescending(x => x.Clock).ThenBy(x => x.Id))
                {
                    var spec = shop.Stock.FirstOrDefault(x => x.Id == v.SpecimenId);
                    body.Add(Text("· " + Visitor(v.Type) + " wants a " + (spec?.Def.Name ?? "?") + " (" + (spec == null ? 0 : shop.Price(spec.Species)) + " cr) · patience " + Mathf.CeilToInt(v.Patience) + " s"));
                }
                if (!shop.Queue.Any()) body.Add(Text("No one is waiting.", "es-text--muted"));
            }
            UpgradeSection(body, "counter");
            Footer(body);
        }

        static string Visitor(string type) => GrowthContent.Visitors.First(v => v.Id == type).Name;

        void FillWorkbench()
        {
            var shop = s.Shop;
            var body = Head("Research bench", "bench " + shop.Level("bench") + " · siphon " + shop.Level("siphon"), "Bench");
            body.Add(Text("Your siphon holds " + shop.SiphonCapacity + " doses and takes " + (shop.PerHold > 1 ? shop.PerHold + " doses" : "one dose") + " every " + shop.HoldSeconds.ToString("0.0") + " s" +
                (shop.Vacuum ? "; the vacuum keeps pulling from the next grower in reach when the one you aim at runs dry." : ".")));
            UpgradeSection(body, "siphon", "The siphon");
            body.Add(Text(shop.Microscope ? "The microscope: the incubator card says how many doses each mix is from something new." :
                "Notes: undiscovered founders show the cultures they use in the journal. The microscope (level 2) measures how far a mix is from something new.", "es-text--muted"));
            UpgradeSection(body, "bench", "The research bench");
            AnalyzerSection(body);
            Footer(body);
        }

        void FillUnitB()
        {
            var shop = s.Shop;
            var unit = GrowthShop.UnitBDef;
            var body = Head("Shop unit B", shop.UnitB ? "open" : unit.Credits + " credits");
            body.Add(Text("Open the wall into the unit next door: four more places for tanks and one more visitor at a time."));
            var failure = shop.UnitBCheck(out string why);
            body.Add(Text("Needs era 2 (2 stars, 3 creatures discovered, 2 tanks), 2 stars and " + unit.Credits + " credits, at night.", "es-text--muted"));
            foreach (var (text, done) in shop.Objectives().Where(_ => shop.Era == 1)) body.Add(Text((done ? "✓ " : "· ") + text, done ? "es-req--done" : "es-req"));
            body.Add(Button(failure == GrowthFailure.None ? "Open unit B for " + unit.Credits + " cr" : why, () => { if (s.Act(shop.BuyUnitB).Success) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None));
            Footer(body);
        }

        void FillBoard()
        {
            var shop = s.Shop;
            var body = Head("Pinboard", shop.ExhibitionDone ? "free play" : "era " + shop.Era + ": " + GrowthContent.Eras[shop.Era - 1].Name);
            // GR-11: the objective board (GR.12): now, next and the long goal, the same at the top of the journal.
            BoardSections(body);
            // GR-04: research turns research points (earned by discoveries) into Spark and, much later, Dusk.
            var research = new VisualElement(); research.style.marginTop = 10; research.style.borderTopWidth = 2; research.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); research.style.paddingTop = 6;
            research.Add(Text("Research · " + shop.Rp + " research points (each discovery brings some)"));
            foreach (var c in GrowthContent.Cultures.Where(x => x.Source == CultureSource.Research))
            {
                var failure = shop.ResearchCheck(c.Id, out string why);
                string id = c.Id;
                var b = Button(failure == GrowthFailure.None ? "Research " + c.Name + " · " + c.Rp + " RP" : c.Name + ": " + why, () => s.Act(() => shop.Research(id)), failure == GrowthFailure.None, failure == GrowthFailure.None);
                b.style.borderLeftWidth = 8; b.style.borderLeftColor = CultureColour(c.Id);
                research.Add(b);
            }
            body.Add(research);
            Footer(body);
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

        void FillCatalog()
        {
            var shop = s.Shop;
            var body = Head("Supplier catalog", shop.Phase == GrowthPhase.Night ? "orders arrive when the shop opens" : "orders are placed at night");
            body.Add(Text("Order at night; what you order arrives when the shop next opens. Pages open with stars. Bloom and Flow growers are yours from the start.", "es-text--muted"));
            for (int page = 2; page <= 5; page++)
            {
                var items = GrowthContent.Catalog.Where(i => i.Stars == page).ToList();
                if (items.Count == 0) continue;
                var title = Text("Page " + page + " · " + page + " stars" + (shop.Stars < page ? " (opens at " + page + " stars)" : ""));
                title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginTop = 8;
                body.Add(title);
                foreach (var item in items)
                {
                    var failure = shop.OrderCheck(item.Id, out string why);
                    var row = new VisualElement(); row.style.marginTop = 4;
                    var name = Text(item.Name + " · " + item.Credits + " credits" + (item.Rp > 0 ? " and " + item.Rp + " RP" : ""), shop.Stars < page ? "es-text--muted" : "es-text");
                    name.style.borderLeftWidth = 8; name.style.borderLeftColor = CultureColour(item.Culture); name.style.paddingLeft = 6;
                    row.Add(name);
                    row.Add(Text(item.Text, "es-text--muted"));
                    string id = item.Id;
                    if (failure == GrowthFailure.Ordered && shop.Orders.Contains(id))
                        row.Add(Button("On order · take it back", () => s.Act(() => shop.CancelOrder(id)), false, shop.Phase == GrowthPhase.Night));
                    else
                        row.Add(Button(failure == GrowthFailure.None ? "Order for " + item.Credits + " cr" + (item.Rp > 0 ? " and " + item.Rp + " RP" : "") : why,
                            () => s.Act(() => shop.Order(id)), item.Kind != CatalogKind.Vial, failure == GrowthFailure.None));
                    body.Add(row);
                }
            }
            if (shop.Orders.Count > 0)
                body.Add(Text("On order: " + string.Join(", ", shop.Orders.Select(o => GrowthContent.CatalogItem(o).Name)) + ". They arrive when the shop opens.", "es-card__reason"));
            Footer(body);
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

        /// <summary>GR.13's family chart: a tile for each of a family's eight, named once known, with its colours found.</summary>
        VisualElement FamilyTiles(FamilyDef family)
        {
            var shop = s.Shop;
            var row = Row(); row.style.flexWrap = Wrap.NoWrap; row.style.marginTop = 4;
            foreach (var sp in GrowthContent.Species.Where(x => x.Family == family.Id))
            {
                bool known = shop.Discovered.Contains(sp.Id);
                var colour = known ? GrowthCreatureView.ColourOf(sp) : new Color(0.62f, 0.62f, 0.62f);
                var tile = new VisualElement(); tile.style.width = 106; tile.style.height = 46; tile.style.marginRight = 4; tile.style.paddingLeft = 6; tile.style.paddingTop = 2;
                tile.style.overflow = Overflow.Hidden;
                tile.style.backgroundColor = known ? Color.Lerp(colour, Color.white, 0.62f) : new Color(0.9f, 0.88f, 0.82f);
                tile.style.borderTopWidth = tile.style.borderBottomWidth = tile.style.borderLeftWidth = tile.style.borderRightWidth = sp.Kind == SpeciesKind.Crown ? 3 : 2;
                tile.style.borderTopColor = tile.style.borderBottomColor = tile.style.borderLeftColor = tile.style.borderRightColor = sp.Kind == SpeciesKind.Crown ? new Color(0.85f, 0.62f, 0.18f) : colour;
                tile.style.borderTopLeftRadius = tile.style.borderTopRightRadius = tile.style.borderBottomLeftRadius = tile.style.borderBottomRightRadius = 8;
                var name = Text((known ? sp.Name : "?") + " · " + (sp.Kind == SpeciesKind.Founder ? "F" : sp.Kind == SpeciesKind.Hybrid ? "H" : "C"));
                name.style.fontSize = 13; name.style.whiteSpace = WhiteSpace.NoWrap; name.style.marginTop = 0; name.style.marginBottom = 0;
                tile.Add(name);
                var dots = Row(); dots.style.flexWrap = Wrap.NoWrap;
                foreach (Colour c in Enum.GetValues(typeof(Colour)))
                {
                    bool found = shop.Journal.Contains(GrowthShop.JournalKey(sp.Id, c));
                    var d = Dot(found ? GrowthCreatureView.ColourOf(sp, c) : Color.clear, 11);
                    d.style.borderTopWidth = d.style.borderBottomWidth = d.style.borderLeftWidth = d.style.borderRightWidth = 1;
                    d.style.borderTopColor = d.style.borderBottomColor = d.style.borderLeftColor = d.style.borderRightColor = new Color(0.5f, 0.46f, 0.38f);
                    dots.Add(d);
                }
                tile.Add(dots);
                row.Add(tile);
            }
            return row;
        }

        void FillJournal()
        {
            var shop = s.Shop;
            var body = ScreenHead("Journal");
            // GR-11: the objective board, mirrored from the pinboard (GR.12).
            var goals = Text("Goals"); goals.style.fontSize = 24; goals.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(goals);
            BoardSections(body);
            // GR.6: the Cultures page shows all seven from the start: owned ones with their source and level, the rest
            // greyed with where to get them.
            var cultures = Text("Cultures"); cultures.style.fontSize = 24; cultures.style.unityFontStyleAndWeight = FontStyle.Bold; cultures.style.marginTop = 14;
            body.Add(cultures);
            foreach (var c in GrowthContent.Cultures)
            {
                bool owned = shop.Owns(c.Id);
                var row = Row(); row.style.marginTop = 4; row.style.flexWrap = Wrap.NoWrap;
                row.Add(Dot(owned || shop.Vials.ContainsKey(c.Id) ? CultureColour(c.Id) : new Color(0.62f, 0.62f, 0.62f), 22));
                var entry = Text(CultureLine(c), owned ? "es-text" : "es-text--muted");
                entry.style.flexGrow = 1; entry.style.flexShrink = 1; entry.style.marginLeft = 8;
                row.Add(entry);
                body.Add(row);
            }
            var creatures = Text("Creatures"); creatures.style.fontSize = 24; creatures.style.unityFontStyleAndWeight = FontStyle.Bold; creatures.style.marginTop = 14;
            body.Add(creatures);
            body.Add(Text("Discovered " + shop.Discovered.Count + " of " + GrowthContent.SpeciesCount + " species · journal " + shop.Journal.Count + "/" + GrowthContent.JournalEntries + " (each species in four colours: Classic, Sunny, Deep, Shimmer)."));
            body.Add(Text("Each undiscovered founder shows the cultures it uses; an exact recipe finds it. Hybrids and crowns are bred. A family shows once you have the cultures for one of its founders or have found one of it.", "es-text--muted"));
            bool Reachable(SpeciesDef sp) => sp.Recipe != null && Enumerable.Range(0, sp.Recipe.Length).All(i => sp.Recipe[i] == 0 || shop.Owns(GrowthContent.Cultures[i].Id) || shop.Vials.ContainsKey(GrowthContent.Cultures[i].Id));
            foreach (var family in GrowthContent.Families)
            {
                var members = GrowthContent.Species.Where(x => x.Family == family.Id).ToList();
                if (family.Id != "brookkin" && !members.Any(m => shop.Discovered.Contains(m.Id) || m.Kind == SpeciesKind.Founder && Reachable(m))) continue;
                var head = Text(family.Name + " · " + family.Size.ToString().ToLowerInvariant() + " · " + members.Count(m => shop.Discovered.Contains(m.Id)) + "/" + members.Count +
                    " · colours " + members.Sum(m => shop.Journal.Count(j => j.StartsWith(m.Id + "/"))) + "/" + members.Count * GrowthContent.ColourCount);
                head.style.unityFontStyleAndWeight = FontStyle.Bold; head.style.marginTop = 8;
                body.Add(head);
                body.Add(FamilyTiles(family));
                foreach (var sp in members)
                {
                    bool known = shop.Discovered.Contains(sp.Id);
                    var row = Row(); row.style.marginTop = 4; row.style.flexWrap = Wrap.NoWrap;
                    row.Add(Dot(known ? GrowthCreatureView.ColourOf(sp) : new Color(0.6f, 0.6f, 0.6f), 22));
                    string clue = sp.Kind != SpeciesKind.Founder ? (sp.Kind == SpeciesKind.Crown ? "the family's crown: one special pair of its own, never an average" :
                        shop.HasBreedingTank ? "a hybrid: try pairs in the breeding tank" : "a hybrid, bred rather than mixed (the breeding tank, era 2)") :
                        "uses " + string.Join(" and ", Enumerable.Range(0, sp.Recipe.Length).Where(i => sp.Recipe[i] > 0).Select(i => GrowthContent.Cultures[i].Name));
                    var bred = shop.PairLog.FirstOrDefault(p => p.Species == sp.Id);
                    string how = sp.Recipe != null ? GrowthReport.Recipe(sp.Recipe) : bred != null ? "bred from " + GrowthContent.Get(bred.A).Name + " × " + GrowthContent.Get(bred.B).Name : "bred";
                    var entry = Text(known ? sp.Name + " · " + sp.Kind.ToString().ToLowerInvariant() + " · " + how + " · " + sp.Price + " cr" : "??? · " + clue);
                    entry.style.flexGrow = 1; entry.style.flexShrink = 1; entry.style.marginLeft = 8; entry.style.whiteSpace = WhiteSpace.NoWrap;
                    row.Add(entry);
                    body.Add(row);
                }
            }
            // GR-13: the mix log lives here (the incubator's card links to it): each mix tried and what it made, newest first.
            var mixes = Text("Mix log · " + shop.MixLog.Count + " different mixes tried"); mixes.style.fontSize = 24; mixes.style.unityFontStyleAndWeight = FontStyle.Bold; mixes.style.marginTop = 14;
            body.Add(mixes);
            if (shop.MixLog.Count == 0) body.Add(Text("Every mix makes a creature. Try two or three doses at the incubator.", "es-text--muted"));
            foreach (var m in shop.MixLog.AsEnumerable().Reverse())
            {
                var row = Row(); row.style.marginTop = 4; row.style.flexWrap = Wrap.NoWrap;
                for (int i = 0; i < m.Doses.Length; i++) for (int n = 0; n < m.Doses[i]; n++) row.Add(Dot(CultureColour(GrowthContent.Cultures[i].Id), 16));
                var entry = Text("→ " + GrowthContent.Get(m.Species).Name + (m.Count > 1 ? " ×" + m.Count : "") + (m.Exact ? " · its recipe" : m.DistanceToNew == 1 ? " · one dose from something new" : ""));
                entry.style.marginLeft = 6; entry.style.marginBottom = 0; entry.style.flexGrow = 1;
                row.Add(entry);
                body.Add(row);
            }
            // GR-05: the pair log, every pair tried and what it makes.
            var pairs = Text("Pair log · " + shop.PairLog.Count + " pairs tried"); pairs.style.fontSize = 24; pairs.style.unityFontStyleAndWeight = FontStyle.Bold; pairs.style.marginTop = 14;
            body.Add(pairs);
            if (shop.PairLog.Count == 0) body.Add(Text(shop.HasBreedingTank ? "Put two creatures in the breeding tank: every pair makes something." : "Pairs come with the breeding tank (era 2).", "es-text--muted"));
            foreach (var p in shop.PairLog.AsEnumerable().Reverse()) body.Add(Text(PairLine(p)));
            var close = Row(); close.Add(Button("Close (Tab)", () => s.SetCard(GrowthCard.None), true)); body.Add(close);
        }

        void FillReceipt()
        {
            var shop = s.Shop;
            var d = shop.LastDay;
            var body = ScreenHead(d == null ? "Night" : "Day " + d.Day + " is over");
            if (d != null)
            {
                body.Add(Text(d.Visitors + " visitors came in and " + d.Sales + " bought, for " + d.Revenue + " credits." +
                    (d.WalkedPast > 0 ? " " + d.WalkedPast + " more looked in while your tanks were empty: more creatures, more sales." : "")));
                if (d.Refusals > 0) body.Add(Text(d.Refusals + " found nothing to buy" + (d.Impatient > 0 ? ", " + d.Impatient + " got tired of waiting" : "") + "."));
                else if (d.Impatient > 0) body.Add(Text(d.Impatient + " got tired of waiting."));
                body.Add(Text("Reputation " + (d.ReputationGained >= 0 ? "+" : "") + d.ReputationGained + " · " + StarsText(d.StarsAfter) + (d.StarsAfter > d.StarsBefore ? " — a new star!" : "")));
                ExhibitionReceipt(body, d);
            }
            body.Add(Text("Night " + shop.Day + ": prepare at your own pace. Goal: " + shop.EraGoal, "es-text--muted"));
            var row = Row();
            if (d != null && d.ExhibitionWon) row.Add(Button("See the results", () => s.SetCard(GrowthCard.Results), true));
            row.Add(Button("Good night", () => s.SetCard(GrowthCard.None), d == null || !d.ExhibitionWon));
            body.Add(row);
        }

        void FillWelcome()
        {
            var body = ScreenHead("Evolution Shop · the new start (GR-02 greybox)");
            body.Add(Text("A tiny shop and a back room. Make creatures, fill your first tank, sell them, and grow."));
            body.Add(Text("· Back room: hold the left mouse on the Bloom and Flow growers to fill your siphon (3 doses)."));
            body.Add(Text("· The incubator (E): put 2–3 doses in and mix. Every mix makes a creature; an exact recipe finds a new one."));
            body.Add(Text("· Carry hatchlings (E) to the tank in the shop (E puts one in). Look closely at the glass to peek."));
            body.Add(Text("· The counter (E) opens the shop for a 5:30 day. Hold the mouse on it to bag each sale."));
            body.Add(Text("· Tab: journal · Esc: pause · the pinboard and the HUD show your goal."));
            body.Add(Text("This is a greybox: plain shapes, placeholder creatures, its own save. The classic game is untouched.", "es-text--muted"));
            if (s.Notice != null) body.Add(Text(s.Notice, "es-card__reason"));
            var row = Row(); row.Add(Button("Start", () => s.SetCard(GrowthCard.None), true)); body.Add(row);
        }

        void FillPause()
        {
            var body = ScreenHead("Paused");
            body.Add(Text("Day " + s.Shop.Day + " · " + s.Shop.Credits + " credits · " + StarsText(s.Shop.Stars) + " · saved to " + System.IO.Path.GetFileName(s.SavePath), "es-text--muted"));
            var row = Row();
            row.Add(Button("Resume", () => s.SetCard(GrowthCard.None), true));
            row.Add(Button("Peek: " + (s.PeekOn ? "on" : "off"), () => { s.PeekOn = !s.PeekOn; s.SaveNow(); }));
            row.Add(Button("Reduced motion: " + (s.ReducedMotion ? "on" : "off"), () => { s.ReducedMotion = !s.ReducedMotion; s.SaveNow(); }));
            row.Add(Button("Mouse " + s.Sensitivity.ToString("0.0") + "×", () => { s.Sensitivity = s.Sensitivity >= 2f ? 0.5f : s.Sensitivity + 0.25f; s.SaveNow(); }));
            body.Add(row);
            var danger = Row();
            danger.Add(Button("Start over (new greybox shop)", () => s.StartOver()));
            danger.Add(Button("Save and quit", () => { s.SaveNow(); Application.Quit(); }));
            body.Add(danger);
        }
    }
}
