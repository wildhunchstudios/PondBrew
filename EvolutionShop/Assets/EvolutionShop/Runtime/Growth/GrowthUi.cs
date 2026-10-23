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
    public sealed class GrowthUi : MonoBehaviour
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
            starsLabel.text = StarsText(shop.Stars) + "  reputation " + shop.Reputation + (shop.Stars < 5 ? "/" + next : "");
            var goals = shop.Objectives();
            goalLabel.text = "Goal: " + shop.EraGoal + "\n" + string.Join("\n", goals.Select(g => (g.done ? "✓ " : "· ") + g.text));
            siphonLabel.text = "Siphon " + shop.Held.Count + "/" + shop.SiphonCapacity + (shop.Held.Count == 0 ? ": empty" : ": " + string.Join(", ", shop.Held.GroupBy(c => c).Select(g => g.Count() + " " + GrowthShop.Name(g.Key))));
            carryLabel.text = shop.Carried.Count == 0 ? "Hands free" : "Carrying " + shop.Carried.Count + "/" + GrowthShop.CarryLimit + ": " + string.Join(", ", shop.Carried.Select(c => c.Def.Name)) + " · E on a tank to put in";
            bool free = s.Card == GrowthCard.None;
            hud.style.display = s.Card == GrowthCard.Pause || s.Card == GrowthCard.Welcome ? DisplayStyle.None : DisplayStyle.Flex;
            string name = free ? s.FocusName(out string status) : null;
            promptBox.style.display = name == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (name != null)
            {
                promptName.text = name; promptStatus.text = s.FocusName(out status) != null ? status : "";
                float progress = s.CollectingNow ? shop.CollectProgress / Mathf.Max(0.01f, shop.HoldSeconds) : s.ServingNow ? shop.ServeProgress / Mathf.Max(0.01f, shop.ServeSeconds) : 0;
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
            peekLabel.text = spec.Def.Name + (spec.ReservedBy != 0 ? " (reserved)" : "");
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
                case GrowthEventKind.Collected: return;
                case GrowthEventKind.Discovery:
                case GrowthEventKind.EraUp:
                case GrowthEventKind.StarUp:
                    Log.Add(e.Text);
                    if (!Ready) return;
                    bannerLabel.text = e.Text; banner.style.display = DisplayStyle.Flex; bannerUntil = Time.unscaledTime + 3f;
                    return;
                default: Toast(e.Text, e.Kind == GrowthEventKind.Refusal || e.Kind == GrowthEventKind.Impatient); return;
            }
        }

        // ---------------------------------------------------------------- cards

        public void ShowCard(GrowthCard which, string target)
        {
            if (!Ready) return;
            cardLayer.Clear(); screenLayer.Clear(); card = null; liveLabel = null;
            if (which == GrowthCard.None) return;
            bool screen = which == GrowthCard.Journal || which == GrowthCard.Receipt || which == GrowthCard.Welcome || which == GrowthCard.Pause;
            card = screen ? ScreenCard() : StationCard(which == GrowthCard.Incubator || which == GrowthCard.Tank ? 560 : 470);
            (screen ? screenLayer : cardLayer).Add(screen ? Dim(card) : card);
            cardSignature = Signature();
            Fill(which, target);
        }

        public void RefreshCard() { if (card != null) ShowCard(s.Card, s.CardTarget); }

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
            c.style.right = 64; c.style.top = 140; c.style.width = width;
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
            var strip = new VisualElement(); strip.AddToClassList("es-card__strip");
            var t = new Label(title); t.AddToClassList("es-card__title");
            var st = new Label(status); st.AddToClassList("es-card__status");
            strip.Add(t); strip.Add(st);
            card.Add(strip);
            var body = new VisualElement(); body.AddToClassList("es-card__body");
            card.Add(body);
            return body;
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
                "|" + shop.Incubating + "|" + (shop.Hatchling?.Id ?? 0) + "|" + shop.Carried.Count + "|" + shop.Containers.Count + "|" + string.Join(",", shop.Containers.Select(c => c.Occupants.Count + ":" + c.Occupants.Count(o => o.ReservedBy != 0))) +
                "|" + shop.Queue.Count() + "|" + shop.Stars + "|" + shop.Era + "|" + shop.UnitB + "|" + string.Join(",", GrowthShop.GreyboxStations.Select(shop.Level)) + "|" + Mathf.FloorToInt(shop.GrowerUnits.Values.Sum());
        }

        string LiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Incubator: return shop.Incubating ? "Incubating… " + Mathf.CeilToInt(shop.IncubationLeft) + " s" : shop.Hatchling != null ? "A hatchling is ready." : "Idle.";
                case GrowthCard.Counter: return shop.Phase == GrowthPhase.Day ? "Open: " + ShopClock(shop.DaySecondsLeft) + " left." : "Closed for the night.";
                case GrowthCard.Grower: return s.CardTarget == null ? "" : Mathf.FloorToInt(shop.GrowerUnits[s.CardTarget]) + "/" + shop.GrowerCapacity + " doses ready";
                default: return "";
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
                case GrowthCard.Counter: FillCounter(); break;
                case GrowthCard.Workbench: FillWorkbench(); break;
                case GrowthCard.UnitB: FillUnitB(); break;
                case GrowthCard.Board: FillBoard(); break;
                case GrowthCard.Journal: FillJournal(); break;
                case GrowthCard.Receipt: FillReceipt(); break;
                case GrowthCard.Welcome: FillWelcome(); break;
                case GrowthCard.Pause: FillPause(); break;
            }
        }

        void UpgradeSection(VisualElement body, string station)
        {
            var shop = s.Shop;
            var def = GrowthContent.Station(station);
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            section.Add(Text("Upgrade · now level " + shop.Level(station) + ": " + shop.LevelDef(station).Change, "es-text--muted"));
            var failure = shop.UpgradeCheck(station, out var next, out string why);
            if (next == null) section.Add(Text(why, "es-text--muted"));
            else
            {
                section.Add(Text("Level " + next.Level + ": " + next.Change + " · " + next.Credits + " credits"));
                section.Add(Button(failure == GrowthFailure.None ? "Upgrade for " + next.Credits + " cr" : why, () => s.Act(() => shop.Upgrade(station)), true, failure == GrowthFailure.None));
            }
            body.Add(section);
        }

        void FillGrower(string culture)
        {
            var shop = s.Shop;
            var body = Head(GrowthShop.Name(culture) + " grower", "level " + shop.Level("grower"));
            Live(body);
            body.Add(Text("Hold the left mouse on a grower to fill the siphon: one dose every " + shop.HoldSeconds.ToString("0.0") + " s. It refills one dose every " + shop.RefillSeconds.ToString("0.#") + " s by itself."));
            body.Add(Text("Siphon: " + shop.Held.Count + "/" + shop.SiphonCapacity + " doses.", "es-text--muted"));
            UpgradeSection(body, "grower");
            Footer(body, "One upgrade covers every grower.");
        }

        void FillIncubator()
        {
            var shop = s.Shop;
            var body = Head("Incubator", "level " + shop.Level("incubator") + " · " + shop.MaxDoses + " doses");
            Live(body);
            if (shop.Hatchling != null)
            {
                var h = shop.Hatchling.Def;
                body.Add(Text("Out came a " + h.Name + (shop.LastMix != null && shop.LastMix.WasNew ? " — new to your journal!" : ".")));
                body.Add(Button("Take the " + h.Name + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(shop.TakeHatchling), true, shop.Carried.Count < GrowthShop.CarryLimit));
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
                if (preview.CloseToNew) body.Add(Text("Hmm… this is one dose from something new.", "es-card__reason"));
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
            UpgradeSection(body, "incubator");
            Footer(body);
        }

        void FillTank(string containerId)
        {
            var shop = s.Shop;
            var c = shop.Container(containerId);
            if (c == null) { s.SetCard(GrowthCard.None); return; }
            var body = Head("Starter tank", c.Used + "/" + c.Spaces + " spaces");
            body.Add(Text("Holds small and medium creatures: a small one takes 1 space, a medium one 2. Three of a kind swim together as a school and sell a little better."));
            // The occupants as space icons; press one to take it out.
            var grid = Row();
            foreach (var o in c.Occupants)
            {
                var spec = o;
                var b = Button(spec.Def.Name + (spec.ReservedBy != 0 ? " (sold)" : "") + " · " + shop.Price(spec.Species) + " cr", () => s.Act(() => shop.TakeOut(c.Id, spec.Id)), false, spec.ReservedBy == 0 && shop.Carried.Count < GrowthShop.CarryLimit,
                    "Take it out");
                b.style.borderLeftWidth = 10; b.style.borderLeftColor = GrowthCreatureView.ColourOf(spec.Def);
                grid.Add(b);
            }
            for (int i = c.Used; i < c.Spaces; i++) { var free = Text("□"); free.style.fontSize = 30; free.style.marginRight = 6; grid.Add(free); }
            body.Add(grid);
            var schools = c.Occupants.GroupBy(o => o.Species).Where(g => g.Count() >= GrowthShop.SchoolSize).Select(g => GrowthContent.Get(g.Key).Name).ToList();
            if (schools.Count > 0) body.Add(Text("School: " + string.Join(", ", schools), "es-card__reason"));
            if (shop.Carried.Count > 0)
            {
                var put = Row();
                foreach (var carried in shop.Carried.ToList())
                {
                    var spec = carried;
                    put.Add(Button("Put in " + spec.Def.Name, () => s.Act(() => shop.PutIn(c.Id, spec.Id)), true, GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(x => x.Species), spec.Species)));
                }
                body.Add(put);
            }
            body.Add(Text("Peek: look closely at the glass to read each creature; E takes the one under the reticle.", "es-text--muted"));
            Footer(body);
        }

        void FillPlace(string place)
        {
            var shop = s.Shop;
            int cost = GrowthContent.Container("starter").Levels[0].Credits;
            var body = Head("Empty place", place.StartsWith("b") ? "shop unit B" : "shop unit A");
            body.Add(Text("A Starter tank: 4 spaces, small and medium creatures. " + cost + " credits."));
            bool ok = shop.Phase == GrowthPhase.Night && shop.Credits >= cost;
            body.Add(Button(shop.Phase != GrowthPhase.Night ? "Tanks are placed at night" : shop.Credits < cost ? "Needs " + cost + " credits" : "Buy a Starter tank for " + cost + " cr",
                () => { if (s.Act(() => shop.BuyTank(place)).Success) s.SetCard(GrowthCard.None); }, true, ok));
            Footer(body);
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
                body.Add(Text("Hold the left mouse on the counter to bag a sale: " + shop.ServeSeconds.ToString("0.0") + " s each."));
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
            var body = Head("Tool bench", "siphon level " + shop.Level("siphon"));
            body.Add(Text("Your siphon holds " + shop.SiphonCapacity + " doses and takes " + shop.HoldSeconds.ToString("0.0") + " s for each."));
            UpgradeSection(body, "siphon");
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
            body.Add(Text("Discovered " + shop.Discovered.Count + " of " + GrowthContent.SpeciesCount + " species · journal " + shop.Discovered.Count + "/" + GrowthContent.JournalEntries, "es-text--muted"));
            Footer(body);
        }

        void FillJournal()
        {
            var shop = s.Shop;
            var body = ScreenHead("Journal");
            body.Add(Text("Brookkin: small, Bloom and Flow. Each undiscovered founder shows the cultures it uses; an exact recipe finds it.", "es-text--muted"));
            foreach (var sp in GrowthContent.Species.Where(x => x.Family == "brookkin"))
            {
                bool known = shop.Discovered.Contains(sp.Id);
                var row = Row(); row.style.marginTop = 6; row.style.flexWrap = Wrap.NoWrap;
                row.Add(Dot(known ? GrowthCreatureView.ColourOf(sp) : new Color(0.6f, 0.6f, 0.6f), 26));
                string clue = sp.Kind != SpeciesKind.Founder ? (sp.Kind == SpeciesKind.Crown ? "the family's crown, bred from its best" : "a hybrid, bred rather than mixed (breeding comes in era 2)") :
                    "uses " + string.Join(" and ", Enumerable.Range(0, sp.Recipe.Length).Where(i => sp.Recipe[i] > 0).Select(i => GrowthContent.Cultures[i].Name));
                var entry = Text(known ? sp.Name + " · " + sp.Kind.ToString().ToLowerInvariant() + " · " + (sp.Recipe != null ? GrowthReport.Recipe(sp.Recipe) : "") + " · " + sp.Price + " cr" : "??? · " + clue);
                entry.style.flexGrow = 1; entry.style.flexShrink = 1; entry.style.marginLeft = 8; entry.style.whiteSpace = WhiteSpace.NoWrap;
                row.Add(entry);
                body.Add(row);
            }
            body.Add(Text("Mix log: " + shop.MixLog.Count + " different mixes tried.", "es-text--muted"));
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
