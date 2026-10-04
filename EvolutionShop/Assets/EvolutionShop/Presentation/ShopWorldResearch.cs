using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        readonly List<Transform> sparkCells = new List<Transform>();
        Material sparkMat, sparkCoreMat, mistMat;
        Transform upgradeRoot, sparkModule;
        Station upgradeStation;
        TextMesh sparkStatus, researchHint;
        int sparkCount, mistCount;
        CultureKind harvestingCulture;

        void BuildResearchMaterials()
        {
            sparkMat = MakeMaterial("Spark lavender pearls", new Color(.70f, .43f, .96f), .08f, .62f);
            mistMat = MakeMaterial("Mist opal droplets", new Color(.62f, .94f, .94f), .05f, .42f);
            sparkCoreMat = MakeMaterial("Spark warm gold core", new Color(1f, .86f, .30f), .04f, .5f);
            sparkCoreMat.EnableKeyword("_EMISSION");
            sparkCoreMat.SetColor("_EmissionColor", new Color(.22f, .12f, .025f));
        }

        void BuildSparkModule(Transform bench)
        {
            upgradeRoot = bench;
            upgradeStation = bench.GetComponent<Station>();
            upgradeStation.Label = "Research / Siphon upgrade";
            sparkModule = Group("Spark research culture module", bench, Vector3.zero);
            // Reuse the existing glass sample chamber. No station, collider, or footprint is added.
            for (int i = 0; i < 6; i++)
            {
                var cell = SparkOrganism(sparkModule, .062f);
                cell.name = "Spark grower unit " + (i + 1);
                sparkCells.Add(cell);
            }
            TablePart(sparkModule, "Spark stock plaque", new Vector3(0, 2.11f, -.34f), new Vector3(.94f, .17f, .035f), dark, false);
            sparkStatus = Text(sparkModule, "SPARK 0 / 6", new Vector3(0, 2.11f, -.365f), .037f, new Color(1f, .88f, .53f), .84f);
            AnimateSparkCells(0);
            sparkModule.gameObject.SetActive(false);
        }

        void RefreshSparkModule(GameModel model)
        {
            bool unlocked = model.SparkResearchUnlocked;
            sparkModule.gameObject.SetActive(unlocked);
            upgradeStation.Label = unlocked ? "Spark grower / Siphon upgrade" : "Research / Siphon upgrade";
            researchHint.text = unlocked ? "SPARK CULTURE / HOLD LMB" : model.SparkResearchReady ?
                "SPARK RESEARCH READY / E" : "RESEARCH / DISCOVER 3 SPECIES";
            if (!unlocked) return;
            var grower = model.GetGrower(CultureKind.Spark);
            sparkStatus.text = "SPARK " + grower.Units + " / " + grower.Capacity;
            for (int i = 0; i < sparkCells.Count; i++) sparkCells[i].gameObject.SetActive(i < grower.Units);
        }

        void AnimateSparkCells(float time)
        {
            for (int i = 0; i < sparkCells.Count; i++)
            {
                float angle = i * 2.399f + time * .42f;
                sparkCells[i].localPosition = new Vector3(Mathf.Cos(angle) * .13f,
                    1.27f + i % 3 * .20f + Mathf.Sin(time * 1.7f + i) * .024f, Mathf.Sin(angle) * .13f);
                sparkCells[i].localRotation = Quaternion.Euler(time * 16 + i * 33, time * 30 + i * 41, Mathf.Sin(time + i) * 18);
            }
        }

        bool TryHarvestCulture(Station station, out CultureKind culture)
        {
            culture = CultureKind.Bloom;
            if (!station) return false;
            if (station.Kind == StationKind.Bloom) return true;
            if (station.Kind == StationKind.Flow) { culture = CultureKind.Flow; return true; }
            if (station.Kind == StationKind.Conditioning && lastModel != null && lastModel.MistResearchUnlocked &&
                lastModel.GetConditioner(station.ConditionerItemId).HasValue)
            { culture = CultureKind.Mist; return true; }
            if (station.Kind == StationKind.Upgrade && lastModel != null && lastModel.SparkResearchUnlocked)
            { culture = CultureKind.Spark; return true; }
            return false;
        }

        static string CultureVisualName(CultureKind culture)
            => culture == CultureKind.Bloom ? "Bloom" : culture == CultureKind.Flow ? "Flow" : culture == CultureKind.Spark ? "Spark" : "Mist";

        Transform CultureOrganism(Transform parent, CultureKind culture, float scale)
        {
            if (culture == CultureKind.Bloom) return Organism(parent, true, bloomMat, scale);
            if (culture == CultureKind.Flow) return Organism(parent, false, flowMat, scale);
            if (culture == CultureKind.Spark) return SparkOrganism(parent, scale);
            if (culture == CultureKind.Mist) return MistOrganism(parent, scale);
            throw new ArgumentOutOfRangeException(nameof(culture));
        }

        Transform SparkOrganism(Transform parent, float scale)
        {
            var root = Group("Spark bead cluster", parent, Vector3.zero);
            root.localScale = Vector3.one * scale;
            Shape(root, "Golden Spark core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .64f, sparkCoreMat);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2 / 3;
                Shape(root, "Lavender Spark pearl", PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(angle) * .36f, Mathf.Sin(angle) * .36f, .035f), Vector3.one * .43f, sparkMat);
            }
            return root;
        }

        Transform MistOrganism(Transform parent, float scale)
        {
            var root = Group("Mist opal droplet", parent, Vector3.zero);
            root.localScale = Vector3.one * scale;
            Shape(root, "Mist pearl", PrimitiveType.Sphere, Vector3.zero, new Vector3(.62f, .84f, .62f), mistMat);
            Shape(root, "Mist satellite", PrimitiveType.Sphere, new Vector3(.42f, .45f, 0), Vector3.one * .25f, cream);
            return root;
        }

        void DisposeResearchPresentation()
        {
            if (sparkMat) Destroy(sparkMat);
            if (sparkCoreMat) Destroy(sparkCoreMat);
            if (mistMat) Destroy(mistMat);
        }
    }
}
