using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        sealed class AquariumHabitat
        {
            public Transform Water;
            public Vector3 WaterRest;
            public readonly Transform[] Plants = new Transform[2], Bubbles = new Transform[4];
            public readonly Quaternion[] PlantRest = new Quaternion[2];
            public readonly Vector3[] BubbleRest = new Vector3[4];
            public float Clock, Seed;
            public string TankId;
        }
        bool aquariumLow;
        Station aquariumFocus;
        public int PondletPlacementCount { get; private set; }
        public int PondletGlanceCount { get; private set; }
        AquariumHabitat BuildAquariumHabitat(Transform root, float seed)
        {
            var habitat = new AquariumHabitat { Water = root.Find("Water volume"), Seed = seed, TankId = root.GetComponent<Station>().TankId };
            if (habitat.Water) habitat.WaterRest = habitat.Water.localScale;
            int plants = 0, bubblesFound = 0;
            // Cache existing visual children once. No new transforms/renderers/materials/lights.
            foreach (Transform child in root)
            {
                if (child.name == "Aquatic frond" && plants < 2)
                {
                    child.localScale = new Vector3(.13f, .37f, .065f);
                    var p = child.localPosition; p.y = 1.26f; child.localPosition = p;
                    habitat.Plants[plants] = child; habitat.PlantRest[plants++] = child.localRotation;
                }
                if (child.name == "Air bubble" && bubblesFound < 4)
                { habitat.Bubbles[bubblesFound] = child; habitat.BubbleRest[bubblesFound++] = child.localScale; }
            }
            return habitat;
        }
        void ResetPondletIdentity(Display display)
        {
            display.PondletClock = 0; display.PondletPhase = PondletMotion.IdentityPhase(display.FxSpecimenId);
            display.PondletFocus = display.PondletCooldown = 0; display.PondletPlacedAge = display.PondletGlanceAge = -1;
            display.PondletSequence = 0; if (display.Creature) display.Creature.SetPondletIdentity(display.FxSpecimenId);
        }
        public void SetAquariumFocus(Station station) { aquariumFocus = station; }
        public void ClearFishMimic()
        { foreach (var display in displays.Values) if (display.Creature) display.Creature.ClearMimicPose(); }
        void CancelAquariumTransient()
        {
            aquariumFocus = null; ClearFishMimic();
            foreach (var display in displays.Values)
            { display.PondletFocus = 0; display.PondletPlacedAge = display.PondletGlanceAge = -1; }
        }
        public bool PresentPondletPlacement(string tankId, uint sequence)
        {
            if (!fxEnabled || fxPolicy != FxViewPolicy.Visible || string.IsNullOrEmpty(tankId) || !displays.TryGetValue(tankId, out var display) ||
                display.Habitat == null || display.SpeciesId != "pondlet" || !display.Creature || string.IsNullOrEmpty(display.FxSpecimenId)) return false;
            if (fxCues == null || !fxCues.Play(fxCues.Find(tankId), FxCueId.PondletPlaced, sequence, FxCueOrigin.Manual, .52f, 4)) return false;
            display.PondletPlacedAge = 0; PondletPlacementCount++; return true;
        }
        void TickPondlet(Display display, float dt)
        {
            bool visible = fxEnabled && fxPolicy == FxViewPolicy.Visible;
            if (visible)
            {
                display.PondletClock += dt;
                display.PondletCooldown = Mathf.Max(0, display.PondletCooldown - dt);
                if (display.PondletPlacedAge >= 0) { display.PondletPlacedAge += dt; if (display.PondletPlacedAge >= .52f) display.PondletPlacedAge = -1; }
                if (display.PondletGlanceAge >= 0) { display.PondletGlanceAge += dt; if (display.PondletGlanceAge >= .6f) display.PondletGlanceAge = -1; }
                display.PondletFocus = aquariumFocus && aquariumFocus.transform == display.Root.parent ? display.PondletFocus + dt : 0;
                if (display.PondletFocus >= 1.5f && display.PondletCooldown <= 0 && !fxReducedMotion && display.PondletPlacedAge < 0)
                {
                    var token = fxCues.Find(display.Habitat.TankId);
                    if (fxCues.Play(token, FxCueId.PondletGlance, ++display.PondletSequence, FxCueOrigin.Manual, .6f, 1))
                    { display.PondletGlanceAge = 0; display.PondletCooldown = 8; PondletGlanceCount++; }
                }
            }
            var pose = PondletMotion.Resolve(display.PondletClock, display.PondletPhase, fxReducedMotion || !fxEnabled, display.PondletPlacedAge, display.PondletGlanceAge);
            display.Creature.transform.localPosition = pose.Position;
            display.Creature.transform.localRotation = Quaternion.Euler(pose.Angles);
            display.Creature.AnimatePondlet(display.PondletClock, pose.Effort, fxReducedMotion || !fxEnabled, display.PondletGlanceAge);
        }
        void TickAquariumHabitat(float dt)
        {
            foreach (var display in displays.Values)
            {
                var h = display.Habitat; if (h == null) continue;
                bool moving = fxEnabled && fxPolicy == FxViewPolicy.Visible && !fxReducedMotion;
                if (moving) h.Clock += dt;
                float time = h.Clock + h.Seed;
                for (int i = 0; i < h.Plants.Length; i++) if (h.Plants[i])
                    h.Plants[i].localRotation = h.PlantRest[i] * Quaternion.Euler(0, 0, moving ? Mathf.Sin(time * 1.1f + i) * .8f : 0);
                // Breathe inward only: the fixed original water/sand prism never grows.
                if (h.Water) h.Water.localScale = new Vector3(h.WaterRest.x, h.WaterRest.y * (moving ? 1 - (.5f + .5f * Mathf.Sin(time * .8f)) * .004f : 1), h.WaterRest.z);
                for (int i = 0; i < h.Bubbles.Length; i++)
                {
                    var bubble = h.Bubbles[i]; if (!bubble) continue;
                    bool active = moving && (!aquariumLow || i == 0);
                    bubble.gameObject.SetActive(active); if (!active) continue;
                    float progress = Mathf.Repeat(time * .19f + i * .25f, 1);
                    bubble.localPosition = new Vector3(.75f + Mathf.Sin(time * 1.2f + i) * .018f, 1.10f + progress * .84f, .29f);
                    // Zero-size ends conceal the reset seam; no spawn/despawn allocation.
                    float fade = Mathf.Clamp01(Mathf.Min(progress, 1 - progress) / .12f);
                    bubble.localScale = h.BubbleRest[i] * fade;
                }
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Transform PondletVisualForValidation(string id) => displays.TryGetValue(id, out var d) && d.Creature ? d.Creature.transform : null;
        public float PondletAgeForValidation(string id) => displays.TryGetValue(id, out var d) ? d.PondletClock : -1;
        public bool PondletTransientForValidation(string id) => displays.TryGetValue(id, out var d) && (d.PondletPlacedAge >= 0 || d.PondletGlanceAge >= 0);
        public void PreviewPondletForValidation(string id, float time, bool reduced, float glanceAge = -1, float placedAge = -1)
        {
            if (!displays.TryGetValue(id, out var d) || !d.Creature || d.SpeciesId != "pondlet") return;
            var pose = PondletMotion.Resolve(time, d.PondletPhase, reduced, placedAge, glanceAge);
            d.Creature.transform.localPosition = pose.Position; d.Creature.transform.localRotation = Quaternion.Euler(pose.Angles);
            d.Creature.AnimatePondlet(time, pose.Effort, reduced, glanceAge);
        }
#endif
    }
}
