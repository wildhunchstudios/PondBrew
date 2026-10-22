using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D (AD.5) reactions of a kit visitor: a little hop as it takes its bagged creature at checkout, and after a slap,
    /// cartoon birds circling with the stars while its hat spins. Presentation only, on the visitor's own clock; reduced
    /// motion keeps all three still (no hop, birds or spin), as it keeps the stars.
    /// </summary>
    public sealed partial class CustomerActor
    {
        public const float DioramaHopStart = .62f, DioramaHopEnd = .98f, DioramaHopHeight = .13f, DioramaBirdSeconds = .64f;
        readonly Transform[] dioramaBirds = new Transform[2];
        readonly Transform[] dioramaBirdWings = new Transform[4];

        /// <summary>How high the visitor hops this frame (0 outside the checkout hop).</summary>
        public float DioramaHopLift { get; private set; }
        /// <summary>Whether the slap birds are circling now.</summary>
        public bool DioramaBirdsShown => dioramaBirds[0] && dioramaBirds[0].gameObject.activeInHierarchy;

        /// <summary>The checkout hop: one hop as the bag reaches the visitor's hand.</summary>
        public static float DioramaHop(float reactionAge, bool reduced)
        {
            if (reduced || reactionAge <= DioramaHopStart || reactionAge >= DioramaHopEnd) return 0;
            return DioramaHopHeight * Mathf.Sin(Mathf.PI * (reactionAge - DioramaHopStart) / (DioramaHopEnd - DioramaHopStart));
        }

        float DioramaHopNow(bool reduced)
        {
            DioramaHopLift = dioramaVisitor && customerReaction == FxCustomerReaction.Checkout ? DioramaHop(reactionAge, reduced) : 0;
            return DioramaHopLift;
        }

        void BuildDioramaBirds()
        {
            if (!slapStars) return;
            var feather = DioramaLook.Toon("slap bird", new Color(1f, .9f, .52f), DioramaLook.Style.Character); ownedMaterials.Add(feather);
            var beak = DioramaLook.Toon("slap bird beak", new Color(.96f, .52f, .3f), DioramaLook.Style.Character); ownedMaterials.Add(beak);
            var eye = DioramaLook.Toon("slap bird eye", new Color(.1f, .14f, .16f), DioramaLook.Style.Character); ownedMaterials.Add(eye);
            for (int i = 0; i < dioramaBirds.Length; i++)
            {
                var bird = dioramaBirds[i] = Group("Cartoon slap bird " + (i + 1), slapStars, Vector3.zero);
                Part(bird, "Bird body", new Vector3(0, 0, 0), new Vector3(.07f, .06f, .09f), feather);
                Part(bird, "Bird head", new Vector3(0, .035f, .045f), Vector3.one * .055f, feather);
                Part(bird, "Bird beak", new Vector3(0, .03f, .08f), new Vector3(.022f, .016f, .035f), beak);
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(bird, "Bird eye", new Vector3(s * .018f, .045f, .066f), Vector3.one * .012f, eye);
                    var wing = dioramaBirdWings[i * 2 + (s + 1) / 2] = Group("Bird wing", bird, new Vector3(s * .03f, .01f, 0));
                    Part(wing, "Bird wing feathers", new Vector3(s * .04f, 0, 0), new Vector3(.08f, .014f, .05f), feather);
                }
                bird.gameObject.SetActive(false);
            }
        }

        static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = ShopWorld.Shape(parent, name, PrimitiveType.Sphere, position, scale, material);
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        /// <summary>Two birds circle the other way round from the stars, a little wider, flapping, for their brief
        /// moment after the contact (the slap's own window, .65 s); age below zero hides them.</summary>
        void PoseDioramaBirds(float age)
        {
            bool show = age >= 0 && age < DioramaBirdSeconds;
            for (int i = 0; i < dioramaBirds.Length; i++)
            {
                var bird = dioramaBirds[i];
                if (!bird) continue;
                if (bird.gameObject.activeSelf != show) bird.gameObject.SetActive(show);
                if (!show) continue;
                float angle = -age * 4.2f + i * Mathf.PI;
                bird.localPosition = new Vector3(Mathf.Cos(angle) * .34f, .06f + Mathf.Sin(age * 9 + i) * .02f, Mathf.Sin(angle) * .26f);
                // Each bird faces along its circle (the kit faces +Z).
                bird.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle)), Vector3.up);
                float grow = Mathf.Clamp01(age * 6) * Mathf.Clamp01((DioramaBirdSeconds - age) * 5);
                bird.localScale = Vector3.one * grow;
                float flap = Mathf.Sin(age * 28 + i * 1.7f) * 38;
                for (int s = 0; s < 2; s++)
                {
                    var wing = dioramaBirdWings[i * 2 + s];
                    if (wing) wing.localRotation = Quaternion.Euler(0, 0, (s == 0 ? -1 : 1) * flap);
                }
            }
        }
    }
}
