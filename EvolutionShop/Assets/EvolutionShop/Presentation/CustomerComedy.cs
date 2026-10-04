using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class CustomerActor
    {
        // Applied by the session after navigation and ordinary gait. Never touches the actor root,
        // destination, caption, patience or transaction. Normal gait restores the base each frame.
        public void SetComedyPose(ComedyKind kind,float elapsed,bool reduced)
        {
            if(!isActiveAndEnabled || !initialized || paused || !body || IsSlapReacting || reduced || elapsed<ShopComedy.ReactionDelaySeconds)return;
            float wave=Mathf.Sin(elapsed*6f);
            if(kind==ComedyKind.Fart)
                rightArm.localRotation=Quaternion.Euler(-112,0,-20);
            else if(kind==ComedyKind.Complaint)
            {
                leftArm.localRotation=Quaternion.Euler(-25,0,-50-wave*8);
                rightArm.localRotation=Quaternion.Euler(-25,0,50+wave*8);
            }
            else if(kind==ComedyKind.Mimic)
                rightArm.localRotation=Quaternion.Euler(-18,0,112+wave*10);
            body.localRotation*=Quaternion.Euler(0,0,wave*3);
        }
        public void ClearComedyPose()
        {
            if(!body || IsSlapReacting)return;
            body.localRotation=Quaternion.identity;
            if(leftArm)leftArm.localRotation=Quaternion.Euler(0,0,-7);
            if(rightArm)rightArm.localRotation=Quaternion.Euler(0,0,7);
        }
        public void CancelSlapPresentation()=>ClearSlapReaction();
    }
}
