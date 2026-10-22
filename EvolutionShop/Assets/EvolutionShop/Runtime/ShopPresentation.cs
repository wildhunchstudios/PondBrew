using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public int PresentationView { get; private set; }
        public CharacterSamplePose PresentationPose { get; private set; }
        public bool PresentationMotion { get; private set; }=true;
        /// <summary>AD-B: the creature study's species (index in DioramaSpecies.All) and colour variant, with the diorama look on.</summary>
        public int PresentationSpecies { get; private set; }
        public DioramaVariant PresentationVariant { get; private set; }
        public string PresentationSpeciesId => DioramaSpecies.All[Mathf.Clamp(PresentationSpecies,0,DioramaSpecies.All.Count-1)].Id;
        /// <summary>Whether the creature study offers every species, its variants and its trick (the look is on, the creature shown).</summary>
        public bool PresentationCreatures => Panel==ShopPanel.Presentation && !ComedyPreview && PresentationView==2 && DioramaLookOn;
        void ApplyPresentationCreature() => World.SetStudyCreature(DioramaLookOn?PresentationSpeciesId:"pondlet",PresentationVariant);
        public void SelectPresentationSpecies(int index)
        {
            if(!PresentationCreatures) return;
            int count=DioramaSpecies.All.Count;PresentationSpecies=((index%count)+count)%count;sampleClock=0;
            ApplyPresentationCreature();
        }
        public void SelectPresentationVariant(int index)
        {
            if(!PresentationCreatures) return;
            PresentationVariant=(DioramaVariant)(((index%4)+4)%4);
            ApplyPresentationCreature();
        }
        public bool PlayPresentationTrick() => PresentationCreatures && World.PlayStudyTrick();
        /// <summary>The studies the viewer shows (store corner, grower, Pondlet, customer, employee); their words are the UI's (study.*).</summary>
        public const int PresentationViews = 5;
        Vector3 sampleCameraPosition;
        Quaternion sampleCameraRotation;
        float sampleCameraFov,sampleClock,sampleAngle;
        bool sampleToolVisible;

        void BeginPresentationView()
        {
            ComedyPreview=false;
            sampleCameraPosition=Player.View.transform.localPosition;
            sampleCameraRotation=Player.View.transform.localRotation;
            sampleCameraFov=Player.View.fieldOfView;
            sampleToolVisible=World.ToolRoot.gameObject.activeSelf;
            World.ToolRoot.gameObject.SetActive(false);
            PresentationView=0;PresentationPose=CharacterSamplePose.Idle;PresentationMotion=true;sampleClock=0;sampleAngle=0;
            PresentationSpecies=0;PresentationVariant=DioramaVariant.Classic;ApplyPresentationCreature();
            World.ShowPresentationSample(0,Player.View);
        }
        void EndPresentationView()
        {
            StopComedyPreview();ComedyPreview=false;
            World.HidePresentationSample();
            Player.View.transform.localPosition=sampleCameraPosition;
            Player.View.transform.localRotation=sampleCameraRotation;
            Player.View.fieldOfView=sampleCameraFov;
            World.ToolRoot.gameObject.SetActive(sampleToolVisible);
        }
        public void SelectPresentationView(int index)
        {
            if(Panel!=ShopPanel.Presentation) return;
            PresentationView=Mathf.Clamp(index,0,4);sampleAngle=0;sampleClock=0;
            if(PresentationView==2) ApplyPresentationCreature();
            World.ShowPresentationSample(PresentationView,Player.View);
            World.AnimatePresentationSample(PresentationPose,sampleClock,sampleAngle);
        }
        public void SetPresentationPose(CharacterSamplePose pose)
        {
            if(Panel!=ShopPanel.Presentation || !System.Enum.IsDefined(typeof(CharacterSamplePose),pose)) return;
            PresentationPose=pose;sampleClock=0;
        }
        public void SetPresentationMotion(bool enabled){if(Panel==ShopPanel.Presentation) PresentationMotion=enabled;}
        /// <summary>The turntable angle for the studies from the Pondlet on, in degrees (-180 to 180).</summary>
        public float PresentationAngle => sampleAngle;
        public void SetPresentationAngle(float degrees){if(Panel==ShopPanel.Presentation && !float.IsNaN(degrees)) sampleAngle=Mathf.Clamp(degrees,-180,180);}
        void TickPresentationView()
        {
            if(ComedyPreview){TickComedyPreview();return;}
            if(PresentationMotion) sampleClock+=Time.unscaledDeltaTime;
            World.AnimatePresentationSample(PresentationPose,sampleClock,sampleAngle);
        }
    }
}
