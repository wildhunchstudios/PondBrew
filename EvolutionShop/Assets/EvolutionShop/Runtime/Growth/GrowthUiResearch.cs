using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-30 (tester round 1, F1 to F4): the research room's cards. The wall's card opens it. The desk has a grid of every pair in the
    /// jars: what each made, ? for one not tried (a press queues it), a dot for one queued; with the microscope a star marks a pair
    /// that would make something new, and with the planner the cell shows what it would make. A second tab queues mixes the same
    /// way, a third keeps the jars, and the room's upgrades are on the last.
    /// </summary>
    public sealed partial class GrowthUi
    {
        const int ResearchCell = 44;
        /// <summary>The mix being put together on the desk's Mixes tab: one count per culture.</summary>
        int[] researchMix;

        void FillResearchWall()
        {
            var shop = s.Shop;
            var room = GrowthShop.ResearchDef;
            var body = Head("Research room", shop.ResearchRoom ? "open" : room.Credits + " credits", null, "Through the power room's west wall, once the generator stands. Era 3 and 3 stars. Walls open at night.");
            if (shop.ResearchRoom) { body.Add(L("Open: three research tanks, the specimen jars, the tray and the desk.", "gr-hint")); Footer(body); return; }
            var failure = shop.ResearchCheck(out string why);
            BuyPanel(body, "The power room's west wall", "The research room", "Three small tanks that try pairs and mixes by themselves on the generator's power, jars for one creature of each kind, and a desk to plan it all.",
                Gates(room.Era, room.Stars, room.Credits, 0, true), failure, why, "Open the research room for " + room.Credits + " cr", shop.BuyResearchRoom);
            Footer(body);
        }

        string ResearchLiveText()
        {
            var shop = s.Shop;
            int working = shop.ResearchRunning.Count(j => j.Left > 0), waiting = shop.ResearchWaiting.Count();
            string line = working + " of " + GrowthShop.ResearchTankCount + " tanks at work · " + waiting + " waiting";
            if (working > 0) line += " · next done in " + Mathf.Max(1, Mathf.CeilToInt(shop.ResearchRunning.Where(j => j.Left > 0).Min(j => j.Left))) + " s";
            var use = shop.PowerUse(GrowthShop.ResearchPower);
            if (use != null && use.Draw > 0 && use.Got < use.Draw - 0.001f) line += " · short of power";
            return line;
        }

        string ResearchSignature()
        {
            var shop = s.Shop;
            return shop.ResearchRoom + "|" + string.Join(",", shop.Specimens.Select(x => x.Id)) + "|" + string.Join(",", shop.ResearchTray.Select(x => x.Id)) + "|" + shop.PairLog.Count + "|" + shop.MixLog.Count +
                "|" + string.Join(",", shop.ResearchJobs.Select(j => (j.IsMix ? string.Join("", j.Doses) : j.A + j.B) + j.Tank)) + "|" + (researchMix == null ? "" : string.Join("", researchMix));
        }

        void FillResearch()
        {
            var shop = s.Shop;
            var body = Notebook("Research desk", "", "Pairs");
            LiveStatus();
            if (shop.ResearchTray.Count > 0) body.Add(TrayCallout());
            FillResearchPairs(body);
            var mixes = AddTab("Mixes");
            if (shop.ResearchTray.Count > 0) mixes.Add(TrayCallout());
            FillResearchMixes(mixes);
            FillResearchJars(AddTab("Jars"));
            UpgradeSection(body, "research");
            NotebookFooter("Close");
        }

        VisualElement TrayCallout()
        {
            var shop = s.Shop;
            var first = shop.ResearchTray[0];
            var call = Box("gr-callout");
            var row = Box("gr-row");
            row.Add(CreatureIcon(first.Def, first.Colour, 44, "gr-result__glyph"));
            row.Add(L(shop.ResearchTray.Count > 1 ? shop.ResearchTray.Count + " new creatures on the tray" : "A new creature on the tray: " + first.Def.Name, "gr-strong"));
            call.Add(row);
            call.Add(Wide("Take the " + first.Def.Name + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(() => shop.TakeResearchResult()), true,
                shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full: put a creature in a tank first"));
            return call;
        }

        // ---------------------------------------------------------------- pairs

        void FillResearchPairs(VisualElement body)
        {
            var shop = s.Shop;
            if (shop.Specimens.Count < 2)
            {
                body.Add(L(shop.Specimens.Count == 0 ? "The jars are empty. Bring a creature here and click a jar: it stays there, not for sale and not used up." : "One more kind in the jars makes a first pair: bring it here and click a jar.", "gr-hint"));
                return;
            }
            body.Add(L("Press ? on a pair to queue it: each takes " + shop.ResearchSeconds.ToString("0") + " s and a unit of power. Something new leaves one creature on the tray. A repeat only tells you what it makes.", "gr-hint"));
            int left = shop.PairsLeft().Count;
            var all = Wide("Queue every pair left (" + left + ")", () => s.Act(shop.QueueAllPairs), false, left > 0, "Every pair in the jars is tried or queued");
            all.style.alignSelf = Align.FlexStart;
            body.Add(all);
            var grid = Box("gr-rs-grid");
            var head = Box("gr-rs-row");
            head.Add(Box("gr-rs-corner"));
            foreach (var x in shop.Specimens) head.Add(Axis(x));
            grid.Add(head);
            foreach (var a in shop.Specimens)
            {
                var row = Box("gr-rs-row");
                row.Add(Axis(a));
                foreach (var b in shop.Specimens) row.Add(PairCell(a.Species, b.Species));
                grid.Add(row);
            }
            body.Add(grid);
        }

        VisualElement Axis(GrowthSpecimen x)
        {
            var box = Box("gr-rs-axis");
            box.tooltip = x.Def.Name;
            box.Add(CreatureIcon(x.Def, x.Colour, ResearchCell - 8, "gr-rs-icon"));
            return box;
        }

        VisualElement PairCell(string a, string b)
        {
            var shop = s.Shop;
            string names = GrowthContent.Get(a).Name + " × " + GrowthContent.Get(b).Name;
            if (a == b)
            {
                var same = Box("gr-rs-cell", "gr-rs-cell--same");
                same.tooltip = "Two of one kind only make more of it";
                return same;
            }
            var tried = shop.PairEntry(a, b);
            if (tried != null)
            {
                var made = GrowthContent.Get(tried.Species);
                var cell = Box("gr-rs-cell", "gr-rs-cell--tried");
                cell.tooltip = names + " makes " + made.Name + (tried.WasNew ? " (your first)" : "");
                cell.Add(CreatureIcon(made, ColourFound(shop, made), ResearchCell - 8, "gr-rs-icon"));
                return cell;
            }
            var job = shop.QueuedPair(a, b);
            if (job != null)
            {
                var queued = new Button(() => { if (!job.Running) s.Act(() => shop.Unqueue(job)); }) { text = job.Running ? "…" : "·" };
                queued.AddToClassList("gr-rs-cell");
                queued.AddToClassList(job.Running ? "gr-rs-cell--running" : "gr-rs-cell--queued");
                queued.tooltip = names + (job.Running ? ": in a tank now" : ": in the queue. Press to take it out");
                return queued;
            }
            bool looksNew = shop.PairLooksNew(a, b);
            var predicted = shop.PlannerPredicts(a, b);
            var open = new Button(() => s.Act(() => shop.QueuePair(a, b))) { text = predicted != null ? "" : looksNew ? "★" : "?" };
            open.AddToClassList("gr-rs-cell");
            open.AddToClassList("gr-rs-cell--open");
            if (looksNew) open.AddToClassList("gr-rs-cell--new");
            if (predicted != null)
            {
                var icon = CreatureIcon(predicted, Colour.Classic, ResearchCell - 12, "gr-rs-icon");
                icon.style.opacity = 0.55f;
                open.Add(icon);
            }
            open.tooltip = names + (predicted != null ? ": the planner says " + predicted.Name : "") + (looksNew ? ": the microscope says this would be something new" : "") + ". Press to queue it";
            return open;
        }

        // ---------------------------------------------------------------- mixes

        void FillResearchMixes(VisualElement body)
        {
            var shop = s.Shop;
            if (researchMix == null || researchMix.Length != GrowthContent.Cultures.Length) researchMix = new int[GrowthContent.Cultures.Length];
            int total = researchMix.Sum();
            body.Add(L("Put a mix together and queue it: it runs by itself, and takes its doses from your buffers and growers as it starts, never your siphon.", "gr-hint"));
            var section = Box("gr-section");
            section.Add(Eyebrow("This mix · " + total + " of " + shop.MaxDoses));
            var doses = Box("gr-row");
            foreach (var culture in shop.Cultures)
            {
                string c = culture;
                int i = GrowthContent.CultureIndex(c);
                var chip = Box("gr-dose"); chip.style.backgroundColor = CultureGround(c);
                chip.Add(PlusMinus("−", () => { researchMix[i]--; RefreshCard(); }, researchMix[i] > 0, "None of it in this mix"));
                var words = Box("gr-dose__text");
                words.Add(L(GrowthShop.Name(c) + " " + researchMix[i], "gr-dose__name"));
                words.Add(L(shop.DosesForResearch(c) + " ready", "gr-dose__count"));
                chip.Add(words);
                chip.Add(PlusMinus("+", () => { researchMix[i]++; RefreshCard(); }, total < shop.MaxDoses, "A mix takes up to " + shop.MaxDoses + " doses"));
                doses.Add(chip);
            }
            section.Add(doses);
            body.Add(section);
            var failure = shop.MixCheck(researchMix, out string why);
            if (total > 0)
            {
                var tried = shop.MixEntry(researchMix);
                if (tried != null) body.Add(L("Already tried: this makes " + GrowthContent.Get(tried.Species).Name + ".", "gr-strong"));
                else
                {
                    int d = GrowthRules.Mix(researchMix, shop.Cultures, shop.Discovered).DistanceToNew;
                    if (shop.Microscope) body.Add(L("Microscope: " + (d < 0 ? "nothing new left to find with these cultures" : d == 0 ? "this mix is something new" : d + " dose" + (d == 1 ? "" : "s") + " from something new"), "gr-hint"));
                    else if (d == 1) body.Add(L("Close to something new", "gr-hint"));
                }
            }
            body.Add(Wide("Queue this mix", () => { var mix = (int[])researchMix.Clone(); if (s.Act(() => shop.QueueMix(mix)).Success) { researchMix = new int[mix.Length]; RefreshCard(); } }, true, failure == GrowthFailure.None, why));
            var queue = shop.ResearchJobs.Where(j => j.IsMix).ToList();
            if (queue.Count > 0)
            {
                var waiting = Box("gr-section");
                waiting.Add(Eyebrow("In the queue"));
                foreach (var job in queue)
                {
                    var row = Box("gr-row", "gr-row--spread");
                    row.Add(L(DoseWords(job.Doses) + (job.Running ? " · in a tank" : shop.MixHasDoses(job.Doses) ? "" : " · waiting for doses"), "gr-hint"));
                    if (!job.Running) { var j = job; row.Add(ChipButton("Take out", () => s.Act(() => shop.Unqueue(j)), true)); }
                    waiting.Add(row);
                }
                body.Add(waiting);
            }
            var log = shop.MixLog.AsEnumerable().Reverse().Take(8).ToList();
            if (log.Count > 0)
            {
                var list = Box("gr-section");
                list.Add(Eyebrow("Mixes tried · " + shop.MixLog.Count));
                foreach (var m in log) list.Add(L(DoseWords(m.Doses) + " makes " + GrowthContent.Get(m.Species).Name + (m.Exact ? "" : " (not exact)"), "gr-hint"));
                body.Add(list);
            }
        }

        // ---------------------------------------------------------------- jars

        void FillResearchJars(VisualElement body)
        {
            var shop = s.Shop;
            body.Add(L(shop.Specimens.Count + " of " + GrowthShop.SpecimenJars + " jars. One creature of each kind: it stays here, not for sale and not used up.", "gr-hint"));
            foreach (var carried in shop.Carried)
            {
                var x = carried;
                var failure = shop.SpecimenCheck(x, out string why);
                var row = Box("gr-row", "gr-row--spread");
                row.Add(CreatureIcon(x.Def, x.Colour, 36, "gr-rs-icon"));
                row.Add(L(x.Def.Name + " in your hands", "gr-hint"));
                row.Add(ChipButton("Put in a jar", () => s.Act(() => shop.PutSpecimen(x.Id)), failure == GrowthFailure.None, why));
                body.Add(row);
            }
            var jars = Box("gr-row");
            jars.style.marginTop = 8;
            foreach (var specimen in shop.Specimens)
            {
                var x = specimen;
                int pairs = shop.ResearchWaiting.Count(j => !j.IsMix && (j.A == x.Species || j.B == x.Species));
                var tile = Box("gr-rs-jar");
                tile.Add(CreatureIcon(x.Def, x.Colour, 48, "gr-rs-icon"));
                tile.Add(L(x.Def.Name, "gr-tile__name"));
                var take = ChipButton("Take back", () => s.Act(() => shop.TakeSpecimen(x.Id)), shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full");
                if (pairs > 0) take.tooltip = "Its " + pairs + (pairs == 1 ? " pair" : " pairs") + " in the queue will be taken out";
                tile.Add(take);
                jars.Add(tile);
            }
            body.Add(jars);
        }
    }
}
