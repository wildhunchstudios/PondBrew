using System.Collections.Generic;
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
        /// <summary>GR-33 (F59): the desk's tabs a device opens on (the card's target).</summary>
        public const string ResearchTanksTab = "Tanks", ResearchJarsTab = "Jars";
        /// <summary>The mix being put together on the desk's Mixes tab: one count per culture.</summary>
        int[] researchMix;

        void FillResearchWall()
        {
            var shop = s.Shop;
            var room = GrowthShop.ResearchDef;
            var body = Head("Research room", shop.ResearchRoom ? "open" : room.Credits + " credits", null, "Through the power room's west wall, once the generator stands. Era 3 and 3 stars.");
            if (shop.ResearchRoom) { body.Add(L("Open: three research tanks, the specimen jars, the tray and the desk.", "gr-hint")); Footer(body); return; }
            var failure = shop.ResearchCheck(out string why);
            BuyPanel(body, "The power room's west wall", "The research room", "Three small tanks that try pairs and mixes by themselves on the generator's power, jars for one creature of each kind, and a desk to plan it all.",
                Gates(room.Era, room.Stars, room.Credits, 0, false), failure, why, "Open the research room for " + room.Credits + " cr", shop.BuyResearchRoom);
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

        void FillResearch(string tab)
        {
            var shop = s.Shop;
            // GR-33 (F59): opened from a tank, the tray or a jar, the desk opens on that device's tab (until another is chosen).
            if (tabChosen == null && tab != null) tabChosen = tab;
            var body = Notebook("Research desk", "", "Pairs");
            LiveStatus();
            if (shop.ResearchTray.Count > 0) body.Add(TrayCallout());
            FillResearchPairs(body);
            var mixes = AddTab("Mixes");
            if (shop.ResearchTray.Count > 0) mixes.Add(TrayCallout());
            FillResearchMixes(mixes);
            FillResearchTanks(AddTab(ResearchTanksTab));
            FillResearchJars(AddTab(ResearchJarsTab));
            FillFamilyTree(AddTab(FamilyTreeTab));
            UpgradeSection(body, "research");
            NotebookFooter("Close");
        }

        // ---------------------------------------------------------------- tanks (F60)

        /// <summary>GR-33 (F60): each tank's job and how far along it is, live; what waits in the queue; and the tray.</summary>
        void FillResearchTanks(VisualElement body)
        {
            var shop = s.Shop;
            body.Add(L("Each tank works through the queue by itself, by day and night, on a unit of the generator's power: " + shop.ResearchSeconds.ToString("0") + " s a job.", "gr-hint"));
            var tanks = Box("gr-rs-tanks");
            for (int t = 0; t < GrowthShop.ResearchTankCount; t++)
            {
                int tank = t;
                var job = shop.ResearchInTank(tank);
                var panel = Box("gr-rs-tank");
                panel.Add(L("Tank " + (tank + 1), "gr-strong"));
                if (job == null)
                {
                    panel.Add(L(shop.ResearchWaiting.Any() ? "Starting the next job" : "Empty: queue a pair or a mix", "gr-hint"));
                    tanks.Add(panel);
                    continue;
                }
                panel.Add(JobLine(job));
                panel.Add(LiveBar(() => { var j = shop.ResearchInTank(tank); return j == null ? 1 : 1 - j.Left / Mathf.Max(1, shop.ResearchSeconds); }, new Color(0.62f, 0.52f, 0.86f)));
                panel.Add(LiveL(() =>
                {
                    var j = shop.ResearchInTank(tank);
                    if (j == null) return "Done";
                    if (j.Left <= 0) return "Something new: waiting for room on the tray";
                    var use = shop.PowerUse(GrowthShop.ResearchPower);
                    return Mathf.CeilToInt(j.Left) + " s left" + (use != null && use.Draw > 0 && use.Got < use.Draw - 0.001f ? " · short of power" : "");
                }, "gr-hint"));
                tanks.Add(panel);
            }
            body.Add(tanks);
            int waiting = shop.ResearchWaiting.Count();
            body.Add(L(waiting == 0 ? "Nothing waits in the queue." : waiting + (waiting == 1 ? " job waits" : " jobs wait") + " in the queue: pairs on the Pairs tab, mixes on the Mixes tab.", "gr-hint"));
            if (shop.ResearchTray.Count > 0)
            {
                body.Add(Eyebrow("On the tray · " + shop.ResearchTray.Count + " of " + GrowthShop.ResearchTrayPlaces));
                body.Add(TrayCallout());
            }
            // GR-33 (F72): what each job made, newest first: a repeat leaves no creature, so it shows here.
            if (shop.ResearchResults.Count > 0)
            {
                body.Add(Eyebrow("Results · the last " + shop.ResearchResults.Count));
                foreach (var r in shop.ResearchResults.AsEnumerable().Reverse().Take(8))
                {
                    var made = GrowthContent.Get(r.Made);
                    var row = Box("gr-logrow");
                    if (r.IsMix) row.Add(DoseDots(r.Doses));
                    else
                    {
                        SpeciesDef a = GrowthContent.Get(r.A), b = GrowthContent.Get(r.B);
                        row.Add(CreatureIcon(a, ColourFound(shop, a), 26, "gr-rs-icon"));
                        row.Add(L("×", "gr-logrow__text"));
                        row.Add(CreatureIcon(b, ColourFound(shop, b), 26, "gr-rs-icon"));
                    }
                    row.Add(L("→", "gr-logrow__text"));
                    row.Add(CreatureIcon(made, ColourFound(shop, made), 26, "gr-rs-icon"));
                    row.Add(L(made.Name + (r.WasNew ? " · new: on the tray" : " · a repeat"), "gr-logrow__text"));
                    body.Add(row);
                }
            }
        }

        /// <summary>A job in words and pictures: the pair's two creatures, or the mix's doses as coloured dots.</summary>
        VisualElement JobLine(GrowthResearchJob job)
        {
            var row = Box("gr-row");
            row.style.flexWrap = Wrap.NoWrap;
            if (job.IsMix)
            {
                row.Add(Dots(job.Doses));
                row.Add(L("a mix", "gr-hint"));
                return row;
            }
            var a = GrowthContent.Get(job.A); var b = GrowthContent.Get(job.B);
            row.Add(CreatureIcon(a, ColourFound(s.Shop, a), 30, "gr-rs-icon"));
            row.Add(L("×", "gr-hint"));
            row.Add(CreatureIcon(b, ColourFound(s.Shop, b), 30, "gr-rs-icon"));
            // The names under the pictures, where they have the panel's width.
            var both = Box("gr-section");
            both.Add(row);
            both.Add(L(a.Name + " × " + b.Name, "gr-hint"));
            return both;
        }

        /// <summary>A mix's doses as coloured dots, one a dose, in the cultures' order.</summary>
        VisualElement Dots(int[] doses)
        {
            var dots = Row();
            for (int i = 0; i < doses.Length; i++)
                for (int n = 0; n < doses[i]; n++)
                {
                    var dot = Dot(CultureColour(GrowthContent.Cultures[i].Id), 14);
                    dot.tooltip = GrowthShop.Name(GrowthContent.Cultures[i].Id);
                    dots.Add(dot);
                }
            return dots;
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
                    var what = Box("gr-row");
                    what.Add(DoseDots(job.Doses));
                    what.Add(L(DoseWords(job.Doses) + (job.Running ? " · in a tank" : shop.MixHasDoses(job.Doses) ? "" : " · waiting for doses"), "gr-hint"));
                    row.Add(what);
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
                var found = shop.FoundRecipes();
                foreach (var m in log)
                {
                    var line = Box("gr-row", "gr-row--spread");
                    var what = Box("gr-row");
                    what.Add(DoseDots(m.Doses));
                    what.Add(L(DoseWords(m.Doses) + " makes " + GrowthContent.Get(m.Species).Name + (m.Exact ? "" : " (not exact)"), "gr-hint"));
                    line.Add(what);
                    // GR-35 (F73): a recipe found is made again in the incubator's queue.
                    if (shop.HasMakeQueue && m.Exact && found.Contains(m.Species)) line.Add(MakeButton(m.Species));
                    list.Add(line);
                }
                body.Add(list);
            }
        }

        // ---------------------------------------------------------------- jars

        void FillResearchJars(VisualElement body)
        {
            var shop = s.Shop;
            // GR-35 (F70): what the jars are for, in four steps.
            var steps = Box("gr-rs-steps");
            foreach (var (n, words) in new[] { ("1", "Put a creature in a jar: one of each kind"), ("2", "Pick two on the Pairs tab"), ("3", "A tank breeds them by itself"), ("4", "Anything new waits on the tray") })
            {
                var step = Box("gr-rs-step");
                step.Add(L(n, "gr-rs-step__n"));
                step.Add(L(words, "gr-rs-step__text"));
                steps.Add(step);
            }
            body.Add(steps);
            // GR-33 (F63): what you carry, as tiles like the jars', under its own heading (a row of its own read as coming from nowhere).
            body.Add(Eyebrow("In your hands"));
            var hands = Box("gr-row");
            if (shop.Carried.Count == 0) hands.Add(L("Nothing. Bring a creature here, then press Put in a jar, or click a jar with it.", "gr-hint"));
            foreach (var carried in shop.Carried)
            {
                var x = carried;
                var failure = shop.SpecimenCheck(x, out string why);
                var tile = Box("gr-rs-jar", "gr-rs-jar--hand");
                tile.Add(CreatureIcon(x.Def, x.Colour, 48, "gr-rs-icon"));
                tile.Add(L(x.Def.Name, "gr-tile__name"));
                tile.Add(ChipButton("Put in a jar", () => s.Act(() => shop.PutSpecimen(x.Id)), failure == GrowthFailure.None, why));
                hands.Add(tile);
            }
            body.Add(hands);
            body.Add(Eyebrow("In the jars · " + shop.Specimens.Count + " of " + GrowthShop.SpecimenJars));
            var jars = Box("gr-row");
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
            // GR-35 (F70): the empty jars say how to fill one (a few, then how many more).
            int empty = GrowthShop.SpecimenJars - shop.Specimens.Count;
            for (int i = 0; i < Mathf.Min(empty, EmptyJarsShown); i++)
            {
                var tile = Box("gr-rs-jar", "gr-rs-jar--empty");
                tile.Add(L("Empty", "gr-tile__name"));
                tile.Add(L(shop.Carried.Count > 0 ? "Press Put in a jar above" : "Click a jar with a creature in hand", "gr-rs-jar__how"));
                jars.Add(tile);
            }
            if (empty > EmptyJarsShown) jars.Add(L("and " + (empty - EmptyJarsShown) + " more empty", "gr-hint"));
            body.Add(jars);
            body.Add(L("A creature in a jar is kept for pairing: it never sells, and you can take it back any time.", "gr-hint"));
        }

        const int EmptyJarsShown = 3;
        public const string FamilyTreeTab = "Family tree";
        string treeFamily;

        // ---------------------------------------------------------------- the family tree (F74)

        /// <summary>
        /// GR-35 (F74, GR.77): one family at a time: its founders at the top (their recipe's dots once found), its hybrids
        /// below, its crown at the bottom; a line from each creature found to the pair that made it (a parent from another
        /// family is named under it); a "?" with no line for what is not found, so the tree never gives a pair away; and the
        /// pairs left to try with the family's creatures found. Every pair tried counts, at the breeding tank or in this room.
        /// </summary>
        void FillFamilyTree(VisualElement body)
        {
            var shop = s.Shop;
            var families = GrowthContent.Families;
            if (treeFamily == null || families.All(f => f.Id != treeFamily))
                treeFamily = (families.LastOrDefault(f => GrowthContent.Species.Any(x => x.Family == f.Id && shop.Discovered.Contains(x.Id))) ?? families[0]).Id;
            var chips = Box("gr-row");
            chips.style.flexWrap = Wrap.Wrap;
            foreach (var family in families)
            {
                var f = family;
                var members = GrowthContent.Species.Where(x => x.Family == f.Id).ToList();
                var chip = ChipButton(f.Name + " " + members.Count(x => shop.Discovered.Contains(x.Id)) + "/" + members.Count, () => { treeFamily = f.Id; RefreshCard(); }, true);
                chip.name = "gr-tree-family-" + f.Id;
                chip.AddToClassList("gr-tree__chip");
                if (f.Id == treeFamily) chip.AddToClassList("gr-chip--on");
                chips.Add(chip);
            }
            body.Add(chips);
            var all = GrowthContent.Species.Where(x => x.Family == treeFamily).ToList();
            var rows = new[] { all.Where(x => x.Kind == SpeciesKind.Founder).ToList(), all.Where(x => x.Kind == SpeciesKind.Hybrid).ToList(), all.Where(x => x.Kind == SpeciesKind.Crown).ToList() };
            var where = new Dictionary<string, Vector2>();
            // The tree takes the left part; the pairs left to try stand in the top right corner, clear of it.
            float[] rowY = { 0.13f, 0.5f, 0.86f };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Count; i++) where[rows[r][i].Id] = new Vector2(0.03f + 0.64f * (i + 0.5f) / rows[r].Count, rowY[r]);
            // The pair that made each creature found, as found (the first logged).
            var madeBy = new Dictionary<string, GrowthPairEntry>();
            foreach (var p in shop.PairLog) if (!madeBy.ContainsKey(p.Species) && shop.Discovered.Contains(p.Species)) madeBy[p.Species] = p;
            var tree = Box("gr-tree");
            var lines = new VisualElement { name = "gr-tree-lines", pickingMode = PickingMode.Ignore };
            lines.AddToClassList("gr-tree__lines");
            var edges = new List<(Vector2 a, Vector2 b)>();
            foreach (var sp in all)
                if (madeBy.TryGetValue(sp.Id, out var p))
                    foreach (var parent in new[] { p.A, p.B }.Distinct())
                        if (where.TryGetValue(parent, out var from)) edges.Add((from, where[sp.Id]));
            lines.generateVisualContent += mgc =>
            {
                var r = lines.contentRect;
                var painter = mgc.painter2D;
                painter.strokeColor = new Color(0.13f, 0.75f, 0.69f);
                painter.lineWidth = 5;
                painter.lineCap = LineCap.Round;
                foreach (var (a, b) in edges)
                {
                    Vector2 pa = new Vector2(a.x * r.width, a.y * r.height + 48), pb = new Vector2(b.x * r.width, b.y * r.height - 36);
                    float mid = (pa.y + pb.y) * 0.5f;
                    painter.BeginPath();
                    painter.MoveTo(pa);
                    painter.BezierCurveTo(new Vector2(pa.x, mid), new Vector2(pb.x, mid), pb);
                    painter.Stroke();
                }
            };
            lines.RegisterCallback<GeometryChangedEvent>(_ => lines.MarkDirtyRepaint());
            tree.Add(lines);
            foreach (var sp in all)
            {
                var at = where[sp.Id];
                bool found = shop.Discovered.Contains(sp.Id);
                var node = Box("gr-tree__node");
                node.name = "gr-tree-node-" + sp.Id;
                node.style.left = Length.Percent(at.x * 100); node.style.top = Length.Percent(at.y * 100);
                if (sp.Kind == SpeciesKind.Crown) node.Add(L("Crown", "gr-tree__crown"));
                if (found) node.Add(CreatureIcon(sp, ColourFound(shop, sp), 54, "gr-rs-icon"));
                else node.Add(L("?", "gr-tree__unknown"));
                node.Add(L(found ? sp.Name : "Not found yet", found ? "gr-tree__name" : "gr-tree__name gr-tree__name--unknown"));
                if (found && sp.Recipe != null) node.Add(DoseDots(sp.Recipe));
                if (found && madeBy.TryGetValue(sp.Id, out var pair))
                {
                    var outside = new[] { pair.A, pair.B }.Distinct().Where(x => !where.ContainsKey(x)).Select(x => GrowthContent.Get(x).Name).ToList();
                    if (outside.Count > 0) node.Add(L("with " + string.Join(" and ", outside), "gr-tree__with"));
                }
                RecipeTipFor(node, sp, found);
                tree.Add(node);
            }
            // What is left to try with the family's creatures found.
            var have = all.Where(x => shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            int possible = have.Count * (have.Count - 1) / 2;
            int tried = shop.PairLog.Where(p => p.A != p.B && have.Contains(p.A) && have.Contains(p.B)).Select(p => string.CompareOrdinal(p.A, p.B) < 0 ? p.A + "|" + p.B : p.B + "|" + p.A).Distinct().Count();
            var side = Box("gr-tree__side");
            side.Add(L("Pairs left to try", "gr-tree__side-title"));
            side.Add(L((possible - tried).ToString(), "gr-tree__side-count"));
            side.Add(L(have.Count < 2 ? "Find two of the family to pair them." : "of " + possible + ", with the " + have.Count + " " + GrowthContent.Families.First(f => f.Id == treeFamily).Name + " you have.", "gr-tree__side-text"));
            var show = Link("Show them on Pairs", () => { tabChosen = "Pairs"; ShowTab(); });
            show.name = "gr-tree-to-pairs";
            show.style.marginLeft = 0;
            side.Add(show);
            tree.Add(side);
            body.Add(tree);
        }

        /// <summary>A node's hover tip: a found creature's name and how it is made; one not found, only that it is to find.</summary>
        void RecipeTipFor(VisualElement node, SpeciesDef sp, bool found)
        {
            if (!found) { node.tooltip = "Not found yet: try your pairs and mixes"; return; }
            if (sp.Recipe != null) RecipeTip(node, sp.Name + ": " + GrowthReport.Recipe(sp.Recipe), sp.Recipe);
            else node.tooltip = sp.Name;
        }
    }
}
