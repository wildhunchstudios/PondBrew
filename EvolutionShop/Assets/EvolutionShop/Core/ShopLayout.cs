using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    public enum LayoutFailure
    {
        None, WrongPhase, InvalidModel, UnknownItem, InvalidRotation, OutOfBounds,
        OverlapsFurniture, ProtectedAccess, UnreachableAccess, LimitReached, InsufficientFunds,
        UnknownDefinition, LockedDefinition, StockedDisplay, ModelMismatch
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
        internal LayoutItemView(string id, string definitionId, int x, int z, int turns)
        { Id = id; DefinitionId = definitionId; X = x; Z = z; QuarterTurns = turns; }
    }

    [Serializable]
    public sealed class LayoutSave
    {
        public int version;
        public int storeTier;
        public int nextItemNumber;
        public LayoutItemSave[] items;
    }

    [Serializable]
    public sealed class LayoutItemSave
    {
        public string id, definitionId;
        public int x, z, quarterTurns;
    }

    /// <summary>
    /// Owns bounded furniture and store expansion, not stock or money. Preview builds an independent
    /// candidate; a successful night commit debits the authoritative GameModel once.
    /// Runtime must also preflight actual player/collider/NavMesh access before committing.
    /// </summary>
    public sealed class ShopLayout
    {
        public const int SaveVersion = 2, TablePrice = 12, MaxItems = 3, MaxOwnedItems = 6, MaxStoreTier = 2;
        public const float GridSize = .5f, TableWidth = 1.5f, TableDepth = 1f;
        public const string TableDefinitionId = "work-table";
        public const string SmallDisplayId = "small-display", LargeDisplayId = "large-display";
        private const float BodyMargin = .35f, PathStep = .25f;
        private const float MinPathZ = -5f;
        private const int PathHeight = 41;
        private float MinPathX => StoreMinX + .5f;
        private int PathWidth => 53 + 16 * StoreTier;
        private List<LayoutItemView> items = new List<LayoutItemView>();
        private int nextItemNumber = 1;
        private bool[] reachable;
        private Rect[] bodyObstacles;

        public int StoreTier { get; private set; }
        public float StoreMinX => -7f - 4f * StoreTier;
        public int NextExpansionPrice => StoreTier == 0 ? 60 : StoreTier == 1 ? 120 : 0;
        public int DisplayLimit => StoreTier + 1;
        public string[] TankIds
        {
            get
            {
                var ids = new List<string> { "tank-a", "tank-b" };
                foreach (var item in items) ids.AddRange(GetSlotIds(item.Id));
                return ids.ToArray();
            }
        }

        public static string DefinitionName(string definitionId)
            => definitionId == TableDefinitionId ? "Work table" : definitionId == SmallDisplayId ? "Small display" :
                definitionId == LargeDisplayId ? "Large display" : null;
        public static int DefinitionPrice(string definitionId)
            => definitionId == TableDefinitionId ? TablePrice : definitionId == SmallDisplayId ? 24 : definitionId == LargeDisplayId ? 40 : 0;
        public static float DefinitionWidth(string definitionId)
            => definitionId == TableDefinitionId || definitionId == SmallDisplayId ? 1.5f : definitionId == LargeDisplayId ? 2.5f : 0;
        public static float DefinitionDepth(string definitionId) => DefinitionPrice(definitionId) > 0 ? 1f : 0;
        public static int DefinitionSlotCount(string definitionId)
            => definitionId == SmallDisplayId ? 1 : definitionId == LargeDisplayId ? 2 : 0;

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
                foreach (var id in GetSlotIds(item.Id))
                    if (slot == id)
                    {
                        Point point = BrowsePoint(item, spot);
                        x = point.X; z = point.Z; return true;
                    }
            return false;
        }

        public IReadOnlyList<LayoutItemView> Items => Array.AsReadOnly(items.ToArray());
        public LayoutItemView? GetItem(string id)
        {
            foreach (var item in items) if (item.Id == id) return item;
            return null;
        }

        // These conservative visual footprints match the fixed ShopWorld stations, not just
        // their narrower interaction colliders. Coordinates are local to the shop floor.
        private static readonly Rect[] FixedFurniture =
        {
            Rect.At(-4.2f, 3.85f, 1.75f, 1.6f), Rect.At(-1.4f, 3.85f, 1.75f, 1.6f),
            Rect.At(2f, 3.85f, 2f, 1.5f), Rect.At(5.5f, -1.2f, 1.5f, 2.2f),
            Rect.At(5.5f, -3.85f, 1.5f, 2.2f), Rect.At(-5.5f, -2.2f, 1.3f, 2.4f),
            Rect.At(-5.5f, 1.9f, 1.25f, 1.75f)
        };
        private static readonly Rect[] Partition =
        { new Rect(-7, .39f, -1.6f, .61f), new Rect(1.6f, .39f, 7, .61f) };
        private static readonly Point[] AccessPoints =
        {
            new Point(0, -4.8f), new Point(0, -3.8f), // Entry and player spawn.
            new Point(-3.8f, -2.8f), new Point(-2.85f, -3.55f), // Queue.
            new Point(3.7f, -1.2f), new Point(3.05f, -1.95f),
            new Point(3.7f, -3.85f), new Point(3.05f, -3.1f), // Two browse places per tank.
            new Point(-4.2f, 2.35f), new Point(-1.4f, 2.35f), new Point(2f, 2.35f),
            new Point(-4f, 1.9f), new Point(-3.9f, -2.2f) // Lab operators and counter.
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
            if (purchase && (items.Count >= MaxOwnedItems || CountTables() >= MaxItems && definitionId == TableDefinitionId ||
                CountDisplays() >= DisplayLimit && definitionId != TableDefinitionId)) return LayoutResult.Fail(LayoutFailure.LimitReached);
            if (quarterTurns < 0 || quarterTurns > 3) return LayoutResult.Fail(LayoutFailure.InvalidRotation);
            // Bound integers before multiplying, indexing or allocating for untrusted input.
            if (!ValidGrid(x, z, StoreTier)) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
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
                    if (model.GetTank(slot).HasValue || model.GetReservation(slot).HasValue)
                        return LayoutResult.Fail(LayoutFailure.StockedDisplay);
            LayoutResult valid = TryPreview(definitionId, movingId, x, z, quarterTurns, out var candidate);
            if (!valid.Success) return valid;
            // All fallible validation/allocation precedes this debit. Moving owned furniture is free.
            string committedId = movingId ?? candidate.items[candidate.items.Count - 1].Id;
            if (movingId == null)
            {
                string[] slots = candidate.GetSlotIds(committedId);
                bool paid = slots.Length == 0 ? model.TrySpendCredits(DefinitionPrice(definitionId)) :
                    model.TryPurchaseSlots(DefinitionPrice(definitionId), slots);
                if (!paid) return LayoutResult.Fail(LayoutFailure.InsufficientFunds);
            }
            itemId = committedId;
            Publish(candidate);
            return LayoutResult.Ok();
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

        private void Publish(ShopLayout candidate)
        {
            items = candidate.items;
            nextItemNumber = candidate.nextItemNumber;
            StoreTier = candidate.StoreTier;
            reachable = candidate.reachable;
            bodyObstacles = candidate.bodyObstacles;
        }

        private ShopLayout Clone() => new ShopLayout { nextItemNumber = nextItemNumber, StoreTier = StoreTier,
            items = new List<LayoutItemView>(items) };
        private int CountTables() => items.FindAll(item => item.DefinitionId == TableDefinitionId).Count;
        private int CountDisplays() => items.Count - CountTables();
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
            var save = new LayoutSave { version = SaveVersion, storeTier = StoreTier,
                nextItemNumber = nextItemNumber, items = new LayoutItemSave[items.Count] };
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                save.items[i] = new LayoutItemSave { id = item.Id, definitionId = item.DefinitionId,
                    x = item.X, z = item.Z, quarterTurns = item.QuarterTurns };
            }
            return save;
        }

        public static bool TryRestore(LayoutSave save, out ShopLayout layout)
        {
            layout = null;
            if (save == null || (save.version != 1 && save.version != SaveVersion) || save.storeTier < 0 || save.storeTier > MaxStoreTier ||
                save.version == 1 && save.storeTier != 0 || save.items == null || save.items.Length > MaxOwnedItems ||
                save.nextItemNumber != save.items.Length + 1) return false;
            var ordered = new LayoutItemSave[save.items.Length];
            foreach (var item in save.items)
            {
                if (item == null || DefinitionPrice(item.definitionId) == 0 ||
                    save.version == 1 && item.definitionId != TableDefinitionId ||
                    item.definitionId == LargeDisplayId && save.storeTier < 1 ||
                    item.quarterTurns < 0 || item.quarterTurns > 3 || !ValidGrid(item.x, item.z, save.storeTier)) return false;
                int index = -1;
                for (int i = 0; i < ordered.Length; i++) if (item.id == ItemId(i + 1)) { index = i; break; }
                if (index < 0 || ordered[index] != null) return false;
                ordered[index] = item;
            }
            var candidate = new ShopLayout { nextItemNumber = save.nextItemNumber, StoreTier = save.storeTier };
            foreach (var item in ordered)
            {
                if (item == null) return false;
                candidate.items.Add(new LayoutItemView(item.id, item.definitionId, item.x, item.z, item.quarterTurns));
            }
            if (candidate.CountTables() > MaxItems || candidate.CountDisplays() > candidate.DisplayLimit) return false;
            if (!candidate.ValidateAll().Success) return false;
            layout = candidate;
            return true;
        }

        private LayoutResult ValidateAll()
        {
            var access = RequiredAccessPoints();
            for (int i = 0; i < items.Count; i++)
            {
                Rect table = Footprint(items[i]);
                if (table.MinX < StoreMinX + .2f || table.MaxX > 6.8f || table.MinZ < -5.4f || table.MaxZ > 5.4f ||
                    table.MinX < -6.8f && table.MaxZ > .19f ||
                    DefinitionSlotCount(items[i].DefinitionId) > 0 && table.MaxZ > .19f)
                    return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                foreach (var wall in Partition) if (table.Overlaps(wall)) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                foreach (var fixedItem in FixedFurniture) if (table.Overlaps(fixedItem)) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                for (int j = 0; j < i; j++) if (table.Overlaps(Footprint(items[j]))) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                if (table.Overlaps(MainAisle) || table.Overlaps(DoorClearance)) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
                foreach (var point in access)
                    if (table.Overlaps(Rect.At(point.X, point.Z, 1, 1))) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
            }
            BuildReachability();
            foreach (var point in access)
                if (!CanReachMainAisle(point.X, point.Z)) return LayoutResult.Fail(LayoutFailure.UnreachableAccess);
            // The native customer surface deliberately excludes the lab. Every added display's
            // approach must remain on the store side even when its footprint is rotated.
            foreach (var item in items)
                if (DefinitionSlotCount(item.DefinitionId) > 0)
                    for (int spot = 0; spot < 2; spot++)
                        if (BrowsePoint(item, spot).Z >= .05f) return LayoutResult.Fail(LayoutFailure.UnreachableAccess);
            return LayoutResult.Ok();
        }

        private List<Point> RequiredAccessPoints()
        {
            var points = new List<Point>(AccessPoints);
            for (int tier = 1; tier <= StoreTier; tier++) points.Add(new Point(-5f - tier * 4f, -2.5f));
            foreach (var item in items)
                if (DefinitionSlotCount(item.DefinitionId) > 0)
                { points.Add(BrowsePoint(item, 0)); points.Add(BrowsePoint(item, 1)); }
            return points;
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
            foreach (var rect in Partition) obstacles.Add(rect.Inflate(BodyMargin));
            // The extension is an L-shaped floor: its north edge is a wall, not extra lab.
            if (StoreTier > 0) obstacles.Add(new Rect(StoreMinX - 1f, .19f - BodyMargin, -6.8f + BodyMargin, 6f));
            foreach (var item in items) obstacles.Add(Footprint(item).Inflate(BodyMargin));
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
            if (x < StoreMinX + .2f + BodyMargin || x > 6.8f - BodyMargin || z < -5.4f + BodyMargin || z > 5.4f - BodyMargin) return false;
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
        private static bool ValidGrid(int x, int z, int tier)
            => x >= -13 - 8 * tier && x <= 13 && z >= -10 && z <= 10;
        private static string ItemId(int number) => "table-" + number;
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
