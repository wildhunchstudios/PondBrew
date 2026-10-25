using UnityEditor;
using UnityEditor.SceneManagement;

namespace EvolutionShop.Editor
{
    /// <summary>
    /// GR-14 (GR.43): Play in the Editor plays the game, the grow-from-nothing shop. The classic game stays behind a switch:
    /// its menu asks for it until Play mode ends, so the next Play is the game again.
    /// </summary>
    public static class GrowthGreyboxMenu
    {
        [MenuItem("Evolution Shop/Play the game")]
        public static void Play() => Enter(false);

        [MenuItem("Evolution Shop/Play the classic game")]
        public static void PlayClassic() => Enter(true);

        static void Enter(bool classic)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SessionState.SetBool(GrowthGreybox.ClassicEditorFlag, classic);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void ClearAfterPlay()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(GrowthGreybox.ClassicEditorFlag, false);
            };
        }
    }
}
