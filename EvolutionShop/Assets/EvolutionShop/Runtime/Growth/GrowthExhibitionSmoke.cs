#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-11's Windows route (-evolutionGrowthExhibitionSmoke): the objective board on the first night (the pinboard and
    /// the journal's goals: now, next and the long goal with the Grand Exhibition's requirements); the exhibition start
    /// point's card at the hall's board; five crowns carried onto the spotlit places; preparing it (protected displays, six
    /// announced creatures, the sign over the door and the display tags); an attempt that falls short and loses nothing;
    /// preparing it again; an exhibition guest walking in; the exhibition held, with confetti, the award by the door and the
    /// results card; free play on the board; save and reload.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string ExhibitionScenarioText = "GR-11 objective board and the Grand Exhibition: the pinboard and the journal's goals on the first night (now, next and the long goal with the exhibition's requirements), the exhibition's card at the hall's board, five crowns carried onto the spotlit places, preparing it (protected displays, six announced creatures, the sign over the door, the display tags), an attempt that falls short with nothing lost, preparing it again, an exhibition guest walking in, the exhibition held with confetti, the award and the results card, free play on the board, save and reload";

        IEnumerator ExhibitionScenario()
        {
            limitations.Add("GR-11 exhibition route: fixtures are labelled: a fresh shop for the first night's board, then the exhibition start point (GrowthStarts.ExhibitionStart: era 5 with the workshop, unit C and the hall, a full day run on the line, 55 species from all nine families with five crowns, the crowns waiting in tanks on c3 and c4). Crowns are carried, the exhibition prepared and the shop opened through the shop's actions as E and the card buttons do (UI Toolkit buttons are not clicked); the six announced creatures are kept in stock with development stock as a player or Nori would keep them (one withheld all through the first attempt), room made by taking out creatures not announced; the days run on the shop's own clock (accelerated), except the first guest's walk in on the second, at the game's own pace; the counter is served as holding it does. Placeholder art.");
            yield return Opening();
            TakeInput();

            // The first night: the board shows now, next and the long goal, the exhibition's requirements included.
            s.LoadFixture(new GrowthShop());
            var shop = s.Shop;
            yield return null; yield return null;
            yield return AimAt(new Vector3(0.4f, 0, GrowthWorld.BoardPosition.z), GrowthWorld.BoardPosition, GrowthTargetKind.Board, null, "The reticle focuses the pinboard in the laboratory");
            yield return Press(Key.E);
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Board && card.Contains("Now · Era 1 · Laboratory") && card.Contains("Next: Era 2 · Real shop") && card.Contains("Gate: 2 stars, 3 creatures discovered, 2 tanks") &&
                card.Contains("Discover creatures: 0 / 72 · Journal: 0 / 288 · Exhibition: 0 of 6 requirements") && card.Contains("Discover 48 of the 72 creatures (0/48)") && card.Contains("Discover 5 of the 9 crowns (0/5)"),
                "E opens the pinboard: now (Era 1's goal and steps), next (Era 2 and its gate) and the long goal with the Grand Exhibition's requirements, from the first night");
            yield return Shot("150-board-first-night");
            s.SetCard(GrowthCard.None);
            yield return null;
            yield return Press(Key.Tab);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Journal && card.Contains("Goals") && card.Contains("Discover creatures: 0 / 72 · Journal: 0 / 288") && card.Contains("Next: Era 2 · Real shop"), "Tab: the journal opens on the same goals");
            yield return Shot("151-journal-goals");
            s.SetCard(GrowthCard.None);

            // The exhibition start point (labelled): every requirement met, no crown on display yet.
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Exhibition));
            shop = s.Shop;
            yield return null; yield return null;
            Check(shop.ExhibitionOpen && shop.DisplayReady == 0 && !shop.ExhibitionPrepared && shop.Phase == GrowthPhase.Night && !s.World.AwardShowing && !s.World.SignShowing,
                "Fixture: the exhibition start point, every requirement met, no crown on display yet, no sign and no award");
            var hallFeet = new Vector3(GrowthWorld.HallBoardPosition.x, 0, 6.8f);
            yield return AimAt(hallFeet, GrowthWorld.HallBoardPosition, GrowthTargetKind.HallBoard, "hall", "The reticle focuses the hall's board in unit C");
            Check(s.FocusName(out string status) == "Exhibition hall" && status == "E the Grand Exhibition", "It is the Grand Exhibition's board");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Hall && card.Contains("The Grand Exhibition") && shop.ExhibitionOpen && card.Contains("Discover 48 of the 72 creatures (48/48)") && card.Contains("On display: 5 different crowns (0/5)") &&
                card.Contains("Put 5 different crowns on display in tanks on the spotlit places (0/5)"), "E opens its card: every requirement met, no crown on display, preparing waits for five");
            yield return Shot("152-exhibition-card");
            s.SetCard(GrowthCard.None);

            // Five crowns carried from c3 and c4 to the tanks on the spotlit places (as E takes and puts in).
            var crowns = shop.Stock.Where(x => x.Def.Kind == SpeciesKind.Crown).ToList();
            for (int i = 0; i < crowns.Count; i++)
            {
                Ok(shop.TakeOut(shop.HolderOf(crowns[i].Id).Id, crowns[i].Id), "Take the " + crowns[i].Def.Name + " out (E on it)");
                Ok(shop.PutIn(shop.ContainerAt(GrowthShop.SpotlitPlaces[i]).Id), "Put it in the tank on " + GrowthShop.SpotlitPlaces[i]);
            }
            yield return null; yield return null;
            Check(crowns.Count == 5 && shop.DisplayReady == 5 && shop.PrepareCheck(out _) == GrowthFailure.None, "Five different crowns on the spotlit places: it can be prepared");
            LookAt(new Vector3(11.0f, 0, 6.0f), new Vector3(5.5f, 1.2f, 8.4f)); yield return Shot("153-crowns-on-display");

            // Prepared: the crowns go on display (protected), six creatures are announced, the sign goes up over the door.
            yield return AimAt(hallFeet, GrowthWorld.HallBoardPosition, GrowthTargetKind.HallBoard, "hall", "The reticle focuses the hall's board again");
            yield return Press(Key.E);
            Ok(s.Act(shop.PrepareExhibition), "Prepare the Grand Exhibition for tomorrow (the card's button)");
            yield return null; yield return null;
            card = s.Ui.CardText();
            var six = shop.ExhibitionOrders.Select(o => o.Species).ToList();
            int families = six.Select(id => GrowthContent.Get(id).Family).Distinct().Count();
            Check(shop.ExhibitionPrepared && shop.OnDisplay.Count() == 5 && six.Count == 6 && families >= GrowthShop.ExhibitionFamilies && card.Contains("Call it off") &&
                six.All(id => card.Contains(GrowthContent.Get(id).Name)) && s.Ui.Log.Any(l => l.StartsWith("The Grand Exhibition is tomorrow: six guests are coming for")),
                "Prepared: five crowns on display; six creatures announced from " + families + " families, each with its stock on the card; Call it off");
            yield return Shot("154-prepared-card");
            s.SetCard(GrowthCard.None);
            yield return null;
            var shown = shop.OnDisplay.First();
            var take = shop.TakeOut(shop.HolderOf(shown.Id).Id, shown.Id);
            Check(!take.Success && take.Detail == "on display for the Grand Exhibition" && s.World.SignShowing && !s.World.SignToday && s.World.DisplayTagsShown == 5,
                "On display a crown cannot be taken out (as E would); the sign over the door is up for tomorrow; each spotlit tank wears a tag");
            LookAt(new Vector3(-0.5f, 0, -0.2f), GrowthWorld.ExhibitionSignPosition); yield return Shot("155-sign-tomorrow");
            LookAt(new Vector3(11.0f, 0, 6.0f), new Vector3(5.5f, 1.8f, 8.4f)); yield return Shot("156-display-tags");

            // The first attempt (labelled): one of the six withheld from stock all day.
            string withheld = six[5];
            foreach (var c in shop.ShopContainers) c.Occupants.RemoveAll(x => x.Species == withheld && x.ReservedBy == 0);
            ExhibitionStock(shop, withheld);
            int discovered = shop.Discovered.Count, creditsBefore = shop.Credits;
            Ok(s.Act(shop.OpenShop), "Open the shop: the Grand Exhibition's day (E at the counter)");
            yield return null; yield return null;
            Check(shop.ExhibitionToday && s.World.SignToday && s.Ui.HudClock.Contains("exhibition guests 0/6"), "Under way: the sign is lit and the HUD counts the guests");
            yield return ExhibitionDay(withheld);
            string name = GrowthContent.Get(withheld).Name;
            card = s.Ui.CardText();
            Check(!shop.ExhibitionDone && shop.ExhibitionAttempts == 1 && shop.ExhibitionFailure.Contains("the guest for a " + name + " found no " + name + " in stock") &&
                s.Card == GrowthCard.Receipt && card.Contains("The Grand Exhibition fell short") && card.Contains("Nothing is lost"),
                "The guest for a " + name + " found none: the attempt falls short, and the receipt says why");
            yield return Shot("157-receipt-fell-short");
            Check(shop.Discovered.Count == discovered && shop.Credits == creditsBefore + shop.LastDay.Revenue && !shop.OnDisplay.Any() && shop.DisplayReady == 5 && !s.World.SignShowing && s.World.DisplayTagsShown == 0,
                "Nothing lost: discoveries kept, the day's " + shop.LastDay.Revenue + " credits of sales kept, the crowns still in place and free again; the sign and tags down");

            // The second night: prepared again with the same six, all of them in stock.
            s.SetCard(GrowthCard.None);
            Ok(s.Act(shop.PrepareExhibition), "Prepare it again (the card's button)");
            Check(shop.ExhibitionOrders.Select(o => o.Species).SequenceEqual(six), "The same six are announced");
            ExhibitionStock(shop, null);
            Ok(s.Act(shop.OpenShop), "Open the shop for the second attempt");
            float firstAt = GrowthShop.GuestArrival(0) * GrowthContent.DaySeconds - 1.5f;
            for (int i = 0; GrowthContent.DaySeconds - shop.DaySecondsLeft < firstAt && i < 400; i++)
            {
                s.Advance(0.25f);
                ExhibitionStock(shop, null);
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
                if (i % 5 == 0) yield return null;
            }
            // The first guest's walk in, at the game's own pace (the figures walk in real time).
            bool guestShot = false;
            float walkStart = Time.realtimeSinceStartup;
            while (!guestShot && shop.Phase == GrowthPhase.Day && Time.realtimeSinceStartup - walkStart < 60)
            {
                ExhibitionStock(shop, null);
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
                yield return null;
                var g = shop.Visitors.FirstOrDefault(v => v.Type == GrowthShop.GuestType && v.State == GrowthVisitorState.Browsing && v.ContainerId != null);
                if (g == null || !s.World.VisitorViews.TryGetValue(g.Id, out var view) || !view.Arrived) continue;
                string place = shop.Container(g.ContainerId).Place;
                var b = GrowthWorld.Places[place].Browse;
                if ((view.transform.position - b).sqrMagnitude >= 0.04f) continue;
                LookAt(GuestView(view.transform.position, place, g.Id), new Vector3(b.x, 1.2f, b.z));
                yield return Shot("158-exhibition-guest");
                guestShot = true;
                Check(g.Wants == six[g.Order] && shop.Container(g.ContainerId).Occupants.Any(o => o.Species == g.Wants), "An exhibition guest, in gold, looks into a tank holding the " + GrowthContent.Get(g.Wants).Name + " they came for");
            }
            Check(guestShot, "The first exhibition guest walked in to the tank holding what they came for");
            yield return ExhibitionDay(null);

            // Held: confetti, the award by the door, the receipt and the results.
            Check(shop.ExhibitionDone && shop.ExhibitionResults.Attempts == 2 && shop.LastDay.GuestsServed == 6 && s.Ui.Log.Contains("The Grand Exhibition is a success!") && s.World.ConfettiCount > 0 && s.World.AwardShowing,
                "All six guests bought what they came for with the crowns on display to the close: the Grand Exhibition is held (at the second attempt), confetti falls and the award goes up");
            s.SetCard(GrowthCard.None);
            yield return Shot("159-celebration");
            s.SetCard(GrowthCard.Receipt);
            yield return null; yield return null;
            card = s.Ui.CardText();
            Check(card.Contains("The Grand Exhibition is a success!") && card.Contains("See the results"), "The receipt says it is a success and offers the results");
            yield return Shot("160-receipt-success");
            s.SetCard(GrowthCard.Results);
            yield return null; yield return null;
            card = s.Ui.CardText();
            int day = shop.ExhibitionResults.Day;
            Check(card.Contains("The Grand Exhibition · day " + day) && six.All(id => card.Contains(GrowthContent.Get(id).Name)) && crowns.All(cr => card.Contains(cr.Def.Name)) && card.Contains("held at attempt 2") && card.Contains("Free play"),
                "The results card: the six guests' creatures, the five crowns, the collection, the day's takings, the attempts, free play");
            yield return Shot("161-results");
            s.SetCard(GrowthCard.None);
            yield return AimAt(new Vector3(0.75f, 0, -2.4f), GrowthWorld.AwardPosition, GrowthTargetKind.Award, "award", "The reticle focuses the award on unit A's front wall, east of the door");
            Check(s.FocusName(out status) == "The Grand Exhibition award" && status == "Held on day " + day + " · E the results", "It is the Grand Exhibition's award");
            yield return Shot("162-award");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Results, "E on the award shows the results");
            s.SetCard(GrowthCard.None);

            // Free play on the board.
            yield return AimAt(new Vector3(0.4f, 0, GrowthWorld.BoardPosition.z), GrowthWorld.BoardPosition, GrowthTargetKind.Board, null, "The reticle focuses the pinboard again");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("Now · Free play") && card.Contains("Next: free play") && card.Contains("Exhibition: held on day " + day) && card.Contains("Fill the journal"), "The pinboard: free play, and the journal is the long goal");
            yield return Shot("163-board-free-play");
            s.SetCard(GrowthCard.None);

            // Save and reload: held once, for good.
            Check(s.SaveNow(), "Save");
            string saved = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            shop = s.Shop;
            yield return null; yield return null;
            var file = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            var again = shop.PrepareCheck(out string why);
            Check(file.version == GrowthSave.CurrentVersion && JsonUtility.ToJson(GrowthShop.Restore(file, out _).Snapshot()) == saved && shop.ExhibitionDone && shop.ExhibitionResults.Day == day &&
                again == GrowthFailure.MaxLevel && s.World.AwardShowing && !s.World.SignShowing,
                "The version " + GrowthSave.CurrentVersion + " save keeps the exhibition held, its results and the award; it is never prepared again (" + why + ")");
        }

        /// <summary>Where to stand to see a guest at a tank: about 2 m off, to one side of the tank's front, in a shop room clear
        /// of the places and fixtures, with no other visitor between.</summary>
        Vector3 GuestView(Vector3 at, string place, int guest)
        {
            var tank = GrowthWorld.Places[place].Position;
            var away = new Vector3(at.x - tank.x, 0, at.z - tank.z);
            away = away.sqrMagnitude < 0.01f ? Vector3.back : away.normalized;
            var others = s.World.VisitorViews.Where(kv => kv.Key != guest).Select(kv => kv.Value.transform.position).ToList();
            bool InShop(float x, float z) { var r = GrowthSpaces.RoomAt((x, z)); return r != null && r.Shop; }
            bool Clear(Vector3 p) =>
                InShop(p.x, p.z) && InShop(p.x + 0.35f, p.z) && InShop(p.x - 0.35f, p.z) && InShop(p.x, p.z + 0.35f) && InShop(p.x, p.z - 0.35f) &&
                GrowthSpaces.Spots.All(sp => GrowthSpaces.Footprint(sp.Id).Distance((p.x, p.z)) >= 0.4f) && GrowthSpaces.Fixtures.All(f => f.area.Distance((p.x, p.z)) >= 0.4f);
            float Between(Vector3 o, Vector3 a, Vector3 b2)
            {
                var ab = new Vector2(b2.x - a.x, b2.z - a.z); var ao = new Vector2(o.x - a.x, o.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(ao, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                return (ao - ab * t).magnitude;
            }
            foreach (float deg in new[] { 45f, -45f, 65f, -65f, 25f, -25f, 0f, 90f, -90f })
                foreach (float dist in new[] { 2.2f, 1.8f, 2.6f })
                {
                    var p = at + Quaternion.Euler(0, deg, 0) * away * dist;
                    if (Clear(p) && others.All(o => Between(o, p, at) >= 0.55f)) return new Vector3(p.x, 0, p.z);
                }
            var fallback = at + away * 1.8f;
            return new Vector3(fallback.x, 0, fallback.z);
        }

        /// <summary>An exhibition day on the shop's own clock (accelerated), the six kept in stock (development stock, labelled) but one withheld.</summary>
        IEnumerator ExhibitionDay(string except)
        {
            var shop = s.Shop;
            for (int i = 0; i < 1600 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.25f);
                ExhibitionStock(shop, except);
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
                if (i % 5 == 0) yield return null;
            }
            yield return null; yield return null;
        }

        /// <summary>Keeps three of each announced creature still to buy in stock outside the spotlit places, making room by
        /// taking out a creature that was not announced (labelled development stock). Three, not two (GR-12B wave 2): ordinary
        /// visitors bought both of two between top-ups once, while the guests' walk in runs at the game's own pace.</summary>
        static void ExhibitionStock(GrowthShop shop, string except)
        {
            foreach (var o in shop.ExhibitionOrders.Where(o => !o.Bought && o.Species != except))
                for (int guard = 0; guard < 12 && shop.Stock.Count(x => x.Species == o.Species && x.ReservedBy == 0) < 3; guard++)
                {
                    var open = shop.ShopContainers.Where(c => !GrowthShop.SpotlitPlaces.Contains(c.Place)).ToList();
                    if (open.Any(c => shop.DevelopmentStock(c.Id, o.Species).Success)) continue;
                    var spare = open.SelectMany(c => c.Occupants).FirstOrDefault(x => x.ReservedBy == 0 && shop.ExhibitionOrders.All(e => e.Species != x.Species));
                    if (spare != null) shop.HolderOf(spare.Id).Occupants.Remove(spare);
                }
        }
    }
}
#endif
