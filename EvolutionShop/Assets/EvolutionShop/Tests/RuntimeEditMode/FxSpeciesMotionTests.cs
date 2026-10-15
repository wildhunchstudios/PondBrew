using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxSpeciesMotionTests
    {
        static readonly string[] Species = { "pondlet", "bubblebelly", "ribbonfin", "lanternfin", "driftwing" };
        static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);

        [Test] public void IdentityVariesPresentationDeterministicallyWithoutGlobalRandom()
        {
            var random = UnityEngine.Random.state;
            float a = CreatureMotion.IdentityPhase("saved-specimen-a"), b = CreatureMotion.IdentityPhase("saved-specimen-b");
            Assert.That(a, Is.EqualTo(CreatureMotion.IdentityPhase("saved-specimen-a")));
            Assert.That(a, Is.Not.EqualTo(b));
            foreach (string species in Species) {
                float difference = 0;
                for (int i = 0; i < 20; i++) difference += (CreatureMotion.Resolve(species, i * .37f, a, false).Position - CreatureMotion.Resolve(species, i * .37f, b, false).Position).sqrMagnitude;
                Assert.That(difference, Is.GreaterThan(.00000001f), species + " actual phase variation");
            }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        }

        [TestCase("pondlet")][TestCase("bubblebelly")][TestCase("ribbonfin")][TestCase("lanternfin")][TestCase("driftwing")]
        public void TravelRemainsFiniteBoundedAndContinuousThroughTurnsAndWrap(string species)
        {
            float period = CreatureMotion.Period(species);
            Assert.That(period, Is.InRange(7.6f, 20f));
            float traveled = 0;
            var previous = CreatureMotion.Resolve(species, -.001f, 1.37f, false);
            for (int i = 0; i <= 2400; i++) {
                float t = i * period / 1200;
                var p = CreatureMotion.Resolve(species, t, 1.37f, false);
                Assert.That(Finite(p.Position) && Finite(p.Angles), Is.True, species);
                Assert.That(Mathf.Abs(p.Position.x), Is.LessThanOrEqualTo(.30001f));
                Assert.That(Mathf.Abs(p.Position.y), Is.LessThanOrEqualTo(.10001f));
                Assert.That(Mathf.Abs(p.Position.z), Is.LessThanOrEqualTo(.08001f));
                Assert.That(p.Angles.y, Is.InRange(-120f, -60f));
                Assert.That(p.Effort, Is.InRange(0f, 1f));
                Assert.That(Vector3.Distance(p.Position, previous.Position), Is.LessThan(.012f), "No travel jump / " + t);
                Assert.That(Quaternion.Angle(Quaternion.Euler(p.Angles), Quaternion.Euler(previous.Angles)), Is.LessThan(1f), "No turn jump / " + t);
                traveled += Vector3.Distance(p.Position, previous.Position); previous = p;
            }
            Assert.That(traveled, Is.GreaterThan(.3f), species + " visibly travels");
            // Period describes travel/hold, while bob and appendage subcycles remain continuous.
            foreach (float seam in new[] { 0f, period * .5f, period, period * 2 }) {
                var before = CreatureMotion.Resolve(species, seam - .001f, 0, false);
                var after = CreatureMotion.Resolve(species, seam + .001f, 0, false);
                Assert.That(Vector3.Distance(before.Position, after.Position), Is.LessThan(.002f), "Wrap/turn position / " + seam);
                Assert.That(Quaternion.Angle(Quaternion.Euler(before.Angles), Quaternion.Euler(after.Angles)), Is.LessThan(.2f), "Wrap/turn rotation / " + seam);
            }
        }

        [Test] public void SpeciesHaveDifferentObservableMovementAndReducedMotionRests()
        {
            for (int a = 0; a < Species.Length; a++) {
                var rest = CreatureMotion.Resolve(Species[a], 0, 0, true);
                for (int i = 0; i < 40; i++) {
                    var p = CreatureMotion.Resolve(Species[a], i * .63f, 2.4f, true);
                    Assert.That(p.Position, Is.EqualTo(rest.Position)); Assert.That(p.Angles, Is.EqualTo(rest.Angles));
                }
                for (int b = a + 1; b < Species.Length; b++) {
                    float difference = 0;
                    for (int i = 0; i < 32; i++) difference += (CreatureMotion.Resolve(Species[a], i * .41f, 0, false).Position - CreatureMotion.Resolve(Species[b], i * .41f, 0, false).Position).sqrMagnitude;
                    Assert.That(difference, Is.GreaterThan(.001f), Species[a] + " differs from " + Species[b]);
                }
            }
        }

        [TestCase("pondlet")][TestCase("bubblebelly")][TestCase("ribbonfin")][TestCase("lanternfin")][TestCase("driftwing")]
        public void DetailPoseIsAbsolutePreservesRootAndDoesNotCreateResources(string species)
        {
            var root = new GameObject("FX12 original " + species);
            try {
                var visual = root.AddComponent<CreatureVisual>(); visual.Build(species); visual.SetPondletIdentity("stable-detail-id");
                var transforms = root.GetComponentsInChildren<Transform>();
                var filters = root.GetComponentsInChildren<MeshFilter>(); var meshes = new Mesh[filters.Length];
                for (int i = 0; i < filters.Length; i++) meshes[i] = filters[i].sharedMesh;
                var renderers = root.GetComponentsInChildren<Renderer>(); var materials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].sharedMaterials;
                root.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(4, 5, 6)); root.transform.localScale = Vector3.one * .69f;
                visual.AnimateSpecies(3.2f, .7f, false);
                var rotations = new Quaternion[transforms.Length]; var scales = new Vector3[transforms.Length]; var positions = new Vector3[transforms.Length];
                for (int i = 0; i < transforms.Length; i++) { rotations[i] = transforms[i].localRotation; scales[i] = transforms[i].localScale; positions[i] = transforms[i].localPosition; }
                for (int i = 0; i < 120; i++) visual.AnimateSpecies(i * .2f, (i % 11) / 10f, false);
                visual.AnimateSpecies(3.2f, .7f, false);
                for (int i = 0; i < transforms.Length; i++) { Assert.That(Quaternion.Angle(rotations[i], transforms[i].localRotation), Is.LessThan(.05f)); Assert.That(transforms[i].localScale, Is.EqualTo(scales[i])); Assert.That(transforms[i].localPosition, Is.EqualTo(positions[i])); }
                Assert.That(root.transform.position, Is.EqualTo(new Vector3(1, 2, 3))); Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(4, 5, 6)), Is.LessThan(.01f));
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one * .69f));
                Assert.That(root.GetComponentsInChildren<Transform>().Length, Is.EqualTo(transforms.Length));
                for (int i = 0; i < filters.Length; i++) Assert.That(filters[i].sharedMesh, Is.SameAs(meshes[i]));
                for (int i = 0; i < renderers.Length; i++) CollectionAssert.AreEqual(materials[i], renderers[i].sharedMaterials);
                visual.AnimateSpecies(0, 1, true);
                for (int i = 0; i < transforms.Length; i++) { rotations[i] = transforms[i].localRotation; scales[i] = transforms[i].localScale; positions[i] = transforms[i].localPosition; }
                visual.AnimateSpecies(91, 0, true);
                for (int i = 0; i < transforms.Length; i++) { Assert.That(Quaternion.Angle(rotations[i], transforms[i].localRotation), Is.LessThan(.05f)); Assert.That(transforms[i].localScale, Is.EqualTo(scales[i])); Assert.That(transforms[i].localPosition, Is.EqualTo(positions[i])); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void BlinkAndWingStrokeHaveCalmHoldsAndContinuousBoundaries()
        {
            int open = 0, glide = 0;
            for (int i = 0; i < 640; i++) {
                float blink = CreatureMotion.Blink(i * .01f, 0, false), wing = CreatureMotion.WingStroke(i * 2.8f / 640);
                Assert.That(blink, Is.InRange(.0999f, 1f)); Assert.That(wing, Is.InRange(0f, 1f));
                if (blink > .999f) open++; if (wing < .001f) glide++;
                Assert.That(CreatureMotion.Blink(i, 3, true), Is.EqualTo(1f));
            }
            Assert.That(open, Is.GreaterThan(600)); Assert.That(glide, Is.GreaterThan(350));
            foreach (float t in new[] { 0f, .18f, 6.4f }) Assert.That(Mathf.Abs(CreatureMotion.Blink(t - .001f, 0, false) - CreatureMotion.Blink(t + .001f, 0, false)), Is.LessThan(.002f));
            foreach (float t in new[] { 0f, 2.8f * .42f, 2.8f }) Assert.That(Mathf.Abs(CreatureMotion.WingStroke(t - .001f) - CreatureMotion.WingStroke(t + .001f)), Is.LessThan(.002f));
        }

        [Test] public void WarmedSpeciesSamplingAllocatesNoManagedMemory()
        {
            foreach (string species in Species) CreatureMotion.Resolve(species, 0, 1, false);
            long before = GC.GetAllocatedBytesForCurrentThread(); float sum = 0;
            for (int i = 0; i < 10000; i++) sum += CreatureMotion.Resolve(Species[i % 5], i * .01f, 1, false).Position.x;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.EqualTo(0)); Assert.That(float.IsNaN(sum), Is.False);
        }
    }
}
