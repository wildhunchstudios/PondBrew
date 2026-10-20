using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop.Tests
{
    // UX-06 planning notebook: which panels it draws, its tabs, the crew's status words, the catalogue's and the lab
    // helper's reasons, the research path's one primary, strings, copy and stylesheet isolation.
    public sealed class PlanningViewTests
    {
        static ShopUiStrings Table() => ShopUiStrings.LoadDefault();

        [Test] public void TheNotebookDrawsThePlanningPanelsAndNoStationPanel()
        {
            var planning = new[] { ShopPanel.Staff, ShopPanel.Stocker, ShopPanel.StockerTargets, ShopPanel.Technician, ShopPanel.TechnicianOrders,
                ShopPanel.Automation, ShopPanel.Arrange, ShopPanel.Research };
            foreach (ShopPanel panel in Enum.GetValues(typeof(ShopPanel)))
            {
                bool expected = planning.Contains(panel);
                Assert.That(ShopSession.IsPlanningPanel(panel), Is.EqualTo(expected), panel.ToString());
                Assert.That(PlanningViewPresenter.Handles(panel), Is.EqualTo(expected), panel.ToString());
                if (expected) Assert.That(StationCardPresenter.Handles(panel), Is.False, panel + " is not a station card");
            }
        }

        [Test] public void EachTabOpensItsOwnPanelAndTheCrewPagesBelongToTheCrewTab()
        {
            foreach (PlanningTab tab in Enum.GetValues(typeof(PlanningTab)))
                Assert.That(PlanningRules.TabOf(PlanningRules.TabPanel(tab)), Is.EqualTo(tab), tab.ToString());
            foreach (var page in new[] { ShopPanel.Stocker, ShopPanel.StockerTargets, ShopPanel.Technician, ShopPanel.TechnicianOrders })
                Assert.That(PlanningRules.TabOf(page), Is.EqualTo(PlanningTab.Crew), page.ToString());
            Assert.That(PlanningRules.TabWhereKey(PlanningTab.Crew), Is.EqualTo("plan.tab.where.counter"));
            Assert.That(PlanningRules.TabWhereKey(PlanningTab.Automation), Is.EqualTo("plan.tab.where.bench"));
            Assert.That(PlanningRules.TabWhereKey(PlanningTab.Research), Is.EqualTo("plan.tab.where.bench"));
            Assert.That(PlanningRules.TabWhereKey(PlanningTab.Arrange), Is.EqualTo("plan.tab.where.night"));
        }

        static CrewMemberState Member(CrewRole role, bool hired = true, bool scheduled = true, bool paused = false, bool paid = true,
            bool blocked = false, bool working = false, bool work = true) =>
            new CrewMemberState { Role = role, Hired = hired, Scheduled = scheduled, Paused = paused, PaidToday = paid, OnDuty = hired && scheduled && !paused && paid,
                Blocked = blocked, Working = working, HasWork = work, Wage = 4 };

        [Test] public void CrewCardsSayOneShortStatusWord()
        {
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Cashier, hired: false), true), Is.EqualTo("plan.crew.status.notHired"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Cashier, paused: true), true), Is.EqualTo("plan.crew.status.paused"), "paused before anything else");
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Stocker, scheduled: false), true), Is.EqualTo("plan.crew.status.off"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Stocker, paid: false), true), Is.EqualTo("plan.crew.status.unpaid"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Stocker, paid: false), false), Is.EqualTo("plan.crew.status.offToday"), "unpaid by day is off for today");
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Technician, blocked: true, working: true), false), Is.EqualTo("plan.crew.status.stuck"), "stuck before working");
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Cashier, working: true), false), Is.EqualTo("plan.crew.status.serving"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Stocker, working: true), false), Is.EqualTo("plan.crew.status.stocking"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Technician, working: true), false), Is.EqualTo("plan.crew.status.labWork"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Cashier), true), Is.EqualTo("plan.crew.status.ready"), "Pip waits for opening at night");
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Cashier, work: false), false), Is.EqualTo("plan.crew.status.waiting"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Stocker, work: false), false), Is.EqualTo("plan.crew.status.noTargets"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Technician, work: false), true), Is.EqualTo("plan.crew.status.noOrders"));
            Assert.That(PlanningRules.CrewStatusKey(Member(CrewRole.Technician), true), Is.EqualTo("plan.crew.status.onDuty"));
            Assert.That(PlanningRules.CrewStatusWarns("plan.crew.status.stuck") && PlanningRules.CrewStatusWarns("plan.crew.status.unpaid"), Is.True);
            Assert.That(PlanningRules.CrewStatusWarns("plan.crew.status.off"), Is.False);
        }

        [Test] public void PayShowsOnlyForAScheduledUnpaidCrewMemberAtNight()
        {
            Assert.That(PlanningRules.CrewNeedsPay(Member(CrewRole.Cashier, paid: false), true), Is.True);
            Assert.That(PlanningRules.CrewNeedsPay(Member(CrewRole.Cashier, paid: false), false), Is.False, "wages start at night only");
            Assert.That(PlanningRules.CrewNeedsPay(Member(CrewRole.Cashier, paid: false, paused: true), true), Is.False, "paused work isn't paid");
            Assert.That(PlanningRules.CrewNeedsPay(Member(CrewRole.Cashier, paid: false, scheduled: false), true), Is.False);
            Assert.That(PlanningRules.CrewNeedsPay(Member(CrewRole.Cashier), true), Is.False, "already paid");
        }

        [Test] public void CatalogueTilesSayWhyTheyCantBeUsed()
        {
            Assert.That(PlanningRules.TileBlockedKey(true, true, 0, 40, out _), Is.EqualTo("plan.arrange.why.locked"), "locked first");
            Assert.That(PlanningRules.TileBlockedKey(true, false, 0, 40, out _), Is.EqualTo("plan.arrange.why.limit"));
            Assert.That(PlanningRules.TileBlockedKey(false, false, 30, 40, out int amount), Is.EqualTo("plan.arrange.why.coins"));
            Assert.That(amount, Is.EqualTo(10));
            Assert.That(PlanningRules.TileBlockedKey(false, false, 40, 40, out amount), Is.Null);
            Assert.That(amount, Is.Zero);
            Assert.That(PlanningRules.DecorAppeal(false), Is.EqualTo(10));
            Assert.That(PlanningRules.DecorAppeal(true), Is.Zero, "a duplicate type adds nothing");
            Assert.That(Table().Format("plan.arrange.why.coins", 10), Is.EqualTo("Need 10 more coins"));
        }

        [Test] public void TheLabHelperSaysWhyABatchOrKeepingStockCantStart()
        {
            Assert.That(PlanningRules.PrepareBlockedKey(true, true, true, true, true), Is.EqualTo("plan.auto.why.save"), "a save failure first");
            Assert.That(PlanningRules.PrepareBlockedKey(true, true, true, true, false), Is.EqualTo("plan.auto.why.maintain"));
            Assert.That(PlanningRules.PrepareBlockedKey(true, true, true, false, false), Is.EqualTo("plan.auto.why.pending"));
            Assert.That(PlanningRules.PrepareBlockedKey(false, true, false, false, false), Is.EqualTo("plan.auto.why.recipe"));
            Assert.That(PlanningRules.PrepareBlockedKey(true, false, false, false, false), Is.EqualTo("plan.auto.why.module"));
            Assert.That(PlanningRules.PrepareBlockedKey(true, true, false, false, false), Is.Null);
            Assert.That(PlanningRules.MaintainBlockedKey(false, true), Is.EqualTo("plan.auto.why.recipe"));
            Assert.That(PlanningRules.MaintainBlockedKey(true, false), Is.EqualTo("plan.auto.why.destination"));
            Assert.That(PlanningRules.MaintainBlockedKey(true, true), Is.Null);
        }

        [Test] public void TheResearchPathsPrimaryIsTheFirstLockedCultureResearchableHere()
        {
            var s = new ResearchViewState();
            s.Clear();
            s.Steps[0].Unlocked = s.Steps[1].Unlocked = true;
            s.Steps[2].Here = true;
            Assert.That(PlanningRules.ResearchPrimary(s), Is.EqualTo(CultureKind.Spark));
            s.Steps[2].Here = false;
            Assert.That(PlanningRules.ResearchPrimary(s), Is.Null, "Spark is researched at the bench");
            s.Steps[2].Unlocked = true;
            Assert.That(PlanningRules.ResearchPrimary(s), Is.Null, "Mist is next, and it's researched at the vat");
            s.Steps[3].Unlocked = true;
            Assert.That(PlanningRules.ResearchPrimary(s), Is.Null, "nothing left");
            s.Clear();
            Assert.That(s.Steps.Select(step => step.Culture), Is.EqualTo(new[] { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark, CultureKind.Mist }), "Clear keeps the path");
        }

        static readonly string[] PlanKeys =
        {
            "plan.tab.crew", "plan.tab.automation", "plan.tab.arrange", "plan.tab.research", "plan.tab.where.counter", "plan.tab.where.bench", "plan.tab.where.night",
            "plan.back", "plan.crew.title", "plan.crew.subtitle.night", "plan.crew.subtitle.day", "plan.crew.note.night", "plan.crew.note.day",
            "plan.crew.pip.name", "plan.crew.pip.role", "plan.crew.pip.what", "plan.crew.pip.dismissAsk",
            "plan.crew.nori.name", "plan.crew.nori.role", "plan.crew.nori.what", "plan.crew.nori.page", "plan.crew.nori.dismissAsk",
            "plan.crew.moss.name", "plan.crew.moss.role", "plan.crew.moss.what", "plan.crew.moss.page", "plan.crew.moss.dismissAsk",
            "plan.crew.more", "plan.crew.perNight", "plan.crew.hire", "plan.crew.onDuty", "plan.crew.offDuty", "plan.crew.pay", "plan.crew.pause", "plan.crew.resume",
            "plan.crew.retry", "plan.crew.dismiss", "plan.crew.keep", "plan.crew.dismissNow",
            "plan.crew.status.notHired", "plan.crew.status.paused", "plan.crew.status.off", "plan.crew.status.unpaid", "plan.crew.status.offToday",
            "plan.crew.status.stuck", "plan.crew.status.serving", "plan.crew.status.stocking", "plan.crew.status.labWork", "plan.crew.status.ready",
            "plan.crew.status.waiting", "plan.crew.status.noTargets", "plan.crew.status.noOrders", "plan.crew.status.onDuty",
            "plan.targets.title", "plan.targets.subtitle", "plan.targets.note", "plan.targets.empty", "plan.targets.off", "plan.targets.any",
            "plan.queue.title", "plan.queue.subtitle", "plan.queue.note", "plan.queue.waiting", "plan.queue.empty", "plan.queue.add", "plan.queue.addOne",
            "plan.queue.clear", "plan.queue.clearAsk", "plan.queue.keep", "plan.queue.clearNow", "plan.queue.growing", "plan.queue.start", "plan.queue.starts",
            "plan.queue.earlier", "plan.queue.later", "plan.queue.remove",
            "plan.auto.title", "plan.auto.recipes", "plan.auto.collector", "plan.auto.doser", "plan.auto.helpers", "plan.auto.module.missing", "plan.auto.pick", "plan.auto.keep",
            "plan.auto.into", "plan.auto.maintain", "plan.auto.prepare", "plan.auto.cancel", "plan.auto.pause", "plan.auto.resume", "plan.auto.retry",
            "plan.auto.choose", "plan.auto.chosen", "plan.auto.keeping", "plan.auto.subtitle.none", "plan.auto.subtitle.keeping", "plan.auto.subtitle.chosen",
            "plan.auto.note", "plan.auto.why.save", "plan.auto.why.maintain", "plan.auto.why.pending", "plan.auto.why.recipe", "plan.auto.why.module",
            "plan.auto.why.destination",
            "plan.arrange.title", "plan.arrange.closed", "plan.arrange.subtitle", "plan.arrange.note", "plan.arrange.note.owned",
            "plan.arrange.tab.furniture", "plan.arrange.tab.decor", "plan.arrange.tab.owned", "plan.arrange.tab.lab", "plan.arrange.equipment",
            "plan.arrange.appeal", "plan.arrange.ownedEmpty", "plan.arrange.place.store", "plan.arrange.place.lab", "plan.arrange.place.storage",
            "plan.arrange.placeOwned", "plan.arrange.move", "plan.arrange.store", "plan.arrange.sell", "plan.arrange.sellAsk", "plan.arrange.keepIt",
            "plan.arrange.sellNow", "plan.arrange.stocked", "plan.arrange.reserved", "plan.arrange.number", "plan.arrange.moveFree",
            "plan.arrange.expandStore", "plan.arrange.expandLab", "plan.arrange.expandStoreDetail", "plan.arrange.expandLabDetail",
            "plan.arrange.appealGain", "plan.arrange.appealNone", "plan.arrange.slot", "plan.arrange.slots", "plan.arrange.noSlots",
            "plan.arrange.rackDetail", "plan.arrange.vatDetail", "plan.arrange.why.coins", "plan.arrange.why.wing", "plan.arrange.why.displays",
            "plan.arrange.why.vat", "plan.arrange.why.racks", "plan.arrange.why.decor", "plan.arrange.why.tables", "plan.arrange.why.locked", "plan.arrange.why.limit",
            "plan.facility.lab-bloom", "plan.facility.lab-flow", "plan.facility.lab-incubator", "plan.facility.lab-upgrade",
            "plan.place.move", "plan.place.buy", "plan.place.key.rotate", "plan.place.key.click", "plan.place.rotate", "plan.place.place", "plan.place.cancel",
            "plan.research.title", "plan.research.subtitle", "plan.research.note", "plan.research.equipment", "plan.research.starter", "plan.research.done",
            "plan.research.locked", "plan.research.researchSpark", "plan.research.atVat",
            "plan.culture.bloom", "plan.culture.flow", "plan.culture.spark", "plan.culture.mist", "card.bench.research"
        };

        [Test] public void ShippedStringsCoverEveryPlanningKey()
        {
            var s = Table();
            foreach (var key in PlanKeys) Assert.That(s.Has(key), Is.True, key);
            foreach (var facility in new[] { ShopLayout.BloomFacilityId, ShopLayout.FlowFacilityId, ShopLayout.IncubatorFacilityId, ShopLayout.UpgradeFacilityId })
                Assert.That(s.Has("plan.facility." + facility), Is.True, facility);
            foreach (PlanningTab tab in Enum.GetValues(typeof(PlanningTab))) Assert.That(s.Has("plan.tab." + tab.ToString().ToLowerInvariant()), Is.True, tab.ToString());
            foreach (ArrangeTab tab in Enum.GetValues(typeof(ArrangeTab))) Assert.That(s.Has("plan.arrange.tab." + tab.ToString().ToLowerInvariant()), Is.True, tab.ToString());
            foreach (OwnedPlace place in Enum.GetValues(typeof(OwnedPlace))) Assert.That(s.Has("plan.arrange.place." + place.ToString().ToLowerInvariant()), Is.True, place.ToString());
        }

        [Test] public void PlanningButtonsFollowTheCopyRules()
        {
            var s = Table();
            Assert.That(s.Format("plan.crew.pay", 4), Is.EqualTo("Pay · 4"));
            Assert.That(s.Format("plan.crew.hire", "Pip"), Is.EqualTo("Hire Pip"));
            Assert.That(s.Format("plan.research.researchSpark", 48), Is.EqualTo("Research Spark · 48"));
            Assert.That(s.Format("plan.place.buy", "Small display", 24), Is.EqualTo("Place Small display · 24"));
            Assert.That(s.Format("plan.arrange.sell", 12), Is.EqualTo("Sell · +12…"));
            foreach (var key in new[] { "plan.back", "plan.crew.hire", "plan.crew.pay", "plan.crew.pause", "plan.crew.resume", "plan.crew.retry", "plan.crew.dismiss",
                "plan.crew.keep", "plan.crew.dismissNow", "plan.crew.nori.page", "plan.crew.moss.page", "plan.queue.addOne", "plan.queue.clear", "plan.queue.keep",
                "plan.queue.clearNow", "plan.queue.earlier", "plan.queue.later", "plan.queue.remove", "plan.auto.prepare", "plan.auto.cancel", "plan.auto.pause",
                "plan.auto.resume", "plan.auto.retry", "plan.auto.choose", "plan.arrange.placeOwned", "plan.arrange.move", "plan.arrange.store", "plan.arrange.sell",
                "plan.arrange.keepIt", "plan.arrange.sellNow", "plan.research.researchSpark", "plan.targets.off", "plan.targets.any", "card.bench.research",
                "plan.tab.crew", "plan.tab.automation", "plan.tab.arrange", "plan.tab.research" })
            {
                string text = s.Format(key, "Pip", 4);
                Assert.That(text.Contains(" / "), Is.False, key + " uses the retired ' / ' separator");
                int words = text.Split(' ').Count(word => word.Any(char.IsLetter));
                Assert.That(words, Is.InRange(1, 4), key + " is 1-4 words: " + text);
            }
            foreach (var key in PlanKeys) Assert.That(s.Get(key).Contains(" / "), Is.False, key + " uses ' / '");
        }

        /// <summary>
        /// The HUD, card and planning stylesheets all sit on the panel root, so a class name styled in two of them styles both
        /// (UX-04's .es-pip broke the HUD stamp's pips). ShopTokens.uss is the shared base: its classes may be refined by any.
        /// </summary>
        [Test] public void PlanningStylesShareNoClassNamesWithTheHudOrCards()
        {
            string folder = System.IO.Path.Combine(Application.dataPath, "EvolutionShop/Resources/EvolutionShopUI");
            HashSet<string> Read(string file) => SelectorClasses(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, file)));
            var tokens = Read("ShopTokens.uss");
            var hud = Read("HudStyles.uss"); hud.ExceptWith(tokens);
            var cards = Read("CardStyles.uss"); cards.ExceptWith(tokens);
            var planning = Read("PlanningStyles.uss"); planning.ExceptWith(tokens);
            Assert.That(planning.Count, Is.GreaterThan(60), "planning selectors were read");
            Assert.That(planning.All(name => name.StartsWith("es-plan")), Is.True,
                "planning classes start with es-plan: " + string.Join(", ", planning.Where(name => !name.StartsWith("es-plan"))));
            Assert.That(planning.Intersect(hud).ToArray(), Is.Empty, "classes styled by both the planning notebook and the HUD");
            Assert.That(planning.Intersect(cards).ToArray(), Is.Empty, "classes styled by both the planning notebook and the cards");
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

        [Test] public void ThePlanningStylesheetImports()
        {
            Assert.That(Resources.Load<StyleSheet>(ShopUiRoot.PlanningStylesResource), Is.Not.Null, "planning styles");
        }
    }
}
