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
    /// GR-12's Windows route (-evolutionGrowthCreaturesSmoke): waves 1 to 4's 72 kit creatures in the greybox. The creatures
    /// start point (tanks for each family, the newest wave's species in two colours); each family's tank; a small creature in peek;
    /// lingering on a creature in peek plays its trick with its burst (a family's at the species' own tempo, and each crown's
    /// own); every kit creature measured inside its tank's water every frame; Reduced motion plays no trick; the cards and
    /// the journal in the atlas's colours; frame times with the three tanks in view.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string CreaturesScenarioText = "GR-12 creatures, waves 1 to 4: the creatures start point (tanks for all nine families, all 72 species, wave 4's Gloamer and Cometail in two colours), each family's tank, a small creature in peek, tricks played by lingering on a creature in peek (a family's at its species' tempo, each crown's own) with their bursts, every kit creature inside its tank's water every frame, no trick with Reduced motion, the journal and cards in the atlas's colours, frame times";

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
                            if (over > containWorst)
                            {
                                containWorst = over;
                                var centre = toTank.MultiplyPoint3x4(view.transform.position);
                                containWorstAt = view.Species + " " + r.name + " in " + tank.Place + (view.TrickAge >= 0 ? " (trick)" : "") + (view.Highlighted ? " (in peek)" : "") +
                                    " (its centre " + centre.x.ToString("0.000") + ", " + (centre.y - tank.WaterY).ToString("0.000") + ", " + centre.z.ToString("0.000") + " from the water's, the part " +
                                    p.x.ToString("0.000") + ", " + (p.y - tank.WaterY).ToString("0.000") + ", " + p.z.ToString("0.000") + ", scale " + view.transform.localScale.x.ToString("0.00") +
                                    ", its room " + view.TrickReach.ToString("0.000") + " across, " + view.TrickHalf.ToString("0.000") + " up, detail " + view.Detail + ")";
                            }
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
            // Linger as a player would: if the reticle keeps leaving it, keep aiming at it, up to three tries; every frame
            // notes how the linger went, so a trick that never plays says why.
            int frames = 0, away = 0, farLook = 0, tries = 0; float streak = 0, longest = 0;
            while (creature.TrickAge < 0 && tries < 3)
            {
                tries++;
                float until = Time.realtimeSinceStartup + GrowthCreatureView.TrickLinger + 3;
                while (creature.TrickAge < 0 && Time.realtimeSinceStartup < until)
                {
                    LookAt(feet, creature.transform.position); Contain();
                    yield return null;
                    frames++;
                    if (creature.Detail != 0) farLook++;
                    if (s.PeekCreature == creature) { streak += Time.unscaledDeltaTime; longest = Mathf.Max(longest, streak); } else { away++; streak = 0; }
                }
            }
            string linger = frames + " frames, the reticle away " + away + ", the longest stretch on it " + longest.ToString("0.00") + " s, its distant look " + farLook + ", " + tries + " tr" + (tries == 1 ? "y" : "ies");
            Check(creature.TrickAge >= 0 && GrowthCreatureView.TricksPlayed == played + 1, "Lingering on it " + GrowthCreatureView.TrickLinger.ToString("0.0") + " s plays its trick: " + why + " (" + linger + ")");
            if (creature.TrickAge < 0) yield break;
            float duration = DioramaTricks.Duration(species);
            float shotAt = Time.realtimeSinceStartup + duration * 0.45f;
            while (Time.realtimeSinceStartup < shotAt) { LookAt(feet, creature.transform.position); Contain(); yield return null; }
            yield return Shot(shot);
            yield return Wait(() => { LookAt(feet, creature.transform.position); Contain(); return creature.TrickAge < 0; }, duration + 2, species + "'s trick to end");
            Check(creature.Bursts - bursts == DioramaTricks.Releases(species), "It lets its burst go on each beat (" + (creature.Bursts - bursts) + " of " + DioramaTricks.Releases(species) + ") and settles back to its swim");
        }

        IEnumerator CreaturesScenario()
        {
            limitations.Add("GR-12 creatures route: the creatures start point (GrowthStarts.CreaturesStart: the spaces start with shop unit C and the exhibition hall open, all nine families' species known in all four colours; wave 4's Gloamer and Cometail, each species in Classic and one other colour, in Large tanks fitted to 16 spaces, the Gloamer on the hall's e1 and e2 (second colours b1 and b2) and the Cometail on c3 and c4 (second colours c5 and c6); waves 1 to 3, approved, each species in Classic: the Brookkin with the Saltscale in unit B's Show tank on b4 (24 spaces), the Sparkline with the Dewling and the Airfin in unit C's Show tanks on c1 and c2, the Cinderling on a1 and the Reefback on a3 and b3; the spaces start's own tanks on a1, a3, b1 and b2 emptied and sold) is a labelled fixture. The view is aimed as a player would aim it, following a creature in peek until it is under the reticle; everything runs at the game's own pace. Water containment is measured on every kit creature's parts every frame of the route's swims and tricks (each renderer's own box, turned into its tank's space). Reduced motion is switched in the session as the pause card does. Frame times are measured with the game's 120 fps cap. The models are the kit's (Blender review renders in ArtSource/DioramaKit); the greybox's rooms and lighting are placeholders.");
            yield return Opening();
            TakeInput();
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Creatures));
            var shop = s.Shop;
            yield return null; yield return null;
            var wave = DioramaSpecies.All.Select(x => x.Id).ToList();
            var newest = wave.Where(id => GrowthStarts.NewestFamilies.Contains(GrowthContent.Get(id).Family)).ToList();
            // The fourteen tanks of the look (the era 4 start's own stocked tank on a2 stays as it was).
            var places = new[] { "b4", "c1", "c2", "a1", "a3", "b3", "e1", "e2", "b1", "b2", "c3", "c4", "c5", "c6" };
            var mine = shop.ShopContainers.Where(c => places.Contains(c.Place)).SelectMany(c => c.Occupants).ToList();
            var views = s.World.Tanks.Values.Where(t => places.Contains(t.Place)).SelectMany(t => t.Creatures.Values).ToList();
            Check(wave.Count == 72 && newest.Count == 16 && wave.All(id => mine.Count(x => x.Species == id) == (newest.Contains(id) ? 2 : 1)) && mine.Count == 88,
                "Fixture: all 72 of waves 1 to 4 (nine families, eight each) in the look's fourteen tanks, wave 4's Gloamer and Cometail in two colours each, waves 1 to 3 in Classic");
            Check(views.Count == 88 && views.All(v => v.Kit != null && v.Kit.IsDiorama && v.Kit.DioramaSpecies.Id == v.Species), "Every one is its kit model");
            var everywhere = s.World.Tanks.Values.SelectMany(t => t.Creatures.Values).ToList();
            Check(everywhere.All(v => v.Kit != null && DioramaSpecies.TryGet(v.Species, out _)), "In every tank, every creature is its kit model: all nine families (" +
                everywhere.Count(v => v.Kit == null) + " placeholders)");
            Check(views.All(v =>
            {
                var colour = mine.First(x => x.Id == v.SpecimenId).Colour;
                return v.Kit.DioramaVariant.ToString() == colour.ToString();
            }) && mine.Select(x => x.Colour).Distinct().Count() == 4, "Each shows its specimen's colour from the variant atlas (Classic, Sunny, Deep and Shimmer all here)");
            Check(views.All(v => Mathf.Approximately(v.Length, GrowthCreatureView.LengthOf(GrowthContent.Get(v.Species)))) &&
                views.Where(v => GrowthContent.Get(v.Species).Kind != SpeciesKind.Crown).Select(v => v.Length).Distinct().Count() == 3 &&
                views.Where(v => GrowthContent.Get(v.Species).Kind == SpeciesKind.Crown).All(v => v.Length > GrowthCreatureView.LengthOf(GrowthContent.Get(v.Species).Size)),
                "Each at its size class (small 7.5 cm; the Airfin, Cinderling and Saltscale medium 15 cm; the Reefback, Gloamer and Cometail large 32 cm), the crowns grander at the top of theirs");
            Check(wave.All(id => System.Enum.GetValues(typeof(Colour)).Cast<Colour>().All(c => shop.Journal.Contains(GrowthShop.JournalKey(id, c)))), "All 288 of waves 1 to 4's journal entries known");

            // Each family's tank, from where a visitor browses (waves 1 to 3 share tanks two families to one where there is room).
            foreach (var (place, shot, families) in new[] { ("b4", "170-brookkin-tank", new[] { "Brookkin", "Saltscale" }), ("c1", "171-sparkline-tank", new[] { "Sparkline", "Dewling" }),
                ("c2", "172-airfin-tank", new[] { "Airfin" }), ("a1", "183-cinderling-tank", new[] { "Cinderling" }), ("a3", "191-reefback-tank", new[] { "Reefback" }),
                ("e1", "199-gloamer-tank", new[] { "Gloamer" }), ("c3", "200-cometail-tank", new[] { "Cometail" }) })
            {
                var p = GrowthWorld.Places[place];
                LookAt(p.Browse + (p.Browse - p.Position).normalized * 0.9f, p.Position + new Vector3(0, 1.1f, 0));
                yield return Swim(2.5f);
                var tank = s.World.Tanks.Values.First(t => t.Place == place);
                bool large = families[0] == "Reefback" || families[0] == "Gloamer" || families[0] == "Cometail";
                var counts = families.Select(f => tank.Creatures.Values.Count(v => v.Kit.DioramaSpecies.Family.ToString() == f)).ToArray();
                Check(counts.All(n => n == (large ? 4 : 8)) && tank.Creatures.Count == counts.Sum(),
                    "The " + string.Join(" and the ", families) + "'s tank on " + place + ": " + (large ? "four of its large species" : "eight species of each") + " swimming");
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
            yield return Linger("a1", "emberpuff", "185-trick-emberpuff-flare", "the Cinderling's flare (its ember spines stand tall and blaze, a fiery flick of the tail), at the Emberpuff's tempo");
            yield return Linger("c1", "dewdrop", "186-trick-dewdrop-sprinkle", "the Dewling's sprinkle (a shake from nose to tail, its dew beads scattering droplets), at the Dewdrop's tempo");
            yield return Linger("a1", "phoenixfry", "187-trick-phoenixfry-rebirth", "the Phoenix Fry's own rebirth (a somersault through its blazing flames, sparks twice)");
            yield return Linger("c1", "rainbowregent", "188-trick-rainbowregent-rainbow", "the Rainbow Regent's own rainbow (a slow royal turn, its rainbow tail sweeping, droplets three times)");
            yield return Linger("a3", "shellback", "193-trick-shellback-bloom", "the Reefback's bloom (a slow bow, its corals swaying open, a plankton cloud), at the Shellback's tempo");
            yield return Linger("b4", "brinefin", "194-trick-brinefin-dart", "the Saltscale's dart (a quick dart and turn, its salt stripe flashing silver, a sparkle of salt), at the Brinefin's tempo");
            yield return Linger("b3", "coralossus", "195-trick-coralossus-reef", "Coralossus's own reef (its whole reef blooming as it turns slowly once, plankton three times)");
            yield return Linger("b4", "pearlemperor", "196-trick-pearlemperor-pearl", "the Pearl Emperor's own pearl (two royal darts round, its stripe flashing silver on each)");
            yield return Linger("e1", "moonmouth", "201-trick-moonmouth-lure", "the Gloamer's lure (its lights dim, then its lure pulses brighter and brighter, a drift of glowing motes), at the Moonmouth's tempo");
            yield return Linger("c3", "starmaw", "202-trick-starmaw-streak", "the Cometail's streak (round a loop, rising and banking as its head and tail blaze, a trail of stardust), at the Starmaw's tempo");
            yield return Linger("e2", "eclipseleviathan", "203-trick-eclipseleviathan-eclipse", "the Eclipse Leviathan's own eclipse (its lights go dark as it turns, then the corona round its lure blazes twice)");
            yield return Linger("c4", "celestialwyrm", "204-trick-celestialwyrm-starfall", "the Celestial Wyrm's own starfall (it rises and spirals round twice, stardust three times)");

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
            })), "Cards and the journal colour waves 1 to 4's creatures from the atlas, as they swim");
            s.SetCard(GrowthCard.None); yield return null;
            yield return Press(Key.Tab);
            yield return new WaitForSeconds(0.25f);
            string journal = s.Ui.CardText();
            Check(s.Card == GrowthCard.Journal && journal.Contains("Brookkin · small · 8/8 · colours 32/32") && journal.Contains("Sparkline · small · 8/8 · colours 32/32") &&
                journal.Contains("Airfin · medium · 8/8 · colours 32/32") && journal.Contains("Cinderling · medium · 8/8 · colours 32/32") && journal.Contains("Dewling · small · 8/8 · colours 32/32") &&
                journal.Contains("Reefback · large · 8/8 · colours 32/32") && journal.Contains("Saltscale · medium · 8/8 · colours 32/32") &&
                journal.Contains("Gloamer · large · 8/8 · colours 32/32") && journal.Contains("Cometail · large · 8/8 · colours 32/32"),
                "Tab: the journal has all nine families complete, eight species in four colours each");
            Check(s.Ui.ScrollTo("Brookkin ·"), "Scroll to the Brookkin's chart");
            yield return Shot("180-journal-brookkin");
            Check(s.Ui.ScrollTo("Airfin ·"), "Scroll to the Airfin's chart");
            yield return Shot("181-journal-airfin");
            Check(s.Ui.ScrollTo("Cinderling ·"), "Scroll to the Cinderling's chart");
            yield return Shot("189-journal-cinderling");
            Check(s.Ui.ScrollTo("Dewling ·"), "Scroll to the Dewling's chart");
            yield return Shot("190-journal-dewling");
            Check(s.Ui.ScrollTo("Reefback ·"), "Scroll to the Reefback's chart");
            yield return Shot("197-journal-reefback");
            Check(s.Ui.ScrollTo("Saltscale ·"), "Scroll to the Saltscale's chart");
            yield return Shot("198-journal-saltscale");
            Check(s.Ui.ScrollTo("Gloamer ·"), "Scroll to the Gloamer's chart");
            yield return Shot("205-journal-gloamer");
            Check(s.Ui.ScrollTo("Cometail ·"), "Scroll to the Cometail's chart");
            yield return Shot("206-journal-cometail");
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
            Check(frames > 30, "Frame timings with unit C's tanks in view (average " + (sum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
            yield return Shot("182-unit-c-tanks");
            Check(containFrames > 200 && containWorst <= 0.005f, "Every kit creature stayed inside its tank's water every frame (" + containFrames + " frames measured; farthest past the water " +
                (containWorst * 100f).ToString("0.0") + " cm" + (containWorstAt.Length > 0 ? ", " + containWorstAt : "") + ")");
        }
    }
}
#endif
