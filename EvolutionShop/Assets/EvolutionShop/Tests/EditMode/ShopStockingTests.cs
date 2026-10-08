using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class ShopStockingTests
    {
        GameModel game;
        ShopDayCycle cycle;
        ShopStocking stocking;

        [SetUp] public void Setup()
        {
            game = WithCredits(20); cycle = new ShopDayCycle(); stocking = new ShopStocking();
        }

        [Test] public void EmptyRosterAndEveryDisplayStartOffWithoutStockMutation()
        {
            string before = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(stocking.Employee.EmployeeId, Is.EqualTo(ShopStaff.StockerId));
            Assert.That(stocking.Employee.Wage, Is.EqualTo(4));
            Assert.That(stocking.Employee.Hired || stocking.Employee.Scheduled || stocking.Employee.Paused, Is.False);
            foreach (string tank in game.TankIds) Assert.That(stocking.GetTarget(tank), Is.Null);
            Assert.That(stocking.GetTarget(null), Is.Null);
            Assert.That(stocking.GetTarget("missing"), Is.Null);
            Assert.That(stocking.CaptureSave(game).targets, Is.Empty);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
        }

        [Test] public void RolesPayTheirOwnReceiptsOnceFromTheSameWallet()
        {
            var cashier = new ShopStaff(); var stocker = stocking.Employee;
            Schedule(cashier); Schedule(stocker);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(cashier.TryBeginDuty(game, cycle), Is.True);
                Assert.That(stocker.TryBeginDuty(game, cycle), Is.True);
            }
            Assert.That(game.Currency, Is.EqualTo(12));
            Assert.That(cashier.CaptureSave().employeeId, Is.EqualTo(ShopStaff.CashierId));
            Assert.That(stocker.CaptureSave().employeeId, Is.EqualTo(ShopStaff.StockerId));
            Assert.That(cashier.WagesForDay(1) + stocker.WagesForDay(1), Is.EqualTo(8));
            Assert.That(stocker.Dismiss(cycle), Is.True); Schedule(stocker);
            Assert.That(stocker.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(12));
            cycle.OpenShop(); cycle.Tick(483);
            Assert.That(cashier.TryBeginDuty(game, cycle), Is.True);
            Assert.That(stocker.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.Currency, Is.EqualTo(4));
            Assert.That(cashier.WagesForDay(1) + stocker.WagesForDay(1), Is.EqualTo(8));
        }

        [TestCase(true)] [TestCase(false)]
        public void InsufficientFundsNeverSpendOtherRolesReceiptOrCreateDebt(bool cashierFirst)
        {
            game = WithCredits(4); var cashier = new ShopStaff(); var stocker = stocking.Employee;
            Schedule(cashier); Schedule(stocker);
            var first = cashierFirst ? cashier : stocker; var second = cashierFirst ? stocker : cashier;
            Assert.That(first.TryBeginDuty(game, cycle), Is.True);
            Assert.That(second.TryBeginDuty(game, cycle), Is.False);
            Assert.That(first.IsOnDuty(1), Is.True); Assert.That(second.IsOnDuty(1), Is.False);
            Assert.That(second.PaidDay, Is.Zero); Assert.That(second.Scheduled, Is.True);
            Assert.That(game.Currency, Is.Zero);
            Assert.That(cycle.OpenShop(), Is.True);
        }

        [Test] public void StockerWageDoesNotDisturbManualCustomerReservationOrInventory()
        {
            Produce(game); Assert.That(game.TransferToTank("tank-a").Success, Is.True);
            Assert.That(game.ReserveTank("tank-a", "customer-1").Success, Is.True);
            var expected = game.CaptureSave(); expected.currency -= ShopStaff.StockerWage;
            Schedule(stocking.Employee);
            Assert.That(stocking.Employee.TryBeginDuty(game, cycle), Is.True);
            Assert.That(game.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("customer-1"));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(expected)));
        }

        [Test] public void CashierAndStockerReceiptsCannotBeRestoredAsTheOtherRole()
        {
            var stockerSave = stocking.Employee.CaptureSave();
            Assert.That(ShopStaff.TryRestore(stockerSave, 1, out _), Is.False);
            Assert.That(ShopStaff.TryRestore(new ShopStaff().CaptureSave(), 1, ShopStaff.StockerId, out _), Is.False);
            Assert.That(ShopStaff.TryRestore(stockerSave, 1, ShopStaff.StockerId, out var restored), Is.True);
            Assert.That(restored.EmployeeId, Is.EqualTo(ShopStaff.StockerId));
            Assert.That(ShopStaff.TryRestore(stockerSave, 1, null, out _), Is.False);
            Assert.That(ShopStaff.TryRestore(stockerSave, 1, "future-role", out _), Is.False);
            Assert.Throws<ArgumentException>(() => new ShopStaff("future-role"));
        }

        [Test] public void ExplicitTargetsRequireCurrentDisplayAndKnownSpeciesAndCanBeCleared()
        {
            Assert.That(stocking.SetTarget(game, "tank-a", "pondlet"), Is.False, "No unseen species.");
            Assert.That(stocking.SetTarget(game, "tank-a", ShopStocking.AnySpecies), Is.True);
            Assert.That(stocking.SetTarget(game, "tank-a", ShopStocking.AnySpecies), Is.False);
            Assert.That(stocking.SetTarget(game, "tank-a", "missing"), Is.False);
            Assert.That(stocking.SetTarget(game, "tank-a", ""), Is.False);
            Assert.That(stocking.SetTarget(game, "missing", ShopStocking.AnySpecies), Is.False);
            Assert.That(stocking.SetTarget(game, null, ShopStocking.AnySpecies), Is.False);
            Assert.That(stocking.SetTarget(null, "tank-a", ShopStocking.AnySpecies), Is.False);
            Produce(game);
            Assert.That(stocking.SetTarget(game, "tank-a", "pondlet"), Is.True);
            Assert.That(stocking.GetTarget("tank-a"), Is.EqualTo("pondlet"));
            Assert.That(stocking.SetTarget(game, "tank-a", null), Is.True);
            Assert.That(stocking.SetTarget(game, "tank-a", null), Is.False);
            Assert.That(stocking.GetTarget("tank-a"), Is.Null);
        }

        [Test] public void ConfigurationDoesNotGrantSpeciesHireEmployeeMoveFishOrChangeCustomerClaim()
        {
            Produce(game); game.TransferToTank("tank-a"); game.ReserveTank("tank-a", "existing-customer");
            string before = JsonUtility.ToJson(game.CaptureSave());
            Assert.That(stocking.SetTarget(game, "tank-a", "pondlet"), Is.True);
            Assert.That(stocking.SetTarget(game, "tank-b", ShopStocking.AnySpecies), Is.True);
            Assert.That(stocking.Employee.Hired, Is.False);
            Assert.That(game.GetReservation("tank-a").Value.CustomerId, Is.EqualTo("existing-customer"));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
        }

        [Test] public void SaveTargetsSortByStableIdAndReturnedDataDoesNotMutateAggregate()
        {
            Produce(game);
            stocking.SetTarget(game, "tank-b", ShopStocking.AnySpecies);
            stocking.SetTarget(game, "tank-a", "pondlet");
            Schedule(stocking.Employee); stocking.Employee.TryBeginDuty(game, cycle); stocking.Employee.SetPaused(true);
            var save = stocking.CaptureSave(game);
            Assert.That(save.targets[0].tankId, Is.EqualTo("tank-a"));
            Assert.That(save.targets[1].tankId, Is.EqualTo("tank-b"));
            Assert.That(ShopStocking.TryRestore(save, game, 1, out var restored), Is.True);
            string canonical = JsonUtility.ToJson(restored.CaptureSave(game));
            save.targets[0].speciesId = ShopStocking.AnySpecies; save.employee.paidDay = 0;
            stocking.SetTarget(game, "tank-b", null);
            Assert.That(restored.GetTarget("tank-a"), Is.EqualTo("pondlet"));
            Assert.That(restored.Employee.PaidDay, Is.EqualTo(1));
            Assert.That(restored.Employee.Paused, Is.True);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave(game)), Is.EqualTo(canonical));
        }

        [Test] public void SnapshotOmitsRemovedDisplaysWithoutChangingTheGameOrOriginalSettings()
        {
            var expandedConfig = GameConfig.CreateDefault(); expandedConfig.TankIds = new[] { "tank-a", "tank-b", "owned-display" };
            var expanded = new GameModel(expandedConfig);
            stocking.SetTarget(expanded, "tank-a", ShopStocking.AnySpecies);
            stocking.SetTarget(expanded, "owned-display", ShopStocking.AnySpecies);
            string before = JsonUtility.ToJson(game.CaptureSave());
            var saved = stocking.CaptureSave(game);
            Assert.That(saved.targets.Length, Is.EqualTo(1));
            Assert.That(saved.targets[0].tankId, Is.EqualTo("tank-a"));
            Assert.That(stocking.GetTarget("owned-display"), Is.EqualTo(ShopStocking.AnySpecies));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
            Assert.That(ShopStocking.TryRestore(saved, game, 1, out var restored), Is.True);
            Assert.That(restored.GetTarget("owned-display"), Is.Null);
        }

        [Test] public void PruningRetiredDisplayKeepsRedeployedFurnitureOffUntilExplicitlyConfigured()
        {
            var expandedConfig = GameConfig.CreateDefault(); expandedConfig.TankIds = new[] { "tank-a", "tank-b", "owned-display" };
            var expanded = new GameModel(expandedConfig);
            stocking.SetTarget(expanded, "tank-a", ShopStocking.AnySpecies);
            stocking.SetTarget(expanded, "owned-display", ShopStocking.AnySpecies);
            string beforeGame = JsonUtility.ToJson(game.CaptureSave());
            string beforeEmployee = JsonUtility.ToJson(stocking.Employee.CaptureSave());
            Assert.That(stocking.PruneTargets(null), Is.False);
            Assert.That(stocking.PruneTargets(game), Is.True);
            Assert.That(stocking.PruneTargets(game), Is.False);
            Assert.That(stocking.GetTarget("tank-a"), Is.EqualTo(ShopStocking.AnySpecies));
            Assert.That(stocking.GetTarget("owned-display"), Is.Null);
            Assert.That(stocking.CaptureSave(expanded).targets.Length, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(beforeGame));
            Assert.That(JsonUtility.ToJson(stocking.Employee.CaptureSave()), Is.EqualTo(beforeEmployee));
            Assert.That(stocking.SetTarget(expanded, "owned-display", ShopStocking.AnySpecies), Is.True);
        }

        [TestCase("null")] [TestCase("version")] [TestCase("employee-null")] [TestCase("cashier")]
        [TestCase("targets-null")] [TestCase("null-target")] [TestCase("unknown-tank")] [TestCase("null-tank")]
        [TestCase("unknown-species")] [TestCase("undiscovered")] [TestCase("null-species")]
        [TestCase("empty-species")] [TestCase("duplicate")] [TestCase("future-wage")] [TestCase("game-null")]
        public void InvalidConfigurationRejectsWholeAggregateWithoutInventoryMutation(string problem)
        {
            stocking.SetTarget(game, "tank-a", ShopStocking.AnySpecies);
            var saved = stocking.CaptureSave(game); var sourceGame = game;
            string before = JsonUtility.ToJson(game.CaptureSave());
            switch (problem)
            {
                case "null": saved = null; break;
                case "version": saved.version = 0; break;
                case "employee-null": saved.employee = null; break;
                case "cashier": saved.employee = new ShopStaff().CaptureSave(); break;
                case "targets-null": saved.targets = null; break;
                case "null-target": saved.targets[0] = null; break;
                case "unknown-tank": saved.targets[0].tankId = "missing"; break;
                case "null-tank": saved.targets[0].tankId = null; break;
                case "unknown-species": saved.targets[0].speciesId = "missing"; break;
                case "undiscovered": saved.targets[0].speciesId = "pondlet"; break;
                case "null-species": saved.targets[0].speciesId = null; break;
                case "empty-species": saved.targets[0].speciesId = ""; break;
                case "duplicate": saved.targets = new[] { saved.targets[0], saved.targets[0] }; break;
                case "future-wage": saved.employee.paidDay = 2; saved.employee.paidWage = 4; break;
                case "game-null": sourceGame = null; break;
            }
            Assert.That(ShopStocking.TryRestore(saved, sourceGame, 1, out var restored), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(before));
        }

        void Schedule(ShopStaff employee)
        { Assert.That(employee.Hire(cycle), Is.True); Assert.That(employee.SetScheduled(true, cycle), Is.True); }
        static void Produce(GameModel model)
        {
            Assert.That(model.Collect(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Bloom).Success, Is.True);
            Assert.That(model.Dose(CultureKind.Flow).Success, Is.True);
            Assert.That(model.BeginIncubation().Success, Is.True); model.Tick(10);
            Assert.That(model.IsDiscovered("pondlet"), Is.True);
        }
        static GameModel WithCredits(int credits)
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.currency = credits;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
    }
}
