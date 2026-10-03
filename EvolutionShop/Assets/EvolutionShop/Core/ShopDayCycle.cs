using System;

namespace EvolutionShop
{
    public enum ShopDayPhase { Night = 0, Open = 1, Closing = 2 }

    [Serializable]
    public sealed class ShopDaySave
    {
        public int version = 1;
        public int dayNumber;
        public ShopDayPhase phase;
        public float secondsRemaining;
    }

    /// <summary>Owns the day clock; callers advance it only while gameplay is unpaused.</summary>
    public sealed class ShopDayCycle
    {
        public const float DefaultTradingSeconds = 480f;
        public const float DefaultClosingSeconds = 3f;

        private readonly float tradingSeconds;
        private readonly float closingSeconds;

        public int DayNumber { get; private set; }
        public ShopDayPhase Phase { get; private set; }
        public float SecondsRemaining { get; private set; }

        public ShopDayCycle(float tradingSeconds = DefaultTradingSeconds,
            float closingSeconds = DefaultClosingSeconds)
        {
            if (!PositiveFinite(tradingSeconds)) throw new ArgumentOutOfRangeException(nameof(tradingSeconds));
            if (!PositiveFinite(closingSeconds)) throw new ArgumentOutOfRangeException(nameof(closingSeconds));
            this.tradingSeconds = tradingSeconds;
            this.closingSeconds = closingSeconds;
            DayNumber = 1;
            Phase = ShopDayPhase.Night;
        }

        public bool OpenShop()
        {
            // Every accepted opening must be able to complete without overflowing the day counter.
            if (Phase != ShopDayPhase.Night || DayNumber == int.MaxValue) return false;
            Phase = ShopDayPhase.Open;
            SecondsRemaining = tradingSeconds;
            return true;
        }

        public bool RequestClose()
        {
            if (Phase != ShopDayPhase.Open) return false;
            Phase = ShopDayPhase.Closing;
            SecondsRemaining = closingSeconds;
            return true;
        }

        public void Tick(float seconds)
        {
            if (!PositiveFinite(seconds) || Phase == ShopDayPhase.Night) return;
            if (Phase == ShopDayPhase.Open)
            {
                if (seconds < SecondsRemaining)
                {
                    SecondsRemaining -= seconds;
                    return;
                }
                seconds -= SecondsRemaining;
                Phase = ShopDayPhase.Closing;
                SecondsRemaining = closingSeconds;
            }

            if (seconds < SecondsRemaining)
            {
                SecondsRemaining -= seconds;
                return;
            }

            // Night is untimed. Discard any excess delta instead of opening/skipping more days.
            Phase = ShopDayPhase.Night;
            SecondsRemaining = 0f;
            DayNumber++;
        }

        public ShopDaySave CaptureSave()
        {
            return new ShopDaySave
            {
                version = 1,
                dayNumber = DayNumber,
                phase = Phase,
                secondsRemaining = SecondsRemaining
            };
        }

        public static bool TryRestore(ShopDaySave save, out ShopDayCycle cycle,
            float tradingSeconds = DefaultTradingSeconds, float closingSeconds = DefaultClosingSeconds)
        {
            cycle = null;
            if (!PositiveFinite(tradingSeconds) || !PositiveFinite(closingSeconds) ||
                save == null || save.version != 1 || save.dayNumber < 1 ||
                !Finite(save.secondsRemaining)) return false;

            switch (save.phase)
            {
                case ShopDayPhase.Night:
                    if (save.secondsRemaining != 0f) return false;
                    break;
                case ShopDayPhase.Open:
                    if (save.dayNumber == int.MaxValue || save.secondsRemaining <= 0f ||
                        save.secondsRemaining > tradingSeconds) return false;
                    break;
                case ShopDayPhase.Closing:
                    if (save.dayNumber == int.MaxValue || save.secondsRemaining <= 0f ||
                        save.secondsRemaining > closingSeconds) return false;
                    break;
                default:
                    return false;
            }

            cycle = new ShopDayCycle(tradingSeconds, closingSeconds)
            {
                DayNumber = save.dayNumber,
                Phase = save.phase,
                SecondsRemaining = save.secondsRemaining
            };
            return true;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool PositiveFinite(float value) { return Finite(value) && value > 0f; }
    }
}
