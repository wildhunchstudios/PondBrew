using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopAudio
    {
        // Original mathematical synthesis, with no recordings, speech, external assets or RNG
        // shared with gameplay. Each short cue has two restrained pitch/timing variants.
        static AudioClip MakeComedy(ComedyKind kind, int variation)
        {
            const int rate = 22050;
            float seconds = kind == ComedyKind.Fart ? .46f : kind == ComedyKind.Complaint ? .65f :
                kind == ComedyKind.Belch ? .53f : kind == ComedyKind.WorkerCelebration ? .4f : .57f;
            seconds += variation * .025f;
            var samples = new float[Mathf.RoundToInt(seconds * rate)];
            var envelopes = new float[samples.Length];
            float phase = 0, accentPhase = 0, noiseFilter = 0;
            uint noiseState = 0x71C8439Bu + (uint)((int)kind * 177 + variation * 23);
            double sum = 0, envelopeSum = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, p = (float)i / (samples.Length - 1);
                float window = Mathf.Sin(Mathf.PI * p);
                window *= window;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                float noise = (noiseState >> 8) / 8388607.5f - 1f;
                noiseFilter = Mathf.Lerp(noiseFilter, noise, .15f);
                float frequency, signal;
                switch (kind)
                {
                    case ComedyKind.Fart:
                        frequency = Mathf.Lerp(128f + variation * 12, 57f, p) + Mathf.Sin(t * 67f) * 15f;
                        phase += 2 * Mathf.PI * frequency / rate;
                        accentPhase += 2 * Mathf.PI * (410f + p * 280f + variation * 27) / rate;
                        signal = (Mathf.Sin(phase) + .22f * Mathf.Sin(phase * 2f)) * .12f + noiseFilter * .035f;
                        signal += Mathf.Sin(accentPhase) * Mathf.SmoothStep(0, 1, (p - .52f) * 4) * .045f;
                        break;
                    case ComedyKind.Complaint:
                        // Three nonsense syllables; no words or real voice identity.
                        float syllable = p * 3f, local = syllable - Mathf.Floor(syllable);
                        frequency = 173f + variation * 17f + Mathf.Floor(syllable) * 28f + Mathf.Sin(local * Mathf.PI) * 48f;
                        phase += 2 * Mathf.PI * frequency / rate;
                        float mouth = Mathf.Sin(local * Mathf.PI); mouth *= mouth;
                        signal = (Mathf.Sin(phase) + .28f * Mathf.Sin(phase * 2f) + .13f * Mathf.Sin(phase * 3f)) * mouth * .12f;
                        break;
                    case ComedyKind.Belch:
                        frequency = 86f + variation * 9f + Mathf.Sin(p * Mathf.PI) * 23f - p * 24f;
                        phase += 2 * Mathf.PI * frequency / rate;
                        signal = (Mathf.Sin(phase) + .3f * Mathf.Sin(phase * 2f)) * (.1f + .025f * Mathf.Sin(t * 81f));
                        signal += noiseFilter * .04f;
                        break;
                    case ComedyKind.WorkerCelebration:
                        // Two rubber-shoe squeaks accompany the worker's tiny happy foot shuffle.
                        float step = p * 2f, stepTime = step - Mathf.Floor(step);
                        frequency = 690f + variation * 45f + Mathf.Sin(stepTime * Mathf.PI) * 570f;
                        phase += 2 * Mathf.PI * frequency / rate;
                        float sole = Mathf.Sin(stepTime * Mathf.PI); sole *= sole;
                        signal = (Mathf.Sin(phase) + .15f * Mathf.Sin(phase * 2f)) * sole * .14f;
                        break;
                    default: // Mimic: three rounded rising chirrups, distinct from the discovery glide.
                        float chirp = p * 3f, chirpTime = chirp - Mathf.Floor(chirp);
                        frequency = 620f + variation * 65f + chirpTime * chirpTime * 810f;
                        phase += 2 * Mathf.PI * frequency / rate;
                        float beak = chirpTime < .8f ? Mathf.Sin(chirpTime / .8f * Mathf.PI) : 0;
                        signal = (Mathf.Sin(phase) + .12f * Mathf.Sin(phase * 2f)) * beak * beak * .15f;
                        break;
                }
                envelopes[i] = window;
                samples[i] = signal * window;
                sum += samples[i]; envelopeSum += window;
            }
            // Remove DC through the existing envelope, preserving exactly silent endpoints.
            // Limit downward only: the authoring ceilings are not loudness-normalization targets.
            float bias = envelopeSum > 0 ? (float)(sum / envelopeSum) : 0;
            float peak = 0; double squares = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] -= bias * envelopes[i];
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                squares += (double)samples[i] * samples[i];
            }
            float rms = Mathf.Sqrt((float)(squares / samples.Length));
            float gain = Mathf.Min(1f, Mathf.Min(.22f / Mathf.Max(peak, .000001f), .105f / Mathf.Max(rms, .000001f)));
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            samples[0] = samples[samples.Length - 1] = 0;
            var clip = AudioClip.Create("original " + ComedyLabel(kind) + " " + (variation + 1), samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        static string ComedyLabel(ComedyKind kind)
        {
            switch (kind)
            {
                case ComedyKind.Fart: return "rubber raspberry squeak";
                case ComedyKind.Complaint: return "mumbling complaint";
                case ComedyKind.Belch: return "bubbly belch";
                case ComedyKind.WorkerCelebration: return "happy shoe squeaks";
                default: return "fish mimic chirrups";
            }
        }
    }
}
