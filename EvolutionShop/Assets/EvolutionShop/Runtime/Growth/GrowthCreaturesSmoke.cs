#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-12's Windows route (-evolutionGrowthCreaturesSmoke): wave 1's 24 kit creatures in the greybox. The creatures start
    /// point (a Show tank for each family, every species in two colours); each family's tank; a small creature in peek;
    /// lingering on a creature in peek plays its trick with its burst (a family's at the species' own tempo, and each crown's
    /// own); every kit creature measured inside its tank's water every frame; Reduced motion plays no trick; the cards and
    /// the journal in the atlas's colours; frame times with the three tanks in view.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string CreaturesScenarioText = "GR-12 wave 1 creatures: the creatures start point (a Show tank for each of the Brookkin, Sparkline and Airfin, all 24 species in two colours), each family's tank, a small creature in peek, tricks played by lingering on a creature in peek (a family's at its species' tempo, each crown's own) with their bursts, every kit creature inside its tank's water every frame, no trick with Reduced motion, the journal and cards in the atlas's colours, frame times";

        readonly Dictionary<GrowthCreatureView, Renderer[]> kitParts = new Dictionary<GrowthCreatureView, Renderer[]>();
        float containWorst;
        string containWorstAt = "";
        int containFrames;

        /// <summary>Every kit creature's parts measured against its tank's water this frame (its renderers' own boxes, turned with
        /// them into the tank's space): the farthest any part reaches past the glass, the surface or the sand.</summary>
        void Contain()
        {
            containFrames++;
            foreach (var tank in s.World.Tanks.Values)
                foreach (var view in tank.Creatures.Values)
                {
                    if (view == null || view.Kit == null) continue;
                    if (!kitParts.TryGetValue(view, out var parts)) kitParts[view] = parts = view.Kit.GetComponentsInChildren<Renderer>(true);
                    var toTank = tank.Root.worldToLocalMatrix;
                    foreach (var r in parts)
                    {
                        if (!r) continue;
                        var b = r is SkinnedMeshRenderer skinned ? skinned.localBounds : r.GetComponent<MeshFilter>() is MeshFilter f && f.sharedMesh ? f.sharedMesh.bounds : r.localBounds;
                        var m = toTank * r.transform.localToWorldMatrix;
                        for (int k = 0; k < 8; k++)
                        {
                            var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                            float over = Mathf.Max(Mathf.Abs(p.x) - tank.Inner.x * 0.5f, Mathf.Abs(p.z) - tank.Inner.z * 0.5f, Mathf.Abs(p.y - tank.WaterY) - tank.Inner.y * 0.5f);
                            if (over > containWorst) { containWorst = over; containWorstAt = view.Species + " " + r.name + " in " + tank.Place + (view.TrickAge >= 0 ? " (trick)" : "") + (view.Highlighted ? " (in peek)" : ""); }
                        }
                    }
                }
        }

        /// <summary>Waits a while at the game's own pace, measuring every kit creature against its water each frame.</summary>
        IEnumerator Swim(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) { Contain(); yield return null; }
        }

        static Vector3 Near(GrowthWorld.Place p, float fromBrowse) => p.Browse + (p.Position - p.Browse).normalized * fromBrowse;

        /// <summary>Peeks at the tank on a place, follows one creature until it is under the reticle, and lingers on it as a
        /// player would until its trick plays; captures it mid-trick.</summary>
        IEnumerator Linger(string place, string species, string shot, string why)
        {
            var p = GrowthWorld.Places[place];
            var tank = s.World.Tanks.Values.First(t => t.Place == place);
            var creature = tank.Creatures.Values.First(v => v.Species == species);
            var feet = Near(p, 0.1f);
            int played = GrowthCreatureView.TricksPlayed, bursts = creature.Bursts;
            LookAt(feet, creature.transform.position);
            yield return Wait(() => { LookAt(feet, creature.transform.position); Contain(); return s.Peeking && s.PeekCreature == creature; }, 8, "peek at the " + species);
            Check(s.PeekCreature == creature && creature.Highlighted, "Peek puts the " + species + " under the reticle (" + s.FocusName(out _) + ")");
            yield return Wait(() => { LookAt(feet, creature.transform.position); Contain(); return creature.TrickAge >= 0; }, GrowthCreatureView.TrickLinger + 3, species + "'s trick");
            Check(creature.TrickAge >= 0 && GrowthCreatureView.TricksPlayed == played + 1, "Lingering on it " + GrowthCreatureView.TrickLinger.ToString("0.0") + " s plays its trick: " + why);
            float duration = DioramaTricks.Duration(species);
            float shotAt = Time.realtimeSinceStartup + duration * 0.45f;
            while (Time.realtimeSinceStartup < shotAt) { LookAt(feet, creature.transform.position); Contain(); yield return null; }
            yield return Shot(shot);
            yield return Wait(() => { LookAt(feet, creature.transform.position); Contain(); return creature.TrickAge < 0; }, duration + 2, species + "'s trick to end");
            Check(creature.Bursts - bursts == DioramaTricks.Releases(species), "It lets its burst go on each beat (" + (creature.Bursts - bursts) + " of " + DioramaTricks.Releases(species) + ") and settles back to its swim");
        }

        IEnumerator CreaturesScenario()
        {
            limitations.Add("GR-12 creatures route: the creatures start point (GrowthStarts.CreaturesStart: the spaces start with shop unit C open, every Brookkin, Sparkline and Airfin known in all four colours, a Show tank of each family on b4, c1 and c2 holding its species in Classic and one other colour, the Airfin's second colours in Large tanks on c3 and c4) is a labelled fixture. The view is aimed as a player would aim it, following a creature in peek until it is under the reticle; everything runs at the game's own pace. Water containment is measured on every kit creature's parts every frame of the route's swims and tricks (each renderer's own box, turned into its tank's space). Reduced motion is switched in the session as the pause card does. Frame times are measured with the game's 120 fps cap. The models are the kit's (Blender review renders in ArtSource/DioramaKit); the greybox's rooms and lighting are placeholders.");
            yield return Opening();
            TakeInput();
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Creatures));
            var shop = s.Shop;
            yield return null; yield return null;
            var wave = DioramaSpecies.All.Select(x => x.Id).ToList();
            // The five tanks of the look (the era 4 start's own stocked tanks stay as they were).
            var places = new[] { "b4", "c1", "c2", "c3", "c4" };
            var mine = shop.ShopContainers.Where(c => places.Contains(c.Place)).SelectMany(c => c.Occupants).ToList();
            var views = s.World.Tanks.Values.Where(t => places.Contains(t.Place)).SelectMany(t => t.Creatures.Values).ToList();
            Check(wave.Count == 24 && wave.All(id => mine.Count(x => x.Species == id) == 2) && mine.Count == 48,
                "Fixture: all 24 of wave 1 (the Brookkin, Sparkline and Airfin, eight each) in the look's five tanks, two of each");
            Check(views.Count == 48 && views.All(v => v.Kit != null && v.Kit.IsDiorama && v.Kit.DioramaSpecies.Id == v.Species), "Every one is its kit model");
            var everywhere = s.World.Tanks.Values.SelectMany(t => t.Creatures.Values).ToList();
            Check(everywhere.All(v => (v.Kit != null) == DioramaSpecies.TryGet(v.Species, out _)), "In every tank, the three families' creatures are kit models and the other families' are still placeholders (" +
                everywhere.Count(v => v.Kit == null) + " placeholders)");
            Check(views.All(v =>
            {
                var colour = mine.First(x => x.Id == v.SpecimenId).Colour;
                return v.Kit.DioramaVariant.ToString() == colour.ToString();
            }) && mine.Select(x => x.Colour).Distinct().Count() == 4, "Each shows its specimen's colour from the variant atlas (Classic, Sunny, Deep and Shimmer all here)");
            Check(views.All(v => Mathf.Approximately(v.Length, GrowthCreatureView.LengthOf(GrowthContent.Get(v.Species)))) &&
                views.Where(v => GrowthContent.Get(v.Species).Kind != SpeciesKind.Crown).Select(v => v.Length).Distinct().Count() == 2 &&
                views.Where(v => GrowthContent.Get(v.Species).Kind == SpeciesKind.Crown).All(v => v.Length > GrowthCreatureView.LengthOf(GrowthContent.Get(v.Species).Size)),
                "Each at its size class (small 7.5 cm, the Airfin medium 15 cm), the crowns grander at the top of theirs");
            Check(wave.All(id => System.Enum.GetValues(typeof(Colour)).Cast<Colour>().All(c => shop.Journal.Contains(GrowthShop.JournalKey(id, c)))), "All 96 of wave 1's journal entries known");

            // Each family's Show tank, from where a visitor browses.
            foreach (var (place, shot, family) in new[] { ("b4", "170-brookkin-tank", "Brookkin"), ("c1", "171-sparkline-tank", "Sparkline"), ("c2", "172-airfin-tank", "Airfin") })
            {
                var p = GrowthWorld.Places[place];
                LookAt(p.Browse + (p.Browse - p.Position).normalized * 0.9f, p.Position + new Vector3(0, 1.1f, 0));
                yield return Swim(2.5f);
                var tank = s.World.Tanks.Values.First(t => t.Place == place);
                Check(tank.Creatures.Count == 16 / (family == "Airfin" ? 2 : 1) && tank.Creatures.Values.All(v => v.Kit.DioramaSpecies.Family.ToString() == family),
                    "The " + family + "'s Show tank on " + place + ": its eight species swimming");
                yield return Shot(shot);
            }

            // A small creature in peek: big eyes and its face readable at 7.5 cm (GR.14).
            yield return Linger("b4", "puddlepup", "173-trick-puddlepup-splash", "the Brookkin's splash (a hop that shakes the water off), at the Puddlepup's tempo");
            yield return Linger("c1", "starglimmer", "174-trick-starglimmer-twinkle", "the Sparkline's twinkle (a turn on the spot, the lantern twinkling three times), at the Starglimmer's tempo");
            yield return Linger("c2", "hazewing", "175-trick-hazewing-glide", "the Airfin's glide (sails wide, one banked circle), at the Hazewing's tempo");
            yield return Linger("b4", "brookmonarch", "176-trick-brookmonarch-fountain", "the Brook Monarch's own fountain (a bow, three fountains of bubbles)");
            yield return Linger("c1", "aurorafin", "177-trick-aurorafin-aurora", "the Aurorafin's own aurora (a barrel roll, its three lanterns glowing in waves)");
            yield return Linger("c2", "zephyray", "178-trick-zephyray-gale", "the Zephyray's own gale (two great wing beats, a rising spiral)");
            yield return Linger("b4", "pondlet", "179-trick-pondlet-bubble", "the Pondlet keeps its own bubble");

            // Reduced motion: the same linger plays nothing.
            s.ReducedMotion = true;
            {
                var p = GrowthWorld.Places["c1"];
                var tank = s.World.Tanks.Values.First(t => t.Place == "c1");
                var creature = tank.Creatures.Values.First(v => v.Species == "fizzlet");
                var feet = Near(p, 0.1f);
                int played = GrowthCreatureView.TricksPlayed;
                yield return Wait(() => { LookAt(feet, creature.transform.position); return s.Peeking && s.PeekCreature == creature; }, 8, "peek at the Fizzlet");
                float until = Time.realtimeSinceStartup + GrowthCreatureView.TrickLinger + 2.5f;
                while (Time.realtimeSinceStartup < until) { LookAt(feet, creature.transform.position); Contain(); yield return null; }
                Check(s.PeekCreature == creature && GrowthCreatureView.TricksPlayed == played && creature.TrickAge < 0, "With Reduced motion, lingering on the Fizzlet in peek plays no trick");
            }
            s.ReducedMotion = false;

            // The cards and the journal show wave 1 in the atlas's colours.
            Check(GrowthContent.Species.Where(x => GrowthStarts.KitFamilies.Contains(x.Family)).All(def => System.Enum.GetValues(typeof(Colour)).Cast<Colour>().All(c =>
            {
                DioramaSpecies.TryGet(def.Id, out var kit);
                var atlas = DioramaVariants.Color(kit.Index, (DioramaVariant)System.Enum.Parse(typeof(DioramaVariant), c.ToString()), 0); atlas.a = 1;
                return Vector4.Distance(GrowthCreatureView.ColourOf(def, c), atlas) < 0.005f;
            })), "Cards and the journal colour wave 1's creatures from the atlas, as they swim");
            s.SetCard(GrowthCard.None); yield return null;
            yield return Press(Key.Tab);
            yield return new WaitForSeconds(0.25f);
            string journal = s.Ui.CardText();
            Check(s.Card == GrowthCard.Journal && journal.Contains("Brookkin · small · 8/8 · colours 32/32") && journal.Contains("Sparkline · small · 8/8 · colours 32/32") &&
                journal.Contains("Airfin · medium · 8/8 · colours 32/32"), "Tab: the journal has the three families complete, eight species in four colours each");
            Check(s.Ui.ScrollTo("Brookkin ·"), "Scroll to the Brookkin's chart");
            yield return Shot("180-journal-brookkin");
            Check(s.Ui.ScrollTo("Airfin ·"), "Scroll to the Airfin's chart");
            yield return Shot("181-journal-airfin");
            s.SetCard(GrowthCard.None); yield return null;

            // A long look across the three tanks at the game's own pace: containment and frame times.
            var c1 = GrowthWorld.Places["c1"];
            LookAt(c1.Browse + (c1.Browse - c1.Position).normalized * 2.4f + new Vector3(-0.6f, 0, 0), c1.Position + new Vector3(0, 1.0f, 0));
            yield return Swim(1f);
            int frames = 0; float sum = 0, worstFrame = 0;
            float until2 = Time.realtimeSinceStartup + 8;
            while (Time.realtimeSinceStartup < until2)
            {
                Contain(); yield return null;
                frames++; sum += Time.unscaledDeltaTime; worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
            }
            Check(frames > 30, "Frame timings with unit C's two Show tanks in view (average " + (sum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
            yield return Shot("182-unit-c-tanks");
            Check(containFrames > 200 && containWorst <= 0.005f, "Every kit creature stayed inside its tank's water every frame (" + containFrames + " frames measured; farthest past the water " +
                (containWorst * 100f).ToString("0.0") + " cm" + (containWorstAt.Length > 0 ? ", " + containWorstAt : "") + ")");
        }
    }
}
#endif
