using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxHatchChoreographyTests
    {
        [TestCase(true)] [TestCase(false)]
        public void WholeWakeIsBoundedContinuousAndSettles(bool first)
        {
            float duration=FxHatchChoreography.Duration(first); var previous=FxHatchChoreography.Resolve(0,first,false);
            float minY=1,maxY=1;
            for(int i=0;i<=2000;i++)
            {
                var p=FxHatchChoreography.Resolve(i*duration/2000,first,false);
                minY=Mathf.Min(minY,p.Scale.y);maxY=Mathf.Max(maxY,p.Scale.y);
                Assert.That(p.Scale.y,Is.InRange(.8799f,1.1201f));Assert.That(p.Scale.x,Is.InRange(.9549f,1.0451f));
                Assert.That(Mathf.Abs(p.Fin),Is.LessThanOrEqualTo(24.001f));Assert.That(Mathf.Abs(p.Roll),Is.LessThanOrEqualTo(3.501f));
                Assert.That(p.Lift,Is.InRange(0,.03001f));Assert.That(p.FluidScale,Is.InRange(.9749f,1));
                Assert.That(Vector3.Distance(previous.Scale,p.Scale),Is.LessThan(.006));previous=p;
            }
            Assert.That(minY,Is.LessThan(.93f));Assert.That(maxY,Is.GreaterThan(1.07f));
            Assert.That(previous.Scale,Is.EqualTo(Vector3.one));Assert.That(previous.Fin,Is.Zero);
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(2f)]
        public void InvalidOrCompletedClockReturnsRest(float age)
        { var p=FxHatchChoreography.Resolve(age,true,false);Assert.That(p.Scale,Is.EqualTo(Vector3.one));Assert.That(p.Fin,Is.Zero); }
        [Test] public void ReducedRemovesSquashTravelAndFinAccent()
        {
            for(int i=0;i<150;i++){var p=FxHatchChoreography.Resolve(i*.01f,true,true);Assert.That(p.Scale,Is.EqualTo(Vector3.one));Assert.That(p.Lift+p.Roll+p.Fin+p.Tail,Is.Zero);}
        }
        [TestCase(true,.95f)] [TestCase(false,.65f)]
        public void RealHatchSamplesAreQuietBeforeReleaseBoundedAndClickFree(bool first,float duration)
        {
            var method=typeof(ShopAudio).GetMethod("MakeHatchSamples",BindingFlags.Static|BindingFlags.NonPublic);
            var data=(float[])method.Invoke(null,new object[]{first,duration});double early=0,late=0,sum=0,sq=0;float peak=0;
            for(int i=0;i<data.Length;i++)
            {
                Assert.That(float.IsNaN(data[i])||float.IsInfinity(data[i]),Is.False);sum+=data[i];sq+=data[i]*data[i];peak=Math.Max(peak,Math.Abs(data[i]));
                if(i/22050f<FxHatchChoreography.Release(first))early+=data[i]*data[i];else late+=data[i]*data[i];
            }
            Assert.That(late,Is.GreaterThan(early*8));Assert.That(peak,Is.LessThanOrEqualTo(.20001f));
            Assert.That(Math.Sqrt(sq/data.Length),Is.LessThan(.12));Assert.That(Math.Abs(sum/data.Length),Is.LessThan(.005));
            Assert.That(data[0],Is.Zero);Assert.That(data[data.Length-1],Is.Zero);
        }
    }
}
