using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// A kit creature (AD-B's family rigs, GR-12B's body plans): built by <see cref="BuildKit"/> and posed each frame by
    /// <see cref="AnimateSpecies"/> (CreatureVisualDiorama.cs, PondletMotion.cs). Animation never changes simulation state.
    /// GR-17 removed the classic game's procedural placeholder creatures that shared this class.
    /// </summary>
    public sealed partial class CreatureVisual : MonoBehaviour
    {
        Transform tail;
        Transform finLeft;
        Transform finRight;
        Transform crest;
        string species;

        /// <summary>Absolute local detail pose; the container alone moves the creature root.</summary>
        public void AnimateSpecies(float time, float effort, bool reduced)
        {
            if (!CreatureMotion.Finite(time)) time = 0;
            effort = CreatureMotion.Finite(effort) ? Mathf.Clamp01(effort) : 0;
            if (species == "pondlet") { AnimatePondlet(time, effort, reduced); return; }
            if (IsDiorama) AnimateDioramaSpecies(time, effort, reduced);
        }

        static void ReleaseCreatureAsset(UnityEngine.Object asset)
        {
            if (!asset) return;
            if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
        }

        void OnDestroy() => ReleaseDioramaMaterials();
    }
}
