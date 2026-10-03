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
            yield return EnterCounter();
            yield return WaitForCustomer(CustomerPhase.Queued,26);
            Check(shop.CloseForNight(), "Close with a valid queued order");
            Check(shop.ServeCustomer() && shop.Model.Currency==16 && shop.Model.GetTank("tank-b")==null, "Accepted queued checkout may finish during closing without a new reservation");
            Check(!shop.ServeCustomer() && shop.Model.Currency==16, "Closing checkout still pays exactly once");
            yield return WaitForNight(6);
            Check(shop.Customers.ActiveCount==0 && shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==16, "Night cleanup and reload preserve the completed closing sale");

            SeedStock("ribbonfin","tank-a");
            string cancelled = shop.Model.GetTank("tank-a").Value.Id;
            yield return EnterCounter();
            Check(shop.OpenForDay(), "Open a second service day");
            yield return WaitForCustomer(CustomerPhase.Queued,26);
            Check(shop.CloseForNight(), "Close with an unpaid queued creature");
            yield return WaitForNight(6);
            Check(shop.Model.Currency==16 && shop.Model.GetTank("tank-a").Value.Id==cancelled && !shop.Model.GetReservation("tank-a").HasValue, "Expired closing releases the queued creature without payment or stock loss");
            Check(shop.Customers.ActiveCount==0 && shop.SaveNow() && shop.ReloadSaved(), "Closed visit cleanup survives a real reload");
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
            Check(nav.BuildCount==1, "Customer changes and reloads reuse one navigation build");
            Check(shop.SaveNow(), "Save the customer-funded upgrade to the isolated evidence slot");
            File.Copy(shop.SavePath,Path.Combine(evidence,"customer-progress.json"),true);
            shop.Customers.ArrivalsEnabled=false;
            shop.ConfirmNewGame();
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
            if(!shop.Perform(model.TransferToTank(tankId)).Success) throw new InvalidOperationException("Seed transfer");
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
