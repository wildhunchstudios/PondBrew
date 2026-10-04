using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopAudio : MonoBehaviour
    {
        AudioSource source, comedySource;
        AudioClip collect, confirm, reject, discovery, slap;
        readonly AudioClip[] comedyClips = new AudioClip[10];
        double servicePriorityUntil;
        /// <summary>Accepted playback requests, including gain zero; not proof of audible output.</summary>
        public int SlapPlaybackCount { get; private set; }
        public float LastSlapGain { get; private set; }
        /// <summary>Accepted comedy cues, including gain zero; not a listening measurement.</summary>
        public int ComedyPlaybackCount { get; private set; }
        public float LastComedyGain { get; private set; }
        public bool ServicePlaying => source && source.isPlaying;
        public bool ComedyPlaying => comedySource && comedySource.isPlaying;
        public int ActiveVoiceCount => (ServicePlaying ? 1 : 0) + (ComedyPlaying ? 1 : 0);
        public bool ServicePriorityActive => Time.realtimeSinceStartupAsDouble < servicePriorityUntil;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Validation reads the exact generated clips; no duplicated synthesis or playback.
        internal AudioClip[] ValidationClips()
        {
            var clips = new AudioClip[15];
            clips[0] = collect; clips[1] = confirm; clips[2] = reject; clips[3] = discovery; clips[4] = slap;
            comedyClips.CopyTo(clips, 5);
            return clips;
        }
#endif
        void Awake()
        {
            source = CreateVoice();
            comedySource = CreateVoice();
            collect = Tone("siphon droplet", 640, 0.12f, 180);
            confirm = Tone("glass chime", 850, 0.2f, 320);
            reject = Tone("soft blocked", 170, 0.14f, -30);
            discovery = Tone("new life", 520, 0.7f, 600);
            slap = MakeSlap();
            for (int kind = 1; kind <= 5; kind++)
                for (int variation = 0; variation < 2; variation++)
                    comedyClips[(kind - 1) * 2 + variation] = MakeComedy((ComedyKind)kind, variation);
        }
        AudioSource CreateVoice()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            voice.loop = false;
            return voice;
        }
        AudioClip Tone(string label, float frequency, float seconds, float sweep)
        {
            const int rate = 22050;
            float[] samples = new float[(int)(seconds * rate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Sin(Mathf.PI * t / seconds) * Mathf.Exp(-t * 4);
                samples[i] = Mathf.Sin(2 * Mathf.PI * (frequency * t + sweep * t * t)) * envelope * 0.19f;
            }
            var clip = AudioClip.Create(label, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        public void Collect() => PlayService(collect, .25f);
        public void Feedback(bool success) => PlayService(success ? confirm : reject, .25f);
        public void Discovery() => PlayService(discovery, .8f);
        void PlayService(AudioClip clip, float protection)
        {
            if (!isActiveAndEnabled || !source || !source.isActiveAndEnabled || !clip) return;
            StopComedy();
            // Replace a service cue instead of stacking unbounded one-shots. Protect a preceding
            // discovery's full window even if another valid service request arrives during it.
            double until = Time.realtimeSinceStartupAsDouble + protection;
            if (until > servicePriorityUntil) servicePriorityUntil = until;
            source.Stop();
            source.clip = clip;
            source.volume = 1f;
            source.Play();
        }
        public void Slap(float humorVolume = 1f)
        {
            if (!isActiveAndEnabled || !comedySource || !comedySource.isActiveAndEnabled || !slap) return;
            float gain = HumorGain(humorVolume);
            LastSlapGain = gain;
            SlapPlaybackCount++;
            // The legacy counter measures accepted slap requests, including muted/priority-suppressed
            // requests. Service feedback remains audible even when an intentional slap happens.
            TryComedy(slap, gain, true);
        }
        public bool PlayComedy(ComedyKind kind, int variation, float humorVolume)
        {
            if ((int)kind < 0 || (int)kind > 5) return false;
            AudioClip clip = kind == ComedyKind.Slap ? slap : comedyClips[((int)kind - 1) * 2 + (variation & 1)];
            float gain = HumorGain(humorVolume);
            if (!TryComedy(clip, gain, kind == ComedyKind.Slap)) return false;
            if (kind == ComedyKind.Slap) { LastSlapGain = gain; SlapPlaybackCount++; }
            return true;
        }
        bool TryComedy(AudioClip clip, float gain, bool mayReplace)
        {
            if (!isActiveAndEnabled || !comedySource || !comedySource.isActiveAndEnabled || !clip ||
                ServicePriorityActive || (ComedyPlaying && !mayReplace)) return false;
            StopComedy();
            comedySource.clip = clip;
            comedySource.volume = gain;
            LastComedyGain = gain;
            ComedyPlaybackCount++;
            // AudioListener.volume remains the sole master control. No setting is written here.
            comedySource.Play();
            return true;
        }
        static float HumorGain(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        public void StopComedy()
        {
            if (!comedySource) return;
            comedySource.Stop();
            comedySource.clip = null;
        }
        static AudioClip MakeSlap()
        {
            const int rate = 22050;
            const float seconds = .28f;
            var samples = new float[(int)(seconds * rate)];
            uint noiseState = 0x51A9B31Du;
            float filteredNoise = 0f, phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                float noise = (noiseState >> 8) / 8388607.5f - 1f;
                filteredNoise = Mathf.Lerp(filteredNoise, noise, .35f);
                float pap = filteredNoise * (1f - Mathf.Exp(-t * 550f)) * Mathf.Exp(-t * 75f) * .20f;
                float boingTime = Mathf.Max(0f, t - .018f);
                float boingProgress = Mathf.Clamp01(boingTime / (seconds - .018f));
                float frequency = Mathf.Lerp(310f, 90f, boingProgress) + Mathf.Sin(boingTime * 70f) * 22f;
                phase += 2f * Mathf.PI * frequency / rate;
                float envelope = Mathf.Sin(boingProgress * Mathf.PI) * Mathf.Exp(-boingTime * 7f);
                float boing = (Mathf.Sin(phase) + .22f * Mathf.Sin(phase * 2f)) * envelope * .18f;
                samples[i] = pap + boing;
            }
            var clip = AudioClip.Create("original soft pap and rubber boing", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        void OnDisable()
        {
            StopComedy();
            if (source) { source.Stop(); source.clip = null; }
            servicePriorityUntil = 0;
        }
        void OnDestroy()
        {
            if (collect) Destroy(collect);
            if (confirm) Destroy(confirm);
            if (reject) Destroy(reject);
            if (discovery) Destroy(discovery);
            if (slap) Destroy(slap);
            foreach (var clip in comedyClips) if (clip) Destroy(clip);
        }
    }
}
