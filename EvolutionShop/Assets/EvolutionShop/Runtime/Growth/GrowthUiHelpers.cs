using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-09's cards (GR.10) in GR-13's style: the staff board (each helper as a panel: who they are, what they need and
    /// cost, or their level, duty and wage, hiring at night) and each helper's card, Job (their duty and what they are
    /// doing; their job's settings: Nori's stock targets, which the sorter shares; Moss's known recipe and known pair, and
    /// at level 2 which of the two he runs) and Training (a track of their levels, the next one with its price).
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
                default: return "Works by hand where no machine reaches: fetches a recipe's doses from the growers, with no pipes, and starts it, batch after batch. From level 2, keeps a pair you have bred breeding, which only Moss does. With the auto-doser on, set Moss to the pair.";
            }
        }

        /// <summary>A helper's round badge with their initial.</summary>
        static VisualElement Avatar(HelperDef def)
        {
            var a = Box("gr-avatar");
            a.Add(L(def.Name.Substring(0, 1), "gr-avatar__letter"));
            return a;
        }

        void FillStaff()
        {
            var shop = s.Shop;
            var body = Head("Staff board", "", null, "Helpers are hired and trained at night. Each is paid a wage for a night and the day after it, when the night starts; one the shop cannot pay stays home and says so, and comes back when you call them.");
            LiveStatus();
            foreach (var def in GrowthContent.Helpers)
            {
                var h = shop.Helper(def.Id);
                string id = def.Id;
                var panel = Box("gr-benefit");
                panel.tooltip = Duties(id);
                var top = Box("gr-row"); top.style.flexWrap = Wrap.NoWrap;
                top.Add(Avatar(def));
                var words = Box("gr-result__text");
                words.Add(L(def.Name + ", " + def.Job, "gr-strong"));
                words.Add(L(h != null ? "Level " + h.Level + " · " + (h.OnDuty ? "on duty" : "home") + " · " + def.Wages[h.Level - 1] + " credits a night and day. " + h.Status + "." :
                    GrowthShop.LevelText(id, 1) + ".", "gr-hint"));
                top.Add(words);
                if (h != null) top.Add(Link("Open", () => s.SetCard(GrowthCard.Helper, id)));
                panel.Add(top);
                if (h == null)
                {
                    var failure = shop.HireCheck(id, out string why);
                    var chips = Gates(0, def.Stars, def.Fee + def.Wages[0], 0, true);
                    // The deed it waits for (150 sales, a Large tank, a powered generator), coral until done.
                    string deed = def.Gate.Contains(" and ") ? def.Gate.Substring(def.Gate.IndexOf(" and ") + 5) : def.Gate;
                    string later = shop.HelperAchievement(id);
                    chips.Insert(0, Chip(char.ToUpperInvariant(deed[0]) + deed.Substring(1), later != null, later));
                    LockChip(chips, failure, why);
                    panel.Add(chips);
                    var hire = Wide("Hire " + def.Name + " for " + (def.Fee + def.Wages[0]) + " cr (the fee and tonight's wage)", () => { if (s.Act(() => shop.Hire(id)).Success) s.SetCard(GrowthCard.Helper, id); }, true, failure == GrowthFailure.None, why);
                    hire.style.marginTop = 10; hire.style.marginBottom = 0;
                    panel.Add(hire);
                }
                body.Add(panel);
            }
            Footer(body);
        }

        /// <summary>GR-13's helper card: Job (duty, what they are doing, their job's settings) and Training.</summary>
        void FillHelper(string id)
        {
            var shop = s.Shop;
            var def = GrowthContent.Helper(id);
            var h = shop.Helper(id);
            if (def == null || h == null)
            {
                var none = Head(def?.Name ?? "Helper", "not hired");
                none.Add(L("Hire them at the staff board, on the shop's north wall.", "gr-hint"));
                Footer(none);
                return;
            }
            var body = Head(def.Name + ", " + def.Job, "", "Job", Duties(id) + " Hired and trained at night; paid a wage for a night and the day after it.");
            LiveStatus();
            body.Add(L(GrowthShop.LevelText(id, h.Level) + ".", "gr-strong"));
            int wage = def.Wages[h.Level - 1];
            bool paid = h.PaidPeriod == shop.Period;
            bool on = h.OnDuty;
            var duty = Item(on ? "On duty" : "Home", on ? wage + " credits a night and day, paid when the night starts" : paid ? "Tonight is paid: calling " + def.Name + " back costs nothing" : "No wage; calling " + def.Name + " back pays tonight's " + wage + " credits",
                Button(on ? "Send home" : paid ? "Call back" : "Call back for " + wage + " cr", () => s.Act(() => shop.SetDuty(id, !on)), !on));
            duty.style.marginTop = 10;
            body.Add(duty);
            if (id == "pip")
                body.Add(L("Pip takes the front of the queue. " + (shop.SecondTill ? "At the second till you serve beside Pip: while you hold the counter the front of the queue is yours, and Pip takes the visitor behind." : "You serve only at the counter's second till (its level 4) while Pip is at work."), "gr-hint"));
            else if (id == "nori") { var targets = Box("gr-section"); targets.style.marginTop = 10; TargetsSection(targets, "No targets yet: Nori fills the shop's tanks with whatever is waiting. Set a target to keep that many of a species on show."); body.Add(targets); }
            else MossSection(body, h);
            // Training: their levels on a track, the next one with its price.
            var train = AddTab("Training");
            train.Add(L(def.Name + " · level " + h.Level + " of 3", "gr-upgrade__head"));
            train.Add(Track(3, h.Level, h.Level < 3 ? h.Level + 1 : -1, n => n + ". " + GrowthShop.LevelText(id, n) + " · " + def.Wages[n - 1] + " credits a night and day" + (n > 1 ? " · training " + def.TrainCosts[n - 2] + " cr" : "")));
            if (h.Level >= 3) train.Add(L("Highest level.", "gr-hint"));
            else
            {
                var failure = shop.TrainCheck(id, out string why);
                int cost = def.TrainCosts[h.Level - 1];
                var chips = Gates(0, 0, cost, 0, true);
                LockChip(chips, failure, why);
                train.Add(Benefit("Level " + (h.Level + 1), GrowthShop.LevelText(id, h.Level + 1), "The wage rises to " + def.Wages[h.Level] + " from the next night.", chips));
                train.Add(Wide("Train to level " + (h.Level + 1) + " for " + cost + " cr", () => s.Act(() => shop.Train(id)), true, failure == GrowthFailure.None,
                    failure == GrowthFailure.NotEnoughCredits ? cost - shop.Credits + " more credits" : why));
            }
            Footer(body);
        }

        /// <summary>The stock targets (GR-08): how many of each species the shop's tanks should show; Nori's from era 3, the sorter's too.</summary>
        void TargetsSection(VisualElement body, string empty)
        {
            var shop = s.Shop;
            body.Add(L("Stock targets", "gr-label"));
            var known = GrowthContent.Species.Where(x => shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            var summary = L(shop.StockTargets.Count == 0 ? empty : "Below its target, a species goes to the shop's tanks; the rest to the racks.", "gr-hint");
            body.Add(summary);
            if (known.Count == 0) return;
            // GR-31 (F35): a table of every creature found, those with a target first.
            var head = Box("gr-target", "gr-target--head");
            head.Add(L("Creature", "gr-target__name")); head.Add(L("In shop", "gr-target__num")); head.Add(L("In racks", "gr-target__num")); head.Add(L("Target", "gr-target__set"));
            body.Add(head);
            foreach (var sp in known.OrderByDescending(x => shop.Target(x) > 0))
            {
                string pick = sp;
                int target = shop.Target(pick);
                var row = Box("gr-target");
                row.Add(L(GrowthContent.Get(pick).Name, "gr-target__name"));
                row.Add(L(shop.Shown(pick).ToString(), "gr-target__num"));
                row.Add(L(shop.Racked(pick).ToString(), "gr-target__num"));
                var set = Box("gr-target__set");
                set.Add(PlusMinus("−", () => s.Act(() => shop.SetTarget(pick, target - 1)), target > 0, "No target to lower"));
                set.Add(L(target.ToString(), "gr-target__value"));
                set.Add(PlusMinus("+", () => s.Act(() => shop.SetTarget(pick, target + 1)), target < GrowthShop.MostTarget, "The most is " + GrowthShop.MostTarget));
                row.Add(set);
                body.Add(row);
            }
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
            var recipe = Box("gr-section"); recipe.style.marginTop = 10; body.Add(recipe);
            recipe.Add(L("Recipe", "gr-label"));
            var founders = GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder && shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            recipe.Add(L(shop.MossRecipe == null ? "No recipe chosen yet." : "Running a " + GrowthContent.Get(shop.MossRecipe).Name + ": " + RecipeText(shop.MossRecipe) + ". " + shop.MossBatches + " batch" + (shop.MossBatches == 1 ? "" : "es") + " so far.", "gr-hint"));
            if (founders.Count == 0) recipe.Add(L("No founder made yet: Moss runs only recipes you have made.", "gr-hint"));
            else
            {
                if (mossRecipePick == null || !founders.Contains(mossRecipePick)) mossRecipePick = shop.MossRecipe ?? founders[0];
                string pick = mossRecipePick;
                var row = Box("gr-pair"); row.style.marginTop = 8;
                row.Add(Picker(founders, pick, v => mossRecipePick = v, RecipeText));
                var failure = shop.MossRecipeCheck(pick, out string why);
                bool chosen = pick == shop.MossRecipe;
                var run = Button(chosen ? "Running" : "Run it", () => s.Act(() => shop.SetMossRecipe(pick)), true, !chosen && failure == GrowthFailure.None, chosen ? null : why);
                run.style.marginLeft = 10;
                row.Add(run);
                recipe.Add(row);
            }
            var pair = Box("gr-section"); body.Add(pair);
            pair.Add(L("Pair", "gr-label"));
            if (h.Level < 2) { pair.Add(L("At level 2, Moss keeps a pair you have bred breeding.", "gr-hint")); return; }
            pair.Add(L(shop.MossPairA == null ? "No pair chosen yet." : "Keeping a " + GrowthContent.Get(shop.MossPairA).Name + " and a " + GrowthContent.Get(shop.MossPairB).Name + " breeding.", "gr-hint"));
            var log = shop.PairLog;
            if (log.Count == 0) pair.Add(L("No pair bred yet: Moss keeps only pairs you have bred.", "gr-hint"));
            else
            {
                if (mossPairPick < 0 || mossPairPick >= log.Count)
                {
                    int now = shop.MossPairA == null ? -1 : log.FindIndex(p => p == shop.PairEntry(shop.MossPairA, shop.MossPairB));
                    mossPairPick = now < 0 ? 0 : now;
                }
                int at = mossPairPick;
                var entry = log[at];
                var row = Box("gr-pair"); row.style.marginTop = 8;
                var picker = Box("gr-picker");
                picker.Add(PlusMinus("◀", () => mossPairPick = (at + log.Count - 1) % log.Count, log.Count > 1, "Only one pair bred"));
                var words = Box("gr-picker__words");
                words.Add(L(GrowthContent.Get(entry.A).Name + " and " + GrowthContent.Get(entry.B).Name + " make a " + GrowthContent.Get(entry.Species).Name, "gr-tile__name"));
                picker.Add(words);
                picker.Add(PlusMinus("▶", () => mossPairPick = (at + 1) % log.Count, log.Count > 1, "Only one pair bred"));
                row.Add(picker);
                bool chosen = shop.MossPairA != null && shop.PairEntry(shop.MossPairA, shop.MossPairB) == entry;
                var keep = Button(chosen ? "Breeding" : "Keep them breeding", () => s.Act(() => shop.SetMossPair(entry.A, entry.B)), true, !chosen);
                keep.style.marginLeft = 10;
                row.Add(keep);
                pair.Add(row);
            }
            var job = Box("gr-section"); body.Add(job);
            job.Add(L("Job", "gr-label"));
            if (h.Level >= 3) { job.Add(L("At level 3 Moss does both, in turn.", "gr-hint")); return; }
            job.Add(L("At level 2 Moss runs one of the two jobs; at level 3 both.", "gr-hint"));
            var jobs = Box("gr-row");
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
