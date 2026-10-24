using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>AD-A Pocket Aquarium Diorama: shared shaders, toon materials and the slice's palette. Presentation only.</summary>
    public static class DioramaLook
    {
        /// <summary>Whether the diorama look is on. The world owns it (ShopWorld.SetDioramaLook); actors built later, such as
        /// customers, read it. Only tests set it directly.</summary>
        public static bool Active { get; set; }

        public const string ToonResource = "EvolutionDioramaToon", GlassResource = "EvolutionDioramaGlass",
            UnlitResource = "EvolutionDioramaUnlit", WindowResource = "EvolutionDioramaWindow";
        public const string ToonShaderName = "EvolutionShop/Diorama Toon", GlassShaderName = "EvolutionShop/Diorama Glass",
            UnlitShaderName = "EvolutionShop/Diorama Unlit", WindowShaderName = "EvolutionShop/Diorama Window";
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
        public static Shader WindowShader => Load(WindowResource);

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
            { "DK_Blush", "FF9EA0" }, { "DK_EyeWhite", "FFFFFF" }, { "DK_Pupil", "1B413E" }, { "DK_Glint", "FFFFFF" },
            { "DK_CasualKnit", "F08A3C" }, { "DK_Collar", "FFF6DF" }, { "DK_Trousers", "2B3B4F" }, { "DK_Shoes", "3B2A22" },
            { "DK_Skin", "B9774E" }, { "DK_Beret", "1B2B33" }, { "DK_Nub", "D66145" }, { "DK_Tote", "E9D6A8" }, { "DK_Hair", "3B2A22" },
            { "DK_Mouth", "5A1F22" }, { "DK_Button", "D66145" }, { "DK_Gold", "D9B04A" }, { "DK_Lens", "CFEFF2" },
            // AD-C: the other three customers (never teal) and the crew (always shop teal).
            { "DK_BargainKnit", "6E9B4A" }, { "DK_BargainScarf", "E8B83A" }, { "DK_BargainTrousers", "6B4E32" }, { "DK_BargainShoes", "3A2A1E" },
            { "DK_BargainSkin", "E3AE88" }, { "DK_BargainHair", "8C5A2B" }, { "DK_BargainCap", "C98F3A" }, { "DK_BargainPurse", "C8553D" },
            { "DK_CollectorCoat", "7A4A8C" }, { "DK_CollectorTrousers", "2E2638" }, { "DK_CollectorShoes", "1E1A22" }, { "DK_CollectorSkin", "7A4E33" },
            { "DK_CollectorHair", "D8D2C8" }, { "DK_CollectorHat", "2A2230" }, { "DK_CollectorBow", "C9473F" }, { "DK_CollectorHandle", "6B3F2A" },
            { "DK_PremiumCoat", "EDE3CC" }, { "DK_PremiumTrim", "2B2B33" }, { "DK_PremiumTrousers", "1E1E24" }, { "DK_PremiumShoes", "141418" },
            { "DK_PremiumSkin", "C08A63" }, { "DK_PremiumHair", "2B2118" }, { "DK_PremiumHat", "18181C" }, { "DK_PremiumBag", "E8C25A" },
            { "DK_PremiumFlower", "E8628C" },
            { "DK_CrewTeal", "1F7A72" }, { "DK_CrewCream", "F4E9CF" }, { "DK_CrewBadge", "E8664F" }, { "DK_CrewTrousers", "33404D" }, { "DK_CrewShoes", "3B2A22" },
            { "DK_PipSkin", "D09466" }, { "DK_PipHair", "3B2A22" }, { "DK_Pencil", "F2C14E" }, { "DK_Stool", "C98F52" },
            { "DK_NoriSkin", "8A5A3C" }, { "DK_NoriHair", "1E1612" }, { "DK_NoriGlove", "C9A06A" },
            { "DK_MossSkin", "F0C8A0" }, { "DK_MossHair", "5E8F3A" }, { "DK_MossGoggle", "2B3B4F" },
            { "DK_Crate", "C99A62" }, { "DK_CrateSlat", "A87A48" },
            // GR-12 wave 2: the Rainbow Regent's middle tail bands.
            { "DK_RainbowYellow", "FFE27A" }, { "DK_RainbowGreen", "8FE08A" },
        };

        /// <summary>Slots that glow (bloom) as well as being lit: a Sparkline's accent, its lantern (GR-12: and what else its
        /// species paints with it, a glowing stripe, belly, fin tips or stars), and (GR-12 wave 2) a Cinderling's accent, its
        /// embers: the tips of its spines and its flames, a coal belly, a sizzling snout, glowing patches.</summary>
        public static bool GlowSlot(string slot) =>
            DioramaVariants.TryParse(slot, out var species, out int column) && column == 3 &&
            (species.Family == DioramaFamily.Sparkline || species.Family == DioramaFamily.Cinderling);

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

    public enum DioramaKey { Morning, Midday, Closing, Night }

    /// <summary>One lighting key of the shop: sun, ambient, fills, the street seen through the window and each room's grade.</summary>
    public struct DioramaLightKey
    {
        public Color Sun, AmbientSky, AmbientEquator, AmbientGround, ShopFill, LabFill, WindowLight;
        public float SunIntensity, SunPitch, SunYaw, ShopFillIntensity, LabFillIntensity, WindowLightIntensity;
        public Color SkyTop, SkyBottom, Silhouette, Lamp, Stars;
        public float LampsOn, TankGlow, Bloom;
        public Color ShopFilter, LabFilter;
        public float ShopTemperature, ShopExposure, ShopSaturation, LabTemperature, LabExposure, LabSaturation;
    }

    /// <summary>Pure: the four lighting keys and how the day phase and the trading day's progress pick between them.</summary>
    public static class DioramaLighting
    {
        public const float PhaseBlendSeconds = 1.6f;
        static Color C(float r, float g, float b) => new Color(r, g, b, 1);

        public static DioramaLightKey Key(DioramaKey key)
        {
            switch (key)
            {
                case DioramaKey.Morning: return new DioramaLightKey {
                    Sun = C(1f, .9f, .76f), SunIntensity = 1.35f, SunPitch = 26, SunYaw = -48,
                    AmbientSky = C(.66f, .76f, .9f), AmbientEquator = C(.6f, .58f, .54f), AmbientGround = C(.34f, .31f, .28f),
                    ShopFill = C(1f, .95f, .86f), ShopFillIntensity = 2.4f, LabFill = C(.76f, 1f, .95f), LabFillIntensity = 2.9f,
                    WindowLight = C(1f, .88f, .7f), WindowLightIntensity = 4.2f,
                    SkyTop = C(.55f, .76f, .95f), SkyBottom = C(1f, .86f, .72f), Silhouette = C(.47f, .64f, .62f), Lamp = Color.black, Stars = Color.black,
                    LampsOn = .15f, TankGlow = 0, Bloom = .45f,
                    ShopFilter = C(1f, .97f, .94f), ShopTemperature = -6, ShopExposure = .05f, ShopSaturation = 6,
                    LabFilter = C(.92f, 1f, .98f), LabTemperature = -14, LabExposure = .05f, LabSaturation = 4 };
                case DioramaKey.Midday: return new DioramaLightKey {
                    Sun = C(1f, .97f, .9f), SunIntensity = 1.6f, SunPitch = 58, SunYaw = -14,
                    AmbientSky = C(.74f, .8f, .88f), AmbientEquator = C(.64f, .6f, .52f), AmbientGround = C(.36f, .32f, .27f),
                    ShopFill = C(1f, .96f, .88f), ShopFillIntensity = 2.6f, LabFill = C(.78f, 1f, .95f), LabFillIntensity = 3.0f,
                    WindowLight = C(1f, .97f, .9f), WindowLightIntensity = 3.2f,
                    SkyTop = C(.36f, .66f, .95f), SkyBottom = C(.8f, .92f, 1f), Silhouette = C(.38f, .62f, .52f), Lamp = Color.black, Stars = Color.black,
                    LampsOn = 0, TankGlow = 0, Bloom = .4f,
                    ShopFilter = C(1f, .98f, .95f), ShopTemperature = 0, ShopExposure = .1f, ShopSaturation = 8,
                    LabFilter = C(.92f, 1f, .98f), LabTemperature = -12, LabExposure = .1f, LabSaturation = 5 };
                case DioramaKey.Closing: return new DioramaLightKey {
                    Sun = C(1f, .66f, .4f), SunIntensity = 1.25f, SunPitch = 16, SunYaw = 52,
                    AmbientSky = C(.62f, .55f, .62f), AmbientEquator = C(.62f, .48f, .38f), AmbientGround = C(.32f, .24f, .2f),
                    ShopFill = C(1f, .78f, .52f), ShopFillIntensity = 2.6f, LabFill = C(.8f, .98f, .92f), LabFillIntensity = 2.7f,
                    WindowLight = C(1f, .62f, .36f), WindowLightIntensity = 4.6f,
                    SkyTop = C(.55f, .42f, .66f), SkyBottom = C(1f, .66f, .42f), Silhouette = C(.3f, .27f, .35f), Lamp = C(2.4f, 1.6f, .7f), Stars = Color.black,
                    LampsOn = 1, TankGlow = .25f, Bloom = .6f,
                    ShopFilter = C(1f, .9f, .8f), ShopTemperature = 18, ShopExposure = 0, ShopSaturation = 10,
                    LabFilter = C(.96f, 1f, .96f), LabTemperature = -6, LabExposure = 0, LabSaturation = 5 };
                default: return new DioramaLightKey {
                    Sun = C(.55f, .66f, 1f), SunIntensity = .42f, SunPitch = 48, SunYaw = 30,
                    AmbientSky = C(.32f, .38f, .55f), AmbientEquator = C(.3f, .3f, .38f), AmbientGround = C(.16f, .15f, .18f),
                    ShopFill = C(1f, .8f, .58f), ShopFillIntensity = 2.0f, LabFill = C(.72f, .96f, 1f), LabFillIntensity = 2.9f,
                    WindowLight = C(.5f, .58f, .9f), WindowLightIntensity = 1.2f,
                    SkyTop = C(.04f, .07f, .18f), SkyBottom = C(.12f, .17f, .34f), Silhouette = C(.05f, .06f, .1f), Lamp = C(3.2f, 2.2f, 1f), Stars = C(.9f, .92f, 1f),
                    LampsOn = 1, TankGlow = 1, Bloom = .75f,
                    ShopFilter = C(.9f, .9f, 1f), ShopTemperature = -4, ShopExposure = -.12f, ShopSaturation = 4,
                    LabFilter = C(.9f, .98f, 1f), LabTemperature = -10, LabExposure = -.05f, LabSaturation = 4 };
            }
        }

        static Color L(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);
        static float L(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);

        public static DioramaLightKey Blend(DioramaLightKey a, DioramaLightKey b, float t)
        {
            t = float.IsNaN(t) || float.IsInfinity(t) ? 0 : Mathf.Clamp01(t);
            return new DioramaLightKey {
                Sun = L(a.Sun, b.Sun, t), AmbientSky = L(a.AmbientSky, b.AmbientSky, t), AmbientEquator = L(a.AmbientEquator, b.AmbientEquator, t),
                AmbientGround = L(a.AmbientGround, b.AmbientGround, t), ShopFill = L(a.ShopFill, b.ShopFill, t), LabFill = L(a.LabFill, b.LabFill, t),
                WindowLight = L(a.WindowLight, b.WindowLight, t), SunIntensity = L(a.SunIntensity, b.SunIntensity, t),
                SunPitch = L(a.SunPitch, b.SunPitch, t), SunYaw = L(a.SunYaw, b.SunYaw, t),
                ShopFillIntensity = L(a.ShopFillIntensity, b.ShopFillIntensity, t), LabFillIntensity = L(a.LabFillIntensity, b.LabFillIntensity, t),
                WindowLightIntensity = L(a.WindowLightIntensity, b.WindowLightIntensity, t),
                SkyTop = L(a.SkyTop, b.SkyTop, t), SkyBottom = L(a.SkyBottom, b.SkyBottom, t), Silhouette = L(a.Silhouette, b.Silhouette, t),
                Lamp = L(a.Lamp, b.Lamp, t), Stars = L(a.Stars, b.Stars, t), LampsOn = L(a.LampsOn, b.LampsOn, t),
                TankGlow = L(a.TankGlow, b.TankGlow, t), Bloom = L(a.Bloom, b.Bloom, t),
                ShopFilter = L(a.ShopFilter, b.ShopFilter, t), LabFilter = L(a.LabFilter, b.LabFilter, t),
                ShopTemperature = L(a.ShopTemperature, b.ShopTemperature, t), ShopExposure = L(a.ShopExposure, b.ShopExposure, t),
                ShopSaturation = L(a.ShopSaturation, b.ShopSaturation, t), LabTemperature = L(a.LabTemperature, b.LabTemperature, t),
                LabExposure = L(a.LabExposure, b.LabExposure, t), LabSaturation = L(a.LabSaturation, b.LabSaturation, t) };
        }

        /// <summary>
        /// The light of a moment. Night and Closing are their own keys; the open day starts as morning, turns to midday by a
        /// third of the way through and leans toward evening near its end, so a glance at the window tells the time.
        /// openProgress is 0 at opening and 1 when the trading time runs out.
        /// </summary>
        public static DioramaLightKey Resolve(ShopDayPhase phase, float openProgress)
        {
            if (phase == ShopDayPhase.Night) return Key(DioramaKey.Night);
            if (phase == ShopDayPhase.Closing) return Key(DioramaKey.Closing);
            float p = float.IsNaN(openProgress) || float.IsInfinity(openProgress) ? 0 : Mathf.Clamp01(openProgress);
            if (p < .35f) return Blend(Key(DioramaKey.Morning), Key(DioramaKey.Midday), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .35f, p)));
            if (p < .8f) return Key(DioramaKey.Midday);
            return Blend(Key(DioramaKey.Midday), Key(DioramaKey.Closing), .55f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1f, p)));
        }

        /// <summary>Which named key a moment is closest to (for captures, tests and the review guide).</summary>
        public static DioramaKey Nearest(ShopDayPhase phase, float openProgress)
        {
            if (phase == ShopDayPhase.Night) return DioramaKey.Night;
            if (phase == ShopDayPhase.Closing) return DioramaKey.Closing;
            return openProgress < .2f ? DioramaKey.Morning : DioramaKey.Midday;
        }

        /// <summary>0 in the shop, 1 in the lab: the doorway at z 0.5 blends the two room grades over 1.4 m.</summary>
        public static float LabWeight(Vector3 viewer)
        {
            if (float.IsNaN(viewer.z) || float.IsInfinity(viewer.z)) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.2f, 1.2f, viewer.z));
        }
    }

    public struct DioramaHatchPose
    {
        public float Squash, Glow, Shiver, Stretch, Lift, TurnToViewer, Blink, Happy, Surprised, RingFlash;
        public bool Frozen;
    }

    /// <summary>
    /// Pure: the hatch hero moment of AD.5. First discovery (1.4 s): wind-up, a 0.08 s presentation-only freeze beat, pop with a
    /// bubble cloud, pose toward the player, settle. A repeat (0.85 s) is wind-up, pop and settle. Reduced motion has no freeze,
    /// flash or shiver. The game clock and collection never wait for any of it.
    /// </summary>
    public static class DioramaHatch
    {
        public const float FirstDuration = 1.4f, RepeatDuration = .85f, FreezeSeconds = .08f;
        public static float Duration(bool first) => first ? FirstDuration : RepeatDuration;
        public static float WindUpEnd(bool first) => first ? .3f : .22f;
        public static float PopTime(bool first, bool reduced) => WindUpEnd(first) + (first && !reduced ? FreezeSeconds : 0);
        public static float PoseStart(bool first) => first ? .7f : float.PositiveInfinity;
        public static float SettleStart(bool first) => first ? 1.1f : .5f;

        static float Ease(float t) => t * t * (3 - 2 * t);
        static float Range(float age, float from, float to) => Mathf.Clamp01((age - from) / Mathf.Max(.0001f, to - from));

        public static DioramaHatchPose Resolve(float age, bool first, bool reduced)
        {
            var pose = new DioramaHatchPose();
            if (float.IsNaN(age) || float.IsInfinity(age) || age < 0 || age >= Duration(first)) return pose;
            float windEnd = WindUpEnd(first), pop = PopTime(first, reduced), settle = SettleStart(first);
            float strength = reduced ? .35f : first ? 1 : .75f;
            if (age < windEnd)
            {
                float w = Ease(Range(age, 0, windEnd));
                pose.Squash = w * strength; pose.Glow = w; pose.Shiver = reduced ? 0 : w;
                pose.Surprised = w * .6f;
                return pose;
            }
            if (age < pop)
            {
                pose.Frozen = true; pose.Squash = strength; pose.Glow = 1; pose.RingFlash = 1; pose.Surprised = .6f;
                return pose;
            }
            float popEnd = first ? .7f : settle;
            if (age < popEnd)
            {
                float p = Range(age, pop, popEnd);
                // Released squash overshoots into a stretch, then springs back toward rest.
                pose.Squash = strength * Mathf.Lerp(1, -.55f, Ease(Mathf.Min(1, p * 2.2f))) * (1 - Ease(Mathf.Max(0, p * 1.6f - .6f)));
                pose.Stretch = strength * Mathf.Sin(Mathf.Min(1, p * 1.4f) * Mathf.PI) * .18f;
                pose.Lift = strength * Mathf.Sin(p * Mathf.PI) * .05f;
                pose.Glow = 1 - Ease(p);
                pose.Surprised = Mathf.Lerp(.6f, 0, Ease(p));
                pose.Happy = Ease(p) * (first ? .4f : .8f);
                return pose;
            }
            if (first && age < settle)
            {
                float p = Range(age, .7f, settle);
                pose.TurnToViewer = reduced ? 0 : Ease(Mathf.Min(1, p * 2.5f));
                pose.Happy = 1;
                // One deliberate blink while it looks at you.
                pose.Blink = Mathf.Clamp01(1 - Mathf.Abs(p - .55f) / .12f);
                return pose;
            }
            float s = Range(age, settle, Duration(first));
            pose.TurnToViewer = first && !reduced ? 1 - Ease(s) : 0;
            pose.Happy = (first ? 1 : .8f) * (1 - Ease(s));
            return pose;
        }

        /// <summary>The pose clock: real time, except that it holds still through the freeze beat.</summary>
        public static float AnimationAge(float age, bool first, bool reduced)
        {
            if (!first || reduced || age < WindUpEnd(true)) return age;
            return age < WindUpEnd(true) + FreezeSeconds ? WindUpEnd(true) : age - FreezeSeconds;
        }

        /// <summary>Burst sizes for the flipbook particles at the pop. Low detail halves them; Reduced keeps a few bubbles.</summary>
        public static void BurstCounts(bool first, bool low, bool reduced, out int bubbles, out int puffs, out int sparkles)
        {
            if (reduced) { bubbles = 6; puffs = 0; sparkles = 0; return; }
            bubbles = first ? 40 : 18; puffs = first ? 3 : 1; sparkles = first ? 12 : 4;
            if (low) { bubbles /= 2; puffs = Mathf.Max(1, puffs - 1); sparkles /= 2; }
        }
    }
}
