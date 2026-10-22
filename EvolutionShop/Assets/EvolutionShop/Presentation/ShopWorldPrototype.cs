using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-E prototype placeholder art (AD.6): the shop's 1-5 star sign on the store side of the partition, a theme tag
    /// over each themed display, and the bag that fills on the counter during the hands-on checkout. Kept under a root of
    /// its own beside the world, so the room look and the world's checks never count it; built on first use.
    /// </summary>
    public sealed partial class ShopWorld
    {
        Transform prototypeRoot, starSign, checkoutBag;
        readonly List<Renderer> starFills = new List<Renderer>();
        TextMesh starNext;
        readonly List<(Transform root, TextMesh text, Renderer back)> themeTags = new List<(Transform, TextMesh, Renderer)>();
        readonly List<Material> prototypeMaterials = new List<Material>();
        Material starOn, starOff, tagMatching, tagPlain;
        Mesh starMesh;
        int starsShown = -1, nextShown = -2;
        Camera prototypeView;

        public bool PrototypeSignShown => starSign && starSign.gameObject.activeSelf;
        public int PrototypeStarsShown => PrototypeSignShown ? starsShown : 0;
        public string PrototypeSignNextText => starNext ? starNext.text : "";
        public int ThemeTagsShown { get { int n = 0; foreach (var t in themeTags) if (t.root && t.root.gameObject.activeSelf) n++; return n; } }
        public IEnumerable<string> ThemeTagTexts { get { foreach (var t in themeTags) if (t.root && t.root.gameObject.activeSelf) yield return t.text.text; } }
        public float CheckoutBagFill => checkoutBag && checkoutBag.gameObject.activeSelf ? checkoutBag.localScale.y / .36f : 0;
        public Transform StarSign => starSign;

        Transform PrototypeRoot()
        {
            if (prototypeRoot) return prototypeRoot;
            prototypeRoot = new GameObject("Gameplay prototype placeholders (AD-E)").transform;
            prototypeRoot.SetPositionAndRotation(transform.position, transform.rotation);
            starOn = PrototypeMaterial("Prototype star / earned", new Color(1f, .8f, .28f));
            starOff = PrototypeMaterial("Prototype star / to earn", new Color(.32f, .36f, .4f));
            tagMatching = PrototypeMaterial("Prototype theme tag / matching", new Color(.18f, .5f, .44f));
            tagPlain = PrototypeMaterial("Prototype theme tag / theme", new Color(.24f, .28f, .34f));
            return prototypeRoot;
        }

        Material PrototypeMaterial(string name, Color color)
        {
            var material = MakeMaterial(name, color, 0, .35f);
            prototypeMaterials.Add(material);
            return material;
        }

        /// <summary>The star sign: earned stars gold, the rest grey, and what the next star needs and unlocks.</summary>
        public void SetPrototypeSign(bool shown, int stars, int nextThreshold, string nextUnlock)
        {
            if (!shown) { if (starSign) starSign.gameObject.SetActive(false); return; }
            if (!starSign) BuildStarSign();
            starSign.gameObject.SetActive(true);
            if (stars == starsShown && nextThreshold == nextShown) return;
            starsShown = stars; nextShown = nextThreshold;
            for (int i = 0; i < starFills.Count; i++) starFills[i].sharedMaterial = i < stars ? starOn : starOff;
            starNext.text = nextThreshold < 0 ? "FIVE STARS: " + nextUnlock.ToUpperInvariant() : "NEXT STAR AT " + nextThreshold + " REP: " + nextUnlock.ToUpperInvariant();
        }

        void BuildStarSign()
        {
            var root = PrototypeRoot();
            // Hung from the ceiling in front of the partition's wall dressing, over the counter side of the store.
            starSign = Group("Shop star sign (prototype)", root, new Vector3(-4f, 2.78f, -.7f));
            var dark = PrototypeMaterial("Prototype star sign / board", new Color(.1f, .14f, .18f));
            var trim = PrototypeMaterial("Prototype star sign / trim", new Color(.92f, .72f, .36f));
            foreach (float side in new[] { -.9f, .9f })
                Shape(starSign, "Hanging rod", PrimitiveType.Cylinder, new Vector3(side, .95f, .02f), new Vector3(.025f, .55f, .025f), trim);
            Shape(starSign, "Board", PrimitiveType.Cube, new Vector3(0, 0, .02f), new Vector3(2.3f, .78f, .04f), dark);
            Shape(starSign, "Trim", PrimitiveType.Cube, new Vector3(0, 0, .03f), new Vector3(2.38f, .86f, .02f), trim);
            starMesh = StarMesh();
            for (int i = 0; i < ShopStars.Max; i++)
            {
                var star = new GameObject("Star " + (i + 1));
                star.transform.SetParent(starSign, false);
                star.transform.localPosition = new Vector3(-.76f + i * .38f, .12f, -.012f);
                star.transform.localScale = Vector3.one * .32f;
                star.AddComponent<MeshFilter>().sharedMesh = starMesh;
                var renderer = star.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = starOff;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                starFills.Add(renderer);
            }
            starNext = Text(starSign, "NEXT STAR", new Vector3(0, -.24f, -.014f), .05f, new Color(.95f, .92f, .82f), 2.1f);
        }

        static Mesh StarMesh()
        {
            var vertices = new Vector3[11];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2 + i * Mathf.PI / 5, radius = i % 2 == 0 ? .5f : .21f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            }
            // One face, wound to face -Z: the store side, where the sign is read.
            var triangles = new int[30];
            for (int i = 0; i < 10; i++)
            {
                int a = i + 1, b = (i + 1) % 10 + 1;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = b; triangles[i * 3 + 2] = a;
            }
            var mesh = new Mesh { name = "Prototype star", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A tag over each themed display, saying its theme and whether its resident matches.</summary>
        public void SetThemeTags(IReadOnlyList<PrototypeThemeTag> tags, Camera view)
        {
            prototypeView = view ? view : prototypeView;
            int count = tags == null ? 0 : tags.Count;
            if (count > 0) PrototypeRoot();
            for (int i = 0; i < count; i++)
            {
                if (i >= themeTags.Count)
                {
                    var root = Group("Theme tag (prototype)", prototypeRoot, Vector3.zero);
                    var back = Shape(root, "Tag board", PrimitiveType.Cube, new Vector3(0, 0, .012f), new Vector3(1.16f, .22f, .02f), tagPlain).GetComponent<Renderer>();
                    var label = Text(root, "THEME", new Vector3(0, 0, -.004f), .055f, new Color(.96f, .94f, .86f), 1.08f);
                    themeTags.Add((root, label, back));
                }
                var tag = tags[i];
                var entry = themeTags[i];
                entry.root.gameObject.SetActive(true);
                entry.root.localPosition = new Vector3(tag.X, 2.08f, tag.Z);
                string name = ShopThemes.Name(tag.Theme).ToUpperInvariant();
                string text = tag.Matching ? name + " THEME: " + ShopThemes.Family(tag.Theme).ToUpperInvariant() + " +" + ShopThemes.TolerancePoints + "%" : name + " THEME (" + ShopThemes.Family(tag.Theme).ToUpperInvariant() + ")";
                if (entry.text.text != text) entry.text.text = text;
                entry.back.sharedMaterial = tag.Matching ? tagMatching : tagPlain;
            }
            for (int i = count; i < themeTags.Count; i++) if (themeTags[i].root) themeTags[i].root.gameObject.SetActive(false);
            FaceThemeTags();
        }

        void FaceThemeTags()
        {
            if (themeTags.Count == 0) return;
            if (!prototypeView) prototypeView = Camera.main;
            if (!prototypeView) return;
            foreach (var tag in themeTags)
            {
                if (!tag.root || !tag.root.gameObject.activeSelf) continue;
                Vector3 away = tag.root.position - prototypeView.transform.position;
                away.y = 0;
                if (away.sqrMagnitude > .001f) tag.root.rotation = Quaternion.LookRotation(away);
            }
        }

        /// <summary>The hands-on checkout's bag, filling on the counter while the mouse is held (gently, with reduced motion).</summary>
        public void PresentCheckoutHold(float progress, Vector3 counter, bool reduced)
        {
            FaceThemeTags();
            if (progress <= 0) { if (checkoutBag) checkoutBag.gameObject.SetActive(false); return; }
            if (!checkoutBag)
            {
                var bagMaterial = PrototypeMaterial("Prototype checkout bag", new Color(.72f, .9f, 1f, 1f));
                checkoutBag = Shape(PrototypeRoot(), "Checkout bag (prototype)", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .1f, bagMaterial);
                checkoutBag.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            checkoutBag.gameObject.SetActive(true);
            float wobble = reduced ? 0 : Mathf.Sin(progress * 30f) * .015f;
            checkoutBag.position = counter + new Vector3(0, 1.12f + .18f * progress, 0);
            checkoutBag.localScale = new Vector3(.2f + .08f * progress + wobble, .36f * progress, .2f + .08f * progress - wobble);
        }

        void DisposePrototype()
        {
            foreach (var material in prototypeMaterials) if (material) Destroy(material);
            prototypeMaterials.Clear();
            if (starMesh) Destroy(starMesh);
            if (prototypeRoot) Destroy(prototypeRoot.gameObject);
        }
    }
}
