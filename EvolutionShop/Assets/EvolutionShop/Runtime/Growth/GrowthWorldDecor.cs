using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-19's world: a tank's piece of decor. Since tester round 2 (F67, F85; the user's choice, GR.74) it stands in the
    /// water, on the gravel at the back where the kit's bed leaves room, scaled to stay under the surface, the creatures
    /// swimming in front of it (TankView.Animate); reeds, ferns and bamboo sway, the lily lamp and the duck bob, and bubbles
    /// rise from it, in puffs from the volcano (GrowthEffects). In the look it is the kit's piece; in the greybox
    /// (-evolutionGrowthPlain) a base and a ball in its theme's colour. A tank is rebuilt when its decor changes
    /// (TankView.SignatureOf), so the piece always follows the shop.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        /// <summary>Where a tank's decor stands, in the tank's own space (its front is +Z): on the gravel (stand + 0.055) at the
        /// back, clear of the kit's plants, rock and arch for that kind of tank.</summary>
        public static Vector3 DecorAt(string kind, float standH)
        {
            var (x, z) = kind == "show" ? (-0.95f, -0.45f) : kind == "large" ? (0.08f, -0.26f) : (0.12f, -0.26f);
            return new Vector3(x, standH + 0.055f, z);
        }

        /// <summary>A piece's scale in a tank: whole, or smaller so its top stays 4 cm under the water's surface (the water
        /// stands from stand + 0.05 to waterH above it).</summary>
        public static float DecorScale(float pieceHeight, float waterH) => Mathf.Min(1f, (waterH - 0.045f) / Mathf.Max(0.05f, pieceHeight));

        /// <summary>How far forward of the back of the water the creatures keep, so they swim in front of the decor (its front
        /// edge: a piece is at most 0.34 m across).</summary>
        public static float DecorBack(Vector3 at, float scale, float innerDepth) => at.z + 0.17f * scale + innerDepth * 0.5f;

        public enum DecorMotion { Still, Sway, Bob }
        public static DecorMotion MotionOf(string asset) => asset == "DecorReeds" || asset == "DecorFern" || asset == "DecorBamboo" ? DecorMotion.Sway : asset == "DecorLilyLamp" ? DecorMotion.Bob : DecorMotion.Still;
        /// <summary>Where the duck floats in the duck spa's pool (the duck is its own piece, so it can bob).</summary>
        public static readonly Vector3 DuckAt = new Vector3(0.01f, 0.073f, 0.01f);

        readonly Dictionary<Water, Material> decorMaterials = new Dictionary<Water, Material>();

        sealed class DecorBuilt { public Transform Holder, Duck; public DecorMotion Motion; public float Back, Top; public string Asset; public Vector3 Base; }

        DecorBuilt BuildDecor(Transform root, GrowthContainer c, float standH, float waterH)
        {
            var def = GrowthShop.DecorOn(c);
            if (def == null || c.Def.Lab) return null;
            var holder = new GameObject("Decor " + def.Id).transform;
            holder.SetParent(root, false);
            var at = DecorAt(c.Kind, standH);
            holder.localPosition = at;
            float height = 0.27f;
            Transform duck = null;
            if (LookOn && Kit(holder, def.Asset) != null)
            {
                height = holder.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh.bounds.max.y).DefaultIfEmpty(height).Max();
                if (def.Asset == "DecorDuckSpa") { duck = Kit(holder, "DecorDuck", DuckAt); if (duck != null) duck.name = "Duck"; }
            }
            else
            {
                if (!decorMaterials.TryGetValue(def.Water, out var m))
                    decorMaterials[def.Water] = m = ShopWorld.MakeMaterial("GR decor " + def.Water, def.Water == Water.Fresh ? new Color(0.34f, 0.73f, 0.28f) : def.Water == Water.Warm ? new Color(1f, 0.48f, 0.3f) : new Color(0.25f, 0.66f, 1f), 0, 0.5f);
                ShopWorld.Shape(holder, "Base", PrimitiveType.Cylinder, new Vector3(0, 0.0175f, 0), new Vector3(0.34f, 0.0175f, 0.34f), m, false);
                ShopWorld.Shape(holder, "Piece", PrimitiveType.Sphere, new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.24f, 0.2f), m, false);
            }
            float scale = DecorScale(height, waterH);
            holder.localScale = Vector3.one * scale;
            return new DecorBuilt { Holder = holder, Duck = duck, Motion = MotionOf(def.Asset), Back = DecorBack(at, scale, WaterOf(c.Kind).z), Top = height, Asset = def.Asset, Base = at };
        }

        /// <summary>The decor standing in a container's tank (null for none), for routes and the look.</summary>
        public Transform DecorOf(string containerId)
        {
            if (!tanks.TryGetValue(containerId, out var view)) return null;
            foreach (Transform child in view.Root) if (child.name.StartsWith("Decor ")) return child;
            return null;
        }
    }
}
