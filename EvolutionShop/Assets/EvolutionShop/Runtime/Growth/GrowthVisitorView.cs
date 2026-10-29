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
    /// routes': the figure only shows it.
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
            float step = Speed * dt, moved = 0;
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
            Walked += moved;
            clock += dt;
            if (person != null) Animate(dt, moved, reduced);
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
                        case GrowthVisitOutcome.TiredOfWaiting:
                            target.E[GrowthPersonPose.ShoulderL] = new Vector3(-40, -6, 0); target.E[GrowthPersonPose.ShoulderR] = new Vector3(-40, 6, 0);
                            target.E[GrowthPersonPose.ElbowL] = new Vector3(-100, 0, -70); target.E[GrowthPersonPose.ElbowR] = new Vector3(-100, 0, 70);
                            target.E[GrowthPersonPose.Head] += new Vector3(-12, 0, 0);
                            mouth = "o"; Activity = "leave-huff"; break;
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
            person.ShowMouth(mouth ?? person.Look.Mouth);
            if (!posed) { pose.CopyFrom(target); posed = true; }
            else pose.Blend(target, 1 - Mathf.Exp(-dt * 12));
            person.Apply(pose);
            if (bagged != null) bagged.Tick(clock, reduced);
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
