using UnityEngine;
namespace EvolutionShop
{
    public enum FxCustomerReaction { None, Accepted, Refused, Checkout }
    public readonly struct FxCustomerPose
    {
        public readonly Vector3 LeftLegEuler, RightLegEuler, LeftArmEuler, RightArmEuler, UpperEuler;
        public FxCustomerPose(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e)
        { LeftLegEuler=a; RightLegEuler=b; LeftArmEuler=c; RightArmEuler=d; UpperEuler=e; }
    }
    public readonly struct FxCustomerSnapshot
    {
        public readonly float GaitPhase, TravelDistance, Speed, MovingBlend, ReactionAge, Turn;
        public readonly int ReactionCount, SampleCount;
        public readonly FxCustomerReaction Reaction;
        public readonly bool Paused, Reduced;
        public FxCustomerSnapshot(float gait,float distance,float speed,float moving,float age,float turn,int reactions,int samples,FxCustomerReaction reaction,bool paused,bool reduced)
        { GaitPhase=gait; TravelDistance=distance; Speed=speed; MovingBlend=moving; ReactionAge=age; Turn=turn;
            ReactionCount=reactions; SampleCount=samples; Reaction=reaction; Paused=paused; Reduced=reduced; }
    }
    /// <summary>Deterministic local poses; no navigation, transaction or saved state.</summary>
    public static class FxCustomerMotion
    {
        public static float StrideLength(string archetype) => archetype=="bargain"?1.16f:archetype=="collector"?1.24f:archetype=="premium"?1.42f:1.30f;
        public static float ReactionDuration(FxCustomerReaction reaction) => reaction==FxCustomerReaction.Checkout?1.25f:reaction==FxCustomerReaction.Refused? .85f:reaction==FxCustomerReaction.Accepted? .75f:0f;
        static float Finite(float value) => float.IsNaN(value)||float.IsInfinity(value)?0f:value;
        public static FxCustomerPose Sample(string archetype,CustomerPhase phase,float gaitPhase,float moving,float idleTime,
            FxCustomerReaction reaction,float reactionAge,float turn,bool reduced)
        {
            moving=Mathf.Clamp01(Finite(moving)); gaitPhase=Finite(gaitPhase); idleTime=Finite(idleTime); turn=Mathf.Clamp(Finite(turn),-1,1);
            bool bargain=archetype=="bargain",collector=archetype=="collector",premium=archetype=="premium";
            float stride=Mathf.Sin(gaitPhase)*moving, legAngle=premium?20f:bargain?22f:23f;
            var leftLeg=new Vector3(stride*legAngle,0,0); var rightLeg=new Vector3(-stride*legAngle,0,0);
            var leftArm=new Vector3(-stride*(premium?11f:17f),0,-7); var rightArm=new Vector3(stride*(premium?11f:17f),0,7);
            // Idle/reaction movement stays above the hips; stationary feet never follow a clock.
            float calm=reduced?0f:1f-moving;
            float rhythm=Mathf.Sin(idleTime*(bargain?1.18f:premium? .70f:.88f));
            var upper=new Vector3(calm*rhythm*.65f,reduced?0:turn*6f,reduced?0:-turn*2f*moving);
            if(phase==CustomerPhase.Browsing) {
                upper.x+=calm*(collector?4.2f:bargain?3.4f:premium?1.6f:2.5f);
                upper.y+=calm*Mathf.Sin(idleTime*.8f)*(collector?6f:4f);
                rightArm.x-=calm*(bargain?32f:collector?23f:premium?14f:12f);
            } else if(phase==CustomerPhase.Queued) {
                upper.y+=calm*Mathf.Sin(idleTime*.43f)*(premium?1.5f:3f);
                leftArm.x-=calm*(collector?18f:premium?9f:4f); rightArm.x-=calm*(bargain?17f:collector?18f:5f);
            }
            float duration=ReactionDuration(reaction),age=Mathf.Max(0,Finite(reactionAge));
            float beat=duration>0&&age<duration?Mathf.Sin(Mathf.PI*age/duration):0;
            beat*=reduced? .28f:1f;
            if(reaction==FxCustomerReaction.Accepted) {
                upper.x+=beat*(premium?3f:6f);
                rightArm.x-=beat*(bargain?42f:collector?36f:premium?21f:30f); rightArm.z+=beat*(premium?5f:14f);
                if(collector)leftArm.x-=beat*26f;
            } else if(reaction==FxCustomerReaction.Refused) {
                upper.x-=beat*(premium?2f:4f); upper.y+=beat*Mathf.Sin(age*13f)*(premium?3f:6f);
                leftArm.x-=beat*(bargain?32f:20f); rightArm.x-=beat*(premium?18f:30f);
                leftArm.z-=beat*12f; rightArm.z+=beat*12f;
            } else if(reaction==FxCustomerReaction.Checkout) {
                upper.x+=beat*(premium?5f:3f); rightArm.x-=beat*(premium?32f:58f); rightArm.z+=beat*(premium?8f:21f);
                if(collector)leftArm.x-=beat*32f; if(bargain)upper.z+=beat*Mathf.Sin(age*8f)*2f;
            }
            return new FxCustomerPose(leftLeg,rightLeg,leftArm,rightArm,upper);
        }
    }
}