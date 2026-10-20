using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Arrange tab (UX.6): a catalogue grid with tabs Furniture, Decor, Owned and Lab. Each tile shows a drawing, price,
    /// slots or appeal; choosing one enters the existing placement mode, where the notebook steps aside for a key bar.
    /// Owned lists everything you have with Move, Store and Sell, which asks first.
    /// </summary>
    public sealed class ArrangeView : PlanningView
    {
        public const string ViewName = "es-plan-arrange";
        public const string ClosedName = "es-plan-arrange-closed", AppealName = "es-plan-arrange-appeal", AppealBarName = "es-plan-arrange-appeal-bar";
        public static string SegName(ArrangeTab tab) => "es-plan-arrange-seg-" + tab.ToString().ToLowerInvariant();
        public static string TileName(string id) => "es-plan-tile-" + id;
        public static string TileReasonName(string id) => TileName(id) + "-why";
        public static string OwnedName(string itemId) => "es-plan-owned-" + itemId;
        public static string MoveName(string itemId) => OwnedName(itemId) + "-move";
        public static string StoreName(string itemId) => OwnedName(itemId) + "-store";
        public static string SellName(string itemId) => OwnedName(itemId) + "-sell";
        public static string KeepName(string itemId) => OwnedName(itemId) + "-keep";
        public static string SellNowName(string itemId) => OwnedName(itemId) + "-sell-now";

        readonly ArrangeViewState state = new ArrangeViewState();
        readonly Dictionary<ArrangeTab, Button> segs = new Dictionary<ArrangeTab, Button>();
        readonly Dictionary<ArrangeTab, VisualElement> pages = new Dictionary<ArrangeTab, VisualElement>();
        readonly VisualElement furniture, decor, lab, labMoves, appealFill, open;
        readonly ScrollView owned;
        readonly Label closed, appealText, ownedEmpty;
        string shape;

        public ArrangeTab Tab { get; private set; } = ArrangeTab.Furniture;
        public ArrangeViewState State => state;

        public ArrangeView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("plan.arrange.title");
            closed = NewLabel(Root, ClosedName, "es-plan-empty", Strings.Get("plan.arrange.closed"));
            open = NewBox(Root, "es-plan-arrange__open", "es-plan-arrange-open");
            var segRow = NewBox(open, "es-plan-segs", "es-plan-arrange-segs");
            foreach (ArrangeTab tab in Enum.GetValues(typeof(ArrangeTab)))
            {
                var t = tab;
                var seg = NewButton(segRow, SegName(tab), "es-plan-seg", Strings.Get("plan.arrange.tab." + tab.ToString().ToLowerInvariant()), () => Tab = t);
                segs[tab] = seg;
            }
            furniture = NewBox(open, "es-plan-tiles", "es-plan-arrange-furniture");
            pages[ArrangeTab.Furniture] = furniture;
            var decorPage = NewBox(open, "es-plan-arrange__page", "es-plan-arrange-decor");
            var appeal = NewBox(decorPage, "es-plan-appeal", AppealName);
            appealText = NewLabel(appeal, AppealName + "-text", "es-plan-appeal__text");
            var bar = NewBox(appeal, "es-plan-appeal__bar", AppealBarName);
            appealFill = NewBox(bar, "es-plan-appeal__fill", AppealBarName + "-fill");
            decor = NewBox(decorPage, "es-plan-tiles", "es-plan-arrange-decor-tiles");
            pages[ArrangeTab.Decor] = decorPage;
            var ownedPage = NewBox(open, "es-plan-arrange__page", "es-plan-arrange-owned");
            owned = new ScrollView(ScrollViewMode.Vertical) { name = "es-plan-arrange-owned-list" };
            owned.AddToClassList("es-plan-scroll");
            ownedPage.Add(owned);
            ownedEmpty = NewLabel(ownedPage, "es-plan-arrange-owned-empty", "es-plan-empty", Strings.Get("plan.arrange.ownedEmpty"));
            pages[ArrangeTab.Owned] = ownedPage;
            var labPage = NewBox(open, "es-plan-arrange__page", "es-plan-arrange-lab");
            lab = NewBox(labPage, "es-plan-tiles", "es-plan-arrange-lab-tiles");
            NewLabel(labPage, null, "es-eyebrow", Strings.Get("plan.arrange.equipment"));
            labMoves = NewBox(labPage, "es-plan-tiles es-plan-tiles--small", "es-plan-arrange-lab-moves");
            pages[ArrangeTab.Lab] = labPage;
        }

        /// <summary>A fresh visit opens on Furniture; returning from placement keeps the tab the player was on (no Opened then).</summary>
        public override void Opened() { Tab = ArrangeTab.Furniture; }

        /// <summary>Switches the catalogue's own tab (a view choice, not a command).</summary>
        public void ShowTab(ArrangeTab tab) => Tab = tab;

        public override void Refresh()
        {
            Shop.FillArrangeView(state);
            Subtitle = Strings.Format("plan.arrange.subtitle", state.Appeal, PlanningRules.AppealCap, state.DisplayCount, state.DisplayLimit);
            Note = Strings.Get(Tab == ArrangeTab.Owned ? "plan.arrange.note.owned" : "plan.arrange.note");
            Shown(closed, !state.CanArrange);
            Shown(open, state.CanArrange);
            if (!state.CanArrange) return;
            string now = Shape();
            if (now != shape) { shape = now; Rebuild(); }
            foreach (var pair in segs) pair.Value.EnableInClassList("es-plan-seg--on", pair.Key == Tab);
            foreach (var pair in pages) Shown(pair.Value, pair.Key == Tab);
            RefreshTiles(furniture, state.Furniture);
            RefreshTiles(decor, state.Decor);
            RefreshTiles(lab, state.Lab);
            RefreshTiles(labMoves, state.Lab);
            SetText(appealText, Strings.Format("plan.arrange.appeal", state.Appeal, PlanningRules.AppealCap, state.DecorCount, state.DecorLimit));
            appealFill.style.width = Length.Percent(100f * Math.Min(state.Appeal, PlanningRules.AppealCap) / PlanningRules.AppealCap);
            Shown(ownedEmpty, state.Owned.Count == 0);
            foreach (var row in state.Owned)
            {
                var line = owned.Q(OwnedName(row.ItemId));
                if (line == null) continue;
                bool pending = state.PendingSaleId == row.ItemId;
                var place = line.Q<Label>(OwnedName(row.ItemId) + "-place");
                SetText(place, Strings.Get("plan.arrange.place." + row.Place.ToString().ToLowerInvariant()));
                Shown(line.Q(OwnedName(row.ItemId) + "-stocked"), row.Stocked);
                Shown(line.Q(OwnedName(row.ItemId) + "-reserved"), row.Reserved);
                var move = line.Q<Button>(MoveName(row.ItemId));
                SetText(move, Strings.Get(row.Place == OwnedPlace.Storage ? "plan.arrange.placeOwned" : "plan.arrange.move"));
                Shown(move, !row.Reserved && !pending);
                Shown(line.Q(StoreName(row.ItemId)), !row.Reserved && !row.Stocked && row.Place != OwnedPlace.Storage && !pending && row.Resale > 0);
                Shown(line.Q(SellName(row.ItemId)), !row.Reserved && !row.Stocked && !pending && row.Resale > 0);
                Shown(line.Q(OwnedName(row.ItemId) + "-confirm"), pending);
            }
        }

        void RefreshTiles(VisualElement box, List<ArrangeTile> tiles)
        {
            foreach (var tile in tiles)
            {
                var button = box.Q<Button>(TileName(tile.Id));
                if (button == null) continue;
                Enabled(button, tile.BlockedKey == null);
                var why = button.Q<Label>(TileReasonName(tile.Id));
                if (why != null)
                {
                    Shown(why, tile.BlockedKey != null);
                    if (tile.BlockedKey != null) SetText(why, tile.BlockedKey == "plan.arrange.why.coins" ? Strings.Format(tile.BlockedKey, tile.Amount) : Strings.Get(LockedKey(tile)));
                }
            }
        }

        static string LockedKey(ArrangeTile tile)
        {
            if (tile.BlockedKey != "plan.arrange.why.locked" && tile.BlockedKey != "plan.arrange.why.limit") return tile.BlockedKey;
            switch (tile.Id)
            {
                case ShopLayout.LargeDisplayId: return tile.BlockedKey == "plan.arrange.why.locked" ? "plan.arrange.why.wing" : "plan.arrange.why.displays";
                case ShopLayout.SmallDisplayId: return "plan.arrange.why.displays";
                case ShopLayout.ConditionerDefinitionId: return "plan.arrange.why.vat";
                case ShopLayout.RackDefinitionId: return "plan.arrange.why.racks";
                default: return ShopLayout.IsDecor(tile.Id) ? "plan.arrange.why.decor" : "plan.arrange.why.tables";
            }
        }

        string Shape()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var list in new[] { state.Furniture, state.Decor, state.Lab })
            {
                foreach (var t in list) sb.Append(t.Id).Append(':').Append(t.Price).Append(':').Append(t.Appeal).Append(',');
                sb.Append('|');
            }
            foreach (var row in state.Owned) sb.Append(row.ItemId).Append(',');
            return sb.ToString();
        }

        void Rebuild()
        {
            furniture.Clear(); decor.Clear(); lab.Clear(); labMoves.Clear(); owned.Clear();
            foreach (var tile in state.Furniture) Tile(furniture, tile);
            foreach (var tile in state.Decor) Tile(decor, tile);
            foreach (var tile in state.Lab) Tile(tile.Move ? labMoves : lab, tile);
            foreach (var row in state.Owned) Owned(row);
        }

        void Tile(VisualElement parent, ArrangeTile tile)
        {
            var t = tile;
            var button = NewButton(parent, TileName(tile.Id), tile.Move ? "es-plan-tile es-plan-tile--small" : "es-plan-tile", "", () => Shop.PlanTile(t));
            var thumb = new FurnitureThumb { name = TileName(tile.Id) + "-thumb" };
            thumb.Set(tile.Look);
            button.Add(thumb);
            string name = tile.Expansion ? Strings.Get(tile.Id == "expand-lab" ? "plan.arrange.expandLab" : "plan.arrange.expandStore") :
                tile.Move ? Strings.Get("plan.facility." + tile.Id) : tile.Name;
            // An equipment move is a compact row: its drawing beside its name, so the Lab tab fits the short ultrawide panel.
            VisualElement holder = button;
            if (tile.Move) { holder = NewBox(button, "es-plan-tile__text"); holder.pickingMode = PickingMode.Ignore; }
            NewLabel(holder, TileName(tile.Id) + "-name", "es-plan-tile__name", name).pickingMode = PickingMode.Ignore;
            var meta = NewBox(holder, "es-plan-tile__meta");
            meta.pickingMode = PickingMode.Ignore;
            if (tile.Move) NewLabel(meta, TileName(tile.Id) + "-price", "es-plan-tile__detail", Strings.Get("plan.arrange.moveFree")).pickingMode = PickingMode.Ignore;
            else
            {
                NewCoin(meta, "es-plan-tile__coin");
                NewLabel(meta, TileName(tile.Id) + "-price", "es-plan-tile__price", tile.Price.ToString()).pickingMode = PickingMode.Ignore;
                string detail = tile.Expansion ? Strings.Get(tile.Id == "expand-lab" ? "plan.arrange.expandLabDetail" : "plan.arrange.expandStoreDetail") :
                    ShopLayout.IsDecor(tile.Id) ? Strings.Format(tile.Appeal > 0 ? "plan.arrange.appealGain" : "plan.arrange.appealNone", tile.Appeal) :
                    tile.Slots > 0 ? Strings.Format(tile.Slots == 1 ? "plan.arrange.slot" : "plan.arrange.slots", tile.Slots) :
                    Strings.Get(tile.Id == ShopLayout.RackDefinitionId ? "plan.arrange.rackDetail" : tile.Id == ShopLayout.ConditionerDefinitionId ? "plan.arrange.vatDetail" : "plan.arrange.noSlots");
                NewLabel(meta, TileName(tile.Id) + "-detail", "es-plan-tile__detail", detail).pickingMode = PickingMode.Ignore;
            }
            NewLabel(button, TileReasonName(tile.Id), "es-plan-tile__why").pickingMode = PickingMode.Ignore;
        }

        void Owned(OwnedRow row)
        {
            string id = row.ItemId;
            var line = NewBox(owned, "es-plan-row", OwnedName(id));
            var thumb = new FurnitureThumb { name = OwnedName(id) + "-thumb" };
            thumb.AddToClassList("es-plan-row__thumb");
            thumb.Set(row.DefinitionId);
            line.Add(thumb);
            var text = NewBox(line, "es-plan-row__text");
            NewLabel(text, OwnedName(id) + "-name", "es-plan-row__name", row.Name);
            NewLabel(text, OwnedName(id) + "-label", "es-plan-row__meta", Strings.Format("plan.arrange.number", row.Label));
            NewLabel(line, OwnedName(id) + "-place", "es-plan-chip");
            NewLabel(line, OwnedName(id) + "-stocked", "es-plan-chip es-plan-chip--on", Strings.Get("plan.arrange.stocked"));
            NewLabel(line, OwnedName(id) + "-reserved", "es-plan-chip es-plan-chip--warn", Strings.Get("plan.arrange.reserved"));
            var actions = NewBox(line, "es-plan-row__actions");
            NewButton(actions, MoveName(id), "es-button--secondary es-button--compact", "", () => Shop.PlanMoveOwned(id));
            NewButton(actions, StoreName(id), "es-button--secondary es-button--compact", Strings.Get("plan.arrange.store"), () => Shop.StoreFurniture(id));
            NewButton(actions, SellName(id), "es-button--danger es-button--compact", Strings.Format("plan.arrange.sell", row.Resale), () => Shop.RequestFurnitureSale(id));
            var confirm = NewBox(line, "es-plan-confirm es-plan-confirm--inline", OwnedName(id) + "-confirm");
            NewLabel(confirm, null, "es-plan-confirm__text", Strings.Format("plan.arrange.sellAsk", row.Resale));
            var buttons = NewBox(confirm, "es-plan-confirm__buttons");
            NewButton(buttons, KeepName(id), "es-button--secondary es-button--compact", Strings.Get("plan.arrange.keepIt"), () => Shop.CancelFurnitureSale());
            NewButton(buttons, SellNowName(id), "es-button--danger es-button--compact", Strings.Get("plan.arrange.sellNow"), () => Shop.ConfirmFurnitureSale());
        }
    }
}
