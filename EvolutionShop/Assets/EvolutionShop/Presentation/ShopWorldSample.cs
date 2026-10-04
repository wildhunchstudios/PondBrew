using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        readonly List<Material> sampleMaterials = new List<Material>();
        Transform sampleTank, sampleBloom, sampleStage, sampleFigure;
        Transform pondletStudy, customerStudy, employeeStudy, fishPedestal;
        CreatureVisual studyFish;
        ShopCharacterVisual studyCustomer, studyEmployee;
        int sampleView;

        Material SampleMaterial(string name, Color color)
        {
            var m=MakeMaterial("Presentation sample / "+name,color,0,.3f);sampleMaterials.Add(m);return m;
        }
        static void SampleLight(Transform parent,Vector3 position,float intensity,float range)
        {
            var light=new GameObject("Warm sample task light").AddComponent<Light>();
            light.transform.SetParent(parent,false);light.transform.localPosition=position;
            light.type=LightType.Point;light.color=new Color(1,.85f,.66f);
            light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;
        }
        void BuildStoreSample(Transform tank)
        {
            sampleTank=tank;
            var wood=SampleMaterial("honey wood",new Color(.63f,.36f,.16f));
            var lightWood=SampleMaterial("cut oak",new Color(.84f,.62f,.33f));
            var sea=SampleMaterial("painted teal",new Color(.10f,.35f,.33f));
            var coral=SampleMaterial("coral sign",new Color(.83f,.29f,.20f));
            var back=Group("Little Wonders store corner",tank,new Vector3(0,0,1.17f));
            Shape(back,"Teal wall panel",PrimitiveType.Cube,new Vector3(0,1.74f,0),new Vector3(2.40f,3.38f,.09f),sea);
            for(int i=-4;i<=4;i++)
                Shape(back,"Oak fluted panel",PrimitiveType.Cube,new Vector3(i*.26f,1.61f,-.058f),new Vector3(.115f,2.98f,.075f),wood);
            Shape(back,"Crown sign",PrimitiveType.Cube,new Vector3(0,2.92f,-.12f),new Vector3(2.32f,.58f,.08f),cream);
            Text(back,"LITTLE WONDERS",new Vector3(0,3.02f,-.17f),.112f,new Color(.045f,.19f,.20f),2.08f);
            Text(back,"GROWN HERE. LOVED EVERYWHERE.",new Vector3(0,2.80f,-.17f),.044f,new Color(.025f,.065f,.07f),2.06f);
            Shape(back,"Coral sign underline",PrimitiveType.Cube,new Vector3(0,2.65f,-.16f),new Vector3(2.32f,.045f,.09f),coral);
            for(int side=-1;side<=1;side+=2)
            {
                Shape(tank,"Inset wood cabinet door",PrimitiveType.Cube,new Vector3(side*.505f,.405f,-.673f),new Vector3(.89f,.70f,.032f),wood);
                Shape(tank,"Raised door panel",PrimitiveType.Cube,new Vector3(side*.505f,.405f,-.695f),new Vector3(.75f,.54f,.019f),lightWood);
                Shape(tank,"Cabinet pull",PrimitiveType.Sphere,new Vector3(side*.16f,.53f,-.728f),new Vector3(.07f,.07f,.035f),gold);
            }
            // Existing title and state stay in front of the cabinet trim.
            Shape(tank,"Label rail",PrimitiveType.Cube,new Vector3(0,.47f,-.738f),new Vector3(1.56f,.46f,.025f),dark);
            foreach(var label in tank.GetComponentsInChildren<TextMesh>())
                if(label.transform.localPosition.y<.8f){var p=label.transform.localPosition;p.z=-.765f;label.transform.localPosition=p;}
            Shape(tank,"Enamel canopy",PrimitiveType.Cube,new Vector3(0,2.22f,0),new Vector3(2.15f,.09f,1.42f),sea);
            Shape(tank,"Canopy warm light",PrimitiveType.Cube,new Vector3(0,2.166f,-.13f),new Vector3(1.77f,.016f,.17f),glow);
            SampleLight(tank,new Vector3(-.2f,2.55f,-1.1f),3.2f,3.5f);
            for(int i=0;i<5;i++)
            {
                float x=-.82f+i*.10f;
                Shape(tank,"Terracotta river gravel",PrimitiveType.Sphere,new Vector3(x,1.061f,.30f+i%2*.1f),new Vector3(.095f,.045f,.08f),i%2==0?wood:lightWood);
            }
        }
        void BuildLabSample(Transform root)
        {
            sampleBloom=root;
            var coral=SampleMaterial("lab coral enamel",new Color(.87f,.31f,.24f));
            var teal=SampleMaterial("lab gasket",new Color(.08f,.29f,.31f));
            // All detail is attached to the existing relocatable station and inside its footprint.
            Shape(root,"Bloom cap stripe",PrimitiveType.Cylinder,new Vector3(0,2.17f,0),new Vector3(.83f,.025f,.83f),coral);
            Shape(root,"Cap hand grip",PrimitiveType.Cube,new Vector3(0,2.225f,0),new Vector3(.38f,.055f,.11f),teal);
            for(int side=-1;side<=1;side+=2)
                for(int i=0;i<3;i++) Shape(root,"Ribbed base seam",PrimitiveType.Cube,new Vector3(side*.58f,.36f+i*.12f,-.30f),new Vector3(.05f,.025f,.24f),teal);
            Shape(root,"Outlet escutcheon",PrimitiveType.Cylinder,new Vector3(.36f,.93f,-.60f),new Vector3(.32f,.018f,.32f),cream).localRotation=Quaternion.Euler(90,0,0);
            var gauge=Group("Bloom pressure gauge",root,new Vector3(-.32f,.87f,-.56f));
            Shape(gauge,"Gauge rim",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.31f,.025f,.31f),teal).localRotation=Quaternion.Euler(90,0,0);
            Shape(gauge,"Gauge face",PrimitiveType.Cylinder,new Vector3(0,0,-.03f),new Vector3(.255f,.008f,.255f),cream).localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<5;i++)
            {
                float angle=(-65+i*32.5f)*Mathf.Deg2Rad;
                Shape(gauge,"Gauge tick",PrimitiveType.Cube,new Vector3(Mathf.Sin(angle)*.10f,Mathf.Cos(angle)*.10f,-.043f),new Vector3(.012f,.028f,.005f),teal).localRotation=Quaternion.Euler(0,0,-angle*Mathf.Rad2Deg);
            }
            Shape(gauge,"Decorative pressure needle",PrimitiveType.Cube,new Vector3(.024f,.029f,-.052f),new Vector3(.012f,.088f,.008f),coral).localRotation=Quaternion.Euler(0,0,-35);
            Text(root,"PETAL WORKS / B-01",new Vector3(0,.14f,-.627f),.028f,new Color(.94f,.83f,.63f),.97f);
            SampleLight(root,new Vector3(.15f,2.30f,-.9f),2.4f,2.7f);
        }

        void BuildSampleStage()
        {
            if(sampleStage) return;
            sampleStage=Group("Look-only presentation stage",transform,new Vector3(0,0,-2.35f));
            SampleLight(sampleStage,new Vector3(.8f,2.30f,-1.5f),4.8f,4.5f);
            var oak=SampleMaterial("study platform",new Color(.65f,.41f,.20f));
            Shape(sampleStage,"Study plinth",PrimitiveType.Cylinder,new Vector3(0,.06f,0),new Vector3(1.56f,.06f,1.56f),oak);
            sampleFigure=Group("Turntable",sampleStage,new Vector3(0,.12f,0));
            pondletStudy=Group("Pondlet study (not inventory)",sampleFigure,new Vector3(0,1.04f,0));
            studyFish=pondletStudy.gameObject.AddComponent<CreatureVisual>();studyFish.Build("pondlet");
            pondletStudy.localScale=Vector3.one*1.5f;
            fishPedestal=Shape(sampleFigure,"Specimen study pedestal",PrimitiveType.Cylinder,new Vector3(0,.20f,0),new Vector3(.80f,.20f,.80f),dark);
            customerStudy=Group("Casual customer study",sampleFigure,Vector3.zero);
            customerStudy.localRotation=Quaternion.Euler(0,180,0);
            studyCustomer=customerStudy.gameObject.AddComponent<ShopCharacterVisual>();studyCustomer.Build(false);
            employeeStudy=Group("Employee study / no jobs or wages",sampleFigure,Vector3.zero);
            employeeStudy.localRotation=Quaternion.Euler(0,180,0);
            studyEmployee=employeeStudy.gameObject.AddComponent<ShopCharacterVisual>();studyEmployee.Build(true);
            sampleStage.gameObject.SetActive(false);
        }
        public void ShowPresentationSample(int index,Camera camera)
        {
            BuildSampleStage();sampleView=Mathf.Clamp(index,0,4);
            sampleStage.gameObject.SetActive(sampleView>=2);
            pondletStudy.gameObject.SetActive(sampleView==2);fishPedestal.gameObject.SetActive(sampleView==2);
            customerStudy.gameObject.SetActive(sampleView==3);employeeStudy.gameObject.SetActive(sampleView==4);
            Vector3 eye,look;
            if(sampleView==0){eye=sampleTank.TransformPoint(new Vector3(-.7f,1.90f,-3.65f));look=sampleTank.TransformPoint(new Vector3(0,1.38f,.3f));}
            else if(sampleView==1){eye=sampleBloom.TransformPoint(new Vector3(1.1f,1.72f,-2.90f));look=sampleBloom.TransformPoint(new Vector3(0,.98f,0));}
            else{eye=new Vector3(1.6f,1.55f,-5.08f);look=new Vector3(0,.72f,-2.35f);}
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));
            // Frame the full subject between the fixed header and preview controls at 16:9.
            camera.fieldOfView=sampleView>=2?55:72;
            foreach(var label in labels) FitLabel(label);
        }
        public void AnimatePresentationSample(CharacterSamplePose pose,float time,float angle)
        {
            if(!sampleStage || !sampleStage.gameObject.activeSelf) return;
            sampleFigure.localRotation=Quaternion.Euler(0,angle,0);
            if(sampleView==2) studyFish.Animate(time);
            else if(sampleView==3) studyCustomer.Pose(pose,time);
            else if(sampleView==4) studyEmployee.Pose(pose,time);
        }
        public void HidePresentationSample(){if(sampleStage) sampleStage.gameObject.SetActive(false);}
        void DisposeSamplePresentation(){foreach(var material in sampleMaterials) if(material) Destroy(material);}
    }
}
