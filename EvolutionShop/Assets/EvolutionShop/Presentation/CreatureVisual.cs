using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Original smooth procedural placeholder. Animation never changes simulation state.</summary>
    public sealed partial class CreatureVisual : MonoBehaviour
    {
        Transform tail;
        Transform finLeft;
        Transform finRight;
        Transform crest;
        string species;
        float phase;
        Transform lanternBulb;
        Material[] lanternMaterials;
        Material[] driftwingMaterials;

        public void Build(string speciesId)
        {
            species = speciesId;
            if (speciesId == "pondlet") { BuildPondlet(); return; }
            if (speciesId == "lanternfin") { BuildLanternfin(); return; }
            if (speciesId == "driftwing") { BuildDriftwing(); return; }
            phase = speciesId == "pondlet" ? 0 : speciesId == "bubblebelly" ? 1.7f : 3.5f;
            bool round = speciesId == "bubblebelly";
            bool ribbon = speciesId == "ribbonfin";
            var bodyColor = round ? new Color(1f, 0.72f, 0.24f) : ribbon ? new Color(0.19f, 0.81f, 0.83f) : new Color(1f, 0.47f, 0.38f);
            var body = ShopWorld.MakeMaterial("Creature " + speciesId, bodyColor, 0.07f, 0.65f);
            var fin = ShopWorld.MakeMaterial("Soft fins " + speciesId, Color.Lerp(bodyColor, Color.white, 0.38f), 0.03f, 0.45f);
            var cream = ShopWorld.MakeMaterial("Warm creature belly", new Color(1f, 0.96f, 0.82f), 0, 0.35f);
            var dark = ShopWorld.MakeMaterial("Creature pupils", new Color(0.013f, 0.055f, 0.075f), 0, 0.8f);
            Vector3 bodyScale = round ? new Vector3(0.74f, 0.7f, 0.77f) : ribbon ? new Vector3(0.31f, 0.3f, 1.1f) : new Vector3(0.48f, 0.41f, 0.74f);
            ShopWorld.Shape(transform, "Silky body", PrimitiveType.Sphere, Vector3.zero, bodyScale, body);
            ShopWorld.Shape(transform, "Cream belly", PrimitiveType.Sphere, new Vector3(0, -bodyScale.y * 0.16f, -0.045f), Vector3.Scale(bodyScale, new Vector3(0.86f, 0.78f, 0.87f)), cream);
            var tailPivot = new GameObject("Animated tail").transform;
            tailPivot.SetParent(transform, false);
            tailPivot.localPosition = new Vector3(0, 0, bodyScale.z * 0.36f);
            tail = tailPivot;
            if (ribbon)
            {
                ShopWorld.Shape(tail, "Long ribbon upper", PrimitiveType.Sphere, new Vector3(0, 0.105f, 0.35f), new Vector3(0.055f, 0.18f, 0.78f), fin).localRotation = Quaternion.Euler(-13, 0, 0);
                ShopWorld.Shape(tail, "Long ribbon lower", PrimitiveType.Sphere, new Vector3(0, -0.105f, 0.29f), new Vector3(0.05f, 0.15f, 0.64f), body).localRotation = Quaternion.Euler(14, 0, 0);
            }
            else
            {
                ShopWorld.Shape(tail, "Upper tail paddle", PrimitiveType.Sphere, new Vector3(0, 0.09f, 0.18f), new Vector3(0.07f, round ? 0.21f : 0.31f, 0.32f), fin).localRotation = Quaternion.Euler(-30, 0, 0);
                ShopWorld.Shape(tail, "Lower tail paddle", PrimitiveType.Sphere, new Vector3(0, -0.09f, 0.18f), new Vector3(0.065f, round ? 0.21f : 0.29f, 0.30f), fin).localRotation = Quaternion.Euler(30, 0, 0);
            }
            float eyeX = bodyScale.x * 0.31f;
            float eyeZ = -bodyScale.z * 0.34f;
            for (int side = -1; side <= 1; side += 2)
            {
                ShopWorld.Shape(transform, "Bright eye", PrimitiveType.Sphere, new Vector3(side * eyeX, 0.07f, eyeZ), Vector3.one * (round ? 0.16f : 0.125f), cream);
                ShopWorld.Shape(transform, "Curious pupil", PrimitiveType.Sphere, new Vector3(side * eyeX, 0.07f, eyeZ - 0.052f), Vector3.one * (round ? 0.085f : 0.064f), dark);
                ShopWorld.Shape(transform, "Eye highlight", PrimitiveType.Sphere, new Vector3(side * eyeX - 0.012f, 0.089f, eyeZ - 0.088f), Vector3.one * 0.021f, cream);
            }
            finLeft = ShopWorld.Shape(transform, "Left fin", PrimitiveType.Sphere, new Vector3(-bodyScale.x * 0.44f, -0.06f, 0.09f), new Vector3(round ? 0.22f : 0.28f, 0.05f, ribbon ? 0.31f : 0.19f), fin);
            finRight = ShopWorld.Shape(transform, "Right fin", PrimitiveType.Sphere, new Vector3(bodyScale.x * 0.44f, -0.06f, 0.09f), new Vector3(round ? 0.22f : 0.28f, 0.05f, ribbon ? 0.31f : 0.19f), fin);
            crest = ShopWorld.Shape(transform, "Soft dorsal fin", PrimitiveType.Sphere, new Vector3(0, bodyScale.y * 0.44f, 0.11f), new Vector3(0.06f, ribbon ? 0.25f : 0.16f, ribbon ? 0.52f : 0.25f), fin);
            if (round)
            {
                for (int i = 0; i < 3; i++)
                    ShopWorld.Shape(transform, "Belly freckles", PrimitiveType.Sphere, new Vector3((i - 1) * 0.095f, -0.1f, -0.352f), new Vector3(0.043f, 0.047f, 0.014f), body);
            }
        }

        public void Animate(float time)
        {
            if (species == "driftwing")
            {
                float wingWave = Mathf.Sin(time * 3.8f + .4f);
                if (finLeft) finLeft.localRotation = Quaternion.Euler(wingWave * 14, -12, -18 + wingWave * 17);
                if (finRight) finRight.localRotation = Quaternion.Euler(-wingWave * 14, 12, 18 - wingWave * 17);
                if (tail) tail.localRotation = Quaternion.Euler(0, wingWave * 13, wingWave * 5);
                if (crest) crest.localRotation = Quaternion.Euler(0, 0, wingWave * 6);
                return;
            }
            if (species == "lanternfin")
            {
                float flutter = Mathf.Sin(time * 4.6f + 1.1f);
                if (tail) tail.localRotation = Quaternion.Euler(0, flutter * 16, 0);
                if (finLeft) finLeft.localRotation = Quaternion.Euler(flutter * 7, -12, -16 + flutter * 14);
                if (finRight) finRight.localRotation = Quaternion.Euler(-flutter * 7, 12, 16 - flutter * 14);
                if (crest) crest.localRotation = Quaternion.Euler(0, 0, flutter * 5);
                if (lanternBulb) lanternBulb.localScale = Vector3.one * (.20f + Mathf.Sin(time * 2.2f) * .012f);
                return;
            }
            float frequency = species == "bubblebelly" ? 4.1f : species == "ribbonfin" ? 3.5f : 6.8f;
            float wave = Mathf.Sin(time * frequency + phase);
            if (tail) tail.localRotation = Quaternion.Euler(0, wave * (species == "ribbonfin" ? 26 : 19), 0);
            if (finLeft) finLeft.localRotation = Quaternion.Euler(wave * 12, species == "pondlet" ? 162 : -18, -20 + wave * 22);
            if (finRight) finRight.localRotation = Quaternion.Euler(wave * -12, 18, 20 - wave * 22);
            if (crest) crest.localRotation = Quaternion.Euler(0, 0, wave * 7);
        }

        void BuildLanternfin()
        {
            var body = ShopWorld.MakeMaterial("Lanternfin lavender body", new Color(.56f, .39f, .83f), .05f, .63f);
            var fin = ShopWorld.MakeMaterial("Lanternfin soft wing fins", new Color(.80f, .66f, .96f), .03f, .45f);
            var cream = ShopWorld.MakeMaterial("Lanternfin pale belly", new Color(1f, .94f, .78f), 0, .35f);
            var pupil = ShopWorld.MakeMaterial("Lanternfin curious pupils", new Color(.045f, .025f, .09f), 0, .7f);
            var light = ShopWorld.MakeMaterial("Lanternfin warm lantern", new Color(1f, .83f, .27f), .02f, .42f);
            light.EnableKeyword("_EMISSION");
            light.SetColor("_EmissionColor", new Color(.32f, .16f, .025f));
            lanternMaterials = new[] { body, fin, cream, pupil, light };
            ShopWorld.Shape(transform, "Compact lantern body", PrimitiveType.Sphere, Vector3.zero, new Vector3(.45f, .4f, .65f), body);
            ShopWorld.Shape(transform, "Lanternfin cream belly", PrimitiveType.Sphere, new Vector3(0, -.066f, -.045f), new Vector3(.39f, .29f, .55f), cream);
            tail = new GameObject("Lanternfin short tail").transform;
            tail.SetParent(transform, false); tail.localPosition = new Vector3(0, 0, .25f);
            ShopWorld.Shape(tail, "Rounded lantern tail paddle", PrimitiveType.Sphere, new Vector3(0, 0, .17f), new Vector3(.075f, .27f, .26f), fin);
            for (int side = -1; side <= 1; side += 2)
            {
                ShopWorld.Shape(transform, "Lanternfin bright eye", PrimitiveType.Sphere, new Vector3(side * .145f, .065f, -.215f), Vector3.one * .13f, cream);
                ShopWorld.Shape(transform, "Lanternfin pupil", PrimitiveType.Sphere, new Vector3(side * .145f, .065f, -.270f), Vector3.one * .067f, pupil);
                ShopWorld.Shape(transform, "Lanternfin eye glint", PrimitiveType.Sphere, new Vector3(side * .145f - .013f, .083f, -.3f), Vector3.one * .021f, cream);
            }
            finLeft = ShopWorld.Shape(transform, "Lanternfin left wing", PrimitiveType.Sphere, new Vector3(-.27f, -.045f, .08f), new Vector3(.38f, .07f, .38f), fin);
            finRight = ShopWorld.Shape(transform, "Lanternfin right wing", PrimitiveType.Sphere, new Vector3(.27f, -.045f, .08f), new Vector3(.38f, .07f, .38f), fin);
            crest = ShopWorld.Shape(transform, "Lanternfin back crest", PrimitiveType.Sphere, new Vector3(0, .19f, .11f), new Vector3(.07f, .13f, .3f), fin);
            ShopWorld.Shape(transform, "Short lantern stalk", PrimitiveType.Capsule, new Vector3(0, .235f, -.24f), new Vector3(.035f, .10f, .035f), body).localRotation = Quaternion.Euler(-20, 0, 0);
            lanternBulb = ShopWorld.Shape(transform, "Glowing forehead lantern", PrimitiveType.Sphere, new Vector3(0, .39f, -.29f), Vector3.one * .2f, light);
            Animate(0);
        }

        void BuildDriftwing()
        {
            var body = ShopWorld.MakeMaterial("Driftwing opal body", new Color(.40f, .79f, .84f), .04f, .55f);
            var wing = ShopWorld.MakeMaterial("Driftwing pearl sails", new Color(.74f, .98f, .92f), .04f, .45f);
            var belly = ShopWorld.MakeMaterial("Driftwing pale belly", new Color(1f, .94f, .82f), 0, .38f);
            var pupil = ShopWorld.MakeMaterial("Driftwing bright eyes", new Color(.04f, .10f, .13f), 0, .7f);
            driftwingMaterials = new[] { body, wing, belly, pupil };
            ShopWorld.Shape(transform, "Tapered Airfin body", PrimitiveType.Sphere, Vector3.zero, new Vector3(.43f, .34f, .66f), body);
            ShopWorld.Shape(transform, "Pearl chest", PrimitiveType.Sphere, new Vector3(0, -.08f, -.06f), new Vector3(.36f, .25f, .54f), belly);
            tail = new GameObject("Driftwing trailing tail").transform;
            tail.SetParent(transform, false); tail.localPosition = new Vector3(0, 0, .20f);
            for (int side = -1; side <= 1; side += 2)
            {
                ShopWorld.Shape(transform, "Driftwing eye", PrimitiveType.Sphere, new Vector3(side * .16f, .07f, -.24f), Vector3.one * .12f, belly);
                ShopWorld.Shape(transform, "Driftwing pupil", PrimitiveType.Sphere, new Vector3(side * .16f, .07f, -.29f), Vector3.one * .055f, pupil);
                ShopWorld.Shape(tail, "Trailing pearl ribbon", PrimitiveType.Sphere,
                    new Vector3(side * .10f, .02f, .23f), new Vector3(.055f, .12f, .45f), wing).localRotation = Quaternion.Euler(0, side * 13, 0);
            }
            finLeft = ShopWorld.Shape(transform, "Airfin left sail", PrimitiveType.Sphere,
                new Vector3(-.52f, .04f, .03f), new Vector3(.48f, .055f, .50f), wing);
            finRight = ShopWorld.Shape(transform, "Airfin right sail", PrimitiveType.Sphere,
                new Vector3(.52f, .04f, .03f), new Vector3(.48f, .055f, .50f), wing);
            crest = ShopWorld.Shape(transform, "Airfin dorsal sail", PrimitiveType.Sphere,
                new Vector3(0, .23f, .12f), new Vector3(.075f, .28f, .41f), wing);
            Animate(0);
        }

        void OnDestroy()
        {
            if (pondletFinMesh) Destroy(pondletFinMesh);
            if (pondletMaterials != null) foreach (var material in pondletMaterials) if (material) Destroy(material);
            if (driftwingMaterials != null) foreach (var material in driftwingMaterials) if (material) Destroy(material);
            if (lanternMaterials == null) return;
            // Storage may already have released these private materials when retiring a resident.
            foreach (var material in lanternMaterials) if (material) Destroy(material);
        }
    }
}
