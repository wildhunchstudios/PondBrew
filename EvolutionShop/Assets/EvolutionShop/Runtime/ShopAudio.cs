using UnityEngine;

namespace EvolutionShop
{
    public sealed class ShopAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip collect, confirm, reject, discovery;
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            collect = Tone("siphon droplet", 640, 0.12f, 180);
            confirm = Tone("glass chime", 850, 0.2f, 320);
            reject = Tone("soft blocked", 170, 0.14f, -30);
            discovery = Tone("new life", 520, 0.7f, 600);
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
        void OnDestroy()
        {
            if (collect) Destroy(collect);
            if (confirm) Destroy(confirm);
            if (reject) Destroy(reject);
            if (discovery) Destroy(discovery);
        }
    }
}
