using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>Navigation and presentation for one visitor. Never owns stock, payment, or visit decisions.</summary>
    public sealed class CustomerActor : MonoBehaviour
    {
        public bool PathFailed => pathFailed;
        public string LastError { get; private set; } = string.Empty;
        public CustomerPhase Phase { get; private set; }
        public string StatusLabel => caption ? caption.text : string.Empty;
        public bool Arrived => initialized && !pathFailed && hasDestination && agent && agent.isOnNavMesh &&
            !agent.pathPending && Vector3.Distance(transform.position, destination) <= agent.stoppingDistance + .07f;

        ShopCustomerNavigation navigation;
        NavMeshAgent agent;
        NavMeshPath path;
        readonly List<Material> ownedMaterials = new List<Material>();
        Transform body, leftLeg, rightLeg, leftArm, rightArm, captionRoot;
        TextMesh caption;
        Renderer captionRenderer;
        Font font;
        Material fontMaterial;
        Camera playerCamera;
        Vector3 destination, lastSafePosition;
        bool initialized, hasDestination, pathFailed, paused, hasPresentation;
        float animationTime, paidTime;

        void OnEnable()
        {
            Font.textureRebuilt += FontAtlasRebuilt;
            if (agent && agent.isOnNavMesh) agent.isStopped = paused || pathFailed;
        }
        void OnDisable()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
        }

        public void Initialize(ShopCustomerNavigation nav)
        {
            if (initialized) return;
            navigation = nav;
            if (!navigation || !navigation.Ready || !navigation.TryResolveDestination(navigation.EntryPoint, out var entry))
            {
                Fail("Customer entry has no store navigation.");
                return;
            }
            // Explicit runtime initialization keeps native allocation out of MonoBehaviour construction.
            path = new NavMeshPath();
            transform.position = entry;
            lastSafePosition = entry;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.agentTypeID = navigation.AgentTypeId;
            agent.speed = 1.8f;
            agent.acceleration = 8;
            agent.angularSpeed = 360;
            agent.radius = .28f;
            agent.height = 1.8f;
            agent.stoppingDistance = .14f;
            agent.autoBraking = true;
            agent.autoRepath = false;
            agent.areaMask = NavMesh.AllAreas;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            if (!agent.Warp(entry)) { Fail("Customer could not enter the store navigation surface."); return; }
            BuildAppearance();
            playerCamera = Camera.main;
            initialized = true;
            Present(CustomerPhase.Arriving, "WELCOME IN");
            SetPaused(paused);
        }

        public bool MoveTo(Vector3 target)
        {
            if (!initialized || !navigation || !navigation.Ready || !agent || !agent.isOnNavMesh)
            {
                Fail("Customer navigation is unavailable.");
                return false;
            }
            if (!navigation.TryResolveDestination(target, out var resolved) ||
                !navigation.TryGetPath(transform.position, resolved, path))
            {
                Fail("Customer destination has no complete path within the store.");
                return false;
            }
            if (!agent.SetPath(path)) { Fail("Customer path could not be assigned."); return false; }
            destination = resolved;
            hasDestination = true;
            pathFailed = false;
            LastError = string.Empty;
            agent.isStopped = paused;
            return true;
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (agent && agent.isOnNavMesh) agent.isStopped = paused || pathFailed;
        }

        public void Present(CustomerPhase phase, string label, bool upset = false)
        {
            if ((!hasPresentation || Phase != phase) && phase == CustomerPhase.Paid) paidTime = 0;
            Phase = phase;
            hasPresentation = true;
            if (!caption) return;
            caption.text = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
            // Refusals read in coral; the word in the caption carries the meaning, not the color alone.
            caption.color = upset ? new Color(1f, .55f, .47f) : phase == CustomerPhase.Paid ? new Color(1f, .84f, .44f) : new Color(.73f, 1f, .88f);
            FitCaption();
        }

        void Fail(string reason)
        {
            pathFailed = true;
            hasDestination = false;
            LastError = reason;
            if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
        }

        void Update()
        {
            if (!initialized || paused) return;
            if (!navigation || !navigation.Ready || !agent || !agent.isOnNavMesh)
            {
                Fail("Customer lost its navigation surface.");
                return;
            }
            // The bake excludes the lab. This separate guard also contains unexpected displacement.
            if (!navigation.IsInStore(transform.position))
            {
                agent.Warp(lastSafePosition);
                Fail("Customer left the permitted store area.");
                return;
            }
            lastSafePosition = transform.position;
            if (hasDestination && !Arrived && !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
                Fail("Customer path became incomplete.");

            float dt = Time.deltaTime;
            float walking = Mathf.Clamp01(agent.velocity.magnitude / agent.speed);
            animationTime += dt * Mathf.Lerp(1.4f, 8f, walking);
            float stride = Mathf.Sin(animationTime) * walking;
            leftLeg.localRotation = Quaternion.Euler(stride * 23, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-stride * 23, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-stride * 18, 0, -7);
            rightArm.localRotation = Quaternion.Euler(stride * 18, 0, 7);
            float lift = Mathf.Abs(Mathf.Sin(animationTime)) * walking * .025f;
            float happyTilt = 0;
            if (Phase == CustomerPhase.Paid)
            {
                paidTime += dt;
                lift += Mathf.Max(0, Mathf.Sin(paidTime * 7)) * .055f * Mathf.Clamp01(1 - paidTime / 1.5f);
                happyTilt = Mathf.Sin(paidTime * 6) * 5 * Mathf.Clamp01(1 - paidTime / 1.5f);
            }
            body.localPosition = Vector3.up * lift;
            body.localRotation = Quaternion.Euler(0, 0, happyTilt);
        }

        void LateUpdate()
        {
            if (!captionRoot) return;
            // TextMesh faces local -Z. Keep the sign upright with its front toward the player.
            if (playerCamera)
            {
                Vector3 away = captionRoot.position - playerCamera.transform.position;
                away.y = 0;
                if (away.sqrMagnitude > .001f) captionRoot.rotation = Quaternion.LookRotation(away);
            }
            FitCaption();
            FontAtlasRebuilt(font);
        }

        Material Material(string label, Color color, float smoothness = .4f)
        {
            var result = ShopWorld.MakeMaterial(label, color, 0, smoothness);
            ownedMaterials.Add(result);
            return result;
        }

        static Transform Group(string name, Transform parent, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        void BuildAppearance()
        {
            // Smooth original placeholder art. No physical NPC collider: player pass-through is deliberate.
            var coat = Material("Visitor / ochre cardigan", new Color(.83f, .52f, .18f));
            var skin = Material("Visitor / warm clay", new Color(.66f, .37f, .23f));
            var dark = Material("Visitor / teal trousers", new Color(.055f, .16f, .19f));
            var cream = Material("Visitor / cream collar", new Color(.95f, .91f, .74f));
            var eyes = Material("Visitor / dark eyes", new Color(.025f, .048f, .049f), .65f);
            body = Group("Visitor appearance", transform, Vector3.zero);
            leftLeg = Group("Left walking leg", body, new Vector3(-.115f, .7f, 0));
            rightLeg = Group("Right walking leg", body, new Vector3(.115f, .7f, 0));
            foreach (var leg in new[] { leftLeg, rightLeg })
            {
                ShopWorld.Shape(leg, "Trouser leg", PrimitiveType.Capsule, new Vector3(0, -.27f, 0), new Vector3(.17f, .27f, .19f), dark);
                ShopWorld.Shape(leg, "Rounded shoe", PrimitiveType.Sphere, new Vector3(0, -.61f, .07f), new Vector3(.21f, .15f, .34f), dark);
            }
            ShopWorld.Shape(body, "Soft cardigan", PrimitiveType.Capsule, new Vector3(0, 1.02f, 0), new Vector3(.47f, .35f, .33f), coat);
            ShopWorld.Shape(body, "Collar", PrimitiveType.Sphere, new Vector3(0, 1.30f, .035f), new Vector3(.31f, .115f, .3f), cream);
            ShopWorld.Shape(body, "Head", PrimitiveType.Sphere, new Vector3(0, 1.49f, .015f), new Vector3(.4f, .43f, .38f), skin);
            ShopWorld.Shape(body, "Soft beret", PrimitiveType.Sphere, new Vector3(-.018f, 1.7f, -.015f), new Vector3(.46f, .17f, .40f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                ShopWorld.Shape(body, "Eye", PrimitiveType.Sphere, new Vector3(side * .082f, 1.515f, .194f), new Vector3(.046f, .058f, .022f), eyes);
                ShopWorld.Shape(body, "Eye glint", PrimitiveType.Sphere, new Vector3(side * .082f - .007f, 1.53f, .205f), Vector3.one * .012f, cream);
                ShopWorld.Shape(body, "Ear", PrimitiveType.Sphere, new Vector3(side * .204f, 1.48f, .015f), new Vector3(.075f, .13f, .09f), skin);
                var arm = Group(side < 0 ? "Left swinging arm" : "Right swinging arm", body, new Vector3(side * .26f, 1.23f, 0));
                ShopWorld.Shape(arm, "Cardigan sleeve", PrimitiveType.Capsule, new Vector3(0, -.19f, 0), new Vector3(.16f, .20f, .19f), coat);
                ShopWorld.Shape(arm, "Hand", PrimitiveType.Sphere, new Vector3(0, -.42f, .018f), new Vector3(.15f, .17f, .15f), skin);
                if (side < 0) leftArm = arm; else rightArm = arm;
            }
            ShopWorld.Shape(body, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.47f, .21f), new Vector3(.075f, .085f, .09f), skin);
            ShopWorld.Shape(body, "Small smile", PrimitiveType.Sphere, new Vector3(0, 1.393f, .187f), new Vector3(.085f, .022f, .02f), eyes);
            for (int i = 0; i < 3; i++)
                ShopWorld.Shape(body, "Cardigan button", PrimitiveType.Sphere, new Vector3(0, 1.20f - i * .13f, .173f), Vector3.one * .034f, cream);

            captionRoot = Group("Visitor status sign", transform, new Vector3(0, 2.03f, 0));
            ShopWorld.Shape(captionRoot, "Status backing", PrimitiveType.Cube, new Vector3(0, 0, .014f), new Vector3(1.58f, .245f, .025f), dark);
            caption = Group("Depth-tested visitor caption", captionRoot, new Vector3(0, 0, -.006f)).gameObject.AddComponent<TextMesh>();
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = caption.font;
            caption.fontSize = 80;
            caption.characterSize = .01f;
            caption.anchor = TextAnchor.MiddleCenter;
            caption.alignment = TextAlignment.Center;
            var shader = Resources.Load<Shader>("EvolutionWorldText");
            if (!shader) throw new System.InvalidOperationException("Missing Resources/EvolutionWorldText.shader for the visitor caption.");
            fontMaterial = new Material(shader) { name = "Visitor / depth-tested caption" };
            ownedMaterials.Add(fontMaterial);
            FontAtlasRebuilt(font);
            captionRenderer = caption.GetComponent<Renderer>();
            captionRenderer.sharedMaterial = fontMaterial;
            captionRenderer.shadowCastingMode = ShadowCastingMode.Off;
            captionRenderer.receiveShadows = false;
        }

        void FontAtlasRebuilt(Font rebuilt)
        {
            if (font && rebuilt == font && font.material && fontMaterial)
                fontMaterial.mainTexture = font.material.mainTexture;
        }

        void FitCaption()
        {
            if (!captionRenderer || string.IsNullOrEmpty(caption.text)) return;
            Vector3 size = captionRenderer.localBounds.size;
            if (size.x > .0001f && size.y > .0001f)
                caption.transform.localScale = Vector3.one * Mathf.Min(1.42f / size.x, .14f / size.y);
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            foreach (var material in ownedMaterials) if (material) Destroy(material);
        }
    }
}
