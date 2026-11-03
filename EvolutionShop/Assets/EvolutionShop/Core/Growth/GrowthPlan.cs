using System;

namespace EvolutionShop.Growth
{
    /// <summary>
    /// GR-34 (tester round 2, F68, GR.77): every room a quarter larger each way. The plan before (the coordinates the code
    /// and the kit were written in) is mapped here, the one rule the rules, the world, the kit's rooms and the routes share:
    /// the walls, doorways and floors scale by <see cref="K"/> about the shop's centre (0, 0); a thing standing against a wall
    /// keeps its distance to it; anything else has its centre scaled with its room. Things keep their own size, so the room
    /// they gain goes to the aisles. Outside the rooms, the street keeps its distance to the shop's front.
    /// </summary>
    public static class GrowthPlan
    {
        public const float K = 1.25f;
        /// <summary>A point within this of a room's wall keeps its distance to the wall.</summary>
        public const float Hug = 0.6f;
        /// <summary>A thing whose side comes within this of a room's wall keeps its distance to the wall.</summary>
        public const float EdgeHug = 0.35f;

        /// <summary>The rooms as they were before GR-34 (x0, z0, x1, z1): unit A, unit B, unit C, the laboratory, the power
        /// room, the workshop and the research room.</summary>
        public static readonly float[][] OldRooms =
        {
            new[] { -4f, -4f, 4f, 4f }, new[] { 4f, -4f, 12f, 4f }, new[] { 2f, 4f, 12f, 9f }, new[] { -4f, 4f, 2f, 9f },
            new[] { -4f, 9f, 4f, 17f }, new[] { 4f, 9f, 12f, 15f }, new[] { -10f, 11f, -4f, 17f },
        };

        /// <summary>A length scaled with the rooms (a room's size, a doorway's width, the pipe grid's cells).</summary>
        public static float S(float v) => v * K;

        /// <summary>A point on the old plan (a wall's end, a doorway's side, a floor's corner) scaled with the rooms.</summary>
        public static (float x, float z) Scaled(float x, float z) => (x * K, z * K);

        /// <summary>
        /// Where a thing standing at (x, z) on the old plan stands now. Given its half sizes (hx, hz), it keeps its distance to
        /// a wall its side is within <see cref="EdgeHug"/> of; without them, to a wall its centre is within <see cref="Hug"/>
        /// of; otherwise its centre scales with its room.
        /// </summary>
        public static (float x, float z) At(float x, float z, float hx = -1, float hz = -1)
        {
            var room = RoomAt(x, z);
            if (room == null) return (Outside(x, -10, 12, 2.5f, 3f), Outside(z, -4, 17, 1f, 4.25f));
            return (Axis(x, room[0], room[2], hx), Axis(z, room[1], room[3], hz));
        }

        public static (float x, float z) At((float x, float z) p, float hx = -1, float hz = -1) => At(p.x, p.z, hx, hz);

        /// <summary>The old room a point is in (a point on a wall belongs to the first room that has it), or null.</summary>
        public static float[] RoomAt(float x, float z)
        {
            foreach (var r in OldRooms)
                if (x >= r[0] - 1e-3f && x <= r[2] + 1e-3f && z >= r[1] - 1e-3f && z <= r[3] + 1e-3f) return r;
            return null;
        }

        static float Axis(float v, float lo, float hi, float half)
        {
            float toLo = half >= 0 ? v - half - lo : v - lo, toHi = half >= 0 ? hi - v - half : hi - v, near = half >= 0 ? EdgeHug : Hug;
            bool byLo = toLo <= near, byHi = toHi <= near;
            if (byLo && (!byHi || toLo <= toHi)) return v + lo * (K - 1);
            if (byHi) return v + hi * (K - 1);
            return v * K;
        }

        /// <summary>Outside the rooms: beyond the shop's sides a thing keeps its distance to them; between them it scales.</summary>
        static float Outside(float v, float lo, float hi, float shiftLo, float shiftHi) => v < lo ? v - shiftLo : v > hi ? v + shiftHi : v * K;
    }
}
