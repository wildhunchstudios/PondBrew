using UnityEngine;
namespace EvolutionShop
{
    public enum FxWorkerActivity { Idle, Walk, Checkout, Pickup, Carry, Place, Measure, Dose, Start, Check }
    public enum FxWorkerEvent { None, Pickup, Dose, Place, Checkout, Refused }
    public readonly struct FxWorkerPose
    {
        public readonly Vector3 LeftLegEuler,RightLegEuler,LeftArmEuler,RightArmEuler,HeadEuler,ParcelPosition;
        public readonly bool ProxyVisible;
        public FxWorkerPose(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 e,Vector3 parcel,bool visible)
        {LeftLegEuler=a;RightLegEuler=b;LeftArmEuler=c;RightArmEuler=d;HeadEuler=e;ParcelPosition=parcel;ProxyVisible=visible;}
    }
    public readonly struct FxWorkerSnapshot
    {
        public readonly string WorkerId,JobId,ProxyIdentity,EventJobId;
        public readonly FxWorkerActivity Activity;
        public readonly FxWorkerEvent EventKind;
        public readonly float Progress,GaitPhase,TravelDistance,Speed,MovingBlend,EventAge;
        public readonly int EventCount,SampleCount,ProxyCount;
        public readonly bool Carrying,ProxyVisible,Paused,Reduced;
        public FxWorkerSnapshot(string worker,string job,string identity,string eventJob,FxWorkerActivity activity,FxWorkerEvent kind,
            float progress,float gait,float distance,float speed,float moving,float age,int events,int samples,bool carrying,bool visible,bool paused,bool reduced)
        {WorkerId=worker;JobId=job;ProxyIdentity=identity;EventJobId=eventJob;Activity=activity;EventKind=kind;Progress=progress;
            GaitPhase=gait;TravelDistance=distance;Speed=speed;MovingBlend=moving;EventAge=age;EventCount=events;SampleCount=samples;
            Carrying=carrying;ProxyVisible=visible;ProxyCount=visible?1:0;Paused=paused;Reduced=reduced;}
    }
    /// <summary>In-place employee poses only. Timers, claims and all commands remain with their existing owners.</summary>
    public static class FxWorkerMotion
    {
        public const float StrideLength=1.30f;
        public static float EventDuration(FxWorkerEvent kind)=>kind==FxWorkerEvent.None?0:kind==FxWorkerEvent.Refused? .55f:.70f;
        static float Finite(float x)=>float.IsNaN(x)||float.IsInfinity(x)?0:x;
        public static FxWorkerPose Sample(FxWorkerActivity activity,float progress,float gaitPhase,float moving,float idleTime,
            FxWorkerEvent eventKind,float eventAge,bool carrying,bool reduced)
        {
            progress=Mathf.Clamp01(Finite(progress));moving=Mathf.Clamp01(Finite(moving));gaitPhase=Finite(gaitPhase);idleTime=Finite(idleTime);
            float swing=Mathf.Sin(gaitPhase)*moving;
            var leftLeg=new Vector3(swing*23,0,0);var rightLeg=new Vector3(-swing*23,0,0);
            var leftArm=new Vector3(-swing*16,0,-7);var rightArm=new Vector3(swing*16,0,7);
            var head=new Vector3(0,reduced?0:Mathf.Sin(idleTime*.7f)*3f*(1-moving),0);
            var parcel=new Vector3(0,.91f,.38f);
            float comfort=reduced? .35f:1f,beat=Mathf.Sin(progress*Mathf.PI);
            if(activity==FxWorkerActivity.Checkout){
                // Keep the service baseline above the counter; comfort reduces only the small hand beat.
                leftArm=new Vector3(-98,0,-7);
                rightArm=new Vector3(-(101+Mathf.Sin(progress*Mathf.PI*4)*3*comfort),0,9);
                head.x=(3+beat*5)*comfort;
            }else if(activity==FxWorkerActivity.Pickup){
                float reach=Mathf.SmoothStep(0,1,progress);
                leftArm=new Vector3(-67*reach,0,-7*(1-reach));rightArm=new Vector3(-67*reach,0,7*(1-reach));head.x=beat*8*comfort;
            }else if(activity==FxWorkerActivity.Measure){
                leftArm=new Vector3(-48*comfort,0,-5);rightArm=new Vector3(-35*comfort,0,7);head.x=6*comfort;head.y=-4*comfort;
            }else if(activity==FxWorkerActivity.Dose){
                leftArm=new Vector3(-36*comfort,0,-5);rightArm=new Vector3(-(48+beat*14)*comfort,0,5);head.x=5*comfort;
            }else if(activity==FxWorkerActivity.Start){
                leftArm=new Vector3(-24*comfort,0,-5);rightArm=new Vector3(-(30+beat*18)*comfort,0,5);head.x=3*comfort;
            }else if(activity==FxWorkerActivity.Check){head.x=(3+Mathf.Sin(idleTime)*2)*comfort;rightArm.x=-15*comfort;}
            if(carrying){
                // Both mittens and the empty proxy use the same grip angle. Carrying has
                // no arm swing or box bob, including a stationary door wait.
                float angle=-67+(activity==FxWorkerActivity.Place?Mathf.SmoothStep(0,1,progress)*9:0);
                leftArm=new Vector3(angle,0,0);rightArm=new Vector3(angle,0,0);
                float a=angle*Mathf.Deg2Rad,b=-67*Mathf.Deg2Rad;
                parcel+=new Vector3(0,-.375f*(Mathf.Cos(a)-Mathf.Cos(b))-.018f*(Mathf.Sin(a)-Mathf.Sin(b)),
                    -.375f*(Mathf.Sin(a)-Mathf.Sin(b))+.018f*(Mathf.Cos(a)-Mathf.Cos(b)));
                if(activity==FxWorkerActivity.Place)head.x=beat*6*comfort;
            }
            float age=Mathf.Max(0,Finite(eventAge)),duration=EventDuration(eventKind);
            float envelope=duration>0&&age<duration?Mathf.Sin(age/duration*Mathf.PI)*comfort:0;
            // Work/grip wins over a finishing accent. Existing comedy remains the later overlay.
            if(!carrying && activity!=FxWorkerActivity.Checkout){
                if(eventKind==FxWorkerEvent.Refused){head.y+=Mathf.Sin(age*14)*5*envelope;rightArm.x-=20*envelope;}
                else if(eventKind==FxWorkerEvent.Checkout){head.x+=6*envelope;if(envelope>0)rightArm.x=-98-8*envelope;}
                else if(eventKind==FxWorkerEvent.Place){head.x+=6*envelope;rightArm.x-=34*envelope;}
                else if(eventKind==FxWorkerEvent.Dose){head.x+=4*envelope;rightArm.x-=25*envelope;}
            }
            return new FxWorkerPose(leftLeg,rightLeg,leftArm,rightArm,head,parcel,carrying);
        }
    }
}