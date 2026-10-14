using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxIncubationMotionTests
    {
        struct Pose { public Vector3 position,scale; public Quaternion rotation; }
        static Pose[] Read(Transform[] parts)
        {
            var poses=new Pose[parts.Length];
            for(int i=0;i<parts.Length;i++)poses[i]=new Pose {position=parts[i].localPosition,scale=parts[i].localScale,rotation=parts[i].localRotation};
            return poses;
        }
        static void Same(Transform[] parts,Pose[] expected)
        {
            for(int i=0;i<parts.Length;i++) {
                Assert.That(Vector3.Distance(parts[i].localPosition,expected[i].position),Is.LessThan(.00001f),parts[i].name+" position");
                Assert.That(Vector3.Distance(parts[i].localScale,expected[i].scale),Is.LessThan(.00001f),parts[i].name+" scale");
                Assert.That(Quaternion.Angle(parts[i].localRotation,expected[i].rotation),Is.LessThan(.03f),parts[i].name+" rotation");
            }
        }
        static void Release(GameObject root)
        {
            // Some older species use instance-private palette materials without their own release list.
            var materials=new HashSet<Material>();foreach(var r in root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)if(m)materials.Add(m);
            UnityEngine.Object.DestroyImmediate(root);
            foreach(var material in materials)if(material)UnityEngine.Object.DestroyImmediate(material);
        }
        [TestCase("pondlet")][TestCase("bubblebelly")][TestCase("ribbonfin")][TestCase("lanternfin")][TestCase("driftwing")]
        public void IncubationPoseIsAbsoluteAndHistoryIndependent(string species)
        {
            var root=new GameObject("Actual "+species+" incubation pose");
            try {
                var visual=root.AddComponent<CreatureVisual>();visual.Build(species);var parts=root.GetComponentsInChildren<Transform>();
                visual.AnimateIncubation(2.71f,.38f,false,.43f,true);var expected=Read(parts);var random=UnityEngine.Random.state;
                for(int i=0;i<96;i++)visual.AnimateIncubation(i*.137f,(i%13)/12f,i%3==0,i*.019f,i%2==0);
                visual.AnimateIncubation(2.71f,.38f,false,.43f,true);Same(parts,expected);
                Assert.That(root.GetComponentsInChildren<Transform>().Length,Is.EqualTo(parts.Length));
                Assert.That(root.GetComponentsInChildren<Collider>().Length,Is.Zero);
                Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            }
            finally {Release(root);}
        }
        [TestCase("pondlet")][TestCase("bubblebelly")][TestCase("ribbonfin")][TestCase("lanternfin")][TestCase("driftwing")]
        public void ReducedIncubationStopsTimeDrivenDeformationWithoutAccumulation(string species)
        {
            var root=new GameObject("Reduced "+species);
            try {
                var visual=root.AddComponent<CreatureVisual>();visual.Build(species);var parts=root.GetComponentsInChildren<Transform>();
                visual.AnimateIncubation(0,.7f,true);var expected=Read(parts);
                foreach(float time in new[]{.13f,2.7f,10f,97f,float.NaN,float.PositiveInfinity}) {
                    visual.AnimateIncubation(time,.7f,true);Same(parts,expected);
                }
            }
            finally {Release(root);}
        }
        [TestCase("pondlet")][TestCase("bubblebelly")][TestCase("ribbonfin")][TestCase("lanternfin")][TestCase("driftwing")]
        public void DestructionCallbackReleasesEveryPrivatePaletteMaterial(string species)
        {
            var root=new GameObject("Owned palette "+species);var materials=new HashSet<Material>();
            try {
                var visual=root.AddComponent<CreatureVisual>();visual.Build(species);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
                Assert.That(materials.Count,Is.GreaterThan(0));
                // A synchronous EditMode fixture never enters this ordinary MonoBehaviour's play lifecycle.
                // Test the callback contract directly; actual engine-triggered destruction is in FX09 smoke.
                var callback=typeof(CreatureVisual).GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Assert.That(callback,Is.Not.Null);callback.Invoke(visual,null);UnityEngine.Object.DestroyImmediate(root);
                foreach(var material in materials)Assert.That(material==null,Is.True,"Private palette survived resident destruction / "+species);
            }
            finally {if(root)Release(root);foreach(var material in materials)if(material)UnityEngine.Object.DestroyImmediate(material);}
        }
        [TestCase("pondlet")][TestCase("bubblebelly")][TestCase("ribbonfin")][TestCase("lanternfin")][TestCase("driftwing")]
        public void InterruptedIncubationReturnsToExistingOrdinarySpeciesAnimation(string species)
        {
            var root=new GameObject("Restored "+species);
            try {
                var visual=root.AddComponent<CreatureVisual>();visual.Build(species);var parts=root.GetComponentsInChildren<Transform>();
                visual.Animate(4.31f);var expected=Read(parts);
                visual.AnimateIncubation(1.7f,.2f,false,FxHatchChoreography.Release(true)+.1f,true);
                visual.Animate(4.31f);Same(parts,expected);
            }
            finally {Release(root);}
        }
    }
}
