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
        VisualElement root, hud, cardLayer, screenLayer, promptBox, promptProgress, card, dayIcon, nextBox, carryBox, toastBox, eventBox, modeBox, modeLay, slapBox, captionBox, wishBox;
        readonly List<VisualElement> stars = new List<VisualElement>();
        bool nextShown;
        int nextRemeasure;
        Label dayLabel, clockLabel, creditsLabel, rpLabel, nextLabel, promptName, promptStatus, siphonLabel, carryLabel, hintLabel, toastLabel, eventLabel, modeTitle, modeDetail, slapKey, slapName, slapWho, captionWho, captionText, wishName, wishWho;
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
        /// <summary>Validation (GR-33, F61): how many times a card has been built, rebuilds included.</summary>
        public int CardBuilds { get; private set; }
        /// <summary>Validation (GR-33, F62): the notes in the side log now, oldest first.</summary>
        public IReadOnlyList<string> LogNotes => noteRows.Select(n => n.label.text).ToList();

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
        /// list is on the pinboard and in the journal); the item bar (GR-29) with what the siphon and the hands hold; notes
        /// come one at a time at the top, a big moment above them, clear of the station cards.
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
            BuildItemBar();
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
            // GR-31 (F31): and the boop's.
            boopBox = Box("es-prompt", "es-prompt--slap");
            boopKey = L("C", "es-keycap");
            boopBox.Add(boopKey); boopBox.Add(L("Boop", "es-prompt__name"));
            boopBox.style.display = DisplayStyle.None;
            prompts.Add(boopBox);
            // GR-20: the wish of the visitor under the reticle.
            wishBox = Box("es-prompt", "es-prompt--wish");
            wishName = L("", "es-prompt__name"); wishWho = L("", "es-prompt__status");
            wishBox.Add(wishName); wishBox.Add(wishWho);
            wishBox.style.display = DisplayStyle.None;
            prompts.Add(wishBox);
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
            // GR-33 (F62): the shop's own notes, a short log down the left side.
            noteLog = Box("gr-notelog"); noteLog.pickingMode = PickingMode.Ignore;
            hud.Add(noteLog);
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
            // GR-33 (F65): in words, so it does not read as the stars' count (reputation makes stars; discoveries make research points).
            rpLabel.text = shop.Rp > 0 ? shop.Rp + " research points" : "";
            // GR-13: one next step, the first goal still to do (the pinboard and the journal have the whole list); during the
            // guided first night (GR-14), the guide's step.
            var guide = shop.GuideNow();
            string next = guide?.Text ?? shop.Objectives().Where(g => !g.done).Select(g => g.text).FirstOrDefault() ?? shop.EraGoal;
            bool free = s.Card == GrowthCard.None;
            // A label measured while hidden, or before its font had drawn its glyphs, keeps that width: measure it again for the
            // frames after it changes or shows (setting its words anew makes it measure itself again).
            if (GrowthControls.Keyed(next) != nextLabel.text || free && !nextShown) nextRemeasure = 30;
            else if (nextRemeasure > 0) { nextRemeasure--; nextLabel.text = ""; }
            nextLabel.text = GrowthControls.Keyed(next); nextShown = free;
            hud.style.display = s.Card == GrowthCard.Pause || s.Card == GrowthCard.Settings || s.Card == GrowthCard.Welcome || s.Card == GrowthCard.Menu ? DisplayStyle.None : DisplayStyle.Flex;
            TickMenu();
            nextBox.style.display = free ? DisplayStyle.Flex : DisplayStyle.None;
            TickItemBar(free);
            bool piping = free && s.PipeFrom != null;
            modeBox.style.display = piping ? DisplayStyle.Flex : DisplayStyle.None;
            if (piping)
            {
                modeTitle.text = s.FocusName(out _);
                modeDetail.text = s.PipeTo == null ? "Aim at its other end" : s.PipePlan == null ? s.PipeWhy :
                    "to " + shop.EndName(s.PipeTo) + " · " + s.PipePlan.Length.ToString("0.#") + " m · " + s.PipePlan.Paid + " credits" + (s.PipeReady ? "" : " · " + s.PipeWhy);
                modeLay.style.display = s.PipeReady ? DisplayStyle.Flex : DisplayStyle.None;
                modeLay.Q<Label>(className: "es-keycap").text = GrowthControls.NameOf(GrowthAction.Interact);
                nextBox.style.display = DisplayStyle.None;
            }
            TickGuideMarker(guide, free && !piping);
            string name = free && !piping ? s.FocusName(out string status) : null;
            promptBox.style.display = name == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (name != null)
            {
                promptName.text = name; promptStatus.text = s.FocusName(out status) != null ? GrowthControls.Keyed(status) : "";
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
            boopBox.style.display = slapAt != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (slapAt != null) boopKey.text = GrowthControls.NameOf(GrowthAction.Boop);
            if (slapAt != null)
            {
                slapKey.text = GrowthControls.NameOf(GrowthAction.Slap);
                slapWho.text = "the " + GrowthShop.TypeName(shop.Visitors.FirstOrDefault(v => v.Id == slapAt.VisitorId)?.Type).ToLowerInvariant();
            }
            // GR-20: the wish of the visitor under the reticle, while they browse or queue.
            var lookedAt = free && !piping && s.Gags != null && s.Gags.LookedAt != null ? shop.Visitors.FirstOrDefault(v => v.Id == s.Gags.LookedAt.VisitorId) : null;
            bool wishing = lookedAt?.Wish != null && lookedAt.State != GrowthVisitorState.Leaving;
            wishBox.style.display = wishing ? DisplayStyle.Flex : DisplayStyle.None;
            if (wishing)
            {
                wishName.text = "Wishes for " + lookedAt.Wish.Text;
                wishWho.text = GrowthShop.TypeName(lookedAt.Type) + (shop.WouldGrant(lookedAt) ? " · has found it: a tip of " + GrowthWishes.Tip(shop.ReservedPrice(lookedAt)) : "");
            }
            if (Time.unscaledTime > hintUntil) hintLabel.text = "";
            if (Time.unscaledTime > eventUntil) eventBox.style.display = DisplayStyle.None;
            if (Time.unscaledTime > toastUntil) toastBox.style.display = DisplayStyle.None;
            TickNoteLog();
            refreshClock += dt;
            // GR-19: never while the mouse is held, so a click begun on the card is not lost to a rebuild before its release.
            if (refreshClock >= 0.1f) { refreshClock = 0; if (card != null && !MouseHeld() && Signature() != cardSignature) RefreshCard(); }
            if (liveLabel != null) { liveLabel.text = LiveText(); liveLabel.tooltip = liveLabel.text; }
            TickLiveValues();
            TickRings();
            ApplyPendingScroll();
            KeepScroll();
            UpdateTip();
        }

        static string ShopClock(float seconds) { int t = Mathf.Max(0, Mathf.CeilToInt(seconds)); return t / 60 + ":" + (t % 60).ToString("00"); }

        public void Hint(string text) { hintLabel.text = GrowthControls.Keyed(text); hintUntil = Time.unscaledTime + 1.5f; }

        /// <summary>GR-18 wave 4 (§6.7): a silly moment's caption (who says it, and what) until it ends (every caption stays in <see cref="Log"/>).</summary>
        public void Caption(string who, string words)
        {
            Log.Add((who != null ? who + ": " : "") + words);
            // GR-23: with Captions off nothing shows (every caption still goes to the log).
            if (!Ready || s.Settings.captionsOff) return;
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
        /// <summary>Validation (GR-20): the wish line under the reticle ("Wishes for a Pondlet · Casual visitor"), or null.</summary>
        public string WishShown => Ready && wishBox.style.display == DisplayStyle.Flex ? wishName.text + " · " + wishWho.text : null;
        public string SlapPromptKey => Ready ? slapKey.text : null;

        VisualElement noteLog;
        readonly List<(VisualElement row, Label label, float born)> noteRows = new List<(VisualElement, Label, float)>();
        /// <summary>F62: how many notes the side log keeps, how long one stays, and how long it takes to fade.</summary>
        public const int NoteLogLength = 5;
        const float NoteSeconds = 6f, NoteFade = 1.2f;

        /// <summary>
        /// GR-33 (F62): a note of the shop at work (a sale, a helper, a visitor) at the foot of a short log down the left side: the
        /// last five, each fading after a while. They no longer replace each other at the top, which flickered when Pip sold fast.
        /// </summary>
        public void LogNote(string text, bool warn = false)
        {
            Log.Add(text);
            if (!Ready) return;
            var row = Box("gr-notelog__row");
            if (warn) row.AddToClassList("gr-notelog__row--warn");
            var label = L(text, "gr-notelog__text");
            row.Add(label);
            noteLog.Add(row);
            noteRows.Add((row, label, Time.unscaledTime));
            while (noteRows.Count > NoteLogLength) { noteRows[0].row.RemoveFromHierarchy(); noteRows.RemoveAt(0); }
        }

        void TickNoteLog()
        {
            for (int i = noteRows.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - noteRows[i].born;
                if (age >= NoteSeconds + NoteFade) { noteRows[i].row.RemoveFromHierarchy(); noteRows.RemoveAt(i); continue; }
                noteRows[i].row.style.opacity = age <= NoteSeconds ? 1 : 1 - (age - NoteSeconds) / NoteFade;
            }
        }

        /// <summary>A note: one at a time at the top, the newest replacing the last (every note stays in <see cref="Log"/>). GR-33:
        /// the player's own refusals and messages; the shop's notes go to the side log (<see cref="LogNote"/>).</summary>
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
                // GR-20: the sale's own note says the wish was granted; this one is for the log.
                case GrowthEventKind.WishGranted: Log.Add(e.Text); return;
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
                default: LogNote(e.Text, e.Kind == GrowthEventKind.Refusal || e.Kind == GrowthEventKind.Impatient || e.Kind == GrowthEventKind.StaffHome || e.Kind == GrowthEventKind.ExhibitionFailed || e.Kind == GrowthEventKind.Slapped && e.Text.Contains("huff")); return;
            }
        }

        // ---------------------------------------------------------------- cards

        public void ShowCard(GrowthCard which, string target)
        {
            if (!Ready) return;
            // A card's chosen tab, its open Move list and a Sell waiting for its yes last while it stays open (refreshes rebuild it).
            string key = which + "|" + target;
            if (key != tabCard) { tabCard = key; tabChosen = null; moveOpen = false; recipesOpen = false; sellAsked = false; closeAsked = false; pageShown = 0; guidePage = 0; menuAskNew = false; }
            heldCard = null; shownCard = null; keepScroll = null; keepIn = null;
            cardLayer.Clear(); screenLayer.Clear(); card = null; liveLabel = null; statusLabel = null; liveValues.Clear();
            tabList.Clear(); tabRow = null; footer = null; rings.Clear(); notebook = false;
            tip.style.display = DisplayStyle.None; tipAnchor = null;
            if (which == GrowthCard.None) return;
            CardBuilds++;
            // GR-27: the main menu is no card: a panel down the left over the live shop, with nothing dimmed.
            if (which == GrowthCard.Menu) { card = MenuScreen(); screenLayer.Add(card); cardSignature = Signature(); return; }
            // GR-13: the planning views (the pinboard, the catalog, the journal, the receipt, the results) and the welcome and
            // pause screens are notebooks over the dimmed shop; which of them pause the game is the session's, unchanged.
            bool screen = which == GrowthCard.Journal || which == GrowthCard.Receipt || which == GrowthCard.Welcome || which == GrowthCard.Pause || which == GrowthCard.Settings || which == GrowthCard.Results || which == GrowthCard.Board || which == GrowthCard.Catalog || which == GrowthCard.Lesson || which == GrowthCard.Research || which == GrowthCard.Stock;
            card = screen ? ScreenCard(which) : which == GrowthCard.Incubator ? StationCard(IncubatorWidth) : StationCard(which == GrowthCard.Tank || which == GrowthCard.Crate || which == GrowthCard.Catalog || which == GrowthCard.Breeding || which == GrowthCard.Helper ? 580 : which == GrowthCard.Place || which == GrowthCard.Board || which == GrowthCard.Workbench || which == GrowthCard.Counter || which == GrowthCard.Grower || which == GrowthCard.Buffer || which == GrowthCard.Staff ? 540 : which == GrowthCard.PowerBoard || which == GrowthCard.Sorter ? 600 : 470);
            (screen ? screenLayer : cardLayer).Add(screen ? Dim(card) : card);
            cardSignature = Signature();
            Fill(which, target);
            shownPage = pageShown; shownTab = tabChosen;
        }

        /// <summary>
        /// Rebuilds the open card as it is now (its tab, its page, its open lists). GR-20 run 2: it keeps the card where it was
        /// scrolled to. The open card refreshes whenever the shop changes (at night as the growers fill, and after each of its
        /// buttons), and each refresh sent a scrolled card back to its top, moving its buttons from under the pointer; a new
        /// tab or page still starts at the top, and a scroll the card asks for itself (ScrollTo) wins.
        /// </summary>
        public void RefreshCard()
        {
            if (card == null) return;
            var scroll = card.Q<ScrollView>();
            var offset = scroll != null ? scroll.scrollOffset : Vector2.zero;
            bool same = s.Card + "|" + s.CardTarget == tabCard && pageShown == shownPage && tabChosen == shownTab;
            // GR-33 (F61): the card on show is the one held while a rebuild waits hidden, never the hidden one: a second rebuild in
            // that time kept the hidden one, so the card vanished and clicks went through it.
            var old = heldCard ?? TopOf(card);
            old.style.visibility = StyleKeyword.Null;
            ShowCard(s.Card, s.CardTarget);
            keepScroll = null; keepIn = null;
            var now = same && offset.y > 0.5f && card != null ? card.Q<ScrollView>() : null;
            if (now == null) return;
            keepScroll = offset; keepIn = now; keepFrames = 10;
            // GR-29 (F15): the rebuilt card waits, hidden, under the old one until it is back where it was scrolled to: its first
            // layout, at the top of its body, was showing for a frame or two, so every press in a scrolled card made it jump.
            shownCard = TopOf(card);
            shownCard.style.visibility = Visibility.Hidden;
            shownCard.parent.Add(old);
            heldCard = old;
            // Restored once the new body is laid out (the scroll view clamps an offset its content cannot reach yet).
            EventCallback<GeometryChangedEvent> restore = null;
            restore = e => { now.contentContainer.UnregisterCallback(restore); KeepScroll(); };
            now.contentContainer.RegisterCallback(restore);
        }

        Vector2? keepScroll;
        ScrollView keepIn;
        VisualElement heldCard, shownCard;
        int keepFrames, shownPage = -1;

        /// <summary>A card with what dims the shop behind it (a notebook), as it stands in its layer.</summary>
        VisualElement TopOf(VisualElement c) => c.parent == cardLayer || c.parent == screenLayer ? c : c.parent;

        /// <summary>GR-29: the rebuilt card takes the old one's place.</summary>
        void ShowRebuiltCard()
        {
            keepScroll = null; keepIn = null;
            heldCard?.RemoveFromHierarchy(); heldCard = null;
            if (shownCard != null) shownCard.style.visibility = StyleKeyword.Null;
            shownCard = null;
        }
        string shownTab;

        /// <summary>Puts a refreshed card back where it was scrolled to, as soon as its content reaches there (within ten frames).</summary>
        void KeepScroll()
        {
            if (keepScroll == null) return;
            // A press that landed on the old card finishes there: it goes once the button is up, or the click would be lost with it.
            if (heldCard != null && MouseHeld()) return;
            if (keepIn == null || keepIn.panel == null || keepFrames-- <= 0) { ShowRebuiltCard(); return; }
            // Laid out far enough to hold the offset, so nothing clamps it after the card shows.
            float room = keepIn.contentContainer.layout.height - keepIn.contentViewport.layout.height;
            // GR-33: a card at its top shows as soon as it is laid out, however short its content (room is then negative).
            if (float.IsNaN(room) || keepScroll.Value.y > 0.5f && room < keepScroll.Value.y - 0.5f) return;
            keepIn.scrollOffset = keepScroll.Value;
            if (Mathf.Abs(keepIn.scrollOffset.y - keepScroll.Value.y) < 0.5f) ShowRebuiltCard();
        }

        /// <summary>Validation (GR-33, F76): whether the open card's body is taller than it can show, so it scrolls.</summary>
        public bool CardScrolls
        {
            get
            {
                var scroll = card?.Q<ScrollView>();
                if (scroll == null) return false;
                float content = scroll.contentContainer.layout.height, view = scroll.contentViewport.layout.height;
                return !float.IsNaN(content) && !float.IsNaN(view) && content > view + 1;
            }
        }

        /// <summary>Validation (GR-20): the open card's body's scroll now (0 for none).</summary>
        public float CardScroll => card?.Q<ScrollView>()?.scrollOffset.y ?? 0;

        /// <summary>Validation (GR-29): the scroll of the card showing now: the old one while its rebuilt one waits.</summary>
        public float ShownCardScroll => (heldCard ?? card)?.Q<ScrollView>()?.scrollOffset.y ?? 0;

        /// <summary>Validation (GR-20): scrolls the open card's body down by an amount (as a mouse wheel does), and how far it went.</summary>
        public float ScrollCardBy(float pixels)
        {
            var scroll = card?.Q<ScrollView>();
            if (scroll == null) return 0;
            scroll.scrollOffset = new Vector2(0, scroll.scrollOffset.y + pixels);
            return scroll.scrollOffset.y;
        }

        static bool MouseHeld() => UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;

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
                if (v.ClassListContains("gr-page") && v.style.display.value == DisplayStyle.None)
                {
                    // R09.4: the Guide's pages have a pager of their own.
                    if (v.ClassListContains("gr-page--guide")) guidePage = v.parent.IndexOf(v); else pageShown = v.parent.IndexOf(v);
                    pendingScroll = startsWith; RefreshCard(); return true;
                }
            float y = label.worldBound.y - scroll.contentContainer.worldBound.y;
            keepScroll = null; keepIn = null;
            scroll.scrollOffset = new Vector2(0, Mathf.Max(0, y - 8));
            return true;
        }

        /// <summary>
        /// Every label and button text on the open card or screen, every tab's included, and every hover text (why a
        /// control can't be used, the levels on the Upgrade tab's track, the rules on the "?"), for the validation route.
        /// </summary>
        /// <summary>Validation: whether the boop's prompt shows.</summary>
        public bool BoopPromptShown => boopBox.style.display == DisplayStyle.Flex;

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
            c.style.width = wide ? 1620 : which == GrowthCard.Receipt ? 820 : which == GrowthCard.Settings ? 1180 : 1000;
            // GR-23: the Settings screen keeps one size whichever tab shows.
            if (wide || which == GrowthCard.Settings) c.style.height = 860;
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

        /// <summary>
        /// GR-33 (F61): the cards that show the shop's stock as it moves (creatures in and out of tanks, visitors in the queue)
        /// and so rebuild when it does; every other card leaves it out, or shows it live (Nori's and the sorter's counts), so
        /// helpers and visitors at work no longer rebuild a card that does not show their work.
        /// </summary>
        static bool ShowsStock(GrowthCard card) =>
            card == GrowthCard.Tank || card == GrowthCard.Place || card == GrowthCard.Counter || card == GrowthCard.Crate || card == GrowthCard.Breeding || card == GrowthCard.Board;

        /// <summary>
        /// GR-33 (F61): the cards about people rather than the lab's work: a helper's card and the staff board. They
        /// leave out what moves as the lab works (the siphon, the chamber, hatchlings, carried creatures, the breeding tanks,
        /// research, power and lines), which they do not show; their own counts show live.
        /// </summary>
        static bool AboutPeople(GrowthCard card) => card == GrowthCard.Helper || card == GrowthCard.Staff || card == GrowthCard.Stock;

        string Signature()
        {
            var shop = s.Shop;
            bool stock = ShowsStock(s.Card), lab = !AboutPeople(s.Card);
            var b = new System.Text.StringBuilder();
            // What every card may show: what can be afforded, the time of day, the shop's standing, its stations, cultures and tanks.
            b.Append(s.Card).Append('|').Append(s.CardTarget).Append('|').Append(shop.Credits).Append('|').Append(shop.Phase).Append('|').Append(shop.Rp).Append('|').Append(shop.Stars).Append('|').Append(shop.Era).Append('|').Append(shop.UnitB)
                .Append('|').Append(string.Join(",", GrowthShop.UpgradeableStations.Select(shop.Level))).Append('|').Append(string.Join(",", shop.Cultures))
                .Append('|').Append(string.Join(",", shop.Containers.Select(c => c.Kind + c.Level + c.Water + c.Place + c.Decor))).Append('|').Append(string.Join(",", shop.Vials.Select(v => v.Key + v.Value))).Append('|').Append(string.Join(",", shop.Orders))
                .Append('|').Append(shop.PairLog.Count).Append('|').Append(shop.Journal.Count).Append('|').Append(HelperSignature()).Append('|').Append(SpacesSignature()).Append('|').Append(ExhibitionSignature());
            // The shop's stock as it moves, for the cards that show it.
            if (stock) b.Append('|').Append(string.Join(",", shop.Containers.Select(c => c.Occupants.Count + ":" + c.Occupants.Count(o => o.ReservedBy != 0)))).Append('|').Append(shop.Queue.Count());
            // The lab at work, for every card but those about people.
            if (lab)
                b.Append('|').Append(shop.Held.Count).Append(string.Join(",", shop.Held)).Append('|').Append(string.Join(",", shop.Chamber)).Append('|').Append(shop.Incubating).Append('|').Append(shop.Hatchling?.Id ?? 0)
                    .Append('|').Append(shop.Hatchlings.Count).Append(shop.IncubatorTint).Append('|').Append(shop.Tint).Append('|').Append(string.Join(",", shop.Carried.Select(c => c.Id))).Append('|').Append(s.CarriedChoice)
                    .Append('|').Append(string.Join(",", shop.Parents.Select(p => p.Id))).Append('|').Append(shop.Offspring?.Id ?? 0).Append('|').Append(string.Join(",", shop.Pairs[1].Parents.Select(p => p.Id))).Append(shop.Pairs[1].Offspring?.Id ?? 0)
                    .Append('|').Append(s.Card == GrowthCard.Grower ? Mathf.FloorToInt(shop.GrowerUnits.Values.Sum()) : 0).Append('|').Append(PowerSignature()).Append('|').Append(LineSignature()).Append('|').Append(ResearchSignature()).Append('|').Append(string.Join(",", shop.MakeQueue));
            return b.ToString();
        }

        string LiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Incubator: return shop.Incubating ? "Incubating · " + Mathf.CeilToInt(shop.IncubationLeft) + " s" : shop.Hatchlings.Count > 1 ? shop.Hatchlings.Count + " hatchlings are ready" : shop.Hatchling != null ? "A hatchling is ready" : shop.Chamber.Count > 0 ? "Ready to mix" : "Idle";
                case GrowthCard.Counter: return shop.Phase == GrowthPhase.Day ? "Open: " + GrowthShop.DurationWords(shop.DaySecondsLeft) + " left." : "Closed for the night.";
                case GrowthCard.Grower: return s.CardTarget == null ? "" : Mathf.FloorToInt(shop.GrowerUnits[s.CardTarget]) + "/" + shop.GrowerCapacityOf(s.CardTarget) + " doses ready";
                case GrowthCard.Settings: return SettingsLiveText();
                case GrowthCard.Research: return ResearchLiveText();
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
                case GrowthCard.Settings: FillSettings(); break;
                case GrowthCard.Annex: FillAnnex(); break;
                case GrowthCard.Generator: FillGenerator(); break;
                case GrowthCard.PowerBoard: FillPowerBoard(); break;
                case GrowthCard.Buffer: FillBuffer(target); break;
                case GrowthCard.Workshop: FillWorkshop(); break;
                case GrowthCard.ResearchWall: FillResearchWall(); break;
                case GrowthCard.Research: FillResearch(target); break;
                case GrowthCard.Stock: FillStock(target); break;
                case GrowthCard.Sorter: FillSorter(); break;
                case GrowthCard.Staff: FillStaff(); break;
                case GrowthCard.UnitC: FillUnitC(); break;
                case GrowthCard.Hall: FillHall(); break;
                case GrowthCard.Helper: FillHelper(target); break;
                case GrowthCard.Results: FillResults(); break;
                case GrowthCard.Lesson: FillLesson(target); break;
            }
        }

        void FillGrower(string culture)
        {
            var shop = s.Shop;
            bool vat = GrowthContent.Culture(culture).Source == CultureSource.Vat;
            var body = Head(vat ? "Conditioning vat (" + GrowthShop.Name(culture) + ")" : GrowthShop.Name(culture) + " grower", "", vat ? "Vat" : "Grower",
                GrowthShop.Name(culture) + " is a culture: a gene your creatures are made from. Hold the left mouse on it to fill your siphon. It refills by itself, one dose at a time. One upgrade covers every grower" + (vat ? "; the vat has levels of its own." : ".") +
                (shop.Taps < 1 ? " Growers' level 3 gives each grower a tap for a pipe to a buffer in the power room." : ""));
            LiveStatus();
            // How full it is, and its numbers as chips (GR-13: numbers, not sentences).
            float units = shop.GrowerUnits[culture];
            int capacity = shop.GrowerCapacityOf(culture);
            var fill = Box("gr-section");
            fill.Add(L(GrowthShop.Name(culture) + " is a culture: a gene your creatures are made from.", "gr-hint"));
            fill.Add(Bar(units / Mathf.Max(1, capacity), CultureColour(culture)));
            var chips = Box("gr-chips");
            chips.Add(Chip("It holds " + capacity));
            chips.Add(Chip("Refills a dose every " + shop.RefillSecondsOf(culture).ToString("0.#") + " s"));
            chips.Add(Chip("A hold collects " + (shop.PerHold > 1 ? shop.PerHold + " doses" : "a dose") + " every " + shop.HoldSeconds.ToString("0.0") + " s"));
            fill.Add(chips);
            body.Add(fill);
            var siphon = Box("gr-row", "gr-row--spread");
            siphon.Add(L("Your siphon: " + shop.Held.Count + " of " + shop.SiphonCapacity + " doses", "gr-hint"));
            siphon.Add(Link("Pour out your siphon", () => s.Act(() => shop.PourOutSiphon()), shop.Held.Count > 0, "Your siphon is empty"));
            body.Add(siphon);
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
            var body = Head("Incubator", "", "Mix", "Click the incubator to put your siphon's doses in. Here, + adds a dose, − takes it back, and × pours your siphon's doses of it away. Each dose is a gene, and the genes you mix decide the creature: every mix makes one, and an exact recipe finds a new one. A recipe you have found doses itself in one press.");
            LiveStatus();
            bool locked = shop.Incubating || shop.ChamberLock != null;
            // GR-33 (F76): two columns so the card never scrolls: the chamber and Start on the left; on the right, the hatchling,
            // the quick recipes and the mixes tried lately.
            var columns = Box("gr-cols");
            var left = Box("gr-col"); var right = Box("gr-col");
            columns.Add(left); columns.Add(right);
            body.Add(columns);
            var main = body;
            body = right;
            if (shop.Hatchling != null)
            {
                string name = GrowthSession.Named(shop.Hatchling);
                int clutch = shop.Hatchlings.Count;
                var call = Box("gr-callout");
                var row = Box("gr-row");
                row.Add(CreatureIcon(shop.Hatchling.Def, shop.Hatchling.Colour, 56, "gr-result__glyph"));
                row.Add(L(clutch > 1 ? clutch + " " + GrowthShop.Plural(name) : "A " + name, "gr-strong"));
                if (shop.LastMix != null && shop.LastMix.WasNew) row.Add(L("New!", "gr-newtag"));
                call.Add(row);
                call.Add(Wide("Take " + (clutch > 1 ? "a " : "the ") + name + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(shop.TakeHatchling), true,
                    shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full: put a creature in a tank first"));
                body.Add(call);
            }
            // The recipes found: one press doses each from the siphon (the doses already in go back first).
            var recipes = shop.QuickRecipes();
            var close = shop.MixLog.AsEnumerable().Reverse().FirstOrDefault(m => m.DistanceToNew == 1 && !m.Exact);
            var found = Box("gr-section");
            found.Add(Eyebrow("Quick recipes"));
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
            // GR-31 (F26): which three stay at hand is the player's: every recipe found, each with its pin.
            var everyRecipe = shop.FoundRecipes();
            if (everyRecipe.Count > 0)
            {
                var choose = Link("Choose your " + GrowthShop.QuickRecipeSlots + " (" + everyRecipe.Count + " found)", () => tabChosen = "Recipes");
                choose.style.marginLeft = 0; choose.style.alignSelf = Align.FlexStart;
                found.Add(choose);
            }
            body.Add(found);
            // GR-35 (F73): the queue: a found recipe made again and again, dosed from the buffers and growers.
            if (shop.HasMakeQueue && everyRecipe.Count > 0) body.Add(QueueSection(recipes));
            // The chamber: a chip for each culture in the siphon or the chamber, − and + on either side.
            body = left;
            var chamber = Box("gr-section");
            chamber.Add(Eyebrow("In the chamber · " + shop.Chamber.Count + " of " + shop.MaxDoses));
            chamber.Add(L("Each dose is a gene. The genes you mix decide the creature.", "gr-hint"));
            var doses = Box("gr-row");
            foreach (var culture in GrowthContent.Cultures.Select(c => c.Id).Where(c => shop.Held.Contains(c) || shop.Chamber.Contains(c)))
            {
                string c = culture;
                int inChamber = shop.Chamber.Count(x => x == c), held = shop.Held.Count(x => x == c);
                var chip = Box("gr-dose"); chip.style.backgroundColor = CultureGround(c);
                chip.Add(PlusMinus("−", () => s.Act(() => shop.ReturnDose(c)), !locked && inChamber > 0 && shop.Held.Count < shop.SiphonCapacity,
                    locked ? "The incubator is busy" : inChamber == 0 ? "None of it in the chamber" : "Your siphon is full: pour something out first"));
                var words = Box("gr-dose__text");
                words.Add(L(GrowthShop.Name(c) + " " + inChamber, "gr-dose__name"));
                words.Add(L(held + " in siphon", "gr-dose__count"));
                chip.Add(words);
                chip.Add(PlusMinus("+", () => s.Act(() => shop.AddDose(c)), !locked && held > 0 && shop.Chamber.Count < shop.MaxDoses,
                    locked ? "The incubator is busy" : held == 0 ? "Collect " + GrowthShop.Name(c) + " at its grower" : "The chamber holds " + shop.MaxDoses));
                var pour = PlusMinus("×", () => s.Act(() => shop.PourOutSiphon(c)), held > 0, "None of it in your siphon");
                if (held > 0) pour.tooltip = "Pour out the " + held + " " + GrowthShop.Name(c) + " in your siphon";
                chip.Add(pour);
                doses.Add(chip);
            }
            if (shop.Held.Count == 0 && shop.Chamber.Count == 0) doses.Add(L("Your siphon is empty: hold the mouse on a grower to collect.", "gr-hint"));
            chamber.Add(doses);
            body.Add(chamber);
            if (shop.Chamber.Count > 0 && !shop.Incubating)
            {
                // GR-30 (F7): a repeat says so, and what it makes.
                var repeat = shop.MixEntry(shop.ChamberRecipe());
                if (repeat != null) body.Add(L("Already tried: this makes " + GrowthContent.Get(repeat.Species).Name + ".", "gr-strong"));
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
            // GR-29 (F11): what holds Start back, in words, not only on hover: any mix can start, so it is never the mix.
            if (!locked && shop.Hatchling != null)
                body.Add(L(shop.Carried.Count < GrowthShop.CarryLimit ? "Start waits until you take the hatchling." : "Start waits for the hatchling, and your hands are full: put a creature in a tank first.", "gr-hint"));
            var links = Box("gr-row", "gr-row--spread");
            links.style.flexWrap = Wrap.Wrap;
            bool roomInSiphon = shop.Held.Count < shop.SiphonCapacity;
            var back = Link("Return doses", () => { foreach (var c in shop.Chamber.ToList()) shop.ReturnDose(c); s.SaveNow(); }, !locked && shop.Chamber.Count > 0 && roomInSiphon, shop.Chamber.Count == 0 ? "Nothing in the chamber" : "Your siphon is full: pour something out first");
            back.style.marginLeft = 0;
            links.Add(back);
            // GR-29 (F10): the way out when the doses cannot go back.
            links.Add(Link("Pour out the chamber", () => s.Act(shop.PourOutChamber), !locked && shop.Chamber.Count > 0, "Nothing in the chamber"));
            links.Add(Link("Mix log · " + shop.MixLog.Count + " tried", () => OpenJournalAt("Mix log")));
            body.Add(links);
            // GR-30 (F6): the mixes tried lately, in plain sight.
            if (shop.MixLog.Count > 0)
            {
                body = right;
                var lately = Box("gr-section");
                lately.style.marginTop = 12;
                lately.Add(Eyebrow("Tried lately"));
                foreach (var m in shop.MixLog.AsEnumerable().Reverse().Take(3))
                {
                    var line = Box("gr-row");
                    line.style.flexWrap = Wrap.NoWrap; line.style.alignItems = Align.FlexStart; line.style.marginBottom = 4;
                    line.Add(DoseDots(m.Doses));
                    var words = L(DoseWords(m.Doses) + " makes " + GrowthContent.Get(m.Species).Name, "gr-hint");
                    words.style.flexShrink = 1; words.style.whiteSpace = WhiteSpace.Normal;
                    line.Add(words);
                    lately.Add(line);
                }
                body.Add(lately);
            }
            // GR-06: the tint port (level 3) colours the next clutch, as the breeding tank's water does.
            body = left;
            if (shop.IncubatorTintPort) body.Add(TintRow(shop.IncubatorTint, "hatchlings", c => shop.IncubatorTintCheck(c, out string w) == GrowthFailure.None ? null : w,
                c => s.Act(() => shop.TintIncubator(c)), () => s.Act(shop.ClearIncubatorTint), "Tinted with " + (shop.IncubatorTint == null ? "" : GrowthShop.Name(shop.IncubatorTint)) + ": what hatches next is " + (shop.IncubatorTint == null ? "" : GrowthRules.BreedColour(Colour.Classic, Colour.Classic, shop.IncubatorTint).ToString()) + "."));
            body = main;
            if (everyRecipe.Count > 0)
            {
                var all = AddTab("Recipes");
                all.Add(L("Pin " + GrowthShop.QuickRecipeSlots + " to keep them at hand on the Mix tab." + (shop.HasMakeQueue ? " Make adds a batch to the incubator's queue." : ""), "gr-hint"));
                foreach (var id in everyRecipe)
                {
                    var def = GrowthContent.Get(id);
                    var row = Box("gr-row", "gr-row--spread");
                    var what = Box("gr-row");
                    what.Add(CreatureIcon(def, Colour.Classic, 32, "gr-rs-icon"));
                    what.Add(DoseDots(def.Recipe));
                    what.Add(L(def.Name, "gr-hint"));
                    row.Add(what);
                    bool pinned = shop.PinnedRecipes.Contains(id);
                    var buttons = Box("gr-row");
                    if (shop.HasMakeQueue) buttons.Add(MakeButton(id));
                    buttons.Add(ChipButton(pinned ? "Pinned" : "Pin", () => s.Act(() => shop.PinRecipe(id)), pinned || shop.PinnedRecipes.Count < GrowthShop.QuickRecipeSlots, pinned ? "Take its pin off" : "Keep it at hand"));
                    row.Add(buttons);
                    all.Add(row);
                }
            }
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

        /// <summary>
        /// GR-35 (F73): the incubator's queue: each recipe queued, in turn, with its batches and − to take one off; what it is
        /// doing (live); and Make for the recipes at hand. Its batches come from the buffers and growers, never the siphon.
        /// </summary>
        VisualElement QueueSection(List<string> atHand)
        {
            var shop = s.Shop;
            var box = Box("gr-section");
            var head = Box("gr-row", "gr-row--spread");
            head.Add(Eyebrow("Queue · " + shop.MakeQueue.Count + " of " + GrowthShop.MakeQueueLimit));
            if (shop.MakeQueue.Count > 0) head.Add(Link("Clear", () => s.Act(shop.ClearMakeQueue)));
            box.Add(head);
            if (shop.MakeQueue.Count == 0)
                box.Add(L("Make a recipe and the incubator makes it by itself, batch after batch, from the buffers and growers.", "gr-hint"));
            else
            {
                var row = Box("gr-row");
                row.style.flexWrap = Wrap.Wrap;
                for (int i = 0; i < shop.MakeQueue.Count;)
                {
                    int j = i;
                    while (j < shop.MakeQueue.Count && shop.MakeQueue[j] == shop.MakeQueue[i]) j++;
                    string id = shop.MakeQueue[i];
                    var def = GrowthContent.Get(id);
                    var chip = Box("gr-qchip");
                    chip.Add(CreatureIcon(def, Colour.Classic, 28, "gr-rs-icon"));
                    chip.Add(L(def.Name + " ×" + (j - i), "gr-qchip__text"));
                    var minus = PlusMinus("−", () => s.Act(() => shop.UnqueueMake(id)), true, null);
                    minus.tooltip = "Take a " + def.Name + " off the queue";
                    chip.Add(minus);
                    row.Add(chip);
                    i = j;
                }
                box.Add(row);
                box.Add(LiveL(() => shop.MakeStatus, "gr-hint"));
            }
            var make = Box("gr-row");
            make.style.flexWrap = Wrap.Wrap;
            make.Add(L("Make:", "gr-hint"));
            foreach (var id in atHand) make.Add(MakeButton(id, GrowthContent.Get(id).Name + " +1"));
            box.Add(make);
            return box;
        }

        /// <summary>GR-35 (F73): Make: one batch of a found recipe into the incubator's queue, saying why when it can't.</summary>
        Button MakeButton(string species, string text = "Make")
        {
            var shop = s.Shop;
            bool can = shop.MakeCheck(species, out string why) == GrowthFailure.None;
            var b = ChipButton(text, () => s.Act(() => shop.QueueMake(species)), can, can ? "Add a " + GrowthContent.Get(species).Name + " to the incubator's queue" : why);
            b.name = "gr-make-" + species;
            return b;
        }

        /// <summary>GR-33 (F76): the incubator's card is wide enough for two columns, so it never scrolls.</summary>
        const int IncubatorWidth = 1100;

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
            if (can) RecipeTip(tile, "Dose " + def.Name + ": " + GrowthReport.Recipe(def.Recipe), def.Recipe);
            else tile.tooltip = locked ? "The incubator is busy" : shop.Hatchling != null ? "Take the hatchling first" : missing;
            tile.Add(CreatureIcon(def, Colour.Classic, 48, "gr-tile__glyph"));
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

        bool moveOpen, recipesOpen, sellAsked;

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
                cell.Add(CreatureIcon(o.Def, o.Colour, 46 + 12 * (Mathf.Min(spaces, 3) - 1), "gr-space__glyph"));
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
                (comfort ? " Creatures that share a temperament and the tank's water are in harmony: +1 appeal." : "") + " Click a tank to put in the creature you chose, or with empty hands to take the one you aim at; right-click takes it with others in hand. Look closely at the glass to peek." +
                (c.Def.Lab ? "" : " A piece of decor on its top (the Decor tab) makes it more appealing, and visitors pay more for the creatures its theme suits.");
            var body = Head(c.Def.Name, c.Used + " of " + c.Spaces + " spaces", c.Def.Lab ? "Rack" : "Tank", rules, !c.Def.Lab);
            body.Add(SpaceGrid(c));
            // Each species with its count and price; a press takes one out.
            var kinds = Box("gr-row", "gr-section");
            bool handsFull = shop.Carried.Count >= GrowthShop.CarryLimit;
            // GR-29 (F12): with full hands a press swaps: the creature chosen in hand, or else another that fits in its place.
            var swappers = shop.Carried.OrderByDescending(x => x == s.ChosenCarried).ToList();
            if (handsFull && c.Occupants.Count > 0) body.Add(L("Your hands are full: press a creature to swap it for one you carry.", "gr-hint"));
            foreach (var g in c.Occupants.GroupBy(o => (o.Species, o.Colour)))
            {
                var free = g.FirstOrDefault(o => o.ReservedBy == 0 && !GrowthShop.IsOnDisplay(o));
                int sold = g.Count(o => o.ReservedBy != 0);
                var def = g.First().Def;
                bool can = free != null && !handsFull;
                var swap = free != null && handsFull ? swappers.FirstOrDefault(x => shop.SwapCheck(c.Id, free.Id, x.Id, out _) == GrowthFailure.None) : null;
                var b = ChipButton(GrowthSession.Named(g.First()) + (g.Count() > 1 ? " ×" + g.Count() : "") + (sold > 0 ? " (" + sold + " sold)" : "") + " · " + shop.Price(def.Id) + " cr",
                    () => s.Act(() => swap != null ? shop.Swap(c.Id, free.Id, swap.Id) : shop.TakeOut(c.Id, free.Id)), can || swap != null,
                    can ? "Take one out" : swap != null ? "Swap it for your " + GrowthSession.Named(swap) : free == null ? "None free to take" : "Your hands are full, and nothing you carry fits in its place", GrowthCreatureView.ColourOf(def, g.Key.Colour));
                kinds.Add(b);
            }
            if (c.Occupants.Count == 0) kinds.Add(L("Empty: carry creatures here and click the tank.", "gr-hint"));
            body.Add(kinds);
            // GR-31 (F37): what Nori and the sorter may put here.
            if (!c.Def.Lab && (shop.Helper("nori") != null || shop.HasSorter))
            {
                var rule = Box("gr-section");
                rule.Add(L("Nori and the sorter keep here", "gr-label"));
                var chips = Box("gr-chips");
                foreach (var r in GrowthShop.TankRules)
                {
                    string choice = r;
                    var b = ChipButton(r == "" ? "Anything" : char.ToUpperInvariant(r[0]) + r.Substring(1), () => s.Act(() => shop.SetTankRule(c.Id, choice)), true);
                    b.name = "gr-rule-" + (r == "" ? "any" : r.Replace(' ', '-'));
                    if (r == c.Rule) b.AddToClassList("gr-chip--on");
                    chips.Add(b);
                }
                rule.Add(chips);
                body.Add(rule);
            }
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
                    feel.Add(Chip("Appeal +" + appeal, false, string.Join(" and ", new[] { c.Kind == "show" ? "a lit Show tank" : null, harmony ? "harmony" : null, c.Decor != null ? "its decor" : null }.Where(x => x != null)) +
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
                string moveWhy = places.Count == 0 ? "No free place it fits" : null;
                var move = Button(moveOpen ? "Move: pick a place" : "Move", () => moveOpen = !moveOpen, false, moveWhy == null, moveWhy);
                arrange.Add(move);
                if (moveOpen && moveWhy == null)
                    foreach (var p in places) { var place = p; arrange.Add(ChipButton("To " + place, () => { if (s.Act(() => shop.MoveContainer(c.Id, place)).Success) s.SetCard(GrowthCard.None); }, true, "Move it with its creatures to " + place)); }
            }
            var sell = shop.SellCheck(c.Id, out string sellWhy);
            if (sell == GrowthFailure.None && sellAsked)
            {
                arrange.Add(L("Sell it for " + GrowthShop.SellValue(c) + " cr?" + (c.Decor != null ? " Its decor goes back to the box." : ""), "gr-hint"));
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
            // GR-19: decor on the tank's top, in a tab of its own (shop tanks only).
            if (!c.Def.Lab) DecorTab(c);
            var upgrade = AddTab(UpgradeTab);
            var check = shop.ContainerUpgradeCheck(c.Id, out var next, out string upgradeWhy);
            upgrade.Add(UpgradeBlock(c.Def.Name + " · level " + c.Level + " of " + c.Def.Levels.Length, c.Def.Levels, c.Level, next, check, upgradeWhy, "Upgrade", () => s.Act(() => shop.UpgradeContainer(c.Id))));
            Footer(body, "click puts in or takes · right-click takes");
        }

        /// <summary>GR-19: a theme's colour: Riverbank green, Hot springs coral, Seaside blue.</summary>
        static Color DecorColour(Water w) => w == Water.Fresh ? new Color(0.34f, 0.73f, 0.28f) : w == Water.Warm ? new Color(1f, 0.48f, 0.3f) : new Color(0.25f, 0.66f, 1f);

        /// <summary>GR-19: a tank's Decor tab: the piece on its top (what its theme suits, and Take it off at night), and every
        /// other piece the shop owns, in the box or on another tank, with Put it on (from the box, or moved here).</summary>
        void DecorTab(GrowthContainer c)
        {
            var shop = s.Shop;
            var tab = AddTab("Decor");
            var on = GrowthShop.DecorOn(c);
            if (on != null)
            {
                int suited = c.Occupants.Count(o => GrowthShop.Themed(c, o.Species));
                var take = shop.TakeDecorCheck(c.Id, out string takeWhy);
                var off = Button("Take it off", () => s.Act(() => shop.TakeDecor(c.Id)), false, take == GrowthFailure.None, take == GrowthFailure.None ? "Back to the decor box" : takeWhy);
                tab.Add(Item(on.Name, on.Theme + ": " + GrowthContent.ThemeSuits(on.Water) + " sell for more here", off));
                var chips = Box("gr-chips", "gr-section");
                chips.Add(Chip("Appeal +1", false, "Visitors look here first and stretch their budget 10%"));
                chips.Add(Chip(suited + (suited == 1 ? " creature themed" : " creatures themed"), suited == 0, "Visitors stretch their budget 25% more for each (with a school, up to 40%)"));
                tab.Add(chips);
            }
            else tab.Add(L("No decor on it. A piece on its top makes it more appealing, and visitors pay more for the creatures its theme suits.", "gr-hint"));
            var others = shop.DecorOwned.Where(d => d != c.Decor).ToList();
            if (others.Count > 0)
            {
                tab.Add(Eyebrow(on == null ? "Your decor" : "Or put on instead"));
                foreach (var id in others)
                {
                    var d = GrowthContent.DecorPiece(id);
                    var holder = shop.DecorHolder(id);
                    int fit = c.Occupants.Count(o => o.Def.Water == d.Water);
                    var failure = shop.DecorCheck(id, c.Id, out string why);
                    string pieceId = id;
                    var put = Button("Put it on", () => s.Act(() => shop.PutDecor(pieceId, c.Id)), true, failure == GrowthFailure.None,
                        failure == GrowthFailure.None ? (holder != null ? "Moves it here from the tank at " + holder.Place : "From the decor box") : why);
                    put.name = "gr-decor-put-" + id;
                    var row = Item(d.Name, d.Theme + " · " + (holder != null ? "on the tank at " + holder.Place : "in the box") + " · suits " + fit + (fit == 1 ? " creature here" : " creatures here"), put);
                    tab.Add(row);
                }
            }
            else if (on == null) tab.Add(L("Decor comes from the supplier catalog by the counter: Riverbank and Hot springs on its 2-star page, Seaside on its 4-star page.", "gr-hint"));
        }

        /// <summary>GR-13's empty place: each container that could stand here as a row with its spaces, size and price, and moving a tank here.</summary>
        void FillPlace(string place)
        {
            var shop = s.Shop;
            if (GrowthShop.LabPlace(place) != null) { FillRackPlace(place); return; }
            var spot = GrowthShop.Place(place);
            if (spot == null) { s.SetCard(GrowthCard.None); return; }
            var body = Head("Empty place", (spot.Unit == "unit-b" ? "shop unit B" : "shop unit A") + " · " + spot.Size, null,
                "A " + spot.Size + " place: room for up to " + spot.Width.ToString("0.#") + " × " + spot.Depth.ToString("0.#") + " m. Tanks are bought, moved and sold at any time.", true);
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
            body.Add(Text("For looking at sizes and families before cultures and breeding arrive: press a species to take one in hand (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried), then click a tank to put it in. It is not a discovery.", "es-text--muted"));
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

        /// <summary>GR-31 (F32): opening by itself once enough creatures are on show.</summary>
        void AutoOpenRow(VisualElement body)
        {
            var shop = s.Shop;
            var auto = Box("gr-section");
            // GR-33 (F83): the choice first, then what it does, in plain words.
            auto.Add(L("Open by itself, with this many on show", "gr-label"));
            var chips = Box("gr-chips");
            foreach (int at in GrowthShop.AutoOpenChoices)
            {
                int value = at;
                var b = ChipButton(value == 0 ? "Off" : value.ToString(), () => { shop.AutoOpenAt = value; s.SaveNow(); RefreshCard(); }, true);
                b.name = "gr-autoopen-" + value;
                if (value == shop.AutoOpenAt) b.AddToClassList("gr-chip--on");
                chips.Add(b);
            }
            auto.Add(chips);
            auto.Add(L(shop.AutoOpenAt == 0 ? "Off: you open the shop yourself, here." :
                "Once " + shop.AutoOpenAt + " or more creatures are on show, the night ends and the shop opens by itself. Never in the night's first " + GrowthShop.AutoOpenWait.ToString("0") + " seconds, so you have time to look around.", "gr-hint"));
            body.Add(auto);
        }

        /// <summary>GR-33 (F82): how many visitors can be in the shop at once, and why: each shop unit adds room for more.</summary>
        Label VisitorsChip()
        {
            var shop = s.Shop;
            var chip = Chip("Up to " + shop.VisitorsAtOnce + " visitors at once");
            chip.tooltip = "Your first shop unit has room for " + GrowthContent.Expansions.First(e => e.Id == "unit-a").VisitorsAtOnce + " visitors at a time; each shop unit you open, and the exhibition hall, makes room for one more.";
            return chip;
        }

        void FillCounter()
        {
            var shop = s.Shop;
            var body = Head("Counter", "", "Counter", "Open the shop here at night: a day lasts " + GrowthShop.DayLengthWords + ", and visitors pick one creature each and wait here to pay. Hold the left mouse on the counter to bag each sale; others wait by the tanks. Nobody comes into an empty shop. You can close it here before its time too.");
            LiveStatus();
            if (shop.Phase == GrowthPhase.Night)
            {
                closeAsked = false;
                int stock = shop.Stock.Count();
                body.Add(Stat(stock.ToString(), stock == 1 ? "creature on show" : "creatures on show"));
                var room = Box("gr-chips"); room.Add(VisitorsChip()); body.Add(room);
                if (stock == 0) body.Add(L("Your tanks are empty: nobody comes into an empty shop.", "gr-hint"));
                var open = Wide("Open the shop", () => { if (s.Act(shop.OpenShop).Success) s.SetCard(GrowthCard.None); }, true, true);
                open.style.marginTop = 14;
                AutoOpenRow(body);
                body.Add(open);
            }
            else
            {
                bool pip = shop.Working("pip");
                var chips = Box("gr-chips", "gr-section");
                chips.Add(Chip(pip && !shop.SecondTill ? "Pip bags each order in " + GrowthShop.PipServeSeconds[shop.Helper("pip").Level - 1].ToString("0.0") + " s" : "A sale takes " + shop.ServeSeconds.ToString("0.0") + " s" + (pip ? ", beside Pip" : "")));
                chips.Add(Chip("The queue holds " + shop.QueueLength));
                chips.Add(VisitorsChip());
                body.Add(chips);
                if (pip && !shop.SecondTill) body.Add(L("The counter's second till (level 4) lets you serve beside Pip.", "gr-hint"));
                body.Add(Eyebrow("Waiting · " + shop.Queue.Count()));
                foreach (var v in shop.Queue.OrderByDescending(x => x.Clock).ThenBy(x => x.Id))
                {
                    var patience = Bar(v.Patience / (v.Type == GrowthShop.GuestType ? GrowthShop.GuestPatience : GrowthShop.QueuePatience), new Color(0.98f, 0.78f, 0.37f));
                    patience.style.width = 120; patience.tooltip = "Patience: " + Mathf.CeilToInt(v.Patience) + " s";
                    // GR-20: their wish, granted by this sale (with its tip) or not.
                    string wish = v.Wish == null ? "" : shop.WouldGrant(v) ? " · their wish: a tip of " + GrowthWishes.Tip(shop.ReservedPrice(v)) : " · wished for " + v.Wish.Text;
                    body.Add(Item(Visitor(v.Type), "wants " + shop.ReservedText(v) + " · " + shop.ReservedPrice(v) + " cr" + wish, patience));
                }
                if (!shop.Queue.Any()) body.Add(L("No one is waiting.", "gr-hint"));
                CloseEarlySection(body);
            }
            UpgradeSection(body, "counter");
            Footer(body);
        }

        bool closeAsked;

        /// <summary>GR-22: Close for the day, before its time, asking first and saying what closing now cuts short.</summary>
        void CloseEarlySection(VisualElement body)
        {
            var shop = s.Shop;
            var row = Box("gr-row", "gr-section");
            row.name = "gr-close-early";
            if (closeAsked)
            {
                var ask = L("Close the shop now, with " + GrowthShop.ClockText(shop.DaySecondsLeft) + " of the day to go?", "gr-strong");
                body.Add(ask);
                foreach (var cost in shop.CloseEarlyCosts()) body.Add(L(cost + ".", "gr-hint"));
                var yes = Button("Close", () => { closeAsked = false; s.Act(shop.CloseEarly); }, false, true, "The night begins; the receipt shows the day");
                yes.RemoveFromClassList("es-button--secondary"); yes.AddToClassList("es-button--danger");
                row.Add(yes);
                var no = Button("Stay open", () => closeAsked = false);
                no.style.marginLeft = 10;
                row.Add(no);
            }
            else row.Add(Button("Close for the day · " + GrowthShop.ClockText(shop.DaySecondsLeft) + " left", () => closeAsked = true, false, true, "Close the shop before its time; it asks first"));
            body.Add(row);
        }

        static string Visitor(string type) => GrowthContent.Visitors.First(v => v.Id == type).Name;

        void FillWorkbench()
        {
            var shop = s.Shop;
            // GR-13: Analyzer, Planner and Upgrade, each one job (the mockup); before the analyzer, the siphon's numbers first.
            var body = Head("Research bench", "bench " + shop.Level("bench") + " · siphon " + shop.Level("siphon"), shop.Analyzer ? "Analyzer" : "Siphon",
                "Your siphon is upgraded here, and so is your study: the microscope (level 2) measures how far each mix is from something new, the analyzer (level 3) gives a hint a day, and the planner (level 4) predicts what any two known species breed. Undiscovered founders show the cultures they use in the journal.");
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
            body.Add(L("Your siphon is upgraded here: see the Upgrade tab.", "gr-strong"));
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
            var body = Head("Shop unit B", shop.UnitB ? "open" : unit.Credits + " credits", null, "Era 2 comes with 2 stars, 3 creatures discovered and 2 tanks.");
            var failure = shop.UnitBCheck(out string why);
            if (shop.UnitB) { body.Add(L("Open: four more places for tanks and one more visitor at a time.", "gr-hint")); Footer(body); return; }
            BuyPanel(body, "The unit next door", "Four more places for tanks", "And one more visitor at a time.", Gates(unit.Era, unit.Stars, unit.Credits, 0, false), failure, why,
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
                var item = ShopItem(CultureColour(c.Id), c.Name, owned ? "Researched: its grower stands in the laboratory." : "Its grower stands in the laboratory at once.", c.Rp + " RP", false);
                var b = Button(owned ? "Researched" : "Research " + c.Name + " · " + c.Rp + " RP", () => s.Act(() => shop.Research(id)), true, failure == GrowthFailure.None, owned ? null : why);
                b.style.marginTop = 10; b.style.marginRight = 0;
                item.Add(b);
                items.Add(item);
            }
            research.Add(items);
            // R10.3: at night, tomorrow's first visitors and their wishes.
            if (shop.Phase == GrowthPhase.Night) ForecastSection(AddTab("Tomorrow"));
            NotebookFooter("Back", null, null, shop.Rp + " research points");
        }

        /// <summary>An item card on a planning view: a swatch, a name, a line, its price, and its button added after.</summary>
        VisualElement ShopItem(Color swatch, string name, string line, string price, bool locked, string kind = null)
        {
            var item = Box("gr-shopitem");
            if (locked) item.AddToClassList("gr-shopitem--locked");
            var picture = Box("gr-shopitem__picture"); picture.style.backgroundColor = Color.Lerp(swatch, Color.white, 0.55f);
            var dot = Box("gr-shopitem__dot"); dot.style.backgroundColor = swatch; picture.Add(dot);
            item.Add(picture);
            if (kind != null) item.Add(Eyebrow(kind));
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
                case CultureSource.CatalogGrower: return "supplier catalog, page " + c.Stars + " (Era " + c.Era + "): its grower for " + c.Credits + " credits, or a starter vial for " + c.VialCredits;
                case CultureSource.Vat: return "supplier catalog, page " + c.Stars + " (Era " + c.Era + "): the conditioning vat, " + c.Credits + " credits and " + c.Rp + " research points; or a starter vial for " + c.VialCredits;
                default: return "research at the pinboard: " + c.Rp + " research points (Era " + c.Era + ", " + c.Stars + " stars); or a starter vial for " + c.VialCredits;
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

        /// <summary>GR-32 (F47): what kind of thing a catalog item is.</summary>
        static string CatalogKindName(CatalogKind kind) => kind == CatalogKind.Decor ? "Decoration" : kind == CatalogKind.Vial ? "Starter vial" : kind == CatalogKind.Vat ? "Machine" : "Grower";

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
                var tab = body == null ? body = Notebook("Supplier catalog", shop.Phase == GrowthPhase.Night ? "Order tonight: it arrives when you open the shop" : "Orders are placed at night", name) : AddTab(name);
                bool locked = shop.Stars < page;
                if (locked) { var tabButton = tabList[tabList.Count - 1].tab; tabButton.tooltip = "Opens at " + page + " stars"; tabButton.AddToClassList("gr-nb-tab--locked"); tab.Add(L("Opens at " + page + " stars: you have " + shop.Stars + ".", "gr-label")); }
                var items = Box("gr-items");
                foreach (var item in GrowthContent.Catalog.Where(i => i.Stars == page))
                {
                    var failure = shop.OrderCheck(item.Id, out string why);
                    string id = item.Id;
                    var card = ShopItem(item.Kind == CatalogKind.Decor ? DecorColour(GrowthContent.DecorPiece(item.Decor).Water) : CultureColour(item.Culture), item.Name, item.Text, item.Credits + " credits" + (item.Rp > 0 ? " and " + item.Rp + " RP" : ""), locked, CatalogKindName(item.Kind));
                    Button b;
                    if (failure == GrowthFailure.Ordered && shop.Orders.Contains(id))
                        b = Button("On order · take it back", () => s.Act(() => shop.CancelOrder(id)), false, shop.Phase == GrowthPhase.Night, "Orders are changed at night");
                    else if (failure == GrowthFailure.Owned)
                        b = Button("Yours", () => { }, false, false, why);
                    else
                        b = Button("Order for " + item.Credits + " cr" + (item.Rp > 0 ? " and " + item.Rp + " RP" : ""), () => s.Act(() => shop.Order(id)), item.Kind != CatalogKind.Vial, failure == GrowthFailure.None, why);
                    b.name = "gr-order-" + id;
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
                    row.Add(CreatureIcon(o.Def, o.Colour, 56, "gr-result__glyph"));
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
                        parent.Add(CreatureIcon(p.Def, p.Colour, 52, "gr-parent__glyph"));
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
                    if (makes != null) result.Add(CreatureIcon(makes, next, 56, "gr-result__glyph"));
                    else { var rg = new GrowthGlyph(new Color(0.72f, 0.67f, 0.57f)); rg.AddToClassList("gr-result__glyph"); result.Add(rg); }
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
                    shop.HasBreedingTank ? "a hybrid: try pairs in the breeding tank" : "a hybrid, bred rather than mixed (the breeding tank, Era 2)") :
                    "uses " + string.Join(" and ", Enumerable.Range(0, sp.Recipe.Length).Where(i => sp.Recipe[i] > 0).Select(i => GrowthContent.Cultures[i].Name));
                string how = shop.HowToMake(sp);
                if (known) RecipeTip(tile, sp.Name + " · " + sp.Kind.ToString().ToLowerInvariant() + " · " + how + " · " + sp.Price + " cr", sp.Recipe);
                else tile.tooltip = "??? · " + clue;
                if (known) { tile.Add(CreatureIcon(sp, ColourFound(shop, sp), 58, "gr-jt__glyph")); Pickable(tile, sp, "gr-jt--pick"); }
                else { var g = new GrowthGlyph(new Color(0.72f, 0.67f, 0.57f, 0.55f)); g.AddToClassList("gr-jt__glyph"); tile.Add(g); }
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
            int found = shop.Discovered.Count(d => GrowthContent.Get(d) != null);
            var body = Notebook("Journal", "Found " + found + " of " + GrowthContent.SpeciesCount + " creatures · " + (GrowthContent.SpeciesCount - found) + " left · colours " + shop.Journal.Count + "/" + GrowthContent.JournalEntries, "Goals");
            // GR-11: the objective board (GR.12), mirrored from the pinboard.
            BoardSections(body);
            // Creatures: GR-26's whole collection first, all 72 found and left; then each family's shelf, every family shown (one not
            // reached yet as silhouettes) so the journal shows what is left as well as what is found.
            var creatures = AddTab("Creatures");
            bool Reachable(SpeciesDef sp) => sp.Recipe != null && Enumerable.Range(0, sp.Recipe.Length).All(i => sp.Recipe[i] == 0 || shop.Owns(GrowthContent.Cultures[i].Id) || shop.Vials.ContainsKey(GrowthContent.Cultures[i].Id));
            var shelves = GrowthContent.Families.Select(f => FamilyShelf(f, Reachable)).ToList();
            var pages = new List<VisualElement> { CollectionPage(Reachable) };
            // Two families a page, so a page fits the sheet at 1280×720 without scrolling.
            for (int i = 0; i < shelves.Count; i += 2)
            {
                var page = Box("gr-body");
                foreach (var shelf in shelves.Skip(i).Take(2)) page.Add(shelf);
                pages.Add(page);
            }
            // GR-33 (F79): a creature pressed shows what makes it, above the pages.
            var picked = CreatureDetail();
            if (picked != null) creatures.Add(picked);
            Pager(creatures, pages, "The collection and its " + shelves.Count + " families");
            // R09.4: every lesson, to read again.
            GuideTab(AddTab("Guide"));
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
            // GR-13: the mix log lives here (the incubator's card links to it): each mix tried and what it made, newest first. GR-33
            // (F78): filtered, as dots and pictures, a page at a time; and the pair log the same way.
            MixLogTab(AddTab("Mix log"));
            PairLogTab(AddTab("Pairs"));
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
                if (d.ClosedEarly) chips.Add(Chip("Closed with " + GrowthShop.ClockText(d.SecondsLeft) + " to go"));
                if (d.Wishes > 0) chips.Add(Chip(d.WishesGranted + " of " + d.Wishes + (d.Wishes == 1 ? " wish" : " wishes") + " granted"));
                if (d.Tips > 0) chips.Add(Chip(d.Tips + " credits in tips"));
                stats.Add(chips);
                stats.Add(L(d.Visitors + " visitors came in and " + d.Sales + " bought, for " + d.Revenue + " credits.", "gr-hint"));
                if (d.WalkedPast > 0) stats.Add(L("More creatures, more sales: " + d.WalkedPast + " more looked in while your tanks were empty.", "gr-hint"));
                // GR-20: what visitors wished for and did not get, the most wished first, to stock tonight.
                if (d.WishesMissed.Count > 0)
                {
                    var missed = d.WishesMissed.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, System.StringComparer.Ordinal).Take(4)
                        .Select(kv => kv.Key + (kv.Value > 1 ? " (" + kv.Value + ")" : ""));
                    var line = L("Wished for and not granted: " + string.Join(", ", missed) + ". A granted wish tips a quarter more and earns a reputation point.", "gr-hint");
                    line.name = "gr-receipt-wishes";
                    stats.Add(line);
                }
                var rep = L("Reputation " + (d.ReputationGained >= 0 ? "+" : "") + d.ReputationGained + " · " + StarsText(d.StarsAfter) + (d.StarsAfter > d.StarsBefore ? " — a new star!" : d.StarsAfter >= 5 ? " — every star" : ""), "gr-strong");
                rep.style.marginTop = 10;
                stats.Add(rep);
                // GR-33 (F65): reputation (from sales and wishes) makes stars; research points are another thing, and sales never give them.
                stats.Add(L("Reputation comes from sales and granted wishes, and makes stars. Research points (" + shop.Rp + " now) come only from discoveries, never sales: spend them at the pinboard.", "gr-hint"));
                body.Add(stats);
                ExhibitionReceipt(body, d);
                // R10.3: tomorrow's first visitors and their wishes, the night's goal.
                ForecastSection(body);
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
            body.Add(L("A tiny shop and a laboratory. Make creatures, fill your first tank, sell them, and grow.", "gr-strong"));
            var steps = new[]
            {
                "Laboratory: Bloom and Flow are cultures, the genes creatures are made from. Hold the left mouse on their growers to fill your siphon (3 doses).",
                "Click the incubator to put your doses in, then open it (E) and start. Every mix of genes makes a creature; an exact recipe finds a new one.",
                "Click the incubator to take the hatchling, and click the tank in the shop to put it in. Look closely at the glass to peek.",
                "The counter (E) opens the shop for a day of " + GrowthShop.DayLengthWords + ". Hold the mouse on it to bag each sale.",
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
                guide.Add(L("Your first night is guided: a marker shows the next thing to use, and the line at the bottom says what to do. After it, each new part of the shop is explained as it appears. Skip any time from the pause menu (Esc).", "gr-strong"));
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
            // GR-23: the settings have a screen of their own (graphics, display, sound, controls, accessibility).
            var g = s.Settings;
            var open = Button("Settings", () => s.OpenSettings(), true); open.name = "gr-open-settings";
            body.Add(Item("Settings", "graphics, display, sound, controls and accessibility · music " + g.music + ", sounds " + g.sounds +
                " · " + GrowthSettings.PresetNames[g.MatchingPreset()] + " quality", open));
            if (s.Shop.Guide == GrowthGuideMode.On)
            {
                // GR-16 wave 5: a line of its own rather than a settings row, so with the sound row the card still fits at 1280x720.
                var guide = Box("gr-row"); guide.style.marginTop = 8; guide.style.alignItems = Align.Center;
                guide.Add(L("Guided first night: a marker and the line at the bottom show what to do next", "gr-hint"));
                var skipGuide = ChipButton("Skip it", () => { s.Shop.SkipGuide(); s.SaveNow(); }, true); skipGuide.style.marginLeft = 8;
                guide.Add(skipGuide);
                body.Add(guide);
            }
            PauseLessonRow(body);
            var danger = Box("gr-row"); danger.style.marginTop = 14;
            var over = Button("Start over (a new shop)", () => s.StartOver()); over.RemoveFromClassList("es-button--secondary"); over.AddToClassList("es-button--danger");
            danger.Add(over);
            var menu = Button("Main menu", () => s.OpenMenu()); menu.name = "gr-pause-menu";
            danger.Add(menu);
            danger.Add(Button("Save and quit", () => { s.SaveNow(); Application.Quit(); }));
            body.Add(danger);
            NotebookFooter("Resume", () => s.SetCard(GrowthCard.None), "Resume");
        }
    }
}
