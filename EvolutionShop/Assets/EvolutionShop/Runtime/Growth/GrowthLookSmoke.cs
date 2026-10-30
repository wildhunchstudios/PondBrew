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
    /// GR-16's Windows route (-evolutionGrowthLookSmoke): waves 1 to 5 of the new game's look (GR.48) and its sounds. The back room and shop
    /// unit A dressed with the growth kit; the growers, the incubator, Starter tanks and the counter in the kit's shapes at every
    /// level (wave 1); the vat, the breeding tank, the research bench and its siphon, the vial shelf, the catalog, the pinboard,
    /// Large and Show tanks and lab racks at every level (wave 2); the machines, the lab annex and the workshop, and the pipes and
    /// the chute (wave 3); units B and C, the hall, the set pieces, the exhibition's pieces, the boards and the partitions (wave
    /// 4); the sounds and their settings (wave 5); the greybox's shapes hidden but still solid and still focused; every kit piece in the look's own materials; the
    /// reflection probes rendered; the light following the shop from night through morning, midday and closing; the dressed
    /// rooms inside the frame budget; and captures of each by night and by day.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string LookScenarioText = "GR-16 waves 1 and 2, the new game's look: the back room and shop unit A dressed with the growth kit; growers, the incubator, Starter tanks and the counter (wave 1), the vat, the breeding tank, the research bench and its siphon, the vial shelf, the catalog, the pinboard, Large and Show tanks and lab racks (wave 2), the generator, pumps, buffers, the power board, the sorter, the lab annex, the workshop, the pipes and the chute (wave 3), units B and C, the hall's banners and spotlights, the bay window bench, the window display, the feature wall and tank, the exhibition sign, award and tags, the staff board, the hall's plans and the partitions (wave 4), in the kit's shapes at every level; the sounds where they happen, scaled by their settings (wave 5); the greybox's shapes hidden but still solid and focused; the look's own materials; reflections; the light from night through morning, midday and closing; the dressed rooms inside the frame budget; captures by night and by day";

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
            limitations.Add("GR-16 look route: the fixtures are labelled: an era 4 shop with 20,000 credits, Ember owned, a Starter tank on a1 with three creatures, every station at level 1; then the same shop with the growers, the incubator and the counter at level 4 and Starter tanks at levels 2 and 3 on a1 and a3; then (wave 2) the shop with Ember and Mist owned, unit B, the annex and the workshop open, the vat at level 3, the breeding tank at 3, the research bench and the siphon at 4, five starter vials on the shelf, a Large tank at level 3 on a3 and one at level 2 on b1, a Show tank at level 2 on b4 and lab racks at levels 3 and 1 on r1 and r2, with creatures; then (wave 3) the look start point (GrowthStarts.Look: the spaces start with every station at its top level, three piped buffers, the tide engine, fast pumps, the sorter and its chute), whose generator and pumps are then set back to levels 1, 2 and 3 to see each (labelled); pipes are watched flowing on the shop's own power for up to 8 s. The day is opened by the shop's own action and its clock set to the morning, midday and closing marks (labelled); frame times are measured as the player runs (vSync on) for 6 s at each view after 1 s settling. Waves 1 to 3 dress the back room, unit A, the lab annex and the workshop and every station, tank and machine. Wave 4 runs on the look start too (with unit C and the hall open, Show tanks on c1 and c2, a Large tank on e1); the exhibition's sign, a display tag and the award are shown by setting the shop's exhibition flags and one creature's display mark directly for a few frames (labelled), then setting them back; the partitions are seen on two fixtures of their own (an era 5, five-star shop with 30,000 credits and every room closed; the same with unit B and the annex open), and unit C is opened on the second with the shop's own action. Wave 5 (sounds) can only be checked as voices, volumes and positions (requests, not a listening measurement): the hum and the whirr on the look start's own power, the crank and the hand pump held with the route's mouse after setting the generator and pumps back to level 1 (labelled), an offspring put in the breeding tank and taken out again, and Pondlets put into the emptied Large tank on e1 with the routes' fixture action to make a school (labelled).");
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
            yield return Wait(() => FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Count(p => p.name.StartsWith("Look /") && p.realtimeTexture != null) == 6, 5, "the rooms' reflections");
            Check(true, "The dressed rooms' reflection probes are rendered (unit A, the back room, the lab annex, the workshop, unit B, unit C)");

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

            // Wave 3: the machines, the lab annex and the workshop, the pipes and the chute, on the look start point.
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Look));
            shop = s.Shop;
            yield return null; yield return null; yield return null;
            Check(shop.Level("generator") == 4 && shop.Level("pump") == 3 && shop.HasSorter && shop.Lines.Count() >= 3 && shop.Chute != null,
                "Fixture: the look start, the tide engine, fast pumps, three piped buffers, the sorter and its chute");
            var dressed = w.transform.Find("Look rooms");
            foreach (var asset in new[] { "AnnexFloor", "AnnexDressing", "WorkshopFloor", "WorkshopDressing" })
                Check(HasPiece(dressed, asset), "The annex and the workshop wear the kit's " + asset);
            Replaced(w.transform.Find("Lab annex"), "Annex floor", true, "The lab annex");
            Replaced(w.transform.Find("Workshop"), "Workshop floor", true, "The workshop");
            var sets = w.transform.Find("Set pieces");
            Check(HasPiece(sets.Find("Pipe gallery"), "PipeGallery") && HasPiece(sets.Find("Workshop dome"), "WorkshopDome"), "GR-10's pipe gallery and dome light in the kit's shapes");
            var lab = w.transform.Find("Lab annex");
            var generator = lab.Find("Generator");
            Check(w.GeneratorInWorkshop && HasPiece(generator, "GenTide"), "The tide engine in the workshop, in the kit's shape");
            Check(generator.GetComponentsInChildren<Renderer>(true).Count(r => r.name.StartsWith("Bubble") && r.enabled) >= 24, "Its columns' bubbles still rise (24 of them)");
            var buffers = lab.Cast<Transform>().Where(t => t.name.StartsWith("Buffer ") && !t.name.StartsWith("Buffer place")).ToList();
            Check(buffers.Count >= 3 && buffers.All(b => HasPiece(b, "Buffer") && HasPiece(b, "BufferTag" + b.name.Substring(7))), "Each buffer (" + buffers.Count + ") a glass column with its numbered tag");
            Replaced(buffers[0], "Tank", false, "A buffer");
            Check(buffers.Any(b => b.Find("Culture").gameObject.activeSelf && b.Find("Culture").GetComponent<Renderer>().enabled), "A buffer's culture still shows how full it is");
            var bloomGrower = Station("Bloom grower");
            var pump = bloomGrower.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Pump ("));
            Check(pump != null && HasPiece(pump, "PumpMotor"), "A piped grower's fast pump: an electric motor in the kit's shape");
            Check(HasPiece(lab.Find("Power board"), "PowerBoard") && lab.Find("Power board/Supply").GetComponent<Renderer>().enabled, "The power board in the kit's shape, its supply and draw bars still moving");
            var sorter = lab.Find("Sorter");
            Check(HasPiece(sorter, "Sorter") && HasPiece(lab.Find("Incubator outlet"), "IncubatorOutlet"), "The sorter and the incubator's outlet in the kit's shapes");
            Replaced(sorter.GetChild(sorter.childCount - 1), "Cabinet", true, "The sorter");
            Check(w.LookPipeCount == shop.Pipes.Count(p => !p.Chute) && w.ChuteBuilt, "Every pipe (" + w.LookPipeCount + ") in the look's glass with its culture's core, and the chute");
            var chuteGlass = w.transform.Find("Pipes").GetComponentsInChildren<Renderer>().Where(r => r.name == "Glass").Select(r => r.sharedMaterial.shader.name).Distinct().ToList();
            Check(chuteGlass.Count > 0 && chuteGlass.All(n => n == DioramaLook.GlassShaderName), "The pipes' and the chute's tubes are the look's glass");
            w.ResetLookPulsePeak();
            yield return Wait(() => w.LookPipesFlowing > 0 && w.LookPulsePeak > 0.5f, 8, "a line flowing and a dose's pulse");
            Check(true, "The lines flow on the shop's power (" + w.LookPipesFlowing + " flowing): beads travel down them, and a dose into a buffer pulses its pipe");
            stray = w.LookPieces.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m == null || m.name.StartsWith("GK_")).ToList();
            Check(stray.Count == 0, "Every kit piece of the three waves (" + w.LookPieces.Count + ") is in the look's own materials");
            Check(!w.LookPieces.Any(p => p.GetComponentInChildren<Collider>(true) != null), "Still no kit piece brings a collider");
            var labProbes = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p => p.name == "Look / reflections (lab annex)" || p.name == "Look / reflections (workshop)").ToList();
            Check(labProbes.Count == 2 && labProbes.All(p => p.realtimeTexture != null), "The annex's and the workshop's reflection probes are rendered");

            // The reticle still focuses each machine.
            var gen = generator.position; var board = lab.Find("Power board").position;
            LookAt(gen + new Vector3(0, 0, -1.6f), gen + new Vector3(0, 0.9f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Generator, null, "The reticle focuses the tide engine");
            LookAt(new Vector3(2.4f, 0, board.z), board); yield return null; yield return null;
            Aimed(GrowthTargetKind.PowerBoard, null, "The reticle focuses the power board");
            LookAt(buffers[0].position + new Vector3(0, 0, 1.2f), buffers[0].position + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Buffer, (int.Parse(buffers[0].name.Substring(7)) - 1).ToString(), "The reticle focuses " + buffers[0].name);
            LookAt(sorter.position + new Vector3(0, 0, -1.7f), sorter.position + new Vector3(0, 0.8f, 0)); yield return null; yield return null;
            Aimed(GrowthTargetKind.Sorter, null, "The reticle focuses the sorter");
            LookAt(pump.position + new Vector3(0.9f, -0.42f, 0), pump.position); yield return null; yield return null;
            Aimed(GrowthTargetKind.Pump, "bloom", "The reticle focuses the Bloom grower's pump");

            // By night: the annex, the workshop, the line.
            LookAt(new Vector3(2.6f, 0, 10.0f), new Vector3(-2.0f, 1.2f, 13.5f)); yield return Shot("440-annex");
            LookAt(new Vector3(-1.0f, 0, 12.2f), new Vector3(-2.2f, 1.2f, 9.4f)); yield return Shot("441-buffers-and-pipes");
            LookAt(new Vector3(2.0f, 0, 12.5f), board); yield return Shot("442-power-board");
            LookAt(new Vector3(-1.6f, 0, 12.4f), new Vector3(-3.84f, 1.8f, 12.7f)); yield return Shot("443-pipe-gallery");
            LookAt(new Vector3(6.2f, 0, 10.8f), new Vector3(8.8f, 1.2f, 13.6f)); yield return Shot("444-workshop");
            LookAt(gen + new Vector3(-0.6f, 0, -2.2f), gen + new Vector3(0, 1.0f, 0)); yield return Shot("445-tide-engine-level-4");
            LookAt(sorter.position + new Vector3(0.8f, 0, -2.0f), sorter.position + new Vector3(0, 1.0f, 0)); yield return Shot("446-sorter-and-chute");
            LookAt(new Vector3(-2.2f, 0, 5.4f), new Vector3(-3.0f, 2.6f, 7.4f)); yield return Shot("447-pipes-over-the-back-room");
            LookAt(GrowthWorld.GrowerFront("flow") + new Vector3(0.3f, 0, -0.5f), GrowthWorld.GrowerPositions["flow"] + new Vector3(0, 0.6f, 0)); yield return Shot("448-electric-pumps");
            yield return Measure("423-look-annex", new Vector3(2.6f, 0, 10.0f), new Vector3(-2.0f, 1.2f, 13.5f), 6);
            yield return Measure("424-look-workshop", new Vector3(6.2f, 0, 10.8f), new Vector3(8.8f, 1.2f, 13.6f), 6);

            // Every level of the generator and the pumps (set back on the same shop, labelled): the hand crank and the hand pump,
            // the bubble turbine and electric pumps, the twin turbine.
            shop.Levels["generator"] = 1; shop.Levels["pump"] = 1;
            yield return null; yield return null; yield return null;
            generator = lab.Find("Generator");
            pump = bloomGrower.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Pump ("));
            Check(!w.GeneratorInWorkshop && HasPiece(generator, "GenCrank") && HasPiece(generator, "GenCrankWheel"), "A level 1 generator back in the annex: the hand crank and its wheel");
            Check(generator.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Look / GenCrankWheel" && t.parent.name == "Crank wheel"), "The crank's wheel on the greybox's wheel, so it turns while cranked");
            Check(pump != null && HasPiece(pump, "PumpHand") && HasPiece(pump, "PumpHandWheel"), "Hand pumps: the housing and the wheel that turns while it pumps");
            gen = generator.position;
            LookAt(gen + new Vector3(-0.5f, 0, -1.8f), gen + new Vector3(0, 0.8f, 0)); yield return Shot("449-hand-crank-level-1");
            LookAt(GrowthWorld.GrowerFront("bloom") + new Vector3(0.2f, 0, 0.3f), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 0.6f, 0)); yield return Shot("450-hand-pump");
            shop.Levels["generator"] = 2; shop.Levels["pump"] = 2;
            yield return null; yield return null; yield return null;
            Check(HasPiece(lab.Find("Generator"), "GenTurbine") && !w.GeneratorInWorkshop, "A level 2 generator in the annex: the bubble turbine");
            LookAt(gen + new Vector3(-0.5f, 0, -1.8f), gen + new Vector3(0, 1.0f, 0)); yield return Shot("451-bubble-turbine-level-2");
            shop.Levels["generator"] = 3;
            yield return null; yield return null; yield return null;
            generator = lab.Find("Generator"); gen = generator.position;
            Check(HasPiece(generator, "GenTwin") && w.GeneratorInWorkshop, "A level 3 generator in the workshop: the twin turbine");
            LookAt(gen + new Vector3(-0.5f, 0, -2.0f), gen + new Vector3(0, 1.0f, 0)); yield return Shot("452-twin-turbine-level-3");
            shop.Levels["generator"] = 4; shop.Levels["pump"] = 3;
            yield return null; yield return null; yield return null;

            // Wave 4: units B and C and the hall, their set pieces, the exhibition's pieces, the boards, on the look start (unit C
            // and the hall open, Show tanks on c1 and c2, a Large tank on e1).
            Check(shop.UnitC && shop.Hall && shop.ContainerAt("c1") != null && shop.ContainerAt("e1") != null,
                "Fixture: the look start with unit C and the exhibition hall open, Show tanks on c1 and c2, a Large tank on e1");
            foreach (var asset in new[] { "UnitBFloor", "UnitBDressing", "StreetEast", "UnitCFloor", "UnitCDressing", "FeatureWall" })
                Check(HasPiece(dressed, asset), "Units B and C wear the kit's " + asset);
            Replaced(w.transform.Find("Rooms"), "Unit B floor", true, "Unit B");
            var unitC = w.transform.Find("Shop unit C");
            Replaced(unitC, "Unit C floor", true, "Unit C"); Replaced(unitC, "Feature wall", false, "Unit C");
            var bay = sets.Find("Bay window bench");
            Check(HasPiece(bay, "BayWindow"), "The bay window bench: its window, seat, cushion and pillows, and the hanging bowls");
            Replaced(bay, "Seat", true, "The bay window bench"); Replaced(sets, "Bay window sill", true, "The bay window");
            Check(HasPiece(sets.Find("Window display"), "WindowDisplay") && sets.Find("Window display/Water").GetComponent<Renderer>().enabled,
                "Unit A's window display: its base, light hood and scene, the greybox's water kept");
            var feature = unitC.Find("Feature tank (set piece)");
            Check(HasPiece(feature, "FeatureTank") && feature.Find("Water").GetComponent<Renderer>().enabled && feature.Find("Glass").GetComponent<Renderer>().enabled,
                "Unit C's feature tank: its cabinet, bezel, light hood and scene, the greybox's water and glass kept");
            Replaced(feature, "Frame", true, "The feature tank");
            var hall = w.transform.Find("Exhibition hall finish");
            var banners = hall.Cast<Transform>().Where(t => t.name.StartsWith("Banner ") && t.name != "Banner rod").ToList();
            Check(banners.Count == 5 && banners.All(b => HasPiece(b, "BannerCoral") || HasPiece(b, "BannerYellow") || HasPiece(b, "BannerBlue")) && w.BannersShown == 5,
                "The hall's five banners in the kit's coral, yellow and blue, unrolled");
            Replaced(hall, "Banner rod", false, "The hall's banners");
            var fixtures = hall.Cast<Transform>().Where(t => t.name.StartsWith("Fixture over ")).ToList();
            Check(fixtures.Count == 5 && fixtures.All(x => x.gameObject.activeSelf && HasPiece(x, "HallSpot")) && w.LampIntensity("hall") > 10.9f,
                "A fixture over each of the hall's five spotlights, lit");
            var staff = w.transform.Find("Staff board"); var plans = w.transform.Find("Exhibition hall plans");
            Check(HasPiece(staff, "StaffBoard") && HasPiece(plans, "HallBoard"), "The staff board and the hall's plans in the kit's shapes");
            Replaced(staff, "Board", true, "The staff board"); Replaced(plans, "Board", true, "The hall's plans");
            foreach (var (root, piece) in new[] { ("Unit B partition", "PartitionB"), ("Unit C partition", "PartitionC"), ("Lab annex partition", "PartitionAnnex"), ("Workshop partition", "PartitionWorkshop") })
            {
                Check(HasPiece(w.transform.Find(root), piece), "The " + root.ToLowerInvariant() + " a painted hoarding (" + piece + ")");
                Replaced(w.transform.Find(root), "Partition", false, "The " + root.ToLowerInvariant());
            }
            Check(Mathf.Abs(w.LampIntensity("unit-b") - w.LampFull("unit-b")) < 0.05f && w.LampFull("unit-b") > 2.6f && Mathf.Abs(w.LampIntensity("unit-c") - w.LampFull("unit-c")) < 0.05f && w.LampFull("unit-c") > 2.6f,
                "Units B and C have two lamps each under their pendants, at the night key (" + w.LampFull("unit-b").ToString("0.0") + " and " + w.LampFull("unit-c").ToString("0.0") + ")");
            stray = w.LookPieces.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m == null || m.name.StartsWith("GK_")).ToList();
            Check(stray.Count == 0, "Every kit piece of the four waves (" + w.LookPieces.Count + ") is in the look's own materials");
            Check(!w.LookPieces.Any(p => p.GetComponentInChildren<Collider>(true) != null), "Still no kit piece brings a collider");

            // The reticle still focuses the boards.
            LookAt(new Vector3(-0.45f, 0, 2.4f), GrowthWorld.StaffBoardPosition); yield return null; yield return null;
            Aimed(GrowthTargetKind.StaffBoard, null, "The reticle focuses the staff board");
            yield return Shot("464-staff-board");
            LookAt(new Vector3(10.3f, 0, 7.3f), GrowthWorld.HallBoardPosition); yield return null; yield return null;
            Aimed(GrowthTargetKind.HallBoard, null, "The reticle focuses the hall's plans");
            yield return Shot("465-hall-plans");

            // By night: unit B, the bay window, unit C, the feature tank, the window display, the banners.
            LookAt(new Vector3(4.8f, 0, 1.6f), new Vector3(10.5f, 1.3f, -2.4f)); yield return Shot("460-unit-b");
            LookAt(new Vector3(8.0f, 0, -1.2f), new Vector3(8.0f, 1.6f, -3.9f)); yield return Shot("461-bay-window");
            LookAt(new Vector3(11.0f, 0, 4.6f), new Vector3(4.0f, 1.4f, 8.4f)); yield return Shot("462-unit-c");
            LookAt(new Vector3(5.975f, 0, 6.4f), new Vector3(5.975f, 1.4f, 8.75f)); yield return Shot("463-feature-tank");
            LookAt(new Vector3(-2.6f, 0, -2.6f), new Vector3(-2.6f, 1.1f, -3.93f)); yield return Shot("470-window-display");
            LookAt(new Vector3(6.0f, 0, 1.0f), new Vector3(8.0f, 2.6f, -3.8f)); yield return Shot("471-hall-banners");
            yield return Measure("425-look-unit-b", new Vector3(4.8f, 0, 1.6f), new Vector3(10.5f, 1.3f, -2.4f), 6);
            yield return Measure("426-look-unit-c", new Vector3(11.0f, 0, 4.6f), new Vector3(4.0f, 1.4f, 8.4f), 6);

            // GR-11's pieces: the sign while the exhibition is prepared and on its day, a tag over a tank on display, the award once
            // it is held (the shop's flags and a display mark set directly for a few frames: labelled).
            shop.ExhibitionPrepared = true; yield return null; yield return null;
            var grand = w.transform.Find("Grand Exhibition");
            Check(w.SignShowing && !w.SignToday && HasPiece(grand.Find("Exhibition sign"), "ExhibitionSign") && grand.Find("Exhibition sign/Face").GetComponent<Renderer>().enabled,
                "The exhibition sign over the door, prepared: its board, letters and rosette round the pale gold face");
            LookAt(new Vector3(-0.2f, 0, -1.6f), new Vector3(-0.5f, 2.6f, -3.86f)); yield return Shot("466-exhibition-sign-prepared");
            shop.ExhibitionPrepared = false; shop.ExhibitionToday = true; yield return null; yield return null;
            Check(w.SignToday && grand.Find("Exhibition sign/Face").GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_EMISSION"), "On its day: the face glows gold and its lamp is lit");
            yield return Shot("467-exhibition-sign-today");
            var shown = shop.ContainerAt("b4").Occupants[0];
            shown.ReservedBy = GrowthShop.ExhibitionReservation; yield return null; yield return null;
            Check(w.DisplayTagsShown >= 1 && HasPiece(grand.Find("Display tag b4"), "DisplayTag"), "A tag over the Show tank on display: a coral ribbon, ON SHOW and a gold rosette");
            LookAt(new Vector3(9.6f, 0, -0.2f), new Vector3(11.1f, 2.1f, -0.2f)); yield return Shot("468-display-tag");
            shown.ReservedBy = 0; shop.ExhibitionToday = false; shop.ExhibitionDone = true; shop.ExhibitionResults = new GrowthExhibitionResults { Day = shop.Day };
            yield return null; yield return null;
            Check(w.AwardShowing && HasPiece(grand.Find("Grand Exhibition award"), "Award"), "Once it is held, the award on unit A's front wall: a wooden plaque, a gold medal and its card");
            LookAt(new Vector3(0.6f, 0, -1.8f), GrowthWorld.AwardPosition); yield return null; yield return null;
            Aimed(GrowthTargetKind.Award, null, "The reticle focuses the award");
            yield return Shot("469-award");
            shop.ExhibitionDone = false; shop.ExhibitionResults = null; yield return null; yield return null;

            // The rooms' partitions, closed, on two shops of their own; and unit C opened in the look: its hoarding sinks and its
            // lamps fade up to the night key.
            var closed = new GrowthShop { Era = 5, Reputation = GrowthContent.StarThresholds[4] + 50, Credits = 30000 };
            s.LoadFixture(closed);
            yield return null; yield return null; yield return null;
            foreach (var (root, piece, room) in new[] { ("Unit B partition", "PartitionB", "unit-b"), ("Lab annex partition", "PartitionAnnex", "annex") })
            {
                Check(w.PartitionShowing(room) && HasPiece(w.transform.Find(root), piece), "Closed, the " + room + " partition is a painted hoarding: COMING SOON");
                Replaced(w.transform.Find(root), "Partition", true, "The " + room + " partition");
            }
            LookAt(new Vector3(1.4f, 0, 0.4f), new Vector3(4.0f, 1.3f, 0f)); yield return Shot("472-partition-unit-b");
            LookAt(new Vector3(0.9f, 0, 6.6f), new Vector3(1.15f, 1.4f, 9f)); yield return Shot("473-partition-annex");
            var half = new GrowthShop { Era = 5, Reputation = GrowthContent.StarThresholds[4] + 50, Credits = 30000, UnitB = true, Annex = true };
            s.LoadFixture(half);
            yield return null; yield return null; yield return null;
            foreach (var (root, piece, room) in new[] { ("Unit C partition", "PartitionC", "unit-c"), ("Workshop partition", "PartitionWorkshop", "workshop") })
            {
                Check(w.PartitionShowing(room) && HasPiece(w.transform.Find(root), piece), "Closed, the " + room + " partition is a painted hoarding: COMING SOON");
                Replaced(w.transform.Find(root), "Partition", true, "The " + room + " partition");
            }
            LookAt(new Vector3(10.6f, 0, 1.4f), new Vector3(10.875f, 1.3f, 4f)); yield return Shot("474-partition-unit-c");
            LookAt(new Vector3(1.6f, 0, 10.4f), new Vector3(4.0f, 1.4f, 10.3f)); yield return Shot("475-partition-workshop");
            Ok(s.Act(half.BuyUnitC), "Open unit C (the card's button)");
            yield return new WaitForSeconds(0.9f);
            float lamps = w.LampIntensity("unit-c"), lampsFull = w.LampFull("unit-c");
            Check(w.Revealing("unit-c") && lamps > 0.05f && lamps < lampsFull - 0.05f && lampsFull > 2.6f,
                "Unit C opening in the look: its hoarding sinking, its two lamps fading up (" + lamps.ToString("0.00") + " of " + lampsFull.ToString("0.00") + ")");
            yield return Wait(() => !w.Revealing("unit-c"), 6, "unit C's reveal");
            yield return null;
            Check(Mathf.Abs(w.LampIntensity("unit-c") - w.LampFull("unit-c")) < 0.05f && !w.PartitionShowing("unit-c"), "Revealed: its lamps at the night key, its hoarding gone");

            // Wave 5: the sounds, on the look start again. The tide engine hums and the fast pumps whirr on the shop's own power;
            // the settings scale them; the hand crank's ratchet and a hand pump's strokes while held; an offspring's pop and a
            // school's swish.
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Look));
            shop = s.Shop;
            yield return null; yield return null; yield return null;
            var audio = s.Audio;
            Check(audio != null && audio.Clips.All(c => c != null && c.length > 0.03f) && audio.Voices.All(v => v != null && v.spatialBlend == 1f),
                "Six sounds made by the game itself (nothing recorded), played by five voices placed where each happens");
            s.SoundLevel = GrowthAudio.Level.Full; s.HumLevel = GrowthAudio.Level.Full;
            yield return Wait(() => audio.HumPlaying && audio.MotorPlaying && audio.HumVolume >= GrowthAudio.HumGain - 0.001f, 4, "the generator's hum and the pumps' whirr, settled");
            Check(Vector3.Distance(audio.HumPosition, w.GeneratorPlace) < 1.2f && Mathf.Abs(audio.HumPitch - 0.8f) < 0.01f, "The tide engine hums from the workshop's corner, deepest at level 4");
            Check(GrowthWorld.GrowerPositions.Values.Any(g => Vector3.Distance(audio.MotorPosition, g) < 1.2f), "The electric pumps whirr from the running pump nearest the player");
            float fullHum = audio.HumVolume;
            s.HumLevel = GrowthAudio.Level.Half;
            yield return Wait(() => Mathf.Abs(audio.HumVolume - fullHum * 0.5f) < 0.01f, 3, "the hum at half");
            Check(true, "Machine hum at half: the hum settles at half (" + audio.HumVolume.ToString("0.00") + " of " + fullHum.ToString("0.00") + ")");
            s.HumLevel = GrowthAudio.Level.Off;
            yield return Wait(() => !audio.HumPlaying && !audio.MotorPlaying, 3, "the hum off");
            Check(true, "Machine hum off: the hum and the whirr fade out and stop");
            s.HumLevel = GrowthAudio.Level.Full;
            yield return Wait(() => audio.HumPlaying, 3, "the hum back");
            s.SetCard(GrowthCard.Pause);
            yield return Wait(() => !audio.HumPlaying, 3, "the hum while paused");
            // GR-23: the volumes moved from the pause screen to the Settings screen's Sound tab, each 0 to 100.
            s.OpenSettings("Sound");
            yield return null; yield return null;
            Check(s.Ui.CurrentTab == "Sound" && s.Ui.CardText().Contains("Sounds") && s.Ui.CardText().Contains("Machine hum") && s.Ui.SettingValue("sounds") == "100" && s.Ui.SettingValue("hum") == "100",
                "Paused: the hum fades out; the Settings screen's Sound tab has Sounds and Machine hum, each 0 to 100");
            yield return Shot("480-pause-sounds");
            s.SetCard(GrowthCard.None);
            yield return Wait(() => audio.HumPlaying, 3, "the hum after the pause");
            // The hand crank and the hand pumps (set back to level 1 on the same shop: labelled): no hum; a ratchet and strokes while held.
            shop.Levels["generator"] = 1; shop.Levels["pump"] = 1;
            // The hand crank stands in the annex again once the world has rebuilt it, and the ray sees it after the next physics
            // step (transforms are not synced on every change: GR-17's run 1 aimed between the two and met the wall behind).
            yield return Wait(() => !audio.HumPlaying && !audio.MotorPlaying && w.GeneratorLookLevel == 1, 3, "no hum from the hand crank and hand pumps, the crank rebuilt");
            yield return new WaitForFixedUpdate();
            int clicks = audio.Clicks;
            var crankAt = w.GeneratorPlace;
            yield return HoldAt(crankAt + new Vector3(0, 0, -1.6f), crankAt + new Vector3(0, 0.9f, 0), GrowthTargetKind.Generator, null, () => audio.Clicks >= clicks + 6, "the crank's ratchet", "The reticle focuses the hand crank");
            Check(audio.Clicks >= clicks + 6, "Cranking: the ratchet clicks in time while it turns (" + (audio.Clicks - clicks) + " clicks)");
            shop.BufferAt(0).Units = 0;
            int strokes = audio.Strokes;
            yield return HoldAt(GrowthWorld.GrowerFront("bloom") + new Vector3(0.25f, 0, -0.26f), PumpPoint("bloom"), GrowthTargetKind.Pump, "bloom", () => audio.Strokes >= strokes + 3, "the hand pump's strokes", "The reticle focuses the Bloom grower's hand pump");
            Check(audio.Strokes >= strokes + 3, "Pumping by hand: a stroke at each turn (" + (audio.Strokes - strokes) + "), from the pump");
            shop.Levels["generator"] = 4; shop.Levels["pump"] = 3;
            yield return null; yield return null;
            // The moments.
            int pops = audio.Pops;
            shop.Pairs[0].Offspring = new GrowthSpecimen { Id = 990001, Species = shop.Discovered[0] };
            yield return null; yield return null;
            Check(audio.Pops == pops + 1 && Vector3.Distance(audio.LastPop, GrowthWorld.BreedingPosition) < 1.2f && audio.LastMomentGain > 0.6f, "An offspring arrives in the breeding tank: a pop from the tank");
            shop.Pairs[0].Offspring = null; yield return null;
            var e1 = shop.ContainerAt("e1");
            e1.Occupants.Clear(); yield return null; yield return null;
            int swishes = audio.Swishes;
            for (int i = 0; i < 3; i++) Ok(shop.DevelopmentStock(e1.Id, "pondlet"), "A Pondlet into the Large tank on e1 (the routes' fixture action)");
            yield return null; yield return null;
            Check(audio.Swishes == swishes + 1 && Vector3.Distance(audio.LastSwish, w.Tanks[e1.Id].Root.position) < 1.2f && audio.LastMomentGain > 0.5f,
                "Three Pondlets make a school: a swish from their tank");
            s.SoundLevel = GrowthAudio.Level.Off;
            e1.Occupants.Clear(); yield return null; yield return null;
            swishes = audio.Swishes;
            for (int i = 0; i < 3; i++) shop.DevelopmentStock(e1.Id, "pondlet");
            yield return null; yield return null;
            Check(audio.Swishes == swishes + 1 && audio.LastMomentGain == 0, "Sounds off: the swish is asked for and played at nothing");
            s.SoundLevel = GrowthAudio.Level.Full;

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
