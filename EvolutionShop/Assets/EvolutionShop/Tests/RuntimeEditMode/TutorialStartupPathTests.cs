using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    // Guard-only tests; the separate process chain validates actual Awake restoration.
    public sealed class TutorialStartupPathTests
    {
        string directory;
        static readonly MethodInfo Guard = typeof(ShopSession).GetMethod("ValidateTutorialRestartStartup", BindingFlags.Static | BindingFlags.NonPublic);
        bool Validate(bool enabled, string stage, string path) => (bool)Guard.Invoke(null, new object[] { enabled, stage, path });

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "EvolutionShop-TutorialStartup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void Cleanup()
        {
            Assert.That(Path.GetDirectoryName(directory), Is.EqualTo(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
            Assert.That(Path.GetFileName(directory), Does.StartWith("EvolutionShop-TutorialStartup-"));
            Directory.Delete(directory, true);
        }
        [TestCase(false, "orient")]
        [TestCase(true, null)]
        public void BothExplicitDevelopmentFlagsAreRequired(bool validation, string stage)
        {
            Assert.That(Validate(validation, stage, null), Is.False);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        [Test] public void FreshOptInDoesNotWriteOrInventASlot()
        {
            Assert.That(Validate(true, "new", directory), Is.True);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        [Test] public void ContinuationGuardPreservesExactExistingInput()
        {
            string path = Path.Combine(directory, "smoke-save.json");
            File.WriteAllText(path, "guard-only sentinel; content validation belongs to SaveStore");
            byte[] before = File.ReadAllBytes(path);
            Assert.That(Validate(true, "working", directory), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }
        [TestCase("")]
        [TestCase("unknown")]
        public void MalformedStageCannotStart(string stage)
        {
            var error = Assert.Throws<TargetInvocationException>(() => Validate(true, stage, directory));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        [TestCase(null)]
        [TestCase("")]
        [TestCase("relative-evidence")]
        [TestCase(@"C:relative-evidence")]
        [TestCase(@"\relative-evidence")]
        [TestCase(@"C:\")]
        public void ExplicitAbsoluteEvidenceIsRequired(string path)
        {
            var error = Assert.Throws<TargetInvocationException>(() => Validate(true, "new", path));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
        }
        [TestCase(false)]
        [TestCase(true)]
        public void NormalProfileAndItsChildrenAreNeverProbeDestinations(bool child)
        {
            string path = Path.GetDirectoryName(SaveStore.DefaultPath);
            if (child) path = Path.Combine(path, "hypothetical-test-folder");
            var error = Assert.Throws<TargetInvocationException>(() => Validate(true, "new", path));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
        }
        [Test] public void MissingContinuationCannotFallBackToFreshGame()
        {
            var error = Assert.Throws<TargetInvocationException>(() => Validate(true, "working", directory));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        [Test] public void FreshProbeCannotOverwriteAnExistingSlot()
        {
            string path = Path.Combine(directory, "smoke-save.json"); File.WriteAllText(path, "preserve");
            var error = Assert.Throws<TargetInvocationException>(() => Validate(true, "new", directory));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(File.ReadAllText(path), Is.EqualTo("preserve"));
        }
    }
}
