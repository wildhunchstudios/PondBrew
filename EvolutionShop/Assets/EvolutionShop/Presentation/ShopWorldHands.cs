using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Pure presentation curves in tool-body metres/degrees. No gameplay callbacks.</summary>
    public struct FxHandPose
    {
        public Vector3 ToolOffset, ToolAngles, LeftOffset;
        public float RightCurl, LeftCurl, LeftRoll, RightRoll;
        static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3 - 2 * value); }
        static float Pulse(float time, float peak, float end) => time < peak ? Smooth(time / peak) : 1 - Smooth((time - peak) / (end - peak));
        public static FxHandPose Resolve(FxCueId action, float age, bool collecting, float releaseCurl, bool reduced)
        {
            var pose = new FxHandPose();
            if (collecting) pose.RightCurl = 12;
            if (action == FxCueId.HandCollectStart)
            {
                pose.RightCurl = 12 * Smooth(age / .2f);
                if (!reduced) { float p = Pulse(age, .20f, .30f); pose.ToolOffset.y = .004f * p; pose.ToolAngles.z = 2 * p; }
            }
            else if (action == FxCueId.HandCollectEnd) pose.RightCurl = releaseCurl * (1 - Smooth(age / .22f));
            else if (action == FxCueId.CollectedUnit && !reduced) pose.ToolOffset.z = -.008f * Pulse(age, .12f, .28f);
            else if (action == FxCueId.HandDose && !reduced)
            { float p = Pulse(age, .23f, .40f); pose.LeftRoll = -6 * p; pose.LeftOffset.y = .004f * p; }
            else if (action == FxCueId.HandPlace && !reduced)
            { float p = Pulse(age, .22f, .40f); pose.LeftCurl = -10 * p; pose.LeftOffset = new Vector3(0, -.006f, .010f) * p; }
            else if (action == FxCueId.HandUpgrade && !reduced)
            { float p = Pulse(age, .26f, .48f); pose.LeftOffset.x = -.015f * p; pose.LeftRoll = -2 * p; }
            else if (action == FxCueId.HandRefused && !reduced) pose.RightRoll = Pulse(age, .08f, .18f);
            return pose;
        }
    }

    public sealed partial class ShopWorld
    {
        static readonly string[] HandNames = { "RightWrist", "RightThumbBase", "RightThumbTip", "RightIndexBase", "RightIndexTip", "RightFingers", "RightCuff",
            "LeftWrist", "LeftThumbBase", "LeftThumbTip", "LeftIndexBase", "LeftIndexTip", "LeftFingers", "LeftCuff" };
        readonly Transform[] handPivots = new Transform[14];
        readonly Vector3[] handRestPositions = new Vector3[14], handXAxes = new Vector3[14], handZAxes = new Vector3[14];
        readonly Quaternion[] handRestRotations = new Quaternion[14];
        Transform handsVisual;
        bool handsRigValid, handSlap, handPositionKnown;
        bool handPreviewFrozen, handPreviewCollecting;
        Vector3 handPlayerPosition;
        float handTravel, handMovePhase, handMoveWeight, handAge, handDuration, handReleaseCurl;
        int handPriority;
        uint handSequence;
        FxCueId handAction;
        public Transform HandsVisual => handsVisual;
        public bool HandsRigValid => handsRigValid;
        public FxCueId HandAction => handAction;
        public int HandCueCount { get; private set; }

        void BuildHandsVisual()
        {
            if (!toolBody || !AttachAuthored(toolBody, "LivingShopFX", "HandsPair")) return;
            handsVisual = redesignVisuals[redesignVisuals.Count - 1];
            foreach (var renderer in handsVisual.GetComponentsInChildren<Renderer>(true))
            { renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
            var imported = handsVisual.GetComponentsInChildren<Transform>(true);
            handsRigValid = true;
            for (int i = 0; i < HandNames.Length; i++)
            {
                foreach (var pivot in imported) if (pivot.name == HandNames[i]) { handPivots[i] = pivot; break; }
                var t = handPivots[i];
                if (!t) { handsRigValid = false; continue; }
                handRestPositions[i] = t.localPosition; handRestRotations[i] = t.localRotation;
                // FBX axis conversion is authoritative; never assume an empty's imported local X.
                handXAxes[i] = t.parent.InverseTransformDirection(toolBody.right).normalized;
                handZAxes[i] = t.parent.InverseTransformDirection(toolBody.forward).normalized;
            }
            BindHandsOwner(); CancelHandsPose();
        }
        void BindHandsOwner()
        {
            if (handsVisual) BindFxOwner("hands", null, toolBody, true);
            BindFxOwner("hand-command", null, toolBody, true);
        }
        bool HandsVisible => handsVisual && isActiveAndEnabled && fxPolicy == FxViewPolicy.Visible && !handSlap;
        // Input/navigation/slap keep their own authority. Only actual horizontal travel is sampled.
        public void SetHandContext(Vector3 playerPosition, bool canMove, bool slapPlaying)
        {
            Vector3 delta = handPositionKnown ? playerPosition - handPlayerPosition : Vector3.zero;
            handPlayerPosition = playerPosition; handPositionKnown = true; delta.y = 0;
            handTravel = canMove && delta.sqrMagnitude < .25f ? delta.magnitude : 0;
            if (handSlap != slapPlaying) { handSlap = slapPlaying; CancelHandsPose(); }
        }
        void HandCollectEdge(bool next, bool previous)
        {
            if (next == previous || fxPolicy != FxViewPolicy.Visible || !fxEnabled || handSlap) return;
            if (next) handReleaseCurl = 0;
            StartHandCue(next ? FxCueId.HandCollectStart : FxCueId.HandCollectEnd, next ? .30f : .22f, 3);
        }
        bool StartHandCue(FxCueId id, float duration, int priority)
        {
            if (!fxEnabled || fxPolicy != FxViewPolicy.Visible || handSlap || !isActiveAndEnabled || !handsVisual) return false;
            if (handAction != FxCueId.None && handAge < handDuration && priority < handPriority) return false;
            var token = BindFxOwner("hands", null, toolBody, true);
            handSequence++; if (handSequence == 0) { fxCues.Unregister(token); token = BindFxOwner("hands", null, toolBody, true); handSequence = 1; }
            fxCues.Cancel(token);
            if (!fxCues.Play(token, id, handSequence, FxCueOrigin.Manual, duration, priority)) return false;
            handAction = id; handAge = 0; handDuration = duration; handPriority = priority; handPreviewFrozen = false; HandCueCount++; return true;
        }
        bool TryHandCommand(FxCueId id, uint sequence, FxCueOrigin origin)
        {
            if (origin != FxCueOrigin.Manual || fxPolicy != FxViewPolicy.Visible || handSlap || !handsVisual) return false;
            var token = BindFxOwner("hand-command", null, toolBody, true);
            float duration = id == FxCueId.HandUpgrade ? .48f : .40f;
            if (!fxCues.Play(token, id, sequence, origin, duration, 4)) return false;
            AcceptHandPose(id, duration); return true;
        }
        void AcceptHandPose(FxCueId id, float duration)
        {
            if (!fxEnabled || fxPolicy != FxViewPolicy.Visible || handSlap || !isActiveAndEnabled || !handsVisual) return;
            // Collection/refusal/placement already passed the shared deduplication owner.
            // Pose and particles are two render bindings of that single accepted action.
            fxCues.Cancel(fxCues.Find("hands"));
            handAction = id; handAge = 0; handDuration = duration; handPriority = id == FxCueId.HandRefused ? 5 : 4; handPreviewFrozen = false; HandCueCount++;
        }
        void CancelHandsPose()
        {
            handAction = FxCueId.None; handAge = handDuration = handReleaseCurl = handTravel = handMovePhase = handMoveWeight = 0;
            handPriority = 0; handPreviewFrozen = handPreviewCollecting = false;
            if (fxCues != null) fxCues.Cancel(fxCues.Find("hands"));
            RestoreHandRest();
            if (ToolRoot) { ToolRoot.localPosition = new Vector3(.33f, -.16f, .58f); ToolRoot.localRotation = Quaternion.Euler(-5, -7, -5); }
            if (handsVisual) handsVisual.gameObject.SetActive(HandsVisible);
        }
        void RestoreHandRest()
        {
            for (int i = 0; i < handPivots.Length; i++) if (handPivots[i])
            { handPivots[i].localPosition = handRestPositions[i]; handPivots[i].localRotation = handRestRotations[i]; }
        }
        void TickHandsPose(float dt)
        {
            if (!ToolRoot) return;
            if (handsVisual) handsVisual.gameObject.SetActive(HandsVisible);
            if (!HandsVisible || !fxEnabled) { CancelHandsPose(); return; }
            if (handAction != FxCueId.None && !handPreviewFrozen) { handAge += Mathf.Max(0, dt); if (handAge >= handDuration) handAction = FxCueId.None; }
            if (handsRigValid) for (int i = 0; i < handPivots.Length; i++) if (!handPivots[i]) { handsRigValid = false; RestoreHandRest(); break; }
            var pose = FxHandPose.Resolve(handAction, handAge, handPreviewFrozen ? handPreviewCollecting : harvesting, handReleaseCurl, fxReducedMotion);
            bool moving = handTravel > .0001f;
            handMoveWeight = Mathf.MoveTowards(handMoveWeight, moving ? 1 : 0, Mathf.Max(0,dt) / (moving ? .20f : .16f));
            if (!fxReducedMotion && handAction == FxCueId.None && !harvesting && handMoveWeight > 0)
            {
                handMovePhase += handTravel * 22;
                pose.ToolOffset.y = Mathf.Sin(handMovePhase) * .006f * handMoveWeight;
                pose.ToolAngles.z = Mathf.Sin(handMovePhase * .5f) * 1.5f * handMoveWeight;
            }
            var rotation = Quaternion.Euler(-5, -7, -5);
            ToolRoot.localPosition = new Vector3(.33f, -.16f, .58f) + rotation * pose.ToolOffset * .62f;
            ToolRoot.localRotation = rotation * Quaternion.Euler(pose.ToolAngles);
            if (toolNozzle) toolNozzle.localRotation = Quaternion.identity;
            if (!handsRigValid) return; // Imported rest remains usable if any rigid pivot is absent.
            RestoreHandRest();
            SetHandRotation(0, pose.RightRoll, false); SetHandRotation(7, pose.LeftRoll, false);
            SetHandRotation(3, pose.RightCurl, true); SetHandRotation(4, pose.RightCurl * .5f, true);
            SetHandRotation(10, pose.LeftCurl, true); SetHandRotation(11, pose.LeftCurl * .5f, true);
            var left = handPivots[7];
            left.localPosition += left.parent.InverseTransformVector(toolBody.TransformVector(pose.LeftOffset));
            fxCues.SetState(fxCues.Find("hands"), harvesting ? FxSteadyState.Working : FxSteadyState.Empty);
        }
        void SetHandRotation(int index, float angle, bool curl)
        {
            var t = handPivots[index];
            t.localRotation = Quaternion.AngleAxis(angle, curl ? handXAxes[index] : handZAxes[index]) * handRestRotations[index];
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PreviewHandPoseForValidation(FxCueId id, float age, bool collecting, bool reduced)
        {
            fxReducedMotion = reduced; handPreviewCollecting = collecting; handAction = id; handDuration = 1; handAge = age; handPreviewFrozen = true; TickHandsPose(0);
        }
#endif
    }
}
