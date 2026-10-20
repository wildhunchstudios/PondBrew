using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>A selectable tile: a creature silhouette, a name and where it is. Shared by the lab cards.</summary>
    sealed class CreatureTile
    {
        public Button Button; public CreatureGlyph Glyph; public Label Name, Where;

        public static CreatureTile Make(VisualElement parent, string name, System.Action onClick)
        {
            var tile = new CreatureTile();
            tile.Button = new Button(onClick) { name = name, text = "" };
            tile.Button.AddToClassList("es-button");
            tile.Button.AddToClassList("es-tile");
            parent.Add(tile.Button);
            tile.Glyph = new CreatureGlyph { name = name + "-glyph" };
            tile.Glyph.AddToClassList("es-tile__glyph");
            tile.Button.Add(tile.Glyph);
            tile.Name = new Label { name = name + "-name", pickingMode = PickingMode.Ignore };
            tile.Name.AddToClassList("es-tile__name");
            tile.Button.Add(tile.Name);
            tile.Where = new Label { name = name + "-where", pickingMode = PickingMode.Ignore };
            tile.Where.AddToClassList("es-tile__time");
            tile.Button.Add(tile.Where);
            return tile;
        }

        public void Set(string look, string name, string where, bool selected, bool enabled, bool empty = false, bool clickable = true)
        {
            Button.pickingMode = clickable ? PickingMode.Position : PickingMode.Ignore;
            Button.EnableInClassList("es-tile--static", !clickable);
            Glyph.Set(look ?? "", empty ? 0 : 1);
            Glyph.style.visibility = empty ? Visibility.Hidden : Visibility.Visible;
            if (Name.text != (name ?? "")) Name.text = name ?? "";
            if (Where.text != (where ?? "")) Where.text = where ?? "";
            Button.EnableInClassList("es-tile--selected", selected);
            Button.EnableInClassList("es-tile--empty", empty);
            if (Button.enabledSelf != enabled) Button.SetEnabled(enabled);
        }
    }

    /// <summary>Specimen rack card (UX.5): its compartments and the ready output to store, one Store primary.</summary>
    public sealed class RackCardView : StationCardView
    {
        public const string ViewName = "es-rack";
        public const string CompartmentsName = "es-rack-compartments", SourcesName = "es-rack-sources", SourcesHeadingName = "es-rack-sources-heading";
        public static string CompartmentName(int index) => "es-rack-compartment-" + index;
        public static string SourceName(int index) => "es-rack-source-" + index;

        public readonly RackCardState State = new RackCardState();
        readonly VisualElement body, compartments, sources;
        readonly Label sourcesHeading;
        readonly List<CreatureTile> compartmentTiles = new List<CreatureTile>();
        readonly List<CreatureTile> sourceTiles = new List<CreatureTile>();
        string chosenCompartment;
        int chosenSource;

        /// <summary>The compartment a store goes into now; null when the rack is full.</summary>
        public string Target { get; private set; }
        public int ChosenSource => chosenSource;

        public RackCardView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            body = NewBox(Root, "es-view", "es-rack-body");
            NewLabel(body, "es-rack-compartments-heading", "es-eyebrow", Strings.Get("card.rack.compartments"));
            compartments = NewBox(body, "es-tiles", CompartmentsName);
            sourcesHeading = NewLabel(body, SourcesHeadingName, "es-eyebrow", Strings.Get("card.rack.ready"));
            sources = NewBox(body, "es-tiles", SourcesName);
        }

        public override void Opened() { chosenCompartment = null; chosenSource = 0; }

        public override void Refresh()
        {
            Shop.FillRackCard(State, Strings);
            var s = State;
            Reason = null; ReasonIsWarning = false;
            Shown(body, s.Placed);
            if (!s.Placed)
            {
                Status = null; Line = Strings.Get("card.rack.gone"); PrimaryText = null; Target = null;
                return;
            }
            int free = 0;
            foreach (var compartment in s.Compartments) if (compartment.SpeciesName == null) free++;
            Status = free == 0 ? Strings.Get("card.rack.status.full") : Strings.Format("card.rack.status.free", free);
            Line = Strings.Get("card.rack.line");
            Target = StationCardRules.RackTarget(s.Compartments, chosenCompartment);
            while (compartmentTiles.Count < s.Compartments.Count)
            {
                int index = compartmentTiles.Count;
                compartmentTiles.Add(CreatureTile.Make(compartments, CompartmentName(index), () =>
                {
                    if (index < State.Compartments.Count && State.Compartments[index].SpeciesName == null) chosenCompartment = State.Compartments[index].StorageId;
                }));
            }
            for (int i = 0; i < compartmentTiles.Count; i++)
            {
                bool on = i < s.Compartments.Count;
                Shown(compartmentTiles[i].Button, on);
                if (!on) continue;
                var c = s.Compartments[i];
                bool empty = c.SpeciesName == null;
                string where = c.Reserved ? Strings.Format("card.rack.reserved", c.Where) : c.Where;
                compartmentTiles[i].Set(c.Look, empty ? Strings.Get("card.rack.empty") : c.SpeciesName, where,
                    empty && c.StorageId == Target && s.Sources.Count > 0, true, empty, empty);
            }
            if (chosenSource >= s.Sources.Count) chosenSource = 0;
            while (sourceTiles.Count < s.Sources.Count)
            {
                int index = sourceTiles.Count;
                sourceTiles.Add(CreatureTile.Make(sources, SourceName(index), () => { if (index < State.Sources.Count) chosenSource = index; }));
            }
            for (int i = 0; i < sourceTiles.Count; i++)
            {
                bool on = i < s.Sources.Count;
                Shown(sourceTiles[i].Button, on);
                if (on) sourceTiles[i].Set(s.Sources[i].Look, s.Sources[i].Name, s.Sources[i].Where, i == chosenSource, s.Sources[i].Enabled);
            }
            Shown(sources, s.Sources.Count > 0);
            Shown(sourcesHeading, s.Sources.Count > 0);
            if (s.Sources.Count == 0)
            {
                PrimaryText = Strings.Get("card.rack.storeIdle"); PrimaryEnabled = false; Reason = Strings.Get("card.rack.why.nothing");
            }
            else
            {
                PrimaryText = Strings.Format("card.rack.store", s.Sources[chosenSource].Name);
                PrimaryEnabled = Target != null;
                if (Target == null) Reason = Strings.Get("card.rack.why.full");
            }
        }

        public override void Primary()
        {
            if (Target == null || chosenSource >= State.Sources.Count) return;
            if (Shop.CardStoreInRack(State.Sources[chosenSource], Target)) { chosenCompartment = null; chosenSource = 0; }
        }
    }

    /// <summary>Conditioning vat card (UX.5): stored Pondlets as tiles, Start vat · 12s, a glowing ring, ready output, Mist research.</summary>
    public sealed class VatCardView : StationCardView
    {
        public const string ViewName = "es-vat";
        public const string InputsName = "es-vat-inputs", OutputsName = "es-vat-outputs", RingName = "es-vat-ring", SecondsName = "es-vat-seconds",
            SparkName = "es-vat-spark", CancelName = "es-vat-cancel", ConfirmName = "es-vat-confirm", KeepName = "es-vat-keep", CancelNowName = "es-vat-cancel-now",
            MistName = "es-vat-mist", MistButtonName = "es-vat-research-mist", MistNoteName = "es-vat-mist-note", InputsHeadingName = "es-vat-inputs-heading",
            OutputsNoteName = "es-vat-outputs-note";
        public static string InputName(int index) => "es-vat-input-" + index;
        public static string OutputName(int index) => "es-vat-output-" + index;

        public readonly VatCardState State = new VatCardState();
        readonly VisualElement idle, inputs, glowing, outputs, mist, confirm;
        readonly Label inputsHeading, spark, seconds, mistNote, outputsNote;
        readonly Button cancel, researchMist;
        readonly ProgressRing ring;
        readonly List<CreatureTile> inputTiles = new List<CreatureTile>();
        readonly List<CreatureTile> outputTiles = new List<CreatureTile>();
        bool confirming;

        public bool Confirming => confirming;

        public VatCardView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            // A shop owns one vat (ShopLayout.ConditionerLimit), so its layout number adds nothing and wrapped the strip.
            Title = Strings.Get("card.title.Conditioning");
            idle = NewBox(Root, "es-view", "es-vat-idle");
            inputsHeading = NewLabel(idle, InputsHeadingName, "es-eyebrow", Strings.Get("card.vat.pondlets"));
            inputs = NewBox(idle, "es-tiles", InputsName);
            spark = NewLabel(idle, SparkName, "es-note");

            glowing = NewBox(Root, "es-inc__growing", "es-vat-glowing");
            ring = new ProgressRing { name = RingName };
            glowing.Add(ring);
            var glowText = NewBox(glowing, "es-inc__grow-text");
            seconds = NewLabel(glowText, SecondsName, "es-big-number");
            NewLabel(glowText, "es-vat-glow-note", "es-note", Strings.Get("card.vat.sparkReserved"));

            outputs = NewBox(Root, "es-tiles", OutputsName);
            outputsNote = NewLabel(Root, OutputsNoteName, "es-note");

            var links = NewBox(After, "es-links");
            cancel = NewButton(links, CancelName, "es-link", Strings.Get("card.vat.cancel"), () => confirming = true);
            confirm = NewBox(After, "es-confirm", ConfirmName);
            NewLabel(confirm, "es-vat-confirm-text", "es-confirm__text", Strings.Get("card.vat.confirm"));
            var confirmButtons = NewBox(confirm, "es-confirm__buttons");
            NewButton(confirmButtons, KeepName, "es-button--secondary es-button--compact", Strings.Get("card.vat.keep"), () => confirming = false);
            NewButton(confirmButtons, CancelNowName, "es-button--danger es-button--compact", Strings.Get("card.vat.cancelNow"), () =>
            {
                confirming = false;
                Shop.CardCancelVat();
            });

            mist = NewBox(After, "es-view", MistName);
            NewBox(mist, "es-rule");
            NewLabel(mist, "es-vat-mist-heading", "es-eyebrow", Strings.Get("card.vat.mist"));
            mistNote = NewLabel(mist, MistNoteName, "es-note");
            researchMist = NewButton(mist, MistButtonName, "es-button--secondary es-button--compact es-card__wide", "", () => Shop.CardResearchMist());
        }

        public override void Opened() => confirming = false;

        public override void Refresh()
        {
            Shop.FillVatCard(State, Strings);
            var s = State;
            PrimaryText = null; PrimaryEnabled = false; Reason = null; ReasonIsWarning = false;
            if (s.Mode != VatCardMode.Processing) confirming = false;
            Shown(idle, s.Mode == VatCardMode.Idle);
            Shown(glowing, s.Mode == VatCardMode.Processing);
            Shown(outputs, s.Mode == VatCardMode.Ready);
            // The body line already says where to collect; the note only warns when there's nowhere to put them.
            Shown(outputsNote, s.Mode == VatCardMode.Ready && !s.FreeSpace);
            Shown(cancel, s.Mode == VatCardMode.Processing && !confirming);
            Shown(confirm, confirming);
            Shown(mist, s.Mode != VatCardMode.Missing);
            switch (s.Mode)
            {
                case VatCardMode.Missing:
                    Status = null; Line = Strings.Get("card.vat.gone");
                    return;
                case VatCardMode.Locked:
                    Status = Strings.Get("card.vat.status.idle"); Line = Strings.Get("card.vat.line.locked");
                    PrimaryText = Strings.Format("card.vat.start", Mathf.RoundToInt(s.Duration)); Reason = Strings.Get("card.vat.why.locked");
                    break;
                case VatCardMode.Idle:
                    Status = Strings.Get("card.vat.status.idle"); Line = Strings.Get("card.vat.line.idle");
                    RefreshInputs(s);
                    PrimaryText = Strings.Format("card.vat.start", Mathf.RoundToInt(s.Duration));
                    string why = StationCardRules.VatBlockedKey(s.Inputs.Count, s.SelectedCount, s.SparkInSiphon);
                    PrimaryEnabled = why == null;
                    Reason = why == null ? null : Strings.Get(why);
                    break;
                case VatCardMode.Processing:
                    Status = Strings.Get("card.vat.status.glowing");
                    Line = Strings.Format(s.Outputs.Count == 1 ? "card.vat.line.glowingOne" : "card.vat.line.glowing", s.Outputs.Count);
                    ring.Set(s.Duration > 0 ? 1f - s.SecondsLeft / s.Duration : 0, "lantern");
                    SetText(seconds, Strings.Format("card.inc.seconds", Mathf.CeilToInt(s.SecondsLeft)));
                    break;
                default:
                    Status = Strings.Get("card.vat.status.ready");
                    Line = Strings.Get("card.vat.line.ready");
                    RefreshOutputs(s);
                    SetText(outputsNote, Strings.Get("card.vat.noSpace"));
                    outputsNote.EnableInClassList("es-note--warn", true);
                    break;
            }
            RefreshMist(s);
        }

        void RefreshInputs(VatCardState s)
        {
            while (inputTiles.Count < s.Inputs.Count)
            {
                int index = inputTiles.Count;
                inputTiles.Add(CreatureTile.Make(inputs, InputName(index), () => { if (index < State.Inputs.Count) Shop.CardToggleVatInput(State.Inputs[index]); }));
            }
            string pondlet = Shop.Model.GetSpecies("pondlet")?.DisplayName ?? "Pondlet";
            for (int i = 0; i < inputTiles.Count; i++)
            {
                bool on = i < s.Inputs.Count;
                Shown(inputTiles[i].Button, on);
                if (!on) continue;
                var input = s.Inputs[i];
                // A third Pondlet can't join two chosen ones; a chosen one can always be dropped.
                inputTiles[i].Set("teardrop", pondlet, input.Where, input.Selected, input.Selected || s.SelectedCount < StationCardRules.VatCapacity);
            }
            // With no stored Pondlets the reason line says what to do; no heading over an empty list.
            Shown(inputs, s.Inputs.Count > 0);
            Shown(inputsHeading, s.Inputs.Count > 0);
            bool hasSpark = s.SparkInSiphon > 0;
            // Once a Pondlet is picked, a missing Spark is the reason line's to say; with no Pondlets, Spark doesn't matter yet.
            Shown(spark, s.Inputs.Count > 0 && (hasSpark || s.SelectedCount == 0));
            SetText(spark, hasSpark ? Strings.Format("card.vat.sparkOk", s.SparkInSiphon) : Strings.Get("card.vat.sparkMissing"));
            spark.EnableInClassList("es-note--warn", !hasSpark);
        }

        void RefreshOutputs(VatCardState s)
        {
            while (outputTiles.Count < s.Outputs.Count)
                outputTiles.Add(CreatureTile.Make(outputs, OutputName(outputTiles.Count), null));
            for (int i = 0; i < outputTiles.Count; i++)
            {
                bool on = i < s.Outputs.Count;
                Shown(outputTiles[i].Button, on);
                if (!on) continue;
                var output = s.Outputs[i];
                outputTiles[i].Set(output.Look, output.Name, Strings.Format("card.tank.fromVat", output.Chamber), false, true, false, false);
            }
        }

        void RefreshMist(VatCardState s)
        {
            if (s.MistUnlocked)
            {
                SetText(mistNote, Strings.Get("card.vat.mistUnlocked"));
                Shown(researchMist, false);
                return;
            }
            SetText(researchMist, Strings.Format("card.vat.researchMist", s.MistCost));
            Shown(researchMist, true);
            string why = !s.MistReady ? "card.vat.why.mistLocked" : !s.Night ? "card.bench.why.night" : s.Currency < s.MistCost ? "card.bench.why.coins" : null;
            Enabled(researchMist, why == null);
            SetText(mistNote, why == null ? Strings.Get("card.vat.mistOffer") : Strings.Format(why, s.MistCost - s.Currency));
            mistNote.EnableInClassList("es-note--warn", why != null);
        }

        public override void Primary()
        {
            if (State.Mode == VatCardMode.Idle) Shop.CardStartVat();
        }
    }

    /// <summary>
    /// Research bench card (UX.5): Spark research, the bigger siphon and the collector and doser as rows; the primary is the
    /// next thing to get or switch on (StationCardRules.BenchPrimary); each other row has its own small button.
    /// </summary>
    public sealed class BenchCardView : StationCardView
    {
        public const string ViewName = "es-bench";
        public const string SparkRowName = "es-bench-spark", SiphonRowName = "es-bench-siphon", CollectorRowName = "es-bench-collector", DoserRowName = "es-bench-doser",
            CulturePrepName = "es-bench-culture-prep", ResearchLinkName = "es-bench-research";
        public static string ButtonName(string row) => row + "-button";
        public static string ChipName(string row) => row + "-chip";

        public readonly BenchCardState State = new BenchCardState();
        readonly Row spark, siphon, collector, doser;
        readonly VisualElement stamps;
        readonly List<Label> stampLabels = new List<Label>();

        public BenchAction PrimaryAction { get; private set; }

        sealed class Row { public VisualElement Box; public Label Title, Detail, Chip; public Button Button; }

        public BenchCardView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("card.title.Upgrade");
            spark = NewRow(SparkRowName, "card.bench.spark", () => Shop.CardResearchSpark());
            stamps = NewBox(null, "es-reqs", "es-bench-stamps");
            spark.Detail.parent.Add(stamps);
            siphon = NewRow(SiphonRowName, "card.bench.siphon", () => Shop.CardBuySiphon());
            collector = NewRow(CollectorRowName, "card.bench.collector", () => ModuleRowPressed(true));
            doser = NewRow(DoserRowName, "card.bench.doser", () => ModuleRowPressed(false));
            var links = NewBox(After, "es-links");
            NewButton(links, CulturePrepName, "es-link", Strings.Get("card.bench.culturePrep"), () => Shop.CardCulturePrep());
            // UX-06: the research path, a planning page of its own.
            NewButton(links, ResearchLinkName, "es-link", Strings.Get("card.bench.research"), () => Shop.OpenResearchView());
        }

        Row NewRow(string name, string titleKey, System.Action onClick)
        {
            var row = new Row();
            row.Box = NewBox(Root, "es-bench-row", name);
            var text = NewBox(row.Box, "es-bench-row__text");
            row.Title = NewLabel(text, name + "-title", "es-bench-row__title", Strings.Get(titleKey));
            row.Detail = NewLabel(text, name + "-detail", "es-note es-bench-row__detail");
            var side = NewBox(row.Box, "es-bench-row__side");
            row.Chip = NewLabel(side, ChipName(name), "es-bench-chip");
            row.Button = NewButton(side, ButtonName(name), "es-button--secondary es-button--compact", "", onClick);
            return row;
        }

        void ModuleRowPressed(bool isCollector)
        {
            var module = isCollector ? State.Collector : State.Doser;
            if (!module.Owned) Shop.CardInstallModule(isCollector);
            else Shop.CardSwitchModule(isCollector, !module.Enabled);
        }

        public override void Refresh()
        {
            Shop.FillBenchCard(State);
            var s = State;
            int done = (s.SparkUnlocked ? 1 : 0) + (s.UpgradeOwned ? 1 : 0) + (s.Collector.Owned ? 1 : 0) + (s.Doser.Owned ? 1 : 0);
            Status = Strings.Format("card.bench.status", done, 4);
            Line = Strings.Get(s.SparkUnlocked ? "card.bench.line.spark" : "card.bench.line");
            PrimaryAction = StationCardRules.BenchPrimary(s.SparkUnlocked, s.UpgradeOwned, s.Collector, s.Doser);
            Reason = null; ReasonIsWarning = false;

            // Spark research.
            SetText(spark.Detail, Strings.Get(s.SparkUnlocked ? "card.bench.sparkDone" : "card.bench.sparkDetail"));
            Shown(stamps, !s.SparkUnlocked);
            while (stampLabels.Count < s.Requirements.Count) stampLabels.Add(NewLabel(stamps, "es-bench-stamp", "es-req"));
            for (int i = 0; i < stampLabels.Count; i++)
            {
                bool on = i < s.Requirements.Count;
                Shown(stampLabels[i], on);
                if (!on) continue;
                SetText(stampLabels[i], Strings.Format(s.Requirements[i].Done ? "card.bench.stampDone" : "card.bench.stampTodo", s.Requirements[i].Name));
                stampLabels[i].EnableInClassList("es-req--done", s.Requirements[i].Done);
            }
            string sparkWhy = StationCardRules.SparkBlockedKey(s.SparkReady, s.Night, s.Currency, s.SparkCost);
            SetRow(spark, s.SparkUnlocked, Strings.Get("card.bench.researched"), Strings.Format("card.bench.researchShort", s.SparkCost),
                PrimaryAction == BenchAction.ResearchSpark, sparkWhy == null);

            // The bigger siphon.
            SetText(siphon.Detail, Strings.Format("card.bench.siphonDetail", s.BaseCapacity, s.UpgradedCapacity));
            bool siphonAffordable = s.Currency >= s.UpgradeCost;
            SetRow(siphon, s.UpgradeOwned, Strings.Get("card.bench.installed"), Strings.Format("card.bench.buyShort", s.UpgradeCost),
                PrimaryAction == BenchAction.BuySiphon, siphonAffordable);

            // Helper modules.
            SetModule(collector, s.Collector, "card.bench.collectorDetail", PrimaryAction == BenchAction.InstallCollector || PrimaryAction == BenchAction.TurnOnCollector);
            SetModule(doser, s.Doser, "card.bench.doserDetail", PrimaryAction == BenchAction.InstallDoser || PrimaryAction == BenchAction.TurnOnDoser);

            switch (PrimaryAction)
            {
                case BenchAction.ResearchSpark:
                    PrimaryText = Strings.Format("card.bench.researchSpark", s.SparkCost); PrimaryEnabled = sparkWhy == null;
                    if (sparkWhy != null) Reason = Strings.Format(sparkWhy, s.SparkCost - s.Currency);
                    break;
                case BenchAction.BuySiphon:
                    PrimaryText = Strings.Format("card.bench.buySiphon", s.UpgradeCost); PrimaryEnabled = siphonAffordable;
                    if (!siphonAffordable) Reason = Strings.Format("card.bench.why.coins", s.UpgradeCost - s.Currency);
                    break;
                case BenchAction.InstallCollector: case BenchAction.InstallDoser:
                    var module = PrimaryAction == BenchAction.InstallCollector ? s.Collector : s.Doser;
                    PrimaryText = Strings.Format(PrimaryAction == BenchAction.InstallCollector ? "card.bench.installCollector" : "card.bench.installDoser", module.Cost);
                    PrimaryEnabled = module.CanInstall;
                    if (!module.CanInstall) Reason = Strings.Format(module.BlockedKey, module.Missing);
                    break;
                case BenchAction.TurnOnCollector: PrimaryText = Strings.Get("card.bench.turnOnCollector"); PrimaryEnabled = true; break;
                case BenchAction.TurnOnDoser: PrimaryText = Strings.Get("card.bench.turnOnDoser"); PrimaryEnabled = true; break;
                default: PrimaryText = null; PrimaryEnabled = false; break;
            }
        }

        void SetRow(Row row, bool owned, string ownedChip, string buttonText, bool isPrimary, bool enabled)
        {
            row.Box.EnableInClassList("es-bench-row--next", isPrimary);
            Shown(row.Chip, owned);
            SetText(row.Chip, ownedChip);
            // The row's own button only when it isn't the card's primary: one action, one place.
            Shown(row.Button, !owned && !isPrimary);
            SetText(row.Button, buttonText);
            Enabled(row.Button, enabled);
        }

        void SetModule(Row row, BenchModule module, string detailKey, bool isPrimary)
        {
            row.Box.EnableInClassList("es-bench-row--next", isPrimary);
            bool blocked = !module.Owned && !module.CanInstall;
            SetText(row.Detail, blocked && !isPrimary ? Strings.Format(module.BlockedKey, module.Missing) : Strings.Get(detailKey));
            row.Detail.EnableInClassList("es-note--warn", blocked && !isPrimary);
            Shown(row.Chip, module.Owned);
            SetText(row.Chip, Strings.Get(module.Enabled ? "card.bench.on" : "card.bench.off"));
            row.Chip.EnableInClassList("es-bench-chip--on", module.Enabled);
            Shown(row.Button, !isPrimary);
            SetText(row.Button, !module.Owned ? Strings.Format("card.bench.installShort", module.Cost) : Strings.Get(module.Enabled ? "card.bench.turnOff" : "card.bench.turnOn"));
            Enabled(row.Button, module.Owned || module.CanInstall);
        }

        public override void Primary()
        {
            switch (PrimaryAction)
            {
                case BenchAction.ResearchSpark: Shop.CardResearchSpark(); break;
                case BenchAction.BuySiphon: Shop.CardBuySiphon(); break;
                case BenchAction.InstallCollector: Shop.CardInstallModule(true); break;
                case BenchAction.InstallDoser: Shop.CardInstallModule(false); break;
                case BenchAction.TurnOnCollector: Shop.CardSwitchModule(true, true); break;
                case BenchAction.TurnOnDoser: Shop.CardSwitchModule(false, true); break;
            }
        }
    }
}
