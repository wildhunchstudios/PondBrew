#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator AirfinScenario()
        {
            limitations.Add("R05.4 isolated frozen-v15 fixture starts with the discovered Lanternfin and placed vat; exact physical E/held-mouse interactions, normal-time incubation and a selected casual visitor are exercised. Panel buttons use session commands. Camera teleports and presentation-only bounds sweep do not measure human traversal or tuning.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")),
                "Airfin uses the isolated evidence save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionAirfinKeyboard");
            cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionAirfinMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent(); shop.Customers.ArrivalsEnabled = false;
            if (ShopSession.Arg("-evolutionAirfinReload") != null) { yield return AirfinRestart(); yield break; }

            string fixture = Application.isEditor ?
                Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V15Conditioning.json") :
                Path.Combine(evidence, "seed-v15.json");
            byte[] original = File.ReadAllBytes(fixture);
            File.WriteAllBytes(shop.SavePath, original);
            Check(shop.ReloadSaved() && shop.Cycle.Phase == ShopDayPhase.Night && shop.Model.Currency == 350 &&
                shop.Model.GetConditioner("table-2").Value.Phase == ConditionerPhase.Processing,
                "Frozen v15 conditioning progress loads into the current game without advancing time");
            Check(File.ReadAllBytes(shop.SavePath).Length == original.Length && shop.Model.MistResearchReady && !shop.Model.MistResearchUnlocked,
                "Migration is read-only; placed vat and discovered Lanternfin satisfy the new gate");
            Check(shop.World.Stations.Count == 9 && shop.Model.Capacity == 4 && !shop.Model.UpgradeOwned,
                "Fourth culture adds no station, floor machine or required siphon upgrade");
            string vat = shop.Model.ConditionerIds[0];
            yield return ConditioningPanel(vat); yield return Capture("190-airfin-research");
            Check(shop.ResearchMist() && shop.Model.Currency == 310 && shop.Model.MistResearchUnlocked &&
                shop.Model.GetGrower(CultureKind.Mist).Units == 4,
                "Physical nighttime vat buys renewable Mist once for 40 credits");
            Check(!shop.ResearchMist() && shop.Model.Currency == 310, "Repeat research cannot pay twice");
            Check(File.Exists(shop.SavePath + ".v15.bak") && File.ReadAllBytes(shop.SavePath + ".v15.bak").Length == original.Length,
                "First current save preserves immutable frozen v15 backup");
            yield return ConditioningPanel(vat); yield return Capture("191-airfin-mist-vat");
            yield return ResearchHarvest(CultureKind.Bloom);
            yield return ResearchHarvest(CultureKind.Flow);
            yield return MistHarvest(vat);
            Check(shop.Model.Available(CultureKind.Mist) == 1 && shop.Model.ReservedCultureCount == 4,
                "Held mouse at the real vat fills the original four-unit siphon beside active conditioning");
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Bloom)), "Dose Driftwing Bloom");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Flow)), "Dose Driftwing Flow");
            Ok(shop.Perform(shop.Model.Dose(CultureKind.Mist)), "Dose Driftwing Mist");
            Check(shop.Model.Incubator.Bloom == 1 && shop.Model.Incubator.Flow == 1 && shop.Model.Incubator.Spark == 0 && shop.Model.Incubator.Mist == 1,
                "Four-culture recipe stages exact Airfin ingredients");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Begin ordinary Driftwing incubation");
            float began = Time.realtimeSinceStartup;
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "ordinary Driftwing maturation");
            string first = shop.Model.Incubator.SpecimenId;
            Check(Time.realtimeSinceStartup - began >= shop.Config.IncubationSeconds - .1f &&
                shop.Model.IsDiscovered("driftwing") && shop.Model.GetSpecies("driftwing").FamilyId == "Airfin" &&
                shop.Model.GetPrice("driftwing").Value.Standard == 18,
                "Normal-time maturation discovers one distinct Airfin at standard price 18");
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            yield return Capture("192-airfin-ready");
            AuditHatcheryCreature();
            shop.SetPanel(ShopPanel.None); yield return Capture("255-hatchery-driftwing-ready");
            yield return SpecimenRackPanel("table-1");
            Check(shop.StoreReadyInRack("table-1-storage-2"), "Physical rack stores the exact first Airfin away from customers");
            yield return SpecimenDisplayPanel("tank-a");
            Check(shop.StockDisplayFromStorage("table-1-storage-2", first) && shop.Model.GetTank("tank-a").Value.SpeciesId == "driftwing",
                "Physical front display receives exact Airfin from hidden lab rack");
            yield return SpecimenBounds("Driftwing front display");
            shop.SetPanel(ShopPanel.Journal); yield return Capture("193-airfin-journal");
            yield return SpecimenDisplayPanel("tank-a"); yield return Capture("194-airfin-display");
            LabRoutesCheck("Airfin lab");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open store with Driftwing stock");
            var buyer = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => buyer.Phase == CustomerPhase.Queued, 45, "Airfin buyer queues");
            Check(buyer.Price == 18 && buyer.SpecimenId == first, "Real visitor reserves exact Airfin for 18 credits");
            yield return ServeNext("Airfin buyer");
            Check(shop.Model.Currency == 328 && !shop.Model.GetTank("tank-a").HasValue && !shop.ServeCustomer(),
                "Airfin checkout pays once and removes exact display stock");
            yield return WaitFor(() => shop.Customers.ActiveCount == 0, 20, "Airfin buyer exits");
            Check(shop.CloseForNight(), "Close Airfin trading day");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "Airfin closing");
            Check(shop.Reports.Last.sales == 1 && shop.Reports.Last.revenue == 18 && shop.Reports.Last.pathProblems == 0,
                "Daily report records one real Airfin sale and no route problem");

            yield return ResearchHarvest(CultureKind.Bloom);
            yield return ResearchHarvest(CultureKind.Flow);
            yield return MistHarvest(vat);
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Ok(shop.Perform(shop.Model.UseKnownRecipe("driftwing")), "Known Airfin recipe stages in one action");
            Ok(shop.Perform(shop.Model.BeginIncubation()), "Start repeat Airfin before restart");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.Model.Incubator.Phase == IncubatorPhase.Incubating && shop.Model.Currency == 328,
                "Saved active Airfin, paid research and committed sale for separate process");
            File.Copy(shop.SavePath, Path.Combine(evidence, "airfin-active-save.json"), true);
        }

        IEnumerator MistHarvest(string vat)
        {
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); yield return null;
            bool access = shop.Layout.TryGetConditionerAccessPoint(vat, out float x, out float z);
            bool placed = shop.World.TryGetConditioner(vat, out var root);
            Check(access && placed,
                "Find Mist grower on the placed conditioning vat");
            AimAt(new Vector3(x, .2f, z), root.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return null; yield return null;
            Check(shop.Focused && shop.TryStationCulture(shop.Focused, out var culture) && culture == CultureKind.Mist,
                "Normal crosshair focus resolves Mist at the exact vat");
            int before = shop.Model.Available(CultureKind.Mist);
            float deadline = Time.realtimeSinceStartup + 10;
            while (shop.Model.Available(CultureKind.Mist) == before)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Mist harvest at placed vat");
                shop.Player.View.transform.LookAt(root.TransformPoint(new Vector3(0, 1.1f, 0)));
                cycleMouse.MakeCurrent(); InputSystem.QueueStateEvent(cycleMouse, new MouseState { buttons = 1 });
                yield return null;
            }
            yield return PlacementNeutral();
            Check(shop.Model.Available(CultureKind.Mist) == before + 1, "Held left mouse collects one renewable Mist dose");
        }

        IEnumerator AirfinRestart()
        {
            shop.SetPanel(ShopPanel.Pause);
            Check(File.Exists(shop.SavePath) && shop.ReloadSaved(), "Fresh process loads supplied active Airfin save");
            var before = shop.Model.Incubator;
            Check(before.Phase == IncubatorPhase.Incubating && before.SpeciesId == "driftwing" &&
                shop.Model.MistResearchUnlocked && shop.Model.Currency == 328 && shop.Reports.Last.revenue == 18,
                "Restart preserves paid Mist research, active Airfin and committed sale");
            yield return new WaitForSeconds(.4f);
            Check(shop.Model.Incubator.SecondsRemaining == before.SecondsRemaining, "Paused restart and closed-app time do not advance incubation");
            shop.SetPanel(ShopPanel.None);
            yield return WaitFor(() => shop.Model.Incubator.Phase == IncubatorPhase.Ready, 16, "restarted Airfin maturity");
            Check(shop.Model.Incubator.SpecimenId == before.SpecimenId && shop.Model.DiscoveredSpeciesIds.Count == 5,
                "Restart keeps exact repeat specimen and does not rediscover the Airfin");
            yield return SpecimenDisplayPanel("tank-b");
            Check(shop.TransferToDisplay("tank-b").Success && shop.Model.GetTank("tank-b").Value.Id == before.SpecimenId,
                "Fresh process physically displays exact resumed Airfin");
            yield return SpecimenBounds("Restarted Airfin display");
            yield return Capture("195-airfin-restarted-display");
            Check(shop.SaveNow() && shop.Model.Currency == 328 && shop.Model.MistResearchUnlocked,
                "Fresh process saves playable Airfin, research and earned cash without paying again");
        }
    }
}
#endif
