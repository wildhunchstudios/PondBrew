using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D (AD.5) sounds of the diorama moments: the sale bag's tie, the slap's birds and a refusal's puff. They play on the
    /// existing comedy voice (no new voice), only while the look is on, never over a service cue or another comedy sound,
    /// and at the humour volume. The clips are made on first use, so the classic presentation's clips are unchanged.
    /// </summary>
    public sealed partial class ShopAudio
    {
        AudioClip dioramaBagTie, dioramaBirds, dioramaPuff;
        /// <summary>Diorama sounds played (not proof of audible output) and the last one's gain.</summary>
        public int DioramaCuePlayCount { get; private set; }
        public float LastDioramaCueGain { get; private set; }

        public bool DioramaCue(ShopWorld.DioramaSound sound, float humorVolume)
        {
            if (!DioramaLook.Active || !isActiveAndEnabled || !comedySource || !comedySource.isActiveAndEnabled ||
                ServicePriorityActive || ComedyPlaying) return false;
            var clip = sound == ShopWorld.DioramaSound.BagTie ? dioramaBagTie ? dioramaBagTie : dioramaBagTie = MakeBagTie() :
                sound == ShopWorld.DioramaSound.Birds ? dioramaBirds ? dioramaBirds : dioramaBirds = MakeBirds() :
                dioramaPuff ? dioramaPuff : dioramaPuff = MakePuff();
            float gain = HumorGain(humorVolume) * (sound == ShopWorld.DioramaSound.Puff ? .6f : .85f);
            comedySource.clip = clip;
            comedySource.volume = gain;
            comedySource.Play();
            DioramaCuePlayCount++; LastDioramaCueGain = gain;
            return true;
        }

        /// <summary>A fresh copy of a moment's clip (the tests read them).</summary>
        public static AudioClip MakeDioramaClip(ShopWorld.DioramaSound sound) =>
            sound == ShopWorld.DioramaSound.BagTie ? MakeBagTie() : sound == ShopWorld.DioramaSound.Birds ? MakeBirds() : MakePuff();

        static AudioClip Synth(string label, float seconds, System.Func<float, float> sample)
        {
            const int rate = 22050;
            var samples = new float[(int)(seconds * rate)];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(sample((float)i / rate), -1, 1);
            var clip = AudioClip.Create(label, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Two rubbery upward squeaks, as the bag's neck twists and ties.</summary>
        static AudioClip MakeBagTie() => Synth("diorama bag tie", .2f, t =>
        {
            float local = t < .09f ? t : t - .1f, length = .085f;
            if (local < 0 || local > length) return 0;
            float f0 = t < .09f ? 820 : 1060;
            float phase = 2 * Mathf.PI * (f0 * local + 2600 * local * local) + Mathf.Sin(local * 2 * Mathf.PI * 34) * 1.4f;
            float envelope = Mathf.Sin(Mathf.PI * local / length);
            return (Mathf.Sin(phase) * .7f + Mathf.Sin(phase * 2) * .2f) * envelope * .2f;
        });

        /// <summary>Three bright chirps, the cartoon birds' tweet.</summary>
        static AudioClip MakeBirds() => Synth("diorama slap birds", .4f, t =>
        {
            int chirp = Mathf.FloorToInt(t / .12f);
            float local = t - chirp * .12f, length = .07f;
            if (chirp > 2 || local > length) return 0;
            float f0 = chirp == 1 ? 3300 : 2900;
            float phase = 2 * Mathf.PI * (f0 * local - 9000 * local * local);
            float envelope = Mathf.Sin(Mathf.PI * local / length);
            return Mathf.Sin(phase) * envelope * envelope * .13f;
        });

        /// <summary>A soft breathy puff: low-passed noise, quick in, slow out.</summary>
        static AudioClip MakePuff()
        {
            uint state = 0x2F6B19A3u; float filtered = 0;
            return Synth("diorama grey puff", .22f, t =>
            {
                state = unchecked(state * 1664525u + 1013904223u);
                float noise = (state >> 8) / 8388607.5f - 1f;
                filtered += (noise - filtered) * .08f;
                float envelope = Mathf.Min(1, t / .02f) * Mathf.Exp(-t * 14);
                return filtered * envelope * .9f;
            });
        }
    }
}
