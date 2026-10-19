using UnityEngine;

namespace EvolutionShop
{
    public readonly struct FxAudioClipInfo
    {
        public readonly AudioClip Clip;
        public readonly bool Loop;
        public FxAudioClipInfo(AudioClip clip, bool loop) { Clip = clip; Loop = loop; }
    }

    public readonly struct FxAudioMixSnapshot
    {
        public readonly float WaterGain, WaterTarget, MachineGain, MachineTarget, DuckGain, AmbienceVolume, DoorFraction;
        public readonly bool WaterPlaying, MachinePlaying, MachineWorking, Suspended, PlayerInLab;
        public readonly string WaterSourceId, MachineSourceId;
        public readonly Vector3 WaterSourcePosition, MachineSourcePosition, ListenerPosition;
        public readonly int VoiceCapacity, ActiveVoices, FrameCount, DoorPlaybackCount, UiPlaybackCount;
        public readonly float Realtime;
        public readonly uint ServiceRequestSequence;
        public FxAudioMixSnapshot(float water, float waterTarget, float machine, float machineTarget, float duck,
            float ambience, float door, bool waterPlaying, bool machinePlaying, bool working, bool suspended,
            bool inLab, string waterId, string machineId, Vector3 waterPosition, Vector3 machinePosition,
            Vector3 listener, int active, int doorCount, int uiCount, uint serviceSequence)
        {
            WaterGain = water; WaterTarget = waterTarget; MachineGain = machine; MachineTarget = machineTarget;
            DuckGain = duck; AmbienceVolume = ambience; DoorFraction = door; WaterPlaying = waterPlaying;
            MachinePlaying = machinePlaying; MachineWorking = working; Suspended = suspended; PlayerInLab = inLab;
            WaterSourceId = waterId; MachineSourceId = machineId; WaterSourcePosition = waterPosition;
            MachineSourcePosition = machinePosition; ListenerPosition = listener; VoiceCapacity = FxAudioMix.VoiceCapacity;
            ActiveVoices = active; DoorPlaybackCount = doorCount; UiPlaybackCount = uiCount;
            ServiceRequestSequence = serviceSequence; FrameCount = Time.frameCount; Realtime = Time.realtimeSinceStartup;
        }
    }

    public static class FxAudioMix
    {
        public const int VoiceCapacity = 5;
        public const float DefaultAmbienceVolume = .5f, LoopSeconds = 4f;
        public const int SampleRate = 22050;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        public static float Attenuation(Vector3 listener, Vector3 source, float doorZ, float doorFraction)
        {
            if (!Finite(listener) || !Finite(source) || !Finite(doorZ) || !Finite(doorFraction)) return 0;
            float distance = Vector3.Distance(listener, source);
            if (!Finite(distance)) return 0;
            float gain = 1f / (1f + Mathf.Max(0, distance - 1) * .28f);
            // The door motor belongs to the threshold itself, audible from either side.
            bool opposite = Mathf.Abs(source.z - doorZ) > .1f && (listener.z > doorZ) != (source.z > doorZ);
            return gain * (opposite ? Mathf.Lerp(.06f, .45f, Mathf.Clamp01(doorFraction)) : 1);
        }
        public static float StepGain(float current, float target, float seconds, float fadeSeconds)
        {
            current = Finite(current) ? Mathf.Clamp01(current) : 0;
            target = Finite(target) ? Mathf.Clamp01(target) : 0;
            if (!Finite(seconds) || seconds <= 0) return current;
            if (!Finite(fadeSeconds) || fadeSeconds <= 0) return target;
            return Mathf.MoveTowards(current, target, Mathf.Min(seconds, .5f) / fadeSeconds);
        }
        public static float[] MakeLoopSamples(bool machinery)
        {
            var data = new float[(int)(SampleRate * LoopSeconds)];
            // Integer cycles in the four-second period: amplitude and carrier derivatives wrap.
            // No independent noise seed or envelope restart can introduce a seam click.
            for (int i = 0; i < data.Length; i++)
            {
                double phase = 2 * System.Math.PI * i / data.Length;
                if (machinery)
                    data[i] = (float)((System.Math.Sin(phase * 288) * .042 + System.Math.Sin(phase * 576) * .012) *
                        (.7 + .3 * System.Math.Cos(phase * 3)) + System.Math.Sin(phase * 1536) * .009 *
                        (.5 + .5 * System.Math.Sin(phase * 5)));
                else
                    data[i] = (float)(System.Math.Sin(phase * 960 + .8 * System.Math.Sin(phase * 7)) * .027 *
                        System.Math.Pow(.5 + .5 * System.Math.Cos(phase * 3), 4) +
                        System.Math.Sin(phase * 1240 + .6 * System.Math.Sin(phase * 5)) * .022 *
                        System.Math.Pow(.5 + .5 * System.Math.Sin(phase * 2), 4));
            }
            return data;
        }
        public static float[] MakeQuietCueSamples(bool door)
        {
            float seconds = door ? .28f : .07f;
            var data = new float[Mathf.RoundToInt(seconds * SampleRate)];
            double sum = 0, weights = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate, p = i / (float)(data.Length - 1);
                float envelope = Mathf.Sin(Mathf.PI * p); envelope *= envelope;
                data[i] = envelope * (door ?
                    (.055f * Mathf.Sin(2 * Mathf.PI * (130 * t + 70 * t * t)) + .018f * Mathf.Sin(2 * Mathf.PI * 390 * t)) :
                    .075f * Mathf.Sin(2 * Mathf.PI * (720 * t - 900 * t * t)));
                sum += data[i]; weights += envelope;
            }
            float bias = (float)(sum / weights);
            for (int i = 0; i < data.Length; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / (data.Length - 1));
                data[i] -= bias * envelope * envelope;
            }
            data[0] = data[data.Length - 1] = 0;
            return data;
        }
    }
}
