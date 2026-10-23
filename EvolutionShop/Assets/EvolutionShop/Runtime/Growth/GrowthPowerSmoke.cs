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
    /// GR-07's Windows route (-evolutionGrowthPowerSmoke): the lab annex opened at night; the generator's hand crank and the
    /// power board bought; buffers placed; pipes connected end to end in pipe mode with their preview; the hand pump cranked
    /// with the held mouse; the siphon at a buffer; electric pumps on the hand crank, in priority order; the bubble turbine
    /// opening era 4; fast pumps over budget, slowed and saying so; the incubator's auto-doser dosing a known recipe from the
    /// port's buffers; a pipe taken down at night; save and reload in the middle of a batch.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string PowerScenarioText = "GR-07 power and pipes: the lab annex, the generator's hand crank and bubble turbine, the power board's order, buffers, pipes connected end to end with their preview, the hand pump, electric and fast pumps over budget, era 4, the auto-doser, a pipe taken down, save and reload mid-batch";

        IEnumerator AimAt(Vector3 feet, Vector3 target, GrowthTargetKind kind, string id, string label)
        {
            LookAt(feet, target);
            yield return null; yield return null;
            Aimed(kind, id, label);
        }

        IEnumerator HoldAt(Vector3 feet, Vector3 target, GrowthTargetKind kind, string id, System.Func<bool> enough, string reason, string label)
        {
            yield return AimAt(feet, target, kind, id, label);
            Hold(true);
            yield return Wait(enough, 8, reason);
            Hold(false); yield return null;
        }

        static Vector3 BufferFront(int place) => GrowthWorld.BufferPosition(place) + new Vector3(0, 0, 1.4f);
        static Vector3 BufferAimPoint(int place) => GrowthWorld.BufferPosition(place) + new Vector3(0, 0.7f, 0);

        /// <summary>A grower's pump on its cabinet's front, in the world (the pump is built on the grower's own turn).</summary>
        static Vector3 PumpPoint(string culture) =>
            GrowthWorld.GrowerPositions[culture] + Quaternion.Euler(0, GrowthWorld.GrowerYaw(culture), 0) * new Vector3(0.26f, 0.42f, 0.48f);

        IEnumerator PowerScenario()
        {
            limitations.Add("GR-07 power route: the fixture is labelled: an era 3, four-star shop (reputation 860) with 10,000 credits and 40 research points, unit B open, 12 founders known, growers at level 3 (one tap each) holding 10 doses of Bloom, Flow and Ember. Buying, placing buffers, taking pipes down, the power board's order, the auto-doser's recipe and switch, and the incubator's levels call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked); pipe mode starts as the cards' buttons start it, and the route aims at each pipe's other end and presses E; the hand pump, the generator's crank and the siphon at a buffer are held with the route's mouse; electric pumping, incubation and dosing run on the shop's own clock (accelerated). Placeholder art: plain shapes for the annex, the generator, the power board, buffers, pumps and pipes.");
            yield return Opening();
            TakeInput();

            var fx = new GrowthShop { Era = 3, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 10000, Rp = 40, UnitB = true };
            fx.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).Select(x => x.Id));
            if (!fx.Discovered.Contains("pondlet")) fx.Discovered[0] = "pondlet";
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Cultures.Add("ember");
            fx.Levels["grower"] = 3;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            var starter = fx.Containers[0];
            Stock(fx, starter, "pondlet"); Stock(fx, starter, "pondlet");
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Era == 3 && shop.Stars == 4 && shop.Discovered.Count == 12 && shop.Taps == 1 && !shop.Annex && s.World.AnnexWallStanding,
                "Fixture: an era 3, four-star shop with 12 species, growers at level 3 and the lab annex closed");

            // The lab annex: its partition in the back room's north doorway, opened at night.
            yield return AimAt(new Vector3(0.5f, 0, 6.6f), new Vector3(1.15f, 1.4f, 9f), GrowthTargetKind.AnnexWall, "annex", "The reticle focuses the lab annex's partition");
            Check(s.FocusName(out string status) == "Lab annex" && status == "E see what it needs", "It is the lab annex");
            yield return Shot("100-annex-closed");
            yield return Press(Key.E);
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Annex && card.Contains("Open the lab annex for 1000 cr"), "E opens its card: open it for 1,000 credits at night");
            Ok(s.Act(shop.BuyAnnex), "Open the lab annex");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.Annex && !s.World.AnnexWallStanding && shop.Objectives().Any(o => o.text.StartsWith("Open the lab annex") && o.done), "The partition is gone and the goal ticked");

            // The generator (its hand crank) and the power board with pumps and pipes.
            var crank = GrowthWorld.GeneratorPosition + new Vector3(0, 0.9f, -0.3f);
            yield return AimAt(GrowthWorld.GeneratorPosition + new Vector3(0, 0, -1.4f), crank, GrowthTargetKind.Generator, "generator", "The reticle focuses the generator's place");
            Check(s.FocusName(out status) == "Generator (not bought)" && status.Contains("200 credits and 30 RP"), "Not bought yet: 200 credits and 30 RP");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Generator && card.Contains("The generator · level 0 of 4") && card.Contains("Upgrade to level 1 for 200 cr and 30 RP") && card.Contains("3. Twin turbine: 6 units · 3000 cr, era 4"),
                "Its card: the hand crank for 200 credits and 30 RP; the twin turbine in era 4 (GR-08: in the workshop)");
            Ok(s.Act(() => shop.Upgrade("generator")), "Buy the generator's hand crank");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.HandCrank && s.World.GeneratorLookLevel == 1, "The hand crank stands in the annex's corner");
            yield return AimAt(new Vector3(2.6f, 0, 12.5f), GrowthWorld.PowerBoardPosition - new Vector3(0.05f, 0, 0), GrowthTargetKind.PowerBoard, "board", "The reticle focuses the power board");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.PowerBoard && card.Contains("Pumps and pipes · level 0 of 3") && card.Contains("Upgrade to level 1 for 150 cr") && card.Contains("Nothing to power yet"),
                "The power board's card: nothing to power yet, and pumps and pipes for 150 credits");
            Ok(s.Act(() => shop.Upgrade("pump")), "Buy the hand pump and pipes");
            s.SetCard(GrowthCard.None);

            // Buffers at their places along the annex's south wall.
            yield return AimAt(BufferFront(0), BufferAimPoint(0), GrowthTargetKind.Buffer, "0", "The reticle focuses buffer place 1");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Buffer && card.Contains("Place a buffer for 60 cr"), "Its card: a buffer for 60 credits");
            for (int p = 0; p < 3; p++) { int place = p; Ok(s.Act(() => shop.BuyBuffer(place)), "Place buffer " + (place + 1)); }
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(s.World.BuiltBuffers == 3, "Three buffers stand at their places");

            // Pipes: from the Bloom grower's card to buffer 1, previewed along the grid, then laid with E.
            yield return AimAt(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom", "The reticle focuses the Bloom grower");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("Tap and pump") && card.Contains("Pipe its tap to a buffer"), "The grower's card offers to pipe its tap");
            s.StartPipe(GrowthLab.Tap("bloom"));
            Check(s.Card == GrowthCard.None && s.PipeFrom == GrowthLab.Tap("bloom"), "Pipe mode, from the Bloom tap (as the card's button starts it)");
            yield return AimAt(BufferFront(0) + new Vector3(0.4f, 0, 0.6f), GrowthWorld.BufferPosition(0) + new Vector3(0, 1.1f, 0), GrowthTargetKind.Buffer, "0", "Aimed at buffer 1");
            yield return null;
            Check(s.PipeReady && s.PipePlan != null && s.World.GhostShowing && s.FocusName(out status) == "Pipe from the Bloom tap" && status.Contains("E lay it") && status.Contains(s.PipePlan.Paid + " credits"),
                "The preview: its route along the grid, its length and cost, E to lay it");
            yield return Shot("101-pipe-preview");
            int credits = shop.Credits, cost = s.PipePlan.Paid;
            yield return Press(Key.E);
            yield return null;
            Check(shop.Pipes.Count == 1 && shop.Credits == credits - cost && shop.BufferAt(0).Culture == "bloom" && s.PipeFrom == null && !s.World.GhostShowing && s.World.BuiltPipes == 1,
                "E lays it: paid by its length, buffer 1 takes Bloom, pipe mode ends");
            yield return null;
            Check(s.World.PumpStanding("bloom"), "A hand pump on the Bloom grower's front");
            // The other way round: from buffer 2's card to the Flow grower.
            s.StartPipe(GrowthLab.Buffer(1));
            yield return AimAt(GrowthWorld.GrowerFront("flow"), GrowthWorld.GrowerPositions["flow"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "flow", "Aimed at the Flow grower");
            yield return Press(Key.E);
            Check(shop.Pipes.Count == 2 && shop.Pipes[1].From == GrowthLab.Tap("flow") && shop.Pipes[1].To == GrowthLab.Buffer(1), "A pipe from buffer 2 to the Flow grower runs from its tap to the buffer");
            // A pipe that cannot be: the Bloom tap is in use.
            s.StartPipe(GrowthLab.Buffer(2));
            yield return AimAt(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom", "Aimed at the Bloom grower again");
            Check(!s.PipeReady && s.PipeWhy == "The Bloom tap is in use" && s.FocusName(out status) != null && status.Contains("The Bloom tap is in use"), "Refused, and the prompt says why: the Bloom tap is in use");
            yield return Press(Key.Escape);
            Check(s.PipeFrom == null && s.Card == GrowthCard.None, "Esc stops pipe mode (and does not pause)");
            Ok(s.Act(() => shop.LayPipe(GrowthLab.Tap("ember"), GrowthLab.Buffer(2))), "The Ember line to buffer 3 (as pipe mode lays it)");
            LookAt(new Vector3(-1.0f, 0, 5.4f), new Vector3(-3.0f, 3.0f, 8.8f)); yield return Shot("102-pipes-overhead");

            // The hand pump: hold the mouse on its crank and the buffer fills in bulk.
            float before = shop.BufferAt(0).Units, grower = shop.GrowerUnits["bloom"];
            yield return HoldAt(GrowthWorld.GrowerFront("bloom") + new Vector3(0.25f, 0, -0.26f), PumpPoint("bloom"), GrowthTargetKind.Pump, "bloom", () => shop.BufferAt(0).Units >= before + 5, "the hand pump filling buffer 1", "The reticle focuses the Bloom grower's hand pump");
            Check(shop.BufferAt(0).Units >= before + 5 && shop.GrowerUnits["bloom"] < grower, "Cranking the hand pump moves doses from the grower into buffer 1");
            yield return Shot("103-hand-pump");
            shop.Held.Clear();
            float inBuffer = shop.BufferAt(0).Units;
            yield return HoldAt(BufferFront(0), BufferAimPoint(0), GrowthTargetKind.Buffer, "0", () => shop.Held.Count >= 2, "the siphon at buffer 1", "The reticle focuses buffer 1");
            Check(shop.Held.All(h => h == "bloom") && shop.BufferAt(0).Units <= inBuffer - 2, "The siphon collects from a buffer as from a grower");
            shop.Held.Clear();

            // Electric pumps on the hand crank: one unit, served in the board's order.
            Ok(s.Act(() => shop.Upgrade("pump")), "Electric pumps (400 credits), with the hand crank");
            s.Advance(0.5f);
            yield return null; yield return null;
            Check(shop.Lines.All(p => shop.LineStatus(p) == "Waiting for power"), "Without cranking, every electric pump waits for power and says so");
            float b1 = shop.BufferAt(0).Units, b2 = shop.BufferAt(1).Units;
            yield return HoldAt(GrowthWorld.GeneratorPosition + new Vector3(0, 0, -1.4f), crank, GrowthTargetKind.Generator, "generator", () => shop.BufferAt(0).Units >= b1 + 0.5f, "the crank powering the Bloom pump", "The reticle focuses the generator's crank");
            Check(shop.BufferAt(1).Units == b2, "One unit runs the first pump on the board; the Flow pump waits");
            yield return Shot("104-crank");
            Ok(s.Act(() => shop.MovePower(shop.Pipes[1].PowerId, -1)), "Move the Flow pump to the top of the board");
            yield return AimAt(new Vector3(2.6f, 0, 12.5f), GrowthWorld.PowerBoardPosition - new Vector3(0.05f, 0, 0), GrowthTargetKind.PowerBoard, "board", "The reticle focuses the power board again");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("1. Flow pump to buffer 2") && card.Contains("2. Bloom pump to buffer 1") && card.Contains("Waiting for power"), "The board lists its machines in order, each with what it is doing");
            s.SetCard(GrowthCard.None);

            // The bubble turbine: 3 units by itself, and powered pumps open era 4.
            Ok(s.Act(() => shop.Upgrade("generator")), "The bubble turbine (800 credits)");
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.PoweredPumps && s.Ui.Log.Any(l => l.StartsWith("Era 4")) && s.World.GeneratorLookLevel == 2, "Powered pumps running, 4 stars and 12 species: era 4 opens");
            float f1 = shop.BufferAt(1).Units;
            s.Advance(3f);
            Check(shop.BufferAt(1).Units >= f1 + 1.9f && shop.Lines.All(p => shop.LineStatus(p) == "Pumping" || shop.LineStatus(p).Contains("empty") || shop.LineStatus(p).Contains("full")), "The pumps run by themselves on the turbine's 3 units");
            // Fast pumps, 2 units each: three lines over budget.
            Ok(s.Act(() => shop.Upgrade("pump")), "Fast pumps (era 4): 2 units each");
            foreach (var c in new[] { "bloom", "flow", "ember" }) shop.GrowerUnits[c] = 10;
            foreach (var b in shop.Buffers) b.Units = Mathf.Min(b.Units, 4);
            s.Advance(0.3f);
            yield return AimAt(new Vector3(2.6f, 0, 12.5f), GrowthWorld.PowerBoardPosition - new Vector3(0.05f, 0, 0), GrowthTargetKind.PowerBoard, "board", "The reticle focuses the power board");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(shop.Draw == 6 && shop.Supply == 3 && card.Contains("Slowed: power for 1 of 2 units") && card.Contains("Waiting for power"), "Over budget: 6 units asked for, 3 supplied; the second is slowed and the third waits, each saying so");
            yield return Shot("105-power-board");
            s.SetCard(GrowthCard.None);

            // The auto-doser: the incubator's level 4 port, fed by buffers, dosing a known recipe.
            for (int lv = 2; lv <= 4; lv++) Ok(s.Act(() => shop.Upgrade("incubator")), "Incubator level " + lv);
            s.StartPipe(GrowthLab.Port);
            yield return AimAt(BufferFront(0), BufferAimPoint(0), GrowthTargetKind.Buffer, "0", "Aimed at buffer 1 from the incubator's port");
            Check(s.PipeReady, "A port pipe can run from buffer 1");
            yield return Press(Key.E);
            Ok(s.Act(() => shop.LayPipe(GrowthLab.Buffer(1), GrowthLab.Port)), "Buffer 2 to the port (as pipe mode lays it)");
            yield return null; yield return null;
            Check(shop.PortBuffers.Count() == 2 && s.World.BuiltPipes == 5, "Two buffers feed the port; five pipes in the lab");
            Ok(s.Act(() => shop.SetAutoRecipe("pondlet")), "The auto-doser's recipe: a Pondlet (Bloom and Flow, already made)");
            Check(shop.AutoRecipeCheck(GrowthContent.Species.First(x => x.Kind == SpeciesKind.Founder && !shop.Discovered.Contains(x.Id)).Id, out string never) == GrowthFailure.NotDiscovered && never.Contains("never tries something new"),
                "It refuses a recipe not made yet: it never tries something new");
            Ok(s.Act(() => shop.SwitchAutoDoser(true)), "Switch the auto-doser on");
            Ok(s.Act(() => shop.MovePower(GrowthShop.DoserPower, -3)), "Move the auto-doser to the top of the power board");
            foreach (var c in new[] { "bloom", "flow" }) shop.BufferAt(c == "bloom" ? 0 : 1).Units = Mathf.Max(shop.BufferAt(c == "bloom" ? 0 : 1).Units, 4);
            for (int i = 0; i < 20 && !shop.Incubating; i++) s.Advance(0.25f);
            Check(shop.Incubating && shop.AutoBatches == 1 && shop.Chamber.Count == 0 && s.Ui.Log.Any(l => l.StartsWith("The auto-doser started a Pondlet")), "It doses Bloom and Flow from the port's buffers and incubates them");
            yield return AimAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0), GrowthTargetKind.Incubator, "incubator", "The reticle focuses the incubator");
            yield return Press(Key.E);
            s.Ui.ScrollTo("Auto-doser");
            card = s.Ui.CardText();
            Check(card.Contains("Auto-doser · on") && card.Contains("Incubating") && card.Contains("Piped to the port: Bloom (buffer 1"), "The incubator's card: the auto-doser on, incubating, with the port's buffers");
            yield return Shot("106-auto-doser");
            s.SetCard(GrowthCard.None);
            s.Advance(shop.IncubationSeconds + 0.1f);
            Check(shop.Hatchlings.Count == 3 && shop.AutoStatus == "Waiting: take the clutch out of the incubator", "A clutch of three Pondlets; the auto-doser waits for the clutch to be taken (a full output waits)");
            // A pipe taken down at night: half its cost back; the auto-doser says what it needs.
            var flowPort = shop.OutletOf(1);
            credits = shop.Credits;
            Ok(s.Act(() => shop.RemovePipe(flowPort.Id)), "Take buffer 2's port pipe down");
            while (shop.Hatchlings.Count > 0) { shop.TakeHatchling(); shop.Carried.Clear(); }
            s.Advance(0.1f);
            Check(shop.Credits == credits + flowPort.Paid / 2 && shop.AutoStatus == "Pipe a Flow buffer to the port", "Half its cost back; the auto-doser says it needs a Flow buffer piped to the port");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(1.4f, 0, -0.6f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.7f, 0.6f)); yield return Shot("107-incubator-port");
            Ok(s.Act(() => shop.LayPipe(GrowthLab.Buffer(1), GrowthLab.Port)), "Lay it again");

            // Save and reload in the middle of a batch: the job resumes and no culture is doubled.
            for (int i = 0; i < 40 && !(shop.AutoBatch && shop.Chamber.Count == 1); i++) s.Advance(0.05f);
            Check(shop.AutoBatch && shop.Chamber.Count == 1, "Mid-batch: one dose in the chamber");
            float total = shop.Buffers.Sum(b => b.Units) + shop.Chamber.Count;
            Check(s.SaveNow(), "Save");
            string saved = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            shop = s.Shop;
            var file = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            Check(JsonUtility.ToJson(GrowthShop.Restore(file, out _).Snapshot()) == saved && file.version == GrowthSave.CurrentVersion && shop.AutoBatch && shop.Chamber.Count == 1 && shop.Pipes.Count == 5 &&
                shop.PowerOrder.First() == GrowthShop.DoserPower && Mathf.Abs(shop.Buffers.Sum(b => b.Units) + shop.Chamber.Count - total) < 0.001f,
                "The save (version " + GrowthSave.CurrentVersion + ") brings the lab back mid-batch: pipes, buffers, the board's order and the auto-doser's dose, no culture doubled");
            yield return null; yield return null;
            Check(s.World.BuiltPipes == 5 && s.World.BuiltBuffers == 3 && s.World.GeneratorLookLevel == 2, "The reloaded lab looks the same: five pipes, three buffers, the bubble turbine");
            foreach (var c in shop.Cultures) shop.GrowerUnits[c] = 0;
            for (int i = 0; i < 20 && !shop.Incubating; i++) s.Advance(0.1f);
            Check(shop.Incubating && shop.AutoBatches == 2, "The batch carries on and incubates");
            LookAt(new Vector3(0.6f, 0, 9.6f), new Vector3(-0.5f, 1.2f, 14.5f)); yield return Shot("108-annex");
            LookAt(new Vector3(1.15f, 0, 16.2f), new Vector3(-1.6f, 1.0f, 9.6f)); yield return Shot("109-buffers");
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
