using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    public readonly struct WorkerPathPoint
    {
        public readonly float X, Z;
        public WorkerPathPoint(float x, float z) { X = x; Z = z; }
    }

    public sealed partial class ShopLayout
    {
        /// <summary>Bounded full-room worker route on the same conservative body-clearance lattice as layout validation.</summary>
        public bool TryGetWorkerPath(float fromX, float fromZ, float toX, float toZ, out WorkerPathPoint[] path)
        {
            path = Array.Empty<WorkerPathPoint>();
            if (!Finite(fromX) || !Finite(fromZ) || !Finite(toX) || !Finite(toZ)) return false;
            if (reachable == null) BuildReachability();
            if (!Walkable(fromX, fromZ) || !Walkable(toX, toZ)) return false;
            // A clear direct link still requires endpoints in the valid main-aisle component.
            if (!CanReachMainAisle(fromX, fromZ) || !CanReachMainAisle(toX, toZ)) return false;
            if (ClearSegment(fromX, fromZ, toX, toZ))
            {
                path = new[] { new WorkerPathPoint(fromX, fromZ), new WorkerPathPoint(toX, toZ) };
                return true;
            }

            int width = PathWidth, count = reachable.Length;
            var previous = new int[count];
            for (int i = 0; i < count; i++) previous[i] = -2;
            var targets = new bool[count];
            var queue = new Queue<int>();
            AddLinks(fromX, fromZ, true);
            AddLinks(toX, toZ, false);
            int end = -1;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (targets[current]) { end = current; break; }
                int x = current % width, z = current / width;
                if (x > 0) Visit(current - 1);
                if (x + 1 < width) Visit(current + 1);
                if (z > 0) Visit(current - width);
                if (z + 1 < PathHeight) Visit(current + width);
                void Visit(int next)
                {
                    if (!reachable[next] || previous[next] != -2) return;
                    if (!ClearSegment(MinPathX + x * PathStep, MinPathZ + z * PathStep,
                        MinPathX + (next % width) * PathStep, MinPathZ + (next / width) * PathStep)) return;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            if (end < 0) return false;
            var reverse = new List<WorkerPathPoint>();
            for (int current = end; current >= 0; current = previous[current])
                reverse.Add(new WorkerPathPoint(MinPathX + (current % width) * PathStep, MinPathZ + (current / width) * PathStep));
            reverse.Reverse();
            reverse.Insert(0, new WorkerPathPoint(fromX, fromZ));
            reverse.Add(new WorkerPathPoint(toX, toZ));
            // Remove only collinear intermediate nodes. Never cut across a corner or change the verified route.
            var result = new List<WorkerPathPoint> { reverse[0] };
            for (int i = 1; i + 1 < reverse.Count; i++)
            {
                WorkerPathPoint a = result[result.Count - 1], b = reverse[i], c = reverse[i + 1];
                bool collinear = Math.Abs((b.X - a.X) * (c.Z - b.Z) - (b.Z - a.Z) * (c.X - b.X)) < .000001f;
                if (!collinear || !ClearSegment(a.X, a.Z, c.X, c.Z)) result.Add(b);
            }
            result.Add(reverse[reverse.Count - 1]);
            path = result.ToArray();
            return true;

            void AddLinks(float worldX, float worldZ, bool origin)
            {
                int left = (int)Math.Floor((worldX - MinPathX) / PathStep);
                int bottom = (int)Math.Floor((worldZ - MinPathZ) / PathStep);
                for (int dz = 0; dz <= 1; dz++)
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int x = left + dx, z = bottom + dz;
                        if (x < 0 || x >= width || z < 0 || z >= PathHeight) continue;
                        int index = z * width + x;
                        if (!reachable[index] || !ClearSegment(worldX, worldZ, MinPathX + x * PathStep, MinPathZ + z * PathStep)) continue;
                        if (origin) { previous[index] = -1; queue.Enqueue(index); }
                        else targets[index] = true;
                    }
            }
        }

        /// <summary>Recheck each movement step after furniture changes. This does not modify customer navigation.</summary>
        public bool IsWorkerSegmentClear(float ax, float az, float bx, float bz)
        {
            if (!Finite(ax) || !Finite(az) || !Finite(bx) || !Finite(bz)) return false;
            if (reachable == null) BuildReachability();
            return Walkable(ax, az) && Walkable(bx, bz) && ClearSegment(ax, az, bx, bz);
        }
    }
}
