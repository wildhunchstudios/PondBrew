using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        FxOwnerToken comedyFxOwner;
        Transform comedyFxSource;
        ComedyKind comedyFxKind;
        int comedyFxVariant,comedyFxCount,comedyFxFrame,comedyFxBeatFrame;
        uint comedyFxSequence;
        float comedyFxElapsed,comedyFxRealtime,comedyFxBeatRealtime;
        bool comedyFxActive,comedyFxReduced,comedyFxEmitted;
        Vector3 comedyFxPoint,comedyFxLastPosition;
        public FxOwnerToken ComedyFxOwner=>comedyFxOwner;
        public FxComedySnapshot ComedyFxSnapshot=>new FxComedySnapshot(comedyFxKind,comedyFxVariant,comedyFxSequence,
            comedyFxElapsed,comedyFxEmitted,comedyFxCount,comedyFxActive,comedyFxReduced,comedyFxLastPosition,
            comedyFxFrame,comedyFxRealtime,comedyFxBeatFrame,comedyFxBeatRealtime);
        public Transform ComedyMimicSource(string tank)=>tank!=null&&displays.TryGetValue(tank,out var display)?display.Root:null;
        public bool BeginComedyFx(ComedyKind kind,int variant,Transform source,uint sequence,bool reduced)
        {
            CancelComedyFx();
            if(!fxEnabled||fxCues==null||!source||!source.gameObject.activeInHierarchy||fxPolicy!=FxViewPolicy.Visible||
                kind<ComedyKind.Slap||kind>ComedyKind.Mimic||sequence==0)return false;
            comedyFxOwner=BindFxOwner("comedy",null,source,kind!=ComedyKind.Belch&&kind!=ComedyKind.Mimic);
            if(!fxCues.IsValid(comedyFxOwner))return false;
            comedyFxSource=source;comedyFxKind=kind;comedyFxVariant=variant&1;comedyFxSequence=sequence;
            comedyFxReduced=reduced||fxReducedMotion;comedyFxElapsed=0;comedyFxEmitted=false;comedyFxActive=true;
            comedyFxBeatFrame=-1;comedyFxBeatRealtime=-1;
            // Belch escapes the existing crown vent, clear of the glass and opaque surround.
            comedyFxPoint=kind==ComedyKind.Belch?new Vector3(0,2.32f,.027f):
                kind==ComedyKind.Mimic?new Vector3(.15f,.12f,0):
                kind==ComedyKind.Fart?new Vector3(.22f,.82f,-.10f):
                kind==ComedyKind.WorkerCelebration?new Vector3(.23f,1.35f,.32f):new Vector3(.18f,1.48f,.23f);
            comedyFxLastPosition=source.TransformPoint(comedyFxPoint);
            comedyFxFrame=Time.frameCount;comedyFxRealtime=Time.realtimeSinceStartup;
            return true;
        }
        public void CancelComedyFx()
        {
            comedyFxActive=false;comedyFxSource=null;
            fxCues?.Cancel(comedyFxOwner);fxPool?.Cancel(comedyFxOwner);
        }
        public void TickComedyFx(float elapsed)
        {
            if(!comedyFxActive)return;
            if(!fxEnabled||fxPolicy!=FxViewPolicy.Visible||!comedyFxSource||!comedyFxSource.gameObject.activeInHierarchy||
                fxCues==null||!fxCues.IsValid(comedyFxOwner)){CancelComedyFx();return;}
            if(float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0)return;
            comedyFxElapsed=elapsed;comedyFxFrame=Time.frameCount;comedyFxRealtime=Time.realtimeSinceStartup;
            comedyFxLastPosition=comedyFxSource.TransformPoint(comedyFxPoint);
            float beat=comedyFxKind==ComedyKind.Slap?FxComedyMotion.SlapContactSeconds(comedyFxReduced):ShopComedy.ReactionDelaySeconds;
            if(comedyFxEmitted||elapsed<beat)return;
            // Consume the crossing even if capacity rejects it: a later free slot must not replay a gag.
            comedyFxEmitted=true;
            var cue=comedyFxKind==ComedyKind.Slap?FxCueId.ComedyContact:FxCueId.ComedyBeat;
            if(!fxCues.Play(comedyFxOwner,cue,comedyFxSequence,FxCueOrigin.Manual,.65f,1))return;
            comedyFxCount++;comedyFxBeatFrame=Time.frameCount;comedyFxBeatRealtime=Time.realtimeSinceStartup;
            bool slap=comedyFxKind==ComedyKind.Slap,lab=comedyFxKind==ComedyKind.Belch||
                comedyFxKind==ComedyKind.WorkerCelebration&&comedyFxSource.position.z>.4f;
            Color color=slap?new Color(1,.84f,.57f,.82f):lab?new Color(.60f,.91f,.80f,.72f):new Color(.91f,.85f,.63f,.67f);
            int count=comedyFxReduced?1:fxPool!=null&&fxPool.ActionLimit<64?3:6;
            for(int i=0;i<count;i++){
                bool fizz=lab&&comedyFxVariant==1&&i>0;
                var particle=FxParticleRequest.Create(fizz?FxParticleShape.Bubble:slap&&i>2?FxParticleShape.Spark:FxParticleShape.Puff,color);
                particle.Lifetime=comedyFxReduced? .30f:.38f+(i%3)*.075f;
                particle.Size=i==0? .105f:.045f+(i%2)*.018f;
                particle.EndSize=comedyFxReduced?particle.Size:i==0? .19f:.018f;
                if(!comedyFxReduced){
                    float angle=i*2.399963f+comedyFxVariant*.71f;
                    particle.Motion=FxParticleMotion.Curve;
                    particle.End=new Vector3(Mathf.Cos(angle)*.15f,.08f+(i%3)*.045f,Mathf.Sin(angle)*.11f);
                    particle.Bend=new Vector3(Mathf.Sin(angle)*.06f,.035f,-.025f);
                }
                fxPool?.EmitParticle(fxCues,comedyFxOwner,comedyFxSource,comedyFxPoint,particle);
            }
        }


        Transform comedyBubble;
        Material comedyBubbleMaterial;
        public void PoseFishMimic(string tank,float elapsed,bool reduced)
        {
            if(!displays.TryGetValue(tank,out var display) || !display.Creature)return;
            if (!fxEnabled || fxPolicy != FxViewPolicy.Visible) { display.Creature.ClearMimicPose(); return; }
            // Queue the visitor beat; the normal detail resolver remains the only fin writer.
            display.Creature.RequestMimicPose(elapsed,reduced);
        }
        public void AnimateComedySample(ComedyKind kind,float elapsed,bool reduced)
        {
            if(sampleView==2){studyFish.Animate(reduced?0:Mathf.Floor(elapsed*2f)*.25f);return;}
            var figure=sampleView==4?studyEmployee:studyCustomer;
            if(figure)figure.PoseComedy(kind,elapsed,reduced);
        }
        public void ShowComedyBubble(Vector3 position,float elapsed,bool reduced)
        {
            if(reduced){HideComedyBubble();return;}
            if(!comedyBubble)
            {
                comedyBubbleMaterial=MakeMaterial("Original belch bubble",new Color(.47f,.90f,.78f),0,.7f);
                sampleMaterials.Add(comedyBubbleMaterial);
                comedyBubble=Shape(transform,"Comic bubble / no stock",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.15f,comedyBubbleMaterial);
            }
            float progress=Mathf.Clamp01(elapsed/2f);
            comedyBubble.gameObject.SetActive(progress<.95f);
            comedyBubble.position=position+new Vector3(Mathf.Sin(elapsed*6)*.09f,progress*.40f,0);
            comedyBubble.localScale=Vector3.one*(.10f+Mathf.Sin(progress*Mathf.PI)*.18f);
        }
        public void HideComedyBubble(){if(comedyBubble)comedyBubble.gameObject.SetActive(false);}
    }
}
