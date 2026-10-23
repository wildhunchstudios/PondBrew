using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-07's cards (GR.13): the lab annex, the generator, the power board (supply against draw and the order machines are
    /// served in), a buffer and its pipes, a grower's tap and pump, and the incubator's auto-doser. Pipes are connected from
    /// these cards: a button starts pipe mode at one end, and E at the other lays it.
    /// </summary>
    public sealed partial class GrowthUi
    {
        void FillAnnex()
        {
            var shop = s.Shop;
            var annex = GrowthShop.AnnexDef;
            var body = Head("Lab annex", shop.Annex ? "open" : annex.Credits + " credits");
            body.Add(Text("Open the back room's north wall into the lab annex: room for the generator, the power board and culture buffers, where pipes bring culture from the growers."));
            body.Add(Text("Needs era 3 (3 stars, a hybrid bred and a Large tank), 3 stars and " + annex.Credits + " credits, at night.", "es-text--muted"));
            var failure = shop.AnnexCheck(out string why);
            if (!shop.Annex) body.Add(Button(failure == GrowthFailure.None ? "Open the lab annex for " + annex.Credits + " cr" : why, () => { if (s.Act(shop.BuyAnnex).Success) s.SetCard(GrowthCard.None); }, true, failure == GrowthFailure.None));
            Footer(body);
        }

        void FillGenerator()
        {
            var shop = s.Shop;
            var body = Head("Generator", shop.HasGenerator ? "level " + shop.Level("generator") : "not bought");
            Live(body);
            body.Add(Text(!shop.HasGenerator ? "The generator powers electric pumps and the incubator's auto-doser. Its hand crank gives a unit of power while you crank it; the bubble turbine gives 3 by itself." :
                shop.HandCrank ? "Hold the left mouse on the crank: 1 unit of power flows while you crank, for the machines first on the power board." :
                "The bubble turbine gives " + shop.GeneratorUnits.ToString("0") + " units by itself, day and night."));
            UpgradeSection(body, "generator", "The generator");
            Footer(body);
        }

        void FillPowerBoard()
        {
            var shop = s.Shop;
            var body = Head("Power board", shop.HasGenerator ? "generator level " + shop.Level("generator") : "no generator");
            Live(body);
            body.Add(Text("Machines are served in this order. One short of power runs slower and says so; nothing stops without a reason.", "es-text--muted"));
            if (shop.PowerOrder.Count == 0) body.Add(Text(shop.HasPumps ? "Nothing to power yet: pipe a grower's tap to a buffer." : "Nothing to power yet: buy pumps and pipes below, then pipe a grower's tap to a buffer."));
            for (int i = 0; i < shop.PowerOrder.Count; i++)
            {
                string id = shop.PowerOrder[i];
                var use = shop.PowerUse(id);
                bool machine = id == GrowthShop.DoserPower || id == GrowthShop.SorterPower;
                string status = id == GrowthShop.DoserPower ? shop.AutoStatus : id == GrowthShop.SorterPower ? shop.SorterStatus : shop.LineStatus(shop.Pipe(int.Parse(id.Substring(5))));
                bool electric = machine || shop.ElectricPumps;
                var row = Row();
                var label = Text((i + 1) + ". " + shop.PowerName(id) + (electric ? " · draws " + (id == GrowthShop.DoserPower ? GrowthContent.AutoDoserUnits : id == GrowthShop.SorterPower ? (int)shop.SorterUnits : shop.PumpUnits) : " · cranked by hand") + " · " + status);
                // A machine short of power says so in the warning colour (never by colour alone: its words say it too).
                if (use != null && use.Draw > 0 && use.Got < use.Draw - 0.001f) label.style.color = new Color(0.62f, 0.25f, 0.12f);
                label.style.flexGrow = 1; label.style.flexShrink = 1; label.style.width = 360;
                row.Add(label);
                row.Add(Button("Up", () => s.Act(() => shop.MovePower(id, -1)), false, i > 0));
                row.Add(Button("Down", () => s.Act(() => shop.MovePower(id, 1)), false, i < shop.PowerOrder.Count - 1));
                body.Add(row);
            }
            if (shop.Pipes.Count > 0) body.Add(Text("Pipes: " + shop.Pipes.Count + " laid, " + shop.Pipes.Sum(p => p.Length).ToString("0.#") + " m in all.", "es-text--muted"));
            UpgradeSection(body, "pump", "Pumps and pipes");
            Footer(body, "Pipes start on a grower's or a buffer's card; the chute on the sorter's.");
        }

        void FillBuffer(string target)
        {
            var shop = s.Shop;
            int place = int.Parse(target);
            var b = shop.BufferAt(place);
            if (b == null)
            {
                var body0 = Head("Buffer place " + (place + 1), GrowthContent.BufferCredits + " credits");
                body0.Add(Text("A culture buffer holds " + GrowthContent.BufferCapacity + " doses of one culture, brought by a pipe from a grower's tap. The siphon collects from it as from a grower; the incubator's auto-doser draws from it."));
                var failure = shop.BufferCheck(place, out string why);
                body0.Add(Button(failure == GrowthFailure.None ? "Place a buffer for " + GrowthContent.BufferCredits + " cr" : why, () => s.Act(() => shop.BuyBuffer(place)), true, failure == GrowthFailure.None));
                Footer(body0);
                return;
            }
            var body = Head(shop.BufferName(place), b.Culture == null ? "empty" : GrowthShop.Name(b.Culture));
            Live(body);
            body.Add(Text(b.Culture == null ? "Empty: pipe a grower's tap to it." : "Holds " + Mathf.FloorToInt(b.Units) + " of " + GrowthContent.BufferCapacity + " doses of " + GrowthShop.Name(b.Culture) + ". Hold the left mouse on it to collect with the siphon."));
            bool night = shop.Phase == GrowthPhase.Night;
            var inlet = shop.InletOf(place);
            if (inlet != null)
            {
                var row = Row();
                var label = Text("In: from " + shop.EndName(inlet.From) + " (" + inlet.Length.ToString("0.#") + " m) · " + shop.LineStatus(inlet)); label.style.flexGrow = 1; label.style.width = 300; row.Add(label);
                row.Add(Button("Take it down", () => s.Act(() => shop.RemovePipe(inlet.Id)), false, night, night ? null : "Pipes are taken down at night"));
                body.Add(row);
            }
            else body.Add(Button("Pipe a grower's tap to it", () => s.StartPipe(GrowthLab.Buffer(place)), true, night, night ? null : "Pipes are laid at night"));
            if (shop.AutoDoser)
            {
                var outlet = shop.OutletOf(place);
                if (outlet != null)
                {
                    var row = Row();
                    var label = Text("Out: to the incubator's port (" + outlet.Length.ToString("0.#") + " m)"); label.style.flexGrow = 1; label.style.width = 300; row.Add(label);
                    row.Add(Button("Take it down", () => s.Act(() => shop.RemovePipe(outlet.Id)), false, night, night ? null : "Pipes are taken down at night"));
                    body.Add(row);
                }
                else body.Add(Button("Pipe it to the incubator's port", () => s.StartPipe(GrowthLab.Buffer(place)), false, night && inlet != null, inlet == null ? "Pipe a grower into it first" : night ? null : "Pipes are laid at night"));
            }
            else body.Add(Text("The incubator's level 4 has a port: a buffer piped to it feeds the auto-doser.", "es-text--muted"));
            var sell = shop.SellBufferCheck(place, out string sellWhy);
            body.Add(Button(sell == GrowthFailure.None ? "Sell the buffer for " + GrowthContent.BufferCredits / 2 + " cr" : "Sell: " + sellWhy, () => { if (s.Act(() => shop.SellBuffer(place)).Success) s.SetCard(GrowthCard.None); }, false, sell == GrowthFailure.None));
            Footer(body);
        }

        /// <summary>A grower's tap and pump (GR-07) on its card: its pipes with their status, and a button to pipe it on.</summary>
        void TapSection(VisualElement body, string culture)
        {
            var shop = s.Shop;
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            var head = Text("Tap and pump"); head.style.unityFontStyleAndWeight = FontStyle.Bold; section.Add(head);
            if (shop.Taps < 1) { section.Add(Text("Growers' level 3 gives each grower a tap for a pipe to a buffer in the lab annex.", "es-text--muted")); body.Add(section); return; }
            bool night = shop.Phase == GrowthPhase.Night;
            var lines = shop.Lines.Where(p => p.Culture == culture).ToList();
            foreach (var p in lines)
            {
                var row = Row();
                var label = Text("To " + shop.EndName(p.To) + " (" + p.Length.ToString("0.#") + " m) · " + shop.LineStatus(p)); label.style.flexGrow = 1; label.style.width = 330; row.Add(label);
                row.Add(Button("Take it down", () => s.Act(() => shop.RemovePipe(p.Id)), false, night, night ? null : "Pipes are taken down at night"));
                section.Add(row);
            }
            if (lines.Count > 0) section.Add(Text(shop.HandPumps ? "Its hand pump is on the cabinet's front: hold the left mouse on it to crank, a dose every " + shop.PumpDoseSeconds.ToString("0.0#") + " s." :
                "Its electric pump runs by itself on the generator's power: a dose every " + shop.PumpDoseSeconds.ToString("0.0#") + " s, drawing " + shop.PumpUnits + " unit" + (shop.PumpUnits == 1 ? "" : "s") + ".", "es-text--muted"));
            if (lines.Count < shop.Taps)
            {
                string why = !shop.Annex ? "Open the lab annex first" : !shop.HasPumps ? "Buy pumps and pipes on the power board first" : !night ? "Pipes are laid at night" : null;
                section.Add(Button(why ?? "Pipe its tap to a buffer", () => s.StartPipe(GrowthLab.Tap(culture)), true, why == null));
            }
            body.Add(section);
        }

        /// <summary>The incubator's auto-doser (its level 4 port, GR-07): the recipe it doses, on or off, its status and the port's pipes.</summary>
        void AutoDoserSection(VisualElement body)
        {
            var shop = s.Shop;
            if (!shop.AutoDoser) return;
            var section = new VisualElement(); section.style.marginTop = 12; section.style.borderTopWidth = 2; section.style.borderTopColor = new Color(0.79f, 0.72f, 0.56f); section.style.paddingTop = 8;
            var head = Text("Auto-doser · " + (shop.AutoOn ? "on" : "off")); head.style.unityFontStyleAndWeight = FontStyle.Bold; section.Add(head);
            section.Add(Text(shop.AutoStatus, shop.AutoOn && shop.AutoStatus.StartsWith("Waiting") ? "es-card__reason" : "es-text"));
            section.Add(Text("It doses a recipe you already know from the buffers piped to the port, then incubates it; it never tries something new." +
                (shop.HasSorter ? " By stock targets, each batch is the known recipe furthest below its target on the sorter's card." : ""), "es-text--muted"));
            var recipes = Row();
            if (shop.HasSorter) recipes.Add(Button((shop.AutoByTargets ? "✓ " : "") + "By stock targets", () => s.Act(shop.SetAutoByTargets), shop.AutoByTargets, !shop.AutoBatch && !shop.AutoByTargets));
            foreach (var sp in GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder && shop.Discovered.Contains(x.Id) && x.Doses <= shop.MaxDoses))
            {
                string id = sp.Id;
                bool chosen = shop.AutoRecipe == id && !shop.AutoByTargets;
                var b = Button((chosen ? "✓ " : "") + sp.Name, () => s.Act(() => shop.SetAutoRecipe(id)), chosen, !shop.AutoBatch);
                b.style.borderLeftWidth = 8; b.style.borderLeftColor = CultureColour(GrowthContent.Cultures[System.Array.FindIndex(sp.Recipe, n => n > 0)].Id);
                recipes.Add(b);
            }
            section.Add(recipes);
            section.Add(Button(shop.AutoOn ? "Switch it off" : "Switch it on", () => s.Act(() => shop.SwitchAutoDoser(!shop.AutoOn)), !shop.AutoOn, shop.AutoOn || shop.AutoRecipe != null || shop.AutoByTargets));
            var port = shop.PortBuffers.ToList();
            section.Add(Text(port.Count == 0 ? "No buffer is piped to the port yet." : "Piped to the port: " + string.Join(", ", port.Select(b => GrowthShop.Name(b.Culture) + " (" + shop.BufferName(b.Place).ToLowerInvariant() + ", " + Mathf.FloorToInt(b.Units) + ")")) + ".", "es-text--muted"));
            bool night = shop.Phase == GrowthPhase.Night;
            section.Add(Button(night ? "Pipe a buffer to the port" : "Pipes are laid at night", () => s.StartPipe(GrowthLab.Port), false, night));
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
