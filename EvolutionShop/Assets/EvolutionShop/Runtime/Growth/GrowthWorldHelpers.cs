using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-09's helpers in the greybox (GR.10): the staff board on unit A's north wall, west of the back room's door, where
    /// helpers are hired and trained; and a figure for each helper at work. The user chose (2026-10-23) figures at work:
    /// their work runs on timers in the rules (GrowthShop's helper steps), and each figure follows its helper's step along
    /// the same walk through the doorways, carrying the creature or the dose in hand. Pip stands behind the counter; a helper
    /// who is home and has nothing in hand is not shown. Plain shapes only: the staff wear an apron and a cap, so they read
    /// apart from visitors. Each figure is focusable (E opens the helper's card) but never in the way.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        public static readonly Vector3 StaffBoardPosition = new Vector3(-0.45f, 1.6f, 3.86f);

        sealed class HelperFigure
        {
            public Transform Root, Hand;
            public GrowthCreatureView Carried;
            public Transform DoseBlob;
            public Vector3 Last;
        }

        readonly Dictionary<string, HelperFigure> figures = new Dictionary<string, HelperFigure>();
        Transform staffRoot, staffBoard;
        readonly Dictionary<string, Material> staffMaterials = new Dictionary<string, Material>();

        public bool StaffBoardStanding => staffBoard != null;
        /// <summary>Validation: whether a helper's figure shows, where it stands, and what it carries.</summary>
        public bool FigureShowing(string id) => figures.TryGetValue(id, out var f) && f.Root.gameObject.activeSelf;
        public Vector3 FigurePosition(string id) => figures.TryGetValue(id, out var f) ? f.Root.position : Vector3.zero;
        public int FigureCarrying(string id) => figures.TryGetValue(id, out var f) && f.Carried != null && f.Carried.gameObject.activeSelf ? f.Carried.SpecimenId : 0;
        public bool FigureHoldsDose(string id) => figures.TryGetValue(id, out var f) && f.DoseBlob.gameObject.activeSelf;

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
            var body = StaffMaterial(id); var apron = StaffMaterial("apron"); var skin = StaffMaterial("skin");
            ShopWorld.Shape(root, "Body", PrimitiveType.Capsule, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.62f, 0.32f), body);
            Box(root, "Apron", new Vector3(0, 0.78f, 0.15f), new Vector3(0.36f, 0.62f, 0.04f), apron);
            ShopWorld.Shape(root, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.3f, skin);
            ShopWorld.Shape(root, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0.15f), Vector3.one * 0.07f, skin);
            if (id == "moss") Box(root, "Goggles", new Vector3(0, 1.6f, 0.13f), new Vector3(0.26f, 0.07f, 0.06f), stand);
            else Box(root, "Cap", new Vector3(0, 1.7f, 0.04f), new Vector3(0.3f, 0.06f, 0.36f), apron);
            var hand = new GameObject("Hand").transform; hand.SetParent(root, false);
            hand.localPosition = new Vector3(0, 1.0f, 0.34f);
            var dose = ShopWorld.Shape(hand, "Dose", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.09f, paper);
            dose.gameObject.SetActive(false);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.9f, 0); col.size = new Vector3(0.6f, 1.8f, 0.5f); col.isTrigger = true; // focus, not a wall
            Target(root.gameObject, GrowthTargetKind.Helper, id);
            return new HelperFigure { Root = root, Hand = hand, DoseBlob = dose };
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
                if (moved.sqrMagnitude > 1e-6f) f.Root.localRotation = Quaternion.LookRotation(moved.normalized, Vector3.up);
                else if (h.Id == "pip") f.Root.localRotation = Quaternion.identity; // facing the queue across the counter
                f.Root.localPosition = f.Last = position;
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

        /// <summary>Where a helper is: along their step's walk (the moment at its end to pick up or put down is spent standing).</summary>
        static (float x, float z) WhereIs(GrowthHelper h)
        {
            if (h.Step == null || h.StepSeconds <= 0) return h.Spot;
            float walk = Mathf.Max(0.01f, h.StepSeconds - GrowthWalk.Handle);
            return GrowthWalk.Along(h.Spot, h.Target, h.Progress / walk);
        }

        void AnimateHelpers(float t)
        {
            foreach (var f in figures.Values)
                if (f.Carried != null && f.Root.gameObject.activeSelf) f.Carried.Tick(t);
        }
    }
}
