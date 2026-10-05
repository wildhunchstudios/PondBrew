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
        void AuditCollectorCapacityClearance()
        {
            Transform cap = null, body = null;
            foreach (var visual in shop.World.RedesignVisuals)
            {
                if (visual.name == "Redesign / CollectorCap") cap = visual;
                if (visual.name == "Redesign / CollectorBody") body = visual;
            }
            Check(cap && body && cap.parent == shop.World.ToolChamber,
                "Collector's authored cap follows the live reservoir at both capacities");
            var seal = shop.World.ToolChamber.Find("Reservoir top");
            var bounds = ContractMeshBounds(cap);
            float sealTop = seal.localPosition.y + seal.localScale.y;
            float sealBottom = seal.localPosition.y - seal.localScale.y;
            Check(bounds.min.y >= sealBottom && bounds.min.y < sealTop && bounds.max.y > sealTop,
                "Imported twist cap seats into the vessel seal / comparison=" + shop.BaselinePreview);
            Check(cap.localPosition == Vector3.zero && cap.localScale == Vector3.one &&
                body.localPosition == Vector3.zero && body.localScale == Vector3.one,
                "Capacity changes move the cap with the vessel and preserve the fixed grip and muzzle");
            var muzzle = body.parent.Find("Collection nozzle/Nozzle light");
            Check(muzzle && Vector3.Distance(body.parent.InverseTransformPoint(muzzle.position),new Vector3(0,.05f,.489f)) < .001f,
                "Collector light remains seated at the authored muzzle");
        }

        void AuditHatcheryCreature()
        {
            var station = StationOf(StationKind.Incubator).transform;
            var resident = station.Find("Incubator creature");
            Check(resident && resident.childCount > 0, "Hatchery has the real live incubation specimen");
            Bounds actual = ContractMeshBounds(resident);
            var aperture = new Bounds(Vector3.zero, new Vector3(1.40f,1.00f,1.00f));
            Check(aperture.Contains(actual.min) && aperture.Contains(actual.max),
                "Current animated specimen clears the hatchery opening / " + shop.Model.Incubator.SpeciesId);
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
                    case "IncubatorFrame": limits = new Bounds(new Vector3(0,1.125f,0),new Vector3(1.95f,2.25f,1.44f)); break;
                    case "ResearchBench": limits = new Bounds(new Vector3(0,1.11f,0),new Vector3(1.71f,2.22f,1.21f)); break;
                    case "CollectorBody": limits = new Bounds(new Vector3(0,-.045f,.15f),new Vector3(.40f,.44f,.72f)); break;
                    case "CollectorCap": limits = new Bounds(new Vector3(0,.15f,0),new Vector3(.25f,.08f,.25f)); break;
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
                Vector3 attachment = Vector3.zero;
                Check(visual.localScale==Vector3.one && visual.localPosition==attachment && Quaternion.Angle(visual.localRotation,Quaternion.identity)<.001f,"Visual child uses its documented attachment without changing the station root / " + name);
                Check(visual.GetComponentsInChildren<Collider>(true).Length==0 && visual.GetComponentsInChildren<Station>(true).Length==0,"Imported prop creates no collision or station authority / " + name);
            }
            Check(names.Count==8,"Eight distinct approved core assets are instantiated");
            foreach (string id in new[] { ShopLayout.IncubatorFacilityId, ShopLayout.UpgradeFacilityId })
            {
                Check(shop.World.TryGetLabFacility(id, out var station), "Redesigned lab station keeps its stable facility ID / " + id);
                var bounds = ContractMeshBounds(station);
                var allowed = new Bounds(new Vector3(0,1.25f,0),new Vector3(ShopLayout.FacilityWidth(id),2.5f,ShopLayout.FacilityDepth(id)));
                Check(allowed.Contains(bounds.min) && allowed.Contains(bounds.max), "All visible equipment stays inside its unchanged placement footprint / " + id);
                Check(station.GetComponentsInChildren<Collider>(true).Length==1, "Station retains one authoritative collider / " + id);
            }
            AuditCollectorCapacityClearance();
        }
    }
}
#endif
