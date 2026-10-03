using System;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    [Serializable]
    public sealed class SlotData
    {
        public int formatVersion = SaveStore.CurrentFormatVersion;
        public SaveData game;
        public ShopDaySave cycle;
        public TradeLedgerSave trades = new TradeLedgerSave();
        public ReputationSave reputation = new ShopReputation().CaptureSave();
        public float sensitivity = 1f;
        public float volume = 0.65f;
        public string slapKey = "F";
        public float humorVolume = 1f;
        public bool reducedGags;
    }

    public static class SaveStore
    {
        // 4: price tags (game 2) and price refusals (report 2). 5: several unresolved visitors (report 3).
        // 6: reputation. 7: slap outcomes and intentional slap/audio/motion settings.
        // Formats 1-6 migrate in memory; historical reports never invent new outcomes or rewards.
        public const int CurrentFormatVersion = 7;

        /// <summary>The game version each envelope carried: price tags arrived with format 4.</summary>
        public static int GameVersionFor(int format) => format >= 1 && format <= 3 ? 1 :
            format >= 4 && format <= 7 ? 2 : 0;

        /// <summary>The report version each envelope carried; formats 1-2 had no report.</summary>
        public static int ReportVersionFor(int format) => format == 7 ? 4 : format == 5 || format == 6 ? 3 :
            format == 4 ? 2 : format == 3 ? 1 : 0;
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "evolution-shop.json");

        // Zero distinguishes an omitted envelope version from a current save.
        [Serializable]
        private sealed class SlotHeader
        {
            public int formatVersion = 0;
            public ReportHeader trades = null;
            public ReputationHeader reputation = null;
            // A missing current-format binding must not silently become SlotData's default F.
            public string slapKey = null;
        }

        [Serializable]
        private sealed class ReportHeader
        {
            // JsonUtility can manufacture inline objects for missing/null fields. A separate
            // zero-default version detects them without TradeLedgerSave's version-one initializer.
            public int version = 0;
        }

        [Serializable]
        private sealed class ReputationHeader
        {
            // SlotData's convenient fresh-state initializer must not validate missing JSON data.
            public int version = 0;
        }

        public static bool Write(string path, SlotData data, out string error)
        {
            error = null;
            if (data == null || data.formatVersion != CurrentFormatVersion || data.game == null ||
                data.game.version != GameModel.SaveVersion || data.trades == null ||
                data.trades.version != ShopTradeLedger.SaveVersion || !Finite(data.sensitivity) || !Finite(data.volume) ||
                !ShopDayCycle.TryRestore(data.cycle, out _) ||
                !ShopTradeLedger.TryRestore(data.trades, data.cycle, out _) ||
                !ShopReputation.TryRestore(data.reputation, out _) ||
                !SlapRules.IsAllowedKey(data.slapKey) || !ValidHumorVolume(data.humorVolume))
            {
                error = "Could not save: the current slot, day clock, trading report, reputation, or gag settings are invalid. Original file preserved.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                {
                    // Keep the first legacy file byte-for-byte, independently of the rotating backup.
                    int legacyVersion = LegacyVersion(File.ReadAllText(path));
                    if (legacyVersion != 0)
                    {
                        string legacyBackup = path + ".v" + legacyVersion + ".bak";
                        if (!File.Exists(legacyBackup)) File.Copy(path, legacyBackup, false);
                    }
                    File.Replace(temp, path, path + ".bak");
                }
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                error = "Could not save: " + e.Message;
                return false;
            }
        }

        public static bool Read(string path, GameConfig config, out GameModel game, out SlotData slot, out string error)
        {
            game = null; slot = null; error = null;
            if (!File.Exists(path)) return false;
            try
            {
                string json = File.ReadAllText(path);
                var header = JsonUtility.FromJson<SlotHeader>(json);
                var candidate = JsonUtility.FromJson<SlotData>(json);
                // Each envelope carries exactly one game version: price tags arrived with format 4.
                if (header == null || header.formatVersion < 1 || header.formatVersion > CurrentFormatVersion ||
                    candidate == null || candidate.game == null ||
                    candidate.game.version != GameVersionFor(header.formatVersion) ||
                    !Finite(candidate.sensitivity) || !Finite(candidate.volume) ||
                    !GameModel.TryRestore(config, candidate.game, out var restoredGame, out _))
                {
                    error = "Save could not be validated. Your original file is preserved. Confirm New game to replace it.";
                    return false;
                }

                if (header.formatVersion == 1)
                {
                    // Migration changes only the returned data. Reading never writes or grants rewards.
                    candidate.cycle = new ShopDayCycle().CaptureSave();
                }
                else if (!ShopDayCycle.TryRestore(candidate.cycle, out _))
                {
                    error = "Saved day clock could not be validated. Your original file is preserved.";
                    return false;
                }

                if (header.formatVersion < 3)
                {
                    int stockedCount = 0;
                    foreach (var tank in restoredGame.CaptureSave().tanks)
                        if (restoredGame.GetTank(tank.tankId).HasValue) stockedCount++;
                    candidate.trades = ShopTradeLedger.LegacyForCycle(candidate.cycle, stockedCount).CaptureSave();
                }
                else
                {
                    int reportVersion = ReportVersionFor(header.formatVersion);
                    if (header.trades == null || header.trades.version != reportVersion ||
                        !ShopTradeLedger.TryRestore(candidate.trades, candidate.cycle, out var restoredTrades))
                    {
                        error = "Saved trading report could not be validated. Your original file is preserved.";
                        return false;
                    }
                    // Normalize empty inline day objects back to absent snapshots and upgrade a
                    // version 1 report. Keep the pending visitor until Session reconciles a reload.
                    candidate.trades = restoredTrades.CaptureSave();
                }

                if (header.formatVersion < 6)
                {
                    // Earlier versions never earned reputation. Do not backfill old sales or refusals.
                    candidate.reputation = new ShopReputation().CaptureSave();
                }
                else
                {
                    if (header.reputation == null || header.reputation.version != ShopReputation.SaveVersion ||
                        !ShopReputation.TryRestore(candidate.reputation, out var restoredReputation))
                    {
                        error = "Saved reputation could not be validated. Your original file is preserved.";
                        return false;
                    }
                    candidate.reputation = restoredReputation.CaptureSave();
                }

                if (header.formatVersion < 7)
                {
                    candidate.slapKey = "F";
                    candidate.humorVolume = 1f;
                    candidate.reducedGags = false;
                }
                else if (!SlapRules.IsAllowedKey(header.slapKey) || !SlapRules.IsAllowedKey(candidate.slapKey) ||
                    !ValidHumorVolume(candidate.humorVolume))
                {
                    error = "Saved gag settings could not be validated. Your original file is preserved.";
                    return false;
                }

                // A version 1 game restores with standard prices; capture it in the current form.
                candidate.game = restoredGame.CaptureSave();
                candidate.formatVersion = CurrentFormatVersion;
                candidate.sensitivity = Mathf.Clamp(candidate.sensitivity, 0.25f, 2.5f);
                candidate.volume = Mathf.Clamp01(candidate.volume);
                game = restoredGame;
                slot = candidate;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                error = "Save could not be read. Original file preserved. " + e.Message;
                return false;
            }
        }

        private static int LegacyVersion(string json)
        {
            try
            {
                var header = JsonUtility.FromJson<SlotHeader>(json);
                return header != null && header.formatVersion >= 1 && header.formatVersion < CurrentFormatVersion
                    ? header.formatVersion : 0;
            }
            catch (ArgumentException)
            {
                // Confirmed New game may replace a corrupt primary; it is not a legacy migration.
                return 0;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidHumorVolume(float value) => Finite(value) && value >= 0f && value <= 1f;
    }
}
