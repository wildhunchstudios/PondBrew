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
        IEnumerator StaffScenario()
        {
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionStaffSmoke") != null &&
                Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),
                "Cashier route uses only its isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors==2 && !shop.Customers.UseBaseReputationEffectsForValidation &&
                shop.Customers.ForcedArchetypeId==null,"Cashier route retains modern two-customer and reputation rules");
            previousInputSettings=InputSystem.settings; smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionStaffKeyboard");
            cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionStaffMouse");
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled=false; shop.AmbientComedyEnabled=false;
            if(ShopSession.Arg("-evolutionStaffReload")!=null)
            {
                yield return StaffRestartScenario();
                yield break;
            }
            limitations.Add("R07.1/R07.2 isolated cashier route: labelled 60-credit and zero-credit save fixtures; stock uses existing recipe commands with accelerated production. Scripted customer types/arrivals use real browsing, queueing and movement. Staff buttons are invoked through the same session commands as the HUD at the physical counter; native mouse clicking, economy pacing, audio acceptance, all layouts and performance are not established.");
            Check(shop.Staff!=null && shop.Cashier && !shop.Staff.Hired && shop.Model.Currency==0 &&
                shop.Cycle.Phase==ShopDayPhase.Night && shop.Cycle.DayNumber==1,"Fresh shop has no staff, wages, money or completed duty period");
            yield return StaffEnter();
            Check(shop.HireCashier() && shop.Staff.Hired && !shop.Staff.Scheduled && shop.Model.Currency==0,
                "Hiring is free and explicitly starts off schedule");
            Check(!shop.HireCashier() && shop.Model.Currency==0,"Repeated hiring cannot duplicate the employee or charge money");
            Check(shop.SetCashierScheduled(true) && shop.Staff.Scheduled && !shop.Staff.IsOnDuty(1) && shop.Staff.PaidDay==0,
                "Unaffordable scheduling persists the plan without debt, payment or duty");
            Check(!shop.PayCashierDuty() && shop.Model.Currency==0 && shop.Staff.PaidWage==0,
                "No-funds payment retry changes neither money nor wage receipt");
            yield return GateCapture("280-staff-night-no-funds");

            // Probe real location and panel guards, not merely the domain night rule.
            shop.Player.Teleport(new Vector3(0,.2f,3),Quaternion.identity);
            Check(!shop.OpenStaffMenu() && !shop.DismissCashier() && !shop.SetCashierScheduled(false) &&
                !shop.SetCashierPaused(true) && !shop.PayCashierDuty() && shop.Staff.Hired && shop.Staff.Scheduled && !shop.Staff.Paused,
                "Lab-side staff management is rejected without changing the roster or money");
            yield return EnterCounter(); shop.SetPanel(ShopPanel.Journal);
            Check(!shop.OpenStaffMenu() && !shop.SetCashierScheduled(false),"Physical proximity alone cannot bypass the required counter/staff panel");
            yield return StaffEnter();

            StaffCreditFixture(60,"night-one hiring/wage validation credit");
            Check(shop.Staff.Scheduled && shop.Staff.PaidDay==0 && shop.Model.Currency==60,
                "Loading an unpaid scheduled employee does not silently charge or start duty");
            yield return StaffEnter();
            var unpaidModel=shop.Model; string unpaidRoster=JsonUtility.ToJson(shop.Staff.CaptureSave());
            string unpaidFile=File.ReadAllText(shop.SavePath), blockedTemp=shop.SavePath+".tmp";
            Check(!File.Exists(blockedTemp) && !Directory.Exists(blockedTemp),"Isolated wage-save obstruction path starts unused");
            Directory.CreateDirectory(blockedTemp);
            try
            {
                Check(!shop.PayCashierDuty() && ReferenceEquals(shop.Model,unpaidModel) && shop.Model.Currency==60 &&
                    JsonUtility.ToJson(shop.Staff.CaptureSave())==unpaidRoster && File.ReadAllText(shop.SavePath)==unpaidFile,
                    "Failed wage save retains the original model, exact money, roster, receipts and file");
            }
            finally { Directory.Delete(blockedTemp); }
            Check(shop.PayCashierDuty() && shop.Model.Currency==56 && shop.Staff.IsOnDuty(1) &&
                shop.Staff.PaidDay==1 && shop.Staff.WagesForDay(1)==ShopStaff.CashierWage,
                "Recovered first payment commits exactly four credits for night one and day one");
            Check(shop.PayCashierDuty() && shop.Model.Currency==56,"Repeated paid-period retry is idempotent");
            Check(shop.SetCashierScheduled(false) && !shop.Staff.IsOnDuty(1) && shop.SetCashierScheduled(true) &&
                shop.Staff.IsOnDuty(1) && shop.Model.Currency==56,"Off/on scheduling preserves the paid period without another charge");
            Check(shop.DismissCashier() && !shop.Staff.Hired && shop.HireCashier() && shop.SetCashierScheduled(true) &&
                shop.Staff.IsOnDuty(1) && shop.Model.Currency==56,"Night dismissal and re-hiring retain the receipt without refund or duplicate wage");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==56 && shop.Staff.IsOnDuty(1),
                "Paid roster reload neither charges nor loses duty");
            yield return StaffEnter();
            yield return GateCapture("281-staff-night-paid");
            yield return null;
            StaffActorBounds();
            Check(shop.Cashier.Actor.GetComponentsInChildren<Collider>(true).Length==0 &&
                shop.Cashier.Actor.GetComponentsInChildren<Station>(true).Length==0,
                "Cashier visual has no collider, station, stock or payment authority");
            int navigationBuilds=shop.Customers.Navigation.BuildCount;

            SeedStock("pondlet","tank-a");
            string firstSpecimen=shop.Model.GetTank("tank-a").Value.Id;
            Check(shop.SetCashierPaused(true),"Player can explicitly pause paid employee work");
            yield return EnterCounter(); Check(shop.OpenForDay(),"Open the first staffed trading day");
            Check(shop.OpenStaffMenu(),"Physical counter opens staff controls during trading");
            int dayMoney=shop.Model.Currency;
            var dayModel=shop.Model;
            Check(!shop.DismissCashier() && !shop.HireCashier() && !shop.SetCashierScheduled(false) && !shop.PayCashierDuty() &&
                shop.Model.Currency==dayMoney && shop.Staff.Hired && shop.Staff.Scheduled,
                "Daytime hire, dismiss, schedule and payment commands reject without roster or currency changes");
            yield return GateCapture("282-staff-day-controls");
            var first=Spawn(CustomerArchetypes.Casual);
            Vector3 arrival=ActorPosition(first);
            yield return WaitFor(()=>first.Phase==CustomerPhase.Queued,30,"first staffed customer at checkout");
            Check(Vector3.Distance(arrival,ActorPosition(first))>1 && first.SpecimenId==firstSpecimen && shop.Model.Currency==dayMoney,
                "Actual visitor walks from entry, reserves the exact Pondlet and queues without payment");
            shop.SetPanel(ShopPanel.None);
            GateAim(new Vector3(-1.1f,.2f,-4.3f),shop.Cashier.Actor.transform.position+Vector3.up*1.2f);
            yield return GateCapture("283-cashier-and-customer");
            yield return StaffEnter(); Check(shop.SetCashierPaused(false) && ReferenceEquals(dayModel,shop.Model),
                "Daytime pause/resume retains the same live model and customer reservation");
            shop.SetPanel(ShopPanel.None);shop.Player.Teleport(new Vector3(0,.2f,3),Quaternion.identity);
            Check(!shop.IsAtCounter,"Player leaves the counter for the laboratory during cashier service");
            yield return WaitFor(()=>first.Outcome==CustomerOutcome.Sold,ShopCashier.ServiceSeconds+6,"autonomous sale while player is in lab");
            Check(shop.Model.Currency==dayMoney+first.Price && !shop.Model.GetTank("tank-a").HasValue &&
                !shop.Model.GetReservation("tank-a").HasValue && shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==first.Price,
                "Cashier commits one exact reserved specimen and agreed payment into daily results");
            Check(!shop.ServeCashier(first) && shop.Model.Currency==dayMoney+first.Price,"Repeated worker completion cannot pay the old visitor twice");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"first paid visitor leaving");

            // Bounded two-visitor fixture: manual service competes with an in-progress cashier.
            yield return StaffEnter(); Check(shop.SetCashierPaused(true),"Pause cashier to form the explicit two-visitor race fixture");
            SeedStock("pondlet","tank-a"); SeedStock("bubblebelly","tank-b");
            var manual=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>manual.Phase==CustomerPhase.Queued,30,"manual race customer at queue front");
            var follower=Spawn(CustomerArchetypes.Collector);
            yield return WaitFor(()=>follower.Phase==CustomerPhase.Queued,30,"second race customer at queue back");
            Check(shop.Customers.NextToServe==manual && manual.TargetTankId!=follower.TargetTankId && shop.Customers.WaitingCount==2,
                "Two customers own different exact specimens and an ordered service queue");
            int raceMoney=shop.Model.Currency, raceSales=shop.Reports.Current.sales;
            string followingSpecimen=follower.SpecimenId;
            Check(shop.SetCashierPaused(false),"Resume cashier for the contested front order");
            yield return WaitFor(()=>shop.Cashier.Target==manual && shop.Cashier.Progress>.02f,3,"cashier claiming front order");
            string oldJob=shop.Cashier.CurrentJobId; float progress=shop.Cashier.Progress;
            Check(!string.IsNullOrEmpty(oldJob) && oldJob.Contains(manual.Id) && progress<1,"Worker exposes a stable job tied to the exact visitor");
            string unfinishedStock=GateStock();
            Check(!shop.ServeCashier(manual) && shop.Model.Currency==raceMoney && shop.Reports.Current.sales==raceSales &&
                GateStock()==unfinishedStock && manual.Phase==CustomerPhase.Queued && shop.Cashier.Target==manual &&
                shop.Cashier.CurrentJobId==oldJob && shop.Cashier.Progress==progress,
                "Early worker completion rejects before its service time without changing money, stock, reservations, sales or job progress");
            shop.SetPanel(ShopPanel.Pause);
            float patience=manual.QueueSecondsRemaining; Vector3 workerPosition=shop.Cashier.Actor.transform.position;
            yield return new WaitForSeconds(.65f);
            Check(shop.Cashier.CurrentJobId==oldJob && shop.Cashier.Target==manual && Mathf.Abs(shop.Cashier.Progress-progress)<.0001f &&
                Mathf.Abs(manual.QueueSecondsRemaining-patience)<.0001f && Vector3.Distance(workerPosition,shop.Cashier.Actor.transform.position)<.0001f,
                "Global pause freezes worker progress, job ownership, queue patience and employee position");
            shop.SetPanel(ShopPanel.Counter);
            Check(shop.ServeCustomer() && manual.Outcome==CustomerOutcome.Sold && shop.Model.Currency==raceMoney+manual.Price,
                "Player may finish the exact front order while the cashier was partway through it");
            Check(!shop.ServeCashier(manual) && shop.Model.Currency==raceMoney+manual.Price &&
                shop.Model.GetTank(follower.TargetTankId).Value.Id==followingSpecimen && follower.Outcome!=CustomerOutcome.Sold,
                "Stale worker completion neither duplicates payment nor redirects to the following customer's fish");
            yield return WaitFor(()=>shop.Cashier.Target==follower,8,"cashier acquiring new job after manual service");
            Check(shop.Cashier.CurrentJobId!=oldJob && shop.Cashier.CurrentJobId.Contains(follower.Id) && shop.Cashier.Progress<.35f,
                "Following customer gets a fresh named job and a fresh service timer");
            yield return WaitFor(()=>follower.Outcome==CustomerOutcome.Sold,ShopCashier.ServiceSeconds+6,"following customer automatic service");
            Check(shop.Model.Currency==raceMoney+manual.Price+follower.Price && shop.Reports.Current.sales==raceSales+2 &&
                !shop.Model.GetTank("tank-a").HasValue && !shop.Model.GetTank("tank-b").HasValue,
                "Mixed manual and automatic queue service reconciles exactly two sales and two specimens");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"both race visitors leaving");

            // Disabled and missing presentation must block work without silently committing a sale.
            SeedStock("pondlet","tank-a");
            var blocked=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>shop.Cashier.Target==blocked && shop.Cashier.Progress>.02f,30,"cashier work before actor disable");
            int blockedMoney=shop.Model.Currency; string blockedFish=blocked.SpecimenId;
            var employee=shop.Cashier.Actor; employee.enabled=false;
            yield return new WaitForSeconds(ShopCashier.ServiceSeconds+.3f);
            Check(shop.Cashier.Target==null && shop.Cashier.Progress==0 && blocked.Outcome!=CustomerOutcome.Sold &&
                shop.Model.Currency==blockedMoney && shop.Model.GetTank("tank-a").Value.Id==blockedFish && !shop.ServeCashier(blocked),
                "Disabled employee cancels its transient timer while preserving unpaid reserved stock");
            employee.enabled=true;
            yield return WaitFor(()=>shop.Cashier.Target==blocked && shop.Cashier.Progress>.02f,3,"reenabled cashier starts a fresh job");
            Destroy(employee.gameObject);
            yield return new WaitForSeconds(ShopCashier.ServiceSeconds+.3f);
            Check(!shop.Cashier.Actor && shop.Cashier.Target==null && shop.Model.Currency==blockedMoney && !shop.ServeCashier(blocked),
                "Missing employee remains visibly blocked and cannot sell through an absent actor");
            yield return EnterCounter();
            Check(shop.ServeCustomer() && shop.Model.Currency==blockedMoney+blocked.Price,"Manual checkout remains available when the worker visual is missing");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"manual recovery visitor leaving");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Cashier.Actor && shop.Staff.IsOnDuty(1),
                "Explicit reload restores the employee without another wage");

            // Loading cancels an unpaid job; its specimen and paid duty survive.
            SeedStock("pondlet","tank-a");
            var interrupted=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>shop.Cashier.Target==interrupted && shop.Cashier.Progress>.02f,30,"cashier progress before reload");
            shop.SetPanel(ShopPanel.Pause);
            int reloadMoney=shop.Model.Currency, reloadSales=shop.Reports.Current.sales;
            string reloadFish=interrupted.SpecimenId;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==reloadMoney && shop.Reports.Current.sales==reloadSales &&
                shop.Staff.PaidDay==1 && shop.Staff.PaidWage==4 && shop.Cashier.Target==null && shop.Cashier.CurrentJobId==null &&
                shop.Cashier.Progress==0 && shop.Customers.ActiveCount==0 && !shop.Model.GetReservation("tank-a").HasValue &&
                shop.Model.GetTank("tank-a").Value.Id==reloadFish,"Mid-job reload cancels transient ownership and releases the exact unpaid fish while retaining wages and previous sales");
            shop.SetPanel(ShopPanel.None);
            var recovered=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>recovered.Outcome==CustomerOutcome.Sold,32,"restored cashier selling the released fish");
            Check(shop.Model.Currency==reloadMoney+recovered.Price && shop.Reports.Current.sales==reloadSales+1 &&
                !shop.Model.GetTank("tank-a").HasValue,"Recovered cashier sells the preserved fish once after reload");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"recovered paid visitor leaving");

            yield return EnterCounter(); int closingMoney=shop.Model.Currency;
            Check(shop.CloseForNight(),"Close the completed staffed shift");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"next staff duty period");
            Check(shop.Cycle.DayNumber==2 && shop.Model.Currency==closingMoney-4 && shop.Staff.PaidDay==2 &&
                shop.Staff.PreviousPaidDay==1 && shop.Staff.WagesForDay(1)==4 && shop.Staff.WagesForDay(2)==4,
                "Next preparation night charges exactly one new four-credit duty period and retains the completed day's wage");
            int secondNightMoney=shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==secondNightMoney && shop.Staff.PaidDay==2,
                "Next-night reload does not duplicate the automatic wage");
            yield return StaffEnter();
            Check(shop.SetCashierScheduled(false) && shop.DismissCashier() && shop.Staff.PaidDay==2 &&
                shop.HireCashier() && shop.Model.Currency==secondNightMoney && !shop.Staff.Scheduled,
                "Night schedule cancellation and dismissal preserve paid history; re-hiring starts off schedule");
            yield return EnterCounter(); Check(shop.OpenForDay() && shop.CloseForNight(),"Advance an explicitly empty unscheduled day");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"unpaid third night");
            Check(shop.Cycle.DayNumber==3 && shop.Model.Currency==secondNightMoney && shop.Staff.PaidDay==2,
                "An unscheduled employee does not charge a new period");
            StaffCreditFixture(0,"zero-credit manual-work recovery validation");
            yield return StaffEnter();
            Check(shop.SetCashierScheduled(true) && !shop.PayCashierDuty() && !shop.Staff.IsOnDuty(3) && shop.Model.Currency==0,
                "Later insufficient funds preserve the employee and schedule without debt or work");
            yield return GateCapture("284-staff-later-no-funds");
            SeedStock("pondlet","tank-a");
            yield return EnterCounter(); Check(shop.OpenForDay(),"A zero-credit unpaid shop can still open normally");
            var fallback=Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(()=>fallback.Phase==CustomerPhase.Queued,30,"manual fallback visitor queued");
            yield return new WaitForSeconds(ShopCashier.ServiceSeconds+.3f);
            Check(fallback.Outcome!=CustomerOutcome.Sold && shop.Model.Currency==0 && shop.Cashier.Target==null,
                "Scheduled unpaid cashier never performs a free or hidden checkout");
            Check(shop.OpenStaffMenu(),"Day menu explains the unpaid manual-work fallback");
            yield return GateCapture("285-staff-day-manual-fallback");
            shop.SetPanel(ShopPanel.Counter);
            Check(shop.ServeCustomer() && shop.Model.Currency==fallback.Price && shop.Staff.WagesForDay(3)==0,
                "Manual service earns the full agreed price while the unpaid cashier remains off duty");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"fallback customer leaving");
            Check(shop.CloseForNight(),"Manual fallback shift can close safely");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"funded recovery duty period");
            Check(shop.Cycle.DayNumber==4 && shop.Model.Currency==fallback.Price-4 && shop.Staff.IsOnDuty(4) &&
                shop.Staff.WagesForDay(3)==0 && shop.Staff.WagesForDay(4)==4,"Manual income funds the next period once without collecting unpaid historical debt");

            shop.SetPanel(ShopPanel.None);shop.Player.Teleport(new Vector3(0,.2f,-3.8f),Quaternion.identity);
            editedWalkingMetres=0;editedDoorCrossings=0;
            yield return EditedWalkTo(new Vector2(0,2.5f),"staffed shop laboratory door");
            yield return EditedWalkTo(new Vector2(0,-3.8f),"staffed shop return to sales floor");
            Check(editedDoorCrossings==2 && shop.Customers.Navigation.BuildCount==navigationBuilds,
                "Staff management and service preserve both-way keyboard walking, automatic door and existing customer navigation bake");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Staff.IsOnDuty(4) && shop.Cashier.Target==null,
                "Final saved manual-to-staff recovery resumes with paid duty and no stale job");
            File.Copy(shop.SavePath,Path.Combine(evidence,"staff-progress.json"),true);
        }

        IEnumerator StaffRestartScenario()
        {
            limitations.Add("Fresh-process cashier continuation consumes a copied isolated day-four fixture from the main staff route. The process starts with validation's empty in-memory model and explicitly calls normal ReloadSaved. Original fixture bytes are archived before commands. One recipe is accelerated and one casual visitor is explicitly spawned; travel, checkout and the next night use ordinary runtime frames. Native mouse clicks, pacing, sound and general performance are unverified.");
            Check(File.Exists(shop.SavePath),"Fresh-process staff route has an explicitly supplied isolated input fixture");
            byte[] original=File.ReadAllBytes(shop.SavePath);
            File.WriteAllBytes(Path.Combine(evidence,"staff-restart-original.json"),original);
            var fixture=JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            Check(fixture!=null && fixture.game!=null && fixture.cycle!=null && fixture.staff!=null &&
                fixture.cycle.phase==ShopDayPhase.Night && fixture.cycle.dayNumber==4 && fixture.staff.hired &&
                fixture.staff.scheduled && !fixture.staff.paused && fixture.staff.paidDay==4 && fixture.staff.paidWage==4,
                "Restart input is the hired, scheduled, paid night-four cashier state from the completed main route");
            int originalMoney=fixture.game.currency;
            string originalGame=JsonUtility.ToJson(fixture.game), originalStaff=JsonUtility.ToJson(fixture.staff);
            Check(!shop.Staff.Hired && shop.Model.Currency==0 && shop.Cycle.DayNumber==1,
                "New validation process begins with its ordinary fresh in-memory state before explicit restore");
            Check(shop.ReloadSaved() && shop.Model.Currency==originalMoney && shop.Cycle.DayNumber==4 &&
                shop.Cycle.Phase==ShopDayPhase.Night && shop.Staff.IsOnDuty(4) &&
                JsonUtility.ToJson(shop.Model.CaptureSave())==originalGame && JsonUtility.ToJson(shop.Staff.CaptureSave())==originalStaff &&
                Convert.ToBase64String(File.ReadAllBytes(shop.SavePath))==Convert.ToBase64String(original),
                "Actual fresh-process reload preserves exact model, currency, duty receipts and original file bytes without a charge");
            Check(shop.Cashier.Actor && shop.Cashier.Actor.isActiveAndEnabled && shop.Cashier.Target==null &&
                shop.Cashier.CurrentJobId==null && shop.Cashier.Progress==0 && shop.Customers.ActiveCount==0,
                "Fresh process restores the worker visual with no remembered visitors or job progress");
            Check(shop.ReloadSaved() && shop.Model.Currency==originalMoney && JsonUtility.ToJson(shop.Staff.CaptureSave())==originalStaff,
                "Repeated fresh-process reload cannot collect the same duty wage again");
            Check(!shop.Model.GetTank("tank-a").HasValue,"Restart route begins with the main route's empty display");
            SeedStock("pondlet","tank-a");
            string specimen=shop.Model.GetTank("tank-a").Value.Id;
            yield return EnterCounter(); Check(shop.OpenForDay(),"Restored paid worker's day four opens normally");
            Check(shop.Model.Currency==originalMoney && shop.Staff.WagesForDay(4)==4,
                "Opening the previously paid day does not charge again");
            var visitor=Spawn(CustomerArchetypes.Casual);
            Vector3 entered=ActorPosition(visitor);
            shop.SetPanel(ShopPanel.None);shop.Player.Teleport(new Vector3(0,.2f,3),Quaternion.identity);
            yield return WaitFor(()=>shop.Cashier.Target==visitor,30,"fresh-process cashier starting a real checkout");
            Check(visitor.SpecimenId==specimen && Vector3.Distance(entered,ActorPosition(visitor))>1 && !shop.IsAtCounter,
                "Restored cashier serves a visitor who physically reached the queue while player is in the lab");
            yield return WaitFor(()=>visitor.Outcome==CustomerOutcome.Sold,ShopCashier.ServiceSeconds+6,"fresh-process autonomous sale");
            Check(shop.Model.Currency==originalMoney+visitor.Price && !shop.Model.GetTank("tank-a").HasValue &&
                !shop.Model.GetReservation("tank-a").HasValue && shop.Reports.Current.sales==1 && shop.Reports.Current.revenue==visitor.Price,
                "Fresh-process cashier pays the exact agreed sale once and consumes its one specimen");
            Check(!shop.ServeCashier(visitor) && shop.Model.Currency==originalMoney+visitor.Price,
                "Duplicate worker completion after fresh-process restore cannot pay again");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"fresh-process paid visitor leaving");
            yield return EnterCounter(); Check(shop.CloseForNight(),"Fresh-process staffed shift closes normally");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"fresh-process next duty period");
            int expectedMoney=originalMoney+visitor.Price-4;
            Check(shop.Cycle.DayNumber==5 && shop.Model.Currency==expectedMoney && shop.Staff.IsOnDuty(5) &&
                shop.Staff.PaidDay==5 && shop.Staff.PreviousPaidDay==4 && shop.Staff.WagesForDay(4)==4 && shop.Staff.WagesForDay(5)==4,
                "Fresh-process next night charges exactly one new period and retains day-four wage history");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==expectedMoney && shop.Staff.IsOnDuty(5) &&
                shop.Cashier.Target==null && shop.Cashier.CurrentJobId==null,
                "New period survives another file reload without payment replay or stale work");
            yield return StaffEnter(); yield return GateCapture("286-staff-fresh-process-paid");
            File.Copy(shop.SavePath,Path.Combine(evidence,"staff-restart-progress.json"),true);
        }

        IEnumerator StaffEnter()
        {
            yield return EnterCounter();
            Check(shop.OpenStaffMenu() && shop.Panel==ShopPanel.Staff,"Physical service counter opens the staff panel");
        }

        void StaffCreditFixture(int credits,string label)
        {
            Check(shop.ValidationMode && shop.Cycle.Phase==ShopDayPhase.Night && shop.Customers.ActiveCount==0 && shop.SaveNow(),
                "Credit fixture requires an isolated saved night without customers / "+label);
            var fixture=JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)); fixture.game.currency=credits;
            Check(SaveStore.Write(shop.SavePath,fixture,out var error) && shop.ReloadSaved() && shop.Model.Currency==credits,
                "Load labelled "+credits+"-credit fixture / "+label+" / "+error);
            File.Copy(shop.SavePath,Path.Combine(evidence,credits==0?"staff-zero-credit-fixture.json":"staff-starting-credit-fixture.json"),true);
        }

        void StaffActorBounds()
        {
            var actor=shop.Cashier.Actor;
            Check(actor && actor.isActiveAndEnabled,"Hired cashier is a visible active full-size employee");
            Bounds combined=default; bool found=false;
            foreach(var pose in new[]{CharacterSamplePose.Idle,CharacterSamplePose.Greet})
                for(int sample=0;sample<12;sample++)
                {
                    actor.Pose(pose,sample*.23f);
                    foreach(var renderer in actor.GetComponentsInChildren<MeshRenderer>())
                    {
                        if(!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        bool sign=false;
                        for(var parent=renderer.transform;parent && parent!=actor.transform;parent=parent.parent)
                            if(parent.name=="Cashier duty sign") { sign=true;break; }
                        if(sign)continue;
                        if(!found) { combined=renderer.bounds;found=true; } else combined.Encapsulate(renderer.bounds);
                    }
                }
            actor.Pose(CharacterSamplePose.Idle,0);
            File.WriteAllText(Path.Combine(evidence,"cashier-mesh-bounds.txt"),"Idle/Greet 12 samples each; active meshes excluding duty sign.\n"+combined+"\nMin="+combined.min+"\nMax="+combined.max+"\n");
            Check(found && combined.min.x>=-6.85f && combined.max.x<=-6.12f && combined.max.y>1.5f && combined.max.y<1.9f,
                "Idle/greet cashier mesh remains full-size between west wall and counter, excluding its sign / "+combined);
        }
    }
}
#endif
