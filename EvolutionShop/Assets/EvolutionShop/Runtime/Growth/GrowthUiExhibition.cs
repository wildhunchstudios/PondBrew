using System.Linq;
using System.Text.RegularExpressions;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-11's interface (GR.12) in GR-13's style: the objective board as three pinned papers (now, the next era, and the
    /// long goal with the Grand Exhibition's needs as rings), on the pinboard and at the top of the journal; the Grand
    /// Exhibition's card at the hall's board in unit C (Prepare, Display, Guests); the exhibition's lines on the night's
    /// receipt; and the results, from the receipt and from the award by the shop door.
    /// </summary>
    public sealed partial class GrowthUi
    {
        static readonly string[] SpotlitNames = { "Unit B's feature place", "Unit C's feature wall, west", "Unit C's feature wall, east", "Exhibition place, west of the bay window", "Exhibition place, east of the bay window" };

        /// <summary>Validation: the HUD's clock line (the exhibition's guests on its day).</summary>
        public string HudClock => clockLabel == null ? "" : clockLabel.text;

        /// <summary>A goal as a tick box and its words (GR-13: ticks, not pills).</summary>
        static VisualElement TickRow(bool done, string text)
        {
            var row = Box("gr-check");
            var box = Box("gr-check__box");
            if (done) { box.AddToClassList("gr-check__box--on"); box.Add(L("✓", "gr-check__mark")); }
            row.Add(box);
            row.Add(L(text, "gr-check__text"));
            return row;
        }

        static VisualElement Paper(string sub, string title)
        {
            var paper = Box("gr-paper");
            paper.Add(Box("gr-paper__pin"));
            paper.Add(L(sub, "gr-paper__sub"));
            if (title != null) paper.Add(L(title, "gr-paper__title"));
            return paper;
        }

        static readonly Regex Count = new Regex(@"(\w+) \((\d+)/(\d+)\)$");

        /// <summary>The objective board as three pinned papers, for the pinboard and the journal.</summary>
        void BoardSections(VisualElement body)
        {
            var shop = s.Shop;
            var b = shop.Board();
            var papers = Box("gr-papers");
            var now = Paper("Now · " + b.NowTitle, b.Goal);
            foreach (var (text, done) in b.Now) now.Add(TickRow(done, text));
            papers.Add(now);
            var next = Paper(b.NextTitle, null);
            next.tooltip = "Gate: " + b.NextGate;
            next.Add(L("Opens when you have", "gr-hint"));
            var gate = Box("gr-chips");
            foreach (var part in b.NextGate.Split(new[] { ", " }, System.StringSplitOptions.RemoveEmptyEntries)) gate.Add(Chip(part));
            next.Add(gate);
            var opens = L("Then: " + b.NextOpens, "gr-hint"); opens.style.marginTop = 12;
            next.Add(opens);
            papers.Add(next);
            var longGoal = Paper("The long goal", "The Grand Exhibition");
            longGoal.Add(L(b.LongGoal, "gr-hint"));
            if (!shop.ExhibitionDone)
            {
                var rings = Box("gr-rings");
                var rest = Box("gr-section"); rest.style.marginTop = 6;
                foreach (var (text, done) in b.Exhibition)
                {
                    var m = Count.Match(text);
                    if (!m.Success) { rest.Add(TickRow(done, text)); continue; }
                    int have = int.Parse(m.Groups[2].Value), need = int.Parse(m.Groups[3].Value);
                    var cell = Box("gr-goalring"); cell.tooltip = text;
                    var ring = new GrowthRing(true);
                    ring.Set(need == 0 ? 1 : (float)have / need, have.ToString());
                    cell.Add(ring);
                    cell.Add(L("of " + need + " " + m.Groups[1].Value, "gr-hint"));
                    rings.Add(cell);
                }
                longGoal.Add(rings);
                longGoal.Add(rest);
                longGoal.Add(L("Then five different crowns on display on the hall's spotlit places, and a day for six guests.", "gr-hint"));
            }
            else longGoal.Add(Button("See the Grand Exhibition's results", () => s.SetCard(GrowthCard.Results), true));
            papers.Add(longGoal);
            body.Add(papers);
        }

        /// <summary>GR-13's Grand Exhibition card, at the hall's board once the hall is finished: Prepare, Display and Guests.</summary>
        void FillExhibition()
        {
            var shop = s.Shop;
            string status = shop.ExhibitionDone ? "held on day " + shop.ExhibitionResults.Day : shop.ExhibitionToday ? "today: " + shop.GuestsServed + "/" + GrowthShop.ExhibitionPurchases + " guests" :
                shop.ExhibitionPrepared ? "tomorrow" : shop.ExhibitionOpen ? "open" : "not open yet";
            const string rules = "Prepare it at night with five different crowns on display in tanks on the hall's spotlit places. Preparing announces six creatures you know; the next day six exhibition guests come in among the ordinary visitors, each for one, and buy it at the counter. On display, crowns cannot be sold, taken out or moved by helpers until it closes.";
            if (shop.ExhibitionDone)
            {
                var done = Head("The Grand Exhibition", status, null, rules);
                done.Add(L("Held on day " + shop.ExhibitionResults.Day + ": all six guests bought what they came for, with five crowns on display to the close. The shop and the lab are yours: free play.", "gr-hint"));
                var see = Wide("See the results", () => s.SetCard(GrowthCard.Results), true, true); see.style.marginTop = 12;
                done.Add(see);
                Footer(done);
                return;
            }
            var body = Head("The Grand Exhibition", status, "Prepare", rules);
            body.Add(L("What it needs", "gr-label"));
            foreach (var (text, done) in shop.ExhibitionRequirements()) body.Add(TickRow(done, text));
            bool set = shop.ExhibitionPrepared || shop.ExhibitionToday;
            int ready = set ? shop.OnDisplay.Select(x => x.Species).Distinct().Count() : shop.DisplayReady;
            body.Add(TickRow(ready >= GrowthShop.ExhibitionDisplays, "Five different crowns on the spotlit places (" + ready + "/" + GrowthShop.ExhibitionDisplays + ")"));
            if (shop.ExhibitionFailure.Length > 0 && !set) { var fell = Box("gr-callout"); fell.style.marginTop = 10; fell.Add(L("Last time it fell short: " + shop.ExhibitionFailure + ". Nothing was lost.", "gr-hint")); body.Add(fell); }
            if (shop.ExhibitionPrepared)
            {
                var off = Button("Call it off (the crowns are freed)", () => s.Act(shop.CallOffExhibition));
                off.RemoveFromClassList("es-button--secondary"); off.AddToClassList("es-button--danger"); off.style.marginTop = 12;
                body.Add(off);
            }
            else if (!shop.ExhibitionToday)
            {
                var failure = shop.PrepareCheck(out string why);
                var prepare = Wide("Prepare the Grand Exhibition for tomorrow", () => s.Act(shop.PrepareExhibition), true, failure == GrowthFailure.None, why);
                prepare.style.marginTop = 12;
                body.Add(prepare);
            }
            // Display: the five spotlit places and the crowns in their tanks.
            var display = AddTab("Display");
            display.Add(L("On display: " + GrowthShop.ExhibitionDisplays + " different crowns (" + ready + "/" + GrowthShop.ExhibitionDisplays + ")", "gr-label"));
            for (int i = 0; i < GrowthShop.SpotlitPlaces.Length; i++)
            {
                var c = shop.ContainerAt(GrowthShop.SpotlitPlaces[i]);
                var crowns = c == null ? null : c.Occupants.Where(o => o.Def.Kind == SpeciesKind.Crown).Select(o => o.Def.Name).Distinct().ToList();
                bool has = crowns != null && crowns.Count > 0;
                display.Add(Item(SpotlitNames[i], c == null ? "no tank yet" : c.Def.Name + (has ? " · " + string.Join(", ", crowns) : ", no crown"), Pill(has ? "Crowned" : c == null ? "No tank" : "No crown", has ? 0 : 2)));
            }
            display.Add(L(set ? "On display, crowns cannot be sold, taken out or moved by helpers until the exhibition closes." :
                "Crowns are bred from their special pair (the journal says which). Carry them to the tanks on these places.", "gr-hint"));
            // Guests: the six purchases and how each stands.
            var guests = AddTab("Guests");
            guests.Add(L("The six purchases", "gr-label"));
            if (shop.ExhibitionOrders.Count == 0) guests.Add(L("Announced when you prepare it: six creatures you know, from at least four families. The same six until the exhibition is held.", "gr-hint"));
            foreach (var o in shop.ExhibitionOrders)
            {
                int inStock = shop.Stock.Count(x => x.Species == o.Species && x.ReservedBy == 0);
                var row = Item(o.Def.Name, o.Bought ? "bought" : o.Missed.Length > 0 ? "the guest " + o.Missed : inStock + " in stock",
                    Pill(o.Bought ? "Bought" : o.Missed.Length > 0 ? "Missed" : inStock == 0 ? "None in stock" : "Ready", o.Bought || o.Missed.Length == 0 && inStock > 0 ? 0 : 2));
                var g = new GrowthGlyph(GrowthCreatureView.ColourOf(o.Def)); g.AddToClassList("gr-result__glyph"); row.Insert(0, g);
                guests.Add(row);
            }
            if (shop.ExhibitionOrders.Count > 0 && !shop.ExhibitionToday) guests.Add(L("Ordinary visitors buy them too: keep a few of each in stock.", "gr-hint"));
            Footer(body);
        }

        /// <summary>The exhibition day's lines on the night's receipt.</summary>
        void ExhibitionReceipt(VisualElement body, GrowthDaySummary d)
        {
            if (d == null || !d.Exhibition) return;
            if (d.ExhibitionWon)
            {
                var won = Box("gr-callout");
                won.Add(L("The Grand Exhibition is a success! All six guests bought what they came for, with five crowns on display to the close.", "gr-strong"));
                body.Add(won);
                return;
            }
            var fell = Box("gr-callout");
            fell.Add(L("The Grand Exhibition fell short (" + d.GuestsServed + " of " + GrowthShop.ExhibitionPurchases + " guests served):", "gr-strong"));
            foreach (var note in d.ExhibitionNotes) fell.Add(TickRow(false, note));
            fell.Add(L("Nothing is lost: sales stay made and the crowns are free again. Prepare it again any night, with the same six.", "gr-hint"));
            body.Add(fell);
        }

        /// <summary>The results, a planning view: the six guests, the crowns, the shop's numbers, and free play.</summary>
        void FillResults()
        {
            var shop = s.Shop;
            var r = shop.ExhibitionResults;
            var body = Notebook(r == null ? "The Grand Exhibition" : "The Grand Exhibition · day " + r.Day, r == null ? "not held yet" : "a success: the shop's award hangs by the door");
            if (r == null) body.Add(L("Not held yet: its card is at the hall's board in unit C.", "gr-hint"));
            else
            {
                var columns = Box("gr-papers");
                var guests = Paper("Six guests came and bought", null);
                foreach (var id in r.Guests) guests.Add(TickRow(true, GrowthContent.Get(id).Name));
                columns.Add(guests);
                var crowns = Paper("On display", null);
                foreach (var id in r.Crowned) crowns.Add(TickRow(true, GrowthContent.Get(id).Name));
                columns.Add(crowns);
                var numbers = Paper("The shop", null);
                numbers.Add(Stat(r.Species + " of " + GrowthContent.SpeciesCount, "creatures discovered"));
                numbers.Add(L(r.Crowns + " of " + GrowthContent.Species.Count(x => x.Kind == SpeciesKind.Crown) + " crowns · journal " + r.Journal + "/" + GrowthContent.JournalEntries, "gr-hint"));
                numbers.Add(L("The day's takings: " + r.Revenue + " credits from " + r.Sales + " sales", "gr-hint"));
                numbers.Add(L((r.Attempts <= 1 ? "held at the first attempt" : "held at attempt " + r.Attempts) + " · " + r.Day + " days in all", "gr-hint"));
                columns.Add(numbers);
                body.Add(columns);
                var free = L("Free play: the shop and the lab are yours. The long goal is the journal, every species in four colours (" + shop.Journal.Count + "/" + GrowthContent.JournalEntries + ").", "gr-hint");
                free.style.marginTop = 16;
                body.Add(free);
            }
            NotebookFooter("Back to the shop", () => s.SetCard(GrowthCard.None));
        }

        string ExhibitionSignature()
        {
            var shop = s.Shop;
            return shop.ExhibitionPrepared + "|" + shop.ExhibitionToday + "|" + shop.ExhibitionDone + "|" + shop.ExhibitionAttempts + "|" + string.Join(",", shop.ExhibitionOrders.Select(o => o.Species + o.Bought + o.Missed.Length)) + "|" + shop.DisplayReady;
        }
    }
}
