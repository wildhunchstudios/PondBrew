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
            public bool Ambient, FullDisplay;
            public MeshRenderer WaterRenderer;
            public MaterialPropertyBlock WaterProperties;
            public Color WaterColor;
            public Bounds InnerWater;
            public readonly MeshRenderer[] BubbleRenderers = new MeshRenderer[4];
            public readonly MaterialPropertyBlock[] BubbleProperties = new MaterialPropertyBlock[4];
            public readonly Color[] BubbleColors = new Color[4];
            public Vector3 LastFishPosition;
            public float TrailClock, ResponseAge = -1;
            public int TrailEmissions;
            public uint TrailSequence, PlacementMask;
            public bool FishPositionKnown, TrailActive, SourceResponse;
            public Bounds ResponseBounds, PreviousBounds;
            public string PreviousSpecimen;
            public Transform FishBoundsRoot;
            public Renderer[] FishRenderers;
        }
        static readonly int AquariumColorId = Shader.PropertyToID("_BaseColor");
        bool aquariumLow, habitatTransferCaptured;
        Station aquariumFocus;
        Display aquariumWakeFirst, aquariumWakeSecond;
        public int PondletPlacementCount { get; private set; }
        public int PondletGlanceCount { get; private set; }
        public int AquariumTrailEmissionCount { get; private set; }
        public int AquariumPlacementEmissionCount { get; private set; }
        public int HabitatTransferCount { get; private set; }
        public int HabitatSourceEmissionCount { get; private set; }
        public int HabitatDestinationEmissionCount { get; private set; }
        public string LastHabitatSourceId { get; private set; }
        public string LastHabitatDestinationId { get; private set; }
        public string LastHabitatSpecimenId { get; private set; }
        public FxCueOrigin LastHabitatOrigin { get; private set; }
        public Vector3 LastHabitatSourceAnchor { get; private set; }
        public Vector3 LastHabitatDestinationAnchor { get; private set; }

        AquariumHabitat BuildAquariumHabitat(Transform root,float seed)
        {
            // Retain the fixed-tank silhouette; its full tilted capsule stays above sand.
            foreach(Transform child in root) if(child.name=="Aquatic frond") {
                child.localScale=new Vector3(.13f,.37f,.065f);
                var point=child.localPosition; point.y=1.43f; child.localPosition=point;
            }
            return BuildContainerHabitat(root,root.GetComponent<Station>().TankId,seed,"Water volume","Tank sand",true,true,false);
        }
        AquariumHabitat BuildContainerHabitat(Transform root,string id,float seed,string waterName,string sandName,
            bool ambient,bool fullDisplay,bool addDetails)
        {
            var h=new AquariumHabitat { Water=root.Find(waterName),TankId=id,Seed=seed,Ambient=ambient,FullDisplay=fullDisplay };
            if(!h.Water) return h;
            h.WaterRest=h.Water.localScale;
            Vector3 min=h.Water.localPosition-h.WaterRest*.5f+Vector3.one*.025f;
            Vector3 max=h.Water.localPosition+h.WaterRest*.5f-Vector3.one*.025f;
            var sand=string.IsNullOrEmpty(sandName) ? null : root.Find(sandName);
            if(sand) min.y=Mathf.Max(min.y,sand.localPosition.y+sand.localScale.y*.5f+.025f);
            h.InnerWater=new Bounds((min+max)*.5f,max-min);
            // The incubator is an ellipsoid, not a rectangular tank. Keep its small
            // transfer response in an inscribed box, clear of the curved sides/top.
            if(id=="incubator") h.InnerWater=new Bounds(h.Water.localPosition,h.WaterRest*.5f);
            if(addDetails) {
                float scale=FxAquariumMotion.ContainerScale(h.InnerWater);
                for(int i=0;i<(fullDisplay?2:0);i++) {
                    float side=i==0?-1:1;
                    var plant=Shape(root,"Aquatic frond",PrimitiveType.Capsule,
                        new Vector3(h.InnerWater.center.x+side*h.InnerWater.extents.x*.77f,min.y+.16f*scale,h.InnerWater.center.z+h.InnerWater.extents.z*.65f),
                        new Vector3(.10f,.16f,.05f)*scale,shell);
                    plant.localRotation=Quaternion.Euler(0,0,side*10);
                }
                for(int i=0;i<(fullDisplay?4:2);i++)
                    Shape(root,"Air bubble",PrimitiveType.Sphere,h.InnerWater.center,Vector3.one*.04f*scale,glass);
            }
            if(!ambient) return h; // Incubator/conditioner retain their existing sole fluid writer.
            h.WaterRenderer=h.Water.GetComponent<MeshRenderer>(); h.WaterProperties=new MaterialPropertyBlock();
            h.WaterColor=h.WaterRenderer.sharedMaterial.GetColor(AquariumColorId);
            int plants=0,bubbles=0;
            foreach(Transform child in root) {
                if(child.name=="Aquatic frond" && plants<2) { h.Plants[plants]=child;h.PlantRest[plants++]=child.localRotation; }
                if(child.name=="Air bubble" && bubbles<4) {
                    int i=bubbles++;h.Bubbles[i]=child;h.BubbleRest[i]=child.localScale;
                    h.BubbleRenderers[i]=child.GetComponent<MeshRenderer>();h.BubbleProperties[i]=new MaterialPropertyBlock();
                    h.BubbleColors[i]=h.BubbleRenderers[i].sharedMaterial.GetColor(AquariumColorId);
                }
            }
            return h;
        }
        void ResetPondletIdentity(Display display)
        {
            display.PondletClock=0;display.PondletPhase=CreatureMotion.IdentityPhase(display.FxSpecimenId);
            display.PondletFocus=display.PondletCooldown=0;display.PondletPlacedAge=display.PondletGlanceAge=-1;
            display.PondletSequence=0;if(display.Creature) display.Creature.SetPondletIdentity(display.FxSpecimenId);
            ResetAquariumHero(display.Habitat);
        }
        public void SetAquariumFocus(Station station) { aquariumFocus=station; }
        public void ClearFishMimic()
        { foreach(var display in displays.Values) if(display.Creature) display.Creature.ClearMimicPose(); }
        void CancelAquariumTransient()
        {
            aquariumFocus=null;habitatTransferCaptured=false;ClearFishMimic();
            foreach(var display in displays.Values) {
                display.PondletFocus=0;display.PondletPlacedAge=display.PondletGlanceAge=-1;ResetAquariumHero(display.Habitat);
            }
        }
        public bool PresentPondletPlacement(string tankId,uint sequence,FxCueOrigin origin=FxCueOrigin.Manual)
        {
            if(!displays.TryGetValue(tankId??"",out var d) || d.Habitat==null || !d.Habitat.FullDisplay ||
                d.SpeciesId!="pondlet" || !d.Creature || string.IsNullOrEmpty(d.FxSpecimenId) || !HabitatResponseVisible(d)) return false;
            if(fxCues==null || !fxCues.Play(fxCues.Find(tankId),FxCueId.PondletPlaced,sequence,origin,.52f,4)) return false;
            d.PondletPlacedAge=0;PondletPlacementCount++;BeginHabitatResponse(d,AquariumFishBounds(d),false);return true;
        }
        // Called only around an existing saved publication, never by ordinary refresh/load.
        // The snapshot owns no stock: matching requires the same identity in a different current container.
        public void CaptureHabitatTransfers()
        {
            habitatTransferCaptured=true;
            foreach(var d in displays.Values) if(d.Habitat!=null) {
                d.Habitat.PreviousSpecimen=d.FxSpecimenId;
                if(d.Creature) d.Habitat.PreviousBounds=AquariumFishBounds(d);
            }
        }
        public void PresentHabitatTransfers(bool saved,uint sequence,FxCueOrigin origin)
        {
            if(!habitatTransferCaptured) return;habitatTransferCaptured=false;
            if(!saved) return;
            foreach(var destination in displays.Values) {
                var to=destination.Habitat;
                if(to==null || !destination.Creature || string.IsNullOrEmpty(destination.FxSpecimenId) ||
                    destination.FxSpecimenId==to.PreviousSpecimen) continue;
                foreach(var source in displays.Values) {
                    var from=source.Habitat;
                    if(source==destination || from==null || from.PreviousSpecimen!=destination.FxSpecimenId ||
                        source.FxSpecimenId==from.PreviousSpecimen || !HabitatResponseVisible(source) || !HabitatResponseVisible(destination)) continue;
                    HabitatTransferCount++;LastHabitatSourceId=from.TankId;LastHabitatDestinationId=to.TankId;
                    LastHabitatSpecimenId=destination.FxSpecimenId;LastHabitatOrigin=origin;
                    BeginHabitatResponse(source,from.PreviousBounds,true);
                    if(!PresentPondletPlacement(to.TankId,sequence,origin)) BeginHabitatResponse(destination,AquariumFishBounds(destination),false);
                    break;
                }
            }
        }
        void BeginHabitatResponse(Display d,Bounds bounds,bool source)
        {
            var h=d.Habitat;h.ResponseAge=0;h.ResponseBounds=bounds;h.SourceResponse=source;h.PlacementMask=0;
            EmitAquariumPlacement(d);
        }
        void PoseResident(Display display, float time, bool reduced)
        {
            var pose = CreatureMotion.Resolve(display.SpeciesId, time, display.PondletPhase, reduced);
            display.Creature.transform.localPosition = pose.Position;
            display.Creature.transform.localRotation = Quaternion.Euler(pose.Angles);
            display.Creature.AnimateSpecies(time, pose.Effort, reduced);
        }

        void TickResident(Display display, float dt)
        {
            if (display.SpeciesId == "pondlet" && display.Habitat != null && display.Habitat.FullDisplay) { TickPondlet(display, dt); return; }
            if (fxEnabled && fxPolicy == FxViewPolicy.Visible && !fxReducedMotion && display.Root.gameObject.activeInHierarchy)
                display.PondletClock += Mathf.Max(0, dt);
            PoseResident(display, display.PondletClock, fxReducedMotion || !fxEnabled);
        }

        void TickPondlet(Display display, float dt)
        {
            bool visible = fxEnabled && fxPolicy == FxViewPolicy.Visible && display.Root.gameObject.activeInHierarchy;
            if (visible)
            {
                if (!fxReducedMotion) display.PondletClock += Mathf.Max(0, dt);
                display.PondletCooldown = Mathf.Max(0, display.PondletCooldown - dt);
                if (display.PondletPlacedAge >= 0) { display.PondletPlacedAge += dt; if (display.PondletPlacedAge >= .52f) display.PondletPlacedAge = -1; }
                if (display.PondletGlanceAge >= 0) { display.PondletGlanceAge += dt; if (display.PondletGlanceAge >= .6f) display.PondletGlanceAge = -1; }
                display.PondletFocus = aquariumFocus && aquariumFocus.transform == display.Root.parent ? display.PondletFocus + dt : 0;
                if (display.PondletFocus >= 1.5f && display.PondletCooldown <= 0 && !fxReducedMotion && display.PondletPlacedAge < 0)
                {
                    var token = fxCues.Find(display.Habitat.TankId);
                    if (fxCues.Play(token, FxCueId.PondletGlance, ++display.PondletSequence, FxCueOrigin.Manual, .6f, 1))
                    {
                        display.PondletGlanceAge = 0; display.PondletCooldown = 8; PondletGlanceCount++;
                        // AD-A signature trick: the kit Pondlet blows a bubble at whoever lingers at its tank.
                        if (dioramaActive && dioramaCamera) DioramaTrickBubble(display.Creature, dioramaCamera.transform.position);
                    }
                }
            }
            var pose = PondletMotion.Resolve(display.PondletClock, display.PondletPhase, fxReducedMotion || !fxEnabled, display.PondletPlacedAge, display.PondletGlanceAge);
            if (display.Creature.IsDiorama)
            {
                bool calm = fxReducedMotion || !fxEnabled;
                if (display.DioramaLarge) pose.Position *= CreatureVisual.DioramaSwimAmplitude;
                // Idle habit: between darts it bobs nose-down.
                if (!calm) pose.Angles.x -= (1 - pose.Effort) * (5 + 3 * Mathf.Sin(display.PondletClock * 2.3f));
                bool trick = display.PondletGlanceAge >= 0;
                display.Creature.SetDioramaFace(trick ? .75f : .2f, 0, trick ? .3f : 0);
                if (dioramaCamera) display.Creature.SetLookTarget(dioramaCamera.transform.position);
            }
            display.Creature.transform.localPosition = pose.Position;
            display.Creature.transform.localRotation = Quaternion.Euler(pose.Angles);
            display.Creature.AnimatePondlet(display.PondletClock, pose.Effort, fxReducedMotion || !fxEnabled, display.PondletGlanceAge);
        }
        bool HabitatResponseVisible(Display d) => fxEnabled && isActiveAndEnabled &&
            (fxPolicy==FxViewPolicy.Visible || fxPolicy==FxViewPolicy.Modal) && d.Root && d.Root.gameObject.activeInHierarchy &&
            d.Habitat!=null && d.Habitat.Water && d.Habitat.Water.gameObject.activeInHierarchy &&
            fxCues!=null && fxCues.IsValid(fxCues.Find(d.Habitat.TankId));
        bool AquariumHeroVisible(Display d) => HabitatResponseVisible(d) && fxPolicy==FxViewPolicy.Visible;
        void TickAquariumHabitat(float dt)
        {
            // Focused aquarium first, then the camera-centered habitats (two/Low-one).
            // Rank stable water centers: adjacent slots share a browse position, and a
            // neighboring moving fish must not steal the looked-at compartment's budget.
            // Cached details need no pool slots; wakes retain the hero action partition.
            aquariumWakeFirst=aquariumWakeSecond=null;float first=float.MaxValue,second=float.MaxValue;
            Transform view=ToolRoot ? ToolRoot.parent : null;
            foreach(var d in displays.Values) if(d.Habitat!=null && d.Habitat.FullDisplay && d.Creature && AquariumHeroVisible(d)) {
                Vector3 center=d.Root.parent.TransformPoint(d.Habitat.InnerWater.center);
                Vector3 offset=center-(view?view.position:transform.position);
                float alignment=view?Mathf.Clamp01(Vector3.Dot(view.forward,offset.normalized)):1;
                if(view && alignment<.1f) continue;
                bool focused=aquariumFocus && aquariumFocus.transform==d.Root.parent;
                float distance=focused?-1:16*(1-alignment)+offset.sqrMagnitude*.01f;
                if(distance<first) {second=first;aquariumWakeSecond=aquariumWakeFirst;first=distance;aquariumWakeFirst=d;}
                else if(distance<second) {second=distance;aquariumWakeSecond=d;}
            }
            foreach(var d in displays.Values) if(d.Habitat!=null) TickAquariumHero(d,dt);
        }
        void ResetAquariumHero(AquariumHabitat h)
        {
            if(h==null) return;
            h.FishPositionKnown=h.TrailActive=false;h.TrailClock=0;h.PlacementMask=0;h.ResponseAge=-1;
            if(!h.Ambient) return;
            if(h.Water) h.Water.localScale=h.WaterRest;
            if(h.WaterRenderer) h.WaterRenderer.SetPropertyBlock(null);
            for(int i=0;i<h.Plants.Length;i++) if(h.Plants[i]) h.Plants[i].localRotation=h.PlantRest[i];
            for(int i=0;i<h.Bubbles.Length;i++) if(h.Bubbles[i]) {
                h.Bubbles[i].localScale=h.BubbleRest[i];h.Bubbles[i].gameObject.SetActive(false);
                if(h.BubbleRenderers[i]) h.BubbleRenderers[i].SetPropertyBlock(null);
            }
        }
        void CancelAquariumOwner(FxOwnerToken owner)
        {
            if(fxCues==null || !owner.IsDefined) return;
            foreach(var d in displays.Values) if(d.Habitat!=null && owner.Equals(fxCues.Find(d.Habitat.TankId))) {
                ResetAquariumHero(d.Habitat);d.PondletPlacedAge=d.PondletGlanceAge=-1;return;
            }
        }
        Bounds AquariumFishBounds(Display d)
        {
            var h=d.Habitat;var creature=d.Creature.transform;var station=d.Root.parent;
            if(h.FishBoundsRoot!=creature) {h.FishBoundsRoot=creature;h.FishRenderers=creature.GetComponentsInChildren<Renderer>();}
            var result=new Bounds(station.InverseTransformPoint(creature.position),Vector3.zero);
            foreach(var renderer in h.FishRenderers) if(renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) {
                var bounds=renderer.bounds;var min=bounds.min;var max=bounds.max;
                for(int i=0;i<8;i++) result.Encapsulate(station.InverseTransformPoint(new Vector3(
                    (i&1)==0?min.x:max.x,(i&2)==0?min.y:max.y,(i&4)==0?min.z:max.z)));
            }
            return result;
        }
        void TickAquariumHero(Display d,float dt)
        {
            var h=d.Habitat;
            if(!HabitatResponseVisible(d)) {ResetAquariumHero(h);return;}
            bool moving=AquariumHeroVisible(d) && !fxReducedMotion;
            bool occupied=d.Creature && !string.IsNullOrEmpty(d.FxSpecimenId);
            if(moving) h.Clock+=Mathf.Max(0,dt);
            if(h.ResponseAge>=0) {h.ResponseAge+=Mathf.Max(0,dt);if(h.ResponseAge>=.52f) {h.ResponseAge=-1;d.PondletPlacedAge=-1;}}
            if(h.Ambient) {
                var pose=FxAquariumMotion.Habitat(h.Clock,h.Seed,occupied,!moving,h.ResponseAge);
                float strength=h.FullDisplay?1:.35f;
                h.Water.localScale=new Vector3(h.WaterRest.x,h.WaterRest.y*(1+(pose.WaterScale-1)*strength),h.WaterRest.z);
                h.WaterProperties.SetColor(AquariumColorId,Color.Lerp(h.WaterColor,new Color(.35f,.83f,.88f,h.WaterColor.a),pose.WaterTint*strength));
                h.WaterRenderer.SetPropertyBlock(h.WaterProperties);
                for(int i=0;i<h.Plants.Length;i++) if(h.Plants[i]) h.Plants[i].localRotation=h.PlantRest[i]*Quaternion.Euler(0,0,i==0?pose.PlantLeft:pose.PlantRight);
                for(int i=0;i<h.Bubbles.Length;i++) {
                    var bubble=h.Bubbles[i];if(!bubble) continue;
                    bool visible=moving && (!aquariumLow || i==0);bubble.gameObject.SetActive(visible);if(!visible) continue;
                    FxAquariumMotion.ScaledBubble(i,h.Clock,h.Seed,h.InnerWater,occupied,out var point,out float size);
                    bubble.localPosition=point;bubble.localScale=Vector3.one*size*(h.FullDisplay?1:.65f);
                    h.BubbleProperties[i].SetColor(AquariumColorId,Color.Lerp(h.BubbleColors[i],new Color(.68f,.93f,1,.26f),.65f));
                    h.BubbleRenderers[i].SetPropertyBlock(h.BubbleProperties[i]);
                }
            }
            // Empty sources retain the short committed transfer response, but never a fish wake.
            if(!occupied || !moving || !h.FullDisplay) {h.FishPositionKnown=h.TrailActive=false;h.TrailClock=0;EmitAquariumPlacement(d);return;}
            var station=d.Root.parent;Vector3 fish=station.InverseTransformPoint(d.Creature.transform.position);
            Vector3 velocity=h.FishPositionKnown && dt>0 && dt<.25f?(fish-h.LastFishPosition)/dt:Vector3.zero;velocity.y=0;
            h.LastFishPosition=fish;h.FishPositionKnown=true;
            h.TrailActive=(d==aquariumWakeFirst || (!aquariumLow && d==aquariumWakeSecond)) && velocity.sqrMagnitude>=.0001f;
            if(h.TrailActive) {
                h.TrailClock-=dt;
                if(h.TrailClock<=0) {
                    h.TrailClock=.08f;
                    if(FxAquariumMotion.ScaledWakeAnchor(AquariumFishBounds(d),velocity,h.InnerWater,out var origin) &&
                        FxAquariumMotion.ScaledTrail(++h.TrailSequence,origin,velocity,h.InnerWater,aquariumLow,fxReducedMotion,
                        out h.TrailClock,out var local,out var request) && fxPool!=null &&
                        fxPool.EmitParticle(fxCues,fxCues.Find(h.TankId),station,local,request,true)) {AquariumTrailEmissionCount++;h.TrailEmissions++;}
                }
            } else h.TrailClock=.08f;
            EmitAquariumPlacement(d);
        }
        void EmitAquariumPlacement(Display d)
        {
            var h=d.Habitat;
            if(h==null || !HabitatResponseVisible(d) || h.ResponseAge<0 || fxPool==null) return;
            int count=h.SourceResponse || !h.FullDisplay?1:FxAquariumMotion.PlacementCount(aquariumLow,fxReducedMotion);
            for(int i=0;i<count;i++) {
                uint bit=1u<<i;if((h.PlacementMask&bit)!=0) continue;
                FxAquariumMotion.ScaledPlacement(i,h.ResponseBounds,h.InnerWater,fxReducedMotion,out float delay,out var local,out var request);
                if(h.ResponseAge<delay) continue;h.PlacementMask|=bit;
                request.Lifetime=Mathf.Min(request.Lifetime,.52f-h.ResponseAge);
                if(request.Lifetime>0 && fxPool.EmitParticle(fxCues,fxCues.Find(h.TankId),d.Root.parent,local,request,true)) {
                    if(h.SourceResponse) {HabitatSourceEmissionCount++;LastHabitatSourceAnchor=local;}
                    else {HabitatDestinationEmissionCount++;AquariumPlacementEmissionCount++;LastHabitatDestinationAnchor=local;}
                }
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float HabitatResponseAgeForValidation(string id) => displays.TryGetValue(id,out var d) && d.Habitat!=null ? d.Habitat.ResponseAge : -1;
        public int AquariumTrailEmissionCountForValidation(string id) => displays.TryGetValue(id,out var d) && d.Habitat!=null ? d.Habitat.TrailEmissions : 0;
        public bool TryGetHabitatForValidation(string id,out Transform space,out Bounds water,out bool ambient,out float clock)
        {
            space=null;water=default;ambient=false;clock=0;
            if(!displays.TryGetValue(id,out var d) || d.Habitat==null || !d.Root) return false;
            space=d.Root.parent;water=d.Habitat.InnerWater;ambient=d.Habitat.Ambient;clock=d.Habitat.Clock;return true;
        }
        public float CreatureAgeForValidation(string id) => displays.TryGetValue(id, out var d) ? d.PondletClock : -1;
        public float CreaturePhaseForValidation(string id) => displays.TryGetValue(id, out var d) ? d.PondletPhase : -1;
        public bool TryGetCreatureForValidation(string id, out Transform interiorSpace, out Bounds interior,
            out CreatureVisual creature, out string speciesId, out string specimenId)
        {
            interiorSpace = null; interior = default; creature = null; speciesId = specimenId = null;
            if (!displays.TryGetValue(id, out var d) || d.Incubator || !d.Root) return false;
            interiorSpace = d.Root.parent; interior = d.Interior; creature = d.Creature;
            speciesId = d.SpeciesId; specimenId = d.FxSpecimenId; return true;
        }
        public void PreviewCreatureForValidation(string id, float time, bool reduced)
        {
            if (displays.TryGetValue(id, out var d) && d.Creature && !d.Incubator) PoseResident(d, time, reduced);
        }


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
