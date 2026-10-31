using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-07 (GR.8): power and pipes, tiers T1 and T2, in the lab annex the user chose to open for them (a first version of
    // GR.11's 8 x 8 m annex north of the back room, at 3 stars; GR-10 finishes it). A grower's tap (growers' level 3) is
    // piped to a culture buffer in the annex, and later a buffer to the incubator's port (its level 4); a pipe is laid by
    // picking its two ends and runs itself overhead along the lab grid, around other pipes, costing by its length. The hand
    // pump (pumps' level 1) moves culture down a pipe while the player cranks it; electric pumps (level 2, fast at 3) run
    // by themselves on the generator's power (the hand crank, 1 unit while cranked, then the bubble turbine's 3). Power is a
    // budget: supply against draw, served in the player's priority order, a machine short of power slowed and saying so,
    // never stopped silently. The auto-doser doses a known recipe from the port's buffers into the incubator, never
    // something undiscovered. R08's modules are re-homed here (GR.8): the collector is the pumps and buffers, the doser the
    // auto-doser; each line and the doser own their work, say why they wait, and a save restores a job without duplicating
    // culture (GR.15.1).

    /// <summary>
    /// The lab grid (GR.8, GR.11): 0.5 m cells over the back room (6 x 5 m), the lab annex (8 x 8 m) north of it and, from
    /// GR-08, the workshop (8 x 6 m) east of the annex, where pipes run overhead under the 3.6 m ceiling. Where every pipe end
    /// is: each grower's tap (on its back, by the wall), the annex's buffer places along its south wall, the incubator's port
    /// behind it and its outlet beside it (the hatchling chute's top), and the sorter's hopper in the workshop. The runtime
    /// builds the lab from these.
    /// </summary>
    public static class GrowthLab
    {
        public const float CellSize = 0.5f, MinX = -4f, MinZ = 4f, PipeHeight = 3.1f;
        public const int Columns = 32, Rows = 26, BackRoomColumns = 12, AnnexColumns = 16, AnnexFirstRow = 10, WorkshopRows = 12, BufferPlaces = 6;
        public const string Port = "port", Out = "out", Sorter = "sorter";

        /// <summary>GR-04's slots for each culture's source: four growers along the west wall facing east, three along the north wall facing south.</summary>
        public static readonly Dictionary<string, (float x, float z)> Growers = new Dictionary<string, (float x, float z)>
        {
            ["bloom"] = (-3.45f, 4.75f), ["flow"] = (-3.45f, 5.85f), ["ember"] = (-3.45f, 6.95f), ["brine"] = (-3.45f, 8.05f),
            ["spark"] = (-2.5f, 8.5f), ["mist"] = (-1.3f, 8.5f), ["dusk"] = (-0.1f, 8.5f),
        };
        public static readonly (float x, float z) Incubator = (-0.8f, 6.9f), PortPoint = (-0.8f, 7.35f), OutPoint = (-0.3f, 6.9f);
        /// <summary>GR-08: the workshop east of the annex (x 4 to 12, z 9 to 15): the sorter against its north wall, its hopper in front.</summary>
        public static readonly (float x, float z) SorterStand = (6f, 14.2f), SorterPoint = (6f, 13.55f);

        public static bool OnNorthWall(string culture) => Growers[culture].z > 8.2f;
        public static (float x, float z) TapPoint(string culture) { var g = Growers[culture]; return OnNorthWall(culture) ? (g.x, 8.85f) : (-3.85f, g.z); }
        /// <summary>The buffer places, 0.5 m apart along the annex's south wall (behind the back room's north wall), facing north.</summary>
        public static (float x, float z) BufferPoint(int place) => (MinX + (place + 1.5f) * CellSize, 9.4f);

        public static string Tap(string culture) => "tap:" + culture;
        public static string Buffer(int place) => "buffer:" + place;
        public static bool IsTap(string end) => end != null && end.StartsWith("tap:", StringComparison.Ordinal) && Growers.ContainsKey(end.Substring(4));
        public static bool IsBuffer(string end) => end != null && end.StartsWith("buffer:", StringComparison.Ordinal) && int.TryParse(end.Substring(7), out int p) && p >= 0 && p < BufferPlaces && end == Buffer(p);
        public static bool IsEnd(string end) => IsTap(end) || IsBuffer(end) || end == Port || end == Out || end == Sorter;
        public static string TapCulture(string end) => end.Substring(4);
        public static int BufferPlace(string end) => int.Parse(end.Substring(7));

        public static (float x, float z) Point(string end) =>
            IsTap(end) ? TapPoint(TapCulture(end)) : IsBuffer(end) ? BufferPoint(BufferPlace(end)) : end == Out ? OutPoint : end == Sorter ? SorterPoint : PortPoint;
        /// <summary>The height a pipe drops to at an end: a tap on a grower's back, a buffer's lid, the incubator's port and outlet, the sorter's hopper.</summary>
        public static float Height(string end) => IsTap(end) ? 1.3f : IsBuffer(end) ? 1.5f : end == Out ? 0.95f : end == Sorter ? 1.6f : 1.25f;
        public static (int c, int r) EndCell(string end) { var p = Point(end); return Cell(p.x, p.z); }
        public static IEnumerable<string> AllEnds => Growers.Keys.Select(Tap).Concat(Enumerable.Range(0, BufferPlaces).Select(Buffer)).Concat(new[] { Port, Out, Sorter });

        public static (int c, int r) Cell(float x, float z) => ((int)Math.Floor((x - MinX) / CellSize), (int)Math.Floor((z - MinZ) / CellSize));
        public static (float x, float z) Centre((int c, int r) cell) => (MinX + (cell.c + 0.5f) * CellSize, MinZ + (cell.r + 0.5f) * CellSize);
        /// <summary>A cell under the lab's ceiling: the back room's, the annex's once it is open, the workshop's once it is.</summary>
        public static bool InLab((int c, int r) cell, bool annex, bool workshop = false) =>
            cell.c >= 0 && cell.r >= 0 && cell.c < Columns && cell.r < Rows &&
            (cell.c >= AnnexColumns ? workshop && cell.r >= AnnexFirstRow && cell.r < AnnexFirstRow + WorkshopRows : cell.r < AnnexFirstRow ? cell.c < BackRoomColumns : annex);
        public static bool Adjacent((int c, int r) a, (int c, int r) b) => Math.Abs(a.c - b.c) + Math.Abs(a.r - b.r) == 1;

        /// <summary>A pipe's length in metres: its overhead run from cell to cell and its drop at each end.</summary>
        public static float Length(IList<(int c, int r)> cells, string from, string to) =>
            (cells.Count - 1) * CellSize + (PipeHeight - Height(from)) + (PipeHeight - Height(to));

        /// <summary>
        /// The cheapest run from one cell to another: a cell costs 1, a bend 0.5 and a crossing 0.25, so pipes run straight
        /// where they can; ties go the same way every time. Open cells are under the lab's ceiling and no other end's; a cell
        /// another pipe runs straight through may be crossed at right angles (the new pipe hops over it), never run along or
        /// turned in. Null when other pipes leave no way through.
        /// </summary>
        public static List<(int c, int r)> Route((int c, int r) from, (int c, int r) to, Func<(int c, int r), bool> open, IReadOnlyDictionary<(int c, int r), char> pipes = null)
        {
            int[] dc = { 1, 0, -1, 0 }, dr = { 0, 1, 0, -1 };
            int Index((int c, int r) x, int d) => (x.c * Rows + x.r) * 5 + d;
            char Taken((int c, int r) x) => pipes != null && pipes.TryGetValue(x, out char s) ? s : ' ';
            var cost = new Dictionary<int, float>();
            var back = new Dictionary<int, int>();
            var queue = new SortedSet<(float g, int i)>();
            int start = Index(from, 4);
            cost[start] = 0; queue.Add((0, start));
            while (queue.Count > 0)
            {
                var (g, i) = queue.Min; queue.Remove(queue.Min);
                if (g > cost[i] + 1e-4f) continue;
                int d = i % 5, cell = i / 5;
                var here = (c: cell / Rows, r: cell % Rows);
                if (here == to)
                {
                    var path = new List<(int c, int r)>();
                    for (int k = i; ; k = back[k]) { int ck = k / 5; path.Add((ck / Rows, ck % Rows)); if (k == start) break; }
                    path.Reverse();
                    return path;
                }
                bool crossing = here != from && Taken(here) != ' ';
                for (int k = 0; k < 4; k++)
                {
                    if (crossing && k != d) continue;
                    var next = (c: here.c + dc[k], r: here.r + dr[k]);
                    if (next.c < 0 || next.r < 0 || next.c >= Columns || next.r >= Rows) continue;
                    float extra = 0;
                    if (next != to)
                    {
                        if (!open(next)) continue;
                        char s = Taken(next);
                        if (s != ' ' && !(s == 'V' && k % 2 == 0 || s == 'H' && k % 2 == 1)) continue;
                        if (s != ' ') extra = 0.25f;
                    }
                    float step = g + 1 + extra + (d != 4 && d != k ? 0.5f : 0);
                    int j = Index(next, k);
                    if (cost.TryGetValue(j, out float old) && step >= old - 1e-4f) continue;
                    cost[j] = step; back[j] = i;
                    queue.Add((step, j));
                }
            }
            return null;
        }

        /// <summary>A pipe's shape at a cell: 'H' running east-west straight through, 'V' north-south, 'X' a bend or an end.</summary>
        public static char Shape(IList<(int c, int r)> cells, int i)
        {
            if (i == 0 || i == cells.Count - 1) return 'X';
            var a = cells[i - 1]; var b = cells[i + 1];
            return a.r == b.r ? 'H' : a.c == b.c ? 'V' : 'X';
        }

        /// <summary>Where pipes run: each cell's shape, 'X' where two cross (full) or where one bends or ends.</summary>
        public static Dictionary<(int c, int r), char> Occupancy(IEnumerable<IList<(int c, int r)>> pipes)
        {
            var taken = new Dictionary<(int c, int r), char>();
            foreach (var cells in pipes)
                for (int i = 0; i < cells.Count; i++)
                    taken[cells[i]] = taken.ContainsKey(cells[i]) ? 'X' : Shape(cells, i);
            return taken;
        }

        /// <summary>
        /// Whether a pipe can join the others: where it shares a cell with one, both run straight through it at right angles;
        /// its own ends may be shared only with pipes ending in the same cell.
        /// </summary>
        public static bool CanJoin(IEnumerable<IList<(int c, int r)>> others, IList<(int c, int r)> cells)
        {
            var list = others.ToList();
            var taken = Occupancy(list);
            var endsOfOthers = new HashSet<(int c, int r)>(list.Where(p => p.Count > 0).SelectMany(p => new[] { p[0], p[p.Count - 1] }));
            for (int i = 0; i < cells.Count; i++)
            {
                if (!taken.TryGetValue(cells[i], out char s)) continue;
                char mine = Shape(cells, i);
                if (mine == 'X') { if (i > 0 && i < cells.Count - 1 || !endsOfOthers.Contains(cells[i]) || list.Any(p => p.Skip(1).Take(p.Count - 2).Contains(cells[i]))) return false; continue; }
                if (!(s == 'H' && mine == 'V' || s == 'V' && mine == 'H')) return false;
            }
            return true;
        }
    }

    /// <summary>A pipe on the lab grid: from a grower's tap to a buffer (a pump's line), from a buffer to the incubator's port, or the
    /// hatchling chute from the incubator's outlet to the sorter (GR-08).</summary>
    public sealed class GrowthPipe
    {
        public int Id;
        /// <summary>Its upstream end (a tap or a buffer) and its downstream end (a buffer or the port).</summary>
        public string From, To;
        public readonly List<(int c, int r)> Cells = new List<(int c, int r)>();
        public float Length;
        public int Paid;
        /// <summary>A grower's tap to a buffer: the line a pump moves culture down.</summary>
        public bool Line => GrowthLab.IsTap(From);
        public bool Chute => From == GrowthLab.Out;
        public string Culture => Line ? GrowthLab.TapCulture(From) : null;
        public string PowerId => "line:" + Id;
    }

    /// <summary>A culture buffer in the annex: the culture its line brings and how many doses it holds.</summary>
    public sealed class GrowthBuffer
    {
        public int Place;
        public string Culture;
        public float Units;
    }

    /// <summary>One machine on the power board in the last tick: what it asked for, what it got and why.</summary>
    public sealed class GrowthPowerUse
    {
        public string Id, Name, Status;
        public float Draw, Got;
    }

    public sealed partial class GrowthShop
    {
        /// <summary>How long one press of the generator's hand crank keeps its unit flowing (the runtime presses every frame while held).</summary>
        public const float CrankWindow = 0.3f;
        public const string DoserPower = "doser";

        /// <summary>GR-07: the lab annex (GR.11's tier 3), opened at night with 3 stars in era 3.</summary>
        public bool Annex;
        public readonly List<GrowthBuffer> Buffers = new List<GrowthBuffer>();
        public readonly List<GrowthPipe> Pipes = new List<GrowthPipe>();
        /// <summary>The power board's priority: the machine first in the list is served first.</summary>
        public readonly List<string> PowerOrder = new List<string>();
        /// <summary>The auto-doser (the incubator's level 4 port): the known recipe it doses, whether it is on, and its batch.</summary>
        public string AutoRecipe;
        public bool AutoOn, AutoBatch;
        public float AutoProgress;
        /// <summary>Batches the auto-doser has started (the objective board's "the incubator dosed a recipe by itself").</summary>
        public int AutoBatches;
        public float PumpProgress { get; private set; }
        public float BufferProgress { get; private set; }
        public float Supply { get; private set; }
        public float Draw { get; private set; }
        public readonly List<GrowthPowerUse> PowerUses = new List<GrowthPowerUse>();
        float crankLeft;

        // ---------------------------------------------------------------- reading

        public static ExpansionDef AnnexDef => GrowthContent.Expansions.First(e => e.Id == "annex");
        /// <summary>Pipes a grower's tap takes: growers' level 3 has one, level 4 two (GR.9).</summary>
        public int Taps => (int)Stat("grower", "tap");
        public bool HasPumps => Level("pump") > 0;
        public bool HandPumps => Level("pump") == 1;
        public bool ElectricPumps => Level("pump") >= 2;
        public float PumpDoseSeconds => HasPumps ? Stat("pump", "doseSeconds") : 0;
        public int PumpUnits => HasPumps ? (int)Stat("pump", "units") : 0;
        public bool HasGenerator => Level("generator") > 0;
        /// <summary>The generator's level 1, the hand crank: its unit flows only while it is cranked.</summary>
        public bool HandCrank => HasGenerator && Stat("generator", "manual") > 0;
        public float GeneratorUnits => HasGenerator ? Stat("generator", "units") : 0;
        public bool Cranking => crankLeft > 0;
        /// <summary>Power on offer now: the bubble turbine's always, the hand crank's while it is cranked.</summary>
        public float SupplyNow => !HasGenerator ? 0 : HandCrank ? (crankLeft > 0 ? GeneratorUnits : 0) : GeneratorUnits;
        /// <summary>The incubator's level 4 port, where the auto-doser works.</summary>
        public bool AutoDoser => Stat("incubator", "autoDoser") > 0;
        public GrowthBuffer BufferAt(int place) => Buffers.FirstOrDefault(b => b.Place == place);
        public GrowthPipe Pipe(int id) => Pipes.FirstOrDefault(p => p.Id == id);
        public IEnumerable<GrowthPipe> PipesAt(string end) => Pipes.Where(p => p.From == end || p.To == end);
        public GrowthPipe InletOf(int place) => Pipes.FirstOrDefault(p => p.To == GrowthLab.Buffer(place));
        public GrowthPipe OutletOf(int place) => Pipes.FirstOrDefault(p => p.From == GrowthLab.Buffer(place));
        public IEnumerable<GrowthPipe> Lines => Pipes.Where(p => p.Line);
        /// <summary>The buffers piped to the incubator's port, which the auto-doser draws from.</summary>
        public IEnumerable<GrowthBuffer> PortBuffers => Buffers.Where(b => b.Culture != null && OutletOf(b.Place) != null);
        public float PortUnits(string culture) => PortBuffers.Where(b => b.Culture == culture).Sum(b => b.Units);
        /// <summary>GR.4's "powered pumps running": electric pumps, the bubble turbine and a grower piped to a buffer.</summary>
        public bool PoweredPumps => Level("pump") >= 2 && Level("generator") >= 2 && Lines.Any();
        /// <summary>GR.4's era 4 gate: 4 stars, powered pumps running, 12 species discovered.</summary>
        public bool EraFourGateMet => Stars >= 4 && PoweredPumps && Discovered.Count >= 12;
        public GrowthPowerUse PowerUse(string id) => PowerUses.FirstOrDefault(u => u.Id == id);
        public string BufferName(int place) => "Buffer " + (place + 1);

        /// <summary>An end's name for the player: "the Bloom tap", "buffer 2", "the incubator's port".</summary>
        public string EndName(string end) =>
            GrowthLab.IsTap(end) ? "the " + Name(GrowthLab.TapCulture(end)) + " tap" : GrowthLab.IsBuffer(end) ? "buffer " + (GrowthLab.BufferPlace(end) + 1) :
            end == GrowthLab.Out ? "the incubator's outlet" : end == GrowthLab.Sorter ? "the sorter" : "the incubator's port";

        // ---------------------------------------------------------------- the lab annex

        /// <summary>Why the lab annex cannot open now (None when it can), with a sentence.</summary>
        public GrowthFailure AnnexCheck(out string why)
        {
            var annex = AnnexDef;
            why = null;
            if (Annex) { why = "Already open"; return GrowthFailure.MaxLevel; }
            if (Era < annex.Era) { why = "Needs Era " + annex.Era + ": 3 stars, a hybrid bred and a Large tank"; return GrowthFailure.Locked; }
            if (Stars < annex.Stars) { why = "Needs " + annex.Stars + " stars"; return GrowthFailure.Locked; }
            if (Phase != GrowthPhase.Night) { why = "Walls open at night"; return GrowthFailure.NotAtNight; }
            if (Credits < annex.Credits) { why = "Needs " + annex.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Opens the back room's north wall into the lab annex (GR.11): room for the generator, the power board and buffers.</summary>
        public GrowthResult BuyAnnex()
        {
            var failure = AnnexCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= AnnexDef.Credits;
            Annex = true;
            Raise(GrowthEventKind.Opened, AnnexDef.Name + " opened", amount: AnnexDef.Credits);
            return GrowthResult.Ok();
        }

        // ---------------------------------------------------------------- buffers

        /// <summary>Why a buffer cannot be placed at a buffer place now (None when it can).</summary>
        public GrowthFailure BufferCheck(int place, out string why)
        {
            why = null;
            if (place < 0 || place >= GrowthLab.BufferPlaces) { why = "Not a buffer place"; return GrowthFailure.NoSuchPlace; }
            if (!Annex) { why = "Open the power room first"; return GrowthFailure.Locked; }
            if (!HasPumps) { why = "Buffers come with pumps and pipes (on the power board)"; return GrowthFailure.Locked; }
            if (BufferAt(place) != null) { why = "A buffer stands here"; return GrowthFailure.PlaceTaken; }
            if (Phase != GrowthPhase.Night) { why = "Buffers are placed at night"; return GrowthFailure.NotAtNight; }
            if (Credits < GrowthContent.BufferCredits) { why = "Needs " + GrowthContent.BufferCredits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        public GrowthResult BuyBuffer(int place)
        {
            var failure = BufferCheck(place, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= GrowthContent.BufferCredits;
            Buffers.Add(new GrowthBuffer { Place = place });
            Raise(GrowthEventKind.Bought, BufferName(place) + " placed", amount: GrowthContent.BufferCredits);
            return GrowthResult.Ok();
        }

        /// <summary>Why a buffer cannot be sold now: its pipes go first, and its culture (GR.15.1: nothing is lost).</summary>
        public GrowthFailure SellBufferCheck(int place, out string why)
        {
            why = null;
            var b = BufferAt(place);
            if (b == null) { why = "No buffer here"; return GrowthFailure.NoSuchPlace; }
            if (PipesAt(GrowthLab.Buffer(place)).Any()) { why = "Take its pipes down first"; return GrowthFailure.NotEmpty; }
            if (b.Units >= 1) { why = "Siphon its " + (int)b.Units + " " + Name(b.Culture) + " out first"; return GrowthFailure.NotEmpty; }
            if (Phase != GrowthPhase.Night) { why = "Buffers are moved at night"; return GrowthFailure.NotAtNight; }
            return GrowthFailure.None;
        }

        public GrowthResult SellBuffer(int place)
        {
            var failure = SellBufferCheck(place, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Buffers.Remove(BufferAt(place));
            int value = GrowthContent.BufferCredits / 2;
            Credits += value;
            Raise(GrowthEventKind.Sold, BufferName(place) + " sold for " + value + " credits", amount: value);
            return GrowthResult.Ok();
        }

        /// <summary>Holding the siphon on a buffer: as many doses a hold as the siphon takes, as from a grower.</summary>
        public GrowthResult CollectBuffer(int place, float seconds)
        {
            var b = BufferAt(place);
            if (b == null || b.Culture == null || b.Units < 1) { BufferProgress = 0; return GrowthResult.Fail(GrowthFailure.GrowerEmpty, BufferName(place) + " is empty"); }
            if (Held.Count >= SiphonCapacity) { BufferProgress = 0; return GrowthResult.Fail(GrowthFailure.SiphonFull); }
            BufferProgress += seconds;
            int collected = 0;
            while (BufferProgress >= HoldSeconds && Held.Count < SiphonCapacity && b.Units >= 1)
            {
                BufferProgress -= HoldSeconds;
                for (int n = 0; n < PerHold && Held.Count < SiphonCapacity && b.Units >= 1; n++) { b.Units -= 1; Held.Add(b.Culture); collected++; }
            }
            if (Held.Count >= SiphonCapacity || b.Units < 1) BufferProgress = 0;
            if (collected > 0) Raise(GrowthEventKind.Collected, collected + " " + Name(b.Culture) + " from " + BufferName(place).ToLowerInvariant(), amount: collected);
            return GrowthResult.Ok(collected.ToString());
        }

        public void StopBuffer() => BufferProgress = 0;

        /// <summary>GR-29 (F18): throws a buffer's doses away. A line still piped into it keeps it for that culture, and fills it again.</summary>
        public GrowthResult PourOutBuffer(int place)
        {
            var b = BufferAt(place);
            if (b == null || b.Culture == null || b.Units <= 0) return GrowthResult.Fail(GrowthFailure.GrowerEmpty, BufferName(place) + " is empty");
            int poured = (int)Math.Floor(b.Units);
            b.Units = 0;
            BufferProgress = 0;
            if (InletOf(place) == null) b.Culture = null;
            return GrowthResult.Ok(poured.ToString());
        }

        // ---------------------------------------------------------------- pipes

        /// <summary>Two picked ends as a pipe's upstream and downstream: a tap to a buffer, a buffer to the port, or the outlet to the sorter, either way round.</summary>
        public static bool Orient(string a, string b, out string from, out string to)
        {
            from = to = null;
            if (GrowthLab.IsTap(b) || a == GrowthLab.Port && GrowthLab.IsBuffer(b) || b == GrowthLab.Out) { var t = a; a = b; b = t; }
            if (GrowthLab.IsTap(a) && GrowthLab.IsBuffer(b) || GrowthLab.IsBuffer(a) && b == GrowthLab.Port || a == GrowthLab.Out && b == GrowthLab.Sorter) { from = a; to = b; return true; }
            return false;
        }

        /// <summary>
        /// Why a pipe cannot be laid between two ends now (None when it can), with its plan: the route along the lab grid,
        /// its length and cost (given whenever a route was found, for the preview, even when the credits are short).
        /// </summary>
        public GrowthFailure PipeCheck(string a, string b, out GrowthPipe plan, out string why)
        {
            plan = null; why = null;
            if (!Annex) { why = "Open the power room first"; return GrowthFailure.Locked; }
            if (!HasPumps) { why = "Buy pumps and pipes on the power board first"; return GrowthFailure.Locked; }
            if (!Orient(a, b, out string from, out string to)) { why = "A pipe runs from a grower's tap to a buffer, from a buffer to the incubator's port, or from the incubator's outlet to the sorter"; return GrowthFailure.NoSuchPlace; }
            int place = GrowthLab.IsBuffer(to) ? GrowthLab.BufferPlace(to) : GrowthLab.IsBuffer(from) ? GrowthLab.BufferPlace(from) : -1;
            var buffer = BufferAt(place);
            if (from == GrowthLab.Out)
            {
                // GR-08: the hatchling chute, from the incubator's outlet to the sorter in the workshop (no pump: clutches slide down it).
                if (!Workshop) { why = "Open the workshop first: the sorter stands there"; return GrowthFailure.Locked; }
                if (!HasSorter) { why = "Buy the sorter in the workshop first"; return GrowthFailure.Locked; }
                if (Chute != null) { why = "The chute is laid already"; return GrowthFailure.Busy; }
            }
            else if (GrowthLab.IsTap(from))
            {
                string culture = GrowthLab.TapCulture(from);
                if (!Owns(culture)) { why = "You don't grow " + Name(culture) + " yet"; return GrowthFailure.NotOwned; }
                if (Taps < 1) { why = "A grower's tap comes with growers' level 3"; return GrowthFailure.Locked; }
                if (Pipes.Count(p => p.From == from) >= Taps) { why = Taps == 1 ? "The " + Name(culture) + " tap is in use" : "Both of the " + Name(culture) + " grower's taps are in use"; return GrowthFailure.Busy; }
                if (buffer == null) { why = "Place a buffer there first"; return GrowthFailure.NoSuchPlace; }
                if (InletOf(place) != null) { why = BufferName(place) + " has its pipe from " + Name(InletOf(place).Culture); return GrowthFailure.Busy; }
                if (buffer.Culture != null && buffer.Culture != culture) { why = BufferName(place) + " holds " + Name(buffer.Culture); return GrowthFailure.Busy; }
            }
            else
            {
                if (!AutoDoser) { why = "The incubator's port comes with its level 4"; return GrowthFailure.Locked; }
                if (buffer == null) { why = "Place a buffer there first"; return GrowthFailure.NoSuchPlace; }
                if (InletOf(place) == null) { why = "Pipe a grower into " + BufferName(place).ToLowerInvariant() + " first"; return GrowthFailure.Locked; }
                if (OutletOf(place) != null) { why = BufferName(place) + " is piped to the port already"; return GrowthFailure.Busy; }
            }
            if (Phase != GrowthPhase.Night) { why = "Pipes are laid at night"; return GrowthFailure.NotAtNight; }
            var cells = RouteFor(from, to);
            if (cells == null || !GrowthLab.CanJoin(Pipes.Select(p => p.Cells), cells)) { why = "No way through: other pipes are in the way. Try another buffer place, or take a pipe off at its buffer"; return GrowthFailure.NoRoute; }
            plan = new GrowthPipe { From = from, To = to, Length = GrowthLab.Length(cells, from, to) };
            plan.Cells.AddRange(cells);
            plan.Paid = (int)Math.Ceiling(plan.Length * GrowthContent.PipeCreditsPerMetre - 1e-4f);
            if (Credits < plan.Paid) { why = "Needs " + plan.Paid + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>The route between two ends through cells no other pipe or other end takes (an end's own cell is shared by its pipes).</summary>
        List<(int c, int r)> RouteFor(string from, string to)
        {
            var ends = new HashSet<(int c, int r)>(GrowthLab.AllEnds.Where(e => e != from && e != to).Select(GrowthLab.EndCell));
            return GrowthLab.Route(GrowthLab.EndCell(from), GrowthLab.EndCell(to), c => GrowthLab.InLab(c, Annex, Workshop) && !ends.Contains(c), GrowthLab.Occupancy(Pipes.Select(p => p.Cells)));
        }

        /// <summary>Lays a pipe at night between two ends: paid by its length; a tap's pipe gives its buffer the culture and joins the power board.</summary>
        public GrowthResult LayPipe(string a, string b)
        {
            var failure = PipeCheck(a, b, out var plan, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= plan.Paid;
            plan.Id = nextId++;
            Pipes.Add(plan);
            if (plan.Line)
            {
                BufferAt(GrowthLab.BufferPlace(plan.To)).Culture = plan.Culture;
                PowerOrder.Add(plan.PowerId);
            }
            Raise(GrowthEventKind.Laid, "Pipe laid: " + EndName(plan.From) + " to " + EndName(plan.To) + " (" + plan.Length.ToString("0.#") + " m, " + plan.Paid + " credits)", amount: plan.Paid);
            CheckEra();
            return GrowthResult.Ok(plan.Id.ToString());
        }

        /// <summary>Takes a pipe down at night for half its cost; its line stops and its culture stays where it is (GR.15.1).</summary>
        public GrowthResult RemovePipe(int id)
        {
            var p = Pipe(id);
            if (p == null) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "No such pipe");
            if (Phase != GrowthPhase.Night) return GrowthResult.Fail(GrowthFailure.NotAtNight, "Pipes are taken down at night");
            if (!p.Line && !p.Chute && AutoBatch) return GrowthResult.Fail(GrowthFailure.Busy, "The auto-doser is dosing from it");
            Pipes.Remove(p);
            PowerOrder.Remove(p.PowerId);
            if (p.Line)
            {
                var b = BufferAt(GrowthLab.BufferPlace(p.To));
                if (b != null && b.Units < 1) { b.Units = 0; b.Culture = null; }
            }
            int value = p.Paid / 2;
            Credits += value;
            Raise(GrowthEventKind.Sold, "Pipe taken down: " + EndName(p.From) + " to " + EndName(p.To) + " (" + value + " credits back)", amount: value);
            return GrowthResult.Ok();
        }

        // ---------------------------------------------------------------- pumps and the generator

        /// <summary>
        /// Holding the hand pump's crank on a grower (pumps' level 1): a dose down the line every pump time while the grower
        /// has culture and the buffer room (GR.8 T1: the buffer fills in bulk instead of a dose at a time in the siphon).
        /// </summary>
        public GrowthResult CrankPump(string culture, float seconds)
        {
            if (!HasPumps) { PumpProgress = 0; return GrowthResult.Fail(GrowthFailure.Locked, "Buy pumps and pipes first"); }
            if (!HandPumps) { PumpProgress = 0; return GrowthResult.Fail(GrowthFailure.Busy, "The pumps are electric: they run by themselves"); }
            var lines = Lines.Where(p => p.Culture == culture).ToList();
            if (lines.Count == 0) { PumpProgress = 0; return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "Pipe the " + Name(culture) + " tap to a buffer first"); }
            GrowthBuffer Room() => lines.Select(p => BufferAt(GrowthLab.BufferPlace(p.To))).FirstOrDefault(b => GrowthContent.BufferCapacity - b.Units >= 1);
            if (Room() == null) { PumpProgress = 0; return GrowthResult.Fail(GrowthFailure.ChamberFull, BufferName(GrowthLab.BufferPlace(lines[0].To)) + " is full"); }
            if (GrowerUnits[culture] < 1) { PumpProgress = 0; return GrowthResult.Fail(GrowthFailure.GrowerEmpty, culture); }
            PumpProgress += seconds;
            int moved = 0; GrowthBuffer last = null;
            while (PumpProgress >= PumpDoseSeconds && GrowerUnits[culture] >= 1 && (last = Room()) != null)
            {
                PumpProgress -= PumpDoseSeconds;
                GrowerUnits[culture] -= 1;
                last.Units += 1;
                moved++;
            }
            if (GrowerUnits[culture] < 1 || Room() == null) PumpProgress = 0;
            if (moved > 0) Raise(GrowthEventKind.Pumped, "Pumped " + moved + " " + Name(culture) + " into " + BufferName(last.Place).ToLowerInvariant(), culture, moved);
            return GrowthResult.Ok(moved.ToString());
        }

        public void StopPumping() => PumpProgress = 0;

        /// <summary>Cranking the generator's level 1: its unit flows for a moment after each press (the runtime presses every frame while held).</summary>
        public GrowthResult CrankGenerator()
        {
            if (!HasGenerator) return GrowthResult.Fail(GrowthFailure.Locked, "Buy the generator first");
            if (!HandCrank) return GrowthResult.Fail(GrowthFailure.Busy, "The " + GrowthContent.Station("generator").Levels[Level("generator") - 1].Change.Split(':')[0].ToLowerInvariant() + " runs by itself");
            crankLeft = CrankWindow;
            return GrowthResult.Ok();
        }

        /// <summary>Moves a machine one place up (toward first served) or down on the power board.</summary>
        public GrowthResult MovePower(string id, int by)
        {
            int i = PowerOrder.IndexOf(id);
            if (i < 0) return GrowthResult.Fail(GrowthFailure.NoSuchPlace, "Not on the power board");
            int j = Math.Max(0, Math.Min(PowerOrder.Count - 1, i + by));
            if (j == i) return GrowthResult.Fail(GrowthFailure.MaxLevel, by < 0 ? "First already" : "Last already");
            PowerOrder.RemoveAt(i);
            PowerOrder.Insert(j, id);
            return GrowthResult.Ok();
        }

        /// <summary>A machine's name on the power board: a pump by its line, the auto-doser, or the sorter.</summary>
        public string PowerName(string id)
        {
            if (id == DoserPower) return "Auto-doser";
            if (id == SorterPower) return "Sorter";
            var p = Pipe(int.Parse(id.Substring(5)));
            return p == null ? id : Name(p.Culture) + " pump to " + BufferName(GrowthLab.BufferPlace(p.To)).ToLowerInvariant();
        }

        /// <summary>Why a line moves nothing now, or null when it has work: its grower empty or its buffer full.</summary>
        string LineIdle(GrowthPipe p)
        {
            var b = BufferAt(GrowthLab.BufferPlace(p.To));
            if (GrowthContent.BufferCapacity - b.Units < 0.001f) return BufferName(b.Place) + " is full";
            if (GrowerUnits[p.Culture] < 0.001f) return "The " + Name(p.Culture) + " grower is empty";
            return null;
        }

        /// <summary>What a line is doing now, in a sentence (the power board and the grower's card).</summary>
        public string LineStatus(GrowthPipe p)
        {
            string idle = LineIdle(p);
            if (idle != null) return idle;
            if (HandPumps) return "Hold the mouse on the crank, on the front of the grower";
            return PowerStatus(PowerUse(p.PowerId), "Pumping");
        }

        static string PowerStatus(GrowthPowerUse use, string running)
        {
            if (use == null || use.Draw <= 0) return running;
            if (use.Got <= 0.001f) return "Waiting for power";
            if (use.Got < use.Draw - 0.001f) return "Slowed: power for " + use.Got.ToString("0.#") + " of " + use.Draw.ToString("0.#") + " units";
            return running;
        }

        // ---------------------------------------------------------------- the auto-doser

        /// <summary>Why the auto-doser cannot take this recipe (None when it can): only a creature already made, never something new.</summary>
        public GrowthFailure AutoRecipeCheck(string species, out string why)
        {
            why = null;
            if (!AutoDoser) { why = "The auto-doser comes with the incubator's level 4"; return GrowthFailure.Locked; }
            var s = GrowthContent.Get(species);
            if (s == null || s.Kind != SpeciesKind.Founder) { why = "Only a founder's mix"; return GrowthFailure.NotAFounder; }
            if (!Discovered.Contains(species)) { why = "Only a creature you have made: the auto-doser never tries something new"; return GrowthFailure.NotDiscovered; }
            if (s.Doses > MaxDoses) { why = "Up to " + MaxDoses + " doses"; return GrowthFailure.ChamberFull; }
            if (AutoBatch) { why = "Wait for the batch being dosed"; return GrowthFailure.Busy; }
            return GrowthFailure.None;
        }

        public GrowthResult SetAutoRecipe(string species)
        {
            var failure = AutoRecipeCheck(species, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            AutoRecipe = species;
            AutoByTargets = false;
            return GrowthResult.Ok(species);
        }

        /// <summary>Switches the auto-doser on or off; off mid-batch leaves its doses in the chamber for the player.</summary>
        public GrowthResult SwitchAutoDoser(bool on)
        {
            if (!AutoDoser) return GrowthResult.Fail(GrowthFailure.Locked, "The auto-doser comes with the incubator's level 4");
            if (on && AutoRecipe == null && !AutoByTargets) return GrowthResult.Fail(GrowthFailure.NoDose, "Choose a recipe first");
            if (AutoOn == on) return GrowthResult.Fail(GrowthFailure.MaxLevel, on ? "On already" : "Off already");
            AutoOn = on;
            if (on && !PowerOrder.Contains(DoserPower)) PowerOrder.Add(DoserPower);
            if (!on) { AutoBatch = false; AutoProgress = 0; }
            return GrowthResult.Ok();
        }

        /// <summary>The recipe's doses not yet in the chamber, in culture order.</summary>
        List<string> AutoMissing()
        {
            var missing = new List<string>();
            if (AutoRecipe == null) return missing;
            var recipe = GrowthContent.Get(AutoRecipe).Recipe;
            for (int i = 0; i < recipe.Length; i++)
            {
                string c = GrowthContent.Cultures[i].Id;
                for (int n = Chamber.Count(x => x == c); n < recipe[i]; n++) missing.Add(c);
            }
            return missing;
        }

        /// <summary>Why the auto-doser is not dosing now, or null when it is or can start.</summary>
        string AutoWaiting()
        {
            if (!AutoDoser) return "Needs the incubator's level 4";
            if (AutoRecipe == null && !AutoByTargets) return "Choose a recipe";
            if (!AutoOn) return "Off";
            if (AutoBatch) { var next = AutoMissing().FirstOrDefault(); return next != null && PortUnits(next) < 1 ? "Waiting for 1 " + Name(next) + " in the port's buffers" : null; }
            if (MossBatch) return "Moss is dosing the incubator";
            if (Incubating) return "Incubating";
            if (Hatchlings.Count > 0) return HasSorter && Chute != null ? "Waiting for the sorter to clear the tray" : "Waiting: take the hatchlings out of the incubator";
            if (Chamber.Count > 0) return "The chamber has doses: start them or take them back";
            string species = NextRecipe;
            if (species == null) return "Every stock target the port can make is met";
            var recipe = GrowthContent.Get(species).Recipe;
            for (int i = 0; i < recipe.Length; i++)
            {
                if (recipe[i] == 0) continue;
                string c = GrowthContent.Cultures[i].Id;
                if (!PortBuffers.Any(b => b.Culture == c)) return "Pipe a " + Name(c) + " buffer to the port";
                if (PortUnits(c) < recipe[i]) return "Waiting for " + recipe[i] + " " + Name(c) + " in the port's buffers";
            }
            return null;
        }

        /// <summary>What the auto-doser is doing now, in a sentence (the incubator card and the power board).</summary>
        public string AutoStatus
        {
            get
            {
                string waiting = AutoWaiting();
                if (waiting != null) return waiting;
                if (!AutoBatch) return "Starting a " + GrowthContent.Get(NextRecipe).Name;
                int total = GrowthContent.Get(AutoRecipe).Doses;
                return PowerStatus(PowerUse(DoserPower), "Dosing a " + GrowthContent.Get(AutoRecipe).Name + ": " + (total - AutoMissing().Count) + " of " + total + " doses");
            }
        }

        /// <summary>One dose from the port's fullest buffer of its culture into the chamber; the batch incubates once complete.</summary>
        void AutoDose()
        {
            var next = AutoMissing().FirstOrDefault();
            if (next == null) return;
            var b = PortBuffers.Where(x => x.Culture == next && x.Units >= 1).OrderByDescending(x => x.Units).ThenBy(x => x.Place).FirstOrDefault();
            if (b == null) return;
            b.Units -= 1;
            Chamber.Add(next);
            if (AutoMissing().Count > 0) return;
            AutoBatch = false;
            AutoProgress = 0;
            string species = AutoRecipe;
            if (StartIncubation().Success) { AutoBatches++; Raise(GrowthEventKind.AutoDosed, "The auto-doser started a " + GrowthContent.Get(species).Name, species); }
        }

        // ---------------------------------------------------------------- the tick

        /// <summary>
        /// GR.8's power budget for one tick: every machine with work asks for its units (an electric pump on a line with culture
        /// and room, the auto-doser with a dose to move); the supply serves them in the board's order, and each runs at the
        /// share it got. Hand pumps and the siphon need none.
        /// </summary>
        void TickPower(float seconds)
        {
            float supply = !HasGenerator ? 0 : HandCrank ? GeneratorUnits * Math.Min(1f, crankLeft / seconds) : GeneratorUnits;
            crankLeft = Math.Max(0, crankLeft - seconds);
            if (AutoDoser && AutoOn && !AutoBatch && AutoWaiting() == null) { AutoRecipe = NextRecipe; AutoBatch = true; AutoProgress = 0; }
            PowerUses.Clear();
            foreach (string id in PowerOrder)
            {
                var use = new GrowthPowerUse { Id = id, Name = PowerName(id) };
                if (id == DoserPower) use.Draw = AutoBatch && AutoWaiting() == null ? GrowthContent.AutoDoserUnits : 0;
                else if (id == SorterPower) use.Draw = HasSorter && SorterIdle() == null ? SorterUnits : 0;
                else { var p = Pipe(int.Parse(id.Substring(5))); use.Draw = p != null && ElectricPumps && LineIdle(p) == null ? PumpUnits : 0; }
                PowerUses.Add(use);
            }
            float left = supply;
            foreach (var use in PowerUses.Where(u => u.Draw > 0)) { use.Got = Math.Min(use.Draw, left); left -= use.Got; }
            Supply = supply;
            Draw = PowerUses.Sum(u => u.Draw);
            foreach (var use in PowerUses.Where(u => u.Got > 0))
            {
                float share = use.Got / use.Draw;
                if (use.Id == DoserPower)
                {
                    AutoProgress += seconds * share;
                    while (AutoBatch && AutoProgress >= GrowthContent.AutoDoseSeconds && AutoWaiting() == null) { AutoProgress -= GrowthContent.AutoDoseSeconds; AutoDose(); }
                    continue;
                }
                if (use.Id == SorterPower)
                {
                    SortProgress += seconds * share;
                    while (SortProgress >= SortSeconds && SorterIdle() == null) { SortProgress -= SortSeconds; SortOne(); }
                    continue;
                }
                var p = Pipe(int.Parse(use.Id.Substring(5)));
                var b = BufferAt(GrowthLab.BufferPlace(p.To));
                float amount = Math.Min(seconds * share / PumpDoseSeconds, Math.Min(GrowerUnits[p.Culture], GrowthContent.BufferCapacity - b.Units));
                GrowerUnits[p.Culture] -= amount;
                b.Units += amount;
            }
            if (!HasSorter || SorterIdle() != null) SortProgress = 0;
            foreach (var use in PowerUses) use.Status = use.Id == DoserPower ? AutoStatus : use.Id == SorterPower ? SorterStatus : LineStatus(Pipe(int.Parse(use.Id.Substring(5))));
            TickLineDay();
        }

        // ---------------------------------------------------------------- saving (version 6)

        void SavePower(GrowthSave s)
        {
            s.annex = Annex;
            s.buffers = Buffers.Select(b => new GrowthSave.Buffer { place = b.Place, culture = b.Culture ?? "", units = b.Units }).ToArray();
            s.pipes = Pipes.Select(p => new GrowthSave.Pipe { id = p.Id, from = p.From, to = p.To, cells = p.Cells.SelectMany(c => new[] { c.c, c.r }).ToArray(), length = p.Length, paid = p.Paid }).ToArray();
            s.powerOrder = PowerOrder.ToArray();
            s.autoRecipe = AutoRecipe ?? ""; s.autoOn = AutoOn; s.autoBatch = AutoBatch; s.autoProgress = AutoProgress; s.autoBatches = AutoBatches;
        }

        /// <summary>Restores the annex, buffers, pipes, the power board and the auto-doser; a reason when they are not consistent.</summary>
        static string RestorePower(GrowthShop shop, GrowthSave s)
        {
            shop.Annex = s.annex;
            shop.Workshop = s.workshop;
            if ((shop.HasGenerator || shop.HasPumps) && !shop.Annex) return "Power without the power room";
            foreach (var b in s.buffers ?? new GrowthSave.Buffer[0])
            {
                if (!shop.Annex) return "A buffer without the power room";
                if (b.place < 0 || b.place >= GrowthLab.BufferPlaces || shop.BufferAt(b.place) != null) return "Bad buffer place " + b.place;
                string culture = string.IsNullOrEmpty(b.culture) ? null : b.culture;
                if (culture != null && GrowthContent.CultureIndex(culture) < 0) return "Unknown culture in a buffer";
                if (b.units < 0 || b.units > GrowthContent.BufferCapacity + 0.001f || culture == null && b.units >= 1) return "A buffer holds more than it can";
                shop.Buffers.Add(new GrowthBuffer { Place = b.place, Culture = culture, Units = b.units });
            }
            foreach (var x in s.pipes ?? new GrowthSave.Pipe[0])
            {
                if (!Orient(x.from, x.to, out string from, out string to) || from != x.from) return "A pipe between ends that are not";
                if (shop.Pipes.Any(p => p.Id == x.id)) return "A pipe is saved twice";
                var cells = new List<(int c, int r)>();
                for (int i = 0; x.cells != null && i + 1 < x.cells.Length; i += 2) cells.Add((x.cells[i], x.cells[i + 1]));
                if (cells.Count == 0 || cells[0] != GrowthLab.EndCell(from) || cells[cells.Count - 1] != GrowthLab.EndCell(to)) return "A pipe that does not reach its ends";
                for (int i = 0; i < cells.Count; i++)
                {
                    if (!GrowthLab.InLab(cells[i], shop.Annex, shop.Workshop)) return "A pipe outside the lab";
                    if (i > 0 && !GrowthLab.Adjacent(cells[i - 1], cells[i])) return "A broken pipe";
                }
                if (!GrowthLab.CanJoin(shop.Pipes.Select(p => p.Cells), cells)) return "Two pipes in one place";
                int place = GrowthLab.IsBuffer(to) ? GrowthLab.BufferPlace(to) : GrowthLab.IsBuffer(from) ? GrowthLab.BufferPlace(from) : -1;
                var buffer = shop.BufferAt(place);
                if (from == GrowthLab.Out) { if (!shop.Workshop || !shop.HasSorter || shop.Chute != null) return "A chute that cannot be"; }
                else if (buffer == null) return "A pipe to a buffer that is not there";
                else if (GrowthLab.IsTap(from))
                {
                    string culture = GrowthLab.TapCulture(from);
                    if (!shop.Owns(culture) || shop.Pipes.Count(p => p.From == from) >= shop.Taps || shop.InletOf(place) != null) return "A tap's pipe that cannot be";
                    if (buffer.Culture != culture) return "A buffer holding another culture than its line";
                }
                else if (!shop.AutoDoser || shop.OutletOf(place) != null || buffer.Culture == null) return "A port pipe that cannot be";
                var pipe = new GrowthPipe { Id = x.id, From = from, To = to, Length = x.length, Paid = Math.Max(0, x.paid) };
                pipe.Cells.AddRange(cells);
                shop.Pipes.Add(pipe);
            }
            foreach (var id in s.powerOrder ?? new string[0])
            {
                bool known = id == DoserPower || id == SorterPower && shop.HasSorter || id.StartsWith("line:", StringComparison.Ordinal) && int.TryParse(id.Substring(5), out int pid) && shop.Pipe(pid) != null && shop.Pipe(pid).Line;
                if (!known || shop.PowerOrder.Contains(id)) return "Unknown power use " + id;
                shop.PowerOrder.Add(id);
            }
            foreach (var line in shop.Lines) if (!shop.PowerOrder.Contains(line.PowerId)) shop.PowerOrder.Add(line.PowerId);
            if (shop.HasSorter && !shop.PowerOrder.Contains(SorterPower)) shop.PowerOrder.Add(SorterPower);
            shop.AutoRecipe = string.IsNullOrEmpty(s.autoRecipe) ? null : s.autoRecipe;
            shop.AutoOn = s.autoOn; shop.AutoBatch = s.autoBatch; shop.AutoProgress = Math.Max(0, s.autoProgress); shop.AutoBatches = Math.Max(0, s.autoBatches);
            if (shop.AutoRecipe != null)
            {
                var r = GrowthContent.Get(shop.AutoRecipe);
                if (r == null || r.Kind != SpeciesKind.Founder || !shop.Discovered.Contains(shop.AutoRecipe) || !shop.AutoDoser) return "An auto-doser recipe that cannot be";
            }
            shop.AutoByTargets = s.autoByTargets;
            if (shop.AutoByTargets && !shop.AutoDoser) return "An auto-doser by targets without the auto-doser";
            if (shop.AutoOn && shop.AutoRecipe == null && !shop.AutoByTargets) return "The auto-doser on without a recipe";
            if (shop.AutoOn && !shop.PowerOrder.Contains(DoserPower)) shop.PowerOrder.Add(DoserPower);
            if (shop.AutoBatch)
            {
                // A batch mid-way is the doses it moved into the chamber so far: never more than the recipe (GR.15.1, no culture twice).
                var recipe = GrowthContent.Get(shop.AutoRecipe).Recipe;
                bool fits = shop.Chamber.All(c => shop.Chamber.Count(x => x == c) <= recipe[GrowthContent.CultureIndex(c)]);
                if (shop.AutoRecipe == null || !shop.AutoOn || !fits || shop.Incubating || shop.Hatchlings.Count > 0) return "An auto-doser batch that cannot be";
            }
            return null;
        }
    }
}
