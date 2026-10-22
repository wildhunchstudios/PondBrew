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
        // AD-B: the creature study shows any species and variant once the diorama look is on, and plays its trick.
        string studySpecies = "pondlet";
        DioramaVariant studyVariant;
        bool studyBuiltDiorama;
        float studyTrickAge = -1, studyLastTime;

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
        void BuildStoreSample(Transform tank, bool hero)
        {
            if (hero) sampleTank=tank;
            var wood=artHoney;
            var lightWood=artPaleOak;
            var sea=artTeal;
            var coral=artCoral;
            if (hero)
            {
                var back=Group("Little Wonders store corner",tank,new Vector3(0,0,1.17f));
                ArtBox(back,"Teal wall panel",new Vector3(0,1.74f,0),new Vector3(2.40f,3.38f,.09f),sea);
                for(int i=-4;i<=4;i++)
                    ArtBox(back,"Oak fluted panel",new Vector3(i*.26f,1.61f,-.058f),new Vector3(.085f,2.98f,.075f),wood);
                ArtBox(back,"Crown sign",new Vector3(0,2.92f,-.12f),new Vector3(2.32f,.58f,.08f),cream);
                Text(back,"LITTLE WONDERS",new Vector3(0,3.02f,-.17f),.10f,new Color(.045f,.19f,.20f),2.08f);
                Text(back,"GROWN HERE. LOVED EVERYWHERE.",new Vector3(0,2.80f,-.17f),.036f,new Color(.025f,.065f,.07f),2.06f);
                ArtBox(back,"Coral sign underline",new Vector3(0,2.65f,-.16f),new Vector3(2.32f,.045f,.09f),coral);
                SealArt(back);
            }
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
            ArtBox(tank,"Enamel canopy",new Vector3(0,2.22f,0),new Vector3(2.15f,.09f,1.42f),sea);
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
            RebuildStudyCreature();
            fishPedestal=Shape(sampleFigure,"Specimen study pedestal",PrimitiveType.Cylinder,new Vector3(0,.20f,0),new Vector3(.80f,.20f,.80f),dark);
            RebuildStudyCharacter(false);
            RebuildStudyCharacter(true);
            sampleStage.gameObject.SetActive(false);
        }
        void RebuildStudyCreature()
        {
            if(pondletStudy) {pondletStudy.gameObject.SetActive(false);Destroy(pondletStudy.gameObject);}
            pondletStudy=Group((studySpecies=="pondlet"?"Pondlet":"Creature")+" study (not inventory)",sampleFigure,new Vector3(0,1.04f,0));
            studyFish=pondletStudy.gameObject.AddComponent<CreatureVisual>();studyFish.Build(studySpecies);
            studyBuiltDiorama=studyFish.IsDiorama;
            float scale=1.5f;
            if(studyFish.IsDiorama)
            {
                // Every kit species fits the plinth: the longest side about as long as the Pondlet's.
                var bounds=new Bounds(pondletStudy.position,Vector3.zero);
                foreach(var renderer in pondletStudy.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                float longest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                if(longest>.01f) scale=Mathf.Clamp(1.5f*.97f/longest,.8f,1.5f);
                studyFish.SetDioramaVariant(studyVariant);
            }
            pondletStudy.localScale=Vector3.one*scale;
            studyTrickAge=-1;
        }
        /// <summary>AD-B: the creature study's species and variant (the variant shows only on kit creatures, with the look on).</summary>
        public void SetStudyCreature(string species,DioramaVariant variant)
        {
            if(string.IsNullOrEmpty(species)) return;
            BuildSampleStage();
            bool rebuild=species!=studySpecies || studyBuiltDiorama!=DioramaLook.Active || !studyFish;
            studySpecies=species;studyVariant=variant;
            if(rebuild){RebuildStudyCreature();pondletStudy.gameObject.SetActive(sampleStage.gameObject.activeSelf && sampleView==2);}
            else if(studyFish.IsDiorama) studyFish.SetDioramaVariant(variant);
        }
        /// <summary>AD-B: play the study creature's signature trick (kit creatures, with the look on and motion playing).</summary>
        public bool PlayStudyTrick()
        {
            if(!studyFish || !studyFish.IsDiorama || fxReducedMotion || sampleView!=2) return false;
            studyTrickAge=0;
            if(studySpecies=="pondlet" && dioramaCamera) DioramaTrickBubble(studyFish,dioramaCamera.transform.position);
            return true;
        }
        // AD-C: the character studies show every customer type and crew member with the look on, holding a chosen reaction.
        string studyCustomerRole="casual",studyCrewRole="cashier";
        DioramaVisitorMood studyMood;
        float studyFaceTime;
        void RebuildStudyCharacter(bool crew)
        {
            var old=crew?employeeStudy:customerStudy;
            bool shown=old && old.gameObject.activeSelf;
            if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            var group=Group(crew?"Employee study / no jobs or wages":(studyCustomerRole=="casual"?"Casual customer study":"Customer study"),sampleFigure,Vector3.zero);
            group.localRotation=Quaternion.Euler(0,180,0);
            var figure=group.gameObject.AddComponent<ShopCharacterVisual>();
            figure.BuildForRole(crew?studyCrewRole:studyCustomerRole,crew);
            if(crew){employeeStudy=group;studyEmployee=figure;}else{customerStudy=group;studyCustomer=figure;}
            group.gameObject.SetActive(shown);
        }
        /// <summary>AD-C: the character study's role (a customer type, or a crew member) and the reaction it holds.</summary>
        public void SetStudyCharacter(bool crew,string role,DioramaVisitorMood mood)
        {
            if(string.IsNullOrEmpty(role)) return;
            BuildSampleStage();
            var figure=crew?studyEmployee:studyCustomer;
            bool rebuild=role!=(crew?studyCrewRole:studyCustomerRole) || !figure || figure.IsDiorama!=DioramaLook.Active;
            if(crew) studyCrewRole=role; else studyCustomerRole=role;
            studyMood=mood;
            if(rebuild) RebuildStudyCharacter(crew);
        }
        public ShopCharacterVisual StudyCharacterForValidation(bool crew)=>crew?studyEmployee:studyCustomer;
        public DioramaVisitorMood StudyMoodForValidation=>studyMood;
        public string StudySpeciesForValidation => studySpecies;
        public CreatureVisual StudyCreatureForValidation => studyFish;
        public float StudyTrickAgeForValidation => studyTrickAge;

        public void ShowPresentationSample(int index,Camera camera)
        {
            BuildSampleStage();sampleView=Mathf.Clamp(index,0,4);
            // The creature study follows the look: the kit creature while it is on, the classic Pondlet while it is off.
            if(sampleView==2 && studyBuiltDiorama!=DioramaLook.Active) SetStudyCreature(DioramaLook.Active?studySpecies:"pondlet",studyVariant);
            sampleStage.gameObject.SetActive(sampleView>=2);
            pondletStudy.gameObject.SetActive(sampleView==2);fishPedestal.gameObject.SetActive(sampleView==2);
            // The character studies follow the look too: the role's kit figure while it is on, the first ones while it is off.
            if(sampleView==3 && studyCustomer && studyCustomer.IsDiorama!=DioramaLook.Active) RebuildStudyCharacter(false);
            if(sampleView==4 && studyEmployee && studyEmployee.IsDiorama!=DioramaLook.Active) RebuildStudyCharacter(true);
            customerStudy.gameObject.SetActive(sampleView==3);employeeStudy.gameObject.SetActive(sampleView==4);
            Vector3 eye,look;
            if(sampleView==0){eye=sampleTank.TransformPoint(new Vector3(-.7f,1.90f,-3.65f));look=sampleTank.TransformPoint(new Vector3(0,1.16f,.3f));}
            else if(sampleView==1){eye=sampleBloom.TransformPoint(new Vector3(1.1f,1.72f,-2.90f));look=sampleBloom.TransformPoint(new Vector3(0,.98f,0));}
            else{eye=new Vector3(1.6f,1.55f,-5.08f);look=new Vector3(0,.72f,-2.35f);}
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));
            // Frame the full subject between the fixed header and preview controls at 16:9.
            camera.fieldOfView=sampleView>=2?57:72;
            foreach(var label in labels) FitLabel(label);
        }
        public void AnimatePresentationSample(CharacterSamplePose pose,float time,float angle)
        {
            if(!sampleStage || !sampleStage.gameObject.activeSelf) return;
            if(fxReducedMotion) time=0;
            sampleFigure.localRotation=Quaternion.Euler(0,angle,0);
            if(sampleView==2) AnimateStudyCreature(time);
            else if(sampleView==3 || sampleView==4)
            {
                var figure=sampleView==3?studyCustomer:studyEmployee;
                figure.Pose(pose,time);
                float dt=Mathf.Clamp(time-studyFaceTime,0,.25f);studyFaceTime=time;
                if(figure.IsDiorama) figure.TickDioramaFace(studyMood,dt<=0?Time.unscaledDeltaTime:dt,fxReducedMotion);
            }
        }
        void AnimateStudyCreature(float time)
        {
            float dt=Mathf.Clamp(time-studyLastTime,0,.25f);studyLastTime=time;
            if(!studyFish.IsDiorama){studyFish.Animate(time);return;}
            // The kit creature's idle habit, and its trick when asked (both still in Reduced motion, where time stays 0).
            float before=studyTrickAge;
            float duration=studySpecies=="pondlet"?.6f:DioramaTricks.Duration(studySpecies);
            if(studyTrickAge>=0){studyTrickAge+=dt;if(studyTrickAge>=duration) studyTrickAge=-1;}
            bool tricking=studyTrickAge>=0 && !fxReducedMotion;
            var idle=fxReducedMotion?default:DioramaTricks.Idle(studySpecies,time,0,time-dt);
            var trick=tricking && studySpecies!="pondlet"?DioramaTricks.Trick(studySpecies,studyTrickAge,before):default;
            if(tricking && studySpecies=="pondlet"){trick.Happy=.75f;trick.Puffed=.3f;}
            studyFish.SetDioramaFace(tricking?Mathf.Max(.2f,trick.Happy):.2f,Mathf.Max(idle.Surprised,trick.Surprised),Mathf.Max(idle.Puffed,trick.Puffed));
            studyFish.SetDioramaAction(idle.Glow+trick.Glow,idle.Tail+trick.Tail,idle.Fins+trick.Fins);
            if(dioramaCamera) studyFish.SetLookTarget(dioramaCamera.transform.position);
            pondletStudy.localPosition=new Vector3(0,1.04f+(idle.Lift+trick.Lift)*2,0);
            pondletStudy.localRotation=Quaternion.Euler(idle.Pitch+trick.Pitch,idle.Yaw+trick.Yaw,idle.Roll+trick.Roll);
            studyFish.Animate(time);
            if(!fxReducedMotion && studySpecies!="pondlet" && (trick.Release || idle.Release) && dioramaCamera)
                DioramaCreatureBurst(studyFish,trick.Release,dioramaCamera.transform.position);
        }
        public void HidePresentationSample(){if(sampleStage) sampleStage.gameObject.SetActive(false);}
        void DisposeSamplePresentation(){foreach(var material in sampleMaterials) if(material) Destroy(material);}
    }
}
