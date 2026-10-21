using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop.Tests
{
    // UX-07 screens: which panels they draw, the receipt's reasons and tip, the help steps, reputation tiers as the game
    // sets them, strings (the tutorial's toolkit wording included), copy rules and stylesheet isolation.
    public sealed class ScreenViewTests
    {
        static ShopUiStrings Table() => ShopUiStrings.LoadDefault();
        static string RuntimeFile(string name) => System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "EvolutionShop/Runtime", name));

        [Test] public void TheScreensDrawTheirPanelsAndNoOthers()
        {
            var screens = new[] { ShopPanel.Welcome, ShopPanel.TutorialHelp, ShopPanel.Journal, ShopPanel.DayReport, ShopPanel.Pause, ShopPanel.Reset };
            foreach (ShopPanel panel in Enum.GetValues(typeof(ShopPanel)))
            {
                bool expected = screens.Contains(panel);
                Assert.That(ShopSession.IsScreenPanel(panel), Is.EqualTo(expected), panel.ToString());
                Assert.That(ScreenPresenter.Handles(panel), Is.EqualTo(expected), panel.ToString());
                if (!expected) continue;
                Assert.That(PlanningViewPresenter.Handles(panel), Is.False, panel + " is not a notebook page");
                Assert.That(StationCardPresenter.Handles(panel), Is.False, panel + " is not a station card");
            }
            // UX-09: the development studies have a view of their own, and closing early is the counter card's confirm step.
            Assert.That(ScreenPresenter.Handles(ShopPanel.Presentation), Is.False);
            Assert.That(StudyPresenter.Handles(ShopPanel.Presentation), Is.True);
            Assert.That(ScreenPresenter.Handles(ShopPanel.CloseShop), Is.False);
            Assert.That(StationCardPresenter.Handles(ShopPanel.CloseShop), Is.True);
        }

        [Test] public void TheReceiptListsOnlyTheReasonsThatHappenedInAFixedOrder()
        {
            var reasons = new List<ReportReason>();
            ScreenRules.MissedReasons(new TradeDaySave(), reasons);
            Assert.That(reasons, Is.Empty);
            var day = new TradeDaySave { tooExpensive = 2, impatient = 1, slapped = 3, noStock = 0, closed = 0, interrupted = 0, pathProblems = 1 };
            ScreenRules.MissedReasons(day, reasons);
            Assert.That(reasons.Select(r => r.Key + ":" + r.Count).ToArray(),
                Is.EqualTo(new[] { "report.why.pricey:2", "report.why.impatient:1", "report.why.route:1", "report.why.slapped:3" }));
            Assert.That(ScreenRules.Missed(day), Is.EqualTo(7));
            Assert.That(ScreenRules.Missed(null), Is.EqualTo(0));
        }

        [Test] public void TonightsTipFollowsTheClassicReportsOrder()
        {
            var quiet = new TradeDaySave();
            Assert.That(ScreenRules.ReportTip(quiet, 1, IncubatorPhase.Ready, "Pondlet", false, 0, 24, out var args), Is.EqualTo("report.tip.stockReady"));
            Assert.That(args[0], Is.EqualTo("Pondlet"));
            Assert.That(ScreenRules.ReportTip(quiet, 0, IncubatorPhase.Ready, "Pondlet", false, 0, 24, out _), Is.EqualTo("report.tip.readyWaits"));
            Assert.That(ScreenRules.ReportTip(quiet, 1, IncubatorPhase.Dosing, null, false, 0, 24, out _), Is.EqualTo("report.tip.fillOne"));
            Assert.That(ScreenRules.ReportTip(quiet, 2, IncubatorPhase.Incubating, null, false, 0, 24, out args), Is.EqualTo("report.tip.fillMany"));
            Assert.That(args[0], Is.EqualTo(2));
            // With the displays stocked, the day's biggest problem comes first: prices, then slaps, patience, empty shelves.
            Assert.That(ScreenRules.ReportTip(new TradeDaySave { tooExpensive = 2, impatient = 2, noStock = 1 }, 0, IncubatorPhase.Dosing, null, false, 0, 24, out args),
                Is.EqualTo("report.tip.pricey"));
            Assert.That(args[0], Is.EqualTo(2));
            Assert.That(ScreenRules.ReportTip(new TradeDaySave { tooExpensive = 1, impatient = 2, slapped = 1 }, 0, IncubatorPhase.Dosing, null, false, 0, 24, out _),
                Is.EqualTo("report.tip.slapped"));
            Assert.That(ScreenRules.ReportTip(new TradeDaySave { impatient = 2, noStock = 2 }, 0, IncubatorPhase.Dosing, null, false, 0, 24, out _), Is.EqualTo("report.tip.impatient"));
            Assert.That(ScreenRules.ReportTip(new TradeDaySave { impatient = 1, noStock = 2 }, 0, IncubatorPhase.Dosing, null, false, 0, 24, out _), Is.EqualTo("report.tip.noStock"));
            Assert.That(ScreenRules.ReportTip(quiet, 0, IncubatorPhase.Dosing, null, false, 30, 24, out args), Is.EqualTo("report.tip.upgrade"));
            Assert.That(args[0], Is.EqualTo(24));
            Assert.That(ScreenRules.ReportTip(quiet, 0, IncubatorPhase.Dosing, null, true, 30, 24, out _), Is.EqualTo("report.tip.ready"));
            Assert.That(ScreenRules.ReportTip(quiet, 0, IncubatorPhase.Incubating, null, false, 10, 24, out _), Is.EqualTo("report.tip.growing"));
        }

        [Test] public void HelpStepsAreNumberedFromOrientToTheNextGoal()
        {
            Assert.That(ScreenRules.LessonNumber(TutorialLesson.Orient), Is.EqualTo(1));
            Assert.That(ScreenRules.LessonNumber(TutorialLesson.NextPlan), Is.EqualTo(10));
            Assert.That(ScreenRules.LessonKey(TutorialLesson.PriceAndOpen), Is.EqualTo("help.lesson.priceandopen"));
        }

        [Test] public void TheJournalsTierLinesMatchTheGamesReputation()
        {
            foreach (int points in new[] { 0, 9, 10, 29, 30, 100 })
            {
                Assert.That(ShopReputation.TryRestore(new ReputationSave { version = ShopReputation.SaveVersion, points = points }, out var rep), Is.True);
                Assert.That(ScreenRules.TierArrivalSeconds(rep.Tier), Is.EqualTo(rep.ArrivalSeconds), points + " arrival");
                Assert.That(ScreenRules.TierPricePercent(rep.Tier), Is.EqualTo(rep.PremiumPercent), points + " price");
                Assert.That(ScreenRules.TierBudget(rep.Tier), Is.EqualTo(rep.BudgetBonus), points + " budget");
            }
            var s = Table();
            for (int tier = 1; tier <= 2; tier++)
            {
                Assert.That(ShopReputation.TryRestore(new ReputationSave { version = ShopReputation.SaveVersion, points = tier == 1 ? 10 : 30 }, out var rep), Is.True);
                Assert.That(s.Get("journal.tier." + tier), Is.EqualTo(rep.TierName), "tier " + tier + " name");
            }
        }

        [Test] public void BarsAndPercentsStayInRange()
        {
            Assert.That(ScreenRules.Fill(5, 0), Is.EqualTo(0));
            Assert.That(ScreenRules.Fill(-1, 10), Is.EqualTo(0));
            Assert.That(ScreenRules.Fill(15, 10), Is.EqualTo(1));
            Assert.That(ScreenRules.Fill(5, 10), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(ScreenRules.Percent(0.654f), Is.EqualTo(65));
            Assert.That(ScreenRules.Percent(2f), Is.EqualTo(100));
            Assert.That(ScreenRules.Percent(-1f), Is.EqualTo(0));
        }

        static readonly string[] ScreenKeys =
        {
            "welcome.title", "welcome.sub", "welcome.help", "welcome.recovery", "welcome.skip", "welcome.skip.ask", "welcome.skip.keep", "welcome.skip.yes",
            "help.title", "help.subtitle", "help.subtitle.tutorial", "help.footer", "help.step", "help.step.named", "help.controls", "help.show",
            "help.show.continue", "help.directions.off", "help.directions.note", "help.current", "help.now", "help.staff", "help.lesson.serve.intro",
            "help.note.reference", "help.note.reading", "help.note.practice", "help.next.title", "help.next.finish", "help.next.journal", "help.next.upgrade",
            "help.up.title", "help.up.back", "help.receipts.title", "help.receipts.ok", "help.receipts.browse", "help.slap.rule",
            "journal.title", "journal.note", "journal.note.help", "journal.back.help", "journal.unknown", "journal.budget", "journal.budget.none",
            "journal.patience", "journal.pays", "journal.favorites", "journal.favorites.none", "journal.shop.next", "journal.shop.top", "journal.shop.now",
            "report.title", "report.title.none", "report.none", "report.earned", "report.sold", "report.visitors", "report.missed", "report.walked",
            "report.profit", "report.details", "report.partial", "report.reputation", "report.tonight", "report.next", "report.guided",
            "pause.title", "pause.resume", "pause.settings", "pause.help", "pause.quit", "pause.file", "pause.save.ok", "pause.save.error", "pause.save.blocked",
            "pause.reload", "pause.reload.title", "pause.reload.text", "pause.skip.title", "pause.skip.text", "pause.reload.keep", "pause.reload.yes", "pause.reloaded", "pause.newGame", "pause.dev", "pause.presentation",
            "pause.comedy", "pause.sensitivity", "pause.slap", "pause.slap.change", "pause.slap.wait", "pause.volume", "pause.ambience", "pause.humor",
            "pause.reducedMotion", "pause.detail", "pause.reducedGags", "pause.footer", "pause.footer.comfort", "pause.reset.title", "pause.reset.text",
            "pause.reset.keep", "pause.reset.yes"
        };

        [Test] public void ShippedStringsCoverEveryScreenKey()
        {
            var s = Table();
            foreach (var key in ScreenKeys) Assert.That(s.Has(key), Is.True, key);
            foreach (var mode in new[] { "blocked", "offered", "active", "completed", "ready" })
            {
                Assert.That(s.Has("welcome.head." + mode), Is.True, mode); Assert.That(s.Has("welcome.line." + mode), Is.True, mode);
            }
            foreach (var mode in new[] { "blocked", "offered", "active", "done" }) Assert.That(s.Has("welcome.primary." + mode), Is.True, mode);
            foreach (var mode in new[] { "blocked", "offered", "help" }) Assert.That(s.Has("welcome.secondary." + mode), Is.True, mode);
            for (var lesson = TutorialLesson.Orient; lesson <= TutorialLesson.NextPlan; lesson++)
            {
                Assert.That(s.Has(ScreenRules.LessonKey(lesson)), Is.True, lesson + " name");
                Assert.That(s.Has(ScreenRules.LessonKey(lesson) + ".do"), Is.True, lesson + " do");
            }
            foreach (JournalTab tab in Enum.GetValues(typeof(JournalTab))) Assert.That(s.Has("journal.tab." + tab.ToString().ToLowerInvariant()), Is.True, tab.ToString());
            foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab))) Assert.That(s.Has("pause.tab." + tab.ToString().ToLowerInvariant()), Is.True, tab.ToString());
            var reasons = new List<ReportReason>();
            ScreenRules.MissedReasons(new TradeDaySave { tooExpensive = 1, noStock = 1, impatient = 1, closed = 1, interrupted = 1, pathProblems = 1, slapped = 1 }, reasons);
            Assert.That(reasons.Count, Is.EqualTo(7));
            foreach (var r in reasons) Assert.That(s.Has(r.Key), Is.True, r.Key);
            foreach (var key in new[] { "stockReady", "readyWaits", "fillOne", "fillMany", "pricey", "slapped", "impatient", "noStock", "upgrade", "growing", "ready" })
                Assert.That(s.Has("report.tip." + key), Is.True, key);
            foreach (var clue in new[] { "pondlet", "bubblebelly", "ribbonfin", "sparkLocked", "sparkKnown", "mistLocked", "mistKnown" })
                Assert.That(s.Has("journal.clue." + clue), Is.True, clue);
        }

        static string[] TutKeys()
        {
            string source = RuntimeFile("ShopTutorialHud.cs") + RuntimeFile("ShopTutorialGuidance.cs") + RuntimeFile("ShopTutorialSession.cs");
            return System.Text.RegularExpressions.Regex.Matches(source, "TutText\\(\"([A-Za-z.]+)\"").Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value).Distinct().ToArray();
        }

        /// <summary>
        /// The tutorial's toolkit wording (UX-07): every line the tutorial code routes through TutText has a string, says
        /// nothing with " / ", and names the cards' controls rather than the classic panels' buttons.
        /// </summary>
        [Test] public void TheTutorialsToolkitWordingNamesTheCardsControls()
        {
            var s = Table();
            var keys = TutKeys();
            Assert.That(keys.Length, Is.GreaterThanOrEqualTo(65), "TutText keys were read");
            string[] classic = { "Begin incubation", "Open for day", "Counter > Staff", "Continue preparation", "Close for the night", "Release 1 ", "below" };
            foreach (var key in keys)
            {
                Assert.That(s.Has(key), Is.True, key);
                string text = s.Get(key);
                Assert.That(text.Contains(" / "), Is.False, key + " uses ' / '");
                foreach (var name in classic) Assert.That(text.Contains(name), Is.False, key + " names the classic '" + name + "'");
            }
        }

        [Test] public void ScreenButtonsFollowTheCopyRules()
        {
            var s = Table();
            foreach (var key in new[] { "welcome.primary.offered", "welcome.primary.active", "welcome.primary.done", "welcome.primary.blocked", "welcome.secondary.offered",
                "welcome.secondary.blocked", "welcome.secondary.help", "help.show", "help.directions.off", "help.next.finish", "help.receipts.ok", "report.next",
                "pause.resume", "pause.settings", "pause.help", "pause.quit", "pause.reload.yes", "pause.reload.keep", "pause.reset.keep", "pause.reset.yes",
                "pause.slap.change", "welcome.skip.keep", "welcome.skip.yes" })
            {
                string text = s.Get(key);
                int words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.That(words, Is.InRange(1, 4), key + ": " + text);
                Assert.That(text.Contains(" / "), Is.False, key);
            }
            foreach (var key in ScreenKeys) Assert.That(s.Get(key).Contains(" / "), Is.False, key + " uses ' / '");
        }

        /// <summary>
        /// The screen stylesheet sits on the panel root with the HUD, card and notebook sheets, so a class styled in two of
        /// them styles both. Its own classes start with es-screen; Unity's slider parts may be styled inside them.
        /// </summary>
        [Test] public void ScreenStylesShareNoClassNamesWithTheOtherSheets()
        {
            string folder = System.IO.Path.Combine(Application.dataPath, "EvolutionShop/Resources/EvolutionShopUI");
            HashSet<string> Read(string file) => SelectorClasses(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, file)));
            var tokens = Read("ShopTokens.uss");
            var others = new HashSet<string>(Read("HudStyles.uss").Concat(Read("CardStyles.uss")).Concat(Read("PlanningStyles.uss")));
            others.ExceptWith(tokens);
            var screens = Read("ScreenStyles.uss"); screens.ExceptWith(tokens);
            Assert.That(screens.Count, Is.GreaterThan(80), "screen selectors were read");
            var foreign = screens.Where(name => !name.StartsWith("es-screen") && !name.StartsWith("unity-")).ToArray();
            Assert.That(foreign, Is.Empty, "screen classes start with es-screen: " + string.Join(", ", foreign));
            Assert.That(screens.Where(n => n.StartsWith("es-")).Intersect(others).ToArray(), Is.Empty, "classes styled by the screens and another sheet");
            Assert.That(Read("CardStyles.uss").Contains("es-card__callout"), Is.True, "the cards style the tutorial callout");
        }

        static HashSet<string> SelectorClasses(string uss)
        {
            uss = System.Text.RegularExpressions.Regex.Replace(uss, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline);
            uss = System.Text.RegularExpressions.Regex.Replace(uss, @"\{[^}]*\}", " ");
            var names = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(uss, @"\.([A-Za-z][\w-]*)"))
                names.Add(match.Groups[1].Value);
            return names;
        }

        [Test] public void TheScreenStylesheetImports()
        {
            Assert.That(Resources.Load<StyleSheet>(ShopUiRoot.ScreenStylesResource), Is.Not.Null, "screen styles");
        }
    }
}
