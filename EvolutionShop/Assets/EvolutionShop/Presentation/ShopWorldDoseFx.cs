using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        uint labDoseSeed,labDoseEmitted;
        FxOwnerToken labDoseOwner;
        FxDoseMode labDoseMode;
        FxDoseMix labDoseMix;
        MeshRenderer labDoseFluidRenderer;
        MaterialPropertyBlock labDoseFluidProperties;
        Color labDoseFluidBase;
        static readonly int DoseFluidColor=Shader.PropertyToID("_BaseColor");
        public FxDoseMode LabDoseMode => labDoseMode;
        public FxDoseMix LabDoseMixture => labDoseMix;
        public uint LabDoseSeed => labDoseSeed;
        public bool LabDoseReduced => fxReducedMotion;
        public bool LabDoseLow => fxPool!=null && fxPool.ActionLimit<64;
        public float LabDoseAge => labDoseRemaining>0 ? FxDosePour.Duration-labDoseRemaining : -1;
        public Color LabMixtureTint { get; private set; }
        void BuildDoseFx()
        {
            labDoseFluidRenderer=labFluid ? labFluid.GetComponent<MeshRenderer>() : null;
            if(!labDoseFluidRenderer)return;
            labDoseFluidBase=labDoseFluidRenderer.sharedMaterial.GetColor(DoseFluidColor);
            labDoseFluidProperties=new MaterialPropertyBlock();
        }
        void BeginDoseFx(FxOwnerToken owner,uint sequence,FxCueId id,CultureKind culture)
        {
            labDoseRemaining=FxDosePour.Duration;labDoseOwner=owner;labDoseSeed=sequence;labDoseEmitted=0;
            labDoseId=id;labDoseCulture=culture;labDoseMix=default;
            labDoseMode=id==FxCueId.DoseReturned ? FxDoseMode.Return : FxDoseMode.Pour;
        }
        public void ConfigureDoseContext(FxDoseMix before,FxDoseMix after,FxDoseMode mode)
        {
            if(labDoseRemaining<=0 || labDoseId==FxCueId.HandRefused)return;
            labDoseMode=mode;labDoseMix=mode==FxDoseMode.Clear || mode==FxDoseMode.Return ? before : after;
        }
        void TickDoseFx()
        {
            if(labDoseRemaining<=0 || labDoseId==FxCueId.HandRefused || fxPool==null || !fxCues.IsValid(labDoseOwner) || !displays.TryGetValue("incubator",out var display))return;
            int count=FxDosePour.Count(labDoseMode,LabDoseLow,fxReducedMotion);
            for(int i=0;i<count;i++)
            {
                uint bit=1u<<i;if((labDoseEmitted&bit)!=0)continue;
                FxDosePour.Sample(i,labDoseSeed,labDoseMode,labDoseMix,labDoseCulture,LabDoseLow,fxReducedMotion,out float delay,out var point,out var request);
                if(LabDoseAge<delay)continue;labDoseEmitted|=bit;
                // A stalled frame must not replay a delayed pour.
                if(LabDoseAge>=delay+request.Lifetime)continue;
                request.Lifetime=Mathf.Min(request.Lifetime,labDoseRemaining);
                fxPool.EmitParticle(fxCues,labDoseOwner,display.Root.parent,point,request);
            }
        }
        void RefreshDoseMixture()
        {
            if(lastModel==null || !labDoseFluidRenderer)return;
            var mix=FxDoseMix.Read(lastModel);
            Color target=lastModel.Incubator.Phase==IncubatorPhase.Dosing && mix.Total>0 ? Color.Lerp(labDoseFluidBase,mix.Tint,.60f) : GrowthFluidTint(labDoseFluidBase);
            target.a=labDoseFluidBase.a;if(LabMixtureTint==target)return;
            LabMixtureTint=target;labDoseFluidProperties.SetColor(DoseFluidColor,target);labDoseFluidRenderer.SetPropertyBlock(labDoseFluidProperties);
        }
        void ResetDoseFx() { labDoseOwner=default;labDoseEmitted=labDoseSeed=0;labDoseMix=default;RefreshDoseMixture(); }
    }
}
