#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        bool stockerDoorOpenedAlone;

        IEnumerator StockerScenario()
        {
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionStockerSmoke")!=null &&
                Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),
                "Stocker route uses only its explicitly isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors==2 && !shop.Customers.UseBaseReputationEffectsForValidation &&
                shop.Customers.ForcedArchetypeId==null,"Stocker route retains modern customer concurrency and reputation rules");
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionStockerKeyboard");
            cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionStockerMouse");
            syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;
            if(ShopSession.Arg("-evolutionStockerReload")!=null)
            { yield return StockerRestartScenario();yield break; }
            limitations.Add("R07.3 isolated stocker route uses a labelled 300-credit construction/wage fixture, accelerated recipe commands, selected customer arrivals and fixture camera/player positions. Rack/counter interactions and placement use synthetic input; staff menu actions call the same guarded session commands as the HUD. Worker movement, door collision, carrying, checkout, pauses and next-night wages run on ordinary frames. A temporary world-child doorway barrier and isolated .tmp-directory save obstruction are deliberate fault injections. This does not establish native mouse-menu usability, ordinary economy balance, every layout, audio acceptance or performance.");
            Check(shop.Stocking!=null && shop.Stocker && !shop.Stocking.Employee.Hired && !shop.Staff.Hired &&
                shop.Model.Currency==0 && shop.Cycle.DayNumber==1,"Fresh shop starts with neither worker nor wage receipts");
            yield return StockerEnter();
            Check(shop.HireStocker() && !shop.HireStocker() && shop.Stocking.Employee.Hired && !shop.Stocking.Employee.Scheduled &&
                shop.Model.Currency==0,"Nori is hired once for free and starts off schedule");
            StockerParkPlayer();
            yield return StockerWait(()=>shop.World.LabDoor.OpenFraction<.01f,5,"idle off-schedule worker leaves the room door closed");
            Check(shop.Stocker.Actor && shop.Stocker.Actor.isActiveAndEnabled && shop.Stocker.CurrentJobId==null &&
                !shop.World.LabDoor.WantsOpen,"Visible off-schedule employee stands clear of the door sensor");
            yield return StockerEnter();
            Check(shop.SetStockerScheduled(true) && !shop.PayStockerDuty() && shop.Stocking.Employee.Scheduled &&
                !shop.Stocking.Employee.IsOnDuty(1) && shop.Stocking.Employee.PaidDay==0 && shop.Model.Currency==0,
                "No-funds schedule persists without debt, work or a paid receipt");
            shop.Player.Teleport(new Vector3(0,.2f,3),Quaternion.identity);
            Check(!shop.OpenStockerMenu() && !shop.PayStockerDuty() && !shop.DismissStocker(),
                "Lab-side staff commands cannot bypass the physical front counter");
            Check(shop.SaveNow(),"Save isolated unpaid roster before labelled credit fixture");
            var fixture=JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));fixture.game.currency=300;
            Check(SaveStore.Write(shop.SavePath,fixture,out var fixtureError) && shop.ReloadSaved() && shop.Model.Currency==300 &&
                shop.Stocking.Employee.PaidDay==0,"Load labelled 300-credit fixture without charging scheduled unpaid Nori / "+fixtureError);
            File.Copy(shop.SavePath,Path.Combine(evidence,"stocker-starting-credit-fixture.json"),true);
            yield return StockerEnter();
            string unpaidRoster=JsonUtility.ToJson(shop.Stocking.CaptureSave(shop.Model));
            string unpaidFile=File.ReadAllText(shop.SavePath);var unpaidModel=shop.Model;
            string blockedTemp=shop.SavePath+".tmp";
            Check(!File.Exists(blockedTemp) && !Directory.Exists(blockedTemp),"Wage fault-injection path is unused and belongs to this isolated save");
            Directory.CreateDirectory(blockedTemp);
            try
            {
                Check(!shop.PayStockerDuty() && ReferenceEquals(unpaidModel,shop.Model) && shop.Model.Currency==300 &&
                    JsonUtility.ToJson(shop.Stocking.CaptureSave(shop.Model))==unpaidRoster && File.ReadAllText(shop.SavePath)==unpaidFile,
                    "Failed wage write preserves exact live model, money, roster, receipts and previous file");
            }
            finally { Directory.Delete(blockedTemp); }
            Check(shop.PayStockerDuty() && shop.Model.Currency==296 && shop.Stocking.Employee.IsOnDuty(1),
                "Recovered payment spends four credits once for preparation night and following day");
            Check(shop.PayStockerDuty() && shop.SetStockerScheduled(false) && shop.SetStockerScheduled(true) &&
                shop.Model.Currency==296,"Repeated paid retry and schedule toggles cannot charge again");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Stocking.Employee.IsOnDuty(1) && shop.Model.Currency==296,
                "Reload retains stocker duty without duplicating its wage");
            yield return StaffEnter();
            Check(shop.HireCashier() && shop.SetCashierScheduled(true) && shop.Staff.IsOnDuty(1) && shop.Model.Currency==292,
                "Pip has an independent four-credit receipt alongside Nori");
            yield return StockerEnter();Check(shop.SetStockerPaused(true),"Pause Nori before constructing and preparing the explicit route");
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));
            string rack=StockerRack();string[] slots=shop.Layout.GetStorageSlotIds(rack);
            Check(slots.Length==2 && shop.Model.Currency==260,"A normal paid rack adds two sources without changing wage receipts");
            SpecimenProduce("pondlet");string pond=shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]),"Physically store exact Pondlet source in the rack");
            SpecimenProduce("bubblebelly");string bubble=shop.Model.Incubator.SpecimenId;
            Check(shop.StoreReadyInRack(slots[1]) && shop.StoredSpecimenCount==2,"Physically store distinct Bubblebelly source");
            yield return StockerEnter();
            Check(shop.SetStockingTarget("tank-a","pondlet") && shop.SetStockingTarget("tank-b","bubblebelly"),
                "Explicit discovered-species targets configure one Pondlet in A and one Bubblebelly in B");
            yield return GateCapture("300-stocker-controls");
            shop.SetPanel(ShopPanel.StockerTargets);yield return GateCapture("301-stocker-targets");
            Check(shop.SetStockerPaused(false),"Resume Nori for the first reserved source/destination job");
            StockerParkPlayer();
            yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"first stocker claim");
            string job=shop.Stocker.CurrentJobId;
            var claim=shop.Model.GetStockTransfer(job);
            Check(claim.HasValue && claim.Value.OwnerId==ShopStaff.StockerId && claim.Value.StorageId==slots[0] &&
                claim.Value.TankId=="tank-a" && claim.Value.SpecimenId==pond && shop.Model.GetStorage(slots[0]).Value.Id==pond,
                "Stable first job owns exact rack source and target while the only specimen remains in its rack");
            string ownership=StockerInventory();int money=shop.Model.Currency;
            Check(!shop.CompleteStockerJob(job) && StockerInventory()==ownership && shop.Model.Currency==money,
                "Early completion cannot bypass real travel and service time");
            Check(!shop.Model.TransferStorageToTank(slots[0],"tank-b",pond).Success &&
                !shop.Model.TransferStorageToTank(slots[1],"tank-a",bubble).Success &&
                !shop.Model.ReserveStockTransfer("competing-job","other-worker",slots[0],"tank-b",pond).Success &&
                StockerInventory()==ownership,"Manual transfers and a competing worker cannot take the claimed source or destination");
            shop.SetPanel(ShopPanel.Pause);Vector3 pausedPosition=shop.Stocker.Actor.transform.position;
            float pausedTravel=shop.Stocker.TravelDistance;
            yield return new WaitForSeconds(.6f);
            Check(shop.Stocker.CurrentJobId==job && shop.Stocker.TravelDistance==pausedTravel &&
                Vector3.Distance(pausedPosition,shop.Stocker.Actor.transform.position)<.0001f && StockerInventory()==ownership,
                "Global pause freezes job, physical position, travel and specimen ownership");
            yield return StockerEnter();
            Check(shop.SetStockerPaused(true) && shop.Stocker.CurrentJobId==null && !shop.Model.GetStockTransfer(job).HasValue &&
                !shop.Model.IsStorageReservedForStocking(slots[0]) && !shop.Model.IsTankReservedForStocking("tank-a") &&
                StockerInventory()==ownership,"Pausing Nori immediately releases both claims without moving the creature");
            Check(shop.Model.TransferStorageToTank(slots[0],"tank-a",pond).Success &&
                shop.Model.ReturnTankToStorage("tank-a",slots[0],pond).Success && StockerInventory()==ownership,
                "Released source and display support reversible manual work with the same exact specimen");
            Check(shop.SetStockerPaused(false),"Resume for mid-job save recovery");StockerParkPlayer();
            yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"claim before reload");job=shop.Stocker.CurrentJobId;
            var claimedModel=shop.Model;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Stocker.CurrentJobId==null &&
                !claimedModel.GetStockTransfer(job).HasValue && !shop.Model.IsStorageReservedForStocking(slots[0]) &&
                !shop.Model.IsTankReservedForStocking("tank-a") && StockerInventory()==ownership && shop.Model.Currency==money,
                "Mid-job reload releases old authority and restores the exact source with no timer, claim, charge or delivery replay");
            StockerParkPlayer();
            yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"source claim before construction pause");
            job=shop.Stocker.CurrentJobId;shop.SetPanel(ShopPanel.Arrange);
            Check(shop.Stocker.CurrentJobId==null && !shop.Model.GetStockTransfer(job).HasValue && StockerInventory()==ownership,
                "Opening room editing immediately releases both claims and preserves the source");
            yield return new WaitForSeconds(.2f);
            Check(shop.Stocker.CurrentJobId==null && !shop.Stocker.Blocked,"Room editing prevents new worker jobs without requiring blocked-route recovery");
            StockerParkPlayer();
            yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"recovered source claim");
            // Geometry outside the authoritative floorplan emulates an unexpected solid route obstruction.
            var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.name="Validation-only stocker doorway obstruction";
            barrier.transform.SetParent(shop.World.transform,false);
            barrier.transform.localPosition=new Vector3(0,1.2f,.8f);barrier.transform.localScale=new Vector3(3.3f,2.4f,.45f);
            Physics.SyncTransforms();
            try
            {
                yield return StockerWait(()=>shop.Stocker.Blocked,35,"real capsule sweep meets temporary door barrier",false);
                Check(shop.Stocker.CurrentJobId==null && !shop.Model.IsStorageReservedForStocking(slots[0]) &&
                    !shop.Model.IsTankReservedForStocking("tank-a") && StockerInventory()==ownership,
                    "Physical path failure visibly blocks and releases both claims with the source still safe");
                yield return new WaitForSeconds(ShopStocker.RetrySeconds+.2f);
                Check(shop.Stocker.Blocked && shop.Stocker.CurrentJobId==null && StockerInventory()==ownership,
                    "Blocked physical work does not silently retry forever or consume inventory");
            }
            finally { Destroy(barrier); }
            yield return null;Physics.SyncTransforms();
            yield return StockerEnter();Check(shop.RetryStocker(),"Player explicitly retries after clearing physical obstruction");StockerParkPlayer();
            yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"job before component disable");job=shop.Stocker.CurrentJobId;
            shop.Stocker.enabled=false;yield return new WaitForSeconds(.25f);
            Check(shop.Stocker.CurrentJobId==null && !shop.Model.GetStockTransfer(job).HasValue &&
                !shop.Stocker.Actor.gameObject.activeSelf && shop.Stocker.Status.ToLowerInvariant().Contains("disabled") && StockerInventory()==ownership,
                "Disabled worker cancels claims, hides presentation and cannot silently transfer stock");
            shop.Stocker.enabled=true;
            yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"job before missing employee visual");job=shop.Stocker.CurrentJobId;
            Destroy(shop.Stocker.Actor.gameObject);yield return null;yield return null;
            Check(!shop.Stocker.Actor && shop.Stocker.Blocked && shop.Stocker.CurrentJobId==null &&
                !shop.Model.GetStockTransfer(job).HasValue && StockerInventory()==ownership,
                "Destroyed employee visual leaves a blocked worker and the exact source safe without automatic respawn");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Stocker.Actor && shop.Stocking.Employee.IsOnDuty(1) &&
                shop.Model.Currency==money,"Explicit reload restores missing employee without another wage");
            StockerParkPlayer();
            yield return StockerWait(()=>shop.Stocker.Carrying,30,"real rack arrival and carrying pose");
            Check(shop.Model.GetStorage(slots[0]).Value.Id==pond && !shop.Model.GetTank("tank-a").HasValue &&
                shop.Stocker.TravelDistance>1,"Carrying presentation follows real movement without a second inventory copy");
            GateAim(new Vector3(1f,.2f,2.2f),shop.Stocker.Actor.transform.position+Vector3.up*1.1f);
            yield return GateCapture("302-stocker-carrying");StockerParkPlayer();
            string beforeDelivery=StockerInventory(), beforeReport=JsonUtility.ToJson(shop.Reports.CaptureSave());
            int beforeJobs=shop.Stocker.CompletedJobs;
            Check(!File.Exists(blockedTemp) && !Directory.Exists(blockedTemp),"Delivery fault-injection path remains isolated and unused");
            Directory.CreateDirectory(blockedTemp);
            try
            {
                yield return StockerWait(()=>shop.Stocker.Blocked,35,"delivery persistence failure",false);
                Check(shop.SaveError!=null && StockerInventory()==beforeDelivery && shop.Model.Currency==money &&
                    JsonUtility.ToJson(shop.Reports.CaptureSave())==beforeReport && shop.Stocker.CompletedJobs==beforeJobs &&
                    !shop.Model.IsStorageReservedForStocking(slots[0]) && !shop.Model.IsTankReservedForStocking("tank-a"),
                    "Failed delivery save changes no source, destination, money, report or completed-job count and releases claims");
            }
            finally { Directory.Delete(blockedTemp); }
            yield return StockerEnter();Check(shop.RetryStocker(),"Retry the safe rack source after storage is writable again");StockerParkPlayer();
            stockerDoorOpenedAlone=false;
            yield return StockerWait(()=>shop.Model.GetTank("tank-a").HasValue && shop.Model.GetTank("tank-b").HasValue,55,
                "two actual rack-to-display deliveries");
            Check(shop.Model.GetTank("tank-a").Value.Id==pond && shop.Model.GetTank("tank-b").Value.Id==bubble &&
                !shop.Model.GetStorage(slots[0]).HasValue && !shop.Model.GetStorage(slots[1]).HasValue &&
                shop.Stocker.CompletedJobs==beforeJobs+2 && shop.Stocker.TravelDistance>8 && shop.Model.Currency==money &&
                shop.Reports.Current==null && shop.SaveError==null,
                "Exactly two physical deliveries preserve species targets, unique identities, money and night report semantics");
            Check(stockerDoorOpenedAlone,"Nori alone opens the solid room door while the player remains outside its sensor");
            yield return StockerWait(()=>shop.World.LabDoor.OpenFraction<.01f,5,"door closing after employee clears threshold");
            int navigationBuilds=shop.Customers.Navigation.BuildCount;
            var nav=shop.Customers.Navigation;
            Check(!nav.HasNavigationAt(new Vector3(0,0,3),.1f) && nav.CanReach(nav.EntryPoint,nav.BrowseSpot("tank-a",0)) &&
                nav.CanReach(nav.BrowseSpot("tank-b",0),nav.QueueSpot(0)),"Employee route keeps customers in the store and preserves entry/display/queue access");
            yield return StaffEnter();Check(shop.SetCashierPaused(true),"Pause Pip to inspect the actual reserved order");
            yield return EnterCounter();Check(shop.OpenForDay(),"Open day one with Nori's exact two delivered creatures");
            var visitor=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>visitor.Phase==CustomerPhase.Queued,30,"customer queues for Nori-delivered specimen");
            string soldTank=visitor.TargetTankId,soldId=visitor.SpecimenId,soldSpecies=shop.Model.GetTank(soldTank).Value.SpeciesId;
            Check(soldId==pond || soldId==bubble,"A real walking customer reserves one exact worker-delivered specimen");
            int beforeSale=shop.Model.Currency;
            yield return StaffEnter();Check(shop.SetCashierPaused(false),"Resume Pip to finish this real checkout");StockerParkPlayer();
            yield return WaitFor(()=>visitor.Outcome==CustomerOutcome.Sold,ShopCashier.ServiceSeconds+6,"Pip sells Nori delivery");
            Check(shop.Model.Currency==beforeSale+visitor.Price && !shop.Model.GetTank(soldTank).HasValue &&
                shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==visitor.Price && !shop.ServeCashier(visitor),
                "Cashier and stocker share one ownership chain and one exact agreed payment");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"first staffed customer leaving");
            yield return StaffEnter();Check(shop.SetCashierPaused(true),"Pause checkout for a concurrent reservation/restock proof");
            var waiting=Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(()=>waiting.Phase==CustomerPhase.Queued,30,"other display remains customer-owned");
            string reservedId=waiting.SpecimenId,reservedTank=waiting.TargetTankId;
            var liveModel=shop.Model;int restocks=shop.Reports.Current.restocked;
            SpecimenProduce(soldSpecies);string replacement=shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(rack);Check(shop.StoreReadyInRack(slots[0]),"Put newly grown replacement into real rack during trading");
            StockerParkPlayer();
            yield return StockerWait(()=>shop.Model.GetTank(soldTank).HasValue,35,"daytime replenishment alongside another accepted order");
            Check(ReferenceEquals(liveModel,shop.Model) && shop.Model.GetTank(soldTank).Value.Id==replacement &&
                !shop.Model.GetStorage(slots[0]).HasValue && shop.Reports.Current.restocked==restocks+1 &&
                waiting.Phase==CustomerPhase.Queued && shop.Model.GetReservation(reservedTank).HasValue &&
                shop.Model.GetReservation(reservedTank).Value.CustomerId==waiting.Id && shop.Model.GetTank(reservedTank).Value.Id==reservedId,
                "Daytime delivery retains the same live model, increments one restock and preserves another customer's exact accepted order");
            yield return StockerEnter();int dayMoney=shop.Model.Currency;
            Check(!shop.DismissStocker() && !shop.SetStockerScheduled(false) && !shop.PayStockerDuty() && shop.Model.Currency==dayMoney,
                "Day management cannot dismiss, reschedule or recollect a duty wage");
            yield return StaffEnter();Check(shop.SetCashierPaused(false),"Resume checkout after concurrent stocking proof");StockerParkPlayer();
            yield return WaitFor(()=>waiting.Outcome==CustomerOutcome.Sold,ShopCashier.ServiceSeconds+6,"preserved second checkout");
            Check(shop.Model.Currency==dayMoney+waiting.Price && shop.Reports.Current.sales==2,
                "The preserved second reservation pays once after the stocker transaction");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"second staffed customer leaving");
            Check(shop.Customers.Navigation.BuildCount==navigationBuilds,"Worker travel and trading do not rebuild or replace customer navigation");
            yield return EnterCounter();int closingMoney=shop.Model.Currency;
            Check(shop.CloseForNight(),"Close the staffing integration day");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"night two paired wages");
            Check(shop.Cycle.DayNumber==2 && shop.Model.Currency==closingMoney-8 && shop.Staff.IsOnDuty(2) &&
                shop.Stocking.Employee.IsOnDuty(2) && shop.Staff.WagesForDay(1)==4 && shop.Stocking.Employee.WagesForDay(1)==4 &&
                shop.Staff.WagesForDay(2)==4 && shop.Stocking.Employee.WagesForDay(2)==4,
                "Night two charges both independent four-credit schedules once and retains the completed day's wages");
            int nightMoney=shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==nightMoney && shop.Staff.IsOnDuty(2) &&
                shop.Stocking.Employee.IsOnDuty(2),"Night-two reload does not duplicate either automatic wage");
            yield return StockerEnter();Check(shop.SetStockerPaused(true),"Pause Nori while preparing the explicit fresh-process continuation");
            Check(shop.SetStockingTarget("tank-b",null),"Disable B in the continuation so the next job has one explicit destination");
            foreach(string tank in shop.Model.TankIds)
                if(shop.Model.GetTank(tank).HasValue)
                {
                    string id=shop.Model.GetTank(tank).Value.Id;
                    string destination=tank=="tank-a" ? slots[0] : slots[1];
                    yield return SpecimenDisplayPanel(tank);
                    Check(shop.ReturnDisplayToStorage(destination,id),"Physically return remaining display creature for safe restart fixture / "+tank);
                }
            if(!shop.Model.GetStorage(slots[0]).HasValue)
            {
                SpecimenProduce("pondlet");yield return SpecimenRackPanel(rack);
                Check(shop.StoreReadyInRack(slots[0]),"Prepare one explicit Pondlet source for the next process");
            }
            Check(shop.Model.GetStorage(slots[0]).Value.SpeciesId=="pondlet" && !shop.Model.GetTank("tank-a").HasValue &&
                !shop.Model.GetTank("tank-b").HasValue && shop.Stocking.GetTarget("tank-a")=="pondlet",
                "Continuation contains a unique rack Pondlet and one empty configured display");
            yield return StockerEnter();yield return GateCapture("303-stocker-night-two-ready");
            Check(shop.SetStockerPaused(false) && shop.Staff.IsOnDuty(2) && shop.Stocking.Employee.IsOnDuty(2) &&
                shop.SaveNow(),"Save both paid unpaused workers immediately before any new job frame");
            File.Copy(shop.SavePath,Path.Combine(evidence,"stocker-progress.json"),true);
        }

        IEnumerator StockerRestartScenario()
        {
            limitations.Add("Fresh-process stocker continuation reads a copied isolated paid-night-two fixture from the main route. It explicitly invokes normal ReloadSaved before work, archives input bytes, runs real employee travel/door/checkout and the next duty period. A final accelerated recipe is used only to verify confirmed new-game cancellation. Menu commands and fixture camera positions do not establish human usability, balance, sound or performance.");
            Check(File.Exists(shop.SavePath),"Fresh-process route has an explicitly supplied isolated continuation save");
            byte[] original=File.ReadAllBytes(shop.SavePath);File.WriteAllBytes(Path.Combine(evidence,"stocker-restart-original.json"),original);
            var fixture=JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            Check(fixture!=null && fixture.formatVersion==SaveStore.CurrentFormatVersion && fixture.cycle.dayNumber==2 &&
                fixture.cycle.phase==ShopDayPhase.Night && fixture.staff.hired && fixture.staff.scheduled && !fixture.staff.paused &&
                fixture.staff.paidDay==2 && fixture.stocker.employee.hired && fixture.stocker.employee.scheduled &&
                !fixture.stocker.employee.paused && fixture.stocker.employee.paidDay==2,
                "Restart input is the exact paid and scheduled night-two two-worker continuation");
            string gameJson=JsonUtility.ToJson(fixture.game),staffJson=JsonUtility.ToJson(fixture.staff),stockerJson=JsonUtility.ToJson(fixture.stocker);
            int money=fixture.game.currency;
            Check(!shop.Staff.Hired && !shop.Stocking.Employee.Hired && shop.Model.Currency==0 && shop.Cycle.DayNumber==1,
                "New validation process begins with fresh in-memory state before explicit restore");
            Check(shop.ReloadSaved() && shop.Model.Currency==money && shop.Cycle.DayNumber==2 && shop.Staff.IsOnDuty(2) &&
                shop.Stocking.Employee.IsOnDuty(2) && JsonUtility.ToJson(shop.Model.CaptureSave())==gameJson &&
                JsonUtility.ToJson(shop.Staff.CaptureSave())==staffJson && JsonUtility.ToJson(shop.Stocking.CaptureSave(shop.Model))==stockerJson &&
                Convert.ToBase64String(File.ReadAllBytes(shop.SavePath))==Convert.ToBase64String(original),
                "Actual fresh-process restore keeps exact model, settings, wages and original file bytes without a charge");
            Check(shop.ReloadSaved() && shop.Model.Currency==money && shop.Stocker.CurrentJobId==null && shop.Cashier.Target==null &&
                shop.Stocker.Actor && shop.Stocker.Actor.isActiveAndEnabled && shop.Cashier.Actor && shop.Cashier.Actor.isActiveAndEnabled,
                "Repeated fresh-process restore retains both visible workers without transient claims or payment replay");
            string rack=StockerRack();string[] slots=shop.Layout.GetStorageSlotIds(rack);
            string pond=shop.Model.GetStorage(slots[0]).Value.Id;
            Check(shop.Stocking.GetTarget("tank-a")=="pondlet" && !shop.Model.GetTank("tank-a").HasValue &&
                !shop.Model.GetTank("tank-b").HasValue && !shop.Model.IsStorageReservedForStocking(slots[0]),
                "Fresh process starts with the original rack ownership and one empty configured target");
            StockerParkPlayer();stockerDoorOpenedAlone=false;
            yield return StockerWait(()=>shop.Model.GetTank("tank-a").HasValue,35,"fresh-process physical delivery");
            Check(shop.Model.GetTank("tank-a").Value.Id==pond && !shop.Model.GetStorage(slots[0]).HasValue &&
                shop.Model.Currency==money && shop.Stocker.CompletedJobs==1 && shop.Stocker.TravelDistance>3 && stockerDoorOpenedAlone,
                "Fresh process performs one actual paid-worker delivery through its own door opening without duplicating inventory or wages");
            yield return EnterCounter();Check(shop.OpenForDay(),"Open restored workers' paid day two");
            var visitor=Spawn(CustomerArchetypes.Casual);StockerParkPlayer();
            yield return WaitFor(()=>visitor.Outcome==CustomerOutcome.Sold,35,"fresh-process cashier selling newly delivered Pondlet");
            Check(visitor.SpecimenId==pond && shop.Model.Currency==money+visitor.Price && !shop.Model.GetTank("tank-a").HasValue &&
                shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==visitor.Price && !shop.ServeCashier(visitor),
                "Restarted stocker-to-cashier chain consumes one exact fish and pays its agreed price once");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"fresh-process customer leaving");
            yield return EnterCounter();Check(shop.CloseForNight(),"Close restored staff day two");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"fresh-process night-three duty");
            int expected=money+visitor.Price-8;
            Check(shop.Cycle.DayNumber==3 && shop.Model.Currency==expected && shop.Staff.IsOnDuty(3) && shop.Stocking.Employee.IsOnDuty(3) &&
                shop.Staff.WagesForDay(2)==4 && shop.Stocking.Employee.WagesForDay(2)==4 && shop.Staff.WagesForDay(3)==4 &&
                shop.Stocking.Employee.WagesForDay(3)==4,"Fresh process starts both next-night duty periods exactly once and retains both previous wages");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==expected && shop.Staff.IsOnDuty(3) &&
                shop.Stocking.Employee.IsOnDuty(3),"Another reload cannot duplicate either new duty payment");
            yield return StockerEnter();yield return GateCapture("304-stocker-fresh-process-paid");
            Check(shop.SaveNow(),"Save fresh-process progress before destructive isolated new-game validation");
            File.Copy(shop.SavePath,Path.Combine(evidence,"stocker-restart-progress.json"),true);
            SpecimenProduce("pondlet");yield return SpecimenRackPanel(rack);
            Check(shop.StoreReadyInRack(slots[0]),"Prepare labelled source solely for confirmed-reset ownership cleanup");
            StockerParkPlayer();yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"job before confirmed reset");
            var oldModel=shop.Model;string oldJob=shop.Stocker.CurrentJobId;
            shop.SetPanel(ShopPanel.Reset);shop.ConfirmNewGame();
            Check(shop.Model.Currency==0 && shop.Cycle.DayNumber==1 && !shop.Staff.Hired && !shop.Stocking.Employee.Hired &&
                shop.Staff.PaidDay==0 && shop.Stocking.Employee.PaidDay==0 && shop.Stocking.CaptureSave(shop.Model).targets.Length==0 &&
                shop.Model.StorageIds.Length==0 && shop.Stocker.CurrentJobId==null && !oldModel.GetStockTransfer(oldJob).HasValue &&
                !shop.Stocker.Actor.gameObject.activeSelf && !shop.Cashier.Actor.gameObject.activeSelf && shop.Customers.ActiveCount==0,
                "Confirmed new game clears both employees, receipts, targets, former claims and worker visibility in this isolated save");
        }

        IEnumerator StockerEnter()
        {
            yield return EnterCounter();
            Check(shop.OpenStockerMenu() && shop.Panel==ShopPanel.Stocker,"Physical counter opens Nori's staffing panel");
        }
        void StockerParkPlayer()
        {
            shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;
            shop.Player.Teleport(new Vector3(-2.5f,.2f,-4.7f),Quaternion.identity);
        }
        string StockerRack()
        {
            foreach(var item in shop.Layout.PlacedItems) if(item.DefinitionId==ShopLayout.RackDefinitionId) return item.Id;
            throw new InvalidOperationException("Stocker route requires its one placed source rack.");
        }
        string StockerInventory()
        {
            var result=new StringBuilder(GateStock());
            foreach(string id in shop.Model.StorageIds)
            { var source=shop.Model.GetStorage(id);result.Append('|').Append(id).Append(':').Append(source.HasValue?source.Value.Id:"empty"); }
            result.Append("|incubator:").Append(shop.Model.Incubator.SpecimenId);
            return result.ToString();
        }
        IEnumerator StockerWait(Func<bool> condition,float timeout,string reason,bool rejectBlocked=true)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(!condition())
            {
                ObserveCustomers();
                if(shop.World.LabDoor.OpenFraction>.98f && shop.Player.transform.position.z< -3 &&
                    shop.Stocker.Actor && Mathf.Abs(shop.Stocker.Actor.transform.position.x)<2.05f)
                    stockerDoorOpenedAlone=true;
                if(rejectBlocked && shop.Stocker.Blocked)
                    throw new InvalidOperationException("Stocker blocked while waiting for "+reason+" / "+shop.Stocker.Status+" / position="+
                        (shop.Stocker.Actor?shop.Stocker.Actor.transform.position.ToString():"missing"));
                if(Time.realtimeSinceStartup>deadline)
                    throw new TimeoutException("Stocker waiting for "+reason+" / "+shop.Stocker.Status+" / job="+shop.Stocker.CurrentJobId+
                        " / position="+(shop.Stocker.Actor?shop.Stocker.Actor.transform.position.ToString():"missing"));
                yield return null;
            }
            ObserveCustomers();
        }
    }
}
#endif
