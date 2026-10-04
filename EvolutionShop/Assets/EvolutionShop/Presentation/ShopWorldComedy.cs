using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        Transform comedyBubble;
        Material comedyBubbleMaterial;
        public void PoseFishMimic(string tank,float elapsed,bool reduced)
        {
            if(reduced || !displays.TryGetValue(tank,out var display) || !display.Creature)return;
            // Copy the visitor's held-arm beat using existing safe fin poses, never the swim root.
            display.Creature.Animate(Mathf.Floor(elapsed*2f)*.25f);
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
