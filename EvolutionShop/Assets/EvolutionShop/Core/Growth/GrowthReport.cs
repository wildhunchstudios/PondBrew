using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EvolutionShop.Growth
{
    /// <summary>
    /// GR-01's review documents, written from GrowthContent and the pacing model: the roster and tables (ROSTER.md), the
    /// pacing report for a typical, a careful and a quick player (PACING.md), and the curve as a picture
    /// (pacing-curve.svg). Engine-free; the Editor command EvolutionShop.Editor.GrowthReports.WriteGr01 calls WriteAll.
    /// </summary>
    public static class GrowthReport
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The players the pacing report compares: the typical one the tables are tuned for, and two around it.</summary>
        public static IReadOnlyList<(string name, string description, PacingPolicy policy)> Players => new[]
        {
            ("Typical", "4-minute nights, 3 new mixes and 4 new pairs a day, a quarter of the day free while serving alone, 30% of sales grant a wish", new PacingPolicy()),
            ("Careful", "5-minute nights, 2 new mixes and 3 new pairs a day, a fifth of the day free while serving alone, 20% wishes",
                new PacingPolicy { NightWorkSeconds = 300, MixTriesPerNight = 2, PairTriesPerDay = 3, DayShareAlone = .2f, WishShare = .2f }),
            ("Quick", "3-minute nights, 4 new mixes and 6 new pairs a day, over a third of the day free while serving alone, 40% wishes",
                new PacingPolicy { NightWorkSeconds = 180, MixTriesPerNight = 4, PairTriesPerDay = 6, DayShareAlone = .35f, WishShare = .4f }),
        };

        /// <summary>Writes ROSTER.md, PACING.md and pacing-curve.svg into the directory; returns their paths.</summary>
        public static string[] WriteAll(string directory)
        {
            Directory.CreateDirectory(directory);
            var runs = Players.Select(p => (p.name, p.description, report: PacingSimulator.Run(p.policy))).ToList();
            var typical = runs[0].report;
            var files = new[]
            {
                (Path.Combine(directory, "ROSTER.md"), Roster(typical)),
                (Path.Combine(directory, "PACING.md"), Pacing(runs)),
                (Path.Combine(directory, "pacing-curve.svg"), CurveSvg(typical)),
            };
            foreach (var (path, text) in files) File.WriteAllText(path, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
            return files.Select(f => f.Item1).ToArray();
        }

        // ---------------------------------------------------------------- roster

        public static string Recipe(int[] recipe)
        {
            var parts = new List<string>();
            for (int i = 0; i < recipe.Length; i++) if (recipe[i] > 0) parts.Add(GrowthContent.Cultures[i].Name + " " + recipe[i]);
            return string.Join(" + ", parts);
        }

        /// <summary>How many of the 630 pairs of two different species breed each species.</summary>
        public static Dictionary<string, int> PairCoverage()
        {
            var count = GrowthContent.Species.ToDictionary(s => s.Id, s => 0);
            var sp = GrowthContent.Species;
            for (int i = 0; i < sp.Length; i++)
                for (int j = i + 1; j < sp.Length; j++) count[GrowthRules.Breed(sp[i].Id, sp[j].Id).Id]++;
            return count;
        }

        /// <summary>What every mix of 2-6 doses lands on with all seven cultures owned (GR.7.1: no mix fails).</summary>
        public static (int mixes, Dictionary<string, int> landing) MixCoverage()
        {
            var owned = GrowthContent.Cultures.Select(c => c.Id).ToList();
            var landing = new Dictionary<string, int>();
            int mixes = 0;
            var doses = new int[GrowthContent.Cultures.Length];
            void Visit(int index, int left)
            {
                if (index == doses.Length)
                {
                    if (doses.Sum() < 2) return;
                    var result = GrowthRules.Mix(doses, owned);
                    mixes++;
                    landing[result.Species.Id] = (landing.TryGetValue(result.Species.Id, out int n) ? n : 0) + 1;
                    return;
                }
                for (int v = 0; v <= left; v++) { doses[index] = v; Visit(index + 1, left - v); }
                doses[index] = 0;
            }
            Visit(0, 6);
            return (mixes, landing);
        }

        static string Cost(int credits, int rp) => credits > 0 && rp > 0 ? credits + " cr + " + rp + " RP" : rp > 0 ? rp + " RP" : credits > 0 ? credits + " cr" : "free";
        static string Gate(int era, int stars) => "era " + era + ", " + stars + "★";

        public static string Roster(PacingReport typical = null)
        {
            typical = typical ?? PacingSimulator.Run();
            var b = new StringBuilder();
            var sp = GrowthContent.Species;
            int founders = sp.Count(s => s.Kind == SpeciesKind.Founder), hybrids = sp.Count(s => s.Kind == SpeciesKind.Hybrid), crowns = sp.Count(s => s.Kind == SpeciesKind.Crown);
            b.AppendLine("# GR-01 roster and tables").AppendLine()
             .AppendLine("Generated from `Core/Growth/GrowthContent.cs` by `EvolutionShop.Editor.GrowthReports.WriteGr01` (GR-01). Names are provisional (GR.17); every number is a first-pass value tuned by the pacing model (PACING.md) and to be changed by playtests.").AppendLine()
             .AppendLine("**" + sp.Length + " species** in " + GrowthContent.Families.Length + " families (" + founders + " founders made by mixing, " + hybrids + " hybrids and " + crowns + " crowns made by breeding), " +
                 "**" + GrowthContent.JournalEntries + " journal entries** (each species in " + GrowthContent.ColourCount + " colours), **" + GrowthContent.Cultures.Length + " cultures**. `GrowthRules.Validate()` reports no problems: unique recipes and breeding values, every founder's recipe makes it, every hybrid's intended pair breeds it, and every species is reachable by its family's era.").AppendLine();

            b.AppendLine("## Cultures (GR.6)").AppendLine().AppendLine("| Culture | Colour | Source | Gate | Cost |").AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var c in GrowthContent.Cultures)
                b.AppendLine("| " + c.Name + " | " + c.Hex + " | " + Source(c.Source) + " | " + (c.Era <= 1 ? "start" : Gate(c.Era, c.Stars)) + " | " + Cost(c.Credits, c.Rp) + " |");

            b.AppendLine().AppendLine("## Families (GR.7)").AppendLine().AppendLine("| Family | Size (spaces) | Lead culture | Era | Founders / hybrids / crown |").AppendLine("| --- | --- | --- | ---: | --- |");
            foreach (var f in GrowthContent.Families)
            {
                var members = sp.Where(s => s.Family == f.Id).ToList();
                b.AppendLine("| " + f.Name + " | " + f.Size + " (" + (int)f.Size + ") | " + GrowthContent.Cultures[GrowthContent.CultureIndex(f.LeadCulture)].Name + " | " + f.Era + " | " +
                    members.Count(s => s.Kind == SpeciesKind.Founder) + " / " + members.Count(s => s.Kind == SpeciesKind.Hybrid) + " / " + members.Count(s => s.Kind == SpeciesKind.Crown) + " |");
            }

            var pairs = PairCoverage();
            b.AppendLine().AppendLine("## Species (GR.7)").AppendLine()
             .AppendLine("*Made by*: a founder's recipe (any other mix makes the nearest founder); a hybrid's intended pair (any pair whose breeding values average nearest it also makes it); a crown's special pair (the only way). *Pairs*: how many of the 630 pairs of two different species breed it. *Found*: when the typical player in PACING.md first makes it.").AppendLine()
             .AppendLine("| # | Species | Family | Size | Kind | Made by | Breeding value | Price | Temperament / water | Pairs | Found |").AppendLine("| ---: | --- | --- | --- | --- | --- | ---: | ---: | --- | ---: | ---: |");
            int n = 0;
            foreach (var s in sp)
            {
                string made = s.Kind == SpeciesKind.Founder ? Recipe(s.Recipe)
                    : s.Kind == SpeciesKind.Crown ? "special pair: " + Name(s.PairA) + " × " + Name(s.PairB)
                    : "pair: " + string.Join(" × ", GrowthContent.IntendedPairs.Where(p => p.hybrid == s.Id).Select(p => Name(p.a) + " × " + Name(p.b)));
                b.AppendLine("| " + ++n + " | " + s.Name + " | " + GrowthContent.Family(s.Family).Name + " | " + s.Size + " | " + s.Kind + " | " + made + " | " + s.BreedingValue + " | " + s.Price + " | " +
                    s.Temperament + " / " + s.Water + " | " + pairs[s.Id] + " | " + (typical.SpeciesMinute.TryGetValue(s.Id, out double m) ? PacingReport.Clock(m) : "—") + " |");
            }

            var (mixes, landing) = MixCoverage();
            b.AppendLine().AppendLine("## Mixing never fails (GR.7.1)").AppendLine()
             .AppendLine("With all seven cultures and a level 3 incubator, there are " + mixes + " different mixes of 2–6 doses; " + GrowthContent.Species.Count(s => s.Kind == SpeciesKind.Founder) +
                 " of them are exact recipes and every one makes a founder. Where the mixes land:").AppendLine()
             .AppendLine("| Founder | Mixes that make it |").AppendLine("| --- | ---: |");
            foreach (var kv in landing.OrderByDescending(k => k.Value)) b.AppendLine("| " + Name(kv.Key) + " | " + kv.Value + " |");

            b.AppendLine().AppendLine("## Stations and their levels (GR.9)").AppendLine().AppendLine("| Station | Level | Cost | Gate | What changes |").AppendLine("| --- | ---: | --- | --- | --- |");
            foreach (var st in GrowthContent.Stations)
                foreach (var l in st.Levels)
                    b.AppendLine("| " + st.Name + " | " + l.Level + " | " + (l.Level == 1 && l.Credits == 0 && l.Rp == 0 ? "start" : Cost(l.Credits, l.Rp)) + " | " + Gate(l.Era, l.Stars) + " | " + l.Change + " |");

            b.AppendLine().AppendLine("## Containers (GR.5)").AppendLine().AppendLine("| Container | Holds up to | Level | Cost | Gate | Spaces |").AppendLine("| --- | --- | ---: | --- | --- | ---: |");
            foreach (var c in GrowthContent.Containers)
                foreach (var l in c.Levels)
                    b.AppendLine("| " + c.Name + (c.Lab ? " (lab)" : "") + " | " + c.Largest + " | " + l.Level + " | " + Cost(l.Credits, l.Rp) + " | " + Gate(l.Era, l.Stars) + " | " + l.Stat("spaces") + " |");

            b.AppendLine().AppendLine("## Spaces (GR.11)").AppendLine().AppendLine("| Space | Room | Cost | Gate | Container places | Visitors at once |").AppendLine("| --- | --- | --- | --- | ---: | ---: |");
            foreach (var e in GrowthContent.Expansions)
                b.AppendLine("| " + e.Name + " | " + e.Room + " | " + (e.Credits == 0 ? "start" : Cost(e.Credits, 0)) + " | " + Gate(e.Era, e.Stars) + " | " + e.ContainerPlaces + " | +" + e.VisitorsAtOnce + " |");

            b.AppendLine().AppendLine("## Helpers (GR.10)").AppendLine().AppendLine("| Helper | Job | Earned with | Hire | Wage a duty period (L1-L3) | Training (L2, L3) |").AppendLine("| --- | --- | --- | ---: | --- | --- |");
            foreach (var h in GrowthContent.Helpers)
                b.AppendLine("| " + h.Name + " | " + h.Job + " | " + (h.Id == "pip" ? h.Stars + "★ and " + GrowthContent.PipSalesGate + " sales" : h.Gate) + " | " + h.Fee + " cr | " + string.Join(" / ", h.Wages) + " | " + string.Join(", ", h.TrainCosts.Select(c => c + " cr")) + " |");

            b.AppendLine().AppendLine("## Eras (GR.4)").AppendLine().AppendLine("| Era | Gate | Goal shown | Target to reach it | Typical player |").AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var e in GrowthContent.Eras)
                b.AppendLine("| " + e.Number + " " + e.Name + " | " + e.Gate + " | " + e.Goal + " | " + (e.Number == 1 ? "start" : PacingReport.Clock(e.TargetFrom) + "–" + PacingReport.Clock(e.TargetTo)) + " | " + PacingReport.Clock(typical.EraMinute[e.Number - 1]) + " |");
            b.AppendLine().AppendLine("The Grand Exhibition (GR.12) needs the final spaces, a full day on the line, " + GrowthContent.ExhibitionSpecies + " of 36 species across all six families and " + GrowthContent.ExhibitionCrowns +
                " crowns; target " + PacingReport.Clock(GrowthContent.ExhibitionFrom) + "–" + PacingReport.Clock(GrowthContent.ExhibitionTo) + ", typical player " + PacingReport.Clock(typical.ExhibitionMinute) + ".");

            b.AppendLine().AppendLine("## Stars, visitors and research points (GR.10, GR.7.2)").AppendLine()
             .AppendLine("Reputation for 1–5 stars: " + string.Join(" / ", GrowthContent.StarThresholds) + " (of " + GrowthContent.ReputationMax + "). A sale gives " + GrowthContent.RepPerSale + ", a granted wish " + GrowthContent.RepPerWish +
                 " more, a refusal " + GrowthContent.RepPerRefusal + ". A day lasts " + (int)(GrowthContent.DaySeconds / 60) + ":" + ((int)(GrowthContent.DaySeconds % 60)).ToString("00", Inv) + "; visits last about " + GrowthContent.VisitSeconds + " s.").AppendLine()
             .AppendLine("Research points: a new founder " + GrowthContent.RpFounder + ", hybrid " + GrowthContent.RpHybrid + ", crown " + GrowthContent.RpCrown + ", colour " + GrowthContent.RpColour + ". Sales never give RP.").AppendLine()
             .AppendLine("| Visitor | From | Weight by stars (1-5) | Budget by stars (1-5) |").AppendLine("| --- | --- | --- | --- |");
            foreach (var v in GrowthContent.Visitors)
                b.AppendLine("| " + v.Name + " | " + v.Stars + "★ | " + string.Join(" / ", v.Weights) + " | " + string.Join(" / ", v.Budgets.Select(x => x == 0 ? "—" : x.ToString(Inv))) + " |");
            b.AppendLine().AppendLine("Seconds between arrivals by stars: " + string.Join(" / ", GrowthContent.ArrivalSeconds.Select(x => x.ToString("0.#", Inv))) + ".");
            return b.ToString();
        }

        static string Name(string id) => GrowthContent.Get(id).Name;

        static string Source(CultureSource s)
        {
            switch (s)
            {
                case CultureSource.StarterGrower: return "starter grower";
                case CultureSource.CatalogGrower: return "grower from the supplier catalog";
                case CultureSource.Research: return "research";
                default: return "conditioning vat";
            }
        }

        // ---------------------------------------------------------------- pacing

        public static string Pacing(IList<(string name, string description, PacingReport report)> runs)
        {
            var b = new StringBuilder();
            var typical = runs[0].report;
            b.AppendLine("# GR-01 pacing report").AppendLine()
             .AppendLine("Generated by `PacingSimulator` (`Core/Growth/PacingSimulator.cs`), a deterministic, engine-free model of a first-time player: each night they buy what they can afford (saving for the next gate purchase), try a few new mixes and pairs, chase a colour and fill the shop's free spaces by hand; each 5:30 day, visitors each buy the most valuable creature in stock within their budget. It estimates pacing to tune GrowthContent's tables; it is not the game's rules engine, and playtests replace its assumptions (GR-15). The curve is drawn in `pacing-curve.svg`.").AppendLine()
             .AppendLine("## Three players").AppendLine()
             .AppendLine("| Player | Assumptions | Era 2 | Era 3 | Era 4 | Era 5 | First helper | Powered | Exhibition | Collect-all | Targets met |").AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var (name, description, r) in runs)
                b.AppendLine("| " + name + " | " + description + " | " + string.Join(" | ", r.EraMinute.Skip(1).Select(PacingReport.Clock)) + " | " + PacingReport.Clock(r.FirstHelperMinute) + " | " +
                    PacingReport.Clock(r.PoweredMinute) + " | " + PacingReport.Clock(r.ExhibitionMinute) + " | " + PacingReport.Clock(r.CollectAllMinute) + " | " + r.Checks().Count(c => c.met) + " / " + r.Checks().Count + " |");
            b.AppendLine().AppendLine("The tables are tuned for the typical player; the careful and quick players show the spread around it.").AppendLine()
             .AppendLine("## The typical player").AppendLine().Append(typical.ToMarkdown());
            return b.ToString();
        }

        // ---------------------------------------------------------------- the curve

        /// <summary>The typical player's curve: species discovered and stars over time with the eras and their targets, and revenue per day.</summary>
        public static string CurveSvg(PacingReport r)
        {
            const double W = 960, left = 56, right = 56, top = 40, h1 = 250, gap = 70, h2 = 150;
            double end = Math.Max(270, Math.Ceiling((Math.Max(r.ExhibitionMinute, 0) + 30) / 30) * 30);
            double plotW = W - left - right, y1 = top + h1, top2 = y1 + gap, y2 = top2 + h2, H = y2 + 40;
            double X(double minute) => left + Math.Min(minute, end) / end * plotW;
            double YS(double species) => y1 - species / GrowthContent.SpeciesCount * (h1 - 20);
            double YStar(double stars) => y1 - (stars - 1) / 4 * (h1 - 20);
            string F(double v) => v.ToString("0.#", Inv);
            var s = new StringBuilder();
            s.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(F(W)).Append("\" height=\"").Append(F(H)).Append("\" viewBox=\"0 0 ").Append(F(W)).Append(' ').Append(F(H))
             .AppendLine("\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\" font-size=\"12\">")
             .AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>")
             .Append("<text x=\"").Append(F(left)).Append("\" y=\"22\" font-size=\"15\" font-weight=\"600\" fill=\"#1a1a1a\">GR-01 pacing: species discovered and stars (typical player)</text>\n");
            // Era bands, each from when it was reached to the next.
            string[] band = { "#f4f6f9", "#e9eef5" };
            for (int e = 0; e < 5; e++)
            {
                double from = r.EraMinute[e], to = e < 4 && r.EraMinute[e + 1] >= 0 ? r.EraMinute[e + 1] : end;
                if (from < 0) continue;
                s.Append("<rect x=\"").Append(F(X(from))).Append("\" y=\"").Append(F(top)).Append("\" width=\"").Append(F(X(to) - X(from))).Append("\" height=\"").Append(F(h1)).Append("\" fill=\"").Append(band[e % 2]).AppendLine("\"/>");
                s.Append("<text x=\"").Append(F(X(from) + 4)).Append("\" y=\"").Append(F(top + 14)).Append("\" fill=\"#555\">").Append(e + 1).Append(' ').Append(GrowthContent.Eras[e].Name).AppendLine("</text>");
                if (e > 0)
                {
                    var era = GrowthContent.Eras[e];
                    s.Append("<rect x=\"").Append(F(X(era.TargetFrom))).Append("\" y=\"").Append(F(y1 + 4)).Append("\" width=\"").Append(F(X(era.TargetTo) - X(era.TargetFrom))).Append("\" height=\"6\" fill=\"#7cc48a\" opacity=\"0.8\"/>\n");
                }
            }
            s.Append("<rect x=\"").Append(F(X(GrowthContent.ExhibitionFrom))).Append("\" y=\"").Append(F(y1 + 12)).Append("\" width=\"").Append(F(X(GrowthContent.ExhibitionTo) - X(GrowthContent.ExhibitionFrom))).Append("\" height=\"6\" fill=\"#c99a2e\" opacity=\"0.8\"/>\n");
            // Axes and grid.
            for (int sp = 0; sp <= GrowthContent.SpeciesCount; sp += 6)
                s.Append("<line x1=\"").Append(F(left)).Append("\" x2=\"").Append(F(left + plotW)).Append("\" y1=\"").Append(F(YS(sp))).Append("\" y2=\"").Append(F(YS(sp))).Append("\" stroke=\"#d5dbe3\"/>")
                 .Append("<text x=\"").Append(F(left - 8)).Append("\" y=\"").Append(F(YS(sp) + 4)).Append("\" text-anchor=\"end\" fill=\"#1f5fa8\">").Append(sp).AppendLine("</text>");
            for (int st = 1; st <= 5; st++)
                s.Append("<text x=\"").Append(F(left + plotW + 8)).Append("\" y=\"").Append(F(YStar(st) + 4)).Append("\" fill=\"#c2561a\">").Append(st).AppendLine("★</text>");
            for (double t = 0; t <= end; t += 30)
                s.Append("<line x1=\"").Append(F(X(t))).Append("\" x2=\"").Append(F(X(t))).Append("\" y1=\"").Append(F(y1)).Append("\" y2=\"").Append(F(y1 + 20)).Append("\" stroke=\"#888\"/>")
                 .Append("<text x=\"").Append(F(X(t))).Append("\" y=\"").Append(F(y1 + 32)).Append("\" text-anchor=\"middle\" fill=\"#333\">").Append(PacingReport.Clock(t)).AppendLine("</text>");
            s.Append("<text x=\"").Append(F(left - 40)).Append("\" y=\"").Append(F(top - 6)).Append("\" fill=\"#1f5fa8\">species</text>");
            s.Append("<text x=\"").Append(F(left + plotW - 4)).Append("\" y=\"").Append(F(top - 6)).Append("\" fill=\"#c2561a\">stars</text>\n");
            // Species discovered (step line) and stars (step line).
            var found = r.SpeciesMinute.Values.OrderBy(m => m).ToList();
            var path = new StringBuilder("M" + F(X(0)) + "," + F(YS(0)));
            for (int i = 0; i < found.Count; i++) path.Append(" H").Append(F(X(found[i]))).Append(" V").Append(F(YS(i + 1)));
            path.Append(" H").Append(F(X(end)));
            s.Append("<path d=\"").Append(path).AppendLine("\" fill=\"none\" stroke=\"#1f5fa8\" stroke-width=\"2.5\"/>");
            var stars = new StringBuilder("M" + F(X(0)) + "," + F(YStar(1)));
            // Stars as they stand at the end of each day.
            for (int i = 0; i < r.Days.Count; i++)
            {
                double dayEnd = i + 1 < r.Days.Count ? r.Days[i + 1].StartMinute : r.EndMinute;
                stars.Append(" H").Append(F(X(dayEnd))).Append(" V").Append(F(YStar(r.Days[i].Stars)));
            }
            stars.Append(" H").Append(F(X(end)));
            s.Append("<path d=\"").Append(stars).AppendLine("\" fill=\"none\" stroke=\"#c2561a\" stroke-width=\"2\" stroke-dasharray=\"6 3\"/>");
            // Milestones.
            var marks = new List<(double minute, string text)>();
            foreach (var h in GrowthContent.Helpers) if (r.HelperMinute.TryGetValue(h.Id, out double m)) marks.Add((m, h.Name));
            marks.Add((r.PoweredMinute, "powered pumps"));
            marks.Add((r.LineMinute, "line"));
            marks.Add((r.ExhibitionMinute, "Exhibition"));
            int row = 0;
            foreach (var (minute, text) in marks.Where(x => x.minute >= 0).OrderBy(x => x.minute))
            {
                double x = X(minute), y = top + 34 + (row++ % 4) * 16;
                s.Append("<line x1=\"").Append(F(x)).Append("\" x2=\"").Append(F(x)).Append("\" y1=\"").Append(F(y + 3)).Append("\" y2=\"").Append(F(y1)).Append("\" stroke=\"#444\" stroke-dasharray=\"2 3\"/>")
                 .Append("<text x=\"").Append(F(x + 3)).Append("\" y=\"").Append(F(y)).Append("\" fill=\"#222\" stroke=\"#ffffff\" stroke-width=\"3\" paint-order=\"stroke\">").Append(text).Append(' ').Append(PacingReport.Clock(minute)).AppendLine("</text>");
            }
            // Legend for the target bars.
            s.Append("<rect x=\"").Append(F(left)).Append("\" y=\"").Append(F(y1 + 40)).Append("\" width=\"14\" height=\"6\" fill=\"#7cc48a\"/>")
             .Append("<text x=\"").Append(F(left + 20)).Append("\" y=\"").Append(F(y1 + 47)).Append("\" fill=\"#333\">era target windows (GR.4)</text>")
             .Append("<rect x=\"").Append(F(left + 190)).Append("\" y=\"").Append(F(y1 + 40)).Append("\" width=\"14\" height=\"6\" fill=\"#c99a2e\"/>")
             .Append("<text x=\"").Append(F(left + 210)).Append("\" y=\"").Append(F(y1 + 47)).AppendLine("\" fill=\"#333\">exhibition target (GR.3)</text>");
            // Revenue per day, log scale.
            s.Append("<text x=\"").Append(F(left)).Append("\" y=\"").Append(F(top2 - 8)).AppendLine("\" font-size=\"14\" font-weight=\"600\" fill=\"#1a1a1a\">Revenue per day (credits, log scale)</text>");
            double YR(double credits) => y2 - (Math.Log10(Math.Max(10, credits)) - 1) / 3 * h2;
            foreach (int c in new[] { 10, 100, 1000, 10000 })
                s.Append("<line x1=\"").Append(F(left)).Append("\" x2=\"").Append(F(left + plotW)).Append("\" y1=\"").Append(F(YR(c))).Append("\" y2=\"").Append(F(YR(c))).Append("\" stroke=\"#d5dbe3\"/>")
                 .Append("<text x=\"").Append(F(left - 8)).Append("\" y=\"").Append(F(YR(c) + 4)).Append("\" text-anchor=\"end\" fill=\"#333\">").Append(c.ToString("#,0", Inv)).AppendLine("</text>");
            for (int i = 0; i < r.Days.Count; i++)
            {
                var d = r.Days[i];
                double from = d.StartMinute, to = i + 1 < r.Days.Count ? r.Days[i + 1].StartMinute : from + 6;
                if (from >= end) break;
                s.Append("<rect x=\"").Append(F(X(from) + 1)).Append("\" y=\"").Append(F(YR(d.Revenue))).Append("\" width=\"").Append(F(Math.Max(1, X(to) - X(from) - 2))).Append("\" height=\"").Append(F(y2 - YR(d.Revenue)))
                 .Append("\" fill=\"#5b8fd0\"><title>Day ").Append(d.Day).Append(": ").Append(d.Revenue).AppendLine(" credits</title></rect>");
            }
            for (double t = 0; t <= end; t += 30)
                s.Append("<text x=\"").Append(F(X(t))).Append("\" y=\"").Append(F(y2 + 16)).Append("\" text-anchor=\"middle\" fill=\"#333\">").Append(PacingReport.Clock(t)).AppendLine("</text>");
            s.AppendLine("</svg>");
            return s.ToString();
        }
    }
}
