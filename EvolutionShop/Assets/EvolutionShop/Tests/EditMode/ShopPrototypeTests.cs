using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>AD-E gameplay prototype rules (AD.6): stars, wishes, haggling and tips, tank themes and the day plan.</summary>
    public sealed class ShopPrototypeTests
    {
        private GameConfig config;
        private GameModel model;
        private static readonly string[] Tanks = { "tank-a", "tank-b" };

        [SetUp] public void Setup() { config = GameConfig.CreateDefault(); model = new GameModel(config); }

        [Test]
        public void StarsFollowReputationAndEachStarUnlocksSomethingVisible()
        {
            int[] points = { 0, 9, 10, 29, 30, 54, 55, 84, 85, 100 };
            int[] stars = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };
            for (int i = 0; i < points.Length; i++) Assert.That(ShopStars.Of(points[i]), Is.EqualTo(stars[i]), points[i] + " reputation");
            Assert.That(ShopStars.NextThreshold(0), Is.EqualTo(10));
            Assert.That(ShopStars.NextThreshold(30), Is.EqualTo(55));
            Assert.That(ShopStars.NextThreshold(90), Is.EqualTo(-1));
            for (int s = 1; s <= ShopStars.Max; s++) Assert.That(ShopStars.Unlock(s), Is.Not.Empty);

            var catalog = CustomerArchetypes.CreateDefault();
            var one = ShopStars.ApplyTo(catalog, 1);
            Assert.That(Weight(one, CustomerArchetypes.Casual), Is.GreaterThan(0));
            Assert.That(Weight(one, CustomerArchetypes.Bargain), Is.GreaterThan(0));
            Assert.That(Weight(one, CustomerArchetypes.Collector), Is.Zero, "Collectors wait for two stars.");
            Assert.That(Weight(one, CustomerArchetypes.Premium), Is.Zero, "Big spenders wait for three stars.");
            Assert.That(Weight(ShopStars.ApplyTo(catalog, 2), CustomerArchetypes.Collector), Is.GreaterThan(0));
            Assert.That(Weight(ShopStars.ApplyTo(catalog, 2), CustomerArchetypes.Premium), Is.Zero);
            Assert.That(Weight(ShopStars.ApplyTo(catalog, 3), CustomerArchetypes.Premium), Is.GreaterThan(0));
            var four = ShopStars.ApplyTo(catalog, 4);
            Assert.That(CustomerArchetypes.Find(four, CustomerArchetypes.Casual).Budget, Is.EqualTo(38), "Four stars: budgets +25% (30 -> 38).");
            Assert.That(CustomerArchetypes.Find(ShopStars.ApplyTo(catalog, 3), CustomerArchetypes.Casual).Budget, Is.EqualTo(30));
            Assert.That(Weight(catalog, CustomerArchetypes.Collector), Is.EqualTo(20), "The base catalog is never changed.");
            for (int s = 1; s <= ShopStars.Max; s++) Assert.DoesNotThrow(() => CustomerArchetypes.Pick(ShopStars.ApplyTo(catalog, s), 3, 1));
        }

        [Test]
        public void WishesAreDeterministicMatchExactlyOrCloselyAndFollowTheVisitorType()
        {
            var species = config.Species;
            var discovered = new[] { species[0], species[1], species[2], species[3] }; // Pondlet, Bubblebelly, Ribbonfin, Lanternfin
            Assert.That(ShopWishes.Pick(4, 2, CustomerArchetypes.Casual, null, new SpeciesDefinition[0], species), Is.Null, "Nothing discovered, no wish.");
            for (int day = 1; day < 6; day++)
                for (int i = 0; i < 6; i++)
                    foreach (var type in new[] { CustomerArchetypes.Casual, CustomerArchetypes.Bargain, CustomerArchetypes.Collector, CustomerArchetypes.Premium })
                    {
                        var first = ShopWishes.Pick(day, i, type, new[] { "bubblebelly", "ribbonfin" }, discovered, species);
                        var again = ShopWishes.Pick(day, i, type, new[] { "bubblebelly", "ribbonfin" }, discovered, species);
                        Assert.That(first.Key, Is.EqualTo(again.Key), "The same day and place always wish for the same thing.");
                        bool known = false;
                        foreach (string id in first.Exact) foreach (var d in discovered) if (d.Id == id) known = true;
                        Assert.That(known, Is.True, "A wish can always be met from what the shop has discovered: " + first.Key);
                        if (type == CustomerArchetypes.Collector) Assert.That(first.Exact, Is.SubsetOf(new[] { "bubblebelly", "ribbonfin" }));
                        if (type == CustomerArchetypes.Premium) Assert.That(first.Key, Is.EqualTo("species:lanternfin"), "Big spenders wish for the most valuable species known.");
                        if (type == CustomerArchetypes.Casual) Assert.That(first.IsSpecies, Is.False, "Casual visitors wish for a trait.");
                    }

            var ribbonfin = ShopWishes.ForSpecies(species[2], species);
            Assert.That(ribbonfin.Label, Is.EqualTo("a Ribbonfin"));
            Assert.That(ribbonfin.Match("ribbonfin"), Is.EqualTo(WishMatch.Exact));
            Assert.That(ribbonfin.Match("pondlet"), Is.EqualTo(WishMatch.Close), "Same family is a close match.");
            Assert.That(ribbonfin.Match("lanternfin"), Is.EqualTo(WishMatch.None), "The Lanternfin glows: a Sparkline for wishes, not a close Brookkin.");
            Assert.That(ribbonfin.Match("driftwing"), Is.EqualTo(WishMatch.None));
            var glows = ShopWishes.ForFamily("Sparkline", species);
            Assert.That(glows.Label, Is.EqualTo("something that glows"));
            Assert.That(glows.Match("lanternfin"), Is.EqualTo(WishMatch.Exact));
            Assert.That(ShopWishes.ForSpecies(species[4], species).Label, Is.EqualTo("a Driftwing"));
            Assert.That(ShopWishes.ForFamily("Airfin", species).Label, Is.EqualTo("something that glides"));

            Assert.That(ShopWishes.Tip(10), Is.EqualTo(3)); Assert.That(ShopWishes.Tip(6), Is.EqualTo(2)); Assert.That(ShopWishes.Tip(1), Is.EqualTo(1));
            Assert.That(ShopWishes.Haggled(10), Is.EqualTo(8)); Assert.That(ShopWishes.Haggled(6), Is.EqualTo(5)); Assert.That(ShopWishes.Haggled(1), Is.EqualTo(1));
        }

        [Test]
        public void AVisitWithoutPreferenceIsExactlyTheClassicVisit()
        {
            Produce("pondlet", "tank-a"); Produce("ribbonfin", "tank-b");
            var classic = new CustomerVisit(model, Tanks);
            var none = new CustomerVisit(model, Tanks, preference: new VisitPreference());
            Assert.That(none.TargetTankId, Is.EqualTo(classic.TargetTankId));
            Queue(classic);
            Assert.That(classic.WishMatch, Is.EqualTo(WishMatch.None)); Assert.That(classic.Haggled, Is.False);
            Assert.That(classic.Wish, Is.Null); Assert.That(classic.Price, Is.EqualTo(6));
        }

        [Test]
        public void AnExactWishHeadsForItsCreatureAndAClosePairHaggles()
        {
            Produce("pondlet", "tank-a"); Produce("ribbonfin", "tank-b");
            var wish = ShopWishes.ForSpecies(model.GetSpecies("ribbonfin"), config.Species);
            var wishing = new CustomerVisit(model, Tanks, preference: new VisitPreference { Wish = wish });
            Assert.That(wishing.TargetTankId, Is.EqualTo("tank-b"), "The wished-for creature comes before the shop's display order.");
            Queue(wishing);
            Assert.That(wishing.WishMatch, Is.EqualTo(WishMatch.Exact)); Assert.That(wishing.Price, Is.EqualTo(10)); Assert.That(wishing.Haggled, Is.False);
            Ok(wishing.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(10), "The sale pays the agreed price; the tip is paid by the shop beside it.");
            Ok(model.ReceiveTip(ShopWishes.Tip(wishing.Price)));
            Assert.That(model.Currency, Is.EqualTo(13));
            Assert.That(model.ReceiveTip(0).Failure, Is.EqualTo(FailureCode.InvalidPrice));

            // Only a Bubblebelly is left (the Pondlet, also a close match, is sold first): a close match for a Ribbonfin wish
            // haggles 15% off the tag, and pays that.
            Ok(model.SellFromTank("tank-a"));
            Assert.That(model.Currency, Is.EqualTo(19));
            Produce("bubblebelly", "tank-b");
            var close = new CustomerVisit(model, Tanks, preference: new VisitPreference { Wish = wish });
            Assert.That(close.TargetTankId, Is.EqualTo("tank-b"));
            Queue(close);
            Assert.That(close.WishMatch, Is.EqualTo(WishMatch.Close)); Assert.That(close.Haggled, Is.True);
            Assert.That(close.TagPrice, Is.EqualTo(10)); Assert.That(close.Price, Is.EqualTo(8));
            Assert.That(model.GetReservation("tank-b").Value.Price, Is.EqualTo(8), "The reservation holds the haggled price.");
            Ok(close.Serve(ShopDayPhase.Open));
            Assert.That(model.Currency, Is.EqualTo(27));
            Assert.That(model.ReserveTank("tank-a", "someone", 100, 0).Failure, Is.EqualTo(FailureCode.InvalidPrice));
        }

        [Test]
        public void ADecoratedDisplaysThemeRaisesWhatMatchingSpeciesSellForAndDrawsItsVisitors()
        {
            var layout = new ShopLayout();
            var cash = Cash(64);
            Assert.That(layout.ThemeOf("tank-a"), Is.EqualTo(ShopTheme.None));
            Assert.That(layout.TryCommit(cash, ShopDayPhase.Night, ShopLayout.SmallDisplayId, null, 3, -5, 0, out string display).Success, Is.True);
            Assert.That(layout.TryCommit(cash, ShopDayPhase.Night, ShopLayout.BubbleLampId, null, 4, -1, 0, out string lamp).Success, Is.True);
            string slot = layout.GetSlotIds(display)[0];
            Assert.That(layout.TryGetDisplayCentre(slot, out float x, out float z), Is.True);
            Assert.That(x, Is.EqualTo(1.5f)); Assert.That(z, Is.EqualTo(-2.5f));
            Assert.That(layout.TryGetDisplayCentre("tank-a", out x, out z), Is.True);
            Assert.That(x, Is.EqualTo(5.5f)); Assert.That(z, Is.EqualTo(-1.2f));
            Assert.That(layout.ThemeOf(slot, out string decor), Is.EqualTo(ShopTheme.Glow), "The lamp 2 m away sets the display's theme.");
            Assert.That(decor, Is.EqualTo(lamp));
            Assert.That(layout.ThemeOf("tank-a"), Is.EqualTo(ShopTheme.None), "The fixed tank is 3.6 m from the lamp, past its reach.");
            Assert.That(layout.ThemeOf("no-such-slot"), Is.EqualTo(ShopTheme.None));
            Assert.That(ShopThemes.Matches(ShopTheme.Reef, "Brookkin"), Is.True);
            Assert.That(ShopThemes.Matches(ShopTheme.Reef, ShopWishes.Family(config.Species[3])), Is.False, "A Lanternfin is not a reef creature.");
            Assert.That(ShopThemes.Matches(ShopTheme.Glow, ShopWishes.Family(config.Species[3])), Is.True);

            // A Pondlet tagged at 10 is over a casual visitor's 150% of 6; the reef theme's 25 points take it.
            Produce("pondlet", "tank-a");
            Ok(model.SetPrice("pondlet", 10));
            var classic = new CustomerVisit(model, new[] { "tank-a" });
            classic.Advance(.1f, true, ShopDayPhase.Open); classic.Advance(2f, true, ShopDayPhase.Open);
            Assert.That(classic.Outcome, Is.EqualTo(CustomerOutcome.TooExpensive));
            var themed = new CustomerVisit(model, new[] { "tank-a" }, preference: new VisitPreference { ThemeTolerance = (tank, species) => ShopThemes.ToleranceFor(ShopTheme.Reef, "Brookkin") });
            themed.Advance(.1f, true, ShopDayPhase.Open); themed.Advance(2f, true, ShopDayPhase.Open);
            Assert.That(themed.Phase, Is.EqualTo(CustomerPhase.ToCounter));
            Assert.That(themed.Price, Is.EqualTo(10)); Assert.That(themed.ThemePoints, Is.EqualTo(ShopThemes.TolerancePoints));
            Assert.That(themed.Reaction, Is.EqualTo(PriceReaction.Pricey), "The themed tag reads as pricey, not too expensive.");

            var catalog = CustomerArchetypes.CreateDefault();
            var drawn = ShopThemes.Attract(catalog, new[] { ShopTheme.Reef, ShopTheme.Reef, ShopTheme.Reef, ShopTheme.Reef, ShopTheme.Glow });
            Assert.That(Weight(drawn, CustomerArchetypes.Collector), Is.EqualTo(20 + ShopThemes.MaxAttract), "Reef displays draw Collectors, up to the cap.");
            Assert.That(Weight(drawn, CustomerArchetypes.Premium), Is.EqualTo(10 + ShopThemes.AttractWeight), "Glow displays draw Big spenders.");
            var locked = ShopThemes.Attract(ShopStars.ApplyTo(catalog, 1), new[] { ShopTheme.Reef });
            Assert.That(Weight(locked, CustomerArchetypes.Collector), Is.Zero, "A theme never brings a type the stars have not unlocked.");
        }

        [Test]
        public void ThePrototypeDayIsShorterWithOneRushAndTheClassicDayIsUnchanged()
        {
            var classic = new ShopDayCycle();
            Assert.That(classic.OpenShop(), Is.True);
            Assert.That(classic.SecondsRemaining, Is.EqualTo(ShopDayCycle.DefaultTradingSeconds)); Assert.That(classic.DayLength, Is.EqualTo(480f));
            var day = new ShopDayCycle();
            Assert.That(day.OpenShop(ShopDayCycle.DefaultTradingSeconds + 1), Is.False, "A day never outlasts what a save can restore.");
            Assert.That(day.OpenShop(float.NaN), Is.False);
            Assert.That(day.OpenShop(ShopDayPlan.PrototypeSeconds), Is.True);
            Assert.That(day.DayLength, Is.EqualTo(330f)); Assert.That(day.SecondsRemaining, Is.EqualTo(330f));
            Assert.That(ShopDayPlan.InRush(330, 330), Is.False);
            Assert.That(ShopDayPlan.InRush(330, 330 - 164), Is.False);
            Assert.That(ShopDayPlan.InRush(330, 330 - 166), Is.True);
            Assert.That(ShopDayPlan.InRush(330, 330 - 214), Is.True);
            Assert.That(ShopDayPlan.InRush(330, 330 - 215), Is.False);
            Assert.That(ShopDayPlan.RushStartsAt(330), Is.EqualTo(165f)); Assert.That(ShopDayPlan.RushEndsAt(330), Is.EqualTo(214.5f).Within(.001f));
            day.Tick(100);
            Assert.That(ShopDayCycle.TryRestore(day.CaptureSave(), out var restored), Is.True, "A prototype day's save restores with the classic rules.");
            Assert.That(restored.SecondsRemaining, Is.EqualTo(230f));
            day.Tick(1000);
            Assert.That(day.Phase, Is.EqualTo(ShopDayPhase.Night)); Assert.That(day.DayNumber, Is.EqualTo(2));
        }

        [Test]
        public void AWishPointIsOneReputationPointWithinTheLimit()
        {
            var reputation = new ShopReputation();
            reputation.AwardWish();
            Assert.That(reputation.Points, Is.EqualTo(ShopReputation.WishPoints));
            Assert.That(ShopReputation.TryRestore(new ReputationSave { version = ShopReputation.SaveVersion, points = ShopReputation.MaxPoints }, out var full), Is.True);
            full.AwardWish();
            Assert.That(full.Points, Is.EqualTo(ShopReputation.MaxPoints));
        }

        private static int Weight(IReadOnlyList<CustomerArchetype> catalog, string id) => CustomerArchetypes.Find(catalog, id).Weight;
        private static void Queue(CustomerVisit customer)
        {
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            customer.Advance(5f, false, ShopDayPhase.Open);
            customer.Advance(0.1f, true, ShopDayPhase.Open);
            Assert.That(customer.Phase, Is.EqualTo(CustomerPhase.Queued));
        }
        private void Produce(string species, string tank)
        {
            model.Tick(100f); var recipe = model.GetSpecies(species);
            for (int i = 0; i < recipe.Bloom; i++) { Ok(model.Collect(CultureKind.Bloom)); Ok(model.Dose(CultureKind.Bloom)); }
            for (int i = 0; i < recipe.Flow; i++) { Ok(model.Collect(CultureKind.Flow)); Ok(model.Dose(CultureKind.Flow)); }
            Ok(model.BeginIncubation()); model.Tick(config.IncubationSeconds); Ok(model.TransferToTank(tank));
        }
        private static GameModel Cash(int amount)
        {
            var defaults = GameConfig.CreateDefault(); var save = new GameModel(defaults).CaptureSave(); save.currency = amount;
            Assert.That(GameModel.TryRestore(defaults, save, out var cash, out _), Is.True); return cash;
        }
        private static void Ok(ActionResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
