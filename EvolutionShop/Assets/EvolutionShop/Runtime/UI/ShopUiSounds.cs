using System;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>The UX-08 UI sounds (UX.9). The discovery stamp uses the existing first-hatch sound, so it has none here.</summary>
    public enum UiSound { CardPaper, CardEnamel, Tick, Thunk, KaChing }

    /// <summary>
    /// Pure synthesis of the UI sounds, generated in code like ShopAudio's clips (22050 Hz mono, no recorded audio or
    /// outside assets). Deterministic: the noise is a fixed-seed sequence, so the same sound is the same samples every run.
    /// Every sound fades in and out over a few milliseconds, so none starts or stops with a click of its own.
    /// </summary>
    public static class UiSoundSynth
    {
        public const int Rate = 22050;
        const float Edge = .003f;

        public static float Seconds(UiSound sound)
        {
            switch (sound)
            {
                case UiSound.CardPaper: return .07f;
                case UiSound.CardEnamel: return .11f;
                case UiSound.Tick: return .03f;
                case UiSound.Thunk: return .11f;
                case UiSound.KaChing: return .42f;
                default: return 0;
            }
        }

        public static string Label(UiSound sound)
        {
            switch (sound)
            {
                case UiSound.CardPaper: return "ui paper card";
                case UiSound.CardEnamel: return "ui enamel card";
                case UiSound.Tick: return "ui button tick";
                case UiSound.Thunk: return "ui blocked thunk";
                case UiSound.KaChing: return "ui ka-ching";
                default: return "ui";
            }
        }

        public static float[] Samples(UiSound sound)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(Seconds(sound) * Rate));
            var samples = new float[count];
            uint seed = 0x9E3779B9u ^ (uint)((int)sound * 7919 + 17);
            float low = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float noise = Noise(ref seed);
                float v;
                switch (sound)
                {
                    case UiSound.CardPaper:
                        // A dry paper flick: a soft, slightly muffled noise burst over a low thump.
                        low += (noise - low) * .35f;
                        v = low * Mathf.Exp(-t * 85f) * .55f + Mathf.Sin(2 * Mathf.PI * 180f * t) * Mathf.Exp(-t * 60f) * .14f;
                        break;
                    case UiSound.CardEnamel:
                        // A small enamel tap: two bright partials and a very short click at the start.
                        v = (Mathf.Sin(2 * Mathf.PI * 1250f * t) * .6f + Mathf.Sin(2 * Mathf.PI * 2830f * t) * .4f) * Mathf.Exp(-t * 38f) * .17f +
                            (t < .006f ? noise * .12f : 0f);
                        break;
                    case UiSound.Tick:
                        v = Mathf.Sin(2 * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 220f) * .22f + noise * Mathf.Exp(-t * 400f) * .03f;
                        break;
                    case UiSound.Thunk:
                    {
                        // A dull, falling knock: low sine dropping in pitch, muffled noise on top.
                        low += (noise - low) * .12f;
                        float f = Mathf.Lerp(120f, 80f, t / .11f);
                        v = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 30f) * .24f + low * Mathf.Exp(-t * 80f) * .35f;
                        break;
                    }
                    case UiSound.KaChing:
                    {
                        // "Ka": a short drawer click. "Ching": a bell chord (G6, C7, E7) from 40 ms with a slight shimmer.
                        float ka = t < .03f ? noise * Mathf.Exp(-t * 150f) * .22f : 0f;
                        float c = t - .04f;
                        float ching = 0;
                        if (c > 0)
                        {
                            float shimmer = 1 + .08f * Mathf.Sin(2 * Mathf.PI * 9f * c);
                            ching = (Mathf.Sin(2 * Mathf.PI * 1568f * c) + Mathf.Sin(2 * Mathf.PI * 2093f * c) * .8f + Mathf.Sin(2 * Mathf.PI * 2637f * c) * .6f) *
                                Mathf.Exp(-c * 7f) * Mathf.Clamp01(c / .004f) * shimmer * .085f;
                        }
                        v = ka + ching;
                        break;
                    }
                    default: v = 0; break;
                }
                float fadeIn = Mathf.Clamp01(t / Edge), fadeOut = Mathf.Clamp01((count - 1 - i) / (Edge * Rate));
                samples[i] = v * fadeIn * fadeOut;
            }
            return samples;
        }

        static float Noise(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x7FFFFF - 1f;
        }
    }

    /// <summary>
    /// Plays the UI sounds on one voice of its own, a child object of the UI root, so the shop's five game voices (FX-19)
    /// stay exactly as they were. Master volume applies (AudioListener.volume); pausing doesn't silence it, so the pause
    /// menu's buttons still tick. A sound asked for twice within 35 ms plays once. Counts are requests, not proof of output.
    /// </summary>
    public sealed class ShopUiSounds
    {
        public const string VoiceName = "Evolution Shop UI Voice";
        const float Repeat = .035f;
        readonly AudioClip[] clips = new AudioClip[Enum.GetValues(typeof(UiSound)).Length];
        readonly int[] counts = new int[Enum.GetValues(typeof(UiSound)).Length];
        readonly float[] lastAt = new float[Enum.GetValues(typeof(UiSound)).Length];
        GameObject voiceObject;

        public AudioSource Voice { get; private set; }
        public int Total { get; private set; }
        public int Dropped { get; private set; }
        public UiSound Last { get; private set; }
        public int Count(UiSound sound) => counts[(int)sound];
        public AudioClip Clip(UiSound sound) => clips[(int)sound];

        public static ShopUiSounds Create(Transform parent)
        {
            var sounds = new ShopUiSounds();
            sounds.voiceObject = new GameObject(VoiceName);
            sounds.voiceObject.transform.SetParent(parent, false);
            var voice = sounds.voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0;
            voice.ignoreListenerPause = true;
            sounds.Voice = voice;
            foreach (UiSound sound in Enum.GetValues(typeof(UiSound)))
            {
                var data = UiSoundSynth.Samples(sound);
                var clip = AudioClip.Create(UiSoundSynth.Label(sound), data.Length, 1, UiSoundSynth.Rate, false);
                clip.SetData(data, 0);
                sounds.clips[(int)sound] = clip;
                sounds.lastAt[(int)sound] = float.NegativeInfinity;
            }
            return sounds;
        }

        /// <summary>Plays a UI sound; false when it was dropped as a repeat or the voice is unavailable.</summary>
        public bool Play(UiSound sound)
        {
            int i = (int)sound;
            float now = Time.unscaledTime;
            if (now - lastAt[i] < Repeat) { Dropped++; return false; }
            if (!Voice || !Voice.isActiveAndEnabled || !clips[i]) return false;
            lastAt[i] = now;
            Voice.PlayOneShot(clips[i]);
            counts[i]++; Total++; Last = sound;
            return true;
        }

        public void Destroy()
        {
            foreach (var clip in clips) if (clip) UnityEngine.Object.Destroy(clip);
            if (voiceObject) UnityEngine.Object.Destroy(voiceObject);
            Voice = null;
        }
    }
}
