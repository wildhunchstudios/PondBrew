using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-02's interface in the game's UI Toolkit style (the UX tokens, HUD and card sheets): the HUD (day and clock,
    /// credits and stars, the current goal, the siphon and what is carried), the prompt under the reticle with its hold
    /// progress, the peek label, notes, station cards (GR.13: the incubator's dose slots, "close to something new" and mix
    /// log; a container's occupants as space icons; Upgrade on every station), and the journal, night receipt, welcome and
    /// pause screens. Greybox: plain layout, the words in English only.
    /// </summary>
    public sealed partial class GrowthUi : MonoBehaviour
    {
        GrowthSession s;
        UIDocument document;
        PanelSettings settings;
        VisualElement root, hud, cardLayer, screenLayer, toasts, peekTag, promptBox, promptProgress, card, banner;
        Label dayLabel, clockLabel, creditsLabel, starsLabel, goalLabel, promptName, promptStatus, siphonLabel, carryLabel, hintLabel, peekLabel, bannerLabel, liveLabel;
        float hintUntil, bannerUntil, refreshClock;
        string cardSignature;
        readonly List<(VisualElement element, float until)> toastItems = new List<(VisualElement, float)>();
        public bool Ready { get; private set; }
        public VisualElement Root => root;
        public VisualElement CardElement => card;
        /// <summary>Notes shown so far (validation reads them).</summary>
        public readonly List<string> Log = new List<string>();

        public void Initialize(GrowthSession session)
        {
            s = session;
            var theme = Resources.Load<ThemeStyleSheet>(ShopUiRoot.ThemeResource);
            var tokens = Resources.Load<StyleSheet>(ShopUiRoot.TokensResource);
            var hudStyles = Resources.Load<StyleSheet>(ShopUiRoot.HudStylesResource);
            var cardStyles = Resources.Load<StyleSheet>(ShopUiRoot.CardStylesResource);
            var screenStyles = Resources.Load<StyleSheet>(ShopUiRoot.ScreenStylesResource);
            if (!theme || !tokens || !hudStyles || !cardStyles || !screenStyles) { Debug.LogError("GR-02: a UI Toolkit resource is missing"); return; }
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
            foreach (var sheet in new[] { tokens, hudStyles, cardStyles, screenStyles }) root.styleSheets.Add(sheet);
            root.pickingMode = PickingMode.Ignore;
            hud = Layer("gr-hud"); cardLayer = Layer("gr-cards"); screenLayer = Layer("gr-screens");
            BuildHud();
            Ready = true;
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

        /// <summary>A one-line HUD label in ink on the paper chips (never wrapped, so the chip grows to fit it).</summary>
        static Label Line(string text, float size, bool title)
        {
            var l = new Label(text);
            l.AddToClassList("es-text");
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.fontSize = size;
            l.style.color = title ? new Color(0.17f, 0.14f, 0.11f) : new Color(0.33f, 0.29f, 0.23f);
            if (title) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        static VisualElement Paper(float padding = 14)
        {
            var p = new VisualElement();
            p.style.backgroundColor = new Color(1f, 0.965f, 0.875f, 0.96f);
            p.style.borderTopLeftRadius = p.style.borderTopRightRadius = p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = 18;
            p.style.borderBottomWidth = 4; p.style.borderBottomColor = new Color(0.79f, 0.72f, 0.56f);
            p.style.paddingLeft = p.style.paddingRight = padding + 4; p.style.paddingTop = p.style.paddingBottom = padding * 0.6f;
            return p;
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

        // ---------------------------------------------------------------- the HUD

        void BuildHud()
        {
            var crosshair = new VisualElement(); crosshair.AddToClassList("es-crosshair"); hud.Add(crosshair);
            var left = Paper(); left.style.position = Position.Absolute; left.style.left = 32; left.style.top = 28; left.style.minWidth = 300;
            dayLabel = Line("Night 1", 26, true); clockLabel = Line("", 17, false);
            left.Add(dayLabel); left.Add(clockLabel); hud.Add(left);
            var right = Paper(); right.style.position = Position.Absolute; right.style.right = 32; right.style.top = 28; right.style.alignItems = Align.FlexEnd;
            creditsLabel = Line("10 credits", 26, true); starsLabel = Line("", 17, false);
            right.Add(creditsLabel); right.Add(starsLabel); hud.Add(right);
            var goal = Paper(10); goal.style.position = Position.Absolute; goal.style.left = 32; goal.style.top = 140; goal.style.maxWidth = 520;
            goalLabel = Text(""); goalLabel.style.fontSize = 18; goal.Add(goalLabel); hud.Add(goal);
            var pockets = Paper(10); pockets.style.position = Position.Absolute; pockets.style.left = 32; pockets.style.bottom = 32;
            siphonLabel = Line("", 19, false); carryLabel = Line("", 19, false);
            pockets.Add(siphonLabel); pockets.Add(carryLabel); hud.Add(pockets);
            var prompts = new VisualElement(); prompts.AddToClassList("es-prompts"); hud.Add(prompts);
            promptBox = new VisualElement(); promptBox.AddToClassList("es-prompt");
            promptName = new Label(); promptName.AddToClassList("es-prompt__name");
            promptStatus = new Label(); promptStatus.AddToClassList("es-prompt__status");
            promptProgress = new VisualElement(); promptProgress.AddToClassList("es-prompt__progress");
            promptBox.Add(promptName); promptBox.Add(promptStatus); promptBox.Add(promptProgress);
            prompts.Add(promptBox);
            hintLabel = Text(""); hintLabel.style.color = new Color(1f, 0.92f, 0.75f); hintLabel.style.fontSize = 18; hintLabel.style.marginLeft = 8;
            prompts.Add(hintLabel);
            toasts = new VisualElement(); toasts.AddToClassList("es-floaters"); hud.Add(toasts);
            peekTag = Paper(6); peekTag.style.position = Position.Absolute; peekTag.pickingMode = PickingMode.Ignore;
            peekTag.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-100, LengthUnit.Percent));
            peekLabel = Line("", 19, true); peekTag.Add(peekLabel); peekTag.style.display = DisplayStyle.None; hud.Add(peekTag);
            banner = Paper(20); banner.style.position = Position.Absolute; banner.style.left = new Length(50, LengthUnit.Percent); banner.style.top = 220;
            banner.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0); banner.style.display = DisplayStyle.None;
            bannerLabel = Text(""); bannerLabel.style.fontSize = 40; bannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter; banner.Add(bannerLabel); hud.Add(banner);
        }

        public static string StarsText(int stars) => new string('★', stars) + new string('☆', 5 - stars);

        public void Tick(float dt)
        {
            if (!Ready) return;
            var shop = s.Shop;
            dayLabel.text = (shop.Phase == GrowthPhase.Night ? "Night " : "Day ") + shop.Day + " · Era " + shop.Era;
            clockLabel.text = shop.Phase == GrowthPhase.Day ? ShopClock(shop.DaySecondsLeft) + " left · " + shop.Queue.Count() + " waiting" : "Untimed: open the shop at the counter";
            creditsLabel.text = shop.Credits + " credits";
            int next = shop.Stars < 5 ? GrowthContent.StarThresholds[shop.Stars] : GrowthContent.ReputationMax;
            starsLabel.text = StarsText(shop.Stars) + "  reputation " + shop.Reputation + (shop.Stars < 5 ? "/" + next : "") + (shop.Rp > 0 ? " · " + shop.Rp + " RP" : "");
            var goals = shop.Objectives();
            // GR-05: the HUD keeps to what is still to do, three lines at most; the pinboard has every line.
            var todo = goals.Where(g => !g.done).ToList();
            goalLabel.text = "Goal: " + shop.EraGoal + (todo.Count == 0 ? "" : "\n" + string.Join("\n", todo.Take(3).Select(g => "· " + g.text))) +
                (todo.Count > 3 ? "\n· " + (todo.Count - 3) + " more on the pinboard" : "");
            siphonLabel.text = "Siphon " + shop.Held.Count + "/" + shop.SiphonCapacity + (shop.Held.Count == 0 ? ": empty" : ": " + string.Join(", ", shop.Held.GroupBy(c => c).Select(g => g.Count() + " " + GrowthShop.Name(g.Key))));
            carryLabel.text = shop.Carried.Count == 0 ? "Hands free" : "Carrying " + shop.Carried.Count + "/" + GrowthShop.CarryLimit + ": " + string.Join(", ", shop.Carried.Select(GrowthSession.Named)) + " · E on a tank to put in";
            bool free = s.Card == GrowthCard.None;
            hud.style.display = s.Card == GrowthCard.Pause || s.Card == GrowthCard.Welcome ? DisplayStyle.None : DisplayStyle.Flex;
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
            if (Time.unscaledTime > bannerUntil) banner.style.display = DisplayStyle.None;
            for (int i = toastItems.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > toastItems[i].until) { toastItems[i].element.RemoveFromHierarchy(); toastItems.RemoveAt(i); }
            UpdatePeek();
            refreshClock += dt;
            if (refreshClock >= 0.1f) { refreshClock = 0; if (card != null && Signature() != cardSignature) RefreshCard(); }
            if (liveLabel != null) liveLabel.text = LiveText();
        }

        static string ShopClock(float seconds) { int t = Mathf.Max(0, Mathf.CeilToInt(seconds)); return t / 60 + ":" + (t % 60).ToString("00"); }

        void UpdatePeek()
        {
            var creature = s.PeekCreature;
            if (creature == null || document == null || document.rootVisualElement.panel == null) { peekTag.style.display = DisplayStyle.None; return; }
            var spec = s.Shop.Stock.FirstOrDefault(x => x.Id == creature.SpecimenId);
            if (spec == null) { peekTag.style.display = DisplayStyle.None; return; }
            var holder = s.Shop.HolderOf(spec.Id);
            peekLabel.text = GrowthSession.Named(spec) + (spec.ReservedBy != 0 ? " (reserved)" : "");
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(document.rootVisualElement.panel, creature.transform.position + Vector3.up * (creature.Length * 0.9f), s.Player.View);
            peekTag.style.display = DisplayStyle.Flex;
            peekTag.style.left = p.x;
            peekTag.style.top = p.y - 18;
        }

        public void Hint(string text) { hintLabel.text = text; hintUntil = Time.unscaledTime + 1.5f; }

        public void Toast(string text, bool warn = false)
        {
            Log.Add(text);
            if (!Ready) return;
            var t = Paper(8); t.style.marginBottom = 6;
            var l = Text(text); l.style.fontSize = 20; if (warn) l.style.color = new Color(0.54f, 0.18f, 0.10f);
            t.Add(l); toasts.Add(t);
            toastItems.Add((t, Time.unscaledTime + (warn ? 3.5f : 3f)));
            while (toastItems.Count > 4) { toastItems[0].element.RemoveFromHierarchy(); toastItems.RemoveAt(0); }
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
                    Log.Add(e.Text);
                    if (!Ready) return;
                    bannerLabel.text = e.Text; banner.style.display = DisplayStyle.Flex; bannerUntil = Time.unscaledTime + 3f;
                    return;
                default: Toast(e.Text, e.Kind == GrowthEventKind.Refusal || e.Kind == GrowthEventKind.Impatient || e.Kind == GrowthEventKind.StaffHome); return;
            }
        }

        // ---------------------------------------------------------------- cards

        public void ShowCard(GrowthCard which, string target)
        {
            if (!Ready) return;
            cardLayer.Clear(); screenLayer.Clear(); card = null; liveLabel = null;
            if (which == GrowthCard.None) return;
            bool screen = which == GrowthCard.Journal || which == GrowthCard.Receipt || which == GrowthCard.Welcome || which == GrowthCard.Pause;
            card = screen ? ScreenCard() : StationCard(which == GrowthCard.Incubator || which == GrowthCard.Tank || which == GrowthCard.Crate || which == GrowthCard.Catalog || which == GrowthCard.Breeding || which == GrowthCard.Helper ? 580 : which == GrowthCard.Place || which == GrowthCard.Board || which == GrowthCard.Workbench || which == GrowthCard.Counter || which == GrowthCard.Grower || which == GrowthCard.Buffer || which == GrowthCard.Staff ? 540 : which == GrowthCard.PowerBoard || which == GrowthCard.Sorter ? 600 : 470);
            (screen ? screenLayer : cardLayer).Add(screen ? Dim(card) : card);
            cardSignature = Signature();
            Fill(which, target);
        }

        public void RefreshCard() { if (card != null) ShowCard(s.Card, s.CardTarget); }

        /// <summary>Validation: scrolls the open card to its first label that starts with the text (journal pages below the fold).</summary>
        public bool ScrollTo(string startsWith)
        {
            if (card == null) return false;
            var label = card.Query<Label>().ToList().FirstOrDefault(l => l.text.StartsWith(startsWith));
            var scroll = card.Q<ScrollView>();
            if (label == null || scroll == null) return false;
            float y = label.worldBound.y - scroll.contentContainer.worldBound.y;
            scroll.scrollOffset = new Vector2(0, Mathf.Max(0, y - 8));
            return true;
        }

        /// <summary>Every label and button text on the open card or screen (for the validation route).</summary>
        public string CardText() => card == null ? "" : string.Join(" | ", card.Query<TextElement>().ToList().Select(t => t.text));

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

        VisualElement Head(string title, string status = "")
        {
            var strip = new VisualElement(); strip.AddToClassList("es-card__strip"); strip.style.flexShrink = 0;
            var t = new Label(title); t.AddToClassList("es-card__title");
            var st = new Label(status); st.AddToClassList("es-card__status");
            strip.Add(t); strip.Add(st);
            card.Add(strip);
            // GR-03: a station card's body scrolls when it is taller than the card may be (tank cards, the crate).
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("es-card__body");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; scroll.style.flexShrink = 1;
            card.Add(scroll);
            return scroll.contentContainer;
        }

        VisualElement ScreenHead(string title)
        {
            var t = new Label(title); t.AddToClassList("es-screen-card__title"); t.style.fontSize = 44;
            card.Add(t);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.marginTop = 12;
            card.Add(scroll);
            return scroll.contentContainer;
        }

        void Footer(VisualElement body, string hint = "Esc closes")
        {
            var row = new VisualElement(); row.AddToClassList("es-row"); row.style.marginTop = 10; row.style.alignItems = Align.Center;
            var key = new Label("Esc"); key.AddToClassList("es-keycap");
            var t = Text(hint, "es-text--muted"); t.style.marginLeft = 8; t.style.flexGrow = 1;
            row.Add(key); row.Add(t);
            row.Add(Button("Close", () => s.SetCard(GrowthCard.None)));
            body.Add(row);
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

        string Signature()
        {
            var shop = s.Shop;
            return s.Card + "|" + s.CardTarget + "|" + shop.Credits + "|" + shop.Phase + "|" + shop.Held.Count + string.Join(",", shop.Held) + "|" + string.Join(",", shop.Chamber) +
                "|" + shop.Incubating + "|" + (shop.Hatchling?.Id ?? 0) + "|" + shop.Carried.Count + "|" + string.Join(",", shop.Containers.Select(c => c.Kind + c.Level + c.Water + c.Place)) + "|" + shop.Rp + "|" + string.Join(",", shop.Cultures) +
                "|" + string.Join(",", shop.Vials.Select(v => v.Key + v.Value)) + "|" + string.Join(",", shop.Orders) + "|" + string.Join(",", shop.Containers.Select(c => c.Occupants.Count + ":" + c.Occupants.Count(o => o.ReservedBy != 0))) +
                "|" + shop.Queue.Count() + "|" + shop.Stars + "|" + shop.Era + "|" + shop.UnitB + "|" + string.Join(",", GrowthShop.UpgradeableStations.Select(shop.Level)) +
                "|" + string.Join(",", shop.Parents.Select(p => p.Id)) + "|" + (shop.Offspring?.Id ?? 0) + "|" + string.Join(",", shop.Pairs[1].Parents.Select(p => p.Id)) + (shop.Pairs[1].Offspring?.Id ?? 0) + "|" + shop.Hatchlings.Count + shop.IncubatorTint + "|" + shop.Tint + "|" + shop.PairLog.Count + "|" + shop.Journal.Count + "|" + string.Join(",", shop.Carried.Select(c => c.Id)) + "|" + Mathf.FloorToInt(shop.GrowerUnits.Values.Sum()) + "|" + PowerSignature() + "|" + LineSignature() + "|" + HelperSignature() + "|" + SpacesSignature();
        }

        string LiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Incubator: return shop.Incubating ? "Incubating… " + Mathf.CeilToInt(shop.IncubationLeft) + " s" : shop.Hatchlings.Count > 1 ? "A clutch of " + shop.Hatchlings.Count + " is ready." : shop.Hatchling != null ? "A hatchling is ready." : "Idle.";
                case GrowthCard.Counter: return shop.Phase == GrowthPhase.Day ? "Open: " + ShopClock(shop.DaySecondsLeft) + " left." : "Closed for the night.";
                case GrowthCard.Grower: return s.CardTarget == null ? "" : Mathf.FloorToInt(shop.GrowerUnits[s.CardTarget]) + "/" + shop.GrowerCapacityOf(s.CardTarget) + " doses ready";
                case GrowthCard.Breeding: return !shop.HasBreedingTank ? "" : shop.Offspring != null ? "An offspring is in the nursery." : shop.Breeding ? "Breeding… " + Mathf.CeilToInt(shop.BreedingLeft) + " s" : "Waiting for a pair.";
                default: return PowerLiveText() ?? LineLiveText() ?? HelperLiveText() ?? "";
            }
        }

        void Live(VisualElement body)
        {
            liveLabel = Text(LiveText()); liveLabel.style.fontSize = 22; liveLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(liveLabel);
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
            }
        }

        static string Cost(LevelDef l) => l.Credits + " cr" + (l.Rp > 0 ? " and " + l.Rp + " RP" : "");

        /// <summary>
        /// GR-06's upgrade framework on every station card (GR.9, GR.13): the station's levels as a ladder, those it has ticked,
        /// the rest with their change, cost and gate or what they wait for, and the next level's button when it can be bought.
        /// </summary>
        void UpgradeSection(VisualElement body, string station, string title = null)
        {
            var shop = s.Shop;
            var def = GrowthContent.Station(station);
            int now = shop.Level(station);
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            var head = Text((title ?? def.Name) + " · level " + now + " of " + def.Levels.Length); head.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(head);
            foreach (var l in def.Levels)
            {
                if (l.Level <= now) { section.Add(Text("✓ " + l.Level + ". " + l.Change, "es-req--done")); continue; }
                string waits = GrowthShop.WaitsFor(station, l.Level);
                section.Add(Text("· " + l.Level + ". " + l.Change + " · " + (waits ?? Cost(l) + ", era " + l.Era + ", " + l.Stars + "★"), "es-text--muted"));
            }
            var failure = shop.UpgradeCheck(station, out var next, out string why);
            if (next != null && GrowthShop.WaitsFor(station, next.Level) == null)
                section.Add(Button(failure == GrowthFailure.None ? "Upgrade to level " + next.Level + " for " + Cost(next) : why, () => s.Act(() => shop.Upgrade(station)), true, failure == GrowthFailure.None));
            else if (next == null) section.Add(Text("Highest level.", "es-text--muted"));
            body.Add(section);
        }

        void FillGrower(string culture)
        {
            var shop = s.Shop;
            bool vat = GrowthContent.Culture(culture).Source == CultureSource.Vat;
            var body = Head(vat ? "Conditioning vat (" + GrowthShop.Name(culture) + ")" : GrowthShop.Name(culture) + " grower", vat ? "vat level " + shop.Level("vat") : "level " + shop.Level("grower"));
            Live(body);
            body.Add(Text("Hold the left mouse on a grower to fill the siphon: " + (shop.PerHold > 1 ? shop.PerHold + " doses" : "one dose") + " every " + shop.HoldSeconds.ToString("0.0") + " s. It holds " + shop.GrowerCapacityOf(culture) +
                " and refills one dose every " + shop.RefillSecondsOf(culture).ToString("0.#") + " s by itself."));
            body.Add(Text("Siphon: " + shop.Held.Count + "/" + shop.SiphonCapacity + " doses.", "es-text--muted"));
            TapSection(body, culture);
            if (vat) UpgradeSection(body, "vat", "The vat");
            UpgradeSection(body, "grower", "Every grower");
            Footer(body, "One upgrade covers every grower.");
        }

        void FillIncubator()
        {
            var shop = s.Shop;
            var body = Head("Incubator", "level " + shop.Level("incubator") + " · " + shop.MaxDoses + " doses");
            Live(body);
            if (shop.Hatchling != null)
            {
                string name = GrowthSession.Named(shop.Hatchling);
                int clutch = shop.Hatchlings.Count;
                body.Add(Text((clutch > 1 ? "Out came a clutch of " + clutch + ": " + GrowthShop.Plural(name) : "Out came a " + name) + (shop.LastMix != null && shop.LastMix.WasNew ? " — new to your journal!" : ".")));
                body.Add(Button("Take " + (clutch > 1 ? "a " : "the ") + name + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(shop.TakeHatchling), true, shop.Carried.Count < GrowthShop.CarryLimit));
            }
            // The siphon's doses, each pressed into the chamber.
            body.Add(Text("In the siphon (press to add):", "es-text--muted"));
            var held = Row();
            foreach (var g in shop.Held.GroupBy(c => c))
            {
                string culture = g.Key;
                var b = Button("+ " + GrowthShop.Name(culture) + " (" + g.Count() + ")", () => s.Act(() => shop.AddDose(culture)), false, !shop.Incubating && shop.Chamber.Count < shop.MaxDoses);
                b.style.borderLeftWidth = 8; b.style.borderLeftColor = CultureColour(culture);
                held.Add(b);
            }
            if (shop.Held.Count == 0) held.Add(Text("Empty: hold the mouse on a grower to collect.", "es-text--muted"));
            body.Add(held);
            body.Add(Text("The mix (" + shop.Chamber.Count + "/" + shop.MaxDoses + ", press a dose to take it back):", "es-text--muted"));
            var slots = Row();
            for (int i = 0; i < shop.MaxDoses; i++)
            {
                if (i < shop.Chamber.Count)
                {
                    string culture = shop.Chamber[i];
                    var b = Button(GrowthShop.Name(culture), () => s.Act(() => shop.ReturnDose(culture)), false, !shop.Incubating);
                    b.style.backgroundColor = CultureColour(culture); b.style.color = Color.white;
                    slots.Add(b);
                }
                else
                {
                    // An empty slot: an outlined space the next dose goes into.
                    var empty = new VisualElement(); empty.style.width = 70; empty.style.height = 30; empty.style.marginTop = 6; empty.style.marginRight = 6;
                    empty.style.borderTopWidth = empty.style.borderBottomWidth = empty.style.borderLeftWidth = empty.style.borderRightWidth = 2;
                    empty.style.borderTopColor = empty.style.borderBottomColor = empty.style.borderLeftColor = empty.style.borderRightColor = new Color(0.79f, 0.72f, 0.56f);
                    empty.style.borderTopLeftRadius = empty.style.borderTopRightRadius = empty.style.borderBottomLeftRadius = empty.style.borderBottomRightRadius = 10;
                    slots.Add(empty);
                }
            }
            body.Add(slots);
            if (shop.Chamber.Count > 0 && !shop.Incubating)
            {
                var preview = shop.Preview();
                if (preview.CloseToNew && !shop.Microscope) body.Add(Text("Hmm… this is one dose from something new.", "es-card__reason"));
                // GR-06: the research bench's microscope measures every mix: warmer as it nears something new.
                if (shop.Microscope)
                    body.Add(Text("Microscope: " + (preview.DistanceToNew < 0 ? "nothing new left to find with these cultures" : preview.DistanceToNew == 0 ? "this mix is something new" :
                        preview.DistanceToNew + " dose" + (preview.DistanceToNew == 1 ? "" : "s") + " from something new" + (preview.DistanceToNew <= 1 ? " (hot)" : preview.DistanceToNew <= 2 ? " (warm)" : " (cold)")),
                        preview.CloseToNew ? "es-card__reason" : "es-text"));
            }
            body.Add(Button(shop.Incubating ? "Incubating…" : "Mix and incubate (" + shop.IncubationSeconds.ToString("0") + " s)", () => s.Act(shop.StartIncubation), true,
                !shop.Incubating && shop.Hatchling == null && shop.Chamber.Count > 0));
            if (shop.LastMix != null && shop.LastMix.DistanceToNew == 1 && !shop.LastMix.Exact)
                body.Add(Text("Your last mix was one dose from something new.", "es-card__reason"));
            // The mix log: each distinct mix and what it made, with known recipes in one press.
            var logTitle = Text("Mix log"); logTitle.style.fontSize = 17; logTitle.style.unityFontStyleAndWeight = FontStyle.Bold; logTitle.style.marginTop = 10;
            body.Add(logTitle);
            if (shop.MixLog.Count == 0) body.Add(Text("Every mix makes a creature. Try two or three doses.", "es-text--muted"));
            foreach (var m in shop.MixLog.AsEnumerable().Reverse().Take(6))
            {
                var row = Row();
                for (int i = 0; i < m.Doses.Length; i++) for (int n = 0; n < m.Doses[i]; n++) row.Add(Dot(CultureColour(GrowthContent.Cultures[i].Id), 16));
                var label = Text("→ " + GrowthContent.Get(m.Species).Name + (m.Count > 1 ? " ×" + m.Count : "") + (m.DistanceToNew == 1 && !m.Exact ? " · one from new" : ""));
                label.style.marginLeft = 6; label.style.flexGrow = 1; row.Add(label);
                if (m.Exact && shop.Discovered.Contains(m.Species))
                {
                    string species = m.Species;
                    row.Add(Button("Use", () => s.Act(() => shop.FillRecipe(species)), false, !shop.Incubating));
                }
                body.Add(row);
            }
            // GR-06: the tint port (level 3) colours the next clutch, as the breeding tank's water does.
            if (shop.IncubatorTintPort)
            {
                var tintBox = new VisualElement(); tintBox.style.marginTop = 8;
                if (shop.IncubatorTint != null)
                {
                    tintBox.Add(Text("Tinted with " + GrowthShop.Name(shop.IncubatorTint) + ": the next clutch is " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, shop.IncubatorTint) + "."));
                    tintBox.Add(Button("Take the tint back", () => s.Act(shop.ClearIncubatorTint), false, shop.Held.Count < shop.SiphonCapacity));
                }
                else
                {
                    tintBox.Add(Text("Tint port: a dose from the siphon colours the next clutch.", "es-text--muted"));
                    var tints = Row();
                    foreach (var culture in GrowthShop.TintCultures)
                    {
                        string c = culture;
                        var f = shop.IncubatorTintCheck(c, out string why);
                        var b = Button(GrowthShop.Name(c) + " → " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, c), () => s.Act(() => shop.TintIncubator(c)), false, f == GrowthFailure.None,
                            f == GrowthFailure.NoDose ? "Collect " + GrowthShop.Name(c) + " first" : why);
                        b.style.borderLeftWidth = 8; b.style.borderLeftColor = CultureColour(c);
                        tints.Add(b);
                    }
                    tintBox.Add(tints);
                }
                body.Add(tintBox);
            }
            AutoDoserSection(body);
            ChuteSection(body);
            UpgradeSection(body, "incubator");
            Footer(body);
        }

        static string Holds(ContainerDef def) => def.Largest == SizeClass.Large ? "creatures of every size" : "small and medium creatures";

        /// <summary>GR.13's occupant grid: one cell a space, each creature as wide as its spaces in its colour, a sold one paler.</summary>
        VisualElement SpaceGrid(GrowthContainer c)
        {
            var grid = Row(); grid.style.marginTop = 6; grid.style.marginBottom = 4;
            void Cell(int spaces, Color fill, bool outline, bool sold)
            {
                var cell = new VisualElement();
                cell.style.width = 22 * spaces + 4 * (spaces - 1); cell.style.height = 22; cell.style.marginRight = 4; cell.style.marginBottom = 4;
                cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 6;
                if (outline)
                {
                    cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 2;
                    cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = new Color(0.79f, 0.72f, 0.56f);
                }
                else cell.style.backgroundColor = sold ? Color.Lerp(fill, Color.white, 0.55f) : fill;
                grid.Add(cell);
            }
            foreach (var o in c.Occupants.OrderBy(o => o.Species)) Cell((int)o.Def.Size, GrowthCreatureView.ColourOf(o.Def, o.Colour), false, o.ReservedBy != 0);
            for (int i = 0; i < c.Free; i++) Cell(1, Color.clear, true, false);
            return grid;
        }

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

        void FillTank(string containerId)
        {
            var shop = s.Shop;
            var c = shop.Container(containerId);
            if (c == null) { s.SetCard(GrowthCard.None); return; }
            var body = Head(c.Def.Name + " · level " + c.Level, c.Used + "/" + c.Spaces + " spaces");
            body.Add(Text(c.Def.Lab ? "In the workshop, off the shop floor: visitors never see it. Holds " + Holds(c.Def) + "; carry creatures from here to the shop's tanks." :
                "Holds " + Holds(c.Def) + ": a small one takes 1 space, a medium one 2, a large one 4. Three of a kind swim as a school and sell a little better.", "es-text--muted"));
            body.Add(SpaceGrid(c));
            // Each species in it with its count; press one to take a creature of it out.
            var grid = Row();
            foreach (var g in c.Occupants.GroupBy(o => (o.Species, o.Colour)))
            {
                var free = g.FirstOrDefault(o => o.ReservedBy == 0);
                int sold = g.Count(o => o.ReservedBy != 0);
                var def = g.First().Def;
                var b = Button(GrowthSession.Named(g.First()) + (g.Count() > 1 ? " ×" + g.Count() : "") + (sold > 0 ? " (" + sold + " sold)" : "") + " · " + shop.Price(def.Id) + " cr",
                    () => { if (free != null) s.Act(() => shop.TakeOut(c.Id, free.Id)); }, false, free != null && shop.Carried.Count < GrowthShop.CarryLimit, "Take one out");
                b.style.borderLeftWidth = 10; b.style.borderLeftColor = GrowthCreatureView.ColourOf(def, g.Key.Colour);
                grid.Add(b);
            }
            body.Add(grid);
            var schools = c.Occupants.GroupBy(o => o.Species).Where(g => g.Count() >= GrowthShop.SchoolSize).Select(g => GrowthContent.Get(g.Key).Name).ToList();
            if (schools.Count > 0) body.Add(Text("School: " + string.Join(", ", schools), "es-card__reason"));
            if (shop.Carried.Count > 0)
            {
                var put = Row();
                foreach (var carried in shop.Carried.ToList())
                {
                    var spec = carried;
                    bool fits = GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(x => x.Species), spec.Species);
                    put.Add(Button("Put in " + GrowthSession.Named(spec), () => s.Act(() => shop.PutIn(c.Id, spec.Id)), true, fits, fits ? null : (int)spec.Def.Size > (int)c.Def.Largest ? "Too big for this tank" : "Not enough spaces"));
                }
                body.Add(put);
            }
            // GR.5 comfort (era 3): the water, harmony and the water kits (not for a lab rack, which is never on show).
            if (shop.Era >= GrowthContent.ComfortEra && !c.Def.Lab)
            {
                var waterLine = Text("Water: " + c.Water.ToString().ToLowerInvariant() + " · " + HarmonyText(c)); waterLine.style.marginTop = 8;
                body.Add(waterLine);
                var waters = Row();
                foreach (Water w in Enum.GetValues(typeof(Water)))
                {
                    if (w == c.Water) continue;
                    var water = w;
                    var failure = shop.WaterCheck(c.Id, water, out string why);
                    waters.Add(Button(water + " water · " + GrowthContent.WaterKitCredits + " cr", () => s.Act(() => shop.SetWater(c.Id, water)), false, failure == GrowthFailure.None, why));
                }
                body.Add(waters);
            }
            int appeal = shop.Appeal(c);
            if (appeal > 0)
                body.Add(Text("Appeal +" + appeal + " (" + string.Join(", ", new[] { c.Kind == "show" ? "lit Show tank" : null, shop.Harmony(c) ? "harmony" : null }.Where(x => x != null)) +
                    "): visitors look here first and stretch their budget " + (appeal * 10) + "%.", "es-card__reason"));
            // The next level.
            var section = new VisualElement(); section.style.marginTop = 10; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 6;
            var upgrade = shop.ContainerUpgradeCheck(c.Id, out var next, out string upgradeWhy);
            if (next == null) section.Add(Text("Highest level.", "es-text--muted"));
            else
            {
                section.Add(Text("Level " + next.Level + ": " + next.Change + " · " + next.Credits + " credits"));
                section.Add(Button(upgrade == GrowthFailure.None ? "Fit it for " + next.Credits + " cr" : upgradeWhy, () => s.Act(() => shop.UpgradeContainer(c.Id)), true, upgrade == GrowthFailure.None));
            }
            // Arranging, at night: move it with its creatures to a free place it fits, or sell it empty.
            if (shop.Phase == GrowthPhase.Night)
            {
                var places = shop.OwnedPlaces.Where(p => shop.MoveCheck(c.Id, p, out _) == GrowthFailure.None).ToList();
                var arrange = Row();
                foreach (var p in places) { var place = p; arrange.Add(Button("Move to " + place, () => { if (s.Act(() => shop.MoveContainer(c.Id, place)).Success) s.SetCard(GrowthCard.None); })); }
                var sell = shop.SellCheck(c.Id, out string sellWhy);
                arrange.Add(Button(sell == GrowthFailure.None ? "Sell (+" + GrowthShop.SellValue(c) + " cr)" : "Sell: " + sellWhy.Substring(0, 1).ToLowerInvariant() + sellWhy.Substring(1), () => { if (s.Act(() => shop.SellContainer(c.Id)).Success) s.SetCard(GrowthCard.None); }, false, sell == GrowthFailure.None));
                section.Add(arrange);
            }
            body.Add(section);
            body.Add(Text("Peek: look closely at the glass to read each creature; E takes the one under the reticle.", "es-text--muted"));
            Footer(body);
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
            Footer(body, "F8 or Esc closes");
        }

        void FillCounter()
        {
            var shop = s.Shop;
            var body = Head("Counter", "level " + shop.Level("counter"));
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
            var body = Head("Research bench", "bench level " + shop.Level("bench") + " · siphon level " + shop.Level("siphon"));
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
            var body = Head("Pinboard", "era " + shop.Era + ": " + GrowthContent.Eras[shop.Era - 1].Name);
            body.Add(Text("Goal: " + shop.EraGoal));
            foreach (var (text, done) in shop.Objectives()) body.Add(Text((done ? "✓ " : "· ") + text, done ? "es-req--done" : "es-req"));
            body.Add(Text("Discovered " + shop.Discovered.Count + " of " + GrowthContent.SpeciesCount + " species · journal " + shop.Journal.Count + "/" + GrowthContent.JournalEntries, "es-text--muted"));
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

        // ---------------------------------------------------------------- the breeding tank (GR-05)

        void FillBreeding()
        {
            var shop = s.Shop;
            var levels = GrowthContent.Station("breeding").Levels;
            if (!shop.HasBreedingTank)
            {
                var intro = Head("Breeding tank", "not bought");
                intro.Add(Text("Two creatures in, one offspring out, and the parents stay. Every pair makes something: hybrids, the families' crowns and new colours come from breeding, and a known pair makes more stock without cultures."));
                var first = levels[0];
                intro.Add(Text("Level 1: " + first.Change + " · " + first.Credits + " credits and " + first.Rp + " research points (era " + first.Era + ", " + first.Stars + " stars).", "es-text--muted"));
                var buy = shop.UpgradeCheck("breeding", out _, out string buyWhy);
                intro.Add(Button(buy == GrowthFailure.None ? "Buy the breeding tank for " + first.Credits + " cr and " + first.Rp + " RP" : buyWhy,
                    () => s.Act(() => shop.Upgrade("breeding")), true, buy == GrowthFailure.None));
                Footer(intro);
                return;
            }
            var body = Head("Breeding tank", "level " + shop.Level("breeding") + " · a pair every " + shop.BreedSeconds.ToString("0") + " s");
            Live(body);
            for (int k = 0; k < shop.PairPlaces; k++)
            {
            var pairState = shop.Pairs[k];
            int pairIndex = k;
            if (pairState.Offspring != null)
            {
                var o = pairState.Offspring;
                body.Add(Text("In the nursery: a " + GrowthSession.Named(o) + (pairState.OffspringWasNew ? " — new to your journal!" : "."), pairState.OffspringWasNew ? "es-card__reason" : "es-text"));
                body.Add(Button("Take the " + GrowthSession.Named(o) + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(() => shop.TakeOffspring(pairIndex)), true, shop.Carried.Count < GrowthShop.CarryLimit));
            }
            string pairName = shop.PairPlaces > 1 ? "Pair " + (k + 1) : "The pair";
            body.Add(Text(pairName + " (" + pairState.Parents.Count + "/" + GrowthShop.PairSize + ", press one to take it out at any time)" + (shop.PairPlaces > 1 && pairState.Breeding ? " · " + Mathf.CeilToInt(pairState.Left) + " s" : "") + ":", "es-text--muted"));
            var pair = Row();
            foreach (var p in pairState.Parents.ToList())
            {
                int id = p.Id;
                var b = Button(GrowthSession.Named(p), () => s.Act(() => shop.TakeParent(id)), false, shop.Carried.Count < GrowthShop.CarryLimit, "Take it out");
                b.style.borderLeftWidth = 10; b.style.borderLeftColor = GrowthCreatureView.ColourOf(p.Def, p.Colour);
                pair.Add(b);
            }
            for (int i = pairState.Parents.Count; i < GrowthShop.PairSize; i++)
            {
                var empty = new VisualElement(); empty.style.width = 110; empty.style.height = 30; empty.style.marginTop = 6; empty.style.marginRight = 6;
                empty.style.borderTopWidth = empty.style.borderBottomWidth = empty.style.borderLeftWidth = empty.style.borderRightWidth = 2;
                empty.style.borderTopColor = empty.style.borderBottomColor = empty.style.borderLeftColor = empty.style.borderRightColor = new Color(0.79f, 0.72f, 0.56f);
                empty.style.borderTopLeftRadius = empty.style.borderTopRightRadius = empty.style.borderBottomLeftRadius = empty.style.borderBottomRightRadius = 10;
                pair.Add(empty);
            }
            body.Add(pair);
            if (pairState.Parents.Count >= GrowthShop.PairSize)
            {
                // What the pair makes: the pair log knows a pair once tried (the planner, research bench level 4, predicts any pair).
                var known = shop.PairEntry(pairState.Parents[0].Species, pairState.Parents[1].Species);
                var next = shop.NextColourOf(pairState);
                string colour = next == Colour.Classic ? "" : " Its colour: " + next + (shop.Tint != null ? " (the " + GrowthShop.Name(shop.Tint) + " tint)." : " (the higher parent colour).");
                var planned = known == null ? shop.PlannerPredicts(pairState.Parents[0].Species, pairState.Parents[1].Species) : null;
                body.Add(Text((known != null ? "This pair makes a " + GrowthContent.Get(known.Species).Name + " (bred ×" + known.Count + " so far)." :
                    planned != null ? "The planner: this pair makes a " + planned.Name + (shop.Discovered.Contains(planned.Id) ? "." : " — new to your journal.") : "A pair you have not tried: every pair makes something.") + colour,
                    known == null ? "es-card__reason" : "es-text"));
            }
            }
            if (shop.ActivePairs.Any(p => p.Parents.Count < GrowthShop.PairSize))
            {
                if (shop.Carried.Count == 0) body.Add(Text("Bring creatures from a tank (peek and E takes one), then E here puts them in.", "es-text--muted"));
                var put = Row();
                foreach (var carried in shop.Carried.ToList())
                {
                    var spec = carried;
                    var f = shop.ParentCheck(spec, out string why);
                    put.Add(Button(f == GrowthFailure.None ? "Put in " + GrowthSession.Named(spec) : GrowthSession.Named(spec) + ": " + why, () => s.Act(() => shop.PutParent(spec.Id)), true, f == GrowthFailure.None));
                }
                body.Add(put);
            }
            // Tinting the water (level 2): Bloom makes Sunny offspring, Flow Deep, Mist Shimmer.
            var tint = new VisualElement(); tint.style.marginTop = 10; tint.style.borderTopWidth = 2; tint.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); tint.style.paddingTop = 6;
            if (!shop.TintPort) tint.Add(Text("Tint port at level 2: a dose of Bloom, Flow or Mist in the water makes the next offspring Sunny, Deep or Shimmer. Untinted, it takes the higher parent colour (Classic, Sunny, Deep, Shimmer).", "es-text--muted"));
            else if (shop.Tint != null)
            {
                tint.Add(Text("The water is tinted with " + GrowthShop.Name(shop.Tint) + ": the next offspring is " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, shop.Tint) + "."));
                tint.Add(Button("Take the tint back", () => s.Act(shop.ClearTint), false, shop.Held.Count < shop.SiphonCapacity));
            }
            else
            {
                tint.Add(Text("Tint the water with a dose from the siphon (used by the next offspring):", "es-text--muted"));
                var tints = Row();
                foreach (var culture in GrowthShop.TintCultures)
                {
                    string c = culture;
                    var f = shop.TintCheck(c, out string why);
                    var b = Button(GrowthShop.Name(c) + " → " + GrowthRules.BreedColour(Colour.Classic, Colour.Classic, c), () => s.Act(() => shop.TintWater(c)), false, f == GrowthFailure.None,
                        f == GrowthFailure.NoDose ? "Collect " + GrowthShop.Name(c) + " first" : why);
                    b.style.borderLeftWidth = 8; b.style.borderLeftColor = CultureColour(c);
                    tints.Add(b);
                }
                tint.Add(tints);
            }
            body.Add(tint);
            // The pair log, newest first.
            var logTitle = Text("Pair log · " + shop.PairLog.Count + " pairs tried"); logTitle.style.fontSize = 17; logTitle.style.unityFontStyleAndWeight = FontStyle.Bold; logTitle.style.marginTop = 10;
            body.Add(logTitle);
            if (shop.PairLog.Count == 0) body.Add(Text("Any two creatures make something; a pair breeds the same thing every time.", "es-text--muted"));
            foreach (var p in shop.PairLog.AsEnumerable().Reverse().Take(6)) body.Add(Text(PairLine(p)));
            UpgradeSection(body, "breeding");
            Footer(body);
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
            // GR.6: the Cultures page shows all seven from the start: owned ones with their source and level, the rest
            // greyed with where to get them.
            var cultures = Text("Cultures"); cultures.style.fontSize = 24; cultures.style.unityFontStyleAndWeight = FontStyle.Bold;
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
            body.Add(Text("Mix log: " + shop.MixLog.Count + " different mixes tried.", "es-text--muted"));
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
            }
            body.Add(Text("Night " + shop.Day + ": prepare at your own pace. Goal: " + shop.EraGoal, "es-text--muted"));
            var row = Row(); row.Add(Button("Good night", () => s.SetCard(GrowthCard.None), true)); body.Add(row);
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
