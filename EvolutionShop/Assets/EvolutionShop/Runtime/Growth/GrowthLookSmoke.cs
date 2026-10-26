#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// GR-16's Windows route (-evolutionGrowthLookSmoke): waves 1 and 2 of the new game's look (GR.48). The back room and shop
    /// unit A dressed with the growth kit; the growers, the incubator, Starter tanks and the counter in the kit's shapes at every
    /// level (wave 1); the vat, the breeding tank, the research bench and its siphon, the vial shelf, the catalog, the pinboard,
    /// Large and Show tanks and lab racks at every level (wave 2); the greybox's shapes hidden but still solid and still focused; every kit piece in the look's own materials; the
    /// reflection probes rendered; the light following the shop from night through morning, midday and closing; the dressed
    /// rooms inside the frame budget; and captures of each by night and by day.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string LookScenarioText = "GR-16 waves 1 and 2, the new game's look: the back room and shop unit A dressed with the growth kit; growers, the incubator, Starter tanks and the counter (wave 1), the vat, the breeding tank, the research bench and its siphon, the vial shelf, the catalog, the pinboard, Large and Show tanks and lab racks (wave 2), in the kit's shapes at every level; the greybox's shapes hidden but still solid and focused; the look's own materials; reflections; the light from night through morning, midday and closing; the dressed rooms inside the frame budget; captures by night and by day";

        Transform Station(string path) => s.World.transform.Find("Stations/" + path);

        static bool HasPiece(Transform t, string asset) => t != null && t.GetComponentsInChildren<Transform>(true).Any(c => c.name == "Look / " + asset);
        static int Pieces(Transform t, string asset) => t == null ? 0 : t.GetComponentsInChildren<Transform>(true).Count(c => c.name == "Look / " + asset);

        /// <summary>A greybox part replaced by the kit: its renderers off, its collider (when it had one) still on.</summary>
        void Replaced(Transform root, string part, bool solid, string what)
        {
            var t = root == null ? null : root.Find(part);
            Check(t != null && t.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), what + ": the greybox's " + part + " is hidden");
            if (solid) Check(t.GetComponent<Collider>() != null && t.GetComponent<Collider>().enabled, what + ": its " + part + " is still solid");
        }

        IEnumerator LookScenario()
        {
            limitations.Add("GR-16 look route: the fixtures are labelled: an era 4 shop with 20,000 credits, Ember owned, a Starter tank on a1 with three creatures, every station at level 1; then the same shop with the growers, the incubator and the counter at level 4 and Starter tanks at levels 2 and 3 on a1 and a3; then (wave 2) the shop with Ember and Mist owned, unit B, the annex and the workshop open, the vat at level 3, the breeding tank at 3, the research bench and the siphon at 4, five starter vials on the shelf, a Large tank at level 3 on a3 and one at level 2 on b1, a Show tank at level 2 on b4 and lab racks at levels 3 and 1 on r1 and r2, with creatures. The day is opened by the shop's own action and its clock set to the morning, midday and closing marks (labelled); frame times are measured as the player runs (vSync on) for 6 s at each view after 1 s settling. Waves 1 and 2 dress the back room and unit A and every station and tank; unit B, unit C, the annex, the workshop and the hall take the look's palette only (waves 3 and 4).");
            yield return Opening();
            TakeInput();
            var w = s.World;
            Check(GrowthWorld.LookOn, "The new game wears its look (no -evolutionGrowthPlain)");

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 50, Credits = 20000, Rp = 300 };
            fx.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Cultures.Add("ember");
            fx.GrowerUnits["bloom"] = 6; fx.GrowerUnits["flow"] = 3; fx.GrowerUnits["ember"] = 8;
            var starter = fx.Containers[0];
            Stock(fx, starter, "pondlet"); Stock(fx, starter, "pondlet"); Stock(fx, starter, "bubblebelly");
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null; yield return null;
            Check(shop.Phase == GrowthPhase.Night && w.LookKeyName == "night", "Fixture: a shop at night, the look's night key (" + w.LookKeyName + ")");

            // The dressing and the stations' pieces.
            var rooms = w.transform.Find("Look rooms");
            foreach (var asset in new[] { "UnitAFloor", "UnitADressing", "Street", "BackRoomFloor", "BackRoomDressing" })
                Check(HasPiece(rooms, asset), "The rooms wear the kit's " + asset);
            Check(w.transform.Find("Rooms/Unit A floor").GetComponent<Renderer>().enabled == false && w.transform.Find("Rooms/Unit A floor").GetComponent<Collider>().enabled,
                "Unit A's greybox floor is hidden under the planks, and still solid");
            var bloom = Station("Bloom grower"); var incubator = Station("Incubator"); var counter = Station("Counter");
            Check(HasPiece(bloom, "Grower") && HasPiece(Station("Flow grower"), "Grower") && HasPiece(Station("Ember grower"), "Grower"), "Each grower wears the kit's grower");
            Replaced(bloom, "Cabinet", true, "The Bloom grower"); Replaced(bloom, "Jar", false, "The Bloom grower");
            var fill = bloom.Find("Culture").GetComponent<Renderer>();
            Check(fill.enabled && fill.sharedMaterial.IsKeywordEnabled("_EMISSION"), "The grower's culture still shows how full it is, glowing");
            Check(HasPiece(incubator, "Incubator"), "The incubator wears the kit's incubator");
            Replaced(incubator, "Base", true, "The incubator"); Replaced(incubator, "Tray", false, "The incubator");
            Check(incubator.Find("Dome").GetComponent<Renderer>().enabled && incubator.Find("Dome").GetComponent<Renderer>().sharedMaterial.shader.name == DioramaLook.GlassShaderName,
                "The incubator's dome stays, in the look's glass");
            Check(HasPiece(counter, "Counter"), "The counter wears the kit's counter");
            Replaced(counter, "Counter body", true, "The counter");
            var tank = w.Tanks[starter.Id].Root;
            Check(HasPiece(tank, "TankStarter") && HasPiece(tank, "TankBed"), "The Starter tank wears the kit's cabinet, frame, pebbles and plants");
            Replaced(tank, "Stand", false, "The Starter tank");
            Check(tank.Find("Water").GetComponent<Renderer>().enabled && tank.Find("Glass").GetComponent<Renderer>().enabled, "The tank's water and glass stay, in the look's materials");
            // Every kit piece in the look's materials: none of the kit's GK_ placeholders reaches the screen.
            var stray = w.LookPieces.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m == null || m.name.StartsWith("GK_")).ToList();
            Check(stray.Count == 0 && w.LookPieces.Count >= 12, "Every kit piece (" + w.LookPieces.Count + ") is in the look's own materials");
            Check(!w.LookPieces.Any(p => p.GetComponentInChildren<Collider>(true) != null), "No kit piece brings a collider (the reticle meets only the greybox's)");
            var cam = s.Player.View;
            Check(cam.GetUniversalAdditionalCameraData().renderPostProcessing, "The camera renders the look's grade (post-processing on)");
            Check(FindObjectsByType<Volume>(FindObjectsSortMode.None).Any(v => v.isGlobal && v.sharedProfile != null && v.sharedProfile.name == "GR look grade"), "The look's grade: bloom, neutral tonemapping, colour, white balance, vignette");
            yield return Wait(() => FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Count(p => p.name.StartsWith("Look /") && p.realtimeTexture != null) == 2, 5, "the rooms' reflections");
            Check(true, "Both dressed rooms' reflection probes are rendered");

            // Still the same stations: the reticle focuses them through the kit.
            LookAt(GrowthWorld.GrowerFront("bloom"), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "bloom", "The reticle focuses the Bloom grower through its new shape");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.4f), GrowthWorld.IncubatorPosition + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            LookAt(GrowthWorld.CounterPosition + new Vector3(0, 0, 1.5f), GrowthWorld.CounterPosition + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            var a1 = GrowthWorld.Places["a1"];
            LookAt(a1.Browse, a1.Position + new Vector3(0, 1.1f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, starter.Id, "The reticle focuses the Starter tank");

            // By night: the lamps carry the rooms.
            float nightSun = w.LookSunIntensity;
            LookAt(new Vector3(0.8f, 0, 3.2f), new Vector3(-1.5f, 1.2f, -2.0f)); yield return Shot("400-unit-a-night");
            LookAt(new Vector3(1.2f, 0, 4.6f), new Vector3(-2.8f, 1.1f, 7.6f)); yield return Shot("401-back-room-night");
            LookAt(GrowthWorld.GrowerFront("flow") + new Vector3(0.2f, 0, 0), GrowthWorld.GrowerPositions["flow"] + new Vector3(0, 1.0f, 0)); yield return Shot("402-growers-night");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0.6f, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 0.9f, 0)); yield return Shot("403-incubator-night");

            // Every level: growers, the incubator and the counter at 4, Starter tanks at 2 and 3.
            var fx2 = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 50, Credits = 20000, Rp = 300 };
            fx2.Discovered.AddRange(fx.Discovered); fx2.Journal.AddRange(fx.Journal);
            fx2.Cultures.Add("ember");
            fx2.GrowerUnits["bloom"] = 12; fx2.GrowerUnits["flow"] = 6; fx2.GrowerUnits["ember"] = 16;
            fx2.Levels["grower"] = 4; fx2.Levels["incubator"] = 4; fx2.Levels["counter"] = 4;
            fx2.Containers[0].Level = 2;
            Stock(fx2, fx2.Containers[0], "pondlet"); Stock(fx2, fx2.Containers[0], "ribbonfin");
            var third = new GrowthContainer { Id = "tank-look", Kind = "starter", Place = "a3", Level = 3 };
            fx2.Containers.Add(third);
            Stock(fx2, third, "bubblebelly");
            s.LoadFixture(fx2);
            shop = s.Shop;
            yield return null; yield return null; yield return null;
            bloom = Station("Bloom grower"); incubator = Station("Incubator"); counter = Station("Counter");
            Check(w.LookLevel("grower:bloom") == 4 && HasPiece(bloom, "GrowerRing") && Pieces(bloom, "GrowerTap") == 2 && HasPiece(bloom, "GrowerLid"),
                "A level 4 grower: its shelf ring, two taps and its lid, in the kit's shapes");
            Check(w.LookLevel("incubator") == 4 && HasPiece(incubator, "IncubatorScreen") && HasPiece(incubator, "IncubatorPort") && HasPiece(incubator, "IncubatorDoser"),
                "A level 4 incubator: its mix log screen, tint port and auto-doser port");
            Check(w.LookLevel("counter") == 4 && HasPiece(counter, "CounterBags") && HasPiece(counter, "CounterReader") && HasPiece(counter, "CounterTill"),
                "A level 4 counter: its bag dispenser, card reader and second till");
            foreach (var key in new[] { "Level 4 parts (grower:bloom)", "Level 4 parts (incubator)", "Level 4 parts (counter)" })
            {
                var parts = (key.Contains("grower") ? bloom : key.Contains("incubator") ? incubator : counter).Find(key);
                Check(parts != null && parts.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.GetComponentsInParent<Transform>(true).Any(p => p.name.StartsWith("Look /"))).All(r => !r.enabled),
                    key + ": the greybox's placeholders hidden behind the kit's");
            }
            var t2 = w.Tanks[fx2.Containers[0].Id].Root; var t3 = w.Tanks["tank-look"].Root;
            Check(HasPiece(t2, "TankFilter") && !HasPiece(t2, "TankLid"), "A level 2 Starter tank: its filter");
            Check(HasPiece(t3, "TankFilter") && HasPiece(t3, "TankLid"), "A level 3 Starter tank: its filter and its lid");
            LookAt(new Vector3(-1.6f, 0, 6.3f), new Vector3(-3.45f, 1.1f, 5.3f)); yield return Shot("404-growers-level-4");
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0.8f, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 0.9f, 0)); yield return Shot("405-incubator-level-4");
            LookAt(GrowthWorld.CounterPosition + new Vector3(-0.6f, 0, 1.6f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0)); yield return Shot("406-counter-level-4");
            LookAt(new Vector3(0.9f, 0, -1.9f), new Vector3(-1.2f, 1.1f, 2.0f)); yield return Shot("407-tank-levels");

            // Wave 2: the vat, the breeding tank, the research bench and its siphon at their top levels, the vial shelf with five
            // vials, the catalog, the pinboard, Large and Show tanks and lab racks.
            var fx3 = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 50, Credits = 20000, Rp = 300, UnitB = true, Annex = true, Workshop = true };
            fx3.Discovered.AddRange(fx.Discovered); fx3.Journal.AddRange(fx.Journal);
            fx3.Cultures.AddRange(new[] { "ember", "mist" });
            fx3.GrowerUnits["bloom"] = 12; fx3.GrowerUnits["flow"] = 6; fx3.GrowerUnits["ember"] = 16; fx3.GrowerUnits["mist"] = 4;
            fx3.Levels["grower"] = 4; fx3.Levels["incubator"] = 4; fx3.Levels["counter"] = 4;
            fx3.Levels["vat"] = 3; fx3.Levels["breeding"] = 3; fx3.Levels["bench"] = 4; fx3.Levels["siphon"] = 4;
            foreach (var (culture, doses) in new[] { ("ember", 3), ("spark", 2), ("mist", 3), ("brine", 1), ("dusk", 2) }) fx3.Vials[culture] = doses;
            fx3.Containers[0].Level = 2;
            Stock(fx3, fx3.Containers[0], "pondlet"); Stock(fx3, fx3.Containers[0], "bubblebelly");
            var tanks = new[] { ("large-look", "large", "a3", 3), ("large-look-b", "large", "b1", 2), ("show-look", "show", "b4", 2), ("rack-look", "rack", "r1", 3), ("rack-look-2", "rack", "r2", 1) };
            foreach (var (id, kind, place, level) in tanks)
            {
                var c = new GrowthContainer { Id = id, Kind = kind, Place = place, Level = level };
                fx3.Containers.Add(c);
                foreach (var sp in kind == "rack" ? new[] { "ribbonfin" } : new[] { "pondlet", "bubblebelly", "ribbonfin", "pondlet" }) Stock(fx3, c, sp);
            }
            s.LoadFixture(fx3);
            shop = s.Shop;
            yield return null; yield return null; yield return null;
            var vat = Station("Mist vat"); var breeding = Station("Breeding tank (level 3)"); var bench = Station("Research bench");
            Check(HasPiece(vat, "Vat") && w.LookLevel("grower:mist") == 3 && HasPiece(vat, "VatBatch") && HasPiece(vat, "VatStirrer"),
                "A level 3 vat: its glass column, its second batch and its stirrer, in the kit's shapes");
            Replaced(vat, "Vat base", true, "The vat"); Replaced(vat, "Vat", false, "The vat");
            var mistFill = vat.Find("Culture").GetComponent<Renderer>();
            Check(mistFill.enabled && mistFill.sharedMaterial.IsKeywordEnabled("_EMISSION"), "The vat's Mist still shows how full it is, glowing");
            Check(breeding != null && HasPiece(breeding, "BreedingTank") && HasPiece(breeding, "BreedingPort") && HasPiece(breeding, "BreedingPair"),
                "A level 3 breeding tank: its cabinet, frame and divider, its tint port and its second pair's frame");
            Replaced(breeding, "Cabinet", true, "The breeding tank");
            Check(breeding.Find("Water").GetComponent<Renderer>().enabled && breeding.Find("Glass").GetComponent<Renderer>().enabled && breeding.Find("Pair divider").GetComponent<Renderer>().enabled,
                "The breeding tank's glass, water and second pair's divider stay");
            Check(HasPiece(bench, "Bench") && HasPiece(bench, "SiphonBulb") && w.LookLevel("bench") == 4 && HasPiece(bench, "BenchMicroscope") && HasPiece(bench, "BenchAnalyzer") && HasPiece(bench, "BenchPlanner"),
                "A level 4 research bench: its microscope, analyzer and planner");
            Check(w.LookLevel("siphon") == 4 && HasPiece(bench, "SiphonBig") && HasPiece(bench, "SiphonTwin") && HasPiece(bench, "SiphonVacuum"),
                "A level 4 siphon: its bigger bulb, twin chamber and vacuum");
            Replaced(bench, "Bench top", true, "The research bench"); Replaced(bench, "Siphon bulb", false, "The research bench");
            foreach (var key in new[] { "Level 3 parts (grower:mist)", "Level 4 parts (bench)", "Level 4 parts (siphon)" })
            {
                var parts = (key.Contains("mist") ? vat : bench).Find(key);
                Check(parts != null && parts.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.GetComponentsInParent<Transform>(true).Any(p => p.name.StartsWith("Look /"))).All(r => !r.enabled),
                    key + ": the greybox's placeholders hidden behind the kit's");
            }
            var shelf = Station("Vial shelf");
            var bottles = shelf.Cast<Transform>().Where(t => t.name.EndsWith(" vial")).ToList();
            Check(HasPiece(shelf, "VialShelf") && bottles.Count == 5 && bottles.All(b => b.gameObject.activeSelf && HasPiece(b, "VialBottle") && b.Find("Culture").GetComponent<Renderer>().enabled),
                "The vial shelf in the kit's shape, each of its five vials a glass bottle with its culture inside");
            Check(HasPiece(Station("Supplier catalog"), "Catalog"), "The supplier catalog: a lectern with a big open book");
            Replaced(Station("Supplier catalog"), "Stand", true, "The catalog");
            Check(HasPiece(Station("Pinboard"), "Pinboard"), "The pinboard: a cork board with three papers pinned");
            Replaced(Station("Pinboard"), "Board", true, "The pinboard");
            var large = w.Tanks["large-look"].Root; var largeB = w.Tanks["large-look-b"].Root; var show = w.Tanks["show-look"].Root;
            var rack = w.Tanks["rack-look"].Root; var rack2 = w.Tanks["rack-look-2"].Root;
            Check(HasPiece(large, "TankLarge") && HasPiece(large, "TankBedLarge") && HasPiece(large, "TankFilter") && HasPiece(large, "TankLidLarge"), "A level 3 Large tank: its cabinet, scene, filter and lid");
            Check(HasPiece(largeB, "TankLarge") && HasPiece(largeB, "TankFilter") && !HasPiece(largeB, "TankLidLarge"), "A level 2 Large tank: its filter, no lid yet");
            Check(HasPiece(show, "TankShow") && HasPiece(show, "TankBedShow") && HasPiece(show, "TankFilter") && HasPiece(show, "TankSpotlight"), "A level 2 Show tank: its plinth, light hood, scene, filter and spotlight");
            Replaced(show, "Light bar", false, "The Show tank"); Replaced(show, "Spotlight", false, "The Show tank");
            Check(w.Tanks["show-look"].ShowLight != null && w.Tanks["show-look"].ShowLight.enabled, "The Show tank's lamp still lights it");
            Check(HasPiece(rack, "TankRack") && HasPiece(rack, "TankFilter") && HasPiece(rack, "TankLidRack") && !HasPiece(rack, "TankBed"), "A level 3 lab rack: its shelving, filter and lid, no plants");
            Check(HasPiece(rack2, "TankRack") && !HasPiece(rack2, "TankFilter"), "A level 1 lab rack: its shelving");
            Replaced(rack, "Stand", true, "The lab rack"); Replaced(rack, "Rack post", false, "The lab rack");
            foreach (var (t, name) in new[] { (large, "Large tank"), (show, "Show tank"), (rack, "lab rack") })
                Check(t.Find("Water").GetComponent<Renderer>().enabled && t.Find("Glass").GetComponent<Renderer>().enabled, "The " + name + "'s water and glass stay, in the look's materials");
            stray = w.LookPieces.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m == null || m.name.StartsWith("GK_")).ToList();
            Check(stray.Count == 0 && w.LookPieces.Count >= 45, "Every kit piece of both waves (" + w.LookPieces.Count + ") is in the look's own materials");
            Check(!w.LookPieces.Any(p => p.GetComponentInChildren<Collider>(true) != null), "Still no kit piece brings a collider");

            // Still the same stations: the reticle focuses each through its new shape.
            LookAt(GrowthWorld.GrowerFront("mist"), GrowthWorld.GrowerPositions["mist"] + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "mist", "The reticle focuses the vat");
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0, 0, 1.3f), GrowthWorld.BreedingPosition + new Vector3(0, 0.6f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Breeding, null, "The reticle focuses the breeding tank");
            LookAt(GrowthWorld.WorkbenchPosition + new Vector3(-1.25f, 0, 0), GrowthWorld.WorkbenchPosition + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Workbench, null, "The reticle focuses the research bench");
            LookAt(GrowthWorld.VialShelfPosition + new Vector3(-0.5f, 0, 1.2f), GrowthWorld.VialShelfPosition + new Vector3(-0.5f, 1.08f, 0.06f)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Vial, "ember", "The reticle focuses the Ember vial through its bottle");
            LookAt(GrowthWorld.CatalogPosition + new Vector3(0, 0, 1.2f), GrowthWorld.CatalogPosition + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Catalog, null, "The reticle focuses the catalog");
            LookAt(new Vector3(0.6f, 0, 5.6f), GrowthWorld.BoardPosition); yield return null; yield return null;
            Aimed(GrowthTargetKind.Board, null, "The reticle focuses the pinboard");
            var b4 = GrowthWorld.Places["b4"]; var r1 = GrowthWorld.RackPlaces["r1"];
            LookAt(b4.Browse, b4.Position + new Vector3(0, 1.0f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, "show-look", "The reticle focuses the Show tank");
            LookAt(r1.Browse, r1.Position + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, "rack-look", "The reticle focuses the lab rack");

            // By night.
            LookAt(GrowthWorld.GrowerFront("mist") + new Vector3(0.6f, 0, 0.1f), GrowthWorld.GrowerPositions["mist"] + new Vector3(0, 0.95f, 0)); yield return Shot("430-vat-level-3");
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0.45f, 0, 1.5f), GrowthWorld.BreedingPosition + new Vector3(0, 0.9f, 0)); yield return Shot("431-breeding-tank-level-3");
            LookAt(GrowthWorld.WorkbenchPosition + new Vector3(-1.3f, 0, -0.35f), GrowthWorld.WorkbenchPosition + new Vector3(0, 1.05f, 0)); yield return Shot("432-research-bench-level-4");
            LookAt(new Vector3(-1.3f, 0, 6.2f), new Vector3(-1.3f, 0.95f, 4.3f)); yield return Shot("433-vials-and-breeding");
            LookAt(new Vector3(0.1f, 0, 5.4f), GrowthWorld.BoardPosition + new Vector3(0, -0.05f, 0)); yield return Shot("434-pinboard");
            LookAt(GrowthWorld.CatalogPosition + new Vector3(0.25f, 0, 1.3f), GrowthWorld.CatalogPosition + new Vector3(0, 0.95f, 0)); yield return Shot("435-catalog");
            var a3 = GrowthWorld.Places["a3"];
            LookAt(new Vector3(a3.Position.x, 0, -2.3f), a3.Position + new Vector3(0, 1.0f, 0)); yield return Shot("436-large-tank-level-3");
            LookAt(new Vector3(8.4f, 0, -0.2f), b4.Position + new Vector3(0, 1.1f, 0)); yield return Shot("437-show-tank-level-2");
            LookAt(new Vector3(7.9f, 0, 11.9f), new Vector3(7.9f, 0.9f, 9.6f)); yield return Shot("438-lab-racks");

            // By day: the shop opens in the morning; the sun comes in by the window; midday; closing.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            yield return new WaitForSeconds(2.5f);
            Check(w.LookKeyName == "morning" && w.LookSunIntensity > nightSun + 0.8f, "The morning key: the sun up (" + w.LookSunIntensity.ToString("0.00") + " against " + nightSun.ToString("0.00") + " at night)");
            LookAt(new Vector3(1.0f, 0, 3.0f), new Vector3(-2.2f, 1.0f, -3.0f)); yield return Shot("410-unit-a-morning");
            shop.DaySecondsLeft = GrowthContent.DaySeconds * 0.5f;
            yield return new WaitForSeconds(2.5f);
            Check(w.LookKeyName == "midday", "Halfway through the day, the midday key");
            LookAt(new Vector3(0.6f, 0, 2.6f), new Vector3(-2.4f, 0.4f, -2.4f)); yield return Shot("411-unit-a-midday-window");
            LookAt(new Vector3(1.2f, 0, 4.6f), new Vector3(-2.8f, 1.1f, 7.6f)); yield return Shot("412-back-room-day");
            LookAt(new Vector3(-0.5f, 0, -2.0f), new Vector3(-0.5f, 1.2f, -8.0f)); yield return Shot("413-the-street");
            LookAt(new Vector3(-2.2f, 0, 5.0f), new Vector3(1.4f, 1.0f, 7.0f)); yield return Shot("415-back-room-bench-day");
            LookAt(new Vector3(-1.6f, 0, 6.6f), new Vector3(-0.8f, 0.9f, 4.2f)); yield return Shot("416-breeding-and-vials-day");
            LookAt(new Vector3(8.4f, 0, -0.2f), b4.Position + new Vector3(0, 1.1f, 0)); yield return Shot("417-show-tank-day");
            yield return Measure("420-look-unit-a", new Vector3(0.8f, 0, 3.2f), new Vector3(-1.5f, 1.2f, -2.0f), 6);
            yield return Measure("421-look-back-room", new Vector3(1.2f, 0, 4.6f), new Vector3(-2.8f, 1.1f, 7.6f), 6);
            yield return Measure("422-look-unit-b-show-tank", new Vector3(8.4f, 0, -0.2f), b4.Position + new Vector3(0, 1.1f, 0), 6);
            shop.DaySecondsLeft = GrowthContent.DaySeconds * 0.1f;
            yield return new WaitForSeconds(2.5f);
            Check(w.LookKeyName == "closing", "Near the day's end, the closing key: the low amber sun");
            LookAt(new Vector3(1.0f, 0, 3.0f), new Vector3(-2.2f, 1.0f, -3.0f)); yield return Shot("414-unit-a-closing");
        }
    }
}
#endif
