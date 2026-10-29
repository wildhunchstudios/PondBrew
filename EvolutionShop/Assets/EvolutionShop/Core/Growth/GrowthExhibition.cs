using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-11: GR.12's objective board and the Grand Exhibition in the greybox (R09.2 folded in).
    //
    // The board always shows the current era's goal and progress, the next unlock and its gate, and the long goal
    // ("Discover creatures: n / 72 · Journal: n / 288 · Exhibition: requirements"), from the first days. The Grand Exhibition
    // opens once the final shop and lab tiers stand (unit C, the exhibition hall finish, the workshop), 48 of the 72 species
    // are known from all nine families with any 5 of the 9 crowns, and a full day has run on the line. It is prepared at
    // night at the hall's board, with five different crowns on display in tanks on the hall's five spotlit places (the
    // user's choice, 2026-10-24): on display they are protected (an exhibition reservation: visitors never take them,
    // helpers never move them, the player cannot take them out). Preparing announces six purchases, six species the player
    // knows from at least four families (the user's choice); the next day six exhibition guests come in among the ordinary
    // visitors, each wanting one, and buy it through the usual queue and checkout. All six bought with the displays kept to
    // close: the exhibition is held, once, with a celebration, an award by the shop door and a results card (the user's
    // choice), then free play in the same save. Anything short of that fails only that attempt: sales stay made, nothing
    // else changes, the displays are released, and it can be prepared again any night with the same six.

    /// <summary>GR-11: one of the exhibition's six announced purchases.</summary>
    public sealed class GrowthExhibitionOrder
    {
        public string Species;
        public bool Bought;
        /// <summary>Why the guest for it went without, in the attempt under way or the last one ("" when not).</summary>
        public string Missed = "";
        /// <summary>The exhibition guest in the shop for it now (0 when none: visitors are not saved, so a reload sends a new one).</summary>
        public int Guest;
        public SpeciesDef Def => GrowthContent.Get(Species);
    }

    /// <summary>GR-11: what the results card shows once the Grand Exhibition has been held.</summary>
    public sealed class GrowthExhibitionResults
    {
        public int Day, Species, Crowns, Journal, Revenue, Sales, Attempts;
        public string[] Guests = new string[0], Crowned = new string[0];
    }

    /// <summary>GR-11: the objective board's contents (GR.12), shared by the pinboard, the journal and the HUD.</summary>
    public sealed class GrowthBoard
    {
        public string NowTitle, Goal, NextTitle, NextGate, NextOpens, LongGoal;
        public List<(string text, bool done)> Now = new List<(string, bool)>(), Exhibition = new List<(string, bool)>();
    }

    public sealed partial class GrowthShop
    {
        /// <summary>A specimen on exhibition display carries this reservation: no visitor, helper or hand takes it.</summary>
        public const int ExhibitionReservation = -1;
        /// <summary>The hall's five spotlit places (GR-10): unit B's feature place, unit C's feature wall and the two exhibition places.</summary>
        public static readonly string[] SpotlitPlaces = { "b4", "c1", "c2", "e1", "e2" };
        public const int ExhibitionPurchases = 6, ExhibitionFamilies = 4, ExhibitionDisplays = 5;
        /// <summary>Exhibition guests wait three times as long in the queue as ordinary visitors.</summary>
        public const float GuestPatience = QueuePatience * 3;
        public const string GuestType = "guest";

        public bool ExhibitionPrepared, ExhibitionToday, ExhibitionDone;
        public int ExhibitionAttempts;
        /// <summary>Why the last attempt fell short ("" when none has, or once the exhibition is held).</summary>
        public string ExhibitionFailure = "";
        public readonly List<GrowthExhibitionOrder> ExhibitionOrders = new List<GrowthExhibitionOrder>();
        public GrowthExhibitionResults ExhibitionResults;

        /// <summary>When the i-th guest comes in, as a share of the day (the first early, the last with a third of it left).</summary>
        public static float GuestArrival(int i) => 0.08f + 0.12f * i;

        public int CrownsDiscovered => Discovered.Count(d => GrowthContent.Get(d).Kind == SpeciesKind.Crown);
        public int FamiliesDiscovered => GrowthContent.Families.Count(f => Discovered.Any(d => GrowthContent.Get(d).Family == f.Id));

        /// <summary>GR.12's requirements for the Grand Exhibition, each with its progress, shown from the first days.</summary>
        public List<(string text, bool done)> ExhibitionRequirements() => new List<(string, bool)>
        {
            ("The final shop: shop unit C open and the exhibition hall finished", UnitC && Hall),
            ("The final lab: the workshop open", Workshop),
            ("Discover " + GrowthContent.ExhibitionSpecies + " of the " + GrowthContent.SpeciesCount + " creatures (" + Math.Min(GrowthContent.ExhibitionSpecies, Discovered.Count) + "/" + GrowthContent.ExhibitionSpecies + ")", Discovered.Count >= GrowthContent.ExhibitionSpecies),
            ("… from all " + GrowthContent.Families.Length + " families (" + FamiliesDiscovered + "/" + GrowthContent.Families.Length + ")", FamiliesDiscovered >= GrowthContent.Families.Length),
            ("Discover " + GrowthContent.ExhibitionCrowns + " of the " + GrowthContent.Species.Count(s => s.Kind == SpeciesKind.Crown) + " crowns (" + Math.Min(GrowthContent.ExhibitionCrowns, CrownsDiscovered) + "/" + GrowthContent.ExhibitionCrowns + ")", CrownsDiscovered >= GrowthContent.ExhibitionCrowns),
            ("Run a full day on the line", LineDays > 0),
        };

        public bool ExhibitionOpen => ExhibitionRequirements().All(r => r.done);

        /// <summary>Crowns in tanks on the spotlit places (whether or not the exhibition is prepared).</summary>
        public IEnumerable<GrowthSpecimen> CrownsOnSpotlitPlaces => ShopContainers.Where(c => SpotlitPlaces.Contains(c.Place)).OrderBy(c => Array.IndexOf(SpotlitPlaces, c.Place))
            .SelectMany(c => c.Occupants).Where(s => s.Def.Kind == SpeciesKind.Crown);

        /// <summary>Different crowns ready for display: on a spotlit place and not reserved by a visitor.</summary>
        public int DisplayReady => CrownsOnSpotlitPlaces.Where(s => s.ReservedBy == 0 || s.ReservedBy == ExhibitionReservation).Select(s => s.Species).Distinct().Count();

        /// <summary>The specimens on exhibition display (reserved for it) while it is prepared or under way.</summary>
        public IEnumerable<GrowthSpecimen> OnDisplay => Stock.Where(s => s.ReservedBy == ExhibitionReservation);

        public static bool IsOnDisplay(GrowthSpecimen s) => s.ReservedBy == ExhibitionReservation;

        public int GuestsServed => ExhibitionOrders.Count(o => o.Bought);

        /// <summary>Why the exhibition cannot be prepared now (None when it can).</summary>
        public GrowthFailure PrepareCheck(out string why)
        {
            why = null;
            if (ExhibitionDone) { why = "The Grand Exhibition has been held: the shop is in free play"; return GrowthFailure.MaxLevel; }
            if (ExhibitionToday) { why = "The Grand Exhibition is under way"; return GrowthFailure.Busy; }
            if (ExhibitionPrepared) { why = "It is prepared for tomorrow: open the shop to host it"; return GrowthFailure.Busy; }
            var missing = ExhibitionRequirements().FirstOrDefault(r => !r.done);
            if (missing.text != null) { why = "Not yet open: " + missing.text; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "The exhibition is prepared at night"; return GrowthFailure.NotAtNight; }
            if (DisplayReady < ExhibitionDisplays)
            { why = "Put " + ExhibitionDisplays + " different crowns on display in tanks on the spotlit places (" + DisplayReady + "/" + ExhibitionDisplays + ")"; return GrowthFailure.NotEnough; }
            return GrowthFailure.None;
        }

        /// <summary>Prepares tomorrow's Grand Exhibition: the crowns on the spotlit places go on display (protected) and six
        /// purchases are announced (the same six on every attempt until it is held).</summary>
        public GrowthResult PrepareExhibition()
        {
            var failure = PrepareCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            foreach (var s in CrownsOnSpotlitPlaces.Where(x => x.ReservedBy == 0)) s.ReservedBy = ExhibitionReservation;
            if (ExhibitionOrders.Count == 0) ExhibitionOrders.AddRange(AnnouncePurchases().Select(id => new GrowthExhibitionOrder { Species = id }));
            foreach (var o in ExhibitionOrders) { o.Bought = false; o.Missed = ""; o.Guest = 0; }
            ExhibitionPrepared = true;
            Raise(GrowthEventKind.Exhibition, "The Grand Exhibition is tomorrow: six guests are coming for " + PurchaseList());
            return GrowthResult.Ok();
        }

        /// <summary>Calls off a prepared exhibition at night: the displays are released, the six stay announced.</summary>
        public GrowthResult CallOffExhibition()
        {
            if (!ExhibitionPrepared) return GrowthResult.Fail(GrowthFailure.NotEnough, "Nothing is prepared");
            if (Phase != GrowthPhase.Night) return GrowthResult.Fail(GrowthFailure.NotAtNight);
            ReleaseDisplays();
            ExhibitionPrepared = false;
            Raise(GrowthEventKind.Exhibition, "The Grand Exhibition is called off: the crowns are free to move");
            return GrowthResult.Ok();
        }

        public string PurchaseList() => string.Join(", ", ExhibitionOrders.Select(o => o.Def.Name));

        /// <summary>Six species the player knows (never a crown), one from each family in turn, starting from a family that
        /// depends on the day: from six different families when six have a non-crown known, never fewer than four.</summary>
        public List<string> AnnouncePurchases()
        {
            var byFamily = GrowthContent.Families.Select(f => GrowthContent.Species.Where(s => s.Family == f.Id && s.Kind != SpeciesKind.Crown && Discovered.Contains(s.Id)).Select(s => s.Id).ToList())
                .Where(l => l.Count > 0).ToList();
            var chosen = new List<string>();
            if (byFamily.Count == 0) return chosen;
            int start = Day % byFamily.Count;
            for (int round = 0; chosen.Count < ExhibitionPurchases && round < 8; round++)
                for (int k = 0; k < byFamily.Count && chosen.Count < ExhibitionPurchases; k++)
                {
                    var family = byFamily[(start + k) % byFamily.Count];
                    var left = family.Where(id => !chosen.Contains(id)).ToList();
                    if (left.Count > 0) chosen.Add(left[(int)(Hash(Day * 131 + (start + k) * 17 + round) % (uint)left.Count)]);
                }
            return chosen;
        }

        void ReleaseDisplays() { foreach (var s in Stock.Where(IsOnDisplay)) s.ReservedBy = 0; }

        /// <summary>The prepared exhibition starts as the shop opens: the six guests come during the day.</summary>
        void StartExhibitionDay()
        {
            if (!ExhibitionPrepared) return;
            ExhibitionPrepared = false;
            ExhibitionToday = true;
            foreach (var o in ExhibitionOrders) { o.Bought = false; o.Missed = ""; o.Guest = 0; }
            if (Today != null) Today.Exhibition = true;
            Raise(GrowthEventKind.Exhibition, "The Grand Exhibition: six guests are coming today for " + PurchaseList());
        }

        /// <summary>Each guest comes in at their time; one whose guest went without does not get another today.</summary>
        void TickGuests()
        {
            if (!ExhibitionToday) return;
            float elapsed = GrowthContent.DaySeconds - DaySecondsLeft;
            for (int i = 0; i < ExhibitionOrders.Count; i++)
            {
                var o = ExhibitionOrders[i];
                if (o.Bought || o.Guest != 0 || o.Missed.Length > 0 || elapsed < GuestArrival(i) * GrowthContent.DaySeconds) continue;
                GuestArrives(i);
            }
        }

        void GuestArrives(int index)
        {
            var o = ExhibitionOrders[index];
            var v = new GrowthVisitor { Id = nextId++, Type = GuestType, Wants = o.Species, Order = index, Budget = Price(o.Species) * 3, State = GrowthVisitorState.Browsing };
            // The guest looks into the tanks that hold what they came for, the most appealing first; with none, the most
            // appealing tank, and then they go without.
            var holding = ShopContainers.Where(c => c.Occupants.Any(s => s.Species == o.Species && s.ReservedBy == 0))
                .OrderByDescending(Appeal).ThenBy(c => c.Place, StringComparer.Ordinal).Take(MostBrowsed).Select(c => c.Id).ToList();
            if (holding.Count == 0) holding.AddRange(ShopContainers.OrderByDescending(Appeal).ThenBy(c => c.Place, StringComparer.Ordinal).Take(1).Select(c => c.Id));
            v.Plan.AddRange(holding);
            v.ContainerId = v.Plan.Count > 0 ? v.Plan[0] : null;
            if (v.ContainerId != null) v.BrowseFor = GrowthSpaces.FirstLook(Container(v.ContainerId).Place);
            o.Guest = v.Id;
            visitorsToday++;
            if (Today != null) { Today.Visitors++; Today.Guests++; }
            Visitors.Add(v);
        }

        /// <summary>A guest who goes without: their purchase is missed for this attempt (no reputation is lost for it).</summary>
        void GuestLeaves(GrowthVisitor v, GrowthVisitOutcome outcome)
        {
            if (v.Order < 0 || v.Order >= ExhibitionOrders.Count) return;
            var o = ExhibitionOrders[v.Order];
            if (o.Guest == v.Id) o.Guest = 0;
            if (outcome == GrowthVisitOutcome.Bought) return;
            o.Missed = outcome == GrowthVisitOutcome.NothingAffordable ? "found no " + o.Def.Name + " in stock" :
                outcome == GrowthVisitOutcome.TiredOfWaiting ? "got tired of waiting in the queue" : "was still waiting at close";
            Raise(GrowthEventKind.Refusal, "The exhibition guest for a " + o.Def.Name + " " + o.Missed);
        }

        void GuestBuys(GrowthVisitor v)
        {
            if (v.Order < 0 || v.Order >= ExhibitionOrders.Count) return;
            var o = ExhibitionOrders[v.Order];
            o.Bought = true; o.Guest = 0;
            if (Today != null) Today.GuestsServed++;
            Raise(GrowthEventKind.Exhibition, "Exhibition guest " + GuestsServed + "/" + ExhibitionPurchases + ": a " + o.Def.Name + " bought");
        }

        /// <summary>At the exhibition day's close: held when all six were bought with five crowns still on display; otherwise
        /// the attempt fails and says why. Either way the displays are released.</summary>
        void EndExhibitionDay()
        {
            if (!ExhibitionToday) return;
            ExhibitionToday = false;
            ExhibitionAttempts++;
            var reasons = ExhibitionOrders.Where(o => !o.Bought).Select(o => "the guest for a " + o.Def.Name + " " + (o.Missed.Length > 0 ? o.Missed : "never came")).ToList();
            int shown = OnDisplay.Where(s => s.Def.Kind == SpeciesKind.Crown && SpotlitPlaces.Contains(HolderOf(s.Id)?.Place)).Select(s => s.Species).Distinct().Count();
            if (shown < ExhibitionDisplays) reasons.Add("only " + shown + " of " + ExhibitionDisplays + " crowns were still on display");
            // GR-22: closed before its time, the day says so first.
            if (reasons.Count > 0 && LastDay != null && LastDay.ClosedEarly) reasons.Insert(0, "the shop closed with " + ClockText(LastDay.SecondsLeft) + " to go");
            var crowned = OnDisplay.Select(s => s.Species).Distinct().ToArray();
            ReleaseDisplays();
            if (LastDay != null) { LastDay.Exhibition = true; LastDay.ExhibitionNotes.Clear(); LastDay.ExhibitionNotes.AddRange(reasons); }
            if (reasons.Count == 0)
            {
                ExhibitionDone = true;
                ExhibitionFailure = "";
                ExhibitionResults = new GrowthExhibitionResults
                {
                    Day = Day, Species = Discovered.Count, Crowns = CrownsDiscovered, Journal = Journal.Count, Revenue = LastDay?.Revenue ?? 0, Sales = LastDay?.Sales ?? 0,
                    Attempts = ExhibitionAttempts, Guests = ExhibitionOrders.Select(o => o.Species).ToArray(), Crowned = crowned,
                };
                if (LastDay != null) LastDay.ExhibitionWon = true;
                Raise(GrowthEventKind.ExhibitionWon, "The Grand Exhibition is a success!");
            }
            else
            {
                ExhibitionFailure = string.Join("; ", reasons);
                Raise(GrowthEventKind.ExhibitionFailed, "The exhibition fell short: " + ExhibitionFailure + ". Prepare it again any night: nothing is lost.");
            }
        }

        /// <summary>Era 5's lines on the board and the HUD: the final tiers, the requirements, the displays and the day itself;
        /// once it is held, free play and the journal.</summary>
        void ExhibitionObjectives(List<(string, bool)> list)
        {
            if (ExhibitionDone)
            {
                list.Add(("The Grand Exhibition was held on day " + ExhibitionResults.Day + ": the shop is in free play", true));
                list.Add(("Fill the journal: every species in four colours (" + Journal.Count + "/" + GrowthContent.JournalEntries + ")", Journal.Count >= GrowthContent.JournalEntries));
                return;
            }
            if (!UnitC) list.Add(("Open shop unit C behind unit B (" + UnitCDef.Credits + " credits, at night)", false));
            list.Add(("Finish the exhibition hall: its plans hang in unit C (" + HallDef.Credits + " credits, at night)", Hall));
            foreach (var r in ExhibitionRequirements().Skip(2)) list.Add(r);
            bool set = ExhibitionPrepared || ExhibitionToday;
            list.Add(("Put " + ExhibitionDisplays + " different crowns on display in tanks on the spotlit places (" + (set ? ExhibitionDisplays : Math.Min(ExhibitionDisplays, DisplayReady)) + "/" + ExhibitionDisplays + ")", set || DisplayReady >= ExhibitionDisplays));
            list.Add(("Prepare the Grand Exhibition at the hall's board in unit C, at night" + (ExhibitionAttempts > 0 && !set ? " (again: nothing was lost)" : ""), set));
            list.Add(("Host it: six exhibition guests buy what they came for (" + (ExhibitionToday ? "today " + GuestsServed + "/" + ExhibitionPurchases : ExhibitionPrepared ? "tomorrow" : "the day after you prepare it") + ")", false));
        }

        /// <summary>The objective board (GR.12): now, next and the long goal.</summary>
        public GrowthBoard Board()
        {
            var board = new GrowthBoard
            {
                NowTitle = ExhibitionDone ? "Free play" : "Era " + Era + " · " + GrowthContent.Eras[Era - 1].Name,
                Goal = EraGoal,
                Now = Objectives(),
                Exhibition = ExhibitionRequirements(),
            };
            if (Era < GrowthContent.Eras.Length)
            {
                var next = GrowthContent.Eras[Era];
                board.NextTitle = "Next: era " + next.Number + " · " + next.Name;
                board.NextGate = next.Gate;
                board.NextOpens = next.Opens;
            }
            else if (!ExhibitionDone)
            {
                board.NextTitle = "Next: the Grand Exhibition";
                board.NextGate = "every exhibition requirement below, then five crowns on display";
                board.NextOpens = "six guests come for six creatures you know; then free play in the same shop";
            }
            else
            {
                board.NextTitle = "Next: free play";
                board.NextGate = "nothing: the shop and the lab are yours";
                board.NextOpens = "every species in four colours, the journal's " + GrowthContent.JournalEntries + " entries";
            }
            int met = board.Exhibition.Count(r => r.done);
            board.LongGoal = "Discover creatures: " + Discovered.Count + " / " + GrowthContent.SpeciesCount + " · Journal: " + Journal.Count + " / " + GrowthContent.JournalEntries +
                " · Exhibition: " + (ExhibitionDone ? "held on day " + ExhibitionResults.Day : met + " of " + board.Exhibition.Count + " requirements");
            return board;
        }

        // ---------------------------------------------------------------- the save (version 10)

        void SaveExhibition(GrowthSave s)
        {
            s.exhibitionPrepared = ExhibitionPrepared; s.exhibitionToday = ExhibitionToday; s.exhibitionDone = ExhibitionDone;
            s.exhibitionAttempts = ExhibitionAttempts; s.exhibitionFailure = ExhibitionFailure ?? "";
            s.exhibitionSpecies = ExhibitionOrders.Select(o => o.Species).ToArray();
            s.exhibitionBought = ExhibitionOrders.Select(o => o.Bought).ToArray();
            s.exhibitionMissed = ExhibitionOrders.Select(o => o.Missed ?? "").ToArray();
            s.exhibitionDisplay = OnDisplay.Select(x => x.Id).ToArray();
            if (ExhibitionResults != null)
            {
                var r = ExhibitionResults;
                s.resultDay = r.Day; s.resultSpecies = r.Species; s.resultCrowns = r.Crowns; s.resultJournal = r.Journal; s.resultRevenue = r.Revenue; s.resultSales = r.Sales;
                s.resultAttempts = r.Attempts; s.resultGuests = r.Guests; s.resultCrowned = r.Crowned;
            }
        }

        /// <summary>A save's exhibition: six known non-crown purchases, displays only while prepared or under way (crowns in
        /// tanks on the spotlit places, five different), prepared only at night and under way only by day, and a held one's results.</summary>
        static string RestoreExhibition(GrowthShop shop, GrowthSave s)
        {
            if (s.version < 10) return null;
            var species = s.exhibitionSpecies ?? new string[0];
            if (species.Length != 0 && species.Length != ExhibitionPurchases) return "The exhibition's purchases are not six";
            for (int i = 0; i < species.Length; i++)
            {
                var def = GrowthContent.Get(species[i]);
                if (def == null || def.Kind == SpeciesKind.Crown) return "Unknown exhibition purchase " + species[i];
                shop.ExhibitionOrders.Add(new GrowthExhibitionOrder
                {
                    Species = species[i], Bought = s.exhibitionBought != null && i < s.exhibitionBought.Length && s.exhibitionBought[i],
                    Missed = s.exhibitionMissed != null && i < s.exhibitionMissed.Length ? s.exhibitionMissed[i] ?? "" : "",
                });
            }
            shop.ExhibitionPrepared = s.exhibitionPrepared; shop.ExhibitionToday = s.exhibitionToday; shop.ExhibitionDone = s.exhibitionDone;
            shop.ExhibitionAttempts = Math.Max(0, s.exhibitionAttempts); shop.ExhibitionFailure = s.exhibitionFailure ?? "";
            bool set = shop.ExhibitionPrepared || shop.ExhibitionToday;
            if (shop.ExhibitionPrepared && shop.ExhibitionToday) return "An exhibition both prepared and under way";
            if (shop.ExhibitionPrepared && shop.Phase != GrowthPhase.Night || shop.ExhibitionToday && shop.Phase != GrowthPhase.Day) return "An exhibition at the wrong time of day";
            if (set && (shop.ExhibitionDone || species.Length == 0 || !shop.ExhibitionOpen)) return "An exhibition prepared before it opened";
            var display = s.exhibitionDisplay ?? new int[0];
            if (!set && display.Length > 0) return "Crowns on display with no exhibition";
            foreach (int id in display)
            {
                var c = shop.HolderOf(id);
                var sp = c?.Occupants.First(o => o.Id == id);
                if (c == null || !SpotlitPlaces.Contains(c.Place) || sp.Def.Kind != SpeciesKind.Crown) return "A creature on display that is not a crown on a spotlit place";
                sp.ReservedBy = ExhibitionReservation;
            }
            if (set && shop.OnDisplay.Select(x => x.Species).Distinct().Count() < ExhibitionDisplays) return "Fewer than " + ExhibitionDisplays + " crowns on display";
            if (shop.ExhibitionDone)
            {
                if (s.resultDay < 1) return "A Grand Exhibition held without its results";
                shop.ExhibitionResults = new GrowthExhibitionResults
                {
                    Day = s.resultDay, Species = s.resultSpecies, Crowns = s.resultCrowns, Journal = s.resultJournal, Revenue = s.resultRevenue, Sales = s.resultSales,
                    Attempts = s.resultAttempts, Guests = s.resultGuests ?? new string[0], Crowned = s.resultCrowned ?? new string[0],
                };
            }
            return null;
        }
    }
}
