using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-16 wave 5: the new game's sounds. Each is made in the game (original, nothing recorded), never clips, a one-shot
    /// starts and ends at silence, a loop is four seconds and joins its end to its start; the two settings scale them full, half
    /// and off and are kept with the save (an older save, without them, is full); and the moments are noticed once each: an
    /// offspring's pop, a school's swish, nothing for a shop just loaded or a tank seen for the first time.</summary>
    public sealed class GrowthAudioTests
    {
        static float Peak(float[] d) => d.Max(Mathf.Abs);
        static float Step(float[] d) { float m = 0; for (int i = 1; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i] - d[i - 1])); return m; }

        [Test] public void EachSoundIsMadeHereAndNeverClips()
        {
            foreach (var (name, d) in new[] { ("hum", GrowthAudio.MakeHum()), ("motor", GrowthAudio.MakeMotor()), ("click", GrowthAudio.MakeClick()),
                                             ("stroke", GrowthAudio.MakeStroke()), ("pop", GrowthAudio.MakePop()), ("swish", GrowthAudio.MakeSwish()),
                                             ("brew", GrowthAudio.MakeBrew()), ("draw", GrowthAudio.MakeDraw()), ("ready", GrowthAudio.MakeReady()), ("bell", GrowthAudio.MakeBell()) })
            {
                Assert.That(d.Length, Is.GreaterThan(GrowthAudio.SampleRate / 50), name + " has length");
                Assert.That(d.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True, name + " is finite");
                Assert.That(Peak(d), Is.InRange(0.05f, 0.6f), name + " is heard and never clips");
            }
        }

        [Test] public void AOneShotStartsAndEndsAtSilence()
        {
            foreach (var (name, d) in new[] { ("click", GrowthAudio.MakeClick()), ("stroke", GrowthAudio.MakeStroke()), ("pop", GrowthAudio.MakePop()), ("swish", GrowthAudio.MakeSwish()), ("ready", GrowthAudio.MakeReady()), ("bell", GrowthAudio.MakeBell()) })
            {
                Assert.That(Mathf.Abs(d[0]), Is.LessThan(1e-3f), name + " starts at silence");
                Assert.That(Mathf.Abs(d[d.Length - 1]), Is.LessThan(1e-3f), name + " ends at silence");
            }
            Assert.That(GrowthAudio.MakeClick().Length, Is.LessThan(GrowthAudio.ClickEvery * GrowthAudio.SampleRate), "a click is over before the next");
            Assert.That(GrowthAudio.MakeStroke().Length, Is.LessThan(GrowthAudio.StrokeEvery * GrowthAudio.SampleRate), "a stroke is over before the next");
        }

        [Test] public void ALoopJoinsItsEndToItsStart()
        {
            foreach (var (name, d) in new[] { ("hum", GrowthAudio.MakeHum()), ("motor", GrowthAudio.MakeMotor()) })
            {
                Assert.That(d.Length, Is.EqualTo((int)(GrowthAudio.SampleRate * GrowthAudio.LoopSeconds)), name + " is four seconds");
                // Across the join the wave moves no more than it does between any two samples inside it: no click.
                Assert.That(Mathf.Abs(d[0] - d[d.Length - 1]), Is.LessThanOrEqualTo(Step(d) + 1e-4f), name + " loops seamlessly");
            }
        }

        [Test] public void TheSettingsScaleFullHalfOffAndCycle()
        {
            Assert.That(GrowthAudio.Gain(GrowthAudio.Level.Full), Is.EqualTo(1f));
            Assert.That(GrowthAudio.Gain(GrowthAudio.Level.Half), Is.EqualTo(0.5f));
            Assert.That(GrowthAudio.Gain(GrowthAudio.Level.Off), Is.EqualTo(0f));
            var l = GrowthAudio.Level.Full;
            l = GrowthAudio.Next(l); Assert.That(l, Is.EqualTo(GrowthAudio.Level.Half));
            l = GrowthAudio.Next(l); Assert.That(l, Is.EqualTo(GrowthAudio.Level.Off));
            l = GrowthAudio.Next(l); Assert.That(l, Is.EqualTo(GrowthAudio.Level.Full), "the chip cycles back to full");
        }

        [Test] public void TheSettingsAreKeptWithTheSaveAndAnOlderSaveIsFull()
        {
            var save = new GrowthShop().Snapshot();
            save.soundLevel = 2; save.humLevel = 1;
            var back = JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(save));
            Assert.That(back.soundLevel == 2 && back.humLevel == 1, Is.True, "kept with the slot");
            var json = JsonUtility.ToJson(new GrowthShop().Snapshot());
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"(soundLevel|humLevel)\":\\s*\\d+,?", "");
            var older = JsonUtility.FromJson<GrowthSave>(json);
            Assert.That(older.soundLevel == 0 && older.humLevel == 0, Is.True, "a save from before wave 5 has neither: full");
            Assert.That(GrowthShop.Restore(older, out string problem), Is.Not.Null, "and it still loads (" + problem + ")");
        }

        [Test] public void ASchoolFormingIsNoticedOnce()
        {
            var shop = new GrowthShop();
            var tank = shop.Containers[0];
            var moments = new GrowthMoments();
            Assert.That(moments.Notice(shop).schools, Is.Empty, "a shop seen for the first time: nothing to mark");
            shop.DevelopmentStock(tank.Id, "pondlet"); shop.DevelopmentStock(tank.Id, "pondlet");
            Assert.That(moments.Notice(shop).schools, Is.Empty, "two of a kind are not a school");
            Assert.That(shop.DevelopmentStock(tank.Id, "pondlet").Success, Is.True);
            Assert.That(moments.Notice(shop).schools, Is.EqualTo(new[] { tank.Id }), "the third makes a school: a swish");
            Assert.That(moments.Notice(shop).schools, Is.Empty, "once");
            tank.Occupants.RemoveAt(0);
            Assert.That(moments.Notice(shop).schools, Is.Empty, "a school broken up is quiet");
            shop.DevelopmentStock(tank.Id, "pondlet");
            Assert.That(moments.Notice(shop).schools, Is.EqualTo(new[] { tank.Id }), "formed again: another swish");
            var loaded = new GrowthShop();
            for (int i = 0; i < 3; i++) loaded.DevelopmentStock(loaded.Containers[0].Id, "pondlet");
            Assert.That(moments.Notice(loaded).schools, Is.Empty, "a shop loaded with its schools: nothing to mark");
        }

        [Test] public void AnOffspringArrivingIsNoticedOnce()
        {
            var shop = new GrowthShop();
            var moments = new GrowthMoments();
            Assert.That(moments.Notice(shop).pops, Is.EqualTo(0));
            shop.Pairs[0].Offspring = new GrowthSpecimen { Id = 901, Species = "pondlet" };
            Assert.That(moments.Notice(shop).pops, Is.EqualTo(1), "an offspring arrives: a pop");
            Assert.That(moments.Notice(shop).pops, Is.EqualTo(0), "once");
            shop.Pairs[0].Offspring = null;
            Assert.That(moments.Notice(shop).pops, Is.EqualTo(0), "taken out: quiet");
            shop.Pairs[0].Offspring = new GrowthSpecimen { Id = 902, Species = "pondlet" };
            Assert.That(moments.Notice(shop).pops, Is.EqualTo(1), "the next one: another pop");
            var loaded = new GrowthShop();
            loaded.Pairs[0].Offspring = new GrowthSpecimen { Id = 903, Species = "pondlet" };
            Assert.That(moments.Notice(loaded).pops, Is.EqualTo(0), "a shop loaded with an offspring waiting: nothing to mark");
        }
    }
}
