using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// A visitor walking the shop's routes as the Core visit unfolds. GR-18 wave 1: in the look, a person from the people kit
    /// (<see cref="GrowthPerson"/>), their kind's figure with a look of their own (seeded by the visitor's id), who walks in
    /// their kind's way (the step matched to the distance walked), browses a tank (leaning in, pointing), waits in the queue
    /// (a tapping foot and a look at the wrist as patience runs low), holds out the coins at the counter, and leaves with the
    /// creature they bought in the shop's water bag and a grin, or with a shrug, a huff, or a wave at closing. Without the look
    /// (-evolutionGrowthPlain) or the kit, the greybox's coloured capsule. Where a visitor goes stays the rules' and the
    /// routes': the figure only shows it. GR-18 wave 4 (§6.7): a silly moment's reaction plays over what the visitor is doing
    /// (<see cref="React"/>, driven by GrowthGags' clock): slapped (the head knocked aside, eyes squeezed shut, a hand mark, dizzy
    /// stars, a grumble on the way out), laughing a slap off, the innocent culprit of a smell with its puff of a cloud, a sniff
    /// and the finger at the fish, a complaint, and a fish face at the glass. It never changes where they go.
    /// </summary>
    public sealed class GrowthVisitorView : MonoBehaviour
    {
        public int VisitorId { get; private set; }
        public GrowthVisitorState State { get; private set; } = (GrowthVisitorState)(-1);
        public bool Arrived => path.Count == 0;
        readonly List<Vector3> path = new List<Vector3>();
        string place;
        int queueIndex = -1;
        bool wasQueued;
        /// <summary>GR-10: as the rules time a visitor's walk (GrowthSpaces.VisitorSpeed).</summary>
        const float Speed = GrowthSpaces.VisitorSpeed;
        static readonly Dictionary<string, Material> visitorMaterials = new Dictionary<string, Material>();

        // GR-18: the person, and what they are doing.
        GrowthPerson person;
        string kind;
        GrowthPeopleMotion.Gait gait;
        float clock, phase, leftAt = -1, stateAt;
        GrowthVisitOutcome outcome;
        float impatience;
        GrowthSpecimen bought;
        GrowthCreatureView bagged;
        // GR-20: the visitor's wish, its bubble, and whether a sale granted it.
        GrowthWish wish;
        GrowthWishBubble bubble;
        bool wishGranted;
        /// <summary>Validation: the wish's bubble (null for a visitor without a wish).</summary>
        public GrowthWishBubble Bubble => bubble;
        readonly GrowthPersonPose target = new GrowthPersonPose(), pose = new GrowthPersonPose();
        bool posed;
        /// <summary>The person drawn for this visitor (null in the greybox look).</summary>
        public GrowthPerson Person => person;
        /// <summary>Validation: what the figure is doing now (walk, stand, browse, wait, pay, leave-happy, leave-shrug,
        /// leave-huff, leave-wave), and how far it has walked.</summary>
        public string Activity { get; private set; } = "stand";
        public float Walked { get; private set; }
        /// <summary>Validation: the species carried home in the shop's bag (null until one is).</summary>
        public string Carrying => bagged != null ? bagged.Species : null;

        // GR-18 wave 4: the silly moment this visitor reacts to, how far into it, and where what it is about is.
        string reaction;
        float reactionAge, holdUntil = -1;
        Vector3 reactionAt;
        /// <summary>Validation: the reaction playing (slapped, laugh, culprit, sniff, blame, complain, fishface), or null; how
        /// long the walk out was held for a slap's stagger.</summary>
        public string Reaction => reaction;
        public float Staggered { get; private set; }

        /// <summary>Plays a reaction at an age (seconds into it) about a place (the player, a tank); each frame while it lasts.</summary>
        public void React(string kind, float age, Vector3 at) { reaction = kind; reactionAge = age; reactionAt = at; }

        /// <summary>Ends the reaction: the face, the stars and the cloud go, and the visitor is as they were.</summary>
        public void EndReaction()
        {
            reaction = null;
            if (person != null) person.HideGags();
        }

        /// <summary>A slap's stagger: the walk out waits a moment, only as long as the walk still ends before the rules let the
        /// visitor go (GrowthShop.LeaveSeconds), so nobody vanishes on the way to the door.</summary>
        public void Stagger(float seconds)
        {
            float walk = 0; var from = transform.position; from.y = 0;
            foreach (var p in path) { var to = p; to.y = 0; walk += Vector3.Distance(from, to); from = to; }
            float spare = GrowthShop.LeaveSeconds - walk / Speed - 0.75f - (State == GrowthVisitorState.Leaving && leftAt >= 0 ? clock - leftAt : 0);
            Staggered = Mathf.Clamp(Mathf.Min(seconds, spare), 0, seconds);
            holdUntil = clock + Staggered;
        }

        static Color TypeColour(string type)
        {
            switch (type)
            {
                case "bargain": return new Color(0.98f, 0.78f, 0.37f);
                case "collector": return new Color(0.84f, 0.38f, 0.27f);
                case "premium": return new Color(0.55f, 0.42f, 0.78f);
                case GrowthShop.GuestType: return new Color(0.86f, 0.64f, 0.16f);
                default: return new Color(0.31f, 0.66f, 0.75f);
            }
        }

        public static GrowthVisitorView Create(GrowthVisitor v, Transform parent)
        {
            var root = new GameObject("Visitor " + v.Id + " (" + v.Type + ")");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GrowthVisitorView>();
            view.VisitorId = v.Id;
            view.kind = GrowthPersonLook.KindOf(v.Type);
            view.gait = GrowthPeopleMotion.Gaits[view.kind];
            if (GrowthWorld.LookOn && GrowthPeopleKit.Available)
                view.person = GrowthPerson.Build(GrowthPersonLook.For(view.kind, v.Id), root.transform);
            if (view.person != null) return view;
            if (!visitorMaterials.TryGetValue(v.Type, out var body) || body == null) visitorMaterials[v.Type] = body = ShopWorld.MakeMaterial("GR visitor " + v.Type, TypeColour(v.Type), 0, 0.3f);
            if (!visitorMaterials.TryGetValue("skin", out var skin) || skin == null) visitorMaterials["skin"] = skin = ShopWorld.MakeMaterial("GR visitor skin", new Color(0.93f, 0.78f, 0.64f), 0, 0.3f);
            ShopWorld.Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.62f, 0.32f), body);
            ShopWorld.Shape(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.3f, skin);
            ShopWorld.Shape(root.transform, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0.15f), Vector3.one * 0.07f, skin);
            // GR-11: an exhibition guest wears gold and a rosette.
            if (v.Type == GrowthShop.GuestType)
            {
                if (!visitorMaterials.TryGetValue("rosette", out var rosette) || rosette == null) visitorMaterials["rosette"] = rosette = ShopWorld.MakeMaterial("GR guest rosette", new Color(0.98f, 0.95f, 0.85f), 0, 0.2f);
                ShopWorld.Shape(root.transform, "Rosette", PrimitiveType.Sphere, new Vector3(0.1f, 1.12f, 0.17f), new Vector3(0.14f, 0.14f, 0.05f), rosette);
                ShopWorld.Shape(root.transform, "Hat", PrimitiveType.Cylinder, new Vector3(0, 1.74f, 0), new Vector3(0.26f, 0.05f, 0.26f), body);
            }
            return view;
        }

        /// <summary>Re-routes when the visit's state, browse place or queue spot changes; keeps what the figure shows current
        /// (the outcome, how patient they still are, and what they reserved, to carry home in the bag).</summary>
        public void Follow(GrowthVisitor v, GrowthShop shop, int index)
        {
            outcome = v.Outcome;
            wish = v.Wish; wishGranted = v.WishGranted;
            if (wish != null && bubble == null)
                bubble = GrowthWishBubble.Create(transform, wish, person != null ? HeadTop() : 1.72f, v.Id);
            float patience = v.Type == GrowthShop.GuestType ? GrowthShop.GuestPatience : GrowthShop.QueuePatience;
            impatience = v.State == GrowthVisitorState.Queued ? Mathf.Clamp01(1 - v.Patience / patience) : 0;
            if (v.SpecimenId != 0 && (bought == null || bought.Id != v.SpecimenId))
            {
                var s = shop.Stock.FirstOrDefault(x => x.Id == v.SpecimenId);
                if (s != null) bought = new GrowthSpecimen { Id = s.Id, Species = s.Species, Colour = s.Colour };
            }
            string target = v.ContainerId == null ? null : shop.Container(v.ContainerId)?.Place;
            if (v.State == State && (v.State != GrowthVisitorState.Queued || index == queueIndex) && (v.State != GrowthVisitorState.Browsing || target == place)) return;
            path.Clear();
            switch (v.State)
            {
                // GR-03: a visitor who found nothing walks on to the next container in their plan.
                case GrowthVisitorState.Browsing: path.AddRange(State == GrowthVisitorState.Browsing && place != null ? GrowthWorld.RouteBetween(place, target) : GrowthWorld.RouteTo(target)); place = target; break;
                case GrowthVisitorState.Queued:
                    if (State == GrowthVisitorState.Queued) path.Add(GrowthWorld.QueueSpots[Mathf.Clamp(index, 0, GrowthWorld.QueueSpots.Length - 1)]);
                    else path.AddRange(GrowthWorld.RouteToQueue(place, index));
                    wasQueued = true;
                    break;
                case GrowthVisitorState.Leaving: path.AddRange(GrowthWorld.RouteOut(place, wasQueued)); leftAt = clock; break;
            }
            if (v.State != State) stateAt = clock;
            State = v.State;
            queueIndex = index;
        }

        public void Tick(float dt, bool reduced = false)
        {
            float step = clock < holdUntil ? 0 : Speed * dt, moved = 0;
            while (path.Count > 0 && step > 0)
            {
                Vector3 to = path[0], from = transform.position; to.y = 0; from.y = 0;
                Vector3 d = to - from;
                if (d.magnitude <= step) { transform.position = to; step -= d.magnitude; moved += d.magnitude; path.RemoveAt(0); continue; }
                transform.position = from + d.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 0.25f);
                moved += step;
                step = 0;
            }
            if (path.Count == 0 && State == GrowthVisitorState.Queued)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.back, Vector3.up), 0.2f);
            // GR-18: at a tank, a visitor turns to face it (the walk may come along the wall).
            if (path.Count == 0 && State == GrowthVisitorState.Browsing && place != null && GrowthWorld.Places.TryGetValue(place, out var at))
            {
                Vector3 look = at.Position - transform.position; look.y = 0;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 0.2f);
            }
            // GR-18 wave 4: slapped or laughing it off, they turn to whoever did it while they stand (a slap's stagger, a laugh
            // at a tank), so the face shows; the walk out turns them to the door again.
            if ((reaction == "slapped" || reaction == "laugh") && (path.Count == 0 || clock < holdUntil))
            {
                Vector3 to = reactionAt - transform.position; to.y = 0;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 0.35f);
            }
            Walked += moved;
            clock += dt;
            if (person != null) Animate(dt, moved, reduced);
            // GR-20: the wish shows while they browse and queue; as they leave, a heart if a sale granted it.
            if (bubble != null) bubble.Tick(dt, State == GrowthVisitorState.Browsing || State == GrowthVisitorState.Queued, wishGranted && State == GrowthVisitorState.Leaving, reduced, clock);
        }

        /// <summary>The top of the person's head above their feet (the head joint's height and its head above it).</summary>
        float HeadTop()
        {
            var head = person.Joint("head");
            return head != null ? head.position.y - transform.position.y + person.HeadHeight * 0.95f : 1.72f;
        }

        /// <summary>The person's pose for what the visitor is doing, blended in.</summary>
        void Animate(float dt, float moved, bool reduced)
        {
            bool walking = moved > 0.0005f || path.Count > 0;
            bool leaving = State == GrowthVisitorState.Leaving;
            float sinceLeft = leftAt < 0 ? 999 : clock - leftAt;
            string mouth = null;
            if (leaving && outcome == GrowthVisitOutcome.Bought) ShowBag();
            if (walking)
            {
                phase += moved / gait.Cycle;
                GrowthPeopleMotion.Walk(target, phase, gait);
                GrowthPeopleMotion.HoldArms(target, kind, person.Look.HeldLeft, person.Look.HeldRight);
                Activity = "walk";
                if (leaving)
                {
                    switch (outcome)
                    {
                        case GrowthVisitOutcome.Bought:
                            // Hugging the bag to the chest and a bounce in the step.
                            target.E[GrowthPersonPose.ShoulderR] = new Vector3(-50, 10, 0); target.E[GrowthPersonPose.ElbowR] = new Vector3(-96, 0, 30);
                            if (!reduced && sinceLeft < 2.5f) target.Lift += 0.05f * Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2));
                            mouth = "grin"; Activity = "leave-happy"; break;
                        case GrowthVisitOutcome.NothingAffordable:
                            target.E[GrowthPersonPose.Spine] += new Vector3(10, 0, 0); target.E[GrowthPersonPose.Head] += new Vector3(14, 0, 0);
                            if (sinceLeft < 1.6f) { var shrug = new GrowthPersonPose(); GrowthPeopleMotion.Shrug(shrug, sinceLeft / 1.6f); BlendArms(shrug, 0.8f); }
                            mouth = "o"; Activity = "leave-shrug"; break;
                        case GrowthVisitOutcome.Slapped:
                        case GrowthVisitOutcome.TiredOfWaiting:
                            target.E[GrowthPersonPose.ShoulderL] = new Vector3(-40, -6, 0); target.E[GrowthPersonPose.ShoulderR] = new Vector3(-40, 6, 0);
                            target.E[GrowthPersonPose.ElbowL] = new Vector3(-100, 0, -70); target.E[GrowthPersonPose.ElbowR] = new Vector3(-100, 0, 70);
                            target.E[GrowthPersonPose.Head] += new Vector3(-12, 0, 0);
                            mouth = outcome == GrowthVisitOutcome.Slapped ? "wobble" : "o"; Activity = "leave-huff"; break;
                        case GrowthVisitOutcome.Closed:
                            if (sinceLeft < 2.4f) { target.E[GrowthPersonPose.ShoulderR] = new Vector3(-10, 112, 0); target.E[GrowthPersonPose.ElbowR] = new Vector3(0, 46, 0); target.E[GrowthPersonPose.WristR] = new Vector3(0, 0, -80 + 18 * Mathf.Sin(clock * 12)); person.ShowHand("R", "open"); }
                            Activity = "leave-wave"; break;
                    }
                }
            }
            else if (State == GrowthVisitorState.Browsing)
            {
                GrowthPeopleMotion.Browse(target, (clock - stateAt) / 4f % 1f);
                Activity = "browse";
                if (person.Look.HeldRight == null) person.ShowHand("R", "point");
            }
            else if (State == GrowthVisitorState.Queued && queueIndex == 0)
            {
                GrowthPeopleMotion.Pay(target, (clock - stateAt) / 3f % 1f);
                person.ShowHeld("R", "coin");
                Activity = "pay";
            }
            else if (State == GrowthVisitorState.Queued)
            {
                GrowthPeopleMotion.Wait(target, (clock - stateAt) / 4f % 1f, impatience);
                GrowthPeopleMotion.HoldArms(target, kind, person.Look.HeldLeft, person.Look.HeldRight);
                Activity = "wait";
                if (impatience > 0.7f) mouth = "o";
            }
            else
            {
                GrowthPeopleMotion.Stand(target, clock, 0.2f);
                GrowthPeopleMotion.HoldArms(target, kind, person.Look.HeldLeft, person.Look.HeldRight);
                Activity = leaving && outcome == GrowthVisitOutcome.Bought ? "leave-happy" : "stand";
            }
            if (Activity != "pay" && !(leaving && outcome == GrowthVisitOutcome.Bought)) RestoreHeld();
            // The right hand relaxes unless it points, holds out the coins, carries the bag, or waves.
            bool waving = Activity == "leave-wave" && sinceLeft < 2.4f;
            if (Activity != "browse" && Activity != "pay" && !waving && person.Look.HeldRight == null && !(leaving && outcome == GrowthVisitOutcome.Bought)) person.ShowHand("R", "relaxed");
            if (reaction != null) mouth = ApplyReaction(mouth);
            person.ShowMouth(mouth ?? person.Look.Mouth);
            if (!posed) { pose.CopyFrom(target); posed = true; }
            else pose.Blend(target, 1 - Mathf.Exp(-dt * 12));
            person.Apply(pose);
            if (bagged != null) bagged.Tick(clock, reduced);
        }

        /// <summary>GR-18 wave 4: the reaction over the pose (the head turned towards what it is about), its face and props; the mouth.</summary>
        string ApplyReaction(string mouth)
        {
            float a = Mathf.Max(0, reactionAge);
            var local = transform.InverseTransformPoint(reactionAt);
            float yaw = Mathf.Clamp(-Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70, 70);
            bool talking = Mathf.Repeat(a * 7f, 1f) < 0.5f;
            string own = person.Look.Mouth;
            // What another reaction showed goes (the culprit's cloud once the culprit points at the fish).
            if (reaction != "slapped" && reaction != "laugh") { person.Gag("head__mark-slap", false); person.Gag("head__eyes-squeeze", false); person.Gag("gag__stars", false); }
            if (reaction != "culprit") person.Gag("gag__cloud", false);
            switch (reaction)
            {
                case "slapped":
                    GrowthPeopleMotion.Slapped(target, a);
                    person.Gag("head__mark-slap", true);
                    person.Gag("head__eyes-squeeze", a < 0.9f);
                    Stars(a < 1.9f, a);
                    person.ShowHand("L", "open"); person.ShowHand("R", "open");
                    return a < 0.9f ? "o" : "wobble";
                case "laugh":
                    person.Gag("head__mark-slap", true);
                    person.Gag("head__eyes-squeeze", a < 0.45f);
                    Stars(a < 0.45f, a);
                    if (a < 0.45f) { GrowthPeopleMotion.Slapped(target, a); return "o"; }
                    GrowthPeopleMotion.Laugh(target, a - 0.45f);
                    return "grin";
                case "culprit":
                    GrowthPeopleMotion.Innocent(target, a);
                    Cloud(a);
                    return "o";
                case "sniff":
                    GrowthPeopleMotion.Sniff(target, a, yaw);
                    return "o";
                case "blame":
                    if (a < 0.8f) { GrowthPeopleMotion.Sniff(target, a, yaw); return "o"; }
                    GrowthPeopleMotion.Accuse(target, a - 0.8f, yaw);
                    if (person.Look.HeldRight == null) person.ShowHand("R", "point");
                    return talking ? "o" : own;
                case "complain":
                    GrowthPeopleMotion.Complain(target, a, yaw);
                    if (person.Look.HeldRight == null) person.ShowHand("R", a < 1.1f ? "point" : "open");
                    return talking ? "o" : own;
                case "fishface":
                    GrowthPeopleMotion.FishFace(target, a, yaw);
                    if (person.Look.HeldRight == null) person.ShowHand("R", "open");
                    if (person.Look.HeldLeft == null) person.ShowHand("L", "open");
                    return "o";
            }
            return mouth;
        }

        /// <summary>The dizzy stars circling over the head, spinning and bobbing.</summary>
        void Stars(bool on, float a)
        {
            // GR-24: over a hat's crown, not sunk into it (a top hat is tall).
            float over = person.Look.Hat == null ? 1.16f : person.Look.Hat == "tophat" ? 1.62f : 1.36f;
            person.Gag("gag__stars", on, at: new Vector3(0, over, 0));
            var holder = person.GagHolder("gag__stars");
            if (!on || holder == null) return;
            holder.localRotation = Quaternion.Euler(8 * Mathf.Sin(a * 5), a * 320f, 0);
            holder.localPosition = new Vector3(0, person.HeadKit * (over + 0.03f * Mathf.Sin(a * 9)), 0);
        }

        /// <summary>The culprit's puff of a cloud behind them, swelling, rising and fading.</summary>
        void Cloud(float a)
        {
            const float last = 1.8f;
            bool on = a < last;
            // GR-24: behind the seat, rising a little (it sat flat on the middle of the back).
            person.Gag("gag__cloud", on, joint: "hips", at: new Vector3(0, -0.08f, -0.3f));
            var holder = person.GagHolder("gag__cloud");
            if (!on || holder == null) return;
            float s = Mathf.Sin(Mathf.PI * Mathf.Clamp01(a / last));
            holder.localScale = Vector3.one * (0.4f + 0.9f * s);
            holder.localPosition = new Vector3(0.04f * Mathf.Sin(a * 4), -0.08f + 0.2f * a / last, -0.3f - 0.12f * a / last);
        }

        void BlendArms(GrowthPersonPose from, float w)
        {
            foreach (int i in new[] { GrowthPersonPose.ShoulderL, GrowthPersonPose.ElbowL, GrowthPersonPose.ShoulderR, GrowthPersonPose.ElbowR, GrowthPersonPose.Head })
                target.E[i] = Vector3.Lerp(target.E[i], from.E[i], w);
        }

        void RestoreHeld()
        {
            var right = person.Look.HeldRight;
            if (person.HeldShown.Contains("R.coin") || person.HeldShown.Contains("R.creature_bag")) person.ShowHeld("R", right);
        }

        /// <summary>The creature they bought, in the shop's water bag in their right hand (a display of it: the sale is done).</summary>
        void ShowBag()
        {
            if (person.HeldShown.Contains("R.creature_bag")) return;
            person.ShowHeld("R", "creature_bag");
            var bag = person.BagIn("R");
            if (bag == null || bought == null || bagged != null) return;
            var mf = bag.GetComponentInChildren<MeshFilter>();
            bagged = GrowthCreatureView.Create(bought, mf ? mf.transform : bag);
            var centre = mf && mf.sharedMesh ? mf.sharedMesh.bounds.center : Vector3.zero;
            bagged.transform.localPosition = centre + Vector3.down * (mf && mf.sharedMesh ? mf.sharedMesh.bounds.extents.y * 0.25f : 0);
            float world = Mathf.Max(0.001f, bagged.transform.lossyScale.x);
            float fit = Mathf.Min(1f, 0.11f / Mathf.Max(0.01f, bagged.Length));
            bagged.transform.localScale = Vector3.one * (bagged.transform.localScale.x / world * fit);
            bagged.transform.localRotation = Quaternion.Euler(0, 90, 0);
            foreach (var c in bagged.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
    }
}
