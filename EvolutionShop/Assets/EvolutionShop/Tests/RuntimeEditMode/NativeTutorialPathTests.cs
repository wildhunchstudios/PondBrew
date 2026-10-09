using System;
using System.IO;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    public sealed class NativeTutorialPathTests
    {
        string directory;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "EvolutionShop-NativeQa-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void Cleanup()
        {
            Assert.That(Path.GetDirectoryName(directory), Is.EqualTo(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
            Assert.That(Path.GetFileName(directory), Does.StartWith("EvolutionShop-NativeQa-"));
            Directory.Delete(directory, true);
        }
        [Test] public void OrdinaryLaunchDoesNotOptInOrWrite()
        {
            Assert.That(NativeTutorialProbe.ResolveSavePath(null, false), Is.Null);
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        [Test] public void CannotCombineWithSmokeOverrides()
        { Assert.Throws<ArgumentException>(() => NativeTutorialProbe.ResolveSavePath(directory, true)); }
        [TestCase("")][TestCase("relative")][TestCase(@"C:relative")][TestCase(@"\relative")][TestCase(@"C:\")]
        public void RejectAmbiguousOrRootPaths(string path)
        { Assert.Throws<ArgumentException>(() => NativeTutorialProbe.ResolveSavePath(path, false)); }
        [TestCase(false)][TestCase(true)] public void RejectNormalProfileAndChildren(bool child)
        {
            string path = Path.GetDirectoryName(SaveStore.DefaultPath);
            if (child) path = Path.Combine(path, "native-qa");
            Assert.Throws<ArgumentException>(() => NativeTutorialProbe.ResolveSavePath(path, false));
        }
        [Test] public void RequiresExistingFolderAndCorrectMarker()
        {
            Assert.Throws<ArgumentException>(() => NativeTutorialProbe.ResolveSavePath(Path.Combine(directory, "absent"), false));
            Assert.Throws<ArgumentException>(() => NativeTutorialProbe.ResolveSavePath(directory, false));
            File.WriteAllText(Path.Combine(directory, "native-qa-profile.txt"), "incorrect");
            Assert.Throws<ArgumentException>(() => NativeTutorialProbe.ResolveSavePath(directory, false));
        }
        [TestCase(false)][TestCase(true)] public void ResolvesFreshAndExistingSlotsWithoutWrites(bool existing)
        {
            File.WriteAllText(Path.Combine(directory, "native-qa-profile.txt"), NativeTutorialProbe.Marker);
            string path = Path.Combine(directory, "native-save.json");
            if (existing) File.WriteAllText(path, "unchanged sentinel");
            Assert.That(NativeTutorialProbe.ResolveSavePath(directory, false), Is.EqualTo(path));
            if (existing) Assert.That(File.ReadAllText(path), Is.EqualTo("unchanged sentinel"));
            else Assert.That(File.Exists(path), Is.False);
        }
    }
}
