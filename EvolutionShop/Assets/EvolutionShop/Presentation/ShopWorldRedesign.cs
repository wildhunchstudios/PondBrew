using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        readonly Dictionary<string, Material> redesignPalette = new Dictionary<string, Material>();
        readonly List<Transform> redesignVisuals = new List<Transform>();
        public IReadOnlyList<Transform> RedesignVisuals => redesignVisuals;

        Material RedesignMaterial(string name)
        {
            name = name.Replace(" (Instance)", "");
            if (redesignPalette.TryGetValue(name, out var found)) return found;
            string color;
            switch (name)
            {
                case "RW_Oak": color = "#754426"; break;
                case "RW_Honey": color = "#C98B46"; break;
                case "RW_PaleOak": color = "#E7B777"; break;
                case "RW_Teal": color = "#246F68"; break;
                case "RW_Mint": color = "#8CCCB0"; break;
                case "RW_Cream": color = "#F5E5BD"; break;
                case "RW_Coral": color = "#ED795E"; break;
                case "RW_Brass": color = "#D5AA4D"; break;
                case "RW_Ink": color = "#253C3E"; break;
                case "RW_Leaf": color = "#538A4C"; break;
                case "RW_Sky": color = "#8CCCD4"; break;
                default: throw new System.InvalidOperationException("Unmapped redesign material: " + name);
            }
            ColorUtility.TryParseHtmlString(color, out var tint);
            var material = MakeMaterial("Redesign / " + name, tint, name == "RW_Brass" ? .3f : 0, .28f);
            redesignPalette.Add(name, material);
            return material;
        }

        bool AttachRedesign(Transform parent, string asset, params string[] replaced)
            => AttachAuthored(parent, "RedesignKit", asset, replaced);

        bool AttachAuthored(Transform parent, string kit, string asset, params string[] replaced)
        {
            var source = Resources.Load<GameObject>("Art/" + kit + "/" + asset);
            // Retain the existing geometry if the optional visual resource cannot load.
            if (!source) { Debug.LogWarning("Missing redesign visual: " + asset); return false; }
            var visual = Instantiate(source, parent, false).transform;
            visual.name = "Redesign / " + asset;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = RedesignMaterial(materials[i].name);
                renderer.sharedMaterials = materials;
            }
            redesignVisuals.Add(visual);
            foreach (Transform child in parent)
                foreach (var name in replaced)
                    if (child.name == name) child.gameObject.SetActive(false);
            return true;
        }

        void RedesignLabelDepth(Transform root, float depth, float center, float width, bool atCanopy = false)
        {
            foreach (Transform child in root)
            {
                var label = child.GetComponent<TextMesh>();
                if (label)
                {
                    if (atCanopy && child.localPosition.y > 2.3f)
                    { child.gameObject.SetActive(false); continue; }
                    if (child.localPosition.y >= .8f) continue;
                    if (child.localPosition.y < .2f)
                    { child.gameObject.SetActive(false); continue; }
                    bool title = child.localPosition.y > .5f;
                    child.localPosition = new Vector3(0, center + (title ? .064f : -.067f), depth - .025f);
                    // The custom unlit text shader consumes vertex colour in linear space.
                    label.color = new Color(.08f,.22f,.21f).linear;
                    label.fontStyle = FontStyle.Bold;
                    foreach (var fit in labels)
                        if (fit.Text == label)
                        { fit.Height = title ? .095f : .062f; fit.Width = width - .14f; FitLabel(fit); break; }
                    continue;
                }
                if (child.name == "Label rail") { child.gameObject.SetActive(false); continue; }
                if (child.name == "Machine faceplate")
                {
                    child.localPosition = new Vector3(0,center,depth);
                    child.localScale = new Vector3(width,.285f,.035f);
                    child.GetComponent<MeshFilter>().sharedMesh = ArtSoftCube();
                    child.GetComponent<MeshRenderer>().sharedMaterial = RedesignMaterial("RW_Cream");
                }
                else if (child.name == "Identification stripe")
                {
                    child.localPosition = new Vector3(-width*.5f+.025f,center,depth-.02f);
                    child.localScale = new Vector3(.032f,.21f,.009f);
                    child.GetComponent<MeshRenderer>().sharedMaterial = RedesignMaterial("RW_Coral");
                }
            }
        }

        void DisposeRedesign()
        {
            // Resource meshes belong to Unity; this instance owns only the mapped palette.
            foreach (var material in redesignPalette.Values) if (material) Destroy(material);
            redesignPalette.Clear();
        }
    }
}
