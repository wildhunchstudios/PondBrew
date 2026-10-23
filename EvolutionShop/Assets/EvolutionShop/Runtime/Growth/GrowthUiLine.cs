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
        string targetPick, planA, planB;

        void FillWorkshop()
        {
            var shop = s.Shop;
            var w = GrowthShop.WorkshopDef;
            var body = Head("Workshop", shop.Workshop ? "open" : w.Credits + " credits");
            body.Add(Text("Open the lab annex's east wall into the workshop: the sorter and its hatchling chute, four rack places for lab racks, and the generator's twin turbine and tide engine."));
            body.Add(Text("Needs era 4 (4 stars, powered pumps running and 12 species), 4 stars and " + w.Credits + " credits, at night.", "es-text--muted"));
            var failure = shop.WorkshopCheck(out string why);
            if (!shop.Workshop) body.Add(Button(failure == GrowthFailure.None ? "Open the workshop for " + w.Credits + " cr" : why, () => { if (s.Act(shop.BuyWorkshop).Success) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None));
            Footer(body);
        }

        void FillSorter()
        {
            var shop = s.Shop;
            if (!shop.HasSorter)
            {
                var intro = Head("Hatchling chute and sorter", "not bought");
                intro.Add(Text("The incubator's clutches slide down a chute to the sorter, which puts each one in a shop tank while its species is below the stock target you set, and the rest in the workshop's racks. Nothing it cannot place is lost: it waits on the tray and says why."));
                UpgradeSection(intro, "sorter", "The sorter");
                Footer(intro);
                return;
            }
            var body = Head("Sorter", "draws " + shop.SorterUnits.ToString("0") + " unit");
            Live(body);
            body.Add(Text(shop.SorterStatus, shop.SorterStatus.StartsWith("No room") || shop.SorterStatus.StartsWith("Waiting for power") ? "es-card__reason" : "es-text"));
            ChuteRow(body);
            // The stock targets: how many of each species the shop's tanks should show (shared with Nori's card, GR-09).
            TargetsSection(body, "No targets yet: every hatchling goes to a rack.");
            var racks = shop.Racks.ToList();
            body.Add(Text(racks.Count == 0 ? "No racks yet: place one on a rack place in the workshop." : "Racks: " + string.Join(", ", racks.OrderBy(c => c.Place).Select(c => GrowthShop.RackName(c.Place) + " " + c.Used + "/" + c.Spaces)) + ".", "es-text--muted"));
            Footer(body);
        }

        /// <summary>The chute's line on the sorter's and the incubator's cards: laid with its length, or a button to lay it.</summary>
        void ChuteRow(VisualElement body)
        {
            var shop = s.Shop;
            bool night = shop.Phase == GrowthPhase.Night;
            var chute = shop.Chute;
            if (chute != null)
            {
                var row = Row();
                var label = Text("Chute: from the incubator's outlet to the sorter (" + chute.Length.ToString("0.#") + " m)"); label.style.flexGrow = 1; label.style.width = 330; row.Add(label);
                row.Add(Button("Take it down", () => s.Act(() => shop.RemovePipe(chute.Id)), false, night, night ? null : "Pipes are taken down at night"));
                body.Add(row);
                return;
            }
            string end = s.Card == GrowthCard.Incubator ? GrowthLab.Out : GrowthLab.Sorter;
            string why = !night ? "Pipes are laid at night" : null;
            body.Add(Button(why ?? (end == GrowthLab.Out ? "Lay the chute to the sorter" : "Lay the chute from the incubator"), () => s.StartPipe(end), true, why == null));
        }

        /// <summary>The incubator's chute (GR-08), once the sorter is bought: where its clutches go.</summary>
        void ChuteSection(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.HasSorter) return;
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            var head = Text("Hatchling chute"); head.style.unityFontStyleAndWeight = FontStyle.Bold; section.Add(head);
            section.Add(Text(shop.Chute == null ? "Laid, the chute takes each clutch to the sorter in the workshop." : "Each clutch slides to the sorter: " + shop.SorterStatus.Substring(0, 1).ToLowerInvariant() + shop.SorterStatus.Substring(1) + ".", "es-text--muted"));
            ChuteRow(section);
            body.Add(section);
        }

        void FillRackPlace(string place)
        {
            var shop = s.Shop;
            var def = GrowthContent.Container("rack");
            var body = Head("Rack place " + place.Substring(1), "workshop");
            body.Add(Text("A lab rack holds creatures off the shop floor: the sorter puts what the shop's tanks do not need here, and visitors never see it. " + def.Levels[0].Change + ", creatures of every size; later levels hold more."));
            var failure = shop.RackCheck(place, out string why);
            body.Add(Button(failure == GrowthFailure.None ? "Place a lab rack for " + def.Levels[0].Credits + " cr" : why, () => { if (s.Act(() => shop.BuyRack(place)).Success) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None));
            Footer(body);
        }

        /// <summary>The research bench's analyzer (level 3, a hint a day) and planner (level 4, predicts any two known species).</summary>
        void AnalyzerSection(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.Analyzer) return;
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            var head = Text("Analyzer · today's hint"); head.style.unityFontStyleAndWeight = FontStyle.Bold; section.Add(head);
            section.Add(Text(shop.HintText, shop.Hint != null && !shop.Discovered.Contains(shop.Hint.Species) ? "es-card__reason" : "es-text"));
            if (shop.Planner)
            {
                var head2 = Text("Planner"); head2.style.unityFontStyleAndWeight = FontStyle.Bold; head2.style.marginTop = 8; section.Add(head2);
                var known = GrowthContent.Species.Where(x => shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
                if (planA == null || !known.Contains(planA)) planA = known.FirstOrDefault();
                if (planB == null || !known.Contains(planB)) planB = known.Skip(1).FirstOrDefault() ?? planA;
                if (planA != null)
                {
                    section.Add(PickRow(known, planA, v => planA = v));
                    section.Add(PickRow(known, planB, v => planB = v));
                    var child = shop.PlannerPredicts(planA, planB);
                    section.Add(Text(child == null ? "No prediction." : "A " + GrowthContent.Get(planA).Name + " and a " + GrowthContent.Get(planB).Name + " make a " + child.Name +
                        (shop.Discovered.Contains(child.Id) ? "." : " — not in your journal yet!"), child != null && !shop.Discovered.Contains(child.Id) ? "es-card__reason" : "es-text"));
                }
            }
            else section.Add(Text("The planner (level 4) predicts what any two known species breed.", "es-text--muted"));
            body.Add(section);
        }

        VisualElement PickRow(List<string> known, string now, System.Action<string> set)
        {
            int at = known.IndexOf(now);
            var row = Row();
            row.Add(Button("◀", () => { set(known[(at + known.Count - 1) % known.Count]); RefreshCard(); }, false, known.Count > 1));
            var label = Text(GrowthContent.Get(now).Name); label.style.width = 220; label.style.unityTextAlign = TextAnchor.MiddleCenter; row.Add(label);
            row.Add(Button("▶", () => { set(known[(at + 1) % known.Count]); RefreshCard(); }, false, known.Count > 1));
            return row;
        }

        string LineSignature()
        {
            var shop = s.Shop;
            return shop.Workshop + "|" + string.Join(",", shop.StockTargets.Select(t => t.Key + t.Value)) + "|" + shop.AutoByTargets + "|" + shop.LineDays + shop.LineToday + shop.LineSalesToday +
                "|" + (shop.Hint?.Species ?? "") + shop.Discovered.Count + "|" + shop.SorterStatus + "|" + targetPick + planA + planB;
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
