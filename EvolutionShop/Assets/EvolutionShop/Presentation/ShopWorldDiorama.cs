using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// AD-A art slice: the Pocket Aquarium Diorama look on the display corner (tank-a and tank-b beside it) and the incubator, the kit Pondlet
    /// and Casual visitor, URP bloom with a warm shop grade and a cool lab grade, and four lighting keys shown through a
    /// street window. Switching it off restores every material, mesh, light and shadow it changed. Presentation only:
    /// station roots, colliders, labels, saves and every rule are untouched.
    /// </summary>
    public sealed partial class ShopWorld
    {
        sealed class DioramaSwap { public Renderer Renderer; public Material[] Classic, Diorama; }

        readonly List<DioramaSwap> dioramaSwaps = new List<DioramaSwap>();
        readonly Dictionary<(Material, int), Material> dioramaMaterialMap = new Dictionary<(Material, int), Material>();
        readonly List<Object> dioramaOwned = new List<Object>();
        readonly List<(Renderer renderer, ShadowCastingMode mode)> dioramaShadowRestore = new List<(Renderer, ShadowCastingMode)>();
        readonly List<(Light light, float intensity)> dioramaPendants = new List<(Light, float)>();
        readonly List<Light> dioramaWindowLights = new List<Light>();
        bool dioramaActive, dioramaBuilt, dioramaClassicPost, dioramaKeyKnown;
        Camera dioramaCamera;
        Transform dioramaRoot;
        Volume dioramaShopVolume, dioramaLabVolume;
        VolumeProfile dioramaShopProfile, dioramaLabProfile;
        Material dioramaWindowMaterial, dioramaTankWater, dioramaIncubatorFluid, dioramaDome;
        Texture2D dioramaStreetMask;
        Light dioramaTankGlow, dioramaHatchLight, dioramaPortholeFill;
        readonly List<(MeshFilter filter, Mesh classic)> dioramaWaterFilters = new List<(MeshFilter, Mesh)>();
        Mesh dioramaWaterMesh;
        AmbientMode dioramaClassicAmbientMode;
        Color dioramaClassicSky, dioramaClassicEquator, dioramaClassicGround;
        Quaternion dioramaClassicSun;
        ShopDayPhase dioramaPhase = ShopDayPhase.Night;
        float dioramaOpenProgress, dioramaBlendAge = 99;
        DioramaLightKey dioramaFrom, dioramaTarget, dioramaShown;
        Vector3 dioramaViewer;

        public bool DioramaActive => dioramaActive;
        public DioramaLightKey DioramaShownKey => dioramaShown;
        public float DioramaLabWeight { get; private set; }
        public int DioramaSwapCount => dioramaSwaps.Count;
        public Volume DioramaShopVolume => dioramaShopVolume;
        public Volume DioramaLabVolume => dioramaLabVolume;

        /// <summary>Switch the AD-A look on or off. Off restores the classic presentation exactly.</summary>
        public void SetDioramaLook(bool on, Camera camera)
        {
            if (on == dioramaActive) return;
            if (on)
            {
                if (!dioramaBuilt) BuildDiorama();
                dioramaActive = true; DioramaLook.Active = true;
                foreach (var swap in dioramaSwaps) if (swap.Renderer) swap.Renderer.sharedMaterials = swap.Diorama;
                foreach (var (filter, _) in dioramaWaterFilters) if (filter) filter.sharedMesh = dioramaWaterMesh;
                // A diorama has no lid: the room shell stops blocking the key light so the sun can tell the time.
                dioramaShadowRestore.Clear();
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer || renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
                    var b = renderer.bounds;
                    if (b.size.y < 3f && b.min.y < 3.5f) continue;
                    dioramaShadowRestore.Add((renderer, renderer.shadowCastingMode));
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                dioramaClassicAmbientMode = RenderSettings.ambientMode; dioramaClassicSky = RenderSettings.ambientSkyColor;
                dioramaClassicEquator = RenderSettings.ambientEquatorColor; dioramaClassicGround = RenderSettings.ambientGroundColor;
                dioramaClassicSun = keyLight ? keyLight.transform.rotation : Quaternion.identity;
                dioramaCamera = camera;
                if (camera)
                {
                    var data = camera.GetUniversalAdditionalCameraData();
                    dioramaClassicPost = data.renderPostProcessing;
                    data.renderPostProcessing = true;
                }
                dioramaShopVolume.enabled = dioramaLabVolume.enabled = true;
                dioramaRoot.gameObject.SetActive(true);
                dioramaKeyKnown = false;
                TickDioramaLight(0);
            }
            else
            {
                ClearDioramaHatch();
                dioramaActive = false; DioramaLook.Active = false;
                foreach (var swap in dioramaSwaps) if (swap.Renderer) swap.Renderer.sharedMaterials = swap.Classic;
                foreach (var (filter, classic) in dioramaWaterFilters) if (filter) filter.sharedMesh = classic;
                foreach (var (renderer, mode) in dioramaShadowRestore) if (renderer) renderer.shadowCastingMode = mode;
                dioramaShadowRestore.Clear();
                RenderSettings.ambientMode = dioramaClassicAmbientMode; RenderSettings.ambientSkyColor = dioramaClassicSky;
                RenderSettings.ambientEquatorColor = dioramaClassicEquator; RenderSettings.ambientGroundColor = dioramaClassicGround;
                if (keyLight) keyLight.transform.rotation = dioramaClassicSun;
                foreach (var (light, intensity) in dioramaPendants) if (light) light.intensity = intensity;
                if (dioramaCamera) dioramaCamera.GetUniversalAdditionalCameraData().renderPostProcessing = dioramaClassicPost;
                dioramaShopVolume.enabled = dioramaLabVolume.enabled = false;
                dioramaRoot.gameObject.SetActive(false);
                if (roomLightsInitialized) RestoreRoomPhase(roomTargetPhase);
                if (lastModel != null) RefreshHatcheryLamp(lastModel.Incubator.Phase);
            }
            RebuildDioramaPondlets();
        }

        /// <summary>The presentation clock of the look: day phase and how far the trading day has run (0..1).</summary>
        public void SetDioramaTime(ShopDayPhase phase, float openProgress, Vector3 viewer)
        {
            if (phase != dioramaPhase && dioramaKeyKnown) { dioramaFrom = dioramaShown; dioramaBlendAge = 0; }
            dioramaPhase = phase; dioramaOpenProgress = openProgress; dioramaViewer = viewer;
        }

        void RebuildDioramaPondlets()
        {
            bool any = false;
            foreach (var display in displays.Values)
            {
                if (display.SpeciesId != "pondlet") continue;
                if (display.Incubator && fxCues != null) CancelFxOwner(fxCues.Find("incubator"));
                SetCreature(display, null); any = true;
            }
            if (any && lastModel != null) Refresh(lastModel);
        }

        /// <summary>The classic material's diorama equivalent (made once); returns it unchanged while the look is off.</summary>
        Material DioramaFor(Material classic, DioramaLook.Style style = DioramaLook.Style.Prop, int caustic = 0)
        {
            if (!dioramaActive || !classic) return classic;
            return DioramaMaterial(classic, style, caustic);
        }

        Material DioramaMaterial(Material classic, DioramaLook.Style style, int caustic)
        {
            if (dioramaMaterialMap.TryGetValue((classic, caustic), out var made)) return made;
            Color color = classic.HasProperty("_BaseColor") ? classic.GetColor("_BaseColor") : classic.color;
            if (classic.renderQueue >= 3000)
            {
                bool watery = classic == water;
                made = DioramaLook.Glass(classic.name, watery ? new Color(.3f, .78f, .88f, .14f) : new Color(.78f, .95f, 1f, .05f),
                    watery ? .22f : .38f, watery ? .45f : .75f);
            }
            else
            {
                color.a = 1;
                made = DioramaLook.Toon(classic.name, color, style);
                if (classic.IsKeywordEnabled("_EMISSION") && classic.HasProperty("_EmissionColor"))
                    made.SetColor("_EmissionColor", classic.GetColor("_EmissionColor") * 2.2f);
                if (caustic > 0) made.SetFloat("_CausticStrength", caustic * .25f);
            }
            dioramaMaterialMap.Add((classic, caustic), made);
            dioramaOwned.Add(made);
            return made;
        }

        void BuildDiorama()
        {
            dioramaBuilt = true;
            dioramaRoot = Group("Diorama look (AD-A)", transform, Vector3.zero);
            var tank = displays["tank-a"].Root.parent;
            var incubator = displays["incubator"].Root.parent;
            CollectDioramaSwaps(tank, tank.Find("Little Wonders store corner"));
            // The corner's second display stands right beside the first: it takes the look too, or its sign blooms.
            var second = displays.TryGetValue("tank-b", out var b) ? b.Root.parent : null;
            if (second) CollectDioramaSwaps(second, null);
            CollectDioramaSwaps(incubator, null);
            // The tanks' water becomes a block with a wobbling surface; the classic cube comes back with the look off.
            dioramaWaterMesh = DioramaWaterBlock(); dioramaOwned.Add(dioramaWaterMesh);
            dioramaTankWater = DioramaMaterial(water, DioramaLook.Style.Prop, 0);
            dioramaTankWater.SetFloat("_WobbleAmount", .012f);
            foreach (var station in new[] { tank, second })
            {
                var waterVolume = station ? station.Find("Water volume") : null;
                var filter = waterVolume ? waterVolume.GetComponent<MeshFilter>() : null;
                if (filter) dioramaWaterFilters.Add((filter, filter.sharedMesh));
            }
            dioramaIncubatorFluid = DioramaMaterial(water, DioramaLook.Style.Prop, 1);
            dioramaIncubatorFluid.SetColor("_BaseColor", new Color(.35f, .85f, .9f, .2f));
            dioramaIncubatorFluid.SetColor("_EmissionColor", new Color(.04f, .2f, .22f));
            var fluid = incubator.Find("Incubator fluid");
            foreach (var swap in dioramaSwaps)
                if (swap.Renderer && fluid && swap.Renderer.transform == fluid)
                    for (int i = 0; i < swap.Diorama.Length; i++) swap.Diorama[i] = dioramaIncubatorFluid;
            // The dome gets a glass of its own, so its hatch glow never lights the tank's glass.
            var dome = incubator.Find("Crystal incubator dome");
            dioramaDome = DioramaMaterial(glass, DioramaLook.Style.Prop, 1);
            foreach (var swap in dioramaSwaps)
                if (swap.Renderer && dome && swap.Renderer.transform == dome)
                    for (int i = 0; i < swap.Diorama.Length; i++) swap.Diorama[i] = dioramaDome;
            // Air bubbles: a thin bright rim and a glint around a clear middle, drawn after the water they rise through
            // (a filled pale sphere read as a white blob in the incubator).
            var bubble = DioramaMaterial(glass, DioramaLook.Style.Prop, 2);
            bubble.SetColor("_BaseColor", new Color(.86f, .97f, 1f, 0)); bubble.SetFloat("_RimStrength", .8f);
            bubble.SetFloat("_GlossStrength", 1); bubble.renderQueue = 3002;
            foreach (var swap in dioramaSwaps)
                if (swap.Renderer && swap.Renderer.name == "Air bubble")
                    for (int i = 0; i < swap.Diorama.Length; i++) swap.Diorama[i] = bubble;
            BuildDioramaWindows();
            BuildDioramaVolumes();
            dioramaTankGlow = new GameObject("Diorama night tank glow").AddComponent<Light>();
            dioramaTankGlow.transform.SetParent(dioramaRoot, false);
            dioramaTankGlow.transform.position = tank.TransformPoint(new Vector3(0, 1.5f, 0));
            dioramaTankGlow.type = LightType.Point; dioramaTankGlow.color = new Color(.45f, .92f, 1f);
            dioramaTankGlow.range = 3.2f; dioramaTankGlow.intensity = 0; dioramaTankGlow.shadows = LightShadows.None;
            // A soft fill just outside the porthole: the key light comes from behind the incubator, and a creature
            // looking out at the player should not sit in its own shadow.
            dioramaPortholeFill = new GameObject("Diorama porthole fill").AddComponent<Light>();
            dioramaPortholeFill.transform.SetParent(dioramaRoot, false);
            dioramaPortholeFill.transform.position = incubator.TransformPoint(new Vector3(0, 1.6f, -1.05f));
            dioramaPortholeFill.type = LightType.Point; dioramaPortholeFill.color = new Color(1f, .95f, .88f);
            dioramaPortholeFill.range = 1.9f; dioramaPortholeFill.intensity = .7f; dioramaPortholeFill.shadows = LightShadows.None;
            foreach (var light in GetComponentsInChildren<Light>(true))
                if (light.name == "Warm pendant light") dioramaPendants.Add((light, light.intensity));
            BuildDioramaHatch(incubator);
            dioramaRoot.gameObject.SetActive(false);
            dioramaShopVolume.enabled = dioramaLabVolume.enabled = false;
        }

        void CollectDioramaSwaps(Transform station, Transform roomBacking)
        {
            foreach (var renderer in station.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>() || renderer.GetComponentInParent<CreatureVisual>()) continue;
                var classic = renderer.sharedMaterials;
                var diorama = new Material[classic.Length];
                bool changed = false;
                bool room = roomBacking && renderer.transform.IsChildOf(roomBacking);
                int caustic = DioramaCausticLevel(renderer.name, room);
                for (int i = 0; i < classic.Length; i++)
                {
                    var m = classic[i];
                    bool lit = m && m.shader && (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "Standard");
                    diorama[i] = lit ? DioramaMaterial(m, room ? DioramaLook.Style.Room : DioramaLook.Style.Prop, m.renderQueue >= 3000 ? 0 : caustic) : m;
                    changed |= lit;
                }
                if (changed) dioramaSwaps.Add(new DioramaSwap { Renderer = renderer, Classic = classic, Diorama = diorama });
            }
        }

        static int DioramaCausticLevel(string name, bool room)
        {
            // Moving caustic light on the tank floor and what sits on it, and a little on the wall behind.
            if (name == "Tank sand") return 4;
            if (name == "Rounded river pebble" || name == "Terracotta river gravel") return 3;
            if (name == "Aquatic frond") return 2;
            return room ? 1 : 0;
        }

        static Mesh DioramaWaterBlock()
        {
            // A unit cube like the primitive, with a 24 x 24 top so the surface can wobble.
            const int n = 24;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d }); normals.AddRange(new[] { normal, normal, normal, normal });
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            float h = .5f;
            for (int x = 0; x < n; x++)
                for (int z = 0; z < n; z++)
                {
                    float x0 = -h + x / (float)n, x1 = -h + (x + 1) / (float)n, z0 = -h + z / (float)n, z1 = -h + (z + 1) / (float)n;
                    Quad(new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0), Vector3.up);
                }
            Quad(new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h), Vector3.down);
            Quad(new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h), new Vector3(h, -h, -h), Vector3.back);
            Quad(new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h), new Vector3(-h, -h, h), Vector3.forward);
            Quad(new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h), new Vector3(-h, -h, -h), Vector3.left);
            Quad(new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(h, -h, h), Vector3.right);
            var mesh = new Mesh { name = "Diorama water block" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildDioramaWindows()
        {
            dioramaStreetMask = DioramaStreetMask(); dioramaOwned.Add(dioramaStreetMask);
            dioramaWindowMaterial = new Material(DioramaLook.WindowShader) { name = "Diorama / street window" };
            dioramaWindowMaterial.SetTexture("_MainTex", dioramaStreetMask);
            dioramaOwned.Add(dioramaWindowMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                // In front of the painted sky, trees and clouds (their faces reach z -5.205), behind the cream mullions
                // (front faces at z -5.145) of the front wall windows at z -5.38.
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Diorama street window";
                var collider = quad.GetComponent<Collider>(); if (collider) { collider.enabled = false; Destroy(collider); }
                quad.transform.SetParent(dioramaRoot, false);
                quad.transform.position = new Vector3(side * 3.75f, 2.25f, -5.195f);
                quad.transform.rotation = Quaternion.Euler(0, 180, 0);
                quad.transform.localScale = new Vector3(2.3f, 1.89f, 1);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = dioramaWindowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                var light = new GameObject("Diorama window light").AddComponent<Light>();
                light.transform.SetParent(dioramaRoot, false);
                light.transform.position = new Vector3(side * 3.75f, 2.7f, -4.95f);
                light.type = LightType.Spot; light.spotAngle = 80; light.innerSpotAngle = 30; light.range = 9;
                light.shadows = LightShadows.None;
                dioramaWindowLights.Add(light);
            }
        }

        static Texture2D DioramaStreetMask()
        {
            // R: rooftops and round trees; G: a street lamp's glow; B: a few stars. Drawn here, no image file.
            const int w = 256, h = 192;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    float roof = .22f + .05f * Mathf.Floor(Mathf.Repeat(u * 5.3f, 1) * 3) / 3;
                    float block = Mathf.Repeat(u * 5.3f, 1);
                    float gable = block > .2f && block < .8f ? roof + (.3f - Mathf.Abs(block - .5f)) * .35f : roof;
                    float silhouette = v < gable ? 1 : 0;
                    for (int t = 0; t < 4; t++)
                    {
                        float tx = .12f + t * .27f, ty = .36f + .04f * (t % 2), r = .085f + .015f * (t % 3);
                        float dx = (u - tx) * (w / (float)h), dy = v - ty;
                        if (dx * dx + dy * dy < r * r) silhouette = 1;
                        if (Mathf.Abs(u - tx) < .006f && v < ty) silhouette = 1;
                    }
                    float lampX = .64f, lampY = .55f;
                    float pole = Mathf.Abs(u - lampX) < .005f && v < lampY ? 1 : 0;
                    float ldx = (u - lampX) * (w / (float)h), ldy = v - lampY;
                    float glow = Mathf.Clamp01(1 - Mathf.Sqrt(ldx * ldx + ldy * ldy) / .07f);
                    glow = glow * glow;
                    if (pole > 0) silhouette = 1;
                    uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                    float star = v > .55f && (hash % 997) == 0 ? 1 : 0;
                    pixels[y * w + x] = new Color32((byte)(silhouette * 255), (byte)(glow * 255), (byte)(star * 255), 255);
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Diorama street mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        void BuildDioramaVolumes()
        {
            dioramaShopProfile = DioramaProfile("Diorama shop grade"); dioramaLabProfile = DioramaProfile("Diorama lab grade");
            var go = new GameObject("Diorama room grades");
            go.transform.SetParent(transform, false);
            dioramaShopVolume = go.AddComponent<Volume>(); dioramaShopVolume.isGlobal = true; dioramaShopVolume.priority = 50; dioramaShopVolume.sharedProfile = dioramaShopProfile;
            dioramaLabVolume = go.AddComponent<Volume>(); dioramaLabVolume.isGlobal = true; dioramaLabVolume.priority = 51; dioramaLabVolume.sharedProfile = dioramaLabProfile;
        }

        VolumeProfile DioramaProfile(string name)
        {
            // Only effects the project's own profiles already use (Bloom HQ, Neutral tonemapping, colour adjustments,
            // white balance, vignette), so the build keeps their shader variants.
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = name;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f); bloom.intensity.Override(.5f); bloom.scatter.Override(.62f); bloom.highQualityFiltering.Override(true);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0); color.contrast.Override(8); color.saturation.Override(6); color.colorFilter.Override(Color.white);
            profile.Add<WhiteBalance>(true).temperature.Override(0);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.16f); vignette.smoothness.Override(.45f);
            dioramaOwned.Add(profile);
            return profile;
        }

        static void DioramaGrade(VolumeProfile profile, float bloomIntensity, Color filter, float temperature, float exposure, float saturation)
        {
            if (profile.TryGet<Bloom>(out var bloom)) bloom.intensity.value = bloomIntensity;
            if (profile.TryGet<ColorAdjustments>(out var color))
            { color.colorFilter.value = filter; color.postExposure.value = exposure; color.saturation.value = saturation; }
            if (profile.TryGet<WhiteBalance>(out var balance)) balance.temperature.value = temperature;
        }

        void TickDioramaLight(float dt)
        {
            if (!dioramaActive) return;
            var target = DioramaLighting.Resolve(dioramaPhase, dioramaOpenProgress);
            if (!dioramaKeyKnown) { dioramaShown = dioramaFrom = target; dioramaBlendAge = 99; dioramaKeyKnown = true; }
            dioramaBlendAge += Mathf.Max(0, dt);
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(dioramaBlendAge / DioramaLighting.PhaseBlendSeconds));
            dioramaShown = DioramaLighting.Blend(dioramaFrom, target, blend);
            var k = dioramaShown;
            if (keyLight)
            {
                keyLight.color = k.Sun; keyLight.intensity = k.SunIntensity;
                keyLight.transform.rotation = Quaternion.Euler(k.SunPitch, k.SunYaw, 0);
            }
            if (storeFillLight) { storeFillLight.color = k.ShopFill; storeFillLight.intensity = k.ShopFillIntensity; }
            if (labFillLight) { labFillLight.color = k.LabFill; labFillLight.intensity = k.LabFillIntensity; }
            RefreshWingLights(); RefreshLabWingLights();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = k.AmbientSky; RenderSettings.ambientEquatorColor = k.AmbientEquator; RenderSettings.ambientGroundColor = k.AmbientGround;
            foreach (var (light, intensity) in dioramaPendants) if (light) light.intensity = intensity * (.55f + .9f * k.LampsOn);
            foreach (var light in dioramaWindowLights)
            {
                light.color = k.WindowLight; light.intensity = k.WindowLightIntensity;
                light.transform.rotation = Quaternion.Euler(Mathf.Clamp(k.SunPitch + 10, 25, 70), k.SunYaw * .5f, 0);
            }
            dioramaWindowMaterial.SetColor("_SkyTop", k.SkyTop); dioramaWindowMaterial.SetColor("_SkyBottom", k.SkyBottom);
            dioramaWindowMaterial.SetColor("_Silhouette", k.Silhouette); dioramaWindowMaterial.SetColor("_LampColor", k.Lamp);
            dioramaWindowMaterial.SetColor("_StarColor", k.Stars);
            if (dioramaTankGlow) dioramaTankGlow.intensity = 1.8f * k.TankGlow;
            if (dioramaTankWater) dioramaTankWater.SetColor("_EmissionColor", new Color(.05f, .45f, .55f) * (k.TankGlow * 1.6f));
            DioramaLabWeight = DioramaLighting.LabWeight(dioramaViewer);
            dioramaLabVolume.weight = DioramaLabWeight; dioramaShopVolume.weight = 1;
            DioramaGrade(dioramaShopProfile, k.Bloom, k.ShopFilter, k.ShopTemperature, k.ShopExposure, k.ShopSaturation);
            DioramaGrade(dioramaLabProfile, k.Bloom, k.LabFilter, k.LabTemperature, k.LabExposure, k.LabSaturation);
        }

        void TickDiorama(float dt)
        {
            if (!dioramaActive) return;
            TickDioramaLight(dt);
            TickDioramaHatch(dt);
        }

        void DisposeDiorama()
        {
            if (dioramaActive) DioramaLook.Active = false;
            foreach (var asset in dioramaOwned) if (asset) Destroy(asset);
            dioramaOwned.Clear(); dioramaMaterialMap.Clear();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Validation: every swapped renderer shows its diorama (on) or classic (off) materials.</summary>
        public bool DioramaMaterialsConsistent(out string detail)
        {
            foreach (var swap in dioramaSwaps)
            {
                if (!swap.Renderer) continue;
                var expected = dioramaActive ? swap.Diorama : swap.Classic;
                var shown = swap.Renderer.sharedMaterials;
                for (int i = 0; i < shown.Length; i++)
                {
                    // The hatchery lamp is set by its state; it only needs to be a diorama material while the look is on.
                    if (hatcheryLamp && swap.Renderer == hatcheryLamp)
                    {
                        bool toon = shown[i] && shown[i].shader && shown[i].shader.name == DioramaLook.ToonShaderName;
                        if (toon != dioramaActive) { detail = "Hatchery lamp shows " + shown[i]; return false; }
                        continue;
                    }
                    if (shown[i] != expected[i]) { detail = swap.Renderer.name + " slot " + i + " shows " + (shown[i] ? shown[i].name : "null"); return false; }
                }
            }
            detail = dioramaSwaps.Count + " renderers consistent with the look " + (dioramaActive ? "on" : "off") + ".";
            return true;
        }
        /// <summary>Validation: show the current key at once instead of blending to it.</summary>
        public void SnapDioramaLightForValidation() { dioramaKeyKnown = false; TickDioramaLight(0); }
        public Light KeyLightForValidation => keyLight;
        public Light DioramaWindowLightForValidation(int index) => index >= 0 && index < dioramaWindowLights.Count ? dioramaWindowLights[index] : null;
        public Material DioramaWindowMaterialForValidation => dioramaWindowMaterial;
        public Light DioramaTankGlowForValidation => dioramaTankGlow;
        public Light DioramaPortholeFillForValidation => dioramaPortholeFill;
        public int DioramaShadowsLiftedForValidation => dioramaShadowRestore.Count;
        public CreatureVisual DisplayCreatureForValidation(string id) => displays.TryGetValue(id, out var d) ? d.Creature : null;
        public Bounds DisplayInteriorForValidation(string id) => displays.TryGetValue(id, out var d) ? d.Interior : default;
        public Transform DisplayStationForValidation(string id) => displays.TryGetValue(id, out var d) ? d.Root.parent : null;
#endif
    }
}
