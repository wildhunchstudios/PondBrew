using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-18 wave 4 (§6.7): the silly moments the user asked back for (GR.55), played by the people of waves 1 to 3:
    /// <list type="bullet">
    /// <item>the cartoon slap: the player's own deliberate action, on its own key (F unless changed in the pause menu, never the
    /// service or interact button), at a visitor under the reticle within reach, with a clear prompt; the rules decide what
    /// follows (GrowthShop.Slap: a casual visitor laughs off a first slap, anyone else leaves in a huff); the right hand
    /// swings, the visitor's head is knocked aside with eyes squeezed shut, a hand mark and dizzy stars, a thwack, a boing
    /// and a grumbled gibberish;</item>
    /// <item>the suspicious fart: a squeak by a visitor standing at a tank or in the queue, their innocent look and a puff of a
    /// cloud, and a moment later a neighbour sniffs and blames the fish;</item>
    /// <item>the ridiculous complaint: a visitor at a tank jabs a finger at a creature and says it looks like their accountant;</item>
    /// <item>the incubator's burp: after a hatch, once the hatchling shows, a squash and a bubble from the top;</item>
    /// <item>the overenthusiastic helper: a tiny victory dance with squeaky shoes after a job, only while they have nothing to do;</item>
    /// <item>the fish that copies a visitor: a visitor makes a fish face at the glass, and a creature in the tank puffs its
    /// cheeks back with a blub and a burst of its bubbles.</item>
    /// </list>
    /// One at a time, with quiet in between and each kind's own cooldown (GrowthComedy, Core); the room's own moments are tried
    /// every so often (fewer and shorter with the Gags setting at Fewer, none at Off but the slap). Each says a caption, and
    /// Backspace skips one (a slap's outcome stays: it is the rules'). Its own voice, at the Humour setting (full, half or off);
    /// sounds made here (no recording, nothing bought). Nothing here pays, sells, moves stock or a visitor's route: the room's
    /// moments only show; a subject that leaves, closing time, a pause (everything waits), a new shop or a reload end or hold
    /// them. The session steps it each frame after the world has followed the shop and before the people move.
    /// </summary>
    public sealed class GrowthGags : MonoBehaviour
    {
        /// <summary>How far a slap reaches (metres from the eye), how near the reticle must pass a visitor's middle, how far from
        /// the player the room's moments may be (so they are seen), and the neighbour who smells a fart.</summary>
        public const float SlapReach = 2.1f, SlapAim = 0.34f, NearPlayer = 7f, Neighbour = 4f;
        /// <summary>GR-20: how far away the reticle reads a visitor's wish.</summary>
        public const float WishReach = 9f;
        /// <summary>The burp waits for the hatchling to show (1.2 s) and is dropped if it cannot play within 8 s; a helper's dance
        /// waits for them to have nothing to do for 0.4 s.</summary>
        public const float BelchDelay = 1.2f, BelchExpiry = 8f, DanceAfter = 0.4f;
        /// <summary>GR-32: one hatch in this many ends in a burp.</summary>
        public const int BelchEvery = 8;
        /// <summary>Hatches since the last burp (a route sets it to hear one).</summary>
        public int HatchesSinceBurp;
        public const float SampleRate = GrowthAudio.SampleRate;
        public const Key DefaultSlapKey = Key.F;
        /// <summary>Keys that already do something (moving, E, Esc, Tab, Backspace's skip, F8): never the slap's.</summary>
        public static readonly Key[] Reserved = { Key.W, Key.A, Key.S, Key.D, Key.E, Key.Escape, Key.Tab, Key.Backspace, Key.F8, Key.C, Key.LeftShift };

        GrowthSession session;
        public readonly GrowthComedy Policy = new GrowthComedy();
        AudioSource voice;
        public readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();

        // What is playing: about whom, where, and which of its beats have sounded.
        GrowthVisitorView subject, other;
        string subjectReaction, otherReaction, helper, fishName, speaker;
        GrowthCreatureView fish;
        Vector3 about;
        int line;
        readonly HashSet<string> beats = new HashSet<string>();
        bool staggered;
        // A burp waiting for its hatch; the helpers' steps last frame and when each last finished one.
        bool belchWaiting; float belchAt;
        readonly Dictionary<string, string> lastStep = new Dictionary<string, string>();
        readonly Dictionary<string, float> finishedAt = new Dictionary<string, float>();
        GrowthShop seenShop;

        /// <summary>The visitor a slap would land on now (under the reticle, within reach, in the day), or null.</summary>
        public GrowthVisitorView SlapTarget { get; private set; }
        /// <summary>GR-20: the visitor under the reticle, within WishReach (their wish shows under it), or null.</summary>
        public GrowthVisitorView LookedAt { get; private set; }
        /// <summary>When the last slap landed, on the comedy's clock (-1 none): the hands swing then.</summary>
        public float LastSlap { get; private set; } = -1;
        /// <summary>The caption showing (who says it and what), or null.</summary>
        public string CaptionWho { get; private set; }
        public string CaptionText { get; private set; }
        /// <summary>Validation: the sounds asked for by name (requests, gain zero included), the last gain, where the last played.</summary>
        public readonly Dictionary<string, int> Asked = new Dictionary<string, int>();
        public float LastGain { get; private set; }
        public Vector3 LastSoundAt { get; private set; }
        /// <summary>Validation: whom the moment playing is about.</summary>
        public GrowthVisitorView Subject => subject;
        public GrowthVisitorView Other => other;
        public GrowthCreatureView Fish => fish;
        public string Helper => helper;
        /// <summary>The room's own moments (the fart, the complaint, the fish, the burp, the dance) may play: always in play;
        /// in a validation run only when its route says so (labelled), so no route's checks meet a moment it did not ask for.
        /// The player's slap is never held back by it.</summary>
        public bool RoomMoments = true;
        /// <summary>Waiting for a new slap key (the pause menu's chip).</summary>
        /// <summary>Whether the slap waits for its new key or button, and what the last rebinding said (GR-23: the session's).</summary>
        public bool Rebinding => session != null && session.Rebinding == GrowthAction.Slap;
        public string RebindProblem => session != null ? session.RebindNote : null;

        public void Initialize(GrowthSession owner)
        {
            session = owner;
            RoomMoments = !owner.ValidationMode;
            Sounds["slap"] = Clip("cartoon slap", MakeSlap());
            Sounds["boop"] = Clip("boop squeak", MakeBoop());
            Sounds["boing"] = Clip("dizzy boing", MakeBoing());
            Sounds["grumble"] = Clip("grumbled gibberish", MakeGibberish(7, 150f, 0.85f, 21));
            Sounds["laugh"] = Clip("ha ha ha", MakeLaugh());
            Sounds["squeak"] = Clip("suspicious squeak", MakeSqueak(0));
            Sounds["squeak2"] = Clip("suspicious squeak, the other", MakeSqueak(1));
            Sounds["mumble"] = Clip("complaining gibberish", MakeGibberish(9, 210f, 1.15f, 5));
            Sounds["mumble2"] = Clip("complaining gibberish, the other", MakeGibberish(8, 190f, 1.25f, 11));
            Sounds["burp"] = Clip("incubator burp", MakeBurp());
            Sounds["blub"] = Clip("fish blub honk", MakeBlub());
            Sounds["shoe"] = Clip("squeaky shoe", MakeShoe());
            var go = new GameObject("Sound / comedy"); go.transform.SetParent(transform, false);
            voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0.6f; voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 3f; voice.maxDistance = 20f; voice.dopplerLevel = 0; voice.priority = 140;
        }

        static AudioClip Clip(string label, float[] data)
        {
            var clip = AudioClip.Create(label, data.Length, 1, (int)SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy() { foreach (var c in Sounds.Values) if (c) Destroy(c); }

        // ---------------------------------------------------------------- each frame

        /// <summary>One frame: the clock (held while paused), the slap's target and key, Skip, what is playing, and what may start.</summary>
        public void Step(float dt)
        {
            if (session == null || session.Shop == null || session.World == null) return;
            var shop = session.Shop;
            if (shop != seenShop) { Clear(); seenShop = shop; }
            Policy.Level = RoomMoments ? session.GagLevel : GrowthGagLevel.Off;
            if (session.Paused)
            {
                if (voice.isPlaying) voice.Pause();
                SlapTarget = null; LookedAt = null;
                return;
            }
            voice.UnPause();
            Policy.Tick(dt);
            bool free = session.Card == GrowthCard.None;
            SlapTarget = free ? FindSlapTarget() : null;
            LookedAt = free ? FindVisitor(WishReach) : null;
            bool input = !session.ValidationMode || session.AllowValidationInput;
            // GR-23: the slap and the skip on the keys (or buttons) the player chose, F and Backspace unless changed.
            if (free && input && session.Rebinding == null)
            {
                if (GrowthControls.Pressed(GrowthAction.Slap) && SlapTarget != null) TrySlap(SlapTarget);
                else if (GrowthControls.Pressed(GrowthAction.Boop) && SlapTarget != null) TryBoop(SlapTarget);
                if (GrowthControls.Pressed(GrowthAction.Skip)) Skip();
            }
            Play();
            WatchHatch();
            WatchHelpers(shop);
            if (Policy.Current == null && Policy.AmbientDue())
            {
                var gag = Policy.NextAmbient();
                if (!free || shop.Phase != GrowthPhase.Day || session.PipeFrom != null || !TryAmbient(gag)) Policy.Drop();
            }
        }

        /// <summary>A new shop (a load, a fixture, Start over): everything stops and is forgotten.</summary>
        public void Clear()
        {
            EndShow();
            Policy.Clear();
            belchWaiting = false;
            lastStep.Clear(); finishedAt.Clear();
            LastSlap = -1;
        }

        /// <summary>Backspace: the moment playing stops (a slap's outcome is the rules' and stays).</summary>
        public void Skip()
        {
            if (Policy.Current == null) return;
            Policy.Cancel(true);
            EndShow();
        }

        // ---------------------------------------------------------------- the slap

        /// <summary>The visitor nearest the eye whose middle (from the hips to the top of the head) the reticle passes within
        /// SlapAim of, within reach, not behind anything solid; never an exhibition guest or someone leaving, never at night.</summary>
        GrowthVisitorView FindSlapTarget() => FindVisitor(SlapReach);

        /// <summary>The visitor nearest the eye under the reticle within a reach (as a slap aims); see FindSlapTarget.</summary>
        GrowthVisitorView FindVisitor(float reach)
        {
            var shop = session.Shop;
            if (shop.Phase != GrowthPhase.Day || session.PipeFrom != null || session.Player == null) return null;
            var eye = session.Player.View.transform;
            Vector3 o = eye.position, d = eye.forward;
            GrowthVisitorView best = null; float bestT = float.MaxValue;
            foreach (var view in session.World.VisitorViews.Values)
            {
                if (view == null) continue;
                var v = shop.Visitors.FirstOrDefault(x => x.Id == view.VisitorId);
                if (v == null || v.State == GrowthVisitorState.Leaving || v.Type == GrowthShop.GuestType) continue;
                float t = RayToSegment(o, d, view.transform.position + Vector3.up * 0.85f, view.transform.position + Vector3.up * 1.75f, out float miss);
                if (t <= 0.2f || t > reach || miss > SlapAim || t >= bestT) continue;
                if (Physics.Raycast(o, d, out var hit, t - 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                best = view; bestT = t;
            }
            return best;
        }

        /// <summary>How far along a ray it comes closest to a segment, and how close (the miss).</summary>
        static float RayToSegment(Vector3 o, Vector3 d, Vector3 a, Vector3 b, out float miss)
        {
            Vector3 u = d.normalized, v = b - a, w = o - a;
            float A = 1, B = Vector3.Dot(u, v), C = Vector3.Dot(v, v), D = Vector3.Dot(u, w), E = Vector3.Dot(v, w);
            float den = A * C - B * B;
            float s = den < 1e-6f ? 0 : Mathf.Clamp((A * E - B * D) / den, 0, 1);
            float t = Mathf.Max(0, B * s - D);
            miss = Vector3.Distance(o + u * t, a + v * s);
            return t;
        }

        /// <summary>A slap at a visitor: the rules decide (a laugh or a huff); on a slap that lands, the hand swings and the
        /// visitor reacts; a refusal (the hand still stinging, a guest) is a hint, never a strike.</summary>
        public GrowthResult TrySlap(GrowthVisitorView target)
        {
            var r = session.Shop.Slap(target.VisitorId);
            if (!r.Success) { session.Ui?.Hint(GrowthSession.Explain(r)); return r; }
            session.SaveNow();
            EndShow();
            Policy.Start(GrowthGag.Slap, target.VisitorId);
            LastSlap = Policy.Clock;
            session.Hands?.Slap();
            subject = target;
            subjectReaction = r.Detail == "laughed" ? "laugh" : "slapped";
            about = session.Player.View.transform.position;
            speaker = GrowthShop.TypeName(session.Shop.Visitors.FirstOrDefault(x => x.Id == target.VisitorId)?.Type);
            line = Policy.Variant(GrowthGag.Slap, 3);
            staggered = false;
            Sound("slap", target.transform.position + Vector3.up * 1.5f, 1f);
            return r;
        }

        /// <summary>GR-31 (F31): a boop on the nose: the visitor giggles, with a boop's squeak. The hand is the slap's, gentler in spirit.</summary>
        public GrowthResult TryBoop(GrowthVisitorView target)
        {
            var r = session.Shop.Boop(target.VisitorId);
            if (!r.Success) { session.Ui?.Hint(GrowthSession.Explain(r)); return r; }
            EndShow();
            Policy.Start(GrowthGag.Boop, target.VisitorId);
            session.Hands?.Slap();
            subject = target;
            subjectReaction = "laugh";
            about = session.Player.View.transform.position;
            speaker = GrowthShop.TypeName(session.Shop.Visitors.FirstOrDefault(x => x.Id == target.VisitorId)?.Type);
            line = Policy.Variant(GrowthGag.Boop, GrowthComedy.Giggles.Length);
            staggered = false;
            Boops++;
            Sound("boop", target.transform.position + Vector3.up * 1.5f, 1f);
            return r;
        }

        /// <summary>Validation: boops so far.</summary>
        public int Boops { get; private set; }

        // ---------------------------------------------------------------- the room's moments

        /// <summary>Validation: tries one of the room's moments now (as the room's own turn would), if it may start and someone is there.</summary>
        public bool Try(GrowthGag gag) => session != null && session.Shop.Phase == GrowthPhase.Day && session.Card == GrowthCard.None && TryAmbient(gag);

        /// <summary>Starts the room's moment if someone is there to play it (near the player, standing at a tank or in the queue).</summary>
        bool TryAmbient(GrowthGag gag)
        {
            if (!Policy.CanStart(gag)) return false;
            var shop = session.Shop;
            var me = session.Player.transform.position;
            var standing = session.World.VisitorViews.Values.Where(view => view != null && view.Person != null && view.Arrived && Vector3.Distance(view.transform.position, me) < NearPlayer)
                .Select(view => (view, v: shop.Visitors.FirstOrDefault(x => x.Id == view.VisitorId)))
                .Where(p => p.v != null && p.v.Type != GrowthShop.GuestType && (p.v.State == GrowthVisitorState.Browsing || p.v.State == GrowthVisitorState.Queued) && p.view.Reaction == null)
                .OrderBy(p => Vector3.Distance(p.view.transform.position, me)).ThenBy(p => p.v.Id).ToList();
            switch (gag)
            {
                case GrowthGag.Fart:
                    {
                        if (standing.Count == 0) return false;
                        var culprit = standing[0];
                        var tank = NearestTank(culprit.view.transform.position);
                        if (tank == null) return false;
                        var neighbour = standing.Skip(1).FirstOrDefault(p => Vector3.Distance(p.view.transform.position, culprit.view.transform.position) < Neighbour);
                        // The one who blames the fish speaks: the neighbour, or the culprit alone.
                        return Begin(gag, culprit.view, "culprit", neighbour.view, neighbour.view != null ? "blame" : null, tank, neighbour.view != null ? neighbour.v.Type : culprit.v.Type);
                    }
                case GrowthGag.Complaint:
                case GrowthGag.Mimic:
                    {
                        foreach (var (view, v) in standing.Where(p => p.v.State == GrowthVisitorState.Browsing && p.v.ContainerId != null))
                        {
                            if (!session.World.Tanks.TryGetValue(v.ContainerId, out var tank) || tank.Creatures.Count == 0) continue;
                            if (gag == GrowthGag.Mimic && tank.Detail != 0) continue;
                            var creature = tank.Creatures.Values.Where(c => c != null).OrderBy(c => Vector3.Distance(c.transform.position, view.transform.position)).ThenBy(c => c.SpecimenId).First();
                            return Begin(gag, view, gag == GrowthGag.Complaint ? "complain" : "fishface", null, null, tank, v.Type, creature);
                        }
                        return false;
                    }
            }
            return false;
        }

        GrowthWorld.TankView NearestTank(Vector3 at) =>
            session.World.Tanks.Values.Where(t => t != null && t.Creatures.Count > 0).OrderBy(t => Vector3.Distance(t.Root.position, at)).FirstOrDefault();

        bool Begin(GrowthGag gag, GrowthVisitorView who, string reaction, GrowthVisitorView neighbour, string neighbourReaction, GrowthWorld.TankView tank, string type, GrowthCreatureView creature = null)
        {
            if (!Policy.Start(gag, who.VisitorId)) return false;
            subject = who; subjectReaction = reaction;
            other = neighbour; otherReaction = neighbourReaction;
            about = tank.Root.position + Vector3.up * tank.WaterY;
            fish = creature ?? tank.Creatures.Values.FirstOrDefault(c => c != null);
            fishName = fish != null ? GrowthContent.Get(fish.Species)?.Name ?? "fish" : "fish";
            speaker = GrowthShop.TypeName(type);
            line = Policy.Variant(gag, 3);
            return true;
        }

        /// <summary>A hatch (the session passes the shop's events on): the burp waits for the hatchling to show.</summary>
        public void Event(GrowthEvent e)
        {
            if (e.Kind != GrowthEventKind.Hatched) return;
            // GR-32 (F55): testers found a burp at every hatch unpleasant: the finish is the audio's two notes, and a burp a rare treat.
            HatchesSinceBurp++;
            if (HatchesSinceBurp < BelchEvery) return;
            HatchesSinceBurp = 0;
            belchWaiting = true; belchAt = Policy.Clock + BelchDelay;
        }

        void WatchHatch()
        {
            if (!belchWaiting || Policy.Clock < belchAt) return;
            if (Policy.Clock > belchAt + BelchExpiry || Policy.Level == GrowthGagLevel.Off) { belchWaiting = false; Policy.Drop(); return; }
            if (session.Card != GrowthCard.None || !Policy.Start(GrowthGag.Belch, 0)) return;
            belchWaiting = false;
            line = Policy.Variant(GrowthGag.Belch, 2);
            speaker = "The incubator";
        }

        /// <summary>A helper who just finished a step and has had nothing to do since (Pip with no one in the queue) may dance.</summary>
        void WatchHelpers(GrowthShop shop)
        {
            foreach (var h in shop.Staff.Values)
            {
                lastStep.TryGetValue(h.Id, out var was);
                if (was != null && h.Step == null) finishedAt[h.Id] = Policy.Clock;
                lastStep[h.Id] = h.Step;
                if (!finishedAt.TryGetValue(h.Id, out float at)) continue;
                bool idle = h.Step == null && h.Hands.Count == 0 && h.Dose == null && (h.Id != "pip" || !shop.Queue.Any()) && session.World.FigureShowing(h.Id);
                if (!idle) { finishedAt.Remove(h.Id); continue; }
                if (Policy.Clock - at < DanceAfter) continue;
                finishedAt.Remove(h.Id);
                if (Policy.Current != null || session.Card != GrowthCard.None || !Policy.Start(GrowthGag.Dance, System.Array.IndexOf(GrowthPersonLook.Helpers, h.Id))) { Policy.Drop(); continue; }
                helper = h.Id;
                speaker = h.Def.Name;
                line = Policy.Variant(GrowthGag.Dance, 2);
            }
        }

        // ---------------------------------------------------------------- playing

        /// <summary>Drives the moment playing at its age: the reactions, its beats' sounds and its caption; ends it with the moment,
        /// or early when its subject is gone (or, for the room's moments, walks off or the day closes).</summary>
        void Play()
        {
            var gag = Policy.Current;
            if (gag == null) { if (subject != null || other != null || helper != null || fish != null || CaptionWho != null || beats.Count > 0) EndShow(); return; }
            float a = Policy.Age;
            var shop = session.Shop;
            switch (gag.Value)
            {
                case GrowthGag.Slap:
                    if (subject == null) { Policy.Cancel(); EndShow(); return; }
                    subject.React(subjectReaction, a, about);
                    if (subjectReaction == "slapped" && !staggered && subject.State == GrowthVisitorState.Leaving) { staggered = true; subject.Stagger(0.55f); }
                    if (subjectReaction == "slapped") Beat("boing", 0.12f, "boing", subject.transform.position + Vector3.up * 1.8f, 0.7f);
                    Beat("say", 0.45f, subjectReaction == "laugh" ? "laugh" : "grumble", subject.transform.position + Vector3.up * 1.5f, 0.9f,
                        subjectReaction == "laugh" ? GrowthComedy.Line(GrowthComedy.Laughs, line) : GrowthComedy.Line(GrowthComedy.Grumbles, line));
                    break;
                case GrowthGag.Boop:
                    if (subject == null) { Policy.Cancel(); EndShow(); return; }
                    subject.React("laugh", a, about);
                    Beat("say", 0.25f, "laugh", subject.transform.position + Vector3.up * 1.5f, 0.9f, GrowthComedy.Line(GrowthComedy.Giggles, line));
                    break;
                case GrowthGag.Fart:
                case GrowthGag.Complaint:
                case GrowthGag.Mimic:
                    if (subject == null || shop.Phase != GrowthPhase.Day || session.Shop.Visitors.All(v => v.Id != subject.VisitorId || v.State == GrowthVisitorState.Leaving))
                    { Policy.Cancel(); EndShow(); return; }
                    if (gag == GrowthGag.Fart) PlayFart(a);
                    else if (gag == GrowthGag.Complaint)
                    {
                        subject.React("complain", a, about);
                        Beat("say", 0.2f, line % 2 == 0 ? "mumble" : "mumble2", subject.transform.position + Vector3.up * 1.5f, 0.85f, GrowthComedy.Line(GrowthComedy.Complaints, line, fishName));
                    }
                    else
                    {
                        subject.React("fishface", a, about);
                        if (fish != null) { fish.MimicFor = Policy.Duration - 0.5f; fish.Mimic = a >= 0.5f ? a - 0.5f : -1; }
                        Beat("blub", 0.7f, "blub", fish != null ? fish.transform.position : about, 0.8f);
                        Beat("say", 0.9f, null, Vector3.zero, 0, GrowthComedy.Line(GrowthComedy.Mimics, line, fishName));
                    }
                    break;
                case GrowthGag.Belch:
                    session.World.Belch(a);
                    Beat("burp", 0.05f, "burp", GrowthWorld.IncubatorPosition + Vector3.up * session.World.IncubatorTop, 1f, GrowthComedy.Line(GrowthComedy.Belches, line));
                    break;
                case GrowthGag.Dance:
                    {
                        var h = helper == null ? null : shop.Helper(helper);
                        // The moment a step comes, the dance stops: the work never waits for it.
                        if (h == null || h.Step != null || h.Hands.Count > 0 || !session.World.FigureShowing(helper)) { Policy.Cancel(); EndShow(); return; }
                        session.World.HelperGag(helper, "dance", a);
                        var at = session.World.FigurePosition(helper);
                        for (int i = 0; i < 4; i++) Beat("shoe" + i, 0.25f + 0.5f * i, "shoe", at, 0.6f);
                        Beat("say", 0.3f, null, Vector3.zero, 0, GrowthComedy.Cheer(helper, line));
                        break;
                    }
            }
        }

        /// <summary>The fart: the squeak and the culprit's innocent look; after a moment the neighbour sniffs and blames the fish
        /// (or, alone, the culprit does).</summary>
        void PlayFart(float a)
        {
            subject.React(other != null || a < 0.9f ? "culprit" : "blame", other != null ? a : Mathf.Max(0, a - 0.9f) + (a >= 0.9f ? 0.8f : 0), about);
            Beat("squeak", 0.05f, line % 2 == 0 ? "squeak" : "squeak2", subject.transform.position + Vector3.up * 0.8f, 0.8f);
            if (other != null)
            {
                if (session.Shop.Visitors.Any(v => v.Id == other.VisitorId && v.State != GrowthVisitorState.Leaving)) other.React(a < 0.7f ? null : otherReaction, a - 0.7f, about);
                else { other.EndReaction(); other = null; }
            }
            Beat("say", 1.0f, null, Vector3.zero, 0, GrowthComedy.Line(GrowthComedy.Blames, line, fishName));
        }

        /// <summary>A beat of the moment: once, at its time, a sound where it happens and its caption.</summary>
        void Beat(string key, float at, string sound, Vector3 where, float gain, string caption = null)
        {
            if (Policy.Age < at || !beats.Add(key)) return;
            if (sound != null) Sound(sound, where, gain);
            if (caption != null) Caption(speaker, caption);
        }

        void Caption(string who, string words)
        {
            CaptionWho = who; CaptionText = words;
            session.Ui?.Caption(who, words);
        }

        void Sound(string name, Vector3 at, float gain)
        {
            Asked[name] = Asked.TryGetValue(name, out int n) ? n + 1 : 1;
            float g = session.HumourVolume * gain;
            LastGain = g; LastSoundAt = at;
            if (g <= 0 || !Sounds.TryGetValue(name, out var clip) || !isActiveAndEnabled) return;
            voice.transform.position = at;
            voice.clip = clip; voice.volume = g;
            voice.Play();
        }

        /// <summary>Everything the last moment showed goes: the reactions, the dance, the fish's face, the burp, the sound, the caption.</summary>
        void EndShow()
        {
            if (subject != null) subject.EndReaction();
            if (other != null) other.EndReaction();
            if (fish != null) fish.Mimic = -1;
            if (helper != null && session != null && session.World != null) session.World.HelperGag(helper, null, -1);
            if (session != null && session.World != null) session.World.Belch(-1);
            subject = other = null; fish = null; helper = null;
            subjectReaction = otherReaction = null;
            beats.Clear();
            if (voice != null && voice.isPlaying) voice.Stop();
            if (CaptionWho != null) { CaptionWho = CaptionText = null; session?.Ui?.ClearCaption(); }
        }

        // ---------------------------------------------------------------- the slap's key (the pause menu)

        /// <summary>Starts waiting for the slap's new key or button (GR-23: the Settings screen's Controls tab does it for every
        /// action; see <see cref="GrowthSession.StartRebind"/>).</summary>
        public void StartRebind() => session.StartRebind(GrowthAction.Slap);

        public static string KeyName(Key key) => key == Key.None ? "none" : key.ToString().Replace("Digit", "");

        /// <summary>The slap's key a save names, as the HUD and the pause menu show it (validation, by name).</summary>
        public static string KeyFromSave(string saved) => KeyName(GrowthSession.KeyFrom(saved));
        /// <summary>Validation: whether a key (by name) already does something, so it is never the slap's.</summary>
        public static bool IsReserved(string name) => System.Array.Exists(Reserved, k => k.ToString() == name);

        // ---------------------------------------------------------------- the sounds, made here (original; nothing recorded)

        /// <summary>The cartoon slap: a sharp crack of noise with a meaty thump under it, over in a quarter of a second.</summary>
        /// <summary>GR-31: a boop: a short rubbery squeak rising in pitch.</summary>
        public static float[] MakeBoop()
        {
            var d = new float[(int)(0.16f * SampleRate)];
            float phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate, u = t / 0.16f;
                phase += 2 * Mathf.PI * Mathf.Lerp(520f, 1180f, u) / SampleRate;
                d[i] = Mathf.Min(1, t / 0.006f) * (1 - u) * (1 - u) * 0.5f * (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2 * phase));
            }
            return d;
        }

        public static float[] MakeSlap()
        {
            var d = new float[(int)(0.25f * SampleRate)];
            uint s = 0x5EED1234u; float low = 0, band = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate;
                s = unchecked(s * 1664525u + 1013904223u);
                float noise = (s >> 8) / 8388607.5f - 1f;
                float f = 2 * Mathf.Sin(Mathf.PI * 2400f / SampleRate);
                low += f * band; float high = noise - low - 0.5f * band; band += f * high;
                d[i] = band * Mathf.Exp(-t * 55) * 0.55f + noise * Mathf.Exp(-t * 300) * 0.25f + Mathf.Sin(2 * Mathf.PI * 110 * t) * Mathf.Exp(-t * 30) * 0.3f;
            }
            return Ends(Tame(d));
        }

        /// <summary>The dizzy boing: a spring's twang, its pitch wobbling and falling.</summary>
        public static float[] MakeBoing()
        {
            var d = new float[(int)(0.6f * SampleRate)];
            float phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate;
                float f = 420f * Mathf.Exp(-t * 1.6f) * (1 + 0.12f * Mathf.Sin(2 * Mathf.PI * 14 * t));
                phase += 2 * Mathf.PI * f / SampleRate;
                d[i] = (Mathf.Sin(phase) * 0.6f + Mathf.Sin(2 * phase) * 0.2f) * Mathf.Min(1, t / 0.01f) * Mathf.Exp(-t * 4) * 0.5f;
            }
            return Ends(Tame(d));
        }

        /// <summary>Gibberish in a cartoon voice: syllables of a buzzing voice through two moving formants, its pitch rising and
        /// falling like speech (a grumble low, a complaint higher); no words, no recording.</summary>
        public static float[] MakeGibberish(int syllables, float pitch, float rate, int seed)
        {
            float each = 0.13f / rate;
            var d = new float[(int)((syllables * each + 0.1f) * SampleRate)];
            var rng = new System.Random(seed);
            float phase = 0, b1 = 0, l1 = 0, b2 = 0, l2 = 0;
            var vowels = new[] { (700f, 1200f), (400f, 2000f), (300f, 900f), (600f, 1700f), (500f, 1000f) };
            for (int k = 0; k < syllables; k++)
            {
                var (f1, f2) = vowels[rng.Next(vowels.Length)];
                float bend = (float)rng.NextDouble() * 0.3f - 0.1f;
                int a = (int)(k * each * SampleRate), n = (int)(each * 0.85f * SampleRate);
                for (int j = 0; j < n && a + j < d.Length; j++)
                {
                    float t = j / SampleRate, p = j / (float)n;
                    float f0 = pitch * (1 + bend * p + 0.08f * Mathf.Sin(2 * Mathf.PI * k / syllables));
                    phase += 2 * Mathf.PI * f0 / SampleRate;
                    float buzz = Mathf.Repeat(phase / (2 * Mathf.PI), 1) * 2 - 1;
                    float c1 = 2 * Mathf.Sin(Mathf.PI * f1 / SampleRate), c2 = 2 * Mathf.Sin(Mathf.PI * f2 / SampleRate);
                    l1 += c1 * b1; float h1 = buzz - l1 - 0.25f * b1; b1 += c1 * h1;
                    l2 += c2 * b2; float h2 = buzz - l2 - 0.3f * b2; b2 += c2 * h2;
                    float env = Mathf.Sin(Mathf.PI * p);
                    d[a + j] += (b1 * 0.6f + b2 * 0.35f) * env * 0.35f;
                }
            }
            return Ends(Tame(d));
        }

        /// <summary>A cartoon laugh: four "ha"s, each a breathy burst and a bright buzzing vowel, falling in pitch.</summary>
        public static float[] MakeLaugh()
        {
            var d = new float[(int)(0.95f * SampleRate)];
            uint s = 0x1A2B3C4Du; float phase = 0, b = 0, l = 0;
            for (int k = 0; k < 4; k++)
            {
                int a = (int)(k * 0.21f * SampleRate), n = (int)(0.17f * SampleRate);
                float pitch = 260 - 22 * k;
                for (int j = 0; j < n && a + j < d.Length; j++)
                {
                    float t = j / SampleRate, p = j / (float)n;
                    s = unchecked(s * 1664525u + 1013904223u);
                    float noise = (s >> 8) / 8388607.5f - 1f;
                    phase += 2 * Mathf.PI * pitch * (1 - 0.15f * p) / SampleRate;
                    float buzz = Mathf.Repeat(phase / (2 * Mathf.PI), 1) * 2 - 1;
                    float c = 2 * Mathf.Sin(Mathf.PI * 800f / SampleRate);
                    l += c * b; float h = (buzz * Mathf.Clamp01((p - 0.15f) * 6) + noise * Mathf.Clamp01(1 - p * 5) * 0.8f) - l - 0.3f * b; b += c * h;
                    d[a + j] += b * Mathf.Sin(Mathf.PI * p) * 0.4f;
                }
            }
            return Ends(Tame(d));
        }

        /// <summary>The suspicious squeak: a short rubbery buzz, its pitch wavering (two of them, so it is not the same each time).</summary>
        public static float[] MakeSqueak(int which)
        {
            float length = which == 0 ? 0.42f : 0.3f;
            var d = new float[(int)(length * SampleRate)];
            float phase = 0, b = 0, l = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate, p = t / length;
                float f = (which == 0 ? 95f : 130f) * (1 + 0.35f * p) * (1 + 0.06f * Mathf.Sin(2 * Mathf.PI * 22 * t));
                phase += 2 * Mathf.PI * f / SampleRate;
                float buzz = Mathf.Repeat(phase / (2 * Mathf.PI), 1) * 2 - 1;
                float c = 2 * Mathf.Sin(Mathf.PI * (which == 0 ? 420f : 560f) / SampleRate);
                l += c * b; float h = buzz - l - 0.4f * b; b += c * h;
                d[i] = b * Mathf.Min(1, t / 0.02f) * Mathf.Pow(1 - p, 0.7f) * 0.45f;
            }
            return Ends(Tame(d));
        }

        /// <summary>The incubator's burp: a low, gurgling vowel half a second long.</summary>
        public static float[] MakeBurp()
        {
            var d = new float[(int)(0.55f * SampleRate)];
            float phase = 0, b = 0, l = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate, p = t / 0.55f;
                float f = 78f * (1 + 0.3f * Mathf.Sin(Mathf.PI * p)) * (1 + 0.1f * Mathf.Sin(2 * Mathf.PI * 31 * t));
                phase += 2 * Mathf.PI * f / SampleRate;
                float buzz = Mathf.Repeat(phase / (2 * Mathf.PI), 1) * 2 - 1;
                float c = 2 * Mathf.Sin(Mathf.PI * Mathf.Lerp(500f, 350f, p) / SampleRate);
                l += c * b; float h = buzz - l - 0.35f * b; b += c * h;
                d[i] = b * Mathf.Min(1, t / 0.03f) * Mathf.Sin(Mathf.PI * Mathf.Min(1, p * 1.1f)) * 0.55f;
            }
            return Ends(Tame(d));
        }

        /// <summary>The fish's blub and honk: two bubbly bloops, then a little nasal honk.</summary>
        public static float[] MakeBlub()
        {
            var d = new float[(int)(0.6f * SampleRate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate;
                for (int k = 0; k < 2; k++)
                {
                    float u = t - 0.12f * k;
                    if (u < 0 || u > 0.12f) continue;
                    d[i] += Mathf.Sin(2 * Mathf.PI * (260 * u + 2400 * u * u)) * Mathf.Sin(Mathf.PI * u / 0.12f) * 0.35f;
                }
                float h = t - 0.3f;
                if (h >= 0 && h < 0.25f)
                {
                    float hp = h / 0.25f, f = 340 + 60 * Mathf.Sin(Mathf.PI * hp);
                    float sq = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * h)) * 0.5f + Mathf.Sin(2 * Mathf.PI * 2 * f * h) * 0.2f;
                    d[i] += sq * Mathf.Sin(Mathf.PI * hp) * 0.22f;
                }
            }
            return Ends(Tame(d));
        }

        /// <summary>A squeaky shoe: a bright chirp sliding up, a tenth of a second.</summary>
        public static float[] MakeShoe()
        {
            var d = new float[(int)(0.12f * SampleRate)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / SampleRate, p = t / 0.12f;
                d[i] = Mathf.Sin(2 * Mathf.PI * (1900 * t + 2600 * t * t)) * Mathf.Sin(Mathf.PI * p) * (0.7f + 0.3f * Mathf.Sin(2 * Mathf.PI * 60 * t)) * 0.3f;
            }
            return Ends(Tame(d));
        }

        /// <summary>Keeps a sound's peak at most 0.6 (nothing clips, however the moments stack with the shop's own sounds).</summary>
        static float[] Tame(float[] d)
        {
            float peak = 0;
            foreach (var x in d) peak = Mathf.Max(peak, Mathf.Abs(x));
            if (peak > 0.6f) for (int i = 0; i < d.Length; i++) d[i] *= 0.6f / peak;
            return d;
        }

        /// <summary>Starts and ends a one-shot at silence (a short fade at each end), so it never clicks.</summary>
        static float[] Ends(float[] d)
        {
            int n = Mathf.Min(64, d.Length / 4);
            for (int i = 0; i < n; i++) { float k = i / (float)n; d[i] *= k; d[d.Length - 1 - i] *= k; }
            return d;
        }
    }
}
