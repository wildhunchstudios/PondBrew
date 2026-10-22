using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// Up to MaxVisitors concurrent visits and the counter line. Domain rules own orders and money;
    /// this director connects each visit to its actor, browse spot and queue place.
    /// </summary>
    public sealed class ShopCustomers : MonoBehaviour
    {
        sealed class Visitor
        {
            public CustomerVisit Visit;
            public CustomerActor Actor;
            public string LastCaption;
            public int HeardRejections;
            public int BrowseSpot;
            public int QueuePlace = -1;
            // AD-E prototype: arrived during the rush, and the wish bubble over the visitor.
            public bool Rush;
            public CustomerWishBubble Bubble;
        }

        ShopSession shop;
        readonly CustomerRules rules = new CustomerRules();
        readonly List<Visitor> visitors = new List<Visitor>(); // arrival order
        readonly List<Visitor> line = new List<Visitor>();     // agreed orders, in the order they were agreed
        CustomerArchetype[] catalog, effectiveCatalog;
        ShopReputation cachedReputation;
        int cachedTier = -1, cachedAppealPoints = -1;
        bool baseEffects, cachedBaseEffects;
        // AD-E prototype: stars and tank themes adjust the catalog while the prototype is active.
        bool cachedPrototype;
        int cachedStars = -1, cachedThemes = -1, themeFrame = -1, themeSignature;
        int maxVisitors = 1;
        float untilArrival;
        bool introductoryStockUnavailable;
        public const float IntroductoryPatienceSeconds = 90f;
        public bool IntroductoryArrivalsActive => shop != null && shop.Tutorial.FirstSaleAccommodationActive;
        public int EffectiveMaxVisitors => IntroductoryArrivalsActive ? 1 : MaxVisitors;
        readonly RaycastHit[] slapObstructions = new RaycastHit[24];

        public ShopCustomerNavigation Navigation { get; private set; }
        public bool ArrivalsEnabled { get; set; } = true;
        public bool Ready => Navigation != null && Navigation.isActiveAndEnabled && Navigation.Ready;
        public string NavigationIssue { get; private set; }
        public int ActiveCount => visitors.Count;
        /// <summary>Visitors in the shop at once, bounded by the counter queue's places.</summary>
        public int MaxVisitors
        {
            get => maxVisitors;
            set => maxVisitors = Mathf.Clamp(value, 1, ShopCustomerNavigation.QueueCapacity);
        }
        /// <summary>Validation only: pins every new visitor to one type. Normal play leaves it null.</summary>
        public string ForcedArchetypeId { get; set; }
        /// <summary>Validation only: types for the next arrivals, used before ForcedArchetypeId.</summary>
        public readonly Queue<string> ScriptedArchetypes = new Queue<string>();
        /// <summary>Copies of the customer types, for presentation; changing them changes nothing.</summary>
        public IReadOnlyList<CustomerArchetype> Archetypes
        {
            get
            {
                RefreshReputation();
                return Array.AsReadOnly(IntroductoryArrivalsActive ? new[] { IntroductoryArchetype() } : Array.ConvertAll(effectiveCatalog, a => a.Copy()));
            }
        }
        /// <summary>Validation only; normal play always applies the current shop reputation.</summary>
        public bool UseBaseReputationEffectsForValidation
        {
            get => baseEffects;
            set => baseEffects = shop != null && shop.ValidationMode && value;
        }
        public float NextArrivalSeconds => baseEffects ? rules.NextArrivalSeconds : shop.InRush ? ShopDayPlan.RushArrivalSeconds : shop.Appeal.ArrivalSeconds(shop.Reputation);
        bool holdVisits;
        /// <summary>Validation only (AD-C): every visit holds its phase and timers while set, so a reaction can be shown and read
        /// before the visit moves on; the visitors keep animating.</summary>
        public bool HoldVisitsForValidation
        {
            get => holdVisits;
            set => holdVisits = shop != null && shop.ValidationMode && value;
        }
        void RefreshReputation()
        {
            bool prototype = !baseEffects && shop.PrototypeActive;
            int stars = prototype ? ShopStars.Of(shop.Reputation.Points) : -1;
            int themes = prototype ? ThemeSignature() : -1;
            if (effectiveCatalog != null && cachedReputation == shop.Reputation && cachedTier == shop.Reputation.Tier && cachedAppealPoints == shop.Appeal.Points && cachedBaseEffects == baseEffects &&
                cachedPrototype == prototype && cachedStars == stars && cachedThemes == themes) return;
            cachedReputation = shop.Reputation; cachedTier = shop.Reputation.Tier; cachedAppealPoints = shop.Appeal.Points; cachedBaseEffects = baseEffects;
            cachedPrototype = prototype; cachedStars = stars; cachedThemes = themes;
            effectiveCatalog = baseEffects ? Array.ConvertAll(catalog, a => a.Copy()) : shop.Appeal.ApplyTo(catalog, shop.Reputation);
            if (prototype) effectiveCatalog = ShopThemes.Attract(ShopStars.ApplyTo(effectiveCatalog, stars), MatchingThemes());
        }

        /// <summary>AD-E prototype: the displays whose resident matches their theme, as a small count per theme (once a frame).</summary>
        int ThemeSignature()
        {
            if (themeFrame == Time.frameCount) return themeSignature;
            themeFrame = Time.frameCount;
            int reef = 0, glow = 0, sky = 0;
            foreach (var theme in MatchingThemes())
            {
                if (theme == ShopTheme.Reef) reef = Math.Min(3, reef + 1);
                else if (theme == ShopTheme.Glow) glow = Math.Min(3, glow + 1);
                else if (theme == ShopTheme.Sky) sky = Math.Min(3, sky + 1);
            }
            return themeSignature = reef + 4 * glow + 16 * sky;
        }

        List<ShopTheme> MatchingThemes()
        {
            var result = new List<ShopTheme>();
            if (shop.Layout == null) return result;
            foreach (string tank in shop.Model.TankIds)
            {
                var stock = shop.Model.GetTank(tank);
                if (!stock.HasValue) continue;
                var species = shop.Model.GetSpecies(stock.Value.SpeciesId);
                var theme = shop.Layout.ThemeOf(tank);
                if (species != null && ShopThemes.Matches(theme, ShopWishes.Family(species))) result.Add(theme);
            }
            return result;
        }

        /// <summary>AD-E prototype: a visitor's wish, from the species the shop has discovered.</summary>
        public ShopWish WishFor(int dayNumber, int visitorIndex, CustomerArchetype type)
        {
            if (type == null) return null;
            var discovered = new List<SpeciesDefinition>();
            foreach (string id in shop.Model.DiscoveredSpeciesIds) { var species = shop.Model.GetSpecies(id); if (species != null) discovered.Add(species); }
            return ShopWishes.Pick(dayNumber, visitorIndex, type.Id, type.FavoriteSpecies, discovered, shop.Config.Species);
        }

        /// <summary>AD-E prototype: who arrives at this place in a day, as arrivals pick them now (scripted types aside).</summary>
        public CustomerArchetype ForecastArchetype(int dayNumber, int visitorIndex)
        {
            RefreshReputation();
            return IntroductoryArrivalsActive ? IntroductoryArchetype() : CustomerArchetypes.Pick(effectiveCatalog, dayNumber, visitorIndex);
        }

        int ThemeTolerance(string tankId, string speciesId)
        {
            var species = shop.Model.GetSpecies(speciesId);
            return species == null || shop.Layout == null ? 0 : ShopThemes.ToleranceFor(shop.Layout.ThemeOf(tankId), ShopWishes.Family(species));
        }
        public float PremiumSharePercent
        {
            get
            {
                RefreshReputation();
                if (IntroductoryArrivalsActive) return 0;
                int total = 0, premium = 0;
                foreach (var type in effectiveCatalog) { total += type.Weight; if (type.Id == CustomerArchetypes.Premium) premium = type.Weight; }
                return total > 0 ? 100f * premium / total : 0;
            }
        }
        public IReadOnlyList<CustomerVisit> Visits => visitors.ConvertAll(v => v.Visit);
        /// <summary>The order the counter serves next: the first one waiting, else the next one on its way.</summary>
        public CustomerVisit NextToServe => (FirstQueued() ?? Front())?.Visit;
        /// <summary>The visitor the HUD and earlier single-visitor checks follow.</summary>
        public CustomerVisit Visit => Front()?.Visit;
        public CustomerActor Actor => Front()?.Actor;
        public int WaitingCount => line.FindAll(v => v.Visit.Phase == CustomerPhase.Queued).Count;
        public CustomerActor ActorOf(CustomerVisit visit) => Find(visit)?.Actor;
        public int QueuePlaceOf(CustomerVisit visit) => Find(visit)?.QueuePlace ?? -1;
        public int BrowseSpotOf(CustomerVisit visit) => Find(visit)?.BrowseSpot ?? -1;

        public void Initialize(ShopSession session)
        {
            shop = session;
            MaxVisitors = rules.MaxVisitors;
            catalog = CustomerArchetypes.Validate(CustomerArchetypes.CreateDefault(), id => session.Config != null &&
                Array.Exists(session.Config.Species, s => s.Id == id));
            RefreshReputation();
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

        /// <summary>How the casual visitor (the R03.1 customer) takes a tag.</summary>
        public PriceReaction Preview(PriceView tag) { RefreshReputation(); return Preview(CustomerArchetypes.Find(effectiveCatalog, CustomerArchetypes.Casual), tag); }
        public PriceReaction Preview(CustomerArchetype type, PriceView tag) =>
            type == null ? rules.Judge(tag.Price, tag.Standard) : type.Judge(tag.SpeciesId, tag.Price, tag.Standard);
        /// <summary>How many arriving customer types would buy at this tag.</summary>
        public int BuyerTypes(PriceView tag)
        {
            RefreshReputation();
            if (IntroductoryArrivalsActive) return Preview(tag) != PriceReaction.TooExpensive ? 1 : 0;
            int buyers = 0;
            foreach (var type in effectiveCatalog) if (type.Weight > 0 && type.Judge(tag.SpeciesId, tag.Price, tag.Standard) != PriceReaction.TooExpensive) buyers++;
            return buyers;
        }
        public int ArrivingTypes { get { RefreshReputation(); if (IntroductoryArrivalsActive) return 1; int count = 0; foreach (var type in effectiveCatalog) if (type.Weight > 0) count++; return count; } }

        public void Tick(float seconds)
        {
            if (!Ready || shop.Paused || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0 || holdVisits) return;
            foreach (var visitor in visitors.ToArray()) AdvanceVisitor(visitor, seconds);
            RefreshLine();
            StaggerCaptions();
            if (shop.Cycle.Phase != ShopDayPhase.Open) { untilArrival = rules.ArrivalDelaySeconds; return; }
            // The timer pauses while the shop is full, so a freed place fills NextArrivalSeconds later.
            if (!ArrivalsEnabled || visitors.Count >= EffectiveMaxVisitors) return;
            if (shop.InRush && untilArrival > ShopDayPlan.RushArrivalSeconds) untilArrival = ShopDayPlan.RushArrivalSeconds;
            untilArrival -= seconds;
            if (untilArrival > 0) return;
            // Failed introductory admission retries at the ordinary initial gap, not every frame.
            untilArrival = BeginVisit() ? NextArrivalSeconds : rules.ArrivalDelaySeconds;
        }

        /// <summary>Validation only: starts an arrival now if the shop is open and has room.</summary>
        public bool SpawnNow()
        {
            if (!Ready || shop.Paused || shop.Cycle.Phase != ShopDayPhase.Open || visitors.Count >= EffectiveMaxVisitors) return false;
            int before = visitors.Count;
            // Failed introductory admission retries at the ordinary initial gap, not every frame.
            untilArrival = BeginVisit() ? NextArrivalSeconds : rules.ArrivalDelaySeconds;
            return visitors.Count == before + 1;
        }

        void AdvanceVisitor(Visitor visitor, float seconds)
        {
            var visit = visitor.Visit;
            var previous = visit.Phase;
            string previousTarget = visit.TargetTankId;
            if (!visitor.Actor)
            {
                visit.Cancel(CustomerOutcome.PathBlocked);
                RecordOutcome(visitor);
                Remove(visitor);
                shop.Notify("A visitor could not reach the shop. Their unpaid stock is available again.");
                return;
            }
            if (visitor.Actor.PathFailed && (previous == CustomerPhase.Arriving || previous == CustomerPhase.ToCounter || previous == CustomerPhase.Leaving))
            {
                if (previous == CustomerPhase.Leaving) { EndVisit(visitor); return; }
                visit.Cancel(CustomerOutcome.PathBlocked);
            }
            // A visitor slapped at the entrance may already be at the exit. Let the brief
            // reaction finish before normal departure; night and path-failure cleanup still win.
            else visit.Advance(seconds, visitor.Actor.Arrived && !visitor.Actor.IsSlapReacting, shop.Cycle.Phase);
            if (visit.PriceRejections > visitor.HeardRejections)
            {
                visitor.HeardRejections = visit.PriceRejections;
                shop.PriceRefused(visit);
                shop.PresentCustomerPrice(visitor.Actor,FxCustomerReaction.Refused);
            }
            RecordOutcome(visitor);

            if(visit.Phase==CustomerPhase.ToCounter && previous!=CustomerPhase.ToCounter)
                shop.PresentCustomerPrice(visitor.Actor,FxCustomerReaction.Accepted);
            if (visit.Phase == CustomerPhase.Finished) { EndVisit(visitor); return; }
            if (visit.Phase != previous || visit.TargetTankId != previousTarget) ApplyDestination(visitor);
            RefreshCaption(visitor);
        }

        bool BeginVisit()
        {
            if (!Ready || shop.Paused || shop.Cycle.Phase != ShopDayPhase.Open || visitors.Count >= EffectiveMaxVisitors) return false;
            bool introductory = IntroductoryArrivalsActive;
            string[] arrivalTanks;
            if (introductory)
            {
                if (!TryIntroductoryStock(out arrivalTanks))
                { introductoryStockUnavailable = true; return false; }
            }
            else arrivalTanks = shop.Model.TankIds;
            introductoryStockUnavailable = false;
            var report = shop.Reports.Current;
            int visitorIndex = report == null ? 0 : report.visitors;
            var type = ChooseArchetype(visitorIndex);
            var entry = new Visitor();
            var preference = !introductory && shop.PrototypeActive
                ? new VisitPreference { Wish = WishFor(shop.Cycle.DayNumber, visitorIndex, type), ThemeTolerance = ThemeTolerance } : null;
            var visit = new CustomerVisit(shop.Model, arrivalTanks, rules, type, tank => Contended(tank, entry), preference);
            if (!shop.Reports.BeginVisit(visit.Id))
            {
                Debug.LogWarning("EVOLUTION_CUSTOMER_LEDGER the day report could not record a new visitor; arrival skipped.");
                return false;
            }
            entry.Visit = visit;
            entry.Actor = new GameObject("Store customer / " + type.DisplayName).AddComponent<CustomerActor>();
            entry.Actor.Initialize(Navigation, type.PresentationId);
            entry.Actor.BindCustomerPresentation(shop.World,visit.Id);
            entry.Actor.SetPaused(shop.Paused);
            entry.Rush = shop.InRush;
            if (visit.Wish != null) entry.Bubble = CustomerWishBubble.Attach(entry.Actor, visit.Wish, () => shop.ReducedVisualMotion);
            visitors.Add(entry);
            if (preference != null) shop.PrototypeArrived(visit, entry.Rush);
            shop.Notify("A " + type.DisplayName.ToLowerInvariant() + " arrived. " + (visit.Wish != null ? "They wish for " + visit.Wish.Label + "." : "They will browse your front displays."));
            ApplyDestination(entry);
            RefreshCaption(entry);
            shop.SaveNow();
            return true;
        }

        bool TryIntroductoryStock(out string[] tankIds)
        {
            // These are real model slots and current NavMesh paths, checked only when the
            // bounded arrival timer expires. Refusal still happens at normal browsing time.
            var available = new List<string>();
            foreach (string tank in shop.Model.TankIds)
            {
                if (!shop.Model.GetTank(tank).HasValue || shop.Model.GetReservation(tank).HasValue) continue;
                Vector3 browse = Navigation.BrowseSpot(tank, 0);
                if (Navigation.CanReach(Navigation.EntryPoint, browse) &&
                    Navigation.CanReach(browse, Navigation.QueueSpot(0)) &&
                    Navigation.CanReach(Navigation.QueueSpot(0), Navigation.ExitPoint)) available.Add(tank);
            }
            tankIds = available.ToArray();
            return tankIds.Length > 0;
        }

        CustomerArchetype IntroductoryArchetype()
        {
            // Find returns a copy. Prices, budgets, visuals and ordinary catalog stay intact;
            // CustomerVisit takes its own immutable copy for the lifetime of this visit.
            var type = CustomerArchetypes.Find(effectiveCatalog, CustomerArchetypes.Casual);
            if (type == null) throw new InvalidOperationException("The introductory Casual customer is missing.");
            type.PatienceSeconds = IntroductoryPatienceSeconds;
            return type;
        }

        CustomerArchetype ChooseArchetype(int visitorIndex)
        {
            RefreshReputation();
            if (IntroductoryArrivalsActive) return IntroductoryArchetype();
            string id = ScriptedArchetypes.Count > 0 ? ScriptedArchetypes.Dequeue() : ForcedArchetypeId;
            if (id != null)
                return CustomerArchetypes.Find(effectiveCatalog, id) ?? throw new InvalidOperationException("Unknown scripted customer type " + id);
            return CustomerArchetypes.Pick(effectiveCatalog, shop.Cycle.DayNumber, visitorIndex);
        }

        bool Contended(string tankId, Visitor self)
        {
            foreach (var other in visitors)
                if (other != self && other.Visit != null && other.Visit.TargetTankId == tankId &&
                    (other.Visit.Phase == CustomerPhase.Arriving || other.Visit.Phase == CustomerPhase.Browsing)) return true;
            return false;
        }

        void ApplyDestination(Visitor visitor)
        {
            if (visitor.Visit == null || !visitor.Actor) return;
            Vector3 destination;
            switch (visitor.Visit.Phase)
            {
                case CustomerPhase.Arriving:
                    visitor.BrowseSpot = ChooseBrowseSpot(visitor);
                    destination = Navigation.BrowseSpot(visitor.Visit.TargetTankId, visitor.BrowseSpot);
                    break;
                case CustomerPhase.ToCounter:
                    JoinLine(visitor);
                    destination = Navigation.QueueSpot(visitor.QueuePlace);
                    break;
                case CustomerPhase.Leaving: destination = Navigation.ExitPoint; break;
                default: return;
            }
            if (visitor.Actor.MoveTo(destination)) return;
            if (visitor.Visit.Phase == CustomerPhase.Leaving) { EndVisit(visitor); return; }
            visitor.Visit.Cancel(CustomerOutcome.PathBlocked);
            RecordOutcome(visitor);
            if (!visitor.Actor.MoveTo(Navigation.ExitPoint)) EndVisit(visitor);
        }

        /// <summary>Spot 0 at the display unless another visitor already stands or walks there.</summary>
        int ChooseBrowseSpot(Visitor visitor)
        {
            foreach (var other in visitors)
                if (other != visitor && (Navigation.BrowseSpot(other.Visit.TargetTankId, 0) - Navigation.BrowseSpot(visitor.Visit.TargetTankId, 0)).sqrMagnitude < .04f && other.BrowseSpot == 0 &&
                    (other.Visit.Phase == CustomerPhase.Arriving || other.Visit.Phase == CustomerPhase.Browsing)) return 1;
            return 0;
        }

        static bool InLine(CustomerPhase phase) => phase == CustomerPhase.ToCounter || phase == CustomerPhase.Queued || phase == CustomerPhase.Paid;

        void JoinLine(Visitor visitor)
        {
            if (!line.Contains(visitor)) line.Add(visitor);
            visitor.QueuePlace = line.IndexOf(visitor);
        }

        /// <summary>Drops finished orders from the line and walks everyone behind them one place forward.</summary>
        void RefreshLine()
        {
            line.RemoveAll(v => !visitors.Contains(v) || !InLine(v.Visit.Phase));
            foreach (var visitor in visitors)
                if (!line.Contains(visitor)) visitor.QueuePlace = -1;
            for (int place = 0; place < line.Count; place++)
            {
                var visitor = line[place];
                if (visitor.QueuePlace == place) continue;
                visitor.QueuePlace = place;
                // A paid customer finishes their reaction where they stand; the others move up.
                if (visitor.Visit.Phase == CustomerPhase.Paid || !visitor.Actor) continue;
                if (!visitor.Actor.MoveTo(Navigation.QueueSpot(place)))
                    Debug.LogWarning("EVOLUTION_CUSTOMER_QUEUE could not move a waiting customer forward: " + visitor.Actor.LastError);
            }
        }

        /// <summary>
        /// Two signs 1.6 m wide overlap when their visitors stand close together. The visitor behind
        /// (second queue place or second browse spot, else the later arrival) raises its sign above the other.
        /// </summary>
        void StaggerCaptions()
        {
            const float closeEnough = 1.8f, gap = .34f;
            foreach (var visitor in visitors) if (visitor.Actor) visitor.Actor.SetCaptionLift(0f);
            for (int i = 0; i < visitors.Count; i++)
                for (int j = i + 1; j < visitors.Count; j++)
                {
                    var first = visitors[i];
                    var second = visitors[j];
                    if (!first.Actor || !second.Actor) continue;
                    Vector3 delta = first.Actor.transform.position - second.Actor.transform.position;
                    delta.y = 0;
                    if (delta.magnitude > closeEnough) continue;
                    bool firstBehind = first.QueuePlace > second.QueuePlace && second.QueuePlace >= 0 ||
                        (first.QueuePlace < 0 && second.QueuePlace < 0 && first.BrowseSpot > second.BrowseSpot);
                    var upper = firstBehind ? first : second;
                    var lower = upper == first ? second : first;
                    upper.Actor.SetCaptionLift(Mathf.Max(0f, lower.Actor.CaptionBaseHeight + gap - upper.Actor.CaptionBaseHeight));
                }
        }

        Visitor FirstQueued()
        {
            foreach (var visitor in line) if (visitor.Visit.Phase == CustomerPhase.Queued) return visitor;
            return null;
        }

        Visitor Front()
        {
            var queued = FirstQueued();
            if (queued != null) return queued;
            foreach (var visitor in line) if (visitor.Visit.Phase == CustomerPhase.ToCounter) return visitor;
            return visitors.Count > 0 ? visitors[0] : null;
        }

        Visitor Find(CustomerVisit visit)
        {
            if (visit == null) return null;
            foreach (var visitor in visitors) if (visitor.Visit == visit) return visitor;
            return null;
        }

        /// <summary>Serves the first waiting order. With nobody waiting, reports why on the next visitor.</summary>
        public ActionResult? Serve() => Serve(FirstQueued() ?? Front());

        /// <summary>
        /// Commits only the exact queued visitor a cashier has worked on. A stale job cannot
        /// silently charge a different visitor after manual checkout, cancellation, or reload.
        /// </summary>
        public ActionResult? ServeExpected(CustomerVisit expected)
        {
            var target = Find(expected);
            if (!shop || shop.Paused || !isActiveAndEnabled || !Ready || !Navigation.isActiveAndEnabled ||
                target == null || target != FirstQueued() || expected.Phase != CustomerPhase.Queued ||
                !target.Actor || !target.Actor.isActiveAndEnabled || target.Actor.PathFailed)
                return null;
            return Serve(target);
        }

        ActionResult? Serve(Visitor target)
        {
            if (target == null) return null;
            var result = target.Visit.Serve(shop.Cycle.Phase);
            RecordOutcome(target, !result.Success);
            if (target.Visit.Phase == CustomerPhase.Leaving) ApplyDestination(target);
            RefreshLine();
            RefreshCaption(target);
            return result;
        }

        /// <summary>Aim at the nearest customer's upper body; geometry and the player are separate from stock authority.</summary>
        public CustomerVisit FindSlapTarget(Camera camera)
        {
            if (!camera || !Ready || shop.Paused ||
                (shop.Cycle.Phase != ShopDayPhase.Open && shop.Cycle.Phase != ShopDayPhase.Closing)) return null;
            Visitor closest = null;
            float nearest = float.MaxValue;
            Vector3 origin = camera.transform.position, forward = camera.transform.forward;
            foreach (var entry in visitors)
            {
                if (!entry.Actor || !entry.Actor.isActiveAndEnabled) continue;
                Vector3 delta = entry.Actor.transform.position + Vector3.up * 1.45f - origin;
                float depth = Vector3.Dot(delta, forward);
                if (depth <= 0 || delta.sqrMagnitude > SlapRules.Range * SlapRules.Range ||
                    (delta - forward * depth).sqrMagnitude > .42f * .42f || depth >= nearest) continue;
                closest = entry; nearest = depth;
            }
            // Ineligible foreground customers must not let a slap pass through to somebody behind them.
            if (closest == null || !closest.Visit.CanBeSlapped || !shop.Reports.IsPending(closest.Visit.Id)) return null;
            Vector3 offset = closest.Actor.transform.position + Vector3.up * 1.45f - origin;
            int hits = Physics.RaycastNonAlloc(origin, offset.normalized, slapObstructions, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (hits == slapObstructions.Length) return null; // Fail closed if a dense scene exhausts the probe.
            for (int i = 0; i < hits; i++)
            {
                var hit = slapObstructions[i];
                if (!hit.collider || hit.transform.IsChildOf(shop.Player.transform) || hit.transform.IsChildOf(closest.Actor.transform)) continue;
                if (hit.distance < offset.magnitude - .05f) return null;
            }
            return closest.Visit;
        }

        public bool Slap(CustomerVisit visit)
        {
            var visitor = Find(visit);
            if (shop.Panel != ShopPanel.None || shop.Paused || visitor == null || !visitor.Actor ||
                !shop.Reports.IsPending(visit.Id) || !visit.Slap(shop.Cycle.Phase)) return false;
            RecordOutcome(visitor);
            RefreshLine();
            ApplyDestination(visitor);
            if (visitor.Actor) visitor.Actor.PlaySlapReaction(shop.ReducedGags);
            RefreshCaption(visitor);
            return true;
        }

        public void SetPaused(bool paused) { foreach (var visitor in visitors) if (visitor.Actor) visitor.Actor.SetPaused(paused); }

        /// <summary>Validation only (AD-C): sends every visitor away now, each recorded as the shop closing on them, as the day's
        /// report and reputation record an ordinary visit (ResetVisitors, for reloads, leaves the day's ledger to be replaced).</summary>
        public void SendVisitorsAwayForValidation()
        {
            if (shop == null || !shop.ValidationMode) return;
            foreach (var visitor in visitors.ToArray())
            {
                if (visitor.Visit != null && visitor.Visit.Outcome == CustomerOutcome.None) visitor.Visit.Cancel(CustomerOutcome.Closed);
                RecordOutcome(visitor, false);
                Remove(visitor);
            }
            untilArrival = rules.ArrivalDelaySeconds;
        }

        public void ResetVisitors()
        {
            foreach (var visitor in visitors)
            {
                if (visitor.Visit != null) visitor.Visit.Cancel(CustomerOutcome.Closed);
                if (visitor.Actor) { visitor.Actor.gameObject.SetActive(false); Destroy(visitor.Actor.gameObject); }
            }
            visitors.Clear();
            line.Clear();
            introductoryStockUnavailable = false;
            untilArrival = rules.ArrivalDelaySeconds;
        }

        void Remove(Visitor visitor)
        {
            if (visitor.Actor) { visitor.Actor.gameObject.SetActive(false); Destroy(visitor.Actor.gameObject); }
            visitor.Actor = null;
            visitors.Remove(visitor);
            line.Remove(visitor);
            RefreshLine();
        }

        void EndVisit(Visitor visitor)
        {
            var visit = visitor.Visit;
            RecordOutcome(visitor);
            Remove(visitor);
            switch (visit.Outcome)
            {
                case CustomerOutcome.Slapped: shop.Notify("The customer left after the slap. Their unpaid creature stayed in the tank."); break;
                case CustomerOutcome.NoStock: shop.Notify("The visitor found no available creature. Stock a front display for the next customer."); break;
                case CustomerOutcome.Impatient: shop.Notify("The customer ran out of patience. Their creature is available again."); break;
                case CustomerOutcome.PathBlocked: shop.Notify("A customer could not reach their destination. Unpaid stock was released."); break;
                case CustomerOutcome.OrderLost: shop.Notify("The customer's order was cancelled. No payment was taken."); break;
                case CustomerOutcome.TooExpensive: shop.Notify("The customer left without buying: your prices were too high for them. Lower a price tag with E at the display."); break;
            }
        }

        void RecordOutcome(Visitor visitor, bool save = true)
        {
            var visit = visitor.Visit;
            if (visit == null || visit.Outcome == CustomerOutcome.None) return;
            int price = visit.Outcome == CustomerOutcome.Sold ? visit.Price : 0;
            bool resolved = shop.Reputation.ResolveVisit(shop.Reports, visit.Id, visit.Outcome, price);
            // AD-E prototype: a matched wish tips and earns its point once, with the sale (by hand or by Pip).
            if (resolved && visit.Outcome == CustomerOutcome.Sold) shop.PrototypeSold(visit, visitor.Rush);
            else if (resolved) shop.PrototypeLeft(visit);
            if (resolved && save && shop.Cycle.Phase != ShopDayPhase.Night)
                shop.SaveNow(); // Night saves once after the report is finalized with the new day clock.
        }

        void RefreshCaption(Visitor visitor)
        {
            if (visitor.Visit == null || !visitor.Actor) return;
            string caption = CaptionOf(visitor.Visit);
            if (caption == visitor.LastCaption && visitor.Actor.Phase == visitor.Visit.Phase) return;
            visitor.LastCaption = caption;
            var visit = visitor.Visit;
            bool upset = visit.Outcome == CustomerOutcome.TooExpensive ||
                (visit.Phase == CustomerPhase.Arriving && visit.PriceRejections > 0);
            visitor.Actor.Present(visit.Phase, caption, upset);
            if (visitor.Bubble) visitor.Bubble.Present(visit.Phase, visit.WishMatch, visit.Outcome);
        }

        public string Caption => CaptionOf(Visit);

        public string CaptionOf(CustomerVisit visit)
        {
            if (visit == null) return "";
            switch (visit.Phase)
            {
                case CustomerPhase.Arriving when visit.Wish != null && visit.PriceRejections == 0:
                    return "Looking for " + visit.Wish.Label;
                case CustomerPhase.ToCounter when visit.WishMatch == WishMatch.Exact:
                    return "My wish! " + visit.Price + " is fair!";
                case CustomerPhase.ToCounter when visit.WishMatch == WishMatch.Close:
                    return visit.Haggled ? "Close enough... " + visit.Price + ", not " + visit.TagPrice + "?" : "Close enough. I'll take it";
                case CustomerPhase.Paid when visit.WishMatch == WishMatch.Exact:
                    return "My wish came true! +" + ShopWishes.Tip(visit.Price) + " tip";
                case CustomerPhase.Arriving:
                    if (visit.PriceRejections == 0) return "Taking a look";
                    return visit.RefusedOverBudget ? "Over my budget! What else?" : "Too pricey! What else is here?";
                case CustomerPhase.Browsing: return "Hmm... this one?";
                case CustomerPhase.ToCounter:
                    bool favourite = visit.Likes(visit.SpeciesId);
                    switch (visit.Reaction)
                    {
                        case PriceReaction.Bargain: return "A bargain at " + visit.Price + "!";
                        case PriceReaction.Fair: return favourite ? "Just what I collect!" : "Fair price. I'll take it!";
                        case PriceReaction.Pricey: return favourite ? "Pricey, but I collect these!" : "Pricey... but I want it";
                        default: return "Ready to buy";
                    }
                case CustomerPhase.Queued: return "Checkout please!  " + Mathf.CeilToInt(visit.QueueSecondsRemaining) + "s";
                case CustomerPhase.Paid: return "Thanks! A new friend!";
                case CustomerPhase.Leaving:
                    switch (visit.Outcome)
                    {
                        case CustomerOutcome.Slapped: return "HEY! MY DIGNITY!";
                        case CustomerOutcome.Sold: return "See you next time!";
                        case CustomerOutcome.TooExpensive:
                            return visit.QuotedPrice + (visit.RefusedOverBudget ? " credits? Over my budget!" : " credits?! Too expensive!");
                        case CustomerOutcome.NoStock: return "I'll try again later";
                        default: return "Maybe another time";
                    }
                default: return "";
            }
        }

        public string Status
        {
            get
            {
                if (!string.IsNullOrEmpty(NavigationIssue)) return NavigationIssue;
                if (!Ready) return "Preparing customer routes...";
                if (visitors.Count == 0 && shop.Cycle.Phase == ShopDayPhase.Open && IntroductoryArrivalsActive && introductoryStockUnavailable)
                    return "Your first visitor needs available stock. Fill a display and keep its route clear.";
                if (visitors.Count == 0) return shop.Cycle.Phase == ShopDayPhase.Open ? "Waiting for the next visitor" : "Shop closed to new visitors";
                if (visitors.Count == 1) return StatusOf(visitors[0].Visit);
                var waiting = FirstQueued();
                if (waiting != null)
                    return "Serve the counter / " + WaitingCount + " waiting / " + Mathf.CeilToInt(waiting.Visit.QueueSecondsRemaining) + "s patience";
                return visitors.Count + " customers in the shop. " + StatusOf(Front().Visit);
            }
        }

        public string StatusOf(CustomerVisit visit)
        {
            if (visit == null) return "";
            string type = visit.ArchetypeName;
            switch (visit.Phase)
            {
                case CustomerPhase.Arriving: return visit.PriceRejections > 0 ? "A customer refused a price and is checking another display" : "A customer is walking to the displays";
                case CustomerPhase.Browsing: return "A customer is browsing your creatures";
                case CustomerPhase.ToCounter: return type + ": order at " + visit.Price + " credits (" + ShopSession.ReactionLabel(visit.Reaction) + ")";
                case CustomerPhase.Queued: return "Serve the counter / " + Mathf.CeilToInt(visit.QueueSecondsRemaining) + "s patience";
                case CustomerPhase.Paid: return "Purchase complete";
                default: return visit.Outcome == CustomerOutcome.Slapped ? "Slapped customer leaving / reputation -1" : visit.Outcome == CustomerOutcome.TooExpensive ? (visit.RefusedOverBudget ? "The customer left: over budget" : "The customer left: too expensive") : "The customer is leaving";
            }
        }

        void OnDestroy() { ResetVisitors(); }
    }
}
