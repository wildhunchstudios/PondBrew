using UnityEditor;
using UnityEditor.SceneManagement;

namespace EvolutionShop.Editor
{
    /// <summary>
    /// GR-02: plays the grow-from-nothing greybox in the Editor (the shop scene, with the greybox asked for in place of
    /// the classic session, its own save file). The request lasts until Play mode ends, so the next Play is the classic game.
    /// </summary>
    public static class GrowthGreyboxMenu
    {
        [MenuItem("Evolution Shop/GR/Play the GR-02 greybox")]
        public static void Play()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SessionState.SetBool(GrowthGreybox.EditorFlag, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void ClearAfterPlay()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(GrowthGreybox.EditorFlag, false);
            };
        }
    }
}
