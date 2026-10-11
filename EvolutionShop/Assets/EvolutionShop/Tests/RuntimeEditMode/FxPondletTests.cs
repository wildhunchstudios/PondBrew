using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxPondletTests
    {
        [Test] public void CruiseAndAccentsStayInAdmittedEnvelope()
        {
            for (int i=0;i<2400;i++)
            {
                var p=PondletMotion.Resolve(i*.01f,1.37f,false,(i%52)*.01f,(i%60)*.01f);
                Assert.That(Mathf.Abs(p.Position.x),Is.LessThanOrEqualTo(.22001f));
                Assert.That(Mathf.Abs(p.Position.y),Is.LessThanOrEqualTo(.05001f));
                Assert.That(p.Position.z,Is.EqualTo(0));
                Assert.That(p.Angles.y,Is.InRange(-100f,-80f));
                Assert.That(Mathf.Abs(p.Angles.x),Is.LessThanOrEqualTo(2));
                Assert.That(Mathf.Abs(p.Angles.z),Is.LessThanOrEqualTo(2));
            }
        }
        [Test] public void CruiseHasSmoothEndpointsAndARealCuriousHold()
        {
            foreach(float t in new[]{.4f,2.6f,3f,3.8f,6.8f,7.6f})
            {
                float before=PondletMotion.Resolve(t-.001f,0,false).Position.x,after=PondletMotion.Resolve(t+.001f,0,false).Position.x;
                Assert.That(Mathf.Abs(after-before),Is.LessThan(.00035f),"No endpoint teleport / "+t);
            }
            var hold=PondletMotion.Resolve(3.4f,0,false);
            Assert.That(hold.Effort,Is.EqualTo(0)); Assert.That(hold.Position.x,Is.EqualTo(.22f).Within(.00001f));
        }
        [Test] public void IdentityIsStableAndDoesNotUseGlobalRandom()
        {
            var prior=UnityEngine.Random.state;
            Assert.That(PondletMotion.IdentityPhase("specimen-a"),Is.EqualTo(PondletMotion.IdentityPhase("specimen-a")));
            Assert.That(PondletMotion.IdentityPhase("specimen-b"),Is.Not.EqualTo(PondletMotion.IdentityPhase("specimen-a")));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(prior));
        }
        [Test] public void ReducedMotionRemovesTravelAndAccents()
        {
            var root=PondletMotion.Resolve(0,0,true);
            for(int i=0;i<120;i++){var p=PondletMotion.Resolve(i,2.8f,true,.18f,.3f);Assert.That(p.Position,Is.EqualTo(root.Position));Assert.That(p.Angles,Is.EqualTo(root.Angles));}
        }
        [Test] public void PetalsKeepTheAuthoredMirroredRestAndSmallSweep()
        {
            var root=new GameObject("Pondlet intended fin invariant");
            try
            {
                var v=root.AddComponent<CreatureVisual>();v.Build("pondlet");
                var left=root.transform.Find("Left petal");var right=root.transform.Find("Right petal");
                var leftRest=Quaternion.Euler(0,180,0);
                for(int i=0;i<180;i++){v.AnimatePondlet(i*.1f,(i%10)/10f,false,.3f);Assert.That(Quaternion.Angle(leftRest,left.localRotation),Is.LessThanOrEqualTo(12.01f));Assert.That(Quaternion.Angle(Quaternion.identity,right.localRotation),Is.LessThanOrEqualTo(12.01f));}
                v.AnimatePondlet(17,1,true);Assert.That(Quaternion.Angle(leftRest,left.localRotation),Is.LessThan(.01f));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void CurveSamplingDoesNotAllocatePerFrame()
        {
            PondletMotion.Resolve(0,1,false);long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;
            for(int i=0;i<10000;i++)sum+=PondletMotion.Resolve(i*.01f,1,false).Position.x;
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.That(bytes,Is.EqualTo(0));Assert.That(float.IsNaN(sum),Is.False);
        }
    }
}
