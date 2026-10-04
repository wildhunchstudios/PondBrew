using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        public LayoutSave layout = new ShopLayout().CaptureSave();
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
        // 8: owned work-table layout. 9: two store expansions and owned stock displays.
        // 10: initial decor. Appeal is derived from layout and is never a second saved authority.
        // 11: stored furniture, permanent sales, and stable IDs across ownership gaps.
        // 12: two lab expansions and poses for the four existing production facilities.
        // Formats 1-11 migrate in memory; historical saves never
        // invent furniture, outcomes, rewards, or purchases.
        public const int CurrentFormatVersion = 12;

        /// <summary>The game version each envelope carried: price tags arrived with format 4.</summary>
        public static int GameVersionFor(int format) => format >= 1 && format <= 3 ? 1 :
            format >= 4 && format <= 12 ? 2 : 0;

        /// <summary>The report version each envelope carried; formats 1-2 had no report.</summary>
        public static int ReportVersionFor(int format) => format >= 7 && format <= 12 ? 4 : format == 5 || format == 6 ? 3 :
            format == 4 ? 2 : format == 3 ? 1 : 0;
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "evolution-shop.json");

        // Zero distinguishes an omitted envelope version from a current save.
        [Serializable]
        private sealed class SlotHeader
        {
            public int formatVersion = 0;
            public ReportHeader trades = null;
            public ReputationHeader reputation = null;
            public LayoutHeader layout = null;
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

        [Serializable]
        private sealed class LayoutHeader
        {
            // A missing/null/empty inline layout must not inherit the fresh-slot initializer.
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
                data.layout == null || data.layout.version != ShopLayout.SaveVersion ||
                !ShopLayout.TryRestore(data.layout, out var layout) ||
                !GameModel.TryRestore(layout.WithTankIds(GameConfig.CreateDefault()), data.game, out _, out _) ||
                !SlapRules.IsAllowedKey(data.slapKey) || !ValidHumorVolume(data.humorVolume))
            {
                error = "Could not save: the current slot, day clock, trading report, reputation, layout, or gag settings are invalid. Original file preserved.";
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
                    !Finite(candidate.sensitivity) || !Finite(candidate.volume))
                {
                    error = "Save could not be validated. Your original file is preserved. Confirm New game to replace it.";
                    return false;
                }

                // Furniture owns the stock-slot registry. Validate it before inventory so an
                // orphan slot cannot grant capacity, and a missing slot cannot delete stock.
                ShopLayout restoredLayout;
                if (header.formatVersion < 8)
                {
                    restoredLayout = new ShopLayout();
                    candidate.layout = restoredLayout.CaptureSave();
                }
                else
                {
                    int layoutVersion = header.formatVersion == 8 ? 1 : header.formatVersion == 9 ? 2 :
                        header.formatVersion == 10 ? 3 : header.formatVersion == 11 ? 4 : 5;
                    if (header.layout == null || header.layout.version != layoutVersion ||
                        !new LayoutJsonShape(json, header.formatVersion >= 9, header.formatVersion >= 11,
                            header.formatVersion >= 12).HasExplicitItemsArray() ||
                        !ShopLayout.TryRestore(candidate.layout, out restoredLayout))
                    {
                        error = "Saved layout could not be validated. Your original file is preserved.";
                        return false;
                    }
                    candidate.layout = restoredLayout.CaptureSave();
                }
                if (!GameModel.TryRestore(restoredLayout.WithTankIds(config), candidate.game, out var restoredGame, out _))
                {
                    error = "Saved stock and layout could not be validated. Your original file is preserved.";
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

        // JsonUtility normalizes a missing/null array to [], which is otherwise a valid empty
        // layout. Missing/malformed bools can likewise become false. Check only the raw
        // root/layout/item shape; JsonUtility and domain restore still
        // own JSON deserialization and semantic validation. Never search for a token inside
        // an unrelated nested value or quoted string.
        private sealed class LayoutJsonShape
        {
            readonly string json;
            readonly bool requireStoreTier;
            readonly bool requireStored;
            readonly bool requireLab;
            int position;
            public LayoutJsonShape(string json, bool requireStoreTier, bool requireStored, bool requireLab)
            { this.json = json; this.requireStoreTier = requireStoreTier; this.requireStored = requireStored; this.requireLab = requireLab; }

            public bool HasExplicitItemsArray()
            {
                if (json == null || !ReadObject(false)) return false;
                SkipSpace(); return position == json.Length;
            }

            bool ReadObject(bool layout)
            {
                if (!Take('{')) return false;
                bool required = false, hasStoreTier = false, hasLabTier = false, hasFacilities = false;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                SkipSpace();
                if (Take('}')) return false;
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    if (!layout && key == "layout")
                    {
                        if (!ReadObject(true)) return false;
                        required = true;
                    }
                    else if (layout && key == "items")
                    {
                        SkipSpace();
                        if (position >= json.Length || json[position] != '[' ||
                            !(requireStored ? ReadItems() : SkipValue(0))) return false;
                        required = true;
                    }
                    else if (layout && requireStoreTier && key == "storeTier")
                    {
                        // A missing/null/bool/string/fraction must not normalize to valid tier 0.
                        SkipSpace();
                        if (position >= json.Length || json[position] < '0' || json[position] > '2') return false;
                        position++;
                        if (position < json.Length && !char.IsWhiteSpace(json[position]) &&
                            json[position] != ',' && json[position] != '}') return false;
                        hasStoreTier = true;
                    }
                    else if (layout && requireLab && key == "labTier")
                    {
                        if (!ReadInteger(out int tier) || tier < 0 || tier > 2) return false;
                        hasLabTier = true;
                    }
                    else if (layout && requireLab && key == "facilities")
                    {
                        if (!ReadFacilities()) return false;
                        hasFacilities = true;
                    }
                    else if (!SkipValue(0)) return false;
                    if (Take('}')) return required && (!layout ||
                        ((!requireStoreTier || hasStoreTier) && (!requireLab || hasLabTier && hasFacilities)));
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadFacilities()
            {
                if (!Take('[') || Take(']')) return false;
                int count = 0;
                while (position < json.Length)
                {
                    if (++count > 4 || !ReadFacility()) return false;
                    if (Take(']')) return count == 4;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadFacility()
            {
                if (!Take('{')) return false;
                int required = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    if (key == "id")
                    { if (!ReadString(true, out _)) return false; required |= 1; }
                    else if (key == "x" || key == "z" || key == "quarterTurns")
                    {
                        if (!ReadInteger(out int value) || key == "quarterTurns" && (value < 0 || value > 3)) return false;
                        required |= key == "x" ? 2 : key == "z" ? 4 : 8;
                    }
                    else if (!SkipValue(0)) return false;
                    if (Take('}')) return required == 15;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadInteger(out int value)
            {
                value = 0; SkipSpace(); int began = position;
                if (position < json.Length && json[position] == '-') position++;
                if (position >= json.Length || json[position] < '0' || json[position] > '9') return false;
                if (json[position] == '0') position++;
                else while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++;
                if (position < json.Length && !char.IsWhiteSpace(json[position]) &&
                    json[position] != ',' && json[position] != '}') return false;
                return int.TryParse(json.Substring(began, position - began), System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            bool ReadItems()
            {
                if (!Take('[')) return false;
                if (Take(']')) return true;
                while (position < json.Length)
                {
                    if (!ReadItem()) return false;
                    if (Take(']')) return true;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadItem()
            {
                if (!Take('{')) return false;
                bool hasStored = false;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    if (key == "stored")
                    {
                        SkipSpace();
                        int length = position + 4 <= json.Length &&
                            string.CompareOrdinal(json, position, "true", 0, 4) == 0 ? 4 :
                            position + 5 <= json.Length &&
                            string.CompareOrdinal(json, position, "false", 0, 5) == 0 ? 5 : 0;
                        if (length == 0) return false;
                        position += length;
                        if (position < json.Length && !char.IsWhiteSpace(json[position]) &&
                            json[position] != ',' && json[position] != '}') return false;
                        hasStored = true;
                    }
                    else if (!SkipValue(0)) return false;
                    if (Take('}')) return hasStored;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool SkipValue(int depth)
            {
                if (depth > 32) return false;
                SkipSpace();
                if (position >= json.Length) return false;
                char first = json[position];
                if (first == '"') return ReadString(false, out _);
                if (first == '{' || first == '[')
                {
                    char closing = first == '{' ? '}' : ']'; position++;
                    while (position < json.Length)
                    {
                        SkipSpace();
                        if (position >= json.Length) return false;
                        char next = json[position];
                        if (next == closing) { position++; return true; }
                        if (next == '}' || next == ']') return false;
                        if (next == '"') { if (!ReadString(false, out _)) return false; }
                        else if (next == '{' || next == '[') { if (!SkipValue(depth + 1)) return false; }
                        else position++;
                    }
                    return false;
                }
                int began = position;
                while (position < json.Length && json[position] != ',' && json[position] != '}' &&
                    json[position] != ']' && !char.IsWhiteSpace(json[position])) position++;
                return position > began;
            }

            bool ReadString(bool capture, out string value)
            {
                value = null;
                if (!Take('"')) return false;
                StringBuilder builder = capture ? new StringBuilder() : null;
                while (position < json.Length)
                {
                    char next = json[position++];
                    if (next == '"') { value = capture ? builder.ToString() : null; return true; }
                    if (next < ' ') return false;
                    if (next == '\\')
                    {
                        if (position >= json.Length) return false;
                        next = json[position++];
                        switch (next)
                        {
                            case '"': case '\\': case '/': break;
                            case 'b': next = '\b'; break;
                            case 'f': next = '\f'; break;
                            case 'n': next = '\n'; break;
                            case 'r': next = '\r'; break;
                            case 't': next = '\t'; break;
                            case 'u':
                                int code = 0;
                                for (int i = 0; i < 4; i++)
                                {
                                    if (position >= json.Length) return false;
                                    char digit = json[position++];
                                    int hex = digit >= '0' && digit <= '9' ? digit - '0' :
                                        digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 :
                                        digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                                    if (hex < 0) return false;
                                    code = code * 16 + hex;
                                }
                                next = (char)code; break;
                            default: return false;
                        }
                    }
                    if (capture)
                    {
                        if (builder.Length >= 128) return false;
                        builder.Append(next);
                    }
                }
                return false;
            }

            bool Take(char expected)
            {
                SkipSpace();
                if (position >= json.Length || json[position] != expected) return false;
                position++; return true;
            }
            void SkipSpace() { while (position < json.Length && char.IsWhiteSpace(json[position])) position++; }
        }
    }
}
