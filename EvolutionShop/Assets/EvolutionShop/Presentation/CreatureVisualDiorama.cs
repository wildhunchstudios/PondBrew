using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>AD-A/AD-B: a creature from the diorama kit. The existing swim, growth and hatch code drives its fin, tail and crest
    /// pivots (each family shares them); blinks and faces are blend shapes; the pupils follow the player; the Sparkline's lantern
    /// glows; the colour variant comes from the atlas. Presentation only.</summary>
    public sealed partial class CreatureVisual
    {
        // The Pondlet's sizes (AD-A); every species has its own in DioramaSpecies, about twice the old on-screen size (AD.4).
        public const float DioramaModelScale = .9f, DioramaTankScale = 1.6f, DioramaIncubatorScale = 1.5f;
        public const float DioramaSwimAmplitude = .82f;
        readonly List<Material> dioramaMaterials = new List<Material>();
        readonly Dictionary<string, Material> dioramaSlots = new Dictionary<string, Material>();
        SkinnedMeshRenderer dioramaBody, dioramaLids, dioramaLips;
        Transform dioramaPupils, dioramaLipsPoint, dioramaLantern, dioramaLanternPoint;
        Material dioramaGlow;
        Color dioramaGlowBase;
        Vector3 dioramaPupilRest;
        Quaternion dioramaLanternRest;
        DioramaSpecies dioramaSpecies;
        int bodyPuffed = -1, lidBlink = -1, lidHappy = -1, lipHappy = -1, lipSurprised = -1;
        float faceHappy = .2f, faceSurprised, facePuffed, faceBlink, actionGlow, actionTail, actionFins;
        Vector3 lookTarget;
        bool lookTargetSet;

        /// <summary>True for a kit creature; false for every classic one.</summary>
        public bool IsDiorama { get; private set; }
        public DioramaSpecies DioramaSpecies => dioramaSpecies;
        public DioramaVariant DioramaVariant { get; private set; }
        public Transform DioramaLips => dioramaLipsPoint;
        /// <summary>The lantern's bulb (Sparkline), where its flash sparkles; null for the other families.</summary>
        public Transform DioramaLanternPoint => dioramaLanternPoint;
        public Material DioramaGlowMaterial => dioramaGlow;

        /// <summary>Extra container scale: a kit creature reads about twice as large in display tanks and the incubator.</summary>
        public float ContainerScale(bool incubator) => IsDiorama ? (incubator ? dioramaSpecies.IncubatorScale : dioramaSpecies.TankScale) : 1;
        /// <summary>How much of its swim path a kit creature uses in the large tanks, so the larger model stays in the glass.</summary>
        public float DioramaSwim => IsDiorama ? dioramaSpecies.SwimAmplitude : 1;

        bool BuildDioramaCreature(string speciesId)
        {
            if (!DioramaSpecies.TryGet(speciesId, out var spec)) return false;
            var model = DioramaLook.InstantiateKit(spec.Kit, transform, DioramaLook.Style.Creature, dioramaMaterials);
            if (!model) { Debug.LogWarning("Missing diorama " + spec.Kit + "; using the classic model."); return false; }
            IsDiorama = true; dioramaSpecies = spec; DioramaVariant = DioramaVariant.Classic;
            model.localScale = Vector3.one * spec.ModelScale;
            tail = model.Find("Tail pivot"); finLeft = model.Find("Left fin pivot"); finRight = model.Find("Right fin pivot");
            crest = model.Find("Crest pivot");
            foreach (var material in dioramaMaterials) if (material) dioramaSlots[material.name.Replace("Diorama / ", "")] = material;
            dioramaBody = Skinned(model, "Body"); dioramaLids = Skinned(model, "Lids"); dioramaLips = Skinned(model, "Lips");
            dioramaPupils = model.Find("Pupils");
            if (dioramaPupils) dioramaPupilRest = dioramaPupils.localPosition;
            bodyPuffed = Shape(dioramaBody, "Puffed"); lidBlink = Shape(dioramaLids, "Blink"); lidHappy = Shape(dioramaLids, "Happy");
            lipHappy = Shape(dioramaLips, "Happy"); lipSurprised = Shape(dioramaLips, "Surprised");
            // A point just in front of the pouty lips: where bubble tricks start.
            dioramaLipsPoint = new GameObject("Diorama lips point").transform;
            dioramaLipsPoint.SetParent(model, false);
            var lipsBounds = dioramaLips ? dioramaLips.sharedMesh.bounds : new Bounds(new Vector3(0, -.032f, -.38f), Vector3.zero);
            Vector3 lipsAt = dioramaLips ? dioramaLips.transform.localPosition : Vector3.zero;
            dioramaLipsPoint.localPosition = lipsAt + new Vector3(lipsBounds.center.x, lipsBounds.center.y, lipsBounds.min.z - .05f);
            dioramaLantern = model.Find("Lantern pivot");
            if (dioramaLantern)
            {
                dioramaLanternRest = dioramaLantern.localRotation;
                var bulb = dioramaLantern.GetComponent<MeshFilter>().sharedMesh.bounds;
                // The bulb is the stalk's top and front: its centre sits about one bulb radius in from both.
                dioramaLanternPoint = new GameObject("Diorama lantern point").transform;
                dioramaLanternPoint.SetParent(dioramaLantern, false);
                dioramaLanternPoint.localPosition = new Vector3(bulb.center.x, bulb.max.y - .085f, bulb.min.z + .08f);
            }
            foreach (var pair in dioramaSlots)
                if (DioramaLook.GlowSlot(pair.Key)) { dioramaGlow = pair.Value; dioramaGlowBase = pair.Value.GetColor("_BaseColor"); }
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

        /// <summary>Colours the creature as one of AD.3's variants from the atlas (Shimmer adds its iridescent sheen).</summary>
        public void SetDioramaVariant(DioramaVariant variant)
        {
            if (!IsDiorama || !DioramaVariants.Ready) return;
            DioramaVariant = variant;
            var sheen = DioramaVariants.Color(dioramaSpecies.Index, variant, DioramaVariants.Sheen);
            for (int slot = 0; slot < DioramaVariants.Sheen; slot++)
            {
                if (!dioramaSlots.TryGetValue(DioramaVariants.SlotName(dioramaSpecies.Id, slot), out var material) || !material) continue;
                var color = DioramaVariants.Color(dioramaSpecies.Index, variant, slot); color.a = 1;
                material.SetColor("_BaseColor", color);
                material.SetColor("_SheenColor", new Color(sheen.r, sheen.g, sheen.b, 1));
                material.SetFloat("_SheenStrength", sheen.a);
                if (material == dioramaGlow) dioramaGlowBase = color;
            }
        }

        /// <summary>Face for the next pose: each 0..1. Containers, the hatch and tricks set it; otherwise it relaxes.</summary>
        public void SetDioramaFace(float happy, float surprised, float puffed, float blink = 0)
        {
            faceHappy = happy; faceSurprised = surprised; facePuffed = puffed; faceBlink = blink;
        }
        /// <summary>A trick's or habit's extras for the next pose: lantern glow (0 at rest, about 3 for a flash), extra tail
        /// swing and extra fin spread, in degrees.</summary>
        public void SetDioramaAction(float glow, float tail, float fins)
        {
            actionGlow = FxParticleRequest.Finite(glow) ? glow : 0; actionTail = FxParticleRequest.Finite(tail) ? tail : 0;
            actionFins = FxParticleRequest.Finite(fins) ? fins : 0;
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
            if (dioramaGlow)
            {
                // The lantern glows softly at rest; its flash pushes it far past the bloom threshold.
                float glow = Mathf.Max(.1f, .6f + (reduced ? 0 : actionGlow));
                dioramaGlow.SetColor("_EmissionColor", dioramaGlowBase * glow);
            }
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

        /// <summary>The kit species' strokes, as deltas on each pivot's rest pose (the classic ones set absolute angles for
        /// their own primitives). The hatch wake follows FX's choreography, as the Pondlet's does.</summary>
        void AnimateDioramaSpecies(float time, float effort, bool reduced, float hatchAge = -1, bool firstHatch = false)
        {
            if (!FxParticleRequest.Finite(time)) time = 0;
            effort = FxParticleRequest.Finite(effort) ? Mathf.Clamp01(effort) : 0;
            AnimateDioramaFace(time, reduced);
            var wake = FxHatchChoreography.Resolve(hatchAge, firstHatch, reduced);
            float t = time + pondletIdentityPhase;
            if (MimicPoseActive && !reduced) t = Mathf.Floor(mimicTime * 2f) * .25f;
            float drive = reduced ? 0 : Mathf.Lerp(.28f, 1, effort) * wake.IdleWeight;
            float tailYaw, finRoll, finRollOther, finPitch, crestRoll;
            switch (dioramaSpecies.Id)
            {
                case "driftwing":
                {
                    // A brief soft downstroke gives way to a long open-sail glide.
                    float stroke = reduced ? 0 : CreatureMotion.WingStroke(t) * drive;
                    tailYaw = Mathf.Sin(t * 2.1f - .6f) * 10 * drive; finRoll = finRollOther = -stroke * 17; finPitch = stroke * 6;
                    crestRoll = Mathf.Sin(t * 2.1f - 1) * 5 * drive;
                    break;
                }
                case "lanternfin":
                {
                    float flutter = Mathf.Sin(t * 4.6f), opposite = Mathf.Sin(t * 4.6f - .5f);
                    tailYaw = Mathf.Sin(t * 4.6f - .8f) * 15 * drive; finRoll = flutter * 13 * drive; finRollOther = opposite * 13 * drive;
                    finPitch = flutter * 5 * drive; crestRoll = Mathf.Sin(t * 4.6f - 1.2f) * 4 * drive;
                    break;
                }
                default:
                {
                    bool ribbon = dioramaSpecies.Id == "ribbonfin";
                    float speed = ribbon ? 2.7f : 3.6f;
                    float wave = Mathf.Sin(t * speed), delayed = Mathf.Sin(t * speed - (ribbon ? .9f : .65f));
                    tailYaw = delayed * (ribbon ? 21 : 15) * drive; finRoll = wave * 12 * drive; finRollOther = delayed * 12 * drive;
                    finPitch = wave * 6 * drive; crestRoll = Mathf.Sin(t * speed - 1.4f) * 6 * drive;
                    break;
                }
            }
            float tailAngle = Mathf.Clamp(tailYaw + wake.Tail + (reduced ? 0 : actionTail), -32, 32);
            float spread = reduced ? 0 : actionFins;
            if (tail) tail.localRotation = pondletTailRest * Quaternion.Euler(0, tailAngle, 0);
            if (finLeft) finLeft.localRotation = pondletLeftRest * Quaternion.Euler(finPitch, 0, -finRoll - wake.Fin - spread);
            if (finRight) finRight.localRotation = pondletRightRest * Quaternion.Euler(-finPitch, 0, finRollOther + wake.Fin + spread);
            if (crest) crest.localRotation = pondletCrestRest * Quaternion.Euler(0, 0, crestRoll + wake.Crest);
            if (dioramaLantern) dioramaLantern.localRotation = dioramaLanternRest * Quaternion.Euler(reduced ? 0 : Mathf.Sin(t * 1.7f) * 5 * drive, 0, 0);
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
        /// <summary>Validation: the material of one of the species' own colour slots (Body, Fin, Belly, Accent, Blush).</summary>
        public Material DioramaSlotForValidation(int slot) =>
            IsDiorama && dioramaSlots.TryGetValue(DioramaVariants.SlotName(dioramaSpecies.Id, slot), out var m) ? m : null;

        void ReleaseDioramaMaterials()
        {
            foreach (var material in dioramaMaterials) if (material) ReleaseCreatureAsset(material);
            dioramaMaterials.Clear(); dioramaSlots.Clear();
        }
    }
}
