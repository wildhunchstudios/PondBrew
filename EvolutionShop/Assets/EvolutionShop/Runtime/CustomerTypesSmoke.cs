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
        int typesNavigationSamples, expectedSales, expectedRevenue, expectedRestocks;

        /// <summary>
        /// R03.2: four customer types, two visitors at once, a two-place counter queue, races and recovery,
        /// with real navigation and E input. Scripted steps choose types and start arrivals with validation hooks.
        /// </summary>
        IEnumerator CustomerTypesScenario()
        {
            limitations.Add("Scripted steps choose each visitor's type and start arrivals with ShopCustomers.SpawnNow (validation hooks); walking, browsing, judging, queueing, moving up and leaving use real navigation frames. Natural weighted arrivals are checked on day 2.");
            limitations.Add("Fixture stock uses recipe commands with accelerated model ticks; one patience expiry uses an accelerated delta. No balance, pacing or enjoyment claim.");
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionCustomerTypesSmoke") != null, "Customer-types scenario requires isolated evolutionSmoke and evolutionCustomerTypesSmoke flags");
            Check(Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Customer-types scenario uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            var customers = shop.Customers;
            var nav = customers.Navigation;
            Check(customers.Ready, "Customer navigation ready with two browse spots per display and two queue places / " + nav.LastError);
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionCustomerTypesKeyboard");
            syntheticKeyboard.MakeCurrent();

            Check(customers.MaxVisitors == 2 && customers.ForcedArchetypeId == null && customers.Archetypes.Count == 4 &&
                customers.Archetypes[0].Id == CustomerArchetypes.Casual && customers.Archetypes[1].Id == CustomerArchetypes.Bargain &&
                customers.Archetypes[2].Id == CustomerArchetypes.Collector && customers.Archetypes[3].Id == CustomerArchetypes.Premium,
                "Normal play allows two visitors at once and four customer types");
            Check(nav.CanReach(nav.EntryPoint, nav.BrowseSpot("tank-a", 1)) && nav.CanReach(nav.EntryPoint, nav.BrowseSpot("tank-b", 1)) &&
                nav.CanReach(nav.BrowseSpot("tank-a", 1), nav.QueueSpot(1)) && nav.CanReach(nav.QueueSpot(1), nav.ExitPoint) &&
                Flat(nav.QueueSpot(0), nav.QueueSpot(1)) > .9f && Flat(nav.BrowseSpot("tank-a", 0), nav.BrowseSpot("tank-a", 1)) > .7f,
                "Second browse spots and the second queue place are walkable and spaced apart");
            customers.ArrivalsEnabled = false;

            // A: two types at once, queue order, serving in order, move-up after service.
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the customer-types day");
            var bargain = Spawn(CustomerArchetypes.Bargain);
            yield return Hold(4f);
            var collector = Spawn(CustomerArchetypes.Collector);
            Check(bargain.TargetTankId == "tank-a" && collector.TargetTankId == "tank-b",
                "The collector heads for its favourite Bubblebelly while the bargain hunter takes the first display");
            yield return WaitFor(() => collector.Phase == CustomerPhase.ToCounter, 25, "the collector agreeing a price");
            Check(collector.Price == 10 && collector.Reaction == PriceReaction.Fair && customers.CaptionOf(collector) == "Just what I collect!" &&
                customers.ActorOf(collector).StatusLabel == "Just what I collect!", "The collector accepts its favourite at the fair 10 credits and says so");
            yield return WaitFor(() => bargain.Phase == CustomerPhase.Queued && collector.Phase == CustomerPhase.Queued, 25, "both visitors queued");
            Check(customers.ActiveCount == 2 && bargain.Price == 6 && bargain.Reaction == PriceReaction.Fair,
                "Two visitors of different types wait at once; the bargain hunter agreed the Pondlet at 6");
            Check(customers.QueuePlaceOf(bargain) == 0 && customers.QueuePlaceOf(collector) == 1 && customers.NextToServe == bargain,
                "The first agreed order holds the first queue place and is served next");
            yield return WaitFor(() => Flat(ActorPosition(bargain), nav.QueueSpot(0)) < .5f && Flat(ActorPosition(collector), nav.QueueSpot(1)) < .5f,
                10, "both visitors standing in their queue places");
            Check(true, "Both visitors physically stand in queue places 0 and 1");
            yield return SignsStaggered(bargain, collector, "queued bargain hunter and collector");
            AimAt(new Vector3(-0.6f, 0.2f, -1.2f), (nav.QueueSpot(0) + nav.QueueSpot(1)) * .5f + Vector3.up * 1.2f);
            yield return Capture("50-two-customers-queue");
            yield return EnterCounter();
            yield return Capture("51-counter-queue");
            yield return ServeNext("The bargain hunter at the front");
            Check(collector.Phase == CustomerPhase.Queued && shop.Model.GetTank("tank-b").HasValue, "Serving the front leaves the collector's order untouched");
            yield return WaitFor(() => customers.QueuePlaceOf(collector) == 0 && Flat(ActorPosition(collector), nav.QueueSpot(0)) < .5f, 12,
                "the collector moving up to the counter");
            Check(true, "The collector physically moves up to the first queue place");
            yield return ServeNext("The collector");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "both buyers leaving");

            // B: a budget refusal, then the same type buying at a lower tag.
            SeedStock("bubblebelly", "tank-b"); expectedRestocks++;
            string bubblebelly = shop.Model.GetTank("tank-b").Value.Id;
            var thrifty = Spawn(CustomerArchetypes.Bargain);
            yield return WaitFor(() => thrifty.Phase == CustomerPhase.Leaving, 25, "the bargain hunter judging the Bubblebelly");
            Check(thrifty.Outcome == CustomerOutcome.TooExpensive && thrifty.RefusedOverBudget && thrifty.QuotedPrice == 10 &&
                customers.CaptionOf(thrifty) == "10 credits? Over my budget!" && !shop.Model.GetReservation("tank-b").HasValue &&
                shop.Model.GetTank("tank-b").Value.Id == bubblebelly, "The bargain hunter refuses a 10-credit Bubblebelly as over its budget of 8, with no claim");
            AimAtVisit(thrifty, new Vector3(1.2f, 0.2f, -2.6f));
            yield return Capture("52-over-budget");
            Ok(shop.SetSpeciesPrice("bubblebelly", 8), "Lower the Bubblebelly tag to 8");
            Check(customers.Preview(customers.Archetypes[1], shop.Model.GetPrice("bubblebelly").Value) == PriceReaction.Bargain,
                "Preview reads Bargain for the bargain hunter at 8");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "the refusing visitor leaving");
            var saver = Spawn(CustomerArchetypes.Bargain);
            yield return WaitFor(() => saver.Phase == CustomerPhase.ToCounter, 25, "the bargain hunter agreeing");
            Check(saver.Price == 8 && saver.Reaction == PriceReaction.Bargain && customers.CaptionOf(saver) == "A bargain at 8!",
                "A bargain hunter buys the Bubblebelly at 8 and calls it a bargain");
            yield return WaitFor(() => saver.Phase == CustomerPhase.Queued, 15, "the bargain hunter queued");
            yield return ServeNext("The bargain hunter");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "the buyer leaving");

            // C: a collector refuses a pricey non-favourite but pays a premium for its favourite.
            SeedStock("pondlet", "tank-a"); expectedRestocks++;
            Ok(shop.SetSpeciesPrice("pondlet", 7), "Tag the Pondlet at 7");
            yield return PressEAt(StationKind.TankA);
            Check(shop.Panel == ShopPanel.Tank, "Real E opens the Pondlet's price tag with every customer type's reaction");
            yield return Capture("56-price-panel-four-types");
            shop.SetPanel(ShopPanel.None);
            var picky = Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(() => picky.Phase == CustomerPhase.Leaving, 25, "the collector judging the Pondlet");
            Check(picky.Outcome == CustomerOutcome.TooExpensive && !picky.RefusedOverBudget && picky.QuotedPrice == 7 &&
                customers.CaptionOf(picky) == "7 credits?! Too expensive!", "A collector pays only a fair price for a Pondlet and refuses 7");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "the collector leaving");
            SeedStock("ribbonfin", "tank-b"); expectedRestocks++;
            Ok(shop.SetSpeciesPrice("ribbonfin", 25), "Tag the Ribbonfin at 25");
            var fan = Spawn(CustomerArchetypes.Collector);
            Check(fan.TargetTankId == "tank-b", "A collector goes to its favourite Ribbonfin before the first display's Pondlet");
            yield return WaitFor(() => fan.Phase == CustomerPhase.ToCounter, 25, "the collector agreeing a premium");
            Check(fan.Price == 25 && fan.Reaction == PriceReaction.Pricey && customers.CaptionOf(fan) == "Pricey, but I collect these!",
                "The collector pays a pricey 25 for its favourite and says why");
            AimAtVisit(fan, new Vector3(1.0f, 0.2f, -2.2f));
            yield return Capture("53-collector-favourite");
            yield return WaitFor(() => fan.Phase == CustomerPhase.Queued, 15, "the collector queued");
            yield return ServeNext("The collector");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "the collector leaving");

            // D: a second visitor spreads to the display nobody else is heading to.
            Ok(shop.SetSpeciesPrice("pondlet", 6), "Return the Pondlet tag to 6");
            SeedStock("pondlet", "tank-b"); expectedRestocks++;
            var left = Spawn(CustomerArchetypes.Casual);
            yield return Hold(1f);
            var right = Spawn(CustomerArchetypes.Casual);
            Check(left.TargetTankId == "tank-a" && right.TargetTankId == "tank-b" && customers.BrowseSpotOf(left) == 0 && customers.BrowseSpotOf(right) == 0,
                "A second visitor picks the stocked display nobody else is heading to");
            yield return WaitFor(() => left.Phase == CustomerPhase.Queued && right.Phase == CustomerPhase.Queued, 30, "both casual visitors queued");
            yield return ServeNext("The first casual visitor");
            yield return ServeNext("The second casual visitor, from the second place");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "both casual buyers leaving");

            // E: two visitors race for the last specimen at one display.
            SeedStock("pondlet", "tank-a"); expectedRestocks++;
            var racerA = Spawn(CustomerArchetypes.Collector);
            yield return Hold(.4f);
            var racerB = Spawn(CustomerArchetypes.Collector);
            Check(racerA.TargetTankId == "tank-a" && racerB.TargetTankId == "tank-a" && customers.BrowseSpotOf(racerA) != customers.BrowseSpotOf(racerB),
                "With one stocked display, two visitors share it from separate browse spots");
            yield return WaitFor(() => racerA.Phase == CustomerPhase.Browsing && racerB.Phase == CustomerPhase.Browsing, 15, "both visitors browsing one display");
            Check(Flat(ActorPosition(racerA), nav.BrowseSpot("tank-a", customers.BrowseSpotOf(racerA))) < .5f &&
                Flat(ActorPosition(racerB), nav.BrowseSpot("tank-a", customers.BrowseSpotOf(racerB))) < .5f,
                "Both visitors stand at their own browse spots side by side");
            yield return SignsStaggered(racerA, racerB, "two visitors sharing one display");
            AimAt(new Vector3(.5f, .2f, -2.6f), nav.BrowseSpot("tank-a", 0) + Vector3.up * 1.2f);
            yield return Capture("54-shared-display");
            yield return WaitFor(() => (racerA.Phase == CustomerPhase.ToCounter && racerB.Phase == CustomerPhase.Leaving) ||
                (racerB.Phase == CustomerPhase.ToCounter && racerA.Phase == CustomerPhase.Leaving), 10, "the race for the last Pondlet");
            var winner = racerA.Phase == CustomerPhase.ToCounter ? racerA : racerB;
            var loser = winner == racerA ? racerB : racerA;
            Check(loser.Outcome == CustomerOutcome.NoStock && loser.SpecimenId == null &&
                shop.Model.GetReservation("tank-a").Value.CustomerId == winner.Id, "Exactly one visitor wins the last Pondlet; the other leaves without a claim");
            yield return WaitFor(() => winner.Phase == CustomerPhase.Queued, 15, "the winner queued");
            yield return ServeNext("The race winner");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "the race visitors leaving");

            // F: the impatient front customer leaves and the next moves up.
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b"); expectedRestocks += 2;
            Ok(shop.SetSpeciesPrice("bubblebelly", 10), "Return the Bubblebelly tag to 10");
            string waitingPondlet = shop.Model.GetTank("tank-a").Value.Id;
            var hurried = Spawn(CustomerArchetypes.Premium);
            yield return Hold(2f);
            var calm = Spawn(CustomerArchetypes.Casual);
            Check(hurried.TargetTankId == "tank-a" && calm.TargetTankId == "tank-b", "The big spender and the casual visitor go to different displays");
            yield return WaitFor(() => hurried.Phase == CustomerPhase.Queued && calm.Phase == CustomerPhase.Queued, 30, "both visitors queued");
            Check(customers.QueuePlaceOf(hurried) == 0 && customers.QueuePlaceOf(calm) == 1 && hurried.QueueSecondsRemaining <= 30f &&
                calm.QueueSecondsRemaining > 30f, "The big spender is first in line but waits less than the casual visitor");
            yield return SignsStaggered(hurried, calm, "big spender in front of a casual visitor");
            AimAt(new Vector3(-0.6f, 0.2f, -1.2f), (nav.QueueSpot(0) + nav.QueueSpot(1)) * .5f + Vector3.up * 1.2f);
            yield return Capture("55-big-spender-and-casual");
            shop.Advance(31f); // Accelerate only this patience expiry.
            Check(hurried.Outcome == CustomerOutcome.Impatient && !shop.Model.GetReservation("tank-a").HasValue &&
                shop.Model.GetTank("tank-a").Value.Id == waitingPondlet && calm.Phase == CustomerPhase.Queued,
                "The impatient big spender leaves first and releases the Pondlet; the casual visitor keeps waiting (accelerated delta)");
            yield return WaitFor(() => customers.QueuePlaceOf(calm) == 0 && Flat(ActorPosition(calm), nav.QueueSpot(0)) < .5f, 12,
                "the casual visitor moving up after the impatient departure");
            Check(true, "The casual visitor physically moves up after the impatient departure");
            yield return ServeNext("The casual visitor");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "visitors leaving");

            // G: reloading with two unpaid orders releases both and counts both as interrupted.
            SeedStock("bubblebelly", "tank-b"); expectedRestocks++;
            string keptA = shop.Model.GetTank("tank-a").Value.Id, keptB = shop.Model.GetTank("tank-b").Value.Id;
            var g1 = Spawn(CustomerArchetypes.Casual);
            yield return Hold(1f);
            var g2 = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => Reserved(g1) && Reserved(g2), 25, "two unpaid orders");
            int interrupted = shop.Reports.Current.interrupted, money = shop.Model.Currency;
            Check(shop.Reports.PendingCount == 2, "The day report holds two unresolved visitors");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved(), "Save and reload with two unpaid orders");
            Check(shop.Customers.ActiveCount == 0 && !shop.Model.GetReservation("tank-a").HasValue && !shop.Model.GetReservation("tank-b").HasValue &&
                shop.Model.GetTank("tank-a").Value.Id == keptA && shop.Model.GetTank("tank-b").Value.Id == keptB && shop.Model.Currency == money,
                "Reload releases both claims and keeps both creatures and the money");
            Check(shop.Reports.Current.interrupted == interrupted + 2 && !shop.Reports.HasPending, "Both unpaid visits count once each as interrupted");

            // H: closing with two in line: one is served in the grace period, the other leaves unpaid.
            yield return EnterCounter();
            var h1 = Spawn(CustomerArchetypes.Casual);
            yield return Hold(2f);
            var h2 = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => h1.Phase == CustomerPhase.Queued && h2.Phase == CustomerPhase.Queued, 30, "two orders in line before closing");
            Check(shop.CloseForNight(), "Close with two orders in line");
            yield return ServeNext("The first order during the closing grace");
            var unpaid = h1.Outcome == CustomerOutcome.Sold ? h2 : h1;
            string unpaidTank = unpaid.TargetTankId, unpaidSpecimen = unpaid.SpecimenId;
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 6, "night after closing");
            Check(unpaid.Outcome == CustomerOutcome.Closed && !shop.Model.GetReservation(unpaidTank).HasValue &&
                shop.Model.GetTank(unpaidTank).Value.Id == unpaidSpecimen, "The second order is released at night with its creature kept and no payment");
            var report = shop.Reports.Last;
            Check(report.visitors == 16 && report.sales == expectedSales && report.revenue == expectedRevenue && report.tooExpensive == 2 &&
                report.noStock == 1 && report.impatient == 1 && report.interrupted == 2 && report.closed == 1 && report.pathProblems == 0 &&
                report.restocked == expectedRestocks,
                "The day report reconciles 16 visitors: " + expectedSales + " sales for " + expectedRevenue + ", 2 too expensive, 1 no stock, 1 impatient, 2 interrupted, 1 closed");
            yield return Capture("57-two-visitor-day-report");
            shop.SetPanel(ShopPanel.Journal);
            shop.ScrollPanel(10000);
            yield return null;
            yield return Capture("59-journal-customers");
            shop.SetPanel(ShopPanel.None);

            // I: unscripted arrivals follow the deterministic weighted rotation and look like their types.
            if (!shop.Model.GetTank("tank-a").HasValue) SeedStock("pondlet", "tank-a");
            if (!shop.Model.GetTank("tank-b").HasValue) SeedStock("bubblebelly", "tank-b");
            customers.ArrivalsEnabled = true;
            yield return EnterCounter();
            Check(shop.OpenForDay() && shop.Cycle.DayNumber == 2, "Open day 2 with natural arrivals");
            var arrivals = new List<CustomerVisit>();
            var looks = new List<string>();
            yield return WaitFor(() =>
            {
                foreach (var visit in customers.Visits)
                    if (!arrivals.Contains(visit)) { arrivals.Add(visit); looks.Add(customers.ActorOf(visit).PresentationId); }
                return arrivals.Count >= 2;
            }, 30, "two natural arrivals");
            customers.ArrivalsEnabled = false;
            for (int i = 0; i < 2; i++)
            {
                var expected = CustomerArchetypes.Pick(customers.Archetypes, 2, i);
                Check(arrivals[i].ArchetypeId == expected.Id && looks[i] == expected.PresentationId,
                    "Natural arrival " + (i + 1) + " on day 2 is the deterministic weighted choice (" + expected.DisplayName + ") and looks like it");
            }
            if (customers.ActiveCount == 2)
            {
                AimAt(new Vector3(0.2f, 0.2f, -1.0f), shop.Customers.Actor.transform.position + Vector3.up * 1.2f);
                yield return Capture("58-natural-arrivals");
            }
            Check(shop.CloseForNight(), "Close day 2");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night && customers.ActiveCount == 0, 8, "night 3");
            Check(typesNavigationSamples > 500, "Observed " + typesNavigationSamples + " visitor navigation samples, all inside the front store with at most two visitors");
            Check(nav.BuildCount == 1, "Every step and reload reused one navigation build");

            // J: an R03.1 format-4 save reloads with its tags and is archived on the first new save.
            shop.ConfirmNewGame();
            SeedStock("pondlet", "tank-a");
            Ok(shop.SetSpeciesPrice("pondlet", 8), "Tag a Pondlet at 8 for the format-4 fixture");
            var trades = shop.Reports.CaptureSave(); trades.version = 2; trades.pendingCustomerIds = null;
            string formatFour = "{\"formatVersion\":4,\"game\":" + JsonUtility.ToJson(shop.Model.CaptureSave(), true) + ",\"cycle\":" +
                JsonUtility.ToJson(shop.Cycle.CaptureSave(), true) + ",\"trades\":" + JsonUtility.ToJson(trades, true) +
                ",\"sensitivity\":" + shop.Sensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"volume\":" + shop.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
            File.WriteAllText(shop.SavePath, formatFour); // Isolated smoke slot only; the R03.1 player's format.
            Check(shop.ReloadSaved() && shop.Model.GetPrice("pondlet").Value.Price == 8 && File.ReadAllText(shop.SavePath) == formatFour,
                "Runtime reload of an R03.1 format-4 save keeps its price tags without writing");
            Check(shop.SaveNow() && File.ReadAllText(shop.SavePath + ".v4.bak") == formatFour &&
                JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)).formatVersion == SaveStore.CurrentFormatVersion,
                "The first new save archives the exact format-4 bytes and writes the current format");
            shop.ConfirmNewGame();
        }

        CustomerVisit Spawn(string type)
        {
            var customers = shop.Customers;
            customers.ScriptedArchetypes.Enqueue(type);
            int before = customers.ActiveCount;
            bool started = customers.SpawnNow();
            if (!started) customers.ScriptedArchetypes.Clear();
            Check(started && customers.ActiveCount == before + 1, "Start a " + type + " visitor");
            var visit = customers.Visits[customers.Visits.Count - 1];
            var expected = CustomerArchetypes.Find(customers.Archetypes, type);
            Check(visit.ArchetypeId == type && customers.ActorOf(visit).PresentationId == expected.PresentationId,
                "The " + expected.DisplayName.ToLowerInvariant() + " arrives with its own look");
            return visit;
        }

        IEnumerator ServeNext(string label)
        {
            yield return EnterCounter();
            var next = shop.Customers.NextToServe;
            int before = shop.Model.Currency;
            int price = next == null ? -1 : next.Price;
            Check(next != null && next.Phase == CustomerPhase.Queued && shop.ServeCustomer() && shop.Model.Currency == before + price &&
                next.Outcome == CustomerOutcome.Sold, label + " pays the agreed " + price + " credits once");
            expectedSales++;
            expectedRevenue += price;
        }

        static bool Reserved(CustomerVisit visit) => visit.Phase == CustomerPhase.ToCounter || visit.Phase == CustomerPhase.Queued;

        IEnumerator SignsStaggered(CustomerVisit a, CustomerVisit b, string label)
        {
            var customers = shop.Customers;
            yield return WaitFor(() => customers.ActorOf(a) && customers.ActorOf(b) &&
                Mathf.Abs(customers.ActorOf(a).CaptionHeight - customers.ActorOf(b).CaptionHeight) >= .3f, 3, "staggered signs for " + label);
            Check(true, "Caption signs of the " + label + " are staggered at least 0.3 m so both stay readable");
        }

        Vector3 ActorPosition(CustomerVisit visit)
        {
            var actor = shop.Customers.ActorOf(visit);
            return actor ? actor.transform.position : new Vector3(999, 0, 999);
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        void AimAt(Vector3 playerPosition, Vector3 target)
        {
            shop.Player.Teleport(playerPosition, Quaternion.identity);
            shop.Player.View.transform.LookAt(target);
        }

        void AimAtVisit(CustomerVisit visit, Vector3 playerPosition)
        {
            var actor = shop.Customers.ActorOf(visit);
            AimAt(playerPosition, actor ? actor.transform.position + Vector3.up * 1.25f : playerPosition + Vector3.forward);
        }

        IEnumerator Hold(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { ObserveCustomers(); yield return null; }
        }

        IEnumerator WaitFor(Func<bool> condition, float timeout, string reason)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                ObserveCustomers();
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Customer types waiting for " + reason + "; status=" + shop.Customers.Status + "; nav=" + shop.Customers.Navigation.LastError);
                yield return null;
            }
            ObserveCustomers();
        }

        void ObserveCustomers()
        {
            var customers = shop.Customers;
            if (customers.ActiveCount > customers.MaxVisitors) throw new InvalidOperationException("Customer concurrency exceeded " + customers.MaxVisitors);
            foreach (var visit in customers.Visits)
            {
                var actor = customers.ActorOf(visit);
                if (!actor) continue;
                typesNavigationSamples++;
                if (!customers.Navigation.IsInStore(actor.transform.position)) throw new InvalidOperationException("A visitor entered the lab or left the front store");
            }
        }
    }
}
#endif
