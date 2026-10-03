using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    public enum LayoutFailure
    {
        None, WrongPhase, InvalidModel, UnknownItem, InvalidRotation, OutOfBounds,
        OverlapsFurniture, ProtectedAccess, UnreachableAccess, LimitReached, InsufficientFunds
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
        internal LayoutItemView(string id, int x, int z, int turns)
        { Id = id; DefinitionId = ShopLayout.TableDefinitionId; X = x; Z = z; QuarterTurns = turns; }
    }

    [Serializable]
    public sealed class LayoutSave
    {
        public int version;
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
    /// Owns the bounded work-table layout, not stock or money. Preview builds an independent
    /// candidate; a successful night commit debits the authoritative GameModel once.
    /// Runtime must also preflight actual player/collider/NavMesh access before committing.
    /// </summary>
    public sealed class ShopLayout
    {
        public const int SaveVersion = 1, TablePrice = 12, MaxItems = 3;
        public const float GridSize = .5f, TableWidth = 1.5f, TableDepth = 1f;
        public const string TableDefinitionId = "work-table";
        private const float BodyMargin = .35f, PathStep = .25f;
        private const float MinPathX = -6.5f, MinPathZ = -5f;
        private const int PathWidth = 53, PathHeight = 41;
        private List<LayoutItemView> items = new List<LayoutItemView>();
        private int nextItemNumber = 1;
        private bool[] reachable;
        private Rect[] bodyObstacles;

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

        public LayoutResult TryPreview(string movingId, int x, int z, int quarterTurns, out ShopLayout candidate)
        {
            candidate = null;
            bool purchase = movingId == null;
            if (!purchase && !GetItem(movingId).HasValue) return LayoutResult.Fail(LayoutFailure.UnknownItem);
            if (purchase && items.Count >= MaxItems) return LayoutResult.Fail(LayoutFailure.LimitReached);
            if (quarterTurns < 0 || quarterTurns > 3) return LayoutResult.Fail(LayoutFailure.InvalidRotation);
            // Bound integers before multiplying, indexing or allocating for untrusted input.
            if (x < -13 || x > 13 || z < -10 || z > 10) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
            var proposed = new ShopLayout { nextItemNumber = nextItemNumber, items = new List<LayoutItemView>(items) };
            string id = purchase ? ItemId(nextItemNumber) : movingId;
            var replacement = new LayoutItemView(id, x, z, quarterTurns);
            if (purchase) { proposed.items.Add(replacement); proposed.nextItemNumber++; }
            else proposed.items[proposed.items.FindIndex(v => v.Id == movingId)] = replacement;
            LayoutResult valid = proposed.ValidateAll();
            if (!valid.Success) return valid;
            candidate = proposed;
            return LayoutResult.Ok();
        }

        public LayoutResult TryCommit(GameModel model, ShopDayPhase phase, string movingId,
            int x, int z, int quarterTurns, out string itemId)
        {
            itemId = null;
            if (phase != ShopDayPhase.Night) return LayoutResult.Fail(LayoutFailure.WrongPhase);
            if (model == null) return LayoutResult.Fail(LayoutFailure.InvalidModel);
            LayoutResult valid = TryPreview(movingId, x, z, quarterTurns, out var candidate);
            if (!valid.Success) return valid;
            // All fallible validation/allocation precedes this debit. Moving owned furniture is free.
            string committedId = movingId ?? candidate.items[candidate.items.Count - 1].Id;
            if (movingId == null && !model.TrySpendCredits(TablePrice)) return LayoutResult.Fail(LayoutFailure.InsufficientFunds);
            itemId = committedId;
            items = candidate.items;
            nextItemNumber = candidate.nextItemNumber;
            reachable = candidate.reachable;
            bodyObstacles = candidate.bodyObstacles;
            return LayoutResult.Ok();
        }

        public LayoutSave CaptureSave()
        {
            var save = new LayoutSave { version = SaveVersion, nextItemNumber = nextItemNumber, items = new LayoutItemSave[items.Count] };
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
            if (save == null || save.version != SaveVersion || save.items == null || save.items.Length > MaxItems ||
                save.nextItemNumber != save.items.Length + 1) return false;
            var ordered = new LayoutItemSave[save.items.Length];
            foreach (var item in save.items)
            {
                if (item == null || item.definitionId != TableDefinitionId || item.quarterTurns < 0 || item.quarterTurns > 3 ||
                    item.x < -13 || item.x > 13 || item.z < -10 || item.z > 10) return false;
                int index = -1;
                for (int i = 0; i < ordered.Length; i++) if (item.id == ItemId(i + 1)) { index = i; break; }
                if (index < 0 || ordered[index] != null) return false;
                ordered[index] = item;
            }
            var candidate = new ShopLayout { nextItemNumber = save.nextItemNumber };
            foreach (var item in ordered)
            {
                if (item == null) return false;
                candidate.items.Add(new LayoutItemView(item.id, item.x, item.z, item.quarterTurns));
            }
            if (!candidate.ValidateAll().Success) return false;
            layout = candidate;
            return true;
        }

        private LayoutResult ValidateAll()
        {
            for (int i = 0; i < items.Count; i++)
            {
                Rect table = Footprint(items[i]);
                if (table.MinX < -6.8f || table.MaxX > 6.8f || table.MinZ < -5.4f || table.MaxZ > 5.4f)
                    return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                foreach (var wall in Partition) if (table.Overlaps(wall)) return LayoutResult.Fail(LayoutFailure.OutOfBounds);
                foreach (var fixedItem in FixedFurniture) if (table.Overlaps(fixedItem)) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                for (int j = 0; j < i; j++) if (table.Overlaps(Footprint(items[j]))) return LayoutResult.Fail(LayoutFailure.OverlapsFurniture);
                if (table.Overlaps(MainAisle) || table.Overlaps(DoorClearance)) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
                foreach (var point in AccessPoints)
                    if (table.Overlaps(Rect.At(point.X, point.Z, 1, 1))) return LayoutResult.Fail(LayoutFailure.ProtectedAccess);
            }
            BuildReachability();
            foreach (var point in AccessPoints)
                if (!CanReachMainAisle(point.X, point.Z)) return LayoutResult.Fail(LayoutFailure.UnreachableAccess);
            return LayoutResult.Ok();
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
            foreach (var item in items) obstacles.Add(Footprint(item).Inflate(BodyMargin));
            bodyObstacles = obstacles.ToArray();
            reachable = new bool[PathWidth * PathHeight];
            var free = new bool[reachable.Length];
            for (int z = 0; z < PathHeight; z++)
                for (int x = 0; x < PathWidth; x++) free[z * PathWidth + x] = Walkable(MinPathX + x * PathStep, MinPathZ + z * PathStep);
            int origin = 6 * PathWidth + 26; // (0, -3.5), inside the protected main aisle.
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
            if (x < -6.8f + BodyMargin || x > 6.8f - BodyMargin || z < -5.4f + BodyMargin || z > 5.4f - BodyMargin) return false;
            foreach (var rect in bodyObstacles) if (rect.Contains(x, z)) return false;
            return true;
        }
        private bool ClearSegment(float ax, float az, float bx, float bz)
        {
            foreach (var rect in bodyObstacles) if (rect.IntersectsSegment(ax, az, bx, bz)) return false;
            return true;
        }
        private static Rect Footprint(LayoutItemView item)
            => Rect.At(item.X * GridSize, item.Z * GridSize, item.QuarterTurns % 2 == 0 ? TableWidth : TableDepth,
                item.QuarterTurns % 2 == 0 ? TableDepth : TableWidth);
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
