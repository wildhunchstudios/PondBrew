using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class TutorialSaveTests
    {
        const string Prefix = "EvolutionShop-TutorialSaveTests-";
        string directory;
        string PathFor() => Path.Combine(directory, "slot.json");
        static GameConfig Config => GameConfig.CreateDefault();
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(directory).StartsWith(Prefix, StringComparison.Ordinal), Is.True);
            Directory.Delete(directory, true);
        }

        [TestCase(TutorialMode.Offered)] [TestCase(TutorialMode.Active)] [TestCase(TutorialMode.Skipped)]
        [TestCase(TutorialMode.Completed)] [TestCase(TutorialMode.LegacyUnguided)]
        public void EveryModeRoundTripsInSameSlotWithoutWritingOrRewardingOnRead(TutorialMode mode)
        {
            var slot = SlotFor(mode); string expected = JsonUtility.ToJson(slot.tutorial);
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] bytes = File.ReadAllBytes(PathFor());
            for (int i = 0; i < 3; i++)
            {
                Assert.That(SaveStore.Read(PathFor(), Config, out var game, out var loaded, out error), Is.True, error);
                Assert.That(loaded.formatVersion, Is.EqualTo(23)); Assert.That(loaded.game.version, Is.EqualTo(6));
                Assert.That(JsonUtility.ToJson(loaded.tutorial), Is.EqualTo(expected));
                Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(slot.game)));
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
            }
        }

        [TestCase("orient")] [TestCase("collect")] [TestCase("prepare")] [TestCase("grow")]
        [TestCase("ready")] [TestCase("display")] [TestCase("open")] [TestCase("first-sale")]
        [TestCase("practice")] [TestCase("practice-sale")] [TestCase("report")] [TestCase("complete")]
        public void LessonProgressRoundTripsTogetherWithExactStockMoneyAndDay(string point)
        {
            var slot = LessonSlot(point); string state = JsonUtility.ToJson(slot.tutorial);
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), Config, out var game, out var loaded, out error), Is.True, error);
            Assert.That(JsonUtility.ToJson(loaded.tutorial), Is.EqualTo(state));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(slot.game)));
            Assert.That(JsonUtility.ToJson(loaded.cycle), Is.EqualTo(JsonUtility.ToJson(slot.cycle)));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)]
        [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)]
        public void EveryLegacyEnvelopeMigratesUnguidedWithoutInventingFactsOrTouchingOriginal(int format)
        {
            string json = LegacyFreshJson(format); File.WriteAllText(PathFor(), json); byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), Config, out var game, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.tutorial.mode, Is.EqualTo(TutorialMode.LegacyUnguided));
            Assert.That(JsonUtility.ToJson(loaded.tutorial), Is.EqualTo(JsonUtility.ToJson(ShopTutorial.LegacyUnguided().CaptureSave())));
            Assert.That(game.Currency, Is.Zero); Assert.That(game.DiscoveredSpeciesIds, Is.Empty);
            Assert.That(loaded.cycle.dayNumber, Is.EqualTo(1)); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".v" + format + ".bak"), Is.False);
            Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v" + format + ".bak"), Is.EqualTo(original));
            loaded.volume = .17f; Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v" + format + ".bak"), Is.EqualTo(original));
        }

        [Test] public void PaidActiveTechnicianNineteenFixtureKeepsWagesBatchQueueAndArchiveBytes()
        {
            byte[] original = LegacyTechnician(); File.WriteAllBytes(PathFor(), original);
            var before = JsonUtility.FromJson<SlotData>(File.ReadAllText(PathFor()));
            Assert.That(before.formatVersion, Is.EqualTo(19)); Assert.That(before.technician.employee.paidWage, Is.EqualTo(4));
            Assert.That(before.technician.activeSpecimenId, Is.Not.Null.And.Not.Empty);
            Assert.That(SaveStore.Read(PathFor(), Config, out var game, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.tutorial.mode, Is.EqualTo(TutorialMode.LegacyUnguided));
            Assert.That(JsonUtility.ToJson(loaded.technician), Is.EqualTo(JsonUtility.ToJson(before.technician)));
            Assert.That(JsonUtility.ToJson(loaded.staff), Is.EqualTo(JsonUtility.ToJson(before.staff)));
            Assert.That(JsonUtility.ToJson(loaded.stocker), Is.EqualTo(JsonUtility.ToJson(before.stocker)));
            Assert.That(JsonUtility.ToJson(game.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(before.game)));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v19.bak"), Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(), Config, out var reloaded, out var current, out error), Is.True, error);
            Assert.That(reloaded.Currency, Is.EqualTo(game.Currency));
            Assert.That(JsonUtility.ToJson(current.technician), Is.EqualTo(JsonUtility.ToJson(loaded.technician)));
            current.volume = .27f; Assert.That(SaveStore.Write(PathFor(), current, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v19.bak"), Is.EqualTo(original));
        }

        [Test] public void FailedNineteenArchivePreservesOriginalPaidBatchAndNoTutorialCommit()
        {
            byte[] original = LegacyTechnician(); File.WriteAllBytes(PathFor(), original);
            Assert.That(SaveStore.Read(PathFor(), Config, out _, out var loaded, out var error), Is.True, error);
            Directory.CreateDirectory(PathFor() + ".v19.bak");
            Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        [Test] public void FailedFullSlotWriteNeverPublishesLearningAheadOfGameplay()
        {
            var before = LessonSlot("display"); Assert.That(SaveStore.Write(PathFor(), before, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor()); var candidate = LessonSlot("first-sale");
            Directory.CreateDirectory(PathFor() + ".tmp");
            Assert.That(SaveStore.Write(PathFor(), candidate, out error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(), Config, out var game, out var loaded, out error), Is.True, error);
            Assert.That(loaded.tutorial.first.saleDay, Is.Zero); Assert.That(game.Currency, Is.Zero);
            Assert.That(game.GetTank("tank-a").Value.Id, Is.EqualTo(before.tutorial.first.specimenId));
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("empty")] [TestCase("array")] [TestCase("bool")]
        [TestCase("nested-only")] [TestCase("duplicate-root")]
        public void MissingOrWrongTutorialObjectCannotInheritFreshInitializer(string problem)
        {
            var slot = SlotFor(TutorialMode.Active); string valid = JsonUtility.ToJson(slot), part = JsonUtility.ToJson(slot.tutorial);
            AssertReadable(valid);
            string invalid = valid;
            switch (problem)
            {
                case "missing": invalid = RemoveField(valid, "tutorial", part); break;
                case "null": invalid = valid.Replace("\"tutorial\":" + part, "\"tutorial\":null"); break;
                case "empty": invalid = valid.Replace("\"tutorial\":" + part, "\"tutorial\":{}"); break;
                case "array": invalid = valid.Replace("\"tutorial\":" + part, "\"tutorial\":[]"); break;
                case "bool": invalid = valid.Replace("\"tutorial\":" + part, "\"tutorial\":false"); break;
                case "nested-only": invalid = RemoveField(valid, "tutorial", part).TrimEnd('}') + ",\"ignored\":{\"tutorial\":" + part + "}}"; break;
                case "duplicate-root": invalid = valid.Replace("\"tutorial\":" + part, "\"tutorial\":" + part + ",\"tutorial\":" + part); break;
            }
            AssertRejectedUnchanged(invalid);
        }

        static IEnumerable<TestCaseData> RequiredFields()
        {
            foreach (string field in new[] { "version", "mode", "facts", "firstSaleAccommodationEnded", "practiceAssisted", "reportAcknowledged", "handoffCompleted", "reportDay", "practiceBlockedSpecimenId" })
                foreach (string problem in new[] { "missing", "wrong-type", "duplicate" }) yield return new TestCaseData("root", field, problem);
            foreach (string chain in new[] { "first", "practice" })
                foreach (string field in new[] { "specimenId", "speciesId", "ready", "manuallyDisplayed", "saleDay" })
                    foreach (string problem in new[] { "missing", "wrong-type", "duplicate" }) yield return new TestCaseData(chain, field, problem);
        }
        [TestCaseSource(nameof(RequiredFields))]
        public void RequiredScalarFieldsAreTypedExplicitAndUnique(string scope, string field, string problem)
        {
            var slot = SlotFor(TutorialMode.Active); string valid = JsonUtility.ToJson(slot), part = JsonUtility.ToJson(slot.tutorial);
            AssertReadable(valid);
            string selected = scope == "root" ? part : JsonUtility.ToJson(scope == "first" ? slot.tutorial.first : slot.tutorial.practice);
            var match = Regex.Match(selected, "\"" + field + "\":(\"(?:\\\\.|[^\"])*\"|true|false|null|-?[0-9]+)");
            Assert.That(match.Success, Is.True, field); string value = match.Groups[1].Value;
            string changed = selected;
            if (problem == "missing") changed = RemoveField(selected, field, value);
            if (problem == "wrong-type") changed = selected.Replace(match.Value, "\"" + field + "\":" + (value == "true" || value == "false" || value.StartsWith("\"") || value == "null" ? "123" : "\"123\""));
            if (problem == "duplicate") changed = selected.Replace(match.Value, match.Value + "," + match.Value);
            if (scope != "root") changed = part.Replace("\"" + scope + "\":" + selected, "\"" + scope + "\":" + changed);
            AssertRejectedUnchanged(valid.Replace("\"tutorial\":" + part, "\"tutorial\":" + changed));
        }

        [TestCase("first", "missing")] [TestCase("first", "null")] [TestCase("first", "empty")] [TestCase("first", "array")]
        [TestCase("practice", "missing")] [TestCase("practice", "null")] [TestCase("practice", "empty")] [TestCase("practice", "array")]
        public void BothBoundedChainObjectsAreRequired(string name, string problem)
        {
            var slot = SlotFor(TutorialMode.Active); string valid = JsonUtility.ToJson(slot), part = JsonUtility.ToJson(slot.tutorial);
            string chain = JsonUtility.ToJson(name == "first" ? slot.tutorial.first : slot.tutorial.practice); AssertReadable(valid);
            string changed = problem == "missing" ? RemoveField(part, name, chain) : part.Replace("\"" + name + "\":" + chain,
                "\"" + name + "\":" + (problem == "null" ? "null" : problem == "array" ? "[]" : "{}"));
            AssertRejectedUnchanged(valid.Replace("\"tutorial\":" + part, "\"tutorial\":" + changed));
        }

        [TestCase("version")] [TestCase("mode")] [TestCase("flags")] [TestCase("future-day")] [TestCase("ready-incubating")]
        [TestCase("display-incubator")] [TestCase("false-complete")] [TestCase("false-report")] [TestCase("absent-id")]
        [TestCase("wrong-species")] [TestCase("practice-before-first")] [TestCase("duplicate-chain")]
        public void ImpossibleCurrentSnapshotFailsBothReadAndWritePreservingPriorSlot(string problem)
        {
            var slot = LessonSlot(problem == "display-incubator" ? "ready" : "grow");
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error); byte[] original = File.ReadAllBytes(PathFor());
            switch (problem)
            {
                case "version": slot.tutorial.version = 2; break;
                case "mode": slot.tutorial.mode = (TutorialMode)99; break;
                case "flags": slot.tutorial.facts = (TutorialFacts)1024; break;
                case "future-day": slot.tutorial.first.saleDay = 2; break;
                case "ready-incubating": slot.tutorial.first.ready = true; slot.tutorial.facts |= TutorialFacts.CreatureReady; break;
                case "display-incubator": slot.tutorial.first.manuallyDisplayed = true; slot.tutorial.facts |= TutorialFacts.CreatureDisplayed; break;
                case "false-complete": slot.tutorial.mode = TutorialMode.Completed; slot.tutorial.handoffCompleted = true; break;
                case "false-report": slot.tutorial.reportAcknowledged = true; slot.tutorial.reportDay = 1; break;
                case "absent-id": slot.tutorial.first.specimenId = "absent"; break;
                case "wrong-species": slot.tutorial.first.speciesId = "bubblebelly"; break;
                case "practice-before-first": slot.tutorial.practice = slot.tutorial.first; slot.tutorial.first = new TutorialChainSave(); break;
                case "duplicate-chain": slot.tutorial.practice = slot.tutorial.first; break;
            }
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            AssertRejectedUnchanged(JsonUtility.ToJson(slot));
        }

        static SlotData SlotFor(TutorialMode mode)
        {
            if (mode == TutorialMode.Completed) return LessonSlot("complete");
            var slot = LessonSlot("orient"); var tutorial = new ShopTutorial();
            if (mode == TutorialMode.Active) tutorial.Start();
            if (mode == TutorialMode.Skipped) tutorial.Skip();
            if (mode == TutorialMode.LegacyUnguided) tutorial = ShopTutorial.LegacyUnguided();
            slot.tutorial = tutorial.CaptureSave(); return slot;
        }
        static SlotData LessonSlot(string point)
        {
            var game = new GameModel(Config); var cycle = new ShopDayCycle(); var tutorial = new ShopTutorial(); tutorial.Start();
            tutorial.ObserveOrientation(true, true, true);
            if (point != "orient")
            {
                CollectAndStage(game, tutorial, false);
                if (point != "collect")
                {
                    game.Dose(CultureKind.Bloom); game.Dose(CultureKind.Flow); tutorial.ObservePrepared(game, "pondlet", TutorialActionOrigin.Player);
                    if (point != "prepare")
                    {
                        tutorial.ObserveStarted(game, game.BeginIncubation(), TutorialActionOrigin.Player);
                        if (point != "grow")
                        {
                            game.Tick(10); tutorial.ObserveReady(game);
                            if (point != "ready")
                            {
                                tutorial.ObserveDisplayed(game, game.TransferToTank("tank-a"), TutorialActionOrigin.Player);
                                if (point != "display")
                                {
                                    tutorial.ObserveOpened(cycle.OpenShop());
                                    if (point != "open")
                                    {
                                        tutorial.ObserveSale(game, game.SellFromTank("tank-a"), TutorialActionOrigin.Player, 1);
                                        if (point != "first-sale")
                                        {
                                            tutorial.MarkPracticeAssisted(); game.Tick(20); CollectAndStage(game, tutorial, true);
                                            tutorial.ObserveStarted(game, game.BeginIncubation(), TutorialActionOrigin.Player);
                                            if (point != "practice")
                                            {
                                                game.Tick(10); tutorial.ObserveReady(game);
                                                tutorial.ObserveDisplayed(game, game.TransferToTank("tank-a"), TutorialActionOrigin.Player);
                                                tutorial.ObserveSale(game, game.SellFromTank("tank-a"), TutorialActionOrigin.Player, 1);
                                                if (point != "practice-sale")
                                                {
                                                    cycle.RequestClose(); cycle.Tick(3); tutorial.ObserveReportAcknowledged(1);
                                                    if (point == "complete") Assert.That(tutorial.Finish(cycle), Is.True);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return new SlotData { game = game.CaptureSave(), cycle = cycle.CaptureSave(), tutorial = tutorial.CaptureSave(),
                trades = ShopTradeLedger.LegacyForCycle(cycle.CaptureSave(), game.GetTank("tank-a").HasValue ? 1 : 0).CaptureSave() };
        }
        static void CollectAndStage(GameModel game, ShopTutorial tutorial, bool stage)
        {
            foreach (var kind in new[] { CultureKind.Bloom, CultureKind.Flow })
            {
                var result = game.Collect(kind); Assert.That(result.Success, Is.True); tutorial.ObserveCollection(game, kind, result, TutorialActionOrigin.Player);
                if (stage) Assert.That(game.Dose(kind).Success, Is.True);
            }
        }
        static string LegacyFreshJson(int format)
        {
            var slot = new SlotData { formatVersion = format, game = new GameModel(Config).CaptureSave(), cycle = new ShopDayCycle().CaptureSave() };
            slot.game.version = SaveStore.GameVersionFor(format);
            if (format <= 13) slot.game = LegacyGameSaveFixture.AsVersion(slot.game, slot.game.version);
            else if (format < 16) slot.game.growers = Array.FindAll(slot.game.growers, grower => grower.cultureId != "mist");
            if (format >= 3) slot.trades.version = SaveStore.ReportVersionFor(format);
            if (format >= 8) slot.layout.version = format == 8 ? 1 : format == 9 ? 2 : format == 10 ? 3 : format == 11 ? 4 : format == 12 ? 5 : format <= 14 ? 6 : 7;
            return RemoveField(JsonUtility.ToJson(slot), "tutorial", JsonUtility.ToJson(slot.tutorial));
        }
        static byte[] LegacyTechnician() => File.ReadAllBytes(Path.Combine(Application.dataPath, "EvolutionShop/Tests/RuntimeEditMode/Fixtures/V19PaidActiveTechnician.json"));
        static string RemoveField(string json, string field, string value)
        {
            string pair = "\"" + field + "\":" + value;
            return json.Contains(pair + ",") ? json.Replace(pair + ",", "") : json.Replace("," + pair, "");
        }
        void AssertReadable(string json)
        { File.WriteAllText(PathFor(), json); Assert.That(SaveStore.Read(PathFor(), Config, out _, out _, out var error), Is.True, error); }
        void AssertRejectedUnchanged(string json)
        {
            File.WriteAllText(PathFor(), json); byte[] bytes = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), Config, out var game, out var slot, out var error), Is.False);
            Assert.That(game, Is.Null); Assert.That(slot, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
        }
    }
}
