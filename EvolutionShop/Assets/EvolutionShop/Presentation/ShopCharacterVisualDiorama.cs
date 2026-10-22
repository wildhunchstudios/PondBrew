using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>The kit characters' reactions (face shape keys, lids and brows). Customers: Content, Haggle (studying a price),
    /// Happy, Shocked (refusing) and Ouch (slapped); crew: Content, Focus (working), Happy (a job done), Shocked (caught out)
    /// and Greet.</summary>
    public enum DioramaVisitorMood { Content, Haggle, Happy, Shocked, Ouch, Focus, Greet }

    /// <summary>AD-A/AD-C: the diorama kit's characters. Their hips, shoulders and head pivot carry the classic joint names, so
    /// walking, reactions, comedy and work poses drive them unchanged; the face is blend shapes. Every role (the four customer
    /// types and the three crew) is built from the same humanoid rig; with the look switched, crew and study figures rebuild
    /// in place (RebuildForLook).</summary>
    public sealed partial class ShopCharacterVisual
    {
        readonly List<Material> dioramaMaterials = new List<Material>();
        SkinnedMeshRenderer dioramaMouth, dioramaLids;
        Transform dioramaBrowLeft, dioramaBrowRight;
        Quaternion browLeftRest, browRightRest;
        int mouthHappy = -1, mouthHaggle = -1, mouthShocked = -1, mouthOuch = -1, lidsBlink = -1;
        float happyWeight = .35f, haggleWeight, shockedWeight, ouchWeight, greetWeight, focusWeight, faceClock;
        float faceSeed;

        public bool IsDiorama { get; private set; }
        /// <summary>The kit role built, or null for a classic figure.</summary>
        public DioramaRole DioramaRole { get; private set; }

        /// <summary>The kit exports every part directly under its root (FBX keeps only that level exactly); these ride on
        /// the head pivot, so they are reparented onto it here.</summary>
        public static readonly string[] DioramaHeadParts =
            { "Head", "Eyes", "Pupils", "Glints", "Lids", "Blush", "Mouth", "Left brow pivot", "Right brow pivot" };

        /// <summary>Builds the kit visitor in place of the classic Casual. Returns false (and builds nothing) if the kit is missing.</summary>
        public bool BuildDioramaCasual() => BuildDioramaRole("casual");

        /// <summary>Builds a kit role in place of the classic figure. Returns false (and builds nothing) if the role or kit is missing.</summary>
        public bool BuildDioramaRole(string roleId)
        {
            if (built || !DioramaRoles.TryGet(roleId, out var role)) return false;
            var before = new HashSet<Transform>(); foreach (Transform child in transform) before.Add(child);
            var model = DioramaLook.InstantiateKit(role.Kit, transform, DioramaLook.Style.Character, dioramaMaterials);
            if (!model) { Debug.LogWarning("Missing diorama character " + role.Kit + "; using the classic one."); ReleaseDiorama(); return false; }
            // The classic hierarchy has its joints directly under the appearance root (the customer reparents all but the
            // legs into its upper body), so lift the kit's parts out of the model root.
            for (int i = model.childCount - 1; i >= 0; i--) model.GetChild(i).SetParent(transform, false);
            model.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(model.gameObject); else DestroyImmediate(model.gameObject);
            built = true; IsDiorama = true; DioramaRole = role; builtRole = roleId;
            LeftLeg = transform.Find("Left hip"); RightLeg = transform.Find("Right hip");
            LeftArm = transform.Find("Left shoulder"); RightArm = transform.Find("Right shoulder");
            head = transform.Find("Head pivot");
            if (head)
                foreach (var name in DioramaHeadParts)
                {
                    var part = transform.Find(name);
                    if (part) part.SetParent(head, true);
                }
            // The carry socket (the classic preview parcel): crew hang their crate on it, shown only while carrying.
            parcel = new GameObject("Preview parcel").transform;
            parcel.SetParent(transform, false); parcel.localPosition = new Vector3(0, .91f, .38f) * role.CarryScale;
            var crate = transform.Find("Carry crate");
            if (crate) { crate.SetParent(parcel, false); crate.localPosition = Vector3.zero; crate.localRotation = Quaternion.identity; }
            parcel.gameObject.SetActive(false);
            dioramaMouth = SkinnedPart(head, "Mouth"); dioramaLids = SkinnedPart(head, "Lids");
            dioramaBrowLeft = head ? head.Find("Left brow pivot") : null; dioramaBrowRight = head ? head.Find("Right brow pivot") : null;
            if (dioramaBrowLeft) browLeftRest = dioramaBrowLeft.localRotation;
            if (dioramaBrowRight) browRightRest = dioramaBrowRight.localRotation;
            browLeftY = browRightY = float.NaN;
            mouthHappy = BlendIndex(dioramaMouth, "Happy"); mouthHaggle = BlendIndex(dioramaMouth, "Haggle");
            mouthShocked = BlendIndex(dioramaMouth, "Shocked"); mouthOuch = BlendIndex(dioramaMouth, "Ouch");
            lidsBlink = BlendIndex(dioramaLids, "Blink");
            faceSeed = (GetInstanceID() & 1023) * .0137f;
            RecordOwnedParts(before);
            Pose(CharacterSamplePose.Idle, 0);
            TickDioramaFace(DioramaVisitorMood.Content, 0, false);
            return true;
        }

        static SkinnedMeshRenderer SkinnedPart(Transform root, string name)
        {
            var t = root ? root.Find(name) : null;
            return t ? t.GetComponent<SkinnedMeshRenderer>() : null;
        }
        static int BlendIndex(SkinnedMeshRenderer renderer, string name)
            => renderer && renderer.sharedMesh ? renderer.sharedMesh.GetBlendShapeIndex(name) : -1;
        static void Blend(SkinnedMeshRenderer renderer, int index, float weight)
        {
            if (renderer && index >= 0) renderer.SetBlendShapeWeight(index, Mathf.Clamp01(weight) * 100);
        }

        /// <summary>Eases the face toward a mood and blinks on its own clock. Reduced motion skips the blink. Each role leans
        /// into its reactions its own way: how far it squints and lifts one brow while haggling or concentrating.</summary>
        public void TickDioramaFace(DioramaVisitorMood mood, float dt, bool reduced)
        {
            if (!IsDiorama) return;
            dt = float.IsNaN(dt) || float.IsInfinity(dt) ? 0 : Mathf.Clamp(dt, 0, .25f);
            float step = dt <= 0 ? 1 : dt * 9;
            bool pleased = mood == DioramaVisitorMood.Happy || mood == DioramaVisitorMood.Greet;
            happyWeight = Mathf.MoveTowards(happyWeight, pleased ? 1 : mood == DioramaVisitorMood.Content ? .35f : 0, step);
            haggleWeight = Mathf.MoveTowards(haggleWeight, mood == DioramaVisitorMood.Haggle ? .85f : mood == DioramaVisitorMood.Focus ? .55f : 0, step);
            shockedWeight = Mathf.MoveTowards(shockedWeight, mood == DioramaVisitorMood.Shocked ? 1 : 0, step);
            ouchWeight = Mathf.MoveTowards(ouchWeight, mood == DioramaVisitorMood.Ouch ? 1 : 0, step);
            greetWeight = Mathf.MoveTowards(greetWeight, mood == DioramaVisitorMood.Greet ? 1 : 0, step);
            focusWeight = Mathf.MoveTowards(focusWeight, mood == DioramaVisitorMood.Focus ? 1 : 0, step);
            Blend(dioramaMouth, mouthHappy, happyWeight); Blend(dioramaMouth, mouthHaggle, haggleWeight);
            Blend(dioramaMouth, mouthShocked, shockedWeight); Blend(dioramaMouth, mouthOuch, ouchWeight);
            faceClock += dt;
            float cycle = Mathf.Repeat(faceClock + faceSeed * 7, 3.4f);
            float blink = reduced ? 0 : Mathf.Clamp01(1 - Mathf.Abs(cycle - 3.25f) / .09f);
            var role = DioramaRole;
            float squint = role == null ? 0 : role.Squint * Mathf.Max(haggleWeight / .85f, focusWeight);
            Blend(dioramaLids, lidsBlink, Mathf.Max(blink, Mathf.Max(ouchWeight * .7f, squint)));
            // Brows: one raised to haggle (each role its own way), both up when shocked or greeting, pinched when slapped or
            // concentrating.
            float tilt = role == null ? 10 : role.BrowTilt;
            float lift = shockedWeight * 14 + greetWeight * 9 - ouchWeight * 12 - focusWeight * 7;
            if (dioramaBrowLeft) dioramaBrowLeft.localRotation = browLeftRest * Quaternion.Euler(0, 0, -lift - haggleWeight * tilt);
            if (dioramaBrowRight) dioramaBrowRight.localRotation = browRightRest * Quaternion.Euler(0, 0, lift - haggleWeight * 2);
            float raise = shockedWeight * .025f + greetWeight * .02f - focusWeight * .008f;
            if (dioramaBrowLeft) dioramaBrowLeft.localPosition = new Vector3(dioramaBrowLeft.localPosition.x, BrowRestY(true) + raise + haggleWeight * .02f, dioramaBrowLeft.localPosition.z);
            if (dioramaBrowRight) dioramaBrowRight.localPosition = new Vector3(dioramaBrowRight.localPosition.x, BrowRestY(false) + raise, dioramaBrowRight.localPosition.z);
        }

        float browLeftY = float.NaN, browRightY = float.NaN;
        float BrowRestY(bool left)
        {
            if (left) { if (float.IsNaN(browLeftY)) browLeftY = dioramaBrowLeft.localPosition.y; return browLeftY; }
            if (float.IsNaN(browRightY)) browRightY = dioramaBrowRight.localPosition.y; return browRightY;
        }

        public float DioramaMouthWeight(DioramaVisitorMood mood)
            => mood == DioramaVisitorMood.Happy || mood == DioramaVisitorMood.Greet ? happyWeight :
               mood == DioramaVisitorMood.Haggle || mood == DioramaVisitorMood.Focus ? haggleWeight :
               mood == DioramaVisitorMood.Shocked ? shockedWeight : mood == DioramaVisitorMood.Ouch ? ouchWeight : happyWeight;
        /// <summary>How far the lids are closed now (a blink, a squint or a wince).</summary>
        public float DioramaLidWeight => dioramaLids && lidsBlink >= 0 ? dioramaLids.GetBlendShapeWeight(lidsBlink) / 100 : 0;
        /// <summary>How far the left brow is lifted from rest (degrees; positive up), for validation and the study.</summary>
        public float DioramaBrowLift => dioramaBrowLeft ? -Mathf.DeltaAngle(0, (Quaternion.Inverse(browLeftRest) * dioramaBrowLeft.localRotation).eulerAngles.z) : 0;

        // ---------------------------------------------------------------- crew reactions
        float crewGreetUntil = -1, crewCelebrateUntil = -1;
        DioramaVisitorMood crewMood;
        /// <summary>The crew member's current reaction: caught out (a refused job), pleased (a job done, a celebration),
        /// greeting, concentrating while working, or content.</summary>
        public DioramaVisitorMood CrewMood => crewMood;
        DioramaVisitorMood ResolveCrewMood()
        {
            float now = Time.time;
            if (workerEvent == FxWorkerEvent.Refused) return DioramaVisitorMood.Shocked;
            if (workerEvent == FxWorkerEvent.Place || workerEvent == FxWorkerEvent.Checkout || now < crewCelebrateUntil) return DioramaVisitorMood.Happy;
            // Pip greets the customer, then concentrates while ringing the sale up.
            if (now < crewGreetUntil && !(workerActivity == FxWorkerActivity.Checkout && workerProgress > .45f)) return DioramaVisitorMood.Greet;
            switch (workerActivity)
            {
                case FxWorkerActivity.Checkout: case FxWorkerActivity.Pickup: case FxWorkerActivity.Measure:
                case FxWorkerActivity.Dose: case FxWorkerActivity.Start: case FxWorkerActivity.Check: case FxWorkerActivity.Place:
                    return DioramaVisitorMood.Focus;
            }
            return DioramaVisitorMood.Content;
        }
        void TickCrewFace(float dt)
        {
            if (!IsDiorama || DioramaRole == null || !DioramaRole.Crew) return;
            crewMood = ResolveCrewMood();
            TickDioramaFace(crewMood, dt, workerWorld && workerWorld.WorkerReducedMotion);
        }

        // ---------------------------------------------------------------- switching the look in place
        string builtRole;
        bool builtEmployee;
        readonly List<Transform> ownedParts = new List<Transform>();
        void RecordOwnedParts(HashSet<Transform> before)
        {
            ownedParts.Clear();
            foreach (Transform child in transform) if (!before.Contains(child)) ownedParts.Add(child);
        }

        /// <summary>Builds the role's kit figure while the diorama look is on, else the classic one (employee or customer).</summary>
        public void BuildForRole(string roleId, bool employee)
        {
            if (built) return;
            builtRole = roleId; builtEmployee = employee;
            if (DioramaLook.Active && BuildDioramaRole(roleId)) return;
            Build(employee);
            builtRole = roleId;
        }

        /// <summary>After the look switches: rebuilds this figure as the kit or the classic one, in place, keeping its owner's
        /// own children (signs, name labels), its worker binding and its place. Returns true when it rebuilt.</summary>
        public bool RebuildForLook()
        {
            if (!built || builtRole == null) return false;
            bool wantKit = DioramaLook.Active && DioramaRoles.TryGet(builtRole, out _);
            if (wantKit == IsDiorama) return false;
            string role = builtRole; bool employee = builtEmployee;
            // Destroy waits for the end of the frame in play, so the old parts leave the figure first: the new figure finds its
            // joints by name, and the classic and kit figures share them.
            foreach (var part in ownedParts)
                if (part) { part.gameObject.SetActive(false); part.SetParent(null, false); if (Application.isPlaying) Destroy(part.gameObject); else DestroyImmediate(part.gameObject); }
            ownedParts.Clear();
            ReleaseDiorama();
            foreach (var material in materials) if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            materials.Clear();
            built = false; IsDiorama = false; DioramaRole = null;
            LeftLeg = RightLeg = LeftArm = RightArm = null; head = parcel = null;
            dioramaMouth = dioramaLids = null; dioramaBrowLeft = dioramaBrowRight = null;
            mouthHappy = mouthHaggle = mouthShocked = mouthOuch = lidsBlink = -1;
            leftWorkerPoints = rightWorkerPoints = null;
            // The worker's resting leg places belong to the old figure, so the new one builds as a plain figure (its own
            // idle pose leaves the legs where its kit put them) and then measures its own.
            string worker = workerId; workerId = null;
            BuildForRole(role, employee);
            workerId = worker;
            if (workerId != null && built)
            {
                CacheWorkerLegs();
                ApplyWorkerPose(FxWorkerMotion.Sample(workerActivity, workerProgress, workerGait, 0, workerTime, FxWorkerEvent.None, 0, workerCarrying, true));
            }
            return true;
        }

        void ReleaseDiorama()
        {
            ReleaseDioramaHat();
            foreach (var material in dioramaMaterials)
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            dioramaMaterials.Clear();
        }
    }
}
