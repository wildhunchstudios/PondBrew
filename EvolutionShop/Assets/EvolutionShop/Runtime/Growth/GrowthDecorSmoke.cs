#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    /// <summary>
    /// GR-19 and GR-22's Windows route (-evolutionGrowthDecorSmoke). GR-19, decor: three pieces ordered from the supplier
    /// catalog with its buttons (Riverbank and Hot springs on its 2-star page, Seaside on its 4-star page), delivered into
    /// the decor box when the shop opens; at night each put on a tank's top from the tank card's Decor tab, standing on the
    /// tank in the look; the tank's appeal and its themed creatures' budgets; a piece swapped, a decorated tank moved with its
    /// piece, a piece taken off; by day the Decor tab saying why not; save and reload. GR-22, closing early: the counter
    /// card's Close for the day, asking first with what it cuts short, Stay open, then Close: the night begins and the receipt
    /// says the day closed early.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string DecorScenarioText = "GR-19 decor and GR-22 closing early: three pieces ordered from the supplier catalog's pages with its buttons, delivered into the decor box when the shop opens; the counter card's Close for the day asking first, Stay open, then Close, the receipt saying so; each piece put on a tank's top from the Decor tab and standing there; appeal and themed budgets; a swap, a decorated tank moved with its piece, a piece taken off; the Decor tab by day; save and reload";

        /// <summary>Clicks a control of the open card with the route's mouse (UI Toolkit reads the Input System's pointer).</summary>
        IEnumerator ClickOn(Func<UnityEngine.UIElements.VisualElement, bool> which, string what)
        {
            yield return null; yield return null;
            var card = s.Ui.CardElement;
            var element = card == null ? null : UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.VisualElement>(card).ToList().FirstOrDefault(which);
            if (element == null) { Check(false, what + " (no such control on the card; it says: " + s.Ui.CardText() + ")"); yield break; }
            var scroll = element.GetFirstAncestorOfType<UnityEngine.UIElements.ScrollView>();
            if (scroll != null) { scroll.ScrollTo(element); yield return null; yield return null; }
            Rect r = element.worldBound; Vector2 panel = s.Ui.Root.layout.size;
            var at = new Vector2(r.center.x * Screen.width / panel.x, Screen.height - r.center.y * Screen.height / panel.y);
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = 1 });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2) });
            yield return null; yield return null;
            checks.Add(what); Debug.Log("SMOKE_CHECK " + what);
        }

        /// <summary>Whether a decor holder carries the kit piece (by its child name: Transform.Find would read the slash as a path).</summary>
        static bool HasKit(Transform holder, string asset) => holder != null && holder.Cast<Transform>().Any(c => c.name == "Look / " + asset);

        static bool ButtonText(UnityEngine.UIElements.VisualElement e, string text) => e is UnityEngine.UIElements.Button b && b.text == text;

        /// <summary>A view of a tank's decor from a little behind its browse spot.</summary>
        void ViewDecor(GrowthContainer c, float back = 0.9f, float side = 0.5f)
        {
            var piece = s.World.DecorOf(c.Id);
            var place = GrowthWorld.Places[c.Place];
            var out_ = (place.Browse - place.Position); out_.y = 0; out_.Normalize();
            var across = Vector3.Cross(Vector3.up, out_);
            var feet = place.Browse + out_ * back + across * side;
            LookAt(feet, (piece != null ? piece.position : place.Position + Vector3.up * 1.6f) + Vector3.up * 0.2f);
        }

        IEnumerator DecorScenario()
        {
            limitations.Add("GR-19 decor route: the fixture is the development start point for the decor (GrowthStarts.DecorStart, unfurnished, labelled): the era 4 start at 4 stars with 8,000 credits, a1's fresh-water Brookkin, b1's warm-water creatures in warm water, two Saltscale founders (development stock) in b2's brine water, and no decor yet. The catalog's order buttons, the counter's Open the shop, Close for the day, Stay open and Close, the tank card's Decor tab, its Put it on and Take it off buttons are clicked with the route's mouse; one piece (the lighthouse on b2), the swap, the move and the second day's opening and closing call the shop's actions as those buttons do (labelled). The day runs on the shop's own clock, accelerated.");
            yield return Opening();
            TakeInput();

            s.LoadFixture(GrowthStarts.DecorStart(furnished: false));
            var shop = s.Shop;
            var a1 = shop.ContainerAt("a1"); var b1 = shop.ContainerAt("b1"); var b2 = shop.ContainerAt("b2");
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.Stars == 4 && shop.Credits == 8000 && shop.DecorOwned.Count == 0 && b1.Water == Water.Warm && b2.Water == Water.Brine &&
                  b2.Occupants.Count == 2 && b2.Occupants.All(o => o.Def.Family == "saltscale"), "Fixture: the decor start point, unfurnished: era 4, 4 stars, 8,000 credits, warm b1, brine b2 with two Saltscale, no decor");

            // ---- the supplier catalog: decor on its pages, ordered with its buttons
            s.SetCard(GrowthCard.Catalog);
            yield return null; yield return null;
            string card = s.Ui.CardText();
            Check(new[] { "Reeds and a dragonfly", "Lily-pad lamp", "Little watermill", "Little volcano", "Stone lantern", "Rubber-duck hot pool" }.All(card.Contains) && card.Contains("Riverbank") && card.Contains("Hot springs"),
                "The catalog's 2-star page sells the Riverbank and Hot springs pieces, each saying its theme");
            yield return Shot("580-catalog-decor");
            yield return ClickOn(e => e.name == "gr-order-decor-reeds", "Click Order for the reeds");
            yield return ClickOn(e => e.name == "gr-order-decor-volcano", "Click Order for the little volcano");
            yield return ClickOn(e => ButtonText(e, "Page 4 · 4 stars"), "Click the catalog's page 4");
            card = s.Ui.CardText();
            Check(card.Contains("Lighthouse") && card.Contains("Treasure chest") && card.Contains("Coral arch") && card.Contains("Seaside"), "Page 4 sells the Seaside pieces");
            yield return ClickOn(e => e.name == "gr-order-decor-lighthouse", "Click Order for the lighthouse");
            yield return Shot("581-catalog-seaside");
            Check(shop.Orders.Contains("decor-reeds") && shop.Orders.Contains("decor-volcano") && shop.Orders.Contains("decor-lighthouse") && shop.Credits == 8000 - 120 - 120 - 800 && shop.DecorOwned.Count == 0,
                "Three pieces on order, paid (" + shop.Credits + " credits left), none here before the shop opens");

            // ---- the counter: open the shop; the night's orders arrive in the decor box
            s.SetCard(GrowthCard.Counter);
            yield return ClickOn(e => ButtonText(e, "Open the shop"), "Click Open the shop on the counter card");
            Check(shop.Phase == GrowthPhase.Day && shop.DecorOwned.Count == 3 && shop.DecorBox.Count() == 3 && s.Ui.Log.Any(l => l == "Delivered: Lighthouse (in the decor box: put it on a tank at night)"),
                "The shop opens and the decor arrives in the box, the note saying where");

            // ---- GR-22: Close for the day, asking first; Stay open; then Close
            for (int i = 0; i < 400 && shop.Visitors.Count(v => v.State != GrowthVisitorState.Leaving) < 2; i++) { s.Advance(0.1f); if (i % 5 == 0) yield return null; }
            int inside = shop.Visitors.Count(v => v.State != GrowthVisitorState.Leaving);
            int reservedBefore = shop.Stock.Count(x => x.ReservedBy != 0);
            s.SetCard(GrowthCard.Counter);
            yield return null; yield return null;
            Check(inside > 0 && s.Ui.CardText().Contains("Close for the day · "), "By day the counter card offers Close for the day, with the time left (" + inside + " visitors inside)");
            yield return Shot("582-counter-close-for-the-day");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Close for the day"), "Click Close for the day");
            card = s.Ui.CardText();
            Check(shop.Phase == GrowthPhase.Day && card.Contains("Close the shop now, with") && card.Contains("still in the shop") && card.Contains("without buying"),
                "It asks first, saying what closing now cuts short: the visitors still inside leave without buying");
            yield return Shot("583-counter-asks");
            yield return ClickOn(e => ButtonText(e, "Stay open"), "Click Stay open");
            Check(shop.Phase == GrowthPhase.Day && s.Ui.CardText().Contains("Close for the day · "), "Stay open: the shop stays open and the button is back");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Close for the day"), "Click Close for the day again");
            int credits = shop.Credits;
            yield return ClickOn(e => ButtonText(e, "Close") && e.ClassListContains("es-button--danger"), "Click Close");
            yield return Wait(() => s.Card == GrowthCard.Receipt, 5, "the receipt after closing early");
            Check(shop.Phase == GrowthPhase.Night && shop.Day == 16 && shop.LastDay.ClosedEarly && shop.LastDay.SecondsLeft > 30 && shop.Credits == credits &&
                  shop.Visitors.All(v => v.State == GrowthVisitorState.Leaving && v.Outcome == GrowthVisitOutcome.Closed) && shop.Stock.All(x => x.ReservedBy == 0),
                "The shop closes as at closing time: everyone leaves, " + reservedBefore + " reservations released, no unfinished sale paid, the night begins");
            card = s.Ui.CardText();
            Check(card.Contains("Closed with " + GrowthShop.ClockText(shop.LastDay.SecondsLeft) + " to go") && s.Ui.Log.Any(l => l.StartsWith("Day 15 is over: you closed the shop with ")),
                "The receipt and the note say the day closed early, and with how long to go");
            yield return Shot("584-receipt-closed-early");
            s.SetCard(GrowthCard.None);
            yield return new WaitForSeconds(GrowthShop.LeaveSeconds * 0.6f);

            // ---- GR-19: the Decor tab, at night; a piece on a1's top
            s.SetCard(GrowthCard.Tank, a1.Id);
            yield return ClickOn(e => ButtonText(e, "Decor") && e.ClassListContains("gr-tab"), "Click the tank card's Decor tab");
            card = s.Ui.CardText();
            Check(card.Contains("No decor on it") && card.Contains("YOUR DECOR") && card.Contains("Reeds and a dragonfly") && card.Contains("in the box") && card.Contains("suits 5 creatures here"),
                "The Decor tab: nothing on it yet, the box's three pieces, each saying how many of these creatures it suits");
            yield return Shot("585-decor-tab");
            yield return ClickOn(e => e.name == "gr-decor-put-reeds", "Click Put it on for the reeds");
            yield return null; yield return null;
            var reeds = s.World.DecorOf(a1.Id);
            Check(a1.Decor == "reeds" && reeds != null && reeds.GetComponentsInChildren<Renderer>().Any(r => r.enabled) && HasKit(reeds, "DecorReeds"),
                "The reeds stand on a1's top, the kit's piece");
            card = s.Ui.CardText();
            Check(card.Contains("Take it off") && card.Contains("Riverbank: fresh-water creatures sell for more here") && card.Contains("5 creatures themed") && card.Contains("Appeal +1"),
                "The tab says what the piece does: Riverbank, five themed creatures, appeal +1");
            yield return Shot("586-decor-on");
            s.SetCard(GrowthCard.None);
            var place = GrowthWorld.Places[a1.Place];
            LookAt(place.Browse + (place.Browse - place.Position).normalized * 0.3f, place.Position + Vector3.up * 1.1f);
            yield return null; yield return null;
            s.FocusName(out string status);
            Check(status != null && status.Contains(" · Riverbank"), "Focused, the tank says its theme (" + status + ")");
            ViewDecor(a1); yield return Shot("587-reeds-on-a1");

            // ---- the volcano on b1 (clicked) and the lighthouse on b2 (as the button does)
            s.SetCard(GrowthCard.Tank, b1.Id);
            yield return ClickOn(e => ButtonText(e, "Decor") && e.ClassListContains("gr-tab"), "Click b1's Decor tab");
            yield return ClickOn(e => e.name == "gr-decor-put-volcano", "Click Put it on for the little volcano");
            Ok(s.Act(() => shop.PutDecor("lighthouse", b2.Id)), "The lighthouse on b2 (as the Decor tab's Put it on does)");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(b1.Decor == "volcano" && b2.Decor == "lighthouse" && HasKit(s.World.DecorOf(b1.Id), "DecorVolcano") && HasKit(s.World.DecorOf(b2.Id), "DecorLighthouse") && !shop.DecorBox.Any(),
                "The little volcano on b1 and the lighthouse on b2, each standing on its tank; the box is empty");
            ViewDecor(b1); yield return Shot("588-volcano-on-b1");
            ViewDecor(b2); yield return Shot("589-lighthouse-on-b2");
            LookAt(new Vector3(5.0f, 0, -1.8f), new Vector3(7.2f, 1.5f, 3.4f)); yield return Shot("590-unit-b-decor");

            // ---- what decor does: appeal, and themed creatures' budgets (capped with a school)
            float Expected(GrowthContainer c, string species) => 1f + Mathf.Min(GrowthContent.ToleranceCap, (c.InSchool(species) ? GrowthContent.SchoolTolerance : 0) +
                shop.Appeal(c) * GrowthContent.AppealTolerance + (GrowthShop.Themed(c, species) ? GrowthContent.ThemeTolerance : 0));
            var salt = b2.Occupants.First().Species;
            var warm = b1.Occupants.First(o => o.Def.Water == Water.Warm).Species;
            Check(GrowthShop.Themed(b2, salt) && GrowthShop.Themed(b1, warm) && GrowthShop.Themed(a1, "pondlet") && !GrowthShop.Themed(b1, "pondlet") &&
                  Mathf.Abs(shop.Tolerance(b2, salt) - Expected(b2, salt)) < 1e-5f && shop.Tolerance(b2, salt) >= 1.35f - 1e-4f && Mathf.Abs(shop.Tolerance(a1, "pondlet") - 1.4f) < 1e-4f,
                "Themed creatures: the Saltscale on b2 stretch budgets to " + shop.Tolerance(b2, salt).ToString("0.00") + "x, a1's school of Pondlets to the cap, 1.40x");

            // ---- a swap, a move with its piece, a piece taken off
            Ok(s.Act(() => shop.PutDecor("reeds", b2.Id)), "The reeds on b2 instead (as Put it on does)");
            Check(b2.Decor == "reeds" && shop.DecorBox.SequenceEqual(new[] { "lighthouse" }) && a1.Decor == null, "The reeds move from a1 to b2, and b2's lighthouse goes back to the box");
            Ok(s.Act(() => shop.PutDecor("lighthouse", a1.Id)), "The lighthouse on a1");
            Ok(s.Act(() => shop.MoveContainer(a1.Id, "a4")), "Move a1's tank to a4 (as the tank card's Move does)");
            yield return null; yield return null;
            var moved = s.World.DecorOf(a1.Id);
            Check(a1.Place == "a4" && a1.Decor == "lighthouse" && moved != null && Vector3.Distance(new Vector3(moved.position.x, 0, moved.position.z), GrowthWorld.Places["a4"].Position) < 1.5f,
                "The tank moved to a4 with the lighthouse on its top");
            ViewDecor(a1); yield return Shot("591-moved-with-its-tank");
            s.SetCard(GrowthCard.Tank, a1.Id);
            yield return ClickOn(e => ButtonText(e, "Decor") && e.ClassListContains("gr-tab"), "Click the moved tank's Decor tab");
            yield return ClickOn(e => ButtonText(e, "Take it off"), "Click Take it off");
            yield return null; yield return null;
            Check(a1.Decor == null && shop.DecorBox.SequenceEqual(new[] { "lighthouse" }) && s.World.DecorOf(a1.Id) == null, "Taken off: back in the box, gone from the tank");

            // ---- by day the Decor tab says why not
            Ok(s.Act(shop.OpenShop), "Open the shop for a second day (as the counter's button does)");
            s.SetCard(GrowthCard.Tank, a1.Id);
            yield return ClickOn(e => ButtonText(e, "Decor") && e.ClassListContains("gr-tab"), "Click the Decor tab by day");
            yield return HoverOn(e => e.name == "gr-decor-put-lighthouse", "Decor goes on at night", "By day Put it on says why not", "592-decor-by-day");
            Ok(s.Act(shop.CloseEarly), "Close the second day early (as Close does)");
            s.SetCard(GrowthCard.None);
            yield return null;

            // ---- save and reload
            Check(s.SaveNow(), "Saved");
            yield return null; yield return null;
            Check(s.ReloadSaved(), "Reloaded from the file");
            s.SetCard(GrowthCard.None);
            shop = s.Shop;
            yield return null; yield return null;
            b1 = shop.ContainerAt("b1"); b2 = shop.ContainerAt("b2");
            Check(shop.DecorOwned.Count == 3 && b1.Decor == "volcano" && b2.Decor == "reeds" && shop.DecorBox.SequenceEqual(new[] { "lighthouse" }) &&
                  HasKit(s.World.DecorOf(b1.Id), "DecorVolcano") && HasKit(s.World.DecorOf(b2.Id), "DecorReeds"),
                "After a reload the decor is where it was: the volcano on b1, the reeds on b2, the lighthouse in the box");
            LookAt(new Vector3(5.0f, 0, -1.8f), new Vector3(7.2f, 1.5f, 3.4f)); yield return Shot("593-after-reload");
        }
    }
}
#endif
