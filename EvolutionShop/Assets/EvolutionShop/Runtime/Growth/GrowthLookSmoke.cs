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
    /// GR-16's Windows route (-evolutionGrowthLookSmoke): wave 1 of the new game's look (GR.48). The back room and shop unit A
    /// dressed with the growth kit; the growers, the incubator, Starter tanks and the counter in the kit's shapes at every
    /// level; the greybox's shapes hidden but still solid and still focused; every kit piece in the look's own materials; the
    /// reflection probes rendered; the light following the shop from night through morning, midday and closing; the dressed
    /// rooms inside the frame budget; and captures of each by night and by day.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string LookScenarioText = "GR-16 wave 1, the new game's look: the back room and shop unit A dressed with the growth kit; growers, the incubator, Starter tanks and the counter in the kit's shapes at every level; the greybox's shapes hidden but still solid and focused; the look's own materials; reflections; the light from night through morning, midday and closing; the dressed rooms inside the frame budget; captures by night and by day";

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
            limitations.Add("GR-16 look route: the fixture is labelled: an era 4 shop with 20,000 credits, Ember owned, a Starter tank on a1 with three creatures, every station at level 1; then the same shop with the growers, the incubator and the counter at level 4 and Starter tanks at levels 2 and 3 on a1 and a3. The day is opened by the shop's own action and its clock set to the morning, midday and closing marks (labelled); frame times are measured uncapped (vSync off) for 6 s at each view after 1 s settling. Wave 1 dresses the back room and unit A; the other rooms take the look's palette only (waves 2 to 4).");
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
            yield return Measure("420-look-unit-a", new Vector3(0.8f, 0, 3.2f), new Vector3(-1.5f, 1.2f, -2.0f), 6);
            yield return Measure("421-look-back-room", new Vector3(1.2f, 0, 4.6f), new Vector3(-2.8f, 1.1f, 7.6f), 6);
            shop.DaySecondsLeft = GrowthContent.DaySeconds * 0.1f;
            yield return new WaitForSeconds(2.5f);
            Check(w.LookKeyName == "closing", "Near the day's end, the closing key: the low amber sun");
            LookAt(new Vector3(1.0f, 0, 3.0f), new Vector3(-2.2f, 1.0f, -3.0f)); yield return Shot("414-unit-a-closing");
        }
    }
}
#endif
