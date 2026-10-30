using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>GR-25: the kinds of visual effect, each one shared particle system emitting wherever it is wanted.</summary>
    public enum GrowthFx { Bubble, Steam, Sparkle, Heart, Coin, Puff, Ripple, Drop, Spark, Mote, Confetti, Star, Cloud, Glow, Dust }

    /// <summary>
    /// GR-25 (the user's request 2026-10-30, "more vfx in a lot of parts of the game"; lively but calm, bigger only for big
    /// moments): the game's visual effects, made here in code like its sounds (the textures drawn at start, nothing bought).
    /// One particle system per kind of effect emits wherever it is wanted, in the world's space, so a hundred places cost one
    /// draw each. What it shows follows the shop each frame: the stations working (bubbles in the growers' culture, the
    /// siphon drawing it up, the incubator's steam while it incubates, the vat bubbling, the crank's sparks, the hand pump's
    /// drips, hearts over a breeding pair, a turbine's bubbles rising up its columns with the tide engine's mist and a glint
    /// at its lamp, a running pump's glint), the tanks (bubbles from the filter, motes drifting in the water, a splash and a ring
    /// when a creature goes in, a sparkle when a school forms), visitors and sales (coins from a sale, a puff at the door,
    /// sparkles over a happy visitor and a grey cloud over an unhappy one) and the rooms and big moments (dust in the sunlight
    /// by day, warm motes under the lamps at night, a burst of stars and sparkles for a discovery, a new colour, a star or an era, confetti for a room opened).
    /// Reduced motion halves them and keeps them still; the Low preset halves them and drops the ambient ones (motes, dust, the lamps' motes).
    /// Nothing new starts while the game is paused; what is in the air finishes.
    /// </summary>
    public sealed class GrowthEffects : MonoBehaviour
    {
        GrowthSession session;
        readonly Dictionary<GrowthFx, ParticleSystem> systems = new Dictionary<GrowthFx, ParticleSystem>();
        readonly Dictionary<GrowthFx, int> emitted = new Dictionary<GrowthFx, int>();
        readonly List<Object> made = new List<Object>();
        readonly Dictionary<string, float> clocks = new Dictionary<string, float>();
        readonly System.Random rng = new System.Random(25);
        GrowthShop seenShop;
        readonly Dictionary<string, int> tankCounts = new Dictionary<string, int>();
        readonly HashSet<string> schools = new HashSet<string>();
        readonly HashSet<int> visitorsSeen = new HashSet<int>(), visitorsLeft = new HashSet<int>();
        readonly HashSet<int> offspringSeen = new HashSet<int>();

        /// <summary>Validation: how many particles each kind has emitted, and where the last of each was.</summary>
        public int Emitted(GrowthFx fx) => emitted.TryGetValue(fx, out int n) ? n : 0;
        public readonly Dictionary<GrowthFx, Vector3> Last = new Dictionary<GrowthFx, Vector3>();
        public int Alive(GrowthFx fx) => systems.TryGetValue(fx, out var p) ? p.particleCount : 0;
        public int AliveTotal => systems.Values.Sum(p => p.particleCount);
        /// <summary>How much the effects show: 1, halved by Reduced motion and again by the Low preset.</summary>
        public float Intensity => IntensityFor(session != null && session.ReducedMotion, session != null && session.Settings.MatchingPreset() == 0);
        public static float IntensityFor(bool reducedMotion, bool lowPreset) => (reducedMotion ? 0.5f : 1f) * (lowPreset ? 0.5f : 1f);
        bool Ambient => session == null || session.Settings.MatchingPreset() != 0;
        /// <summary>Validation: off in validation runs (as the music is) unless a route turns the effects on, so earlier routes'
        /// frame times and captures stay as they were.</summary>
        public bool OnInValidation;
        bool On => session != null && (!session.ValidationMode || OnInValidation);

        public void Initialize(GrowthSession owner)
        {
            session = owner;
            var shader = Resources.Load<Shader>("EvolutionDioramaUnlit") ?? Shader.Find("EvolutionShop/Diorama Unlit");
            var dot = Make(Draw(64, Dot)); var ring = Make(Draw(64, Ring)); var sparkle = Make(Draw(64, SparkleShape));
            var heart = Make(Draw(64, HeartShape)); var coin = Make(Draw(64, CoinShape, true)); var puff = Make(Draw(64, PuffShape));
            var star = Make(Draw(64, StarShape)); var paper = Make(Draw(32, PaperShape));
            Material Mat(Texture2D tex, bool add, float glow = 1f)
            {
                var m = new Material(shader) { name = "GR-25 " + tex.name };
                m.SetTexture("_MainTex", tex);
                m.SetColor("_BaseColor", new Color(glow, glow, glow, 1));
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_Cull", 0);
                m.renderQueue = 3100;
                made.Add(m);
                return m;
            }
            // Bubbles rise and wobble; steam swells and fades; sparkles twinkle (additive, bright enough to bloom a little).
            Make(GrowthFx.Bubble, Mat(ring, false), gravity: 0, noise: 0.04f, grow: 1.1f, fadeIn: 0.1f, spin: 0);
            Make(GrowthFx.Steam, Mat(puff, false), gravity: -0.05f, noise: 0.3f, grow: 2.4f, fadeIn: 0.25f, spin: 40);
            Make(GrowthFx.Sparkle, Mat(sparkle, true, 1.6f), gravity: 0, noise: 0.15f, grow: 0.4f, fadeIn: 0.05f, spin: 120, twinkle: true);
            Make(GrowthFx.Heart, Mat(heart, false), gravity: -0.15f, noise: 0.2f, grow: 1.2f, fadeIn: 0.15f, spin: 0);
            Make(GrowthFx.Coin, Mat(coin, false), gravity: 1.2f, noise: 0, grow: 1f, fadeIn: 0.02f, spin: 360);
            Make(GrowthFx.Puff, Mat(puff, false), gravity: -0.02f, noise: 0.2f, grow: 2f, fadeIn: 0.1f, spin: 60);
            Make(GrowthFx.Ripple, Mat(ring, false), gravity: 0, noise: 0, grow: 4f, fadeIn: 0.02f, spin: 0, flat: true);
            Make(GrowthFx.Drop, Mat(dot, false), gravity: 1.4f, noise: 0, grow: 0.8f, fadeIn: 0.02f, spin: 0);
            Make(GrowthFx.Spark, Mat(dot, true, 2.2f), gravity: 1.0f, noise: 0, grow: 0.3f, fadeIn: 0.01f, spin: 0, stretch: true);
            Make(GrowthFx.Mote, Mat(dot, true, 1.2f), gravity: 0, noise: 0.08f, grow: 1f, fadeIn: 0.4f, spin: 0, twinkle: true);
            Make(GrowthFx.Confetti, Mat(paper, false), gravity: 0.35f, noise: 0.4f, grow: 1f, fadeIn: 0.02f, spin: 400);
            Make(GrowthFx.Star, Mat(star, true, 1.8f), gravity: 0.2f, noise: 0.1f, grow: 0.6f, fadeIn: 0.03f, spin: 180);
            Make(GrowthFx.Cloud, Mat(puff, false), gravity: -0.03f, noise: 0.15f, grow: 1.3f, fadeIn: 0.15f, spin: 20);
            Make(GrowthFx.Glow, Mat(dot, true, 1.5f), gravity: 0, noise: 0.05f, grow: 1.4f, fadeIn: 0.3f, spin: 0);
            Make(GrowthFx.Dust, Mat(dot, true, 1.2f), gravity: 0, noise: 0.08f, grow: 1f, fadeIn: 0.4f, spin: 0, twinkle: true);
        }

        // ---------------------------------------------------------------- emitting

        /// <summary>Emits particles of a kind at a point: each with a velocity around the one given, its size and life varied a
        /// little, in the colour given. Counts are scaled by the intensity (at least one).</summary>
        public void Emit(GrowthFx fx, Vector3 at, int count, Color colour, Vector3 velocity, float spread = 0.2f, float size = 0.06f, float life = 1f, float radius = 0f, bool scaled = true)
        {
            if (!On || !systems.TryGetValue(fx, out var ps)) return;
            int n = scaled ? Mathf.Max(1, Mathf.RoundToInt(count * Intensity)) : count;
            bool still = session.ReducedMotion;
            for (int i = 0; i < n; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = at + (radius > 0 ? Random3() * radius : Vector3.zero),
                    velocity = velocity + Random3() * spread * (still ? 0.5f : 1f),
                    startSize = size * (0.75f + 0.5f * Rand()),
                    startLifetime = life * (0.8f + 0.4f * Rand()),
                    startColor = colour,
                    rotation = Rand() * 360f,
                    applyShapeToPosition = false,
                };
                ps.Emit(p, 1);
            }
            emitted[fx] = Emitted(fx) + n;
            Last[fx] = at;
        }

        float Rand() => (float)rng.NextDouble();
        Vector3 Random3() { var v = new Vector3(Rand() - 0.5f, Rand() - 0.5f, Rand() - 0.5f) * 2f; return v.sqrMagnitude > 1 ? v.normalized : v; }

        /// <summary>A steady stream: how many to emit this frame for a rate a second (kept per key, so a stream is smooth).</summary>
        int Due(string key, float perSecond, float dt)
        {
            if (perSecond <= 0) { clocks.Remove(key); return 0; }
            clocks.TryGetValue(key, out float c);
            int n = Stream(ref c, perSecond * Intensity, dt);
            clocks[key] = c;
            return n;
        }

        /// <summary>How many of a stream are due this frame at a rate a second, its remainder carried in the clock (so it keeps
        /// its rate whatever the frame rate).</summary>
        public static int Stream(ref float clock, float perSecond, float dt)
        {
            clock += Mathf.Max(0, perSecond) * dt;
            int n = Mathf.FloorToInt(clock);
            clock -= n;
            return n;
        }

        // ---------------------------------------------------------------- the shop's moments

        /// <summary>The shop's events (the session passes each): the big moments and the service cues.</summary>
        public void Event(GrowthEvent e)
        {
            if (!On || session.World == null || session.Shop == null) return;
            var w = session.World;
            switch (e.Kind)
            {
                case GrowthEventKind.Sale:
                    var till = GrowthWorld.CounterPosition + new Vector3(0, 1.15f, 0);
                    Emit(GrowthFx.Coin, till, 7, Color.white, new Vector3(0, 2.2f, 0), 0.9f, 0.09f, 0.9f, 0.05f);
                    Emit(GrowthFx.Sparkle, till, 6, new Color(1f, 0.86f, 0.4f), new Vector3(0, 0.5f, 0), 0.6f, 0.12f, 0.7f, 0.15f);
                    break;
                case GrowthEventKind.Hatched:
                    var inc = GrowthWorld.IncubatorPosition + new Vector3(0, 1.2f, 0);
                    Emit(GrowthFx.Puff, inc, 5, new Color(1f, 1f, 1f, 0.6f), new Vector3(0, 0.5f, 0), 0.4f, 0.17f, 1f, 0.12f);
                    Emit(GrowthFx.Sparkle, inc, 14, new Color(0.7f, 1f, 0.95f), new Vector3(0, 0.8f, 0), 1f, 0.12f, 1f, 0.2f);
                    break;
                case GrowthEventKind.Discovery:
                    var at = GrowthWorld.IncubatorPosition + new Vector3(0, 1.4f, 0);
                    Emit(GrowthFx.Star, at, 18, new Color(1f, 0.85f, 0.35f), new Vector3(0, 1.4f, 0), 1.8f, 0.16f, 1.6f, 0.2f);
                    Emit(GrowthFx.Sparkle, at, 30, new Color(0.9f, 1f, 1f), new Vector3(0, 1f, 0), 1.6f, 0.12f, 1.4f, 0.3f);
                    Emit(GrowthFx.Glow, at, 3, new Color(1f, 0.9f, 0.6f, 0.6f), Vector3.zero, 0.05f, 0.9f, 1.2f);
                    break;
                case GrowthEventKind.NewColour:
                    var c = GrowthWorld.IncubatorPosition + new Vector3(0, 1.4f, 0);
                    for (int i = 0; i < 6; i++)
                        Emit(GrowthFx.Sparkle, c, 5, Color.HSVToRGB(i / 6f, 0.55f, 1f), new Vector3(0, 1f, 0), 1.4f, 0.13f, 1.4f, 0.3f);
                    break;
                case GrowthEventKind.StarUp: case GrowthEventKind.EraUp: case GrowthEventKind.ExhibitionWon:
                    var eye = Ahead(2f, 1.6f);
                    Emit(GrowthFx.Star, eye, e.Kind == GrowthEventKind.StarUp ? 16 : 24, new Color(1f, 0.85f, 0.3f), new Vector3(0, 1f, 0), 2f, 0.15f, 1.8f, 0.3f);
                    if (e.Kind != GrowthEventKind.StarUp) Confetti(eye + Vector3.up * 0.6f, 60);
                    break;
                case GrowthEventKind.Opened:
                    var where = Ahead(2.2f, 1.2f);
                    Emit(GrowthFx.Puff, where, 14, new Color(0.95f, 0.9f, 0.82f, 0.8f), new Vector3(0, 0.4f, 0), 1f, 0.35f, 1.6f, 0.6f);
                    Confetti(where + Vector3.up, 40);
                    break;
                case GrowthEventKind.Bred:
                    Emit(GrowthFx.Heart, GrowthWorld.BreedingPosition + new Vector3(0, 1.3f, 0), 6, new Color(1f, 0.55f, 0.7f), new Vector3(0, 0.5f, 0), 0.4f, 0.1f, 1.6f, 0.2f);
                    break;
                case GrowthEventKind.Upgraded: case GrowthEventKind.Bought: case GrowthEventKind.Decorated:
                    if (session.LastStation.HasValue) Emit(GrowthFx.Sparkle, session.LastStation.Value + Vector3.up * 1.1f, 16, new Color(1f, 0.95f, 0.7f), new Vector3(0, 0.8f, 0), 1.2f, 0.12f, 1f, 0.4f);
                    break;
            }
        }

        static readonly Dictionary<string, Color> cultureColours = new Dictionary<string, Color>();

        /// <summary>A culture's colour (its content's).</summary>
        static Color CultureColour(string culture)
        {
            if (!cultureColours.TryGetValue(culture, out var c))
            {
                ColorUtility.TryParseHtmlString(GrowthContent.Cultures[GrowthContent.CultureIndex(culture)].Hex, out c);
                cultureColours[culture] = c;
            }
            return c;
        }

        /// <summary>A point in front of the player, at a height.</summary>
        Vector3 Ahead(float distance, float height)
        {
            var p = session.Player != null ? session.Player.transform : null;
            if (p == null) return new Vector3(0, height, 0);
            var f = p.forward; f.y = 0; f = f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
            return p.position + f * distance + Vector3.up * height;
        }

        static readonly Color[] ConfettiColours = { new Color(1f, 0.48f, 0.48f), new Color(1f, 0.79f, 0.24f), new Color(0.13f, 0.75f, 0.69f), new Color(0.66f, 0.55f, 1f), new Color(1f, 0.62f, 0.77f), new Color(0.61f, 0.86f, 0.3f) };

        void Confetti(Vector3 at, int count)
        {
            for (int i = 0; i < ConfettiColours.Length; i++)
                Emit(GrowthFx.Confetti, at, Mathf.Max(1, count / ConfettiColours.Length), ConfettiColours[i], new Vector3(0, 1.2f, 0), 2.2f, 0.07f, 2.6f, 0.4f);
        }

        // ---------------------------------------------------------------- each frame

        void LateUpdate()
        {
            if (!On || session.Shop == null || session.World == null) return;
            var shop = session.Shop; var w = session.World;
            if (shop != seenShop) { seenShop = shop; tankCounts.Clear(); schools.Clear(); visitorsSeen.Clear(); visitorsLeft.Clear(); offspringSeen.Clear(); foreach (var o in shop.AllOffspring) offspringSeen.Add(o.Id); foreach (var v in shop.Visitors) visitorsSeen.Add(v.Id); }
            if (session.Paused) return;
            float dt = Time.deltaTime;
            var cam = session.Player != null && session.Player.View != null ? session.Player.View.transform.position : Vector3.zero;
            bool near(Vector3 p, float d) => (p - cam).sqrMagnitude < d * d;

            // The growers: bubbles rising in the culture, more as it fills; drawn up toward the siphon while collecting.
            foreach (var culture in shop.Cultures)
            {
                if (!w.GrowerLiquid(culture, out var bottom, out float height, out float radius) || !near(bottom, 12)) continue;
                var colour = CultureColour(culture);
                float fill = shop.GrowerCapacity > 0 && shop.GrowerUnits.TryGetValue(culture, out var units) ? Mathf.Clamp01(units / shop.GrowerCapacity) : 0;
                int n = Due("grower/" + culture, fill > 0.02f ? 4f + 6f * fill : 0, dt);
                for (int i = 0; i < n; i++)
                {
                    var a = Rand() * Mathf.PI * 2; float r = radius * 0.65f * Mathf.Sqrt(Rand());
                    Emit(GrowthFx.Bubble, bottom + new Vector3(Mathf.Cos(a) * r, height + 0.005f, Mathf.Sin(a) * r), 1, Color.Lerp(colour, Color.white, 0.6f), new Vector3(0, 0.14f, 0), 0.015f, 0.048f, 0.75f, 0, false);
                }
                if (session.CollectingNow && session.Focused != null && session.Focused.Kind == GrowthTargetKind.Grower && session.Focused.Id == culture)
                {
                    int k = Due("draw/" + culture, 30, dt);
                    var top = bottom + Vector3.up * height;
                    var toward = (cam + Vector3.down * 0.35f - top);
                    for (int i = 0; i < k; i++)
                        Emit(GrowthFx.Glow, top + Random3() * radius * 0.6f, 1, new Color(colour.r, colour.g, colour.b, 0.8f), toward.normalized * 1.6f + Vector3.up * 0.3f, 0.2f, 0.06f, Mathf.Min(0.6f, toward.magnitude / 1.6f), 0, false);
                }
            }

            // The incubator: steam from its top and soft glints while it incubates.
            var incubator = GrowthWorld.IncubatorPosition;
            if (shop.Incubating && near(incubator, 14))
            {
                int s = Due("incubator/steam", 4f, dt);
                for (int i = 0; i < s; i++) Emit(GrowthFx.Steam, incubator + new Vector3(0, 1.3f, 0), 1, new Color(1f, 1f, 1f, 0.6f), new Vector3(0, 0.3f, 0), 0.08f, 0.17f, 2f, 0.1f, false);
                int g = Due("incubator/glint", 4f, dt);
                for (int i = 0; i < g; i++) Emit(GrowthFx.Sparkle, incubator + new Vector3(0, 1.0f, 0), 1, new Color(0.6f, 1f, 0.9f), new Vector3(0, 0.25f, 0), 0.15f, 0.07f, 0.9f, 0.35f, false);
            }

            // The breeding tank: hearts over a pair while it breeds; a pop of sparkles when an offspring arrives.
            if (shop.ActivePairs.Any(p => p.Breeding) && near(GrowthWorld.BreedingPosition, 12))
            {
                int h = Due("breeding", 0.8f, dt);
                for (int i = 0; i < h; i++) Emit(GrowthFx.Heart, GrowthWorld.BreedingPosition + new Vector3(0, 1.25f, 0), 1, new Color(1f, 0.6f, 0.75f), new Vector3(0, 0.35f, 0), 0.12f, 0.09f, 1.8f, 0.25f, false);
            }
            foreach (var o in shop.AllOffspring)
                if (offspringSeen.Add(o.Id))
                    Emit(GrowthFx.Sparkle, GrowthWorld.BreedingPosition + new Vector3(0, 1.1f, 0), 16, new Color(1f, 0.85f, 0.95f), new Vector3(0, 0.7f, 0), 0.9f, 0.1f, 1f, 0.2f);

            // The crank's sparks and the hand pump's drips, in time with them.
            if (session.CrankingNow && Due("crank", 20, dt) is int sparks && sparks > 0)
                Emit(GrowthFx.Spark, w.GeneratorPlace + new Vector3(0.12f, 0.95f, -0.24f), sparks, new Color(1f, 0.82f, 0.38f), new Vector3(0, 1.3f, 0), 1.3f, 0.055f, 0.5f, 0.06f, false);
            if (session.PumpingNow && session.Focused != null && GrowthWorld.GrowerPositions.ContainsKey(session.Focused.Id) && Due("pump", 6, dt) is int drips && drips > 0)
            {
                var spout = GrowthWorld.GrowerPositions[session.Focused.Id] + Quaternion.Euler(0, GrowthWorld.GrowerYaw(session.Focused.Id), 0) * new Vector3(0, 0.75f, 0.45f);
                Emit(GrowthFx.Drop, spout, drips, Color.Lerp(CultureColour(session.Focused.Id), Color.white, 0.3f), new Vector3(0, 0.3f, 0), 0.5f, 0.04f, 0.5f, 0.02f, false);
            }

            // The generator while it makes power (a turbine; the crank has its sparks): glowing bubbles rising up each glass
            // column, a soft blue glint at its lamp now and then, and a fine mist off the tide engine's drum as it turns.
            if (!shop.HandCrank && shop.SupplyNow > 0 && near(w.GeneratorLamp, 12))
            {
                int column = 0;
                foreach (var foot in w.GeneratorColumnFeet())
                {
                    int n = Due("generator/" + column++, 5f, dt);
                    for (int i = 0; i < n; i++)
                    {
                        var a = Rand() * Mathf.PI * 2; float r = 0.13f * Mathf.Sqrt(Rand());
                        Emit(GrowthFx.Bubble, foot + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), 1, new Color(0.8f, 0.95f, 1f, 0.9f), new Vector3(0, 0.5f, 0), 0.03f, 0.045f, 1.6f, 0, false);
                    }
                }
                if (Due("generator/glint", 1.2f, dt) > 0)
                    Emit(GrowthFx.Spark, w.GeneratorLamp, 3, new Color(0.6f, 0.88f, 1f), new Vector3(0, 0.35f, 0), 0.6f, 0.04f, 0.35f, 0.03f, false);
                if (w.GeneratorDrum(out var drum))
                {
                    int m = Due("generator/mist", 3f, dt);
                    for (int i = 0; i < m; i++)
                        Emit(GrowthFx.Steam, drum + new Vector3((Rand() - 0.5f) * 0.9f, 0, 0), 1, new Color(0.85f, 0.95f, 1f, 0.35f), new Vector3(0, 0.18f, 0), 0.06f, 0.12f, 1.4f, 0.05f, false);
                }
            }

            // The electric pumps while they run: a small green glint at each motor now and then.
            if (shop.ElectricPumps)
                foreach (var (culture, motor) in w.PumpMotors())
                {
                    var line = shop.Lines.FirstOrDefault(p => p.Culture == culture);
                    var use = line == null ? null : shop.PowerUse(line.PowerId);
                    if (use == null || use.Got <= 0 || !near(motor, 8)) continue;
                    if (Due("pump/" + culture, 0.8f, dt) > 0)
                        Emit(GrowthFx.Spark, motor, 2, new Color(0.65f, 1f, 0.6f), new Vector3(0, 0.25f, 0), 0.4f, 0.03f, 0.3f, 0.02f, false);
                }

            // The tanks: bubbles from the filter and motes in the water; a splash and a ring when a creature goes in; a sparkle
            // when a school forms.
            foreach (var kv in w.Tanks)
            {
                var t = kv.Value; var c = shop.Container(kv.Key);
                if (c == null || t.Root == null) continue;
                int count = c.Occupants.Count;
                var surface = t.Root.TransformPoint(new Vector3(0, t.WaterY + t.Inner.y * 0.5f - 0.02f, 0));
                bool known = tankCounts.TryGetValue(kv.Key, out int was);
                if (known && count > was)
                {
                    Emit(GrowthFx.Ripple, surface, 1, new Color(1, 1, 1, 0.9f), Vector3.zero, 0, Mathf.Min(t.Width, t.Depth) * 0.25f, 0.9f, 0, false);
                    Emit(GrowthFx.Drop, surface + Vector3.up * 0.03f, 8, new Color(0.75f, 0.95f, 1f, 0.9f), new Vector3(0, 1.1f, 0), 0.7f, 0.035f, 0.55f, 0.05f);
                }
                tankCounts[kv.Key] = count;
                if (!known || count != was)
                {
                    // Looked at when its count changes; a school that broke up (sold, moved, the tank emptied) sparkles again
                    // when it forms again.
                    var inSchool = new HashSet<string>(GrowthMoments.SchoolsIn(c).Select(sp => kv.Key + "/" + sp));
                    schools.RemoveWhere(k => k.StartsWith(kv.Key + "/", System.StringComparison.Ordinal) && !inSchool.Contains(k));
                    foreach (var key in inSchool)
                        if (schools.Add(key) && known)  // not for the schools there when the shop is first seen
                            Emit(GrowthFx.Sparkle, t.Root.TransformPoint(new Vector3(0, t.WaterY, 0)), 14, new Color(0.75f, 1f, 1f), new Vector3(0, 0.3f, 0), 0.6f, 0.08f, 1f, Mathf.Min(t.Width, t.Depth) * 0.3f);
                }
                if (!near(t.Root.position, 9)) continue;
                var bed = t.Root.TransformPoint(new Vector3(t.Inner.x * 0.32f, t.WaterY - t.Inner.y * 0.5f + 0.05f, -t.Inner.z * 0.25f));
                int b = Due("tank/" + kv.Key, 3f + t.Inner.x * 2f, dt);
                float rise = t.Inner.y * 0.9f;
                for (int i = 0; i < b; i++)
                    Emit(GrowthFx.Bubble, bed + new Vector3((Rand() - 0.5f) * 0.05f, 0, (Rand() - 0.5f) * 0.05f), 1, new Color(1, 1, 1, 0.85f), new Vector3(0, 0.3f, 0), 0.03f, 0.04f, rise / 0.3f, 0, false);
                if (Ambient && count > 0)
                {
                    int m = Due("motes/" + kv.Key, 1.2f, dt);
                    for (int i = 0; i < m; i++)
                        Emit(GrowthFx.Mote, t.Root.TransformPoint(new Vector3((Rand() - 0.5f) * t.Inner.x * 0.8f, t.WaterY + (Rand() - 0.5f) * t.Inner.y * 0.7f, (Rand() - 0.5f) * t.Inner.z * 0.7f)), 1,
                            new Color(0.8f, 1f, 1f, 0.5f), new Vector3(0, 0.03f, 0), 0.03f, 0.018f, 3f, 0, false);
                }
            }
            foreach (var id in tankCounts.Keys.Where(k => !w.Tanks.ContainsKey(k)).ToList()) tankCounts.Remove(id);

            // Visitors: a puff at the door as each comes in; as they go, sparkles over the happy and a grey cloud over the unhappy.
            var door = GrowthWorld.DoorPoint;
            foreach (var v in shop.Visitors)
            {
                if (visitorsSeen.Add(v.Id)) Emit(GrowthFx.Puff, door + new Vector3(0, 0.1f, 0), 5, new Color(0.95f, 0.9f, 0.8f, 0.55f), new Vector3(0, 0.25f, 0), 0.5f, 0.2f, 0.9f, 0.2f);
                if (v.State != GrowthVisitorState.Leaving || !visitorsLeft.Add(v.Id) || !w.VisitorViews.TryGetValue(v.Id, out var view) || view == null) continue;
                var head = view.transform.position + Vector3.up * 2.0f;
                if (v.Outcome == GrowthVisitOutcome.Bought)
                    Emit(GrowthFx.Sparkle, head, v.WishGranted ? 14 : 8, new Color(1f, 0.92f, 0.55f), new Vector3(0, 0.5f, 0), 0.5f, 0.09f, 0.9f, 0.2f);
                else if (v.Outcome == GrowthVisitOutcome.TiredOfWaiting || v.Outcome == GrowthVisitOutcome.NothingAffordable || v.Outcome == GrowthVisitOutcome.Slapped)
                    Emit(GrowthFx.Cloud, head + Vector3.up * 0.15f, 5, new Color(0.55f, 0.58f, 0.66f, 0.8f), new Vector3(0, 0.15f, 0), 0.12f, 0.22f, 1.6f, 0.12f);
            }

            // The rooms: dust drifting in the sunlight by the shop's window and door by day.
            if (Ambient && shop.Phase == GrowthPhase.Day)
                for (int k = 0; k < GrowthWorld.Sunbeams.Length; k++)
                {
                    var beam = GrowthWorld.Sunbeams[k];
                    if (!near(beam, 10) || k == 1 && !shop.UnitB) continue;
                    int d = Due("dust/" + beam, 3f, dt);
                    for (int i = 0; i < d; i++)
                        Emit(GrowthFx.Dust, beam + new Vector3((Rand() - 0.5f) * 1.6f, Rand() * 1.8f, (Rand() - 0.5f) * 1.2f), 1, new Color(1f, 0.95f, 0.8f, 0.55f), new Vector3(0.02f, 0.01f, 0), 0.02f, 0.02f, 4f, 0, false);
                }

            // At night, when the work happens: warm motes drifting in the light under each lit lamp.
            if (Ambient && shop.Phase == GrowthPhase.Night)
            {
                int index = 0;
                foreach (var lamp in w.LitLamps())
                {
                    int k = index++;
                    if (k >= LampKeys.Length || !near(lamp, 9)) continue;
                    int d = Due(LampKeys[k], 1.5f, dt);
                    for (int i = 0; i < d; i++)
                        Emit(GrowthFx.Dust, lamp + new Vector3((Rand() - 0.5f) * 0.9f, -0.15f - Rand() * 0.9f, (Rand() - 0.5f) * 0.9f), 1, new Color(1f, 0.86f, 0.58f, 0.75f), new Vector3(0, -0.01f, 0), 0.03f, 0.03f, 3.5f, 0, false);
                }
            }
        }

        /// <summary>The lamps' stream keys (made once, so a frame makes no strings for them).</summary>
        static readonly string[] LampKeys = Enumerable.Range(0, 12).Select(i => "lamp/" + i).ToArray();

        // ---------------------------------------------------------------- the particle systems

        void Make(GrowthFx fx, Material material, float gravity, float noise, float grow, float fadeIn, float spin, bool twinkle = false, bool flat = false, bool stretch = false)
        {
            var go = new GameObject("Effects / " + fx);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500; main.gravityModifier = gravity; main.startSpeed = 0; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, Mathf.Clamp(fadeIn, 0.01f, 0.5f)), new GradientAlphaKey(1, twinkle ? 0.5f : 0.7f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, twinkle ? new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(0.6f, 0.7f), new Keyframe(1, 0.2f))
                                                               : AnimationCurve.Linear(0, 1, 1, grow));
            if (spin != 0) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(spin * Mathf.Deg2Rad * -0.5f, spin * Mathf.Deg2Rad); }
            if (noise > 0) { var n = ps.noise; n.enabled = true; n.strength = noise; n.frequency = 0.6f; n.scrollSpeed = 0.3f; n.quality = ParticleSystemNoiseQuality.Low; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = flat ? ParticleSystemRenderMode.HorizontalBillboard : stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch) { r.velocityScale = 0.06f; r.lengthScale = 1.5f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.minParticleSize = 0; r.maxParticleSize = 2;
            ps.Play();
            systems[fx] = ps;
        }

        // ---------------------------------------------------------------- the textures (drawn here)

        delegate float Shape(float x, float y);

        /// <summary>Validation: each texture's shape (its alpha at a point from -1 to 1 across).</summary>
        public static IEnumerable<(string name, System.Func<float, float, float> alpha)> Shapes => new (string, System.Func<float, float, float>)[]
        {
            ("dot", Dot), ("ring", Ring), ("sparkle", SparkleShape), ("heart", HeartShape), ("coin", CoinShape), ("puff", PuffShape),
            ("star", StarShape), ("paper", PaperShape),
        };

        Texture2D Make(Texture2D t) { made.Add(t); return t; }

        static Texture2D Draw(int size, Shape shape, bool coin = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = shape.Method.Name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2 - 1, v = (y + 0.5f) / size * 2 - 1;
                    float a = Mathf.Clamp01(shape(u, v));
                    Color c = Color.white;
                    if (coin)
                    {
                        float r = Mathf.Sqrt(u * u + v * v);
                        c = r > 0.72f ? new Color(0.85f, 0.6f, 0.15f) : new Color(1f, 0.82f, 0.25f);
                        if (u * 0.6f + v * 0.8f > 0.25f && r < 0.62f) c = Color.Lerp(c, Color.white, 0.45f);
                        if (Mathf.Abs(u) < 0.08f && Mathf.Abs(v) < 0.38f && r < 0.6f) c = new Color(0.9f, 0.66f, 0.18f);
                    }
                    px[y * size + x] = new Color(c.r, c.g, c.b, a);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        static float Edge(float d, float soft) => Mathf.Clamp01((1f - d) / soft);
        static float Dot(float x, float y) { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Pow(Mathf.Clamp01(1 - r), 1.8f); }
        static float Ring(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float rim = Mathf.Clamp01(1 - Mathf.Abs(r - 0.82f) / 0.12f);
            float fill = r < 0.8f ? 0.12f : 0;
            float shine = Mathf.Clamp01(1 - ((x + 0.35f) * (x + 0.35f) + (y - 0.35f) * (y - 0.35f)) / 0.04f);
            return Mathf.Max(rim, Mathf.Max(fill, shine)) * Edge(r, 0.08f);
        }
        static float SparkleShape(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float cross = Mathf.Max(Mathf.Exp(-ax * ax * 180) * Mathf.Clamp01(1 - ay), Mathf.Exp(-ay * ay * 180) * Mathf.Clamp01(1 - ax));
            float core = Mathf.Exp(-(x * x + y * y) * 14);
            return Mathf.Max(cross, core);
        }
        static float HeartShape(float x, float y)
        {
            x *= 1.25f; y = y * 1.25f + 0.2f;
            float f = Mathf.Pow(x * x + y * y - 1, 3) - x * x * y * y * y;
            return Mathf.Clamp01(-f * 6f + 0.5f);
        }
        static float CoinShape(float x, float y) { float r = Mathf.Sqrt(x * x + y * y); return Edge(r / 0.86f, 0.06f); }
        static float PuffShape(float x, float y)
        {
            float a = 0;
            foreach (var (cx, cy, rr) in new[] { (0f, -0.1f, 0.62f), (-0.38f, -0.18f, 0.42f), (0.38f, -0.15f, 0.45f), (0.12f, 0.3f, 0.45f), (-0.2f, 0.22f, 0.4f) })
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rr;
                a = Mathf.Max(a, Mathf.Clamp01((1 - d) / 0.35f));
            }
            return a * 0.85f;
        }
        static float StarShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y), ang = Mathf.Atan2(y, x) + Mathf.PI / 2;
            float k = Mathf.Cos(5 * ang) * 0.5f + 0.5f;
            float edge = Mathf.Lerp(0.42f, 0.9f, Mathf.Pow(k, 3));
            return Mathf.Clamp01((edge - r) / 0.08f);
        }
        static float PaperShape(float x, float y) => Mathf.Clamp01((0.8f - Mathf.Abs(x)) / 0.1f) * Mathf.Clamp01((0.5f - Mathf.Abs(y)) / 0.1f);

        void OnDestroy()
        {
            foreach (var o in made) if (o) Destroy(o);
        }
    }
}
