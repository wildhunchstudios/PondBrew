using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>R09.4 (broadened at the user's word: a tutorial for every part of the game), R10.3 and GR-26: every lesson is
    /// well made; after the guided first night the lessons are met one at a time, in their order, each when its system appears
    /// and affordable; one is learned when its system has been used (its step on the guide's line until then), a card only by
    /// reading it, and either can be skipped; the setting turns them off; an established shop (an older save, a start point)
    /// meets only lessons for what it has not used; the lessons, notes and the lesson now are saved; granting a wish, ordering,
    /// a vial and opening the pinboard or journal are noted; and tomorrow's forecast is the visitors who then come.</summary>
    public sealed class GrowthLessonsTests
    {
        /// <summary>A new game past its first night: day 2, at night, with money and research points to spend.</summary>
        static GrowthShop Fresh(int credits = 100000)
        {
            var shop = new GrowthShop { Guide = GrowthGuideMode.Done, Day = 2, Credits = credits, Rp = 500 };
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            return shop;
        }

        [Test]
        public void EveryLessonIsWellMade()
        {
            var ids = new HashSet<string>();
            foreach (var l in GrowthLessons.All)
            {
                Assert.That(ids.Add(l.Id), Is.True, l.Id + " is one of a kind");
                Assert.That(l.Title, Is.Not.Empty, l.Id);
                Assert.That(l.Era, Is.InRange(1, 5), l.Id + "'s era");
                Assert.That(l.Card, Is.Not.Empty, l.Id + " has a card");
                Assert.That(l.Card.All(p => p.Length > 30 && p.Length < 320), Is.True, l.Id + "'s card reads in a few short paragraphs");
                Assert.That(l.Appears, Is.Not.Null, l.Id + " appears with its system");
                Assert.That(l.Step == null, Is.EqualTo(l.Done == null), l.Id + ": guided steps exactly when it is learned by doing");
            }
            Assert.That(GrowthLessons.All.Length, Is.GreaterThanOrEqualTo(30), "every part of the game");
            Assert.That(GrowthLessons.All.Select(l => l.Era).ToList(), Is.Ordered, "the lessons in their eras' order");
        }

        [Test]
        public void EveryLessonIsMetInItsOrderAndLearnedByUsingIt()
        {
            // The lessons route's walk (GrowthLessonWalk): each lesson staged, met, and its system used once.
            var shop = new GrowthShop();
            GrowthLessonWalk.Prepare(shop);
            foreach (var l in GrowthLessons.All)
            {
                Assert.That(GrowthLessonWalk.Stage(shop, l.Id, shop.Tick), Is.Null, l.Id + " staged");
                Assert.That(shop.LessonDue()?.Id, Is.EqualTo(l.Id), l.Id + " is the lesson due");
                Assert.That(l.AnyTime || shop.Phase == GrowthPhase.Night, Is.True, l.Id + ": at night, unless it is about the day");
                shop.MeetLesson(l.Id);
                if (!l.CardOnly)
                {
                    var step = shop.GuideNow();
                    Assert.That(step, Is.Not.Null, l.Id + ": its step");
                    Assert.That(step.Text, Is.Not.Empty, l.Id + ": its step says what to do");
                    Assert.That(step.Text.Length, Is.LessThanOrEqualTo(78), l.Id + ": its step fits the next-step line (" + step.Text + ")");
                    Assert.That(step.Target == null || step.Target.Length > 0, Is.True, l.Id + ": its marker has a target or none");
                }
                Assert.That(GrowthLessonWalk.Act(shop, l.Id, shop.Tick), Is.Null, l.Id + " used");
                shop.Tick(0.05f);
                Assert.That(shop.LessonsLearned, Does.Contain(l.Id), l.Id + " learned");
                Assert.That(shop.LessonNow, Is.Null, l.Id + ": no step left");
                Assert.That(() => shop.Objectives(), Throws.Nothing, l.Id + ": the goals read as the HUD reads them");
            }
            Assert.That(shop.LessonsMet.Count, Is.EqualTo(GrowthLessons.All.Length), "every lesson met");
            Assert.That(shop.LessonDue(), Is.Null, "nothing left");
        }

        [Test]
        public void TheLessonsWaitForTheFirstNightAndComeOneAtATime()
        {
            var shop = Fresh();
            shop.Guide = GrowthGuideMode.On;
            Assert.That(shop.LessonDue(), Is.Null, "nothing while the guided first night runs");
            shop.Guide = GrowthGuideMode.Done;
            Assert.That(shop.LessonDue()?.Id, Is.EqualTo("upgrades"), "the first lesson after it");
            shop.MeetLesson("upgrades");
            Assert.That(shop.LessonNow, Is.EqualTo("upgrades"));
            Assert.That(shop.LessonDue(), Is.Null, "one at a time");
            Assert.That(shop.LessonsMet, Does.Contain("upgrades"), "met: in the journal's Guide");
            var step = shop.GuideNow();
            Assert.That(step, Is.Not.Null, "its step on the guide's line");
            Assert.That(step.Target, Is.EqualTo("Counter"));
            Assert.That(step.Text, Does.Contain("Upgrade"));
        }

        [Test]
        public void ALessonIsLearnedWhenItsSystemIsUsedAndTheNextFollows()
        {
            var shop = Fresh();
            shop.MeetLesson(shop.LessonDue().Id);
            Assert.That(shop.Upgrade("counter").Success, Is.True);
            shop.Tick(0.1f);
            Assert.That(shop.LessonNow, Is.Null, "used once: learned");
            Assert.That(shop.LessonsLearned, Does.Contain("upgrades"));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Lesson && e.Text.Contains("Upgrading stations")), Is.True, "a note says so");
            Assert.That(shop.GuideNow(), Is.Null, "no step left on the line");
            Assert.That(shop.LessonDue()?.Id, Is.EqualTo("tanks"), "the next lesson");
        }

        [Test]
        public void ASystemAlreadyUsedIsNeverTaught()
        {
            // Used before the shop's tick noticed it (a card could open just after): its lesson is passed by. Décor's lesson stays
            // there to be met after a piece is put on a tank, so only this rule passes it by.
            var shop = Fresh();
            shop.Era = 2; shop.Reputation = GrowthContent.StarThresholds[1] + 5;
            shop.LessonsLearned.UnionWith(GrowthLessons.All.TakeWhile(l => l.Id != "decor").Select(l => l.Id));
            shop.ShopContainers.First().Decor = "reeds";
            Assert.That(shop.LessonsLearned, Does.Not.Contain("decor"), "not yet noticed");
            Assert.That(GrowthLessons.Get("decor").Appears(shop), Is.True, "its system is still there");
            Assert.That(shop.LessonDue(), Is.Null, "décor's lesson passed by: a piece is on a tank (and nothing else is due)");
            shop.Tick(0.05f);
            Assert.That(shop.LessonsLearned, Does.Contain("decor"), "the tick learns it, with no note (it was never met)");
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Lesson), Is.False);
        }

        [Test]
        public void ANoteLearnsItsLessonAtOnce()
        {
            // The journal pauses the shop: opening it learns its lesson then, so a save made as it closes has it.
            var shop = Fresh();
            shop.LessonsLearned.UnionWith(new[] { "upgrades", "tanks", "goals" });
            shop.MeetLesson("journal");
            shop.Note("journal");
            Assert.That(shop.LessonsLearned, Does.Contain("journal"), "learned as it is opened");
            Assert.That(shop.LessonNow, Is.Null);
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Lesson && e.Text.Contains("Your journal")), Is.True, "with its note");
            var again = GrowthShop.Restore(shop.Snapshot(), out string problem);
            Assert.That(problem, Is.Null);
            var learned = again.LessonsLearned.ToList();
            again.Tick(0.05f);
            Assert.That(again.LessonsLearned, Is.EqualTo(learned), "reloaded, its first tick changes nothing of the lessons");
        }

        [Test]
        public void ALessonAppearsOnlyWhenItsPurchaseIsWithinReach()
        {
            var shop = Fresh(credits: 5);
            Assert.That(shop.LessonDue()?.Id, Is.Not.EqualTo("upgrades"), "the counter's upgrade out of reach: not yet");
            shop.Credits = 100000;
            Assert.That(shop.LessonDue()?.Id, Is.EqualTo("upgrades"));
            shop.Era = 2;
            shop.LessonsLearned.UnionWith(GrowthLessons.All.Where(l => l.Era == 1).Select(l => l.Id));
            shop.Reputation = 0;
            Assert.That(shop.LessonDue()?.Id, Is.Not.EqualTo("unit-b"), "unit B needs its stars first");
            shop.Reputation = GrowthContent.StarThresholds[1];
            Assert.That(shop.LessonDue()?.Id, Is.EqualTo("unit-b"));
        }

        [Test]
        public void ACardOnlyIsLearnedByReadingItAndAnyLessonCanBeSkipped()
        {
            var shop = Fresh();
            shop.MeetLesson("days");
            Assert.That(shop.LessonsLearned, Does.Contain("days"), "a card only: learned by reading it");
            Assert.That(shop.LessonNow, Is.Null);
            shop.MeetLesson("upgrades");
            shop.SkipLesson();
            Assert.That(shop.LessonNow, Is.Null, "skipped");
            Assert.That(shop.LessonsLearned, Does.Contain("upgrades"), "never asked again");
            Assert.That(shop.Level("counter"), Is.EqualTo(1), "and nothing else changes");
            Assert.That(shop.LessonDue()?.Id, Is.EqualTo("tanks"));
        }

        [Test]
        public void TheSettingTurnsTheLessonsOff()
        {
            var shop = Fresh();
            shop.MeetLesson("upgrades");
            shop.LessonsOff = true;
            Assert.That(shop.GuideNow(), Is.Null, "no step on the line");
            shop.SkipLesson();
            Assert.That(shop.LessonDue(), Is.Null, "no card");
            shop.LessonsOff = false;
            Assert.That(shop.LessonDue(), Is.Not.Null);
        }

        [Test]
        public void AnEstablishedShopMeetsOnlyTheLessonsForWhatItHasNotUsed()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Era4);
            foreach (var id in new[] { "upgrades", "tanks", "unit-b", "breeding", "large", "annex", "generator", "pipes", "goals", "journal" })
                Assert.That(shop.LessonsLearned, Does.Contain(id), id + ": used already, learned");
            Assert.That(shop.LessonsLearned, Does.Contain("days"), "a card only for what it has met: learned");
            Assert.That(shop.LessonsLearned, Does.Not.Contain("decor"), "decor not used yet: its lesson still to come");
            Assert.That(shop.LessonsLearned, Does.Not.Contain("pip"), "no helper hired yet: Pip's lesson still to come");
            var due = shop.LessonDue();
            Assert.That(due, Is.Not.Null, "it still meets lessons");
            Assert.That(due.Done == null || !due.Done(shop), Is.True, due.Id + " is for something not used yet");
        }

        [Test]
        public void AnOlderSaveLearnsWhatItHasUsedAndTheLessonsAreSaved()
        {
            Assert.That(GrowthSave.CurrentVersion, Is.EqualTo(13), "the lessons came with version 13");
            var shop = GrowthStarts.Make(GrowthStarts.Era3);
            var old = shop.Snapshot();
            old.version = 12; old.lessonsKept = false; old.lessonsMet = null; old.lessonsLearned = null; old.noted = null; old.lessonNow = null;
            var restored = GrowthShop.Restore(old, out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(restored.LessonsLearned, Does.Contain("unit-b"), "an older save: unit B used, learned");
            Assert.That(restored.LessonsLearned, Does.Contain("goals"), "past era 1, the pinboard and the journal are known");

            var fresh = Fresh();
            fresh.MeetLesson("upgrades"); fresh.MeetLesson("days"); fresh.Note("board");
            var again = GrowthShop.Restore(fresh.Snapshot(), out problem);
            Assert.That(problem, Is.Null);
            Assert.That(again.LessonNow, Is.EqualTo("upgrades"), "the lesson now");
            Assert.That(again.LessonsMet, Is.EquivalentTo(fresh.LessonsMet));
            Assert.That(again.LessonsLearned, Is.EquivalentTo(fresh.LessonsLearned));
            Assert.That(again.Noted, Does.Contain("board"));
            Assert.That(again.LessonsLearned, Does.Not.Contain("tanks"), "a new game's lessons stay to come");
        }

        [Test]
        public void WhatTheLessonsReadIsNoted()
        {
            var shop = Fresh();
            shop.Era = 2; shop.Reputation = GrowthContent.StarThresholds[1];
            Assert.That(shop.Order("grower-ember").Success, Is.True);
            Assert.That(shop.Noted, Does.Contain("order"), "an order");
            shop.Vials["ember"] = 3;
            Assert.That(shop.CollectVial("ember", shop.HoldSeconds + 0.01f).Success, Is.True);
            Assert.That(shop.Noted, Does.Contain("vial"), "a vial's dose");

            // A wish granted, at the counter.
            var wished = new GrowthShop { Era = 2, UnitB = true, Credits = 1000, Reputation = GrowthContent.StarThresholds[1] + 5 };
            wished.Discovered.Add("pondlet");
            var tank = wished.Containers[0];
            Assert.That(wished.DevelopmentStock(tank.Id, "pondlet").Success, Is.True);
            Assert.That(wished.OpenShop().Success, Is.True);
            wished.DevelopmentArrive("casual", new GrowthWish { Kind = WishKind.Species, Species = "pondlet" }, tank.Id, 0.2f);
            for (int i = 0; i < 400 && !wished.Noted.Contains("wish"); i++)
            {
                wished.Tick(0.1f);
                if (wished.FrontOfQueue != null) wished.Serve(wished.ServeSeconds);
            }
            Assert.That(wished.Noted, Does.Contain("wish"), "a wish granted");
        }

        [Test]
        public void TomorrowsForecastIsTheVisitorsWhoThenCome()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Era3);
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
            var forecast = shop.Forecast();
            Assert.That(forecast.Count, Is.EqualTo(GrowthShop.ForecastVisitors));
            Assert.That(forecast.Any(f => f.wish != null), Is.True, "some wish for something");
            foreach (var c in shop.ShopContainers.Where(c => c.Free > 0).ToList())
                while (c.Free > 0 && shop.DevelopmentStock(c.Id, "pondlet").Success) { }
            Assert.That(shop.OpenShop().Success, Is.True);
            Assert.That(shop.Forecast(), Is.Empty, "no forecast in the day");
            var came = new List<GrowthVisitor>();
            var seen = new HashSet<int>();
            for (int i = 0; i < 6000 && came.Count < forecast.Count && shop.Phase == GrowthPhase.Day; i++)
            {
                shop.Tick(0.1f);
                foreach (var v in shop.Visitors.Where(v => v.Type != GrowthShop.GuestType).OrderBy(v => v.Id)) if (seen.Add(v.Id)) came.Add(v);
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
            }
            Assert.That(came.Count, Is.GreaterThanOrEqualTo(forecast.Count), "enough of tomorrow's visitors came");
            for (int n = 0; n < forecast.Count; n++)
            {
                Assert.That(came[n].Type, Is.EqualTo(forecast[n].type.Id), "visitor " + n + "'s type as forecast");
                Assert.That(came[n].Wish?.Text, Is.EqualTo(forecast[n].wish?.Text), "visitor " + n + "'s wish as forecast");
            }
        }
    }
}
