using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D culture pipes (AD.4): with the look on, glowing glass pipes carry each culture's colour from where it grows to
    /// the incubator. Bloom and Flow rise from their growers, Spark from the research bench and Mist from a working
    /// conditioner once researched; each climbs to a lane of its own under the ceiling, runs across and drops into the
    /// incubator's top. A pipe's core glows with its grower's stock, beads of culture travel along it, and a dose sends a
    /// bright pulse down it. The network follows the stations when they are moved at night. Decoration only: no collider,
    /// footprint or rule.
    /// </summary>
    public sealed partial class ShopWorld
    {
        sealed class DioramaPipe { public CultureKind Kind; public Vector3[] Points; public float Length; public Transform Root; public Transform[] Beads; }

        const float DioramaPipeRadius = DioramaRoomRules.PipeRadius;
        const float DioramaPulseSeconds = 1.4f;
        static readonly Color[] DioramaCultureColours =
        {
            new Color(1f, .36f, .3f), new Color(.12f, .81f, .87f), new Color(1f, .78f, .3f), new Color(.62f, .94f, .94f),
        };

        readonly List<DioramaPipe> dioramaNetworkPipes = new List<DioramaPipe>(), dioramaGalleryPipes = new List<DioramaPipe>();
        readonly Material[] dioramaPipeCore = new Material[4], dioramaPipeBead = new Material[4];
        readonly float[] dioramaPipePulse = new float[4];
        Material dioramaPipeGlass, dioramaPipeCollar;
        Transform dioramaPipeRoot;
        string dioramaPipeSignature;
        float dioramaPipeClock, dioramaPipeCheck;

        /// <summary>How many doses have pulsed down a pipe while the look was on (validation and the route).</summary>
        public int DioramaPipePulses { get; private set; }
        /// <summary>The cultures the network has a pipe for now.</summary>
        public IEnumerable<CultureKind> DioramaPipedCultures { get { foreach (var pipe in dioramaNetworkPipes) yield return pipe.Kind; } }
        /// <summary>A culture's pipe as drawn, in the world's own coordinates (empty without one).</summary>
        public Vector3[] DioramaPipePath(CultureKind kind)
        {
            foreach (var pipe in dioramaNetworkPipes) if (pipe.Kind == kind) return (Vector3[])pipe.Points.Clone();
            return new Vector3[0];
        }
        public float DioramaPipePulse(CultureKind kind) => dioramaPipePulse[(int)kind];
        public Color DioramaPipeCoreGlow(CultureKind kind) => dioramaPipeCore[(int)kind] ? dioramaPipeCore[(int)kind].GetColor("_EmissionColor") : Color.black;

        void EnsureDioramaPipeMaterials()
        {
            if (dioramaPipeGlass) return;
            dioramaPipeGlass = DioramaOwn(DioramaLook.Glass("culture pipe glass", new Color(.85f, .95f, 1f, .2f), .55f, .85f));
            dioramaPipeCollar = DioramaOwn(DioramaLook.Toon("culture pipe collar", new Color(.86f, .66f, .3f), DioramaLook.Style.Prop));
            for (int k = 0; k < 4; k++)
            {
                var c = DioramaCultureColours[k];
                dioramaPipeCore[k] = DioramaOwn(DioramaLook.Toon("culture pipe core " + (CultureKind)k, c, DioramaLook.Style.Prop));
                dioramaPipeBead[k] = DioramaOwn(DioramaLook.Toon("culture bead " + (CultureKind)k, Color.Lerp(c, Color.white, .35f), DioramaLook.Style.Glint));
            }
        }

        /// <summary>A pipe along a polyline (the world's own coordinates): a glass tube with a glowing core, elbows at its
        /// bends, collars at its ends and three travelling beads.</summary>
        DioramaPipe DioramaPipeRun(Transform parent, string name, CultureKind kind, Vector3[] points, float radius)
        {
            EnsureDioramaPipeMaterials();
            var pipe = new DioramaPipe { Kind = kind, Points = points, Root = Group(name, parent, Vector3.zero) };
            pipe.Root.position = transform.position; pipe.Root.rotation = transform.rotation;
            int k = (int)kind;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                var a = points[i]; var b = points[i + 1];
                float length = Vector3.Distance(a, b);
                if (length < .001f) continue;
                pipe.Length += length;
                var turn = Quaternion.FromToRotation(Vector3.up, (b - a) / length);
                var glassPart = Shape(pipe.Root, "Pipe glass", PrimitiveType.Cylinder, (a + b) * .5f, new Vector3(radius * 2, length * .5f, radius * 2), dioramaPipeGlass);
                glassPart.localRotation = turn;
                var core = Shape(pipe.Root, "Pipe culture core", PrimitiveType.Cylinder, (a + b) * .5f, new Vector3(radius * .8f, length * .5f, radius * .8f), dioramaPipeCore[k]);
                core.localRotation = turn;
            }
            for (int i = 1; i + 1 < points.Length; i++)
            {
                Shape(pipe.Root, "Pipe elbow glass", PrimitiveType.Sphere, points[i], Vector3.one * radius * 2.1f, dioramaPipeGlass);
                Shape(pipe.Root, "Pipe elbow core", PrimitiveType.Sphere, points[i], Vector3.one * radius * .8f, dioramaPipeCore[k]);
            }
            foreach (int end in new[] { 0, points.Length - 1 })
            {
                var neighbour = points[end == 0 ? 1 : points.Length - 2];
                var collar = Shape(pipe.Root, "Pipe collar", PrimitiveType.Cylinder, points[end], new Vector3(radius * 2.7f, .02f, radius * 2.7f), dioramaPipeCollar);
                collar.localRotation = Quaternion.FromToRotation(Vector3.up, (neighbour - points[end]).normalized);
            }
            pipe.Beads = new Transform[3];
            for (int i = 0; i < pipe.Beads.Length; i++)
                pipe.Beads[i] = Shape(pipe.Root, "Culture bead", PrimitiveType.Sphere, points[0], Vector3.one * radius * 1.25f, dioramaPipeBead[k]);
            foreach (var renderer in pipe.Root.GetComponentsInChildren<Renderer>(true)) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
            return pipe;
        }

        static Vector3 DioramaPipeAt(DioramaPipe pipe, float distance)
        {
            var p = pipe.Points;
            for (int i = 0; i + 1 < p.Length; i++)
            {
                float length = Vector3.Distance(p[i], p[i + 1]);
                if (distance <= length || i + 2 == p.Length) return length < 1e-4f ? p[i] : Vector3.Lerp(p[i], p[i + 1], Mathf.Clamp01(distance / length));
                distance -= length;
            }
            return p[0];
        }

        /// <summary>Where a culture comes from now, or null when it has no source yet.</summary>
        Transform DioramaPipeSource(CultureKind kind)
        {
            if (kind == CultureKind.Bloom) return TryGetLabFacility(ShopLayout.BloomFacilityId, out var bloom) && bloom.gameObject.activeInHierarchy ? bloom : null;
            if (kind == CultureKind.Flow) return TryGetLabFacility(ShopLayout.FlowFacilityId, out var flow) && flow.gameObject.activeInHierarchy ? flow : null;
            return AdvancedSourceRoot(kind);
        }

        /// <summary>A station's top centre in the world's own coordinates, from what it shows.</summary>
        bool DioramaStationTop(Transform station, out Vector3 top)
        {
            top = default;
            bool any = false; var bounds = new Bounds();
            foreach (var renderer in station.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer.GetComponent<TextMesh>()) continue;
                if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!any) return false;
            top = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y + .02f, bounds.center.z));
            return true;
        }

        string DioramaPipeKey()
        {
            var key = new StringBuilder();
            void Add(Transform t) { if (!t) { key.Append("-|"); return; } var p = t.position; key.Append(Mathf.RoundToInt(p.x * 100)).Append(',').Append(Mathf.RoundToInt(p.z * 100)).Append(',').Append(Mathf.RoundToInt(t.eulerAngles.y)).Append('|'); }
            TryGetLabFacility(ShopLayout.IncubatorFacilityId, out var incubator);
            Add(incubator);
            for (int k = 0; k < 4; k++) Add(DioramaPipeSource((CultureKind)k));
            return key.ToString();
        }

        void RebuildDioramaPipes()
        {
            dioramaNetworkPipes.Clear();
            if (dioramaPipeRoot) Destroy(dioramaPipeRoot.gameObject);
            dioramaPipeRoot = Group("Diorama culture pipes (AD-D)", transform, Vector3.zero);
            if (!TryGetLabFacility(ShopLayout.IncubatorFacilityId, out var incubator) || !DioramaStationTop(incubator, out var inlet)) return;
            for (int k = 0; k < 4; k++)
            {
                var kind = (CultureKind)k;
                var source = DioramaPipeSource(kind);
                if (!source || !DioramaStationTop(source, out var start)) continue;
                var points = DioramaRoomRules.PipeRoute(start, inlet, kind);
                if (points.Length < 2) continue;
                dioramaNetworkPipes.Add(DioramaPipeRun(dioramaPipeRoot, kind + " culture pipe", kind, points, DioramaPipeRadius));
            }
        }

        /// <summary>A dose (by hand or by a module) pulses down its culture's pipe while the look is on.</summary>
        void PulseDioramaPipe(CultureKind culture)
        {
            if (!dioramaActive || (int)culture < 0 || (int)culture > 3) return;
            dioramaPipePulse[(int)culture] = 1;
            DioramaPipePulses++;
        }

        bool DioramaCultureFlows(CultureKind kind) =>
            lastModel != null && (kind == CultureKind.Spark ? lastModel.SparkResearchUnlocked : kind == CultureKind.Mist ? lastModel.MistResearchUnlocked : true);

        void TickDioramaPipes(float dt)
        {
            if (!dioramaActive) return;
            dt = Mathf.Max(0, dt);
            dioramaPipeCheck -= dt;
            if (dioramaPipeCheck <= 0 || !dioramaPipeRoot)
            {
                dioramaPipeCheck = .25f;
                string key = DioramaPipeKey();
                if (key != dioramaPipeSignature || !dioramaPipeRoot) { dioramaPipeSignature = key; RebuildDioramaPipes(); }
            }
            bool still = fxReducedMotion;
            if (!still) dioramaPipeClock += dt;
            for (int k = 0; k < 4; k++)
            {
                var kind = (CultureKind)k;
                dioramaPipePulse[k] = Mathf.Max(0, dioramaPipePulse[k] - dt / DioramaPulseSeconds);
                bool flows = DioramaCultureFlows(kind);
                float stock = 0;
                if (flows && lastModel != null)
                {
                    var grower = lastModel.GetGrower(kind);
                    stock = grower.Capacity > 0 ? Mathf.Clamp01(grower.Units / (float)grower.Capacity) : 0;
                    if (kind == CultureKind.Mist) stock = Mathf.Max(stock, .6f);
                }
                float glow = flows ? .35f + .65f * stock : .08f;
                // Reduced motion keeps a dose's pulse to a gentle brightening (AD.7: no flash).
                float pulse = dioramaPipePulse[k] * (fxReducedMotion ? .35f : 1);
                var colour = DioramaCultureColours[k];
                if (dioramaPipeCore[k]) dioramaPipeCore[k].SetColor("_EmissionColor", colour * (glow * .7f + pulse * 2.2f));
                if (dioramaPipeBead[k]) dioramaPipeBead[k].SetColor("_EmissionColor", colour * (1.6f + pulse * 2.6f));
            }
            TickDioramaPipeBeads(dioramaNetworkPipes, still);
            TickDioramaPipeBeads(dioramaGalleryPipes, still);
        }

        void TickDioramaPipeBeads(List<DioramaPipe> pipes, bool still)
        {
            foreach (var pipe in pipes)
            {
                if (pipe.Beads == null || pipe.Length <= 0) continue;
                int k = (int)pipe.Kind;
                bool flows = DioramaCultureFlows(pipe.Kind);
                // Low detail keeps one bead a pipe.
                int shown = aquariumLow ? 1 : pipe.Beads.Length;
                float speed = .45f + dioramaPipePulse[k] * 1.6f;
                for (int i = 0; i < pipe.Beads.Length; i++)
                {
                    var bead = pipe.Beads[i];
                    if (!bead) continue;
                    bool on = flows && i < shown;
                    if (bead.gameObject.activeSelf != on) bead.gameObject.SetActive(on);
                    if (!on) continue;
                    float spacing = pipe.Length / pipe.Beads.Length;
                    float along = still ? (i + .5f) * spacing : Mathf.Repeat(dioramaPipeClock * speed + i * spacing + k * .37f, pipe.Length);
                    bead.localPosition = DioramaPipeAt(pipe, along);
                }
            }
        }

        void ShowDioramaPipes()
        {
            if (dioramaPipeRoot) dioramaPipeRoot.gameObject.SetActive(dioramaActive);
            if (dioramaActive) { dioramaPipeCheck = 0; dioramaPipeSignature = null; }
        }
    }
}
