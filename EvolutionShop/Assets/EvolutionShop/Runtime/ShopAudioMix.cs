using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopAudio
    {
        AudioSource waterVoice, machineVoice;
        AudioClip waterLoop, machineLoop, doorCue, uiCue;
        string waterId, machineId;
        Vector3 waterPosition, machinePosition, mixListener;
        float waterBase, machineBase, waterTarget, machineTarget, mixDuck = 1, mixAmbience, mixDoor, mixDoorZ;
        bool mixSuspended = true, machineWorking;
        double nextDoorCue;
        int doorPlaybackCount, uiPlaybackCount;
        float quietCueGain;
        public uint ServiceRequestSequence { get; private set; }
        public FxAudioMixSnapshot MixSnapshot => new FxAudioMixSnapshot(waterVoice ? waterVoice.volume : 0,
            waterTarget, machineVoice ? machineVoice.volume : 0, machineTarget, mixDuck, mixAmbience, mixDoor,
            waterVoice && waterVoice.isPlaying, machineVoice && machineVoice.isPlaying, machineWorking, mixSuspended,
            mixListener.z > mixDoorZ, waterId, machineId, waterPosition, machinePosition, mixListener,
            ActiveVoiceCount, doorPlaybackCount, uiPlaybackCount, ServiceRequestSequence);

        void BuildMixAudio()
        {
            waterVoice = CreateVoice(); machineVoice = CreateVoice();
            waterVoice.loop = machineVoice.loop = true;
            waterVoice.priority = machineVoice.priority = 192;
            waterVoice.volume = machineVoice.volume = 0;
            waterLoop = MixClip("original aquarium water circulation / four second loop", FxAudioMix.MakeLoopSamples(false));
            machineLoop = MixClip("original gentle lab pump / four second loop", FxAudioMix.MakeLoopSamples(true));
            doorCue = MixClip("original quiet sliding motor", FxAudioMix.MakeQuietCueSamples(true));
            uiCue = MixClip("original soft navigation tick", FxAudioMix.MakeQuietCueSamples(false));
        }
        static AudioClip MixClip(string label, float[] data)
        {
            var clip = AudioClip.Create(label, data.Length, 1, FxAudioMix.SampleRate, false);
            clip.SetData(data, 0); return clip;
        }
        internal void SetMixState(Vector3 listener, float doorZ, float doorFraction, float ambience,
            string aquariumId, Vector3 aquariumPosition, string workingId, Vector3 workingPosition, bool suspended)
        {
            mixListener = listener; mixDoorZ = doorZ; mixDoor = Mathf.Clamp01(doorFraction);
            mixAmbience = HumorGain(ambience); machineWorking = workingId != null;
            waterId = aquariumId; machineId = workingId; waterPosition = aquariumPosition; machinePosition = workingPosition;
            mixSuspended = suspended || !isActiveAndEnabled;
            if (mixSuspended) { StopMixVoices(); return; }
            waterBase = aquariumId == null ? 0 : .22f * mixAmbience * FxAudioMix.Attenuation(listener, aquariumPosition, doorZ, doorFraction);
            machineBase = workingId == null ? 0 : .26f * mixAmbience * FxAudioMix.Attenuation(listener, workingPosition, doorZ, doorFraction);
            waterTarget = waterBase * mixDuck; machineTarget = machineBase * mixDuck;
            // A user's zero volume is immediate; it does not wait for an artistic fade.
            if (mixAmbience == 0) StopMixVoices();
        }
        void TickMixAudio(float dt)
        {
            if (mixSuspended) return;
            mixDuck = FxAudioMix.StepGain(mixDuck, ServicePriorityActive ? .18f : 1, dt, ServicePriorityActive ? .04f : .45f);
            waterTarget = waterBase * mixDuck; machineTarget = machineBase * mixDuck;
            TickMixVoice(waterVoice, waterLoop, waterTarget, dt);
            TickMixVoice(machineVoice, machineLoop, machineTarget, dt);
        }
        static void TickMixVoice(AudioSource voice, AudioClip clip, float target, float dt)
        {
            if (!voice || !clip) return;
            voice.volume = FxAudioMix.StepGain(voice.volume, target, dt, target < voice.volume ? .15f : .6f);
            if (target > 0 && !voice.isPlaying) { voice.clip = clip; voice.Play(); }
            if (target == 0 && voice.volume <= 0) { voice.Stop(); voice.clip = null; }
        }
        void DuckMixForService()
        {
            // Immediate protected attack, bounded recovery. Never duck the service itself.
            mixDuck = .18f;
            waterTarget = waterBase * mixDuck; machineTarget = machineBase * mixDuck;
            if (waterVoice) waterVoice.volume = Mathf.Min(waterVoice.volume, waterTarget);
            if (machineVoice) machineVoice.volume = Mathf.Min(machineVoice.volume, machineTarget);
        }
        void StopMixVoices()
        {
            waterBase = machineBase = waterTarget = machineTarget = 0;
            if (waterVoice) { waterVoice.Stop(); waterVoice.clip = null; waterVoice.volume = 0; }
            if (machineVoice) { machineVoice.Stop(); machineVoice.clip = null; machineVoice.volume = 0; }
        }
        public void StopMixAudio()
        {
            mixSuspended = true; machineWorking = false; waterId = machineId = null;
            StopMixVoices(); mixDuck = 1; nextDoorCue = 0;
            if (labSource && labSource.clip == doorCue) StopLabAudio();
        }
        internal bool DoorMotion(float gain)
        {
            if (Time.realtimeSinceStartupAsDouble < nextDoorCue || !PlayQuietCue(doorCue, gain)) return false;
            nextDoorCue = Time.realtimeSinceStartupAsDouble + .35; doorPlaybackCount++; return true;
        }
        internal bool UiNavigation(uint precedingServiceSequence)
        {
            if (precedingServiceSequence != ServiceRequestSequence || !PlayQuietCue(uiCue, .4f)) return false;
            uiPlaybackCount++; return true;
        }
        bool PlayQuietCue(AudioClip clip, float gain)
        {
            if (!isActiveAndEnabled || !labSource || !labSource.isActiveAndEnabled || !clip || ServicePriorityActive || LabPlaying) return false;
            quietCueGain = HumorGain(gain);
            labSource.clip = clip; labSource.volume = quietCueGain; labSource.Play(); return true;
        }
        void DisposeMixAudio()
        {
            StopMixAudio();
            if (waterLoop) Destroy(waterLoop); if (machineLoop) Destroy(machineLoop);
            if (doorCue) Destroy(doorCue); if (uiCue) Destroy(uiCue);
            if (waterVoice) Destroy(waterVoice); if (machineVoice) Destroy(machineVoice);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FxAudioClipInfo[] MixValidationClips()
        {
            var result = new FxAudioClipInfo[25];
            var originals = ValidationClips();
            for (int i = 0; i < originals.Length; i++) result[i] = new FxAudioClipInfo(originals[i], false);
            for (int i = 0; i < labClips.Length; i++) result[15 + i] = new FxAudioClipInfo(labClips[i], false);
            result[21] = new FxAudioClipInfo(waterLoop, true); result[22] = new FxAudioClipInfo(machineLoop, true);
            result[23] = new FxAudioClipInfo(doorCue, false); result[24] = new FxAudioClipInfo(uiCue, false);
            return result;
        }
#endif
    }
}
