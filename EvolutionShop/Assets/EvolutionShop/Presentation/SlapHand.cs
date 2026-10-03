using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>A brief original glove gesture. Presentation only; never moves the camera or hit target.</summary>
    public sealed class SlapHand : MonoBehaviour
    {
        public bool IsPlaying => remaining > 0f;
        public float Remaining => remaining;

        Transform glove;
        Material gloveMaterial, cuffMaterial;
        bool initialized, paused, reduced;
        float remaining, duration;

        public void Initialize(Camera camera)
        {
            if (initialized || !camera) return;
            glove = new GameObject("First person cartoon slap glove").transform;
            glove.SetParent(camera.transform, false);
            gloveMaterial = ShopWorld.MakeMaterial("Slap glove / warm cream", new Color(.98f, .92f, .72f), 0, .45f);
            cuffMaterial = ShopWorld.MakeMaterial("Slap glove / mint cuff", new Color(.16f, .48f, .43f), 0, .4f);
            Shape("Rounded palm", PrimitiveType.Sphere, Vector3.zero, new Vector3(.21f, .24f, .075f), gloveMaterial);
            Shape("Soft cuff", PrimitiveType.Cube, new Vector3(0, -.15f, 0), new Vector3(.16f, .085f, .09f), cuffMaterial);
            for (int i = 0; i < 4; i++)
            {
                float x = -.075f + i * .05f;
                float y = i == 1 || i == 2 ? .17f : .145f;
                Shape("Glove finger " + (i + 1), PrimitiveType.Capsule, new Vector3(x, y, 0), new Vector3(.048f, .083f, .06f), gloveMaterial);
            }
            Shape("Rounded thumb", PrimitiveType.Capsule, new Vector3(.13f, .005f, 0), new Vector3(.065f, .08f, .065f), gloveMaterial).localRotation = Quaternion.Euler(0, 0, -45f);
            initialized = true;
            Hide();
        }

        Transform Shape(string label, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            // ShopWorld.Shape immediately disables the primitive collider before scheduling its removal.
            var part = ShopWorld.Shape(glove, label, type, position, scale, material);
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part;
        }

        public bool Play(bool reduced = false)
        {
            if (!initialized || !isActiveAndEnabled || paused || !glove || IsPlaying) return false;
            this.reduced = reduced;
            duration = reduced ? .18f : .30f;
            remaining = duration;
            glove.gameObject.SetActive(true);
            ApplyPose();
            return true;
        }

        public void SetPaused(bool value) => paused = value;
        public void Cancel() => Hide();

        void Update()
        {
            if (paused || !IsPlaying || !glove) return;
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (!IsPlaying) { Hide(); return; }
            ApplyPose();
        }

        void ApplyPose()
        {
            float progress = 1f - remaining / duration;
            float sweep = Mathf.Sin(progress * Mathf.PI);
            Vector3 start = reduced ? new Vector3(.38f, -.26f, .52f) : new Vector3(.58f, -.24f, .58f);
            Vector3 contact = reduced ? new Vector3(.30f, -.20f, .55f) : new Vector3(.10f, -.02f, .60f);
            glove.localPosition = Vector3.Lerp(start, contact, sweep);
            glove.localRotation = reduced
                ? Quaternion.Euler(0, -12f, -15f + sweep * 8f)
                : Quaternion.Euler(10f * (1f - sweep), Mathf.Lerp(-35f, 12f, sweep), Mathf.Lerp(-55f, 25f, sweep));
            glove.localScale = Vector3.one * (reduced ? .60f : .70f);
        }

        void Hide()
        {
            remaining = 0f;
            if (glove) glove.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (glove) Destroy(glove.gameObject);
            if (gloveMaterial) Destroy(gloveMaterial);
            if (cuffMaterial) Destroy(cuffMaterial);
        }
    }
}
