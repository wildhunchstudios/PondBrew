using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-04: the content of one station card. The frame (StationCardPresenter) owns the title strip, the status
    /// line, the single primary button and the footer; a view fills <see cref="Root"/> (above the primary) and
    /// <see cref="After"/> (below it) and says what the primary does. Views read a snapshot the session fills
    /// (ShopToolkitCards.cs) and call the session's existing commands only.
    /// </summary>
    public abstract class StationCardView
    {
        protected readonly ShopSession Shop;
        protected readonly ShopUiStrings Strings;
        public VisualElement Root { get; }
        public VisualElement After { get; }
        /// <summary>The card's title when it isn't the station's own label (a tank's slot, the bench's name); null keeps the label.</summary>
        public string Title { get; protected set; }
        public string Status { get; protected set; }
        public string Line { get; protected set; }
        /// <summary>The primary button's label; null when this card has no primary action in its current state.</summary>
        public string PrimaryText { get; protected set; }
        public bool PrimaryEnabled { get; protected set; }
        /// <summary>Why the primary can't be used, or a warning about it; shown above it. Null when none.</summary>
        public string Reason { get; protected set; }
        public bool ReasonIsWarning { get; protected set; }

        protected StationCardView(ShopSession shop, ShopUiStrings strings, string name)
        {
            Shop = shop; Strings = strings;
            Root = new VisualElement { name = name };
            Root.AddToClassList("es-view");
            After = new VisualElement { name = name + "-after" };
            After.AddToClassList("es-view-after");
        }

        public abstract void Refresh();
        public virtual void Primary() { }
        /// <summary>Called when the card opens on this view, to reset transient state such as a confirm step.</summary>
        public virtual void Opened() { }

        protected static Label NewLabel(VisualElement parent, string name, string className, string text = "")
        {
            var label = new Label(text) { name = name };
            if (!string.IsNullOrEmpty(className)) foreach (var c in className.Split(' ')) label.AddToClassList(c);
            parent?.Add(label);
            return label;
        }

        protected static Button NewButton(VisualElement parent, string name, string className, string text, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.AddToClassList("es-button");
            if (!string.IsNullOrEmpty(className)) foreach (var c in className.Split(' ')) button.AddToClassList(c);
            parent?.Add(button);
            return button;
        }

        protected static VisualElement NewBox(VisualElement parent, string className, string name = null)
        {
            var box = new VisualElement();
            if (name != null) box.name = name;
            if (!string.IsNullOrEmpty(className)) foreach (var c in className.Split(' ')) box.AddToClassList(c);
            parent?.Add(box);
            return box;
        }

        protected static void SetText(TextElement element, string text)
        {
            text = text ?? "";
            if (element.text != text) element.text = text;
        }

        protected static void Shown(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.keyword != StyleKeyword.Undefined || element.style.display.value != display) element.style.display = display;
        }

        protected static void Enabled(VisualElement element, bool enabled)
        {
            if (element.enabledSelf != enabled) element.SetEnabled(enabled);
        }

        /// <summary>Why a disabled control can't be used, shown in a tip when it is pressed anyway (UX-08); empty when it can be.</summary>
        protected static void Tooltip(VisualElement element, string why)
        {
            why = why ?? "";
            if (element.tooltip != why) element.tooltip = why;
        }

        protected static bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        protected static string CultureClass(CultureKind kind) => "es-culture--" + kind.ToString().ToLowerInvariant();

        readonly List<Image> coins = new List<Image>();

        protected Image NewCoin(VisualElement parent, string className)
        {
            var coin = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            coin.AddToClassList(className);
            parent.Add(coin);
            coins.Add(coin);
            return coin;
        }

        /// <summary>Gives coin images their texture once the HUD theme has made it; the cards are built before it exists.</summary>
        public void FillIcons()
        {
            for (int i = 0; i < coins.Count; i++)
                if (coins[i].image == null) coins[i].image = Shop.HudIcon(ShopUiTheme.Symbol.Coin);
        }
    }

    /// <summary>Incubator card (UX.5): recipe tiles, culture chips with − / + and the wheel, Start · 10s, a progress ring.</summary>
    public sealed class IncubatorCardView : StationCardView
    {
        public const string ViewName = "es-inc";
        public const string RecipesName = "es-inc-recipes", ChipsName = "es-inc-chips", ReturnName = "es-inc-return", ReleaseName = "es-inc-release",
            GrowingName = "es-inc-growing", RingName = "es-inc-ring", SecondsName = "es-inc-seconds", CancelHelperName = "es-inc-cancel-helper";
        public static string RecipeName(string speciesId) => "es-inc-recipe-" + speciesId;
        public static string PlusName(CultureKind kind) => "es-inc-plus-" + kind;
        public static string MinusName(CultureKind kind) => "es-inc-minus-" + kind;
        public static string ChipName(CultureKind kind) => "es-inc-chip-" + kind;
        public static string ReleaseButtonName(CultureKind kind) => "es-inc-release-" + kind;

        public readonly IncubatorCardState State = new IncubatorCardState();
        readonly VisualElement dosing, recipeGrid, chipRow, releaseRow, growing;
        readonly Label noRecipes, seconds, growNote;
        readonly Button returnDoses, cancelHelper;
        readonly ProgressRing ring;
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Chip> chips = new List<Chip>();
        readonly Dictionary<CultureKind, Button> releases = new Dictionary<CultureKind, Button>();

        sealed class Tile
        {
            public Button Button; public CreatureGlyph Glyph; public Label Name, Time, Need; public VisualElement Pips; public string SpeciesId;
            public readonly List<VisualElement> PipDots = new List<VisualElement>();
        }

        sealed class Chip
        {
            public VisualElement Box; public Button Minus, Plus; public Label Text, Siphon; public CultureKind Kind;
        }

        public IncubatorCardView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            dosing = NewBox(Root, "es-inc__dosing", "es-inc-dosing");
            NewLabel(dosing, "es-inc-recipes-heading", "es-eyebrow", Strings.Get("card.inc.recipes"));
            recipeGrid = NewBox(dosing, "es-tiles", RecipesName);
            noRecipes = NewLabel(dosing, "es-inc-no-recipes", "es-note", Strings.Get("card.inc.noRecipes"));
            NewLabel(dosing, "es-inc-chamber-heading", "es-eyebrow", Strings.Get("card.inc.chamber"));
            chipRow = NewBox(dosing, "es-chips", ChipsName);
            releaseRow = NewBox(dosing, "es-release", ReleaseName);
            NewLabel(releaseRow, "es-inc-release-text", "es-note es-note--warn", Strings.Get("card.inc.release"));
            var releaseButtons = NewBox(releaseRow, "es-release__buttons");
            foreach (CultureKind kind in Enum.GetValues(typeof(CultureKind)))
            {
                var k = kind;
                var b = NewButton(releaseButtons, ReleaseButtonName(k), "es-button--tiny " + CultureClass(k), Strings.Get("hud.culture." + k), () => Shop.CardRelease(k));
                releases[k] = b;
            }

            growing = NewBox(Root, "es-inc__growing", GrowingName);
            ring = new ProgressRing { name = RingName };
            growing.Add(ring);
            var growText = NewBox(growing, "es-inc__grow-text");
            seconds = NewLabel(growText, SecondsName, "es-big-number");
            growNote = NewLabel(growText, "es-inc-grow-note", "es-note", Strings.Get("card.inc.growNote"));

            var links = NewBox(After, "es-links");
            returnDoses = NewButton(links, ReturnName, "es-link", Strings.Get("card.inc.return"), () => Shop.CardReturnAllDoses());
            cancelHelper = NewButton(links, CancelHelperName, "es-link", Strings.Get("card.inc.cancelHelper"), () => Shop.CardCancelHelper());
        }

        public override void Refresh()
        {
            Shop.FillIncubatorCard(State, Strings);
            var s = State;
            bool dosingMode = s.Mode == IncubatorCardMode.Dosing;
            Shown(dosing, dosingMode);
            Shown(growing, !dosingMode);
            bool busy = s.Busy != null;
            ReasonIsWarning = false; Reason = null; PrimaryText = null; PrimaryEnabled = false;
            switch (s.Mode)
            {
                case IncubatorCardMode.Growing:
                    Status = Strings.Get("card.inc.status.growing");
                    Line = Strings.Format("card.inc.line.growing", s.SpeciesName);
                    ring.Set(s.Duration > 0 ? 1f - s.SecondsLeft / s.Duration : 0, s.SpeciesLook);
                    SetText(seconds, Strings.Format("card.inc.seconds", Mathf.CeilToInt(s.SecondsLeft)));
                    Shown(seconds, true); Shown(growNote, true);
                    break;
                case IncubatorCardMode.Ready:
                    Status = Strings.Get("card.inc.status.ready");
                    Line = Strings.Format("card.inc.line.ready", s.SpeciesName);
                    ring.Set(1, s.SpeciesLook);
                    Shown(seconds, false); Shown(growNote, false);
                    break;
                default:
                    Status = busy ? Strings.Get("card.inc.status.busy") : s.StagedTotal > 0 ? Strings.Format("card.inc.status.dosed", s.StagedTotal) : Strings.Get("card.inc.status.mix");
                    Line = Strings.Get("card.inc.line.mix");
                    PrimaryText = Strings.Format("card.inc.start", Mathf.RoundToInt(s.Duration));
                    PrimaryEnabled = !busy && s.StagedTotal > 0;
                    Reason = busy ? s.Busy : s.StagedTotal == 0 ? Strings.Get("card.inc.why.empty") : null;
                    RefreshTiles(s, busy);
                    RefreshChips(s, busy);
                    break;
            }
            Shown(returnDoses, dosingMode);
            Enabled(returnDoses, dosingMode && !busy && s.StagedTotal > 0);
            Shown(cancelHelper, s.CanCancelHelper);
        }

        void RefreshTiles(IncubatorCardState s, bool busy)
        {
            while (tiles.Count < s.Recipes.Count) tiles.Add(NewTile(tiles.Count));
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                bool on = i < s.Recipes.Count;
                Shown(tile.Button, on);
                if (!on) continue;
                var recipe = s.Recipes[i];
                if (tile.SpeciesId != recipe.SpeciesId) { tile.SpeciesId = recipe.SpeciesId; tile.Button.name = RecipeName(recipe.SpeciesId); }
                tile.Glyph.Set(recipe.Look, 1);
                SetText(tile.Name, recipe.Name);
                SetText(tile.Time, Strings.Format("card.inc.time", Mathf.RoundToInt(s.Duration)));
                SetPips(tile, recipe);
                bool isShort = recipe.Missing.HasValue;
                Shown(tile.Need, isShort);
                if (isShort) SetText(tile.Need, Strings.Format("card.inc.need", Strings.Get("hud.culture." + recipe.Missing.Value)));
                tile.Button.EnableInClassList("es-tile--short", isShort);
                Enabled(tile.Button, !busy && !isShort);
            }
            Shown(noRecipes, s.Recipes.Count == 0);
        }

        Tile NewTile(int index)
        {
            var tile = new Tile();
            int i = index;
            tile.Button = NewButton(recipeGrid, "es-inc-recipe", "es-tile", "", () =>
            {
                if (i < State.Recipes.Count) Shop.CardRecipe(State.Recipes[i].SpeciesId);
            });
            tile.Glyph = new CreatureGlyph { name = "es-inc-recipe-glyph" };
            tile.Glyph.AddToClassList("es-tile__glyph");
            tile.Button.Add(tile.Glyph);
            tile.Name = NewLabel(tile.Button, "es-inc-recipe-name", "es-tile__name");
            var meta = NewBox(tile.Button, "es-tile__meta");
            tile.Pips = NewBox(meta, "es-card-pips");
            tile.Time = NewLabel(meta, "es-inc-recipe-time", "es-tile__time");
            tile.Need = NewLabel(tile.Button, "es-inc-recipe-need", "es-tile__need");
            return tile;
        }

        static void SetPips(Tile tile, RecipeTile recipe)
        {
            int total = recipe.Bloom + recipe.Flow + recipe.Spark + recipe.Mist;
            while (tile.PipDots.Count < total)
            {
                var dot = NewBox(tile.Pips, "es-card-pip");
                dot.pickingMode = PickingMode.Ignore;
                tile.PipDots.Add(dot);
            }
            int n = 0;
            void Fill(int count, CultureKind kind)
            {
                for (int j = 0; j < count; j++, n++)
                {
                    var dot = tile.PipDots[n];
                    foreach (CultureKind k in Enum.GetValues(typeof(CultureKind))) dot.EnableInClassList(CultureClass(k), k == kind);
                    Shown(dot, true);
                }
            }
            Fill(recipe.Bloom, CultureKind.Bloom); Fill(recipe.Flow, CultureKind.Flow); Fill(recipe.Spark, CultureKind.Spark); Fill(recipe.Mist, CultureKind.Mist);
            for (int j = n; j < tile.PipDots.Count; j++) Shown(tile.PipDots[j], false);
        }

        void RefreshChips(IncubatorCardState s, bool busy)
        {
            while (chips.Count < s.Chips.Count) chips.Add(NewChip(chips.Count));
            for (int i = 0; i < chips.Count; i++)
            {
                var chip = chips[i];
                bool on = i < s.Chips.Count;
                Shown(chip.Box, on);
                if (!on) continue;
                var data = s.Chips[i];
                if (chip.Kind != data.Kind || chip.Box.name != ChipName(data.Kind))
                {
                    chip.Box.RemoveFromClassList(CultureClass(chip.Kind));
                    chip.Kind = data.Kind;
                    chip.Box.AddToClassList(CultureClass(data.Kind));
                    chip.Box.name = ChipName(data.Kind); chip.Plus.name = PlusName(data.Kind); chip.Minus.name = MinusName(data.Kind);
                }
                SetText(chip.Text, Strings.Format("card.inc.chip", Strings.Get("hud.culture." + data.Kind), data.Staged));
                SetText(chip.Siphon, Strings.Format("card.inc.inSiphon", data.InSiphon));
                Enabled(chip.Plus, !busy && data.InSiphon > 0);
                Enabled(chip.Minus, !busy && data.Staged > 0);
            }
            foreach (var pair in releases)
            {
                int index = s.Chips.FindIndex(c => c.Kind == pair.Key);
                bool show = index >= 0;
                Shown(pair.Value, show);
                if (show) Enabled(pair.Value, !busy && s.Chips[index].InSiphon > 0);
            }
            Shown(releaseRow, s.SiphonFull && !busy);
        }

        Chip NewChip(int index)
        {
            var chip = new Chip { Kind = CultureKind.Bloom };
            int i = index;
            chip.Box = NewBox(chipRow, "es-chip-dose " + CultureClass(chip.Kind), ChipName(chip.Kind));
            chip.Minus = NewButton(chip.Box, MinusName(chip.Kind), "es-step es-step--small", Strings.Get("card.tank.lower"), () => Step(i, -1));
            var middle = NewBox(chip.Box, "es-chip-dose__text");
            chip.Text = NewLabel(middle, "es-inc-chip-text", "es-chip-dose__count");
            chip.Siphon = NewLabel(middle, "es-inc-chip-siphon", "es-chip-dose__siphon");
            chip.Plus = NewButton(chip.Box, PlusName(chip.Kind), "es-step es-step--small", Strings.Get("card.tank.raise"), () => Step(i, 1));
            // The wheel doses: up adds one from the siphon, down returns one.
            chip.Box.RegisterCallback<WheelEvent>(e => { Step(i, e.delta.y < 0 ? 1 : -1); e.StopPropagation(); });
            return chip;
        }

        void Step(int chipIndex, int direction)
        {
            if (chipIndex >= State.Chips.Count || State.Busy != null || State.Mode != IncubatorCardMode.Dosing) return;
            var kind = State.Chips[chipIndex].Kind;
            if (direction > 0) Shop.CardDose(kind); else Shop.CardReturnDose(kind);
        }

        public override void Primary() => Shop.CardStart();
    }

    /// <summary>Display tank price tag (UX.5): − / + (Shift ±5, wheel), the four customer faces, Use standard; stock tiles when empty.</summary>
    public sealed class TankCardView : StationCardView
    {
        public const string ViewName = "es-tank";
        public const string TagName = "es-tank-tag", PriceName = "es-tank-price", LowerName = "es-tank-lower", RaiseName = "es-tank-raise",
            StandardName = "es-tank-standard", StandardChipName = "es-tank-standard-chip", BuyersName = "es-tank-buyers", DetailName = "es-tank-detail",
            ReturnName = "es-tank-return", StockName = "es-tank-stock", SpeciesName = "es-tank-species";
        public static string FaceName(string archetypeId) => "es-tank-face-" + archetypeId;
        public static string StockTileName(int index) => "es-tank-stock-" + index;

        public readonly TankCardState State = new TankCardState();
        readonly VisualElement tag, stock, stockGrid, faceRow, priceRow, priceAmount;
        readonly Label species, price, buyers, detail, standardChip, delivering;
        readonly Button lower, raise, useStandard, backToRack;
        readonly List<Face> faces = new List<Face>();
        readonly List<StockButton> stockTiles = new List<StockButton>();
        int hovered = -1;
        // UX-08: the price and moods last shown, so a change wiggles the tag or bounces the face (not the first fill).
        bool priceKnown;
        string priceSpecies;
        int priceShown;

        public int Wiggles { get; private set; }
        public int Bounces { get; private set; }
        public VisualElement PriceAmount => priceAmount;

        sealed class Face { public VisualElement Box; public MoodFace Mood; public Label Word; public string ArchetypeId; public bool HasMood; public BuyerMood LastMood; }
        sealed class StockButton { public Button Button; public CreatureGlyph Glyph; public Label Name, Where; }

        public TankCardView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            tag = NewBox(Root, "es-tag", TagName);
            var hole = NewBox(tag, "es-tag__hole");
            hole.pickingMode = PickingMode.Ignore;
            species = NewLabel(tag, SpeciesName, "es-tag__species");
            priceRow = NewBox(tag, "es-tag__price-row");
            lower = NewButton(priceRow, LowerName, "es-step", Strings.Get("card.tank.lower"), () => StepPrice(-1));
            var amount = NewBox(priceRow, "es-tag__amount", "es-tank-amount");
            priceAmount = amount;
            NewCoin(amount, "es-tag__coin");
            price = NewLabel(amount, PriceName, "es-tag__price");
            raise = NewButton(priceRow, RaiseName, "es-step", Strings.Get("card.tank.raise"), () => StepPrice(1));
            priceRow.RegisterCallback<WheelEvent>(e => { StepPrice(e.delta.y < 0 ? 1 : -1); e.StopPropagation(); });
            var standardRow = NewBox(tag, "es-tag__standard");
            standardChip = NewLabel(standardRow, StandardChipName, "es-tag__standard-chip", Strings.Get("card.tank.standardChip"));
            useStandard = NewButton(standardRow, StandardName, "es-link", "", () =>
            {
                if (!State.Empty && State.SpeciesId != null) Shop.CardSetPrice(State.SpeciesId, State.Standard);
            });
            NewBox(tag, "es-rule");
            faceRow = NewBox(tag, "es-faces", "es-tank-faces");
            var footer = NewBox(tag, "es-tag__footer");
            buyers = NewLabel(footer, BuyersName, "es-tag__buyers");
            detail = NewLabel(footer, DetailName, "es-tag__detail", Strings.Get("card.tank.shift"));

            stock = NewBox(Root, "es-stock", StockName);
            delivering = NewLabel(stock, "es-tank-delivering", "es-note es-note--warn", Strings.Get("card.tank.delivering"));
            stockGrid = NewBox(stock, "es-tiles", "es-tank-stock-grid");

            var links = NewBox(After, "es-links");
            backToRack = NewButton(links, ReturnName, "es-link", "", () =>
            {
                if (State.ReturnStorageId != null && State.ResidentId != null) Shop.CardReturnToRack(State.ReturnStorageId, State.ResidentId);
            });
        }

        public override void Opened()
        {
            hovered = -1;
            priceKnown = false;
            foreach (var face in faces) face.HasMood = false;
        }

        public override void Refresh()
        {
            Shop.FillTankCard(State, Strings);
            var s = State;
            Title = s.Title;
            PrimaryText = null; PrimaryEnabled = false; Reason = null; ReasonIsWarning = false;
            Shown(tag, !s.Empty);
            Shown(stock, s.Empty);
            if (s.Empty)
            {
                Status = Strings.Get("card.tank.status.empty");
                Line = Strings.Get(s.Sources.Count > 0 ? "card.tank.line.empty" : "card.tank.line.none");
                Shown(delivering, s.StockerDelivering);
                RefreshStock(s);
                Shown(backToRack, false);
                priceKnown = false;
                return;
            }
            Status = Strings.Get(s.Reserved ? "card.tank.status.reserved" : "card.tank.status.sale");
            Line = s.Reserved ? Strings.Format("card.tank.line.reserved", s.ReservedPrice) : Strings.Format("card.tank.line.tag", s.SpeciesName);
            SetText(species, s.SpeciesName);
            SetText(price, s.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (priceKnown && priceSpecies == s.SpeciesId && priceShown != s.Price)
            {
                Wiggles++;
                Shop.ToolkitUi?.Feel?.Motion.Play(priceAmount, UiMotionKind.PriceWiggle);
            }
            priceKnown = true; priceSpecies = s.SpeciesId; priceShown = s.Price;
            Enabled(lower, s.Price > s.Minimum);
            Enabled(raise, s.Price < s.Maximum);
            // A press on a step at its limit says why (UX-08 disabled press).
            Tooltip(lower, s.Price > s.Minimum ? "" : Strings.Get("card.tank.why.min"));
            Tooltip(raise, s.Price < s.Maximum ? "" : Strings.Get("card.tank.why.max"));
            bool standard = s.Price == s.Standard;
            Shown(standardChip, standard);
            Shown(useStandard, !standard);
            if (!standard) SetText(useStandard, Strings.Format("card.tank.useStandard", s.Standard));
            RefreshFaces(s);
            SetText(buyers, Strings.Format("card.tank.buyers", s.Buyers, s.Types));
            if (hovered >= 0 && hovered < s.Faces.Count)
            {
                var face = s.Faces[hovered];
                string word = Strings.Get(StationCardRules.MoodWordKey(face.Reaction, face.OverBudget));
                string tip = Strings.Format("card.tank.faceTip", face.Name, word, face.Budget);
                SetText(detail, face.Collects ? Strings.Format("card.tank.collects", tip) : tip);
            }
            else SetText(detail, Strings.Get("card.tank.shift"));
            bool canReturn = !s.Reserved && s.ReturnStorageId != null;
            Shown(backToRack, canReturn);
            if (canReturn) SetText(backToRack, Strings.Format("card.tank.back", s.ReturnWhere));
        }

        void RefreshFaces(TankCardState s)
        {
            while (faces.Count < s.Faces.Count) faces.Add(NewFace(faces.Count));
            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                bool on = i < s.Faces.Count;
                Shown(face.Box, on);
                if (!on) continue;
                var data = s.Faces[i];
                bool same = face.ArchetypeId == data.ArchetypeId;
                if (!same) { face.ArchetypeId = data.ArchetypeId; face.Box.name = FaceName(data.ArchetypeId); }
                var mood = StationCardRules.MoodFor(data.Reaction, data.OverBudget);
                // UX-08: a face whose mood swaps bounces (fades in with reduced motion).
                if (same && face.HasMood && face.LastMood != mood)
                {
                    Bounces++;
                    Shop.ToolkitUi?.Feel?.Motion.Play(face.Mood, UiMotionKind.FaceBounce);
                }
                face.HasMood = true; face.LastMood = mood;
                face.Mood.Set(mood, data.ArchetypeId, HudPresenter.ArchetypeColor(data.ArchetypeId));
                SetText(face.Word, Strings.Get(StationCardRules.MoodWordKey(data.Reaction, data.OverBudget)));
                face.Word.EnableInClassList("es-face-word--buys", StationCardRules.WillBuy(mood) && mood == BuyerMood.Happy);
                face.Word.EnableInClassList("es-face-word--pricey", mood == BuyerMood.Neutral);
                face.Word.EnableInClassList("es-face-word--nope", !StationCardRules.WillBuy(mood));
            }
        }

        Face NewFace(int index)
        {
            var face = new Face();
            int i = index;
            face.Box = NewBox(faceRow, "es-face-cell", "es-tank-face");
            face.Box.pickingMode = PickingMode.Position;
            face.Mood = new MoodFace { name = "es-tank-face-mood" };
            face.Box.Add(face.Mood);
            face.Word = NewLabel(face.Box, "es-tank-face-word", "es-face-word");
            face.Word.pickingMode = PickingMode.Ignore;
            // Runtime UI Toolkit has no tooltips: hovering a face shows its exact reaction and budget in the tag's footer.
            face.Box.RegisterCallback<PointerEnterEvent>(_ => hovered = i);
            face.Box.RegisterCallback<PointerLeaveEvent>(_ => { if (hovered == i) hovered = -1; });
            return face;
        }

        void RefreshStock(TankCardState s)
        {
            while (stockTiles.Count < s.Sources.Count) stockTiles.Add(NewStockTile(stockTiles.Count));
            for (int i = 0; i < stockTiles.Count; i++)
            {
                var tile = stockTiles[i];
                bool on = i < s.Sources.Count;
                Shown(tile.Button, on);
                if (!on) continue;
                var data = s.Sources[i];
                tile.Glyph.Set(data.Look, 1);
                SetText(tile.Name, data.Name);
                SetText(tile.Where, data.Where);
                Enabled(tile.Button, data.Enabled);
            }
        }

        StockButton NewStockTile(int index)
        {
            var tile = new StockButton();
            int i = index;
            tile.Button = NewButton(stockGrid, StockTileName(index), "es-tile es-tile--stock", "", () =>
            {
                if (i < State.Sources.Count) Shop.CardStock(State.Sources[i]);
            });
            tile.Glyph = new CreatureGlyph { name = "es-tank-stock-glyph" };
            tile.Glyph.AddToClassList("es-tile__glyph");
            tile.Button.Add(tile.Glyph);
            tile.Name = NewLabel(tile.Button, "es-tank-stock-name", "es-tile__name");
            tile.Where = NewLabel(tile.Button, "es-tank-stock-where", "es-tile__time");
            return tile;
        }

        void StepPrice(int direction)
        {
            var s = State;
            if (s.Empty || s.SpeciesId == null) return;
            int target = StationCardRules.PriceStep(s.Price, s.Minimum, s.Maximum, direction, ShiftHeld);
            if (target != s.Price) Shop.CardSetPrice(s.SpeciesId, target);
        }
    }

    /// <summary>Counter order clipboard (UX.5): waiting customers, Serve, Open shop · 8:00 / Close early with a confirm step.</summary>
    public sealed class CounterCardView : StationCardView
    {
        public const string ViewName = "es-counter";
        public const string OrdersName = "es-counter-orders", NoneName = "es-counter-none", BrowsingName = "es-counter-browsing", DayRowName = "es-counter-day",
            CloseEarlyName = "es-counter-close-early", ConfirmName = "es-counter-confirm", KeepName = "es-counter-keep", CloseNowName = "es-counter-close-now",
            CrewName = "es-counter-crew", ArrangeName = "es-counter-arrange", ReportName = "es-counter-report", NextGoalName = "es-counter-next-goal",
            CashierName = "es-counter-cashier";
        public static string RowName(int index) => "es-counter-row-" + index;
        public const int MaxRows = 4;

        public readonly CounterCardState State = new CounterCardState();
        readonly VisualElement heading, orders, dayRow, confirm;
        readonly Label none, browsing, more, dayHeading, dayLeft, cashier;
        readonly Button closeEarly, arrange, report, nextGoal;
        readonly List<Row> rows = new List<Row>();
        bool confirming;

        public bool Confirming => confirming;

        sealed class Row { public VisualElement Box; public HudFace Face; public Label Species, Meta, Price; }

        public CounterCardView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            heading = NewBox(Root, "es-clip__heading");
            NewLabel(heading, "es-counter-heading", "es-clip__title", Strings.Get("card.counter.orders"));
            orders = NewBox(Root, "es-orders", OrdersName);
            for (int i = 0; i < MaxRows; i++) rows.Add(NewRow(i));
            more = NewLabel(Root, "es-counter-more", "es-note");
            none = NewLabel(Root, NoneName, "es-note", Strings.Get("card.counter.none"));
            browsing = NewLabel(Root, BrowsingName, "es-note");

            NewBox(After, "es-rule");
            dayRow = NewBox(After, "es-day-row", DayRowName);
            var dayText = NewBox(dayRow, "es-day-row__text");
            dayHeading = NewLabel(dayText, "es-counter-day-heading", "es-eyebrow");
            dayLeft = NewLabel(dayText, "es-counter-day-left", "es-day-row__left");
            closeEarly = NewButton(dayRow, CloseEarlyName, "es-button--secondary es-button--compact", Strings.Get("card.counter.closeEarly"), () => confirming = true);
            confirm = NewBox(After, "es-confirm", ConfirmName);
            NewLabel(confirm, "es-counter-confirm-text", "es-confirm__text", Strings.Get("card.counter.confirm"));
            var confirmButtons = NewBox(confirm, "es-confirm__buttons");
            NewButton(confirmButtons, KeepName, "es-button--secondary es-button--compact", Strings.Get("card.counter.keep"), () => confirming = false);
            NewButton(confirmButtons, CloseNowName, "es-button--danger es-button--compact", Strings.Get("card.counter.closeNow"), () =>
            {
                confirming = false;
                Shop.CardCloseEarly();
            });
            var links = NewBox(After, "es-links");
            NewButton(links, CrewName, "es-link", Strings.Get("card.counter.crew"), () => Shop.CardCrew());
            arrange = NewButton(links, ArrangeName, "es-link", Strings.Get("card.counter.arrange"), () => Shop.CardArrange());
            report = NewButton(links, ReportName, "es-link", "", () => Shop.CardReport());
            nextGoal = NewButton(links, NextGoalName, "es-link", Strings.Get("card.counter.nextGoal"), () => Shop.CardNextGoal());
            cashier = NewLabel(After, CashierName, "es-note");
        }

        Row NewRow(int index)
        {
            var row = new Row();
            row.Box = NewBox(orders, "es-order", RowName(index));
            row.Face = new HudFace { name = "es-counter-face" };
            row.Face.AddToClassList("es-order__face");
            row.Box.Add(row.Face);
            var text = NewBox(row.Box, "es-order__text");
            row.Species = NewLabel(text, "es-counter-species", "es-order__species");
            row.Meta = NewLabel(text, "es-counter-meta", "es-order__meta");
            var amount = NewBox(row.Box, "es-order__amount");
            NewCoin(amount, "es-order__coin");
            row.Price = NewLabel(amount, "es-counter-price", "es-order__price");
            return row;
        }

        public override void Opened() => confirming = false;

        public override void Refresh()
        {
            Shop.FillCounterCard(State);
            var s = State;
            bool night = s.Phase == HudDayPhase.Night;
            if (s.Phase != HudDayPhase.Open) confirming = false;
            int waiting = 0;
            foreach (var r in s.Rows) if (r.AtCounter) waiting++;
            Status = night ? Strings.Get("card.counter.status.night") : waiting > 0 ? Strings.Format("card.counter.status.waiting", waiting) :
                Strings.Get(s.Phase == HudDayPhase.Open ? "card.counter.status.open" : "card.counter.status.closing");
            // While open the clipboard's Orders heading says it all; night and closing get a line.
            Line = night ? Strings.Get("card.counter.line.night") : s.Phase == HudDayPhase.Open ? null : Strings.Get("card.counter.line.closing");
            Reason = null; ReasonIsWarning = false;
            if (StationCardRules.PrimaryFor(s.Phase) == CounterPrimary.OpenShop)
            {
                PrimaryText = Strings.Format("card.counter.open", ShopHudRules.Clock(s.TradingSeconds));
                PrimaryEnabled = true;
                string warning = StationCardRules.NightWarningKey(s.Displays, s.EmptyDisplays);
                if (warning != null) { Reason = Strings.Format(warning, s.EmptyDisplays); ReasonIsWarning = true; }
            }
            else
            {
                PrimaryText = s.CanServe ? Strings.Format("card.counter.serve", s.ServePrice) : Strings.Get("card.counter.serveIdle");
                PrimaryEnabled = s.CanServe;
                if (!s.CanServe) Reason = Strings.Get(waiting == 0 ? "card.counter.why.nobody" : !s.AtCounter ? "card.counter.why.notAtCounter" : "card.counter.why.paused");
            }
            Shown(heading, !night);
            RefreshRows(s, night);
            Shown(dayRow, !night && !confirming);
            if (!night)
            {
                SetText(dayHeading, Strings.Get(s.Phase == HudDayPhase.Open ? "card.counter.shopOpen" : "card.counter.shopClosing"));
                SetText(dayLeft, Strings.Format("card.counter.left", ShopHudRules.Clock(s.SecondsLeft)));
                Shown(closeEarly, s.Phase == HudDayPhase.Open);
            }
            Shown(confirm, confirming);
            Shown(arrange, night);
            Shown(report, night && s.HasReport);
            if (s.HasReport) SetText(report, Strings.Format("card.counter.report", s.ReportDay));
            Shown(nextGoal, s.ShowNextGoal);
            Shown(cashier, !string.IsNullOrEmpty(s.Cashier));
            SetText(cashier, s.Cashier);
        }

        void RefreshRows(CounterCardState s, bool night)
        {
            int shown = Mathf.Min(s.Rows.Count, MaxRows);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool on = i < shown;
                Shown(row.Box, on);
                if (!on) continue;
                var data = s.Rows[i];
                float patience = data.PatienceSeconds > 0 ? Mathf.Clamp01(data.SecondsLeft / data.PatienceSeconds) : 1;
                row.Face.Set(patience, data.AtCounter && patience <= 0.3f, data.AtCounter, HudPresenter.ArchetypeColor(data.ArchetypeId), null);
                SetText(row.Species, data.Species);
                SetText(row.Meta, data.AtCounter ? Strings.Format("card.counter.row.meta", data.Customer, Mathf.CeilToInt(data.SecondsLeft)) :
                    Strings.Format("card.counter.row.coming", data.Customer));
                SetText(row.Price, data.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
                row.Box.EnableInClassList("es-order--next", i == 0 && data.AtCounter);
                row.Box.EnableInClassList("es-order--coming", !data.AtCounter);
            }
            int extra = s.Rows.Count - shown;
            Shown(more, extra > 0);
            if (extra > 0) SetText(more, Strings.Format("hud.queue.more", extra));
            Shown(none, !night && s.Rows.Count == 0);
            Shown(browsing, !night && s.Browsing > 0);
            if (s.Browsing > 0) SetText(browsing, Strings.Format("card.counter.browsing", s.Browsing));
            Shown(orders, shown > 0);
        }

        public override void Primary()
        {
            if (StationCardRules.PrimaryFor(State.Phase) == CounterPrimary.OpenShop) Shop.CardOpenShop();
            else Shop.CardServe();
        }
    }
}
