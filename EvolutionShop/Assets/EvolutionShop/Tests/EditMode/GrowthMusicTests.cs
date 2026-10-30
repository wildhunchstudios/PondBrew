using System;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-21: the game's music, written and played in code (GR.62): three calm loops, each with full bars, chords the
    /// score knows, notes within an instrument's sensible range, rendered the same every time, seamless (each pass after the
    /// first is the loop: the next pass is the same, sample for sample), never clipping and set to its level.</summary>
    public sealed class GrowthMusicTests
    {
        static MusicTrack[] Tracks => (MusicTrack[])Enum.GetValues(typeof(MusicTrack));

        [Test]
        public void EveryTuneFillsItsBarsAndEveryChordIsKnown()
        {
            foreach (var (track, harmony, tune, chords) in GrowthMusicScore.Lines())
            {
                var notes = GrowthMusicScore.TuneNotes(tune, 4);
                Assert.That(notes.Count, Is.GreaterThan(30), track + ": a tune of its own");
                Assert.That(notes.Max(n => n.beat + n.beats), Is.LessThanOrEqualTo(64), track + ": sixteen bars");
                var names = GrowthMusicScore.Harmony(harmony, 4).ToList();
                Assert.That(names.Sum(c => c.beats), Is.EqualTo(64), track + ": the chords fill sixteen bars");
                Assert.That(names.All(c => chords.Contains(c.name)), Is.True, track + ": every chord has its voicing");
            }
            Assert.Throws<FormatException>(() => GrowthMusicScore.TuneNotes("81:1 79:1 | 81:4", 4), "a bar not filled is refused");
        }

        [Test]
        public void ThreePiecesEachWithItsOwnTempoAndInstruments()
        {
            var night = GrowthMusicScore.Piece(MusicTrack.Night);
            var day = GrowthMusicScore.Piece(MusicTrack.Day);
            var show = GrowthMusicScore.Piece(MusicTrack.Exhibition);
            Assert.That(new[] { night.Bpm, day.Bpm, show.Bpm }, Is.EqualTo(new double[] { 72, 96, 108 }), "calm at night, livelier by day, festive for the exhibition");
            Assert.That(night.Notes.Any(n => n.Voice == MusicVoice.MusicBox) && night.Notes.Any(n => n.Voice == MusicVoice.Drop) && !night.Notes.Any(n => n.Voice == MusicVoice.Kick), Is.True, "the night: a music box and drops of water, no drums");
            Assert.That(day.Notes.Any(n => n.Voice == MusicVoice.Vibraphone) && day.Notes.Any(n => n.Voice == MusicVoice.EPiano) && day.Notes.Any(n => n.Voice == MusicVoice.Brush), Is.True, "the day: vibraphone, electric piano, brushes");
            Assert.That(show.Notes.Any(n => n.Voice == MusicVoice.Glockenspiel) && show.Notes.Any(n => n.Voice == MusicVoice.Snap) && show.Notes.Any(n => n.Voice == MusicVoice.Triangle), Is.True, "the exhibition: glockenspiel, claps, a triangle");
            foreach (var p in new[] { night, day, show })
            {
                Assert.That(p.Notes.All(n => n.Beat >= 0 && n.Beat < p.Beats && n.Beats > 0), Is.True, p.Title + ": every note inside the loop");
                Assert.That(p.Notes.Where(n => n.Voice == MusicVoice.Bass || n.Voice == MusicVoice.SoftBass).All(n => n.Midi >= 33 && n.Midi <= 62), Is.True, p.Title + ": the bass low");
                Assert.That(p.Notes.All(n => n.Velocity > 0 && n.Velocity <= 1 && Math.Abs(n.Pan) <= 1), Is.True, p.Title + ": every note at a sane level and place");
                Assert.That(p.Seconds, Is.InRange(30, 60), p.Title + ": half a minute to a minute before it loops");
            }
        }

        [Test]
        public void EachLoopIsSeamlessTheSameEveryTimeAndNeverClips()
        {
            foreach (var track in Tracks)
            {
                var piece = GrowthMusicScore.Piece(track);
                int frames = piece.LoopFrames * GrowthMusicScore.Channels;
                var three = GrowthMusicScore.RenderPasses(piece, 3);
                float peak = 0, worst = 0;
                for (int i = 0; i < frames; i++)
                {
                    peak = Math.Max(peak, Math.Abs(three[frames + i]));
                    worst = Math.Max(worst, Math.Abs(three[frames + i] - three[2 * frames + i]));
                }
                Assert.That(worst, Is.LessThan(peak * 1e-4f), track + ": the second pass and the third are the same, so the loop runs into itself without a seam");
                var loop = GrowthMusicScore.Render(track);
                Assert.That(loop.Length, Is.EqualTo(frames), track + ": one loop, stereo");
                var (top, rms) = GrowthMusicScore.Level(loop);
                Assert.That(top, Is.LessThanOrEqualTo(GrowthMusicScore.PeakLimit + 1e-6f), track + ": never above the peak limit");
                Assert.That(rms, Is.InRange(GrowthMusicScore.TargetRms * 0.5f, GrowthMusicScore.TargetRms * 1.001f), track + ": at its level");
                Assert.That(loop.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True, track + ": every sample a number");
                Assert.That(GrowthMusicScore.Render(track).SequenceEqual(loop), Is.True, track + ": the same every time");
                // The step across the seam is no larger than the loop's own steps.
                float step = 0; for (int i = 2; i < loop.Length; i += 2) step = Math.Max(step, Math.Abs(loop[i] - loop[i - 2]));
                Assert.That(Math.Abs(loop[0] - loop[loop.Length - 2]), Is.LessThanOrEqualTo(step), track + ": its end runs into its start");
            }
        }

        [Test]
        public void TheLoopIsTheSecondPassOfTwoSoTheEndsTailsRingIntoItsStart()
        {
            var piece = GrowthMusicScore.Piece(MusicTrack.Night);
            int frames = piece.LoopFrames * GrowthMusicScore.Channels;
            var two = GrowthMusicScore.RenderPasses(piece, 2);
            var loop = GrowthMusicScore.Render(MusicTrack.Night);
            int at = 0; for (int i = 1; i < frames; i++) if (Math.Abs(two[frames + i]) > Math.Abs(two[frames + at])) at = i;
            float k = loop[at] / two[frames + at];
            float worst = 0; for (int i = 0; i < frames; i++) worst = Math.Max(worst, Math.Abs(loop[i] - k * two[frames + i]));
            Assert.That(worst, Is.LessThan(1e-4f), "the loop is the second pass (set to its level), not the first, which starts from silence");
        }

        [Test]
        public void SettingTheLevelNeverPassesThePeakLimit()
        {
            var spiky = new float[10000];
            for (int i = 0; i < spiky.Length; i++) spiky[i] = 0.001f * (float)Math.Sin(i * 0.1);
            spiky[5000] = 0.5f;
            GrowthMusicScore.Normalize(spiky);
            var (peak, rms) = GrowthMusicScore.Level(spiky);
            Assert.That(peak, Is.LessThanOrEqualTo(GrowthMusicScore.PeakLimit + 1e-6f), "a quiet loop with one loud moment is limited by its peak");
            Assert.That(rms, Is.LessThan(GrowthMusicScore.TargetRms), "so it stays under its level rather than clipping");
        }

        [Test]
        public void TheLeftAndRightDifferSoTheMusicHasWidth()
        {
            var loop = GrowthMusicScore.Render(MusicTrack.Day);
            double diff = 0, sum = 0;
            for (int i = 0; i < loop.Length; i += 2) { diff += Math.Abs(loop[i] - loop[i + 1]); sum += Math.Abs(loop[i]) + Math.Abs(loop[i + 1]); }
            Assert.That(diff / sum, Is.GreaterThan(0.05), "stereo, not the same on both sides");
        }
    }
}
