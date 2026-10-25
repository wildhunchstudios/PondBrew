using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-07's cards (GR.13) in GR-13's style: the lab annex, the generator, the power board (supply against draw as one
    /// bar, each machine a row with its draw and a status pill, in the order they are served), a buffer and its pipes, a
    /// grower's tap and pump, and the incubator's auto-doser. Pipes are connected from these cards: a button starts pipe
    /// mode at one end, and E at the other lays it.
    /// </summary>
    public sealed partial class GrowthUi
    {
        void FillAnnex()
        {
            var shop = s.Shop;
            var annex = GrowthShop.AnnexDef;
            var body = Head("Lab annex", shop.Annex ? "open" : annex.Credits + " credits", null, "Behind the back room's north wall. Era 3 comes with 3 stars, a hybrid bred and a Large tank. Walls open at night.");
            if (shop.Annex) { body.Add(L("Open: the generator, the power board and culture buffers, where pipes bring culture from the growers.", "gr-hint")); Footer(body); return; }
            var failure = shop.AnnexCheck(out string why);
            BuyPanel(body, "The back room's north wall", "The lab annex", "Room for the generator, the power board and culture buffers, where pipes bring culture from the growers.",
                Gates(annex.Era, annex.Stars, annex.Credits, 0, true), failure, why, "Open the lab annex for " + annex.Credits + " cr", shop.BuyAnnex);
            Footer(body);
        }

        void FillGenerator()
        {
            var shop = s.Shop;
            const string rules = "The generator powers electric pumps, the incubator's auto-doser and the sorter. Its hand crank gives a unit of power while you hold the left mouse on it; the bubble turbine gives 3 by itself, and the workshop's engines more.";
            if (!shop.HasGenerator)
            {
                var intro = Head("Generator", "not bought", null, rules);
                UpgradeSection(intro, "generator", "The generator");
                Footer(intro);
                return;
            }
            var body = Head("Generator", "", "Power", rules);
            LiveStatus();
            body.Add(Stat(shop.GeneratorUnits.ToString("0") + (shop.GeneratorUnits == 1 ? " unit" : " units"), shop.HandCrank ? "while you crank it" : "by itself, day and night"));
            if (shop.HandCrank) body.Add(L("Hold the left mouse on the crank: the power goes to the machines first on the power board.", "gr-hint"));
            UpgradeSection(body, "generator", "The generator");
            Footer(body);
        }

        /// <summary>GR-13's power board: supply against draw as one bar; each machine a row with its draw, a status pill and its place in the order.</summary>
        void FillPowerBoard()
        {
            var shop = s.Shop;
            var body = Head("Power board", "", "Board", "Machines are served in this order: one short of power runs slower and says so; nothing stops without a reason. Pipes start on a grower's or a buffer's card; the chute on the sorter's.");
            LiveStatus();
            float supply = shop.Supply, draw = shop.Draw, scale = Mathf.Max(1f, supply, draw);
            var head = Box("gr-row", "gr-row--spread");
            head.Add(Eyebrow("Supply · asked for"));
            head.Add(L(supply.ToString("0.#") + " supplied · " + (draw > supply ? (draw - supply).ToString("0.#") + " short" : (supply - draw).ToString("0.#") + " spare"), "gr-hint"));
            body.Add(head);
            var bar = Bar(Mathf.Min(supply, draw) / scale, new Color(0.13f, 0.37f, 0.35f), Mathf.Abs(draw - supply) / scale, draw > supply ? new Color(0.84f, 0.38f, 0.27f) : new Color(0.98f, 0.78f, 0.37f));
            bar.style.marginBottom = 16;
            body.Add(bar);
            if (shop.PowerOrder.Count == 0) body.Add(L(shop.HasPumps ? "Nothing to power yet: pipe a grower's tap to a buffer." : "Nothing to power yet: buy pumps and pipes on the Upgrade tab, then pipe a grower's tap to a buffer.", "gr-hint"));
            for (int i = 0; i < shop.PowerOrder.Count; i++)
            {
                string id = shop.PowerOrder[i];
                var use = shop.PowerUse(id);
                bool machine = id == GrowthShop.DoserPower || id == GrowthShop.SorterPower;
                string status = id == GrowthShop.DoserPower ? shop.AutoStatus : id == GrowthShop.SorterPower ? shop.SorterStatus : shop.LineStatus(shop.Pipe(int.Parse(id.Substring(5))));
                bool electric = machine || shop.ElectricPumps;
                int units = id == GrowthShop.DoserPower ? GrowthContent.AutoDoserUnits : id == GrowthShop.SorterPower ? (int)shop.SorterUnits : shop.PumpUnits;
                // A machine short of power says so in words as well as its pill's colour.
                int tone = use != null && use.Draw > 0 && use.Got < use.Draw - 0.001f ? (use.Got > 0.001f ? 1 : 2) : status.StartsWith("Waiting") ? 2 : 0;
                int index = i;
                var row = Item((i + 1) + ". " + shop.PowerName(id), electric ? "draws " + units + (units == 1 ? " unit" : " units") : "cranked by hand",
                    Pill(status, tone), ChipButton("Up", () => s.Act(() => shop.MovePower(id, -1)), index > 0, "First already"), ChipButton("Down", () => s.Act(() => shop.MovePower(id, 1)), index < shop.PowerOrder.Count - 1, "Last already"));
                row.tooltip = (i + 1) + ". " + shop.PowerName(id) + (electric ? " · draws " + units : " · cranked by hand") + " · " + status;
                body.Add(row);
            }
            if (shop.Pipes.Count > 0) body.Add(L("Pipes: " + shop.Pipes.Count + " laid, " + shop.Pipes.Sum(p => p.Length).ToString("0.#") + " m in all.", "gr-hint"));
            UpgradeSection(body, "pump", "Pumps and pipes");
            Footer(body);
        }

        void FillBuffer(string target)
        {
            var shop = s.Shop;
            int place = int.Parse(target);
            var b = shop.BufferAt(place);
            const string rules = "A culture buffer holds doses of one culture, brought by a pipe from a grower's tap. The siphon collects from it as from a grower; the incubator's auto-doser draws from it through the port (incubator level 4).";
            if (b == null)
            {
                var body0 = Head("Buffer place " + (place + 1), GrowthContent.BufferCredits + " credits", null, rules);
                var failure = shop.BufferCheck(place, out string why);
                BuyPanel(body0, "A culture buffer", "Holds " + GrowthContent.BufferCapacity + " doses of one culture", "Piped from a grower's tap; collected like a grower.", Gates(0, 0, GrowthContent.BufferCredits, 0, true), failure, why,
                    "Place a buffer for " + GrowthContent.BufferCredits + " cr", () => shop.BuyBuffer(place), false);
                Footer(body0);
                return;
            }
            var body = Head(shop.BufferName(place), "", "Buffer", rules);
            LiveStatus();
            var fill = Box("gr-section");
            fill.Add(Bar(b.Units / GrowthContent.BufferCapacity, b.Culture == null ? new Color(0.8f, 0.75f, 0.6f) : CultureColour(b.Culture)));
            var chips = Box("gr-chips");
            chips.Add(Chip(b.Culture == null ? "Empty" : GrowthShop.Name(b.Culture)));
            chips.Add(Chip("Holds " + GrowthContent.BufferCapacity));
            fill.Add(chips);
            body.Add(fill);
            bool night = shop.Phase == GrowthPhase.Night;
            body.Add(Eyebrow("Pipes"));
            var inlet = shop.InletOf(place);
            if (inlet != null) body.Add(PipeItem("In: from " + shop.EndName(inlet.From), inlet.Length.ToString("0.#") + " m · " + shop.LineStatus(inlet), inlet.Id));
            else body.Add(Wide("Pipe a grower's tap to it", () => s.StartPipe(GrowthLab.Buffer(place)), true, night, "Pipes are laid at night"));
            if (shop.AutoDoser)
            {
                var outlet = shop.OutletOf(place);
                if (outlet != null) body.Add(PipeItem("Out: to the incubator's port", outlet.Length.ToString("0.#") + " m", outlet.Id));
                else body.Add(Wide("Pipe it to the incubator's port", () => s.StartPipe(GrowthLab.Buffer(place)), false, night && inlet != null, inlet == null ? "Pipe a grower into it first" : "Pipes are laid at night"));
            }
            else body.Add(L("The incubator's level 4 has a port: a buffer piped to it feeds the auto-doser.", "gr-hint"));
            var sell = shop.SellBufferCheck(place, out string sellWhy);
            if (sell == GrowthFailure.None && sellAsked)
            {
                var ask = Box("gr-row");
                ask.Add(L("Sell it for " + GrowthContent.BufferCredits / 2 + " cr?", "gr-hint"));
                var yes = Button("Sell", () => { sellAsked = false; if (s.Act(() => shop.SellBuffer(place)).Success) s.SetCard(GrowthCard.None); });
                yes.RemoveFromClassList("es-button--secondary"); yes.AddToClassList("es-button--danger"); yes.style.marginLeft = 10;
                ask.Add(yes);
                ask.Add(Button("Keep it", () => sellAsked = false));
                body.Add(ask);
            }
            else
            {
                var sellButton = Button("Sell the buffer · +" + GrowthContent.BufferCredits / 2 + " cr", () => sellAsked = true, false, sell == GrowthFailure.None, sellWhy);
                sellButton.RemoveFromClassList("es-button--secondary"); sellButton.AddToClassList("es-button--danger");
                body.Add(sellButton);
            }
            Footer(body);
        }

        /// <summary>A grower's tap and pump (GR-07), on its card's Pipes tab: its pipes with their status, and a button to pipe it on.</summary>
        void TapSection(VisualElement body, string culture)
        {
            var shop = s.Shop;
            body.Add(L("Tap and pump", "gr-label"));
            bool night = shop.Phase == GrowthPhase.Night;
            var lines = shop.Lines.Where(p => p.Culture == culture).ToList();
            foreach (var p in lines) body.Add(PipeItem("To " + shop.EndName(p.To), p.Length.ToString("0.#") + " m · " + shop.LineStatus(p), p.Id));
            if (lines.Count > 0) body.Add(L(shop.HandPumps ? "Its hand pump is on the cabinet's front: hold the left mouse on it to crank, a dose every " + shop.PumpDoseSeconds.ToString("0.0#") + " s." :
                "Its electric pump runs by itself on the generator's power: a dose every " + shop.PumpDoseSeconds.ToString("0.0#") + " s, drawing " + shop.PumpUnits + " unit" + (shop.PumpUnits == 1 ? "" : "s") + ".", "gr-hint"));
            if (lines.Count < shop.Taps)
            {
                string why = !shop.Annex ? "Open the lab annex first" : !shop.HasPumps ? "Buy pumps and pipes on the power board first" : !night ? "Pipes are laid at night" : null;
                var pipe = Wide("Pipe its tap to a buffer", () => s.StartPipe(GrowthLab.Tap(culture)), true, why == null, why);
                pipe.style.marginTop = 10;
                body.Add(pipe);
            }
        }

        /// <summary>The incubator's auto-doser (its level 4 port, GR-07), on its Machines tab: the recipe it doses, on or off, its status and the port's pipes.</summary>
        void AutoDoserSection(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.AutoDoser) return;
            var section = Box("gr-section");
            var head = Box("gr-row", "gr-row--spread");
            head.Add(L("Auto-doser · " + (shop.AutoOn ? "on" : "off"), "gr-label"));
            head.Add(Pill(shop.AutoStatus, shop.AutoOn && shop.AutoStatus.StartsWith("Waiting") ? 2 : shop.AutoOn ? 0 : 1));
            section.Add(head);
            section.tooltip = "It doses a recipe you already know from the buffers piped to the port, then incubates it; it never tries something new." +
                (shop.HasSorter ? " By stock targets, each batch is the known recipe furthest below its target on the sorter's card." : "");
            var recipes = Box("gr-row");
            if (shop.HasSorter) recipes.Add(ChipButton((shop.AutoByTargets ? "✓ " : "") + "By stock targets", () => s.Act(shop.SetAutoByTargets), !shop.AutoBatch && !shop.AutoByTargets, shop.AutoByTargets ? "Chosen" : "It is dosing"));
            foreach (var sp in GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder && shop.Discovered.Contains(x.Id) && x.Doses <= shop.MaxDoses))
            {
                string id = sp.Id;
                bool chosen = shop.AutoRecipe == id && !shop.AutoByTargets;
                recipes.Add(ChipButton((chosen ? "✓ " : "") + sp.Name, () => s.Act(() => shop.SetAutoRecipe(id)), !shop.AutoBatch && !chosen, chosen ? "Chosen" : "It is dosing",
                    CultureColour(GrowthContent.Cultures[System.Array.FindIndex(sp.Recipe, n => n > 0)].Id)));
            }
            section.Add(recipes);
            var port = shop.PortBuffers.ToList();
            section.Add(L(port.Count == 0 ? "No buffer is piped to the port yet." : "Piped to the port: " + string.Join(", ", port.Select(b => GrowthShop.Name(b.Culture) + " (" + shop.BufferName(b.Place).ToLowerInvariant() + ", " + Mathf.FloorToInt(b.Units) + ")")) + ".", "gr-hint"));
            var actions = Box("gr-row");
            actions.Add(Button(shop.AutoOn ? "Switch it off" : "Switch it on", () => s.Act(() => shop.SwitchAutoDoser(!shop.AutoOn)), !shop.AutoOn, shop.AutoOn || shop.AutoRecipe != null || shop.AutoByTargets, "Choose a recipe first"));
            bool night = shop.Phase == GrowthPhase.Night;
            actions.Add(Button("Pipe a buffer to the port", () => s.StartPipe(GrowthLab.Port), false, night, "Pipes are laid at night"));
            section.Add(actions);
            body.Add(section);
        }

        string PowerSignature()
        {
            var shop = s.Shop;
            return shop.Annex + "|" + string.Join(",", shop.Buffers.Select(b => b.Place + (b.Culture ?? "") + Mathf.FloorToInt(b.Units))) + "|" + string.Join(",", shop.Pipes.Select(p => p.Id)) + "|" + string.Join(",", shop.PowerOrder) +
                "|" + string.Join(",", shop.PowerUses.Select(u => u.Status)) + "|" + shop.AutoRecipe + shop.AutoOn + shop.AutoBatch + shop.AutoBatches + "|" + shop.HandCrank + Mathf.RoundToInt(shop.Supply);
        }

        string PowerLiveText()
        {
            var shop = s.Shop;
            switch (s.Card)
            {
                case GrowthCard.Generator: return !shop.HasGenerator ? "" : shop.HandCrank ? (shop.Cranking ? "Cranking: 1 unit flowing." : "Idle: crank it for power.") : shop.GeneratorUnits.ToString("0") + " units, day and night.";
                case GrowthCard.PowerBoard: return "Supply " + shop.Supply.ToString("0.#") + " units · asked for " + shop.Draw.ToString("0.#");
                case GrowthCard.Buffer:
                    var b = s.CardTarget == null ? null : shop.BufferAt(int.Parse(s.CardTarget));
                    return b == null ? "" : Mathf.FloorToInt(b.Units) + "/" + GrowthContent.BufferCapacity + " doses";
                default: return null;
            }
        }
    }
}
