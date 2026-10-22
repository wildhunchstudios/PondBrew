using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>AD-B creature families. Each family shares one rig (the same pivots, face parts and face shape keys); its
    /// species differ by body, fins, colours and the family's signature part (Sparkline: the lantern).</summary>
    public enum DioramaFamily { Brookkin, Sparkline, Airfin }

    /// <summary>AD.3's four colour variants. Colour swaps only; which one a specimen is comes with AD-F.</summary>
    public enum DioramaVariant { Classic, Sunny, Deep, Shimmer }

    /// <summary>One kit species: its model, family, place in the variant atlas and how big containers show it.</summary>
    public readonly struct DioramaSpecies
    {
        public readonly string Id, Kit, Signature;
        public readonly DioramaFamily Family;
        /// <summary>Row block in the variant atlas (rows Index * 4 to Index * 4 + 3), fixed forever as species are added.</summary>
        public readonly int Index;
        public readonly float ModelScale, TankScale, IncubatorScale, SwimAmplitude, TrickSeconds;

        DioramaSpecies(string id, string kit, DioramaFamily family, int index, float model, float tank, float incubator,
            float swim, float trick, string signature)
        {
            Id = id; Kit = kit; Family = family; Index = index; ModelScale = model; TankScale = tank; IncubatorScale = incubator;
            SwimAmplitude = swim; TrickSeconds = trick; Signature = signature;
        }

        /// <summary>The five existing species. Scales are on top of each container's classic size; the AD-A and AD-B routes
        /// check every one stays inside its glass while it swims and does its trick.</summary>
        public static readonly IReadOnlyList<DioramaSpecies> All = new[]
        {
            new DioramaSpecies("pondlet", "DioramaPondlet", DioramaFamily.Brookkin, 0, .9f, 1.6f, 1.5f, .82f, .6f, "bubble"),
            new DioramaSpecies("bubblebelly", "DioramaBubblebelly", DioramaFamily.Brookkin, 1, .9f, 1.2f, 1.2f, .7f, 1.8f, "balloon"),
            new DioramaSpecies("ribbonfin", "DioramaRibbonfin", DioramaFamily.Brookkin, 2, .9f, 1.25f, 1.2f, 1.6f, 1.4f, "twirl"),
            new DioramaSpecies("lanternfin", "DioramaLanternfin", DioramaFamily.Sparkline, 3, .9f, 1.3f, 1.25f, .75f, 1.2f, "flash"),
            new DioramaSpecies("driftwing", "DioramaDriftwing", DioramaFamily.Airfin, 4, .9f, 1.2f, 1.15f, .7f, 1.8f, "loop"),
        };

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
        public const int SpeciesCapacity = 24;
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

        /// <summary>Which species and slot a kit material slot name is, for a species' own five colour slots.</summary>
        public static bool TryParse(string slotName, out DioramaSpecies species, out int slot)
        {
            foreach (var s in DioramaSpecies.All)
                for (int k = 0; k < Sheen; k++)
                    if (SlotName(s.Id, k) == slotName) { species = s; slot = k; return true; }
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
        /// <summary>A bubble, sparkle or puff the container releases this frame (crossing a beat).</summary>
        public bool Release;
    }

    /// <summary>AD.4's signature tricks (when the player lingers at a display) and idle habits, one each per species.
    /// Pure functions of time; Reduced motion plays neither. The Pondlet's bubble trick and nose-down bob are AD-A's.</summary>
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
            float d = Duration(species);
            if (!(age >= 0) || age >= d || d <= 0) return p;
            float u = age / d;
            switch (species)
            {
                case "bubblebelly":
                    // Balloon belly: puffs up round, turns once on the spot, then lets the air out in a burp of bubbles.
                    p.Puffed = Ease(u / .33f) * (1 - Ease((u - .7f) / .3f));
                    p.Surprised = Bump(u, 0, .3f) * .6f;
                    p.Happy = Mathf.Clamp01((u - .2f) / .2f) * (1 - Ease((u - .85f) / .15f));
                    p.Yaw = Ease((u - .33f) / .37f) * 360;
                    p.Lift = p.Puffed * .03f;
                    p.Release = Crossed(previous / d, u, .72f);
                    break;
                case "ribbonfin":
                    // Ribbon twirl: one barrel roll with its ribbons streaming.
                    p.Roll = Ease(u) * 360;
                    p.Tail = Bump(u, 0, 1) * 18;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(previous / d, u, .5f);
                    break;
                case "lanternfin":
                    // Lantern flash: a surprised look, then the lantern flares twice with sparkles.
                    p.Surprised = Bump(u, 0, .3f) * .8f;
                    p.Glow = Bump(u, .15f, .45f) * 2.6f + Bump(u, .5f, .8f) * 2.6f;
                    p.Happy = Mathf.Clamp01((u - .3f) / .2f) * (1 - Ease((u - .85f) / .15f)) * .85f;
                    p.Pitch = -Bump(u, .1f, .9f) * 8;
                    p.Release = Crossed(previous / d, u, .3f) || Crossed(previous / d, u, .65f);
                    break;
                case "driftwing":
                    // Loop glide: sails wide open, a small vertical loop and back into the glide.
                    p.Fins = Bump(u, 0, 1) * 22;
                    p.Pitch = -Ease((u - .1f) / .75f) * 360;
                    p.Lift = Bump(u, .05f, .95f) * .02f;
                    p.Happy = Bump(u, 0, 1) * .9f;
                    p.Release = Crossed(previous / d, u, .5f);
                    break;
            }
            return p;
        }

        /// <summary>How far a trick has drawn the creature to mid-water (0..1): in over its first 0.3 s, out over its last.</summary>
        public static float Settle(string species, float age)
        {
            float d = Duration(species);
            if (!(age >= 0) || age >= d) return 0;
            return Ease(Mathf.Min(age, d - age) / .3f);
        }

        /// <summary>The idle habit at a clock (seconds of the creature's own clock) and its identity phase; previous is the
        /// clock last frame, for the beats it releases.</summary>
        public static DioramaTrickPose Idle(string species, float clock, float phase, float previous = -1)
        {
            var p = new DioramaTrickPose();
            if (!(clock >= 0) || float.IsInfinity(clock)) return p;
            float t = clock + phase;
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
        /// sand for the lilac Airfin.</summary>
        public static Color Tint(DioramaFamily family) =>
            family == DioramaFamily.Sparkline ? new Color(.2f, .74f, .58f, .13f) :
            family == DioramaFamily.Airfin ? new Color(1f, .8f, .55f, .13f) : new Color(.55f, .62f, .98f, .1f);

        /// <summary>The incubator fluid for a family (more opaque, since the creature grows inside it).</summary>
        /// <summary>How much more opaque the water's far side (behind the creature, and the sand) is than its near side, so the
        /// family tint reads around the creature without veiling it.</summary>
        public const float FarSide = 3f;
        /// <summary>The night glow of the water and the incubator fluid, as a share of the tint's colour.</summary>
        public const float GlowShare = .6f, FluidGlowShare = .26f;

        public static Color Fluid(DioramaFamily family) =>
            family == DioramaFamily.Sparkline ? new Color(.25f, .88f, .55f, .2f) :
            family == DioramaFamily.Airfin ? new Color(1f, .86f, .66f, .2f) : new Color(.35f, .85f, .9f, .2f);
    }
}
