using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>A brief original glove gesture. Presentation only; never moves the camera or hit target.</summary>
    public sealed class SlapHand : MonoBehaviour
    {
        public bool IsPlaying => remaining > 0f;
        public float Remaining => remaining;
        public float Elapsed => duration-remaining;
        public bool IsWhiff { get; private set; }
        public event System.Action ContactReached;
        public int ContactCount { get; private set; }
        public int ContactFrameCount { get; private set; }
        public float ContactRealtime { get; private set; }
        bool contactPlayed;

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

        public bool Play(bool reduced = false) => Begin(reduced,false);
        public bool PlayWhiff(bool reduced = false) => Begin(reduced,true);
        bool Begin(bool reduced,bool whiff)
        {
            if (!initialized || !isActiveAndEnabled || paused || !glove || IsPlaying) return false;
            this.reduced = reduced;IsWhiff=whiff;contactPlayed=false;
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
            if(!IsWhiff && !contactPlayed && Elapsed>=FxComedyMotion.SlapContactSeconds(reduced)){
                contactPlayed=true;ContactCount++;ContactFrameCount=Time.frameCount;ContactRealtime=Time.realtimeSinceStartup;
                ContactReached?.Invoke();
            }
            if (!IsPlaying) { Hide(); return; }
            ApplyPose();
        }

        void ApplyPose()
        {
            FxComedyMotion.SampleSlapHand(Elapsed,reduced,IsWhiff,out var position,out var rotation,out var scale);
            glove.localPosition=position;glove.localRotation=rotation;glove.localScale=scale;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PreviewSlapForValidation(float elapsed,bool reduced,bool whiff)
        {
            if(!glove)return;
            FxComedyMotion.SampleSlapHand(elapsed,reduced,whiff,out var position,out var rotation,out var scale);
            glove.localPosition=position;glove.localRotation=rotation;glove.localScale=scale;
        }
#endif
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
