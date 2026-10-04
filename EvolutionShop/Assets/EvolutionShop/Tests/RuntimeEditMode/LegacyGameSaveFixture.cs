using System;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    // Historical file-test builders must write the actual two-culture game1/2/3 shape.
    // This helper is test-only; it refuses to erase newly unlocked gameplay state.
    internal static class LegacyGameSaveFixture
    {
        public static SaveData AsVersion(SaveData game, int version)
        {
            Assert.That(version, Is.InRange(1, 3));
            Assert.That(game.sparkResearchUnlocked, Is.False);
            Assert.That(game.availableSpark, Is.Zero);
            Assert.That(game.incubator.stagedSpark, Is.Zero);
            Assert.That(game.mistResearchUnlocked, Is.False);
            Assert.That(game.availableMist, Is.Zero);
            Assert.That(game.incubator.stagedMist, Is.Zero);
            Assert.That(game.discoveries, Does.Not.Contain("lanternfin"));
            Assert.That(game.discoveries, Does.Not.Contain("driftwing"));
            game.version = version;
            game.growers = Array.FindAll(game.growers, grower => grower == null || grower.cultureId != "spark" && grower.cultureId != "mist");
            return game;
        }
    }
}
