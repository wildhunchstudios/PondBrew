#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        int customerNavigationSamples;
        IEnumerator CustomerScenario()
        {
            Check(shop.Customers.Ready, "Customer NavMesh built successfully in the running shop");
            var nav = shop.Customers.Navigation;
            // Earlier failed-save recovery deliberately rebakes before this customer scenario starts.
            // Customer activity and same-layout reloads must preserve the count observed here.
            int initialBuildCount = nav.BuildCount;
            Check(nav.CanReach(nav.EntryPoint, nav.BrowsePoint("tank-a")) && nav.CanReach(nav.EntryPoint, nav.BrowsePoint("tank-b")), "Both display browsing points have complete entry paths");
            Check(nav.CanReach(nav.BrowsePoint("tank-a"),nav.QueuePoint) && nav.CanReach(nav.QueuePoint,nav.ExitPoint), "Display-to-counter and counter-to-exit paths are complete");
            Check(!nav.HasNavigationAt(new Vector3(0,0,1),0.1f) && !nav.HasNavigationAt(new Vector3(0,0,3),0.1f), "Actual baked NavMesh excludes doorway lab side and back lab");
            Check(!nav.CanReach(nav.EntryPoint,new Vector3(0,0,3)), "A lab destination is rejected");

            // Use real recipe commands for fixtures; the preceding baseline tested timed production.
            SeedStock("pondlet","tank-a"); SeedStock("bubblebelly","tank-b");
            Check(shop.Model.Currency == 0 && shop.SaveNow(), "Customer slice begins with two seeded creatures and no money");
            shop.Customers.ArrivalsEnabled = true;
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open the shop for real customer service");
            Check(shop.Reports.Current.dayNumber==1 && shop.Reports.Current.stockedAtOpen==2 && shop.Reports.Last==null, "First day report starts with prepared display stock and no fabricated history");
            yield return WaitForCustomer(CustomerPhase.Arriving,8);
            string firstVisitor = shop.Customers.Visit.Id;
            Vector3 arrivalPosition = shop.Customers.Actor.transform.position;
            shop.SetPanel(ShopPanel.Pause);
            float dayTime = shop.Cycle.SecondsRemaining;
            yield return new WaitForSeconds(0.5f);
            Check(Vector3.Distance(arrivalPosition,shop.Customers.Actor.transform.position)<0.03f && Mathf.Abs(dayTime-shop.Cycle.SecondsRemaining)<0.001f, "Pause stops the physical visitor and trading clock");
            shop.SetPanel(ShopPanel.None);
            yield return WaitForCustomer(CustomerPhase.Browsing,15);
            Check(Vector3.Distance(arrivalPosition,shop.Customers.Actor.transform.position)>1, "Customer actually walks from the entrance to a display");
            AimAtCustomer(new Vector3(1,0.2f,-2.5f));
            yield return Capture("20-customer-browsing");
            yield return WaitForCustomer(CustomerPhase.ToCounter,6);
            var reservation = shop.Model.GetReservation("tank-a");
            Check(reservation.HasValue && reservation.Value.CustomerId == firstVisitor && shop.Model.Currency == 0, "Browsing reserves the exact Pondlet without paying");
            string firstSpecimen = reservation.Value.SpecimenId;
            Check(!shop.Model.SellFromTank("tank-a").Success, "Legacy domain shortcut cannot steal a reserved creature");
            Check(shop.Model.ReserveTank("tank-a","another-visitor").Failure == FailureCode.TankReserved, "A second buyer cannot claim the reserved specimen");
            yield return WaitForCustomer(CustomerPhase.Queued,15);
            Check(shop.Customers.ActiveCount == 1 && shop.Customers.Visit.Id == firstVisitor, "Concurrency remains one through browsing and queuing");
            shop.Player.Teleport(new Vector3(0,0.2f,3),Quaternion.identity);
            shop.SetPanel(ShopPanel.Counter);
            Check(!shop.ServeCustomer() && shop.Model.Currency==0 && shop.Model.GetTank("tank-a").Value.Id==firstSpecimen, "Remote checkout from the lab cannot pay or consume stock");
            shop.SetPanel(ShopPanel.Pause);
            float patience = shop.Customers.Visit.QueueSecondsRemaining;
            yield return new WaitForSeconds(0.5f);
            Check(Mathf.Abs(patience-shop.Customers.Visit.QueueSecondsRemaining)<0.001f, "Pause freezes queued customer patience");
            yield return EnterCounter();
            yield return Capture("21-customer-checkout");
            Check(shop.ServeCustomer() && shop.Model.Currency==6 && shop.Model.GetTank("tank-a")==null && !shop.Model.GetReservation("tank-a").HasValue, "Physical checkout pays six once and removes only the reserved Pondlet");
            Check(!shop.ServeCustomer() && shop.Model.Currency==6 && shop.Model.GetTank("tank-b")!=null, "Repeated checkout cannot pay again or consume another tank");
            Check(shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==6 && !shop.Reports.HasPending, "Checkout records exactly one sale and six income before saving");
            shop.SetPanel(ShopPanel.None);
            AimAtCustomer(new Vector3(-1.7f,0.2f,-3.9f));
            yield return Capture("22-customer-paid");
            yield return WaitForNoCustomer(12);
            Check(shop.Customers.ActiveCount==0 && shop.Model.Currency==6, "Paid visitor physically leaves and frees the visit slot");

            yield return WaitForCustomer(CustomerPhase.Queued,26);
            Check(shop.Customers.Visit.TargetTankId=="tank-b", "Next visitor selects the remaining stocked display");
            string carried = shop.Customers.Visit.SpecimenId;
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved(), "Save and reload a real queued purchase");
            Check(shop.Customers.Visit==null && !shop.Model.GetReservation("tank-b").HasValue && shop.Model.GetTank("tank-b").Value.Id==carried && shop.Model.Currency==6, "Reload cancels the unpaid visitor but preserves exact stock and completed income");
            Check(shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==6 && shop.Reports.Current.interrupted==1, "Reload retains earned day income and counts the unpaid visit as interrupted");
            yield return EnterCounter();
            yield return WaitForCustomer(CustomerPhase.Queued,26);
            Check(shop.CloseForNight(), "Close with a valid queued order");
            Check(shop.ServeCustomer() && shop.Model.Currency==16 && shop.Model.GetTank("tank-b")==null, "Accepted queued checkout may finish during closing without a new reservation");
            Check(!shop.ServeCustomer() && shop.Model.Currency==16, "Closing checkout still pays exactly once");
            yield return WaitForNight(6);
            var firstReport=shop.Reports.Last;
            Check(shop.Panel==ShopPanel.DayReport && firstReport.dayNumber==1 && firstReport.visitors==3 && firstReport.sales==2 && firstReport.revenue==16 && firstReport.interrupted==1 && firstReport.stockedAtClose==0, "Day one report opens automatically with both sales and the interrupted visit");
            yield return Capture("30-first-day-report");
            Check(shop.Customers.ActiveCount==0 && shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==16, "Night cleanup and reload preserve the completed closing sale");
            Check(shop.ShowDayReport() && shop.Reports.Last.revenue==16 && shop.Model.Currency==16 && shop.ShowDayReport() && shop.Model.Currency==16, "Saved report can reopen repeatedly without paying income again");

            SeedStock("ribbonfin","tank-a");
            string cancelled = shop.Model.GetTank("tank-a").Value.Id;
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open a second service day");
            yield return WaitForCustomer(CustomerPhase.Queued,26);
            Check(shop.CloseForNight(), "Close with an unpaid queued creature");
            yield return WaitForNight(6);
            Check(shop.Model.Currency==16 && shop.Model.GetTank("tank-a").Value.Id==cancelled && !shop.Model.GetReservation("tank-a").HasValue, "Expired closing releases the queued creature without payment or stock loss");
            Check(shop.Customers.ActiveCount==0 && shop.SaveNow() && shop.ReloadSaved(), "Closed visit cleanup survives a real reload");
            Check(shop.Reports.Last.dayNumber==2 && shop.Reports.Last.sales==0 && shop.Reports.Last.closed==1 && shop.Reports.Last.stockedAtClose==1, "Second report resets sales and records the safely returned closing order");
            yield return Capture("23-unpaid-order-returned");

            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open again after cancelled checkout");
            yield return WaitForCustomer(CustomerPhase.Queued,26);
            // Accelerate patience only; travel in every visit above uses real navigation frames.
            shop.Advance(46);
            Check(shop.Customers.Visit!=null && shop.Customers.Visit.Outcome==CustomerOutcome.Impatient && !shop.Model.GetReservation("tank-a").HasValue && shop.Model.GetTank("tank-a").Value.Id==cancelled && shop.Model.Currency==16, "Accelerated patience expiry releases the exact unpaid stock");
            yield return WaitForNoCustomer(12);
            yield return WaitForCustomer(CustomerPhase.ToCounter,26);
            shop.Customers.Visit.Advance(19,false,shop.Cycle.Phase);
            Check(shop.Customers.Visit.Outcome==CustomerOutcome.PathBlocked && !shop.Model.GetReservation("tank-a").HasValue && shop.Model.GetTank("tank-a").Value.Id==cancelled, "Injected travel timeout cancels the reservation without moving stock");
            yield return null; // Let the runtime director record the injected outcome before save/reload.
            // Simulate lost navigation after the domain timeout and prove the director recovers on reload.
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Customers.Visit==null && shop.Model.Currency==16, "Blocked visit reload resets transient navigation and retains progression");
            yield return EnterCounter();
            yield return WaitForCustomer(CustomerPhase.Queued,26);
            Check(shop.ServeCustomer() && shop.Model.Currency==26, "Another real customer buys the released Ribbonfin for ten");
            Check(shop.Perform(shop.Model.BuyUpgrade()).Success && shop.Model.Currency==2 && shop.Model.UpgradeOwned, "Three actual customer purchases fund the existing siphon upgrade");
            yield return WaitForNoCustomer(12);
            yield return WaitForCustomer(CustomerPhase.Browsing,26);
            yield return WaitForCustomer(CustomerPhase.Leaving,6);
            Check(shop.Customers.Visit.Outcome==CustomerOutcome.NoStock && shop.Model.Currency==2, "Empty displays cause a no-stock departure without money changes");
            yield return WaitForNoCustomer(12);
            Check(customerNavigationSamples>100, "Observed " + customerNavigationSamples + " navigation frames with at most one visitor, always inside the front store");
            Check(ReferenceEquals(nav, shop.Customers.Navigation) && nav.Ready && nav.BuildCount==initialBuildCount, "Customer activity and unchanged-layout reloads reuse the same ready navigation without rebuilding");
            Check(shop.SaveNow(), "Save the customer-funded upgrade to the isolated evidence slot");
            File.Copy(shop.SavePath,Path.Combine(evidence,"customer-progress.json"),true);
            SeedStock("pondlet","tank-a");
            Check(shop.Reports.Current.restocked==1 && shop.Model.GetTank("tank-a").HasValue, "Restocking during the day records one successful display transfer");
            Check(!shop.TransferToDisplay("tank-a").Success && shop.Reports.Current.restocked==1, "Failed restocking does not inflate report counts");
            Check(shop.CloseForNight(), "Close the report test day after stock recovery");
            yield return WaitForNight(6);
            var recoveryReport=shop.Reports.Last;
            Check(recoveryReport.dayNumber==3 && recoveryReport.visitors==4 && recoveryReport.sales==1 && recoveryReport.revenue==10 && recoveryReport.impatient==1 && recoveryReport.pathProblems==1 && recoveryReport.noStock==1 && recoveryReport.restocked==1 && recoveryReport.stockedAtClose==1, "Recovery report reconciles sale, impatience, blocked travel, no stock and restocking");
            Check(shop.Model.Currency==2 && recoveryReport.revenue==10 && !shop.Paused, "Report income is separate from spent cash and night production remains active");
            yield return Capture("31-recovery-day-report");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.ShowDayReport() && shop.Reports.Last.revenue==10 && shop.Reports.Last.noStock==1 && shop.Model.Currency==2, "Full recovery report survives file reload without replaying payments");
            File.Copy(shop.SavePath,Path.Combine(evidence,"report-progress.json"),true);
            yield return MoveFor(Key.Escape,0.05f);
            Check(shop.Panel==ShopPanel.None, "Real Escape input dismisses the day report");
            yield return EnterCounter();
            Check(shop.ShowDayReport() && shop.Reports.Last.dayNumber==3, "The physical counter can reopen the last completed report");
            shop.SetPanel(ShopPanel.Counter);
            Check(shop.OpenForDay() && shop.Reports.Current.sales==0 && shop.Reports.Current.stockedAtOpen==1 && shop.Reports.Last.dayNumber==3, "Next day starts clean counters and retains the previous report");
            Check(shop.CloseForNight(), "An empty trading day may still close and report");
            yield return WaitForNight(6);
            Check(shop.Reports.Last.dayNumber==4 && shop.Reports.Last.visitors==0 && shop.Reports.Last.sales==0 && shop.Reports.Last.stockedAtClose==1 && shop.Model.Currency==2, "Zero-visitor day reports honestly and preserves the free recovery route");
            shop.Model.Tick(100);
            Ok(shop.Model.Collect(CultureKind.Bloom), "Collect free Bloom with report open");
            Ok(shop.Model.Collect(CultureKind.Flow), "Collect free Flow with report open");
            shop.Model.Dose(CultureKind.Bloom); shop.Model.Dose(CultureKind.Flow);
            Ok(shop.Model.BeginIncubation(), "Prepare next stock while the night report is open");
            float reportProduction=shop.Model.Incubator.SecondsRemaining;
            yield return new WaitForSeconds(0.4f);
            Check(shop.Model.Incubator.SecondsRemaining<reportProduction && shop.Cycle.Phase==ShopDayPhase.Night && shop.Reports.Last.sales==0, "Night incubation advances during report reading without changing completed results");
            yield return Capture("32-empty-day-report");
            var legacyCycle=new ShopDayCycle(); legacyCycle.OpenShop(); legacyCycle.Tick(11);
            var legacyGame=shop.Model.CaptureSave(); legacyGame.version=1; legacyGame.prices=null; // Format 2 carried game version 1, without price tags.
            string legacyJson=JsonUtility.ToJson(new SlotData {formatVersion=2,game=legacyGame,cycle=legacyCycle.CaptureSave(),trades=null,sensitivity=shop.Sensitivity,volume=shop.Volume},true);
            File.WriteAllText(shop.SavePath,legacyJson); // Isolated smoke slot only; simulate the delivered v2 player format.
            Check(shop.ReloadSaved() && shop.Reports.Current.partial && shop.Reports.Current.sales==0 && shop.Model.Currency==2 && shop.Model.UpgradeOwned && File.ReadAllText(shop.SavePath)==legacyJson, "Runtime v2 reload preserves progress and starts explicitly partial day statistics without writing");
            Check(shop.CloseForNight(), "Migrated partial day can close normally");
            yield return WaitForNight(6);
            Check(shop.Reports.Last.partial && shop.Reports.Last.revenue==0 && shop.Reports.Last.stockedAtClose==1 && File.ReadAllText(shop.SavePath+".v2.bak")==legacyJson, "Migrated report does not invent earlier income and archives the exact v2 save");
            yield return Capture("33-migrated-partial-report");
            shop.Customers.ArrivalsEnabled=false;
            shop.ConfirmNewGame();
            Check(shop.Reports.Current==null && shop.Reports.Last==null, "Confirmed new game clears report history with progression");
        }
        void SeedStock(string speciesId,string tankId)
        {
            var model=shop.Model;
            model.Tick(100);
            var recipe=model.GetSpecies(speciesId);
            for(int i=0;i<recipe.Bloom;i++) if(!model.Collect(CultureKind.Bloom).Success) throw new InvalidOperationException("Seed Bloom");
            for(int i=0;i<recipe.Flow;i++) if(!model.Collect(CultureKind.Flow).Success) throw new InvalidOperationException("Seed Flow");
            for(int i=0;i<recipe.Bloom;i++) model.Dose(CultureKind.Bloom);
            for(int i=0;i<recipe.Flow;i++) model.Dose(CultureKind.Flow);
            if(!model.BeginIncubation().Success) throw new InvalidOperationException("Seed incubation");
            model.Tick(shop.Config.IncubationSeconds);
            if(!shop.TransferToDisplay(tankId).Success) throw new InvalidOperationException("Seed transfer");
        }
        IEnumerator EnterCounter()
        {
            shop.SetPanel(ShopPanel.None);
            var counter=StationOf(StationKind.Counter).transform;
            shop.Player.Teleport(counter.TransformPoint(new Vector3(0,0.2f,-2.5f)),counter.rotation);
            shop.Player.View.transform.LookAt(counter.TransformPoint(new Vector3(0,1.1f,0)));
            yield return null; yield return null;
            yield return MoveFor(Key.E,0.05f);
            Check(shop.Panel==ShopPanel.Counter && shop.IsAtCounter,"Real E input opens the physical service counter");
        }
        void AimAtCustomer(Vector3 playerPosition)
        {
            shop.Player.Teleport(playerPosition,Quaternion.identity);
            if(shop.Customers.Actor) shop.Player.View.transform.LookAt(shop.Customers.Actor.transform.position+Vector3.up*1.25f);
        }
        void ObserveCustomerNavigation()
        {
            if(shop.Customers.ActiveCount>1) throw new InvalidOperationException("Customer concurrency exceeded");
            if(!shop.Customers.Actor) return;
            customerNavigationSamples++;
            if(!shop.Customers.Navigation.IsInStore(shop.Customers.Actor.transform.position)) throw new InvalidOperationException("Visitor entered the lab or left front-store bounds");
        }
        IEnumerator WaitForCustomer(CustomerPhase phase,float timeout)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(shop.Customers.Visit==null || shop.Customers.Visit.Phase!=phase)
            {
                ObserveCustomerNavigation();
                if(Time.realtimeSinceStartup>deadline) throw new TimeoutException("Customer never reached "+phase+"; current="+shop.Customers.Status+"; nav="+shop.Customers.Navigation.LastError);
                yield return null;
            }
            ObserveCustomerNavigation();
        }
        IEnumerator WaitForNoCustomer(float timeout)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(shop.Customers.ActiveCount!=0)
            {
                ObserveCustomerNavigation();
                if(Time.realtimeSinceStartup>deadline) throw new TimeoutException("Customer did not leave");
                yield return null;
            }
        }
        IEnumerator WaitForNight(float timeout)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(shop.Cycle.Phase!=ShopDayPhase.Night)
            {
                ObserveCustomerNavigation();
                if(Time.realtimeSinceStartup>deadline) throw new TimeoutException("Closing did not reach night");
                yield return null;
            }
        }
    }
}
#endif
