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
            public MeshRenderer WaterRenderer;
            public MaterialPropertyBlock WaterProperties;
            public Color WaterColor;
            public Bounds InnerWater;
            public readonly MeshRenderer[] BubbleRenderers = new MeshRenderer[4];
            public readonly MaterialPropertyBlock[] BubbleProperties = new MaterialPropertyBlock[4];
            public readonly Color[] BubbleColors = new Color[4];
            public Vector3 LastFishPosition;
            public float TrailClock;
            public uint TrailSequence, PlacementMask;
            public bool FishPositionKnown, TrailActive;
            public Transform FishBoundsRoot;
            public Renderer[] FishRenderers;
        }
        static readonly int AquariumColorId = Shader.PropertyToID("_BaseColor");
        bool aquariumLow;
        Station aquariumFocus;
        public int PondletPlacementCount { get; private set; }
        public int PondletGlanceCount { get; private set; }
        public int AquariumTrailEmissionCount { get; private set; }
        public int AquariumPlacementEmissionCount { get; private set; }
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
            if (habitat.TankId == "tank-a" && habitat.Water) {
                habitat.WaterRenderer = habitat.Water.GetComponent<MeshRenderer>();
                habitat.WaterProperties = new MaterialPropertyBlock();
                habitat.WaterColor = habitat.WaterRenderer.sharedMaterial.GetColor(AquariumColorId);
                Vector3 min = habitat.Water.localPosition-habitat.WaterRest*.5f+Vector3.one*.025f;
                Vector3 max = habitat.Water.localPosition+habitat.WaterRest*.5f-Vector3.one*.025f;
                var sand = root.Find("Tank sand");
                if (sand) min.y = Mathf.Max(min.y,sand.localPosition.y+sand.localScale.y*.5f+.025f);
                habitat.InnerWater = new Bounds((min+max)*.5f,max-min);
                for (int i=0;i<habitat.Bubbles.Length;i++) if (habitat.Bubbles[i]) {
                    habitat.BubbleRenderers[i]=habitat.Bubbles[i].GetComponent<MeshRenderer>();
                    habitat.BubbleProperties[i]=new MaterialPropertyBlock();
                    habitat.BubbleColors[i]=habitat.BubbleRenderers[i].sharedMaterial.GetColor(AquariumColorId);
                }
            }
            return habitat;
        }
        void ResetPondletIdentity(Display display)
        {
            display.PondletClock = 0; display.PondletPhase = PondletMotion.IdentityPhase(display.FxSpecimenId);
            display.PondletFocus = display.PondletCooldown = 0; display.PondletPlacedAge = display.PondletGlanceAge = -1;
            display.PondletSequence = 0; if (display.Creature) display.Creature.SetPondletIdentity(display.FxSpecimenId);
            ResetAquariumHero(display.Habitat);
        }
        public void SetAquariumFocus(Station station) { aquariumFocus = station; }
        public void ClearFishMimic()
        { foreach (var display in displays.Values) if (display.Creature) display.Creature.ClearMimicPose(); }
        void CancelAquariumTransient()
        {
            aquariumFocus = null; ClearFishMimic();
            foreach (var display in displays.Values)
            { display.PondletFocus = 0; display.PondletPlacedAge = display.PondletGlanceAge = -1; ResetAquariumHero(display.Habitat); }
        }
        public bool PresentPondletPlacement(string tankId, uint sequence)
        {
            if (!fxEnabled || fxPolicy != FxViewPolicy.Visible || string.IsNullOrEmpty(tankId) || !displays.TryGetValue(tankId, out var display) ||
                display.Habitat == null || display.SpeciesId != "pondlet" || !display.Creature || string.IsNullOrEmpty(display.FxSpecimenId)) return false;
            if (fxCues == null || !fxCues.Play(fxCues.Find(tankId), FxCueId.PondletPlaced, sequence, FxCueOrigin.Manual, .52f, 4)) return false;
            display.PondletPlacedAge = 0; display.Habitat.PlacementMask = 0; PondletPlacementCount++;
            EmitAquariumPlacement(display); return true;
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
                if (h.TankId == "tank-a") { TickAquariumHero(display,dt); continue; }
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
        void ResetAquariumHero(AquariumHabitat h)
        {
            if (h == null || h.TankId != "tank-a") return;
            h.FishPositionKnown = h.TrailActive = false; h.TrailClock = 0; h.PlacementMask = 0;
            if (h.Water) h.Water.localScale = h.WaterRest;
            if (h.WaterRenderer) h.WaterRenderer.SetPropertyBlock(null);
            for(int i=0;i<h.Plants.Length;i++) if(h.Plants[i]) h.Plants[i].localRotation=h.PlantRest[i];
            for(int i=0;i<h.Bubbles.Length;i++) if(h.Bubbles[i]) {
                h.Bubbles[i].localScale=h.BubbleRest[i]; h.Bubbles[i].gameObject.SetActive(false);
                if(h.BubbleRenderers[i]) h.BubbleRenderers[i].SetPropertyBlock(null);
            }
        }
        void CancelAquariumOwner(FxOwnerToken owner)
        {
            if(fxCues == null || !owner.IsDefined || !owner.Equals(fxCues.Find("tank-a")) || !displays.TryGetValue("tank-a",out var d)) return;
            ResetAquariumHero(d.Habitat); d.PondletPlacedAge=d.PondletGlanceAge=-1;
        }
        bool AquariumHeroVisible(Display d) => fxEnabled && isActiveAndEnabled && fxPolicy==FxViewPolicy.Visible &&
            d.Root && d.Root.gameObject.activeInHierarchy && d.Habitat.Water && d.Habitat.Water.gameObject.activeInHierarchy &&
            fxCues!=null && fxCues.IsValid(fxCues.Find(d.Habitat.TankId));
        Bounds AquariumFishBounds(Display d)
        {
            var h=d.Habitat; var creature=d.Creature.transform; var station=d.Root.parent;
            if(h.FishBoundsRoot!=creature) {
                h.FishBoundsRoot=creature; h.FishRenderers=creature.GetComponentsInChildren<Renderer>();
            }
            var result=new Bounds(station.InverseTransformPoint(creature.position),Vector3.zero);
            // Convert all corners of current world renderer bounds: conservative under tank rotation.
            foreach(var renderer in h.FishRenderers) if(renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) {
                var bounds=renderer.bounds; var min=bounds.min; var max=bounds.max;
                for(int i=0;i<8;i++) result.Encapsulate(station.InverseTransformPoint(new Vector3(
                    (i&1)==0 ? min.x : max.x,(i&2)==0 ? min.y : max.y,(i&4)==0 ? min.z : max.z)));
            }
            return result;
        }
        void TickAquariumHero(Display d,float dt)
        {
            var h=d.Habitat;
            if(!AquariumHeroVisible(d)) {
                if(h.FishPositionKnown || d.PondletPlacedAge>=0) fxPool?.Cancel(fxCues==null ? default : fxCues.Find(h.TankId));
                d.PondletPlacedAge=-1; ResetAquariumHero(h); return;
            }
            bool occupied=d.SpeciesId=="pondlet" && d.Creature && !string.IsNullOrEmpty(d.FxSpecimenId);
            if(!fxReducedMotion) h.Clock+=Mathf.Max(0,dt);
            var pose=FxAquariumMotion.Habitat(h.Clock,h.Seed,occupied,fxReducedMotion,d.PondletPlacedAge);
            h.Water.localScale=new Vector3(h.WaterRest.x,h.WaterRest.y*pose.WaterScale,h.WaterRest.z);
            Color tint=Color.Lerp(h.WaterColor,new Color(.35f,.83f,.88f,h.WaterColor.a),pose.WaterTint);
            h.WaterProperties.SetColor(AquariumColorId,tint); h.WaterRenderer.SetPropertyBlock(h.WaterProperties);
            for(int i=0;i<h.Plants.Length;i++) if(h.Plants[i])
                h.Plants[i].localRotation=h.PlantRest[i]*Quaternion.Euler(0,0,i==0 ? pose.PlantLeft : pose.PlantRight);
            for(int i=0;i<h.Bubbles.Length;i++) {
                var bubble=h.Bubbles[i]; if(!bubble) continue;
                bool visible=!fxReducedMotion && (!aquariumLow || i==0);
                bubble.gameObject.SetActive(visible); if(!visible) continue;
                FxAquariumMotion.Bubble(i,h.Clock,h.Seed,h.InnerWater,occupied,out var point,out float size);
                bubble.localPosition=point; bubble.localScale=Vector3.one*size;
                Color bubbleTint=Color.Lerp(h.BubbleColors[i],new Color(.68f,.93f,1,.26f),.65f);
                h.BubbleProperties[i].SetColor(AquariumColorId,bubbleTint);
                h.BubbleRenderers[i].SetPropertyBlock(h.BubbleProperties[i]);
            }
            if(!occupied) {
                if(h.FishPositionKnown || d.PondletPlacedAge>=0) fxPool?.Cancel(fxCues.Find(h.TankId));
                h.FishPositionKnown=h.TrailActive=false; h.TrailClock=0; d.PondletPlacedAge=-1; return;
            }
            var station=d.Root.parent;
            Vector3 fish=station.InverseTransformPoint(d.Creature.transform.position);
            Vector3 velocity=h.FishPositionKnown && dt>0 && dt<.25f ? (fish-h.LastFishPosition)/dt : Vector3.zero;
            velocity.y=0;
            h.LastFishPosition=fish; h.FishPositionKnown=true;
            h.TrailActive=!fxReducedMotion && velocity.sqrMagnitude>=.000625f;
            if(h.TrailActive) {
                h.TrailClock-=dt;
                if(h.TrailClock<=0) {
                    h.TrailClock=.05f;
                    if(FxAquariumMotion.WakeAnchor(AquariumFishBounds(d),velocity,h.InnerWater,out var origin) &&
                        FxAquariumMotion.Trail(++h.TrailSequence,origin,velocity,h.InnerWater,aquariumLow,fxReducedMotion,
                        out h.TrailClock,out var local,out var request) && fxPool!=null &&
                        fxPool.EmitParticle(fxCues,fxCues.Find(h.TankId),station,local,request,true)) AquariumTrailEmissionCount++;
                }
            }
            else h.TrailClock=.05f;
            EmitAquariumPlacement(d);
        }
        void EmitAquariumPlacement(Display d)
        {
            var h=d.Habitat;
            if(h==null || h.TankId!="tank-a" || !AquariumHeroVisible(d) || d.PondletPlacedAge<0 ||
                d.SpeciesId!="pondlet" || !d.Creature || fxPool==null) return;
            int count=FxAquariumMotion.PlacementCount(aquariumLow,fxReducedMotion);
            uint complete=(1u<<count)-1;
            if((h.PlacementMask&complete)==complete) return;
            Bounds fishBounds=AquariumFishBounds(d);
            for(int i=0;i<count;i++) {
                uint bit=1u<<i; if((h.PlacementMask&bit)!=0) continue;
                FxAquariumMotion.Placement(i,fishBounds,h.InnerWater,fxReducedMotion,out float delay,out var local,out var request);
                if(d.PondletPlacedAge<delay) continue;
                h.PlacementMask|=bit;
                request.Lifetime=Mathf.Min(request.Lifetime,.52f-d.PondletPlacedAge);
                if(request.Lifetime>0 && fxPool.EmitParticle(fxCues,fxCues.Find(h.TankId),d.Root.parent,local,request,true)) AquariumPlacementEmissionCount++;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Bounds AquariumWaterBoundsForValidation(string id) => displays.TryGetValue(id,out var d) && d.Habitat!=null ? d.Habitat.InnerWater : default;
        public FxOwnerToken AquariumOwnerForValidation(string id) => fxCues==null ? default : fxCues.Find(id);
        public bool AquariumTrailActiveForValidation(string id) => displays.TryGetValue(id,out var d) && d.Habitat!=null && d.Habitat.TrailActive;
        public float AquariumPlacementAgeForValidation(string id) => displays.TryGetValue(id,out var d) ? d.PondletPlacedAge : -1;
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
