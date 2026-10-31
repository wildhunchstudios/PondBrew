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
    /// GR-04's Windows route (-evolutionGrowthCulturesSmoke): the journal's Cultures page; the supplier catalog by the
    /// counter, an Ember grower and a Spark starter vial ordered at night and taken back, delivered when the shop opens;
    /// Ember collected from its new grower and mixed into a new Cinderling; the Spark vial collected and mixed into a new
    /// Sparkline before Spark is owned; Spark researched at the pinboard; then (labelled era 5 fixture) the vat, the Brine
    /// grower and Dusk research, all seven sources in the back room; save and reload.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string CulturesScenarioText = "GR-04 cultures: the journal's Cultures page, the supplier catalog with night orders delivered at the next opening, a new grower, a starter vial, research at the pinboard, the vat, all seven cultures, save and reload";

        IEnumerator CollectWithHeldMouse(Vector3 feet, Vector3 target, GrowthTargetKind kind, string id, System.Func<bool> enough, string reason)
        {
            LookAt(feet, target);
            yield return null; yield return null;
            Aimed(kind, id, "The reticle focuses the " + (kind == GrowthTargetKind.Vial ? GrowthShop.Name(id) + " vial" : GrowthShop.Name(id) + " source"));
            Hold(true);
            yield return Wait(enough, 8, reason);
            Hold(false); yield return null;
        }

        IEnumerator CulturesScenario()
        {
            limitations.Add("GR-04 cultures route: fixtures are labelled: an era 2 shop with two stars, 600 credits, 12 research points, the three Brookkin founders known and two Pondlets in the Starter tank; later an era 5, five-star save edit with 5,000 credits and 200 research points. Orders, cancelling and research call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked); doses are collected by holding the route's mouse on the new grower and the vial, and E is pressed at the catalog, the incubator, the pinboard and the tank; one incubation is real (12 s), one accelerated on the shop's clock. Placeholder art.");
            yield return Opening();
            TakeInput();

            var fx = new GrowthShop { Era = 2, Reputation = GrowthContent.StarThresholds[1] + 30, Credits = 600, Rp = 12 };
            fx.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            var starter = fx.Containers[0];
            Stock(fx, starter, "pondlet"); Stock(fx, starter, "pondlet");
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null;
            Check(shop.Cultures.SequenceEqual(new[] { "bloom", "flow" }) && shop.Stars == 2 && shop.Rp == 12, "Fixture: an era 2, two-star shop with Bloom and Flow, 600 credits and 12 research points");

            // The journal's Cultures page: all seven from the start, owned ones with their source, the rest with where to get them.
            s.SetCard(GrowthCard.Journal); yield return new WaitForSeconds(0.2f);
            string journal = s.Ui.CardText();
            Check(journal.Contains("Bloom · grower · level 1") && journal.Contains("Ember · where to get it: supplier catalog, page 2") && journal.Contains("Spark · where to get it: research at the pinboard: 10 research points")
                && journal.Contains("Mist · where to get it: supplier catalog, page 3") && journal.Contains("Dusk · where to get it: research"),
                "The journal's Cultures page lists all seven: Bloom and Flow owned, the rest with where to get them");
            s.Ui.ShowTabNamed("Cultures"); yield return null;
            yield return Shot("60-journal-cultures");
            s.SetCard(GrowthCard.None);
            Check(GrowthWorld.GrowerPositions.Count == 7 && !s.World.HasSource("ember"), "The back room has a slot for each of the seven sources; Ember's is empty");
            LookAt(new Vector3(0.9f, 0, 6.6f), new Vector3(-2.4f, 0.6f, 7.4f)); yield return Shot("61-back-room-slots");

            // The supplier catalog by the counter: an Ember grower and a Spark vial ordered at night; the Spark vial taken back and ordered again.
            LookAt(new Vector3(1.2f, 0, -2.2f), GrowthWorld.CatalogPosition + new Vector3(0, 1.05f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Catalog, "catalog", "The reticle focuses the supplier catalog");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Catalog, "E opens the supplier catalog");
            string catalog = s.Ui.CardText();
            Check(catalog.Contains("Ember grower") && catalog.Contains("150 credits") && catalog.Contains("Order for 150 cr") && catalog.Contains("Catalog page 4: needs 4 stars") && catalog.Contains("Page 3 · 3 stars") && catalog.Contains("Opens at 3 stars"),
                "The catalog's pages: page 2 open (the Ember grower for 150 credits), pages 3 and 4 waiting for their stars");
            yield return Shot("62-catalog");
            Ok(s.Act(() => shop.Order("grower-ember")), "Order the Ember grower");
            Ok(s.Act(() => shop.Order("vial-spark")), "Order a Spark starter vial");
            Check(shop.Credits == 600 - 150 - 40 && shop.Orders.Count == 2, "Both are paid at once: 190 credits");
            Ok(s.Act(() => shop.CancelOrder("vial-spark")), "Take the Spark vial back");
            Check(shop.Credits == 450 && shop.Orders.SequenceEqual(new[] { "grower-ember" }), "Its 40 credits come back");
            Ok(s.Act(() => shop.Order("vial-spark")), "Order the Spark vial again");
            yield return new WaitForSeconds(0.2f);
            Check(s.Ui.CardText().Contains("On order · take it back"), "The catalog shows what is on order");
            yield return Shot("63-catalog-ordered");
            s.SetCard(GrowthCard.None);
            Check(!shop.Owns("ember") && !s.World.HasSource("ember"), "Nothing arrives before the shop opens");

            // Opening the shop: the orders arrive.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            yield return null; yield return null;
            Check(shop.Owns("ember") && s.World.HasSource("ember") && shop.Vials.TryGetValue("spark", out int sparkDoses) && sparkDoses == GrowthContent.VialDoses && shop.Orders.Count == 0,
                "As the shop opens the Ember grower stands in its slot and the Spark vial (3 doses) on its shelf");
            Check(s.Ui.Log.Any(l => l.StartsWith("New culture: Ember")) && s.Ui.Log.Any(l => l == "Delivered: Ember grower"), "The delivery and the new culture are announced");
            LookAt(new Vector3(-1.0f, 0, 6.2f), new Vector3(-3.2f, 0.9f, 6.2f)); yield return Shot("64-delivered");

            // Ember from its new grower, held, and Bloom: a new Cinderling (medium) in the real 12 s.
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("ember"), GrowthWorld.GrowerPositions["ember"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "ember",
                () => shop.Held.Contains("ember"), "an Ember dose from the new grower");
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom",
                () => shop.Held.Contains("bloom"), "a Bloom dose");
            while (shop.Held.Count > 2) shop.Held.Remove(shop.Held.Last(c => c == "ember" || c == "bloom"));
            Check(shop.Held.Count(c => c == "ember") == 1 && shop.Held.Count(c => c == "bloom") == 1, "The siphon holds one Ember and one Bloom");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Press(Key.E);
            Ok(s.Act(() => shop.AddDose("bloom")), "Add the Bloom dose");
            Ok(s.Act(() => shop.AddDose("ember")), "Add the Ember dose");
            Check(shop.Preview().Exact && shop.Preview().Species.Id == "emberpuff", "Bloom 1 + Ember 1 is the Emberpuff's recipe");
            Ok(s.Act(shop.StartIncubation), "Mix and incubate");
            yield return Wait(() => shop.Hatchling != null, 20, "the 12 s incubation");
            yield return new WaitForSeconds(0.3f);
            Check(shop.Discovered.Contains("emberpuff") && shop.Hatchling.Def.Size == SizeClass.Medium, "A new Cinderling: the Emberpuff, a medium creature");
            yield return Shot("65-emberpuff");
            Ok(s.Act(shop.TakeHatchling), "Take the Emberpuff");
            s.SetCard(GrowthCard.None);
            LookAt(GrowthWorld.Places["a1"].Browse + new Vector3(0, 0, -0.6f), GrowthWorld.Places["a1"].Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, starter.Id, "The reticle focuses the Starter tank");
            yield return Click();
            Check(starter.Occupants.Any(o => o.Species == "emberpuff") && starter.Used == 4, "A click puts the Emberpuff (2 spaces) beside the two Pondlets");

            // The Spark vial, held: three doses, Spark not owned; with Flow they make a new Sparkline.
            var bottle = GrowthWorld.VialShelfPosition + new Vector3(-0.5f + 0.25f * 1, 1.1f, 0.06f);
            LookAt(new Vector3(bottle.x + 0.4f, 0, 5.4f), bottle); yield return Shot("66-vial-on-shelf");
            yield return CollectWithHeldMouse(new Vector3(bottle.x, 0, 5.3f), bottle, GrowthTargetKind.Vial, "spark",
                () => !shop.Vials.ContainsKey("spark") || shop.Held.Count >= shop.SiphonCapacity, "the Spark vial's doses");
            Check(shop.Held.Count(c => c == "spark") == GrowthContent.VialDoses && !shop.Vials.ContainsKey("spark") && !shop.Owns("spark"),
                "Holding the siphon on the vial collects its three Spark doses; the vial is used up and Spark is still not owned");
            yield return Shot("66b-vial-used");
            Ok(shop.AddDose("spark"), "Dose Spark"); Ok(shop.AddDose("spark"), "Dose Spark");
            yield return CollectWithHeldMouse(GrowthWorld.GrowerFront("flow"), GrowthWorld.GrowerPositions["flow"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "flow",
                () => shop.Held.Contains("flow"), "a Flow dose");
            Ok(shop.AddDose("flow"), "Dose Flow");
            Check(shop.Preview().Exact && shop.Preview().Species.Id == "sparkminnow", "Flow 1 + Spark 2 is the Sparkminnow's recipe");
            Ok(s.Act(shop.StartIncubation), "Incubate");
            s.Advance(shop.IncubationSeconds + 0.05f);
            Check(shop.Discovered.Contains("sparkminnow") && shop.Hatchling.Def.Family == "sparkline", "A vial's culture made a new founder before Spark is owned (accelerated incubation)");
            Ok(s.Act(shop.TakeHatchling), "Take the Sparkminnow");

            // Research at the pinboard: Spark.
            LookAt(new Vector3(0.5f, 0, GrowthWorld.BoardPosition.z), GrowthWorld.BoardPosition);
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Board, null, "The reticle focuses the pinboard on the back room's east wall");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Board && s.Ui.CardText().Contains("Research Spark · 10 RP") && s.Ui.CardText().Contains("Research Dusk · ") && s.Ui.CardText().Contains("Needs era 5"), "The pinboard offers Spark's research for 10 research points; Dusk waits for era 5");
            s.Ui.ShowTabNamed("Research"); yield return null;
            yield return Shot("67-research");
            int rp = shop.Rp;
            Ok(s.Act(() => shop.Research("spark")), "Research Spark");
            yield return null; yield return null;
            Check(shop.Owns("spark") && shop.Rp == rp - 10 && s.World.HasSource("spark"), "Spark is owned and its grower stands in the back room");
            s.SetCard(GrowthCard.None);
            LookAt(new Vector3(-1.6f, 0, 6.6f), GrowthWorld.GrowerPositions["spark"] + new Vector3(0, 0.9f, 0)); yield return Shot("68-spark-grower");
            for (int i = 0; i < 2000 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(0.5f);
                if (i % 40 == 0) yield return null;
            }
            yield return null;
            s.SetCard(GrowthCard.None);
            Check(shop.Phase == GrowthPhase.Night, "The day ends (accelerated)");

            // Era 5 (labelled save edit): the vat and the Brine grower ordered, Dusk researched; all seven.
            Check(s.SaveNow(), "Save before the era 5 fixture");
            var save = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            save.era = 5; save.reputation = GrowthContent.StarThresholds[4] + 50; save.credits = 5000; save.rp = 200;
            File.WriteAllText(s.SavePath, JsonUtility.ToJson(save));
            Check(s.ReloadSaved() && s.Shop.Era == 5 && s.Shop.Stars == 5, "Load the labelled era 5, five-star fixture");
            shop = s.Shop;
            Ok(s.Act(() => shop.Order("vat")), "Order the conditioning vat (600 credits and 40 RP)");
            Ok(s.Act(() => shop.Order("grower-brine")), "Order the Brine grower (1,500 credits)");
            Ok(s.Act(() => shop.Research("dusk")), "Research Dusk (150 RP)");
            Check(shop.Owns("dusk") && !shop.Owns("mist") && !shop.Owns("brine"), "Dusk is researched at once; the vat and the Brine grower wait for the opening");
            Ok(s.Act(shop.OpenShop), "Open the shop");
            yield return null; yield return null;
            Check(GrowthContent.Cultures.All(c => shop.Owns(c.Id) && s.World.HasSource(c.Id)), "All seven cultures are owned, each with its source in the back room");
            LookAt(new Vector3(1.4f, 0, 4.6f), new Vector3(-2.0f, 0.8f, 7.6f)); yield return Shot("69-all-seven");
            LookAt(new Vector3(-1.75f, 0, 7.5f), GrowthWorld.GrowerPositions["mist"] + new Vector3(0, 0.8f, 0)); // beside the incubator, not in it
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "mist", "The reticle focuses the conditioning vat");
            Check(s.FocusName(out _) == "Conditioning vat (Mist)", "The vat is named as the Mist source");
            yield return Shot("70-vat");
            s.SetCard(GrowthCard.Journal); yield return new WaitForSeconds(0.2f);
            journal = s.Ui.CardText();
            Check(journal.Contains("Mist · conditioning vat") && journal.Contains("Spark · researched, its grower") && journal.Contains("Brine · grower from the supplier") && !journal.Contains("where to get it"),
                "The journal's Cultures page shows all seven owned with their sources");
            s.Ui.ShowTabNamed("Cultures"); yield return null;
            yield return Shot("71-journal-all");
            s.SetCard(GrowthCard.None);

            Check(s.SaveNow(), "Save");
            string before = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            Check(JsonUtility.ToJson(s.Shop.Snapshot()) == before && JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath)).version >= 3, "The save (version 3 and later) brings the same shop back, with its seven cultures");
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
