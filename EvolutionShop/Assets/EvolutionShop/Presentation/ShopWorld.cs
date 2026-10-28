using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// Shapes and materials the world is built from: a primitive under a parent with a material, and a URP Lit material.
    /// What is left of the classic game's world builder after GR-17; the new game's rooms are <see cref="GrowthWorld"/>.
    /// </summary>
    public static class ShopWorld
    {
        public static Material MakeMaterial(string name, Color color, float metallic = 0, float smoothness = 0.45f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>A primitive named <paramref name="name"/> under <paramref name="parent"/>: no collider unless asked for, and
        /// no shadows for a transparent material.</summary>
        public static Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            var c = g.GetComponent<Collider>();
            if (!collision && c) { c.enabled = false; if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
            if (material.renderQueue >= 3000)
            {
                g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                g.GetComponent<Renderer>().receiveShadows = false;
            }
            return g.transform;
        }
    }
}
