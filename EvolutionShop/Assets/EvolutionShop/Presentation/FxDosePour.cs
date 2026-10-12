using UnityEngine;

namespace EvolutionShop
{
    public enum FxDoseMode { Pour, Return, Clear, Recipe, Refused }
    public struct FxDoseMix
    {
        public int Bloom, Flow, Spark, Mist;
        public int Total => Bloom + Flow + Spark + Mist;
        public static FxDoseMix Read(GameModel model) => new FxDoseMix { Bloom=model.Incubator.Bloom,Flow=model.Incubator.Flow,Spark=model.Incubator.Spark,Mist=model.Incubator.Mist };
        public bool Same(FxDoseMix other) => Bloom==other.Bloom && Flow==other.Flow && Spark==other.Spark && Mist==other.Mist;
        public CultureKind At(int unit)
        {
            unit = Total>0 ? (unit%Total+Total)%Total : 0;
            if(unit<Bloom)return CultureKind.Bloom;unit-=Bloom;
            if(unit<Flow)return CultureKind.Flow;unit-=Flow;
            return unit<Spark ? CultureKind.Spark : CultureKind.Mist;
        }
        public Color Tint => Total>0 ? (FxDosePour.ColorOf(CultureKind.Bloom)*Bloom+FxDosePour.ColorOf(CultureKind.Flow)*Flow+FxDosePour.ColorOf(CultureKind.Spark)*Spark+FxDosePour.ColorOf(CultureKind.Mist)*Mist)/Total : new Color(.28f,.77f,.8f);
    }
    /// <summary>One bounded dose composition, shared by world and measured-panel presentation.</summary>
    public static class FxDosePour
    {
        public const float Duration=.58f;
        public static Color ColorOf(CultureKind culture) => culture==CultureKind.Flow ? new Color(.22f,.83f,.91f) : culture==CultureKind.Spark ? new Color(.76f,.47f,.98f) : culture==CultureKind.Mist ? new Color(.68f,.96f,.87f) : new Color(1,.51f,.38f);
        public static int Count(FxDoseMode mode,bool low,bool reduced) => mode==FxDoseMode.Refused ? 0 : reduced ? 1 : low ? 8 : mode==FxDoseMode.Return ? 10 : 16;
        static float Noise(int index,uint seed,uint salt)
        { uint x=unchecked((uint)(index+1)*747796405u+seed*2891336453u+salt);x=unchecked(((x>>(int)((x>>28)+4))^x)*277803737u);return ((x>>22)^x)/(float)uint.MaxValue; }
        public static void Sample(int index,uint seed,FxDoseMode mode,FxDoseMix mix,CultureKind single,bool low,bool reduced,out float delay,out Vector3 origin,out FxParticleRequest p)
        {
            float a=Noise(index,seed,17),b=Noise(index,seed,919),c=Noise(index,seed,109);
            bool outward=mode==FxDoseMode.Return || mode==FxDoseMode.Clear;
            bool impact=!outward && index>=Count(mode,low,reduced)-4 && !reduced;
            CultureKind culture=(mode==FxDoseMode.Clear || mode==FxDoseMode.Recipe) && mix.Total>0 ? mix.At(index) : single;
            p=FxParticleRequest.Create(impact ? (index%2==0 ? FxParticleShape.Ring : FxParticleShape.Bubble) : FxParticleShape.Droplet,ColorOf(culture));
            Vector3 inlet=new Vector3(.34f+(a-.5f)*.07f,1.78f+(b-.5f)*.045f,-.55f);
            Vector3 landing=new Vector3(-.08f+(c-.5f)*.14f,1.35f+(a-.5f)*.08f,-.56f);
            origin=outward ? landing : inlet;
            p.Motion=FxParticleMotion.Curve;p.End=(outward ? inlet : landing)-origin;
            p.Bend=new Vector3(-.10f-c*.07f,.08f,-.025f);
            delay=.01f+a*.18f;p.Lifetime=.27f+b*.07f;p.Size=.045f+c*.035f;p.EndSize=p.Size*.65f;
            if(impact)
            {
                origin=landing;delay=.20f+a*.05f;p.Lifetime=.30f;
                p.End=new Vector3((b-.5f)*.24f,.03f+c*.14f,-.015f);p.Bend=new Vector3((a-.5f)*.06f,.025f,0);
                p.Size=.09f;p.EndSize=index%2==0 ? .29f : .13f;p.Color.a=.75f;
            }
            if(reduced)
            { origin=new Vector3(-.12f,1.38f,-.59f);delay=0;p.Motion=FxParticleMotion.Drift;p.End=p.Bend=Vector3.zero;p.Size=p.EndSize=.15f;p.Lifetime=.4f;p.Shape=FxParticleShape.Ring; }
        }
        public static Vector3 Point(Vector3 origin,FxParticleRequest p,float normalized) => origin+(p.Motion==FxParticleMotion.Drift ? Vector3.zero : p.End*normalized+p.Bend*(4*normalized*(1-normalized)));
    }
}
