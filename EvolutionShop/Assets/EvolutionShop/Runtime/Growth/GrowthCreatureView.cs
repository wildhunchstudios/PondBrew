using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// A creature at its size class (GR.5: a small one is 6-9 cm, a medium 12-18, a large 25-40). GR-12: a species the diorama
    /// kit has (waves 1 to 4: all nine families) is its kit model in its colour from the variant atlas, its body set
    /// to the class's length, with its family's strokes and habit at its own tempo, blinks and pupils on the player, and its
    /// trick when the player lingers on it in peek. Any other is GR-02's placeholder, with a head 1.3 times the usual size for
    /// small species (GR.14 readability), eyes, a tail and a fin, coloured from its recipe's cultures, with (GR-03) its
    /// family's feature: dew beads, spikes, a stripe, a shell, a lure or a comet tail. Creatures cast no shadows (GR.5's
    /// budget: up to 180 on screen).
    /// </summary>
    public sealed class GrowthCreatureView : MonoBehaviour
    {
        public int SpecimenId { get; private set; }
        public string Species { get; private set; }
        public float Length { get; private set; }
        public bool Reserved
        {
            get => reserved;
            set { if (reserved == value) return; reserved = value; if (reservedTag) reservedTag.gameObject.SetActive(value); }
        }
        /// <summary>Peek enlarges the creature under the reticle (GR-02: 1.35 times, for readability), as far as its tank's water
        /// lets it be enlarged with its trick at its widest (GR-12: less for a large crown in a Large tank; FitTo).</summary>
        public bool Highlighted
        {
            get => highlighted;
            set { highlighted = value; transform.localScale = Vector3.one * (value ? peekScale : 1f); }
        }
        public const float PeekGrowth = 1.35f;
        float peekScale = PeekGrowth;
        /// <summary>How far peek enlarges it in its tank (1 to PeekGrowth).</summary>
        public float PeekScale => peekScale;
        /// <summary>GR-12: the farthest it reaches across the water and above or below its centre through its trick and habit
        /// (turns, rolls, bows, lifts, its crest at each moment's flare), at scale 1; a placeholder's are its Reach and HalfHeight.</summary>
        public float TrickReach { get; private set; }
        public float TrickHalf { get; private set; }

        /// <summary>Fits its peek size to its tank's water (inner size): enlarged no further than its trick, at its widest and in
        /// mid-water, stays inside.</summary>
        public void FitTo(Vector3 inner)
        {
            float fit = Mathf.Min(PeekGrowth, (inner.y * 0.5f - 0.03f) / Mathf.Max(1e-4f, TrickHalf), (Mathf.Min(inner.x, inner.z) * 0.5f - 0.03f) / Mathf.Max(1e-4f, TrickReach));
            fit = Mathf.Max(1, fit);
            if (fit == peekScale) return;
            peekScale = fit;
            if (highlighted) transform.localScale = Vector3.one * peekScale;
        }
        Transform tail, reservedTag;
        bool reserved, highlighted;
        float wag;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        // GR-12: the kit creature, its holder (turned to face +Z, scaled to the size class) and its clocks; its distant look.
        CreatureVisual kit;
        Transform kitHolder;
        GameObject far;
        int detail;
        /// <summary>Each species' distant look in each colour, baked once and shared by every specimen of it.</summary>
        static readonly Dictionary<string, (Mesh mesh, Material[] outlined, Material[] plain)> farLooks = new Dictionary<string, (Mesh, Material[], Material[])>();
        DioramaFamily family;
        float phase, clock, lastTick = -1, focus, trickAge = -1;
        /// <summary>When its rest after a trick ends, on the shop's clock (the t it is ticked with), so the rest runs on while its
        /// tank is far away and not animated.</summary>
        float restUntil = float.NegativeInfinity;
        /// <summary>The kit model's turn to swim forward (+Z in the greybox; the kit faces -Z).</summary>
        public static readonly Quaternion KitTurn = Quaternion.Euler(0, 180, 0);
        /// <summary>Seconds on a creature in peek before it plays its trick, and the rest after one (AD-B's linger is 1.5 s at
        /// a display; a peek is already a close look).</summary>
        public const float TrickLinger = .8f, TrickRest = 6f;
        /// <summary>How far a creature's puffed body, swaying crest and lantern reach past its sampled poses (TrickReach, TrickHalf).</summary>
        const float TrickSwing = 1.08f;
        /// <summary>Each species' Reach, HalfHeight, TrickReach and TrickHalf (x, y, z, w), sampled once.</summary>
        static readonly Dictionary<string, Vector4> envelopes = new Dictionary<string, Vector4>();
        /// <summary>A trick's lift is in the classic display's units; in the greybox it is this share of the creature's length.</summary>
        public const float LiftShare = 1.6f;

        /// <summary>GR-12: the kit creature showing this specimen; null for a placeholder.</summary>
        public CreatureVisual Kit => kit;
        /// <summary>The farthest any part reaches from the creature's centre across the water, and above or below it, at scale 1:
        /// the swim keeps both inside the glass (a placeholder's are GR-02's 0.6 and 0.4 of its length).</summary>
        public float Reach { get; private set; }
        public float HalfHeight { get; private set; }
        /// <summary>The trick's age in seconds while it plays, else -1.</summary>
        public float TrickAge => trickAge;
        /// <summary>How far a trick has drawn the creature to mid-water (0..1).</summary>
        public float Settle => trickAge >= 0 && kit != null ? DioramaTricks.Settle(Species, trickAge) : 0;
        /// <summary>The look it is shown in: 0 the full kit creature (near, in view), 1 its distant look with its ink outline, 2
        /// without (beyond 6 m or out of view). GrowthWorld picks it by distance (KitDistance, FullDistance).</summary>
        public int Detail => detail;
        /// <summary>Its distant look while it is shown, else null.</summary>
        public Renderer FarRenderer => far != null && far.activeSelf ? far.GetComponent<Renderer>() : null;
        /// <summary>Validation: tricks started and bursts released by every creature so far.</summary>
        public static int TricksPlayed { get; private set; }
        public static int BurstsReleased { get; private set; }
        /// <summary>Validation: the bursts this creature has released (its tricks' and its habit's).</summary>
        public int Bursts { get; private set; }
        /// <summary>Where the player's eyes are; kit creatures' pupils follow it while it is set.</summary>
        public static Vector3 Viewer;
        public static bool ViewerSet;

        public static float LengthOf(SizeClass size) => size == SizeClass.Small ? 0.075f : size == SizeClass.Medium ? 0.15f : 0.32f;
        /// <summary>GR-12: crowns are grander, at the top of their size class (9, 18 and 38 cm).</summary>
        public const float CrownLength = 1.2f;
        public static float LengthOf(SpeciesDef def) => LengthOf(def.Size) * (def.Kind == SpeciesKind.Crown ? CrownLength : 1f);

        static DioramaVariant Variant(Colour colour) =>
            colour == Colour.Sunny ? DioramaVariant.Sunny : colour == Colour.Deep ? DioramaVariant.Deep :
            colour == Colour.Shimmer ? DioramaVariant.Shimmer : DioramaVariant.Classic;

        /// <summary>GR-12: a kit species' body colour in a variant, from the atlas, so cards and the journal match the creature.</summary>
        static bool KitColour(SpeciesDef s, Colour colour, out Color c)
        {
            c = default;
            if (s == null || !DioramaSpecies.TryGet(s.Id, out var spec) || !DioramaVariants.Ready) return false;
            c = DioramaVariants.Color(spec.Index, Variant(colour), 0); c.a = 1;
            return true;
        }

        /// <summary>The species' colour: a kit species' Classic body from the atlas (GR-12); otherwise its recipe's cultures mixed
        /// by dose, or its family's lead culture shaded by its breeding value.</summary>
        public static Color ColourOf(SpeciesDef s)
        {
            if (KitColour(s, Colour.Classic, out var kitColour)) return kitColour;
            if (s.Recipe != null)
            {
                Color sum = Color.black; int doses = 0;
                for (int i = 0; i < s.Recipe.Length; i++)
                {
                    if (s.Recipe[i] == 0) continue;
                    ColorUtility.TryParseHtmlString(GrowthContent.Cultures[i].Hex, out var c);
                    sum += c * s.Recipe[i]; doses += s.Recipe[i];
                }
                if (doses == 0) return Color.grey;
                // Averaged cultures go muddy (red and blue make grey), so the mix keeps a clear hue: more saturated, never dark.
                Color.RGBToHSV(sum / doses, out float mh, out float ms, out float mv);
                return Color.HSVToRGB(mh, Mathf.Clamp01(ms * 1.7f + 0.12f), Mathf.Max(mv, 0.72f));
            }
            var lead = GrowthContent.Cultures[GrowthContent.CultureIndex(GrowthContent.Family(s.Family).LeadCulture)];
            ColorUtility.TryParseHtmlString(lead.Hex, out var l);
            Color.RGBToHSV(l, out float h, out float sat, out float v);
            return Color.HSVToRGB(Mathf.Repeat(h + (s.BreedingValue % 7 - 3) * 0.025f, 1), sat * 0.9f, Mathf.Clamp01(v * 0.95f));
        }

        /// <summary>
        /// GR.7.2's four colours on the species' own (placeholders for AD.3's palettes): Classic as it is, Sunny warmer and
        /// brighter, Deep darker and richer, Shimmer pale and softly glowing.
        /// </summary>
        public static Color ColourOf(SpeciesDef s, Colour colour)
        {
            if (KitColour(s, colour, out var kitColour)) return kitColour;
            var c = ColourOf(s);
            if (colour == Colour.Classic) return c;
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            float Toward(float degrees, float t) => Mathf.Repeat(Mathf.LerpAngle(h * 360f, degrees, t), 360f) / 360f;
            switch (colour)
            {
                case Colour.Sunny: return Color.HSVToRGB(Toward(48f, 0.72f), Mathf.Max(sat, 0.6f), Mathf.Max(v, 0.97f));
                case Colour.Deep: return Color.HSVToRGB(Toward(232f, 0.35f), Mathf.Clamp01(sat * 1.15f + 0.15f), v * 0.55f);
                default: return Color.HSVToRGB(h, sat * 0.3f, 1f);
            }
        }

        static Material Mat(string key, Color c)
        {
            if (!materials.TryGetValue(key, out var m) || m == null) materials[key] = m = ShopWorld.MakeMaterial("GR creature " + key, c, 0, 0.55f);
            return m;
        }

        /// <summary>A softly glowing accent (lanterns and lures), under the bloom threshold.</summary>
        static Material GlowMat(string key, Color c, float strength = 0.75f)
        {
            if (!materials.TryGetValue("glow " + key, out var m) || m == null)
            {
                materials["glow " + key] = m = ShopWorld.MakeMaterial("GR creature glow " + key, c, 0, 0.6f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * strength);
            }
            return m;
        }

        public static GrowthCreatureView Create(GrowthSpecimen specimen, Transform parent)
        {
            var def = specimen.Def;
            var root = new GameObject(def.Name + " #" + specimen.Id);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GrowthCreatureView>();
            view.SpecimenId = specimen.Id;
            view.Species = specimen.Species;
            float L = view.Length = LengthOf(def);
            view.Reach = view.TrickReach = 0.6f * L; view.HalfHeight = view.TrickHalf = 0.4f * L;
            if (BuildKit(view, root.transform, specimen, def, L)) return view;
            // GR-05: the specimen's colour (Classic, Sunny, Deep or Shimmer); a Shimmer body glows softly, under the bloom threshold.
            string key = def.Id + (specimen.Colour == Colour.Classic ? "" : "-" + specimen.Colour);
            Color body = ColourOf(def, specimen.Colour), belly = Color.Lerp(body, Color.white, 0.55f), fin = Color.Lerp(body, Color.black, 0.25f);
            var bodyMat = specimen.Colour == Colour.Shimmer ? GlowMat(key + "-body", body, 0.22f) : Mat(key + "-body", body);
            var bellyMat = Mat(key + "-belly", belly); var finMat = Mat(key + "-fin", fin);
            var white = Mat("eye", Color.white); var black = Mat("pupil", new Color(0.08f, 0.1f, 0.12f));
            float head = def.Size == SizeClass.Small ? 1.3f : 1f;
            // Shape by species: Bubblebelly is round, Ribbonfin long with a ribbon tail, the rest a plain teardrop.
            Vector3 bodyScale = new Vector3(0.42f, 0.42f, 0.72f) * L, tailScale = new Vector3(0.04f, 0.34f, 0.3f) * L;
            if (def.Id == "bubblebelly") bodyScale = new Vector3(0.58f, 0.56f, 0.66f) * L;
            if (def.Id == "ribbonfin") { bodyScale = new Vector3(0.34f, 0.36f, 0.8f) * L; tailScale = new Vector3(0.03f, 0.26f, 0.62f) * L; }
            if (def.Family == "saltscale") bodyScale = new Vector3(0.34f, 0.56f, 0.72f) * L;
            if (def.Family == "cometail") tailScale = new Vector3(0.03f, 0.24f, 0.7f) * L;
            ShopWorld.Shape(root.transform, "Body", PrimitiveType.Sphere, Vector3.zero, bodyScale, bodyMat);
            ShopWorld.Shape(root.transform, "Belly", PrimitiveType.Sphere, new Vector3(0, -0.08f * L, 0.02f * L), Vector3.Scale(bodyScale, new Vector3(0.8f, 0.6f, 0.85f)), bellyMat);
            var h = ShopWorld.Shape(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 0.03f * L, 0.36f * L), Vector3.one * (0.42f * L * head), bodyMat);
            float eye = 0.15f * L * head;
            foreach (int side in new[] { -1, 1 })
            {
                var e = ShopWorld.Shape(root.transform, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.15f * L * head, 0.09f * L * head, 0.44f * L + 0.08f * L * (head - 1)), Vector3.one * eye, white);
                ShopWorld.Shape(e, "Pupil", PrimitiveType.Sphere, new Vector3(side * 0.18f, 0.05f, 0.32f), Vector3.one * 0.55f, black);
            }
            view.tail = new GameObject("Tail pivot").transform;
            view.tail.SetParent(root.transform, false);
            view.tail.localPosition = new Vector3(0, 0, -0.34f * L);
            ShopWorld.Shape(view.tail, "Tail", PrimitiveType.Cube, new Vector3(0, 0, -tailScale.z * 0.5f), tailScale, finMat);
            ShopWorld.Shape(root.transform, "Fin", PrimitiveType.Cube, new Vector3(0, 0.22f * L, -0.05f * L), new Vector3(0.03f, 0.16f, 0.22f) * L, finMat);
            Family(view, root.transform, def, key, L, head, bodyScale, body, fin);
            // A small honey tag over a creature a visitor is buying.
            Finish(view, root.transform, specimen, L, 0.45f * L);
            return view;
        }

        /// <summary>A small honey tag over a creature a visitor is buying; no shadows for any creature.</summary>
        static void Finish(GrowthCreatureView view, Transform root, GrowthSpecimen specimen, float L, float tagHeight)
        {
            view.reservedTag = ShopWorld.Shape(root, "Reserved tag", PrimitiveType.Cube, new Vector3(0, tagHeight, 0), new Vector3(0.18f, 0.12f, 0.02f) * L * 1.6f, Mat("reserved", new Color(0.98f, 0.78f, 0.37f)));
            view.reservedTag.gameObject.SetActive(false);
            view.wag = specimen.Id * 0.73f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// GR-12: the kit creature for a kit species, turned to swim along +Z (the kit faces -Z), its body the size class's
        /// length (GR.14; the kit's big-eyed heads already carry the readability the placeholders' 1.3 head gives), in the
        /// specimen's colour. False (and nothing built) without its model.
        /// </summary>
        static bool BuildKit(GrowthCreatureView view, Transform root, GrowthSpecimen specimen, SpeciesDef def, float L)
        {
            if (!DioramaSpecies.TryGet(def.Id, out var spec)) return false;
            var holder = new GameObject("Kit " + spec.Id).transform;
            holder.SetParent(root, false);
            holder.localRotation = KitTurn;
            var visual = holder.gameObject.AddComponent<CreatureVisual>();
            if (!visual.BuildKit(def.Id) || visual.DioramaBodyLength <= 0)
            {
                if (Application.isPlaying) Destroy(holder.gameObject); else DestroyImmediate(holder.gameObject);
                return false;
            }
            holder.localScale = Vector3.one * (L / (visual.DioramaBodyLength * spec.ModelScale));
            visual.SetDioramaVariant(Variant(specimen.Colour));
            visual.SetPondletIdentity(def.Id + "#" + specimen.Id);
            view.kit = visual; view.kitHolder = holder; view.family = spec.Family;
            view.phase = PondletMotion.IdentityPhase("#" + specimen.Id);
            if (!envelopes.TryGetValue(def.Id, out var envelope))
            {
                // GR-12B: its room is measured at rest, since BuildKit leaves it at the start of a stroke, its tail and fins turned (a
                // long tail turned aside reaches less far than at rest); the strokes it samples then swing either way from rest.
                visual.AnimateSpecies(0, 0, true);
                envelopes[def.Id] = envelope = Envelope(def.Id, spec.Family, holder, root, L);
            }
            view.Reach = envelope.x; view.HalfHeight = envelope.y; view.TrickReach = envelope.z; view.TrickHalf = envelope.w;
            Finish(view, root, specimen, L, view.HalfHeight + 0.1f * L);
            return true;
        }

        /// <summary>A kit species' reach across the water and its height, from every part's box (fins, sails, lanterns, tails), at
        /// scale 1: as it rests with its crest at the fullest its trick and habit ever stand it (a Cinderling's spines, a
        /// Reefback's corals: Reach, HalfHeight; x, y), and through its trick with its habit on top (turns, rolls, bows, lifts, the
        /// crest at each moment's flare, its fins and tail at their strokes' extremes with the trick's spread and swing:
        /// TrickReach, TrickHalf; z, w). GR-12B: a remade species' way of swimming too: its crest's and keel's sway, its cloak's
        /// pulse, its fluke's beat up and down, and its body's wave, with its tail fin riding it; and a lantern's or lure's sway.</summary>
        static Vector4 Envelope(string species, DioramaFamily family, Transform holder, Transform root, float L)
        {
            Transform crest = null;
            var pivots = new Transform[5];
            foreach (var x in holder.GetComponentsInChildren<Transform>(true))
                if (x.name == "Crest pivot") crest = crest ? crest : x;
                else if (x.name == "Left fin pivot") pivots[0] = pivots[0] ? pivots[0] : x;
                else if (x.name == "Right fin pivot") pivots[1] = pivots[1] ? pivots[1] : x;
                else if (x.name == "Tail pivot") pivots[2] = pivots[2] ? pivots[2] : x;
                else if (x.name == "Keel pivot") pivots[3] = pivots[3] ? pivots[3] : x;
                else if (x.name == "Lantern pivot") pivots[4] = pivots[4] ? pivots[4] : x;
            var toRoot = root.worldToLocalMatrix;
            var fromRoot = Quaternion.Inverse(root.rotation);
            Vector3 crestAt = crest ? toRoot.MultiplyPoint3x4(crest.position) : Vector3.zero;
            var crestAxes = crest ? fromRoot * crest.rotation : Quaternion.identity;
            // Each swinging pivot (the fins, the tail, a sunfish's keel, then a lantern): where it is and its axes, in the creature's space.
            var pivotAt = new Vector3[5];
            var axes = new Quaternion[5];
            for (int i = 0; i < 5; i++) if (pivots[i]) { pivotAt[i] = toRoot.MultiplyPoint3x4(pivots[i].position); axes[i] = fromRoot * pivots[i].rotation; }
            float tailShift = 0;
            var corners = new List<(Vector3 p, bool onCrest, int on)>();
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;
                bool onCrest = crest && r.transform.IsChildOf(crest);
                int on = -1;
                for (int i = 0; i < 5; i++) if (pivots[i] && r.transform.IsChildOf(pivots[i])) on = i;
                var m = toRoot * r.transform.localToWorldMatrix;
                var b = mesh.bounds;
                // GR-12B: an undulating body's box grows by the farthest its wave moves it, and its tail fin rides the wave as far.
                float wave = r is SkinnedMeshRenderer ? WaveReach(mesh) : 0;
                if (wave > 0) { b.Expand(2 * wave); tailShift = Mathf.Max(tailShift, m.MultiplyVector(new Vector3(wave, 0, 0)).magnitude); }
                for (int k = 0; k < 8; k++)
                    corners.Add((m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))), onCrest, on));
            }
            var stroke = CreatureVisual.DioramaStroke(family, species);
            var sway = CreatureVisual.DioramaGaitStroke(DioramaSpecies.TryGet(species, out var remade) ? remade.Gait : DioramaGait.Family);
            (float reach, float half) Extent(Quaternion turn, float lift, float flare, float fins, float tail, bool swing)
            {
                float grow = 1 + CreatureVisual.CrestFlareGrowth * Mathf.Clamp(flare, 0, 1.5f), far = 0, high = 0;
                float roll = stroke.roll + Mathf.Abs(fins), yaw = Mathf.Min(CreatureVisual.DioramaTailLimit, stroke.tail + Mathf.Abs(tail));
                foreach (var (p, onCrest, on) in corners)
                {
                    var at = onCrest ? crestAt + (p - crestAt) * grow : p;
                    // GR-12B: a squid's cloak (its tail) at its widest pulse.
                    if (swing && on == 2 && sway.pulse > 0)
                        at = pivotAt[2] + axes[2] * Vector3.Scale(Quaternion.Inverse(axes[2]) * (at - pivotAt[2]), new Vector3(1 + sway.pulse, 1, 1 + sway.pulse));
                    int beats = sway.fluke > 0 ? 3 : 1;
                    int ways = !swing ? 1 : onCrest ? (sway.crest > 0 ? 3 : 1) : on < 0 ? 1 : on < 2 ? 9 : on == 2 ? 3 * beats * (tailShift > 0 ? 3 : 1) : 3;
                    for (int k = 0; k < ways; k++)
                    {
                        var q = at;
                        if (ways > 1)
                        {
                            if (onCrest)
                                // GR-12B: a sunfish's tall crest at either side of its sway.
                                q = crestAt + crestAxes * Quaternion.Euler(0, 0, sway.crest * (k - 1)) * Quaternion.Inverse(crestAxes) * (at - crestAt);
                            else
                            {
                                // A fin at each corner of its roll and pitch, the tail at either side (and a fluke at the top and the
                                // bottom of its beat; riding a body's wave to either side too), a sunfish's keel at either side of its sway,
                                // a lantern at either end of its sway.
                                var e = on < 2 ? new Vector3(stroke.pitch * (k / 3 - 1), 0, roll * (k % 3 - 1))
                                    : on == 2 ? new Vector3(beats > 1 ? sway.fluke * ((k / 3) % 3 - 1) : 0, yaw * (k % 3 - 1), 0)
                                    : on == 3 ? new Vector3(0, 0, sway.keel * (k - 1))
                                    : new Vector3(CreatureVisual.DioramaLanternSway * (k - 1), 0, 0);
                                q = pivotAt[on] + axes[on] * Quaternion.Euler(e) * Quaternion.Inverse(axes[on]) * (at - pivotAt[on]);
                                if (on == 2 && tailShift > 0) q += Vector3.right * (tailShift * (k / (3 * beats) - 1));
                            }
                        }
                        q = turn * q + Vector3.up * lift;
                        far = Mathf.Max(far, new Vector2(q.x, q.z).magnitude); high = Mathf.Max(high, Mathf.Abs(q.y));
                    }
                }
                return (far, high);
            }
            var (reach, half) = Extent(Quaternion.identity, 0, DioramaTricks.MaxFlare(species), 0, 0, false);
            // Its habit's farthest pitch, roll, lift, flare, spread and swing, added to each moment of its trick.
            float idlePitch = 0, idleRoll = 0, idleUp = 0, idleDown = 0, idleFlare = 0, idleFins = 0, idleTail = 0;
            for (float t = 0; t < 30; t += 1 / 20f)
            {
                var p = DioramaTricks.Idle(species, t, 0);
                idlePitch = Mathf.Max(idlePitch, Mathf.Abs(p.Pitch)); idleRoll = Mathf.Max(idleRoll, Mathf.Abs(p.Roll));
                idleUp = Mathf.Max(idleUp, p.Lift); idleDown = Mathf.Min(idleDown, p.Lift); idleFlare = Mathf.Max(idleFlare, p.Flare);
                idleFins = Mathf.Max(idleFins, Mathf.Abs(p.Fins)); idleTail = Mathf.Max(idleTail, Mathf.Abs(p.Tail));
            }
            float trickReach = reach, trickHalf = half, d = DioramaTricks.Duration(species);
            var unturn = Quaternion.Inverse(KitTurn);
            for (int i = 0; i <= 61; i++)
            {
                var pose = i == 61 || d <= 0 ? default : species == "pondlet" ? DioramaTricks.PondletBubble(d * i / 60f) : DioramaTricks.Trick(species, d * i / 60f);
                for (int c = 0; c < 8; c++)
                {
                    var turn = KitTurn * Quaternion.Euler(pose.Pitch + ((c & 1) == 0 ? -idlePitch : idlePitch), pose.Yaw, pose.Roll + ((c & 2) == 0 ? -idleRoll : idleRoll)) * unturn;
                    float lift = (pose.Lift + ((c & 4) == 0 ? idleDown : idleUp)) * L * LiftShare;
                    var (far, high) = Extent(turn, lift, pose.Flare + idleFlare, Mathf.Abs(pose.Fins) + idleFins, Mathf.Abs(pose.Tail) + idleTail, true);
                    trickReach = Mathf.Max(trickReach, far); trickHalf = Mathf.Max(trickHalf, high);
                }
            }
            return new Vector4(reach, half, trickReach * TrickSwing, trickHalf * TrickSwing);
        }

        /// <summary>GR-12B: the farthest an undulating body's wave moves any of its vertices (0 without Wave keys): its WaveSin and
        /// WaveCos together, since it bends by WaveSin cos - WaveCos sin of its phase.</summary>
        static float WaveReach(Mesh mesh)
        {
            int s = mesh.GetBlendShapeIndex("WaveSin"), c = mesh.GetBlendShapeIndex("WaveCos");
            if (s < 0 || c < 0) return 0;
            var ds = new Vector3[mesh.vertexCount]; var dc = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(s, 0, ds, null, null); mesh.GetBlendShapeFrameVertices(c, 0, dc, null, null);
            float most = 0;
            for (int i = 0; i < ds.Length; i++) most = Mathf.Max(most, Mathf.Sqrt(ds[i].sqrMagnitude + dc[i].sqrMagnitude));
            return most;
        }

        /// <summary>Each family's feature, so sizes and families read apart at a glance (placeholders for GR.14's rigs).</summary>
        static void Family(GrowthCreatureView view, Transform root, SpeciesDef def, string key, float L, float head, Vector3 bodyScale, Color body, Color fin)
        {
            var finMat = Mat(key + "-fin", fin);
            switch (def.Family)
            {
                case "sparkline":
                    ShopWorld.Shape(root, "Lantern", PrimitiveType.Sphere, new Vector3(0, 0.26f * L * head, 0.46f * L), Vector3.one * 0.13f * L, GlowMat("spark", new Color(1f, 0.85f, 0.35f)));
                    break;
                case "dewling":
                    foreach (int side in new[] { -1, 1 })
                        ShopWorld.Shape(root, "Dew bead", PrimitiveType.Sphere, new Vector3(side * 0.09f * L, 0.24f * L, -0.04f * L), Vector3.one * 0.11f * L, Mat("dew", new Color(0.92f, 0.95f, 1f)));
                    break;
                case "airfin":
                    foreach (int side in new[] { -1, 1 })
                    {
                        var wing = ShopWorld.Shape(root, "Wing", PrimitiveType.Cube, new Vector3(side * 0.36f * L, 0.03f * L, 0.02f * L), new Vector3(0.42f, 0.02f, 0.3f) * L, finMat);
                        wing.localRotation = Quaternion.Euler(0, 0, side * 14f);
                    }
                    break;
                case "cinderling":
                    for (int i = 0; i < 3; i++)
                    {
                        var spike = ShopWorld.Shape(root, "Spike", PrimitiveType.Cube, new Vector3(0, 0.25f * L, (0.14f - 0.14f * i) * L), new Vector3(0.06f, 0.06f, 0.06f) * L, finMat);
                        spike.localRotation = Quaternion.Euler(45, 0, 0);
                    }
                    break;
                case "saltscale":
                    ShopWorld.Shape(root, "Stripe", PrimitiveType.Sphere, Vector3.zero, Vector3.Scale(bodyScale, new Vector3(1.04f, 0.22f, 0.92f)), Mat("stripe", new Color(0.95f, 0.97f, 0.96f)));
                    break;
                case "reefback":
                    ShopWorld.Shape(root, "Shell", PrimitiveType.Sphere, new Vector3(0, 0.17f * L, -0.04f * L), new Vector3(0.5f, 0.3f, 0.62f) * L, Mat(key + "-shell", Color.Lerp(fin, new Color(0.45f, 0.36f, 0.28f), 0.5f)));
                    break;
                case "gloamer":
                    ShopWorld.Shape(root, "Lure stalk", PrimitiveType.Cube, new Vector3(0, 0.3f * L, 0.38f * L), new Vector3(0.025f, 0.2f, 0.025f) * L, finMat);
                    ShopWorld.Shape(root, "Lure", PrimitiveType.Sphere, new Vector3(0, 0.42f * L, 0.44f * L), Vector3.one * 0.09f * L, GlowMat("lure", new Color(0.6f, 0.95f, 1f)));
                    break;
                case "cometail":
                    ShopWorld.Shape(root, "Comet glow", PrimitiveType.Sphere, new Vector3(0, 0, -0.42f * L), new Vector3(0.18f, 0.18f, 0.3f) * L, GlowMat("comet", Color.Lerp(body, Color.white, 0.5f)));
                    break;
            }
        }

        /// <summary>One frame of its life at time t: a placeholder wags its tail; a kit creature strokes, blinks, looks at the
        /// player, plays its family's habit and, after a moment under the reticle in peek, its trick (Reduced motion: none).</summary>
        public void Tick(float t, bool reduced = false)
        {
            if (kit == null)
            {
                if (tail) tail.localRotation = Quaternion.Euler(0, 22f * Mathf.Sin(t * 7f + wag), 0);
                return;
            }
            // Its distant look is at rest: no face, habit or trick to run.
            if (detail != 0) { lastTick = -1; return; }
            float dt = lastTick < 0 || !CreatureMotion.Finite(t) ? 0 : Mathf.Clamp(t - lastTick, 0, .25f);
            lastTick = CreatureMotion.Finite(t) ? t : lastTick;
            float before = clock;
            if (!reduced) clock += dt;
            focus = highlighted ? focus + dt : 0;
            float trickBefore = trickAge;
            if (trickAge >= 0) { trickAge += dt; if (trickAge >= DioramaTricks.Duration(Species)) trickAge = -1; }
            if (reduced) trickAge = -1;
            else if (trickAge < 0 && focus >= TrickLinger && !(t < restUntil)) { trickAge = 0; trickBefore = -1; restUntil = t + TrickRest; TricksPlayed++; }
            var idle = reduced ? default : DioramaTricks.Idle(Species, clock, phase, before);
            var trick = trickAge < 0 ? default : Species == "pondlet" ? DioramaTricks.PondletBubble(trickAge, trickBefore) : DioramaTricks.Trick(Species, trickAge, trickBefore);
            bool tricking = trickAge >= 0;
            kit.SetDioramaFace(tricking ? Mathf.Max(.2f, trick.Happy) : .2f + idle.Happy, Mathf.Max(idle.Surprised, trick.Surprised), Mathf.Max(idle.Puffed, trick.Puffed));
            kit.SetDioramaAction(idle.Glow + trick.Glow, idle.Tail + trick.Tail, idle.Fins + trick.Fins, idle.Flare + trick.Flare);
            if (ViewerSet && !reduced) kit.SetLookTarget(Viewer); else kit.ClearLookTarget();
            kitHolder.localRotation = KitTurn * Quaternion.Euler(idle.Pitch + trick.Pitch, idle.Yaw + trick.Yaw, idle.Roll + trick.Roll);
            kitHolder.localPosition = Vector3.up * ((idle.Lift + trick.Lift) * Length * LiftShare);
            kit.AnimateSpecies(clock, .7f, reduced);
            if (!reduced && (trick.Release || idle.Release)) Burst(trick.Release);
        }

        /// <summary>
        /// GR.5's update levels, for a kit creature's look: near and in view (0) the full kit creature, with its face, strokes,
        /// habit and trick (peek, tricks and the player's look at it all happen there); further (1), and beyond 6 m or out of
        /// view (2), its distant look instead: its species in its colour, at rest, baked into one mesh shared by every specimen
        /// of it, with one material a colour slot, so a tank full of creatures costs a handful of draws; with its ink outline at
        /// 1, without at 2. A placeholder keeps its primitives at every level.
        /// </summary>
        public void SetDetail(int level)
        {
            if (kit == null || detail == level) return;
            detail = level;
            bool near = level == 0;
            if (!near && far == null) far = BuildFar();
            trickAge = -1;
            kitHolder.gameObject.SetActive(near);
            if (far != null)
            {
                far.SetActive(!near);
                if (!near && farLooks.TryGetValue(FarKey, out var look)) far.GetComponent<MeshRenderer>().sharedMaterials = level == 1 ? look.outlined : look.plain;
            }
        }

        string FarKey => Species + "/" + kit.DioramaVariant;

        GameObject BuildFar()
        {
            if (!farLooks.TryGetValue(FarKey, out var look) || look.mesh == null) farLooks[FarKey] = look = Bake(kitHolder);
            var go = new GameObject("Far " + Species);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = KitTurn;
            go.transform.localScale = kitHolder.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = look.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = look.outlined;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>The kit creature as it is now, in its holder's space, merged into one mesh with a submesh a colour slot,
        /// and copies of its materials (the lantern at its resting glow), with its outline and without.</summary>
        static (Mesh, Material[], Material[]) Bake(Transform holder)
        {
            var groups = new List<(Material material, List<CombineInstance> parts)>();
            var toHolder = holder.worldToLocalMatrix;
            var temporary = new List<Mesh>();
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh;
                if (r is SkinnedMeshRenderer skinned) { mesh = new Mesh(); skinned.BakeMesh(mesh, false); temporary.Add(mesh); }
                else if (r.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh) mesh = filter.sharedMesh;
                else continue;
                var matrix = toHolder * r.transform.localToWorldMatrix;
                var materials = r.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    int g = groups.FindIndex(x => x.material == materials[sub]);
                    if (g < 0) { groups.Add((materials[sub], new List<CombineInstance>())); g = groups.Count - 1; }
                    groups[g].parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                }
            }
            var slots = new List<CombineInstance>();
            var copies = new Material[groups.Count];
            var plain = new Material[groups.Count];
            for (int g = 0; g < groups.Count; g++)
            {
                var merged = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                merged.CombineMeshes(groups[g].parts.ToArray(), true, true);
                temporary.Add(merged);
                slots.Add(new CombineInstance { mesh = merged, transform = Matrix4x4.identity });
                var copy = new Material(groups[g].material) { name = groups[g].material.name + " (far)" };
                if (copy.IsKeywordEnabled("_EMISSION") || DioramaLook.GlowSlot(groups[g].material.name.Replace("Diorama / ", "")))
                    copy.SetColor("_EmissionColor", copy.GetColor("_BaseColor") * .6f);
                copies[g] = copy;
                plain[g] = new Material(copy) { name = copy.name.Replace(" (far)", " (far, plain)") };
                plain[g].SetShaderPassEnabled(DioramaLook.OutlinePass, false);
            }
            var result = new Mesh { name = "Far look", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            result.CombineMeshes(slots.ToArray(), false, false);
            result.RecalculateBounds();
            foreach (var m in temporary) if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            return (result, copies, plain);
        }

        /// <summary>The bubbles (Brookkin, from its lips), sparkles (Sparkline, from its lantern), puff of mist (Airfin), sparks
        /// (Cinderling, from its spines), droplets (Dewling, from its dew beads), plankton (Reefback, from its corals), salt
        /// (Saltscale, from its body, along its stripe), glowing motes (Gloamer, from its lure) or stardust (Cometail, from its
        /// tail) a trick or habit lets go on its beat.</summary>
        void Burst(bool trick)
        {
            bool fromCrest = (family == DioramaFamily.Cinderling || family == DioramaFamily.Dewling || family == DioramaFamily.Reefback) && kit.DioramaCrestPoint;
            bool fromLantern = (family == DioramaFamily.Sparkline || family == DioramaFamily.Gloamer) && kit.DioramaLanternPoint;
            var from = fromLantern ? kit.DioramaLanternPoint : fromCrest ? kit.DioramaCrestPoint :
                family == DioramaFamily.Cometail && kit.DioramaTailPivot ? kit.DioramaTailPivot :
                family == DioramaFamily.Saltscale ? kit.transform : kit.DioramaLips ? kit.DioramaLips : transform;
            GrowthCreatureBurst.Spawn(transform.parent, from.position, family, trick ? 5 : 2, Length);
            BurstsReleased++; Bursts++;
        }
    }
}
