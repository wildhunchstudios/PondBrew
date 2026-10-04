using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class SaveStoreTests
    {
        string directory;
        GameConfig config;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(),"EvolutionShop-SaveTests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            config = GameConfig.CreateDefault();
        }
        [TearDown] public void Cleanup()
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            string target = Path.GetFullPath(directory);
            Assert.That(Path.GetDirectoryName(target).TrimEnd(Path.DirectorySeparatorChar),
                Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)).IgnoreCase);
            Assert.That(Path.GetFileName(target).StartsWith("EvolutionShop-SaveTests-", StringComparison.Ordinal), Is.True);
            Directory.Delete(target, true);
        }
        string PathFor(string name="slot.json") => Path.Combine(directory,name);
        static void Ok(ActionResult result) => Assert.That(result.Success,Is.True,result.Failure.ToString());
        SlotData FreshSlot() => new SlotData {game=new GameModel(config).CaptureSave(),cycle=new ShopDayCycle().CaptureSave()};
        byte[] HistoricalFixture() => File.ReadAllBytes(Path.Combine(Application.dataPath,"EvolutionShop/Tests/RuntimeEditMode/Fixtures/V1Progress.json"));

        [TestCase("fresh")]
        [TestCase("staged")]
        [TestCase("incubating")]
        [TestCase("full-tanks")]
        [TestCase("after-sale")]
        [TestCase("upgraded")]
        public void FullSlotRoundTripPreservesSettingsAndProgress(string state)
        {
            var model = new GameModel(config);
            if(state=="staged") { Collect(model,2,2); Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); model.Tick(0.5f); }
            if(state=="incubating") { StartPondlet(model); model.Tick(3.5f); }
            if(state=="full-tanks")
            {
                Produce(model,"pondlet"); Ok(model.TransferToTank("tank-a"));
                Produce(model,"bubblebelly"); Ok(model.TransferToTank("tank-b")); Produce(model,"ribbonfin");
            }
            if(state=="after-sale") { Produce(model,"pondlet"); Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a")); }
            if(state=="upgraded")
            {
                foreach(var id in new[]{"pondlet","bubblebelly","ribbonfin"}) { Produce(model,id); Ok(model.TransferToTank("tank-a")); Ok(model.SellFromTank("tank-a")); }
                Ok(model.BuyUpgrade()); model.Tick(100); Collect(model,4,4);
            }
            var slot = new SlotData {game=model.CaptureSave(),cycle=new ShopDayCycle().CaptureSave(),sensitivity=1.7f,volume=0.31f};
            Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out var restored,out var loaded,out error),Is.True,error);
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()),Is.EqualTo(JsonUtility.ToJson(model.CaptureSave())));
            Assert.That(loaded.formatVersion,Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(loaded.game.version,Is.EqualTo(GameModel.SaveVersion));
            Assert.That(loaded.sensitivity,Is.EqualTo(1.7f)); Assert.That(loaded.volume,Is.EqualTo(0.31f));
            Assert.That(SaveStore.Write(PathFor(),loaded,out error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out restored,out _,out error),Is.True,error);
            if(state=="incubating")
            {
                string id=restored.Incubator.SpecimenId;
                Assert.That(restored.Incubator.SecondsRemaining,Is.EqualTo(6.5f));
                Assert.That(restored.Tick(6.5f).Matured,Is.True); Assert.That(restored.Tick(100).Matured,Is.False);
                Assert.That(restored.Incubator.SpecimenId,Is.EqualTo(id));
            }
            if(state=="full-tanks") { string id=restored.Incubator.SpecimenId; Assert.That(restored.TransferToTank("tank-a").Success,Is.False); Assert.That(restored.Incubator.SpecimenId,Is.EqualTo(id)); }
            if(state=="after-sale") { Assert.That(restored.SellFromTank("tank-a").Success,Is.False); Assert.That(restored.Currency,Is.EqualTo(6)); }
            if(state=="upgraded") { Assert.That(restored.Currency,Is.EqualTo(2)); Assert.That(restored.ReservedCultureCount,Is.EqualTo(8)); }
        }

        [TestCase(ShopDayPhase.Night)]
        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void CurrentFormatFileRoundTripPreservesEveryDayPhase(ShopDayPhase phase)
        {
            var day=new ShopDayCycle(); day.OpenShop(); day.Tick(483f);
            if(phase!=ShopDayPhase.Night)
            {
                day.OpenShop();
                if(phase==ShopDayPhase.Closing) day.RequestClose();
                day.Tick(0.75f);
            }
            var slot=FreshSlot(); slot.cycle=day.CaptureSave(); slot.sensitivity=2.1f; slot.volume=0.23f;
            slot.trades=ShopTradeLedger.LegacyForCycle(slot.cycle,0).CaptureSave();
            Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var loaded,out error),Is.True,error);
            Assert.That(loaded.formatVersion,Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(loaded.game.version,Is.EqualTo(GameModel.SaveVersion));
            Assert.That(ShopDayCycle.TryRestore(loaded.cycle,out var restored),Is.True);
            Assert.That(restored.DayNumber,Is.EqualTo(2)); Assert.That(restored.Phase,Is.EqualTo(phase));
            Assert.That(restored.SecondsRemaining,Is.EqualTo(day.SecondsRemaining));
            Assert.That(loaded.sensitivity,Is.EqualTo(2.1f)); Assert.That(loaded.volume,Is.EqualTo(0.23f));
            day.Tick(1000f); restored.Tick(1000f);
            Assert.That(restored.DayNumber,Is.EqualTo(day.DayNumber)); Assert.That(restored.Phase,Is.EqualTo(day.Phase));
        }

        [Test] public void HistoricalVersionOneSlotMigratesInMemoryWithoutChangingItsBytesOrProgress()
        {
            byte[] original=HistoricalFixture(); File.WriteAllBytes(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out var model,out var slot,out var error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(File.Exists(PathFor()+".bak"),Is.False); Assert.That(File.Exists(PathFor()+".v1.bak"),Is.False);
            Assert.That(model.Currency,Is.EqualTo(2)); Assert.That(model.UpgradeOwned,Is.True);
            Assert.That(model.DiscoveredSpeciesIds.Count,Is.EqualTo(3)); Assert.That(model.Incubator.Phase,Is.EqualTo(IncubatorPhase.Incubating));
            Assert.That(model.Incubator.SpecimenId,Is.EqualTo("657d8c9c16744f6ab0436867af9d82d2"));
            Assert.That(model.Incubator.SecondsRemaining,Is.EqualTo(10)); Assert.That(slot.sensitivity,Is.EqualTo(1));
            Assert.That(slot.volume,Is.EqualTo(0.65f)); Assert.That(slot.formatVersion,Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(slot.game.version,Is.EqualTo(GameModel.SaveVersion)); Assert.That(slot.cycle.version,Is.EqualTo(1));
            Assert.That(slot.cycle.phase,Is.EqualTo(ShopDayPhase.Night)); Assert.That(slot.cycle.dayNumber,Is.EqualTo(1));
            Assert.That(slot.cycle.secondsRemaining,Is.Zero);
            Assert.That(slot.trades.version,Is.EqualTo(ShopTradeLedger.SaveVersion));
            Assert.That(slot.game.prices,Is.Empty); Assert.That(model.GetPrice("pondlet").Value.Price,Is.EqualTo(6));
            Assert.That(slot.trades.current,Is.Null); Assert.That(slot.trades.last,Is.Null);
            Assert.That(SaveStore.Read(PathFor(),config,out var again,out var repeated,out error),Is.True,error);
            Assert.That(again.Currency,Is.EqualTo(2)); Assert.That(repeated.cycle.dayNumber,Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
        }

        [Test] public void FirstMigrationWritePreservesImmutableLegacyBytesAcrossLaterWrites()
        {
            byte[] original=HistoricalFixture(); File.WriteAllBytes(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var slot,out var error),Is.True,error);
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor()+".bak"),Is.EqualTo(original));
            byte[] firstCurrentVersion=File.ReadAllBytes(PathFor());
            slot.volume=0.9f;
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(PathFor()+".bak"),Is.EqualTo(firstCurrentVersion));
            slot.volume=0.2f;
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(),config,out var restored,out var loaded,out error),Is.True,error);
            Assert.That(restored.Currency,Is.EqualTo(2)); Assert.That(restored.UpgradeOwned,Is.True);
            Assert.That(loaded.formatVersion,Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(loaded.volume,Is.EqualTo(0.2f));
        }

        [Test] public void MigrationDoesNotReplaceAnExistingLegacyArchive()
        {
            File.WriteAllBytes(PathFor(),HistoricalFixture());
            byte[] existingArchive={1,3,5,7,9}; File.WriteAllBytes(PathFor()+".v1.bak",existingArchive);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var slot,out var error),Is.True,error);
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.True,error);
            Assert.That(File.ReadAllBytes(PathFor()+".v1.bak"),Is.EqualTo(existingArchive));
        }

        [Test] public void FailedLegacyBackupPreventsMigrationFromReplacingPrimary()
        {
            byte[] original=HistoricalFixture(); File.WriteAllBytes(PathFor(),original);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var slot,out var error),Is.True,error);
            Directory.CreateDirectory(PathFor()+".v1.bak");
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(File.Exists(PathFor()+".bak"),Is.False);
        }

        [TestCase("game")]
        [TestCase("settings")]
        public void InvalidLegacyDataFailsMigrationWithoutChangingOriginal(string problem)
        {
            var legacy=FreshSlot(); legacy.formatVersion=1; legacy.cycle=null; legacy.game.version=1; legacy.game.prices=null;
            if(problem=="game") legacy.game.currency=-1;
            else legacy.sensitivity=float.NaN;
            string json=JsonUtility.ToJson(legacy); File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out var game,out var slot,out var error),Is.False);
            Assert.That(game,Is.Null); Assert.That(slot,Is.Null); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }

        [TestCase("missing-cycle")]
        [TestCase("null-cycle")]
        [TestCase("cycle-version")]
        [TestCase("phase")]
        [TestCase("day")]
        [TestCase("remaining")]
        [TestCase("open-zero")]
        [TestCase("future-format")]
        [TestCase("missing-format")]
        public void InvalidVersionTwoSlotIsRejectedWithoutExposingPartialGame(string problem)
        {
            var data=FreshSlot(); data.formatVersion=2; data.trades=null; data.game.version=1; data.game.prices=null;
            switch(problem)
            {
                case "null-cycle": data.cycle=null; break;
                case "cycle-version": data.cycle.version=99; break;
                case "phase": data.cycle.phase=(ShopDayPhase)99; break;
                case "day": data.cycle.dayNumber=0; break;
                case "remaining": data.cycle.secondsRemaining=2f; break;
                case "open-zero": data.cycle.phase=ShopDayPhase.Open; break;
                case "future-format": data.formatVersion=999; break;
            }
            string json=JsonUtility.ToJson(data);
            if(problem=="missing-cycle") json="{\"formatVersion\":2,\"game\":"+JsonUtility.ToJson(data.game)+",\"sensitivity\":1,\"volume\":0.5}";
            if(problem=="missing-format") json="{\"game\":"+JsonUtility.ToJson(data.game)+",\"cycle\":"+JsonUtility.ToJson(data.cycle)+"}";
            File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out var game,out var slot,out var error),Is.False);
            Assert.That(game,Is.Null); Assert.That(slot,Is.Null); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }

        [TestCase("{not valid json")]
        [TestCase("{\"formatVersion\":999,\"game\":{\"version\":1}}")]
        public void MalformedOrUnsupportedSlotIsRejectedWithoutOverwrite(string json)
        {
            File.WriteAllText(PathFor(),json);
            Assert.That(SaveStore.Read(PathFor(),config,out _,out _,out _),Is.False);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(json));
        }

        [TestCase("null-cycle")]
        [TestCase("invalid-cycle")]
        [TestCase("old-format")]
        [TestCase("future-format")]
        public void InvalidWritePreservesExistingPrimary(string problem)
        {
            var slot=FreshSlot(); Assert.That(SaveStore.Write(PathFor(),slot,out var error),Is.True,error);
            byte[] original=File.ReadAllBytes(PathFor());
            if(problem=="null-cycle") slot.cycle=null;
            if(problem=="invalid-cycle") slot.cycle.dayNumber=0;
            if(problem=="old-format") slot.formatVersion=1;
            if(problem=="future-format") slot.formatVersion=99;
            Assert.That(SaveStore.Write(PathFor(),slot,out error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()),Is.EqualTo(original));
            Assert.That(File.Exists(PathFor()+".bak"),Is.False);
        }

        [Test] public void FailedWritePreservesExistingPrimaryAndReportsError()
        {
            var original=FreshSlot(); original.volume=0.25f;
            Assert.That(SaveStore.Write(PathFor(),original,out _),Is.True);
            string bytes=File.ReadAllText(PathFor()); Directory.CreateDirectory(PathFor()+".tmp");
            original.volume=0.9f;
            Assert.That(SaveStore.Write(PathFor(),original,out var error),Is.False); Assert.That(error,Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo(bytes));
        }

        [Test] public void AtomicReplacementKeepsPreviousSlotAsBackup()
        {
            var slot=FreshSlot(); slot.volume=0.25f;
            Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True); string original=File.ReadAllText(PathFor());
            slot.volume=0.9f; Assert.That(SaveStore.Write(PathFor(),slot,out _),Is.True);
            Assert.That(File.ReadAllText(PathFor()+".bak"),Is.EqualTo(original));
            Assert.That(SaveStore.Read(PathFor(),config,out _,out var read,out _),Is.True); Assert.That(read.volume,Is.EqualTo(0.9f));
            Assert.That(File.Exists(PathFor()+".v1.bak"),Is.False);
        }

        [Test] public void InvalidPrimaryDoesNotAutomaticallyLoadEitherBackup()
        {
            var slot=FreshSlot();
            Assert.That(SaveStore.Write(PathFor()+".bak",slot,out _),Is.True);
            File.WriteAllBytes(PathFor()+".v1.bak",HistoricalFixture());
            File.WriteAllText(PathFor(),"{bad primary");
            Assert.That(SaveStore.Read(PathFor(),config,out var model,out var loaded,out _),Is.False);
            Assert.That(model,Is.Null); Assert.That(loaded,Is.Null);
            Assert.That(File.ReadAllText(PathFor()),Is.EqualTo("{bad primary"));
        }

        [TestCase(0, ShopDayPhase.Night)]
        [TestCase(47, ShopDayPhase.Open)]
        [TestCase(100, ShopDayPhase.Closing)]
        public void ReputationFileRoundTripPreservesBoundedScoreAndCompleteSlot(int points, ShopDayPhase phase)
        {
            var model = new GameModel(config);
            Produce(model, "pondlet"); Ok(model.TransferToTank("tank-a"));
            Ok(model.SetPrice("pondlet", 8));
            var day = new ShopDayCycle();
            if (phase != ShopDayPhase.Night) day.OpenShop();
            if (phase == ShopDayPhase.Closing) day.RequestClose();
            var slot = new SlotData
            {
                game = model.CaptureSave(), cycle = day.CaptureSave(),
                trades = ShopTradeLedger.LegacyForCycle(day.CaptureSave(), 1).CaptureSave(),
                reputation = new ReputationSave { version = ShopReputation.SaveVersion, points = points },
                sensitivity = 1.65f, volume = .27f
            };
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] bytes = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out error), Is.True, error);
            Assert.That(loaded.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion));
            Assert.That(loaded.reputation.version, Is.EqualTo(1));
            Assert.That(loaded.reputation.points, Is.EqualTo(points));
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()), Is.EqualTo(JsonUtility.ToJson(slot.game)));
            Assert.That(JsonUtility.ToJson(loaded.cycle), Is.EqualTo(JsonUtility.ToJson(slot.cycle)));
            Assert.That(JsonUtility.ToJson(loaded.trades), Is.EqualTo(JsonUtility.ToJson(slot.trades)));
            Assert.That(loaded.sensitivity, Is.EqualTo(slot.sensitivity));
            Assert.That(loaded.volume, Is.EqualTo(slot.volume));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(bytes));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase(ShopDayPhase.Open)]
        [TestCase(ShopDayPhase.Closing)]
        public void PendingOrdersReloadAndCancellationKeepReputationStockAndMoney(ShopDayPhase phase)
        {
            var model = new GameModel(config);
            Produce(model, "pondlet"); Ok(model.TransferToTank("tank-a"));
            Produce(model, "bubblebelly"); Ok(model.TransferToTank("tank-b"));
            var day = new ShopDayCycle(); day.OpenShop();
            if (phase == ShopDayPhase.Closing) day.RequestClose();
            var reports = new ShopTradeLedger(); Assert.That(reports.BeginDay(1, 2), Is.True);
            Assert.That(reports.BeginVisit("queued-a"), Is.True); Assert.That(reports.BeginVisit("queued-b"), Is.True);
            Ok(model.ReserveTank("tank-a", "queued-a")); Ok(model.ReserveTank("tank-b", "queued-b"));
            string a = model.GetTank("tank-a").Value.Id, b = model.GetTank("tank-b").Value.Id;
            var slot = new SlotData { game = model.CaptureSave(), cycle = day.CaptureSave(), trades = reports.CaptureSave(),
                reputation = new ReputationSave { version = 1, points = 36 } };
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out error), Is.True, error);
            Assert.That(loaded.trades.pendingCustomerIds, Is.EqualTo(new[] { "queued-a", "queued-b" }));
            Assert.That(restored.GetReservation("tank-a"), Is.Null); Assert.That(restored.GetReservation("tank-b"), Is.Null);
            Assert.That(restored.GetTank("tank-a").Value.Id, Is.EqualTo(a)); Assert.That(restored.GetTank("tank-b").Value.Id, Is.EqualTo(b));
            Assert.That(restored.Currency, Is.EqualTo(model.Currency));
            Assert.That(ShopReputation.TryRestore(loaded.reputation, out var reputation), Is.True);
            Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out var ledger), Is.True);
            Assert.That(ledger.CancelPendingForReload(), Is.True);
            Assert.That(ledger.CancelPendingForReload(), Is.False);
            Assert.That(reputation.ResolveVisit(ledger, "queued-a", CustomerOutcome.Sold, 6), Is.False);
            Assert.That(reputation.Points, Is.EqualTo(36)); Assert.That(ledger.Current.interrupted, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            loaded.trades = ledger.CaptureSave(); loaded.reputation = reputation.CaptureSave();
            Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var again, out error), Is.True, error);
            Assert.That(again.reputation.points, Is.EqualTo(36)); Assert.That(again.trades.current.interrupted, Is.EqualTo(2));
        }

        [Test]
        public void PaidSaleReloadCannotReplayMoneyOrReputation()
        {
            var slot = PaidReputationSlot(out var model, out var reports, out var reputation);
            Assert.That(reputation.Points, Is.EqualTo(2)); Assert.That(model.Currency, Is.EqualTo(6));
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var restored, out var loaded, out error), Is.True, error);
                Assert.That(ShopTradeLedger.TryRestore(loaded.trades, loaded.cycle, out reports), Is.True);
                Assert.That(ShopReputation.TryRestore(loaded.reputation, out reputation), Is.True);
                Assert.That(reputation.ResolveVisit(reports, "paid-visitor", CustomerOutcome.Sold, 6), Is.False);
                Assert.That(restored.SellFromTank("tank-a").Success, Is.False);
                Assert.That(restored.GetTank("tank-a"), Is.Null);
                Assert.That(restored.Currency, Is.EqualTo(6)); Assert.That(reputation.Points, Is.EqualTo(2));
                Assert.That(reports.Current.sales, Is.EqualTo(1)); Assert.That(reports.Current.revenue, Is.EqualTo(6));
                loaded.reputation = reputation.CaptureSave(); loaded.trades = reports.CaptureSave();
                Assert.That(SaveStore.Write(PathFor(), loaded, out error), Is.True, error);
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void LegacyFormatsStartReputationAtZeroWithoutBackfillOrReadWrites(int format)
        {
            byte[] original = WriteLegacyReputationSlot(format);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(SaveStore.Read(PathFor(), config, out var model, out var slot, out var error), Is.True, error);
                Assert.That(slot.formatVersion, Is.EqualTo(SaveStore.CurrentFormatVersion)); Assert.That(slot.reputation.version, Is.EqualTo(1));
                Assert.That(slot.reputation.points, Is.Zero); Assert.That(model.Currency, Is.EqualTo(6));
                Assert.That(model.GetTank("tank-a"), Is.Null);
                if (format >= 3) { Assert.That(slot.trades.current.sales, Is.EqualTo(1)); Assert.That(slot.trades.current.revenue, Is.EqualTo(6)); }
                Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void FormatFiveMigrationArchivesExactBytesAndKeepsThemAcrossLaterWrites()
        {
            byte[] legacy = WriteLegacyReputationSlot(5);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v5.bak"), Is.EqualTo(legacy));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(legacy));
            byte[] firstCurrent = File.ReadAllBytes(PathFor());
            slot.reputation.points = 14;
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v5.bak"), Is.EqualTo(legacy));
            Assert.That(File.ReadAllBytes(PathFor() + ".bak"), Is.EqualTo(firstCurrent));
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var loaded, out error), Is.True, error);
            Assert.That(loaded.reputation.points, Is.EqualTo(14));
        }

        [Test]
        public void FormatFiveMigrationNeverOverwritesAnExistingArchive()
        {
            WriteLegacyReputationSlot(5);
            byte[] archive = { 5, 1, 9, 2 }; File.WriteAllBytes(PathFor() + ".v5.bak", archive);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.True, error);
            Assert.That(File.ReadAllBytes(PathFor() + ".v5.bak"), Is.EqualTo(archive));
        }

        [Test]
        public void FailedFormatFiveArchivePreservesPrimaryAndDoesNotCreateRotatingBackup()
        {
            byte[] original = WriteLegacyReputationSlot(5);
            Assert.That(SaveStore.Read(PathFor(), config, out _, out var slot, out var error), Is.True, error);
            Directory.CreateDirectory(PathFor() + ".v5.bak");
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(File.Exists(PathFor() + ".bak"), Is.False);
        }

        [TestCase("missing")] [TestCase("null")] [TestCase("empty")] [TestCase("missing-version")]
        [TestCase("version-zero")] [TestCase("version-future")] [TestCase("version-negative")]
        [TestCase("points-negative")] [TestCase("points-overmax")]
        public void MalformedCurrentReputationIsRejectedWithoutChangingTheFile(string problem)
        {
            string payload = "{\"version\":1,\"points\":31}";
            switch (problem)
            {
                case "null": payload = "null"; break;
                case "empty": payload = "{}"; break;
                case "missing-version": payload = "{\"points\":31}"; break;
                case "version-zero": payload = "{\"version\":0,\"points\":31}"; break;
                case "version-future": payload = "{\"version\":" + (ShopReputation.SaveVersion + 1) + ",\"points\":31}"; break;
                case "version-negative": payload = "{\"version\":-1,\"points\":31}"; break;
                case "points-negative": payload = "{\"version\":1,\"points\":-1}"; break;
                case "points-overmax": payload = "{\"version\":1,\"points\":" + (ShopReputation.MaxPoints + 1) + "}"; break;
            }
            var slot = FreshSlot();
            string json = "{\"formatVersion\":" + SaveStore.CurrentFormatVersion + ",\"game\":" + JsonUtility.ToJson(slot.game) +
                ",\"cycle\":" + JsonUtility.ToJson(slot.cycle) + ",\"trades\":" + JsonUtility.ToJson(slot.trades) +
                ",\"layout\":" + JsonUtility.ToJson(slot.layout) +
                (problem == "missing" ? "" : ",\"reputation\":" + payload) + ",\"slapKey\":\"F\",\"humorVolume\":1,\"reducedGags\":false,\"sensitivity\":1,\"volume\":0.5}";
            File.WriteAllText(PathFor(), json);
            Assert.That(SaveStore.Read(PathFor(), config, out var model, out var loaded, out var error), Is.False);
            Assert.That(model, Is.Null); Assert.That(loaded, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllText(PathFor()), Is.EqualTo(json));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [TestCase("null")] [TestCase("version-zero")] [TestCase("version-future")]
        [TestCase("points-negative")] [TestCase("points-overmax")]
        public void InvalidReputationWritePreservesKnownGoodPrimaryBeforeAnyFileWork(string problem)
        {
            var slot = FreshSlot(); slot.reputation.points = 22;
            Assert.That(SaveStore.Write(PathFor(), slot, out var error), Is.True, error);
            byte[] original = File.ReadAllBytes(PathFor());
            switch (problem)
            {
                case "null": slot.reputation = null; break;
                case "version-zero": slot.reputation.version = 0; break;
                case "version-future": slot.reputation.version = ShopReputation.SaveVersion + 1; break;
                case "points-negative": slot.reputation.points = -1; break;
                case "points-overmax": slot.reputation.points = ShopReputation.MaxPoints + 1; break;
            }
            Assert.That(SaveStore.Write(PathFor(), slot, out error), Is.False); Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(PathFor()), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test]
        public void FormatThirteenRetainsExplicitHistoricalGameAndReportPairings()
        {
            Assert.That(SaveStore.CurrentFormatVersion, Is.EqualTo(13));
            int[] games = { 0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3 }, reports = { 0, 0, 0, 1, 2, 3, 3, 4, 4, 4, 4, 4, 4, 4 };
            for (int format = 1; format <= 13; format++)
            {
                Assert.That(SaveStore.GameVersionFor(format), Is.EqualTo(games[format]));
                Assert.That(SaveStore.ReportVersionFor(format), Is.EqualTo(reports[format]));
            }
            Assert.That(new SlotData().reputation.version, Is.EqualTo(1));
            Assert.That(new SlotData().reputation.points, Is.Zero);
        }

        SlotData PaidReputationSlot(out GameModel model, out ShopTradeLedger reports, out ShopReputation reputation)
        {
            model = new GameModel(config); Produce(model, "pondlet"); Ok(model.TransferToTank("tank-a"));
            var day = new ShopDayCycle(); Assert.That(day.OpenShop(), Is.True);
            reports = new ShopTradeLedger(); Assert.That(reports.BeginDay(1, 1), Is.True);
            Assert.That(reports.BeginVisit("paid-visitor"), Is.True);
            Ok(model.ReserveTank("tank-a", "paid-visitor"));
            string specimen = model.GetTank("tank-a").Value.Id;
            Ok(model.SellReservedTank("tank-a", "paid-visitor", specimen));
            reputation = new ShopReputation();
            Assert.That(reputation.ResolveVisit(reports, "paid-visitor", CustomerOutcome.Sold, 6), Is.True);
            return new SlotData { game = model.CaptureSave(), cycle = day.CaptureSave(), trades = reports.CaptureSave(), reputation = reputation.CaptureSave() };
        }

        byte[] WriteLegacyReputationSlot(int format)
        {
            var slot = PaidReputationSlot(out _, out _, out _);
            slot.game.version = format < 4 ? 1 : 2;
            if (format < 4) slot.game.prices = null;
            slot.trades.version = format == 3 ? 1 : format == 4 ? 2 : 3;
            if (format < 5) slot.trades.pendingCustomerIds = null;
            string json = "{\n  \"formatVersion\": " + format + ",\n  \"game\": " + JsonUtility.ToJson(slot.game, true) +
                (format >= 2 ? ",\n  \"cycle\": " + JsonUtility.ToJson(slot.cycle, true) : "") +
                (format >= 3 ? ",\n  \"trades\": " + JsonUtility.ToJson(slot.trades, true) : "") +
                ",\n  \"sensitivity\": 1.2,\n  \"volume\": 0.4\n}";
            File.WriteAllText(PathFor(), json);
            return File.ReadAllBytes(PathFor());
        }

        void Collect(GameModel model,int bloom,int flow)
        {
            for(int i=0;i<bloom;i++) Ok(model.Collect(CultureKind.Bloom));
            for(int i=0;i<flow;i++) Ok(model.Collect(CultureKind.Flow));
        }
        void StartPondlet(GameModel model) { Collect(model,1,1); Ok(model.Dose(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Flow)); Ok(model.BeginIncubation()); }
        void Produce(GameModel model,string id)
        {
            model.Tick(100); var recipe=model.GetSpecies(id); Collect(model,recipe.Bloom,recipe.Flow);
            for(int i=0;i<recipe.Bloom;i++) Ok(model.Dose(CultureKind.Bloom));
            for(int i=0;i<recipe.Flow;i++) Ok(model.Dose(CultureKind.Flow));
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds);
        }
    }
}
