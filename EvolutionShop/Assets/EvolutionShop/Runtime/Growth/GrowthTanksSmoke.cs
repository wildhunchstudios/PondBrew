#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-03's two Windows routes, beside GR-02's in the same component:
    /// -evolutionGrowthTanksSmoke: places that fit containers, a Large tank bought on a long place, the development crate's
    /// medium and large creatures, size classes in a Starter and a Large tank, peek on them, container levels, moving and
    /// selling, an era 4 Show tank with brine water and harmony, a day of visitors who browse one container at a time and a
    /// pair reserved and sold together, save and reload.
    /// -evolutionGrowthDensitySmoke: GrowthShop.Densest (every place full, 120 small creatures) and a 180-creature stress,
    /// with frame times at five views, uncapped, written to density-timings.json.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string TanksScenarioText = "GR-03 tanks: places by footprint, a Large tank on a long place, medium and large creatures, size classes, peek, container levels, moving and selling, a Show tank with water and harmony, visitors browsing one container at a time, a pair reserved and sold, save and reload";
        const string DensityScenarioText = "GR-03 density: the densest greybox layout (120 small creatures) and a 180-creature stress, frame times at five views and during a day of visitors";

        GrowthSpecimen Stock(GrowthShop shop, GrowthContainer c, string species)
        {
            Ok(shop.DevelopmentStock(c.Id, species), "Fixture: " + species + " into the " + c.Def.Name);
            return c.Occupants.Last();
        }

        IEnumerator TanksScenario()
        {
            limitations.Add("GR-03 tanks route: fixtures are labelled: an era 2 shop with two stars, 900 credits, unit B open and three creatures (GrowthSession.LoadFixture), then an era 4, four-star save edit with 6,000 credits; medium and large creatures come from the development crate (F8) or straight into a tank, since cultures and breeding are GR-04 and GR-05; part of the day runs on the shop's own clock (accelerated). Placement and arranging go through the shop's actions, as the card buttons do; E is pressed at the tanks and places through the session's real input. Placeholder art.");
            yield return Opening();
            TakeInput();

            // An era 2 shop (labelled fixture): two stars, 900 credits, unit B open, the three Brookkin founders known,
            // three Pondlets in the first Starter tank.
            var fx = new GrowthShop { Era = 2, Reputation = GrowthContent.StarThresholds[1] + 20, Credits = 900, UnitB = true };
            fx.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            var starter = fx.Containers[0];
            for (int i = 0; i < 3; i++) Stock(fx, starter, "pondlet");
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null;
            Check(shop.Era == 2 && shop.Stars == 2 && shop.UnitB && starter.Place == "a1", "Fixture: an era 2 shop with two stars, unit B open, a Starter tank on a1");
            var unitsAB = GrowthShop.Places.Where(p => p.Unit == "unit-a" || p.Unit == "unit-b").ToList();
            Check(unitsAB.Count(p => p.Size == "long") == 5 && unitsAB.Count(p => p.Size == "standard") == 2 && unitsAB.Count(p => p.Size == "feature") == 1,
                "Places: five long, two standard and one feature place across units A and B");
            LookAt(new Vector3(1.6f, 0, -3.4f), new Vector3(-1.4f, 1.0f, 1.2f)); yield return Shot("30-unit-a-places");

            // A standard place refuses a Large tank and says why; a long place takes it.
            var a2 = GrowthWorld.Places["a2"];
            LookAt(new Vector3(-1.9f, 0, 0.9f), a2.Position + new Vector3(0, 0.4f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Place, "a2", "The reticle focuses the standard place a2");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Place, "E opens the place card");
            Check(s.Ui.CardText().Contains("Large tank is 2.5 × 1 m; this place takes 1.5 × 1 m"), "The standard place's card says a Large tank is too big for it");
            yield return Shot("31-standard-place-card");
            Check(shop.BuyContainer("large", "a2").Failure == GrowthFailure.TooBigForPlace, "Buying a Large tank for a2 is refused: too big for the place");
            s.SetCard(GrowthCard.None);
            var a3 = GrowthWorld.Places["a3"];
            LookAt(new Vector3(-0.4f, 0, -1.3f), a3.Position + new Vector3(0, 0.4f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Place, "a3", "The reticle focuses the long place a3");
            yield return Press(Key.E);
            Check(s.Ui.CardText().Contains("Buy a Large tank for 200 cr"), "The long place offers a Large tank for 200 credits");
            int credits = shop.Credits;
            Ok(s.Act(() => shop.BuyContainer("large", "a3")), "Buy a Large tank for a3");
            s.SetCard(GrowthCard.None);
            yield return null;
            var large = shop.ContainerAt("a3");
            Check(large != null && large.Spaces == 8 && shop.Credits == credits - 200, "A Large tank of 8 spaces stands on a3 for 200 credits");
            Check(s.World.Tanks.TryGetValue(large.Id, out var largeView) && Mathf.Approximately(largeView.Width, 2.5f), "The world builds it 2.5 m wide");
            LookAt(new Vector3(1.4f, 0, -1.9f), a3.Position + new Vector3(0, 1.0f, 0)); yield return Shot("32-large-tank");

            // The development crate (labelled): a large and a medium creature in hand.
            yield return Press(Key.F8);
            Check(s.Card == GrowthCard.Crate, "F8 opens the development crate");
            yield return Shot("33-crate");
            Ok(s.Act(() => shop.DevelopmentGive("shellback")), "Crate: a Shellback (large) in hand");
            s.SetCard(GrowthCard.None);
            // The Starter tank refuses a large creature: E on it says so.
            LookAt(new Vector3(-2.2f, 0, 2.0f), GrowthWorld.Places["a1"].Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, starter.Id, "The reticle focuses the Starter tank");
            yield return Press(Key.E);
            Check(shop.Carried.Count == 1 && starter.Occupants.Count == 3, "E on the Starter tank keeps the Shellback in hand");
            Check(s.Ui.Log.Any(l => l.Contains("Too big for a Starter tank")), "It says the Shellback is too big for a Starter tank");
            Ok(s.Act(() => shop.DevelopmentGive("emberpuff")), "Crate: an Emberpuff (medium) in hand");
            Ok(s.Act(() => shop.DevelopmentGive("lanternfin")), "Crate: a Lanternfin (small) in hand");
            // The Large tank takes all three sizes: E three times.
            LookAt(new Vector3(-0.4f, 0, -1.2f), a3.Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, large.Id, "The reticle focuses the Large tank");
            for (int i = 0; i < 3; i++) yield return Press(Key.E);
            Check(shop.Carried.Count == 0 && large.Used == 7 && large.Occupants.Select(o => o.Def.Size).Distinct().Count() == 3,
                "E puts a large (4 spaces), a medium (2) and a small (1) creature into the Large tank: 7 of 8 spaces");
            Check(s.World.Tanks[large.Id].Creatures.Values.Select(v => v.Length).Distinct().Count() == 3, "The three swim at their size classes (7.5, 15 and 32 cm)");
            Stock(shop, large, "pondlet");
            Check(large.Free == 0 && shop.PutIn(large.Id).Failure == GrowthFailure.HandsEmpty, "A fourth, small, fills the Large tank");
            yield return new WaitForSeconds(0.5f);
            LookAt(new Vector3(0.9f, 0, -1.5f), a3.Position + new Vector3(0, 1.15f, 0)); yield return Shot("34-sizes-side-by-side");
            // Peek reads a medium or large creature with its size: the view follows the Shellback as a player would.
            var peekFeet = new Vector3(-0.4f, 0, -0.75f);
            LookAt(peekFeet, a3.Position + new Vector3(0, 1.15f, 0));
            yield return Wait(() => s.Peeking && s.Player.View.fieldOfView < GrowthSession.PeekFov + 1, 3, "peek at the Large tank");
            var big = s.World.Tanks[large.Id].Creatures.Values.First(v => v.Species == "shellback");
            float hunt = Time.realtimeSinceStartup + 8;
            string peekName = null, peekStatus = null;
            while (Time.realtimeSinceStartup < hunt)
            {
                LookAt(peekFeet, big.transform.position);
                yield return null;
                peekName = s.FocusName(out peekStatus);
                if (s.PeekCreature == big) break;
            }
            Check(peekStatus != null && (peekStatus.StartsWith("large") || peekStatus.StartsWith("medium")), "Peek names a bigger creature with its size: " + peekName + " (" + peekStatus + ")");
            yield return Shot("35-peek-bigger");

            // The tank card: the space grid, the species and the next level.
            s.SetCard(GrowthCard.Tank, large.Id); yield return new WaitForSeconds(0.2f);
            string text = s.Ui.CardText();
            Check(text.Contains("Large tank · level 1") && text.Contains("8/8 spaces") && text.Contains("Needs era 3"), "The Large tank's card: level 1, 8 of 8 spaces, level 2 needs era 3");
            yield return Shot("36-large-tank-card");
            s.SetCard(GrowthCard.None);
            // The Starter tank's levels (era 2): a filter (6 spaces), then a lid (8).
            Ok(s.Act(() => shop.UpgradeContainer(starter.Id)), "Fit the Starter tank's filter (level 2)");
            Ok(s.Act(() => shop.UpgradeContainer(starter.Id)), "Fit the Starter tank's lid (level 3)");
            yield return null;
            Check(starter.Level == 3 && starter.Spaces == 8 && starter.Occupants.Count == 3, "The Starter tank at level 3 holds 8 spaces and kept its three Pondlets");
            Check(s.World.Tanks[starter.Id].Signature.Contains("|3|"), "The world rebuilds it with its filter and lid");
            LookAt(new Vector3(-1.4f, 0, 1.7f), GrowthWorld.Places["a1"].Position + new Vector3(0, 1.3f, 0)); yield return Shot("37-starter-level-3");
            // Moving it with its creatures, and selling an empty one for half.
            Ok(s.Act(() => shop.MoveContainer(starter.Id, "a2")), "Move the Starter tank, with its creatures, to a2");
            yield return null;
            Check(s.World.Tanks[starter.Id].Place == "a2" && starter.Occupants.Count == 3 && shop.ContainerAt("a1") == null, "It stands on a2 with its three Pondlets; a1 is free");
            LookAt(new Vector3(-1.0f, 0, 1.6f), new Vector3(-3.0f, 1.0f, 1.5f)); yield return Shot("38-moved");
            credits = shop.Credits;
            Ok(s.Act(() => shop.BuyContainer("starter", "b1")), "Buy a Starter tank for b1");
            string spare = shop.ContainerAt("b1").Id;
            Ok(s.Act(() => shop.SellContainer(spare)), "Sell it back, empty");
            Check(shop.Credits == credits - 10 && shop.Container(spare) == null, "An empty tank sells for half: 20 paid, 10 back");
            Check(shop.SellContainer(starter.Id).Failure == GrowthFailure.NotEmpty, "A tank with creatures in it is not sold");

            // Era 4 (labelled save edit): a Show tank on the feature place, brine water, harmony.
            Check(s.SaveNow(), "Save before the era 4 fixture");
            var save = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            save.era = 4; save.reputation = GrowthContent.StarThresholds[3] + 50; save.credits = 6000;
            File.WriteAllText(s.SavePath, JsonUtility.ToJson(save));
            Check(s.ReloadSaved() && s.Shop.Era == 4 && s.Shop.Stars == 4, "Load the labelled era 4, four-star fixture");
            shop = s.Shop;
            large = shop.ContainerAt("a3"); starter = shop.ContainerAt("a2");
            Ok(s.Act(() => shop.BuyContainer("show", "b4")), "Buy a Show tank for the feature place b4");
            var show = shop.ContainerAt("b4");
            Stock(shop, show, "shellback"); Stock(shop, show, "shellback"); Stock(shop, show, "clamjaw");
            Check(!shop.Harmony(show) && shop.Appeal(show) == 1, "Three calm brine-water creatures in fresh water: lit (+1 appeal) but no harmony");
            Ok(s.Act(() => shop.SetWater(show.Id, Water.Brine)), "A water kit: brine water in the Show tank");
            yield return null;
            Check(shop.Harmony(show) && shop.Appeal(show) == 2, "In brine water they are in harmony: appeal +2 (lit, harmony)");
            var showView = s.World.Tanks[show.Id];
            Check(showView.HarmonyFrame.activeSelf && showView.ShowLight != null, "The Show tank glows in harmony and is lit");
            var b4 = GrowthWorld.Places["b4"];
            LookAt(new Vector3(8.4f, 0, -1.4f), b4.Position + new Vector3(0, 1.1f, 0)); yield return Shot("39-show-tank-harmony");
            s.SetCard(GrowthCard.Tank, show.Id); yield return new WaitForSeconds(0.2f);
            Check(s.Ui.CardText().Contains("in harmony: +1 appeal") && s.Ui.CardText().Contains("Appeal +2"), "The Show tank's card shows its water, harmony and appeal");
            yield return Shot("40-show-tank-card");
            s.SetCard(GrowthCard.None);
            Stock(shop, starter, "pondlet");

            // A day: visitors look into the Show tank first and walk on when nothing there is within budget.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            GrowthVisitor walker = null;
            yield return Wait(() => (walker = shop.Visitors.FirstOrDefault(v => v.Browsed >= 1 && v.State == GrowthVisitorState.Browsing)) != null, 45, "a visitor to walk on to a second container");
            Check(walker.Plan.Count >= 2 && walker.Plan[0] == show.Id, "Visitors look into the most appealing container first (the Show tank), then the next");
            yield return null;
            Check(s.World.VisitorViews.TryGetValue(walker.Id, out var walkerView) && !walkerView.Arrived, "The visitor walks on to the next container");
            LookAt(new Vector3(6.6f, 0, -3.3f), walkerView.transform.position + Vector3.up); yield return Shot("41-walking-on");
            // Collectors at four stars take a pair of one species (on the shop's own clock, labelled accelerated; the counter
            // serves whoever is first in line meanwhile, as a player would).
            GrowthVisitor pair = null;
            float dayAtStart = shop.DaySecondsLeft;
            for (int i = 0; i < 1600 && pair == null && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.25f);
                pair = shop.Queue.FirstOrDefault(v => v.SpecimenIds.Count == 2);
                if (pair == null && shop.FrontOfQueue != null) shop.Serve(0.25f);
                if (i % 20 == 0) yield return null;
            }
            Check(pair != null && pair.Type == "collector", "A Collector reserved a pair of one species, all or none (after " + (dayAtStart - shop.DaySecondsLeft).ToString("0") + " s of the day)");
            var holder = shop.HolderOf(pair.SpecimenId);
            Check(pair.SpecimenIds.All(id => holder.Occupants.First(o => o.Id == id).ReservedBy == pair.Id), "Both specimens carry the Collector's reservation");
            yield return null;
            var holderPlace = GrowthWorld.Places[holder.Place];
            LookAt(holderPlace.Browse + (holderPlace.Position - holderPlace.Browse).normalized * -0.6f, holderPlace.Position + new Vector3(0, 1.15f, 0)); yield return Shot("42-pair-reserved");
            // The held mouse at the counter sells everyone in front, then the pair together.
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            int sales = shop.SalesTotal;
            Hold(true);
            yield return Wait(() => pair.Outcome == GrowthVisitOutcome.Bought || pair.State == GrowthVisitorState.Leaving, 30, "the pair to be bagged");
            Hold(false);
            yield return null; yield return null; // the sale's note reaches the log on the next frame
            Check(pair.Outcome == GrowthVisitOutcome.Bought && shop.SalesTotal >= sales + 2 && s.Ui.Log.Any(l => l.StartsWith("Sold a pair of")),
                "The pair is sold together: two sales, one bag (outcome " + pair.Outcome + ", " + (shop.SalesTotal - sales) + " sales, " + shop.DaySecondsLeft.ToString("0") + " s of the day left)");
            yield return Shot("43-pair-sold");
            for (int i = 0; i < 2000 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(0.5f);
                if (i % 40 == 0) yield return null;
            }
            yield return null;
            Check(shop.Phase == GrowthPhase.Night && shop.LastDay != null && shop.Stock.All(x => x.ReservedBy == 0), "The day ends; every reservation is released");
            s.SetCard(GrowthCard.None);

            // Save and reload: version 2 keeps kinds, levels, water and places.
            Check(s.SaveNow(), "Save");
            string before = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            Check(JsonUtility.ToJson(s.Shop.Snapshot()) == before, "The reloaded shop is the saved one");
            var reloaded = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            Check(reloaded.version >= 2 && reloaded.containers.Any(b => b.kind == "show" && b.water == "Brine") && reloaded.containers.Any(b => b.kind == "starter" && b.level == 3),
                "The save (version 2 and later) keeps the Show tank's brine water and the Starter tank's level 3");
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }

        [Serializable] sealed class ViewTiming
        {
            public string view;
            public int creatures, frames, full, cheaper, frozen, renderers;
            public float averageMs, p95Ms, worstMs, fps;
        }

        [Serializable] sealed class DensityReport
        {
            public string note;
            public int shopCreatures, stressCreatures;
            public ViewTiming[] views;
        }

        readonly List<ViewTiming> timings = new List<ViewTiming>();

        IEnumerator Measure(string name, Vector3 feet, Vector3 target, float seconds = 8)
        {
            LookAt(feet, target);
            yield return new WaitForSeconds(1);
            var dts = new List<float>();
            long full = 0, cheaper = 0, frozen = 0;
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                dts.Add(Time.unscaledDeltaTime);
                full += s.World.DetailCounts[0]; cheaper += s.World.DetailCounts[1]; frozen += s.World.DetailCounts[2];
            }
            dts.Sort();
            int n = Mathf.Max(1, dts.Count);
            var t = new ViewTiming
            {
                view = name, frames = dts.Count, creatures = s.World.Tanks.Values.Sum(v => v.Creatures.Count),
                full = (int)(full / n), cheaper = (int)(cheaper / n), frozen = (int)(frozen / n),
                renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.isVisible),
                averageMs = dts.Count == 0 ? 0 : dts.Average() * 1000f, p95Ms = dts.Count == 0 ? 0 : dts[Mathf.Min(dts.Count - 1, (int)(dts.Count * 0.95f))] * 1000f,
                worstMs = dts.Count == 0 ? 0 : dts[dts.Count - 1] * 1000f,
            };
            t.fps = t.averageMs > 0 ? 1000f / t.averageMs : 0;
            timings.Add(t);
            Check(t.frames > 30, name + ": " + t.creatures + " creatures, " + t.averageMs.ToString("0.00") + " ms average, " + t.p95Ms.ToString("0.00") + " ms at the 95th percentile, worst " + t.worstMs.ToString("0.0") +
                " ms (" + t.full + " full, " + t.cheaper + " cheaper, " + t.frozen + " frozen)");
            Check(t.averageMs < 1000f / 60f && t.p95Ms < 25f, name + " stays inside the budget: under 16.7 ms on average and 25 ms at the 95th percentile");
            yield return Shot(name);
        }

        IEnumerator DensityScenario()
        {
            limitations.Add("GR-03 density route: GrowthShop.Densest (every place holding the largest container it takes at its highest level, filled with small creatures: 120) is loaded as a labelled fixture; the 180-creature stress adds 60 view-only creatures to the tanks (labelled; they are not in the shop). Frame times are measured uncapped (vSync off, no frame-rate cap) for 8 s at each view after 1 s settling, standing still; the last view runs during a day of visitors. Creatures without shadows: GR-12's waves 1 to 3 (the Brookkin, Sparkline, Airfin, Cinderling, Dewling, Reefback and Saltscale) as kit models, the full kit near and in view and one shared baked mesh of the species and colour beyond (GR.5), the other families as placeholders (about 10 primitives each); this machine's GPU and CPU are in the report.");
            yield return Opening();
            TakeInput();
            int previousRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;


            s.LoadFixture(GrowthShop.Densest());
            yield return null; yield return null;
            int count = s.World.Tanks.Values.Sum(v => v.Creatures.Count);
            Check(s.Shop.Stock.Count() == 120 && count == 120, "The densest layout: 8 containers at their highest levels, 120 creatures in the world");
            Check(s.World.Tanks.Count == 8 && s.World.Tanks.Values.Count(v => v.Width >= 3f) == 1, "Every place holds a container; one Show tank");
            yield return Measure("50-dense-unit-a", new Vector3(0.6f, 0, -3.2f), new Vector3(-1.5f, 1.1f, 1.5f));
            yield return Measure("51-dense-unit-b", new Vector3(5.0f, 0, -3.2f), new Vector3(10.5f, 1.1f, 1.8f));
            yield return Measure("52-dense-across", new Vector3(-3.0f, 0, -3.3f), new Vector3(11.0f, 1.1f, 0.8f));
            yield return Measure("53-dense-show-close", new Vector3(9.6f, 0, 0.2f), new Vector3(11.15f, 1.15f, 0.2f));
            yield return Measure("54-dense-back-room", GrowthWorld.Spawn, GrowthWorld.Spawn + new Vector3(-3f, 1.2f, 0));
            // The stress: 60 view-only creatures more, spread over the tanks (labelled), 180 in all.
            var tanks = s.World.Tanks.Values.ToList();
            var small = GrowthContent.Species.Where(x => x.Size == SizeClass.Small).Select(x => x.Id).ToList();
            for (int i = 0; i < 60; i++)
                tanks[i % tanks.Count].AddStressCreature(new GrowthSpecimen { Id = -(i + 1), Species = small[i % small.Count] });
            yield return null;
            count = s.World.Tanks.Values.Sum(v => v.Creatures.Count);
            Check(count == 180, "The stress: 180 creatures on screen in the two shop units (60 of them view-only)");
            yield return Measure("55-stress-unit-a", new Vector3(0.6f, 0, -3.2f), new Vector3(-1.5f, 1.1f, 1.5f));
            yield return Measure("56-stress-across", new Vector3(-3.0f, 0, -3.3f), new Vector3(11.0f, 1.1f, 0.8f));
            Ok(s.Act(s.Shop.OpenShop), "Open the shop for a day of visitors in the dense layout");
            yield return new WaitForSeconds(8);
            Check(s.Shop.Visitors.Count > 0, "Visitors are in the shop");
            yield return Measure("57-stress-day", new Vector3(2.0f, 0, -3.5f), new Vector3(4.0f, 1.0f, 1.0f), 10);
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVSync;
            var report = new DensityReport
            {
                note = "Uncapped frame times (" + SystemInfo.graphicsDeviceName + ", " + SystemInfo.processorType + ", " + Screen.width + "x" + Screen.height + "). full/cheaper/frozen: creatures at each GR.5 update level, averaged over the frames.",
                shopCreatures = s.Shop.Stock.Count(), stressCreatures = 60, views = timings.ToArray(),
            };
            File.WriteAllText(Path.Combine(evidence, "density-timings.json"), JsonUtility.ToJson(report, true));
            Check(File.Exists(Path.Combine(evidence, "density-timings.json")), "Timings written to density-timings.json");
        }
    }
}
#endif
