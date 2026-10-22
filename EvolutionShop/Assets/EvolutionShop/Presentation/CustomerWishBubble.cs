using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-E prototype placeholder art: a thought bubble over a visitor with their wish (a coloured family dot and a short
    /// name), riding above the status sign. It brightens gold when the wish is matched, turns grey for a close match,
    /// and goes away once the visitor leaves. Presentation only; the visit owns the wish.
    /// </summary>
    public sealed class CustomerWishBubble : MonoBehaviour
    {
        public const float Lift = .42f;
        CustomerActor actor;
        Transform root;
        TextMesh label;
        Renderer labelRenderer, bubbleRenderer, dotRenderer;
        Material textMaterial, bubbleMaterial, dotMaterial, edgeMaterial;
        Font font;
        Func<bool> reducedMotion;
        Camera view;
        float age;
        static readonly Color Paper = new Color(.98f, .96f, .9f), Matched = new Color(1f, .86f, .42f), Near = new Color(.8f, .8f, .78f);

        public string Text => label ? label.text : "";
        public bool Shown => root && root.gameObject.activeSelf;
        public WishMatch ShownMatch { get; private set; }
        public Vector3 WorldPosition => root ? root.position : Vector3.zero;

        public static CustomerWishBubble Attach(CustomerActor actor, ShopWish wish, Func<bool> reducedMotion)
        {
            if (!actor || wish == null) return null;
            var bubble = actor.gameObject.AddComponent<CustomerWishBubble>();
            bubble.Build(actor, wish, reducedMotion);
            return bubble;
        }

        void Build(CustomerActor owner, ShopWish wish, Func<bool> reduced)
        {
            actor = owner; reducedMotion = reduced; view = Camera.main;
            root = new GameObject("Wish bubble (prototype)").transform;
            root.SetParent(transform, false);
            bubbleMaterial = ShopWorld.MakeMaterial("Wish bubble / paper", Paper, 0, .2f);
            dotMaterial = ShopWorld.MakeMaterial("Wish bubble / family", FamilyColour(wish), 0, .5f);
            var dark = edgeMaterial = ShopWorld.MakeMaterial("Wish bubble / edge", new Color(.16f, .2f, .24f), 0, .2f);
            bubbleRenderer = ShopWorld.Shape(root, "Bubble", PrimitiveType.Sphere, new Vector3(0, 0, .02f), new Vector3(.86f, .34f, .05f), bubbleMaterial).GetComponent<Renderer>();
            ShopWorld.Shape(root, "Bubble rim", PrimitiveType.Sphere, new Vector3(0, 0, .035f), new Vector3(.9f, .38f, .03f), dark);
            ShopWorld.Shape(root, "Thought dot", PrimitiveType.Sphere, new Vector3(-.12f, -.24f, .02f), new Vector3(.09f, .09f, .04f), bubbleMaterial);
            ShopWorld.Shape(root, "Thought dot small", PrimitiveType.Sphere, new Vector3(-.17f, -.33f, .02f), new Vector3(.05f, .05f, .03f), bubbleMaterial);
            dotRenderer = ShopWorld.Shape(root, "Family dot", PrimitiveType.Sphere, new Vector3(-.3f, 0, -.01f), new Vector3(.13f, .13f, .04f), dotMaterial).GetComponent<Renderer>();
            label = new GameObject("Wish").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(.07f, 0, -.012f);
            label.font = font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 80; label.characterSize = .01f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = new Color(.14f, .18f, .22f);
            label.text = wish.Short;
            var shader = Resources.Load<Shader>("EvolutionWorldText");
            if (!shader) throw new InvalidOperationException("Missing Resources/EvolutionWorldText.shader for the wish bubble.");
            textMaterial = new Material(shader) { name = "Wish bubble / text", mainTexture = font.material.mainTexture };
            labelRenderer = label.GetComponent<Renderer>();
            labelRenderer.sharedMaterial = textMaterial;
            labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
            labelRenderer.receiveShadows = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            Font.textureRebuilt += Rebuilt;
            Fit();
            Place();
        }

        static Color FamilyColour(ShopWish wish)
        {
            string key = wish.Key;
            if (key.Contains("Sparkline") || key.Contains("lanternfin")) return new Color(1f, .78f, .25f);
            if (key.Contains("Airfin") || key.Contains("driftwing")) return new Color(.72f, .6f, 1f);
            return new Color(.3f, .78f, .74f);
        }

        /// <summary>Follow the visit: a matched wish glows gold, a close one greys, and the bubble leaves with the visitor.</summary>
        public void Present(CustomerPhase phase, WishMatch match, CustomerOutcome outcome)
        {
            if (!root) return;
            ShownMatch = match;
            bool show = phase != CustomerPhase.Leaving && phase != CustomerPhase.Finished;
            root.gameObject.SetActive(show);
            if (bubbleMaterial) bubbleMaterial.color = match == WishMatch.Exact ? Matched : match == WishMatch.Close ? Near : Paper;
            if (match == WishMatch.Exact && label) label.text = "Wish!";
            Fit();
        }

        void Rebuilt(Font rebuilt)
        {
            if (rebuilt == font && textMaterial && font.material) textMaterial.mainTexture = font.material.mainTexture;
        }

        void Fit()
        {
            if (!labelRenderer || string.IsNullOrEmpty(label.text)) return;
            label.transform.localScale = Vector3.one;
            Vector3 size = labelRenderer.localBounds.size;
            if (size.x > .0001f && size.y > .0001f) label.transform.localScale = Vector3.one * Mathf.Min(.5f / size.x, .15f / size.y);
        }

        void Place()
        {
            if (!root || !actor) return;
            bool still = reducedMotion != null && reducedMotion();
            float bob = still ? 0 : Mathf.Sin(age * 2.2f) * .025f;
            Vector3 at = actor.transform.position;
            root.position = new Vector3(at.x, actor.CaptionHeight + Lift + bob, at.z);
            if (!view) view = Camera.main;
            if (view)
            {
                Vector3 away = root.position - view.transform.position;
                away.y = 0;
                if (away.sqrMagnitude > .001f) root.rotation = Quaternion.LookRotation(away);
            }
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            Place();
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= Rebuilt;
            if (textMaterial) Destroy(textMaterial);
            if (bubbleMaterial) Destroy(bubbleMaterial);
            if (dotMaterial) Destroy(dotMaterial);
            if (edgeMaterial) Destroy(edgeMaterial);
        }
    }
}
