using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-02's placeholder creature at its size class (GR.5: a small one is 6-9 cm, a medium 12-18, a large 25-40), with a
    /// head 1.3 times the usual size for small species (GR.14 readability), eyes, a tail and a fin; colour from its recipe's
    /// cultures. Each of the Brookkin founders has a shape of its own so they can be told apart in a school.
    /// </summary>
    public sealed class GrowthCreatureView : MonoBehaviour
    {
        public int SpecimenId { get; private set; }
        public string Species { get; private set; }
        public float Length { get; private set; }
        public bool Reserved
        {
            get => reserved;
            set { if (reserved == value) return; reserved = value; if (reservedTag) reservedTag.gameObject.SetActive(value); }
        }
        public bool Highlighted
        {
            get => highlighted;
            set { highlighted = value; transform.localScale = Vector3.one * (value ? 1.35f : 1f); }
        }
        Transform tail, reservedTag;
        bool reserved, highlighted;
        float wag;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static float LengthOf(SizeClass size) => size == SizeClass.Small ? 0.075f : size == SizeClass.Medium ? 0.15f : 0.32f;

        /// <summary>The species' colour: its recipe's cultures mixed by dose, or its family's lead culture shaded by its breeding value.</summary>
        public static Color ColourOf(SpeciesDef s)
        {
            if (s.Recipe != null)
            {
                Color sum = Color.black; int doses = 0;
                for (int i = 0; i < s.Recipe.Length; i++)
                {
                    if (s.Recipe[i] == 0) continue;
                    ColorUtility.TryParseHtmlString(GrowthContent.Cultures[i].Hex, out var c);
                    sum += c * s.Recipe[i]; doses += s.Recipe[i];
                }
                if (doses == 0) return Color.grey;
                // Averaged cultures go muddy (red and blue make grey), so the mix keeps a clear hue: more saturated, never dark.
                Color.RGBToHSV(sum / doses, out float mh, out float ms, out float mv);
                return Color.HSVToRGB(mh, Mathf.Clamp01(ms * 1.7f + 0.12f), Mathf.Max(mv, 0.72f));
            }
            var lead = GrowthContent.Cultures[GrowthContent.CultureIndex(GrowthContent.Family(s.Family).LeadCulture)];
            ColorUtility.TryParseHtmlString(lead.Hex, out var l);
            Color.RGBToHSV(l, out float h, out float sat, out float v);
            return Color.HSVToRGB(Mathf.Repeat(h + (s.BreedingValue % 7 - 3) * 0.025f, 1), sat * 0.9f, Mathf.Clamp01(v * 0.95f));
        }

        static Material Mat(string key, Color c)
        {
            if (!materials.TryGetValue(key, out var m) || m == null) materials[key] = m = ShopWorld.MakeMaterial("GR creature " + key, c, 0, 0.55f);
            return m;
        }

        public static GrowthCreatureView Create(GrowthSpecimen specimen, Transform parent)
        {
            var def = specimen.Def;
            var root = new GameObject(def.Name + " #" + specimen.Id);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GrowthCreatureView>();
            view.SpecimenId = specimen.Id;
            view.Species = specimen.Species;
            float L = view.Length = LengthOf(def.Size);
            Color body = ColourOf(def), belly = Color.Lerp(body, Color.white, 0.55f), fin = Color.Lerp(body, Color.black, 0.25f);
            var bodyMat = Mat(def.Id + "-body", body); var bellyMat = Mat(def.Id + "-belly", belly); var finMat = Mat(def.Id + "-fin", fin);
            var white = Mat("eye", Color.white); var black = Mat("pupil", new Color(0.08f, 0.1f, 0.12f));
            float head = def.Size == SizeClass.Small ? 1.3f : 1f;
            // Shape by species: Bubblebelly is round, Ribbonfin long with a ribbon tail, the rest a plain teardrop.
            Vector3 bodyScale = new Vector3(0.42f, 0.42f, 0.72f) * L, tailScale = new Vector3(0.04f, 0.34f, 0.3f) * L;
            if (def.Id == "bubblebelly") bodyScale = new Vector3(0.58f, 0.56f, 0.66f) * L;
            if (def.Id == "ribbonfin") { bodyScale = new Vector3(0.34f, 0.36f, 0.8f) * L; tailScale = new Vector3(0.03f, 0.26f, 0.62f) * L; }
            ShopWorld.Shape(root.transform, "Body", PrimitiveType.Sphere, Vector3.zero, bodyScale, bodyMat);
            ShopWorld.Shape(root.transform, "Belly", PrimitiveType.Sphere, new Vector3(0, -0.08f * L, 0.02f * L), Vector3.Scale(bodyScale, new Vector3(0.8f, 0.6f, 0.85f)), bellyMat);
            var h = ShopWorld.Shape(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 0.03f * L, 0.36f * L), Vector3.one * (0.42f * L * head), bodyMat);
            float eye = 0.15f * L * head;
            foreach (int side in new[] { -1, 1 })
            {
                var e = ShopWorld.Shape(root.transform, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.15f * L * head, 0.09f * L * head, 0.44f * L + 0.08f * L * (head - 1)), Vector3.one * eye, white);
                ShopWorld.Shape(e, "Pupil", PrimitiveType.Sphere, new Vector3(side * 0.18f, 0.05f, 0.32f), Vector3.one * 0.55f, black);
            }
            view.tail = new GameObject("Tail pivot").transform;
            view.tail.SetParent(root.transform, false);
            view.tail.localPosition = new Vector3(0, 0, -0.34f * L);
            ShopWorld.Shape(view.tail, "Tail", PrimitiveType.Cube, new Vector3(0, 0, -tailScale.z * 0.5f), tailScale, finMat);
            ShopWorld.Shape(root.transform, "Fin", PrimitiveType.Cube, new Vector3(0, 0.22f * L, -0.05f * L), new Vector3(0.03f, 0.16f, 0.22f) * L, finMat);
            // A small honey tag over a creature a visitor is buying.
            view.reservedTag = ShopWorld.Shape(root.transform, "Reserved tag", PrimitiveType.Cube, new Vector3(0, 0.45f * L, 0), new Vector3(0.18f, 0.12f, 0.02f) * L * 1.6f, Mat("reserved", new Color(0.98f, 0.78f, 0.37f)));
            view.reservedTag.gameObject.SetActive(false);
            view.wag = specimen.Id * 0.73f;
            return view;
        }

        public void Tick(float t)
        {
            if (tail) tail.localRotation = Quaternion.Euler(0, 22f * Mathf.Sin(t * 7f + wag), 0);
        }
    }

    /// <summary>A greybox visitor (a coloured capsule by type) walking the shop's routes as the Core visit unfolds.</summary>
    public sealed class GrowthVisitorView : MonoBehaviour
    {
        public int VisitorId { get; private set; }
        public GrowthVisitorState State { get; private set; } = (GrowthVisitorState)(-1);
        public bool Arrived => path.Count == 0;
        readonly List<Vector3> path = new List<Vector3>();
        string place;
        int queueIndex = -1;
        bool wasQueued;
        const float Speed = 1.5f;

        static Color TypeColour(string type)
        {
            switch (type)
            {
                case "bargain": return new Color(0.98f, 0.78f, 0.37f);
                case "collector": return new Color(0.84f, 0.38f, 0.27f);
                case "premium": return new Color(0.55f, 0.42f, 0.78f);
                default: return new Color(0.31f, 0.66f, 0.75f);
            }
        }

        public static GrowthVisitorView Create(GrowthVisitor v, Transform parent)
        {
            var root = new GameObject("Visitor " + v.Id + " (" + v.Type + ")");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<GrowthVisitorView>();
            view.VisitorId = v.Id;
            var body = ShopWorld.MakeMaterial("GR visitor " + v.Type, TypeColour(v.Type), 0, 0.3f);
            var skin = ShopWorld.MakeMaterial("GR visitor skin", new Color(0.93f, 0.78f, 0.64f), 0, 0.3f);
            ShopWorld.Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.62f, 0.32f), body);
            ShopWorld.Shape(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.3f, skin);
            ShopWorld.Shape(root.transform, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0.15f), Vector3.one * 0.07f, skin);
            return view;
        }

        /// <summary>Re-routes when the visit's state, browse place or queue spot changes.</summary>
        public void Follow(GrowthVisitor v, GrowthShop shop, int index)
        {
            string target = v.ContainerId == null ? null : shop.Container(v.ContainerId)?.Place;
            if (v.State == State && (v.State != GrowthVisitorState.Queued || index == queueIndex) && (v.State != GrowthVisitorState.Browsing || target == place)) return;
            path.Clear();
            switch (v.State)
            {
                case GrowthVisitorState.Browsing: path.AddRange(GrowthWorld.RouteTo(target)); place = target; break;
                case GrowthVisitorState.Queued:
                    if (State == GrowthVisitorState.Queued) path.Add(GrowthWorld.QueueSpots[Mathf.Clamp(index, 0, GrowthWorld.QueueSpots.Length - 1)]);
                    else path.AddRange(GrowthWorld.RouteToQueue(place, index));
                    wasQueued = true;
                    break;
                case GrowthVisitorState.Leaving: path.AddRange(GrowthWorld.RouteOut(place, wasQueued)); break;
            }
            State = v.State;
            queueIndex = index;
        }

        public void Tick(float dt)
        {
            float step = Speed * dt;
            while (path.Count > 0 && step > 0)
            {
                Vector3 to = path[0], from = transform.position; to.y = 0; from.y = 0;
                Vector3 d = to - from;
                if (d.magnitude <= step) { transform.position = to; step -= d.magnitude; path.RemoveAt(0); continue; }
                transform.position = from + d.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 0.25f);
                step = 0;
            }
            if (path.Count == 0 && State == GrowthVisitorState.Queued)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.back, Vector3.up), 0.2f);
        }
    }
}
