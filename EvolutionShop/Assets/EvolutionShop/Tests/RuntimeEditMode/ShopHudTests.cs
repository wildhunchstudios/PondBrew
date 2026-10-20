using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop.Tests
{
    // UX-03 HUD: text, timing, patience, placement rules, the ping tracker and the shipped template.
    public sealed class ShopHudTests
    {
        static ShopUiStrings Table() => ShopUiStrings.LoadDefault();

        [Test] public void DayChipReadsNightDayClosingAndPaused()
        {
            var s = Table();
            ShopHudRules.DayChip(s, HudDayPhase.Night, 3, 0, false, out var title, out var detail);
            Assert.That(title, Is.EqualTo("Night 3")); Assert.That(detail, Is.EqualTo("no rush"));
            ShopHudRules.DayChip(s, HudDayPhase.Open, 3, 252.2f, false, out title, out detail);
            Assert.That(title, Is.EqualTo("Day 3")); Assert.That(detail, Is.EqualTo("4:13 left"));
            ShopHudRules.DayChip(s, HudDayPhase.Closing, 3, 2.4f, false, out title, out detail);
            Assert.That(detail, Is.EqualTo("closing · 0:03"));
            ShopHudRules.DayChip(s, HudDayPhase.Open, 12, 10, true, out title, out detail);
            Assert.That(title, Is.EqualTo("Day 12")); Assert.That(detail, Is.EqualTo("paused"));
        }

        [Test] public void ClockRoundsUpAndNeverGoesNegative()
        {
            Assert.That(ShopHudRules.Clock(0), Is.EqualTo("0:00"));
            Assert.That(ShopHudRules.Clock(-4), Is.EqualTo("0:00"));
            Assert.That(ShopHudRules.Clock(59.01f), Is.EqualTo("1:00"));
            Assert.That(ShopHudRules.Clock(480), Is.EqualTo("8:00"));
        }

        [Test] public void PatienceIsFullUntilQueuedAndLowBelowThirtyPercent()
        {
            var walking = new HudQueueEntry { AtCounter = false, SecondsLeft = 0, PatienceSeconds = 45 };
            Assert.That(ShopHudRules.Patience01(walking), Is.EqualTo(1f));
            Assert.That(ShopHudRules.PatienceLow(walking), Is.False);
            var waiting = new HudQueueEntry { AtCounter = true, SecondsLeft = 30, PatienceSeconds = 60 };
            Assert.That(ShopHudRules.Patience01(waiting), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(ShopHudRules.PatienceLow(waiting), Is.False);
            waiting.SecondsLeft = 17;
            Assert.That(ShopHudRules.PatienceLow(waiting), Is.True);
            waiting.SecondsLeft = 90;
            Assert.That(ShopHudRules.Patience01(waiting), Is.EqualTo(1f));
        }

        [Test] public void NextStepFadesAfterTenSecondsExceptInTheTutorial()
        {
            Assert.That(ShopHudRules.NextStepVisible(9.9f, 0, false), Is.True);
            Assert.That(ShopHudRules.NextStepVisible(10.1f, 0, false), Is.False);
            Assert.That(ShopHudRules.NextStepVisible(500, 0, true), Is.True);
        }

        [Test] public void FloatersRiseAndFadeOrOnlyFadeWithReducedMotion()
        {
            ShopHudRules.FloaterPose(0, false, out float a0, out float r0);
            ShopHudRules.FloaterPose(0.5f, false, out float a1, out float r1);
            ShopHudRules.FloaterPose(ShopHudRules.FloaterSeconds, false, out float a2, out float r2);
            Assert.That(a0, Is.EqualTo(0f)); Assert.That(a1, Is.EqualTo(1f)); Assert.That(a2, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(r0, Is.EqualTo(0f)); Assert.That(r1, Is.GreaterThan(0f)); Assert.That(r2, Is.EqualTo(ShopHudRules.FloaterRise).Within(1e-3f));
            ShopHudRules.FloaterPose(0.5f, true, out float ra, out float rr);
            Assert.That(ra, Is.EqualTo(1f)); Assert.That(rr, Is.EqualTo(0f));
        }

        [Test] public void SignedNumbersUseAPlusOrATrueMinus()
        {
            Assert.That(ShopHudRules.Signed(12), Is.EqualTo("+12"));
            Assert.That(ShopHudRules.Signed(-1500), Is.EqualTo("−1,500"));
            Assert.That(ShopHudRules.Signed(0), Is.EqualTo("0"));
        }

        [Test] public void SlotsSitOnEachSideOfThePoint()
        {
            var p = new Vector2(500, 400); var size = new Vector2(100, 40);
            Assert.That(ShopHudRules.Slot(0, p, size, 20), Is.EqualTo(new Rect(520, 380, 100, 40)));
            Assert.That(ShopHudRules.Slot(1, p, size, 20), Is.EqualTo(new Rect(380, 380, 100, 40)));
            Assert.That(ShopHudRules.Slot(2, p, size, 20), Is.EqualTo(new Rect(450, 340, 100, 40)));
            Assert.That(ShopHudRules.Slot(3, p, size, 20), Is.EqualTo(new Rect(450, 420, 100, 40)));
        }

        [Test] public void PlacementSkipsOccupiedSlotsAndTheCrosshair()
        {
            var panel = new Vector2(1920, 1080);
            var occupied = new List<Rect> { ShopHudRules.CrosshairArea(panel) };
            var bounds = new Rect(0, 0, 1920, 1080);
            // A point at the crosshair: the near slots all touch its square.
            Assert.That(ShopHudRules.TryPlaceAround(panel * 0.5f, new Vector2(200, 80), 28, bounds, occupied, out _), Is.False);
            Assert.That(ShopHudRules.TryPlaceAround(panel * 0.5f, new Vector2(200, 80), ShopHudRules.CrosshairHalf + 20, bounds, occupied, out var placed), Is.True);
            Assert.That(placed.Overlaps(ShopHudRules.CrosshairArea(panel)), Is.False);
            occupied.Add(placed);
            Assert.That(ShopHudRules.TryPlaceAround(panel * 0.5f, new Vector2(200, 80), ShopHudRules.CrosshairHalf + 20, bounds, occupied, out var second), Is.True);
            Assert.That(second.Overlaps(placed), Is.False);
            Assert.That(ShopHudRules.Free(new Rect(-5, 10, 50, 50), bounds, new List<Rect>()), Is.False);
        }

        [Test] public void EdgeArrowsPointAtTheTargetFromTheSafeBand()
        {
            var panel = new Vector2(1920, 1080);
            var safe = new Rect(70, 230, 1780, 680);
            Vector2 right = ShopHudRules.EdgePoint(panel, new Vector2(4000, 540), false, safe, out float angle);
            Assert.That(right.x, Is.EqualTo(safe.xMax).Within(0.01f)); Assert.That(right.y, Is.EqualTo(540).Within(0.01f));
            Assert.That(angle, Is.EqualTo(0f).Within(0.01f));
            Vector2 behind = ShopHudRules.EdgePoint(panel, new Vector2(4000, 540), true, safe, out float back);
            Assert.That(behind.x, Is.EqualTo(safe.xMin).Within(0.01f)); Assert.That(Mathf.Abs(back), Is.EqualTo(180f).Within(0.01f));
            Vector2 below = ShopHudRules.EdgePoint(panel, new Vector2(960, 5000), false, safe, out float down);
            Assert.That(below.y, Is.EqualTo(safe.yMax).Within(0.01f)); Assert.That(down, Is.EqualTo(90f).Within(0.01f));
            Assert.That(ShopHudRules.OffScreen(new Vector2(10, 10), panel, false), Is.False);
            Assert.That(ShopHudRules.OffScreen(new Vector2(10, 10), panel, true), Is.True);
            Assert.That(ShopHudRules.OffScreen(new Vector2(-1, 10), panel, false), Is.True);
        }

        [Test] public void TopColumnStopsAboveTheCrosshairArea()
        {
            // A 2560x1080 screen gives a 926-high panel: the crosshair square starts at 403.
            float limit = ShopHudRules.ColumnLimit(926, 28, 16);
            Assert.That(limit, Is.EqualTo(926 * 0.5f - ShopHudRules.CrosshairHalf - 16 - 28).Within(1e-3f));
            var heights = new List<float> { 64, 64, 64 };
            Assert.That(ShopHudRules.ToastsThatFit(limit, 0, heights, 10), Is.EqualTo(3));
            Assert.That(ShopHudRules.ToastsThatFit(limit, 230, heights, 10), Is.EqualTo(1), "a stamp leaves room for one toast");
            Assert.That(ShopHudRules.ToastsThatFit(limit, 400, heights, 10), Is.EqualTo(1), "the first (a save error) always shows");
            Assert.That(ShopHudRules.ToastsThatFit(limit, 0, new List<float>(), 10), Is.EqualTo(0));
        }

        [Test] public void PingTrackerReportsOnlyNewlyStartedPings()
        {
            var tracker = new HudPingTracker();
            var started = new List<HudPing>();
            var current = new List<HudPing> { new HudPing { Key = "incubator" } };
            tracker.Update(current, started);
            Assert.That(started.Count, Is.EqualTo(1));
            tracker.Update(current, started);
            Assert.That(started.Count, Is.EqualTo(0), "a ping that stays active shows its toast once");
            current.Add(new HudPing { Key = "vat:a" });
            current.Add(new HudPing { Key = "vat:a" });
            tracker.Update(current, started);
            Assert.That(started.Count, Is.EqualTo(1), "duplicates count once");
            current.Clear();
            tracker.Update(current, started);
            Assert.That(tracker.IsActive("incubator"), Is.False);
            current.Add(new HudPing { Key = "incubator" });
            tracker.Update(current, started);
            Assert.That(started.Count, Is.EqualTo(1), "a ping that ends and starts again is new");
        }

        [Test] public void FormatFillsArgumentsAndToleratesABadPattern()
        {
            var s = ShopUiStrings.Parse("{\"entries\":[{\"key\":\"a\",\"text\":\"{0} waiting\"},{\"key\":\"bad\",\"text\":\"{0\"}]}");
            Assert.That(s.Format("a", 3), Is.EqualTo("3 waiting"));
            Assert.That(s.Format("bad", 3), Is.EqualTo("{0"));
            Assert.That(s.Format("missing", 1), Is.EqualTo("[missing]"));
        }

        [Test] public void ShippedStringsCoverEveryHudKeyTheCodeUses()
        {
            var s = Table();
            foreach (var key in new[]
            {
                "hud.day.day", "hud.day.night", "hud.day.noRush", "hud.day.left", "hud.day.closing", "hud.day.paused",
                "hud.ping.ready", "hud.ping.creature", "hud.ping.vat", "hud.ping.vatShort", "hud.ping.stuck",
                "hud.key.use", "hud.key.hold", "hud.key.skip", "hud.key.help", "hud.name.grower", "hud.name.slap",
                "hud.status.full", "hud.status.ready", "hud.status.empty", "hud.status.seconds", "hud.status.secondsTenths",
                "hud.status.dosed", "hud.status.openShop", "hud.status.waiting", "hud.status.open", "hud.status.closing",
                "hud.status.research", "hud.status.upgrade", "hud.status.readyCount", "hud.float.rep", "hud.float.appeal",
                "hud.discovery.title", "hud.discovery.journal", "hud.bubble.skip", "hud.queue.more"
            }) Assert.That(s.Has(key), Is.True, key);
            foreach (CultureKind kind in System.Enum.GetValues(typeof(CultureKind))) Assert.That(s.Has("hud.culture." + kind), Is.True, kind.ToString());
        }

        [Test] public void HudTemplateAndStylesImportWithTheirNamedParts()
        {
            Assert.That(Resources.Load<StyleSheet>(ShopUiRoot.HudStylesResource), Is.Not.Null, "HUD styles");
            var template = Resources.Load<VisualTreeAsset>(ShopUiRoot.HudResource);
            Assert.That(template, Is.Not.Null, "HUD template");
            var tree = template.Instantiate();
            foreach (var name in new[] { HudPresenter.RootName, HudPresenter.DayName, HudPresenter.MoneyName, HudPresenter.QueueName, HudPresenter.NextName,
                HudPresenter.PromptName, HudPresenter.SlapName, HudPresenter.DiscoveryName, HudPresenter.BubbleName, HudPresenter.MarkerName, HudPresenter.CrosshairName,
                "es-hud-toasts", "es-hud-floaters", "es-hud-arrows", "es-hud-day-title", "es-hud-day-detail", "es-hud-money-amount", "es-hud-next-text",
                "es-hud-prompt-key", "es-hud-prompt-name", "es-hud-prompt-status" })
                Assert.That(tree.Q(name), Is.Not.Null, name);
            Assert.That(tree.Q(HudPresenter.DayName).ClassListContains(HudPresenter.ItemClass), Is.True);
            Assert.That(tree.Q(HudPresenter.CrosshairName).ClassListContains(HudPresenter.ItemClass), Is.False, "the crosshair is the protected area, not an item");
        }
    }
}
