using System;

namespace EvolutionShop
{
    // AD-E prototype (AD.6): tank themes. A display takes the theme of the nearest decor on the store side within
    // ShopThemes.Radius of its centre; derived from the layout, never saved.
    public sealed partial class ShopLayout
    {
        /// <summary>The centre of the display holding this tank slot, on the shop floor.</summary>
        public bool TryGetDisplayCentre(string slot, out float x, out float z)
        {
            x = z = 0;
            if (slot == "tank-a") { x = 5.5f; z = -1.2f; return true; }
            if (slot == "tank-b") { x = 5.5f; z = -3.85f; return true; }
            foreach (var item in items)
                if (!item.Stored && Array.IndexOf(GetSlotIds(item.Id), slot) >= 0)
                { x = item.X * GridSize; z = item.Z * GridSize; return true; }
            return false;
        }

        /// <summary>The display's theme and the decor that sets it (null for none).</summary>
        public ShopTheme ThemeOf(string slot, out string decorId)
        {
            decorId = null;
            if (!TryGetDisplayCentre(slot, out float x, out float z)) return ShopTheme.None;
            var best = ShopTheme.None;
            float bestDistance = float.MaxValue;
            foreach (var item in items)
            {
                var theme = ShopThemes.OfDecor(item.DefinitionId);
                if (item.Stored || theme == ShopTheme.None || Footprint(item).MaxZ >= .39f) continue;
                float dx = item.X * GridSize - x, dz = item.Z * GridSize - z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance > ShopThemes.Radius) continue;
                if (distance < bestDistance - 1e-4f || Math.Abs(distance - bestDistance) <= 1e-4f && theme < best)
                { best = theme; bestDistance = distance; decorId = item.Id; }
            }
            return best;
        }

        public ShopTheme ThemeOf(string slot) => ThemeOf(slot, out _);
    }
}
