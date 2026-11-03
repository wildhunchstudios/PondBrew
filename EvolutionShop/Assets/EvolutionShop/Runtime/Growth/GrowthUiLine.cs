using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-08's cards (GR.13): the workshop, the sorter with its chute and the stock targets it sorts by, a rack place, the
    /// incubator's chute, and the research bench's analyzer and planner. The chute is laid in pipe mode from the sorter's or
    /// the incubator's card, as GR-07's pipes are.
    /// </summary>
    public sealed partial class GrowthUi
    {
        /// <summary>The species the stock targets' picker shows (the sorter's and Nori's cards), and the planner's two picks (the cards' own state).</summary>
        string planA, planB;

        void FillWorkshop()
        {
            var shop = s.Shop;
            var w = GrowthShop.WorkshopDef;
            var body = Head("Workshop", shop.Workshop ? "open" : w.Credits + " credits", null, "Through the power room's east wall. Era 4 comes with 4 stars, powered pumps running and 12 species.");
            if (shop.Workshop) { body.Add(L("Open: the sorter and its chute, four rack places, and the generator's twin turbine and tide engine.", "gr-hint")); Footer(body); return; }
            var failure = shop.WorkshopCheck(out string why);
            BuyPanel(body, "The power room's east wall", "The workshop", "The sorter and its hatchling chute, four rack places for lab racks, and the generator's twin turbine and tide engine.",
                Gates(w.Era, w.Stars, w.Credits, 0, false), failure, why, "Open the workshop for " + w.Credits + " cr", shop.BuyWorkshop);
            Footer(body);
        }

        /// <summary>
        /// GR-13's sorter: Sorter (what it is doing, its chute, the racks) and Targets (the stock targets it sorts by, shared
        /// with Nori's card). Not bought, the chute and sorter as a purchase.
        /// </summary>
        void FillSorter()
        {
            var shop = s.Shop;
            const string rules = "The incubator's hatchlings slide down a chute to the sorter, which puts each one in a shop tank while its species is below the stock target you set, and the rest in the workshop's racks. Nothing it cannot place is lost: it waits on the tray and says why.";
            if (!shop.HasSorter)
            {
                var intro = Head("Hatchling chute and sorter", "not bought", null, rules);
                UpgradeSection(intro, "sorter", "The sorter");
                Footer(intro);
                return;
            }
            var body = Head("Sorter", "", "Sorter", rules);
            LiveStatus();
            bool trouble = shop.SorterStatus.StartsWith("No room") || shop.SorterStatus.StartsWith("Waiting for power");
            var state = Box("gr-row", "gr-section");
            state.Add(Pill(shop.SorterStatus, trouble ? 2 : 0));
            state.Add(Chip("draws " + shop.SorterUnits.ToString("0") + " unit"));
            body.Add(state);
            body.Add(Eyebrow("Chute"));
            ChuteRow(body);
            body.Add(Eyebrow("Racks"));
            var racks = shop.Racks.ToList();
            var rackLine = L(racks.Count == 0 ? "No racks yet: place one on a rack place in the workshop." : "Racks: " + string.Join(", ", racks.OrderBy(c => c.Place).Select(c => GrowthShop.RackName(c.Place) + " " + c.Used + "/" + c.Spaces)) + ".", "gr-hint");
            body.Add(rackLine);
            // The stock targets: how many of each species the shop's tanks should show (shared with Nori's card, GR-09).
            TargetsSection(AddTab("Targets"), "No targets yet: every hatchling goes to a rack.");
            Footer(body);
        }

        /// <summary>The chute on the sorter's and the incubator's cards: laid with its length, or a button to lay it.</summary>
        void ChuteRow(VisualElement body)
        {
            var shop = s.Shop;
            bool night = shop.Phase == GrowthPhase.Night;
            var chute = shop.Chute;
            if (chute != null)
            {
                body.Add(PipeItem("Chute: from the incubator's outlet to the sorter", chute.Length.ToString("0.#") + " m", chute.Id));
                return;
            }
            string end = s.Card == GrowthCard.Incubator ? GrowthLab.Out : GrowthLab.Sorter;
            body.Add(Wide(end == GrowthLab.Out ? "Lay the chute to the sorter" : "Lay the chute from the incubator", () => s.StartPipe(end), true, true));
        }

        /// <summary>The incubator's chute (GR-08), once the sorter is bought: where its clutches go.</summary>
        void ChuteSection(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.HasSorter) return;
            var section = Box("gr-section");
            section.Add(L("Hatchling chute", "gr-label"));
            section.Add(L(shop.Chute == null ? "Laid, the chute takes the hatchlings to the sorter in the workshop." : "Hatchlings slide to the sorter: " + shop.SorterStatus.Substring(0, 1).ToLowerInvariant() + shop.SorterStatus.Substring(1) + ".", "gr-hint"));
            ChuteRow(section);
            body.Add(section);
        }

        void FillRackPlace(string place)
        {
            var shop = s.Shop;
            var def = GrowthContent.Container("rack");
            var body = Head("Rack place " + place.Substring(1), "workshop", null, "A lab rack holds creatures off the shop floor: the sorter puts what the shop's tanks do not need here, and visitors never see it. Later levels hold more.");
            var failure = shop.RackCheck(place, out string why);
            BuyPanel(body, "A lab rack", def.Levels[0].Change + ", creatures of every size", "Off the shop floor, never seen by visitors.", Gates(0, 0, def.Levels[0].Credits, 0, false), failure, why,
                "Place a lab rack for " + def.Levels[0].Credits + " cr", () => shop.BuyRack(place));
            Footer(body);
        }

        /// <summary>The research bench's analyzer (level 3): a hint a day.</summary>
        void AnalyzerSection(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.Analyzer) return;
            body.Add(L("Analyzer · today's hint", "gr-label"));
            bool fresh = shop.Hint != null && !shop.Discovered.Contains(shop.Hint.Species);
            var box = Box(fresh ? "gr-callout" : "gr-result");
            var hint = L(shop.HintText, "gr-strong"); hint.style.fontSize = 22;
            box.Add(hint);
            body.Add(box);
            body.Add(L(shop.Planner ? "A new hint comes each day." : "A new hint comes each day. The planner (level 4) predicts what any two known species breed.", "gr-hint"));
        }

        /// <summary>The research bench's planner (level 4): pick two species you know and see what they'd make.</summary>
        void PlannerSection(VisualElement body)
        {
            var shop = s.Shop;
            body.Add(L("Planner · pick two you know", "gr-label"));
            var known = GrowthContent.Species.Where(x => shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            if (planA == null || !known.Contains(planA)) planA = known.FirstOrDefault();
            if (planB == null || !known.Contains(planB)) planB = known.Skip(1).FirstOrDefault() ?? planA;
            if (planA == null) { body.Add(L("Discover a creature first.", "gr-hint")); return; }
            var pair = Box("gr-pair");
            pair.Add(Picker(known, planA, v => planA = v, null));
            pair.Add(L("×", "gr-times"));
            pair.Add(Picker(known, planB, v => planB = v, null));
            body.Add(pair);
            var child = shop.PlannerPredicts(planA, planB);
            body.Add(Eyebrow("They'd make"));
            var result = Box("gr-result");
            if (child != null) result.Add(CreatureIcon(child, Colour.Classic, 56, "gr-result__glyph"));
            else { var g = new GrowthGlyph(new Color(0.72f, 0.67f, 0.57f)); g.AddToClassList("gr-result__glyph"); result.Add(g); }
            var words = Box("gr-result__text");
            var name = Box("gr-row");
            name.Add(L(child == null ? "?" : child.Name, "gr-strong"));
            if (child != null && !shop.Discovered.Contains(child.Id)) name.Add(L("New", "gr-newtag"));
            words.Add(name);
            words.Add(L(child == null ? "No prediction." : "A " + GrowthContent.Get(planA).Name + " and a " + GrowthContent.Get(planB).Name + " make a " + child.Name +
                (shop.Discovered.Contains(child.Id) ? "." : " — not in your journal yet!"), "gr-hint"));
            result.Add(words);
            body.Add(result);
        }

        string LineSignature()
        {
            var shop = s.Shop;
            return shop.Workshop + "|" + string.Join(",", shop.StockTargets.Select(t => t.Key + t.Value)) + "|" + shop.AutoByTargets + "|" + shop.LineDays + shop.LineToday + shop.LineSalesToday +
                "|" + (shop.Hint?.Species ?? "") + shop.Discovered.Count + "|" + shop.SorterStatus + "|" + planA + planB;
        }

        string LineLiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Sorter: return !shop.HasSorter ? "" : shop.Hatchlings.Count + " on the tray · " + shop.Racks.Sum(c => c.Occupants.Count) + " in racks";
                default: return null;
            }
        }
    }
}
