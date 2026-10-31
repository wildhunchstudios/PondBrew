#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    /// <summary>
    /// GR-31's Windows route (-evolutionGrowthFloorSmoke): tester round 1's batch 2, played in the real game. Running, and a way
    /// behind the counter walked with the real body; peek only once standing still; quick recipes and upgrades as numbers;
    /// the counter's cue and bell, what a visitor picked and bought, a boop, and upgrades by day; the shop opening by itself;
    /// Nori working with no targets, the targets table, and a tank's rule.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string FloorScenarioText = "GR-31 tester round 1, batch 2: running and the way behind the counter; peek once still; quick recipes and upgrades as numbers; the counter's cue and bell, picked and bought icons, a boop, upgrades by day; the auto-open; Nori with no targets, the targets table, and a tank's rule.";

        /// <summary>Walks with the keys held for a while, or until a point is reached; how far it went and how long it took.</summary>
        IEnumerator WalkKeys(float seconds, System.Func<bool> until, params Key[] keys)
        {
            float start = Time.realtimeSinceStartup;
            Vector3 from = s.Player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            while (Time.realtimeSinceStartup - start < seconds && (until == null || !until())) yield return null;
            walked = Vector3.Distance(from, s.Player.transform.position);
            walkedFor = Time.realtimeSinceStartup - start;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }
        float walked, walkedFor;

        GrowthVisitor Arrive(string containerId, string type = "bargain") => s.Shop.DevelopmentArrive(type, null, containerId, 0.2f);

        IEnumerator FloorScenario()
        {
            limitations.Add("GR-31 route: the fixture is labelled: an era 4, four-star shop with 30,000 credits, unit B open, 12 founders known and four of their recipes found, a Large tank at a3, and racks in the workshop.");
            yield return Opening();
            TakeInput();
            s.AllowValidationInput = true;

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 30000, Rp = 300, UnitB = true, SalesTotal = 160 };
            var founders = GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).ToList();
            fx.Discovered.AddRange(founders.Select(x => x.Id));
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            foreach (var f in founders.Take(5)) fx.MixLog.Add(new GrowthMixEntry { Doses = (int[])f.Recipe.Clone(), Species = f.Id, Exact = true });
            fx.Levels["grower"] = 2; fx.Levels["generator"] = 2; fx.Levels["pump"] = 2; fx.Levels["incubator"] = 1; fx.Levels["counter"] = 1; fx.Levels["breeding"] = 1;
            fx.Annex = true;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            fx.Containers.Add(new GrowthContainer { Id = "floor-large", Kind = "large", Place = "a3" });
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.Containers.Any(c => c.Place == "a3" && c.Kind == "large"), "Fixture: an era 4 shop with a Large tank and no helpers");

            // ---- F22: the run button is faster than walking
            var gap = new Vector3(1.32f, 0, -0.2f);
            Stand(gap, 180, 0);
            yield return null; yield return null;
            yield return WalkKeys(0.45f, null, Key.W);
            float walking = walked / walkedFor;
            Stand(gap, 180, 0);
            yield return null; yield return null;
            yield return WalkKeys(0.45f, null, Key.W, Key.LeftShift);
            float running = walked / walkedFor;
            Check(walking > 2.2f && running > walking * 1.3f, "Left Shift runs: " + walking.ToString("0.0") + " m/s walking, " + running.ToString("0.0") + " m/s running");

            // ---- F24: the real body fits between the catalog and the counter, and walks behind the counter
            Stand(gap, 180, 0);
            yield return null; yield return null;
            yield return WalkKeys(3, () => s.Player.transform.position.z < -3.45f, Key.W);
            Check(s.Player.transform.position.z < -3.45f, "Walking south, the body passes between the catalog and the counter (z " + s.Player.transform.position.z.ToString("0.00") + ")");
            Stand(new Vector3(s.Player.transform.position.x, 0, -3.5f), 90, 0);
            yield return null; yield return null;
            yield return WalkKeys(3, () => s.Player.transform.position.x > 3.2f, Key.W);
            Check(s.Player.transform.position.x > 3.2f, "Walking east, the body gets behind the counter (x " + s.Player.transform.position.x.ToString("0.00") + ")");
            LookAt(s.Player.transform.position, GrowthWorld.CounterPosition + new Vector3(0, 0.9f, 0));
            yield return Shot("1200-behind-the-counter");

            // ---- F23: no zooming into a tank in passing
            var a1 = GrowthWorld.Places["a1"];
            LookAt(a1.Browse + new Vector3(0, 0, 0.15f), a1.Position + new Vector3(0, 1.18f, -0.1f));
            yield return null;
            Check(!s.Peeking, "Not peeking the moment it comes into view");
            yield return WalkKeys(0.5f, null, Key.W);
            Check(!s.Peeking, "Walking while looking at a tank does not zoom");
            yield return Wait(() => s.Peeking, 3, "peek once standing still");
            Check(s.Peeking, "Standing still, it does");

            // ---- F26 and F27: three quick recipes, the rest behind a chooser
            yield return AimAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0), GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Press(Key.E);
            yield return null; yield return null;
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Incubator && card.Contains("QUICK RECIPES") && card.Contains("Choose your 3 (5 found)") && shop.QuickRecipes().Count == 3, "The incubator's card has three quick recipes and offers the rest (5 found)");
            yield return Shot("1201-quick-recipes");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Choose your 3"), "Press Choose your 3");
            yield return null;
            Check(s.Ui.CardText().Contains("Close the list") && s.Ui.CardText().Contains(founders[0].Name), "The list shows every recipe found, each with its pin");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "Pin", "Press Pin");
            yield return null;
            Check(shop.PinnedRecipes.Count == 1 && shop.QuickRecipes()[0] == shop.PinnedRecipes[0], "A pinned recipe comes first");
            yield return Shot("1202-recipe-list");

            // ---- F28: an upgrade reads as numbers
            card = s.Ui.CardText();
            Check(card.Contains("→") && card.Contains("Takes") && card.Contains("Doses in a mix"), "The incubator's upgrade says what changes, as numbers");
            s.SetCard(GrowthCard.None);
            yield return null;

            // ---- F32: the shop opens by itself, once there is enough on show and the night's first seconds have passed
            var tank = shop.Containers.First(c => c.Place == "a1");
            var large = shop.Containers.First(c => c.Place == "a3");
            for (int i = 0; i < 3; i++) Ok(shop.DevelopmentStock(tank.Id, "pondlet"), "Stock a Pondlet");
            for (int i = 0; i < 3; i++) Ok(shop.DevelopmentStock(large.Id, "pondlet"), "Stock a Pondlet in the Large tank");
            var counterFront = GrowthWorld.CounterPosition + new Vector3(0, 0, -1.25f);
            var counterAim = GrowthWorld.CounterPosition + new Vector3(0, 0.9f, 0);
            yield return AimAt(counterFront, counterAim, GrowthTargetKind.Counter, null, "From behind it, the reticle focuses the counter");
            yield return Press(Key.E);
            yield return null; yield return null;
            Check(s.Card == GrowthCard.Counter && s.Ui.CardText().Contains("Open by itself") && s.Ui.CardText().Contains("Off: the shop opens when you open it here."), "The counter's card offers the auto-open, off");
            yield return ClickOn(e => e.name == "gr-autoopen-3", "Press 3");
            yield return null; yield return null;
            Check(shop.AutoOpenAt == 3 && s.Ui.CardText().Contains("With 3 or more on show"), "Set to 3, the card says when it opens");
            yield return Shot("1203-auto-open");
            s.SetCard(GrowthCard.None);
            yield return Wait(() => shop.Phase == GrowthPhase.Day, GrowthShop.AutoOpenWait + 5, "the shop opening by itself");
            Check(shop.Phase == GrowthPhase.Day && s.Ui.Log.Any(l => l.StartsWith("The shop opened by itself")), "With 6 on show the shop opened by itself, and says so");
            shop.AutoOpenAt = 0;

            // ---- F29 and F30: the bell and the cue as a visitor reaches the counter, with what they picked by their hand
            foreach (var v in shop.Visitors.ToList()) shop.Visitors.Remove(v);
            yield return null;
            var first = Arrive(tank.Id);
            Check(!s.Ui.CounterCue.on, "Nobody at the counter: no cue");
            int bells = s.Audio.Bells;
            LookAt(counterFront, counterAim);
            yield return Wait(() => s.World.VisitorViews.TryGetValue(first.Id, out var at) && at != null && first.State == GrowthVisitorState.Queued && s.Ui.CounterCue.on, 40, "the visitor to reach the counter");
            yield return new WaitForSeconds(0.1f);
            var cue = s.Ui.CounterCue;
            Check(cue.on && cue.words == "Ready: hold the mouse to bag", "A visitor at the counter: the cue says so (" + cue.words + ")");
            Check(s.Audio.Bells == bells + 1, "And the bell rings once (" + (s.Audio.Bells - bells) + ")");
            Check(s.Ui.PickedIcons.Contains("picked"), "Their pick shows by their hand (" + string.Join(", ", s.Ui.PickedIcons) + ")");
            yield return Shot("1204-counter-cue");

            // ---- F31: a boop: a giggle, their patience, and they stay
            var fv = s.World.VisitorViews[first.Id];
            LookAt(counterFront, fv.transform.position + Vector3.up * 1.3f);
            yield return Wait(() => s.Gags.SlapTarget == fv, 4, "the visitor under the reticle");
            yield return null; yield return null;
            Check(s.Ui.BoopPromptShown, "Within reach, the prompt offers a boop");
            yield return Wait(() => shop.SlapWait <= 0, 3, "the hand to be free");
            int boops = s.Gags.Boops;
            yield return Press(Key.C);
            Check(first.Boops == 1 && first.State == GrowthVisitorState.Queued && s.Gags.Boops == boops + 1, "C boops them, and they stay in the queue");
            yield return new WaitForSeconds(0.3f);
            Check(GrowthComedy.Giggles.Contains(s.Gags.CaptionText), "They giggle (\"" + s.Gags.CaptionText + "\")");
            yield return Shot("1205-boop");

            // ---- F33: the counter's upgrade with visitors waiting
            yield return AimAt(counterFront, counterAim, GrowthTargetKind.Counter, null, "Back at the counter");
            yield return Press(Key.E);
            yield return null; yield return null;
            card = s.Ui.CardText();
            int counterLevel = shop.Level("counter");
            Check(s.Card == GrowthCard.Counter && !card.Contains("Visitors are waiting") && shop.UpgradeCheck("counter", out _, out _) == GrowthFailure.None, "With a visitor waiting, the counter's upgrade is still there");
            s.Ui.ShowTabNamed("Upgrade");
            yield return null; yield return null;
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Upgrade") && b.text != "Upgrade", "Press Upgrade");
            Check(shop.Level("counter") == counterLevel + 1 && first.State == GrowthVisitorState.Queued, "It upgrades with the shop open and the visitor still waiting");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;

            // ---- F29: bagging shows on the cue; F30: what they bought by their hand as they leave
            LookAt(counterFront, counterAim);
            yield return null; yield return null;
            Hold(true);
            yield return Wait(() => s.ServingNow && shop.ServeProgress > 0.05f, 4, "bagging to start");
            yield return null;
            Check(s.Ui.CounterCue.words == "Bagging...", "Holding the mouse, the cue says it is bagging");
            yield return Shot("1206-bagging");
            yield return Wait(() => first.State == GrowthVisitorState.Leaving, 12, "the sale");
            Hold(false);
            yield return null; yield return null;
            Check(first.Outcome == GrowthVisitOutcome.Bought && first.BoughtSpecies == "pondlet", "The sale is made");
            Check(s.Ui.PickedIcons.Contains("bought"), "What they bought shows as they leave (" + string.Join(", ", s.Ui.PickedIcons) + ")");
            yield return Shot("1207-bought");

            // ---- a tank upgrades by day too
            yield return AimAt(a1.Browse + new Vector3(0, 0, -0.6f), a1.Position + new Vector3(0, 1.1f, 0), GrowthTargetKind.Tank, tank.Id, "The reticle focuses the Starter tank");
            yield return Press(Key.E);
            yield return null; yield return null;
            int tankLevel = tank.Level;
            Check(s.Card == GrowthCard.Tank && !s.Ui.CardText().Contains("Fitted at night") && shop.ContainerUpgradeCheck(tank.Id, out _, out _) == GrowthFailure.None, "By day the tank's upgrade is on offer");
            s.Ui.ShowTabNamed("Upgrade");
            yield return null; yield return null;
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Upgrade") && b.text != "Upgrade", "Press Upgrade");
            Check(tank.Level == tankLevel + 1, "And it upgrades, the shop open");
            s.SetCard(GrowthCard.None);

            // ---- F34 and F35: Nori with no targets, and the targets as a table
            // Helpers are hired at night: the day is ended here (its clock advanced: labelled).
            s.Advance(shop.DaySecondsLeft + 1);
            yield return Wait(() => shop.Phase == GrowthPhase.Night, 8, "the night");
            yield return null;
            s.SetCard(GrowthCard.None);
            yield return null;
            shop.Reputation = GrowthContent.StarThresholds[3] + 10;
            Ok(shop.Hire("nori"), "Hire Nori (the staff card's button)");
            yield return null; yield return null;
            var nori = shop.Helper("nori");
            Check(shop.StockTargets.Count == 0 && nori.Status == "Nothing waiting to move", "With nothing waiting, Nori says so (" + nori.Status + ")");
            s.SetCard(GrowthCard.Helper, "nori");
            yield return null; yield return null;
            card = s.Ui.CardText();
            Check(card.Contains("No targets yet: Nori fills the shop's tanks with whatever is waiting.") && card.Contains("In shop") && card.Contains("In racks"), "Nori's card has the targets as a table");
            yield return Shot("1208-targets-table");
            s.SetCard(GrowthCard.None);
            foreach (var c in shop.ShopContainers) c.Occupants.RemoveAll(o => o.ReservedBy == 0);
            shop.Hatchlings.Add(new GrowthSpecimen { Id = 9401, Species = "pondlet" });
            yield return Wait(() => nori.Step == "fetch", 3, "Nori setting off");
            yield return Wait(() => shop.Hatchlings.Count == 0 && nori.Hands.Count == 0, 40, "Nori carrying it");
            Check(shop.Stock.Any(o => o.Id == 9401), "With no targets at all, Nori carried the hatchling to a tank");

            // ---- F37: a tank's rule
            yield return AimAt(a1.Browse + new Vector3(0, 0, -0.6f), a1.Position + new Vector3(0, 1.1f, 0), GrowthTargetKind.Tank, tank.Id, "The reticle focuses the Starter tank");
            yield return Press(Key.E);
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("Nori and the sorter keep here") && s.Ui.CardText().Contains("Anything"), "The tank's card offers a rule");
            yield return ClickOn(e => e.name == "gr-rule-calm", "Press Calm");
            yield return null; yield return null;
            Check(tank.Rule == "calm", "The tank's rule is Calm");
            yield return Shot("1209-tank-rule");
            s.SetCard(GrowthCard.None);

            // ---- what's new is kept
            shop.AutoOpenAt = 6;
            s.SaveNow();
            var saved = GrowthShop.Restore(shop.Snapshot(), out string problem);
            Check(problem == null && saved.AutoOpenAt == 6 && saved.Containers.First(c => c.Id == tank.Id).Rule == "calm", "The auto-open and the tank's rule keep in the save");
        }
    }
}
#endif
