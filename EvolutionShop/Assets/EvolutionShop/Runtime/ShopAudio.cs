using UnityEngine;

namespace EvolutionShop
{
    public sealed class ShopAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip collect, confirm, reject, discovery, slap;
        /// <summary>Accepted playback requests, including gain zero; not proof of audible output.</summary>
        public int SlapPlaybackCount { get; private set; }
        public float LastSlapGain { get; private set; }
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            collect = Tone("siphon droplet", 640, 0.12f, 180);
            confirm = Tone("glass chime", 850, 0.2f, 320);
            reject = Tone("soft blocked", 170, 0.14f, -30);
            discovery = Tone("new life", 520, 0.7f, 600);
            slap = MakeSlap();
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
        public void Collect() => source.PlayOneShot(collect);
        public void Feedback(bool success) => source.PlayOneShot(success ? confirm : reject);
        public void Discovery() => source.PlayOneShot(discovery);
        public void Slap(float humorVolume = 1f)
        {
            if (!isActiveAndEnabled || !source || !source.isActiveAndEnabled || !slap) return;
            float gain = float.IsNaN(humorVolume) || float.IsInfinity(humorVolume) ? 0f : Mathf.Clamp01(humorVolume);
            LastSlapGain = gain;
            SlapPlaybackCount++;
            // The existing AudioListener volume remains the master control.
            source.PlayOneShot(slap, gain);
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
        void OnDestroy()
        {
            if (collect) Destroy(collect);
            if (confirm) Destroy(confirm);
            if (reject) Destroy(reject);
            if (discovery) Destroy(discovery);
            if (slap) Destroy(slap);
        }
    }
}
