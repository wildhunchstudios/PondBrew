#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator PlacementRecoveryScenario()
        {
            limitations.Add("Placement recovery uses an isolated cash/table fixture, accelerated recipe stock, two scripted casual visitors and a temporary entry blocker. Navigation and retained-order recovery use real frames; this is not production pacing or native UI coverage.");
            Check(shop.ValidationMode && shop.SavePath == Path.Combine(evidence, "smoke-save.json"), "Placement recovery uses only the isolated smoke slot");
            bool previousValidationInput = shop.AllowValidationInput; shop.AllowValidationInput = false;
            shop.SetPanel(ShopPanel.Reset); shop.ConfirmNewGame();
            var customers = shop.Customers; var nav = customers.Navigation;
            customers.ArrivalsEnabled = false; customers.MaxVisitors = 2;
            customers.ForcedArchetypeId = null; customers.ScriptedArchetypes.Clear();
            var seededSave = shop.Model.CaptureSave(); seededSave.currency = 36;
            Check(GameModel.TryRestore(shop.Config, seededSave, out var seededModel, out _), "Create explicit 36-credit recovery fixture");
            var seededLayout = new ShopLayout();
            Check(seededLayout.TryCommit(seededModel, ShopDayPhase.Night, null, -4, -1, 0, out var tableId).Success,
                "Recovery fixture owns one table after its 12-credit debit");
            var fixture = new SlotData { game = seededModel.CaptureSave(), layout = seededLayout.CaptureSave(),
                cycle = new ShopDayCycle().CaptureSave(), trades = new ShopTradeLedger().CaptureSave(), reputation = new ShopReputation().CaptureSave() };
            Check(SaveStore.Write(shop.SavePath, fixture, out var writeError) && shop.ReloadSaved(), "Load seeded table/cash fixture / " + writeError);
            shop.SetPanel(ShopPanel.None);
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            Check(shop.OpenForDay(), "Open recovery fixture for two unpaid visitors");
            var first = Spawn(CustomerArchetypes.Casual); yield return Hold(1);
            var second = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => Reserved(first) && Reserved(second), 30, "two exact unpaid recovery claims");
            shop.SetPanel(ShopPanel.Pause);
            var liveModel = shop.Model; var liveLayout = shop.Layout; var liveReports = shop.Reports;
            var actorA = customers.ActorOf(first); var actorB = customers.ActorOf(second);
            Vector3 positionA = actorA.transform.position, positionB = actorB.transform.position;
            string gameBefore = JsonUtility.ToJson(liveModel.CaptureSave()), layoutBefore = JsonUtility.ToJson(liveLayout.CaptureSave());
            string reportsBefore = JsonUtility.ToJson(liveReports.CaptureSave()), cycleBefore = JsonUtility.ToJson(shop.Cycle.CaptureSave());
            string reputationBefore = JsonUtility.ToJson(shop.Reputation.CaptureSave());
            bool blockedBefore = shop.AutosaveBlocked; string saveErrorBefore = shop.SaveError;
            Check(liveLayout.TryPreview(tableId, 4, -1, 0, out var alternative).Success, "Prepare a different valid saved layout without changing live ownership");
            fixture = new SlotData { game = liveModel.CaptureSave(), layout = alternative.CaptureSave(),
                cycle = shop.Cycle.CaptureSave(), trades = liveReports.CaptureSave(), reputation = shop.Reputation.CaptureSave() };
            Check(SaveStore.Write(shop.SavePath, fixture, out writeError), "Write alternate layout for failed-reload probe / " + writeError);
            string savedBytes = Convert.ToBase64String(File.ReadAllBytes(shop.SavePath));
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Validation only / blocked customer entry"; blocker.transform.SetParent(shop.World.transform, true);
            blocker.transform.position = nav.EntryPoint + Vector3.up * .8f; blocker.transform.localScale = new Vector3(3, 2, 2);
            Physics.SyncTransforms(); int buildsBefore = nav.BuildCount;
            try
            {
                Check(!shop.ReloadSaved() && nav.BuildCount > buildsBefore, "Changed-layout reload rejects an actual blocked NavMesh preflight");
                Check(RecoveryStateUnchanged(), "Failed reload preserves live model, layout, reports, visitors, exact claims, save guard and saved bytes");
                shop.SetPanel(ShopPanel.Reset); shop.ConfirmNewGame();
                Check(RecoveryStateUnchanged() && shop.Panel == ShopPanel.Reset,
                    "Failed new-game layout preflight preserves owned table, cash, unpaid visits and original saved bytes");
                yield return null; yield return null; // Native reattachment can surface after the synchronous bake returns.
                Check(RecoveryStateUnchanged() && Vector3.Distance(positionA, actorA.transform.position) < .001f &&
                    Vector3.Distance(positionB, actorB.transform.position) < .001f,
                    "Rejected layout remains atomic after two native frames while visitors stay paused");
            }
            finally
            {
                blocker.GetComponent<Collider>().enabled = false; blocker.SetActive(false); Destroy(blocker);
                Physics.SyncTransforms();
            }
            Check(nav.Rebuild(shop.World), "Removing the temporary blocker restores the current world's actual routes");
            yield return null; yield return null;
            Check(RecoveryStateUnchanged(), "Successful navigation recovery alone changes no gameplay authority or saved bytes");
            shop.SetPanel(ShopPanel.None);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 25,
                "retained customer paths reaching checkout after rejected layout");
            var front = customers.NextToServe; var back = front == first ? second : first;
            int beforeSale = shop.Model.Currency, income = first.Price + second.Price;
            var counter = StationOf(StationKind.Counter).transform;
            shop.Player.Teleport(counter.TransformPoint(new Vector3(0, .2f, -2.5f)), counter.rotation);
            shop.SetPanel(ShopPanel.Counter);
            Check(shop.ServeCustomer() && front.Outcome == CustomerOutcome.Sold, "First retained order still pays exactly once after navigation recovery");
            yield return WaitFor(() => customers.QueuePlaceOf(back) == 0 && Flat(ActorPosition(back), nav.QueueSpot(0)) < .5f,
                12, "the retained second customer physically advancing in line");
            Check(shop.ServeCustomer() && back.Outcome == CustomerOutcome.Sold && shop.Model.Currency == beforeSale + income &&
                shop.Reports.Current.sales == 2 && shop.Reports.Current.interrupted == 0 && shop.Reports.Current.pathProblems == 0,
                "Both preserved claims complete with exact payment and no artificial interrupted/path-loss outcomes");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "recovered customers leaving normally");
            Check(shop.CloseForNight(), "Close recovered trading fixture");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night, 8, "recovery fixture closing");
            shop.SetPanel(ShopPanel.Reset); shop.ConfirmNewGame();
            Check(shop.Layout.Items.Count == 0 && shop.Model.Currency == 0 && shop.Reports.Last == null,
                "Final successful reset removes recovery fixture ownership and progression");
            shop.AllowValidationInput = previousValidationInput;

            bool RecoveryStateUnchanged()
            {
                return ReferenceEquals(liveModel, shop.Model) && ReferenceEquals(liveLayout, shop.Layout) && ReferenceEquals(liveReports, shop.Reports) &&
                    JsonUtility.ToJson(shop.Model.CaptureSave()) == gameBefore && JsonUtility.ToJson(shop.Layout.CaptureSave()) == layoutBefore &&
                    JsonUtility.ToJson(shop.Reports.CaptureSave()) == reportsBefore && JsonUtility.ToJson(shop.Cycle.CaptureSave()) == cycleBefore &&
                    JsonUtility.ToJson(shop.Reputation.CaptureSave()) == reputationBefore && shop.AutosaveBlocked == blockedBefore && shop.SaveError == saveErrorBefore &&
                    customers.ActiveCount == 2 && customers.ActorOf(first) == actorA && customers.ActorOf(second) == actorB &&
                    shop.Model.GetReservation(first.TargetTankId)?.CustomerId == first.Id && shop.Model.GetReservation(first.TargetTankId)?.SpecimenId == first.SpecimenId &&
                    shop.Model.GetReservation(second.TargetTankId)?.CustomerId == second.Id && shop.Model.GetReservation(second.TargetTankId)?.SpecimenId == second.SpecimenId &&
                    Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)) == savedBytes;
            }
        }
    }
}
#endif
