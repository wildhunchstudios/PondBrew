using System;
using System.Collections.Generic;
using System.Globalization;

namespace EvolutionShop
{
    public enum LayoutFailure
    {
        None, WrongPhase, InvalidModel, UnknownItem, InvalidRotation, OutOfBounds,
        OverlapsFurniture, ProtectedAccess, UnreachableAccess, LimitReached, InsufficientFunds,
        UnknownDefinition, LockedDefinition, StockedDisplay, ModelMismatch,
        ReservedDisplay, AlreadyStored, CurrencyOverflow, UnknownFacility
    }

    public readonly struct LayoutResult
    {
        public bool Success { get; }
        public LayoutFailure Failure { get; }
        private LayoutResult(bool success, LayoutFailure failure) { Success = success; Failure = failure; }
        internal static LayoutResult Ok() => new LayoutResult(true, LayoutFailure.None);
        internal static LayoutResult Fail(LayoutFailure failure) => new LayoutResult(false, failure);
    }

    public readonly struct LayoutItemView
    {
        public readonly string Id, DefinitionId;
        public readonly int X, Z, QuarterTurns;
        public readonly bool Stored;
        internal LayoutItemView(string id, string definitionId, int x, int z, int turns, bool stored = false)
        { Id = id; DefinitionId = definitionId; X = x; Z = z; QuarterTurns = turns; Stored = stored; }
    }

    public readonly struct FacilityView
    {
        public readonly string Id;
        // Grid offsets preserve the original non-grid anchors exactly during migration.
        public readonly int X, Z, QuarterTurns;
        public float WorldX => ShopLayout.FacilityAnchorX(Id) + X * ShopLayout.GridSize;
        public float WorldZ => ShopLayout.FacilityAnchorZ(Id) + Z * ShopLayout.GridSize;
        internal FacilityView(string id, int x, int z, int quarterTurns)
        { Id = id; X = x; Z = z; QuarterTurns = quarterTurns; }
    }

    [Serializable]
    public sealed class FacilitySave
    {
        public string id;
        public int x, z, quarterTurns;
    }

    [Serializable]
    public sealed class LayoutSave
    {
        public int version;
        public int storeTier;
        public int labTier;
        public int nextItemNumber;
        public LayoutItemSave[] items;
        public FacilitySave[] facilities;
    }

    [Serializable]
    public sealed class LayoutItemSave
    {
        public string id, definitionId;
        public int x, z, quarterTurns;
        public bool stored;
    }

    /// <summary>
    /// Owns bounded furniture and store expansion, not stock or money. Preview builds an independent
    /// candidate; a successful night commit debits the authoritative GameModel once.
    /// Runtime must also preflight actual player/collider/NavMesh access before committing.
    /// </summary>
    public sealed class ShopLayout
    {
        public const int SaveVersion = 5, TablePrice = 12, MaxItems = 3, MaxOwnedItems = 12, MaxStoreTier = 2, MaxLabTier = 2, DecorLimit = 6;
        public const float GridSize = .5f, TableWidth = 1.5f, TableDepth = 1f;
        public const string TableDefinitionId = "work-table";
        public const string SmallDisplayId = "small-display", LargeDisplayId = "large-display";
        public const string CoralPlanterId = "coral-planter", BubbleLampId = "bubble-lamp", SillyStatueId = "silly-statue";
        public const string BloomFacilityId = "lab-bloom", FlowFacilityId = "lab-flow", IncubatorFacilityId = "lab-incubator", UpgradeFacilityId = "lab-upgrade";
        private const float BodyMargin = .35f, PathStep = .25f;
        private const float MinPathZ = -5f;
        private int PathHeight => 41 + 16 * LabTier;
        private float MinPathX => StoreMinX + .5f;
        private int PathWidth => 53 + 16 * StoreTier;
        private List<LayoutItemView> items = new List<LayoutItemView>();
        private List<FacilityView> facilities = DefaultFacilities();
        private int nextItemNumber = 1;
        private bool[] reachable;
        private Rect[] bodyObstacles;

        public int StoreTier { get; private set; }
        public int LabTier { get; private set; }
        public float LabMaxZ => 5.5f + 4f * LabTier;
        public int NextLabExpansionPrice => LabTier == 0 ? 60 : LabTier == 1 ? 120 : 0;
        public float StoreMinX => -7f - 4f * StoreTier;
        public int NextExpansionPrice => StoreTier == 0 ? 60 : StoreTier == 1 ? 120 : 0;
        public int DisplayLimit => StoreTier + 1;
        public int DecorCount => items.FindAll(item => IsDecor(item.DefinitionId)).Count;
        public int StoredCount => items.FindAll(item => item.Stored).Count;
        public IReadOnlyList<FacilityView> Facilities => Array.AsReadOnly(facilities.ToArray());

        public FacilityView? GetFacility(string id)
        {
            foreach (var facility in facilities) if (facility.Id == id) return facility;
            return null;
        }
        public static bool IsFacility(string id) => FacilityName(id) != null;
        public static string FacilityName(string id)
            => id == BloomFacilityId ? "Bloom grower" : id == FlowFacilityId ? "Flow grower" :
                id == IncubatorFacilityId ? "Incubator" : id == UpgradeFacilityId ? "Siphon upgrade" : null;
        public static float FacilityWidth(string id)
            => id == IncubatorFacilityId ? 2f : IsFacility(id) ? 1.75f : 0f;
        public static float FacilityDepth(string id)
            => id == BloomFacilityId || id == FlowFacilityId ? 1.6f : id == IncubatorFacilityId ? 1.5f : id == UpgradeFacilityId ? 1.25f : 0f;
        internal static float FacilityAnchorX(string id)
            => id == BloomFacilityId ? -4.2f : id == FlowFacilityId ? -1.4f : id == IncubatorFacilityId ? 2f : -5.5f;
        internal static float FacilityAnchorZ(string id) => id == UpgradeFacilityId ? 1.9f : 3.85f;
        private static List<FacilityView> DefaultFacilities() => new List<FacilityView>
        {
            new FacilityView(BloomFacilityId, 0, 0, 0), new FacilityView(FlowFacilityId, 0, 0, 0),
            new FacilityView(IncubatorFacilityId, 0, 0, 0), new FacilityView(UpgradeFacilityId, 0, 0, 3)
        };
        public bool TryGetFacilityAccessPoint(string id, out float x, out float z)
        {
            x = z = 0;
            var facility = GetFacility(id);
            if (!facility.HasValue) return false;
            Point point = FacilityAccessPoint(facility.Value);
            x = point.X; z = point.Z;
            return true;
        }
        // A tiny derived mask avoids allocations when the HUD/customer cache polls appeal.
        // Moving furniture changes this value; it is never separately persisted.
        public int AppealPoints
        {
            get
            {
                int mask = 0;
                foreach (var item in items)
                {
                    if (item.Stored || Footprint(item).MaxZ >= .39f) continue;
                    if (item.DefinitionId == CoralPlanterId) mask |= 1;
                    else if (item.DefinitionId == BubbleLampId) mask |= 2;
                    else if (item.DefinitionId == SillyStatueId) mask |= 4;
                }
                int unique = (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
                return unique * ShopAppeal.PointsPerDefinition;
            }
        }
        public string[] TankIds
        {
            get
            {
                var ids = new List<string> { "tank-a", "tank-b" };
                foreach (var item in items) if (!item.Stored) ids.AddRange(GetSlotIds(item.Id));
                return ids.ToArray();
            }
        }

        public static string DefinitionName(string definitionId)
            => definitionId == TableDefinitionId ? "Work table" : definitionId == SmallDisplayId ? "Small display" :
                definitionId == LargeDisplayId ? "Large display" : definitionId == CoralPlanterId ? "Coral planter" :
                definitionId == BubbleLampId ? "Bubble lamp" : definitionId == SillyStatueId ? "Silly fish statue" : null;
        public static int DefinitionPrice(string definitionId)
            => definitionId == TableDefinitionId ? TablePrice : definitionId == SmallDisplayId ? 24 : definitionId == LargeDisplayId ? 40 :
                definitionId == CoralPlanterId ? 16 : definitionId == BubbleLampId ? 24 : definitionId == SillyStatueId ? 32 : 0;
        public static int DefinitionResalePrice(string definitionId) => DefinitionPrice(definitionId) / 2;
        public static float DefinitionWidth(string definitionId)
            => definitionId == TableDefinitionId || definitionId == SmallDisplayId ? 1.5f : definitionId == LargeDisplayId ? 2.5f :
                IsDecor(definitionId) ? 1f : 0;
        public static float DefinitionDepth(string definitionId) => DefinitionPrice(definitionId) > 0 ? 1f : 0;
        public static int DefinitionSlotCount(string definitionId)
            => definitionId == SmallDisplayId ? 1 : definitionId == LargeDisplayId ? 2 : 0;
        public static bool IsDecor(string definitionId)
            => definitionId == CoralPlanterId || definitionId == BubbleLampId || definitionId == SillyStatueId;

        public string[] GetSlotIds(string itemId)
        {
            var item = GetItem(itemId);
            int count = item.HasValue ? DefinitionSlotCount(item.Value.DefinitionId) : 0;
            var ids = new string[count];
            for (int i = 0; i < count; i++) ids[i] = itemId + "-slot-" + (i + 1);
            return ids;
        }

        public GameConfig WithTankIds(GameConfig config)
        {
            var copy = GameModel.CopyAndValidate(config);
            copy.TankIds = TankIds;
            return copy;
        }

        public bool TryGetBrowsePoint(string slot, int spot, out float x, out float z)
        {
            x = z = 0;
            if (spot < 0 || spot > 1) return false;
            if (slot == "tank-a" || slot == "tank-b")
            {
                Point point = AccessPoints[4 + (slot == "tank-b" ? 2 : 0) + spot];
                x = point.X; z = point.Z; return true;
            }
            foreach (var item in items)
                if (!item.Stored)
                foreach (var id in GetSlotIds(item.Id))
                    if (slot == id)
                    {
                        Point point = BrowsePoint(item, spot);
                        x = point.X; z = point.Z; return true;
                    }
            return false;
        }

        public IReadOnlyList<LayoutItemView> Items => Array.AsReadOnly(items.ToArray());
        public IReadOnlyList<LayoutItemView> PlacedItems => Array.AsReadOnly(items.FindAll(item => !item.Stored).ToArray());
        public LayoutItemView? GetItem(string id)
        {
            foreach (var item in items) if (item.Id == id) return item;
            return null;
        }

        // These conservative visual footprints match the fixed ShopWorld stations, not just
        // their narrower interaction colliders. Coordinates are local to the shop floor.
        private static readonly Rect[] FixedFurniture =
        {
            Rect.At(5.5f, -1.2f, 1.5f, 2.2f), Rect.At(5.5f, -3.85f, 1.5f, 2.2f),
            Rect.At(-5.5f, -2.2f, 1.3f, 2.4f)
        };
        private static readonly Rect[] Partition =
        { new Rect(-7, .39f, -1.6f, .61f), new Rect(1.6f, .39f, 7, .61f) };
        private static readonly Point[] AccessPoints =
        {
            new Point(0, -4.8f), new Point(0, -3.8f), // Entry and player spawn.
            new Point(-3.8f, -2.8f), new Point(-2.85f, -3.55f), // Queue.
            new Point(3.7f, -1.2f), new Point(3.05f, -1.95f),
            new Point(3.7f, -3.85f), new Point(3.05f, -3.1f), // Two browse places per tank.
            new Point(-3.9f, -2.2f) // Counter; lab operator points follow their facility.
        };
        private static readonly Rect MainAisle = new Rect(-.6f, -5.2f, .6f, 2.4f);
        private static readonly Rect DoorClearance = new Rect(-1.6f, .05f, 1.6f, .95f);

        public LayoutResult ValidatePlacement(string movingId, int x, int z, int quarterTurns)
            => TryPreview(movingId, x, z, quarterTurns, out _);

        public LayoutResult ValidatePlacement(string definitionId, string movingId, int x, int z, int quarterTurns)
            => TryPreview(definitionId, movingId, x, z, quarterTurns, out _);

        public LayoutResult TryPreview(string movingId, int x, int z, int quarterTurns, out ShopLayout candidate)
            => TryPreview(movingId == null ? TableDefinitionId : GetItem(movingId)?.DefinitionId,
                movingId, x, z, quarterTurns, out candidate);

        public LayoutResult TryPreview(string definitionId, string movingId, int x, int z, int quarterTurns, out ShopLayout candidate)
        {
            candidate = null;
            bool purchase = movingId == null;
            if (!purchase && !GetItem(movingId).HasValue) return LayoutResult.Fail(LayoutFailure.UnknownItem);
            if (DefinitionPrice(definitionId) == 0) return LayoutResult.Fail(LayoutFailure.UnknownDefinition);
            if (!purchase && GetItem(movingId).Value.DefinitionId != definitionId) return LayoutResult.Fail(LayoutFailure.UnknownDefinition);
            if (definitionId == LargeDisplayId && StoreTier < 1) return LayoutResult.Fail(LayoutFailure.LockedDefinition);
            if (purchase && (nextItemNumber == int.MaxValue || items.Count >= MaxOwnedItems || CountTables() >= MaxItems && definitionId == TableDefinitionId ||
                CountDisplays() >= DisplayLimit && DefinitionSlotCount(definitionId) > 0 ||
                DecorCount >= DecorLimit && IsDecor(definitionId))) return LayoutResult.Fail(LayoutFailure.LimitReached);
            if (quarterTurns < 0 || quarterTurns > 3) return LayoutResult.Fail(LayoutFailure.InvalidRotation);
            // Bound integers before multiplying, indexing or allocating for untrusted input.
            if (!ValidGrid(x, z, StoreTier, LabTier)) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
            var proposed = Clone();
            string id = purchase ? ItemId(nextItemNumber) : movingId;
            var replacement = new LayoutItemView(id, definitionId, x, z, quarterTurns);
            if (purchase) { proposed.items.Add(replacement); proposed.nextItemNumber++; }
            else proposed.items[proposed.items.FindIndex(v => v.Id == movingId)] = replacement;
            LayoutResult valid = proposed.ValidateAll();
            if (!valid.Success) return valid;
            candidate = proposed;
            return LayoutResult.Ok();
        }

        public LayoutResult TryCommit(GameModel model, ShopDayPhase phase, string movingId,
            int x, int z, int quarterTurns, out string itemId)
            => TryCommit(model, phase, movingId == null ? TableDefinitionId : GetItem(movingId)?.DefinitionId,
                movingId, x, z, quarterTurns, out itemId);

        public LayoutResult TryCommit(GameModel model, ShopDayPhase phase, string definitionId, string movingId,
            int x, int z, int quarterTurns, out string itemId)
        {
            itemId = null;
            if (phase != ShopDayPhase.Night) return LayoutResult.Fail(LayoutFailure.WrongPhase);
            if (model == null) return LayoutResult.Fail(LayoutFailure.InvalidModel);
            if (!MatchesModel(model)) return LayoutResult.Fail(LayoutFailure.ModelMismatch);
            if (movingId != null)
                foreach (string slot in GetSlotIds(movingId))
                    if (model.GetReservation(slot).HasValue)
                        return LayoutResult.Fail(LayoutFailure.ReservedDisplay);
            LayoutResult valid = TryPreview(definitionId, movingId, x, z, quarterTurns, out var candidate);
            if (!valid.Success) return valid;
            // All fallible validation/allocation precedes this debit. Moving owned furniture is free.
            string committedId = movingId ?? candidate.items[candidate.items.Count - 1].Id;
            var changed = model.TryApplyLayoutSlots(candidate.TankIds, movingId == null ? -DefinitionPrice(definitionId) : 0);
            if (!changed.Success) return LayoutResult.Fail(LayoutFailureFor(changed.Failure));
            itemId = committedId;
            Publish(candidate);
            return LayoutResult.Ok();
        }

        public LayoutResult TryPreviewStore(string itemId, out ShopLayout candidate)
            => PreviewRemoval(itemId, false, out candidate);
        public LayoutResult TryPreviewSell(string itemId, out ShopLayout candidate)
            => PreviewRemoval(itemId, true, out candidate);
        public LayoutResult TryStore(GameModel model, ShopDayPhase phase, string itemId)
            => CommitRemoval(model, phase, itemId, false);
        public LayoutResult TrySell(GameModel model, ShopDayPhase phase, string itemId)
            => CommitRemoval(model, phase, itemId, true);

        private LayoutResult PreviewRemoval(string itemId, bool selling, out ShopLayout candidate)
        {
            candidate = null;
            var owned = GetItem(itemId);
            if (!owned.HasValue) return LayoutResult.Fail(LayoutFailure.UnknownItem);
            if (!selling && owned.Value.Stored) return LayoutResult.Fail(LayoutFailure.AlreadyStored);
            var proposed = Clone();
            int index = proposed.items.FindIndex(item => item.Id == itemId);
            if (selling) proposed.items.RemoveAt(index);
            else
            {
                var item = owned.Value;
                proposed.items[index] = new LayoutItemView(item.Id, item.DefinitionId, item.X, item.Z, item.QuarterTurns, true);
            }
            var valid = proposed.ValidateAll();
            if (!valid.Success) return valid;
            candidate = proposed;
            return LayoutResult.Ok();
        }

        private LayoutResult CommitRemoval(GameModel model, ShopDayPhase phase, string itemId, bool selling)
        {
            if (phase != ShopDayPhase.Night) return LayoutResult.Fail(LayoutFailure.WrongPhase);
            if (model == null) return LayoutResult.Fail(LayoutFailure.InvalidModel);
            if (!MatchesModel(model)) return LayoutResult.Fail(LayoutFailure.ModelMismatch);
            var valid = PreviewRemoval(itemId, selling, out var candidate);
            if (!valid.Success) return valid;
            foreach (string slot in GetSlotIds(itemId))
            {
                if (model.GetReservation(slot).HasValue) return LayoutResult.Fail(LayoutFailure.ReservedDisplay);
                if (model.GetTank(slot).HasValue) return LayoutResult.Fail(LayoutFailure.StockedDisplay);
            }
            int refund = selling ? DefinitionResalePrice(GetItem(itemId).Value.DefinitionId) : 0;
            var changed = model.TryApplyLayoutSlots(candidate.TankIds, refund);
            if (!changed.Success) return LayoutResult.Fail(LayoutFailureFor(changed.Failure));
            Publish(candidate);
            return LayoutResult.Ok();
        }

        private static LayoutFailure LayoutFailureFor(FailureCode failure)
        {
            switch (failure)
            {
                case FailureCode.InsufficientFunds: return LayoutFailure.InsufficientFunds;
                case FailureCode.CurrencyOverflow: return LayoutFailure.CurrencyOverflow;
                case FailureCode.TankReserved: return LayoutFailure.ReservedDisplay;
                case FailureCode.TankFull: return LayoutFailure.StockedDisplay;
                default: return LayoutFailure.ModelMismatch;
            }
        }

        public LayoutResult TryPreviewExpansion(out ShopLayout candidate)
        {
            candidate = null;
            if (StoreTier >= MaxStoreTier) return LayoutResult.Fail(LayoutFailure.LimitReached);
            var proposed = Clone();
            proposed.StoreTier++;
            LayoutResult valid = proposed.ValidateAll();
            if (!valid.Success) return valid;
            candidate = proposed;
            return LayoutResult.Ok();
        }

        public LayoutResult TryExpand(GameModel model, ShopDayPhase phase)
        {
            if (phase != ShopDayPhase.Night) return LayoutResult.Fail(LayoutFailure.WrongPhase);
            if (model == null) return LayoutResult.Fail(LayoutFailure.InvalidModel);
            if (!MatchesModel(model)) return LayoutResult.Fail(LayoutFailure.ModelMismatch);
            LayoutResult valid = TryPreviewExpansion(out var candidate);
            if (!valid.Success) return valid;
            if (!model.TrySpendCredits(NextExpansionPrice)) return LayoutResult.Fail(LayoutFailure.InsufficientFunds);
            Publish(candidate);
            return LayoutResult.Ok();
        }

        public LayoutResult TryPreviewLabExpansion(out ShopLayout candidate)
        {
            candidate = null;
            if (LabTier >= MaxLabTier) return LayoutResult.Fail(LayoutFailure.LimitReached);
            var proposed = Clone();
            proposed.LabTier++;
            LayoutResult valid = proposed.ValidateAll();
            if (!valid.Success) return valid;
            candidate = proposed;
            return LayoutResult.Ok();
        }

        public LayoutResult TryExpandLab(GameModel model, ShopDayPhase phase)
        {
            if (phase != ShopDayPhase.Night) return LayoutResult.Fail(LayoutFailure.WrongPhase);
            if (model == null) return LayoutResult.Fail(LayoutFailure.InvalidModel);
            if (!MatchesModel(model)) return LayoutResult.Fail(LayoutFailure.ModelMismatch);
            LayoutResult valid = TryPreviewLabExpansion(out var candidate);
            if (!valid.Success) return valid;
            if (!model.TrySpendCredits(NextLabExpansionPrice)) return LayoutResult.Fail(LayoutFailure.InsufficientFunds);
            Publish(candidate);
            return LayoutResult.Ok();
        }

        public LayoutResult TryPreviewFacility(string id, int x, int z, int quarterTurns, out ShopLayout candidate)
        {
            candidate = null;
            if (!IsFacility(id)) return LayoutResult.Fail(LayoutFailure.UnknownFacility);
            if (quarterTurns < 0 || quarterTurns > 3) return LayoutResult.Fail(LayoutFailure.InvalidRotation);
            if (!ValidFacilityOffsets(x, z)) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
            var proposed = Clone();
            int index = proposed.facilities.FindIndex(facility => facility.Id == id);
            proposed.facilities[index] = new FacilityView(id, x, z, quarterTurns);
            LayoutResult valid = proposed.ValidateAll();
            if (!valid.Success) return valid;
            candidate = proposed;
            return LayoutResult.Ok();
        }

        public LayoutResult TryMoveFacility(GameModel model, ShopDayPhase phase, string id, int x, int z, int quarterTurns)
        {
            if (phase != ShopDayPhase.Night) return LayoutResult.Fail(LayoutFailure.WrongPhase);
            if (model == null) return LayoutResult.Fail(LayoutFailure.InvalidModel);
            if (!MatchesModel(model)) return LayoutResult.Fail(LayoutFailure.ModelMismatch);
            LayoutResult valid = TryPreviewFacility(id, x, z, quarterTurns, out var candidate);
            if (!valid.Success) return valid;
            // Production lives in GameModel; relocating its one presentation never recreates or resets it.
            Publish(candidate);
            return LayoutResult.Ok();
        }

        private void Publish(ShopLayout candidate)
        {
            items = candidate.items;
            facilities = candidate.facilities;
            nextItemNumber = candidate.nextItemNumber;
            StoreTier = candidate.StoreTier;
            LabTier = candidate.LabTier;
            reachable = candidate.reachable;
            bodyObstacles = candidate.bodyObstacles;
        }

        private ShopLayout Clone() => new ShopLayout { nextItemNumber = nextItemNumber, StoreTier = StoreTier, LabTier = LabTier,
            items = new List<LayoutItemView>(items), facilities = new List<FacilityView>(facilities) };
        private int CountTables() => items.FindAll(item => item.DefinitionId == TableDefinitionId).Count;
        private int CountDisplays() => items.FindAll(item => DefinitionSlotCount(item.DefinitionId) > 0).Count;
        private bool MatchesModel(GameModel model)
        {
            string[] expected = TankIds;
            var actual = model.TankIds;
            if (expected.Length != actual.Length) return false;
            var ids = new HashSet<string>(actual, StringComparer.Ordinal);
            foreach (string id in expected) if (!ids.Contains(id)) return false;
            return true;
        }

        public LayoutSave CaptureSave()
        {
            var save = new LayoutSave { version = SaveVersion, storeTier = StoreTier, labTier = LabTier,
                nextItemNumber = nextItemNumber, items = new LayoutItemSave[items.Count], facilities = new FacilitySave[facilities.Count] };
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                save.items[i] = new LayoutItemSave { id = item.Id, definitionId = item.DefinitionId,
                    x = item.X, z = item.Z, quarterTurns = item.QuarterTurns, stored = item.Stored };
            }
            for (int i = 0; i < facilities.Count; i++)
            {
                var facility = facilities[i];
                save.facilities[i] = new FacilitySave { id = facility.Id, x = facility.X, z = facility.Z, quarterTurns = facility.QuarterTurns };
            }
            return save;
        }

        public static bool TryRestore(LayoutSave save, out ShopLayout layout)
        {
            layout = null;
            if (save == null || save.version < 1 || save.version > SaveVersion || save.storeTier < 0 || save.storeTier > MaxStoreTier ||
                save.labTier < 0 || save.labTier > MaxLabTier || save.version < 5 && save.labTier != 0 ||
                save.version == 1 && save.storeTier != 0 || save.items == null ||
                save.items.Length > (save.version == 1 ? MaxItems : save.version == 2 ? 6 : MaxOwnedItems) ||
                save.nextItemNumber < 1 || save.version < 4 && save.nextItemNumber != save.items.Length + 1) return false;
            var ordered = new List<LayoutItemSave>(save.items.Length);
            var seen = new HashSet<int>();
            foreach (var item in save.items)
            {
                if (item == null || DefinitionPrice(item.definitionId) == 0 ||
                    save.version == 1 && item.definitionId != TableDefinitionId ||
                    save.version < 3 && IsDecor(item.definitionId) ||
                    save.version < 4 && item.stored ||
                    item.definitionId == LargeDisplayId && save.storeTier < 1 ||
                    item.quarterTurns < 0 || item.quarterTurns > 3 || !ValidGrid(item.x, item.z, save.storeTier, save.labTier)) return false;
                if (!TryItemNumber(item.id, out int number) || number >= save.nextItemNumber || !seen.Add(number)) return false;
                ordered.Add(item);
            }
            ordered.Sort((a, b) => { TryItemNumber(a.id, out int an); TryItemNumber(b.id, out int bn); return an.CompareTo(bn); });
            var candidate = new ShopLayout { nextItemNumber = save.nextItemNumber, StoreTier = save.storeTier, LabTier = save.labTier };
            if (!TryReadFacilities(save, out candidate.facilities)) return false;
            foreach (var item in ordered)
            {
                candidate.items.Add(new LayoutItemView(item.id, item.definitionId, item.x, item.z, item.quarterTurns, item.stored));
            }
            if (candidate.CountTables() > MaxItems || candidate.CountDisplays() > candidate.DisplayLimit || candidate.DecorCount > DecorLimit) return false;
            if (!candidate.ValidateAll().Success) return false;
            layout = candidate;
            return true;
        }

        private static bool TryReadFacilities(LayoutSave save, out List<FacilityView> result)
        {
            result = DefaultFacilities();
            // Old saves have no relocation state. Exact default arrays also allow a current
            // capture to represent an old fixture without smuggling new facility poses.
            if (save.version < 5 && (save.facilities == null || save.facilities.Length == 0)) return true;
            if (save.facilities == null || save.facilities.Length != result.Count) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var facility in save.facilities)
            {
                if (facility == null || !IsFacility(facility.id) || !seen.Add(facility.id) ||
                    !ValidFacilityOffsets(facility.x, facility.z) || facility.quarterTurns < 0 || facility.quarterTurns > 3) return false;
                int index = result.FindIndex(value => value.Id == facility.id);
                if (save.version < 5 && (facility.x != 0 || facility.z != 0 || facility.quarterTurns != result[index].QuarterTurns)) return false;
                result[index] = new FacilityView(facility.id, facility.x, facility.z, facility.quarterTurns);
            }
            return true;
        }

        private LayoutResult ValidateAll()
        {
            var access = RequiredAccessPoints();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Stored) continue;
                Rect table = Footprint(items[i]);
                if (table.MinX < StoreMinX + .2f || table.MaxX > 6.8f || table.MinZ < -5.4f || table.MaxZ > LabMaxZ - .1f ||
                    table.MinX < -6.8f && table.MaxZ > .19f ||
                    DefinitionSlotCount(items[i].DefinitionId) > 0 && table.MaxZ > .19f)
                    return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                foreach (var wall in Partition) if (table.Overlaps(wall)) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                foreach (var fixedItem in FixedFurniture) if (table.Overlaps(fixedItem)) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                foreach (var facility in facilities) if (table.Overlaps(FacilityFootprint(facility))) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                for (int j = 0; j < i; j++) if (!items[j].Stored && table.Overlaps(Footprint(items[j]))) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                if (OverlapsProtectedAisle(table)) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
                foreach (var point in access)
                    if (table.Overlaps(Rect.At(point.X, point.Z, 1, 1))) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
            }
            for (int i = 0; i < facilities.Count; i++)
            {
                Rect body = FacilityFootprint(facilities[i]);
                if (body.MinX < -6.8f || body.MaxX > 6.8f || body.MinZ < .81f || body.MaxZ > LabMaxZ - .1f)
                    return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                for (int j = 0; j < i; j++)
                    if (body.Overlaps(FacilityFootprint(facilities[j]))) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                if (OverlapsProtectedAisle(body)) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
                foreach (var point in access)
                    if (body.Overlaps(Rect.At(point.X, point.Z, 1, 1))) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
                // A lab-only operator may not stand in the store across the dividing wall.
                if (FacilityAccessPoint(facilities[i]).Z < .95f) return LayoutResult.Fail(LayoutFailure.UnreachableAccess);
            }
            BuildReachability();
            foreach (var point in access)
                if (!CanReachMainAisle(point.X, point.Z)) return LayoutResult.Fail(LayoutFailure.UnreachableAccess);
            // The native customer surface deliberately excludes the lab. Every added display's
            // approach must remain on the store side even when its footprint is rotated.
            foreach (var item in items)
                if (!item.Stored && DefinitionSlotCount(item.DefinitionId) > 0)
                    for (int spot = 0; spot < 2; spot++)
                        if (BrowsePoint(item, spot).Z >= .05f) return LayoutResult.Fail(LayoutFailure.UnreachableAccess);
            return LayoutResult.Ok();
        }

        private List<Point> RequiredAccessPoints()
        {
            var points = new List<Point>(AccessPoints);
            for (int tier = 1; tier <= StoreTier; tier++) points.Add(new Point(-5f - tier * 4f, -2.5f));
            for (int tier = 1; tier <= LabTier; tier++) points.Add(new Point(.25f, 3.5f + tier * 4f));
            foreach (var facility in facilities) points.Add(FacilityAccessPoint(facility));
            foreach (var item in items)
                if (!item.Stored && DefinitionSlotCount(item.DefinitionId) > 0)
                { points.Add(BrowsePoint(item, 0)); points.Add(BrowsePoint(item, 1)); }
            return points;
        }

        private bool OverlapsProtectedAisle(Rect body)
            => body.Overlaps(MainAisle) || body.Overlaps(DoorClearance) ||
                LabTier > 0 && body.Overlaps(new Rect(-.35f, 5.4f, .85f, LabMaxZ - .3f));

        private static Point FacilityAccessPoint(FacilityView facility)
        {
            float x = 0, z = -1.5f;
            for (int turn = 0; turn < facility.QuarterTurns; turn++) { float oldX = x; x = z; z = -oldX; }
            return new Point(facility.WorldX + x, facility.WorldZ + z);
        }

        private static Point BrowsePoint(LayoutItemView item, int spot)
        {
            float x = spot == 0 ? -.45f : .45f, z = -DefinitionDepth(item.DefinitionId) / 2f - .9f;
            // Unity Y-positive quarter turns: (x,z) -> (z,-x).
            for (int turn = 0; turn < item.QuarterTurns; turn++) { float oldX = x; x = z; z = -oldX; }
            return new Point(item.X * GridSize + x, item.Z * GridSize + z);
        }

        /// <summary>Conservative player access, including the lab. Native customer navigation is checked separately at commit.</summary>
        public bool CanReachMainAisle(float worldX, float worldZ)
        {
            if (!Finite(worldX) || !Finite(worldZ)) return false;
            if (reachable == null) BuildReachability();
            if (!Walkable(worldX, worldZ)) return false;
            // Do not snap through an obstacle: require a clear link to a reached lattice node.
            int left = (int)Math.Floor((worldX - MinPathX) / PathStep);
            int bottom = (int)Math.Floor((worldZ - MinPathZ) / PathStep);
            for (int dz = 0; dz <= 1; dz++)
                for (int dx = 0; dx <= 1; dx++)
                {
                    int x = left + dx, z = bottom + dz;
                    if (x < 0 || x >= PathWidth || z < 0 || z >= PathHeight || !reachable[z * PathWidth + x]) continue;
                    if (ClearSegment(worldX, worldZ, MinPathX + x * PathStep, MinPathZ + z * PathStep)) return true;
                }
            return false;
        }

        private void BuildReachability()
        {
            var obstacles = new List<Rect>();
            foreach (var rect in FixedFurniture) obstacles.Add(rect.Inflate(BodyMargin));
            foreach (var facility in facilities) obstacles.Add(FacilityFootprint(facility).Inflate(BodyMargin));
            foreach (var rect in Partition) obstacles.Add(rect.Inflate(BodyMargin));
            // The extension is an L-shaped floor: its north edge is a wall, not extra lab.
            if (StoreTier > 0) obstacles.Add(new Rect(StoreMinX - 1f, .19f - BodyMargin, -6.8f + BodyMargin, LabMaxZ + 1f));
            foreach (var item in items) if (!item.Stored) obstacles.Add(Footprint(item).Inflate(BodyMargin));
            bodyObstacles = obstacles.ToArray();
            reachable = new bool[PathWidth * PathHeight];
            var free = new bool[reachable.Length];
            for (int z = 0; z < PathHeight; z++)
                for (int x = 0; x < PathWidth; x++) free[z * PathWidth + x] = Walkable(MinPathX + x * PathStep, MinPathZ + z * PathStep);
            int origin = 6 * PathWidth + 26 + 16 * StoreTier; // (0, -3.5), inside the protected main aisle.
            if (!free[origin]) return;
            var queue = new Queue<int>(); queue.Enqueue(origin); reachable[origin] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(), x = current % PathWidth, z = current / PathWidth;
                if (x > 0) Enqueue(current - 1);
                if (x + 1 < PathWidth) Enqueue(current + 1);
                if (z > 0) Enqueue(current - PathWidth);
                if (z + 1 < PathHeight) Enqueue(current + PathWidth);
                void Enqueue(int next)
                {
                    if (!free[next] || reachable[next]) return;
                    if (!ClearSegment(MinPathX + x * PathStep, MinPathZ + z * PathStep,
                        MinPathX + (next % PathWidth) * PathStep, MinPathZ + (next / PathWidth) * PathStep)) return;
                    reachable[next] = true; queue.Enqueue(next);
                }
            }
        }

        private bool Walkable(float x, float z)
        {
            if (x < StoreMinX + .2f + BodyMargin || x > 6.8f - BodyMargin || z < -5.4f + BodyMargin || z > LabMaxZ - .1f - BodyMargin) return false;
            foreach (var rect in bodyObstacles) if (rect.Contains(x, z)) return false;
            return true;
        }
        private bool ClearSegment(float ax, float az, float bx, float bz)
        {
            foreach (var rect in bodyObstacles) if (rect.IntersectsSegment(ax, az, bx, bz)) return false;
            return true;
        }
        private static Rect Footprint(LayoutItemView item)
            => Rect.At(item.X * GridSize, item.Z * GridSize,
                item.QuarterTurns % 2 == 0 ? DefinitionWidth(item.DefinitionId) : DefinitionDepth(item.DefinitionId),
                item.QuarterTurns % 2 == 0 ? DefinitionDepth(item.DefinitionId) : DefinitionWidth(item.DefinitionId));
        private static Rect FacilityFootprint(FacilityView facility)
            => Rect.At(facility.WorldX, facility.WorldZ,
                facility.QuarterTurns % 2 == 0 ? FacilityWidth(facility.Id) : FacilityDepth(facility.Id),
                facility.QuarterTurns % 2 == 0 ? FacilityDepth(facility.Id) : FacilityWidth(facility.Id));
        private static bool ValidGrid(int x, int z, int storeTier, int labTier)
            => x >= -13 - 8 * storeTier && x <= 13 && z >= -10 && z <= 10 + 8 * labTier;
        private static bool ValidFacilityOffsets(int x, int z)
            => x >= -32 && x <= 32 && z >= -16 && z <= 32;
        private static string ItemId(int number) => "table-" + number.ToString(CultureInfo.InvariantCulture);
        private static bool TryItemNumber(string id, out int number)
        {
            number = 0;
            return id != null && id.StartsWith("table-", StringComparison.Ordinal) &&
                int.TryParse(id.Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
                number > 0 && id == ItemId(number);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct Point
        {
            public readonly float X, Z;
            public Point(float x, float z) { X = x; Z = z; }
        }
        private readonly struct Rect
        {
            public readonly float MinX, MinZ, MaxX, MaxZ;
            public Rect(float minX, float minZ, float maxX, float maxZ)
            { MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; }
            public static Rect At(float x, float z, float width, float depth) => new Rect(x - width / 2, z - depth / 2, x + width / 2, z + depth / 2);
            public Rect Inflate(float margin) => new Rect(MinX - margin, MinZ - margin, MaxX + margin, MaxZ + margin);
            public bool Overlaps(Rect other) => MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;
            public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
            public bool IntersectsSegment(float ax, float az, float bx, float bz)
            {
                float first = 0, last = 1;
                return Clip(ax, bx - ax, MinX, MaxX, ref first, ref last) && Clip(az, bz - az, MinZ, MaxZ, ref first, ref last);
            }
            private static bool Clip(float start, float delta, float min, float max, ref float first, ref float last)
            {
                if (Math.Abs(delta) < .000001f) return start >= min && start <= max;
                float a = (min - start) / delta, b = (max - start) / delta;
                if (a > b) { float swap = a; a = b; b = swap; }
                first = Math.Max(first, a); last = Math.Min(last, b);
                return first <= last;
            }
        }
    }
}
