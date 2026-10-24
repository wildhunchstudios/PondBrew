using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>AD-B creature families. Each family shares one rig (the same pivots, face parts and face shape keys); its
    /// species differ by body, fins, colours and the family's signature part (Sparkline: the lantern). GR-12 completes each
    /// family to eight species, wave by wave (wave 1: the first three; wave 2: the Cinderling and the Dewling; wave 3: the
    /// Reefback and the Saltscale; wave 4: the Gloamer and the Cometail).</summary>
    public enum DioramaFamily { Brookkin, Sparkline, Airfin, Cinderling, Dewling, Reefback, Saltscale, Gloamer, Cometail }

    /// <summary>GR-12B: how a species remade as an animal of its own swims, by its body plan (GR.34). Family: its family's strokes;
    /// Flap: its wing fins beat up and down together (a ray); Scull: its tall top and bottom fins sweep side to side together (a
    /// sunfish, its bottom fin the Keel pivot); Undulate: a wave runs down its body from head to tail (an eel or a ribbon, its
    /// Body's Wave keys); Pulse: its cloak opens and closes (a squid, the Tail pivot).</summary>
    public enum DioramaGait { Family, Flap, Scull, Undulate, Pulse }

    /// <summary>AD.3's four colour variants. Colour swaps only; in the greybox a specimen's colour (GR.7.2) picks it.</summary>
    public enum DioramaVariant { Classic, Sunny, Deep, Shimmer }

    /// <summary>One kit species: its model, family, place in the variant atlas and how big containers show it.</summary>
    public readonly struct DioramaSpecies
    {
        public readonly string Id, Kit, Signature;
        public readonly DioramaFamily Family;
        /// <summary>Row block in the variant atlas (rows Index * 4 to Index * 4 + 3), fixed forever as species are added.</summary>
        public readonly int Index;
        public readonly float ModelScale, TankScale, IncubatorScale, SwimAmplitude, TrickSeconds;
        /// <summary>GR-12: how quickly it strokes and plays its family's habit (1 for AD-B's five); its trick's own length is
        /// TrickSeconds, so each species plays its family's trick at its own tempo.</summary>
        public readonly float Tempo;
        /// <summary>GR-12: a family's crown, grander, with a trick of its own.</summary>
        public readonly bool Crown;
        /// <summary>GR-12B: remade as an aquatic animal of its own (GR.34): sized by its span (its largest extent, not counting a lure)
        /// rather than its body's length, and swimming by its body plan.</summary>
        public bool Remade => Id != null && Gaits.ContainsKey(Id);
        /// <summary>GR-12B: how it swims (Family for every species not yet remade).</summary>
        public DioramaGait Gait => Id != null && Gaits.TryGetValue(Id, out var gait) ? gait : DioramaGait.Family;
        /// <summary>GR-12B's remade species, wave by wave (wave 1: the Gloamer), and how each swims.</summary>
        static readonly Dictionary<string, DioramaGait> Gaits = new Dictionary<string, DioramaGait>
        {
            { "moonmouth", DioramaGait.Family }, { "gloomfin", DioramaGait.Scull }, { "shadeback", DioramaGait.Family },
            { "hushray", DioramaGait.Flap }, { "duskdrifter", DioramaGait.Undulate }, { "murkmaw", DioramaGait.Family },
            { "nightlantern", DioramaGait.Pulse }, { "eclipseleviathan", DioramaGait.Family },
        };

        DioramaSpecies(string id, string kit, DioramaFamily family, int index, float model, float tank, float incubator,
            float swim, float trick, string signature, float tempo = 1, bool crown = false)
        {
            Id = id; Kit = kit; Family = family; Index = index; ModelScale = model; TankScale = tank; IncubatorScale = incubator;
            SwimAmplitude = swim; TrickSeconds = trick; Signature = signature; Tempo = tempo; Crown = crown;
        }

        /// <summary>A GR-12 species: shown only in the greybox at its GR size class, so the classic containers' sizes stay
        /// plain (the model's 0.9, and 1 in tanks and the incubator).</summary>
        static DioramaSpecies Wave(string id, DioramaFamily family, int index, float trick, float tempo, string signature, bool crown = false)
            => new DioramaSpecies(id, "Diorama" + char.ToUpperInvariant(id[0]) + id.Substring(1), family, index, .9f, 1, 1, .7f, trick,
                signature, tempo, crown);

        /// <summary>AD-B's five, then GR-12's waves in the kit script's order (the atlas rows follow it forever). AD-B's scales
        /// are on top of each classic container's size, and the AD-A and AD-B routes check those stay inside their glass; the
        /// greybox shows every species at its GR size class instead. Signatures name the trick: AD-B's five keep their own,
        /// each family's other species play its family's (splash, twinkle, glide, flare, sprinkle, bloom, dart, lure, streak) at
        /// their own tempo, and each crown its own.</summary>
        public static readonly IReadOnlyList<DioramaSpecies> All = new[]
        {
            new DioramaSpecies("pondlet", "DioramaPondlet", DioramaFamily.Brookkin, 0, .9f, 1.6f, 1.5f, .82f, .6f, "bubble"),
            new DioramaSpecies("bubblebelly", "DioramaBubblebelly", DioramaFamily.Brookkin, 1, .9f, 1.2f, 1.2f, .7f, 1.8f, "balloon"),
            new DioramaSpecies("ribbonfin", "DioramaRibbonfin", DioramaFamily.Brookkin, 2, .9f, 1.25f, 1.2f, 1.6f, 1.4f, "twirl"),
            new DioramaSpecies("lanternfin", "DioramaLanternfin", DioramaFamily.Sparkline, 3, .9f, 1.3f, 1.25f, .75f, 1.2f, "flash"),
            new DioramaSpecies("driftwing", "DioramaDriftwing", DioramaFamily.Airfin, 4, .9f, 1.2f, 1.15f, .7f, 1.8f, "loop"),
            // GR-12 wave 1 (2026-10-24): the Brookkin, Sparkline and Airfin completed to eight each.
            Wave("puddlepup", DioramaFamily.Brookkin, 5, 1f, 1.25f, "splash"),
            Wave("splashkit", DioramaFamily.Brookkin, 6, .95f, 1.3f, "splash"),
            Wave("reedwhisker", DioramaFamily.Brookkin, 7, 1.4f, .85f, "splash"),
            Wave("lilyfin", DioramaFamily.Brookkin, 8, 1.3f, .9f, "splash"),
            Wave("brookmonarch", DioramaFamily.Brookkin, 9, 2f, .8f, "fountain", true),
            Wave("sparkminnow", DioramaFamily.Sparkline, 10, 1.1f, 1.3f, "twinkle"),
            Wave("fizzlet", DioramaFamily.Sparkline, 11, 1.2f, 1.2f, "twinkle"),
            Wave("glowbelly", DioramaFamily.Sparkline, 12, 1.6f, .9f, "twinkle"),
            Wave("flickerfin", DioramaFamily.Sparkline, 13, 1.15f, 1.25f, "twinkle"),
            Wave("buzzgill", DioramaFamily.Sparkline, 14, 1.05f, 1.35f, "twinkle"),
            Wave("starglimmer", DioramaFamily.Sparkline, 15, 1.3f, 1.1f, "twinkle"),
            Wave("aurorafin", DioramaFamily.Sparkline, 16, 2f, .8f, "aurora", true),
            Wave("puffglider", DioramaFamily.Airfin, 17, 2f, .85f, "glide"),
            Wave("hazewing", DioramaFamily.Airfin, 18, 1.5f, 1.2f, "glide"),
            Wave("gustgill", DioramaFamily.Airfin, 19, 1.4f, 1.3f, "glide"),
            Wave("mistrunner", DioramaFamily.Airfin, 20, 1.45f, 1.25f, "glide"),
            Wave("cloudcarp", DioramaFamily.Airfin, 21, 2f, .85f, "glide"),
            Wave("breezeling", DioramaFamily.Airfin, 22, 1.5f, 1.2f, "glide"),
            Wave("zephyray", DioramaFamily.Airfin, 23, 2f, .75f, "gale", true),
            // GR-12 wave 2 (2026-10-24): the Cinderling and the Dewling, eight each (lively species quicker, calm ones slower).
            Wave("emberpuff", DioramaFamily.Cinderling, 24, 1.2f, 1.15f, "flare"),
            Wave("cindertail", DioramaFamily.Cinderling, 25, 1.05f, 1.3f, "flare"),
            Wave("coalbelly", DioramaFamily.Cinderling, 26, 1.6f, .8f, "flare"),
            Wave("sizzlesnout", DioramaFamily.Cinderling, 27, 1.1f, 1.25f, "flare"),
            Wave("kindlekoi", DioramaFamily.Cinderling, 28, 1.5f, .9f, "flare"),
            Wave("sootsnap", DioramaFamily.Cinderling, 29, 1.15f, 1.2f, "flare"),
            Wave("ashfin", DioramaFamily.Cinderling, 30, 1.45f, .95f, "flare"),
            Wave("phoenixfry", DioramaFamily.Cinderling, 31, 2f, 1f, "rebirth", true),
            Wave("dewdrop", DioramaFamily.Dewling, 32, 1.2f, 1f, "sprinkle"),
            Wave("fogfin", DioramaFamily.Dewling, 33, 1.05f, 1.25f, "sprinkle"),
            Wave("mistlet", DioramaFamily.Dewling, 34, 1.3f, .95f, "sprinkle"),
            Wave("drizzler", DioramaFamily.Dewling, 35, 1f, 1.3f, "sprinkle"),
            Wave("pearlpip", DioramaFamily.Dewling, 36, 1.4f, .85f, "sprinkle"),
            Wave("haloling", DioramaFamily.Dewling, 37, 1.5f, .8f, "sprinkle"),
            Wave("rainskipper", DioramaFamily.Dewling, 38, .95f, 1.35f, "sprinkle"),
            Wave("rainbowregent", DioramaFamily.Dewling, 39, 2f, .8f, "rainbow", true),
            // GR-12 wave 3 (2026-10-24): the Reefback and the Saltscale, eight each (lively species quicker, calm ones slower).
            Wave("shellback", DioramaFamily.Reefback, 40, 1.8f, .8f, "bloom"),
            Wave("coralhorn", DioramaFamily.Reefback, 41, 1.4f, 1.1f, "bloom"),
            Wave("clamjaw", DioramaFamily.Reefback, 42, 1.9f, .75f, "bloom"),
            Wave("kelpgrazer", DioramaFamily.Reefback, 43, 1.7f, .85f, "bloom"),
            Wave("barnaclebrow", DioramaFamily.Reefback, 44, 1.75f, .8f, "bloom"),
            Wave("tidetusk", DioramaFamily.Reefback, 45, 1.35f, 1.15f, "bloom"),
            Wave("wavebreaker", DioramaFamily.Reefback, 46, 1.3f, 1.2f, "bloom"),
            Wave("coralossus", DioramaFamily.Reefback, 47, 2f, .75f, "reef", true),
            Wave("brinefin", DioramaFamily.Saltscale, 48, 1f, 1.3f, "dart"),
            Wave("pebblejaw", DioramaFamily.Saltscale, 49, 1.4f, .85f, "dart"),
            Wave("seaspark", DioramaFamily.Saltscale, 50, .9f, 1.35f, "dart"),
            Wave("foamtail", DioramaFamily.Saltscale, 51, 1.05f, 1.25f, "dart"),
            Wave("surfskipper", DioramaFamily.Saltscale, 52, 1.1f, 1.3f, "dart"),
            Wave("shoalglider", DioramaFamily.Saltscale, 53, 1.3f, .9f, "dart"),
            Wave("tidepip", DioramaFamily.Saltscale, 54, 1.2f, .95f, "dart"),
            Wave("pearlemperor", DioramaFamily.Saltscale, 55, 2f, .8f, "pearl", true),
            // GR-12 wave 4 (2026-10-24): the Gloamer and the Cometail, eight each (lively species quicker, calm ones slower).
            Wave("moonmouth", DioramaFamily.Gloamer, 56, 1.8f, .8f, "lure"),
            Wave("gloomfin", DioramaFamily.Gloamer, 57, 1.3f, 1.15f, "lure"),
            Wave("shadeback", DioramaFamily.Gloamer, 58, 1.7f, .85f, "lure"),
            Wave("hushray", DioramaFamily.Gloamer, 59, 1.9f, .75f, "lure"),
            Wave("duskdrifter", DioramaFamily.Gloamer, 60, 2f, .75f, "lure"),
            Wave("murkmaw", DioramaFamily.Gloamer, 61, 1.35f, 1.1f, "lure"),
            Wave("nightlantern", DioramaFamily.Gloamer, 62, 1.4f, 1.1f, "lure"),
            Wave("eclipseleviathan", DioramaFamily.Gloamer, 63, 2f, .75f, "eclipse", true),
            Wave("starmaw", DioramaFamily.Cometail, 64, 1.1f, 1.3f, "streak"),
            Wave("meteorback", DioramaFamily.Cometail, 65, 1.5f, .9f, "streak"),
            Wave("nebulafin", DioramaFamily.Cometail, 66, 1.6f, .85f, "streak"),
            Wave("orbitail", DioramaFamily.Cometail, 67, 1.15f, 1.25f, "streak"),
            Wave("quasarray", DioramaFamily.Cometail, 68, 1.55f, .9f, "streak"),
            Wave("novatusk", DioramaFamily.Cometail, 69, 1.2f, 1.2f, "streak"),
            Wave("cosmocarp", DioramaFamily.Cometail, 70, 1.7f, .8f, "streak"),
            Wave("celestialwyrm", DioramaFamily.Cometail, 71, 2f, .8f, "starfall", true),
        };

        /// <summary>The classic game's species, AD-B's five: the classic shop, its creature study and AD-B's route show only
        /// these (GR-12's species swim in the greybox at their GR size classes).</summary>
        public static readonly IReadOnlyList<DioramaSpecies> Classic = new List<DioramaSpecies>(All).GetRange(0, 5);

        public static bool TryGet(string id, out DioramaSpecies species)
        {
            foreach (var s in All) if (s.Id == id) { species = s; return true; }
            species = default;
            return false;
        }
    }

    /// <summary>The colour variant atlas the kit script writes (Resources/Art/DioramaKit/DioramaVariantAtlas.png): one texel per
    /// slot column and (species, variant) row, rows counted from the top, colours sRGB as authored.</summary>
    public static class DioramaVariants
    {
        public const string AtlasAsset = "DioramaVariantAtlas";
        public const int SpeciesCapacity = 72;
        /// <summary>Columns: the four species colour slots, the species' blush, and Shimmer's sheen (alpha = its strength).</summary>
        public static readonly string[] Slots = { "Body", "Fin", "Belly", "Accent", "Blush", "Sheen" };
        public const int Sheen = 5;

        static Color32[] pixels;
        static int width, height;

        /// <summary>Loads (once) the atlas; false while it is missing or not the expected size.</summary>
        public static bool Ready
        {
            get
            {
                if (pixels != null) return true;
                var atlas = Resources.Load<Texture2D>(DioramaLook.KitPath + AtlasAsset);
                if (!atlas || atlas.width != Slots.Length || atlas.height != SpeciesCapacity * 4 || !atlas.isReadable) return false;
                pixels = atlas.GetPixels32(); width = atlas.width; height = atlas.height;
                return true;
            }
        }

        /// <summary>The colour of one slot of a species in a variant (sRGB, alpha 1 except the sheen's strength).</summary>
        public static Color Color(int speciesIndex, DioramaVariant variant, int slot)
        {
            if (!Ready) throw new InvalidOperationException("Missing or malformed diorama variant atlas.");
            if (speciesIndex < 0 || speciesIndex >= SpeciesCapacity) throw new ArgumentOutOfRangeException(nameof(speciesIndex));
            if (slot < 0 || slot >= width) throw new ArgumentOutOfRangeException(nameof(slot));
            int row = speciesIndex * 4 + (int)variant;
            // GetPixels32 starts at the bottom row; the atlas counts its rows from the top.
            return pixels[(height - 1 - row) * width + slot];
        }

        /// <summary>The kit material slot of a species colour, such as DK_RibbonfinAccent.</summary>
        public static string SlotName(string speciesId, int slot) =>
            "DK_" + char.ToUpperInvariant(speciesId[0]) + speciesId.Substring(1) + Slots[slot];

        static Dictionary<string, (int species, int slot)> slotNames;

        /// <summary>Which species and slot a kit material slot name is, for a species' own five colour slots.</summary>
        public static bool TryParse(string slotName, out DioramaSpecies species, out int slot)
        {
            if (slotNames == null)
            {
                // Built once: every kit creature asks for each of its slots.
                var names = new Dictionary<string, (int, int)>();
                for (int i = 0; i < DioramaSpecies.All.Count; i++)
                    for (int k = 0; k < Sheen; k++) names[SlotName(DioramaSpecies.All[i].Id, k)] = (i, k);
                slotNames = names;
            }
            if (slotName != null && slotNames.TryGetValue(slotName, out var found))
            {
                species = DioramaSpecies.All[found.species]; slot = found.slot;
                return true;
            }
            species = default; slot = -1;
            return false;
        }

        /// <summary>For tests: forget the loaded atlas.</summary>
        public static void Reset() { pixels = null; }
    }

    /// <summary>A creature's trick or idle offsets, added to its swim pose by the container. Presentation only.</summary>
    public struct DioramaTrickPose
    {
        public float Pitch, Yaw, Roll, Lift, Puffed, Happy, Surprised, Glow, Tail, Fins;
        /// <summary>GR-12: the crest standing taller from its roots (0 at rest, 1 at its fullest): the Cinderling's ember spines
        /// flaring, the Reefback's corals blooming.</summary>
        public float Flare;
        /// <summary>A bubble, sparkle or puff the container releases this frame (crossing a beat).</summary>
        public bool Release;
    }

    /// <summary>AD.4's signature tricks (when the player lingers at a display) and idle habits, one each per species.
    /// Pure functions of time; Reduced motion plays neither. The Pondlet's bubble trick and nose-down bob are AD-A's. GR-12:
    /// a family's other species play its family's trick and habit at their own tempo, and each crown has a trick of its own.</summary>
    public static class DioramaTricks
    {
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        static float Bump(float t, float start, float end) => t <= start || t >= end ? 0 : Mathf.Sin((t - start) / (end - start) * Mathf.PI);
        static bool Crossed(float before, float now, float beat) => before < beat && now >= beat;

        public static float Duration(string species) => DioramaSpecies.TryGet(species, out var s) ? s.TrickSeconds : 0;

        /// <summary>The trick at an age (seconds); previous is the age last frame, for the beats it releases.</summary>
        public static DioramaTrickPose Trick(string species, float age, float previous = -1)
        {
            var p = new DioramaTrickPose();
            if (!DioramaSpecies.TryGet(species, out var s)) return p;
            float d = s.TrickSeconds;
            if (!(age >= 0) || age >= d || d <= 0) return p;
            float u = age / d, before = previous / d;
            switch (s.Signature)
            {
                case "balloon":
                    // Balloon belly: puffs up round, turns once on the spot, then lets the air out in a burp of bubbles.
                    p.Puffed = Ease(u / .33f) * (1 - Ease((u - .7f) / .3f));
                    p.Surprised = Bump(u, 0, .3f) * .6f;
                    p.Happy = Mathf.Clamp01((u - .2f) / .2f) * (1 - Ease((u - .85f) / .15f));
                    p.Yaw = Ease((u - .33f) / .37f) * 360;
                    p.Lift = p.Puffed * .03f;
                    p.Release = Crossed(previous / d, u, .72f);
                    break;
                case "twirl":
                    // Ribbon twirl: one barrel roll with its ribbons streaming.
                    p.Roll = Ease(u) * 360;
                    p.Tail = Bump(u, 0, 1) * 18;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(previous / d, u, .5f);
                    break;
                case "flash":
                    // Lantern flash: a surprised look, then the lantern flares twice with sparkles.
                    p.Surprised = Bump(u, 0, .3f) * .8f;
                    p.Glow = Bump(u, .15f, .45f) * 2.6f + Bump(u, .5f, .8f) * 2.6f;
                    p.Happy = Mathf.Clamp01((u - .3f) / .2f) * (1 - Ease((u - .85f) / .15f)) * .85f;
                    p.Pitch = -Bump(u, .1f, .9f) * 8;
                    p.Release = Crossed(previous / d, u, .3f) || Crossed(previous / d, u, .65f);
                    break;
                case "loop":
                    // Loop glide: sails wide open, a small vertical loop and back into the glide.
                    p.Fins = Bump(u, 0, 1) * 22;
                    p.Pitch = -Ease((u - .1f) / .75f) * 360;
                    p.Lift = Bump(u, .05f, .95f) * .02f;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(previous / d, u, .5f);
                    break;
                case "splash":
                {
                    // The Brookkin's: a happy hop that shakes the water off (three quick side-to-side rolls), chest puffed, with a
                    // splash of bubbles at the top.
                    float shake = Mathf.Sqrt(Mathf.Sin(u * Mathf.PI));
                    p.Roll = Mathf.Sin(u * Mathf.PI * 6) * 50 * shake;
                    p.Pitch = Bump(u, 0, .45f) * 18;
                    p.Puffed = Bump(u, 0, .5f) * .45f;
                    p.Lift = Bump(u, 0, 1) * .045f;
                    p.Tail = Bump(u, 0, 1) * 15;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .45f);
                    break;
                }
                case "twinkle":
                    // The Sparkline's: one turn on the spot while its lantern twinkles three times, sparkling on the last.
                    p.Yaw = Ease(u) * 360;
                    p.Glow = (Bump(u, .08f, .33f) + Bump(u, .38f, .63f) + Bump(u, .68f, .93f)) * 2.2f;
                    p.Surprised = Bump(u, 0, .25f) * .6f;
                    p.Happy = Mathf.Clamp01((u - .2f) / .2f) * (1 - Ease((u - .85f) / .15f));
                    p.Release = Crossed(before, u, .8f);
                    break;
                case "glide":
                    // The Airfin's: sails spread wide, it banks round in one circle on the spot and levels out, letting a puff
                    // of mist go halfway.
                    p.Fins = Bump(u, 0, 1) * 24;
                    p.Yaw = Ease(u) * 360;
                    p.Roll = Bump(u, .05f, .95f) * 32;
                    p.Lift = Bump(u, 0, 1) * .025f;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(before, u, .5f);
                    break;
                case "fountain":
                    // Brook Monarch: rises proud with its chest out, looks up, bows deeply to its audience swaying on its way,
                    // and lets a fountain of bubbles go three times.
                    p.Lift = Bump(u, 0, 1) * .05f;
                    p.Puffed = Bump(u, .05f, .95f) * .6f;
                    p.Pitch = Bump(u, 0, .3f) * 10 - Bump(u, .3f, .8f) * 30;
                    p.Roll = Mathf.Sin(u * Mathf.PI * 2) * 20;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .35f) || Crossed(before, u, .55f) || Crossed(before, u, .75f);
                    break;
                case "aurora":
                    // Aurorafin: a slow barrel roll, sails wide, while its three lanterns glow in waves, sparkling twice.
                    p.Roll = Ease(u) * 360;
                    p.Glow = (1.2f + Mathf.Sin(u * Mathf.PI * 4)) * Bump(u, 0, 1) * 1.4f;
                    p.Fins = Bump(u, 0, 1) * 20;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(before, u, .35f) || Crossed(before, u, .7f);
                    break;
                case "gale":
                    // Zephyray: two great wing beats, each a gust, then a rising spiral of two turns banked into the wind.
                    p.Fins = (Bump(u, 0, .17f) + Bump(u, .17f, .34f)) * 34;
                    p.Yaw = Ease((u - .34f) / .6f) * 720;
                    p.Roll = Bump(u, .34f, .96f) * 28;
                    p.Lift = Bump(u, .3f, 1) * .05f;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(before, u, .08f) || Crossed(before, u, .25f);
                    break;
                case "flare":
                    // The Cinderling's: it rears up with a start, its ember spines stand tall and blaze, its tail flicks like a
                    // flame, and a puff of sparks flies from its spines.
                    p.Flare = Ease(u / .25f) * (1 - Ease((u - .7f) / .3f));
                    p.Glow = p.Flare * 2.8f;
                    p.Surprised = Bump(u, 0, .3f) * .6f;
                    p.Happy = Mathf.Clamp01((u - .25f) / .2f) * (1 - Ease((u - .85f) / .15f));
                    p.Pitch = Bump(u, 0, .55f) * 12;
                    p.Puffed = Bump(u, .05f, .6f) * .3f;
                    p.Tail = Mathf.Sin(u * Mathf.PI * 4) * 26 * Bump(u, .3f, 1);
                    p.Release = Crossed(before, u, .5f);
                    break;
                case "sprinkle":
                {
                    // The Dewling's: it gathers itself round, then shakes itself from nose to tail, its dew beads scattering a
                    // shimmer of droplets, and settles with a happy look.
                    float shake = Bump(u, .15f, .85f);
                    p.Puffed = Bump(u, 0, .6f) * .45f;
                    p.Roll = Mathf.Sin(u * Mathf.PI * 14) * 50 * shake;
                    p.Yaw = Mathf.Sin(u * Mathf.PI * 7) * 18 * shake;
                    p.Pitch = Bump(u, .1f, .9f) * 12;
                    p.Lift = Bump(u, .1f, .9f) * .02f;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .55f);
                    break;
                }
                case "rebirth":
                    // Phoenix Fry: its flames blaze up and it somersaults forward through them, rising, bursting into sparks as it
                    // goes over and again as it comes round, reborn.
                    p.Flare = Ease(u / .2f) * (1 - Ease((u - .8f) / .2f));
                    p.Glow = p.Flare * 3f;
                    p.Pitch = -Ease((u - .2f) / .6f) * 360;
                    p.Lift = Bump(u, .1f, .95f) * .05f;
                    p.Puffed = Bump(u, 0, .25f) * .4f + Bump(u, .8f, 1) * .3f;
                    p.Fins = Bump(u, .1f, .9f) * 22;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .22f) || Crossed(before, u, .85f);
                    break;
                case "bloom":
                    // The Reefback's: it settles with a slow bow, its corals swaying open, rocks gently as it turns to show its
                    // reef, and puffs a glittering plankton cloud from it.
                    p.Flare = Ease(u / .3f) * (1 - Ease((u - .7f) / .3f));
                    p.Pitch = -Bump(u, 0, .6f) * 14;
                    p.Yaw = Mathf.Sin(u * Mathf.PI * 2) * 38;
                    p.Roll = Mathf.Sin(u * Mathf.PI * 4) * 10 * Bump(u, 0, 1);
                    p.Puffed = Bump(u, .1f, .7f) * .5f;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .55f);
                    break;
                case "dart":
                    // The Saltscale's: a quick dart and turn, banking, its salt stripe flashing silver, with a sparkle of salt.
                    p.Yaw = Ease(u / .8f) * 360;
                    p.Roll = Bump(u, .05f, .85f) * 30;
                    p.Glow = Bump(u, .25f, .75f) * 2.6f;
                    p.Pitch = Bump(u, 0, .4f) * 6;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(before, u, .5f);
                    break;
                case "reef":
                    // Coralossus: its whole reef blooms wide as it rises and turns slowly once to show it, letting plankton go
                    // three times, and settles with a bow.
                    p.Flare = Ease(u / .25f) * (1 - Ease((u - .8f) / .2f));
                    p.Yaw = Ease((u - .05f) / .8f) * 360;
                    p.Lift = Bump(u, 0, .9f) * .04f;
                    p.Pitch = -Bump(u, .75f, 1) * 12;
                    p.Puffed = Bump(u, 0, .6f) * .4f;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .35f) || Crossed(before, u, .55f) || Crossed(before, u, .75f);
                    break;
                case "pearl":
                    // Pearl Emperor: two royal darts round, fins spread and its stripe flashing silver on each, a sparkle of
                    // pearls each time.
                    p.Yaw = Ease(u / .9f) * 720;
                    p.Fins = Bump(u, 0, 1) * 22;
                    p.Roll = Bump(u, .05f, .9f) * 20;
                    p.Glow = (Bump(u, .1f, .45f) + Bump(u, .5f, .85f)) * 2.4f;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .3f) || Crossed(before, u, .7f);
                    break;
                case "lure":
                    // The Gloamer's: its lights go dim, then its lure pulses brighter and brighter as it bows to show it, turning
                    // gently, and lets a slow drift of glowing motes go.
                    p.Glow = -.8f * Bump(u, 0, .35f) + Ease((u - .25f) / .5f) * (1 - Ease((u - .85f) / .15f)) * (1.2f + 1.6f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5)));
                    p.Pitch = -Bump(u, .1f, .9f) * 10;
                    p.Yaw = Mathf.Sin(u * Mathf.PI * 2) * 22;
                    p.Fins = Bump(u, .2f, .9f) * 12;
                    p.Happy = Bump(u, .3f, 1);
                    p.Release = Crossed(before, u, .7f);
                    break;
                case "streak":
                    // The Cometail's: it streaks forward round a loop, rising and banking as its head and tail blaze, leaving a
                    // sparkling trail of stardust.
                    p.Yaw = Ease(u / .85f) * 360;
                    p.Roll = Bump(u, .05f, .9f) * 38;
                    p.Pitch = -Mathf.Sin(u * Mathf.PI * 2) * 14 * Bump(u, 0, 1);
                    p.Lift = Bump(u, 0, 1) * .03f;
                    p.Glow = Bump(u, .05f, .95f) * 2.6f;
                    p.Tail = Mathf.Sin(u * Mathf.PI * 8) * 16 * Bump(u, 0, 1);
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .3f) || Crossed(before, u, .55f) || Crossed(before, u, .8f);
                    break;
                case "eclipse":
                    // Eclipse Leviathan: its lights all go dark as it rises and turns slowly once, then the corona round its lure
                    // blazes, twice, letting motes go each time.
                    p.Glow = -Bump(u, 0, .4f) + (Bump(u, .35f, .65f) + Bump(u, .65f, .95f)) * 3f;
                    p.Yaw = Ease((u - .05f) / .85f) * 360;
                    p.Lift = Bump(u, 0, .95f) * .02f;
                    p.Fins = Bump(u, .1f, .95f) * 20;
                    p.Puffed = Bump(u, .35f, .65f) * .3f;
                    p.Happy = Bump(u, .3f, 1);
                    p.Release = Crossed(before, u, .5f) || Crossed(before, u, .8f);
                    break;
                case "starfall":
                    // Celestial Wyrm: it rises and spirals round twice, its head and tail blazing, stardust falling three times.
                    p.Yaw = Ease(u / .9f) * 720;
                    p.Roll = Bump(u, .05f, .95f) * 26;
                    p.Lift = Bump(u, 0, 1) * .045f;
                    p.Pitch = -Bump(u, 0, .5f) * 10 + Bump(u, .5f, 1) * 8;
                    p.Glow = Bump(u, .05f, .95f) * 2.4f + Bump(u, .4f, .6f) * .8f;
                    p.Tail = Mathf.Sin(u * Mathf.PI * 6) * 14 * Bump(u, 0, 1);
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .3f) || Crossed(before, u, .55f) || Crossed(before, u, .8f);
                    break;
                case "rainbow":
                    // Rainbow Regent: rises and turns slowly once like a royal display, fins spread wide and its rainbow tail
                    // sweeping, spraying droplets three times.
                    p.Yaw = Ease((u - .05f) / .85f) * 360;
                    p.Lift = Bump(u, 0, 1) * .045f;
                    p.Fins = Bump(u, 0, 1) * 22;
                    p.Tail = Mathf.Sin(u * Mathf.PI * 6) * 14 * Bump(u, 0, 1);
                    p.Roll = Bump(u, .1f, .9f) * 12;
                    p.Happy = Bump(u, 0, 1);
                    p.Release = Crossed(before, u, .3f) || Crossed(before, u, .55f) || Crossed(before, u, .8f);
                    break;
            }
            return p;
        }

        /// <summary>GR-12: the Pondlet's bubble as a pose for the greybox, where no PondletGlance plays it (the classic shop plays
        /// AD-A's glance instead): a surprised look up, a little nose-up and one bubble from its lips.</summary>
        public static DioramaTrickPose PondletBubble(float age, float previous = -1)
        {
            var p = new DioramaTrickPose();
            float d = Duration("pondlet");
            if (!(age >= 0) || age >= d || d <= 0) return p;
            float u = age / d;
            p.Surprised = Bump(u, 0, .5f) * .7f;
            p.Happy = Bump(u, .3f, 1) * .9f;
            p.Pitch = Bump(u, 0, 1) * 10;
            p.Tail = Bump(u, 0, 1) * 12;
            p.Release = Crossed(previous / d, u, .45f);
            return p;
        }

        /// <summary>GR-12: how many bubbles, sparkles or puffs a species' trick releases (the AD-A Pondlet's one bubble too).</summary>
        public static int Releases(string species)
        {
            if (!DioramaSpecies.TryGet(species, out var s)) return 0;
            switch (s.Signature)
            {
                case "flash": case "aurora": case "gale": case "rebirth": case "pearl": case "eclipse": return 2;
                case "fountain": case "rainbow": case "reef": case "streak": case "starfall": return 3;
                default: return 1;
            }
        }

        /// <summary>How far a trick has drawn the creature to mid-water (0..1): in over its first 0.3 s, out over its last.</summary>
        public static float Settle(string species, float age)
        {
            float d = Duration(species);
            if (!(age >= 0) || age >= d) return 0;
            return Ease(Mathf.Min(age, d - age) / .3f);
        }

        static readonly Dictionary<string, float> maxFlare = new Dictionary<string, float>();

        /// <summary>GR-12: the most a species' crest ever stands taller (its Flare) in its trick or its habit, so its swim room can
        /// leave room for it (a Cinderling's spines, a Reefback's corals); 0 for a crest that never does.</summary>
        public static float MaxFlare(string species)
        {
            if (species == null) return 0;
            if (maxFlare.TryGetValue(species, out float known)) return known;
            float most = 0, d = Duration(species);
            for (int i = 0; i <= 200 && d > 0; i++) most = Mathf.Max(most, Trick(species, d * i / 200f).Flare);
            for (float t = 0; t < 30; t += 1 / 20f) most = Mathf.Max(most, Idle(species, t, 0).Flare);
            return maxFlare[species] = most;
        }

        /// <summary>The idle habit at a clock (seconds of the creature's own clock) and its identity phase; previous is the
        /// clock last frame, for the beats it releases.</summary>
        public static DioramaTrickPose Idle(string species, float clock, float phase, float previous = -1)
        {
            var p = new DioramaTrickPose();
            if (!(clock >= 0) || float.IsInfinity(clock)) return p;
            float t = clock + phase;
            if (DioramaSpecies.TryGet(species, out var s) && s.Index >= 5)
            {
                // GR-12: the family's habit at the species' own tempo.
                float tempo = s.Tempo;
                switch (s.Family)
                {
                    case DioramaFamily.Brookkin:
                    {
                        // A happy wag every few seconds, rolling a little with it.
                        float age = Mathf.Repeat(t * tempo, 6f);
                        float wag = Bump(age, 0, 1.1f);
                        p.Tail = Mathf.Sin(age * 12) * 13 * wag; p.Roll = Mathf.Sin(age * 6) * 5 * wag; p.Happy = wag * .4f;
                        break;
                    }
                    case DioramaFamily.Sparkline:
                        // The lantern breathes at its own pace, and now and then gives a dim flicker.
                        p.Glow = .5f + .5f * Mathf.Sin(t * 1.6f * tempo) - Bump(Mathf.Repeat(t * tempo, 8f), 0, .25f) * .7f;
                        break;
                    case DioramaFamily.Airfin:
                        // Hovering: sails held out with a slow tilt.
                        p.Fins = 6 + 4 * Mathf.Sin(t * .9f * tempo); p.Roll = Mathf.Sin(t * .7f * tempo) * 6;
                        break;
                    case DioramaFamily.Cinderling:
                    {
                        // Smouldering: its embers pulse slowly, and every few seconds its spines bristle with a brighter glow.
                        float age = Mathf.Repeat(t * tempo, 7f);
                        float bristle = Bump(age, 0, .9f);
                        p.Glow = .3f + .3f * Mathf.Sin(t * 1.1f * tempo) + bristle * 1.2f;
                        p.Flare = bristle * .45f;
                        break;
                    }
                    case DioramaFamily.Dewling:
                    {
                        // A gentle bob, and every few seconds a little puff as if gathering a drop.
                        float age = Mathf.Repeat(t * tempo, 5f);
                        float gather = Bump(age, 0, 1.2f);
                        p.Lift = (.5f + .5f * Mathf.Sin(t * 1.8f * tempo)) * .008f; p.Puffed = gather * .3f; p.Happy = gather * .3f;
                        break;
                    }
                    case DioramaFamily.Reefback:
                    {
                        // Grazing: a slow nod now and then, its corals breathing open and closed.
                        float age = Mathf.Repeat(t * tempo, 6f);
                        p.Pitch = -Bump(age, 0, 1.6f) * 7;
                        p.Flare = .12f + .12f * Mathf.Sin(t * .8f * tempo);
                        break;
                    }
                    case DioramaFamily.Saltscale:
                    {
                        // Its stripe glitters softly, flickering bright now and then, and every few seconds it glances aside.
                        float age = Mathf.Repeat(t * tempo, 5f);
                        p.Glow = -.45f + Bump(age, 0, .3f) * .9f;
                        p.Yaw = Bump(Mathf.Repeat(t * tempo + 2.5f, 6f), 0, .5f) * 10;
                        break;
                    }
                    case DioramaFamily.Gloamer:
                    {
                        // Its lights breathe slowly, deep and dim, and every few seconds its lure gives a brighter wink; it hangs
                        // in the water, nose a little down.
                        float age = Mathf.Repeat(t * tempo, 7f);
                        p.Glow = -.2f + .35f * Mathf.Sin(t * .7f * tempo) + Bump(age, 0, .5f) * 1.1f;
                        p.Pitch = -3 - 2 * Mathf.Sin(t * .5f * tempo);
                        break;
                    }
                    case DioramaFamily.Cometail:
                    {
                        // Its head and tail glow bright, flickering like a comet's, and every few seconds its tail streams out
                        // with a flick.
                        float age = Mathf.Repeat(t * tempo, 5f);
                        float flick = Bump(age, 0, .8f);
                        p.Glow = .35f + .25f * Mathf.Sin(t * 2.4f * tempo) + flick * .7f;
                        p.Tail = Mathf.Sin(age * 14) * 14 * flick;
                        break;
                    }
                }
                return p;
            }
            switch (species)
            {
                case "bubblebelly":
                {
                    // A hiccup every 7 s: a quick little puff and hop, and one bubble.
                    float age = Mathf.Repeat(t, 7f);
                    float hic = Bump(age, 0, .35f);
                    p.Puffed = hic * .45f; p.Lift = hic * .02f; p.Surprised = hic * .35f;
                    float before = Mathf.Repeat(previous + phase, 7f);
                    p.Release = previous >= 0 && clock - previous < 1 && before < .15f && age >= .15f;
                    break;
                }
                case "ribbonfin":
                {
                    // A slow ribbon wave every 6 s, rolling a little with it.
                    float age = Mathf.Repeat(t, 6f);
                    float wave = Bump(age, 0, 1.2f);
                    p.Tail = Mathf.Sin(age * 10) * 14 * wave; p.Roll = Mathf.Sin(age * 5) * 10 * wave;
                    break;
                }
                case "lanternfin":
                    // The lantern breathes, and every 8 s gives a dim flicker.
                    p.Glow = .5f + .5f * Mathf.Sin(t * 1.6f) - Bump(Mathf.Repeat(t, 8f), 0, .25f) * .7f;
                    break;
                case "driftwing":
                    // Hovering: sails held out with a slow tilt.
                    p.Fins = 6 + 4 * Mathf.Sin(t * .9f); p.Roll = Mathf.Sin(t * .7f) * 6;
                    break;
            }
            return p;
        }
    }

    public static class DioramaWater
    {
        /// <summary>AD.4's family water tints (glass base colour, alpha its strength), so no species blends into its water:
        /// a periwinkle stream for the Brookkin (coral, yellow and teal), a green lagoon for the Sparkline's indigo and gold, warm
        /// sand for the lilac Airfin; GR-12: a cool blue for the ember-warm Cinderling, a mint pool for the pastel Dewling, a deep
        /// sea teal for the Reefback's corals and shells, warm coral shallows for the blue and silver Saltscale, deep twilight
        /// indigo for the dark Gloamer and its glowing lures, a warm violet sky for the bright-headed Cometail.</summary>
        public static Color Tint(DioramaFamily family) =>
            family == DioramaFamily.Sparkline ? new Color(.2f, .74f, .58f, .13f) :
            family == DioramaFamily.Airfin ? new Color(1f, .8f, .55f, .13f) :
            family == DioramaFamily.Cinderling ? new Color(.3f, .62f, .85f, .13f) :
            family == DioramaFamily.Dewling ? new Color(.55f, .85f, .6f, .13f) :
            family == DioramaFamily.Reefback ? new Color(.05f, .6f, .75f, .13f) :
            family == DioramaFamily.Saltscale ? new Color(.98f, .66f, .8f, .13f) :
            family == DioramaFamily.Gloamer ? new Color(.35f, .3f, .75f, .13f) :
            family == DioramaFamily.Cometail ? new Color(.75f, .35f, .85f, .13f) : new Color(.55f, .62f, .98f, .1f);

        /// <summary>The incubator fluid for a family (more opaque, since the creature grows inside it).</summary>
        /// <summary>How much more opaque the water's far side (behind the creature, and the sand) is than its near side, so the
        /// family tint reads around the creature without veiling it.</summary>
        public const float FarSide = 3f;
        /// <summary>The night glow of the water and the incubator fluid, as a share of the tint's colour.</summary>
        public const float GlowShare = .6f, FluidGlowShare = .26f;

        public static Color Fluid(DioramaFamily family) =>
            family == DioramaFamily.Sparkline ? new Color(.25f, .88f, .55f, .2f) :
            family == DioramaFamily.Airfin ? new Color(1f, .86f, .66f, .2f) :
            family == DioramaFamily.Cinderling ? new Color(.4f, .72f, .95f, .2f) :
            family == DioramaFamily.Dewling ? new Color(.62f, .92f, .66f, .2f) :
            family == DioramaFamily.Reefback ? new Color(.1f, .7f, .85f, .2f) :
            family == DioramaFamily.Saltscale ? new Color(1f, .75f, .85f, .2f) :
            family == DioramaFamily.Gloamer ? new Color(.45f, .4f, .85f, .2f) :
            family == DioramaFamily.Cometail ? new Color(.85f, .45f, .95f, .2f) : new Color(.35f, .85f, .9f, .2f);
    }
}
