using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace EvolutionShop.Tests
{
    public sealed class FxGrowerMotionTests
    {
        [Test] public void InteractionRequiresAnActualAttemptAndNeverOverridesStockTruth()
        {
            foreach (int units in new[] { 0, 3, 6 }) {
                var stock = FxGrowthMotion.ReadGrower(units, 6, 1, 2);
                foreach (bool full in new[] { false, true }) foreach (bool failed in new[] { false, true }) foreach (bool clear in new[] { false, true })
                    Assert.That(FxGrowerMotion.ReadInteraction(stock, false, true, full, failed, clear), Is.EqualTo(FxGrowerInteraction.Idle));
                Assert.That(FxGrowerMotion.ReadInteraction(stock, true, true, true, true, false), Is.EqualTo(FxGrowerInteraction.SaveBlocked));
                Assert.That(FxGrowerMotion.ReadInteraction(stock, true, true, true, false, false), Is.EqualTo(FxGrowerInteraction.ReservoirFull));
                Assert.That(FxGrowerMotion.ReadInteraction(stock, true, true, false, false, true),
                    Is.EqualTo(units == 0 ? FxGrowerInteraction.Idle : FxGrowerInteraction.Collecting));
                Assert.That(FxGrowerMotion.ReadInteraction(stock, true, true, false, false, false),
                    Is.EqualTo(units == 0 ? FxGrowerInteraction.Idle : FxGrowerInteraction.PathObstructed));
                Assert.That(stock.Fill, Is.EqualTo(units / 6f), "The interaction is separate from actual availability");
            }
        }
        [TestCase(CultureKind.Bloom)] [TestCase(CultureKind.Flow)]
        public void FullStopsCirculationAndDepletedUsesQuietNonCultureCooling(CultureKind culture)
        {
            var full = FxGrowthMotion.ReadGrower(6, 6, .1f, 2);
            Assert.That(FxGrowerMotion.SampleCirculation(culture, 5, full, false, false, out _, out _, out _), Is.False);
            var empty = FxGrowthMotion.ReadGrower(0, 6, .1f, 2); var partial = FxGrowthMotion.ReadGrower(3, 6, 1, 2);
            Assert.That(FxGrowerMotion.SampleCirculation(culture, 5, empty, false, false, out float quiet, out _, out var cooling), Is.True);
            Assert.That(FxGrowerMotion.SampleCirculation(culture, 5, partial, false, false, out float active, out _, out var circulation), Is.True);
            Assert.That(cooling.Shape, Is.EqualTo(FxParticleShape.Bubble));
            Assert.That(cooling.Size, Is.LessThan(circulation.Size)); Assert.That(cooling.Color.a, Is.LessThan(circulation.Color.a));
            Assert.That(quiet, Is.GreaterThan(active)); Assert.That(empty.Fill, Is.Zero);
            Assert.That(FxGrowerMotion.SampleCirculation(CultureKind.Spark, 5, partial, false, false, out _, out _, out _), Is.False);
        }
        [Test] public void RecipesAreDeterministicFiniteAndRespectActualVesselAndMediumEnvelope()
        {
            var random = Random.state;
            foreach (CultureKind culture in new[] { CultureKind.Bloom, CultureKind.Flow })
            foreach (int units in new[] { 0, 1, 3, 5 }) foreach (bool reduced in new[] { false, true })
            for (uint sequence = 1; sequence <= 24; sequence++) {
                var stock = FxGrowthMotion.ReadGrower(units, 6, .8f, 2);
                Assert.That(FxGrowerMotion.SampleCirculation(culture, sequence, stock, false, reduced, out float interval, out var start, out var request), Is.True);
                FxGrowerMotion.SampleCirculation(culture, sequence, stock, false, reduced, out float again, out var sameStart, out var sameRequest);
                Assert.That(again, Is.EqualTo(interval)); Assert.That(sameStart, Is.EqualTo(start)); Assert.That(sameRequest, Is.EqualTo(request));
                Assert.That(interval, Is.GreaterThan(0)); Assert.That(request.IsValid, Is.True);
                AssertPathInsideExistingMedium(start, request, stock.Fill);
                foreach (bool accepted in new[] { false, true }) {
                    FxGrowerMotion.SampleAccent(culture, sequence, stock, accepted, reduced, out var origin, out var accent);
                    Assert.That(accent.IsValid, Is.True); AssertPathInsideExistingMedium(origin, accent, stock.Fill);
                }
            }
            Assert.That(Random.state, Is.EqualTo(random));
        }
        static void AssertPathInsideExistingMedium(Vector3 origin, FxParticleRequest request, float fill)
        {
            // These are the existing cylinder/medium dimensions, independent of the new sampler.
            float surface = .93f + .88f * Mathf.Lerp(.55f, 1, fill);
            float radius = Mathf.Max(request.Size, request.EndSize) * .5f;
            for (int step = 0; step <= 32; step++) {
                float f = step / 32f;
                Vector3 point = request.Motion == FxParticleMotion.Drift ? origin + request.Velocity * (request.Lifetime * f) :
                    origin + request.End * f + request.Bend * (4 * f * (1 - f));
                Assert.That(new Vector2(point.x, point.z).magnitude + radius, Is.LessThan(.46f));
                Assert.That(point.y - radius, Is.GreaterThan(.93f)); Assert.That(point.y + radius, Is.LessThan(surface));
            }
        }
        [Test] public void CulturesRetainDistinctVisualLanguageWhileLowReducesFrequency()
        {
            var stock = FxGrowthMotion.ReadGrower(3, 6, 1, 2);
            for (uint sequence = 1; sequence <= 12; sequence++) {
                FxGrowerMotion.SampleCirculation(CultureKind.Bloom, sequence, stock, false, false, out float normal, out _, out var bloom);
                FxGrowerMotion.SampleCirculation(CultureKind.Bloom, sequence, stock, true, false, out float low, out _, out var sparse);
                FxGrowerMotion.SampleCirculation(CultureKind.Flow, sequence, stock, false, false, out _, out _, out var flow);
                Assert.That(low, Is.GreaterThan(normal)); Assert.That(sparse.Color, Is.EqualTo(bloom.Color));
                Assert.That(bloom.Color.r, Is.GreaterThan(bloom.Color.b)); Assert.That(flow.Color.b, Is.GreaterThan(flow.Color.r));
                Assert.That(bloom.End, Is.Not.EqualTo(flow.End));
            }
        }
        [Test] public void ReducedRemovesTravelExpansionAndTimeMotionButKeepsStateFeedback()
        {
            var stock = FxGrowthMotion.ReadGrower(3, 6, 1, 2);
            foreach (CultureKind culture in new[] { CultureKind.Bloom, CultureKind.Flow }) {
                FxGrowerMotion.SampleCirculation(culture, 7, stock, false, true, out _, out _, out var request);
                Assert.That(request.Motion, Is.EqualTo(FxParticleMotion.Drift));
                Assert.That(request.End + request.Bend + request.Velocity, Is.EqualTo(Vector3.zero));
                Assert.That(request.Turns + request.Radius + request.Spin, Is.Zero); Assert.That(request.Size, Is.EqualTo(request.EndSize));
                FxGrowerMotion.SampleAccent(culture, 7, stock, true, true, out _, out var accent);
                Assert.That(accent.End + accent.Bend + accent.Velocity, Is.EqualTo(Vector3.zero)); Assert.That(accent.Size, Is.EqualTo(accent.EndSize));
            }
            float idle = FxGrowerMotion.Needle(stock, FxGrowerInteraction.Idle, .2f, 0, true);
            Assert.That(FxGrowerMotion.Needle(stock, FxGrowerInteraction.Idle, .1f, 99, true), Is.EqualTo(idle));
            float blocked = FxGrowerMotion.Needle(stock, FxGrowerInteraction.ReservoirFull, .2f, 0, true);
            Assert.That(blocked, Is.Not.EqualTo(idle));
            Assert.That(FxGrowerMotion.Needle(stock, FxGrowerInteraction.ReservoirFull, 0, 99, true), Is.EqualTo(blocked));
        }
        [Test] public void MechanismIsBoundedAndInvalidClocksCannotGenerateUnboundedMotion()
        {
            foreach (int units in new[] { 0, 3, 6 }) foreach (FxGrowerInteraction interaction in Enum.GetValues(typeof(FxGrowerInteraction)))
            foreach (float time in new[] { 0f, 123f, float.NaN, float.PositiveInfinity })
            foreach (float response in new[] { 0f, .1f, -1f, float.NaN, float.PositiveInfinity }) {
                float angle = FxGrowerMotion.Needle(FxGrowthMotion.ReadGrower(units, 6, 1, 2), interaction, response, time, false);
                Assert.That(float.IsNaN(angle) || float.IsInfinity(angle), Is.False); Assert.That(Mathf.Abs(angle), Is.LessThan(45));
            }
        }
        [TestCase(CultureKind.Bloom)] [TestCase(CultureKind.Flow)]
        public void RealPoolMeshesRemainInSourceLocalVesselForAllQuarterTurnsAndCancelCleanly(CultureKind culture)
        {
            var parent = new GameObject("Grower ownership contract"); var source = new GameObject("Moved grower"); source.transform.SetParent(parent.transform);
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            var cues = new FxCueRuntime(); var pool = new FxEffectPool(parent.transform, mesh);
            try {
                for (int quarter = 0; quarter < 4; quarter++) {
                    source.transform.SetPositionAndRotation(new Vector3(4, 0, 7), Quaternion.Euler(0, quarter * 90, 0));
                    var token = cues.Register("grower", quarter.ToString()); var stock = FxGrowthMotion.ReadGrower(2, 6, 1, 2);
                    FxGrowerMotion.SampleCirculation(culture, 8, stock, false, false, out _, out var origin, out var request);
                    Assert.That(pool.EmitParticle(cues, token, source.transform, origin, request, false), Is.True);
                    var visual = pool.ParticleVisualForValidation(token, 0); var vertices = visual.GetComponent<MeshFilter>().sharedMesh.vertices;
                    for (int step = 0; step < 24; step++) {
                        pool.Tick(cues, request.Lifetime / 25);
                        Assert.That(pool.TryGetParticleForValidation(token, 0, out var start, out _, out _, out _, out _), Is.True);
                        Assert.That(Vector3.Distance(source.transform.InverseTransformPoint(start), origin), Is.LessThan(.0001f));
                        foreach (var vertex in vertices) {
                            Vector3 point = source.transform.InverseTransformPoint(visual.TransformPoint(vertex));
                            Assert.That(new Vector2(point.x, point.z).magnitude, Is.LessThan(.46f));
                            Assert.That(point.y, Is.InRange(.93f, .93f + .88f * Mathf.Lerp(.55f, 1, stock.Fill)));
                        }
                    }
                    cues.Unregister(token); pool.Tick(cues, .01f);
                    Assert.That(pool.ParticleCountForValidation(token), Is.Zero);
                    Assert.That(pool.EmitParticle(cues, token, source.transform, origin, request, false), Is.False, "Retired source token cannot emit into a reused slot");
                }
                Assert.That(pool.OwnedCount, Is.EqualTo(96)); Assert.That(pool.OwnedMeshCount, Is.EqualTo(5)); Assert.That(pool.OwnedMaterialCount, Is.EqualTo(1));
            }
            finally { pool.Dispose(); cues.Dispose(); Object.DestroyImmediate(parent); Object.DestroyImmediate(mesh); }
        }
        [Test] public void WarmedPurePresentationSamplingAllocatesNoManagedBytes()
        {
            var stock = FxGrowthMotion.ReadGrower(3, 6, .7f, 2); float sum = 0;
            for (uint i = 0; i < 50; i++) Sample(i, stock, ref sum);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint i = 0; i < 1000; i++) Sample(i, stock, ref sum);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero); Assert.That(float.IsNaN(sum), Is.False);
        }
        static void Sample(uint sequence, FxGrowerPose stock, ref float sum)
        {
            FxGrowerMotion.SampleCirculation(CultureKind.Flow, sequence, stock, false, false, out float interval, out var origin, out var request);
            FxGrowerMotion.SampleAccent(CultureKind.Bloom, sequence, stock, true, false, out var accentOrigin, out var accent);
            sum += interval + origin.y + request.Size + accentOrigin.y + accent.Size + FxGrowerMotion.Needle(stock, FxGrowerInteraction.Collecting, .2f, sequence, false);
        }
    }
}
