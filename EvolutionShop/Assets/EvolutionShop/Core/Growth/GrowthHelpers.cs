using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-09 (GR.10): the helpers. Pip (cashier), Nori (stocker) and Moss (technician) are earned by stars and an achievement,
    // hired for a fee, paid a wage each duty period and trained at night through three levels. §6.3's paid-duty rules: a duty
    // period is a night and the trading day after it; the wage is charged once, when the period starts (or when the helper
    // is hired or comes back on duty within it); short of credits a helper stays home and says so; never a debt, never a
    // helper lost. The user chose (2026-10-23) figures at work (their work runs on timers here, along real walking paths the
    // runtime's figures follow) and helpers who fill in before the line: Nori keeps the shop's tanks at the stock targets
    // (hatchlings from the tray while no chute is laid, offspring from the nursery, creatures from the racks), Moss runs one
    // known recipe by hand from the growers before the auto-doser, at level 2 keeps a known pair breeding instead, and at
    // level 3 does both; Pip serves at the counter, and the counter's second till (level 4) lets the player serve beside
    // Pip. Neither helpers nor machines ever choose an undiscovered mix or pair (GR.8).

    /// <summary>
    /// Where helpers walk (GR-09): the rooms of the greybox (shop units A, B and C, the back room, the lab annex, the
    /// workshop) and the points they work at. In the lab a path runs through the doorways between two points' rooms; in the
    /// shop it keeps to the visitors' walking network (GR-10, GrowthSpaces), joined from the back room's door.
    /// </summary>
    public static class GrowthWalk
    {
        public const float Speed = 2f, Handle = 0.6f;
        public enum Room { A, B, C, Back, Annex, Workshop }

        public static Room RoomOf((float x, float z) p) =>
            p.z < 4 ? (p.x < 4 ? Room.A : Room.B) : p.z < 9 ? (p.x < 2 ? Room.Back : Room.C) : p.x < 4 ? Room.Annex : Room.Workshop;

        static bool InShop(Room r) => r == Room.A || r == Room.B || r == Room.C;

        /// <summary>The back room's door into unit A: a point on either side of it (walked through straight).</summary>
        static readonly (float x, float z) BackDoorShop = (0.8f, 3.5f), BackDoorLab = (0.8f, 4.5f);

        /// <summary>The lab's doorways: the two rooms and a point on either side of each (walked through straight).</summary>
        static readonly (Room a, Room b, (float x, float z) pa, (float x, float z) pb)[] Doors =
        {
            (Room.Back, Room.Annex, (1.15f, 8.5f), (1.15f, 9.5f)),
            (Room.Annex, Room.Workshop, (3.5f, 10.3f), (4.5f, 10.3f)),
        };

        /// <summary>The counter's staff side, where Pip stands; each helper's home spot.</summary>
        public static readonly (float x, float z) Counter = (3.4f, -3.3f), NoriHome = (0.6f, 5.4f), MossHome = (0.2f, 6.3f);
        /// <summary>In front of the incubator's tray, and of the breeding tank's nursery.</summary>
        public static readonly (float x, float z) Tray = (-0.8f, 6.05f), Nursery = (-0.5f, 5.15f);
        /// <summary>Where a visitor or a helper stands at each shop place (GrowthWorld.Places' browse spots) and each rack place.</summary>
        public static readonly Dictionary<string, (float x, float z)> Places = GrowthSpaces.Spots.Select(s => (s.Id, s.Browse))
            .Concat(new[] { ("r1", (7.0f, 10.8f)), ("r2", (8.75f, 10.8f)), ("r3", (10.2f, 11.6f)), ("r4", (10.2f, 13.4f)) })
            .ToDictionary(x => x.Item1, x => x.Item2);

        public static (float x, float z) Home(string helper) => helper == "pip" ? Counter : helper == "nori" ? NoriHome : MossHome;

        /// <summary>In front of a grower (as the runtime's GrowerFront): 1.3 m from its cabinet, into the back room.</summary>
        public static (float x, float z) Grower(string culture)
        {
            var g = GrowthLab.Growers[culture];
            return GrowthLab.OnNorthWall(culture) ? (g.x, g.z - 1.3f) : (g.x + 1.3f, g.z);
        }

        static List<Room> Rooms(Room from, Room to)
        {
            var back = new Dictionary<Room, Room> { [from] = from };
            var queue = new Queue<Room>(); queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                if (r == to) break;
                foreach (var d in Doors)
                {
                    Room next = d.a == r ? d.b : d.b == r ? d.a : r;
                    if (next == r || back.ContainsKey(next)) continue;
                    back[next] = r; queue.Enqueue(next);
                }
            }
            var path = new List<Room> { to };
            while (path[0] != from) path.Insert(0, back[path[0]]);
            return path;
        }

        /// <summary>The walk from one point to another: in the lab through the doorways between their rooms; into, out of
        /// and across the shop along its walking network, through the back room's door.</summary>
        public static List<(float x, float z)> Path((float x, float z) from, (float x, float z) to)
        {
            if (GrowthSpaces.Distance(from, to) < 1e-3f) return new List<(float x, float z)> { from, to };
            Room rf = RoomOf(from), rt = RoomOf(to);
            if (!InShop(rf) && !InShop(rt)) return LabPath(from, to);
            var points = new List<(float x, float z)>();
            string start, end;
            if (InShop(rf)) { points.Add(from); start = GrowthSpaces.JoinOf(from); }
            else { points.AddRange(LabPath(from, BackDoorLab)); points.Add(BackDoorShop); start = "BackA"; }
            var tail = new List<(float x, float z)>();
            if (InShop(rt)) { end = GrowthSpaces.JoinOf(to); tail.Add(to); }
            else { end = "BackA"; tail.Add(BackDoorShop); tail.AddRange(LabPath(BackDoorLab, to)); }
            points.AddRange(GrowthSpaces.NetworkPath(start, end).Select(n => GrowthSpaces.Points[n]));
            points.AddRange(tail);
            var walk = new List<(float x, float z)>();
            foreach (var p in points) if (walk.Count == 0 || GrowthSpaces.Distance(walk[walk.Count - 1], p) > 1e-4f) walk.Add(p);
            if (walk.Count == 1) walk.Add(walk[0]);
            return walk;
        }

        /// <summary>A walk between two points in the lab (the back room, the annex, the workshop), through their doorways.</summary>
        static List<(float x, float z)> LabPath((float x, float z) from, (float x, float z) to)
        {
            var points = new List<(float x, float z)> { from };
            var rooms = Rooms(RoomOf(from), RoomOf(to));
            for (int i = 1; i < rooms.Count; i++)
            {
                var d = Doors.First(x => x.a == rooms[i - 1] && x.b == rooms[i] || x.b == rooms[i - 1] && x.a == rooms[i]);
                bool forward = d.a == rooms[i - 1];
                points.Add(forward ? d.pa : d.pb); points.Add(forward ? d.pb : d.pa);
            }
            points.Add(to);
            return points;
        }

        public static float Length(IList<(float x, float z)> path)
        {
            float n = 0;
            for (int i = 1; i < path.Count; i++) n += (float)Math.Sqrt((path[i].x - path[i - 1].x) * (path[i].x - path[i - 1].x) + (path[i].z - path[i - 1].z) * (path[i].z - path[i - 1].z));
            return n;
        }

        /// <summary>A walk's time: its length at walking speed, and a moment to pick up or put down at its end.</summary>
        public static float Seconds((float x, float z) from, (float x, float z) to) => Length(Path(from, to)) / Speed + Handle;

        /// <summary>The point a fraction of the way along a walk (the runtime's figures).</summary>
        public static (float x, float z) Along((float x, float z) from, (float x, float z) to, float f)
        {
            var path = Path(from, to);
            float want = Length(path) * Math.Max(0, Math.Min(1, f));
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1]; var b = path[i];
                float d = (float)Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
                if (want <= d || i == path.Count - 1) { float t = d < 1e-4f ? 1 : Math.Min(1, want / d); return (a.x + (b.x - a.x) * t, a.z + (b.z - a.z) * t); }
                want -= d;
            }
            return to;
        }
    }

    /// <summary>A hired helper: their level, duty and wage, and the step of work they are on (a walk with something to do at its end).</summary>
    public sealed class GrowthHelper
    {
        public string Id;
        public int Level = 1;
        public bool OnDuty = true;
        /// <summary>The duty period (the shop's day number: a night and the day after it) whose wage is paid.</summary>
        public int PaidPeriod;
        /// <summary>The step: null when between steps; "serve" (Pip); "fetch" and "deliver" (Nori); "dose-out", "dose-back",
        /// "parent-fetch" and "parent-deliver" (Moss). Spot is where it started, Target where it walks to, Progress how far.</summary>
        public string Step;
        public float Progress, StepSeconds;
        public (float x, float z) Spot, Target;
        /// <summary>What the step is about: a source ("tray", "nursery:0", "c:&lt;container&gt;", "grower:bloom") or a destination
        /// ("c:&lt;container&gt;", "incubator", "breeding:0"), and the creature fetched.</summary>
        public string Place;
        public int SpecimenId;
        /// <summary>Creatures carried (Nori's, Moss's parent), and a dose of culture carried (Moss).</summary>
        public readonly List<GrowthSpecimen> Hands = new List<GrowthSpecimen>();
        public string Dose;
        /// <summary>Moss at level 3: which job goes next (0 the recipe, 1 the pair).</summary>
        public int NextJob;
        public string Status = "";
        public HelperDef Def => GrowthContent.Helper(Id);
        public bool Working => Step != null;
    }

    public sealed partial class GrowthShop
    {
        /// <summary>Pip's seconds to bag an order by level; Nori's creatures a trip; Moss's jobs at once.</summary>
        public static readonly float[] PipServeSeconds = { 3f, 2f, 1.2f };
        public static readonly int[] NoriCarry = { 1, 2, 3 };
        public const string MossRecipeJob = "recipe", MossPairJob = "pair";

        public readonly Dictionary<string, GrowthHelper> Staff = new Dictionary<string, GrowthHelper>();
        /// <summary>Moss's settings: the known recipe, the known pair, and at level 2 which of the two jobs runs.</summary>
        public string MossRecipe, MossPairA, MossPairB, MossTask = MossRecipeJob;
        /// <summary>Moss is dosing the incubator: the chamber is his batch (the player's doses keep out, as from the auto-doser's).</summary>
        public bool MossBatch;
        public int MossBatches;
        /// <summary>The visitor Pip is bagging an order for (visitors are not saved: a day resumes empty).</summary>
        public int PipVisitor;
        /// <summary>The player is holding the counter (Serve until StopServing): at the second till the front of the queue is
        /// theirs and Pip takes the visitor behind (not saved: a moment's state).</summary>
        public bool PlayerAtTill;

        public GrowthHelper Helper(string id) => Staff.TryGetValue(id, out var h) ? h : null;
        public bool Working(string id) { var h = Helper(id); return h != null && h.OnDuty; }
        public int Period => Day;
        /// <summary>The counter's second till (level 4): the player serves beside Pip.</summary>
        public bool SecondTill => Stat("counter", "tills") >= 2;
        /// <summary>Pip at level 3 calls the next visitor over: the queue holds one more.</summary>
        int PipCalls => Working("pip") && Helper("pip").Level >= 3 ? 1 : 0;
        public int WageBill => Staff.Values.Where(h => h.OnDuty).Sum(h => h.Def.Wages[h.Level - 1]);

        /// <summary>The achievement besides stars each helper needs (GR.10), with its progress, or null when it is met.</summary>
        public string HelperAchievement(string id)
        {
            switch (id)
            {
                case "pip": return SalesTotal >= GrowthContent.PipSalesGate ? null : "Needs " + GrowthContent.PipSalesGate + " sales (" + SalesTotal + " so far)";
                case "nori": return ShopContainers.Any(c => c.Kind == "large") ? null : "Needs a Large tank in the shop";
                case "moss": return Level("generator") >= 2 && Level("pump") >= 2 ? null : "Needs a powered generator: the bubble turbine and electric pumps";
                default: return "No such helper";
            }
        }

        // ---------------------------------------------------------------- hiring, training and duty

        public GrowthFailure HireCheck(string id, out string why)
        {
            why = null;
            var def = GrowthContent.Helper(id);
            if (def == null) { why = "No such helper"; return GrowthFailure.NoSuchPlace; }
            if (Staff.ContainsKey(id)) { why = def.Name + " works here already"; return GrowthFailure.Owned; }
            if (Stars < def.Stars) { why = "Needs " + def.Stars + " stars"; return GrowthFailure.Locked; }
            string later = HelperAchievement(id);
            if (later != null) { why = later; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Helpers are hired at night"; return GrowthFailure.NotAtNight; }
            int cost = def.Fee + def.Wages[0];
            if (Credits < cost) { why = "Needs " + cost + " credits: the fee and tonight's wage"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Hires a helper at night: their fee and this duty period's wage; they start at level 1, on duty, at home.</summary>
        public GrowthResult Hire(string id)
        {
            var failure = HireCheck(id, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var def = GrowthContent.Helper(id);
            Credits -= def.Fee + def.Wages[0];
            var h = new GrowthHelper { Id = id, PaidPeriod = Period, Spot = GrowthWalk.Home(id), Target = GrowthWalk.Home(id) };
            Staff[id] = h;
            Raise(GrowthEventKind.Hired, def.Name + " joins the shop as its " + def.Job + " (" + def.Fee + " credits and " + def.Wages[0] + " for tonight and tomorrow)", amount: def.Fee + def.Wages[0]);
            return GrowthResult.Ok();
        }

        public GrowthFailure TrainCheck(string id, out string why)
        {
            why = null;
            var h = Helper(id);
            if (h == null) { why = "Hire " + GrowthContent.Helper(id)?.Name + " first"; return GrowthFailure.Locked; }
            if (h.Level >= 3) { why = "Highest level"; return GrowthFailure.MaxLevel; }
            if (Phase != GrowthPhase.Night) { why = "Training happens at night"; return GrowthFailure.NotAtNight; }
            int cost = h.Def.TrainCosts[h.Level - 1];
            if (Credits < cost) { why = "Needs " + cost + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>What a helper's next level gives (GR.10).</summary>
        public static string LevelText(string id, int level)
        {
            switch (id)
            {
                case "pip": return level == 1 ? "Bags an order in 3 s" : level == 2 ? "Bags an order in 2 s" : "Bags an order in 1.2 s and calls the next visitor over (the queue holds one more)";
                case "nori": return level == 1 ? "Carries one creature at a time" : level == 2 ? "Carries two at a time" : "Carries three of one species at a time, to their school";
                default: return level == 1 ? "Runs one known recipe by hand, with no pipes" : level == 2 ? "Runs a known recipe or keeps a known pair breeding, which no machine does" : "Does both jobs";
            }
        }

        /// <summary>Trains a helper at night to their next level; their wage rises from the next duty period.</summary>
        public GrowthResult Train(string id)
        {
            var failure = TrainCheck(id, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var h = Helper(id);
            int cost = h.Def.TrainCosts[h.Level - 1];
            Credits -= cost;
            h.Level++;
            Raise(GrowthEventKind.Upgraded, h.Def.Name + " trained to level " + h.Level + ": " + LevelText(id, h.Level), amount: cost);
            return GrowthResult.Ok();
        }

        /// <summary>
        /// Sends a helper home, or back on duty. A helper going home first finishes the step in hand (nothing they carry is
        /// lost). Coming back within a duty period already paid costs nothing; otherwise the period's wage is charged now.
        /// </summary>
        public GrowthResult SetDuty(string id, bool on)
        {
            var h = Helper(id);
            if (h == null) return GrowthResult.Fail(GrowthFailure.Locked, "Hire " + GrowthContent.Helper(id)?.Name + " first");
            if (h.OnDuty == on) return GrowthResult.Fail(GrowthFailure.MaxLevel, on ? "On duty already" : "Home already");
            if (on && h.PaidPeriod != Period)
            {
                int wage = h.Def.Wages[h.Level - 1];
                if (Credits < wage) return GrowthResult.Fail(GrowthFailure.NotEnoughCredits, "Needs " + wage + " credits for the wage");
                Credits -= wage; h.PaidPeriod = Period;
                Raise(GrowthEventKind.Wages, h.Def.Name + "'s wage: " + wage + " credits for tonight and tomorrow", amount: wage);
            }
            h.OnDuty = on;
            if (!on && id == "pip") PipVisitor = 0;
            return GrowthResult.Ok();
        }

        /// <summary>
        /// A new duty period starts with the night (§6.3): each helper on duty is paid once; one the shop cannot pay stays home
        /// and says so (no debt; the player can always work by hand, and can call them back when there are credits).
        /// </summary>
        void StartDutyPeriod()
        {
            int paid = 0; var names = new List<string>();
            foreach (var h in Staff.Values.Where(x => x.OnDuty).OrderBy(x => x.Def.Fee))
            {
                int wage = h.Def.Wages[h.Level - 1];
                if (Credits >= wage) { Credits -= wage; h.PaidPeriod = Period; paid += wage; names.Add(h.Def.Name + " " + wage); continue; }
                h.OnDuty = false;
                Raise(GrowthEventKind.StaffHome, h.Def.Name + " stays home: the wage is " + wage + " credits and the shop has " + Credits);
            }
            if (paid > 0) Raise(GrowthEventKind.Wages, "Wages for tonight and tomorrow: " + paid + " credits (" + string.Join(", ", names) + ")", amount: paid);
        }

        // ---------------------------------------------------------------- the tick

        void TickHelpers(float seconds)
        {
            foreach (var h in Staff.Values)
            {
                if (h.Id == "pip") TickPip(h, seconds);
                else if (h.Id == "nori") TickNori(h, seconds);
                else TickMoss(h, seconds);
            }
        }

        void StartStep(GrowthHelper h, string step, (float x, float z) target, string place, int specimen = 0)
        {
            h.Step = step; h.Target = target; h.Place = place; h.SpecimenId = specimen;
            h.Progress = 0; h.StepSeconds = GrowthWalk.Seconds(h.Spot, target);
        }

        /// <summary>Advances a step; true when it ends this tick (the helper is then at its target).</summary>
        static bool Advance(GrowthHelper h, float seconds)
        {
            h.Progress += seconds;
            if (h.Progress < h.StepSeconds) return false;
            h.Spot = h.Target; h.Progress = 0; h.Step = null;
            return true;
        }

        // ---------------------------------------------------------------- Pip

        /// <summary>The visitor the player bags an order for: the first queued, or with Pip at the till the first Pip is not serving.</summary>
        public GrowthVisitor PlayerVisitor => Queue.Where(v => v.Id != PipVisitor).OrderByDescending(v => v.Clock).ThenBy(v => v.Id).FirstOrDefault();

        void TickPip(GrowthHelper h, float seconds)
        {
            h.Spot = h.Target = GrowthWalk.Counter;
            if (!h.OnDuty) { h.Step = null; h.Status = "Home today"; PipVisitor = 0; return; }
            if (Phase != GrowthPhase.Day) { h.Step = null; h.Status = "Waiting for the shop to open"; PipVisitor = 0; return; }
            var v = Queue.FirstOrDefault(x => x.Id == PipVisitor);
            if (v == null)
            {
                // At the second till, the player holding the counter serves the front of the queue; Pip takes the one behind.
                var line = Queue.OrderByDescending(x => x.Clock).ThenBy(x => x.Id);
                v = PlayerAtTill && SecondTill ? line.Skip(1).FirstOrDefault() : line.FirstOrDefault();
                PipVisitor = v?.Id ?? 0; h.Progress = 0;
            }
            if (v == null) { h.Step = null; h.Status = "Waiting for a visitor at the till"; return; }
            h.Step = "serve"; h.StepSeconds = PipServeSeconds[h.Level - 1];
            h.Progress += seconds;
            h.Status = "Bagging an order (" + Math.Max(0, h.StepSeconds - h.Progress).ToString("0.0") + " s)";
            if (h.Progress < h.StepSeconds) return;
            h.Progress = 0; h.Step = null; PipVisitor = 0;
            SellTo(v, "Pip sold ");
        }

        // ---------------------------------------------------------------- Nori

        /// <summary>Where Nori would put a creature: a shop tank while its species is below its target (counting what Nori
        /// already carries there), else a lab rack; only where it fits with what goes in with it. Null when nothing has room.</summary>
        GrowthContainer NoriDestination(string species, IEnumerable<string> alongside, bool rack = true)
        {
            var with = alongside.ToList();
            bool Fits(GrowthContainer c) => GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(o => o.Species).Concat(with), species);
            // GR-31 (F34): with no targets set at all, whatever waits goes to any tank with room (Nori used to stand idle beside it).
            if (StockTargets.Count == 0 || Shown(species) + with.Count(x => x == species) < Target(species))
            {
                // GR-31 (F37): a tank with a rule takes only what the rule allows, and is filled before those with none.
                var tank = ShopContainers.Where(c => Fits(c) && RuleAllows(c, species)).OrderByDescending(c => c.Rule != "").ThenByDescending(c => c.Occupants.Count(o => o.Species == species)).ThenBy(c => c.Place, StringComparer.Ordinal).FirstOrDefault();
                if (tank != null) return tank;
            }
            return rack ? Racks.Where(Fits).OrderBy(c => c.Place, StringComparer.Ordinal).FirstOrDefault() : null;
        }

        /// <summary>The creatures Nori could fetch now, in order: the tray (while no chute takes it), the nursery, the racks
        /// (only what a shop tank below its target takes), each with where it is and where it would go.</summary>
        IEnumerable<(string place, GrowthSpecimen s, (float x, float z) at)> NoriSources()
        {
            if (Chute == null) foreach (var s in Hatchlings) yield return ("tray", s, GrowthWalk.Tray);
            for (int k = 0; k < Pairs.Length; k++) if (Pairs[k].Offspring != null) yield return ("nursery:" + k, Pairs[k].Offspring, GrowthWalk.Nursery);
            foreach (var r in Racks.OrderBy(c => c.Place, StringComparer.Ordinal))
                foreach (var s in r.Occupants) yield return ("c:" + r.Id, s, GrowthWalk.Places[r.Place]);
        }

        bool NoriCanMove(string place, GrowthSpecimen s) =>
            place.StartsWith("c:", StringComparison.Ordinal) ? NoriDestination(s.Species, new string[0], rack: false) != null : NoriDestination(s.Species, new string[0]) != null;

        static (float x, float z) PointOf(GrowthContainer c) => GrowthWalk.Places[c.Place];

        string ContainerName(GrowthContainer c) => c.Def.Lab ? RackName(c.Place) : "the " + c.Def.Name.ToLowerInvariant() + " at " + c.Place;

        static string SourceName(string place) => place == "tray" ? "the incubator's tray" : place.StartsWith("nursery", StringComparison.Ordinal) ? "the nursery" : "a rack";

        void TickNori(GrowthHelper h, float seconds)
        {
            if (h.Step == null)
            {
                if (h.Hands.Count > 0) { NoriDeliver(h); return; }
                if (!h.OnDuty) { h.Status = "Home today"; return; }
                var next = NoriSources().FirstOrDefault(x => NoriCanMove(x.place, x.s));
                if (next.s == null)
                {
                    h.Status = StockTargets.Count == 0 && Hatchlings.Count == 0 && !Pairs.Any(p => p.Offspring != null) && !Racks.Any(r => r.Occupants.Count > 0) ? "Nothing waiting to move" :
                        Hatchlings.Count > 0 && Chute == null || Pairs.Any(p => p.Offspring != null) ? "No room for what is waiting: the tanks are at their targets or full, and no rack has room" : "Nothing to move";
                    return;
                }
                StartStep(h, "fetch", next.at, next.place, next.s.Id);
                h.Status = "Fetching a " + next.s.Def.Name + " from " + SourceName(next.place);
                return;
            }
            string step = h.Step;
            if (!Advance(h, seconds)) return;
            if (step == "fetch") NoriPickUp(h); else NoriPutDown(h);
        }

        /// <summary>At the source: takes the creature fetched and, from level 2, more from the same place that go with it.</summary>
        void NoriPickUp(GrowthHelper h)
        {
            var at = NoriSources().Where(x => x.place == h.Place).ToList();
            var first = at.FirstOrDefault(x => x.s.Id == h.SpecimenId);
            if (first.s == null || !NoriCanMove(first.place, first.s)) { h.Status = "It was gone: looking again"; return; }
            var batch = new List<GrowthSpecimen> { first.s };
            var dest = NoriDestination(first.s.Species, new string[0], rack: !h.Place.StartsWith("c:", StringComparison.Ordinal));
            int most = NoriCarry[h.Level - 1];
            foreach (var x in at.Where(x => x.s.Id != first.s.Id))
            {
                if (batch.Count >= most) break;
                if (h.Level >= 3 && x.s.Species != first.s.Species) continue;
                var d = NoriDestination(x.s.Species, batch.Select(b => b.Species), rack: !h.Place.StartsWith("c:", StringComparison.Ordinal));
                if (d == dest) batch.Add(x.s);
            }
            foreach (var s in batch) TakeFrom(h.Place, s);
            h.Hands.AddRange(batch);
            NoriDeliver(h);
        }

        void TakeFrom(string place, GrowthSpecimen s)
        {
            if (place == "tray") { Hatchlings.Remove(s); return; }
            if (place.StartsWith("nursery:", StringComparison.Ordinal))
            {
                var pair = Pairs[int.Parse(place.Substring(8))];
                if (pair.Offspring == s) { pair.Offspring = null; RestartBreeding(pair); }
                return;
            }
            Container(place.Substring(2))?.Occupants.Remove(s);
        }

        /// <summary>Sets off for where the carried creatures go; waits (holding them) when nothing has room.</summary>
        void NoriDeliver(GrowthHelper h)
        {
            var first = h.Hands[0];
            var dest = NoriDestination(first.Species, h.Hands.Skip(1).Select(x => x.Species)) ?? NoriDestination(first.Species, new string[0]);
            if (dest == null) { h.Status = "Holding a " + first.Def.Name + ": no tank below its target and no rack has room"; return; }
            StartStep(h, "deliver", PointOf(dest), "c:" + dest.Id);
            h.Status = "Carrying " + (h.Hands.Count > 1 ? h.Hands.Count + " " + Plural(first.Def.Name) : "a " + first.Def.Name) + " to " + ContainerName(dest);
        }

        /// <summary>At the destination: each carried creature goes in if it still fits (else it stays in hand for the next trip).</summary>
        void NoriPutDown(GrowthHelper h)
        {
            var c = Container(h.Place.Substring(2));
            var placed = new List<GrowthSpecimen>();
            foreach (var s in h.Hands.ToList())
            {
                if (c == null || !GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(o => o.Species), s.Species)) continue;
                if (!c.Def.Lab && Shown(s.Species) >= Target(s.Species) && Racks.Any(r => GrowthRules.Fits(r.Def, r.Level, r.Occupants.Select(o => o.Species), s.Species))) continue;
                h.Hands.Remove(s); c.Occupants.Add(s); placed.Add(s);
            }
            if (h.Hands.Count > 0) { NoriDeliver(h); return; }
            var first = placed[0].Def.Name;
            h.Status = "Put " + (placed.Count > 1 ? placed.Count + " " + Plural(first) : "a " + first) + " in " + ContainerName(c);
        }

        // ---------------------------------------------------------------- Moss

        public GrowthFailure MossRecipeCheck(string species, out string why)
        {
            why = null;
            var s = GrowthContent.Get(species);
            if (s == null || s.Kind != SpeciesKind.Founder) { why = "Only a founder's mix"; return GrowthFailure.NotAFounder; }
            if (!Discovered.Contains(species)) { why = "Only a creature you have made: Moss never tries something new"; return GrowthFailure.NotDiscovered; }
            if (s.Doses > MaxDoses) { why = "Up to " + MaxDoses + " doses"; return GrowthFailure.ChamberFull; }
            if (MossBatch) { why = "Wait for the batch Moss is dosing"; return GrowthFailure.Busy; }
            return GrowthFailure.None;
        }

        public GrowthResult SetMossRecipe(string species)
        {
            var failure = MossRecipeCheck(species, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            MossRecipe = species;
            return GrowthResult.Ok();
        }

        /// <summary>Moss's known pair (level 2): two species whose pair has been bred before (the pair log), never a new pair.</summary>
        public GrowthResult SetMossPair(string a, string b)
        {
            if (GrowthContent.Get(a) == null || GrowthContent.Get(b) == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen, "No such species");
            if (PairEntry(a, b) == null) return GrowthResult.Fail(GrowthFailure.NotDiscovered, "Only a pair you have bred: Moss never tries a new pair");
            MossPairA = a; MossPairB = b;
            return GrowthResult.Ok();
        }

        /// <summary>At level 2, Moss runs the recipe or the pair (level 3 runs both).</summary>
        public GrowthResult SetMossTask(string task)
        {
            if (task != MossRecipeJob && task != MossPairJob) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "No such job");
            if (task == MossPairJob && (Helper("moss")?.Level ?? 0) < 2) return GrowthResult.Fail(GrowthFailure.Locked, "Keeping a pair breeding comes with Moss's level 2");
            MossTask = task;
            return GrowthResult.Ok();
        }

        /// <summary>The recipe's doses not yet in the chamber, in culture order (Moss's batch).</summary>
        List<string> MossMissing()
        {
            var missing = new List<string>();
            if (MossRecipe == null) return missing;
            var recipe = GrowthContent.Get(MossRecipe).Recipe;
            for (int i = 0; i < recipe.Length; i++)
            {
                string c = GrowthContent.Cultures[i].Id;
                for (int n = Chamber.Count(x => x == c); n < recipe[i]; n++) missing.Add(c);
            }
            return missing;
        }

        /// <summary>Why Moss's recipe job does nothing now, or null when it has a dose to fetch.</summary>
        string MossRecipeWaiting()
        {
            if (MossRecipe == null) return "Choose a recipe on my card";
            if (MossBatch) return null;
            if (AutoDoser && AutoOn) return "The auto-doser runs the incubator";
            if (AutoBatch) return "The auto-doser is dosing";
            if (Incubating) return "Waiting: the incubator is incubating";
            if (Hatchlings.Count > 0) return "Waiting for the hatchlings to be taken off the tray";
            if (Chamber.Count > 0) return "The chamber has doses: start them or take them back";
            var s = GrowthContent.Get(MossRecipe);
            if (s.Doses > MaxDoses) return "The recipe needs more doses than the incubator takes";
            for (int i = 0; i < s.Recipe.Length; i++)
                if (s.Recipe[i] > 0 && !Owns(GrowthContent.Cultures[i].Id)) return "Needs " + Name(GrowthContent.Cultures[i].Id) + ": its grower";
            return null;
        }

        /// <summary>The pair place Moss keeps his pair in: one holding only his pair's species (or empty), the first such.</summary>
        GrowthBreedingPair MossPairPlace()
        {
            if (!HasBreedingTank || MossPairA == null) return null;
            var want = new List<string> { MossPairA, MossPairB };
            foreach (var p in ActivePairs)
            {
                var left = new List<string>(want);
                if (p.Parents.All(x => left.Remove(x.Species))) return p;
            }
            return null;
        }

        /// <summary>The species Moss's pair place still needs, or null when the pair is in.</summary>
        string MossPairMissing(GrowthBreedingPair p)
        {
            var left = new List<string> { MossPairA, MossPairB };
            foreach (var x in p.Parents) left.Remove(x.Species);
            return left.FirstOrDefault();
        }

        /// <summary>Why Moss's pair job does nothing now, or null when it has a parent to fetch (with where it is).</summary>
        string MossPairWaiting(out GrowthContainer from, out GrowthSpecimen parent)
        {
            from = null; parent = null;
            if (MossPairA == null) return "Choose a known pair on my card";
            if (!HasBreedingTank) return "Needs the breeding tank";
            var place = MossPairPlace();
            if (place == null) return "The breeding tank holds another pair";
            string need = MossPairMissing(place);
            if (need == null) return place.Offspring != null ? "Breeding: an offspring waits in the nursery" : "Breeding a " + GrowthContent.Get(GrowthRules.Breed(MossPairA, MossPairB).Id).Name;
            if (GrowthContent.Get(need).Size == SizeClass.Large && !LargeParents) return "Large parents need the breeding tank's level 2";
            foreach (var c in Racks.OrderBy(x => x.Place, StringComparer.Ordinal).Concat(ShopContainers.OrderBy(x => x.Place, StringComparer.Ordinal)))
            {
                var s = c.Occupants.FirstOrDefault(o => o.Species == need && o.ReservedBy == 0);
                if (s != null) { from = c; parent = s; return null; }
            }
            return "Needs a " + GrowthContent.Get(need).Name + " in a tank or rack";
        }

        /// <summary>The jobs Moss runs at his level: the recipe; at level 2 the one chosen; at level 3 both, in turn. A batch he
        /// is dosing is always finished first (its doses would otherwise hold the chamber).</summary>
        IEnumerable<string> MossJobs(GrowthHelper h)
        {
            if (MossBatch) return new[] { MossRecipeJob };
            if (h.Level >= 3) return h.NextJob == 0 ? new[] { MossRecipeJob, MossPairJob } : new[] { MossPairJob, MossRecipeJob };
            return new[] { h.Level >= 2 ? MossTask : MossRecipeJob };
        }

        void TickMoss(GrowthHelper h, float seconds)
        {
            if (h.Step == null)
            {
                // Something in hand goes where it was going first.
                if (h.Dose != null) { MossDoseBack(h); return; }
                if (h.Hands.Count > 0) { MossCarry(h); return; }
                if (!h.OnDuty) { MossBatch = false; h.Status = "Home today"; return; }
                var reasons = new List<string>();
                foreach (var job in MossJobs(h))
                {
                    if (job == MossRecipeJob)
                    {
                        string waiting = MossRecipeWaiting();
                        if (waiting != null) { reasons.Add(waiting); continue; }
                        var missing = MossMissing();
                        if (missing.Count == 0) { MossIncubate(); reasons.Add("Incubating"); continue; }
                        string next = missing[0];
                        if (GrowerUnits[next] < 1) { reasons.Add("Waiting for " + Name(next) + " at its grower"); continue; }
                        MossBatch = true;
                        StartStep(h, "dose-out", GrowthWalk.Grower(next), "grower:" + next);
                        h.Status = "Fetching 1 " + Name(next) + " for a " + GrowthContent.Get(MossRecipe).Name;
                        h.NextJob = 1;
                        return;
                    }
                    string why = MossPairWaiting(out var from, out var parent);
                    if (why != null) { reasons.Add(why); continue; }
                    StartStep(h, "parent-fetch", PointOf(from), "c:" + from.Id, parent.Id);
                    h.Status = "Fetching a " + parent.Def.Name + " for the breeding tank";
                    h.NextJob = 0;
                    return;
                }
                h.Status = string.Join("; ", reasons);
                return;
            }
            string step = h.Step;
            if (!Advance(h, seconds)) return;
            switch (step)
            {
                case "dose-out":
                    string c = h.Place.Substring(7);
                    if (GrowerUnits[c] >= 1) { GrowerUnits[c] -= 1; h.Dose = c; MossDoseBack(h); }
                    else h.Status = "Waiting for " + Name(c) + " at its grower";
                    return;
                case "dose-back": MossDoseBack(h); return;
                case "parent-fetch":
                    var box = Container(h.Place.Substring(2));
                    var s = box?.Occupants.FirstOrDefault(o => o.Id == h.SpecimenId && o.ReservedBy == 0);
                    if (s == null) { h.Status = "It was gone: looking again"; return; }
                    box.Occupants.Remove(s); h.Hands.Add(s);
                    MossCarry(h);
                    return;
                default: MossCarry(h); return;
            }
        }

        static bool Near((float x, float z) a, (float x, float z) b) => (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z) < 0.01f;

        /// <summary>With a dose in hand: at the incubator it goes into Moss's batch (which incubates once complete), else Moss walks there.</summary>
        void MossDoseBack(GrowthHelper h)
        {
            if (!Near(h.Spot, GrowthWalk.Tray)) { StartStep(h, "dose-back", GrowthWalk.Tray, "incubator"); h.Status = "Bringing 1 " + Name(h.Dose) + " to the incubator"; return; }
            if (!MossBatch || Incubating || Hatchlings.Count > 0 || AutoBatch) { h.Status = "Holding 1 " + Name(h.Dose) + ": the incubator is not free"; return; }
            Chamber.Add(h.Dose); h.Dose = null;
            if (MossMissing().Count == 0) MossIncubate();
        }

        void MossIncubate()
        {
            MossBatch = false;
            string species = MossRecipe;
            if (StartIncubation().Success) { MossBatches++; Raise(GrowthEventKind.AutoDosed, "Moss started a " + GrowthContent.Get(species).Name, species); }
        }

        /// <summary>
        /// With a parent in hand: into the breeding tank when Moss's pair place still needs it (walking there first), else back
        /// to a tank or rack it fits (when the pair changed meanwhile); held, and said, when nowhere has room.
        /// </summary>
        void MossCarry(GrowthHelper h)
        {
            var s = h.Hands[0];
            var place = MossPairPlace();
            bool wanted = place != null && place.Parents.Count < PairSize && MossPairMissing(place) == s.Species && (s.Def.Size != SizeClass.Large || LargeParents);
            if (wanted)
            {
                if (Near(h.Spot, GrowthWalk.Nursery)) { h.Hands.Clear(); place.Parents.Add(s); RestartBreeding(place); h.Status = "Put a " + s.Def.Name + " in the breeding tank"; return; }
                StartStep(h, "parent-deliver", GrowthWalk.Nursery, "breeding");
                h.Status = "Carrying a " + s.Def.Name + " to the breeding tank";
                return;
            }
            var back = Racks.OrderBy(c => c.Place, StringComparer.Ordinal).Concat(ShopContainers.OrderBy(c => c.Place, StringComparer.Ordinal))
                .FirstOrDefault(c => GrowthRules.Fits(c.Def, c.Level, c.Occupants.Select(o => o.Species), s.Species));
            if (back == null) { h.Status = "Holding a " + s.Def.Name + ": nowhere has room"; return; }
            if (Near(h.Spot, PointOf(back))) { h.Hands.Clear(); back.Occupants.Add(s); h.Status = "Put a " + s.Def.Name + " back in " + ContainerName(back); return; }
            StartStep(h, "parent-deliver", PointOf(back), "c:" + back.Id);
            h.Status = "Taking a " + s.Def.Name + " back to " + ContainerName(back);
        }

        // ---------------------------------------------------------------- saving (version 8)

        void SaveHelpers(GrowthSave s)
        {
            s.staff = Staff.Values.Select(h => new GrowthSave.Helper
            {
                id = h.Id, level = h.Level, onDuty = h.OnDuty, paidPeriod = h.PaidPeriod, step = h.Step ?? "", progress = h.Progress, stepSeconds = h.StepSeconds,
                spotX = h.Spot.x, spotZ = h.Spot.z, targetX = h.Target.x, targetZ = h.Target.z, place = h.Place ?? "", specimen = h.SpecimenId,
                hands = h.Hands.Select(GrowthSave.Specimen.Of).ToArray(), dose = h.Dose ?? "", nextJob = h.NextJob, status = h.Status ?? "",
            }).ToArray();
            s.mossRecipe = MossRecipe ?? ""; s.mossPairA = MossPairA ?? ""; s.mossPairB = MossPairB ?? ""; s.mossTask = MossTask; s.mossBatch = MossBatch; s.mossBatches = MossBatches;
        }

        static string RestoreHelpers(GrowthShop shop, GrowthSave s)
        {
            var ids = new HashSet<int>(shop.Containers.SelectMany(c => c.Occupants).Concat(shop.Hatchlings).Concat(shop.Carried).Concat(shop.AllParents).Concat(shop.AllOffspring).Select(x => x.Id));
            foreach (var x in s.staff ?? new GrowthSave.Helper[0])
            {
                var def = GrowthContent.Helper(x.id);
                if (def == null || shop.Staff.ContainsKey(x.id) || x.level < 1 || x.level > 3) return "A helper that cannot be";
                var h = new GrowthHelper { Id = x.id, Level = x.level, OnDuty = x.onDuty, PaidPeriod = x.paidPeriod, Step = string.IsNullOrEmpty(x.step) ? null : x.step,
                    Progress = Math.Max(0, x.progress), StepSeconds = Math.Max(0, x.stepSeconds), Spot = (x.spotX, x.spotZ), Target = (x.targetX, x.targetZ),
                    Place = string.IsNullOrEmpty(x.place) ? null : x.place, SpecimenId = x.specimen, Dose = string.IsNullOrEmpty(x.dose) ? null : x.dose, NextJob = x.nextJob == 1 ? 1 : 0, Status = x.status ?? "" };
                foreach (var sp in x.hands ?? new GrowthSave.Specimen[0])
                {
                    if (GrowthContent.Get(sp.species) == null || !ids.Add(sp.id)) return "A creature is saved twice";
                    h.Hands.Add(sp.ToSpecimen());
                }
                if (h.Dose != null && GrowthContent.CultureIndex(h.Dose) < 0) return "A helper carrying an unknown culture";
                if (h.Hands.Count > NoriCarry[2] || h.Id == "pip" && (h.Hands.Count > 0 || h.Dose != null)) return "A helper carrying more than they can";
                if (h.Step != null && h.Place == null && h.Step != "serve") return "A helper's step without its place";
                shop.Staff[x.id] = h;
            }
            shop.MossRecipe = string.IsNullOrEmpty(s.mossRecipe) ? null : s.mossRecipe;
            shop.MossPairA = string.IsNullOrEmpty(s.mossPairA) ? null : s.mossPairA;
            shop.MossPairB = string.IsNullOrEmpty(s.mossPairB) ? null : s.mossPairB;
            shop.MossTask = s.mossTask == MossPairJob ? MossPairJob : MossRecipeJob;
            shop.MossBatch = s.mossBatch; shop.MossBatches = Math.Max(0, s.mossBatches);
            if (shop.MossRecipe != null && (GrowthContent.Get(shop.MossRecipe)?.Kind != SpeciesKind.Founder || !shop.Discovered.Contains(shop.MossRecipe))) return "Moss's recipe that cannot be";
            if (shop.MossPairA != null && (shop.MossPairB == null || shop.PairEntry(shop.MossPairA, shop.MossPairB) == null)) return "Moss's pair that cannot be";
            if (shop.MossBatch)
            {
                var recipe = shop.MossRecipe == null ? null : GrowthContent.Get(shop.MossRecipe).Recipe;
                bool fits = recipe != null && shop.Chamber.All(c => shop.Chamber.Count(x => x == c) <= recipe[GrowthContent.CultureIndex(c)]);
                if (!fits || shop.AutoBatch || shop.Incubating || shop.Hatchlings.Count > 0 || shop.Helper("moss") == null) return "Moss's batch that cannot be";
            }
            if (shop.Level("counter") >= 4 && shop.Helper("pip") == null) return "A second till without Pip";
            int most = shop.Staff.Values.SelectMany(h => h.Hands).Select(x => x.Id).DefaultIfEmpty(0).Max();
            shop.nextId = Math.Max(shop.nextId, most + 1);
            return null;
        }
    }
}
