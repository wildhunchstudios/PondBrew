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
        public StaffSave staff = new ShopStaff().CaptureSave();
        public StockerSave stocker = new StockerSave
        {
            version = ShopStocking.SaveVersion, employee = new ShopStaff(ShopStaff.StockerId).CaptureSave(),
            targets = Array.Empty<StockerTargetSave>()
        };
        public TechnicianSave technician = new TechnicianSave
        {
            version = ShopLaboratory.SaveVersion, employee = new ShopStaff(ShopStaff.TechnicianId).CaptureSave(),
            queue = Array.Empty<LabOrderSave>()
        };
        public TutorialSave tutorial = new ShopTutorial().CaptureSave();
        public AutomationSave automation = new ShopAutomation().CaptureSave();
        public float sensitivity = 1f;
        public float volume = 0.65f;
        public string slapKey = "F";
        public float humorVolume = 1f;
        public float ambienceVolume = FxAudioMix.DefaultAmbienceVolume;
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
        // 13: separate specimen-rack storage (game 3, layout 6).
        // 14: Spark research and a third culture (game 4; layout remains 6).
        // 15: an owned conditioning vat and exact batch residents (game 5, layout 7).
        // 16: Mist research (game 6). 17: first cashier employment and paid-period receipts.
        // 18: stocker employment and explicit display targets; jobs/reservations stay transient.
        // 19: technician employment, finite recipe priorities, and exact active batch ticket.
        // 20: guided-opening learning receipts; legacy saves remain unguided.
        // 21: optional one-batch culture collector/doser; shared inventory remains GameModel authority.
        // 22: machine/ambience volume; gameplay and layout versions are unchanged.
        // Formats 1-21 migrate in memory; historical saves never
        // invent furniture, outcomes, rewards, or purchases.
        public const int CurrentFormatVersion = 22;

        /// <summary>The game version each envelope carried: price tags arrived with format 4.</summary>
        public static int GameVersionFor(int format) => format >= 1 && format <= 3 ? 1 :
            format >= 4 && format <= 12 ? 2 : format == 13 ? 3 : format == 14 ? 4 : format == 15 ? 5 : format >= 16 && format <= 22 ? 6 : 0;

        /// <summary>The report version each envelope carried; formats 1-2 had no report.</summary>
        public static int ReportVersionFor(int format) => format >= 7 && format <= 22 ? 4 : format == 5 || format == 6 ? 3 :
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
                !ShopStaff.TryRestore(data.staff, data.cycle.dayNumber, out _) ||
                !ShopTradeLedger.TryRestore(data.trades, data.cycle, out _) ||
                !ShopReputation.TryRestore(data.reputation, out _) ||
                data.layout == null || data.layout.version != ShopLayout.SaveVersion ||
                !ShopLayout.TryRestore(data.layout, out var layout) ||
                !GameModel.TryRestore(layout.WithTankIds(GameConfig.CreateDefault()), data.game, out var game, out _) ||
                !ShopStocking.TryRestore(data.stocker, game, data.cycle.dayNumber, out _) ||
                !ShopLaboratory.TryRestore(data.technician, game, data.cycle.dayNumber, out _) ||
                !ShopTutorial.TryRestore(data.tutorial, game, data.cycle.dayNumber, out _) ||
                !ShopAutomation.TryRestore(data.automation, game, out _) ||
                !ValidOwnedConditionerProgress(layout, game) ||
                !SlapRules.IsAllowedKey(data.slapKey) || !ValidHumorVolume(data.humorVolume) || !ValidHumorVolume(data.ambienceVolume))
            {
                error = "Could not save: the current slot, day clock, trading report, reputation, layout, staff, stocker, technician, tutorial, automation, or gag settings are invalid. Original file preserved.";
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
                // Check the new array and its entry fields before the full JsonUtility parse can
                // manufacture empty/default values for missing, null or incorrectly typed data.
                bool currentStorage = header != null && header.formatVersion >= 13 && header.formatVersion <= CurrentFormatVersion;
                if (currentStorage && !new LayoutJsonShape(json, true, true, true, true,
                    header.formatVersion >= 14, header.formatVersion >= 15, header.formatVersion >= 16,
                    header.formatVersion >= 17, header.formatVersion >= 18, header.formatVersion >= 19,
                    header.formatVersion >= 20, header.formatVersion >= 21, header.formatVersion >= 22).HasExplicitItemsArray())
                {
                    error = "Saved layout, storage, research, conditioning, staff, stocker, technician, tutorial or automation shape could not be validated. Your original file is preserved.";
                    return false;
                }
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
                        header.formatVersion == 10 ? 3 : header.formatVersion == 11 ? 4 : header.formatVersion == 12 ? 5 :
                        header.formatVersion <= 14 ? 6 : 7;
                    if (header.layout == null || header.layout.version != layoutVersion ||
                        (!currentStorage && !new LayoutJsonShape(json, header.formatVersion >= 9, header.formatVersion >= 11,
                            header.formatVersion >= 12).HasExplicitItemsArray()) ||
                        !ShopLayout.TryRestore(candidate.layout, out restoredLayout))
                    {
                        error = "Saved layout could not be validated. Your original file is preserved.";
                        return false;
                    }
                    candidate.layout = restoredLayout.CaptureSave();
                }
                if (!GameModel.TryRestore(restoredLayout.WithTankIds(config), candidate.game, out var restoredGame, out _) ||
                    !ValidOwnedConditionerProgress(restoredLayout, restoredGame))
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

                if (header.formatVersion < 17)
                {
                    // Historical saves never employed staff. Migration reads and restores no wages.
                    candidate.staff = new ShopStaff().CaptureSave();
                }
                else if (!ShopStaff.TryRestore(candidate.staff, candidate.cycle.dayNumber, out _))
                {
                    error = "Saved staff could not be validated. Your original file is preserved.";
                    return false;
                }

                if (header.formatVersion < 18)
                {
                    // Cashier-only saves acquire no hired stocker, target, charge or work.
                    candidate.stocker = new ShopStocking().CaptureSave(restoredGame);
                }
                else if (!ShopStocking.TryRestore(candidate.stocker, restoredGame, candidate.cycle.dayNumber, out _))
                {
                    error = "Saved stocker settings could not be validated. Your original file is preserved.";
                    return false;
                }

                if (header.formatVersion < 19)
                {
                    // Prior saves acquire no hired technician, orders, batch ticket, payment or work.
                    candidate.technician = new ShopLaboratory().CaptureSave(restoredGame);
                }
                else if (!ShopLaboratory.TryRestore(candidate.technician, restoredGame, candidate.cycle.dayNumber, out _))
                {
                    error = "Saved technician settings could not be validated. Your original file is preserved.";
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

                if (header.formatVersion < 20)
                {
                    // Preserve established shops: migration neither invents learning nor starts guidance.
                    candidate.tutorial = ShopTutorial.LegacyUnguided().CaptureSave();
                }
                else if (!ShopTutorial.TryRestore(candidate.tutorial, restoredGame, candidate.cycle.dayNumber, out _))
                {
                    error = "Saved tutorial progress could not be validated. Your original file is preserved.";
                    return false;
                }

                if (header.formatVersion < 21)
                {
                    // Ignore injected future fields. An established shop never receives free modules.
                    candidate.automation = new ShopAutomation().CaptureSave();
                }
                else if (!ShopAutomation.TryRestore(candidate.automation, restoredGame, out _))
                {
                    error = "Saved culture preparation could not be validated. Your original file is preserved.";
                    return false;
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

                if (header.formatVersion < 22) candidate.ambienceVolume = FxAudioMix.DefaultAmbienceVolume;
                else if (!ValidHumorVolume(candidate.ambienceVolume))
                {
                    error = "Saved machine/ambience volume could not be validated. Your original file is preserved.";
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

        private static bool ValidOwnedConditionerProgress(ShopLayout layout, GameModel game)
        {
            // Stored furniture has no active machine registry but remains purchased ownership.
            // Both placed and stored vats require the progression used by their purchase gate.
            foreach (var item in layout.Items)
                if (item.DefinitionId == ShopLayout.ConditionerDefinitionId &&
                    (!game.SparkResearchUnlocked || !game.IsDiscovered("lanternfin"))) return false;
            return true;
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
            readonly bool requireStorage;
            readonly bool requireResearch;
            readonly bool requireConditioners;
            readonly bool requireMist;
            readonly bool requireStaff;
            readonly bool requireStocker;
            readonly bool requireTechnician;
            readonly bool requireTutorial;
            readonly bool requireAutomation;
            readonly bool requireAmbience;
            int position;
            public LayoutJsonShape(string json, bool requireStoreTier, bool requireStored, bool requireLab, bool requireStorage = false, bool requireResearch = false, bool requireConditioners = false, bool requireMist = false, bool requireStaff = false, bool requireStocker = false, bool requireTechnician = false, bool requireTutorial = false, bool requireAutomation = false, bool requireAmbience = false)
            { this.json = json; this.requireStoreTier = requireStoreTier; this.requireStored = requireStored; this.requireLab = requireLab; this.requireStorage = requireStorage; this.requireResearch = requireResearch; this.requireConditioners = requireConditioners; this.requireMist = requireMist; this.requireStaff = requireStaff; this.requireStocker = requireStocker; this.requireTechnician = requireTechnician; this.requireTutorial = requireTutorial; this.requireAutomation = requireAutomation; this.requireAmbience = requireAmbience; }

            public bool HasExplicitItemsArray()
            {
                if (json == null || !ReadObject(false)) return false;
                SkipSpace(); return position == json.Length;
            }

            bool ReadObject(bool layout)
            {
                if (!Take('{')) return false;
                bool required = false, hasStoreTier = false, hasLabTier = false, hasFacilities = false, hasStorage = false, hasStaff = false, hasStocker = false, hasTechnician = false, hasTutorial = false, hasAutomation = false, hasAmbience = false;
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
                    else if (!layout && requireStorage && key == "game")
                    {
                        if (!ReadGame()) return false;
                        hasStorage = true;
                    }
                    else if (!layout && requireStaff && key == "staff")
                    {
                        if (!ReadStaff()) return false;
                        hasStaff = true;
                    }
                    else if (!layout && requireStocker && key == "stocker")
                    {
                        if (!ReadStocker()) return false;
                        hasStocker = true;
                    }
                    else if (!layout && requireTechnician && key == "technician")
                    {
                        if (!ReadTechnician()) return false;
                        hasTechnician = true;
                    }
                    else if (!layout && requireTutorial && key == "tutorial")
                    {
                        if (!ReadTutorial()) return false;
                        hasTutorial = true;
                    }
                    else if (!layout && requireAutomation && key == "automation")
                    {
                        if (!ReadAutomation()) return false;
                        hasAutomation = true;
                    }
                    else if (!layout && requireAmbience && key == "ambienceVolume")
                    {
                        if (!ReadFiniteNumber()) return false;
                        hasAmbience = true;
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
                    if (Take('}')) return required && (layout ?
                        ((!requireStoreTier || hasStoreTier) && (!requireLab || hasLabTier && hasFacilities)) :
                        (!requireStorage || hasStorage) && (!requireStaff || hasStaff) && (!requireStocker || hasStocker) &&
                        (!requireTechnician || hasTechnician) && (!requireTutorial || hasTutorial) && (!requireAutomation || hasAutomation) && (!requireAmbience || hasAmbience));
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadAutomation()
            {
                if (!Take('{')) return false;
                int required = 0;
                int version = 0, extended = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    switch (key)
                    {
                        case "version": if (!ReadInteger(out version)) return false; required |= 1; break;
                        case "collectorOwned": if (!ReadBoolean()) return false; required |= 2; break;
                        case "collectorEnabled": if (!ReadBoolean()) return false; required |= 4; break;
                        case "doserOwned": if (!ReadBoolean()) return false; required |= 8; break;
                        case "doserEnabled": if (!ReadBoolean()) return false; required |= 16; break;
                        case "recipeId": if (!ReadNullableString()) return false; required |= 32; break;
                        case "requestId": if (!ReadNullableString()) return false; required |= 64; break;
                        case "maintainEnabled": if (!ReadBoolean()) return false; extended |= 1; break;
                        case "paused": if (!ReadBoolean()) return false; extended |= 2; break;
                        case "desiredStockCount": if (!ReadInteger(out _)) return false; extended |= 4; break;
                        case "destinationTankId": if (!ReadNullableString()) return false; extended |= 8; break;
                        case "policyHandoffWaiting": if (!ReadBoolean()) return false; extended |= 16; break;
                        case "policyQueueCountAtRequest": if (!ReadInteger(out _)) return false; extended |= 32; break;
                        default: if (!SkipValue(0)) return false; break;
                    }
                    if (Take('}')) return required == 127 && (version == 1 || version == 2 && extended == 63);
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadTutorial()
            {
                if (!Take('{')) return false;
                int required = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    switch (key)
                    {
                        case "version": if (!ReadInteger(out _)) return false; required |= 1; break;
                        case "mode": if (!ReadInteger(out _)) return false; required |= 2; break;
                        case "facts": if (!ReadInteger(out _)) return false; required |= 4; break;
                        case "firstSaleAccommodationEnded": if (!ReadBoolean()) return false; required |= 8; break;
                        case "practiceAssisted": if (!ReadBoolean()) return false; required |= 16; break;
                        case "reportAcknowledged": if (!ReadBoolean()) return false; required |= 32; break;
                        case "handoffCompleted": if (!ReadBoolean()) return false; required |= 64; break;
                        case "reportDay": if (!ReadInteger(out _)) return false; required |= 128; break;
                        case "practiceBlockedSpecimenId": if (!ReadNullableString()) return false; required |= 256; break;
                        case "first": if (!ReadTutorialChain()) return false; required |= 512; break;
                        case "practice": if (!ReadTutorialChain()) return false; required |= 1024; break;
                        default: if (!SkipValue(0)) return false; break;
                    }
                    if (Take('}')) return required == 2047;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadTutorialChain()
            {
                if (!Take('{')) return false;
                int required = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    switch (key)
                    {
                        case "specimenId": if (!ReadNullableString()) return false; required |= 1; break;
                        case "speciesId": if (!ReadNullableString()) return false; required |= 2; break;
                        case "ready": if (!ReadBoolean()) return false; required |= 4; break;
                        case "manuallyDisplayed": if (!ReadBoolean()) return false; required |= 8; break;
                        case "saleDay": if (!ReadInteger(out _)) return false; required |= 16; break;
                        default: if (!SkipValue(0)) return false; break;
                    }
                    if (Take('}')) return required == 31;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadTechnician()
            {
                if (!Take('{')) return false;
                int required = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    switch (key)
                    {
                        case "version": if (!ReadInteger(out _)) return false; required |= 1; break;
                        case "employee": if (!ReadStaff()) return false; required |= 2; break;
                        case "queue": if (!ReadLabOrders()) return false; required |= 4; break;
                        case "activeSpeciesId": if (!ReadNullableString()) return false; required |= 8; break;
                        case "activeSpecimenId": if (!ReadNullableString()) return false; required |= 16; break;
                        default: if (!SkipValue(0)) return false; break;
                    }
                    if (Take('}')) return required == 31;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadLabOrders()
            {
                if (!Take('[')) return false;
                if (Take(']')) return true;
                while (position < json.Length)
                {
                    if (!Take('{')) return false;
                    int required = 0;
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    while (position < json.Length)
                    {
                        if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                        if (key == "speciesId") { if (!ReadString(true, out _)) return false; required |= 1; }
                        else if (key == "count") { if (!ReadInteger(out _)) return false; required |= 2; }
                        else if (!SkipValue(0)) return false;
                        if (Take('}')) break;
                        if (!Take(',')) return false;
                    }
                    if (required != 3) return false;
                    if (Take(']')) return true;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadNullableString()
            {
                SkipSpace();
                if (position < json.Length && json[position] == '"') return ReadString(false, out _);
                if (position + 4 > json.Length || string.CompareOrdinal(json, position, "null", 0, 4) != 0) return false;
                position += 4;
                return position >= json.Length || char.IsWhiteSpace(json[position]) || json[position] == ',' || json[position] == '}';
            }

            bool ReadStocker()
            {
                if (!Take('{')) return false;
                int required = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    switch (key)
                    {
                        case "version": if (!ReadInteger(out _)) return false; required |= 1; break;
                        case "employee": if (!ReadStaff()) return false; required |= 2; break;
                        case "targets": if (!ReadStockerTargets()) return false; required |= 4; break;
                        default: if (!SkipValue(0)) return false; break;
                    }
                    if (Take('}')) return required == 7;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadStockerTargets()
            {
                if (!Take('[')) return false;
                if (Take(']')) return true;
                while (position < json.Length)
                {
                    if (!Take('{')) return false;
                    int required = 0;
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    while (position < json.Length)
                    {
                        if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                        if (key == "tankId") { if (!ReadString(true, out _)) return false; required |= 1; }
                        else if (key == "speciesId") { if (!ReadString(true, out _)) return false; required |= 2; }
                        else if (!SkipValue(0)) return false;
                        if (Take('}')) break;
                        if (!Take(',')) return false;
                    }
                    if (required != 3) return false;
                    if (Take(']')) return true;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadStaff()
            {
                if (!Take('{')) return false;
                int required = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    switch (key)
                    {
                        case "version": if (!ReadInteger(out _)) return false; required |= 1; break;
                        case "employeeId": if (!ReadString(true, out _)) return false; required |= 2; break;
                        case "hired": if (!ReadBoolean()) return false; required |= 4; break;
                        case "scheduled": if (!ReadBoolean()) return false; required |= 8; break;
                        case "paused": if (!ReadBoolean()) return false; required |= 16; break;
                        case "paidDay": if (!ReadInteger(out _)) return false; required |= 32; break;
                        case "paidWage": if (!ReadInteger(out _)) return false; required |= 64; break;
                        case "previousPaidDay": if (!ReadInteger(out _)) return false; required |= 128; break;
                        case "previousPaidWage": if (!ReadInteger(out _)) return false; required |= 256; break;
                        default: if (!SkipValue(0)) return false; break;
                    }
                    if (Take('}')) return required == 511;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadGame()
            {
                if (!Take('{')) return false;
                bool hasStorage = false, hasConditioners = false;
                int researchFields = 0, mistFields = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    if (key == "storage")
                    { if (!ReadStorage()) return false; hasStorage = true; }
                    else if (requireResearch && key == "sparkResearchUnlocked")
                    { if (!ReadBoolean()) return false; researchFields |= 1; }
                    else if (requireResearch && key == "availableSpark")
                    { if (!ReadInteger(out _)) return false; researchFields |= 2; }
                    else if (requireMist && key == "mistResearchUnlocked")
                    { if (!ReadBoolean()) return false; mistFields |= 1; }
                    else if (requireMist && key == "availableMist")
                    { if (!ReadInteger(out _)) return false; mistFields |= 2; }
                    else if (requireResearch && key == "growers")
                    { if (!ReadGrowers()) return false; researchFields |= 4; }
                    else if (requireResearch && key == "incubator")
                    { if (!ReadIncubator()) return false; researchFields |= 8; }
                    else if (requireConditioners && key == "conditioners")
                    { if (!ReadConditioners()) return false; hasConditioners = true; }
                    else if (!SkipValue(0)) return false;
                    if (Take('}')) return hasStorage && (!requireResearch || researchFields == 15) &&
                        (!requireConditioners || hasConditioners) && (!requireMist || mistFields == 3);
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadConditioners()
            {
                if (!Take('[')) return false;
                if (Take(']')) return true;
                int count = 0;
                while (position < json.Length)
                {
                    if (++count > 1 || !Take('{')) return false;
                    int required = 0;
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    while (position < json.Length)
                    {
                        if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                        if (key == "conditionerId")
                        { if (!ReadString(true, out string id) || string.IsNullOrWhiteSpace(id)) return false; required |= 1; }
                        else if (key == "phase")
                        { if (!ReadInteger(out _)) return false; required |= 2; }
                        else if (key == "secondsRemaining")
                        { if (!ReadFiniteNumber()) return false; required |= 4; }
                        else if (key == "firstSpecimenId" || key == "firstSpeciesId" || key == "secondSpecimenId" || key == "secondSpeciesId")
                        {
                            SkipSpace();
                            if (position < json.Length && json[position] == '"')
                            { if (!ReadString(false, out _)) return false; }
                            else
                            {
                                if (position + 4 > json.Length || string.CompareOrdinal(json, position, "null", 0, 4) != 0) return false;
                                position += 4;
                            }
                            required |= key == "firstSpecimenId" ? 8 : key == "firstSpeciesId" ? 16 : key == "secondSpecimenId" ? 32 : 64;
                        }
                        else if (!SkipValue(0)) return false;
                        if (Take('}')) break;
                        if (!Take(',')) return false;
                    }
                    if (required != 127) return false;
                    if (Take(']')) return true;
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadIncubator()
            {
                if (!Take('{')) return false;
                bool hasSpark = false, hasMist = false;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (position < json.Length)
                {
                    if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                    if (key == "stagedSpark") { if (!ReadInteger(out _)) return false; hasSpark = true; }
                    else if (requireMist && key == "stagedMist") { if (!ReadInteger(out _)) return false; hasMist = true; }
                    else if (!SkipValue(0)) return false;
                    if (Take('}')) return hasSpark && (!requireMist || hasMist);
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadGrowers()
            {
                if (!Take('[') || Take(']')) return false;
                int count = 0;
                while (position < json.Length)
                {
                    if (++count > (requireMist ? 4 : 3) || !Take('{')) return false;
                    int required = 0;
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    while (position < json.Length)
                    {
                        if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                        if (key == "cultureId") { if (!ReadString(true, out _)) return false; required |= 1; }
                        else if (key == "units") { if (!ReadInteger(out _)) return false; required |= 2; }
                        else if (key == "secondsUntilNextUnit") { if (!ReadFiniteNumber()) return false; required |= 4; }
                        else if (!SkipValue(0)) return false;
                        if (Take('}')) break;
                        if (!Take(',')) return false;
                    }
                    if (required != 7) return false;
                    if (Take(']')) return count == (requireMist ? 4 : 3);
                    if (!Take(',')) return false;
                }
                return false;
            }

            bool ReadBoolean()
            {
                SkipSpace();
                int length = position + 4 <= json.Length && string.CompareOrdinal(json, position, "true", 0, 4) == 0 ? 4 :
                    position + 5 <= json.Length && string.CompareOrdinal(json, position, "false", 0, 5) == 0 ? 5 : 0;
                if (length == 0) return false;
                position += length;
                return position >= json.Length || char.IsWhiteSpace(json[position]) || json[position] == ',' || json[position] == '}';
            }

            bool ReadFiniteNumber()
            {
                SkipSpace(); int began = position;
                if (position < json.Length && json[position] == '-') position++;
                if (position >= json.Length || json[position] < '0' || json[position] > '9') return false;
                if (json[position] == '0') position++;
                else while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++;
                if (position < json.Length && json[position] == '.')
                {
                    position++; int fraction = position;
                    while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++;
                    if (position == fraction) return false;
                }
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++;
                    int exponent = position;
                    while (position < json.Length && json[position] >= '0' && json[position] <= '9') position++;
                    if (position == exponent) return false;
                }
                if (position < json.Length && !char.IsWhiteSpace(json[position]) && json[position] != ',' && json[position] != '}') return false;
                return float.TryParse(json.Substring(began, position - began), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value) && Finite(value);
            }

            bool ReadStorage()
            {
                if (!Take('[')) return false;
                if (Take(']')) return true;
                while (position < json.Length)
                {
                    if (!Take('{')) return false;
                    int required = 0;
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    while (position < json.Length)
                    {
                        if (!ReadString(true, out string key) || !keys.Add(key) || !Take(':')) return false;
                        if (key == "storageId")
                        { if (!ReadString(true, out string id) || string.IsNullOrWhiteSpace(id)) return false; required |= 1; }
                        else if (key == "specimenId" || key == "speciesId")
                        {
                            SkipSpace();
                            if (position < json.Length && json[position] == '"')
                            { if (!ReadString(false, out _)) return false; }
                            else
                            {
                                if (position + 4 > json.Length || string.CompareOrdinal(json, position, "null", 0, 4) != 0) return false;
                                position += 4;
                            }
                            required |= key == "specimenId" ? 2 : 4;
                        }
                        else if (!SkipValue(0)) return false;
                        if (Take('}')) break;
                        if (!Take(',')) return false;
                    }
                    if (required != 7) return false;
                    if (Take(']')) return true;
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
