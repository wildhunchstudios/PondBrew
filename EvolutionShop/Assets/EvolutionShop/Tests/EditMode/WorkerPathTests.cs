using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class WorkerPathTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EveryStoreAndLabTierRoutesRackToConfiguredDisplaysThroughDoor(int tier)
        {
            var layout = new ShopLayout(); var model = Cash();
            for (int i = 0; i < tier; i++)
            {
                Ok(layout.TryExpand(model, ShopDayPhase.Night));
                Ok(layout.TryExpandLab(model, ShopDayPhase.Night));
            }
            int rackZ = tier == 0 ? 7 : 7 + tier * 8;
            Ok(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.RackDefinitionId, null, 9, rackZ, 0, out string rack));
            if (tier > 0) Ok(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.LargeDisplayId, null, tier == 1 ? -18 : -26, -3, 0, out _));
            Assert.That(layout.TryGetStorageAccessPoint(rack, out float x, out float z), Is.True);
            foreach (string slot in layout.TankIds)
            {
                Assert.That(layout.TryGetBrowsePoint(slot, 0, out float tx, out float tz), Is.True);
                AssertPath(layout, x, z, tx, tz);
                AssertPath(layout, tx, tz, x, z);
            }
        }

        [Test]
        public void DirectWallCrossingIsRejectedButRouteUsesTheDoorAndAvoidsFurniture()
        {
            var layout = new ShopLayout();
            Assert.That(layout.IsWorkerSegmentClear(3, -1, 3, 1.5f), Is.False);
            var path = AssertPath(layout, 3, -1, 3, 1.5f);
            Assert.That(path.Length, Is.GreaterThan(2));
            bool crossedDoor = false;
            for (int i = 1; i < path.Length; i++)
            {
                var a = path[i - 1]; var b = path[i];
                if (a.Z <= .5f && b.Z >= .5f || b.Z <= .5f && a.Z >= .5f)
                {
                    float x = a.X + (b.X - a.X) * (.5f - a.Z) / (b.Z - a.Z);
                    Assert.That(x, Is.InRange(-1.25f, 1.25f)); crossedDoor = true;
                }
            }
            Assert.That(crossedDoor, Is.True);
            Assert.That(layout.IsWorkerSegmentClear(3.7f, -1.2f, 6.4f, -1.2f), Is.False);
            AssertPath(layout, 3.7f, -1.2f, 0, -3.8f);
        }

        [TestCase(float.NaN, 0)] [TestCase(float.PositiveInfinity, 0)]
        [TestCase(float.MaxValue, float.MaxValue)] [TestCase(-100, 0)]
        [TestCase(0, -6)] [TestCase(5.5f, -1.2f)] [TestCase(-4, .5f)]
        public void InvalidOrBlockedEndpointsNeverSnapThroughWalls(float x, float z)
        {
            var layout = new ShopLayout();
            Assert.That(layout.TryGetWorkerPath(x, z, 0, -3.8f, out var path), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(layout.TryGetWorkerPath(0, -3.8f, x, z, out path), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(layout.IsWorkerSegmentClear(x, z, 0, -3.8f), Is.False);
        }

        [Test]
        public void ExactNonLatticeEndpointsAndZeroLengthPathRemainValid()
        {
            var layout = new ShopLayout();
            AssertPath(layout, .137f, -3.871f, 3.05f, -1.95f);
            var path = AssertPath(layout, .137f, -3.871f, .137f, -3.871f);
            Assert.That(path.Length, Is.EqualTo(2));
        }

        [Test]
        public void GeometryChangesInvalidateOldSegmentsAndProduceANewSafeRoute()
        {
            var layout = new ShopLayout(); var model = Cash();
            Assert.That(layout.IsWorkerSegmentClear(-3, -1, -1, -1), Is.True);
            Ok(layout.TryCommit(model, ShopDayPhase.Night, ShopLayout.SmallDisplayId, null, -4, -1, 0, out _));
            Assert.That(layout.IsWorkerSegmentClear(-3, -1, -1, -1), Is.False);
            AssertPath(layout, -3, -1.8f, -.8f, -1.8f);
        }

        [Test]
        public void ExpandedNorthwestVoidIsNeverMistakenForLabFloor()
        {
            var layout = new ShopLayout(); var model = Cash();
            Ok(layout.TryExpand(model, ShopDayPhase.Night)); Ok(layout.TryExpandLab(model, ShopDayPhase.Night));
            Assert.That(layout.TryGetWorkerPath(-9, -2.5f, -9, 2, out _), Is.False);
            Assert.That(layout.IsWorkerSegmentClear(-9, -2.5f, -9, 2), Is.False);
            AssertPath(layout, -9, -2.5f, .25f, 7.5f);
        }

        static WorkerPathPoint[] AssertPath(ShopLayout layout, float x, float z, float tx, float tz)
        {
            Assert.That(layout.TryGetWorkerPath(x, z, tx, tz, out var path), Is.True, $"{x},{z} -> {tx},{tz}");
            Assert.That(path.Length, Is.InRange(2, 6207));
            Assert.That(path[0].X, Is.EqualTo(x)); Assert.That(path[0].Z, Is.EqualTo(z));
            Assert.That(path[path.Length - 1].X, Is.EqualTo(tx)); Assert.That(path[path.Length - 1].Z, Is.EqualTo(tz));
            for (int i = 1; i < path.Length; i++)
                Assert.That(layout.IsWorkerSegmentClear(path[i - 1].X, path[i - 1].Z, path[i].X, path[i].Z), Is.True, $"segment {i}");
            return path;
        }
        static GameModel Cash()
        {
            var config = GameConfig.CreateDefault(); var save = new GameModel(config).CaptureSave(); save.currency = 1000;
            Assert.That(GameModel.TryRestore(config, save, out var model, out _), Is.True); return model;
        }
        static void Ok(LayoutResult result) => Assert.That(result.Success, Is.True, result.Failure.ToString());
    }
}
