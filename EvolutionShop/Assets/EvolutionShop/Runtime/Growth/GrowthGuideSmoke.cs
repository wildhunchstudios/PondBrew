#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-14's Windows route (-evolutionGrowthGuideSmoke): the cut-over (GR.43). Which game each command line starts (the new
    /// game with none, the classic game behind -evolutionClassic and its own tools' flags); the guided first night from a
    /// fresh shop: the welcome's word on it, each step's line and marker through the first loop, the marker at the screen's
    /// edge when its target is behind, hidden while a card is open, a step undone asked for again, the first sale, and the
    /// first day's receipt ending it with a note; its state saved and restored; skipping it from the pause screen and from
    /// the welcome; an old save without it.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string GuideScenarioText = "GR-14 the cut-over: which game each command line starts; the guided first night from a fresh shop (the welcome, each step's line and marker, the marker at the screen's edge and hidden behind a card, a step undone, the first sale, the receipt ending it); saved and restored; skipped from the pause screen and the welcome; an old save without it";

        /// <summary>Presses a button of the open card through the panel's own dispatch (the classic UI routes' way).</summary>
        void Submit(string text, string what)
        {
            var card = s.Ui.CardElement;
            var button = card == null ? null : UQueryExtensions.Query<Button>(card).ToList().FirstOrDefault(b => b.text == text);
            Check(ShopUiTestDriver.Submit(button), what);
        }

        IEnumerator GuideScenario()
        {
            limitations.Add("GR-14 route: the shop's own actions drive the incubator and the day (what the card buttons call); a synthetic mouse is held on the growers and the counter and a synthetic E pressed at the incubator and the tank; the pause screen's Skip it and the welcome's Play without guidance are pressed through the panel's own dispatch; the day after the first sale runs on the shop's clock, accelerated.");
            yield return Opening();
            var shop = s.Shop;

            // The switch: the new game unless the classic game is asked for.
            Check(GrowthGreybox.Requested, "This route's command line runs the new game");
            Check(!GrowthGreybox.ClassicAsked(new[] { "EvolutionShop.exe" }), "No flags: EvolutionShop.exe starts the new game");
            Check(GrowthGreybox.ClassicAsked(new[] { "EvolutionShop.exe", "-evolutionClassic" }), "-evolutionClassic starts the classic game");
            Check(GrowthGreybox.ClassicAsked(new[] { "EvolutionShop.exe", "-evolutionSmoke", "-evolutionCycleSmoke" }) && GrowthGreybox.ClassicAsked(new[] { "EvolutionShop.exe", "-evolutionSmoke" }),
                "The classic game's routes start the classic game");
            Check(!GrowthGreybox.ClassicAsked(new[] { "EvolutionShop.exe", "-evolutionGrowth", "-evolutionGrowthStart", "era3" }), "A start point starts the new game");

            Check(shop.Guide == GrowthGuideMode.Off && shop.GuideNow() == null, "A route's fresh shop has no guide (only a new game outside a route starts with it)");
            shop.Guide = GrowthGuideMode.On;
            limitations.Add("The guide switched on for the route's fresh shop, as a new game outside a route starts.");
            TakeInput();
            s.SetCard(GrowthCard.Welcome); yield return null; yield return null;
            Check(s.Ui.CardText().Contains("Your first night is guided") && s.Ui.CardText().Contains("Play without guidance") && s.Ui.CardText().Contains("your first night"),
                "The welcome says the first night is guided and offers to play without guidance");
            Check(!s.Ui.CardText().Contains("greybox"), "The welcome no longer calls the game a greybox");
            yield return Shot("300-welcome-guided");
            s.SetCard(GrowthCard.None);

            // Step 1: the growers. The next-step line says it; the marker points at the Bloom grower.
            Stand(GrowthWorld.Spawn, -90, 8); yield return null; yield return null; yield return null;
            var g = shop.GuideNow();
            Check(g != null && g.Step == GrowthGuideStep.Collect && g.Target == "grower:bloom", "Step 1: fill the siphon, at the Bloom grower first");
            Check(s.Ui.HudNext == g.Text, "The next-step line says the guide's step: " + s.Ui.HudNext);
            Check(s.Ui.GuideTarget == "grower:bloom" && s.Ui.GuideLabel == "Bloom grower", "The marker points at the Bloom grower (" + s.Ui.GuideTarget + ", " + s.Ui.GuideLabel + ")");
            yield return Shot("301-guide-collect");
            // Facing away from it, the marker waits at the screen's edge, pointing the way.
            var bloom = GrowthWorld.GrowerPositions["bloom"];
            LookAt(bloom + new Vector3(1.4f, 0, 0), bloom + new Vector3(8f, 1.2f, 0));
            yield return null; yield return null; yield return null;
            Check(s.Ui.GuideTarget == "grower:bloom" && s.Ui.GuideAtEdge, "Facing away, the marker is at the screen's edge, pointing the way");
            yield return Shot("302-guide-edge");
            LookAt(bloom + new Vector3(1.4f, 0, 0), bloom + new Vector3(0, 1.0f, 0));
            yield return null; yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "bloom", "The reticle focuses the Bloom grower");
            Check(s.Ui.GuideTarget == "grower:bloom" && !s.Ui.GuideAtEdge && s.Ui.GuidePoint.y > 0, "Facing it, the marker stands over the Bloom grower");
            Hold(true);
            yield return Wait(() => shop.Held.Contains("bloom"), 6, "a Bloom dose from a held mouse");
            Hold(false); yield return null; yield return null;
            g = shop.GuideNow();
            Check(g.Step == GrowthGuideStep.Collect && g.Target == "grower:flow" && s.Ui.GuideLabel == "Flow grower" && s.Ui.HudNext.StartsWith("Now the Flow grower"), "With a Bloom dose, the guide moves on to the Flow grower");
            var flow = GrowthWorld.GrowerPositions["flow"];
            LookAt(flow + new Vector3(1.4f, 0, 0), flow + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Hold(true);
            yield return Wait(() => shop.Held.Contains("flow"), 6, "a Flow dose from a held mouse");
            Hold(false); yield return null; yield return null;

            // Step 2: the incubator. Its card hides the marker; doses in and Start.
            g = shop.GuideNow();
            Check(g.Step == GrowthGuideStep.Mix && g.Target == "incubator", "Step 2: mix at the incubator");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.6f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null; yield return null;
            Check(s.Ui.GuideTarget == "incubator" && !s.Ui.GuideAtEdge && s.Ui.GuideLabel == "Incubator", "The marker stands over the incubator");
            yield return Shot("303-guide-incubator");
            Aimed(GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Incubator && s.Ui.GuideTarget == null, "E opens the incubator card; the marker hides while a card is open");
            Ok(s.Act(() => shop.AddDose("bloom")), "Add the Bloom dose");
            Ok(s.Act(() => shop.AddDose("flow")), "Add the Flow dose");
            Ok(s.Act(shop.StartIncubation), "Start the incubator");
            s.SetCard(GrowthCard.None); yield return null; yield return null;
            g = shop.GuideNow();
            Check(g.Step == GrowthGuideStep.Take && s.Ui.HudNext.StartsWith("Your creature is growing"), "Step 3, while it grows: take it when it hatches");
            s.Advance(shop.IncubationSeconds + 0.05f); yield return null; yield return null;
            Check(shop.Hatchling != null && shop.GuideNow().Text.StartsWith("It hatched") && s.Ui.GuideLabel == "Take it here", "It hatched: the guide says to take it, at the incubator");
            Ok(s.Act(shop.TakeHatchling), "Take the hatchling");

            // Step 4: the tank in the shop. Then a step undone is asked for again.
            var tank = shop.Containers[0];
            var a1 = GrowthWorld.Places["a1"];
            LookAt(a1.Browse + new Vector3(0, 0, -0.6f), a1.Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null; yield return null;
            g = shop.GuideNow();
            Check(g.Step == GrowthGuideStep.Stock && g.Target == "container:" + tank.Id && s.Ui.GuideLabel == "Starter tank", "Step 4: put it in the Starter tank");
            Check(s.Ui.GuideTarget == g.Target && !s.Ui.GuideAtEdge, "The marker stands over the Starter tank");
            yield return Shot("304-guide-tank");
            Aimed(GrowthTargetKind.Tank, tank.Id, "The reticle focuses the Starter tank");
            yield return Press(Key.E);
            Check(tank.Occupants.Count == 1, "E puts it in the tank");
            Check(shop.GuideNow().Step == GrowthGuideStep.Open && shop.GuideNow().Target == "counter", "Step 5: open the shop at the counter");
            Ok(s.Act(() => shop.TakeOut(tank.Id, tank.Occupants[0].Id)), "Take it back out of the tank");
            Check(shop.GuideNow().Step == GrowthGuideStep.Stock, "A step undone is asked for again: put it in the tank");
            Ok(s.Act(() => shop.PutIn(tank.Id)), "Put it back in");

            // Steps 5 and 6: open the shop, a visitor queues, a held mouse bags the first sale.
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null; yield return null;
            Check(s.Ui.GuideTarget == "counter" && s.Ui.GuideLabel == "Counter", "The marker stands over the counter");
            yield return Shot("305-guide-counter");
            Ok(s.Act(shop.OpenShop), "Open the shop");
            yield return null; yield return null;
            Check(shop.GuideNow().Step == GrowthGuideStep.Sell && s.Ui.HudNext.StartsWith("Visitors queue at the counter"), "Step 6: bag each sale at the counter");
            yield return Wait(() => shop.FrontOfQueue != null, 30, "a visitor to choose and queue");
            var front = shop.FrontOfQueue;
            yield return Wait(() => s.World.VisitorViews.TryGetValue(front.Id, out var view) && view.Arrived, 20, "the visitor to reach the queue");
            yield return null;
            Aimed(GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            yield return Shot("306-guide-sell");
            Hold(true);
            yield return Wait(() => shop.SalesTotal >= 1, 6, "the held mouse to bag the first sale");
            Hold(false); yield return null; yield return null;
            Check(shop.GuideNow().Step == GrowthGuideStep.FirstDay && s.Ui.HudNext.StartsWith("Your first sale!"), "After the first sale: keep serving until the day ends");
            yield return Shot("307-guide-first-sale");

            // The rest of the day on the shop's clock (accelerated); its receipt ends the guide.
            for (int i = 0; i < 2000 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(0.5f);
                if (i % 40 == 0) yield return null;
            }
            yield return null;
            Check(shop.Phase == GrowthPhase.Night && shop.Day == 2 && s.Card == GrowthCard.Receipt, "The day ends and its receipt opens");
            s.SetCard(GrowthCard.None);
            yield return Wait(() => shop.Guide == GrowthGuideMode.Done, 3, "the guide to end after the receipt");
            yield return null; yield return null;
            Check(shop.GuideNow() == null && s.Ui.GuideTarget == null && s.Ui.HudNext == shop.Objectives().First(x => !x.done).text,
                "After the first day the guide is done: no marker, and the next-step line shows the next goal again (" + s.Ui.HudNext + ")");
            Check(s.Ui.Log.Any(l => l.StartsWith("That was your first day!")), "A note says the first day is done and where the goals are");
            yield return Shot("308-guide-done");
            Check(s.SaveNow() && s.ReloadSaved() && s.Shop.Guide == GrowthGuideMode.Done, "Saved and restored, the guide stays done");

            // Skipping it from the pause screen: nothing else changes.
            s.StartOver(); yield return null;
            shop = s.Shop;
            shop.Guide = GrowthGuideMode.On;
            limitations.Add("Start over's new shop has its guide switched on for the route, as outside a route.");
            s.SetCard(GrowthCard.Pause); yield return null; yield return null;
            Check(s.Ui.CardText().Contains("Guided first night") && s.Ui.CardText().Contains("Skip it"), "The pause screen offers to skip the guided first night");
            yield return Shot("309-pause-skip");
            int credits = shop.Credits;
            Submit("Skip it", "Press Skip it");
            yield return null;
            Check(shop.Guide == GrowthGuideMode.Skipped && shop.Credits == credits && !s.Ui.CardText().Contains("Skip it"), "Skipped: the guide stops, and nothing else changes");
            s.SetCard(GrowthCard.None); yield return null; yield return null;
            Check(s.Ui.GuideTarget == null && s.Ui.HudNext == shop.Objectives().First(x => !x.done).text, "Skipped: no marker, the next-step line shows the goals");
            Check(s.SaveNow() && s.ReloadSaved() && s.Shop.Guide == GrowthGuideMode.Skipped, "Saved and restored, the guide stays skipped");

            // Skipping it from the welcome.
            s.StartOver(); yield return null;
            Check(s.Card == GrowthCard.Welcome, "Start over opens the welcome");
            s.Shop.Guide = GrowthGuideMode.On;
            s.SetCard(GrowthCard.Welcome); yield return null;
            Submit("Play without guidance", "Press Play without guidance");
            yield return null;
            Check(s.Shop.Guide == GrowthGuideMode.Skipped && s.Card == GrowthCard.None, "Play without guidance skips it and starts playing");

            // An old save (version 10, before GR-14) has no guide: an established shop is never put through it.
            var old = s.Shop.Snapshot(); old.version = 10; old.guide = null;
            var restored = GrowthShop.Restore(old, out string problem);
            Check(restored != null && restored.Guide == GrowthGuideMode.Off && restored.GuideNow() == null, "An old save loads without the guide" + (problem == null ? "" : " / " + problem));
        }
    }
}
#endif
