using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-A hatch hero moment (AD.5) with the hand-drawn flipbook shapes on Unity's built-in particle system. It follows the
    /// existing hatch cue: it starts only after the saved result, never delays input or collection, and clears on every
    /// cancellation the classic cue clears on (pause, focus, arrange, reset, reload, the look switched off).
    /// </summary>
    public sealed partial class ShopWorld
    {
        public const int DioramaFlipColumns = 4, DioramaFlipRows = 6;
        public const int DioramaRowBubble = 0, DioramaRowSwirl = 1, DioramaRowSparkle = 2, DioramaRowPuff = 3, DioramaRowRipple = 4, DioramaRowCoin = 5;
        ParticleSystem dioramaBubbles, dioramaPuffs, dioramaSparkles, dioramaRings;
        Transform dioramaIncubator, dioramaDomeTransform, dioramaFrameVisual;
        Vector3 dioramaDomeRest, dioramaFrameRest;
        uint dioramaHatchSeed = uint.MaxValue;
        bool dioramaPopped, dioramaFlashed;
        public int DioramaHatchBursts { get; private set; }
        public int DioramaTrickBubbles { get; private set; }

        void BuildDioramaHatch(Transform incubator)
        {
            dioramaIncubator = incubator;
            dioramaDomeTransform = incubator.Find("Crystal incubator dome");
            if (dioramaDomeTransform) dioramaDomeRest = dioramaDomeTransform.localScale;
            dioramaFrameVisual = incubator.Find("Redesign / IncubatorFrame");
            if (dioramaFrameVisual) dioramaFrameRest = dioramaFrameVisual.localPosition;
            var sheet = Resources.Load<Texture2D>(DioramaLook.KitPath + "DioramaFlipbook");
            if (!sheet) Debug.LogWarning("Missing diorama flipbook sheet; the hatch keeps its glow and pose only.");
            var plain = DioramaLook.Unlit("flipbook", Color.white, false, sheet); dioramaOwned.Add(plain);
            var gold = DioramaLook.Unlit("flipbook sparkle", new Color(2.6f, 2.1f, 1.1f, 1), false, sheet); dioramaOwned.Add(gold);
            var flash = DioramaLook.Unlit("flipbook flash ring", new Color(1.5f, 1.5f, 1.4f, 1), false, sheet); dioramaOwned.Add(flash);
            var bubbleFrames = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.15f, .3f), new Keyframe(.82f, .55f), new Keyframe(.9f, .8f), new Keyframe(1, .99f));
            var linear = AnimationCurve.Linear(0, 0, 1, .99f);
            dioramaBubbles = DioramaFlipbook("Diorama bubbles", DioramaRowBubble, plain, -.16f, bubbleFrames, false);
            dioramaPuffs = DioramaFlipbook("Diorama puffs", DioramaRowPuff, plain, -.05f, linear, true);
            dioramaSparkles = DioramaFlipbook("Diorama sparkles", DioramaRowSparkle, gold, 0, linear, true);
            dioramaRings = DioramaFlipbook("Diorama ring flash", DioramaRowRipple, flash, 0, linear, true);
            dioramaHatchLight = new GameObject("Diorama hatch glow").AddComponent<Light>();
            dioramaHatchLight.transform.SetParent(incubator, false);
            dioramaHatchLight.transform.localPosition = new Vector3(0, 1.35f, -.25f);
            dioramaHatchLight.type = LightType.Point; dioramaHatchLight.color = new Color(.7f, 1f, .92f);
            dioramaHatchLight.range = 3.4f; dioramaHatchLight.intensity = 0; dioramaHatchLight.shadows = LightShadows.None;
            dioramaHatchLight.enabled = false;
        }

        ParticleSystem DioramaFlipbook(string name, int row, Material material, float gravity, AnimationCurve frames, bool grow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(dioramaRoot, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96; main.startLifetime = 1; main.startSpeed = 0; main.startSize = .1f;
            main.gravityModifier = gravity;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = DioramaFlipColumns; sheet.numTilesY = DioramaFlipRows;
            sheet.animation = ParticleSystemAnimationType.SingleRow; sheet.rowMode = ParticleSystemAnimationRowMode.Custom; sheet.rowIndex = row;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, frames);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, grow ? AnimationCurve.EaseInOut(0, .55f, 1, 1.25f) :
                new AnimationCurve(new Keyframe(0, .4f), new Keyframe(.18f, 1.05f), new Keyframe(.3f, 1), new Keyframe(1, 1)));
            var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 2.2f; drag.multiplyDragByParticleSize = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance; renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.minParticleSize = 0; renderer.maxParticleSize = 2;
            ps.Play();
            return ps;
        }

        static float DioramaNoise(int index, uint seed, uint salt)
        {
            uint x = unchecked((uint)(index + 1) * 747796405u + seed * 2891336453u + salt);
            x = unchecked(((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u);
            return ((x >> 22) ^ x) / (float)uint.MaxValue;
        }

        void DioramaEmit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float rotation = 0)
        {
            if (!ps) return;
            var p = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size, startLifetime = lifetime,
                startColor = color, rotation = rotation, applyShapeToPosition = false
            };
            ps.Emit(p, 1);
        }

        void DioramaHatchBurst(uint seed, bool first)
        {
            if (!dioramaIncubator) return;
            bool low = fxPool != null && fxPool.ActionLimit < 64;
            DioramaHatch.BurstCounts(first, low, fxReducedMotion, out int bubbles, out int puffs, out int sparkles);
            var t = dioramaIncubator;
            for (int i = 0; i < bubbles; i++)
            {
                float a = DioramaNoise(i, seed, 11), b = DioramaNoise(i, seed, 23), c = DioramaNoise(i, seed, 37);
                float angle = (a - .5f) * Mathf.PI * 1.3f;
                Vector3 local = new Vector3(Mathf.Sin(angle) * .45f, 1.25f + b * .45f, -.42f - c * .12f);
                Vector3 out_ = new Vector3(Mathf.Sin(angle) * (.9f + c), .9f + b * 1.3f, -.7f - a * .6f) * (fxReducedMotion ? .4f : 1);
                Color tint = b < .18f ? new Color(1, .82f, .78f) : b > .86f ? new Color(.86f, 1, .8f) : new Color(.82f, .97f, 1f);
                DioramaEmit(dioramaBubbles, t.TransformPoint(local), t.TransformVector(out_), .07f + c * c * .13f, .85f + a * .45f, tint, (a - .5f) * 40);
            }
            for (int i = 0; i < puffs; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, a = DioramaNoise(i, seed, 51);
                Vector3 local = new Vector3(side * (.55f + a * .1f), 1.0f + i * .08f, -.45f);
                DioramaEmit(dioramaPuffs, t.TransformPoint(local), t.TransformVector(new Vector3(side * .55f, .35f, -.2f)), .34f + a * .08f, .7f, new Color(1, 1, 1, .95f), a * 30);
            }
            for (int i = 0; i < sparkles; i++)
            {
                float angle = i / (float)Mathf.Max(1, sparkles) * Mathf.PI * 2 + DioramaNoise(i, seed, 71) * .3f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * .66f, 1.48f + Mathf.Sin(angle) * .5f, -.52f);
                Vector3 v = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.3f) * .7f;
                DioramaEmit(dioramaSparkles, t.TransformPoint(local), t.TransformVector(v), .13f, .62f, Color.white, angle * 20);
            }
            DioramaHatchBursts++;
        }

        /// <summary>Signature trick: the Pondlet blows a bubble at you when you linger at its tank (the existing glance).</summary>
        void DioramaTrickBubble(CreatureVisual creature, Vector3 viewer)
        {
            if (!dioramaActive || !creature || !creature.DioramaLips || fxReducedMotion) return;
            Vector3 lips = creature.DioramaLips.position;
            Vector3 toward = viewer - lips; toward.y = 0;
            toward = toward.sqrMagnitude > .0001f ? toward.normalized : Vector3.zero;
            DioramaEmit(dioramaBubbles, lips, toward * .18f + Vector3.up * .08f, .085f, 1.25f, new Color(.86f, .98f, 1f));
            DioramaEmit(dioramaBubbles, lips + Vector3.up * .03f, toward * .12f + Vector3.up * .12f, .055f, 1.1f, new Color(.86f, .98f, 1f));
            DioramaTrickBubbles++;
        }

        void TickDioramaHatch(float dt)
        {
            if (!dioramaIncubator) return;
            bool hatching = labHatchRemaining > 0 && fxCues != null && fxCues.IsValid(labHatchOwner);
            float age = hatching ? LabHatchAge : -1;
            var pose = DioramaHatch.Resolve(age, labHatchFirst, fxReducedMotion);
            if (hatching && labHatchSeed != dioramaHatchSeed) { dioramaHatchSeed = labHatchSeed; dioramaPopped = dioramaFlashed = false; }
            if (hatching && labHatchFirst && !fxReducedMotion && !dioramaFlashed && age >= DioramaHatch.WindUpEnd(true))
            {
                dioramaFlashed = true;
                DioramaEmit(dioramaRings, dioramaIncubator.TransformPoint(new Vector3(0, 1.48f, -.58f)), Vector3.zero, 1.25f, .38f, Color.white);
            }
            if (hatching && !dioramaPopped && age >= DioramaHatch.PopTime(labHatchFirst, fxReducedMotion))
            {
                dioramaPopped = true;
                DioramaHatchBurst(labHatchSeed, labHatchFirst);
            }
            float glow = hatching ? pose.Glow : 0;
            if (dioramaHatchLight)
            {
                dioramaHatchLight.enabled = glow > .001f;
                dioramaHatchLight.intensity = glow * (fxReducedMotion ? 1.2f : 3.6f);
            }
            if (dioramaDome) dioramaDome.SetColor("_EmissionColor", new Color(.2f, .55f, .5f) * (glow * (fxReducedMotion ? .35f : .7f)));
            if (dioramaIncubatorFluid) dioramaIncubatorFluid.SetColor("_EmissionColor", new Color(.04f, .2f, .22f) + new Color(.25f, .75f, .6f) * (glow * 1.1f));
            if (dioramaDomeTransform)
            {
                float squash = hatching ? pose.Squash : 0;
                dioramaDomeTransform.localScale = Vector3.Scale(dioramaDomeRest, new Vector3(1 + squash * .035f, 1 - squash * .05f, 1 + squash * .03f));
            }
            if (dioramaFrameVisual)
            {
                float shiver = hatching ? pose.Shiver : 0;
                float t = Time.time * 47;
                dioramaFrameVisual.localPosition = dioramaFrameRest + new Vector3(Mathf.Sin(t) * .004f, Mathf.Sin(t * 1.31f) * .002f, 0) * shiver;
            }
        }

        /// <summary>The kit Pondlet's pose through the hatch: squash and pop, stretch, a lift, then a turn to look at you and a blink.</summary>
        bool PoseDioramaHatchCreature(Display display, float time, float age, bool first, bool quiet)
        {
            var creature = display.Creature;
            if (!creature || !creature.IsDiorama || age < 0) return false;
            float animation = DioramaHatch.AnimationAge(age, first, quiet);
            var pose = DioramaHatch.Resolve(animation, first, quiet);
            if (age >= DioramaHatch.WindUpEnd(true) && age < DioramaHatch.PopTime(true, quiet) && first) pose.Frozen = true;
            var root = creature.transform;
            float scale = CreatureVisual.IncubationScale(display.SpeciesId) * creature.ContainerScale(true);
            float squash = pose.Squash;
            root.localScale = scale * new Vector3(1 + squash * .06f - pose.Stretch * .1f, 1 - squash * .1f - pose.Stretch * .12f, 1 + squash * .05f + pose.Stretch * .35f);
            float idleYaw = -90;
            if (pose.TurnToViewer > 0 && dioramaCamera)
            {
                Vector3 viewer = display.Root.InverseTransformPoint(dioramaCamera.transform.position);
                float toViewer = Mathf.Atan2(-viewer.x, -viewer.z) * Mathf.Rad2Deg;
                idleYaw = Mathf.LerpAngle(-90, Mathf.Clamp(Mathf.DeltaAngle(-90, toViewer), -70, 70) - 90, pose.TurnToViewer);
            }
            root.localPosition = new Vector3(0, pose.Lift, 0);
            root.localRotation = Quaternion.Euler(0, idleYaw, 0);
            creature.SetDioramaFace(pose.Happy, pose.Surprised, squash * .35f, pose.Blink);
            if (dioramaCamera) creature.SetLookTarget(dioramaCamera.transform.position);
            creature.AnimateIncubation(time + display.Seed, .7f, quiet, age, first);
            return true;
        }

        void ClearDioramaHatch()
        {
            foreach (var ps in new[] { dioramaBubbles, dioramaPuffs, dioramaSparkles, dioramaRings })
                if (ps) ps.Clear(true);
            dioramaPopped = dioramaFlashed = false; dioramaHatchSeed = uint.MaxValue;
            if (dioramaHatchLight) { dioramaHatchLight.intensity = 0; dioramaHatchLight.enabled = false; }
            if (dioramaDomeTransform) dioramaDomeTransform.localScale = dioramaDomeRest;
            if (dioramaFrameVisual) dioramaFrameVisual.localPosition = dioramaFrameRest;
            if (dioramaDome) dioramaDome.SetColor("_EmissionColor", Color.black);
            if (dioramaIncubatorFluid) dioramaIncubatorFluid.SetColor("_EmissionColor", new Color(.04f, .2f, .22f));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int DioramaLiveParticlesForValidation
        {
            get
            {
                int count = 0;
                foreach (var ps in new[] { dioramaBubbles, dioramaPuffs, dioramaSparkles, dioramaRings }) if (ps) count += ps.particleCount;
                return count;
            }
        }
        public float DioramaHatchLightForValidation => dioramaHatchLight && dioramaHatchLight.enabled ? dioramaHatchLight.intensity : 0;
        /// <summary>Validation: hold the flipbook particles so a stepped hatch can advance them by the same steps.</summary>
        public void PauseDioramaParticlesForValidation(bool paused)
        {
            foreach (var ps in new[] { dioramaBubbles, dioramaPuffs, dioramaSparkles, dioramaRings })
                if (ps) { if (paused) ps.Pause(true); else ps.Play(true); }
        }
        public void SimulateDioramaParticlesForValidation(float seconds)
        {
            foreach (var ps in new[] { dioramaBubbles, dioramaPuffs, dioramaSparkles, dioramaRings })
                if (ps) ps.Simulate(seconds, true, false, false);
        }
        public Vector3 DioramaDomeScaleForValidation => dioramaDomeTransform ? dioramaDomeTransform.localScale : Vector3.zero;
        public Vector3 DioramaDomeRestForValidation => dioramaDomeRest;
#endif
    }
}
