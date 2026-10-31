using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-16 wave 5: the new game's sounds (GR.14): the hand crank's ratchet, the hand pump's strokes, the electric pumps'
    /// whirr and the generator's hum (the ambience), the breeding tank's "pop" when an offspring arrives and a swish when three
    /// of a kind in a tank become a school. Each is made here at start, original (no recorded or bought sound), and plays where
    /// it happens: nearer is louder, out of a room it fades. Two settings scale them (AD.7: comfort settings scale every new
    /// moment): Sounds (the ratchet, the strokes, the pop, the swish) and Machine hum (the generator and the pumps), each 0 to
    /// 100 (GR-23; full, half or off before). Nothing new plays while the game is paused, and the hums fade out until it resumes.
    /// </summary>
    public sealed class GrowthAudio : MonoBehaviour
    {
        public enum Level { Full, Half, Off }
        public static float Gain(Level level) => level == Level.Full ? 1f : level == Level.Half ? 0.5f : 0f;
        public static Level Next(Level level) => level == Level.Full ? Level.Half : level == Level.Half ? Level.Off : Level.Full;
        public static string Name(Level level) => level == Level.Full ? "Full" : level == Level.Half ? "Half" : "Off";

        public const int SampleRate = 22050;
        public const float LoopSeconds = 4f, ClickEvery = 0.12f, StrokeEvery = 0.45f, Fade = 0.4f, NearDistance = 2f, FarDistance = 16f;
        /// <summary>How loud each sound is at full (before the setting and the distance).</summary>
        public const float HumGain = 0.5f, MotorGain = 0.35f, ClickGain = 0.45f, StrokeGain = 0.6f, PopGain = 0.7f, SwishGain = 0.55f, ChimeGain = 0.5f;

        GrowthSession session;
        AudioSource hum, motor, crank, stroke, moment;
        AudioClip humClip, motorClip, clickClip, strokeClip, popClip, swishClip, chimeClip, bellClip;
        float clickClock, strokeClock;
        readonly GrowthMoments moments = new GrowthMoments();

        /// <summary>Validation: the sounds asked for (requests, gain zero included; not a listening measurement) and where.</summary>
        public int Clicks { get; private set; }
        public int Strokes { get; private set; }
        public int Pops { get; private set; }
        public int Swishes { get; private set; }
        /// <summary>GR-20: wishes granted, each a chime by the visitor, and where the last one rang.</summary>
        public int Chimes { get; private set; }
        public Vector3 LastChime { get; private set; }
        public float LastMomentGain { get; private set; }
        public Vector3 LastPop { get; private set; }
        public Vector3 LastSwish { get; private set; }
        public bool HumPlaying => hum != null && hum.isPlaying;
        public float HumVolume => hum != null ? hum.volume : 0;
        public float HumPitch => hum != null ? hum.pitch : 0;
        public Vector3 HumPosition => hum != null ? hum.transform.position : Vector3.zero;
        public bool MotorPlaying => motor != null && motor.isPlaying;
        public float MotorVolume => motor != null ? motor.volume : 0;
        public Vector3 MotorPosition => motor != null ? motor.transform.position : Vector3.zero;
        public IEnumerable<AudioClip> Clips => new[] { humClip, motorClip, clickClip, strokeClip, popClip, swishClip, chimeClip, bellClip };
        public IEnumerable<AudioSource> Voices => new[] { hum, motor, crank, stroke, moment };

        public void Initialize(GrowthSession owner)
        {
            session = owner;
            humClip = Clip("generator hum (four second loop)", MakeHum());
            motorClip = Clip("electric pump whirr (four second loop)", MakeMotor());
            clickClip = Clip("crank ratchet click", MakeClick());
            strokeClip = Clip("hand pump stroke", MakeStroke());
            popClip = Clip("breeding pop", MakePop());
            swishClip = Clip("school swish", MakeSwish());
            chimeClip = Clip("wish chime", MakeChime());
            bellClip = Clip("counter bell", MakeBell());
            hum = Voice("Sound / generator hum", true);
            motor = Voice("Sound / pump motor", true);
            crank = Voice("Sound / crank", false);
            stroke = Voice("Sound / hand pump", false);
            moment = Voice("Sound / moments", false);
        }

        AudioSource Voice(string name, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var v = go.AddComponent<AudioSource>();
            v.playOnAwake = false; v.loop = loop; v.volume = 0;
            v.spatialBlend = 1; v.rolloffMode = AudioRolloffMode.Linear; v.minDistance = NearDistance; v.maxDistance = FarDistance;
            v.dopplerLevel = 0; v.spread = 60; v.priority = loop ? 160 : 128;
            return v;
        }

        static AudioClip Clip(string label, float[] data)
        {
            var clip = AudioClip.Create(label, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------------------------------------------------------------- each frame

        void Update()
        {
            if (session == null || session.Shop == null || session.World == null) return;
            var shop = session.Shop;
            float dt = Time.deltaTime;
            bool paused = session.Paused;
            float sounds = session.SoundVolume, hums = session.HumVolume;

            // The generator's hum, from a turbine (level 2 and up) while it makes power: deeper and fuller as it grows.
            int level = shop.Level("generator");
            bool humming = !paused && hums > 0 && shop.HasGenerator && !shop.HandCrank && shop.SupplyNow > 0;
            if (humming) hum.transform.position = session.World.GeneratorPlace + new Vector3(0, 1.0f, 0);
            hum.pitch = level >= 4 ? 0.8f : level == 3 ? 0.9f : 1f;
            Loop(hum, humClip, humming ? hums * HumGain * (level >= 4 ? 1f : level == 3 ? 0.85f : 0.7f) : 0, dt);

            // The electric pumps' whirr, from the running pump nearest the player.
            Vector3? pump = null; float nearest = float.MaxValue;
            if (!paused && hums > 0 && shop.ElectricPumps)
                foreach (var line in shop.Lines)
                {
                    var use = shop.PowerUse(line.PowerId);
                    if (use == null || use.Got <= 0 || !GrowthWorld.GrowerPositions.ContainsKey(line.Culture)) continue;
                    var at = PumpPlace(line.Culture);
                    float d = (at - Listener).sqrMagnitude;
                    if (d < nearest) { nearest = d; pump = at; }
                }
            if (pump.HasValue) motor.transform.position = pump.Value;
            Loop(motor, motorClip, pump.HasValue ? hums * MotorGain : 0, dt);

            // The hand crank's ratchet and the hand pump's strokes, in time while they are worked.
            if (!paused && session.CrankingNow)
            {
                clickClock -= dt;
                if (clickClock <= 0) { clickClock += ClickEvery; Clicks++; Play(crank, session.World.GeneratorPlace + new Vector3(0, 0.9f, 0), clickClip, sounds * ClickGain); }
            }
            else clickClock = 0;
            if (!paused && session.PumpingNow && session.Focused != null && GrowthWorld.GrowerPositions.ContainsKey(session.Focused.Id))
            {
                strokeClock -= dt;
                if (strokeClock <= 0) { strokeClock += StrokeEvery; Strokes++; Play(stroke, PumpPlace(session.Focused.Id), strokeClip, sounds * StrokeGain); }
            }
            else strokeClock = 0;

            // The moments: an offspring in the breeding tank, a school formed in a tank.
            var (pops, schools) = moments.Notice(shop);
            if (paused) return;
            for (int i = 0; i < pops; i++)
            {
                Pops++; LastPop = GrowthWorld.BreedingPosition + new Vector3(0, 1.0f, 0);
                Play(moment, LastPop, popClip, sounds * PopGain);
            }
            foreach (var id in schools)
            {
                Swishes++;
                LastSwish = session.World.Tanks.TryGetValue(id, out var tank) ? tank.Root.position + new Vector3(0, 1.0f, 0) : Listener;
                Play(moment, LastSwish, swishClip, sounds * SwishGain);
            }
            // GR-31 (F29): the counter's bell as a visitor reaches it, so there is no waiting for a prompt to know.
            if (BellDue) { BellDue = false; Bells++; Play(moment, GrowthWorld.CounterPosition + new Vector3(0, 1.1f, 0), bellClip, sounds * ChimeGain); }
            // GR-20: a wish granted, a chime by the visitor's bubble as it pops into a heart.
            foreach (var id in moments.NoticeWishes(shop))
            {
                Chimes++;
                LastChime = session.World.VisitorViews.TryGetValue(id, out var view) && view != null ? view.transform.position + new Vector3(0, 1.9f, 0) : Listener;
                Play(moment, LastChime, chimeClip, sounds * ChimeGain);
            }
        }

        Vector3 Listener => session.Player != null && session.Player.View != null ? session.Player.View.transform.position : Vector3.zero;

        /// <summary>A grower's pump, on its front (the hand pump and the motor stand there).</summary>
        static Vector3 PumpPlace(string culture) =>
            GrowthWorld.GrowerPositions[culture] + Quaternion.Euler(0, GrowthWorld.GrowerYaw(culture), 0) * new Vector3(0, 0.7f, 0.45f);

        void Play(AudioSource voice, Vector3 at, AudioClip clip, float gain)
        {
            LastMomentGain = gain;
            if (gain <= 0 || !isActiveAndEnabled) return;
            voice.transform.position = at;
            voice.PlayOneShot(clip, gain);
        }

        static void Loop(AudioSource voice, AudioClip clip, float target, float dt)
        {
            voice.volume = Mathf.MoveTowards(voice.volume, target, dt / Fade);
            if (target > 0 && !voice.isPlaying) { voice.clip = clip; voice.Play(); }
            if (target <= 0 && voice.volume <= 0 && voice.isPlaying) voice.Stop();
        }

        void OnDestroy()
        {
            foreach (var c in Clips) if (c) Destroy(c);
        }

        // ---------------------------------------------------------------- the sounds, made here (original; nothing recorded)

        /// <summary>The generator's hum: a low drone (55 Hz and its harmonics) with a slow swell and bubbles rising through
        /// it. Every partial makes whole cycles in the four seconds and every bubble ends inside them, so it loops seamlessly.</summary>
        public static float[] MakeHum()
        {
            var d = new float[(int)(SampleRate * LoopSeconds)];
            for (int i = 0; i < d.Length; i++)
            {
                double p = 2 * System.Math.PI * i / d.Length;
                double swell = 0.75 + 0.25 * System.Math.Cos(p * 3);
                d[i] = (float)((System.Math.Sin(p * 220) * 0.16 + System.Math.Sin(p * 440) * 0.07 + System.Math.Sin(p * 660) * 0.03) * swell);
            }
            var rng = new System.Random(5);
            for (int k = 0; k < 22; k++)
            {
                float start = 0.05f + (float)rng.NextDouble() * 3.8f, f0 = 380 + (float)rng.NextDouble() * 260;
                int a = (int)(start * SampleRate), n = (int)(0.05f * SampleRate);
                for (int j = 0; j < n && a + j < d.Length; j++)
                {
                    float t = j / (float)SampleRate, e = Mathf.Sin(Mathf.PI * j / n);
                    d[a + j] += Mathf.Sin(2 * Mathf.PI * (f0 * t + 5000 * t * t)) * e * e * 0.07f;
                }
            }
            return d;
        }

        /// <summary>The electric pump's whirr: a soft 120 Hz whine with its harmonics and a gentle wobble, whole cycles in four seconds.</summary>
        public static float[] MakeMotor()
        {
            var d = new float[(int)(SampleRate * LoopSeconds)];
            for (int i = 0; i < d.Length; i++)
            {
                double p = 2 * System.Math.PI * i / d.Length;
                double wobble = 0.8 + 0.2 * System.Math.Sin(p * 28);
                d[i] = (float)((System.Math.Sin(p * 480) * 0.08 + System.Math.Sin(p * 960) * 0.05 + System.Math.Sin(p * 1440) * 0.025 +
                                System.Math.Sin(p * 3840) * 0.012) * wobble);
            }
            return d;
        }

        /// <summary>One click of the crank's ratchet: a bright metallic tick and a little thump, over in 45 ms.</summary>
        public static float[] MakeClick()
        {
            var d = new float[(int)(0.045f * SampleRate)];
            uint s = 0x1F2E3D4Cu;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                s = unchecked(s * 1664525u + 1013904223u);
                float noise = (s >> 8) / 8388607.5f - 1f;
                float e = Mathf.Exp(-t * 140);
                d[i] = (Mathf.Sin(2 * Mathf.PI * 3100 * t) * 0.35f + Mathf.Sin(2 * Mathf.PI * 1750 * t) * 0.2f + noise * 0.15f) * e +
                       Mathf.Sin(2 * Mathf.PI * 160 * t) * Mathf.Exp(-t * 90) * 0.2f;
            }
            return Ends(d);
        }

        /// <summary>One stroke of the hand pump: a squelch as the plunger draws, then a glug as the dose goes down the pipe.</summary>
        public static float[] MakeStroke()
        {
            var d = new float[(int)(0.32f * SampleRate)];
            uint s = 0x6C8E9CF5u; float filtered = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                s = unchecked(s * 1664525u + 1013904223u);
                filtered += (((s >> 8) / 8388607.5f - 1f) - filtered) * 0.12f;
                float squelch = filtered * Mathf.Min(1, t / 0.01f) * Mathf.Exp(-t * 28) * 0.5f;
                float g = Mathf.Max(0, t - 0.11f);
                float glug = Mathf.Sin(2 * Mathf.PI * (240 * g - 180 * g * g)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(g / 0.2f)) * 0.3f;
                d[i] = squelch + glug;
            }
            return Ends(d);
        }

        /// <summary>The breeding tank's pop: a cartoon bloop sweeping up, with a soft click at its start.</summary>
        public static float[] MakePop()
        {
            var d = new float[(int)(0.22f * SampleRate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = 2 * Mathf.PI * (300 * t + 3400 * t * t);
                d[i] = Mathf.Sin(f) * Mathf.Min(1, t / 0.004f) * Mathf.Exp(-t * 20) * 0.45f + Mathf.Sin(2 * Mathf.PI * 1800 * t) * Mathf.Exp(-t * 400) * 0.15f;
            }
            return Ends(d);
        }

        /// <summary>The school's swish: breathy noise through a filter sweeping up and down, as a shoal turns together.</summary>
        public static float[] MakeSwish()
        {
            var d = new float[(int)(0.5f * SampleRate)];
            uint s = 0x2B7E1516u; float low = 0, band = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate, p = t / 0.5f;
                s = unchecked(s * 1664525u + 1013904223u);
                float noise = (s >> 8) / 8388607.5f - 1f;
                // A state variable band-pass whose centre sweeps 700 Hz -> 2600 Hz -> 900 Hz.
                float fc = Mathf.Lerp(700, 2600, Mathf.Sin(Mathf.PI * p)) * (p > 0.5f ? 0.85f : 1f);
                float f = 2 * Mathf.Sin(Mathf.PI * fc / SampleRate), q = 0.6f;
                low += f * band; float high = noise - low - q * band; band += f * high;
                d[i] = band * Mathf.Pow(Mathf.Sin(Mathf.PI * p), 1.5f) * 0.32f;
            }
            return Ends(d);
        }

        /// <summary>GR-20's chime for a wish granted: two soft bell notes rising (E6 then A6), each with a quieter octave.</summary>
        /// <summary>GR-31 (F29): rings the counter's bell on the next frame; how many times it has rung (validation).</summary>
        public bool BellDue;
        public int Bells { get; private set; }

        /// <summary>One ding of a small shop bell: over in half a second.</summary>
        public static float[] MakeBell()
        {
            var d = new float[(int)(0.5f * SampleRate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                d[i] = Mathf.Min(1, t / 0.002f) * Mathf.Exp(-t * 9f) * (Mathf.Sin(2 * Mathf.PI * 1975f * t) * 0.3f + Mathf.Sin(2 * Mathf.PI * 3951f * t) * 0.1f);
            }
            return d;
        }

        public static float[] MakeChime()
        {
            var d = new float[(int)(0.7f * SampleRate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SampleRate, v = 0;
                foreach (var (start, f) in new[] { (0f, 1318.5f), (0.12f, 1760f) })
                {
                    float u = t - start;
                    if (u < 0) continue;
                    float env = Mathf.Min(1, u / 0.003f) * Mathf.Exp(-u * 7f);
                    v += env * (Mathf.Sin(2 * Mathf.PI * f * u) * 0.32f + Mathf.Sin(2 * Mathf.PI * 2 * f * u) * 0.08f * Mathf.Exp(-u * 10f));
                }
                d[i] = v;
            }
            return Ends(d);
        }

        /// <summary>Starts and ends a one-shot at silence (a short fade at each end), so it never clicks.</summary>
        static float[] Ends(float[] d)
        {
            int n = Mathf.Min(64, d.Length / 4);
            for (int i = 0; i < n; i++) { float k = i / (float)n; d[i] *= k; d[d.Length - 1 - i] *= k; }
            return d;
        }
    }

    /// <summary>
    /// GR-16 wave 5: the moments the sounds mark, noticed by comparing the shop with how it was: each offspring that arrives
    /// in the breeding tank (a pop) and each school that forms in a tank (three of a kind; a swish). A new shop (a load, a
    /// fixture) is taken as it is, with nothing to mark; so is a tank seen for the first time.
    /// </summary>
    public sealed class GrowthMoments
    {
        GrowthShop seen;
        readonly HashSet<int> offspring = new HashSet<int>();
        readonly Dictionary<string, HashSet<string>> schools = new Dictionary<string, HashSet<string>>();
        GrowthShop seenWishes;
        readonly HashSet<int> granted = new HashSet<int>();

        /// <summary>GR-20: the visitors whose wish a sale granted since the last look (none on a new shop).</summary>
        public List<int> NoticeWishes(GrowthShop shop)
        {
            bool fresh = shop != seenWishes;
            if (fresh) { seenWishes = shop; granted.Clear(); }
            var now = new List<int>();
            foreach (var v in shop.Visitors) if (v.WishGranted && granted.Add(v.Id) && !fresh) now.Add(v.Id);
            return now;
        }

        public static IEnumerable<string> SchoolsIn(GrowthContainer c) => c.Occupants.Select(o => o.Species).Distinct().Where(c.InSchool);

        /// <summary>The offspring that arrived since the last look, and the containers where a school formed.</summary>
        public (int pops, List<string> schools) Notice(GrowthShop shop)
        {
            bool fresh = shop != seen;
            if (fresh) { seen = shop; offspring.Clear(); schools.Clear(); }
            int pops = 0;
            foreach (var o in shop.AllOffspring) if (offspring.Add(o.Id) && !fresh) pops++;
            var formed = new List<string>();
            foreach (var c in shop.Containers)
            {
                var now = new HashSet<string>(SchoolsIn(c));
                if (schools.TryGetValue(c.Id, out var had) && now.Any(sp => !had.Contains(sp))) formed.Add(c.Id);
                schools[c.Id] = now;
            }
            return (pops, formed);
        }
    }
}
