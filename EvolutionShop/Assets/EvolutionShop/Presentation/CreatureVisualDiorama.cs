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
        /// <summary>GR-12: how much taller a crest stands at its fullest Flare (1.3 times).</summary>
        public const float CrestFlareGrowth = .3f;
        readonly List<Material> dioramaMaterials = new List<Material>();
        readonly Dictionary<string, Material> dioramaSlots = new Dictionary<string, Material>();
        SkinnedMeshRenderer dioramaBody, dioramaLids, dioramaLips;
        Transform dioramaPupils, dioramaLipsPoint, dioramaLantern, dioramaLanternPoint, dioramaCrestPoint;
        Vector3 dioramaCrestScale = Vector3.one;
        Material dioramaGlow;
        Color dioramaGlowBase;
        Vector3 dioramaPupilRest;
        Quaternion dioramaLanternRest;
        DioramaSpecies dioramaSpecies;
        int bodyPuffed = -1, lidBlink = -1, lidHappy = -1, lipHappy = -1, lipSurprised = -1;
        float faceHappy = .2f, faceSurprised, facePuffed, faceBlink, actionGlow, actionTail, actionFins, actionFlare;
        Vector3 lookTarget;
        bool lookTargetSet;

        /// <summary>True for a kit creature; false for every classic one.</summary>
        public bool IsDiorama { get; private set; }
        public DioramaSpecies DioramaSpecies => dioramaSpecies;
        public DioramaVariant DioramaVariant { get; private set; }
        public Transform DioramaLips => dioramaLipsPoint;
        /// <summary>The lantern's bulb (Sparkline), where its flash sparkles; null for the other families.</summary>
        public Transform DioramaLanternPoint => dioramaLanternPoint;
        /// <summary>GR-12: the top of the crest (the Cinderling's spine tips, the Dewling's dew beads), where their sparks and
        /// droplets fly from; null without a crest mesh.</summary>
        public Transform DioramaCrestPoint => dioramaCrestPoint;
        public Material DioramaGlowMaterial => dioramaGlow;

        /// <summary>Extra container scale: a kit creature reads about twice as large in display tanks and the incubator.</summary>
        public float ContainerScale(bool incubator) => IsDiorama ? (incubator ? dioramaSpecies.IncubatorScale : dioramaSpecies.TankScale) : 1;
        /// <summary>How much of its swim path a kit creature uses in the large tanks, so the larger model stays in the glass.</summary>
        public float DioramaSwim => IsDiorama ? dioramaSpecies.SwimAmplitude : 1;
        /// <summary>GR-12: the kit body's length (its Body mesh, nose to tail root, before the model's scale), which the greybox
        /// sets to the species' GR size class.</summary>
        public float DioramaBodyLength => dioramaBody && dioramaBody.sharedMesh ? dioramaBody.sharedMesh.bounds.size.z : 0;

        /// <summary>GR-12: builds the kit creature whatever the classic look switch says, for the greybox, which shows every kit
        /// species in the AD look. False when the species or its model is missing.</summary>
        public bool BuildKit(string speciesId)
        {
            species = speciesId;
            if (!DioramaSpecies.TryGet(speciesId, out _) || !BuildDioramaCreature(speciesId)) return false;
            Animate(0);
            return true;
        }

        bool BuildDioramaCreature(string speciesId)
        {
            if (!DioramaSpecies.TryGet(speciesId, out var spec)) return false;
            var model = DioramaLook.InstantiateKit(spec.Kit, transform, DioramaLook.Style.Creature, dioramaMaterials);
            if (!model) { Debug.LogWarning("Missing diorama " + spec.Kit + "; using the classic model."); return false; }
            IsDiorama = true; dioramaSpecies = spec; DioramaVariant = DioramaVariant.Classic;
            model.localScale = Vector3.one * spec.ModelScale;
            tail = model.Find("Tail pivot"); finLeft = model.Find("Left fin pivot"); finRight = model.Find("Right fin pivot");
            crest = model.Find("Crest pivot");
            dioramaCrestPoint = null;
            if (crest)
            {
                dioramaCrestScale = crest.localScale;
                var crestMesh = crest.GetComponent<MeshFilter>();
                if (crestMesh && crestMesh.sharedMesh)
                {
                    var b = crestMesh.sharedMesh.bounds;
                    dioramaCrestPoint = new GameObject("Diorama crest point").transform;
                    dioramaCrestPoint.SetParent(crest, false);
                    dioramaCrestPoint.localPosition = new Vector3(b.center.x, b.max.y, b.center.z);
                }
            }
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
        /// swing and extra fin spread, in degrees, and (GR-12) the Cinderling's spines flaring (0 at rest, 1 blazing).</summary>
        public void SetDioramaAction(float glow, float tail, float fins, float flare = 0)
        {
            actionGlow = FxParticleRequest.Finite(glow) ? glow : 0; actionTail = FxParticleRequest.Finite(tail) ? tail : 0;
            actionFins = FxParticleRequest.Finite(fins) ? fins : 0; actionFlare = FxParticleRequest.Finite(flare) ? Mathf.Clamp(flare, 0, 1.5f) : 0;
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

        /// <summary>GR-12: how far each family's strokes turn its fins (roll along the fin, and pitch) and its tail (yaw) at full
        /// drive, in degrees. Its strokes use them, and a creature's swim room samples its fins and tail at them.</summary>
        public static (float roll, float pitch, float tail) DioramaStroke(DioramaFamily family, string species)
        {
            switch (family)
            {
                case DioramaFamily.Airfin: return (17, 6, 10);
                case DioramaFamily.Cinderling: return (10, 5, 18);
                case DioramaFamily.Dewling: return (16, 7, 10);
                case DioramaFamily.Reefback: return (16, 8, 8);
                case DioramaFamily.Saltscale: return (6, 3, 17);
                case DioramaFamily.Sparkline: return (13, 5, 15);
                default: return (12, 6, species == "ribbonfin" ? 21 : 15);
            }
        }
        /// <summary>The most a kit tail ever turns, with a trick's swing and the hatch's wake (degrees).</summary>
        public const float DioramaTailLimit = 32;

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
            // GR-12: each family strokes its own way (AD-B's Driftwing, Lanternfin and Brookkin), each species at its own tempo
            // (1 for AD-B's five, which stroke exactly as before).
            float tempo = dioramaSpecies.Tempo;
            var amp = DioramaStroke(dioramaSpecies.Family, dioramaSpecies.Id);
            switch (dioramaSpecies.Family)
            {
                case DioramaFamily.Airfin:
                {
                    // A brief soft downstroke gives way to a long open-sail glide.
                    float stroke = reduced ? 0 : CreatureMotion.WingStroke(t * tempo) * drive;
                    tailYaw = Mathf.Sin(t * 2.1f * tempo - .6f) * amp.tail * drive; finRoll = finRollOther = -stroke * amp.roll; finPitch = stroke * amp.pitch;
                    crestRoll = Mathf.Sin(t * 2.1f * tempo - 1) * 5 * drive;
                    break;
                }
                case DioramaFamily.Cinderling:
                {
                    // A slow, strong beat of the tail, the chunky body following, its spines swaying like embers.
                    float w = 3f * tempo;
                    float beat = Mathf.Sin(t * w), delayed = Mathf.Sin(t * w - .7f);
                    tailYaw = delayed * amp.tail * drive; finRoll = beat * amp.roll * drive; finRollOther = delayed * amp.roll * drive;
                    finPitch = beat * amp.pitch * drive; crestRoll = Mathf.Sin(t * w * .5f - 1.1f) * 4 * drive;
                    break;
                }
                case DioramaFamily.Dewling:
                {
                    // Little round fins paddling quickly in turn, and a short flutter of the tail.
                    float w = 5.4f * tempo;
                    float paddle = Mathf.Sin(t * w), other = Mathf.Sin(t * w - Mathf.PI * .5f);
                    tailYaw = Mathf.Sin(t * w - .6f) * amp.tail * drive; finRoll = paddle * amp.roll * drive; finRollOther = other * amp.roll * drive;
                    finPitch = paddle * amp.pitch * drive; crestRoll = Mathf.Sin(t * w * .4f) * 3 * drive;
                    break;
                }
                case DioramaFamily.Reefback:
                {
                    // Slow, heavy strokes of both flippers together, like a turtle's, a lazy tail and its corals swaying.
                    float w = 2.2f * tempo;
                    float flap = Mathf.Sin(t * w), late = Mathf.Sin(t * w - .5f);
                    tailYaw = late * amp.tail * drive; finRoll = finRollOther = flap * amp.roll * drive; finPitch = Mathf.Sin(t * w - 1.2f) * amp.pitch * drive;
                    crestRoll = Mathf.Sin(t * w * .6f) * 3 * drive;
                    break;
                }
                case DioramaFamily.Saltscale:
                {
                    // Quick, stiff beats of the tail, the little fins held close and trimming.
                    float w = 5.2f * tempo;
                    float beat = Mathf.Sin(t * w), trim = Mathf.Sin(t * w * .5f);
                    tailYaw = Mathf.Sin(t * w - .5f) * amp.tail * drive; finRoll = trim * amp.roll * drive; finRollOther = -trim * amp.roll * drive;
                    finPitch = beat * amp.pitch * drive; crestRoll = beat * 2 * drive;
                    break;
                }
                case DioramaFamily.Sparkline:
                {
                    float w = 4.6f * tempo;
                    float flutter = Mathf.Sin(t * w), opposite = Mathf.Sin(t * w - .5f);
                    tailYaw = Mathf.Sin(t * w - .8f) * amp.tail * drive; finRoll = flutter * amp.roll * drive; finRollOther = opposite * amp.roll * drive;
                    finPitch = flutter * amp.pitch * drive; crestRoll = Mathf.Sin(t * w - 1.2f) * 4 * drive;
                    break;
                }
                default:
                {
                    bool ribbon = dioramaSpecies.Id == "ribbonfin";
                    float speed = (ribbon ? 2.7f : 3.6f) * tempo;
                    float wave = Mathf.Sin(t * speed), delayed = Mathf.Sin(t * speed - (ribbon ? .9f : .65f));
                    tailYaw = delayed * amp.tail * drive; finRoll = wave * amp.roll * drive; finRollOther = delayed * amp.roll * drive;
                    finPitch = wave * amp.pitch * drive; crestRoll = Mathf.Sin(t * speed - 1.4f) * 6 * drive;
                    break;
                }
            }
            float tailAngle = Mathf.Clamp(tailYaw + wake.Tail + (reduced ? 0 : actionTail), -DioramaTailLimit, DioramaTailLimit);
            float spread = reduced ? 0 : actionFins;
            if (tail) tail.localRotation = pondletTailRest * Quaternion.Euler(0, tailAngle, 0);
            if (finLeft) finLeft.localRotation = pondletLeftRest * Quaternion.Euler(finPitch, 0, -finRoll - wake.Fin - spread);
            if (finRight) finRight.localRotation = pondletRightRest * Quaternion.Euler(-finPitch, 0, finRollOther + wake.Fin + spread);
            if (crest)
            {
                crest.localRotation = pondletCrestRest * Quaternion.Euler(0, 0, crestRoll + wake.Crest);
                // GR-12: the crest stands taller from its roots, the Cinderling's spines flaring and the Reefback's corals
                // blooming (never with Reduced motion).
                crest.localScale = dioramaCrestScale * (1 + (reduced ? 0 : actionFlare) * CrestFlareGrowth);
            }
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
