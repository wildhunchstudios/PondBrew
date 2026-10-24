using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// A creature at its size class (GR.5: a small one is 6-9 cm, a medium 12-18, a large 25-40). GR-12: a species the diorama
    /// kit has (wave 1: the Brookkin, Sparkline and Airfin) is its kit model in its colour from the variant atlas, its body set
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
        public bool Highlighted
        {
            get => highlighted;
            set { highlighted = value; transform.localScale = Vector3.one * (value ? 1.35f : 1f); }
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
        float phase, clock, lastTick = -1, focus, cooldown, trickAge = -1;
        static readonly Quaternion KitTurn = Quaternion.Euler(0, 180, 0);
        /// <summary>Seconds on a creature in peek before it plays its trick, and the rest after one (AD-B's linger is 1.5 s at
        /// a display; a peek is already a close look).</summary>
        public const float TrickLinger = .8f, TrickRest = 6f;
        /// <summary>A trick's lift is in the classic display's units; in the greybox it is this share of the creature's length.</summary>
        const float LiftShare = 1.6f;

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
            view.Reach = 0.6f * L; view.HalfHeight = 0.4f * L;
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
            // Its reach across the water and its height, from every part (fins, sails, lanterns, tails), as it rests.
            float reach = 0, half = 0;
            var toRoot = root.worldToLocalMatrix;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;
                var m = toRoot * r.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    reach = Mathf.Max(reach, new Vector2(p.x, p.z).magnitude); half = Mathf.Max(half, Mathf.Abs(p.y));
                }
            }
            view.Reach = reach; view.HalfHeight = half;
            Finish(view, root, specimen, L, half + 0.1f * L);
            return true;
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
            float dt = lastTick < 0 || !FxParticleRequest.Finite(t) ? 0 : Mathf.Clamp(t - lastTick, 0, .25f);
            lastTick = FxParticleRequest.Finite(t) ? t : lastTick;
            float before = clock;
            if (!reduced) clock += dt;
            cooldown = Mathf.Max(0, cooldown - dt);
            focus = highlighted ? focus + dt : 0;
            float trickBefore = trickAge;
            if (trickAge >= 0) { trickAge += dt; if (trickAge >= DioramaTricks.Duration(Species)) trickAge = -1; }
            if (reduced) trickAge = -1;
            else if (trickAge < 0 && focus >= TrickLinger && cooldown <= 0) { trickAge = 0; trickBefore = -1; cooldown = TrickRest; TricksPlayed++; }
            var idle = reduced ? default : DioramaTricks.Idle(Species, clock, phase, before);
            var trick = trickAge < 0 ? default : Species == "pondlet" ? DioramaTricks.PondletBubble(trickAge, trickBefore) : DioramaTricks.Trick(Species, trickAge, trickBefore);
            bool tricking = trickAge >= 0;
            kit.SetDioramaFace(tricking ? Mathf.Max(.2f, trick.Happy) : .2f + idle.Happy, Mathf.Max(idle.Surprised, trick.Surprised), Mathf.Max(idle.Puffed, trick.Puffed));
            kit.SetDioramaAction(idle.Glow + trick.Glow, idle.Tail + trick.Tail, idle.Fins + trick.Fins);
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

        /// <summary>The bubbles (Brookkin, from its lips), sparkles (Sparkline, from its lantern) or puff of mist (Airfin) a trick or
        /// habit lets go on its beat.</summary>
        void Burst(bool trick)
        {
            var from = family == DioramaFamily.Sparkline && kit.DioramaLanternPoint ? kit.DioramaLanternPoint : kit.DioramaLips ? kit.DioramaLips : transform;
            GrowthCreatureBurst.Spawn(transform.parent, from.position, family, trick ? 5 : 2, Length);
            BurstsReleased++; Bursts++;
        }
    }

    /// <summary>A greybox visitor (a coloured capsule by type) walking the shop's routes as the Core visit unfolds.</summary>
    public sealed class GrowthVisitorView : MonoBehaviour
    {
        public int VisitorId { get; private set; }
        public GrowthVisitorState State { get; private set; } = (GrowthVisitorState)(-1);
        public bool Arrived => path.Count == 0;
        readonly List<Vector3> path = new List<Vector3>();
        string place;
        int queueIndex = -1;
        bool wasQueued;
        /// <summary>GR-10: as the rules time a visitor's walk (GrowthSpaces.VisitorSpeed).</summary>
        const float Speed = GrowthSpaces.VisitorSpeed;
        static readonly Dictionary<string, Material> visitorMaterials = new Dictionary<string, Material>();

        static Color TypeColour(string type)
        {
            switch (type)
            {
                case "bargain": return new Color(0.98f, 0.78f, 0.37f);
                case "collector": return new Color(0.84f, 0.38f, 0.27f);
                case "premium": return new Color(0.55f, 0.42f, 0.78f);
                case GrowthShop.GuestType: return new Color(0.86f, 0.64f, 0.16f);
                default: return new Color(0.31f, 0.66f, 0.75f);
            }
        }

        public static GrowthVisitorView Create(GrowthVisitor v, Transform parent)
        {
            var root = new GameObject("Visitor " + v.Id + " (" + v.Type + ")");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GrowthVisitorView>();
            view.VisitorId = v.Id;
            if (!visitorMaterials.TryGetValue(v.Type, out var body) || body == null) visitorMaterials[v.Type] = body = ShopWorld.MakeMaterial("GR visitor " + v.Type, TypeColour(v.Type), 0, 0.3f);
            if (!visitorMaterials.TryGetValue("skin", out var skin) || skin == null) visitorMaterials["skin"] = skin = ShopWorld.MakeMaterial("GR visitor skin", new Color(0.93f, 0.78f, 0.64f), 0, 0.3f);
            ShopWorld.Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.62f, 0.32f), body);
            ShopWorld.Shape(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.3f, skin);
            ShopWorld.Shape(root.transform, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0.15f), Vector3.one * 0.07f, skin);
            // GR-11: an exhibition guest wears gold and a rosette.
            if (v.Type == GrowthShop.GuestType)
            {
                if (!visitorMaterials.TryGetValue("rosette", out var rosette) || rosette == null) visitorMaterials["rosette"] = rosette = ShopWorld.MakeMaterial("GR guest rosette", new Color(0.98f, 0.95f, 0.85f), 0, 0.2f);
                ShopWorld.Shape(root.transform, "Rosette", PrimitiveType.Sphere, new Vector3(0.1f, 1.12f, 0.17f), new Vector3(0.14f, 0.14f, 0.05f), rosette);
                ShopWorld.Shape(root.transform, "Hat", PrimitiveType.Cylinder, new Vector3(0, 1.74f, 0), new Vector3(0.26f, 0.05f, 0.26f), body);
            }
            return view;
        }

        /// <summary>Re-routes when the visit's state, browse place or queue spot changes.</summary>
        public void Follow(GrowthVisitor v, GrowthShop shop, int index)
        {
            string target = v.ContainerId == null ? null : shop.Container(v.ContainerId)?.Place;
            if (v.State == State && (v.State != GrowthVisitorState.Queued || index == queueIndex) && (v.State != GrowthVisitorState.Browsing || target == place)) return;
            path.Clear();
            switch (v.State)
            {
                // GR-03: a visitor who found nothing walks on to the next container in their plan.
                case GrowthVisitorState.Browsing: path.AddRange(State == GrowthVisitorState.Browsing && place != null ? GrowthWorld.RouteBetween(place, target) : GrowthWorld.RouteTo(target)); place = target; break;
                case GrowthVisitorState.Queued:
                    if (State == GrowthVisitorState.Queued) path.Add(GrowthWorld.QueueSpots[Mathf.Clamp(index, 0, GrowthWorld.QueueSpots.Length - 1)]);
                    else path.AddRange(GrowthWorld.RouteToQueue(place, index));
                    wasQueued = true;
                    break;
                case GrowthVisitorState.Leaving: path.AddRange(GrowthWorld.RouteOut(place, wasQueued)); break;
            }
            State = v.State;
            queueIndex = index;
        }

        public void Tick(float dt)
        {
            float step = Speed * dt;
            while (path.Count > 0 && step > 0)
            {
                Vector3 to = path[0], from = transform.position; to.y = 0; from.y = 0;
                Vector3 d = to - from;
                if (d.magnitude <= step) { transform.position = to; step -= d.magnitude; path.RemoveAt(0); continue; }
                transform.position = from + d.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 0.25f);
                step = 0;
            }
            if (path.Count == 0 && State == GrowthVisitorState.Queued)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.back, Vector3.up), 0.2f);
        }
    }
}
