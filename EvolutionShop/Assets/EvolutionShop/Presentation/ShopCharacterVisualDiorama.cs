using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public enum DioramaVisitorMood { Content, Haggle, Happy, Shocked, Ouch }

    /// <summary>AD-A: the redesigned Casual visitor from the diorama kit. Its hips, shoulders and head pivot carry the
    /// classic joint names, so walking, reactions and comedy poses drive it unchanged; the face is blend shapes.</summary>
    public sealed partial class ShopCharacterVisual
    {
        readonly List<Material> dioramaMaterials = new List<Material>();
        SkinnedMeshRenderer dioramaMouth, dioramaLids;
        Transform dioramaBrowLeft, dioramaBrowRight;
        Quaternion browLeftRest, browRightRest;
        int mouthHappy = -1, mouthHaggle = -1, mouthShocked = -1, mouthOuch = -1, lidsBlink = -1;
        float happyWeight = .35f, haggleWeight, shockedWeight, ouchWeight, faceClock;
        float faceSeed;

        public bool IsDiorama { get; private set; }

        /// <summary>The kit exports every part directly under its root (FBX keeps only that level exactly); these ride on
        /// the head pivot, so they are reparented onto it here.</summary>
        public static readonly string[] DioramaHeadParts =
            { "Head", "Eyes", "Pupils", "Glints", "Lids", "Blush", "Mouth", "Left brow pivot", "Right brow pivot" };

        /// <summary>Builds the kit visitor in place of the classic Casual. Returns false (and builds nothing) if the kit is missing.</summary>
        public bool BuildDioramaCasual()
        {
            if (built) return false;
            var model = DioramaLook.InstantiateKit("DioramaCasualVisitor", transform, DioramaLook.Style.Character, dioramaMaterials);
            if (!model) { Debug.LogWarning("Missing diorama Casual visitor; using the classic one."); return false; }
            // The classic hierarchy has its joints directly under the appearance root (the customer reparents all but the
            // legs into its upper body), so lift the kit's parts out of the model root.
            for (int i = model.childCount - 1; i >= 0; i--) model.GetChild(i).SetParent(transform, false);
            model.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(model.gameObject); else DestroyImmediate(model.gameObject);
            built = true; IsDiorama = true;
            LeftLeg = transform.Find("Left hip"); RightLeg = transform.Find("Right hip");
            LeftArm = transform.Find("Left shoulder"); RightArm = transform.Find("Right shoulder");
            head = transform.Find("Head pivot");
            if (head)
                foreach (var name in DioramaHeadParts)
                {
                    var part = transform.Find(name);
                    if (part) part.SetParent(head, true);
                }
            parcel = new GameObject("Preview parcel").transform;
            parcel.SetParent(transform, false); parcel.localPosition = new Vector3(0, .91f, .38f);
            parcel.gameObject.SetActive(false);
            dioramaMouth = SkinnedPart(head, "Mouth"); dioramaLids = SkinnedPart(head, "Lids");
            dioramaBrowLeft = head ? head.Find("Left brow pivot") : null; dioramaBrowRight = head ? head.Find("Right brow pivot") : null;
            if (dioramaBrowLeft) browLeftRest = dioramaBrowLeft.localRotation;
            if (dioramaBrowRight) browRightRest = dioramaBrowRight.localRotation;
            mouthHappy = BlendIndex(dioramaMouth, "Happy"); mouthHaggle = BlendIndex(dioramaMouth, "Haggle");
            mouthShocked = BlendIndex(dioramaMouth, "Shocked"); mouthOuch = BlendIndex(dioramaMouth, "Ouch");
            lidsBlink = BlendIndex(dioramaLids, "Blink");
            faceSeed = (GetInstanceID() & 1023) * .0137f;
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

        /// <summary>Eases the face toward a mood and blinks on its own clock. Reduced motion skips the blink.</summary>
        public void TickDioramaFace(DioramaVisitorMood mood, float dt, bool reduced)
        {
            if (!IsDiorama) return;
            dt = float.IsNaN(dt) || float.IsInfinity(dt) ? 0 : Mathf.Clamp(dt, 0, .25f);
            float step = dt <= 0 ? 1 : dt * 9;
            happyWeight = Mathf.MoveTowards(happyWeight, mood == DioramaVisitorMood.Happy ? 1 : mood == DioramaVisitorMood.Content ? .35f : 0, step);
            haggleWeight = Mathf.MoveTowards(haggleWeight, mood == DioramaVisitorMood.Haggle ? .85f : 0, step);
            shockedWeight = Mathf.MoveTowards(shockedWeight, mood == DioramaVisitorMood.Shocked ? 1 : 0, step);
            ouchWeight = Mathf.MoveTowards(ouchWeight, mood == DioramaVisitorMood.Ouch ? 1 : 0, step);
            Blend(dioramaMouth, mouthHappy, happyWeight); Blend(dioramaMouth, mouthHaggle, haggleWeight);
            Blend(dioramaMouth, mouthShocked, shockedWeight); Blend(dioramaMouth, mouthOuch, ouchWeight);
            faceClock += dt;
            float cycle = Mathf.Repeat(faceClock + faceSeed * 7, 3.4f);
            float blink = reduced ? 0 : Mathf.Clamp01(1 - Mathf.Abs(cycle - 3.25f) / .09f);
            Blend(dioramaLids, lidsBlink, Mathf.Max(blink, ouchWeight * .7f));
            // Brows: a raised one to haggle, both up when shocked, pinched when slapped.
            float lift = shockedWeight * 14 - ouchWeight * 12;
            if (dioramaBrowLeft) dioramaBrowLeft.localRotation = browLeftRest * Quaternion.Euler(0, 0, -lift - haggleWeight * 10);
            if (dioramaBrowRight) dioramaBrowRight.localRotation = browRightRest * Quaternion.Euler(0, 0, lift - haggleWeight * 2);
            if (dioramaBrowLeft) dioramaBrowLeft.localPosition = new Vector3(dioramaBrowLeft.localPosition.x, BrowRestY(true) + shockedWeight * .025f + haggleWeight * .02f, dioramaBrowLeft.localPosition.z);
            if (dioramaBrowRight) dioramaBrowRight.localPosition = new Vector3(dioramaBrowRight.localPosition.x, BrowRestY(false) + shockedWeight * .025f, dioramaBrowRight.localPosition.z);
        }

        float browLeftY = float.NaN, browRightY = float.NaN;
        float BrowRestY(bool left)
        {
            if (left) { if (float.IsNaN(browLeftY)) browLeftY = dioramaBrowLeft.localPosition.y; return browLeftY; }
            if (float.IsNaN(browRightY)) browRightY = dioramaBrowRight.localPosition.y; return browRightY;
        }

        public float DioramaMouthWeight(DioramaVisitorMood mood)
            => mood == DioramaVisitorMood.Happy ? happyWeight : mood == DioramaVisitorMood.Haggle ? haggleWeight :
               mood == DioramaVisitorMood.Shocked ? shockedWeight : mood == DioramaVisitorMood.Ouch ? ouchWeight : happyWeight;

        void ReleaseDiorama()
        {
            foreach (var material in dioramaMaterials)
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            dioramaMaterials.Clear();
        }
    }
}
