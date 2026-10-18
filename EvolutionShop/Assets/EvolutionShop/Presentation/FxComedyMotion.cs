using UnityEngine;
namespace EvolutionShop
{
    public readonly struct FxComedyPose
    {
        public readonly Vector3 LeftArmEuler,RightArmEuler,HeadEuler,UpperEuler;
        public readonly float Envelope,Contact;
        public FxComedyPose(Vector3 left,Vector3 right,Vector3 head,Vector3 upper,float envelope,float contact)
        {LeftArmEuler=left;RightArmEuler=right;HeadEuler=head;UpperEuler=upper;Envelope=envelope;Contact=contact;}
    }
    public readonly struct FxComedySnapshot
    {
        public readonly ComedyKind Kind;
        public readonly int Variant,EmissionCount,FrameCount,BeatFrameCount;
        public readonly uint Sequence;
        public readonly float Elapsed,Realtime,BeatRealtime;
        public readonly bool BeatEmitted,Active,Reduced;
        public readonly Vector3 SourcePosition;
        public FxComedySnapshot(ComedyKind kind,int variant,uint sequence,float elapsed,bool emitted,int count,
            bool active,bool reduced,Vector3 position,int frame,float realtime,int beatFrame,float beatRealtime)
        {Kind=kind;Variant=variant;Sequence=sequence;Elapsed=elapsed;BeatEmitted=emitted;EmissionCount=count;
            Active=active;Reduced=reduced;SourcePosition=position;FrameCount=frame;Realtime=realtime;BeatFrameCount=beatFrame;BeatRealtime=beatRealtime;}
    }
    // Pure local pose timing. Existing ShopComedy remains the sole incident/cooldown authority.
    public static class FxComedyMotion
    {
        public static float SlapContactSeconds(bool reduced)=>reduced? .06f:.10f;
        public static float SlapDuration(bool reduced)=>reduced? .32f:.75f;
        // Every local gesture has settled within the existing 2.6-second incident window.
        static float Finite(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,0,2.6f);
        static float Beat(float age,float attack,float hold,float release)
        {
            if(age<=0)return 0;
            if(age<attack)return Mathf.SmoothStep(0,1,age/attack);
            if(age<attack+hold)return 1;
            return 1-Mathf.SmoothStep(0,1,(age-attack-hold)/release);
        }
        public static FxComedyPose Sample(ComedyKind kind,float elapsed,bool reduced,int variant)
        {
            elapsed=Finite(elapsed);
            var left=new Vector3(0,0,-7);var right=new Vector3(0,0,7);
            Vector3 head=Vector3.zero,upper=Vector3.zero;
            float comfort=reduced? .22f:1,sign=(variant&1)==0?1:-1;
            float age=elapsed-ShopComedy.ReactionDelaySeconds;
            float envelope=Beat(age,.14f,.18f,reduced? .42f:.90f),contact=0;
            float answer=Mathf.Sin(Mathf.Max(0,age)*8)*envelope;
            if(kind==ComedyKind.Slap){
                age=elapsed-SlapContactSeconds(reduced);
                float span=SlapDuration(reduced)-SlapContactSeconds(reduced);
                envelope=age<0?0:Mathf.Clamp01(1-age/span);
                contact=age>=0?Mathf.Clamp01(1-age/.12f):0;
                float recoil=age<0?0:Mathf.Sin(Mathf.Min(1,age/span)*Mathf.PI*2.25f)*envelope;
                upper=new Vector3(0,-8*contact*comfort,(-10*contact+5*recoil)*comfort);
                head=new Vector3(2*contact,0,(-9*contact+4*recoil)*comfort);
            }else if(kind==ComedyKind.Fart){
                right=new Vector3(-82*envelope*comfort,0,7-13*envelope*comfort);
                head=new Vector3(3*envelope,sign*13*envelope*comfort,2*answer*comfort);
                upper=new Vector3(0,-sign*5*envelope*comfort,2*answer*comfort);
            }else if(kind==ComedyKind.Complaint){
                left=new Vector3(-42*envelope*comfort,0,-7-13*envelope*comfort);
                right=new Vector3(-57*envelope*comfort,0,7+11*envelope*comfort);
                head=new Vector3(4*envelope,sign*8*answer*comfort,0);
                upper=new Vector3(0,sign*4*envelope*comfort,2*answer*comfort);
            }else if(kind==ComedyKind.WorkerCelebration){
                left=new Vector3(-78*envelope*comfort,0,-7-10*envelope*comfort);
                right=new Vector3(-94*envelope*comfort,0,7+8*envelope*comfort);
                head=new Vector3(-5*envelope*comfort,sign*5*answer*comfort,4*answer*comfort);
            }else if(kind==ComedyKind.Mimic){
                right=new Vector3(-55*envelope*comfort,0,7+16*envelope*comfort);
                head=new Vector3(4*answer*comfort,sign*10*envelope*comfort,0);
                upper=new Vector3(0,sign*5*answer*comfort,2*answer*comfort);
            }else if(kind==ComedyKind.Belch){
                head=new Vector3(-6*envelope*comfort,0,3*answer*comfort);
                upper=new Vector3(0,0,2*answer*comfort);
            }
            return new FxComedyPose(left,right,head,upper,envelope,contact);
        }
        public static void SampleSlapHand(float elapsed,bool reduced,bool whiff,out Vector3 position,out Quaternion rotation,out Vector3 scale)
        {
            elapsed=Finite(elapsed);
            float duration=reduced? .18f:.30f,hit=SlapContactSeconds(reduced);
            float reach=elapsed<hit?Mathf.SmoothStep(0,1,elapsed/hit):1-Mathf.SmoothStep(0,1,(elapsed-hit)/(duration-hit));
            Vector3 start=reduced?new Vector3(.38f,-.26f,.52f):new Vector3(.58f,-.24f,.58f);
            Vector3 contact=reduced?new Vector3(.30f,-.20f,.55f):new Vector3(.10f,-.02f,.60f);
            if(whiff)contact=Vector3.Lerp(start,contact,.60f)+new Vector3(0,-.035f,-.02f);
            position=Vector3.Lerp(start,contact,reach);
            rotation=reduced?Quaternion.Euler(0,-12,-15+reach*8):Quaternion.Euler(10*(1-reach),Mathf.Lerp(-35,12,reach),Mathf.Lerp(-55,25,reach));
            scale=Vector3.one*(reduced? .60f:.70f);
        }
    }
}
