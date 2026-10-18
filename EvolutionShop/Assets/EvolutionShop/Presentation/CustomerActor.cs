using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>Navigation and presentation for one visitor. Never owns stock, payment, or visit decisions.</summary>
    public sealed partial class CustomerActor : MonoBehaviour
    {
        public bool PathFailed => pathFailed;
        public string LastError { get; private set; } = string.Empty;
        public CustomerPhase Phase { get; private set; }
        public string StatusLabel => caption ? caption.text : string.Empty;
        /// <summary>Which customer-type look was built: casual, bargain, collector or premium.</summary>
        public string PresentationId { get; private set; } = "casual";
        /// <summary>Sign height above the visitor's feet before any lift.</summary>
        public float CaptionBaseHeight => captionBase;
        /// <summary>World height of the caption sign, including the current lift.</summary>
        public float CaptionHeight => captionRoot ? captionRoot.position.y : 0f;
        public bool IsSlapReacting => slapRemaining > 0f;
        public float SlapReactionRemaining => slapRemaining;

        /// <summary>Raises the sign so it does not cover a nearby visitor's sign. Eases toward the target.</summary>
        public void SetCaptionLift(float lift) { captionLiftTarget = Mathf.Max(0f, lift); }
        public bool Arrived => initialized && !pathFailed && hasDestination && agent && agent.isOnNavMesh &&
            !agent.pathPending && Vector3.Distance(transform.position, destination) <= agent.stoppingDistance + .07f;

        ShopCustomerNavigation navigation;
        NavMeshAgent agent;
        NavMeshPath path;
        readonly List<Material> ownedMaterials = new List<Material>();
        Transform body, leftLeg, rightLeg, leftArm, rightArm, captionRoot;
        Transform slapStars;
        readonly Transform[] slapStarNodes = new Transform[3];
        Mesh slapStarMesh;
        TextMesh caption;
        Renderer captionRenderer;
        Font font;
        Material fontMaterial;
        Camera playerCamera;
        Vector3 destination, lastSafePosition;
        Vector3 bodyBaseScale = Vector3.one;
        bool initialized, hasDestination, pathFailed, paused, hasPresentation;
        bool reducedSlap;
        float animationTime, captionBase = 2.03f, captionLift, captionLiftTarget;
        Transform upperBody;
        ShopWorld customerWorld;
        string customerVisitId;
        FxOwnerToken customerFxOwner;
        Vector3 motionPosition, leftLegRest, rightLegRest;
        Quaternion motionRotation;
        Vector3[] leftSolePoints, rightSolePoints;
        float leftFloor, rightFloor, gaitPhase, travelDistance, measuredSpeed, movingBlend, turnBlend, reactionAge;
        int reactionCount, motionSamples; bool checkoutPresented;
        FxCustomerReaction customerReaction;
        public FxCustomerSnapshot MotionSnapshot => new FxCustomerSnapshot(gaitPhase,travelDistance,measuredSpeed,movingBlend,
            reactionAge,turnBlend,reactionCount,motionSamples,customerReaction,paused,customerWorld && customerWorld.CustomerReducedMotion);
        public FxOwnerToken CustomerFxOwner => customerFxOwner;
        public void BindCustomerPresentation(ShopWorld world,string visitId)
        { customerWorld=world; customerVisitId=visitId; customerFxOwner=world?world.BindCustomerPresentation(this,visitId):default; }
        public bool PlayCustomerReaction(FxCustomerReaction reaction)
        {
            if(!initialized || !isActiveAndEnabled || paused || IsSlapReacting ||
                reaction<=FxCustomerReaction.None || reaction>FxCustomerReaction.Checkout || (reaction==FxCustomerReaction.Checkout && checkoutPresented))return false;
            customerReaction=reaction; reactionAge=0; reactionCount++; if(reaction==FxCustomerReaction.Checkout)checkoutPresented=true; return true;
        }
        public void CancelCustomerPresentation()
        {
            customerReaction=FxCustomerReaction.None; reactionAge=0;
            if(upperBody)upperBody.localRotation=Quaternion.identity;
            if(leftArm)leftArm.localRotation=Quaternion.Euler(0,0,-7);
            if(rightArm)rightArm.localRotation=Quaternion.Euler(0,0,7);
        }
        float slapRemaining, slapDuration;

        void OnEnable()
        {
            Font.textureRebuilt += FontAtlasRebuilt;
            motionPosition=transform.position; motionRotation=transform.rotation;
            if(customerWorld && !string.IsNullOrEmpty(customerVisitId))customerFxOwner=customerWorld.BindCustomerPresentation(this,customerVisitId);
            if (agent && agent.isOnNavMesh) agent.isStopped = paused || pathFailed;
        }
        void OnDisable()
        {
            Font.textureRebuilt -= FontAtlasRebuilt;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
            ClearSlapReaction();
            CancelCustomerPresentation();
            if(customerWorld)customerWorld.ReleaseCustomerPresentation(customerFxOwner);
        }

        public void Initialize(ShopCustomerNavigation nav, string presentationId = "casual")
        {
            if (initialized) return;
            navigation = nav;
            PresentationId = presentationId == "bargain" || presentationId == "collector" || presentationId == "premium" ? presentationId : "casual";
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
            BuildCustomerJoints();
            motionPosition=transform.position; motionRotation=transform.rotation;
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
            motionPosition=transform.position; motionRotation=transform.rotation;
            if(paused) {
                CancelCustomerPresentation(); movingBlend=measuredSpeed=turnBlend=0;
                ApplyCustomerPose(FxCustomerMotion.Sample(PresentationId,Phase,gaitPhase,0,animationTime,FxCustomerReaction.None,0,0,true));
            }
            if (!agent || !agent.isOnNavMesh) return;
            agent.isStopped = paused || pathFailed;
            // isStopped retains residual crowd velocity while braking. Pause must stop
            // immediately, without clearing the path needed when play resumes.
            if (paused) agent.velocity = Vector3.zero;
        }

        /// <summary>Local presentation only; the caller owns targeting, cooldown and visit cancellation.</summary>
        public bool PlaySlapReaction(bool reduced = false)
        {
            if (!initialized || !isActiveAndEnabled || paused || !body || Phase == CustomerPhase.Finished || IsSlapReacting)
                return false;
            CancelCustomerPresentation();
            if(customerWorld)customerWorld.CancelFxOwner(customerFxOwner);
            reduced = reduced || customerWorld && customerWorld.CustomerReducedMotion; reducedSlap = reduced;
            slapDuration = FxComedyMotion.SlapDuration(reduced);
            slapRemaining = slapDuration;
            if (slapStars) slapStars.gameObject.SetActive(false);
            ApplyBodyPose(body.localPosition.y, 0f);
            return true;
        }

        void ClearSlapReaction()
        {
            slapRemaining = 0f;
            if(upperBody)upperBody.localRotation=Quaternion.identity;
            if (slapStars) slapStars.gameObject.SetActive(false);
            if (!body) return;
            body.localScale = bodyBaseScale;
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
        }

        void ApplyBodyPose(float lift, float happyTilt)
        {
            body.localScale=bodyBaseScale;
            // The displacement-driven legs keep their floor compensation during the recoil.
            body.localPosition=Vector3.up*lift;body.localRotation=Quaternion.Euler(0,0,happyTilt);
            if(!IsSlapReacting)return;
            float elapsed=slapDuration-slapRemaining;
            var pose=FxComedyMotion.Sample(ComedyKind.Slap,elapsed,reducedSlap,0);
            if(upperBody)upperBody.localRotation=Quaternion.Euler(pose.UpperEuler+pose.HeadEuler*.35f);
            bool stars=!reducedSlap && elapsed>=FxComedyMotion.SlapContactSeconds(false);
            if(slapStars)slapStars.gameObject.SetActive(stars);
            if(!stars)return;
            float age=elapsed-FxComedyMotion.SlapContactSeconds(false);
            for(int i=0;i<slapStarNodes.Length;i++){
                float angle=age*5.5f+i*Mathf.PI*2f/slapStarNodes.Length;
                var star=slapStarNodes[i];
                star.localPosition=new Vector3(Mathf.Cos(angle)*.27f,Mathf.Sin(angle*2)*.025f,Mathf.Sin(angle)*.20f);
                star.localRotation=Quaternion.Euler(0,age*65+i*25,age*110+i*65);
                star.localScale=Vector3.one*Mathf.Min(1,pose.Envelope*4);
            }
        }
        public void Present(CustomerPhase phase, string label, bool upset = false)
        {
            if (!hasPresentation || Phase != phase) animationTime = 0;
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
            ClearSlapReaction();
            CancelCustomerPresentation();
            if(customerWorld)customerWorld.CancelFxOwner(customerFxOwner);
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

            float dt=Time.deltaTime;
            Vector3 delta=transform.position-motionPosition; delta.y=0;
            float distance=delta.magnitude;
            // Actual NavMesh displacement advances the gait. Ignore discontinuities and
            // retained crowd velocity when the actor itself did not move.
            bool valid=dt>0 && dt<=.25f && distance<=Mathf.Max(.08f,agent.speed*dt*2.5f);
            measuredSpeed=valid?distance/dt:0;
            bool moving=measuredSpeed>.015f;
            if(moving) {
                travelDistance+=distance;
                gaitPhase+=distance*(Mathf.PI*2f/FxCustomerMotion.StrideLength(PresentationId));
            }
            movingBlend=moving?Mathf.MoveTowards(movingBlend,Mathf.Clamp01(measuredSpeed/.6f),dt*9f):0;
            float turn=valid?Mathf.DeltaAngle(motionRotation.eulerAngles.y,transform.eulerAngles.y)/Mathf.Max(.001f,dt)/180f:0;
            turnBlend=Mathf.MoveTowards(turnBlend,Mathf.Clamp(turn,-1,1),Mathf.Max(0,dt)*5f);
            motionPosition=transform.position; motionRotation=transform.rotation;
            animationTime+=Mathf.Max(0,dt); motionSamples++;
            if(customerReaction!=FxCustomerReaction.None) {
                reactionAge+=Mathf.Max(0,dt);
                if(reactionAge>=FxCustomerMotion.ReactionDuration(customerReaction)){customerReaction=FxCustomerReaction.None;reactionAge=0;}
            }
            bool reduced=customerWorld && customerWorld.CustomerReducedMotion;
            bool quiet=customerWorld && customerWorld.FxPolicy!=FxViewPolicy.Visible;
            ApplyCustomerPose(FxCustomerMotion.Sample(PresentationId,Phase,gaitPhase,movingBlend,animationTime,
                customerReaction,reactionAge,turnBlend,reduced||(quiet&&customerReaction==FxCustomerReaction.None)));
            if (IsSlapReacting)
            {
                slapRemaining = Mathf.Max(0f, slapRemaining - dt);
                if (!IsSlapReacting && slapStars) slapStars.gameObject.SetActive(false);
            }
            ApplyBodyPose(0, 0);
        }

        void BuildCustomerJoints()
        {
            // Reparent existing art once; shared employee/sample pose APIs remain intact.
            upperBody=Group("Customer upper body",body,new Vector3(0,.76f,0));
            for(int i=body.childCount-1;i>=0;i--) {
                var child=body.GetChild(i);
                if(child!=upperBody && child!=leftLeg && child!=rightLeg && child!=slapStars)child.SetParent(upperBody,true);
            }
            leftLegRest=leftLeg.localPosition; rightLegRest=rightLeg.localPosition;
            leftSolePoints=CacheLegVertices(leftLeg); rightSolePoints=CacheLegVertices(rightLeg);
            leftFloor=Mathf.Max(0,LegMinimum(leftSolePoints,0)+leftLegRest.y); rightFloor=Mathf.Max(0,LegMinimum(rightSolePoints,0)+rightLegRest.y);
            ApplyCustomerPose(FxCustomerMotion.Sample(PresentationId,Phase,0,0,0,FxCustomerReaction.None,0,0,true));
        }
        static Vector3[] CacheLegVertices(Transform leg)
        {
            var points=new List<Vector3>();
            foreach(var filter in leg.GetComponentsInChildren<MeshFilter>()) {
                if(!filter.sharedMesh)continue;
                var matrix=leg.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(var vertex in filter.sharedMesh.vertices)points.Add(matrix.MultiplyPoint3x4(vertex));
            }
            return points.ToArray();
        }
        static float LegMinimum(Vector3[] points,float angle)
        {
            if(points==null||points.Length==0)return 0;
            float radians=angle*Mathf.Deg2Rad,sin=Mathf.Sin(radians),cos=Mathf.Cos(radians),low=float.MaxValue;
            for(int i=0;i<points.Length;i++)low=Mathf.Min(low,points[i].y*cos-points[i].z*sin);
            return low;
        }
        void ApplyCustomerPose(FxCustomerPose pose)
        {
            if(!upperBody)return;
            leftLeg.localRotation=Quaternion.Euler(pose.LeftLegEuler); rightLeg.localRotation=Quaternion.Euler(pose.RightLegEuler);
            leftLeg.localPosition=leftLegRest+Vector3.up*(leftFloor-leftLegRest.y-LegMinimum(leftSolePoints,pose.LeftLegEuler.x));
            rightLeg.localPosition=rightLegRest+Vector3.up*(rightFloor-rightLegRest.y-LegMinimum(rightSolePoints,pose.RightLegEuler.x));
            leftArm.localRotation=Quaternion.Euler(pose.LeftArmEuler); rightArm.localRotation=Quaternion.Euler(pose.RightArmEuler);
            upperBody.localRotation=Quaternion.Euler(pose.UpperEuler);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PreviewMotionForValidation(CustomerPhase phase,float gaitPhase,float moving,float idleTime,
            FxCustomerReaction reaction,float reactionAge,float turn,bool reduced)
        { ApplyCustomerPose(FxCustomerMotion.Sample(PresentationId,phase,gaitPhase,moving,idleTime,reaction,reactionAge,turn,reduced)); }
#endif

        void LateUpdate()
        {
            if (!captionRoot) return;
            captionLift = Mathf.MoveTowards(captionLift, captionLiftTarget, Time.unscaledDeltaTime * 2f);
            captionRoot.localPosition = new Vector3(0, captionBase + captionLift, 0);
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
            // Each customer type differs by palette and, most visibly from across the shop, by hat silhouette.
            Color coatColor, skinColor, trouserColor, hatColor;
            string look = PresentationId;
            switch (look)
            {
                case "bargain": coatColor = new Color(.36f, .58f, .30f); skinColor = new Color(.85f, .62f, .47f); trouserColor = new Color(.32f, .24f, .16f); hatColor = new Color(.86f, .74f, .30f); break;
                case "collector": coatColor = new Color(.48f, .27f, .58f); skinColor = new Color(.45f, .28f, .18f); trouserColor = new Color(.12f, .10f, .18f); hatColor = new Color(.10f, .08f, .12f); break;
                case "premium": coatColor = new Color(.93f, .90f, .82f); skinColor = new Color(.75f, .52f, .38f); trouserColor = new Color(.10f, .10f, .12f); hatColor = new Color(.03f, .03f, .04f); break;
                default: coatColor = new Color(.83f, .52f, .18f); skinColor = new Color(.66f, .37f, .23f); trouserColor = new Color(.055f, .16f, .19f); hatColor = trouserColor; break;
            }
            var coat = Material("Visitor / " + look + " coat", coatColor);
            var skin = Material("Visitor / " + look + " skin", skinColor);
            var dark = Material("Visitor / " + look + " trousers", trouserColor);
            var hat = Material("Visitor / " + look + " hat", hatColor, .5f);
            var cream = Material("Visitor / cream collar", new Color(.95f, .91f, .74f));
            var gold = Material("Visitor / gold trim", new Color(1f, .74f, .26f), .7f);
            var eyes = Material("Visitor / dark eyes", new Color(.025f, .048f, .049f), .65f);
            if (look == "casual")
            {
                body = Group("Visitor appearance", transform, Vector3.zero);
                var sample = body.gameObject.AddComponent<ShopCharacterVisual>(); sample.Build(false);
                leftLeg=sample.LeftLeg; rightLeg=sample.RightLeg; leftArm=sample.LeftArm; rightArm=sample.RightArm;
                bodyBaseScale=body.localScale;
            }
            else
            {
            body = Group("Visitor appearance", transform, Vector3.zero);
            if (look == "premium") body.localScale = Vector3.one * 1.07f;
            bodyBaseScale = body.localScale;
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
            switch (look)
            {
                case "bargain":
                    ShopWorld.Shape(body, "Brimmed cap", PrimitiveType.Sphere, new Vector3(0, 1.69f, -.01f), new Vector3(.41f, .17f, .39f), hat);
                    ShopWorld.Shape(body, "Cap brim", PrimitiveType.Cube, new Vector3(0, 1.655f, .2f), new Vector3(.32f, .03f, .2f), hat);
                    break;
                case "collector":
                    ShopWorld.Shape(body, "Bowler crown", PrimitiveType.Sphere, new Vector3(0, 1.73f, -.01f), new Vector3(.36f, .25f, .34f), hat);
                    ShopWorld.Shape(body, "Bowler brim", PrimitiveType.Cylinder, new Vector3(0, 1.645f, -.01f), new Vector3(.52f, .012f, .5f), hat);
                    for (int side = -1; side <= 1; side += 2)
                        ShopWorld.Shape(body, "Round spectacle", PrimitiveType.Cylinder, new Vector3(side * .082f, 1.515f, .21f), new Vector3(.1f, .006f, .1f), gold).localRotation = Quaternion.Euler(90, 0, 0);
                    ShopWorld.Shape(body, "Spectacle bridge", PrimitiveType.Cube, new Vector3(0, 1.52f, .215f), new Vector3(.07f, .012f, .012f), gold);
                    break;
                case "premium":
                    ShopWorld.Shape(body, "Top hat", PrimitiveType.Cylinder, new Vector3(0, 1.87f, -.01f), new Vector3(.3f, .2f, .3f), hat);
                    ShopWorld.Shape(body, "Top hat brim", PrimitiveType.Cylinder, new Vector3(0, 1.675f, -.01f), new Vector3(.48f, .012f, .48f), hat);
                    ShopWorld.Shape(body, "Top hat band", PrimitiveType.Cylinder, new Vector3(0, 1.72f, -.01f), new Vector3(.305f, .03f, .305f), gold);
                    break;
                default:
                    ShopWorld.Shape(body, "Soft beret", PrimitiveType.Sphere, new Vector3(-.018f, 1.7f, -.015f), new Vector3(.46f, .17f, .40f), dark);
                    break;
            }
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
                ShopWorld.Shape(body, "Cardigan button", PrimitiveType.Sphere, new Vector3(0, 1.20f - i * .13f, .173f), Vector3.one * .034f, look == "premium" ? gold : cream);

            }
            BuildSlapStars(gold, look == "premium" ? 2.03f : 1.82f);

            captionBase = look == "premium" ? 2.36f : 2.03f;
            captionRoot = Group("Visitor status sign", transform, new Vector3(0, captionBase, 0));
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

        void BuildSlapStars(Material material, float height)
        {
            slapStars = Group("Cartoon slap stars", body, new Vector3(0, height, 0));
            // One tiny original extruded star mesh is shared by this visitor's three stars.
            var vertices = new Vector3[22];
            var triangles = new int[120];
            vertices[0] = new Vector3(0, 0, -.009f);
            vertices[11] = new Vector3(0, 0, .009f);
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * .5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? .065f : .029f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -.009f);
                vertices[i + 12] = new Vector3(vertices[i + 1].x, vertices[i + 1].y, .009f);
                int next = (i + 1) % 10;
                int index = i * 12;
                triangles[index] = 0; triangles[index + 1] = next + 1; triangles[index + 2] = i + 1;
                triangles[index + 3] = 11; triangles[index + 4] = i + 12; triangles[index + 5] = next + 12;
                triangles[index + 6] = i + 1; triangles[index + 7] = next + 1; triangles[index + 8] = next + 12;
                triangles[index + 9] = i + 1; triangles[index + 10] = next + 12; triangles[index + 11] = i + 12;
            }
            slapStarMesh = new Mesh { name = "Original cartoon slap star", vertices = vertices, triangles = triangles };
            slapStarMesh.RecalculateNormals();
            slapStarMesh.RecalculateBounds();
            for (int i = 0; i < slapStarNodes.Length; i++)
            {
                var star = slapStarNodes[i] = Group("Slap star " + (i + 1), slapStars, Vector3.zero);
                star.gameObject.AddComponent<MeshFilter>().sharedMesh = slapStarMesh;
                var renderer = star.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            slapStars.gameObject.SetActive(false);
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
            if (slapStarMesh) Destroy(slapStarMesh);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
        }
    }
}
