using System;

namespace EvolutionShop
{
    public enum PriceReaction { None = 0, Bargain = 1, Fair = 2, Pricey = 3, TooExpensive = 4 }

    /// <summary>Exact integer price rules shared by the model, customers and presentation.</summary>
    public static class ShopPricing
    {
        /// <summary>Highest tag a species may carry. Never below its standard price or the minimum.</summary>
        public static int MaximumPrice(int standard, int minimum, int maximumPercent)
        {
            return Math.Max(Math.Max(standard, minimum), Percent(standard, maximumPercent));
        }

        /// <summary>Highest price a customer still pays. Never below the standard price.</summary>
        public static int AcceptableLimit(int standard, int tolerancePercent)
        {
            return Math.Max(standard, Percent(standard, tolerancePercent));
        }

        /// <summary>Prices at or below this limit, and below standard, read as a bargain.</summary>
        public static int BargainLimit(int standard, int bargainPercent)
        {
            return Math.Min(standard, Percent(standard, bargainPercent));
        }

        public static PriceReaction Judge(int price, int standard, int bargainPercent, int tolerancePercent)
        {
            if (price < 0 || standard < 0 || bargainPercent < 0 || tolerancePercent < 0)
                throw new ArgumentOutOfRangeException(nameof(price), "Prices and percentages cannot be negative.");
            if (price > AcceptableLimit(standard, tolerancePercent)) return PriceReaction.TooExpensive;
            if (price > standard) return PriceReaction.Pricey;
            if (price < standard && price <= BargainLimit(standard, bargainPercent)) return PriceReaction.Bargain;
            return PriceReaction.Fair;
        }

        private static int Percent(int value, int percent)
        {
            return (int)Math.Min(int.MaxValue, (long)value * percent / 100);
        }
    }
}
