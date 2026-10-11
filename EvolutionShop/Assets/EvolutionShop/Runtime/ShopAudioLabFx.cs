using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopAudio
    {
        AudioSource labSource;
        readonly AudioClip[] labClips = new AudioClip[6];
        public int LabPlaybackCount { get; private set; }
        public bool LabPlaying => labSource && labSource.isPlaying;
        void BuildLabAudio()
        {
            labSource = CreateVoice();
            string[] names = { "culture plip", "measured pour", "gentle motor start", "Pondlet first bubble hello", "Pondlet wake", "collector valve tick" };
            float[] durations = { .13f, .23f, .32f, .85f, .45f, .12f };
            for (int i = 0; i < labClips.Length; i++)
            {
                var data = MakeLabSamples(i, durations[i]);
                labClips[i] = AudioClip.Create("original local " + names[i], data.Length, 1, 22050, false); labClips[i].SetData(data, 0);
            }
        }
        internal static float[] MakeLabSamples(int kind, float seconds)
        {
            var data = new float[(int)(22050 * seconds)]; var envelope = new float[data.Length];
            double sum = 0, weights = 0; float phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / 22050f, f = t / seconds;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * f), 2) * Mathf.Exp(-f * 2);
                float freq = kind == 3 ? 380 + Mathf.Floor(f * 3) * 110 : kind == 4 ? 440 + Mathf.Floor(f * 2) * 80 : kind == 5 ? 210 : 330 + kind * 80;
                phase += 2 * Mathf.PI * (freq + (kind == 0 ? -1400 : 240) * t) / 22050f;
                data[i] = (Mathf.Sin(phase) + .18f * Mathf.Sin(phase * 2)) * env; envelope[i] = env; sum += data[i]; weights += env;
            }
            float bias = (float)(sum / weights), peak = 0;
            for (int i = 0; i < data.Length; i++) { data[i] -= bias * envelope[i]; peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
            float gain = .20f / Mathf.Max(peak, .001f);
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            data[0] = data[data.Length - 1] = 0; return data;
        }
        public void LabCollect() { PlayService(labClips[0], .25f); LabPlaybackCount++; }
        public void LabFeedback(bool accepted, FxCueId cue)
        {
            if (!accepted) { Feedback(false); return; }
            int index = cue == FxCueId.IncubationStarted ? 2 : 1;
            PlayService(labClips[index], .3f); LabPlaybackCount++;
        }
        public void LabHatch(bool first) { PlayService(labClips[first ? 3 : 4], first ? .9f : .5f); LabPlaybackCount++; }
        public bool LabModule()
        {
            if (!isActiveAndEnabled || !labSource || !labSource.isActiveAndEnabled || !labClips[5] || ServicePriorityActive) return false;
            labSource.Stop(); labSource.clip = labClips[5]; labSource.volume = .30f; labSource.Play(); LabPlaybackCount++; return true;
        }
        void Update()
        {
            if (labSource) labSource.volume = Mathf.MoveTowards(labSource.volume, ServicePriorityActive ? .03f : .30f,
                Time.unscaledDeltaTime * (.27f / (ServicePriorityActive ? .03f : .20f)));
        }
        public void StopLabAudio() { if (labSource) { labSource.Stop(); labSource.clip = null; } }
        void DisposeLabAudio() { StopLabAudio(); foreach (var clip in labClips) if (clip) Destroy(clip); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal AudioClip[] LabValidationClips() => labClips;
#endif
    }
}
