using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-33 (tester round 2, F78 and F79): the journal's mix log and pair log, filtered and a page at a time, each mix as coloured
    /// dots and each creature as its picture; and a creature pressed in the journal shows what makes it: its ideal recipe or pair,
    /// and every mix and pair tried that made it.
    /// </summary>
    public sealed partial class GrowthUi
    {
        /// <summary>The logs' filters and pages, and the creature picked, kept while the game runs.</summary>
        readonly HashSet<string> mixCultures = new HashSet<string>();
        bool mixNewOnly;
        string pairFamily;
        int mixPage, pairPage;
        string creaturePicked;
        const int LogPage = 10;

        /// <summary>Validation (F79): the creature picked in the journal, or null.</summary>
        public string CreaturePicked => creaturePicked;

        /// <summary>Validation (F79): picks a creature, as pressing its tile does (null to close it).</summary>
        public void PickCreature(string species) { creaturePicked = species; RefreshCard(); }

        void MixLogTab(VisualElement tab)
        {
            var shop = s.Shop;
            tab.Add(L("Mix log · " + shop.MixLog.Count + " different mixes tried", "gr-label"));
            if (shop.MixLog.Count == 0) { tab.Add(L("Every mix makes a creature. Try two or three doses at the incubator.", "gr-hint")); return; }
            // Filters: the cultures a mix must use (each a chip in its colour), and only the mixes that found something new.
            var filters = Box("gr-chips");
            foreach (var c in GrowthContent.Cultures.Where(c => shop.MixLog.Any(m => m.Doses[GrowthContent.CultureIndex(c.Id)] > 0)))
            {
                string id = c.Id;
                bool on = mixCultures.Contains(id);
                var chip = ChipButton(c.Name, () => { if (!mixCultures.Remove(id)) mixCultures.Add(id); mixPage = 0; }, true, null, CultureColour(id));
                chip.EnableInClassList("gr-filter--on", on);
                chip.tooltip = on ? "Showing mixes with " + c.Name + ": press to stop" : "Show only mixes with " + c.Name;
                filters.Add(chip);
            }
            var fresh = ChipButton("Found something new", () => { mixNewOnly = !mixNewOnly; mixPage = 0; }, true);
            fresh.EnableInClassList("gr-filter--on", mixNewOnly);
            filters.Add(fresh);
            tab.Add(filters);
            var list = shop.MixLog.AsEnumerable().Reverse().Where(m => mixCultures.All(c => m.Doses[GrowthContent.CultureIndex(c)] > 0) && (!mixNewOnly || m.WasNew)).ToList();
            if (list.Count == 0) { tab.Add(L("No mix tried matches these. Press a chip again to take it off.", "gr-hint")); return; }
            tab.Add(L(list.Count + (list.Count == 1 ? " mix" : " mixes") + " shown", "gr-hint"));
            var pages = new List<VisualElement>();
            for (int at = 0; at < list.Count; at += LogPage)
            {
                var page = Box("gr-logrows");
                foreach (var m in list.Skip(at).Take(LogPage))
                {
                    var def = GrowthContent.Get(m.Species);
                    var row = Box("gr-logrow");
                    row.Add(DoseDots(m.Doses));
                    row.Add(L("→", "gr-logrow__text"));
                    row.Add(CreatureIcon(def, ColourFound(shop, def), 28, "gr-rs-icon"));
                    row.Add(L(def.Name + (m.Count > 1 ? " ×" + m.Count : "") + (m.WasNew ? " · new then" : "") + (m.Exact ? " · its recipe" : m.DistanceToNew == 1 ? " · one dose from something new" : ""), "gr-logrow__text"));
                    row.tooltip = DoseWords(m.Doses) + " makes " + def.Name;
                    page.Add(row);
                }
                pages.Add(page);
            }
            Pager(tab, pages, list.Count + (list.Count == 1 ? " mix" : " mixes"), () => mixPage, v => mixPage = v);
        }

        void PairLogTab(VisualElement tab)
        {
            var shop = s.Shop;
            tab.Add(L("Pair log · " + shop.PairLog.Count + " pairs tried", "gr-label"));
            if (shop.PairLog.Count == 0) { tab.Add(L(shop.HasBreedingTank ? "Put two creatures in the breeding tank: every pair makes something." : "Pairs come with the breeding tank (Era 2).", "gr-hint")); return; }
            // A filter by family: what the pair made, or either parent, is of it.
            bool InFamily(GrowthPairEntry p, string family) =>
                GrowthContent.Get(p.Species).Family == family || GrowthContent.Get(p.A).Family == family || GrowthContent.Get(p.B).Family == family;
            var filters = Box("gr-chips");
            var all = ChipButton("Every family", () => { pairFamily = null; pairPage = 0; }, true);
            all.EnableInClassList("gr-filter--on", pairFamily == null);
            filters.Add(all);
            foreach (var f in GrowthContent.Families.Where(f => shop.PairLog.Any(p => InFamily(p, f.Id))))
            {
                string id = f.Id;
                var chip = ChipButton(f.Name, () => { pairFamily = pairFamily == id ? null : id; pairPage = 0; }, true);
                chip.EnableInClassList("gr-filter--on", pairFamily == id);
                filters.Add(chip);
            }
            tab.Add(filters);
            var list = shop.PairLog.AsEnumerable().Reverse().Where(p => pairFamily == null || InFamily(p, pairFamily)).ToList();
            tab.Add(L(list.Count + (list.Count == 1 ? " pair" : " pairs") + " shown", "gr-hint"));
            var pages = new List<VisualElement>();
            for (int at = 0; at < list.Count; at += LogPage)
            {
                var page = Box("gr-logrows");
                foreach (var p in list.Skip(at).Take(LogPage))
                {
                    SpeciesDef a = GrowthContent.Get(p.A), b = GrowthContent.Get(p.B), child = GrowthContent.Get(p.Species);
                    var row = Box("gr-logrow");
                    row.Add(CreatureIcon(a, ColourFound(shop, a), 28, "gr-rs-icon"));
                    row.Add(L("×", "gr-logrow__text"));
                    row.Add(CreatureIcon(b, ColourFound(shop, b), 28, "gr-rs-icon"));
                    row.Add(L("→", "gr-logrow__text"));
                    row.Add(CreatureIcon(child, ColourFound(shop, child), 28, "gr-rs-icon"));
                    row.Add(L(PairLine(p), "gr-logrow__text"));
                    page.Add(row);
                }
                pages.Add(page);
            }
            Pager(tab, pages, list.Count + (list.Count == 1 ? " pair" : " pairs"), () => pairPage, v => pairPage = v);
        }

        /// <summary>F79: a creature found can be pressed, in the journal and the collection, to show what makes it.</summary>
        void Pickable(VisualElement tile, SpeciesDef sp, string pickedStyle)
        {
            if (creaturePicked == sp.Id) tile.AddToClassList(pickedStyle);
            tile.RegisterCallback<ClickEvent>(_ => { creaturePicked = creaturePicked == sp.Id ? null : sp.Id; RefreshCard(); });
        }

        /// <summary>
        /// F79: the creature picked: its ideal recipe (a founder's doses) or a pair that makes it, and every mix and pair you have
        /// tried that made it, so what has been tried is plain to see.
        /// </summary>
        VisualElement CreatureDetail()
        {
            var shop = s.Shop;
            var sp = creaturePicked == null ? null : GrowthContent.Get(creaturePicked);
            if (sp == null || !shop.Discovered.Contains(sp.Id)) return null;
            var box = Box("gr-callout", "gr-jdetail");
            var head = Box("gr-row", "gr-row--spread");
            var who = Box("gr-row");
            who.Add(CreatureIcon(sp, ColourFound(shop, sp), 52, "gr-result__glyph"));
            who.Add(L(sp.Name + " · " + sp.Kind.ToString().ToLowerInvariant() + " · " + sp.Price + " cr", "gr-strong"));
            head.Add(who);
            head.Add(Link("Close", () => creaturePicked = null));
            box.Add(head);
            var ideal = Box("gr-row");
            ideal.Add(L("Best way:", "gr-hint"));
            if (sp.Recipe != null)
            {
                ideal.Add(DoseDots(sp.Recipe)); ideal.Add(L(GrowthReport.Recipe(sp.Recipe) + ", in the incubator", "gr-hint"));
                // GR-35 (F73): a recipe found is made again in the incubator's queue.
                if (shop.HasMakeQueue && shop.FoundRecipes().Contains(sp.Id)) ideal.Add(MakeButton(sp.Id));
            }
            else ideal.Add(L(shop.HowToMake(sp) == "bred" ? "a pair in the breeding tank or the research room" : shop.HowToMake(sp), "gr-hint"));
            box.Add(ideal);
            var mixes = shop.MixLog.Where(m => m.Species == sp.Id).ToList();
            var pairs = shop.PairLog.Where(p => p.Species == sp.Id).ToList();
            if (mixes.Count == 0 && pairs.Count == 0) box.Add(L("Nothing you have tried made it yet.", "gr-hint"));
            if (mixes.Count > 0)
            {
                box.Add(Eyebrow("Mixes you tried that made it · " + mixes.Count));
                var rows = Box("gr-row");
                foreach (var m in mixes.Take(8))
                {
                    var one = Box("gr-row");
                    one.style.marginRight = 14;
                    one.Add(DoseDots(m.Doses));
                    if (m.Count > 1) one.Add(L("×" + m.Count, "gr-hint"));
                    one.tooltip = DoseWords(m.Doses) + (m.Exact ? ": its recipe" : "");
                    rows.Add(one);
                }
                box.Add(rows);
            }
            if (pairs.Count > 0)
            {
                box.Add(Eyebrow("Pairs you tried that made it · " + pairs.Count));
                foreach (var p in pairs.Take(6))
                {
                    SpeciesDef a = GrowthContent.Get(p.A), b = GrowthContent.Get(p.B);
                    var row = Box("gr-row");
                    row.Add(CreatureIcon(a, ColourFound(shop, a), 28, "gr-rs-icon"));
                    row.Add(L("×", "gr-hint"));
                    row.Add(CreatureIcon(b, ColourFound(shop, b), 28, "gr-rs-icon"));
                    row.Add(L(a.Name + " × " + b.Name + (p.Count > 1 ? " · ×" + p.Count : ""), "gr-hint"));
                    box.Add(row);
                }
            }
            return box;
        }
    }
}
