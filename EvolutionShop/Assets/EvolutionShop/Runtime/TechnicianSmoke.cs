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
        IEnumerator TechnicianScenario()
        {
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionTechnicianSmoke")!=null &&
                Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),
                "Technician route uses only its explicitly isolated evidence save");
            Check(shop.Customers.Ready && shop.Customers.MaxVisitors==2 && !shop.Customers.UseBaseReputationEffectsForValidation &&
                shop.Customers.ForcedArchetypeId==null,"Technician route retains normal customer concurrency and reputation rules");
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionTechnicianKeyboard");
            cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionTechnicianMouse");
            syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;
            if(ShopSession.Arg("-evolutionTechnicianReload")!=null)
            { yield return TechnicianRestartScenario();yield break; }
            limitations.Add("R07.4 isolated technician route uses a labelled 300-credit construction/wage fixture, accelerated manual discovery preparation and direct fixture sales of those two discovery specimens, selected customer arrivals and fixture camera/player positions. Incubator culture collection calls the normal inventory commands using existing grower units. Rack/counter interactions and placement use synthetic input; staff menu actions call guarded HUD session commands. All technician travel, service, incubation, output delivery, stocker travel, cashier service and next-night wages run on ordinary frames. A temporary world-child barrier and isolated .tmp-directory obstruction inject route/save failures. This does not establish native mouse-menu usability, ordinary economy balance, every layout, audio acceptance or performance.");
            Check(shop.Laboratory!=null && shop.Technician && !shop.Laboratory.Employee.Hired && shop.Model.Currency==0,
                "Fresh shop has no technician employment, money or assigned work");
            yield return TechnicianEnter();
            Check(!shop.QueueTechnicianRecipe("pondlet") && !shop.QueueTechnicianRecipe("missing"),
                "Unknown and undiscovered recipes cannot enter the technician queue");
            Check(shop.HireTechnician() && !shop.HireTechnician() && !shop.Laboratory.Employee.Scheduled && shop.Model.Currency==0,
                "Moss is hired once at no fee and starts off schedule");
            Check(shop.SetTechnicianScheduled(true) && !shop.PayTechnicianDuty() && !shop.Laboratory.Employee.IsOnDuty(1) &&
                shop.Laboratory.Employee.PaidDay==0 && shop.Model.Currency==0,"No-funds schedule creates no debt, receipt or work");
            shop.Player.Teleport(new Vector3(0,.2f,3),Quaternion.identity);
            Check(!shop.OpenTechnicianMenu() && !shop.PayTechnicianDuty(),"Lab-side commands cannot bypass the physical staffing counter");
            StaffCreditFixture(300,"R07.4 technician construction and independent wages");
            yield return TechnicianEnter();
            string beforeRoster=JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model));
            string beforeFile=File.ReadAllText(shop.SavePath);var beforeModel=shop.Model;
            string blockedTemp=shop.SavePath+".tmp";
            Check(!File.Exists(blockedTemp) && !Directory.Exists(blockedTemp),"Fault path is unused inside the isolated evidence save");
            Directory.CreateDirectory(blockedTemp);
            try
            {
                Check(!shop.PayTechnicianDuty() && ReferenceEquals(beforeModel,shop.Model) && shop.Model.Currency==300 &&
                    JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model))==beforeRoster && File.ReadAllText(shop.SavePath)==beforeFile,
                    "Failed technician wage write leaves live model, cash, roster and original file unchanged");
            }
            finally { Directory.Delete(blockedTemp); }
            Check(shop.PayTechnicianDuty() && shop.Model.Currency==296 && shop.Laboratory.Employee.IsOnDuty(1),
                "Moss begins the night/day duty period for exactly four credits");
            Check(shop.PayTechnicianDuty() && shop.SetTechnicianScheduled(false) && shop.SetTechnicianScheduled(true) &&
                shop.Model.Currency==296,"Repeated payment and schedule toggles cannot charge the same technician duty twice");
            Check(shop.SetTechnicianPaused(true),"Pause Moss during physical construction and manual discovery preparation");
            yield return StaffEnter();Check(shop.HireCashier() && shop.SetCashierScheduled(true),"Hire and pay Pip independently");
            yield return StockerEnter();Check(shop.HireStocker() && shop.SetStockerScheduled(true) && shop.SetStockerPaused(true) &&
                shop.Model.Currency==288,"Hire and pay Nori independently; all three roles cost twelve credits total");
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));
            string rack=StockerRack();string[] slots=shop.Layout.GetStorageSlotIds(rack);
            Check(slots.Length==2 && shop.Model.Currency==256,"One normally purchased lab rack provides two output compartments");
            Ok(shop.Perform(shop.Model.BuyUpgrade()),"Buy normal siphon capacity upgrade with labelled fixture funds");
            SpecimenProduce("pondlet");Ok(shop.Model.TransferToTank("tank-a"),"Fixture discovery Pondlet moves to display");
            Ok(shop.Model.SellFromTank("tank-a"),"Labelled fixture sale clears initial Pondlet discovery");
            SpecimenProduce("bubblebelly");Ok(shop.Model.TransferToTank("tank-b"),"Fixture discovery Bubblebelly moves to display");
            Ok(shop.Model.SellFromTank("tank-b"),"Labelled fixture sale clears initial Bubblebelly discovery");
            shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save isolated prepared discovery and empty-rack fixture");
            int money=shop.Model.Currency;
            yield return TechnicianEnter();
            Check(shop.QueueTechnicianRecipe("pondlet") && shop.QueueTechnicianRecipe("bubblebelly") &&
                shop.MoveTechnicianPriority("bubblebelly",-1) && shop.Laboratory.Queue[0].speciesId=="bubblebelly" &&
                shop.MoveTechnicianPriority("bubblebelly",1) && shop.RemoveTechnicianRecipe("bubblebelly") &&
                shop.Laboratory.Queue.Length==1 && shop.Laboratory.Queue[0].count==1,
                "Player can order known recipes, change strict priority and remove waiting work without starting anything");
            shop.SetPanel(ShopPanel.TechnicianOrders);yield return GateCapture("310-technician-orders");
            Check(shop.SetTechnicianPaused(false),"Resume Moss with a known order and no collected cultures");StockerParkPlayer();
            yield return new WaitForSeconds(ShopTechnician.RetrySeconds+.3f);
            Check(shop.Technician.CurrentJobId==null && shop.Laboratory.Queue[0].count==1 &&
                shop.Technician.Status.Contains("cultures") && shop.Model.Incubator.Phase==IncubatorPhase.Dosing,
                "Input shortage waits visibly and does not skip or consume the first recipe");
            Check(!shop.World.LabDoor.WantsOpen && shop.World.LabDoor.OpenFraction<.01f,
                "Idle hired laboratory crew leaves the opaque room door fully closed when the player is clear");
            TechnicianCollect("pondlet");Ok(shop.Model.Dose(CultureKind.Bloom),"Stage one manual Bloom to reserve the incubator for player work");
            yield return new WaitForSeconds(ShopTechnician.RetrySeconds+.3f);
            Check(shop.Technician.CurrentJobId==null && shop.Model.Staged(CultureKind.Bloom)==1 &&
                shop.Technician.Status.Contains("manual"),"Manual staged mixture prevents technician overwrite or ingredient theft");
            Ok(shop.Model.ClearDoses(),"Player clears their staged mixture intact");
            yield return TechnicianWait(()=>shop.Technician.CurrentJobId!=null,8,"first production claim");
            string job=shop.Technician.CurrentJobId;var claim=shop.Model.TechnicianClaim;
            string ingredients=TechnicianIngredients();string queue=JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model));
            Check(claim.HasValue && claim.Value.StartsRecipe && claim.Value.OwnerId==ShopStaff.TechnicianId &&
                claim.Value.SpeciesId=="pondlet" && claim.Value.StorageId==slots[0],
                "Stable production job reserves exact known recipe, planned specimen and empty rack destination");
            Check(!shop.StartTechnicianJob(job) && !shop.CompleteTechnicianJob(job) && TechnicianIngredients()==ingredients &&
                JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model))==queue,
                "Early public completion cannot bypass technician walking and service or decrement the queue");
            Check(!shop.Model.UseKnownRecipe("pondlet").Success && !shop.Model.Dose(CultureKind.Flow).Success &&
                !shop.Model.BeginIncubation().Success && TechnicianIngredients()==ingredients,
                "Manual recipe commands cannot steal the claimed machine or reserved culture budget");
            shop.SetPanel(ShopPanel.Pause);Vector3 position=shop.Technician.Actor.transform.position;
            float travel=shop.Technician.TravelDistance;yield return new WaitForSeconds(.4f);
            Check(shop.Technician.CurrentJobId==job && shop.Technician.TravelDistance==travel &&
                Vector3.Distance(position,shop.Technician.Actor.transform.position)<.0001f && TechnicianIngredients()==ingredients,
                "Global pause freezes technician work, movement and ingredient ownership");
            yield return TechnicianEnter();Check(shop.SetTechnicianPaused(true) && !shop.Model.TechnicianClaim.HasValue &&
                shop.Technician.CurrentJobId==null && TechnicianIngredients()==ingredients,
                "Role pause immediately releases transient station and destination claims without consuming a recipe");
            Ok(shop.Model.UseKnownRecipe("pondlet"),"Paused technician permits normal manual recipe selection");
            Ok(shop.Model.ClearDoses(),"Return manual doses to reservoir before resuming");
            Check(shop.SetTechnicianPaused(false),"Resume production after reversible manual work");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Technician.CurrentJobId!=null,8,"claim for physical route fault");
            var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.name="Validation-only technician obstacle";
            barrier.transform.SetParent(shop.World.transform,false);
            barrier.transform.position=shop.Technician.Actor.transform.position+Vector3.up;
            barrier.transform.localScale=new Vector3(1.4f,2,1.4f);Physics.SyncTransforms();
            try
            {
                yield return TechnicianWait(()=>shop.Technician.Blocked,8,"physical obstruction",false);
                Check(shop.Technician.CurrentJobId==null && !shop.Model.TechnicianClaim.HasValue && TechnicianIngredients()==ingredients &&
                    shop.Model.Incubator.Phase==IncubatorPhase.Dosing,"Physical overlap blocks visibly and releases ownership with ingredients safe");
                yield return new WaitForSeconds(ShopTechnician.RetrySeconds+.2f);
                Check(shop.Technician.Blocked && shop.Technician.CurrentJobId==null,"Blocked route waits for explicit recovery without invisible repeated consumption");
            }
            finally { Destroy(barrier); }
            yield return null;Physics.SyncTransforms();yield return TechnicianEnter();
            Check(shop.RetryTechnician(),"Explicit retry resumes after physical obstacle removal");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Technician.CurrentJobId!=null,8,"claim before disabled component");
            shop.Technician.enabled=false;yield return new WaitForSeconds(.2f);
            Check(!shop.Model.TechnicianClaim.HasValue && shop.Technician.CurrentJobId==null &&
                !shop.Technician.Actor.gameObject.activeSelf && TechnicianIngredients()==ingredients,
                "Disabled technician cancels transient ownership and cannot silently start production");
            shop.Technician.enabled=true;
            yield return TechnicianWait(()=>shop.Technician.CurrentJobId!=null,8,"claim before removed visual");
            Destroy(shop.Technician.Actor.gameObject);yield return null;yield return null;
            Check(!shop.Technician.Actor && shop.Technician.Blocked && !shop.Model.TechnicianClaim.HasValue &&
                TechnicianIngredients()==ingredients,"Missing worker visual blocks safely instead of consuming cultures or respawning silently");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Technician.Actor && shop.Laboratory.Employee.IsOnDuty(1) &&
                shop.Model.Currency==money,"Explicit reload restores worker visibility and paid period without another wage");
            StockerParkPlayer();yield return TechnicianWait(()=>shop.Technician.CurrentJobId!=null,8,"claim before failed start save");
            beforeModel=shop.Model;beforeFile=File.ReadAllText(shop.SavePath);int starts=shop.Technician.StartedJobs;
            Directory.CreateDirectory(blockedTemp);
            try
            {
                yield return TechnicianWait(()=>shop.Technician.Blocked,30,"start persistence failure",false);
                Check(shop.SaveError!=null && ReferenceEquals(beforeModel,shop.Model) && TechnicianIngredients()==ingredients &&
                    shop.Laboratory.Queue[0].count==1 && shop.Laboratory.ActiveSpecimenId==null &&
                    shop.Model.Incubator.Phase==IncubatorPhase.Dosing && shop.Technician.StartedJobs==starts &&
                    !shop.Model.TechnicianClaim.HasValue && File.ReadAllText(shop.SavePath)==beforeFile,
                    "Failed start save changes no ingredient, queue, machine, payment or published model and releases transient claim");
            }
            finally { Directory.Delete(blockedTemp); }
            yield return TechnicianEnter();Check(shop.RetryTechnician(),"Retry unchanged recipe after save path recovers");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Laboratory.ActiveSpecimenId!=null,30,"first actual committed start");
            string firstId=shop.Laboratory.ActiveSpecimenId;
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Incubating && shop.Model.Incubator.SpecimenId==firstId &&
                shop.Laboratory.Queue.Length==0 && shop.Technician.StartedJobs==starts+1 &&
                shop.Model.Available(CultureKind.Bloom)==0 && shop.Model.Available(CultureKind.Flow)==0 && shop.Model.Currency==money,
                "Actual technician arrival spends one exact recipe once, consumes one waiting start and persists matching batch ticket");
            shop.SetPanel(ShopPanel.Pause);float remaining=shop.Model.Incubator.SecondsRemaining;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Laboratory.ActiveSpecimenId==firstId &&
                shop.Model.Incubator.SpecimenId==firstId && shop.Model.Incubator.SecondsRemaining==remaining &&
                shop.Laboratory.Queue.Length==0 && shop.Model.Currency==money,
                "Mid-incubation save/reload restores exact countdown, consumed order and batch with no offline work or wage replay");
            yield return TechnicianEnter();
            Check(shop.DismissTechnician() && !shop.Laboratory.Employee.Hired && shop.Laboratory.ActiveSpecimenId==null &&
                shop.Laboratory.Queue.Length==0 && shop.Model.Incubator.SpecimenId==firstId,
                "Dismissal clears assignments while the committed batch remains safe in the physical incubator");
            Check(shop.HireTechnician() && shop.SetTechnicianScheduled(true) && shop.SetTechnicianPaused(true) &&
                shop.Model.Currency==money,"Rehire in an already paid period does not duplicate technician wages");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,15,"unattended machine completes dismissed assignment");
            yield return SpecimenRackPanel(rack);Check(shop.StoreReadyInRack(slots[0]) && shop.Model.GetStorage(slots[0]).Value.Id==firstId,
                "Player manually collects the dismissed assignment as the same exact creature");
            yield return TechnicianEnter();Check(shop.QueueTechnicianRecipe("pondlet"),"Queue second batch for output delivery fault proof");
            TechnicianCollect("pondlet");Check(shop.SetTechnicianPaused(false),"Resume paid technician for another exact recipe");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Laboratory.ActiveSpecimenId!=null,30,"second actual production start");
            string producedId=shop.Laboratory.ActiveSpecimenId;
            GateAim(new Vector3(0,.2f,3),shop.Technician.Actor.transform.position+Vector3.up*1.1f);
            yield return GateCapture("311-technician-active");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Technician.Carrying,35,"technician collects ready output and carries parcel");
            Check(shop.Model.Incubator.SpecimenId==producedId && !shop.Model.GetStorage(slots[1]).HasValue &&
                shop.Technician.TravelDistance>1,"Carrying is presentation; exact specimen remains in incubator until durable rack delivery");
            GateAim(new Vector3(4.5f,.2f,2.1f),shop.Technician.Actor.transform.position+Vector3.up*1.1f);
            yield return GateCapture("312-technician-carrying");StockerParkPlayer();
            int completed=shop.Technician.CompletedJobs;beforeModel=shop.Model;Directory.CreateDirectory(blockedTemp);
            try
            {
                yield return TechnicianWait(()=>shop.Technician.Blocked,30,"output delivery save failure",false);
                Check(shop.SaveError!=null && ReferenceEquals(beforeModel,shop.Model) && shop.Model.Incubator.Phase==IncubatorPhase.Ready &&
                    shop.Model.Incubator.SpecimenId==producedId && !shop.Model.GetStorage(slots[1]).HasValue &&
                    shop.Laboratory.ActiveSpecimenId==producedId && shop.Technician.CompletedJobs==completed &&
                    !shop.Model.TechnicianClaim.HasValue && shop.Model.Currency==money,
                    "Failed output write preserves incubator ownership and active ticket without duplicate storage or money change");
            }
            finally { Directory.Delete(blockedTemp); }
            yield return TechnicianEnter();Check(shop.RetryTechnician(),"Explicit retry resumes safe incubator output after save recovery");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Model.GetStorage(slots[1]).HasValue,35,"physical delivery to second rack compartment");
            Check(shop.Model.GetStorage(slots[1]).Value.Id==producedId && shop.Model.Incubator.Phase==IncubatorPhase.Dosing &&
                shop.Laboratory.ActiveSpecimenId==null && shop.Technician.CompletedJobs==completed+1 &&
                shop.StoredSpecimenCount==2 && shop.Model.Currency==money && shop.SaveError==null,
                "One durable output delivery moves the same creature to its rack exactly once and retires the batch ticket");
            yield return TechnicianEnter();Check(shop.QueueTechnicianRecipe("bubblebelly"),"Queue a second known species for full-buffer proof");
            TechnicianCollect("bubblebelly");ingredients=TechnicianIngredients();StockerParkPlayer();
            yield return new WaitForSeconds(ShopTechnician.RetrySeconds+.3f);
            Check(shop.Technician.CurrentJobId==null && shop.Technician.Status.Contains("rack") && TechnicianIngredients()==ingredients &&
                shop.Laboratory.Queue[0].speciesId=="bubblebelly" && shop.Laboratory.Queue[0].count==1,
                "A full rack halts new starts without consuming available cultures or changing waiting priorities");
            yield return TechnicianEnter();Check(shop.CancelTechnicianAssignments() && shop.Laboratory.Queue.Length==0 &&
                shop.Model.GetStorage(slots[1]).Value.Id==producedId,"Clearing waiting assignments leaves stored creatures untouched");
            yield return StockerEnter();Check(shop.SetStockingTarget("tank-a","pondlet") && shop.SetStockerPaused(false),
                "Assign Nori one exact Pondlet display target and resume delivery");StockerParkPlayer();
            yield return StockerWait(()=>shop.Model.GetTank("tank-a").HasValue,40,"first safe rack specimen reaches display");
            Check(shop.Model.GetTank("tank-a").Value.Id==firstId && shop.Model.GetStorage(slots[1]).Value.Id==producedId,
                "Nori takes the first matching rack specimen while Moss's later output keeps its identity");
            yield return EnterCounter();Check(shop.OpenForDay(),"Open the three-worker integration day");
            var firstVisitor=Spawn(CustomerArchetypes.Casual);StockerParkPlayer();
            yield return WaitFor(()=>firstVisitor.Outcome==CustomerOutcome.Sold,40,"cashier sells first carried specimen");
            Check(firstVisitor.SpecimenId==firstId && shop.Model.Currency==money+firstVisitor.Price,
                "Pip sells the manually recovered first batch exactly once");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"first buyer leaves");
            yield return StockerWait(()=>shop.Model.GetTank("tank-a").HasValue,35,"Nori replenishes the display with Moss output");
            Check(shop.Model.GetTank("tank-a").Value.Id==producedId && !shop.Model.GetStorage(slots[1]).HasValue,
                "Moss's exact automatically produced and stored specimen reaches the configured shop display");
            var secondVisitor=Spawn(CustomerArchetypes.Casual);StockerParkPlayer();
            yield return WaitFor(()=>secondVisitor.Outcome==CustomerOutcome.Sold,40,"cashier sells technician-to-stocker output");
            Check(secondVisitor.SpecimenId==producedId && shop.Model.Currency==money+firstVisitor.Price+secondVisitor.Price &&
                shop.Reports.Current.sales==2 && !shop.ServeCashier(secondVisitor),
                "Actual production-to-storage-to-display-to-cashier chain consumes one specimen and pays its agreed price once");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"second buyer leaves");
            yield return EnterCounter();int beforeNight=shop.Model.Currency;Check(shop.CloseForNight(),"Close the three-worker day");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"night two all-role wage receipts");
            Check(shop.Cycle.DayNumber==2 && shop.Model.Currency==beforeNight-12 && shop.Staff.IsOnDuty(2) &&
                shop.Stocking.Employee.IsOnDuty(2) && shop.Laboratory.Employee.IsOnDuty(2) &&
                shop.Staff.WagesForDay(1)==4 && shop.Stocking.Employee.WagesForDay(1)==4 && shop.Laboratory.Employee.WagesForDay(1)==4,
                "Next preparation night pays all three independent four-credit schedules once and retains previous receipts");
            int nightMoney=shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==nightMoney && shop.Laboratory.Employee.IsOnDuty(2),
                "Reload cannot duplicate next-night staff wages");
            yield return TechnicianEnter();Check(shop.QueueTechnicianRecipe("pondlet") && shop.QueueTechnicianRecipe("pondlet"),
                "Prepare two waiting starts for exact fresh-process count proof");
            // The unstarted Bubblebelly ingredients remain collected; one Pondlet uses one of each without granting more.
            Check(shop.Model.Available(CultureKind.Bloom)>=1 && shop.Model.Available(CultureKind.Flow)>=1,
                "Unused full-buffer cultures remain available for next night's first recipe");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Laboratory.ActiveSpecimenId!=null,30,"paid night-two continuation batch starts");
            Check(shop.Laboratory.Queue.Length==1 && shop.Laboratory.Queue[0].count==1 &&
                shop.Model.Incubator.Phase==IncubatorPhase.Incubating && shop.Model.Currency==nightMoney,
                "Continuation saves one exact started batch and one future start without a new charge");
            shop.SetPanel(ShopPanel.Pause);
            Check(shop.SaveNow(),"Save exact active batch before new process verification");
            File.Copy(shop.SavePath,Path.Combine(evidence,"technician-progress.json"),true);
        }

        IEnumerator TechnicianRestartScenario()
        {
            limitations.Add("Fresh-process technician continuation reads a copied isolated paid-night-two active-batch fixture. Normal ReloadSaved verifies original bytes, exact countdown and all role receipts before normal-frame incubation, transport and checkout. The remaining queued order has insufficient collected Flow and safely waits. This route does not simulate offline progress, balance, human menu usability, sound or performance.");
            Check(File.Exists(shop.SavePath),"Fresh-process technician route has an explicit isolated input fixture");
            byte[] original=File.ReadAllBytes(shop.SavePath);File.WriteAllBytes(Path.Combine(evidence,"technician-restart-original.json"),original);
            var fixture=JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath));
            Check(fixture!=null && fixture.formatVersion==SaveStore.CurrentFormatVersion && fixture.cycle.dayNumber==2 &&
                fixture.cycle.phase==ShopDayPhase.Night && fixture.technician.employee.hired && fixture.technician.employee.scheduled &&
                !fixture.technician.employee.paused && fixture.technician.employee.paidDay==2 &&
                fixture.technician.queue.Length==1 && fixture.technician.queue[0].count==1 &&
                fixture.game.incubator.phase==IncubatorPhase.Incubating && fixture.technician.activeSpecimenId==fixture.game.incubator.specimenId,
                "Restart fixture is a paid night-two active technician batch with one future start");
            string gameJson=JsonUtility.ToJson(fixture.game),staffJson=JsonUtility.ToJson(fixture.staff),
                stockerJson=JsonUtility.ToJson(fixture.stocker),labJson=JsonUtility.ToJson(fixture.technician);
            int money=fixture.game.currency;string specimen=fixture.technician.activeSpecimenId;
            Check(!shop.Staff.Hired && !shop.Stocking.Employee.Hired && !shop.Laboratory.Employee.Hired && shop.Model.Currency==0,
                "Fresh validation process begins without applying fixture state or offline work");
            Check(shop.ReloadSaved() && shop.Model.Currency==money && shop.Staff.IsOnDuty(2) && shop.Stocking.Employee.IsOnDuty(2) &&
                shop.Laboratory.Employee.IsOnDuty(2) && JsonUtility.ToJson(shop.Model.CaptureSave())==gameJson &&
                JsonUtility.ToJson(shop.Staff.CaptureSave())==staffJson && JsonUtility.ToJson(shop.Stocking.CaptureSave(shop.Model))==stockerJson &&
                JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model))==labJson &&
                Convert.ToBase64String(File.ReadAllBytes(shop.SavePath))==Convert.ToBase64String(original),
                "Actual fresh process restores exact inventory, machine countdown, queue, all receipts and file bytes with no offline work or charge");
            Check(shop.ReloadSaved() && !shop.Model.TechnicianClaim.HasValue && shop.Technician.CurrentJobId==null &&
                shop.Laboratory.ActiveSpecimenId==specimen && shop.Laboratory.Queue[0].count==1 && shop.Model.Currency==money,
                "Repeated restore retains the durable ticket while reconstructing no transient claim or consumed start");
            yield return TechnicianEnter();shop.SetPanel(ShopPanel.TechnicianOrders);yield return GateCapture("313-technician-restart");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Model.GetTank("tank-a").HasValue,55,"fresh-process incubation, technician rack delivery and stocker display route");
            Check(shop.Model.GetTank("tank-a").Value.Id==specimen && shop.Model.Incubator.Phase==IncubatorPhase.Dosing &&
                shop.Laboratory.ActiveSpecimenId==null && shop.Laboratory.Queue[0].count==1 && shop.Model.Currency==money &&
                shop.Technician.StartedJobs==0 && shop.Technician.CompletedJobs==1 && shop.Stocker.CompletedJobs==1 &&
                shop.Technician.TravelDistance>1,
                "Restored active batch finishes and moves through both visible workers exactly once without restarting or consuming future work");
            yield return EnterCounter();Check(shop.OpenForDay(),"Open restored paid day two");
            var visitor=Spawn(CustomerArchetypes.Casual);StockerParkPlayer();
            yield return WaitFor(()=>visitor.Outcome==CustomerOutcome.Sold,40,"fresh-process cashier completes production chain");
            Check(visitor.SpecimenId==specimen && shop.Model.Currency==money+visitor.Price && shop.Reports.Current.sales==1 &&
                !shop.ServeCashier(visitor),"Fresh process sells restored exact technician output for one agreed payment");
            yield return WaitFor(()=>shop.Customers.ActiveCount==0,15,"restored buyer leaves");
            yield return EnterCounter();Check(shop.CloseForNight(),"Close restored duty period");
            yield return WaitFor(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"night three technician and shop wages");
            Check(shop.Cycle.DayNumber==3 && shop.Model.Currency==money+visitor.Price-12 && shop.Staff.IsOnDuty(3) &&
                shop.Stocking.Employee.IsOnDuty(3) && shop.Laboratory.Employee.IsOnDuty(3) &&
                shop.Laboratory.Employee.WagesForDay(2)==4 && shop.Laboratory.Employee.WagesForDay(3)==4,
                "New process pays the next three scheduled wages once while keeping technician's previous receipt");
            int paidMoney=shop.Model.Currency;
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Model.Currency==paidMoney,
                "Reload after night-three wages cannot replay any payment");
            Check(shop.SaveNow(),"Save final technician restart progress before isolated reset");
            File.Copy(shop.SavePath,Path.Combine(evidence,"technician-restart-progress.json"),true);
            TechnicianCollect("pondlet");StockerParkPlayer();
            yield return TechnicianWait(()=>shop.Technician.CurrentJobId!=null,8,"pending production claim before confirmed new game");
            var oldModel=shop.Model;Check(oldModel.TechnicianClaim.HasValue,"Reset proof begins with a real pending technician reservation");
            shop.SetPanel(ShopPanel.Reset);shop.ConfirmNewGame();
            Check(shop.Model.Currency==0 && shop.Cycle.DayNumber==1 && !shop.Staff.Hired && !shop.Stocking.Employee.Hired &&
                !shop.Laboratory.Employee.Hired && shop.Laboratory.Employee.PaidDay==0 && shop.Laboratory.Queue.Length==0 &&
                shop.Laboratory.ActiveSpecimenId==null && shop.Technician.CurrentJobId==null && !oldModel.TechnicianClaim.HasValue &&
                !shop.Technician.Actor.gameObject.activeSelf && !shop.Stocker.Actor.gameObject.activeSelf && !shop.Cashier.Actor.gameObject.activeSelf,
                "Confirmed isolated new game clears all roles, queue, receipts, batch ticket, old claims and worker visibility");
        }

        IEnumerator TechnicianEnter()
        {
            yield return EnterCounter();
            Check(shop.OpenTechnicianMenu() && shop.Panel==ShopPanel.Technician,"Physical counter opens Moss's staff controls");
        }
        void TechnicianCollect(string speciesId)
        {
            var recipe=shop.Model.GetSpecies(speciesId);
            for(int i=shop.Model.Available(CultureKind.Bloom);i<recipe.Bloom;i++) Ok(shop.Model.Collect(CultureKind.Bloom),"Collect available grower Bloom for technician");
            for(int i=shop.Model.Available(CultureKind.Flow);i<recipe.Flow;i++) Ok(shop.Model.Collect(CultureKind.Flow),"Collect available grower Flow for technician");
        }
        string TechnicianIngredients() => shop.Model.Available(CultureKind.Bloom)+":"+shop.Model.Available(CultureKind.Flow)+":"+
            shop.Model.Available(CultureKind.Spark)+":"+shop.Model.Available(CultureKind.Mist)+"/"+shop.Model.Incubator.Bloom+":"+
            shop.Model.Incubator.Flow+":"+shop.Model.Incubator.Spark+":"+shop.Model.Incubator.Mist;
        IEnumerator TechnicianWait(Func<bool> condition,float timeout,string reason,bool rejectBlocked=true)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(!condition())
            {
                ObserveCustomers();
                if(rejectBlocked && shop.Technician.Blocked)
                    throw new InvalidOperationException("Technician blocked while waiting for "+reason+" / "+shop.Technician.Status+" / position="+
                        (shop.Technician.Actor?shop.Technician.Actor.transform.position.ToString():"missing"));
                if(Time.realtimeSinceStartup>deadline)
                    throw new TimeoutException("Technician waiting for "+reason+" / "+shop.Technician.Status+" / job="+shop.Technician.CurrentJobId+
                        " / position="+(shop.Technician.Actor?shop.Technician.Actor.transform.position.ToString():"missing")+
                        " / phase="+shop.Model.Incubator.Phase+" / remaining="+shop.Model.Incubator.SecondsRemaining);
                yield return null;
            }
            ObserveCustomers();
        }
    }
}
#endif
