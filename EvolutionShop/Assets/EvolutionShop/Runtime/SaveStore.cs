using System;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    [Serializable]
    public sealed class SlotData
    {
        public int formatVersion = 1;
        public SaveData game;
        public float sensitivity = 1f;
        public float volume = 0.65f;
    }
    public static class SaveStore
    {
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "evolution-shop.json");
        public static bool Write(string path, SlotData data, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
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
                slot = JsonUtility.FromJson<SlotData>(File.ReadAllText(path));
                if (slot == null || slot.formatVersion != 1 || slot.game == null ||
                    float.IsNaN(slot.sensitivity) || float.IsInfinity(slot.sensitivity) ||
                    float.IsNaN(slot.volume) || float.IsInfinity(slot.volume) ||
                    !GameModel.TryRestore(config, slot.game, out game, out _))
                {
                    error = "Save could not be validated. Your original file is preserved. Confirm New game to replace it.";
                    return false;
                }
                slot.sensitivity = Mathf.Clamp(slot.sensitivity, 0.25f, 2.5f);
                slot.volume = Mathf.Clamp01(slot.volume);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                error = "Save could not be read. Original file preserved. " + e.Message;
                return false;
            }
        }
    }
}
