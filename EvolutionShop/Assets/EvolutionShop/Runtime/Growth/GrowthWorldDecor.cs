using System.Collections.Generic;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-19's world: a tank's piece of decor on its top, at the back corner on the viewer's right (the filter is at the other),
    /// standing on the light bar and the lid. In the look it is the kit's piece (Tools/Art/create_growth_kit.py's decor); in
    /// the greybox (-evolutionGrowthPlain) a base and a ball in its theme's colour. A tank is rebuilt when its decor changes
    /// (TankView.SignatureOf), so the piece always follows the shop.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        /// <summary>Where a tank's decor stands, in the tank's own space (its front is +Z).</summary>
        public static Vector3 DecorAt(float width, float depth, float top) => new Vector3(-(width * 0.5f - 0.2f), top + 0.1f, -(depth * 0.5f - 0.2f));

        readonly Dictionary<Water, Material> decorMaterials = new Dictionary<Water, Material>();

        void BuildDecor(Transform root, GrowthContainer c, float width, float depth, float top)
        {
            var def = GrowthShop.DecorOn(c);
            if (def == null || c.Def.Lab) return;
            var holder = new GameObject("Decor " + def.Id).transform;
            holder.SetParent(root, false);
            holder.localPosition = DecorAt(width, depth, top);
            if (LookOn && Kit(holder, def.Asset) != null) return;
            if (!decorMaterials.TryGetValue(def.Water, out var m))
                decorMaterials[def.Water] = m = ShopWorld.MakeMaterial("GR decor " + def.Water, def.Water == Water.Fresh ? new Color(0.34f, 0.73f, 0.28f) : def.Water == Water.Warm ? new Color(1f, 0.48f, 0.3f) : new Color(0.25f, 0.66f, 1f), 0, 0.5f);
            ShopWorld.Shape(holder, "Base", PrimitiveType.Cylinder, new Vector3(0, 0.0175f, 0), new Vector3(0.34f, 0.0175f, 0.34f), m, false);
            ShopWorld.Shape(holder, "Piece", PrimitiveType.Sphere, new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.24f, 0.2f), m, false);
        }

        /// <summary>The decor standing on a container's tank (null for none), for routes and the look.</summary>
        public Transform DecorOf(string containerId)
        {
            if (!tanks.TryGetValue(containerId, out var view)) return null;
            foreach (Transform child in view.Root) if (child.name.StartsWith("Decor ")) return child;
            return null;
        }
    }
}
