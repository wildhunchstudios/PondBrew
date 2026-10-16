using UnityEngine;

namespace EvolutionShop
{
    public struct FxAquariumPose
    {
        public float WaterScale, WaterTint, PlantLeft, PlantRight;
    }
    /// <summary>Cosmetic recipes inside the existing water prism. No specimen or sale authority.</summary>
    public static class FxAquariumMotion
    {
        // Map the admitted fixed-tank recipes uniformly into each actual water prism.
        // Uniform sizing keeps rings round and smaller residents visually primary.
        public static float ContainerScale(Bounds water) => Mathf.Clamp(Mathf.Min(water.size.x / 1.85f,
            water.size.y / .895f, water.size.z / 1.15f), .15f, 1f);
        static Bounds ScaledBounds(Bounds value, Vector3 center, float scale) => new Bounds((value.center-center)/scale,value.size/scale);
        static void ScaleParticle(float scale, ref FxParticleRequest request)
        { request.Size*=scale; request.EndSize*=scale; request.End*=scale; request.Bend*=scale; request.Velocity*=scale; request.Radius*=scale; }
        public static void ScaledBubble(int index,float time,float seed,Bounds water,bool occupied,out Vector3 position,out float size)
        {
            float scale=ContainerScale(water);
            Bubble(index,time,seed,new Bounds(Vector3.zero,water.size/scale),occupied,out position,out size);
            position=water.center+position*scale; size*=scale;
        }
        public static bool ScaledWakeAnchor(Bounds fish,Vector3 velocity,Bounds water,out Vector3 origin)
        {
            float scale=ContainerScale(water);
            bool result=WakeAnchor(ScaledBounds(fish,water.center,scale),velocity/scale,new Bounds(Vector3.zero,water.size/scale),out origin);
            origin=water.center+origin*scale; return result;
        }
        public static bool ScaledTrail(uint index,Vector3 origin,Vector3 velocity,Bounds water,bool low,bool reduced,
            out float interval,out Vector3 local,out FxParticleRequest request)
        {
            float scale=ContainerScale(water);
            bool result=Trail(index,(origin-water.center)/scale,velocity/scale,new Bounds(Vector3.zero,water.size/scale),low,reduced,out interval,out local,out request);
            local=water.center+local*scale; ScaleParticle(scale,ref request); return result;
        }
        public static void ScaledPlacement(int index,Bounds fish,Bounds water,bool reduced,
            out float delay,out Vector3 local,out FxParticleRequest request)
        {
            float scale=ContainerScale(water);
            Placement(index,ScaledBounds(fish,water.center,scale),new Bounds(Vector3.zero,water.size/scale),reduced,out delay,out local,out request);
            local=water.center+local*scale; ScaleParticle(scale,ref request);
        }
        public const float TrailRadius = .08f;
        public const float WakeGap = .025f;
        static float Noise(uint n, uint salt)
        {
            n = unchecked(n * 747796405u + salt * 2891336453u + 277803737u);
            n = unchecked((n ^ (n >> 16)) * 2246822519u);
            return (n & 65535u) / 65535f;
        }
        public static FxAquariumPose Habitat(float time, float seed, bool occupied, bool reduced, float placementAge)
        {
            if (reduced) return new FxAquariumPose { WaterScale = 1 };
            float t = time + seed;
            float ripple = placementAge >= 0 && placementAge < .52f ? Mathf.Sin(placementAge / .52f * Mathf.PI) : 0;
            float strength = occupied ? 1 : .55f;
            return new FxAquariumPose {
                WaterScale = 1 - (.5f + .5f*Mathf.Sin(t*.73f))*.010f*strength - ripple*.008f,
                WaterTint = (.5f + .5f*Mathf.Sin(t*.73f))*.14f*strength + ripple*.15f,
                PlantLeft = (Mathf.Sin(t*.91f)*3.2f + Mathf.Sin(t*1.73f+.6f)*1.1f)*strength + ripple*2,
                PlantRight = (Mathf.Sin(t*.78f+1.9f)*3.8f + Mathf.Sin(t*1.41f)*.8f)*strength - ripple*2
            };
        }
        public static void Bubble(int index, float time, float seed, Bounds water, bool occupied, out Vector3 position, out float size)
        {
            float a = Noise((uint)index, 1), b = Noise((uint)index, 2), c = Noise((uint)index, 3);
            float progress = Mathf.Repeat(time*(.13f+a*.055f) + b + seed*.071f, 1);
            float fade = Mathf.Clamp01(Mathf.Min(progress,1-progress)/.14f);
            size = (.065f+c*.045f)*fade*(occupied ? 1 : .8f);
            float side = index % 2 == 0 ? -1 : 1;
            float x = side*(.62f+.10f*b) + Mathf.Sin(progress*5.2f+a*7)*.055f;
            position = new Vector3(x, Mathf.Lerp(water.min.y+.09f,water.max.y-.075f,progress), .19f+.19f*c+Mathf.Sin(progress*4+a*8)*.05f);
            position = Clamp(position, water, size*.5f);
        }
        static Vector3 Clamp(Vector3 point, Bounds water, float margin)
        {
            var min = water.min + Vector3.one*margin; var max = water.max - Vector3.one*margin;
            return new Vector3(Mathf.Clamp(point.x,min.x,max.x),Mathf.Clamp(point.y,min.y,max.y),Mathf.Clamp(point.z,min.z,max.z));
        }
        static void Contain(Bounds water, ref Vector3 local, Vector3 target, Vector3 bend, ref FxParticleRequest request)
        {
            float margin = Mathf.Max(request.Size,request.EndSize)*.5f + .006f;
            local = Clamp(local,water,margin);
            target = Clamp(target,water,margin);
            // Quadratic Bezier convex hull: constrain both endpoints and its control point.
            Vector3 middle = (local+target)*.5f;
            Vector3 control = Clamp(middle+bend*2,water,margin);
            request.End = target-local; request.Bend = (control-middle)*.5f;
        }
        public static bool WakeAnchor(Bounds actualFish, Vector3 velocity, Bounds water, out Vector3 origin)
        {
            origin = actualFish.center;
            if (!FxParticleRequest.Finite(velocity)) return false;
            velocity.y = 0;
            if (velocity.sqrMagnitude < .000625f) return false;
            Vector3 behind = -velocity.normalized;
            Vector3 extent = actualFish.extents;
            float support = Mathf.Abs(behind.x)*extent.x + Mathf.Abs(behind.z)*extent.z;
            // Clear the current body AND animated tail, rather than drawing bubbles through the fish.
            origin += behind*(support+TrailRadius+WakeGap) + Vector3.up*.04f;
            float margin = TrailRadius+.006f;
            Vector3 min = water.min+Vector3.one*margin, max = water.max-Vector3.one*margin;
            // Near the glass, wait for room instead of clamping the outlet back into the silhouette.
            return origin.x>=min.x && origin.x<=max.x && origin.y>=min.y && origin.y<=max.y && origin.z>=min.z && origin.z<=max.z;
        }
        public static bool Trail(uint index, Vector3 origin, Vector3 velocity, Bounds water, bool low, bool reduced,
            out float interval, out Vector3 local, out FxParticleRequest request)
        {
            float a = Noise(index, 4), b = Noise(index, 5), c = Noise(index, 6);
            interval = (low ? .25f : .13f)+a*(low ? .13f : .09f);
            local = origin; request = default;
            if (reduced || !FxParticleRequest.Finite(velocity)) return false;
            velocity.y = 0;
            if (velocity.sqrMagnitude < .000625f) return false;
            Vector3 behind = -velocity.normalized;
            request = FxParticleRequest.Create(FxParticleShape.Bubble,new Color(.62f+b*.16f,.96f,1,1));
            request.Motion = FxParticleMotion.Curve; request.Lifetime = .65f+c*.28f;
            request.Size = .11f+b*.05f; request.EndSize = .035f+a*.025f;
            Vector3 target = local + behind*(.045f+a*.055f) + new Vector3((b-.5f)*.035f,.14f+c*.10f,(a-.5f)*.045f);
            Contain(water,ref local,target,new Vector3((c-.5f)*.055f,.018f,(b-.5f)*.045f),ref request);
            return true;
        }
        public static int PlacementCount(bool low, bool reduced) => reduced ? 1 : low ? 3 : 6;
        public static void Placement(int index, Bounds actualFish, Bounds water, bool reduced,
            out float delay, out Vector3 local, out FxParticleRequest request)
        {
            bool ring = index < 2;
            delay = reduced ? 0 : ring ? index*.12f : .03f+(index-2)*.045f;
            Vector3 center = actualFish.center;
            local = new Vector3(center.x+(index==1 ? .07f : -.035f),water.max.y-.018f,actualFish.min.z-.10f);
            request = FxParticleRequest.Create(ring ? FxParticleShape.Ring : FxParticleShape.Bubble,new Color(.72f,.97f,1,1));
            request.Motion = FxParticleMotion.Curve; request.Lifetime = .50f-delay;
            request.Size = reduced ? .12f : ring ? .20f+index*.06f : .12f+Noise((uint)index,7)*.04f;
            request.EndSize = reduced ? .12f : ring ? .30f : .045f;
            request.SurfaceAligned = ring;
            if (ring) {
                // A horizontal ring has no vertical radius; keep it just below the real water surface.
                float margin = Mathf.Max(request.Size,request.EndSize)*.5f+.006f;
                local.x = Mathf.Clamp(local.x,water.min.x+margin,water.max.x-margin);
                local.z = Mathf.Clamp(local.z,water.min.z+margin,water.max.z-margin);
            }
            else {
                // Stagger a loose water response around the actual silhouette, below the lid.
                float side = index%2==0 ? -1 : 1;
                float x = index<4 ? center.x+side*(actualFish.extents.x+.10f) : center.x+side*(index==4 ? .16f : .06f);
                float y = index<4 ? center.y+.035f+(index-2)*.045f : actualFish.max.y+.095f+(index-4)*.025f;
                local = new Vector3(x,y,actualFish.min.z-.10f);
                Contain(water,ref local,local+new Vector3(side*.035f,.14f,0),new Vector3(side*.025f,.025f,.025f),ref request);
            }
        }
    }
}
