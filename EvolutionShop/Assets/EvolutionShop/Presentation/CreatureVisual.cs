using UnityEngine;

namespace EvolutionShop
{
    /// <summary>Original smooth procedural placeholder. Animation never changes simulation state.</summary>
    public sealed class CreatureVisual : MonoBehaviour
    {
        Transform tail;
        Transform finLeft;
        Transform finRight;
        Transform crest;
        string species;
        float phase;

        public void Build(string speciesId)
        {
            species = speciesId;
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
            float frequency = species == "bubblebelly" ? 4.1f : species == "ribbonfin" ? 3.5f : 6.8f;
            float wave = Mathf.Sin(time * frequency + phase);
            if (tail) tail.localRotation = Quaternion.Euler(0, wave * (species == "ribbonfin" ? 26 : 19), 0);
            if (finLeft) finLeft.localRotation = Quaternion.Euler(wave * 12, -18, -20 + wave * 22);
            if (finRight) finRight.localRotation = Quaternion.Euler(wave * -12, 18, 20 - wave * 22);
            if (crest) crest.localRotation = Quaternion.Euler(0, 0, wave * 7);
        }
    }
}
