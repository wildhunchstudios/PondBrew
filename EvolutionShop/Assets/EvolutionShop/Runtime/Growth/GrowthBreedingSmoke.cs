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
    /// GR-05's Windows route (-evolutionGrowthBreedingSmoke): the breeding tank's slot in the back room and buying it (era 2,
    /// credits and research points); a Bubblebelly and a Pondlet put in with E, a new Puddlepup in the nursery, taken with
    /// E, and the same pair breeding again; a Reedwhisker, then the Brookkin special pair's crown, the Brook Monarch; era 3
    /// opening with the third star after a day of visitors who never buy the breeding tank's creatures; level 2 with its
    /// tint port, a Bloom dose held from the grower tinting the water and a new Sunny Pondlet; the journal's family chart
    /// and pair log; save and reload.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string BreedingScenarioText = "GR-05 breeding: the breeding tank bought, a pair put in with E, hybrids and a crown in the nursery, the same pair again, research points, era 3, level 2 with a tinted Sunny offspring, the journal's family chart and pair log, save and reload";

        IEnumerator AtBreedingTank(string label)
        {
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0, 0, 1.5f), GrowthWorld.BreedingPosition + new Vector3(0, 0.55f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Breeding, "breeding", label);
        }

        IEnumerator BreedingScenario()
        {
            limitations.Add("GR-05 breeding route: fixtures are labelled: an era 2 shop with two stars (reputation 218), 700 credits, 15 research points, the three Brookkin founders known, a Starter tank on a1 holding two Pondlets, a Bubblebelly and a Ribbonfin, and a second Starter tank on a2. The first pair goes in and its offspring comes out with E pressed at the breeding tank; later moves between hands, tanks and the breeding tank, buying, upgrading and tinting call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked). Each breeding (60 s, then 40 s) and the day of visitors run on the shop's own clock (accelerated). The Bloom dose for the tint is collected by holding the route's mouse on the grower. Placeholder art.");
            yield return Opening();
            TakeInput();

            var fx = new GrowthShop { Era = 2, Reputation = GrowthContent.StarThresholds[2] - 2, Credits = 720, Rp = 15 };
            fx.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            var starter = fx.Containers[0];
            var pondletA = Stock(fx, starter, "pondlet"); var pondletB = Stock(fx, starter, "pondlet");
            var bubble = Stock(fx, starter, "bubblebelly"); var ribbon = Stock(fx, starter, "ribbonfin");
            Ok(fx.BuyContainer("starter", "a2"), "Fixture: a second Starter tank on a2");
            var second = fx.ContainerAt("a2");
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null;
            Check(shop.Era == 2 && shop.Stars == 2 && shop.Rp == 15 && shop.Credits == 700 && !shop.HasBreedingTank, "Fixture: an era 2, two-star shop with 700 credits, 15 research points and no breeding tank");

            // The breeding tank's slot in the back room, and its card.
            yield return AtBreedingTank("The reticle focuses the breeding tank's slot by the back room's south wall");
            Check(s.FocusName(out string status) == "Breeding tank (not bought)" && status.Contains("60 credits and 5 RP"), "The slot says what the breeding tank costs: " + status);
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Breeding && s.Ui.CardText().Contains("Buy the breeding tank for 60 cr and 5 RP"), "E opens its card: buy it for 60 credits and 5 research points");
            yield return Shot("80-breeding-slot");
            Ok(s.Act(() => shop.Upgrade("breeding")), "Buy the breeding tank");
            yield return null; yield return null;
            Check(shop.Level("breeding") == 1 && shop.Credits == 640 && shop.Rp == 10 && s.World.BreedingTankStanding, "The breeding tank stands in its place: 60 credits and 5 RP paid");
            s.SetCard(GrowthCard.None);
            LookAt(new Vector3(0.7f, 0, 6.4f), GrowthWorld.BreedingPosition + new Vector3(0, 0.8f, 0)); yield return Shot("81-breeding-tank");

            // A Bubblebelly and a Pondlet from the Starter tank, put in with E.
            Ok(s.Act(() => shop.TakeOut(starter.Id, bubble.Id)), "Take the Bubblebelly out of the Starter tank (as the tank card's button does)");
            Ok(s.Act(() => shop.TakeOut(starter.Id, pondletA.Id)), "Take a Pondlet out");
            yield return AtBreedingTank("The reticle focuses the breeding tank");
            yield return Press(Key.E);
            yield return Press(Key.E);
            Check(shop.Parents.Select(p => p.Species).OrderBy(x => x).SequenceEqual(new[] { "bubblebelly", "pondlet" }) && shop.Carried.Count == 0, "E twice puts the pair in");
            Check(shop.Breeding && shop.BreedingLeft > 55, "The pair breeds: 60 s to the offspring");
            Check(s.FocusName(out status) == "Breeding tank" && status.Contains("s to the offspring"), "The reticle's line counts down: " + status);
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Breeding && s.Ui.CardText().Contains("A pair you have not tried"), "With nothing to put in or take, E opens the card: a pair not tried yet");
            yield return Shot("82-pair-breeding");
            s.SetCard(GrowthCard.None);
            Check(s.World.BreedingViews.Count == 2, "The pair swims in the breeding tank");

            int rp = shop.Rp;
            s.Advance(shop.BreedingLeft + 0.05f);
            yield return null; yield return null;
            Check(shop.Offspring != null && shop.Offspring.Species == "puddlepup" && shop.Discovered.Contains("puddlepup") && shop.Rp == rp + GrowthContent.RpHybrid,
                "60 s later (accelerated) a new Puddlepup is in the nursery: a hybrid, 8 research points");
            Check(s.Ui.Log.Contains("New creature: Puddlepup!") && shop.Parents.Count == 2 && s.World.BreedingViews.Count == 3, "Its discovery is announced; the parents stay; three swim in the breeding tank");
            Check(shop.Era == 2, "Era 2 still: two stars and no Large tank yet");
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0.36f, 0, 0.95f), GrowthWorld.BreedingPosition + new Vector3(0.36f, GrowthWorld.BreedingWaterY, 0));
            yield return Shot("83-puddlepup");
            yield return AtBreedingTank("The reticle focuses the breeding tank");
            yield return Press(Key.E);
            Check(shop.Offspring == null && shop.Carried.Single().Species == "puddlepup" && shop.BreedingLeft > 55, "E takes the Puddlepup; the pair starts on the next one");
            var puddle1 = shop.Carried.Single();
            Ok(s.Act(() => shop.PutIn(starter.Id, puddle1.Id)), "Put the Puddlepup in the Starter tank");
            s.Advance(shop.BreedingLeft + 0.05f);
            Check(shop.Offspring?.Species == "puddlepup" && shop.Rp == rp + GrowthContent.RpHybrid && shop.PairEntry("pondlet", "bubblebelly").Count == 2 && s.Ui.Log.Contains("Puddlepup bred"),
                "The same pair breeds true to its result again: a second Puddlepup, no more research points (breeding as production)");

            // A Reedwhisker from a Pondlet and a Ribbonfin.
            Ok(s.Act(shop.TakeOffspring), "Take the second Puddlepup");
            var puddle2 = shop.Offspring == null ? shop.Carried.Last() : null;
            Ok(s.Act(() => shop.TakeParent(bubble.Id)), "Take the Bubblebelly out (the card's button)");
            Ok(s.Act(() => shop.PutIn(starter.Id, bubble.Id)), "Put it back in the Starter tank");
            Ok(s.Act(() => shop.TakeOut(starter.Id, ribbon.Id)), "Take the Ribbonfin out");
            Ok(s.Act(() => shop.PutParent(ribbon.Id)), "Put the Ribbonfin in with the Pondlet");
            rp = shop.Rp;
            s.Advance(shop.BreedingLeft + 0.05f);
            Check(shop.Offspring?.Species == "reedwhisker" && shop.Rp == rp + GrowthContent.RpHybrid, "Pondlet × Ribbonfin: a new Reedwhisker (accelerated)");

            // The Brookkin special pair: Puddlepup × Reedwhisker, the crown.
            Ok(s.Act(shop.TakeOffspring), "Take the Reedwhisker");
            var reed = shop.Carried.Last();
            Ok(s.Act(() => shop.TakeParent(pondletA.Id)), "Take the Pondlet out");
            Ok(s.Act(() => shop.PutIn(starter.Id, pondletA.Id)), "Put it back in the Starter tank");
            Ok(s.Act(() => shop.TakeParent(ribbon.Id)), "Take the Ribbonfin out");
            Ok(s.Act(() => shop.PutIn(second.Id, ribbon.Id)), "Put it in the second Starter tank");
            Ok(s.Act(() => shop.PutParent(puddle2.Id)), "Put the second Puddlepup in");
            Ok(s.Act(() => shop.PutParent(reed.Id)), "Put the Reedwhisker in");
            rp = shop.Rp;
            s.Advance(shop.BreedingLeft + 0.05f);
            yield return null; yield return null;
            Check(shop.Offspring?.Species == "brookmonarch" && shop.Offspring.Def.Kind == SpeciesKind.Crown && shop.Rp == rp + GrowthContent.RpCrown,
                "Puddlepup × Reedwhisker, the Brookkin special pair: the crown, a Brook Monarch, 20 research points");
            s.SetCard(GrowthCard.Breeding); yield return new WaitForSeconds(0.2f);
            string card = s.Ui.CardText();
            Check(card.Contains("In the nursery: a Brook Monarch — new to your journal!") && card.Contains("Puddlepup × Reedwhisker → Brook Monarch (a crown!)") && card.Contains("Pair log · 3 pairs tried"),
                "The card shows the crown in the nursery and the pair log (3 pairs tried)");
            yield return Shot("84-crown");
            s.SetCard(GrowthCard.None);

            // Era 3: a Large tank at night, then the third star in a day of visitors, who never buy the breeding tank's creatures.
            Ok(s.Act(() => shop.BuyContainer("large", "a3")), "Buy a Large tank for the long place a3 (200 credits, at night)");
            Check(shop.Era == 2 && shop.Objectives().Single(o => o.text.StartsWith("Reach 3 stars")).done == false, "Era 2 still: the hybrid and the Large tank are there, the third star is not");
            // The two Pondlets for later stay in hand through the day, so no visitor buys them.
            Ok(s.Act(() => shop.TakeOut(starter.Id, pondletA.Id)), "Take a Pondlet out of the Starter tank and keep it in hand");
            Ok(s.Act(() => shop.TakeOut(starter.Id, pondletB.Id)), "Take the other Pondlet too");
            LookAt(new Vector3(1.6f, 0, 0.6f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0));
            Ok(s.Act(shop.OpenShop), "Open the shop");
            bool eraShot = false;
            for (int i = 0; i < 3000 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(0.5f);
                if (shop.Era == 3 && !eraShot)
                {
                    eraShot = true;
                    s.Advance(0.01f); // the sale's events reach the HUD with the next step
                    Check(s.Ui.Log.Contains("Era 3: Plumbing. Water kits and catalog page 3 open."), "The third star opens era 3: 3 stars, a hybrid bred and a Large tank");
                    yield return Shot("85-era-three");
                }
                if (i % 40 == 0) yield return null;
            }
            yield return null;
            s.SetCard(GrowthCard.None);
            Check(eraShot && shop.Era == 3 && shop.Phase == GrowthPhase.Night, "The day ends in era 3 (accelerated)");
            Check(shop.Parents.Count == 2 && shop.Offspring?.Species == "brookmonarch", "The breeding tank's pair and the crown are still there: they are not for sale");
            Check(shop.Objectives().Any(o => o.text.StartsWith("Fill a Large tank")) && shop.EraGoal == GrowthContent.Eras[2].Goal, "The goal is era 3's");

            // Level 2: the tint port. The crown goes to the Large tank, a Bloom dose tints the water, and two Pondlets breed Sunny.
            Ok(s.Act(shop.TakeOffspring), "Take the Brook Monarch");
            var crown = shop.Carried.Last();
            var large = shop.ContainerAt("a3");
            Ok(s.Act(() => shop.PutIn(large.Id, crown.Id)), "Put the Brook Monarch in the Large tank");
            Ok(s.Act(() => shop.TakeParent(reed.Id)), "Take the Reedwhisker out: the tank is idle");
            Ok(s.Act(() => shop.Upgrade("breeding")), "Breeding tank level 2 (200 credits)");
            yield return null; yield return null;
            Check(shop.Level("breeding") == 2 && shop.BreedSeconds == 40 && shop.TintPort && shop.LargeParents && shop.Parents.Single().Id == puddle2.Id, "Level 2: 40 s, a tint port and Large parents; the Puddlepup stayed in");
            Ok(s.Act(() => shop.PutIn(large.Id, reed.Id)), "Put the Reedwhisker in the Large tank");
            Ok(s.Act(() => shop.TakeParent(puddle2.Id)), "Take the Puddlepup out");
            Ok(s.Act(() => shop.PutIn(large.Id, puddle2.Id)), "Put the Puddlepup in the Large tank");
            Ok(s.Act(() => shop.PutParent(pondletA.Id)), "Put a Pondlet in");
            Ok(s.Act(() => shop.PutParent(pondletB.Id)), "Put the other in");
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom",
                () => shop.Held.Contains("bloom"), "a Bloom dose");
            Ok(s.Act(() => shop.TintWater("bloom")), "Tint the breeding water with the Bloom dose (the card's button)");
            yield return AtBreedingTank("The reticle focuses the breeding tank");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Breeding && card.Contains("The water is tinted with Bloom: the next offspring is Sunny") && card.Contains("Its colour: Sunny (the Bloom tint)"),
                "The card says the water is tinted and the next Pondlet will be Sunny");
            yield return Shot("86-tinted");
            s.SetCard(GrowthCard.None);
            rp = shop.Rp;
            s.Advance(shop.BreedingLeft + 0.05f);
            yield return null; yield return null;
            Check(shop.Offspring != null && shop.Offspring.Species == "pondlet" && shop.Offspring.Colour == Colour.Sunny && shop.Tint == null && shop.Rp == rp + GrowthContent.RpColour,
                "Two Pondlets in Bloom-tinted water: a Sunny Pondlet, a new colour (2 research points); the tint is used up");
            Check(s.Ui.Log.Contains("New colour: Sunny Pondlet!") && shop.Journal.Contains("pondlet/Sunny"), "The new colour is announced and in the journal");
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0.36f, 0, 0.95f), GrowthWorld.BreedingPosition + new Vector3(0.36f, GrowthWorld.BreedingWaterY, 0));
            yield return Shot("87-sunny-pondlet");

            // The journal: each family's chart of eight with its colours, and the pair log.
            s.SetCard(GrowthCard.Journal); yield return new WaitForSeconds(0.25f);
            string journal = s.Ui.CardText();
            Check(journal.Contains("Discovered 6 of 72 species · journal 7/288") && journal.Contains("Brookkin · small · 6/8 · colours 7/32"), "The journal counts species and colours: 6 of 72, 7 of 288 entries");
            Check(journal.Contains("Puddlepup · hybrid · bred from Bubblebelly × Pondlet") && journal.Contains("Brook Monarch · crown · bred from Puddlepup × Reedwhisker"), "Known hybrids and the crown say which pair made them");
            Check(journal.Contains("Pair log · 4 pairs tried") && journal.Contains("Pondlet × Pondlet → Pondlet") && journal.Contains("Bubblebelly × Pondlet → Puddlepup ×2"), "The pair log lists every pair tried");
            Check(s.Ui.ScrollTo("Creatures"), "Scroll to the family chart");
            yield return Shot("88-family-chart");
            Check(s.Ui.ScrollTo("Pair log"), "Scroll to the pair log");
            yield return Shot("89-pair-log");
            s.SetCard(GrowthCard.None);

            Check(s.SaveNow(), "Save");
            string before = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            yield return null; yield return null;
            var reloaded = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            Check(JsonUtility.ToJson(s.Shop.Snapshot()) == before && reloaded.version >= 4 && s.Shop.Offspring?.Colour == Colour.Sunny && s.Shop.PairLog.Count == 4 && s.World.BreedingTankStanding,
                "The version 4 save brings the same shop back: the level 2 breeding tank, its pair and Sunny offspring, the pair log and the journal's colours");
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
