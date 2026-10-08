using System;

namespace EvolutionShop
{
    public enum TutorialMode { Offered, Active, Completed, Skipped, LegacyUnguided }
    public enum TutorialActionOrigin { Player, Worker }
    public enum TutorialLesson { None, Orient, Collect, Prepare, Grow, Display, PriceAndOpen, Serve, Practice, CloseAndReview, NextPlan }
    [Flags]
    public enum TutorialFacts
    {
        None = 0, Moved = 1, Looked = 2, LabVisited = 4, BloomCollected = 8, FlowCollected = 16,
        RecipePrepared = 32, CreatureReady = 64, CreatureDisplayed = 128, PriceViewed = 256, ShopOpened = 512
    }

    [Serializable]
    public sealed class TutorialChainSave
    {
        public string specimenId;
        public string speciesId;
        public bool ready;
        public bool manuallyDisplayed;
        public int saleDay;
    }

    [Serializable]
    public sealed class TutorialSave
    {
        public int version = ShopTutorial.SaveVersion;
        public TutorialMode mode;
        public TutorialFacts facts;
        public bool firstSaleAccommodationEnded;
        public bool practiceAssisted;
        public bool reportAcknowledged;
        public bool handoffCompleted;
        public int reportDay;
        public string practiceBlockedSpecimenId;
        public TutorialChainSave first = new TutorialChainSave();
        public TutorialChainSave practice = new TutorialChainSave();
    }

    /// <summary>Bounded learning receipts. Observations never execute gameplay or grant rewards.</summary>
    public sealed class ShopTutorial
    {
        public const int SaveVersion = 1;
        const TutorialFacts Orientation = TutorialFacts.Moved | TutorialFacts.Looked | TutorialFacts.LabVisited;
        const TutorialFacts Collection = TutorialFacts.BloomCollected | TutorialFacts.FlowCollected;
        const TutorialFacts AllFacts = Orientation | Collection | TutorialFacts.RecipePrepared | TutorialFacts.CreatureReady |
            TutorialFacts.CreatureDisplayed | TutorialFacts.PriceViewed | TutorialFacts.ShopOpened;
        TutorialSave state = new TutorialSave();
        public TutorialMode Mode => state.mode;
        public TutorialFacts Facts => state.facts;
        // Read-only identity for transient guidance; no DTO cloning in the HUD.
        public string FirstSpecimenId => state.first.specimenId;
        public bool PracticeAssisted => state.practiceAssisted;
        public bool FirstSaleAccommodationActive => Mode == TutorialMode.Active && !state.firstSaleAccommodationEnded;

        public static ShopTutorial LegacyUnguided() => new ShopTutorial { state = new TutorialSave { mode = TutorialMode.LegacyUnguided } };
        public bool Start()
        {
            if (Mode != TutorialMode.Offered) return false;
            state.mode = TutorialMode.Active; return true;
        }
        public bool Skip()
        {
            if (Mode != TutorialMode.Offered && Mode != TutorialMode.Active) return false;
            state.mode = TutorialMode.Skipped;
            // Unguided future gameplay must not leave a live tutorial ownership dependency behind.
            if (state.first.saleDay == 0) state.first = new TutorialChainSave();
            if (state.practice.saleDay == 0) state.practice = new TutorialChainSave();
            state.practiceBlockedSpecimenId = null;
            return true;
        }
        public bool MarkPracticeAssisted()
        {
            if (Mode != TutorialMode.Active || state.first.saleDay == 0 || state.practice.saleDay > 0 || state.practiceAssisted) return false;
            state.practiceAssisted = true; return true;
        }
        public bool Finish(ShopDayCycle cycle)
        {
            if (CurrentLesson(cycle) != TutorialLesson.NextPlan) return false;
            state.handoffCompleted = true; state.mode = TutorialMode.Completed; return true;
        }

        public TutorialLesson CurrentLesson(ShopDayCycle cycle)
        {
            if (Mode != TutorialMode.Active || cycle == null) return TutorialLesson.None;
            if (!Has(Orientation)) return TutorialLesson.Orient;
            if (!Has(Collection)) return TutorialLesson.Collect;
            if (!Has(TutorialFacts.RecipePrepared)) return TutorialLesson.Prepare;
            if (state.first.saleDay == 0)
            {
                if (Empty(state.first) || !state.first.ready) return TutorialLesson.Grow;
                if (!state.first.manuallyDisplayed) return TutorialLesson.Display;
                if (!Has(TutorialFacts.ShopOpened)) return TutorialLesson.PriceAndOpen;
                return TutorialLesson.Serve;
            }
            if (state.practice.saleDay == 0) return TutorialLesson.Practice;
            if (!state.reportAcknowledged || cycle.Phase != ShopDayPhase.Night || cycle.DayNumber <= state.practice.saleDay)
                return TutorialLesson.CloseAndReview;
            return TutorialLesson.NextPlan;
        }

        public bool ObserveOrientation(bool moved, bool looked, bool inLab)
        {
            if (Mode != TutorialMode.Active) return false;
            return Add((moved ? TutorialFacts.Moved : 0) | (looked ? TutorialFacts.Looked : 0) | (inLab ? TutorialFacts.LabVisited : 0));
        }
        public bool ObserveCollection(GameModel game, CultureKind culture, ActionResult result, TutorialActionOrigin origin)
        {
            if (!Manual(origin) || game == null || !result.Success || (culture != CultureKind.Bloom && culture != CultureKind.Flow) ||
                game.Available(culture) + game.Staged(culture) < 1) return false;
            return Add(culture == CultureKind.Bloom ? TutorialFacts.BloomCollected : TutorialFacts.FlowCollected);
        }
        public bool ObservePrepared(GameModel game, string speciesId, TutorialActionOrigin origin)
        {
            if (!Manual(origin) || game == null || game.Incubator.Phase != IncubatorPhase.Dosing) return false;
            var recipe = game.GetSpecies(speciesId); var inc = game.Incubator;
            if (recipe == null || recipe.Bloom != inc.Bloom || recipe.Flow != inc.Flow || recipe.Spark != inc.Spark || recipe.Mist != inc.Mist ||
                recipe.Spark > 0 && !game.SparkResearchUnlocked || recipe.Mist > 0 && !game.MistResearchUnlocked) return false;
            bool changed = Add(TutorialFacts.RecipePrepared);
            return ObserveInventory(game) | changed;
        }
        public bool ObserveStarted(GameModel game, ActionResult result, TutorialActionOrigin origin)
        {
            if (!Manual(origin) || game == null || !ValidResult(result) || game.Incubator.Phase != IncubatorPhase.Incubating ||
                game.Incubator.SpecimenId != result.SpecimenId || game.Incubator.SpeciesId != result.SpeciesId ||
                result.SpecimenId == state.first.specimenId || result.SpecimenId == state.practice.specimenId ||
                result.SpecimenId == state.practiceBlockedSpecimenId) return false;
            var recipe = game.GetSpecies(result.SpeciesId);
            if (recipe == null) return false;
            // A new start is accepted only when the relevant chain has no still-owned candidate.
            bool changed = Reconcile(game);
            var chain = state.first.saleDay == 0 ? state.first : state.practice;
            if (!Empty(chain)) return changed;
            chain.specimenId = result.SpecimenId; chain.speciesId = result.SpeciesId;
            if (state.first.saleDay > 0) state.practiceBlockedSpecimenId = null;
            Add(TutorialFacts.RecipePrepared | (recipe.Bloom > 0 ? TutorialFacts.BloomCollected : 0) |
                (recipe.Flow > 0 ? TutorialFacts.FlowCollected : 0));
            return true;
        }
        public bool ObserveReady(GameModel game)
        {
            if (Mode != TutorialMode.Active || game == null || game.Incubator.Phase != IncubatorPhase.Ready) return false;
            var chain = Matching(game.Incubator.SpecimenId, game.Incubator.SpeciesId);
            if (chain == null || chain.saleDay > 0) return false;
            bool changed = !chain.ready; chain.ready = true;
            return Add(TutorialFacts.CreatureReady) | changed;
        }
        public bool ObserveDisplayed(GameModel game, ActionResult result, TutorialActionOrigin origin)
        {
            if (Mode != TutorialMode.Active || game == null || !ValidResult(result) || !ValidOrigin(origin) ||
                !InDisplay(game, result.SpecimenId, result.SpeciesId)) return false;
            var chain = Matching(result.SpecimenId, result.SpeciesId);
            if (chain == null || chain.saleDay > 0) return false;
            if (origin == TutorialActionOrigin.Worker) { Retire(chain); return true; }
            bool changed = !chain.ready || !chain.manuallyDisplayed;
            chain.ready = chain.manuallyDisplayed = true;
            return Add(TutorialFacts.CreatureReady | TutorialFacts.CreatureDisplayed) | changed;
        }
        public bool ObservePriceViewed() => Mode == TutorialMode.Active && Add(TutorialFacts.PriceViewed);
        public bool ObserveOpened(bool success) => Mode == TutorialMode.Active && success && Add(TutorialFacts.ShopOpened);
        public bool ObserveSale(GameModel game, ActionResult result, TutorialActionOrigin origin, int day)
        {
            if (Mode != TutorialMode.Active || game == null || !ValidResult(result) || !ValidOrigin(origin) || day < 1 ||
                Exists(game, result.SpecimenId, null) || game.GetSpecies(result.SpeciesId) == null ||
                state.first.saleDay > 0 && result.SpecimenId == state.first.specimenId ||
                state.practice.saleDay > 0 && result.SpecimenId == state.practice.specimenId) return false;
            bool changed = !state.firstSaleAccommodationEnded;
            state.firstSaleAccommodationEnded = true;
            var chain = Matching(result.SpecimenId, result.SpeciesId);
            if (chain == null) return changed;
            if (origin != TutorialActionOrigin.Player || !chain.ready || !chain.manuallyDisplayed)
            { Retire(chain); return true; }
            if (chain == state.practice && day < state.first.saleDay) return changed;
            chain.saleDay = day;
            Add(TutorialFacts.ShopOpened);
            if (chain == state.first)
            {
                // This exact batch existed before practice became eligible. Replayed start notifications cannot qualify it.
                state.practiceBlockedSpecimenId = game.Incubator.SpecimenId;
            }
            return true;
        }
        public bool ObserveReportAcknowledged(int day)
        {
            if (Mode != TutorialMode.Active || state.reportAcknowledged || day < 1 ||
                day != state.first.saleDay && day != state.practice.saleDay) return false;
            state.reportAcknowledged = true; state.reportDay = day; return true;
        }

        /// <summary>Call after authoritative actions and before a combined save; never infers manual transfers or sales.</summary>
        public bool Reconcile(GameModel game)
        {
            if (Mode != TutorialMode.Active || game == null) return false;
            bool changed = ObserveInventory(game);
            foreach (var chain in new[] { state.first, state.practice })
            {
                if (Empty(chain) || chain.saleDay > 0) continue;
                if (!Exists(game, chain.specimenId, chain.speciesId)) { Retire(chain); changed = true; }
            }
            if (!string.IsNullOrEmpty(state.practiceBlockedSpecimenId) && !Exists(game, state.practiceBlockedSpecimenId, null))
            { state.practiceBlockedSpecimenId = null; changed = true; }
            return ObserveReady(game) | changed;
        }

        public TutorialSave CaptureSave() => Copy(state);
        public static bool TryRestore(TutorialSave save, GameModel game, int currentDay, out ShopTutorial tutorial)
        {
            tutorial = null;
            if (save == null || save.version != SaveVersion || game == null || currentDay < 1 ||
                !Enum.IsDefined(typeof(TutorialMode), save.mode) || (save.facts & ~AllFacts) != 0 ||
                !ValidChain(save.first, game, currentDay, save.mode) || !ValidChain(save.practice, game, currentDay, save.mode) ||
                !NullableId(save.practiceBlockedSpecimenId) || save.reportDay < 0 || save.reportDay >= currentDay && save.reportDay != 0) return false;
            bool firstSold = save.first.saleDay > 0, practiceSold = save.practice.saleDay > 0;
            if ((save.facts & TutorialFacts.CreatureReady) != 0 && (save.facts & TutorialFacts.RecipePrepared) == 0 ||
                (save.facts & TutorialFacts.CreatureDisplayed) != 0 && (save.facts & TutorialFacts.CreatureReady) == 0) return false;
            if (!Empty(save.first) && save.first.specimenId == save.practice.specimenId ||
                !Empty(save.practice) && !firstSold || practiceSold && save.practice.saleDay < save.first.saleDay ||
                save.practiceAssisted && !firstSold || (firstSold || practiceSold) && !save.firstSaleAccommodationEnded ||
                save.reportAcknowledged != (save.reportDay > 0) || save.reportAcknowledged &&
                save.reportDay != save.first.saleDay && save.reportDay != save.practice.saleDay ||
                save.handoffCompleted != (save.mode == TutorialMode.Completed)) return false;
            bool blocked = !string.IsNullOrEmpty(save.practiceBlockedSpecimenId);
            if (blocked && (save.mode != TutorialMode.Active || !firstSold || !Empty(save.practice) ||
                save.practiceBlockedSpecimenId == save.first.specimenId || !Exists(game, save.practiceBlockedSpecimenId, null))) return false;
            if (save.mode == TutorialMode.Offered || save.mode == TutorialMode.LegacyUnguided)
            {
                if (save.facts != 0 || save.firstSaleAccommodationEnded || save.practiceAssisted || save.reportAcknowledged ||
                    !Empty(save.first) || !Empty(save.practice) || blocked) return false;
            }
            if (save.mode == TutorialMode.Completed && (!firstSold || !practiceSold || !save.reportAcknowledged ||
                currentDay <= save.practice.saleDay || (save.facts & (Orientation | Collection)) != (Orientation | Collection))) return false;
            if ((!Empty(save.first) || !Empty(save.practice)) && (save.facts & TutorialFacts.RecipePrepared) == 0 ||
                (save.first.ready || save.practice.ready) && (save.facts & TutorialFacts.CreatureReady) == 0 ||
                (save.first.manuallyDisplayed || save.practice.manuallyDisplayed) && (save.facts & TutorialFacts.CreatureDisplayed) == 0 ||
                firstSold && (save.facts & TutorialFacts.ShopOpened) == 0) return false;
            tutorial = new ShopTutorial { state = Copy(save) }; return true;
        }

        bool Manual(TutorialActionOrigin origin) => Mode == TutorialMode.Active && origin == TutorialActionOrigin.Player;
        bool Has(TutorialFacts facts) => (state.facts & facts) == facts;
        bool Add(TutorialFacts facts) { var before = state.facts; state.facts |= facts; return before != state.facts; }
        bool ObserveInventory(GameModel game) => Add((game.Available(CultureKind.Bloom) + game.Staged(CultureKind.Bloom) > 0 ? TutorialFacts.BloomCollected : 0) |
            (game.Available(CultureKind.Flow) + game.Staged(CultureKind.Flow) > 0 ? TutorialFacts.FlowCollected : 0));
        TutorialChainSave Matching(string id, string species) => !string.IsNullOrEmpty(id) && state.first.specimenId == id && state.first.speciesId == species ? state.first :
            !string.IsNullOrEmpty(id) && state.practice.specimenId == id && state.practice.speciesId == species ? state.practice : null;
        void Retire(TutorialChainSave chain) { if (chain == state.first) state.first = new TutorialChainSave(); else state.practice = new TutorialChainSave(); }
        static bool Empty(TutorialChainSave chain) => string.IsNullOrEmpty(chain.specimenId);
        static bool ValidOrigin(TutorialActionOrigin origin) => origin == TutorialActionOrigin.Player || origin == TutorialActionOrigin.Worker;
        static bool ValidResult(ActionResult result) => result.Success && Id(result.SpecimenId) && Id(result.SpeciesId);
        static bool Id(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Trim() != id) return false;
            foreach (char c in id) if (char.IsControl(c)) return false;
            return true;
        }
        static bool NullableId(string id) => string.IsNullOrEmpty(id) || Id(id);
        static bool ValidChain(TutorialChainSave chain, GameModel game, int currentDay, TutorialMode mode)
        {
            if (chain == null || !NullableId(chain.specimenId) || !NullableId(chain.speciesId) || chain.saleDay < 0 || chain.saleDay > currentDay) return false;
            if (Empty(chain)) return string.IsNullOrEmpty(chain.speciesId) && !chain.ready && !chain.manuallyDisplayed && chain.saleDay == 0;
            if (!Id(chain.speciesId) || game.GetSpecies(chain.speciesId) == null || chain.manuallyDisplayed && !chain.ready ||
                chain.saleDay > 0 && (!chain.ready || !chain.manuallyDisplayed)) return false;
            if (chain.saleDay > 0) return !Exists(game, chain.specimenId, null);
            if (game.Incubator.SpecimenId == chain.specimenId &&
                (chain.manuallyDisplayed || chain.ready && game.Incubator.Phase == IncubatorPhase.Incubating)) return false;
            return mode == TutorialMode.Active && Exists(game, chain.specimenId, chain.speciesId);
        }
        static bool InDisplay(GameModel game, string id, string species)
        {
            foreach (string tankId in game.TankIds) if (Matches(game.GetTank(tankId), id, species)) return true;
            return false;
        }
        static bool Exists(GameModel game, string id, string species)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (game.Incubator.SpecimenId == id && (species == null || game.Incubator.SpeciesId == species)) return true;
            if (InDisplay(game, id, species)) return true;
            foreach (string storage in game.StorageIds) if (Matches(game.GetStorage(storage), id, species)) return true;
            foreach (string machine in game.ConditionerIds)
            {
                var view = game.GetConditioner(machine);
                if (view.HasValue && (Matches(view.Value.First, id, species) || Matches(view.Value.Second, id, species))) return true;
            }
            return false;
        }
        static bool Matches(SpecimenView? value, string id, string species) => value.HasValue && value.Value.Id == id && (species == null || value.Value.SpeciesId == species);
        static TutorialChainSave Copy(TutorialChainSave chain) => new TutorialChainSave
        { specimenId = chain.specimenId, speciesId = chain.speciesId, ready = chain.ready, manuallyDisplayed = chain.manuallyDisplayed, saleDay = chain.saleDay };
        static TutorialSave Copy(TutorialSave save) => new TutorialSave
        {
            version = save.version, mode = save.mode, facts = save.facts, firstSaleAccommodationEnded = save.firstSaleAccommodationEnded,
            practiceAssisted = save.practiceAssisted, reportAcknowledged = save.reportAcknowledged, handoffCompleted = save.handoffCompleted,
            reportDay = save.reportDay, practiceBlockedSpecimenId = save.practiceBlockedSpecimenId, first = Copy(save.first), practice = Copy(save.practice)
        };
    }
}
