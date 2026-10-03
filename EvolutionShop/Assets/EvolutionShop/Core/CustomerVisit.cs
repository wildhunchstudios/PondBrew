using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    public enum CustomerPhase { Arriving, Browsing, ToCounter, Queued, Paid, Leaving, Finished }
    public enum CustomerOutcome { None, Sold, NoStock, Impatient, Closed, PathBlocked, OrderLost, TooExpensive }

    public sealed class CustomerRules
    {
        public float ArrivalDelaySeconds = 2f;
        public float NextArrivalSeconds = 6f;
        public float BrowseSeconds = 2f;
        public float PatienceSeconds = 45f;
        public float TravelTimeoutSeconds = 18f;
        public float PaidReactionSeconds = 1.5f;
        // The first customer type pays up to 150% of the standard price; 75% or less reads as a bargain.
        public int BargainPercent = 75;
        public int PriceTolerancePercent = 150;

        public int AcceptableLimit(int standard) => ShopPricing.AcceptableLimit(standard, PriceTolerancePercent);
        public PriceReaction Judge(int price, int standard) => ShopPricing.Judge(price, standard, BargainPercent, PriceTolerancePercent);
    }

    /// <summary>One transient customer visit. Navigation reports arrival; the player explicitly serves it.</summary>
    public sealed class CustomerVisit
    {
        private readonly GameModel model;
        private readonly CustomerRules rules;
        private readonly string[] tankIds;
        private readonly HashSet<string> rejectedTanks = new HashSet<string>(StringComparer.Ordinal);
        private float phaseSeconds;

        public string Id { get; }
        public CustomerPhase Phase { get; private set; }
        public CustomerOutcome Outcome { get; private set; }
        public string TargetTankId { get; private set; }
        public string SpecimenId { get; private set; }
        public string SpeciesId { get; private set; }
        /// <summary>The agreed price copied from the reservation. Repricing the display never changes it.</summary>
        public int Price { get; private set; }
        /// <summary>The latest judgement: of the agreed price, or of the tag that was refused.</summary>
        public PriceReaction Reaction { get; private set; }
        /// <summary>The species and tag behind Reaction, for captions and notices.</summary>
        public string QuotedSpeciesId { get; private set; }
        public int QuotedPrice { get; private set; }
        public int PriceRejections { get; private set; }
        public float QueueSecondsRemaining => Phase == CustomerPhase.Queued ? Math.Max(0f, phaseSeconds) : 0f;

        public CustomerVisit(GameModel model, string[] tankIds, CustomerRules rules = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.rules = CopyRules(rules ?? new CustomerRules());
            if (tankIds == null || tankIds.Length == 0) throw new ArgumentException("At least one display tank is required.", nameof(tankIds));
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tank in model.CaptureSave().tanks) known.Add(tank.tankId);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string tankId in tankIds)
                if (string.IsNullOrWhiteSpace(tankId) || !known.Contains(tankId) || !seen.Add(tankId))
                    throw new ArgumentException("Display tank IDs must be known and unique.", nameof(tankIds));

            this.tankIds = (string[])tankIds.Clone();
            Id = Guid.NewGuid().ToString("N");
            TargetTankId = this.tankIds[0];
            foreach (string tankId in this.tankIds)
            {
                if (model.GetTank(tankId).HasValue && !model.GetReservation(tankId).HasValue)
                {
                    TargetTankId = tankId;
                    break;
                }
            }
            Phase = CustomerPhase.Arriving;
            phaseSeconds = this.rules.TravelTimeoutSeconds;
        }

        public void Advance(float seconds, bool arrivedAtDestination, ShopDayPhase shopPhase)
        {
            if (!PositiveFinite(seconds) || Phase == CustomerPhase.Finished) return;
            if (shopPhase == ShopDayPhase.Night)
            {
                if (Outcome == CustomerOutcome.None) Outcome = CustomerOutcome.Closed;
                Finish();
                return;
            }
            if (shopPhase != ShopDayPhase.Open && shopPhase != ShopDayPhase.Closing) return;
            if (shopPhase == ShopDayPhase.Closing && (Phase == CustomerPhase.Arriving || Phase == CustomerPhase.Browsing))
            {
                Cancel(CustomerOutcome.Closed);
                return;
            }
            if ((Phase == CustomerPhase.ToCounter || Phase == CustomerPhase.Queued) && !OwnsExpectedReservation())
            {
                Cancel(CustomerOutcome.OrderLost);
                return;
            }

            // Discard surplus time at transitions so one update cannot browse, reserve, and pay at once.
            switch (Phase)
            {
                case CustomerPhase.Arriving:
                    if (arrivedAtDestination) SetPhase(CustomerPhase.Browsing, rules.BrowseSeconds);
                    else if (Expired(seconds)) Cancel(CustomerOutcome.PathBlocked);
                    break;
                case CustomerPhase.Browsing:
                    if (Expired(seconds)) Decide();
                    break;
                case CustomerPhase.ToCounter:
                    if (arrivedAtDestination) SetPhase(CustomerPhase.Queued, rules.PatienceSeconds);
                    else if (Expired(seconds)) Cancel(CustomerOutcome.PathBlocked);
                    break;
                case CustomerPhase.Queued:
                    if (Expired(seconds)) Cancel(CustomerOutcome.Impatient);
                    break;
                case CustomerPhase.Paid:
                    if (Expired(seconds)) SetPhase(CustomerPhase.Leaving, rules.TravelTimeoutSeconds);
                    break;
                case CustomerPhase.Leaving:
                    if (arrivedAtDestination || Expired(seconds)) Finish();
                    break;
            }
        }

        public ActionResult Serve(ShopDayPhase shopPhase)
        {
            if (Phase != CustomerPhase.Queued || (shopPhase != ShopDayPhase.Open && shopPhase != ShopDayPhase.Closing))
                return ActionResult.Fail(FailureCode.NoCustomerReady);
            var result = model.SellReservedTank(TargetTankId, Id, SpecimenId);
            if (!result.Success)
            {
                Cancel(CustomerOutcome.OrderLost);
                return result;
            }
            Outcome = CustomerOutcome.Sold;
            SetPhase(CustomerPhase.Paid, rules.PaidReactionSeconds);
            return result;
        }

        public void Cancel(CustomerOutcome reason)
        {
            if (reason == CustomerOutcome.None || reason == CustomerOutcome.Sold || !Enum.IsDefined(typeof(CustomerOutcome), reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (Phase == CustomerPhase.Finished || Phase == CustomerPhase.Leaving) return;
            ReleaseOwnReservation();
            if (Outcome != CustomerOutcome.Sold) Outcome = reason;
            SetPhase(CustomerPhase.Leaving, rules.TravelTimeoutSeconds);
        }

        /// <summary>
        /// Judge the target display's tag. The model checks the limit and copies the agreed price in
        /// the same command that reserves, so a refused tag never leaves a claim behind.
        /// </summary>
        private void Decide()
        {
            var stock = model.GetTank(TargetTankId);
            var tag = stock.HasValue ? model.GetPrice(stock.Value.SpeciesId) : null;
            int limit = tag.HasValue ? rules.AcceptableLimit(tag.Value.Standard) : int.MaxValue;
            var reserved = model.ReserveTank(TargetTankId, Id, limit);
            if (!reserved.Success)
            {
                if (reserved.Failure == FailureCode.PriceTooHigh && tag.HasValue)
                {
                    PriceRejections++;
                    rejectedTanks.Add(TargetTankId);
                    QuotedSpeciesId = tag.Value.SpeciesId;
                    QuotedPrice = tag.Value.Price;
                    Reaction = PriceReaction.TooExpensive;
                }
                // Displays this visitor refused stay refused, so two overpriced tanks end the visit.
                if (!RedirectToAvailableDisplay())
                    Cancel(PriceRejections > 0 ? CustomerOutcome.TooExpensive : CustomerOutcome.NoStock);
                return;
            }
            var reservation = model.GetReservation(TargetTankId);
            if (!reservation.HasValue || reservation.Value.CustomerId != Id)
            { Cancel(CustomerOutcome.OrderLost); return; }
            SpecimenId = reservation.Value.SpecimenId;
            SpeciesId = reservation.Value.SpeciesId;
            Price = reservation.Value.Price;
            QuotedSpeciesId = SpeciesId;
            QuotedPrice = Price;
            var agreed = model.GetPrice(SpeciesId);
            Reaction = agreed.HasValue ? rules.Judge(Price, agreed.Value.Standard) : PriceReaction.Fair;
            SetPhase(CustomerPhase.ToCounter, rules.TravelTimeoutSeconds);
        }

        private bool OwnsExpectedReservation()
        {
            var reservation = model.GetReservation(TargetTankId);
            return reservation.HasValue && reservation.Value.CustomerId == Id &&
                reservation.Value.SpecimenId == SpecimenId && reservation.Value.SpeciesId == SpeciesId && reservation.Value.Price == Price;
        }

        private bool RedirectToAvailableDisplay()
        {
            foreach (string tankId in tankIds)
            {
                if (tankId == TargetTankId || rejectedTanks.Contains(tankId) ||
                    !model.GetTank(tankId).HasValue || model.GetReservation(tankId).HasValue)
                    continue;
                TargetTankId = tankId;
                SetPhase(CustomerPhase.Arriving, rules.TravelTimeoutSeconds);
                return true;
            }
            return false;
        }

        private void ReleaseOwnReservation()
        {
            var reservation = model.GetReservation(TargetTankId);
            if (reservation.HasValue && reservation.Value.CustomerId == Id)
                model.ReleaseReservation(TargetTankId, Id);
        }

        private void Finish()
        {
            ReleaseOwnReservation();
            SetPhase(CustomerPhase.Finished, 0f);
        }

        private void SetPhase(CustomerPhase phase, float seconds) { Phase = phase; phaseSeconds = seconds; }
        private bool Expired(float seconds) { phaseSeconds = Math.Max(0f, phaseSeconds - seconds); return phaseSeconds <= 0f; }

        private static CustomerRules CopyRules(CustomerRules value)
        {
            if (!PositiveFinite(value.ArrivalDelaySeconds) || !PositiveFinite(value.NextArrivalSeconds) ||
                !PositiveFinite(value.BrowseSeconds) || !PositiveFinite(value.PatienceSeconds) ||
                !PositiveFinite(value.TravelTimeoutSeconds) || !PositiveFinite(value.PaidReactionSeconds))
                throw new ArgumentException("Customer timings must be finite and positive.", nameof(value));
            if (value.BargainPercent < 0 || value.BargainPercent > 100 ||
                value.PriceTolerancePercent < 100 || value.PriceTolerancePercent > 1000)
                throw new ArgumentException("Bargain must be 0-100% and tolerance 100-1000% of the standard price.", nameof(value));
            return new CustomerRules
            {
                ArrivalDelaySeconds = value.ArrivalDelaySeconds, NextArrivalSeconds = value.NextArrivalSeconds,
                BrowseSeconds = value.BrowseSeconds, PatienceSeconds = value.PatienceSeconds,
                TravelTimeoutSeconds = value.TravelTimeoutSeconds, PaidReactionSeconds = value.PaidReactionSeconds,
                BargainPercent = value.BargainPercent, PriceTolerancePercent = value.PriceTolerancePercent
            };
        }
        private static bool PositiveFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
