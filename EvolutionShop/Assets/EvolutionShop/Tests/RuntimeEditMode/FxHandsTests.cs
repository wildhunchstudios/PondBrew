using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxHandsTests
    {
        [Test] public void ActualImportedRigHasPositiveScaleAndBoundedResources()
        {
            var root=Resources.Load<GameObject>("Art/LivingShopFX/HandsPair");Assert.That(root,Is.Not.Null);
            Assert.That(Quaternion.Angle(root.transform.localRotation,Quaternion.identity),Is.LessThan(.01f));
            var renderers=root.GetComponentsInChildren<Renderer>(true);Assert.That(renderers.Length,Is.EqualTo(12));
            var names=new HashSet<string>();int triangles=0;
            foreach(var t in root.GetComponentsInChildren<Transform>(true)){Assert.That(t.localScale.x,Is.GreaterThan(0));Assert.That(t.localScale.y,Is.GreaterThan(0));Assert.That(t.localScale.z,Is.GreaterThan(0));Assert.That(names.Add(t.name),Is.True);}
            foreach(var side in new[]{"Right","Left"})foreach(var part in new[]{"Wrist","ThumbBase","ThumbTip","IndexBase","IndexTip","Fingers","Cuff"})Assert.That(names.Contains(side+part),Is.True,side+part);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){Assert.That(filter.sharedMesh.isReadable,Is.True);triangles+=filter.sharedMesh.triangles.Length/3;}
            Assert.That(triangles,Is.InRange(1,6000));Assert.That(root.GetComponentsInChildren<Animator>(true),Is.Empty);Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
        }
        [Test] public void ActualImportedWristSocketsMatchAuthoredToolSpace()
        {
            var root=Resources.Load<GameObject>("Art/LivingShopFX/HandsPair");
            foreach(var item in new[]{new KeyValuePair<string,Vector3>("RightWrist",new Vector3(.057f,-.245f,-.098f)),new KeyValuePair<string,Vector3>("LeftWrist",new Vector3(-.092f,-.20f,.082f))})
            {Transform found=null;foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==item.Key)found=t;Assert.That(found,Is.Not.Null);Assert.That(Vector3.Distance(root.transform.InverseTransformPoint(found.position),item.Value),Is.LessThan(.0001f));}
        }
        [Test] public void EveryReducedActionLeavesToolAndSupportStationary()
        {
            foreach(FxCueId id in new[]{FxCueId.HandCollectStart,FxCueId.CollectedUnit,FxCueId.HandDose,FxCueId.HandPlace,FxCueId.HandUpgrade,FxCueId.HandRefused})
            for(int i=0;i<=100;i++){var pose=FxHandPose.Resolve(id,i*.006f,true,12,true);Assert.That(pose.ToolOffset,Is.EqualTo(Vector3.zero));Assert.That(pose.ToolAngles,Is.EqualTo(Vector3.zero));Assert.That(pose.LeftOffset,Is.EqualTo(Vector3.zero));Assert.That(pose.RightCurl,Is.InRange(0,12));}
        }
        [Test] public void ShortReleaseStartsAtActualPartialCurlAndSettlesWithoutOvershoot()
        {
            float curl=FxHandPose.Resolve(FxCueId.HandCollectStart,.04f,true,0,false).RightCurl;Assert.That(curl,Is.InRange(0.01f,11.99f));
            float previous=curl;for(int i=0;i<=22;i++){float next=FxHandPose.Resolve(FxCueId.HandCollectEnd,i*.01f,false,curl,false).RightCurl;Assert.That(next,Is.InRange(0,previous+.00001f));previous=next;}Assert.That(previous,Is.EqualTo(0));
        }
        [Test] public void ResolvedActionsSettleWithoutResidualOffsets()
        {
            foreach(var id in new[]{FxCueId.HandCollectStart,FxCueId.CollectedUnit,FxCueId.HandDose,FxCueId.HandPlace,FxCueId.HandUpgrade,FxCueId.HandRefused})
            {var pose=FxHandPose.Resolve(id,1,false,0,false);Assert.That(pose.ToolOffset,Is.EqualTo(Vector3.zero));Assert.That(pose.ToolAngles,Is.EqualTo(Vector3.zero));Assert.That(pose.LeftOffset,Is.EqualTo(Vector3.zero));Assert.That(pose.LeftRoll,Is.EqualTo(0));Assert.That(pose.RightRoll,Is.EqualTo(0));}
        }
    }
}
