using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-16 wave 3: the pipes and the chute in the new look (AD.4's glowing culture pipes). A pipe is a glass tube with a
    /// glowing core of the culture it carries, turquoise joints where it turns and at its ends, and hangers to the ceiling over
    /// its overhead runs; while it flows (an electric pump with power, the hand pump being cranked, the auto-doser drawing from
    /// its buffer) beads of its culture travel down it, and every dose that reaches its buffer, or leaves it for the port, sends
    /// a pulse along its core. The chute is a wider glass tube with joints, its hatchling sliding down it as before. The paths
    /// are the greybox's (GrowthWorldLab.PipePath), so where pipes run, hop and end is unchanged.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        sealed class LookPipe
        {
            public int Id;
            public bool Line;
            /// <summary>The buffer end it watches for doses: a line's buffer (doses in), a port pipe's buffer (doses out).</summary>
            public string Buffer;
            public readonly List<Vector3> Path = new List<Vector3>();
            public float Length, Phase, Pulse;
            public int Units = -1;
            public Transform[] Beads;
            public Material Core;
            public Color CoreColor;
        }

        public const int BeadsPerPipe = 3;
        const float CoreGlow = 1.1f, BeadSpeed = 0.9f, LabCeiling = 3.6f, PulseSeconds = 0.35f;
        readonly List<LookPipe> lookPipes = new List<LookPipe>();
        Material pipeGlass, chuteGlass;
        /// <summary>The pipes drawn in the look, how many are flowing now, and the strongest pulse since it was last reset (validation).</summary>
        public int LookPipeCount => lookPipes.Count;
        public int LookPipesFlowing { get; private set; }
        public float LookPulsePeak { get; private set; }
        public void ResetLookPulsePeak() => LookPulsePeak = 0;

        void ClearLookPipes()
        {
            foreach (var lp in lookPipes) if (lp.Core) Destroy(lp.Core);
            lookPipes.Clear(); // their objects are the pipes root's children, destroyed with it
        }

        /// <summary>The path without its straight-through points, so a run is one tube and joints sit only where it turns.</summary>
        static List<Vector3> Simplified(List<Vector3> path)
        {
            var points = new List<Vector3>();
            foreach (var p in path) if (points.Count == 0 || (p - points[points.Count - 1]).sqrMagnitude > 1e-6f) points.Add(p);
            for (int i = points.Count - 2; i >= 1; i--)
            {
                var a = (points[i] - points[i - 1]).normalized; var b = (points[i + 1] - points[i]).normalized;
                if (Vector3.Dot(a, b) > 0.9995f) points.RemoveAt(i);
            }
            return points;
        }

        static void Segment(Transform root, string name, Vector3 a, Vector3 b, float radius, Material material)
        {
            float length = Vector3.Distance(a, b);
            if (length < 0.005f) return;
            var seg = ShopWorld.Shape(root, name, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(radius * 2, length * 0.5f, radius * 2), material);
            seg.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        /// <summary>A glass tube along a path with joints at its turns (and its ends, for a pipe; the chute ends open, in the
        /// incubator's outlet and the sorter's hopper) and hangers over its overhead runs; returns its simplified path and its length.</summary>
        List<Vector3> Tube(Transform root, List<Vector3> path, float radius, Material glassMat, Material core, bool endJoints, float jointSize, out float length)
        {
            var joint = LookMaterial("GK_Teal"); var hanger = LookMaterial("GK_TealDark");
            var points = Simplified(path);
            length = 0;
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1]; var b = points[i];
                length += Vector3.Distance(a, b);
                Segment(root, "Glass", a, b, radius, glassMat);
                if (core != null) Segment(root, "Core", a, b, radius * 0.45f, core);
                // A hanger from the middle of each overhead run longer than a cell, up to the ceiling.
                if (Mathf.Abs(a.y - b.y) < 0.01f && a.y > 2.5f && Vector3.Distance(a, b) > 0.6f)
                {
                    var m = (a + b) * 0.5f;
                    // GR-24: down to the pipe's middle, through a clamp round it (it stopped at the clear glass's top, so it
                    // looked to end short of the pipe).
                    var along = (b - a).normalized;
                    Segment(root, "Hanger", m, new Vector3(m.x, LabCeiling, m.z), 0.012f, hanger);
                    Segment(root, "Clamp", m - along * 0.03f, m + along * 0.03f, radius * 1.2f, hanger);
                }
            }
            for (int i = endJoints ? 0 : 1; i < (endJoints ? points.Count : points.Count - 1); i++)
                ShopWorld.Shape(root, "Joint", PrimitiveType.Sphere, points[i], Vector3.one * radius * jointSize, joint);
            return points;
        }

        /// <summary>A culture pipe in the look: its tube, its core (none for a port pipe whose buffer is empty) and its beads.</summary>
        void DrawLookPipe(Transform root, List<Vector3> path, GrowthPipe pipe, string cultureId)
        {
            if (pipeGlass == null) pipeGlass = LookGlass("pipe glass", new Color(0.92f, 0.98f, 1f, 0.16f), 0.6f, 0.8f);
            var lp = new LookPipe { Id = pipe.Id, Line = pipe.Line, Buffer = pipe.Line ? pipe.To : GrowthLab.IsBuffer(pipe.From) ? pipe.From : null };
            if (cultureId != null)
            {
                lp.CoreColor = Hex(GrowthContent.Culture(cultureId).Hex);
                lp.Core = Glowing("pipe core " + pipe.Id, lp.CoreColor, CoreGlow);
            }
            lp.Path.AddRange(Tube(root, path, PipeRadius, pipeGlass, lp.Core, true, 2.7f, out lp.Length));
            if (cultureId != null)
            {
                lp.Beads = new Transform[BeadsPerPipe];
                for (int k = 0; k < BeadsPerPipe; k++)
                {
                    lp.Beads[k] = ShopWorld.Shape(root, "Bead", PrimitiveType.Sphere, lp.Path[0], Vector3.one * PipeRadius * 1.5f, culture[cultureId]);
                    lp.Beads[k].gameObject.SetActive(false);
                }
            }
            lookPipes.Add(lp);
        }

        /// <summary>The hatchling chute in the look: a wider glass tube with joints and hangers (its bead is GrowthWorldLine's).</summary>
        void DrawLookChute(Transform root, List<Vector3> path)
        {
            if (chuteGlass == null) chuteGlass = LookGlass("chute glass", new Color(0.86f, 0.96f, 1f, 0.2f), 0.65f, 0.8f);
            Tube(root, path, ChuteRadius, chuteGlass, null, false, 2.2f, out _);
        }

        /// <summary>Beads travel down each pipe while it flows, and its core pulses on every dose (no pulse with Reduced motion).</summary>
        void AnimateLookPipes(GrowthShop shop, float dt)
        {
            int flowing = 0;
            var doser = shop.PowerUse(GrowthShop.DoserPower);
            bool dosing = doser != null && doser.Got > 0;
            foreach (var lp in lookPipes)
            {
                bool flow;
                if (lp.Line)
                {
                    var p = shop.Pipe(lp.Id);
                    var use = p == null ? null : shop.PowerUse(p.PowerId);
                    flow = p != null && (shop.ElectricPumps ? use != null && use.Got > 0 : session.PumpingNow && session.Focused != null && session.Focused.Id == p.Culture);
                }
                else flow = lp.Buffer != null && dosing;
                // A pulse on every dose: a whole unit into the line's buffer, or out of the port pipe's buffer.
                var b = lp.Buffer == null ? null : shop.BufferAt(GrowthLab.BufferPlace(lp.Buffer));
                int units = b == null ? 0 : Mathf.FloorToInt(b.Units + 1e-4f);
                if (lp.Units >= 0 && (lp.Line ? units > lp.Units : units < lp.Units)) lp.Pulse = 1;
                lp.Units = units;
                LookPulsePeak = Mathf.Max(LookPulsePeak, lp.Pulse);
                lp.Pulse = Mathf.Max(0, lp.Pulse - dt / PulseSeconds);
                if (lp.Core) lp.Core.SetColor("_EmissionColor", lp.CoreColor * (CoreGlow * (1 + (session.ReducedMotion ? 0 : 1.6f * lp.Pulse))));
                if (lp.Beads == null) continue;
                if (flow) { flowing++; lp.Phase = Mathf.Repeat(lp.Phase + dt * BeadSpeed / Mathf.Max(0.1f, lp.Length), 1f); }
                for (int k = 0; k < lp.Beads.Length; k++)
                {
                    if (lp.Beads[k].gameObject.activeSelf != flow) lp.Beads[k].gameObject.SetActive(flow);
                    if (flow) lp.Beads[k].localPosition = Along(lp.Path, Mathf.Repeat(lp.Phase + k / (float)lp.Beads.Length, 1f));
                }
            }
            LookPipesFlowing = flowing;
        }
    }
}
