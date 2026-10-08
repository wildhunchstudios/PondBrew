using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>Isolated live transforms and colliders; no full room build, gameplay input or timing claim.</summary>
    public sealed class TutorialGuidanceTests
    {
        GameObject owner, scene;
        ShopSession session;
        ShopWorld world;
        ShopPlayer player;
        Camera camera;
        List<Station> stations;
        Station bloom, flow, incubator, tankA, tankB, counter;
        CursorLockMode previousLock;
        bool previousVisible;

        [SetUp]
        public void Setup()
        {
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            owner = new GameObject("Inactive tutorial guidance authority"); owner.SetActive(false);
            scene = new GameObject("Isolated tutorial guidance geometry");
            session = owner.AddComponent<ShopSession>();
            Set("Config", GameConfig.CreateDefault()); Set("Model", new GameModel(session.Config));
            Set("Cycle", new ShopDayCycle()); Set("Reports", new ShopTradeLedger());
            Set("Reputation", new ShopReputation()); Set("Layout", new ShopLayout());
            Set("Staff", new ShopStaff()); Set("Stocking", new ShopStocking()); Set("Laboratory", new ShopLaboratory());
            world = Child("World").AddComponent<ShopWorld>(); Set("World", world);
            stations = (List<Station>)typeof(ShopWorld).GetField("stations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
            player = Child("Player").AddComponent<ShopPlayer>(); player.enabled = false;
            camera = Child("Camera").AddComponent<Camera>(); camera.enabled = false; camera.aspect = 16f / 9f;
            player.Initialize(camera); Set("Player", player);
            bloom = StationFixture(StationKind.Bloom, new Vector3(-4, 0, 4), ShopLayout.BloomFacilityId);
            flow = StationFixture(StationKind.Flow, new Vector3(-1, 0, 4), ShopLayout.FlowFacilityId);
            incubator = StationFixture(StationKind.Incubator, new Vector3(2, 0, 4), ShopLayout.IncubatorFacilityId);
            tankA = StationFixture(StationKind.TankA, new Vector3(-4, 0, -3), tank: "tank-a");
            tankB = StationFixture(StationKind.TankB, new Vector3(-1, 0, -3), tank: "tank-b");
            counter = StationFixture(StationKind.Counter, new Vector3(4, 0, -3));
            var door = Child("Door").AddComponent<ShopRoomDoor>(); door.transform.position = new Vector3(0, 0, .5f);
            typeof(ShopWorld).GetProperty("LabDoor").SetValue(world, door);
            session.Tutorial.Start(); session.Tutorial.ObserveOrientation(true, true, true);
            player.Teleport(new Vector3(0, 0, 2), Quaternion.identity);
        }

        [TearDown]
        public void Cleanup()
        {
            if (owner) Object.DestroyImmediate(owner);
            if (scene) Object.DestroyImmediate(scene);
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        [TestCase(CultureKind.Bloom, StationKind.Flow)]
        [TestCase(CultureKind.Flow, StationKind.Bloom)]
        public void CollectionAcceptsEitherOrderAndTargetsOnlyTheMissingCulture(CultureKind first, StationKind next)
        {
            Collect(first);
            Assert.That(Target().Station.Kind, Is.EqualTo(next));
            Collect(first == CultureKind.Flow ? CultureKind.Bloom : CultureKind.Flow);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Prepare));
            Assert.That(Target().Station, Is.SameAs(incubator));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullBufferDirectsToIncubatorAndExplainsARealSlotRelease(bool staged)
        {
            for (int i = 0; i < session.Model.Capacity; i++) Collect(CultureKind.Bloom);
            if (staged) for (int i = 0; i < session.Model.Capacity; i++) session.Model.Dose(CultureKind.Bloom);
            Assert.That(Target().Station, Is.SameAs(incubator));
            Set("Panel", ShopPanel.Incubator);
            Assert.That(session.TutorialLocalCallout, Does.Contain("Release").And.Contain("Bloom").And.Contain("Flow"));
            if (staged)
            {
                Assert.That(session.TutorialLocalCallout, Does.Contain("Returning"));
                session.Model.ReturnDose(CultureKind.Bloom);
                Assert.That(session.Model.ReservedCultureCount, Is.EqualTo(session.Model.Capacity));
            }
            session.Model.ReleaseCulture(CultureKind.Bloom);
            if (staged) session.Model.ClearDoses();
            Set("Panel", ShopPanel.None); Assert.That(Target().Station, Is.SameAs(flow));
        }

        [Test]
        public void WrongMixtureOffersConcreteReturnAndValidAlternativeIsAccepted()
        {
            Collect(CultureKind.Bloom); Collect(CultureKind.Bloom); Collect(CultureKind.Bloom); Collect(CultureKind.Flow);
            session.Model.Dose(CultureKind.Bloom); session.Model.Dose(CultureKind.Bloom); session.Model.Dose(CultureKind.Bloom);
            Set("Panel", ShopPanel.Incubator);
            Assert.That(session.TutorialLocalCallout, Does.Contain("Bloom").And.Contain("2"));
            session.Model.ReturnDose(CultureKind.Bloom); session.Model.Dose(CultureKind.Flow);
            Assert.That(session.Tutorial.ObservePrepared(session.Model, "bubblebelly", TutorialActionOrigin.Player), Is.True);
            Assert.That(session.TutorialLocalCallout, Does.Contain("Bubblebelly").And.Contain("Begin"));
            Assert.That(session.TutorialLocalCallout, Does.Not.Contain("repair"));
            Set("Panel", ShopPanel.None); Assert.That(Target().Station, Is.SameAs(incubator));
        }

        [Test]
        public void TargetFollowsLiveFacilityMoveWithoutChangingProgress()
        {
            var initial = Target(); string before = State();
            var translation = new Vector3(.5f, 0, 1);
            bloom.transform.position += translation;
            var moved = Target();
            Assert.That(moved.Root, Is.SameAs(initial.Root));
            Assert.That(Vector3.Distance(moved.Position, initial.Position + translation), Is.LessThan(.001f));
            Assert.That(State(), Is.EqualTo(before));
        }

        [TestCase("inactive")]
        [TestCase("disabled")]
        [TestCase("destroyed")]
        public void MissingLiveGrowerNeverLeavesAStaleMarker(string removal)
        {
            Assert.That(Target().Station, Is.SameAs(bloom));
            if (removal == "inactive") bloom.gameObject.SetActive(false);
            else if (removal == "disabled") bloom.enabled = false;
            else Object.DestroyImmediate(bloom.gameObject);
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.TutorialGuidanceRecovery, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void RemovedOrStoredDisplayIsNotSelectedWhenItsModelSlotIsAbsent()
        {
            Produce(true, false);
            // Deliberately retained visual object for a slot removed by storing furniture.
            var stale = StationFixture(StationKind.Display, new Vector3(1, 0, -3), tank: "stored-item-slot-1");
            tankA.gameObject.SetActive(false); tankB.gameObject.SetActive(false);
            player.Teleport(new Vector3(0, 0, -1), Quaternion.identity);
            if (session.TryGetTutorialGuidance(out var result)) Assert.That(result.Station, Is.Not.SameAs(stale));
            Assert.That(session.TutorialGuidanceRecovery, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void PricingFindsExactTrackedSpecimenAmongOtherStock()
        {
            Produce(true, false); var unrelated = session.Model.TransferToTank("tank-a"); Assert.That(unrelated.Success, Is.True);
            // Existing stock is a labelled fixture. New tutorial authority starts after it exists.
            Set("Tutorial", new ShopTutorial()); session.Tutorial.Start(); session.Tutorial.ObserveOrientation(true, true, true);
            Produce(true, true, "tank-b"); player.Teleport(new Vector3(0, 0, -1), Quaternion.identity);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.PriceAndOpen));
            Assert.That(session.Tutorial.FirstSpecimenId, Is.EqualTo(session.Model.GetTank("tank-b").Value.Id));
            Assert.That(Target().Station, Is.SameAs(tankB));
            session.Tutorial.ObservePriceViewed(); Assert.That(Target().Station, Is.SameAs(counter));
        }

        [Test]
        public void CrossRoomRouteUsesDoorBeforeStationInBothDirections()
        {
            player.Teleport(new Vector3(0, 0, -2), Quaternion.identity);
            Assert.That(Target().IsDoor, Is.True);
            player.Teleport(new Vector3(0, 0, 2), Quaternion.identity);
            Assert.That(Target().Station, Is.SameAs(bloom));
            Produce(true, false);
            Assert.That(Target().IsDoor, Is.True);
            player.Teleport(new Vector3(0, 0, -2), Quaternion.identity);
            Assert.That(Target().Station.Kind, Is.EqualTo(StationKind.TankA).Or.EqualTo(StationKind.TankB));
        }

        [Test]
        public void ProjectionRejectsOpaqueObstructionAndFacingAway()
        {
            var target = Target(); camera.transform.LookAt(target.Position); Physics.SyncTransforms();
            Assert.That(session.TryProjectTutorialGuidance(out _, out var projected), Is.True);
            Assert.That(projected.x, Is.InRange(0f, 1280f)); Assert.That(projected.y, Is.InRange(0f, 720f));
            var wall = Child("Opaque collider"); wall.transform.position = Vector3.Lerp(camera.transform.position, target.Position, .5f);
            wall.AddComponent<BoxCollider>().size = Vector3.one;
            Physics.SyncTransforms(); Assert.That(session.TryProjectTutorialGuidance(out _, out _), Is.False);
            Object.DestroyImmediate(wall); Physics.SyncTransforms();
            camera.transform.rotation = Quaternion.LookRotation(camera.transform.position - target.Position);
            Assert.That(session.TryProjectTutorialGuidance(out _, out _), Is.False);
        }

        [TestCase(ShopPanel.Pause)]
        [TestCase(ShopPanel.TutorialHelp)]
        [TestCase(ShopPanel.Journal)]
        [TestCase(ShopPanel.Incubator)]
        public void NonGameplayPanelsHideWorldMarker(ShopPanel panel)
        {
            Set("Panel", panel); Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.TryProjectTutorialGuidance(out _, out _), Is.False);
        }

        [Test]
        public void SkipAndIndependentPracticeHideGuidance()
        {
            Produce(true, true); session.Cycle.OpenShop();
            var sold = session.Model.SellFromTank("tank-a");
            session.Tutorial.ObserveSale(session.Model, sold, TutorialActionOrigin.Player, 1);
            Assert.That(session.ActiveTutorialLesson, Is.EqualTo(TutorialLesson.Practice));
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Set("Panel", ShopPanel.Incubator); Assert.That(session.TutorialLocalCallout, Is.Null.Or.Empty);
            Set("Panel", ShopPanel.None); session.Tutorial.Skip();
            Assert.That(session.TryGetTutorialGuidance(out _), Is.False);
            Assert.That(session.ShowTutorialTarget(), Is.False);
        }

        [Test]
        public void RenderQueriesAreReadOnlyAndShowMeOnlyExitsActiveHelp()
        {
            string before = State();
            for (int i = 0; i < 20; i++)
            {
                session.TryGetTutorialGuidance(out _); session.TryProjectTutorialGuidance(out _, out _);
                _ = session.TutorialLocalCallout; _ = session.TutorialGuidanceRecovery;
            }
            Assert.That(State(), Is.EqualTo(before));
            Assert.That(session.ShowTutorialTarget(), Is.False);
            Assert.That(session.OpenTutorialHelp(), Is.True); Assert.That(session.ShowTutorialTarget(), Is.True);
            Assert.That(session.Panel, Is.EqualTo(ShopPanel.None)); Assert.That(State(), Is.EqualTo(before));
        }

        TutorialGuidanceTarget Target()
        {
            Assert.That(session.TryGetTutorialGuidance(out var target), Is.True, session.TutorialGuidanceRecovery);
            return target;
        }
        void Collect(CultureKind kind)
        {
            var result = session.Model.Collect(kind); Assert.That(result.Success, Is.True);
            session.Tutorial.ObserveCollection(session.Model, kind, result, TutorialActionOrigin.Player);
        }
        void Produce(bool ready, bool display, string tank = "tank-a")
        {
            Collect(CultureKind.Bloom); Collect(CultureKind.Flow);
            session.Model.Dose(CultureKind.Bloom); session.Model.Dose(CultureKind.Flow);
            var started = session.Model.BeginIncubation(); Assert.That(started.Success, Is.True);
            session.Tutorial.ObserveStarted(session.Model, started, TutorialActionOrigin.Player);
            if (!ready) return;
            session.Model.Tick(10); session.Tutorial.ObserveReady(session.Model);
            if (!display) return;
            var result = session.Model.TransferToTank(tank); Assert.That(result.Success, Is.True);
            session.Tutorial.ObserveDisplayed(session.Model, result, TutorialActionOrigin.Player);
        }
        string State() => JsonUtility.ToJson(session.Model.CaptureSave()) + JsonUtility.ToJson(session.Tutorial.CaptureSave());
        GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(scene.transform); return go; }
        Station StationFixture(StationKind kind, Vector3 position, string facility = null, string tank = null)
        {
            var station = Child(kind.ToString()).AddComponent<Station>(); station.Kind = kind; station.Label = kind.ToString();
            station.FacilityId = facility; station.TankId = tank; station.transform.position = position; stations.Add(station); return station;
        }
        void Set(string property, object value) => typeof(ShopSession).GetProperty(property, BindingFlags.Instance | BindingFlags.Public).SetValue(session, value);
    }
}
