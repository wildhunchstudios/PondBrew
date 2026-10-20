using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop.Tests
{
    // UX-04 station cards: price steps, buyer faces, recipe shortages, the counter's one primary, strings and template.
    public sealed class StationCardTests
    {
        static ShopUiStrings Table() => ShopUiStrings.LoadDefault();

        [Test] public void PriceStepsByOneOrFiveWithShiftAndStaysInRange()
        {
            Assert.That(StationCardRules.PriceStep(10, 1, 30, 1, false), Is.EqualTo(11));
            Assert.That(StationCardRules.PriceStep(10, 1, 30, -1, false), Is.EqualTo(9));
            Assert.That(StationCardRules.PriceStep(10, 1, 30, 1, true), Is.EqualTo(15));
            Assert.That(StationCardRules.PriceStep(10, 1, 30, -1, true), Is.EqualTo(5));
            Assert.That(StationCardRules.PriceStep(28, 1, 30, 1, true), Is.EqualTo(30), "Shift near the top stops at the maximum");
            Assert.That(StationCardRules.PriceStep(3, 2, 30, -1, true), Is.EqualTo(2), "Shift near the bottom stops at the minimum");
            Assert.That(StationCardRules.PriceStep(30, 1, 30, 1, false), Is.EqualTo(30));
        }

        [Test] public void FacesFollowTheReactionAndBudgetBeatsEverything()
        {
            Assert.That(StationCardRules.MoodFor(PriceReaction.Bargain, false), Is.EqualTo(BuyerMood.Happy));
            Assert.That(StationCardRules.MoodFor(PriceReaction.Fair, false), Is.EqualTo(BuyerMood.Happy));
            Assert.That(StationCardRules.MoodFor(PriceReaction.Pricey, false), Is.EqualTo(BuyerMood.Neutral));
            Assert.That(StationCardRules.MoodFor(PriceReaction.TooExpensive, false), Is.EqualTo(BuyerMood.Frown));
            Assert.That(StationCardRules.MoodFor(PriceReaction.TooExpensive, true), Is.EqualTo(BuyerMood.Broke));
            Assert.That(StationCardRules.WillBuy(BuyerMood.Happy) && StationCardRules.WillBuy(BuyerMood.Neutral), Is.True, "pricey still buys");
            Assert.That(StationCardRules.WillBuy(BuyerMood.Frown) || StationCardRules.WillBuy(BuyerMood.Broke), Is.False);
        }

        [Test] public void FaceWordsComeFromTheStringTable()
        {
            var s = Table();
            Assert.That(s.Get(StationCardRules.MoodWordKey(PriceReaction.Bargain, false)), Is.EqualTo("Bargain!"));
            Assert.That(s.Get(StationCardRules.MoodWordKey(PriceReaction.Fair, false)), Is.EqualTo("Fair"));
            Assert.That(s.Get(StationCardRules.MoodWordKey(PriceReaction.Pricey, false)), Is.EqualTo("Pricey"));
            Assert.That(s.Get(StationCardRules.MoodWordKey(PriceReaction.TooExpensive, false)), Is.EqualTo("Nope"));
            Assert.That(s.Get(StationCardRules.MoodWordKey(PriceReaction.TooExpensive, true)), Is.EqualTo("Broke"));
        }

        [Test] public void BuyersCountsHappyAndNeutralFaces()
        {
            var faces = new List<BuyerFace>
            {
                new BuyerFace { Reaction = PriceReaction.Fair },
                new BuyerFace { Reaction = PriceReaction.Pricey },
                new BuyerFace { Reaction = PriceReaction.TooExpensive },
                new BuyerFace { Reaction = PriceReaction.TooExpensive, OverBudget = true },
            };
            Assert.That(StationCardRules.Buyers(faces), Is.EqualTo(2));
        }

        [Test] public void MissingCultureCountsSiphonPlusStagedInCultureOrder()
        {
            // Bubblebelly: 2 Bloom + 1 Flow.
            int[] recipe = { 2, 1, 0, 0 };
            Assert.That(StationCardRules.MissingCulture(new[] { 2, 1, 0, 0 }, new[] { 0, 0, 0, 0 }, recipe), Is.Null);
            Assert.That(StationCardRules.MissingCulture(new[] { 1, 0, 0, 0 }, new[] { 1, 1, 0, 0 }, recipe), Is.Null, "staged doses count");
            Assert.That(StationCardRules.MissingCulture(new[] { 1, 1, 0, 0 }, new[] { 0, 0, 0, 0 }, recipe), Is.EqualTo(CultureKind.Bloom));
            Assert.That(StationCardRules.MissingCulture(new[] { 2, 0, 0, 0 }, new[] { 0, 0, 0, 0 }, recipe), Is.EqualTo(CultureKind.Flow));
            Assert.That(StationCardRules.MissingCulture(new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 }, recipe), Is.EqualTo(CultureKind.Bloom), "the first lacking culture is named");
            Assert.That(StationCardRules.MissingCulture(new[] { 1, 1, 0, 0 }, new[] { 0, 0, 0, 0 }, new[] { 1, 1, 1, 0 }), Is.EqualTo(CultureKind.Spark));
        }

        [Test] public void CounterHasOpenShopAtNightAndServeWhileTradingOrClosing()
        {
            Assert.That(StationCardRules.PrimaryFor(HudDayPhase.Night), Is.EqualTo(CounterPrimary.OpenShop));
            Assert.That(StationCardRules.PrimaryFor(HudDayPhase.Open), Is.EqualTo(CounterPrimary.Serve));
            Assert.That(StationCardRules.PrimaryFor(HudDayPhase.Closing), Is.EqualTo(CounterPrimary.Serve), "an accepted checkout can finish while closing");
        }

        [Test] public void NightWarningsSayHowManyDisplaysAreEmpty()
        {
            var s = Table();
            Assert.That(StationCardRules.NightWarningKey(2, 0), Is.Null);
            Assert.That(s.Format(StationCardRules.NightWarningKey(2, 1), 1), Is.EqualTo("1 display empty"));
            Assert.That(s.Format(StationCardRules.NightWarningKey(4, 2), 2), Is.EqualTo("2 displays empty"));
            Assert.That(s.Get(StationCardRules.NightWarningKey(2, 2)), Is.EqualTo("Nothing on display yet"));
            Assert.That(StationCardRules.NightWarningKey(0, 0), Is.Null);
        }

        [Test] public void CardLabelsFollowTheCopyRules()
        {
            var s = Table();
            Assert.That(s.Format("card.inc.start", 10), Is.EqualTo("Start · 10s"));
            Assert.That(s.Get("card.inc.return"), Is.EqualTo("Return doses"));
            Assert.That(s.Format("card.tank.useStandard", 10), Is.EqualTo("Use standard (10)"));
            Assert.That(s.Format("card.tank.buyers", 3, 4), Is.EqualTo("3 of 4 will buy"));
            Assert.That(s.Format("card.counter.open", ShopHudRules.Clock(ShopDayCycle.DefaultTradingSeconds)), Is.EqualTo("Open shop · 8:00"));
            Assert.That(s.Format("card.inc.need", "Flow"), Is.EqualTo("Grab Flow at the grower"));
            foreach (var key in new[] { "card.inc.start", "card.inc.return", "card.tank.useStandard", "card.counter.serve", "card.counter.open",
                "card.counter.closeEarly", "card.counter.keep", "card.counter.closeNow", "card.counter.crew", "card.counter.arrange", "card.close" })
            {
                string text = s.Get(key);
                Assert.That(text.Contains(" / "), Is.False, key + " uses the retired ' / ' separator");
                Assert.That(text.Split(' ').Length, Is.LessThanOrEqualTo(4), key + " is 1-4 words: " + text);
            }
        }

        [Test] public void ShippedStringsCoverEveryCardKeyTheCodeUses()
        {
            var s = Table();
            foreach (var key in new[]
            {
                "card.inc.status.mix", "card.inc.status.dosed", "card.inc.status.growing", "card.inc.status.ready", "card.inc.status.busy",
                "card.inc.line.mix", "card.inc.line.growing", "card.inc.line.ready", "card.inc.recipes", "card.inc.noRecipes", "card.inc.need",
                "card.inc.time", "card.inc.chamber", "card.inc.chip", "card.inc.inSiphon", "card.inc.start", "card.inc.return", "card.inc.release",
                "card.inc.why.empty", "card.inc.busyMoss", "card.inc.busyHelper", "card.inc.cancelHelper", "card.inc.seconds", "card.inc.growNote",
                "card.tank.status.empty", "card.tank.status.sale", "card.tank.status.reserved", "card.tank.line.empty", "card.tank.line.none",
                "card.tank.line.tag", "card.tank.line.reserved", "card.tank.fromIncubator", "card.tank.fromVat", "card.tank.delivering",
                "card.tank.lower", "card.tank.raise", "card.tank.standardChip", "card.tank.useStandard", "card.tank.buyers", "card.tank.shift",
                "card.tank.faceTip", "card.tank.collects", "card.tank.back",
                "card.counter.status.night", "card.counter.status.open", "card.counter.status.closing", "card.counter.status.waiting",
                "card.counter.orders", "card.counter.none", "card.counter.line.night", "card.counter.line.closing",
                "card.counter.row.meta", "card.counter.row.coming", "card.counter.browsing", "card.counter.serve", "card.counter.serveIdle",
                "card.counter.open", "card.counter.why.notAtCounter", "card.counter.why.nobody", "card.counter.why.paused",
                "card.counter.shopOpen", "card.counter.shopClosing", "card.counter.left", "card.counter.closeEarly", "card.counter.confirm",
                "card.counter.keep", "card.counter.closeNow", "card.counter.crew", "card.counter.arrange", "card.counter.report", "card.counter.nextGoal"
            }) Assert.That(s.Has(key), Is.True, key);
        }

        [Test] public void CardTemplateHasOnePrimaryAndCloseIsSecondary()
        {
            Assert.That(Resources.Load<StyleSheet>(ShopUiRoot.CardStylesResource), Is.Not.Null, "card styles");
            var tree = Resources.Load<VisualTreeAsset>(ShopUiRoot.StationCardResource).Instantiate();
            foreach (var name in new[] { StationCardPresenter.ViewSlotName, StationCardPresenter.AfterSlotName, StationCardPresenter.ReasonName })
                Assert.That(tree.Q<VisualElement>(name), Is.Not.Null, name);
            var primaries = tree.Query<Button>(className: StationCardPresenter.PrimaryClass).ToList();
            Assert.That(primaries.Count, Is.EqualTo(1), "one primary per card");
            Assert.That(primaries[0].name, Is.EqualTo(StationCardPresenter.PrimaryName));
            var close = tree.Q<Button>(StationCardPresenter.CloseName);
            Assert.That(close.ClassListContains(StationCardPresenter.PrimaryClass), Is.False, "Close is secondary");
            Assert.That(close.ClassListContains("es-button--secondary"), Is.True);
        }
    }
}
