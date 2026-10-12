using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxDosePourTests
    {
        [TestCase(FxDoseMode.Pour)] [TestCase(FxDoseMode.Return)] [TestCase(FxDoseMode.Clear)] [TestCase(FxDoseMode.Recipe)]
        public void SamplesStayBoundedAndFinishWithinOneCue(FxDoseMode mode)
        {
            var mix=new FxDoseMix{Bloom=1,Flow=2,Spark=1,Mist=2};var state=Random.state;
            for(int c=0;c<4;c++)for(int comfort=0;comfort<3;comfort++)for(uint seed=1;seed<9;seed++)
            {
                bool low=comfort==1,reduced=comfort==2;
                int count=FxDosePour.Count(mode,low,reduced);Assert.That(count,Is.InRange(1,16));
                for(int i=0;i<count;i++)
                {
                    FxDosePour.Sample(i,seed,mode,mix,(CultureKind)c,low,reduced,out float delay,out var start,out var p);
                    Assert.That(p.IsValid,Is.True);Assert.That(delay+p.Lifetime,Is.LessThanOrEqualTo(FxDosePour.Duration));
                    for(int t=0;t<=20;t++)
                    {var v=FxDosePour.Point(start,p,t/20f);Assert.That(v.x,Is.InRange(-.4f,.5f));Assert.That(v.y,Is.InRange(1.1f,2));Assert.That(v.z,Is.InRange(-.7f,-.4f));}
                    if(reduced)Assert.That(FxDosePour.Point(start,p,.7f),Is.EqualTo(start));
                }
            }
            Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(JsonUtility.ToJson(state)));
        }
        [Test] public void ReturnTravelsOutAndPourTravelsIntoTheSameMeasuredVessel()
        {
            FxDosePour.Sample(0,3,FxDoseMode.Pour,default,CultureKind.Flow,false,false,out _,out var start,out var p);
            FxDosePour.Sample(0,3,FxDoseMode.Return,default,CultureKind.Flow,false,false,out _,out var returned,out var r);
            Assert.That(start+p.End,Is.EqualTo(returned));Assert.That(returned+r.End,Is.EqualTo(start));
            Assert.That(p.End.y,Is.LessThan(0));Assert.That(r.End.y,Is.GreaterThan(0));
            Assert.That(FxDosePour.Count(FxDoseMode.Refused,false,false),Is.Zero);
        }
        [Test] public void RecipeAndClearColorsReflectEveryActualCultureWithoutInventingOne()
        {
            var mix=new FxDoseMix{Bloom=2,Flow=1,Spark=1,Mist=1};
            Assert.That(mix.Total,Is.EqualTo(5));Assert.That(mix.At(0),Is.EqualTo(CultureKind.Bloom));Assert.That(mix.At(2),Is.EqualTo(CultureKind.Flow));
            Assert.That(mix.At(3),Is.EqualTo(CultureKind.Spark));Assert.That(mix.At(4),Is.EqualTo(CultureKind.Mist));
            Color expected=(FxDosePour.ColorOf(CultureKind.Bloom)*2+FxDosePour.ColorOf(CultureKind.Flow)+FxDosePour.ColorOf(CultureKind.Spark)+FxDosePour.ColorOf(CultureKind.Mist))/5;
            Assert.That(mix.Tint,Is.EqualTo(expected));
            for(int i=0;i<5;i++){FxDosePour.Sample(i,1,FxDoseMode.Clear,mix,CultureKind.Bloom,false,false,out _,out _,out var p);Assert.That(p.Color,Is.EqualTo(FxDosePour.ColorOf(mix.At(i))));}
        }
    }
}
