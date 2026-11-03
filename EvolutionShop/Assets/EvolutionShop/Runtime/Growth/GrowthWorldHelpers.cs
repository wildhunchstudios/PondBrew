using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-09's helpers in the greybox (GR.10): the staff board on unit A's north wall, west of the back room's door, where
    /// helpers are hired and trained; and a figure for each helper at work. The user chose (2026-10-23) figures at work:
    /// their work runs on timers in the rules (GrowthShop's helper steps), and each figure follows its helper's step along
    /// the same walk through the doorways, carrying the creature or the dose in hand. Pip stands behind the counter; a helper
    /// who is home and has nothing in hand is not shown. Each figure is focusable (E opens the helper's card) but never in
    /// the way. GR-18 wave 2: in the look, each helper is a person from the people kit (<see cref="GrowthPerson"/>), the
    /// board's cast: Pip in the pink polo and cap with the shop's apron, Nori in the green coverall and bandana carrying the
    /// shop's jar (the creatures Nori carries swim in it), Moss in the lab coat and goggles with the pipette (in the colour of
    /// the culture Moss carries) and the clipboard; each walks in their own way and, at the moment a step picks up or puts
    /// down, does the board's work: Pip's sweep of the scanner and wave at the till, Nori's reach over the rim, Moss's drops.
    /// Without the look (-evolutionGrowthPlain) or the kit, the greybox's capsule in the helper's colour, apron and cap.
    /// GR-18 wave 4 (§6.7): after a job, a helper with nothing to do may do a tiny victory dance (<see cref="HelperGag"/>,
    /// GrowthGags' clock), music notes over their head; it stops the moment the rules give them a step.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        public static readonly Vector3 StaffBoardPosition = At(-0.45f, 1.6f, 3.86f);

        sealed class HelperFigure
        {
            public Transform Root, Hand;
            public GrowthCreatureView Carried;
            public Transform DoseBlob;
            public Vector3 Last;
            // GR-18 wave 2: the person, and what they are doing.
            public GrowthPerson Person;
            public string Kind, Activity = "stand";
            public GrowthPeopleMotion.Gait Gait;
            public readonly GrowthPersonPose Target = new GrowthPersonPose(), Pose = new GrowthPersonPose();
            public bool Posed, HasDose;
            public float Moved, Phase, Clock, WorkClock;
            public string Step;
            public int Carrying;
            public readonly List<GrowthCreatureView> InJar = new List<GrowthCreatureView>();
            // GR-18 wave 4: the silly moment the helper plays (a dance), and how far into it (-1 with none).
            public string Gag;
            public float GagAge = -1;
        }

        readonly Dictionary<string, HelperFigure> figures = new Dictionary<string, HelperFigure>();
        Transform staffRoot, staffBoard;
        readonly Dictionary<string, Material> staffMaterials = new Dictionary<string, Material>();

        public bool StaffBoardStanding => staffBoard != null;
        /// <summary>Validation: whether a helper's figure shows, where it stands, and what it carries.</summary>
        public bool FigureShowing(string id) => figures.TryGetValue(id, out var f) && f.Root.gameObject.activeSelf;
        public Vector3 FigurePosition(string id) => figures.TryGetValue(id, out var f) ? f.Root.position : Vector3.zero;
        public int FigureCarrying(string id) => figures.TryGetValue(id, out var f) ? (f.Person != null ? f.Carrying : f.Carried != null && f.Carried.gameObject.activeSelf ? f.Carried.SpecimenId : 0) : 0;
        public bool FigureHoldsDose(string id) => figures.TryGetValue(id, out var f) && (f.Person != null ? f.HasDose : f.DoseBlob.gameObject.activeSelf);
        /// <summary>Validation (GR-18): the person drawn for a helper (null in the greybox look), what they are doing (walk, work,
        /// stand), and the creatures swimming in Nori's (or Moss's) jar.</summary>
        public GrowthPerson FigurePerson(string id) => figures.TryGetValue(id, out var f) ? f.Person : null;
        public string FigureActivity(string id) => figures.TryGetValue(id, out var f) ? f.Activity : null;
        public int FigureInJar(string id) => figures.TryGetValue(id, out var f) ? f.InJar.Count : 0;

        /// <summary>GR-18 wave 4: a helper's silly moment ("dance") at an age, or none (null): played only while they stand
        /// with no step to do; the rules never wait for it.</summary>
        public void HelperGag(string id, string gag, float age)
        {
            if (id == null || !figures.TryGetValue(id, out var f)) return;
            f.Gag = gag; f.GagAge = gag == null ? -1 : age;
            if (gag == null && f.Person != null) f.Person.HideGags();
        }

        static Color HelperColour(string id) =>
            id == "pip" ? new Color(0.93f, 0.47f, 0.62f) : id == "nori" ? new Color(0.36f, 0.66f, 0.36f) : new Color(0.90f, 0.92f, 0.94f);

        /// <summary>The staff board: a board with a card for each helper in their colour, on the north wall facing the shop.</summary>
        void StaffBoard()
        {
            staffRoot = new GameObject("Staff").transform; staffRoot.SetParent(transform, false);
            staffBoard = new GameObject("Staff board").transform; staffBoard.SetParent(transform, false);
            staffBoard.localPosition = StaffBoardPosition; // its front (local -z) faces south, into unit A
            Box(staffBoard, "Board", Vector3.zero, new Vector3(1.0f, 0.7f, 0.05f), counterMat, true);
            Box(staffBoard, "Heading", new Vector3(0, 0.25f, -0.03f), new Vector3(0.6f, 0.1f, 0.01f), paper);
            int i = 0;
            foreach (var def in GrowthContent.Helpers)
            {
                float x = -0.3f + 0.3f * i++;
                Box(staffBoard, def.Name + "'s card", new Vector3(x, -0.06f, -0.03f), new Vector3(0.22f, 0.3f, 0.01f), paper);
                Box(staffBoard, def.Name + "'s stripe", new Vector3(x, 0.05f, -0.04f), new Vector3(0.22f, 0.06f, 0.01f), StaffMaterial(def.Id));
            }
            Target(staffBoard.gameObject, GrowthTargetKind.StaffBoard, "staff");
        }

        Material StaffMaterial(string id)
        {
            if (!staffMaterials.TryGetValue(id, out var m) || m == null)
                staffMaterials[id] = m = id == "apron" ? ShopWorld.MakeMaterial("GR staff apron", new Color(0.21f, 0.37f, 0.35f), 0, 0.3f) :
                    id == "skin" ? ShopWorld.MakeMaterial("GR staff skin", new Color(0.86f, 0.70f, 0.56f), 0, 0.3f) :
                    ShopWorld.MakeMaterial("GR helper " + id, HelperColour(id), 0, 0.3f);
            return m;
        }

        HelperFigure BuildFigure(string id)
        {
            var root = new GameObject("Helper " + id).transform; root.SetParent(staffRoot, false);
            var hand = new GameObject("Hand").transform; hand.SetParent(root, false);
            hand.localPosition = new Vector3(0, 1.0f, 0.34f);
            var dose = ShopWorld.Shape(hand, "Dose", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.09f, paper);
            dose.gameObject.SetActive(false);
            var figure = new HelperFigure { Root = root, Hand = hand, DoseBlob = dose, Kind = id };
            if (LookOn && GrowthPeopleKit.Available && GrowthPeopleKit.Kind(id) != null)
            {
                figure.Person = GrowthPerson.Build(GrowthPersonLook.Helper(id), root);
                figure.Gait = GrowthPeopleMotion.Gaits[id];
                // Nori and Moss start the day in the back room facing the shop through its door; Pip faces the queue.
                if (id != "pip") root.localRotation = Quaternion.Euler(0, 180, 0);
            }
            if (figure.Person == null)
            {
                var body = StaffMaterial(id); var apron = StaffMaterial("apron"); var skin = StaffMaterial("skin");
                ShopWorld.Shape(root, "Body", PrimitiveType.Capsule, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.62f, 0.32f), body);
                Box(root, "Apron", new Vector3(0, 0.78f, 0.15f), new Vector3(0.36f, 0.62f, 0.04f), apron);
                ShopWorld.Shape(root, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.3f, skin);
                ShopWorld.Shape(root, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0.15f), Vector3.one * 0.07f, skin);
                if (id == "moss") Box(root, "Goggles", new Vector3(0, 1.6f, 0.13f), new Vector3(0.26f, 0.07f, 0.06f), stand);
                else Box(root, "Cap", new Vector3(0, 1.7f, 0.04f), new Vector3(0.3f, 0.06f, 0.36f), apron);
            }
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.9f, 0); col.size = new Vector3(0.6f, 1.8f, 0.5f); col.isTrigger = true; // focus, not a wall
            Target(root.gameObject, GrowthTargetKind.Helper, id);
            return figure;
        }

        void ResetHelpers()
        {
            foreach (var f in figures.Values) if (f.Root) Destroy(f.Root.gameObject);
            figures.Clear();
        }

        // ---------------------------------------------------------------- following the shop

        /// <summary>Each hired helper's figure: shown at work (or with something in hand), at their place along the step they are on.</summary>
        void SyncHelpers(GrowthShop shop)
        {
            foreach (var h in shop.Staff.Values)
            {
                if (!figures.TryGetValue(h.Id, out var f)) figures[h.Id] = f = BuildFigure(h.Id);
                bool show = h.OnDuty || h.Working || h.Hands.Count > 0 || h.Dose != null;
                if (f.Root.gameObject.activeSelf != show) f.Root.gameObject.SetActive(show);
                if (!show) continue;
                var at = WhereIs(h);
                var position = new Vector3(at.x, 0, at.z);
                var moved = position - f.Last;
                moved.y = 0;
                if (f.Person != null)
                {
                    // A person turns into their walk, and Pip back to the queue across the counter.
                    // (A jump, when hired or the shop loads, keeps their facing: it is not a walk.)
                    if (moved.sqrMagnitude > 1e-6f && moved.magnitude < 1f) f.Root.localRotation = Quaternion.Slerp(f.Root.localRotation, Quaternion.LookRotation(moved.normalized, Vector3.up), 0.3f);
                    else if (moved.sqrMagnitude <= 1e-6f && h.Id == "pip") f.Root.localRotation = Quaternion.Slerp(f.Root.localRotation, Quaternion.identity, 0.2f);
                    f.Moved += moved.magnitude < 1f ? moved.magnitude : 0; // a jump (hired, or the shop loaded) is not a walk
                }
                else if (moved.sqrMagnitude > 1e-6f) f.Root.localRotation = Quaternion.LookRotation(moved.normalized, Vector3.up);
                else if (h.Id == "pip") f.Root.localRotation = Quaternion.identity; // facing the queue across the counter
                f.Root.localPosition = f.Last = position;
                // At the end of a step's walk, the moment to pick up or put down (GrowthWalk.Handle) is the work.
                float walk = h.Step == null ? 0 : Mathf.Max(0.01f, h.StepSeconds - GrowthWalk.Handle);
                f.Step = h.Step;
                f.WorkClock = h.Step == null ? -1 : h.Step == "serve" ? h.Progress : h.Progress - walk;
                if (f.Person != null) { SyncHelperHands(f, h); continue; }
                // What is in hand: the first creature carried, or a dose of culture.
                var carried = h.Hands.Count > 0 ? h.Hands[0] : null;
                if (carried == null && f.Carried != null) { Destroy(f.Carried.gameObject); f.Carried = null; }
                if (carried != null && (f.Carried == null || f.Carried.SpecimenId != carried.Id))
                {
                    if (f.Carried != null) Destroy(f.Carried.gameObject);
                    f.Carried = GrowthCreatureView.Create(carried, f.Hand);
                    f.Carried.transform.localPosition = Vector3.zero;
                }
                bool dose = h.Dose != null && culture.ContainsKey(h.Dose);
                if (f.DoseBlob.gameObject.activeSelf != dose) f.DoseBlob.gameObject.SetActive(dose);
                if (dose)
                {
                    var r = f.DoseBlob.GetComponent<Renderer>();
                    if (r.sharedMaterial != culture[h.Dose]) r.sharedMaterial = culture[h.Dose];
                }
            }
            foreach (var id in new List<string>(figures.Keys))
                if (!shop.Staff.ContainsKey(id)) { Destroy(figures[id].Root.gameObject); figures.Remove(id); }
        }

        /// <summary>GR-18: what a helper person holds. The creatures carried swim in the shop's jar (Nori always carries it; Moss
        /// takes it in place of the pipette and clipboard while carrying a parent); a dose colours Moss's pipette.</summary>
        void SyncHelperHands(HelperFigure f, GrowthHelper h)
        {
            var p = f.Person;
            bool carrying = h.Hands.Count > 0;
            f.Carrying = carrying ? h.Hands[0].Id : 0;
            if (f.Kind == "moss")
            {
                p.ShowHeld("R", carrying ? "jar" : "pipette");
                p.ShowHeld("L", carrying ? null : "clipboard");
                if (carrying) p.ShowHand("L", "grip");
            }
            else if (f.Kind == "nori") p.ShowHand("L", "grip");
            // The creatures in the jar: the ones in hand, each swimming there (a view of them: they are the rules').
            var jar = p.HeldIn("R", "jar");
            for (int i = f.InJar.Count - 1; i >= 0; i--)
                if (!f.InJar[i] || !h.Hands.Exists(s => s.Id == f.InJar[i].SpecimenId)) { if (f.InJar[i]) Destroy(f.InJar[i].gameObject); f.InJar.RemoveAt(i); }
            if (jar != null)
                foreach (var s in h.Hands)
                    if (!f.InJar.Exists(v => v.SpecimenId == s.Id)) f.InJar.Add(InJar(jar, s, f.InJar.Count));
            // A dose of culture fills the pipette in its colour; without one it is rinsed pale.
            f.HasDose = h.Dose != null && GrowthContent.Culture(h.Dose) != null;
            if (f.Kind == "moss") p.SetColour("DOSE", f.HasDose ? GrowthContent.Culture(h.Dose).Hex : GrowthPersonLook.EmptyDose);
        }

        /// <summary>A creature put in the jar's water, small enough to swim there, a little apart from the others.</summary>
        static GrowthCreatureView InJar(Transform jar, GrowthSpecimen s, int index)
        {
            var mf = jar.GetComponentInChildren<MeshFilter>();
            var holder = mf ? mf.transform : jar;
            var view = GrowthCreatureView.Create(s, holder);
            var centre = mf && mf.sharedMesh ? mf.sharedMesh.bounds.center : Vector3.zero;
            float r = mf && mf.sharedMesh ? Mathf.Min(mf.sharedMesh.bounds.extents.x, mf.sharedMesh.bounds.extents.z) : 0.1f;
            view.transform.localPosition = centre + new Vector3(Mathf.Cos(index * 2.1f), -0.2f + 0.25f * index, Mathf.Sin(index * 2.1f)) * r * 0.35f;
            float world = Mathf.Max(0.001f, view.transform.lossyScale.x);
            float fit = Mathf.Min(1f, 0.12f / Mathf.Max(0.01f, view.Length));
            view.transform.localScale = Vector3.one * (view.transform.localScale.x / world * fit);
            view.transform.localRotation = Quaternion.Euler(0, 90 + 70 * index, 0);
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Destroy(c);
            return view;
        }

        /// <summary>Where a helper is: along their step's walk (the moment at its end to pick up or put down is spent standing).</summary>
        static (float x, float z) WhereIs(GrowthHelper h)
        {
            if (h.Step == null || h.StepSeconds <= 0) return h.Spot;
            float walk = Mathf.Max(0.01f, h.StepSeconds - GrowthWalk.Handle);
            return GrowthWalk.Along(h.Spot, h.Target, h.Progress / walk);
        }

        void AnimateHelpers(float t, float dt, bool reduced)
        {
            foreach (var f in figures.Values)
            {
                if (!f.Root.gameObject.activeSelf) { f.Moved = 0; continue; }
                if (f.Person != null) AnimateHelper(f, dt, reduced);
                if (f.Carried != null) f.Carried.Tick(t);
                foreach (var v in f.InJar) if (v) v.Tick(t, reduced);
            }
        }

        /// <summary>GR-18: a helper person's pose: walking their step (the step matched to the distance), at work at its end, or standing.</summary>
        void AnimateHelper(HelperFigure f, float dt, bool reduced)
        {
            var p = f.Person; var look = p.Look;
            float moved = f.Moved; f.Moved = 0;
            f.Clock += dt;
            var heldLeft = p.HeldShown.Contains("L.clipboard") ? "clipboard" : null;
            var heldRight = p.HeldShown.Contains("R.jar") ? "jar" : p.HeldShown.Contains("R.pipette") ? "pipette" : null;
            if (moved > 0.0005f)
            {
                f.Phase += moved / f.Gait.Cycle;
                GrowthPeopleMotion.Walk(f.Target, f.Phase, f.Gait);
                if (!f.Gait.Carry) GrowthPeopleMotion.HoldArms(f.Target, f.Kind, heldLeft, heldRight);
                else if (heldRight != "jar") GrowthPeopleMotion.HoldArms(f.Target, f.Kind, heldLeft, heldRight);
                f.Activity = "walk";
            }
            else if (f.Gag == "dance" && f.GagAge >= 0 && f.Step == null)
            {
                // GR-18 wave 4: the job done and nothing to do: a tiny victory dance, two notes rising over the head.
                GrowthPeopleMotion.Dance(f.Target, f.GagAge);
                if (reduced) { f.Target.Lift *= 0.3f; }
                for (int i = 0; i < 2; i++)
                {
                    p.Gag("note" + i, true, "gag__note", "head", new Vector3(i == 0 ? 0.32f : -0.3f, 1.1f, 0));
                    var holder = p.GagHolder("note" + i);
                    if (holder == null) continue;
                    float u = Mathf.Repeat(f.GagAge * 0.7f + i * 0.5f, 1f);
                    holder.localPosition = new Vector3((i == 0 ? 0.32f : -0.3f) * p.HeadKit + 0.03f * Mathf.Sin(f.GagAge * 6 + i), p.HeadKit * (1.05f + 0.8f * u), 0);
                    holder.localScale = Vector3.one * (1.6f * Mathf.Sin(Mathf.PI * u));
                    holder.localRotation = Quaternion.Euler(0, 0, 15 * Mathf.Sin(f.GagAge * 5 + i));
                }
                f.Activity = "dance";
            }
            else if (f.Step != null && f.WorkClock >= 0)
            {
                // Pip bags the order through the serve; Nori and Moss reach in at the step's end (0.6 s, one reach or three drops).
                if (f.Kind == "pip") GrowthPeopleMotion.WorkPip(f.Target, reduced ? 0.1f : f.WorkClock / 1.6f % 1f);
                else if (heldRight == "jar") GrowthPeopleMotion.WorkNori(f.Target, Mathf.Clamp01(f.WorkClock / GrowthWalk.Handle));
                else GrowthPeopleMotion.WorkMoss(f.Target, Mathf.Clamp01(f.WorkClock / GrowthWalk.Handle));
                f.Activity = "work";
            }
            else
            {
                GrowthPeopleMotion.Stand(f.Target, f.Clock, f.Kind == "pip" ? 0.5f : 0);
                GrowthPeopleMotion.HoldArms(f.Target, f.Kind, heldLeft, heldRight);
                f.Activity = "stand";
            }
            if (f.Activity != "dance" && p.GagShown("note0")) { p.Gag("note0", false); p.Gag("note1", false); }
            // Pip's hands: the scanner's grip on the left and an open right hand to wave, at the till.
            if (f.Kind == "pip") { p.ShowHand("L", f.Activity == "work" ? "grip" : "relaxed"); p.ShowHand("R", f.Activity == "work" ? "open" : "relaxed"); }
            if (!f.Posed) { f.Pose.CopyFrom(f.Target); f.Posed = true; }
            else f.Pose.Blend(f.Target, 1 - Mathf.Exp(-dt * 12));
            p.Apply(f.Pose);
        }
    }
}
