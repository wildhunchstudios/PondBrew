#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-24's and GR-25's Windows route (-evolutionGrowthEffectsSmoke): the models and the visual effects in the game. The look
    /// start at night: the growers' bubbles; collecting, the culture drawn toward the siphon; warm motes under the lamps; the incubator's steam while it
    /// incubates, a hatch and a discovery; a creature put into a tank (a splash and a ring), the tanks' bubbles and motes, a
    /// school formed; the breeding tank's hearts and an offspring's sparkles; the crank's sparks; the tide engine's bubbles,
    /// mist and glints, and a running pump's glint; then the day: a visitor's
    /// puff at the door, a sale's coins, happy and unhappy visitors, dust in the sunlight; a star and a room opened; Reduced
    /// motion and the Low preset halving them; nothing new while paused; and the frame budget with the effects playing.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string EffectsScenarioText = "GR-24 models and GR-25 visual effects: the growers' bubbles, collecting, motes under the lamps at night, the incubator's steam, a hatch and a discovery, a creature put into a tank, the tanks' bubbles and motes, a school, the breeding tank's hearts, the crank's sparks, the tide engine and a running pump, a visitor at the door, a sale's coins, happy and unhappy visitors, dust in the sunlight, a star and a room opened, Reduced motion and Low, paused, and the frame budget";

        IEnumerator EffectsScenario()
        {
            limitations.Add("GR-24 and GR-25 effects route: the fixture is the development start point for the look (GrowthStarts.LookStart, labelled), at night. The effects are off in validation runs; the route turns them on (labelled). Incubation, a creature put into a tank (the routes' fixture action), a pair set breeding and an offspring, the shop's opening, visitors, sales (the shop's Serve, as the held mouse calls it), a star and a room opened are the shop's own actions and events, some staged by the route (labelled); the crank is held with the route's mouse; the buffers are emptied so every pump runs (fixture). How the effects look is for the user to judge; the route checks that each is emitted where it belongs, and the frame budget.");
            yield return Opening();
            TakeInput();
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Look));
            var shop = s.Shop; var w = s.World; var fx = s.Effects;
            yield return null; yield return null;
            Check(fx != null && fx.Emitted(GrowthFx.Bubble) == 0, "In a validation run the effects are off until a route turns them on (every other route shows none)");
            fx.OnInValidation = true;

            // ---- the growers
            var bloom = GrowthWorld.GrowerPositions["bloom"];
            LookAt(GrowthWorld.GrowerFront("bloom") + new Vector3(0.6f, 0, 0.6f), bloom + new Vector3(0, 1.1f, 0));
            yield return new WaitForSeconds(1.5f);
            Check(fx.Emitted(GrowthFx.Bubble) > 5 && w.GrowerLiquid("bloom", out var bottom, out float height, out float radius) && height > 0.05f,
                "The growers' culture bubbles as it stands in the glass (" + fx.Emitted(GrowthFx.Bubble) + " bubbles)");
            yield return Shot("800-growers-bubbling");
            int glows = fx.Emitted(GrowthFx.Glow);
            yield return HoldAt(GrowthWorld.GrowerFront("bloom"), bloom + new Vector3(0, 1.1f, 0), GrowthTargetKind.Grower, "bloom", () => fx.Emitted(GrowthFx.Glow) > glows + 10, "the culture drawn up", "The reticle focuses the Bloom grower");
            Check(fx.Emitted(GrowthFx.Glow) > glows + 10, "Collecting: the culture streams up toward the siphon");

            // ---- night: warm motes under the lamps
            var front = GrowthWorld.GrowerFront("bloom");
            var lampAt = w.LitLamps().OrderBy(l => (l - front).sqrMagnitude).FirstOrDefault();
            LookAt(front, lampAt + Vector3.down * 1.3f);
            bool underLamp = false;
            for (float t = 0; t < 4 && !underLamp; t += Time.deltaTime)
            {
                yield return null;
                var dust = fx.Last.TryGetValue(GrowthFx.Dust, out var d) ? d : Vector3.zero;
                underLamp = fx.Emitted(GrowthFx.Dust) > 0 && w.LitLamps().Any(l => Vector3.Distance(l, dust) < 1.4f);
            }
            Check(shop.Phase == GrowthPhase.Night && underLamp, "At night, warm motes drift in the light under the lamps (" + fx.Emitted(GrowthFx.Dust) + " motes)");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("814-night-lamp-motes");

            // ---- the incubator
            var inc = GrowthWorld.IncubatorPosition;
            LookAt(inc + new Vector3(0.9f, 0, -1.6f), inc + new Vector3(0, 1.1f, 0));
            foreach (var c in new[] { "bloom", "flow" }) { shop.GrowerUnits[c] = Mathf.Max(shop.GrowerUnits[c], 4); }
            shop.Held.Clear();
            Ok(shop.Collect("bloom", shop.HoldSeconds), "Collect a Bloom dose (fixture)"); Ok(shop.Collect("flow", shop.HoldSeconds), "Collect a Flow dose (fixture)");
            Ok(shop.AddDose("bloom"), "Dose Bloom"); Ok(shop.AddDose("flow"), "Dose Flow");
            Ok(s.Act(shop.StartIncubation), "Start incubating");
            int steam = fx.Emitted(GrowthFx.Steam);
            yield return new WaitForSeconds(1.5f);
            Check(fx.Emitted(GrowthFx.Steam) > steam + 2, "Incubating: steam rises from the incubator, with soft glints");
            yield return Shot("801-incubator-steam");
            int sparkles = fx.Emitted(GrowthFx.Sparkle);
            s.Advance(shop.IncubationLeft + 0.05f);
            yield return null;
            Check(fx.Emitted(GrowthFx.Sparkle) > sparkles + 5 && fx.Emitted(GrowthFx.Puff) > 0, "A hatch: a puff and sparkles at the incubator");
            yield return new WaitForSeconds(0.25f);
            yield return Shot("802-hatch");
            int stars = fx.Emitted(GrowthFx.Star);
            fx.Event(new GrowthEvent { Kind = GrowthEventKind.Discovery });
            yield return new WaitForSeconds(0.35f);
            Check(fx.Emitted(GrowthFx.Star) > stars + 5, "A discovery (its event, labelled): a burst of stars and sparkles over the incubator");
            yield return Shot("803-discovery");
            if (shop.Hatchling != null) shop.TakeHatchling();
            shop.Carried.Clear();

            // ---- the tanks
            var a1 = shop.Containers.FirstOrDefault(c => w.Tanks.ContainsKey(c.Id) && c.Place != null && c.Place.StartsWith("a"));
            Check(a1 != null, "A tank in unit A");
            var tank = w.Tanks[a1.Id];
            LookAt(tank.Root.position + new Vector3(0.3f, 0, -1.7f), tank.Root.position + new Vector3(0, tank.WaterY, 0));
            yield return new WaitForSeconds(1f);
            int drops = fx.Emitted(GrowthFx.Drop), ripples = fx.Emitted(GrowthFx.Ripple);
            Ok(shop.DevelopmentStock(a1.Id, shop.Discovered[0]), "A creature into the tank (the routes' fixture action)");
            yield return null; yield return null;
            Check(fx.Emitted(GrowthFx.Ripple) == ripples + 1 && fx.Emitted(GrowthFx.Drop) > drops, "A creature goes in: a ring on the water and a splash");
            yield return new WaitForSeconds(0.15f);
            yield return Shot("804-creature-in");
            yield return new WaitForSeconds(1.5f);
            Check(fx.Alive(GrowthFx.Bubble) > 3, "Bubbles rise from the tanks' filters");
            yield return Shot("805-tank-bubbles");
            a1.Occupants.Clear(); yield return null; yield return null;
            int schoolSparkles = fx.Emitted(GrowthFx.Sparkle);
            for (int i = 0; i < 3; i++) Ok(shop.DevelopmentStock(a1.Id, "pondlet"), "A Pondlet into the emptied tank (fixture)");
            yield return null; yield return null;
            Check(GrowthMoments.SchoolsIn(a1).Any() && fx.Emitted(GrowthFx.Sparkle) > schoolSparkles, "Three of a kind make a school: a sparkle through the water");

            // ---- the breeding tank
            int hearts = fx.Emitted(GrowthFx.Heart);
            fx.Event(new GrowthEvent { Kind = GrowthEventKind.Bred });
            LookAt(GrowthWorld.BreedingPosition + new Vector3(0.4f, 0, 1.6f), GrowthWorld.BreedingPosition + new Vector3(0, 1.0f, 0));
            yield return new WaitForSeconds(0.4f);
            Check(fx.Emitted(GrowthFx.Heart) > hearts, "A pair bred (its event, labelled): hearts over the breeding tank");
            yield return Shot("806-breeding-hearts");

            // ---- the crank
            shop.Levels["generator"] = 1;
            yield return Wait(() => w.GeneratorLookLevel == 1, 3, "the hand crank rebuilt");
            yield return new WaitForFixedUpdate();
            var crankAt = w.GeneratorPlace;
            int sparks = fx.Emitted(GrowthFx.Spark);
            yield return AimAt(crankAt + new Vector3(0, 0, -1.6f), crankAt + new Vector3(0, 0.9f, 0), GrowthTargetKind.Generator, null, "The reticle focuses the hand crank");
            Hold(true);
            yield return new WaitForSeconds(0.6f);
            Check(fx.Emitted(GrowthFx.Spark) > sparks + 3, "Cranking: sparks fly from the crank");
            yield return Shot("807-crank-sparks");
            Hold(false); yield return null;
            shop.Levels["generator"] = 4;

            // ---- the tide engine and the electric pumps while they run
            yield return Wait(() => w.GeneratorLookLevel == 4, 3, "the tide engine rebuilt");
            int mist = fx.Emitted(GrowthFx.Steam), glints = fx.Emitted(GrowthFx.Spark), rising = fx.Emitted(GrowthFx.Bubble);
            LookAt(w.GeneratorPlace + new Vector3(0.6f, 0, -2.4f), w.GeneratorPlace + new Vector3(0, 1.2f, 0));
            yield return new WaitForSeconds(1.5f);
            Check(shop.SupplyNow > 0 && w.GeneratorColumnFeet().Count() == 3 && fx.Emitted(GrowthFx.Bubble) > rising + 10, "The tide engine makes power: glowing bubbles rise up its three glass columns");
            Check(w.GeneratorDrum(out var drum) && fx.Emitted(GrowthFx.Steam) > mist + 2 && Vector3.Distance(fx.Last[GrowthFx.Steam], drum) < 0.8f, "and a fine mist comes off its drum as it turns");
            Check(fx.Emitted(GrowthFx.Spark) > glints, "with a soft blue glint at its lamp now and then");
            yield return Shot("815-tide-engine");
            var motors = w.PumpMotors().ToList();
            Check(shop.ElectricPumps && motors.Count > 0, "Electric pumps on the piped growers (" + motors.Count + ")");
            foreach (var b in shop.Buffers) b.Units = 0; // room in every buffer, so every pump runs (fixture, labelled)
            var bloomMotor = motors.FirstOrDefault(m => m.culture == "bloom").motor;
            LookAt(GrowthWorld.GrowerFront("bloom"), bloomMotor);
            bool atMotor = false;
            for (float t = 0; t < 4 && !atMotor; t += Time.deltaTime)
            {
                yield return null;
                atMotor = fx.Emitted(GrowthFx.Spark) > 0 && motors.Any(m => Vector3.Distance(m.motor, fx.Last[GrowthFx.Spark]) < 0.25f);
            }
            Check(atMotor, "A running pump: a small green glint at its motor now and then");
            yield return Shot("816-pump-glint");

            // ---- the day: visitors, sales, dust in the sunlight
            Ok(s.Act(shop.OpenShop), "Open the shop");
            int puffs = fx.Emitted(GrowthFx.Puff);
            for (int i = 0; i < 400 && shop.Visitors.Count == 0; i++) { s.Advance(0.1f); if (i % 5 == 0) yield return null; }
            yield return null; yield return null;
            Check(fx.Emitted(GrowthFx.Puff) > puffs, "A visitor comes in: a puff at the door");
            LookAt(P(-1.2f, 0, -0.6f), P(-2.6f, 1.2f, -3.6f));
            yield return new WaitForSeconds(2f);
            Check(fx.Emitted(GrowthFx.Dust) > 3 && GrowthWorld.Sunbeams.Any(b => Vector3.Distance(fx.Last[GrowthFx.Dust], b) < 4), "By day, dust drifts in the sunlight by the window and the door (" + fx.Emitted(GrowthFx.Dust) + " motes)");
            yield return Shot("808-sunlit-dust");
            for (int i = 0; i < 800 && shop.FrontOfQueue == null; i++) { s.Advance(0.1f); if (i % 5 == 0) yield return null; }
            int coins = fx.Emitted(GrowthFx.Coin);
            LookAt(GrowthWorld.CounterPosition + new Vector3(-0.2f, 0, 1.6f), GrowthWorld.CounterPosition + new Vector3(0, 1.2f, 0));
            yield return null;
            // A sale: served by the player, or by Pip when Pip has the till.
            for (int i = 0; i < 600 && fx.Emitted(GrowthFx.Coin) == coins; i++) { if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds); s.Advance(0.1f); if (i % 3 == 0) yield return null; }
            Check(fx.Emitted(GrowthFx.Coin) > coins, "A sale: coins spring up from the till");
            yield return new WaitForSeconds(0.2f);
            yield return Shot("809-sale-coins");
            int happy = fx.Emitted(GrowthFx.Sparkle), clouds = fx.Emitted(GrowthFx.Cloud);
            for (int i = 0; i < 1500 && fx.Emitted(GrowthFx.Cloud) == clouds; i++)
            {
                s.Advance(0.2f);
                foreach (var v in shop.Visitors.Where(v => v.State == GrowthVisitorState.Queued)) v.Patience = 0;
                if (i % 10 == 0) yield return null;
            }
            Check(fx.Emitted(GrowthFx.Sparkle) > happy || fx.Emitted(GrowthFx.Cloud) > clouds, "Visitors going: sparkles over a happy one");
            Check(fx.Emitted(GrowthFx.Cloud) > clouds, "and a grey cloud over one tired of waiting (patience run out, labelled)");

            // ---- big moments
            LookAt(P(-0.5f, 0, -1f), P(-0.5f, 1.5f, 1f));
            stars = fx.Emitted(GrowthFx.Star);
            fx.Event(new GrowthEvent { Kind = GrowthEventKind.StarUp });
            yield return new WaitForSeconds(0.3f);
            Check(fx.Emitted(GrowthFx.Star) > stars + 5, "A star earned (its event, labelled): a burst of stars ahead");
            yield return Shot("810-star-up");
            int confetti = fx.Emitted(GrowthFx.Confetti);
            fx.Event(new GrowthEvent { Kind = GrowthEventKind.Opened });
            yield return new WaitForSeconds(0.6f);
            Check(fx.Emitted(GrowthFx.Confetti) > confetti + 20, "A room opened (its event, labelled): a puff of dust and confetti");
            yield return Shot("811-room-opened");

            // ---- calmer: Reduced motion and Low; nothing new while paused
            int full = Burst(fx);
            s.ReducedMotion = true;
            int reduced = Burst(fx);
            s.Settings.ApplyPreset(0);
            int both = Burst(fx);
            s.ReducedMotion = false; s.Settings.ApplyPreset(2); s.ApplySettings();
            Check(reduced <= full / 2 + 1 && both <= full / 4 + 1, "Reduced motion halves the effects (" + full + " to " + reduced + "), and the Low preset halves them again (" + both + ")");
            s.SetCard(GrowthCard.Pause);
            yield return null;
            int before = fx.Emitted(GrowthFx.Bubble);
            yield return new WaitForSeconds(0.6f);
            Check(fx.Emitted(GrowthFx.Bubble) == before, "Paused: nothing new starts");
            s.SetCard(GrowthCard.None);

            // ---- the frame budget with the effects playing (uncapped)
            int previousRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            yield return Measure("812-effects-unit-a", P(1.25f, 0, -1.9f), P(0.6f, 1.6f, 1.9f), 5);
            yield return Measure("813-effects-back-room", GrowthWorld.GrowerFront("bloom") + new Vector3(0.8f, 0, 1.2f), bloom + new Vector3(0, 1.1f, 0), 5);
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVSync;
            Check(fx.AliveTotal < 1500, "Never more than a few hundred particles at once (" + fx.AliveTotal + " now)");
        }

        /// <summary>A sale's burst as the effects emit it now: how many particles it gives.</summary>
        static int Burst(GrowthEffects fx)
        {
            int before = fx.Emitted(GrowthFx.Coin);
            fx.Event(new GrowthEvent { Kind = GrowthEventKind.Sale });
            return fx.Emitted(GrowthFx.Coin) - before;
        }
    }
}
#endif
