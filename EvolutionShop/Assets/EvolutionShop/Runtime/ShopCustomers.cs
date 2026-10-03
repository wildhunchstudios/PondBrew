using System.Collections;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>One active visit. Domain rules own orders; this component connects them to navigation.</summary>
    public sealed class ShopCustomers : MonoBehaviour
    {
        ShopSession shop;
        readonly CustomerRules rules = new CustomerRules();
        float untilArrival;
        string lastCaption;
        int heardRejections;
        public ShopCustomerNavigation Navigation { get; private set; }
        public CustomerActor Actor { get; private set; }
        public CustomerVisit Visit { get; private set; }
        public bool ArrivalsEnabled { get; set; } = true;
        public bool Ready => Navigation != null && Navigation.Ready;
        public string NavigationIssue { get; private set; }
        public int ActiveCount => Visit == null ? 0 : 1;
        /// <summary>How this customer type would judge a tag; the visit applies the same rules.</summary>
        public PriceReaction Preview(PriceView tag) => rules.Judge(tag.Price, tag.Standard);

        public void Initialize(ShopSession session)
        {
            shop = session;
            Navigation = gameObject.AddComponent<ShopCustomerNavigation>();
            untilArrival = rules.ArrivalDelaySeconds;
            StartCoroutine(BuildAfterWorldSettles());
        }
        IEnumerator BuildAfterWorldSettles()
        {
            // Procedural visual primitives discard their colliders at the end of the creation frame.
            yield return null;
            yield return null;
            if (!Navigation.Build(shop.World))
            {
                NavigationIssue = "Customer routes are unavailable. Save, quit and reopen the game to rebuild them.";
                Debug.LogWarning("EVOLUTION_CUSTOMER_NAVIGATION " + Navigation.LastError);
                shop.Notify(NavigationIssue, 10);
            }
        }
        public void Tick(float seconds)
        {
            if (!Ready || shop.Paused || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            if (Visit == null)
            {
                if (shop.Cycle.Phase != ShopDayPhase.Open) { untilArrival = rules.ArrivalDelaySeconds; return; }
                if (!ArrivalsEnabled) return;
                untilArrival -= seconds;
                if (untilArrival <= 0) BeginVisit();
                return;
            }

            var previous = Visit.Phase;
            if (!Actor)
            {
                Visit.Cancel(CustomerOutcome.PathBlocked);
                RecordOutcome();
                ResetVisitors();
                shop.Notify("A visitor could not reach the shop. Their unpaid stock is available again.");
                return;
            }
            if (Actor.PathFailed && (previous == CustomerPhase.Arriving || previous == CustomerPhase.ToCounter || previous == CustomerPhase.Leaving))
            {
                if (previous == CustomerPhase.Leaving) { EndVisit(); return; }
                Visit.Cancel(CustomerOutcome.PathBlocked);
            }
            else Visit.Advance(seconds, Actor.Arrived, shop.Cycle.Phase);
            if (Visit.PriceRejections > heardRejections)
            {
                heardRejections = Visit.PriceRejections;
                shop.PriceRefused(Visit);
            }
            RecordOutcome();

            if (Visit.Phase == CustomerPhase.Finished) { EndVisit(); return; }
            if (Visit.Phase != previous) ApplyDestination();
            RefreshCaption();
        }
        void BeginVisit()
        {
            Visit = new CustomerVisit(shop.Model, shop.Config.TankIds, rules);
            shop.Reports.BeginVisit(Visit.Id);
            Actor = new GameObject("Store customer").AddComponent<CustomerActor>();
            Actor.Initialize(Navigation);
            Actor.SetPaused(shop.Paused);
            shop.Notify("A customer arrived. They will browse your front displays.");
            ApplyDestination();
            RefreshCaption();
            shop.SaveNow();
        }
        void ApplyDestination()
        {
            if (Visit == null || !Actor) return;
            Vector3 destination;
            switch (Visit.Phase)
            {
                case CustomerPhase.Arriving: destination = Navigation.BrowsePoint(Visit.TargetTankId); break;
                case CustomerPhase.ToCounter: destination = Navigation.QueuePoint; break;
                case CustomerPhase.Leaving: destination = Navigation.ExitPoint; break;
                default: return;
            }
            if (Actor.MoveTo(destination)) return;
            if (Visit.Phase == CustomerPhase.Leaving) { EndVisit(); return; }
            Visit.Cancel(CustomerOutcome.PathBlocked);
            RecordOutcome();
            if (!Actor.MoveTo(Navigation.ExitPoint)) EndVisit();
        }
        public ActionResult? Serve()
        {
            if (Visit == null) return null;
            var result = Visit.Serve(shop.Cycle.Phase);
            RecordOutcome(!result.Success);
            if (Visit.Phase == CustomerPhase.Leaving) ApplyDestination();
            RefreshCaption();
            return result;
        }
        public void SetPaused(bool paused) { if (Actor) Actor.SetPaused(paused); }
        public void ResetVisitors()
        {
            if (Visit != null) Visit.Cancel(CustomerOutcome.Closed);
            if (Actor) { Actor.gameObject.SetActive(false); Destroy(Actor.gameObject); }
            Actor = null; Visit = null; lastCaption = null; heardRejections = 0;
            untilArrival = rules.ArrivalDelaySeconds;
        }
        void EndVisit()
        {
            var outcome = Visit == null ? CustomerOutcome.None : Visit.Outcome;
            RecordOutcome();
            ResetVisitors();
            untilArrival = rules.NextArrivalSeconds;
            switch (outcome)
            {
                case CustomerOutcome.NoStock: shop.Notify("The visitor found no available creature. Stock a front display for the next customer."); break;
                case CustomerOutcome.Impatient: shop.Notify("The customer ran out of patience. Their creature is available again."); break;
                case CustomerOutcome.PathBlocked: shop.Notify("A customer could not reach their destination. Unpaid stock was released."); break;
                case CustomerOutcome.OrderLost: shop.Notify("The customer's order was cancelled. No payment was taken."); break;
                case CustomerOutcome.TooExpensive: shop.Notify("The customer left without buying: your prices were too high for them. Lower a price tag with E at the display."); break;
            }
        }
        void RecordOutcome(bool save = true)
        {
            if (Visit == null || Visit.Outcome == CustomerOutcome.None) return;
            int price = Visit.Outcome == CustomerOutcome.Sold ? Visit.Price : 0;
            if (shop.Reports.ResolveVisit(Visit.Id, Visit.Outcome, price) && save && shop.Cycle.Phase != ShopDayPhase.Night)
                shop.SaveNow(); // Night saves once after the report is finalized with the new day clock.
        }
        void RefreshCaption()
        {
            if (Visit == null || !Actor) return;
            string caption = Caption;
            if (caption == lastCaption) return;
            lastCaption = caption;
            bool upset = Visit.Outcome == CustomerOutcome.TooExpensive ||
                (Visit.Phase == CustomerPhase.Arriving && Visit.PriceRejections > 0);
            Actor.Present(Visit.Phase, caption, upset);
        }
        public string Caption
        {
            get
            {
                if (Visit == null) return "";
                switch (Visit.Phase)
                {
                    case CustomerPhase.Arriving: return Visit.PriceRejections > 0 ? "Too pricey! What else is here?" : "Taking a look";
                    case CustomerPhase.Browsing: return "Hmm... this one?";
                    case CustomerPhase.ToCounter:
                        switch (Visit.Reaction)
                        {
                            case PriceReaction.Bargain: return "A bargain at " + Visit.Price + "!";
                            case PriceReaction.Fair: return "Fair price. I'll take it!";
                            case PriceReaction.Pricey: return "Pricey... but I want it";
                            default: return "Ready to buy";
                        }
                    case CustomerPhase.Queued: return "Checkout please!  " + Mathf.CeilToInt(Visit.QueueSecondsRemaining) + "s";
                    case CustomerPhase.Paid: return "Thanks! A new friend!";
                    case CustomerPhase.Leaving:
                        switch (Visit.Outcome)
                        {
                            case CustomerOutcome.Sold: return "See you next time!";
                            case CustomerOutcome.TooExpensive: return Visit.QuotedPrice + " credits?! Too expensive!";
                            case CustomerOutcome.NoStock: return "I'll try again later";
                            default: return "Maybe another time";
                        }
                    default: return "";
                }
            }
        }
        public string Status
        {
            get
            {
                if (!string.IsNullOrEmpty(NavigationIssue)) return NavigationIssue;
                if (!Ready) return "Preparing customer routes...";
                if (Visit == null) return shop.Cycle.Phase == ShopDayPhase.Open ? "Waiting for the next visitor" : "Shop closed to new visitors";
                switch (Visit.Phase)
                {
                    case CustomerPhase.Arriving: return Visit.PriceRejections > 0 ? "A customer refused a price and is checking another display" : "A customer is walking to the displays";
                    case CustomerPhase.Browsing: return "A customer is browsing your creatures";
                    case CustomerPhase.ToCounter: return "Order at " + Visit.Price + " credits (" + ShopSession.ReactionLabel(Visit.Reaction) + ")";
                    case CustomerPhase.Queued: return "Serve the counter / " + Mathf.CeilToInt(Visit.QueueSecondsRemaining) + "s patience";
                    case CustomerPhase.Paid: return "Purchase complete";
                    default: return Visit.Outcome == CustomerOutcome.TooExpensive ? "The customer left: too expensive" : "The customer is leaving";
                }
            }
        }
        void OnDestroy() { ResetVisitors(); }
    }
}
