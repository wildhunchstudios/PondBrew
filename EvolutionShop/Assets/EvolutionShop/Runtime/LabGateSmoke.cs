#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        const string GateVat = "table-2", GateRack = "table-1";
        static readonly string[] GateSpecies = { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };

        IEnumerator LabGateScenario()
        {
            limitations.Add("R05.5 uses an isolated progressed-layout fixture with all five discoveries/research and zero cash. Initial display/input stock is prepared with accelerated model ticks. Later simultaneous production and blocked-output recovery use ordinary runtime time. Physical E, placement input and a selected casual buyer run; menu buttons use application commands and camera poses are set. This is not a human GUI, natural unlock-economy, audio or hardware benchmark.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Lab gate uses only its isolated save");
            shop.SetPanel(ShopPanel.Pause);
            yield return new WaitForSeconds(.5f);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionLabGateKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionLabGateMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionLabGateReload") != null) { yield return LabGateRestart(); yield break; }

            string path = Application.isEditor ? Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V15Conditioning.json") : Path.Combine(evidence, "seed-v15.json");
            var historical = JsonUtility.FromJson<SlotData>(File.ReadAllText(path));
            Check(ShopLayout.TryRestore(historical.layout, out var layout), "Use the frozen progressed layout without changing its fixture");
            var seed = new GameModel(layout.WithTankIds(shop.Config)).CaptureSave();
            seed.sparkResearchUnlocked = seed.mistResearchUnlocked = true; seed.discoveries = (string[])GateSpecies.Clone();
            foreach (var grower in seed.growers) grower.units = 6;
            var slot = new SlotData { game = seed, layout = layout.CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
            Check(SaveStore.Write(shop.SavePath, slot, out var error) && shop.ReloadSaved(), "Load labelled all-research zero-cash fixture / " + error);
            Check(shop.Model.Currency == 0 && shop.Model.Capacity == 4 && !shop.Model.UpgradeOwned, "Gate uses the original four-unit tool and zero money");

            GatePrepare("ribbonfin"); Ok(shop.Model.TransferToTank("tank-a"), "Setup original Ribbonfin display");
            GatePrepare("lanternfin"); Ok(shop.Model.TransferToTank("tank-b"), "Setup original Lanternfin display");
            var sources = shop.Model.StorageIds; var inputIds = new string[2];
            for (int i = 0; i < 2; i++) { GatePrepare("pondlet"); inputIds[i] = shop.Model.Incubator.SpecimenId; Ok(shop.Model.StoreReadyOutput(sources[i]), "Setup exact Pondlet input " + i); }
            shop.World.Refresh(shop.Model);
            Ok(shop.Model.Collect(CultureKind.Spark), "Collect batch Spark from renewable supply");
            string before = GateState();
            Check(shop.Model.BeginConditioning(GateVat, sources, new[] { inputIds[0], "stale-second-id" }).Failure == FailureCode.SpecimenMismatch && GateState() == before,
                "Stale second batch input preserves both Pondlets and Spark atomically");
            yield return ConditioningPanel(GateVat);
            Check(shop.ToggleConditioningInput(sources[0], inputIds[0]) && shop.ToggleConditioningInput(sources[1], inputIds[1]) && shop.BeginSelectedConditioning(), "Physical vat starts both exact Pondlets");

            GateDose(CultureKind.Bloom, 1); GateDose(CultureKind.Spark, 1); GateDose(CultureKind.Mist, 1);
            before = GateState();
            Check(shop.Model.BeginIncubation().Failure == FailureCode.InvalidRecipe && GateState() == before, "Invalid Bloom/Spark/Mist mixture preserves active vat and all doses");
            Check(shop.Model.Collect(CultureKind.Flow).Failure == FailureCode.ReservoirFull && GateState() == before, "Staged input plus reserved Spark blocks a fifth carried unit without mutation");
            Ok(shop.Model.ClearDoses(), "Return invalid recipe losslessly"); Ok(shop.Model.ReleaseCulture(CultureKind.Spark), "Release surplus Spark to free one slot");
            Ok(shop.Model.Collect(CultureKind.Flow), "Collect replacement Flow"); Ok(shop.Model.UseKnownRecipe("driftwing"), "Prepare exact known Airfin from recovered doses");
            Check(shop.Model.ReservedCultureCount == 4 && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Three staged Airfin doses coexist with one reserved conditioning Spark");
            GateSnapshot("staged");

            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Start Airfin beside active conditioning"); string airfin = shop.Model.Incubator.SpecimenId;
            yield return EditedPlace(GateVat, ShopLayout.ConditionerDefinitionId, -8, 14, 0, new Vector3(-4, .2f, 5.2f));
            yield return LabMove(ShopLayout.IncubatorFacilityId, 0, 8, 1, new Vector3(0, .2f, 6.3f));
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Model.Incubator.SpecimenId == airfin &&
                shop.Model.GetConditioner(GateVat).Value.Phase == ConditionerPhase.Processing && shop.Model.GetConditioner(GateVat).Value.First.Value.Id == inputIds[0],
                "Simultaneously running relocated machines retain jobs and exact identities");
            GateStockCount(5); GateSnapshot("working");
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator); yield return Capture("200-lab-gate-working");
            yield return ConditioningPanel(GateVat);
            Check(shop.CancelSelectedConditioning() && shop.Model.Available(CultureKind.Spark) == 1, "Cancel relocated vat returns one reserved Spark");
            before = GateState(); Check(!shop.CancelSelectedConditioning() && GateState() == before, "Repeat cancel cannot refund again");
            Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Model.GetConditioner(GateVat).Value.First.Value.SpeciesId == "pondlet", "Cancel keeps original Pondlets and leaves independent incubation running");
            GateSnapshot("canceled");
            yield return SpecimenRackPanel(GateRack);
            Check(shop.StoreConditionerInRack(GateVat, 0, sources[0], inputIds[0]), "Collect first canceled resident into its original compartment");
            Check(!shop.Model.GetConditioner(GateVat).Value.First.HasValue && shop.Model.GetConditioner(GateVat).Value.Second.Value.Id == inputIds[1], "Partial collection retains the exact second resident and empty first chamber");
            GateSnapshot("partial");

            shop.SetPanel(ShopPanel.Pause); File.Copy(Path.Combine(evidence, "working.json"), shop.SavePath, true);
            Check(shop.ReloadSaved(), "Reload isolated working branch to validate completed output separately from cancellation");
            shop.SetPanel(ShopPanel.None);
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready && shop.Model.GetConditioner(GateVat).Value.Phase == ConditionerPhase.Ready, 17, "simultaneous production completion");
            Check(shop.Model.Incubator.SpecimenId == airfin && shop.Model.GetConditioner(GateVat).Value.First.Value.Id == inputIds[0] &&
                shop.Model.GetConditioner(GateVat).Value.Second.Value.Id == inputIds[1] && shop.Model.GetConditioner(GateVat).Value.First.Value.SpeciesId == "lanternfin", "Ordinary-time completion preserves Airfin and both conditioned identities");
            yield return SpecimenRackPanel(GateRack); Check(shop.StoreReadyInRack(sources[0]), "Store parallel Airfin in first rack compartment");
            yield return GateIncubate("bubblebelly");
            yield return SpecimenRackPanel(GateRack); Check(shop.StoreReadyInRack(sources[1]), "Fill second rack compartment with ordinary-time Bubblebelly");
            yield return GateIncubate("driftwing");
            string blockedAirfin = shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(GateRack);
            before = GateState();
            Check(!shop.StoreReadyInRack(sources[0]) && !shop.StoreConditionerInRack(GateVat, 0, sources[1], inputIds[0]) && GateState() == before, "Physical full rack rejects both incubator and vat output without loss");
            yield return SpecimenDisplayPanel("tank-a"); before = GateState();
            Check(shop.TransferToDisplay("tank-a").Failure == FailureCode.TankFull && !shop.StockDisplayFromConditioner(GateVat, 1, inputIds[1]) && GateState() == before, "Physical full display rejects both output sources without mutation");
            Check(shop.OpenOwnedFurnitureMenu() && !shop.StoreFurniture(GateVat) && !shop.RequestFurnitureSale(GateVat) && !shop.StoreFurniture(GateRack), "Occupied vat and rack cannot be stored or sold to discard stock");
            yield return EditedPlace(GateRack, ShopLayout.RackDefinitionId, 8, 14, 0, new Vector3(4, .2f, 5.2f));
            Check(shop.Model.GetStorage(sources[0]).Value.Id == airfin && shop.Model.Incubator.SpecimenId == blockedAirfin && shop.Model.Currency == 0, "Full rack relocation preserves hidden and blocked output at zero cash");
            GateStockCount(7); LabRoutesCheck("R05.5 occupied lab");
            yield return ConditioningPanel(GateVat); yield return Capture("201-lab-gate-full-output");
            yield return SpecimenBounds("R05.5 full relocated stock"); GateSnapshot("ready");

            yield return EnterCounter(); Check(shop.OpenForDay(), "Zero-cash shop opens with all production blocked by full destinations");
            var buyer = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => buyer.Phase == CustomerPhase.Queued, 45, "lab recovery buyer queues");
            int paid = buyer.Price; string sold = buyer.SpecimenId, emptied = buyer.TargetTankId;
            yield return ServeNext("Lab full-storage recovery buyer");
            Check(shop.Model.Currency == paid && !shop.Model.GetTank(emptied).HasValue && !shop.ServeCustomer(), "One real checkout creates space and pays once from zero cash");
            yield return SpecimenDisplayPanel(emptied); Ok(shop.TransferToDisplay(emptied), "Restock exact previously blocked Airfin");
            Check(shop.Model.GetTank(emptied).Value.Id == blockedAirfin && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Freeing one display recovers the production loop without discarding stock");
            GateStockCount(6);
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "lab recovery buyer exits");
            yield return EnterCounter(); Check(shop.CloseForNight(), "Close recovery shift");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "recovery closing");
            Check(shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == paid && shop.Reports.Last.pathProblems == 0, "Recovery report records the one committed sale without route failure");
            foreach (var resident in GateResidents()) Check(resident != sold, "Sold identity is absent after restock");
            yield return SpecimenDisplayPanel(emptied); yield return Capture("202-lab-gate-recovered"); GateSnapshot("recovered");
        }

        IEnumerator GateIncubate(string species)
        {
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            GateRecipe(species); Ok(shop.Perform(shop.Model.BeginIncubation()), "Start ordinary-time " + species);
            float started = Time.realtimeSinceStartup;
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "ordinary-time " + species);
            Check(Time.realtimeSinceStartup - started >= shop.Config.IncubationSeconds - .1f && shop.Model.Incubator.SpeciesId == species, "Correct ordinary-time output / " + species);
        }
        void GateRecipe(string species)
        {
            var recipe = shop.Model.GetSpecies(species);
            GateDose(CultureKind.Bloom, recipe.Bloom); GateDose(CultureKind.Flow, recipe.Flow);
            GateDose(CultureKind.Spark, recipe.Spark); GateDose(CultureKind.Mist, recipe.Mist);
        }
        void GateDose(CultureKind culture, int count)
        { for (int i = 0; i < count; i++) { Ok(shop.Model.Collect(culture), "Collect gate " + culture); Ok(shop.Model.Dose(culture), "Dose gate " + culture); } }
        void GatePrepare(string species)
        { shop.Model.Tick(30); GateRecipe(species); Ok(shop.Model.BeginIncubation(), "Accelerated initial fixture / " + species); shop.Model.Tick(30); }
        string GateState() => JsonUtility.ToJson(shop.Model.CaptureSave());
        void GateSnapshot(string stage)
        {
            shop.SetPanel(ShopPanel.Pause); Check(shop.SaveNow(), "Save isolated stage / " + stage);
            File.Copy(shop.SavePath, Path.Combine(evidence, stage + ".json"));
            Check(SaveStore.Read(shop.SavePath, shop.Config, out var read, out _, out var error) && JsonUtility.ToJson(read.CaptureSave()) == GateState(), "Disk round trip exactly matches / " + stage + " / " + error);
        }
        HashSet<string> GateResidents()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Action<string> add = id => { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidOperationException("Missing/duplicate lab specimen identity: " + id); };
            foreach (string id in shop.Model.TankIds) if (shop.Model.GetTank(id).HasValue) add(shop.Model.GetTank(id).Value.Id);
            foreach (string id in shop.Model.StorageIds) if (shop.Model.GetStorage(id).HasValue) add(shop.Model.GetStorage(id).Value.Id);
            foreach (string id in shop.Model.ConditionerIds)
            { var vat = shop.Model.GetConditioner(id).Value; if (vat.First.HasValue) add(vat.First.Value.Id); if (vat.Second.HasValue) add(vat.Second.Value.Id); }
            if (shop.Model.Incubator.Phase != IncubatorPhase.Dosing) add(shop.Model.Incubator.SpecimenId);
            return ids;
        }
        void GateStockCount(int count) => Check(GateResidents().Count == count, "Exactly " + count + " uniquely owned lab/store specimens");

        IEnumerator LabGateRestart()
        {
            string stage = ShopSession.Arg("-evolutionLabGateStage");
            Check(Array.IndexOf(new[] { "staged", "working", "canceled", "partial", "ready", "recovered" }, stage) >= 0, "Restart names an explicit lab stage");
            byte[] original = File.ReadAllBytes(shop.SavePath);
            Check(SaveStore.Read(shop.SavePath, shop.Config, out var expected, out var slot, out var error), "Read supplied stage / " + stage + " / " + error);
            string expectedState = JsonUtility.ToJson(expected.CaptureSave());
            Check(shop.ReloadSaved() && GateState() == expectedState, "Fresh process restores every culture, specimen, timer, research, price and cash field exactly / " + stage);
            Check(JsonUtility.ToJson(shop.Layout.CaptureSave()) == JsonUtility.ToJson(slot.layout) &&
                JsonUtility.ToJson(shop.Cycle.CaptureSave()) == JsonUtility.ToJson(slot.cycle) && JsonUtility.ToJson(shop.Reports.CaptureSave()) == JsonUtility.ToJson(slot.trades), "Fresh process restores placement, day and sale ledger / " + stage);
            Check(Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)) == Convert.ToBase64String(original), "Reading/restoring stage does not rewrite primary bytes / " + stage);
            yield return new WaitForSeconds(.5f);
            Check(GateState() == expectedState, "Closed application and paused reload add no production progress / " + stage);
            GateStockCount(stage == "staged" ? 4 : stage == "ready" ? 7 : stage == "recovered" ? 6 : 5);
            Check(shop.World.Stations.Count == 9 && shop.Model.Capacity == 4 && shop.Model.MistResearchUnlocked, "Restart has one physical vat, rack, four facilities and original-capacity tool");
            LabRoutesCheck("Restart " + stage);
            var vat = shop.Model.GetConditioner(GateVat).Value; string before = GateState();
            if (stage == "staged")
            {
                Check(shop.Model.Incubator.Phase == IncubatorPhase.Dosing && vat.Phase == ConditionerPhase.Processing && shop.Model.ReservedCultureCount == 4, "Staged snapshot retains the full shared capacity");
                Ok(shop.Model.ClearDoses(), "Restart returns all three staged cultures");
                Check(shop.Model.Available(CultureKind.Mist) == 1 && shop.Model.ReservedCultureCount == 4, "Returning Mist preserves reserved conditioning Spark");
                Ok(shop.Model.UseKnownRecipe("driftwing"), "Restart can prepare recovered known recipe"); Ok(shop.Model.BeginIncubation(), "Restart starts recovered Airfin");
            }
            else if (stage == "working")
            {
                Check(shop.Model.Incubator.Phase == IncubatorPhase.Incubating && vat.Phase == ConditionerPhase.Processing, "Working snapshot restores both independent jobs");
                Ok(shop.Model.CancelConditioning(GateVat), "Fresh process cancels exact active batch");
                Check(shop.Model.Available(CultureKind.Spark) == 1 && !shop.Model.CancelConditioning(GateVat).Success && shop.Model.GetConditioner(GateVat).Value.First.Value.Id == vat.First.Value.Id, "Restart cancellation refunds once and preserves inputs");
            }
            else if (stage == "canceled" || stage == "partial")
            {
                Check(vat.Phase == ConditionerPhase.Ready && shop.Model.Available(CultureKind.Spark) == 1 && !shop.Model.CancelConditioning(GateVat).Success && GateState() == before, "Canceled reload cannot refund a second Spark");
                int index = stage == "partial" ? 1 : 0; var fish = index == 0 ? vat.First.Value : vat.Second.Value;
                Check(fish.SpeciesId == "pondlet", "Canceled output keeps original species");
                Ok(shop.Model.TransferConditionerToStorage(GateVat, index, shop.Model.StorageIds[index], fish.Id), "Restart collects exact canceled output once");
                Check(!shop.Model.TransferConditionerToStorage(GateVat, index, shop.Model.StorageIds[index], fish.Id).Success, "Repeat canceled collection cannot duplicate a resident");
            }
            else if (stage == "ready")
            {
                Check(shop.Model.Currency == 0 && shop.Model.Incubator.Phase == IncubatorPhase.Ready && vat.Count == 2, "Full-output restart stays safely blocked at zero cash");
                Check(shop.Model.StoreReadyOutput(shop.Model.StorageIds[0]).Failure == FailureCode.StorageFull && shop.Model.TransferConditionerToTank(GateVat, 0, "tank-a", vat.First.Value.Id).Failure == FailureCode.TankFull && GateState() == before, "Full destinations still preserve ready output after fresh launch");
                string sold = shop.Model.GetTank("tank-a").Value.Id; int price = shop.Model.GetPrice(shop.Model.GetTank("tank-a").Value.SpeciesId).Value.Price;
                Ok(shop.Model.ReserveTank("tank-a", "gate-restart"), "Reserve a recovery display through normal model transaction");
                Ok(shop.Model.SellReservedTank("tank-a", "gate-restart", sold), "Commit isolated domain recovery sale");
                string ready = shop.Model.Incubator.SpecimenId; Ok(shop.Model.TransferToTank("tank-a"), "Restock exact blocked incubation output after fresh launch");
                Check(shop.Model.GetTank("tank-a").Value.Id == ready && shop.Model.Currency == price && !shop.Model.SellReservedTank("tank-a", "gate-restart", sold).Success, "Fresh-process recovery pays and transfers once");
                limitations.Add("The ready-state restart uses a direct model reservation/checkout for a focused transaction check. The main gate scenario uses a real customer and physical counter.");
            }
            else
            {
                Check(shop.Model.Currency == shop.Reports.Last.revenue && shop.Reports.Last.sales == 1 && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Recovered restart retains the one real sale and free incubator");
                Check(!shop.ServeCustomer() && GateState() == before, "Reload cannot replay completed checkout");
            }
            if (shop.Model.Incubator.Phase == IncubatorPhase.Incubating)
            {
                string id = shop.Model.Incubator.SpecimenId; float remaining = shop.Model.Incubator.SecondsRemaining;
                shop.SetPanel(ShopPanel.None); float started = Time.realtimeSinceStartup;
                yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 17, "resumed exact Airfin");
                Check(shop.Model.Incubator.SpecimenId == id && Time.realtimeSinceStartup - started >= remaining - .15f, "Resumed incubation keeps identity and takes remaining active time");
            }
            shop.World.Refresh(shop.Model); yield return ConditioningPanel(GateVat); yield return Capture("203-lab-restart-" + stage);
            shop.SetPanel(ShopPanel.Pause); before = GateState();
            Check(shop.SaveNow() && shop.ReloadSaved() && GateState() == before, "Fresh process can save/reload post-recovery state without duplication / " + stage);
            GateResidents();
        }
    }
}
#endif
