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
        public float sensitivity = 1f;
        public float volume = 0.65f;
    }

    public static class SaveStore
    {
        public const int CurrentFormatVersion = 2;
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "evolution-shop.json");

        // Zero distinguishes an omitted envelope version from a current save.
        [Serializable]
        private sealed class SlotHeader
        {
            public int formatVersion = 0;
        }

        public static bool Write(string path, SlotData data, out string error)
        {
            error = null;
            if (data == null || data.formatVersion != CurrentFormatVersion || data.game == null ||
                data.game.version != 1 || !Finite(data.sensitivity) || !Finite(data.volume) ||
                !ShopDayCycle.TryRestore(data.cycle, out _))
            {
                error = "Could not save: the current slot or day clock is invalid. Original file preserved.";
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
                    string legacyBackup = path + ".v1.bak";
                    if (!File.Exists(legacyBackup) && IsVersionOne(File.ReadAllText(path)))
                        File.Copy(path, legacyBackup, false);
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
                if (header == null || (header.formatVersion != 1 && header.formatVersion != CurrentFormatVersion) ||
                    candidate == null || candidate.game == null ||
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

        private static bool IsVersionOne(string json)
        {
            try
            {
                var header = JsonUtility.FromJson<SlotHeader>(json);
                return header != null && header.formatVersion == 1;
            }
            catch (ArgumentException)
            {
                // Confirmed New game may replace a corrupt primary; it is not a version-one migration.
                return false;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
