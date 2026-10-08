using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ShopLaboratoryTests
    {
        GameModel game;
        ShopLaboratory lab;
        ShopDayCycle cycle;

        [SetUp] public void Setup()
        {
            game = KnownGame(); lab = new ShopLaboratory(); cycle = new ShopDayCycle();
        }

        [Test] public void MossHasIndependentWagesAndCannotRestoreAnotherRole()
        {
            var cashier = new ShopStaff(); var stocker = new ShopStaff(ShopStaff.StockerId);
            foreach (var employee in new[] { cashier, stocker, lab.Employee })
            {
                Assert.That(employee.Hire(cycle), Is.True);
                Assert.That(employee.SetScheduled(true, cycle), Is.True);
                Assert.That(employee.TryBeginDuty(game, cycle), Is.True);
            }
            Assert.That(game.Currency, Is.EqualTo(88));
            Assert.That(lab.Employee.EmployeeId, Is.EqualTo("technician-moss"));
            Assert.That(lab.Employee.Wage, Is.EqualTo(4));
            Assert.That(lab.Employee.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(88));
            Assert.That(ShopStaff.TryRestore(lab.Employee.CaptureSave(), 1, out _), Is.False);
            Assert.That(ShopStaff.TryRestore(stocker.CaptureSave(), 1, ShopStaff.TechnicianId, out _), Is.False);
            Assert.That(ShopStaff.TryRestore(lab.Employee.CaptureSave(), 1, ShopStaff.TechnicianId, out var restored), Is.True);
            Assert.That(restored.IsOnDuty(1), Is.True);
        }

        [Test] public void OrderedQueueMergesOnlySameSpeciesAndRejectsOverflowWithoutChangingPriority()
        {
            string before = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(lab.AddOrder(game, "pondlet", 2), Is.True);
            Assert.That(lab.AddOrder(game, "bubblebelly"), Is.True);
            Assert.That(lab.AddOrder(game, "pondlet", 7), Is.True);
            Assert.That(lab.AddOrder(game, "pondlet"), Is.False);
            Assert.That(lab.Queue[0].speciesId, Is.EqualTo("pondlet"));
            Assert.That(lab.Queue[0].count, Is.EqualTo(9));
            Assert.That(lab.Queue[1].count, Is.EqualTo(1));
            Assert.That(lab.Employee.Hired, Is.False);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
        }

        [TestCase(null, 1)] [TestCase("missing", 1)] [TestCase("", 1)]
        [TestCase("lanternfin", 1)] [TestCase("driftwing", 1)] [TestCase("pondlet", 0)]
        [TestCase("pondlet", -1)] [TestCase("pondlet", 10)] [TestCase("pondlet", int.MaxValue)]
        public void BadOrLockedOrderCannotAlterQueue(string id, int count)
        {
            Assert.That(lab.AddOrder(game, id, count), Is.False);
            Assert.That(lab.Queue, Is.Empty);
            Assert.That(lab.AddOrder(null, "pondlet"), Is.False);
        }

        [Test] public void UndiscoveredAndResearchRecipesRequireActualProgress()
        {
            Assert.That(lab.AddOrder(new GameModel(GameConfig.CreateDefault()), "pondlet"), Is.False);
            game = KnownGame(true);
            Assert.That(lab.AddOrder(game, "lanternfin"), Is.True);
            Assert.That(lab.AddOrder(game, "driftwing"), Is.True);
            Assert.That(lab.Queue.Length, Is.EqualTo(2));
        }

        [Test] public void PriorityMovesAreAdjacentAndSnapshotsCannotMutateOrders()
        {
            lab.AddOrder(game, "pondlet", 2); lab.AddOrder(game, "bubblebelly", 3); lab.AddOrder(game, "ribbonfin");
            Assert.That(lab.MovePriority("pondlet", -1), Is.False);
            Assert.That(lab.MovePriority("ribbonfin", 1), Is.False);
            Assert.That(lab.MovePriority("missing", -1), Is.False);
            Assert.That(lab.MovePriority("pondlet", 0), Is.False);
            Assert.That(lab.MovePriority("pondlet", 2), Is.False);
            Assert.That(lab.MovePriority("ribbonfin", -1), Is.True);
            Assert.That(lab.Queue[1].speciesId, Is.EqualTo("ribbonfin"));
            Assert.That(lab.MovePriority("pondlet", 1), Is.True);
            var snapshot = lab.Queue; snapshot[0].count = 8; snapshot[1] = null;
            Assert.That(lab.Queue[0].count, Is.EqualTo(1));
            Assert.That(lab.Queue[1].speciesId, Is.EqualTo("pondlet"));
            Assert.That(lab.RemoveOrder("pondlet"), Is.True);
            Assert.That(lab.RemoveOrder("pondlet"), Is.False);
            Assert.That(lab.Queue.Length, Is.EqualTo(2));
        }

        [Test] public void MarkStartedConsumesOnlyHeadOnceAndDoesNotStartOrMoveMachine()
        {
            lab.AddOrder(game, "pondlet", 2); lab.AddOrder(game, "bubblebelly");
            Assert.That(lab.MarkStarted("pondlet", "test-ticket"), Is.False, "Unhired cannot own an active batch.");
            lab.Employee.Hire(cycle);
            Assert.That(lab.MarkStarted("bubblebelly", "test-ticket"), Is.False, "No fallback past first recipe.");
            Assert.That(lab.MarkStarted("pondlet", " "), Is.False);
            Start(game, "pondlet"); string before = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(lab.MarkStarted("pondlet", game.Incubator.SpecimenId), Is.True);
            Assert.That(lab.MarkStarted("pondlet", game.Incubator.SpecimenId), Is.False);
            Assert.That(lab.Queue[0].count, Is.EqualTo(1));
            Assert.That(lab.ActiveSpecimenId, Is.EqualTo(game.Incubator.SpecimenId));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
            Assert.That(lab.RemoveOrder("pondlet"), Is.True);
            Assert.That(lab.ActiveSpecimenId, Is.EqualTo(game.Incubator.SpecimenId));
        }

        [TestCase(false)] [TestCase(true)]
        public void IncubatingAndReadyTicketsSurviveRestorationWithoutNewWorkOrCounts(bool ready)
        {
            BeginAssignedBatch(); if (ready) game.Tick(10);
            lab.Employee.SetPaused(true);
            var saved = lab.CaptureSave(game); string before = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(ShopLaboratory.TryRestore(saved, game, 1, out var restored), Is.True);
            Assert.That(restored.ActiveSpecimenId, Is.EqualTo(game.Incubator.SpecimenId));
            Assert.That(restored.ActiveSpeciesId, Is.EqualTo("pondlet"));
            Assert.That(restored.Employee.Paused, Is.True);
            Assert.That(restored.Queue[0].count, Is.EqualTo(1));
            saved.queue[0].count = 9; saved.employee.hired = false;
            Assert.That(restored.Queue[0].count, Is.EqualTo(1));
            Assert.That(restored.Employee.Hired, Is.True);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
            Assert.That(restored.Reconcile(game), Is.False);
        }

        [Test] public void ManualCollectionRetiresTicketPurelyInSnapshotAndExplicitlyInReconcile()
        {
            BeginAssignedBatch(); string ticket = lab.ActiveSpecimenId; game.Tick(10);
            Assert.That(game.TransferToTank("tank-a").Success, Is.True);
            string before = JsonUtility.ToJson(game.CaptureSave());
            var snapshot = lab.CaptureSave(game);
            Assert.That(snapshot.activeSpecimenId, Is.Null); Assert.That(snapshot.activeSpeciesId, Is.Null);
            Assert.That(lab.ActiveSpecimenId, Is.EqualTo(ticket), "Capturing never mutates the live aggregate.");
            Assert.That(ShopLaboratory.TryRestore(snapshot, game, 1, out var restored), Is.True);
            Assert.That(restored.ActiveSpecimenId, Is.Null); Assert.That(restored.Queue[0].count, Is.EqualTo(1));
            Assert.That(lab.Reconcile(null), Is.False);
            Assert.That(lab.Reconcile(game), Is.True); Assert.That(lab.Reconcile(game), Is.False);
            Assert.That(lab.Queue[0].count, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
        }

        [Test] public void AReplacementOfSameSpeciesCannotInheritOldBatchTicket()
        {
            BeginAssignedBatch(); string ticket = lab.ActiveSpecimenId; game.Tick(10); game.TransferToTank("tank-a");
            Start(game, "pondlet"); Assert.That(game.Incubator.SpecimenId, Is.Not.EqualTo(ticket));
            Assert.That(lab.CaptureSave(game).activeSpecimenId, Is.Null);
            Assert.That(lab.Reconcile(game), Is.True);
        }

        [Test] public void ClearingAssignmentsLeavesMachineAndManualRecoveryIntact()
        {
            BeginAssignedBatch(); string before = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(lab.ClearAssignments(), Is.True); Assert.That(lab.ClearAssignments(), Is.False);
            Assert.That(lab.Queue, Is.Empty); Assert.That(lab.ActiveSpecimenId, Is.Null);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
            game.Tick(10); Assert.That(game.TransferToTank("tank-a").Success, Is.True);
        }

        [TestCase("null")] [TestCase("version")] [TestCase("employee-null")] [TestCase("role")]
        [TestCase("queue-null")] [TestCase("entry-null")] [TestCase("duplicate")]
        [TestCase("species-null")] [TestCase("species-unknown")] [TestCase("species-locked")]
        [TestCase("zero-count")] [TestCase("high-count")] [TestCase("active-one-field")]
        [TestCase("active-unknown")] [TestCase("active-wrong-id")] [TestCase("active-unhired")]
        [TestCase("future-wage")] [TestCase("game-null")]
        public void InvalidRestoreRejectsWholeAggregateWithoutMutatingModel(string problem)
        {
            BeginAssignedBatch(); var save = lab.CaptureSave(game); var source = game;
            string before = JsonUtility.ToJson(game.CaptureSave());
            switch (problem)
            {
                case "null": save = null; break;
                case "version": save.version = 0; break;
                case "employee-null": save.employee = null; break;
                case "role": save.employee.employeeId = ShopStaff.StockerId; break;
                case "queue-null": save.queue = null; break;
                case "entry-null": save.queue[0] = null; break;
                case "duplicate": save.queue = new[] { save.queue[0], save.queue[0] }; break;
                case "species-null": save.queue[0].speciesId = null; break;
                case "species-unknown": save.queue[0].speciesId = "missing"; break;
                case "species-locked": save.queue[0].speciesId = "lanternfin"; break;
                case "zero-count": save.queue[0].count = 0; break;
                case "high-count": save.queue[0].count = 10; break;
                case "active-one-field": save.activeSpeciesId = null; break;
                case "active-unknown": save.activeSpeciesId = "missing"; break;
                case "active-wrong-id": save.activeSpecimenId = "other-fish"; break;
                case "active-unhired": save.employee.hired = false; break;
                case "future-wage": save.employee.paidDay = 2; save.employee.paidWage = 4; break;
                case "game-null": source = null; break;
            }
            Assert.That(ShopLaboratory.TryRestore(save, source, 1, out var restored), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
        }

        void BeginAssignedBatch()
        {
            lab.Employee.Hire(cycle); lab.AddOrder(game, "pondlet", 2);
            Start(game, "pondlet"); Assert.That(lab.MarkStarted("pondlet", game.Incubator.SpecimenId), Is.True);
        }
        static void Start(GameModel model, string speciesId)
        {
            model.Tick(100); var recipe = model.GetSpecies(speciesId);
            for (int i = 0; i < recipe.Bloom; i++) Assert.That(model.Collect(CultureKind.Bloom).Success, Is.True);
            for (int i = 0; i < recipe.Flow; i++) Assert.That(model.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(model.UseKnownRecipe(speciesId).Success, Is.True);
            Assert.That(model.BeginIncubation().Success, Is.True);
        }
        static GameModel KnownGame(bool research = false)
        {
            var config = GameConfig.CreateDefault(); var saved = new GameModel(config).CaptureSave();
            saved.currency = 100;
            saved.discoveries = research ? new[] { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" } :
                new[] { "pondlet", "bubblebelly", "ribbonfin" };
            saved.sparkResearchUnlocked = research; saved.mistResearchUnlocked = research;
            Assert.That(GameModel.TryRestore(config, saved, out var restored, out _), Is.True); return restored;
        }
    }
}
