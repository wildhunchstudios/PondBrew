using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// GR-12: the little burst a kit creature lets go on its trick's beat in the greybox, sized to the creature: bubbles from a
    /// Brookkin's lips, sparkles from a Sparkline's lantern (glowing), a puff of mist from an Airfin, sparks from a Cinderling's
    /// spines (glowing embers, flying high), droplets from a Dewling's beads (scattering wide), a glittering plankton cloud
    /// from a Reefback's corals, a sparkle of salt from a Saltscale, a slow drift of glowing motes from a Gloamer's lure and
    /// a trail of stardust from a Cometail's tail. They rise, sway, shrink and are gone in a second or so. Presentation only.
    /// </summary>
    public sealed class GrowthCreatureBurst : MonoBehaviour
    {
        const float Life = 1.1f;
        readonly List<Transform> pieces = new List<Transform>();
        readonly List<Vector3> drift = new List<Vector3>();
        readonly List<float> sizes = new List<float>();
        float age, rise;
        static readonly Dictionary<DioramaFamily, Material> materials = new Dictionary<DioramaFamily, Material>();

        static Material MaterialFor(DioramaFamily family)
        {
            if (materials.TryGetValue(family, out var m) && m != null) return m;
            switch (family)
            {
                case DioramaFamily.Sparkline:
                    m = ShopWorld.MakeMaterial("GR burst sparkle", new Color(1f, .9f, .45f), 0, .6f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, .82f, .3f) * 1.6f);
                    break;
                case DioramaFamily.Cinderling:
                    m = ShopWorld.MakeMaterial("GR burst spark", new Color(1f, .62f, .25f), 0, .5f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, .45f, .12f) * 1.8f);
                    break;
                case DioramaFamily.Dewling: m = ShopWorld.MakeMaterial("GR burst droplet", new Color(.8f, .94f, 1f), .2f, .95f); break;
                case DioramaFamily.Reefback:
                    m = ShopWorld.MakeMaterial("GR burst plankton", new Color(1f, .95f, .7f), 0, .4f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, .85f, .5f) * 1.2f);
                    break;
                case DioramaFamily.Saltscale:
                    m = ShopWorld.MakeMaterial("GR burst salt", Color.white, 0, .7f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(.9f, .95f, 1f) * 1.6f);
                    break;
                case DioramaFamily.Gloamer:
                    m = ShopWorld.MakeMaterial("GR burst mote", new Color(.8f, .95f, 1f), 0, .3f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(.7f, .95f, 1f) * 1.4f);
                    break;
                case DioramaFamily.Cometail:
                    m = ShopWorld.MakeMaterial("GR burst stardust", new Color(1f, .95f, .75f), 0, .6f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, .9f, .6f) * 1.8f);
                    break;
                case DioramaFamily.Airfin: m = ShopWorld.MakeMaterial("GR burst mist", new Color(.97f, .95f, 1f), 0, .2f); break;
                default: m = ShopWorld.MakeMaterial("GR burst bubble", new Color(.86f, .97f, 1f), .1f, .9f); break;
            }
            return materials[family] = m;
        }

        /// <summary>Lets count pieces go at a point (world), in parent's space, for a creature of the given length.</summary>
        public static GrowthCreatureBurst Spawn(Transform parent, Vector3 at, DioramaFamily family, int count, float length)
        {
            var go = new GameObject("Creature burst (" + family + ")");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var burst = go.AddComponent<GrowthCreatureBurst>();
            burst.rise = length * (family == DioramaFamily.Airfin ? 1.2f : family == DioramaFamily.Dewling ? .8f : family == DioramaFamily.Cinderling ? 2.6f :
                family == DioramaFamily.Reefback ? .9f : family == DioramaFamily.Saltscale ? 1.6f : family == DioramaFamily.Gloamer ? .7f : family == DioramaFamily.Cometail ? 1.2f : 2.2f);
            bool spark = family == DioramaFamily.Sparkline || family == DioramaFamily.Cinderling || family == DioramaFamily.Saltscale || family == DioramaFamily.Cometail;
            var material = MaterialFor(family);
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.4f, r = length * (.08f + .05f * (i % 3));
                float size = length * (family == DioramaFamily.Airfin ? .32f : family == DioramaFamily.Reefback || family == DioramaFamily.Gloamer ? .05f : family == DioramaFamily.Cometail ? .045f :
                    spark ? .09f : family == DioramaFamily.Dewling ? .1f : .14f) * (1 - .12f * (i % 3));
                var piece = ShopWorld.Shape(go.transform, "Burst piece", spark ? PrimitiveType.Cube : PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), Vector3.one * size, material);
                if (spark) piece.localRotation = Quaternion.Euler(45, 45 * i, 45);
                var renderer = piece.GetComponent<Renderer>();
                if (renderer) renderer.shadowCastingMode = ShadowCastingMode.Off;
                var collider = piece.GetComponent<Collider>();
                if (collider) { if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
                burst.pieces.Add(piece); burst.sizes.Add(size);
                burst.drift.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * length * (family == DioramaFamily.Dewling ? .6f : family == DioramaFamily.Reefback || family == DioramaFamily.Cometail ? .5f :
                    family == DioramaFamily.Gloamer ? .3f : .35f));
            }
            return burst;
        }

        void Update()
        {
            age += Mathf.Min(Time.deltaTime, .1f);
            float u = age / Life;
            if (u >= 1) { Destroy(gameObject); return; }
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!pieces[i]) continue;
                float lag = Mathf.Clamp01(u * 1.15f - i * .04f);
                var p = drift[i] * Mathf.Sqrt(lag) + Vector3.up * (rise * lag);
                p.x += Mathf.Sin(age * 9 + i) * sizes[i] * .3f;
                pieces[i].localPosition = p;
                pieces[i].localScale = Vector3.one * (sizes[i] * (1 - u * u));
            }
        }
    }
}
