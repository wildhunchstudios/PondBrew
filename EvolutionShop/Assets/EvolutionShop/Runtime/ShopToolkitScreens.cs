using UnityEngine;

namespace EvolutionShop
{
    // UX-07: what the welcome, help, journal, day report and pause screens read, and the commands they call. Every
    // command is the session's existing one (the tutorial's, ReloadSaved, ConfirmNewGame, the settings), with its own
    // checks and saves. Nothing here changes a rule, a lesson or the save.
    public sealed partial class ShopSession
    {
        /// <summary>Panels the UX-07 screens draw: welcome, help, journal, day report, and pause with its New game step.</summary>
        public static bool IsScreenPanel(ShopPanel panel) =>
            panel == ShopPanel.Welcome || panel == ShopPanel.TutorialHelp || panel == ShopPanel.Journal ||
            panel == ShopPanel.DayReport || panel == ShopPanel.Pause || panel == ShopPanel.Reset;

        bool ToolkitScreen => ToolkitOwnsPanel(Panel) && IsScreenPanel(Panel);

        // From BackFromPanel: on the toolkit screens, Esc in the New game step goes back to Pause rather than to play.
        bool ScreenBack()
        {
            if (!ToolkitScreen || Panel != ShopPanel.Reset) return false;
            SetPanel(ShopPanel.Pause);
            return true;
        }

        /// <summary>The current notice for a screen's footer: the save error, else a message raised since <paramref name="since"/>.</summary>
        public string ScreenNotice(out bool error, float since) => PlanningNotice(out error, since);

        // ---------- welcome ----------

        public void FillWelcome(WelcomeState s)
        {
            s.Mode = Tutorial.Mode;
            s.AutosaveBlocked = AutosaveBlocked;
            s.SaveError = !string.IsNullOrEmpty(SaveError);
            s.Lesson = ActiveTutorialLesson;
            s.LessonNumber = ScreenRules.LessonNumber(s.Lesson);
        }

        /// <summary>Welcome's way to Pause, for save recovery.</summary>
        public bool ScreenSaveRecovery()
        {
            if (Panel != ShopPanel.Welcome) return false;
            SetPanel(ShopPanel.Pause); return true;
        }

        /// <summary>Welcome's "Explore without saving" while the save is protected.</summary>
        public bool ScreenExplore()
        {
            if (Panel != ShopPanel.Welcome || !AutosaveBlocked) return false;
            SetPanel(ShopPanel.None); return true;
        }

        /// <summary>Welcome's "Continue my shop" outside the guided opening.</summary>
        public bool ScreenContinueShop()
        {
            if (Panel != ShopPanel.Welcome || Tutorial.Mode == TutorialMode.Offered || Tutorial.Mode == TutorialMode.Active) return false;
            SetPanel(ShopPanel.None); return true;
        }

        // ---------- help ----------

        public void FillHelp(HelpState s, bool controls)
        {
            s.TutorialActive = Tutorial.Mode == TutorialMode.Active;
            s.Paused = Paused;
            int page = Mathf.Clamp((int)TutorialHelpLesson, (int)TutorialLesson.Orient, (int)TutorialLesson.NextPlan);
            s.Lesson = (TutorialLesson)page;
            s.ActiveLesson = ActiveTutorialLesson;
            s.OnActiveLesson = s.TutorialActive && s.Lesson == s.ActiveLesson;
            if (controls) s.Page = HelpPage.Controls;
            else if (TutorialUpgradeHelpOpen) s.Page = HelpPage.Upgrade;
            else if (s.TutorialActive && s.ActiveLesson == TutorialLesson.NextPlan && s.Lesson == TutorialLesson.NextPlan) s.Page = HelpPage.NextPlan;
            else if (TutorialNeedsReportRecovery && s.Lesson == TutorialLesson.CloseAndReview) s.Page = HelpPage.Receipts;
            else s.Page = HelpPage.Lesson;
            s.Practice = s.TutorialActive && s.ActiveLesson == TutorialLesson.Practice;
            s.Assisted = Tutorial.PracticeAssisted;
            s.CanShowTarget = CanShowTutorialTarget;
            s.Directions = TutorialPracticeGuidanceRequested;
            string recovery = s.CanShowTarget ? TutorialGuidanceRecovery : null;
            if (string.IsNullOrEmpty(recovery) && s.OnActiveLesson &&
                (s.Lesson == TutorialLesson.Collect || s.Lesson == TutorialLesson.Prepare || s.Lesson == TutorialLesson.Grow))
                recovery = TutorialPreparationRecovery(s.Lesson);
            s.Recovery = recovery;

            s.Recipe = TutorialStarterRecipe();
            s.IncubationSeconds = Mathf.RoundToInt(Config.IncubationSeconds);
            var starter = Model.GetSpecies("pondlet");
            var tag = starter == null ? (PriceView?)null : Model.GetPrice(starter.Id);
            s.StandardPrice = tag.HasValue ? tag.Value.Standard : 0;
            s.TagPrice = tag.HasValue ? tag.Value.Price : 0;
            s.IntroArrivals = Customers != null && Customers.IntroductoryArrivalsActive;
            s.IntroPatience = Mathf.RoundToInt(ShopCustomers.IntroductoryPatienceSeconds);

            s.Day = Cycle.DayNumber;
            s.Night = Cycle.Phase == ShopDayPhase.Night;
            s.Open = Cycle.Phase == ShopDayPhase.Open;
            s.Money = Model.Currency;
            s.Bloom = Model.Available(CultureKind.Bloom); s.Flow = Model.Available(CultureKind.Flow);
            s.Spark = Model.Available(CultureKind.Spark); s.Mist = Model.Available(CultureKind.Mist);
            s.StagedBloom = Model.Staged(CultureKind.Bloom); s.StagedFlow = Model.Staged(CultureKind.Flow);
            s.SparkKnown = Model.SparkResearchUnlocked; s.MistKnown = Model.MistResearchUnlocked;
            s.Reserved = Model.ReservedCultureCount; s.Capacity = Model.Capacity;
            s.Stocked = DisplayedStockCount();
            s.IncubatorPhase = Model.Incubator.Phase;
            s.IncubatorName = TutorialIncubatorName();
            s.IncubatorSeconds = Mathf.CeilToInt(Model.Incubator.SecondsRemaining);
            s.StaffHired = Staff.Hired || Stocking.Employee.Hired || Laboratory.Employee.Hired;

            s.UpgradeCost = Config.UpgradeCost; s.UpgradeOwned = Model.UpgradeOwned;
            s.BaseCapacity = Config.BaseCapacity; s.UpgradedCapacity = Config.UpgradedCapacity;
            s.BaseCollect = Config.BaseCollectionSeconds; s.UpgradedCollect = Config.UpgradedCollectionSeconds;

            s.FirstName = Model.GetSpecies(Tutorial.FirstSpeciesId)?.DisplayName ?? "";
            s.PracticeName = Model.GetSpecies(Tutorial.PracticeSpeciesId)?.DisplayName ?? "";
            s.FirstDay = Tutorial.FirstSaleDay; s.PracticeDay = Tutorial.PracticeSaleDay;
        }

        // ---------- journal ----------

        public void FillJournal(JournalState s)
        {
            s.Clear();
            s.Discovered = 0;
            foreach (var species in Config.Species)
            {
                var c = new JournalCreature
                {
                    Id = species.Id, Name = species.DisplayName, Family = species.FamilyId ?? "", Look = species.PresentationId ?? "",
                    Discovered = Model.IsDiscovered(species.Id),
                    Bloom = species.Bloom, Flow = species.Flow, Spark = species.Spark, Mist = species.Mist
                };
                if (c.Discovered)
                {
                    s.Discovered++;
                    var tag = Model.GetPrice(species.Id);
                    if (tag.HasValue) { c.Standard = tag.Value.Standard; c.Price = tag.Value.Price; }
                }
                // The same clues as the classic journal, in the same order of checks.
                c.ClueKey = species.Mist > 0 ? (Model.MistResearchUnlocked ? "journal.clue.mistKnown" : "journal.clue.mistLocked") :
                    species.Spark > 0 ? (Model.SparkResearchUnlocked ? "journal.clue.sparkKnown" : "journal.clue.sparkLocked") :
                    species.Id == "pondlet" ? "journal.clue.pondlet" : species.Id == "bubblebelly" ? "journal.clue.bubblebelly" : "journal.clue.ribbonfin";
                s.Creatures.Add(c);
            }
            foreach (var type in Customers.Archetypes)
            {
                if (type.Weight <= 0) continue;
                var c = new JournalCustomer
                {
                    Id = type.Id, Name = type.DisplayName, Budget = type.Budget, TolerancePercent = type.TolerancePercent,
                    FavoritePercent = type.FavoriteTolerancePercent, BargainPercent = type.BargainPercent,
                    Patience = Mathf.RoundToInt(type.PatienceSeconds)
                };
                if (type.FavoriteSpecies != null)
                    foreach (string id in type.FavoriteSpecies)
                    {
                        var species = Model.GetSpecies(id);
                        if (species == null) continue;
                        c.FavoriteLooks.Add(species.PresentationId ?? "");
                        c.FavoriteNames.Add(species.DisplayName);
                    }
                s.Customers.Add(c);
            }
            s.Reputation = Reputation.Points; s.ReputationMax = ShopReputation.MaxPoints;
            s.Tier = Reputation.Tier; s.NextThreshold = Reputation.NextThreshold; s.TierName = Reputation.TierName;
            s.Appeal = Appeal.Points; s.AppealMax = ShopAppeal.MaxPoints;
            s.PriceBonus = Reputation.PremiumPercent + Appeal.ToleranceBonusPercent;
            s.BudgetBonus = Reputation.BudgetBonus + Appeal.BudgetBonus;
            s.ArrivalSeconds = Customers.NextArrivalSeconds;
            s.ReturnsToHelp = TutorialJournalReturnsToHelp;
        }

        // ---------- day report ----------

        public void FillReport(ReportState s)
        {
            var report = LastReportForDisplay();
            s.Has = report != null;
            s.Guided = Tutorial.Mode == TutorialMode.Active && ActiveTutorialLesson == TutorialLesson.CloseAndReview;
            s.Cash = Model.Currency;
            s.Reputation = Reputation.Points; s.Tier = Reputation.Tier; s.NextThreshold = Reputation.NextThreshold; s.TierName = Reputation.TierName;
            if (report == null) { s.Reasons.Clear(); s.TipKey = ""; return; }
            s.Day = report.dayNumber; s.Partial = report.partial;
            s.Revenue = report.revenue; s.Sales = report.sales; s.Visitors = report.visitors;
            s.Wages = Staff.WagesForDay(report.dayNumber) + Stocking.Employee.WagesForDay(report.dayNumber) + Laboratory.Employee.WagesForDay(report.dayNumber);
            s.Profit = report.revenue - s.Wages;
            s.StockedOpen = report.stockedAtOpen; s.Restocked = report.restocked; s.StockedClose = report.stockedAtClose;
            ScreenRules.MissedReasons(report, s.Reasons);
            s.Missed = ScreenRules.Missed(report);
            int empty = 0;
            foreach (string tank in Model.TankIds) if (!Model.GetTank(tank).HasValue) empty++;
            var incubator = Model.Incubator;
            string readyName = incubator.Phase == IncubatorPhase.Ready ? Model.GetSpecies(incubator.SpeciesId)?.DisplayName : null;
            s.TipKey = ScreenRules.ReportTip(report, empty, incubator.Phase, readyName, Model.UpgradeOwned, Model.Currency, Config.UpgradeCost, out s.TipArgs);
        }

        /// <summary>The receipt's Next night: back to play, which acknowledges the report as the classic Continue does.</summary>
        public bool ScreenNextNight()
        {
            if (Panel != ShopPanel.DayReport) return false;
            SetPanel(ShopPanel.None); return true;
        }

        // ---------- pause ----------

        public void FillPause(PauseState s)
        {
            s.Reset = Panel == ShopPanel.Reset;
            s.TutorialActive = Tutorial.Mode == TutorialMode.Active;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s.Dev = true;
#else
            s.Dev = false;
#endif
            s.AutosaveBlocked = AutosaveBlocked;
            s.SaveError = SaveError ?? "";
            s.WaitingForSlapKey = WaitingForSlapKey;
            s.SlapKey = SlapKey;
            s.ReducedMotion = ReducedVisualMotion;
            s.LowDetail = EffectsDetail == FxEffectDetail.Low;
            s.ReducedGags = ReducedGags;
            s.Sensitivity = Sensitivity; s.Volume = Volume; s.Ambience = AmbienceVolume; s.Humor = HumorVolume;
            s.Day = Cycle.DayNumber; s.Night = Cycle.Phase == ShopDayPhase.Night;
        }

        bool InPause => Panel == ShopPanel.Pause || Panel == ShopPanel.Reset;

        /// <summary>Resume: back to play, which saves, as the classic Save and resume does.</summary>
        public bool ScreenResume()
        {
            if (!InPause) return false;
            SetPanel(ShopPanel.None); return true;
        }

        public bool ScreenNewGame()
        {
            if (Panel != ShopPanel.Pause) return false;
            SetPanel(ShopPanel.Reset); return true;
        }

        public bool ScreenKeepShop()
        {
            if (Panel != ShopPanel.Reset) return false;
            SetPanel(ShopPanel.Pause); return true;
        }

        public bool ScreenConfirmNewGame()
        {
            if (Panel != ShopPanel.Reset) return false;
            ConfirmNewGame();
            return Panel != ShopPanel.Reset;
        }

        /// <summary>Reload the last save, as the classic Reload local save does, then back to play.</summary>
        public bool ScreenReload(string notice)
        {
            if (!InPause) return false;
            if (!ReloadSaved()) return false;
            SetPanel(ShopPanel.None);
            Notify(notice);
            return true;
        }

        public void ScreenQuit()
        {
            if (InPause) Quit();
        }

        public bool ScreenSetSensitivity(float value)
        {
            if (!InPause) return false;
            Sensitivity = Mathf.Clamp(value, ScreenRules.MinSensitivity, ScreenRules.MaxSensitivity);
            return true;
        }

        public bool ScreenSetVolumes(float master, float ambience, float humor)
        {
            if (!InPause) return false;
            Volume = Mathf.Clamp01(master); AudioListener.volume = Volume;
            AmbienceVolume = Mathf.Clamp01(ambience);
            HumorVolume = Mathf.Clamp01(humor);
            return true;
        }

        public bool ScreenSetReducedGags(bool on)
        {
            if (!InPause) return false;
            ReducedGags = on; return true;
        }

        public bool ScreenSlapRebind()
        {
            if (Panel != ShopPanel.Pause) return false;
            BeginSlapRebind();
            return WaitingForSlapKey;
        }

        /// <summary>Development builds: the R06 presentation sample, as the classic pause offers it.</summary>
        public bool ScreenPresentation()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Panel != ShopPanel.Pause) return false;
            SetPanel(ShopPanel.Presentation); return true;
#else
            return false;
#endif
        }

        public bool ScreenComedyPreview()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Panel != ShopPanel.Pause) return false;
            OpenComedyPreview(); return Panel == ShopPanel.Presentation;
#else
            return false;
#endif
        }
    }
}
