using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D tier set pieces (AD.4: "each tier adds a set piece"), shown while the look is on. They are decorative children
    /// of the wing that tier opens, built into its walls and ceiling, never on the floor the player arranges furniture on
    /// (placed items keep 0.2 m off a wing's walls): store wing 1 a bay window with a cushioned bench, recessed into the
    /// front wall, and glass bowls hanging from the ceiling; store wing 2 a feature tank recessed into its front wall, with
    /// kit creatures; lab wing 1 an overhead gallery of culture pipes; lab wing 2 a glass dome hanging from the ceiling,
    /// glowing with Spark and Mist once they are researched. A recess replaces its wall's art with the same panelling and
    /// panels around the opening; the classic wall keeps its collider, so nobody walks into a recess. Switching the look
    /// off shows the classic wing again.
    /// </summary>
    public sealed partial class ShopWorld
    {
        public enum DioramaSetPiece { WindowBench, HangingBowls, FeatureTank, PipeGallery, GlassDome }

        sealed class DioramaWingSet
        {
            public Transform Wing, Look;
            public GameObject ClassicArt;
            public Renderer ClassicWall;
            public bool WallWasShown;
        }

        sealed class DioramaSwimmer { public Transform Pivot; public CreatureVisual Creature; public Vector3 Centre, Radii; public float Speed, Phase; }

        readonly DioramaWingSet[] dioramaStoreSets = new DioramaWingSet[2], dioramaLabSets = new DioramaWingSet[2];
        readonly List<(Transform bowl, float phase)> dioramaBowls = new List<(Transform, float)>();
        readonly List<(Transform fish, Vector3 centre, float radius, float phase)> dioramaBowlFish = new List<(Transform, Vector3, float, float)>();
        readonly List<DioramaSwimmer> dioramaTankSwimmers = new List<DioramaSwimmer>();
        readonly List<(Transform mote, Vector3 centre, float radius, float speed, float phase, bool mist)> dioramaDomeMotes = new List<(Transform, Vector3, float, float, float, bool)>();
        Light dioramaFeatureTankGlow, dioramaDomeLight, dioramaBayLamp;
        Material dioramaFeatureWater, dioramaSparkMote, dioramaMistMote;
        float dioramaSetPieceClock;

        /// <summary>Whether the dome shows a culture's glow now (validation and the route).</summary>
        public bool DioramaDomeGlows(CultureKind culture)
        {
            foreach (var (mote, _, _, _, _, mist) in dioramaDomeMotes)
                if (mote && mist == (culture == CultureKind.Mist) && mote.gameObject.activeInHierarchy) return true;
            return false;
        }

        /// <summary>Whether a set piece is built and shown now (validation and the route).</summary>
        public bool DioramaSetPieceShown(DioramaSetPiece piece)
        {
            var root = DioramaSetPieceRoot(piece);
            return dioramaActive && root && root.gameObject.activeInHierarchy;
        }

        public Transform DioramaSetPieceRoot(DioramaSetPiece piece)
        {
            var set = piece == DioramaSetPiece.WindowBench || piece == DioramaSetPiece.HangingBowls ? dioramaStoreSets[0] :
                piece == DioramaSetPiece.FeatureTank ? dioramaStoreSets[1] : piece == DioramaSetPiece.PipeGallery ? dioramaLabSets[0] : dioramaLabSets[1];
            if (set == null || !set.Look) return null;
            string name = piece == DioramaSetPiece.WindowBench ? "Bay window bench" : piece == DioramaSetPiece.HangingBowls ? "Hanging fish bowls" :
                piece == DioramaSetPiece.FeatureTank ? "Feature tank" : piece == DioramaSetPiece.PipeGallery ? "Overhead culture pipe gallery" : "Spark and Mist glass dome";
            return set.Look.Find(name);
        }

        /// <summary>Builds the set pieces of every open wing (once each) and shows them with the look, or the classic wings.</summary>
        void RefreshDioramaSetPieces()
        {
            for (int i = 0; i < 2; i++)
            {
                if (storeWings[i] && (dioramaStoreSets[i] == null || dioramaStoreSets[i].Wing != storeWings[i]) && dioramaActive) BuildStoreSetPiece(i);
                if (labWings[i] && (dioramaLabSets[i] == null || dioramaLabSets[i].Wing != labWings[i]) && dioramaActive) BuildLabSetPiece(i);
                ShowDioramaWingSet(dioramaStoreSets[i]); ShowDioramaWingSet(dioramaLabSets[i]);
            }
        }

        void ShowDioramaWingSet(DioramaWingSet set)
        {
            if (set == null || !set.Look) return;
            set.Look.gameObject.SetActive(dioramaActive);
            if (set.ClassicArt) set.ClassicArt.SetActive(!dioramaActive);
            if (set.ClassicWall) set.ClassicWall.enabled = dioramaActive ? false : set.WallWasShown;
        }

        Material DioramaOwn(Material m) { dioramaOwned.Add(m); return m; }

        Transform DioramaPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
            => Shape(parent, name, type, position, scale, material);

        /// <summary>The wall pieces around a recess in a store wing's front wall (world z -5.75 to -5.45), with the
        /// classic wall's material; and the wing's art rebuilt as the classic has it, except the front panel.</summary>
        Transform StoreWingLook(int index, out DioramaWingSet set)
        {
            var wing = storeWings[index];
            float x = -9 - index * 4;
            set = new DioramaWingSet { Wing = wing };
            set.Look = Group("Diorama wing look (AD-D)", wing, Vector3.zero);
            var art = wing.Find("Aquatic gallery extension art");
            set.ClassicArt = art ? art.gameObject : null;
            var wall = wing.Find("Expanded store front wall");
            set.ClassicWall = wall ? wall.GetComponent<Renderer>() : null;
            set.WallWasShown = set.ClassicWall && set.ClassicWall.enabled;
            // The same floor and rear wall as the classic wing (DecorateAquaticStoreWing).
            var look = set.Look;
            var built = Group("Wing art", look, Vector3.zero);
            AquaticPlankFloor(built, x - 2, 2, 2);
            var rear = Group("Gallery aquatic rear wall", built, new Vector3(x, 0, .355f));
            ShopPanelling(rear, 3.90f);
            AquaticScenePanel(rear, new Vector3(0, 2.04f, -.074f), 3.66f, 1.38f, "AQUATIC DISCOVERIES", index == 1);
            SealArt(built);
            return look;
        }

        /// <summary>The look's own lift: like the walls it replaces, a tall or overhead piece casts no shadow, so the sun
        /// can reach into an open diorama.</summary>
        static void DioramaLiftShadows(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var b = renderer.bounds;
                if (b.size.y >= 3f || b.min.y >= 3.5f) renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void RecessWall(Transform parent, float x, float halfWidth, float bottom, float top, Material wall)
        {
            // Around an opening x ±halfWidth, y bottom..top, in a store wing's front wall (centre z -5.6, 0.3 thick).
            float side = 2 - halfWidth;
            DioramaPart(parent, "Front wall beside the recess", PrimitiveType.Cube, new Vector3(x - halfWidth - side * .5f, 2.1f, -5.6f), new Vector3(side, 4.2f, .3f), wall);
            DioramaPart(parent, "Front wall beside the recess", PrimitiveType.Cube, new Vector3(x + halfWidth + side * .5f, 2.1f, -5.6f), new Vector3(side, 4.2f, .3f), wall);
            DioramaPart(parent, "Front wall above the recess", PrimitiveType.Cube, new Vector3(x, (top + 4.2f) * .5f, -5.6f), new Vector3(halfWidth * 2, 4.2f - top, .3f), wall);
            if (bottom > 0) DioramaPart(parent, "Front wall below the recess", PrimitiveType.Cube, new Vector3(x, bottom * .5f, -5.6f), new Vector3(halfWidth * 2, bottom, .3f), wall);
            // The wainscot and rails continue either side, as the classic front wall has them.
            for (int s = -1; s <= 1; s += 2)
            {
                var panelling = Group("Gallery front panelling", parent, new Vector3(x + s * (halfWidth + side * .5f), 0, -5.38f));
                panelling.localRotation = Quaternion.Euler(0, 180, 0);
                ShopPanelling(panelling, side - .04f);
            }
        }

        void BuildStoreSetPiece(int index)
        {
            var look = StoreWingLook(index, out var set);
            dioramaStoreSets[index] = set;
            float x = -9 - index * 4;
            var wall = transform.Find("Right wall").GetComponent<Renderer>().sharedMaterial;
            if (index == 0) BuildWindowBench(look, x, wall); else BuildFeatureTank(look, x, wall);
            DioramaLiftShadows(look);
        }

        void BuildWindowBench(Transform look, float x, Material wall)
        {
            var piece = Group("Bay window bench", look, Vector3.zero);
            var built = Group("Built in", piece, Vector3.zero);
            BuildBayWindow(built, x, wall);
            var street = GameObject.CreatePrimitive(PrimitiveType.Quad);
            street.name = "Bay street window";
            var streetCollider = street.GetComponent<Collider>(); if (streetCollider) { streetCollider.enabled = false; Destroy(streetCollider); }
            street.transform.SetParent(piece, false);
            street.transform.position = new Vector3(x, 1.62f, -6.14f);
            street.transform.rotation = Quaternion.Euler(0, 180, 0);
            street.transform.localScale = new Vector3(1.9f, 1.05f, 1);
            var streetRenderer = street.GetComponent<MeshRenderer>();
            streetRenderer.sharedMaterial = dioramaWindowMaterial; streetRenderer.shadowCastingMode = ShadowCastingMode.Off; streetRenderer.receiveShadows = false;
            SealArt(built);
            dioramaBayLamp = Group("Bay reading light", piece, new Vector3(x, 2.3f, -5.85f)).gameObject.AddComponent<Light>();
            dioramaBayLamp.type = LightType.Point; dioramaBayLamp.color = new Color(1, .82f, .6f); dioramaBayLamp.range = 1.8f; dioramaBayLamp.intensity = .9f;
            dioramaBayLamp.shadows = LightShadows.None;
            // The hanging bowls: three glass bowls on cords from the ceiling, each with water, sand, a sprig and a fish.
            var bowls = Group("Hanging fish bowls", look, Vector3.zero);
            var places = new[] { new Vector3(x - 1.15f, 3.02f, -1.55f), new Vector3(x + .15f, 2.86f, -3.05f), new Vector3(x + 1.15f, 3.1f, -4.35f) };
            for (int i = 0; i < places.Length; i++) BuildHangingBowl(bowls, places[i], i);
        }

        void BuildBayWindow(Transform piece, float x, Material wall)
        {
            RecessWall(piece, x, 1.2f, 0, 2.5f, wall);
            // The bay: an alcove behind the wall (z -5.45 to -6.2) with a cushioned bench under a street window.
            DioramaPart(piece, "Bay side wall", PrimitiveType.Cube, new Vector3(x - 1.22f, 1.25f, -5.975f), new Vector3(.04f, 2.5f, .45f), wall);
            DioramaPart(piece, "Bay side wall", PrimitiveType.Cube, new Vector3(x + 1.22f, 1.25f, -5.975f), new Vector3(.04f, 2.5f, .45f), wall);
            ArtBox(piece, "Bay ceiling", new Vector3(x, 2.53f, -5.83f), new Vector3(2.48f, .06f, .78f), artHoney);
            ArtBox(piece, "Bay floor", new Vector3(x, .012f, -5.83f), new Vector3(2.44f, .024f, .76f), artHoney);
            DioramaPart(piece, "Bay back wall", PrimitiveType.Cube, new Vector3(x, 1.25f, -6.21f), new Vector3(2.48f, 2.5f, .04f), wall);
            ArtBox(piece, "Bay window frame", new Vector3(x, 1.62f, -6.17f), new Vector3(2.06f, 1.2f, .05f), artPaleOak);
            ArtBox(piece, "Bay window mullion", new Vector3(x, 1.62f, -6.11f), new Vector3(.05f, 1.08f, .05f), cream);
            ArtBox(piece, "Bay window transom", new Vector3(x, 1.86f, -6.11f), new Vector3(1.92f, .045f, .05f), cream);
            ArtBox(piece, "Bay window sill", new Vector3(x, 1.03f, -6.08f), new Vector3(2.1f, .06f, .2f), artPaleOak);
            // The bench: a honey cabinet, a coral cushion, two pillows, a little plant and a stack of books.
            ArtBox(piece, "Window bench cabinet", new Vector3(x, .22f, -5.95f), new Vector3(2.3f, .44f, .46f), artHoney);
            for (int s = -1; s <= 1; s += 2)
                ArtBox(piece, "Window bench drawer", new Vector3(x + s * .56f, .22f, -5.715f), new Vector3(.9f, .26f, .02f), artPaleOak);
            ArtBox(piece, "Window bench cushion", new Vector3(x, .5f, -5.95f), new Vector3(2.22f, .12f, .44f), artCoral);
            ArtBox(piece, "Window bench pillow", new Vector3(x - .62f, .72f, -6.1f), new Vector3(.52f, .34f, .14f), artButter).localRotation = Quaternion.Euler(-12, 0, 6);
            ArtBox(piece, "Window bench pillow", new Vector3(x + .64f, .71f, -6.1f), new Vector3(.48f, .32f, .14f), artMint).localRotation = Quaternion.Euler(-12, 0, -5);
            ArtBox(piece, "Plant pot", new Vector3(x + 1.0f, .65f, -5.92f), new Vector3(.16f, .2f, .16f), artCoral);
            for (int i = 0; i < 3; i++)
                DioramaPart(piece, "Potted plant leaf", PrimitiveType.Sphere, new Vector3(x + 1.0f + (i - 1) * .05f, .82f + (i % 2) * .06f, -5.92f), new Vector3(.12f, .2f, .07f), artLeaf)
                    .localRotation = Quaternion.Euler(0, 0, (i - 1) * 24);
            ArtBox(piece, "Book", new Vector3(x - 1.0f, .59f, -5.9f), new Vector3(.24f, .05f, .17f), artTeal);
            ArtBox(piece, "Book", new Vector3(x - .99f, .635f, -5.9f), new Vector3(.22f, .04f, .16f), artButter).localRotation = Quaternion.Euler(0, 9, 0);
            // A striped valance over the recess, on the room side.
            for (int i = 0; i < 8; i++)
                ArtBox(piece, "Valance stripe", new Vector3(x - 1.05f + i * .3f, 2.43f, -5.43f), new Vector3(.3f, .16f, .05f), i % 2 == 0 ? artCoral : cream);
        }

        void BuildHangingBowl(Transform parent, Vector3 centre, int index)
        {
            // The pivot hangs at the ceiling (y 4.16); the bowl swings gently beneath it.
            var pivot = Group("Hanging bowl " + (index + 1), parent, new Vector3(centre.x, 4.16f, centre.z));
            float drop = 4.16f - centre.y;
            DioramaPart(pivot, "Bowl cord", PrimitiveType.Cylinder, new Vector3(0, -drop * .5f + .12f, 0), new Vector3(.012f, drop * .5f - .12f, .012f), dark);
            DioramaPart(pivot, "Ceiling hook", PrimitiveType.Sphere, new Vector3(0, -.02f, 0), new Vector3(.07f, .04f, .07f), gold);
            var bowl = Group("Bowl", pivot, new Vector3(0, -drop, 0));
            bowl.localScale = Vector3.one * 1.3f;
            DioramaPart(bowl, "Bowl glass", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .5f, glass);
            DioramaPart(bowl, "Bowl water", PrimitiveType.Sphere, new Vector3(0, -.05f, 0), new Vector3(.44f, .36f, .44f), water);
            DioramaPart(bowl, "Bowl sand", PrimitiveType.Sphere, new Vector3(0, -.2f, 0), new Vector3(.3f, .07f, .3f), cream);
            var rim = Ring(bowl, "Bowl rim", new Vector3(0, .17f, 0), .16f, .012f, gold);
            dioramaOwned.Add(rim.GetComponent<MeshFilter>().sharedMesh);
            for (int s = 0; s < 3; s++)
                DioramaPart(bowl, "Bowl cradle strap", PrimitiveType.Cylinder, new Vector3(Mathf.Cos(s * 2.094f) * .16f, .3f, Mathf.Sin(s * 2.094f) * .16f), new Vector3(.008f, .14f, .008f), gold)
                    .localRotation = Quaternion.Euler(Mathf.Sin(s * 2.094f) * 28, 0, -Mathf.Cos(s * 2.094f) * 28);
            DioramaPart(bowl, "Bowl sprig", PrimitiveType.Sphere, new Vector3(.06f, -.1f, .03f), new Vector3(.04f, .16f, .03f), artLeaf).localRotation = Quaternion.Euler(0, 0, -12);
            var fishColour = index == 0 ? artCoral : index == 1 ? artButter : artMint;
            var fish = Group("Bowl fish", bowl, new Vector3(0, -.04f, 0));
            DioramaPart(fish, "Fish body", PrimitiveType.Sphere, Vector3.zero, new Vector3(.05f, .045f, .085f), fishColour);
            DioramaPart(fish, "Fish tail", PrimitiveType.Sphere, new Vector3(0, 0, -.055f), new Vector3(.012f, .04f, .03f), fishColour);
            DioramaPart(fish, "Fish eye", PrimitiveType.Sphere, new Vector3(.018f, .01f, .028f), Vector3.one * .012f, black);
            DioramaPart(fish, "Fish eye", PrimitiveType.Sphere, new Vector3(-.018f, .01f, .028f), Vector3.one * .012f, black);
            dioramaBowls.Add((pivot, index * 1.9f));
            dioramaBowlFish.Add((fish, new Vector3(0, -.04f, 0), .1f, index * 2.3f));
        }

        void BuildFeatureTank(Transform look, float x, Material wall)
        {
            var piece = Group("Feature tank", look, Vector3.zero);
            var built = Group("Built in", piece, Vector3.zero);
            BuildFeatureTankBox(built, x, wall);
            dioramaFeatureWater = DioramaOwn(DioramaLook.Glass("feature tank water", new Color(.3f, .78f, .88f, .12f), .22f, .45f));
            dioramaFeatureWater.SetFloat("_WobbleAmount", .01f);
            dioramaFeatureWater.SetFloat("_FarSide", 2.5f);
            const float back = -6.35f, front = -5.47f, mid = (back + front) * .5f, depth = front - back;
            var block = Group("Tank water", piece, new Vector3(x, 1.56f, mid));
            block.localScale = new Vector3(2.96f, 2.32f, depth - .02f);
            block.gameObject.AddComponent<MeshFilter>().sharedMesh = dioramaWaterMesh;
            var blockRenderer = block.gameObject.AddComponent<MeshRenderer>();
            blockRenderer.sharedMaterial = dioramaFeatureWater; blockRenderer.shadowCastingMode = ShadowCastingMode.Off;
            DioramaPart(piece, "Tank front glass", PrimitiveType.Cube, new Vector3(x, 1.6f, front), new Vector3(3.0f, 2.3f, .015f), glass);
            dioramaFeatureTankGlow = Group("Feature tank night glow", piece, new Vector3(x, 1.7f, -5.7f)).gameObject.AddComponent<Light>();
            dioramaFeatureTankGlow.type = LightType.Point; dioramaFeatureTankGlow.color = new Color(.45f, .92f, 1f);
            dioramaFeatureTankGlow.range = 3.2f; dioramaFeatureTankGlow.intensity = 0; dioramaFeatureTankGlow.shadows = LightShadows.None;
            // Three kit residents swim slow loops: a Bubblebelly, a Ribbonfin and a Lanternfin.
            var swimmers = new[] { ("bubblebelly", new Vector3(-.45f, 1.55f, -5.95f), new Vector3(.75f, .25f, .22f), .32f),
                ("ribbonfin", new Vector3(.4f, 2.1f, -5.9f), new Vector3(.85f, .18f, .2f), .27f),
                ("lanternfin", new Vector3(.1f, 1.1f, -6.0f), new Vector3(.95f, .12f, .18f), .22f) };
            for (int i = 0; i < swimmers.Length; i++)
            {
                var (species, centre, radii, speed) = swimmers[i];
                var pivot = Group("Feature tank resident " + species, piece, new Vector3(x, 0, 0) + centre);
                var creature = new GameObject("Kit " + species).AddComponent<CreatureVisual>();
                creature.transform.SetParent(pivot, false);
                creature.Build(species);
                creature.transform.localScale = Vector3.one * .9f;
                dioramaTankSwimmers.Add(new DioramaSwimmer { Pivot = pivot, Creature = creature, Centre = new Vector3(x, 0, 0) + centre, Radii = radii, Speed = speed, Phase = i * 2.1f });
            }
        }

        void BuildFeatureTankBox(Transform piece, float x, Material wall)
        {
            RecessWall(piece, x, 1.5f, .45f, 2.75f, wall);
            const float back = -6.35f, front = -5.47f, mid = (back + front) * .5f, depth = front - back;
            DioramaPart(piece, "Tank back wall", PrimitiveType.Cube, new Vector3(x, 1.6f, back - .02f), new Vector3(3.04f, 2.34f, .04f), artTeal);
            ArtBox(piece, "Tank painted background", new Vector3(x, 1.7f, back + .015f), new Vector3(2.9f, 2.0f, .02f), artSky);
            for (int s = -1; s <= 1; s += 2)
                DioramaPart(piece, "Tank side wall", PrimitiveType.Cube, new Vector3(x + s * 1.52f, 1.6f, mid), new Vector3(.04f, 2.34f, depth + .04f), artTeal);
            DioramaPart(piece, "Tank top cover", PrimitiveType.Cube, new Vector3(x, 2.77f, mid), new Vector3(3.04f, .04f, depth + .04f), dark);
            var light = DioramaOwn(DioramaLook.Toon("feature tank lamp", new Color(.9f, 1f, 1f), DioramaLook.Style.Prop));
            light.SetColor("_EmissionColor", new Color(.7f, .9f, .95f) * 1.2f);
            DioramaPart(piece, "Tank lamp strip", PrimitiveType.Cube, new Vector3(x, 2.735f, mid), new Vector3(2.8f, .03f, .5f), light);
            var sand = DioramaOwn(DioramaLook.Toon("feature tank sand", new Color(.93f, .84f, .64f), DioramaLook.Style.Prop));
            sand.SetFloat("_CausticStrength", 1);
            DioramaPart(piece, "Tank sand", PrimitiveType.Cube, new Vector3(x, .52f, mid), new Vector3(2.98f, .1f, depth), sand);
            var rock = DioramaOwn(DioramaLook.Toon("feature tank rock", new Color(.47f, .55f, .62f), DioramaLook.Style.Prop));
            rock.SetFloat("_CausticStrength", .75f);
            var rocks = new[] { new Vector4(-1.05f, .62f, -6.1f, .32f), new Vector4(-.78f, .6f, -5.9f, .2f), new Vector4(.9f, .63f, -6.15f, .38f), new Vector4(1.15f, .6f, -5.85f, .18f) };
            foreach (var r in rocks)
                DioramaPart(piece, "Tank river stone", PrimitiveType.Sphere, new Vector3(x + r.x, r.y, r.z), new Vector3(r.w, r.w * .7f, r.w * .9f), rock);
            for (int i = 0; i < 7; i++)
            {
                float fx = -1.25f + i * .42f, height = .5f + (i * 37 % 5) * .12f;
                DioramaPart(piece, "Tank frond", PrimitiveType.Sphere, new Vector3(x + fx, .57f + height * .5f, -6.18f + (i % 2) * .12f), new Vector3(.07f, height, .05f), artLeaf)
                    .localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * 6);
            }
            // A honey frame round the opening on the room side.
            ArtBox(piece, "Feature tank frame", new Vector3(x, 2.79f, -5.42f), new Vector3(3.16f, .1f, .06f), artHoney);
            ArtBox(piece, "Feature tank frame", new Vector3(x, .41f, -5.42f), new Vector3(3.16f, .1f, .06f), artHoney);
            for (int s = -1; s <= 1; s += 2)
                ArtBox(piece, "Feature tank frame", new Vector3(x + s * 1.53f, 1.6f, -5.42f), new Vector3(.1f, 2.48f, .06f), artHoney);
            SealArt(piece);
        }

        void BuildLabSetPiece(int index)
        {
            var wing = labWings[index];
            var set = new DioramaWingSet { Wing = wing, Look = Group("Diorama wing look (AD-D)", wing, Vector3.zero) };
            dioramaLabSets[index] = set;
            float z = 7.5f + index * 4;
            if (index == 0) BuildPipeGallery(set.Look, z); else BuildGlassDome(set.Look, z);
            DioramaLiftShadows(set.Look);
        }

        void BuildPipeGallery(Transform look, float z)
        {
            var piece = Group("Overhead culture pipe gallery", look, Vector3.zero);
            for (int s = -1; s <= 1; s += 2)
                DioramaPart(piece, "Gallery rail", PrimitiveType.Cube, new Vector3(0, 3.66f, z + s * .78f), new Vector3(12.6f, .05f, .05f), gold);
            for (int i = -2; i <= 2; i++)
            {
                for (int s = -1; s <= 1; s += 2)
                    DioramaPart(piece, "Gallery hanger", PrimitiveType.Cylinder, new Vector3(i * 2.6f, 3.92f, z + s * .78f), new Vector3(.025f, .25f, .025f), dark);
                DioramaPart(piece, "Gallery cross bar", PrimitiveType.Cube, new Vector3(i * 2.6f, 3.66f, z), new Vector3(.05f, .05f, 1.6f), gold);
            }
            for (int c = 0; c < 4; c++)
            {
                var kind = (CultureKind)c;
                float pz = z - .45f + c * .3f;
                var start = new Vector3(-6.55f, 3.5f, pz); var end = new Vector3(6.55f, 3.5f, pz);
                var pipe = DioramaPipeRun(piece, "Gallery " + kind + " main", kind, new[] { start, end }, .055f);
                DioramaPart(piece, "Pipe wall flange", PrimitiveType.Cylinder, new Vector3(-6.62f, 3.5f, pz), new Vector3(.16f, .03f, .16f), gold).localRotation = Quaternion.Euler(0, 0, 90);
                DioramaPart(piece, "Pipe wall flange", PrimitiveType.Cylinder, new Vector3(6.62f, 3.5f, pz), new Vector3(.16f, .03f, .16f), gold).localRotation = Quaternion.Euler(0, 0, 90);
                dioramaGalleryPipes.Add(pipe);
            }
            // A valve wheel and a gauge on the junction box in the middle.
            ArtBox(piece, "Gallery junction box", new Vector3(0, 3.5f, z), new Vector3(.5f, .3f, 1.3f), artTeal);
            var dial = DioramaPart(piece, "Gallery gauge", PrimitiveType.Cylinder, new Vector3(.27f, 3.5f, z - .3f), new Vector3(.2f, .01f, .2f), cream);
            dial.localRotation = Quaternion.Euler(0, 0, 90);
            DioramaPart(piece, "Gauge needle", PrimitiveType.Cube, new Vector3(.285f, 3.52f, z - .3f), new Vector3(.004f, .07f, .01f), artCoral).localRotation = Quaternion.Euler(30, 0, 0);
            var wheel = Ring(piece, "Gallery valve wheel", new Vector3(.27f, 3.5f, z + .3f), .085f, .012f, artCoral);
            wheel.localRotation = Quaternion.Euler(0, 0, 90);
            dioramaOwned.Add(wheel.GetComponent<MeshFilter>().sharedMesh);
        }

        void BuildGlassDome(Transform look, float z)
        {
            var piece = Group("Spark and Mist glass dome", look, new Vector3(0, 0, z));
            var ring = Ring(piece, "Dome ceiling ring", new Vector3(0, 4.12f, 0), 1.16f, .035f, gold);
            dioramaOwned.Add(ring.GetComponent<MeshFilter>().sharedMesh);
            var domeGlass = DioramaOwn(DioramaLook.Glass("Spark and Mist dome", new Color(.82f, .93f, 1f, .1f), .55f, .8f));
            var dome = DioramaPart(piece, "Dome glass", PrimitiveType.Sphere, new Vector3(0, 4.15f, 0), new Vector3(2.2f, 1.76f, 2.2f), domeGlass);
            dome.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 4; i++)
            {
                // A vertical rib squashed to the dome's height (0.88 m against 1.1 m across).
                var rib = Ring(piece, "Dome rib", new Vector3(0, 4.15f, 0), 1.11f, .012f, gold);
                rib.localRotation = Quaternion.Euler(90, i * 45, 0);
                rib.localScale = new Vector3(1, 1, .8f);
                dioramaOwned.Add(rib.GetComponent<MeshFilter>().sharedMesh);
            }
            DioramaPart(piece, "Dome finial", PrimitiveType.Sphere, new Vector3(0, 3.27f, 0), Vector3.one * .09f, gold);
            dioramaSparkMote = DioramaOwn(DioramaLook.Toon("spark mote", new Color(1f, .82f, .35f), DioramaLook.Style.Glint));
            dioramaSparkMote.SetColor("_EmissionColor", new Color(1f, .7f, .2f) * 2.4f);
            dioramaMistMote = DioramaOwn(DioramaLook.Glass("mist puff", new Color(.75f, .97f, .97f, .35f), .6f, .3f));
            dioramaMistMote.SetColor("_EmissionColor", new Color(.4f, .7f, .72f));
            for (int i = 0; i < 14; i++)
            {
                bool mist = i >= 8;
                var mote = DioramaPart(piece, mist ? "Mist puff" : "Spark mote", PrimitiveType.Sphere, Vector3.zero,
                    Vector3.one * (mist ? .16f + (i % 3) * .04f : .045f + (i % 3) * .015f), mist ? dioramaMistMote : dioramaSparkMote);
                mote.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                dioramaDomeMotes.Add((mote, new Vector3(0, mist ? 3.75f : 3.85f, 0), mist ? .55f + (i % 3) * .12f : .35f + (i % 4) * .14f,
                    (mist ? .25f : .7f) * (i % 2 == 0 ? 1 : -1), i * .9f, mist));
            }
            dioramaDomeLight = Group("Dome inner light", piece, new Vector3(0, 3.7f, 0)).gameObject.AddComponent<Light>();
            dioramaDomeLight.type = LightType.Point; dioramaDomeLight.range = 3.4f; dioramaDomeLight.intensity = 0; dioramaDomeLight.shadows = LightShadows.None;
        }

        void TickDioramaSetPieces(float dt)
        {
            if (!dioramaActive) return;
            bool still = fxReducedMotion;
            if (!still) dioramaSetPieceClock += Mathf.Max(0, dt);
            float t = dioramaSetPieceClock;
            foreach (var (bowl, phase) in dioramaBowls)
                if (bowl) bowl.localRotation = still ? Quaternion.identity : Quaternion.Euler(Mathf.Sin(t * .9f + phase) * 2.2f, 0, Mathf.Sin(t * .7f + phase * 1.3f) * 2.8f);
            foreach (var (fish, centre, radius, phase) in dioramaBowlFish)
            {
                if (!fish) continue;
                float a = t * .8f + phase;
                fish.localPosition = centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 1.7f) * .02f, Mathf.Sin(a) * radius);
                fish.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            foreach (var s in dioramaTankSwimmers)
            {
                if (!s.Pivot || !s.Creature) continue;
                float a = t * s.Speed + s.Phase;
                var position = s.Centre + new Vector3(Mathf.Sin(a) * s.Radii.x, Mathf.Sin(a * 2) * s.Radii.y, Mathf.Cos(a) * s.Radii.z);
                var ahead = s.Centre + new Vector3(Mathf.Sin(a + .05f) * s.Radii.x, Mathf.Sin((a + .05f) * 2) * s.Radii.y, Mathf.Cos(a + .05f) * s.Radii.z);
                s.Pivot.localPosition = position;
                var heading = ahead - position; heading.y *= .3f;
                // Kit creatures face local -Z.
                if (heading.sqrMagnitude > 1e-6f) s.Pivot.localRotation = Quaternion.LookRotation(-heading.normalized, Vector3.up);
                s.Creature.SetDioramaFace(.3f, 0, 0);
                if (dioramaCamera) s.Creature.SetLookTarget(dioramaCamera.transform.position);
                s.Creature.AnimateSpecies(t, .5f, still);
            }
            if (dioramaBayLamp) dioramaBayLamp.intensity = .35f + .75f * dioramaShown.LampsOn;
            if (dioramaFeatureTankGlow) dioramaFeatureTankGlow.intensity = 1.6f * dioramaShown.TankGlow;
            if (dioramaFeatureWater) dioramaFeatureWater.SetColor("_EmissionColor", new Color(.05f, .45f, .55f) * dioramaTankGlowLevel);
            bool spark = lastModel != null && lastModel.SparkResearchUnlocked, mist = lastModel != null && lastModel.MistResearchUnlocked;
            foreach (var (mote, centre, radius, speed, phase, isMist) in dioramaDomeMotes)
            {
                if (!mote) continue;
                bool shown = isMist ? mist : spark;
                if (mote.gameObject.activeSelf != shown) mote.gameObject.SetActive(shown);
                if (!shown) continue;
                float a = t * speed + phase;
                mote.localPosition = centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 1.3f + phase) * (isMist ? .12f : .2f), Mathf.Sin(a) * radius);
            }
            if (dioramaDomeLight)
            {
                dioramaDomeLight.intensity = (spark ? .9f : 0) + (mist ? .7f : 0);
                dioramaDomeLight.color = spark && mist ? new Color(.95f, .9f, .75f) : spark ? new Color(1f, .78f, .4f) : new Color(.6f, .95f, .95f);
            }
        }
    }
}
