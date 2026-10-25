using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// GR-16: the new game's look (GR.48, approved by the user on 2026-10-25): juicy soft colours, glossy plastic, cartoon
    /// glass, glowing cultures and screens, sunlight through the window by day and lamps by night. Wave 1 dresses the back
    /// room and shop unit A and their stations (growers, the incubator, Starter tanks, the counter) with the growth kit
    /// (Tools/Art/create_growth_kit.py, Resources/Art/GrowthKit) as decorative children: the greybox's shapes keep their
    /// colliders and targets, only their renderers are switched off, so positions, footprints, aisles and save ids stay as
    /// they are (AD.4). Every other greybox material takes the look's palette. -evolutionGrowthPlain keeps the greybox look.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        /// <summary>Whether the new game wears its look (off with -evolutionGrowthPlain, for comparing and measuring).</summary>
        public static readonly bool LookOn = !System.Environment.GetCommandLineArgs().Contains("-evolutionGrowthPlain");
        public const string KitPath = "Art/GrowthKit/";

        readonly Dictionary<string, Material> lookPalette = new Dictionary<string, Material>();
        /// <summary>The kit pieces placed so far (validation reads them by name).</summary>
        readonly List<Transform> lookPieces = new List<Transform>();
        public IReadOnlyList<Transform> LookPieces => lookPieces.Where(t => t != null).ToList();
        Light[] shopLamps = new Light[0];
        Light labLamp;
        Volume lookVolume;
        VolumeProfile lookProfile;
        readonly List<ReflectionProbe> lookProbes = new List<ReflectionProbe>();
        LookKey lookNow;
        bool lookStarted;
        int probeFrame = -1;
        GrowthPhase probePhase;
        public float LookSunIntensity => sun ? sun.intensity : 0;
        public string LookKeyName { get; private set; } = "";

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // ---------------------------------------------------------------- materials

        static Material Plastic(string name, Color c, float smooth = 0.62f) => ShopWorld.MakeMaterial("GR look " + name, c, 0, smooth);

        static Material Glowing(string name, Color c, float strength, float smooth = 0.5f)
        {
            var m = ShopWorld.MakeMaterial("GR look " + name, c, 0, smooth);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        /// <summary>Cartoon glass: nearly clear face on, a pale rim where it turns away (the classic look's glass shader).</summary>
        static Material LookGlass(string name, Color tint, float edge = 0.55f, float glint = 0.75f) => DioramaLook.Glass("GR look " + name, tint, edge, glint);

        /// <summary>The kit's material slots (GK_*, as Tools/Art/create_growth_kit.py names them): plastic (p, its smoothness),
        /// glowing (g, its emission), metal (m), cartoon glass (x, its alpha) and the culture's own (c, a grower's culture).</summary>
        public static readonly IReadOnlyDictionary<string, (char kind, string hex, float value)> LookSlots = new Dictionary<string, (char, string, float)>
        {
            ["GK_Cream"] = ('p', "#FFF3DF", 0.62f),
            ["GK_Teal"] = ('p', "#22BFB0", 0.62f),
            ["GK_TealDark"] = ('p', "#15867F", 0.62f),
            ["GK_TealLight"] = ('p', "#7FE3D6", 0.5f),
            ["GK_Panel"] = ('p', "#8FDDD0", 0.5f),
            ["GK_Yellow"] = ('p', "#FFC93C", 0.62f),
            ["GK_Coral"] = ('p', "#FF7A7A", 0.62f),
            ["GK_Pink"] = ('p', "#FF9EC4", 0.3f),
            ["GK_Lime"] = ('p', "#9BDB4D", 0.5f),
            ["GK_Green"] = ('p', "#56B947", 0.5f),
            ["GK_GreenDark"] = ('p', "#3E9A45", 0.5f),
            ["GK_Lilac"] = ('p', "#A98BFF", 0.62f),
            ["GK_Ink"] = ('p', "#2B3A55", 0.4f),
            ["GK_Wood"] = ('p', "#E39A5A", 0.38f),
            ["GK_WoodLight"] = ('p', "#EFB277", 0.38f),
            ["GK_WoodMid"] = ('p', "#DB8F4F", 0.38f),
            ["GK_WoodDark"] = ('p', "#B9713D", 0.38f),
            ["GK_Beam"] = ('p', "#A8612F", 0.32f),
            ["GK_Sand"] = ('p', "#F7DFAE", 0.25f),
            ["GK_Tile"] = ('p', "#FFF1DC", 0.72f),
            ["GK_Tile2"] = ('p', "#FFE4C4", 0.72f),
            ["GK_TileAccent"] = ('p', "#4FCDBE", 0.72f),
            ["GK_Gold"] = ('m', "#FFC93C", 0.78f),
            ["GK_Pebble1"] = ('p', "#FFC1B0", 0.45f),
            ["GK_Pebble2"] = ('p', "#C9B6FF", 0.45f),
            ["GK_Pebble3"] = ('p', "#FFE08A", 0.45f),
            ["GK_Pebble4"] = ('p', "#BDEFE8", 0.45f),
            ["GK_Rock"] = ('p', "#B9A7E8", 0.4f),
            ["GK_Soil"] = ('p', "#7A4E35", 0.1f),
            ["GK_Paper"] = ('p', "#FFFBEF", 0.15f),
            ["GK_Plaster"] = ('p', "#FFE8C6", 0.12f),
            ["GK_PlasterLab"] = ('p', "#DDF5EC", 0.12f),
            ["GK_Pavement"] = ('p', "#D9D2C6", 0.12f),
            ["GK_Grass"] = ('p', "#7CCF4E", 0.15f),
            ["GK_Sky"] = ('p', "#9ADBFF", 0.1f),
            ["GK_Glass"] = ('x', "#EBFAFF", 0.1f),
            ["GK_GlowWarm"] = ('g', "#FFF4D6", 3.2f),
            ["GK_GlowWhite"] = ('g', "#F5FCFF", 2.2f),
            ["GK_GlowLime"] = ('g', "#9BDB4D", 2.4f),
            ["GK_GlowTeal"] = ('g', "#7FE3D6", 2.0f),
            ["GK_GlowCoral"] = ('g', "#FF5E7E", 1.6f),
            ["GK_GlowBlue"] = ('g', "#3FA9FF", 1.6f),
            ["GK_GlowYellow"] = ('g', "#FFC93C", 2.4f),
            ["GK_Screen"] = ('g', "#0F3B4C", 0.6f),
            ["GK_GaugeFace"] = ('g', "#FFF8E6", 0.35f),
            ["GK_Culture"] = ('c', "#FF5E7E", 0),
            ["GK_Cloud"] = ('g', "#FFFFFF", 0.45f),
        };

        /// <summary>The material for a kit slot (GK_*), the look's own palette.</summary>
        public Material LookMaterial(string slot)
        {
            slot = slot.Replace(" (Instance)", "");
            if (lookPalette.TryGetValue(slot, out var found)) return found;
            if (!LookSlots.TryGetValue(slot, out var def)) throw new System.InvalidOperationException("Unmapped growth kit material: " + slot);
            Material m;
            switch (def.kind)
            {
                case 'g': m = Glowing(slot, C(def.hex), def.value, slot == "GK_Screen" ? 0.8f : 0.5f); break;
                case 'm': m = ShopWorld.MakeMaterial("GR look " + slot, C(def.hex), 0.8f, def.value); break;
                case 'x': { var c = C(def.hex); c.a = def.value; m = LookGlass(slot, c); break; }
                case 'c': m = culture["bloom"]; break;
                default: m = Plastic(slot, C(def.hex), def.value); break;
            }
            lookPalette[slot] = m;
            return m;
        }

        /// <summary>Gives the greybox's own materials the look's palette before anything is built with them: every room and
        /// station, dressed or not yet, wears the same colours (warm plaster, turquoise trim, cream stands, glossy cultures,
        /// cartoon glass and water).</summary>
        void RestyleForLook()
        {
            floorWood = Plastic("wood floor", C("#E39A5A"), 0.38f);
            floorB = Plastic("unit B floor", C("#EFB277"), 0.38f);
            floorTile = Plastic("tile floor", C("#FFF1DC"), 0.72f);
            wall = Plastic("shop wall", C("#FFE8C6"), 0.12f);
            wallLab = Plastic("lab wall", C("#DDF5EC"), 0.12f);
            trim = Plastic("trim", C("#22BFB0"));
            stand = Plastic("stand", C("#FFF3DF"));
            cabinet = Plastic("cabinet", C("#FFF3DF"));
            counterMat = Plastic("counter", C("#E39A5A"), 0.38f);
            paper = Plastic("paper", C("#FFFBEF"), 0.15f);
            marker = Plastic("place marker", C("#FFC93C"), 0.4f);
            partition = Plastic("partition", C("#F3E3CA"), 0.15f);
            sign = Plastic("sign", C("#FF7A7A"));
            gravel = Plastic("gravel", C("#F7DFAE"), 0.25f);
            glass = LookGlass("glass", new Color(0.92f, 0.98f, 1f, 0.1f));
            dome = LookGlass("dome", new Color(0.95f, 0.99f, 1f, 0.12f), 0.6f);
            water = LookGlass("water", new Color(0.36f, 0.78f, 0.92f, 0.26f), 0.4f, 0.5f);
            waterBrine = LookGlass("brine water", new Color(0.26f, 0.8f, 0.68f, 0.28f), 0.4f, 0.5f);
            waterWarm = LookGlass("warm water", new Color(0.98f, 0.7f, 0.4f, 0.26f), 0.4f, 0.5f);
            foreach (var c in GrowthContent.Cultures) culture[c.Id] = Glowing("culture " + c.Id, Hex(c.Hex), 0.55f, 0.75f);
        }

        /// <summary>The incubator's dome tinted by the dose in its tint port (Sync), in the look's glass.</summary>
        Material TintedDome(string name, Color tint) => LookOn ? LookGlass(name, tint, 0.6f) : Transparent(name, tint);

        // ---------------------------------------------------------------- kit pieces

        /// <summary>Places a kit asset under a station (at its origin) or the world, maps its GK_* slots onto the look's
        /// materials (GK_Culture onto a culture's when given), and switches off the renderers of the greybox parts it replaces
        /// (named children of the parent; their colliders and targets stay).</summary>
        Transform Kit(Transform parent, string asset, Vector3 offset = default, string cultureId = null, params string[] replaced)
        {
            var source = Resources.Load<GameObject>(KitPath + asset);
            if (!source) { Debug.LogWarning("Missing growth kit piece: " + asset); return null; }
            var piece = Instantiate(source, parent, false).transform;
            piece.name = "Look / " + asset;
            piece.localPosition = offset; piece.localRotation = Quaternion.identity; piece.localScale = Vector3.one;
            foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = mats[i] ? mats[i].name.Replace(" (Instance)", "") : "";
                    mats[i] = slot == "GK_Culture" && cultureId != null ? culture[cultureId] : LookMaterial(slot);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            foreach (var c in piece.GetComponentsInChildren<Collider>(true)) Destroy(c); // decoration only: the reticle never meets it
            Hide(parent, replaced);
            lookPieces.Add(piece);
            return piece;
        }

        static void Hide(Transform parent, params string[] names)
        {
            if (names == null) return;
            foreach (Transform child in parent)
                if (names.Contains(child.name))
                    foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        /// <summary>The rooms' dressing: floors, panelling, beams, frames, lamps, sign and shelf, and the street outside.</summary>
        void DressRooms()
        {
            var rooms = transform.Find("Rooms");
            Hide(rooms, "Unit A floor", "Back room floor", "Pavement");
            var dressing = new GameObject("Look rooms").transform; dressing.SetParent(transform, false); // no slash: Transform.Find reads one as a path
            foreach (var asset in new[] { "UnitAFloor", "UnitADressing", "Street", "BackRoomFloor", "BackRoomDressing" }) Kit(dressing, asset);
            // The lamps hang where the kit's pendants are; a second lamp for unit A, the back room's under its light strip.
            var a = transform.Find("Shop unit A lamp")?.GetComponent<Light>();
            if (a)
            {
                a.transform.localPosition = new Vector3(-1.4f, 2.35f, 1.2f);
                var b = Instantiate(a.gameObject, transform).GetComponent<Light>();
                b.name = "Shop unit A lamp 2"; b.transform.localPosition = new Vector3(1.7f, 2.35f, -1.0f);
                shopLamps = new[] { a, b };
            }
            labLamp = transform.Find("Back room lamp")?.GetComponent<Light>();
            if (labLamp) { labLamp.transform.localPosition = new Vector3(-1, 3.3f, 6.5f); labLamp.range = 10; }
        }

        void DressStations()
        {
            Kit(incubatorRoot, "Incubator", default, null, "Base", "Tray");
            Kit(counterRoot, "Counter", default, null, "Counter body", "Counter top", "Bell");
        }

        void DressSource(Transform root, string id, bool vat)
        {
            if (vat) return; // the vat comes with wave 2
            Kit(root, "Grower", default, id, "Cabinet", "Jar", "Label");
        }

        /// <summary>A Starter tank's cabinet and frame, pebbles and plants, and its level's filter and lid; the greybox's water,
        /// glass and gravel stay (in the look's materials). The harmony frame sits on the kit's rim, a little wider.</summary>
        void DressTank(Transform root, GrowthContainer c, float W, float D, float top, GameObject harmonyFrame)
        {
            if (c.Kind != "starter" || c.Def.Lab) return; // Large and Show tanks and racks come with wave 2
            Kit(root, "TankStarter", default, null, "Stand", "Rim");
            Kit(root, "TankBed", default, null, "Plant");
            if (c.Level >= 2) Kit(root, "TankFilter", default, null, "Filter");
            if (c.Level >= 3) Kit(root, "TankLid", default, null, "Lid");
            if (harmonyFrame)
            {
                harmonyFrame.transform.localPosition = new Vector3(0, top + 0.055f, 0);
                harmonyFrame.transform.localScale = new Vector3(W + 0.1f, 0.02f, D + 0.1f);
            }
        }

        /// <summary>A station's level parts in the kit's shapes (GR-06's Look rebuilds them when the level changes).</summary>
        void DressLevel(string key, Transform parts, int level)
        {
            void Replace() { foreach (var r in parts.GetComponentsInChildren<Renderer>(true)) r.enabled = false; }
            if (key == "incubator")
            {
                Replace();
                if (level >= 2) Kit(parts, "IncubatorScreen");
                if (level >= 3) Kit(parts, "IncubatorPort");
                if (level >= 4) Kit(parts, "IncubatorDoser");
            }
            else if (key == "counter")
            {
                Replace();
                if (level >= 2) Kit(parts, "CounterBags");
                if (level >= 3) Kit(parts, "CounterReader");
                if (level >= 4) Kit(parts, "CounterTill");
            }
            else if (key.StartsWith("grower:") && GrowthContent.Culture(key.Substring(7)).Source != CultureSource.Vat)
            {
                Replace();
                if (level >= 2) Kit(parts, "GrowerRing");
                if (level >= 3) Kit(parts, "GrowerTap");
                if (level >= 4) { Kit(parts, "GrowerTap", new Vector3(0.16f, 0, 0)); Kit(parts, "GrowerLid"); }
            }
        }

        // ---------------------------------------------------------------- light: night, morning, midday, closing

        struct LookKey
        {
            public Vector2 Sun; // pitch, yaw
            public Color SunColor, Sky, Equator, Ground, Background;
            public float SunI, Lamps, Strip, Exposure, Temperature, Bloom;

            public static LookKey Lerp(LookKey a, LookKey b, float t) => new LookKey
            {
                Sun = Vector2.Lerp(a.Sun, b.Sun, t), SunColor = Color.Lerp(a.SunColor, b.SunColor, t), Sky = Color.Lerp(a.Sky, b.Sky, t),
                Equator = Color.Lerp(a.Equator, b.Equator, t), Ground = Color.Lerp(a.Ground, b.Ground, t), Background = Color.Lerp(a.Background, b.Background, t),
                SunI = Mathf.Lerp(a.SunI, b.SunI, t), Lamps = Mathf.Lerp(a.Lamps, b.Lamps, t), Strip = Mathf.Lerp(a.Strip, b.Strip, t),
                Exposure = Mathf.Lerp(a.Exposure, b.Exposure, t), Temperature = Mathf.Lerp(a.Temperature, b.Temperature, t), Bloom = Mathf.Lerp(a.Bloom, b.Bloom, t),
            };
        }

        // Production happens at night (GrowthPhase.Night), so night stays bright enough to work by: the lamps carry the rooms,
        // the moon and a navy sky outside. The shop's day runs morning, midday, closing by its clock.
        static readonly LookKey NightKey = new LookKey
        {
            Sun = new Vector2(48, 20), SunColor = C("#A9C0FF"), SunI = 0.35f, Sky = C("#8C90B8"), Equator = C("#B39C8C"), Ground = C("#5E504A"),
            Background = C("#1E2A55"), Lamps = 2.4f, Strip = 2.4f, Exposure = 0.2f, Temperature = -2, Bloom = 0.5f,
        };
        static readonly LookKey MorningKey = new LookKey
        {
            Sun = new Vector2(26, -38), SunColor = C("#FFD9A6"), SunI = 2.4f, Sky = C("#88AFCC"), Equator = C("#AE9C88"), Ground = C("#5E5044"),
            Background = C("#BFE6FF"), Lamps = 0.9f, Strip = 1.4f, Exposure = 0.05f, Temperature = 8, Bloom = 0.35f,
        };
        static readonly LookKey MiddayKey = new LookKey
        {
            Sun = new Vector2(52, -8), SunColor = C("#FFF1DC"), SunI = 2.8f, Sky = C("#92B8D4"), Equator = C("#AFA390"), Ground = C("#5E5446"),
            Background = C("#9ADBFF"), Lamps = 0.6f, Strip = 1.3f, Exposure = 0, Temperature = 0, Bloom = 0.3f,
        };
        static readonly LookKey ClosingKey = new LookKey
        {
            Sun = new Vector2(16, 40), SunColor = C("#FFB070"), SunI = 2.2f, Sky = C("#9C88B0"), Equator = C("#B08A74"), Ground = C("#52403A"),
            Background = C("#FFC9A0"), Lamps = 1.6f, Strip = 1.5f, Exposure = 0.05f, Temperature = 16, Bloom = 0.45f,
        };

        /// <summary>The key for the shop's phase and how far its day has run (validation names it).</summary>
        static LookKey Target(GrowthShop shop, out string name)
        {
            if (shop.Phase == GrowthPhase.Night) { name = "night"; return NightKey; }
            float p = 1 - Mathf.Clamp01(shop.DaySecondsLeft / Mathf.Max(1, GrowthContent.DaySeconds));
            if (p < 0.25f) { name = "morning"; return LookKey.Lerp(MorningKey, MiddayKey, p / 0.25f); }
            if (p < 0.7f) { name = "midday"; return MiddayKey; }
            name = "closing"; return LookKey.Lerp(MiddayKey, ClosingKey, (p - 0.7f) / 0.3f);
        }

        void BuildLookLight()
        {
            sun.shadows = LightShadows.Soft;
            // The reflection probes below are rendered at run time; the project keeps realtime probes off for the classic game.
            QualitySettings.realtimeReflectionProbes = true;
            sun.shadowStrength = 0.8f;
            lookProfile = ScriptableObject.CreateInstance<VolumeProfile>(); lookProfile.name = "GR look grade";
            // Only effects the project's own profiles already use (Bloom HQ, Neutral tonemapping, colour adjustments, white
            // balance, vignette), so the build keeps their shader variants.
            var bloom = lookProfile.Add<Bloom>(true);
            bloom.threshold.Override(1f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f); bloom.highQualityFiltering.Override(true);
            lookProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var color = lookProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0); color.contrast.Override(8); color.saturation.Override(14); color.colorFilter.Override(Color.white);
            lookProfile.Add<WhiteBalance>(true).temperature.Override(0);
            var vignette = lookProfile.Add<Vignette>(true); vignette.intensity.Override(0.14f); vignette.smoothness.Override(0.45f);
            var go = new GameObject("Look / grade"); go.transform.SetParent(transform, false);
            lookVolume = go.AddComponent<Volume>(); lookVolume.isGlobal = true; lookVolume.priority = 40; lookVolume.sharedProfile = lookProfile;
            // A reflection probe in each dressed room, rendered once the room is built and again when night turns to day, so
            // the glossy plastic reflects the room rather than nothing.
            foreach (var (name, center, size) in new[] { ("unit A", new Vector3(0, 1.6f, 0), new Vector3(8, 3.2f, 8)), ("back room", new Vector3(-1, 1.8f, 6.5f), new Vector3(6, 3.6f, 5)) })
            {
                var probe = new GameObject("Look / reflections (" + name + ")").AddComponent<ReflectionProbe>();
                probe.transform.SetParent(transform, false); probe.transform.localPosition = center;
                probe.size = size; probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce; probe.resolution = 128; probe.boxProjection = true;
                probe.hdr = true; probe.importance = 1; probe.farClipPlane = 20;
                lookProbes.Add(probe);
            }
        }

        void TickLook(GrowthShop shop, float dt)
        {
            if (lookVolume == null) return;
            var target = Target(shop, out var keyName);
            LookKeyName = keyName;
            if (!lookStarted)
            {
                lookStarted = true; lookNow = target; probeFrame = Time.frameCount + 2; probePhase = shop.Phase;
                var cam = Camera.main;
                if (cam) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
            else lookNow = LookKey.Lerp(lookNow, target, 1 - Mathf.Exp(-dt / 0.6f));
            sun.transform.rotation = Quaternion.Euler(lookNow.Sun.x, lookNow.Sun.y, 0);
            sun.color = lookNow.SunColor; sun.intensity = lookNow.SunI;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = lookNow.Sky; RenderSettings.ambientEquatorColor = lookNow.Equator; RenderSettings.ambientGroundColor = lookNow.Ground;
            foreach (var l in shopLamps) if (l) l.intensity = lookNow.Lamps;
            if (labLamp) labLamp.intensity = lookNow.Strip;
            var cam2 = Camera.main;
            if (cam2) cam2.backgroundColor = lookNow.Background;
            if (lookProfile.TryGet<ColorAdjustments>(out var color)) color.postExposure.value = lookNow.Exposure;
            if (lookProfile.TryGet<WhiteBalance>(out var balance)) balance.temperature.value = lookNow.Temperature;
            if (lookProfile.TryGet<Bloom>(out var bloom)) bloom.intensity.value = lookNow.Bloom;
            // Reflections: once the rooms stand, and again a moment after night and day change over (the blend has settled).
            if (shop.Phase != probePhase) { probePhase = shop.Phase; probeFrame = Time.frameCount + 150; }
            if (probeFrame > 0 && Time.frameCount >= probeFrame)
            {
                probeFrame = -1;
                foreach (var p in lookProbes) if (p) p.RenderProbe();
            }
        }
    }
}
