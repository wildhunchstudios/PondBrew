using System;
using System.Collections.Generic;

namespace EvolutionShop
{
    public enum CustomerPhase { Arriving, Browsing, ToCounter, Queued, Paid, Leaving, Finished }
    public enum CustomerOutcome { None, Sold, NoStock, Impatient, Closed, PathBlocked, OrderLost, TooExpensive, Slapped }

    public sealed class CustomerRules
    {
        public float ArrivalDelaySeconds = 2f;
        public float NextArrivalSeconds = 6f;
        public float BrowseSeconds = 2f;
        public float PatienceSeconds = 45f;
        public float TravelTimeoutSeconds = 18f;
        public float PaidReactionSeconds = 1.5f;
        // Visitors in the shop at once; the counter queue has a place for each.
        public int MaxVisitors = 2;
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
        private readonly CustomerArchetype archetype;
        private readonly Func<string, bool> contended;
        private readonly VisitPreference preference;
        private float phaseSeconds;

        public string Id { get; }
        /// <summary>A copy of this visitor's type; changing it cannot change the visit.</summary>
        public CustomerArchetype Archetype => archetype.Copy();
        public string ArchetypeId => archetype.Id;
        public string ArchetypeName => archetype.DisplayName;
        public bool Likes(string speciesId) => archetype.Likes(speciesId);
        public int Budget => archetype.Budget;
        /// <summary>True when the latest refused tag was above this visitor's budget, not only its tolerance.</summary>
        public bool RefusedOverBudget { get; private set; }
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
        /// <summary>AD-E prototype: the visitor's wish, or null (always null without a preference).</summary>
        public ShopWish Wish => preference?.Wish;
        /// <summary>AD-E prototype: how the agreed creature matches the wish; None until agreed, and without a wish.</summary>
        public WishMatch WishMatch { get; private set; }
        /// <summary>AD-E prototype: the agreed price was haggled down from the tag (a close match).</summary>
        public bool Haggled { get; private set; }
        /// <summary>AD-E prototype: the tag the visitor saw when it haggled, and the theme points behind the agreed limit.</summary>
        public int TagPrice { get; private set; }
        public int ThemePoints { get; private set; }
        public float QueueSecondsRemaining => Phase == CustomerPhase.Queued ? Math.Max(0f, phaseSeconds) : 0f;
        public bool CanBeSlapped => Outcome == CustomerOutcome.None &&
            (Phase == CustomerPhase.Arriving || Phase == CustomerPhase.Browsing ||
             Phase == CustomerPhase.ToCounter || Phase == CustomerPhase.Queued);

        /// <param name="archetype">The visitor's type; null means the shared rules' standard customer.</param>
        /// <param name="contended">Whether another visitor is already heading to or browsing a display.
        /// Used only to spread visitors out; claims are still settled by the model.</param>
        /// <param name="preference">AD-E prototype: a wish and tank themes; null keeps the classic visit exactly.</param>
        public CustomerVisit(GameModel model, string[] tankIds, CustomerRules rules = null,
            CustomerArchetype archetype = null, Func<string, bool> contended = null, VisitPreference preference = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.rules = CopyRules(rules ?? new CustomerRules());
            var type = archetype ?? CustomerArchetype.FromRules(this.rules);
            string problem = type.Problem(id => model.GetSpecies(id) != null);
            if (problem != null) throw new ArgumentException(problem, nameof(archetype));
            this.archetype = type.Copy();
            this.contended = contended;
            this.preference = preference;
            if (tankIds == null || tankIds.Length == 0) throw new ArgumentException("At least one display tank is required.", nameof(tankIds));
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tank in model.CaptureSave().tanks) known.Add(tank.tankId);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string tankId in tankIds)
                if (string.IsNullOrWhiteSpace(tankId) || !known.Contains(tankId) || !seen.Add(tankId))
                    throw new ArgumentException("Display tank IDs must be known and unique.", nameof(tankIds));

            this.tankIds = (string[])tankIds.Clone();
            Id = Guid.NewGuid().ToString("N");
            TargetTankId = BestDisplay(null) ?? this.tankIds[0];
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
                    if (arrivedAtDestination) SetPhase(CustomerPhase.Browsing, archetype.BrowseSeconds);
                    else if (Expired(seconds)) Cancel(CustomerOutcome.PathBlocked);
                    break;
                case CustomerPhase.Browsing:
                    if (Expired(seconds)) Decide();
                    break;
                case CustomerPhase.ToCounter:
                    if (arrivedAtDestination) SetPhase(CustomerPhase.Queued, archetype.PatienceSeconds);
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

        /// <summary>Cancel one unpaid visit; targeting, input and the shared cooldown belong to runtime.</summary>
        public bool Slap(ShopDayPhase shopPhase)
        {
            if (!CanBeSlapped || (shopPhase != ShopDayPhase.Open && shopPhase != ShopDayPhase.Closing)) return false;
            Cancel(CustomerOutcome.Slapped);
            return true;
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
            int limit = tag.HasValue ? archetype.AcceptableLimit(tag.Value.SpeciesId, tag.Value.Standard) : int.MaxValue;
            var match = WishMatch.None;
            int theme = 0;
            if (preference != null && tag.HasValue)
            {
                match = preference.Wish != null ? preference.Wish.Match(tag.Value.SpeciesId) : WishMatch.None;
                theme = preference.ThemeTolerance != null ? Math.Max(0, preference.ThemeTolerance(TargetTankId, tag.Value.SpeciesId)) : 0;
                if (theme > 0) limit = Math.Min(archetype.Budget, ShopPricing.AcceptableLimit(tag.Value.Standard, Tolerance(tag.Value.SpeciesId, theme)));
            }
            var reserved = match == WishMatch.Close
                ? model.ReserveTank(TargetTankId, Id, limit, ShopWishes.Haggled(tag.Value.Price))
                : model.ReserveTank(TargetTankId, Id, limit);
            if (!reserved.Success)
            {
                if (reserved.Failure == FailureCode.PriceTooHigh && tag.HasValue)
                {
                    PriceRejections++;
                    rejectedTanks.Add(TargetTankId);
                    QuotedSpeciesId = tag.Value.SpeciesId;
                    QuotedPrice = tag.Value.Price;
                    Reaction = PriceReaction.TooExpensive;
                    RefusedOverBudget = archetype.OverBudget(tag.Value.Price);
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
            WishMatch = match;
            Haggled = match == WishMatch.Close && tag.HasValue && Price < tag.Value.Price;
            TagPrice = tag.HasValue ? tag.Value.Price : Price;
            ThemePoints = theme;
            var agreed = model.GetPrice(SpeciesId);
            Reaction = !agreed.HasValue ? PriceReaction.Fair : theme > 0
                ? (archetype.OverBudget(Price) ? PriceReaction.TooExpensive : ShopPricing.Judge(Price, agreed.Value.Standard, archetype.BargainPercent, Tolerance(SpeciesId, theme)))
                : archetype.Judge(SpeciesId, Price, agreed.Value.Standard);
            SetPhase(CustomerPhase.ToCounter, rules.TravelTimeoutSeconds);
        }

        private int Tolerance(string speciesId, int themePoints) => Math.Min(1000, archetype.ToleranceFor(speciesId) + themePoints);

        private bool OwnsExpectedReservation()
        {
            var reservation = model.GetReservation(TargetTankId);
            return reservation.HasValue && reservation.Value.CustomerId == Id &&
                reservation.Value.SpecimenId == SpecimenId && reservation.Value.SpeciesId == SpeciesId && reservation.Value.Price == Price;
        }

        private bool RedirectToAvailableDisplay()
        {
            string next = BestDisplay(TargetTankId);
            if (next == null) return false;
            TargetTankId = next;
            SetPhase(CustomerPhase.Arriving, rules.TravelTimeoutSeconds);
            return true;
        }

        /// <summary>
        /// Stocked, unclaimed displays this visitor has not refused. Favourite species come first, then
        /// displays nobody else is heading to, then the shop's display order.
        /// </summary>
        private string BestDisplay(string exclude)
        {
            string best = null;
            int bestScore = int.MinValue;
            foreach (string tankId in tankIds)
            {
                if (tankId == exclude || rejectedTanks.Contains(tankId)) continue;
                var stock = model.GetTank(tankId);
                if (!stock.HasValue || model.GetReservation(tankId).HasValue) continue;
                int score = (archetype.Likes(stock.Value.SpeciesId) ? 2 : 0) + (contended != null && contended(tankId) ? 0 : 1);
                if (preference?.Wish != null)
                {
                    var match = preference.Wish.Match(stock.Value.SpeciesId);
                    score += match == WishMatch.Exact ? ShopWishes.ExactScore : match == WishMatch.Close ? ShopWishes.CloseScore : 0;
                }
                if (score > bestScore) { best = tankId; bestScore = score; }
            }
            return best;
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
            if (value.MaxVisitors < 1 || value.MaxVisitors > ShopTradeLedger.MaxPending)
                throw new ArgumentException("Between 1 and " + ShopTradeLedger.MaxPending + " visitors may be in the shop at once.", nameof(value));
            return new CustomerRules
            {
                ArrivalDelaySeconds = value.ArrivalDelaySeconds, NextArrivalSeconds = value.NextArrivalSeconds,
                BrowseSeconds = value.BrowseSeconds, PatienceSeconds = value.PatienceSeconds,
                TravelTimeoutSeconds = value.TravelTimeoutSeconds, PaidReactionSeconds = value.PaidReactionSeconds,
                BargainPercent = value.BargainPercent, PriceTolerancePercent = value.PriceTolerancePercent,
                MaxVisitors = value.MaxVisitors
            };
        }
        private static bool PositiveFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
