using System;
using System.Collections.Generic;
using System.Globalization;

namespace EvolutionShop.Growth
{
    /// <summary>GR-21: the three pieces of music, one for each mood of the shop.</summary>
    public enum MusicTrack { Night, Day, Exhibition }

    /// <summary>GR-21: the small instruments the music is played on, each made here in code (nothing recorded).</summary>
    public enum MusicVoice { MusicBox, Vibraphone, Glockenspiel, Marimba, EPiano, Pad, Bass, SoftBass, Kick, Brush, Shaker, Snap, Triangle, Drop }

    /// <summary>One note of a piece: when (in beats), how long, which pitch (MIDI) on which instrument, how loud, where in the
    /// stereo field (-1 left to 1 right) and how much of it goes to the reverb. Percussion ignores the pitch.</summary>
    public struct MusicNote
    {
        public double Beat, Beats;
        public int Midi;
        public MusicVoice Voice;
        public float Velocity, Pan, Send;
        /// <summary>The note's own noise seed (its place in the piece), so every pass of a loop sounds the same.</summary>
        public int Seed;
    }

    /// <summary>GR-21: a piece of music as notes, with its tempo, length and reverb.</summary>
    public sealed class MusicPiece
    {
        public MusicTrack Track;
        public string Title;
        public double Bpm;
        public int Bars, BeatsPerBar = 4;
        /// <summary>How much of the reverb is mixed in (0 none).</summary>
        public float Reverb;
        public readonly List<MusicNote> Notes = new List<MusicNote>();
        public double Beats => Bars * BeatsPerBar;
        public double Seconds => Beats * 60.0 / Bpm;
        public int LoopFrames => (int)Math.Round(Seconds * GrowthMusicScore.SampleRate);
    }

    /// <summary>
    /// GR-21 (GR.62, the user's choice 2026-10-29): the game's music, written here as notes and played by small instruments made
    /// in code, like the game's sounds: original, nothing recorded or bought. Three calm loops: the night (72 bpm, D major, a
    /// music box over a slow pad, with drops of water), the day (96 bpm, F major, a vibraphone tune over an electric piano, a
    /// light bass and brushes) and the exhibition (108 bpm, G major, a glockenspiel over marimba, with claps and a triangle).
    /// Each is rendered twice in a row and the second pass kept, so notes and the reverb ringing over the end carry on into the
    /// start: the loop has no seam. Rendering takes no Unity (the runtime plays the result) and is the same every time.
    /// </summary>
    public static class GrowthMusicScore
    {
        public const int SampleRate = 32000, Channels = 2;
        /// <summary>The loudest sample a rendered loop may reach (about -1 dBFS), and the level it is set to (about -20 dBFS RMS).</summary>
        public const float PeakLimit = 0.89f, TargetRms = 0.1f;

        // ---------------------------------------------------------------- the score

        // Each chord: its bass note and the pad's or piano's notes above it (MIDI; 60 is middle C).
        static readonly Dictionary<string, (int bass, int[] notes)> NightChords = new Dictionary<string, (int, int[])>
        {
            ["Dmaj7"] = (38, new[] { 50, 54, 57, 61 }),
            ["Bm9"] = (35, new[] { 50, 54, 57, 61 }),
            ["Gmaj7"] = (43, new[] { 50, 54, 59, 62 }),
            ["Asus4"] = (45, new[] { 52, 57, 62, 64 }),
            ["A"] = (45, new[] { 52, 57, 61, 64 }),
            ["F#m7"] = (42, new[] { 52, 57, 61, 64 }),
            ["Gmaj9"] = (43, new[] { 50, 54, 57, 59 }),
            ["Em9"] = (40, new[] { 50, 54, 55, 59 }),
            ["A6"] = (45, new[] { 52, 54, 57, 61 }),
            ["Bm7"] = (47, new[] { 50, 54, 57, 59 }),
            ["Em7"] = (40, new[] { 50, 52, 55, 59 }),
            ["A7sus4"] = (45, new[] { 50, 55, 57, 64 }),
        };
        const string NightHarmony = "Dmaj7 | Dmaj7 | Bm9 | Bm9 | Gmaj7 | Gmaj7 | Asus4 | A | Dmaj7 | F#m7 | Gmaj9 | Em9 | Gmaj7 | A6 | Bm7 Em7 | A7sus4 A";
        const string NightTune =
            "r:1 78:1 81:1 85:1 | 86:2.5 85:0.5 81:1 | r:1 78:1 81:1 86:1 | 85:3 r:1 | r:1 79:1 83:1 86:1 | 90:2 88:1 86:1 | 88:3 r:1 | 85:2 81:2 |" +
            " r:1 78:1 81:1 85:1 | 88:2 85:1 81:1 | 83:1 81:1 79:1 81:1 | 78:3 r:1 | r:1 79:1 83:1 86:1 | 85:2 83:1 81:1 | 83:2 79:2 | 81:3 r:1";

        static readonly Dictionary<string, (int bass, int[] notes)> DayChords = new Dictionary<string, (int, int[])>
        {
            ["Fmaj7"] = (41, new[] { 53, 57, 60, 64 }),
            ["Dm7"] = (38, new[] { 53, 57, 60, 62 }),
            ["Gm7"] = (43, new[] { 55, 58, 62, 65 }),
            ["C9"] = (36, new[] { 52, 58, 62, 67 }),
            ["Am7"] = (45, new[] { 52, 55, 60, 64 }),
            ["Bbmaj7"] = (46, new[] { 53, 57, 62, 65 }),
            ["C7sus4"] = (36, new[] { 53, 55, 58, 60 }),
            ["C7"] = (36, new[] { 52, 55, 58, 60 }),
        };
        const string DayHarmony = "Fmaj7 | Dm7 | Gm7 | C9 | Fmaj7 | Am7 | Bbmaj7 | C7sus4 C7 | Fmaj7 | Dm7 | Gm7 | C9 | Bbmaj7 | Am7 Dm7 | Gm7 C7 | Fmaj7";
        const string DayTune =
            "81:1 84:1 81:0.5 79:0.5 77:1 | 74:1.5 77:0.5 81:2 | 82:1 81:0.5 79:0.5 77:1 74:1 | 76:2 r:1 79:1 | 81:1 84:1 86:1 84:1 | 81:1.5 79:0.5 76:2 | 77:1 81:1 86:1 84:1 | 82:2 79:1 76:1 |" +
            " 77:0.5 81:0.5 84:1 r:0.5 84:0.5 86:1 | 84:1 81:1 77:2 | 79:1 82:1 86:1 84:1 | 86:2 84:1 79:1 | 81:2 82:1 81:1 | 79:1 76:1 77:1 81:1 | 79:1 82:1 79:1 76:1 | 77:3 r:1";

        static readonly Dictionary<string, (int bass, int[] notes)> ExhibitionChords = new Dictionary<string, (int, int[])>
        {
            ["Gmaj7"] = (43, new[] { 55, 59, 62, 66 }),
            ["Em7"] = (40, new[] { 55, 59, 62, 64 }),
            ["Cmaj7"] = (48, new[] { 52, 55, 59, 64 }),
            ["D7sus4"] = (38, new[] { 55, 57, 60, 62 }),
            ["D7"] = (38, new[] { 54, 57, 60, 62 }),
            ["Bm7"] = (47, new[] { 54, 57, 59, 62 }),
            ["Am7"] = (45, new[] { 52, 55, 60, 64 }),
            ["D"] = (38, new[] { 54, 57, 62, 66 }),
            ["G/B"] = (47, new[] { 55, 59, 62, 67 }),
            ["Cmaj9"] = (48, new[] { 55, 59, 62, 64 }),
            ["G"] = (43, new[] { 55, 59, 62, 67 }),
        };
        const string ExhibitionHarmony = "Gmaj7 | Em7 | Cmaj7 | D7sus4 D7 | Gmaj7 | Bm7 | Cmaj7 Am7 | D | Em7 | Cmaj7 | G/B Am7 | D7 | Cmaj9 | Bm7 Em7 | Am7 D7 | G";
        const string ExhibitionTune =
            "83:1 86:1 91:1 90:0.5 88:0.5 | 86:1.5 83:0.5 79:2 | 88:1 86:0.5 84:0.5 83:1 84:1 | 86:2 r:1 81:1 | 83:1 86:1 90:1 91:1 | 90:1.5 88:0.5 86:2 | 88:1 84:1 81:1 84:1 | 86:3 r:1 |" +
            " 83:1 79:1 76:1 79:1 | 84:1.5 83:0.5 79:2 | 83:1 86:1 84:1 81:1 | 90:2 86:1 84:1 | 88:1 86:1 83:1 86:1 | 90:1 86:1 83:1 79:1 | 84:1 81:1 78:1 81:1 | 79:3 r:1";

        /// <summary>The piece for a track, its notes laid out in beats.</summary>
        public static MusicPiece Piece(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Day: return Day();
                case MusicTrack.Exhibition: return Exhibition();
                default: return Night();
            }
        }

        public static string Title(MusicTrack track) => track == MusicTrack.Day ? "Open for the day" : track == MusicTrack.Exhibition ? "The Grand Exhibition" : "Moonlit tanks";

        static MusicPiece Night()
        {
            var p = new MusicPiece { Track = MusicTrack.Night, Title = Title(MusicTrack.Night), Bpm = 72, Bars = 16, Reverb = 0.5f };
            foreach (var (beat, beats, name) in Harmony(NightHarmony, p.BeatsPerBar))
            {
                var (bass, notes) = NightChords[name];
                foreach (int m in notes) Add(p, beat, beats + 0.1, m, MusicVoice.Pad, 0.085f, Spread(m), 0.6f);
                Add(p, beat, beats - 0.1, bass, MusicVoice.SoftBass, 0.3f, 0, 0.15f);
            }
            Tune(p, NightTune, MusicVoice.MusicBox, 0.42f, 0.15f, 0.5f, 0);
            // From the ninth bar the tune is doubled an octave down, softly, on the vibraphone: the second half warms up.
            Tune(p, NightTune, MusicVoice.Vibraphone, 0.12f, -0.25f, 0.45f, -12, 8);
            // Drops of water in the tanks, here and there.
            var drops = new[] { (1, 3.5, 96), (3, 2.75, 93), (5, 3.25, 98), (7, 1.5, 91), (9, 1.75, 95), (11, 3.5, 96), (13, 2.25, 93), (15, 3.75, 98) };
            for (int i = 0; i < drops.Length; i++)
                Add(p, drops[i].Item1 * p.BeatsPerBar + drops[i].Item2, 0.25, drops[i].Item3, MusicVoice.Drop, 0.15f, i % 2 == 0 ? -0.55f : 0.55f, 0.7f);
            return p;
        }

        static MusicPiece Day()
        {
            var p = new MusicPiece { Track = MusicTrack.Day, Title = Title(MusicTrack.Day), Bpm = 96, Bars = 16, Reverb = 0.3f };
            var chords = new List<(double beat, double beats, string name)>(Harmony(DayHarmony, p.BeatsPerBar));
            for (int c = 0; c < chords.Count; c++)
            {
                var (beat, beats, name) = chords[c];
                var (bass, notes) = DayChords[name];
                int nextBass = DayChords[chords[(c + 1) % chords.Count].name].bass;
                // The electric piano: on the beat and on the "and" of two (a whole bar), or on each half.
                foreach (int m in notes)
                {
                    if (beats >= 4) { Add(p, beat, 1.5, m, MusicVoice.EPiano, 0.2f, -0.2f, 0.25f); Add(p, beat + 1.5, 1.0, m, MusicVoice.EPiano, 0.14f, -0.2f, 0.25f); }
                    else Add(p, beat, 1.5, m, MusicVoice.EPiano, 0.18f, -0.2f, 0.25f);
                }
                // The bass: the root, the fifth, and a step leading into the next chord.
                Add(p, beat, 1.4, bass, MusicVoice.Bass, 0.38f, 0, 0.1f);
                if (beats >= 4) Add(p, beat + 2, 0.9, Fifth(bass), MusicVoice.Bass, 0.3f, 0, 0.1f);
                if (beat + beats >= Math.Floor(beat / 4) * 4 + 4) Add(p, beat + beats - 0.5, 0.45, nextBass - 1, MusicVoice.Bass, 0.23f, 0, 0.1f);
                // From the ninth bar, a marimba walks up and down the chord in eighths.
                if (beat >= 32)
                    for (int e = 0; e < beats * 2; e++)
                        Add(p, beat + e * 0.5, 0.5, notes[ArpOrder[e % ArpOrder.Length]] + 12, MusicVoice.Marimba, e % 2 == 0 ? 0.2f : 0.15f, -0.4f, 0.3f);
            }
            Tune(p, DayTune, MusicVoice.Vibraphone, 0.4f, 0.2f, 0.4f, 0);
            Drums(p, kick: 0.26f, back: MusicVoice.Brush, backVelocity: 0.35f, shaker: 0.12f, triangle: 0);
            return p;
        }

        static MusicPiece Exhibition()
        {
            var p = new MusicPiece { Track = MusicTrack.Exhibition, Title = Title(MusicTrack.Exhibition), Bpm = 108, Bars = 16, Reverb = 0.32f };
            var chords = new List<(double beat, double beats, string name)>(Harmony(ExhibitionHarmony, p.BeatsPerBar));
            for (int c = 0; c < chords.Count; c++)
            {
                var (beat, beats, name) = chords[c];
                var (bass, notes) = ExhibitionChords[name];
                int nextBass = ExhibitionChords[chords[(c + 1) % chords.Count].name].bass;
                foreach (int m in notes) Add(p, beat, beats + 0.05, m, MusicVoice.Pad, 0.06f, Spread(m), 0.5f);
                Add(p, beat, 0.9, bass, MusicVoice.Bass, 0.39f, 0, 0.1f);
                if (beats >= 4)
                {
                    Add(p, beat + 1.5, 0.45, bass, MusicVoice.Bass, 0.26f, 0, 0.1f);
                    Add(p, beat + 2, 0.9, Fifth(bass), MusicVoice.Bass, 0.33f, 0, 0.1f);
                    Add(p, beat + 3.5, 0.45, nextBass - 1, MusicVoice.Bass, 0.24f, 0, 0.1f);
                }
                else Add(p, beat + 1.5, 0.45, bass + 12, MusicVoice.Bass, 0.22f, 0, 0.1f);
                for (int e = 0; e < beats * 2; e++)
                    Add(p, beat + e * 0.5, 0.5, notes[ArpOrder[e % ArpOrder.Length]] + 12, MusicVoice.Marimba, e % 2 == 0 ? 0.16f : 0.12f, -0.35f, 0.3f);
            }
            Tune(p, ExhibitionTune, MusicVoice.Glockenspiel, 0.36f, 0.2f, 0.45f, 0);
            // From the ninth bar the vibraphone joins the tune an octave down.
            Tune(p, ExhibitionTune, MusicVoice.Vibraphone, 0.16f, -0.1f, 0.35f, -12, 8);
            Drums(p, kick: 0.36f, back: MusicVoice.Snap, backVelocity: 0.28f, shaker: 0.13f, triangle: 0.13f);
            return p;
        }

        static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 3, 2 };

        static int Fifth(int bass) => bass + 7 > 50 ? bass - 5 : bass + 7;

        static float Spread(int midi) => Math.Max(-0.45f, Math.Min(0.45f, (midi - 57) / 16f));

        /// <summary>Kick on one and three, the backbeat (brush or snap) on two and four, a shaker in eighths and, when asked, a
        /// triangle on the first beat of every fourth bar.</summary>
        static void Drums(MusicPiece p, float kick, MusicVoice back, float backVelocity, float shaker, float triangle)
        {
            for (int bar = 0; bar < p.Bars; bar++)
            {
                double b0 = bar * p.BeatsPerBar;
                Add(p, b0, 0.5, 36, MusicVoice.Kick, kick, 0, 0.05f);
                Add(p, b0 + 2, 0.5, 36, MusicVoice.Kick, kick * 0.75f, 0, 0.05f);
                Add(p, b0 + 1, 0.5, 38, back, backVelocity, 0.1f, 0.25f);
                Add(p, b0 + 3, 0.5, 38, back, backVelocity, 0.1f, 0.25f);
                for (int e = 0; e < 8; e++) Add(p, b0 + e * 0.5, 0.25, 70, MusicVoice.Shaker, e % 2 == 0 ? shaker * 0.7f : shaker, 0.45f, 0.15f);
                if (triangle > 0 && bar % 4 == 0) Add(p, b0, 1, 81, MusicVoice.Triangle, triangle, -0.5f, 0.5f);
            }
        }

        static void Add(MusicPiece p, double beat, double beats, int midi, MusicVoice voice, float velocity, float pan, float send) =>
            p.Notes.Add(new MusicNote { Beat = beat, Beats = beats, Midi = midi, Voice = voice, Velocity = velocity, Pan = pan, Send = send, Seed = p.Notes.Count * 7919 + 17 });

        /// <summary>The chords of a harmony line ("Dmaj7 | Bm7 Em7 | ..."): each bar's chords share it equally.</summary>
        public static IEnumerable<(double beat, double beats, string name)> Harmony(string text, int beatsPerBar)
        {
            var bars = text.Split('|');
            for (int b = 0; b < bars.Length; b++)
            {
                var names = bars[b].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                double each = beatsPerBar / (double)names.Length;
                for (int i = 0; i < names.Length; i++) yield return (b * beatsPerBar + i * each, each, names[i]);
            }
        }

        /// <summary>The notes of a tune line ("81:1 r:0.5 79:0.5 | ..."): a MIDI pitch or r (a rest), and its length in beats.
        /// Bars are separated by |; each must fill its bar, or the line is refused.</summary>
        public static List<(double beat, double beats, int midi)> TuneNotes(string text, int beatsPerBar)
        {
            var list = new List<(double, double, int)>();
            var bars = text.Split('|');
            for (int b = 0; b < bars.Length; b++)
            {
                double at = b * beatsPerBar, filled = 0;
                foreach (var token in bars[b].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = token.Split(':');
                    double len = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    if (parts[0] != "r") list.Add((at + filled, len, int.Parse(parts[0], CultureInfo.InvariantCulture)));
                    filled += len;
                }
                if (Math.Abs(filled - beatsPerBar) > 1e-9) throw new FormatException("Bar " + (b + 1) + " of a tune holds " + filled + " beats, not " + beatsPerBar + ".");
            }
            return list;
        }

        static void Tune(MusicPiece p, string text, MusicVoice voice, float velocity, float pan, float send, int transpose, int fromBar = 0)
        {
            foreach (var (beat, beats, midi) in TuneNotes(text, p.BeatsPerBar))
                if (beat >= fromBar * p.BeatsPerBar) Add(p, beat, beats, midi + transpose, voice, velocity, pan, send);
        }

        /// <summary>The harmony and tune lines, for the tests: each tune's bars must be full and its chords known.</summary>
        public static IEnumerable<(MusicTrack track, string harmony, string tune, IEnumerable<string> chords)> Lines()
        {
            yield return (MusicTrack.Night, NightHarmony, NightTune, NightChords.Keys);
            yield return (MusicTrack.Day, DayHarmony, DayTune, DayChords.Keys);
            yield return (MusicTrack.Exhibition, ExhibitionHarmony, ExhibitionTune, ExhibitionChords.Keys);
        }

        // ---------------------------------------------------------------- rendering

        /// <summary>A track's loop, ready to play: interleaved stereo at <see cref="SampleRate"/>, seamless, set to
        /// <see cref="TargetRms"/> and never above <see cref="PeakLimit"/>.</summary>
        public static float[] Render(MusicTrack track)
        {
            var piece = Piece(track);
            var two = RenderPasses(piece, 2);
            int frames = piece.LoopFrames;
            var loop = new float[frames * Channels];
            Array.Copy(two, frames * Channels, loop, 0, loop.Length);
            Normalize(loop);
            return loop;
        }

        /// <summary>The piece played the given number of times in a row, as one interleaved stereo buffer (not normalized). Notes
        /// and the reverb ring on from one pass into the next, so every pass after the first is the loop.</summary>
        public static float[] RenderPasses(MusicPiece piece, int passes)
        {
            int loop = piece.LoopFrames, frames = loop * passes;
            var left = new float[frames]; var right = new float[frames]; var send = new float[frames];
            var synth = new Synth();
            for (int pass = 0; pass < passes; pass++)
                foreach (var note in piece.Notes)
                    synth.Play(note, piece.Bpm, left, right, send, pass * loop);
            if (piece.Reverb > 0) new Reverb().Process(send, left, right, piece.Reverb);
            var outBuffer = new float[frames * Channels];
            for (int i = 0; i < frames; i++) { outBuffer[2 * i] = left[i]; outBuffer[2 * i + 1] = right[i]; }
            return outBuffer;
        }

        /// <summary>Sets a loop to <see cref="TargetRms"/>, lowered as far as it must be to stay under <see cref="PeakLimit"/>.</summary>
        public static void Normalize(float[] data)
        {
            double sum = 0; float peak = 0;
            foreach (float v in data) { sum += v * (double)v; float a = Math.Abs(v); if (a > peak) peak = a; }
            if (peak <= 0) return;
            double rms = Math.Sqrt(sum / data.Length);
            float gain = (float)Math.Min(TargetRms / rms, PeakLimit / peak);
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        public static (float peak, float rms) Level(float[] data)
        {
            double sum = 0; float peak = 0;
            foreach (float v in data) { sum += v * (double)v; float a = Math.Abs(v); if (a > peak) peak = a; }
            return (peak, data.Length == 0 ? 0 : (float)Math.Sqrt(sum / data.Length));
        }

        // ---------------------------------------------------------------- the instruments

        /// <summary>The instruments. A sine table (4096 steps, interpolated) keeps rendering quick; each renderer has its own
        /// scratch buffer, so tracks can render on separate threads.</summary>
        sealed class Synth
        {
            const int TableSize = 4096;
            static readonly float[] Table = MakeTable();
            static float[] MakeTable()
            {
                var t = new float[TableSize + 1];
                for (int i = 0; i <= TableSize; i++) t[i] = (float)Math.Sin(2 * Math.PI * i / TableSize);
                return t;
            }
            /// <summary>sin(2π·phase) for a phase in cycles, from the table.</summary>
            static float Sin(double phase)
            {
                phase -= Math.Floor(phase);
                double x = phase * TableSize; int i = (int)x;
                return Table[i] + (Table[i + 1] - Table[i]) * (float)(x - i);
            }

            float[] mono = new float[SampleRate * 8], monoRight = new float[SampleRate * 8];
            const double Nyquist = SampleRate * 0.45;

            static double Hz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

            public void Play(MusicNote n, double bpm, float[] left, float[] right, float[] send, int offset)
            {
                double spb = 60.0 / bpm, held = n.Beats * spb;
                int start = offset + (int)Math.Round(n.Beat * spb * SampleRate);
                if (start >= left.Length) return;
                int count; bool stereo = false;
                switch (n.Voice)
                {
                    case MusicVoice.MusicBox: count = Bar(n, new[] { 1.0, 2.0, 3.0, 4.2 }, new[] { 1f, 0.25f, 0.1f, 0.06f }, new[] { 1.4, 0.6, 0.3, 0.12 }, 0.002, -1, 0); break;
                    case MusicVoice.Vibraphone: count = Bar(n, new[] { 1.0, 4.0, 10.0 }, new[] { 1f, 0.18f, 0.03f }, new[] { 1.6, 0.35, 0.08 }, 0.003, held + 0.12, 5.2); break;
                    case MusicVoice.Glockenspiel: count = Bar(n, new[] { 1.0, 2.756, 5.404, 8.933 }, new[] { 1f, 0.35f, 0.12f, 0.05f }, new[] { 1.1, 0.4, 0.15, 0.07 }, 0.001, -1, 0); break;
                    case MusicVoice.Marimba: count = Bar(n, new[] { 1.0, 4.0, 10.0 }, new[] { 1f, 0.22f, 0.05f }, new[] { 0.38, 0.08, 0.03 }, 0.0015, -1, 0); break;
                    case MusicVoice.Triangle: count = Bar(n, new[] { 1.0, 2.76, 5.4 }, new[] { 1f, 0.4f, 0.2f }, new[] { 0.9, 0.5, 0.3 }, 0.001, -1, 0, 4200); break;
                    case MusicVoice.EPiano: count = EPiano(n, held); break;
                    case MusicVoice.Pad: count = Pad(n, held, bpm >= 100 ? 0.5 : 1.2); stereo = true; break;
                    case MusicVoice.Bass: count = Bass(n, held, 0.008, true); break;
                    case MusicVoice.SoftBass: count = Bass(n, held, 0.04, false); break;
                    case MusicVoice.Kick: count = Kick(n); break;
                    case MusicVoice.Brush: count = Noise(n, 0.3, 0.01, 0.09, 0.25f, false); break;
                    case MusicVoice.Shaker: count = Noise(n, 0.12, 0.004, 0.035, 1f, true); break;
                    case MusicVoice.Snap: count = Snap(n); break;
                    case MusicVoice.Drop: count = Drop(n); break;
                    default: return;
                }
                count = Math.Min(count, left.Length - start);
                double angle = (n.Pan + 1) * Math.PI / 4;
                float gl = (float)Math.Cos(angle), gr = (float)Math.Sin(angle);
                for (int i = 0; i < count; i++)
                {
                    float l = stereo ? mono[i] : mono[i] * gl, r = stereo ? monoRight[i] : mono[i] * gr;
                    left[start + i] += l; right[start + i] += r;
                    send[start + i] += (l + r) * 0.5f * n.Send;
                }
            }

            float[] Need(int count)
            {
                if (mono.Length < count) { mono = new float[count]; monoRight = new float[count]; }
                Array.Clear(mono, 0, count); Array.Clear(monoRight, 0, count);
                return mono;
            }

            /// <summary>A struck bar or tine: partials at the given ratios, each dying away at its own rate. With a damp time the
            /// note is stopped there (a damper); with a tremolo rate it shimmers (a vibraphone's fan).</summary>
            int Bar(MusicNote n, double[] ratios, float[] amps, double[] taus, double attack, double damp, double tremolo, double fixedHz = 0)
            {
                double f = fixedHz > 0 ? fixedHz : Hz(n.Midi), longest = 0;
                foreach (var t in taus) longest = Math.Max(longest, t);
                double seconds = Math.Min(6, damp > 0 ? damp + 0.6 : longest * 5);
                int count = (int)(seconds * SampleRate);
                var d = Need(count);
                int attackN = Math.Max(1, (int)(attack * SampleRate)), dampAt = damp > 0 ? (int)(damp * SampleRate) : int.MaxValue;
                for (int k = 0; k < ratios.Length; k++)
                {
                    double fk = f * ratios[k];
                    if (fk >= Nyquist) continue;
                    double inc = fk / SampleRate, phase = 0, env = amps[k] * n.Velocity, decay = Math.Exp(-1.0 / (taus[k] * SampleRate));
                    double release = Math.Exp(-1.0 / (0.12 * SampleRate));
                    for (int i = 0; i < count; i++)
                    {
                        float a = (float)env * (i < attackN ? i / (float)attackN : 1f);
                        d[i] += a * Sin(phase);
                        phase += inc;
                        env *= i >= dampAt ? decay * release : decay;
                    }
                }
                if (tremolo > 0)
                    for (int i = 0; i < count; i++) d[i] *= 1f - 0.18f * (0.5f + 0.5f * Sin(tremolo * i / SampleRate));
                return Ends(d, count);
            }

            /// <summary>An electric piano: a frequency-modulated tone whose brightness fades as it rings, with a short bell on top.</summary>
            int EPiano(MusicNote n, double held)
            {
                double f = Hz(n.Midi);
                int count = (int)((held + 0.7) * SampleRate), heldN = (int)(held * SampleRate), attackN = (int)(0.005 * SampleRate);
                var d = Need(count);
                double inc = f / SampleRate, phase = 0;
                double decay = Math.Exp(-1.0 / (1.5 * SampleRate)), release = Math.Exp(-1.0 / (0.12 * SampleRate)), env = n.Velocity;
                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)SampleRate, index = 1.2 * Math.Exp(-t / 0.4) + 0.2;
                    float tone = Sin(phase + index * Sin(phase) / (2 * Math.PI));
                    float bell = 3 * f < Nyquist ? Sin(phase * 3) * 0.12f * (float)Math.Exp(-t / 0.05) : 0;
                    d[i] = (float)env * (tone + bell) * (i < attackN ? i / (float)attackN : 1f);
                    phase += inc;
                    env *= i >= heldN ? decay * release : decay;
                }
                return Ends(d, count);
            }

            /// <summary>A soft pad: two voices a little apart in pitch, one on each side, each a gentle sawtooth (four harmonics),
            /// swelling in and fading out after the note.</summary>
            int Pad(MusicNote n, double held, double attack)
            {
                double f = Hz(n.Midi);
                int count = (int)((held + 2.5) * SampleRate), heldN = (int)(held * SampleRate), attackN = (int)(attack * SampleRate);
                Need(count);
                double release = Math.Exp(-1.0 / (0.8 * SampleRate));
                for (int side = 0; side < 2; side++)
                {
                    var d = side == 0 ? mono : monoRight;
                    double cents = side == 0 ? -6 : 6, fs = f * Math.Pow(2, cents / 1200.0);
                    for (int h = 1; h <= 4; h++)
                    {
                        double fh = fs * h;
                        if (fh >= Nyquist) break;
                        double inc = fh / SampleRate, phase = side * 0.37 + h * 0.11, env = 1;
                        float amp = n.Velocity * (float)Math.Pow(h, -1.6);
                        for (int i = 0; i < count; i++)
                        {
                            float swell = i < attackN ? (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / attackN)) : 1f;
                            d[i] += amp * swell * (float)env * Sin(phase);
                            phase += inc;
                            if (i >= heldN) env *= release;
                        }
                    }
                }
                Ends(monoRight, count);
                return Ends(mono, count);
            }

            /// <summary>A round bass: a sine with its second (and, plucked, third) harmonic, settling after the attack.</summary>
            int Bass(MusicNote n, double held, double attack, bool plucked)
            {
                double f = Hz(n.Midi);
                int count = (int)((held + 0.4) * SampleRate), heldN = (int)(held * SampleRate), attackN = Math.Max(1, (int)(attack * SampleRate));
                var d = Need(count);
                double inc = f / SampleRate, phase = 0, release = Math.Exp(-1.0 / (0.08 * SampleRate)), tail = 1;
                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)SampleRate;
                    float body = Sin(phase) + 0.3f * Sin(phase * 2) + (plucked ? 0.08f * Sin(phase * 3) : 0);
                    float env = (float)((plucked ? 0.6 + 0.4 * Math.Exp(-t / 0.25) : 0.85 + 0.15 * Math.Exp(-t / 0.5)) * tail);
                    d[i] = n.Velocity * body * env * (i < attackN ? i / (float)attackN : 1f);
                    phase += inc;
                    if (i >= heldN) tail *= release;
                }
                return Ends(d, count);
            }

            /// <summary>A soft kick: a sine falling quickly from about 118 Hz to 48 Hz.</summary>
            int Kick(MusicNote n)
            {
                int count = (int)(0.4 * SampleRate);
                var d = Need(count);
                double phase = 0;
                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)SampleRate;
                    d[i] = n.Velocity * Sin(phase) * (float)Math.Exp(-t / 0.16) * Math.Min(1f, i / 32f);
                    phase += (48 + 70 * Math.Exp(-t / 0.03)) / SampleRate;
                }
                return Ends(d, count);
            }

            /// <summary>Filtered noise: darker (a brush) or brighter (a shaker, its low part taken away).</summary>
            int Noise(MusicNote n, double seconds, double attack, double tau, float smooth, bool bright)
            {
                int count = (int)(seconds * SampleRate), attackN = Math.Max(1, (int)(attack * SampleRate));
                var d = Need(count);
                uint s = (uint)n.Seed * 2654435761u + 1013904223u; float low = 0, slow = 0;
                for (int i = 0; i < count; i++)
                {
                    s = unchecked(s * 1664525u + 1013904223u);
                    float white = (s >> 8) / 8388607.5f - 1f;
                    low += (white - low) * smooth;
                    slow += (low - slow) * 0.02f;
                    float v = bright ? low - slow : low;
                    double t = i / (double)SampleRate;
                    d[i] = n.Velocity * v * (float)Math.Exp(-t / tau) * Math.Min(1f, i / (float)attackN);
                }
                return Ends(d, count);
            }

            static readonly double[] Claps = { 0, 0.011, 0.023 };

            /// <summary>A finger snap and clap: three quick bursts of noise through a band around 1.5 kHz, and a short tail.</summary>
            int Snap(MusicNote n)
            {
                int count = (int)(0.25 * SampleRate);
                var d = Need(count);
                uint s = (uint)n.Seed * 2246822519u + 374761393u; float low = 0, band = 0;
                float fc = 2 * (float)Math.Sin(Math.PI * 1500 / SampleRate), q = 0.8f;
                for (int i = 0; i < count; i++)
                {
                    s = unchecked(s * 1664525u + 1013904223u);
                    float white = (s >> 8) / 8388607.5f - 1f;
                    low += fc * band; float high = white - low - q * band; band += fc * high;
                    double t = i / (double)SampleRate, env = 0;
                    foreach (double at in Claps) if (t >= at) env = Math.Max(env, Math.Exp(-(t - at) / 0.007));
                    env = Math.Max(env, t >= 0.023 ? 0.35 * Math.Exp(-(t - 0.023) / 0.05) : 0);
                    d[i] = n.Velocity * band * (float)env * 1.6f;
                }
                return Ends(d, count);
            }

            /// <summary>A drop of water: a short blip rising in pitch.</summary>
            int Drop(MusicNote n)
            {
                double f0 = Hz(n.Midi) * 0.5;
                int count = (int)(0.25 * SampleRate);
                var d = Need(count);
                double phase = 0;
                for (int i = 0; i < count; i++)
                {
                    double t = i / (double)SampleRate;
                    d[i] = n.Velocity * Sin(phase) * (float)Math.Exp(-t / 0.05) * Math.Min(1f, i / 64f);
                    phase += f0 * (1 + 0.9 * Math.Min(1, t / 0.035)) / SampleRate;
                }
                return Ends(d, count);
            }

            /// <summary>Fades a note's last 3 ms to silence, so a cut-off tail never clicks.</summary>
            static int Ends(float[] d, int count)
            {
                int n = Math.Min(96, count / 2);
                for (int i = 0; i < n; i++) d[count - 1 - i] *= i / (float)n;
                return count;
            }
        }

        /// <summary>A small stereo room (Schroeder and Moorer's design, as in Freeverb): six damped echoes in parallel and three
        /// all-pass diffusers on each side, the right a little longer than the left for width.</summary>
        sealed class Reverb
        {
            static readonly int[] Combs = { 810, 862, 927, 984, 1032, 1082 };
            static readonly int[] Passes = { 403, 320, 247 };
            const int Spread = 17;
            const float Feedback = 0.84f, Damp = 0.25f, InputGain = 0.03f;

            public void Process(float[] send, float[] left, float[] right, float wet)
            {
                var l = Channel(send, 0); var r = Channel(send, Spread);
                for (int i = 0; i < left.Length; i++) { left[i] += l[i] * wet; right[i] += r[i] * wet; }
            }

            static float[] Channel(float[] input, int spread)
            {
                var output = new float[input.Length];
                foreach (int length in Combs)
                {
                    var buffer = new float[length + spread]; int index = 0; float store = 0;
                    for (int i = 0; i < input.Length; i++)
                    {
                        float o = buffer[index];
                        store = o * (1 - Damp) + store * Damp;
                        buffer[index] = input[i] * InputGain + store * Feedback;
                        if (++index >= buffer.Length) index = 0;
                        output[i] += o;
                    }
                }
                foreach (int length in Passes)
                {
                    var buffer = new float[length + spread]; int index = 0;
                    for (int i = 0; i < output.Length; i++)
                    {
                        float b = buffer[index], x = output[i];
                        output[i] = -x + b;
                        buffer[index] = x + b * 0.5f;
                        if (++index >= buffer.Length) index = 0;
                    }
                }
                return output;
            }
        }
    }
}
