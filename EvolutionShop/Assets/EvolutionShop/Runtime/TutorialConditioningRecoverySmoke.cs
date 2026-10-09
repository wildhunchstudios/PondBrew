#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator TutorialConditioningRecoveryScenario()
        {
            limitations.Add("TUT-09 isolated canceled-vat recovery fixture explicitly supplies 500 credits, Spark research, four known species, one expanded lab, a rack and a vat through a validated full slot. It does not measure natural unlock pacing or affordability. No normal player save or scene asset is used.");
            limitations.Add("Culture harvesting and E use synthetic Input System devices with scripted physical station positions. Dosing, incubation, rack/vat/display actions and checkout use guarded production session commands. Both incubations and the completed conditioning process run on normal frames; no stock, tutorial specimen receipt or sale is seeded. This is not native mouse usability or a fresh-process restart test.");
            yield return new WaitForSeconds(Application.isEditor ? 1 : 3);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionTutorialConditioningRecoverySmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Canceled-vat audit uses its explicitly isolated evidence save");
            Check(shop.Tutorial.Mode == TutorialMode.Offered && shop.Model.Currency == 0 && shop.Cycle.Phase == ShopDayPhase.Night,
                "Recovery audit begins from the fresh Offered development fixture");
            cycleStarted = Time.realtimeSinceStartup; cycleMetrics = new ContinuousMetrics();
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionTutorialConditioningRecoveryKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionTutorialConditioningRecoveryMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false;

            var layoutData = new ShopLayout().CaptureSave();
            layoutData.labTier = 1; layoutData.nextItemNumber = 3;
            layoutData.items = new[]
            {
                new LayoutItemSave { id = "table-1", definitionId = ShopLayout.RackDefinitionId, x = 9, z = 7 },
                new LayoutItemSave { id = "table-2", definitionId = ShopLayout.ConditionerDefinitionId, x = 9, z = 13 }
            };
            Check(ShopLayout.TryRestore(layoutData, out var layout), "Labelled rack/vat layout satisfies the existing layout validator");
            var data = new GameModel(layout.WithTankIds(shop.Config)).CaptureSave();
            data.currency = 500; data.sparkResearchUnlocked = true;
            data.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin" };
            foreach (var grower in data.growers)
                if (grower.cultureId == "spark") grower.units = 4;
            var fixture = new SlotData { game = data, cycle = new ShopDayCycle().CaptureSave(), layout = layoutData };
            Check(SaveStore.Write(shop.SavePath, fixture, out var error) && shop.ReloadSaved(),
                "Validated isolated research/layout fixture reloads through actual geometry / " + error);
            const string rack = "table-1", vat = "table-2";
            string storage = shop.Layout.GetStorageSlotIds(rack)[0];
            Check(shop.Model.Currency == 500 && shop.Layout.LabTier == 1 && shop.Model.ConditionerIds.Length == 1 &&
                shop.World.TryGetConditioner(vat, out _) && shop.World.TryGetStorageRack(rack, out _) &&
                shop.Tutorial.Mode == TutorialMode.Offered && !shop.Model.GetTank("tank-a").HasValue,
                "Fixture supplies only documented setup resources and real rack/vat geometry");
            Check(shop.StartGuidedOpening(), "Start actual guided state without seeding learned specimen facts");

            shop.Tutorial.ObserveOrientation(true, true, true); // Labelled pose/orientation fixture for actionable target inspection.
            yield return TutorialConditioningManualBatch();
            string first = shop.Model.Incubator.SpecimenId;
            Check(shop.Tutorial.FirstSpecimenId == first && shop.Tutorial.CaptureSave().first.ready &&
                !shop.Tutorial.CaptureSave().first.manuallyDisplayed, "Real manual incubation owns the first exact ready tutorial candidate");
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(storage) && shop.Model.GetStorage(storage).Value.Id == first,
                "Physical rack accepts the same manually grown candidate");
            yield return ResearchHarvest(CultureKind.Spark);
            yield return ConditioningPanel(vat);
            Check(shop.ToggleConditioningInput(storage, first) && shop.BeginSelectedConditioning(),
                "Physical vat begins a real conditioning process for that exact Pondlet");
            Check(shop.Tutorial.FirstSpecimenId == first && shop.Model.GetConditioner(vat).Value.First.Value.Id == first,
                "Processing retains the candidate identity while its Pondlet species remains unchanged");
            shop.SetPanel(ShopPanel.None);
            yield return TutorialCapture("485-conditioning-processing");
            Check(shop.TryGetTutorialGuidance(out var processTarget) && processTarget.Station &&
                processTarget.Station.ConditionerItemId == vat && shop.TutorialObjectiveForDisplay.Contains("conditioning"),
                "Processing exact candidate points to its actual vat and wait/cancel recovery rather than unavailable display stock");
            yield return ConditioningPanel(vat);
            Check(shop.CancelSelectedConditioning() && shop.Model.GetConditioner(vat).Value.First.Value.SpeciesId == "pondlet" &&
                shop.Model.GetConditioner(vat).Value.First.Value.Id == first && shop.Model.Available(CultureKind.Spark) == 1,
                "Cancel returns unchanged Pondlet identity and one reserved Spark without creating stock");
            yield return SpecimenDisplayPanel("tank-a");
            int cash = shop.Model.Currency;
            Check(shop.StockDisplayFromConditioner(vat, 0, first) && shop.Model.GetTank("tank-a").Value.Id == first &&
                shop.Model.GetConditioner(vat).Value.Count == 0 && shop.Model.Currency == cash,
                "Production display wrapper transfers the canceled output once without money");
            string transferReceipt = Path.Combine(evidence, "conditioning-cancel-transfer-save.json");
            Check(!File.Exists(transferReceipt), "Canceled-output failure evidence destination is unused");
            File.Copy(shop.SavePath, transferReceipt);
            Check(shop.Tutorial.CaptureSave().first.manuallyDisplayed && shop.Tutorial.FirstSpecimenId == first,
                "Canceled-vat manual display records its exact tutorial receipt before the combined save");
            Check(SaveStore.Read(shop.SavePath, shop.Config, out var loadedGame, out var loadedSlot, out error) &&
                loadedSlot.tutorial.first.manuallyDisplayed && loadedSlot.tutorial.first.specimenId == first &&
                loadedGame.GetTank("tank-a").Value.Id == first && loadedGame.Currency == cash,
                "Disk snapshot contains display stock and its manual tutorial fact together / " + error);
            string receipt = JsonUtility.ToJson(shop.Tutorial.CaptureSave());
            Check(!shop.StockDisplayFromConditioner(vat, 0, first) && shop.Model.Currency == cash &&
                JsonUtility.ToJson(shop.Tutorial.CaptureSave()) == receipt,
                "Repeated canceled-output collection cannot duplicate specimen, money or tutorial credit");
            Check(shop.ReloadSaved() && shop.Tutorial.CaptureSave().first.manuallyDisplayed &&
                shop.Model.GetTank("tank-a").Value.Id == first, "Same-process reload retains canceled-output manual attribution");
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Ordinary opening makes the restored canceled Pondlet available to customers");
            yield return TutorialCheckout(first, cash + 6);
            Check(shop.Tutorial.FirstSaleDay == 1 && shop.Tutorial.FirstSpecimenId == first &&
                !shop.Tutorial.FirstSaleAccommodationActive, "Actual manual checkout qualifies the canceled Pondlet loop exactly once");
            Check(shop.CloseForNight(), "Normal close after recovered first sale");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 8, "canceled-vat first-sale night");
            shop.BackFromPanel();

            // Opposite outcome: completing the process changes species, so it must
            // never inherit the canceled-Pondlet shortcut or false manual proof.
            yield return TutorialConditioningManualBatch();
            string practice = shop.Model.Incubator.SpecimenId;
            Check(shop.Tutorial.PracticeSpecimenId == practice && practice != first,
                "Later real manual batch becomes the independent practice candidate");
            yield return SpecimenRackPanel(rack); Check(shop.StoreReadyInRack(storage), "Store actual practice candidate");
            yield return ConditioningPanel(vat);
            Check(shop.ToggleConditioningInput(storage, practice) && shop.BeginSelectedConditioning(),
                "Re-use refunded Spark in the later actual practice batch");
            yield return WaitFor(() => shop.Model.GetConditioner(vat).Value.Phase == ConditionerPhase.Ready, 16,
                "normal-time transformed practice output");
            Check(shop.Model.GetConditioner(vat).Value.First.Value.Id == practice &&
                shop.Model.GetConditioner(vat).Value.First.Value.SpeciesId == "lanternfin" &&
                string.IsNullOrEmpty(shop.Tutorial.PracticeSpecimenId),
                "Completed species transformation retires the old Pondlet tutorial chain while retaining actual stock");
            yield return SpecimenDisplayPanel("tank-a");
            Check(shop.StockDisplayFromConditioner(vat, 0, practice) &&
                shop.Model.GetTank("tank-a").Value.Id == practice && shop.Model.GetTank("tank-a").Value.SpeciesId == "lanternfin" &&
                string.IsNullOrEmpty(shop.Tutorial.PracticeSpecimenId) && shop.Tutorial.PracticeSaleDay == 0 && shop.Model.Currency == cash + 6,
                "Manual display of transformed output preserves commerce without inventing a qualifying practice chain");
            Check(shop.SaveNow() && SaveStore.Read(shop.SavePath, shop.Config, out loadedGame, out loadedSlot, out error) &&
                loadedSlot.tutorial.first.saleDay == 1 && loadedSlot.tutorial.practice.saleDay == 0 &&
                loadedGame.GetTank("tank-a").Value.SpeciesId == "lanternfin" && loadedGame.Currency == cash + 6,
                "Final full slot preserves recovered first sale and unqualified transformed practice stock / " + error);
        }

        IEnumerator TutorialConditioningManualBatch()
        {
            yield return ResearchHarvest(CultureKind.Bloom);
            yield return ResearchHarvest(CultureKind.Flow);
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Check(shop.DoseCulture(CultureKind.Bloom) && shop.DoseCulture(CultureKind.Flow) && shop.BeginManualIncubation(),
                "Physical preparation wrappers start one manually collected tutorial Pondlet");
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "ordinary manual tutorial incubation");
        }
    }
}
#endif
