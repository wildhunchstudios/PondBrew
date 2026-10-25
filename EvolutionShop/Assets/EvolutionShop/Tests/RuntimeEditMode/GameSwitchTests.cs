using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-14: which game a command line starts (GR.43): the new game unless the classic game is asked for, by
    /// -evolutionClassic or by one of the classic game's own tools.</summary>
    public sealed class GameSwitchTests
    {
        static bool Classic(params string[] args) => GrowthGreybox.ClassicAsked(args);

        [Test]
        public void NoFlagsStartTheNewGame()
        {
            Assert.That(Classic("EvolutionShop.exe"), Is.False);
            Assert.That(Classic("EvolutionShop.exe", "-screen-width", "1280", "-logFile", "x.log"), Is.False, "Unity's own flags");
        }

        [Test]
        public void TheClassicSwitchStartsTheClassicGame()
        {
            Assert.That(Classic("EvolutionShop.exe", "-evolutionClassic"), Is.True);
            Assert.That(Classic("EvolutionShop.exe", "-evolutionGrowth", "-evolutionClassic"), Is.True, "the classic switch wins");
        }

        [Test]
        public void TheClassicGamesToolsStartTheClassicGame()
        {
            Assert.That(Classic("x", "-evolutionSmoke"), Is.True, "the classic default route");
            Assert.That(Classic("x", "-evolutionSmoke", "-evolutionCycleSmoke", "-evolutionEvidence", "dir"), Is.True);
            Assert.That(Classic("x", "-evolutionSmoke", "-evolutionToolkitHudSmoke"), Is.True);
            Assert.That(Classic("x", "-evolutionNativeQa", "probe"), Is.True, "the tutorial's native probe");
            Assert.That(Classic("x", "-evolutionStaffReload"), Is.True, "a classic route's reload");
            Assert.That(Classic("x", "-evolutionPrototypeSmoke"), Is.True, "AD-E's prototype");
        }

        [Test]
        public void TheNewGamesFlagsAndTheSharedOnesStartTheNewGame()
        {
            Assert.That(Classic("x", "-evolutionGrowth"), Is.False);
            Assert.That(Classic("x", "-evolutionGrowth", "-evolutionGrowthStart", "era3"), Is.False);
            Assert.That(Classic("x", "-evolutionSmoke", "-evolutionGrowthSmoke", "-evolutionEvidence", "dir"), Is.False);
            Assert.That(Classic("x", "-evolutionSmoke", "-evolutionGrowthGuideSmoke", "-evolutionIsolateSyntheticInput", "-evolutionFxReviewEncoder", "ffmpeg.exe"), Is.False);
            Assert.That(Classic("x", "-evolutionBuildRevision", "gr14-1"), Is.False);
        }
    }
}
