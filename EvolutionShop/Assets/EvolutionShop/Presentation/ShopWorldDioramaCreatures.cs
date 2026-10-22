using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>AD-B: the kit species in their containers (the Pondlet keeps AD-A's own path): swim, idle habit, the signature
    /// trick when the player lingers at a display, the family water tint, and the incubator fluid. Presentation only: no
    /// rule, save or cue owner changes; Reduced motion plays no habit or trick.</summary>
    public sealed partial class ShopWorld
    {
        /// <summary>Signature tricks played by kit species other than the Pondlet (its bubble is AD-A's PondletGlance).</summary>
        public int DioramaTricksPlayed { get; private set; }
        public int DioramaCreatureBursts { get; private set; }

        void TickDioramaResident(Display display, float dt)
        {
            var creature = display.Creature;
            string species = display.SpeciesId;
            bool visible = fxEnabled && fxPolicy == FxViewPolicy.Visible && display.Root.gameObject.activeInHierarchy;
            bool calm = fxReducedMotion || !fxEnabled;
            bool full = display.Habitat != null && display.Habitat.FullDisplay;
            dt = FxParticleRequest.Finite(dt) ? Mathf.Clamp(dt, 0, .25f) : 0;
            float clockBefore = display.PondletClock, trickBefore = display.DioramaTrickAge;
            if (visible)
            {
                if (!fxReducedMotion) display.PondletClock += dt;
                display.DioramaTrickCooldown = Mathf.Max(0, display.DioramaTrickCooldown - dt);
                if (display.DioramaTrickAge >= 0)
                {
                    display.DioramaTrickAge += dt;
                    if (display.DioramaTrickAge >= DioramaTricks.Duration(species)) display.DioramaTrickAge = -1;
                }
                // The same linger as the Pondlet's glance: 1.5 s at a display tank, then an 8 s rest.
                display.PondletFocus = full && aquariumFocus && aquariumFocus.transform == display.Root.parent ? display.PondletFocus + dt : 0;
                if (full && display.PondletFocus >= 1.5f && display.DioramaTrickCooldown <= 0 && !calm && display.DioramaTrickAge < 0)
                {
                    display.DioramaTrickAge = 0; trickBefore = -1; display.DioramaTrickCooldown = 8; DioramaTricksPlayed++;
                }
            }
            else display.DioramaTrickAge = -1;
            var pose = CreatureMotion.Resolve(species, display.PondletClock, display.PondletPhase, calm);
            if (display.DioramaLarge) pose.Position *= creature.DioramaSwim;
            var idle = calm || !visible ? default : DioramaTricks.Idle(species, display.PondletClock, display.PondletPhase, clockBefore);
            bool tricking = !calm && display.DioramaTrickAge >= 0;
            var trick = tricking ? DioramaTricks.Trick(species, display.DioramaTrickAge, trickBefore) : default;
            // A trick that turns the creature (a loop, a roll, a balloon) eases it to mid-water first and back after, so it
            // never breaks the glass however high or low its swim had taken it.
            if (tricking) pose.Position.y *= 1 - DioramaTricks.Settle(species, display.DioramaTrickAge);
            creature.SetDioramaFace(tricking ? Mathf.Max(.2f, trick.Happy) : .2f + idle.Happy, Mathf.Max(idle.Surprised, trick.Surprised),
                Mathf.Max(idle.Puffed, trick.Puffed));
            creature.SetDioramaAction(idle.Glow + trick.Glow, idle.Tail + trick.Tail, idle.Fins + trick.Fins);
            if (dioramaCamera) creature.SetLookTarget(dioramaCamera.transform.position);
            creature.transform.localPosition = pose.Position + Vector3.up * (idle.Lift + trick.Lift);
            creature.transform.localRotation = Quaternion.Euler(pose.Angles) *
                Quaternion.Euler(idle.Pitch + trick.Pitch, idle.Yaw + trick.Yaw, idle.Roll + trick.Roll);
            creature.AnimateSpecies(display.PondletClock, pose.Effort, calm);
            if (visible && !calm && (trick.Release || idle.Release))
                DioramaCreatureBurst(creature, trick.Release, dioramaCamera ? dioramaCamera.transform.position : creature.transform.position);
        }

        /// <summary>The bubbles, sparkles or puff a trick or habit releases at its beat, from the creature's lips or lantern.</summary>
        void DioramaCreatureBurst(CreatureVisual creature, bool trick, Vector3 viewer)
        {
            if (!dioramaActive || !creature || !creature.IsDiorama || fxReducedMotion) return;
            if (creature.DioramaSpecies.Id == "pondlet") { DioramaTrickBubble(creature, viewer); return; }
            bool low = fxPool != null && fxPool.ActionLimit < 64;
            Vector3 lips = creature.DioramaLips ? creature.DioramaLips.position : creature.transform.position;
            Vector3 toward = viewer - lips; toward.y = 0;
            toward = toward.sqrMagnitude > .0001f ? toward.normalized : Vector3.zero;
            float size = Mathf.Max(.4f, creature.transform.lossyScale.x);
            var bubble = new Color(.86f, .98f, 1f);
            switch (creature.DioramaSpecies.Id)
            {
                case "lanternfin":
                {
                    var bulb = creature.DioramaLanternPoint ? creature.DioramaLanternPoint.position : lips;
                    int count = trick ? (low ? 2 : 4) : 0;
                    for (int i = 0; i < count; i++)
                    {
                        float a = i / (float)count * Mathf.PI * 2 + .4f;
                        DioramaEmit(dioramaSparkles, bulb, new Vector3(Mathf.Cos(a) * .22f, .12f + Mathf.Sin(a) * .18f, 0) + toward * .08f,
                            .07f * size, .55f, new Color(1f, .93f, .6f), a * 30);
                    }
                    break;
                }
                case "bubblebelly":
                {
                    // Its burp of bubbles after the balloon, or one bubble for a hiccup.
                    int count = trick ? (low ? 3 : 6) : 1;
                    for (int i = 0; i < count; i++)
                    {
                        float a = (i - (count - 1) * .5f) * .35f;
                        DioramaEmit(dioramaBubbles, lips + Vector3.up * .02f * i, toward * (.14f + .03f * i) + new Vector3(a * .1f, .1f + .03f * i, a * .1f),
                            (i == 0 ? .08f : .05f) * size, 1.1f + i * .05f, bubble, a * 40);
                    }
                    break;
                }
                default:
                {
                    int count = low ? 1 : 3;
                    for (int i = 0; i < count; i++)
                        DioramaEmit(dioramaBubbles, lips + Vector3.up * .025f * i, toward * .12f + Vector3.up * (.1f + .03f * i), (.06f - .01f * i) * size,
                            1.05f, bubble, i * 25);
                    break;
                }
            }
            DioramaCreatureBursts++;
        }

        /// <summary>The water tint of a display while the look is on: its resident's family (AD.4), easing over about a second
        /// when the resident changes; empty tanks keep the classic water.</summary>
        Color DioramaWaterTint(Display d, float dt, Color classic)
        {
            var target = d.Creature && d.Creature.IsDiorama ? DioramaWater.Tint(d.Creature.DioramaSpecies.Family) : classic;
            if (!d.DioramaWaterSet) { d.DioramaWater = target; d.DioramaWaterSet = true; }
            float step = FxParticleRequest.Finite(dt) ? Mathf.Clamp(dt, 0, .25f) * 1.2f : 0;
            d.DioramaWater = new Color(Mathf.MoveTowards(d.DioramaWater.r, target.r, step), Mathf.MoveTowards(d.DioramaWater.g, target.g, step),
                Mathf.MoveTowards(d.DioramaWater.b, target.b, step), Mathf.MoveTowards(d.DioramaWater.a, target.a, step * .25f));
            return d.DioramaWater;
        }

        /// <summary>The incubator's fluid takes the growing creature's family tint too.</summary>
        void TickDioramaIncubatorFluid(float dt)
        {
            if (!dioramaIncubatorFluid || !displays.TryGetValue("incubator", out var d)) return;
            var target = d.Creature && d.Creature.IsDiorama ? DioramaWater.Fluid(d.Creature.DioramaSpecies.Family) : DioramaWater.Fluid(DioramaFamily.Brookkin);
            var now = dioramaIncubatorFluid.GetColor("_BaseColor");
            float step = Mathf.Clamp(dt, 0, .25f) * 1.2f;
            dioramaIncubatorFluid.SetColor("_BaseColor", new Color(Mathf.MoveTowards(now.r, target.r, step), Mathf.MoveTowards(now.g, target.g, step),
                Mathf.MoveTowards(now.b, target.b, step), Mathf.MoveTowards(now.a, target.a, step * .25f)));
            // Its glow follows the same family colour, with the hatch's glow on top while one plays.
            dioramaFluidGlow = DioramaFluidGlow(dioramaIncubatorFluid.GetColor("_BaseColor"));
            dioramaIncubatorFluid.SetColor("_EmissionColor", dioramaFluidGlow + dioramaFluidHatchGlow);
        }

        /// <summary>The resident a display shows: the game's own, unless a validation route previews another species there
        /// (development builds only).</summary>
        void ShowResident(string id, string species, string specimen)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (previewResidents.TryGetValue(id, out var preview)) { species = preview.species; specimen = preview.specimen; }
#endif
            SetCreature(displays[id], species, specimen);
        }

        bool PreviewLabel(string id, out string label)
        {
            label = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (previewResidents.TryGetValue(id, out var preview)) label = SpeciesName(preview.species).ToUpperInvariant() + "  /  PREVIEW";
#endif
            return label != null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly Dictionary<string, (string species, string specimen)> previewResidents = new Dictionary<string, (string species, string specimen)>();
        /// <summary>Validation: show a species in a display without a saved specimen (presentation only, nothing saved). It
        /// stays through the game's refreshes until the route clears it with a null species, which puts the saved resident back.</summary>
        public void PreviewResidentForValidation(string id, string species, string specimen)
        {
            if (!displays.TryGetValue(id, out var d)) return;
            if (string.IsNullOrEmpty(species))
            {
                previewResidents.Remove(id);
                if (lastModel != null) Refresh(lastModel); else SetCreature(d, null);
                return;
            }
            previewResidents[id] = (species, specimen);
            SetCreature(d, species, specimen);
            if (lastModel != null) Refresh(lastModel);
        }
        /// <summary>The age of the display's signature trick (the Pondlet's is AD-A's glance and bubble), or -1.</summary>
        public float DioramaTrickAgeForValidation(string id) =>
            displays.TryGetValue(id, out var d) ? d.SpeciesId == "pondlet" ? d.PondletGlanceAge : d.DioramaTrickAge : -1;
        public Color DioramaWaterForValidation(string id) => displays.TryGetValue(id, out var d) && d.DioramaWaterSet ? d.DioramaWater : Color.clear;
        public Color DioramaIncubatorFluidForValidation => dioramaIncubatorFluid ? dioramaIncubatorFluid.GetColor("_BaseColor") : Color.clear;
        public void SnapDioramaWaterForValidation() { foreach (var d in displays.Values) d.DioramaWaterSet = false; }
#endif
    }
}
