using System;
using System.Reflection;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class FxLabAudioTests
    {
        [TestCase(0,.13f)][TestCase(1,.23f)][TestCase(2,.32f)][TestCase(3,.85f)][TestCase(4,.45f)][TestCase(5,.12f)]
        public void OriginalLabPcmFitsDeclaredShortCueEnvelope(int kind,float duration)
        {
            var make=typeof(ShopAudio).GetMethod("MakeLabSamples",BindingFlags.NonPublic|BindingFlags.Static);
            var data=(float[])make.Invoke(null,new object[]{kind,duration});
            double square=0,sum=0;float peak=0;
            foreach(float v in data){Assert.That(float.IsNaN(v)||float.IsInfinity(v),Is.False);square+=v*v;sum+=v;peak=Math.Max(peak,Math.Abs(v));}
            Assert.That(data.Length,Is.EqualTo((int)(22050*duration)));Assert.That(peak,Is.LessThanOrEqualTo(.25f));
            Assert.That(Math.Sqrt(square/data.Length),Is.LessThanOrEqualTo(.12));Assert.That(Math.Abs(sum/data.Length),Is.LessThan(.005));
            Assert.That(data[0],Is.Zero);Assert.That(data[data.Length-1],Is.Zero);
        }
    }
}
