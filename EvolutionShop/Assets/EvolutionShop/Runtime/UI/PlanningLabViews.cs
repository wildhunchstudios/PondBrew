using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Automation tab (UX.6): one row per known recipe. The lab helper keeps one recipe at a time (ShopAutomation), so the
    /// chosen row's details sit beside the list: how many to keep (1–9), which display to fill, the keep-stock switch and
    /// what the helper is doing in plain words. Prepare one batch is the page's one primary.
    /// </summary>
    public sealed class AutomationView : PlanningView
    {
        public const string ViewName = "es-plan-auto";
        public const string RecipesName = "es-plan-auto-recipes", DetailName = "es-plan-auto-detail", PickRecipeName = "es-plan-auto-pick-recipe",
            MinusName = "es-plan-auto-minus", PlusName = "es-plan-auto-plus", CountName = "es-plan-auto-count", MaintainName = "es-plan-auto-maintain",
            MaintainReasonName = "es-plan-auto-maintain-why", StatusName = "es-plan-auto-status", PrepareName = "es-plan-auto-prepare",
            PrepareReasonName = "es-plan-auto-prepare-why", CancelName = "es-plan-auto-cancel", PauseName = "es-plan-auto-pause",
            RetryName = "es-plan-auto-retry", CollectorName = "es-plan-auto-collector", DoserName = "es-plan-auto-doser";
        public static string RowName(string speciesId) => "es-plan-auto-" + speciesId;
        public static string ChooseName(string speciesId) => RowName(speciesId) + "-choose";
        public static string DestinationName(string tankId) => "es-plan-auto-into-" + tankId;

        readonly AutomationViewState state = new AutomationViewState();
        readonly VisualElement rows, detail, chosen, destinations;
        readonly Label pick, count, maintainWhy, status, prepareWhy, chosenName, collectorState, doserState;
        readonly CreatureGlyph chosenGlyph;
        readonly PlanSwitch maintain, collector, doser;
        readonly Button minus, plus, prepare, cancel, pause, retry;
        string shape;

        public AutomationViewState State => state;

        public AutomationView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("plan.auto.title");
            var columns = NewBox(Root, "es-plan-columns");
            var left = NewBox(columns, "es-plan-col");
            NewLabel(left, null, "es-eyebrow", Strings.Get("plan.auto.recipes"));
            // Many species later in the game: the list scrolls only once it is longer than its column.
            var list = new ScrollView(ScrollViewMode.Vertical) { name = RecipesName + "-scroll" };
            list.AddToClassList("es-plan-scroll");
            left.Add(list);
            rows = NewBox(list, "es-plan-list", RecipesName);
            NewLabel(left, null, "es-eyebrow es-plan-auto__helpers", Strings.Get("plan.auto.helpers"));
            var modules = NewBox(left, "es-plan-modules");

            detail = NewBox(columns, "es-plan-col es-plan-col--detail", DetailName);
            collector = Module(modules, CollectorName, "plan.auto.collector", out collectorState, () => Shop.SetAutomationEnabled(true, !state.CollectorOn));
            doser = Module(modules, DoserName, "plan.auto.doser", out doserState, () => Shop.SetAutomationEnabled(false, !state.DoserOn));
            pick = NewLabel(detail, PickRecipeName, "es-plan-empty", Strings.Get("plan.auto.pick"));
            chosen = NewBox(detail, "es-plan-auto__chosen", "es-plan-auto-chosen");
            var head = NewBox(chosen, "es-plan-auto__head");
            chosenGlyph = new CreatureGlyph { name = "es-plan-auto-glyph" };
            chosenGlyph.AddToClassList("es-plan-auto__glyph");
            head.Add(chosenGlyph);
            chosenName = NewLabel(head, "es-plan-auto-name", "es-plan-auto__name");
            var keep = NewBox(chosen, "es-plan-auto__line");
            NewLabel(keep, null, "es-plan-auto__label", Strings.Get("plan.auto.keep"));
            minus = NewButton(keep, MinusName, "es-step es-step--small", "−", () => Shop.SetAutomationDesiredStock(state.Desired - 1));
            count = NewLabel(keep, CountName, "es-plan-auto__count");
            plus = NewButton(keep, PlusName, "es-step es-step--small", "+", () => Shop.SetAutomationDesiredStock(state.Desired + 1));
            var into = NewBox(chosen, "es-plan-auto__line es-plan-auto__into");
            NewLabel(into, null, "es-plan-auto__label", Strings.Get("plan.auto.into"));
            destinations = NewBox(into, "es-plan-picks es-plan-auto__picks", "es-plan-auto-into");
            var maintainRow = NewBox(chosen, "es-plan-switch-row es-plan-auto__maintain");
            NewLabel(maintainRow, null, "es-plan-switch-row__label", Strings.Get("plan.auto.maintain"));
            maintain = new PlanSwitch(() => Shop.SetAutomationMaintainEnabled(!state.Maintain)) { name = MaintainName };
            maintainRow.Add(maintain);
            maintainWhy = NewLabel(chosen, MaintainReasonName, "es-plan-why");
            status = NewLabel(chosen, StatusName, "es-plan-auto__status");
            NewBox(detail, "es-plan-grow");
            var actions = NewBox(detail, "es-plan-auto__actions");
            prepareWhy = NewLabel(actions, PrepareReasonName, "es-plan-why");
            prepare = NewButton(actions, PrepareName, "es-button--primary", Strings.Get("plan.auto.prepare"), () => Shop.RequestAutomationBatch());
            var links = NewBox(actions, "es-plan-links");
            cancel = NewButton(links, CancelName, "es-link", Strings.Get("plan.auto.cancel"), () => Shop.CancelAutomationBatch());
            pause = NewButton(links, PauseName, "es-link", "", () => Shop.SetAutomationPaused(!state.Paused));
            retry = NewButton(links, RetryName, "es-link", Strings.Get("plan.auto.retry"), () => Shop.RetryAutomation());
        }

        PlanSwitch Module(VisualElement parent, string name, string titleKey, out Label stateLabel, System.Action onClick)
        {
            var box = NewBox(parent, "es-plan-module", name + "-box");
            var text = NewBox(box, "es-plan-module__text");
            NewLabel(text, name + "-title", "es-plan-module__title", Strings.Get(titleKey));
            stateLabel = NewLabel(text, name + "-state", "es-plan-module__state");
            var toggle = new PlanSwitch(onClick) { name = name };
            box.Add(toggle);
            return toggle;
        }

        public override void Refresh()
        {
            Shop.FillAutomationView(state);
            string now = Shape();
            if (now != shape) { shape = now; Rebuild(); }
            Subtitle = state.RecipeName == null ? Strings.Get("plan.auto.subtitle.none") :
                state.Maintain ? Strings.Format("plan.auto.subtitle.keeping", state.Desired, state.RecipeName) : Strings.Format("plan.auto.subtitle.chosen", state.RecipeName);
            Note = Strings.Get("plan.auto.note");
            ModuleState(collector, collectorState, state.CollectorOwned, state.CollectorOn);
            ModuleState(doser, doserState, state.DoserOwned, state.DoserOn);
            foreach (var row in state.Rows)
            {
                var line = rows.Q(RowName(row.SpeciesId));
                if (line == null) continue;
                line.EnableInClassList("es-plan-row--chosen", row.Chosen);
                var choose = line.Q<Button>(ChooseName(row.SpeciesId));
                var chosenChip = line.Q<Label>(RowName(row.SpeciesId) + "-chosen");
                Shown(choose, !row.Chosen);
                Shown(chosenChip, row.Chosen);
                // The helper keeps its recipe while a batch is on its way: cancel or finish it first (classic rule).
                Enabled(choose, !state.Pending);
            }
            bool has = state.RecipeId != null;
            Shown(pick, !has);
            Shown(chosen, has);
            if (has)
            {
                chosenGlyph.Set(state.RecipeLook, 1);
                SetText(chosenName, Strings.Format("plan.auto.keeping", state.RecipeName));
                SetText(count, state.Desired.ToString());
                Enabled(minus, state.Desired > 1);
                Enabled(plus, state.Desired < state.MaxDesired);
                foreach (var option in state.Destinations)
                {
                    var b = destinations.Q<Button>(DestinationName(option.TankId));
                    if (b != null) b.EnableInClassList("es-plan-pick--on", option.Chosen);
                }
                maintain.SetOn(state.Maintain);
                string why = state.Maintain ? null : PlanningRules.MaintainBlockedKey(true, state.DestinationId != null);
                Enabled(maintain, state.Maintain || why == null);
                Shown(maintainWhy, why != null);
                if (why != null) SetText(maintainWhy, Strings.Get(why));
                SetText(status, state.Status);
            }
            bool moduleOn = state.CollectorOn || state.DoserOn;
            string prepareBlocked = PlanningRules.PrepareBlockedKey(has, moduleOn, state.Pending, state.Maintain, state.BlockedBySave);
            // A one-off batch on its way swaps Prepare for Cancel; while keeping stock, Prepare stays and says why it waits.
            bool oneOff = state.Pending && !state.Maintain;
            Shown(prepare, !oneOff);
            Enabled(prepare, prepareBlocked == null);
            Shown(prepareWhy, prepareBlocked != null && !oneOff);
            if (prepareBlocked != null) SetText(prepareWhy, Strings.Get(prepareBlocked));
            // A stock target's request stays while it runs (classic rule): stop keeping stock first.
            Shown(cancel, state.Pending && !state.Maintain);
            Shown(pause, state.Pending || state.Maintain);
            SetText(pause, Strings.Get(state.Paused ? "plan.auto.resume" : "plan.auto.pause"));
            Shown(retry, state.BlockedBySave);
        }

        void ModuleState(PlanSwitch toggle, Label label, bool owned, bool on)
        {
            Shown(toggle, owned);
            toggle.SetOn(on);
            SetText(label, Strings.Get(!owned ? "plan.auto.module.missing" : on ? "card.bench.on" : "card.bench.off"));
            label.EnableInClassList("es-plan-module__state--on", owned && on);
        }

        string Shape()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var row in state.Rows) sb.Append(row.SpeciesId).Append(',');
            sb.Append('|');
            foreach (var d in state.Destinations) sb.Append(d.TankId).Append(',').Append(d.Name).Append(',');
            return sb.ToString();
        }

        void Rebuild()
        {
            rows.Clear(); destinations.Clear();
            foreach (var row in state.Rows)
            {
                string id = row.SpeciesId;
                var line = NewBox(rows, "es-plan-row", RowName(id));
                var glyph = new CreatureGlyph { name = RowName(id) + "-glyph" };
                glyph.AddToClassList("es-plan-row__glyph");
                glyph.Set(row.Look, 1);
                line.Add(glyph);
                var text = NewBox(line, "es-plan-row__text");
                NewLabel(text, RowName(id) + "-name", "es-plan-row__name", row.Name);
                FillRecipe(NewRecipe(text, RowName(id) + "-recipe"), row.Bloom, row.Flow, row.Spark, row.Mist);
                NewButton(line, ChooseName(id), "es-button--secondary es-button--compact", Strings.Get("plan.auto.choose"), () => Shop.SelectAutomationRecipe(id));
                NewLabel(line, RowName(id) + "-chosen", "es-plan-chip es-plan-chip--on", Strings.Get("plan.auto.chosen"));
            }
            foreach (var option in state.Destinations)
            {
                string tank = option.TankId;
                NewButton(destinations, DestinationName(tank), "es-plan-pick", option.Name, () => Shop.SetAutomationDestination(tank));
            }
        }
    }

    /// <summary>
    /// Research tab (UX.6): the four cultures on a short path — Bloom and Flow come with the shop, Spark and Mist are
    /// researched — with what each needs as stamps and its cost. Research Spark is the one primary when it's next; Mist
    /// is researched at the conditioning vat, so the path names it. Siphon MK II sits beside as equipment.
    /// </summary>
    public sealed class ResearchView : PlanningView
    {
        public const string ViewName = "es-plan-research";
        public const string SparkName = "es-plan-research-spark", SparkReasonName = "es-plan-research-spark-why", SiphonName = "es-plan-research-siphon",
            SiphonStateName = "es-plan-research-siphon-state";
        public static string StepName(CultureKind kind) => "es-plan-research-" + kind.ToString().ToLowerInvariant();
        public static string StepStateName(CultureKind kind) => StepName(kind) + "-state";
        public static string StampName(CultureKind kind, int i) => StepName(kind) + "-need-" + i;

        readonly ResearchViewState state = new ResearchViewState();
        readonly VisualElement[] steps = new VisualElement[4];
        readonly Label[] stepStates = new Label[4];
        readonly VisualElement[] stamps = new VisualElement[4];
        readonly Label sparkWhy, mistWhere, siphonState;
        readonly Button spark, siphon;

        public ResearchViewState State => state;

        public ResearchView(ShopSession shop, ShopUiStrings strings) : base(shop, strings, ViewName)
        {
            Title = Strings.Get("plan.research.title");
            Subtitle = Strings.Get("plan.research.subtitle");
            var path = NewBox(Root, "es-plan-path", "es-plan-research-path");
            for (int i = 0; i < 4; i++)
            {
                var kind = state.Steps[i].Culture;
                if (i > 0) NewBox(path, "es-plan-path__link");
                var card = NewBox(path, "es-plan-culture", StepName(kind));
                steps[i] = card;
                var swatch = NewBox(card, "es-plan-culture__swatch " + CultureClass(kind));
                swatch.pickingMode = PickingMode.Ignore;
                NewLabel(card, StepName(kind) + "-name", "es-plan-culture__name", Strings.Get("plan.culture." + kind.ToString().ToLowerInvariant()));
                stepStates[i] = NewLabel(card, StepStateName(kind), "es-plan-chip");
                stamps[i] = NewBox(card, "es-reqs", StepName(kind) + "-needs");
                if (kind == CultureKind.Spark)
                {
                    NewBox(card, "es-plan-grow");
                    sparkWhy = NewLabel(card, SparkReasonName, "es-plan-why");
                    spark = NewButton(card, SparkName, "es-button--primary es-plan-culture__button", "", () => Shop.PlanResearchSpark());
                }
                else if (kind == CultureKind.Mist)
                {
                    NewBox(card, "es-plan-grow");
                    mistWhere = NewLabel(card, StepName(kind) + "-where", "es-plan-why es-plan-why--info", Strings.Get("plan.research.atVat"));
                }
            }
            NewLabel(Root, null, "es-eyebrow es-plan-research__eyebrow", Strings.Get("plan.research.equipment"));
            var gear = NewBox(Root, "es-plan-row es-plan-research__gear", "es-plan-research-gear");
            var gearThumb = new FurnitureThumb(); gearThumb.Set(FurnitureThumb.SiphonLook); gear.Add(gearThumb);
            var gearText = NewBox(gear, "es-plan-row__text");
            NewLabel(gearText, null, "es-plan-row__name", Strings.Get("card.bench.siphon"));
            var detailLabel = NewLabel(gearText, "es-plan-research-siphon-detail", "es-plan-row__meta");
            siphonState = NewLabel(gear, SiphonStateName, "es-plan-chip es-plan-chip--on");
            siphon = NewButton(gear, SiphonName, "es-button--secondary es-button--compact", "", () => Shop.PlanBuySiphon());
            this.detailLabel = detailLabel;
        }

        readonly Label detailLabel;

        public override void Refresh()
        {
            Shop.FillResearchView(state);
            Note = Strings.Get("plan.research.note");
            var primary = PlanningRules.ResearchPrimary(state);
            for (int i = 0; i < 4; i++)
            {
                var step = state.Steps[i];
                bool starter = step.Culture == CultureKind.Bloom || step.Culture == CultureKind.Flow;
                SetText(stepStates[i], Strings.Get(starter ? "plan.research.starter" : step.Unlocked ? "plan.research.done" : "plan.research.locked"));
                stepStates[i].EnableInClassList("es-plan-chip--on", step.Unlocked);
                steps[i].EnableInClassList("es-plan-culture--done", step.Unlocked);
                steps[i].EnableInClassList("es-plan-culture--next", !step.Unlocked && primary == step.Culture);
                FillStamps(stamps[i], step);
                Shown(stamps[i], !step.Unlocked && step.Requirements.Count > 0);
            }
            var sparkStep = state.Steps[2];
            bool sparkNext = primary == CultureKind.Spark;
            Shown(spark, !sparkStep.Unlocked && sparkNext);
            SetText(spark, Strings.Format("plan.research.researchSpark", sparkStep.Cost));
            Enabled(spark, sparkStep.BlockedKey == null);
            // The stamps already say which starters are missing, so the reason line only speaks for night or coins.
            string why = sparkStep.Unlocked ? null : !sparkStep.Here ? "plan.tab.where.bench" :
                sparkStep.BlockedKey == "card.bench.why.starters" ? null : sparkStep.BlockedKey;
            Shown(sparkWhy, why != null);
            if (why != null) SetText(sparkWhy, sparkStep.BlockedKey == "card.bench.why.coins" ? Strings.Format(why, sparkStep.Amount) : Strings.Get(why));
            Shown(mistWhere, !state.Steps[3].Unlocked);
            SetText(mistWhere, Strings.Format("plan.research.atVat", state.Steps[3].Cost));
            SetText(detailLabel, Strings.Format("card.bench.siphonDetail", state.SiphonFrom, state.SiphonTo));
            Shown(siphonState, state.SiphonOwned);
            SetText(siphonState, Strings.Get("card.bench.installed"));
            Shown(siphon, !state.SiphonOwned);
            SetText(siphon, Strings.Format("card.bench.buyShort", state.SiphonCost));
            Enabled(siphon, state.SiphonCanBuy);
        }

        void FillStamps(VisualElement box, ResearchStep step)
        {
            while (box.childCount < step.Requirements.Count)
            {
                var stamp = new Label { name = StampName(step.Culture, box.childCount), pickingMode = PickingMode.Ignore };
                stamp.AddToClassList("es-req");
                box.Add(stamp);
            }
            for (int i = 0; i < box.childCount; i++)
            {
                var stamp = (Label)box[i];
                bool on = i < step.Requirements.Count;
                Shown(stamp, on);
                if (!on) continue;
                var need = step.Requirements[i];
                SetText(stamp, Strings.Format(need.Done ? "card.bench.stampDone" : "card.bench.stampTodo", need.Name));
                stamp.EnableInClassList("es-req--done", need.Done);
            }
        }
    }
}
