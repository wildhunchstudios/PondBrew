using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-11's interface (GR.12): the objective board's sections (now, next and the long goal with the exhibition's
    /// requirements), shown on the pinboard and mirrored at the top of the journal; the Grand Exhibition's card at the
    /// hall's board in unit C (what it needs, the crowns on display, the six purchases, preparing it); the exhibition's lines
    /// on the night's receipt; and the results card, from the receipt and from the award by the shop door.
    /// </summary>
    public sealed partial class GrowthUi
    {
        static readonly string[] SpotlitNames = { "Unit B's feature place", "Unit C's feature wall, west", "Unit C's feature wall, east", "Exhibition place, west of the bay window", "Exhibition place, east of the bay window" };

        /// <summary>Validation: the HUD's clock line (the exhibition's guests on its day).</summary>
        public string HudClock => clockLabel == null ? "" : clockLabel.text;

        static string Line(bool done, string text) => (done ? "✓ " : "· ") + text;

        /// <summary>The objective board's three sections, for the pinboard and the journal.</summary>
        void BoardSections(VisualElement body)
        {
            var shop = s.Shop;
            var b = shop.Board();
            var now = Section(body, "Now · " + b.NowTitle);
            now.Add(Text("Goal: " + b.Goal));
            foreach (var (text, done) in b.Now) now.Add(Text(Line(done, text), done ? "es-req--done" : "es-req"));
            var next = Section(body, b.NextTitle);
            next.Add(Text("Gate: " + b.NextGate));
            next.Add(Text("Opens: " + b.NextOpens, "es-text--muted"));
            var longGoal = Section(body, "The long goal");
            longGoal.Add(Text(b.LongGoal));
            if (!shop.ExhibitionDone)
            {
                longGoal.Add(Text("The Grand Exhibition needs:", "es-text--muted"));
                foreach (var (text, done) in b.Exhibition) longGoal.Add(Text(Line(done, text), done ? "es-req--done" : "es-req"));
                longGoal.Add(Text("Then five different crowns on display on the hall's spotlit places, and a day for six guests.", "es-text--muted"));
            }
            else longGoal.Add(Button("See the Grand Exhibition's results", () => s.SetCard(GrowthCard.Results)));
        }

        /// <summary>The Grand Exhibition's card, at the hall's board once the hall is finished.</summary>
        void FillExhibition()
        {
            var shop = s.Shop;
            string status = shop.ExhibitionDone ? "held on day " + shop.ExhibitionResults.Day : shop.ExhibitionToday ? "today: " + shop.GuestsServed + "/" + GrowthShop.ExhibitionPurchases + " guests" :
                shop.ExhibitionPrepared ? "tomorrow" : shop.ExhibitionOpen ? "open" : "not open yet";
            var body = Head("The Grand Exhibition", status);
            if (shop.ExhibitionDone)
            {
                body.Add(Text("Held on day " + shop.ExhibitionResults.Day + ": all six guests bought what they came for, with five crowns on display to the close. The shop and the lab are yours: free play."));
                body.Add(Button("See the results", () => s.SetCard(GrowthCard.Results), true));
                Footer(body);
                return;
            }
            body.Add(Text("Prepare it at night with five different crowns on display in tanks on the hall's spotlit places. Preparing announces six creatures you know; the next day six exhibition guests come in among the ordinary visitors, each for one, and buy it at the counter."));

            var needs = Section(body, "What it needs");
            foreach (var (text, done) in shop.ExhibitionRequirements()) needs.Add(Text(Line(done, text), done ? "es-req--done" : "es-req"));

            bool set = shop.ExhibitionPrepared || shop.ExhibitionToday;
            var display = Section(body, "On display: " + GrowthShop.ExhibitionDisplays + " different crowns (" + (set ? shop.OnDisplay.Select(x => x.Species).Distinct().Count() : shop.DisplayReady) + "/" + GrowthShop.ExhibitionDisplays + ")");
            for (int i = 0; i < GrowthShop.SpotlitPlaces.Length; i++)
            {
                var c = shop.ContainerAt(GrowthShop.SpotlitPlaces[i]);
                var crowns = c == null ? null : c.Occupants.Where(o => o.Def.Kind == SpeciesKind.Crown).Select(o => o.Def.Name).Distinct().ToList();
                display.Add(Text(SpotlitNames[i] + ": " + (c == null ? "no tank yet" : c.Def.Name + (crowns.Count == 0 ? ", no crown" : " · " + string.Join(", ", crowns))),
                    crowns != null && crowns.Count > 0 ? "es-text" : "es-text--muted"));
            }
            display.Add(Text(set ? "On display, crowns cannot be sold, taken out or moved by helpers until the exhibition closes." :
                "Crowns are bred from their special pair (the journal says which). Carry them to the tanks on these places.", "es-text--muted"));

            var buys = Section(body, "The six purchases");
            if (shop.ExhibitionOrders.Count == 0) buys.Add(Text("Announced when you prepare it: six creatures you know, from at least four families. The same six until the exhibition is held.", "es-text--muted"));
            foreach (var o in shop.ExhibitionOrders)
            {
                int inStock = shop.Stock.Count(x => x.Species == o.Species && x.ReservedBy == 0);
                string line = o.Bought ? "✓ " + o.Def.Name + ": bought" : o.Missed.Length > 0 ? "✗ " + o.Def.Name + ": the guest " + o.Missed : "· " + o.Def.Name + " · " + inStock + " in stock";
                buys.Add(Text(line, o.Bought ? "es-req--done" : o.Missed.Length > 0 ? "es-card__reason" : inStock == 0 ? "es-card__reason" : "es-req"));
            }
            if (shop.ExhibitionOrders.Count > 0 && !shop.ExhibitionToday) buys.Add(Text("Ordinary visitors buy them too: keep a few of each in stock.", "es-text--muted"));

            if (shop.ExhibitionFailure.Length > 0 && !set) body.Add(Text("Last time it fell short: " + shop.ExhibitionFailure + ". Nothing was lost.", "es-card__reason"));
            var row = Row();
            if (shop.ExhibitionPrepared) row.Add(Button("Call it off (the crowns are freed)", () => s.Act(shop.CallOffExhibition)));
            else if (!shop.ExhibitionToday)
            {
                var failure = shop.PrepareCheck(out string why);
                row.Add(Button(failure == GrowthFailure.None ? "Prepare the Grand Exhibition for tomorrow" : why, () => s.Act(shop.PrepareExhibition), true, failure == GrowthFailure.None));
            }
            body.Add(row);
            Footer(body);
        }

        /// <summary>The exhibition day's lines on the night's receipt.</summary>
        void ExhibitionReceipt(VisualElement body, GrowthDaySummary d)
        {
            if (d == null || !d.Exhibition) return;
            if (d.ExhibitionWon)
            {
                var won = Text("The Grand Exhibition is a success! All six guests bought what they came for, with five crowns on display to the close.");
                won.style.unityFontStyleAndWeight = FontStyle.Bold;
                body.Add(won);
                return;
            }
            body.Add(Text("The Grand Exhibition fell short (" + d.GuestsServed + " of " + GrowthShop.ExhibitionPurchases + " guests served):"));
            foreach (var note in d.ExhibitionNotes) body.Add(Text("· " + note, "es-card__reason"));
            body.Add(Text("Nothing is lost: sales stay made and the crowns are free again. Prepare it again any night, with the same six.", "es-text--muted"));
        }

        void FillResults()
        {
            var shop = s.Shop;
            var r = shop.ExhibitionResults;
            var body = ScreenHead(r == null ? "The Grand Exhibition" : "The Grand Exhibition · day " + r.Day);
            if (r == null) body.Add(Text("Not held yet: its card is at the hall's board in unit C."));
            else
            {
                var title = Text("A success: the shop's award hangs by the door.");
                title.style.fontSize = 24; title.style.unityFontStyleAndWeight = FontStyle.Bold;
                body.Add(title);
                body.Add(Text("Six guests came and bought: " + string.Join(", ", r.Guests.Select(id => GrowthContent.Get(id).Name)) + "."));
                body.Add(Text("On display: " + string.Join(", ", r.Crowned.Select(id => GrowthContent.Get(id).Name)) + "."));
                body.Add(Text("Discovered " + r.Species + " of " + GrowthContent.SpeciesCount + " creatures and " + r.Crowns + " of " + GrowthContent.Species.Count(x => x.Kind == SpeciesKind.Crown) + " crowns · journal " + r.Journal + "/" + GrowthContent.JournalEntries + "."));
                body.Add(Text("The day's takings: " + r.Revenue + " credits from " + r.Sales + " sales · " + (r.Attempts <= 1 ? "held at the first attempt" : "held at attempt " + r.Attempts) + " · " + r.Day + " days in all."));
                body.Add(Text("Free play: the shop and the lab are yours. The long goal is the journal, every species in four colours (" + shop.Journal.Count + "/" + GrowthContent.JournalEntries + ").", "es-text--muted"));
            }
            var row = Row(); row.Add(Button("Back to the shop", () => s.SetCard(GrowthCard.None), true)); body.Add(row);
        }

        string ExhibitionSignature()
        {
            var shop = s.Shop;
            return shop.ExhibitionPrepared + "|" + shop.ExhibitionToday + "|" + shop.ExhibitionDone + "|" + shop.ExhibitionAttempts + "|" + string.Join(",", shop.ExhibitionOrders.Select(o => o.Species + o.Bought + o.Missed.Length)) + "|" + shop.DisplayReady;
        }
    }
}
