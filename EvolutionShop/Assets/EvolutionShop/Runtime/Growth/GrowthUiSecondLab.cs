using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-34 wave 2 (tester round 2, F69): the second lab's cards. Its wall's card opens the room and places its machines, each one
    /// at a time; an incubator's card has its tray, its queue with Make for the recipes found (from the buffers and growers, never
    /// the siphon) and, from the first incubator's level 4, its auto-doser's recipe; a breeding tank's card its pairs and nurseries.
    /// Moss's card chooses the incubator and the breeding tank he works at (GrowthUiHelpers).
    /// </summary>
    public sealed partial class GrowthUi
    {
        const string LabRules = "Its machines work at the first incubator's and the first breeding tank's levels. An incubator here has no chamber: it makes the recipes you have found from its queue, dosed from the buffers and growers through the room's pipe, onto its own tray; from the first incubator's level 4 it doses a chosen recipe by itself. A breeding tank here keeps its own pairs (its water has no tint port). Moss can be set to any of them; Nori collects from every tray.";

        void FillSecondLabWall()
        {
            var shop = s.Shop;
            var room = GrowthShop.SecondLabDef;
            var body = Head("Second lab", shop.SecondLab ? "open" : room.Credits + " credits", null, "Through the power room's west wall, below the research room. Era 4 and 4 stars. " + LabRules);
            if (!shop.SecondLab)
            {
                var failure = shop.SecondLabCheck(out string why);
                BuyPanel(body, "The power room's west wall", "The second lab", "Three more incubators and three more breeding tanks, each bought one at a time, working at the first ones' levels, and a pipe from the buffers to them.",
                    Gates(room.Era, room.Stars, room.Credits, 0, false), failure, why, "Open the second lab for " + room.Credits + " cr", shop.BuySecondLab);
                Footer(body);
                return;
            }
            body.Add(L("Open: " + shop.LabIncubators.Count + " of " + GrowthShop.Lab2Places + " incubators and " + shop.LabBreeders.Count + " of " + GrowthShop.Lab2Places + " breeding tanks placed.", "gr-hint"));
            LabBuyButtons(body);
            Footer(body);
        }

        /// <summary>The next incubator and breeding tank to place, each with its price and, when it can't be pressed, why.</summary>
        void LabBuyButtons(VisualElement body)
        {
            var shop = s.Shop;
            if (shop.LabIncubators.Count < GrowthShop.Lab2Places)
            {
                var f = shop.LabIncubatorCheck(out string why);
                var b = Wide("Place incubator " + (shop.LabIncubators.Count + 2) + " · " + shop.LabIncubatorNextCredits + " cr", () => s.Act(shop.BuyLabIncubator), true, f == GrowthFailure.None, why);
                b.name = "gr-buy-lab-incubator";
                body.Add(b);
            }
            if (shop.LabBreeders.Count < GrowthShop.Lab2Places)
            {
                var f = shop.LabBreederCheck(out string why);
                var b = Wide("Place breeding tank " + (shop.LabBreeders.Count + 2) + " · " + shop.LabBreederNextCredits + " cr", () => s.Act(shop.BuyLabBreeder), false, f == GrowthFailure.None, why);
                b.name = "gr-buy-lab-breeder";
                body.Add(b);
            }
        }

        static int LabPlace(string target) => int.TryParse(target, out int k) ? k : 0;

        void FillLabIncubator(string target)
        {
            var shop = s.Shop;
            int place = LabPlace(target);
            var inc = shop.LabIncubator(place);
            string name = "Incubator " + (place + 2);
            if (inc == null)
            {
                var none = Head(name, "not placed", null, "One of the second lab's three incubator places. " + LabRules);
                if (place == shop.LabIncubators.Count) LabBuyButtons(none);
                else none.Add(L("Place incubator " + (shop.LabIncubators.Count + 2) + " first: they stand in turn, west to east.", "gr-hint"));
                Footer(none);
                return;
            }
            var body = Head(name, "", "Make", LabRules);
            LiveStatus();
            if (inc.Tray.Count > 0)
            {
                var first = inc.Tray[0];
                var call = Box("gr-callout");
                var row = Box("gr-row");
                row.Add(CreatureIcon(first.Def, first.Colour, 44, "gr-result__glyph"));
                row.Add(L(inc.Tray.Count > 1 ? inc.Tray.Count + " " + GrowthShop.Plural(first.Def.Name) + " on its tray" : "A " + first.Def.Name + " on its tray", "gr-strong"));
                call.Add(row);
                call.Add(Wide("Take the " + first.Def.Name + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(() => shop.TakeLabHatchling(place)), true,
                    shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full: put a creature in a tank first"));
                body.Add(call);
            }
            // Its queue, as the laboratory's (F73).
            var box = Box("gr-section");
            box.Add(Eyebrow("Queue · " + inc.Queue.Count + " of " + GrowthShop.MakeQueueLimit));
            if (inc.Queue.Count == 0) box.Add(L("Make a recipe and it makes it by itself, batch after batch, from the buffers and growers.", "gr-hint"));
            else
            {
                var row = Box("gr-row");
                row.style.flexWrap = Wrap.Wrap;
                for (int i = 0; i < inc.Queue.Count;)
                {
                    int j = i;
                    while (j < inc.Queue.Count && inc.Queue[j] == inc.Queue[i]) j++;
                    string id = inc.Queue[i];
                    var def = GrowthContent.Get(id);
                    var chip = Box("gr-qchip");
                    chip.Add(CreatureIcon(def, Colour.Classic, 28, "gr-rs-icon"));
                    chip.Add(L(def.Name + " ×" + (j - i), "gr-qchip__text"));
                    var minus = PlusMinus("−", () => s.Act(() => shop.UnqueueLabMake(place, id)), true, null);
                    minus.tooltip = "Take a " + def.Name + " off the queue";
                    chip.Add(minus);
                    row.Add(chip);
                    i = j;
                }
                box.Add(row);
            }
            box.Add(LiveL(() => shop.LabIncubatorStatus(place), "gr-hint"));
            var found = shop.FoundRecipes();
            var make = Box("gr-row");
            make.style.flexWrap = Wrap.Wrap;
            make.Add(L(found.Count == 0 ? "No recipe found yet: find one at the first incubator." : "Make:", "gr-hint"));
            foreach (var id in found.Take(12))
            {
                string species = id;
                bool can = shop.LabMakeCheck(place, species, out string why) == GrowthFailure.None;
                var b = ChipButton(GrowthContent.Get(species).Name + " +1", () => s.Act(() => shop.QueueLabMake(place, species)), can, can ? "Add a " + GrowthContent.Get(species).Name + " to its queue" : why);
                b.name = "gr-lab-make-" + species;
                make.Add(b);
            }
            box.Add(make);
            body.Add(box);
            // Its auto-doser, from the first incubator's level 4.
            var auto = Box("gr-section");
            auto.Add(Eyebrow("Auto-doser"));
            if (!shop.LabAutoDoser) auto.Add(L("Comes with the first incubator's level 4: it then doses a recipe you choose by itself, batch after batch.", "gr-hint"));
            else
            {
                auto.Add(L(inc.Auto == null ? "Off: choose a recipe it doses whenever it is free (its queue goes first)." : "Dosing a " + GrowthContent.Get(inc.Auto).Name + " whenever it is free (its queue goes first).", "gr-hint"));
                var row = Box("gr-row");
                row.style.flexWrap = Wrap.Wrap;
                foreach (var id in found.Take(12))
                {
                    string species = id;
                    bool on = inc.Auto == species;
                    var b = ChipButton(on ? GrowthContent.Get(species).Name + " · on" : GrowthContent.Get(species).Name, () => s.Act(() => shop.SetLabAuto(place, on ? null : species)), true, on ? "Stop it" : "Dose a " + GrowthContent.Get(species).Name + " by itself");
                    b.name = "gr-lab-auto-" + species;
                    row.Add(b);
                }
                auto.Add(row);
            }
            body.Add(auto);
            if (shop.MossIncubator == place + 1) body.Add(L("Moss runs his recipe here" + (shop.MossRecipe != null ? ": a " + GrowthContent.Get(shop.MossRecipe).Name : "") + ".", "gr-hint"));
            Footer(body);
        }

        void FillLabBreeder(string target)
        {
            var shop = s.Shop;
            int place = LabPlace(target);
            var b = shop.LabBreeder(place);
            string name = "Breeding tank " + (place + 2);
            if (b == null)
            {
                var none = Head(name, "not placed", null, "One of the second lab's three breeding tank places. " + LabRules);
                if (place == shop.LabBreeders.Count) LabBuyButtons(none);
                else none.Add(L("Place breeding tank " + (shop.LabBreeders.Count + 2) + " first: they stand in turn, south to north.", "gr-hint"));
                Footer(none);
                return;
            }
            var body = Head(name, "", "Pair", "Two creatures in, one offspring out, and the parents stay; a pair breeds the same thing every time. Untinted: an offspring takes the higher parent colour. " + LabRules);
            LiveStatus();
            var pairs = shop.LabActivePairs(b).ToList();
            for (int k = 0; k < pairs.Count; k++)
            {
                var pairState = pairs[k];
                int pairIndex = k;
                var section = Box("gr-section");
                if (pairs.Count > 1) section.Add(L("Pair " + (k + 1) + " (" + pairState.Parents.Count + "/" + GrowthShop.PairSize + ")" + (pairState.Breeding ? " · breeding" : pairState.Offspring != null ? " · offspring ready" : ""), "gr-label"));
                if (pairState.Offspring != null)
                {
                    var o = pairState.Offspring;
                    var call = Box("gr-callout");
                    var row = Box("gr-row");
                    row.Add(CreatureIcon(o.Def, o.Colour, 56, "gr-result__glyph"));
                    row.Add(L("In the nursery: a " + GrowthSession.Named(o) + (pairState.OffspringWasNew ? " — new to your journal!" : "."), "gr-strong"));
                    call.Add(row);
                    call.Add(Wide("Take the " + GrowthSession.Named(o) + " (" + shop.Carried.Count + "/" + GrowthShop.CarryLimit + " carried)", () => s.Act(() => shop.TakeLabOffspring(place, pairIndex)), true,
                        shop.Carried.Count < GrowthShop.CarryLimit, "Your hands are full: put a creature in a tank first"));
                    section.Add(call);
                }
                var pair = Box("gr-pair");
                for (int i = 0; i < GrowthShop.PairSize; i++)
                {
                    if (i == 1) pair.Add(L("×", "gr-times"));
                    if (i < pairState.Parents.Count)
                    {
                        var p = pairState.Parents[i];
                        int id = p.Id;
                        bool can = shop.Carried.Count < GrowthShop.CarryLimit;
                        var parent = new Button(() => { if (can) { s.Act(() => shop.TakeLabParent(place, id)); RefreshCard(); } });
                        parent.AddToClassList("gr-parent");
                        parent.SetEnabled(can);
                        parent.tooltip = can ? "Take it out" : "Your hands are full";
                        parent.Add(CreatureIcon(p.Def, p.Colour, 52, "gr-parent__glyph"));
                        parent.Add(L(p.Def.Name, "gr-tile__name"));
                        parent.Add(L(p.Colour.ToString(), "gr-dose__count"));
                        pair.Add(parent);
                    }
                    else
                    {
                        var empty = Box("gr-parent", "gr-parent--empty");
                        empty.Add(L("Empty", "gr-tile__name"));
                        empty.Add(L(shop.Carried.Count > 0 ? "put one in below" : "click here with one in hand", "gr-dose__count"));
                        pair.Add(empty);
                    }
                }
                section.Add(pair);
                if (pairState.Parents.Count >= GrowthShop.PairSize)
                {
                    var known = shop.PairEntry(pairState.Parents[0].Species, pairState.Parents[1].Species);
                    var makes = known != null ? GrowthContent.Get(known.Species) : shop.PlannerPredicts(pairState.Parents[0].Species, pairState.Parents[1].Species);
                    section.Add(L("They'll make: " + (makes != null ? makes.Name : "a pair you have not tried") + (known != null ? " · bred ×" + known.Count + " so far" : ""), "gr-hint"));
                    int at = k;
                    section.Add(LiveL(() => { var ps = shop.LabBreeder(place)?.Pairs[at]; return ps == null || !ps.Breeding ? "" : Mathf.CeilToInt(ps.Left) + " s to the offspring"; }, "gr-hint"));
                }
                body.Add(section);
            }
            if (pairs.Any(p => p.Parents.Count < GrowthShop.PairSize) && shop.Carried.Count > 0)
            {
                var put = Box("gr-row", "gr-section");
                foreach (var carried in shop.Carried.ToList())
                {
                    var spec = carried;
                    var f = shop.LabParentCheck(place, spec, out string why);
                    put.Add(Button("Put in " + GrowthSession.Named(spec), () => s.Act(() => shop.PutLabParent(place, spec.Id)), true, f == GrowthFailure.None, why));
                }
                body.Add(put);
            }
            if (shop.MossBreeder == place + 1) body.Add(L("Moss keeps his pair here.", "gr-hint"));
            Footer(body);
        }

        /// <summary>Moss's machines (GrowthUiHelpers' card): which incubator runs his recipe and which breeding tank keeps his pair.</summary>
        void MossMachines(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.SecondLab || shop.LabIncubators.Count == 0 && shop.LabBreeders.Count == 0) return;
            var box = Box("gr-section"); body.Add(box);
            box.Add(L("Where", "gr-label"));
            var incs = Box("gr-row");
            incs.style.flexWrap = Wrap.Wrap;
            incs.Add(L("His recipe in:", "gr-hint"));
            for (int k = 0; k <= shop.LabIncubators.Count; k++)
            {
                int which = k;
                bool on = shop.MossIncubator == which;
                bool dosing = shop.MossBatch && !on;
                var b = ChipButton(which == 0 ? "The incubator" : "Incubator " + (which + 1), () => s.Act(() => shop.SetMossIncubator(which)), !on && !dosing, on ? "He runs it here" : dosing ? "Wait for the batch Moss is dosing" : null);
                b.name = "gr-moss-incubator-" + which;
                incs.Add(b);
            }
            box.Add(incs);
            if (shop.LabBreeders.Count == 0) return;
            var tanks = Box("gr-row");
            tanks.style.flexWrap = Wrap.Wrap;
            tanks.Add(L("His pair in:", "gr-hint"));
            for (int k = 0; k <= shop.LabBreeders.Count; k++)
            {
                int which = k;
                bool on = shop.MossBreeder == which;
                var b = ChipButton(which == 0 ? "The breeding tank" : "Breeding tank " + (which + 1), () => s.Act(() => shop.SetMossBreeder(which)), !on, on ? "He keeps it here" : null);
                b.name = "gr-moss-breeder-" + which;
                tanks.Add(b);
            }
            box.Add(tanks);
        }

        string SecondLabSignature()
        {
            var shop = s.Shop;
            if (!shop.SecondLab) return "";
            return shop.MossIncubator + "," + shop.MossBreeder + "|" + string.Join(";", shop.LabIncubators.Select(i => i.Incubating + ":" + string.Join(",", i.Queue) + ":" + i.Auto + ":" + string.Join(",", i.Tray.Select(x => x.Id)))) +
                "|" + string.Join(";", shop.LabBreeders.Select(b => string.Join(",", b.Pairs.Select(p => string.Join("+", p.Parents.Select(x => x.Id)) + "/" + (p.Offspring?.Id ?? 0)))));
        }

        string SecondLabLiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.LabIncubator: return shop.LabIncubatorStatus(LabPlace(s.CardTarget));
                case GrowthCard.LabBreeder: return shop.LabBreederStatus(LabPlace(s.CardTarget));
                case GrowthCard.SecondLab: return shop.SecondLab ? "Open" : "Closed";
                default: return null;
            }
        }
    }
}
