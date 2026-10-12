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
            float[] durations = { .13f, .23f, .32f, .95f, .65f, .12f };
            for (int i = 0; i < labClips.Length; i++)
            {
                var data = i == 3 || i == 4 ? MakeHatchSamples(i == 3, durations[i]) : MakeLabSamples(i, durations[i]);
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
        internal static float[] MakeHatchSamples(bool first, float seconds)
        {
            const int rate = 22050;
            var data = new float[(int)(rate*seconds)];
            float release = FxHatchChoreography.Release(first), phase = 0;
            double sum = 0, weights = 0;
            var envelope = new float[data.Length];
            for (int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate, age=t-release;
                float gather = t < release ? Mathf.Pow(Mathf.Sin(Mathf.PI*t/release),2)*.045f*Mathf.Sin(2*Mathf.PI*(170*t+400*t*t)) : 0;
                float pop = age>=0 ? Mathf.Sin(2*Mathf.PI*(610*age-380*age*age))*Mathf.Exp(-age*22)*Mathf.SmoothStep(0,1,age/.009f)*.18f : 0;
                float chimeAge=Mathf.Max(0,age-.045f), fade=Mathf.Sin(Mathf.PI*t/seconds);
                phase+=2*Mathf.PI*(first?660:550)/rate;
                float chime=age>.045f ? (Mathf.Sin(phase)+.23f*Mathf.Sin(phase*1.5f))*Mathf.Exp(-chimeAge*(first?5:8))*Mathf.SmoothStep(0,1,chimeAge/.035f)*.08f : 0;
                envelope[i]=fade*fade;
                data[i]=(gather+pop+chime)*envelope[i];sum+=data[i];weights+=envelope[i];
            }
            float bias=(float)(sum/weights),peak=0;
            for(int i=0;i<data.Length;i++){data[i]-=bias*envelope[i];peak=Mathf.Max(peak,Mathf.Abs(data[i]));}
            for(int i=0;i<data.Length;i++)data[i]*=.20f/Mathf.Max(.001f,peak);
            data[0]=data[data.Length-1]=0;return data;
        }
        public bool HatchAudioActive => source && (source.clip == labClips[3] || source.clip == labClips[4]) && source.isPlaying;
        public void StopHatchAudio()
        {
            if (!source || (source.clip != labClips[3] && source.clip != labClips[4])) return;
            source.Stop();source.clip=null;servicePriorityUntil=0;
        }
        public void StopDoseAudio()
        {
            if (!source || source.clip != labClips[1]) return;
            source.Stop(); source.clip=null; servicePriorityUntil=0;
        }
        public void LabCollect() { PlayService(labClips[0], .25f); LabPlaybackCount++; }
        public void LabFeedback(bool accepted, FxCueId cue)
        {
            if (!accepted) { Feedback(false); return; }
            int index = cue == FxCueId.IncubationStarted ? 2 : 1;
            PlayService(labClips[index], .3f); LabPlaybackCount++;
        }
        public void LabHatch(bool first) { PlayService(labClips[first ? 3 : 4], first ? .95f : .65f); LabPlaybackCount++; }
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
