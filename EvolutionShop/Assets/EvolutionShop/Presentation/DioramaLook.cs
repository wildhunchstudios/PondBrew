using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>AD-A Pocket Aquarium Diorama: shared shaders, toon materials and the slice's palette. Presentation only.</summary>
    public static class DioramaLook
    {
        public const string ToonResource = "EvolutionDioramaToon", GlassResource = "EvolutionDioramaGlass",
            UnlitResource = "EvolutionDioramaUnlit";
        public const string ToonShaderName = "EvolutionShop/Diorama Toon", GlassShaderName = "EvolutionShop/Diorama Glass",
            UnlitShaderName = "EvolutionShop/Diorama Unlit";
        public const string KitPath = "Art/DioramaKit/";
        public const string OutlinePass = "SRPDefaultUnlit";
        public static readonly Color Ink = Hex("1B413E");

        public enum Style { Room, Prop, Creature, Character, Glint }

        static Shader Load(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            if (!shader) throw new InvalidOperationException("Missing Resources/" + resource + ".shader.");
            return shader;
        }
        public static Shader ToonShader => Load(ToonResource);
        public static Shader GlassShader => Load(GlassResource);
        public static Shader UnlitShader => Load(UnlitResource);

        public static Color Hex(string code)
        {
            ColorUtility.TryParseHtmlString("#" + code, out var color);
            return color;
        }

        /// <summary>A toon material. Creatures and characters get the thin dark outline; rooms and props never do.</summary>
        public static Material Toon(string name, Color color, Style style = Style.Prop)
        {
            var m = new Material(ToonShader) { name = "Diorama / " + name };
            m.SetColor("_BaseColor", color);
            ApplyToonStyle(m, style);
            return m;
        }

        /// <summary>Sets a toon material's shading for its style (AD-D converts the world's own materials with it).</summary>
        public static void ApplyToonStyle(Material m, Style style)
        {
            bool alive = style == Style.Creature || style == Style.Character;
            // Creatures and characters keep a lighter shadow side, so a face turned from the key light still reads.
            m.SetColor("_ShadeColor", style == Style.Room ? new Color(.62f, .6f, .74f) : alive ? new Color(.7f, .62f, .8f) : new Color(.52f, .5f, .68f));
            m.SetFloat("_ShadeSoftness", alive ? .035f : .06f);
            m.SetFloat("_RimStrength", style == Style.Room ? .12f : alive ? .38f : .24f);
            m.SetFloat("_RimWidth", alive ? .3f : .24f);
            m.SetFloat("_GlossStrength", style == Style.Creature ? .55f : style == Style.Glint ? 0 : style == Style.Character ? .12f : .2f);
            m.SetFloat("_OutlineWidth", alive ? 1.6f : 0);
            m.SetColor("_OutlineColor", Ink);
            m.SetShaderPassEnabled(OutlinePass, alive);
            if (style == Style.Glint) m.SetColor("_EmissionColor", new Color(.45f, .45f, .45f));
        }

        public static Material Glass(string name, Color tint, float edge = .35f, float glint = .6f)
        {
            var m = new Material(GlassShader) { name = "Diorama / " + name, renderQueue = 3000 };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_RimStrength", edge);
            m.SetFloat("_GlossStrength", glint);
            return m;
        }

        public static Material Unlit(string name, Color tint, bool additive, Texture texture = null, int queue = 3000)
        {
            var m = new Material(UnlitShader) { name = "Diorama / " + name, renderQueue = queue };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (texture) m.SetTexture("_MainTex", texture);
            return m;
        }

        /// <summary>The kit's shared DK_* slot colours (sRGB), as authored in Tools/Art/create_diorama_kit.py. A species' own
        /// colour slots (DK_<Species>Body, Fin, Belly, Accent, Blush) come from the variant atlas instead.</summary>
        public static readonly IReadOnlyDictionary<string, string> KitPalette = new Dictionary<string, string>
        {
            { "DK_EyeWhite", "FFFFFF" }, { "DK_Pupil", "1B413E" }, { "DK_Glint", "FFFFFF" },
            { "DK_Mouth", "5A1F22" }, { "DK_Gold", "D9B04A" },
            // GR-12 wave 2: the Rainbow Regent's middle tail bands.
            { "DK_RainbowYellow", "FFE27A" }, { "DK_RainbowGreen", "8FE08A" },
        };

        /// <summary>Slots that glow (bloom) as well as being lit: a Sparkline's accent, its lantern (GR-12: and what else its
        /// species paints with it, a glowing stripe, belly, fin tips or stars), and (GR-12 wave 2) a Cinderling's accent, its
        /// embers: the tips of its spines and its flames, a coal belly, a sizzling snout, glowing patches; and (GR-12 wave 3) a
        /// Saltscale's salt stripe, which glitters (its habit keeps it soft at rest, its dart flashes it); and (GR-12 wave 4) a
        /// Gloamer's lure and the spots along its sides, and a Cometail's bright head and comet tail.</summary>
        public static bool GlowSlot(string slot) =>
            DioramaVariants.TryParse(slot, out var species, out int column) && column == 3 &&
            (species.Family == DioramaFamily.Sparkline || species.Family == DioramaFamily.Cinderling || species.Family == DioramaFamily.Saltscale ||
             species.Family == DioramaFamily.Gloamer || species.Family == DioramaFamily.Cometail);

        /// <summary>A kit slot's colour: shared slots from the palette, a species' own slots from the atlas's Classic row.</summary>
        public static bool TryGetSlotColor(string slot, out Color color)
        {
            if (KitPalette.TryGetValue(slot, out var code)) { color = Hex(code); return true; }
            if (DioramaVariants.TryParse(slot, out var species, out int column) && DioramaVariants.Ready)
            {
                color = DioramaVariants.Color(species.Index, DioramaVariant.Classic, column); color.a = 1;
                return true;
            }
            color = default;
            return false;
        }

        /// <summary>Instantiates a kit model and gives every slot its own toon material (released by the caller).</summary>
        public static Transform InstantiateKit(string asset, Transform parent, Style style, List<Material> owned)
        {
            var source = Resources.Load<GameObject>(KitPath + asset);
            if (!source) return null;
            var root = UnityEngine.Object.Instantiate(source, parent, false).transform;
            root.name = "Diorama / " + asset;
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
            var made = new Dictionary<string, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slot = slots[i] ? slots[i].name.Replace(" (Instance)", "") : "";
                    if (!made.TryGetValue(slot, out var material))
                    {
                        if (!TryGetSlotColor(slot, out var color)) throw new InvalidOperationException("Unmapped diorama kit slot: " + slot);
                        material = Toon(slot, color, slot == "DK_Glint" ? Style.Glint : style);
                        if (GlowSlot(slot)) material.SetColor("_EmissionColor", color * .9f);
                        made.Add(slot, material); owned?.Add(material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            return root;
        }
    }
}
