using System;
using EvolutionShop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvolutionShop.Tests
{
    public sealed class ProjectSetupSafetyTests
    {
        Scene scene;


        [SetUp]
        public void CreateTemporaryScene()
        {
            // Preview scenes coexist with the Test Runner's unsaved untitled scene.
            scene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void CloseOnlyTemporaryScene()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);

        }

        GameObject ObjectInTestScene(string name)
        {
            var value = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }

        ShopSession AddSession()
        {
            return ObjectInTestScene("Shop session").AddComponent<ShopSession>();
        }

        [Test]
        public void ValidationPreservesAuthoredObjectsAndDoesNotSaveScene()
        {
            var session = AddSession();
            var authored = ObjectInTestScene("Authored room fixture");
            authored.transform.position = new Vector3(2, 3, 4);
            int id = authored.GetInstanceID();
            bool wasDirty = scene.isDirty;
            Assert.DoesNotThrow(() => ProjectSetup.ValidatePlayableScene(scene));
            Assert.AreEqual(2, scene.rootCount);
            Assert.AreEqual(id, authored.GetInstanceID());
            Assert.AreEqual(new Vector3(2, 3, 4), authored.transform.position);
            Assert.AreEqual(wasDirty, scene.isDirty);
            Assert.IsTrue(EditorSceneManager.IsPreviewScene(scene));
            Assert.IsTrue(session);
            Assert.IsFalse(ProjectSetup.IsBootstrapOnlyScene(scene));
        }

        [Test]
        public void EmptySceneIsRejectedWithoutCreatingABootstrap()
        {
            Assert.Throws<InvalidOperationException>(() => ProjectSetup.ValidatePlayableScene(scene));
            Assert.AreEqual(0, scene.rootCount);
        }

        [Test]
        public void DuplicateSessionIsRejectedWithoutDeletingEither()
        {
            var first = AddSession();
            var second = AddSession();
            Assert.Throws<InvalidOperationException>(() => ProjectSetup.ValidatePlayableScene(scene));
            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreEqual(2, scene.rootCount);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DisabledOrInactiveSessionIsRejected(bool disableComponent)
        {
            var session = AddSession();
            if (disableComponent) session.enabled = false;
            else session.gameObject.SetActive(false);
            Assert.Throws<InvalidOperationException>(() => ProjectSetup.ValidatePlayableScene(scene));
            Assert.IsFalse(ProjectSetup.IsBootstrapOnlyScene(scene));
        }

        [Test]
        public void PristineGeneratedBootstrapCanBeIdentified()
        {
            AddSession();
            Assert.DoesNotThrow(() => ProjectSetup.ValidatePlayableScene(scene));
            Assert.IsTrue(ProjectSetup.IsBootstrapOnlyScene(scene));
        }

        [TestCase("child")]
        [TestCase("component")]
        [TestCase("transform")]
        public void BatchRegenerationGuardRejectsAuthoredBootstrapChanges(string change)
        {
            var session = AddSession();
            if (change == "child") ObjectInTestScene("Authored child").transform.SetParent(session.transform);
            if (change == "component") session.gameObject.AddComponent<BoxCollider>();
            if (change == "transform") session.transform.position = Vector3.right;
            Assert.IsFalse(ProjectSetup.IsBootstrapOnlyScene(scene));
            Assert.DoesNotThrow(() => ProjectSetup.ValidatePlayableScene(scene));
        }
    }
}
