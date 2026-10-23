using System.IO;
using EvolutionShop.Growth;
using UnityEditor;
using UnityEngine;

namespace EvolutionShop.Editor
{
    /// <summary>
    /// GR-01: validates the grow-from-nothing content and writes its review documents (ROSTER.md, PACING.md,
    /// pacing-curve.svg) to Docs/Evidence/gr-01. Batch: -executeMethod EvolutionShop.Editor.GrowthReports.WriteGr01
    /// </summary>
    public static class GrowthReports
    {
        public const string EvidenceDirectory = "../Docs/Evidence/gr-01";

        [MenuItem("Evolution Shop/GR/Write roster and pacing report")]
        public static void WriteGr01()
        {
            var problems = GrowthRules.Validate();
            foreach (var problem in problems) Debug.LogError("EVOLUTION_GR01_CONTENT " + problem);
            if (problems.Count == 0)
                foreach (var file in GrowthReport.WriteAll(EvidenceDirectory)) Debug.Log("EVOLUTION_GR01_REPORT " + Path.GetFullPath(file));
            if (Application.isBatchMode) EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }
    }
}
