#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class RedesignReport
        {
            public string unity = Application.unityVersion;
            public string note = "Actual imported instance bounds and material/index metadata. Not draw calls or a performance benchmark. Station roots and collision remain authoritative.";
            public List<RedesignRow> assets = new List<RedesignRow>();
        }
        [Serializable] sealed class RedesignRow
        {
            public string asset, parent;
            public Vector3 min, max;
            public int meshes, triangles, materials;
        }
        void AuditSiphonPumpClearance()
        {
            Transform crown = null, cage = null;
            foreach (var visual in shop.World.RedesignVisuals)
                {
                    if (visual.name == "Redesign / SiphonCrown") crown = visual;
                    if (visual.name == "Redesign / SiphonCage") cage = visual;
                }
            Check(crown && cage, "Authored pump and cage remain attached during capacity comparison");
            var cap = shop.World.ToolChamber.Find("Reservoir top");
            // Source manifest/generator: bulb lower surface Y .176 in tool-root coordinates.
            // The retained primitive cylinder has local top Y 1. Compare in the actual hand frame.
            float capTop = crown.parent.InverseTransformPoint(cap.TransformPoint(Vector3.up)).y;
            float bulbBottom = crown.parent.InverseTransformPoint(crown.TransformPoint(new Vector3(0,.176f,.025f))).y;
            Check(bulbBottom > capTop, "Authored pump bulb clears the live opaque reservoir cap / comparison=" + shop.BaselinePreview);
            float capBottom = crown.parent.InverseTransformPoint(cap.TransformPoint(Vector3.down)).y;
            float cageTop = crown.parent.InverseTransformPoint(cage.TransformPoint(new Vector3(0,ContractMeshBounds(cage).max.y,0))).y;
            Check(cageTop >= capBottom && cageTop <= capTop + .001f, "Imported cage supports stay seated inside the upper cap / comparison=" + shop.BaselinePreview);
        }
        void AuditRedesignKit()
        {
            var report = new RedesignReport();
            Check(shop.World.RedesignVisuals.Count == 11, "All eleven core station and siphon instances load the original Blender kit");
            var names = new HashSet<string>();
            foreach (var visual in shop.World.RedesignVisuals)
            {
                string name = visual.name.Replace("Redesign / ", "");
                names.Add(name);
                Bounds bounds = ContractMeshBounds(visual);
                Bounds limits;
                switch (name)
                {
                    case "DisplayCabinet": limits = new Bounds(new Vector3(0,.49f,0),new Vector3(2.11f,.99f,1.31f)); break;
                    case "ServiceCounter": limits = new Bounds(new Vector3(0,.55f,0),new Vector3(2.41f,1.11f,1.11f)); break;
                    case "GrowerBase": limits = new Bounds(new Vector3(0,.52f,0),new Vector3(1.56f,1.05f,1.36f)); break;
                    case "GrowerCap": limits = new Bounds(new Vector3(0,2.125f,0),new Vector3(1.23f,.25f,1.2f)); break;
                    case "IncubatorBase": limits = new Bounds(new Vector3(0,.45f,0),new Vector3(1.91f,.91f,1.41f)); break;
                    case "SiphonCage":
                    case "SiphonCrown":
                    case "Siphon": limits = new Bounds(new Vector3(0,0,.15f),new Vector3(.30f,.65f,.72f)); break;
                    default: throw new InvalidOperationException("Unreviewed core asset: " + name);
                }
                var row = new RedesignRow { asset=name, parent=visual.parent.name, min=bounds.min, max=bounds.max };
                var materials = new HashSet<Material>();
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    row.meshes++;
                    row.triangles += ContractTriangleCount(filter.sharedMesh);
                    Check(filter.sharedMesh.vertexCount > 0, "Imported mesh has vertices / " + name);
                }
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Check(material && material.name.StartsWith("Redesign / RW_") && material.shader.name=="Universal Render Pipeline/Lit" && material.shader.isSupported, "Imported slot resolves to shared supported URP palette / " + name);
                        materials.Add(material);
                    }
                row.materials=materials.Count;
                report.assets.Add(row);
                File.WriteAllText(Path.Combine(evidence,"redesign-import-contracts.json"), JsonUtility.ToJson(report,true));
                Check(row.meshes==1 && row.triangles>0 && row.triangles<15000,"Authored prop is one consolidated mesh within the provisional triangle budget / " + name);
                Check(limits.Contains(bounds.min) && limits.Contains(bounds.max), "Imported metres/axes match the intended visual footprint / " + name);
                Vector3 attachment = name == "SiphonCage" ? new Vector3(0,-.02f,-.025f) : Vector3.zero;
                Check(visual.localScale==Vector3.one && visual.localPosition==attachment && Quaternion.Angle(visual.localRotation,Quaternion.identity)<.001f,"Visual child uses its documented attachment without changing the station root / " + name);
                Check(visual.GetComponentsInChildren<Collider>(true).Length==0 && visual.GetComponentsInChildren<Station>(true).Length==0,"Imported prop creates no collision or station authority / " + name);
            }
            Check(names.Count==8,"Eight distinct approved core assets are instantiated");
        }
    }
}
#endif
