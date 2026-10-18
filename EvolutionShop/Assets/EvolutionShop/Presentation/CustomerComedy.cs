using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class CustomerActor
    {
        // Applied by the session after navigation and ordinary gait. Never touches the actor root,
        // destination, caption, patience or transaction. Normal gait restores the base each frame.
        public void SetComedyPose(ComedyKind kind,float elapsed,bool reduced)
            =>SetComedyPose(kind,elapsed,reduced,0);
        public void SetComedyPose(ComedyKind kind,float elapsed,bool reduced,int variant)
        {
            if(!isActiveAndEnabled || !initialized || paused || !body || IsSlapReacting || customerReaction!=FxCustomerReaction.None)return;
            ApplyComedyPose(FxComedyMotion.Sample(kind,elapsed,reduced,variant));
        }
        void ApplyComedyPose(FxComedyPose pose)
        {
            if(leftArm)leftArm.localRotation=Quaternion.Euler(pose.LeftArmEuler);
            if(rightArm)rightArm.localRotation=Quaternion.Euler(pose.RightArmEuler);
            if(upperBody)upperBody.localRotation=Quaternion.Euler(pose.UpperEuler+pose.HeadEuler*.35f);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PreviewComedyForValidation(ComedyKind kind,float elapsed,bool reduced,int variant)
        { ApplyComedyPose(FxComedyMotion.Sample(kind,elapsed,reduced,variant)); }
#endif
        public void ClearComedyPose()
        {
            if(!body || IsSlapReacting)return;
            if(upperBody)upperBody.localRotation=Quaternion.identity;
            if(leftArm)leftArm.localRotation=Quaternion.Euler(0,0,-7);
            if(rightArm)rightArm.localRotation=Quaternion.Euler(0,0,7);
        }
        public void CancelSlapPresentation()=>ClearSlapReaction();
    }
}
