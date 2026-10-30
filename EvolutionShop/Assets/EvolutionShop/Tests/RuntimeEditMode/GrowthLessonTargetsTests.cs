using System;
using System.Collections.Generic;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>R09.4: every lesson's guided step points the marker at a kind of thing the rooms have (a GrowthTarget's kind,
    /// and maybe its id), on the lessons route's walk through all of them.</summary>
    public sealed class GrowthLessonTargetsTests
    {
        [Test]
        public void EveryStepsMarkerPointsAtAKindOfThingInTheRooms()
        {
            var shop = new GrowthShop();
            GrowthLessonWalk.Prepare(shop);
            var targets = new List<string>();
            foreach (var l in GrowthLessons.All)
            {
                Assert.That(GrowthLessonWalk.Stage(shop, l.Id, shop.Tick), Is.Null, l.Id + " staged");
                shop.MeetLesson(l.Id);
                var target = l.CardOnly ? null : shop.GuideNow()?.Target;
                if (target != null)
                {
                    targets.Add(target);
                    string kind = target.Split(':')[0];
                    Assert.That(Enum.TryParse<GrowthTargetKind>(kind, out _), Is.True, l.Id + "'s marker: " + target + " is a kind of thing in the rooms");
                }
                Assert.That(GrowthLessonWalk.Act(shop, l.Id, shop.Tick), Is.Null, l.Id + " used");
                shop.Tick(0.05f);
            }
            Assert.That(targets.Count, Is.GreaterThanOrEqualTo(25), "most lessons point the marker at something (" + targets.Count + ")");
        }
    }
}
