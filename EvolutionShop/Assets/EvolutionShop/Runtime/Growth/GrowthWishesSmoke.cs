#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-20's Windows route (-evolutionGrowthWishesSmoke): visitors' wishes. A day of the shop's own visitors at the wishes
    /// start point (4 stars, colours made, small and medium creatures discovered): each comes in wishing for something the
    /// shop could offer them, a bubble over their head showing it; the reticle on one reads their wish; the counter's queue
    /// says each wish, granted by the sale or not; the day closed early and its receipt. Then a day of staged visitors (made
    /// as arrivals are, labelled) wishing for a species, a colour, a size, a matching pair and a school of three: each bubble's
    /// content captured; each served at the counter, the first by holding the mouse on it: a granted wish's tip, reputation
    /// point, note, heart and chime, a wish not granted shrinking away; the receipt's wishes granted, tips and what was wished
    /// for and not granted.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string WishesScenarioText = "GR-20 visitors' wishes: a day of the shop's own visitors at the wishes start point, each wishing for something the shop could offer them with a bubble over their head showing it, the reticle on one reading their wish, the counter's queue with each wish, the receipt; staged visitors wishing for a species, a colour, a size, a matching pair and a school of three, each bubble captured; each served at the counter: a granted wish's tip, reputation point, note, heart and chime, a wish not granted shrinking away; the receipt's wishes granted, tips and what was wished for and not granted";

        /// <summary>What a wish's bubble shows (GrowthWishBubble.Content).</summary>
        static string BubbleContent(GrowthWish w) =>
            w.Kind == WishKind.Species ? "species:" + w.Species : w.Kind == WishKind.Colour ? "colour:" + w.Colour : w.Kind == WishKind.Size ? "size:" + w.Size : w.Kind == WishKind.Pair ? "pair" : "school";

        /// <summary>Whether a visitor's bubble shows their wish, over their head, turned to the camera.</summary>
        bool BubbleRight(GrowthVisitor v, out string why)
        {
            why = "";
            var view = ViewOf(v);
            if (view == null) { why = "no view"; return false; }
            var b = view.Bubble;
            if (b == null) { why = "no bubble"; return false; }
            if (!b.Shown) { why = "not shown (scale " + b.Scale.ToString("0.00") + ")"; return false; }
            if (b.Content != BubbleContent(v.Wish)) { why = "shows " + b.Content + " for " + v.Wish.Text; return false; }
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 to = cam.transform.position - b.Centre; to.y *= 0.5f;
                if (Vector3.Dot(b.BubbleTransform.forward, to.normalized) < 0.95f) { why = "not turned to the camera"; return false; }
            }
            if (view.Person != null && b.Centre.y < view.Person.Joint("head").position.y + view.Person.HeadHeight) { why = "not above the head"; return false; }
            return true;
        }

        /// <summary>A view of a visitor at a tank and their bubble from the aisle beside them (side +1 their right, -1 their left),
        /// a little back from the tank (in front of them is the tank).</summary>
        void ViewWish(GrowthVisitorView view, float side = 1, float distance = 1.5f, float back = 0.5f, float lookAt = 1.8f)
        {
            Vector3 p = view.transform.position, f = view.transform.forward, r = view.transform.right * side; f.y = 0; r.y = 0;
            LookAt(p + r.normalized * distance - f.normalized * back, p + Vector3.up * lookAt);
        }

        IEnumerator WishesScenario()
        {
            limitations.Add("GR-20 wishes route: the fixture is the development start point for the wishes (GrowthStarts.WishesStart, labelled): the decor start furnished, 4 stars, a Sunny Pondlet in a1, a Shimmer Ribbonfin in a2, an Emberpuff in a3, the Deep Bubblebelly in the journal, 6,000 credits. The counter's Open the shop, Close for the day and Close are clicked with the route's mouse, the first sale of the second day is bagged by holding the mouse on the counter, and the other sales call the shop's Serve as that hold does (labelled). The first day's visitors are the shop's own; the second day's five are made as arrivals are, each with a wish chosen to show every kind (GrowthShop.DevelopmentArrive, labelled), and decide when the route lets their look end. Days run on the shop's own clock, accelerated where the route advances it.");
            yield return Opening();
            TakeInput();

            s.LoadFixture(GrowthStarts.WishesStart());
            var shop = s.Shop;
            var a1 = shop.ContainerAt("a1"); var a2 = shop.ContainerAt("a2"); var a3 = shop.ContainerAt("a3"); var b1 = shop.ContainerAt("b1");
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.Stars == 4 && shop.Credits == 6000 && shop.Journal.Contains("pondlet/Sunny") && shop.Journal.Contains("bubblebelly/Deep") && shop.Journal.Contains("ribbonfin/Shimmer") &&
                  a1.Occupants.Count(o => o.Species == "pondlet") == 3 && a1.Occupants.Any(o => o.Colour == Colour.Sunny) && a2.Occupants.Any(o => o.Colour == Colour.Shimmer) && a3.Occupants.Any(o => o.Species == "emberpuff"),
                "Fixture: the wishes start point: era 4, 4 stars, colours made and on show, small and medium creatures, 6,000 credits");

            // ---- a day of the shop's own visitors, each with a wish
            s.SetCard(GrowthCard.Counter);
            yield return ClickOn(e => ButtonText(e, "Open the shop"), "Click Open the shop on the counter card");
            Check(shop.Phase == GrowthPhase.Day, "The shop opens");
            for (int i = 0; i < 600 && shop.Visitors.Count(v => v.State == GrowthVisitorState.Browsing) < 3; i++) { s.Advance(0.1f); if (i % 5 == 0) yield return null; }
            yield return new WaitForSeconds(0.6f);
            var inside = shop.Visitors.Where(v => v.Type != GrowthShop.GuestType && v.State != GrowthVisitorState.Leaving).ToList();
            Check(inside.Count >= 3 && inside.All(v => v.Wish != null), "Every visitor comes in with a wish (" + string.Join("; ", inside.Select(v => GrowthShop.TypeName(v.Type) + ": " + v.Wish?.Text)) + ")");
            Check(inside.All(v => v.Wish.Kind != WishKind.Species || shop.Discovered.Contains(v.Wish.Species) && GrowthContent.Get(v.Wish.Species).Kind != SpeciesKind.Crown && GrowthContent.Get(v.Wish.Species).Price <= v.Budget),
                "Each wishes for something the shop could offer them: a species it has discovered, never a crown, at a price they can pay");
            foreach (var v in inside)
            {
                bool right = BubbleRight(v, out string why);
                Check(right, "A bubble over the " + GrowthShop.TypeName(v.Type).ToLowerInvariant() + " shows their wish, " + v.Wish.Text + (right ? "" : " (" + why + ")"));
            }
            // The reticle on a visitor at a tank reads their wish.
            yield return Wait(() => shop.Visitors.Any(v => v.Wish != null && v.State == GrowthVisitorState.Browsing && ViewOf(v) != null && ViewOf(v).Arrived), 20, "a visitor browsing at a tank");
            var looked = shop.Visitors.First(v => v.Wish != null && v.State == GrowthVisitorState.Browsing && ViewOf(v) != null && ViewOf(v).Arrived);
            looked.BrowseFor = looked.Clock + 60;
            // First a wider view of them at their tank, from the aisle behind them, above the reticle's line.
            ViewWish(ViewOf(looked), 1, 1.8f, 2.6f, 2.2f);
            yield return Shot("600-visitors-wishing");
            // From the aisle behind them and to their side, the reticle on their middle.
            ViewWish(ViewOf(looked), 1, 1.5f, 1.8f, 1.25f);
            yield return null; yield return null; yield return null;
            Check(s.Ui.WishShown != null && s.Ui.WishShown.StartsWith("Wishes for " + looked.Wish.Text + " · " + GrowthShop.TypeName(looked.Type)),
                "The reticle on a visitor reads their wish under it (\"" + s.Ui.WishShown + "\")");
            yield return Shot("601-reticle-reads-a-wish");
            looked.BrowseFor = looked.Clock + 0.1f;
            // The counter's queue says each wish, granted by the sale or not.
            for (int i = 0; i < 800 && shop.Queue.Count() < 2; i++) { s.Advance(0.1f); if (i % 5 == 0) yield return null; }
            s.SetCard(GrowthCard.Counter);
            yield return null; yield return null;
            string card = s.Ui.CardText();
            var queued = shop.Queue.Where(v => v.Wish != null).ToList();
            Check(queued.Count >= 1 && queued.All(v => card.Contains(shop.WouldGrant(v) ? "their wish: a tip of " + GrowthWishes.Tip(shop.ReservedPrice(v)) : "wished for " + v.Wish.Text)),
                "The counter's queue says what each is buying and their wish, granted by this sale with its tip or not (" + queued.Count + " waiting)");
            yield return Shot("602-counter-queue-wishes");
            s.SetCard(GrowthCard.None);
            // A while of the day on the shop's clock (labelled), serving, then closed early from the counter.
            for (int i = 0; i < 600; i++) { s.Advance(0.25f); if (shop.FrontOfQueue != null) shop.Serve(0.5f); if (i % 20 == 0) yield return null; }
            s.SetCard(GrowthCard.Counter);
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Close for the day"), "Click Close for the day");
            yield return ClickOn(e => ButtonText(e, "Close") && e.ClassListContains("es-button--danger"), "Click Close");
            yield return Wait(() => s.Card == GrowthCard.Receipt, 5, "the first day's receipt");
            var d1 = shop.LastDay;
            card = s.Ui.CardText();
            Check(d1.Wishes == d1.Visitors && d1.WishesGranted + d1.WishesMissed.Values.Sum() == d1.Wishes && card.Contains(d1.WishesGranted + " of " + d1.Wishes + " wishes granted") &&
                  (d1.Tips == 0 || card.Contains(d1.Tips + " credits in tips")) && (d1.WishesMissed.Count == 0 || card.Contains("Wished for and not granted: ")),
                "The receipt counts the day's wishes: " + d1.WishesGranted + " of " + d1.Wishes + " granted, " + d1.Tips + " credits in tips, every other one listed as wished for and not granted");
            yield return Shot("603-receipt-day-one");
            s.SetCard(GrowthCard.None);
            yield return new WaitForSeconds(GrowthShop.LeaveSeconds + 0.5f);

            // ---- a day of staged visitors, one for each kind of wish, each at a tank of their own
            // The tanks restocked at night as the start point had them (development stock, labelled): the first day sold some.
            var b2 = shop.ContainerAt("b2");
            void Restock(GrowthContainer c, params (string species, Colour colour)[] stock)
            {
                c.Occupants.Clear();
                foreach (var (sp, col) in stock) { Ok(shop.DevelopmentStock(c.Id, sp), "Restock " + sp); c.Occupants[c.Occupants.Count - 1].Colour = col; }
            }
            Restock(a1, ("pondlet", Colour.Sunny), ("pondlet", Colour.Classic), ("pondlet", Colour.Classic), ("bubblebelly", Colour.Classic), ("bubblebelly", Colour.Classic));
            Restock(a2, ("ribbonfin", Colour.Classic), ("ribbonfin", Colour.Classic), ("ribbonfin", Colour.Shimmer));
            Restock(a3, ("puddlepup", Colour.Classic), ("puddlepup", Colour.Classic), ("reedwhisker", Colour.Classic), ("emberpuff", Colour.Classic));
            Restock(b1, ("lanternfin", Colour.Classic), ("lanternfin", Colour.Classic), ("sparkminnow", Colour.Classic), ("sparkminnow", Colour.Classic));
            Restock(b2, ("brinefin", Colour.Classic), ("pebblejaw", Colour.Classic));
            s.SetCard(GrowthCard.Counter);
            yield return ClickOn(e => ButtonText(e, "Open the shop"), "Click Open the shop on the counter card for the second day");
            var size = shop.DevelopmentArrive("premium", new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Medium }, b2.Id, 999);
            var pair = shop.DevelopmentArrive("collector", new GrowthWish { Kind = WishKind.Pair }, b1.Id, 999);
            var school = shop.DevelopmentArrive("premium", new GrowthWish { Kind = WishKind.School }, a1.Id, 999);
            var species = shop.DevelopmentArrive("collector", new GrowthWish { Kind = WishKind.Species, Species = "ribbonfin" }, a2.Id, 999);
            var colour = shop.DevelopmentArrive("casual", new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Sunny }, a3.Id, 999);
            var staged = new[] { size, pair, school, species, colour };
            yield return Wait(() => staged.All(v => ViewOf(v) != null && ViewOf(v).Arrived), 25, "the five to reach their tanks");
            yield return new WaitForSeconds(0.5f);
            Check(shop.Visitors.Count(v => v.State != GrowthVisitorState.Leaving) == 5, "Five visitors in the shop, the units' most at once");
            var expected = new[] { (size, "size:Medium", 1, 2, (string)null), (pair, "pair", 2, 0, null), (school, "school", 3, 0, null), (species, "species:ribbonfin", 0, 0, null), (colour, "colour:Sunny", 1, 0, "wish__sun") };
            int shot = 604;
            foreach (var (v, content, tokens, pips, sign) in expected)
            {
                var view = ViewOf(v);
                ViewWish(view, 1, 1.5f, 0.5f, 1.4f);
                yield return null; yield return null;
                bool right = BubbleRight(v, out string why);
                var b = view.Bubble;
                Check(right && b.Content == content && b.Tokens == tokens && b.Pips == pips && (sign == null || b.Sign == sign) && (v != species || b.Creature == "ribbonfin"),
                    "The " + GrowthShop.TypeName(v.Type).ToLowerInvariant() + "'s bubble shows " + v.Wish.Text + ": " + content + ", " + tokens + " token(s), " + pips + " pip(s)" + (right ? "" : " (" + why + ")"));
                yield return Shot(shot++ + "-bubble-" + content.Replace(':', '-').ToLowerInvariant());
            }

            // ---- the size and pair wishes decided (the counter's queue holds two) and served: tips, points, notes, a heart, a chime
            foreach (var v in new[] { size, pair }) { v.BrowseFor = v.Clock + 0.05f; s.Advance(0.1f); yield return null; }
            Check(new[] { size, pair }.All(v => v.State == GrowthVisitorState.Queued && shop.WouldGrant(v)) && shop.Reserved(size).Single().Species == "pebblejaw" &&
                  shop.Reserved(pair).Count == 2 && shop.Reserved(pair).All(x => x.Species == "lanternfin"),
                "Each took their wish from their tank: the big spender a Pebblejaw (medium, the dearest), the collector two Lanternfins; both in the queue");
            s.SetCard(GrowthCard.Counter);
            yield return null; yield return null;
            card = s.Ui.CardText();
            Check(card.Contains("wants a Pebblejaw · 42 cr · their wish: a tip of 11") && card.Contains("wants a pair of Lanternfins · 24 cr · their wish: a tip of 6"),
                "The counter's queue: a Pebblejaw and a pair of Lanternfins, each granting the wish with its tip");
            yield return Shot("609-counter-two-wishes");
            s.SetCard(GrowthCard.None);
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0));
            yield return Wait(() => shop.FrontOfQueue != null && ViewOf(shop.FrontOfQueue) != null && ViewOf(shop.FrontOfQueue).Arrived, 20, "the first to reach the counter");
            yield return null;
            Aimed(GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            var first = shop.FrontOfQueue;
            int credits = shop.Credits, rep = shop.Reputation, chimes = s.Audio.Chimes, price = shop.ReservedPrice(first), sales = shop.SalesTotal;
            Hold(true);
            yield return Wait(() => shop.SalesTotal > sales, 6, "the held mouse to bag the sale");
            Hold(false);
            yield return null; yield return null;
            Check(first.WishGranted && shop.Credits == credits + price + GrowthWishes.Tip(price) && shop.Reputation == rep + first.SpecimenIds.Count * GrowthContent.RepPerSale + GrowthContent.RepPerWish,
                "The held mouse bags the sale: " + price + " credits, a tip of " + GrowthWishes.Tip(price) + " and a reputation point for the wish");
            Check(s.Ui.Log.Any(l => l.Contains(", a wish granted: a tip of " + GrowthWishes.Tip(price))) && s.Ui.Log.Any(l => l == "A wish granted: " + first.Wish.Text + ", with a tip of " + GrowthWishes.Tip(price) + " credits"),
                "The sale's note says the wish was granted, with its tip");
            Check(ViewOf(first).Bubble.Content == "heart" && ViewOf(first).Bubble.HeartShown, "The bubble pops into a heart");
            Check(s.Audio.Chimes == chimes + 1, "A chime by the visitor (" + s.Audio.Chimes + ")");
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), ViewOf(first).transform.position + Vector3.up * 2.5f);
            yield return Shot("610-wish-granted-heart");
            for (int i = 0; i < 200 && !(pair.WishGranted && size.WishGranted); i++) { s.Advance(0.1f); if (shop.FrontOfQueue != null) shop.Serve(0.5f); if (i % 5 == 0) yield return null; }
            Check(size.WishGranted && pair.WishGranted, "Both served, each wish granted");

            // ---- the school of three
            school.BrowseFor = school.Clock + 0.05f; s.Advance(0.1f); yield return null;
            Check(school.State == GrowthVisitorState.Queued && shop.WouldGrant(school) && shop.Reserved(school).Count == 3 && shop.Reserved(school).All(x => x.Species == "pondlet"),
                "The big spender took a school of three Pondlets from a1");
            for (int i = 0; i < 200 && !school.WishGranted; i++) { s.Advance(0.1f); if (shop.FrontOfQueue != null) shop.Serve(0.5f); if (i % 5 == 0) yield return null; }
            Check(school.WishGranted, "Served, the school granted that wish");

            // ---- the species wish granted, the colour wish not: it shrinks away
            foreach (var v in new[] { species, colour }) { v.BrowseFor = v.Clock + 0.05f; s.Advance(0.1f); yield return null; }
            Check(species.State == GrowthVisitorState.Queued && shop.WouldGrant(species) && colour.State == GrowthVisitorState.Queued && !shop.WouldGrant(colour) && shop.Reserved(colour).Single().Colour != Colour.Sunny,
                "The collector found a Ribbonfin; a3 holds nothing Sunny, so the casual visitor chose something else");
            s.SetCard(GrowthCard.Counter);
            yield return null; yield return null;
            card = s.Ui.CardText();
            Check(card.Contains("wants a Ribbonfin · 8 cr · their wish: a tip of 2") && card.Contains("· wished for a Sunny creature"), "The queue: one wish granted by the sale, one not");
            yield return Shot("611-counter-granted-and-not");
            s.SetCard(GrowthCard.None);
            for (int i = 0; i < 200 && !(species.State == GrowthVisitorState.Leaving && colour.State == GrowthVisitorState.Leaving); i++) { s.Advance(0.1f); if (shop.FrontOfQueue != null) shop.Serve(0.5f); if (i % 5 == 0) yield return null; }
            yield return null; yield return null;
            Check(species.WishGranted && colour.WishMissed && !colour.WishGranted && colour.Outcome == GrowthVisitOutcome.Bought, "The Ribbonfin's sale granted that wish; the casual visitor bought without theirs");
            Check(ViewOf(colour) != null && !ViewOf(colour).Bubble.HeartShown, "Their bubble shrinks away as they leave, no heart");
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), ViewOf(colour).transform.position + Vector3.up * 1.6f);
            yield return Shot("612-wish-missed-shrinks");

            // ---- the receipt
            s.SetCard(GrowthCard.Counter);
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Close for the day"), "Click Close for the day on the second day");
            yield return ClickOn(e => ButtonText(e, "Close") && e.ClassListContains("es-button--danger"), "Click Close on the second day");
            yield return Wait(() => s.Card == GrowthCard.Receipt, 5, "the second day's receipt");
            var d2 = shop.LastDay;
            card = s.Ui.CardText();
            Check(d2.WishesGranted >= 4 && d2.Tips >= 24 && d2.WishesMissed.ContainsKey("a Sunny creature") && card.Contains(d2.WishesGranted + " of " + d2.Wishes + " wishes granted") &&
                  card.Contains(d2.Tips + " credits in tips") && card.Contains("Wished for and not granted: ") && card.Contains("a Sunny creature"),
                "The receipt: " + d2.WishesGranted + " of " + d2.Wishes + " wishes granted, " + d2.Tips + " credits in tips, and a Sunny creature wished for and not granted");
            yield return Shot("613-receipt-wishes");
            s.SetCard(GrowthCard.None);
        }
    }
}
#endif
