using UnityEngine;
namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        public bool WorkerReducedMotion=>fxReducedMotion;
        public int WorkerPickupAccentCount{get;private set;}
        public int WorkerDoseAccentCount{get;private set;}
        public int WorkerPlaceAccentCount{get;private set;}
        public int WorkerCheckoutAccentCount{get;private set;}
        public int WorkerRefusalAccentCount{get;private set;}
        public FxOwnerToken BindWorkerPresentation(ShopCharacterVisual actor,string id)
        {
            if(!actor||string.IsNullOrEmpty(id))return default;
            var owner=BindFxOwner("worker:"+id,id,actor.transform,true);
            if(fxCues!=null&&fxCues.IsValid(owner))fxBindings[owner.Slot].Worker=actor;
            return owner;
        }
        public void ReleaseWorkerPresentation(FxOwnerToken owner)
        {
            if(fxCues==null||!fxCues.IsValid(owner))return;
            CancelFxOwner(owner);fxCues.Unregister(owner);fxBindings[owner.Slot]=default;
        }
        public bool PresentWorkerEvent(ShopCharacterVisual actor,FxWorkerEvent kind,string jobId,uint sequence)
        {
            if(!fxEnabled||fxCues==null||!isActiveAndEnabled||!actor||!actor.isActiveAndEnabled||
                kind<=FxWorkerEvent.None||kind>FxWorkerEvent.Refused||
                (fxPolicy!=FxViewPolicy.Visible&&fxPolicy!=FxViewPolicy.Modal))return false;
            var owner=actor.WorkerFxOwner;
            if(!fxCues.IsValid(owner)||fxBindings[owner.Slot].Worker!=actor)return false;
            FxCueId cue=kind==FxWorkerEvent.Pickup?FxCueId.WorkerPickup:kind==FxWorkerEvent.Dose?FxCueId.WorkerDose:
                kind==FxWorkerEvent.Place?FxCueId.WorkerPlace:kind==FxWorkerEvent.Checkout?FxCueId.WorkerCheckout:FxCueId.WorkerRefused;
            if(!fxCues.Play(owner,cue,sequence,FxCueOrigin.Worker,FxWorkerMotion.EventDuration(kind),kind==FxWorkerEvent.Refused?3:4))return false;
            fxPool?.Cancel(owner);
            if(!actor.PlayWorkerEvent(kind,jobId)){fxCues.Cancel(owner);return false;}
            switch(kind){
                case FxWorkerEvent.Pickup:WorkerPickupAccentCount++;break;
                case FxWorkerEvent.Dose:WorkerDoseAccentCount++;break;
                case FxWorkerEvent.Place:WorkerPlaceAccentCount++;break;
                case FxWorkerEvent.Checkout:WorkerCheckoutAccentCount++;break;
                case FxWorkerEvent.Refused:WorkerRefusalAccentCount++;break;
            }
            Transform source=actor.transform;Vector3 point=new Vector3(.20f,1.04f,.37f);
            if(actor.WorkerSnapshot.WorkerId==ShopStaff.CashierId &&
                (kind==FxWorkerEvent.Checkout||kind==FxWorkerEvent.Refused))point.y=1.28f;
            if(kind==FxWorkerEvent.Dose && displays.TryGetValue("incubator",out var incubator)&&incubator.Root){
                source=incubator.Root.parent;point=new Vector3(0,1.48f,-.46f);
            }
            bool refused=kind==FxWorkerEvent.Refused,pickup=kind==FxWorkerEvent.Pickup;
            Color color=refused?new Color(.95f,.58f,.43f,.7f):kind==FxWorkerEvent.Checkout?
                new Color(1,.81f,.42f,.78f):new Color(.59f,.91f,.84f,.75f);
            var primary=FxParticleRequest.Create(refused?FxParticleShape.Puff:FxParticleShape.Ring,color);
            primary.Lifetime=.45f;primary.Size=pickup? .055f:.075f;primary.EndSize=fxReducedMotion?primary.Size:pickup? .09f:.14f;
            fxPool?.EmitParticle(fxCues,owner,source,point,primary);
            // Pickup means handling the existing empty proxy, not removing source stock.
            // Saved habitat transfers already own their vessel effects; do not duplicate them.
            if(fxReducedMotion||pickup||refused||fxPool==null)return true;
            int count=fxPool.ActionLimit<64?1:3;
            for(int i=0;i<count;i++){
                float phase=i*2.399963f+(sequence%11)*.29f;
                var mote=FxParticleRequest.Create(kind==FxWorkerEvent.Checkout?FxParticleShape.Spark:FxParticleShape.Bubble,color);
                mote.Lifetime=.32f+i*.045f;mote.Size=.035f;mote.EndSize=.012f;mote.Motion=FxParticleMotion.Curve;
                mote.End=new Vector3(Mathf.Cos(phase)*.07f,.10f,Mathf.Sin(phase)*.05f);mote.Bend=new Vector3(0,.035f,.02f);
                fxPool.EmitParticle(fxCues,owner,source,point,mote);
            }
            return true;
        }
    }
}