using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class TechnicianSaveTests
    {
        const string Prefix = "EvolutionShop-TechnicianSaveTests-";
        string directory;
        GameConfig config;
        string PathFor() => Path.Combine(directory, "slot.json");

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); config = GameConfig.CreateDefault();
        }
        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(directory, true);
        }

        [TestCase(ShopDayPhase.Night, false)] [TestCase(ShopDayPhase.Open, false)]
        [TestCase(ShopDayPhase.Closing, true)] [TestCase(ShopDayPhase.Night, true)]
        public void CurrentRoundTripKeepsThreeRoleWagesPriorityAndExactActiveBatch(ShopDayPhase phase, bool ready)
        {
            var slot = ActiveSlot(out var game, out var cycle, out var lab);
            if (ready) game.Tick(10);
            if (phase != ShopDayPhase.Night) cycle.OpenShop();
            if (phase == ShopDayPhase.Closing) cycle.RequestClose();
            lab.Employee.SetPaused(true);
            slot.game = game.CaptureSave(); slot.cycle = cycle.CaptureSave(); slot.technician = lab.CaptureSave(game);
            slot.trades = ShopTradeLedger.LegacyForCycle(slot.cycle, 0).CaptureSave();
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            for (int i = 0; i < 3; i++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out error), Is.True, error);
                Assert.That(loaded.formatVersion, Is.EqualTo(19));
                Assert.That(ShopLaboratory.TryRestore(loaded.technician, restored, 1, out var restoredLab), Is.True);
                Assert.That(restoredLab.Employee.Paused, Is.True);
                Assert.That(restoredLab.Employee.WagesForDay(1), Is.EqualTo(4));
                Assert.That(restoredLab.ActiveSpecimenId, Is.EqualTo(game.Incubator.SpecimenId));
                Assert.That(restoredLab.Queue[0].speciesId, Is.EqualTo("pondlet"));
                Assert.That(restoredLab.Queue[0].count, Is.EqualTo(1));
                Assert.That(restoredLab.Queue[1].speciesId, Is.EqualTo("bubblebelly"));
                Assert.That(restored.Currency, Is.EqualTo(88));
                Assert.That(JsonUtility.ToJson(loaded.staff), Is.EqualTo(JsonUtility.ToJson(slot.staff)));
                Assert.That(JsonUtility.ToJson(loaded.stocker), Is.EqualTo(JsonUtility.ToJson(slot.stocker)));
                Assert.That(JsonUtility.ToJson(loaded.technician), Is.EqualTo(JsonUtility.ToJson(slot.technician)));
                Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(slot.game)));
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void FormatEighteenPreservesExactCashierStockerAndBytesWhileAddingOnlyEmptyTechnician(bool paid)
        {
            var slot = ActiveSlot(out var game, out var cycle, out _);
            slot.formatVersion = 18;
            if (!paid) { slot.staff = new ShopStaff().CaptureSave(); slot.stocker.employee = new ShopStaff(ShopStaff.StockerId).CaptureSave(); }
            string cashier = JsonUtility.ToJson(slot.staff), stocker = JsonUtility.ToJson(slot.stocker);
            string json = WithoutTechnician(JsonUtility.ToJson(slot), slot.technician);
            File.WriteAllText(PathFor(), json); byte[] old = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.formatVersion, Is.EqualTo(19));
            Assert.That(JsonUtility.ToJson(loaded.staff), Is.EqualTo(cashier));
            Assert.That(JsonUtility.ToJson(loaded.stocker), Is.EqualTo(stocker));
            Assert.That(loaded.technician.employee.hired || loaded.technician.employee.scheduled, Is.False);
            Assert.That(loaded.technician.employee.paidWage, Is.Zero);
            Assert.That(loaded.technician.queue, Is.Empty);
            Assert.That(loaded.technician.activeSpecimenId, Is.Null.Or.Empty);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(game.CaptureSave())));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(old));
            Assert.That(File.Exists(PathFor() + ".v18.bak"), Is.False);
            Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v18.bak"), Is.EqualTo(old));
            loaded.volume = .11f; Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v18.bak"), Is.EqualTo(old));
        }

        [TestCase("cashier")] [TestCase("stocker")]
        public void FormatEighteenCannotHideMissingExistingRoleBehindMigration(string missing)
        {
            var slot = ActiveSlot(out _, out _, out _); slot.formatVersion = 18;
            string json = WithoutTechnician(JsonUtility.ToJson(slot), slot.technician);
            File.WriteAllText(PathFor(), json);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out var error), Is.True, error);
            json = missing == "cashier" ? json.Replace("\"staff\":" + JsonUtility.ToJson(slot.staff) + ",", "") :
                json.Replace("\"stocker\":" + JsonUtility.ToJson(slot.stocker) + ",", "");
            File.WriteAllText(PathFor(), json);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out _), Is.False);
            Assert.That(File.ReadAllText(PathFor()), Is.EqualTo(json));
        }

        [Test] public void UnpaidQueuedTechnicianReloadDoesNotChargeOrStartWork()
        {
            var slot = FreshSlot(); var game = KnownGame(); var cycle = new ShopDayCycle(); var lab = new ShopLaboratory();
            lab.Employee.Hire(cycle); lab.Employee.SetScheduled(true, cycle); lab.AddOrder(game, "pondlet", 3);
            slot.game = game.CaptureSave(); slot.technician = lab.CaptureSave(game);
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out game, out var loaded, out error), Is.True, error);
            Assert.That(ShopLaboratory.TryRestore(loaded.technician, game, 1, out lab), Is.True);
            Assert.That(game.Currency, Is.EqualTo(100)); Assert.That(lab.Employee.IsOnDuty(1), Is.False);
            Assert.That(lab.Queue[0].count, Is.EqualTo(3)); Assert.That(game.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing));
            Assert.That(lab.Employee.TryBeginDuty(game, cycle), Is.True); Assert.That(game.Currency, Is.EqualTo(96));
        }

        [Test] public void SaveAfterManualPickupRetiresTicketWithoutDuplicatingConsumedOrder()
        {
            var slot = ActiveSlot(out var game, out _, out var lab); string oldTicket = lab.ActiveSpecimenId;
            game.Tick(10); Assert.That(game.TransferToTank("tank-a").Success, Is.True);
            slot.game = game.CaptureSave(); slot.technician = lab.CaptureSave(game);
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out game, out var loaded, out error), Is.True, error);
            Assert.That(loaded.technician.activeSpecimenId, Is.Null.Or.Empty);
            Assert.That(loaded.technician.queue[0].count, Is.EqualTo(1));
            Assert.That(game.GetTank("tank-a").Value.Id, Is.EqualTo(oldTicket));
            Assert.That(game.Incubator.Phase, Is.EqualTo(IncubatorPhase.Dosing));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("empty")] [TestCase("nested-only")]
        [TestCase("duplicate-root")] [TestCase("version-missing")] [TestCase("version-string")] [TestCase("version-zero")]
        [TestCase("employee-missing")] [TestCase("employee-null")] [TestCase("employee-empty")] [TestCase("employee-role")]
        [TestCase("employee-missing-bool")] [TestCase("employee-bool-null")] [TestCase("employee-bool-number")]
        [TestCase("employee-missing-day")] [TestCase("employee-day-string")] [TestCase("employee-day-fraction")]
        [TestCase("employee-duplicate")] [TestCase("employee-future-wage")] [TestCase("employee-unhired")]
        [TestCase("queue-missing")] [TestCase("queue-null")] [TestCase("queue-object")] [TestCase("queue-string")]
        [TestCase("queue-duplicate-key")] [TestCase("entry-null")] [TestCase("entry-empty")] [TestCase("entry-duplicate")]
        [TestCase("species-missing")] [TestCase("species-null")] [TestCase("species-number")] [TestCase("species-unknown")]
        [TestCase("species-undiscovered")] [TestCase("count-missing")] [TestCase("count-null")] [TestCase("count-string")]
        [TestCase("count-fraction")] [TestCase("count-zero")] [TestCase("count-overflow")] [TestCase("entry-duplicate-field")]
        [TestCase("active-species-missing")] [TestCase("active-id-missing")] [TestCase("active-species-null")]
        [TestCase("active-id-number")] [TestCase("active-id-wrong")] [TestCase("active-species-wrong")]
        public void MalformedCurrentTechnicianFailsWholeReadAndPreservesOriginal(string problem)
        {
            var slot = ActiveSlot(out _, out _, out _);
            string valid = JsonUtility.ToJson(slot), part = JsonUtility.ToJson(slot.technician);
            File.WriteAllText(PathFor(), valid);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out _, out var controlError), Is.True, controlError);
            string changed = part, invalid = valid;
            string employee = JsonUtility.ToJson(slot.technician.employee);
            string first = JsonUtility.ToJson(slot.technician.queue[0]);
            string queue = "[" + first + "," + JsonUtility.ToJson(slot.technician.queue[1]) + "]";
            string activeSpecies = "\"activeSpeciesId\":\"pondlet\"";
            string activeId = "\"activeSpecimenId\":\"" + slot.technician.activeSpecimenId + "\"";
            switch (problem)
            {
                case "missing": invalid = WithoutTechnician(valid, slot.technician); break;
                case "null": changed = "null"; break;
                case "empty": changed = "{}"; break;
                case "nested-only": invalid = WithoutTechnician(valid, slot.technician).TrimEnd('}') + ",\"ignored\":{\"technician\":" + part + "}}"; break;
                case "duplicate-root": invalid = valid.Replace("\"technician\":" + part, "\"technician\":" + part + ",\"technician\":" + part); break;
                case "version-missing": changed = changed.Remove(1, "\"version\":1,".Length); break;
                case "version-string": changed = changed.Remove(1, "\"version\":1".Length).Insert(1, "\"version\":\"1\""); break;
                case "version-zero": changed = changed.Remove(1, "\"version\":1".Length).Insert(1, "\"version\":0"); break;
                case "employee-missing": changed = changed.Replace("\"employee\":" + employee + ",", ""); break;
                case "employee-null": changed = changed.Replace(employee, "null"); break;
                case "employee-empty": changed = changed.Replace(employee, "{}"); break;
                case "employee-role": changed = changed.Replace(ShopStaff.TechnicianId, ShopStaff.StockerId); break;
                case "employee-missing-bool": changed = changed.Replace("\"hired\":true,", ""); break;
                case "employee-bool-null": changed = changed.Replace("\"hired\":true", "\"hired\":null"); break;
                case "employee-bool-number": changed = changed.Replace("\"hired\":true", "\"hired\":1"); break;
                case "employee-missing-day": changed = changed.Replace("\"paidDay\":1,", ""); break;
                case "employee-day-string": changed = changed.Replace("\"paidDay\":1", "\"paidDay\":\"1\""); break;
                case "employee-day-fraction": changed = changed.Replace("\"paidDay\":1", "\"paidDay\":1.1"); break;
                case "employee-duplicate": changed = changed.Replace("\"paused\":false", "\"paused\":false,\"paused\":true"); break;
                case "employee-future-wage": changed = changed.Replace("\"paidDay\":1", "\"paidDay\":2"); break;
                case "employee-unhired": changed = changed.Replace("\"hired\":true", "\"hired\":false").Replace("\"scheduled\":true", "\"scheduled\":false"); break;
                case "queue-missing": changed = changed.Replace("\"queue\":" + queue + ",", ""); break;
                case "queue-null": changed = changed.Replace(queue, "null"); break;
                case "queue-object": changed = changed.Replace(queue, "{}"); break;
                case "queue-string": changed = changed.Replace(queue, "\"[]\""); break;
                case "queue-duplicate-key": changed = changed.Replace("\"queue\":", "\"queue\":[],\"queue\":"); break;
                case "entry-null": changed = changed.Replace(first, "null"); break;
                case "entry-empty": changed = changed.Replace(first, "{}"); break;
                case "entry-duplicate": changed = changed.Replace(first, first + "," + first); break;
                case "species-missing": changed = changed.Replace(first, "{\"count\":1}"); break;
                case "species-null": changed = changed.Replace(first, "{\"speciesId\":null,\"count\":1}"); break;
                case "species-number": changed = changed.Replace(first, "{\"speciesId\":1,\"count\":1}"); break;
                case "species-unknown": changed = changed.Replace(first, "{\"speciesId\":\"unknown\",\"count\":1}"); break;
                case "species-undiscovered": changed = changed.Replace(first, "{\"speciesId\":\"lanternfin\",\"count\":1}"); break;
                case "count-missing": changed = changed.Replace(first, "{\"speciesId\":\"pondlet\"}"); break;
                case "count-null": changed = changed.Replace(first, first.Replace("\"count\":1", "\"count\":null")); break;
                case "count-string": changed = changed.Replace(first, first.Replace("\"count\":1", "\"count\":\"1\"")); break;
                case "count-fraction": changed = changed.Replace(first, first.Replace("\"count\":1", "\"count\":1.5")); break;
                case "count-zero": changed = changed.Replace(first, first.Replace("\"count\":1", "\"count\":0")); break;
                case "count-overflow": changed = changed.Replace(first, first.Replace("\"count\":1", "\"count\":10")); break;
                case "entry-duplicate-field": changed = changed.Replace(first, first.Replace("\"count\":1", "\"count\":1,\"count\":2")); break;
                case "active-species-missing": changed = changed.Replace(activeSpecies + ",", ""); break;
                case "active-id-missing": changed = changed.Replace("," + activeId, ""); break;
                case "active-species-null": changed = changed.Replace(activeSpecies, "\"activeSpeciesId\":null"); break;
                case "active-id-number": changed = changed.Replace(activeId, "\"activeSpecimenId\":1"); break;
                case "active-id-wrong": changed = changed.Replace(activeId, "\"activeSpecimenId\":\"other-fish\""); break;
                case "active-species-wrong": changed = changed.Replace(activeSpecies, "\"activeSpeciesId\":\"bubblebelly\""); break;
            }
            if (changed != part) invalid = valid.Replace("\"technician\":" + part, "\"technician\":" + changed);
            Assert.That(invalid, Is.Not.EqualTo(valid), "Mutation must change its positive control.");
            File.WriteAllText(PathFor(), invalid); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var game, out var loaded, out var error), Is.False);
            Assert.That(game, Is.Null); Assert.That(loaded, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase("null")] [TestCase("receipt")] [TestCase("order")] [TestCase("role")] [TestCase("ticket")]
        public void InvalidWritePreservesGoodSlotExactly(string problem)
        {
            var slot = ActiveSlot(out _, out _, out _);
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error); byte[] original = File.ReadAllBytes(PathFor());
            if (problem == "null") slot.technician = null;
            else if (problem == "receipt") slot.technician.employee.paidDay = 2;
            else if (problem == "order") slot.technician.queue[0].count = 10;
            else if (problem == "role") slot.technician.employee.employeeId = ShopStaff.CashierId;
            else slot.technician.activeSpecimenId = "different-fish";
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
        }

        SlotData ActiveSlot(out GameModel game, out ShopDayCycle cycle, out ShopLaboratory lab)
        {
            game = KnownGame(); cycle = new ShopDayCycle(); lab = new ShopLaboratory();
            var cashier = new ShopStaff(); var stocking = new ShopStocking();
            foreach (var employee in new[] { cashier, stocking.Employee, lab.Employee })
            { employee.Hire(cycle); employee.SetScheduled(true, cycle); Assert.That(employee.TryBeginDuty(game, cycle), Is.True); }
            stocking.SetTarget(game, "tank-a", "pondlet"); stocking.SetTarget(game, "tank-b", ShopStocking.AnySpecies);
            lab.AddOrder(game, "pondlet", 2); lab.AddOrder(game, "bubblebelly", 3);
            Assert.That(game.Collect(CultureKind.Bloom).Success, Is.True); Assert.That(game.Collect(CultureKind.Flow).Success, Is.True);
            Assert.That(game.UseKnownRecipe("pondlet").Success, Is.True); Assert.That(game.BeginIncubation().Success, Is.True);
            Assert.That(lab.MarkStarted("pondlet", game.Incubator.SpecimenId), Is.True);
            return new SlotData { game = game.CaptureSave(), cycle = cycle.CaptureSave(), staff = cashier.CaptureSave(),
                stocker = stocking.CaptureSave(game), technician = lab.CaptureSave(game) };
        }
        GameModel KnownGame()
        {
            var save = new GameModel(config).CaptureSave(); save.currency = 100;
            save.discoveries = new[] { "pondlet", "bubblebelly", "ribbonfin" };
            Assert.That(GameModel.TryRestore(config, save, out var game, out _), Is.True); return game;
        }
        SlotData FreshSlot() => new SlotData { game = new GameModel(config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
        static string WithoutTechnician(string json, TechnicianSave technician) => json.Replace("\"technician\":" + JsonUtility.ToJson(technician) + ",", "");
    }
}
