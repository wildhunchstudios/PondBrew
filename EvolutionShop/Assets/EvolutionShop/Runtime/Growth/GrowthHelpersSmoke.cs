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
    /// GR-09's Windows route (-evolutionGrowthHelpersSmoke): the staff board and its card, a helper's achievement met (a Large
    /// tank for Nori), Pip, Nori and Moss hired at night and standing at work; Nori's stock targets on Nori's card and Nori
    /// carrying a new clutch from the incubator's tray to the shop's tank; Moss running a known recipe by hand, a dose in
    /// hand; both saying why they wait; Pip at the till through a day, the counter refusing the player while Pip works; the
    /// night's wages; the counter's second till and the player serving beside Pip; Moss trained and keeping a known pair
    /// breeding, saved and reloaded with a parent in hand; a night the shop cannot pay everyone, and who stays home.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string HelpersScenarioText = "GR-09 helpers: the staff board, an achievement met, Pip, Nori and Moss hired and at work, Nori's stock targets and Nori carrying a clutch to the shop's tank, Moss running a known recipe by hand, the helpers saying why they wait, Pip at the till through a day, the night's wages, the counter's second till with the player serving beside Pip, Moss trained and keeping a known pair breeding, saved and reloaded mid-step, and a night the shop cannot pay everyone";

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
        static Vector2 Flat((float x, float z) p) => new Vector2(p.x, p.z);

        /// <summary>Where a helper's figure should stand now (the rules' step, as the world draws it).</summary>
        static Vector2 Expected(GrowthHelper h)
        {
            if (h.Step == null || h.StepSeconds <= 0) return Flat(h.Spot);
            return Flat(GrowthWalk.Along(h.Spot, h.Target, h.Progress / Mathf.Max(0.01f, h.StepSeconds - GrowthWalk.Handle)));
        }

        /// <summary>The player's own clutch of two Pondlets (the incubator's card and a held mouse on the growers).</summary>
        void PlayerClutch()
        {
            var shop = s.Shop;
            Ok(shop.Collect("bloom", shop.HoldSeconds), "Collect a Bloom dose (as holding the mouse on its grower does)");
            Ok(shop.Collect("flow", shop.HoldSeconds), "Collect a Flow dose");
            Ok(shop.AddDose("bloom"), "Dose the Bloom"); Ok(shop.AddDose("flow"), "Dose the Flow");
            Ok(shop.StartIncubation(), "Start a Pondlet mix (the incubator card's button)");
        }

        IEnumerator HelpersScenario()
        {
            limitations.Add("GR-09 helpers route: the fixture is labelled: an era 4, four-star shop (reputation 860) with 8,000 credits, 300 research points and 160 sales so far, unit B open, 12 founders known, a Pondlet and Ribbonfin pair in the pair log, growers at level 2, the lab annex with the bubble turbine and electric pumps, the incubator at level 3, the breeding tank, and the counter at level 3. The Large tank is bought with the shop's action, then stocked with six Ribbonfins (development stock); on the second night it is filled again and a Starter tank bought on a2 is stocked with Bubblebellies (development stock), and Moss goes back to his recipe once his pair is in, so the second day has stock for two visitors to wait at once. Hiring, training, duty, stock targets, Moss's settings and upgrades call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked); the days run on the shop's own clock (accelerated); before the second day closes, the shop's credits are set to 20 (labelled) to show helpers staying home. Placeholder figures: plain shapes for the helpers and the staff board.");
            yield return Opening();
            TakeInput();

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 8000, Rp = 300, UnitB = true, SalesTotal = 160 };
            fx.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).Select(x => x.Id));
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Levels["grower"] = 2; fx.Levels["generator"] = 2; fx.Levels["pump"] = 2; fx.Levels["incubator"] = 3; fx.Levels["breeding"] = 1; fx.Levels["counter"] = 3;
            fx.Annex = true;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            fx.PairLog.Add(new GrowthPairEntry { A = "pondlet", B = "ribbonfin", Species = GrowthRules.Breed("pondlet", "ribbonfin").Id });
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.Stars == 4 && shop.Staff.Count == 0 && shop.Discovered.Contains("ribbonfin") && shop.HasBreedingTank && s.World.StaffBoardStanding,
                "Fixture: an era 4, four-star shop with no helpers yet, and the staff board on the wall");

            // The staff board on unit A's north wall, west of the back room's door.
            yield return AimAt(new Vector3(-0.45f, 0, 2.0f), GrowthWorld.StaffBoardPosition, GrowthTargetKind.StaffBoard, "staff", "The reticle focuses the staff board");
            Check(s.FocusName(out string status) == "Staff board" && status == "E see who could work here", "It is the staff board, with no one hired");
            yield return Shot("120-staff-board");
            yield return Press(Key.E);
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Staff && card.Contains("Hire Pip for 108 cr (the fee and tonight's wage)") && card.Contains("Needs a Large tank in the shop") && card.Contains("Hire Moss for 860 cr"),
                "E opens its card: Pip and Moss can be hired (the fee and tonight's wage); Nori needs a Large tank in the shop");
            yield return Shot("121-staff-card");
            Ok(s.Act(() => shop.BuyContainer("large", "a3")), "A Large tank on a3 (the place card's button)");
            card = s.Ui.CardText();
            Check(card.Contains("Hire Nori for 425 cr (the fee and tonight's wage)"), "With a Large tank in the shop, Nori can be hired");
            var large = shop.ContainerAt("a3");
            for (int i = 0; i < 6; i++) Ok(shop.DevelopmentStock(large.Id, "ribbonfin"), "Fixture Ribbonfin " + (i + 1));
            int credits = shop.Credits;
            foreach (var id in new[] { "pip", "nori", "moss" }) Ok(s.Act(() => shop.Hire(id)), "Hire " + GrowthContent.Helper(id).Name + " (the staff card's button)");
            yield return null; yield return null; // a card's events reach the notes on the next frame
            Check(shop.Staff.Count == 3 && shop.Credits == credits - 108 - 425 - 860 && s.Ui.Log.Any(l => l == "Pip joins the shop as its cashier (100 credits and 8 for tonight and tomorrow)") &&
                s.Ui.Log.Any(l => l.StartsWith("Moss joins the shop as its technician")), "All three hired: each fee and tonight's wage paid, and each says so");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(s.World.FigureShowing("pip") && s.World.FigureShowing("nori") && s.World.FigureShowing("moss") &&
                (Flat(s.World.FigurePosition("pip")) - Flat(GrowthWalk.Counter)).magnitude < 0.05f && (Flat(s.World.FigurePosition("nori")) - Flat(GrowthWalk.NoriHome)).magnitude < 0.05f &&
                (Flat(s.World.FigurePosition("moss")) - Flat(GrowthWalk.MossHome)).magnitude < 0.05f, "Their figures stand at work: Pip behind the counter, Nori and Moss in the back room");

            // Nori's card: the stock targets (Nori's from era 3; the sorter shares them).
            yield return AimAt(new Vector3(0.8f, 0, 2.2f), new Vector3(0.6f, 1.1f, 5.4f), GrowthTargetKind.Helper, "nori", "The reticle focuses Nori through the back room's door");
            Check(s.FocusName(out status) == "Nori, stocker (level 1)" && status == "Nothing to move: set stock targets on my card · E card", "It is Nori, saying there is nothing to move without targets");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Helper && s.CardTarget == "nori" && card.Contains("Carries one creature at a time.") && card.Contains("Stock targets") &&
                card.Contains("No targets yet: Nori moves nothing to the shop's tanks until you set some.") && card.Contains("Train to level 2 for 1000 cr") && card.Contains("Send home"),
                "E opens Nori's card: what Nori does at this level, the stock targets, duty and training");
            Ok(s.Act(() => shop.SetTarget("pondlet", 2)), "Target 2 Pondlets (the card's + button)");
            yield return null;
            Check(s.Ui.CardText().Contains("Targets: Pondlet 0/2"), "Nori's card shows the target");
            yield return Shot("122-nori-card");
            s.SetCard(GrowthCard.None);

            // The player's clutch of two: Nori carries each from the tray to the shop's tank below its target.
            PlayerClutch();
            var nori = shop.Helper("nori");
            bool carried = false, followed = true;
            for (int i = 0; i < 2000 && shop.Shown("pondlet") < 2; i++)
            {
                s.Advance(0.05f);
                if (i % 4 == 0)
                {
                    yield return null;
                    if (nori.Step != null) followed &= (Flat(s.World.FigurePosition("nori")) - Expected(nori)).magnitude < 0.05f;
                }
                if (!carried && nori.Step == "deliver" && nori.Progress >= 0.7f * (nori.StepSeconds - GrowthWalk.Handle) && nori.Progress < nori.StepSeconds - GrowthWalk.Handle)
                {
                    yield return null;
                    var at = s.World.FigurePosition("nori");
                    LookAt(new Vector3(-2.0f, 0, 0.9f), at + new Vector3(0, 1.0f, 0));
                    yield return null;
                    Check(nori.Hands.Count == 1 && s.World.FigureCarrying("nori") == nori.Hands[0].Id && nori.Status == "Carrying a Pondlet to the starter tank at a1",
                        "Nori carries a Pondlet from the tray to the starter tank at a1, the creature in hand");
                    yield return Shot("123-nori-carrying");
                    carried = true;
                }
            }
            Check(carried && followed, "Nori's figure walked the step's path through the back room's door, as the rules timed it");
            Check(shop.Shown("pondlet") == 2 && shop.Hatchlings.Count == 0 && shop.ContainerAt("a1").Occupants.Count(o => o.Species == "pondlet") == 2,
                "The clutch is in the shop's tank: two Pondlets, the target met");

            // Moss's card: a known recipe, run by hand from the growers.
            s.SetCard(GrowthCard.Helper, "moss");
            card = s.Ui.CardText();
            Check(card.Contains("Runs one known recipe.") && card.Contains("No recipe chosen yet.") && card.Contains("Pondlet · 1 Bloom, 1 Flow") && card.Contains("Run it") &&
                card.Contains("At level 2, Moss keeps a pair you have bred breeding."), "Moss's card (the staff card's Open button): choose a known recipe; the pair comes with level 2");
            Ok(s.Act(() => shop.SetMossRecipe("pondlet")), "Moss runs the Pondlet (the card's Run it button)");
            s.SetCard(GrowthCard.None);
            var moss = shop.Helper("moss");
            bool dose = false;
            for (int i = 0; i < 2000 && shop.MossBatches < 1; i++)
            {
                s.Advance(0.05f);
                if (i % 4 == 0) yield return null;
                if (!dose && moss.Step == "dose-back" && moss.Progress >= 0.4f * (moss.StepSeconds - GrowthWalk.Handle))
                {
                    yield return null;
                    LookAt(new Vector3(1.3f, 0, 5.8f), s.World.FigurePosition("moss") + new Vector3(0, 1.0f, 0));
                    yield return null;
                    Check(moss.Dose != null && s.World.FigureHoldsDose("moss") && moss.Status.StartsWith("Bringing 1 ") && shop.MossBatch, "Moss brings a dose from its grower to the incubator, the dose in hand");
                    yield return Shot("124-moss-dose");
                    dose = true;
                }
            }
            Check(dose && shop.MossBatches == 1 && shop.Incubating && shop.Pending == "pondlet" && !shop.MossBatch && s.Ui.Log.Any(l => l == "Moss started a Pondlet"),
                "Moss fetched the recipe's two doses one by one and started a Pondlet");
            for (int i = 0; i < 400 && shop.Hatchlings.Count == 0; i++) { s.Advance(0.1f); if (i % 10 == 0) yield return null; }
            for (int i = 0; i < 40; i++) { s.Advance(0.1f); if (i % 10 == 0) yield return null; }
            Check(shop.Hatchlings.Count == 2 && nori.Status == "No room for what is waiting: the tanks are at their targets or full, and no rack has room" && moss.Status == "Waiting for the clutch to be taken off the tray",
                "With the target met the clutch waits on the tray, and both say why: Nori has no room for it, Moss waits for the tray");
            yield return AimAt(new Vector3(-0.45f, 0, 2.0f), GrowthWorld.StaffBoardPosition, GrowthTargetKind.StaffBoard, "staff", "The reticle focuses the staff board again");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("No room for what is waiting") && card.Contains("Waiting for the clutch to be taken off the tray") && card.Contains("Wages: 93 credits a night and day."),
                "The staff card says what each helper is doing, and the wage bill");
            yield return Shot("125-helpers-waiting");
            s.SetCard(GrowthCard.None);
            Ok(s.Act(() => shop.SetTarget("pondlet", 4)), "Target 4 Pondlets");
            for (int i = 0; i < 2000 && shop.Shown("pondlet") < 4; i++) { s.Advance(0.1f); if (i % 10 == 0) yield return null; }
            Check(shop.Shown("pondlet") == 4, "Nori stocks the tank to its new target of four");

            // A day: Pip at the till; the counter is Pip's until its second till.
            Ok(s.Act(shop.OpenShop), "Open the shop (as E at the counter does)");
            for (int i = 0; i < 400 && shop.Queue.Count() < 2; i++) { s.Advance(0.25f); if (i % 10 == 0) yield return null; }
            yield return AimAt(GrowthWorld.CounterPosition + new Vector3(0, 0, 1.4f), GrowthWorld.CounterPosition + new Vector3(0, 1.05f, 0), GrowthTargetKind.Counter, "counter", "The reticle focuses the counter");
            Check(s.FocusName(out status) == "Counter" && status.StartsWith("Pip is at the till ("), "The counter says Pip is at the till");
            var refused = shop.Serve(0.1f);
            Hold(true);
            yield return null; yield return null; yield return null;
            Check(!s.ServingNow && refused.Failure == GrowthFailure.Busy && refused.Detail == "Pip is at the till: the counter's second till (level 4) lets you serve beside Pip",
                "Holding the mouse on the counter bags nothing: Pip is at the till, and the second till lets the player serve beside Pip");
            Hold(false); yield return null;
            bool tillShot = false;
            for (int i = 0; i < 900 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (i % 10 == 0) yield return null;
                if (!tillShot && shop.Phase == GrowthPhase.Day && shop.Helper("pip").Step == "serve" && shop.Queue.Any())
                {
                    yield return null;
                    LookAt(new Vector3(0.6f, 0, -1.0f), new Vector3(3.0f, 1.1f, -2.8f));
                    yield return null;
                    Check(s.World.FigureShowing("pip") && shop.Helper("pip").Status.StartsWith("Bagging an order"), "Pip bags an order behind the counter, a visitor waiting");
                    yield return Shot("126-pip-at-till");
                    tillShot = true;
                }
            }
            yield return null; yield return null;
            int pipSales = s.Ui.Log.Count(l => l.StartsWith("Pip sold "));
            Check(tillShot && shop.Phase == GrowthPhase.Night && pipSales >= 3 && !s.Ui.Log.Any(l => l.StartsWith("Sold ")),
                "Through the day (accelerated) Pip served every sale at the till (" + pipSales + "), the player none");
            Check(s.Ui.Log.Any(l => l == "Wages for tonight and tomorrow: 93 credits (Pip 8, Nori 25, Moss 60)") && shop.Staff.Values.All(h => h.PaidPeriod == shop.Period && h.OnDuty),
                "The night starts a duty period: each helper's wage paid once (Pip 8, Nori 25, Moss 60)");
            s.SetCard(GrowthCard.None);

            // The second night: the counter's second till, Moss trained, and a known pair kept breeding.
            Ok(s.Act(() => shop.Upgrade("counter")), "The counter's second till (level 4, 1,500 credits): its card's button");
            Ok(s.Act(() => shop.Train("moss")), "Train Moss to level 2 (2,000 credits): his card's button");
            yield return null; yield return null;
            Check(shop.SecondTill && moss.Level == 2 && s.Ui.Log.Any(l => l == "Moss trained to level 2: Runs a known recipe or keeps a known pair breeding"), "The second till is in, and Moss is at level 2");
            // The tanks are topped up for the second day (development stock, labelled).
            while (large.Free > 0) Ok(shop.DevelopmentStock(large.Id, "ribbonfin"), "Fixture Ribbonfin top-up");
            Ok(s.Act(() => shop.BuyContainer("starter", "a2")), "A Starter tank on a2 (the place card's button)");
            var starter = shop.ContainerAt("a2");
            while (starter.Free > 0) Ok(shop.DevelopmentStock(starter.Id, "bubblebelly"), "Fixture Bubblebelly");
            Ok(s.Act(() => shop.SetMossPair("pondlet", "ribbonfin")), "Moss keeps the Pondlet and Ribbonfin breeding (his card's button)");
            Ok(s.Act(() => shop.SetMossTask(GrowthShop.MossPairJob)), "Moss's job: the pair (his card's button)");
            s.SetCard(GrowthCard.Helper, "moss");
            card = s.Ui.CardText();
            Check(card.Contains("Keeping a Pondlet and a Ribbonfin breeding.") && card.Contains("Pondlet and Ribbonfin make a ") && card.Contains("Breeding") && card.Contains("Keep the pair breeding"),
                "Moss's card: the known pair and his job");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(s.World.LookLevel("counter") == 4, "The counter shows its second till");
            // Save and reload with a parent in Moss's hands, halfway to the breeding tank.
            bool saved = false;
            for (int i = 0; i < 2000 && !saved; i++)
            {
                s.Advance(0.05f);
                if (i % 4 == 0) yield return null;
                if (moss.Step == "parent-deliver" && moss.Progress >= 0.5f * (moss.StepSeconds - GrowthWalk.Handle))
                {
                    int hand = moss.Hands[0].Id; float progress = moss.Progress; string place = moss.Place;
                    Check(s.SaveNow(), "Save with Moss carrying a " + moss.Hands[0].Def.Name);
                    string before = JsonUtility.ToJson(s.Shop.Snapshot());
                    Check(s.ReloadSaved(), "Reload the saved slot");
                    shop = s.Shop; moss = shop.Helper("moss"); nori = shop.Helper("nori"); large = shop.ContainerAt("a3");
                    var file = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
                    Check(file.version >= 8 && JsonUtility.ToJson(GrowthShop.Restore(file, out _).Snapshot()) == before && moss.Step == "parent-deliver" && moss.Hands.Count == 1 && moss.Hands[0].Id == hand &&
                        Mathf.Abs(moss.Progress - progress) < 1e-4f && moss.Place == place && shop.MossPairA == "pondlet" && moss.Level == 2 && shop.SecondTill && shop.Staff.Count == 3,
                        "The version 8 (and later) save brings the helpers back mid-step: Moss's walk, the creature in his hands, his level and pair, the second till");
                    yield return null; yield return null;
                    Check(s.World.FigureCarrying("moss") == hand && (Flat(s.World.FigurePosition("moss")) - Expected(moss)).magnitude < 0.05f && moss.Status.StartsWith("Carrying a "),
                        "Moss's figure is back where he was, the same creature in hand, saying what he is doing");
                    LookAt(new Vector3(1.2f, 0, 6.2f), s.World.FigurePosition("moss") + new Vector3(0, 1.0f, 0));
                    yield return Shot("127-moss-pair");
                    saved = true;
                }
            }
            Check(saved, "Moss fetched a parent for the breeding tank");
            for (int i = 0; i < 2000 && shop.Pairs[0].Parents.Count < 2; i++) { s.Advance(0.1f); if (i % 10 == 0) yield return null; }
            Check(shop.Pairs[0].Parents.Select(p => p.Species).OrderBy(x => x).SequenceEqual(new[] { "pondlet", "ribbonfin" }) && moss.Hands.Count == 0,
                "Moss put both parents in the breeding tank: the known pair breeds");
            Ok(s.Act(() => shop.SetMossTask(GrowthShop.MossRecipeJob)), "Moss back on the recipe for the day: the pair breeds on (his card's button)");

            // The second day: the player serves beside Pip at the second till.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            for (int i = 0; i < 1000 && !shop.Queue.Any(); i++) { s.Advance(0.1f); if (i % 5 == 0) yield return null; }
            Check(shop.Queue.Any(), "A visitor waits at the counter");
            yield return AimAt(GrowthWorld.CounterPosition + new Vector3(0, 0, 1.4f), GrowthWorld.CounterPosition + new Vector3(0, 1.05f, 0), GrowthTargetKind.Counter, "counter", "The reticle focuses the counter");
            Check(s.FocusName(out status) == "Counter" && status.StartsWith("Hold mouse to bag a sale beside Pip ("), "The counter offers the second till beside Pip");
            // Holding the counter at the second till, the player serves the front of the queue and Pip the visitor behind.
            int mine = s.Ui.Log.Count(l => l.StartsWith("Sold ")), pips = s.Ui.Log.Count(l => l.StartsWith("Pip sold "));
            Hold(true);
            yield return Wait(() => s.ServingNow, 30, "the player bagging an order at the second till");
            Check(shop.PlayerAtTill && shop.Helper("pip").Status != "Home today", "Holding the counter, the player bags an order at the second till: the front of the queue is theirs");
            yield return Shot("128-second-till");
            yield return Wait(() => s.Ui.Log.Count(l => l.StartsWith("Sold ")) > mine, 30, "the player's sale at the second till");
            Hold(false); yield return null;
            Check(!shop.PlayerAtTill, "Letting go of the counter leaves the till to Pip");
            // The day runs to its last moments; then the shop has only 20 credits (labelled) as the night's wages fall due.
            for (int i = 0; i < 900 && shop.Phase == GrowthPhase.Day && shop.DaySecondsLeft > 1; i++) { s.Advance(0.5f); if (i % 10 == 0) yield return null; }
            Check(s.Ui.Log.Count(l => l.StartsWith("Pip sold ")) > pips, "Pip sold beside the player and on through the day");
            shop.Credits = 20;
            s.Advance(shop.DaySecondsLeft + 0.05f);
            yield return null; yield return null;
            Check(shop.Phase == GrowthPhase.Night && s.Ui.Log.Any(l => l == "Wages for tonight and tomorrow: 8 credits (Pip 8)") &&
                s.Ui.Log.Any(l => l == "Nori stays home: the wage is 25 credits and the shop has 12") && s.Ui.Log.Any(l => l == "Moss stays home: the wage is 80 credits and the shop has 12"),
                "Short of credits, Pip is paid and Nori and Moss stay home, each saying why: never a debt");
            // The receipt pauses the shop's own clock: the night is run on (accelerated) until both have finished what was in hand.
            for (int i = 0; i < 400 && (nori.Status != "Home today" || moss.Status != "Home today"); i++) { s.Advance(0.1f); if (i % 10 == 0) yield return null; }
            yield return null; yield return null;
            var back = shop.SetDuty("nori", true);
            Check(!s.World.FigureShowing("nori") && !s.World.FigureShowing("moss") && s.World.FigureShowing("pip") && nori.Status == "Home today" && !back.Success && back.Detail == "Needs 25 credits for the wage",
                "Home, Nori and Moss are not at work; calling Nori back needs tonight's 25 credits (Nori: " + nori.Status + ", " + nori.Hands.Count + " in hand; Moss: " + moss.Status + ", " + moss.Hands.Count + " in hand, dose " + moss.Dose + "; shown " + s.World.FigureShowing("nori") + "/" + s.World.FigureShowing("moss") + "/" + s.World.FigureShowing("pip") + "; " + back.Detail + ")");
            s.SetCard(GrowthCard.Staff);
            card = s.Ui.CardText();
            Check(card.Contains("Level 1 · home · 25 credits a night and day. Home today.") && card.Contains("Level 2 · home · 80 credits a night and day.") && card.Contains("Wages: 8 credits a night and day."),
                "The staff card shows who is home");
            LookAt(new Vector3(-0.45f, 0, 1.6f), GrowthWorld.StaffBoardPosition);
            yield return Shot("129-staying-home");
            s.SetCard(GrowthCard.None);
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
