using System;
using System.Linq;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class AutomationTests
    {
        const string RequestId = "f2c95e1e30c744199e4b3cd3f47e6d17";
        static GameConfig Config()
        {
            var c = GameConfig.CreateDefault(); c.StorageIds = new[] { "rack" }; c.ConditionerIds = new[] { "vat" }; return c;
        }
        static GameModel Restore(SaveData save)
        {
            Assert.That(GameModel.TryRestore(Config(), save, out var model, out var failure), Is.True, failure.ToString()); return model;
        }
        static GameModel Fixture()
        {
            var save = new GameModel(Config()).CaptureSave(); save.currency = 300; save.upgradeOwned = true;
            save.sparkResearchUnlocked = save.mistResearchUnlocked = true;
            save.growers[2].units = save.growers[3].units = 4;
            save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };
            return Restore(save);
        }
        static ShopAutomation Modules(GameModel game, bool collect = true, bool dose = true, string species = "pondlet")
        {
            var a = new ShopAutomation();
            Assert.That(a.Purchase(game, true) && a.Purchase(game, false), Is.True);
            if (collect) Assert.That(a.SetEnabled(true, true), Is.True);
            if (dose) Assert.That(a.SetEnabled(false, true), Is.True);
            Assert.That(a.SelectRecipe(game, species), Is.True);
            return a;
        }
        static void Request(ShopAutomation a, GameModel game) => Assert.That(a.Request(game, RequestId), Is.True);
        static int Total(GameModel g, CultureKind c) => g.GetGrower(c).Units + g.Available(c) + g.Staged(c);
        static int[] Totals(GameModel g) => Enumerable.Range(0, 4).Select(i => Total(g, (CultureKind)i)).ToArray();
        static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());

        [TestCase(true, ShopAutomation.CollectorCost)] [TestCase(false, ShopAutomation.DoserCost)]
        public void PurchaseCostsOnceAndStartsDisabled(bool collector, int cost)
        {
            var g = Fixture(); var a = new ShopAutomation();
            Assert.That(a.Purchase(g, collector), Is.True); Assert.That(g.Currency, Is.EqualTo(300-cost));
            Assert.That(a.CollectorEnabled || a.DoserEnabled || a.IsPending, Is.False);
            Assert.That(a.Purchase(g, collector), Is.False); Assert.That(g.Currency, Is.EqualTo(300-cost));
        }
        [TestCase("upgrade")] [TestCase("discovery")] [TestCase("funds")]
        public void FailedPurchasePreservesMoneyAndOwnership(string missing)
        {
            var s = Fixture().CaptureSave(); if (missing == "upgrade") s.upgradeOwned = false;
            if (missing == "discovery") { s = new GameModel(Config()).CaptureSave(); s.currency = 300; s.upgradeOwned = true; } if (missing == "funds") s.currency = 0;
            var g = Restore(s); var a = new ShopAutomation(); int cash = g.Currency;
            Assert.That(a.Purchase(g, true) || a.Purchase(g, false), Is.False);
            Assert.That(g.Currency, Is.EqualTo(cash)); Assert.That(a.CollectorOwned || a.DoserOwned, Is.False);
        }
        [TestCase("pondlet")] [TestCase("bubblebelly")] [TestCase("ribbonfin")] [TestCase("lanternfin")] [TestCase("driftwing")]
        public void OneBatchCollectsDeficitsAndStagesExactRecipeWithoutGrowing(string id)
        {
            var g = Fixture(); var a = Modules(g, species:id); var recipe = g.GetSpecies(id);
            Ok(g.Collect(CultureKind.Bloom)); int[] totals = Totals(g); int money = g.Currency; Request(a,g);
            int steps = 0;
            while (a.IsPending && steps++ < 8)
            {
                var next = a.NextStep(g,false); int before = g.Available(CultureKind.Bloom)+g.Available(CultureKind.Flow)+g.Available(CultureKind.Spark)+g.Available(CultureKind.Mist);
                Assert.That(a.Step(g,false),Is.True); Assert.That(Totals(g),Is.EqualTo(totals));
                if (next.Kind == AutomationStepKind.Collect)
                    Assert.That(g.Available(CultureKind.Bloom)+g.Available(CultureKind.Flow)+g.Available(CultureKind.Spark)+g.Available(CultureKind.Mist),Is.EqualTo(before+1));
            }
            Assert.That(a.IsPending,Is.False); Assert.That(g.Currency,Is.EqualTo(money));
            for(int i=0;i<4;i++) Assert.That(g.Staged((CultureKind)i),Is.EqualTo(ShopAutomation.Required(recipe,(CultureKind)i)));
            Assert.That(g.Incubator.Phase,Is.EqualTo(IncubatorPhase.Dosing)); Assert.That(g.Incubator.SpecimenId,Is.Null);
            Assert.That(a.Step(g,false),Is.False); Assert.That(Totals(g),Is.EqualTo(totals));
        }
        [Test]
        public void CollectorOnlyFinishesWithAvailableCulturesAndDoserOnlyWaitsThenDoses()
        {
            var g=Fixture(); var a=Modules(g,true,false); Request(a,g);
            Assert.That(a.Step(g,false)&&a.Step(g,false)&&a.Step(g,false),Is.True);
            Assert.That(a.IsPending,Is.False); Assert.That(g.Available(CultureKind.Bloom),Is.EqualTo(1)); Assert.That(g.Staged(CultureKind.Flow),Is.Zero);
            Assert.That(a.SetEnabled(true,false)&&a.SetEnabled(false,true),Is.True); Request(a,g);
            Assert.That(a.Step(g,false),Is.True); Assert.That(g.Staged(CultureKind.Flow),Is.EqualTo(1));
            Ok(g.ClearDoses()); Ok(g.ReleaseCulture(CultureKind.Flow)); Request(a,g);
            Assert.That(a.Step(g,false),Is.False); Assert.That(a.IsPending,Is.True);
            Ok(g.Collect(CultureKind.Flow)); Assert.That(a.Step(g,false),Is.True);
        }
        [Test]
        public void CancelDisableAndRestoreRetainPartialCollectionWithoutRepeatingCompletedRequest()
        {
            var g=Fixture();var a=Modules(g);Request(a,g);Assert.That(a.Step(g,false),Is.True);
            var totals=Totals(g);Assert.That(a.SetEnabled(true,false)&&a.SetEnabled(false,false),Is.True);
            Assert.That(ShopAutomation.TryRestore(a.CaptureSave(),g,out a),Is.True);Assert.That(a.RequestId,Is.EqualTo(RequestId));
            Assert.That(a.Step(g,false),Is.False);Assert.That(Totals(g),Is.EqualTo(totals));
            Assert.That(a.Cancel(),Is.True);Assert.That(a.Cancel(),Is.False);Assert.That(Totals(g),Is.EqualTo(totals));
            Assert.That(a.SetEnabled(true,true)&&a.SetEnabled(false,true),Is.True);Request(a,g);
            Assert.That(a.Step(g,false)&&a.Step(g,false),Is.True);Assert.That(a.IsPending,Is.False);
            Assert.That(ShopAutomation.TryRestore(a.CaptureSave(),g,out a),Is.True);Assert.That(a.Step(g,false),Is.False);
            Assert.That(Totals(g),Is.EqualTo(totals));Assert.That(g.Staged(CultureKind.Bloom),Is.EqualTo(1));
        }
        [Test]
        public void PendingRequestCannotChangeRecipeOrDuplicateAndSnapshotIsDetached()
        {
            var g=Fixture();var a=Modules(g);Request(a,g);var snapshot=a.CaptureSave();snapshot.recipeId="bubblebelly";snapshot.collectorOwned=false;
            Assert.That(a.SelectRecipe(g,"bubblebelly")||a.Request(g,Guid.NewGuid().ToString("N")),Is.False);
            Assert.That(a.RequestId,Is.EqualTo(RequestId));Assert.That(a.RecipeId,Is.EqualTo("pondlet"));Assert.That(a.CollectorOwned,Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void ManualMixtureAndBusyReadyOutputRemainUntouched(bool ready)
        {
            var g=Fixture();var a=Modules(g);Request(a,g);Ok(g.Collect(CultureKind.Flow));Ok(g.Dose(CultureKind.Flow));
            var totals=Totals(g);Assert.That(a.Step(g,false),Is.False);Assert.That(g.Staged(CultureKind.Flow),Is.EqualTo(1));Assert.That(Totals(g),Is.EqualTo(totals));
            Ok(g.Collect(CultureKind.Bloom));Ok(g.Dose(CultureKind.Bloom));Ok(g.BeginIncubation());if(ready)g.Tick(10);
            string specimen=g.Incubator.SpecimenId;float time=g.Incubator.SecondsRemaining;
            Assert.That(a.Step(g,false),Is.False);Assert.That(g.Incubator.SpecimenId,Is.EqualTo(specimen));Assert.That(g.Incubator.SecondsRemaining,Is.EqualTo(time));
        }
        [Test]
        public void FullReservoirAndEmptyGrowerWaitWithoutDiscardingAndRecover()
        {
            var s=Fixture().CaptureSave();s.availableBloom=8;var g=Restore(s);var a=Modules(g);Request(a,g);var totals=Totals(g);
            Assert.That(a.Step(g,false),Is.False);Assert.That(Totals(g),Is.EqualTo(totals));
            Ok(g.ReleaseCulture(CultureKind.Bloom));Assert.That(a.Step(g,false),Is.True);Assert.That(g.ReservedCultureCount,Is.EqualTo(8));
            s=Fixture().CaptureSave();s.growers[0].units=0;g=Restore(s);a=Modules(g);Request(a,g);
            Assert.That(a.Step(g,false),Is.False);g.Tick(2);Assert.That(a.Step(g,false),Is.True);
        }
        [Test]
        public void ConditioningReturnReservationConsumesSharedCapacity()
        {
            var s=Fixture().CaptureSave();s.availableBloom=6;s.availableSpark=1;s.storage[0].specimenId="existing";s.storage[0].speciesId="pondlet";
            var g=Restore(s);Ok(g.BeginConditioning("vat",new[]{"rack"},new[]{"existing"}));
            Ok(g.Collect(CultureKind.Bloom));Assert.That(g.ReservedCultureCount,Is.EqualTo(8));
            var a=Modules(g);Request(a,g);Assert.That(a.Step(g,false),Is.False);Assert.That(g.Available(CultureKind.Flow),Is.Zero);
            Ok(g.ReleaseCulture(CultureKind.Bloom));Assert.That(a.Step(g,false),Is.True);Assert.That(g.ReservedCultureCount,Is.EqualTo(8));
        }
        [Test]
        public void TechnicianPriorityAndRealReservationPreserveInputsAndClaim()
        {
            var g=Fixture();var a=Modules(g);Request(a,g);Assert.That(a.Step(g,true),Is.False);
            Ok(g.Collect(CultureKind.Bloom));Ok(g.Collect(CultureKind.Flow));
            Ok(g.ReserveTechnicianStart("job","moss","pondlet","rack","planned"));var totals=Totals(g);
            Assert.That(a.Step(g,false),Is.False);Assert.That(Totals(g),Is.EqualTo(totals));Assert.That(g.TechnicianClaim.Value.JobId,Is.EqualTo("job"));
        }
        [Test]
        public void MatchingTechnicianHandoffCollectsButLeavesDosingAndProductionToMoss()
        {
            var save = Fixture().CaptureSave();
            save.availableBloom = save.availableFlow = save.availableSpark = save.availableMist = 0;
            save.incubator.stagedBloom = save.incubator.stagedFlow =
                save.incubator.stagedSpark = save.incubator.stagedMist = 0;
            var g = Restore(save);
            var a = Modules(g, true, true);
            Request(a, g);
            int[] totals = Totals(g); int money = g.Currency;
            int steps = 0;
            while (a.IsPending && steps++ < 8)
            {
                var next = a.NextStep(g, false, true);
                if (next.Kind == AutomationStepKind.Finish)
                    Assert.That(a.NextStep(g, false).Kind, Is.EqualTo(AutomationStepKind.Dose),
                        "The existing manual-preparation route keeps its default behavior.");
                Assert.That(next.Kind == AutomationStepKind.Collect || next.Kind == AutomationStepKind.Finish, Is.True);
                Assert.That(a.Step(g, false, true), Is.True);
                Assert.That(Totals(g), Is.EqualTo(totals), "Collection moves each culture without creating or deleting it.");
            }
            var recipe = g.GetSpecies("pondlet");
            Assert.That(a.IsPending, Is.False);
            Assert.That(g.Available(CultureKind.Bloom), Is.GreaterThanOrEqualTo(recipe.Bloom));
            Assert.That(g.Available(CultureKind.Flow), Is.GreaterThanOrEqualTo(recipe.Flow));
            Assert.That(g.Staged(CultureKind.Bloom) + g.Staged(CultureKind.Flow) +
                g.Staged(CultureKind.Spark) + g.Staged(CultureKind.Mist), Is.Zero,
                "The installed Doser must not stage a mixture over Moss's matching queue entry.");
            Ok(g.ReserveTechnicianStart("moss-job", "moss", "pondlet", "rack", "route-specimen"));
            Ok(g.CompleteTechnicianStart("moss-job", "moss"));
            Assert.That(g.Incubator.Phase, Is.EqualTo(IncubatorPhase.Incubating));
            Assert.That(g.Incubator.SpecimenId, Is.EqualTo("route-specimen"));
            Assert.That(g.Currency, Is.EqualTo(money), "The production handoff does not charge again.");
        }

        [TestCase("version")] [TestCase("collector-enabled-unowned")] [TestCase("doser-enabled-unowned")]
        [TestCase("unknown")] [TestCase("unknown-request")] [TestCase("no-owned-request")] [TestCase("bad-id")]
        [TestCase("empty-guid")] [TestCase("guid-dashes")] [TestCase("whitespace-id")] [TestCase("whitespace-recipe")]
        public void InvalidSaveIsRejected(string corruption)
        {
            var g=Fixture();var a=Modules(g);Request(a,g);var s=a.CaptureSave();
            switch(corruption)
            {
                case "version":s.version=99;break;
                case "collector-enabled-unowned":s.collectorOwned=false;break;
                case "doser-enabled-unowned":s.doserOwned=false;break;
                case "unknown":s.recipeId="unknown";break;
                case "unknown-request":s.recipeId=null;break;
                case "no-owned-request":s.collectorOwned=s.collectorEnabled=s.doserOwned=s.doserEnabled=false;break;
                case "bad-id":s.requestId="bad";break;
                case "empty-guid":s.requestId=Guid.Empty.ToString("N");break;
                case "guid-dashes":s.requestId=Guid.NewGuid().ToString();break;
                case "whitespace-id":s.requestId=" ";break;
                case "whitespace-recipe":s.recipeId=" ";break;
            }
            Assert.That(ShopAutomation.TryRestore(s,g,out _),Is.False);
        }
        [Test]
        public void FreshOffAndDisabledPendingRoundTripWithoutProgressGrants()
        {
            var g=new GameModel(Config());Assert.That(ShopAutomation.TryRestore(new ShopAutomation().CaptureSave(),g,out var a),Is.True);
            Assert.That(a.CollectorOwned||a.DoserOwned||a.IsPending,Is.False);Assert.That(a.SetEnabled(true,true)||a.Request(g,RequestId),Is.False);
            g=Fixture();a=Modules(g);Request(a,g);a.SetEnabled(true,false);a.SetEnabled(false,false);
            Assert.That(ShopAutomation.TryRestore(a.CaptureSave(),g,out var b),Is.True);Assert.That(b.RequestId,Is.EqualTo(RequestId));
        }

        [Test]
        public void StockPolicyNeedsRecipeAndDestinationAndRoundTripsPauseAndDesiredLevel()
        {
            var g=Fixture();var a=Modules(g);
            Assert.That(a.SetMaintainEnabled(true),Is.False);
            Assert.That(a.SetDesiredStockCount(3),Is.True);
            Assert.That(a.SetDestination(g,"tank-a"),Is.True);
            Assert.That(a.SetMaintainEnabled(true),Is.True);
            Assert.That(a.SetPaused(true),Is.True);
            var saved=a.CaptureSave();
            Assert.That(ShopAutomation.TryRestore(saved,g,out var restored),Is.True);
            Assert.That(restored.MaintainEnabled,Is.True);Assert.That(restored.Paused,Is.True);
            Assert.That(restored.DesiredStockCount,Is.EqualTo(3));Assert.That(restored.DestinationTankId,Is.EqualTo("tank-a"));
            Assert.That(restored.SetPaused(false),Is.True);Assert.That(restored.SetMaintainEnabled(false),Is.True);
            Assert.That(restored.MaintainEnabled,Is.False);
        }

        [Test]
        public void PolicyRequestWaitsForItsMatchingMossQueueStartAfterHelperFinishes()
        {
            var g=Fixture();var a=Modules(g);a.SetDestination(g,"tank-a");a.SetMaintainEnabled(true);
            Assert.That(a.RequestForPolicy(g,RequestId,2),Is.True);
            Assert.That(ShopAutomation.TryRestore(a.CaptureSave(),g,out var restored),Is.True);
            Assert.That(restored.PolicyHandoffWaiting,Is.True);Assert.That(restored.PolicyQueueCountAtRequest,Is.EqualTo(2));
            Assert.That(restored.ClearPolicyHandoffWait(),Is.True);
            Assert.That(restored.PolicyHandoffWaiting,Is.False);Assert.That(restored.PolicyQueueCountAtRequest,Is.Zero);
        }

        [Test]
        public void VersionOneAutomationSaveMigratesToDisabledOneSpecimenPolicy()
        {
            var g=Fixture();var save=Modules(g).CaptureSave();save.version=1;
            save.maintainEnabled=true;save.paused=true;save.desiredStockCount=6;save.destinationTankId="tank-a";
            save.policyHandoffWaiting=true;save.policyQueueCountAtRequest=3;
            Assert.That(ShopAutomation.TryRestore(save,g,out var restored),Is.True);
            Assert.That(restored.MaintainEnabled,Is.False);Assert.That(restored.Paused,Is.False);
            Assert.That(restored.DesiredStockCount,Is.EqualTo(1));Assert.That(restored.DestinationTankId,Is.Null);
            Assert.That(restored.PolicyHandoffWaiting,Is.False);
        }

        [TestCase(0)] [TestCase(10)]
        public void InvalidDesiredStockCountIsRejected(int count)
        {
            var g=Fixture();var save=Modules(g).CaptureSave();save.desiredStockCount=count;
            Assert.That(ShopAutomation.TryRestore(save,g,out _),Is.False);
        }

        [Test]
        public void MissingDestinationAfterRoomEditLoadsAndCanBeRepaired()
        {
            var g=Fixture();var a=Modules(g);a.SetDestination(g,"tank-a");a.SetMaintainEnabled(true);
            var save=a.CaptureSave();save.destinationTankId="retired-display-slot";
            Assert.That(ShopAutomation.TryRestore(save,g,out var restored),Is.True);
            Assert.That(restored.MaintainEnabled,Is.True);
            Assert.That(restored.SetDestination(g,"tank-b"),Is.True);
        }
    }
}
