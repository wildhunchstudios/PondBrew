using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D (AD.5) flipbook effects everywhere, while the look is on, in place of the pool's procedural accents: a sale
    /// scoops the creature into a bag that ties and hops into the visitor's hand while coins jump from the counter (the
    /// money chip's own coins carry on to it); a slap is an impact star (the visitor's birds and spinning hat are its own);
    /// a refusal or a blocked action is only a small grey puff and, at the siphon, a shake; the comedy beats, crew work and
    /// price nods are the same six hand-drawn shapes; display fish leave bubble trails. Each moment follows the cue that
    /// starts it and clears on every cancellation that clears the cue's owner (pause, focus, arrange, reset, the look off).
    /// Comfort: reduced motion keeps every moment to a still puff or glint (no hop, shake, coin jump or trail); low detail
    /// halves the particles.
    /// </summary>
    public sealed partial class ShopWorld
    {
        public enum DioramaSound { BagTie, Birds, Puff }

        sealed class DioramaSale
        {
            public FxOwnerToken Owner; public CustomerActor Actor; public Transform Bag, Tie;
            public Vector3 Scoop; public float Age; public uint Seed; public bool Reduced, Low, Tied, Coined, Popped;
        }

        public const float DioramaSaleSeconds = 2.25f, DioramaBagTieAt = .38f, DioramaCoinsAt = .5f, DioramaShakeSeconds = .24f;
        ParticleSystem dioramaMomentBubbles, dioramaMomentPuffs, dioramaMomentSparkles, dioramaMomentRings, dioramaMomentSwirls, dioramaMomentCoins;
        readonly List<DioramaSale> dioramaSales = new List<DioramaSale>();
        readonly List<Transform> dioramaBagPool = new List<Transform>();
        readonly List<(float at, DioramaSound sound, FxOwnerToken owner)> dioramaSoundsDue = new List<(float, DioramaSound, FxOwnerToken)>();
        readonly Dictionary<Transform, float> dioramaTrailClocks = new Dictionary<Transform, float>();
        Material dioramaBagGlass, dioramaBagWater, dioramaBagFish, dioramaBagTie, dioramaBagRibbon;
        float dioramaMomentClock, dioramaToolShake;

        /// <summary>Asks the session's audio for a diorama sound on an existing voice (set by the session).</summary>
        public System.Action<DioramaSound> DioramaSoundCue;
        public int DioramaSoundCues { get; private set; }
        public int DioramaSaleMoments { get; private set; }
        public int DioramaImpactStars { get; private set; }
        public int DioramaGreyPuffs { get; private set; }
        public int DioramaFlipbookAccents { get; private set; }
        public int DioramaBubbleTrails { get; private set; }
        public int DioramaToolShakes { get; private set; }
        /// <summary>Particles alive in the moments' flipbook systems now.</summary>
        public int DioramaMomentParticles
        {
            get
            {
                int n = 0;
                foreach (var ps in DioramaMomentSystems) if (ps) n += ps.particleCount;
                return n;
            }
        }
        /// <summary>The sale bag shown now (null when none), and whether it is tied.</summary>
        public Transform DioramaSaleBag { get { foreach (var s in dioramaSales) if (s.Bag && s.Bag.gameObject.activeSelf) return s.Bag; return null; } }
        public bool DioramaSaleBagTied { get { foreach (var s in dioramaSales) if (s.Tied && s.Bag && s.Bag.gameObject.activeSelf) return true; return false; } }
        public float DioramaToolShakeLeft => dioramaToolShake;

        IEnumerable<ParticleSystem> DioramaMomentSystems =>
            new[] { dioramaMomentBubbles, dioramaMomentPuffs, dioramaMomentSparkles, dioramaMomentRings, dioramaMomentSwirls, dioramaMomentCoins };

        bool DioramaMomentsLow => aquariumLow || fxPool != null && fxPool.ActionLimit < 64;
        /// <summary>How many flipbook particles the last grey puff let go (low detail and reduced motion let one go).</summary>
        public int DioramaLastPuffSize { get; private set; }

        void EnsureDioramaMoments()
        {
            if (dioramaMomentBubbles || !dioramaRoot) return;
            var sheet = Resources.Load<Texture2D>(DioramaLook.KitPath + "DioramaFlipbook");
            var plain = DioramaOwn(DioramaLook.Unlit("flipbook moments", Color.white, false, sheet));
            var gold = DioramaOwn(DioramaLook.Unlit("flipbook moments gold", new Color(2.4f, 2f, 1.1f, 1), false, sheet));
            var bubbleFrames = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.15f, .3f), new Keyframe(.82f, .55f), new Keyframe(.9f, .8f), new Keyframe(1, .99f));
            var linear = AnimationCurve.Linear(0, 0, 1, .99f);
            dioramaMomentBubbles = DioramaFlipbook("Diorama moment bubbles", DioramaRowBubble, plain, -.16f, bubbleFrames, false);
            dioramaMomentPuffs = DioramaFlipbook("Diorama moment puffs", DioramaRowPuff, plain, -.04f, linear, true);
            dioramaMomentSparkles = DioramaFlipbook("Diorama moment sparkles", DioramaRowSparkle, gold, 0, linear, true);
            dioramaMomentRings = DioramaFlipbook("Diorama moment ripples", DioramaRowRipple, plain, 0, linear, true);
            dioramaMomentSwirls = DioramaFlipbook("Diorama moment swirls", DioramaRowSwirl, plain, 0, linear, true);
            dioramaMomentCoins = DioramaFlipbook("Diorama moment coins", DioramaRowCoin, gold, .55f, linear, false);
            var coinSheet = dioramaMomentCoins.textureSheetAnimation; coinSheet.cycleCount = 2;
        }

        void DioramaSoundNow(DioramaSound sound, FxOwnerToken owner, float delay = 0)
        {
            if (!dioramaActive) return;
            if (delay > 0) { dioramaSoundsDue.Add((dioramaMomentClock + delay, sound, owner)); return; }
            DioramaSoundCues++;
            DioramaSoundCue?.Invoke(sound);
        }

        void DioramaAccent(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float rotation = 0)
        {
            DioramaEmit(ps, position, velocity, size, lifetime, color, rotation);
            DioramaFlipbookAccents++;
        }

        static readonly Color DioramaGrey = new Color(.74f, .76f, .8f, .92f);

        /// <summary>A refusal or a blocked action: one small grey puff (and a smaller one beside it unless reduced).</summary>
        void DioramaGreyPuff(Vector3 position, FxOwnerToken owner, bool sound)
        {
            EnsureDioramaMoments();
            DioramaAccent(dioramaMomentPuffs, position, fxReducedMotion ? Vector3.zero : new Vector3(0, .12f, 0), .26f, .6f, DioramaGrey, 15);
            DioramaLastPuffSize = fxReducedMotion || DioramaMomentsLow ? 1 : 2;
            if (!fxReducedMotion && !DioramaMomentsLow) DioramaAccent(dioramaMomentPuffs, position + new Vector3(.09f, -.04f, 0), new Vector3(.08f, .08f, 0), .11f, .5f, DioramaGrey, -20);
            DioramaGreyPuffs++;
            if (sound) DioramaSoundNow(DioramaSound.Puff, owner);
        }

        // ---------------------------------------------------------------- sale

        Transform DioramaBag()
        {
            foreach (var bag in dioramaBagPool) if (bag && !bag.gameObject.activeSelf) return bag;
            if (!dioramaBagGlass)
            {
                dioramaBagGlass = DioramaOwn(DioramaLook.Glass("sale bag", new Color(.92f, .98f, 1f, .24f), .9f, .95f));
                dioramaBagWater = DioramaOwn(DioramaLook.Glass("sale bag water", new Color(.3f, .78f, .92f, .34f), .3f, .55f));
                dioramaBagFish = DioramaOwn(DioramaLook.Toon("sale bag fish", new Color(1f, .62f, .3f), DioramaLook.Style.Prop));
                dioramaBagTie = DioramaOwn(DioramaLook.Toon("sale bag knot", new Color(.97f, .93f, .82f), DioramaLook.Style.Prop));
                dioramaBagRibbon = DioramaOwn(DioramaLook.Toon("sale bag ribbon", new Color(.92f, .38f, .36f), DioramaLook.Style.Prop));
            }
            var root = Group("Diorama sale bag", dioramaRoot, Vector3.zero);
            BagPart(root, "Bag water", PrimitiveType.Sphere, new Vector3(0, -.025f, 0), new Vector3(.17f, .17f, .17f), dioramaBagWater);
            var fish = Group("Bag fish", root, new Vector3(0, -.02f, 0));
            BagPart(fish, "Fish body", PrimitiveType.Sphere, Vector3.zero, new Vector3(.05f, .045f, .075f), dioramaBagFish);
            BagPart(fish, "Fish tail", PrimitiveType.Sphere, new Vector3(0, 0, -.05f), new Vector3(.014f, .04f, .03f), dioramaBagFish);
            BagPart(root, "Bag", PrimitiveType.Sphere, Vector3.zero, new Vector3(.21f, .25f, .21f), dioramaBagGlass);
            BagPart(root, "Bag neck", PrimitiveType.Cylinder, new Vector3(0, .145f, 0), new Vector3(.06f, .035f, .06f), dioramaBagGlass);
            var tie = Group("Bag tie", root, new Vector3(0, .18f, 0));
            BagPart(tie, "Knot", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .05f, dioramaBagTie);
            for (int s = -1; s <= 1; s += 2)
                BagPart(tie, "Ribbon loop", PrimitiveType.Sphere, new Vector3(s * .045f, .014f, 0), new Vector3(.065f, .032f, .02f), dioramaBagRibbon).localRotation = Quaternion.Euler(0, 0, s * 25);
            root.gameObject.SetActive(false);
            dioramaBagPool.Add(root);
            return root;
        }

        Transform BagPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = Shape(parent, name, type, position, scale, material);
            var r = part.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return part;
        }

        /// <summary>The checkout moment at a visitor (after the sale cue played and the visitor's own reaction started).</summary>
        void PresentDioramaSale(CustomerActor actor, FxOwnerToken owner, uint sequence)
        {
            EnsureDioramaMoments();
            CancelDioramaSale(owner);
            var sale = new DioramaSale { Owner = owner, Actor = actor, Seed = sequence, Reduced = fxReducedMotion, Low = DioramaMomentsLow };
            sale.Scoop = actor.transform.TransformPoint(new Vector3(0, 1.2f, .5f));
            sale.Bag = DioramaBag(); sale.Tie = sale.Bag.Find("Bag tie");
            sale.Bag.position = sale.Scoop; sale.Bag.rotation = actor.transform.rotation; sale.Bag.localScale = Vector3.zero;
            sale.Tie.localScale = Vector3.zero;
            sale.Bag.gameObject.SetActive(true);
            dioramaSales.Add(sale);
            DioramaSaleMoments++;
            // The scoop: a ripple where the creature is lifted, and its splash of bubbles.
            DioramaAccent(dioramaMomentRings, sale.Scoop, Vector3.zero, .26f, .5f, new Color(.62f, .93f, 1f, .9f));
            int bubbles = sale.Reduced ? 1 : sale.Low ? 2 : 4;
            for (int i = 0; i < bubbles; i++)
            {
                float a = DioramaNoise(i, sequence, 13), b = DioramaNoise(i, sequence, 29);
                var v = sale.Reduced ? Vector3.zero : new Vector3((a - .5f) * .3f, .35f + b * .3f, (b - .5f) * .2f);
                DioramaAccent(dioramaMomentBubbles, sale.Scoop + new Vector3((a - .5f) * .1f, .04f, 0), v, .045f + b * .03f, .7f + a * .3f, new Color(.82f, .97f, 1f));
            }
        }

        void TickDioramaSales(float dt)
        {
            for (int i = dioramaSales.Count - 1; i >= 0; i--)
            {
                var sale = dioramaSales[i];
                bool alive = sale.Actor && sale.Actor.isActiveAndEnabled && fxCues != null && fxCues.IsValid(sale.Owner) && sale.Bag;
                if (!alive) { EndDioramaSale(i); continue; }
                sale.Age += dt;
                float t = sale.Age;
                var actor = sale.Actor.transform;
                // The bag fills and appears at the scoop (a small overshoot unless reduced).
                float grow = Mathf.Clamp01(t / .3f);
                float scale = sale.Reduced ? grow : grow < 1 ? Mathf.Sin(grow * Mathf.PI * .5f) * 1.12f : 1;
                if (t > .3f && !sale.Reduced) scale = Mathf.Lerp(1.12f, 1, Mathf.Clamp01((t - .3f) / .12f));
                if (!sale.Tied && t >= DioramaBagTieAt)
                {
                    sale.Tied = true;
                    var knot = sale.Tie.position;
                    DioramaAccent(dioramaMomentSwirls, knot, Vector3.zero, sale.Reduced ? .1f : .16f, .45f, new Color(1f, .97f, .9f, .95f));
                    if (!sale.Reduced) DioramaAccent(dioramaMomentSparkles, knot + new Vector3(.05f, .04f, 0), new Vector3(.2f, .3f, 0), .09f, .4f, Color.white);
                    DioramaSoundNow(DioramaSound.BagTie, sale.Owner);
                }
                if (sale.Tied) sale.Tie.localScale = Vector3.one * Mathf.Clamp01((t - DioramaBagTieAt) / .12f);
                if (!sale.Coined && t >= DioramaCoinsAt)
                {
                    sale.Coined = true;
                    // Coins jump from the counter; the money chip's own coins carry the sale to it.
                    int coins = sale.Reduced ? 0 : sale.Low ? 2 : 4;
                    for (int c = 0; c < coins; c++)
                    {
                        float a = DioramaNoise(c, sale.Seed, 41), b = DioramaNoise(c, sale.Seed, 53);
                        DioramaAccent(dioramaMomentCoins, sale.Scoop + new Vector3((a - .5f) * .14f, .02f, 0),
                            actor.TransformVector(new Vector3((a - .5f) * .8f, 1.5f + b * .6f, -.35f)), .075f, .55f, Color.white, a * 90);
                    }
                }
                // The bag hops from the counter into the visitor's hand and rides along until it pops away.
                var hand = actor.TransformPoint(new Vector3(.27f, .92f, .18f)) + Vector3.up * sale.Actor.DioramaHopLift;
                float move = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .62f) / .38f));
                var position = Vector3.Lerp(sale.Scoop, hand, move);
                if (!sale.Reduced) position += Vector3.up * Mathf.Sin(move * Mathf.PI) * .12f;
                sale.Bag.position = position;
                sale.Bag.rotation = actor.rotation * Quaternion.Euler(0, sale.Reduced ? 0 : Mathf.Sin(t * 3) * 8, 0);
                if (t > DioramaSaleSeconds - .25f) scale *= Mathf.Clamp01((DioramaSaleSeconds - t) / .25f);
                sale.Bag.localScale = Vector3.one * scale;
                var fish = sale.Bag.Find("Bag fish");
                if (fish && !sale.Reduced) fish.localRotation = Quaternion.Euler(0, t * 140, 0);
                if (!sale.Popped && t > DioramaSaleSeconds - .2f)
                {
                    sale.Popped = true;
                    DioramaAccent(dioramaMomentPuffs, position, Vector3.zero, .14f, .4f, new Color(1, 1, 1, .9f));
                }
                if (t >= DioramaSaleSeconds) EndDioramaSale(i);
            }
        }

        void EndDioramaSale(int index)
        {
            var sale = dioramaSales[index];
            if (sale.Bag) sale.Bag.gameObject.SetActive(false);
            dioramaSales.RemoveAt(index);
        }

        void CancelDioramaSale(FxOwnerToken owner)
        {
            for (int i = dioramaSales.Count - 1; i >= 0; i--) if (dioramaSales[i].Owner.Equals(owner)) EndDioramaSale(i);
        }

        // ---------------------------------------------------------------- hooks from the classic cues

        /// <summary>A visitor's price nod, refusal or checkout with the look on; returns true when it presented the moment.</summary>
        bool PresentDioramaCustomerReaction(CustomerActor actor, FxCustomerReaction reaction, FxOwnerToken owner, uint sequence)
        {
            if (!dioramaActive || !dioramaRoot) return false;
            EnsureDioramaMoments();
            float top = actor.DioramaVisitor && actor.DioramaVisitor.DioramaRole != null ? actor.DioramaVisitor.DioramaRole.HeadTop : 1.82f;
            if (reaction == FxCustomerReaction.Checkout) PresentDioramaSale(actor, owner, sequence);
            else if (reaction == FxCustomerReaction.Refused) DioramaGreyPuff(actor.transform.TransformPoint(new Vector3(.18f, top + .05f, .1f)), owner, true);
            else
            {
                var point = actor.transform.TransformPoint(new Vector3(.2f, top - .1f, .15f));
                DioramaAccent(dioramaMomentSwirls, point, Vector3.zero, fxReducedMotion ? .1f : .14f, .5f, new Color(.62f, .96f, .84f, .95f));
                if (!fxReducedMotion) DioramaAccent(dioramaMomentSparkles, point + new Vector3(.06f, .05f, 0), new Vector3(.1f, .25f, 0), .08f, .45f, Color.white);
            }
            return true;
        }

        /// <summary>A worker's moment with the look on; returns true when it presented it.</summary>
        bool PresentDioramaWorkerEvent(FxWorkerEvent kind, Transform source, Vector3 point, FxOwnerToken owner, uint sequence)
        {
            if (!dioramaActive || !dioramaRoot) return false;
            EnsureDioramaMoments();
            var at = source.TransformPoint(point);
            switch (kind)
            {
                case FxWorkerEvent.Refused: DioramaGreyPuff(at, owner, true); break;
                case FxWorkerEvent.Checkout:
                    DioramaAccent(dioramaMomentSparkles, at, Vector3.zero, .14f, .45f, Color.white);
                    if (!fxReducedMotion)
                        for (int i = 0; i < (DioramaMomentsLow ? 1 : 2); i++)
                            DioramaAccent(dioramaMomentCoins, at, source.TransformVector(new Vector3((i - .5f) * .5f, 1.3f, -.2f)), .07f, .5f, Color.white, i * 60);
                    break;
                case FxWorkerEvent.Place: DioramaAccent(dioramaMomentPuffs, at, Vector3.zero, .14f, .45f, new Color(1, 1, 1, .85f)); break;
                default:
                    int bubbles = fxReducedMotion ? 1 : DioramaMomentsLow ? 1 : 3;
                    for (int i = 0; i < bubbles; i++)
                        DioramaAccent(dioramaMomentBubbles, at + new Vector3((i - 1) * .04f, 0, 0), fxReducedMotion ? Vector3.zero : new Vector3(0, .3f + i * .08f, 0), .05f, .6f, new Color(.78f, .97f, 1f));
                    break;
            }
            return true;
        }

        /// <summary>A comedy beat's flipbook with the look on: the slap's impact star, the belch's bubbles, the fart's puff,
        /// the mimic's bubbles and a celebration's sparkles; returns true when it presented it.</summary>
        bool PresentDioramaComedy(ComedyKind kind, int variant, Transform source, Vector3 point, bool reduced, FxOwnerToken owner)
        {
            if (!dioramaActive || !dioramaRoot) return false;
            EnsureDioramaMoments();
            var at = source.TransformPoint(point);
            bool low = DioramaMomentsLow;
            switch (kind)
            {
                case ComedyKind.Slap:
                    DioramaAccent(dioramaMomentSparkles, at, Vector3.zero, reduced ? .22f : .42f, .45f, Color.white, variant * 20);
                    if (!reduced) DioramaAccent(dioramaMomentRings, at, Vector3.zero, .3f, .3f, new Color(1f, .95f, .8f, .85f));
                    if (!reduced && !low)
                        for (int s = -1; s <= 1; s += 2)
                            DioramaAccent(dioramaMomentPuffs, at + new Vector3(s * .06f, 0, 0), new Vector3(s * .35f, .15f, 0), .1f, .4f, new Color(1, 1, 1, .9f));
                    DioramaImpactStars++;
                    if (!reduced) DioramaSoundNow(DioramaSound.Birds, owner, .4f);
                    break;
                case ComedyKind.Fart:
                    for (int i = 0; i < (reduced || low ? 1 : 3); i++)
                        DioramaAccent(dioramaMomentPuffs, at + new Vector3(i * .05f, 0, 0), reduced ? Vector3.zero : new Vector3(.1f + i * .05f, .1f, 0), .16f - i * .03f, .7f, new Color(.78f, .86f, .6f, .85f));
                    break;
                case ComedyKind.WorkerCelebration:
                    DioramaAccent(dioramaMomentSwirls, at, Vector3.zero, .16f, .5f, new Color(1f, .93f, .7f, .95f));
                    for (int i = 0; i < (reduced ? 0 : low ? 1 : 3); i++)
                        DioramaAccent(dioramaMomentSparkles, at, new Vector3((i - 1) * .3f, .35f, 0), .08f, .45f, Color.white, i * 30);
                    break;
                default:
                    // Belch and mimic: bubbles (the lab's fizz).
                    for (int i = 0; i < (reduced ? 1 : low ? 2 : 5); i++)
                    {
                        float a = DioramaNoise(i, (uint)variant, 61);
                        DioramaAccent(dioramaMomentBubbles, at + new Vector3((a - .5f) * .06f, i * .02f, 0), reduced ? Vector3.zero : new Vector3((a - .5f) * .2f, .3f + a * .25f, 0),
                            .04f + a * .03f, .8f, new Color(.72f, .97f, .9f));
                    }
                    break;
            }
            return true;
        }

        /// <summary>The siphon refusing or being blocked with the look on: a grey puff at its nozzle and a short shake.</summary>
        void PresentDioramaToolRefusal(Transform source, Vector3 point, FxOwnerToken owner)
        {
            if (!dioramaActive || !dioramaRoot) return;
            DioramaGreyPuff(source.TransformPoint(point), owner, false);
            if (!fxReducedMotion) { dioramaToolShake = DioramaShakeSeconds; DioramaToolShakes++; }
        }

        /// <summary>The siphon's shake this frame (local offset), as the hands pose adds it.</summary>
        Vector3 DioramaToolShakeOffset
        {
            get
            {
                if (dioramaToolShake <= 0 || !dioramaActive) return Vector3.zero;
                float t = 1 - dioramaToolShake / DioramaShakeSeconds;
                return new Vector3(Mathf.Sin(t * Mathf.PI * 8) * .012f * (1 - t), 0, 0);
            }
        }

        // ---------------------------------------------------------------- water

        /// <summary>A kit fish lets a few bubbles go from its lips every few seconds while it swims (not in reduced motion).</summary>
        void TickDioramaBubbleTrail(Transform key, CreatureVisual creature, float dt, bool live)
        {
            if (!live || !creature || !creature.IsDiorama || !dioramaRoot) return;
            dioramaTrailClocks.TryGetValue(key, out float clock);
            clock -= dt;
            if (clock <= 0)
            {
                EnsureDioramaMoments();
                bool low = DioramaMomentsLow;
                uint seed = (uint)(key.GetInstanceID() & 0xffff) + (uint)DioramaBubbleTrails;
                var lips = creature.DioramaLips ? creature.DioramaLips.position : creature.transform.position;
                int count = low ? 1 : 2 + (int)(DioramaNoise(0, seed, 7) * 2);
                float size = Mathf.Max(.4f, creature.transform.lossyScale.x);
                for (int i = 0; i < count; i++)
                {
                    float a = DioramaNoise(i, seed, 17);
                    DioramaEmit(dioramaMomentBubbles, lips + Vector3.up * i * .02f * size, new Vector3((a - .5f) * .05f, .14f + a * .08f, 0), (.034f + a * .02f) * size, 1.1f + a * .4f, new Color(.86f, .98f, 1f, .9f));
                }
                DioramaBubbleTrails++;
                clock = (low ? 5.5f : 2.6f) + DioramaNoise(1, seed, 23) * 2;
            }
            dioramaTrailClocks[key] = clock;
        }

        // ---------------------------------------------------------------- the clock and clearing

        void TickDioramaMoments(float dt)
        {
            if (!dioramaActive) return;
            dt = Mathf.Max(0, dt);
            dioramaMomentClock += dt;
            if (dioramaToolShake > 0) dioramaToolShake = Mathf.Max(0, dioramaToolShake - dt);
            for (int i = dioramaSoundsDue.Count - 1; i >= 0; i--)
            {
                var (at, sound, owner) = dioramaSoundsDue[i];
                if (dioramaMomentClock < at) continue;
                dioramaSoundsDue.RemoveAt(i);
                if (fxCues != null && (!owner.IsDefined || fxCues.IsValid(owner))) DioramaSoundNow(sound, owner);
            }
            TickDioramaSales(dt);
        }

        /// <summary>An owner's cancellation clears its moments (its bag, its pending sounds).</summary>
        void CancelDioramaMoments(FxOwnerToken owner)
        {
            if (!owner.IsDefined) return;
            CancelDioramaSale(owner);
            dioramaSoundsDue.RemoveAll(s => s.owner.Equals(owner));
        }

        /// <summary>Every cancellation boundary (and the look switched off) clears every moment and its particles.</summary>
        void ClearDioramaMoments()
        {
            for (int i = dioramaSales.Count - 1; i >= 0; i--) EndDioramaSale(i);
            dioramaSoundsDue.Clear();
            dioramaToolShake = 0;
            foreach (var ps in DioramaMomentSystems) if (ps) ps.Clear(true);
        }
    }
}
