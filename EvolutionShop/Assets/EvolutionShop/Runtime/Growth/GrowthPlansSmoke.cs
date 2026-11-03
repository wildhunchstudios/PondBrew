#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EvolutionShop
{
    /// <summary>
    /// GR-34's Windows route (-evolutionGrowthPlansSmoke): the building's plan as it stands with everything bought (F68). The
    /// look start point with every room open, every station and generator at its top level, every place holding the largest
    /// tank it takes, every rack and buffer bought and the research room open; then two pictures from straight above with the
    /// ceilings off, how it looks; and where colliders stand between 10 cm and 1.8 m off the floor, in 5 cm cells, which the
    /// plan page measures for the walkways (at least 1.2 m everywhere is GR.74's aim).
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string PlansScenarioText = "GR-34 the plans (F68): the building with everything bought, from straight above: how it looks, and what stands in the way between 7 cm and 1.9 m, for measuring the walkways";
        /// <summary>The plan's extent in the world (x and z), and its pixels per metre.</summary>
        const float PlanX0 = -10.5f, PlanX1 = 12.5f, PlanZ0 = -4.5f, PlanZ1 = 17.5f, PlanPixelsPerMetre = 40, ObstacleCell = 0.05f;

        IEnumerator PlansScenario()
        {
            limitations.Add("GR-34 plans route (development only): the look start point (GrowthStarts.Look, labelled) with the rest bought by the shop's own actions where they allow (the research room, every rack and buffer, the largest tank on each free place), every station's level set to its top as a fixture; the player stood on the street so the hands are not in the plan; the ceilings hidden for the pictures.");
            yield return Opening();
            TakeInput();
            var shop = GrowthStarts.Make(GrowthStarts.Look);
            shop.Credits = 10000000; shop.Rp = 100000; shop.Reputation = Mathf.Max(shop.Reputation, GrowthContent.StarThresholds[4] + 5); shop.Era = Mathf.Max(shop.Era, 5);
            if (!shop.ResearchRoom) Ok(shop.BuyResearchRoom(), "Open the research room");
            foreach (var r in new[] { "r1", "r2", "r3", "r4" }) if (shop.ContainerAt(r) == null) shop.BuyRack(r);
            for (int b = 0; b < GrowthLab.BufferPlaces; b++) if (shop.BufferAt(b) == null) shop.BuyBuffer(b);
            foreach (var place in shop.FreePlaces.ToList())
            {
                var p = GrowthShop.Place(place);
                shop.BuyContainer(p.Size == "feature" ? "show" : p.Size == "long" ? "large" : "starter", place);
            }
            foreach (var station in GrowthShop.UpgradeableStations)
                for (int i = 0; i < 6 && shop.Upgrade(station).Success; i++) { }
            foreach (var station in GrowthShop.UpgradeableStations)
                if (GrowthContent.Station(station) != null) shop.Levels[station] = GrowthContent.Station(station).Levels.Length;
            foreach (var c in shop.Containers) c.Level = c.Def.Levels.Length;
            shop.TakeEvents();
            s.LoadFixture(shop);
            yield return new WaitForSeconds(4f);
            LookAt(new Vector3(-0.5f, 0, -6.5f), new Vector3(-0.5f, 1.5f, -9f));
            yield return new WaitForSeconds(1f);
            Check(shop.ResearchRoom && shop.Workshop && shop.UnitC && shop.Hall && shop.Buffers.Count == GrowthLab.BufferPlaces, "Every room open and every buffer bought (" + shop.Buffers.Count + ")");
            Check(shop.FreePlaces.Count() == 0, "Every place holds a tank (" + string.Join(",", shop.FreePlaces) + " free)");

            var ceilings = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.name.ToLowerInvariant().Contains("ceiling") || r.enabled && r.transform.parent != null && r.transform.parent.name.ToLowerInvariant().Contains("ceiling")).ToList();
            foreach (var r in ceilings) r.enabled = false;
            int w = Mathf.RoundToInt((PlanX1 - PlanX0) * PlanPixelsPerMetre), h = Mathf.RoundToInt((PlanZ1 - PlanZ0) * PlanPixelsPerMetre);
            var go = new GameObject("GR-34 plan camera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = (PlanZ1 - PlanZ0) * 0.5f; cam.aspect = (float)w / h;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var data = cam.GetUniversalAdditionalCameraData();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.enabled = false;

            // How it looks: from just under the ceilings.
            go.transform.position = new Vector3((PlanX0 + PlanX1) * 0.5f, 3.5f, (PlanZ0 + PlanZ1) * 0.5f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 6f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            data.renderPostProcessing = true;
            yield return new WaitForEndOfFrame();
            SavePlan(cam, rt, w, h, "1400-plan-view.png");

            // What stands in the player's way: every collider between 10 cm and 1.8 m off the floor (walls too, which a picture
            // from above sees edge-on), tested in 5 cm cells as the player's capsule meets them; white where something stands.
            yield return new WaitForFixedUpdate();
            int cw = Mathf.RoundToInt((PlanX1 - PlanX0) / ObstacleCell), ch = Mathf.RoundToInt((PlanZ1 - PlanZ0) / ObstacleCell);
            var grid = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            var half = new Vector3(ObstacleCell * 0.5f, 0.85f, ObstacleCell * 0.5f);
            int blocked = 0;
            for (int z = 0; z < ch; z++)
            {
                for (int x = 0; x < cw; x++)
                {
                    var at = new Vector3(PlanX0 + (x + 0.5f) * ObstacleCell, 0.95f, PlanZ0 + (z + 0.5f) * ObstacleCell);
                    bool hit = Physics.CheckBox(at, half, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (hit) blocked++;
                    grid.SetPixel(x, z, hit ? Color.white : Color.black);
                }
                if (z % 40 == 0) yield return null;
            }
            grid.Apply();
            File.WriteAllBytes(Path.Combine(evidence, "1401-plan-obstacles.png"), grid.EncodeToPNG());
            Destroy(grid);
            Check(blocked > cw * ch / 50 && blocked < cw * ch / 2, "The obstacles found: " + blocked + " of " + (cw * ch) + " cells");
            File.WriteAllText(Path.Combine(evidence, "plan.json"), "{\"x0\":" + PlanX0 + ",\"x1\":" + PlanX1 + ",\"z0\":" + PlanZ0 + ",\"z1\":" + PlanZ1 + ",\"pixelsPerMetre\":" + PlanPixelsPerMetre + ",\"width\":" + w + ",\"height\":" + h + ",\"cell\":" + ObstacleCell + ",\"cellsX\":" + cw + ",\"cellsZ\":" + ch + "}");
            checks.Add("Capture plans");
            foreach (var r in ceilings) r.enabled = true;
            cam.targetTexture = null; rt.Release(); Destroy(go);
        }

        void SavePlan(Camera cam, RenderTexture rt, int w, int h, string name)
        {
            cam.Render();
            var before = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = before;
            File.WriteAllBytes(Path.Combine(evidence, name), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
#endif
