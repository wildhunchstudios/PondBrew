using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    public enum StationKind { Bloom, Flow, Incubator, TankA, TankB, Counter, Upgrade, Display, StorageRack, Conditioning }

    public sealed class Station : MonoBehaviour
    {
        public StationKind Kind;
        public string Label;
        public string TankId;
        public string FacilityId;
        public string StorageItemId;
        public string ConditionerItemId;
    }

    /// <summary>Presentation only. All quantities and transaction decisions belong to GameModel.</summary>
    public sealed partial class ShopWorld : MonoBehaviour
    {
        public Vector3 SpawnPosition => new Vector3(0, 1, -3.8f);
        public IReadOnlyList<Station> Stations => stations;
        public Transform ToolChamber { get; private set; }
        public Transform ToolRoot { get; private set; }

        readonly List<Station> stations = new List<Station>();
        readonly List<Transform> bloomCells = new List<Transform>();
        readonly List<Transform> flowCells = new List<Transform>();
        readonly List<Transform> reservoirCells = new List<Transform>();
        readonly List<Particle> particles = new List<Particle>();
        readonly Dictionary<StationKind, TextMesh> status = new Dictionary<StationKind, TextMesh>();
        readonly Dictionary<string, Display> displays = new Dictionary<string, Display>();
        readonly List<LabelFit> labels = new List<LabelFit>();
        readonly Dictionary<Font, Material> fontMaterials = new Dictionary<Font, Material>();
        Shader worldTextShader;
        Light keyLight, storeFillLight, labFillLight;
        Material shell, dark, trim, cream, bloomMat, flowMat, glass, water, gold, black, glow;
        Transform bloomRoot, flowRoot, incubationRing, toolNozzle, toolBody, toolUpgradeBand;
        TextMesh toolReadout;
        bool upgraded, harvesting;
        float emissionClock, lastTime, harvestKick;
        int bloomCount, flowCount;
        GameModel lastModel;

        sealed class Display
        {
            public Transform Root;
            public CreatureVisual Creature;
            public string SpeciesId;
            public string FxSpecimenId;
            public float Seed;
            public AquariumHabitat Habitat;
            public float PondletClock, PondletPhase, PondletFocus, PondletCooldown;
            public float PondletPlacedAge = -1, PondletGlanceAge = -1;
            public uint PondletSequence;
            public bool Incubator;
            public TextMesh Status;
            public TextMesh StorageStatus;
            public HashSet<Material> StorageMaterials;
            public Bounds Interior = new Bounds(new Vector3(0, 1.57f, 0), new Vector3(1.94f, 1.06f, 1.24f));
        }

        sealed class Particle
        {
            public Transform Root;
            public Vector3 Start;
            public float Age;
            public float Seed;
        }

        sealed class LabelFit
        {
            public TextMesh Text;
            public float Height;
            public float Width;
        }

        void OnEnable() { Font.textureRebuilt += FontAtlasRebuilt; }
        void OnDisable() { Font.textureRebuilt -= FontAtlasRebuilt; CancelFxTransient(); }
        void OnDestroy()
        {
            DisposeFxPresentation();
            DisposeRedesign();
            DisposeSamplePresentation();
            DisposeRoomArt();
            DisposeConditioningPresentation();
            DisposeResearchPresentation();
            DisposeStoragePresentation();
            DisposePlacementPresentation();
            foreach (var material in fontMaterials.Values) if (material) Destroy(material);
        }

        void FontAtlasRebuilt(Font font)
        {
            if (font && font.material && fontMaterials.TryGetValue(font, out var material))
                material.mainTexture = font.material.mainTexture;
        }

        Material WorldTextMaterial(Font font)
        {
            if (!worldTextShader)
                worldTextShader = Resources.Load<Shader>("EvolutionWorldText");
            if (!worldTextShader)
                throw new InvalidOperationException("Missing Resources/EvolutionWorldText.shader.");
            if (!fontMaterials.TryGetValue(font, out var material))
            {
                material = new Material(worldTextShader) { name = "Depth-tested world labels / " + font.name };
                fontMaterials.Add(font, material);
            }
            material.mainTexture = font.material.mainTexture;
            return material;
        }

        public void Build(Camera playerCamera)
        {
            shell = MakeMaterial("Seafoam enamel", new Color(0.61f, 0.8f, 0.75f), 0.15f, 0.55f);
            dark = MakeMaterial("Midnight teal", new Color(0.045f, 0.14f, 0.19f), 0.25f, 0.5f);
            trim = MakeMaterial("Brushed warm metal", new Color(0.67f, 0.73f, 0.70f), 0.7f, 0.63f);
            cream = MakeMaterial("Warm porcelain", new Color(0.91f, 0.92f, 0.82f), 0.04f, 0.5f);
            bloomMat = MakeMaterial("Bloom coral", new Color(1f, 0.36f, 0.3f), 0.05f, 0.7f);
            flowMat = MakeMaterial("Flow turquoise", new Color(0.12f, 0.81f, 0.87f), 0.05f, 0.7f);
            gold = MakeMaterial("Upgrade sunshine", new Color(1f, 0.69f, 0.21f), 0.28f, 0.48f);
            black = MakeMaterial("Ink", new Color(0.008f, 0.027f, 0.04f), 0, 0.35f);
            glow = MakeMaterial("Soft luminous mint", new Color(0.6f, 1f, 0.82f), 0, 0.4f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.15f, 0.55f, 0.3f));
            glass = MakeGlass("Clear aquarium glass", new Color(0.61f, 0.91f, 0.95f, 0.085f));
            water = MakeGlass("Aquatic water tint", new Color(0.17f, 0.73f, 0.84f, 0.085f));
            BuildResearchMaterials();
            BuildArtPalette();
            Room();
            bloomRoot = Grower(StationKind.Bloom, new Vector3(-4.2f, 0, 3.85f), "BLOOM", "01 / PETAL CULTURE", bloomMat, bloomCells, true);
            flowRoot = Grower(StationKind.Flow, new Vector3(-1.4f, 0, 3.85f), "FLOW", "02 / STREAM CULTURE", flowMat, flowCells, false);
            Incubator(new Vector3(2.0f, 0, 3.85f));
            Aquarium(StationKind.TankA, "tank-a", new Vector3(5.5f, 0, -1.2f), "DISPLAY 01", 0.2f);
            Aquarium(StationKind.TankB, "tank-b", new Vector3(5.5f, 0, -3.85f), "DISPLAY 02", 2.1f);
            Counter(new Vector3(-5.5f, 0, -2.2f));
            Upgrade(new Vector3(-5.5f, 0, 1.9f));
            Tool(playerCamera.transform);
            playerCamera.backgroundColor = new Color(0.08f, 0.19f, 0.22f);
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.nearClipPlane = 0.04f;
            BuildFxPresentation();
            fxPool.SetViewCamera(playerCamera);
        }

        public static Material MakeMaterial(string name, Color color, float metallic = 0, float smoothness = 0.45f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        static Material MakeGlass(string name, Color color)
        {
            var m = MakeMaterial(name, color, 0, 0.8f);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_AlphaClip", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
            return m;
        }

        public static Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            var c = g.GetComponent<Collider>();
            if (!collision && c) { c.enabled = false; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
            if (material.renderQueue >= 3000)
            {
                g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                g.GetComponent<Renderer>().receiveShadows = false;
            }
            return g.transform;
        }

        Transform Group(string name, Transform parent, Vector3 position)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = position;
            return g;
        }

        Station MakeStation(StationKind kind, string label, Vector3 position, float rotation = 0)
        {
            var r = Group(label, transform, position);
            r.localRotation = Quaternion.Euler(0, rotation, 0);
            var s = r.gameObject.AddComponent<Station>();
            s.Kind = kind;
            s.Label = label;
            var c = r.gameObject.AddComponent<BoxCollider>();
            c.center = new Vector3(0, 1.0f, 0);
            c.size = new Vector3(1.7f, 2, 1.15f);
            stations.Add(s);
            RegisterLabFacility(s);
            return s;
        }

        void Room()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.70f, .77f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.58f, .55f, .46f);
            RenderSettings.ambientGroundColor = new Color(.31f, .28f, .23f);
            RenderSettings.fog = false;
            var floor = artTile;
            var wall = ArtMaterial("vanilla plaster", new Color(.94f, .87f, .71f), .18f);
            Shape(transform, "Floor", PrimitiveType.Cube, new Vector3(0, -.14f, 0), new Vector3(14, .28f, 11), floor, true);
            labRearAssembly = Group("Lab rear wall assembly", transform, Vector3.zero);
            Shape(labRearAssembly, "Back wall", PrimitiveType.Cube, new Vector3(0, 2.1f, 5.6f), new Vector3(14, 4.2f, .3f), wall, true);
            storeWestWall = Shape(transform, "Store west wall", PrimitiveType.Cube, new Vector3(-7, 2.1f, -2.5f), new Vector3(.3f, 4.2f, 6), wall, true);
            Shape(transform, "Lab west wall", PrimitiveType.Cube, new Vector3(-7, 2.1f, 3), new Vector3(.3f, 4.2f, 5), wall, true);
            Shape(transform, "Right wall", PrimitiveType.Cube, new Vector3(7, 2.1f, 0), new Vector3(.3f, 4.2f, 11), wall, true);
            Shape(transform, "Front wall", PrimitiveType.Cube, new Vector3(0, 2.1f, -5.6f), new Vector3(14, 4.2f, .3f), wall, true);
            Shape(labRearAssembly, "Back wainscot", PrimitiveType.Cube, new Vector3(0, .65f, 5.42f), new Vector3(14, 1.3f, .07f), dark);
            Shape(labRearAssembly, "Back brass rail", PrimitiveType.Cube, new Vector3(0, 1.32f, 5.36f), new Vector3(14, .035f, .07f), trim);
            // The stable floor object name is also used by the store expansion material lookup.
            Shape(transform, "Front store floor", PrimitiveType.Cube, new Vector3(0, .001f, -2.5f), new Vector3(13.7f, .002f, 6), artHoney);
            Shape(transform, "Flush lab threshold", PrimitiveType.Cube, new Vector3(0, .008f, .5f), new Vector3(3.2f, .01f, .3f), trim);
            Partition(wall);
            BuildLabDoor();
            for (int i = -1; i <= 1; i++)
            {
                Shape(transform, "Ceiling cross beam", PrimitiveType.Cube, new Vector3(0, 4.02f, i * 3.2f), new Vector3(14, .12f, .20f), artHoney);
                if (i > 0) Shape(transform, "Lab ceiling light", PrimitiveType.Cube, new Vector3(0, 3.94f, i * 3.2f), new Vector3(4.2f, .06f, .16f), glow);
            }
            Shape(transform, "Ceiling", PrimitiveType.Cube, new Vector3(0, 4.25f, 0), new Vector3(14, .18f, 11), wall);
            keyLight = new GameObject("Soft shop light").AddComponent<Light>();
            keyLight.transform.SetParent(transform, false);
            keyLight.type = LightType.Directional;
            keyLight.transform.rotation = Quaternion.Euler(55, -32, 0);
            keyLight.color = new Color(1, .95f, .84f);
            keyLight.intensity = 1.65f;
            keyLight.shadows = LightShadows.Soft;
            storeFillLight = LightAt(new Vector3(-3, 2.8f, -2), new Color(1, .95f, .83f), 3.4f, 8);
            labFillLight = LightAt(new Vector3(1, 2.8f, 3), new Color(.75f, 1, .94f), 3.2f, 8);
            Shape(labRearAssembly, "Back lab identity panel", PrimitiveType.Cube, new Vector3(0, 3.30f, 5.36f), new Vector3(6.2f, .78f, .07f), artTeal);
            Text(labRearAssembly, "THE CURIOSITY LAB", new Vector3(0, 3.43f, 5.30f), .15f, cream.color, 5.7f);
            Text(labRearAssembly, "BIG IDEAS. LITTLE CREATURES.", new Vector3(0, 3.14f, 5.29f), .045f, artButter.color, 4.8f);
            var door = Group("Quiet entrance", transform, new Vector3(0, 0, -5.35f));
            door.localRotation = Quaternion.Euler(0, 180, 0);
            Shape(door, "Entrance", PrimitiveType.Cube, new Vector3(0, 1.25f, 0), new Vector3(2.1f, 2.5f, .05f), artTeal);
            Shape(door, "Door porthole", PrimitiveType.Cylinder, new Vector3(0, 1.6f, -.06f), new Vector3(.73f, .035f, .73f), trim).localRotation = Quaternion.Euler(90, 0, 0);
            Shape(door, "Porthole glass", PrimitiveType.Cylinder, new Vector3(0, 1.6f, -.10f), new Vector3(.62f, .025f, .62f), shell).localRotation = Quaternion.Euler(90, 0, 0);
            Text(door, "A LITTLE LIFE STARTS HERE", new Vector3(0, 2.94f, -.09f), .065f, new Color(.07f, .2f, .23f), 3.1f);
            BuildRoomArt();
        }

        void Partition(Material wall)
        {
            // Full partition plus the solid pocket door stop movement and interaction rays when closed.
            // Clear opening: 3.2 m wide and 2.8 m high; the player capsule is 0.6 m wide / 1.75 m tall.
            Shape(transform, "Store-lab partition left", PrimitiveType.Cube, new Vector3(-4.3f, 2.1f, .5f), new Vector3(5.4f, 4.2f, .22f), wall, true);
            Shape(transform, "Store-lab partition right", PrimitiveType.Cube, new Vector3(4.3f, 2.1f, .5f), new Vector3(5.4f, 4.2f, .22f), wall, true);
            Shape(transform, "Store-lab doorway header", PrimitiveType.Cube, new Vector3(0, 3.5f, .5f), new Vector3(3.2f, 1.4f, .22f), wall, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Shape(transform, "Store partition base", PrimitiveType.Cube, new Vector3(side * 4.3f, .12f, .365f), new Vector3(5.4f, .24f, .04f), dark);
                Shape(transform, "Lab partition base", PrimitiveType.Cube, new Vector3(side * 4.3f, .12f, .635f), new Vector3(5.4f, .24f, .04f), dark);
            }
            Shape(transform, "Doorway lintel trim", PrimitiveType.Cube, new Vector3(0, 2.83f, .5f), new Vector3(3.4f, .06f, .31f), dark);
            // The store identity sits above the opening, so the entrance view clearly reads as a shop.
            Shape(transform, "Front store identity panel", PrimitiveType.Cube, new Vector3(0, 3.5f, .335f), new Vector3(4.85f, .76f, .045f), artTeal);
            Text(transform, "LITTLE TIDE AQUATICS", new Vector3(0, 3.60f, .30f), .145f, cream.color, 4.45f);
            Text(transform, "CURIOUS FISH. HAPPY LITTLE WORLDS.", new Vector3(0, 3.30f, .30f), .04f, artButter.color, 4.35f);
            Shape(transform, "Back lab direction plaque", PrimitiveType.Cube, new Vector3(0, 3.075f, .335f), new Vector3(2.8f, .17f, .045f), dark);
            Text(transform, "BACK LAB", new Vector3(0, 3.075f, .30f), .065f, new Color(.58f, 1, .84f), 2.5f);
            var returnSign = Group("Return to front store sign", transform, new Vector3(0, 0, .665f));
            returnSign.localRotation = Quaternion.Euler(0, 180, 0);
            Shape(returnSign, "Store direction plaque", PrimitiveType.Cube, new Vector3(0, 3.02f, 0), new Vector3(3, .32f, .045f), dark);
            Text(returnSign, "FRONT STORE", new Vector3(0, 3.02f, -.035f), .075f, new Color(.96f, .95f, .83f), 2.7f);
        }
        /// <summary>Presentation only: update existing room lights after Build or a restored phase.</summary>
        public void SetDayPhase(ShopDayPhase phase)
        {
            if (phase != ShopDayPhase.Night && phase != ShopDayPhase.Open && phase != ShopDayPhase.Closing)
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (!keyLight || !storeFillLight || !labFillLight)
                throw new InvalidOperationException("Build the shop before applying its day phase.");

            // Ambient light, luminous strips, tank materials, and station lighting remain readable.
            // The exterior key becomes cooler at night; shop practical lighting stays warm and welcoming.
            bool night = phase == ShopDayPhase.Night;
            bool closing = phase == ShopDayPhase.Closing;
            keyLight.color = night ? new Color(.70f, .82f, 1f) : closing ? new Color(1f, .89f, .76f) : new Color(1f, .95f, .84f);
            keyLight.intensity = night ? 1.12f : closing ? 1.45f : 1.65f;
            storeFillLight.color = night ? new Color(1f, .83f, .63f) : closing ? new Color(1f, .87f, .69f) : new Color(1f, .95f, .83f);
            storeFillLight.intensity = night ? 2.65f : closing ? 3f : 3.4f;
            labFillLight.color = night ? new Color(.74f, .94f, 1f) : closing ? new Color(.74f, .98f, .97f) : new Color(.75f, 1f, .94f);
            labFillLight.intensity = 3.2f;
            RefreshWingLights();
            RefreshLabWingLights();
        }

        Light LightAt(Vector3 position, Color color, float intensity, float range)
        {
            var l = new GameObject("Aquatic fill light").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = position;
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        Transform Grower(StationKind kind, Vector3 position, string name, string subhead, Material accent, List<Transform> cells, bool bloom)
        {
            var r = MakeStation(kind, name + " grower", position).transform;
            Shape(r, "Rounded machine foot", PrimitiveType.Cylinder, new Vector3(0, .12f, 0), new Vector3(1.6f, .12f, 1.35f), dark);
            Shape(r, "Porcelain grower base", PrimitiveType.Cylinder, new Vector3(0, .48f, 0), new Vector3(1.38f, .29f, 1.13f), cream);
            Shape(r, "Culture stripe", PrimitiveType.Cylinder, new Vector3(0, .79f, 0), new Vector3(1.42f, .06f, 1.17f), accent);
            Shape(r, "Chamber lower collar", PrimitiveType.Cylinder, new Vector3(0, .9f, 0), new Vector3(1.1f, .065f, 1.1f), trim);
            Shape(r, "Clear growing chamber", PrimitiveType.Cylinder, new Vector3(0, 1.47f, 0), new Vector3(.96f, .54f, .96f), glass);
            Shape(r, "Growing medium", PrimitiveType.Cylinder, new Vector3(0, 1.37f, 0), new Vector3(.92f, .44f, .92f), water);
            Shape(r, "Upper collar", PrimitiveType.Cylinder, new Vector3(0, 2.02f, 0), new Vector3(1.1f, .06f, 1.1f), trim);
            Shape(r, "Rounded lid", PrimitiveType.Sphere, new Vector3(0, 2.08f, 0), new Vector3(1.07f, .16f, 1.07f), cream);
            for (int i = -1; i <= 1; i += 2)
                Shape(r, "Chamber support", PrimitiveType.Cylinder, new Vector3(i * .54f, 1.44f, .19f), new Vector3(.06f, .58f, .06f), dark);
            Shape(r, "Siphon outlet", PrimitiveType.Cylinder, new Vector3(.36f, .93f, -.6f), new Vector3(.22f, .13f, .22f), accent).localRotation = Quaternion.Euler(90, 0, 0);
            Shape(r, "Outlet opening", PrimitiveType.Cylinder, new Vector3(.36f, .93f, -.745f), new Vector3(.13f, .015f, .13f), dark).localRotation = Quaternion.Euler(90, 0, 0);
            Faceplate(r, name, new Vector3(0, .52f, -.584f), accent);
            status[kind] = Text(r, "4 / 6  |  GROWING", new Vector3(0, .30f, -.607f), .048f, Color.white);
            Text(r, subhead, new Vector3(0, 2.40f, 0), .047f, new Color(.1f, .27f, .28f), 1.9f);
            for (int i = 0; i < 6; i++)
            {
                var cell = Organism(r, bloom, accent, .17f);
                cell.name = name + " unit " + (i + 1);
                cells.Add(cell);
            }
            if (bloom) BuildLabSample(r);
            if (AttachRedesign(r, "GrowerBase", "Rounded machine foot", "Porcelain grower base", "Culture stripe", "Chamber lower collar", "Ribbed base seam", "Bloom pressure gauge"))
                RedesignLabelDepth(r, -.64f, 2.30f, 1.28f, true);
            AttachRedesign(r, "GrowerCap", "Rounded lid", "Bloom cap stripe", "Cap hand grip");
            return r;
        }

        public Transform Organism(Transform parent, bool bloom, Material material, float scale)
        {
            var root = Group(bloom ? "Petal cell" : "Stream cell", parent, Vector3.zero);
            root.localScale = Vector3.one * scale;
            if (bloom)
            {
                Shape(root, "Cell core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .75f, material);
                for (int p = 0; p < 4; p++)
                {
                    float a = p * Mathf.PI * .5f;
                    Shape(root, "Soft petal", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * .35f, Mathf.Sin(a) * .35f, 0), new Vector3(.53f, .53f, .4f), material);
                }
                Shape(root, "Cell seed", PrimitiveType.Sphere, new Vector3(0, 0, -.33f), Vector3.one * .22f, cream);
            }
            else
            {
                Shape(root, "Stream head", PrimitiveType.Sphere, new Vector3(0, .09f, 0), new Vector3(.57f, .82f, .5f), material);
                Shape(root, "Stream tail", PrimitiveType.Capsule, new Vector3(.15f, -.49f, 0), new Vector3(.18f, .39f, .18f), material).localRotation = Quaternion.Euler(0, 0, 24);
                Shape(root, "Cell seed", PrimitiveType.Sphere, new Vector3(0, .13f, -.22f), Vector3.one * .21f, cream);
            }
            return root;
        }

        void Incubator(Vector3 position)
        {
            var r = MakeStation(StationKind.Incubator, "Incubator", position).transform;
            Shape(r, "Incubator pedestal", PrimitiveType.Cube, new Vector3(0, .42f, 0), new Vector3(1.85f, .84f, 1.3f), artTeal);
            Shape(r, "Rounded worktop", PrimitiveType.Cube, new Vector3(0, .85f, 0), new Vector3(1.99f, .10f, 1.43f), cream);
            Shape(r, "Crystal incubator dome", PrimitiveType.Sphere, new Vector3(0, 1.48f, 0), new Vector3(1.55f, 1.5f, 1.25f), glass);
            Shape(r, "Incubator fluid", PrimitiveType.Sphere, new Vector3(0, 1.42f, 0), new Vector3(1.45f, 1.3f, 1.16f), water);
            incubationRing = Ring(r, "Incubator orbit", new Vector3(0, 1.18f, 0), .78f, .022f, gold);
            incubationRing.localRotation = Quaternion.Euler(0, 0, 15);
            Ring(r, "Base seal", new Vector3(0, .98f, 0), .56f, .05f, trim);
            Faceplate(r, "INCUBATOR", new Vector3(0, .58f, -.663f), shell);
            status[StationKind.Incubator] = Text(r, "ADD MEASURED DOSES", new Vector3(0, .31f, -.674f), .045f, Color.white);
            Text(r, "03 / MAKE A LITTLE LIFE", new Vector3(0, 2.40f, 0), .047f, new Color(.1f, .27f, .28f), 1.9f);
            AddIncubatorArt(r);
            bool hatchery = BuildHatcheryVisual(r);
            if (!hatchery && AttachRedesign(r, "IncubatorBase", "Incubator pedestal", "Rounded worktop", "Incubator enamel finish"))
                RedesignLabelDepth(r, -.738f, .72f, 1.38f);
            var display = Group("Incubator creature", r, new Vector3(0, 1.48f, 0));
            displays["incubator"] = new Display { Root = display, Incubator = true, Seed = 3.5f };
            for (int i = 0; !hatchery && i < 2; i++)
            {
                var color = i == 0 ? bloomMat : flowMat;
                Shape(r, "Measured dose port", PrimitiveType.Cylinder, new Vector3((i == 0 ? -.65f : .65f), 1.02f, -.55f), new Vector3(.24f, .045f, .24f), color);
            }
        }

        void Aquarium(StationKind kind, string id, Vector3 position, string title, float seed)
        {
            var r = MakeStation(kind, title, position, 90).transform;
            r.GetComponent<Station>().TankId = id;
            r.GetComponent<BoxCollider>().size = new Vector3(2.1f, 2.1f, 1.3f);
            Shape(r, "Display cabinet", PrimitiveType.Cube, new Vector3(0, .43f, 0), new Vector3(2.03f, .86f, 1.33f), dark);
            Shape(r, "Tank lower rim", PrimitiveType.Cube, new Vector3(0, .91f, 0), new Vector3(2.14f, .14f, 1.42f), cream);
            Shape(r, "Glass aquarium", PrimitiveType.Cube, new Vector3(0, 1.54f, 0), new Vector3(2f, 1.17f, 1.3f), glass);
            Shape(r, "Water volume", PrimitiveType.Cube, new Vector3(0, 1.48f, 0), new Vector3(1.9f, 1.03f, 1.2f), water);
            Shape(r, "Upper frame", PrimitiveType.Cube, new Vector3(0, 2.14f, 0), new Vector3(2.13f, .09f, 1.41f), trim);
            Shape(r, "Tank sand", PrimitiveType.Cube, new Vector3(0, 1.015f, 0), new Vector3(1.9f, .07f, 1.2f), cream);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    Shape(r, "Tank seam", PrimitiveType.Cube, new Vector3(x * 1.01f, 1.55f, z * .65f), new Vector3(.022f, 1.1f, .022f), trim);
                var plant = Shape(r, "Aquatic frond", PrimitiveType.Capsule, new Vector3(x * .74f, 1.2f, .37f), new Vector3(.07f, .19f, .035f), shell);
                plant.localRotation = Quaternion.Euler(0, 0, x * 15);
                Shape(r, "Rounded river pebble", PrimitiveType.Sphere, new Vector3(x * .58f, 1.06f, .29f), new Vector3(.3f, .15f, .23f), trim);
            }
            Faceplate(r, title, new Vector3(0, .58f, -.68f), shell);
            status[kind] = Text(r, "EMPTY  /  1 SPACE", new Vector3(0, .31f, -.69f), .047f, Color.white);
            displays[id] = new Display { Root = Group("Resident", r, new Vector3(0, 1.56f, 0)), Seed = seed };
            for (int i = 0; i < 4; i++)
            {
                var bubble = Shape(r, "Air bubble", PrimitiveType.Sphere, new Vector3(.72f, 1.16f + i * .21f, .29f), Vector3.one * (.025f + i * .008f), glass);
            }
            displays[id].Habitat = BuildAquariumHabitat(r, seed);
            BuildStoreSample(r, id == "tank-a");
            if (AttachRedesign(r, "DisplayCabinet", "Display cabinet", "Tank lower rim", "Inset wood cabinet door", "Raised door panel", "Cabinet pull"))
                RedesignLabelDepth(r, -.765f, 2.22f, 1.64f);
        }

        void Counter(Vector3 position)
        {
            var r = MakeStation(StationKind.Counter, "Sales counter", position, -90).transform;
            r.GetComponent<BoxCollider>().size = new Vector3(2.25f, 2.2f, 1.15f);
            r.GetComponent<BoxCollider>().center = new Vector3(0, 1.1f, 0);
            Shape(r, "Sales counter cabinet", PrimitiveType.Cube, new Vector3(0, .51f, 0), new Vector3(2.15f, 1.02f, 1.04f), artMint);
            Shape(r, "Sales counter top", PrimitiveType.Cube, new Vector3(0, 1.08f, 0), new Vector3(2.36f, .12f, 1.24f), cream);
            Shape(r, "Counter toe", PrimitiveType.Cube, new Vector3(0, .08f, 0), new Vector3(2.1f, .16f, 1f), dark);
            Faceplate(r, "CREATURE EXCHANGE", new Vector3(0, .65f, -.534f), dark);
            Text(r, "A NEW HOME FOR EVERY DISCOVERY", new Vector3(0, .38f, -.55f), .034f, new Color(.06f, .2f, .24f), 1.85f);
            Shape(r, "Counter tablet", PrimitiveType.Cube, new Vector3(.58f, 1.28f, -.05f), new Vector3(.48f, .28f, .075f), dark).localRotation = Quaternion.Euler(12, 0, 0);
            Text(r, "CHECKOUT", new Vector3(.58f, 1.29f, -.105f), .044f, new Color(.57f, 1, .84f), .42f);
            Shape(r, "Service bell base", PrimitiveType.Cylinder, new Vector3(-.61f, 1.17f, -.15f), new Vector3(.3f, .035f, .3f), dark);
            Shape(r, "Service bell", PrimitiveType.Sphere, new Vector3(-.61f, 1.21f, -.15f), new Vector3(.25f, .13f, .25f), gold);
            AddCounterArt(r);
            if (AttachRedesign(r, "ServiceCounter", "Sales counter cabinet", "Sales counter top", "Counter toe", "Friendly counter finish"))
                RedesignLabelDepth(r, -.61f, .54f, 1.5f);
        }

        void Upgrade(Vector3 position)
        {
            var r = MakeStation(StationKind.Upgrade, "Siphon upgrade", position, -90).transform;
            Shape(r, "Upgrade plinth", PrimitiveType.Cube, new Vector3(0, .47f, 0), new Vector3(1.5f, .94f, 1.1f), dark);
            Shape(r, "Upgrade top", PrimitiveType.Cube, new Vector3(0, .98f, 0), new Vector3(1.63f, .1f, 1.2f), gold);
            Shape(r, "Upgrade reservoir sample", PrimitiveType.Cylinder, new Vector3(0, 1.5f, 0), new Vector3(.55f, .41f, .55f), glass);
            Shape(r, "Upgrade lid", PrimitiveType.Cylinder, new Vector3(0, 1.93f, 0), new Vector3(.64f, .06f, .64f), gold);
            Shape(r, "Upgrade base", PrimitiveType.Cylinder, new Vector3(0, 1.09f, 0), new Vector3(.64f, .06f, .64f), gold);
            Ring(r, "Upgrade feature ring", new Vector3(0, 1.5f, 0), .40f, .034f, glow).localRotation = Quaternion.Euler(0, 0, 22);
            Text(r, "4  >  8", new Vector3(0, 1.53f, -.34f), .11f, new Color(.09f, .24f, .25f), .49f);
            Faceplate(r, "RESEARCH / SIPHON", new Vector3(0, .62f, -.566f), gold);
            status[StationKind.Upgrade] = Text(r, "24 COINS  /  COLLECT FASTER", new Vector3(0, .32f, -.583f), .04f, Color.white);
            researchHint = Text(r, "RESEARCH / DISCOVER 3 SPECIES", new Vector3(0, 2.30f, 0), .05f, new Color(.1f, .27f, .28f), 1.65f);
            BuildSparkModule(r);
            BuildResearchBenchVisual(r);
        }

        void Tool(Transform camera)
        {
            ToolRoot = Group("Culture siphon", camera, new Vector3(.33f, -.16f, .58f));
            ToolRoot.localScale = Vector3.one * .62f;
            ToolRoot.localRotation = Quaternion.Euler(-5, -7, -5);
            toolBody = Group("Siphon chassis", ToolRoot, Vector3.zero);
            Shape(toolBody, "Soft grip", PrimitiveType.Capsule, new Vector3(0, -.14f, -.07f), new Vector3(.10f, .15f, .10f), dark).localRotation = Quaternion.Euler(-22, 0, 0);
            Shape(toolBody, "Base coupling", PrimitiveType.Cylinder, new Vector3(0, -.10f, .025f), new Vector3(.23f, .037f, .23f), cream);
            ToolChamber = Group("Transparent reservoir", toolBody, new Vector3(0, .02f, .025f));
            Shape(ToolChamber, "Reservoir glass", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.2f, .11f, .2f), glass);
            Shape(ToolChamber, "Reservoir top", PrimitiveType.Cylinder, new Vector3(0, .115f, 0), new Vector3(.23f, .025f, .23f), cream);
            Shape(ToolChamber, "Reservoir bottom", PrimitiveType.Cylinder, new Vector3(0, -.115f, 0), new Vector3(.23f, .019f, .23f), trim);
            toolUpgradeBand = Ring(ToolChamber, "Larger capacity upgrade band", Vector3.zero, .106f, .009f, gold);
            toolUpgradeBand.gameObject.SetActive(false);
            toolNozzle = Group("Collection nozzle", toolBody, new Vector3(0, .05f, .17f));
            Shape(toolNozzle, "Barrel", PrimitiveType.Cylinder, new Vector3(0, 0, .12f), new Vector3(.105f, .15f, .105f), dark).localRotation = Quaternion.Euler(90, 0, 0);
            Shape(toolNozzle, "Nozzle collar", PrimitiveType.Cylinder, new Vector3(0, 0, .28f), new Vector3(.13f, .035f, .13f), trim).localRotation = Quaternion.Euler(90, 0, 0);
            Shape(toolNozzle, "Nozzle light", PrimitiveType.Cylinder, new Vector3(0, 0, .319f), new Vector3(.081f, .005f, .081f), glow).localRotation = Quaternion.Euler(90, 0, 0);
            // A physical display sits in front of the grip and both reservoir sizes.
            Shape(toolBody, "Siphon readout panel", PrimitiveType.Cube, new Vector3(0, .04f, -.17f), new Vector3(.21f, .08f, .012f), dark);
            toolReadout = Text(toolBody, "0 / 4", new Vector3(0, .04f, -.181f), .019f, new Color(.6f, 1f, .84f), .16f);
            for (int i = 0; i < 8; i++) reservoirCells.Add(Organism(ToolChamber, true, bloomMat, .026f));
            BuildCollectorVisual();
        }

        void Faceplate(Transform parent, string title, Vector3 position, Material accent)
        {
            Shape(parent, "Machine faceplate", PrimitiveType.Cube, position + new Vector3(0, -.08f, .012f), new Vector3(1.34f, .44f, .035f), dark);
            Shape(parent, "Identification stripe", PrimitiveType.Cube, position + new Vector3(-.625f, -.08f, -.013f), new Vector3(.035f, .34f, .008f), accent);
            Text(parent, title, position + new Vector3(.015f, .015f, -.015f), title.Length > 15 ? .047f : .069f, Color.white);
        }

        TextMesh Text(Transform parent, string value, Vector3 position, float size, Color color, float maxWidth = 1.24f)
        {
            var t = Group(value, parent, position).gameObject.AddComponent<TextMesh>();
            t.text = value;
            t.fontSize = 80;
            // Font size controls raster quality. Physical dimensions are fitted separately.
            t.characterSize = .01f;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = color;
            if (!t.font) t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer = t.GetComponent<Renderer>();
            renderer.sharedMaterial = WorldTextMaterial(t.font);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var fit = new LabelFit { Text = t, Height = size * 2f, Width = maxWidth };
            labels.Add(fit);
            FitLabel(fit);
            return t;
        }

        static void FitLabel(LabelFit fit)
        {
            if (!fit.Text) return;
            var bounds = fit.Text.GetComponent<Renderer>().localBounds.size;
            if (bounds.x <= .00001f || bounds.y <= .00001f) return;
            float scale = Mathf.Min(fit.Height / bounds.y, fit.Width / bounds.x);
            fit.Text.transform.localScale = Vector3.one * scale;
        }

        Transform Ring(Transform parent, string name, Vector3 position, float radius, float thickness, Material material)
        {
            const int segments = 48, sides = 8;
            var vertices = new Vector3[segments * sides];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * sides * 6];
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2 / segments;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j < sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides;
                    int n = s * sides + j;
                    normals[n] = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices[n] = outward * radius + normals[n] * thickness;
                    int next = ((s + 1) % segments) * sides;
                    int k = n * 6;
                    triangles[k] = n; triangles[k + 1] = s * sides + (j + 1) % sides; triangles[k + 2] = next + j;
                    triangles[k + 3] = next + j; triangles[k + 4] = s * sides + (j + 1) % sides; triangles[k + 5] = next + (j + 1) % sides;
                }
            }
            var mesh = new Mesh { name = name + " procedural torus", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            var r = Group(name, parent, position);
            r.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            r.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return r;
        }

        public void Refresh(GameModel model)
        {
            lastModel = model;
            bloomCount = model.Available(CultureKind.Bloom);
            flowCount = model.Available(CultureKind.Flow);
            sparkCount = model.Available(CultureKind.Spark);
            mistCount = model.Available(CultureKind.Mist);
            SetToolUpgrade(model.UpgradeOwned);
            RefreshGrower(model, CultureKind.Bloom, StationKind.Bloom, bloomCells);
            RefreshGrower(model, CultureKind.Flow, StationKind.Flow, flowCells);
            RefreshSparkModule(model);
            var incubation = model.Incubator;
            status[StationKind.Incubator].text = incubation.Phase == IncubatorPhase.Incubating ? "GROWING  /  " + Mathf.CeilToInt((float)incubation.SecondsRemaining) + "s" : incubation.Phase == IncubatorPhase.Ready ? "READY  /  DISPLAY OR STORE" : incubation.Bloom + " BLOOM  +  " + incubation.Flow + " FLOW" + (model.SparkResearchUnlocked ? "  +  " + incubation.Spark + " SPARK" : "") + (model.MistResearchUnlocked ? "  +  " + incubation.Mist + " MIST" : "");
            SetCreature(displays["incubator"], incubation.Phase == IncubatorPhase.Dosing ? null : incubation.SpeciesId, incubation.SpecimenId);
            RefreshHatcheryLamp(incubation.Phase);
            RefreshTank(model, "tank-a", StationKind.TankA);
            RefreshTank(model, "tank-b", StationKind.TankB);
            RefreshPlacedDisplays(model);
            RefreshStorageRacks(model);
            RefreshConditioners(model);
            status[StationKind.Upgrade].text = upgraded ? "INSTALLED  /  8 UNIT RESERVOIR" : "24 COINS  /  COLLECT FASTER";
            // The capacity readout includes culture held for safe returns by active processes.
            toolReadout.text = model.ReservedCultureCount + " / " + model.Capacity;
            for (int i = 0; i < reservoirCells.Count; i++)
            {
                bool active = i < bloomCount + flowCount + sparkCount + mistCount;
                CultureKind culture = i < bloomCount ? CultureKind.Bloom : i < bloomCount + flowCount ? CultureKind.Flow : i < bloomCount + flowCount + sparkCount ? CultureKind.Spark : CultureKind.Mist;
                var cell = reservoirCells[i];
                if (active && cell.name != CultureVisualName(culture))
                {
                    cell.gameObject.SetActive(false);
                    Destroy(cell.gameObject);
                    reservoirCells[i] = cell = CultureOrganism(ToolChamber, culture, .026f);
                    cell.name = CultureVisualName(culture);
                }
                cell.gameObject.SetActive(active);
            }
            ReconcileFxState(model);
        }

        void RefreshGrower(GameModel model, CultureKind culture, StationKind station, List<Transform> cells)
        {
            var grower = model.GetGrower(culture);
            status[station].text = grower.Units + " / " + grower.Capacity + (grower.Units >= grower.Capacity ? "  |  FULL" : "  |  GROWING");
            for (int i = 0; i < cells.Count; i++) cells[i].gameObject.SetActive(i < grower.Units);
        }

        void RefreshTank(GameModel model, string id, StationKind kind)
        {
            var tank = model.GetTank(id);
            SetCreature(displays[id], tank.HasValue ? tank.Value.SpeciesId : null, tank.HasValue ? tank.Value.Id : null);
            // The world label is the visible price tag; a reserved order keeps its agreed price instead.
            status[kind].text = !tank.HasValue ? "EMPTY  /  1 SPACE" : SpeciesName(tank.Value.SpeciesId).ToUpperInvariant() +
                (model.GetReservation(id).HasValue ? "  /  RESERVED" : "  /  " + model.GetPrice(tank.Value.SpeciesId).Value.Price + " CREDITS");
        }

        static string SpeciesName(string id) => id == "pondlet" ? "Pondlet" : id == "bubblebelly" ? "Bubblebelly" : id == "ribbonfin" ? "Ribbonfin" : id == "lanternfin" ? "Lanternfin" : id == "driftwing" ? "Driftwing" : "Unknown";

        void SetCreature(Display display, string species, string specimenId = null)
        {
            bool identityChanged = display.FxSpecimenId != specimenId;
            display.FxSpecimenId = specimenId;
            if (display.SpeciesId == species) { if (identityChanged) ResetPondletIdentity(display); return; }
            if (display.Creature) Destroy(display.Creature.gameObject);
            display.SpeciesId = species;
            display.Creature = null;
            if (string.IsNullOrEmpty(species)) return;
            var r = Group(species, display.Root, Vector3.zero);
            r.localScale = Vector3.one * (display.Incubator ? (species == "ribbonfin" ? .54f : species == "driftwing" ? .52f : .62f) : species == "driftwing" ? .59f : .69f);
            display.Creature = r.gameObject.AddComponent<CreatureVisual>();
            display.Creature.Build(species);
            ResetPondletIdentity(display);
            if (species == "lanternfin" || species == "driftwing")
            {
                r.localRotation = Quaternion.Euler(0, -90, 0);
                display.Creature.Animate(lastTime + display.Seed);
            }
        }

        public void SetToolUpgrade(bool value)
        {
            upgraded = value;
            if (!ToolChamber) return;
            ToolChamber.localScale = value ? new Vector3(1.35f, 1.48f, 1.35f) : Vector3.one;
            ToolChamber.localPosition = new Vector3(0, value ? .067f : .02f, .025f);
            toolUpgradeBand.gameObject.SetActive(value);
            // The authored reservoir cap shares this chamber's transform at both capacities.
        }

        public void SetHarvesting(bool active, Station station)
        {
            bool next = active && TryHarvestCulture(station, out harvestingCulture);
            bool previous = harvesting;
            float releasedCurl = FxHandPose.Resolve(handAction, handAge, previous, handReleaseCurl, fxReducedMotion).RightCurl;
            if (previous && !next) CancelFxTransient();
            harvesting = next;
            labHarvestSource = next && station ? station.transform : null;
            if (previous && !next) handReleaseCurl = releasedCurl;
            HandCollectEdge(next, previous);
        }

        public void PulseHarvest(CultureKind kind)
        {
            harvestKick = 1;
            if (kind == CultureKind.Bloom || kind == CultureKind.Flow) return;
            SpawnParticle(kind);
            SpawnParticle(kind);
        }

        void SpawnParticle(CultureKind kind)
        {
            if (!ToolChamber) return;
            var source = kind == CultureKind.Bloom ? bloomRoot : kind == CultureKind.Flow ? flowRoot :
                kind == CultureKind.Spark && lastModel != null && lastModel.SparkResearchUnlocked ? upgradeRoot :
                kind == CultureKind.Mist && lastModel != null && lastModel.MistResearchUnlocked && lastModel.ConditionerIds.Length > 0 &&
                TryGetConditioner(lastModel.ConditionerIds[0], out var vatRoot) ? vatRoot : null;
            if (!source) return;
            var p = CultureOrganism(transform, kind, .095f);
            Vector3 from = source.TransformPoint(kind == CultureKind.Spark ? new Vector3(0, 1.5f, -.27f) : kind == CultureKind.Mist ? new Vector3(0, .85f, -.53f) : new Vector3(.36f, 1.0f, -.76f));
            p.position = from;
            particles.Add(new Particle { Root = p, Start = from, Seed = particles.Count * 1.73f });
        }

        public void Animate(float time, float dt)
        {
            lastTime = time;
            TickFxPresentation(dt);
            // Repeat fitting after TextMesh has generated its mesh and after dynamic status changes.
            foreach (var label in labels) FitLabel(label);
            // Font rebuild callbacks handle atlas swaps even while the game is paused.
            // This also keeps the material current across renderer/device refreshes.
            foreach (var font in fontMaterials.Keys) FontAtlasRebuilt(font);
            // Bloom/Flow have one pose writer in TickLabFx.
            AnimateSparkCells(time);
            AnimateConditioners(time);
            // The accepted opaque hatchery frame remains fixed; use visible fluid/needle instead.
            for (int i = 0; i < reservoirCells.Count; i++)
            {
                float angle = i * 2.399f + time * .6f;
                reservoirCells[i].localPosition = new Vector3(Mathf.Cos(angle) * .052f, -.062f + (i % 4) * .037f + Mathf.Sin(time * 2 + i) * .008f, Mathf.Sin(angle) * .052f);
                reservoirCells[i].localRotation = Quaternion.Euler(Mathf.Sin(time + i) * 15, time * 31 + i * 52, Mathf.Cos(time * 1.3f + i) * 17);
            }
            foreach (var display in displays.Values)
            {
                if (!display.Creature) continue;
                if (display.SpeciesId == "pondlet" && display.Habitat != null) { TickPondlet(display, dt); continue; }
                float t = time * (display.SpeciesId == "bubblebelly" ? .6f : .78f) + display.Seed;
                // The longest tail stays mostly broadside; its small orbit reserves tail clearance.
                float x = display.Incubator ? Mathf.Sin(t) * .04f : Mathf.Sin(t) * (display.SpeciesId == "ribbonfin" ? .10f : .42f);
                float z = display.Incubator ? 0 : Mathf.Sin(t * .63f) * .07f;
                display.Creature.transform.localPosition = new Vector3(x, Mathf.Sin(t * 1.8f) * .085f, z);
                float turn = display.Incubator ? 20 : display.SpeciesId == "ribbonfin" ? 18 : 28;
                display.Creature.transform.localRotation = Quaternion.Euler(0, -90 + Mathf.Cos(t) * turn, Mathf.Sin(t * 1.6f) * 5);
                display.Creature.Animate(time + display.Seed);
            }
            TickAquariumHabitat(dt);
            if (harvesting && harvestingCulture != CultureKind.Bloom && harvestingCulture != CultureKind.Flow)
            {
                emissionClock -= dt;
                if (emissionClock <= 0)
                {
                    emissionClock = .12f;
                    SpawnParticle(harvestingCulture);
                }
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += dt;
                float f = Mathf.Clamp01(p.Age / .52f);
                Vector3 target = ToolChamber.position;
                p.Root.position = Vector3.Lerp(p.Start, target, f * f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * .24f;
                p.Root.Rotate(0, dt * 180, dt * 70, Space.Self);
                p.Root.localScale = Vector3.one * Mathf.Lerp(.095f, .025f, f);
                if (f >= 1) { Destroy(p.Root.gameObject); particles.RemoveAt(i); }
            }
            harvestKick = Mathf.MoveTowards(harvestKick, 0, dt * 5);
            TickHandsPose(dt);
            TickLabFx(time, dt);
        }

        static void AnimateCells(List<Transform> cells, float time, float seed)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                float a = i * 2.399f + time * .29f + seed;
                cells[i].localPosition = new Vector3(Mathf.Cos(a) * .27f, 1.15f + (i % 3) * .22f + Mathf.Sin(time * 1.7f + i) * .065f, Mathf.Sin(a) * .27f);
                cells[i].localRotation = Quaternion.Euler(Mathf.Sin(time + i) * 20, -a * Mathf.Rad2Deg, Mathf.Sin(time * 2 + i) * 22);
            }
        }

        /// <summary>Validation hook: conservative bounds of every visible fish mesh against tank interior.</summary>
        public bool ValidateTankBounds(out string details)
        {
            int residents = 0;
            foreach (var pair in displays)
            {
                var display = pair.Value;
                if (display.Incubator || !display.Creature) continue;
                residents++;
                var tank = display.Root.parent;
                foreach (var renderer in display.Creature.GetComponentsInChildren<Renderer>())
                {
                    Bounds bounds = renderer.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 local = tank.InverseTransformPoint(corner);
                        if (!display.Interior.Contains(local))
                        {
                            details = pair.Key + " / " + display.SpeciesId + " / " + renderer.name + " outside tank at " + local;
                            return false;
                        }
                    }
                }
            }
            details = residents == 0 ? "No stocked tank residents to inspect." : residents + " stocked tank resident(s), all mesh bounds inside glass.";
            return true;
        }
    }
}
