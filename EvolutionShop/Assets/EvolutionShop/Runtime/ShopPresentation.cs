using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public int PresentationView { get; private set; }
        public CharacterSamplePose PresentationPose { get; private set; }
        public bool PresentationMotion { get; private set; }=true;
        static readonly string[] sampleTitles={"Little Wonders / store corner","Petal Works / Bloom grower","Pondlet / the first little life","Casual customer / curious visitor","Employee / model and motion study"};
        static readonly string[] sampleNotes={
            "Honey wood, teal enamel and a warm aquarium canopy. This is your actual first display; its stock and price stay yours.",
            "Cream porcelain, a coral cap and a pressure dial. Collect Bloom here in normal play; the details move with the station.",
            "Coral clay, a custard chin, petal fins and a heart-shaped tail. This study uses the same model as your living stock.",
            "An ochre cardigan, a teal beret and expressive eyes. The same model browses, queues and celebrates purchases in your store.",
            "A linen apron and work cap share the customer rig. Idle, walk, greet and carry are motion studies; staffing comes later."};
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
        void TickPresentationView()
        {
            if(ComedyPreview){TickComedyPreview();return;}
            if(PresentationMotion) sampleClock+=Time.unscaledDeltaTime;
            World.AnimatePresentationSample(PresentationPose,sampleClock,sampleAngle);
        }
        void DrawPresentationView()
        {
            if(ComedyPreview){DrawComedyPreview();return;}
            Box(new Rect(24,22,1232,83),ink);Box(new Rect(24,22,4,83),mint);
            GUI.Label(new Rect(44,29,1000,23),"PRESENTATION STUDY  /  "+(PresentationView+1)+" OF 5  /  YOUR SHOP IS PAUSED",smallStyle);
            GUI.Label(new Rect(44,53,1090,42),sampleTitles[PresentationView],titleStyle);
            Box(new Rect(24,542,1232,154),ink);
            GUI.Label(new Rect(44,554,1190,46),sampleNotes[PresentationView],bodyStyle);
            if(PresentationView>=2)
            {
                GUI.Label(new Rect(44,603,80,28),"Rotate",smallStyle);
                sampleAngle=GUI.HorizontalSlider(new Rect(111,610,200,20),sampleAngle,-180,180);
                if(GUI.Button(new Rect(330,599,158,34),PresentationMotion?"Freeze motion":"Play motion",buttonStyle)) SetPresentationMotion(!PresentationMotion);
                if(PresentationView>=3)
                {
                    for(int i=0;i<4;i++)
                    {
                        var pose=(CharacterSamplePose)i;
                        if(GUI.Button(new Rect(511+i*175,599,165,34),(PresentationPose==pose?"• ":"")+pose,buttonStyle))SetPresentationPose(pose);
                    }
                }
            }
            else GUI.Label(new Rect(44,604,1140,27),"Look-only preview. Return to your shop to interact, grow and sell.",smallStyle);
            if(GUI.Button(new Rect(44,646,200,34),"Previous",buttonStyle))SelectPresentationView((PresentationView+4)%5);
            if(GUI.Button(new Rect(256,646,200,34),"Next sample",buttonStyle))SelectPresentationView((PresentationView+1)%5);
            if(GUI.Button(new Rect(914,646,320,34),"Back to pause  /  ESC",buttonStyle))SetPanel(ShopPanel.Pause);
        }
    }
}
