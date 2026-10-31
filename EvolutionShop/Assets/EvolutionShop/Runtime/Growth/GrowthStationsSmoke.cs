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
    /// GR-06's Windows route (-evolutionGrowthStationsSmoke): the research bench's card with the siphon's and the bench's
    /// level ladders; the siphon's twin chamber and vacuum collecting with the held mouse; the microscope on the incubator
    /// card; growers and the vat to their top levels; the incubator's tint port and clutches of two and three; the breeding
    /// tank's two pairs; the counter's card reader and queue of 3 through a day; the levels that wait for later tasks; each
    /// station's look at its level; save and reload.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string StationsScenarioText = "GR-06 stations: the upgrade ladder on every station card, the siphon's twin chamber and vacuum, the research bench's microscope, growers and the vat, the incubator's tint port and clutches, the breeding tank's two pairs, the counter's queue, levels that wait for later tasks, each level's look, save and reload";

        IEnumerator StationsScenario()
        {
            limitations.Add("GR-06 stations route: the fixture is labelled: an era 4, four-star shop (reputation 900) with 20,000 credits and 300 research points, unit B open, the three Brookkin founders known, Ember and Mist owned with the vat at level 1, the breeding tank at level 2, a Starter tank on a1 with two Pondlets, a Bubblebelly and a Ribbonfin. Upgrades, tinting, taking hatchlings and offspring call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked); doses are collected by holding the route's mouse on the growers, and E is pressed at the research bench, the incubator, the breeding tank and the counter; incubations, breeding and the day run on the shop's own clock (accelerated). Placeholder art and placeholder level parts.");
            yield return Opening();
            TakeInput();

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 50, Credits = 20000, Rp = 300, UnitB = true };
            fx.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Cultures.AddRange(new[] { "ember", "mist" });
            fx.GrowerUnits["ember"] = 4; fx.GrowerUnits["mist"] = 4;
            fx.Levels["vat"] = 1; fx.Levels["breeding"] = 2;
            var starter = fx.Containers[0];
            Stock(fx, starter, "pondlet"); Stock(fx, starter, "pondlet"); Stock(fx, starter, "bubblebelly"); Stock(fx, starter, "ribbonfin");
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Era == 4 && shop.Stars == 4 && GrowthShop.UpgradeableStations.Where(st => st != "generator" && st != "pump" && st != "sorter").All(st => shop.Level(st) == (st == "breeding" ? 2 : 1)),
                "Fixture: an era 4, four-star shop with every station at level 1 (the breeding tank at 2)");

            // The research bench: the siphon's and the bench's ladders.
            LookAt(GrowthWorld.WorkbenchPosition + new Vector3(-1.25f, 0, 0), GrowthWorld.WorkbenchPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Workbench, null, "The reticle focuses the research bench");
            Check(s.FocusName(out string status) == "Research bench" && status.StartsWith("level 1"), "It is the research bench, level 1");
            yield return Press(Key.E);
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Workbench && card.Contains("The siphon · level 1 of 4") && card.Contains("The research bench · level 1 of 4") && card.Contains("Upgrade · 15 cr"),
                "E opens its card: the siphon's ladder (level 1 of 4) and the bench's");
            Check(card.Contains("3. Analyzer: one undiscovered reachable mix or pair a day · 1500 cr and 120 RP, era 4") && card.Contains("4. Planner: predicts the offspring of any two known species · 4000 cr and 200 RP, era 5"),
                "The bench's levels 3 and 4: the analyzer and the planner (GR-08), with their costs and gates");
            yield return Shot("90-research-bench");
            for (int lv = 2; lv <= 3; lv++) Ok(s.Act(() => shop.Upgrade("siphon")), "Siphon level " + lv);
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.PerHold == 2 && shop.SiphonCapacity == 10 && s.World.LookLevel("siphon") == 3, "Level 3, the twin chamber: 10 doses, two a hold, its look rebuilt");

            // The twin chamber: two doses a hold, with the held mouse.
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom",
                () => shop.Held.Count >= 1, "the first hold");
            Check(shop.Held.Count == 2 && shop.Held.All(h => h == "bloom"), "One hold of the twin chamber collects two Bloom");
            shop.Held.Clear();
            Ok(s.Act(() => shop.Upgrade("siphon")), "Siphon level 4, the vacuum (900 credits)");
            shop.GrowerUnits["bloom"] = 1;
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom",
                () => shop.Held.Contains("flow"), "the vacuum reaching the Flow grower");
            Check(shop.Held.First() == "bloom" && shop.Held.Contains("flow") && !shop.Held.Contains("ember"),
                "The vacuum takes the last Bloom, then pulls from the Flow grower within reach; Ember, further off, is not pulled");
            LookAt(GrowthWorld.WorkbenchPosition + new Vector3(-0.9f, 0, 0.4f), GrowthWorld.WorkbenchPosition + new Vector3(0, 0.95f, 0)); yield return Shot("91-siphon-levels");

            // The microscope (bench level 2) on the incubator card.
            Ok(s.Act(() => shop.Upgrade("bench")), "Research bench level 2, the microscope (80 credits and 5 RP)");
            shop.Held.Clear(); shop.Held.AddRange(new[] { "bloom", "ember" });
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Press(Key.E);
            Ok(s.Act(() => shop.AddDose("ember")), "Dose Ember");
            yield return new WaitForSeconds(0.2f);
            card = s.Ui.CardText();
            Check(shop.Microscope && card.Contains("Microscope: ") && card.Contains("from something new"), "The incubator card's microscope says how many doses the mix is from something new");
            yield return Shot("92-microscope");
            Ok(s.Act(() => shop.ReturnDose("ember")), "Take the Ember dose back");
            s.SetCard(GrowthCard.None);

            // Growers to level 4 and the vat to level 3.
            for (int lv = 2; lv <= 4; lv++) Ok(s.Act(() => shop.Upgrade("grower")), "Growers level " + lv);
            Ok(s.Act(() => shop.Upgrade("vat")), "The vat's double batch (level 2, on its card)");
            Ok(s.Act(() => shop.Upgrade("vat")), "The vat's faster batches (level 3)");
            yield return null; yield return null;
            Check(shop.GrowerCapacity == 16 && shop.RefillSeconds == 1 && shop.GrowerCapacityOf("mist") == 32 && shop.RefillSecondsOf("mist") == 0.5f &&
                s.World.LookLevel("grower:bloom") == 4 && s.World.LookLevel("grower:mist") == 3, "Growers hold 16 and refill a dose a second; the vat holds 32 and refills twice as fast; their looks follow");
            LookAt(new Vector3(-1.6f, 0, 6.2f), new Vector3(-3.45f, 0.9f, 6.0f)); yield return Shot("93-growers");
            LookAt(new Vector3(-1.75f, 0, 7.5f), GrowthWorld.GrowerPositions["mist"] + new Vector3(0, 0.8f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "mist", "The reticle focuses the vat");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("The vat · level 3 of 3") && card.Contains("Every grower · level 4 of 4") && card.Contains("It holds 32"), "The vat's card has its ladder and every grower's");
            yield return Shot("94-vat");
            s.SetCard(GrowthCard.None);

            // The incubator's level 3: the tint port and a clutch of two; level 4: three.
            Ok(s.Act(() => shop.Upgrade("incubator")), "Incubator level 2");
            Ok(s.Act(() => shop.Upgrade("incubator")), "Incubator level 3: clutches of two, mixes up to 6, a tint port");
            shop.Held.Clear();
            shop.GrowerUnits["bloom"] = 3;
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom",
                () => shop.Held.Contains("bloom") && shop.Held.Contains("flow"), "Bloom and Flow with the vacuum");
            Check(shop.Held.Count(h => h == "bloom") >= 2 && shop.Held.Contains("flow"), "A held hold of the vacuum: three Bloom from a low grower, then Flow");
            Ok(s.Act(() => shop.TintIncubator("bloom")), "Tint the incubator with a Bloom dose (the card's button)");
            Ok(s.Act(() => shop.AddDose("bloom")), "Dose Bloom");
            Ok(s.Act(() => shop.AddDose("flow")), "Dose Flow");
            Ok(s.Act(shop.StartIncubation), "Incubate");
            s.Advance(shop.IncubationSeconds + 0.05f);
            yield return null; yield return null;
            Check(shop.Hatchlings.Count == 2 && shop.Hatchlings.All(h => h.Species == "pondlet" && h.Colour == Colour.Sunny) && shop.IncubatorTint == null && shop.Journal.Contains("pondlet/Sunny"),
                "A clutch of two Sunny Pondlets: the tint is used up, a new colour in the journal");
            Check(s.World.HatchlingViews.Count == 2, "Both sit on the tray");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -0.95f), GrowthWorld.IncubatorPosition + new Vector3(0, 0.95f, -0.2f)); yield return Shot("95-clutch");
            Ok(s.Act(shop.TakeHatchling), "Take a Sunny Pondlet");
            Ok(s.Act(shop.TakeHatchling), "Take the other");
            shop.Carried.Clear();
            Ok(s.Act(() => shop.Upgrade("incubator")), "Incubator level 4: clutches of three (its auto-doser port waits for power, GR-07)");
            shop.Held.AddRange(new[] { "bloom", "bloom", "flow" });
            Ok(s.Act(() => shop.AddDose("bloom")), "Dose Bloom"); Ok(s.Act(() => shop.AddDose("bloom")), "Dose Bloom"); Ok(s.Act(() => shop.AddDose("flow")), "Dose Flow");
            Ok(s.Act(shop.StartIncubation), "Incubate");
            s.Advance(shop.IncubationSeconds + 0.05f);
            yield return null; yield return null;
            Check(shop.Hatchlings.Count == 3 && shop.Hatchlings.All(h => h.Species == "bubblebelly") && s.World.LookLevel("incubator") == 4, "A clutch of three Bubblebellies; the incubator's look is level 4");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(-0.9f, 0, -1.1f), GrowthWorld.IncubatorPosition + new Vector3(-0.15f, 0.8f, 0)); yield return Shot("96-incubator-level-4");
            for (int i = 0; i < 3; i++) Ok(s.Act(shop.TakeHatchling), "Take a Bubblebelly");
            shop.Carried.Clear();

            // The breeding tank's level 3: two pairs.
            Ok(s.Act(() => shop.Upgrade("breeding")), "Breeding tank level 3: two pairs at once (2,000 credits)");
            foreach (var sp in new[] { "pondlet", "bubblebelly", "pondlet" }) shop.DevelopmentGive(sp);
            yield return AtBreedingTank("The reticle focuses the breeding tank");
            for (int i = 0; i < 3; i++) yield return Click();
            shop.DevelopmentGive("ribbonfin");
            yield return Click();
            Check(shop.PairPlaces == 2 && shop.Pairs.All(p => p.Breeding) && shop.Carried.Count == 0, "Four clicks fill both pairs, and both breed");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Breeding && card.Contains("Pair 1 (2/2") && card.Contains("Pair 2 (2/2"), "The card shows both pairs");
            s.SetCard(GrowthCard.None);
            s.Advance(shop.BreedSeconds + 0.05f);
            yield return null; yield return null;
            Check(shop.Pairs[0].Offspring?.Species == "puddlepup" && shop.Pairs[1].Offspring?.Species == "reedwhisker" && s.World.BreedingViews.Count == 6,
                "40 s later (accelerated) both nurseries hold an offspring: a Puddlepup and a Reedwhisker");
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0, 0, 1.1f), GrowthWorld.BreedingPosition + new Vector3(0, GrowthWorld.BreedingWaterY, 0)); yield return Shot("97-two-pairs");

            // The counter: a bag dispenser, a card reader and a queue of 3; its second till comes once Pip is hired (GR-09).
            Ok(s.Act(() => shop.Upgrade("counter")), "Counter level 2");
            Ok(s.Act(() => shop.Upgrade("counter")), "Counter level 3: the card reader, 0.5 s, a queue of 3");
            LookAt(GrowthWorld.CounterPosition + new Vector3(0, 0, 1.4f), GrowthWorld.CounterPosition + new Vector3(0, 1.05f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Counter, "counter", "The reticle focuses the counter");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(card.Contains("Counter · level 3 of 4") && card.Contains("Hire Pip first: the second till is for a helper") && shop.ServeSeconds == 0.5f && shop.QueueLength == 3,
                "The counter's card: level 3 of 4, the second till once Pip is hired");
            yield return Shot("98-counter");
            s.SetCard(GrowthCard.None);
            foreach (var place in new[] { "b1", "b2" })
            {
                Ok(s.Act(() => shop.BuyContainer("starter", place)), "A Starter tank on " + place);
                var c = shop.ContainerAt(place);
                while (c.Free > 0) shop.DevelopmentStock(c.Id, "pondlet");
            }
            Ok(s.Act(shop.OpenShop), "Open the shop with three tanks of stock (labelled: stocked straight into the tanks)");
            int most = 0;
            for (int i = 0; i < 1600 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.25f);
                most = Mathf.Max(most, shop.Queue.Count());
                if (shop.Queue.Count() > shop.QueueLength) Check(false, "The queue never holds more than 3");
                if (i > 400 && shop.FrontOfQueue != null) shop.Serve(0.25f);
                if (i % 60 == 0) yield return null;
            }
            yield return null;
            s.SetCard(GrowthCard.None);
            Check(most == 3 && shop.Phase == GrowthPhase.Night, "Through a day (accelerated) the queue fills to 3 and never more; then the shop closes");

            // The ladders say what is still needed: the counter's second till needs Pip (GR-09; the analyzer and planner came with GR-08).
            Check(shop.UpgradeCheck("bench", out var bench3, out string waits) == GrowthFailure.None && bench3.Level == 3 &&
                shop.UpgradeCheck("counter", out _, out waits) == GrowthFailure.Locked && waits.Contains("Hire Pip first"), "The second till needs Pip (GR-09); the bench's analyzer is on offer");
            Check(new[] { "siphon", "grower", "incubator", "breeding", "vat" }.All(st => shop.UpgradeCheck(st, out _, out _) == GrowthFailure.MaxLevel), "Every other station is at its highest level");
            LookAt(new Vector3(0.9f, 0, 5.0f), new Vector3(-2.0f, 0.8f, 7.6f)); yield return Shot("99-back-room-levels");

            Check(s.SaveNow(), "Save");
            string before = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            yield return null; yield return null;
            var reloaded = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            Check(JsonUtility.ToJson(s.Shop.Snapshot()) == before && reloaded.version >= 5 && s.Shop.Level("siphon") == 4 && s.Shop.Pairs[1].Offspring != null &&
                s.World.LookLevel("incubator") == 4 && s.World.LookLevel("grower:mist") == 3, "The version 5 save brings the same shop back, every station at its level and looking it");
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
