using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// GR-20: a visitor's wish, a thought bubble floating beside their head and turned to the camera, its trail running down to
    /// the head. In it: the species wished for, as the creature itself; a fish token in the colour wished for with the colour's
    /// sign (a sun, a drop, a sparkle); a token as big as the size wished for, with a pip for each space it takes in a tank (1, 2
    /// or 4); two tokens for a matching pair, three for a school. It pops in as the visitor comes in and stays while they browse
    /// and queue; when the sale grants the wish it pops into a heart that rises and goes, otherwise it shrinks away as they leave.
    /// Without the people kit (the greybox, -evolutionGrowthPlain) it is a pale ball with coloured balls in it. It only shows
    /// the wish: what the visitor does stays the rules' (GrowthShop.WishFor, WishIn).
    /// </summary>
    public sealed class GrowthWishBubble : MonoBehaviour
    {
        public const string Asset = "PK_Wish";
        /// <summary>How far above the head's top the bubble's middle floats, and how far to the viewer's right of it.</summary>
        public const float Above = 0.27f, Aside = 0.16f;
        /// <summary>Seconds to pop in, to shrink away, and for a granted wish's heart.</summary>
        public const float PopIn = 0.25f, ShrinkAway = 0.35f, HeartSeconds = 1.0f;

        GrowthWish wish;
        Transform bubble, content, heart;
        float headTop, scale, heartAge = -1, bob;
        bool kit;
        readonly List<GameObject> tokens = new List<GameObject>();
        static readonly Dictionary<string, Material> plain = new Dictionary<string, Material>();

        /// <summary>Validation: what the bubble shows ("species:pondlet", "colour:Sunny", "size:Large", "pair", "school", or
        /// "heart" once granted), how many tokens and pips, the creature's species in it, and whether it shows at all now.</summary>
        public string Content { get; private set; }
        public int Tokens { get; private set; }
        public int Pips { get; private set; }
        public string Creature { get; private set; }
        public string Sign { get; private set; }
        public bool Shown => bubble != null && bubble.gameObject.activeInHierarchy && scale > 0.01f;
        public bool HeartShown => heart != null && heart.gameObject.activeInHierarchy;
        /// <summary>Validation: the bubble's middle, its scale now, and where the trail's last puff is (by the head).</summary>
        public Vector3 Centre => bubble != null ? bubble.position : transform.position;
        public float Scale => scale;
        public Transform BubbleTransform => bubble;

        /// <summary>A bubble for a wish above a visitor whose head's top is this high (feet at the parent's origin).</summary>
        public static GrowthWishBubble Create(Transform visitor, GrowthWish wish, float headTop, int seed)
        {
            var go = new GameObject("Wish (" + wish.Text + ")");
            go.transform.SetParent(visitor, false);
            var b = go.AddComponent<GrowthWishBubble>();
            b.wish = wish; b.headTop = headTop; b.bob = (seed % 7) * 0.9f;
            b.Build();
            return b;
        }

        static bool KitHas(string part) => GrowthWorld.LookOn && GrowthPeopleKit.Available && GrowthPeopleKit.Data?.wish?.parts != null && GrowthPeopleKit.Data.wish.parts.Contains(part);

        GameObject Part(string part, Transform holder, PrimitiveType fallback, Color colour, Vector3 fallbackScale)
        {
            if (kit)
            {
                var go = GrowthPeopleKit.Prop(Asset, part, holder);
                if (go) return go;
            }
            string key = part + ColorUtility.ToHtmlStringRGB(colour);
            if (!plain.TryGetValue(key, out var m) || m == null) plain[key] = m = ShopWorld.MakeMaterial("GR wish " + part, colour, 0, 0.4f);
            var p = ShopWorld.Shape(holder, part, fallback, Vector3.zero, fallbackScale, m);
            foreach (var c in p.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            return p.gameObject;
        }

        float Front => kit ? GrowthPeopleKit.Data.wish.front : 0.05f;
        float Radius => kit ? GrowthPeopleKit.Data.wish.iconRadius : 0.07f;

        void Build()
        {
            kit = KitHas("wish__bubble");
            bubble = new GameObject("Bubble").transform; bubble.SetParent(transform, false);
            Part("wish__bubble", bubble, PrimitiveType.Sphere, new Color(1f, 0.98f, 0.94f), new Vector3(0.34f, 0.26f, 0.06f));
            content = new GameObject("Content").transform; content.SetParent(bubble, false);
            // The plain look's icons stand in front of its ball; the kit's are built in front of the cloud.
            if (!kit) content.localPosition = new Vector3(0, 0, Front);
            switch (wish.Kind)
            {
                case WishKind.Species:
                {
                    var view = GrowthCreatureView.Create(new GrowthSpecimen { Id = -1, Species = wish.Species, Colour = Colour.Classic }, content);
                    foreach (var c in view.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    view.SetDetail(1);
                    // Three-quarters on, nose to the viewer's left like the tokens and tipped a little to the viewer, so its body
                    // and face read, as long as the bubble's icon room allows.
                    float fit = 2.1f * Radius / Mathf.Max(0.01f, view.Length);
                    view.transform.localScale = Vector3.one * fit;
                    view.transform.localRotation = Quaternion.Euler(-12, 55, 0);
                    view.transform.localPosition = new Vector3(0, -0.012f, kit ? Front : 0.01f);
                    foreach (var r in view.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                    Creature = wish.Species; Content = "species:" + wish.Species;
                    break;
                }
                case WishKind.Colour:
                {
                    Token(wish.Colour.ToString().ToLowerInvariant(), Vector3.zero, 0.92f);
                    string sign = wish.Colour == Colour.Sunny ? "wish__sun" : wish.Colour == Colour.Deep ? "wish__drop" : "wish__sparkle";
                    var at = new GameObject("Sign").transform; at.SetParent(content, false); at.localPosition = new Vector3(-0.058f, 0.045f, 0.004f);
                    Part(sign, at, PrimitiveType.Sphere, ColourOf(wish.Colour), Vector3.one * 0.03f);
                    Sign = sign; Content = "colour:" + wish.Colour;
                    break;
                }
                case WishKind.Size:
                {
                    // The token as big as the size, and a pip for each space it takes in a tank.
                    float s = wish.Size == SizeClass.Small ? 0.5f : wish.Size == SizeClass.Medium ? 0.72f : 0.95f;
                    Token("plain", new Vector3(0, wish.Size == SizeClass.Large ? 0.012f : 0.018f, 0), s);
                    int pips = (int)wish.Size;
                    for (int i = 0; i < pips; i++)
                    {
                        var at = new GameObject("Pip " + i).transform; at.SetParent(content, false);
                        at.localPosition = new Vector3((i - (pips - 1) / 2f) * 0.026f, -0.058f, 0);
                        Part("wish__pip", at, PrimitiveType.Sphere, new Color(1f, 0.48f, 0.48f), Vector3.one * 0.02f);
                    }
                    Pips = pips; Content = "size:" + wish.Size;
                    break;
                }
                case WishKind.Pair:
                    Token("plain", new Vector3(0.042f, 0.014f, 0), 0.6f);
                    Token("plain", new Vector3(-0.042f, -0.014f, 0.006f), 0.6f);
                    Content = "pair";
                    break;
                default:
                    Token("plain", new Vector3(0.046f, 0.022f, 0), 0.5f);
                    Token("plain", new Vector3(-0.046f, 0.022f, 0.004f), 0.5f);
                    Token("plain", new Vector3(0, -0.03f, 0.008f), 0.5f);
                    Content = "school";
                    break;
            }
            heart = new GameObject("Heart").transform; heart.SetParent(transform, false);
            Part("wish__heart", heart, PrimitiveType.Sphere, new Color(1f, 0.44f, 0.57f), Vector3.one * 0.12f);
            heart.gameObject.SetActive(false);
            bubble.localScale = Vector3.zero; scale = 0;
        }

        static Color ColourOf(Colour c) => c == Colour.Sunny ? new Color(1f, 0.76f, 0.24f) : c == Colour.Deep ? new Color(0.24f, 0.42f, 0.85f) : c == Colour.Shimmer ? new Color(0.79f, 0.66f, 1f) : new Color(0.13f, 0.75f, 0.69f);

        void Token(string colour, Vector3 at, float size)
        {
            var holder = new GameObject("Token").transform; holder.SetParent(content, false);
            // The kit's token is built in front of the cloud: a smaller one keeps that front as it shrinks.
            holder.localPosition = at + (kit ? new Vector3(0, 0, Front * (1 - size)) : Vector3.zero); holder.localScale = Vector3.one * size;
            var c = colour == "sunny" ? Colour.Sunny : colour == "deep" ? Colour.Deep : colour == "shimmer" ? Colour.Shimmer : Colour.Classic;
            tokens.Add(Part("wish__fish-" + colour, holder, PrimitiveType.Sphere, ColourOf(c), new Vector3(0.11f, 0.075f, 0.04f)));
            Tokens = tokens.Count;
        }

        /// <summary>
        /// One frame: shown while the visitor browses or queues (popping in), and as they leave, a heart for a granted wish or a
        /// shrink for any other; placed beside the head and turned to the camera; a gentle bob (none with reduced motion).
        /// </summary>
        public void Tick(float dt, bool wanted, bool granted, bool reduced, float clock)
        {
            if (granted && heartAge < 0) heartAge = 0;
            if (heartAge >= 0)
            {
                heartAge += dt;
                float k = Mathf.Clamp01(heartAge / HeartSeconds);
                scale = Mathf.MoveTowards(scale, 0, dt / 0.2f);
                heart.gameObject.SetActive(k < 1);
                float pop = k < 0.2f ? Mathf.Lerp(0.2f, 1.25f, k / 0.2f) : Mathf.Lerp(1.25f, 0.9f, Mathf.Clamp01((k - 0.2f) / 0.3f));
                heart.localScale = Vector3.one * (k > 0.75f ? pop * (1 - (k - 0.75f) / 0.25f) : pop);
                if (k < 1) Content = "heart";
            }
            else scale = Mathf.MoveTowards(scale, wanted ? 1 : 0, dt / (wanted ? PopIn : ShrinkAway));
            bubble.localScale = Vector3.one * Ease(scale);
            bubble.gameObject.SetActive(scale > 0.001f);
            var cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : transform.right;
            float lift = reduced ? 0 : 0.012f * Mathf.Sin(clock * 2.2f + bob);
            var at = transform.position + Vector3.up * (headTop + Above + lift) + Flat(right) * Aside;
            bubble.position = at;
            heart.position = transform.position + Vector3.up * (headTop + Above * 0.6f + (heartAge >= 0 ? Mathf.Clamp01(heartAge / HeartSeconds) * 0.35f : 0));
            if (cam != null)
            {
                Vector3 to = cam.transform.position - at; to.y *= 0.5f;
                if (to.sqrMagnitude > 1e-4f) bubble.rotation = heart.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.right; }

        /// <summary>A little overshoot as it pops in.</summary>
        static float Ease(float t) => t <= 0 ? 0 : t >= 1 ? 1 : 1 + 2.2f * Mathf.Pow(t - 1, 3) + 1.2f * Mathf.Pow(t - 1, 2);
    }
}
