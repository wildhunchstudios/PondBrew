#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // UX-05: the research bench, specimen rack and conditioning vat cards through their real flows: buy the bigger
        // siphon, research Spark, install and switch the collector and doser, store creatures, run, cancel and finish a
        // glow bath, stock a display from the vat and research Mist. Controls are pressed with the synthetic Input System
        // mouse (CardClick, ToolkitCardsSmoke.cs); stations open with a real E.
        IEnumerator ToolkitLabCardsScenario()
        {
            limitations.Add("UX-05 lab cards route: a labelled 600-credit fixture; starter discoveries, Pondlets and the first Lanternfin are accelerated model fixtures (produce, display and sell), as in the R05 lab routes. Rack and vat are placed with real placement clicks, Spark is a real timed harvest, and stations open with a real E. Card controls are pressed with synthetic Input System devices, not physical hardware. Captures are screen-only.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready && ui.StationCard != null, "UI Toolkit root and station card load" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready || ui.StationCard == null) yield break;
            GateFresh();
            CardInput();
            StaffCreditFixture(600, "UX-05 lab cards");
            Check(shop.SetToolkitUiActive(true) && shop.ToolkitUiActive, "Preview turns the station cards on");
            var card = ui.StationCard;
            var root = ui.Root;
            var s = ui.Strings;
            yield return HudFrames(3);
            var primary = root.Q<Button>(StationCardPresenter.PrimaryName);
            var reason = root.Q<Label>(StationCardPresenter.ReasonName);
            var status = root.Q<Label>(StationCardPresenter.StatusName);
            var body = root.Q<Label>(StationCardPresenter.BodyName);

            // ---------- research bench: locked ----------
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId, ShopPanel.Upgrade);
            yield return HudFrames(3);
            var bench = card.BenchView;
            Check(card.IsShown && card.CurrentView == bench && card.Title == s.Get("card.title.Upgrade") && !card.ShopStyled,
                "The research bench opens its card, named from the string table (" + card.Title + ")");
            Check(CardShown(primary) && !primary.enabledSelf && primary.text == s.Format("card.bench.researchSpark", shop.Config.SparkResearchCost) &&
                reason.text == s.Get("card.bench.why.starters"), "Primary " + primary.text + ", disabled: " + reason.text);
            Check(BenchStamps(root) == s.Format("card.bench.stampTodo", "Pondlet") + "|" + s.Format("card.bench.stampTodo", "Bubblebelly") + "|" + s.Format("card.bench.stampTodo", "Ribbonfin"),
                "Spark's requirements read " + BenchStamps(root));
            var siphonButton = root.Q<Button>(BenchCardView.ButtonName(BenchCardView.SiphonRowName));
            Check(CardShown(siphonButton) && siphonButton.enabledSelf && siphonButton.text == s.Format("card.bench.buyShort", shop.Config.UpgradeCost),
                "The siphon row offers " + siphonButton.text);
            var collectorButton = root.Q<Button>(BenchCardView.ButtonName(BenchCardView.CollectorRowName));
            Check(CardShown(collectorButton) && !collectorButton.enabledSelf &&
                root.Q<Label>(BenchCardView.CollectorRowName + "-detail").text == s.Get("card.bench.why.siphon"), "The collector waits for the siphon and says so");
            yield return HudShot("ux05-bench-locked");
            CheckCard(card, "Bench, Spark locked", 1);
            int credits = shop.Model.Currency;
            yield return CardClick(siphonButton, "Buy siphon");
            Check(shop.Model.UpgradeOwned && shop.Model.Currency == credits - shop.Config.UpgradeCost, "A real click on the siphon row buys Siphon MK II");
            Check(root.Q<Label>(BenchCardView.CollectorRowName + "-detail").text == s.Get("card.bench.why.pondlet"), "The collector now waits for a Pondlet");

            // Labelled starter fixture: discover and sell Pondlet, Bubblebelly and Ribbonfin while the card is open.
            foreach (string species in new[] { "pondlet", "bubblebelly", "ribbonfin" })
            { SpecimenProduce(species); Ok(shop.Model.TransferToTank("tank-a"), "Starter fixture display"); Ok(shop.Model.SellFromTank("tank-a"), "Starter fixture sale"); }
            yield return HudFrames(3);
            Check(primary.enabledSelf && !CardShown(reason) && BenchStampsDone(root) == 3, "With the starters discovered, Research Spark is ready and every requirement is ticked");
            yield return HudShot("ux05-bench-spark");
            CheckCard(card, "Bench, Spark ready", 1);
            credits = shop.Model.Currency;
            yield return CardClick(primary, "Research Spark");
            Check(shop.Model.SparkResearchUnlocked && shop.Model.Currency == credits - shop.Config.SparkResearchCost, "A real click on Research Spark unlocks Spark");
            yield return HudFrames(2);
            Check(bench.PrimaryAction == BenchAction.InstallCollector && primary.text == s.Format("card.bench.installCollector", ShopAutomation.CollectorCost) &&
                primary.enabledSelf && body.text == s.Get("card.bench.line.spark"), "Next primary: " + primary.text);
            yield return CardClick(primary, "Install collector");
            Check(shop.Automation.CollectorOwned && !shop.Automation.CollectorEnabled, "A real click installs the collector, switched off");
            yield return HudFrames(2);
            Check(bench.PrimaryAction == BenchAction.InstallDoser, "Next primary: " + primary.text);
            yield return CardClick(primary, "Install doser");
            Check(shop.Automation.DoserOwned && !shop.Automation.DoserEnabled, "A real click installs the doser, switched off");
            yield return HudFrames(2);
            Check(bench.PrimaryAction == BenchAction.TurnOnCollector && primary.text == s.Get("card.bench.turnOnCollector"), "Next primary: " + primary.text);
            yield return CardClick(primary, "Turn on collector");
            Check(shop.Automation.CollectorEnabled, "A real click turns the collector on");
            yield return HudFrames(2);
            Check(bench.PrimaryAction == BenchAction.TurnOnDoser && root.Q<Label>(BenchCardView.ChipName(BenchCardView.CollectorRowName)).text == s.Get("card.bench.on"),
                "The collector's chip reads On; next primary: " + primary.text);
            yield return CardClick(collectorButton, "Turn collector off");
            Check(!shop.Automation.CollectorEnabled, "The collector row's own button switches it off again");
            yield return HudFrames(2);
            Check(bench.PrimaryAction == BenchAction.TurnOnCollector, "Switched off, the collector is the primary again");
            yield return CardClick(primary, "Turn on collector");
            yield return HudFrames(2);
            yield return CardClick(primary, "Turn on doser");
            Check(shop.Automation.CollectorEnabled && shop.Automation.DoserEnabled && bench.PrimaryAction == BenchAction.None && !CardShown(primary),
                "Everything owned and on: the bench has no primary left");
            yield return HudShot("ux05-bench-done");
            CheckCard(card, "Bench, everything done", 0);
            yield return CardClick(root.Q<Button>(BenchCardView.CulturePrepName), "Culture prep");
            Check(shop.Panel == ShopPanel.Automation && !card.IsShown, "Culture prep opens the planning panel (still classic until UX-06)");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);

            // ---------- specimen rack ----------
            yield return EditedPlace(null, ShopLayout.RackDefinitionId, 9, 7, 0, new Vector3(4.5f, .2f, 1.3f));
            string rack = shop.Layout.Items[0].Id; string[] slots = shop.Layout.GetStorageSlotIds(rack);
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion(), "Expand the lab for the vat (fixture setup)");
            shop.SetPanel(ShopPanel.None);
            CardInput();
            yield return SpecimenRackPanel(rack);
            yield return HudFrames(3);
            var rackView = card.RackView;
            Check(card.IsShown && card.CurrentView == rackView && card.Title.StartsWith("Specimen rack", System.StringComparison.Ordinal) && !card.Title.Contains(" / "),
                "The rack opens its card (" + card.Title + ")");
            Check(rackView.State.Compartments.Count == 2 && root.Q<Label>(RackCardView.CompartmentName(0) + "-name").text == s.Get("card.rack.empty") &&
                !CardShown(root, RackCardView.SourcesHeadingName) && primary.text == s.Get("card.rack.storeIdle") && !primary.enabledSelf &&
                reason.text == s.Get("card.rack.why.nothing"), "An empty rack with nothing ready: Store is disabled and says why");
            yield return HudShot("ux05-rack-empty");
            CheckCard(card, "Rack, empty", 1);
            SpecimenProduce("pondlet"); string firstPondlet = shop.Model.Incubator.SpecimenId;
            yield return HudFrames(3);
            Check(CardShown(root, RackCardView.SourceName(0)) && primary.enabledSelf && primary.text == s.Format("card.rack.store", "Pondlet") && rackView.Target == slots[0],
                "A ready Pondlet is offered; Store aims at the lower compartment");
            yield return CardClick(root.Q<Button>(RackCardView.CompartmentName(1)), "upper compartment");
            Check(rackView.Target == slots[1], "A real click on the upper compartment chooses it");
            yield return HudShot("ux05-rack-store");
            CheckCard(card, "Rack, storing", 1);
            yield return CardClick(primary, "Store Pondlet");
            Check(shop.Model.GetStorage(slots[1]).HasValue && shop.Model.GetStorage(slots[1]).Value.Id == firstPondlet && shop.Model.Incubator.Phase == IncubatorPhase.Dosing,
                "A real click on Store puts the Pondlet in the upper compartment and frees the incubator");
            SpecimenProduce("pondlet"); string secondPondlet = shop.Model.Incubator.SpecimenId;
            yield return HudFrames(3);
            yield return CardClick(primary, "Store Pondlet");
            Check(shop.Model.GetStorage(slots[0]).HasValue && shop.Model.GetStorage(slots[0]).Value.Id == secondPondlet && status.text == s.Get("card.rack.status.full"),
                "The second Pondlet goes in the lower compartment; the rack reads full");
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.None && !card.IsShown, "Esc closes the rack card");

            // ---------- conditioning vat ----------
            // Labelled fixture: the first Lanternfin (manual discovery, as in R05.3), then the vat placed with real clicks.
            shop.Model.Tick(30);
            Ok(shop.Model.Collect(CultureKind.Bloom), "Lanternfin fixture Bloom"); Ok(shop.Model.Collect(CultureKind.Flow), "Lanternfin fixture Flow");
            Ok(shop.Model.Collect(CultureKind.Spark), "Lanternfin fixture Spark");
            Ok(shop.Model.Dose(CultureKind.Bloom), "Dose Bloom"); Ok(shop.Model.Dose(CultureKind.Flow), "Dose Flow"); Ok(shop.Model.Dose(CultureKind.Spark), "Dose Spark");
            Ok(shop.Model.BeginIncubation(), "Lanternfin fixture incubation"); shop.Model.Tick(30); shop.World.Refresh(shop.Model);
            Check(shop.Model.IsDiscovered("lanternfin"), "Fixture discovers Lanternfin");
            Ok(shop.Model.TransferToTank("tank-a"), "Lanternfin fixture goes on display A");
            yield return EditedPlace(null, ShopLayout.ConditionerDefinitionId, -8, 14, 0, new Vector3(-4, .2f, 5.2f));
            string vat = shop.Model.ConditionerIds[0];
            CardInput();
            yield return ConditioningPanel(vat);
            yield return HudFrames(3);
            var vatView = card.VatView;
            Check(card.IsShown && card.CurrentView == vatView && vatView.State.Mode == VatCardMode.Idle && vatView.State.Inputs.Count == 2 &&
                status.text == s.Get("card.vat.status.idle") && card.Title == s.Get("card.title.Conditioning"), "The vat opens its card (" + card.Title + "): idle, with the two stored Pondlets as tiles");
            Check(root.Q<Label>(VatCardView.SparkName).text == s.Get("card.vat.sparkMissing") && !primary.enabledSelf &&
                primary.text == s.Format("card.vat.start", Mathf.RoundToInt(shop.Config.ConditioningSeconds)) && reason.text == s.Get("card.vat.why.pick"),
                "No Spark yet and nothing picked: " + primary.text + " is disabled and says " + reason.text);
            yield return CardClick(root.Q<Button>(VatCardView.InputName(0)), "first Pondlet");
            var chosenTile = root.Q<Button>(VatCardView.InputName(0));
            var otherTile = root.Q<Button>(VatCardView.InputName(1));
            float chosenInset = root.Q<Label>(VatCardView.InputName(0) + "-name").worldBound.x - chosenTile.worldBound.x;
            float otherInset = root.Q<Label>(VatCardView.InputName(1) + "-name").worldBound.x - otherTile.worldBound.x;
            Check(chosenTile.ClassListContains("es-tile--selected") && !otherTile.ClassListContains("es-tile--selected") &&
                UnityEngine.Mathf.Abs(chosenInset - otherInset) < 0.5f,
                "A chosen tile's thicker edge doesn't move its text (" + chosenInset.ToString("0.0") + " vs " + otherInset.ToString("0.0") + " px)");
            Check(vatView.State.SelectedCount == 1 && reason.text == s.Get("card.vat.why.spark") && !CardShown(root, VatCardView.SparkName),
                "A real click picks a Pondlet; the vat now waits for Spark, said once by the reason line");
            Check(CardShown(root.Q<Button>(VatCardView.MistButtonName)) && root.Q<Button>(VatCardView.MistButtonName).enabledSelf,
                "Mist research is offered below, as a secondary button");
            yield return HudShot("ux05-vat-idle");
            CheckCard(card, "Vat, waiting for Spark", 1);
            yield return CardKey(Key.Escape);
            yield return ResearchHarvest(CultureKind.Spark);
            CardInput();
            yield return ConditioningPanel(vat);
            yield return HudFrames(3);
            yield return CardClick(root.Q<Button>(VatCardView.InputName(0)), "first Pondlet");
            yield return CardClick(root.Q<Button>(VatCardView.InputName(1)), "second Pondlet");
            Check(vatView.State.SelectedCount == 2 && primary.enabledSelf && root.Q<Label>(VatCardView.SparkName).text == s.Format("card.vat.sparkOk", shop.Model.Available(CultureKind.Spark)),
                "Both Pondlets picked and Spark in the siphon: Start vat is ready");
            yield return HudShot("ux05-vat-pick");
            CheckCard(card, "Vat, ready to start", 1);
            yield return CardClick(primary, "Start vat");
            var machine = shop.Model.GetConditioner(vat).Value;
            Check(machine.Phase == ConditionerPhase.Processing && machine.Count == 2 && shop.StoredSpecimenCount == 0, "A real click on Start vat starts the glow bath with both Pondlets");
            shop.Advance(shop.Config.ConditioningSeconds * .5f);
            yield return HudFrames(3);
            var vatRing = root.Q<ProgressRing>(VatCardView.RingName);
            Check(vatView.State.Mode == VatCardMode.Processing && CardShown(vatRing) && vatRing.Progress > .3f && vatRing.Progress < .8f &&
                status.text == s.Get("card.vat.status.glowing") && body.text == s.Format("card.vat.line.glowing", 2), "Glowing: the ring fills (" + vatRing.Progress.ToString("0.00") + ")");
            yield return HudShot("ux05-vat-glowing");
            CheckCard(card, "Vat, glowing", 0);
            yield return CardClick(root.Q<Button>(VatCardView.CancelName), "Cancel batch");
            Check(vatView.Confirming && CardShown(root, VatCardView.ConfirmName), "Cancel asks first");
            yield return HudShot("ux05-vat-confirm");
            CheckCard(card, "Vat, cancel confirm", 0);
            yield return CardClick(root.Q<Button>(VatCardView.KeepName), "Keep glowing");
            Check(!vatView.Confirming && shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Processing, "Keep glowing backs out");
            int sparkBefore = shop.Model.Available(CultureKind.Spark);
            yield return CardClick(root.Q<Button>(VatCardView.CancelName), "Cancel batch");
            yield return CardClick(root.Q<Button>(VatCardView.CancelNowName), "Cancel now");
            machine = shop.Model.GetConditioner(vat).Value;
            Check(machine.Phase == ConditionerPhase.Ready && machine.First.Value.SpeciesId == "pondlet" && shop.Model.Available(CultureKind.Spark) == sparkBefore + 1,
                "Cancel batch returns the Spark and leaves the unchanged Pondlets to collect");
            yield return HudFrames(2);
            Check(vatView.State.Mode == VatCardMode.Ready && CardShown(root, VatCardView.OutputName(0)) &&
                root.Q<Label>(VatCardView.OutputName(0) + "-name").text == "Pondlet", "The vat shows its two Pondlets ready to collect");
            yield return HudShot("ux05-vat-cancelled");
            CheckCard(card, "Vat, cancelled", 0);

            // Collect the Pondlets back into the rack from its card.
            yield return SpecimenRackPanel(rack);
            yield return HudFrames(3);
            Check(rackView.State.Sources.Count == 2 && rackView.State.Sources[0].Source == StockSource.Vat, "The rack offers both vat chambers");
            yield return CardClick(primary, "Store from vat");
            yield return HudFrames(2);
            yield return CardClick(primary, "Store from vat");
            Check(shop.StoredSpecimenCount == 2 && shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Idle, "Both Pondlets go back in the rack; the vat is empty again");

            // A finished glow bath.
            yield return ConditioningPanel(vat);
            yield return HudFrames(3);
            yield return CardClick(root.Q<Button>(VatCardView.InputName(0)), "first Pondlet");
            yield return CardClick(root.Q<Button>(VatCardView.InputName(1)), "second Pondlet");
            yield return CardClick(primary, "Start vat");
            shop.Advance(shop.Config.ConditioningSeconds + .1f);
            yield return HudFrames(3);
            machine = shop.Model.GetConditioner(vat).Value;
            Check(machine.Phase == ConditionerPhase.Ready && machine.First.Value.SpeciesId == "lanternfin" && machine.Second.Value.SpeciesId == "lanternfin" &&
                root.Q<Label>(VatCardView.OutputName(1) + "-name").text == shop.Model.GetSpecies("lanternfin").DisplayName, "The finished bath shows two Lanternfins");
            Check(vatView.Line == s.Get("card.vat.line.ready") && !CardShown(root, VatCardView.OutputsNoteName),
                "With room to store them, the body line alone says where to collect");
            yield return HudShot("ux05-vat-done");
            CheckCard(card, "Vat, done", 0);

            // One Lanternfin onto display B through the UX-04 tank card, the other into the rack.
            yield return CardOpenTank("tank-b", StationOf(StationKind.TankB));
            var tank = card.TankView;
            Check(card.CurrentView == tank && tank.State.Sources.Count == 2 && tank.State.Sources[0].Source == StockSource.Vat, "Display B offers the vat's two Lanternfins");
            yield return CardClick(root.Q<Button>(TankCardView.StockTileName(0)), "vat stock tile");
            Check(shop.Model.GetTank("tank-b").HasValue && shop.Model.GetTank("tank-b").Value.SpeciesId == "lanternfin", "A real click stocks display B from the vat");
            yield return SpecimenRackPanel(rack);
            yield return HudFrames(3);
            yield return CardClick(primary, "Store Lanternfin");
            Check(shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Idle && shop.StoredSpecimenCount == 1, "The other Lanternfin goes in the rack");

            // Mist research at the vat.
            yield return ConditioningPanel(vat);
            yield return HudFrames(3);
            var mist = root.Q<Button>(VatCardView.MistButtonName);
            credits = shop.Model.Currency;
            Check(CardShown(mist) && mist.enabledSelf && mist.text == s.Format("card.vat.researchMist", shop.Config.MistResearchCost), "Mist research offers " + mist.text);
            yield return CardClick(mist, "Research Mist");
            Check(shop.Model.MistResearchUnlocked && shop.Model.Currency == credits - shop.Config.MistResearchCost &&
                root.Q<Label>(VatCardView.MistNoteName).text == s.Get("card.vat.mistUnlocked") && !CardShown(mist), "A real click researches Mist; the vat says how to collect it");
            Check(!CardShown(root, VatCardView.InputsHeadingName) && reason.text == s.Get("card.vat.why.none"),
                "With no stored Pondlets, the vat's reason line says to store one, once");
            yield return HudShot("ux05-vat-mist");
            CheckCard(card, "Vat, Mist researched", 1);

            // A full rack with something ready says why Store can't.
            SpecimenProduce("pondlet");
            yield return SpecimenRackPanel(rack);
            yield return HudFrames(3);
            yield return CardClick(primary, "Store Pondlet");
            SpecimenProduce("pondlet");
            yield return HudFrames(3);
            Check(rackView.Target == null && !primary.enabledSelf && reason.text == s.Get("card.rack.why.full") && status.text == s.Get("card.rack.status.full"),
                "A full rack with a Pondlet ready: Store is disabled and says the rack is full");
            yield return HudShot("ux05-rack-full");
            CheckCard(card, "Rack, full", 1);

            // Preview off: the classic panels are back.
            Check(shop.SetToolkitUiActive(false) && !shop.ToolkitUiActive, "Preview turns off");
            yield return SpecimenRackPanel(rack);
            Check(shop.Panel == ShopPanel.StorageRack && !card.IsShown, "With the preview off the classic panel owns the rack again");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        static string BenchStamps(VisualElement root)
        {
            var texts = new System.Collections.Generic.List<string>();
            foreach (var stamp in root.Query<Label>(className: "es-req").ToList()) if (CardShown(stamp)) texts.Add(stamp.text);
            return string.Join("|", texts);
        }

        static int BenchStampsDone(VisualElement root)
        {
            int done = 0;
            foreach (var stamp in root.Query<Label>(className: "es-req").ToList()) if (CardShown(stamp) && stamp.ClassListContains("es-req--done")) done++;
            return done;
        }
    }
}
#endif
