using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-18 wave 4 (§6.7): the silly moments the user asked back for (GR.55): the cartoon slap, the suspicious fart, the
    // ridiculous complaint, the incubator's belch, the overenthusiastic helper and the fish that copies a visitor. The slap is
    // the player's own action and the only one with a rule: the visitor laughs it off or leaves in a huff (their reservation
    // released, a small reputation loss, capped each day), and it never pays. The others only show and sound: the rules never
    // see them. GrowthComedy is their shared timing: one at a time, a quiet time between them, each kind's own cooldown, fewer
    // and shorter with the Gags setting at Fewer, none but the player's slap at Off, and the captions they say.

    public enum GrowthGag { Slap, Fart, Complaint, Belch, Dance, Mimic }

    /// <summary>The Gags setting (§6.7's reduced-gags option): every silly moment, fewer and shorter ones, or none but the
    /// player's own slap (a deliberate action, never one of the room's).</summary>
    public enum GrowthGagLevel { Full, Fewer, Off }

    /// <summary>
    /// The silly moments' shared timing (§6.7: "Gags share cooldown/concurrency limits so the room is not constantly
    /// shouting"): one at a time; a quiet time after each; each kind's own cooldown; the room's own moments (the fart, the
    /// complaint, the fish) tried in turn every so often, an attempt that finds no one simply dropped. Its clock runs only
    /// while the game does (paused, nothing moves on). A slap, the player's own, always plays, cutting short any other.
    /// Transient: nothing here is saved, and a new shop starts it afresh.
    /// </summary>
    public sealed class GrowthComedy
    {
        /// <summary>How long a moment plays (2.6 s; 1.6 s at Fewer), a slap's own (2.2 s), and the quiet after each (8 s; 20 s at Fewer).</summary>
        public const float Length = 2.6f, FewerLength = 1.6f, SlapLength = 2.2f, Quiet = 8f, FewerQuiet = 20f;
        /// <summary>How often the room tries one of its own moments (every 18 s; every 45 s at Fewer).</summary>
        public const float AmbientEvery = 18f, FewerAmbientEvery = 45f;
        /// <summary>The room's own moments, tried in turn.</summary>
        public static readonly GrowthGag[] Ambient = { GrowthGag.Fart, GrowthGag.Complaint, GrowthGag.Mimic };

        /// <summary>Each kind's cooldown after it played (doubled at Fewer): the fart 35 s, the complaint 40 s, the belch 25 s,
        /// a helper's dance 15 s, the fish 30 s; a slap's own is the rules' (GrowthShop.SlapCooldown).</summary>
        public static float Cooldown(GrowthGag g)
        {
            switch (g)
            {
                case GrowthGag.Fart: return 35f;
                case GrowthGag.Complaint: return 40f;
                case GrowthGag.Belch: return 25f;
                case GrowthGag.Dance: return 15f;
                case GrowthGag.Mimic: return 30f;
                default: return 0f;
            }
        }

        public GrowthGagLevel Level = GrowthGagLevel.Full;
        /// <summary>The comedy's own clock (seconds the game has run since the shop started).</summary>
        public float Clock { get; private set; }
        /// <summary>The moment playing now, or null.</summary>
        public GrowthGag? Current { get; private set; }
        /// <summary>Whom it is about (a visitor's id, a helper's index, a hatch's number), as the presentation keyed it.</summary>
        public int Subject { get; private set; }
        /// <summary>Seconds into the moment playing now (-1 with none), and how long it plays.</summary>
        public float Age => Current == null ? -1 : Clock - started;
        public float Duration => Current == null ? 0 : ends - started;
        /// <summary>Validation: moments started, skipped and cut short by a slap, and the room's attempts that found no one.</summary>
        public int Started { get; private set; }
        public int Skipped { get; private set; }
        public int CutShort { get; private set; }
        public int Dropped { get; private set; }
        public readonly Dictionary<GrowthGag, int> Played = new Dictionary<GrowthGag, int>();

        float started, ends, quietUntil = -1, ambientClock;
        int ambientTurn;
        readonly Dictionary<GrowthGag, float> last = new Dictionary<GrowthGag, float>();
        readonly Dictionary<GrowthGag, int> variants = new Dictionary<GrowthGag, int>();

        public float LengthOf(GrowthGag g) => g == GrowthGag.Slap ? SlapLength : Level == GrowthGagLevel.Fewer ? FewerLength : Length;
        float QuietTime => Level == GrowthGagLevel.Fewer ? FewerQuiet : Quiet;
        float CooldownOf(GrowthGag g) => Cooldown(g) * (Level == GrowthGagLevel.Fewer ? 2f : 1f);

        /// <summary>Why a moment may not start now ("off", "busy", "quiet", "cooldown"), or null when it may.</summary>
        public string WhyNot(GrowthGag g)
        {
            if (g == GrowthGag.Slap) return null;
            if (Level == GrowthGagLevel.Off) return "off";
            if (Current != null) return "busy";
            if (Clock < quietUntil) return "quiet";
            if (last.TryGetValue(g, out float at) && Clock - at < CooldownOf(g)) return "cooldown";
            return null;
        }

        public bool CanStart(GrowthGag g) => WhyNot(g) == null;

        /// <summary>Starts a moment about a subject if it may; a slap cuts short whatever else is playing.</summary>
        public bool Start(GrowthGag g, int subject)
        {
            if (!CanStart(g)) return false;
            if (Current != null) CutShort++;
            Current = g; Subject = subject;
            started = Clock; ends = Clock + LengthOf(g);
            last[g] = Clock;
            Started++;
            Played[g] = Played.TryGetValue(g, out int n) ? n + 1 : 1;
            return true;
        }

        /// <summary>Moves the clock on; a moment that has played its length ends, and the quiet time starts.</summary>
        public void Tick(float seconds)
        {
            if (!(seconds > 0) || float.IsInfinity(seconds)) return;
            Clock += seconds;
            if (Current != null && Clock >= ends) Finish();
            if (Current == null) ambientClock += seconds;
        }

        /// <summary>Ends the moment now (Skip; its subject left; the day closed): the quiet time still follows.</summary>
        public void Cancel(bool skipped = false)
        {
            if (Current == null) return;
            if (skipped) Skipped++;
            Finish();
        }

        void Finish() { Current = null; quietUntil = Clock + QuietTime; }

        /// <summary>A new shop (a load, a fixture, Start over): nothing playing, nothing remembered.</summary>
        public void Clear()
        {
            Current = null; Subject = 0;
            quietUntil = -1; ambientClock = 0; ambientTurn = 0;
            last.Clear();
        }

        /// <summary>Whether it is time for the room to try one of its own moments (and the next try waits again); never at Off.</summary>
        public bool AmbientDue()
        {
            if (Level == GrowthGagLevel.Off || Current != null) return false;
            if (ambientClock < (Level == GrowthGagLevel.Fewer ? FewerAmbientEvery : AmbientEvery)) return false;
            ambientClock = 0;
            return true;
        }

        /// <summary>The room's next moment to try, in turn: the fart, the complaint, the fish.</summary>
        public GrowthGag NextAmbient() => Ambient[ambientTurn++ % Ambient.Length];

        /// <summary>An attempt that found no one to play it: dropped, never saved up for later.</summary>
        public void Drop() => Dropped++;

        /// <summary>Which of a kind's lines comes next (each in turn, so the same line never comes twice running).</summary>
        public int Variant(GrowthGag g, int count)
        {
            int n = variants.TryGetValue(g, out int v) ? v : 0;
            variants[g] = n + 1;
            return count <= 0 ? 0 : n % count;
        }

        // ---------------------------------------------------------------- what they say (§6.7: invented mild gibberish, no swearing)

        /// <summary>A slapped visitor's grumble on the way out.</summary>
        public static readonly string[] Grumbles = { "Hmph! Blorf-a-nurgle!", "Well I never! Fizzwhump!", "Snorkelgrump! The nerve!" };
        /// <summary>A casual visitor laughing off a first slap.</summary>
        public static readonly string[] Laughs = { "Ha! Good one!", "Hee hee! Do the fish next!", "Ow! Ha ha, fair enough." };
        /// <summary>A visitor blaming the fish for a smell ({0}: the creature's name).</summary>
        public static readonly string[] Blames = { "Was that... the {0}?", "Phew! Something in that tank is fishy.", "I think the {0} did it." };
        /// <summary>A ridiculous complaint about a creature ({0}: its name).</summary>
        public static readonly string[] Complaints = { "That {0} looks just like my accountant.", "Does the {0} have to stare at me like that?", "My {0} at home is much rounder." };
        /// <summary>The incubator, after a hatch.</summary>
        public static readonly string[] Belches = { "*buuurp*", "*hic* ...*burp*" };
        /// <summary>A visitor whose face the fish copies, and the fish ({0}: its name).</summary>
        public static readonly string[] Mimics = { "Is that {0} copying me?", "The {0} is doing my face!" };
        /// <summary>Each helper's little cheer after a job.</summary>
        public static string Cheer(string helper, int variant)
        {
            switch (helper)
            {
                case "pip": return variant % 2 == 0 ? "Ta-daa!" : "Another happy customer!";
                case "nori": return variant % 2 == 0 ? "Nailed it!" : "Special delivery!";
                default: return variant % 2 == 0 ? "Science!" : "Precisely as planned.";
            }
        }

        public static string Line(string[] lines, int variant, string name = null) =>
            string.Format(lines[((variant % lines.Length) + lines.Length) % lines.Length], name ?? "fish");
    }

    public sealed partial class GrowthShop
    {
        /// <summary>GR-18 wave 4 (§6.7): seconds before the player's hand can slap again, the reputation a huff costs, and how
        /// many huffs a day cost it (later ones the same, and free).</summary>
        public const float SlapCooldown = 3f;
        public const int SlapReputation = -1, SlapPenaltiesADay = 2;
        float slapClock;
        int slapPenaltiesToday, slapPenaltyDay;

        /// <summary>What a visitor of a type is called (a caption's speaker).</summary>
        public static string TypeName(string type) =>
            type == GuestType ? "Exhibition guest" : GrowthContent.Visitors.FirstOrDefault(t => t.Id == type)?.Name ?? "A visitor";

        /// <summary>Seconds before the player can slap again (0 when they can).</summary>
        public float SlapWait => slapClock;

        /// <summary>
        /// §6.7's cartoon slap, the player's deliberate action at a visitor in reach (never the service or interact button): a
        /// casual visitor laughs off a first slap and carries on; anyone else, or a second slap, leaves in a huff, their
        /// reservation released (Leave), at a small reputation cost capped each day. It never pays and never sells. Only during
        /// the day, never an exhibition guest (they are here on business), and once every few seconds.
        /// </summary>
        public GrowthResult Slap(int visitorId)
        {
            if (Phase != GrowthPhase.Day) return GrowthResult.Fail(GrowthFailure.NotDuringDay, "Visitors come by day");
            var v = Visitors.FirstOrDefault(x => x.Id == visitorId);
            if (v == null || v.State == GrowthVisitorState.Leaving) return GrowthResult.Fail(GrowthFailure.NoSuchVisitor, "They are on their way out");
            if (v.Type == GuestType) return GrowthResult.Fail(GrowthFailure.Locked, "Exhibition guests are here on important business");
            if (slapClock > 0) return GrowthResult.Fail(GrowthFailure.Busy, "Your hand is still stinging");
            slapClock = SlapCooldown;
            v.Slaps++;
            if (v.Type == "casual" && v.Slaps == 1)
            {
                if (Today != null) Today.LaughedOff++;
                Raise(GrowthEventKind.Slapped, "A casual visitor laughed it off");
                return GrowthResult.Ok("laughed");
            }
            if (PlayerAtTill && PlayerVisitor == v) ServeProgress = 0;
            Leave(v, GrowthVisitOutcome.Slapped);
            return GrowthResult.Ok("left");
        }

        /// <summary>A slapped visitor leaves in a huff: a reputation point for each of the day's first huffs (never below zero).</summary>
        void SlapLeave(GrowthVisitor v)
        {
            if (slapPenaltyDay != Day) { slapPenaltyDay = Day; slapPenaltiesToday = 0; }
            int before = Reputation;
            if (slapPenaltiesToday < SlapPenaltiesADay)
            {
                slapPenaltiesToday++;
                Reputation = Math.Max(0, Reputation + SlapReputation);
            }
            if (Today != null) Today.Slapped++;
            Raise(GrowthEventKind.Slapped, Reputation < before ? "A slapped visitor left in a huff (" + (Reputation - before) + " reputation)" : "A slapped visitor left in a huff");
        }

        void TickSlap(float seconds) { if (slapClock > 0) slapClock = Math.Max(0, slapClock - seconds); }
    }
}
