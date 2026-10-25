#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-08's Windows route (-evolutionGrowthLineSmoke): the workshop opened at its partition in the annex's east doorway; the
    /// sorter and lab racks bought at their places; the twin turbine standing in the workshop; the hatchling chute laid in pipe
    /// mode from the sorter to the incubator, with its preview; stock targets on the sorter's card; the auto-doser by targets
    /// and the sorter running the line, a hatchling sliding down the chute, the shop's tanks stocked to their targets and the
    /// rest in the racks; the analyzer's hint on the research bench; a full day on the line opening era 5; the planner; save
    /// and reload.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string LineScenarioText = "GR-08 hatchery line and analyzer: the workshop, the sorter and lab racks, the twin turbine in the workshop, the hatchling chute laid in pipe mode with its preview, stock targets, the auto-doser by targets, the line stocking the shop's tanks and the racks, the analyzer's hint, a full day on the line opening era 5, the planner, save and reload";

        static readonly Vector3 SorterFront = GrowthWorld.SorterPosition + new Vector3(0, 0, -2.2f), SorterAim = GrowthWorld.SorterPosition + new Vector3(0, 0.7f, 0);

        IEnumerator LineScenario()
        {
            limitations.Add("GR-08 line route: the fixture is labelled: an era 4, five-star shop (reputation 1710) with 20,000 credits and 400 research points, unit B open, 12 founders and a Shellback known, Ember owned, growers at level 3, the bubble turbine, electric pumps, the incubator at level 4, the breeding tank at level 2, the lab annex open with buffers 1 and 2 piped from the Bloom and Flow growers and to the incubator's port (laid by the shop's actions), and the workshop closed. Buying, racks, targets, the auto-doser's mode and switch call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked); the chute's pipe mode starts as the sorter card's button starts it, and the route aims at the incubator and presses E; the line runs on the shop's own clock (accelerated); the day is run on the shop's clock with the counter served as holding it does. Placeholder art: plain shapes for the workshop, the sorter, the chute and the racks.");
            yield return Opening();
            TakeInput();

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[4] + 10, Credits = 20000, Rp = 400, UnitB = true };
            fx.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).Select(x => x.Id));
            fx.Discovered.Add("shellback");
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Cultures.Add("ember");
            fx.Levels["grower"] = 3; fx.Levels["generator"] = 2; fx.Levels["pump"] = 2; fx.Levels["incubator"] = 4; fx.Levels["breeding"] = 2;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            fx.Annex = true;
            Ok(fx.BuyBuffer(0), "Fixture buffer 1"); Ok(fx.BuyBuffer(1), "Fixture buffer 2");
            Ok(fx.LayPipe(GrowthLab.Tap("bloom"), GrowthLab.Buffer(0)), "Fixture Bloom line"); Ok(fx.LayPipe(GrowthLab.Tap("flow"), GrowthLab.Buffer(1)), "Fixture Flow line");
            Ok(fx.LayPipe(GrowthLab.Buffer(0), GrowthLab.Port), "Fixture port pipe 1"); Ok(fx.LayPipe(GrowthLab.Buffer(1), GrowthLab.Port), "Fixture port pipe 2");
            fx.BufferAt(0).Units = fx.BufferAt(1).Units = GrowthContent.BufferCapacity;
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.Stars == 5 && shop.Annex && !shop.Workshop && shop.Pipes.Count == 4 && shop.AutoDoser && s.World.WorkshopWallStanding,
                "Fixture: an era 4, five-star shop with the lab annex piped to the incubator's port, and the workshop closed");

            // The workshop: its partition in the annex's east doorway, opened at night.
            yield return AimAt(new Vector3(2.4f, 0, 10.3f), new Vector3(4f, 1.4f, 10.3f), GrowthTargetKind.WorkshopWall, "workshop", "The reticle focuses the workshop's partition");
            Check(s.FocusName(out string status) == "Workshop" && status == "E see what it needs", "It is the workshop");
            yield return Shot("110-workshop-closed");
            yield return Press(Key.E);
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Workshop && card.Contains("Open the workshop for 5000 cr"), "E opens its card: open it for 5,000 credits at night");
            Ok(s.Act(shop.BuyWorkshop), "Open the workshop");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.Workshop && !s.World.WorkshopWallStanding && shop.Objectives().Any(o => o.text.StartsWith("Open the workshop") && o.done), "The partition is gone and the goal ticked");

            // The sorter's place against the workshop's north wall.
            yield return AimAt(SorterFront, SorterAim, GrowthTargetKind.Sorter, "sorter", "The reticle focuses the sorter's place");
            Check(s.FocusName(out status) == "Sorter (not bought)" && status.Contains("3000 credits"), "Not bought yet: 3,000 credits");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Sorter && card.Contains("The sorter · level 0 of 1") && card.Contains("Buy · 3000 cr"), "Its card: the chute and sorter for 3,000 credits");
            Ok(s.Act(() => shop.Upgrade("sorter")), "Buy the sorter");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.HasSorter && s.World.SorterLookLevel == 1 && shop.PowerOrder.Contains(GrowthShop.SorterPower) && s.World.OutletShowing, "The sorter stands in the workshop, joins the power board, and the incubator has its outlet");
            // The generator's twin turbine stands in the workshop now.
            Check(shop.UpgradeCheck("generator", out var twin, out _) == GrowthFailure.None && twin.Level == 3, "The twin turbine can be bought now the workshop is open");
            Ok(s.Act(() => shop.Upgrade("generator")), "The twin turbine (3,000 credits)");
            yield return null; yield return null;
            Check(shop.GeneratorUnits == 6 && s.World.GeneratorInWorkshop && s.World.GeneratorLookLevel == 3, "Six units; the generator stands in the workshop's corner with two turbine columns");

            // Lab racks on the rack places.
            yield return AimAt(new Vector3(7.0f, 0, 11.6f), new Vector3(7.0f, 0.45f, 9.6f), GrowthTargetKind.Place, "r1", "The reticle focuses rack place 1");
            Check(s.FocusName(out status) == "Rack place 1" && status == "E place a lab rack", "It is a rack place");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Place && card.Contains("Place a lab rack for 30 cr"), "Its card: a lab rack for 30 credits");
            Ok(s.Act(() => shop.BuyRack("r1")), "Place a lab rack on rack place 1");
            Ok(s.Act(() => shop.BuyRack("r2")), "Place a lab rack on rack place 2");
            var rack1 = shop.ContainerAt("r1");
            Ok(s.Act(() => shop.UpgradeContainer(rack1.Id)), "Rack 1 to level 2"); Ok(s.Act(() => shop.UpgradeContainer(rack1.Id)), "Rack 1 to level 3");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(s.World.BuiltRacks == 2 && shop.MoveCheck(rack1.Id, "a2", out string moveWhy) != GrowthFailure.None && moveWhy == "Racks stay on their rack places in the workshop",
                "Two racks stand in the workshop, and they stay there");

            // The chute: from the sorter's card, pipe mode to the incubator, previewed, laid with E.
            yield return AimAt(SorterFront, SorterAim, GrowthTargetKind.Sorter, "sorter", "The reticle focuses the sorter");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("Lay the chute from the incubator") && card.Contains("No targets yet: every hatchling goes to a rack"), "The sorter's card: lay the chute; no targets yet");
            s.StartPipe(GrowthLab.Sorter);
            Check(s.Card == GrowthCard.None && s.PipeFrom == GrowthLab.Sorter, "Pipe mode, from the sorter (as the card's button starts it)");
            yield return AimAt(GrowthWorld.IncubatorPosition + new Vector3(0.4f, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0.1f, 1.0f, 0), GrowthTargetKind.Incubator, "incubator", "Aimed at the incubator");
            yield return null;
            Check(s.PipeTo == GrowthLab.Out && s.PipeReady && s.PipePlan != null && s.PipePlan.Chute && s.World.GhostShowing && s.FocusName(out status) == "Pipe from the sorter" &&
                status.Contains("to the incubator's outlet") && status.Contains("E lay it"), "The preview: the chute from the incubator's outlet, its length and cost, E to lay it");
            yield return Shot("111-chute-preview");
            int credits = shop.Credits, cost = s.PipePlan.Paid;
            yield return Press(Key.E);
            yield return null; yield return null;
            Check(shop.Chute != null && shop.Credits == credits - cost && s.PipeFrom == null && s.World.ChuteBuilt && shop.Objectives().Any(o => o.text.StartsWith("Buy the sorter") && o.done),
                "E lays it: the chute runs from the incubator's outlet over the annex to the sorter, and the goal is ticked");
            LookAt(new Vector3(3.4f, 0, 9.5f), new Vector3(1.4f, 3.0f, 13.4f)); yield return Shot("112-chute-overhead");

            // Stock targets on the sorter's card, and the auto-doser by targets.
            Ok(s.Act(() => shop.SetTarget("pondlet", 2)), "Target 2 Pondlets (the card's + button)");
            Ok(s.Act(() => shop.SetTarget("ribbonfin", 2)), "Target 2 Ribbonfins");
            Ok(s.Act(shop.SetAutoByTargets), "The auto-doser by stock targets (the incubator card's button)");
            Ok(s.Act(() => shop.SwitchAutoDoser(true)), "Switch the auto-doser on");
            Check(shop.TargetRecipe() == "pondlet" && shop.LineRunning, "The line runs: the first batch is the first of the catalog furthest below its target");
            var tank = shop.ShopContainers.Single();
            bool beadShot = false;
            for (int i = 0; i < 1200 && (shop.Shown("pondlet") < 2 || shop.Shown("ribbonfin") < 2 || shop.Hatchlings.Count > 0); i++)
            {
                var use = shop.PowerUse(GrowthShop.SorterPower);
                bool sorting = shop.Hatchlings.Count > 0 && use != null && use.Got > 0;
                if (!beadShot && sorting && shop.SortProgress >= 0.88f && shop.SortProgress <= 0.92f)
                {
                    LookAt(new Vector3(6.2f, 0, 11.4f), new Vector3(6.0f, 2.0f, 13.6f));
                    yield return null;
                    Check(s.World.BeadShowing, "A hatchling slides down the chute into the sorter's hopper while it sorts");
                    yield return Shot("113-sorting");
                    beadShot = true;
                }
                s.Advance(sorting ? 0.02f : 0.25f);
                if (i % 10 == 0) yield return null;
            }
            Check(beadShot, "The sorting was seen");
            Check(shop.Shown("pondlet") == 2 && shop.Shown("ribbonfin") == 2 && tank.Occupants.All(o => o.FromLine) && shop.Racks.Sum(r => r.Occupants.Count) == 2 && shop.Racks.All(r => r.Occupants.All(o => !o.FromLine)),
                "Two clutches: the shop's tank stocked to its targets by the line, the rest in the racks");
            Check(shop.AutoStatus == "Every stock target the port can make is met" && shop.SorterStatus == "Waiting for a clutch", "Both wait with their targets met, saying so");
            yield return null; yield return null;
            Check(s.World.Tanks.Values.Where(t => t.Place == "r1" || t.Place == "r2").Sum(t => t.Creatures.Count) == 2, "The racks show their creatures");
            LookAt(new Vector3(8.0f, 0, 11.8f), new Vector3(7.9f, 0.8f, 9.6f)); yield return Shot("114-racks");
            yield return AimAt(SorterFront, SorterAim, GrowthTargetKind.Sorter, "sorter", "The reticle focuses the sorter again");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("Targets: Pondlet 2/2, Ribbonfin 2/2") && card.Contains("Chute: from the incubator's outlet to the sorter") && card.Contains("Racks: rack 1 2/12, rack 2 0/4"),
                "The sorter's card: the targets met, the chute, the racks");
            yield return Shot("115-sorter-card");
            s.SetCard(GrowthCard.None);
            yield return AimAt(new Vector3(2.6f, 0, 12.5f), GrowthWorld.PowerBoardPosition - new Vector3(0.05f, 0, 0), GrowthTargetKind.PowerBoard, "board", "The reticle focuses the power board");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("Sorter · draws 1 · Waiting for a clutch") && card.Contains("Auto-doser · draws 1"), "The power board lists the sorter with what it draws and does");
            s.SetCard(GrowthCard.None);

            // The analyzer: the research bench's level 3 names one new thing within reach a day.
            for (int lv = 2; lv <= 3; lv++) Ok(s.Act(() => shop.Upgrade("bench")), "Research bench level " + lv);
            LookAt(GrowthWorld.WorkbenchPosition + new Vector3(-1.25f, 0, 0), GrowthWorld.WorkbenchPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Workbench, null, "The reticle focuses the research bench");
            yield return Press(Key.E);
            s.Ui.ScrollTo("Analyzer");
            card = s.Ui.CardText();
            Check(shop.Hint != null && card.Contains("Analyzer · today's hint") && card.Contains(shop.HintText) && !card.Contains(GrowthContent.Get(shop.Hint.Species).Name),
                "The bench's card: the analyzer's hint, without naming the creature (" + shop.HintText + ")");
            yield return Shot("116-analyzer");
            s.SetCard(GrowthCard.None);

            // A full day on the line: the auto-doser and the sorter running from opening to close.
            Ok(s.Act(shop.OpenShop), "Open the shop (as E at the counter does)");
            Check(shop.LineToday, "The line runs from opening");
            int served = 0;
            for (int i = 0; i < 900 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (shop.Phase == GrowthPhase.Day && shop.FrontOfQueue != null && shop.Serve(shop.ServeSeconds).Detail == "sold") served++;
                if (i % 10 == 0) yield return null;
            }
            yield return null; yield return null;
            Check(shop.Phase == GrowthPhase.Night && served >= GrowthShop.LineDaySales && shop.LineDays == 1 && s.Ui.Log.Any(l => l.StartsWith("A full day on the line")),
                "Through the day (accelerated, " + served + " sales served) the line kept the shop stocked: a full day on the line");
            Check(shop.Era == 5 && s.Ui.Log.Any(l => l.StartsWith("Era 5")) && shop.Objectives().Last().text.StartsWith("Host it: six exhibition guests"),
                "Five stars, a large creature and a full day on the line open era 5, whose goal is the Grand Exhibition");
            yield return Shot("117-era-5");
            s.SetCard(GrowthCard.None);

            // The planner: the research bench's level 4, in era 5.
            Ok(s.Act(() => shop.Upgrade("bench")), "Research bench level 4, the planner (4,000 credits and 200 RP)");
            LookAt(GrowthWorld.WorkbenchPosition + new Vector3(-1.25f, 0, 0), GrowthWorld.WorkbenchPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            yield return Press(Key.E);
            s.Ui.ScrollTo("Planner");
            card = s.Ui.CardText();
            var child = shop.PlannerPredicts("pondlet", "bubblebelly");
            Check(shop.Planner && child != null && card.Contains("A Pondlet and a Bubblebelly make a " + child.Name) && s.World.LookLevel("bench") == 4,
                "The planner predicts what two known species breed: a Pondlet and a Bubblebelly make a " + child?.Name);
            yield return Shot("118-planner");
            s.SetCard(GrowthCard.None);

            // Save and reload: the workshop, racks, chute, targets, the line's days and the hint come back.
            Check(s.SaveNow(), "Save");
            string saved = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            shop = s.Shop;
            var file = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            Check(JsonUtility.ToJson(GrowthShop.Restore(file, out _).Snapshot()) == saved && file.version >= 7 && shop.Workshop && shop.HasSorter && shop.Chute != null && shop.Racks.Count() == 2 &&
                shop.Target("pondlet") == 2 && shop.AutoByTargets && shop.LineDays == 1 && shop.Hint != null && shop.Era == 5,
                "The version 7 (and later) save brings the line back: the workshop, racks, the chute, the targets, the line's day and the analyzer's hint");
            yield return null; yield return null;
            Check(!s.World.WorkshopWallStanding && s.World.BuiltRacks == 2 && s.World.ChuteBuilt && s.World.SorterLookLevel == 1 && s.World.GeneratorInWorkshop, "The reloaded workshop looks the same");
            LookAt(new Vector3(4.8f, 0, 10.2f), new Vector3(9.5f, 1.0f, 13.2f)); yield return Shot("119-workshop");
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
