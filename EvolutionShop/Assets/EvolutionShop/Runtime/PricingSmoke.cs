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
        /// <summary>
        /// R03.1: price tags, refusals, redirect, agreed-price snapshots, reactions, reports and save migration,
        /// with real customer navigation and real E input. Fixtures use recipe commands with accelerated model ticks.
        /// </summary>
        IEnumerator PricingScenario()
        {
            limitations.Add("Price tags are set through ShopSession.SetSpeciesPrice, the command the price buttons call; native GUI clicks on the new buttons are not part of this run.");
            limitations.Add("Fixture stock uses recipe commands with accelerated model ticks. Customer walking, browsing, refusal, redirect, queueing and checkout use real navigation frames. One customer type; no pacing or balance claim.");
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionPricingSmoke") != null, "Pricing scenario requires isolated evolutionSmoke and evolutionPricingSmoke flags");
            Check(Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Pricing scenario uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            Check(shop.Customers.Ready, "Customer navigation ready for the pricing scenario / " + shop.Customers.Navigation.LastError);
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionPricingKeyboard");
            syntheticKeyboard.MakeCurrent();

            var model = shop.Model;
            Check(model.GetPrice("pondlet").Value.Price == 6 && model.GetPrice("bubblebelly").Value.Price == 10 &&
                model.GetPrice("ribbonfin").Value.Price == 10, "Fresh shop tags every species at its standard price");
            Check(shop.SetSpeciesPrice("pondlet", 8).Failure == FailureCode.UndiscoveredRecipe && model.GetPrice("pondlet").Value.Price == 6,
                "An undiscovered species cannot be priced");
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            string refusedPondlet = model.GetTank("tank-a").Value.Id;
            Check(shop.SetSpeciesPrice("pondlet", 0).Failure == FailureCode.InvalidPrice &&
                shop.SetSpeciesPrice("pondlet", 19).Failure == FailureCode.InvalidPrice && model.GetPrice("pondlet").Value.Price == 6,
                "Tags outside 1-18 credits are refused and leave the Pondlet tag unchanged");
            Ok(shop.SetSpeciesPrice("pondlet", 12), "Tag the Pondlet at 12 credits");
            Check(shop.Customers.Preview(model.GetPrice("pondlet").Value) == PriceReaction.TooExpensive &&
                shop.Customers.Preview(model.GetPrice("bubblebelly").Value) == PriceReaction.Fair,
                "Preview reads Too expensive for the 12-credit Pondlet and Fair price for the 10-credit Bubblebelly");

            yield return PressEAt(StationKind.TankA);
            Check(shop.Panel == ShopPanel.Tank && shop.SelectedTank == "tank-a", "Real E input at a stocked display opens its price tag");
            yield return Capture("40-price-tag-panel");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.GetPrice("pondlet").Value.Price == 12 &&
                shop.Model.GetPrice("bubblebelly").Value.Price == 10 && shop.Model.GetTank("tank-a").Value.Id == refusedPondlet,
                "Price tags and stock survive a real file reload at night");
            model = shop.Model;

            shop.Customers.ArrivalsEnabled = true;
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the pricing day");
            yield return WaitForCustomer(CustomerPhase.Browsing, 15);
            Check(shop.Customers.Visit.TargetTankId == "tank-a", "The first visitor walks to the Pondlet display first");
            yield return WaitForVisit(v => v.PriceRejections == 1, 6, "the first price refusal");
            var first = shop.Customers.Visit;
            Check(first.Phase == CustomerPhase.Arriving && first.TargetTankId == "tank-b" && first.QuotedPrice == 12 &&
                !model.GetReservation("tank-a").HasValue && model.Currency == 0 && model.GetTank("tank-a").Value.Id == refusedPondlet,
                "The visitor refuses the 12-credit Pondlet without a claim and heads for the Bubblebelly");
            Check(shop.Customers.Caption.Contains("Too pricey") && shop.Customers.Actor.StatusLabel == shop.Customers.Caption,
                "The refusal is shown on the visitor: " + shop.Customers.Caption);
            AimAtCustomer(new Vector3(1, 0.2f, -2.5f));
            yield return Capture("41-price-refusal");
            yield return WaitForCustomer(CustomerPhase.ToCounter, 15);
            Check(first.Price == 10 && first.Reaction == PriceReaction.Fair && model.GetReservation("tank-b").Value.Price == 10 &&
                shop.Customers.Caption == "Fair price. I'll take it!", "The redirected visitor reserves the Bubblebelly at its fair 10-credit tag");
            Ok(shop.SetSpeciesPrice("bubblebelly", 14), "Raise the Bubblebelly tag to 14 while the order walks to checkout");
            Check(model.GetReservation("tank-b").Value.Price == 10 && first.Price == 10, "Repricing leaves the agreed reservation price at 10");
            yield return WaitForCustomer(CustomerPhase.Queued, 15);
            yield return EnterCounter();
            yield return Capture("42-agreed-price-order");
            Check(shop.ServeCustomer() && model.Currency == 10 && model.GetTank("tank-b") == null &&
                model.GetTank("tank-a").Value.Id == refusedPondlet, "Checkout pays the agreed 10 credits, not the new 14 tag, and keeps the refused Pondlet");
            Check(!shop.ServeCustomer() && model.Currency == 10, "Repeated checkout cannot pay again");
            Check(shop.Reports.Current.sales == 1 && shop.Reports.Current.revenue == 10 && shop.Reports.Current.tooExpensive == 0,
                "A refusal followed by a purchase records one sale and no lost visit");
            yield return WaitForNoCustomer(12);

            yield return WaitForCustomer(CustomerPhase.Leaving, 30);
            var second = shop.Customers.Visit;
            Check(second.Outcome == CustomerOutcome.TooExpensive && second.PriceRejections == 1 && second.QuotedPrice == 12 &&
                !model.GetReservation("tank-a").HasValue && model.Currency == 10 && model.GetTank("tank-a").Value.Id == refusedPondlet,
                "With only an overpriced display left, the visitor leaves for price with no claim or payment");
            Check(shop.Reports.Current.tooExpensive == 1 && shop.Customers.Caption == "12 credits?! Too expensive!" &&
                shop.Customers.Status == "The customer left: too expensive", "The day report counts the refusal and the visitor and status say why");
            AimAtCustomer(new Vector3(-0.5f, 0.2f, -2.2f));
            yield return Capture("43-too-expensive-leaving");
            Ok(shop.SetSpeciesPrice("pondlet", 8), "Lower the Pondlet tag to 8 credits");
            Check(shop.Customers.Preview(model.GetPrice("pondlet").Value) == PriceReaction.Pricey, "Preview reads Pricey for the 8-credit Pondlet");
            yield return WaitForNoCustomer(12);

            yield return WaitForCustomer(CustomerPhase.Queued, 30);
            var third = shop.Customers.Visit;
            Check(third.SpecimenId == refusedPondlet && third.Price == 8 && third.Reaction == PriceReaction.Pricey,
                "The next visitor accepts the Pondlet at a pricey 8 credits");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved(), "Save and reload a queued pricey order");
            model = shop.Model;
            Check(shop.Customers.Visit == null && !model.GetReservation("tank-a").HasValue && model.GetTank("tank-a").Value.Id == refusedPondlet &&
                model.GetPrice("pondlet").Value.Price == 8 && model.GetPrice("bubblebelly").Value.Price == 14 && model.Currency == 10,
                "Reload releases the unpaid claim and keeps the tags, the exact Pondlet and earned money");
            Check(shop.Reports.Current.interrupted == 1 && shop.Reports.Current.tooExpensive == 1 && shop.Reports.Current.revenue == 10,
                "Reload counts the unpaid order as interrupted and keeps the refusal and income");
            yield return EnterCounter();
            yield return WaitForCustomer(CustomerPhase.Queued, 30);
            Check(shop.Customers.Visit.Price == 8 && shop.ServeCustomer() && shop.Model.Currency == 18, "A new visitor buys the Pondlet for the pricey 8 credits");
            yield return WaitForNoCustomer(12);

            SeedStock("pondlet", "tank-a");
            Ok(shop.SetSpeciesPrice("pondlet", 4), "Tag the restocked Pondlet at a bargain 4 credits");
            yield return WaitForCustomer(CustomerPhase.ToCounter, 30);
            var fifth = shop.Customers.Visit;
            Check(fifth.Price == 4 && fifth.Reaction == PriceReaction.Bargain && shop.Customers.Caption == "A bargain at 4!",
                "A bargain tag reads as a bargain on the visitor");
            AimAtCustomer(new Vector3(0.5f, 0.2f, -2.4f));
            yield return Capture("44-bargain-reaction");
            yield return WaitForCustomer(CustomerPhase.Queued, 15);
            yield return EnterCounter();
            Check(shop.ServeCustomer() && shop.Model.Currency == 22, "Checkout pays the bargain 4 credits");
            yield return WaitForNoCustomer(12);

            Check(shop.CloseForNight(), "Close the pricing day");
            yield return WaitForNight(6);
            var report = shop.Reports.Last;
            Check(shop.Panel == ShopPanel.DayReport && report.visitors == 5 && report.sales == 3 && report.revenue == 22 &&
                report.tooExpensive == 1 && report.interrupted == 1 && report.noStock == 0 && report.restocked == 1,
                "The day report reconciles three sales (10 + 8 + 4), one refusal and one interrupted order");
            yield return Capture("45-price-day-report");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Reports.Last.tooExpensive == 1 && shop.Model.Currency == 22 &&
                shop.Model.GetPrice("pondlet").Value.Price == 4 && shop.Model.GetPrice("bubblebelly").Value.Price == 14,
                "The report, money and tags survive a night reload");
            File.Copy(shop.SavePath, Path.Combine(evidence, "pricing-progress.json"), true);
            shop.SetPanel(ShopPanel.None);

            SeedStock("pondlet", "tank-a");
            ReadyOutput("ribbonfin");
            string waiting = shop.Model.Incubator.SpecimenId;
            string displayed = shop.Model.GetTank("tank-a").Value.Id;
            yield return PressEAt(StationKind.TankA);
            Check(shop.Panel == ShopPanel.Tank && shop.Model.Incubator.Phase == IncubatorPhase.Ready &&
                shop.Model.Incubator.SpecimenId == waiting && shop.Model.GetTank("tank-a").Value.Id == displayed,
                "E at an occupied display with ready output opens its price tag and moves nothing");
            yield return Capture("46-full-display-price-tag");
            shop.SetPanel(ShopPanel.None);
            yield return PressEAt(StationKind.TankB);
            Check(shop.Model.GetTank("tank-b").HasValue && shop.Model.GetTank("tank-b").Value.Id == waiting && shop.Panel == ShopPanel.None,
                "E at an empty display still transfers the ready specimen");

            shop.Customers.ArrivalsEnabled = false;
            shop.ConfirmNewGame();
            Check(shop.Model.GetPrice("pondlet").Value.Price == 6 && shop.Model.CaptureSave().prices.Length == 0, "Confirmed new game resets price tags");
            SeedStock("pondlet", "tank-a");
            var legacyGame = shop.Model.CaptureSave(); legacyGame.version = 1; legacyGame.prices = null;
            var legacyTrades = shop.Reports.CaptureSave(); legacyTrades.version = 1;
            string legacyJson = "{\"formatVersion\":3,\"game\":" + JsonUtility.ToJson(legacyGame, true) + ",\"cycle\":" +
                JsonUtility.ToJson(shop.Cycle.CaptureSave(), true) + ",\"trades\":" + JsonUtility.ToJson(legacyTrades, true) +
                ",\"sensitivity\":" + shop.Sensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"volume\":" + shop.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
            File.WriteAllText(shop.SavePath, legacyJson); // Isolated smoke slot only; the delivered R02 player's format.
            string legacyPondlet = shop.Model.GetTank("tank-a").Value.Id;
            Check(shop.ReloadSaved() && shop.Model.GetTank("tank-a").Value.Id == legacyPondlet && shop.Model.GetPrice("pondlet").Value.Price == 6 &&
                File.ReadAllText(shop.SavePath) == legacyJson, "Runtime reload of an R02 format-3 save keeps stock at standard prices without writing");
            Check(shop.SaveNow() && File.ReadAllText(shop.SavePath + ".v3.bak") == legacyJson &&
                JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)).formatVersion == SaveStore.CurrentFormatVersion,
                "The first new save archives the exact format-3 bytes and writes the current format");
            shop.ConfirmNewGame();
        }

        IEnumerator PressEAt(StationKind kind)
        {
            shop.SetPanel(ShopPanel.None);
            var station = StationOf(kind).transform;
            shop.Player.Teleport(station.TransformPoint(new Vector3(0, 0.2f, -2.5f)), station.rotation);
            shop.Player.View.transform.LookAt(station.TransformPoint(new Vector3(0, 1.1f, 0)));
            yield return null; yield return null;
            Check(shop.Focused != null && shop.Focused.Kind == kind, "Aim finds " + kind + " before pressing E");
            yield return MoveFor(Key.E, 0.05f);
        }

        IEnumerator WaitForVisit(Func<CustomerVisit, bool> condition, float timeout, string reason)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (shop.Customers.Visit == null || !condition(shop.Customers.Visit))
            {
                ObserveCustomerNavigation();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Pricing scenario waiting for " + reason + "; current=" + shop.Customers.Status);
                yield return null;
            }
        }

        void ReadyOutput(string speciesId)
        {
            var model = shop.Model;
            model.Tick(100);
            var recipe = model.GetSpecies(speciesId);
            for (int i = 0; i < recipe.Bloom; i++) if (!model.Collect(CultureKind.Bloom).Success || !model.Dose(CultureKind.Bloom).Success) throw new InvalidOperationException("Ready Bloom");
            for (int i = 0; i < recipe.Flow; i++) if (!model.Collect(CultureKind.Flow).Success || !model.Dose(CultureKind.Flow).Success) throw new InvalidOperationException("Ready Flow");
            if (!model.BeginIncubation().Success) throw new InvalidOperationException("Ready incubation");
            model.Tick(shop.Config.IncubationSeconds);
            if (model.Incubator.Phase != IncubatorPhase.Ready) throw new InvalidOperationException("Ready output");
        }
    }
}
#endif
