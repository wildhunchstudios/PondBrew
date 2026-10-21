using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        MeshRenderer hatcheryLamp;
        Material hatcheryIdle, hatcheryGrowing, hatcheryReady;

        bool BuildHatcheryVisual(Transform station)
        {
            if (!AttachAuthored(station, "LabRefreshKit", "IncubatorFrame",
                "Incubator pedestal", "Rounded worktop", "Incubator enamel finish", "Incubator orbit", "Base seal")) return false;
            // The original living specimen/display root stays authoritative inside the new opening.
            var dome = station.Find("Crystal incubator dome");
            dome.localPosition = new Vector3(0, 1.48f, 0);
            dome.localScale = new Vector3(1.48f, 1.04f, 1.05f);
            var fluid = station.Find("Incubator fluid");
            fluid.localPosition = new Vector3(0, 1.48f, .015f);
            fluid.localScale = new Vector3(1.38f, .96f, .98f);
            LabEquipmentLabels(station, "INCUBATOR", "HATCHERY", status[StationKind.Incubator], -.725f, .646f, .88f);
            hatcheryIdle = RedesignMaterial("RW_Sky");
            hatcheryGrowing = RedesignMaterial("RW_Brass");
            hatcheryReady = RedesignMaterial("RW_Mint");
            hatcheryLamp = Shape(station, "Hatchery state lamp", PrimitiveType.Sphere,
                new Vector3(.62f, .94f, -.69f), new Vector3(.09f, .09f, .025f), hatcheryIdle).GetComponent<MeshRenderer>();
            return true;
        }

        void RefreshHatcheryLamp(IncubatorPhase phase)
        {
            if (!hatcheryLamp) return;
            hatcheryLamp.sharedMaterial = DioramaFor(phase == IncubatorPhase.Ready ? hatcheryReady :
                phase == IncubatorPhase.Incubating ? hatcheryGrowing : hatcheryIdle);
        }

        void BuildResearchBenchVisual(Transform station)
        {
            if (!AttachAuthored(station, "LabRefreshKit", "ResearchBench", "Upgrade plinth", "Upgrade top",
                "Upgrade feature ring")) return;
            // Spark cells, glass and stock text keep their existing gameplay owner and transforms.
            foreach (Transform child in station)
            {
                var label = child.GetComponent<TextMesh>();
                if (label && label.text == "4  >  8") child.gameObject.SetActive(false);
            }
            LabEquipmentLabels(station, "RESEARCH / SIPHON", "RESEARCH", status[StationKind.Upgrade], -.620f, .65f, 1.02f);
            researchHint.transform.localPosition = new Vector3(0, 2.144f, .375f);
            researchHint.color = new Color(.08f,.22f,.21f).linear;
            researchHint.fontStyle = FontStyle.Bold;
            foreach (var fit in labels)
                if (fit.Text == researchHint) { fit.Height = .056f; fit.Width = .70f; FitLabel(fit); break; }
            sparkStatus.color = new Color(.08f,.22f,.21f).linear;
            sparkStatus.fontStyle = FontStyle.Bold;
            var stockPlate = sparkModule.Find("Spark stock plaque");
            stockPlate.GetComponent<MeshRenderer>().sharedMaterial = RedesignMaterial("RW_Cream");
            stockPlate.localPosition = new Vector3(0, 1.99f, -.34f);
            sparkStatus.transform.localPosition = new Vector3(0, 1.99f, -.365f);
        }

        void LabEquipmentLabels(Transform station, string oldTitle, string title, TextMesh state,
            float depth, float titleHeight, float statusHeight)
        {
            foreach (Transform child in station)
            {
                if (child.name == "Machine faceplate" || child.name == "Identification stripe")
                    child.gameObject.SetActive(false);
                var label = child.GetComponent<TextMesh>();
                if (!label) continue;
                if (label.text.StartsWith("03 /")) { child.gameObject.SetActive(false); continue; }
                if (label != state && label.text != oldTitle) continue;
                bool isStatus = label == state;
                if (!isStatus) label.text = title;
                child.localPosition = new Vector3(0, isStatus ? statusHeight : titleHeight, depth);
                label.color = new Color(.08f, .22f, .21f).linear;
                label.fontStyle = FontStyle.Bold;
                foreach (var fit in labels)
                    if (fit.Text == label) { fit.Height = isStatus ? .055f : .065f; fit.Width = isStatus ? 1.32f : .50f; FitLabel(fit); break; }
            }
            ArtBox(station, "Equipment status strip", new Vector3(0, statusHeight, depth + .018f),
                new Vector3(1.42f, .10f, .025f), RedesignMaterial("RW_Cream"));
        }

        void BuildCollectorVisual()
        {
            if (!AttachAuthored(toolBody, "CollectorKit", "CollectorBody", "Soft grip", "Base coupling")) return;
            foreach (Transform child in toolNozzle)
                if (child.name == "Barrel" || child.name == "Nozzle collar") child.gameObject.SetActive(false);
            AttachAuthored(ToolChamber, "CollectorKit", "CollectorCap");
            toolBody.Find("Siphon readout panel").gameObject.SetActive(false);
            toolReadout.transform.localPosition = new Vector3(0, -.129f, -.182f);
            foreach (var fit in labels)
                if (fit.Text == toolReadout) { fit.Height = .030f; fit.Width = .135f; FitLabel(fit); break; }
        }
    }
}
