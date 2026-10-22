using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D: the Pocket Aquarium Diorama look everywhere. AD-A gave the display corner and the incubator materials of their
    /// own; every other room, wing, station, placed item and the siphon takes the look by converting the world's own
    /// material objects to the toon (or glass) shader while the look is on, and back exactly when it is off. Converting the
    /// material objects themselves, not the renderers, means a material the game assigns later (a status lamp, a module
    /// indicator) is already in the look. Presentation only: rules, saves, colliders and stations are untouched.
    /// </summary>
    public sealed partial class ShopWorld
    {
        readonly Dictionary<Material, DioramaConvertedMaterial> dioramaConverted = new Dictionary<Material, DioramaConvertedMaterial>();
        readonly HashSet<Renderer> dioramaSwapRenderers = new HashSet<Renderer>();
        // AD-D water (AD.5): the placed displays' sand (and the bowls') take moving caustics, and their water a wobbling
        // surface, as the corner's tanks have since AD-A; each comes back as it was with the look off.
        readonly List<(Renderer renderer, Material classic)> dioramaCausticSands = new List<(Renderer, Material)>();
        readonly List<(MeshFilter filter, Mesh classic)> dioramaPlacedWater = new List<(MeshFilter, Mesh)>();
        Material dioramaCausticSand;
        // AD-D signs (AD.4: fewer, bigger signs): a room sign shows its name alone, larger and centred on its board; its
        // tagline is left off while the look is on. Labels that tell the player something (stock, prices, states) are untouched.
        static readonly (string title, string tagline)[] DioramaSignPairs =
        {
            ("THE CURIOSITY LAB", "BIG IDEAS. LITTLE CREATURES."), ("LITTLE TIDE AQUATICS", "CURIOUS FISH. HAPPY LITTLE WORLDS."),
            ("GALLERY 0", "ROOM FOR MORE LITTLE LIVES"),
        };
        public const float DioramaSignTitleScale = 1.3f;
        readonly List<(TextMesh title, Renderer tagline, Vector3 position, Vector3 scale)> dioramaSigns = new List<(TextMesh, Renderer, Vector3, Vector3)>();
        public int DioramaSignsSimplified { get { int n = 0; foreach (var s in dioramaSigns) if (s.title) n++; return n; } }

        void SimplifyDioramaSigns()
        {
            dioramaSigns.RemoveAll(s => !s.title);
            foreach (var tagline in GetComponentsInChildren<TextMesh>(true))
            {
                foreach (var (titleText, taglineText) in DioramaSignPairs)
                {
                    if (tagline.text != taglineText) continue;
                    var tagRenderer = tagline.GetComponent<Renderer>();
                    if (!tagRenderer || dioramaSigns.Exists(s => s.tagline == tagRenderer)) continue;
                    TextMesh title = null;
                    foreach (Transform sibling in tagline.transform.parent)
                    {
                        var text = sibling.GetComponent<TextMesh>();
                        // A wing's title is its own: the nearest one above its tagline.
                        if (text && text != tagline && text.text.StartsWith(titleText) && Mathf.Abs(sibling.localPosition.x - tagline.transform.localPosition.x) < .5f &&
                            Mathf.Abs(sibling.localPosition.z - tagline.transform.localPosition.z) < .1f) { title = text; break; }
                    }
                    if (!title) continue;
                    var t = title.transform;
                    dioramaSigns.Add((title, tagRenderer, t.localPosition, t.localScale));
                    tagRenderer.enabled = false;
                    var centre = t.localPosition; centre.y = (t.localPosition.y + tagline.transform.localPosition.y) * .5f;
                    t.localPosition = centre; t.localScale = t.localScale * DioramaSignTitleScale;
                }
            }
        }
        public int DioramaCausticSandCount { get { int n = 0; foreach (var (r, _) in dioramaCausticSands) if (r) n++; return n; } }
        public int DioramaWobblingWaterCount { get { int n = 0; foreach (var (f, _) in dioramaPlacedWater) if (f) n++; return n; } }
        int dioramaSweptHierarchy = -1;
        float dioramaSweepClock;

        /// <summary>How many of the world's materials the look has converted (validation and the route's receipt).</summary>
        public int DioramaConvertedCount => dioramaConverted.Count;

        static bool ClassicLit(Material m) => DioramaRoomRules.ClassicLit(m);

        /// <summary>The classic state of a material, whether or not the look has converted it in place.</summary>
        void ClassicState(Material m, out Color baseColor, out bool emissive, out Color emission, out int queue)
        {
            if (dioramaConverted.TryGetValue(m, out var c))
            { baseColor = c.Base; emissive = c.Emissive; emission = c.Emission; queue = c.Queue; return; }
            baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            emissive = m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor");
            emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            queue = m.renderQueue;
        }

        /// <summary>Renderers the conversion leaves alone: particles and text, creatures and characters (they have kit
        /// materials of their own), the FX pool, the look's own objects and the placement ghosts.</summary>
        bool DioramaSkipsRenderer(Renderer renderer)
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) return true;
            if (renderer.GetComponent<TextMesh>()) return true;
            if (dioramaSwapRenderers.Contains(renderer)) return true;
            if (renderer.GetComponentInParent<CreatureVisual>(true) || renderer.GetComponentInParent<ShopCharacterVisual>(true) ||
                renderer.GetComponentInParent<CustomerActor>(true)) return true;
            var t = renderer.transform;
            if (dioramaRoot && t.IsChildOf(dioramaRoot)) return true;
            if (fxPool != null && fxPool.Root && t.IsChildOf(fxPool.Root)) return true;
            return false;
        }

        static bool DioramaSkipsMaterial(Material m) => DioramaRoomRules.SkipsMaterial(m);

        /// <summary>Converts every classic lit material the world and the siphon show (new ones as rooms, wings and placed
        /// items appear). Cheap enough to run whenever the world's hierarchy changes, and once a second otherwise.</summary>
        void SweepDioramaMaterials()
        {
            if (!dioramaActive) return;
            dioramaSweptHierarchy = transform.hierarchyCount;
            dioramaSweepClock = 0;
            var renderers = new List<Renderer>();
            GetComponentsInChildren(true, renderers);
            if (ToolRoot) renderers.AddRange(ToolRoot.GetComponentsInChildren<Renderer>(true));
            SimplifyDioramaSigns();
            dioramaCausticSands.RemoveAll(s => !s.renderer);
            dioramaPlacedWater.RemoveAll(w => !w.filter);
            foreach (var renderer in renderers)
            {
                if (!renderer || DioramaSkipsRenderer(renderer)) continue;
                if ((renderer.name == "Compartment sand" || renderer.name == "Bowl sand") && renderer.sharedMaterial != dioramaCausticSand)
                {
                    if (!dioramaCausticSand)
                    {
                        ClassicState(renderer.sharedMaterial, out var sandColour, out _, out _, out _); sandColour.a = 1;
                        dioramaCausticSand = DioramaOwn(DioramaLook.Toon("caustic sand", sandColour, DioramaLook.Style.Prop));
                        dioramaCausticSand.SetFloat("_CausticStrength", .9f);
                    }
                    dioramaCausticSands.Add((renderer, renderer.sharedMaterial));
                    renderer.sharedMaterial = dioramaCausticSand;
                    continue;
                }
                if (renderer.name == "Aquatic tint" && dioramaWaterMesh)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh != dioramaWaterMesh) { dioramaPlacedWater.Add((filter, filter.sharedMesh)); filter.sharedMesh = dioramaWaterMesh; }
                }
                var b = renderer.bounds;
                bool room = DioramaRoomRules.RoomStyle(b);
                foreach (var m in renderer.sharedMaterials)
                {
                    if (!m || dioramaConverted.ContainsKey(m)) continue;
                    if (DioramaSkipsMaterial(m)) continue;
                    ConvertDioramaMaterial(m, room);
                }
            }
        }

        void ConvertDioramaMaterial(Material m, bool room) => dioramaConverted.Add(m, DioramaRoomRules.Convert(m, room));

        void RestoreDioramaMaterials()
        {
            foreach (var (renderer, classic) in dioramaCausticSands) if (renderer) renderer.sharedMaterial = classic;
            dioramaCausticSands.Clear();
            foreach (var (filter, classic) in dioramaPlacedWater) if (filter) filter.sharedMesh = classic;
            dioramaPlacedWater.Clear();
            foreach (var (title, tagline, position, scale) in dioramaSigns)
            {
                if (title) { title.transform.localPosition = position; title.transform.localScale = scale; }
                if (tagline) tagline.enabled = true;
            }
            dioramaSigns.Clear();
            foreach (var c in dioramaConverted.Values) DioramaRoomRules.Restore(c);
            dioramaConverted.Clear();
            dioramaSweptHierarchy = -1;
        }

        void TickDioramaRooms(float dt)
        {
            if (!dioramaActive) return;
            RefreshDioramaSetPieces();
            TickDioramaSetPieces(dt);
            TickDioramaPipes(dt);
            dioramaSweepClock += Mathf.Max(0, dt);
            if (transform.hierarchyCount != dioramaSweptHierarchy || dioramaSweepClock > 1) SweepDioramaMaterials();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Validation: with the look on, every renderer the conversion covers shows only diorama shaders (or
        /// materials it leaves alone, such as the placement ghosts); with it off, no converted material keeps a diorama
        /// shader.</summary>
        public bool DioramaWorldConsistent(out string detail)
        {
            var renderers = new List<Renderer>();
            GetComponentsInChildren(true, renderers);
            if (ToolRoot) renderers.AddRange(ToolRoot.GetComponentsInChildren<Renderer>(true));
            int checkedRenderers = 0;
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || DioramaSkipsRenderer(renderer)) continue;
                checkedRenderers++;
                foreach (var m in renderer.sharedMaterials)
                {
                    if (!m || !m.shader) continue;
                    string shader = m.shader.name;
                    bool diorama = shader.StartsWith("EvolutionShop/Diorama");
                    if (dioramaActive && ClassicLit(m) && !DioramaSkipsMaterial(m))
                    { detail = renderer.name + " still shows the classic " + m.name; return false; }
                    if (!dioramaActive && diorama)
                    { detail = renderer.name + " still shows the diorama " + m.name; return false; }
                }
            }
            detail = checkedRenderers + " renderers, " + dioramaConverted.Count + " converted materials, consistent with the look " + (dioramaActive ? "on" : "off") + ".";
            return true;
        }
#endif
    }
}
