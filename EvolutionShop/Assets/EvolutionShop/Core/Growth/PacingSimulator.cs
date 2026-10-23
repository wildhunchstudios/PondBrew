using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EvolutionShop.Growth
{
    /// <summary>
    /// How the simulated "typical first-time player" spends time and money (GR-01). Seconds are play time. These are
    /// assumptions about a player, not game rules: playtests replace them with measurements (GR-15).
    /// </summary>
    public sealed class PacingPolicy
    {
        /// <summary>The longest a night's work runs before the player opens the shop.</summary>
        public float NightWorkSeconds = 240;
        /// <summary>A trip between the back room and the shop floor, and the extra a grower visit costs.</summary>
        public float WalkSeconds = 8, GrowerVisitSeconds = 2;
        public int CarryPerTrip = 3;
        public float DoseSeconds = 1, StartSeconds = 2;
        /// <summary>Share of the day the player can still produce while serving: alone, with Pip, with Pip and Nori.</summary>
        public float DayShareAlone = .25f, DayShareWithPip = .6f, DayShareWithNori = .85f;
        /// <summary>Moss's own production, as a share of the day and of a night's work, by level.</summary>
        public float[] MossShare = { .4f, .6f, .8f };
        /// <summary>New mixes tried a night, and new pairs set up a day, while something undiscovered is reachable.</summary>
        public int MixTriesPerNight = 3, PairTriesPerDay = 4;
        /// <summary>Player seconds to set a breeding pair (an experiment, or a known pair for stock).</summary>
        public float PairSetupSeconds = 6;
        /// <summary>New colours chased a night before the exhibition, and after it in free play.</summary>
        public int ColourTriesPerNight = 2, FreePlayColourTriesPerNight = 4;
        /// <summary>Share of sales that grant an exact wish (AD-E: a 25% tip and a reputation point).</summary>
        public float WishShare = .3f, TipShare = .25f;
        /// <summary>Duty periods of wages the player keeps in reserve before buying anything else.</summary>
        public int WageReserveDays = 2;
        /// <summary>While saving for the next gate purchase, the player still buys things costing up to this share of it.</summary>
        public float WhileSavingShare = .1f;
        public float StartingCredits = GrowthContent.StartingCredits;
        public double MaxHours = 16;
    }

    public sealed class PacingEvent
    {
        public double Minute;
        public int Day;
        public string Kind, Text;
    }

    public sealed class PacingDay
    {
        public int Day, Era, Stars, Reputation, Credits, Spent, Rp, Species, Entries, Spaces, Visitors, Sales, Refusals, Revenue, Wages, Produced;
        public double StartMinute, NightSeconds;
    }

    public sealed class PacingReport
    {
        public readonly List<PacingDay> Days = new List<PacingDay>();
        public readonly List<PacingEvent> Events = new List<PacingEvent>();
        /// <summary>Minute each era was reached (era 1 at 0; -1 when never).</summary>
        public readonly double[] EraMinute = { 0, -1, -1, -1, -1 };
        public double FirstUpgradeMinute = -1, SecondTankMinute = -1, FirstHelperMinute = -1, PoweredMinute = -1, LineMinute = -1,
            ExhibitionMinute = -1, CollectAllMinute = -1, LargestStall, EndMinute;
        public string LargestStallAt = "";
        /// <summary>The most creatures on display at once (GR.5's budget: 180 on screen at the final layout).</summary>
        public int MostCreatures;
        public int ExhibitionDay = -1, SpeciesAtExhibition, CrownsAtExhibition, EntriesAtExhibition, EntriesAtEnd, SpeciesAtEnd;
        public readonly List<string> DeadEnds = new List<string>();
        public readonly Dictionary<string, double> HelperMinute = new Dictionary<string, double>();
        public readonly Dictionary<string, double> SpeciesMinute = new Dictionary<string, double>();

        public double FirstMinute(string kind) { foreach (var e in Events) if (e.Kind == kind) return e.Minute; return -1; }

        /// <summary>GR.3's and GR.4's targets, each with the simulated value and whether it is met.</summary>
        public List<(string target, string wanted, string simulated, bool met)> Checks()
        {
            var c = new List<(string, string, string, bool)>();
            void Add(string name, string wanted, double value, bool met) => c.Add((name, wanted, Clock(value), met && value >= 0));
            Add("First upgrade affordable", "within 5 min", FirstUpgradeMinute, FirstUpgradeMinute <= 5);
            Add("Second tank", "about 15 min", SecondTankMinute, SecondTankMinute <= 20);
            for (int e = 2; e <= 5; e++)
            {
                var era = GrowthContent.Eras[e - 1];
                Add("Era " + e + " (" + era.Name + ")", Clock(era.TargetFrom) + "-" + Clock(era.TargetTo), EraMinute[e - 1],
                    EraMinute[e - 1] >= era.TargetFrom && EraMinute[e - 1] <= era.TargetTo);
            }
            Add("First helper", "no earlier than about 1:00", FirstHelperMinute, FirstHelperMinute >= 55);
            Add("First powered automation", "about 1:30-2:00", PoweredMinute, PoweredMinute >= 85 && PoweredMinute <= 125);
            Add("Grand Exhibition available", "about 3:30 (" + Clock(GrowthContent.ExhibitionFrom) + "-" + Clock(GrowthContent.ExhibitionTo) + ")", ExhibitionMinute,
                ExhibitionMinute >= GrowthContent.ExhibitionFrom && ExhibitionMinute <= GrowthContent.ExhibitionTo);
            Add("Collect-all (" + GrowthContent.JournalEntries + " entries)", "8-12 h", CollectAllMinute, CollectAllMinute >= 480 && CollectAllMinute <= 720);
            c.Add(("Longest stretch with nothing new, before the exhibition", "15 min at most", Clock(LargestStall) + LargestStallAt, LargestStall <= 15));
            c.Add(("Most creatures on display at once", "180 at most (GR.5)", MostCreatures.ToString(CultureInfo.InvariantCulture), MostCreatures <= 180));
            return c;
        }

        public bool AllMet => Checks().All(x => x.met);

        public static string Clock(double minutes)
        {
            if (minutes < 0) return "never";
            int m = (int)Math.Round(minutes);
            return (m / 60).ToString(CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public string ToMarkdown()
        {
            var b = new StringBuilder();
            b.AppendLine("| Target | Wanted | Simulated | Met |").AppendLine("| --- | --- | --- | --- |");
            foreach (var (target, wanted, simulated, met) in Checks()) b.AppendLine("| " + target + " | " + wanted + " | " + simulated + " | " + (met ? "yes" : "**no**") + " |");
            b.AppendLine().AppendLine("Timeline (play time h:mm, day):").AppendLine();
            foreach (var e in Events) b.AppendLine("- " + Clock(e.Minute) + " (day " + e.Day + ") " + e.Text);
            b.AppendLine().AppendLine("Day by day (spent is the night before the day; credits are after it):").AppendLine()
                .AppendLine("| Day | Starts | Era | Stars | Rep | Spent | Credits | RP | Species | Entries | Shop spaces | Visitors | Sales | Refused | Revenue | Wages | Night s |")
                .AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var d in Days)
                b.AppendLine("| " + d.Day + " | " + Clock(d.StartMinute) + " | " + d.Era + " | " + d.Stars + " | " + d.Reputation + " | " + d.Spent + " | " + d.Credits + " | " +
                    d.Rp + " | " + d.Species + " | " + d.Entries + " | " + d.Spaces + " | " + d.Visitors + " | " + d.Sales + " | " + d.Refusals + " | " + d.Revenue + " | " +
                    d.Wages + " | " + d.NightSeconds.ToString("0", CultureInfo.InvariantCulture) + " |");
            if (DeadEnds.Count > 0) { b.AppendLine().AppendLine("Dead ends:").AppendLine(); foreach (var d in DeadEnds) b.AppendLine("- " + d); }
            return b.ToString();
        }
    }

    /// <summary>
    /// GR-01's pacing model: a deterministic, engine-free simulation of a first-time player through the five eras to the
    /// Grand Exhibition and on to collect-all, from GrowthContent's tables. Each night the player buys what they can,
    /// tries a few new mixes and pairs, chases a colour and fills the shop's free spaces by hand (or with Moss and,
    /// later, the line); each 5:30 day, visitors each buy the most valuable creature in stock within their budget. It
    /// estimates pacing to tune the tables; it is not the game's rules engine.
    /// </summary>
    public sealed class PacingSimulator
    {
        readonly PacingPolicy p;
        readonly PacingReport report = new PacingReport();
        double clock;
        int day = 1, era = 1, rep, rp, sales;
        double credits;
        readonly HashSet<string> cultures = new HashSet<string> { "bloom", "flow" };
        readonly Dictionary<string, int> level = new Dictionary<string, int>
        { ["siphon"] = 1, ["grower"] = 1, ["incubator"] = 1, ["counter"] = 1, ["bench"] = 1, ["breeding"] = 0, ["vat"] = 0, ["generator"] = 0, ["pump"] = 0, ["sorter"] = 0 };
        readonly List<(string id, int level)> containers = new List<(string, int)> { ("starter", 1) };
        readonly HashSet<string> expansions = new HashSet<string> { "unit-a" };
        readonly Dictionary<string, int> helpers = new Dictionary<string, int> { ["pip"] = 0, ["nori"] = 0, ["moss"] = 0 };
        readonly HashSet<string> discovered = new HashSet<string>();
        readonly HashSet<string> entries = new HashSet<string>();
        readonly Dictionary<string, int> stock = new Dictionary<string, int>();
        double mixProgress, pairProgress, bredCapacity, lastNewMinute;
        bool lineDay, exhibitionReached;
        readonly List<string> bought = new List<string>();
        PacingDay row;

        PacingSimulator(PacingPolicy policy) { p = policy ?? new PacingPolicy(); credits = p.StartingCredits; }

        /// <summary>Runs the model to collect-all (or the policy's time limit).</summary>
        public static PacingReport Run(PacingPolicy policy = null) => new PacingSimulator(policy).Play();

        double Minute => clock / 60.0;
        int Stars => GrowthContent.StarsFor(rep);
        LevelDef Lvl(string station) => level[station] > 0 ? GrowthContent.Station(station).Levels[level[station] - 1] : null;
        float Stat(string station, string stat, float fallback = 0) => Lvl(station)?.Stat(stat, fallback) ?? fallback;

        void Event(string kind, string text) => report.Events.Add(new PacingEvent { Minute = Minute, Day = day, Kind = kind, Text = text });

        /// <summary>Something new happened (a creature, a colour, an unlock or an upgrade): the stall clock restarts.</summary>
        void Touch()
        {
            if (!exhibitionReached && Minute - lastNewMinute > report.LargestStall)
            {
                report.LargestStall = Minute - lastNewMinute;
                report.LargestStallAt = " (" + PacingReport.Clock(lastNewMinute) + "-" + PacingReport.Clock(Minute) + ")";
            }
            lastNewMinute = Minute;
        }

        void New(string kind, string text) { Touch(); Event(kind, text); }

        PacingReport Play()
        {
            double limit = p.MaxHours * 3600;
            while (clock < limit && entries.Count < GrowthContent.JournalEntries)
            {
                row = new PacingDay { Day = day, StartMinute = Minute };
                Night();
                Daytime();
                CheckEras();
                row.Era = era; row.Stars = Stars; row.Reputation = rep; row.Credits = (int)credits; row.Rp = rp;
                row.Species = discovered.Count; row.Entries = entries.Count; row.Spaces = ShopSpaces(true) + ShopSpaces(false);
                report.Days.Add(row);
                day++;
            }
            report.EndMinute = Minute;
            report.EntriesAtEnd = entries.Count;
            report.SpeciesAtEnd = discovered.Count;
            return report;
        }

        // ---------------------------------------------------------------- night

        void Night()
        {
            PayWages();
            Buy();
            double used = 0;
            // Discovery by mixing: a few experiments a night while an undiscovered founder can be made.
            var founder = NextFounder();
            for (int i = 0; i < p.MixTriesPerNight && founder != null; i++)
            {
                used += HandSeconds(founder.Doses);
                mixProgress += 1;
                float needed = Stat("bench", "triesPerFounder", 2.5f);
                if (mixProgress >= needed) { mixProgress -= needed; Discover(founder); founder = NextFounder(); }
            }
            if (founder == null) mixProgress = 0;
            // Discovery by breeding: the tank runs all night and day; the player sets a few new pairs, the rest make stock.
            double cycles = level["breeding"] == 0 ? 0 : (p.NightWorkSeconds + GrowthContent.DaySeconds) / Stat("breeding", "seconds", 60) * Stat("breeding", "pairs", 1);
            double experiments = 0;
            var target = NextBred();
            while (target != null && experiments + 1 <= Math.Min(cycles, p.PairTriesPerDay))
            {
                experiments++;
                used += p.PairSetupSeconds;
                pairProgress += 1;
                float needed = Stat("bench", "triesPerHybrid", 4) * (target.Kind == SpeciesKind.Crown && level["bench"] < 4 ? 2 : 1);
                if (pairProgress >= needed) { pairProgress -= needed; Discover(target); target = NextBred(); }
            }
            if (target == null) pairProgress = 0;
            bredCapacity = cycles - experiments;
            // Colours: tinted batches for journal entries.
            int tries = exhibitionReached ? p.FreePlayColourTriesPerNight : p.ColourTriesPerNight;
            for (int i = 0; i < tries; i++)
            {
                var entry = NextColour();
                if (entry == null) break;
                used += HandSeconds(3);
                AddEntry(entry);
            }
            // Production fills the shop's free spaces within the rest of the night (and Moss works beside the player).
            used += Produce(p.NightWorkSeconds - used, line: false);
            if (helpers["moss"] > 0) Produce(p.NightWorkSeconds * p.MossShare[helpers["moss"] - 1], line: false);
            if (LineRunning) Produce(p.NightWorkSeconds, line: true);
            row.NightSeconds = Math.Max(60, used);
            clock += row.NightSeconds;
            report.MostCreatures = Math.Max(report.MostCreatures, stock.Values.Sum());
        }

        void PayWages()
        {
            foreach (var h in GrowthContent.Helpers)
            {
                int lv = helpers[h.Id];
                if (lv == 0) continue;
                int wage = h.Wages[lv - 1];
                credits -= wage; row.Wages += wage;
            }
        }

        int WageBill() { int n = 0; foreach (var h in GrowthContent.Helpers) if (helpers[h.Id] > 0) n += h.Wages[helpers[h.Id] - 1]; return n; }

        // ---------------------------------------------------------------- purchases

        sealed class Offer { public string Name; public int Credits, Rp, Priority; public Action Apply; }

        void Buy()
        {
            bought.Clear();
            for (int guard = 0; guard < 60; guard++)
            {
                var offers = Offers().ToList();
                double spendable = credits - WageBill() * p.WageReserveDays;
                // The next gate purchase the player is saving for (the cheapest one they cannot yet afford).
                var goal = offers.Where(o => o.Priority == 0 && o.Credits > spendable && o.Rp <= rp).OrderBy(o => o.Credits).FirstOrDefault();
                Offer best = null;
                foreach (var o in offers)
                {
                    if (o.Credits > spendable || o.Rp > rp) continue;
                    if (goal != null && o.Priority > 0 && o.Credits > goal.Credits * p.WhileSavingShare) continue;
                    if (best == null || o.Priority < best.Priority || o.Priority == best.Priority && o.Credits < best.Credits) best = o;
                }
                if (best == null) break;
                credits -= best.Credits; rp -= best.Rp; row.Spent += best.Credits;
                best.Apply();
                bought.Add(best.Name + " (" + (best.Credits > 0 ? best.Credits + " cr" : "") + (best.Credits > 0 && best.Rp > 0 ? ", " : "") + (best.Rp > 0 ? best.Rp + " RP" : "") + ")");
            }
            if (bought.Count > 0) New("buy", "Bought " + string.Join(", ", bought));
        }

        bool Allowed(LevelDef l) => l.Era <= era && l.Stars <= Stars;
        int FreePlaces() => Places() - ShopContainers();
        int Places() { int n = 0; foreach (var e in GrowthContent.Expansions) if (expansions.Contains(e.Id)) n += e.ContainerPlaces; return n; }
        int ShopContainers() => containers.Count(c => !GrowthContent.Container(c.id).Lab);
        bool Owns(string container) => containers.Any(c => c.id == container);
        bool Has(string expansion) => expansions.Contains(expansion);

        IEnumerable<Offer> Offers()
        {
            // Stations: the next level of each, when its era, stars and room allow.
            foreach (var s in GrowthContent.Stations)
            {
                int next = level[s.Id] + 1;
                if (next > s.Levels.Length) continue;
                var l = s.Levels[next - 1];
                if (!Allowed(l) || !StationReady(s.Id, next)) continue;
                string id = s.Id; int lv = next;
                yield return new Offer { Name = s.Name + " L" + lv, Credits = l.Credits, Rp = l.Rp, Priority = StationPriority(id, lv),
                    Apply = () => { level[id] = lv; if (id == "vat" && lv == 1) Culture("mist"); } };
            }
            // Containers: the biggest allowed one for a free place (the Large tank first, as it gates era 3).
            if (FreePlaces() > 0)
                foreach (var c in GrowthContent.Containers)
                {
                    if (c.Lab || !Allowed(c.Levels[0])) continue;
                    string id = c.Id;
                    int priority = id == "large" && !Owns("large") || id == "starter" && ShopContainers() < 2 ? 0 : id == "starter" && Allowed(GrowthContent.Container("large").Levels[0]) ? 3 : 1;
                    yield return new Offer { Name = c.Name, Credits = c.Levels[0].Credits, Priority = priority,
                        Apply = () => { containers.Add((id, 1)); if (ShopContainers() == 2 && report.SecondTankMinute < 0) report.SecondTankMinute = Minute; } };
                }
            // With no free place, a Starter tank can be stored to make room for a bigger container (otherwise a floor full of
            // Starter tanks would block the Large tank that era 3 needs: the game must let containers be stored or sold).
            if (FreePlaces() <= 0 && Owns("starter") && !Owns("large") && Allowed(GrowthContent.Container("large").Levels[0]))
                yield return new Offer { Name = "Large tank (in place of a Starter tank)", Credits = GrowthContent.Container("large").Levels[0].Credits, Priority = 0,
                    Apply = () => { containers[containers.FindIndex(x => x.id == "starter")] = ("large", 1); } };
            for (int i = 0; i < containers.Count; i++)
            {
                var (id, lv) = containers[i];
                var c = GrowthContent.Container(id);
                if (c.Lab || lv >= c.Levels.Length || !Allowed(c.Levels[lv])) continue;
                int index = i;
                yield return new Offer { Name = c.Name + " L" + (lv + 1), Credits = c.Levels[lv].Credits, Priority = 2, Apply = () => containers[index] = (id, lv + 1) };
            }
            // Cultures from the catalog and research.
            foreach (var cu in GrowthContent.Cultures)
            {
                if (cultures.Contains(cu.Id) || cu.Era > era || cu.Stars > Stars || cu.Source == CultureSource.Vat || cu.Source == CultureSource.StarterGrower) continue;
                if (cu.Id == "dusk" && level["bench"] < 3) continue;
                string id = cu.Id;
                yield return new Offer { Name = cu.Name + (cu.Source == CultureSource.Research ? " research" : " grower"), Credits = cu.Credits, Rp = cu.Rp,
                    Priority = 0, Apply = () => Culture(id) };
            }
            // Spaces.
            foreach (var e in GrowthContent.Expansions)
            {
                if (expansions.Contains(e.Id) || e.Era > era || e.Stars > Stars) continue;
                string id = e.Id;
                yield return new Offer { Name = e.Name, Credits = e.Credits, Priority = 0,
                    Apply = () => expansions.Add(id) };
            }
            // Helpers: earned by stars plus an achievement, then trained.
            foreach (var h in GrowthContent.Helpers)
            {
                int lv = helpers[h.Id];
                string id = h.Id;
                if (lv == 0)
                {
                    if (Stars < h.Stars || !HelperGate(id)) continue;
                    yield return new Offer { Name = "hire " + h.Name, Credits = h.Fee + h.Wages[0] * p.WageReserveDays, Priority = 0,
                        Apply = () =>
                        {
                            helpers[id] = 1; credits += h.Wages[0] * p.WageReserveDays; row.Spent -= h.Wages[0] * p.WageReserveDays;
                            report.HelperMinute[id] = Minute;
                            if (report.FirstHelperMinute < 0) report.FirstHelperMinute = Minute;
                        } };
                }
                else if (lv <= h.TrainCosts.Length && Stars >= h.Stars + lv)
                    yield return new Offer { Name = "train " + h.Name + " L" + (lv + 1), Credits = h.TrainCosts[lv - 1], Priority = 2, Apply = () => helpers[id] = lv + 1 };
            }
        }

        bool HelperGate(string id)
        {
            switch (id)
            {
                case "pip": return sales >= GrowthContent.PipSalesGate;
                case "nori": return Owns("large");
                case "moss": return level["generator"] >= 2;
                default: return false;
            }
        }

        /// <summary>Machines need their room; turbines need the annex; the line's machines need the workshop and power.</summary>
        bool StationReady(string id, int next)
        {
            switch (id)
            {
                case "vat": return Has("annex");
                case "generator": return Has("annex") && (next < 3 || Has("workshop"));
                case "pump": return Has("annex") && (next == 1 || level["generator"] >= 2);
                case "sorter": return Has("workshop") && level["generator"] >= 2;
                case "incubator": return next < 4 || level["generator"] >= 2 && level["pump"] >= 2;
                default: return true;
            }
        }

        /// <summary>0: opens the next gate; 1: more stock, faster hands; 2: everything else.</summary>
        int StationPriority(string id, int lv)
        {
            switch (id)
            {
                case "breeding": return lv == 1 ? 0 : 2;
                case "generator": return lv <= 2 ? 0 : 2;
                case "pump": return lv <= 2 ? 0 : 2;
                case "sorter": return 0;
                case "incubator": return lv == 4 ? 0 : 1;
                case "vat": return lv == 1 ? 0 : 2;
                case "bench": return lv == 3 && era >= 5 ? 0 : 1;
                case "siphon": case "grower": case "counter": return 1;
                default: return 2;
            }
        }

        void Culture(string id)
        {
            if (cultures.Add(id)) Event("culture", GrowthContent.Cultures[GrowthContent.CultureIndex(id)].Name + " culture owned");
        }

        // ---------------------------------------------------------------- discovery

        SpeciesDef NextFounder()
        {
            int maxDoses = (int)Stat("incubator", "maxDoses", 3);
            return GrowthContent.Species
                .Where(s => s.Kind == SpeciesKind.Founder && !discovered.Contains(s.Id) && s.Doses <= maxDoses && GrowthContent.Family(s.Family).Era <= era && OwnsRecipe(s))
                .OrderBy(s => GrowthContent.Family(s.Family).Era).FirstOrDefault();
        }

        bool OwnsRecipe(SpeciesDef s)
        {
            for (int i = 0; i < s.Recipe.Length; i++) if (s.Recipe[i] > 0 && !cultures.Contains(GrowthContent.Cultures[i].Id)) return false;
            return true;
        }

        bool Breedable(string species) => Stat("breeding", "largeParents") > 0 || GrowthContent.Get(species).Size != SizeClass.Large;

        SpeciesDef NextBred()
        {
            if (level["breeding"] == 0) return null;
            foreach (var s in GrowthContent.Species.OrderBy(s => GrowthContent.Family(s.Family).Era).ThenBy(s => s.Kind == SpeciesKind.Crown ? 1 : 0))
            {
                if (discovered.Contains(s.Id) || s.Kind == SpeciesKind.Founder || GrowthContent.Family(s.Family).Era > era) continue;
                string a, b;
                if (s.Kind == SpeciesKind.Crown) { a = s.PairA; b = s.PairB; }
                else { var pair = GrowthContent.IntendedPairs.First(x => x.hybrid == s.Id); a = pair.a; b = pair.b; }
                if (discovered.Contains(a) && discovered.Contains(b) && Breedable(a) && Breedable(b)) return s;
            }
            return null;
        }

        void Discover(SpeciesDef s)
        {
            if (!discovered.Add(s.Id)) return;
            report.SpeciesMinute[s.Id] = Minute;
            AddEntry(s.Id + "/" + Colour.Classic);
            rp += GrowthContent.RpFor(s.Kind);
            New("discovery", "Discovered " + s.Name + " (" + s.Kind.ToString().ToLowerInvariant() + ", " + GrowthContent.Family(s.Family).Name + ", " + s.Size.ToString().ToLowerInvariant() +
                "; " + discovered.Count + "/" + GrowthContent.SpeciesCount + ")");
        }

        void AddEntry(string entry)
        {
            if (!entries.Add(entry)) return;
            if (!entry.EndsWith("/" + Colour.Classic)) { rp += GrowthContent.RpColour; Touch(); }
            if (entries.Count == GrowthContent.JournalEntries) { report.CollectAllMinute = Minute; Event("collect-all", "Collect-all: every journal entry"); }
        }

        /// <summary>The next colour entry the player can make: a tint for founders (incubator L3) or bred species (breeding L2); Shimmer needs the vat's L3.</summary>
        string NextColour()
        {
            bool founders = Stat("incubator", "tint") > 0, bred = Stat("breeding", "tint") > 0, shimmer = Stat("vat", "shimmer") > 0;
            foreach (var colour in new[] { Colour.Sunny, Colour.Deep, Colour.Shimmer })
            {
                if (colour == Colour.Shimmer && !shimmer) continue;
                foreach (var s in GrowthContent.Species)
                {
                    if (!discovered.Contains(s.Id) || !(s.Kind == SpeciesKind.Founder ? founders : bred && Breedable(s.Kind == SpeciesKind.Crown ? s.PairA : s.Id))) continue;
                    string entry = s.Id + "/" + colour;
                    if (!entries.Contains(entry)) return entry;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- production and the day

        bool LineRunning => Stat("incubator", "autoDoser") > 0 && level["pump"] >= 2 && level["generator"] >= 2 && level["sorter"] >= 1;

        /// <summary>Player seconds for one creature of this many doses with today's tools.</summary>
        double HandSeconds(int doses)
        {
            float incubation = Stat("incubator", "seconds", 12) / Stat("incubator", "clutch", 1);
            float collectFactor = level["pump"] > 0 ? Stat("pump", "collectFactor", 1) : 1;
            double collect = (doses * Stat("siphon", "holdSeconds", .8f) / Stat("siphon", "perHold", 1) + p.GrowerVisitSeconds) * collectFactor;
            bool autoDosed = Stat("incubator", "autoDoser") > 0 && level["pump"] >= 2 && level["generator"] >= 2;
            double dosing = autoDosed ? 0 : doses * p.DoseSeconds + p.StartSeconds;
            double carry = level["sorter"] > 0 || helpers["nori"] > 0 ? 0 : (p.WalkSeconds + ExtraWalk()) / p.CarryPerTrip;
            return Math.Max(collect + dosing + carry, incubation);
        }

        double LineSeconds() => Stat("incubator", "seconds", 12) / Stat("incubator", "clutch", 1);

        double ExtraWalk() { double n = 0; foreach (var e in GrowthContent.Expansions) if (expansions.Contains(e.Id)) n += e.ExtraWalkSeconds; return n; }

        int ShopSpaces(bool largeCapable)
        {
            int n = 0;
            foreach (var (id, lv) in containers)
            {
                var c = GrowthContent.Container(id);
                if (c.Lab || (c.Largest == SizeClass.Large) != largeCapable) continue;
                n += (int)c.Levels[lv - 1].Stat("spaces");
            }
            return n;
        }

        int UsedSpaces(bool largeOnly)
        {
            int n = 0;
            foreach (var kv in stock) { var s = GrowthContent.Get(kv.Key); if (!largeOnly || s.Size == SizeClass.Large) n += (int)s.Size * kv.Value; }
            return n;
        }

        /// <summary>
        /// Fills free shop spaces toward today's demand (Wanted) within the seconds given; returns seconds used. Never
        /// overstocks a species beyond its demand, so cheap stock is not crowded out by creatures nobody can afford.
        /// </summary>
        double Produce(double budget, bool line)
        {
            if (budget <= 0) return 0;
            double used = 0;
            var want = Wanted();
            for (int guard = 0; guard < 500; guard++)
            {
                int free = ShopSpaces(true) + ShopSpaces(false) - UsedSpaces(false);
                int freeLarge = ShopSpaces(true) - UsedSpaces(true);
                SpeciesDef pick = null; int deficit = 0;
                foreach (var kv in want)
                {
                    var s = GrowthContent.Get(kv.Key);
                    int d = kv.Value - (stock.TryGetValue(kv.Key, out int have) ? have : 0);
                    if (d <= deficit || (int)s.Size > free || s.Size == SizeClass.Large && (int)s.Size > freeLarge || s.Kind != SpeciesKind.Founder && bredCapacity < 1) continue;
                    pick = s; deficit = d;
                }
                if (pick == null) break;
                double cost = pick.Kind != SpeciesKind.Founder ? p.PairSetupSeconds : line ? LineSeconds() : HandSeconds(pick.Doses);
                if (used + cost > budget) break;
                used += cost;
                if (pick.Kind != SpeciesKind.Founder) bredCapacity -= 1;
                stock[pick.Id] = (stock.TryGetValue(pick.Id, out int n) ? n : 0) + 1;
                row.Produced++;
            }
            return used;
        }

        int VisitorsToday()
        {
            int atOnce = 0;
            foreach (var e in GrowthContent.Expansions) if (expansions.Contains(e.Id)) atOnce += e.VisitorsAtOnce;
            return (int)Math.Min(GrowthContent.DaySeconds / GrowthContent.ArrivalSeconds[Stars - 1], atOnce * GrowthContent.DaySeconds / GrowthContent.VisitSeconds);
        }

        bool Makeable(SpeciesDef s) => s.Kind == SpeciesKind.Founder || level["breeding"] > 0 && Breedable(s.Kind == SpeciesKind.Crown ? s.PairA : s.Id);

        /// <summary>
        /// Today's demand: for each visitor type, its share of today's visitors as the known species with the best price
        /// per space within its budget (bred ones only while the breeding tank has cycles to spare).
        /// </summary>
        Dictionary<string, int> Wanted()
        {
            var want = new Dictionary<string, int>();
            int star = Stars - 1, visitors = VisitorsToday();
            int total = GrowthContent.Visitors.Sum(v => v.Weights[star]);
            double bred = bredCapacity;
            foreach (var v in GrowthContent.Visitors.Where(v => v.Weights[star] > 0).OrderByDescending(v => v.Budgets[star]))
            {
                int n = (int)Math.Ceiling(visitors * (double)v.Weights[star] / total);
                var pick = discovered.Select(GrowthContent.Get).Where(s => s.Price <= v.Budgets[star] && Makeable(s))
                    .OrderByDescending(s => (double)s.Price / (int)s.Size).ThenByDescending(s => s.Price)
                    .FirstOrDefault(s => s.Kind == SpeciesKind.Founder || bred >= n);
                if (pick == null) continue;
                if (pick.Kind != SpeciesKind.Founder) bred -= n;
                want[pick.Id] = (want.TryGetValue(pick.Id, out int had) ? had : 0) + n;
            }
            return want;
        }

        void Daytime()
        {
            int star = Stars - 1;
            float share = helpers["nori"] > 0 && helpers["pip"] > 0 ? p.DayShareWithNori : helpers["pip"] > 0 ? p.DayShareWithPip : p.DayShareAlone;
            int visitors = VisitorsToday();
            row.Visitors = visitors;
            // Visitors in weighted round-robin; each buys the most valuable creature in stock within budget. Stock made
            // during the day (by the player between customers, by Moss, by the line) arrives in four waves.
            var types = GrowthContent.Visitors.Where(v => v.Weights[star] > 0).ToList();
            var credit = new double[types.Count];
            int total = types.Sum(v => v.Weights[star]);
            double hands = GrowthContent.DaySeconds * share, moss = helpers["moss"] > 0 ? GrowthContent.DaySeconds * p.MossShare[helpers["moss"] - 1] : 0;
            int cheapest = GrowthContent.Stations.Min(s => s.Levels.Length > 1 ? s.Levels[1].Credits : int.MaxValue);
            int wishEvery = (int)Math.Round(1 / Math.Max(.01f, p.WishShare));
            for (int i = 0; i < visitors; i++)
            {
                if (i % Math.Max(1, visitors / 4) == 0)
                {
                    Produce(hands / 4, line: false);
                    if (moss > 0) Produce(moss / 4, line: false);
                    if (LineRunning) Produce(GrowthContent.DaySeconds / 4, line: true);
                }
                int pick = 0;
                for (int t = 0; t < types.Count; t++) { credit[t] += types[t].Weights[star]; if (credit[t] > credit[pick]) pick = t; }
                credit[pick] -= total;
                int budget = types[pick].Budgets[star];
                string best = null; int bestPrice = -1;
                foreach (var kv in stock)
                {
                    if (kv.Value <= 0) continue;
                    int price = GrowthContent.Get(kv.Key).Price;
                    if (price <= budget && price > bestPrice) { best = kv.Key; bestPrice = price; }
                }
                if (best == null) { row.Refusals++; rep = Math.Max(0, rep + GrowthContent.RepPerRefusal); continue; }
                if (--stock[best] == 0) stock.Remove(best);
                sales++; row.Sales++;
                bool wish = sales % wishEvery == 0;
                double tip = wish ? bestPrice * p.TipShare : 0;
                credits += bestPrice + tip; row.Revenue += bestPrice + (int)Math.Round(tip);
                rep = Math.Min(GrowthContent.ReputationMax, rep + GrowthContent.RepPerSale + (wish ? GrowthContent.RepPerWish : 0));
                if (report.FirstUpgradeMinute < 0 && credits >= cheapest) report.FirstUpgradeMinute = Minute + (i + 1.0) / visitors * GrowthContent.DaySeconds / 60;
            }
            clock += GrowthContent.DaySeconds;
            if (LineRunning && !lineDay) { lineDay = true; report.LineMinute = Minute; New("automation", "A full day on the line (cultures to sale without hands)"); }
        }

        // ---------------------------------------------------------------- eras and the ending

        void CheckEras()
        {
            if (report.PoweredMinute < 0 && level["generator"] >= 2 && level["pump"] >= 2) { report.PoweredMinute = Minute; New("automation", "Powered pumps running"); }
            for (bool advanced = true; advanced && era < 5;)
            {
                advanced = false;
                int stars = Stars;
                bool gate;
                switch (era + 1)
                {
                    case 2: gate = stars >= 2 && discovered.Count >= 3 && ShopContainers() >= 2; break;
                    case 3: gate = stars >= 3 && discovered.Any(id => GrowthContent.Get(id).Kind == SpeciesKind.Hybrid) && Owns("large"); break;
                    case 4: gate = stars >= 4 && report.PoweredMinute >= 0 && discovered.Count >= 12; break;
                    default: gate = stars >= 5 && lineDay && discovered.Any(id => GrowthContent.Get(id).Size == SizeClass.Large); break;
                }
                if (!gate) continue;
                era++; advanced = true;
                report.EraMinute[era - 1] = Minute;
                New("era", "**Era " + era + ": " + GrowthContent.Eras[era - 1].Name + "** (" + stars + " stars, " + discovered.Count + " species, " + (int)credits + " credits)");
            }
            int crowns = discovered.Count(id => GrowthContent.Get(id).Kind == SpeciesKind.Crown);
            bool everyFamily = GrowthContent.Families.All(f => discovered.Any(id => GrowthContent.Get(id).Family == f.Id));
            if (!exhibitionReached && era == 5 && Has("unit-c") && Has("workshop") && Has("exhibition-hall") && lineDay && everyFamily &&
                discovered.Count >= GrowthContent.ExhibitionSpecies && crowns >= GrowthContent.ExhibitionCrowns)
            {
                New("ending", "**The Grand Exhibition is available** (" + discovered.Count + " species, " + crowns + " crowns, " + entries.Count + " entries)");
                exhibitionReached = true;
                report.ExhibitionMinute = Minute; report.ExhibitionDay = day;
                report.SpeciesAtExhibition = discovered.Count; report.CrownsAtExhibition = crowns; report.EntriesAtExhibition = entries.Count;
            }
            if (!exhibitionReached && Minute - lastNewMinute > 30)
                report.DeadEnds.Add("Day " + day + " (" + PacingReport.Clock(Minute) + "): nothing new for " + PacingReport.Clock(Minute - lastNewMinute));
        }
    }
}
