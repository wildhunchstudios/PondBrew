using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>AD-A: the redesigned Pondlet from the diorama kit. The existing swim, growth and hatch code drives its
    /// fin, tail and crest pivots; blinks and faces are blend shapes; the pupils follow the player. Presentation only.</summary>
    public sealed partial class CreatureVisual
    {
        // The kit Pondlet is authored about as long as the classic one; containers then show it larger (AD.4).
        public const float DioramaModelScale = .9f, DioramaTankScale = 1.6f, DioramaIncubatorScale = 1.5f;
        public const float DioramaSwimAmplitude = .82f;
        readonly List<Material> dioramaMaterials = new List<Material>();
        SkinnedMeshRenderer dioramaBody, dioramaLids, dioramaLips;
        Transform dioramaPupils, dioramaLipsPoint;
        Vector3 dioramaPupilRest;
        int bodyPuffed = -1, lidBlink = -1, lidHappy = -1, lipHappy = -1, lipSurprised = -1;
        float faceHappy, faceSurprised, facePuffed, faceBlink;
        Vector3 lookTarget;
        bool lookTargetSet;

        /// <summary>True for the kit Pondlet; false for every classic creature.</summary>
        public bool IsDiorama { get; private set; }
        public Transform DioramaLips => dioramaLipsPoint;

        /// <summary>Extra container scale: the kit Pondlet reads about twice as large in tanks and the incubator.</summary>
        public float ContainerScale(bool incubator) => IsDiorama ? (incubator ? DioramaIncubatorScale : DioramaTankScale) : 1;

        bool BuildDioramaPondlet()
        {
            var model = DioramaLook.InstantiateKit("DioramaPondlet", transform, DioramaLook.Style.Creature, dioramaMaterials);
            if (!model) { Debug.LogWarning("Missing diorama Pondlet; using the classic model."); return false; }
            IsDiorama = true;
            model.localScale = Vector3.one * DioramaModelScale;
            tail = model.Find("Tail pivot"); finLeft = model.Find("Left fin pivot"); finRight = model.Find("Right fin pivot");
            crest = model.Find("Crest pivot");
            dioramaBody = Skinned(model, "Body"); dioramaLids = Skinned(model, "Lids"); dioramaLips = Skinned(model, "Lips");
            dioramaPupils = model.Find("Pupils");
            if (dioramaPupils) dioramaPupilRest = dioramaPupils.localPosition;
            bodyPuffed = Shape(dioramaBody, "Puffed"); lidBlink = Shape(dioramaLids, "Blink"); lidHappy = Shape(dioramaLids, "Happy");
            lipHappy = Shape(dioramaLips, "Happy"); lipSurprised = Shape(dioramaLips, "Surprised");
            // A point just in front of the pouty lips: where its bubble trick starts.
            dioramaLipsPoint = new GameObject("Diorama lips point").transform;
            dioramaLipsPoint.SetParent(model, false);
            dioramaLipsPoint.localPosition = new Vector3(0, -.032f, -.43f);
            CapturePondletRest();
            return true;
        }

        static SkinnedMeshRenderer Skinned(Transform root, string name)
        {
            var t = root.Find(name);
            return t ? t.GetComponent<SkinnedMeshRenderer>() : null;
        }
        static int Shape(SkinnedMeshRenderer renderer, string name)
            => renderer && renderer.sharedMesh ? renderer.sharedMesh.GetBlendShapeIndex(name) : -1;
        static void Weight(SkinnedMeshRenderer renderer, int index, float value)
        {
            if (renderer && index >= 0) renderer.SetBlendShapeWeight(index, Mathf.Clamp01(value) * 100);
        }

        /// <summary>Face for the next pose: each 0..1. The hatch and the bubble trick set it; otherwise it relaxes.</summary>
        public void SetDioramaFace(float happy, float surprised, float puffed, float blink = 0)
        {
            faceHappy = happy; faceSurprised = surprised; facePuffed = puffed; faceBlink = blink;
        }
        public void SetLookTarget(Vector3 world) { lookTarget = world; lookTargetSet = true; }
        public void ClearLookTarget() { lookTargetSet = false; }

        void AnimateDioramaFace(float time, bool reduced)
        {
            float opening = CreatureMotion.Blink(time, pondletIdentityPhase, reduced);
            Weight(dioramaLids, lidBlink, Mathf.Max(1 - opening, faceBlink));
            Weight(dioramaLids, lidHappy, faceHappy * .8f);
            Weight(dioramaLips, lipHappy, faceHappy);
            Weight(dioramaLips, lipSurprised, faceSurprised);
            Weight(dioramaBody, bodyPuffed, facePuffed);
            if (!dioramaPupils) return;
            Vector3 offset = Vector3.zero;
            if (lookTargetSet && !reduced)
            {
                // Pupils slide a little toward the player inside the eye whites.
                Vector3 local = dioramaPupils.parent.InverseTransformPoint(lookTarget) - dioramaPupilRest;
                local.z = 0;
                offset = Vector3.ClampMagnitude(local * .02f, .014f);
            }
            dioramaPupils.localPosition = dioramaPupilRest + offset;
        }

        /// <summary>Validation: a face shape key's current weight (0..100), whichever part carries it.</summary>
        public float DioramaWeightForValidation(string shape)
        {
            foreach (var renderer in new[] { dioramaLips, dioramaLids, dioramaBody })
            {
                int index = Shape(renderer, shape);
                if (index >= 0) return renderer.GetBlendShapeWeight(index);
            }
            return -1;
        }

        void ReleaseDioramaMaterials()
        {
            foreach (var material in dioramaMaterials) if (material) ReleaseCreatureAsset(material);
            dioramaMaterials.Clear();
        }
    }
}
