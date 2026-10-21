using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public int PresentationView { get; private set; }
        public CharacterSamplePose PresentationPose { get; private set; }
        public bool PresentationMotion { get; private set; }=true;
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
