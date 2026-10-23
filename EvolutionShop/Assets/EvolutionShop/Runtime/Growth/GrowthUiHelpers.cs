using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-09's cards (GR.10): the staff board (who could work here, what each needs and does, hiring at night) and each
    /// helper's card (their level and what the next one gives, their duty and wage, training at night, what they are doing
    /// now, and their job's settings: Nori's stock targets, which the sorter shares; Moss's known recipe and known pair, and
    /// at level 2 which of the two he runs).
    /// </summary>
    public sealed partial class GrowthUi
    {
        /// <summary>The recipe and the pair Moss's card shows in its pickers (the card's own state).</summary>
        string mossRecipePick;
        int mossPairPick = -1;

        static string Duties(string id)
        {
            switch (id)
            {
                case "pip": return "Serves at the counter: bags each visitor's order while you do other work. With Pip at the till, you serve at the counter's second till (its level 4).";
                case "nori": return "Keeps the shop's tanks at your stock targets: carries hatchlings from the incubator's tray, offspring from the nursery and creatures from the racks to the tanks below their target, and the rest to the racks.";
                default: return "Runs a recipe you have made at the incubator: fetches its doses from the growers and starts it, clutch after clutch. From level 2, keeps a pair you have bred breeding.";
            }
        }

        VisualElement Section(VisualElement body, string title)
        {
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            var head = Text(title); head.style.unityFontStyleAndWeight = FontStyle.Bold; section.Add(head);
            body.Add(section);
            return section;
        }

        void FillStaff()
        {
            var shop = s.Shop;
            var body = Head("Staff board", shop.Staff.Count + " of " + GrowthContent.Helpers.Length + " hired");
            Live(body);
            body.Add(Text("Helpers are hired and trained at night. Each is paid a wage for a night and the day after it, when the night starts; one the shop cannot pay stays home and says so, and comes back when you call them."));
            foreach (var def in GrowthContent.Helpers)
            {
                var section = Section(body, def.Name + ", " + def.Job);
                var h = shop.Helper(def.Id);
                string id = def.Id;
                if (h != null)
                {
                    section.Add(Text("Level " + h.Level + " · " + (h.OnDuty ? "on duty" : "home") + " · " + def.Wages[h.Level - 1] + " credits a night and day. " + h.Status + "."));
                    section.Add(Button("Open " + def.Name + "'s card", () => s.SetCard(GrowthCard.Helper, id)));
                    continue;
                }
                section.Add(Text(Duties(id), "es-text--muted"));
                section.Add(Text("Needs " + def.Gate + ". The fee is " + def.Fee + " credits, then " + def.Wages[0] + " a night and day.", "es-text--muted"));
                var failure = shop.HireCheck(id, out string why);
                section.Add(Button(failure == GrowthFailure.None ? "Hire " + def.Name + " for " + (def.Fee + def.Wages[0]) + " cr (the fee and tonight's wage)" : why,
                    () => { if (s.Act(() => shop.Hire(id)).Success) s.SetCard(GrowthCard.Helper, id); }, true, failure == GrowthFailure.None));
            }
            Footer(body);
        }

        void FillHelper(string id)
        {
            var shop = s.Shop;
            var def = GrowthContent.Helper(id);
            var h = shop.Helper(id);
            if (def == null || h == null)
            {
                var none = Head(def?.Name ?? "Helper", "not hired");
                none.Add(Text("Hire them at the staff board, on the shop's north wall."));
                Footer(none);
                return;
            }
            var body = Head(def.Name + ", " + def.Job, "level " + h.Level);
            Live(body);
            body.Add(Text(GrowthShop.LevelText(id, h.Level) + "."));
            int wage = def.Wages[h.Level - 1];
            bool paid = h.PaidPeriod == shop.Period;
            var duty = Row();
            var dutyText = Text(h.OnDuty ? "On duty: " + wage + " credits a night and day, paid when the night starts." : "Home: no wage. " + (paid ? "Tonight is paid: calling " + def.Name + " back costs nothing." : "Calling " + def.Name + " back pays tonight's wage (" + wage + " credits)."));
            dutyText.style.width = 330; dutyText.style.flexGrow = 1; duty.Add(dutyText);
            bool on = h.OnDuty;
            duty.Add(Button(on ? "Send home" : paid ? "Call back" : "Call back for " + wage + " cr", () => s.Act(() => shop.SetDuty(id, !on)), !on));
            body.Add(duty);
            var train = Section(body, "Training");
            if (h.Level >= 3) train.Add(Text("Highest level.", "es-text--muted"));
            else
            {
                train.Add(Text("Level " + (h.Level + 1) + ": " + GrowthShop.LevelText(id, h.Level + 1) + ". The wage rises to " + def.Wages[h.Level] + " from the next night.", "es-text--muted"));
                var failure = shop.TrainCheck(id, out string why);
                train.Add(Button(failure == GrowthFailure.None ? "Train to level " + (h.Level + 1) + " for " + def.TrainCosts[h.Level - 1] + " cr" : why, () => s.Act(() => shop.Train(id)), true, failure == GrowthFailure.None));
            }
            if (id == "pip")
                body.Add(Text("Pip takes the front of the queue. " + (shop.SecondTill ? "At the second till you serve beside Pip: while you hold the counter the front of the queue is yours, and Pip takes the visitor behind." : "You serve only at the counter's second till (its level 4) while Pip is at work."), "es-text--muted"));
            else if (id == "nori") TargetsSection(body, "No targets yet: Nori moves nothing to the shop's tanks until you set some.");
            else MossSection(body, h);
            Footer(body);
        }

        /// <summary>The stock targets (GR-08): how many of each species the shop's tanks should show; Nori's from era 3, the sorter's too.</summary>
        void TargetsSection(VisualElement body, string empty)
        {
            var shop = s.Shop;
            var section = Section(body, "Stock targets");
            var known = GrowthContent.Species.Where(x => shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            section.Add(Text(shop.StockTargets.Count == 0 ? empty :
                "Targets: " + string.Join(", ", known.Where(sp => shop.Target(sp) > 0).Select(sp => GrowthContent.Get(sp).Name + " " + shop.Shown(sp) + "/" + shop.Target(sp))) + ". Below its target, a species goes to the shop's tanks; the rest to the racks.", "es-text--muted"));
            if (known.Count == 0) return;
            if (targetPick == null || !known.Contains(targetPick)) targetPick = known.FirstOrDefault(sp => shop.Target(sp) > 0) ?? known[0];
            int at = known.IndexOf(targetPick);
            var row = Row();
            row.Add(Button("◀", () => { targetPick = known[(at + known.Count - 1) % known.Count]; RefreshCard(); }, false, known.Count > 1));
            var label = Text(GrowthContent.Get(targetPick).Name + " · in the shop " + shop.Shown(targetPick) + " · in racks " + shop.Racked(targetPick) + " · target " + shop.Target(targetPick));
            label.style.width = 300; label.style.flexGrow = 1; label.style.flexShrink = 1; row.Add(label);
            row.Add(Button("▶", () => { targetPick = known[(at + 1) % known.Count]; RefreshCard(); }, false, known.Count > 1));
            string pick = targetPick;
            row.Add(Button("−", () => s.Act(() => shop.SetTarget(pick, shop.Target(pick) - 1)), false, shop.Target(pick) > 0));
            row.Add(Button("+", () => s.Act(() => shop.SetTarget(pick, shop.Target(pick) + 1)), true, shop.Target(pick) < GrowthShop.MostTarget));
            section.Add(row);
        }

        static string RecipeText(string species)
        {
            var recipe = GrowthContent.Get(species).Recipe;
            return string.Join(", ", recipe.Select((n, i) => (n, i)).Where(x => x.n > 0).Select(x => x.n + " " + GrowthShop.Name(GrowthContent.Cultures[x.i].Id)));
        }

        /// <summary>Moss's jobs: the known recipe he runs, the known pair he keeps breeding (level 2), and which runs at level 2.</summary>
        void MossSection(VisualElement body, GrowthHelper h)
        {
            var shop = s.Shop;
            var recipe = Section(body, "Recipe");
            var founders = GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder && shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            recipe.Add(Text(shop.MossRecipe == null ? "No recipe chosen yet." : "Running a " + GrowthContent.Get(shop.MossRecipe).Name + ": " + RecipeText(shop.MossRecipe) + ". " + shop.MossBatches + " batch" + (shop.MossBatches == 1 ? "" : "es") + " so far."));
            if (founders.Count == 0) recipe.Add(Text("No founder made yet: Moss runs only recipes you have made.", "es-text--muted"));
            else
            {
                if (mossRecipePick == null || !founders.Contains(mossRecipePick)) mossRecipePick = shop.MossRecipe ?? founders[0];
                int at = founders.IndexOf(mossRecipePick);
                var row = Row();
                row.Add(Button("◀", () => { mossRecipePick = founders[(at + founders.Count - 1) % founders.Count]; RefreshCard(); }, false, founders.Count > 1));
                var label = Text(GrowthContent.Get(mossRecipePick).Name + " · " + RecipeText(mossRecipePick)); label.style.width = 260; label.style.flexGrow = 1; label.style.flexShrink = 1; row.Add(label);
                row.Add(Button("▶", () => { mossRecipePick = founders[(at + 1) % founders.Count]; RefreshCard(); }, false, founders.Count > 1));
                string pick = mossRecipePick;
                var failure = shop.MossRecipeCheck(pick, out string why);
                bool chosen = pick == shop.MossRecipe;
                row.Add(Button(chosen ? "Running" : failure == GrowthFailure.None ? "Run it" : why, () => s.Act(() => shop.SetMossRecipe(pick)), true, !chosen && failure == GrowthFailure.None));
                recipe.Add(row);
            }
            var pair = Section(body, "Pair");
            if (h.Level < 2) { pair.Add(Text("At level 2, Moss keeps a pair you have bred breeding.", "es-text--muted")); return; }
            pair.Add(Text(shop.MossPairA == null ? "No pair chosen yet." : "Keeping a " + GrowthContent.Get(shop.MossPairA).Name + " and a " + GrowthContent.Get(shop.MossPairB).Name + " breeding."));
            var log = shop.PairLog;
            if (log.Count == 0) pair.Add(Text("No pair bred yet: Moss keeps only pairs you have bred.", "es-text--muted"));
            else
            {
                if (mossPairPick < 0 || mossPairPick >= log.Count)
                {
                    int now = shop.MossPairA == null ? -1 : log.FindIndex(p => p == shop.PairEntry(shop.MossPairA, shop.MossPairB));
                    mossPairPick = now < 0 ? 0 : now;
                }
                int at = mossPairPick;
                var entry = log[at];
                var row = Row();
                row.Add(Button("◀", () => { mossPairPick = (at + log.Count - 1) % log.Count; RefreshCard(); }, false, log.Count > 1));
                var label = Text(GrowthContent.Get(entry.A).Name + " and " + GrowthContent.Get(entry.B).Name + " make a " + GrowthContent.Get(entry.Species).Name); label.style.width = 260; label.style.flexGrow = 1; label.style.flexShrink = 1; row.Add(label);
                row.Add(Button("▶", () => { mossPairPick = (at + 1) % log.Count; RefreshCard(); }, false, log.Count > 1));
                bool chosen = shop.MossPairA != null && shop.PairEntry(shop.MossPairA, shop.MossPairB) == entry;
                row.Add(Button(chosen ? "Breeding" : "Keep them breeding", () => s.Act(() => shop.SetMossPair(entry.A, entry.B)), true, !chosen));
                pair.Add(row);
            }
            var job = Section(body, "Job");
            if (h.Level >= 3) { job.Add(Text("At level 3 Moss does both, in turn.", "es-text--muted")); return; }
            job.Add(Text("At level 2 Moss runs one of the two jobs; at level 3 both.", "es-text--muted"));
            var jobs = Row();
            bool recipeJob = shop.MossTask == GrowthShop.MossRecipeJob;
            jobs.Add(Button("Run the recipe", () => s.Act(() => shop.SetMossTask(GrowthShop.MossRecipeJob)), recipeJob, !recipeJob));
            jobs.Add(Button("Keep the pair breeding", () => s.Act(() => shop.SetMossTask(GrowthShop.MossPairJob)), !recipeJob, recipeJob));
            job.Add(jobs);
        }

        string HelperSignature()
        {
            var shop = s.Shop;
            return string.Join(",", shop.Staff.Values.Select(h => h.Id + h.Level + h.OnDuty + h.PaidPeriod)) + "|" + shop.MossRecipe + shop.MossPairA + shop.MossPairB + shop.MossTask + shop.MossBatches +
                "|" + (GrowthContent.Helpers.Select(d => shop.HireCheck(d.Id, out _).ToString()).Aggregate((a, b) => a + b)) + "|" + mossRecipePick + mossPairPick + shop.SalesTotal;
        }

        string HelperLiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Staff: return shop.Staff.Count == 0 ? "No one hired yet." : "Wages: " + shop.WageBill + " credits a night and day.";
                case GrowthCard.Helper: return shop.Helper(s.CardTarget)?.Status ?? "";
                default: return null;
            }
        }
    }
}
