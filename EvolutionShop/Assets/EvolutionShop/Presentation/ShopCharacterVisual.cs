using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public enum CharacterSamplePose { Idle, Walk, Greet, Carry }

    /// <summary>Original jointed sample, shared by the casual visitor and employee study.
    /// Local poses only: navigation, work, inventory and payment have separate owners.</summary>
    public sealed class ShopCharacterVisual : MonoBehaviour
    {
        public Transform LeftLeg { get; private set; }
        public Transform RightLeg { get; private set; }
        public Transform LeftArm { get; private set; }
        public Transform RightArm { get; private set; }
        Transform head, parcel;
        readonly List<Material> materials = new List<Material>();
        bool built;

        // Opt-in only for employed actors. Customer and presentation-sample callers
        // retain the original Pose/PoseComedy API and geometry.
        ShopWorld workerWorld;
        string workerId,workerJob,workerProxyIdentity,workerEventJob;
        FxOwnerToken workerOwner;
        FxWorkerActivity workerActivity;
        FxWorkerEvent workerEvent;
        float workerProgress,workerGait,workerDistance,workerSpeed,workerMoving,workerAge,workerTime;
        int workerEventCount,workerSamples;
        bool workerCarrying,workerPaused;
        Vector3 workerPrevious,leftWorkerRest,rightWorkerRest;
        Vector3[] leftWorkerPoints,rightWorkerPoints;
        float leftWorkerFloor,rightWorkerFloor;
        string workerEventMaskJob;
        int workerEventMask;
        public Transform WorkerProxy => parcel;
        public FxOwnerToken WorkerFxOwner => workerOwner;
        public FxWorkerSnapshot WorkerSnapshot => new FxWorkerSnapshot(workerId,workerJob,workerProxyIdentity,workerEventJob,
            workerActivity,workerEvent,workerProgress,workerGait,workerDistance,workerSpeed,workerMoving,workerAge,workerEventCount,
            workerSamples,workerCarrying,parcel&&parcel.gameObject.activeSelf,workerPaused||(workerWorld&&workerWorld.FxPolicy==FxViewPolicy.Paused),workerWorld&&workerWorld.WorkerReducedMotion);
        public void ConfigureWorker(ShopWorld world,string id)
        {
            if(!built || string.IsNullOrEmpty(id))return;
            workerWorld=world;workerId=id;workerPrevious=transform.position;
            if(leftWorkerPoints==null){
                leftWorkerRest=LeftLeg.localPosition;rightWorkerRest=RightLeg.localPosition;
                leftWorkerPoints=WorkerLegPoints(LeftLeg);rightWorkerPoints=WorkerLegPoints(RightLeg);
                leftWorkerFloor=Mathf.Max(0,leftWorkerRest.y+WorkerLegMinimum(leftWorkerPoints,0));
                rightWorkerFloor=Mathf.Max(0,rightWorkerRest.y+WorkerLegMinimum(rightWorkerPoints,0));
            }
            workerOwner=world?world.BindWorkerPresentation(this,id):default;
            ApplyWorkerPose(FxWorkerMotion.Sample(FxWorkerActivity.Idle,0,0,0,0,FxWorkerEvent.None,0,false,true));
        }
        public void PoseWorker(FxWorkerActivity activity,float progress,float seconds,bool carrying,string jobId,string proxyIdentity,bool paused)
        {
            if(workerId==null || !built || !isActiveAndEnabled)return;
            if(workerWorld && workerWorld.FxCues!=null && !workerWorld.FxCues.IsValid(workerOwner))workerOwner=workerWorld.BindWorkerPresentation(this,workerId);
            if(jobId!=workerJob && (jobId!=null || workerEvent==FxWorkerEvent.Pickup))CancelWorkerPresentation();
            workerJob=jobId;workerActivity=activity;workerProgress=Mathf.Clamp01(progress);workerCarrying=carrying;
            workerProxyIdentity=carrying?proxyIdentity:null;workerPaused=paused;
            float dt=float.IsNaN(seconds)||float.IsInfinity(seconds)?0:Mathf.Max(0,seconds);
            Vector3 delta=transform.position-workerPrevious;delta.y=0;float distance=delta.magnitude;
            bool valid=!paused&&dt>0&&dt<=.5f&&distance<=Mathf.Max(.08f,dt*4.5f);
            workerSpeed=valid?distance/dt:0;
            bool moving=workerSpeed>.015f;
            if(moving){workerDistance+=distance;workerGait+=distance*(Mathf.PI*2/FxWorkerMotion.StrideLength);}
            workerMoving=moving?Mathf.MoveTowards(workerMoving,Mathf.Clamp01(workerSpeed/.6f),dt*9):0;
            workerPrevious=transform.position;workerSamples++;
            if(!paused){workerTime+=dt;if(workerEvent!=FxWorkerEvent.None){
                workerAge+=dt;if(workerAge>=FxWorkerMotion.EventDuration(workerEvent)){workerEvent=FxWorkerEvent.None;workerAge=0;}
            }}
            bool reduced=workerWorld&&workerWorld.WorkerReducedMotion;
            bool quiet=workerWorld&&workerWorld.FxPolicy!=FxViewPolicy.Visible;
            ApplyWorkerPose(FxWorkerMotion.Sample(activity,progress,workerGait,workerMoving,workerTime,workerEvent,workerAge,carrying,
                reduced || (quiet&&workerEvent==FxWorkerEvent.None)));
        }
        public bool PlayWorkerEvent(FxWorkerEvent kind,string jobId)
        {
            if(workerId==null || !isActiveAndEnabled || workerPaused || string.IsNullOrEmpty(jobId) ||
                kind<=FxWorkerEvent.None || kind>FxWorkerEvent.Refused)return false;
            if(workerEventMaskJob!=jobId){workerEventMaskJob=jobId;workerEventMask=0;}
            int mask=1<<(int)kind;if((workerEventMask&mask)!=0)return false;
            workerEventMask|=mask;workerEvent=kind;workerEventJob=jobId;workerAge=0;workerEventCount++;return true;
        }
        public void ClearWorkerJobPresentation()
        {
            workerJob=workerProxyIdentity=null;workerCarrying=false;workerActivity=FxWorkerActivity.Idle;workerProgress=0;
            if(parcel)parcel.gameObject.SetActive(false);
            if(workerEvent==FxWorkerEvent.Pickup){
                CancelWorkerPresentation();
                if(workerWorld)workerWorld.CancelFxOwner(workerOwner);
            }
        }
        public void CancelWorkerPresentation()
        {
            workerEvent=FxWorkerEvent.None;workerAge=0;workerSpeed=workerMoving=0;workerPrevious=transform.position;
            if(workerId!=null && built)ApplyWorkerPose(FxWorkerMotion.Sample(FxWorkerActivity.Idle,0,workerGait,0,0,FxWorkerEvent.None,0,workerCarrying,true));
        }
        static Vector3[] WorkerLegPoints(Transform leg)
        {
            var points=new List<Vector3>();
            foreach(var filter in leg.GetComponentsInChildren<MeshFilter>()){
                if(!filter.sharedMesh)continue;var matrix=leg.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(var vertex in filter.sharedMesh.vertices)points.Add(matrix.MultiplyPoint3x4(vertex));
            }
            return points.ToArray();
        }
        static float WorkerLegMinimum(Vector3[] points,float angle)
        {
            if(points==null||points.Length==0)return 0;
            float a=angle*Mathf.Deg2Rad,sin=Mathf.Sin(a),cos=Mathf.Cos(a),low=float.MaxValue;
            for(int i=0;i<points.Length;i++)low=Mathf.Min(low,points[i].y*cos-points[i].z*sin);
            return low;
        }
        void ApplyWorkerPose(FxWorkerPose pose)
        {
            if(!built || leftWorkerPoints==null)return;
            LeftLeg.localRotation=Quaternion.Euler(pose.LeftLegEuler);RightLeg.localRotation=Quaternion.Euler(pose.RightLegEuler);
            LeftLeg.localPosition=leftWorkerRest+Vector3.up*(leftWorkerFloor-leftWorkerRest.y-WorkerLegMinimum(leftWorkerPoints,pose.LeftLegEuler.x));
            RightLeg.localPosition=rightWorkerRest+Vector3.up*(rightWorkerFloor-rightWorkerRest.y-WorkerLegMinimum(rightWorkerPoints,pose.RightLegEuler.x));
            LeftArm.localRotation=Quaternion.Euler(pose.LeftArmEuler);RightArm.localRotation=Quaternion.Euler(pose.RightArmEuler);
            head.localRotation=Quaternion.Euler(pose.HeadEuler);parcel.localPosition=pose.ParcelPosition;parcel.localRotation=Quaternion.identity;
            parcel.gameObject.SetActive(pose.ProxyVisible);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PreviewWorkerMotionForValidation(FxWorkerActivity activity,float progress,float gaitPhase,float moving,float idleTime,
            FxWorkerEvent eventKind,float eventAge,bool carrying,bool reduced)
        {ApplyWorkerPose(FxWorkerMotion.Sample(activity,progress,gaitPhase,moving,idleTime,eventKind,eventAge,carrying,reduced));}
#endif
        void OnEnable()
        {
            workerPrevious=transform.position;
            if(workerWorld&&workerId!=null)workerOwner=workerWorld.BindWorkerPresentation(this,workerId);
        }
        void OnDisable()
        {
            if(workerId==null)return;
            CancelWorkerPresentation();workerCarrying=false;workerProxyIdentity=null;workerMoving=workerSpeed=0;
            if(parcel)parcel.gameObject.SetActive(false);
            if(workerWorld)workerWorld.ReleaseWorkerPresentation(workerOwner);
        }

        Material Mat(string name, Color color)
        {
            var m = ShopWorld.MakeMaterial("Sample / " + name, color, 0, .32f);
            materials.Add(m); return m;
        }
        static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = position; return t;
        }
        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 p, Vector3 s, Material m)
            => ShopWorld.Shape(parent, name, type, p, s, m);

        public void Build(bool employee)
        {
            if (built) return;
            built = true;
            var ink = Mat("deep teal", new Color(.035f,.12f,.15f));
            var cream = Mat("oat linen", new Color(.96f,.87f,.66f));
            var coral = Mat("coral stitch", new Color(.86f,.29f,.21f));
            var cloth = Mat(employee ? "teal apron" : "ochre cardigan", employee ? new Color(.12f,.44f,.41f) : new Color(.82f,.46f,.12f));
            var skin = Mat("warm clay", new Color(.64f,.36f,.23f));
            var white = Mat("eye ivory", new Color(1,.96f,.83f));
            var cheek = Mat("warm cheeks", new Color(.72f,.30f,.22f));
            LeftLeg = Joint(transform,"Left hip",new Vector3(-.13f,.66f,0));
            RightLeg = Joint(transform,"Right hip",new Vector3(.13f,.66f,0));
            foreach(var leg in new[]{LeftLeg,RightLeg})
            {
                Part(leg,"Rolled trouser",PrimitiveType.Capsule,new Vector3(0,-.24f,0),new Vector3(.20f,.245f,.21f),ink);
                Part(leg,"Trouser cuff",PrimitiveType.Cylinder,new Vector3(0,-.44f,0),new Vector3(.21f,.035f,.22f),cloth);
                Part(leg,"Soft shoe",PrimitiveType.Sphere,new Vector3(0,-.575f,.055f),new Vector3(.23f,.17f,.35f),ink);
                Part(leg,"Cream shoe sole",PrimitiveType.Cube,new Vector3(0,-.638f,.065f),new Vector3(.22f,.035f,.32f),cream);
            }
            Part(transform,"Round coat",PrimitiveType.Capsule,new Vector3(0,.96f,0),new Vector3(.53f,.30f,.37f),cloth);
            Part(transform,"Linen collar",PrimitiveType.Sphere,new Vector3(0,1.22f,.045f),new Vector3(.32f,.10f,.32f),cream);
            if(employee)
            {
                Part(transform,"Apron bib",PrimitiveType.Cube,new Vector3(0,1.025f,.191f),new Vector3(.31f,.34f,.035f),cream);
                Part(transform,"Apron skirt",PrimitiveType.Cube,new Vector3(0,.80f,.176f),new Vector3(.41f,.25f,.035f),cream);
                Part(transform,"Apron pocket",PrimitiveType.Cube,new Vector3(0,.87f,.202f),new Vector3(.22f,.12f,.022f),cloth);
                Part(transform,"Coral badge",PrimitiveType.Sphere,new Vector3(-.085f,1.12f,.216f),new Vector3(.073f,.073f,.025f),coral);
            }
            else
            {
                Part(transform,"Cardigan seam",PrimitiveType.Cube,new Vector3(0,1,.190f),new Vector3(.022f,.39f,.019f),cream);
                for(int i=0;i<3;i++) Part(transform,"Coral button",PrimitiveType.Sphere,new Vector3(.034f,1.13f-i*.11f,.204f),Vector3.one*.034f,coral);
                Part(transform,"Patch pocket",PrimitiveType.Cube,new Vector3(.15f,.91f,.168f),new Vector3(.13f,.12f,.028f),cream);
            }
            head = Joint(transform,"Head pivot",new Vector3(0,1.41f,.005f));
            Part(head,"Clay face",PrimitiveType.Sphere,Vector3.zero,new Vector3(.44f,.43f,.40f),skin);
            Part(head,"Hair at nape",PrimitiveType.Sphere,new Vector3(0,.095f,-.065f),new Vector3(.44f,.29f,.32f),ink);
            Part(head,employee?"Linen work cap":"Teal beret",PrimitiveType.Sphere,new Vector3(-.023f,.217f,-.015f),new Vector3(.49f,.16f,.44f),employee?cream:ink);
            Part(head,"Cap piping",PrimitiveType.Sphere,new Vector3(0,.168f,.005f),new Vector3(.43f,.05f,.40f),coral);
            Part(head,"Cap nub",PrimitiveType.Sphere,new Vector3(-.025f,.30f,-.01f),Vector3.one*.063f,coral);
            for(int side=-1;side<=1;side+=2)
            {
                Part(head,"Ear",PrimitiveType.Sphere,new Vector3(side*.222f,-.017f,0),new Vector3(.085f,.13f,.10f),skin);
                Part(head,"Eye white",PrimitiveType.Sphere,new Vector3(side*.085f,.023f,.184f),new Vector3(.094f,.106f,.045f),white);
                Part(head,"Pupil",PrimitiveType.Sphere,new Vector3(side*.085f,.021f,.210f),new Vector3(.043f,.061f,.025f),ink);
                Part(head,"Eye glint",PrimitiveType.Sphere,new Vector3(side*.085f-.012f,.041f,.224f),Vector3.one*.015f,white);
                Part(head,"Cheek",PrimitiveType.Sphere,new Vector3(side*.145f,-.059f,.161f),new Vector3(.062f,.033f,.025f),cheek);
                Part(head,"Eyebrow",PrimitiveType.Capsule,new Vector3(side*.085f,.098f,.177f),new Vector3(.026f,.043f,.025f),ink).localRotation=Quaternion.Euler(0,0,side*78);
                var arm=Joint(transform,side<0?"Left shoulder":"Right shoulder",new Vector3(side*.28f,1.17f,0));
                Part(arm,"Sleeve",PrimitiveType.Capsule,new Vector3(0,-.15f,0),new Vector3(.18f,.17f,.21f),cloth);
                Part(arm,"Linen cuff",PrimitiveType.Cylinder,new Vector3(0,-.31f,0),new Vector3(.18f,.034f,.19f),cream);
                Part(arm,"Mitten hand",PrimitiveType.Sphere,new Vector3(0,-.375f,.018f),new Vector3(.16f,.18f,.17f),skin);
                if(side<0) LeftArm=arm; else RightArm=arm;
            }
            Part(head,"Button nose",PrimitiveType.Sphere,new Vector3(0,-.025f,.225f),new Vector3(.092f,.095f,.10f),skin);
            Part(head,"Small smile",PrimitiveType.Sphere,new Vector3(0,-.116f,.170f),new Vector3(.087f,.022f,.023f),ink);
            parcel=Joint(transform,"Preview parcel",new Vector3(0,.91f,.38f));
            Part(parcel,"Empty delivery box",PrimitiveType.Cube,Vector3.zero,new Vector3(.47f,.29f,.33f),cream);
            Part(parcel,"Coral wrap",PrimitiveType.Cube,new Vector3(0,0,-.003f),new Vector3(.055f,.30f,.34f),coral);
            parcel.gameObject.SetActive(false);
            Pose(CharacterSamplePose.Idle,0);
        }

        public void Pose(CharacterSamplePose pose,float time)
        {
            if(!built) return;
            if(workerId!=null){LeftLeg.localPosition=leftWorkerRest;RightLeg.localPosition=rightWorkerRest;parcel.localPosition=new Vector3(0,.91f,.38f);}
            bool walking=pose==CharacterSamplePose.Walk || pose==CharacterSamplePose.Carry;
            float stride=walking?Mathf.Sin(time*7f):0;
            LeftLeg.localRotation=Quaternion.Euler(stride*23,0,0);
            RightLeg.localRotation=Quaternion.Euler(-stride*23,0,0);
            LeftArm.localRotation=Quaternion.Euler(-stride*18,0,-7);
            RightArm.localRotation=Quaternion.Euler(stride*18,0,7);
            if(pose==CharacterSamplePose.Greet) RightArm.localRotation=Quaternion.Euler(-18,0,125+Mathf.Sin(time*7)*18);
            if(pose==CharacterSamplePose.Carry)
            {
                LeftArm.localRotation=Quaternion.Euler(-67,0,-9);
                RightArm.localRotation=Quaternion.Euler(-67,0,9);
            }
            head.localRotation=Quaternion.Euler(0,pose==CharacterSamplePose.Idle?Mathf.Sin(time*.8f)*7:0,pose==CharacterSamplePose.Greet?-6:0);
            parcel.gameObject.SetActive(pose==CharacterSamplePose.Carry);
        }
        public void PoseComedy(ComedyKind kind,float elapsed,bool reduced)
        {
            Pose(CharacterSamplePose.Idle,0);
            if(!built || reduced || (kind!=ComedyKind.WorkerCelebration && kind!=ComedyKind.Slap && elapsed<ShopComedy.ReactionDelaySeconds))return;
            float wave=Mathf.Sin(elapsed*6);
            if(kind==ComedyKind.WorkerCelebration)
            {
                LeftLeg.localRotation=Quaternion.Euler(wave*12,0,0);
                RightLeg.localRotation=Quaternion.Euler(-wave*12,0,0);
                LeftArm.localRotation=Quaternion.Euler(-20,0,-100-wave*12);
                RightArm.localRotation=Quaternion.Euler(-20,0,100+wave*12);
                head.localRotation=Quaternion.Euler(0,0,wave*5);
            }
            else if(kind==ComedyKind.Complaint)
            {
                LeftArm.localRotation=Quaternion.Euler(-25,0,-50-wave*8);
                RightArm.localRotation=Quaternion.Euler(-25,0,50+wave*8);
            }
            else if(kind==ComedyKind.Fart)RightArm.localRotation=Quaternion.Euler(-112,0,-20);
            else if(kind==ComedyKind.Slap)head.localRotation=Quaternion.Euler(0,0,wave*12*Mathf.Clamp01(1-elapsed/2.6f));
            else Pose(CharacterSamplePose.Greet,elapsed);
        }
        void OnDestroy() { foreach(var material in materials) if(material) Destroy(material); }
    }
}
