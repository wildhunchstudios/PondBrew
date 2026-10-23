using System.Linq;
using System.Xml.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-01: the pacing model and its report hold GR.3's and GR.4's targets for the tuned tables.</summary>
    public sealed class PacingSimulatorTests
    {
        [Test]
        public void TheModelIsDeterministic()
        {
            Assert.That(PacingSimulator.Run().ToMarkdown(), Is.EqualTo(PacingSimulator.Run().ToMarkdown()));
        }

        [Test]
        public void TheTypicalPlayerMeetsEveryTarget()
        {
            var report = PacingSimulator.Run();
            foreach (var (target, wanted, simulated, met) in report.Checks()) Assert.That(met, Is.True, target + ": wanted " + wanted + ", simulated " + simulated);
            Assert.That(report.DeadEnds, Is.Empty);
        }

        [Test]
        public void ErasHelpersAndTheEndingComeInOrder()
        {
            var r = PacingSimulator.Run();
            for (int e = 1; e < 5; e++) Assert.That(r.EraMinute[e], Is.GreaterThan(r.EraMinute[e - 1]), "era " + (e + 1));
            Assert.That(r.HelperMinute["pip"], Is.LessThanOrEqualTo(r.HelperMinute["nori"]));
            Assert.That(r.HelperMinute["nori"], Is.LessThanOrEqualTo(r.HelperMinute["moss"]));
            Assert.That(r.HelperMinute["moss"], Is.GreaterThanOrEqualTo(r.PoweredMinute), "Moss needs a powered generator");
            Assert.That(r.LineMinute, Is.LessThanOrEqualTo(r.EraMinute[4]), "era 5 needs a full day on the line");
            Assert.That(r.ExhibitionMinute, Is.GreaterThanOrEqualTo(r.EraMinute[4]));
            Assert.That(r.SpeciesAtExhibition, Is.GreaterThanOrEqualTo(GrowthContent.ExhibitionSpecies));
            Assert.That(r.CrownsAtExhibition, Is.GreaterThanOrEqualTo(GrowthContent.ExhibitionCrowns));
            Assert.That(r.CollectAllMinute, Is.GreaterThan(r.ExhibitionMinute));
            // GR-11: the main ending, prepared the night after the exhibition opens and held the next day.
            Assert.That(r.EndingMinute, Is.GreaterThan(r.ExhibitionMinute).And.LessThan(r.ExhibitionMinute + 20));
            Assert.That(r.EntriesAtEnd, Is.EqualTo(GrowthContent.JournalEntries));
            // Every species is found, each family no earlier than its era.
            Assert.That(r.SpeciesMinute.Keys, Is.EquivalentTo(GrowthContent.Species.Select(s => s.Id)));
            foreach (var s in GrowthContent.Species)
            {
                int era = GrowthContent.Family(s.Family).Era;
                if (era > 1) Assert.That(r.SpeciesMinute[s.Id], Is.GreaterThanOrEqualTo(r.EraMinute[era - 1]), s.Id);
            }
        }

        [Test]
        public void CarefulAndQuickPlayersStillFinishWithinTheRunLimit()
        {
            foreach (var (name, _, policy) in GrowthReport.Players)
            {
                var r = PacingSimulator.Run(policy);
                Assert.That(r.ExhibitionMinute, Is.InRange(180, 260), name + " exhibition");
                Assert.That(r.EndingMinute, Is.InRange(190, 280), name + " the Grand Exhibition held");
                Assert.That(r.CollectAllMinute, Is.InRange(420, 780), name + " collect-all");
                Assert.That(r.LargestStall, Is.LessThanOrEqualTo(15), name + " stall");
                Assert.That(r.DeadEnds, Is.Empty, name);
            }
        }

        [Test]
        public void TheReviewDocumentsCoverEverySpeciesAndTheCurveIsWellFormed()
        {
            var typical = PacingSimulator.Run();
            string roster = GrowthReport.Roster(typical);
            foreach (var s in GrowthContent.Species) Assert.That(roster, Does.Contain("| " + s.Name + " |"), s.Id);
            string pacing = GrowthReport.Pacing(GrowthReport.Players.Select(p => (p.name, p.description, PacingSimulator.Run(p.policy))).ToList());
            foreach (var (target, _, _, _) in typical.Checks()) Assert.That(pacing, Does.Contain(target));
            var svg = XDocument.Parse(GrowthReport.CurveSvg(typical));
            Assert.That(svg.Root.Name.LocalName, Is.EqualTo("svg"));
            Assert.That(svg.Descendants().Count(e => e.Name.LocalName == "path"), Is.EqualTo(2), "species and stars");
        }
    }
}
