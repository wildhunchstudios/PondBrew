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
    /// GR-10's Windows route (-evolutionGrowthSpacesSmoke): GR.11's rules checked on the layout the player builds; the view
    /// from the entrance; shop unit C opened at its partition in unit B's arch, the partition coming down while the room's
    /// lamp fades up, the way in clear at once; unit C's feature wall and places, tanks on them, and visitors walking through
    /// unit B's arch to browse there and buy; the workshop opened the same way; the exhibition hall finish bought at its
    /// plans, its banners unrolling and its spotlights coming up, and its two exhibition places; the set pieces; save and
    /// reload showing every room open without a reveal.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string SpacesScenarioText = "GR-10 spaces: GR.11's proportions and navigation checked on the layout, the view from the entrance, shop unit C opened with its partition coming down, its feature wall and places, visitors walking through unit B's arch to browse and buy there, the workshop opened the same way, the exhibition hall finish with its banners and spotlights and its two exhibition places, the set pieces, save and reload";

        IEnumerator SpacesScenario()
        {
            limitations.Add("GR-10 spaces route: the fixture is the development start point a look at the spaces uses (GrowthStarts.SpacesStart, labelled): the era 4 start (unit B and the power room open, the bubble turbine, electric pumps, growers piped to buffers, stocked tanks) at 5 stars in era 5 with 30,000 credits, the workshop, unit C and the hall still closed. Opening rooms, buying tanks and the hall call the shop's actions as the card buttons do (UI Toolkit buttons are not clicked); unit C's tanks are stocked with development stock, and the shop's other tanks are emptied (labelled) so the day's visitors walk to unit C; the day runs on the shop's own clock (accelerated) with the counter served as holding it does; the reveals run in real time. Placeholder art: plain shapes for unit C, the set pieces, the banners and the spotlights.");
            yield return Opening();
            TakeInput();

            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Spaces));
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Era == 5 && shop.Stars == 5 && shop.UnitB && shop.Annex && !shop.Workshop && !shop.UnitC && !shop.Hall && shop.Credits == 30000 &&
                s.World.UnitCWallStanding && s.World.WorkshopWallStanding && !s.World.Revealing("unit-c"), "Fixture: the spaces start point, with the workshop, unit C and the hall still closed and nothing revealing on load");
            var problems = GrowthSpaces.Problems();
            Check(problems.Count == 0, "GR.11's proportions and navigation hold for every place, walk, the queue and the main aisle" + (problems.Count == 0 ? "" : ": " + string.Join("; ", problems.Take(3))));
            Check(new[] { "Bay window bench", "Window display", "Pipe gallery", "Workshop dome", "Feature tank (set piece)", "Exhibition hall plans" }.All(n => GameObject.Find(n) != null),
                "The set pieces' placeholders stand: the bay window bench, the window display, the pipe gallery, the workshop's dome, unit C's feature tank and the hall's plans");
            LookAt(P(-0.5f, 0, -3.4f), P(1.6f, 1.2f, 1.5f)); yield return Shot("130-entrance");

            // Unit C: its partition in unit B's arch, opened at night.
            yield return AimAt(GrowthWorld.UnitCArch + new Vector3(-0.3f, 0, -2.4f), GrowthWorld.UnitCArch + new Vector3(0, 1.2f, 0), GrowthTargetKind.UnitCWall, "unit-c", "The reticle focuses unit C's partition in unit B's arch");
            Check(s.FocusName(out string status) == "Shop unit C" && status == "E see what it needs", "It is shop unit C");
            yield return Press(Key.E);
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.UnitC && card.Contains("Open unit C for 4000 cr") && card.Contains("six more places"), "E opens its card: unit C for 4,000 credits, six more places");
            yield return Shot("131-unit-c-card");
            Ok(s.Act(shop.BuyUnitC), "Open unit C (the card's button)");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            var probe = GrowthWorld.UnitCArch + new Vector3(0, 1.2f, -1.2f);
            bool clear = !Physics.Raycast(probe, Vector3.forward, 2.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Check(shop.UnitC && !s.World.UnitCWallStanding && s.World.Revealing("unit-c") && s.World.PartitionShowing("unit-c") && clear,
                "The partition comes down: it is still sinking, and the way into unit C is clear at once");
            yield return new WaitForSeconds(0.9f);
            // GR-16: in the look a room's lamps follow the light key, so their full intensity is the world's (1.25 in the greybox).
            float drop = s.World.PartitionDrop("unit-c"), lamp = s.World.LampIntensity("unit-c"), full = s.World.LampFull("unit-c");
            Check(drop > 0.3f && drop < 2.4f && lamp > 0.05f && lamp < full - 0.01f && s.Ui.Log.Any(l => l == "Shop unit C opened"),
                "Halfway: the partition has sunk " + drop.ToString("0.00") + " m and the lamps are at " + lamp.ToString("0.00") + " of " + full.ToString("0.00") + "; the banner names the room");
            LookAt(GrowthWorld.UnitCArch + new Vector3(-0.4f, 0, -3.0f), GrowthWorld.UnitCArch + new Vector3(-1.0f, 1.2f, 2.0f)); yield return Shot("132-unit-c-revealing");
            yield return Wait(() => !s.World.Revealing("unit-c"), 6, "unit C's reveal");
            Check(!s.World.PartitionShowing("unit-c") && Mathf.Abs(s.World.LampIntensity("unit-c") - s.World.LampFull("unit-c")) < 0.01f && s.World.LampFull("unit-c") >= 1.25f && shop.FreePlaces.Count(p => p.StartsWith("c")) == 6,
                "Revealed: the partition is gone, the lamp full, and unit C's six places show");
            LookAt(P(10.6f, 0, 5.2f), P(4.0f, 1.1f, 7.2f)); yield return Shot("133-unit-c-open");

            // Tanks on unit C's places: Show tanks on its feature wall, Large and Starter tanks on its long places.
            foreach (var (kind, place) in new[] { ("show", "c1"), ("show", "c2"), ("large", "c3"), ("starter", "c4"), ("starter", "c5"), ("starter", "c6") })
                Ok(s.Act(() => shop.BuyContainer(kind, place)), "A " + kind + " tank on " + place + " (the place card's button)");
            Check(shop.BuyContainer("show", "c4").Failure != GrowthFailure.None, "A Show tank only goes on the feature wall");
            var stock = new[] { "pondlet", "ribbonfin", "bubblebelly", "lanternfin" }.Where(shop.Discovered.Contains).ToArray();
            foreach (var c in shop.Containers.Where(c => c.Place.StartsWith("c")))
                for (int i = 0; i < 12 && c.Free > 0; i++) if (!shop.DevelopmentStock(c.Id, stock[i % stock.Length]).Success) break;
            foreach (var c in shop.ShopContainers.Where(c => !c.Place.StartsWith("c"))) c.Occupants.Clear();
            yield return null; yield return null;
            Check(shop.ShopContainers.Where(c => c.Place.StartsWith("c")).All(c => c.Occupants.Count > 0) && s.World.Tanks.Values.Count(t => t.Place != null && t.Place.StartsWith("c")) == 6,
                "Unit C's six tanks stand stocked (development stock; the other tanks emptied, labelled)");
            LookAt(P(10.6f, 0, 5.4f), P(4.0f, 1.1f, 7.0f)); yield return Shot("134-unit-c-tanks");

            // A day: visitors walk from the door through unit A, unit B and the arch to browse in unit C, and buy there. The
            // figures walk in real time, so the walk in runs at the game's own pace; the rest of the day is accelerated.
            Ok(s.Act(shop.OpenShop), "Open the shop (as E at the counter does)");
            int unitCBefore = shop.ShopContainers.Where(c => c.Place.StartsWith("c")).Sum(c => c.Occupants.Count);
            bool walkedIn = false, shot = false;
            float walkStart = Time.realtimeSinceStartup;
            while (!walkedIn && shop.Phase == GrowthPhase.Day && Time.realtimeSinceStartup - walkStart < 60)
            {
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
                yield return null;
                foreach (var v in shop.Visitors.Where(v => v.State == GrowthVisitorState.Browsing && v.ContainerId != null))
                {
                    var place = shop.Container(v.ContainerId)?.Place;
                    if (place == null || !place.StartsWith("c") || !s.World.VisitorViews.TryGetValue(v.Id, out var view)) continue;
                    var b = GrowthWorld.Places[place].Browse;
                    if (view.Arrived && (view.transform.position - b).sqrMagnitude < 0.04f)
                    {
                        walkedIn = true;
                        if (!shot)
                        {
                            LookAt(P(10.6f, 0, 5.3f), new Vector3(b.x, 1.0f, b.z));
                            yield return Shot("135-visitor-in-unit-c");
                            shot = true;
                        }
                        break;
                    }
                }
            }
            for (int i = 0; i < 1200 && shop.Phase == GrowthPhase.Day; i++)
            {
                s.Advance(i < 400 ? 0.1f : 0.5f);
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
                if (i % 5 == 0) yield return null;
            }
            yield return null; yield return null;
            int unitCAfter = shop.ShopContainers.Where(c => c.Place.StartsWith("c")).Sum(c => c.Occupants.Count);
            Check(walkedIn && shot, "A visitor walked from the door through unit B's arch to a tank in unit C");
            Check(shop.Phase == GrowthPhase.Night && unitCAfter < unitCBefore, "Through the day (accelerated) visitors bought from unit C's tanks (" + (unitCBefore - unitCAfter) + " creatures)");
            s.SetCard(GrowthCard.None);

            // The workshop opens the same way: its partition comes down in the annex's east doorway.
            Ok(s.Act(shop.BuyWorkshop), "Open the workshop (its card's button)");
            yield return null; yield return null;
            Check(shop.Workshop && !s.World.WorkshopWallStanding && s.World.Revealing("workshop") && s.World.PartitionShowing("workshop"), "The workshop's partition comes down too");
            yield return new WaitForSeconds(0.9f);
            LookAt(P(2.4f, 0, 10.3f), P(8f, 2.2f, 11.6f)); yield return Shot("136-workshop-revealing");
            yield return Wait(() => !s.World.Revealing("workshop"), 6, "the workshop's reveal");
            Check(!s.World.PartitionShowing("workshop") && s.Ui.Log.Any(l => l == "Workshop opened"), "Revealed, and the banner named it");

            // The exhibition hall finish, bought at its plans in unit C's north-east corner.
            yield return AimAt(new Vector3(GrowthWorld.HallBoardPosition.x, 0, P(10.3f, 0, 6.8f).z), GrowthWorld.HallBoardPosition, GrowthTargetKind.HallBoard, "hall", "The reticle focuses the exhibition hall's plans");
            Check(s.FocusName(out status) == "Exhibition hall plans" && status == "E see what it needs", "It is the exhibition hall's plans");
            yield return Press(Key.E);
            card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Hall && card.Contains("Finish the hall for 6000 cr") && card.Contains("two exhibition places"), "E opens its card: the hall finish for 6,000 credits");
            Ok(s.Act(shop.BuyHall), "Finish the exhibition hall (the card's button)");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.Hall && s.World.Revealing("hall") && s.World.BannersShown == 0, "The hall's reveal starts: its banners unrolling");
            yield return new WaitForSeconds(0.9f);
            float spots = s.World.LampIntensity("hall");
            Check(spots > 0.5f && spots < s.World.LampFull("hall") - 0.01f && Mathf.Abs(s.World.LampFull("hall") - 11f) < 0.01f && s.Ui.Log.Any(l => l == "The exhibition hall is finished"), "Halfway: the spotlights at " + spots.ToString("0.0") + " of 11; the banner names it");
            LookAt(P(8f, 0, 1.2f), P(8f, 2.0f, -3.6f)); yield return Shot("137-hall-revealing");
            yield return Wait(() => !s.World.Revealing("hall"), 6, "the hall's reveal");
            Check(s.World.BannersShown == 5 && Mathf.Abs(s.World.LampIntensity("hall") - 11f) < 0.01f && shop.FreePlaces.Contains("e1") && shop.FreePlaces.Contains("e2") && shop.VisitorsAtOnce == 5,
                "Finished: five banners, the spotlights full, the two exhibition places along unit B's front wall, and five visitors at a time");
            yield return null; yield return null;
            LookAt(P(8f, 0, 0.8f), P(8f, 1.1f, -3.6f)); yield return Shot("138-exhibition-hall");

            // Save and reload: every room open at once, no reveal.
            Check(s.SaveNow(), "Save");
            string saved = JsonUtility.ToJson(s.Shop.Snapshot());
            Check(s.ReloadSaved(), "Reload the saved slot");
            shop = s.Shop;
            var file = JsonUtility.FromJson<GrowthSave>(File.ReadAllText(s.SavePath));
            Check(file.version == GrowthSave.CurrentVersion && JsonUtility.ToJson(GrowthShop.Restore(file, out _).Snapshot()) == saved && shop.UnitC && shop.Hall && shop.Workshop &&
                shop.ContainerAt("c1")?.Kind == "show", "The version " + GrowthSave.CurrentVersion + " save brings unit C, its tanks and the hall back");
            yield return null; yield return null;
            Check(new[] { "unit-b", "annex", "workshop", "unit-c", "hall" }.All(r => !s.World.Standing(r) && !s.World.Revealing(r) && !s.World.PartitionShowing(r)) && s.World.BannersShown == 5,
                "Reloaded, every room is open at once, without a reveal");
            LookAt(P(-1.0f, 0, 12.7f), P(-3.84f, 1.6f, 12.7f)); yield return Shot("139-pipe-gallery");

            // GR.5's budget at the final layout: every place of all three units holding its largest container, full.
            int previousRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            s.LoadFixture(GrowthShop.Densest(5, final: true));
            yield return null; yield return null;
            int creatures = s.World.Tanks.Values.Sum(v => v.Creatures.Count);
            Check(s.Shop.Stock.Count() == 264 && creatures == 264 && s.World.Tanks.Count == 16, "The final layout at its densest (labelled fixture): 16 containers, 264 creatures in the world");
            yield return Measure("140-final-unit-c", P(10.6f, 0, 5.2f), P(4.0f, 1.1f, 7.2f), 6);
            yield return Measure("141-final-across", P(8.9f, 0, -1.55f), P(10.9f, 1.1f, 6.0f), 6);
            yield return Measure("142-final-hall", P(4.7f, 0, 0f), P(9.0f, 1.0f, -3.4f), 6);
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVSync;
            File.WriteAllText(Path.Combine(evidence, "final-density-timings.json"), JsonUtility.ToJson(new DensityReport
            {
                note = "Uncapped frame times at the final layout (" + SystemInfo.graphicsDeviceName + ", " + SystemInfo.processorType + ", " + Screen.width + "x" + Screen.height + ").",
                shopCreatures = s.Shop.Stock.Count(), stressCreatures = 0, views = timings.ToArray(),
            }, true));
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }
    }
}
#endif
