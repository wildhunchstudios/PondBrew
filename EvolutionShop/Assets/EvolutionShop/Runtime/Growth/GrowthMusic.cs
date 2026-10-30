using System.Threading.Tasks;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-21 (GR.62): the music. Three calm loops written and played in code (<see cref="GrowthMusicScore"/>): the night, the
    /// day and the Grand Exhibition's day. Each is rendered once, off the main thread, the one wanted first; the loop playing
    /// follows the shop (night, day, the exhibition) and crossfades over three seconds when it changes. It is quieter for a
    /// moment under the service and discovery cues (a sale, a discovery, a new colour, a star, an era, a wish granted, the
    /// exhibition won), so it never covers them, and plays on through the pause screen. Its own volume (Music) and the
    /// master volume scale it. In validation runs it stays silent and renders nothing unless a route asks for it.
    /// </summary>
    public sealed class GrowthMusic : MonoBehaviour
    {
        /// <summary>The loudest the music plays at Music 100 (the sounds are at their full gains), the crossfade, and the duck.</summary>
        public const float BaseGain = 0.42f, Crossfade = 3f, DuckGain = 0.55f, DuckAttack = 0.12f, DuckHold = 1.6f, DuckRelease = 1.2f;

        GrowthSession session;
        readonly AudioClip[] clips = new AudioClip[3];
        readonly float[][] rendered = new float[3][];
        readonly Task[] rendering = new Task[3];
        AudioSource current, previous;
        float duck = 1, duckHold, fadeIn, fadeOut;
        bool started;

        /// <summary>Validation: the loop playing (null before the first is ready or when off), the duck now, and how often it ducked.</summary>
        public MusicTrack? Playing { get; private set; }
        public float Duck => duck;
        public int Ducks { get; private set; }
        public float CurrentVolume => current != null ? current.volume : 0;
        public float PreviousVolume => previous != null && previous.isPlaying ? previous.volume : 0;
        public bool CurrentPlaying => current != null && current.isPlaying;
        public bool Ready(MusicTrack track) => clips[(int)track] != null;
        public AudioClip ClipOf(MusicTrack track) => clips[(int)track];
        /// <summary>Validation: when the route turns the music on (it is off in validation runs otherwise).</summary>
        public bool OnInValidation;

        public void Initialize(GrowthSession owner)
        {
            session = owner;
            current = Voice("Music / A"); previous = Voice("Music / B");
        }

        AudioSource Voice(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var v = go.AddComponent<AudioSource>();
            v.playOnAwake = false; v.loop = true; v.volume = 0; v.spatialBlend = 0; v.priority = 0; v.dopplerLevel = 0;
            return v;
        }

        /// <summary>The loop the shop wants now: the exhibition's on its day, else the day's while open, else the night's.</summary>
        public static MusicTrack WantedFor(GrowthShop shop) =>
            shop == null || shop.Phase != GrowthPhase.Day ? MusicTrack.Night : shop.ExhibitionToday ? MusicTrack.Exhibition : MusicTrack.Day;

        bool On => session != null && (!session.ValidationMode || OnInValidation);

        /// <summary>Starts rendering the loops (the wanted one first), each on its own task.</summary>
        void StartRendering(MusicTrack first)
        {
            started = true;
            Render(first);
            foreach (MusicTrack t in System.Enum.GetValues(typeof(MusicTrack))) Render(t);
        }

        void Render(MusicTrack track)
        {
            int i = (int)track;
            if (rendering[i] != null) return;
            rendering[i] = Task.Run(() => { var data = GrowthMusicScore.Render(track); lock (rendered) rendered[i] = data; });
        }

        /// <summary>The shop's events: a service or discovery cue ducks the music for a moment.</summary>
        public void Event(GrowthEvent e)
        {
            switch (e.Kind)
            {
                case GrowthEventKind.Sale: case GrowthEventKind.Discovery: case GrowthEventKind.NewColour: case GrowthEventKind.StarUp:
                case GrowthEventKind.EraUp: case GrowthEventKind.WishGranted: case GrowthEventKind.ExhibitionWon: case GrowthEventKind.CultureOwned:
                    duckHold = DuckHold; Ducks++; break;
            }
        }

        void Update()
        {
            if (session == null || session.Shop == null) return;
            float dt = Time.unscaledDeltaTime;
            if (!On)
            {
                if (current.isPlaying || previous.isPlaying) { current.Stop(); previous.Stop(); }
                Playing = null;
                return;
            }
            var wanted = WantedFor(session.Shop);
            if (!started) StartRendering(wanted);
            // Loops finished rendering become clips here (Unity's objects are made on the main thread).
            for (int i = 0; i < 3; i++)
            {
                if (clips[i] != null) continue;
                float[] data; lock (rendered) data = rendered[i];
                if (data == null) continue;
                var clip = AudioClip.Create("GR-21 music: " + GrowthMusicScore.Title((MusicTrack)i), data.Length / GrowthMusicScore.Channels, GrowthMusicScore.Channels, GrowthMusicScore.SampleRate, false);
                clip.SetData(data, 0);
                clips[i] = clip;
                lock (rendered) rendered[i] = null;
            }

            // A change of loop: the playing one becomes the one fading out; the wanted one starts from its beginning.
            if (Playing != wanted && clips[(int)wanted] != null)
            {
                var t = previous; previous = current; current = t;
                current.Stop(); current.clip = clips[(int)wanted]; current.volume = 0; current.Play();
                fadeOut = Playing != null ? fadeIn : 0; fadeIn = 0;
                Playing = wanted;
            }

            // The duck: down quickly at a cue, held, then back up slowly.
            if (duckHold > 0) { duckHold -= dt; duck = Mathf.MoveTowards(duck, DuckGain, dt * (1 - DuckGain) / DuckAttack); }
            else duck = Mathf.MoveTowards(duck, 1, dt * (1 - DuckGain) / DuckRelease);

            // The crossfade: the new loop in and the old one out over the same three seconds.
            float level = BaseGain * session.MusicVolume * duck;
            fadeIn = Mathf.MoveTowards(fadeIn, Playing != null ? 1 : 0, dt / Crossfade);
            fadeOut = Mathf.MoveTowards(fadeOut, 0, dt / Crossfade);
            current.volume = level * fadeIn;
            if (previous.isPlaying)
            {
                previous.volume = level * fadeOut;
                if (fadeOut <= 0) previous.Stop();
            }
        }

        void OnDestroy()
        {
            foreach (var c in clips) if (c) Destroy(c);
        }
    }
}
